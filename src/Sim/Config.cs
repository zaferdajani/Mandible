using Godot;

namespace Mandible;

public enum AntType
{
    Worker = 0, Soldier = 1, Major = 2, Queen = 3, Beetle = 4, Spider = 5, StagBeetle = 6, Wasp = 7, Flyer = 8,
    Gunner = 9, Warlord = 10, Matron = 11, Marshal = 12,
}

public enum AntState { Idle, Forage, Gather, Return, Move, AttackMove, Engage, Hold }

public enum ProdKind { Worker, Soldier, Major, Chamber, Flyer, Gunner, Warlord, Matron, Marshal }

/// Colony research, done in the special sections of the nest. The queen's ascension gates the rest.
public enum Upgrade { Queen, Wings, Mandibles, Chitin, SilkCutters }

public enum Difficulty { Easy, Normal, Hard }

public struct UnitStats
{
    public string Name;
    public float Hp, Damage, Cooldown, Speed, Reach, Aggro, Sight, Size, Cost, BuildTime, CorpseFood;
    public int Pop;
}

public static class Config
{
    public const float MapSize = 320f;
    public const int MaxAnts = 3000;
    public const int Player = 0;
    public const int Enemy = 1;
    public const int Wild = 2; // neutral bugs guarding food
    public const int MaxLevel = 5;
    public const int HeroMaxLevel = 10;
    public static readonly int[] LevelXp = { 0, 0, 40, 110, 210, 350 }; // xp needed to reach level n
    public static readonly int[] HeroXp = { 0, 0, 110, 270, 500, 800, 1180, 1650, 2200, 2850, 3600 };
    public static int MaxLevelOf(AntType t) => IsHero(t) ? HeroMaxLevel : MaxLevel;
    public static int XpFor(AntType t, int level) => IsHero(t) ? HeroXp[level] : LevelXp[System.Math.Min(level, MaxLevel)];

    public static bool IsBug(AntType t) => t >= AntType.Beetle && t <= AntType.Wasp;
    public static bool IsHero(AntType t) => t >= AntType.Warlord;
    public static bool IsWarrior(AntType t) => t == AntType.Soldier || t == AntType.Major || t == AntType.Flyer || t == AntType.Gunner || IsHero(t);
    public static bool Flies(AntType t) => t == AntType.Wasp || t == AntType.Flyer || t == AntType.Marshal;
    public static bool Ranged(AntType t) => t == AntType.Gunner || t == AntType.Matron;
    public static float CruiseAlt(AntType t) => t == AntType.Wasp ? 5f : t == AntType.Flyer ? 3.6f : t == AntType.Marshal ? 4.4f : 0f;

    /// Per-level growth: warriors +16% hp / +14% bite per level, heroes +12% per level, wild bugs grow faster.
    public static float HpMul(AntType t, int level) => 1 + (level - 1) * (IsBug(t) ? 0.4f : IsHero(t) ? 0.12f : 0.16f);
    public static float DmgMul(AntType t, int level) => 1 + (level - 1) * (IsBug(t) ? 0.28f : IsHero(t) ? 0.12f : 0.14f);
    public static float SizeMul(AntType t, int level) => 1 + (level - 1) * (IsBug(t) ? 0.07f : IsHero(t) ? 0.025f : 0.05f);

    /// Queen level a hero first becomes available at; every queen level above that hatches it stronger.
    public static int HeroBaseTier(AntType t) => t switch { AntType.Warlord => 2, AntType.Matron => 3, _ => 4 };
    public static float HeroTierMul(AntType t, int queenTier) => 1 + 0.25f * System.Math.Max(0, queenTier - HeroBaseTier(t));

    public static string AbilityName(AntType t) => t switch
    {
        AntType.Warlord => "War Cry",
        AntType.Matron => "Acid Burst",
        AntType.Marshal => "Dive Bomb",
        AntType.Beetle or AntType.StagBeetle => "Mandible Sweep",
        AntType.Spider => "Web Shot",
        AntType.Wasp => "Venom Sting",
        _ => "",
    };

    /// XP granted for killing a unit of this type and level.
    public static float Bounty(AntType t, int level) => t switch
    {
        AntType.Worker => 6,
        AntType.Soldier => 14,
        AntType.Major => 32,
        AntType.Queen => 120,
        AntType.Beetle => 18 * level,
        AntType.Spider => 20 * level,
        AntType.Wasp => 24 * level,
        AntType.Flyer => 22,
        AntType.Gunner => 16,
        AntType.Warlord or AntType.Matron or AntType.Marshal => 120 * level,
        _ => 30 * level,
    } * (IsBug(t) ? 1 : 1 + (level - 1) * 0.5f);

