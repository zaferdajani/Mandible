using System.Collections.Generic;
using Godot;

namespace Mandible;

/// Every skill in the pool. Generals, rival colonies' generals and the wild bugs all draw from it: a unit's loadout
/// (data/loadouts.json) lists which of these it has, in slot order (Q W E R). The numbers live in Skills.Pool.
public enum SkillId : byte
{
    WarCry, QueensPheromone, MandibleFrenzy, AcidBurst, SpawnBrood, FormicMist, DiveBomb, TrailBlaze, TalonDive,
    ShellWall, MandibleSweep, AntlerCharge, WebSnare, Lunge, VenomSting, CleavingMandibles,
    RallyScent, LivingBridge, ScoutSwarm, ThrallRaid,
}

/// How a skill is used: on its own (around the caster), at an ant, at a spot, or always on (it changes the bite).
public enum SkillAim : byte { Self, Unit, Point, Passive }

/// When the computer (and autocast) fires a skill. Crowd: at least AiCount enemies within AiRadius. Prize: a worthy
/// target in range (AiCount: how many of the right kind make it worth it). Hurt: below AiHealth of its health, or AiCount
/// enemies crowding it. Rally: at least AiCount nestmates and AiCount2 enemies around. Target: whenever its current
/// fight is in range. Bridge: its fight (or its march) lies across a rock. Raid: an enemy nest is within range.
public enum AiUse : byte { Crowd, Prize, Hurt, Rally, Target, Bridge, Raid }

/// One skill as data: names, ranks and the level each rank opens at, aim and range, cooldown, how the computer uses
/// it, and every number per rank (V: name -> values by rank). Behaviour code reads only these.
public sealed class SkillDef
{
    public SkillId Id;
    public string Name, Lore;
    public int[] Unlock;                 // level each rank opens at (its length is the number of ranks)
    public SkillAim Aim;
    public float Range;
    public float[] Cd;                   // cooldown by rank...
    public float CdLevel, CdMin;         // ...minus this per level of the caster (above CdFrom), never below CdMin
    public int CdFrom;                   // 0: generals, 1: bugs (their old formulas counted levels above the first)
    public AiUse Ai;
    public int AiCount, AiCount2;
    public float AiRadius, AiHealth = 0.6f;
    public int LearnPriority;            // computer players spend points on the highest first
    public bool Ultimate;
    public Dictionary<string, float[]> V = new();
    public (AntType type, int n)[][] Summons; // Brood Call: what each rank calls
    public System.Func<int, string> Line; // the tooltip line for a rank (1-based)
    public int MaxRank => Unlock.Length;
    public float N(string key, int rank) { var a = V[key]; return a[System.Math.Clamp(rank, 1, a.Length) - 1]; }
}

public static class Skills
{
    public const int MaxSlots = 4;
    public static readonly string[] Keys = { "Q", "W", "E", "R" };
    static readonly int[] Basic3 = { 1, 3, 5 }, Basic4 = { 1, 3, 5, 7 }, Ult3 = { 6, 8, 10 }, Npc = { 1, 3, 5 }; // the bugs' specials: generals rank them up; the bugs keep rank 1 and grow with their level

    static string Pct(float m) => $"{(m - 1) * 100:0}%";

    /// THE POOL. To add a skill: an id in SkillId, an entry here, its behaviour in Sim.CastSkill (or in the bite, for a
    /// passive), and put it in a loadout. Numbers per rank are index rank - 1.
    public static readonly SkillDef[] Pool = Build();

