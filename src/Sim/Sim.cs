using System;
using System.Collections.Generic;
using Godot;

namespace Mandible;

public struct Ant
{
    public bool Alive;
    public int Serial;
    public int Team;
    public AntType Type;
    public Vector2 Pos;
    public float Heading, Speed, Hp;
    public AntState State, Prev;
    public Vector2 Goal, Anchor, Home; // Home: a wild bug's lair, the centre of its patrol
    public int Target, TargetSerial;
    public bool Forced;
    public FoodSource FoodTarget, Assigned;
    public bool Carrying;
    public FoodKind CarryKind;
    public float Gait, Wander, Think, Deposit, Cooldown, AttackAnim, HitFlash, Stuck, DamagedT;
    public bool Selected;
    public float Height; // cached ground height for rendering
    public int Level;
    public float Xp, MaxHp, Size, DmgMul;
    public float Alt;        // height above ground (flyers)
    public float Root;       // seconds left stuck in spider silk
    public float AbilityCd;  // special attack cooldown
    public int Forge;        // mandible-forge tier this warrior hatched with
    public float SpeedMul;
    public Vector2 PrevPos;           // state at the previous tick, for smooth rendering between ticks
    public float PrevHeading, PrevAlt;
    public int HeroTier;              // queen tier a hero hatched under
    public int SquadP1;               // squad id + 1 (0 = not in a squad)
    public float BuffT, BuffMul;      // war cry / rally
    public float PoisonT, PoisonDps;  // acid, venom
    public int PoisonSrc;
    // signature specials: a charge or lunge under way, a raised shell, a wasp climbing away, and their shared cooldown
    public float ChargeT, BlockT, RetreatT, SpecialCd;
    // lane war
    public bool Guardian;             // an immobile mound guardian (lane tower)
    public bool Boss;                 // the Old Stag: Lane War's neutral boss, roused rather than killed
    public bool Raider;               // campaign: a wild bug sent from its lair to break into the player's nest
    public byte Lane, LaneStep;       // lane creep: lane index + 1 and the waypoint it is heading for
    public bool Manual;               // a player-steered hero: its ability fires only on command
    public float CastReq;             // seconds left on a pending ability command
    // the nest
    public byte Layer;                // 0 the surface, 1 + team: inside that colony's nest
    public byte GoalLayer;            // layer the current Move / AttackMove / Dig goal is on
    public int DigP1;                 // chamber id + 1 this worker is digging (0 = none)
    // carrion (Carrion.cs)
    public bool Hauling;              // gripping a carcass with nestmates to drag it home
    public bool Burying;              // carrying a dead nestmate to the midden
    public bool Feasting;             // a hero eating at a carcass
    public float FeastT;
    public AntType CarryType;         // what the carried carcass piece came from (rendering)
    public int CarryTeam;
    // evolution by eating (Carrion.cs)
    public float Evo;                 // organic matter eaten toward the next stage
    public int Stage;                 // 0 as hatched, up to Config.MaxStage
    public bool FeastOrder;           // sent to eat the remains in FoodTarget
    // general skills (Skills.cs): ranks of Q W E, unspent points, autocast bits, the W and E cooldowns (Q uses AbilityCd)
    public byte Sk0, Sk1, Sk2, Sk3, SkPts, AutoMask; // ranks of the loadout's slots Q W E R
    public float CdW, CdE, CdR;                       // cooldowns of W E R (Q: AbilityCd)
    public byte ChargeKind;                           // a charge under way: 1 Antler Charge, 2 Lunge
    public float BlockArmor;                          // Shell Wall: the armour its rank adds while BlockT runs
    public byte ChargeRank;                           // Antler Charge: the rank of the charge under way
    public float MarkT, MarkMul;                      // Scout Swarm: marked, takes more damage
    public byte CastSlot;             // pending cast: slot + 1 (with CastReq seconds left to find its moment)
    public int CastTarget, CastSerial;
    public Vector2 CastAt;
    public byte CharmP1;              // under Queen's Pheromone: the ant's own colony + 1, and seconds left
    public float CharmT;
    public bool Summoned;             // a callow from Brood Call: lasts LifeT, hardens a level every few seconds up to SummonCap
    public float LifeT, HardenT;
    public byte SummonCap;
    public float FrenzyT;             // Mandible Frenzy
    public byte FrenzyRank;
    public float HasteT, HasteMul, GuardArmor; // Trail Blaze
    public float SlowT, SlowMul;      // Caustic Cloud
    public bool AutoEat;              // eats what it kills (and remains it idles by); off: leaves them for the workers to haul home
    // mutation (Mutation.cs)
    public int MutaP1;                // mutagen source id + 1 this worker carves, carries or guards (0 = none)
    public bool MutaCarry;            // has the gland in its jaws
    public bool Nurse;                // lives in the nursery, feeding the royal larva
    // flight (the owner's rule: ground ants cannot fight a flyer in the air)
    public float PinT;                // seconds pinned to the ground by thrown silk (Mutation.WebThrowing)
    public float CoverT;              // a ground ant ducking away from a wasp it cannot reach
}

public enum EvKind { HerdArrives, HerdMilked, StagWakes, StagRoused, Bite, Death, Pickup, Deliver, Spawn, ProdDone, QueenHit, QueenDead, PopBlocked, UnderAttack, Shower, LevelUp, WebShot, WebLand, Sweep, Sting, ResearchDone, Spit, WarCry, AcidBurst, DiveBomb, SquadReady, NestFounded, QueenInside, ChamberDone, StoreFull, Charge, Toss, Lunge, ShellWall, Rally, Hurt, Bury, Feast, Evolve,
    SkillLearned, Pheromone, Charmed, Frenzy, Summon, Fade, Cloud, TrailBlaze, TalonDive, Collect, RallyScent, Bridge, Scout, Thrall,
    MutagenFree, MutagenTaken, MutagenDropped, MutagenHome, Mutate, LarvaLaid, Cocoon, HeroBorn, LarvaLost }

public struct SimEvent
{
    public EvKind Kind;
    public Vector2 Pos, To;
    public int Team;
    public AntType Type;
    public ProdKind Prod;
    public Upgrade Upgrade;
    public int Tier;
    public float Radius;
    public ChamberKind Chamber;
    public int HatchP1;               // Spawn of a newly hatched ant: its id + 1 (0 = none); `To` is where it hatched. Rendering only.
    // rendering only: Hurt / Death name the victim (id + 1), the attacker's type and the blow as a share of the victim's health;
    // Death, Pickup and Feast the carcass (food id + 1)
    public int Who, FoodP1;
    public AntType By;
    public float Amount;
}

public sealed class WebZone
{
    public Vector2 Pos;
    public float R, Life, Max, Root;
}

/// Living Bridge: a chain of bodies between A and B; the colony's ants on it walk over rocks.
public sealed class BridgeZone
{
    public Vector2 A, B;
    public float Life, Max, Width;
    public int Team, Rank;
}

/// Scout Swarm: winged scouts circling a spot; the fog lifts there for their colony and enemies inside are marked.
public sealed class ScoutZone
{
    public Vector2 Pos;
    public float R, Life, Max, Mark, Pick, PickT;
    public int Team, Rank, Flyers, Src;
}

/// Venom left on the ground by an Acid Burst: poisons every enemy of its colony standing in it.
public sealed class AcidPool
{
    public Vector2 Pos;
    public float R, Life, Max, Dps;
    public int Team, Src;
    public float Slow;   // Caustic Cloud: enemies inside move this much slower
    public bool Cloud;   // a cloud of mist rather than a pool on the soil (rendering)
}

sealed class WebShotFx
{
    public Vector2 From, To;
    public float T, Dur, R, Life, Root;
}

public enum ResearchResult { Ok, NoFood, NoOrganic, Locked, Maxed, Busy, Dead }

/// A pack of warriors hatched together. It ranks up as one, and refills and heals at its nest.
public sealed class Squad
{
    public int Id, Team, Size, Level = 1, Forge;
    public AntType Type;
    public float Xp, RecruitT;
    public bool Recalled;                     // ordered home to reinforce
    public bool AutoEat;                      // the squad's auto-eat order: new recruits take it too
    public readonly List<int> Members = new();
    public int Count => Members.Count;
    public bool Alive => Members.Count > 0;
}

public sealed class ProdItem
{
    public ProdKind Kind;
    public float Progress, Time;
}

public sealed class Colony
{
    public int Team;
    public float Food;
    public int PopUsed, PopCap = Config.StartPopCap;
    public readonly List<ProdItem> Queue = new();
    public Vector2 Rally;
    public bool HasRally;
    public int QueenId = -1;
    public bool Alive = true;
    public float FoodMult = 1f;
    public Race Race => Config.RaceOf(Team);
    // food effects: each delivery tops these up; they run while the supply keeps flowing
    public float SugarT, JuiceT, ProteinT;
    public readonly float[] GatheredBy = new float[5]; // per FoodKind, for stats and balance tests
    // the nest: dug by workers; nothing hatches until the queen is below ground
    public bool Founded;
    public float FoodCap = Config.BaseFoodCap;
    public readonly List<NestChamber> Chambers = new();
    public bool StoreFullWarned;
    public readonly int[] Count = new int[13];
    public float Gathered, Spent;
    public int Produced, Lost, Killed;
    public bool PopBlocked;
    public float QueenAlarm;
    // the dead (Carrion.cs): the standing order, its effects, and the refuse pile
    public RemainsOrder Remains = RemainsOrder.Haul;
    public float MournT;
    // organic matter: the evolving material, only from other creatures' remains (Carrion.cs)
    public float Organic = Config.StartOrganic, OrganicGathered, OrganicEaten;
    // mutation (Mutation.cs): the campaign colony only; zero everywhere else
    public int Mutagen, Mutations, Instability, Level = 1, WebRank;
    public float MutagenGathered;
    public int LarvaStage;            // 0 none, 1 laid (waiting for the nursery), 2 a larva the nurses feed, 3 a cocoon
    public AntType LarvaHero = AntType.Warlord;
    public float LarvaFed, LarvaNeed, LarvaHp = 1, CocoonT, CocoonTime;
    public int Buried;
    public Vector2 MiddenPos;
    public bool HasMidden;
    // research: the queen starts at tier 1, everything else at 0
    public readonly int[] Tier = { 1, 0, 0, 0, 0 };
    public bool Researching;
    public Upgrade ResearchKind;
    public float ResearchProgress, ResearchTime;

    public int T(Upgrade u) => Tier[(int)u];

    public int PendingChambers() { int n = 0; foreach (var q in Queue) if (q.Kind == ProdKind.Chamber) n++; return n; }
}

public enum QueueResult { Ok, NoFood, NoOrganic, QueueFull, MaxChambers, Dead, Locked, HeroExists }

public sealed partial class Sim
{
    public readonly World W;
    public readonly Pheromones Ph = new();
    public readonly SpatialHash Hash = new(Config.MaxAnts);
    public readonly Ant[] Ants = new Ant[Config.MaxAnts];
    public int High; // highest used index + 1
    readonly Stack<int> _free = new();
    public readonly Colony[] Col = { new() { Team = 0 }, new() { Team = 1 } };
    public int WildCount;
    public readonly List<SimEvent> Events = new();
    public readonly Rng Rng = new(1234);
    public float Time;
    public int Ticks;
    int _serial;
    public int Winner = -1;

    /// Colony War option: every nest starts with mound guardians at its entrance (Lane War always has them).
    public readonly bool Fortified;

