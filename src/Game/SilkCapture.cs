using System;
using Godot;

namespace Mandible;

/// `--silk-capture=<abs dir>`: the silk in real frames for the art gate (targets S1-S3), each view twice:
/// `<view>-close.png` framed like its target and `<view>.png` at play zoom. Views: snare (a spider's web holding
/// ants), spinnery, trap (a trap web holding raiders), works (the web post, web tower and trap nest), and two with
/// the HUD up: `silk-panel` (the Silk panel) and `silk-place` (the placement ghost and reach ring).
/// With `--mobile` it takes only the two HUD views, for the phone layout.
public static class SilkCapture
{
    public static async System.Threading.Tasks.Task Run(Main app, string dir)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(dir);
            async System.Threading.Tasks.Task Wait(float s) { await app.ToSignal(app.GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout); await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
            async System.Threading.Tasks.Task Shot(string name, float s = 1.5f) { await Wait(s); using var im = app.GetViewport().GetTexture().GetImage(); im.SavePng(dir.PathJoin(name + ".png")); GD.Print("[silk-capture] " + name); }
            CampaignProgress.UsePath("user://campaign_silkcap.cfg"); CampaignProgress.Reset();
            void Run(GameSession g, int frames) { for (int i = 0; i < frames; i++) g.Sim.Tick(1f / 30); }

            app.StartEpisode(Reveal.SilkTrapId);
            var gs = app.Session; gs.Hud.CloseBriefing();
            var sim = gs.Sim;
            var home = sim.NestEntrance(0);
            var c = sim.Col[0];
            c.Mutations |= (int)Mutation.Web; c.Evo[(int)EvoNode.Spinneret] = 2; c.Founded = true; c.Food += 600; c.Straw = Config.StrawCap;
            var along = sim.Approach(0);
            var side = new Vector2(-along.Y, along.X);
            Run(gs, 2);
            gs.Ants.VisibleToPlayer = _ => true;

            // ---- the HUD: the Silk panel with nothing built yet, then the placement ghost and the reach ring
            await Wait(4.5f);
            gs.Camera.JumpTo(home, 60);
            await Shot(Settings.Mobile ? "silk-panel-mobile" : "silk-panel", 1.5f);
            gs.Input.BeginSilk(SilkKind.Spinnery);
            var want = sim.DefaultSilkSpot(0, SilkKind.Spinnery);
            gs.Silk.Ghost = (true, false, SilkKind.Spinnery, want, true);
            gs.Camera.JumpTo(home, 75);
            await Shot(Settings.Mobile ? "silk-place-mobile" : "silk-place", 0.2f);
            gs.Input.CancelPlacing();
            gs.Silk.Ghost = default;
            if (Settings.Mobile) { GD.Print("[silk-capture] PASS"); app.GetTree().Quit(); return; }
            gs.Hud.Visible = false;

            GD.Print("[silk-capture] spinnery: " + sim.BuildSilk(0, SilkKind.Spinnery, want));
            var p1 = home + along * 13; var p2 = home + along * 11 + side * 9; var p3 = home + along * 11 - side * 9;
            foreach (var p in new[] { p1, p2, p3 }) GD.Print("[silk-capture] post: " + sim.BuildSilk(0, SilkKind.Post, p));
            foreach (var w in sim.Works) w.Got = w.Need;
            Run(gs, (int)((Config.SpinneryTime + 16) * 30));
            c.Straw = Config.StrawCap; c.Food += 300;
            SilkWork Near(Vector2 p) { SilkWork b = null; foreach (var w in sim.Works) if (b == null || w.Pos.DistanceSquaredTo(p) < b.Pos.DistanceSquaredTo(p)) b = w; return b; }
            sim.UpgradeSilk(0, Near(p2).Id, SilkKind.Tower);
            sim.UpgradeSilk(0, Near(p3).Id, SilkKind.TrapNest);
            sim.HatchSilk(0, Near(want).Id);
            Run(gs, (int)((Config.SilkUpTime + Config.TrapSpinTime * 3 + 3) * 30));
            await Wait(1f);

            // ---- S2: the spinnery beside the mound
            gs.Camera.PitchOverride = 38;
            gs.Camera.JumpTo(want, 15); await Shot("spinnery-close", 2.5f);
            gs.Camera.PitchOverride = 0;
            gs.Camera.JumpTo((want + home) / 2, 55); await Shot("spinnery", 1.5f);

            // ---- the defence: web post, web tower, trap nest
            gs.Camera.PitchOverride = 38;
            gs.Camera.JumpTo(home + along * 12, 24); await Shot("works-close", 2f);
            gs.Camera.PitchOverride = 0;
            gs.Camera.JumpTo(home + along * 8, 62); await Shot("works", 1.5f);

            // ---- S3: a trap web across the approach, raiders marching into it
            var trap = sim.Traps[0].Pos;
            var into = (trap - home).Normalized();
            var across = new Vector2(-into.Y, into.X);
            for (int k = 0; k < 6; k++)
            {
                int id = sim.Spawn(1, AntType.Soldier, trap + into * (k < 2 ? 0.3f : 1.5f + k * 1.2f) + across * ((k % 2) - 0.5f) * 0.9f, (-into).AngleD());
                sim.Ants[id].State = AntState.Hold;
            }
            Run(gs, 8);
            await Wait(0.6f);
            gs.SetPaused(true);
            gs.Camera.PitchOverride = 38;
            gs.Camera.JumpTo(trap, 13); await Shot("trap-close", 2f);
            gs.Camera.PitchOverride = 0;
            gs.Camera.JumpTo(trap, 50); await Shot("trap", 1.5f);
            gs.SetPaused(false);

            // ---- S1: a spider's web holding two ants: the snare tent, the bands, the tension lines
            var at = home - along * 14 + side * 6;
            sim.Webs.Add(new WebZone { Pos = at, R = 2.4f, Life = 400, Max = 404, Root = 1.2f });
            int a1 = sim.Spawn(0, AntType.Worker, at + new Vector2(0.3f, 0.2f), 0.7f);
            int a2 = sim.Spawn(0, AntType.Soldier, at + new Vector2(-1.1f, -0.6f), 2.4f);
            sim.Ants[a1].State = AntState.Hold; sim.Ants[a2].State = AntState.Hold;
            int free = sim.Spawn(0, AntType.Worker, at + new Vector2(3.6f, 2.2f), 3.6f);
            sim.Ants[free].State = AntState.Hold;
            Run(gs, 6);
            sim.Ants[a1].Root = 5; sim.Ants[a2].Root = 5;
            await Wait(0.6f);
            gs.SetPaused(true);
            gs.Camera.PitchOverride = 38;
            gs.Camera.JumpTo(at, 11); await Shot("snare-close", 2f);
            gs.Camera.PitchOverride = 0;
            gs.Camera.JumpTo(at, 50); await Shot("snare", 1.5f);

            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://campaign_silkcap.cfg"));
            CampaignProgress.UsePath("user://campaign.cfg");
            GD.Print("[silk-capture] PASS");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[silk-capture] FAIL " + e); app.GetTree().Quit(1); }
    }
}
