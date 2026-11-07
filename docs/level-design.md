# Level design: fourteen chapters under the grass

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

The owner's premise holds the levels together. A researcher carries a batch of lab tubes in his backpack; one holds black garden ants of a modified strain (label: *Lasius niger, strain M-7, do not release*): a queen (the **Glass Queen**), her eldest soldier **Burr** and his soldiers, a handful of workers, and the old Acid Matron **Vetch**. The batch falls out on a woodland path and breaks, spilling the mutagen it carried as well, and the queen leads her colony into the woods. Every level is a chapter of their survival.

The rival is the red fire ants' royal line: the young queen **Ashling**, then her mother **Queen Cinder**, then Cinder's mother, the **Ember Empress**. They do not appear as an opponent until Act II. Act I is survival after the tube, and the red ants are only hinted at: a strange scent trail, a dead red ant in the grass, a silent line of red ants watching from the hollow log. Their arrival at the start of Act II is the campaign's first cliffhanger. The rival queen kills are the climaxes, and the first comes only in Chapter 6. The **Hornet Mother** is a recurring menace who comes herself in Act III. The colony's first hero, the Warlord **Hemlock**, breaks out of a royal cocoon in Chapter 7 (old Burr stays the soldier he always was), and Vetch is found again on the Matron's Road. Story text lives in `Levels.Intro`/`Outro`; the story panels before each level are in `src/UI/Comic.cs` and `docs/story-art.md`.

"Ep" marks a re-slotted PR #6 episode. The internal scenario ids stay 1-4 so their code does not move, and new levels use ids 5-12, and the two mutation chapters ids 13 and 14. The order on the map is the table below (`Levels.Order`).

