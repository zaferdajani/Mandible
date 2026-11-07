using System.Collections.Generic;
using Godot;

namespace Mandible;

/// What mutagen can do to the colony: things real ants do not have. The tube held strain M-7, a lab line bred to
/// take up mutagen without limit; the researcher's batch broke with it, and the creatures that fed on the spill carry
/// it in their bodies. Evolution (eating remains) makes single ants stronger; mutation changes the whole colony and is
/// paid for only in mutagen, taken from defeated queens, nest masters and rare bosses.
[System.Flags]
public enum Mutation
{
    None = 0,
    Stinger = 1,       // flying ants grow a wasp's venom sting (the queen wasp in the web)
    RoyalLarva = 2,    // the queen can lay a royal larva: the campaign's only way to a hero
    FireBlood = 4,     // soldiers' and majors' bites burn like fire-ant venom (Queen Cinder's line)
    HornetWings = 8,   // flyers faster and tougher (the Hornet Mother)
    IronChitin = 16,   // every fighter's shell thickens
    Regrowth = 32,     // wounds close on their own away from the fight
    HungryGut = 64,    // fighters evolve on less (the starving queen of the Dry Spell)
    AcidBlood = 128,   // a fallen fighter bursts into acid (the queen behind the watch posts)
    SwiftBrood = 256,  // the queen lays faster (the princesses' mother)
    WebThrowing = 512, // soldiers fling silk that pins a flying enemy to the ground (the silk spitter's spinneret; 3 ranks)
}

public static class Mutations
{
    public static readonly Mutation[] All =
    {
        Mutation.Stinger, Mutation.RoyalLarva, Mutation.FireBlood, Mutation.HornetWings, Mutation.IronChitin,
        Mutation.Regrowth, Mutation.HungryGut, Mutation.AcidBlood, Mutation.SwiftBrood, Mutation.WebThrowing,
    };

    public static string Name(Mutation m) => m switch
    {
        Mutation.Stinger => "Venom Stingers",
        Mutation.RoyalLarva => "The Royal Larva",
        Mutation.FireBlood => "Fire Blood",
        Mutation.HornetWings => "Hornet Wings",
        Mutation.IronChitin => "Iron Chitin",
        Mutation.Regrowth => "Regrowth",
        Mutation.HungryGut => "Hungry Gut",
        Mutation.AcidBlood => "Acid Blood",
        Mutation.SwiftBrood => "Swift Brood",
        Mutation.WebThrowing => "Web Throwing",
        _ => "",
    };

    public static string Text(Mutation m) => m switch
    {
        Mutation.Stinger => "Flying ants grow a wasp's sting at the gaster tip. Every strike injects venom that keeps burning.",
        Mutation.RoyalLarva => "The queen can lay a royal larva in its own nursery. Fed on organic matter it spins a cocoon, and a hero breaks out.",
        Mutation.FireBlood => "Soldiers' and majors' bites burn like fire-ant venom for three seconds.",
        Mutation.HornetWings => "Flying ants fly 30% faster and take 25% more punishment.",
        Mutation.IronChitin => "Every fighter's shell thickens: 15% more health.",
        Mutation.Regrowth => "Wounds close on their own: ants mend 1% of their health every second away from a fight.",
        Mutation.HungryGut => "Fighters evolve on 60% more from every mouthful of remains.",
        Mutation.AcidBlood => "A fallen fighter bursts, leaving a pool of acid that burns its killers.",
        Mutation.SwiftBrood => "The queen lays 20% faster.",
        Mutation.WebThrowing => "Soldiers fling silk that pins a flying enemy to the ground, where any ant can bite it. Ranks: longer pins, longer reach, more flyers pinned at once.",
        _ => "",
    };

    /// Where it came from (the card's small line).
    public static string Source(Mutation m) => m switch
    {
        Mutation.Stinger => "from the queen wasp in the web",
        Mutation.RoyalLarva => "from Ashling's glowing body",
        Mutation.FireBlood => "the fire ants' venom",
        Mutation.HornetWings => "the Hornet Mother's flight muscle",
        Mutation.HungryGut => "a queen who starved",
        Mutation.AcidBlood => "the gunners' acid glands",
        Mutation.SwiftBrood => "the princesses' mother",
        Mutation.WebThrowing => "the silk spitter's spinneret",
        _ => "strain M-7",
    };

