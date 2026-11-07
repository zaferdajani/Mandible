using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Mandible;

/// `--campaign-test`, levels 5-12: a scripted player clears each new level through the same objectives and
/// commands a person uses, and the stars, unlocks and saved progress are checked against what happened.
public static class LevelValidation
{
    static void Check(bool value, string message) { if (!value) throw new InvalidOperationException(message); }
    static readonly List<Node3D> _worlds = new();

    /// The colony the next Play/Open carries in (null: the level's own muster). Cleared after use.
    public static ColonyRoster Carry;
    public static ColonyRoster LastHarvest;
    public static Sim LastSim;

    public static Sim Open(Main app, int id, out IColonyAI rival, Perk perks = Perk.None)
    {
        var root = new Node3D(); app.AddChild(root); _worlds.Add(root);
        var s = new Sim(WorldBuilder.Build(root, Levels.Info(id).Seed));
        s.BeginEpisode(id);
        s.ApplyRoster(Carry, Levels.NumberOf(id));
        Carry = null;
        s.ApplyPerks(perks);
        rival = AIController.ForLevel(s);
        Check(s.Mission?.Episode == id && s.Mission.Step == 0 && s.Winner < 0, $"Level {Levels.NumberOf(id)} did not start on its first objective");
        return s;
    }

    public static void Close()
    {
        foreach (var w in _worlds) w.QueueFree();
        _worlds.Clear();
    }

