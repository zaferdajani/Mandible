using Godot;

namespace Mandible;

/// The scorpion (docs/progression.md 1d, level-design.md section 5). Three things live here:
///  - the scorpion ant's tail (Mutation.Scorpion): one strike every Config.TailCooldown that ignores armour and numbs;
///  - numbness itself (Ant.NumbT): the victim walks and bites slower;
///  - the wild scorpion: under its stone (Ant.Home) only a few jaws reach it and it heals; a fresh carcass on open
///    ground draws it out; its tail kills what stands in front of it unless thrown silk holds the tail (Ant.PinT);
///    hurt, it runs for the stone unless something heavy stands in the mouth.
public sealed partial class Sim
{
    public const float DenRadius = 3.2f;

    /// A wild scorpion sitting in the mouth of its burrow.
    public bool Denned(in Ant a) => a.Type == AntType.Scorpion && a.Team == Config.Wild && !a.Raider && a.Pos.DistanceSquaredTo(a.Home) < DenRadius * DenRadius;

    /// Numbs `t`: it walks and bites slower for the sting's time.
    void Numb(ref Ant t, float time)
    {
        if (!t.Alive || t.Type == AntType.Queen) return;
        t.NumbT = Mathf.Max(t.NumbT, time);
        t.SlowMul = t.SlowT > 0 ? Mathf.Max(t.SlowMul, Config.NumbSlow) : Config.NumbSlow;
        t.SlowT = Mathf.Max(t.SlowT, time);
    }

    /// The tail of a scorpion ant or a young scorpion, on the bite that lands while it is ready. Returns the blow's
    /// multiplier and whether it goes between the plates.
    float TailStrike(int i, ref Ant a, ref Ant t, out bool pierce)
    {
        pierce = false;
        if (a.Type != AntType.ScorpionAnt && a.Type != AntType.Scorpling) return 1f;
        if (a.TailCd > 0 || !t.Alive) return 1f;
        a.TailCd = a.Type == AntType.Scorpling ? Config.TailCooldown * 0.8f : Config.TailCooldown;
        pierce = a.Type == AntType.ScorpionAnt;
        Numb(ref t, Config.NumbTime + (a.Team < 2 ? Evolve.DeepStingTime * Evo(a.Team, EvoNode.DeepSting) : 0f));
        Events.Add(new SimEvent { Kind = EvKind.Sting, Pos = t.Pos, Team = a.Team, Type = a.Type });
        if (Mission != null && a.Team == 0) Mission.TailStings++;
        return a.Type == AntType.Scorpling ? 1.2f : Config.TailDamage;
    }

    /// What a numbed victim takes from every blow of a colony that bought Nerve Venom (Damage).
    float NerveMul(in Ant t, int attacker) =>
        t.NumbT > 0 && attacker >= 0 && Ants[attacker].Team < 2 ? 1f + Evolve.NerveVenomDmg * Evo(Ants[attacker].Team, EvoNode.NerveVenom) : 1f;

    static bool HeavyBody(AntType t) => t is AntType.Major or AntType.Armoured or AntType.Queen or AntType.StagBeetle || Config.IsHero(t);

    /// Something heavy stands in the mouth of the burrow: the scorpion cannot back in.
    bool DenBlocked(in Ant a)
    {
        for (int j = 0; j < High; j++)
        {
            ref var b = ref Ants[j];
            if (b.Alive && b.Team != a.Team && b.Layer == 0 && HeavyBody(b.Type) && b.Pos.DistanceSquaredTo(a.Home) < (DenRadius + 1.5f) * (DenRadius + 1.5f)) return true;
        }
        return false;
    }

    /// A fresh carcass on open ground within reach of the stone.
    FoodSource Bait(in Ant a)
    {
        FoodSource best = null; float bd = Config.ScorpionBaitRange * Config.ScorpionBaitRange;
        foreach (var f in W.Food)
        {
            if (f.Kind != FoodKind.Corpse || f.Amount <= 0 || f.Husk) continue; // fresh meat only: a dried husk keeps its part
            float d = f.Pos.DistanceSquaredTo(a.Home);
            if (d < bd) { bd = d; best = f; }
        }
        return best;
    }