    static SkillDef[] Build()
    {
        var list = new List<SkillDef>
        {
            new() { Id = SkillId.WarCry, Name = "War Cry", Unlock = Basic3, Aim = SkillAim.Self, Cd = new[] { 22f, 20f, 18f }, CdLevel = 0.4f, CdMin = 10f,
                Ai = AiUse.Crowd, AiCount = 3, AiRadius = 10f, LearnPriority = 1,
                Lore = "An alarm pheromone and a stridulating roar: the brood's warriors bite harder and run faster.",
                V = { ["bite"] = new[] { 1.3f, 1.4f, 1.5f }, ["time"] = new[] { 8f, 10f, 12f }, ["shock"] = new[] { 1.0f, 1.4f, 1.8f } } },
            new() { Id = SkillId.QueensPheromone, Name = "Queen's Pheromone", Unlock = Basic4, Aim = SkillAim.Unit, Range = 10f, Cd = new[] { 32f, 29f, 26f, 23f },
                Ai = AiUse.Prize, AiCount = 2, AiRadius = 10f, LearnPriority = 2,
                Lore = "Slave-maker queens spray propaganda pheromones that turn defenders on their own nest. This general carries the scent of a queen.",
                V = { ["count"] = new[] { 1f, 2f, 3f, 5f }, ["tier"] = new[] { 1f, 2f, 3f, 4f }, ["level"] = new[] { 2f, 3f, 5f, 10f }, ["time"] = new[] { 8f, 10f, 12f, 14f }, ["cloud"] = new[] { 4.5f } } },
            new() { Id = SkillId.MandibleFrenzy, Name = "Mandible Frenzy", Unlock = Ult3, Ultimate = true, Aim = SkillAim.Self, Cd = new[] { 70f, 60f, 50f },
                Ai = AiUse.Hurt, AiCount = 4, AiRadius = 8f, AiHealth = 0.45f, LearnPriority = 3,
                Lore = "A soldier's last stand: it locks its jaws, ignores silk and stun, and drinks the hemolymph of what it bites.",
                V = { ["time"] = new[] { 8f, 10f, 12f }, ["bite"] = new[] { 1.35f, 1.5f, 1.7f }, ["leech"] = new[] { 0.2f, 0.3f, 0.4f } } },
            new() { Id = SkillId.AcidBurst, Name = "Acid Burst", Unlock = Basic3, Aim = SkillAim.Unit, Range = 11f, Cd = new[] { 9f, 8.5f, 8f }, CdLevel = 0.3f, CdMin = 4.5f,
                Ai = AiUse.Target, AiRadius = 11f, LearnPriority = 1,
                Lore = "A full charge of formic acid from the gaster: it melts a cluster and pools on the soil.",
                V = { ["mul"] = new[] { 1f, 1.25f, 1.5f }, ["wider"] = new[] { 0f, 0.5f, 1f } } },
            new() { Id = SkillId.SpawnBrood, Name = "Spawn Brood", Unlock = Basic4, Aim = SkillAim.Self, Cd = new[] { 40f, 42f, 45f, 50f },
                Ai = AiUse.Crowd, AiCount = 3, AiRadius = 14f, LearnPriority = 2,
                Lore = "She unseals a brood pouch: callow ants, pale and soft, pour out to fight for her and harden as they fight.",
                V = { ["life"] = new[] { 30f, 35f, 40f, 45f }, ["cap"] = new[] { 3f, 3f, 4f, 5f }, ["harden"] = new[] { 6f } },
                Summons = new[]
                {
                    new[] { (AntType.Soldier, 3) }, new[] { (AntType.Major, 2) }, new[] { (AntType.Flyer, 4) },
                    new[] { (AntType.Soldier, 3), (AntType.Major, 2), (AntType.Flyer, 3), (AntType.Gunner, 2) },
                } },
            new() { Id = SkillId.FormicMist, Name = "Formic Mist", Unlock = Ult3, Ultimate = true, Aim = SkillAim.Point, Range = 13f, Cd = new[] { 55f, 50f, 45f },
                Ai = AiUse.Crowd, AiCount = 3, AiRadius = 13f, LearnPriority = 3,
                Lore = "She vents her acid gland into the air: a stinging mist that blinds, slows and burns.",
                V = { ["radius"] = new[] { 5f, 6f, 7f }, ["time"] = new[] { 8f, 10f, 12f }, ["dps"] = new[] { 14f, 22f, 30f }, ["slow"] = new[] { 0.3f, 0.4f, 0.5f } } },
            new() { Id = SkillId.DiveBomb, Name = "Dive Bomb", Unlock = Basic3, Aim = SkillAim.Point, Range = 12f, Cd = new[] { 11f, 10.2f, 9.4f }, CdLevel = 0.4f, CdMin = 5f,
                Ai = AiUse.Crowd, AiCount = 2, AiRadius = 12f, LearnPriority = 1,
                Lore = "The winged commander crashes onto a crowd, scattering it, and rallies every warrior who sees it.",
                V = { ["mul"] = new[] { 1f, 1.25f, 1.5f }, ["stun"] = new[] { 0f, 0.3f, 0.6f } } },
            new() { Id = SkillId.TrailBlaze, Name = "Trail Blaze", Unlock = Basic4, Aim = SkillAim.Self, Cd = new[] { 28f, 26f, 24f, 22f },
                Ai = AiUse.Rally, AiCount = 5, AiCount2 = 3, AiRadius = 18f, LearnPriority = 2,
                Lore = "A recruitment trail laid on the run: nestmates who smell it march faster and hold their line.",
                V = { ["radius"] = new[] { 12f, 13f, 14f, 15f }, ["speed"] = new[] { 1.25f, 1.35f, 1.45f, 1.55f }, ["armor"] = new[] { 0.1f, 0.15f, 0.2f, 0.25f }, ["time"] = new[] { 6f, 7f, 8f, 10f } } },
            new() { Id = SkillId.TalonDive, Name = "Talon Dive", Unlock = Ult3, Ultimate = true, Aim = SkillAim.Unit, Range = 14f, Cd = new[] { 45f, 40f, 35f },
                Ai = AiUse.Prize, AiCount = 1, AiRadius = 14f, LearnPriority = 3,
                Lore = "A hawking stoop onto one chosen enemy: talons and mandibles together, and the victim pinned on its back.",
                V = { ["damage"] = new[] { 150f, 240f, 340f }, ["stun"] = new[] { 2f, 2.5f, 3f }, ["queen"] = new[] { 0.4f } } },
            // ---- the wild bugs' specials, in the same pool: any general can be given them too
            new() { Id = SkillId.ShellWall, Name = "Shell Wall", Unlock = Npc, Aim = SkillAim.Self, Cd = new[] { 13f, 12.4f, 11.8f }, CdLevel = 0.6f, CdFrom = 1, CdMin = 5f,
                Ai = AiUse.Hurt, AiCount = 3, AiRadius = 1.6f, AiHealth = 0.6f, LearnPriority = 1,
                Lore = "Swarmed or hurt, a beetle locks its wing cases down and weathers the storm.",
                V = { ["armor"] = new[] { Config.ShellWallArmor, Config.ShellWallArmor + 0.05f, Config.ShellWallArmor + 0.1f }, ["time"] = new[] { Config.ShellWallTime, Config.ShellWallTime + 0.6f, Config.ShellWallTime + 1.2f } } },
            new() { Id = SkillId.MandibleSweep, Name = "Mandible Sweep", Unlock = Npc, Aim = SkillAim.Self, Cd = new[] { 7f, 6.6f, 6.2f }, CdLevel = 0.4f, CdFrom = 1, CdMin = 3f,
                Ai = AiUse.Crowd, AiCount = 2, AiRadius = 1.2f, LearnPriority = 1,
                Lore = "Crushing jaws swung through everything crowding the head, hurling it back.",
                V = { ["bites"] = new[] { 1.5f, 1.7f, 1.9f }, ["push"] = new[] { 1.2f, 1.4f, 1.6f } } },
            new() { Id = SkillId.AntlerCharge, Name = "Antler Charge", Unlock = Npc, Aim = SkillAim.Unit, Range = 12f, Cd = new[] { 9f, 8.5f, 8f }, CdLevel = 0.5f, CdFrom = 1, CdMin = 4f,
                Ai = AiUse.Target, AiRadius = 12f, LearnPriority = 1,
                Lore = "Head down, a charge at a run: whatever the antler jaws meet is tossed through the air and left on its back.",
                V = { ["bites"] = new[] { Config.TossDamage, Config.TossDamage + 0.4f, Config.TossDamage + 0.8f }, ["throw"] = new[] { Config.TossThrow }, ["stun"] = new[] { Config.TossStun, Config.TossStun + 0.3f, Config.TossStun + 0.6f } } },
            new() { Id = SkillId.WebSnare, Name = "Web Snare", Unlock = Npc, Aim = SkillAim.Unit, Range = 16f, Cd = new[] { 6.5f, 6f, 5.5f }, CdLevel = 0.5f, CdFrom = 1, CdMin = 3f,
                Ai = AiUse.Target, AiRadius = 16f, LearnPriority = 1,
                Lore = "A sticky blob of silk that roots every ant it lands on.",
                V = { ["radius"] = new[] { 2.2f, 2.55f, 2.9f }, ["life"] = new[] { 3.5f, 4.1f, 4.7f }, ["root"] = new[] { 1.8f, 2.25f, 2.7f } } },
            new() { Id = SkillId.Lunge, Name = "Lunge", Unlock = Npc, Aim = SkillAim.Unit, Range = 10f, Cd = new[] { 7f, 6.6f, 6.2f }, CdLevel = 0.4f, CdFrom = 1, CdMin = 3f,
                Ai = AiUse.Target, AiRadius = 10f, LearnPriority = 1,
                Lore = "A pounce onto prey wrapped in silk, fangs first.",
                V = { ["bites"] = new[] { Config.LungeDamage, Config.LungeDamage + 0.3f, Config.LungeDamage + 0.6f } } },
            new() { Id = SkillId.VenomSting, Name = "Venom Sting", Unlock = Npc, Aim = SkillAim.Passive, Cd = new[] { 5f, 4.6f, 4.2f }, CdLevel = 0.4f, CdFrom = 1, CdMin = 2f,
                Ai = AiUse.Target, LearnPriority = 1,
                Lore = "Every few seconds the bite is a sting: a heavy blow that keeps burning, and then a climb out of reach (+0.3x per level).",
                V = { ["bites"] = new[] { Config.StingDamage, Config.StingDamage + 0.3f, Config.StingDamage + 0.6f } } },
            new() { Id = SkillId.CleavingMandibles, Name = "Cleaving Mandibles", Unlock = Basic3, Aim = SkillAim.Passive, Cd = new[] { 0f },
                Ai = AiUse.Target, LearnPriority = 1,
                Lore = "Wide, heavy jaws: every bite also catches the enemies crowding the victim.",
                V = { ["share"] = new[] { 0.45f, 0.55f, 0.65f }, ["targets"] = new[] { 1f, 2f, 3f } } },
            // ---- the owner's new skills
            new() { Id = SkillId.RallyScent, Name = "Rally Scent", Unlock = Basic3, Aim = SkillAim.Point, Range = 14f, Cd = new[] { 24f, 22f, 20f },
                Ai = AiUse.Rally, AiCount = 4, AiCount2 = 2, AiRadius = 16f, LearnPriority = 2,
                Lore = "A spot marked with alarm pheromone: nestmates near it run faster and bite harder, and idle ones come running.",
                V = { ["radius"] = new[] { 8f, 10f, 12f }, ["speed"] = new[] { 1.2f, 1.3f, 1.4f }, ["bite"] = new[] { 1.15f, 1.25f, 1.35f }, ["time"] = new[] { 5f, 6f, 8f }, ["heal"] = new[] { 0f, 0f, 0.1f }, ["draw"] = new[] { 1.5f } } },
            new() { Id = SkillId.LivingBridge, Name = "Living Bridge", Unlock = Basic3, Aim = SkillAim.Point, Range = 22f, Cd = new[] { 30f, 26f, 22f },
                Ai = AiUse.Bridge, AiCount = 3, AiRadius = 22f, LearnPriority = 1,
                Lore = "Workers lock legs and jaws into a chain of bodies over a rock or a gap: the army walks across them.",
                V = { ["span"] = new[] { 14f, 18f, 22f }, ["time"] = new[] { 10f, 14f, 18f }, ["fight"] = new[] { 0f, 0f, 1f }, ["width"] = new[] { 1.8f } } },
            new() { Id = SkillId.ScoutSwarm, Name = "Scout Swarm", Unlock = Basic3, Aim = SkillAim.Point, Range = 30f, Cd = new[] { 30f, 28f, 26f },
                Ai = AiUse.Target, AiRadius = 30f, LearnPriority = 2,
                Lore = "A handful of winged scouts circle a patch of ground: the fog lifts there and every enemy in it is marked.",
                V = { ["radius"] = new[] { 10f, 13f, 16f }, ["time"] = new[] { 12f, 16f, 20f }, ["mark"] = new[] { 1.1f, 1.15f, 1.2f }, ["pick"] = new[] { 0f, 0f, 10f }, ["flyers"] = new[] { 3f, 4f, 5f } } },
            new() { Id = SkillId.ThrallRaid, Name = "Thrall Raid", Unlock = Ult3, Ultimate = true, Aim = SkillAim.Self, Range = 12f, Cd = new[] { 90f, 80f, 70f },
                Ai = AiUse.Raid, AiRadius = 12f, LearnPriority = 3,
                Lore = "Slave-maker ants raid other nests for brood and raise it as their own. Near an enemy nest, the general carries off its brood.",
                V = { ["count"] = new[] { 2f, 3f, 4f } } },
        };
        var byId = new SkillDef[System.Enum.GetValues<SkillId>().Length];
        foreach (var d in list) byId[(int)d.Id] = d;
        Lines(byId);
        return byId;
    }