| # | Act | Level | Novelty | Rival | Win | Star goals (2nd, 3rd) |
| --- | --- | --- | --- | --- | --- | --- |
| 1 | I · The Broken Tube | **The First Nest** (Ep 1) | the hunt, remains, founding, workers | - | hunt the beetle, stock the site with seeds and its remains, found the nest and hatch the first workers | under 9:00 · lose no more than 3 ants |
| 2 | I | **Sweet Rain** | soldiers, food effects, storm deadline | - (a dead red ant: the first hint) | fill the galleries before the storm | under 9:00 · carry home all four kinds of food |
| 3 | I | **The Hollow Log** | majors, spiders, raids led by an old wolf spider | - (red ants watch: the cliffhanger) | break every raid | lose no more than 30 ants · queen never below 70% |
| 4 | II · Strange Blood | **The Matron's Road** | the red colony arrives; escort Vetch home | young | bring Vetch home and break the red raid | under 15:00 · Vetch lives to the end |
| 5 | II | **The Silk Trap** (new, id 13) | mutagen, the sixth sense, winged ants, the first mutation (venom stingers) | young (passive scouts) | carve the queen wasp's gland out of the web, haul it home, feed it to the queen, sting the red raid | under 14:00 · slay the web spider |
| 6 | II | **The Foraging Season** (Ep 2) | the aphid herd, a lair raid, the first queen kill; her body glows | Ashling | kill Ashling | under 18:00 · every step before she falls |
| 7 | II | **The Royal Larva** (new, id 14) | the hero mutation: nursery, nurses, the gauge, the cocoon, the first hero (Hemlock); mutation cards | young (Cinder's scouts) | raise the larva into a hero, guard the cocoon, kill the scouts' queen | under 21:00 · no raider ever wounds the larva |
| 8 | II | **Queen Cinder** (Ep 3) | research, scouting, an outpost | Cinder | kill Queen Cinder | under 22:00 · lose no more than 60 ants |
| 9 | III · Hard Times | **The Hornet Mother** | wasps, a boss with her own nest (Hemlock leads) | grown | kill the red queen | under 25:00 · slay the Hornet Mother first |
| 10 | III | **The Dry Spell** | economy puzzle: the lairs are the larder; evolution by eating | grown | kill the red queen | under 26:00 · clear all three lairs first |
| 11 | III | **Wings and Acid** | wings research (hatching flyers), gunners, watch posts shield the queen | grown, shielded | kill the red queen | under 20:00 · lose no more than 50 ants |
| 12 | IV · The Ember Empress | **The Nuptial Flight** (Ep 4) | the Old Stag, guardians, the princesses' flight | shielded | kill the red queen before the flight | under 20:00 · the Old Stag survives |
| 13 | IV | **The Long March** | time pressure: eight minutes, one army | awake | kill the mound's queen before the seal | under 4:00 · lose no more than 60 ants |
| 14 | IV | **The Ember Empress** | the finale: every tool against an awake Empress | awake, shielded | kill the Ember Empress | under 25:00 · queen never below 50% |

The curve: 1 (gentle) → 2 (breather) → 3 (defence) → 4 (the red arrival, escort) → 5 (mutation puzzle, a boss you may avoid) → 6 (first queen kill) → 7 (defence and the first hero) → 8 (peak) → 9 (boss) → 10 (puzzle) → 11 (new castes) → 12 (peak) → 13 (blitz) → 14 (finale). The two mutation chapters sit in Act II around the first queen kill: the Silk Trap is a change of pace after the escort and before Ashling, and the Royal Larva follows her death directly, because her glowing body is the mutagen that pays for the hero. The red colony still arrives at Chapter 4.

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
| 5 | winged ants (given, not yet hatched) | | the web spider (a nest master) | mutagen, the sixth sense, the first mutation (venom stingers) |
| 6 | | | | the queen kill (her body holds mutagen) |
| 7 | the first hero: Hemlock, from the royal larva | the royal nursery | | the royal larva, heroes, mutation cards, colony level, instability |
| 8 | | royal jelly (Matriarch), Mandible Forge and Chitin Armory rank 1 | | research |
| 9 | | | wasps, the Hornet Mother | |
| 10 | | Silk Cutters | | lairs as larders, evolution by eating |
| 11 | hatching flying ants, acid gunners | wings, Empress, Forge and Armory rank 2 | | wings |
| 12 | | every remaining rank | the Old Stag | |

Star perks never break the rule early: the Acid Matron, the Sky Marshal, early gunners and early flyers open no sooner than Act III (Chapter 9).

### The opening chapter: hunt, remains, founding

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

**Evolution by eating** (`Sim.EatRules`) is the Dry Spell's lesson (Chapter 10). After the lairs are hunted and the feast hauled home, its objective is **Feed a fighter**: bring a squad to the nest and feed it from the reserve, or right-click remains with it, until one fighter evolves.

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

### Unlocks (42 stars in all; the last unlock is at 36)

| Stars | Unlock | Kind |
| --- | --- | --- |
| 3 | Clover emblem | cosmetic |
| 6 | Ember colony colour | cosmetic |
| 9 | Acid Matron can be raised from a royal larva from Level 9 on | hero |
| 12 | Raspberry emblem | cosmetic |
| 15 | Acid gunners from Level 9 on (the story opens them at Level 11) | caste |
| 18 | Saffron colony colour | cosmetic |
| 21 | Sky Marshal can be raised from a royal larva from Level 9 on | hero |
| 24 | Hatching flying ants from Level 9 on (the story opens it at Level 11) | caste |
| 27 | Stag Crest emblem | cosmetic |
| 30 | Rosewood colony colour | cosmetic |
| 33 | Veteran brood: every level starts with an extra veteran squad | perk |
| 36 | Crown emblem and the title "Eternal Mother" | cosmetic |

The colony colour tints your ants' crest and trim and your selection, minimap and banner colour. The emblem appears on
the level map, the briefing and the result card. Unlocks affect only your own colony and are fixed when a level
starts, so the sim stays deterministic (they enter `Expedition.Perks` before the first tick).

## 5. Mutation: the second track

The owner's design (2026-11-04). The tube ants are a lab-modified strain, M-7, bred to take up mutagen without limit. The researcher's batch broke with them, and the creatures that fed on the spill carry mutagen in their bodies. That is why this colony can mutate at all, and why the woods grow more dangerous as it does.

Two tracks grow the colony, and they never mix:

- **Evolution** (unchanged): organic matter eaten makes single ants and the queen stronger (`src/Sim/Carrion.cs`).
- **Mutation** (`src/Sim/Mutation.cs`): **mutagen** is found only in special bodies (dead queens, nest masters, rare bosses). It glows faintly and pulses. Workers carve it out (`CmdKind.Extract`, four at a time), carry it home at 45% pace while hunters within 32 units come for the glow, and the queen consumes it from the reserve (`CmdKind.Mutate`) to give the colony things real ants do not have. Mutagen has its own counter and icon on the objective card (`src/UI/MutationHud.cs`) and its own slot in the save.
- **The sixth sense**: every ant within 60 units feels a source through the fog; it shows as a pulsing violet-green ripple (`Sim.Senses`, `src/Render/MutagenGlow.cs`).
- **Colony level**: every queen or nest master the colony defeats raises it (from Ashling on, plus the web spider if slain). Each level adds 4 population room and 3% health to every fighter.
- **Instability**: every mutation adds one. Each point gives wild predators 4% and the rival strain 1.5% more health (harder to kill, not deadlier: with bite as well, the tuned lair fights of later chapters collapsed).

### The mutations

| Mutation | Where it comes from | Effect |
| --- | --- | --- |
| Venom Stingers | the queen wasp in the web (fixed, Chapter 5) | flyers gain a sting: +10% bite, and every strike injects venom (55% of the blow over 4 s) |
| The Royal Larva | Ashling's body (fixed, Chapter 7) | the queen can lay a royal larva in its own nursery: the campaign's only way to a hero |
| Fire Blood | the fire ants' venom (Cinder's line) | soldiers' and majors' bites burn for 3 s |
| Hornet Wings | the Hornet Mother | flyers 30% faster and 25% tougher |
| Iron Chitin | the sealed mound's queen | every fighter +15% health |
| Regrowth | (pool) | ants mend 1% health a second away from a fight |
| Hungry Gut | the Dry Spell's starving queen | fighters evolve on 60% more from every mouthful |
| Acid Blood | the queen behind the watch posts | a fallen fighter leaves a pool of acid |
| Swift Brood | the nuptial flight's mother | the queen lays 20% faster |
| Web Throwing | the silk spitter's spinneret (fixed, Chapter 9; later cards raise its rank) | soldiers fling silk that pins flying enemies to the ground for every ant to bite. Rank 1: one flyer pinned for 3 s within 8 units; rank 2: two, 4.5 s, 10 units; rank 3: three, 6 s, 12 units; a 9 s cooldown |

