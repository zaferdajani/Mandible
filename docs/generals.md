# Generals: levels 1-10, three skills each, the royal insignia

The heroes are called **generals** in player-facing text. Each general levels from 1 to 10 by **eating remains**
(organic matter, never kills alone), and every level is a **skill point**. Each general has three skills; the
ranks unlock Warcraft III style.

| Slot | Key | Ranks | Opens at levels | Role |
|---|---|---|---|---|
| Q | Q | 3 | 1, 3, 5 | the general's old signature special (rank 1 is learnt at hatching) |
| W | W | 4 | 1, 3, 5, 7 | the basic skill the owner specified (Queen's Pheromone, Brood Call) |
| E | E | 3 | 6, 8, 10 | the ultimate |

3 + 4 + 3 = 10 ranks = the 10 points earned from level 1 to 10, so a level-10 general knows everything.
The order in which points are spent is the player's choice.

## The skills

| General | Q | W | E (ultimate) |
|---|---|---|---|
| Warlord | **War Cry**: allies bite +30/40/50% and run faster; the roar staggers and hits | **Queen's Pheromone**: turns 1/2/3/5 enemies for 8/10/12/14 s | **Mandible Frenzy**: bite +35/50/70%, heals 20/30/40% of each bite, immune to silk and stun |
| Acid Matron | **Acid Burst**: melts a cluster and leaves a venom pool | **Brood Call**: summons callows (r1 3 soldiers, r2 2 majors, r3 4 flying ants, r4 all four types) | **Caustic Cloud**: a 5/6/7-wide mist, 14/22/30 acid a second, 30/40/50% slow |
| Sky Marshal | **Dive Bomb**: crash, stun and rally | **Trail Blaze**: allies within 12-15 run 25-55% faster and take 10-25% less damage | **Talon Dive**: 150/240/340 to one enemy, pinned 2-3 s; a kill refunds half the cooldown |

**Queen's Pheromone** is grounded in real slave-maker ants (propaganda pheromones). Rank caps by tier and level:
r1 soldiers, gunners and flying ants up to level 2; r2 adds majors, beetles and spiders up to level 3; r3 adds
wasps and stag beetles up to level 5; r4 has no level cap and takes enemy generals for half the time. Workers,
queens, the Old Stag and mound guardians are never taken. A turned ant fights for the caster and can be ordered by
it; it keeps its own colony's body under a faint amber haze, and it dies as one of its own colony's (counts,
carcass, roster).

**Brood Call** summons are **callows**: pale and soft when they come out, hardening one level every 6 s (up to
level 3/3/4/5) and darkening as they do, the way a real callow's cuticle tans. They last 30-45 s, take no brood
room, learn nothing and leave no carcass.

## Cooldown only (no mana)

Skills cost nothing but time. Ants have no "mana"; an extra bar would crowd a phone screen that already shows
health and evolution, and the economy already has two resources (food and organic matter) that the general's
levels draw on. Cooldowns are shown on the buttons, in seconds, with a dark sweep.

## Controls

- **Q W E** cast the selected general's skills (Lane War: your hero's). While a general is selected these keys
  belong to it, as on a Warcraft command card; with no general selected they keep their research meaning.
- Aimed skills (Pheromone, Acid Burst, Talon Dive at an ant; Caustic Cloud, Dive Bomb at a spot) wait for a click;
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
