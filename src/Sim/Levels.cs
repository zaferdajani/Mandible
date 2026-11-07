using System.Collections.Generic;
using Godot;

namespace Mandible;

/// The optional goals that earn a level's second and third stars (the first star is the win).
public enum StarGoal { Time, Lost, AllSteps, AllFood, QueenHp, Escort, Stag, Lairs, Master, LarvaSafe }

/// How the red colony behaves in a level: absent, a passive rival the script raids with, or an awake AI that raids on its own.
public enum RivalKind { None, Young, Grown, Awake, Empress }

/// Star-bought extras that change what the player's colony may do; fixed when a level starts.
[System.Flags]
public enum Perk { None = 0, Matron = 1, Gunner = 2, Marshal = 4, Flyer = 8, Veteran = 16 }

/// One level on the map. `Id` is the scenario: 1-4 are the PR #6 episodes (their rules live in Expedition.cs
/// and Campaign.cs), 5-12 the levels below, 13-14 the mutation chapters. `Number` is its place on the map.
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

/// The level sequence: one story in four acts, Episodes 1-4 re-slotted among the new levels. Act I has no rival
/// colony (the red ants are only hinted at); they arrive as an opponent at the start of Act II. Act II also holds the
/// two mutation chapters: the Silk Trap (the first mutagen, the venom stingers) before the first queen kill, and the
/// Royal Larva (the first hero) right after it. What each chapter introduces: Reveal (CampaignReveal.cs).
public static class Levels
{
    public const int Count = 14;
    public static readonly int[] Order = { 1, 5, 6, 7, 13, 2, 14, 3, 10, 8, 9, 4, 11, 12 };
    /// The four acts of the colony's story (LevelInfo.Season is the act).
    public static readonly string[] Seasons = { "Act I", "Act II", "Act III", "Act IV" };
    public static readonly string[] SeasonThemes = { "The Broken Tube", "Strange Blood", "Hard Times", "The Ember Empress" };

    /// The colony's level with no save behind it: one for every queen killed before this chapter (from Ashling on).
    public static int StoryLevel(int chapter)
    {
        int level = 1;
        for (int n = Reveal.FirstQueenKill; n < chapter && n <= Count; n++)
        {
            var info = Info(IdAt(n));
            if (info.Rival != RivalKind.None && info.Id != Reveal.SilkTrapId) level++;
        }
        return level;
    }

    /// A chapter's stable file name (videos and the like): its name in lower case with dashes.
    public static string Slug(int id) => Info(id).Name.ToLowerInvariant().Replace("'", "").Replace(' ', '-');

    public static int NumberOf(int id) => System.Array.IndexOf(Order, id) + 1;
    public static int IdAt(int number) => number >= 1 && number <= Count ? Order[number - 1] : 0;
    public static LevelInfo Info(int id) { foreach (var l in _all) if (l.Id == id) return l; return _all[0]; }

    static string Under(int minutes) => $"Win in under {minutes}:00";

