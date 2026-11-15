using System.Collections.Generic;

namespace Mandible;

/// The campaign shows the game one piece at a time: every caste, hero, research, chamber, wild enemy and system has
/// the chapter (map number, 1-14) that introduces it. Before that chapter it does not exist for the player: the sim
/// refuses it, the HUD and research and hero tabs leave it out, and the Field Guide does not list it. Skirmish and
/// Lane War keep everything. Star perks may open a few castes and heroes early (never before Act III).
public static class Reveal
{
    public const int RedColony = 4;      // the red colony is a real opponent from the start of Act II
    public const int Mutagen = 5;        // the Silk Trap: the sixth sense, the first mutation core (a fly's: winged ants) and the evolve tree
    public const int FirstQueenKill = 6; // Ashling: the first chapter won by killing a queen
    public const int RoyalLarva = 7;     // a mutated queen's core: the royal larva, its nursery and the first general
    public const int PerkFloor = 9;      // star perks open castes and heroes no earlier than Act III
    public const int Organic = 1;        // remains feed the reserve: the founders need food and remains (PR #9's economy)
    /// Eating in the field to mend: the Dry Spell, whose lairs are the larder, teaches it.
    public const int Feasting = 10;
    public const int Armoured = 10;      // the Dry Spell: a beetle's core, armoured ants
    public const int SilkTrapId = 13, RoyalLarvaId = 14; // the two mutation chapters' level ids
    /// The Hornet Mother's chapter: the wasps arrive; the silk spitter's core gives web ants, the answer to them, and
    /// a wasp's core gives the winged ants their sting.
    public const int Web = 9, Stinger = 9;

    public static int Prod(ProdKind k) => k switch
    {
        ProdKind.Worker or ProdKind.Chamber => 1,
        ProdKind.Soldier => 2,
        ProdKind.Major => 3,
        ProdKind.Warlord => 7,           // generals come only from a royal larva (Mutation.RoyalLarva), first in The Royal Larva
        ProdKind.Flyer => Mutagen,       // the mutation units: each from the chapter that holds its core (and only with the core)
        ProdKind.WebAnt => Web,
        ProdKind.Armoured => Armoured,
        ProdKind.Gunner => 11,
        _ => 15,                         // the Acid Matron and Sky Marshal are bought with stars (Levels.Unlocks)
    };

    public static int Chamber(ChamberKind k) => k switch
    {
        ChamberKind.Royal or ChamberKind.Brood or ChamberKind.Storage => 1,
        ChamberKind.Forge or ChamberKind.Armory => 8,
        ChamberKind.Silk => 10,
        ChamberKind.Nursery => 7,        // dug when the queen takes the royal larva mutation
        _ => Mutagen,                    // the wing chamber: dug when the queen takes the fly's core
    };

    public static int Research(Upgrade u, int tier) => u switch
    {
        Upgrade.Queen => tier <= 1 ? 1 : tier == 2 ? 8 : tier == 3 ? 11 : 12,
        Upgrade.Mandibles or Upgrade.Chitin => tier <= 1 ? 8 : tier == 2 ? 11 : 12,
        _ => 10,                         // silk cutters
    };

    /// Wild enemies and the colonies' castes as the player meets them in the field.
    public static int Creature(AntType t) => t switch
    {
        AntType.Worker or AntType.Soldier or AntType.Queen => 1,
        AntType.Beetle => 1,
        AntType.Major or AntType.Spider => 3,
        AntType.Matron => 4,             // Vetch, brought home in The Matron's Road
        AntType.Flyer => 5,              // the first winged ants, out of the fly's core in The Silk Trap
        AntType.WebAnt => Web,
        AntType.Armoured => Armoured,
        AntType.Warlord => 7,            // old Burr, out of the royal cocoon
        AntType.Wasp => 9,               // the Hornet Mother's daughters (the Silk Trap's queen wasp is already dead)
        AntType.Gunner => 11,
        AntType.StagBeetle => 12,
        _ => 15,
    };