    /// The wild scorpion's own behaviour, run before the state machine. True: it has chosen where to walk this tick.
    bool ScorpionTick(int i, ref Ant a, UnitStats st, float dt, bool think, ref Vector2 desired, ref float speedMul)
    {
        bool denned = Denned(a);
        if (denned && a.Hp < a.MaxHp) a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * Config.ScorpionHeal * dt);

        // the tail: whatever ant stands in front of it (anything at all in the mouth of the burrow)
        if (a.SpecialCd <= 0 && a.PinT <= 0)
        {
            float reach = st.Reach + a.Size * 0.3f + 1.6f;
            int victim = -1; float bd = float.MaxValue;
            for (int j = 0; j < High; j++)
            {
                ref var b = ref Ants[j];
                if (!b.Alive || b.Team == a.Team || b.Layer != a.Layer || b.Alt > 2.2f) continue;
                var to = b.Pos - a.Pos;
                float d = to.Length() - b.Size * 0.38f;
                if (d > reach) continue;
                if (!denned && Mathf.Abs(MathX.AngleDiff(a.Heading, to.AngleD())) > Config.ScorpionArc) continue;
                if (d < bd) { bd = d; victim = j; }
            }
            if (victim >= 0)
            {
                ref var v = ref Ants[victim];
                a.SpecialCd = denned ? 1.2f : 2f;
                a.AttackAnim = 1;
                Events.Add(new SimEvent { Kind = EvKind.Sting, Pos = v.Pos, Team = a.Team, Type = a.Type });
                if (HeavyBody(v.Type)) { Numb(ref v, Config.NumbTime); Damage(victim, st.Damage * a.DmgMul * Config.ScorpionSting, i); }
                else Damage(victim, v.MaxHp * 4f, i, pierce: true);   // one strike kills an ant
            }
        }

        // hurt: back under the stone, unless the mouth is held or silk holds its legs
        if (!denned && a.Hp < a.MaxHp * Config.ScorpionFlee && a.Root <= 0 && !DenBlocked(a))
        {
            a.State = AntState.Idle; a.Target = -1; a.Anchor = a.Home;
            desired = a.Home - a.Pos; speedMul = 1.25f;
            return true;
        }
        if (denned)
        {
            a.Anchor = a.Home;
            // hurt, it does not come out after what it cannot reach from the mouth
            if (a.State == AntState.Engage && Valid(a.Target, a.TargetSerial) && a.Hp < a.MaxHp * 0.9f
                && a.Pos.DistanceTo(Ants[a.Target].Pos) > st.Reach + a.Size * 0.3f + Ants[a.Target].Size * 0.38f + 1f) { a.State = AntState.Idle; a.Target = -1; }
        }
        if (a.State != AntState.Idle) return false;

        // meat on open ground: it walks out and feeds
        if (a.Hp > a.MaxHp * 0.8f && Bait(a) is { } meat)
        {
            var to = meat.Pos - a.Pos;
            if (think)
            {
                int e = FindEnemy(a.Pos, a.Team, st.Aggro * 0.6f, false, a.Layer);
                if (e >= 0) { Engage(ref a, e, AntState.Idle); a.Anchor = a.Pos; return false; }
            }
            a.Anchor = a.Pos;
            if (to.Length() > meat.Radius + a.Size * 0.35f) { desired = to; speedMul = 0.7f; return true; }
            meat.Amount = Mathf.Max(0, meat.Amount - 1.5f * dt);
            if (think) a.AttackAnim = 1;
            desired = Vector2.Zero;
            return true;
        }
        a.Anchor = a.Home;
        return false;
    }

    /// A scorpion's death: the young on her back spill off and hunt.
    void ScorpionDeath(ref Ant t)
    {
        if (t.Type != AntType.Scorpion || t.Level < 3) return;
        int n = System.Math.Min(8, t.Level + 2);
        var at = t.Pos;
        for (int k = 0; k < n; k++)
        {
            float ang = k * 6.2831853f / n;
            int id = Spawn(Config.Wild, AntType.Scorpling, ClampMap(at + MathX.Dir(ang) * (1.2f + 0.3f * (k % 3))), ang);
            if (id < 0) continue;
            Ants[id].Home = at; Ants[id].Anchor = Ants[id].Pos; // idle by the body: a core on the move draws them (Mutations.LureRange)
        }
    }
}