    static readonly LevelInfo[] _all =
    {
        new() { Id = 1, Season = 0, Name = "The First Nest", Novelty = "The hunt, remains, founding and workers", Rival = RivalKind.None,
            Blurb = "The tube is broken. Hunt the beetle in the grass alley, stock a clearing with seeds and its remains, and found the Glass Queen's nest.",
            Stars = new[] { (StarGoal.Time, 540f, Under(9)), (StarGoal.Lost, 3f, "Lose no more than 3 ants") } },
        new() { Id = 2, Season = 1, Name = "The Foraging Season", Novelty = "The first queen kill: Ashling, and her glowing body", Rival = RivalKind.Young, RivalFood = 0.35f,
            Blurb = "Dig a store, win the aphid herd, drive off a lair raid and end Ashling, the red raiders' queen.",
            Stars = new[] { (StarGoal.Time, 1080f, Under(18)), (StarGoal.AllSteps, 0f, "Finish every step before the queen falls") } },
        new() { Id = 3, Season = 1, Name = "Queen Cinder", Novelty = "Research, scouting and an outpost", Rival = RivalKind.Grown, RivalFood = 0.6f,
            Blurb = "Scout Queen Cinder's mound, hold your own, break her outpost and end her.",
            Stars = new[] { (StarGoal.Time, 1320f, Under(22)), (StarGoal.Lost, 60f, "Lose no more than 60 ants") } },
        new() { Id = 4, Season = 3, Name = "The Nuptial Flight", Novelty = "The Old Stag and a deadline", Rival = RivalKind.Grown,
            Blurb = "Raise a general, win over the Old Stag and end the red line before the flight.",
            Stars = new[] { (StarGoal.Time, 1200f, Under(20)), (StarGoal.Stag, 0f, "The Old Stag survives to the end") } },
        new() { Id = 5, Season = 0, Name = "Sweet Rain", Novelty = "Soldiers, food effects and a storm", Rival = RivalKind.None, Seed = 20261105,
            Blurb = "Raise Burr a squad, taste honeydew, haul protein and fill the galleries before the storm.",
            Stars = new[] { (StarGoal.Time, 540f, Under(9)), (StarGoal.AllFood, 0f, "Carry home seeds, fruit, honeydew and protein") } },
        new() { Id = 6, Season = 0, Name = "The Hollow Log", Novelty = "Majors, spiders and raids", Rival = RivalKind.None, Seed = 20261106,
            Blurb = "Raise majors and hold the nest against five raids from the hollow log.",
            Stars = new[] { (StarGoal.Lost, 30f, "Lose no more than 30 ants"), (StarGoal.QueenHp, 0.7f, "Your queen never drops below 70%") } },
        new() { Id = 7, Season = 1, Name = "The Matron's Road", Novelty = "The red colony and an escort", Rival = RivalKind.Young, Seed = 20261107, RivalFood = 0.4f,
            Blurb = "Red ants walk your trails. Bring Vetch, the tube's Acid Matron, home through their ground and break their raid.",
            Stars = new[] { (StarGoal.Time, 900f, Under(15)), (StarGoal.Escort, 0f, "Vetch lives to the end") } },
        new() { Id = 8, Season = 2, Name = "The Dry Spell", Novelty = "Economy puzzle: the lairs are the larder", Rival = RivalKind.Grown, Seed = 20261108, RivalFood = 0.35f,
            Blurb = "The yard is bare. Hunt the bug lairs for their feasts and outlast the red colony.",
            Stars = new[] { (StarGoal.Time, 1560f, Under(26)), (StarGoal.Lairs, 0f, "Clear all three lairs before the win") } },
        new() { Id = 9, Season = 2, Name = "Wings and Acid", Novelty = "Wings research, acid gunners, watch posts", Rival = RivalKind.Grown, Seed = 20261109, RivalFood = 0.3f,
            Blurb = "Grow wings, burn three watch posts that shield the red queen, then end her.",
            Stars = new[] { (StarGoal.Time, 1200f, Under(20)), (StarGoal.Lost, 50f, "Lose no more than 50 ants") } },
        new() { Id = 10, Season = 2, Name = "The Hornet Mother", Novelty = "Generals, wasps and a boss who raids", Rival = RivalKind.Grown, Seed = 20261110, RivalFood = 0.3f,
            Blurb = "The Hornet Mother raids from her paper nest. Lead old Burr, the Warlord, against her and end her.",
            Stars = new[] { (StarGoal.Time, 1500f, Under(25)), (StarGoal.AllSteps, 0f, "Slay the Hornet Mother before the queen") } },
        new() { Id = 11, Season = 3, Name = "The Long March", Novelty = "Time pressure: eight minutes, one army", Rival = RivalKind.Awake, Seed = 20261111,
            Blurb = "A strike army, an awake red colony and eight minutes before their mound seals.",
            Stars = new[] { (StarGoal.Time, 240f, Under(4)), (StarGoal.Lost, 60f, "Lose no more than 60 ants") } },
        new() { Id = 13, Season = 1, Name = "The Silk Trap", Novelty = "Mutagen, the sixth sense and the venom stingers", Rival = RivalKind.Young, Seed = 20261113, RivalFood = 0.3f,
            Blurb = "A dead queen wasp hangs in a spider's web, and something in her glows. Carve out the gland, haul it home and feed it to the Glass Queen.",
            Stars = new[] { (StarGoal.Time, 840f, Under(14)), (StarGoal.Master, 0f, "Slay the web spider") } },
        new() { Id = 14, Season = 1, Name = "The Royal Larva", Novelty = "The general mutation: a royal larva, a cocoon, the first general", Rival = RivalKind.Young, Seed = 20261114, RivalFood = 0.4f,
            Blurb = "Ashling's body glows. Feed it to the queen, raise the royal larva on remains, guard the cocoon that closes over old Burr, and blood the new Warlord on a red queen.",
            Stars = new[] { (StarGoal.Time, 1260f, Under(21)), (StarGoal.LarvaSafe, 0f, "No raider ever wounds the larva") } },
        new() { Id = 12, Season = 3, Name = "The Ember Empress", Novelty = "The finale: every tool against an awake Empress", Rival = RivalKind.Empress, Seed = 20261112,
            Blurb = "Both mounds fortified, the Old Stag awake and an Empress who raids. End her.",
            Stars = new[] { (StarGoal.Time, 1500f, Under(25)), (StarGoal.QueenHp, 0.5f, "Your queen never drops below 50%") } },
    };

    // ------------------------------------------------------------------ the story
    // One colony's journey in four acts. The Glass Queen was carried in a collector's tube (a lab strain: "Lasius niger,
    // strain M-7, do not release") until the researcher's batch fell from his backpack and broke on a woodland path.
    // The strain can take up mutagen without limit, and the creatures that fed on the spilled batch carry it. Burr is the tube's eldest soldier (later the colony's Warlord); Vetch
    // its Acid Matron, lost in the fall. The red fire ants are Queen Cinder's line: her daughter Ashling, then
    // Cinder herself, then her mother, the Ember Empress. Comic beats between levels: src/UI/Comic.cs.

