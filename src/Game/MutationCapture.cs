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
            void Run(GameSession g, int frames) { for (int i = 0; i < frames; i++) g.Sim.Tick(1f / 30); }

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

            // ---- H2: the gland cut free and hauled home by workers, soldiers walking guard
            gs.SetPaused(false);
            gland.Done = gland.Work;
            var haulAt = gland.Pos + toward * 14;
            int carrier = sim.Spawn(0, AntType.Worker, haulAt, toward.AngleD());
            sim.Mutagens[0].Pos = haulAt;
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

            // ---- the radiating core: a queen's core (strength 3) in the carrier's jaws at play zoom; a general's burst;
            // the queen's mutation with its column of light (targets M2, M3: docs/art-targets/REVIEW-mutation-core.jpg)
            gland.Strength = 3;
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
            sim.Col[0].Mutagen = 1; sim.Col[0].Cores = 3;
            sim.Mission.Fixed = Mutation.Stinger;
            sim.Col[0].Mutations &= ~(int)Mutation.Stinger;
            gs.Camera.PitchOverride = 38;
            gs.Camera.JumpTo(home - toward * 6, 58);
            await Wait(0.8f);
            // called outside a tick, its event would be cleared before the frame reads it: hand it to the session directly
            if (sim.Mutate(0, Mutation.Stinger)) gs.MutationMoment(sim.Events[^1]);
            await Shot("queen-beam", 0.7f);
            gs.Camera.PitchOverride = 0;

            // ---- H3: the royal nursery, the larva's bed and the heap of food half piled
            app.StartEpisode(Reveal.RoyalLarvaId);
            gs = app.Session; gs.Hud.CloseBriefing();
            sim = gs.Sim;
            sim.Mutate(0, Mutation.RoyalLarva);
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

            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://campaign_mutcap.cfg"));
            CampaignProgress.UsePath("user://campaign.cfg");
            GD.Print("[mutation-capture] PASS");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[mutation-capture] FAIL " + e); app.GetTree().Quit(1); }
    }
}
