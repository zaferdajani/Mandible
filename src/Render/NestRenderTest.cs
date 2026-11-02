using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Mandible;

/// Reproducible renderer regression: -- --nesttest=<capture directory>, or --nesttest=headless.
public static class NestRenderTest
{
    static void Check(bool ok, string message)
    {
        if (!ok) throw new InvalidOperationException(message);
    }

    static void CheckRoutes(NestGeometry geometry, IReadOnlyList<NestChamber> rooms)
    {
        foreach (var room in rooms)
        {
            var from = room.Parent < 0 ? Vector2.Zero : rooms[room.Parent].Offset;
            for (int i = 0; i <= 100; i++)
            {
                var p = from.Lerp(room.Offset, i / 100f);
                Check(geometry.Distance(p) < -0.72f, $"Blocked gallery to room {room.Id} at {p}");
                Check(Mathf.Abs(NestGeometry.Height(geometry.Distance(p)) + NestGeometry.Depth) < 0.001f, "Gallery floor is not continuous");
            }
        }
    }

    public static void GeometryChecks()
    {
        var rooms = new List<NestChamber>
        {
            new() { Id = 0, Kind = ChamberKind.Royal, Offset = new Vector2(0, -9), Parent = -1, Built = true },
            new() { Id = 1, Kind = ChamberKind.Brood, Offset = new Vector2(10, -12), Parent = 0, Built = true },
            new() { Id = 2, Kind = ChamberKind.Storage, Offset = new Vector2(-10, -8), Parent = 0, Built = true },
            new() { Id = 3, Kind = ChamberKind.Forge, Offset = new Vector2(17, -19), Parent = 1, Built = true },
        };
        var geometry = new NestGeometry();
        Check(geometry.Refresh(rooms), "Initial geometry missing");
        CheckRoutes(geometry, rooms);
        Check(geometry.Distance(new Vector2(24, 12)) > 3, "Undug soil opened");
        int revision = geometry.Revision;
        Check(!geometry.Refresh(rooms) && revision == geometry.Revision, "Static colony rebuilt every frame");
        rooms.Add(new NestChamber { Id = 4, Kind = ChamberKind.Brood, Offset = new Vector2(-17, -17), Parent = 2, Work = 100, Done = 5 });
        geometry.Refresh(rooms);
        float early = geometry.Distance(rooms[4].Offset + new Vector2(1.5f, 0));
        rooms[4].Done = 100; rooms[4].Built = true;
        Check(geometry.Refresh(rooms), "Completed excavation did not refresh");
        Check(geometry.Distance(rooms[4].Offset + new Vector2(1.5f, 0)) < early, "Room failed to expand");
        CheckRoutes(geometry, rooms);
        rooms[0].Tier = 5;
        Check(geometry.Refresh(rooms), "Upgrade did not refresh geometry");
        using var mesh = geometry.BuildMesh();
        using var arrays = mesh.SurfaceGetArrays(0);
        var vertices = arrays[(int)Mesh.ArrayType.Vertex].AsVector3Array();
        var indices = arrays[(int)Mesh.ArrayType.Index].AsInt32Array();
        Check(indices.Length > 0 && indices.Length % 3 == 0, "No excavation triangles");
        foreach (var v in vertices) Check(v.IsFinite() && v.Y <= -0.17f && v.Y >= -NestGeometry.Depth - 0.001f, "Invalid excavation vertex");
        foreach (var i in indices) Check(i >= 0 && i < vertices.Length, "Invalid triangle index");
        // Same world must be safe with no chambers and with the shader's full capacity.
        var empty = new NestGeometry(); empty.Refresh(Array.Empty<NestChamber>());
        Check(empty.Count == 1 && empty.Distance(Vector2.Zero) < 0, "Empty entrance missing");
        while (rooms.Count < 40) rooms.Add(new NestChamber { Id = rooms.Count, Kind = ChamberKind.Brood, Offset = new Vector2(rooms.Count, 0), Parent = 0, Built = true });
        geometry.Refresh(rooms);
        Check(geometry.Count == NestGeometry.MaxSegments, "Shader capacity exceeded");
        GD.Print($"[nesttest] geometry PASS: continuous branched routes, solid ground, construction, upgrades, cache, empty/max network, {indices.Length / 3} triangles");
    }

