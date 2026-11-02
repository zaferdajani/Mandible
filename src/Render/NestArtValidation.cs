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
}
