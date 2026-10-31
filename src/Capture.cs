using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Mandible;

/// Scripted screenshot tour for automated visual review:  -- --capture=<dir>
public static class Capture
{
    static Main _app;
    static string _dir;

    public static async Task Run(Main app, string dir)
    {
        _app = app;
        _dir = dir;
        DirAccess.MakeDirRecursiveAbsolute(dir);
        await Wait(6);
        await Shot("01_menu");
        app.Menu.OpenLobby();
        await Wait(3f);
        await Shot("01b_lobby");
        app.Menu.PickRace();
        await Wait(2.5f);
        await Shot("01c_races");
        int g = 0;
        foreach (var tab in new[] { "Colonies", "Castes", "Wildlife", "Foraging", "Food", "Colony Needs" })
        {
            app.Menu.ShowGuide(tab);
            await Wait(tab == "Castes" ? 3f : 1.5f);
            await Shot($"01d{(char)('a' + g++)}_guide_{tab.Replace(" ", "").ToLower()}");
        }

        app.StartBattle(Difficulty.Normal);
        await Wait(3);
        var s = app.Session;
        var nest = s.World.Nests[Config.Player].Pos;
        s.Camera.JumpTo(nest + new Vector2(6, -8), 58);
        await Wait(2);
        await Shot("02_nest");
        s.Camera.JumpTo(nest + new Vector2(5, -7), 18);
        await Wait(2);
        await Shot("03_closeup");

        await InputSelfTest(s);
        await BugShoot(s);

        // let the economy run
        s.Autopilot(true);
        s.FastForward = 7;
        await Wait(22);
        s.FastForward = 0;
        s.Autopilot(false);
        Log(s, "after fast-forward");
        s.Camera.JumpTo(nest + new Vector2(30, -30), 110);
        s.ToggleTrails();
        await Wait(2.5f);
        await Shot("04_trails");
        s.ToggleTrails();

        // stage a battle in the middle of the yard
        var c = new Vector2(Config.MapSize, Config.MapSize) * 0.5f;
        var mine = new List<int>();
        var theirs = new List<int>();
        var rng = new Rng(5);
        for (int i = 0; i < 46; i++)
        {
            mine.Add(s.Sim.Spawn(Config.Player, i % 9 == 0 ? AntType.Major : AntType.Soldier, c + new Vector2(-26, 22) + rng.InDisk(9), -0.8f));
            theirs.Add(s.Sim.Spawn(Config.Enemy, i % 10 == 0 ? AntType.Major : AntType.Soldier, c + new Vector2(26, -22) + rng.InDisk(9), 2.3f));
        }
        s.Sim.FormSquads(mine);
        s.Sim.FormSquads(theirs);
        s.Sim.CmdMove(mine, c + new Vector2(10, -8), true);
        s.Sim.CmdMove(theirs, c + new Vector2(-10, 8), true);
        s.Camera.JumpTo(c + new Vector2(-6, 6), 46);
        await Wait(4.5f);
        await Shot("05_battle");
        s.Camera.JumpTo(c + new Vector2(-3, 3), 22);
        await Wait(2f);
        await Shot("06_battle_close");
        s.Input.SelectArmy();
        s.Camera.JumpTo(c + new Vector2(-8, 8), 70);
        await Wait(2.5f);
        await Shot("07_selected");
        Log(s, "end");

        // click-inspect a food pile
        FoodSource berry = null;
        foreach (var f in s.World.Food) if (f.Alive && f.Kind == FoodKind.Berry && (berry == null || f.Pos.DistanceTo(nest) < berry.Pos.DistanceTo(nest))) berry = f;
        if (berry != null)
        {
            s.Fog.RevealAll = true;
            s.Camera.JumpTo(berry.Pos + new Vector2(3, -3), 30);
            await Wait(1.5f);
            var screen = s.Camera.Cam.UnprojectPosition(s.World.HF.Point(berry.Pos, 0.5f));
            var picked = s.Input.PickFood(screen);
            GD.Print($"[selftest] click on berry picks {(picked == berry ? "it  PASS" : "something else  FAIL")}");
            s.Input.ClearSelection();
            s.Input.InspectFood = berry;
            s.Sim.Col[Local.Me].SugarT = 12; s.Sim.Col[Local.Me].JuiceT = 20; s.Sim.Col[Local.Me].ProteinT = 8;
            await Wait(1f);
            await Shot("08b_food_inspect");
        }

        // lead the red fire ants instead
        app.StartBattle(Difficulty.Normal, 1);
        await Wait(3);
        s = app.Session;
        var redNest = s.World.Nests[1].Pos;
        float camD = new Vector2(s.Camera.Focus.X, s.Camera.Focus.Z).DistanceTo(redNest);
        GD.Print($"[selftest] playing red: Local.Me={Local.Me}, camera starts {camD:0} from the red nest  {(Local.Me == 1 && camD < 30 ? "PASS" : "FAIL")}");
        await Shot("19_red_start");
        s.Camera.JumpTo(redNest + new Vector2(-5, 7), 22);
        await Wait(2);
        await Shot("20_red_close");
        _app.GetTree().Quit();
    }

