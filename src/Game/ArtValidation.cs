using System;
using System.Threading.Tasks;
using Godot;

namespace Mandible;

/// `--art-capture=<dir>`: renders every view that has an approved target in docs/art-targets, always from the same
/// camera, so tools/art-gate.py can set target and frame side by side for an independent review. The view names
/// match the target files (a-opening.png against a-opening-tube.jpg and so on; docs/art-pipeline.md has the table).
/// It also checks the fog of war by sampling pixels: ground nobody has seen must be black, the trail must not be.
public static class ArtValidation
{
    public static async Task Capture(Main app, string directory)
    {
        int failures = 0;
        void Check(string what, bool ok)
        {
            GD.Print($"[art-capture] {(ok ? "PASS" : "FAIL")} {what}");
            if (!ok) failures++;
        }
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            async Task<Image> Shot(string name, float wait = 2.5f)
            {
                await app.ToSignal(app.GetTree().CreateTimer(wait), SceneTreeTimer.SignalName.Timeout);
                await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                var im = app.GetViewport().GetTexture().GetImage();
                im.SavePng(directory.PathJoin(name + ".png"));
                GD.Print($"[art-capture] {name}; fps {Engine.GetFramesPerSecond()}; tier {Settings.Gfx}");
                return im;
            }
            float Luma(Image im, GameSession g, Vector2 p)
            {
                var cam = g.Camera.Cam;
                var w = new Vector3(p.X, g.World.HF.Sample(p.X, p.Y), p.Y);
                if (cam.IsPositionBehind(w)) return -1;
                var s = cam.UnprojectPosition(w) * (im.GetWidth() / (float)app.GetViewport().GetVisibleRect().Size.X);
                if (s.X < 2 || s.Y < 2 || s.X >= im.GetWidth() - 2 || s.Y >= im.GetHeight() - 2) return -1;
                float sum = 0;
                for (int dy = -2; dy <= 2; dy++)
                    for (int dx = -2; dx <= 2; dx++) { var c = im.GetPixel((int)s.X + dx, (int)s.Y + dy); sum += 0.3f * c.R + 0.59f * c.G + 0.11f * c.B; }
                return sum / 25f;
            }

            // A, F: level 1 as it opens: the broken tube, the trail, and black everywhere nobody has walked
            app.StartEpisode(1); var g = app.Session;
            g.Hud.CloseBriefing(); g.SetPaused(true); g.Hud.Visible = false;
            await Shot("a-opening", 3.5f);
            g.Camera.SetYaw(0.5f, true); g.Camera.JumpTo(new Vector2(44, 255), 18); await Shot("a-tube-close");
            // the fog as the player first meets it: the level's own opening camera, a little wider
            g.Camera.SetYaw(0, true); g.Camera.JumpTo(Expedition.Start + new Vector2(16, 0), 52);
            var fog = await Shot("f-fog-of-war");
            float trail = Luma(fog, g, Expedition.Start + new Vector2(14, 0));
            float unknown = Luma(fog, g, Expedition.Start + new Vector2(26, -34));
            float clearing = Luma(fog, g, Expedition.Clearing);
            GD.Print($"[art-capture] fog luma: trail {trail:F3}, unknown {unknown:F3}, clearing {clearing:F3}");
            Check("fog: ground nobody has seen is black", unknown >= 0 && unknown < 0.02f);
            Check("fog: the trail is not black", trail > 0.08f);
            Check("fog: the clearing stays unknown until the queen reaches it", clearing < 0 || clearing < 0.02f);
            Settings.Gfx = Settings.Quality.Low; g.ApplyQuality();
            g.Camera.JumpTo(Expedition.Start + new Vector2(12, 0), 44); await Shot("a-opening-low", 3f);
            Settings.Gfx = Settings.Quality.High; g.ApplyQuality();

            // B, E: the yard and its food at a normal play zoom, after the colony has foraged for a while so workers are
            // on the trails and at the piles (everything revealed so the frame is judged on its art, not the fog)
            app.StartSkirmish(); g = app.Session;
            g.Fog.RevealAll = true; g.Hud.Visible = false;
            g.Camera.UserControl = false;
            var home = g.World.Nests[0].Pos;
            await app.ToSignal(app.GetTree().CreateTimer(28), SceneTreeTimer.SignalName.Timeout);
            g.SetPaused(true);
            int AntsNear(Vector2 p, float r)
            {
                int n = 0;
                for (int i = 0; i < g.Sim.High; i++) { ref var a = ref g.Sim.Ants[i]; if (a.Alive && a.Team == 0 && a.Pos.DistanceTo(p) < r) n++; }
                return n;
            }
            FoodSource Busiest(FoodKind kind)
            {
                FoodSource best = null; int most = int.MinValue;
                foreach (var f in g.World.Food)
                {
                    if (f.Kind != kind || !f.Alive) continue;
                    // a pile still mostly whole, with workers at it, near home
                    int n = (int)(f.Amount / f.Max * 300) + AntsNear(f.Pos, f.Radius + 5) * 60 - (int)f.Pos.DistanceTo(home);
                    if (n > most) { most = n; best = f; }
                }
                return best;
            }
            var busy = Busiest(FoodKind.Crumb);
            // out in the yard on the foraging run, clear of the nest's own cut
            var yardAt = busy != null ? busy.Pos + (busy.Pos - home).Normalized() * 14 : home + new Vector2(60, 0);
            g.Camera.SetYaw(0.6f, true); g.Camera.JumpTo(yardAt, 40);
            await Shot("b-yard", 2.5f);
            foreach (var kind in new[] { FoodKind.Crumb, FoodKind.Seed, FoodKind.Sugar, FoodKind.Berry })
            {
                var f = Busiest(kind);
                if (f == null) continue;
                g.Camera.JumpTo(f.Pos, 24 + f.Radius * 1.5f); await Shot("e-food-" + kind.ToString().ToLowerInvariant(), 1.5f);
            }

            // D: the chambers through the glass earth
            g.SetNestView(true); g.Camera.NestView = true; g.Camera.SetYaw(0, true);
            g.Camera.JumpTo(home + new Vector2(5, 8), 24); await Shot("d-chamber", 3.5f);

            // C: the colony map part-way through the campaign
            CampaignProgress.UsePath("user://campaign_artcap.cfg"); CampaignProgress.Reset();
            int[] earned = { 7, 3, 7, 5, 3, 7 };
            for (int n = 1; n <= 6; n++) CampaignProgress.Record(Levels.IdAt(n), earned[n - 1], 300 + n * 60);
            app.ShowMenu(); app.Menu.OpenCampaign();
            await Shot("c-level-map", 4);
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://campaign_artcap.cfg"));

            GD.Print(failures == 0 ? "[art-capture] PASS all views captured" : $"[art-capture] FAIL {failures} check(s)");
            app.GetTree().Quit(failures == 0 ? 0 : 1);
        }
        catch (Exception e) { GD.PushError("[art-capture] FAIL " + e); app.GetTree().Quit(1); }
    }
}
