using System;
using System.Threading.Tasks;
using Godot;

namespace Mandible;

public static class NestArtValidation
{
    public static async Task Capture(Main app, string directory)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            app.StartSkirmish(); var game = app.Session;
            game.Sim.Tick(1f / 30); game.SetPaused(true); game.SetNestView(true);
            game.Fog.RevealAll = true; // the black-floor check below must not count the fog of war's true black as a floor fault
            var home = game.World.Nests[0].Pos;
            game.Camera.UserControl = false; game.Camera.NestView = true; game.Camera.SetYaw(0, true);
            game.Camera.JumpTo(home + new Vector2(0, 5), 39);
            game.Input.SelectedRoom = game.Sim.ChamberOf(0, ChamberKind.Brood).Id;
            game.Sim.Enqueue(0, ProdKind.Soldier); game.Sim.Enqueue(0, ProdKind.Worker);
            async Task Shot(string name)
            {
                await app.ToSignal(app.GetTree().CreateTimer(3.5), SceneTreeTimer.SignalName.Timeout);
                await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var im = app.GetViewport().GetTexture().GetImage();
                if (im.SavePng(directory.PathJoin(name + ".png")) != Error.Ok) throw new InvalidOperationException("Capture failed");
                if (game.Hud.Visible)
                {
                    int dark = 0, samples = 0;
                    for (int y = (int)(im.GetHeight() * 0.08f); y < im.GetHeight() * 0.70f; y += 3)
                        for (int x = (int)(im.GetWidth() * 0.22f); x < im.GetWidth() * 0.90f; x += 3)
                        {
                            var c = im.GetPixel(x, y); samples++;
                            if (c.R < 0.035f && c.G < 0.035f && c.B < 0.035f) dark++;
                        }
                    float fraction = dark / (float)samples;
                    GD.Print($"[interior-capture] {name}: black world pixels {fraction:P2}");
                    if (fraction > 0.04f) throw new InvalidOperationException("Black floor artifact detected; inspect " + name);
                }
                GD.Print($"[interior-capture] {name}; fps {Engine.GetFramesPerSecond()}; objects {Performance.GetMonitor(Performance.Monitor.ObjectCount)}");
            }
            await Shot("01-colony-interiors");
            game.Hud.Visible = false;
            game.Camera.JumpTo(home + new Vector2(5, 8), 24); await Shot("02-brood-royal-detail");
            game.Camera.JumpTo(home + new Vector2(4, -6), 24); await Shot("03-storage-forge-detail");
            game.Camera.JumpTo(home + new Vector2(-10, 3), 24); await Shot("04-wings-silk-detail");
            game.Camera.SetYaw(0.75f, true); game.Camera.JumpTo(home + new Vector2(7, 3), 28); await Shot("05-rotated-detail");
            game.Camera.SetYaw(0, true); game.Camera.JumpTo(home + new Vector2(0, 5), 39); game.Hud.Visible = true;
            Settings.Gfx = Settings.Quality.Low; game.ApplyQuality(); await Shot("06-low-quality");
            Settings.Gfx = Settings.Quality.High; game.ApplyQuality(); await Shot("07-high-restored");
            Settings.Gfx = Settings.Quality.Low; game.ApplyQuality(); await Shot("08-low-repeated");
            Settings.Gfx = Settings.Quality.High; game.ApplyQuality(); await Shot("09-high-final");
            NestInterior.ValidateInteriorMaterials();
            GD.Print("[interior-capture] PASS all chamber materials compiled; all views and Low quality captured (visual review required)");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[interior-capture] FAIL " + e); app.GetTree().Quit(1); }
    }

    /// --brood-capture=<dir>: waits in a live game for the queen to lay, a nurse to carry an egg and an ant to hatch,
    /// freezes each moment and captures it, and checks that the beds follow the queue and the jaws hold what is carried.
    public static async Task Brood(Main app, string directory)
    {
        static void Check(bool ok, string message) { if (!ok) throw new InvalidOperationException(message); }
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            app.StartSkirmish(); var game = app.Session;
            game.SetNestView(true); game.Hud.Visible = false;
            game.Camera.UserControl = false; game.Camera.NestView = true; game.Camera.SetYaw(0, true);
            var sim = game.Sim; var colony = sim.Col[0]; var brood = game.Interior;
            var royal = sim.ChamberPos(0, sim.Royal(0));
            game.Camera.JumpTo(royal, 30);
            async Task Frame() => await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            async Task<bool> Until(Func<bool> done, float seconds)
            {
                ulong end = Time.GetTicksMsec() + (ulong)(seconds * 1000);
                while (Time.GetTicksMsec() < end) { if (done()) return true; await Frame(); }
                return done();
            }
            async Task Shot(string name, Vector2 at, float distance)
            {
                game.SetPaused(true); game.Camera.JumpTo(at, distance);
                await app.ToSignal(app.GetTree().CreateTimer(1.2), SceneTreeTimer.SignalName.Timeout);
                await Frame();
                Check(game.Ants.VisibleBroodCargo == brood.NursesCarrying, "Brood in the jaws does not match the nurses carrying");
                using var im = app.GetViewport().GetTexture().GetImage();
                Check(im.SavePng(directory.PathJoin(name + ".png")) == Error.Ok, "Brood capture failed");
                var held = brood.BroodHeld;
                GD.Print($"[brood-capture] {name}; nurses {brood.NurseCount} ({brood.NursesCarrying} carrying), laid waiting {brood.LaidWaiting}, beds {held.eggs} eggs / {held.larvae} larvae / {held.cocoons} cocoons, queue {colony.Queue.Count}, fps {Engine.GetFramesPerSecond()}");
                game.SetPaused(false);
            }
            colony.Queue.Clear();
            float warm = sim.Time + 3; // the first frames compile shaders
            await Until(() => sim.Time > warm, 90);
            var empty = brood.BroodHeld;
            Check(brood.NurseCount > 0, $"No nurses in a founded colony with a nursery (founded {colony.Founded}, brood rooms {sim.ChamberCount(0, ChamberKind.Brood, true)}, workers {colony.Count[(int)AntType.Worker]})");
            colony.Food = 5000;
            for (int i = 0; i < 4; i++) sim.Enqueue(0, ProdKind.Worker);
            sim.Enqueue(0, ProdKind.Soldier);
            for (int i = 0; i < 3; i++) sim.Enqueue(0, ProdKind.Worker);
            warm = sim.Time + 0.3f;
            await Until(() => sim.Time > warm, 10);
            var queued = brood.BroodHeld;
            Check(queued.eggs > empty.eggs, $"Beds did not fill with the queue: {empty.eggs} -> {queued.eggs} eggs");
            GD.Print($"[brood-capture] beds follow the queue: {empty.eggs}/{empty.larvae}/{empty.cocoons} empty -> {queued.eggs}/{queued.larvae}/{queued.cocoons} with {colony.Queue.Count} queued");
            Check(await Until(() => brood.SinceLaid > 0.5f && brood.SinceLaid < 0.62f, 25), "The queen never laid");
            await Shot("01-queen-laying", brood.LastLaid, 8);
            Check(await Until(() => brood.CarryingEgg != null, 60), "No nurse ever carried a laid egg to a nursery");
            await Shot("02-nurse-carrying-egg", brood.CarryingEgg.Value, 7);
            colony.Food = 5000;
            for (int i = 0; i < 3; i++) sim.Enqueue(0, ProdKind.Worker); // the carry may have taken the whole queue
            Check(await Until(() => brood.Emerging is { age: > 0.8f and < 1.1f }, 40), "No ant hatched in a nursery");
            var hatch = brood.Emerging.Value.pos;
            await Shot("03-hatch-emerging", hatch, 5.5f);
            Check(await Until(() => brood.Emerging == null || brood.Emerging.Value.age > 2.4f, 5), "Emergence never finished");
            await Shot("04-callow-walking", hatch, 8);
            await Shot("05-nursery-overview", sim.ChamberPos(0, sim.ChamberOf(0, ChamberKind.Brood)), 20);
            Settings.Gfx = Settings.Quality.Low; game.ApplyQuality();
            await Until(() => false, 1f);
            Check(brood.NurseCount <= 2, "Low quality animates too many nurses");
            await Shot("06-low-quality", sim.ChamberPos(0, sim.ChamberOf(0, ChamberKind.Brood)), 20);
            Settings.Gfx = Settings.Quality.High; game.ApplyQuality();
            GD.Print("[brood-capture] PASS laying, nurse carrying, hatch emergence, beds follow the queue, Low tier (visual review required)");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[brood-capture] FAIL " + e); app.GetTree().Quit(1); }
    }
}
