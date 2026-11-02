using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The optional goals that earn a level's second and third stars (the first star is the win).
public enum StarGoal { Time, Lost, AllSteps, AllFood, QueenHp, Escort, Stag, Lairs }

/// How the red colony behaves in a level: absent, a passive rival the script raids with, or an awake AI that raids on its own.
public enum RivalKind { None, Young, Grown, Awake, Empress }

/// Star-bought extras that change what the player's colony may do; fixed when a level starts.
[System.Flags]
public enum Perk { None = 0, Matron = 1, Gunner = 2, Marshal = 4, Flyer = 8, Veteran = 16 }

/// One level on the map. `Id` is the scenario: 1-4 are the PR #6 episodes (their rules live in Expedition.cs
/// and Campaign.cs), 5-12 the levels below. `Number` is its place on the map.
public sealed class LevelInfo
{
    public int Id, Season;
    public string Name, Blurb, Novelty;
    public RivalKind Rival;
    public uint Seed = 20261031;
    public (StarGoal goal, float value, string text)[] Stars;
    public float RivalFood = 0.5f;
    public int Number => Levels.NumberOf(Id);
}

/// The level sequence: four seasons of three, Episodes 1-4 re-slotted among the new levels.
public static class Levels
{
    public const int Count = 12;
    public static readonly int[] Order = { 1, 2, 5, 6, 3, 7, 8, 9, 4, 10, 11, 12 };
    public static readonly string[] Seasons = { "Spring", "Summer", "Autumn", "Late Summer" };
    public static readonly string[] SeasonThemes = { "Founding", "Rivals", "Wings", "The Empress" };

    public static int NumberOf(int id) => System.Array.IndexOf(Order, id) + 1;
    public static int IdAt(int number) => number >= 1 && number <= Count ? Order[number - 1] : 0;
    public static LevelInfo Info(int id) => id >= 1 && id <= Count ? _all[id - 1] : _all[0];

    static string Under(int minutes) => $"Win in under {minutes}:00";

    static readonly LevelInfo[] _all =
    {
        new() { Id = 1, Season = 0, Name = "The First Nest", Novelty = "The basics: lead, found, build, hatch", Rival = RivalKind.None,
            Blurb = "Escort a young queen down the grass alley and found her nest.",
            Stars = new[] { (StarGoal.Time, 540f, Under(9)), (StarGoal.Lost, 3f, "Lose no more than 3 ants") } },
        new() { Id = 2, Season = 0, Name = "The Foraging Season", Novelty = "The aphid herd, a lair raid, the first queen", Rival = RivalKind.Young, RivalFood = 0.35f,
            Blurb = "Dig a store, win the aphid herd, drive off a lair raid, kill a young red queen.",
            Stars = new[] { (StarGoal.Time, 1080f, Under(18)), (StarGoal.AllSteps, 0f, "Finish every step before the queen falls") } },
        new() { Id = 3, Season = 1, Name = "The Rival Colony", Novelty = "Scouting, research and an outpost", Rival = RivalKind.Grown, RivalFood = 0.6f,
            Blurb = "Scout the red mound, hold your own, break their outpost, kill their queen.",
            Stars = new[] { (StarGoal.Time, 1320f, Under(22)), (StarGoal.Lost, 60f, "Lose no more than 60 ants") } },
        new() { Id = 4, Season = 2, Name = "The Nuptial Flight", Novelty = "Heroes, the Old Stag and a deadline", Rival = RivalKind.Grown,
            Blurb = "Raise a hero, win over the Old Stag and end the red line before the flight.",
            Stars = new[] { (StarGoal.Time, 1200f, Under(20)), (StarGoal.Stag, 0f, "The Old Stag survives to the end") } },
        new() { Id = 5, Season = 0, Name = "Sweet Rain", Novelty = "Food effects and a storm deadline", Rival = RivalKind.None, Seed = 20261105,
            Blurb = "Taste honeydew, haul protein and fill the galleries before the storm.",
            Stars = new[] { (StarGoal.Time, 540f, Under(9)), (StarGoal.AllFood, 0f, "Carry home seeds, fruit, honeydew and protein") } },
        new() { Id = 6, Season = 1, Name = "The Hollow Log", Novelty = "Defence: wasps, spiders and the Hornet Mother", Rival = RivalKind.None, Seed = 20261106,
            Blurb = "Raise majors and hold the nest against five raids from the hollow log.",
            Stars = new[] { (StarGoal.Lost, 30f, "Lose no more than 30 ants"), (StarGoal.QueenHp, 0.7f, "Your queen never drops below 70%") } },
        new() { Id = 7, Season = 1, Name = "The Matron's Road", Novelty = "Escort: an Acid Matron hero", Rival = RivalKind.Young, Seed = 20261107, RivalFood = 0.4f,
            Blurb = "Find a wounded Acid Matron, bring her home through bug country, then strike.",
            Stars = new[] { (StarGoal.Time, 1200f, Under(20)), (StarGoal.Escort, 0f, "The Matron lives to the end") } },
        new() { Id = 8, Season = 2, Name = "The Dry Spell", Novelty = "Economy puzzle: the lairs are the larder", Rival = RivalKind.Grown, Seed = 20261108, RivalFood = 0.35f,
            Blurb = "The yard is bare. Hunt the bug lairs for their feasts and outlast the red colony.",
            Stars = new[] { (StarGoal.Time, 1560f, Under(26)), (StarGoal.Lairs, 0f, "Clear all three lairs before the win") } },
        new() { Id = 9, Season = 2, Name = "Wings and Acid", Novelty = "Flying ants, acid gunners, watch posts", Rival = RivalKind.Grown, Seed = 20261109, RivalFood = 0.3f,
            Blurb = "Grow wings, burn three watch posts that shield the red queen, then end her.",
            Stars = new[] { (StarGoal.Time, 1200f, Under(20)), (StarGoal.Lost, 50f, "Lose no more than 50 ants") } },
        new() { Id = 10, Season = 3, Name = "The Hornet Mother", Novelty = "A boss with her own nest who raids you", Rival = RivalKind.Grown, Seed = 20261110, RivalFood = 0.3f,
            Blurb = "A great hornet raids both colonies. Slay her in her nest, then the red queen.",
            Stars = new[] { (StarGoal.Time, 1500f, Under(25)), (StarGoal.AllSteps, 0f, "Slay the Hornet Mother before the queen") } },
        new() { Id = 11, Season = 3, Name = "The Long March", Novelty = "Time pressure: eight minutes, one army", Rival = RivalKind.Awake, Seed = 20261111,
            Blurb = "A strike army, an awake red colony and eight minutes before their mound seals.",
            Stars = new[] { (StarGoal.Time, 240f, Under(4)), (StarGoal.Lost, 60f, "Lose no more than 60 ants") } },
        new() { Id = 12, Season = 3, Name = "Empress of the Garden", Novelty = "The finale: every tool against an awake Empress", Rival = RivalKind.Empress, Seed = 20261112,
            Blurb = "Both mounds fortified, the Old Stag awake and an Empress who raids. End her.",
            Stars = new[] { (StarGoal.Time, 1500f, Under(25)), (StarGoal.QueenHp, 0.5f, "Your queen never drops below 50%") } },
    };