    /// The defeated queen's (or master's) own trait: the first card a chapter's mutagen offers.
    public static Mutation TraitOf(int levelId) => levelId switch
    {
        14 => Mutation.FireBlood,   // the Royal Larva's red scouts: Cinder's daughters
        3 => Mutation.FireBlood,    // Queen Cinder
        10 => Mutation.HornetWings, // the Hornet Mother
        8 => Mutation.HungryGut,    // the Dry Spell's starving queen
        9 => Mutation.AcidBlood,    // the queen behind the watch posts
        4 => Mutation.SwiftBrood,   // the nuptial flight's mother
        11 => Mutation.IronChitin,  // the sealed mound's queen
        _ => Mutation.Regrowth,
    };

    /// The three cards a chapter's mutagen offers (fixed for the chapter, so a replay offers the same): the defeated
    /// queen's trait first, then the next two the colony does not have, in a chapter-seeded order.
    public static List<Mutation> Offer(int levelId, int owned, int webRank = 0)
    {
        var cards = new List<Mutation>();
        var trait = TraitOf(levelId);
        if ((owned & (int)trait) == 0) cards.Add(trait);
        var pool = new List<Mutation>();
        foreach (var m in All)
        {
            if (m is Mutation.Stinger or Mutation.RoyalLarva || cards.Contains(m)) continue;
            // Web Throwing is earned from the silk spitter; once the colony has it, cards can raise its rank (to 3)
            if (m == Mutation.WebThrowing ? (owned & (int)m) != 0 && webRank < 3 : (owned & (int)m) == 0) pool.Add(m);
        }
        var rng = new Rng((uint)(levelId * 7919 + 13));
        while (cards.Count < 3 && pool.Count > 0)
        {
            int k = rng.I(0, pool.Count);
            cards.Add(pool[k]);
            pool.RemoveAt(k);
        }
        return cards;
    }

    public static int Count(int bits) { int n = 0; for (int b = bits; b != 0; b &= b - 1) n++; return n; }

    // ------------------------------------------------------------------ tuning
    public const float ExtractWork = 36f;      // worker-seconds to carve a gland out of a body (four at a time)
    public const int MaxCarvers = 4;
    public const float CarryPace = 0.45f;      // a carrier staggers under the gland
    public const float SenseRange = 60f;       // the sixth sense: how far an ant feels a mutagen pulse through the fog
    public const float LureRange = 32f;        // predators within this of a gland on the move come for it
    public const float StingVenom = 0.55f, StingTime = 4f, StingBite = 1.1f;
    public const float FireVenom = 0.18f;
    public const float RegrowRate = 0.01f;
    public const float GutMul = 1.6f;
    public const float BroodMul = 1.2f;
    public const int Nurses = 3;
    public const float NurseFeed = 0.55f;      // organic matter one nurse spoons into the royal larva each second
    public const float LarvaNeed = 170f;       // the first royal larva: a large share of what a chapter hauls
    public const float LarvaNeedLater = 60f;   // a later one (the nursery and its nurses are already there)
    public const float CocoonTime = 100f, CocoonTimeLater = 40f;
    public const float LarvaBite = 0.014f;     // share of the larva each enemy in the nursery eats per second
    public const float NurseryWork = 70f;
    public const float WildPerInstability = 0.04f, RedPerInstability = 0.015f; // health only: the woods grow harder to kill, not deadlier
    public static float PinTime(int rank) => 1.5f + 1.5f * rank;      // 3, 4.5, 6 s
    public static float WebRange(int rank) => 6f + 2f * rank;         // 8, 10, 12
    public static int PinCount(int rank) => rank;                     // flyers pinned by one throw
    public const float WebCooldown = 9f;
    public const int SlotsPerLevel = 4;        // population room each colony level adds
    public const float LevelHp = 0.03f;        // and a little toughness for every caste
}

