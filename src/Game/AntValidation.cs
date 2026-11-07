using System;
using System.Threading.Tasks;
using Godot;

namespace Mandible;

/// `--ant-capture=<dir>`: the ant views of the two-part gate (docs/art-pipeline.md) against target G
/// (docs/art-targets/g-*.jpg). Each colony's castes and its heroes stand in a lineup on open yard soil, framed close
/// as in the target (`g-*-close.png`), a head fills the frame for the compound eyes (`g-eyes-close.png`), and a busy
/// stretch of the yard is shot at play zoom for the readability check (`g-play.png`), then again on the Low tier with
/// its frame rate (`g-play-low.png`).
public static class AntValidation
{
    public static async Task Capture(Main app, string directory)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            async Task Shot(string name, float wait = 1.5f)
            {
                var cam = app.Session?.Camera;
                if (cam != null) cam.Cam.Near = name.EndsWith("-close") ? Mathf.Max(0.05f, cam.Distance * 0.3f) : 0.2f;
                await app.ToSignal(app.GetTree().CreateTimer(wait), SceneTreeTimer.SignalName.Timeout);
                await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                app.GetViewport().GetTexture().GetImage().SavePng(directory.PathJoin(name + ".png"));
                GD.Print($"[ant-capture] {name}; fps {Engine.GetFramesPerSecond()}; tier {Settings.Gfx}");
            }