    public Sim(World w, GameMode mode = GameMode.Colony, AntType hero0 = AntType.Warlord, AntType hero1 = AntType.Warlord, bool fortified = false)
    {
        W = w;
        Mode = mode;
        Fortified = fortified;
        HeroType[0] = hero0;
        HeroType[1] = hero1;
        if (mode == GameMode.Lanes) BuildLanes();
        foreach (var c in Col)
        {
            c.Food = Config.StartFood;
            var nest = W.Nests[c.Team].Pos;
            var toCenter = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - nest).Normalized();
            c.QueenId = Spawn(c.Team, AntType.Queen, nest + toCenter * 3.4f, toCenter.AngleD());
            // a founding colony: the queen and a handful of workers on bare soil; the nest is theirs to dig
            for (int i = 0; i < (mode == GameMode.Lanes ? 12 : Config.StartWorkers); i++) Spawn(c.Team, AntType.Worker, nest + Rng.InDisk(5), Rng.R(0, DMath.Tau));
            AddChamber(c.Team, ChamberKind.Royal, FoundingWork, false);
            RecountCapacity(c);
            if (mode == GameMode.Lanes) { FoundInstantly(c.Team); SetupLaneColony(c); continue; }
            if (fortified) SpawnNestGuardians(c.Team);
        }
        if (W.FirstNest) BeginExpedition();
        foreach (var camp in W.Camps)
        {
            if (mode == GameMode.Lanes && LaneReserved(camp.Pos)) continue; // the lanes and objective grounds are kept clear; bugs live in the jungle between them
            for (int i = 0; i < camp.Bugs.Length; i++)
            {
                var (type, level) = camp.Bugs[i];
                var off = camp.Bugs.Length == 1 ? Vector2.Zero : MathX.Dir(camp.Facing + i * DMath.Tau / camp.Bugs.Length) * 3.5f;
                int id = Spawn(Config.Wild, type, camp.Pos + off, camp.Facing + DMath.Pi, level);
                Ants[id].State = AntState.Idle;
                Ants[id].Home = camp.Pos;
            }
        }
    }

    void ApplyLevel(ref Ant a)
    {
        var st = Config.Of(a.Type);
        float frac = a.MaxHp > 0 ? a.Hp / a.MaxHp : 1;
        a.MaxHp = st.Hp * Config.HpMul(a.Type, a.Level);
        a.DmgMul = Config.DmgMul(a.Type, a.Level);
        a.Size = st.Size * Config.SizeMul(a.Type, a.Level);
        a.SpeedMul = 1;
        if (a.Team < 2 && Config.IsWarrior(a.Type))
        {
            // forge: bigger, harder-biting, slower; chitin: tougher for everyone
            a.MaxHp *= (1 + 0.12f * a.Forge) * (1 + 0.18f * Col[a.Team].T(Upgrade.Chitin));
            a.DmgMul *= 1 + 0.22f * a.Forge;
            a.Size *= 1 + 0.1f * a.Forge;
            a.SpeedMul = 1 - 0.08f * a.Forge;
            var race = Col[a.Team].Race;
            if (race == Race.BlackGarden) a.MaxHp *= Config.BlackWarriorHp;
            if (a.Type == AntType.Gunner) a.DmgMul *= race == Race.BlackGarden ? Config.BlackAcidMul : Config.RedGunnerMul;
        }
        if (a.Stage > 0)
        {
            // each moult: tougher, harder-biting, a little bigger (the mandibles most of all, in the shader)
            a.MaxHp *= 1 + Config.StageHp * a.Stage;
            a.DmgMul *= 1 + Config.StageBite * a.Stage;
            a.Size *= 1 + Config.StageSize * a.Stage;
        }
        if (Config.IsHero(a.Type))
        {
            float m = Config.HeroTierMul(a.Type, a.HeroTier);
            a.MaxHp *= m;
            a.DmgMul *= m;
        }
        if (a.Type == AntType.Queen && a.Team < 2)
        {
            int q = Col[a.Team].T(Upgrade.Queen) - 1;
            a.MaxHp *= 1 + 0.3f * q;
            a.DmgMul *= 1 + 0.25f * q;
            a.Size *= 1 + 0.12f * q;
        }
        MutationStats(ref a);
        if (a.Guardian) ApplyGuardianStats(ref a);
        if (a.Boss) { a.MaxHp *= Mode == GameMode.Lanes ? Lane.BossHp : Expedition.StagHp; a.DmgMul *= Lane.BossDmg; a.Size *= 1.35f; }
        if (Mode == GameMode.Lanes && a.Team < 2) ApplyLaneStats(ref a);
        a.Hp = a.MaxHp * frac;
    }

    // ------------------------------------------------------------------ research
    public ResearchResult CanResearch(int team, Upgrade u)
    {
        var c = Col[team];
        // the campaign's rival keeps the queen its level gave it: the levels were tuned on her rank, and with remains as
        // organic matter a raiding rival would otherwise ascend on the player's dead
        if (Mission != null && (team == 0 ? !Mission.AllowsResearch(u, c.T(u) + 1) : Mission.Episode < 3 || u == Upgrade.Queen && OrganicRules)) return ResearchResult.Locked;
        if (!c.Alive) return ResearchResult.Dead;
        if (Mode == GameMode.Lanes) return ResearchResult.Locked; // heroes buy upgrades from the shop instead
        if (!c.Founded) return ResearchResult.Locked;
        int next = c.T(u) + 1;
        if (next > Config.MaxTier(u)) return ResearchResult.Maxed;
        if (c.T(Upgrade.Queen) < Config.QueenNeeded(u, next)) return ResearchResult.Locked;
        if (c.Researching) return ResearchResult.Busy;
        if (c.Food < ResearchFood(u, next)) return ResearchResult.NoFood;
        if (c.Organic < ResearchOrganic(u, next)) return ResearchResult.NoOrganic;
        return ResearchResult.Ok;
    }

    public ResearchResult StartResearch(int team, Upgrade u, Vector2? at = null)
    {
        var r = CanResearch(team, u);
        if (r != ResearchResult.Ok) return r;
        var c = Col[team];
        // a new upgrade room must be placed within reach of the tunnels; an existing one is enlarged in place
        if (ChamberOf(team, KindOf(u)) == null && at.HasValue && PlaceCheck(team, at.Value) != DigResult.Ok) return ResearchResult.Locked;
        int next = c.T(u) + 1;
        float cost = ResearchFood(u, next);
        c.Food -= cost;
        c.Spent += cost;
        c.Organic -= ResearchOrganic(u, next);
        c.Researching = true;
        c.ResearchKind = u;
        c.ResearchProgress = 0;
        c.ResearchTime = Config.ResearchTime(u, next);
        // the upgrade is a room: workers dig it (or enlarge it) and the research runs at the pace of their digging
        var k = KindOf(u);
        var ch = ChamberOf(team, k) ?? AddChamber(team, k, 0, false, at);
        float tunnel = ch.Built ? 0 : ChamberPos(team, ch).DistanceTo(NodePos(team, ch.Parent)) * TunnelWorkPerUnit;
        ch.Done = 0;
        ch.Work = c.ResearchTime * 2.5f + tunnel; // four diggers beat the old research time a little; two match it
        return ResearchResult.Ok;
    }

    public void CancelResearch(int team)
    {
        var c = Col[team];
        if (!c.Researching) return;
        float cost = ResearchFood(c.ResearchKind, c.T(c.ResearchKind) + 1);
        c.Organic += ResearchOrganic(c.ResearchKind, c.T(c.ResearchKind) + 1);
        c.Food += cost;
        c.Spent -= cost;
        c.Researching = false;
        var ch = ChamberOf(team, KindOf(c.ResearchKind));
        if (ch != null) { ch.Work = ch.Done; if (!ch.Built) ch.Work = ch.Done = 0; } // an unbuilt room stays a hole in the plan
    }

    /// What the next tier costs: the queen grows on organic matter alone (outside the campaign), everything else on food.
    public float ResearchFood(Upgrade u, int next) => OrganicRules && u == Upgrade.Queen ? 0 : Config.ResearchCost(u, next);
    public float ResearchOrganic(Upgrade u, int next) => OrganicRules && u == Upgrade.Queen ? Config.QueenOrganic(next) : 0;

    void TickResearch(Colony c, float dt)
    {
        if (!c.Researching || !c.Alive) return;
        var ch = ChamberOf(c.Team, KindOf(c.ResearchKind));
        c.ResearchProgress = ch == null ? c.ResearchTime : ch.Progress * c.ResearchTime;
        if (ch != null && ch.Digging) return;
        c.Researching = false;
        var u = c.ResearchKind;
        c.Tier[(int)u]++;
        if (ch != null) { ch.Built = true; ch.Tier = c.Tier[(int)u]; }
        // chitin hardens every warrior; royal jelly grows the queen
        if (u == Upgrade.Chitin || u == Upgrade.Queen)
            for (int i = 0; i < High; i++)
            {
                ref var a = ref Ants[i];
                if (a.Alive && a.Team == c.Team && (Config.IsWarrior(a.Type) || a.Type == AntType.Queen)) ApplyLevel(ref a);
            }
        Events.Add(new SimEvent { Kind = EvKind.ResearchDone, Pos = W.Nests[c.Team].Pos, Team = c.Team, Upgrade = u, Tier = c.T(u) });
    }

    /// Experience. Outside the campaign only eating teaches (Carrion.cs: Nourish); a kill alone makes no one stronger.
    void GrantXp(int id, float xp, bool eaten = false)
    {
        // outside the campaign only eating teaches; the campaign's squads still rank up in battle (eating evolves them on top)
        if (EatRules && !eaten && Mission == null) return;
        ref var a = ref Ants[id];
        if (a.CharmP1 > 0 || a.Summoned) return; // a borrowed mind or a callow learns nothing
        // the campaign's rival heroes grow only so far, from kills or remains: its levels set the rival's strength
        if (Mission != null && a.Team == Config.Enemy && Config.IsHero(a.Type) && a.Level >= Config.RivalHeroCap) return;
        if (a.SquadP1 > 0) { SquadXp(Squads[a.SquadP1 - 1], xp); return; }
        int max = Config.MaxLevelOf(a.Type);
        if (!a.Alive || !Config.IsWarrior(a.Type) || a.Level >= max) return;
        a.Xp += xp;
        while (a.Level < max && a.Xp >= Config.XpFor(a.Type, a.Level + 1))
        {
            a.Level++;
            ApplyLevel(ref a);
            a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * 0.35f); // promotion heals
            if (Config.IsHero(a.Type))
            {
                a.SkPts++; // a general's level-up is a skill point
                // the campaign's scripted rival has no computer player to spend it: it learns on the spot
                if (Mission != null && a.Team == Config.Enemy) for (int s = BestLearn(a); s >= 0 && a.SkPts > 0; s = BestLearn(a)) LearnSkill(id, s);
            }
            Events.Add(new SimEvent { Kind = EvKind.LevelUp, Pos = a.Pos, Team = a.Team, Type = a.Type, Who = id + 1 });
        }
    }

    // ------------------------------------------------------------------ squads
    public Squad NewSquad(int team, AntType type)
    {
        var s = new Squad { Id = Squads.Count, Team = team, Type = type, Size = Config.SquadSize(type), Forge = Col[team].T(Upgrade.Mandibles) };
        Squads.Add(s);
        return s;
    }

    void Join(Squad s, int id)
    {
        ref var a = ref Ants[id];
        a.SquadP1 = s.Id + 1;
        a.Forge = s.Forge;
        a.AutoEat = s.AutoEat;
        if (a.Level != s.Level) { a.Level = s.Level; ApplyLevel(ref a); a.Hp = a.MaxHp; }
        s.Members.Add(id);
    }

    /// Groups freshly spawned ants into squads of their type, as the brood chamber would (tests and tours).
    public void FormSquads(List<int> ids)
    {
        var open = new Dictionary<(int, AntType), Squad>();
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive || a.SquadP1 > 0 || !Config.InSquads(a.Type)) continue;
            if (!open.TryGetValue((a.Team, a.Type), out var s) || s.Count >= s.Size) open[(a.Team, a.Type)] = s = NewSquad(a.Team, a.Type);
            Join(s, id);
        }
    }

    public Squad SquadOf(int id) => id >= 0 && id < High && Ants[id].Alive && Ants[id].SquadP1 > 0 ? Squads[Ants[id].SquadP1 - 1] : null;

    /// Experience goes to the squad; on promotion every member ranks up and is partly healed.
    void SquadXp(Squad s, float xp)
    {
        if (!s.Alive || s.Level >= Config.MaxLevel) return;
        s.Xp += xp;
        while (s.Level < Config.MaxLevel && s.Xp >= Config.SquadXp(s.Level + 1))
        {
            s.Level++;
            foreach (int m in s.Members)
            {
                ref var a = ref Ants[m];
                a.Level = s.Level;
                ApplyLevel(ref a);
                a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * 0.35f);
            }
            Events.Add(new SimEvent { Kind = EvKind.LevelUp, Pos = Centroid(s), Team = s.Team, Type = s.Type });
        }
    }

    public Vector2 Centroid(Squad s)
    {
        var c = Vector2.Zero;
        foreach (int m in s.Members) c += Ants[m].Pos;
        return s.Count > 0 ? c / s.Count : c;
    }

    /// Sends the squads of these ants home to heal and refill their ranks.
    public void CmdReinforce(List<int> ids)
    {
        var done = new HashSet<int>();
        foreach (int id in ids)
        {
            var s = SquadOf(id);
            if (s == null || !done.Add(s.Id)) continue;
            s.Recalled = true;
            var nest = NestEntrance(s.Team);
            var toCenter = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - nest).Normalized();
            CmdMove(s.Members, nest + toCenter * 10, false);
        }
    }

    /// Out of combat near its own nest, a squad heals and re-hatches its fallen, one recruit at a time, paid in food.
    void TickSquads(float dt)
    {
        foreach (var s in Squads)
        {
            if (!s.Alive) continue;
            var col = Col[s.Team];
            var nest = NestEntrance(s.Team);
            bool fighting = false;
            foreach (int m in s.Members) if (Ants[m].State == AntState.Engage || Ants[m].DamagedT > 0) { fighting = true; break; }
            var at = Centroid(s);
            if (fighting || !col.Alive || at.DistanceTo(nest) > Config.ReinforceRadius) continue;
            foreach (int m in s.Members) { ref var a = ref Ants[m]; a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * 0.08f * dt); }
            if (s.Count >= s.Size) { if (s.Recalled) { s.Recalled = false; Events.Add(new SimEvent { Kind = EvKind.SquadReady, Pos = at, Team = s.Team, Type = s.Type }); } continue; }
            s.RecruitT -= dt;
            if (s.RecruitT > 0) continue;
            var st = Config.Of(s.Type);
            if (col.PopUsed + st.Pop > col.PopCap)
            {
                if (s.Recalled) Events.Add(new SimEvent { Kind = EvKind.PopBlocked, Team = s.Team }); // the alert has its own cooldown
                s.RecruitT = 1f;
                continue;
            }
            float protein = OrganicCost(Config.KindOf(s.Type)) / Config.SquadSize(s.Type);
            if (col.Food < st.Cost || col.Organic < protein) { s.RecruitT = 1f; continue; }
            col.Food -= st.Cost;
            col.Organic -= protein;
            col.Spent += st.Cost;
            int id = Spawn(s.Team, s.Type, nest + Rng.InDisk(1.2f), Rng.R(0, DMath.Tau));
            if (id < 0) continue;
            Join(s, id);
            ref var r = ref Ants[id];
            r.Goal = at + Rng.InDisk(3);
            r.State = AntState.Move;
            col.PopUsed += st.Pop;
            s.RecruitT = st.BuildTime * 0.8f;
            Events.Add(new SimEvent { Kind = EvKind.Spawn, Pos = nest, Team = s.Team, Type = s.Type });
        }
    }

    /// XP needed for the next level (0 when maxed).
    public static float NextXp(in Ant a) => a.Level >= Config.MaxLevelOf(a.Type) ? 0 : Config.XpFor(a.Type, a.Level + 1);

    public Vector2 NestEntrance(int team) => W.Nests[team].Pos;

    public bool Valid(int id, int serial) => id >= 0 && id < High && Ants[id].Alive && Ants[id].Serial == serial;

    public int Spawn(int team, AntType type, Vector2 p, float heading, int level = 1)
    {
        int id;
        if (_free.Count > 0) id = _free.Pop();
        else if (High < Config.MaxAnts) id = High++;
        else return -1;
        Ants[id] = new Ant
        {
            Alive = true, Serial = ++_serial, Team = team, Type = type, Pos = p, Heading = heading,
            State = type == AntType.Worker ? AntState.Forage : AntState.Idle, Target = -1, Anchor = p, Home = p, Think = Rng.F() * 0.3f,
            Gait = Rng.F(), Level = level, Xp = Config.XpFor(type, level), BuffMul = 1,
            HeroTier = team < 2 && Config.IsHero(type) ? Col[team].T(Upgrade.Queen) : 0,
            Forge = team < 2 && Config.IsWarrior(type) ? Col[team].T(Upgrade.Mandibles) : 0,
            Alt = Config.CruiseAlt(type), AbilityCd = Rng.R(1, 3),
        };
        ApplyLevel(ref Ants[id]);
        Ants[id].Hp = Ants[id].MaxHp;
        InitSkills(ref Ants[id]);
        Ants[id].AutoEat = Config.IsHero(type); // generals eat to level; fighters leave their kills for the queen until told
        Ants[id].PrevPos = p; Ants[id].PrevHeading = heading; Ants[id].PrevAlt = Ants[id].Alt;
        if (team < 2) Col[team].Count[(int)type]++;
        else WildCount++;
        return id;
    }

    // ------------------------------------------------------------------ commands
    public void CmdMove(List<int> ids, Vector2 p, bool attack, int layer = 0)
    {
        if (Mission?.Confined == true && layer == 0) p = Expedition.Constrain(p);
        if (layer > 0 && (layer > 2 || !Col[layer - 1].Founded)) layer = 0; // no such underground yet
        float spacing = 0;
        foreach (int id in ids) spacing = Mathf.Max(spacing, Ants[id].Size);
        spacing *= 0.95f;
        int k = 0;
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive || a.Type == AntType.Queen) continue;
            var off = k == 0 ? Vector2.Zero : MathX.Dir(k * 2.39996f) * spacing * Mathf.Sqrt(k) * 0.9f;
            k++;
            a.Goal = ClampMap(p + off);
            a.GoalLayer = (byte)layer;
            if (a.DigP1 > 0) a.DigP1 = 0; // an ordered worker drops its shovel
            if (a.MutaP1 > 0) DropMutagen(id, ref a);
            a.Nurse = false;
            if (a.FeastOrder) { a.FeastOrder = false; a.FoodTarget = null; }
            a.State = attack ? AntState.AttackMove : AntState.Move;
            a.Forced = false;
            a.Target = -1;
            a.Assigned = null;
            a.Stuck = 0;
        }
    }

    public void CmdAttack(List<int> ids, int target)
    {
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive || a.Type == AntType.Queen || !CanReach(a, Ants[target])) continue; // a wasp in the air: only flyers
            a.State = AntState.Engage;
            a.Prev = AntState.Idle;
            a.FeastOrder = false;
            a.Target = target;
            a.TargetSerial = Ants[target].Serial;
            a.Forced = true;
            a.Anchor = Ants[target].Pos;
        }
    }

    public void CmdGather(List<int> ids, FoodSource f)
    {
        var others = new List<int>();
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive) continue;
            if (a.Type != AntType.Worker) { others.Add(id); continue; }
            if (a.MutaP1 > 0) DropMutagen(id, ref a);
            a.Nurse = false;
            a.Assigned = f;
            a.FoodTarget = f;
            a.State = a.Carrying ? AntState.Return : AntState.Gather;
        }
        if (others.Count > 0) CmdMove(others, f.Pos, false);
    }

    public void CmdStop(List<int> ids, bool hold)
    {
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive || a.Type == AntType.Queen) continue;
            if (a.MutaP1 > 0) DropMutagen(id, ref a);
            a.Nurse = false;
            a.Assigned = null;
            a.Forced = false;
            a.Anchor = a.Pos;
            a.State = a.Type == AntType.Worker && !hold ? AntState.Forage : (hold ? AntState.Hold : AntState.Idle);
        }
    }

    public QueueResult Enqueue(int team, ProdKind kind)
    {
        var r = CanEnqueue(team, kind);
        if (r != QueueResult.Ok) return r;
        var c = Col[team];
        float cost = Config.ProdCost(kind);
        c.Food -= cost;
        c.Spent += cost;
        if (Mission != null && team == 0 && Config.IsHeroKind(kind))
        {
            // the campaign's heroes come only from a royal larva (Mutation.RoyalLarva)
            LayRoyalLarva(team, Config.UnitOf(kind), Mutations.LarvaNeedLater, Mutations.CocoonTimeLater);
            return QueueResult.Ok;
        }
        c.Organic -= OrganicCost(kind);
        c.Queue.Add(new ProdItem { Kind = kind, Time = Config.ProdTime(kind) });
        return QueueResult.Ok;
    }

    /// The same checks as Enqueue without changing anything (multiplayer predicts results before sending).
    public QueueResult CanEnqueue(int team, ProdKind kind)
    {
        if (Mission != null && (team == 0 ? !Mission.AllowsProd(kind) : Mission.Episode < 3)) return QueueResult.Locked;
        var c = Col[team];
        if (!c.Alive) return QueueResult.Dead;
        if (Mode == GameMode.Lanes) return QueueResult.Locked; // the mound sends its waves on its own
        if (!c.Founded || kind == ProdKind.Chamber) return QueueResult.Locked; // no brood before the nest; chambers are dug, not laid
        if (ChamberCount(team, ChamberKind.Brood, true) == 0) return QueueResult.Locked;
        if (Mission != null && team == 0 && Config.IsHeroKind(kind)) return CanLayHero(c, kind);
        if (!Config.Unlocked(kind, c.Tier)) return QueueResult.Locked;
        if (Config.IsHeroKind(kind) && (c.Count[(int)Config.UnitOf(kind)] > 0 || c.Queue.Exists(q => q.Kind == kind))) return QueueResult.HeroExists;
        if (c.Queue.Count >= Config.QueueLimit) return QueueResult.QueueFull;
        if (kind == ProdKind.Chamber && c.PopCap + (c.PendingChambers() + 1) * Config.ChamberPop > Config.MaxPopCap) return QueueResult.MaxChambers;
        if (c.Food < Config.ProdCost(kind)) return QueueResult.NoFood;
        if (c.Organic < OrganicCost(kind)) return QueueResult.NoOrganic; // larvae need protein, not just sugar
        return QueueResult.Ok;
    }

    /// Food piles shrink as they are eaten; their collision shrinks with them, on the tick (never per frame).
    public static float FoodScale(FoodSource f) => Mathf.Lerp(0.35f, 1f, Mathf.Sqrt(Mathf.Max(0, f.Amount) / f.Max));

    static void ShrinkBlock(FoodSource f)
    {
        if (f.Block == null) return;
        f.Block.R = f.Alive ? f.Block.BaseR * FoodScale(f) : 0;
    }

    /// Hash of the whole simulation state; peers compare it to detect a desync.
    public uint Checksum()
    {
        uint h = 2166136261u;
        void Mix(uint v) { h = (h ^ v) * 16777619u; }
        uint F(float v) => System.BitConverter.SingleToUInt32Bits(v);
        Mix((uint)Ticks);
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive) continue;
            Mix((uint)i); Mix(F(a.Pos.X)); Mix(F(a.Pos.Y)); Mix(F(a.Hp)); Mix(F(a.Heading)); Mix((uint)a.State); Mix((uint)a.Level); Mix(a.Layer); Mix(F(a.ChargeT)); Mix(F(a.BlockT)); Mix(F(a.PoisonT)); Mix(F(a.Evo)); Mix((uint)a.Stage); Mix(F(a.PinT)); Mix(F(a.Alt));
            Mix((uint)a.Team); Mix((uint)(a.Sk0 | a.Sk1 << 8 | a.Sk2 << 16 | a.Sk3 << 24)); Mix(a.SkPts); Mix(F(a.AbilityCd)); Mix(F(a.CdW)); Mix(F(a.CdE)); Mix(F(a.CdR)); Mix(F(a.CharmT)); Mix(F(a.LifeT)); Mix(F(a.FrenzyT)); Mix(F(a.HasteT)); Mix(F(a.SlowT)); Mix(F(a.MarkT)); Mix(a.AutoEat ? 1u : 0u);
        }
        foreach (var c in Col)
        {
            Mix(F(c.Food)); Mix((uint)c.Queue.Count); Mix((uint)c.PopCap); Mix(F(c.SugarT)); Mix(F(c.JuiceT)); Mix(F(c.ProteinT));
            Mix(c.Founded ? 1u : 0u); Mix(F(c.FoodCap));
            foreach (var ch in c.Chambers) { Mix((uint)ch.Kind); Mix(F(ch.Done)); Mix(ch.Built ? 1u : 0u); }
            Mix((uint)c.Remains); Mix(F(c.MournT)); Mix((uint)c.Buried); Mix(F(c.Organic));
            Mix((uint)c.Mutagen); Mix((uint)c.Mutations); Mix((uint)c.WebRank); Mix((uint)c.Instability); Mix((uint)c.LarvaStage); Mix(F(c.LarvaFed)); Mix(F(c.LarvaHp)); Mix(F(c.CocoonT));
        }
        foreach (var mg in Mutagens) { Mix(F(mg.Pos.X)); Mix(F(mg.Pos.Y)); Mix(F(mg.Done)); Mix((uint)(mg.Carrier + 1)); Mix(mg.Home ? 1u : 0u); }
        Mix(Fortified ? 1u : 0u);
        Mix(Loadouts.Hash); // every machine must have the same data/loadouts.json
        if (Mode == GameMode.Lanes)
        {
            for (int t = 0; t < 2; t++) { Mix((uint)HeroId[t]); Mix(F(HeroRespawn[t])); for (int k = 0; k < 4; k++) Mix((uint)Items[t, k]); }
            for (int t = 0; t < 2; t++) Mix(F(JellyCd[t]));
            Mix(HerdHere ? 1u : 0u); Mix(F(HerdBar)); Mix((uint)HerdTeam); Mix(F(HerdNext)); Mix((uint)StagId); Mix((uint)StagTeam); Mix(F(StagNext));
        }
        Mix((uint)W.Food.Count);
        foreach (var f in W.Food) if (f.Kind == FoodKind.Corpse) { Mix(F(f.Pos.X)); Mix(F(f.Pos.Y)); Mix(F(f.Amount)); Mix((uint)f.ClaimTeam); Mix((uint)f.Queued); }
        foreach (var s in Squads) { Mix((uint)s.Count); Mix((uint)s.Level); Mix(F(s.Xp)); }
        return h;
    }

    public void CancelLast(int team)
    {
        var c = Col[team];
        if (c.Queue.Count == 0) return;
        var q = c.Queue[^1];
        c.Queue.RemoveAt(c.Queue.Count - 1);
        c.Food += Config.ProdCost(q.Kind);
        c.Organic += OrganicCost(q.Kind);
        c.Spent -= Config.ProdCost(q.Kind);
    }

    // ------------------------------------------------------------------ tick
    public void Tick(float dt)
    {
        if (dt <= 0) return;
        Time += dt;
        Events.Clear();
        Hash.Clear();
        for (int i = 0; i < High; i++)
            if (Ants[i].Alive) Hash.Insert(i, Ants[i].Pos);

        for (int i = 0; i < High; i++)
        {
            ref var p = ref Ants[i];
            p.PrevPos = p.Pos; p.PrevHeading = p.Heading; p.PrevAlt = p.Alt;
        }
        Ticks++;
        foreach (var c in Col) { TickColony(c, dt); TickResearch(c, dt); }
        TickShowers();
        TickWebs(dt);
        TickPools(dt);
        TickZones(dt);
        TickSquads(dt);
        TickNest(dt);
        TickMutagen();
        foreach (var c in Col) TickLarva(c, dt);
        if (Mode == GameMode.Lanes) TickLanes(dt);
        for (int i = 0; i < High; i++)
            if (Ants[i].Alive) TickAnt(i, dt);
        Ph.Update(dt);
        TickCarrion(dt);
        TickExpedition(dt);
    }

    void TickColony(Colony c, float dt)
    {
        c.PopUsed = 0;
        for (int t = 0; t < c.Count.Length; t++) c.PopUsed += c.Count[t] * Config.Stats[t].Pop;
        c.QueenAlarm -= dt;
        // the queen's own body reserves: a trickle that lets even a starved colony raise new workers
        if (c.Alive && c.Count[(int)AntType.Worker] < 8) { c.Food += 0.6f * dt; }
        c.SugarT = Mathf.Max(0, c.SugarT - dt);
        c.JuiceT = Mathf.Max(0, c.JuiceT - dt);
        c.ProteinT = Mathf.Max(0, c.ProteinT - dt);
        c.MournT = Mathf.Max(0, c.MournT - dt);
        if (!c.Alive || c.Queue.Count == 0) { c.PopBlocked = false; return; }
        if (!c.Founded || c.QueenId < 0 || !Ants[c.QueenId].Alive || Ants[c.QueenId].Layer != NestLayer(c.Team) || ChamberCount(c.Team, ChamberKind.Brood, true) == 0) return;
        if (Ants[c.QueenId].Pos.DistanceTo(ChamberPos(c.Team, Royal(c.Team))) > Royal(c.Team).Radius) return;
        var item = c.Queue[0];
        if (item.Kind != ProdKind.Chamber && c.PopUsed + Config.ProdPop(item.Kind) > c.PopCap)
        {
            if (!c.PopBlocked) Events.Add(new SimEvent { Kind = EvKind.PopBlocked, Team = c.Team });
            c.PopBlocked = true;
            return;
        }
        c.PopBlocked = false;
        // greater queens lay faster; protein feeds the larvae; each race has its own brood rhythm
        float brood = Config.BroodSpeed(c.T(Upgrade.Queen)) * (c.ProteinT > 0 ? 1.3f : 1f);
        if (c.Race == Race.RedFire) brood *= Config.RedBrood;
        if (c.Team == 0 && Has(0, Mutation.SwiftBrood)) brood *= Mutations.BroodMul;
        else brood *= item.Kind == ProdKind.Worker ? Config.BlackWorkerBrood : Config.BlackSquadBrood;
        item.Progress += dt * brood;
        if (item.Progress < item.Time) return;
        c.Queue.RemoveAt(0);
        var nest = NestEntrance(c.Team);
        if (item.Kind == ProdKind.Chamber)
        {
            c.PopCap = Mathf.Min(Config.MaxPopCap, c.PopCap + Config.ChamberPop);
        }
        else
        {
            var type = Config.UnitOf(item.Kind);
            var squad = Config.InSquads(type) ? NewSquad(c.Team, type) : null;
            var squadGoal = c.HasRally ? c.Rally : nest + (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - nest).Normalized() * 10 + Rng.InDisk(5);
            for (int n = 0; n < Config.SquadSize(type); n++)
            {
            int id = Hatch(c, type, n);
            if (id >= 0)
            {
                if (squad != null) Join(squad, id);
                c.Produced++;
                if (Mission != null && c.Team == 0)
                {
                    if (type == AntType.Worker) Mission.WorkersHatched++;
                    if (type == AntType.Soldier) Mission.SoldiersHatched++;
                }
                ref var a = ref Ants[id];
                if (type != AntType.Worker)
                {
                    var toCenter = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - nest).Normalized();
                    a.Goal = squadGoal + (squad != null ? MathX.Dir(n * 2.39996f) * 1.4f * Mathf.Sqrt(n) : Rng.InDisk(3));
                    a.GoalLayer = 0;
                    a.State = AntState.Move;
                }
                else if (c.HasRally && !W.Blocked(c.Rally, 0))
                {
                    var f = NearestFood(c.Rally, 12, null);
                    if (f != null) { a.Assigned = f; a.FoodTarget = f; a.State = AntState.Gather; }
                }
                Events.Add(new SimEvent { Kind = EvKind.Spawn, Pos = nest, To = a.Pos, HatchP1 = id + 1, Team = c.Team, Type = type });
            }
            }
        }
        Events.Add(new SimEvent { Kind = EvKind.ProdDone, Pos = nest, Team = c.Team, Prod = item.Kind });
    }

    /// New ants hatch in a brood chamber and find their own way up the shaft (or at the entrance before the nest exists).
    int Hatch(Colony c, AntType type, int n)
    {
        var nest = NestEntrance(c.Team);
        int id;
        if (c.Founded && ChamberCount(c.Team, ChamberKind.Brood, true) > 0)
        {
            NestChamber room = null;
            int pick = Rng.I(0, ChamberCount(c.Team, ChamberKind.Brood, true));
            foreach (var ch in c.Chambers) if (ch.Kind == ChamberKind.Brood && ch.Built && pick-- == 0) room = ch;
            var p = ChamberPos(c.Team, room) + Rng.InDisk(1.2f);
            id = Spawn(c.Team, type, p, Rng.R(0, DMath.Tau));
            if (id >= 0) { Ants[id].Layer = (byte)NestLayer(c.Team); Ants[id].Anchor = p; }
        }
        else id = Spawn(c.Team, type, nest + Rng.InDisk(1.2f + n * 0.3f), Rng.R(0, DMath.Tau));
        return id;
    }

    float _nextShower = 150;

    /// Crumbs fall from the picnic table above, always in mirrored pairs so neither side is favoured.
    void TickShowers()
    {
        if (Mission?.Confined == true) return;
        if (Time < _nextShower) return;
        _nextShower = Time + Rng.R(70, 95);
        float S = Config.MapSize;
        for (int tries = 0; tries < 60; tries++)
        {
            var p = new Vector2(Rng.R(40, S - 40), Rng.R(40, S - 40));
            var m = new Vector2(S - p.X, S - p.Y);
            if (p.DistanceTo(m) < 30 || W.Blocked(p, 8) || W.Blocked(m, 8)) continue;
            if (p.DistanceTo(W.Nests[0].Pos) < 45 || p.DistanceTo(W.Nests[1].Pos) < 45) continue;
            int n = Rng.I(3, 6);
            for (int i = 0; i < n; i++)
            {
                var off = Rng.InDisk(5);
                float amount = Rng.R(35, 70), size = Rng.R(1.0f, 1.9f);
                if (W.Blocked(p + off, size) || W.Blocked(new Vector2(S, S) - (p + off), size)) continue;
                WorldBuilder.Spawn(W, Rng, FoodKind.Crumb, p + off, amount, size);
                WorldBuilder.Spawn(W, Rng, FoodKind.Crumb, new Vector2(S, S) - (p + off), amount, size);
            }
            Events.Add(new SimEvent { Kind = EvKind.Shower, Pos = p, Team = -1 });
            Events.Add(new SimEvent { Kind = EvKind.Shower, Pos = m, Team = -1 });
            return;
        }
    }

    // ------------------------------------------------------------------ queries
    /// Nearest enemy; ground units cannot reach targets flying high above them.
    public int FindEnemy(Vector2 p, int team, float radius, bool air = false, int layer = 0, bool flier = false)
    {
        int best = -1;
        float bestD = radius * radius;
        int r = (int)(radius / SpatialHash.Cell) + 1;
        int cx = (int)(p.X / SpatialHash.Cell), cz = (int)(p.Y / SpatialHash.Cell);
        for (int z = cz - r; z <= cz + r; z++)
        {
            if (z < 0 || z >= Hash.N) continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                if (x < 0 || x >= Hash.N) continue;
                for (int j = Hash.Head[z * Hash.N + x]; j >= 0; j = Hash.Next[j])
                {
                    ref var b = ref Ants[j];
                    if (!b.Alive || b.Team == team || b.Layer != layer) continue;
                    if (Config.Predator(b.Type) ? !flier && Airborne(b) : !air && b.Alt > 2.2f) continue;
                    float d = p.DistanceSquaredTo(b.Pos);
                    // prefer fighters over workers, workers over the queen when at equal range
                    if (b.Type == AntType.Worker)
                    {
                        // wild predators only snap at foragers that blunder close; they hunt soldiers on sight
                        if (team == Config.Wild && d > 36f) continue;
                        d *= 1.3f;
                    }
                    if (d < bestD) { bestD = d; best = j; }
                }
            }
        }
        return best;
    }

    /// `team` (a forager's colony): skips other creatures' remains when that colony leaves them for its army
    /// (its own dead are still taken to the midden).
    public FoodSource NearestFood(Vector2 p, float radius, FoodSource prefer, int team = -1)
    {
        bool leave = team is >= 0 and <= 1 && EatRules && Col[team].Remains == RemainsOrder.Leave;
        FoodSource best = null;
        float bestD = radius * radius;
        foreach (var f in W.Food)
        {
            if (!f.Alive || f.Layer != 0) continue; // a carcass inside a nest is the nurses' business
            if (leave && f.Kind == FoodKind.Corpse && f.Team != team) continue;
            float rr = radius + f.Radius;
            float d = p.DistanceSquaredTo(f.Pos);
            if (d < rr * rr && d - f.Radius * f.Radius < bestD) { bestD = d - f.Radius * f.Radius; best = f; }
        }
        return best;
    }

    static Vector2 ClampMap(Vector2 p) => new(Mathf.Clamp(p.X, 6, Config.MapSize - 6), Mathf.Clamp(p.Y, 6, Config.MapSize - 6));

    // ------------------------------------------------------------------ ant brain
    void TickAnt(int i, float dt)
    {
        ref var a = ref Ants[i];
        var st = Config.Of(a.Type);
        a.Cooldown -= dt;
        a.Think -= dt;
        a.HitFlash = Mathf.Max(0, a.HitFlash - dt * 5);
        a.DamagedT -= dt;
        a.Feasting = false;
        if (a.Hauling && a.State != AntState.Gather) a.Hauling = false;
        if (a.PoisonT > 0)
        {
            a.PoisonT -= dt;
            if (a.Type != AntType.Queen || !QueenShielded(a.Team)) a.Hp -= a.PoisonDps * dt;
            if (a.Hp <= 0) { Kill(i, a.PoisonSrc >= 0 && Ants[a.PoisonSrc].Alive ? a.PoisonSrc : -1); return; }
        }
        // fruit water: the whole colony slowly mends while it keeps coming in
        if (a.Team == 0 && a.DamagedT <= 0 && a.Hp < a.MaxHp && a.Type != AntType.Queen && Has(0, Mutation.Regrowth)) a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * Mutations.RegrowRate * dt);
        if (a.Team < 2 && Col[a.Team].JuiceT > 0 && a.DamagedT <= 0 && a.Hp < a.MaxHp && a.Type != AntType.Queen && !a.Guardian) a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * 0.015f * dt);
        if (a.Guardian) TickGuardian(ref a, dt);
        a.AttackAnim = Mathf.Max(0, a.AttackAnim - dt * 3.5f);
        Vector2 desired = Vector2.Zero;
        float speedMul = 1f;
        bool think = a.Think <= 0;
        if (think) a.Think = 0.2f + Rng.F() * 0.1f;

        if (a.Type == AntType.Queen)
        {
            if (think) a.Target = FindEnemy(a.Pos, a.Team, st.Reach + 2.5f, Config.Flies(a.Type) || Config.Ranged(a.Type), a.Layer, Config.Flies(a.Type));
            if (a.Target >= 0 && Ants[a.Target].Alive) TryBite(i, a.Target, st);
            if (Mode != GameMode.Lanes) a.Hp = Mathf.Min(a.MaxHp, a.Hp + dt * 5f); // in Lane War her guardians are her only protection
            var walk = a.Team < 2 ? QueenWalk(ref a) : Vector2.Zero;
            if (walk.LengthSquared() > 0.01f)
            {
                var gait = st; gait.Speed = 3.2f; // she only ever walks once: down into her chamber
                Move(i, ref a, gait, walk, 1f, dt);
            }
            else { a.Height = W.HF.Sample(a.Pos.X, a.Pos.Y); a.Gait += dt * 0.05f; }
            return;
        }

        if (!TickSkillState(i, ref a, dt)) return;
        a.ChargeT -= dt;
        a.BlockT -= dt;
        a.RetreatT -= dt;
        a.SpecialCd -= dt;
        if (a.PinT > 0)
        {
            // pinned to the ground by thrown silk: it struggles where it fell, and ground ants can reach it
            a.PinT -= dt;
            a.Alt = Mathf.MoveToward(a.Alt, 0.25f, dt * 12f);
            a.Speed = 0;
            a.Gait += dt * 0.8f;
            return;
        }
        if (Config.Flies(a.Type))
        {
            // flyers cruise high; wasps hang above their prey and drop only to sting; flying ants swoop to bite
            float want = Config.CruiseAlt(a.Type);
            if (a.RetreatT > 0) want += 1.8f; // a wasp that has just stung climbs out of reach
            else if (a.State == AntState.Engage && Valid(a.Target, a.TargetSerial))
            {
                ref var tt = ref Ants[a.Target];
                bool diving = a.Cooldown <= 0.3f || a.AttackAnim > 0.55f; // the moment of the sting: the one window a ground ant has
                if (a.Pos.DistanceTo(tt.Pos) < 5f) want = a.Type == AntType.Wasp ? (diving || Config.Flies(tt.Type) ? tt.Alt + 0.9f : tt.Alt + Config.HoverAlt) : Mathf.Max(tt.Alt, 1.4f);
            }
            a.Alt = Mathf.MoveToward(a.Alt, want, dt * 7f);
        }

        if (a.Root > 0)
        {
            // wrapped in silk: no walking, no biting until it tears free
            bool cutters = a.Team < 2 && Col[a.Team].T(Upgrade.SilkCutters) > 0;
            a.Root -= dt * (cutters ? 2f : 1f);
            a.Speed = 0;
            a.Gait += dt * 0.6f; // legs struggle
            return;
        }

        // skills: generals when they think; the wild bugs (and a roused boss) while they fight; anyone else with a loadout too
        if (Config.IsHero(a.Type) ? think : (a.Team == Config.Wild || a.Boss || a.CharmP1 > 0) && a.State == AntState.Engage) UnitSkills(i, ref a, st);
        a.CastReq -= dt;
        if (a.CastReq <= 0) a.CastSlot = 0;
        a.AbilityCd -= dt;
        a.BuffT -= dt;

        if (think && a.Team < 2 && a.Layer == 0 && !Config.Flies(a.Type) && !a.Guardian && a.Type != AntType.Queen)
        {
            if (a.Type == AntType.Soldier && a.Team == 0 && Has(0, Mutation.WebThrowing) && a.SpecialCd <= 0) ThrowWeb(i, ref a);
            if (a.CoverT <= 0 && a.State is AntState.Idle or AntState.Forage or AntState.Gather && a.MutaP1 == 0 && !a.Nurse && !(Valid(a.Target, a.TargetSerial) && CanReach(a, Ants[a.Target])))
            {
                int h = Hunter(a.Pos, a.Team);
                if (h >= 0) { a.CoverT = 1.2f; a.Anchor = ClampMap(a.Pos + (a.Pos - Ants[h].Pos).Normalized() * 5f); }
            }
        }
        if (a.CoverT > 0)
        {
            a.CoverT -= dt;
            desired = a.Anchor - a.Pos;
            if (desired.LengthSquared() < 0.5f) a.CoverT = 0;
            if (a.Team < 2) desired = Layering(ref a, desired);
            Move(i, ref a, st, desired, 1f, dt);
            return;
        }
        if (a.MutaP1 > 0 || a.Nurse)
        {
            // the gland and the royal larva come before everything else a worker does
            desired = a.Nurse ? NurseLogic(ref a) : MutagenLogic(i, ref a, dt, ref speedMul);
            desired = Layering(ref a, desired);
            Move(i, ref a, st, desired, speedMul, dt);
            return;
        }

        switch (a.State)
        {
            case AntState.Forage:
            case AntState.Gather:
            case AntState.Return:
                desired = WorkerLogic(i, ref a, st, dt, think, ref speedMul);
                break;
            case AntState.Move:
                if (a.FeastOrder && a.FoodTarget is { Alive: true } meal)
                {
                    // sent to eat: follow the remains (they may be dragged) and stop as soon as they are within reach
                    a.Goal = meal.Pos;
                    if (a.Pos.DistanceTo(meal.Pos) < meal.Radius + a.Size * 0.4f + 0.4f) a.Goal = a.Pos;
                }
                desired = a.Goal - a.Pos;
                if (desired.Length() < 0.9f)
                {
                    desired = Vector2.Zero;
                    a.Anchor = a.Pos;
                    a.State = a.Type == AntType.Worker ? AntState.Forage : AntState.Idle;
                }
                break;
            case AntState.AttackMove:
                if (think && a.Lane > 0 && a.LaneStep == 4 && LaneSiege(ref a)) break;
                if (think)
                {
                    int e = FindEnemy(a.Pos, a.Team, st.Aggro, Config.Flies(a.Type) || Config.Ranged(a.Type), a.Layer, Config.Flies(a.Type));
                    if (e >= 0) { Engage(ref a, e, AntState.AttackMove); break; }
                    if ((a.Team < 2 || a.Raider) && a.Layer == 0 && RaidNest(ref a)) break;
                }
                desired = a.Goal - a.Pos;
                // lane creeps pass through their waypoints rather than having to stand on them (crowds jostle)
                if (a.Lane > 0)
                {
                    if (desired.Length() < (a.LaneStep < 4 ? 9f : 3.5f) && NextLaneStep(ref a)) { desired = a.Goal - a.Pos; break; }
                    UnstickCreep(ref a, dt);
                    desired = a.Goal - a.Pos;
                }
                if (desired.Length() < 0.9f)
                {
                    // arrived at an enemy mound with nobody left outside: break in rather than stand about
                    if ((a.Team < 2 || a.Raider) && a.Layer == 0 && RaidNest(ref a)) { desired = a.Goal - a.Pos; break; }
                    desired = Vector2.Zero; a.Anchor = a.Pos; a.State = AntState.Idle;
                }
                break;
            case AntState.Idle:
            case AntState.Hold:
                if (think && a.State == AntState.Idle && a.Team < 2 && a.Layer == 0 && Config.IsWarrior(a.Type) && a.Pos.DistanceTo(W.Nests[1 - a.Team].Pos) < 10)
                {
                    // idle warriors at the enemy's door go in after the queen
                    a.Goal = W.Nests[1 - a.Team].Pos;
                    if (RaidNest(ref a)) { a.State = AntState.AttackMove; desired = a.Goal - a.Pos; break; }
                }
                if (think)
                {
                    float r = a.State == AntState.Hold ? st.Reach + a.Size * 0.5f + 1f : st.Aggro;
                    if (a.Type == AntType.Worker) r = st.Aggro;
                    int e = FindEnemy(a.Pos, a.Team, r, Config.Flies(a.Type) || Config.Ranged(a.Type), a.Layer, Config.Flies(a.Type));
                    if (e >= 0)
                    {
                        if (a.State == AntState.Hold) { a.Target = e; a.TargetSerial = Ants[e].Serial; }
                        else { Engage(ref a, e, AntState.Idle); break; }
                    }
                }
                if (a.State == AntState.Hold && Valid(a.Target, a.TargetSerial)) TryBite(i, a.Target, st);
                // a hero with nothing to fight eats at a carcass it stands by
                if (Eats(a) && (a.State == AntState.Idle || a.State == AntState.Hold) && Feast(i, ref a, dt, think, ref desired)) break;
                if (Eats(a) && desired.LengthSquared() > 0) { speedMul = 0.5f; break; } // stepping up to it
                // drift back to anchor if shoved
                if (a.State == AntState.Idle && a.Pos.DistanceTo(a.Anchor) > 2.5f) { desired = a.Anchor - a.Pos; speedMul = 0.6f; }
                // wild bugs lick their wounds at their lair, and prowl around it looking for prey
                if (a.Team == Config.Wild && a.State == AntState.Idle)
                {
                    a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * (a.Boss ? 0.01f : 0.04f) * dt); // the Old Stag mends slowly, so a retreat does not reset the fight
                    if (think && a.Hp > a.MaxHp * 0.8f && Rng.F() < 0.035f)
                    {
                        a.Goal = a.Home + Rng.InDisk(9f);
                        if (!W.Blocked(a.Goal, a.Size * 0.3f + 0.5f)) a.State = AntState.AttackMove; // pounces on anything it meets (a spot it fits in)
                    }
                }
                break;
            case AntState.Engage:
                desired = EngageLogic(i, ref a, st, think);
                if (a.ChargeT > 0) speedMul = Config.ChargeSpeed; // a stag's charge, a spider's lunge
                break;
            case AntState.Dig:
                desired = DigLogic(ref a);
                break;
        }

        if (a.Team < 2 || a.Raider) desired = Layering(ref a, desired);
        Move(i, ref a, st, desired, speedMul, dt);
    }

    void Engage(ref Ant a, int e, AntState prev)
    {
        if (a.State != AntState.Engage) a.Prev = prev;
        if (prev == AntState.Idle) a.Anchor = a.Pos;
        a.State = AntState.Engage;
        a.Target = e;
        a.TargetSerial = Ants[e].Serial;
    }

    Vector2 EngageLogic(int i, ref Ant a, UnitStats st, bool think)
    {
        if (!Valid(a.Target, a.TargetSerial))
        {
            int e = FindEnemy(a.Pos, a.Team, st.Aggro, Config.Flies(a.Type) || Config.Ranged(a.Type), a.Layer, Config.Flies(a.Type));
            if (e >= 0 && !a.Forced) { a.Target = e; a.TargetSerial = Ants[e].Serial; }
            else
            {
                a.Forced = false;
                a.State = a.Prev == AntState.AttackMove ? AntState.AttackMove : (a.Type == AntType.Worker ? AntState.Forage : AntState.Idle);
                if (a.State == AntState.Idle && a.Pos.DistanceTo(a.Anchor) > 4) { a.Goal = a.Anchor; a.State = AntState.Move; }
                return Vector2.Zero;
            }
        }
        ref var t = ref Ants[a.Target];
        if (!CanReach(a, t)) { a.Target = -1; return Vector2.Zero; } // it flew out of reach
        // leash for idle defenders and wild bugs guarding their lair
        if (a.Team == Config.Wild ? a.Pos.DistanceTo(a.Home) > 30f : !a.Forced && a.Prev == AntState.Idle && a.Pos.DistanceTo(a.Anchor) > st.Aggro * 2.2f)
        {
            a.Goal = a.Team == Config.Wild ? a.Home : a.Anchor; a.State = AntState.Move;
            return Vector2.Zero;
        }
        // switch to a closer threat now and then
        if (think && !a.Forced)
        {
            int e = FindEnemy(a.Pos, a.Team, st.Reach + 2f, Config.Flies(a.Type) || Config.Ranged(a.Type), a.Layer, Config.Flies(a.Type));
            if (e >= 0 && e != a.Target && Ants[e].Type != AntType.Queen) { a.Target = e; a.TargetSerial = Ants[e].Serial; }
        }
        float reach = st.Reach + t.Size * 0.38f + a.Size * 0.3f;
        var to = t.Pos - a.Pos;
        // hit and run: a wasp that has just stung flies off a few lengths before it turns back
        if (a.RetreatT > 0) return to.LengthSquared() < 64f ? -to : Vector2.Zero;
        if (to.Length() > reach) return to;
        if (a.ChargeT > 0) { ChargeImpact(i, ref a, st, a.Target); return Vector2.Zero; }
        a.Heading += MathX.AngleDiff(a.Heading, to.AngleD()) * 0.3f;
        TryBite(i, a.Target, st);
        return Vector2.Zero;
    }

    /// A charging stag reaches its prey: the antler toss. A lunging spider lands on its webbed prey: a heavy bite.
    void ChargeImpact(int i, ref Ant a, UnitStats st, int target)
    {
        a.ChargeT = 0;
        a.AttackAnim = 1;
        a.Cooldown = st.Cooldown;
        float lv = a.Level - 1;
        byte kind = a.ChargeKind;
        a.ChargeKind = 0;
        if (kind == 1)
        {
            var fwd = MathX.Dir(a.Heading);
            var jaws = a.Pos + fwd * (a.Size * 0.5f);
            float r = a.Size * 0.6f + 1.4f + 0.2f * lv;
            float dmg = st.Damage * a.DmgMul * Skills.Def(SkillId.AntlerCharge).N("bites", System.Math.Max(1, (int)a.ChargeRank));
            var victims = AntsNear(jaws, r).ToArray();
            Events.Add(new SimEvent { Kind = EvKind.Toss, Pos = jaws, To = jaws + fwd * Config.TossThrow, Team = a.Team, Type = a.Type, Radius = r });
            foreach (int j in victims)
            {
                ref var b = ref Ants[j];
                if (b.Team == a.Team && j != i) continue; // a general's charge spares its nestmates
                if (j == i) continue;
                if (b.Type == AntType.Queen) { Damage(j, dmg, i); continue; }
                var dir = (fwd * 0.7f + (b.Pos - a.Pos).Normalized() * 0.3f).Normalized();
                b.Pos = ClampMap(b.Pos + dir * (Config.TossThrow + 0.3f * (Config.IsHero(a.Type) ? a.ChargeRank - 1 : lv)));
                b.Root = Mathf.Max(b.Root, Skills.Def(SkillId.AntlerCharge).N("stun", System.Math.Max(1, (int)a.ChargeRank))); // on its back, legs kicking
                Damage(j, dmg, i);
            }
        }
        else if (kind == 2 && Valid(target, Ants[target].Serial))
        {
            // the lunge: a wolf spider lands on its prey jaws first
            float dmg = st.Damage * a.DmgMul * Skills.Def(SkillId.Lunge).N("bites", 1);
            Events.Add(new SimEvent { Kind = EvKind.Lunge, Pos = Ants[target].Pos, Team = a.Team, Type = a.Type });
            Damage(target, dmg, i);
        }
    }

    void TryBite(int i, int target, UnitStats st)
    {
        ref var a = ref Ants[i];
        ref var t = ref Ants[target];
        float reach = st.Reach + t.Size * 0.38f + a.Size * 0.3f + 0.3f;
        if (a.Pos.DistanceTo(t.Pos) > reach) return;
        if (!Config.Ranged(a.Type) && Mathf.Abs(a.Alt - t.Alt) > reach + 0.6f) return;
        if (a.Cooldown > 0) return;
        int bridged = Bridges.Count > 0 ? OnBridge(a) : 0;
        if (bridged is > 0 and < 3) return; // locked into the bridge: no biting until it is rank 3
        a.Cooldown = st.Cooldown * Rng.R(0.85f, 1.15f);
        a.AttackAnim = 1;
        float dmg = st.Damage * a.DmgMul * (a.BuffT > 0 ? a.BuffMul : 1f) * MoraleMul(a.Team) * Rng.R(0.85f, 1.15f);
        if (a.FrenzyT > 0) dmg *= Skills.FrenzyBite[System.Math.Clamp((int)a.FrenzyRank, 1, 3) - 1];
        bool sting = false;
        if (Loadouts.Has(a, SkillId.VenomSting, out int stingSlot) && Rank(a, stingSlot) > 0 && SkillCd(a, stingSlot) <= 0)
        {
            // venomous sting: a heavy blow that keeps burning, then the stinger climbs away (if it flies) before the swarm can answer
            float venomBites = Skills.Def(SkillId.VenomSting).N("bites", Rank(a, stingSlot));
            dmg *= Config.IsHero(a.Type) ? 1f + (venomBites - 1f) * 0.5f : venomBites + 0.3f * (a.Level - 1); // a general carries half the wasp's venom bonus (its bite is already big, and may splash)
            SetCd(ref a, stingSlot, Skills.Cooldown(SkillId.VenomSting, Rank(a, stingSlot), a.Level));
            if (Config.Flies(a.Type)) a.RetreatT = Config.WaspRetreat;
            sting = true;
            Events.Add(new SimEvent { Kind = EvKind.Sting, Pos = t.Pos, Team = a.Team, Type = a.Type });
        }
        if (a.Type == AntType.Spider && t.Root > 0) dmg *= Config.LungeDamage * 0.6f; // prey wrapped in silk cannot dodge the fangs
        if (a.Guardian && Config.IsHero(t.Type)) dmg *= 1.6f; // guardians punish heroes who dive under them
        if (a.Boss && a.Team < 2 && t.Guardian) dmg *= Lane.StagShatter; // a roused Old Stag shatters mounds
        bool fire = a.Team < 2 && Col[a.Team].Race == Race.RedFire && Config.IsWarrior(a.Type) && !Config.Ranged(a.Type);
        if (fire && a.Pos.DistanceTo(NestEntrance(a.Team)) < 40) dmg *= Config.RedHomeDamage;
        if (fire && t.Alive && t.Type != AntType.Queen)
        {
            // fire-ant venom keeps burning after the bite
            t.PoisonDps = t.PoisonT > 0 ? Mathf.Max(t.PoisonDps, dmg * Config.RedVenomDps) : dmg * Config.RedVenomDps;
            t.PoisonT = 3f;
            t.PoisonSrc = i;
        }
        MutantBite(i, ref a, ref t, dmg);
        if (Config.Ranged(a.Type)) Events.Add(new SimEvent { Kind = EvKind.Spit, Pos = a.Pos, To = t.Pos, Team = a.Team, Type = a.Type });
        else Events.Add(new SimEvent { Kind = EvKind.Bite, Pos = t.Pos, Team = a.Team, Type = a.Type });
        var tp = t.Pos;
        if (Loadouts.Has(a, SkillId.CleavingMandibles, out int cleaveSlot) && Rank(a, cleaveSlot) > 0)
        {
            // Cleaving Mandibles: the bite also catches the nearest enemies beside the victim
            var cd = Skills.Def(SkillId.CleavingMandibles);
            int rk = Rank(a, cleaveSlot), extra = (int)cd.N("targets", rk);
            float share = cd.N("share", rk);
            var beside = EnemiesNear(tp, 2.6f, a.Team, false, a.Layer).ToArray();
            System.Array.Sort(beside, (x, y) => { int c = Ants[x].Pos.DistanceSquaredTo(tp).CompareTo(Ants[y].Pos.DistanceSquaredTo(tp)); return c != 0 ? c : x.CompareTo(y); });
            foreach (int j in beside) { if (extra <= 0) break; if (j == target) continue; Damage(j, dmg * share, i); extra--; }
        }
        if (a.Type == AntType.Matron)
        {
            // the acid jet splashes over whoever stands beside the target
            foreach (int j in EnemiesNear(tp, Config.MatronSplashR, a.Team, false, a.Layer).ToArray()) if (j != target) Damage(j, dmg * Config.MatronSplash, i);
        }
        if (sting && t.Alive && t.Type != AntType.Queen)
        {
            float venom = dmg * Config.StingVenom;
            t.PoisonDps = t.PoisonT > 0 ? Mathf.Max(t.PoisonDps, venom) : venom;
            t.PoisonT = Config.StingVenomTime;
            t.PoisonSrc = i;
        }
        if (a.FrenzyT > 0) a.Hp = Mathf.Min(a.MaxHp, a.Hp + dmg * Skills.FrenzyLeech[System.Math.Clamp((int)a.FrenzyRank, 1, 3) - 1]); // it drinks what it bites
        Damage(target, dmg, i);
    }

    // ------------------------------------------------------------------ wild bug specials
    readonly List<int> _near = new();
    readonly List<WebShotFx> _shots = new();
    public readonly List<WebZone> Webs = new();
    public readonly List<Squad> Squads = new();

    /// Ground ants (not flyers) of the colonies within r of p.
    List<int> AntsNear(Vector2 p, float r, bool groundOnly = true)
    {
        _near.Clear();
        int cr = (int)(r / SpatialHash.Cell) + 1;
        int cx = (int)(p.X / SpatialHash.Cell), cz = (int)(p.Y / SpatialHash.Cell);
        for (int z = cz - cr; z <= cz + cr; z++)
        {
            if (z < 0 || z >= Hash.N) continue;
            for (int x = cx - cr; x <= cx + cr; x++)
            {
                if (x < 0 || x >= Hash.N) continue;
                for (int j = Hash.Head[z * Hash.N + x]; j >= 0; j = Hash.Next[j])
                {
                    ref var b = ref Ants[j];
                    if (!b.Alive || b.Team >= 2 || b.Layer != 0 || (groundOnly && b.Alt > 1.5f)) continue;
                    if (b.Pos.DistanceSquaredTo(p) < r * r) _near.Add(j);
                }
            }
        }
        return _near;
    }

    /// Enemies of `team` (colonies and wild bugs) within r of p.
    List<int> EnemiesNear(Vector2 p, float r, int team, bool groundOnly, int layer = 0)
    {
        _near.Clear();
        int cr = (int)(r / SpatialHash.Cell) + 1;
        int cx = (int)(p.X / SpatialHash.Cell), cz = (int)(p.Y / SpatialHash.Cell);
        for (int z = cz - cr; z <= cz + cr; z++)
        {
            if (z < 0 || z >= Hash.N) continue;
            for (int x = cx - cr; x <= cx + cr; x++)
            {
                if (x < 0 || x >= Hash.N) continue;
                for (int j = Hash.Head[z * Hash.N + x]; j >= 0; j = Hash.Next[j])
                {
                    ref var b = ref Ants[j];
                    if (!b.Alive || b.Team == team || b.Layer != layer || (groundOnly && b.Alt > 1.5f)) continue;
                    if (b.Pos.DistanceSquaredTo(p) < r * r) _near.Add(j);
                }
            }
        }
        return _near;
    }

    void TickWebs(float dt)
    {
        for (int s = _shots.Count - 1; s >= 0; s--)
        {
            var w = _shots[s];
            w.T += dt;
            if (w.T < w.Dur) continue;
            _shots.RemoveAt(s);
            Webs.Add(new WebZone { Pos = w.To, R = w.R, Life = w.Life, Max = w.Life, Root = w.Root });
            foreach (int j in AntsNear(w.To, w.R)) Ants[j].Root = Mathf.Max(Ants[j].Root, w.Root);
            Events.Add(new SimEvent { Kind = EvKind.WebLand, Pos = w.To, Team = Config.Wild, Radius = w.R });
        }
        for (int k = Webs.Count - 1; k >= 0; k--)
        {
            var z = Webs[k];
            z.Life -= dt;
            if (z.Life <= 0) { Webs.RemoveAt(k); continue; }
            // ants wading into a fresh web get stuck too
            foreach (int j in AntsNear(z.Pos, z.R)) if (Ants[j].Root <= 0) Ants[j].Root = 0.6f;
        }
    }

    public readonly List<AcidPool> Pools = new();
    public readonly List<BridgeZone> Bridges = new();
    public readonly List<ScoutZone> Scouts = new();

    void TickPools(float dt)
    {
        for (int k = Pools.Count - 1; k >= 0; k--)
        {
            var z = Pools[k];
            z.Life -= dt;
            if (z.Life <= 0) { Pools.RemoveAt(k); continue; }
            // standing in the acid keeps the burn topped up for a moment after stepping out
            foreach (int j in EnemiesNear(z.Pos, z.R, z.Team, true).ToArray())
            {
                ref var b = ref Ants[j];
                if (b.Type == AntType.Queen) continue;
                b.PoisonDps = b.PoisonT > 0 ? Mathf.Max(b.PoisonDps, z.Dps) : z.Dps;
                b.PoisonT = Mathf.Max(b.PoisonT, 1.0f);
                if (z.Src >= 0 && Ants[z.Src].Alive) b.PoisonSrc = z.Src;
                if (z.Slow > 0) { b.SlowMul = b.SlowT > 0 ? Mathf.Max(b.SlowMul, z.Slow) : z.Slow; b.SlowT = Mathf.Max(b.SlowT, 0.6f); }
            }
        }
    }

    /// Web shots in flight, for rendering: (from, to, progress 0..1).
    public IEnumerable<(Vector2 from, Vector2 to, float t)> WebShots()
    {
        foreach (var w in _shots) yield return (w.From, w.To, w.T / w.Dur);
    }

    public void Damage(int target, float dmg, int attacker)
    {
        ref var t = ref Ants[target];
        if (!t.Alive) return;
        if (t.Type == AntType.Queen && QueenShielded(t.Team)) { t.HitFlash = 0.4f; return; }
        // chitin: heroes and wild bugs shrug off a share of every blow; a beetle behind its shell wall most of it
        float armor = Config.Of(t.Type).Armor + (t.BlockT > 0 ? (t.BlockArmor > 0 ? t.BlockArmor : Config.ShellWallArmor) : 0f);
        if (t.HasteT > 0) armor += t.GuardArmor; // Trail Blaze: the line holds
        if (t.MarkT > 0) dmg *= t.MarkMul; // Scout Swarm: marked
        if (armor > 0) dmg *= 1f - Mathf.Min(armor, 0.75f);
        if (t.Guardian) dmg *= GuardianDamageMul(in t, attacker);
        t.Hp -= dmg;
        _blow = dmg / Mathf.Max(1f, t.MaxHp);
        Events.Add(new SimEvent { Kind = EvKind.Hurt, Pos = t.Pos, To = attacker >= 0 ? Ants[attacker].Pos : t.Pos, Team = t.Team, Type = t.Type,
            By = attacker >= 0 ? Ants[attacker].Type : t.Type, Who = target + 1, Amount = _blow });
        t.HitFlash = 1;
        t.DamagedT = 3f;
        if (t.Type == AntType.Queen)
        {
            Events.Add(new SimEvent { Kind = EvKind.QueenHit, Pos = t.Pos, Team = t.Team });
            Col[t.Team].QueenAlarm = 4f;
        }
        else if (t.Team < 2 && (attacker < 0 || Ants[attacker].Team != Config.Wild || t.Type != AntType.Worker))
            Events.Add(new SimEvent { Kind = EvKind.UnderAttack, Pos = t.Pos, Team = t.Team });
        if (t.Hp <= 0) { Kill(target, attacker); return; }
        // retaliate
        if (attacker >= 0 && Ants[attacker].Alive)
        {
            if (t.Type != AntType.Worker && (t.State == AntState.Idle || t.State == AntState.Move && t.Team == Config.Wild))
                Engage(ref t, attacker, AntState.Idle);
            else if (t.Type == AntType.Worker && t.State != AntState.Engage && Rng.F() < 0.5f)
            {
                // workers bite back only when cornered
                if (t.Pos.DistanceTo(Ants[attacker].Pos) < 1.6f) { t.Target = attacker; t.TargetSerial = Ants[attacker].Serial; }
            }
        }
    }

    float _blow; // the last blow as a share of its victim's health (rendering: how violently it dies)

    void Kill(int id, int killer)
    {
        ref var t = ref Ants[id];
        if (t.CharmP1 > 0) { t.Team = t.CharmP1 - 1; t.CharmP1 = 0; } // it dies one of its own colony's
        if (Mission != null && !t.Summoned) RosterKill(id, killer);
        if (t.Boss && t.Team == Config.Wild && killer >= 0 && Ants[killer].Team < 2) { Rouse(id, Ants[killer].Team, killer); return; }
        MutationDeath(id, ref t, killer);
        t.Alive = false;
        if (t.SquadP1 > 0) Squads[t.SquadP1 - 1].Members.Remove(id);
        var st = Config.Of(t.Type);
        var c = t.Team < 2 ? Col[t.Team] : null;
        if (c != null) { if (!t.Guardian && !t.Summoned) c.Count[(int)t.Type]--; if (!t.Summoned) c.Lost++; }
        else WildCount--;
        if (killer >= 0 && Ants[killer].Team < 2)
        {
            ref var k = ref Ants[killer];
            Col[k.Team].Killed++;
            // the killer earns full experience, warriors fighting alongside share a part of it
            float xp = Config.Bounty(t.Type, t.Level);
            GrantXp(killer, xp);
            int cx = (int)(t.Pos.X / SpatialHash.Cell), cz = (int)(t.Pos.Y / SpatialHash.Cell);
            for (int z = cz - 4; z <= cz + 4; z++)
            {
                if (z < 0 || z >= Hash.N) continue;
                for (int x = cx - 4; x <= cx + 4; x++)
                {
                    if (x < 0 || x >= Hash.N) continue;
                    for (int j = Hash.Head[z * Hash.N + x]; j >= 0; j = Hash.Next[j])
                        if (j != killer && Ants[j].Alive && Ants[j].Team == k.Team && Ants[j].Pos.DistanceTo(t.Pos) < (Config.IsHero(Ants[j].Type) ? 15 : 11))
                            GrantXp(j, xp * (Config.IsHero(Ants[j].Type) ? 1f : 0.4f));
                }
            }
        }
        // every death leaves a carcass where it fell, above ground or in the galleries
        var carcass = new FoodSource
        {
            Id = W.NextFoodId++, Kind = FoodKind.Corpse, Pos = t.Pos, Amount = t.Summoned ? 0 : st.CorpseFood, Max = st.CorpseFood, // a callow leaves nothing worth eating Radius = t.Size * 0.45f,
            Team = t.Team, AntType = t.Type, Heading = t.Heading, Layer = t.Layer, Natural = Mission == null, // the campaign's tuned raids keep carcasses whole until they vanish
        };
        if (carcass.Alive) { W.Food.Add(carcass); if (killer >= 0) ClaimKill(killer, carcass); }
        Events.Add(new SimEvent { Kind = EvKind.Death, Pos = t.Pos, Team = t.Team, Type = t.Type, Who = id + 1, FoodP1 = carcass.Alive ? carcass.Id + 1 : 0,
            By = killer >= 0 ? Ants[killer].Type : t.Type, Amount = killer >= 0 ? _blow : 0, To = killer >= 0 ? Ants[killer].Pos : t.Pos });
        if (Mode == GameMode.Lanes) LaneDeath(id, killer);
        if (t.Type == AntType.Queen && c != null)
        {
            c.Alive = false;
            c.Queue.Clear();
            Events.Add(new SimEvent { Kind = EvKind.QueenDead, Pos = t.Pos, Team = t.Team });
            if (Winner < 0) Winner = 1 - t.Team;
        }
        _free.Push(id);
    }

    // ------------------------------------------------------------------ foraging
    static readonly float[] SensorAngles = { -0.6f, 0f, 0.6f };

    float TrailMul(int team) => Col[team].Race == Race.BlackGarden ? Config.BlackTrailMul : Config.RedTrailMul;

    Vector2 WorkerLogic(int i, ref Ant a, UnitStats st, float dt, bool think, ref float speedMul)
    {
        // Resume an interrupted delivery before gathering another item.
        if (a.Carrying) a.State = AntState.Return;
        else if (a.State == AntState.Return) a.State = AntState.Forage;
        // bite whoever is chewing on us
        if (a.Target >= 0 && Valid(a.Target, a.TargetSerial) && a.Pos.DistanceTo(Ants[a.Target].Pos) < 2f) TryBite(i, a.Target, st);

        var nest = NestEntrance(a.Team);
        // the colony swarms to defend its queen, down the shaft if the fight is below
        if (think && Col[a.Team].QueenAlarm > 0 && a.Pos.DistanceTo(nest) < 26)
        {
            int e = FindEnemy(a.Pos, a.Team, 12, Config.Flies(a.Type) || Config.Ranged(a.Type), a.Layer, Config.Flies(a.Type));
            if (e < 0 && a.Layer == 0 && Col[a.Team].Founded) e = FindEnemy(nest, a.Team, NestReach(a.Team), true, NestLayer(a.Team));
            if (e >= 0) { a.Prev = AntState.Forage; a.State = AntState.Engage; a.Target = e; a.TargetSerial = Ants[e].Serial; a.Forced = false; return Vector2.Zero; }
        }
        if (a.State == AntState.Return && a.Burying)
        {
            // the dead go out to the midden, not down to the stores, and no trail is laid to them
            if (AtMidden(ref a)) return Vector2.Zero;
            if (a.Layer > 0) return nest - a.Pos;
            return Midden(a.Team) - a.Pos;
        }
        if (a.State == AntState.Return)
        {
            a.Deposit -= dt;
            if (a.Deposit <= 0) { a.Deposit = 0.12f; if (a.Layer == 0) Ph.Deposit(a.Team, a.Pos, 0.9f * TrailMul(a.Team)); }
            if (a.CarryKind == FoodKind.Seed) speedMul = 0.8f; // seeds are heavy
            var delivery = FoodDeliveryPoint(a.Team);
            var to = delivery.pos - a.Pos;
            if (a.Carrying && a.Layer == delivery.layer && to.Length() < (delivery.layer == 0 ? 1.8f : 1.1f))
            {
                a.Carrying = false;
                var c = Col[a.Team];
                if (a.CarryKind == FoodKind.Corpse && (CarrionRules || OrganicRules && a.CarryTeam != a.Team))
                {
                    // remains are not food: they go into the organic reserve, the colony's evolving material
                    StoreRemains(c, a.CarryType, 1);
                    Events.Add(new SimEvent { Kind = EvKind.Deliver, Pos = delivery.pos, Team = a.Team, Type = a.CarryType, Amount = 1 });
                    a.Heading += DMath.Pi;
                    if (a.Assigned != null && a.Assigned.Alive) { a.FoodTarget = a.Assigned; a.State = AntState.Gather; }
                    else { a.Assigned = null; a.FoodTarget = null; a.State = AntState.Forage; }
                    return Vector2.Zero;
                }
                float gain = Config.FoodValue(a.CarryKind, Mission != null) * c.FoodMult * (Config.Favoured(c.Race, a.CarryKind) ? Config.FoodBonus(c.Race) : 1f);
                if (c.Food + gain > c.FoodCap)
                {
                    // the stores are full: the surplus is eaten on the spot
                    gain = Mathf.Max(0, c.FoodCap - c.Food);
                    if (!c.StoreFullWarned) { c.StoreFullWarned = true; Events.Add(new SimEvent { Kind = EvKind.StoreFull, Pos = nest, Team = a.Team }); }
                }
                else if (c.Food + gain < c.FoodCap * 0.9f) c.StoreFullWarned = false;
                c.Food += gain;
                c.Gathered += gain;
                c.GatheredBy[(int)a.CarryKind] += gain;
                switch (a.CarryKind)
                {
                    case FoodKind.Sugar: c.SugarT = Mathf.Min(Config.EffectMax, c.SugarT + Config.EffectPerDelivery); break;
                    case FoodKind.Berry: c.JuiceT = Mathf.Min(Config.EffectMax, c.JuiceT + Config.EffectPerDelivery); break;
                    case FoodKind.Corpse:
                        c.ProteinT = Mathf.Min(Config.EffectMax, c.ProteinT + Config.EffectPerDelivery);
                        break;
                }
                Events.Add(new SimEvent { Kind = EvKind.Deliver, Pos = delivery.pos, Team = a.Team });
                a.Heading += DMath.Pi;
                if (a.Assigned != null && a.Assigned.Alive) { a.FoodTarget = a.Assigned; a.State = AntState.Gather; }
                else { a.Assigned = null; a.FoodTarget = null; a.State = AntState.Forage; }
                return Vector2.Zero;
            }
            // Keep the full destination below ground so Layering can follow the
            // actual gallery tree. Surface travel retains its slight meander.
            if (a.Layer > 0) return to;
            var surfaceHome = nest - a.Pos;
            return surfaceHome.Normalized() + MathX.Dir(a.Heading + DMath.Sin(Time * 3 + i) * 0.4f) * 0.25f;
        }

        // Leave by the shaft before searching or picking up food on the surface.
        if (a.Layer > 0) return nest - a.Pos;

        if (a.State == AntState.Gather && (a.FoodTarget == null || !a.FoodTarget.Alive))
        {
            a.FoodTarget = null;
            if (a.Assigned != null && !a.Assigned.Alive)
            {
                // assigned pile is gone: look for the next one nearby
                var next = NearestFood(a.Assigned.Pos, 14, null, a.Team);
                a.Assigned = next;
                a.FoodTarget = next;
            }
            if (a.FoodTarget == null) a.State = AntState.Forage;
        }

        if (a.State == AntState.Forage && think)
        {
            var f = NearestFood(a.Pos, 8.5f, null, a.Team);
            if (f != null) a.FoodTarget = f;
            else if (a.FoodTarget != null && !a.FoodTarget.Alive) a.FoodTarget = null;
        }

        if (a.FoodTarget != null && a.FoodTarget.Alive)
        {
            var f = a.FoodTarget;
            var to = f.Pos - a.Pos;
            if (f.Kind == FoodKind.Corpse && (a.Hauling || to.Length() < f.Radius + 0.7f)) return AtCarcass(i, ref a, f, st, ref speedMul);
            if (to.Length() < f.Radius + 0.7f)
            {
                f.Amount -= 1;
                ShrinkBlock(f);
                a.Carrying = true;
                a.CarryKind = f.Kind;
                a.State = AntState.Return;
                a.FoodTarget = null;
                if (a.Assigned == null && f.Amount > 3) a.Assigned = f;
                Ph.Deposit(a.Team, a.Pos, 3f * TrailMul(a.Team));
                Events.Add(new SimEvent { Kind = EvKind.Pickup, Pos = f.Pos, Team = a.Team });
                return Vector2.Zero;
            }
            return to;
        }

        // pheromone following
        float best = 0.04f; int bi = -1;
        for (int s = 0; s < 3; s++)
        {
            var sp = a.Pos + MathX.Dir(a.Heading + SensorAngles[s]) * 3.2f;
            float v = Ph.Sample(a.Team, sp);
            if (v > best) { best = v; bi = s; }
        }
        a.Wander = Mathf.Clamp(a.Wander + Rng.R(-1, 1) * dt * 6, -1.4f, 1.4f);
        float h = a.Heading + a.Wander * dt * 2.2f;
        if (bi >= 0)
        {
            h += SensorAngles[bi] * Mathf.Min(1, dt * 10);
            // trails point both ways: prefer the direction that leads away from home
            var away = (a.Pos - nest).Normalized();
            if (MathX.Dir(h).Dot(away) < -0.3f && a.Pos.DistanceTo(nest) > 12) h += MathX.AngleDiff(h, away.AngleD()) * dt * 1.5f;
        }
        var dir = MathX.Dir(h);
        float far = a.Pos.DistanceTo(nest);
        if (far > 150) dir = (dir + (nest - a.Pos).Normalized() * ((far - 150) / 40f)).Normalized();
        speedMul = 0.85f;
        return dir;
    }

    // ------------------------------------------------------------------ locomotion
    void Move(int i, ref Ant a, UnitStats st, Vector2 desired, float speedMul, float dt)
    {
        if (a.Guardian) { a.Speed = 0; return; } // rooted in its mound
        float radius = a.Size * 0.3f;
        float targetSpeed = 0;
        bool fly = Config.Flies(a.Type) || OnBridge(a) > 0; // on a living bridge, rocks are no obstacle
        if (desired.LengthSquared() > 1e-6f)
        {
            float want = desired.AngleD();
            var dest = a.Pos + desired; // when desired is a full vector this is the destination
            // look-ahead obstacle steering: slide along the rim of rocks (flyers pass over)
            var ahead = a.Pos + MathX.Dir(want) * (2.2f + radius);
            if (!fly && a.Layer == 0) foreach (var o in W.ObstaclesNear(ahead))
            {
                float rr = o.R + radius + 0.4f;
                if (o.R <= 0 || dest.DistanceTo(o.P) < o.R + 1.5f) continue; // heading for this very thing
                if (ahead.DistanceSquaredTo(o.P) < rr * rr)
                {
                    var n = (a.Pos - o.P).Normalized();
                    var tangent = new Vector2(-n.Y, n.X);
                    if (tangent.Dot(MathX.Dir(want)) < 0) tangent = -tangent;
                    want = (tangent * 1.0f + n * 0.25f).AngleD();
                    break;
                }
            }
            float diff = MathX.AngleDiff(a.Heading, want);
            float turn = (a.Type == AntType.Major ? 5f : 8f) * dt;
            a.Heading += Mathf.Clamp(diff, -turn, turn);
            if (a.Type == AntType.Worker && a.Team < 2 && Col[a.Team].SugarT > 0) speedMul *= 1.15f; // sugar energy
            if (a.Type == AntType.Worker && a.Team < 2 && a.Layer == NestLayer(a.Team)) speedMul *= Config.GalleryPace;
            targetSpeed = st.Speed * a.SpeedMul * (a.BuffT > 0 ? 1.2f : 1f) * (a.HasteT > 0 ? a.HasteMul : 1f) * (a.SlowT > 0 ? 1f - a.SlowMul : 1f) * speedMul * (Mathf.Abs(diff) > 1.3f ? 0.35f : 1f);
        }
        a.Speed = Mathf.MoveToward(a.Speed, targetSpeed, st.Speed * dt * 6);
        var pos = a.Pos + MathX.Dir(a.Heading) * a.Speed * dt;

        // separation
        Vector2 push = Vector2.Zero;
        int cx = (int)(a.Pos.X / SpatialHash.Cell), cz = (int)(a.Pos.Y / SpatialHash.Cell);
        int seen = 0;
        for (int z = cz - 1; z <= cz + 1 && seen < 10; z++)
        {
            if (z < 0 || z >= Hash.N) continue;
            for (int x = cx - 1; x <= cx + 1 && seen < 10; x++)
            {
                if (x < 0 || x >= Hash.N) continue;
                for (int j = Hash.Head[z * Hash.N + x]; j >= 0 && seen < 10; j = Hash.Next[j])
                {
                    if (j == i) continue;
                    ref var b = ref Ants[j];
                    if (!b.Alive || Config.Flies(b.Type) != fly || b.Layer != a.Layer) continue;
                    float rb = b.Size * 0.3f;
                    float min = radius + rb;
                    var d = pos - b.Pos;
                    float l2 = d.LengthSquared();
                    if (l2 >= min * min) continue;
                    seen++;
                    float l = Mathf.Sqrt(l2) + 1e-4f;
                    float w = b.Type == AntType.Queen ? 1f : (b.Size / (b.Size + a.Size));
                    if (l2 < 1e-6f) d = MathX.Dir(i * 1.7f);
                    push += d / l * (min - l) * w;
                }
            }
        }
        pos += push * 0.6f;

        if (a.Layer > 0) pos = ClampToNetwork(a.Layer - 1, pos); // underground: galleries and floors only
        else foreach (var o in W.ObstaclesNear(pos)) // hard obstacle resolution
        {
            if (fly || o.R <= 0) continue;
            float rr = o.R + radius;
            var d = pos - o.P;
            float l = d.Length();
            if (l < rr && l > 1e-4f) pos = o.P + d / l * rr;
        }
        pos = ClampMap(pos);
        float moved = pos.DistanceTo(a.Pos);
        a.Pos = Mission?.Confined == true && a.Layer == 0 ? Expedition.Constrain(pos) : pos;
        a.Gait = (a.Gait + moved / (0.55f * a.Size)) % 1f;
        a.Height = W.HF.Sample(pos.X, pos.Y);
    }
}
