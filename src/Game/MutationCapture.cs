using System;
using Godot;

namespace Mandible;

/// `--mutation-capture=<abs dir>`: the mutation props in real frames for the two-part art gate, each view twice:
/// `<view>-close.png` framed like its target (H1 the web, H2 the haul, H3 the nursery) and `<view>.png` at play zoom.
/// Progress comes from a scratch file so the player's own save is never touched.
public static class MutationCapture
{
    public static async System.Threading.Tasks.Task Run(Main app, string dir)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(dir);
            async System.Threading.Tasks.Task Wait(float s) { await app.ToSignal(app.GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout); await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
            async System.Threading.Tasks.Task Shot(string name, float s = 1.5f) { await Wait(s); using var im = app.GetViewport().GetTexture().GetImage(); im.SavePng(dir.PathJoin(name + ".png")); GD.Print("[mutation-capture] " + name); }
            CampaignProgress.UsePath("user://campaign_mutcap.cfg"); CampaignProgress.Reset();
            // MANDIBLE_MUTCAP=units shoots only the unit views (P1-P4), =fly stops after the web and the fly (P5); unset, everything
            string only = OS.GetEnvironment("MANDIBLE_MUTCAP");
            if (only == "units") { await Units(app, Shot, Wait); GD.Print("[mutation-capture] PASS"); app.GetTree().Quit(); return; }
            void Run(GameSession g, int frames) { for (int i = 0; i < frames; i++) g.Sim.Tick(1f / 30); }
            // the hatch cards (docs/progression.md 1c): the part each mutation unit costs, with the count in store or what to hunt
            async System.Threading.Tasks.Task HatchCards()
            {
                app.StartEpisode(9); // Wings and Acid: every core taken, so all three mutation units are on the row
                var hs = app.Session; hs.Hud.CloseBriefing();
                hs.Sim.Col[0].Parts[(int)Part.Wings] = 6; hs.Sim.Col[0].Parts[(int)Part.Sting] = 2; hs.Sim.Col[0].Parts[(int)Part.Gland] = 3; hs.Sim.Col[0].Parts[(int)Part.Plates] = 0;
                Run(hs, 30);
                hs.Hud.CommandHud?.ShowPage(1);
                await Shot("hatch-parts", 2f);
            }
            if (System.Environment.GetEnvironmentVariable("MANDIBLE_CAPTURE_ONLY") == "parts")
            {
                await HatchCards();
                CampaignProgress.UsePath("user://campaign.cfg");
                GD.Print("[mutation-capture] PASS (hatch cards only)");
                app.GetTree().Quit();
                return;
            }

            // ---- H1: the queen wasp in the web, the colony's scouts below it
            app.StartEpisode(Reveal.SilkTrapId);
            var gs = app.Session; gs.Hud.CloseBriefing();
            var sim = gs.Sim; var gland = sim.Mission.Gland;
            var home = sim.NestEntrance(0);
            var toward = (home - gland.Pos).Normalized();
            var scouts = new System.Collections.Generic.List<int>();
            for (int k = 0; k < 7; k++) { int id = sim.Spawn(0, AntType.Worker, gland.Pos + toward * (5 + k % 3) + new Vector2(k - 3, 0) * 0.9f, toward.AngleD() + Mathf.Pi); scouts.Add(id); }
            Run(gs, 90); // the scouts uncover the web
            await Wait(1f); // the fog of war follows them on the frame, not the tick
            gs.SetPaused(true);
            gs.Ants.VisibleToPlayer = _ => true;
            float face = Mathf.Atan2(gland.Pos.X - home.X, gland.Pos.Y - home.Y);
            gs.Camera.SetYaw(face + Mathf.Pi, true);
            gs.Camera.JumpTo(gland.Pos - toward * 3f, 34); await Shot("web-close", 2.5f);
            gs.Camera.JumpTo(gland.Pos + toward * 3f, 60); await Shot("web", 1.5f);
            {
                // P5: the fly at the hub, framed like its target (a lens of its own: the game camera keeps its distance)
                var hubAt = gs.Props?.GlandPoint(gland.Id) ?? gs.World.HF.Point(gland.Pos, 7f);
                var lens = new Camera3D { Fov = 36, Near = 0.3f, Far = 400 };
                app.AddChild(lens);
                var back = new Vector3(toward.X, 0, toward.Y);
                lens.GlobalTransform = new Transform3D(Basis.Identity, hubAt + back * 10f + Vector3.Up * 0.2f).LookingAt(hubAt + Vector3.Up * 0.4f, Vector3.Up);
                lens.MakeCurrent(); gs.Hud.Visible = false;
                bool fogWas = gs.Fog.RevealAll; gs.Fog.RevealAll = true; // the silk shows against the lit yard, as in the target
                await Shot("fly-close", 1.2f);
                lens.GlobalTransform = new Transform3D(Basis.Identity, hubAt + back * 24f + Vector3.Up * 6f).LookingAt(hubAt - Vector3.Up * 2f, Vector3.Up);
                await Shot("fly", 0.6f);
                gs.Fog.RevealAll = fogWas; gs.Hud.Visible = true; gs.Camera.Cam.MakeCurrent(); lens.QueueFree();
            }
            if (only == "fly") { GD.Print("[mutation-capture] PASS"); app.GetTree().Quit(); return; }

            // ---- H2: the gland cut free and hauled home by workers, soldiers walking guard
            gs.SetPaused(false);
            gland.Done = gland.Work;
            var haulAt = gland.Pos + toward * 14;
            int carrier = sim.Spawn(0, AntType.Worker, haulAt, toward.AngleD());
            sim.Cores[0].Pos = haulAt;
            sim.CmdExtract(new System.Collections.Generic.List<int> { carrier }, gland);
            var escort = new System.Collections.Generic.List<int>();
            for (int k = 0; k < 3; k++) escort.Add(sim.Spawn(0, AntType.Worker, haulAt + new Vector2(k - 1, 1.2f), toward.AngleD()));
            for (int k = 0; k < 2; k++) escort.Add(sim.Spawn(0, AntType.Soldier, haulAt + new Vector2(k * 2 - 1, -2), toward.AngleD()));
            sim.CmdExtract(escort.GetRange(0, 3), gland);
            Run(gs, 45);
            await Wait(0.8f);
            gs.SetPaused(true);
            gs.Camera.JumpTo(gland.Pos, 10); await Shot("haul-close", 2f);
            gs.Camera.JumpTo(gland.Pos, 55); await Shot("haul", 1.5f);

            // ---- the core in the carrier's jaws at play zoom; a general's burst; the queen's mutation with its column
            // of light (targets M2, M3: docs/art-targets/REVIEW-mutation-core.jpg)
            gs.SetPaused(false); await Wait(0.3f); gs.SetPaused(true);
            gs.Camera.JumpTo(gland.Pos, 40); await Shot("core-carried", 1.5f);
            gs.SetPaused(false);
            var gAt = haulAt + new Vector2(-10, 4);
            int general = sim.Spawn(0, AntType.Marshal, gAt, 0);
            gs.Camera.JumpTo(gAt, 40);
            await Wait(0.8f);
            gs.GeneralBurst(general, 2);
            await Shot("general-burst", 0.45f);
            await Wait(3f);
            sim.Col[0].Mutations &= ~(int)Mutation.Winged;
            gs.Camera.PitchOverride = 38;
            gs.Camera.JumpTo(home - toward * 6, 58);
            await Wait(0.8f);
            // called outside a tick, its event would be cleared before the frame reads it: hand it to the session directly
            sim.GiveMutation(0, Mutation.Winged); gs.MutationMoment(sim.Events.FindLast(e => e.Kind == EvKind.Mutate));
            await Shot("queen-beam", 0.7f);
            gs.Camera.PitchOverride = 0;

            // ---- H3: the royal nursery, the larva's bed and the heap of food half piled
            app.StartEpisode(Reveal.RoyalLarvaId);
            gs = app.Session; gs.Hud.CloseBriefing();
            sim = gs.Sim;
            sim.GiveMutation(0, Mutation.RoyalLarva);
            var room = sim.ChamberOf(0, ChamberKind.Nursery);
            room.Done = room.Work; room.Built = true;
            sim.Col[0].Organic = 0;
            Run(gs, 60);
            sim.Col[0].LarvaFed = sim.Col[0].LarvaNeed * 0.55f;
            gs.SetNestView(true);
            Run(gs, 30);
            gs.SetPaused(true);
            var at = sim.ChamberPos(0, room);
            gs.Camera.JumpTo(at + new Vector2(0, 2.4f), 11); await Shot("nursery-close", 3f);
            gs.Camera.JumpTo(at, 40); await Shot("nursery", 1.5f);

            // ---- the evolve tree: a colony with winged ants and web ants, a general, and organic matter to spend
            sim.GiveMutation(0, Mutation.Winged); sim.GiveMutation(0, Mutation.Web);
            sim.Col[0].Organic = 64; sim.Col[0].Evo[(int)EvoNode.FlightMuscle] = 2;
            sim.Spawn(0, AntType.Warlord, sim.W.Nests[0].Pos + new Vector2(6, 0), 0, 3);
            gs.SetNestView(false);
            gs.Hud.ToggleEvolveTree();
            await Shot("evolve-tree", 1f);
            gs.Hud.ToggleEvolveTree();
            if (only == "" || only.Contains("units")) await Units(app, Shot, Wait);

            await HatchCards();
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://campaign_mutcap.cfg"));
            CampaignProgress.UsePath("user://campaign.cfg");
            GD.Print("[mutation-capture] PASS");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[mutation-capture] FAIL " + e); app.GetTree().Quit(1); }
    }

