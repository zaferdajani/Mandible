using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The nine general skills: three per general. Slot 0 (Q) is the general's old signature special, slot 1 (W) its
/// four-rank basic skill, slot 2 (E) its ultimate, open from level 6. Every number the sim uses lives here.
public enum SkillId : byte { WarCry, QueensPheromone, MandibleFrenzy, AcidBurst, BroodCall, CausticCloud, DiveBomb, TrailBlaze, TalonDive }

/// How a skill is aimed when the player casts it by hand: on its own (around the general), on an ant, or on a spot.
public enum SkillAim : byte { Self, Unit, Point }

public static class Skills
{
    public const int Slots = 3;
    public static readonly string[] Keys = { "Q", "W", "E" };

    public static SkillId At(AntType t, int slot) => t switch
    {
        AntType.Warlord => slot == 0 ? SkillId.WarCry : slot == 1 ? SkillId.QueensPheromone : SkillId.MandibleFrenzy,
        AntType.Matron => slot == 0 ? SkillId.AcidBurst : slot == 1 ? SkillId.BroodCall : SkillId.CausticCloud,
        _ => slot == 0 ? SkillId.DiveBomb : slot == 1 ? SkillId.TrailBlaze : SkillId.TalonDive,
    };

    public static bool Ultimate(SkillId s) => s is SkillId.MandibleFrenzy or SkillId.CausticCloud or SkillId.TalonDive;
    /// The W skills have four ranks, the rest three: 3 + 4 + 3 = the ten points a general earns from level 1 to 10.
    public static int MaxRank(SkillId s) => s is SkillId.QueensPheromone or SkillId.BroodCall or SkillId.TrailBlaze ? 4 : 3;
    /// Warcraft-style gates: basic ranks at levels 1, 3, 5, 7; the ultimate at 6, 8 and 10.
    public static int ReqLevel(SkillId s, int rank) => Ultimate(s) ? 4 + 2 * rank : 2 * rank - 1;

    public static SkillAim Aim(SkillId s) => s switch
    {
        SkillId.QueensPheromone or SkillId.TalonDive or SkillId.AcidBurst => SkillAim.Unit,
        SkillId.CausticCloud or SkillId.DiveBomb => SkillAim.Point,
        _ => SkillAim.Self,
    };

    /// How far from the general a hand-aimed cast may land.
    public static float Range(SkillId s) => s switch
    {
        SkillId.QueensPheromone => 10f, SkillId.AcidBurst => 11f, SkillId.CausticCloud => 13f,
        SkillId.DiveBomb => 12f, SkillId.TalonDive => 14f, _ => 0f,
    };

    /// Cooldown at this rank (before the general's level shortens the signature specials).
    public static float Cooldown(SkillId s, int rank, int level)
    {
        int r = System.Math.Clamp(rank, 1, MaxRank(s)) - 1;
        return s switch
        {
            SkillId.WarCry => Mathf.Max(10f, 22f - 2f * r - 0.4f * level),
            SkillId.QueensPheromone => 32f - 3f * r,
            SkillId.MandibleFrenzy => 70f - 10f * r,
            SkillId.AcidBurst => Mathf.Max(4.5f, 9f - 0.5f * r - 0.3f * level),
            SkillId.BroodCall => new[] { 40f, 42f, 45f, 50f }[r],
            SkillId.CausticCloud => 55f - 5f * r,
            SkillId.DiveBomb => Mathf.Max(5f, 11f - 0.8f * r - 0.4f * level),
            SkillId.TrailBlaze => 28f - 2f * r,
            _ => 45f - 5f * r,
        };
    }

    public static string Name(SkillId s) => s switch
    {
        SkillId.WarCry => "War Cry",
        SkillId.QueensPheromone => "Queen's Pheromone",
        SkillId.MandibleFrenzy => "Mandible Frenzy",
        SkillId.AcidBurst => "Acid Burst",
        SkillId.BroodCall => "Brood Call",
        SkillId.CausticCloud => "Caustic Cloud",
        SkillId.DiveBomb => "Dive Bomb",
        SkillId.TrailBlaze => "Trail Blaze",
        _ => "Talon Dive",
    };

