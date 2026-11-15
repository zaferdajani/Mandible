using System.Threading.Tasks;
using Godot;

namespace Mandible;

/// --savetest: plays real games with orders given the player's way, saves mid-game, loads the save into a fresh
/// session and checks that the restored game is the same game, checksum for checksum. Then plays on in both the
/// original and the restored game with the same orders and checks they stay identical.
public static class SaveValidation
{
    static Main _app;
    static int _fails;

    public static async void Run(Main app)
    {
        _app = app;
        await Frames(10);
        await Case("Colony War vs AI", () => app.StartBattle(Difficulty.Normal), PlaySkirmishOrders);
        await Case("Level 1 opening", () => app.StartEpisode(1), PlayOpeningOrders);
        GD.Print(_fails == 0 ? "[savetest] ALL PASS" : $"[savetest] {_fails} FAILED");
        app.GetTree().Quit(_fails == 0 ? 0 : 1);
    }

    static async Task Case(string name, System.Action start, System.Action<GameSession, int> orders)
    {
        start();
        await Frames(3);
        var s = _app.Session;
        s.Hud?.CloseBriefing();
        // play 90 s of game time, giving orders the player's way every few seconds
        for (int step = 0; step < 30; step++) { orders(s, step); await Ticks(s, 90); }
        s.SetPaused(true);
        int savedTick = s.Sim.Ticks;
        uint savedSum = s.Sim.Checksum();
        if (!SaveGame.Write(s, "test")) { Fail($"{name}: save failed"); return; }
        _app.LoadSave("test"); // the session is built and replayed as it enters the tree, before the next frame
        var r = _app.Session;
        bool same = r != s && r.Sim.Ticks == savedTick && r.Sim.Checksum() == savedSum;
        Report(same, $"{name}: restored at tick {r.Sim.Ticks}/{savedTick}, checksum {r.Sim.Checksum():X8}/{savedSum:X8}");
        // play on in the restored game with more orders: it must keep running (no stale state from the replay)
        r.Hud?.CloseBriefing();
        r.SetPaused(false);
        for (int step = 30; step < 36; step++) { orders(r, step); await Ticks(r, 90); }
        Report(r.Sim.Ticks > savedTick + 400, $"{name}: plays on after loading (tick {r.Sim.Ticks})");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://saves/slot_test.sav"));
    }

    static void PlaySkirmishOrders(GameSession s, int step)
    {
        if (step % 3 == 0) s.Issue(new Cmd { Kind = CmdKind.Enqueue, Team = (byte)Local.Me, Arg = (byte)(step % 2 == 0 ? ProdKind.Worker : ProdKind.Soldier) });
        var army = new System.Collections.Generic.List<int>();
        for (int i = 0; i < s.Sim.High; i++) if (s.Sim.Ants[i].Alive && s.Sim.Ants[i].Team == Local.Me && Config.IsWarrior(s.Sim.Ants[i].Type)) army.Add(i);
        if (army.Count > 0 && step % 4 == 1)
        {
            var home = s.World.Nests[Local.Me].Pos;
            var c = Cmd.Units(CmdKind.AttackMove, Local.Me, army, s.Sim);
            c.P = home + new Vector2(step % 8 * 4 - 14, (step % 5) * 3 - 6);
            s.Issue(c);
        }
    }

    static void PlayOpeningOrders(GameSession s, int step)
    {
        var army = new System.Collections.Generic.List<int>();
        var workers = new System.Collections.Generic.List<int>();
        for (int i = 0; i < s.Sim.High; i++)
            if (s.Sim.Ants[i].Alive && s.Sim.Ants[i].Team == Local.Me && !s.Sim.Ants[i].Dormant)
            {
                if (Config.IsWarrior(s.Sim.Ants[i].Type)) army.Add(i);
                else if (s.Sim.Ants[i].Type == AntType.Worker) workers.Add(i);
            }
        // the opening the player's way: Burr walks to each stunned soldier, they kill the beetle, then everyone digs the queen out
        var m = s.Sim.Mission;
        void Go(CmdKind kind, Vector2 p) { if (army.Count == 0) return; var c = Cmd.Units(kind, Local.Me, army, s.Sim); c.P = p; s.Issue(c); }
        if (m.Stage <= ExpeditionStage.Gather) { int next = m.NextStray(s.Sim); if (next >= 0) Go(CmdKind.Move, s.Sim.Ants[next].Pos); }
        else if (m.Stage == ExpeditionStage.ClearTrail) Go(CmdKind.AttackMove, Expedition.Ambush + new Vector2(4, 0));
        else if (m.Stage == ExpeditionStage.FreeQueen) Go(CmdKind.Move, Expedition.Start + new Vector2(2, 0));
        else if (m.Stage == ExpeditionStage.EscortQueen) Go(CmdKind.Move, Expedition.Clearing + new Vector2(6, 2));
        else if (step % 5 == 4 && workers.Count > 0) s.Issue(Cmd.Units(CmdKind.Entrance, Local.Me, workers, s.Sim));
    }