    /// Targets P1-P4 (docs/art-targets/README-mutation-units.md): each mutation unit of both colonies in a close frame
    /// like its target (`unit-<name>-close.png`, the red colony's `unit-<name>-red-close.png`), then all of them walking
    /// at play zoom (`units.png`), where the web ant's eight legs have to read as a gait.
    static async System.Threading.Tasks.Task Units(Main app, Func<string, float, System.Threading.Tasks.Task> shot, Func<float, System.Threading.Tasks.Task> wait)
    {
        app.StartSkirmish();
        var g = app.Session; var sim = g.Sim;
        g.Fog.RevealAll = true; g.Hud.Visible = false; g.Camera.UserControl = false;
        await wait(3f);
        g.SetPaused(true);
        var hf = g.World.HF;
        var home = g.World.Nests[0].Pos;
        // the units stand with the flank the lens sees toward the sun, so their bodies are lit, not silhouettes
        var toSun = -(Basis.FromEuler(GameSession.SunAngles * (Mathf.Pi / 180f)) * Vector3.Forward);
        float heading = Mathf.Atan2(toSun.Z, toSun.X) - Mathf.Pi * 0.5f + 0.75f;
        var lensSide = new Vector2(toSun.X, toSun.Z).Normalized(); // where the lens stands, seen from the stage
        // the flattest open patch of yard within reach of home, with the nest behind the lens, not in the picture
        Vector2 stage = home + lensSide * 30; float best = float.MaxValue;
        for (int k = 0; k < 64; k++)
        {
            var p = home + new Vector2(Mathf.Cos(k * 0.7f), Mathf.Sin(k * 0.7f)) * (22 + k % 4 * 6);
            if (p.X < 30 || p.Y < 30 || p.X > Config.MapSize - 30 || p.Y > Config.MapSize - 30) continue;
            if ((home - p).Normalized().Dot(lensSide) < 0.5f) continue;
            float rough = 0;
            for (int j = 0; j < 8; j++) { var q = p + new Vector2(Mathf.Cos(j), Mathf.Sin(j)) * 4; rough += Mathf.Abs(hf.Sample(q.X, q.Y) - hf.Sample(p.X, p.Y)) + (hf.Bilinear(hf.H, q.X, q.Y) - hf.Bilinear(hf.Ground, q.X, q.Y)) * 4; }
            if (rough < best) { best = rough; stage = p; }
        }
        void Clear() { for (int i = 0; i < sim.High; i++) { ref var a = ref sim.Ants[i]; if (a.Alive && a.Pos.DistanceTo(stage) < 16) a.Alive = false; } }
        var lens = new Camera3D { Fov = 38, Near = 0.02f, Far = 300 };
        if (g.Camera.Cam.Attributes is CameraAttributesPractical pa)
        {
            var attr = (CameraAttributesPractical)pa.Duplicate();
            attr.DofBlurFarEnabled = false; attr.DofBlurNearEnabled = false;
            lens.Attributes = attr;
        }
        app.AddChild(lens);
        void Frame(float dist, float pitch, float yaw, float lift)
        {
            var target = new Vector3(stage.X, hf.Sample(stage.X, stage.Y) + lift, stage.Y);
            float a = heading + Mathf.Pi * 0.5f + yaw, p = Mathf.DegToRad(pitch);
            var dir = new Vector3(Mathf.Cos(a) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Sin(a) * Mathf.Cos(p));
            lens.GlobalTransform = new Transform3D(Basis.Identity, target + dir * dist).LookingAt(target, Vector3.Up);
            lens.Near = Mathf.Max(0.02f, dist * 0.2f);
            lens.MakeCurrent();
        }
        var views = new (string name, AntType type, bool sting, float yaw)[]
        {
            ("winged", AntType.Flyer, false, -0.55f), ("stinger", AntType.Flyer, true, -0.3f),
            ("webant", AntType.WebAnt, false, -0.5f), ("armoured", AntType.Armoured, false, -0.3f),
        };
        for (int team = 0; team < 2; team++)
            foreach (var (name, type, sting, yaw) in views)
            {
                Clear();
                if (sting) sim.Col[team].Mutations |= (int)Mutation.Stinger; else sim.Col[team].Mutations &= ~(int)Mutation.Stinger;
                int id = sim.Spawn(team, type, stage, heading);
                sim.Ants[id].Sting = sting; // stingers are per ant
                float sz = Config.Of(type).Size;
                // a winged ant is shown standing, as in its target: wings at rest, feet on the ground
                Frame(1.75f * sz, 13, yaw, 0.2f * sz + sim.Ants[id].Alt);
                await shot($"unit-{name}{(team == 1 ? "-red" : "")}-close", 0.8f);
                if (team == 0) { Frame(1.5f * sz, 62, yaw + 0.5f, 0.12f * sz + sim.Ants[id].Alt); await shot($"unit-{name}-top-close", 0.5f); }
            }
        // all of them on the move at play zoom: the black colony's stingers, web ants and armoured ants, the red colony's winged ants
        Clear();
        sim.Col[0].Mutations |= (int)Mutation.Stinger; sim.Col[1].Mutations &= ~(int)Mutation.Stinger;
        var walkers = new System.Collections.Generic.List<int>();
        var along = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading)); var side = new Vector2(-along.Y, along.X);
        int n = 0;
        foreach (var t in new[] { AntType.WebAnt, AntType.WebAnt, AntType.Armoured, AntType.Armoured, AntType.Flyer, AntType.Flyer, AntType.Soldier, AntType.Worker })
            { int w = sim.Spawn(0, t, stage - along * 6 + side * (n++ * 2.2f - 7.5f), heading); sim.Ants[w].Sting = t == AntType.Flyer; walkers.Add(w); }
        g.Camera.Cam.MakeCurrent();
        g.Camera.JumpTo(stage, 30);
        g.SetPaused(false);
        sim.CmdMove(walkers, stage + along * 14, false);
        await shot("units", 1.6f);
        g.Camera.JumpTo(stage + along * 3, 13);
        await shot("units-walk-1", 0.25f);
        await shot("units-walk-2", 0.12f);
        g.SetPaused(true);
        lens.QueueFree();
    }
}
