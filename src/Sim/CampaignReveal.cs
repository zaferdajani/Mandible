using System.Collections.Generic;

namespace Mandible;

/// The campaign shows the game one piece at a time: every caste, hero, research, chamber, wild enemy and system has
/// the chapter (map number, 1-12) that introduces it. Before that chapter it does not exist for the player: the sim
/// refuses it, the HUD and research and hero tabs leave it out, and the Field Guide does not list it. Skirmish and
/// Lane War keep everything. Star perks may open a few castes and heroes early (never before Act III).
public static class Reveal
{
    public const int RedColony = 4;      // the red colony is a real opponent from the start of Act II
    public const int FirstQueenKill = 5; // Ashling: the first chapter won by killing a queen
    public const int PerkFloor = 7;      // star perks open castes and heroes no earlier than Act III
    public const int Organic = 1;        // remains feed the reserve: the founders need food and remains (PR #9's economy)
    /// Evolution by eating: the Dry Spell, whose lairs are the larder, teaches it. The campaign's squads still rank up in
    /// battle (its levels and veterans are built on it); eating evolves them on top, and the rival does not evolve.
    public const int Evolution = 8;

    public static int Prod(ProdKind k) => k switch
    {
        ProdKind.Worker or ProdKind.Chamber => 1,
        ProdKind.Soldier => 2,
        ProdKind.Major => 3,
        ProdKind.Warlord => 7,           // the first hero raised: old Burr, in The Hornet Mother
        ProdKind.Flyer or ProdKind.Gunner => 9,
        _ => 13,                         // the Acid Matron and Sky Marshal are bought with stars (Levels.Unlocks)
    };

    public static int Chamber(ChamberKind k) => k switch
    {
        ChamberKind.Royal or ChamberKind.Brood or ChamberKind.Storage => 1,
        ChamberKind.Forge or ChamberKind.Armory => 6,
        ChamberKind.Silk => 8,
        _ => 9,                          // the wing chamber
    };

    public static int Research(Upgrade u, int tier) => u switch
    {
        Upgrade.Queen => tier <= 1 ? 1 : tier == 2 ? 6 : tier == 3 ? 9 : 10,
        Upgrade.Mandibles or Upgrade.Chitin => tier <= 1 ? 6 : tier == 2 ? 9 : 10,
        Upgrade.SilkCutters => 8,
        _ => 9,                          // wings
    };

    /// Wild enemies and the colonies' castes as the player meets them in the field.
    public static int Creature(AntType t) => t switch
    {
        AntType.Worker or AntType.Soldier or AntType.Queen => 1,
        AntType.Beetle => 1,
        AntType.Major or AntType.Spider => 3,
        AntType.Matron => 4,             // Vetch, brought home in The Matron's Road
        AntType.Warlord or AntType.Wasp => 7,
        AntType.Flyer or AntType.Gunner => 9,
        AntType.StagBeetle => 10,
        _ => 13,
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
        (5, "Queen kill", "Go down a rival's shaft and end its queen."),
        (6, "Research", "Royal jelly, the Mandible Forge and the Chitin Armory."),
        (7, "Generals", "Raise old Burr as the Warlord (Y): a general levels fast and fights with special moves."),
        (7, "Wasps", "Flyers that sting, climb away and come back."),
        (8, "Lairs as larders", "Every bug the army kills is remains the workers haul home."),
        (8, "Evolution by eating", "Fighters that eat remains evolve through three stages: right-click a carcass with them, or feed them from the reserve at the nest."),
        (8, "Silk Cutters", "A chamber whose research cuts ants free of spider silk."),
        (9, "Wings", "Research wings to hatch flying ants (F) and acid gunners (D)."),
        (10, "The Old Stag", "The lord of the yard. Beat it and it fights for you."),
    };

    public static IEnumerable<(string name, string text)> NewIn(int chapter)
    {
        foreach (var s in Systems) if (s.chapter == chapter) yield return (s.name, s.text);
    }
}

public sealed partial class Expedition
{
    /// This mission's place on the map (1-12).
    public int Chapter => Levels.NumberOf(Episode);

    /// The organic economy (Sim.OrganicRules) and evolution by eating (Sim.EatRules), from the chapters that teach them.
    public bool Organic => Chapter >= Reveal.Organic;
    /// In the campaign remains feed the larder as well as the organic reserve: its levels were tuned on a colony that
    /// eats what it hunts (in the Dry Spell, with every seed dried up, that is all there is).
    public bool RemainsFeed => true;
    public bool Evolution => Chapter >= Reveal.Evolution;

    /// A caste or hero this chapter lets the player hatch: revealed by the story, or opened early by a star perk.
    public bool AllowsProd(ProdKind k) => Reveal.Prod(k) <= Chapter || PerkAllowsProd(k);

    public bool AllowsChamber(ChamberKind k) => Reveal.Chamber(k) <= Chapter;

    public bool AllowsResearch(Upgrade u, int nextTier) => Reveal.Research(u, nextTier) <= Chapter || PerkAllowsResearch(u, nextTier);

    /// Shown on the HUD at all: research whose first rank this chapter reveals (the queen starts at rank 1).
    public bool ShowsResearch(Upgrade u) => AllowsResearch(u, u == Upgrade.Queen ? 2 : 1);

    public bool ShowsAnyResearch => ShowsResearch(Upgrade.Queen) || ShowsResearch(Upgrade.Wings) || ShowsResearch(Upgrade.Mandibles) || ShowsResearch(Upgrade.Chitin) || ShowsResearch(Upgrade.SilkCutters);

    public bool ShowsHeroes => AllowsProd(ProdKind.Warlord) || AllowsProd(ProdKind.Matron) || AllowsProd(ProdKind.Marshal);

    /// Star perks: the Acid Matron and Sky Marshal, and early gunners and flyers, from Act III on.
    public bool PerkAllowsProd(ProdKind k) => Chapter >= Reveal.PerkFloor && k switch
    {
        ProdKind.Matron => Perks.HasFlag(Perk.Matron),
        ProdKind.Marshal => Perks.HasFlag(Perk.Marshal),
        ProdKind.Gunner => Perks.HasFlag(Perk.Gunner),
        ProdKind.Flyer => Perks.HasFlag(Perk.Flyer),
        _ => false,
    };

    public bool PerkAllowsResearch(Upgrade u, int tier) =>
        Chapter >= Reveal.PerkFloor && (Perks.HasFlag(Perk.Gunner) && u == Upgrade.Queen && tier <= 3 || Perks.HasFlag(Perk.Flyer) && u == Upgrade.Wings);
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
