using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Mutation (docs/progression.md, the owner's rules of 2026-11-15): a mutation core cut from a mutated creature and
/// carried to the queen makes her lay a NEW KIND OF UNIT, a hybrid of ant and that creature. One core, one mutation:
/// nothing is counted or saved up, and a mutation never gives the colony plain strength. The tube held strain M-7, a
/// lab line bred to take up mutagen; the creatures that fed on the spill carry it in their bodies as a glowing core.
/// The second progression, evolution (organic matter spent in the evolve tree), is in Evolve.cs.
[System.Flags]
public enum Mutation
{
    None = 0,
    Winged = 1,        // fly core: winged ants (AntType.Flyer)
    Stinger = 2,       // wasp core: the winged ants grow a wasp's venom sting (needs Winged)
    Web = 4,           // spider core: web ants (AntType.WebAnt) that fling silk and pin flyers
    Armoured = 8,      // beetle core: armoured ants (AntType.Armoured), a thick shell
    RoyalLarva = 16,   // a mutated queen's core: the royal larva, out of whose cocoon a general breaks
    // 32 is kept for the mantis ants (later; needs a newly built body)
}

public static class Mutations
{
    public static readonly Mutation[] All = { Mutation.Winged, Mutation.Stinger, Mutation.Web, Mutation.Armoured, Mutation.RoyalLarva };

    /// The unit the queen can lay after it.
    public static string Name(Mutation m) => m switch
    {
        Mutation.Winged => "Winged Ants",
        Mutation.Stinger => "Stinger Ants",
        Mutation.Web => "Web Ants",
        Mutation.Armoured => "Armoured Ants",
        Mutation.RoyalLarva => "The Royal Larva",
        _ => "",
    };

    public static string Text(Mutation m) => m switch
    {
        Mutation.Winged => "The queen lays winged ants: a fly's wings and eyes on an ant's body. They cross ground no ant can walk and fight in the air.",
        Mutation.Stinger => "The winged ants grow a wasp's sting. Every strike injects venom that keeps burning.",
        Mutation.Web => "The queen lays web ants: a spider's spinnerets on an ant. They fling silk that pins a flying enemy to the ground, where any ant can bite it.",
        Mutation.Armoured => "The queen lays armoured ants: a beetle's shell plates over an ant. Slow, and very hard to kill.",
        Mutation.RoyalLarva => "The queen can lay a royal larva in its own nursery. Fed on organic matter it spins a cocoon, and a general breaks out.",
        _ => "",
    };

    /// The creature whose core gives it.
    public static string Source(Mutation m) => m switch
    {
        Mutation.Winged => "a fly's core",
        Mutation.Stinger => "a wasp's core",
        Mutation.Web => "a spider's core",
        Mutation.Armoured => "a beetle's core",
        Mutation.RoyalLarva => "a mutated queen's core",
        _ => "",
    };

    /// A mutation that builds on another: the sting grows on the winged ants, so the fly core comes first.
    public static Mutation Needs(Mutation m) => m == Mutation.Stinger ? Mutation.Winged : Mutation.None;

    /// The caste a mutation lets the queen lay (Queen: none of its own).
    public static AntType Unit(Mutation m) => m switch
    {
        Mutation.Winged or Mutation.Stinger => AntType.Flyer,
        Mutation.Web => AntType.WebAnt,
        Mutation.Armoured => AntType.Armoured,
        Mutation.RoyalLarva => AntType.Warlord,
        _ => AntType.Queen,
    };

    /// The core a wild creature of this kind carries (skirmish lair masters).
    public static Mutation CoreOf(AntType t) => t switch
    {
        AntType.Spider => Mutation.Web,
        AntType.Wasp => Mutation.Stinger,
        AntType.Beetle or AntType.StagBeetle => Mutation.Armoured,
        _ => Mutation.None,
    };

    public static int Count(int bits) { int n = 0; for (int b = bits; b != 0; b &= b - 1) n++; return n; }

    // ------------------------------------------------------------------ tuning
    public const float ExtractWork = 36f;      // worker-seconds to cut a core out of a body (four at a time)
    public const int MaxCarvers = 4;
    public const float CarryPace = 0.45f;      // a carrier staggers under the core
    public const float SenseRange = 60f;       // the sixth sense: how far an ant feels a core's pulse through the fog
    public const float LureRange = 32f;        // predators within this of a core on the move come for it
    public const float CoreOrganic = 40f;      // a core the queen already has is only rich meat
    public const float StingVenom = 0.55f, StingTime = 4f, StingBite = 1.1f;
    public const int Nurses = 3;
    public const float NurseFeed = 0.55f;      // organic matter one nurse spoons into the royal larva each second
    public const float LarvaNeed = 170f;       // the first royal larva: a large share of what a chapter hauls
    public const float LarvaNeedLater = 60f;   // a later one (the nursery and its nurses are already there)
    public const float CocoonTime = 100f, CocoonTimeLater = 40f;
    public const float LarvaBite = 0.014f;     // share of the larva each enemy in the nursery eats per second
    public const float NurseryWork = 70f;
    // the web ants' throw, by the Spinneret's rank in the evolve tree (1-3)
    public static float PinTime(int rank) => 1.5f + 1.5f * rank;      // 3, 4.5, 6 s
    public static float WebRange(int rank) => 6f + 2f * rank;         // 8, 10, 12
    public static int PinCount(int rank) => rank;                     // flyers pinned by one throw
    public const float WebCooldown = 9f;
}

/// A mutation core in a body: workers cut it out and carry it home to the queen. It glows and pulses; the colony
/// feels it through the fog (Mutations.SenseRange). Heavy: its carrier walks at half pace and predators come for it.
public sealed class MutationCore
{
    public int Id;
    public string Name = "";
    public Vector2 Pos;
    public float Heading;
    public AntType From = AntType.Wasp;   // the body it lies in (rendering)
    public Mutation Gives;                // the unit the queen will lay (None: a story object, level 1's gland)
    public float Work = Mutations.ExtractWork, Done;
    public int Carrier = -1, CarrierSerial;
    public int Carvers;                   // workers cutting this tick
    public bool Home;                     // delivered to a queen
    public int HomeTeam = -1;             // whose
    public bool Free => Done >= Work;
    public bool Carried => Carrier >= 0;
}

public sealed partial class Sim
{
    public readonly List<MutationCore> Cores = new();
    int _nextCore;

    public MutationCore AddCore(Vector2 p, string name, AntType from, Mutation gives, float work = Mutations.ExtractWork)
    {
        var m = new MutationCore { Id = _nextCore++, Pos = p, Name = name, From = from, Gives = gives, Work = work, Heading = Rng.R(0, DMath.Tau) };
        Cores.Add(m);
        return m;
    }

    public MutationCore CoreById(int id) { foreach (var m in Cores) if (m.Id == id) return m; return null; }

    public bool Has(int team, Mutation m) => team is >= 0 and <= 1 && (Col[team].Mutations & (int)m) != 0;

    /// The sixth sense: one of the colony's ants is close enough to feel this core's pulse (it shows through the fog).
    public bool Senses(int team, MutationCore m)
    {
        if (m.Home || team is < 0 or > 1 || Mission != null && Mission.Chapter < Reveal.Mutagen) return false;
        for (int i = 0; i < High; i++)
            if (Ants[i].Alive && Ants[i].Team == team && Ants[i].Layer == 0 && Ants[i].Pos.DistanceSquaredTo(m.Pos) < Mutations.SenseRange * Mutations.SenseRange) return true;
        return false;
    }

    // ------------------------------------------------------------------ orders
    /// Workers go to cut the core out (or pick it up, or walk beside its carrier); fighters sent with them guard it.
    public void CmdExtract(List<int> ids, MutationCore m)
    {
        if (m == null || m.Home) return;
        var others = new List<int>();
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive) continue;
            if (a.Type != AntType.Worker) { others.Add(id); continue; }
            if (a.MutaP1 > 0 && a.MutaP1 != m.Id + 1) DropCore(id, ref a);
            a.MutaP1 = m.Id + 1;
            a.StrawP1 = 0;
            a.Nurse = false;
            a.DigP1 = 0;
            a.Assigned = null; a.FoodTarget = null;
            if (a.Carrying && !a.MutaCarry) a.Carrying = false; // it drops its crumb to take the core
            a.State = AntState.Move; a.Goal = m.Pos; a.GoalLayer = 0;
        }
        if (others.Count > 0) CmdMove(others, m.Pos, true);
    }

    /// A carrier sent elsewhere (or killed) lets the core fall where it stands.
    void DropCore(int id, ref Ant a)
    {
        if (a.MutaP1 <= 0) return;
        var m = CoreById(a.MutaP1 - 1);
        if (m != null && m.Carrier == id) { m.Carrier = -1; Events.Add(new SimEvent { Kind = EvKind.MutagenDropped, Pos = m.Pos, Team = a.Team }); }
        a.MutaP1 = 0;
        a.MutaCarry = false;
    }

    /// The core reaches the queen and she mutates at once: one core, one new unit. A core she already carries in her
    /// line is only rich meat; a wasp's core waits in the nest until she has laid winged ants for the sting to grow on.
    void TakeCore(int team, MutationCore core)
    {
        var c = Col[team];
        var mu = core.Gives;
        if (mu == Mutation.None) return;                 // a story object: the chapter's own script reads it
        if ((c.Mutations & (int)mu) != 0) { c.Organic += Mutations.CoreOrganic; c.OrganicGathered += Mutations.CoreOrganic; return; }
        var needs = Mutations.Needs(mu);
        if (needs != Mutation.None && (c.Mutations & (int)needs) == 0)
        {
            c.Waiting |= (int)mu;
            Events.Add(new SimEvent { Kind = EvKind.CoreWaits, Pos = NestEntrance(team), Team = team, Tier = (int)mu });
            return;
        }
        MutateQueen(team, mu);
        // a core that was waiting for this one
        foreach (var w in Mutations.All)
            if ((c.Waiting & (int)w) != 0 && (c.Mutations & (int)Mutations.Needs(w)) != 0) { c.Waiting &= ~(int)w; MutateQueen(team, w); }
    }

    /// Test and tour hook: the queen takes a mutation as if its core had just reached her.
    public void GiveMutation(int team, Mutation mu)
    {
        if (Mutations.Needs(mu) is var n && n != Mutation.None && !Has(team, n)) MutateQueen(team, n);
        MutateQueen(team, mu);
    }

    void MutateQueen(int team, Mutation mu)
    {
        var c = Col[team];
        if (!c.Alive || (c.Mutations & (int)mu) != 0) return;
        c.Mutations |= (int)mu;
        if (Mission != null && team == 0) Mission.Mutated++;
        if (mu == Mutation.RoyalLarva && Mission != null && team == 0) LayRoyalLarva(team, AntType.Warlord, Mutations.LarvaNeed, Mutations.CocoonTime);
        // the winged ants' own room, dug by the workers like any other
        if (mu == Mutation.Winged && c.Founded && ChamberOf(team, ChamberKind.Wings) == null) AddChamber(team, ChamberKind.Wings, 60f, false);
        // the burst rises from the queen's chamber; the sky beam stands over the entrance (To)
        Events.Add(new SimEvent { Kind = EvKind.Mutate, Pos = Royal(team) is { } rc ? ChamberPos(team, rc) : NestEntrance(team), To = NestEntrance(team), Team = team, Tier = (int)mu });
    }
    // ------------------------------------------------------------------ flight: what the ground can reach
    /// A flyer in the air (not pinned, not down at its prey).
    public bool Airborne(in Ant b) => Config.Flies(b.Type) && b.PinT <= 0 && b.Alt > Config.ReachAlt;

    /// Whether `a` can fight `b` where it is: flying predators in the air are out of reach of every ground ant, acid
    /// gunners included; other flyers keep the old rule (ground ants reach anything below the cruise band).
    public bool CanReach(in Ant a, in Ant b)
    {
        if (Config.Flies(a.Type)) return true;
        if (Config.Predator(b.Type)) return !Airborne(b);
        return Config.Ranged(a.Type) || b.Alt <= 2.2f;
    }

    /// A flying predator in the air within striking distance of a ground ant of `team` (the ant ducks away).
    int Hunter(Vector2 p, int team)
    {
        int best = -1; float bd = 36f;
        int cx = (int)(p.X / SpatialHash.Cell), cz = (int)(p.Y / SpatialHash.Cell);
        for (int z = cz - 1; z <= cz + 1; z++)
        {
            if (z < 0 || z >= Hash.N) continue;
            for (int x = cx - 1; x <= cx + 1; x++)
            {
                if (x < 0 || x >= Hash.N) continue;
                for (int j = Hash.Head[z * Hash.N + x]; j >= 0; j = Hash.Next[j])
                {
                    ref var b = ref Ants[j];
                    if (!b.Alive || b.Team == team || !Config.Predator(b.Type) || !Airborne(b) || b.State != AntState.Engage) continue;
                    float d = p.DistanceSquaredTo(b.Pos);
                    if (d < bd) { bd = d; best = j; }
                }
            }
        }
        return best;
    }

    /// A web ant flings silk at the flying enemies in reach and pins them to the ground.
    void ThrowWeb(int i, ref Ant a)
    {
        int rank = 1 + Evo(a.Team, EvoNode.Spinneret);
        float range = Mutations.WebRange(rank);
        int pinned = 0;
        for (int j = 0; j < High && pinned < Mutations.PinCount(rank); j++)
        {
            ref var b = ref Ants[j];
            if (!b.Alive || b.Team == a.Team || b.Layer != 0 || !Config.Flies(b.Type) || !Airborne(b)) continue;
            if (a.Pos.DistanceSquaredTo(b.Pos) > range * range) continue;
            b.PinT = Mutations.PinTime(rank);
            b.Root = 0;
            pinned++;
            Events.Add(new SimEvent { Kind = EvKind.WebShot, Pos = a.Pos, To = b.Pos, Team = a.Team, Type = a.Type });
        }
        if (pinned == 0) return;
        a.SpecialCd = Mutations.WebCooldown - Evolve.SilkGlandsCd * Evo(a.Team, EvoNode.SilkGlands);
        a.AttackAnim = 1;
    }

    /// The evolve tree changes every ant of a caste on the board at once.
    void Reapply(int team)
    {
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (a.Alive && a.Team == team && a.Type != AntType.Queen) ApplyLevel(ref a);
        }
    }

    /// A winged ant's sting (the wasp core): venom that keeps burning after the bite.
    void MutantBite(int i, ref Ant a, ref Ant t, float dmg)
    {
        if (a.Type != AntType.Flyer || !Has(a.Team, Mutation.Stinger) || !t.Alive || t.Type == AntType.Queen) return;
        float venom = dmg * (Mutations.StingVenom + Evolve.VenomSacVenom * Evo(a.Team, EvoNode.VenomSac));
        float time = Mutations.StingTime + Evolve.LastingVenomTime * Evo(a.Team, EvoNode.LastingVenom);
        Events.Add(new SimEvent { Kind = EvKind.Sting, Pos = t.Pos, Team = a.Team, Type = a.Type });
        if (Mission != null && a.Team == 0) Mission.Stings++;
        t.PoisonDps = t.PoisonT > 0 ? Mathf.Max(t.PoisonDps, venom) : venom;
        t.PoisonT = Mathf.Max(t.PoisonT, time);
        t.PoisonSrc = i;
    }

    /// A death that matters to the mutation track: a carrier drops its core, a core's keeper leaves it in its body.
    void MutationDeath(int id, ref Ant t, int killer)
    {
        if (t.MutaP1 > 0) DropCore(id, ref t);
        if (t.CoreP1 > 0)
        {
            // the creature that carried a core (Sim.GiveCore): it lies in the body where it fell
            var gives = (Mutation)(t.CoreP1 - 1);
            var core = AddCore(t.Pos, CoreName(t.Type, gives), t.Type, gives);
            t.CoreP1 = 0;
            if (Mission is { } mm && mm.CoreKeepers.Contains(id)) mm.Gland = core;
        }
        var m = Mission;
        if (m == null || m.MasterGroup < 0 || t.Team != Config.Wild || t.Level < 3) return; // the master, not its brood
        int serial = t.Serial;
        if (m.Groups[m.MasterGroup].Exists(g => g.id == id && g.serial == serial)) m.MastersSlain++;
    }

    /// Skirmish and multiplayer: the cores are in the lairs. Every lair's master (its highest-level bug, level 3 or
    /// more) keeps the core of its kind, and two dead flies lie in the lairs nearest each colony's flank, guarded by
    /// what lives there. Both colonies race for them; nothing is handed over.
    void SeedSkirmishCores()
    {
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive || a.Team != Config.Wild || a.Level < 3 || a.Boss) continue;
            var gives = Mutations.CoreOf(a.Type);
            if (gives == Mutation.None) continue;
            // one keeper per lair: the first of its highest level
            bool master = true;
            for (int j = 0; j < High && master; j++)
                if (j != i && Ants[j].Alive && Ants[j].Team == Config.Wild && Ants[j].Home.DistanceSquaredTo(a.Home) < 1f && (Ants[j].Level > a.Level || Ants[j].Level == a.Level && j < i)) master = false;
            if (master) a.CoreP1 = (byte)((int)gives + 1);
        }
        var n0 = W.Nests[0].Pos; var n1 = W.Nests[1].Pos;
        var along = (n1 - n0).Normalized(); var perp = new Vector2(along.Y, -along.X);
        float span = n0.DistanceTo(n1);
        var used = new List<Camp>();
        foreach (var want in new[] { n0.Lerp(n1, 0.35f) + perp * span * 0.22f, n0.Lerp(n1, 0.65f) - perp * span * 0.22f })
        {
            Camp best = null; float bd = float.MaxValue;
            foreach (var camp in W.Camps)
            {
                float d = camp.Pos.DistanceTo(want);
                if (d < bd && !used.Contains(camp)) { bd = d; best = camp; }
            }
            if (best == null) continue;
            used.Add(best);
            Cores.Add(new MutationCore { Id = _nextCore++, Pos = best.Pos + perp * 2.5f, Name = "a fly's core", From = AntType.Wasp, Gives = Mutation.Winged, Heading = best.Facing });
        }
    }

    void ClearSkirmishCores()
    {
        Cores.Clear(); _nextCore = 0;
        for (int i = 0; i < High; i++) Ants[i].CoreP1 = 0;
    }

    /// Marks a living creature as the keeper of a core: when it dies the core lies in its body.
    public void GiveCore(int id, Mutation gives) { if (id >= 0 && Ants[id].Alive) Ants[id].CoreP1 = (byte)((int)gives + 1); }

    static string CoreName(AntType from, Mutation gives) => from switch
    {
        AntType.Spider => "the spider's core",
        AntType.Wasp => "the wasp's core",
        AntType.Beetle or AntType.StagBeetle => "the beetle's core",
        AntType.Queen => "the queen's core",
        _ => "a mutation core",
    };
    // ------------------------------------------------------------------ the gland on the move
    /// A worker with a mutagen order: carve it out, take it up, or walk beside whoever has it; the carrier staggers home.
    Vector2 CoreLogic(int i, ref Ant a, float dt, ref float speedMul)
    {
        var m = CoreById(a.MutaP1 - 1);
        if (m == null || m.Home) { a.MutaP1 = 0; a.MutaCarry = false; a.State = AntState.Forage; return Vector2.Zero; }
        a.State = AntState.Move;
        a.GoalLayer = 0;
        if (a.MutaCarry)
        {
            speedMul = Mutations.CarryPace;
            var home = NestEntrance(a.Team);
            m.Pos = a.Pos + MathX.Dir(a.Heading) * 0.55f;
            m.Heading = a.Heading;
            if (a.Layer == 0 && a.Pos.DistanceTo(home) < 1.8f)
            {
                // to the queen: she consumes it and mutates at once
                m.Home = true; m.Carrier = -1; m.HomeTeam = a.Team;
                Col[a.Team].CoresHome++;
                a.MutaP1 = 0; a.MutaCarry = false; a.State = AntState.Forage;
                Events.Add(new SimEvent { Kind = EvKind.MutagenHome, Pos = home, Team = a.Team });
                TakeCore(a.Team, m);
                return Vector2.Zero;
            }
            a.Goal = home;
            return home - a.Pos;
        }
        var to = m.Pos - a.Pos;
        a.Goal = m.Pos;
        if (m.Carried && Ants[m.Carrier].Team != a.Team) { a.MutaP1 = 0; a.State = AntState.Forage; return Vector2.Zero; } // the other colony has it: that is the fighters' work
        if (m.Carried)
        {
            // escort: keep close to the carrier, a little to its side
            var side = MathX.Dir(m.Heading + (a.Serial % 2 == 0 ? 1.4f : -1.4f)) * 1.2f;
            var at = m.Pos + side - a.Pos;
            if (at.Length() < 0.6f) return Vector2.Zero;
            speedMul = Mathf.Min(1f, at.Length() > 3 ? 1f : Mutations.CarryPace * 1.2f);
            return at;
        }
        if (a.Layer != 0 || to.Length() > 1.5f) return to;
        if (!m.Free)
        {
            if (m.Carvers < Mutations.MaxCarvers) { m.Carvers++; m.Done += dt; }
            a.Heading += MathX.AngleDiff(a.Heading, to.AngleD()) * 0.2f;
            if (Ticks % 15 == i % 15) a.AttackAnim = 1;
            if (m.Free) Events.Add(new SimEvent { Kind = EvKind.MutagenFree, Pos = m.Pos, Team = a.Team });
            return Vector2.Zero;
        }
        m.Carrier = i; m.CarrierSerial = a.Serial;
        a.MutaCarry = true;
        Events.Add(new SimEvent { Kind = EvKind.MutagenTaken, Pos = m.Pos, Team = a.Team });
        return Vector2.Zero;
    }

    void TickCores()
    {
        foreach (var m in Cores)
        {
            m.Carvers = 0;
            if (m.Carrier >= 0 && (!Valid(m.Carrier, m.CarrierSerial) || !Ants[m.Carrier].MutaCarry)) m.Carrier = -1;
            if (m.Home || !m.Carried || Ticks % 60 != 0) continue;
            // the glow and the smell on the move: hunters nearby come for it
            for (int i = 0; i < High; i++)
            {
                ref var b = ref Ants[i];
                if (!b.Alive || b.Team != Config.Wild || b.Layer != 0 || b.Raider || b.State is not (AntState.Idle or AntState.Hold)) continue;
                if (b.Pos.DistanceTo(m.Pos) > Mutations.LureRange) continue;
                b.State = AntState.AttackMove; b.Goal = b.Anchor = m.Pos;
            }
        }
    }

    // ------------------------------------------------------------------ the royal larva
    /// The queen lays a royal larva for a hero of this kind. It waits in her chamber until the nursery is dug, then
    /// lies there for the nurses to feed.
    void LayRoyalLarva(int team, AntType hero, float need, float cocoon)
    {
        var c = Col[team];
        c.LarvaHero = hero;
        c.LarvaNeed = need;
        c.LarvaFed = 0;
        c.LarvaHp = 1;
        c.CocoonTime = cocoon;
        c.LarvaStage = 1;
        if (ChamberOf(team, ChamberKind.Nursery) == null) AddChamber(team, ChamberKind.Nursery, Mutations.NurseryWork, false);
        Events.Add(new SimEvent { Kind = EvKind.LarvaLaid, Pos = ChamberPos(team, Royal(team)), Team = team, Type = hero });
    }

    public NestChamber Nursery(int team) { var ch = ChamberOf(team, ChamberKind.Nursery); return ch is { Built: true } ? ch : null; }

    /// The campaign's heroes come only from a royal larva: the Heroes tab lays one (food now, organic matter through
    /// the nurses). Returns Locked when the colony cannot (no mutation, no nursery, a larva already growing).
    QueueResult CanLayHero(Colony c, ProdKind kind)
    {
        if (!Has(c.Team, Mutation.RoyalLarva) || Nursery(c.Team) == null) return QueueResult.Locked;
        if (c.LarvaStage != 0 || c.Count[(int)Config.UnitOf(kind)] > 0) return QueueResult.HeroExists;
        if (c.Food < Config.ProdCost(kind)) return QueueResult.NoFood;
        return QueueResult.Ok;
    }

    void TickLarva(Colony c, float dt)
    {
        if (c.LarvaStage == 0 || !c.Alive) { ReleaseNurses(c); return; }
        var room = Nursery(c.Team);
        if (room == null) return;
        if (c.LarvaStage == 1) { c.LarvaStage = 2; Events.Add(new SimEvent { Kind = EvKind.LarvaLaid, Pos = ChamberPos(c.Team, room), Team = c.Team, Type = c.LarvaHero }); }
        var at = ChamberPos(c.Team, room);
        int layer = NestLayer(c.Team);
        // nurses: three workers live in the nursery and do nothing else
        int nurses = 0, here = 0;
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive || a.Team != c.Team || !a.Nurse) continue;
            nurses++;
            if (a.Layer == layer && a.Pos.DistanceTo(at) < room.Radius + 1f) here++;
        }
        if (nurses < Mutations.Nurses && Ticks % 30 == 0)
        {
            int best = -1; float bd = float.MaxValue;
            for (int i = 0; i < High; i++)
            {
                ref var a = ref Ants[i];
                if (!a.Alive || a.Team != c.Team || a.Type != AntType.Worker || a.Nurse || a.MutaP1 > 0 || a.DigP1 > 0 || a.Carrying) continue;
                float d = a.Pos.DistanceTo(NestEntrance(c.Team)) + (a.Layer == layer ? 0 : 6);
                if (d < bd) { bd = d; best = i; }
            }
            if (best >= 0) { ref var n = ref Ants[best]; n.Nurse = true; n.Assigned = null; n.FoodTarget = null; }
        }
        // anything hostile in the nursery eats the larva or tears the cocoon
        int foes = 0;
        for (int i = 0; i < High; i++)
        {
            ref var b = ref Ants[i];
            if (b.Alive && b.Team != c.Team && b.Layer == layer && b.Pos.DistanceTo(at) < room.Radius + 1.6f) foes++;
        }
        if (foes > 0) c.LarvaHp -= Mutations.LarvaBite * foes * dt;
        else c.LarvaHp = Mathf.Min(1, c.LarvaHp + 0.01f * dt);
        if (Mission != null && c.Team == 0) Mission.LarvaLow = Mathf.Min(Mission.LarvaLow, c.LarvaHp);
        if (c.LarvaHp <= 0)
        {
            c.LarvaStage = 0; c.LarvaFed = 0;
            if (Mission is { InCocoon: { } lost } && c.Team == 0) { Mission.Fallen.Add(lost); Mission.InCocoon = null; } // Burr dies in the torn silk
            Events.Add(new SimEvent { Kind = EvKind.LarvaLost, Pos = at, Team = c.Team });
            if (Mission is { } m && c.Team == 0 && m.Objectives.Exists(o => o.Kind is Expedition.Obj.Larva or Expedition.Obj.Cocoon) && !m.Complete && m.HeroesBorn == 0)
            {
                m.LossReason = "The raiders reached the nursery and devoured the royal larva. Without her the colony has no general, and the woods close in.";
                Winner = Config.Enemy;
            }
            return;
        }
        if (c.LarvaStage == 2)
        {
            float take = Mathf.Min(Mutations.NurseFeed * here * dt, Mathf.Min(c.Organic, c.LarvaNeed - c.LarvaFed));
            if (take > 0) { c.Organic -= take; c.LarvaFed += take; }
            if (c.LarvaFed >= c.LarvaNeed - 1e-3f)
            {
                c.LarvaStage = 3; c.CocoonT = c.CocoonTime;
                TakeBurrIntoCocoon(c);
                Events.Add(new SimEvent { Kind = EvKind.Cocoon, Pos = at, Team = c.Team, Type = c.LarvaHero });
            }
            return;
        }
        // the cocoon: it throbs and strains until the hero breaks out
        c.CocoonT -= dt;
        if (c.CocoonT > 0) return;
        c.LarvaStage = 0;
        int hero = Spawn(c.Team, c.LarvaHero, at + Rng.InDisk(0.4f), Rng.R(0, DMath.Tau));
        if (hero >= 0)
        {
            ref var h = ref Ants[hero];
            h.Layer = (byte)layer; h.Anchor = at; h.State = AntState.Idle;
            if (Mission != null && c.Team == 0)
            {
                Mission.HeroesBorn++;
                // old Burr went into the silk a soldier and breaks out the colony's Warlord: same name, same kills
                if (Mission.InCocoon is { } burr) { burr.Type = c.LarvaHero; Mission.Tracked[hero] = (h.Serial, burr); Mission.InCocoon = null; }
            }
            Events.Add(new SimEvent { Kind = EvKind.HeroBorn, Pos = at, Team = c.Team, Type = c.LarvaHero, HatchP1 = hero + 1, To = at });
        }
        ReleaseNurses(c);
    }

    /// The campaign's first Warlord is old Burr, the tube's eldest soldier: when the royal larva spins its cocoon he
    /// lies down beside it and the silk closes over them both. He leaves the field (no death, no corpse) and his
    /// record waits in Mission.InCocoon for the Warlord that breaks out. If Burr has already fallen, the cocoon
    /// hatches a stranger (the roster names it Hemlock).
    void TakeBurrIntoCocoon(Colony c)
    {
        if (Mission is not { } m || c.Team != 0 || c.LarvaHero != AntType.Warlord || m.InCocoon != null) return;
        foreach (var (id, (serial, vet)) in m.Tracked)
        {
            if (vet.Name != "Burr" || Config.IsHero(vet.Type) || !Valid(id, serial)) continue;
            if (Ants[id].SquadP1 > 0) Squads[Ants[id].SquadP1 - 1].Members.Remove(id);
            Remove(id);
            m.Tracked.Remove(id);
            m.InCocoon = vet;
            return;
        }
    }

    void ReleaseNurses(Colony c)
    {
        if (Ticks % 30 != 0) return;
        for (int i = 0; i < High; i++)
            if (Ants[i].Alive && Ants[i].Team == c.Team && Ants[i].Nurse) { Ants[i].Nurse = false; Ants[i].State = AntState.Forage; }
    }

    /// A nurse lives in the nursery beside the larva.
    Vector2 NurseLogic(ref Ant a)
    {
        var room = Nursery(a.Team);
        if (room == null) { a.Nurse = false; a.State = AntState.Forage; return Vector2.Zero; }
        var at = ChamberPos(a.Team, room) + MathX.Dir(a.Serial * 2.39996f) * (room.Radius * 0.7f);
        a.State = AntState.Move; a.Goal = at; a.GoalLayer = (byte)NestLayer(a.Team);
        var to = at - a.Pos;
        if (a.Layer == NestLayer(a.Team) && to.Length() < 0.5f) return Vector2.Zero;
        return to;
    }

    // ------------------------------------------------------------------ between chapters
    /// What the story has given the queen before this chapter begins (a chapter played on its own, with no save).
    public static int StoryMutations(int chapter) =>
        (chapter > Reveal.Mutagen ? (int)Mutation.Winged : 0) | (chapter > Reveal.RoyalLarva ? (int)Mutation.RoyalLarva : 0)
        | (chapter > Reveal.Web ? (int)(Mutation.Web | Mutation.Stinger) : 0) | (chapter > Reveal.Armoured ? (int)Mutation.Armoured : 0);

    /// The colony's mutations and evolve tree as the chapter begins: what the roster carried, and never less than the
    /// story has given by this chapter, so every chapter is playable on its own.
    void ApplyMutationState(ColonyRoster roster, int chapter)
    {
        var c = Col[0];
        c.Mutations = (roster?.Mutations ?? 0) | StoryMutations(chapter);
        c.Waiting = roster?.Waiting ?? 0;
        // the red colony's line has fed on the same spill; with the spider's core it has silk too (web ants, and the Spinnery: HasSilk)
        Col[1].Mutations = StoryMutations(chapter) & ~(int)Mutation.RoyalLarva;
        for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Type != AntType.Queen && !Config.IsHero(Ants[i].Type)) Ants[i].Brood = (byte)System.Math.Clamp(c.T(Upgrade.Queen) - 1, 0, 4);
        if (Mission != null && Mission.Perks.HasFlag(Perk.Larder)) c.Organic += Levels.LarderOrganic;
        if (roster != null) for (int n = 0; n < c.Evo.Length; n++) c.Evo[n] = roster.Evo[n];
        if (Has(0, Mutation.RoyalLarva) && c.Founded && ChamberOf(0, ChamberKind.Nursery) == null)
            AddChamber(0, ChamberKind.Nursery, Mutations.NurseryWork, true);
        if (Has(0, Mutation.Winged) && c.Founded && ChamberOf(0, ChamberKind.Wings) == null) AddChamber(0, ChamberKind.Wings, 60f, true);
        RecountCapacity(c);
        Reapply(0);
        c.PopUsed = 0; for (int t = 0; t < c.Count.Length; t++) c.PopUsed += c.Count[t] * Config.Stats[t].Pop;
    }
}

public sealed partial class Expedition
{
    public int Mutated, Stings, HeroesBorn, MastersSlain;
    public int MasterGroup = -1;          // the nest master whose death the chapter counts (the web spider)
    public float LarvaLow = 1;
}