/// A body that holds mutagen: a gland the workers carve out and carry home. It glows faintly and pulses; the colony
/// feels it through the fog (Mutations.SenseRange). Heavy: its carrier walks at half pace and predators come for it.
public sealed class MutagenSource
{
    public int Id;
    public string Name = "";
    public Vector2 Pos;
    public float Heading;
    public AntType From = AntType.Wasp;   // the body it lies in (rendering)
    public float Work = Mutations.ExtractWork, Done;
    public int Units = 1;
    public int Carrier = -1, CarrierSerial;
    public int Carvers;                   // workers carving this tick
    public bool Home;                     // delivered to the queen's reserve
    public bool Free => Done >= Work;
    public bool Carried => Carrier >= 0;
}

public sealed partial class Sim
{
    public readonly List<MutagenSource> Mutagens = new();
    int _nextMutagen;

    public MutagenSource AddMutagen(Vector2 p, string name, AntType from, float work = Mutations.ExtractWork, int units = 1)
    {
        var m = new MutagenSource { Id = _nextMutagen++, Pos = p, Name = name, From = from, Work = work, Units = units, Heading = Rng.R(0, DMath.Tau) };
        Mutagens.Add(m);
        return m;
    }

    public MutagenSource MutagenById(int id) { foreach (var m in Mutagens) if (m.Id == id) return m; return null; }

    public bool Has(int team, Mutation m) => team is >= 0 and <= 1 && (Col[team].Mutations & (int)m) != 0;

    /// The sixth sense: one of the colony's ants is close enough to feel this gland's pulse (it shows through the fog).
    public bool Senses(int team, MutagenSource m)
    {
        if (m.Home || team is < 0 or > 1 || Mission == null || Mission.Chapter < Reveal.Mutagen) return false;
        for (int i = 0; i < High; i++)
            if (Ants[i].Alive && Ants[i].Team == team && Ants[i].Layer == 0 && Ants[i].Pos.DistanceSquaredTo(m.Pos) < Mutations.SenseRange * Mutations.SenseRange) return true;
        return false;
    }

    // ------------------------------------------------------------------ orders
    /// Workers go to carve the gland out (or pick it up, or walk beside its carrier); fighters sent with them guard it.
    public void CmdExtract(List<int> ids, MutagenSource m)
    {
        if (m == null || m.Home) return;
        var others = new List<int>();
        foreach (int id in ids)
        {
            ref var a = ref Ants[id];
            if (!a.Alive) continue;
            if (a.Type != AntType.Worker) { others.Add(id); continue; }
            if (a.MutaP1 > 0 && a.MutaP1 != m.Id + 1) DropMutagen(id, ref a);
            a.MutaP1 = m.Id + 1;
            a.Nurse = false;
            a.DigP1 = 0;
            a.Assigned = null; a.FoodTarget = null;
            if (a.Carrying && !a.MutaCarry) a.Carrying = false; // it drops its crumb to take the gland
            a.State = AntState.Move; a.Goal = m.Pos; a.GoalLayer = 0;
        }
        if (others.Count > 0) CmdMove(others, m.Pos, true);
    }

    /// A carrier sent elsewhere (or killed) lets the gland fall where it stands.
    void DropMutagen(int id, ref Ant a)
    {
        if (a.MutaP1 <= 0) return;
        var m = MutagenById(a.MutaP1 - 1);
        if (m != null && m.Carrier == id) { m.Carrier = -1; Events.Add(new SimEvent { Kind = EvKind.MutagenDropped, Pos = m.Pos, Team = a.Team }); }
        a.MutaP1 = 0;
        a.MutaCarry = false;
    }

