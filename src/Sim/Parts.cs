using Godot;

namespace Mandible;

/// A body part taken from an insect (docs/progression.md 1c): every mutation unit hatched costs one of its kind.
public enum Part { Wings = 0, Sting = 1, Gland = 2, Plates = 3, Tail = 4 } // Tail: a scorpion's sting (progression.md 1d)

public static class BodyParts
{
    public const int Count = 5;

    /// The part (+1) a dead creature of this kind holds; 0: none. Flies are not a living creature: their remains are
    /// placed with Sim.AddRemains.
    public static byte Of(AntType t) => t switch
    {
        AntType.Wasp => (byte)Part.Sting + 1, AntType.Spider => (byte)Part.Gland + 1,
        AntType.Beetle or AntType.StagBeetle => (byte)Part.Plates + 1,
        AntType.Scorpion or AntType.Scorpling => (byte)Part.Tail + 1, _ => 0,
    };

    /// The part (+1) one brood of this kind costs; 0: none (plain castes and generals). A winged ant always costs a
    /// pair of wings; the sting is extra and never blocks the brood (Sim.StingsFor).
    public static byte Needs(ProdKind k, int mutations) => k switch
    {
        ProdKind.Flyer => (byte)Part.Wings + 1,
        ProdKind.WebAnt => (byte)Part.Gland + 1, ProdKind.Armoured => (byte)Part.Plates + 1,
        ProdKind.ScorpionAnt => (byte)Part.Tail + 1, _ => 0,
    };

    /// The same for the unit that hatches.
    public static byte NeedsFor(AntType unit, int mutations) => unit switch
    {
        AntType.Flyer => Needs(ProdKind.Flyer, mutations), AntType.WebAnt => Needs(ProdKind.WebAnt, mutations),
        AntType.Armoured => Needs(ProdKind.Armoured, mutations), AntType.ScorpionAnt => Needs(ProdKind.ScorpionAnt, mutations), _ => 0,
    };

    public static string Name(Part p, int n = 1) => p switch
    {
        Part.Wings => n == 1 ? "pair of wings" : "pairs of wings", Part.Sting => n == 1 ? "sting" : "stings",
        Part.Gland => n == 1 ? "silk gland" : "silk glands", Part.Tail => n == 1 ? "scorpion's sting" : "scorpion's stings",
        _ => n == 1 ? "set of shell plates" : "sets of shell plates",
    };

    /// Short name for a count on a button.
    public static string Short(Part p) => p switch { Part.Wings => "wings", Part.Sting => "stings", Part.Gland => "silk glands", Part.Tail => "scorpion stings", _ => "shell plates" };

    /// The insect it is taken from.
    public static string Prey(Part p) => p switch { Part.Wings => "flies", Part.Sting => "wasps", Part.Gland => "spiders", Part.Tail => "scorpions", _ => "beetles" };

    /// Two words for the hatch card when there are none in store.
    public static string Lack(Part p) => p == Part.Wings ? "Find flies" : $"Hunt {Prey(p)}";

    /// What to tell a player who has none.
    public static string Hunt(Part p) => p == Part.Wings
        ? "Flies hang dead in spiders' webs: kill the spider and let the workers carry a fly home. Each gives one pair of wings."
        : $"Hunt {Prey(p)} and let the workers carry the remains home: each gives one {Name(p)}.";
}

public sealed partial class Sim
{
    /// The part (+1) the colony must have in store to lay this brood; 0: none.
    public byte PartNeed(int team, ProdKind k) => team is 0 or 1 ? BodyParts.Needs(k, Col[team].Mutations) : (byte)0;

    /// How many of its part one brood costs: one for each ant of the squad that hatches (one part, one ant).
    public static int PartCount(ProdKind k) => Config.SquadSize(k);

    /// Whether the colony holds the parts this brood costs (always true for the plain castes).
    public bool PartReady(int team, ProdKind k)
    {
        int p = PartNeed(team, k);
        return p == 0 || Col[team].Parts[p - 1] >= PartCount(k);
    }

