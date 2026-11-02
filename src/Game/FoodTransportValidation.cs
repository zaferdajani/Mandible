using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Mandible;

public static class FoodTransportValidation
{
    static void Check(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
    static List<int> Prepare(Sim sim, int team = 0)
    {
        var workers = new List<int>();
        for (int i = 0; i < sim.High; i++)
        {
            ref var ant = ref sim.Ants[i];
            if (!ant.Alive || ant.Type != AntType.Worker) continue;
            ant.State = AntState.Hold; ant.Carrying = false; ant.DigP1 = 0; ant.Assigned = ant.FoodTarget = null;
            if (ant.Team == team && workers.Count < 5) workers.Add(i);
        }
        Check(workers.Count == 5, "Five worker fixtures are required");
        sim.W.Food.Clear(); sim.W.Obstacles.Clear(); sim.W.IndexObstacles();
        sim.Col[team].Food = 0; sim.Col[team].Gathered = 0;
        var home = sim.NestEntrance(team);
        for (int kind = 0; kind < workers.Count; kind++)
        {
            ref var ant = ref sim.Ants[workers[kind]];
            var pos = home + new Vector2(-12 + kind * 1.4f, 7.5f + kind % 2 * 1.1f);
            ant.Pos = ant.PrevPos = pos; ant.Layer = 0; ant.Heading = ant.PrevHeading = -0.6f;
            ant.Target = -1; ant.State = AntState.Gather;
            var food = new FoodSource { Id = sim.W.NextFoodId++, Kind = (FoodKind)kind, Pos = pos, Amount = 1, Max = 1, Radius = 0.6f };
            sim.W.Food.Add(food); ant.Assigned = ant.FoodTarget = food;
        }
        return workers;
    }

    public static void Run(Main app)
    {
        try
        {
            Settings.Gfx = Settings.Quality.Low;
            var root = new Node3D(); app.AddChild(root);
            var sim = new Sim(WorldBuilder.Build(root, 20261031)); sim.BeginSkirmish();
            for (int team = 0; team < 2; team++)
            {
                var workers = Prepare(sim, team); var delivery = sim.FoodDeliveryPoint(team);
                Check(delivery.layer == Sim.NestLayer(team), "Completed storage must receive food underground");
                sim.Tick(1f / 30);
                var inside = new bool[5]; var delivered = new bool[5];
                float expected = 0;
                for (int kind = 0; kind < 5; kind++)
                {
                    var ant = sim.Ants[workers[kind]];
                    Check(ant.Carrying && ant.Layer == 0 && ant.CarryKind == (FoodKind)kind, "Pickup did not retain the correct surface cargo");
                    expected += Config.FoodValue((FoodKind)kind) * sim.Col[team].FoodMult *
                        (Config.Favoured(sim.Col[team].Race, (FoodKind)kind) ? Config.FoodBonus(sim.Col[team].Race) : 1f);
                }
                Check(sim.Col[team].Food == 0, "Pickup credited food before delivery");
                var before = new Ant[5];
                for (int tick = 0; tick < 90 * 30; tick++)
                {
                    for (int kind = 0; kind < 5; kind++) before[kind] = sim.Ants[workers[kind]];
                    sim.Tick(1f / 30);
                    for (int kind = 0; kind < workers.Count; kind++)
                    {
                        var ant = sim.Ants[workers[kind]];
                        if (ant.Carrying && ant.Layer == delivery.layer) inside[kind] = true;
                        if (!ant.Carrying && !delivered[kind])
                        {
                            Check(inside[kind], "Cargo vanished before entering the nest");
                            // Delivery precedes locomotion in a tick: an empty worker
                            // may already have taken its first step back to the shaft.
                            Check(before[kind].Layer == delivery.layer && before[kind].Pos.DistanceTo(delivery.pos) < 1.101f, $"Food {kind} was deposited away from storage");
                            delivered[kind] = true;
                        }
                    }
                    if (delivered[0] && delivered[1] && delivered[2] && delivered[3] && delivered[4]) break;
                }
                for (int kind = 0; kind < 5; kind++) Check(delivered[kind], $"Food {kind} never arrived at storage");
                Check(Mathf.Abs(sim.Col[team].Food - expected) < 0.001f, "Delivery changed the food value or credited twice");
                for (int tick = 0; tick < 30 * 30; tick++) sim.Tick(1f / 30);
                foreach (int id in workers) Check(!sim.Ants[id].Carrying && sim.Ants[id].Layer == 0, "Worker did not exit empty handed");
                Check(Mathf.Abs(sim.Col[team].Food - expected) < 0.001f, "Empty return credited food again");
                GD.Print($"[transport-test] PASS team {team}: five food types picked up, carried outside/inside, deposited once, and workers exited empty");
            }
            foreach (var room in sim.Col[0].Chambers) if (room.Kind == ChamberKind.Storage) room.Built = false;
            var fallback = sim.FoodDeliveryPoint(0);
            Check(fallback.layer == Sim.NestLayer(0) && fallback.pos == sim.ChamberPos(0, sim.Royal(0)), "No-store founding fallback must use royal chamber");
            sim.Col[0].Founded = false;
            Check(sim.FoodDeliveryPoint(0).layer == 0, "Undug colony cannot receive underground food");
            GD.Print("[transport-test] ALL PASS including temporary royal larder and undug entrance");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[transport-test] FAIL " + e); app.GetTree().Quit(1); }
    }

    public static async Task Capture(Main app, string directory)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            app.StartSkirmish(); var game = app.Session; game.SetPaused(true);
            var workers = Prepare(game.Sim); game.Sim.Tick(1f / 30);
            game.Camera.UserControl = false; game.Camera.SetYaw(0, true); game.Hud.Visible = false;
            var home = game.Sim.NestEntrance(0);
            async Task Shot(string name)
            {
                await app.ToSignal(app.GetTree().CreateTimer(2.5), SceneTreeTimer.SignalName.Timeout);
                await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                int expected = 0; foreach (int id in workers) if (game.Sim.Ants[id].Carrying) expected++;
                Check(game.Ants.VisibleCargoCount == expected, "Rendered cargo count does not match actual carried food");
                foreach (FoodKind kind in Enum.GetValues<FoodKind>()) Check(WorkerCargo.Material(kind).Shader.GetShaderUniformList().Count > 0, "Cargo shader did not compile");
                using var im = app.GetViewport().GetTexture().GetImage();
                Check(im.SavePng(directory.PathJoin(name + ".png")) == Error.Ok, "Cargo capture failed");
                GD.Print($"[transport-capture] {name}; {expected} actual loads rendered");
            }
            game.SetNestView(false); game.Camera.NestView = false;
            game.Camera.JumpTo(home + new Vector2(-9.2f, 8), 14);
            await Shot("01-food-carried-outside");
            int inside = 0;
            for (int tick = 0; tick < 60 * 30 && inside < 2; tick++)
            {
                game.Sim.Tick(1f / 30); inside = 0;
                foreach (int id in workers) if (game.Sim.Ants[id].Carrying && game.Sim.Ants[id].Layer > 0 && game.Sim.Ants[id].Pos.DistanceTo(home) > 4.0f) inside++;
            }
            Check(inside >= 2, "Workers never carried loads through the galleries");
            game.SetNestView(true); game.Camera.NestView = true;
            var focus = Vector2.Zero; int focused = 0;
            foreach (int id in workers) if (game.Sim.Ants[id].Carrying && game.Sim.Ants[id].Layer > 0) { focus += game.Sim.Ants[id].Pos; focused++; }
            game.Camera.JumpTo(focus / focused, 14);
            await Shot("02-food-carried-inside");
            Settings.Gfx = Settings.Quality.Low; game.ApplyQuality(); await Shot("03-food-carried-low-quality");
            for (int tick = 0; tick < 90 * 30; tick++)
            {
                game.Sim.Tick(1f / 30);
                bool loaded = false; foreach (int id in workers) loaded |= game.Sim.Ants[id].Carrying;
                if (!loaded) break;
            }
            foreach (int id in workers) Check(!game.Sim.Ants[id].Carrying, "Worker did not deposit its load");
            game.Camera.JumpTo(game.Sim.FoodDeliveryPoint(0).pos, 11);
            await Shot("04-food-deposited");
            GD.Print("[transport-capture] PASS actual pickup, both layers, Low quality, and visible cargo removal after deposit");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[transport-capture] FAIL " + e); app.GetTree().Quit(1); }
    }
}