    public static string[] Intro(int id) => id switch
    {
        1 => new[]
        {
            "The Giant never noticed. His batch of lab tubes slipped from the backpack, struck the stones by the path and burst. One label read: Lasius niger, strain M-7. Do not release.",
            "Out of the spilled sand climbed the Glass Queen, old Burr and his soldiers, and a handful of workers, with nothing left to eat. A ground beetle blocks the grass alley: bring it down, and its remains will feed the nest. A queen can only found her home where her workers have stocked both food and remains.",
        },
        5 => new[]
        {
            "Warm rain has passed over the woods. Honeydew glistens on the clover, berries have split open and beetles doze in the wet grass.",
            "Burr wants a squad of his own, and the Glass Queen needs a full larder before the next storm. Hatch soldiers, learn what every food gives the colony, and fill the galleries before the sky breaks again.",
        },
        6 => new[]
        {
            "Beetles and spiders nest in a rotting log at the edge of the clearing, and they have smelled the Glass Queen's brood.",
            "The queen is a Matriarch now: her majors can crush what soldiers cannot. Raise them, keep Burr's soldiers close to home and break every raid. The last is led by the log's old wolf spider.",
        },
        7 => new[]
        {
            "They came the morning after the log fell quiet: red ants, hundreds of them, walking the black colony's own trails as if they had always owned them.",
            "And a scout came home running with other news: Vetch is alive. The tube's old Acid Matron was thrown clear when the glass broke and has hidden in bug country ever since, wounded. Her road home crosses red ground. Bring her home, and break the raid the red ants send after her.",
        },
        13 => new[]
        {
            "The batch did not break empty. Something spilled with the ants of the tube, and the creatures that fed on it carry it still: it glows faintly, and it pulses.",
            "Tonight every ant of the colony feels that pulse. It comes from a queen wasp hanging dead in a spider's web. Find her, deal with the spider however you can, carve out the glowing gland and haul it home. The gland is heavy, and hunters smell it on the move.",
        },
        14 => new[]
        {
            "Ashling's body glowed when the workers dragged it home. The Glass Queen has felt it all night.",
            "Feed it to her and she will lay something no ant has laid: a royal larva. Dig its nursery, keep the remains coming and guard it, for its glow carries through the soil and every hunter in the woods will come for it. When it spins its cocoon, old Burr will lie down beside it and the silk will close over them both. When the cocoon breaks, blood the Warlord on the red scouts' queen.",
        },
        2 => new[]
        {
            "The red raiders came from a young mound across the clearing. Their queen, Ashling, is a daughter of the great Queen Cinder, and her foragers already strip your trails.",
            "Dig a store, win the aphid herd from the beetle that grazes beside it and drive off the lair bugs. Then march down Ashling's shaft before her colony grows. For the first time, the colony goes for a queen.",
        },
        3 => new[]
        {
            "Queen Cinder has come for her daughter's killers. Her red mound rises across the clearing, her foragers strip your trails and her guards hold the middle ground.",
            "The Glass Queen's chambers can do more now: royal jelly, a Mandible Forge, a Chitin Armory. Send a scout, hold the nest when Cinder answers, grow stronger and break her outpost. Then go down her shaft and end her.",
        },
        10 => new[]
        {
            "Something with wings has found you. From a paper nest in the woods the Hornet Mother sends her daughters at every colony she can smell, and yours is the closest.",
            "In the air her daughters are out of reach: no ground ant can bite a wasp until it comes down to sting, and her paper nest hums with them. Before her raids come, find the answer: in the far corner lives a silk spitter, a spider whose body holds a fragment of mutagen: with it, the colony could learn to pull wasps out of the sky. Then send Burr at the paper nest. With the Hornet Mother gone, the red colony stands alone.",
        },
        8 => new[]
        {
            "No rain for weeks. The seed caches are gone and the berries have dried to husks. The only feasts left in the woods are the bugs themselves.",
            "Hunt the lairs: every beetle, spider and wasp Burr's army brings down is a meal for the workers to haul home. The red colony is starving too. Outlast it, then end its queen.",
        },
        9 => new[]
        {
            "The Ember Empress has sent a cautious daughter to the clearing. Three watch posts of rooted guards stand around her mound, and while any of them stands she cannot be harmed.",
            "The Glass Queen is an Empress herself now, and her colony can grow wings. Flying ants cross the clearing fast and acid gunners burn guards from range: burn the posts, then go down the shaft.",
        },
        4 => new[]
        {
            "The evening air is warm and still. Tonight the Ember Empress's winged princesses take their nuptial flight: if they fly, red queens will seed every corner of the woods.",
            "Raise a general, wake the Old Stag that sleeps by the great raspberry and turn its fury on the red mound. Break the guardians that shield its queen and end her line before the flight.",
        },
        11 => new[]
        {
            "Autumn is coming. The Ember Empress is sealing her outer mound for the winter: in eight minutes no army will get in.",
            "Burr leads one strike army and there is no time to grow another. March, break the gate guards and kill the mound's queen before the seal.",
        },
        12 => new[]
        {
            "The Ember Empress herself is awake, fortified and hungry, and her raids will come whether you are ready or not.",
            "Everything the colony has become since the glass broke is yours: Burr and the generals, wings and stings, acid and the Old Stag sleeping by the raspberry. End her, and the woods are yours.",
        },
        _ => System.Array.Empty<string>(),
    };