    /// Stings a brood of winged ants would take now: with the wasp's core each ant takes one while the store lasts
    /// and hatches as a stinger; the rest hatch as plain winged ants. A missing sting never blocks the brood.
    public int StingsFor(int team, ProdKind k)
    {
        if (k != ProdKind.Flyer || team is not (0 or 1) || !Has(team, Mutation.Stinger)) return 0;
        return System.Math.Min(PartCount(k), Col[team].Parts[(int)Part.Sting]);
    }

    /// Makes a winged ant a stinger (the ants a chapter starts with, tests).
    public void GiveSting(int id)
    {
        ref var a = ref Ants[id];
        if (!a.Alive || a.Type != AntType.Flyer || a.Sting) return;
        a.Sting = true;
        ApplyLevel(ref a);
    }

    /// How many of this part the colony has in store.
    public int PartsOf(int team, Part p) => team is 0 or 1 ? Col[team].Parts[(int)p] : 0;

    /// The worker cutting at these remains takes the part with its load; it rides with her until she is home.
    void TakePart(ref Ant a, FoodSource f)
    {
        if (f.Part == 0 || a.PartP1 != 0 || a.Team > 1) return;
        a.PartP1 = f.Part;
        f.Part = 0;
    }

    /// A part reaches the nest: one more of its kind in store.
    void StorePart(Colony c, byte partP1, Vector2 at)
    {
        if (partP1 == 0) return;
        c.Parts[partP1 - 1]++;
        Events.Add(new SimEvent { Kind = EvKind.PartHome, Pos = at, Team = c.Team, Tier = partP1 - 1, Amount = c.Parts[partP1 - 1] });
    }

    /// The campaign's colony sends for the part its objective is waiting on (a hatch objective for a mutation unit, with
    /// too few of its part in store): one free forager walks out to each body that holds one, is known to the colony
    /// (within recruiting range of the nest, or in sight of one of its ants) and has no hunter near it. At every other
    /// time the campaign's foragers take only the remains they find near home, as they always have (far remains are
    /// the player's to send workers to); outside the campaign the carcass crews of Recruit bring parts in.
    /// Whether the queen lays a unit that is paid with this part (+1).
    bool UsesPart(Colony c, byte partP1) => partP1 != 0 && (c.Mutations & (int)((Part)(partP1 - 1) switch
    {
        Part.Wings => Mutation.Winged, Part.Sting => Mutation.Stinger, Part.Gland => Mutation.Web, Part.Tail => Mutation.Scorpion, _ => Mutation.Armoured,
    })) != 0;

