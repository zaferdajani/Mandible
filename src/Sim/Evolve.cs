using Godot;

namespace Mandible;

/// Evolution (docs/progression.md, the owner's rules of 2026-11-15): the colony's balance of organic matter is spent
/// in ONE evolve tree. The queen's levels give better brood (every ant she lays afterwards hatches stronger); a
/// general's levels strengthen its skills and unlock new ones (each level is a skill point); and every mutation unit
/// the queen has gained adds its own branch. Nothing here is earned by an ant's own eating: eating heals, the tree
/// evolves. Everything bought stays for the following chapters (ColonyRoster).
public enum EvoNode : byte { FlightMuscle, LightShell, VenomSac, LastingVenom, Spinneret, SilkGlands, Plates, Mending, DeepSting, NerveVenom }

public enum EvolveResult { Ok, Locked, Maxed, NoOrganic }

public static class Evolve
{
    public readonly record struct Def(EvoNode Node, Mutation Branch, string Name, string Text, int Ranks, float Cost, float Step);

    /// The unit branches, two nodes each, in the order the tree draws them. A branch opens with its mutation.
    public static readonly Def[] Nodes =
    {
        new(EvoNode.FlightMuscle, Mutation.Winged, "Flight Muscle", "Winged ants fly 10% faster per rank.", 3, 20, 10),
        new(EvoNode.LightShell, Mutation.Winged, "Wing Cases", "Winged ants take 12% more punishment per rank.", 2, 25, 15),
        new(EvoNode.VenomSac, Mutation.Stinger, "Venom Sac", "The sting's venom burns 25% harder per rank.", 3, 20, 10),
        new(EvoNode.LastingVenom, Mutation.Stinger, "Lasting Venom", "The venom burns 1.5 seconds longer per rank.", 2, 25, 15),
        new(EvoNode.Spinneret, Mutation.Web, "Spinneret", "Web ants pin for longer, from farther, and one more flyer per throw.", 2, 30, 20),
        new(EvoNode.SilkGlands, Mutation.Web, "Silk Glands", "Web ants throw again 2 seconds sooner per rank.", 2, 25, 15),
        new(EvoNode.Plates, Mutation.Armoured, "Shell Plates", "Armoured ants shrug off 5% more of every blow per rank.", 3, 20, 10),
        new(EvoNode.Mending, Mutation.Armoured, "Mending Shell", "Armoured ants mend 1% of their health a second per rank, away from a fight.", 2, 25, 15),
        new(EvoNode.DeepSting, Mutation.Scorpion, "Deep Sting", "The scorpion ants' sting numbs 1 second longer per rank, and their tails turn amber.", 2, 25, 15),
        new(EvoNode.NerveVenom, Mutation.Scorpion, "Nerve Venom", "A numbed enemy takes 10% more from every bite per rank.", 3, 20, 10),
    };

    public const int Count = 10;
    public static Def Of(EvoNode n) => Nodes[(int)n];
    public static float Cost(EvoNode n, int nextRank) => Of(n).Cost + Of(n).Step * (nextRank - 1);

    // what a rank does
    public const float FlightMuscleSpeed = 0.1f, LightShellHp = 0.12f;
    public const float VenomSacVenom = 0.14f, LastingVenomTime = 1.5f;   // +25% of the base 0.55 share per rank
    public const float SilkGlandsCd = 2f;
    public const float PlatesArmor = 0.05f, MendingRate = 0.01f;
    public const float DeepStingTime = 1f, NerveVenomDmg = 0.1f;
    public const int ScorpionRanks = 5;    // ranks in the scorpion ants' branch: the tail is black at none and amber at all (owner, 2026-11-15)

    /// Better brood: what each queen level above the first adds to every ant she lays from then on.
    public const float BroodHp = 0.06f, BroodBite = 0.05f;
    public const int BroodRoom = 4;        // and room in the nest per queen level
    /// Organic matter for the queen's next level (her levels cost no food).
    public static float QueenCost(int nextLevel) => nextLevel switch { 2 => 30f, 3 => 60f, 4 => 100f, _ => 150f };

    /// Organic matter to raise a general to this level: about 210 from level 1 to 10, cheap early so the first skills
    /// come in the first chapters and the ultimate's ranks are a real choice against the queen and the units.
    static readonly float[] GeneralCosts = { 0, 0, 7, 10, 13, 17, 22, 26, 32, 38, 47 };
    public static float GeneralCost(int nextLevel) => GeneralCosts[System.Math.Clamp(nextLevel, 0, Config.HeroMaxLevel)];
}