    /// The queen consumes mutagen from the reserve and the colony mutates. In the campaign only the mutations the
    /// chapter offers can be taken (the story's first two are fixed; later ones come as cards).
    public bool Mutate(int team, Mutation mu)
    {
        var m = Mission;
        if (m == null || team != 0 || !m.Offers(mu)) return false;
        var c = Col[team];
        if (!c.Alive || c.Mutagen < 1 || (c.Mutations & (int)mu) != 0) return false;
        c.Mutagen--;
        c.Mutations |= (int)mu;
        c.Instability++;
        if (mu == Mutation.WebThrowing) c.WebRank = System.Math.Max(c.WebRank, 1);
        m.Mutated++;
        Reapply();
        if (mu == Mutation.RoyalLarva) LayRoyalLarva(team, AntType.Warlord, Mutations.LarvaNeed, Mutations.CocoonTime);
        Events.Add(new SimEvent { Kind = EvKind.Mutate, Pos = ChamberPos(team, Royal(team)), Team = team, Tier = (int)mu });
        return true;
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

    /// Web Throwing: a soldier flings silk at the flying enemies in reach and pins them to the ground.
    void ThrowWeb(int i, ref Ant a)
    {
        int rank = Col[a.Team].WebRank;
        if (rank <= 0) return;
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
        a.SpecialCd = Mutations.WebCooldown;
        a.AttackAnim = 1;
    }

    /// Mutations and instability change every ant on the board at once.
    void Reapply()
    {
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (a.Alive && a.Type != AntType.Queen) ApplyLevel(ref a);
        }
    }

    /// Stats from mutations, the colony's level and instability (ApplyLevel calls it).
    void MutationStats(ref Ant a)
    {
        if (Mission == null) return;
        if (a.Team == Config.Wild && !a.Boss) { a.MaxHp *= 1 + Mutations.WildPerInstability * Col[0].Instability; return; }
        if (a.Team == 1) { a.MaxHp *= 1 + Mutations.RedPerInstability * Col[0].Instability; return; }
        if (a.Team != 0) return;
        var c = Col[0];
        if (!Config.IsWarrior(a.Type)) return;
        a.MaxHp *= 1 + Mutations.LevelHp * (c.Level - 1);
        if (Has(0, Mutation.IronChitin)) a.MaxHp *= 1.15f;
        if (a.Type == AntType.Flyer && Has(0, Mutation.HornetWings)) { a.MaxHp *= 1.25f; a.SpeedMul *= 1.3f; }
        if (a.Type == AntType.Flyer && Has(0, Mutation.Stinger)) a.DmgMul *= Mutations.StingBite;
    }

    /// A bite from a mutated ant: the flyer's sting and fire blood keep burning after it.
    void MutantBite(int i, ref Ant a, ref Ant t, float dmg)
    {
        if (a.Team != 0 || !t.Alive || t.Type == AntType.Queen) return;
        float venom = 0, time = 0;
        if (a.Type == AntType.Flyer && Has(0, Mutation.Stinger))
        {
            venom = dmg * Mutations.StingVenom; time = Mutations.StingTime;
            Events.Add(new SimEvent { Kind = EvKind.Sting, Pos = t.Pos, Team = a.Team, Type = a.Type });
            if (Mission != null) Mission.Stings++;
        }
        else if (a.Type is AntType.Soldier or AntType.Major && Has(0, Mutation.FireBlood)) { venom = dmg * Mutations.FireVenom; time = 3f; }
        if (venom <= 0) return;
        t.PoisonDps = t.PoisonT > 0 ? Mathf.Max(t.PoisonDps, venom) : venom;
        t.PoisonT = Mathf.Max(t.PoisonT, time);
        t.PoisonSrc = i;
    }

    /// A death that matters to the mutation track: a carrier drops its gland, acid blood bursts, a nest master falls.
    void MutationDeath(int id, ref Ant t, int killer)
    {
        if (t.MutaP1 > 0) DropMutagen(id, ref t);
        if (t.Team == 0 && Config.IsWarrior(t.Type) && Has(0, Mutation.AcidBlood))
            Pools.Add(new AcidPool { Pos = t.Pos, R = 2f, Life = 4f, Max = 4f, Dps = 6f, Team = 0, Src = killer >= 0 ? -1 : -1 });
        var m = Mission;
        if (m != null && m.FragmentGroup >= 0 && t.Team == Config.Wild && t.Type == AntType.Spider && t.Level >= 4 && m.Gland == null)
        {
            int sr = t.Serial;
            if (m.Groups[m.FragmentGroup].Exists(g => g.id == id && g.serial == sr))
                m.Gland = AddMutagen(t.Pos, "the silk spitter's spinneret", AntType.Spider, Mutations.ExtractWork * 0.7f);
        }
        if (m == null || m.MasterGroup < 0 || t.Team != Config.Wild || t.Level < 4) return; // the master, not its brood
        int serial = t.Serial;
        if (m.Groups[m.MasterGroup].Exists(g => g.id == id && g.serial == serial)) m.MastersSlain++;
    }