From the Royal Larva on, every queen kill yields one mutagen, and the result card offers **one of three cards**: the dead queen's own trait first, then two others the colony lacks, in an order seeded by the chapter (a replay offers the same three). The choice goes into the colony that walks into the next chapter (`CampaignProgress.ChooseMutation`); skipping it banks the mutagen for the next card. Mutations, mutagen, level and instability live in the persistent roster (a fifth part of `[colony] enterN`; older saves load as level 1 with no mutations), so a lost chapter retries with exactly the mutations it began with.

### Chapter 5, The Silk Trap

A dead queen wasp hangs in a spider's web on the far flank, and the glow is inside her. The colony has its first winged daughters (6 flyers; hatching more waits for Wings and Acid). The red scouts' mound is across the yard but passive.

1. **Follow the sixth sense** until an ant is within sensing range of the web.
2. **Carve out the gland.** The web spider (level 4, the nest master) sits at the hub 6.5 units above the wasp; a younger spider guards a strand. Fight it, lure it off with soldiers, or slip workers past it (wild hunters only snap at workers that come within 6 units). Four workers carve for 9 s.
3. **Haul it home.** The carrier staggers at 45% pace and hunters on the way come for it; if the carrier falls, the gland lies where it fell for the next worker.
4. **Feed it to the queen** with the mutation card on the objective card. Flyers grow venom stingers at once.
5. **Sting the red raid**: the scouts smelled the change, and ten raiders come for the shaft.