    public static async Task Run(Main app, string directory)
    {
        try
        {
            GeometryChecks();
            if (directory == "headless") { app.GetTree().Quit(); return; }
            bool red = directory.EndsWith(",red", StringComparison.Ordinal);
            if (red) directory = directory[..^4];
            DirAccess.MakeDirRecursiveAbsolute(directory);
            app.StartBattle(Difficulty.Normal, red ? 1 : 0);
            GD.Print("[nesttest] battle ready");
            var game = app.Session;
            game.SetPaused(true);
            game.Camera.UserControl = false;
            var colony = game.Sim.Col[Local.Me];
            colony.Tier[(int)Upgrade.Queen] = 5;
            colony.Tier[(int)Upgrade.Mandibles] = 3;
            colony.Tier[(int)Upgrade.Chitin] = 3;
            game.Sim.DevelopNest(Local.Me);
            // Populate the spatial hash before freezing the fixture. Idle-ant grooming traverses it.
            game.Sim.Tick(1f / 30f);
            GD.Print("[nesttest] colony developed");
            colony.Food = colony.FoodCap * 0.8f;
            var nest = game.World.Nests[Local.Me].Pos;
            var geometry = new NestGeometry(); geometry.Refresh(colony.Chambers);
            CheckRoutes(geometry, colony.Chambers);
            GD.Print("[nesttest] developed routes PASS");
            game.SetNestView(true);
            game.Camera.NestView = true;
            game.Camera.JumpTo(nest, 40);
            game.Camera.SetYaw(-Mathf.Pi / 4, true);
            GD.Print("[nesttest] rendering first view");
            async Task Settle() { await app.ToSignal(app.GetTree().CreateTimer(1.5), SceneTreeTimer.SignalName.Timeout); await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
            async Task Shot(string name)
            {
                await Settle();
                using var image = app.GetViewport().GetTexture().GetImage();
                Check(image.SavePng(directory.PathJoin(name + ".png")) == Error.Ok, "Screenshot write failed");
                GD.Print($"[nesttest] captured {name}");
            }
            await Shot("01-connected-colony");
            foreach (float yaw in new[] { -Mathf.Pi / 4, Mathf.Pi / 3, Mathf.Pi })
            {
                game.Camera.SetYaw(yaw, true);
                await Settle();
                foreach (var room in colony.Chambers)
                {
                    var p = game.Sim.ChamberPos(Local.Me, room);
                    var world = new Vector3(p.X, game.World.HF.Sample(nest.X, nest.Y) - NestGeometry.Depth, p.Y);
                    int picked = game.Interior.PickRoom(game.Camera.Cam.UnprojectPosition(world));
                    Check(picked == room.Id, $"Wrong chamber picked at yaw {yaw}: wanted {room.Id}, got {picked}");
                }
            }
            await Shot("02-rotated-colony");
            game.Camera.SetYaw(-Mathf.Pi / 4, true);
            game.Camera.JumpTo(nest, 27);
            game.Input.SelectedRoom = 0;
            await Shot("03-selected-room");
            game.Input.SelectedRoom = -1;
            game.Camera.JumpTo(nest, 40);
            Settings.Gfx = Settings.Quality.Low; game.ApplyQuality();
            await Shot("04-low-quality");
            game.SetNestView(false);
            await Shot("05-surface-closed");
            GD.Print("[nesttest] PASS: developed routes, picking every room at three rotations, low tier, close/open");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError($"[nesttest] FAIL: {e}"); app.GetTree().Quit(1); }
    }
}