    // ------------------------------------------------------------------ the gland on the move
    /// A worker with a mutagen order: carve it out, take it up, or walk beside whoever has it; the carrier staggers home.
    Vector2 MutagenLogic(int i, ref Ant a, float dt, ref float speedMul)
    {
        var m = MutagenById(a.MutaP1 - 1);
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
                // into the queen's reserve: she consumes it when the colony mutates
                var c = Col[a.Team];
                c.Mutagen += m.Units;
                c.MutagenGathered += m.Units;
                m.Home = true; m.Carrier = -1;
                a.MutaP1 = 0; a.MutaCarry = false; a.State = AntState.Forage;
                Events.Add(new SimEvent { Kind = EvKind.MutagenHome, Pos = home, Team = a.Team });
                return Vector2.Zero;
            }
            a.Goal = home;
            return home - a.Pos;
        }
        var to = m.Pos - a.Pos;
        a.Goal = m.Pos;
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

    void TickMutagen()
    {
        foreach (var m in Mutagens)
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
                m.LossReason = "The raiders reached the nursery and devoured the royal larva. Without her the colony has no hero, and the woods close in.";
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
    /// The colony's mutation state as the chapter begins: what the save carried, or (with no save) what the story has
    /// given by this chapter, so every chapter is playable on its own.
    void ApplyMutationState(ColonyRoster roster, int chapter)
    {
        var c = Col[0];
        int story = (chapter > Reveal.Mutagen ? (int)Mutation.Stinger : 0) | (chapter > Reveal.RoyalLarva ? (int)Mutation.RoyalLarva : 0);
        c.Mutations = (roster?.Mutations ?? 0) | story;
        c.WebRank = roster?.WebRank ?? (chapter > Reveal.WebThrowing ? 1 : 0);
        if (chapter > Reveal.WebThrowing) { c.Mutations |= (int)Mutation.WebThrowing; c.WebRank = System.Math.Max(c.WebRank, 1); }
        c.Instability = roster != null ? System.Math.Max(roster.Instability, Mutations.Count(story)) : Mutations.Count(story);
        c.Level = roster?.Level ?? Levels.StoryLevel(chapter);
        c.Mutagen = roster?.Mutagen ?? 0;
        if (Mission != null && Mission.Chapter == Reveal.RoyalLarva) c.Mutagen = System.Math.Max(c.Mutagen, 1); // Ashling's body
        if (Has(0, Mutation.RoyalLarva) && Mission?.Chapter != Reveal.RoyalLarva && c.Founded && ChamberOf(0, ChamberKind.Nursery) == null)
            AddChamber(0, ChamberKind.Nursery, Mutations.NurseryWork, true);
        RecountCapacity(c);
        Reapply();
        c.PopUsed = 0; for (int t = 0; t < c.Count.Length; t++) c.PopUsed += c.Count[t] * Config.Stats[t].Pop;
    }
}

public sealed partial class Expedition
{
    public int Mutated, Stings, HeroesBorn, MastersSlain;
    public int MasterGroup = -1;          // the nest master whose death raises the colony's level (the web spider)
    public int FragmentGroup = -1;        // the silk spitter's hollow: its body holds the Web Throwing fragment
    public float LarvaLow = 1;
    public Mutation Fixed;                // the mutation this chapter's story gives (the first two)

    /// The mutations the queen may take in this chapter.
    public bool Offers(Mutation m) => Fixed != Mutation.None && m == Fixed;
}