            app.StartSkirmish(); var g = app.Session;
            g.Fog.RevealAll = true; g.Hud.Visible = false; g.Camera.UserControl = false;
            var home = g.World.Nests[0].Pos;
            // let the colony forage so the play-zoom frame has trails of workers, then freeze the world
            await app.ToSignal(app.GetTree().CreateTimer(40), SceneTreeTimer.SignalName.Timeout);
            g.SetPaused(true);
            var hf = g.World.HF;
            // the flattest open patch of yard within reach of home: the stage for the lineups
            Vector2 stage = home + new Vector2(30, 0); float best = float.MaxValue;
            for (int k = 0; k < 64; k++)
            {
                var p = home + new Vector2(Mathf.Cos(k * 0.7f), Mathf.Sin(k * 0.7f)) * (22 + k % 4 * 6);
                if (p.X < 30 || p.Y < 30 || p.X > Config.MapSize - 30 || p.Y > Config.MapSize - 30) continue;
                float rough = 0;
                for (int j = 0; j < 8; j++) { var q = p + new Vector2(Mathf.Cos(j), Mathf.Sin(j)) * 4; rough += Mathf.Abs(hf.Sample(q.X, q.Y) - hf.Sample(p.X, p.Y)) + (hf.Bilinear(hf.H, q.X, q.Y) - hf.Bilinear(hf.Ground, q.X, q.Y)) * 4; }
                if (rough < best) { best = rough; stage = p; }
            }
            const float heading = 0.35f; // walking across the frame, a little toward the camera
            var across = new Vector2(Mathf.Cos(heading), Mathf.Sin(heading));
            void Lineup(int team, AntType[] types)
            {
                // side by side, each facing the same way, spaced by its own width (about half its size), as in the targets
                var side = new Vector2(-across.Y, across.X);
                float width = 0; foreach (var t in types) width += Config.Of(t).Size * 0.75f;
                float x = -width / 2;
                foreach (var t in types)
                {
                    float sz = Config.Of(t).Size * 0.75f;
                    g.Sim.Spawn(team, t, stage + side * (x + sz / 2), heading); x += sz;
                }
            }
            void Clear() => ClearAround(g.Sim, stage, 14);
            // the game camera keeps 3 units above the ground; a macro framing needs its own lens
            var lens = new Camera3D { Fov = 38, Near = 0.02f, Far = 300 };
            if (g.Camera.Cam.Attributes is CameraAttributesPractical pa)
            {
                var attr = (CameraAttributesPractical)pa.Duplicate();
                attr.DofBlurFarEnabled = false; attr.DofBlurNearEnabled = false;
                lens.Attributes = attr;
            }
            app.AddChild(lens);
            void Frame(float dist, float pitch, float yaw = 0, float lift = 0.3f, Vector2? at = null)
            {
                var c = at ?? stage;
                var target = new Vector3(c.X, hf.Sample(c.X, c.Y) + lift, c.Y);
                // look from the ants' left-front, so the lineup crosses the frame toward the camera
                float a = heading + Mathf.Pi * 0.5f + yaw, p = Mathf.DegToRad(pitch);
                var dir = new Vector3(Mathf.Cos(a) * Mathf.Cos(p), Mathf.Sin(p), Mathf.Sin(a) * Mathf.Cos(p));
                lens.GlobalTransform = new Transform3D(Basis.Identity, target + dir * dist).LookingAt(target, Vector3.Up);
                lens.MakeCurrent();
            }
            // the four castes the targets show (G1, G2): worker, major, queen, winged; framed so they fill the frame as there
            AntType[] castes = { AntType.Worker, AntType.Major, AntType.Queen, AntType.Flyer };
            for (int team = 0; team < 2; team++)
            {
                Clear(); Lineup(team, castes);
                Frame(6.5f, 50, -Mathf.Pi * 0.5f + 0.55f, 0.8f); await Shot($"g-castes-{(team == 0 ? "black" : "red")}-close");
            }
            // each hero as in its target: the black colony's on the left, the red colony's on the right
            foreach (var hero in new[] { AntType.Warlord, AntType.Matron, AntType.Marshal })
            {
                Clear();
                float sz = Config.Of(hero).Size;
                var side = new Vector2(-across.Y, across.X);
                int a0 = g.Sim.Spawn(0, hero, stage + side * (0.75f * sz), heading);
                g.Sim.Spawn(1, hero, stage - side * (0.75f * sz), heading);
                // a Sky Marshal hovers: aim at the height it is drawn at, not at the ground under it
                await app.ToSignal(app.GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
                float alt = g.Sim.Ants[a0].Alt + (Config.Flies(hero) ? 0.6f : 0f);
                Frame(2.7f * sz, 50, -Mathf.Pi * 0.5f + 0.55f, 0.3f * sz + alt); await Shot($"g-{hero.ToString().ToLowerInvariant()}-close");
                // the same general at level 10: every growth in, the veins at their brightest
                Clear();
                a0 = g.Sim.Spawn(0, hero, stage + side * (0.75f * sz), heading, Config.MaxLevelOf(hero));
                g.Sim.Spawn(1, hero, stage - side * (0.75f * sz), heading, Config.MaxLevelOf(hero));
                await app.ToSignal(app.GetTree().CreateTimer(0.3f), SceneTreeTimer.SignalName.Timeout);
                Frame(2.7f * sz, 50, -Mathf.Pi * 0.5f + 0.55f, 0.3f * sz + alt); await Shot($"g-{hero.ToString().ToLowerInvariant()}-l10-close");
            }
            // the compound eyes: a worker's head filling the frame, then a queen's (larger eyes, three ocelli). The ant faces
            // the sun and the lens looks at its face from just off the sun's side, so the head is lit, not a silhouette
            var toSun = -(Basis.FromEuler(GameSession.SunAngles * (Mathf.Pi / 180f)) * Vector3.Forward);
            float sunH = Mathf.Atan2(toSun.Z, toSun.X);
            var face = new Vector2(Mathf.Cos(sunH), Mathf.Sin(sunH));
            float faceYaw = sunH - heading - Mathf.Pi * 0.5f + 0.45f;
            Clear(); g.Sim.Spawn(0, AntType.Worker, stage, sunH); Frame(0.48f, 24, faceYaw, 0.15f, stage + face * 0.28f); await Shot("g-eyes-close");
            float qs = Config.Of(AntType.Queen).Size;
            Clear(); g.Sim.Spawn(0, AntType.Queen, stage, sunH); Frame(0.5f * qs, 26, faceYaw, 0.16f * qs, stage + face * (0.3f * qs)); await Shot("g-eyes-queen-close");
            Clear();

            // H3-H5, the mutation track's models, placed on the stage directly (the mutation renderer draws them in play)
            var showcase = new Node3D();
            app.AddChild(showcase);
            Vector3 Ground(Vector2 p, float lift = 0) => new(p.X, hf.Sample(p.X, p.Y) + lift, p.Y);
            Basis Facing(float h, float scale) => AntRenderer.Orient(Vector3.Up, h, scale);
            MeshInstance3D Place(Mesh mesh, Material mat, Basis b, Vector3 at)
            {
                var mi = new MeshInstance3D { Mesh = mesh, MaterialOverride = mat };
                showcase.AddChild(mi);
                mi.GlobalTransform = new Transform3D(b, at);
                return mi;
            }
            float fs = Config.Of(AntType.Flyer).Size;
            var flyerMat = AntRenderer.AntMat(Shaders.Get(Shaders.Ant), 0, AntType.Flyer, false);
            flyerMat.SetShaderParameter("venom_bands", 1f);
            var flyerAt = Ground(stage, 0.6f);
            Place(AntMesh.Build(AntType.Flyer, false, 0, AntMesh.Detail.Near), flyerMat, Facing(heading, fs), flyerAt);
            if (AntMesh.Sting() != null) Place(AntMesh.Sting(), flyerMat, Facing(heading, fs), flyerAt);
            Frame(1.6f * fs, 25, 0.35f, 0.75f); await Shot("h-sting-close");
            foreach (var c in showcase.GetChildren()) c.QueueFree();

            if (AntMesh.RoyalLarva() != null)
            {
                Place(AntMesh.RoyalLarva(), AntMesh.RoyalLarvaMaterial(), Facing(heading + 0.6f, 3.2f), Ground(stage, 0.05f));
                // two nurses at its side, for scale
                g.Sim.Spawn(0, AntType.Worker, stage + new Vector2(1.6f, 0.9f), heading + 2.5f);
                g.Sim.Spawn(0, AntType.Worker, stage - new Vector2(1.4f, 1.1f), heading - 0.4f);
                Frame(5.5f, 45, -0.4f, 0.5f); await Shot("h-larva-close");
                foreach (var c in showcase.GetChildren()) c.QueueFree();
                Clear();
            }
            if (AntMesh.WombCocoon() != null)
            {
                // the hero curled inside, dark behind the lit silk
                var heroMat = AntRenderer.AntMat(Shaders.Get(Shaders.Ant), 0, AntType.Warlord, false);
                Place(AntMesh.Build(AntType.Warlord, false, 0, AntMesh.Detail.Mid), heroMat, Facing(heading, 2.4f), Ground(stage, 0.25f));
                Place(AntMesh.WombCocoon(), AntMesh.WombMaterial(), Facing(heading, 4.2f), Ground(stage, -0.4f));
                g.Sim.Spawn(0, AntType.Worker, stage + new Vector2(2.6f, 0.5f), heading + 2.8f);
                Frame(6.5f, 35, -0.3f, 1.4f); await Shot("h-cocoon-close");
                foreach (var c in showcase.GetChildren()) c.QueueFree();
                Clear();
            }
            showcase.QueueFree();
            g.Camera.Cam.MakeCurrent();

            // play zoom: the busiest stretch of trail near home
            g.Camera.PitchOverride = 0; g.Camera.SetYaw(0.6f, true);
            // out on the foraging trails, clear of the nest's own pit: rings around home and the food piles themselves
            Vector2 busy = home; int most = -1;
            var spots = new System.Collections.Generic.List<Vector2>();
            for (int k = 0; k < 48; k++) spots.Add(home + new Vector2(Mathf.Cos(k * 0.55f), Mathf.Sin(k * 0.55f)) * (24 + k % 4 * 9));
            foreach (var f in g.World.Food) if (f.Alive && f.Pos.DistanceTo(home) > 22) spots.Add(f.Pos);
            foreach (var p in spots)
            {
                int n = AntsNear(g.Sim, p, 14);
                if (n > most) { most = n; busy = p; }
            }
            GD.Print($"[ant-capture] play frame: {most} ants on the surface within 14 units");
            if (most < 25)
            {
                // too quiet to judge: stage a moment of play there, a foraging column of black workers with an escort
                // meeting a red raiding party, every ant at its true size
                var rng = new Rng(77);
                var dir = (busy - home).Normalized(); var side = new Vector2(-dir.Y, dir.X);
                float h = dir.Angle();
                for (int k = 0; k < 34; k++)
                    g.Sim.Spawn(0, k % 11 == 5 ? AntType.Soldier : AntType.Worker, busy + dir * rng.R(-13, 6) + side * rng.R(-1.6f, 1.6f), h + rng.R(-0.4f, 0.4f) + (k % 2 == 0 ? 0 : Mathf.Pi));
                g.Sim.Spawn(0, AntType.Major, busy + dir * 2 + side * 3, h);
                for (int k = 0; k < 9; k++)
                    g.Sim.Spawn(1, k == 4 ? AntType.Major : k % 3 == 0 ? AntType.Soldier : AntType.Worker, busy + dir * rng.R(8, 13) + side * rng.R(-4, 4), h + Mathf.Pi + rng.R(-0.5f, 0.5f));
                GD.Print("[ant-capture] play frame staged: a black foraging column meets a red raiding party");
            }
            g.Camera.JumpTo(busy, 40); await Shot("g-play", 2f);
            Settings.Gfx = Settings.Quality.Low; g.ApplyQuality();
            await Shot("g-play-low", 4f);
            Settings.Gfx = Settings.Quality.High; g.ApplyQuality();
            GD.Print("[ant-capture] PASS all views captured");
            app.GetTree().Quit(0);
        }
        catch (Exception e) { GD.PushError("[ant-capture] FAIL " + e); app.GetTree().Quit(1); }
    }

    static int AntsNear(Sim sim, Vector2 p, float r)
    {
        int n = 0;
        for (int i = 0; i < sim.High; i++) { ref var a = ref sim.Ants[i]; if (a.Alive && a.Layer == 0 && a.Pos.DistanceTo(p) < r) n++; }
        return n;
    }

    /// Clears the stage of other ants so a lineup stands alone (render-only: the capture never resumes this sim).
    static void ClearAround(Sim sim, Vector2 p, float r)
    {
        for (int i = 0; i < sim.High; i++) { ref var a = ref sim.Ants[i]; if (a.Alive && a.Pos.DistanceTo(p) < r) a.Alive = false; }
    }
}