    static void Lines(SkillDef[] p)
    {
        SkillDef D(SkillId id) => p[(int)id];
        D(SkillId.WarCry).Line = r => { var d = D(SkillId.WarCry); float sh = d.N("shock", r); return $"Allies bite +{Pct(d.N("bite", r))} and run 20% faster for {d.N("time", r):0}s; the roar hits for {sh:0.#} {(sh == 1f ? "bite" : "bites")}."; };
        D(SkillId.QueensPheromone).Line = r => { var d = D(SkillId.QueensPheromone); return $"Turns {d.N("count", r):0} {(r == 1 ? "enemy" : "enemies")} for {d.N("time", r):0}s: " + r switch
            { 1 => "soldiers, gunners and flying ants up to level 2.", 2 => "also majors, beetles and spiders, up to level 3.", 3 => "also wasps and stag beetles, up to level 5.", _ => "any level, even enemy generals (for half the time)." }; };
        D(SkillId.MandibleFrenzy).Line = r => { var d = D(SkillId.MandibleFrenzy); return $"{d.N("time", r):0}s: bite +{Pct(d.N("bite", r))}, heals {d.N("leech", r) * 100:0}% of every bite, cannot be rooted or stunned."; };
        D(SkillId.AcidBurst).Line = r => { var d = D(SkillId.AcidBurst); float w = d.N("wider", r); return $"{Pct(d.N("mul", r))} stronger burst and pool{(w > 0 ? $", {w:0.#} wider" : "")}; venom pool for {Config.AcidPoolLife:0}s."; };
        D(SkillId.SpawnBrood).Line = r => { var d = D(SkillId.SpawnBrood); return r switch
            { 1 => "3 callow soldiers: small, fast attackers.", 2 => "2 callow majors: large, huge-headed crushers.", 3 => "4 callow flying ants: they cross rocks and fight wasps.", _ => "A whole brood: 3 soldiers, 2 majors, 3 flying ants and 2 acid gunners." }
            + $" They last {d.N("life", r):0}s and harden one level every {d.N("harden", r):0}s, up to level {d.N("cap", r):0}."; };
        D(SkillId.FormicMist).Line = r => { var d = D(SkillId.FormicMist); return $"A cloud {d.N("radius", r):0} wide for {d.N("time", r):0}s: {d.N("dps", r):0} acid a second, enemies {d.N("slow", r) * 100:0}% slower."; };
        D(SkillId.DiveBomb).Line = r => { var d = D(SkillId.DiveBomb); float st = d.N("stun", r); return $"{Pct(d.N("mul", r))} harder strike{(st > 0 ? $", stun +{st:0.#}s" : "")}; rallies warriors within 12."; };
        D(SkillId.TrailBlaze).Line = r => { var d = D(SkillId.TrailBlaze); return $"Allies within {d.N("radius", r):0} run {Pct(d.N("speed", r))} faster and shrug off {d.N("armor", r) * 100:0}% more of every blow for {d.N("time", r):0}s."; };
        D(SkillId.TalonDive).Line = r => { var d = D(SkillId.TalonDive); return $"Strikes one enemy for {d.N("damage", r):0} (x bite growth) and pins it for {d.N("stun", r):0.#}s. A kill refunds half the cooldown."; };
        D(SkillId.ShellWall).Line = r => { var d = D(SkillId.ShellWall); return $"+{d.N("armor", r) * 100:0}% armour for {d.N("time", r):0.#}s when swarmed or below 60% health (longer at higher levels)."; };
        D(SkillId.MandibleSweep).Line = r => { var d = D(SkillId.MandibleSweep); return $"Hits everything around the jaws for {d.N("bites", r):0.#} bites and throws it back {d.N("push", r):0.#} (more at higher levels)."; };
        D(SkillId.AntlerCharge).Line = r => { var d = D(SkillId.AntlerCharge); return $"Charges at {Config.ChargeSpeed:0.#}x speed; tosses all in front for {d.N("bites", r):0.#} bites, {d.N("stun", r):0.#}s on their backs (further at higher levels)."; };
        D(SkillId.WebSnare).Line = r => { var d = D(SkillId.WebSnare); return $"A silk patch {d.N("radius", r) * 2:0.#} wide roots ants for {d.N("root", r):0.#}s (wider and longer at higher levels)."; };
        D(SkillId.Lunge).Line = r => { var d = D(SkillId.Lunge); return $"Pounces on silk-wrapped prey for {d.N("bites", r):0.#} bites."; };
        D(SkillId.VenomSting).Line = r => { var d = D(SkillId.VenomSting); return $"A {d.N("bites", r):0.#}x bite that burns for {Config.StingVenomTime:0}s, then a climb out of reach (+0.3x per level)."; };
        D(SkillId.CleavingMandibles).Line = r => { var d = D(SkillId.CleavingMandibles); return $"Every bite also hits {d.N("targets", r):0} {(r == 1 ? "enemy" : "enemies")} beside the victim for {d.N("share", r) * 100:0}% of it."; };
        D(SkillId.RallyScent).Line = r => { var d = D(SkillId.RallyScent); return $"Nestmates within {d.N("radius", r):0} of the spot run {Pct(d.N("speed", r))} faster and bite {Pct(d.N("bite", r))} harder for {d.N("time", r):0}s; idle ones come running{(d.N("heal", r) > 0 ? $"; heals {d.N("heal", r) * 100:0}%" : "")}."; };
        D(SkillId.LivingBridge).Line = r => { var d = D(SkillId.LivingBridge); return $"A bridge of bodies up to {d.N("span", r):0} long over rocks for {d.N("time", r):0}s{(d.N("fight", r) > 0 ? "; ants can fight on it" : "; no biting while on it")}."; };
        D(SkillId.ScoutSwarm).Line = r => { var d = D(SkillId.ScoutSwarm); return $"{d.N("flyers", r):0} scouts lift the fog {d.N("radius", r):0} around a spot for {d.N("time", r):0}s; marked enemies take {Pct(d.N("mark", r))} more{(d.N("pick", r) > 0 ? "; they pick off lone workers" : "")}."; };
        D(SkillId.ThrallRaid).Line = r => { var d = D(SkillId.ThrallRaid); return $"Near an enemy nest: carries off {d.N("count", r):0} brood that hatch as your {(r == 1 ? "workers" : r == 2 ? "soldiers" : "majors")}; the enemy loses its brood in the making."; };
    }

    public static SkillDef Def(SkillId s) => Pool[(int)s];

    // ------------------------------------------------------------ the accessors the game uses (all read the table)
    public static int MaxRank(SkillId s) => Def(s).MaxRank;
    public static bool Ultimate(SkillId s) => Def(s).Ultimate;
    public static int ReqLevel(SkillId s, int rank) => Def(s).Unlock[System.Math.Clamp(rank, 1, Def(s).MaxRank) - 1];
    public static SkillAim Aim(SkillId s) => Def(s).Aim;
    public static float Range(SkillId s) => Def(s).Range;
    public static string Name(SkillId s) => Def(s).Name;
    public static string Lore(SkillId s) => Def(s).Lore;
    public static string RankLine(SkillId s, int rank) => Def(s).Line(System.Math.Clamp(rank, 1, Def(s).MaxRank));
    /// The levels a slot's ranks open at. A general with four skills has its ultimate in the last slot (R): 6, 8, 10,
    /// whatever skill the owner put there; every other slot follows the skill's own table.
    public static int[] Unlock(in Ant a, int slot)
    {
        var d = Def(At(a, slot));
        return Config.IsHero(a.Type) && SlotCount(a) == MaxSlots && slot == MaxSlots - 1 ? Ult3 : d.Unlock;
    }
    public static bool IsUlt(in Ant a, int slot) => Unlock(a, slot) == Ult3;
    public static int MaxRank(in Ant a, int slot) => System.Math.Min(Def(At(a, slot)).MaxRank, Unlock(a, slot).Length);
    public static int ReqLevel(in Ant a, int slot, int rank) { var u = Unlock(a, slot); return u[System.Math.Clamp(rank, 1, u.Length) - 1]; }
    public static int LearnPriority(in Ant a, int slot) => IsUlt(a, slot) ? 3 : Def(At(a, slot)).LearnPriority;

    public static float Cooldown(SkillId s, int rank, int level)
    {
        var d = Def(s);
        float cd = d.Cd[System.Math.Clamp(rank, 1, d.Cd.Length) - 1] - d.CdLevel * System.Math.Max(0, level - d.CdFrom);
        return Mathf.Max(d.CdMin, cd);
    }

    // ------------------------------------------------------------ loadouts (data/loadouts.json)
    public static int SlotCount(in Ant a) => Loadouts.For(a).Length;
    public static SkillId At(in Ant a, int slot) { var l = Loadouts.For(a); return l[System.Math.Clamp(slot, 0, l.Length - 1)]; }
    /// The default loadout of a type (no colony override): tests and the field guide.
    public static SkillId At(AntType t, int slot) { var l = Loadouts.For(t, -1); return l[System.Math.Clamp(slot, 0, l.Length - 1)]; }
    public static string KeyOf(int slot) => Keys[System.Math.Clamp(slot, 0, MaxSlots - 1)];

    // ------------------------------------------------------------ shared constants and rules
    public const float HardenEvery = 6f;        // a callow hardens one level every few seconds (Brood Call)

    /// Queen's Pheromone tiers: 1 soldiers, gunners, flying ants; 2 majors, beetles, spiders; 3 wasps and stag beetles;
    /// 4 generals (for half the time). Workers, queens, bosses and mound guardians never.
    public static int ControlTier(AntType t) => t switch
    {
        AntType.Soldier or AntType.Gunner or AntType.Flyer => 1,
        AntType.Major or AntType.Beetle or AntType.Spider => 2,
        AntType.Wasp or AntType.StagBeetle => 3,
        AntType.Warlord or AntType.Matron or AntType.Marshal => 4,
        _ => 99,
    };

    // named views of the table, for the sim code and the tests
    static SkillDef QP => Def(SkillId.QueensPheromone);
    public static float[] CharmCount => QP.V["count"];
    public static float[] CharmTier => QP.V["tier"];
    public static float[] CharmLevel => QP.V["level"];
    public static float[] CharmTime => QP.V["time"];
    public static float CharmRadius => QP.V["cloud"][0];
    public static float[] FrenzyTime => Def(SkillId.MandibleFrenzy).V["time"];
    public static float[] FrenzyBite => Def(SkillId.MandibleFrenzy).V["bite"];
    public static float[] FrenzyLeech => Def(SkillId.MandibleFrenzy).V["leech"];
    public static float[] CryBite => Def(SkillId.WarCry).V["bite"];
    public static float[] CryTime => Def(SkillId.WarCry).V["time"];
    public static float[] CryShock => Def(SkillId.WarCry).V["shock"];
    public static float[] BurstMul => Def(SkillId.AcidBurst).V["mul"];
    public static float[] BurstRadius => Def(SkillId.AcidBurst).V["wider"];
    public static (AntType type, int n)[] Brood(int rank) => Def(SkillId.SpawnBrood).Summons[System.Math.Clamp(rank, 1, 4) - 1];
    public static float[] BroodLife => Def(SkillId.SpawnBrood).V["life"];
    public static float[] BroodMaxLevel => Def(SkillId.SpawnBrood).V["cap"];
    public static float[] CloudRadius => Def(SkillId.FormicMist).V["radius"];
    public static float[] CloudTime => Def(SkillId.FormicMist).V["time"];
    public static float[] CloudDps => Def(SkillId.FormicMist).V["dps"];
    public static float[] CloudSlow => Def(SkillId.FormicMist).V["slow"];
    public static float[] DiveMul => Def(SkillId.DiveBomb).V["mul"];
    public static float[] DiveStun => Def(SkillId.DiveBomb).V["stun"];
    public static float[] BlazeRadius => Def(SkillId.TrailBlaze).V["radius"];
    public static float[] BlazeSpeed => Def(SkillId.TrailBlaze).V["speed"];
    public static float[] BlazeArmor => Def(SkillId.TrailBlaze).V["armor"];
    public static float[] BlazeTime => Def(SkillId.TrailBlaze).V["time"];
    public static float[] TalonDamage => Def(SkillId.TalonDive).V["damage"];
    public static float[] TalonStun => Def(SkillId.TalonDive).V["stun"];
    public static float TalonQueen => Def(SkillId.TalonDive).V["queen"][0];
}

/// Who has which skills: data/loadouts.json maps a unit type (or "Type@team" for one colony's own, e.g. "Warlord@1"
/// for the red colony's) to its skill ids in slot order. Read once at startup; every machine in a match must have the
/// same file, so its hash goes into the sim checksum. A missing or broken file falls back to the built-in defaults.
public static class Loadouts
{
    public const string Path = "res://data/loadouts.json";
    static readonly Dictionary<string, SkillId[]> _map = new();
    static bool _loaded;
    static uint _hash;
    public static uint Hash { get { Load(); return _hash; } }
    public static string Source { get; private set; } = "defaults";

    public static readonly Dictionary<string, SkillId[]> Defaults = new()
    {
        ["Warlord"] = new[] { SkillId.WarCry, SkillId.CleavingMandibles, SkillId.ShellWall, SkillId.AntlerCharge },
        ["Matron"] = new[] { SkillId.AcidBurst, SkillId.VenomSting, SkillId.FormicMist, SkillId.QueensPheromone },
        ["Marshal"] = new[] { SkillId.DiveBomb, SkillId.ScoutSwarm, SkillId.RallyScent, SkillId.ThrallRaid },
        ["Beetle"] = new[] { SkillId.ShellWall, SkillId.MandibleSweep },
        ["StagBeetle"] = new[] { SkillId.AntlerCharge },
        ["Spider"] = new[] { SkillId.Lunge, SkillId.WebSnare },
        ["Wasp"] = new[] { SkillId.VenomSting },
    };

    static void Load()
    {
        if (_loaded) return;
        _loaded = true;
        foreach (var (k, v) in Defaults) _map[k] = v;
        string text = null;
        try { if (FileAccess.FileExists(Path)) text = FileAccess.GetFileAsString(Path); } catch { text = null; }
        if (!string.IsNullOrEmpty(text))
        {
            var parsed = Json.ParseString(text);
            if (parsed.VariantType != Variant.Type.Dictionary) GD.PushWarning($"[skills] {Path} is not a JSON object; using the defaults");
            else
            {
                var dict = parsed.AsGodotDictionary();
                foreach (var key in dict.Keys)
                {
                    string unit = key.AsString();
                    if (unit.StartsWith("_")) continue; // notes
                    var ids = new List<SkillId>();
                    foreach (var v in dict[key].AsGodotArray())
                    {
                        if (System.Enum.TryParse<SkillId>(v.AsString(), true, out var id)) ids.Add(id);
                        else GD.PushWarning($"[skills] {Path}: unknown skill \"{v.AsString()}\" for {unit}");
                    }
                    if (ids.Count > Skills.MaxSlots) { GD.PushWarning($"[skills] {Path}: {unit} has more than {Skills.MaxSlots} skills; the rest are ignored"); ids.RemoveRange(Skills.MaxSlots, ids.Count - Skills.MaxSlots); }
                    _map[unit] = ids.ToArray();
                }
                Source = Path;
            }
        }
        Rehash();
    }

    static void Rehash()
    {
        uint h = 2166136261u;
        var keys = new List<string>(_map.Keys);
        keys.Sort(System.StringComparer.Ordinal);
        foreach (var k in keys)
        {
            foreach (char c in k) h = (h ^ c) * 16777619u;
            foreach (var id in _map[k]) h = (h ^ (uint)id) * 16777619u;
        }
        _hash = h;
    }

    static readonly SkillId[] None = System.Array.Empty<SkillId>();

    /// The loadout for a unit type, with a colony's own override when it has one (team -1: the default).
    public static SkillId[] For(AntType t, int team)
    {
        Load();
        if (team >= 0 && _map.TryGetValue($"{t}@{team}", out var own)) return own;
        return _map.TryGetValue(t.ToString(), out var l) ? l : None;
    }

    /// A live unit's loadout, by its own colony (a borrowed mind keeps its own skills).
    public static SkillId[] For(in Ant a) => For(a.Type, a.CharmP1 > 0 ? a.CharmP1 - 1 : a.Team);

    /// Tests only: give a key its own loadout for this run (the hash follows, so a mismatch still shows as a desync).
    public static void TestSet(string key, params SkillId[] ids) { Load(); _map[key] = ids; Rehash(); }
    public static void TestClear(string key) { Load(); _map.Remove(key); if (Defaults.TryGetValue(key, out var d)) _map[key] = d; Rehash(); }

    public static bool Has(in Ant a, SkillId s, out int slot)
    {
        var l = For(a);
        for (slot = 0; slot < l.Length; slot++) if (l[slot] == s) return true;
        slot = -1;
        return false;
    }
}

public sealed partial class Sim
{
    public static int Rank(in Ant a, int slot) => slot switch { 0 => a.Sk0, 1 => a.Sk1, 2 => a.Sk2, _ => a.Sk3 };
    public static float SkillCd(in Ant a, int slot) => slot switch { 0 => a.AbilityCd, 1 => a.CdW, 2 => a.CdE, _ => a.CdR };
    static void SetRank(ref Ant a, int slot, int r) { switch (slot) { case 0: a.Sk0 = (byte)r; break; case 1: a.Sk1 = (byte)r; break; case 2: a.Sk2 = (byte)r; break; default: a.Sk3 = (byte)r; break; } }
    static void SetCd(ref Ant a, int slot, float cd) { switch (slot) { case 0: a.AbilityCd = cd; break; case 1: a.CdW = cd; break; case 2: a.CdE = cd; break; default: a.CdR = cd; break; } }

    /// Whether this general can put a point into this skill now.
    public static bool CanLearn(in Ant a, int slot)
    {
        if (!a.Alive || !Config.IsHero(a.Type) || a.SkPts <= 0 || slot < 0 || slot >= Skills.SlotCount(a)) return false;
        int r = Rank(a, slot);
        return r < Skills.MaxRank(a, slot) && a.Level >= Skills.ReqLevel(a, slot, r + 1);
    }

    public static bool AnyLearnable(in Ant a)
    {
        for (int s = 0; s < Skills.SlotCount(a); s++) if (CanLearn(a, s)) return true;
        return false;
    }

    /// A newly hatched general knows its first skill at rank 1; every level above 1 is an unspent point. Anything else
    /// with a loadout (the wild bugs, a boss) knows every rank its level has opened, and uses them on its own.
    void InitSkills(ref Ant a)
    {
        a.Sk0 = a.Sk1 = a.Sk2 = a.Sk3 = 0;
        a.AutoMask = 15;
        int n = Skills.SlotCount(a);
        if (n == 0) return;
        if (Config.IsHero(a.Type))
        {
            a.Sk0 = 1;
            a.SkPts = (byte)System.Math.Max(0, a.Level - 1);
            return;
        }
        for (int s = 0; s < n; s++) SetRank(ref a, s, 1); // the bugs' specials grow with the bug's level
    }

    /// Restores a general's skills (Lane War respawn): ranks packed one per byte, the rest of its levels unspent.
    void RestoreSkills(ref Ant a, int packed)
    {
        if (packed == 0) { InitSkills(ref a); return; }
        a.Sk0 = (byte)(packed & 0xFF); a.Sk1 = (byte)((packed >> 8) & 0xFF); a.Sk2 = (byte)((packed >> 16) & 0xFF); a.Sk3 = (byte)((packed >> 24) & 0xFF);
        a.AutoMask = 15;
        a.SkPts = (byte)System.Math.Max(0, a.Level - a.Sk0 - a.Sk1 - a.Sk2 - a.Sk3);
    }

    static int PackSkills(in Ant a) => a.Sk0 | a.Sk1 << 8 | a.Sk2 << 16 | a.Sk3 << 24;

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
        if (id < 0 || id >= High || slot < 0) return;
        ref var a = ref Ants[id];
        if (!a.Alive || !Config.IsHero(a.Type) || slot >= Skills.SlotCount(a) || Rank(a, slot) == 0 || Skills.Aim(Skills.At(a, slot)) == SkillAim.Passive) return;
        a.CastSlot = (byte)(slot + 1);
        a.CastReq = 2f;
        a.CastTarget = Valid(target, targetSerial) ? target : -1;
        a.CastSerial = targetSerial;
        a.CastAt = at;
    }

    /// CmdKind.SkillAuto: turns a skill's autocast on or off (right-click on its button).
    public void ToggleAuto(int id, int slot)
    {
        if (id < 0 || id >= High || slot < 0 || slot >= Skills.MaxSlots) return;
        ref var a = ref Ants[id];
        if (!a.Alive || !Config.IsHero(a.Type)) return;
        a.AutoMask ^= (byte)(1 << slot);
    }

    /// The slot a computer player spends its next point on: the learnable skill with the highest LearnPriority in the
    /// table (ties: the later slot).
    public static int BestLearn(in Ant a)
    {
        int best = -1, bp = int.MinValue;
        for (int s = 0; s < Skills.SlotCount(a); s++)
        {
            if (!CanLearn(a, s)) continue;
            int p = Skills.LearnPriority(a, s);
            if (p >= bp) { bp = p; best = s; }
        }
        return best;
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
        a.MarkT -= dt;
        if (a.MarkT <= 0) a.MarkMul = 1f;
        a.CdW -= dt;
        a.CdE -= dt;
        a.CdR -= dt;
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
        var s = Skills.At(a, slot);
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
                if (!asked && ok.Count < Skills.Def(SkillId.QueensPheromone).AiCount && Skills.ControlTier(Ants[first].Type) < 2) return false;
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
            case SkillId.SpawnBrood:
            {
                int foe = Valid(a.Target, a.TargetSerial) ? a.Target : FindEnemy(a.Pos, a.Team, 16, true, a.Layer);
                int k = 0;
                foreach (var (type, n) in Skills.Brood(rank))
                    for (int m = 0; m < n; m++, k++)
                    {
                        var p = a.Pos + MathX.Dir(a.Heading + DMath.Pi + (k - 4.5f) * 0.55f) * (a.Size * 0.6f + 1.4f + 0.3f * (k % 3));
                        int id = Summon(a.Team, type, p, Skills.BroodLife[r], (int)Skills.BroodMaxLevel[r], a.Layer);
                        if (id < 0) continue;
                        ref var c = ref Ants[id];
                        if (foe >= 0 && Ants[foe].Alive) { c.Goal = Ants[foe].Pos; c.GoalLayer = a.Layer; c.State = AntState.AttackMove; }
                    }
                Events.Add(new SimEvent { Kind = EvKind.Summon, Pos = a.Pos, Team = a.Team, Type = a.Type, Radius = 3, Tier = rank });
                break;
            }
            case SkillId.FormicMist:
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
            case SkillId.ShellWall:
            {
                // swarmed or hurt, it locks its wing cases down and weathers the storm
                if (a.BlockT > 0) return false;
                float lvb = Config.IsHero(a.Type) ? 0 : a.Level - 1;
                a.BlockT = Skills.Def(s).N("time", rank) + 0.3f * lvb;
                a.BlockArmor = Skills.Def(s).N("armor", rank);
                Events.Add(new SimEvent { Kind = EvKind.ShellWall, Pos = a.Pos, Team = a.Team, Type = a.Type, Radius = a.Size * 0.6f });
                SetCd(ref a, slot, cd);
                return true; // no bite animation: it hunkers down
            }
            case SkillId.MandibleSweep:
            {
                // crush every ant around the jaws and hurl them back
                float lvb = Config.IsHero(a.Type) ? 0 : a.Level - 1;
                float rad = a.Size * 0.75f + 1.2f + 0.25f * lvb;
                var hits = AntsNear(a.Pos, rad);
                int foes = 0; foreach (int j in hits) if (j != i && Ants[j].Team != a.Team) foes++;
                if (foes < (asked ? 1 : 2)) return false;
                float dmg = st.Damage * a.DmgMul * Skills.Def(s).N("bites", rank);
                var victims = hits.ToArray();
                Events.Add(new SimEvent { Kind = EvKind.Sweep, Pos = a.Pos, Team = a.Team, Type = a.Type, Radius = rad });
                foreach (int j in victims)
                {
                    ref var b = ref Ants[j];
                    if (j == i || b.Team == a.Team) continue; // a roused stag (or a general) sweeps only the enemy
                    var push = (b.Pos - a.Pos).Normalized();
                    b.Pos = ClampMap(b.Pos + push * (Skills.Def(s).N("push", rank) + 0.2f * lvb));
                    Damage(j, dmg, i);
                }
                break;
            }
            case SkillId.AntlerCharge:
            {
                // head down, a charge at a run; whatever the jaws meet goes flying (ChargeImpact)
                int tgt = ordered >= 0 ? ordered : Valid(a.Target, a.TargetSerial) ? a.Target : -1;
                if (tgt < 0 || a.ChargeT > 0) return false;
                ref var t = ref Ants[tgt];
                float d = a.Pos.DistanceTo(t.Pos);
                if (t.Alt > 1.5f || d < 3.5f || d > 12f + (Config.IsHero(a.Type) ? 2 * (rank - 1) : a.Level - 1)) return false;
                if (ordered >= 0) { a.Target = tgt; a.TargetSerial = t.Serial; a.State = AntState.Engage; }
                a.ChargeT = 1.6f;
                a.ChargeKind = 1;
                a.ChargeRank = (byte)rank;
                Events.Add(new SimEvent { Kind = EvKind.Charge, Pos = a.Pos, To = t.Pos, Team = a.Team, Type = a.Type });
                SetCd(ref a, slot, cd);
                return true;
            }
            case SkillId.Lunge:
            {
                // the pounce: prey wrapped in silk a few lengths away
                int tgt = ordered >= 0 ? ordered : Valid(a.Target, a.TargetSerial) ? a.Target : -1;
                if (tgt < 0 || a.ChargeT > 0) return false;
                ref var t = ref Ants[tgt];
                float d = a.Pos.DistanceTo(t.Pos);
                if ((t.Root <= 0.3f && !asked) || d <= 2.5f || d >= 10f) return false;
                if (ordered >= 0) { a.Target = tgt; a.TargetSerial = t.Serial; a.State = AntState.Engage; }
                a.ChargeT = 0.9f;
                a.ChargeKind = 2;
                Events.Add(new SimEvent { Kind = EvKind.Charge, Pos = a.Pos, To = t.Pos, Team = a.Team, Type = a.Type });
                SetCd(ref a, slot, cd);
                return true;
            }
            case SkillId.WebSnare:
            {
                // a sticky blob that roots every ant it lands on
                int tgt = ordered >= 0 ? ordered : Valid(a.Target, a.TargetSerial) ? a.Target : asked ? FindEnemy(a.Pos, a.Team, Skills.Range(s), false, a.Layer) : -1;
                if (tgt < 0) return false;
                var to = hasSpot ? a.CastAt : Ants[tgt].Pos;
                float d = a.Pos.DistanceTo(to);
                if (d > Skills.Range(s)) return false;
                float lvb = Config.IsHero(a.Type) ? 0 : a.Level - 1;
                _shots.Add(new WebShotFx
                {
                    From = a.Pos, To = to, Dur = 0.15f + d / 35f, R = Skills.Def(s).N("radius", rank) + 0.35f * lvb,
                    Life = Skills.Def(s).N("life", rank) + 0.6f * lvb, Root = Skills.Def(s).N("root", rank) + 0.45f * lvb,
                });
                Events.Add(new SimEvent { Kind = EvKind.WebShot, Pos = a.Pos, To = to, Team = a.Team, Type = a.Type });
                break;
            }
            case SkillId.RallyScent:
            {
                // alarm pheromone on a spot: nestmates near it run and bite harder; idle ones come running
                var dd = Skills.Def(s);
                var to = hasSpot ? a.CastAt : ordered >= 0 ? Ants[ordered].Pos : engagedPos(ref a);
                if (a.Pos.DistanceTo(to) > dd.Range) to = a.Pos + (to - a.Pos).Normalized() * dd.Range;
                float rad = dd.N("radius", rank);
                foreach (int j in AlliesNear(to, rad * dd.N("draw", rank), a.Team, a.Layer).ToArray())
                {
                    ref var b = ref Ants[j];
                    if (b.Type == AntType.Queen || b.Type == AntType.Worker) continue;
                    if (b.Pos.DistanceTo(to) <= rad)
                    {
                        b.HasteT = Mathf.Max(b.HasteT, dd.N("time", rank)); b.HasteMul = Mathf.Max(b.HasteMul, dd.N("speed", rank)); b.GuardArmor = Mathf.Max(0, b.GuardArmor);
                        b.BuffMul = b.BuffT > 0 ? Mathf.Max(b.BuffMul, dd.N("bite", rank)) : dd.N("bite", rank); b.BuffT = Mathf.Max(b.BuffT, dd.N("time", rank));
                        b.Hp = Mathf.Min(b.MaxHp, b.Hp + b.MaxHp * dd.N("heal", rank));
                    }
                    else if (b.State == AntState.Idle && !b.Manual) { b.Goal = to; b.GoalLayer = a.Layer; b.State = AntState.AttackMove; }
                }
                Events.Add(new SimEvent { Kind = EvKind.RallyScent, Pos = to, Team = a.Team, Type = a.Type, Radius = rad, Tier = rank });
                break;
            }
            case SkillId.LivingBridge:
            {
                // a chain of bodies from the general to the spot: on it, ants walk over rocks
                var dd = Skills.Def(s);
                var to = hasSpot ? a.CastAt : ordered >= 0 ? Ants[ordered].Pos : a.State is AntState.Move or AntState.AttackMove ? a.Goal : engagedPos(ref a);
                float span = dd.N("span", rank);
                if (a.Layer != 0 || a.Pos.DistanceTo(to) < 3f) return false;
                if (a.Pos.DistanceTo(to) > span) to = a.Pos + (to - a.Pos).Normalized() * span;
                Bridges.Add(new BridgeZone { A = a.Pos, B = to, Team = a.Team, Life = dd.N("time", rank), Max = dd.N("time", rank), Rank = rank, Width = dd.N("width", rank) });
                Events.Add(new SimEvent { Kind = EvKind.Bridge, Pos = a.Pos, To = to, Team = a.Team, Type = a.Type, Tier = rank });
                break;
            }
            case SkillId.ScoutSwarm:
            {
                // winged scouts circle a spot: the fog lifts and enemies there are marked
                var dd = Skills.Def(s);
                var to = hasSpot ? a.CastAt : ordered >= 0 ? Ants[ordered].Pos : engagedPos(ref a);
                if (a.Pos.DistanceTo(to) > dd.Range) to = a.Pos + (to - a.Pos).Normalized() * dd.Range;
                Scouts.Add(new ScoutZone { Pos = to, R = dd.N("radius", rank), Life = dd.N("time", rank), Max = dd.N("time", rank), Team = a.Team, Rank = rank, Mark = dd.N("mark", rank), Pick = dd.N("pick", rank), Flyers = (int)dd.N("flyers", rank), Src = i });
                Events.Add(new SimEvent { Kind = EvKind.Scout, Pos = a.Pos, To = to, Team = a.Team, Type = a.Type, Radius = dd.N("radius", rank), Tier = rank });
                break;
            }
            case SkillId.ThrallRaid:
            {
                // near an enemy nest: its brood is carried off and raised as our own
                int foe = RaidableNest(a, Skills.Def(s).AiRadius);
                if (foe < 0) return false;
                int n = (int)Skills.Def(s).N("count", rank);
                var c = Col[foe];
                for (int q = c.Queue.Count - 1, taken = 0; q >= 0 && taken < n; q--)
                    if (c.Queue[q].Kind != ProdKind.Chamber) { c.Queue.RemoveAt(q); taken++; } // the brood in the making is gone
                c.Organic = Mathf.Max(0, c.Organic - 2f * n);
                var type = rank == 1 ? AntType.Worker : rank == 2 ? AntType.Soldier : AntType.Major;
                var thralls = new List<int>();
                for (int k = 0; k < n; k++)
                {
                    int id = Spawn(a.Team, type, ClampMap(a.Pos + MathX.Dir(a.Heading + DMath.Pi + k * 0.9f) * (a.Size * 0.6f + 1.2f)), a.Heading, 1);
                    if (id < 0) continue;
                    Ants[id].Layer = a.Layer;
                    if (type != AntType.Worker) { Ants[id].State = AntState.Idle; Ants[id].Anchor = Ants[id].Pos; }
                    thralls.Add(id);
                }
                if (type != AntType.Worker) FormSquads(thralls);
                Events.Add(new SimEvent { Kind = EvKind.Thrall, Pos = a.Pos, To = NestEntrance(foe), Team = a.Team, Type = type, Tier = thralls.Count });
                break;
            }
            default:
                return false; // a passive works in the bite
        }
        SetCd(ref a, slot, cd);
        a.AttackAnim = 1;
        return true;
    }

    Vector2 engagedPos(ref Ant a) => Valid(a.Target, a.TargetSerial) ? Ants[a.Target].Pos : a.Pos + MathX.Dir(a.Heading) * 6f;

    /// Whether a rock stands on the straight line between two points (Living Bridge).
    public bool RockBetween(Vector2 p, Vector2 q)
    {
        float len = p.DistanceTo(q);
        for (float t = 0; t <= len; t += 2f)
        {
            var x = p.Lerp(q, t / Mathf.Max(len, 0.01f));
            foreach (var o in W.ObstaclesNear(x)) if (o.R > 0.8f && x.DistanceTo(o.P) < o.R) return true;
        }
        return false;
    }

    /// The enemy colony whose nest mouth is within reach of this ant, or -1 (Thrall Raid).
    public int RaidableNest(in Ant a, float reach)
    {
        for (int t = 0; t < 2; t++)
            if (t != a.Team && Col[t].Alive && Col[t].Founded && a.Layer == 0 && NestEntrance(t).DistanceTo(a.Pos) <= reach) return t;
        return -1;
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

    /// Units use their skills: generals on their own when autocast is on (or when ordered), the wild bugs and bosses
    /// whenever they fight. Whether the moment is right comes from the skill's AI hints in the table.
    void UnitSkills(int i, ref Ant a, UnitStats st)
    {
        int n = Skills.SlotCount(a);
        for (int slot = 0; slot < n; slot++)
        {
            int rank = Rank(a, slot);
            if (rank == 0 || SkillCd(a, slot) > 0) continue;
            var d = Skills.Def(Skills.At(a, slot));
            if (d.Aim == SkillAim.Passive) continue; // works in the bite
            bool asked = a.CastSlot == slot + 1 && a.CastReq > 0;
            if (!asked && (a.Manual || (a.AutoMask & (1 << slot)) == 0)) continue;
            if (!asked && !AiWants(i, ref a, d)) continue;
            if (CastSkill(i, ref a, st, slot, rank, asked))
            {
                if (asked) { a.CastSlot = 0; a.CastReq = 0; a.CastTarget = -1; a.CastAt = Vector2.Zero; }
                return; // one skill a moment
            }
        }
    }

    /// The table's AI hint for a skill: is this a moment to use it?
    bool AiWants(int i, ref Ant a, SkillDef d)
    {
        bool engaged = a.State == AntState.Engage && Valid(a.Target, a.TargetSerial);
        switch (d.Ai)
        {
            case AiUse.Crowd:
                return EnemiesNear(a.Pos, a.Size * 0.6f + d.AiRadius, a.Team, d.AiRadius > 3f ? false : true, a.Layer).Count >= d.AiCount;
            case AiUse.Hurt:
                return a.Hp < a.MaxHp * d.AiHealth && EnemiesNear(a.Pos, a.Size * 0.6f + 6f, a.Team, false, a.Layer).Count > 0
                    || (d.AiRadius > 3f ? EnemiesNear(a.Pos, a.Size * 0.6f + d.AiRadius, a.Team, false, a.Layer).Count : AntsNear(a.Pos, a.Size * 0.6f + d.AiRadius).Count) >= d.AiCount
                    || engaged && (Config.IsHero(Ants[a.Target].Type) || Ants[a.Target].Type == AntType.Queen || Ants[a.Target].Boss) && d.Id == SkillId.MandibleFrenzy;
            case AiUse.Rally:
                return AlliesNear(a.Pos, d.AiRadius * 0.7f, a.Team, a.Layer).Count >= d.AiCount && EnemiesNear(a.Pos, d.AiRadius, a.Team, true, a.Layer).Count >= d.AiCount2;
            case AiUse.Prize:
                return EnemiesNear(a.Pos, d.AiRadius, a.Team, false, a.Layer).Count >= 1;
            case AiUse.Bridge:
            {
                // its fight, or where it is marching, lies across a rock: span it
                var to = engaged ? Ants[a.Target].Pos : a.State is AntState.Move or AntState.AttackMove ? a.Goal : a.Pos;
                float len = a.Pos.DistanceTo(to);
                return len > 4f && len < d.AiRadius && a.Layer == 0 && AlliesNear(a.Pos, 10f, a.Team, 0).Count >= d.AiCount && RockBetween(a.Pos, to);
            }
            case AiUse.Raid:
                return RaidableNest(a, d.AiRadius) >= 0;
            default: // Target
                return engaged && a.Pos.DistanceTo(Ants[a.Target].Pos) <= (d.AiRadius > 0 ? d.AiRadius : 99f);
        }
    }

    /// Living bridges fade; scout swarms mark the enemies under them (and at rank 3 pick off a lone worker each second).
    void TickZones(float dt)
    {
        for (int k = Bridges.Count - 1; k >= 0; k--) if ((Bridges[k].Life -= dt) <= 0) Bridges.RemoveAt(k);
        for (int k = Scouts.Count - 1; k >= 0; k--)
        {
            var z = Scouts[k];
            z.Life -= dt;
            if (z.Life <= 0) { Scouts.RemoveAt(k); continue; }
            foreach (int j in EnemiesNear(z.Pos, z.R, z.Team, false).ToArray())
            {
                ref var b = ref Ants[j];
                b.MarkT = 0.5f; b.MarkMul = Mathf.Max(b.MarkMul, z.Mark);
            }
            if (z.Pick <= 0 || (z.PickT -= dt) > 0) continue;
            z.PickT = 1f;
            foreach (int j in EnemiesNear(z.Pos, z.R, z.Team, false).ToArray())
            {
                ref var b = ref Ants[j];
                if (b.Type != AntType.Worker) continue;
                bool alone = true;
                foreach (int q in AlliesNear(b.Pos, 4f, b.Team, b.Layer)) if (q != j && Ants[q].Type != AntType.Worker) { alone = false; break; }
                if (!alone) continue;
                Damage(j, z.Pick, z.Src >= 0 && Ants[z.Src].Alive ? z.Src : -1);
                break;
            }
        }
    }

    /// The rank of the living bridge this ant stands on (0: none of its colony's).
    public int OnBridge(in Ant a)
    {
        if (a.Layer != 0) return 0;
        foreach (var b in Bridges)
        {
            if (b.Team != a.Team) continue;
            var ab = b.B - b.A;
            float t = Mathf.Clamp((a.Pos - b.A).Dot(ab) / Mathf.Max(ab.LengthSquared(), 0.01f), 0, 1);
            if (a.Pos.DistanceTo(b.A + ab * t) <= b.Width) return b.Rank;
        }
        return 0;
    }

    /// Validation hooks (GeneralValidation): never called by the game itself.
    public void TestFeed(int id, float organic) => Nourish(id, ref Ants[id], organic);
    public void TestSetRank(int id, int slot, int rank) { SetRank(ref Ants[id], slot, rank); SetCd(ref Ants[id], slot, 0); }
}