    public const float StartFood = 250f;
    public const int StartPopCap = 60;
    public const int ChamberPop = 50;
    public const int MaxPopCap = 460;
    public const float ChamberCost = 100f;
    public const float ChamberTime = 12f;
    public const int QueueLimit = 6;

    public static readonly UnitStats[] Stats =
    {
        new() { Name = "Worker", Hp = 24, Damage = 2.2f, Cooldown = 0.7f, Speed = 7.2f, Reach = 0.9f, Aggro = 4.5f, Sight = 11, Size = 1.0f, Cost = 15, BuildTime = 1.6f, CorpseFood = 3, Pop = 1 },
        new() { Name = "Soldier", Hp = 72, Damage = 6.5f, Cooldown = 0.6f, Speed = 7.6f, Reach = 1.25f, Aggro = 13, Sight = 13, Size = 1.45f, Cost = 35, BuildTime = 3f, CorpseFood = 6, Pop = 1 },
        new() { Name = "Major", Hp = 270, Damage = 19f, Cooldown = 0.9f, Speed = 5.2f, Reach = 1.8f, Aggro = 13, Sight = 14, Size = 2.15f, Cost = 100, BuildTime = 6.5f, CorpseFood = 18, Pop = 3 },
        new() { Name = "Queen", Hp = 3200, Damage = 30f, Cooldown = 0.7f, Speed = 0f, Reach = 3.2f, Aggro = 7, Sight = 30, Size = 3.4f, Cost = 0, BuildTime = 0, CorpseFood = 80, Pop = 0 },
        new() { Name = "Ground Beetle", Hp = 330, Damage = 15f, Cooldown = 0.9f, Speed = 6.6f, Reach = 1.5f, Aggro = 12, Sight = 10, Size = 2.4f, Cost = 0, BuildTime = 0, CorpseFood = 40, Pop = 0 },
        new() { Name = "Wolf Spider", Hp = 240, Damage = 21f, Cooldown = 0.7f, Speed = 11.5f, Reach = 1.8f, Aggro = 15, Sight = 12, Size = 3.1f, Cost = 0, BuildTime = 0, CorpseFood = 35, Pop = 0 },
        new() { Name = "Stag Beetle", Hp = 1100, Damage = 36f, Cooldown = 1.0f, Speed = 5.6f, Reach = 2.6f, Aggro = 14, Sight = 12, Size = 4.0f, Cost = 0, BuildTime = 0, CorpseFood = 160, Pop = 0 },
        new() { Name = "Hunting Wasp", Hp = 250, Damage = 27f, Cooldown = 1.15f, Speed = 12.5f, Reach = 1.7f, Aggro = 17, Sight = 15, Size = 3.0f, Cost = 0, BuildTime = 0, CorpseFood = 45, Pop = 0 },
        new() { Name = "Flying Ant", Hp = 60, Damage = 7f, Cooldown = 0.55f, Speed = 11f, Reach = 1.5f, Aggro = 14, Sight = 18, Size = 1.35f, Cost = 60, BuildTime = 4.5f, CorpseFood = 7, Pop = 2 },
        new() { Name = "Acid Gunner", Hp = 55, Damage = 6f, Cooldown = 1.0f, Speed = 7f, Reach = 7f, Aggro = 13, Sight = 14, Size = 1.3f, Cost = 50, BuildTime = 4f, CorpseFood = 6, Pop = 1 },
        new() { Name = "Warlord", Hp = 900, Damage = 28f, Cooldown = 0.8f, Speed = 7.2f, Reach = 2.0f, Aggro = 15, Sight = 18, Size = 2.7f, Cost = 250, BuildTime = 20f, CorpseFood = 40, Pop = 4 },
        new() { Name = "Acid Matron", Hp = 650, Damage = 18f, Cooldown = 0.9f, Speed = 6.5f, Reach = 6.5f, Aggro = 15, Sight = 18, Size = 2.5f, Cost = 350, BuildTime = 25f, CorpseFood = 40, Pop = 4 },
        new() { Name = "Sky Marshal", Hp = 600, Damage = 22f, Cooldown = 0.7f, Speed = 12f, Reach = 1.9f, Aggro = 16, Sight = 22, Size = 2.2f, Cost = 450, BuildTime = 30f, CorpseFood = 40, Pop = 4 },
    };

    public static UnitStats Of(AntType t) => Stats[(int)t];

