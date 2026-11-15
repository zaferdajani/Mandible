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

    static string Heroes(Sim s) { var sb = new System.Text.StringBuilder(); for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && Config.IsHero(s.Ants[i].Type)) sb.Append($"{s.Ants[i].Team}{s.Ants[i].Type}{s.Ants[i].Level} "); return sb.ToString(); }

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
            var k = (AntType)o.A switch { AntType.Major => ProdKind.Major, AntType.Flyer => ProdKind.Flyer, AntType.ScorpionAnt => ProdKind.ScorpionAnt, _ => ProdKind.Soldier };
            Hatch(s, k, o.B + 6);
            if (c.Count[o.A] < o.B && c.Food < Config.ProdCost(k) * 2) return;
        }
        int workers = id switch { 5 => 34, 8 => 22, 11 => 20, 10 => 34, _ => 28 };
        Hatch(s, ProdKind.Worker, workers);
        if (s.Mission.Objectives[Math.Min(s.Mission.Step, s.Mission.Objectives.Count - 1)].Kind == Expedition.Obj.Hero || id is 10 or 12) Hatch(s, ProdKind.Warlord, 1);
        Hatch(s, ProdKind.Major, id is 6 ? 12 : 10);
        Hatch(s, ProdKind.Gunner, 10);
        Hatch(s, ProdKind.Flyer, 12);
        Hatch(s, ProdKind.WebAnt, 8);
        Hatch(s, ProdKind.Armoured, 6);
        Hatch(s, ProdKind.ScorpionAnt, 6);
        Hatch(s, ProdKind.Soldier, 60);
        if (!c.Researching)
            foreach (var u in new[] { Upgrade.Queen, Upgrade.Mandibles, Upgrade.Chitin })
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
        int gatherAt = id switch { 5 => 6, 6 => 99, 7 => 10, 8 => 20, 9 => 28, 10 => 30, 11 => 1, 13 => 18, 14 => 18, _ => 40 };
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
                        else if (marching && army.Count < Math.Max(3, id == 10 ? strike / 2 : strike / 4)) marching = false;
                        if (marching) s.CmdMove(army, s.W.Nests[1].Pos, true);
                        else s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false);
                        break;
                    case Expedition.Obj.Sense:
                    case Expedition.Obj.Extract:
                    case Expedition.Obj.Deliver:
                    {
                        // the plan, not a charge: the army stands off on the home side of the core, outside any silk, and
                        // fights what comes out to it (or walks beside the carrier); five workers cut and carry the core
                        var gland = m.Gland;
                        if (gland == null) { s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false); break; }
                        bool carried = gland.Carried;
                        // gather before going: an army fed in piecemeal only feeds the guardian
                        if (!marching && army.Count >= gatherAt) { marching = true; strike = army.Count; }
                        else if (marching && !carried && army.Count < Math.Max(3, strike / 3)) marching = false;
                        if (!marching && !carried) { s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false); break; }
                        s.CmdMove(army, carried ? gland.Pos : gland.Pos + (home - gland.Pos).Normalized() * 11, true);
                        if (o.Kind == Expedition.Obj.Extract && m.MasterGroup >= 0 && m.Episode == Reveal.SilkTrapId && s.GroupLeft(m.MasterGroup) > 0) break; // the web still holds: no worker walks into it
                        if (o.Kind != Expedition.Obj.Sense && t % 300 == 0)
                        {
                            int on = 0;
                            for (int i = 0; i < s.High; i++) if (s.Ants[i].Alive && s.Ants[i].Team == 0 && s.Ants[i].MutaP1 == gland.Id + 1) on++;
                            if (on < 4) s.CmdExtract(Workers(s, 5 - on), gland);
                        }
                        break;
                    }
                    case Expedition.Obj.Larva:
                    {
                        // the larva's larder: the army hunts the lairs between raids; the workers haul the remains home
                        var den = m.Groups.FindIndex(g => g.Exists(b => s.Valid(b.id, b.serial)));
                        if (den >= 0 && army.Count >= gatherAt && m.WaveBugs.TrueForAll(b => !s.Valid(b.id, b.serial))) s.CmdMove(army, m.GroupPos[den], true);
                        else s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 6, false);
                        break;
                    }
                    case Expedition.Obj.Evolve:
                        // spend in the evolve tree, as the objective teaches: a general's level if there is one, else the cheapest rank
                        s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false);
                        if (t % 60 == 0) SpendInTree(s);
                        break;
                    case Expedition.Obj.Haul:
                    case Expedition.Obj.Gather when id == 8:
                        // the Dry Spell: the lairs are the only larder, so the army keeps hunting them
                        var lair = s.Mission.Groups.FindIndex(g => g.Exists(b => s.Valid(b.id, b.serial)));
                        if (lair >= 0 && army.Count >= 18) s.CmdMove(army, s.Mission.GroupPos[lair], true);
                        else s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false);
                        break;
                    case Expedition.Obj.Group when m.CurtainGroup >= 0 && o.B == m.CurtainGroup:
                    {
                        // behind the silk curtain: only the winged ants go; everyone else keeps out of the silk
                        var wings = army.FindAll(a => s.Ants[a].Type == AntType.Flyer);
                        var ground = army.FindAll(a => s.Ants[a].Type != AntType.Flyer);
                        if (wings.Count >= 8) s.CmdMove(wings, m.CurtainAt, true); else s.CmdMove(wings, home, false);
                        s.CmdMove(ground, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false);
                        break;
                    }
                    case Expedition.Obj.Group when m.ScorpionGroup >= 0 && o.B == m.ScorpionGroup:
                    {
                        // the plan, not a charge: nobody goes near the stone. The army kills the beetles that graze on the
                        // bank and waits by their carcasses; when she walks out to feed, the heavy ants take the mouth of
                        // her burrow so she cannot go back, the web ants tie her tail, and everyone else bites
                        if (!marching && army.Count >= gatherAt) { marching = true; strike = army.Count; }
                        else if (marching && army.Count < Math.Max(3, strike / 3)) marching = false;
                        if (!marching) { s.CmdMove(army, home + (s.W.Nests[1].Pos - home).Normalized() * 10, false); break; }
                        var stone = m.GroupPos[m.ScorpionGroup];
                        int mother = m.Groups[m.ScorpionGroup].Find(b => s.Valid(b.id, b.serial)).id;
                        bool found = m.Groups[m.ScorpionGroup].Exists(b => s.Valid(b.id, b.serial));
                        if (found && s.Ants[mother].Pos.DistanceTo(stone) > 8)
                        {
                            var heavy = army.FindAll(a => s.Ants[a].Type is AntType.Major or AntType.Armoured);
                            var rest = army.FindAll(a => s.Ants[a].Type is not (AntType.Major or AntType.Armoured));
                            s.CmdMove(heavy, stone, false);
                            s.CmdAttack(rest, mother);
                        }
                        else s.CmdMove(army, m.BaitAt + (home - m.BaitAt).Normalized() * 4, true);
                        break;
                    }
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
        Check(s.QueenShielded(1) && s.CanEnqueue(0, ProdKind.Flyer) != QueueResult.Locked, "Wings and Acid: shield or gates wrong");
        Close();
        s = Open(app, 4, out _);
        Check(s.CanEnqueue(0, ProdKind.Matron) == QueueResult.Locked, "The Acid Matron was open without her stars");
        Close();
        s = Open(app, 4, out _, Perk.Matron | Perk.Veteran);
        Check(s.Mission.AllowsProd(ProdKind.Matron) && !s.Mission.AllowsProd(ProdKind.Marshal), "The Acid Matron perk did not open her (alone)");
        Close();
        s = Open(app, 3, out _, Perk.Gunner | Perk.Larder | Perk.Matron);
        Check(!s.Mission.AllowsProd(ProdKind.Gunner) && !s.Mission.AllowsProd(ProdKind.Matron), "Star perks opened castes before Act III");
        int soldiers = s.Col[0].Count[(int)AntType.Soldier];
        Close();
        s = Open(app, 10, out _, Perk.Gunner);
        Check(s.Mission.AllowsProd(ProdKind.Gunner) && s.Mission.AllowsResearch(Upgrade.Queen, 3), "The early gunner perk did not reach Act III");
        Check(s.CanEnqueue(0, ProdKind.WebAnt) == QueueResult.Locked && s.CanEnqueue(0, ProdKind.Armoured) == QueueResult.Locked, "A mutation unit could be hatched without its core");
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
        GD.Print("[campaign-test] PASS level gates, the watch-post shield and star perks (Matron, early gunners, the stocked larder, veteran brood)");
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
            Check(s.OrganicRules == (n >= Reveal.Organic) && s.EatRules == (n >= Reveal.Feasting) && !s.CarrionRules, $"Chapter {n}: organic matter or eating in the field on the wrong side of their reveal");
            Check(s.Col[1].Alive == (n >= Reveal.RedColony), $"Chapter {n}: the red colony is {(s.Col[1].Alive ? "present" : "absent")}");
            Check(m.EndsWithQueen == (n >= Reveal.FirstQueenKill && Levels.Info(id).Rival != RivalKind.None), $"Chapter {n}: {(m.EndsWithQueen ? "won" : "not won")} by a queen kill");
            // the mutation track: what the story has given by this chapter, and the sixth sense from the Silk Trap on
            Check(s.Col[0].Mutations == Sim.StoryMutations(n) && s.Has(0, Mutation.Winged) == (n > Reveal.Mutagen) && s.Has(0, Mutation.RoyalLarva) == (n > Reveal.RoyalLarva) && s.Has(0, Mutation.Web) == (n > Reveal.Web) && s.Has(0, Mutation.Stinger) == (n > Reveal.Stinger) && s.Has(0, Mutation.Armoured) == (n > Reveal.Armoured), $"Chapter {n}: the story's mutations are wrong ({s.Col[0].Mutations})");
            // a chapter holds a core only for the unit it introduces, and never hands the unit over at the start
            if (n == Reveal.Mutagen) Check(s.Col[0].Count[(int)AntType.Flyer] == 0 && m.Gland is { Gives: Mutation.Winged, Home: false }, "The Silk Trap handed over winged ants, or holds no fly's core");
            Check(s.Col[0].Count[(int)AntType.WebAnt] == 0 || n > Reveal.Web, $"Chapter {n}: web ants before the spider's core");
            Check(s.Col[0].Count[(int)AntType.Armoured] == 0 || n > Reveal.Armoured, $"Chapter {n}: armoured ants before the beetle's core");
            Check((s.Nursery(0) != null) == (n > Reveal.RoyalLarva), $"Chapter {n}: the royal nursery is {(s.Nursery(0) != null ? "dug" : "missing")}");
            if (n > Reveal.RoyalLarva) Check(s.CanEnqueue(0, ProdKind.Warlord) is QueueResult.Ok or QueueResult.HeroExists or QueueResult.NoFood, $"Chapter {n}: the royal larva cannot be laid for a general");
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

    /// Progression (docs/progression.md), end to end. Mutation: the Silk Trap played by the scripted player (the fly's
    /// core cut out, carried home, the queen laying winged ants at once, the brood behind the silk reached only on
    /// wings); one core is one unit and never a stat boost; the wasp's core waits for the fly's; a core already taken is
    /// only meat. Evolution: organic matter buys ranks, a general's levels and the queen's better brood. The Royal
    /// Larva with its losses (the larva devoured, the core carried home by the red colony) and old Burr's cocoon.
    /// Persistence: mutations and tree ranks walk into the next chapter through the roster.
    public static (float silk, float larva) MutationTimes;

    /// A core of this kind, already cut free beside the nest, taken up and carried in by a worker.
    static void Deliver(Sim t, int team, Mutation mu)
    {
        var at = t.W.Nests[team].Pos + new Vector2(5, 0);
        var g = t.AddCore(at, "a test core", AntType.Wasp, mu); g.Done = g.Work;
        int w = t.Spawn(team, AntType.Worker, at + new Vector2(0.5f, 0), 0);
        t.CmdExtract(new List<int> { w }, g);
        for (int k = 0; k < 30 * 30 && !g.Home; k++) t.Tick(1f / 30);
        Check(g.Home && g.HomeTeam == team, $"A free core beside the nest was not carried in ({mu})");
    }

    /// The scripted player's spending: a general's level first, else the cheapest open rank, else the queen.
    static void SpendInTree(Sim s)
    {
        for (int i = 0; i < s.High; i++)
            if (s.Ants[i].Alive && s.Ants[i].Team == 0 && Config.IsHero(s.Ants[i].Type) && s.CanLevelGeneral(i) == EvolveResult.Ok)
            { Cmd.Units(CmdKind.Evolve, 0, new List<int> { i }, s).Apply(s); return; }
        foreach (var d in Evolve.Nodes)
            if (s.CanEvolve(0, d.Node) == EvolveResult.Ok) { new Cmd { Kind = CmdKind.Evolve, Team = 0, Arg = (byte)d.Node }.Apply(s); return; }
    }

    /// `MANDIBLE_LEVELS=scorpion`: the scorpion's checks on their own (they also run inside `mutation`).
    public static void ScorpionOnly(Main app)
    {
        const string path = "user://campaign_scorpion.cfg";
        CampaignProgress.UsePath(path); CampaignProgress.Reset();
        Scorpions(app);
        Close();
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        CampaignProgress.UsePath("user://campaign.cfg");
    }

    /// The scorpion (docs/progression.md 1d): the core and its unit, the sting, the Stone Mother's rules, her chapter.
    public static void Scorpions(Main app)
    {
        var t = Open(app, 9, out _);
        var tc = t.Col[0];
        var yard = t.W.Nests[0].Pos + (t.W.Nests[0].Pos - t.W.Nests[1].Pos).Normalized() * 30;
        for (int pass = 0; pass < 2; pass++) for (int i = 0; i < t.High; i++) if (t.Ants[i].Alive && t.Ants[i].Team == Config.Wild) t.Slay(i); // an empty yard
        t.Cores.Clear();
        t.W.Food.RemoveAll(f => f.Kind == FoodKind.Corpse);
        Check(t.CanEnqueue(0, ProdKind.ScorpionAnt) == QueueResult.Locked, "Scorpion ants could be hatched before the scorpion's core");
        int before = tc.Mutations;
        Deliver(t, 0, Mutation.Scorpion);
        Check(t.Has(0, Mutation.Scorpion) && tc.Mutations == (before | (int)Mutation.Scorpion) && t.CanEnqueue(0, ProdKind.ScorpionAnt) != QueueResult.Locked, "The scorpion's core did not give the queen scorpion ants, and only them");
        // the sting: goes between the plates, numbs, and cannot reach the air
        int ant = t.Spawn(0, AntType.ScorpionAnt, yard, 0), plated = t.Spawn(1, AntType.Armoured, yard + new Vector2(1.2f, 0), 0, 3);
        t.Ants[plated].State = AntState.Hold;
        float hp = t.Ants[plated].Hp; t.Damage(plated, 10f, -1); float turned = hp - t.Ants[plated].Hp;
        hp = t.Ants[plated].Hp; t.Damage(plated, 10f, -1, pierce: true); float through = hp - t.Ants[plated].Hp;
        Check(turned < 7.5f && through > 9.9f, $"A piercing blow did not go between the plates ({turned:0.0} against {through:0.0})");
        for (int k = 0; k < 150 && t.Ants[plated].NumbT <= 0; k++) { t.CmdAttack(new List<int> { ant }, plated); t.Tick(1f / 30); }
        Check(t.Ants[plated].NumbT > 0 && t.Ants[plated].SlowT > 0 && t.Ants[ant].TailCd > 0, "A scorpion ant's sting did not numb");
        int wasp = t.Spawn(Config.Wild, AntType.Wasp, yard + new Vector2(0, 3), 0); t.Ants[wasp].Alt = Config.CruiseAlt(AntType.Wasp);
        Check(!t.CanReach(t.Ants[ant], t.Ants[wasp]), "A scorpion ant could reach a wasp in the air");
        t.Slay(wasp); t.Slay(plated); t.Slay(ant);
        t.W.Food.RemoveAll(f => f.Kind == FoodKind.Corpse);
        // the Stone Mother under her stone: a massed charge feeds her
        var stone = yard + (t.W.Nests[0].Pos - t.W.Nests[1].Pos).Normalized().Orthogonal() * 40;
        int mother = t.Spawn(Config.Wild, AntType.Scorpion, stone, Mathf.Pi, 6);
        t.Ants[mother].Home = t.Ants[mother].Anchor = stone; t.Ants[mother].State = AntState.Idle;
        t.GiveCore(mother, Mutation.Scorpion);
        var charge = new List<int>();
        for (int k = 0; k < 24; k++) charge.Add(t.Spawn(0, AntType.Soldier, stone + new Vector2(-9 - k % 4, (k / 4 - 3) * 1.2f), 0));
        for (int k = 0; k < 30 * 25; k++) { if (k % 30 == 0) t.CmdAttack(charge, mother); t.Tick(1f / 30); }
        int left = charge.FindAll(a => t.Ants[a].Alive && t.Ants[a].Team == 0).Count;
        Check(t.Ants[mother].Alive && t.Ants[mother].Hp > t.Ants[mother].MaxHp * 0.7f && left <= 12, $"A massed charge beat the Stone Mother under her stone (health {t.Ants[mother].Hp / t.Ants[mother].MaxHp:0.00}, {left} of 24 soldiers left)");
        foreach (int a in charge) t.Slay(a);
        t.W.Food.RemoveAll(f => f.Kind == FoodKind.Corpse);
        t.Ants[mother].Hp = t.Ants[mother].MaxHp; t.Ants[mother].State = AntState.Idle; t.Ants[mother].Pos = stone;
        for (int k = 0; k < 30 * 8; k++) t.Tick(1f / 30);
        Check(t.Denned(t.Ants[mother]), "The Stone Mother left her stone with nothing to bring her out");
        // meat on open ground brings her out
        int prey = t.Spawn(Config.Wild, AntType.Beetle, stone + new Vector2(-16, 0), 0); t.Slay(prey);
        for (int k = 0; k < 30 * 20 && t.Ants[mother].Pos.DistanceTo(stone) < 10; k++) t.Tick(1f / 30);
        Check(t.Ants[mother].Pos.DistanceTo(stone) >= 10, "A fresh carcass on open ground did not bring the Stone Mother out");
        // silk ties the tail: no sting while it holds
        int web = t.Spawn(0, AntType.WebAnt, t.Ants[mother].Pos + (t.Ants[mother].Pos - stone).Normalized() * 7.2f, 0);
        for (int k = 0; k < 30 * 6 && t.Ants[mother].PinT <= 0; k++) t.Tick(1f / 30);
        Check(t.Ants[mother].PinT > 0, "A web ant did not tie the scorpion's tail");
        // in the open she takes every blow in full; dead, her young spill off and the core lies in her body
        hp = t.Ants[mother].Hp; t.Damage(mother, 100f, -1); float open = hp - t.Ants[mother].Hp;
        Check(open > 60f, "The Stone Mother in the open shrugged off a blow as if under her stone");
        int cores = t.Cores.Count;
        t.Slay(mother, web);
        int young = 0; for (int i = 0; i < t.High; i++) if (t.Ants[i].Alive && t.Ants[i].Type == AntType.Scorpling) young++;
        Check(young == 8 && t.Cores.Count == cores + 1 && t.Cores[^1].Gives == Mutation.Scorpion, $"The Stone Mother's death did not leave her young and her core ({young} young)");
        GD.Print("[campaign-test] PASS scorpion: the core gives scorpion ants and nothing else; the sting pierces plates and numbs; a charge at the stone fails, meat brings her out, silk ties her tail, her young spill off her body");
        // ---- her chapter, played by the plan
        Play(app, 9, out int stars9);
        var s = LastSim; var m = s.Mission;
        Check(m.Gland != null && m.Gland.Home && m.Gland.HomeTeam == 0 && s.Has(0, Mutation.Scorpion), "Wings and Acid was won without the scorpion's core");
        Check(s.Col[0].Mutations == (Sim.StoryMutations(Reveal.Scorpion) | (int)Mutation.Scorpion), $"The queen did not take exactly the scorpion ants in Wings and Acid (mutations {s.Col[0].Mutations})");
        Check(m.TailStings > 0, "No scorpion ant stung in Wings and Acid");
        GD.Print($"[campaign-test] PASS Wings and Acid: the Stone Mother brought down in the open, her core carried home, {s.Col[0].Count[(int)AntType.ScorpionAnt]} scorpion ants alive, {m.TailStings} tail strikes, {m.EndTime:0}s, lost {m.EndLost}, stars {Count(stars9)}");
    }

    public static void Mutations(Main app)
    {
        const string path = "user://campaign_mutation.cfg";
        CampaignProgress.UsePath(path); CampaignProgress.Reset();
        // ---- the Silk Trap
        Play(app, Reveal.SilkTrapId, out int stars13);
        var s = LastSim; var m = s.Mission; var c = s.Col[0];
        MutationTimes.silk = m.EndTime;
        Check(m.Gland.Home && m.Gland.HomeTeam == 0, "The fly's core was not carried home");
        Check(s.Has(0, Mutation.Winged) && !s.Has(0, Mutation.Stinger) && c.Mutations == (int)Mutation.Winged, $"The queen did not take exactly the winged ants (mutations {c.Mutations})");
        Check(m.MastersSlain > 0, "The web spider was left alive on its catch");
        Check(LastHarvest.Mutations == (int)Mutation.Winged && ColonyRoster.Load(LastHarvest.Save()).Mutations == (int)Mutation.Winged, "The winged ants did not walk out of the Silk Trap");
        GD.Print($"[campaign-test] PASS Silk Trap: the fly's core cut out and hauled home, the queen lays winged ants ({c.Count[(int)AntType.Flyer]} alive), the brood behind the silk destroyed from the air");
        // ---- one core, one unit: nothing counted, nothing boosted, the order of the cores, a core twice
        {
            var t = Open(app, Reveal.SilkTrapId, out _);
            var tc = t.Col[0];
            Check(t.CanEnqueue(0, ProdKind.Flyer) == QueueResult.Locked, "Winged ants could be hatched before the fly's core");
            int soldier = -1; for (int i = 0; i < t.High && soldier < 0; i++) if (t.Ants[i].Alive && t.Ants[i].Team == 0 && t.Ants[i].Type == AntType.Soldier) soldier = i;
            float hp0 = t.Ants[soldier].MaxHp, bite0 = t.Ants[soldier].DmgMul;
            Deliver(t, 0, Mutation.Stinger);
            Check(!t.Has(0, Mutation.Stinger) && (tc.Waiting & (int)Mutation.Stinger) != 0, "A wasp's core before the fly's did not wait in the nest");
            Deliver(t, 0, Mutation.Winged);
            Check(t.Has(0, Mutation.Winged) && t.Has(0, Mutation.Stinger) && tc.Waiting == 0, "The waiting wasp's core was not taken once the queen had winged ants");
            Check(t.CanEnqueue(0, ProdKind.Flyer) != QueueResult.Locked, "The fly's core did not open winged ants");
            Check(t.Ants[soldier].MaxHp == hp0 && t.Ants[soldier].DmgMul == bite0, "A mutation core made the colony's soldiers stronger: it must only give a new unit");
            float organic = tc.Organic;
            Deliver(t, 0, Mutation.Winged);
            Check(tc.Mutations == (int)(Mutation.Winged | Mutation.Stinger) && tc.Organic >= organic + global::Mandible.Mutations.CoreOrganic - 1, "A core the queen already has was not simply eaten");
            // venom: a stinging winged ant's strike keeps burning
            var at = t.W.Nests[0].Pos + new Vector2(30, 30);
            int fly = t.Spawn(0, AntType.Flyer, at, 0), bug = t.Spawn(Config.Wild, AntType.Beetle, at + new Vector2(0.8f, 0), 0, 3);
            t.Ants[fly].Alt = t.Ants[bug].Alt;
            for (int k = 0; k < 90 && t.Ants[bug].PoisonT <= 0; k++) { t.CmdAttack(new List<int> { fly }, bug); t.Tick(1f / 30); }
            Check(t.Ants[bug].PoisonT > 0, "A stinging winged ant's strike did not poison");
            // ---- the evolve tree: organic matter buys ranks on a unit's branch, only where the queen has the unit
            tc.Organic = 0;
            new Cmd { Kind = CmdKind.Evolve, Team = 0, Arg = (byte)EvoNode.FlightMuscle }.Apply(t);
            Check(tc.Evo[(int)EvoNode.FlightMuscle] == 0, "A rank was bought with no organic matter");
            tc.Organic = 100;
            float speed0 = t.Ants[fly].SpeedMul;
            new Cmd { Kind = CmdKind.Evolve, Team = 0, Arg = (byte)EvoNode.FlightMuscle }.Apply(t);
            Check(tc.Evo[(int)EvoNode.FlightMuscle] == 1 && tc.Organic == 100 - Evolve.Cost(EvoNode.FlightMuscle, 1) && t.Ants[fly].SpeedMul > speed0, "Flight Muscle was not bought, paid for and felt by the winged ants");
            Check(t.CanEvolve(0, EvoNode.Spinneret) == EvolveResult.Locked && t.CanEvolve(0, EvoNode.Plates) == EvolveResult.Locked, "A branch was open without its mutation");
            Check(t.Ants[soldier].MaxHp == hp0, "A rank on the winged branch changed the soldiers");
            // the queen's level: better brood for what she lays afterwards, not for those already hatched
            tc.Tier[(int)Upgrade.Queen] = 3; tc.Food = 2000; tc.Organic = 50;
            int before = tc.Count[(int)AntType.Soldier];
            tc.PopCap += 20; tc.Queue.Clear();
            var queued = t.Enqueue(0, ProdKind.Soldier);
            Check(queued == QueueResult.Ok, $"The test could not queue a soldier squad ({queued})");
            for (int k = 0; k < 30 * 120 && tc.Count[(int)AntType.Soldier] < before + 6; k++) t.Tick(1f / 30);
            int young = -1; for (int i = t.High - 1; i >= 0 && young < 0; i--) if (t.Ants[i].Alive && t.Ants[i].Team == 0 && t.Ants[i].Type == AntType.Soldier && t.Ants[i].Brood == 2) young = i;
            Check(young >= 0 && t.Ants[young].MaxHp > hp0 * 1.04f && t.Ants[soldier].MaxHp == hp0, $"A level-3 queen's brood did not hatch stronger than the ants she laid before (young {young}, soldiers {before} -> {tc.Count[(int)AntType.Soldier]}, old {t.Ants[soldier].MaxHp:0.0}/{hp0:0.0} alive {t.Ants[soldier].Alive})");
            GD.Print($"[campaign-test] PASS progression rules: one core one unit, no colony boost, the wasp's core waits for the fly's, a second core is meat; Flight Muscle bought with organic matter; a level-3 queen's soldier hatches with {t.Ants[young].MaxHp:0} health against {hp0:0}");
            Close();
        }
        // a carrier that falls drops the core where it stands; another worker takes it up
        {
            var t = Open(app, Reveal.SilkTrapId, out _);
            var g = t.Mission.Gland; g.Done = g.Work;
            t.Webs.Clear();
            int w = t.Spawn(0, AntType.Worker, g.Pos + new Vector2(0.5f, 0), 0);
            t.CmdExtract(new List<int> { w }, g);
            for (int k = 0; k < 30 && !g.Carried; k++) t.Tick(1f / 30);
            Check(g.Carried, "A worker at a free core did not take it up");
            for (int k = 0; k < 60; k++) t.Tick(1f / 30);
            var dropAt = g.Pos;
            t.Slay(w);
            t.Tick(1f / 30);
            Check(!g.Carried && !g.Home && g.Pos.DistanceTo(dropAt) < 1.5f, "A fallen carrier did not drop the core where it fell");
            GD.Print("[campaign-test] PASS loss path: the core falls with its carrier and waits to be taken up again");
            Close();
        }
        // the massed charge fails: an army sent straight at the core walks into the silk and is held there
        {
            var t = Open(app, Reveal.SilkTrapId, out _);
            var g = t.Mission.Gland;
            var charge = Army(t);
            int sent = charge.Count;
            for (int k = 0; k < 30 * 90; k++) { if (k % 60 == 0) t.CmdMove(Army(t), g.Pos, true); t.Tick(1f / 30); }
            int held = 0; foreach (int id in Army(t)) if (t.Ants[id].Root > 0) held++;
            int left = Army(t).Count;
            GD.Print($"[campaign-test] the charge at the web: {sent} sent, {left} alive after 90 s, {held} held in the silk, web spiders left {t.GroupLeft(t.Mission.MasterGroup)}");
            Check(held + (sent - left) >= sent / 2, "A massed charge at the web was not punished by the silk");
            Close();
        }
        // ---- Ashling's core: the red burial party carries it home if nobody stops it
        CampaignProgress.Record(Levels.IdAt(Reveal.FirstQueenKill), 1, 900);
        var afterAshling = new ColonyRoster { QueenTier = 2, Mutations = LastHarvest.Mutations };
        afterAshling.Veterans.Add(new Veteran { Type = AntType.Soldier, Level = 3, Kills = 7, Joined = 1, Name = "Burr" }); // old Burr, who goes into the silk
        afterAshling.Plain[(int)AntType.Worker] = 14; afterAshling.Plain[(int)AntType.Soldier] = 18; afterAshling.Plain[(int)AntType.Flyer] = 6;
        CampaignProgress.CarryRoster(Levels.IdAt(Reveal.FirstQueenKill), afterAshling);
        var entering = CampaignProgress.RosterFor(Reveal.RoyalLarvaId);
        Check(entering != null && entering.Mutations == (int)Mutation.Winged, "The Royal Larva did not receive the winged ants");
        {
            Carry = entering;
            var t = Open(app, Reveal.RoyalLarvaId, out var rival);
            Check(t.Mission.Gland != null && !t.Has(0, Mutation.RoyalLarva) && t.Col[0].LarvaStage == 0, "Ashling's core was handed over instead of lying in the field");
            for (int k = 0; k < 30 * 240 && t.Winner < 0; k++) { rival?.Tick(1f / 30); t.Tick(1f / 30); }
            Check(t.Winner == 1 && t.Mission.LossReason.Contains("core"), $"The red burial party, left alone, did not carry Ashling's core home ({t.Winner}, {t.Mission.LossReason})");
            GD.Print($"[campaign-test] PASS loss path: {t.Mission.LossReason}");
            Close();
        }
        // the loss of the larva (devoured), then the retry from the same colony
        {
            Carry = entering;
            var t = Open(app, Reveal.RoyalLarvaId, out var rival);
            t.Mission.RivalWantsCore = false;
            t.GiveMutation(0, Mutation.RoyalLarva);
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
        // old Burr goes into the silk: when the larva spins its cocoon he leaves the field, and the Warlord that breaks out is him
        {
            Carry = entering;
            var t = Open(app, Reveal.RoyalLarvaId, out _);
            var tc = t.Col[0];
            t.Mission.RivalWantsCore = false;
            t.GiveMutation(0, Mutation.RoyalLarva);
            var room = t.ChamberOf(0, ChamberKind.Nursery); room.Done = room.Work; room.Built = true;
            for (int k = 0; k < 30; k++) t.Tick(1f / 30);
            int burrId = Find(t, "Burr");
            Check(burrId >= 0 && t.Ants[burrId].Type == AntType.Soldier, "Old Burr was not on the field as a soldier");
            tc.LarvaFed = tc.LarvaNeed; tc.Organic += 10;
            for (int k = 0; k < 90 && tc.LarvaStage != 3; k++) t.Tick(1f / 30);
            Check(tc.LarvaStage == 3 && t.Mission.InCocoon?.Name == "Burr" && !t.Ants[burrId].Alive && Find(t, "Burr") < 0, "Old Burr did not go into the cocoon");
            tc.CocoonT = 0;
            for (int k = 0; k < 5 && t.Mission.HeroesBorn == 0; k++) t.Tick(1f / 30);
            int warlord = Find(t, "Burr");
            Check(t.Mission.InCocoon == null && warlord >= 0 && t.Ants[warlord].Type == AntType.Warlord, "The Warlord that broke out is not old Burr");
            var vet = t.Mission.Tracked[warlord].vet;
            Check(vet.Joined == 1 && vet.Kills == 7, $"Burr lost his history in the silk (joined {vet.Joined}, kills {vet.Kills})");
            // a general levels in the evolve tree, on organic matter, and each level is a skill point
            tc.Organic = 0;
            Cmd.Units(CmdKind.Evolve, 0, new List<int> { warlord }, t).Apply(t);
            Check(t.Ants[warlord].Level == 1, "A general levelled with no organic matter");
            tc.Organic = Evolve.GeneralCost(2) + 1;
            int points = t.Ants[warlord].SkPts;
            Cmd.Units(CmdKind.Evolve, 0, new List<int> { warlord }, t).Apply(t);
            Check(t.Ants[warlord].Level == 2 && t.Ants[warlord].SkPts == points + 1 && tc.Organic < 1.5f, "A general's level was not bought in the evolve tree for a skill point");
            GD.Print("[campaign-test] PASS old Burr went into the cocoon a soldier and broke out the Warlord, with his kills and the chapter he joined; his second level was bought with organic matter");
            Close();
        }
        CampaignProgress.Load();
        var retry = CampaignProgress.RosterFor(Reveal.RoyalLarvaId);
        Check(retry != null && retry.Save() == entering.Save() && !CampaignProgress.Completed(Reveal.RoyalLarvaId), "The lost Royal Larva changed the saved colony");
        Carry = retry;
        Play(app, Reveal.RoyalLarvaId, out int stars14);
        s = LastSim; m = s.Mission; c = s.Col[0];
        MutationTimes.larva = m.EndTime;
        Check(m.HeroesBorn == 1, "No general broke out of the cocoon");
        Check(s.Has(0, Mutation.RoyalLarva) && s.Has(0, Mutation.Winged) && m.Gland.HomeTeam == 0, "Ashling's core did not reach the queen, or the winged ants were lost");
        var hemlock = LastHarvest.Veterans.Find(v => v.Type == AntType.Warlord);
        bool burrFell = m.Fallen.Exists(v => v.Name == "Burr");
        Check(LastHarvest.Veterans.Exists(v => v.Name == "Burr" && v.Type == AntType.Warlord) || m.Fallen.Exists(v => v.Name == "Burr"), "Old Burr neither came out of the cocoon as the Warlord nor fell before it");
        Check(m.InCocoon == null && !LastHarvest.Veterans.Exists(v => v.Name == "Burr" && v.Type == AntType.Soldier), "Old Burr was left in the silk, or walked out twice");
        Check(hemlock == null ? c.Count[(int)AntType.Warlord] == 0 || burrFell : hemlock.Name == "Burr" ? hemlock.Joined == 1 && hemlock.Kills >= 7 : hemlock.Name == "Hemlock" && burrFell,
            $"The Warlord from the cocoon is not old Burr ({hemlock?.Name}, joined {hemlock?.Joined}, kills {hemlock?.Kills}, Burr fell {burrFell})");
        Check(LastHarvest.Chambers.Contains(ChamberKind.Nursery), "The royal nursery was not carried");
        GD.Print($"[campaign-test] PASS Royal Larva: Ashling's core won from the burial party, larva fed {c.LarvaNeed:0} organic, cocoon hatched, {(hemlock?.Name ?? "nobody")} broke out");
        // ---- persistence: mutations and the evolve tree walk into Queen Cinder's chapter
        CampaignProgress.Record(Reveal.RoyalLarvaId, stars14, m.EndTime);
        LastHarvest.Evo[(int)EvoNode.FlightMuscle] = 2;
        CampaignProgress.CarryRoster(Reveal.RoyalLarvaId, LastHarvest);
        CampaignProgress.Load();
        int cinder = Levels.IdAt(Reveal.RoyalLarva + 1);
        var intoCinder = CampaignProgress.RosterFor(cinder);
        Check(intoCinder != null && intoCinder.Mutations == LastHarvest.Mutations && intoCinder.Evo[(int)EvoNode.FlightMuscle] == 2, "Mutations or evolve-tree ranks did not reach the next chapter's roster");
        Check(ColonyRoster.Load("2|0,|10,5,|") == null, "A roster from before the progression rework was loaded");
        Carry = intoCinder;
        var q = Open(app, cinder, out _);
        Check(q.Has(0, Mutation.Winged) && q.Has(0, Mutation.RoyalLarva) && q.Evo(0, EvoNode.FlightMuscle) == 2 && (hemlock == null || q.Col[0].Count[(int)AntType.Warlord] > 0), "Queen Cinder's chapter did not begin with the mutations, the tree and the Warlord");
        if (hemlock != null) { int w = -1; for (int i = 0; i < q.High && w < 0; i++) if (q.Ants[i].Alive && q.Ants[i].Team == 0 && q.Ants[i].Type == AntType.Warlord) w = i; Check(q.Ants[w].Level == hemlock.Level, "The general's level did not carry over"); }
        Check(q.Col[0].PopCap >= Config.StartPopCap + Evolve.BroodRoom * (q.Col[0].T(Upgrade.Queen) - 1), "The queen's level added no room");
        Scorpions(app); // last: its chapter writes its own harvest over the Royal Larva's
        Close();
        DirAccess.RemoveAbsolute(ProjectSettings.GlobalizePath(path));
        CampaignProgress.UsePath("user://campaign.cfg");
        GD.Print($"[campaign-test] PASS progression: mutations and evolve-tree ranks carried into Queen Cinder; stars {Count(stars13)} and {Count(stars14)}");
    }

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
        Check(CampaignProgress.Perks == (Perk.Matron | Perk.Gunner | Perk.Marshal | Perk.Larder | Perk.Veteran) && Levels.NextUnlock(36) == null, "Every unlock not open at 36 stars");
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
