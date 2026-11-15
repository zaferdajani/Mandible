using System;
using Godot;

namespace Mandible;

/// `--silk-capture=<abs dir>`: the silk in real frames for the art gate (targets S1-S3), each view twice:
/// `<view>-close.png` framed like its target and `<view>.png` at play zoom. Views: snare (a spider's web holding
/// ants), spinnery, trap (a trap web holding raiders).
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
            c.Mutations |= (int)Mutation.WebThrowing; c.WebRank = 1; c.Founded = true; c.Food += 300;
            gs.Hud.Visible = false;
            GD.Print("[silk-capture] spinnery ordered: " + sim.OrderSpinnery(0));
            Run(gs, (int)((Config.SpinneryTime + Config.TrapSpinTime * 3 + 3) * 30));
            gs.Ants.VisibleToPlayer = _ => true;
            await Wait(1f);

            // ---- S2: the spinnery beside the mound
            gs.Camera.PitchOverride = 38;
            gs.Camera.JumpTo(c.SpinPos, 15); await Shot("spinnery-close", 2.5f);
            gs.Camera.PitchOverride = 0;
            gs.Camera.JumpTo((c.SpinPos + home) / 2, 55); await Shot("spinnery", 1.5f);

            // ---- S3: a trap web across the approach, raiders marching into it
            var trap = sim.Traps[0].Pos;
            var along = (trap - home).Normalized();
            var side = new Vector2(-along.Y, along.X);
            for (int k = 0; k < 6; k++)
            {
                int id = sim.Spawn(1, AntType.Soldier, trap + along * (k < 2 ? 0.3f : 1.5f + k * 1.2f) + side * ((k % 2) - 0.5f) * 0.9f, (-along).AngleD());
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
            var at = home - along * 12 + side * 6;
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