    public static AntType UnitOf(ProdKind k) => k switch
    {
        ProdKind.Worker => AntType.Worker,
        ProdKind.Soldier => AntType.Soldier,
        ProdKind.Major => AntType.Major,
        ProdKind.Gunner => AntType.Gunner,
        ProdKind.Warlord => AntType.Warlord,
        ProdKind.Matron => AntType.Matron,
        ProdKind.Marshal => AntType.Marshal,
        _ => AntType.Flyer,
    };

    public static bool IsHeroKind(ProdKind k) => k >= ProdKind.Warlord;

    public static string Role(ProdKind k) => k switch
    {
        ProdKind.Worker => "Forages on its own, lays food trails and carries corpses home.",
        ProdKind.Soldier => "Fast, aggressive fighter. The backbone of any raid.",
        ProdKind.Major => "Huge-headed crusher. Slow, tough and devastating.\nRequires a Matriarch queen.",
        ProdKind.Flyer => "Winged alate. Flies over rocks, strikes wasps in the air, swoops on ground prey.\nRequires the Wing Chamber.",
        ProdKind.Gunner => "Sprays formic acid from range. Fragile; keep it behind the soldiers.\nRequires an Empress queen.",
        ProdKind.Warlord => "HERO. Every bite cleaves all enemies around it.\nWar Cry: nearby warriors bite harder and run faster.\nLevels up to 10. Requires a Matriarch; stronger from a higher queen.",
        ProdKind.Matron => "HERO. Spits acid from range.\nAcid Burst: melts a whole cluster of enemies.\nLevels up to 10. Requires an Empress; stronger from a higher queen.",
        ProdKind.Marshal => "HERO. Flies.\nDive Bomb: crashes down on a crowd, crushing and stunning it.\nLevels up to 10. Requires an Ancient Empress and the Wing Chamber.",
        _ => "",
    };

    /// Warriors hatch as squads; workers and heroes hatch alone.
    public static int SquadSize(AntType t) => t switch { AntType.Soldier => 6, AntType.Gunner => 4, AntType.Flyer => 4, AntType.Major => 3, _ => 1 };
    public static int SquadSize(ProdKind k) => k == ProdKind.Chamber ? 1 : SquadSize(UnitOf(k));
    public static bool InSquads(AntType t) => SquadSize(t) > 1;
    /// A squad is 10% cheaper than its members bought one by one, and hatches faster than one at a time.
    public static float ProdCost(ProdKind k) => k == ProdKind.Chamber ? ChamberCost : Of(UnitOf(k)).Cost * SquadSize(k) * (SquadSize(k) > 1 ? 0.9f : 1f);
    public static float ProdTime(ProdKind k) => k == ProdKind.Chamber ? ChamberTime : Of(UnitOf(k)).BuildTime * (SquadSize(k) > 1 ? SquadSize(k) * 0.55f : 1f);
    public static int ProdPop(ProdKind k) => k == ProdKind.Chamber ? 0 : Of(UnitOf(k)).Pop * SquadSize(k);
    /// Squads rank up together; they share every kill, so they need more experience than a lone ant.
    public static int SquadXp(int level) => (int)(LevelXp[System.Math.Min(level, MaxLevel)] * 2.5f);
    public const float ReinforceRadius = 18f;
    public static string ProdName(ProdKind k) => k == ProdKind.Chamber ? "Excavate Chamber" : Of(UnitOf(k)).Name;

    // ---------------------------------------------------------------- research
    public static readonly string[] QueenTitles = { "", "Young Queen", "Matriarch", "Empress", "Ancient Empress", "Eternal Mother" };

    /// Highest tier of each upgrade. The queen starts at tier 1 and can ascend four times.
    public static int MaxTier(Upgrade u) => u switch { Upgrade.Queen => 5, Upgrade.Mandibles => 3, Upgrade.Chitin => 3, _ => 1 };

    /// Brood hatches faster under a greater queen.
    public static float BroodSpeed(int queenTier) => queenTier >= 5 ? 1.5f : queenTier >= 3 ? 1.25f : 1f;

    /// Queen tier required before the next tier of this upgrade can be researched.
    public static int QueenNeeded(Upgrade u, int nextTier) => u switch
    {
        Upgrade.Queen => 0,
        Upgrade.Wings => 2,
        Upgrade.SilkCutters => 2,
        _ => nextTier, // mandibles / chitin tier n needs queen tier n
    };

    public static float ResearchCost(Upgrade u, int nextTier) => u switch
    {
        Upgrade.Queen => nextTier switch { 2 => 200, 3 => 400, 4 => 700, _ => 1100 },
        Upgrade.Wings => 160,
        Upgrade.SilkCutters => 100,
        _ => 90 + 70 * (nextTier - 1),
    };

