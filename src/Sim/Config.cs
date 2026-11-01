using Godot;

namespace Mandible;

public enum AntType
{
    Worker = 0, Soldier = 1, Major = 2, Queen = 3, Beetle = 4, Spider = 5, StagBeetle = 6, Wasp = 7, Flyer = 8,
    Gunner = 9, Warlord = 10, Matron = 11, Marshal = 12,
}

public enum AntState { Idle, Forage, Gather, Return, Move, AttackMove, Engage, Hold, Dig }

public enum ProdKind { Worker, Soldier, Major, Chamber, Flyer, Gunner, Warlord, Matron, Marshal }

/// Colony research, done in the special sections of the nest. The queen's ascension gates the rest.
public enum Upgrade { Queen, Wings, Mandibles, Chitin, SilkCutters }

public enum Difficulty { Easy, Normal, Hard }

public struct UnitStats
{
    public string Name;
    public float Hp, Damage, Cooldown, Speed, Reach, Aggro, Sight, Size, Cost, BuildTime, CorpseFood;
    public int Pop;
    /// Share of every blow the chitin shrugs off (0 = none). Heroes and wild bugs wear real armour.
    public float Armor;
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
        AntType.Beetle => "Shell Wall",
        AntType.StagBeetle => "Antler Toss",
        AntType.Spider => "Web Snare",
        AntType.Wasp => "Venom Sting",
        _ => "",
    };

    // ------------------------------------------------------------ hero and bug identities
    // Every hero and wild bug has three things a player can read at a glance: how it attacks,
    // how it defends, and one signature special. The numbers here are the ones the sim uses.
    public const float WarlordCleave = 0.5f;        // share of each bite dealt to everyone around the victim
    public const float WarlordArmor = 0.3f;
    public const float WarCryBite = 1.3f;           // allies' bite multiplier (+3% a level)
    public const float WarCryShock = 1.0f;          // the roar itself hits everyone within WarCryShockR for one bite
    public const float WarCryShockR = 4.5f;
    public const float MatronSplash = 0.45f;        // share of her spit splashed onto enemies beside the target
    public const float MatronSplashR = 1.8f;
    public const float MatronArmor = 0.1f;
    public const float AcidPoolLife = 6f;           // the burst leaves a venom pool that keeps burning
    public const float MarshalArmor = 0.15f;
    public const float RallyBite = 1.15f;           // Dive Bomb rallies nearby warriors: bite and speed up
    public const float RallyTime = 6f;
    public const float BeetleArmor = 0.25f;
    public const float ShellWallArmor = 0.45f;      // added to the beetle's armour while its shell is raised
    public const float ShellWallTime = 4f;
    public const float StagArmor = 0.3f;
    public const float ChargeSpeed = 2.6f;          // a charging stag or lunging spider runs this much faster
    public const float TossDamage = 2.2f;           // the toss, as bites
    public const float TossThrow = 3.5f;            // how far victims fly
    public const float TossStun = 1.1f;             // seconds on their backs
    public const float SpiderArmor = 0.05f;
    public const float LungeDamage = 2.4f;          // a lunge onto webbed prey, as bites
    public const float WaspArmor = 0.1f;
    public const float StingDamage = 2.5f;          // the sting, as bites (+0.3 a level)
    public const float StingVenom = 0.12f;          // share of the sting dealt again per second for StingVenomTime
    public const float StingVenomTime = 4f;
    public const float WaspRetreat = 2.4f;          // seconds the wasp climbs away after a sting (hit and run)

    /// One line each: how it attacks, how it defends, its special. Shown in the Field Guide, hero picker and tooltips.
    public static string AttackDesc(AntType t) => t switch
    {
        AntType.Warlord => $"Cleaving bite: every bite also deals {WarlordCleave * 100:0}% to all enemies around the victim.",
        AntType.Matron => $"Acid spray from range {Of(t).Reach:0}: the jet splashes {MatronSplash * 100:0}% onto enemies beside the target.",
        AntType.Marshal => "Swooping bite from the air: flies over rocks and strikes wasps and ground prey alike.",
        AntType.Beetle => "Crushing jaws, and a Mandible Sweep that hits and hurls back every ant around it when two or more crowd in.",
        AntType.StagBeetle => "Antler jaws with the longest reach of any bug; what they catch, they toss.",
        AntType.Spider => $"Fast pounce bite; a bite on silk-wrapped prey deals {LungeDamage:0.#}x.",
        AntType.Wasp => $"Flies. Every few seconds a venomous sting: {StingDamage:0.#}x bite plus venom for {StingVenomTime:0}s.",
        _ => "",
    };

    public static string DefenceDesc(AntType t) => t switch
    {
        AntType.Warlord => $"Plated chitin: shrugs off {WarlordArmor * 100:0}% of every blow.",
        AntType.Matron => $"Thin armour ({MatronArmor * 100:0}%); she fights from range behind her line.",
        AntType.Marshal => $"Airborne: ground biters reach it only when it dives. Armour {MarshalArmor * 100:0}%.",
        AntType.Beetle => $"Hard wing cases: armour {BeetleArmor * 100:0}%, {(BeetleArmor + ShellWallArmor) * 100:0}% behind the Shell Wall.",
        AntType.StagBeetle => $"The heaviest chitin in the yard: armour {StagArmor * 100:0}%.",
        AntType.Spider => "Almost no armour: it relies on speed and silk.",
        AntType.Wasp => $"Hit and run: climbs out of reach for {WaspRetreat:0.#}s after every sting. Armour {WaspArmor * 100:0}%.",
        _ => "",
    };

    public static string SpecialDesc(AntType t) => t switch
    {
        AntType.Warlord => $"WAR CRY: a roar that staggers every enemy within {WarCryShockR:0} for a bite and makes warriors within 13 bite {WarCryBite * 100 - 100:0}% harder and run 20% faster for 8s.",
        AntType.Matron => $"ACID BURST: melts a cluster around her target and leaves a venom pool for {AcidPoolLife:0}s that poisons everything standing in it.",
        AntType.Marshal => $"DIVE BOMB: crashes down on a crowd, crushing and stunning it, and rallies warriors within 12: +{RallyBite * 100 - 100:0}% bite and speed for {RallyTime:0}s.",
        AntType.Beetle => $"SHELL WALL: when swarmed or hurt it locks its wing cases down: +{ShellWallArmor * 100:0}% armour for {ShellWallTime:0}s.",
        AntType.StagBeetle => $"ANTLER TOSS: charges prey at {ChargeSpeed:0.#}x speed and tosses everything in front of it {TossThrow:0} lengths through the air for {TossDamage:0.#}x bite, leaving them stunned on their backs.",
        AntType.Spider => "WEB SNARE + LUNGE: spits silk that roots a patch of ants, then lunges onto the wrapped prey at a run.",
        AntType.Wasp => $"VENOM STING: a {StingDamage:0.#}x bite that keeps burning for {StingVenomTime:0}s, then it climbs away before the swarm can answer.",
        _ => "",
    };

    /// Stat line for cards and tooltips: HP · Bite · Armour · Speed.
    public static string StatLine(AntType t)
    {
        var st = Of(t);
        string ar = st.Armor > 0 ? $"  ·  Armour {st.Armor * 100:0}%" : "";
        return $"HP {st.Hp:0}  ·  Bite {st.Damage:0.#} / {st.Cooldown:0.##}s{ar}  ·  Speed {st.Speed:0.#}{(Ranged(t) ? "  ·  ranged" : "")}{(Flies(t) ? "  ·  flies" : "")}";
    }

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
    public const int StartPopCap = 20;      // the shaft and royal chamber alone; brood chambers add ChamberPop each
    public const int FoundingPop = 12;      // before the nest is dug nothing can hatch anyway
    public const float BaseFoodCap = 400f;  // what the first store holds; each extra store adds StorageFoodCap
    public const float StorageFoodCap = 400f;
    public const int StartWorkers = 10;
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
        new() { Name = "Ground Beetle", Hp = 290, Armor = BeetleArmor, Damage = 15f, Cooldown = 0.9f, Speed = 6.6f, Reach = 1.5f, Aggro = 12, Sight = 10, Size = 2.4f, Cost = 0, BuildTime = 0, CorpseFood = 40, Pop = 0 },
        new() { Name = "Wolf Spider", Hp = 240, Armor = SpiderArmor, Damage = 21f, Cooldown = 0.7f, Speed = 11.5f, Reach = 1.8f, Aggro = 15, Sight = 12, Size = 3.1f, Cost = 0, BuildTime = 0, CorpseFood = 35, Pop = 0 },
        new() { Name = "Stag Beetle", Hp = 800, Armor = StagArmor, Damage = 36f, Cooldown = 1.0f, Speed = 5.6f, Reach = 2.6f, Aggro = 14, Sight = 12, Size = 4.0f, Cost = 0, BuildTime = 0, CorpseFood = 160, Pop = 0 },
        new() { Name = "Hunting Wasp", Hp = 240, Armor = WaspArmor, Damage = 27f, Cooldown = 1.15f, Speed = 12.5f, Reach = 1.7f, Aggro = 17, Sight = 15, Size = 3.0f, Cost = 0, BuildTime = 0, CorpseFood = 45, Pop = 0 },
        new() { Name = "Flying Ant", Hp = 60, Damage = 7f, Cooldown = 0.55f, Speed = 11f, Reach = 1.5f, Aggro = 14, Sight = 18, Size = 1.35f, Cost = 60, BuildTime = 4.5f, CorpseFood = 7, Pop = 2 },
        new() { Name = "Acid Gunner", Hp = 55, Damage = 6f, Cooldown = 1.0f, Speed = 7f, Reach = 7f, Aggro = 13, Sight = 14, Size = 1.3f, Cost = 50, BuildTime = 4f, CorpseFood = 6, Pop = 1 },
        new() { Name = "Warlord", Hp = 760, Armor = WarlordArmor, Damage = 28f, Cooldown = 0.8f, Speed = 7.2f, Reach = 2.0f, Aggro = 15, Sight = 18, Size = 2.7f, Cost = 250, BuildTime = 20f, CorpseFood = 40, Pop = 4 },
        new() { Name = "Acid Matron", Hp = 620, Armor = MatronArmor, Damage = 18f, Cooldown = 0.9f, Speed = 6.5f, Reach = 6.5f, Aggro = 15, Sight = 18, Size = 2.5f, Cost = 350, BuildTime = 25f, CorpseFood = 40, Pop = 4 },
        new() { Name = "Sky Marshal", Hp = 560, Armor = MarshalArmor, Damage = 22f, Cooldown = 0.7f, Speed = 12f, Reach = 1.9f, Aggro = 16, Sight = 22, Size = 2.2f, Cost = 450, BuildTime = 30f, CorpseFood = 40, Pop = 4 },
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
        ProdKind.Warlord => "HERO. The plated brawler: cleaving bites, 30% armour, War Cry.\nLevels up to 10. Requires a Matriarch; stronger from a higher queen.",
        ProdKind.Matron => "HERO. The ranged acid spitter: splashing spray, Acid Burst and venom pools.\nLevels up to 10. Requires an Empress; stronger from a higher queen.",
        ProdKind.Marshal => "HERO. The winged commander: swoops from the air, Dive Bomb rallies the army.\nLevels up to 10. Requires an Ancient Empress and the Wing Chamber.",
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