    public static string[] Intro(int id) => id switch
    {
        5 => new[]
        {
            "Warm rain has passed and the yard is glistening: honeydew on the clover, berries split open, beetles drowsy in the wet grass.",
            "Every food does something for the colony. Carry each kind home and learn what it gives you, then fill the galleries before the next storm floods the yard.",
        },
        6 => new[]
        {
            "A rotting log at the edge of the yard has filled with wasps and spiders. They have smelled the queen's brood.",
            "Raise majors, keep your soldiers close to home and break every raid. The last one will be led by the Hornet Mother herself.",
        },
        7 => new[]
        {
            "A scout came home with news: an Acid Matron of a fallen sister colony is alive, wounded, far across the yard.",
            "Find her and bring her home through bug country. Keep her alive: with her acid beside you, the young red queen across the yard will not last long.",
        },
        8 => new[]
        {
            "No rain for weeks. The seed caches are gone and the berries have dried to husks. The only feasts left are the bugs themselves.",
            "Hunt the lairs: every beetle and spider your army brings down is a meal your workers can haul home. The red colony is starving too. Outlast it, then end its queen.",
        },
        9 => new[]
        {
            "The red queen has grown cautious. Three watch posts of rooted guards stand around her mound, and while any of them stands she cannot be harmed.",
            "Grow wings. Flying ants cross the yard fast and acid gunners burn guards from range: burn the posts, then send everything down the shaft.",
        },
        10 => new[]
        {
            "A hornet as big as a beetle has built a paper nest in the yard. She sends her daughters at every colony she can smell, and yours is closest.",
            "Survive her first raid, raise a hero and take the fight to her nest. With her gone the red colony stands alone.",
        },
        11 => new[]
        {
            "Autumn is coming. The red colony is sealing its mound for the winter: in eight minutes no army will get in.",
            "You have one strike army and no time to grow another. March, break the gate guards and kill the queen before the seal.",
        },
        12 => new[]
        {
            "The red Empress rules every mound across the garden. Her colony is awake, fortified and hungry, and her raids will come whether you are ready or not.",
            "Everything you have learned is yours to use: heroes, wings, acid and the Old Stag sleeping by the raspberry. End her, and the garden is yours.",
        },
        _ => System.Array.Empty<string>(),
    };

    public static string Outro(int id) => id switch
    {
        5 => "The galleries are full and dry. When the storm broke, your colony slept on a fat harvest.",
        6 => "The Hornet Mother is dead and the hollow log is quiet. Your majors have become legends of the brood chambers.",
        7 => "The red queen is dead and the Acid Matron sleeps in your royal chamber, healed. Two colonies' memories now walk your tunnels.",
        8 => "The red queen starved and fell. When the rain returned, it found only your colony to feed.",
        9 => "The watch posts are ash and the red queen is dead. Your winged ants rule the air above the yard.",
        10 => "The paper nest is torn open and the red queen is dead. The garden's two great hunters are gone.",
        11 => "You reached her before the seal. The red queen is dead and your army comes home before the first frost.",
        12 => "The Empress is dead. Every mound beneath the grass belongs to your colony now.",
        _ => "",
    };

    public static string StarText(int id, int star) => star == 0 ? "Win the level" : Info(id).Stars[star - 1].text;

    // ------------------------------------------------------------------ unlocks bought with stars
    public sealed class Unlock
    {
        public int Stars;
        public string Name, Kind, Text;
        public Perk Perk;
        public int Colour = -1, Emblem = -1;
    }

    public static readonly Unlock[] Unlocks =
    {
        new() { Stars = 3, Name = "Clover emblem", Kind = "Emblem", Text = "A four-leaf clover for your colony's banner.", Emblem = 1 },
        new() { Stars = 6, Name = "Ember colony colour", Kind = "Colour", Text = "Your ants' crests and your banner glow ember red.", Colour = 1 },
        new() { Stars = 9, Name = "Acid Matron", Kind = "Hero", Text = "Raise the Acid Matron in any level that opens heroes.", Perk = Perk.Matron },
        new() { Stars = 12, Name = "Raspberry emblem", Kind = "Emblem", Text = "The great raspberry, for the colony that took it.", Emblem = 2 },
        new() { Stars = 15, Name = "Early acid gunners", Kind = "Caste", Text = "Acid gunners (and the Empress research they need) from Level 5 on.", Perk = Perk.Gunner },
        new() { Stars = 18, Name = "Saffron colony colour", Kind = "Colour", Text = "Crests and banner in warm saffron.", Colour = 2 },
        new() { Stars = 21, Name = "Sky Marshal", Kind = "Hero", Text = "Raise the Sky Marshal in any level that opens heroes.", Perk = Perk.Marshal },
        new() { Stars = 24, Name = "Early flying ants", Kind = "Caste", Text = "Flying ants (and the wings research) from Level 5 on.", Perk = Perk.Flyer },
        new() { Stars = 27, Name = "Stag Crest emblem", Kind = "Emblem", Text = "The Old Stag's antlers, borne by the colony it fights for.", Emblem = 3 },
        new() { Stars = 30, Name = "Rosewood colony colour", Kind = "Colour", Text = "Crests and banner in deep rosewood.", Colour = 3 },
        new() { Stars = 33, Name = "Veteran brood", Kind = "Perk", Text = "Every level from Level 2 on starts with an extra veteran squad.", Perk = Perk.Veteran },
        new() { Stars = 36, Name = "Crown emblem", Kind = "Emblem", Text = "The crown of the Eternal Mother: every star in the garden is yours.", Emblem = 4 },
    };

    public static readonly string[] ColourNames = { "Honey", "Ember", "Saffron", "Rosewood" };
    public static readonly Color[] Colours = { new(0.95f, 0.68f, 0.2f), new(0.98f, 0.4f, 0.14f), new(1f, 0.83f, 0.3f), new(0.78f, 0.33f, 0.27f) };
    public static readonly string[] EmblemNames = { "Mandibles", "Clover", "Raspberry", "Stag Crest", "Crown" };

    public static Perk PerksFor(int stars)
    {
        var p = Perk.None;
        foreach (var u in Unlocks) if (stars >= u.Stars) p |= u.Perk;
        return p;
    }

    public static Unlock NextUnlock(int stars)
    {
        foreach (var u in Unlocks) if (stars < u.Stars) return u;
        return null;
    }
}

public sealed partial class Expedition
{
    // ---------------------------------------------------------------- levels 5-12: generic objectives
    public enum Obj { Dig, Gather, Taste, Hatch, Research, Hero, Reach, Escort, Group, Waves, KillQueen }

    public sealed class Objective
    {
        public Obj Kind;
        public int A, B;             // Dig: chamber kind, count · Hatch: ant type, count · Research: upgrade, tier · Group: group · Waves: waves · Taste: food kind
        public float Amount;         // Gather
        public string Title, Text;   // Text may use {0} (have) and {1} (need)
        public string Done;          // an alternative text once the objective needs no more than waiting
    }