    public static float ResearchTime(Upgrade u, int nextTier) => u switch
    {
        Upgrade.Queen => 20 + 15 * nextTier,
        Upgrade.Wings => 30,
        Upgrade.SilkCutters => 20,
        _ => 18 + 8 * nextTier,
    };

    public static string ResearchName(Upgrade u, int nextTier) => u switch
    {
        Upgrade.Queen => $"Royal Jelly: {QueenTitles[System.Math.Min(5, nextTier)]}",
        Upgrade.Wings => "Wing Chamber",
        Upgrade.SilkCutters => "Silk Cutters",
        Upgrade.Mandibles => $"Mandible Forge {new string('I', nextTier)}",
        _ => $"Chitin Armory {new string('I', nextTier)}",
    };

    public static string ResearchDesc(Upgrade u) => u switch
    {
        Upgrade.Queen => "Feed the queen royal jelly. She grows mightier, unlocks new castes, and hatches stronger heroes.\nMatriarch: majors, Wing Chamber, Silk Cutters, tier II forges, the Warlord hero.\nEmpress: Acid Gunners, tier III forges, the Acid Matron hero, brood 25% faster.\nAncient Empress: the Sky Marshal hero.\nEternal Mother: brood 50% faster, mightiest heroes.",
        Upgrade.Wings => "Raise winged alates: flying ants that cross rocks and fight wasps in the air.",
        Upgrade.SilkCutters => "Sharpened tarsal claws: webbed ants break free in half the time.",
        Upgrade.Mandibles => "New warriors hatch larger: per tier +22% bite, +12% health, +10% size, but 8% slower.",
        _ => "Hardened chitin: all warriors gain 18% health per tier.",
    };

    /// Unit unlocks: majors need a Matriarch, flyers need the Wing Chamber.
    public static bool Unlocked(ProdKind k, int[] tiers) => k switch
    {
        ProdKind.Major => tiers[(int)Upgrade.Queen] >= 2,
        ProdKind.Flyer => tiers[(int)Upgrade.Wings] >= 1,
        ProdKind.Gunner => tiers[(int)Upgrade.Queen] >= 3,
        ProdKind.Warlord => tiers[(int)Upgrade.Queen] >= 2,
        ProdKind.Matron => tiers[(int)Upgrade.Queen] >= 3,
        ProdKind.Marshal => tiers[(int)Upgrade.Queen] >= 4 && tiers[(int)Upgrade.Wings] >= 1,
        _ => true,
    };

    public static readonly Color PlayerUi = new(1.0f, 0.78f, 0.29f);
    public static readonly Color EnemyUi = new(1.0f, 0.27f, 0.18f);
    public static readonly Color WildUi = new(0.78f, 0.55f, 1.0f);
    public static Color TeamUi(int team) => team == Local.Me ? PlayerUi : team < 2 ? EnemyUi : WildUi;

    // ------------------------------------------------------------------ races
    // The black colony is always black garden ants and the red colony fire ants: the player picks
    // a race by picking which colony to lead. All modifiers depend only on the team, so lockstep holds.
    public static Race RaceOf(int team) => team == 0 ? Race.BlackGarden : Race.RedFire;

    public const float BlackTrailMul = 1.35f;     // longer-lasting, stronger recruitment trails
    public const float BlackWorkerBrood = 1.4f;   // huge colonies: workers hatch 40% faster
    public const float BlackWarriorHp = 1.0f;    // small-bodied, stingless
    public const float BlackAcidMul = 1.3f;       // formic acid: gunners hit 30% harder
    public const float RedTrailMul = 0.85f;
    public const float RedVenomDps = 0.25f;       // venom: share of a bite dealt again per second for 3 s
    public const float RedHomeDamage = 1.15f;     // the mound boils over: +15% damage near home
    public const float RedGunnerMul = 0.75f;      // no acid gland: weak spitters
    public const float RedBrood = 1.12f;          // several queens per mound: all brood 12% faster
    /// Carbohydrates feed the black colony's honeydew economy; flesh and seeds the red predators.
    public static bool Favoured(Race r, FoodKind k) => r == Race.BlackGarden
        ? k is FoodKind.Sugar or FoodKind.Berry or FoodKind.Crumb
        : k is FoodKind.Corpse or FoodKind.Seed;
    public static float FoodBonus(Race r) => r == Race.BlackGarden ? 1.2f : 1.25f;