    static string Heroes(Sim s) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && Config.IsHero(s.Ants[i].Type)) sb.Append($"{s.Ants[i].Team}{s.Ants[i].Type}{s.Ants[i].Level} "); for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && s.Ants[i].Team == 0 && s.Ants[i].Stage > 0) { sb.Append("evolved "); break; } return sb.ToString(); }

    static int Eating(Sim s, int team) { int n = 0; for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && s.Ants[i].Team == team && (s.Ants[i].Feasting || s.Ants[i].FoodTarget != null && Config.IsWarrior(s.Ants[i].Type))) n++; return n; }

    static string WildNear(Sim s, Vector2 home)
    {
        var parts = new List<string>();
        for (int i = 0; i < s.High; i++)
            if (s.Ants[i].Alive && s.Ants[i].Team != 0 && s.Ants[i].Pos.DistanceTo(home) < 60) parts.Add($"{(s.Ants[i].Team == 1 ? "red" : "")}{s.Ants[i].Type}{s.Ants[i].Level}@{s.Ants[i].Pos.DistanceTo(home):0}");
        return " wild[" + string.Join(" ", parts) + "]";
    }

    /// A named veteran the scripted player keeps out of the fighting, as a careful player would (null: none).
    public static string Spare;

    static List<int> Army(Sim s)
    {
        var ids = new List<int>();
        int spared = Spare != null ? Find(s, Spare) : -1;
        for (int i = 0; i < s.High; i++)
        {
            ref var a = ref s.Ants[i];
            if (a.Alive && a.Team == 0 && (Config.IsWarrior(a.Type) || a.Type == AntType.StagBeetle) && !a.Guardian && i != s.Mission.EscortId && i != spared) ids.Add(i);
        }
        return ids;
    }

    static void Hatch(Sim s, ProdKind k, int upTo)
    {
        var c = s.Col[0];
        int have = c.Count[(int)Config.UnitOf(k)];
        foreach (var q in c.Queue) if (q.Kind == k) have += Config.IsHeroKind(k) ? 1 : Config.SquadSize(Config.UnitOf(k));
        if (have < upTo && s.CanEnqueue(0, k) == QueueResult.Ok) s.Enqueue(0, k);
    }

    /// Anything hostile near the mound (raiders, red raids, bugs that wandered close) draws the whole army.
    static bool Repel(Sim s, List<int> army, float radius)
    {
        var home = s.W.Nests[0].Pos;
        int best = -1; float bd = radius;
        for (int i = 0; i < s.High; i++)
        {
            ref var a = ref s.Ants[i];
            if (!a.Alive || a.Team == 0 || a.Guardian || a.Type == AntType.Worker) continue;
            if (a.Team == Config.Wild && !a.Raider) continue;
            float d = a.Pos.DistanceTo(home);
            if (d < bd) { bd = d; best = i; }
        }
        if (best < 0) return false;
        s.CmdMove(army, s.Ants[best].Pos, true, s.Ants[best].Layer);
        return true;
    }

    /// How the scripted player grows its colony in each level: workers, then the warriors the level allows.
    static void Grow(Sim s, int id)
    {
        var o = s.Mission.Objectives[Math.Min(s.Mission.Step, s.Mission.Objectives.Count - 1)];
        var c = s.Col[0];
        // the current objective comes first: its research or its caste before anything else is queued
        if (o.Kind == Expedition.Obj.Research)
        {
            if (!c.Researching && s.CanResearch(0, (Upgrade)o.A) == ResearchResult.Ok) s.StartResearch(0, (Upgrade)o.A);
            if (!c.Researching) return;
        }
        if (o.Kind == Expedition.Obj.Raise && !c.Queue.Exists(q => q.Kind == ProdKind.Soldier) && s.CanEnqueue(0, ProdKind.Soldier) == QueueResult.Ok) s.Enqueue(0, ProdKind.Soldier);
        if (o.Kind == Expedition.Obj.Hatch)
        {
            var k = (AntType)o.A switch { AntType.Major => ProdKind.Major, AntType.Flyer => ProdKind.Flyer, _ => ProdKind.Soldier };
            Hatch(s, k, o.B + 6);
            if (c.Count[o.A] < o.B && c.Food < Config.ProdCost(k) * 2) return;
        }
        int workers = id switch { 5 => 34, 8 => 22, 11 => 20, 10 => 34, _ => 28 };
        Hatch(s, ProdKind.Worker, workers);
        if (s.Mission.Objectives[Math.Min(s.Mission.Step, s.Mission.Objectives.Count - 1)].Kind == Expedition.Obj.Hero || id is 10 or 12) Hatch(s, ProdKind.Warlord, 1);
        Hatch(s, ProdKind.Major, id is 6 ? 12 : 10);
        Hatch(s, ProdKind.Gunner, 10);
        Hatch(s, ProdKind.Flyer, 12);
        Hatch(s, ProdKind.Soldier, 60);
        if (!c.Researching)
            foreach (var u in new[] { Upgrade.Queen, Upgrade.Mandibles, Upgrade.Chitin, Upgrade.Wings })
                if (s.CanResearch(0, u) == ResearchResult.Ok && (u != Upgrade.Queen || c.T(u) < 2)) { s.StartResearch(0, u); break; }
        if (s.ChamberCount(0, ChamberKind.Storage, false) < 2 && s.CanDig(0, ChamberKind.Storage) == Sim.DigResult.Ok) s.Dig(0, ChamberKind.Storage, s.DefaultSpot(0, ChamberKind.Storage));
        else if (c.PopUsed > c.PopCap - 12 && s.ChamberCount(0, ChamberKind.Brood, false) < 4 && s.CanDig(0, ChamberKind.Brood) == Sim.DigResult.Ok) s.Dig(0, ChamberKind.Brood, s.DefaultSpot(0, ChamberKind.Brood));
    }

    /// Plays one level to its win; returns the simulated time.
    public static float Play(Main app, int id, out int stars, Perk perks = Perk.None)
    {
        var s = Open(app, id, out var rival, perks);
        var m = s.Mission;
        var home = s.W.Nests[0].Pos;
        bool marching = false;
        int lastStep = -1, strike = 0;
        int gatherAt = id switch { 5 => 6, 6 => 99, 7 => 10, 8 => 20, 9 => 28, 10 => 30, 11 => 1, 13 => 1, 14 => 18, _ => 40 };
        int queenAt = id switch { 7 => 24, 8 => 34, 9 => 34, 10 => 34, 11 => 1, 14 => 26, _ => 34 };
        float limit = 2400;
        for (int t = 0; t < limit * 30 && s.Winner < 0; t++)
        {
            if (t % 30 == 0)
            {
                Grow(s, id);
                var army = Army(s);
                var o = m.Objectives[Math.Min(m.Step, m.Objectives.Count - 1)];
                // the Silk Trap: the winged ants stay home until they have their stings, as the briefing advises
                if (id == Reveal.SilkTrapId && m.Step < 4) army.RemoveAll(a => s.Ants[a].Type == AntType.Flyer);
                if (m.Step != lastStep) { marching = false; lastStep = m.Step; }
                bool final = o.Kind == Expedition.Obj.KillQueen;
                if (!(marching && final) && Repel(s, army, o.Kind == Expedition.Obj.Waves ? 60 : 45)) goto tick;
                switch (o.Kind)
                {
                    case Expedition.Obj.Taste when o.A == (int)FoodKind.Sugar || o.A == (int)FoodKind.Berry:
                        // a handful of workers are sent to the nearest drop, as a player would
                        var sugar = Nearest(s, (FoodKind)o.A, home);
                        if (sugar != null && t % 300 == 0) s.CmdGather(Workers(s, 6), sugar);
                        s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false);
                        break;
                    case Expedition.Obj.Reach:
                    case Expedition.Obj.Escort:
                        // the escort: walk beside her, a little ahead toward home
                        var to = m.MarkerPos;
                        if (o.Kind == Expedition.Obj.Escort) to = m.MarkerPos + (home - m.MarkerPos).Normalized() * 6;
                        if (army.Count >= gatherAt || o.Kind == Expedition.Obj.Escort) s.CmdMove(army, to, true);
                        break;
                    case Expedition.Obj.Waves:
                        s.CmdMove(army, home + (m.WaveFrom - home).Normalized() * 8, false);
                        break;
                    case Expedition.Obj.KillQueen:
                        // gather a strike force, commit it, and regroup if it is spent before she falls
                        if (!marching && army.Count >= queenAt) { marching = true; strike = army.Count; }
                        else if (marching && army.Count < Math.Max(3, strike / 4)) marching = false;
                        if (marching) s.CmdMove(army, s.W.Nests[1].Pos, true);
                        else s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false);
                        break;
                    case Expedition.Obj.Sense:
                    case Expedition.Obj.Extract:
                    case Expedition.Obj.Deliver:
                    {
                        // the army clears the web (or walks beside the carrier); five workers carve and carry the gland
                        var gland = m.Gland;
                        bool carried = gland.Carried;
                        s.CmdMove(army, carried ? gland.Pos : gland.Pos + (gland.Pos - home).Normalized() * 4, true);
                        if (o.Kind != Expedition.Obj.Sense && t % 300 == 0)
                        {
                            int on = 0;
                            for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && s.Ants[i].Team == 0 && s.Ants[i].MutaP1 == gland.Id + 1) on++;
                            if (on < 4) s.CmdExtract(Workers(s, 5 - on), gland);
                        }
                        break;
                    }
                    case Expedition.Obj.Mutate:
                        // the player opens the mutation card and lets the queen consume the mutagen
                        s.Mutate(0, (Mutation)o.A);
                        s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false);
                        break;
                    case Expedition.Obj.Larva:
                    {
                        // the larva's larder: the army hunts the lairs between raids; the workers haul the remains home
                        var den = m.Groups.FindIndex(g => g.Exists(b => s.Valid(b.id, b.serial)));
                        if (den >= 0 && army.Count >= gatherAt && m.WaveBugs.TrueForAll(b => !s.Valid(b.id, b.serial))) s.CmdMove(army, m.GroupPos[den], true);
                        else s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 6, false);
                        break;
                    }
                    case Expedition.Obj.Evolve:
                        // bring the army to the nest and feed it from the reserve, as the objective teaches
                        s.CmdMove(army, home, false);
                        var atNest = army.FindAll(a => s.Ants[a].Pos.DistanceTo(home) < Config.ReinforceRadius && s.EvolutionNeed(s.Ants[a]) > 0);
                        if (atNest.Count > 0) s.FeedFromReserve(0, atNest);
                        break;
                    case Expedition.Obj.Haul:
                    case Expedition.Obj.Gather when id == 8:
                        // the Dry Spell: the lairs are the only larder, so the army keeps hunting them
                        var lair = s.Mission.Groups.FindIndex(g => g.Exists(b => s.Valid(b.id, b.serial)));
                        if (lair >= 0 && army.Count >= 18) s.CmdMove(army, s.Mission.GroupPos[lair], true);
                        else s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false);
                        break;
                    case Expedition.Obj.Group:
                    case Expedition.Obj.Taste:
                        if (!marching && army.Count >= gatherAt) { marching = true; strike = army.Count; }
                        else if (marching && army.Count < Math.Max(3, strike / 3)) marching = false;
                        if (!marching) { s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false); break; }
                        if (o.B == -4 && s.Valid(s.StagId, s.StagSerial) && s.StagTeam < 0) s.CmdAttack(army, s.StagId);
                        else s.CmdMove(army, m.MarkerPos, true);
                        break;
                    default:
                        s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false);
                        break;
                }
            }
        tick:
            if ((t % 900 == 0 || OS.GetEnvironment("MANDIBLE_CAMPAIGN_TRACE") == "fine" && t % 150 == 0) && OS.GetEnvironment("MANDIBLE_CAMPAIGN_TRACE") != "")
                GD.Print($"[level-trace] L{Levels.NumberOf(id)} t {s.Time:0} step {m.Step} {m.Title} {m.Have}/{m.Need} army {Army(s).Count} workers {s.Col[0].Count[0]} food {s.Col[0].Food:0} gathered {s.Col[0].Gathered - m.GatherBase:0} lost {s.Col[0].Lost} queen {s.Ants[s.Col[0].QueenId].Hp:0} rival {s.Col[1].Count[1]}+{s.Col[1].Count[2]} rq {(s.Col[1].Alive ? s.Ants[s.Col[1].QueenId].Hp : 0):0} waves {m.WaveNext} sol {s.Col[0].Count[1]} eating {Eating(s, 0)}/{Eating(s, 1)} heroes {Heroes(s)} hatchedS {m.SoldiersHatched} q {s.Col[0].Queue.Count}" + (Spare != null ? $" {Spare} {(Find(s, Spare) is int sp and >= 0 ? $"hp {s.Ants[sp].Hp:0} at {s.Ants[sp].Pos.DistanceTo(home):0} {s.Ants[sp].State}" : "gone")}" : "") + (OS.GetEnvironment("MANDIBLE_CAMPAIGN_TRACE") == "bugs" ? WildNear(s, home) : ""));
            rival?.Tick(1f / 30);
            s.Tick(1f / 30);
            Check(s.Winner != 1, $"Level {Levels.NumberOf(id)} ({Levels.Info(id).Name}) lost: {m.LossReason}; step {m.Step} ({m.Title}) {m.Have}/{m.Need}; t {s.Time:0}s");
        }
        Check(s.Winner == 0, $"Level {Levels.NumberOf(id)} ({Levels.Info(id).Name}) timed out on step {m.Step} ({m.Title}) {m.Have}/{m.Need}; t {s.Time:0}s; army {Army(s).Count}; food {s.Col[0].Food:0}");
        for (int i = 0; i < 2; i++) s.Tick(1f / 30); // the win settles: steps and the end are recorded
        Check(m.Complete, $"Level {Levels.NumberOf(id)} won without completing");
        if (m.EndsWithQueen) Check(!s.Col[1].Alive, $"Level {Levels.NumberOf(id)} won without killing the red queen");
        stars = s.Stars();
        LastHarvest = s.HarvestRoster(Levels.NumberOf(id));
        LastSim = s;
        Check((stars & 1) == 1, "A won level earned no star");
        // the stars match what happened
        for (int k = 0; k < 2; k++)
        {
            var (goal, value, text) = m.Info.Stars[k];
            bool earned = (stars & 2 << k) != 0;
            bool expect = goal switch
            {
                StarGoal.Time => m.EndTime <= value,
                StarGoal.Lost => m.EndLost <= value,
                StarGoal.QueenHp => m.QueenLow >= value,
                _ => earned,
            };
            Check(earned == expect, $"Level {Levels.NumberOf(id)} star '{text}' was {(earned ? "" : "not ")}awarded wrongly (time {m.EndTime:0}, lost {m.EndLost}, queen low {m.QueenLow:0.00})");
        }
        GD.Print($"[campaign-test] PASS level {Levels.NumberOf(id)} {Levels.Info(id).Name}: won in {m.EndTime:0}s, lost {m.EndLost}, queen low {m.QueenLow * 100:0}%, stars {Count(stars)} ({Describe(id, stars)})");
        float time = m.EndTime;
        Close();
        return time;
    }

    static int Count(int bits) { int n = 0; for (int i = 0; i < 3; i++) if ((bits & 1 << i) != 0) n++; return n; }
    static string Describe(int id, int bits)
    {
        var parts = new List<string>();
        for (int k = 0; k < 2; k++) parts.Add(((bits & 2 << k) != 0 ? "+" : "-") + Levels.Info(id).Stars[k].text);
        return string.Join("; ", parts);
    }

    static FoodSource Nearest(Sim s, FoodKind kind, Vector2 home)
    {
        FoodSource best = null;
        foreach (var f in s.W.Food) if (f.Kind == kind && f.Alive && (best == null || f.Pos.DistanceTo(home) < best.Pos.DistanceTo(home))) best = f;
        return best;
    }

    static List<int> Workers(Sim s, int n)
    {
        var ids = new List<int>();
        for (int i = 0; i < s.High && ids.Count < n; i++) if (s.Ants[i].Alive && s.Ants[i].Team == 0 && s.Ants[i].Type == AntType.Worker && s.Ants[i].Layer == 0) ids.Add(i);
        return ids;
    }

    /// The persistent colony across chapters: Chapter 1's survivors (with old Burr) walk into Chapter 2 and win it,
    /// Burr surviving both; Chapter 3 starts with the colony and nest they carried, a named veteran's death is
    /// recorded, and a lost Chapter 3 retries from the same colony without wiping the save.
    public static void Chain(Main app, ColonyRoster afterChapter1)
    {
        Check(afterChapter1 != null && afterChapter1.Veterans.Exists(v => v.Name == "Burr" && v.Joined == 1), "Burr did not walk out of the broken tube");
        Check(afterChapter1.Chambers.Contains(ChamberKind.Brood) && afterChapter1.Chambers.Contains(ChamberKind.Storage), "Chapter 1's nest was not carried");
        const string path = "user://campaign_chain.cfg";
        CampaignProgress.UsePath(path); CampaignProgress.Reset();
        CampaignProgress.Record(1, 1, 300);
        CampaignProgress.CarryRoster(1, afterChapter1);
        int ch2 = Levels.IdAt(2), ch3 = Levels.IdAt(3);
        var entering2 = CampaignProgress.RosterFor(ch2);
        Check(entering2 != null && entering2.Total == afterChapter1.Total, "Chapter 2 did not receive Chapter 1's survivors from the save");
        // Chapter 2 with the carried colony
        Carry = entering2;
        var s = Open(app, ch2, out _);
        Check(s.Mission.Tracked.Count > 0 && Exists(s, "Burr"), "Burr was not on the field in Chapter 2");
        Check(s.ChamberCount(0, ChamberKind.Brood, true) >= afterChapter1.Chambers.FindAll(k => k == ChamberKind.Brood).Count, "Chapter 2's nest lost Chapter 1's brood chambers");
        Close();
        Carry = entering2;
        Spare = "Burr"; // the careful player keeps the old soldier home through the beetle hunt
        Play(app, ch2, out _);
        Spare = null;
        var after2 = LastHarvest;
        var burr = after2.Veterans.Find(v => v.Name == "Burr");
        Check(burr != null && burr.Joined == 1, "Burr did not survive two chapters");
        CampaignProgress.Record(ch2, 1, 400);
        CampaignProgress.CarryRoster(ch2, after2);
        // Chapter 3: the carried nest and colony, a named death, a loss and a retry
        var entering3 = CampaignProgress.RosterFor(ch3);
        Check(entering3 != null && entering3.Total == after2.Total, "Chapter 3 did not receive Chapter 2's survivors");
        Carry = entering3;
        s = Open(app, ch3, out var rival);
        Check(s.ChamberCount(0, ChamberKind.Storage, true) >= entering3.Chambers.FindAll(k => k == ChamberKind.Storage).Count, "Chapter 3's nest lost the carried stores");
        int burrId = Find(s, "Burr");
        Check(burrId >= 0, "Burr missing at the start of Chapter 3");
        s.Slay(burrId);
        Check(s.Mission.Fallen.Exists(v => v.Name == "Burr"), "A named veteran's death was not recorded");
        s.Winner = 1; // the chapter is lost
        Close();
        CampaignProgress.Load();
        var retry = CampaignProgress.RosterFor(ch3);
        Check(retry != null && retry.Save() == entering3.Save() && CampaignProgress.Completed(ch2) && !CampaignProgress.Completed(ch3), "A lost chapter changed the saved colony or progress");
        Carry = retry;
        s = Open(app, ch3, out _);
        Check(Find(s, "Burr") >= 0, "The retry did not bring Burr back with the chapter's starting colony");
        Close();
        // a careless colony still gets a fighting floor of fresh brood
        var thin = new ColonyRoster { QueenTier = 1 };
        thin.Plain[(int)AntType.Worker] = 2;
        Carry = thin;
        s = Open(app, ch3, out _);
        Check(s.Mission.Reinforcements > 0 && s.Col[0].Count[(int)AntType.Soldier] >= 6, "A thin colony got no reinforcements: a dead-end save");
        Close();
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        CampaignProgress.UsePath("user://campaign.cfg");
        GD.Print($"[campaign-test] PASS persistent colony: chapter 1 -> 2 -> 3 with {afterChapter1.Total} -> {after2.Total} ants, Burr survived two chapters ({burr.Kills} kills), named death recorded, loss retried from the same colony, thin colony reinforced");
    }

    static int Find(Sim s, string name)
    {
        foreach (var (id, (serial, vet)) in s.Mission.Tracked) if (vet.Name == name && s.Valid(id, serial)) return id;
        return -1;
    }
    static bool Exists(Sim s, string name) => Find(s, name) >= 0;

    /// Losses that are not the queen: the escort falling, the storm, the seal.
    public static void Losses(Main app)
    {
        // the Matron's Road: an army that never fetches her still loses if she dies; here the idle colony simply runs to the deadline-free end, so kill her
        var s = Open(app, 7, out var rival);
        ref var mat = ref s.Ants[s.Mission.EscortId];
        mat.Hp = 0.5f;
        for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && s.Ants[i].Team == Config.Wild && s.Ants[i].Pos.DistanceTo(mat.Pos) < 60) { s.Ants[i].Pos = mat.Pos + new Vector2(1, 0); break; }
        int spider = s.Spawn(Config.Wild, AntType.Spider, mat.Pos + new Vector2(1.2f, 0), 0, 3);
        s.Ants[spider].State = AntState.Idle;
        for (int t = 0; t < 30 * 60 && s.Winner < 0; t++) { rival?.Tick(1f / 30); s.Tick(1f / 30); }
        Check(s.Winner == 1 && s.Col[0].Alive && s.Mission.LossReason.Contains("Matron"), $"Losing the escort did not lose the level ({s.Winner}, {s.Mission.LossReason})");
        GD.Print($"[campaign-test] PASS loss path: {s.Mission.LossReason}");
        Close();
        // Sweet Rain: an idle colony is caught by the storm
        s = Open(app, 5, out rival);
        for (int t = 0; t < 30 * 960 && s.Winner < 0; t++) s.Tick(1f / 30);
        Check(s.Winner == 1 && s.Col[0].Alive && s.Mission.LossReason.Contains("storm"), $"The storm deadline did not end Sweet Rain ({s.Winner}, {s.Mission.LossReason})");
        GD.Print($"[campaign-test] PASS loss path: {s.Mission.LossReason}");
        Close();
    }

    /// Gates and perks: the castes and heroes a level opens, and what stars add on top.
    public static void Gates(Main app)
    {
        var s = Open(app, 5, out _);
        Check(s.CanEnqueue(0, ProdKind.Major) == QueueResult.Locked && s.CanResearch(0, Upgrade.Queen) == ResearchResult.Locked && !s.Col[1].Alive, "Sweet Rain gates or rival wrong");
        Close();
        s = Open(app, 9, out _);
        Check(s.QueenShielded(1) && s.CanResearch(0, Upgrade.Wings) == ResearchResult.Ok, "Wings and Acid: shield or gates wrong");
        Close();
        s = Open(app, 4, out _);
        Check(s.CanEnqueue(0, ProdKind.Matron) == QueueResult.Locked, "The Acid Matron was open without her stars");
        Close();
        s = Open(app, 4, out _, Perk.Matron | Perk.Veteran);
        Check(s.Mission.AllowsProd(ProdKind.Matron) && !s.Mission.AllowsProd(ProdKind.Marshal), "The Acid Matron perk did not open her (alone)");
        Close();
        s = Open(app, 3, out _, Perk.Gunner | Perk.Flyer | Perk.Matron);
        Check(!s.Mission.AllowsProd(ProdKind.Gunner) && !s.Mission.AllowsProd(ProdKind.Flyer) && !s.Mission.AllowsProd(ProdKind.Matron) && !s.Mission.AllowsResearch(Upgrade.Wings, 1), "Star perks opened castes before Act III");
        int soldiers = s.Col[0].Count[(int)AntType.Soldier];
        Close();
        s = Open(app, 10, out _, Perk.Gunner | Perk.Flyer);
        Check(s.Mission.AllowsResearch(Upgrade.Wings, 1) && s.Mission.AllowsProd(ProdKind.Flyer) && s.Mission.AllowsProd(ProdKind.Gunner) && s.Mission.AllowsResearch(Upgrade.Queen, 3), "Early gunner and flyer perks did not reach Act III");
        Close();
        s = Open(app, 3, out _);
        Check(soldiers == s.Col[0].Count[(int)AntType.Soldier], "Perks changed the soldiers without the veteran perk");
        Close();
        s = Open(app, 10, out _);
        Check(!s.Mission.AllowsProd(ProdKind.Gunner), "Gunners open in Act III without stars");
        Close();
        s = Open(app, 3, out _, Perk.Veteran);
        Check(s.Col[0].Count[(int)AntType.Soldier] == soldiers + 6, "The veteran brood perk did not add its squad");
        Close();
        GD.Print("[campaign-test] PASS level gates, the watch-post shield and star perks (Matron, early gunners and flyers, veteran brood)");
        Reveals(app);
    }

    /// The gradual reveal: in every chapter nothing is on offer before the chapter that introduces it, everything
    /// introduced by then is, the HUD shows exactly that, no wild bug the player has not met is on the yard, the red
    /// colony stays away through Act I and no chapter before Ashling's is won by a queen kill.
    public static void Reveals(Main app)
    {
        var introduced = new System.Collections.Generic.List<string>();
        for (int n = 2; n <= Levels.Count; n++) // Chapter 1 (the broken tube) is checked by the episode-one run
        {
            int id = Levels.IdAt(n);
            var s = Open(app, id, out _);
            var m = s.Mission;
            Check(m.Chapter == n, $"Chapter {n} reports itself as {m.Chapter}");
            foreach (ProdKind k in Enum.GetValues(typeof(ProdKind)))
            {
                bool open = Reveal.Prod(k) <= n;
                Check(m.AllowsProd(k) == open, $"Chapter {n}: {k} is {(open ? "hidden" : "on offer")} (introduced in {Reveal.Prod(k)})");
                if (!open) Check(s.CanEnqueue(0, k) == QueueResult.Locked, $"Chapter {n}: {k} could be hatched before its reveal");
            }
            foreach (ChamberKind k in Enum.GetValues(typeof(ChamberKind)))
            {
                bool open = Reveal.Chamber(k) <= n;
                Check(m.AllowsChamber(k) == open, $"Chapter {n}: the {k} chamber is {(open ? "hidden" : "on offer")}");
                if (!open) Check(s.CanDig(0, k) != Sim.DigResult.Ok, $"Chapter {n}: the {k} chamber could be dug before its reveal");
            }
            foreach (Upgrade u in Enum.GetValues(typeof(Upgrade)))
                for (int t = 1; t <= Config.MaxTier(u); t++)
                {
                    bool open = Reveal.Research(u, t) <= n;
                    Check(m.AllowsResearch(u, t) == open, $"Chapter {n}: {u} rank {t} is {(open ? "hidden" : "on offer")}");
                    if (!open && s.Col[0].T(u) == t - 1) Check(s.CanResearch(0, u) == ResearchResult.Locked, $"Chapter {n}: {u} rank {t} could be researched before its reveal");
                }
            foreach (Upgrade u in Enum.GetValues(typeof(Upgrade)))
                Check(m.ShowsResearch(u) == (Reveal.Research(u, u == Upgrade.Queen ? 2 : 1) <= n), $"Chapter {n}: the {u} research card is {(m.ShowsResearch(u) ? "shown" : "hidden")} on the HUD");
            Check(m.ShowsAnyResearch == (n >= Reveal.Research(Upgrade.Queen, 2)), $"Chapter {n}: the Research tab is {(m.ShowsAnyResearch ? "shown" : "hidden")}");
            Check(m.ShowsHeroes == (n >= Reveal.Prod(ProdKind.Warlord)), $"Chapter {n}: the Heroes tab is {(m.ShowsHeroes ? "shown" : "hidden")}");
            for (int i = 0; i < s.High; i++)
            {
                ref var a = ref s.Ants[i];
                if (!a.Alive || a.Team == 1) continue;
                Check(Reveal.Creature(a.Type) <= n, $"Chapter {n}: a {a.Type} is on the field before its reveal in Chapter {Reveal.Creature(a.Type)}");
            }
            Check(s.OrganicRules == (n >= Reveal.Organic) && s.EatRules == (n >= Reveal.Evolution) && !s.CarrionRules, $"Chapter {n}: organic matter or eating to evolve on the wrong side of their reveal");
            Check(s.Col[1].Alive == (n >= Reveal.RedColony), $"Chapter {n}: the red colony is {(s.Col[1].Alive ? "present" : "absent")}");
            Check(m.EndsWithQueen == (n >= Reveal.FirstQueenKill && Levels.Info(id).Rival != RivalKind.None), $"Chapter {n}: {(m.EndsWithQueen ? "won" : "not won")} by a queen kill");
            // the mutation track: what the story has given by this chapter, and the sixth sense from the Silk Trap on
            Check(s.Has(0, Mutation.Stinger) == (n > Reveal.Mutagen) && s.Has(0, Mutation.RoyalLarva) == (n > Reveal.RoyalLarva), $"Chapter {n}: the story's mutations are wrong ({s.Col[0].Mutations})");
            Check((s.Nursery(0) != null) == (n > Reveal.RoyalLarva), $"Chapter {n}: the royal nursery is {(s.Nursery(0) != null ? "dug" : "missing")}");
            if (n > Reveal.RoyalLarva) Check(s.CanEnqueue(0, ProdKind.Warlord) is QueueResult.Ok or QueueResult.HeroExists or QueueResult.NoFood, $"Chapter {n}: the royal larva cannot be laid for a hero");
            foreach (var (name, _) in Reveal.NewIn(n)) introduced.Add(name);
            Close();
        }
        // what a save has met: the Field Guide, the red colony's card and the star unlocks' names
        const string guide = "user://campaign_reveal.cfg";
        CampaignProgress.UsePath(guide); CampaignProgress.Reset();
        var matron = Array.Find(Levels.Unlocks, u => u.Perk == Perk.Matron);
        Check(CampaignProgress.Met(AntType.Worker) && CampaignProgress.Met(AntType.Beetle) && !CampaignProgress.Met(AntType.Spider) && !CampaignProgress.Met(AntType.Gunner) && !CampaignProgress.MetRedColony,
            "A fresh save's Field Guide lists more than the broken tube");
        Check(CampaignProgress.UnlockName(matron) != matron.Name && !CampaignProgress.MetEvolution, "A star unlock named the Acid Matron, or the Feeding page showed, before they were met");
        for (int n = 1; n <= 3; n++) CampaignProgress.Record(Levels.IdAt(n), 1, 300);
        Check(CampaignProgress.Frontier == 4 && CampaignProgress.MetRedColony && CampaignProgress.Met(AntType.Spider) && !CampaignProgress.Met(AntType.Wasp), "Act I's end did not open the red colony and spiders (alone) in the guide");
        CampaignProgress.MetEverything();
        Check(CampaignProgress.Met(AntType.StagBeetle) && CampaignProgress.UnlockName(matron) == matron.Name && CampaignProgress.MetEvolution, "A Skirmish or Lane War game did not open the whole Field Guide");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(guide));
        CampaignProgress.UsePath("user://campaign.cfg");
        GD.Print($"[campaign-test] PASS gradual reveal: every caste, hero, chamber, research rank and wild bug appears only from its chapter, the Field Guide and star unlocks name only what the save has met; red colony from Chapter {Reveal.RedColony}, first queen kill in Chapter {Reveal.FirstQueenKill}; {introduced.Count} lessons introduced after Chapter 1");
    }

    /// The mutation track: both mutation chapters played by the scripted player (the gland carved, carried home and
    /// fed to the queen, the stingers on the flyers; the royal larva fed full, the cocoon, the hero), the result card's
    /// mutation choice carried by the save into the next chapter, instability on the woods, and the losses: the
    /// larva devoured (retried from the same colony) and the gland dropped by a fallen carrier.
    public static (float silk, float larva) MutationTimes;

    public static void Mutations(Main app)
    {
        const string path = "user://campaign_mutation.cfg";
        CampaignProgress.UsePath(path); CampaignProgress.Reset();
        // ---- the Silk Trap
        Play(app, Reveal.SilkTrapId, out int stars13);
        var s = LastSim; var m = s.Mission; var c = s.Col[0];
        MutationTimes.silk = m.EndTime;
        Check(m.Gland.Home && c.MutagenGathered >= 1, "The gland was not carried home");
        Check(s.Has(0, Mutation.Stinger) && c.Instability == 1 && c.Mutagen == 0, $"The queen did not take the venom stingers (mutations {c.Mutations}, instability {c.Instability}, mutagen {c.Mutagen})");
        Check(m.Stings > 0, "No flyer ever stung after the mutation");
        Check(LastHarvest.Mutations == (int)Mutation.Stinger && LastHarvest.Instability == 1, "The stingers did not walk out of the Silk Trap");
        GD.Print($"[campaign-test] PASS Silk Trap: gland carved and hauled home, stingers taken (stings {m.Stings}), web spider {(m.MastersSlain > 0 ? "slain" : "left alive")}, colony level {LastHarvest.Level}");
        // venom: a stinging flyer's strike keeps burning
        {
            var t = Open(app, Reveal.SilkTrapId, out _);
            t.Mutate(0, Mutation.Stinger); // refused: no mutagen in the reserve yet
            Check(!t.Has(0, Mutation.Stinger), "A mutation was taken without mutagen");
            t.Col[0].Mutagen = 1; Check(t.Mutate(0, Mutation.Stinger) && !t.Mutate(0, Mutation.FireBlood), "The chapter's mutation was refused, or another one offered");
            var at = t.W.Nests[0].Pos + new Vector2(30, 30);
            int fly = t.Spawn(0, AntType.Flyer, at, 0), bug = t.Spawn(Config.Wild, AntType.Beetle, at + new Vector2(0.8f, 0), 0, 3);
            t.Ants[fly].Alt = t.Ants[bug].Alt;
            for (int k = 0; k < 90 && t.Ants[bug].PoisonT <= 0; k++) { t.CmdAttack(new List<int> { fly }, bug); t.Tick(1f / 30); }
            Check(t.Ants[bug].PoisonT > 0, "A stinging flyer's strike did not poison");
            Close();
        }
        // a carrier that falls drops the gland where it stands; another worker takes it up
        {
            var t = Open(app, Reveal.SilkTrapId, out _);
            var g = t.Mission.Gland; g.Done = g.Work;
            int w = t.Spawn(0, AntType.Worker, g.Pos + new Vector2(0.5f, 0), 0);
            t.CmdExtract(new List<int> { w }, g);
            for (int k = 0; k < 30 && !g.Carried; k++) t.Tick(1f / 30);
            Check(g.Carried, "A worker at a free gland did not take it up");
            for (int k = 0; k < 60; k++) t.Tick(1f / 30);
            var dropAt = g.Pos;
            t.Slay(w);
            t.Tick(1f / 30);
            Check(!g.Carried && !g.Home && g.Pos.DistanceTo(dropAt) < 1.5f, "A fallen carrier did not drop the gland where it fell");
            GD.Print("[campaign-test] PASS loss path: the gland falls with its carrier and waits to be taken up again");
            Close();
        }
        // ---- Ashling's body: the chapter before the Royal Larva gives its mutagen
        CampaignProgress.Record(Levels.IdAt(Reveal.FirstQueenKill), 1, 900);
        var afterAshling = new ColonyRoster { QueenTier = 2, Mutations = LastHarvest.Mutations, Instability = LastHarvest.Instability, Level = LastHarvest.Level, Mutagen = 1 };
        afterAshling.Plain[(int)AntType.Worker] = 14; afterAshling.Plain[(int)AntType.Soldier] = 18; afterAshling.Plain[(int)AntType.Flyer] = 6;
        CampaignProgress.CarryRoster(Levels.IdAt(Reveal.FirstQueenKill), afterAshling);
        // ---- the Royal Larva: the loss first (the larva devoured), then the retry from the same colony, won
        var entering = CampaignProgress.RosterFor(Reveal.RoyalLarvaId);
        Check(entering != null && entering.Mutagen == 1 && entering.Mutations == (int)Mutation.Stinger, "The Royal Larva did not receive Ashling's mutagen and the stingers");
        {
            Carry = entering;
            var t = Open(app, Reveal.RoyalLarvaId, out var rival);
            Check(t.Mutate(0, Mutation.RoyalLarva), "The queen did not take the royal larva");
            var room = t.ChamberOf(0, ChamberKind.Nursery);
            Check(room != null && !room.Built && t.Col[0].LarvaStage == 1, "The royal larva did not wait for a nursery");
            room.Done = room.Work; room.Built = true;
            for (int k = 0; k < 30; k++) t.Tick(1f / 30);
            Check(t.Col[0].LarvaStage == 2, "The larva did not move into the dug nursery");
            var nest = t.ChamberPos(0, room);
            for (int k = 0; k < 6; k++)
            {
                int sp = t.Spawn(Config.Wild, AntType.Spider, nest + MathX.Dir(k) * 0.8f, 0, 2);
                t.Ants[sp].Layer = (byte)Sim.NestLayer(0); t.Ants[sp].Raider = true; t.Ants[sp].State = AntState.Hold;
            }
            for (int k = 0; k < 30 * 120 && t.Winner < 0; k++) { rival?.Tick(1f / 30); t.Tick(1f / 30); }
            Check(t.Winner == 1 && t.Col[0].Alive && t.Mission.LossReason.Contains("larva"), $"Raiders in the nursery did not cost the chapter ({t.Winner}, {t.Mission.LossReason})");
            GD.Print($"[campaign-test] PASS loss path: {t.Mission.LossReason}");
            Close();
        }
        CampaignProgress.Load();
        var retry = CampaignProgress.RosterFor(Reveal.RoyalLarvaId);
        Check(retry != null && retry.Save() == entering.Save() && !CampaignProgress.Completed(Reveal.RoyalLarvaId), "The lost Royal Larva changed the saved colony");
        Carry = retry;
        Play(app, Reveal.RoyalLarvaId, out int stars14);
        s = LastSim; m = s.Mission; c = s.Col[0];
        MutationTimes.larva = m.EndTime;
        Check(m.HeroesBorn == 1, "No hero broke out of the cocoon");
        Check(s.Has(0, Mutation.RoyalLarva) && c.LarvaFed >= 0 && c.Instability == 2, "The royal larva mutation was not kept");
        var hemlock = LastHarvest.Veterans.Find(v => v.Type == AntType.Warlord);
        Check(hemlock == null ? c.Count[(int)AntType.Warlord] == 0 : hemlock.Name == "Hemlock", "The hero from the cocoon was not remembered as Hemlock"); // the hero may fall in the assault on the queen
        Check(LastHarvest.Mutagen == 1 && LastHarvest.Level == retry.Level + 1, $"The red queen's body gave no mutagen or level (mutagen {LastHarvest.Mutagen}, level {LastHarvest.Level})");
        Check(LastHarvest.Chambers.Contains(ChamberKind.Nursery), "The royal nursery was not carried");
        GD.Print($"[campaign-test] PASS Royal Larva: larva fed {c.LarvaNeed:0} organic, cocoon hatched, Hemlock born and {(hemlock != null ? "walks on" : "fell in the assault")}; queen's mutagen and colony level {LastHarvest.Level}");
        // ---- the result card's choice: three cards, the queen's trait first, carried into Queen Cinder
        CampaignProgress.Record(Reveal.RoyalLarvaId, stars14, m.EndTime);
        CampaignProgress.CarryRoster(Reveal.RoyalLarvaId, LastHarvest);
        var offer = global::Mandible.Mutations.Offer(Reveal.RoyalLarvaId, LastHarvest.Mutations);
        Check(offer.Count == 3 && offer[0] == global::Mandible.Mutations.TraitOf(Reveal.RoyalLarvaId) && !offer.Contains(Mutation.Stinger) && !offer.Contains(Mutation.RoyalLarva), "The mutation cards are wrong");
        Check(global::Mandible.Mutations.Offer(Reveal.RoyalLarvaId, LastHarvest.Mutations).SequenceEqual(offer), "The mutation cards change between looks");
        Check(CampaignProgress.ChooseMutation(Reveal.RoyalLarvaId, LastHarvest, offer[0]) && !CampaignProgress.ChooseMutation(Reveal.RoyalLarvaId, LastHarvest, offer[1]), "The choice spent no mutagen, or a second card was taken");
        CampaignProgress.Load();
        int cinder = Levels.IdAt(Reveal.RoyalLarva + 1);
        var intoCinder = CampaignProgress.RosterFor(cinder);
        Check(intoCinder != null && (intoCinder.Mutations & (int)offer[0]) != 0 && intoCinder.Instability == 3 && intoCinder.Mutagen == 0, "The chosen mutation did not reach the next chapter");
        Carry = intoCinder;
        var q = Open(app, cinder, out _);
        Check(q.Has(0, offer[0]) && q.Col[0].Instability == 3 && (hemlock == null || q.Col[0].Count[(int)AntType.Warlord] > 0), "Queen Cinder did not begin with the mutation, the instability and Hemlock");
        int wild = -1; for (int i = 0; i < q.High; i++) if (q.Ants[i].Alive && q.Ants[i].Team == Config.Wild) { wild = i; break; }
        if (wild >= 0) Check(q.Ants[wild].MaxHp > Config.Of(q.Ants[wild].Type).Hp * Config.HpMul(q.Ants[wild].Type, q.Ants[wild].Level) * 1.1f, "Instability did not toughen the woods");
        Check(q.Col[0].PopCap >= Config.StartPopCap + Global.Slots(intoCinder.Level), "The colony's level added no room");
        Close();
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        CampaignProgress.UsePath("user://campaign.cfg");
        GD.Print($"[campaign-test] PASS mutation track: cards {string.Join(", ", offer)} (trait first), {offer[0]} chosen and carried into Queen Cinder with instability 3; stars {Count(stars13)} and {Count(stars14)}");
    }

    static class Global { public static int Slots(int level) => global::Mandible.Mutations.SlotsPerLevel * (level - 1); }

    /// `--tube-capture=<dir>`: level 1's broken tube from the opening camera, close up and from the alley.
    public static async System.Threading.Tasks.Task TubeCapture(Main app, string directory)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            async System.Threading.Tasks.Task Shot(string name, float s)
            {
                await app.ToSignal(app.GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);
                await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var im = app.GetViewport().GetTexture().GetImage();
                im.SavePng(directory.PathJoin(name + ".png"));
                GD.Print("[tube-capture] " + name);
            }
            app.StartEpisode(1); var g = app.Session;
            g.Hud.CloseBriefing(); g.SetPaused(true);
            await Shot("opening", 3);
            g.Camera.SetYaw(0.5f, true); g.Camera.JumpTo(new Vector2(44, 255), 18); await Shot("mouth-close", 2);
            g.Camera.SetYaw(-0.9f, true); g.Camera.JumpTo(new Vector2(38, 255), 24); await Shot("tube-side", 2);
            g.Camera.SetYaw(2.6f, true); g.Camera.JumpTo(new Vector2(46, 258), 30); await Shot("from-alley", 2);
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[tube-capture] FAIL " + e); app.GetTree().Quit(1); }
    }

    /// `--map-capture=<dir>`: the colony map alone, part-way through the campaign and with its banner picker open.
    public static async System.Threading.Tasks.Task MapCapture(Main app, string directory)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            async System.Threading.Tasks.Task Shot(string name, float s)
            {
                await app.ToSignal(app.GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout);
                await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw);
                using var im = app.GetViewport().GetTexture().GetImage();
                im.SavePng(directory.PathJoin(name + ".png"));
                GD.Print("[map-capture] " + name);
            }
            CampaignProgress.UsePath("user://campaign_mapcap.cfg"); CampaignProgress.Reset();
            int[] earned = { 7, 3, 7, 5, 3, 7 };
            for (int n = 1; n <= 6; n++) CampaignProgress.Record(Levels.IdAt(n), earned[n - 1], 300 + n * 60);
            app.ShowMenu(); app.Menu.OpenCampaign();
            await Shot("map", 4);
            app.Menu.Map.Select(3); await Shot("map-selected", 1.5f);
            app.Menu.Map.ToggleLook(); await Shot("map-banner", 1.5f);
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://campaign_mapcap.cfg"));
            CampaignProgress.UsePath("user://campaign.cfg");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[map-capture] FAIL " + e); app.GetTree().Quit(1); }
    }

    /// `--level-capture=<dir>`: real frames of the level map, the banner picker, briefings, objective cards in
    /// play and the result cards. Progress comes from a scratch file so the player's own is never touched.
    public static async System.Threading.Tasks.Task Capture(Main app, string directory)
    {
        try
        {
            DirAccess.MakeDirRecursiveAbsolute(directory);
            async System.Threading.Tasks.Task Wait(float s = 2) { await app.ToSignal(app.GetTree().CreateTimer(s), SceneTreeTimer.SignalName.Timeout); await app.ToSignal(RenderingServer.Singleton, RenderingServer.SignalName.FramePostDraw); }
            async System.Threading.Tasks.Task Shot(string name, float s = 2) { await Wait(s); using var im = app.GetViewport().GetTexture().GetImage(); Check(im.SavePng(directory.PathJoin(name + ".png")) == Error.Ok, "Screenshot failed"); GD.Print("[level-capture] " + name); }
            CampaignProgress.UsePath("user://campaign_capture.cfg"); CampaignProgress.Reset();
            // a player part-way through: levels 1-6 won with a spread of stars
            int[] earned = { 7, 3, 7, 5, 3, 7 };
            for (int n = 1; n <= 6; n++) CampaignProgress.Record(Levels.IdAt(n), earned[n - 1], 300 + n * 60);
            CampaignProgress.Choose(1, 2);
            app.ShowMenu(); app.Menu.OpenCampaign();
            await Shot("L01-level-map", 3);
            app.Menu.Map.Select(4); await Shot("L02-level-map-hollow-log");
            app.Menu.Map.Select(11); await Shot("L03-level-map-locked");
            app.Menu.Map.ToggleLook(); await Shot("L04-colony-banner");
            app.Menu.Map.ToggleLook();
            // the story: the opening comic panel by panel, then level 1's path where the tube broke
            GameSession.ForceStory = true;
            app.StartEpisode(1);
            var story = app.Session.Hud.Story;
            Check(app.Session.Hud.StoryOpen && !app.Session.Hud.BriefingOpen, "Level 1 did not open with its comic");
            for (int i = 1; i <= 4; i++) { await Shot($"L04-comic-opening-{i}", 2); if (i < 4) story.Advance(); }
            story.Advance();
            await Wait(1);
            Check(!app.Session.Hud.StoryOpen && app.Session.Hud.BriefingOpen, "The comic did not hand over to the briefing");
            app.Session.Hud.CloseBriefing(); app.Session.SetPaused(true);
            app.Session.Camera.JumpTo(Expedition.Start + new Vector2(-4, 0), 26);
            await Shot("L04-level1-tube", 3);
            app.Session.Camera.JumpTo(new Vector2(82, 256), 44);
            await Shot("L04-level1-alley", 2);
            // the opening's new stage: the beetle is down and the founders stock the clearing with seeds and its remains
            {
                var s1 = app.Session.Sim; var m1 = s1.Mission;
                foreach (int g in m1.Guards) s1.Slay(g);
                int qid = s1.Col[0].QueenId;
                s1.Ants[qid].Pos = s1.Ants[qid].PrevPos = Expedition.Clearing + new Vector2(-3, 0);
                app.Session.SetPaused(false);
                for (int i = 0; i < 200 && m1.Stage != ExpeditionStage.Provision; i++) await Wait(0.05f);
                Check(m1.Stage == ExpeditionStage.Provision && m1.RemainsLeft, $"Level 1 did not reach the stocking stage with the beetle's remains ({m1.Stage})");
                await Wait(1.5f);
                app.Session.SetPaused(true);
                app.Session.Camera.JumpTo(m1.RemainsPos, 40); // where Show objective takes the player
                await Shot("L04-level1-stocking", 2);
            }
            app.StartEpisode(10);
            Check(app.Session.Hud.StoryOpen, "Level 7 did not open with its comic beat");
            await Shot("L04-comic-beat-hornet", 2);
            GameSession.ForceStory = false;
            foreach (var (id, name) in new[] { (5, "sweet-rain"), (7, "matrons-road"), (12, "empress") })
            {
                app.StartEpisode(id); var g = app.Session;
                Check(g.Sim.Mission?.Episode == id && g.Hud.BriefingOpen, $"Level {Levels.NumberOf(id)} did not open with its briefing");
                await Shot($"L05-briefing-level{Levels.NumberOf(id):00}-{name}");
            }
            // objective cards in play, the camera on each level's own feature
            foreach (var (id, seconds, name) in new[] { (6, 100, "hollow-log-raid"), (7, 20, "matron"), (9, 30, "watch-post"), (10, 20, "paper-nest"), (8, 20, "dry-lair") })
            {
                app.StartEpisode(id); var g = app.Session;
                g.Hud.CloseBriefing();
                for (int i = 0; i < seconds * 30; i++) g.Sim.Tick(1f / 30);
                g.SetPaused(true);
                if (id is 8 or 9 or 10) g.Ants.VisibleToPlayer = _ => true; // the frame only: show the posts and lairs a scout would find
                var m = g.Sim.Mission;
                var at = id switch { 6 => m.WaveBugs.Count > 0 ? m.MarkerPos : m.WaveFrom, 7 => g.Sim.Ants[m.EscortId].Pos, 10 => m.WaveFrom, _ => m.GroupPos[0] };
                g.Camera.JumpTo(at, id == 6 ? 46 : id == 10 ? 30 : 40);
                await Shot($"L10-play-level{Levels.NumberOf(id):00}-{name}", 3);
            }
            // a won level's result card and a lost one's
            {
                app.StartEpisode(9); var g = app.Session;
                g.Hud.CloseBriefing();
                for (int i = 0; i < 60; i++) g.Sim.Tick(1f / 30);
                g.Sim.Col[1].Alive = false; g.Sim.Winner = 0; // the frame only: the result card as a win draws it
                await Shot("L23-result-win", 4);
                app.StartEpisode(5); g = app.Session;
                g.Hud.CloseBriefing();
                g.Sim.Mission.LossReason = g.Sim.Mission.DeadlineLoss; g.Sim.Winner = 1;
                await Shot("L24-result-loss", 3);
            }
            DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath("user://campaign_capture.cfg"));
            CampaignProgress.UsePath("user://campaign.cfg");
            GD.Print("[level-capture] PASS");
            app.GetTree().Quit();
        }
        catch (Exception e) { GD.PushError("[level-capture] FAIL " + e); app.GetTree().Quit(1); }
    }

    /// The map's progress: unlock order, stars kept one by one across replays, best times, totals, unlock thresholds,
    /// the colony look and the migration from the old episode keys. Uses a scratch file, never the player's.
    public static void Progress()
    {
        const string path = "user://campaign_test.cfg";
        CampaignProgress.UsePath(path);
        CampaignProgress.Reset();
        Check(CampaignProgress.Next == 1 && CampaignProgress.Unlocked(1) && !CampaignProgress.Unlocked(2) && CampaignProgress.TotalStars == 0, "Fresh map not at Level 1");
        int fresh = CampaignProgress.Record(1, 1 | 4, 400);
        Check(fresh == 5, "New stars not reported");
        CampaignProgress.Load();
        int second = Levels.IdAt(2), third = Levels.IdAt(3), fourth = Levels.IdAt(4);
        Check(CampaignProgress.Completed(1) && CampaignProgress.Unlocked(second) && !CampaignProgress.Unlocked(third) && CampaignProgress.Next == second && CampaignProgress.StarCount(1) == 2, "Level 1 win and stars not saved and reloaded");
        fresh = CampaignProgress.Record(1, 1 | 2, 600);
        Check(fresh == 2 && CampaignProgress.StarCount(1) == 3 && CampaignProgress.Best(1) == 400, "Stars or best time not merged across replays");
        // the map's order, not the ids, decides what opens next
        CampaignProgress.Record(second, 1, 900);
        Check(CampaignProgress.Unlocked(third) && !CampaignProgress.Unlocked(fourth) && CampaignProgress.Next == third, "Map order not followed");
        Check(Levels.PerksFor(CampaignProgress.TotalStars) == Perk.None && CampaignProgress.EmblemOpen(1) && !CampaignProgress.ColourOpen(1), "Unlock thresholds wrong at 4 stars");
        CampaignProgress.Choose(1, 1); // the colour is locked, the emblem open
        CampaignProgress.Load();
        Check(CampaignProgress.Colour == 0 && CampaignProgress.Emblem == 1, "The colony look ignored its locks or was not saved");
        foreach (int id in Levels.Order) CampaignProgress.Record(id, 7, 500);
        CampaignProgress.Load();
        Check(CampaignProgress.TotalStars == 3 * Levels.Count && CampaignProgress.Next == 0 && CampaignProgress.CompletedCount == Levels.Count, $"A finished map does not total {3 * Levels.Count} stars");
        Check(CampaignProgress.Perks == (Perk.Matron | Perk.Gunner | Perk.Marshal | Perk.Flyer | Perk.Veteran) && Levels.NextUnlock(36) == null, "Every unlock not open at 36 stars");
        CampaignProgress.Choose(3, 4);
        CampaignProgress.Load();
        Check(CampaignProgress.Colour == 3 && CampaignProgress.Emblem == 4, "The chosen colony look was not kept");
        // progress saved by 0.10 / PR #6 (episode keys) carries over
        var old = new ConfigFile();
        old.SetValue("campaign", "episode1", true); old.SetValue("campaign", "episode2", true);
        old.Save(path);
        CampaignProgress.Load();
        Check(CampaignProgress.Completed(1) && CampaignProgress.Completed(2) && !CampaignProgress.Completed(3) && CampaignProgress.Next == second && CampaignProgress.TotalStars == 2, "Old episode progress did not migrate");
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        CampaignProgress.UsePath("user://campaign.cfg");
        GD.Print("[campaign-test] PASS map progress: unlock order, stars kept across replays, best times, unlock thresholds, colony look, old episode keys");
    }
}
