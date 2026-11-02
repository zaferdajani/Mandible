# Level design: twelve levels under the grass

This is the study and the plan behind Mandible's level system: twelve levels telling one story in four acts, one to
three stars per level from optional goals, and unlocks earned with stars. It sits on top of the campaign
framework from PR #6 (Episodes 1-4, `src/Sim/Campaign.cs`). Those four episodes are re-slotted into the sequence, not
duplicated.

The owner made this game for themself ("I want it to make me have fun"), so the measure of every choice below is
"does it make me want the next level?" Revenue comes second. There are no timers, energy or paywalls anywhere in the level path.

## 1. What already exists (study)

| Piece | Where | What a level can borrow from it |
| --- | --- | --- |
| Colony War | `Sim.cs`, `Nest.cs`, `AIController.cs` | the full economy: foraging on pheromone trails, food kinds with effects, chambers dug by workers, brood that needs the queen below ground, squads that rank up, research tiers |
| Skirmish | `Expedition.BeginSkirmish` | fully developed colonies, the whole roster |
| Lane War | `Lanes.cs`, `docs/moba-plan.md` | mound guardians (rooted towers that mend), the siege rule (`QueenShielded`), the Old Stag boss that is *roused* to your side instead of dying, the aphid herd and its milking bar |
| Tutorial / Episode 1 | `Expedition.cs`, `ExpeditionGuide.cs`, `ExpeditionWorld.cs` | a confined trail, an ambush, escorting the queen, founding the nest, step gates in the sim |
| Episodes 2-4 | `Campaign.cs`, `CampaignProgress.cs` | ordered steps under a permanent **Main goal**, scripted raids (`Ant.Raider` wild bugs that dive into the nest for the queen, or red raids), outposts of guardians, the passive campaign rival AI, a deadline (the nuptial flight), saved progress |
| Heroes | `Config.cs` | Warlord (cleave, War Cry), Acid Matron (splash, Acid Burst pools), Sky Marshal (Dive Bomb rally); level to 10 |
| Wild bugs and lairs | `World.Camps`, `Sim.WildAbility` | beetles (Shell Wall), spiders (webs and lunges), wasps (hit-and-run stings with venom), the stag (charge and toss); levels 1-5 scale health, bite and size |
| Races | `Config.cs` | black garden ants (trails, worker brood, acid) against red fire ants (venom, home fury, faster brood) |
| Food effects | `Config.FoodEffect` | honeydew speeds workers, fruit heals everyone, protein makes the queen lay faster, seeds are heavy but dense |
| Build mode and the nest | `NestInterior.cs`, `BroodLife.cs` | the translucent roof over the nest, seven chamber identities, brood carried by nurses |

The episodes already prove the important things. Steps can be gated deterministically in the sim. Wild bugs can be
turned into raiders, guardians can be planted anywhere, and a rival colony can grow without swamping the player. What
was missing is the *wrapper* that makes people come back. There was no map to look at, no reason to replay a level
they had already won, nothing to earn, and too few levels for the "one more" feeling.

## 2. Research findings

The full notes with every source are in the shared notes folder (`level-research.md`). The points that shaped the plan:

**Why the classics hooked people.** StarCraft II's lead designer Dustin Browder described its missions as small
self-contained games: each twist "could be a mechanic for a whole game" but is spent on a single mission, so players
"don't really know what you're going to get" when one starts ([Shacknews](https://www.shacknews.com/article/59329/first-details-on-starcraft-2s),
[Game Informer](https://gameinformer.com/b/features/archive/2010/04/08/an-extensive-interview-with-starcraft-ii-design-director)).
Warcraft III started life as a heroes-first design, and its RPG-flavoured heroes and its editor are what later grew into
whole genres ([Warcraft Wiki: evolution guide](https://warcraft.wiki.gg/wiki/Warcraft_III_evolution_guide),
[The Frozen Throne](https://en.wikipedia.org/wiki/Warcraft_III:_The_Frozen_Throne)). Outside the RTS genre, Plants vs.
Zombies hands out a new plant after nearly every level and breaks each stage with a mini-game
([Wikipedia](https://en.wikipedia.org/wiki/Plants_vs._Zombies_(video_game))). The common thread is that every level
brings something new, and the reward shows up before the novelty wears off.

**Why the genre thinned.** Developers who left RTS for MOBAs described it as a niche that demands more mental load and
long-term planning than a hero game ([PC Gamer, Ironclad](https://www.pcgamer.com/2013/02/02/ironclad-rts-genre/),
[PC Gamer, decline and future](https://www.pcgamer.com/au/the-decline-evolution-and-future-of-the-rts)). One
retrospective argues the audience splintered into tower defence, MOBA, auto-battlers and survival strategy, with
free-to-play mobile drawing off casual players ([probablydance](https://probablydance.com/2019/09/11/what-happened-to-the-real-time-strategy-genre/)).
The blame on APM is mostly forum opinion ([gamedev.net](https://gamedev.net/forums/topic/665213-why-are-rts-games-becoming-unpopular/)),
but it matches the owner's own experience. The single-player mission-as-puzzle part survives. Long sessions, execution
speed and ladders are what pushed players away.

**What recent games do for retention.**
- *Stars and harder variants.* Kingdom Rush rates every level with up to three stars and unlocks harder challenge
  variants only after a top result ([fan wiki](https://kingdomrushtd.fandom.com/wiki/Heroic_Challenge)). Age of Empires
  II: DE awards medals per scenario and runs short "Art of War" challenges with performance medals
  ([Age of Empires](https://www.ageofempires.com/news/art-of-war-plus-plus/)). Both give a second reason to replay
  without making the main path longer.
- *Same map, a new rule.* Slay the Spire's daily climb gives every player the same seed plus random modifiers, so a
  replay feels new and scores compare skill ([wiki](https://slaythespire.wiki.gg/wiki/Custom_Mode)).
- *Short, fixed sessions.* Clash Royale's battles last three minutes, with the final stretch sped up so it ends on a
  climax ([Supercell dev post via devtrackers](https://devtrackers.gg/clash-royale/p/94fe8728-official-november-update-release-notes-discussion),
  [Deconstructor of Fun](https://www.deconstructoroffun.com/blog//2016/02/clash-royale-next-billion-dollar-game.html)).
- *What not to do.* They Are Billions' long campaign was criticised for missions that add no new constraints, and for
  tech choices that could leave a player stuck several missions later
  ([Vice](https://vice.com/en_us/article/qv7my5/they-are-billions-gets-the-single-player-campaign-nobody-needed),
  [GOG reviews](https://www.gog.com/en/game/they_are_billions)). Players also said they wanted to replay one mission
  without replaying the run.

**Pacing and difficulty.** Baumann, Lürig and Engeser (2016) found that holding skill and demand in constant balance
did *not* maximise flow or enjoyment; pacing curves with breaks did better
([Motivation and Emotion 40](https://www.uni-trier.de/fileadmin/fb1/prof/PSY/PGA/unterlagen/BaumannL%C3%BCrigEngeser_Flow_MOEM_2016.pdf)).
A study of 95,266 players over 168 levels of one mobile game found that players who quit mid-level did so on the hard
levels (Spearman r = -0.586) ([arXiv 2008.12937](https://arxiv.org/pdf/2008.12937)).

**Mobile.** Mobile sessions average around 5 to 10 minutes
([Countly, citing GameAnalytics](https://countly.com/blog/10-game-analytics-metrics-that-separate-successful-studios-from-the-rest)).
In one 275-game analysis, a first session longer than nine minutes went with better day-1 retention
([GameRefinery](https://www.gamerefinery.com/3-things-to-know-about-session-length-restriction-when-designing-a-free2play-game/)).
I found no hard data on landscape preference for RTS. Landscape is the owner's rule and the only sensible frame for a
battlefield.

Caveats: several of these are secondary sources or fan wikis, and the churn study covers one game. None of it is
copied: no level, name, character or text from any of these games appears in Mandible.

## 3. Engagement principles applied

1. **One new idea per level, taught by playing it.** Each level card names its novelty ("NEW · Wasps raid the nest").
   The idea is introduced on a gentle level and remixed on a harder one later.
2. **A sawtooth curve.** Difficulty rises inside each season and drops at the start of the next one, which opens on a
   breather (an economy puzzle or a defence) before the next rival. The season finales are 2, 5, 9 and 12, with smaller
   peaks in between.
3. **Stars are mastery, not grind.** Star 1 is the win. Stars 2 and 3 are optional goals shown on the briefing, live on
   the objective card and on the result screen: a par time, a cap on losses, or a side objective. Each star is kept on
   its own, so a player can chase the time star on one replay and the loss star on the next.
4. **Gate by total stars, never by perfection.** The story path needs only wins. Stars buy extras: cosmetics, heroes,
   early castes and a veteran start.
5. **Something to earn every three stars.** The unlock bar on the map always shows the next reward and how many stars
   it costs, so there is always a goal within reach.
6. **The rule of the queen.** Every level with a rival colony ends by killing her queen in her royal chamber; steps lead
   there and killing her early still wins. Losing your own queen always loses. Levels without a rival end on their own
   objective (a harvest, a defence).
7. **Short, readable levels.** The par times are 9 to 25 minutes, most around 15. The objective card always shows the
   main goal, the current step, the whole list and the stakes, so a player who comes back after a pause knows at once
   what to do.
8. **One story, shown not told.** One colony's journey from a broken collector's tube to the Empress's chamber, with recurring characters. A cinematic panel opens each level and can be skipped with one press; two or three lines brief it and one closes it.
9. **Replay anything.** Every unlocked level can be replayed at any time. Continue opens the first unfinished one.
10. **A map you want to finish.** The level map is the colony's own nest, dug deeper with every win. Unearned stars
    show as empty sockets, and the chambers beyond the next level are still undug soil.
11. **No fail-forward traps.** Choices in a level never carry into the next, so no early choice can make a later level
    unwinnable (the They Are Billions lesson).
12. **Landscape on every device, warm sand everywhere.** The map, briefings and result cards are composed for
    landscape and use the warm sand-and-amber palette, never grey.

## 4. The level plan: one story in four acts

The owner's premise holds the levels together. A collector carries a glass tube of black garden ants in his backpack: a queen (the **Glass Queen**), her eldest soldier **Burr** and his soldiers, a handful of workers, and the old Acid Matron **Vetch**. The tube falls out on a woodland path and breaks, and the queen leads her colony into the woods. Every level is a chapter of their survival.

The rival is the red fire ants' royal line: the young queen **Ashling**, then her mother **Queen Cinder**, then Cinder's mother, the **Ember Empress**. The rival queen kills are the climaxes. The **Hornet Mother** is a recurring menace: her daughter leads the Hollow Log raid before she comes herself. Burr becomes the colony's Warlord, and Vetch is found again on the Matron's Road. Story text lives in `Levels.Intro`/`Outro`; the story panels before each level are in `src/UI/Comic.cs` and `docs/story-art.md`.

"Ep" marks a re-slotted PR #6 episode. The internal scenario ids stay 1-4 so their code does not move, and new levels use ids 5-12. The order on the map is the table below (`Levels.Order`).

| # | Act | Level | Novelty | Rival | Win | Star goals (2nd, 3rd) |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | I · The Broken Tube | **The First Nest** (Ep 1) | the basics, out of the broken tube | - | found the nest | under 9:00 · lose no more than 3 ants |
| 2 | I | **Sweet Rain** | food effects, storm deadline | - | fill the galleries before the storm | under 9:00 · carry home all four kinds of food |
| 3 | I | **The Foraging Season** (Ep 2) | aphid herd, lair raid, first queen kill | Ashling | kill Ashling | under 18:00 · every step before she falls |
| 4 | II · Red Neighbours | **The Hollow Log** | defence: wasps, spiders, a hornet captain | - | break every raid | lose no more than 30 ants · queen never below 70% |
| 5 | II | **Queen Cinder** (Ep 3) | scouting, research, outpost | Cinder | kill Queen Cinder | under 22:00 · lose no more than 60 ants |
| 6 | II | **The Matron's Road** | escort Vetch home | young | kill the red queen | under 20:00 · Vetch lives to the end |
| 7 | III · Hard Times | **The Hornet Mother** | a boss with her own nest; Burr becomes a Warlord | grown | kill the red queen | under 25:00 · slay the Hornet Mother first |
| 8 | III | **The Dry Spell** | economy puzzle: the lairs are the larder | grown | kill the red queen | under 26:00 · clear all three lairs first |
| 9 | III | **Wings and Acid** | flyers, gunners, watch posts shield the queen | grown, shielded | kill the red queen | under 20:00 · lose no more than 50 ants |
| 10 | IV · The Ember Empress | **The Nuptial Flight** (Ep 4) | heroes, the Old Stag, guardians, the princesses' flight | shielded | kill the red queen before the flight | under 20:00 · the Old Stag survives |
| 11 | IV | **The Long March** | time pressure: eight minutes, one army | awake | kill the mound's queen before the seal | under 4:00 · lose no more than 60 ants |
| 12 | IV | **The Ember Empress** | the finale: every tool against an awake Empress | awake, shielded | kill the Ember Empress | under 25:00 · queen never below 50% |

The curve: 1 (gentle) → 2 (breather) → 3 (first peak) → 4 (defence) → 5 (peak) → 6 (escort) → 7 (boss) → 8 (puzzle) → 9 (new castes) → 10 (peak) → 11 (blitz) → 12 (finale).

### The colony map

The level map is a cut through the Glass Queen's nest. Level 1 is the first chamber below the entrance, and each act is a stratum of earth, darker and deeper than the last. The Ember Empress is the deepest, widest chamber. Ants walk the dug galleries. Locked chambers are still undug soil, and won chambers keep a keepsake of their level (a shard of the tube, a honeydew drop, a captured red banner) along with their stars. The palette is warm earth and amber throughout, with no grey.

### Unlocks (36 stars in all)

| Stars | Unlock | Kind |
| --- | --- | --- |
| 3 | Clover emblem | cosmetic |
| 6 | Ember colony colour | cosmetic |
| 9 | Acid Matron can be raised in any level that opens heroes | hero |
| 12 | Raspberry emblem | cosmetic |
| 15 | Acid gunners from Level 5 on (the story opens them at Level 8) | caste |
| 18 | Saffron colony colour | cosmetic |
| 21 | Sky Marshal can be raised in any level that opens heroes | hero |
| 24 | Flying ants from Level 5 on | caste |
| 27 | Stag Crest emblem | cosmetic |
| 30 | Rosewood colony colour | cosmetic |
| 33 | Veteran brood: every level starts with an extra veteran squad | perk |
| 36 | Crown emblem and the title "Eternal Mother" | cosmetic |

The colony colour tints your ants' crest and trim and your selection, minimap and banner colour. The emblem appears on
the level map, the briefing and the result card. Unlocks affect only your own colony and are fixed when a level
starts, so the sim stays deterministic (they enter `Expedition.Perks` before the first tick).

## 5. How it is built

- `src/Sim/Levels.cs`: the level table (order, season, novelty, rival, star goals), the generic objectives the new
  levels use (build, gather, deliver kinds, waves, destroy a group, escort, raise a hero, a deadline) and their
  setup on the yard. Episodes 1-4 keep their own code in `Expedition.cs` and `Campaign.cs`.
- `src/Game/CampaignProgress.cs`: per level the win, the stars (kept one by one) and the best time; the chosen colour
  and emblem; old `episodeN` keys migrate.
- `src/UI/LevelMap.cs`: the colony map (nest cross-section) and the side card with the selected level.
- `src/UI/Comic.cs`: the story panels and their viewer; art in `assets/ui/comic`, provenance in `docs/story-art.md`.
- `src/World/ExpeditionWorld.cs`: level 1's grass alley, dressed naturally, with the broken tube and its spilled sand at the start.
- `src/UI/Emblem.cs`: the procedural emblems.
- The briefing lists the star goals, the objective card tracks them live, and the result card awards the stars and
  names the next unlock.
- `--campaign-test` plays every level with a scripted player and checks the win, the stars, the unlocks and
  save/load.
