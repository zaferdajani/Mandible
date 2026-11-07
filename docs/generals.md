# Generals: levels 1-10, a shared skill pool, the royal insignia

The heroes are called **generals** in player-facing text. Each general levels from 1 to 10 by **eating remains**
(organic matter, never kills alone), and every level is a **skill point**. Which skills a general has is data
(`data/loadouts.json`, see below); ranks unlock Warcraft III style.

| Slot | Key | Ranks open at | Role |
|---|---|---|---|
| Q | Q | the skill's own levels (usually 1, 3, 5) | rank 1 is learnt at hatching |
| W, E | W, E | the skill's own levels (1, 3, 5; four-rank skills 1, 3, 5, 7) | basic skills |
| R | R | 6, 8, 10 | the ultimate, whatever skill sits in the last of four slots |

Four skills of three ranks are 12 ranks for 10 points: a level-10 general masters three and has the fourth at rank
1 (the player chooses which; the computer follows the table's learn priority, ultimate first).

## The skill pool and loadouts (the owner assigns skills as data)

Skills are a **shared pool**, not hard-wired per general:

- **`Skills.Pool`** (src/Sim/Skills.cs) is one table with an entry per skill: id, name, lore, the level each rank opens
  at, aim (self, at an ant, at a spot, passive), range, cooldown per rank (and how much the caster's level shortens
  it), **AI hints** (when to fire: crowd, prize, hurt, rally, target, with counts and radii; which to learn first),
  and every number per rank (`V`). The behaviour code reads only the table.
- **`data/loadouts.json`** says who has which skills, in slot order Q W E R (up to 4). Keys are unit types
  (`Warlord`, `Matron`, `Marshal`, `Beetle`, `StagBeetle`, `Spider`, `Wasp`, ...); `Type@team` gives one colony its
  own (`Warlord@1` = the red colony's). Changing who has what is a data edit, no code. The file ships with exports
  (`include_filter = data/*.json`) and its hash is in the sim checksum, so peers with different files desync loudly.
- Generals learn a skill point per level and the computer spends it by `LearnPriority`. Wild bugs and bosses know
  every rank their level opens and fire through the same code path (`UnitSkills`, the same AI hints). Any skill can
  go on any unit: a general can be given Shell Wall or Venom Sting, a bug boss Queen's Pheromone.
- Pool today (20): see the list below.
- To add a skill: an id, a table entry, its behaviour in `Sim.CastSkill` (or the bite, for a passive), a loadout.
- `--generaltest` gives every skill in turn to a level-10 general and checks it is learnt to its top rank by the AI's
  learn path and then fired by its hints alone at every rank.

## The skills (default loadouts, until the owner assigns)

| General | Q | W | E | R (ultimate) |
|---|---|---|---|---|
| Warlord | War Cry | Cleaving Mandibles (passive) | Shell Wall | Antler Charge |
| Acid Matron | Acid Burst | Venom Sting (passive) | Formic Mist | Queen's Pheromone |
| Sky Marshal | Dive Bomb | Scout Swarm | Rally Scent | Thrall Raid |

Spawn Brood and Living Bridge wait for the future Brood Mother general; Mandible Frenzy, Trail Blaze and Talon Dive
stay in the pool. Every skill's numbers per rank are in `Skills.Pool`; the tooltips and the skill tree read them.

- **War Cry**: allies bite +30/40/50% and run faster; the roar staggers and hits.
- **Queen's Pheromone** (real slave-maker ants' propaganda pheromones): turns 1/2/3/5 enemies for 8-14 s. r1
  soldiers, gunners, flying ants up to level 2; r2 + majors, beetles, spiders up to level 3; r3 + wasps and stag
  beetles up to level 5; r4 any level and enemy generals for half the time (as an ultimate in slot R it stops at r3).
  Workers, queens, bosses and mound guardians never. A turned ant fights for the caster under a faint amber haze,
  keeps its own body and dies as its own colony's.
- **Mandible Frenzy**: bite +35/50/70%, heals 20/30/40% of each bite, immune to silk and stun.
- **Acid Burst**: melts a cluster and leaves a venom pool.
- **Spawn Brood** (Brood Call): callows, r1 3 soldiers, r2 2 majors, r3 4 flying ants, r4 all four types; pale at
  first, they harden a level every 6 s, last 30-45 s, take no brood room and leave no carcass.
- **Formic Mist** (Caustic Cloud): a 5/6/7-wide mist, 14/22/30 acid a second, 30/40/50% slow.
- **Dive Bomb**: crash, stun and rally.
- **Trail Blaze**: allies within 12-15 run 25-55% faster and take 10-25% less damage.
- **Talon Dive**: 150/240/340 to one enemy, pinned 2-3 s; a kill refunds half the cooldown.
- **Shell Wall**, **Mandible Sweep**, **Antler Charge**, **Web Snare**, **Lunge**, **Venom Sting** (passive): the
  wild bugs' specials. Bugs use rank 1 and grow with their level (their old numbers); a general ranks them up.
- **Cleaving Mandibles** (passive): each bite also hits 1/2/3 enemies beside the victim for 45/55/65% of it.
- **Rally Scent**: alarm pheromone on a spot; nestmates within 8/10/12 run 20-40% faster and bite 15-35% harder for
  5-8 s, idle ones come running; rank 3 also heals 10%.
- **Living Bridge**: workers lock into a span of bodies up to 14/18/22 long for 10/14/18 s; the colony's ants on it
  walk over rocks (the map has no impassable gaps yet, so it is a shortcut over obstacles); rank 3 lets them fight
  on it. The computer builds one when its fight or march lies across a rock.
- **Scout Swarm**: 3/4/5 winged scouts circle a spot 10/13/16 wide for 12-20 s: the fog lifts there and enemies in
  it take 10/15/20% more damage; rank 3 picks off lone workers.
- **Thrall Raid** (ultimate): near an enemy nest, carries off 2/3/4 brood that hatch as your workers / soldiers /
  majors; the enemy loses brood in the making and some organic matter.

## Cooldown only (no mana)

Skills cost nothing but time. Ants have no "mana"; an extra bar would crowd a phone screen that already shows
health and evolution, and the economy already has two resources (food and organic matter) that the general's
levels draw on. Cooldowns are shown on the buttons, in seconds, with a dark sweep.

## Controls

- **Q W E R** cast the selected general's skills (Lane War: your hero's). While a general is selected these keys
  belong to it, as on a Warcraft command card; with no general selected (or a three-skill general and R) they keep
  their research meaning. Passive skills show PASSIVE and need no key.