    /// Drives real input events through Godot: box-select, right-click move, hotkeys.
    static async Task InputSelfTest(GameSession s)
    {
        var vp = _app.GetViewport().GetVisibleRect().Size;
        s.Camera.JumpTo(s.World.Nests[Config.Player].Pos, 60);
        await Wait(1);
        void Mouse(MouseButton b, bool pressed, Vector2 at)
        {
            var e = new InputEventMouseButton { ButtonIndex = b, Pressed = pressed, Position = at, GlobalPosition = at };
            Input.ParseInputEvent(e);
        }
        void Move(Vector2 at)
        {
            Input.ParseInputEvent(new InputEventMouseMotion { Position = at, GlobalPosition = at, ButtonMask = MouseButtonMask.Left });
        }
        void Key(Key k)
        {
            Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
            Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
        }
        // drag a box over the middle of the screen
        var a = vp * new Vector2(0.2f, 0.12f);
        var b = vp * new Vector2(0.8f, 0.68f);
        Mouse(MouseButton.Left, true, a);
        await Frames(2);
        Move((a + b) / 2);
        await Frames(2);
        Move(b);
        await Frames(2);
        Mouse(MouseButton.Left, false, b);
        await Frames(3);
        int selected = s.Input.Selection.Count;
        GD.Print($"[selftest] box-select picked {selected} ants  {(selected > 0 ? "PASS" : "FAIL")}");
        // right-click a point on the ground
        var target = vp * new Vector2(0.62f, 0.3f);
        Mouse(MouseButton.Right, true, target);
        await Frames(1);
        Mouse(MouseButton.Right, false, target);
        await Frames(3);
        int moving = 0;
        foreach (int id in s.Input.Selection) if (s.Sim.Ants[id].State is AntState.Move or AntState.Gather) moving++;
        GD.Print($"[selftest] right-click ordered {moving}/{selected} ants  {(moving > 0 && moving == selected ? "PASS" : "FAIL")}");
        // hotkeys: worker + soldier into the brood queue, Esc toggles pause
        int before = s.Sim.Col[Config.Player].Queue.Count;
        Key(Godot.Key.Z);
        Key(Godot.Key.X);
        await Frames(3);
        int after = s.Sim.Col[Config.Player].Queue.Count;
        GD.Print($"[selftest] Z/X queued {after - before} brood  {(after - before == 2 ? "PASS" : "FAIL")}");
        Key(Godot.Key.Escape);
        await Frames(3);
        bool paused = s.Hud.Blocking;
        Key(Godot.Key.Escape);
        await Frames(3);
        GD.Print($"[selftest] Esc pause/resume  {(paused && !s.Hud.Blocking ? "PASS" : "FAIL")}");
        Key(Godot.Key.F2);
        await Frames(2);
        GD.Print($"[selftest] F2 army select {s.Input.Selection.Count}  {(s.Input.Selection.Count == s.Sim.Col[0].Count[1] + s.Sim.Col[0].Count[2] + s.Sim.Col[0].Count[8] + s.Sim.Col[0].Count[9] ? "PASS" : "FAIL")}");
    }

