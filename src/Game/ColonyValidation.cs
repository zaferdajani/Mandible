using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;

namespace Mandible;

public static class ColonyValidation
{
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static List<int> Units(Sim s, AntType type)
    { var ids = new List<int>(); for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && s.Ants[i].Team == 0 && s.Ants[i].Type == type) ids.Add(i); return ids; }
    static void Until(Sim sim, Func<bool> done, int seconds, string reason)
    {
        for (int t = 0; t < seconds * 30 && !done(); t++)
        {
            sim.Tick(1f / 30);
            Check(sim.Col[0].Alive, "Queen died during " + reason);
            for (int i = 0; i < sim.High; i++) if (sim.Ants[i].Alive && sim.Ants[i].Layer == 0)
                Check(Expedition.Contains(sim.Ants[i].Pos), "An ant escaped the confined trail");
        }
        Check(done(), $"Timed out: {reason}; stage {sim.Mission?.Stage}; queen {sim.Ants[sim.Col[0].QueenId].Pos}; food {sim.Col[0].Food}");
        GD.Print("[campaign-test] PASS " + reason);
    }
    public static void Campaign(Main app)
    {
        try
        {
            Settings.Gfx = Settings.Quality.Low;
            var root = new Node3D(); app.AddChild(root);
            var w = WorldBuilder.Build(root, 20261031, firstNest: true);
            var s = new Sim(w); var c = s.Col[0];
            Check(s.Mission != null && c.Chambers.Count == 0 && !c.Founded, "Mission did not start on the trail");
            Check(!s.OrderEntrance(0, Units(s, AntType.Worker)), "Entrance allowed before reaching clearing");
            for (int i = 0; i < 900; i++) s.Tick(1f / 30);
            Check(!c.Founded && s.Winner < 0, "Mission auto-founded or won without the player's orders");
            Check(s.CanResearch(0, Upgrade.Queen) == ResearchResult.Locked && s.CanEnqueue(0, ProdKind.Major) == QueueResult.Locked, "Episode technology gate failed");
            var army = Units(s, AntType.Soldier);
            s.CmdMove(army, Expedition.Ambush, true);
            Until(s, () => s.Mission.Stage == ExpeditionStage.EscortQueen, 120, "attackers clear the enemy patrol");
            s.CmdMove(Units(s, AntType.Soldier), Expedition.Clearing + new Vector2(8, 0), false);
            Until(s, () => s.Mission.Stage == ExpeditionStage.DigEntrance, 150, "queen and workers reach the food clearing");
            Check(!s.OrderEntrance(0, Units(s, AntType.Soldier)), "Soldiers could order the founding dig");
            Check(s.OrderEntrance(0, Units(s, AntType.Worker)), "Selected workers could not start the entrance");
            Check(!s.OrderEntrance(0, Units(s, AntType.Worker)), "Duplicate entrance order accepted");
            Until(s, () => s.Mission.Stage == ExpeditionStage.Incubator, 180, "workers dig and queen enters the royal chamber");
            Check(s.ChamberCount(0, ChamberKind.Brood, false) == 0 && s.ChamberCount(0, ChamberKind.Storage, false) == 0, "Founding gifted rooms instead of teaching construction");
            Check(s.CanEnqueue(0, ProdKind.Worker) == QueueResult.Locked, "Production allowed without an incubator");
            Check(s.CanDig(0, ChamberKind.Storage) != Sim.DigResult.Ok, "Storage allowed before incubator");
            int capacity = c.PopCap;
            Check(s.Dig(0, ChamberKind.Brood, s.DefaultSpot(0, ChamberKind.Brood)) == Sim.DigResult.Ok, "Cannot place incubator");
            Until(s, () => s.Mission.Stage == ExpeditionStage.Store, 180, "incubator completes");
            Check(c.PopCap == capacity + Config.ChamberPop, "Incubator did not increase population capacity");
            Check(s.Dig(0, ChamberKind.Storage, s.DefaultSpot(0, ChamberKind.Storage)) == Sim.DigResult.Ok, "Cannot place storage");
            Until(s, () => s.Mission.Stage == ExpeditionStage.RaiseWorkers, 180, "storage completes");
            for (int i = 0; i < Expedition.WorkerGoal; i++)
            {
                Until(s, () => s.CanEnqueue(0, ProdKind.Worker) == QueueResult.Ok, 120, "economy can pay for a worker");
                Check(s.Enqueue(0, ProdKind.Worker) == QueueResult.Ok, "Worker queue rejected");
            }
            Until(s, () => s.Mission.Stage == ExpeditionStage.RaiseSoldiers, 180, "four workers hatch inside the incubator");
            Until(s, () => s.CanEnqueue(0, ProdKind.Soldier) == QueueResult.Ok, 240, "foragers fund a fighting squad");
            Check(c.Gathered > 0, "No actual food was gathered");
            Check(s.Enqueue(0, ProdKind.Soldier) == QueueResult.Ok, "Soldier queue rejected");
            Until(s, () => s.Winner == 0, 180, "six soldiers hatch and complete episode one");
            Check(s.Mission.Stage == ExpeditionStage.Complete, "Wrong victory condition");
            capacity = c.PopCap;
            var expansionSpot = s.DefaultSpot(0, ChamberKind.Brood);
            var expansionResult = s.Dig(0, ChamberKind.Brood, expansionSpot);
            Check(expansionResult == Sim.DigResult.Ok, $"Cannot expand with a second incubator: {expansionResult}, spot {expansionSpot}, food {c.Food:0}");
            Until(s, () => s.ChamberCount(0, ChamberKind.Brood, true) == 2, 180, "second incubator completes");
            Check(c.PopCap == capacity + Config.ChamberPop, "Second incubator failed to add capacity");
            // A separate unrestricted simulation verifies the full roster without touching episode gates.
            var other = new Node3D(); app.AddChild(other);
            var sk = new Sim(WorldBuilder.Build(other, 20261031)); sk.BeginSkirmish();
            foreach (var kind in new[] { ProdKind.Worker, ProdKind.Soldier, ProdKind.Major, ProdKind.Flyer, ProdKind.Gunner, ProdKind.Warlord, ProdKind.Matron, ProdKind.Marshal })
                Check(sk.CanEnqueue(0, kind) == QueueResult.Ok, $"Skirmish caste locked: {kind}");
            Check(sk.Col[0].Chambers.Count == 7 && sk.Col[1].Chambers.Count == 7, "Skirmish lacks the seven chamber identities");
            int skirmishCapacity = sk.Col[0].PopCap;
            sk.Col[0].PopCap = Config.MaxPopCap;
            Check(sk.CanDig(0, ChamberKind.Brood) == Sim.DigResult.TooMany, "Colony can spend food on incubators beyond the population ceiling");
            sk.Col[0].PopCap = skirmishCapacity;
            Check(sk.Enqueue(0, ProdKind.Worker) == QueueResult.Ok, "Skirmish worker queue failed");
            int queen = sk.Col[0].QueenId; var queenPosition = sk.Ants[queen].Pos;
            sk.Ants[queen].Layer = 0; sk.Ants[queen].Pos = sk.W.Nests[0].Pos + new Vector2(50, 0);
            for (int i = 0; i < 30; i++) sk.Tick(1f / 30);
            Check(sk.Col[0].Queue[0].Progress == 0, "Eggs progressed while the queen was outside");
            sk.Ants[queen].Layer = (byte)Sim.NestLayer(0); sk.Ants[queen].Pos = queenPosition;
            sk.Tick(1f / 30); Check(sk.Col[0].Queue[0].Progress > 0, "Brood did not resume when queen returned");
            GD.Print($"[campaign-test] PASS episode 1 in {s.Time:0}s, gathered {c.Gathered:0}, workers {s.Mission.WorkersHatched}, soldiers {s.Mission.SoldiersHatched}; full skirmish roster unlocked");
            root.QueueFree(); other.QueueFree();
            var times = new[] { s.Time, Episode2(app), Episode3(app), Episode4(app) };
            Defeat(app);
            SaveLoad();
            GD.Print($"[campaign-test] ALL PASS; episodes won in {times[0]:0}s / {times[1]:0}s / {times[2]:0}s / {times[3]:0}s, finale loss path and saved progress checked");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[campaign-test] FAIL " + e); app.GetTree().Quit(1); }
    }

    // ---------------------------------------------------------------- episodes 2-4: scripted players
    static readonly List<Node3D> _worlds = new();

    static Sim Open(Main app, int episode, out IColonyAI rival)
    {
        var root = new Node3D(); app.AddChild(root); _worlds.Add(root);
        var s = new Sim(WorldBuilder.Build(root, 20261031));
        s.BeginEpisode(episode);
        // the same rival a campaign session runs: it grows and guards its mound, the episode sends the raids
        rival = episode >= 3 ? AIController.CampaignRival(s) : null;
        Check(s.Mission?.Episode == episode && s.Mission.Step == 0 && s.Winner < 0, $"Episode {episode} did not start on its first objective");
        return s;
    }

    static void Close()
    {
        foreach (var w in _worlds) w.QueueFree();
        _worlds.Clear();
    }

    /// Ticks the episode like a session does (rival AI, then the simulation), letting the scripted player think once a second.
    static void Play(Sim s, IColonyAI rival, Func<bool> done, int seconds, string reason, Action think, bool allowLoss = false)
    {
        for (int t = 0; t < seconds * 30 && !done(); t++)
        {
            if (t % 30 == 0) think();
            if (t % 600 == 0 && OS.GetEnvironment("MANDIBLE_CAMPAIGN_TRACE") != "")
                GD.Print($"[campaign-trace] ep {s.Mission.Episode} t {s.Time:0} front {Front(s)} step {s.Mission.Step} {s.Mission.Have}/{s.Mission.Need} army {Army(s).Count} workers {s.Col[0].Count[0]} food {s.Col[0].Food:0} guards {s.Mission.GuardsLeft} raiders {s.Mission.RaidersLeft}/{s.Mission.Raiders.Count} post {s.Mission.PostLeft} rival {s.Col[1].Count[1]}+{s.Col[1].Count[2]} g{s.GuardiansLeft(1)} {GuardHp(s)} queen {s.Ants[s.Col[0].QueenId].Hp:0} rq {s.Ants[s.Col[1].QueenId].Hp:0} below {Below(s)}");
            rival?.Tick(1f / 30);
            s.Tick(1f / 30);
            if (!allowLoss) Check(s.Winner != 1, $"Episode {s.Mission.Episode} lost during {reason}: {s.Mission.LossReason}; step {s.Mission.Step}; t {s.Time:0}s");
        }
        var m = s.Mission;
        Check(done(), $"Timed out: {reason}; episode {m.Episode} step {m.Step} ({m.Title}) {m.Have}/{m.Need}; t {s.Time:0}s; food {s.Col[0].Food:0}; army {Army(s).Count}");
        GD.Print($"[campaign-test] PASS episode {m.Episode}: {reason} ({s.Time:0}s)");
    }

    static int Below(Sim s) { int n = 0; for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && s.Ants[i].Team == 0 && s.Ants[i].Layer == 2) n++; return n; }

    static Vector2 Front(Sim s)
    {
        var army = Army(s); var p = Vector2.Zero;
        foreach (int id in army) p += s.Ants[id].Pos / army.Count;
        return p.Round();
    }

    static string GuardHp(Sim s)
    {
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && s.Ants[i].Guardian) sb.Append($"{s.Ants[i].Type}:{s.Ants[i].Hp:0}/{s.Ants[i].MaxHp:0} ");
        return sb.ToString();
    }

    static List<int> Army(Sim s)
    {
        var ids = new List<int>();
        for (int i = 0; i < s.High; i++)
        {
            ref var a = ref s.Ants[i];
            if (a.Alive && a.Team == 0 && (Config.IsWarrior(a.Type) || a.Type == AntType.StagBeetle) && !a.Guardian) ids.Add(i);
        }
        return ids;
    }

    static void Hatch(Sim s, ProdKind k, int upTo)
    {
        var c = s.Col[0];
        int have = c.Count[(int)Config.UnitOf(k)];
        foreach (var q in c.Queue) if (q.Kind == k) have += Config.SquadSize(Config.UnitOf(k));
        if (have < upTo && s.CanEnqueue(0, k) == QueueResult.Ok) s.Enqueue(0, k);
    }

    /// Raiders inside the scripted player's reach: the army turns on them wherever they are.
    static bool Repel(Sim s, List<int> army, float radius)
    {
        var home = s.W.Nests[0].Pos;
        foreach (var (id, serial) in s.Mission.Raiders)
            if (s.Valid(id, serial) && s.Ants[id].Pos.DistanceTo(home) < radius)
            {
                s.CmdMove(army, s.Ants[id].Pos, true, s.Ants[id].Layer);
                return true;
            }
        return false;
    }

    static float Episode2(Main app)
    {
        var s = Open(app, 2, out var rival); var m = s.Mission; var c = s.Col[0];
        var home = s.W.Nests[0].Pos;
        Check(s.CanEnqueue(0, ProdKind.Major) == QueueResult.Locked && s.CanResearch(0, Upgrade.Queen) == ResearchResult.Locked, "Episode 2 gates failed");
        Check(!s.Col[1].Alive && s.ChamberCount(0, ChamberKind.Storage, false) == 0 && s.HerdHere && m.GuardsLeft == 1, "Episode 2 opening state wrong");
        int phase = 0;
        Play(s, rival, () => s.Winner == 0, 1200, "store, herd, harvest and the lair raid", () =>
        {
            if (s.ChamberCount(0, ChamberKind.Storage, false) == 0 && s.CanDig(0, ChamberKind.Storage) == Sim.DigResult.Ok) s.Dig(0, ChamberKind.Storage, s.DefaultSpot(0, ChamberKind.Storage));
            Hatch(s, ProdKind.Worker, 26);
            Hatch(s, ProdKind.Soldier, m.Step >= 2 ? 30 : 12);
            var army = Army(s);
            if (Repel(s, army, 70)) return;
            if (phase == 0 && m.GuardsLeft > 0) { if (army.Count >= 6) s.CmdMove(army, s.Pasture, true); }
            else if (phase == 0) { s.CmdMove(army, s.Pasture + new Vector2(1, 0), false); phase = 2; }
            else if (phase == 2 && m.Step == 3 && m.RaidsSent == 0 && m.Have < 15) { s.CmdMove(army, home.Lerp(m.Lair, 0.12f), false); phase = 3; }
        });
        Check(m.Complete && s.HerdTeam == 0 && c.Gathered - m.GatherBase >= Expedition.GatherGoal && m.RaidsSent == 1 && m.RaidersLeft == 0, "Episode 2 won without its objectives");
        float t = s.Time; Close(); return t;
    }

    static float Episode3(Main app)
    {
        var s = Open(app, 3, out var rival); var m = s.Mission; var c = s.Col[0];
        var home = s.W.Nests[0].Pos; var foe = s.W.Nests[1].Pos;
        Check(s.CanEnqueue(0, ProdKind.Gunner) == QueueResult.Locked && s.CanResearch(0, Upgrade.Wings) == ResearchResult.Locked && s.CanResearch(0, Upgrade.Queen) != ResearchResult.Locked, "Episode 3 gates failed");
        Check(m.PostLeft == 3 && s.Col[1].Alive, "Episode 3 rival or outpost missing");
        int scout = -1, scoutSerial = 0;
        Play(s, rival, () => s.Winner == 0, 1500, "scouting, two raids, research and the outpost", () =>
        {
            Hatch(s, ProdKind.Worker, 32);
            if (!c.Researching && c.T(Upgrade.Queen) < 2 && c.Food >= 220) s.StartResearch(0, Upgrade.Queen);
            Hatch(s, ProdKind.Major, 6);
            Hatch(s, ProdKind.Soldier, 48);
            var army = Army(s);
            // a squad walks (without stopping to fight) until the red thatch is in sight
            // around the great raspberry's lair in the middle, by a waypoint on the flank
            var flank = (home + foe) * 0.5f + new Vector2(-1, -1).Normalized() * 62;
            if (m.Step == 0 && !s.Valid(scout, scoutSerial) && army.Count > 0 && s.SquadOf(army[0]) is { } squad)
            {
                scout = squad.Members[0]; scoutSerial = s.Ants[scout].Serial;
                s.CmdMove(new List<int>(squad.Members), flank, false);
            }
            else if (m.Step == 0 && s.Valid(scout, scoutSerial) && s.Ants[scout].Pos.DistanceTo(flank) < 6 && s.SquadOf(scout) is { } going)
                s.CmdMove(new List<int>(going.Members), foe + (flank - foe).Normalized() * 30, false);
            if (m.Step == 0 && s.Valid(scout, scoutSerial)) army.RemoveAll(id => s.SquadOf(id)?.Members.Contains(scout) == true);
            if (Repel(s, army, 80)) return;
            if (m.Step == 3 && army.Count >= 30) s.CmdMove(army, m.PostPos, true);
            else s.CmdMove(army, home + (foe - home).Normalized() * 10, false);
        });
        Check(m.Complete && m.PostLeft == 0 && m.RaidsSent == 2 && c.T(Upgrade.Queen) >= 2, "Episode 3 won without its objectives");
        float t = s.Time; Close(); return t;
    }

    static float Episode4(Main app)
    {
        var s = Open(app, 4, out var rival); var m = s.Mission;
        var home = s.W.Nests[0].Pos; var foe = s.W.Nests[1].Pos;
        Check(s.StagAwake && s.Ants[s.StagId].Boss && s.QueenShielded(1) && !s.QueenShielded(0) && s.GuardiansLeft(1) == 4, "Episode 4 stag, guardians or siege rule missing");
        Check(s.CanEnqueue(0, ProdKind.Warlord) == QueueResult.Ok, "Episode 4 cannot raise a hero");
        bool marching = false;
        int lastStep = 0;
        Play(s, rival, () => s.Winner == 0, 1700, "hero, Old Stag, guardians and the red queen", () =>
        {
            Hatch(s, ProdKind.Warlord, 1);
            Hatch(s, ProdKind.Worker, 40);
            Hatch(s, ProdKind.Major, 12);
            Hatch(s, ProdKind.Soldier, 60);
            var army = Army(s);
            if (!(marching && m.Step == 3) && Repel(s, army, 110)) return; // the final push does not turn back
            // gather a strike force, then commit until it is spent; regroup after each objective
            if (m.Step != lastStep && m.Step == 2) marching = false;
            lastStep = m.Step;
            // once the queen is exposed every new squad streams after the assault
            marching = marching ? army.Count >= (m.Step == 3 ? 1 : 8) : army.Count >= (m.Step <= 1 ? 30 : m.Step == 2 ? 40 : 42) && m.Step > 0;
            if (!marching) { s.CmdMove(army, home + (foe - home).Normalized() * 10, false); return; }
            if (m.Step <= 1 && s.Valid(s.StagId, s.StagSerial) && s.StagTeam < 0) s.CmdAttack(army, s.StagId);
            else s.CmdMove(army, foe, true);
        });
        Check(m.Complete && !s.Col[1].Alive && s.StagTeam == 0 && s.GuardiansLeft(1) == 0 && s.Time < m.FlightAt, "Episode 4 won without its objectives");
        float t = s.Time; Close(); return t;
    }

    /// The finale's loss path: a colony that never attacks loses to the red raids or the nuptial flight.
    static void Defeat(Main app)
    {
        var s = Open(app, 4, out var rival);
        Play(s, rival, () => s.Winner == 1, 1900, "an idle colony loses the finale", () => { }, allowLoss: true);
        Check(s.Col[1].Alive && !s.Mission.Complete && s.Mission.LossReason.Length > 0, "Defeat recorded no reason");
        GD.Print($"[campaign-test] PASS loss path: {s.Mission.LossReason} ({(s.Col[0].Alive ? "flight" : "queen killed")})");
        Close();
    }

    /// Progress survives a reload from disk; unlocks and Continue follow it. Uses a scratch file, never the player's.
    static void SaveLoad()
    {
        CampaignProgress.UsePath("user://campaign_test.cfg");
        CampaignProgress.Reset();
        Check(CampaignProgress.Next == 1 && CampaignProgress.Unlocked(1) && !CampaignProgress.Unlocked(2), "Fresh campaign not at Episode 1");
        CampaignProgress.Complete(1);
        CampaignProgress.Load();
        Check(CampaignProgress.Completed(1) && CampaignProgress.Unlocked(2) && !CampaignProgress.Unlocked(3) && CampaignProgress.Next == 2, "Episode 1 completion was not saved and reloaded");
        CampaignProgress.Complete(3);
        CampaignProgress.Load();
        Check(CampaignProgress.Next == 2, "Continue skipped an unfinished episode");
        CampaignProgress.Complete(2); CampaignProgress.Complete(4);
        CampaignProgress.Load();
        Check(CampaignProgress.Next == 0 && CampaignProgress.CompletedCount == 4, "Completed campaign not recorded");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://campaign_test.cfg"));
        CampaignProgress.UsePath("user://campaign.cfg");
        GD.Print("[campaign-test] PASS saved progress: save, reload, unlock order and Continue");
    }

    public static async Task Capture(Main app, string directory)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            async Task Wait() { await app.ToSignal(app.GetTree().CreateTimer(2), SceneTreeTimer.SignalName.Timeout); await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
            async Task Shot(string name) { await Wait(); using var im = app.GetViewport().GetTexture().GetImage(); Check(im.SavePng(directory.PathJoin(name + ".png")) == Error.Ok, "Screenshot failed"); GD.Print("[command-capture] " + name); }
            app.StartSkirmish(); var g = app.Session;
            g.Sim.Tick(1f / 30); g.SetPaused(true); g.SetNestView(true);
            var home = g.World.Nests[0].Pos;
            g.Camera.SetYaw(0, true); g.Camera.NestView = true; g.Camera.JumpTo(home + new Vector2(0, 5), 39);
            g.Input.SelectedRoom = g.Sim.ChamberOf(0, ChamberKind.Brood).Id;
            g.Sim.Enqueue(0, ProdKind.Soldier); g.Sim.Enqueue(0, ProdKind.Worker);
            await Shot("01-skirmish-command");
            // Input events exercise the real GUI rather than invoking button handlers directly.
            async Task Click(float x, float y, MouseButton button = MouseButton.Left)
            {
                var size = app.GetTree().Root.Size;
                var p = new Vector2(x * size.X / 1672, y * size.Y / 941);
                Input.ParseInputEvent(new InputEventMouseMotion { Position = p, GlobalPosition = p });
                Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = button, Pressed = true });
                await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame);
                Input.ParseInputEvent(new InputEventMouseButton { Position = p, GlobalPosition = p, ButtonIndex = button, Pressed = false });
                await app.ToSignal(app.GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            await Click(980, 701); await Shot("02-research-tab");
            await Click(1090, 701); await Shot("03-heroes-tab");
            await Click(850, 701);
            int queued = g.Sim.Col[0].Queue.Count;
            await Click(742, 775);
            Check(g.Sim.Col[0].Queue.Count == queued + 1, "Worker portrait did not enqueue through real mouse input");
            await Click(1598, 885);
            Check(g.Input.Selection.Count == g.Sim.Col[0].Count[(int)AntType.Worker], "Workers command did not select the workforce");
            // Placement and its preview use the same underground plane as chamber picking.
            var site = home + new Vector2(20, 2);
            Check(g.Sim.PlaceCheck(0, site) == Sim.DigResult.Ok, "Invalid placement fixture");
            var screen = g.Camera.Cam.UnprojectPosition(new Vector3(site.X, g.World.HF.Sample(home.X, home.Y) - NestGeometry.Depth, site.Y));
            var viewport = g.GetViewport().GetVisibleRect().Size;
            int rooms = g.Sim.Col[0].Chambers.Count;
            await Click(160, 125);
            await Click(screen.X * 1672 / viewport.X, screen.Y * 941 / viewport.Y);
            Check(g.Sim.Col[0].Chambers.Count == rooms + 1, "Room did not place through real mouse input");
            Check(g.Sim.ChamberPos(0, g.Sim.Col[0].Chambers[^1]).DistanceTo(site) < 0.1f, "Room placement drifted from the visible underground cursor");
            g.SetNestView(false); await Shot("04-surface-command");
            app.StartTutorial(); g = app.Session; g.Sim.Tick(1f / 30); g.SetPaused(true);
            Check(g.Hud.BriefingOpen, "Episode 1 opened without its briefing");
            g.Hud.CloseBriefing(); g.SetPaused(true);
            await Shot("05-episode-opening");
            // the campaign list, shown with a scratch progress file: episodes 1-2 done, 3 next, 4 locked
            CampaignProgress.UsePath("user://campaign_capture.cfg"); CampaignProgress.Reset();
            CampaignProgress.Complete(1); CampaignProgress.Complete(2);
            app.ShowMenu(); app.Menu.OpenCampaign();
            await Shot("06-campaign-list");
            for (int ep = 2; ep <= CampaignEpisodes.Count; ep++)
            {
                app.StartEpisode(ep); g = app.Session;
                Check(g.Sim.Mission?.Episode == ep && g.Hud.BriefingOpen, $"Episode {ep} did not open with its briefing");
                await Shot($"{ep + 5:00}-episode{ep}-briefing");
                g.Hud.CloseBriefing();
                for (int i = 0; i < 60; i++) g.Sim.Tick(1f / 30); // ants spread out a little
                g.SetPaused(true);
                var at = ep == 3 ? g.Sim.Mission.PostPos : ep == 4 ? g.Sim.StagLair : g.Sim.Pasture;
                g.Camera.JumpTo(at, 52);
                await Shot($"{ep + 8:00}-episode{ep}-objective");
            }
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://campaign_capture.cfg"));
            CampaignProgress.UsePath("user://campaign.cfg");
            app.StartBattle(Difficulty.Normal, 0); g = app.Session; g.SetPaused(true);
            g.Sim.DevelopNest(0); g.Sim.Tick(1f / 30); g.Sim.Col[0].Food = g.Sim.Col[0].FoodCap;
            await Wait(); await Click(980, 701);
            float researchFood = g.Sim.Col[0].Food;
            await Click(742, 775);
            Check(g.Sim.Col[0].Researching, "Research portrait did not start an upgrade");
            await Wait(); await Click(742, 775, MouseButton.Right);
            Check(!g.Sim.Col[0].Researching && g.Sim.Col[0].Food == researchFood, "Research cancellation did not refund food");
            GD.Print("[command-capture] PASS live tabs, portrait queue, worker selection, underground placement, surface, episode opening, campaign list, episode 2-4 briefings and objectives, research start/cancel/refund");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[command-capture] FAIL " + e); app.GetTree().Quit(1); }
    }
}