- Aimed skills (at an ant: Pheromone, Acid Burst, Talon Dive, Antler Charge...; at a spot: Formic Mist, Dive Bomb,
  Rally Scent, Living Bridge, Scout Swarm) wait for a click;
  pressing the key again casts at the best target in reach. Esc or right-click cancels.
- **O** (or the Skills button) opens the skill tree. A level-up opens it on its own and the button glows until the
  point is spent.
- **Right-click** a skill button (long press on a phone) toggles **autocast**. Every skill starts on autocast,
  so a casual or mobile player loses nothing; Lane War heroes cast only on command, as before.

## Auto-eat and automatic collection

**Auto-eat** (button in the ability bar for any selected fighters or generals, hotkey **J**) is a standing order per ant,
kept per squad (new recruits take it):
- **ON**: after a kill the killer and its squad mates within 15 eat the remains as soon as the fight is over, and
  evolve (a general levels). The kill is *claimed* for 25 s, so the colony's haulers leave it alone.
- **OFF**: the kill is left whole for the workers to haul home as organic matter for the queen and the brood.
- Default: **generals ON** (eating is their only way to level), **fighters OFF** (carcasses go to the queen unless the
  player decides the army should grow instead). A small drop of hemolymph over an ant shows it will eat.
- An explicit "eat this" order (right-click remains) also claims the remains from your own haulers.

**Collection** (the Remains order "Haul"): the colony notices fresh remains on its own anywhere inside a ring around
the nest, 36 + 0.25 per worker (max 80), and beyond it wherever one of its
ants sees them. Every 2 s it ranks the remains by value and distance and crews the best one still short of carriers (fully crewed
ones do not hold up the queue, and most foragers stay on food), skipping
remains that are claimed or being eaten. The ring shows while the Remains page is open; queued remains get an amber
ring; the first time remains are queued the HUD says "Workers are bringing home a beetle carcass". The computer
colonies use the same rules.

Balance: fire ants' remains bonus on **organic matter** is now +10% (it stays +25% on food from corpses), since much
more remains are hauled home than before.

## Determinism

Learning, casting, autocast toggles and auto-eat are Cmds (`Learn`, `Cast`, `SkillAuto`, `AutoEat`), replayed by multiplayer and saved
games. All target choice is in the sim and is index-stable. Protocol `mandible-mp-15`.

## XP curve

`Config.HeroXp = { 0, 0, 90, 210, 370, 580, 850, 1180, 1580, 2060, 2650 }` at 12.5 XP per organic: about 215 organic
from level 1 to 10 (was about 290). The early levels come in the first fights, so the first skills show up early;
the ultimate ranks are earned. In Lane War the hero still levels by feasting and the herd's honeydew, and kill
bounties pay food.

## The insignia

Under every general lies a royal seal instead of the selection ring (targets I1-I3 in
`docs/art-targets/REVIEW-insignia.jpg`, approved by the owner on 2026-11-07):

- levels 1-3: aged bronze ring of antenna links with six leg spokes;
- levels 4-7: chased gold with laurel and a crown of mandibles;
- levels 8-10: red-gold with amber cabochons that glow faintly.

The seal is lit as real metal with a relief normal from the engraving; the raised metal is solid and the enamel
between it lets the soil show through. It brightens within each tier as the general levels, glows when selected
and flares when a skill is cast. The crown always points up the screen.

## Tests

- `--generaltest` (headless): each general eats to level 10, learns every rank through Learn orders (gates checked),
  and casts every skill at every rank through Cast orders: Pheromone victim count, tier and level caps and expiry;
  Brood Call types per rank, hardening and fading; each other skill's effect.
- `--general-capture=DIR` (windowed, via `godot-run.sh`): insignia at levels 1, 5 and 10, the skill bar and tree,
  Queen's Pheromone, each Brood Call rank, the hardened brood, Caustic Cloud, Talon Dive, auto-eat and collection.
- `--generaltest` also checks auto-eat ON (the squad eats its kill, haulers keep off) and OFF (left whole), and that
  workers fetch a carcass inside the ring that no ant has seen while leaving a claimed one alone.