    /// A living fighter or hunter of another side within r of p on the surface.
    bool HunterNear(int team, Vector2 p, float r)
    {
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (a.Alive && a.Team != team && a.Layer == 0 && !a.Dormant && a.Type != AntType.Worker && a.Pos.DistanceSquaredTo(p) < r * r) return true;
        }
        return false;
    }

    const float PartErrandClear = 24f; // wider than any hunter's aggro (a wasp's is 17)

    void PartErrands(Colony c)
    {
        var m = Mission;
        if (!c.Alive || !c.Founded || m.Step >= m.Objectives.Count) return;
        var o = m.Objectives[m.Step];
        if (o.Kind != Expedition.Obj.Hatch || c.Count[o.A] >= o.B) return;
        byte want = BodyParts.NeedsFor((AntType)o.A, c.Mutations);
        int squad = Config.SquadSize((AntType)o.A);
        int broods = (o.B - c.Count[o.A] + squad - 1) / squad;
        foreach (var q in c.Queue) if (Config.UnitOf(q.Kind) == (AntType)o.A) broods--;
        if (want == 0 || c.Parts[want - 1] >= broods * squad) return; // enough in store for the broods the objective still asks
        int team = c.Team;
        var nest = NestEntrance(team);
        foreach (var f in W.Food)
        {
            if (f.Part != want || !f.Alive || f.Layer != 0) continue;
            if (f.Pos.DistanceTo(nest) > Config.RecruitRange && !Seen(team, f.Pos)) continue;
            int best = -1;
            float bd = float.MaxValue;
            bool busy = false;
            for (int i = 0; i < High && !busy; i++)
            {
                ref var a = ref Ants[i];
                if (!a.Alive || a.Layer != 0 || a.Dormant) continue;
                if (a.Team != team)
                {
                    // a living hunter anywhere near the body: no worker walks into its jaws, or brings it home behind her
                    if (a.Type != AntType.Worker && a.Pos.DistanceSquaredTo(f.Pos) < PartErrandClear * PartErrandClear) busy = true;
                    continue;
                }
                if (a.Type != AntType.Worker) continue;
                if (a.Assigned == f || a.FoodTarget == f) { busy = true; continue; }
                if (a.Carrying || a.DigP1 > 0 || a.PartP1 != 0 || a.State != AntState.Forage) continue;
                float d = a.Pos.DistanceSquaredTo(f.Pos);
                if (d < bd) { bd = d; best = i; }
            }
            if (busy || best < 0) continue;
            ref var w = ref Ants[best];
            w.Assigned = f; w.FoodTarget = f; w.State = AntState.Gather;
        }
    }

    /// Where a colony with fewer than `need` of this part (+1) in store can get one: the nearest remains on the surface that hold
    /// it, else the nearest living insect that carries it. Null when it needs none, has one, or the map holds none.
    public Vector2? PartSource(int team, byte partP1, Vector2 from, int need = 1)
    {
        if (partP1 == 0 || Col[team].Parts[partP1 - 1] >= need) return null;
        Vector2? at = null;
        float best = float.MaxValue;
        foreach (var f in W.Food)
        {
            if (f.Part != partP1 || !f.Alive || f.Layer != 0) continue;
            float d = f.Pos.DistanceSquaredTo(from);
            if (d < best) { best = d; at = f.Pos; }
        }
        if (at != null) return at;
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive || a.Team == team || BodyParts.Of(a.Type) != partP1) continue;
            float d = a.Pos.DistanceSquaredTo(from);
            if (d < best) { best = d; at = a.Pos; }
        }
        return at;
    }

    /// Remains that were never a living creature of this map (prey a spider has caught and stored): the dried husk of
    /// the given body, holding a part. It keeps until a worker is sent for it (FoodSource.Husk).
    /// A spider's larder: the spiders of a new group at `at`, and round them the prey they have caught and stored,
    /// each body holding its part. The parts are won by killing the spiders, which ground ants can do.
    int AddLarder(string name, Vector2 at, int spiders, AntType prey, Part part, int bodies)
    {
        int g = AddGroup(name, at);
        for (int i = 0; i < spiders; i++) { int s = GroupBug(g, AntType.Spider, at + MathX.Dir(i * 2.1f + 0.3f) * 2.8f, i == 0 ? 2 : 1); Ants[s].Home = Ants[s].Pos; }
        for (int i = 0; i < bodies; i++) AddRemains(at + MathX.Dir(i * DMath.Tau / bodies + 0.5f) * (5.5f + i % 2 * 1.4f), prey, part, i * 1.9f);
        return g;
    }

    public FoodSource AddRemains(Vector2 p, AntType body, Part part, float heading = 0)
    {
        var f = new FoodSource
        {
            Id = W.NextFoodId++, Kind = FoodKind.Corpse, Pos = p, Amount = 1, Max = Config.Stats[(int)body].CorpseFood, Radius = Config.Stats[(int)body].Size * 0.45f,
            Team = Config.Wild, AntType = body, Heading = heading, Part = (byte)((int)part + 1), Husk = true,
        };
        W.Food.Add(f);
        return f;
    }
}