    /// Close-ups of every wild bug species, then a fight that promotes some soldiers.
    static async Task BugShoot(GameSession s)
    {
        Camp Find(AntType t)
        {
            foreach (var c in s.World.Camps) foreach (var b in c.Bugs) if (b.type == t) return c;
            return null;
        }
        int n = 8;
        s.Fog.RevealAll = true;
        foreach (var t in new[] { AntType.Beetle, AntType.Spider, AntType.StagBeetle })
        {
            var camp = Find(t);
            if (camp == null) { GD.Print($"[bugshoot] no camp with {t}"); continue; }
            int bug = -1;
            for (int i = 0; i < s.Sim.High; i++)
                if (s.Sim.Ants[i].Alive && s.Sim.Ants[i].Type == t && s.Sim.Ants[i].Anchor.DistanceTo(camp.Pos) < 8) { bug = i; break; }
            var at = bug >= 0 ? s.Sim.Ants[bug].Pos : camp.Pos;
            s.Camera.JumpTo(at, t == AntType.StagBeetle ? 26 : 21);
            s.Camera.SetYaw(camp.Facing + Mathf.Pi * 0.6f, true);
            await Wait(1.8f);
            await Shot($"{n++:00}_{t.ToString().ToLower()}");
        }
        // wasp lair close-up
        var waspCamp = Find(AntType.Wasp);
        if (waspCamp != null)
        {
            s.Camera.JumpTo(waspCamp.Pos, 20);
            s.Camera.SetYaw(waspCamp.Facing + 1.2f, true);
            await Wait(1.6f);
            await Shot($"{n++:00}_wasp");
        }
        // a fully developed colony: every chamber dug, heroes and new castes on parade
        var col = s.Sim.Col[Config.Player];
        foreach (var (u, tier) in new[] { (Upgrade.Queen, 5), (Upgrade.Wings, 1), (Upgrade.Mandibles, 3), (Upgrade.Chitin, 3), (Upgrade.SilkCutters, 1) })
            for (int k = col.T(u) + 1; k <= tier; k++) { col.Tier[(int)u] = k; s.Chambers.Build(Config.Player, u, k); }
        var home = s.World.Nests[Config.Player].Pos;
        var toC = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - home).Normalized();
        var side = new Vector2(-toC.Y, toC.X);
        var parade = new List<int>();
        AntType[] line = { AntType.Warlord, AntType.Matron, AntType.Marshal, AntType.Gunner, AntType.Flyer, AntType.Major, AntType.Soldier };
        for (int k = 0; k < line.Length; k++)
        {
            int id = s.Sim.Spawn(Config.Player, line[k], home + toC * 16 + side * (k - 3) * 3.4f, toC.Angle(), Config.IsHero(line[k]) ? 6 : 1);
            s.Sim.Ants[id].State = AntState.Hold;
            parade.Add(id);
        }
        s.Camera.JumpTo(home + toC * 16, 26);
        s.Camera.SetYaw(-toC.Angle() + Mathf.Pi * 0.5f, true);
        await Wait(2f);
        await Shot($"{n++:00}_heroes");
        s.Camera.JumpTo(s.Sim.Ants[parade[1]].Pos, 14);
        await Wait(1.2f);
        await Shot($"{n++:00}_matron_close");
        s.Camera.JumpTo(home - toC * 6, 46);
        await Wait(1.5f);
        await Shot($"{n++:00}_chambers");
        // heroes and a squad take on the spider lair: webs, war cry, acid
        var spiderCamp = Find(AntType.Spider);
        if (spiderCamp != null)
        {
            foreach (int id in parade) { s.Sim.Ants[id].Pos = spiderCamp.Pos + MathX.Dir(spiderCamp.Facing) * 16 + new Rng((uint)id).InDisk(3); }
            var army = new List<int>(parade);
            for (int k = 0; k < 12; k++) army.Add(s.Sim.Spawn(Config.Player, AntType.Soldier, spiderCamp.Pos + MathX.Dir(spiderCamp.Facing) * 18 + new Rng((uint)(k + 40)).InDisk(4), spiderCamp.Facing + Mathf.Pi));
            s.Sim.FormSquads(army);
            s.Sim.CmdMove(army, spiderCamp.Pos, true);
            s.Camera.JumpTo(spiderCamp.Pos + MathX.Dir(spiderCamp.Facing) * 6, 30);
            await Wait(3.2f);
            await Shot($"{n++:00}_lair_fight");
            await Wait(2.5f);
            await Shot($"{n++:00}_lair_fight2");
        }
        s.Fog.RevealAll = false;
        // twenty soldiers take on the stag beetle lord
        var stag = Find(AntType.StagBeetle);
        var squad = new List<int>();
        for (int i = 0; i < 22; i++) squad.Add(s.Sim.Spawn(Config.Player, i % 6 == 0 ? AntType.Major : AntType.Soldier, stag.Pos + MathX.Dir(stag.Facing) * 20 + new Rng((uint)(i + 3)).InDisk(6), stag.Facing + Mathf.Pi));
        s.Sim.FormSquads(squad);
        s.Sim.CmdMove(squad, stag.Pos, true);
        s.Camera.JumpTo(stag.Pos + MathX.Dir(stag.Facing) * 4, 30);
        await Wait(3.5f);
        await Shot($"{n++:00}_stag_fight");
        await Wait(9f);
        int vets = 0, best = -1;
        foreach (int id in squad)
            if (s.Sim.Ants[id].Alive && s.Sim.Ants[id].Level > 1) { vets++; if (best < 0 || s.Sim.Ants[id].Level > s.Sim.Ants[best].Level) best = id; }
        GD.Print($"[bugshoot] squad veterans after stag fight: {vets}, best level {(best >= 0 ? s.Sim.Ants[best].Level : 1)}, wild left {s.Sim.WildCount}");
        if (best >= 0)
        {
            var list = new List<int>();
            foreach (int id in squad) if (s.Sim.Ants[id].Alive) list.Add(id);
            s.Sim.CmdStop(list, false);
            s.Input.SelectArmy();
            s.Camera.JumpTo(s.Sim.Ants[best].Pos, 16);
            await Wait(1.5f);
            await Shot($"{n++:00}_veterans");
        }
        // the battered squads march home, heal and hatch replacements for the fallen
        var survivors = new List<int>();
        foreach (int id in squad) if (s.Sim.Ants[id].Alive) survivors.Add(id);
        if (survivors.Count > 0)
        {
            string Strength()
            {
                var parts = new List<string>();
                foreach (var q in s.Sim.Squads) if (q.Team == Config.Player && q.Alive) parts.Add($"{q.Type}{q.Count}/{q.Size}");
                return string.Join(" ", parts);
            }
            GD.Print($"[bugshoot] squads before reinforcing: {Strength()} food {s.Sim.Col[Config.Player].Food:0}");
            s.Sim.Col[Config.Player].Food += 600;
            s.Sim.Col[Config.Player].PopCap = 400; // the tour spawned far past the brood cap; give the recruits room
            s.Sim.CmdReinforce(survivors);
            s.Camera.JumpTo(s.Sim.W.Nests[Config.Player].Pos, 34);
            Engine.TimeScale = 4;
            await Wait(12f); // real seconds: about 48 s of game time
            Engine.TimeScale = 1;
            await Wait(0.5f);
            await Shot($"{n++:00}_reinforced");
            GD.Print($"[bugshoot] squads after reinforcing: {Strength()} food {s.Sim.Col[Config.Player].Food:0}");
        }
    }

    static async Task Frames(int n)
    {
        for (int i = 0; i < n; i++) await _app.ToSignal(_app.GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    static int _abilities;

    /// Network self-test: --nettest=host[,seconds] or --nettest=join[:address][,seconds].
    /// Two processes play a full AI-vs-AI match over a real ENet connection and compare checksums.
    public static async Task NetTest(Main app, string spec)
    {
        _app = app;
        var parts = spec.Split(',');
        float seconds = parts.Length > 1 ? float.Parse(parts[1]) : 600;
        string shotDir = parts.Length > 2 ? parts[2] : null;
        bool shotTaken = false;
        var net = new Lockstep();
        app.AddChild(net);
        net.OnStart += () =>
        {
            GD.Print($"[nettest] start: this side leads team {net.Me} ({Local.ColonyName(net.Me)})");
            app.StartMultiplayer(net, autoPlay: true, fastForward: shotDir != null ? 0 : 7);
        };
        bool host = parts[0].StartsWith("host");
        if (host)
        {
            if (parts[0] == "host-red") net.HostTeam = 1; // the host leads the red fire ants
            net.OnConnected += () => net.StartMatch();
            if (net.Host("HostBot", false) != Error.Ok) { GD.Print($"[nettest] host failed: {net.Status}"); app.GetTree().Quit(1); return; }
        }
        else
        {
            string addr = parts[0].Contains(':') ? parts[0][(parts[0].IndexOf(':') + 1)..] : "127.0.0.1";
            await Wait(1.5f);
            net.Join(addr, "JoinBot");
        }
        GD.Print($"[nettest] {(host ? "host" : "join")} started: {net.Status}");
        double t0 = Time.GetTicksMsec() / 1000.0;
        float lastReport = 0;
        while (true)
        {
            await Wait(1f);
            double wall = Time.GetTicksMsec() / 1000.0 - t0;
            var s = app.Session;
            if (net.State == Lockstep.Phase.Closed) { GD.Print($"[nettest] connection closed: {net.Status}"); break; }
            if (s == null || s.Demo || s.Net == null)
            {
                if (wall > 30) { GD.Print($"[nettest] timed out waiting for match ({net.Status})"); break; }
                continue;
            }
            if (shotDir != null && !shotTaken && s.Sim.Time > 45)
            {
                shotTaken = true;
                _dir = shotDir;
                DirAccess.MakeDirRecursiveAbsolute(shotDir);
                s.Camera.JumpTo(s.World.Nests[Local.Me].Pos, 70);
                await Wait(1.5f);
                await Shot(host ? "mp_host" : "mp_join");
            }
            if (s.Sim.Time - lastReport >= 60)
            {
                lastReport = s.Sim.Time;
                GD.Print($"[nettest] t={s.Sim.Time / 60:0}m ticks={s.Sim.Ticks} checks ok={net.ChecksOk} desync={net.Desyncs} ping={net.PingMs:0}ms delay={net.Delay} stall={s.NetStall:0.0}s ants={s.Sim.Col[0].PopUsed}/{s.Sim.Col[1].PopUsed} wall={wall:0}s");
            }
            if (s.Sim.Time >= seconds || s.Sim.Winner >= 0)
            {
                GD.Print($"[nettest] DONE t={s.Sim.Time / 60:0.0}m ticks={s.Sim.Ticks} checks ok={net.ChecksOk} desyncs={net.Desyncs} winner={s.Sim.Winner} checksum={s.Sim.Checksum():X8}");
                break;
            }
        }
        net.Leave();
        await Wait(0.5f);
        app.GetTree().Quit();
    }

    /// Headless AI-vs-AI match:  --headless -- --simtest=<seconds>
    public static void SimTest(Main app, float seconds, Difficulty a, Difficulty b, uint seed = 20261031)
    {
        var root = new Node3D();
        app.AddChild(root);
        var world = WorldBuilder.Build(root, seed);
        var sim = new Sim(world);
        var ais = new[] { new AIController(sim, 0, a), new AIController(sim, 1, b) };
        const float dt = 1f / 30f;
        var sw = new System.Diagnostics.Stopwatch();
        double worst = 0, total = 0;
        int ticks = 0, peak = 0;
        float nextLog = 60;
        while (sim.Time < seconds && sim.Winner < 0)
        {
            sw.Restart();
            sim.Tick(dt);
            foreach (var ai in ais) ai.Tick(dt);
            foreach (var e in sim.Events) if (e.Kind is EvKind.WebShot or EvKind.Sweep or EvKind.Sting or EvKind.WarCry or EvKind.AcidBurst or EvKind.DiveBomb) _abilities++;
            sw.Stop();
            double ms = sw.Elapsed.TotalMilliseconds;
            total += ms; ticks++;
            if (ms > worst) worst = ms;
            int alive = 0;
            for (int i = 0; i < sim.High; i++) if (sim.Ants[i].Alive) alive++;
            peak = System.Math.Max(peak, alive);
            if (sim.Time >= nextLog)
            {
                nextLog += 60;
                var p = sim.Col[0]; var e = sim.Col[1];
                int v0 = 0, v1 = 0, top = 1;
                for (int i = 0; i < sim.High; i++)
                    if (sim.Ants[i].Alive && sim.Ants[i].Level > 1 && sim.Ants[i].Team < 2)
                    { if (sim.Ants[i].Team == 0) v0++; else v1++; top = System.Math.Max(top, sim.Ants[i].Level); }
                int sq0 = 0, sq1 = 0, full = 0, ranked = 0;
                foreach (var sq in sim.Squads) if (sq.Alive) { if (sq.Team == 0) sq0++; else sq1++; if (sq.Count == sq.Size) full++; if (sq.Level > 1) ranked++; }
                GD.Print($"[simtest] t={sim.Time / 60:0}m wild={sim.WildCount} veterans {v0}/{v1} top Lv{top} squads {sq0}/{sq1} full {full} ranked {ranked}");
                string Heroes(int team)
                {
                    var parts = new System.Collections.Generic.List<string>();
                    for (int i = 0; i < sim.High; i++)
                        if (sim.Ants[i].Alive && sim.Ants[i].Team == team && Config.IsHero(sim.Ants[i].Type)) parts.Add($"{sim.Ants[i].Type}{sim.Ants[i].Level}");
                    return string.Join(",", parts);
                }
                var c0 = sim.Col[0]; var c1 = sim.Col[1];
                var states = new System.Collections.Generic.Dictionary<string, int>();
                int rooted = 0, killedByWild = 0;
                for (int i = 0; i < sim.High; i++)
                    if (sim.Ants[i].Alive && sim.Ants[i].Team == 0 && Config.IsWarrior(sim.Ants[i].Type))
                    {
                        var k = sim.Ants[i].State.ToString();
                        states[k] = states.GetValueOrDefault(k) + 1;
                        if (sim.Ants[i].Root > 0) rooted++;
                    }
                GD.Print($"[simtest]    black warrior states: {string.Join(" ", states)} rooted {rooted} queue [{string.Join(",", c0.Queue.ConvertAll(q => q.Kind.ToString()))}] researching {(c0.Researching ? c0.ResearchKind.ToString() : "-")}");
                string By(Colony c) => $"crumb {c.GatheredBy[0]:0} berry {c.GatheredBy[1]:0} sugar {c.GatheredBy[2]:0} seed {c.GatheredBy[3]:0} corpse {c.GatheredBy[4]:0}";
                GD.Print($"[simtest]    food black: {By(c0)} | red: {By(c1)}");
                GD.Print($"[simtest]    queen {c0.T(Upgrade.Queen)}/{c1.T(Upgrade.Queen)} forge {c0.T(Upgrade.Mandibles)}/{c1.T(Upgrade.Mandibles)} chitin {c0.T(Upgrade.Chitin)}/{c1.T(Upgrade.Chitin)} wings {c0.T(Upgrade.Wings)}/{c1.T(Upgrade.Wings)} heroes [{Heroes(0)}] vs [{Heroes(1)}] gunners {c0.Count[9]}/{c1.Count[9]} flyers {c0.Count[8]}/{c1.Count[8]} webs {sim.Webs.Count} abilities {_abilities}");
                GD.Print($"[simtest] t={sim.Time / 60:0}m ants={alive} | black {p.Count[0]}w {p.Count[1]}s {p.Count[2]}m pop {p.PopUsed}/{p.PopCap} food {p.Food:0} gathered {p.Gathered:0} killed {p.Killed} | red {e.Count[0]}w {e.Count[1]}s {e.Count[2]}m pop {e.PopUsed}/{e.PopCap} food {e.Food:0} gathered {e.Gathered:0} killed {e.Killed} | tick avg {total / ticks:0.00}ms worst {worst:0.0}ms");
                worst = 0;
            }
        }
        GD.Print($"[simtest] finished t={sim.Time / 60:0.0}m winner={(sim.Winner < 0 ? "none" : sim.Winner == 0 ? "black" : "red")} peak ants={peak} avg tick {total / ticks:0.00}ms");
        app.GetTree().Quit();
    }

    static void Log(GameSession s, string tag)
    {
        var p = s.Sim.Col[0];
        var e = s.Sim.Col[1];
        GD.Print($"[capture] {tag}: t={s.Sim.Time:0} fps={Engine.GetFramesPerSecond()} ants={p.Count[0]}w/{p.Count[1]}s/{p.Count[2]}m vs {e.Count[0]}w/{e.Count[1]}s/{e.Count[2]}m food={p.Food:0}/{e.Food:0} gathered={p.Gathered:0}/{e.Gathered:0}");
    }

    static async Task Wait(float sec)
    {
        await _app.ToSignal(_app.GetTree().CreateTimer(sec, true, false, true), SceneTreeTimer.SignalName.Timeout);
    }

    static async Task Shot(string name)
    {
        await _app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
        var img = _app.GetViewport().GetTexture().GetImage();
        string path = $"{_dir}/{name}.png";
        img.SavePng(path);
        GD.Print($"[capture] {path} fps={Engine.GetFramesPerSecond()}");
    }
}