    public static string Outro(int id) => id switch
    {
        1 => "The beetle's shell lines the first store, and the first brood is hatching. At dusk Burr finds a scent trail crossing the alley, sharp as smoke. No ant of the tube has smelled it before.",
        5 => "The storm breaks over a full larder. In the wet grass Burr's new squad finds a dead ant none of them knows: red, with a sting in its tail.",
        6 => "The wolf spider is dead and the log is quiet. On its far end, red ants stand in a silent line, watching the black colony count its dead. Then they turn and walk away, toward your trails.",
        7 => "Vetch sleeps in the royal chamber beside the Glass Queen, healed, and the red raid lies broken in the grass. But the raiders' young queen is still alive across the clearing.",
        13 => "The Glass Queen ate the gland, and by dawn the winged daughters had grown stings. Far off, other things that fed on the spill have started to stir.",
        14 => "Old Burr broke out of the cocoon a Warlord, into a nursery full of light, and by nightfall the red scouts' queen was dead. Somewhere in the woods, Queen Cinder has heard of the black ants from the glass.",
        2 => "Ashling is dead, and her body glows faintly as the workers carry it home. The Glass Queen will not stop looking at it.",
        3 => "Queen Cinder is dead. Among her guards your soldiers find a mark they do not know: the sign of the Ember Empress, Cinder's mother.",
        10 => "The paper nest is torn open and the Hornet Mother is dead. Burr wears her sting scar with pride.",
        8 => "The red queen starved and fell. When the rain came back, it found only the Glass Queen's colony to feed.",
        9 => "The watch posts are ash and another red queen is dead. Your winged ants have seen the Empress's great mound beyond the woods.",
        4 => "The princesses never flew. Only the Ember Empress's own mounds are left in the woods.",
        11 => "Burr's army reached the mound before the seal. Its queen is dead, and the Empress's chamber lies beneath the last hill.",
        12 => "The Ember Empress is dead. In the deepest chamber the Glass Queen lays her eggs beside a shard of the tube that brought her here.",
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
        new() { Stars = 9, Name = "Acid Matron", Kind = "General", Text = "Raise the Acid Matron from a royal larva from Level 9 on.", Perk = Perk.Matron },
        new() { Stars = 12, Name = "Raspberry emblem", Kind = "Emblem", Text = "The great raspberry, for the colony that took it.", Emblem = 2 },
        new() { Stars = 15, Name = "Early acid gunners", Kind = "Caste", Text = "Acid gunners (and the Empress research they need) from Level 9 on.", Perk = Perk.Gunner },
        new() { Stars = 18, Name = "Saffron colony colour", Kind = "Colour", Text = "Crests and banner in warm saffron.", Colour = 2 },
        new() { Stars = 21, Name = "Sky Marshal", Kind = "General", Text = "Raise the Sky Marshal from a royal larva from Level 9 on.", Perk = Perk.Marshal },
        new() { Stars = 24, Name = "Early flying ants", Kind = "Caste", Text = "Hatch flying ants (and research wings) from Level 9 on.", Perk = Perk.Flyer },
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
    public enum Obj { Dig, Gather, Taste, Hatch, Research, Hero, Reach, Escort, Group, Waves, KillQueen, Raise, Haul, Evolve, Sense, Extract, Deliver, Mutate, Larva, Cocoon }

    public sealed class Objective
    {
        public Obj Kind;
        public int A, B;             // Dig: chamber kind, count · Hatch: ant type, count · Raise: soldiers hatched in this level · Research: upgrade, tier · Group: group · Waves: waves · Taste: food kind
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
    public int WaveStep = -1;     // the raids begin when the player reaches this objective (-1: at WaveAt)
    public MutagenSource Gland;   // the chapter's mutagen source (the Silk Trap)
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
    public float OrganicBase;   // organic matter hauled before the level began (remains go to the reserve, not the larder)
    public float QueenLow = 1;
    public float EndTime = -1;
    public int EndLost;
    public bool EndEscort, EndStag, EndLarders, EndMaster;

    public string Stake(float time)
    {
        string s = Info.Rival == RivalKind.None || !EndsWithQueen ? "Lose if your queen dies"
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

    /// Won by killing the red queen (any time); otherwise by the level's own last objective.
    public bool EndsWithQueen => IsLevel ? Objectives.Count > 0 && Objectives[^1].Kind == Obj.KillQueen : Episode != 1;

    string LevelGoal => Episode == 7 ? "Bring Vetch home and break the red raid."
        : Episode == Reveal.SilkTrapId ? "Bring the queen wasp's glowing gland home and mutate the colony."
        : Episode == Reveal.RoyalLarvaId ? "Raise the royal larva into the colony's first general, then kill the red queen."
        : Info.Rival switch
    {
        RivalKind.None => Episode == 5 ? "Fill the galleries before the storm." : "Break every raid from the hollow log.",
        RivalKind.Empress => "Kill the Ember Empress in her royal chamber.",
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

    /// Sets up one of levels 5-14 on the open yard (called by BeginEpisode).
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
                    // drowsy: they hold their ground until the colony comes for them, so Chapter 2 stays a breather
                    foreach (var (bug, _) in m.Groups[g]) Ants[bug].State = AntState.Hold;
                }
                m.Deadline = 900; m.DeadlineName = "the storm breaks"; m.DeadlineLoss = "The storm flooded the yard before the galleries were full. The colony starved through the wet weeks.";
                Add(Expedition.Obj.Raise, "Raise a squad", "Soldiers can hatch now. Burr wants a squad of his own: press X (or the Soldier card) and the incubator hatches six. Hatched: {0}/{1}.", b: Config.SquadSize(AntType.Soldier));
                Add(Expedition.Obj.Dig, "Dig a second store", "Open the Chambers tab (N), choose Storage and click beside a connected room. Every store holds 400 more food.", a: (int)ChamberKind.Storage, b: 2);
                Add(Expedition.Obj.Taste, "Taste the honeydew", "Send workers to a honeydew drop (right-click it with workers selected). While honeydew keeps arriving, your workers run 15% faster.", a: (int)FoodKind.Sugar);
                Add(Expedition.Obj.Taste, "Bring home protein", "Send Burr's soldiers at the drowsy beetles in the wet grass (F2, then right-click them). Workers haul the remains home: they feed the larder and the organic reserve, and the protein makes the queen lay 30% faster.", a: (int)FoodKind.Corpse);
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
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Beetle, 1, 2), (AntType.Spider, 1, 2) } });
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Spider, 1, 4) } });
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Spider, 2, 3), (AntType.Beetle, 2, 2) } });
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Beetle, 2, 2), (AntType.Spider, 2, 3) } });
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Spider, 5, 1), (AntType.Spider, 2, 3) }, Boss = 0 });
                m.WaveAt = 75; m.WaveGap = 80;
                Add(Expedition.Obj.Hatch, "Raise majors", "Majors can hatch now (C): the Matriarch's giant workers, slow and costly, with jaws that crush beetle shell. Raise three more before the raids grow. Majors: {0}/{1}.", a: (int)AntType.Major, b: 6);
                Add(Expedition.Obj.Waves, "Hold the nest", "Raiders dive straight down your shaft for the queen. Keep your army by the mound and attack-move onto them. Spiders spit silk: keep moving, and kill them before they pounce. Raids beaten: {0}/{1}.", b: 4);
                Add(Expedition.Obj.Waves, "Slay the old wolf spider", "The log's old wolf spider leads the last raid: she roots ants in silk and pounces on whatever she has caught. Surround her with majors. Raiders left: {0}.", b: 5);
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
                    // red foragers already walk her road home: the first red ants the colony fights
                    Muster(1, AntType.Soldier, 4, home.Lerp(at, 0.35f) - side * 5);
                    // two ambushes on her road home
                    var mid = home.Lerp(at, 0.55f) + side * 6;
                    int g1 = AddGroup("Spider ambush", mid);
                    for (int i = 0; i < 3; i++) GroupBug(g1, AntType.Spider, mid + MathX.Dir(i * 2.1f) * 3, 1);
                    var near = home.Lerp(at, 0.8f) - side * 4;
                    int g2 = AddGroup("Beetle ambush", near);
                    for (int i = 0; i < 2; i++) GroupBug(g2, AntType.Beetle, near + side * (i * 3 - 1.5f), 2);
                }
                m.WaveFrom = foe + (home - foe).Normalized() * 12;
                m.Waves.Add(new Expedition.Wave { Team = 1, Bugs = new[] { (AntType.Soldier, 1, 10) } });
                m.WaveAt = 330; m.WaveGap = 0;
                Add(Expedition.Obj.Reach, "Find Vetch", "She waits on the far flank of the yard, wounded. Red foragers walk the road to her: these are fire ants, and their soldiers sting. Send a squad (F2, right-click near her). Distance: {0}.");
                Add(Expedition.Obj.Escort, "Bring her home", "She walks home whenever no enemy is near her. Clear the road ahead and keep her alive. Distance to the mound: {0}.");
                Add(Expedition.Obj.Waves, "Break the red raid", "The red colony has sent a raid after her, straight for your shaft. Bring the army home and break it. Vetch's acid burst melts packed attackers. Raiders left: {0}.", b: 1);
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
                Add(Expedition.Obj.Haul, "Haul the feast home", "Workers carry the remains home on their own: they feed the larder as well as the reserve. Hatch more (Z) to haul faster, and hunt the next lair. Organic matter: {0}/{1}.", amount: 80);
                Add(Expedition.Obj.Evolve, "Feed a fighter", "Remains do more than fill the stores: a fighter that eats them evolves. Bring a squad to the nest and feed it from the reserve (select it, then Feed from the reserve), or right-click remains with it. Evolved fighters: {0}/{1}.", b: 1);
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
                { int hero = Spawn(0, AntType.Warlord, OpenGround(home + toFoe * 12), toFoe.AngleD()); if (hero >= 0) Ants[hero].State = AntState.Idle; } // old Burr, out of the royal cocoon (the roster brings the real one)
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
                    m.WaveAt = -1; m.WaveGap = 160; // the raids come once the colony has its answer (WaveStep)
                }
                {
                    // the silk spitter: a rare spitting spider in the free corner of the yard, its hollow strung with its own webs.
                    // Its body holds a fragment of mutagen: the colony's answer to the wasps.
                    var mid = home.Lerp(foe, 0.5f);
                    var corner = OpenGround(ClampMap(mid - side * home.DistanceTo(foe) * 0.42f));
                    ClearAround(corner, 16);
                    ClearCorridor(home, corner, -1);
                    // no wasp lair on the way: the colony meets the wasps' answer before it meets the wasps
                    for (int i = 0; i < High; i++)
                    {
                        ref var b = ref Ants[i];
                        if (!b.Alive || b.Team != Config.Wild || !Config.Predator(b.Type)) continue;
                        float along = Mathf.Clamp((b.Home - home).Dot(corner - home) / (corner - home).LengthSquared(), 0, 1);
                        if (b.Home.DistanceTo(home.Lerp(corner, along)) < 45f) { b.Alive = false; WildCount--; _free.Push(i); }
                    }
                    int sg = AddGroup("Silk spitter", corner);
                    m.FragmentGroup = sg;
                    GroupBug(sg, AntType.Spider, corner, 4);
                    GroupBug(sg, AntType.Spider, corner + side * 4, 1);
                    GroupBug(sg, AntType.Spider, corner - side * 4, 1);
                    foreach (var (bug, _) in m.Groups[sg]) Ants[bug].Home = Ants[bug].Pos;
                    for (int k = 0; k < 4; k++) Webs.Add(new WebZone { Pos = corner + MathX.Dir(k * 1.57f + 0.4f) * 6.5f, R = 2.4f, Life = 1e6f, Max = 1e6f, Root = 1.2f });
                    m.Fixed = Mutation.WebThrowing;
                }
                Add(Expedition.Obj.Group, "Hunt the silk spitter", "The colony feels mutagen in the far corner: a spitting spider whose silk could bring wasps down. Its hollow is strung with webs that pin ants; send the army in force (the gold ring). Spiders left: {0}.", b: 1);
                Add(Expedition.Obj.Extract, "Cut out its spinneret", "Select workers and right-click the glowing spinneret in the spitter's body. Carved: {0}%.");
                Add(Expedition.Obj.Deliver, "Carry it home", "The fragment is heavy and the wasps smell it. Keep soldiers beside the carrier. Distance: {0}.");
                Add(Expedition.Obj.Mutate, "Feed the queen the fragment", "Open the mutation card (beside the objectives): Web Throwing. Soldiers will fling silk that pins a wasp to the ground, where every ant can bite it.", a: (int)Mutation.WebThrowing);
                Add(Expedition.Obj.Waves, "Break the hornet raid", "The Hornet Mother has smelled the change and sends her daughters at your mound. Keep soldiers close: their silk pins a wasp to the ground, and then every ant can bite it. Raiders left: {0}.", b: 1);
                m.WaveStep = 4;
                Add(Expedition.Obj.Group, "Slay the Hornet Mother", "Take the army to the paper nest (gold ring) and kill her. While she lives the raids keep coming. Hornets left: {0}.", b: 0);
                Add(Expedition.Obj.KillQueen, "Kill the red queen", "The yard is quiet. Gather the army, march on the red mound and send everyone down the shaft.");
                break;
            case 11: // The Long March
                SettleColony(0, brood: 3, stores: 2);
                player.Tier[(int)Upgrade.Queen] = 3; player.Tier[(int)Upgrade.Wings] = 1; Col[0].Chambers[0].Tier = 3;
                ApplyLevel(ref Ants[player.QueenId]); Ants[player.QueenId].Hp = Ants[player.QueenId].MaxHp;
                player.Food = 600;
                Muster(0, AntType.Worker, 10, home);
                // a squad and two majors more since the rival heals by eating (Reveal.Evolution): the gate fight was close
                Muster(0, AntType.Soldier, 36, home + toFoe * 9);
                Muster(0, AntType.Major, 8, home + toFoe * 12);
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
            case 13: // The Silk Trap: the first mutagen
            {
                SettleColony(0, brood: 2, stores: 1);
                player.Tier[(int)Upgrade.Queen] = 2; Col[0].Chambers[0].Tier = 2;
                ApplyLevel(ref Ants[player.QueenId]); Ants[player.QueenId].Hp = Ants[player.QueenId].MaxHp;
                player.Food = 350;
                Muster(0, AntType.Worker, 10, home);
                Muster(0, AntType.Soldier, 14, home + toFoe * 9);
                Muster(0, AntType.Major, 2, home + toFoe * 12);
                Muster(0, AntType.Flyer, 6, home + toFoe * 10); // the strain's first winged daughters
                YoungRival(foe, toFoe, 6);
                ClearCorridor(home, foe, -1);
                // the web is strung between grass stems on the far flank; its spider sits at the hub above the wasp
                var web = OpenGround(centre + side * 100 - toFoe * 8);
                ClearAround(web, 18);
                ClearCorridor(home, web, -1);
                int g = AddGroup("The web", web);
                m.MasterGroup = g;
                var away = (web - home).Normalized();
                GroupBug(g, AntType.Spider, web + away * 6.5f, 4);
                GroupBug(g, AntType.Spider, web + away * 9 + side * 5, 1);
                foreach (var (bug, _) in m.Groups[g]) Ants[bug].Home = Ants[bug].Pos; // each keeps to its own strand
                m.Gland = AddMutagen(web, "the queen wasp's gland", AntType.Wasp);
                m.Fixed = Mutation.Stinger;
                // the red scouts smell the mutation too: their raid comes once the colony has changed
                m.WaveFrom = foe + (home - foe).Normalized() * 12;
                m.Waves.Add(new Expedition.Wave { Team = 1, Bugs = new[] { (AntType.Soldier, 1, 9), (AntType.Major, 1, 1) } });
                Add(Expedition.Obj.Sense, "Follow the sixth sense", "Every ant feels it: a faint pulse through the fog, far out on the flank (the violet ring on the map). Send a squad toward it (F2, then right-click near it) until the colony can sense the source. Distance: {0}.");
                Add(Expedition.Obj.Extract, "Carve out the gland", "A dead queen wasp hangs in the web, and the glow is inside her. Fight the web spider, lure it off with soldiers, or slip workers past it: select workers and right-click the glowing gland. Four carve at once. Carved: {0}%.");
                Add(Expedition.Obj.Deliver, "Haul it home", "The gland is heavy: its carrier staggers at half pace, and hunters smell it on the move. Keep soldiers beside it all the way to the nest mouth. Distance: {0}.");
                Add(Expedition.Obj.Mutate, "Feed it to the queen", "The gland is in the queen's reserve. Open the mutation card (the glowing gland beside the objectives) and let her consume it: the first mutation, Venom Stingers.", a: (int)Mutation.Stinger);
                Add(Expedition.Obj.Waves, "Sting the red raid", "The red scouts smelled the change and come for your shaft. Send the winged ants (F4) at them: every sting keeps burning. Raiders left: {0}.", b: 1);
                m.WaveStep = 4;
                break;
            }
            case 14: // The Royal Larva: the hero mutation
            {
                SettleColony(0, brood: 2, stores: 1);
                player.Tier[(int)Upgrade.Queen] = 2; Col[0].Chambers[0].Tier = 2;
                ApplyLevel(ref Ants[player.QueenId]); Ants[player.QueenId].Hp = Ants[player.QueenId].MaxHp;
                player.Food = 400;
                player.Organic = 40;
                Muster(0, AntType.Worker, 14, home);
                Muster(0, AntType.Soldier, 18, home + toFoe * 9);
                Muster(0, AntType.Major, 3, home + toFoe * 12);
                Muster(0, AntType.Flyer, 6, home + toFoe * 10);
                YoungRival(foe, toFoe, 10);
                ClearCorridor(home, foe, -1);
                // two lairs within reach: the larva's larder
                foreach (var (at, bugs) in new[] { (home + toFoe * 46 + side * 34, AntType.Beetle), (home + toFoe * 40 - side * 40, AntType.Spider) })
                {
                    var p = OpenGround(at);
                    ClearAround(p, 12);
                    int g = AddGroup("Lair", p, larder: true);
                    for (int i = 0; i < 4; i++) GroupBug(g, bugs, p + MathX.Dir(i * 1.57f) * 3.5f, i == 0 ? 2 : 1);
                }
                m.Fixed = Mutation.RoyalLarva;
                // the larva's glow draws hunters from the bramble on the flank, straight down the shaft for the nursery
                m.WaveFrom = OpenGround(home + (centre - home).Normalized() * 70 - side * 30);
                ClearAround(m.WaveFrom, 10);
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Spider, 1, 3) } });
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Beetle, 2, 2), (AntType.Spider, 1, 2) } });
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Spider, 2, 3), (AntType.Beetle, 1, 1) } });
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Beetle, 2, 3), (AntType.Spider, 2, 2) } });
                m.Waves.Add(new Expedition.Wave { Team = Config.Wild, Bugs = new[] { (AntType.Spider, 2, 3), (AntType.Beetle, 2, 2) } });
                m.WaveGap = 85; // a breath between raids: the army rebuilds before the next (70 wiped it out again and again)
                m.WaveStep = 2;
                Add(Expedition.Obj.Mutate, "Feed the queen Ashling's mutagen", "The gland from Ashling's body is in the reserve. Open the mutation card (beside the objectives) and let the queen consume it: The Royal Larva.", a: (int)Mutation.RoyalLarva);
                Add(Expedition.Obj.Dig, "Dig the royal nursery", "The queen holds the royal larva until it has a chamber of its own. Workers dig the nursery by themselves: keep a few at home. Dug: {0}/{1}.", a: (int)ChamberKind.Nursery, b: 1);
                Add(Expedition.Obj.Larva, "Feed the royal larva", "Three nurses feed it from the organic reserve. Hunt the lairs (gold rings) and haul the remains home, and keep the army at the mound: its glow brings raiders down the shaft for the nursery. Fed: {0}/{1}.");
                Add(Expedition.Obj.Cocoon, "Guard the cocoon", "The larva has spun its cocoon: it throbs with light, and the raids grow desperate. Hold the nursery until it bursts. Hatching in {0}s.");
                Add(Expedition.Obj.KillQueen, "Blood the Warlord", "Old Burr is reborn: a Warlord who cleaves through crowds. Lead him and the army down the red scouts' shaft and end their queen.");
                break;
            }
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
                Add(Expedition.Obj.Hero, "Raise a general", "No general walks with the colony: open the Generals tab and the queen lays a royal larva in the nursery. Keep organic matter coming until the cocoon breaks. The Empress's raids will come on their own: keep soldiers home.", done: "A general walks with the colony.");
                Add(Expedition.Obj.Group, "Rouse the Old Stag", "Beat the Old Stag by the great raspberry with your whole army and it joins you. Its bite shatters mound guards. Its strength: {0}%.", b: -4);
                Add(Expedition.Obj.Group, "Break the guardians", "While her mound guardians stand the Empress cannot be harmed. Lead the Old Stag at them. Guardians left: {0}.", b: -3);
                Add(Expedition.Obj.KillQueen, "Kill the Ember Empress", "The way is open. Send everything down the red shaft and end her.");
                break;
        }
        foreach (var c in Col) { c.PopUsed = 0; for (int t = 0; t < c.Count.Length; t++) c.PopUsed += c.Count[t] * Config.Stats[t].Pop; }
        for (int k = 0; k < 5; k++) m.KindBase[k] = player.GatheredBy[k];
        m.OrganicBase = player.OrganicGathered;
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
        bool stopped = m.StopGroup >= 0 && Living(m.Groups[m.StopGroup]) == 0
                       || m.Fixed == Mutation.RoyalLarva && m.HeroesBorn > 0; // the larva's glow drew the hunters: once the hero is out, they stop coming
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
            if (m.Step == m.WaveStep && m.WaveAt < 0 && m.WaveNext == 0) m.WaveAt = Time + 25; // the change is smelled
            if (m.Step < m.Steps && m.Objectives[m.Step].Kind == Expedition.Obj.Escort) { m.Reached = true; ref var e = ref Ants[m.EscortId]; e.State = AntState.Idle; }
            if (m.Step > 0 && m.Objectives[m.Step - 1].Kind == Expedition.Obj.Escort && Valid(m.EscortId, m.EscortSerial))
            { ref var e = ref Ants[m.EscortId]; e.Hp = e.MaxHp; e.State = AntState.Idle; } // home: the nurses heal her
            UpdateLevel();
        }
        // a level that does not end with the red queen is won by its last objective
        if (!m.EndsWithQueen && m.Complete && Winner < 0) Winner = Config.Player;
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
            Expedition.Obj.Taste => Tasted(o.A),
            Expedition.Obj.Haul => c.OrganicGathered - m.OrganicBase >= o.Amount,
            Expedition.Obj.Hatch => c.Count[o.A] >= o.B,
            Expedition.Obj.Raise => m.SoldiersHatched >= o.B,
            Expedition.Obj.Evolve => Evolved() >= o.B,
            Expedition.Obj.Research => c.T((Upgrade)o.A) >= o.B,
            Expedition.Obj.Hero => c.Count[(int)AntType.Warlord] + c.Count[(int)AntType.Matron] + c.Count[(int)AntType.Marshal] > 0,
            Expedition.Obj.Reach => m.Have <= (m.EscortId >= 0 ? 10 : 16),
            Expedition.Obj.Escort => m.Have <= 12,
            Expedition.Obj.Group => o.B == -1 ? GroupLeft(-1) < CountLarders() : GroupLeft(o.B) == 0,
            Expedition.Obj.Waves => m.WaveNext >= o.B && Living(m.WaveBugs) == 0,
            Expedition.Obj.KillQueen => !Col[1].Alive,
            Expedition.Obj.Sense => m.Gland != null && Senses(0, m.Gland),
            Expedition.Obj.Extract => m.Gland != null && (m.Gland.Free || m.Gland.Home),
            Expedition.Obj.Deliver => c.MutagenGathered >= 1,
            Expedition.Obj.Mutate => (c.Mutations & o.A) != 0,
            Expedition.Obj.Larva => c.LarvaStage == 3 || m.HeroesBorn > 0,
            Expedition.Obj.Cocoon => m.HeroesBorn > 0,
            _ => false,
        };
    }

    /// Food of this kind carried home since the level began. Remains count once they reach the organic reserve.
    bool Tasted(int kind)
    {
        var m = Mission; var c = Col[0];
        if (kind == (int)FoodKind.Corpse && OrganicRules && c.OrganicGathered > m.OrganicBase) return true;
        return c.GatheredBy[kind] > m.KindBase[kind];
    }

    /// The player's fighters that have evolved at least once by eating.
    int Evolved() { int n = 0; for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Stage > 0) n++; return n; }

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
            case Expedition.Obj.Raise: m.Have = m.SoldiersHatched; m.Need = o.B; break;
            case Expedition.Obj.Evolve: m.Have = Evolved(); m.Need = o.B; break;
            case Expedition.Obj.Haul: m.Have = (int)(c.OrganicGathered - m.OrganicBase); m.Need = (int)o.Amount; m.MarkerPos = GroupMarker(-1, home); break;
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
            case Expedition.Obj.Dig: m.Have = ChamberCount(0, (ChamberKind)o.A, true); m.Need = o.B; if (ChamberOf(0, (ChamberKind)o.A) is { } dig) m.MarkerPos = ChamberPos(0, dig); break;
            case Expedition.Obj.Sense:
            case Expedition.Obj.Extract:
            case Expedition.Obj.Deliver:
                if (m.Gland == null) break;
                m.MarkerPos = m.Gland.Pos;
                if (o.Kind == Expedition.Obj.Extract) m.Have = (int)(100 * Mathf.Clamp(m.Gland.Done / m.Gland.Work, 0, 1));
                else if (o.Kind == Expedition.Obj.Deliver) m.Have = (int)m.Gland.Pos.DistanceTo(home);
                else
                {
                    float near = float.MaxValue;
                    for (int i = 0; i < High; i++) if (Ants[i].Alive && Ants[i].Team == 0 && Ants[i].Layer == 0) near = Mathf.Min(near, Ants[i].Pos.DistanceTo(m.Gland.Pos));
                    m.Have = near == float.MaxValue ? 999 : Mathf.Max(0, (int)(near - Mutations.SenseRange));
                }
                break;
            case Expedition.Obj.Mutate: m.MarkerPos = home; break;
            case Expedition.Obj.Larva: m.Have = (int)c.LarvaFed; m.Need = (int)c.LarvaNeed; m.MarkerPos = c.Organic < 4 && GroupLeft(-1) > 0 ? GroupMarker(-1, home) : Nursery(0) is { } nr ? ChamberPos(0, nr) : home; break;
            case Expedition.Obj.Cocoon: m.Have = (int)Mathf.Ceil(c.CocoonT); if (Nursery(0) is { } nc) m.MarkerPos = ChamberPos(0, nc); break;
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
        m.EndMaster = m.MasterGroup >= 0 && m.MastersSlain > 0;
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
                                    && Col[0].GatheredBy[(int)FoodKind.Sugar] > m.KindBase[(int)FoodKind.Sugar] && Tasted((int)FoodKind.Corpse),
                StarGoal.QueenHp => m.QueenLow >= value,
                StarGoal.Escort => m.EndEscort,
                StarGoal.Stag => m.EndStag,
                StarGoal.Lairs => m.EndLarders,
                StarGoal.Master => m.EndMaster,
                StarGoal.LarvaSafe => m.LarvaLow >= 0.99f && m.HeroesBorn > 0,
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