public sealed partial class Sim
{
    /// The evolve tree belongs to colonies; Lane War has its own match levels.
    public bool EvolveRules => Mode != GameMode.Lanes;

    public int Evo(int team, EvoNode n) => team is >= 0 and <= 1 ? Col[team].Evo[(int)n] : 0;

    public EvolveResult CanEvolve(int team, EvoNode n)
    {
        if (!EvolveRules || team is < 0 or > 1 || (int)n >= Evolve.Count) return EvolveResult.Locked;
        var c = Col[team];
        var d = Evolve.Of(n);
        if (!c.Alive || (c.Mutations & (int)d.Branch) == 0) return EvolveResult.Locked;
        if (c.Evo[(int)n] >= d.Ranks) return EvolveResult.Maxed;
        return c.Organic >= Evolve.Cost(n, c.Evo[(int)n] + 1) ? EvolveResult.Ok : EvolveResult.NoOrganic;
    }

    /// Spends organic matter on one rank of a unit branch's node.
    public bool EvolveNode(int team, EvoNode n)
    {
        if (CanEvolve(team, n) != EvolveResult.Ok) return false;
        var c = Col[team];
        c.Organic -= Evolve.Cost(n, c.Evo[(int)n] + 1);
        c.Evo[(int)n]++;
        if (Mission != null && team == 0) Mission.TreeBuys++;
        Reapply(team);
        Events.Add(new SimEvent { Kind = EvKind.Evolve, Pos = NestEntrance(team), Team = team, Type = Mutations.Unit(Evolve.Of(n).Branch), Tier = c.Evo[(int)n], Amount = (int)n + 1 });
        return true;
    }

    public EvolveResult CanLevelGeneral(int id)
    {
        if (!EvolveRules || id < 0 || id >= High) return EvolveResult.Locked;
        ref var a = ref Ants[id];
        if (!a.Alive || a.Team > 1 || !Config.IsHero(a.Type) || a.Summoned || a.CharmP1 > 0) return EvolveResult.Locked;
        if (a.Level >= Config.HeroMaxLevel) return EvolveResult.Maxed;
        // the campaign's rival generals grow only so far: its chapters set the rival's strength
        if (Mission != null && a.Team == Config.Enemy && a.Level >= Config.RivalHeroCap) return EvolveResult.Maxed;
        return Col[a.Team].Organic >= Evolve.GeneralCost(a.Level + 1) ? EvolveResult.Ok : EvolveResult.NoOrganic;
    }

    /// Spends organic matter on a general's next level: stronger, and a skill point for its tree.
    public bool LevelGeneral(int id)
    {
        if (CanLevelGeneral(id) != EvolveResult.Ok) return false;
        ref var a = ref Ants[id];
        Col[a.Team].Organic -= Evolve.GeneralCost(a.Level + 1);
        a.Level++;
        if (Mission != null && a.Team == 0) Mission.TreeBuys++;
        ApplyLevel(ref a);
        a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * 0.35f); // the moult heals
        a.SkPts++;
        // the campaign's scripted rival has no computer player to spend it: it learns on the spot
        if (Mission != null && a.Team == Config.Enemy) for (int s = BestLearn(a); s >= 0 && a.SkPts > 0; s = BestLearn(a)) LearnSkill(id, s);
        Events.Add(new SimEvent { Kind = EvKind.LevelUp, Pos = a.Pos, Team = a.Team, Type = a.Type, Who = id + 1 });
        return true;
    }

    /// Brood quality and the unit branches (ApplyLevel calls it).
    void EvolveStats(ref Ant a)
    {
        if (a.Team > 1) return;
        if (a.Brood > 0 && a.Type != AntType.Queen)
        {
            a.MaxHp *= 1 + Evolve.BroodHp * a.Brood;
            a.DmgMul *= 1 + Evolve.BroodBite * a.Brood;
        }
        if (a.Type == AntType.Flyer)
        {
            a.SpeedMul *= 1 + Evolve.FlightMuscleSpeed * Evo(a.Team, EvoNode.FlightMuscle);
            a.MaxHp *= 1 + Evolve.LightShellHp * Evo(a.Team, EvoNode.LightShell);
            if (a.Sting) a.DmgMul *= Mutations.StingBite;
        }
    }

    /// Armour an ant's evolve branch adds on top of its caste's (Damage).
    float EvolveArmor(in Ant t) => t.Type == AntType.Armoured && t.Team < 2 ? Evolve.PlatesArmor * Evo(t.Team, EvoNode.Plates) : 0f;
}