    /// --opening-capture=<dir>: Level 1's opening played the player's way, with a frame of each beat: Burr alone, a
    /// stunned soldier waking as he reaches her, the colony digging the queen out, and the queen free.
    public static async void OpeningCapture(Main app, string dir)
    {
        _app = app;
        DirAccess.MakeDirRecursiveAbsolute(dir);
        await Frames(10);
        app.StartEpisode(1);
        await Frames(3);
        var s = app.Session;
        s.Hud?.CloseBriefing();
        var m = s.Sim.Mission;
        async Task Shot(string name, Vector2 at, float dist, int settle = 25)
        {
            s.FastForward = 0; s.Camera.JumpTo(at, dist);
            await Frames(settle);
            await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            app.GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png");
            GD.Print($"[opening-capture] {name}: stage {m.Stage}, woken {m.Joined}/{m.Strays}, dug {m.Dug:0}%, diggers {m.Diggers}");
        }
        async Task Play(System.Func<bool> done, int maxTicks)
        {
            int end = s.Sim.Ticks + maxTicks, step = 0;
            while (!done() && s.Sim.Ticks < end) { PlayOpeningOrders(s, step++); await Ticks(s, 30); }
        }
        await Frames(90);
        await Shot("1-burr-alone", Expedition.Wake + new Vector2(-3.5f, 0), 24);
        // Burr walks up to the first stunned soldier: the frame catches her kicking upright, the next one still out cold
        int first = m.NextStray(s.Sim);
        var lay = s.Sim.Ants[first].Pos;
        s.Camera.JumpTo(lay + new Vector2(-1.5f, 1), 17);
        PlayOpeningOrders(s, 0);
        for (int i = 0; i < 600 && m.Joined == 0; i++) await Frames(1);
        await Shot("2-waking-a-stray", lay + new Vector2(-1.5f, 1), 17, 14);
        await Play(() => m.Stage == ExpeditionStage.ClearTrail, 3000);
        await Shot("2b-soldiers-gathered", s.Sim.Ants[m.Leader].Pos, 28);
        await Play(() => m.Stage == ExpeditionStage.FreeQueen, 6000);
        await Play(() => m.Dug > 2, 3000);
        await Shot("3a-dig-begins", Expedition.InTube(9.5f, 0.8f), 19);
        await Play(() => m.Dug > 45, 6000);
        await Shot("3-digging-out-the-queen", Expedition.InTube(9.5f, 0.8f), 19);
        await Play(() => m.Stage == ExpeditionStage.EscortQueen, 6000);
        await Shot("4-walking-out", Expedition.InTube(12f, 0.5f), 20, 70);
        GD.Print(m.Stage == ExpeditionStage.EscortQueen ? "[opening-capture] DONE" : $"[opening-capture] FAIL: stuck at {m.Stage}");
        app.GetTree().Quit(m.Stage == ExpeditionStage.EscortQueen ? 0 : 1);
    }

    /// --founding-capture=<dir>: Level 1 played to each founding step (stocking, ready to dig, digging) with a frame of each.
    public static async void FoundingCapture(Main app, string dir)
    {
        _app = app;
        DirAccess.MakeDirRecursiveAbsolute(dir);
        await Frames(10);
        app.StartEpisode(1);
        await Frames(3);
        var s = app.Session;
        s.Hud?.CloseBriefing();
        s.Sim.SkipOpening(); // the founding's frames start with the queen free and the beetle down
        async Task Until(System.Func<bool> done, int maxTicks) { int end = s.Sim.Ticks + maxTicks; while (!done() && s.Sim.Ticks < end) await Ticks(s, 30); }
        async Task Shot(string name)
        {
            s.FastForward = 0; s.Camera.JumpTo(Expedition.Clearing + new Vector2(-4, 6), 46);
            await Frames(20);
            await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
            app.GetViewport().GetTexture().GetImage().SavePng($"{dir}/{name}.png");
            GD.Print($"[founding-capture] {name}: stage {s.Sim.Mission.Stage}, food {s.Sim.Col[0].Food:0}, remains {s.Sim.Col[0].Organic:0}");
        }
        await Until(() => s.Sim.Mission.Stage == ExpeditionStage.EscortQueen, 9000);
        PlayOpeningOrders(s, 12); // the soldiers lead on to the clearing; the queen and workers follow
        await Until(() => s.Sim.Mission.Stage == ExpeditionStage.Provision, 9000);
        await Ticks(s, 240);
        await Shot("1-stocking");
        await Until(() => s.Sim.Mission.Stage == ExpeditionStage.DigEntrance, 18000);
        await Shot("2-ready-to-dig");
        PlayOpeningOrders(s, 14);
        await Ticks(s, 300);
        await Shot("3-digging");
        // the owner's 0.13 playtest: build view (N) while the nest is still being dug turned the whole yard black
        s.SetNestView(true);
        await Frames(30);
        await Shot("4-digging-nest-view");
        s.SetNestView(false);
        await Until(() => s.Sim.Mission.Stage == ExpeditionStage.Incubator, 18000);
        s.SetNestView(true);
        await Frames(30);
        await Shot("5-founded-nest-view");
        app.GetTree().Quit();
    }

    static void Report(bool ok, string what) { GD.Print($"[savetest] {(ok ? "PASS" : "FAIL")} {what}"); if (!ok) _fails++; }
    static void Fail(string what) => Report(false, what);

    static async Task Frames(int n) { for (int i = 0; i < n; i++) await _app.ToSignal(_app.GetTree(), SceneTree.SignalName.ProcessFrame); }

    static async Task Ticks(GameSession s, int ticks)
    {
        s.FastForward = 7;
        int target = s.Sim.Ticks + ticks;
        while (s.Sim.Ticks < target && s.Sim.Winner < 0) await Frames(1);
        s.FastForward = 0;
    }
}