    public readonly List<Objective> Objectives = new();
    public readonly List<List<(int id, int serial)>> Groups = new();
    public readonly List<string> GroupNames = new();
    public readonly List<Vector2> GroupPos = new();
    public readonly List<bool> Larder = new();
    public LevelInfo Info => Levels.Info(Episode);
    public bool IsLevel => Episode >= 5;
    public Perk Perks;

    // waves: wild raiders (or red raids) sent from WaveFrom; they stop once StopGroup's members are dead
    public sealed class Wave { public int Team; public (AntType type, int level, int n)[] Bugs; public int Boss = -1; }
    public readonly List<Wave> Waves = new();
    public int WaveNext, StopGroup = -1;
    public float WaveAt = -1, WaveGap;
    public bool WaveRepeat;
    public Vector2 WaveFrom;
    public readonly List<(int id, int serial)> WaveBugs = new();

    // the escort
    public int EscortId = -1, EscortSerial;
    public bool Reached;
    public string EscortName = "";

    // the deadline (the nuptial flight, the storm, the seal) and the end
    public float Deadline;
    public string DeadlineName = "", DeadlineLoss = "";
    public readonly float[] KindBase = new float[5];
    public float QueenLow = 1;
    public float EndTime = -1;
    public int EndLost;
    public bool EndEscort, EndStag, EndLarders;

    public string Stake(float time)
    {
        string s = Info.Rival == RivalKind.None ? "Lose if your queen dies"
            : Step < Steps - 1 ? "Every step leads to the red queen; kill her any time to win. Lose if your queen dies"
            : "Lose if your queen dies";
        if (EscortId >= 0 && !Complete) s += $" or the {EscortName} falls";
        float at = Episode == 4 ? FlightAt : Deadline;
        if (at > 0)
        {
            int left = Mathf.Max(0, (int)(at - time));
            s += $" or {(Episode == 4 ? "the princesses fly" : DeadlineName)}: {left / 60}:{left % 60:00} left";
        }
        return s + ".";
    }

    string LevelGoal => Info.Rival switch
    {
        RivalKind.None => Episode == 5 ? "Fill the galleries before the storm." : "Break every raid from the hollow log.",
        RivalKind.Empress => "Kill the red Empress in her royal chamber.",
        _ => "Kill the red queen in her royal chamber.",
    };

    string LevelStepTitle(int i) => i < Objectives.Count ? Objectives[i].Title : "Level complete";

    string LevelStepInstruction
    {
        get
        {
            if (Step >= Objectives.Count) return "Level complete.";
            var o = Objectives[Step];
            string text = o.Done != null && Have >= Need && Need > 0 ? o.Done : o.Text;
            return string.Format(text, Have, Need);
        }
    }

    /// What this level lets the player hatch. Star perks add castes and heroes on top of the story's.
    bool LevelAllowsProd(ProdKind k)
    {
        int n = Info.Number;
        if (k is ProdKind.Matron) return n >= 9 && Perks.HasFlag(Perk.Matron) && Episode != 7;
        if (k is ProdKind.Marshal) return n >= 9 && Perks.HasFlag(Perk.Marshal);
        if (k is ProdKind.Gunner && n >= 5 && Perks.HasFlag(Perk.Gunner)) return true;
        if (k is ProdKind.Flyer && n >= 5 && Perks.HasFlag(Perk.Flyer)) return true;
        return Episode switch
        {
            5 => k is ProdKind.Worker or ProdKind.Soldier,
            6 or 7 or 8 => k is ProdKind.Worker or ProdKind.Soldier or ProdKind.Major,
            9 => k is ProdKind.Worker or ProdKind.Soldier or ProdKind.Major or ProdKind.Flyer or ProdKind.Gunner,
            _ => true,
        };
    }

    bool LevelAllowsChamber(ChamberKind k) => Episode switch
    {
        5 or 6 => k is ChamberKind.Brood or ChamberKind.Storage,
        7 or 8 => k is ChamberKind.Brood or ChamberKind.Storage or ChamberKind.Royal or ChamberKind.Forge or ChamberKind.Armory,
        _ => true,
    };

    bool LevelAllowsResearch(Upgrade u, int tier)
    {
        if (Info.Number >= 5 && Perks.HasFlag(Perk.Gunner) && u == Upgrade.Queen && tier <= 3) return true;
        if (Info.Number >= 5 && Perks.HasFlag(Perk.Flyer) && u == Upgrade.Wings) return true;
        return Episode switch
        {
            5 or 6 => false,
            7 or 8 => u == Upgrade.Queen ? tier <= 2 : u is Upgrade.Mandibles or Upgrade.Chitin && tier <= 1,
            9 => u == Upgrade.Queen ? tier <= 3 : u is Upgrade.Wings || u is Upgrade.Mandibles or Upgrade.Chitin && tier <= 2,
            _ => true,
        };
    }

    /// Perks that reach into an episode's own gates (Episodes 3 and 4 keep their story castes; stars only add).
    public bool PerkAllowsProd(ProdKind k) => k switch
    {
        ProdKind.Matron => Episode == 4 && Perks.HasFlag(Perk.Matron),
        ProdKind.Marshal => Episode == 4 && Perks.HasFlag(Perk.Marshal),
        ProdKind.Gunner => Info.Number >= 5 && Perks.HasFlag(Perk.Gunner),
        ProdKind.Flyer => Info.Number >= 5 && Perks.HasFlag(Perk.Flyer),
        _ => false,
    };

    public bool PerkAllowsResearch(Upgrade u, int tier) =>
        Info.Number >= 5 && (Perks.HasFlag(Perk.Gunner) && u == Upgrade.Queen && tier <= 3 || Perks.HasFlag(Perk.Flyer) && u == Upgrade.Wings);
}

public sealed partial class Sim
{
    /// Star perks the player bought: fixed before the first tick so every check after it is deterministic.
    public void ApplyPerks(Perk perks)
    {
        var m = Mission;
        if (m == null) return;
        m.Perks = perks;
        if (perks.HasFlag(Perk.Veteran) && m.Episode != 1)
        {
            var home = W.Nests[0].Pos;
            var toCentre = (new Vector2(Config.MapSize, Config.MapSize) * 0.5f - home).Normalized();
            var ids = new List<int>();
            for (int i = 0; i < 6; i++)
            {
                int id = Spawn(0, AntType.Soldier, OpenGround(home + toCentre * 11 + Rng.InDisk(3)), toCentre.AngleD(), 2);
                if (id < 0) break;
                Ants[id].State = AntState.Idle; ids.Add(id);
            }
            FormSquads(ids);
            var c = Col[0];
            c.PopUsed = 0; for (int t = 0; t < c.Count.Length; t++) c.PopUsed += c.Count[t] * Config.Stats[t].Pop;
        }
    }

