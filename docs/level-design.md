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

The rival is the red fire ants' royal line: the young queen **Ashling**, then her mother **Queen Cinder**, then Cinder's mother, the **Ember Empress**. They do not appear as an opponent until Act II. Act I is survival after the tube, and the red ants are only hinted at: a strange scent trail, a dead red ant in the grass, a silent line of red ants watching from the hollow log. Their arrival at the start of Act II is the campaign's first cliffhanger. The rival queen kills are the climaxes, and the first comes only in Chapter 5. The **Hornet Mother** is a recurring menace who comes herself in Act III. Burr becomes the colony's first hero, its Warlord, in the middle of the campaign, and Vetch is found again on the Matron's Road. Story text lives in `Levels.Intro`/`Outro`; the story panels before each level are in `src/UI/Comic.cs` and `docs/story-art.md`.

"Ep" marks a re-slotted PR #6 episode. The internal scenario ids stay 1-4 so their code does not move, and new levels use ids 5-12. The order on the map is the table below (`Levels.Order`).

| # | Act | Level | Novelty | Rival | Win | Star goals (2nd, 3rd) |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | I · The Broken Tube | **The First Nest** (Ep 1) | the hunt, remains, founding, workers | - | hunt the beetle, stock the site with seeds and its remains, found the nest and hatch the first workers | under 9:00 · lose no more than 3 ants |
| 2 | I | **Sweet Rain** | soldiers, food effects, storm deadline | - (a dead red ant: the first hint) | fill the galleries before the storm | under 9:00 · carry home all four kinds of food |
| 3 | I | **The Hollow Log** | majors, spiders, raids led by an old wolf spider | - (red ants watch: the cliffhanger) | break every raid | lose no more than 30 ants · queen never below 70% |
| 4 | II · Red Neighbours | **The Matron's Road** | the red colony arrives; escort Vetch home | young | bring Vetch home and break the red raid | under 15:00 · Vetch lives to the end |
| 5 | II | **The Foraging Season** (Ep 2) | the aphid herd, a lair raid, the first queen kill | Ashling | kill Ashling | under 18:00 · every step before she falls |
| 6 | II | **Queen Cinder** (Ep 3) | research, scouting, an outpost | Cinder | kill Queen Cinder | under 22:00 · lose no more than 60 ants |
| 7 | III · Hard Times | **The Hornet Mother** | heroes (Burr becomes the Warlord), wasps, a boss with her own nest | grown | kill the red queen | under 25:00 · slay the Hornet Mother first |
| 8 | III | **The Dry Spell** | economy puzzle: the lairs are the larder | grown | kill the red queen | under 26:00 · clear all three lairs first |
| 9 | III | **Wings and Acid** | flyers, gunners, watch posts shield the queen | grown, shielded | kill the red queen | under 20:00 · lose no more than 50 ants |
| 10 | IV · The Ember Empress | **The Nuptial Flight** (Ep 4) | the Old Stag, guardians, the princesses' flight | shielded | kill the red queen before the flight | under 20:00 · the Old Stag survives |
| 11 | IV | **The Long March** | time pressure: eight minutes, one army | awake | kill the mound's queen before the seal | under 4:00 · lose no more than 60 ants |
| 12 | IV | **The Ember Empress** | the finale: every tool against an awake Empress | awake, shielded | kill the Ember Empress | under 25:00 · queen never below 50% |

The curve: 1 (gentle) → 2 (breather) → 3 (defence) → 4 (the red arrival, escort) → 5 (first queen kill) → 6 (peak) → 7 (boss, first hero) → 8 (puzzle) → 9 (new castes) → 10 (peak) → 11 (blitz) → 12 (finale).

### Build the game up gradually

The owner's rule: *build the game experience gradually without exposing all units and skills and enemies.* Every caste, hero, research rank, chamber, wild enemy and system has the one chapter that introduces it (`Reveal` in `src/Sim/CampaignReveal.cs`). Before that chapter it does not exist for the player:

- the sim refuses it (hatching, digging, research);
- the HUD leaves it out. Locked castes, chambers and research cards are not shown greyed: they are absent, and the Research and Heroes tabs appear only once something in them is revealed;
- the field manual (F1) names only what has been introduced;
- the yard holds no wild bug the player has not met yet;
- the Field Guide lists only the castes and bugs the player has met (by the save's furthest chapter, or everything after any Skirmish or Lane War game), and shows the red fire ants' colony card only from Act II.

Each chapter's briefing lists what it introduces, and that chapter's objectives teach it with the guidance arrows. Skirmish and Lane War keep everything available.

| Chapter | Castes and heroes | Chambers and research | Enemies | Systems |
| --- | --- | --- | --- | --- |
| 1 | workers | royal chamber, incubator, store | ground beetle | the hunt, organic matter, founding (food and remains), Glass Earth, brood and stores |
| 2 | soldiers | | | food effects |
| 3 | majors | | spiders | raids down the shaft |
| 4 | Vetch (escort) | | the red colony | escort |
| 5 | | | | the queen kill |
| 6 | | royal jelly (Matriarch), Mandible Forge and Chitin Armory rank 1 | | research |
| 7 | the first hero raised: Burr as Warlord | | wasps, the Hornet Mother | heroes |
| 8 | | Silk Cutters | | lairs as larders, evolution by eating |
| 9 | flying ants, acid gunners | wings, Empress, Forge and Armory rank 2 | | wings |
| 10 | | every remaining rank | the Old Stag | |

Star perks never break the rule early: the Acid Matron, the Sky Marshal, early gunners and early flyers open no sooner than Act III (Chapter 7). ### The opening chapter: hunt, remains, founding

The tube's fall lost everything the colony carried. Chapter 1 opens with no food and no organic matter, and it plays as follows:

1. **The hunt.** Burr's soldiers bring down the ground beetle that blocks the grass alley.
2. **The march.** They lead the Glass Queen to the clearing rich in seeds.
3. **Stocking the site.** A queen can only found her nest where the colony has stocked both **food** (seeds gathered in the clearing) and **remains** (the beetle, dragged in by four workers). The guide arrow stands over the remains, and the objective counts both against `Config.FoundFood` and `Config.FoundOrganic`.
4. **The founding.** The royal chamber consumes the stock, then the colony builds its incubator and store and hatches its first workers.

It runs on PR #9's organic-matter system (`Sim.CanFound`, the organic reserve, hauling remains); there is no second remains system. The tube's look and the alley's ground belong to the art pipeline.

From Chapter 1 the campaign runs the organic economy (`Sim.OrganicRules`):
- other creatures' remains go into the organic reserve;
- the queen ascends on organic matter;
- every brood needs a little of it;
- the queen's trickle feeds only a founded colony.

The campaign's levels were tuned on remains as food, so in the campaign remains also feed the larder. In the Dry Spell, where every seed has dried up, they are the larder.

PR #8's burial, midden and morale (`Sim.CarrionRules`) stay outside the campaign. Its colony still eats its own dead and forages between fights, because when the campaign ran them, workers spent the battles' aftermath at the midden and the late levels starved.

A rival keeps the queen rank its level gives it. The levels are tuned on her, and remains would otherwise let a raiding rival ascend on the player's dead.

**Evolution by eating** (`Sim.EatRules`) is the Dry Spell's lesson (Chapter 8). After the lairs are hunted and the feast hauled home, its objective is **Feed a fighter**: bring a squad to the nest and feed it from the reserve, or right-click remains with it, until one fighter evolves.

From then on, fighters that eat evolve through three stages, and with them come the guide arrow over remains, the Remains order tab and the Field Guide's Feeding page. The colony's save carries each fighter's stage.

The campaign keeps its squads ranking up in battle on top of this. Its levels and the persistent colony's veterans are built on kill ranks, and making eating the only way to grow (skirmish's rule) left Episode 4 and the Long March short of their tuning. For the same reason the campaign's rival does not evolve.

`--campaign-test` checks the whole schedule. In every chapter, nothing is on offer before the chapter that introduces it and everything introduced by then is. The HUD's research cards and tabs follow the same schedule, no unmet wild bug is on the yard, the red colony is absent through Act I, and no chapter before Ashling's is won by a queen kill.

### One persistent colony

The campaign follows one colony from the broken tube to the Empress's chamber (`src/Sim/CampaignColony.cs`).

- **Who survives walks on.** A chapter's survivors are its next chapter's colony. Losses are permanent. The colony is stored as the roster that enters each chapter (`[colony] enterN` in `user://campaign.cfg`). Rank-1 ants are kept as counts by caste. Every ant of rank 2 or higher, and every hero, is kept as a veteran with caste, rank, kills and the chapter it joined.
- **Names you come to care about.** Old Burr is named among the tube's survivors and becomes the Warlord when a Warlord hatches. Vetch is the Acid Matron, and other heroes get names too. A veteran that reaches rank 3 earns a name from the colony's list (Sorrel, Flint, Bramble...). A named ant's death is recorded and shown under **Fallen** on the result card.
- **The nest persists.** Chambers dug in one chapter exist at the start of the next, and the queen keeps her rank (Young Queen, Matriarch, Empress...). The colony map is that nest.
- **Never a dead end.** The level's own muster sets a floor: a carried colony is never weaker than 60% of it, and fresh brood fills the gap. A strong colony carries at most one and a half times the muster plus six, so a careful player keeps a real veteran core while every chapter stays winnable. This avoids the They Are Billions trap of a run that cannot be won any more.
- **Losing costs nothing saved.** Losing your queen loses the chapter, and the retry starts from the colony as it was when the chapter began. Replaying an already-won chapter never rewrites the colony of a later one. Only the save's frontier moves forward.
- **Stars come second.** The result card leads with the colony: ants that walk on, veterans, chambers, the queen's title, named survivors and the fallen. The stars sit below it as bragging rights.
- **Deterministic.** The roster is applied before the first tick, from the save, and the campaign is single-player, so no live lockstep match is ever touched.

**Why it hooks.** Attachment and loss aversion: a named veteran with twelve kills is someone, and losing him hurts, so players play carefully and remember. The underdog-to-ruler arc runs from a dozen ants in a broken tube to an Empress's nest, and you can see it in the roster growing and the queen's title rising. The unfinished-story pull comes from every outro ending on a beat that points to the next chapter. Named characters are why players finish campaigns: they come back for Burr and Vetch, not for a level number.

### The colony map

The level map is a cut through the Glass Queen's nest. Level 1 is the first chamber below the entrance, and each act is a stratum of earth, darker and deeper than the last. The Ember Empress is the deepest, widest chamber. Ants walk the dug galleries. Locked chambers are still undug soil, and won chambers keep a keepsake of their level (a shard of the tube, a honeydew drop, a captured red banner) along with their stars. The palette is warm earth and amber throughout, with no grey.

### Unlocks (36 stars in all)

| Stars | Unlock | Kind |
| --- | --- | --- |
| 3 | Clover emblem | cosmetic |
| 6 | Ember colony colour | cosmetic |
| 9 | Acid Matron can be raised from Level 7 on | hero |
| 12 | Raspberry emblem | cosmetic |
| 15 | Acid gunners from Level 7 on (the story opens them at Level 9) | caste |
| 18 | Saffron colony colour | cosmetic |
| 21 | Sky Marshal can be raised from Level 7 on | hero |
| 24 | Flying ants from Level 7 on | caste |
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