    public static string RaceName(Race r) => r == Race.BlackGarden ? "Black Garden Ants" : "Red Fire Ants";
    public static string RaceLatin(Race r) => r == Race.BlackGarden ? "Lasius niger" : "Solenopsis invicta";
    public static string RaceMotto(Race r) => r == Race.BlackGarden ? "The Garden Legion: numbers, trails and honeydew" : "The Fire Swarm: venom, fury and flesh";
    public static string[] RaceStrengths(Race r) => r == Race.BlackGarden
        ? new[] { "Workers hatch 40% faster: the largest colonies in the yard", "Trail masters: 35% stronger pheromone trails recruit foragers faster", "Honeydew farmers: sugar, fruit and crumbs worth 20% more", "Formic acid: gunners spit 30% harder" }
        : new[] { "Venom sting: soldiers, majors and heroes poison what they bite", "Mound fury: warriors deal 15% more damage near their nest", "Predators: insect corpses and seeds worth 25% more", "Many queens: all brood hatches 12% faster" };
    public static string[] RaceWeaknesses(Race r) => r == Race.BlackGarden
        ? new[] { "Stingless: warriors have no venom, only their bite", "Protein (corpses, seeds) gives no bonus" }
        : new[] { "No acid gland: gunners deal 25% less damage", "Fainter trails (15% weaker): slower to find food", "Sugar, fruit and crumbs give no bonus" };

    // ------------------------------------------------------------------ food
    public const float EffectPerDelivery = 1.2f; // seconds of colony effect added per load delivered
    public const float EffectMax = 30f;

    public static float FoodValue(FoodKind k) => k switch { FoodKind.Seed => 6f, FoodKind.Berry or FoodKind.Sugar => 5f, _ => 4f };
    public static string FoodName(FoodKind k) => k switch
    {
        FoodKind.Crumb => "Crumbs", FoodKind.Berry => "Wild Berry", FoodKind.Sugar => "Honeydew",
        FoodKind.Seed => "Seed Cache", _ => "Insect Carcass",
    };
    public static string FoodNutrient(FoodKind k) => k switch
    {
        FoodKind.Crumb => "Bread and fruit scraps (carbohydrate)", FoodKind.Berry => "Fruit sugar and water", FoodKind.Sugar => "Pure sugar (carbohydrate)",
        FoodKind.Seed => "Fat and protein, heavy", _ => "Protein",
    };
    public static string FoodEffect(FoodKind k) => k switch
    {
        FoodKind.Sugar => "Energy: while honeydew keeps arriving, workers run 15% faster.",
        FoodKind.Berry => "Water: while fruit keeps arriving, every ant regenerates 1.5% health per second.",
        FoodKind.Corpse => "Protein feeds the larvae: while it keeps arriving, the queen lays 30% faster.",
        FoodKind.Seed => "Dense: each load is worth 6 food, but carriers walk 20% slower.",
        _ => "No special effect.",
    };
    public static string FoodEffectShort(FoodKind k) => k switch
    {
        FoodKind.Sugar => "Effect: keeps your workers 15% faster while it flows",
        FoodKind.Berry => "Effect: all your ants slowly heal while it flows",
        FoodKind.Corpse => "Effect: the queen lays 30% faster while it flows",
        FoodKind.Seed => "Heavy: carriers walk 20% slower",
        _ => "No special effect",
    };
}

public enum Race { BlackGarden, RedFire }

/// Which colony this machine plays and views. The player picks black or red; in multiplayer the host picks, the guest takes the other.
public static class Local
{
    public static int Me = Config.Player;
    public static int Foe => 1 - Me;
    public static string ColonyName(int team) => Config.RaceName(Config.RaceOf(team));
}

/// Small fast deterministic RNG (xorshift).
public sealed class Rng
{
    uint _s;
    public Rng(uint seed) { _s = seed == 0 ? 0x9E3779B9u : seed; }
    public uint NextU() { _s ^= _s << 13; _s ^= _s >> 17; _s ^= _s << 5; return _s; }
    public float F() => (NextU() & 0xFFFFFF) / 16777216f;
    public float R(float a, float b) => a + (b - a) * F();
    public int I(int a, int b) => a + (int)(NextU() % (uint)(b - a));
    public Vector2 InDisk(float r) { float a = R(0, DMath.Tau); float d = Mathf.Sqrt(F()) * r; return DMath.Dir(a) * d; }
}

public static class MathX
{
    public static float AngleDiff(float from, float to)
    {
        float d = (to - from) % Mathf.Tau;
        if (d > Mathf.Pi) d -= Mathf.Tau;
        if (d < -Mathf.Pi) d += Mathf.Tau;
        return d;
    }

    public static Vector2 Dir(float a) => DMath.Dir(a);
}