    /// Sets up one of levels 5-12 on the open yard (called by BeginEpisode).
    void BeginLevel(int id)
    {
        var m = Mission;
        var home = W.Nests[0].Pos;
        var foe = W.Nests[1].Pos;
        var toFoe = (foe - home).Normalized();
        var side = new Vector2(toFoe.Y, -toFoe.X);
        var centre = new Vector2(Config.MapSize, Config.MapSize) * 0.5f;
        var player = Col[0];
        var info = m.Info;
        if (info.Rival == RivalKind.None) RemoveRival();
        switch (id)
        {
            case 5: // Sweet Rain: food effects and a storm
                SettleColony(0, brood: 1, stores: 1);
                player.Food = 150;
                Muster(0, AntType.Worker, 6, home);
                Muster(0, AntType.Soldier, 12, home + toFoe * 9);
                ClearCorridor(home, centre, -1);
                {
                    // a drowsy beetle pair beside a fallen cricket: the level's protein, a short walk from the mound
                    var at = OpenGround(home + toFoe * 34 - side * 16);
                    int g = AddGroup("Beetle pair", at);
                    for (int i = 0; i < 2; i++) GroupBug(g, AntType.Beetle, at + side * (i * 3 - 1.5f), 1);
                }
                m.Deadline = 900; m.DeadlineName = "the storm breaks"; m.DeadlineLoss = "The storm flooded the yard before the galleries were full. The colony starved through the wet weeks.";
                Add(Expedition.Obj.Dig, "Dig a second store", "Open the Chambers tab (N), choose Storage and click beside a connected room. Every store holds 400 more food.", a: (int)ChamberKind.Storage, b: 2);
                Add(Expedition.Obj.Taste, "Taste the honeydew", "Send workers to a honeydew drop (right-click it with workers selected). While honeydew keeps arriving, your workers run 15% faster.", a: (int)FoodKind.Sugar);
                Add(Expedition.Obj.Taste, "Bring home protein", "Kill the drowsy beetles in the wet grass (F2, then right-click them). Workers haul the carcasses home: protein makes the queen lay 30% faster.", a: (int)FoodKind.Corpse);
                Add(Expedition.Obj.Gather, "Fill the galleries", "Hatch workers (Z) and keep them foraging. Fruit heals your ants, seeds are heavy but rich. Gathered: {0}/{1}.", amount: 1600);
                break;
            case 6: // The Hollow Log: defence
                SettleColony(0, brood: 2, stores: 1);
                player.Tier[(int)Upgrade.Queen] = 2; Col[0].Chambers[0].Tier = 2;
                ApplyLevel(ref Ants[player.QueenId]); Ants[player.QueenId].Hp = Ants[player.QueenId].MaxHp;
                player.Food = 400;
                Muster(0, AntType.Worker, 8, home);
                Muster(0, AntType.Soldier, 18, home + toFoe * 9);
                Muster(0, AntType.Major, 3, home + toFoe * 12);
                m.WaveFrom = OpenGround(home + (centre - home).Normalized() * 85 + side * 25);
                ClearAround(m.WaveFrom, 22);
                ClearCorridor(home, m.WaveFrom, -1);
                AddGroup("Hollow log", m.WaveFrom);
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Spider, 1, 3), (AntType.Beetle, 1, 2) } });
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Wasp, 1, 4) } });
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Spider, 2, 3), (AntType.Wasp, 1, 3), (AntType.Beetle, 2, 1) } });
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Wasp, 2, 4), (AntType.Spider, 2, 2) } });
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Wasp, 5, 1), (AntType.Wasp, 2, 3) }, Boss = 0 });
                m.WaveAt = 75; m.WaveGap = 80;
                Add(Expedition.Obj.Hatch, "Raise majors", "The queen is a Matriarch: majors can hatch (C). Raise three more before the raids grow. Majors: {0}/{1}.", a: (int)AntType.Major, b: 6);
                Add(Expedition.Obj.Waves, "Hold the nest", "Raiders dive straight down your shaft for the queen. Keep your army by the mound and attack-move onto them. Raids beaten: {0}/{1}.", b: 4);
                Add(Expedition.Obj.Waves, "Slay the Hornet Mother", "The Hornet Mother leads the last raid: she stings hard and flies off between strikes. Surround her. Raiders left: {0}.", b: 5);
                break;
            case 7: // The Matron's Road: escort
                SettleColony(0, brood: 2, stores: 1);
                player.Tier[(int)Upgrade.Queen] = 2; Col[0].Chambers[0].Tier = 2;
                ApplyLevel(ref Ants[player.QueenId]); Ants[player.QueenId].Hp = Ants[player.QueenId].MaxHp;
                player.Food = 300;
                Muster(0, AntType.Worker, 8, home);
                Muster(0, AntType.Soldier, 14, home + toFoe * 9);
                Muster(0, AntType.Major, 2, home + toFoe * 12);
                YoungRival(foe, toFoe, 8);
                ClearCorridor(home, foe, -1);
                {
                    // she waits beyond the bug country on the far flank of the yard
                    var at = OpenGround(centre - side * 105 + toFoe * 10);
                    ClearAround(at, 14);
                    ClearCorridor(home, at, -1); // her road: only the two ambushes below lie on it
                    int mat = Spawn(0, AntType.Matron, at, (home - at).AngleD(), 3);
                    ref var a = ref Ants[mat];
                    a.State = AntState.Hold; a.Hp = a.MaxHp * 0.45f;
                    m.EscortId = mat; m.EscortSerial = a.Serial; m.EscortName = "Acid Matron";
                    // two ambushes on her road home
                    var mid = home.Lerp(at, 0.55f) + side * 6;
                    int g1 = AddGroup("Spider ambush", mid);
                    for (int i = 0; i < 3; i++) GroupBug(g1, AntType.Spider, mid + MathX.Dir(i * 2.1f) * 3, 1);
                    var near = home.Lerp(at, 0.8f) - side * 4;
                    int g2 = AddGroup("Beetle ambush", near);
                    for (int i = 0; i < 2; i++) GroupBug(g2, AntType.Beetle, near + side * (i * 3 - 1.5f), 2);
                }
                Add(Expedition.Obj.Reach, "Find the Matron", "She waits on the far flank of the yard, wounded. Send a squad to her (F2, right-click near her). Distance: {0}.");
                Add(Expedition.Obj.Escort, "Bring her home", "She walks home whenever no enemy is near her. Clear the road ahead and keep her alive. Distance to the mound: {0}.");
                Add(Expedition.Obj.KillQueen, "Kill the red queen", "Lead the Matron and your army down the red shaft: her acid burst melts packed defenders. The queen waits in her royal chamber.");
                break;
            case 8: // The Dry Spell: the lairs are the larder
                SettleColony(0, brood: 2, stores: 1);
                player.Tier[(int)Upgrade.Queen] = 2; Col[0].Chambers[0].Tier = 2;
                ApplyLevel(ref Ants[player.QueenId]); Ants[player.QueenId].Hp = Ants[player.QueenId].MaxHp;
                player.Food = 300;
                Muster(0, AntType.Worker, 6, home);
                Muster(0, AntType.Soldier, 30, home + toFoe * 9);
                Muster(0, AntType.Major, 6, home + toFoe * 12);
                GrownRival(foe, toFoe, workers: 8, soldiers: 12, majors: 1);
                Col[1].Food = 100;
                ClearCorridor(home, foe, -1);
                // the drought: every seed cache, berry and honeydew drop has dried up; the far corners keep a trickle
                foreach (var f in W.Food)
                    if (f.Kind != FoodKind.Corpse && (f.Pos.DistanceTo(home) < 115 || f.Pos.DistanceTo(foe) < 60)) { f.Amount = 0; ShrinkBlock(f); }
                foreach (var (at, bugs) in new[] { (home + toFoe * 50 + side * 40, AntType.Beetle), (home + toFoe * 70 - side * 38, AntType.Spider), (home + toFoe * 105 + side * 12, AntType.Wasp) })
                {
                    var p = OpenGround(at);
                    ClearAround(p, 14);
                    int g = AddGroup("Lair", p, larder: true);
                    for (int i = 0; i < 4; i++) GroupBug(g, bugs, p + MathX.Dir(i * 1.57f) * 3.5f, i == 0 ? 2 : 1);
                }
                m.WaveFrom = foe + (home - foe).Normalized() * 12;
                m.Waves.Add(new Expedition.Wave { Team = 1, Bugs = new[] { (AntType.Soldier, 1, 9), (AntType.Major, 1, 1) } });
                m.Waves.Add(new Expedition.Wave { Team = 1, Bugs = new[] { (AntType.Soldier, 1, 12), (AntType.Major, 1, 2) } });
                m.WaveAt = 330; m.WaveGap = 300;
                Add(Expedition.Obj.Group, "Raid a lair", "Your workers have nothing to forage. March the army on a bug lair (the gold rings): every bug it kills is a carcass to haul home. Lairs left: {0}.", b: -1);
                Add(Expedition.Obj.Gather, "Haul the feast home", "Workers carry the carcasses home on their own. Hatch more (Z) to haul faster, and hunt the next lair. Hauled: {0}/{1}.", amount: 350);
                Add(Expedition.Obj.KillQueen, "Kill the red queen", "The red colony is starving and its raids are thin. Gather the army and send everyone down the red shaft.");
                break;
            case 9: // Wings and Acid
                SettleColony(0, brood: 2, stores: 1);
                player.Tier[(int)Upgrade.Queen] = 3; Col[0].Chambers[0].Tier = 3;
                ApplyLevel(ref Ants[player.QueenId]); Ants[player.QueenId].Hp = Ants[player.QueenId].MaxHp;
                player.Food = 500;
                Muster(0, AntType.Worker, 14, home);
                Muster(0, AntType.Soldier, 30, home + toFoe * 9);
                Muster(0, AntType.Major, 4, home + toFoe * 12);
                GrownRival(foe, toFoe, workers: 8, soldiers: 12, majors: 2);
                ClearCorridor(home, foe, -1);
                foreach (float a in new[] { -0.9f, 0f, 0.9f })
                {
                    var dir = MathX.Dir((home - foe).AngleD() + a);
                    var p = OpenGround(foe + dir * 72);
                    ClearAround(p, 12);
                    int g = AddGroup("Watch post", p);
                    var face = (home - p).Normalized(); var across = new Vector2(face.Y, -face.X);
                    Track(g, SpawnGuardian(1, p, AntType.Gunner));
                    for (int i = 0; i < 3; i++)
                    {
                        int id2 = Spawn(1, AntType.Soldier, OpenGround(p + face * 3 + across * (i * 1.6f - 2.4f)), face.AngleD());
                        if (id2 >= 0) { Ants[id2].State = AntState.Hold; Track(g, id2); }
                    }
                }
                m.WaveFrom = foe + (home - foe).Normalized() * 12;
                m.Waves.Add(new Expedition.Wave { Team = 1, Bugs = new[] { (AntType.Soldier, 1, 10), (AntType.Major, 1, 2) } });
                m.WaveAt = 420; m.WaveGap = 0;
                Add(Expedition.Obj.Research, "Grow wings", "Open the Research tab and research Wings (W). Flying ants hatch only from a winged colony.", a: (int)Upgrade.Wings, b: 1);
                Add(Expedition.Obj.Hatch, "Hatch flying ants", "Hatch flying ants (F) and acid gunners (D). Flyers cross the yard fast; gunners burn guards from range. Flyers: {0}/{1}.", a: (int)AntType.Flyer, b: 6);
                Add(Expedition.Obj.Group, "Burn the watch posts", "While any watch post stands the red queen cannot be harmed. Strike them one by one (gold rings on the map). Posts left: {0}.", b: -2);
                Add(Expedition.Obj.KillQueen, "Kill the red queen", "Her shield is down. Send everything down the red shaft before she raises new guards.");
                break;
            case 10: // The Hornet Mother
                SettleColony(0, brood: 3, stores: 2);
                player.Tier[(int)Upgrade.Queen] = 2; Col[0].Chambers[0].Tier = 2;
                ApplyLevel(ref Ants[player.QueenId]); Ants[player.QueenId].Hp = Ants[player.QueenId].MaxHp;
                player.Food = 500;
                Muster(0, AntType.Worker, 14, home);
                Muster(0, AntType.Soldier, 20, home + toFoe * 9);
                Muster(0, AntType.Major, 4, home + toFoe * 12);
                GrownRival(foe, toFoe, workers: 10, soldiers: 8, majors: 1, queenTier: 2);
                ClearCorridor(home, foe, -1);
                {
                    var nest = OpenGround(centre + side * 70 - toFoe * 25);
                    ClearAround(nest, 20);
                    ClearCorridor(home, nest, -1);
                    int g = AddGroup("Paper nest", nest);
                    GroupBug(g, AntType.Wasp, nest, 5);
                    for (int i = 0; i < 4; i++) GroupBug(g, AntType.Wasp, nest + MathX.Dir(i * 1.57f) * 4, 2);
                    for (int i = 0; i < 2; i++) GroupBug(g, AntType.Spider, nest + MathX.Dir(0.8f + i * 3.1f) * 6, 2);
                    m.WaveFrom = nest; m.StopGroup = g; m.WaveRepeat = true;
                    m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Wasp, 2, 3), (AntType.Spider, 1, 2) } });
                    m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Wasp, 2, 4), (AntType.Spider, 2, 2) } });
                    m.WaveAt = 120; m.WaveGap = 160;
                }
                Add(Expedition.Obj.Waves, "Beat the first hornet raid", "The Hornet Mother sends her daughters at your mound. Keep the army home and break the raid. Raiders left: {0}.", b: 1);
                Add(Expedition.Obj.Hero, "Raise a hero", "Open the Heroes tab and raise a hero (Y for the Warlord). A hero levels fast and fights with special moves.");
                Add(Expedition.Obj.Group, "Slay the Hornet Mother", "Take the army to the paper nest (gold ring) and kill her. While she lives the raids keep coming. Hornets left: {0}.", b: 0);
                Add(Expedition.Obj.KillQueen, "Kill the red queen", "The yard is quiet. Gather the army, march on the red mound and send everyone down the shaft.");
                break;
            case 11: // The Long March
                SettleColony(0, brood: 3, stores: 2);
                player.Tier[(int)Upgrade.Queen] = 3; player.Tier[(int)Upgrade.Wings] = 1; Col[0].Chambers[0].Tier = 3;
                ApplyLevel(ref Ants[player.QueenId]); Ants[player.QueenId].Hp = Ants[player.QueenId].MaxHp;
                player.Food = 600;
                Muster(0, AntType.Worker, 10, home);
                Muster(0, AntType.Soldier, 30, home + toFoe * 9);
                Muster(0, AntType.Major, 6, home + toFoe * 12);
                Muster(0, AntType.Gunner, 6, home + toFoe * 14);
                Muster(0, AntType.Flyer, 4, home + toFoe * 10);
                { int hero = Spawn(0, AntType.Warlord, OpenGround(home + toFoe * 12), toFoe.AngleD(), 3); if (hero >= 0) Ants[hero].State = AntState.Idle; }
                GrownRival(foe, toFoe, workers: 18, soldiers: 26, majors: 6, gunners: 4, queenTier: 4);
                SpawnNestGuardians(1);
                foreach (float along in new[] { 0.55f })
                {
                    // pickets across the yard: the march has to fight its way over
                    var mid = OpenGround(home.Lerp(foe, along));
                    Muster(1, AntType.Soldier, 14, mid);
                    Muster(1, AntType.Major, 3, mid + toFoe * 3);
                    Muster(1, AntType.Gunner, 2, mid + toFoe * 5);
                }
                ClearCorridor(home, foe, -1);
                m.Deadline = 480; m.DeadlineName = "the mound seals"; m.DeadlineLoss = "The red colony sealed its mound for winter. Your army waits outside in the cold.";
                {
                    var mid = OpenGround(home.Lerp(foe, 0.62f));
                    AddGroup("The far side", mid);
                }
                Add(Expedition.Obj.Reach, "Cross the yard", "Select the whole army (F2, F4 for flyers) and attack-move to the gold ring past the middle. Distance: {0}.", b: 0);
                Add(Expedition.Obj.Group, "Break the gate guards", "Four guards grown into the red mound shield the queen. Break them with everything. Guards left: {0}.", b: -3);
                Add(Expedition.Obj.KillQueen, "Kill the red queen", "The gate is open. Down the shaft, before the seal.");
                break;
            default: // 12: Empress of the Garden
                SettleColony(0, brood: 3, stores: 2);
                player.Tier[(int)Upgrade.Queen] = 2; Col[0].Chambers[0].Tier = 2;
                ApplyLevel(ref Ants[player.QueenId]); Ants[player.QueenId].Hp = Ants[player.QueenId].MaxHp;
                player.Food = 700;
                Muster(0, AntType.Worker, 18, home);
                Muster(0, AntType.Soldier, 24, home + toFoe * 9);
                Muster(0, AntType.Major, 4, home + toFoe * 12);
                SpawnNestGuardians(0);
                GrownRival(foe, toFoe, workers: 18, soldiers: 16, majors: 4, gunners: 2, queenTier: 3);
                SpawnNestGuardians(1);
                WakeOldStag(centre);
                ClearCorridor(home, foe, StagId);
                Add(Expedition.Obj.Hero, "Raise a hero", "Open the Heroes tab and raise a hero. The Empress's raids will come on their own: keep soldiers home.");
                Add(Expedition.Obj.Group, "Rouse the Old Stag", "Beat the Old Stag by the great raspberry with your whole army and it joins you. Its bite shatters mound guards. Its strength: {0}%.", b: -4);
                Add(Expedition.Obj.Group, "Break the guardians", "While her mound guardians stand the Empress cannot be harmed. Lead the Old Stag at them. Guardians left: {0}.", b: -3);
                Add(Expedition.Obj.KillQueen, "Kill the Empress", "The way is open. Send everything down the red shaft and end her.");
                break;
        }
        foreach (var c in Col) { c.PopUsed = 0; for (int t = 0; t < c.Count.Length; t++) c.PopUsed += c.Count[t] * Config.Stats[t].Pop; }
        for (int k = 0; k < 5; k++) m.KindBase[k] = player.GatheredBy[k];
    }

    void Add(Expedition.Obj kind, string title, string text, int a = 0, int b = 0, float amount = 0, string done = null)
        => Mission.Objectives.Add(new Expedition.Objective { Kind = kind, Title = title, Text = text, A = a, B = b, Amount = amount, Done = done });

    int AddGroup(string name, Vector2 at, bool larder = false)
    {
        var m = Mission;
        m.Groups.Add(new List<(int, int)>()); m.GroupNames.Add(name); m.GroupPos.Add(at); m.Larder.Add(larder);
        return m.Groups.Count - 1;
    }

    void Track(int group, int id) { if (id >= 0) Mission.Groups[group].Add((id, Ants[id].Serial)); }

    void GroupBug(int group, AntType type, Vector2 p, int level)
    {
        int id = Spawn(Config.Wild, type, OpenGround(p), Rng.R(0, DMath.Tau), level);
        if (id < 0) return;
        Ants[id].State = AntState.Idle; Ants[id].Home = Mission.GroupPos[group];
        Track(group, id);
    }

    /// No red colony in this level: remove it quietly (a death event would award a queen kill).
    void RemoveRival()
    {
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (!a.Alive || a.Team != 1) continue;
            Col[1].Count[(int)a.Type]--; a.Alive = false; _free.Push(i);
        }
        Col[1].Alive = false; Col[1].Chambers.Clear(); Col[1].Queue.Clear();
    }

    /// Wild bugs living within `r` of a spot the level needs leave it.
    void ClearAround(Vector2 p, float r)
    {
        for (int i = 0; i < High; i++)
        {
            ref var b = ref Ants[i];
            if (!b.Alive || b.Team != Config.Wild || b.Home.DistanceTo(p) > r && b.Pos.DistanceTo(p) > r) continue;
            b.Alive = false; WildCount--; _free.Push(i);
        }
    }

    void YoungRival(Vector2 foe, Vector2 toFoe, int soldiers)
    {
        SettleColony(1, brood: 1, stores: 0);
        Col[1].Food = 100;
        Muster(1, AntType.Soldier, soldiers, foe - toFoe * 9);
    }

    void GrownRival(Vector2 foe, Vector2 toFoe, int workers, int soldiers, int majors, int gunners = 0, int queenTier = 1)
    {
        Col[1].Tier[(int)Upgrade.Queen] = queenTier;
        SettleColony(1, brood: 2, stores: 1);
        Col[1].Food = 300;
        Muster(1, AntType.Worker, workers, foe);
        Muster(1, AntType.Soldier, soldiers, foe - toFoe * 9);
        if (majors > 0) Muster(1, AntType.Major, majors, foe - toFoe * 12);
        if (gunners > 0) Muster(1, AntType.Gunner, gunners, foe - toFoe * 7);
    }

    /// The finale's Old Stag: the stag lord nearest the centre, made a boss that joins whoever beats it.
    void WakeOldStag(Vector2 centre)
    {
        int stag = -1;
        for (int i = 0; i < High; i++)
            if (Ants[i].Alive && Ants[i].Team == Config.Wild && Ants[i].Type == AntType.StagBeetle && (stag < 0 || Ants[i].Pos.DistanceTo(centre) < Ants[stag].Pos.DistanceTo(centre))) stag = i;
        if (stag < 0) { stag = Spawn(Config.Wild, AntType.StagBeetle, OpenGround(centre + new Vector2(8, 8)), 0); Ants[stag].State = AntState.Idle; Ants[stag].Home = Ants[stag].Pos; }
        ref var s = ref Ants[stag];
        s.Level = 2; s.Xp = Config.XpFor(s.Type, 2); s.Boss = true;
        ApplyLevel(ref s); s.Hp = s.MaxHp;
        StagId = stag; StagSerial = s.Serial; StagTeam = -1; StagLair = s.Home;
    }

    /// Group -1: every larder lair; -2: every watch post; -3: the red mound guardians; -4: the Old Stag (until roused).
    int GroupLeft(int g)
    {
        var m = Mission;
        if (g == -3) { int k = 0; for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Guardian && Ants[i].Team == 1 && Ants[i].Pos.DistanceTo(W.Nests[1].Pos) < 16) k++; return k; }
        if (g == -4) return StagTeam == 0 ? 0 : 1;
        int n = 0;
        for (int i = 0; i < m.Groups.Count; i++)
            if (g >= 0 ? i == g : g == -1 ? m.Larder[i] : m.GroupNames[i] == "Watch post")
                n += g >= 0 ? Living(m.Groups[i]) : Living(m.Groups[i]) > 0 ? 1 : 0;
        return n;
    }

    /// Wings and Acid: the red queen is shielded while any watch post stands.
    public bool PostsShield(int team) => team == Config.Enemy && Mission is { Episode: 9 } && GroupLeft(-2) > 0;

    void TickLevel(float dt)
    {
        var m = Mission;
        // waves
        bool stopped = m.StopGroup >= 0 && Living(m.Groups[m.StopGroup]) == 0;
        if (m.WaveAt >= 0 && Time >= m.WaveAt && !stopped && (m.WaveNext < m.Waves.Count || m.WaveRepeat) && m.Waves.Count > 0)
        {
            var w = m.Waves[Mathf.Min(m.WaveNext, m.Waves.Count - 1)];
            if (w.Team == Config.Wild || Col[w.Team].Alive) LaunchWave(w);
            m.WaveNext++;
            m.WaveAt = (m.WaveNext < m.Waves.Count || m.WaveRepeat) && m.WaveGap > 0 ? Time + m.WaveGap : -1;
        }
        // the escort walks home whenever nobody is threatening her and the player has not taken her elsewhere
        if (m.EscortId >= 0)
        {
            if (!Valid(m.EscortId, m.EscortSerial))
            {
                m.LossReason = $"The {m.EscortName} has fallen. Without her the colony's hopes die in the grass.";
                Winner = Config.Enemy; return;
            }
            ref var e = ref Ants[m.EscortId];
            var home = W.Nests[0].Pos;
            if (m.Reached && Ticks % 10 == 0 && (e.State is AntState.Idle or AntState.Hold || e.State == AntState.Move && e.Goal.DistanceTo(home) < 1) && e.Pos.DistanceTo(home) > 12)
            {
                if (Threat(e.Pos, 9)) { if (e.State == AntState.Move) { e.State = AntState.Idle; e.Goal = e.Pos; } }
                else { e.State = AntState.Move; e.Goal = e.Anchor = home; e.GoalLayer = 0; }
            }
        }
        if (m.Deadline > 0 && (Col[1].Alive || m.Info.Rival == RivalKind.None) && Time >= m.Deadline && !m.Complete)
        {
            m.LossReason = m.DeadlineLoss;
            Winner = Config.Enemy; return;
        }
        UpdateLevel();
        while (m.Step < m.Steps && LevelStepDone(m.Objectives[m.Step]))
        {
            m.Step++;
            if (m.Step < m.Steps && m.Objectives[m.Step].Kind == Expedition.Obj.Escort) { m.Reached = true; ref var e = ref Ants[m.EscortId]; e.State = AntState.Idle; }
            if (m.Step > 0 && m.Objectives[m.Step - 1].Kind == Expedition.Obj.Escort && Valid(m.EscortId, m.EscortSerial))
            { ref var e = ref Ants[m.EscortId]; e.Hp = e.MaxHp; e.State = AntState.Idle; } // home: the nurses heal her
            UpdateLevel();
        }
        // a level without a rival is won by its last objective
        if (m.Info.Rival == RivalKind.None && m.Complete && Winner < 0) Winner = Config.Player;
    }

    bool Threat(Vector2 p, float r)
    {
        for (int i = 0; i < High; i++)
        {
            ref var a = ref Ants[i];
            if (a.Alive && a.Team != 0 && a.Layer == 0 && a.Type != AntType.Worker && a.Pos.DistanceTo(p) < r) return true;
        }
        return false;
    }

    bool LevelStepDone(Expedition.Objective o)
    {
        var m = Mission;
        var c = Col[0];
        return o.Kind switch
        {
            Expedition.Obj.Dig => ChamberCount(0, (ChamberKind)o.A, true) >= o.B,
            Expedition.Obj.Gather => c.Gathered - m.GatherBase >= o.Amount,
            Expedition.Obj.Taste => c.GatheredBy[o.A] > m.KindBase[o.A],
            Expedition.Obj.Hatch => c.Count[o.A] >= o.B,
            Expedition.Obj.Research => c.T((Upgrade)o.A) >= o.B,
            Expedition.Obj.Hero => c.Count[(int)AntType.Warlord] + c.Count[(int)AntType.Matron] + c.Count[(int)AntType.Marshal] > 0,
            Expedition.Obj.Reach => m.Have <= (m.EscortId >= 0 ? 10 : 16),
            Expedition.Obj.Escort => m.Have <= 12,
            Expedition.Obj.Group => o.B == -1 ? GroupLeft(-1) < CountLarders() : GroupLeft(o.B) == 0,
            Expedition.Obj.Waves => m.WaveNext >= o.B && Living(m.WaveBugs) == 0,
            Expedition.Obj.KillQueen => !Col[1].Alive,
            _ => false,
        };
    }

    int CountLarders() { int n = 0; foreach (bool l in Mission.Larder) if (l) n++; return n; }

    /// The guide panel's numbers and the map marker for the current objective.
    void UpdateLevel()
    {
        var m = Mission;
        var c = Col[0];
        var home = W.Nests[0].Pos;
        if (Col[0].QueenId >= 0 && Ants[c.QueenId].Alive) m.QueenLow = Mathf.Min(m.QueenLow, Ants[c.QueenId].Hp / Ants[c.QueenId].MaxHp);
        m.Have = m.Need = 0;
        m.MarkerPos = home;
        if (m.Step >= m.Objectives.Count) return;
        var o = m.Objectives[m.Step];
        switch (o.Kind)
        {
            case Expedition.Obj.Gather: m.Have = (int)(c.Gathered - m.GatherBase); m.Need = (int)o.Amount; break;
            case Expedition.Obj.Hatch: m.Have = c.Count[o.A]; m.Need = o.B; break;
            case Expedition.Obj.Taste:
                m.MarkerPos = NearestFood((FoodKind)o.A, home, m.Groups.Count > 0 ? m.GroupPos[0] : home);
                break;
            case Expedition.Obj.Reach:
                var to = m.EscortId >= 0 && Valid(m.EscortId, m.EscortSerial) ? Ants[m.EscortId].Pos : m.GroupPos[o.B];
                m.MarkerPos = to;
                float best = float.MaxValue;
                for (int i = 0; i < High; i++)
                    if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Layer == 0 && Ants[i].Type != AntType.Queen && i != m.EscortId && Config.IsWarrior(Ants[i].Type)) best = Mathf.Min(best, Ants[i].Pos.DistanceTo(to));
                m.Have = best == float.MaxValue ? 999 : (int)best;
                break;
            case Expedition.Obj.Escort:
                if (Valid(m.EscortId, m.EscortSerial)) { m.MarkerPos = Ants[m.EscortId].Pos; m.Have = (int)Ants[m.EscortId].Pos.DistanceTo(home); }
                break;
            case Expedition.Obj.Group:
                m.Have = o.B == -4 ? (Valid(StagId, StagSerial) ? (int)(100 * Ants[StagId].Hp / Ants[StagId].MaxHp) : 0) : GroupLeft(o.B);
                m.MarkerPos = GroupMarker(o.B, home);
                break;
            case Expedition.Obj.Waves:
                int left = Living(m.WaveBugs);
                m.Have = m.WaveNext - (left > 0 ? 1 : 0); m.Need = o.B;
                if (o.B == 1 || m.Step == m.Steps - 1 && m.Episode == 6) { m.Have = left; m.Need = 0; }
                m.MarkerPos = left > 0 ? Front(m.WaveBugs, home) : m.WaveFrom;
                break;
            case Expedition.Obj.KillQueen: m.MarkerPos = W.Nests[1].Pos; break;
            case Expedition.Obj.Dig: m.Have = ChamberCount(0, (ChamberKind)o.A, true); m.Need = o.B; break;
        }
        if (o.Kind == Expedition.Obj.Reach && m.EscortId < 0) m.Have = Mathf.Max(0, m.Have);
    }

    Vector2 GroupMarker(int g, Vector2 home)
    {
        var m = Mission;
        if (g == -3) return W.Nests[1].Pos;
        if (g == -4) return Valid(StagId, StagSerial) ? Ants[StagId].Pos : StagLair;
        Vector2 at = home; float best = float.MaxValue;
        for (int i = 0; i < m.Groups.Count; i++)
        {
            bool match = g >= 0 ? i == g : g == -1 ? m.Larder[i] : m.GroupNames[i] == "Watch post";
            if (!match || Living(m.Groups[i]) == 0) continue;
            float d = m.GroupPos[i].DistanceTo(home);
            if (d < best) { best = d; at = m.GroupPos[i]; }
        }
        return at;
    }

    Vector2 Front(List<(int id, int serial)> bugs, Vector2 home)
    {
        var at = home; float best = float.MaxValue;
        foreach (var (id, serial) in bugs)
            if (Valid(id, serial) && Ants[id].Pos.DistanceTo(home) < best) { best = Ants[id].Pos.DistanceTo(home); at = Ants[id].Pos; }
        return at;
    }

    Vector2 NearestFood(FoodKind kind, Vector2 home, Vector2 fallback)
    {
        var at = fallback; float best = float.MaxValue;
        foreach (var f in W.Food)
            if (f.Kind == kind && f.Alive && f.Pos.DistanceTo(home) < best) { best = f.Pos.DistanceTo(home); at = f.Pos; }
        return at;
    }

    /// A raid from the level's lair (wild raiders dive into the nest for the queen) or from the red mound.
    void LaunchWave(Expedition.Wave w)
    {
        var m = Mission;
        var home = W.Nests[0].Pos;
        foreach (var (type, level, n) in w.Bugs)
            for (int i = 0; i < n; i++)
            {
                int id = Spawn(w.Team, type, OpenGround(m.WaveFrom + Rng.InDisk(4)), (home - m.WaveFrom).AngleD(), level);
                if (id < 0) continue;
                ref var a = ref Ants[id];
                a.Raider = w.Team == Config.Wild;
                if (a.Raider) a.Home = home;
                a.State = AntState.AttackMove; a.Goal = a.Anchor = home; a.GoalLayer = 0;
                m.WaveBugs.Add((id, a.Serial));
                if (w.Boss >= 0 && level >= 5 && w.Boss < m.Groups.Count) m.Groups[w.Boss].Add((id, a.Serial));
            }
        Events.Add(new SimEvent { Kind = EvKind.UnderAttack, Pos = home, Team = 0 });
    }

    /// Records the end state the stars are judged on (the tick the level was decided).
    void MarkEnd()
    {
        var m = Mission;
        if (m == null || m.EndTime >= 0 || Winner < 0) return;
        m.EndTime = Time;
        m.EndLost = Col[0].Lost;
        m.EndEscort = m.EscortId >= 0 && Valid(m.EscortId, m.EscortSerial);
        m.EndStag = StagTeam == 0 && Valid(StagId, StagSerial);
        bool larders = true;
        for (int i = 0; i < m.Groups.Count; i++) if (m.Larder[i] && Living(m.Groups[i]) > 0) larders = false;
        m.EndLarders = larders && CountLarders() > 0;
    }

    /// The stars a won level earns: bit 0 the win, bits 1 and 2 its two optional goals.
    public int Stars()
    {
        var m = Mission;
        if (m == null || Winner != Config.Player) return 0;
        MarkEnd();
        int stars = 1;
        var goals = m.Info.Stars;
        for (int s = 0; s < goals.Length; s++)
        {
            var (goal, value, _) = goals[s];
            bool ok = goal switch
            {
                StarGoal.Time => m.EndTime <= value,
                StarGoal.Lost => m.EndLost <= value,
                StarGoal.AllSteps => m.Episode == 1 || m.StepsDone >= m.Steps - 1,
                StarGoal.AllFood => Col[0].GatheredBy[(int)FoodKind.Seed] > m.KindBase[(int)FoodKind.Seed] && Col[0].GatheredBy[(int)FoodKind.Berry] > m.KindBase[(int)FoodKind.Berry]
                                    && Col[0].GatheredBy[(int)FoodKind.Sugar] > m.KindBase[(int)FoodKind.Sugar] && Col[0].GatheredBy[(int)FoodKind.Corpse] > m.KindBase[(int)FoodKind.Corpse],
                StarGoal.QueenHp => m.QueenLow >= value,
                StarGoal.Escort => m.EndEscort,
                StarGoal.Stag => m.EndStag,
                StarGoal.Lairs => m.EndLarders,
                _ => false,
            };
            if (ok) stars |= 2 << s;
        }
        return stars;
    }

    /// Runs every tick of a campaign game, whatever the level: tracks the queen's lowest health and the end.
    void TrackStars()
    {
        var m = Mission;
        var c = Col[0];
        if (Winner < 0 && c.QueenId >= 0 && Ants[c.QueenId].Alive) m.QueenLow = Mathf.Min(m.QueenLow, Ants[c.QueenId].Hp / Ants[c.QueenId].MaxHp);
        if (Winner >= 0) MarkEnd();
    }
}