    /// What each chapter introduces, for the briefing ("New in this chapter") and the docs.
    public static readonly (int chapter, string name, string text)[] Systems =
    {
        (1, "Hunting", "Burr's soldiers bring down a ground beetle; its remains are the colony's first organic matter."),
        (1, "Founding", "A queen founds her nest only where the colony has stocked both food and remains."),
        (1, "Organic matter", "Remains of other creatures, hauled home into the reserve: the queen and every brood need it."),
        (1, "Glass Earth", "The ground over the nest turns to glass so the chambers show."),
        (1, "Brood and stores", "Incubators hatch the brood; stores hold the harvest."),
        (1, "Workers", "Hatch workers (Z) to forage and dig."),
        (1, "Ground beetles", "The first wild bug: armoured, slow to anger."),
        (2, "Soldiers", "Hatch soldiers (X): the colony's fighting caste."),
        (2, "Food effects", "Honeydew speeds the workers, protein speeds the queen, fruit heals."),
        (3, "Majors", "The Matriarch's giant workers (C): heavy jaws, heavy cost."),
        (3, "Spiders", "Fast, fragile, and their silk roots ants in place."),
        (3, "Raids", "Wild bugs dive down the shaft for the queen."),
        (4, "The red colony", "Fire ants: a rival colony that forages, fights and raids."),
        (4, "Escort", "Vetch, the tube's Acid Matron, walks home when the road is clear."),
        (5, "Mutation cores", "Creatures that fed on the lab's spill carry a glowing core. Workers cut it out and carry it home to the queen: one core, and she can lay a new kind of ant, part ant and part that creature."),
        (5, "The sixth sense", "Strain M-7 feels a core pulse through the fog: a violet-green ripple marks every one within reach."),
        (5, "Winged ants", "The first mutation, from a fly's core: the queen lays winged ants (F) that cross ground no ant can walk."),
        (5, "The evolve tree", "Organic matter is spent in the evolve tree (V): every mutation unit grows its own branch there."),
        (6, "Queen kill", "Go down a rival's shaft and end its queen. Her body is a feast of organic matter."),
        (7, "The royal larva", "A mutated queen's core: the queen lays a royal larva in its own nursery. Nurses feed it organic matter until it spins a cocoon."),
        (7, "Generals", "A general breaks out of the royal cocoon. In the campaign every general is born this way (the Generals tab lays the larva)."),
        (7, "A general's levels", "A general levels up in the evolve tree, paid in organic matter. Every level is a skill point: stronger skills, then new ones."),
        (8, "The queen's levels", "The queen levels up in the evolve tree on organic matter: every ant she lays afterwards hatches stronger, and the nest holds more."),
        (8, "Research", "The Mandible Forge and the Chitin Armory."),
        (9, "Wasps", "Flying predators. In the air no ground ant can reach them, not even an acid gunner: only flying ants, or a ground ant in the moment a wasp comes down to sting. Ground ants duck away from them."),
        (9, "Web ants", "From a spider's core: ants with spinnerets that fling silk and pin a flying enemy to the ground for every ant to bite."),
        (9, "Stinger ants", "From a wasp's core: the winged ants grow a venom sting, and every strike keeps burning."),
        (10, "Lairs as larders", "Every bug the army kills is remains the workers haul home."),
        (10, "Eating in the field", "Fighters and generals mend by eating remains: right-click a carcass with them."),
        (10, "Armoured ants", "From a beetle's core: ants under shell plates, slow and very hard to kill."),
        (10, "Silk Cutters", "A chamber whose research cuts ants free of spider silk."),
        (11, "Acid gunners", "An Empress queen lays acid gunners (D)."),
        (12, "The Old Stag", "The lord of the yard. Beat it and it fights for you."),
    };

    public static IEnumerable<(string name, string text)> NewIn(int chapter)
    {
        foreach (var s in Systems) if (s.chapter == chapter) yield return (s.name, s.text);
    }
}

public sealed partial class Expedition
{
    /// This mission's place on the map (1-14).
    public int Chapter => Levels.NumberOf(Episode);

    /// The organic economy (Sim.OrganicRules) and eating in the field (Sim.EatRules), from the chapters that teach them.
    public bool Organic => Chapter >= Reveal.Organic;
    /// In the campaign remains feed the larder as well as the organic reserve: its levels were tuned on a colony that
    /// eats what it hunts (in the Dry Spell, with every seed dried up, that is all there is).
    public bool RemainsFeed => true;
    public bool Feasting => Chapter >= Reveal.Feasting;

    /// A caste or hero this chapter lets the player hatch: revealed by the story, or opened early by a star perk.
    public bool AllowsProd(ProdKind k) => Reveal.Prod(k) <= Chapter || PerkAllowsProd(k);

    public bool AllowsChamber(ChamberKind k) => Reveal.Chamber(k) <= Chapter;

    public bool AllowsResearch(Upgrade u, int nextTier) => Reveal.Research(u, nextTier) <= Chapter || PerkAllowsResearch(u, nextTier);

    /// Shown on the HUD at all: research whose first rank this chapter reveals (the queen starts at rank 1).
    public bool ShowsResearch(Upgrade u) => AllowsResearch(u, u == Upgrade.Queen ? 2 : 1);

    public bool ShowsAnyResearch => ShowsResearch(Upgrade.Queen) || ShowsResearch(Upgrade.Mandibles) || ShowsResearch(Upgrade.Chitin) || ShowsResearch(Upgrade.SilkCutters);

    public bool ShowsHeroes => AllowsProd(ProdKind.Warlord) || AllowsProd(ProdKind.Matron) || AllowsProd(ProdKind.Marshal);

    /// Star perks: the Acid Matron and Sky Marshal, and early gunners, from Act III on. No perk gives a mutation unit:
    /// those come only from their cores.
    public bool PerkAllowsProd(ProdKind k) => Chapter >= Reveal.PerkFloor && k switch
    {
        ProdKind.Matron => Perks.HasFlag(Perk.Matron),
        ProdKind.Marshal => Perks.HasFlag(Perk.Marshal),
        ProdKind.Gunner => Perks.HasFlag(Perk.Gunner),
        _ => false,
    };

    public bool PerkAllowsResearch(Upgrade u, int tier) =>
        Chapter >= Reveal.PerkFloor && Perks.HasFlag(Perk.Gunner) && u == Upgrade.Queen && tier <= 3;
}

public sealed partial class Sim
{
    /// Wild bugs the player has not met yet leave the yard before the first tick (the campaign only).
    void HideUnrevealedWildlife()
    {
        int chapter = Mission.Chapter;
        for (int i = 0; i < High; i++)
        {
            ref var b = ref Ants[i];
            if (!b.Alive || b.Team != Config.Wild || Reveal.Creature(b.Type) <= chapter) continue;
            b.Alive = false; WildCount--; _free.Push(i);
        }
    }
}