    /// What the skill is, in one line of natural history: every power is a real ant trick pushed further by the strain.
    public static string Lore(SkillId s) => s switch
    {
        SkillId.WarCry => "An alarm pheromone and a stridulating roar: the brood's warriors bite harder and run faster.",
        SkillId.QueensPheromone => "Slave-maker queens spray propaganda pheromones that turn defenders on their own nest. This general carries the scent of a queen.",
        SkillId.MandibleFrenzy => "A soldier's last stand: it locks its jaws, ignores silk and stun, and drinks the hemolymph of what it bites.",
        SkillId.AcidBurst => "A full charge of formic acid from the gaster: it melts a cluster and pools on the soil.",
        SkillId.BroodCall => "She unseals a brood pouch: callow ants, pale and soft, pour out to fight for her and harden as they fight.",
        SkillId.CausticCloud => "She vents her acid gland into the air: a stinging mist that blinds, slows and burns.",
        SkillId.DiveBomb => "The winged commander crashes onto a crowd, scattering it, and rallies every warrior who sees it.",
        SkillId.TrailBlaze => "A recruitment trail laid on the run: nestmates who smell it march faster and hold their line.",
        _ => "A hawking stoop onto one chosen enemy: talons and mandibles together, and the victim pinned on its back.",
    };

    // ------------------------------------------------------------ numbers per rank (index rank - 1)
    // Queen's Pheromone: how many, how strong, how long. Tier: 1 soldiers, gunners, flying ants; 2 majors, beetles,
    // spiders; 3 wasps and stag beetles; 4 generals (for half the time). Workers, queens, bosses and mound guardians never.
    public static readonly int[] CharmCount = { 1, 2, 3, 5 };
    public static readonly int[] CharmTier = { 1, 2, 3, 4 };
    public static readonly int[] CharmLevel = { 2, 3, 5, 10 };
    public static readonly float[] CharmTime = { 8f, 10f, 12f, 14f };
    public const float CharmRadius = 4.5f;      // the cloud around the target: other victims are taken from within it

    public static int ControlTier(AntType t) => t switch
    {
        AntType.Soldier or AntType.Gunner or AntType.Flyer => 1,
        AntType.Major or AntType.Beetle or AntType.Spider => 2,
        AntType.Wasp or AntType.StagBeetle => 3,
        AntType.Warlord or AntType.Matron or AntType.Marshal => 4,
        _ => 99,
    };

    // Mandible Frenzy
    public static readonly float[] FrenzyTime = { 8f, 10f, 12f };
    public static readonly float[] FrenzyBite = { 1.35f, 1.5f, 1.7f };
    public static readonly float[] FrenzyLeech = { 0.2f, 0.3f, 0.4f };

    // War Cry: allies' bite bonus and duration, and the roar's shock as bites
    public static readonly float[] CryBite = { 1.3f, 1.4f, 1.5f };
    public static readonly float[] CryTime = { 8f, 10f, 12f };
    public static readonly float[] CryShock = { 1.0f, 1.4f, 1.8f };

    // Acid Burst: damage, radius and venom multiplier over the old special
    public static readonly float[] BurstMul = { 1f, 1.25f, 1.5f };
    public static readonly float[] BurstRadius = { 0f, 0.5f, 1f };

    // Brood Call: rank 1 small attackers, rank 2 large ones, rank 3 flying ants, rank 4 a mixed brood
    public static (AntType type, int n)[] Brood(int rank) => rank switch
    {
        1 => new[] { (AntType.Soldier, 3) },
        2 => new[] { (AntType.Major, 2) },
        3 => new[] { (AntType.Flyer, 4) },
        _ => new[] { (AntType.Soldier, 3), (AntType.Major, 2), (AntType.Flyer, 3), (AntType.Gunner, 2) },
    };
    public static readonly float[] BroodLife = { 30f, 35f, 40f, 45f };
    public static readonly int[] BroodMaxLevel = { 3, 3, 4, 5 };
    public const float HardenEvery = 6f;        // a callow hardens one level every few seconds

    // Caustic Cloud
    public static readonly float[] CloudRadius = { 5f, 6f, 7f };
    public static readonly float[] CloudTime = { 8f, 10f, 12f };
    public static readonly float[] CloudDps = { 14f, 22f, 30f };
    public static readonly float[] CloudSlow = { 0.3f, 0.4f, 0.5f };

    // Dive Bomb
    public static readonly float[] DiveMul = { 1f, 1.25f, 1.5f };
    public static readonly float[] DiveStun = { 0f, 0.3f, 0.6f };

    // Trail Blaze
    public static readonly float[] BlazeRadius = { 12f, 13f, 14f, 15f };
    public static readonly float[] BlazeSpeed = { 1.25f, 1.35f, 1.45f, 1.55f };
    public static readonly float[] BlazeArmor = { 0.1f, 0.15f, 0.2f, 0.25f };
    public static readonly float[] BlazeTime = { 6f, 7f, 8f, 10f };