Stars: under 14:00, slay the web spider (which also raises the colony's level).

### Chapter 7, The Royal Larva

Ashling's body is the mutagen. Feeding it to the queen gives the hero mutation, and the chapter is a defence built around one chamber:

1. **Feed the queen Ashling's mutagen** (the card).
2. **Dig the royal nursery**: workers dig it on their own; the queen holds the larva until it is ready.
3. **Feed the royal larva.** Three workers become its nurses and live in the nursery; each spoons 0.55 organic matter a second from the reserve into it, up to 170 (the gauge on the card and, once the art is approved, the living heap in the chamber). Two bug lairs are the larder. Its glow draws five raids from the bramble, and raiders go down the shaft for the nursery, not the queen. Enemies in the nursery eat the larva (1.4% a second each); if it dies before the hero is born, the chapter is lost.
4. **Guard the cocoon**: 100 s of a throbbing, light-filled cocoon while the raids keep coming.
5. **Blood the hero**: Hemlock breaks out (a Warlord), and the chapter ends with the red scouts' queen.

Stars: under 21:00, no raider ever wounds the larva.

From then on, **heroes in the campaign come only this way**: the Heroes tab lays a royal larva (its food cost now, 60 organic matter through the nurses, a 40 s cocoon). Skirmish and Lane War keep their own hero rules. A chapter played without a save behind it gets the story's mutations and a dug nursery, so every chapter stays playable on its own; the Hornet Mother and the Long March muster the hero the story has given the colony.

### Flyers and the ground (the owner's rule)

"A normal ant should not be able to attack a wasp... It shouldn't be able to fight those predators until we have the flying ants."

- A flying predator (a wasp, the Hornet Mother) in the air is out of every ground ant's reach: ground ants cannot target it, are not sent onto it by an attack order (the click is refused with "It's in the air: only flying ants can reach it"), and duck away from one that is hunting them. Acid gunners are ground ants too: they cannot reach it either, which keeps the rule clean (`Config.Predator`, `Sim.Airborne`, `Sim.CanReach`, used by `FindEnemy` and every engage path, so the AI follows the same rule).
- Flying ants reach it. A ground ant reaches it only in the moment a wasp comes down to sting (it hangs at `Config.HoverAlt` above its prey and drops for the bite), or while thrown silk pins it to the ground (`Ant.PinT`).
- Other flyers (the colonies' flying ants, the Sky Marshal) keep the old rule: they swoop low to bite and can be bitten back.

### Chapter 9, the silk spitter (Web Throwing)

The wasps arrive in Chapter 9 (the Hornet Mother). Her paper nest hums with them and the briefing makes the threat plain, but her raids hold off until the colony has earned its answer:

1. **Hunt the silk spitter** in the free corner of the yard: a level-4 spitting spider and two young spiders in a hollow strung with permanent webs that pin ants. The corridor to it is cleared of wasp lairs.
2. **Cut out its spinneret** and **carry it home** (the mutagen haul).
3. **Feed the queen the fragment**: Web Throwing, rank 1.
4. **Break the hornet raid**: now the raids begin, and the soldiers' silk brings the wasps down.
5. **Slay the Hornet Mother**, then **kill the red queen**.

From Chapter 10 on, a chapter played without a save starts with Web Throwing at rank 1. Later mutation cards can raise it to rank 3.

### Start areas

- No wasp lair lies within `Config.WaspFloor` (115) of a starting nest in any mode. Lairs nearer the nests that used to hold wasps hold spiders or beetles instead, so the first prey is never a flyer.
- The campaign already hides wasps before Chapter 9 (the gradual reveal) and keeps level scripts' wasps far from home. `--campaign-test` checks that no wasp starts within 100 of the nest in any chapter.
- The middle prize is no longer the same raspberry in every game. Its kind (a raspberry, a sugar drop or a pile of seeds) and its place along the line of points equally far from both nests come from the map seed, on a sequence of their own, so the rest of every tuned campaign yard is unchanged. Colony War and Skirmish against the AI now use a new seed each game; test runs keep the fixed seed, and multiplayer already shares the host's seed.

### Cinematic slots

`src/UI/StoryVideo.cs` plays `res://assets/video/opening.ogv` before Chapter 1's comic, and `res://assets/video/reward-<slug>.ogv` after every won chapter, before its result card (`Levels.Slug`: the chapter's name in lower case with dashes, for example `reward-the-silk-trap.ogv`, `reward-the-royal-larva.ogv`). A missing file is skipped with no trace; any key, click or tap skips a playing video. Ogg Theora is Godot's video format.

### Art

The visuals of the track are built against targets **H1-H5**, approved by the owner on 2026-11-06 (`docs/art-targets/REVIEW-mutation.jpg`: the queen wasp in the web, the mutagen haul and its pulse, the royal nursery and its living gauge, the womb cocoon, the venom flyer). Until then `src/Render/MutagenGlow.cs` draws placeholders only: a breathing violet-green light where mutagen lies or is carried (on the game's own wasp mesh), the sixth-sense ripple, and the light of the larva and the cocoon in the nursery. The stinger, royal larva and cocoon meshes belong to the ant-rebuild session. The comic beats for the two chapters use H1 and H4.

### Tests

`--campaign-test` plays both chapters with the scripted player (`LevelValidation.Mutations`, or `MANDIBLE_LEVELS=mutation` alone): the gland carved, carried home and fed to the queen, stingers poisoning, a fallen carrier dropping the gland, the larva devoured (a loss, retried from the same saved colony), the larva fed full, the cocoon, Hemlock born and carried, the queen's mutagen and level, the three cards with the trait first, and the chosen mutation and instability arriving in Queen Cinder. The reveal check also covers the story's mutations and the nursery in every chapter.

## 6. How it is built

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