    // Talon Dive
    public static readonly float[] TalonDamage = { 150f, 240f, 340f };
    public static readonly float[] TalonStun = { 2f, 2.5f, 3f };
    public const float TalonQueen = 0.4f;       // a queen is too big to pin: she takes 40% and is not stunned

    static string Pct(float m) => $"{(m - 1) * 100:0}%";

    /// The tooltip line for one rank: what changes at that rank, in numbers.
    public static string RankLine(SkillId s, int rank)
    {
        int r = System.Math.Clamp(rank, 1, MaxRank(s)) - 1;
        return s switch
        {
            SkillId.WarCry => $"Allies bite +{Pct(CryBite[r])} and run 20% faster for {CryTime[r]:0}s; the roar hits for {CryShock[r]:0.#} {(CryShock[r] == 1f ? "bite" : "bites")}.",
            SkillId.QueensPheromone => $"Turns {CharmCount[r]} {(r == 0 ? "enemy" : "enemies")} for {CharmTime[r]:0}s: " + r switch
            {
                0 => "soldiers, gunners and flying ants up to level 2.",
                1 => "also majors, beetles and spiders, up to level 3.",
                2 => "also wasps and stag beetles, up to level 5.",
                _ => "any level, even enemy generals (for half the time).",
            },
            SkillId.MandibleFrenzy => $"{FrenzyTime[r]:0}s: bite +{Pct(FrenzyBite[r])}, heals {FrenzyLeech[r] * 100:0}% of every bite, cannot be rooted or stunned.",
            SkillId.AcidBurst => $"{Pct(1 + (BurstMul[r] - 1))} stronger burst and pool{(r > 0 ? $", {BurstRadius[r]:0.#} wider" : "")}; venom pool for {Config.AcidPoolLife:0}s.",
            SkillId.BroodCall => r switch
            {
                0 => "3 callow soldiers: small, fast attackers.",
                1 => "2 callow majors: large, huge-headed crushers.",
                2 => "4 callow flying ants: they cross rocks and fight wasps.",
                _ => "A whole brood: 3 soldiers, 2 majors, 3 flying ants and 2 acid gunners.",
            } + $" They last {BroodLife[r]:0}s and harden one level every {HardenEvery:0}s, up to level {BroodMaxLevel[r]}.",
            SkillId.CausticCloud => $"A cloud {CloudRadius[r]:0} wide for {CloudTime[r]:0}s: {CloudDps[r]:0} acid a second, enemies {CloudSlow[r] * 100:0}% slower.",
            SkillId.DiveBomb => $"{Pct(1 + (DiveMul[r] - 1))} harder strike{(r > 0 ? $", stun +{DiveStun[r]:0.#}s" : "")}; rallies warriors within 12.",
            SkillId.TrailBlaze => $"Allies within {BlazeRadius[r]:0} run {Pct(BlazeSpeed[r])} faster and shrug off {BlazeArmor[r] * 100:0}% more of every blow for {BlazeTime[r]:0}s.",
            _ => $"Strikes one enemy for {TalonDamage[r]:0} (x bite growth) and pins it for {TalonStun[r]:0.#}s. A kill refunds half the cooldown.",
        };
    }

    public static string KeyOf(int slot) => Keys[System.Math.Clamp(slot, 0, 2)];
}

public sealed partial class Sim
{
    public static int Rank(in Ant a, int slot) => slot == 0 ? a.Sk0 : slot == 1 ? a.Sk1 : a.Sk2;
    public static float SkillCd(in Ant a, int slot) => slot == 0 ? a.AbilityCd : slot == 1 ? a.CdW : a.CdE;
    static void SetRank(ref Ant a, int slot, int r) { if (slot == 0) a.Sk0 = (byte)r; else if (slot == 1) a.Sk1 = (byte)r; else a.Sk2 = (byte)r; }
    static void SetCd(ref Ant a, int slot, float cd) { if (slot == 0) a.AbilityCd = cd; else if (slot == 1) a.CdW = cd; else a.CdE = cd; }

    /// Whether this general can put a point into this skill now.
    public static bool CanLearn(in Ant a, int slot)
    {
        if (!a.Alive || !Config.IsHero(a.Type) || a.SkPts <= 0 || slot < 0 || slot >= Skills.Slots) return false;
        var s = Skills.At(a.Type, slot);
        int r = Rank(a, slot);
        return r < Skills.MaxRank(s) && a.Level >= Skills.ReqLevel(s, r + 1);
    }

    public static bool AnyLearnable(in Ant a)
    {
        for (int s = 0; s < Skills.Slots; s++) if (CanLearn(a, s)) return true;
        return false;
    }

    /// A newly hatched general knows its signature at rank 1; every level above 1 is an unspent point.
    void InitSkills(ref Ant a)
    {
        if (!Config.IsHero(a.Type)) return;
        a.Sk0 = 1; a.Sk1 = 0; a.Sk2 = 0;
        a.SkPts = (byte)System.Math.Max(0, a.Level - 1);
        a.AutoMask = 7;
    }

    /// Restores a general's skills (Lane War respawn): ranks packed one per byte, the rest of its levels unspent.
    void RestoreSkills(ref Ant a, int packed)
    {
        if (packed == 0) { InitSkills(ref a); return; }
        a.Sk0 = (byte)(packed & 0xFF); a.Sk1 = (byte)((packed >> 8) & 0xFF); a.Sk2 = (byte)((packed >> 16) & 0xFF);
        a.AutoMask = 7;
        a.SkPts = (byte)System.Math.Max(0, a.Level - a.Sk0 - a.Sk1 - a.Sk2);
    }

    static int PackSkills(in Ant a) => a.Sk0 | a.Sk1 << 8 | a.Sk2 << 16;

    /// Spends a skill point (CmdKind.Learn). The new rank works at once.
    public bool LearnSkill(int id, int slot)
    {
        if (id < 0 || id >= High) return false;
        ref var a = ref Ants[id];
        if (!CanLearn(a, slot)) return false;
        int r = Rank(a, slot) + 1;
        SetRank(ref a, slot, r);
        a.SkPts--;
        if (r == 1 && slot > 0) SetCd(ref a, slot, 0); // a new skill is ready the moment it is learnt
        Events.Add(new SimEvent { Kind = EvKind.SkillLearned, Pos = a.Pos, Team = a.Team, Type = a.Type, Tier = r, Who = id + 1, Amount = slot });
        return true;
    }

    /// CmdKind.Cast: fire this skill as soon as it can, at the given ant or spot (or the best target near the general).
    public void CmdCast(int id, int slot, int target, int targetSerial, Vector2 at)
    {
        if (id < 0 || id >= High || slot < 0 || slot >= Skills.Slots) return;
        ref var a = ref Ants[id];
        if (!a.Alive || !Config.IsHero(a.Type) || Rank(a, slot) == 0) return;
        a.CastSlot = (byte)(slot + 1);
        a.CastReq = 2f;
        a.CastTarget = Valid(target, targetSerial) ? target : -1;
        a.CastSerial = targetSerial;
        a.CastAt = at;
    }

    /// CmdKind.SkillAuto: turns a skill's autocast on or off (right-click on its button).
    public void ToggleAuto(int id, int slot)
    {
        if (id < 0 || id >= High || slot < 0 || slot >= Skills.Slots) return;
        ref var a = ref Ants[id];
        if (!a.Alive || !Config.IsHero(a.Type)) return;
        a.AutoMask ^= (byte)(1 << slot);
    }

    /// The general with the highest-priority point to spend, for the computer players: ultimate, then the four-rank skill, then the signature.
    public static int BestLearn(in Ant a)
    {
        foreach (int s in new[] { 2, 1, 0 }) if (CanLearn(a, s)) return s;
        return -1;
    }

    /// Every tick: charm, summons hardening and fading, frenzy, haste and slow. Returns false when the ant has faded away.
    bool TickSkillState(int i, ref Ant a, float dt)
    {
        if (a.CharmP1 > 0 && (a.CharmT -= dt) <= 0) Uncharm(ref a);
        if (a.Summoned)
        {
            a.LifeT -= dt;
            if (a.LifeT <= 0) { Fade(i); return false; }
            a.HardenT += dt;
            if (a.HardenT >= Skills.HardenEvery && a.Level < a.SummonCap)
            {
                a.HardenT = 0;
                a.Level++;
                ApplyLevel(ref a);
                a.Hp = Mathf.Min(a.MaxHp, a.Hp + a.MaxHp * 0.15f);
                Events.Add(new SimEvent { Kind = EvKind.LevelUp, Pos = a.Pos, Team = a.Team, Type = a.Type });
            }
        }
        if (a.FrenzyT > 0) { a.FrenzyT -= dt; a.Root = 0; }
        a.HasteT -= dt;
        a.SlowT -= dt;
        a.CdW -= dt;
        a.CdE -= dt;
        return true;
    }

    void Uncharm(ref Ant a)
    {
        a.Team = a.CharmP1 - 1;
        a.CharmP1 = 0;
        a.CharmT = 0;
        a.Target = -1;
        a.State = AntState.Idle;
        a.Anchor = a.Pos;
        a.Forced = false;
        a.Selected = false;
    }

    void Charm(int j, int team, float time)
    {
        ref var b = ref Ants[j];
        if (b.CharmP1 > 0 && b.CharmP1 - 1 == team) { Uncharm(ref b); return; } // a nestmate's mind cleared
        if (b.CharmP1 == 0) b.CharmP1 = (byte)(b.Team + 1);
        b.Team = team;
        b.CharmT = time;
        b.Target = -1;
        b.State = AntState.Idle;
        b.Anchor = b.Pos;
        b.Forced = false;
        b.Selected = false;
        b.FeastOrder = false;
        b.Feasting = false;
        Events.Add(new SimEvent { Kind = EvKind.Charmed, Pos = b.Pos, Team = team, Type = b.Type, Who = j + 1, Amount = time });
    }

    /// A summoned callow's time is up: it slips away into the soil, leaving nothing to eat.
    void Fade(int id)
    {
        ref var t = ref Ants[id];
        t.Alive = false;
        if (t.SquadP1 > 0) Squads[t.SquadP1 - 1].Members.Remove(id);
        Events.Add(new SimEvent { Kind = EvKind.Fade, Pos = t.Pos, Team = t.Team, Type = t.Type, Who = id + 1 });
        _free.Push(id);
    }

    public bool Charmable(in Ant b, int rank)
    {
        if (!b.Alive || b.Guardian || b.Boss || b.Summoned) return false;
        int r = System.Math.Clamp(rank, 1, 4) - 1;
        return Skills.ControlTier(b.Type) <= Skills.CharmTier[r] && b.Level <= Skills.CharmLevel[r];
    }

    int Summon(int team, AntType type, Vector2 p, float life, int cap, int layer)
    {
        int id = Spawn(team, type, ClampMap(p), Rng.R(0, DMath.Tau));
        if (id < 0) return -1;
        ref var a = ref Ants[id];
        if (team < 2) Col[team].Count[(int)type]--; // a summons takes no room in the brood chambers
        a.Summoned = true;
        a.LifeT = life;
        a.SummonCap = (byte)cap;
        a.Layer = (byte)layer;
        a.State = AntState.Idle;
        return id;
    }

    int Pick(List<int> cands, System.Func<int, float> score)
    {
        int best = -1; float bs = float.MinValue;
        foreach (int j in cands) { float s = score(j); if (s > bs) { bs = s; best = j; } }
        return best;
    }

    /// Fires one skill if the moment is right (or it was ordered). Every cast is decided from sim state alone.
    bool CastSkill(int i, ref Ant a, UnitStats st, int slot, int rank, bool asked)
    {
        var s = Skills.At(a.Type, slot);
        int r = rank - 1;
        float lv = a.Level;
        float tierMul = Config.HeroTierMul(a.Type, a.HeroTier);
        var me = a.Pos;
        int ordered = asked && Valid(a.CastTarget, a.CastSerial) ? a.CastTarget : -1;
        bool hasSpot = asked && a.CastAt != Vector2.Zero;
        float cd = Skills.Cooldown(s, rank, a.Level);
        switch (s)
        {
            case SkillId.WarCry:
            {
                if (!asked && EnemiesNear(a.Pos, 10, a.Team, false, a.Layer).Count < 3) return false;
                float mul = (Skills.CryBite[r] + 0.03f * lv) * (1 + 0.1f * (tierMul - 1));
                float shock = st.Damage * a.DmgMul * Skills.CryShock[r];
                foreach (int j in EnemiesNear(a.Pos, Config.WarCryShockR, a.Team, false, a.Layer).ToArray())
                {
                    ref var b = ref Ants[j];
                    if (b.Type != AntType.Queen) { b.Pos = ClampMap(b.Pos + (b.Pos - a.Pos).Normalized() * 1.5f); b.Root = Mathf.Max(b.Root, 0.5f); }
                    Damage(j, shock, i);
                }
                foreach (int j in AlliesNear(a.Pos, 13, a.Team, a.Layer))
                {
                    ref var b = ref Ants[j];
                    if (Config.IsWarrior(b.Type)) { b.BuffMul = b.BuffT > 0 ? Mathf.Max(b.BuffMul, mul) : mul; b.BuffT = Mathf.Max(b.BuffT, Skills.CryTime[r] + 0.3f * lv); }
                }
                Events.Add(new SimEvent { Kind = EvKind.WarCry, Pos = a.Pos, Team = a.Team, Type = a.Type, Radius = 13 });
                break;
            }
            case SkillId.QueensPheromone:
            {
                int first = ordered;
                if (first >= 0 && (!Charmable(Ants[first], rank) || Ants[first].Team == a.Team || Ants[first].Pos.DistanceTo(a.Pos) > Skills.Range(s) + 2)) first = -1;
                var cands = EnemiesNear(hasSpot ? a.CastAt : a.Pos, hasSpot ? Skills.CharmRadius : Skills.Range(s), a.Team, false, a.Layer).ToArray();
                var ok = new List<int>();
                foreach (int j in cands) if (Charmable(Ants[j], rank)) ok.Add(j);
                if (first < 0) first = Pick(ok, j => Skills.ControlTier(Ants[j].Type) * 10 + Ants[j].Level - Ants[j].Pos.DistanceTo(me) * 0.1f);
                if (first < 0) return false;
                // the computer waits until the scent can take a real prize: a pair, or one big ant
                if (!asked && ok.Count < 2 && Skills.ControlTier(Ants[first].Type) < 2) return false;
                var at = Ants[first].Pos;
                var victims = new List<int> { first };
                ok.Remove(first);
                ok.Sort((x, y) =>
                {
                    int c = Ants[x].Pos.DistanceSquaredTo(at).CompareTo(Ants[y].Pos.DistanceSquaredTo(at));
                    return c != 0 ? c : x.CompareTo(y);
                });
                foreach (int j in ok)
                {
                    if (victims.Count >= Skills.CharmCount[r]) break;
                    if (Ants[j].Pos.DistanceTo(at) <= Skills.CharmRadius) victims.Add(j);
                }
                foreach (int j in victims) Charm(j, a.Team, Config.IsHero(Ants[j].Type) ? Skills.CharmTime[r] * 0.5f : Skills.CharmTime[r]);
                Events.Add(new SimEvent { Kind = EvKind.Pheromone, Pos = a.Pos, To = at, Team = a.Team, Type = a.Type, Radius = Skills.CharmRadius, Tier = victims.Count });
                break;
            }
            case SkillId.MandibleFrenzy:
            {
                if (!asked)
                {
                    int foes = EnemiesNear(a.Pos, 8, a.Team, false, a.Layer).Count;
                    bool big = a.State == AntState.Engage && Valid(a.Target, a.TargetSerial) && (Config.IsHero(Ants[a.Target].Type) || Ants[a.Target].Type == AntType.Queen || Ants[a.Target].Boss);
                    if (foes < 4 && !big && !(foes >= 1 && a.Hp < a.MaxHp * 0.45f)) return false;
                }
                a.FrenzyT = Skills.FrenzyTime[r];
                a.FrenzyRank = (byte)rank;
                a.Root = 0;
                Events.Add(new SimEvent { Kind = EvKind.Frenzy, Pos = a.Pos, Team = a.Team, Type = a.Type, Radius = 3, Tier = rank });
                break;
            }
            case SkillId.AcidBurst:
            {
                int tgt = ordered >= 0 ? ordered : Valid(a.Target, a.TargetSerial) && a.State == AntState.Engage ? a.Target : asked ? FindEnemy(a.Pos, a.Team, Skills.Range(s), true, a.Layer) : -1;
                if (tgt < 0 || Ants[tgt].Team == a.Team) return false;
                var to = Ants[tgt].Pos;
                if (a.Pos.DistanceTo(to) > Skills.Range(s)) return false;
                float rad = 3.5f + 0.15f * lv + Skills.BurstRadius[r];
                float dmg = (40 + 8 * lv) * a.DmgMul * Skills.BurstMul[r];
                foreach (int j in EnemiesNear(to, rad, a.Team, false, a.Layer).ToArray())
                {
                    ref var b = ref Ants[j];
                    b.PoisonT = 4;
                    b.PoisonDps = (5 + 1.5f * lv) * tierMul * Skills.BurstMul[r];
                    b.PoisonSrc = i;
                    Damage(j, dmg, i);
                }
                Pools.Add(new AcidPool { Pos = to, R = rad, Life = Config.AcidPoolLife, Max = Config.AcidPoolLife, Dps = (4 + 1.2f * lv) * tierMul * Skills.BurstMul[r], Team = a.Team, Src = i });
                Events.Add(new SimEvent { Kind = EvKind.AcidBurst, Pos = a.Pos, To = to, Team = a.Team, Type = a.Type, Radius = rad });
                break;
            }
            case SkillId.BroodCall:
            {
                if (!asked && EnemiesNear(a.Pos, 14, a.Team, false, a.Layer).Count < 3) return false;
                int foe = Valid(a.Target, a.TargetSerial) ? a.Target : FindEnemy(a.Pos, a.Team, 16, true, a.Layer);
                int k = 0;
                foreach (var (type, n) in Skills.Brood(rank))
                    for (int m = 0; m < n; m++, k++)
                    {
                        var p = a.Pos + MathX.Dir(a.Heading + DMath.Pi + (k - 4.5f) * 0.55f) * (a.Size * 0.6f + 1.4f + 0.3f * (k % 3));
                        int id = Summon(a.Team, type, p, Skills.BroodLife[r], Skills.BroodMaxLevel[r], a.Layer);
                        if (id < 0) continue;
                        ref var c = ref Ants[id];
                        if (foe >= 0 && Ants[foe].Alive) { c.Goal = Ants[foe].Pos; c.GoalLayer = a.Layer; c.State = AntState.AttackMove; }
                    }
                Events.Add(new SimEvent { Kind = EvKind.Summon, Pos = a.Pos, Team = a.Team, Type = a.Type, Radius = 3, Tier = rank });
                break;
            }
            case SkillId.CausticCloud:
            {
                Vector2 to;
                float rad = Skills.CloudRadius[r];
                if (hasSpot) to = a.CastAt;
                else if (ordered >= 0) to = Ants[ordered].Pos;
                else
                {
                    // the computer aims at the thickest crowd in reach
                    var foes = EnemiesNear(a.Pos, Skills.Range(s), a.Team, true, a.Layer).ToArray();
                    int best = -1, bestN = asked ? 0 : 3;
                    foreach (int j in foes)
                    {
                        int n = 0;
                        foreach (int q in foes) if (Ants[q].Pos.DistanceSquaredTo(Ants[j].Pos) < rad * rad * 0.5f) n++;
                        if (n > bestN) { bestN = n; best = j; }
                    }
                    if (best < 0) return false;
                    to = Ants[best].Pos;
                }
                if (a.Pos.DistanceTo(to) > Skills.Range(s)) to = a.Pos + (to - a.Pos).Normalized() * Skills.Range(s);
                Pools.Add(new AcidPool { Pos = to, R = rad, Life = Skills.CloudTime[r], Max = Skills.CloudTime[r], Dps = Skills.CloudDps[r] * tierMul, Team = a.Team, Src = i, Slow = Skills.CloudSlow[r], Cloud = true });
                Events.Add(new SimEvent { Kind = EvKind.Cloud, Pos = a.Pos, To = to, Team = a.Team, Type = a.Type, Radius = rad });
                break;
            }
            case SkillId.DiveBomb:
            {
                Vector2 to;
                if (hasSpot) to = a.CastAt;
                else if (ordered >= 0) to = Ants[ordered].Pos;
                else
                {
                    var foes = EnemiesNear(a.Pos, 12, a.Team, true, a.Layer);
                    if (foes.Count < (asked ? 1 : 2)) return false;
                    to = Ants[foes[0]].Pos;
                }
                if (a.Pos.DistanceTo(to) > Skills.Range(s)) to = a.Pos + (to - a.Pos).Normalized() * Skills.Range(s);
                float rad = 3.2f + 0.12f * lv;
                float dmg = (35 + 7 * lv) * a.DmgMul * Skills.DiveMul[r];
                if (a.Pos.DistanceSquaredTo(to) > 0.01f) a.Pos = ClampMap(to + (a.Pos - to).Normalized() * 1.5f);
                a.Alt = 0.6f;
                foreach (int j in EnemiesNear(to, rad, a.Team, true, a.Layer).ToArray())
                {
                    Ants[j].Root = Mathf.Max(Ants[j].Root, 1.2f + 0.1f * lv + Skills.DiveStun[r]);
                    Damage(j, dmg, i);
                }
                int rallied = 0;
                float rb = Config.RallyBite + 0.05f * r;
                foreach (int j in AlliesNear(to, 12, a.Team, a.Layer))
                {
                    ref var b = ref Ants[j];
                    if (j == i || !Config.IsWarrior(b.Type)) continue;
                    b.BuffMul = b.BuffT > 0 ? Mathf.Max(b.BuffMul, rb) : rb; b.BuffT = Mathf.Max(b.BuffT, Config.RallyTime);
                    rallied++;
                }
                Events.Add(new SimEvent { Kind = EvKind.DiveBomb, Pos = to, Team = a.Team, Type = a.Type, Radius = rad });
                if (rallied > 0) Events.Add(new SimEvent { Kind = EvKind.Rally, Pos = to, Team = a.Team, Type = a.Type, Radius = 12 });
                break;
            }
            case SkillId.TrailBlaze:
            {
                float rad = Skills.BlazeRadius[r];
                var mates = AlliesNear(a.Pos, rad, a.Team, a.Layer);
                if (!asked && (mates.Count < 5 || EnemiesNear(a.Pos, 18, a.Team, true, a.Layer).Count < 3)) return false;
                foreach (int j in mates)
                {
                    ref var b = ref Ants[j];
                    if (b.Type == AntType.Queen) continue;
                    b.HasteT = Skills.BlazeTime[r];
                    b.HasteMul = Skills.BlazeSpeed[r];
                    b.GuardArmor = Skills.BlazeArmor[r];
                }
                Events.Add(new SimEvent { Kind = EvKind.TrailBlaze, Pos = a.Pos, Team = a.Team, Type = a.Type, Radius = rad, Tier = rank });
                break;
            }
            case SkillId.TalonDive:
            {
                int tgt = ordered;
                if (tgt < 0)
                {
                    var foes = EnemiesNear(a.Pos, Skills.Range(s), a.Team, false, a.Layer).ToArray();
                    // the prize first: a general, a big bug, a major; the queen only when she is all there is
                    tgt = Pick(new List<int>(foes), j =>
                    {
                        ref var b = ref Ants[j];
                        float v = Config.IsHero(b.Type) ? 100 : b.Type == AntType.Queen ? 5 : Config.IsBug(b.Type) ? 30 + b.Level : b.Type == AntType.Major ? 25 : b.Type == AntType.Worker ? 1 : 10;
                        return v + b.Level - b.Pos.DistanceTo(me) * 0.2f;
                    });
                    if (tgt < 0) return false;
                    var tb = Ants[tgt];
                    if (!asked && !Config.IsHero(tb.Type) && !Config.IsBug(tb.Type) && tb.Type != AntType.Major && tb.Type != AntType.Queen) return false;
                }
                ref var t = ref Ants[tgt];
                if (t.Team == a.Team || a.Pos.DistanceTo(t.Pos) > Skills.Range(s) + 2) return false;
                var to = t.Pos;
                bool queen = t.Type == AntType.Queen;
                float dmg = Skills.TalonDamage[r] * a.DmgMul * (queen ? Skills.TalonQueen : 1f);
                if (a.Pos.DistanceSquaredTo(to) > 0.01f) a.Pos = ClampMap(to + (a.Pos - to).Normalized() * 1.2f);
                a.Alt = 0.6f;
                if (!queen) t.Root = Mathf.Max(t.Root, Skills.TalonStun[r]);
                Damage(tgt, dmg, i);
                if (!Ants[tgt].Alive) cd *= 0.5f;
                Events.Add(new SimEvent { Kind = EvKind.TalonDive, Pos = to, Team = a.Team, Type = a.Type, Radius = 2, Tier = rank });
                break;
            }
        }
        SetCd(ref a, slot, cd);
        a.AttackAnim = 1;
        return true;
    }

    readonly List<int> _allies = new();
    /// Live ants of this colony near a point (a fresh list each call is not needed: the caller must not nest calls).
    List<int> AlliesNear(Vector2 p, float r, int team, int layer)
    {
        _allies.Clear();
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
                    if (b.Alive && b.Team == team && b.Layer == layer && b.Pos.DistanceSquaredTo(p) < r * r) _allies.Add(j);
                }
            }
        }
        return _allies;
    }

    /// Generals use their skills: on their own when autocast is on (and the fight calls for it), or when ordered.
    void HeroAbility(int i, ref Ant a, UnitStats st)
    {
        for (int slot = 0; slot < Skills.Slots; slot++)
        {
            int rank = Rank(a, slot);
            if (rank == 0 || SkillCd(a, slot) > 0) continue;
            bool asked = a.CastSlot == slot + 1 && a.CastReq > 0;
            if (!asked && (a.Manual || (a.AutoMask & (1 << slot)) == 0)) continue;
            if (CastSkill(i, ref a, st, slot, rank, asked))
            {
                if (asked) { a.CastSlot = 0; a.CastReq = 0; a.CastTarget = -1; a.CastAt = Vector2.Zero; }
                return; // one skill a moment
            }
        }
    }

    /// Validation hooks (GeneralValidation): never called by the game itself.
    public void TestFeed(int id, float organic) => Nourish(id, ref Ants[id], organic);
    public void TestSetRank(int id, int slot, int rank) { SetRank(ref Ants[id], slot, rank); SetCd(ref Ants[id], slot, 0); }
}
