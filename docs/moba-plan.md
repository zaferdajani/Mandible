# Mandible MOBA plan: making Lane War the game that replaces Vainglory on mobile

*Research and development plan, 1 November 2026. Written against Mandible v0.6.0 (commit `beac140`). No game code was changed for this document.*

## Executive summary

Vainglory (Super Evil Megacorp, iOS 2014, Android 2015) was the mobile MOBA built for touch rather than ported to it: single-tap precision controls with sub-30 ms response, a compact 3v3 map with one lane and a jungle, 15 to 20 minute matches, a neutral boss that marched down the lane when captured, a four-category item shop, and a "nothing pay-to-win, no ads" economy. It won an Apple Design Award, reached 1.5 million monthly players by mid-2015, ran its own world championships, then collapsed under its own cost base: a 100-plus-person studio, self-funded esports, a cosmetics economy that needed far more payers than it had, and a late, expensive 5v5 pivot to chase Honor of Kings. Its publisher switched the servers off on 27 March 2020; what survives is an unmanaged "Community Edition" on iOS with no accounts, ranks, chat or updates. Every surviving mobile MOBA (Mobile Legends, Wild Rift, Arena of Valor, Pokémon Unite, Brawl Stars) is a 5v5 joystick game with a dense live-service economy; none offers tap-precision control, a 3v3 one-lane-plus-jungle format, or a lean, open, "earn everything by playing" stance. That is the gap. Mandible's Lane War already has the hard parts most studios lack: a deterministic 30 Hz lockstep simulation proven across Windows, macOS, Android and iOS, three heroes with real abilities, creep waves, guardians, a shielded queen as the core, a shop, wild bugs in the jungle, an AI opponent, touch input and a balance harness. What it lacks is the MOBA layer on top: jungle objectives worth fighting over, a hero-centric touch scheme, a shop with choices instead of three stat bars, match formats with allies, a match clock, kill feed and surrender, progression between matches, and matchmaking plus a relay so strangers can find each other. This plan ranks those into three phases. The first three features to build are (1) **the ant jungle: respawning bug lairs with buffs, the aphid pasture as a contested gold objective and the Old Stag as the lane-breaking boss**, (2) **the phone hero scheme: tap-precision by default with an optional thumbstick, a thumb-zone ability button with hold-to-aim, camera follow and a phone-sized HUD**, and (3) **3v3 with AI allies on the existing 1v1 netcode**, which gives the Vainglory format without first solving N-player networking.

---

## 1. What Vainglory did, and why it was loved

### 1.1 Timeline

| Date | Event |
|---|---|
| Sep 2014 | Shown on stage at Apple's iPhone 6 event as the Metal graphics showcase [10] |
| 16 Nov 2014 | iOS release; The Guardian's best iOS game of 2014 [1][10] |
| 2015 | Apple Design Award; 1.5 M monthly active players by May; ESL Vainglory Cup; Android release 2 Jul [1][15] |
| Aug 2015 | $26 M raised; Asian esports push [16] |
| Dec 2016 | First World Championship, Los Angeles, $120 K pool [12] |
| Aug 2017 | $19 M more raised, $63 M total; "3,000 teams, 1 billion matches played" [16] |
| Dec 2017 | World Championship, Singapore, $140 K pool; 100,000 concurrent viewers at peak [12][6] |
| 13 Feb 2018 | 5v5 mode ("Sovereign's Rise") for all; joystick controls added in 3.3/3.4 [1][3][4] |
| Jul 2018 / Feb 2019 | PC and Mac alpha, then full Steam release with cross-play [1][10] |
| 2019 | Publishing handed to Rogue Games while the studio turned to Catalyst Black [7][8] |
| 27 Mar 2020 | Rogue shuts the servers down, almost without warning [10][9] |
| 1 Apr 2020 | "Community Edition" announced: client-authoritative, all skins free, no accounts, chat, friends or leaderboards; development paused "indefinitely" that July [9][10][14] |
| 28 Jul 2020 | NetEase closes the China version [17] |
| Jun 2022 | Community Edition development paused for good; downloads end on Steam, Samsung and Amazon; last iOS build 4.13.4 in Sep 2022 [10] |
| 25 May 2022 | Catalyst Black launches; studio down from 100-plus to about 20 people [7][8] |
| 2024 to 2026 | Still installable on iOS as an unmanaged Community Edition; players arrange matches themselves; one 2026 roundup scores its matchmaking 1/10 and support 0/10 but calls it "mechanically better than many supported competitors" [10][11] |

### 1.2 Core loop and match shape

- **Destroy the enemy crystal.** Lane minions spawn in waves and push; turrets line the lane; the crystal is invulnerable until the defences in front of it fall [1][2].
- **Match length.** Designed "from the ground up to cater for 20 minute matches"; one developer's own games averaged 21 minutes; the 3v3 format ran 15 to 20 minutes [5][1][11].
- **Economy.** Gold from last-hitting minions and jungle creatures, spent in a shop at base and at a jungle shop that opened four minutes in [2][18].
- **Three roles on a three-player team.** Laner (the carry who farms the lane), jungler (farms the camps, reaches level six for the ultimate, ganks), captain or roamer (the flexible initiator and disruptor who buys vision and utility and moves between lane and jungle) [13].
- **Three abilities per hero plus a heroic perk**, levelled through a shared tree; 7 heroes at launch, 56 by the end [1].

### 1.3 The 3v3 map, "Halcyon Fold"

One lane across the top, a jungle below it, mirrored for two teams [2]. The jungle was where the game happened:

- **Jungle camps** with buffs; an elder guardian creature that despawned around four minutes, opening the jungle shop [2][18].
- **Gold mine**, mid-jungle, from 4:00: a capture objective whose bar filled over three minutes and paid 400 gold to every member of the capturing team; later patches made it a contestable rather than held objective [2][18].
- **Minion mines**: capture one and your lane minions last longer and hit harder (a push amplifier rather than a gold payout) [2].
- **The Kraken**, from 15:00 (later 12:00), replacing the gold mine: a neutral boss that, once beaten and captured, "rampaged down the enemy side of the lane", breaking turrets. The signature comeback and closer objective [2][18].

### 1.4 The 5v5 map, "Sovereign's Rise" (2018)

Three lanes, a jungle, a river whose current carried heroes toward the outer lanes, shops at both ends of the river so players could spend without walking home, and a richer objective set [4][19]:

- **Two dragons** instead of the Kraken: one that marched mid lane and lit up turrets, one that gave the whole team an out-of-combat regenerating barrier [4][19].
- **Treants** that dropped pick-up orbs (+20 % ability damage with lifesteal; +15 % weapon damage with a slow), a healing treant, and a gold treant worth 200 gold [19].
- **Armouries** in front of the crystal: each one destroyed stripped the crystal of a homing missile and empowered your minions; the crystal fired a stunning line of explosions when threatened [4][19].
- **Teleport boots** to any allied turret, scout camera or minion [19].
- Reported spawn timings disagree between sources (first dragon at 6 or 7 minutes, second at 14), which is itself a lesson: timings were tuned patch by patch [4].

### 1.5 Items

Four categories with explicit counters: **weapon** (basic attack), **crystal** (abilities), **defence** (shield against crystal, armour against weapon, raw health) and **utility** (roam and vision). Items built in three tiers from components; the shop had six tabs and opened on a per-hero "Recommended" tab so new players could buy sensibly in one tap. Counter-building was taught by the game itself ("if the enemy has a great deal of crystal items, a shield is exceptionally important") [20].

### 1.6 Touch controls

- **Launch design: a single tap.** "Tap to move, tap to attack and so on." The studio avoided swipes and gestures "for maximum precision"; its founder argued two index fingers on a tablet "effectively become two mouse pointers", better than a mouse for kiting, and that testers preferred touch for complex moves. The engine targeted sub-30 ms input latency [5].
- **Critique of the tap scheme:** on phones the two-index-finger grip is unintuitive, the ability buttons sat where thumbs struggle to reach, the design favoured iPads, and the small phone minimap caused misplaced pings [21].
- **Joystick came late.** An optional virtual joystick arrived in 3.3 early access and 3.4 for everyone (mid-2018), with tap mode still available in settings; keyboard and mouse came in 2.8 for PC and Samsung DeX [3][1].

### 1.7 Modes

Ranked and casual 3v3 and 5v5; **Blitz**, a five-minute mode on a countdown where both teams start at level 9 with 6,000 gold, respawn in 12 s and race to 15 points with sudden death on a tie; **Battle Royale**, a lane-only mode with random heroes and no recall; rotating event modes such as ARAL, Rumble and Onslaught [22][1].

### 1.8 Progression and monetisation

- **Glory** (earned) unlocked heroes and upgraded Talents; **ICE** was the premium currency for skins and heroes; Opals for special skins; a card-crafting system later replaced by blueprints and essence; chests from play; a weekly free-hero rotation; mastery stars from 3.9 [1][23].
- The studio was described by consultants as an "anti-monetizer": at launch 10 heroes, 4 locked at a time, $2 to $5 each, and no skins or boosts at all; later, "everything, and I mean everything" including skins was earnable, with no ads [24][11].
- **Talents** (2.5) were upgrades only for Blitz and Battle Royale: 168 of them by 2020, with rare ones upgradable 20 times. They were the closest the game came to pay-for-power, and the community pushed back hard enough that 2.7 cut their costs and refunded buyers [1][23].

### 1.9 Esports

ESL ran the Vainglory Cup from June 2015; the Vainglory8 regional leagues fed two World Championships ($120 K in 2016 in Los Angeles, $140 K in 2017 in Singapore, with TSM and Team Secret fielding teams); the 2017 final peaked at 100,000 concurrent viewers; an eight-camera 4K spectator client was built with OGN in Korea [12][11][6][15]. All of it was paid for by the studio with no direct return [11].

### 1.10 Why it died

The studio's own account is consistent across interviews: the game had millions of players but "still wasn't large enough to support the cost base it had run up", a California team of over 100 plus self-funded tournaments; a cosmetics economy "requires a large number of paying players", harder still in a skill-based game; and there was "no plan B" when decline started, because nothing else was in development [6][7][8]. Outside analysts add that the 5v5 mode was built to chase Honor of Kings, "a way to improve the game, not a way to grow the game" [17]; that a 20-minute tutorial-heavy core had a "massive drop off rate" among newcomers even though those who stayed a week stayed for months [21]; and that the free-currency generosity in Southeast Asia removed the grind that keeps loyal players engaged [21].

### 1.11 What players say they loved and miss

Across reviews and retrospectives the same five things recur [5][11][21][25][26]:

1. **Controls that stayed out of the way.** Tap precision and 30 ms response; "instead of mouse clicks it's screen taps", and it was responsive enough to encourage a twitch pace.
2. **A real MOBA, not a cut-down one.** Deep heroes, real item strategy, a jungle that mattered, best-in-class hero balance.
3. **Fairness.** No pay-to-win, no ads, everything earnable.
4. **3v3 on one lane.** A format a trio of friends could fill, with every player mattering and no "feeder" lane.
5. **Craft.** Art and music a cut above, and a studio that visibly cared.

And what they lost with the Community Edition: accounts, ranked, friends and chat, guilds, matchmaking, updates, the Android and PC versions [10][9].

### 1.12 The remaining mobile MOBAs, and what they leave uncovered

| Game | Format | Controls | Match length | Economy | What it does not cover |
|---|---|---|---|---|---|
| Honor of Kings / Arena of Valor (Tencent, TiMi) | 5v5, three lanes | Dual joystick, auto-target | ~15 min | Skins, passes, heroes | China-first; dense live service; no tap precision; no 3v3 |
| Mobile Legends: Bang Bang (Moonton) | 5v5 | Joystick, auto-lock, target switching | 10 to 15 min | Skins, passes, emblems | Heavy monetisation; no 3v3; crowded UI |
| League of Legends: Wild Rift (Riot) | 5v5 | Joystick plus pseudo-joystick aim, auto-place | 15 to 18 min | Wild Pass, skins | The cleanest port, but a port; no 3v3; no tap mode |
| Pokémon Unite (TiMi / Nintendo) | 5v5, 10-minute cap, score goals, no towers | Joystick, auto-lock | 10 min | Licences, passes | Casualised; "designers favour AoE and auto-lock over aimed abilities" |
| Brawl Stars (Supercell) | 3v3 modes, ~2 to 3 min | Twin stick, auto-aim close range | 2 to 3 min | Passes, skins | A hero shooter, not a MOBA: no lanes, items, levelling or jungle |

Market context: 2025 mobile MOBA revenue is dominated by Honor of Kings (~$1.6 B), then Mobile Legends (~$158 M), Wild Rift (~$67 M), Arena of Valor (~$48 M) and Pokémon Unite (~$14 M); Asia Pacific is about 88 % of spending; the genre's spending peaked in October 2021 and has partly recovered. The common trends are shorter matches, battle passes and licensed IP [27][28][29][30][31].

**The uncovered niche, precisely:** a MOBA with (a) tap-precision controls as a first-class option, (b) a 3v3 single-lane-plus-jungle format that friends can fill, (c) 10 to 20 minute matches, (d) an open, honest economy with nothing gameplay-affecting for sale, (e) a studio-grade but lean production, and (f) something genuinely new to look at. Nobody is building that, because it is not where the top-grossing money is. It is exactly where a small, original, technically strong game can own a loyal audience, which is what Vainglory had before it over-scaled.

---

## 2. Gap analysis: Mandible's Lane War today

What exists (from `src/Sim/Lanes.cs`, `src/Game/LaneAI.cs`, `src/UI/LaneHud.cs`, `src/Sim/Sim.cs`, `src/Sim/Config.cs`, `src/Game/GameSession.cs`, `src/Net/Lockstep.cs`, `README.md`):

- Three lanes between the two nests, waypoints slid onto open ground; worn dirt paths and earth mounds as dressing; lanes drawn on the minimap.
- Free creep waves every 30 s from 10 s in: 4 or 5 soldiers per lane per race, gunners after 5 min, a major after 10, elite waves after 15, and a level step every 5 min so the war must end.
- Eight guardians per side (two per lane at 20 % and 36 % of the lane, two at the queen), immobile majors with 9x health, 3.2x damage and a 1.6x bonus against heroes who dive. The queen cannot be hurt while either of her two guardians stands; creeps that break in besiege her.
- One hero per side (Warlord, Acid Matron or Sky Marshal), level 1 to 10, one ability on Q that auto-picks its target, respawn of 8 + 2.2 × level seconds, a healing pool of radius 14 at the nest, hero XP shared within 15 units of a kill.
- A shop of three stat upgrades (bite +18 %, health +20 %, speed +10 % per tier, three tiers at 90/180/300 food) plus a 50-food 60 % heal, paid in food from kill bounties (10 per soldier, 120 per guardian, 60 + 10 × level per hero) and from foragers.
- Wild bugs in the jungle (the Colony War camps, minus any within 16 units of a lane); they guard their lairs, patrol, lick wounds, and have specials, but do not respawn and give no buff.
- A LaneAI opponent that pushes behind its wave, duels when ahead, respects guardians, retreats to heal, casts and shops; Hard switches to the most threatened lane.
- Touch: the generic RTS scheme (tap select, drag box, tap ground to order, long-press attack-move, two-finger pan, pinch, twist); Space finds the hero; the HUD is a 452-px-wide desktop panel in the bottom right.
- Multiplayer: two-player deterministic lockstep over ENet, host and join by address, LAN discovery, UPnP, adaptive input delay of 2 to 6 turns, state checksum every 10 turns, proven bit-identical on Windows x64 and macOS ARM64; hero pick in the lobby.
- Tooling: `--lanetest` headless matches and `tools/lanebalance.ps1` for all six hero matchups.
- In flight on main (uncommitted in the sibling session): armour per unit, redesigned bug specials and three-line hero cards (attack, defence, special) with the numbers the sim uses. This plan assumes those land.

**Landed on this branch after the audit (1 November 2026):** the Lane War yard is now horizontal (nests left and right, lanes along a phone held sideways, `WorldBuilder.Build(…, horizontal)` and a nest-derived `BuildLanes`); every lane post is a major with an acid gunner behind it so ranged heroes cannot siege from outside a post's reach; four guardians stand at each queen; guardians are repaired by the colony when left alone and take only 35 % damage from a hero with no creep wave at the post (`Lane.Backdoor`, `Lane.SiegeRadius`); late waves add a major every five minutes after 15:00 and level to 10; and Colony War gained an optional *Mound guardians* setting (menu and lobby, carried in the lockstep start packet, protocol `mandible-mp-6`). The two objectives also landed (M3 and M4 in their first form): the **aphid herd** arrives at 4:00 in the pasture between the middle and upper lanes, is milked by a lone hero over 90 s for 300 food and 150 hero XP, and returns every 3 minutes until 12:00; the **Old Stag** wakes at 12:00 under a log between the middle and lower lanes, with three times a stag beetle's health, and is roused rather than killed: it marches for the colony that felled it down the enemy's weakest lane at 1.6x bite until killed, then sleeps 4 minutes. The lane AI contests both. Alerts, the score line and the minimap report them. See [open-source-moba-survey.md](open-source-moba-survey.md) for the codebase survey.

**Measured pacing, 1 November 2026.** `tools/lanebalance.ps1 -Diff Normal -Seconds 1800` on commit `beac140` (headless, AI versus AI, seed 20261031):

| Matchup (black vs red) | Result |
|---|---|
| Warlord vs Matron | no winner at 30 min |
| Matron vs Warlord | no winner at 30 min |
| Warlord vs Marshal | Marshal wins at 18.6 min |
| Marshal vs Warlord | no winner at 30 min |
| Matron vs Marshal | no winner at 30 min |
| Marshal vs Matron | no winner at 30 min |

Five of six games never ended. Two AI heroes trading on three lanes, with guardians that punish dives and elite waves that both sides receive equally, reach a stalemate. This is the single most important finding of the audit: before any of the features below, Lane War needs objectives that break symmetry (the Old Stag, the pasture) and a hard pacing target (section 3.6, X3). The same script should print the median match length on every balance run so this never regresses.

| Area | Vainglory | Mandible Lane War today | Gap |
|---|---|---|---|
| Format | 3v3 (and 5v5), three roles | 1v1, one hero per colony | No allies, no roles, no team play |
| Map | One lane + jungle (3v3); three lanes + river (5v5) | Three lanes, jungle between them | Three lanes for one hero is too wide; nothing in the jungle worth fighting over |
| Jungle | Buff camps, gold mine, minion mines, boss | Wild bugs guard lairs, drop food on death, do not respawn, no buffs | Needs respawn timers, buffs, a contested gold objective and a boss |
| Core | Crystal shielded by turrets/armouries, fights back | Queen (3,200 HP) shielded by two guardians, bites back at reach 3.2 | Close. Missing: armoury-style layered defence, a visible "exposed" moment, no crystal special |
| Towers | Turrets with range and target priority | Immobile majors with 1.6x anti-hero damage | Close. Missing: tower aggro rules (hitting a hero under a tower draws it), plates/bounties, visible range |
| Heroes | 56, three abilities + perk each, levelled | 3, one ability each, auto-targeted | Need 2 to 3 abilities per hero, manual aim option, a bigger roster over time |
| Items | 4 categories, 3 tiers, components, counters, recommended tab | 3 stat bars + heal | Needs choices and counters (armour vs venom, utility, actives) |
| Economy | Gold by last hit, passive trickle, jungle gold | Food by kill bounty and foragers | Fine as a base; needs clarity (who gets it, passive income, bounties on killstreaks) |
| Controls | Single tap, then optional joystick; ability targeting | RTS touch scheme; hero found with Space | No hero-centric mode, no camera follow, no aimed abilities, no phone layout |
| Match clock and end | 15 to 20 min; surrender; Blitz 5 min | No clock in HUD; elite waves after 15 min; no surrender; five of six AI matchups stalemate at 30 min | Needs a clock, symmetry-breaking objectives, pacing targets, surrender, a short mode |
| Information | Kill feed, death recap (criticised for lacking it), pings, minimap | Alerts, score line (guardians, hero kills, next wave) | Needs kill feed, death recap, pings, objective timers on the minimap |
| Comms | Pings, emotes, chat lines, voice | None | Pings and canned lines at least |
| Progression | Glory, mastery stars, chests, rotation | None between matches | Needs a local profile: mastery, titles, cosmetic tints |
| Matchmaking | Elo ranked, casual, parties | Direct connect by address, LAN | Needs a lobby service, relay, rating, reconnect |
| Monetisation | Cosmetics; later Talents | Free and open source | A stance to choose (section 3.8) |
| Tutorial | Dull, high drop-off | Colony War tutorial only | A 3-minute Lane War tutorial on the real map |

---

## 3. Roadmap

Effort: **S** = a day or two, **M** = about a week, **L** = two to four weeks, for one developer who knows the codebase. Priority: **P0** = the first release of "Lane War as a MOBA", **P1** = the release that makes it a service, **P2** = growth. Every item names the files it touches; new files are marked *(new)*.

### 3.1 Map and objectives, in ant terms

The map is the yard. The jungle is **the undergrowth** between the lanes. The crystal is **the queen**. Turrets are **mound guardians**. The following gives the undergrowth a reason to exist.

| # | Feature | What it is | Effort | Priority | Files |
|---|---|---|---|---|---|
| M1 | **Lairs that refill** | Every wild camp spawned at start respawns 60 to 90 s after it is cleared (bugs hatch from the lair, with a shimmer and a lair marker on the minimap), at a level that grows every 5 minutes like the waves. Killing a lair pays food and hero XP, as now. | S | P0 | `src/Sim/Sim.cs` (camp spawn loop near line 171 and death bookkeeping), `src/Sim/Lanes.cs` (timers, part of the checksum mix at `Sim.cs:572`), `src/UI/Minimap.cs`, `src/World/WorldBuilder.cs` (lair placement respects lanes) |
| M2 | **Lair buffs** | Each lair kind grants a buff for 90 s to the hero who lands the kill: **beetle lair**: hard shell (+armour, applies on top of the new per-unit armour), **spider web**: silk-cutter stride (+speed, immune to roots), **wasp nest**: venom bite (basic attacks apply a short poison). The buff is a visible ring and an icon on the hero card; dying drops it. | M | P0 | `src/Sim/Sim.cs` (buff fields on `Ant`, `Damage` hooks), `src/Sim/Config.cs`, `src/UI/LaneHud.cs`, `src/Render/Effects.cs`, `src/Audio/AudioDirector.cs` |
| M3 | **The aphid pasture** | Mandible's gold mine, and real ant biology: a herd of aphids on a stem at the map centre from 4:00. A hero standing in the pasture with no enemy hero in it **milks honeydew** for 3 minutes; when the bar fills, every ally hero and the colony bank receive a lump of food, and the herd migrates (respawns 3 minutes later). Contestable: an enemy hero entering pauses the bar for both. Shown on the minimap with its bar. | M | P0 | `src/Sim/Objectives.cs` *(new)*, `src/Sim/Lanes.cs`, `src/World/WorldBuilder.cs` (a stem prop and a flat patch at centre), `src/Render/*` (aphids as tiny instanced bugs), `src/UI/Minimap.cs`, `src/UI/LaneHud.cs` |
| M4 | **The Old Stag** | Mandible's Kraken: at 12:00 a huge stag beetle wakes under a log in the centre of the undergrowth (replacing the pasture at that spot, as Vainglory replaced the mine). Beating it takes a team; the team that lands the kill does not loot it but **rouses** it: it lumbers down the lane with the most damaged enemy guardians, tossing creeps and shattering guardians until killed. It respawns 4 minutes after it dies. This is the comeback and the closer. | L | P0 | `src/Sim/Objectives.cs` *(new)*, `src/Sim/Sim.cs` (a new `Ant` behaviour: a wild bug that becomes a lane pusher for a team), `src/Sim/Lanes.cs`, `src/Game/LaneAI.cs` (the AI contests it), `src/Render/AntMesh.cs` (a larger stag variant with a scar), `src/Audio/AudioDirector.cs` (a rumble), `src/UI/Minimap.cs` |
| M5 | **One-lane map for 3v3** | A second Lane War layout, the Vainglory shape: one lane along the top edge of the yard, the undergrowth below it with four lairs per side, the pasture and the Old Stag in the middle, a healing dew drop at each jungle mouth. Generated by `WorldBuilder` from the same seed logic, mirrored. The three-lane map stays for Colony-style 1v1. | M | P0 | `src/Sim/Lanes.cs` (`BuildLanes` takes a layout), `src/World/WorldBuilder.cs`, `src/Game/GameSession.cs` (`BuildLaneDressing`), `src/Main.cs` (layout choice), `src/Net/Lockstep.cs` (layout in the start packet) |
| M6 | **Layered queen defence** | The queen's two guardians become **brood-gallery gates**: each one destroyed removes one of the queen's two defences, a spray of formic acid that slows and hurts attackers near her, and empowers your creep waves in every lane (+1 soldier, +10 % bite). The queen's "exposed" state gets a sound, a HUD banner and a minimap pulse. | S | P1 | `src/Sim/Lanes.cs` (`QueenShielded`, `LaneSiege`), `src/Sim/Sim.cs` (queen attack), `src/UI/LaneHud.cs`, `src/Audio/AudioDirector.cs` |
| M7 | **Guardian rules** | Visible range ring when a hero approaches; a guardian switches to a hero that attacks an allied hero under it (tower aggro); guardians give a **plate bounty** to the whole team in the first 10 minutes so early sieges pay. | S | P1 | `src/Sim/Sim.cs` (target choice around line 1008), `src/Render/Effects.cs`, `src/Sim/Lanes.cs` (`Bounty`) |
| M8 | **Scent markers and tunnels** | Vision and mobility: a cheap **scent marker** utility item that reveals a radius for 90 s (fog already exists in `FogOfWar`); a **tunnel** between a team's outer guardian and its nest so the walk back is short, dug open at 8:00. | M | P1 | `src/Game/FogOfWar.cs`, `src/Sim/Lanes.cs`, `src/Sim/Nest.cs` (tunnel digging already exists for rooms), `src/Render/NestInterior.cs` |
| M9 | **Dew drops** | A healing pick-up at each jungle mouth: a drop of dew that heals over 8 s and respawns in 90 s, the mid-map sustain that lets a laner stay out. | S | P1 | `src/Sim/Objectives.cs`, `src/World/WorldBuilder.cs`, `src/Render/Effects.cs` |

### 3.2 Hero roles and roster plan

Vainglory's three roles map cleanly onto the three heroes already in the game, which is a strong start for 3v3:

| Role | Vainglory name | Mandible hero | Why it fits | Changes needed |
|---|---|---|---|---|
| Lane carry | Laner | **Acid Matron** | Ranged, splash on basic attacks, Acid Burst clears waves and leaves a pool; 1.45x HP and 1.5x damage in Lane War already | Give her a second ability: a **retreating spray** (short backward hop that leaves a venom line) so she has an escape; make Acid Burst aimable |
| Jungler | Jungler | **Sky Marshal** | Flies over rocks between lairs, fast, Dive Bomb is a gank opener that roots | Second ability: **Scout's eye**, a circling pass that reveals a radius (vision for the team); Dive Bomb aimable; jungle buffs matter most to it |
| Captain | Roamer | **Warlord** | Toughest, cleaving bites, War Cry buffs allies and staggers enemies (in the in-flight redesign) | Second ability: **Mandible lock**, a single-target grab that holds one enemy hero for 1.2 s (the initiator's tool); War Cry stays as the team buff |

Roster growth, all from castes and bugs the game already models or could model with its existing generated art pipeline (original names, no borrowed heroes):

| Order | Hero | Role | Kit sketch | Source art | Effort |
|---|---|---|---|---|---|
| 4th | **Trapjaw** | Burst assassin (jungler) | Snap-jaw leap (real trap-jaw ants jump by firing their jaws), a crit on the first bite after a leap, a vault over a rock | Soldier mesh with long mandibles | M |
| 5th | **Weaver** | Captain | Silk net that roots a patch, a leaf-shield on an ally, a pull line | Worker mesh, silk from the spider's web shader | M |
| 6th | **Replete** | Support | A honeypot ant: feeds allies (heals over time), a sugar rush speed burst, slow and fat (real repletes) | Worker mesh with a swollen gaster | M |
| 7th | **Dracula** | Duellist | The fastest animal bite known; stacks a bleed, a dash that resets on kill | Soldier mesh, dark chitin | M |
| 8th | **Forewoman** | Lane bruiser | Leafcutter: carries a leaf as a shield, throws it as a slow, calls a worker line that bodyblocks | Major mesh with a leaf | M |

Each new hero touches `src/Sim/Config.cs` (stats, `AttackDesc`, `DefenceDesc`, `SpecialDesc`), `src/Sim/Sim.cs` (`HeroAbility`), `src/Render/AntMesh.cs` and `src/Render/AntRenderer.cs`, `src/UI/FieldGuide.cs`, `src/Game/LaneAI.cs` (how the AI plays it), and the `HeroPicker` in `src/Main.cs`. Aim for three roles per two heroes so a 3v3 draft always has a sensible comp.

**Abilities as a system (prerequisite, M, P0).** Today a hero has one ability on Q that fires at the best target when `CastReq` is set. For a MOBA each hero needs two to three abilities with a cast kind each: *self* (War Cry), *target* (Mandible lock), *point* (Acid Burst, Dive Bomb) and *direction* (retreating spray). That means: an `Ability` descriptor table in `src/Sim/Config.cs`, a `Cmd` that carries ability index, point and target (`src/Game/Commands.cs`, `CmdKind.Ability` already exists, add `Arg` and `P` use), per-ability cooldowns on `Ant` (`src/Sim/Sim.cs`), and the HUD buttons (`src/UI/LaneHud.cs`). Keep the auto-target behaviour as the default for every ability so the one-tap game still works; aiming is an option, never a requirement.

### 3.3 Item shop

Replace the three stat bars with a small, legible shop that creates decisions, keeping Mandible's food economy and its "pay in food from kills and foragers" fiction.

| # | Feature | Effort | Priority | Files |
|---|---|---|---|---|
| I1 | **Four stalls**: *Jaws* (bite, cleave, attack speed), *Venom* (ability power, poison length), *Chitin* (armour against bites, resin against venom, raw health), *Trail* (speed, scent markers, dew flasks). Three tiers per stall built from components, 10 to 12 finished items total, each with one line of what it counters. | M | P0 | `src/Sim/Lanes.cs` (item table replaces `ItemNames`/`ItemCost`), `src/Sim/Sim.cs` (`ApplyLaneStats`, damage split into bite and venom so counters work), `src/UI/LaneHud.cs`, `src/Game/LaneAI.cs` (counter-buys: armour when the enemy carry is a biter, resin against the Matron) |
| I2 | **Recommended tab** per hero that fills the first three buys with one tap each, as Vainglory's did; a "Why" line on each recommendation. | S | P0 | `src/UI/LaneHud.cs`, `src/Sim/Config.cs` (per-hero build) |
| I3 | **Two active items**: a dew flask (the current Royal Jelly heal, now with a cooldown rather than unlimited buys) and a scent marker. | S | P0 | `src/Sim/Lanes.cs`, `src/Sim/Sim.cs` |
| I4 | **Shop anywhere at home, and a forward cache**: buying is allowed only within the fountain radius or at a forager's cache that opens at each jungle mouth at 4:00 (Vainglory's jungle shop). | S | P1 | `src/Sim/Lanes.cs` (`CanBuy` gains a position check), `src/World/WorldBuilder.cs`, `src/UI/LaneHud.cs` |
| I5 | **Sell back at 70 %**, and item slots capped at six so late builds are choices. | S | P1 | `src/Sim/Lanes.cs`, `src/UI/LaneHud.cs` |

### 3.4 Match formats

| # | Format | What it is | Effort | Priority | Files |
|---|---|---|---|---|---|
| F1 | **1v1 duel** (exists) | Keep it, on the three-lane map, as the ladder's entry rung and the balance harness. Add a match clock, kill feed, death recap and surrender (section 3.6). | S | P0 | `src/UI/LaneHud.cs` |
| F2 | **3v3 with AI allies** | Each human steers one hero; `LaneAI` instances play the other two per side with role assignments (one laner, one jungler, one captain) and simple coordination (gank calls when the jungler is near a lane with a low enemy, group for the Old Stag). Works today on the 1v1 lockstep because allies are deterministic AI driven by the same sim. Hero picks for allies in the lobby. | M | P0 | `src/Game/LaneAI.cs` (roles, a shared team blackboard), `src/Sim/Lanes.cs` (`HeroId` becomes per-slot: three heroes per team, `Items` per hero), `src/Sim/Sim.cs` (checksum mix), `src/Main.cs` (lobby picks), `src/UI/LaneHud.cs` (ally portraits with a tap to ping them) |
| F3 | **Skirmish** (5 minutes) | Vainglory's Blitz in ant terms: one lane, both sides start at level 6 with a full tier-two build, respawn 10 s, first to 12 points (hero kill 1, guardian 3, queen 6) or the lead at 5:00, sudden death on a tie. The queue-filler and the tutorial's graduation match. | M | P1 | `src/Sim/Lanes.cs` (a `LaneRules` struct), `src/UI/LaneHud.cs` (clock and score), `src/Main.cs` |
| F4 | **Co-op versus the yard** | 3 humans or 1 human plus 2 AI against a Hard AI trio; the easiest way for friends to play together before matchmaking exists. | S after F2 | P1 | `src/Main.cs`, `src/Game/GameSession.cs` |
| F5 | **True 3v3 online** | Six humans. Needs N-peer lockstep and a relay (section 3.7). | L | P2 | `src/Net/Lockstep.cs`, `src/Net/Relay.cs` *(new)* |

### 3.5 Touch controls per phone

Vainglory's lesson is two-sided: tap precision was its identity, and the joystick arrived four years late because players on phones asked for it. Mandible should ship both from the start, and should fix the phone-layout complaints Vainglory never did.

| # | Feature | What it is | Effort | Priority | Files |
|---|---|---|---|---|---|
| T1 | **Hero mode input** | In Lane War the hero is always selected; the camera follows it (toggle by two-finger pan, snap back with the hero button). Tap ground = move, tap enemy = attack, tap and hold on ground = attack-move, double-tap your mound = walk home. No box select. | M | P0 | `src/Game/PlayerInput.cs` (a `HeroInput` path beside the RTS one), `src/Game/RtsCamera.cs` (follow), `src/Game/Settings.cs` |
| T2 | **Precision and Thumbstick modes** | A setting with two schemes. *Precision* is T1. *Thumbstick* puts a floating joystick on the left half (appears where the thumb lands), attack button and abilities on the right, auto-target nearest with a hero-first toggle. Both schemes are always available in settings and mid-match. | M | P0 | `src/UI/TouchPad.cs` *(new)*, `src/Game/PlayerInput.cs`, `src/Game/Settings.cs` |
| T3 | **Ability buttons in the thumb zone** | Right-hand arc of two to three round buttons above the attack button, 64 px minimum, with cooldown sweep. Tap = auto-target cast. **Hold and drag = aim** (a ground reticle or direction arrow, release to cast, drag back onto the button to cancel), the Wild Rift pseudo-joystick without the joystick. | M | P0 | `src/UI/TouchPad.cs`, `src/UI/LaneHud.cs`, `src/Game/Commands.cs` |
| T4 | **Phone HUD** | On `Settings.Mobile` and narrow widths: hero card as a compact strip top-left, shop as a full-screen sheet opened by one button, score and clock top-centre, minimap top-right with larger ping targets (Vainglory's phone minimap was criticised for misplaced pings). Portrait layout keeps the lane HUD usable with the minimap docked top. | M | P0 | `src/UI/LaneHud.cs`, `src/UI/Hud.cs` (portrait layout code already exists), `src/UI/Minimap.cs`, `src/UI/UiTheme.cs` |
| T5 | **Pings** | Long-press the minimap or a lair for a ping wheel: *attack*, *retreat*, *on my way*, *objective*. Pings are orders in the lockstep stream so both sides see them at the same tick. | S | P0 | `src/Game/Commands.cs` (`CmdKind.Ping`), `src/UI/Minimap.cs`, `src/Render/Effects.cs`, `src/Audio/AudioDirector.cs` |
| T6 | **Input latency budget** | Measure tap-to-order-visible on a mid phone; target one lockstep turn (100 ms) plus one frame. Show the ordered move at once as a local marker before the turn executes (the sim stays authoritative). | S | P1 | `src/Game/PlayerInput.cs`, `src/Game/GameSession.cs` |
| T7 | **Controller and keyboard parity** | PC keeps right-click and QWER; add gamepad for Android TV and iPad with a controller: left stick move, face buttons for abilities, aim with the right stick. | S | P2 | `src/Game/PlayerInput.cs` |

### 3.6 Match presentation and pacing

| # | Feature | Effort | Priority | Files |
|---|---|---|---|---|
| X1 | **Match clock, objective timers** (next wave, next lair, pasture bar, Old Stag wake) on the HUD and minimap | S | P0 | `src/UI/LaneHud.cs`, `src/UI/Minimap.cs` |
| X2 | **Kill feed and death recap** (who, with what, last three hits; Vainglory was criticised for lacking this) | S | P0 | `src/Sim/Sim.cs` (record the last damage sources on `Ant`), `src/UI/LaneHud.cs` |
| X3 | **Surrender** at 10:00 by vote; **pace targets**: median 1v1 under 14 minutes, 3v3 under 18, enforced by the elite-wave timer and the Old Stag | S | P0 | `src/Sim/Lanes.cs`, `src/Game/Commands.cs`, `src/UI/LaneHud.cs`, `tools/lanebalance.ps1` (print median length) |
| X4 | **Lane War tutorial**: three minutes on the real map, teaching tap, ability, a lair, the pasture, a guardian; graduates into a Skirmish | M | P0 | `src/UI/Tutorial.cs` (currently Colony War only), `src/Game/GameSession.cs` |
| X5 | **End screen**: timeline of objectives, damage dealt, food earned, items; a "rematch" button that reuses the lobby | S | P1 | `src/UI/Hud.cs`, `src/Main.cs` |
| X6 | **Spectate and replay**: a lockstep order log is a replay by construction; save it, play it back at 1x to 8x; a spectator client is one extra peer that only receives | M | P2 | `src/Net/Lockstep.cs`, `src/Capture.cs` (already replays scripted matches) |

### 3.7 Progression, matchmaking and relay for the deterministic lockstep

**Progression (local first, account later).**

| # | Feature | Effort | Priority | Files |
|---|---|---|---|---|
| P1 | **Local profile**: matches, wins, per-hero mastery level 1 to 20 from XP earned in matches, titles ("Keeper of the Pasture"), Field Guide pages unlocked by deeds (kill the Old Stag once to read its page). Stored in `user://profile.json`, no account. | M | P0 | `src/Meta/Profile.cs` *(new)*, `src/UI/FieldGuide.cs`, `src/Main.cs` |
| P2 | **Cosmetic tints**: chitin colour variants and trail colours for heroes, earned by mastery (everything earnable; this is also the only thing that could ever be sold, see 3.8). Rendered by the existing instanced shader's team tint path. | S | P1 | `src/Render/Shaders.cs`, `src/Render/AntRenderer.cs`, `src/Meta/Profile.cs` |
| P3 | **Daily deeds**: three small goals a day (milk the pasture twice, win a Skirmish, land five Dive Bombs), rewarding mastery XP only. | S | P1 | `src/Meta/Profile.cs`, `src/UI/Hud.cs` |
| P4 | **Cloud profile and friends** once a backend exists (below). | M | P2 | `src/Meta/*`, backend |

**Matchmaking and relay.** The netcode's constraints and what each layer needs:

- *Today*: exactly two peers, host and guest, ENet UDP on port 47010, LAN broadcast discovery, UPnP for internet hosting; phones on carrier NAT cannot host; input delay adapts to ping; a checksum every 10 turns detects desyncs but there is no recovery; determinism proven on Windows x64 and macOS ARM64 and in an Android emulator workflow [README].
- *The MOBA needs*: strangers finding each other, phones behind NAT connecting to phones behind NAT, a game that survives a 10-second signal drop, three to six peers eventually, and fairness information (rating).

| # | Feature | What it is | Effort | Priority | Files |
|---|---|---|---|---|---|
| N1 | **Relay server** | A small stateless UDP relay (Go or C#, one process on a $5 VPS per region) that forwards lockstep packets between peers by room code. Phones can always reach it, so NAT stops mattering. Direct connection is tried first and the relay used as fallback, like TURN behind WebRTC. Godot's `WebRTCMultiplayerPeer` with a coturn server is the alternative, but a bespoke relay for a few hundred bytes a second is simpler and keeps ENet [32][33]. | M | P0 | `src/Net/Relay.cs` *(new, client side)*, `src/Net/Lockstep.cs` (transport abstraction), `server/relay/` *(new)*, `README.md` |
| N2 | **Lobby and room codes** | A tiny HTTPS service: create room, join by six-letter code, list open public rooms by mode and region; the relay address comes from it. Replaces typing an IP. | M | P0 | `server/lobby/` *(new)*, `src/Main.cs` (`Lobby` UI), `src/Net/Lockstep.cs` |
| N3 | **Reconnect** | Keep the last 30 s of turns on both peers and on the relay; a peer that drops rejoins, receives the missed turns and fast-forwards (the sim is deterministic, so replaying orders restores the exact state). If the gap exceeds the buffer, send a state snapshot (`Sim` already has a checksum walk; add a serialiser over the same fields). | M | P0 | `src/Net/Lockstep.cs`, `src/Sim/Sim.cs` (serialise), `src/Game/GameSession.cs` |
| N4 | **Desync recovery** | On a checksum mismatch, the host's snapshot wins and the guest reloads it, with a HUD note; log both checksums and the turn's orders to a file for bug reports. Extend the checksum mix with every new piece of lane state (buffs, objectives, per-hero items), or every feature in this plan is a desync risk. | S | P0 | `src/Net/Lockstep.cs`, `src/Sim/Sim.cs:572` |
| N5 | **Rating and queue** | Glicko-2 per mode on the lobby service; a queue that widens its rating window over 60 s, fills with AI allies in 3v3 after 90 s, and pairs by region. | M | P1 | `server/lobby/`, `src/Main.cs` |
| N6 | **N-peer lockstep** | Generalise `_turns` from two peers to a peer list; a turn is ready when every peer's orders have arrived; the relay collects and rebroadcasts so each phone sends once. Delay chosen from the worst ping. Required only for true 3v3 (F5). | L | P2 | `src/Net/Lockstep.cs`, `src/Game/Commands.cs` |
| N7 | **Cross-platform determinism gate** | A CI job that runs `--synctest` on Windows x64, macOS ARM64, Android ARM64 and iOS simulator and diffs the checksum trails on every commit touching `src/Sim`. Cheap insurance for everything above. | S | P0 | `.github/workflows/` *(new)*, `tools/android-synctest.sh` (exists) |
| N8 | **Fairness in lockstep** | Lockstep gives every client the whole state, so map hacks are possible. Acceptable for 1v1 and friends; for ranked, mitigate with server-side replay verification (the lobby re-simulates reported games headlessly, which `--lanetest` already can) rather than an authoritative server. | M | P2 | `server/verify/` *(new)*, `src/Capture.cs` |

### 3.8 Monetisation: three stances to choose from

Vainglory proved two things: players adore a game with nothing gameplay-affecting for sale, and a cosmetics-only economy cannot carry a 100-person studio. Mandible is one person and a generated-asset pipeline, so the maths is different. Whatever is chosen, the public promise should be fixed now and never broken: **no hero rotation, no locked heroes, no Talents, no ads, no gameplay power for money.**

| Stance | How it earns | Fits | Costs it must cover | Risk |
|---|---|---|---|---|
| **A. Open and supported** (today, plus) | Free, source open, GitHub Sponsors and a tip jar in the menu; relay and lobby funded by sponsors | The project as it is, a community that forks and mods | Relay and lobby, roughly $10 to $40 a month per region at Mandible's bandwidth | No budget for art or a second developer; servers run at the owner's mercy |
| **B. Premium with a free duel** | Free download with 1v1 and Skirmish; a one-time purchase (about $4.99) unlocks 3v3, co-op, cosmetic tints and ranked; no subscriptions | Players who miss Vainglory's fairness and distrust live-service economies; a small studio's cash flow | Same servers, paid by sales; store fees of 15 to 30 % | A paywall in front of the best mode slows the community; piracy on Android |
| **C. Free with cosmetics and a season pass** | Free; chitin tints, trail colours, mound decorations and a two-tier seasonal pass with only cosmetic and mastery rewards | The market's proven model (every surviving MOBA) and the largest potential audience | Needs scale: Vainglory's own lesson is that cosmetics need many payers; needs a storefront, accounts and support | The slippery slope to pay-for-power under revenue pressure; the premium feel of generated art may not sell as skins |

Recommendation for the user to choose against: start on **A** (nothing to build), move to **B** once 3v3 with AI allies and the relay exist, and treat **C** only if the player base reaches tens of thousands of monthly players. The promise above is the thing players said they missed most; it is free to keep.

### 3.9 Phased summary

| Phase | Goal | Items | Rough total |
|---|---|---|---|
| **P0: "Lane War is a MOBA"** | A 3v3 match with AI allies on a one-lane map with a living jungle, playable on a phone with one thumb, between friends over a relay | M1 M2 M3 M4 M5, abilities system, I1 I2 I3, F2, T1 T2 T3 T4 T5, X1 X2 X3 X4, P1, N1 N2 N3 N4 N7 | 3 to 4 months for one developer |
| **P1: "It is a service"** | Ranked ladder, Skirmish, co-op, deeper shop and map, progression deeds | M6 M7 M8 M9, I4 I5, F3 F4, T6, X5, P2 P3, N5 | 2 months |
| **P2: "It grows"** | Six-human 3v3, replays and spectating, cloud profiles, four more heroes, controller | F5, N6 N8, X6, P4, T7, heroes 4 to 8 | ongoing |

---

## 4. Risks

0. **Stalemates.** Measured today: five of six AI matchups do not end in 30 minutes. A MOBA that cannot finish is not a MOBA. The Old Stag (M4), the pasture (M3), gallery gates that empower waves (M6) and a hard pacing budget (X3) exist to fix this; the balance script must report match length on every run.
1. **Scope creep toward 5v5.** Vainglory's 5v5 cost it the identity that made it loved and did not grow it [17]. Mandible should treat 3v3 with one lane as the product and never promise five lanes of heroes. The three-lane map stays for 1v1 and Colony War.
2. **Determinism debt.** Every feature here adds simulation state. One unmixed field in the checksum or one float path that differs between ARM and x64 is a desync in a ranked match. N4 and N7 are not optional and should land before M1.
3. **Phone performance.** The yard already simulates up to 3,000 ants; a one-lane map with lairs that respawn, an Old Stag and six heroes with particle effects must hold 30 Hz on a mid-range Android phone on the Low quality tier. Budget a profiling pass per P0 feature and keep `--lanetest` frame-time output in the balance script.
4. **The AI allies are the product in P0.** If `LaneAI` teammates feed, 3v3 with AI fill is worse than 1v1. Role logic, retreat thresholds and objective calls need their own balance harness (extend `tools/lanebalance.ps1` to trios) before humans meet them.
5. **Running servers.** Even a relay is an ongoing cost and a single point of failure, which is exactly how Vainglory ended. Keep direct connection and LAN as first-class paths forever, publish the relay source so the community can host its own, and design the lobby so the game works when it is down.
6. **Onboarding drop-off.** Vainglory's tutorial lost most newcomers [21]. The three-minute tutorial (X4) and the five-minute Skirmish (F3) are the funnel; measure completion locally before any marketing.
7. **Art bar on a generated pipeline.** The audience that misses Vainglory remembers art "among the best seen on tablets" [21]. Every new object here (aphids, the stem, the Old Stag, the log, dew drops) needs the same care as the nest work in v0.6.0, and hero readability against the ground (a Vainglory complaint) matters more on a phone than on a desktop.
8. **Originality.** Nothing in this plan borrows a name, hero, map or line from Vainglory, and that must hold in implementation: the pasture is aphids because ants farm aphids, the boss is a stag beetle because the game already has one, the "roam" role is the Warlord because it already buffs allies.
9. **Parallel work.** The sibling session is changing `Config.cs`, `Sim.cs`, `Lanes.cs` and `LaneHud.cs` right now (armour, bug specials, hero cards). Rebase this plan's P0 branches on that work rather than beside it; the ability system (3.2) in particular should build on the new `AttackDesc`, `DefenceDesc` and `SpecialDesc` cards.

---

## The first three features to build

1. **The living undergrowth (M1 + M2 + M3 + M4, with N4).** Respawning lairs with buffs, the aphid pasture at 4:00 and the Old Stag at 12:00. This single step turns the empty space between the lanes into the reason to leave your lane, and gives Lane War its comeback mechanic and its closer. Effort L in total; files `src/Sim/Objectives.cs` (new), `src/Sim/Sim.cs`, `src/Sim/Lanes.cs`, `src/World/WorldBuilder.cs`, `src/Render/*`, `src/UI/Minimap.cs`, `src/Game/LaneAI.cs`.
2. **The phone hero scheme (T1 + T2 + T3 + T4 + T5).** Hero-centric input with camera follow, Precision and Thumbstick modes, thumb-zone ability buttons with hold-to-aim, a phone-sized HUD and pings. This is the Vainglory identity (tap precision) with its two biggest phone complaints fixed, and it is what a player feels in the first ten seconds. Effort L in total; files `src/Game/PlayerInput.cs`, `src/UI/TouchPad.cs` (new), `src/UI/LaneHud.cs`, `src/Game/RtsCamera.cs`, `src/Game/Settings.cs`, `src/UI/Minimap.cs`.
3. **3v3 with AI allies on the one-lane map (F2 + M5).** Three heroes per side with roles, two of them played by `LaneAI`, on a Vainglory-shaped single lane with the undergrowth below it, running on the existing two-player lockstep. It delivers the format players miss without first solving six-peer networking, and it is the playtest bed for every balance question above. Effort L; files `src/Game/LaneAI.cs`, `src/Sim/Lanes.cs`, `src/Sim/Sim.cs`, `src/World/WorldBuilder.cs`, `src/Main.cs`, `src/UI/LaneHud.cs`, `src/Net/Lockstep.cs`.

After those three, the relay and room codes (N1 + N2 + N3) are what let strangers play, and the shop stalls (I1 + I2 + I3) are what give a 15-minute match its mid-game decisions.

---

## Sources

1. Wikipedia, "Vainglory (video game)": https://en.wikipedia.org/wiki/Vainglory_(video_game)
2. r/vainglorygame community encyclopedia (mirror): https://reddit.birdcat.cafe/r/vainglorygame/wiki/encyclopedia and the Winter Season jungle changes post, https://de.vainglorygame.com/vainglorylive/winter-season-jungle-changes-more-lethal-more-depth/
3. detikInet, joystick controls after the 5v5 update (May 2018): https://inet.detik.com/games-news/d-4031985/setelah-mode-5v5-vainglory-akan-terapkan-kontrol-joystick ; Softpedia listing for 3.3.1 (joystick early access): https://mobile.softpedia.com/apk/vainglory/3.3.1+%2876797%29/
4. Super Evil Megacorp, "Unveiling Vainglory 5V5": https://superevilmegacorp.com/news/unveiling-vainglory-5v5-because-mobile-gamers-deserve-better/ ; 5v5 map page: https://www.vainglorygame.com/5v5-map ; TouchArcade on the 5v5 release: https://toucharcade.com/?p=230679
5. iMore interview with the creators on controls and 20-minute matches: https://www.imore.com/vainglory-interview-creators-heroes-controls-metal-more
6. PocketGamer.biz, Kristian Segerstrale on cost base and the cosmetics economy: https://www.pocketgamer.biz/kristian-segerstrale-the-best-companies-are-punched-in-the-face-a-few-times/
7. MobileGamer.biz, what the studio learned from Vainglory for Catalyst Black: https://mobilegamer.biz/how-super-evil-megacorp-took-everything-it-learned-from-vainglory-and-put-it-into-catalyst-black/
8. Noisy Pixel, Catalyst Black interview: https://noisypixel.net/catalyst-black-interview-ios/ ; Wikipedia, "Catalyst Black": https://en.wikipedia.org/wiki/Catalyst_Black
9. XDA Developers, Rogue Games shutdown and Community Edition: https://www.xda-developers.com/rogue-games-shuts-down-vainglory-game-servers-community-edition-free-skins-semc/ ; PocketGamer.biz, "Super Evil Megacorp rescues Vainglory": https://www.pocketgamer.biz/super-evil-megacorp-rescues-vainglory
10. Rin Blog, "Is Vainglory still playable? Status and shutdown timeline": https://rinblog.org/en/vainglory-current-status-en/
11. Monster Journal, "Vainglory, the rise and fall of the mobile MOBA ahead of its time": https://monsterjournal.com/vainglory-the-rise-and-fall-of-the-mobile-moba-ahead-of-its-time/ ; Marks Angry Review, 10 best mobile MOBAs of 2026: https://marksangryreview.com/top-mobile-moba-games/
12. Esports Earnings, Vainglory World Championship: https://www.esportsearnings.com/leagues/1061-vainglory-world-championship/events ; GamesBeat, first world champion: https://gamesbeat.com/?p=131907 ; Digital News Asia, 2017 Singapore: https://www.digitalnewsasia.com/personal-tech/vainglory-world-championship-be-held-singapore
13. BlueStacks beginner guide (roles): https://www.bluestacks.com/blog/game-guides/vainglory/vg-beginner-guide-en.html ; jungler guide: https://www.bluestacks.com/blog/game-guides/vainglory/vg-jungler-tips-tricks-en.html ; official "Squid Ink: how roaming has changed": https://kr.vainglorygame.com/news/squid-ink-how-roaming-has-changed/
14. Official Community Edition update (development paused, servers up): https://br.vainglorygame.com/news/vainglory-community-edition-update-edtheshred/
15. GamesBeat, 1.5 M monthly active players (July 2015): https://gamesbeat.com/mobile-game-vainglory-hits-1-5m-monthly-active-users/
16. Inven Global, $19 M raise, $63 M total, 3,000 teams and 1 billion matches (Aug 2017): https://www.invenglobal.com/articles/2606/super-evil-megacorp-has-raised-an-additional-19-million-dollars-from-investors ; PocketGamer.biz on the $26 M Asia-focused round: https://www.pocketgamer.biz/26-million-investment-turns-super-evils-attentions-to-esports-in-asia
17. Medium, "Why Honor of Kings is crushing Vainglory": https://medium.com/esports-and-gaming-analysis-by-owens/why-honor-of-kings-is-crushing-vainglory-2f728a38e53d ; MMO Culture, NetEase closes the China version: https://mmoculture.com/2020/05/vainglory-netease-announces-closure-of-pc-mobile-moba-title-in-china/
18. Inven Global, "Early impression of Vainglory 2.0 meta" (jungle shop, gold mine, Kraken timings): https://www.invenglobal.com/articles/646/early-impression-of-vainglory-20-meta ; 2.1.2 patch notes (APKMirror): https://apkmirror.com/apk/super-evil-megacorp/vainglory/vainglory-2-1-2-47730-release
19. Official 5v5 map guide (treants, dragons, armouries, river shops, teleport boots): https://www.vainglorygame.com/5v5-map
20. Official "Just beginning to Vainglorious: explaining items" series: https://jp.vainglorygame.com/news/just-beginning-to-vainglorious-explaining-items-part-1-blue-crystal-items/ and https://cn.vainglorygame.com/news/just-beginning-to-vainglorious-explaining-red-items/ ; TouchArcade shop walkthrough: https://toucharcade.com/?p=165957
21. GameAnalytics (Michail Katkoff), "If Vainglory doesn't make it, no one will": https://www.gameanalytics.com/blog/vainglory-doesnt-make-one-will
22. Official "Vainglory's game modes in 4.0": https://en.vainglorygame.com/news/vainglorys-game-modes-4-0/ ; Blitz beta announcement: https://id.vainglorygame.com/vainglorylive/update-2-1-preview-announcing-blitz-mode-beta/ ; MMOs.com on Battle Royale in 1.17: https://mmos.com/news/vainglory-adds-new-lane-only-battle-royale-mode-and-more-in-update-1-17
23. TouchArcade on the skin revamp: https://toucharcade.com/?p=228903 ; 2.7.0 Talent cost cut and refund (APKMirror): https://apkmirror.com/apk/super-evil-megacorp/vainglory/vainglory-2-7-0-57856-release ; PhoneArena on Talents and Double Glory: https://www.phonearena.com/news/Vainglory-esports-title-adds-new-hero-additional-skins-and-in-game-events-in-latest-update_id93614
24. Game Developer, "5 monetization optimizations for Vainglory": https://www.gamedeveloper.com/business/5-monetization-optimizations-for-vainglory
25. Kotaku, "My favorite MOBA so far is a mobile game": https://kotaku.com/my-favorite-moba-so-far-is-a-mobile-game-1664922015
26. TechCrunch, 2014 hands-on on responsiveness: https://techcrunch.com/2014/10/30/in-the-best-conditions-ios-exclusive-vainglory-is-a-top-notch-moba
27. GamingonPhone, 2025 mobile MOBA revenue comparison: https://x.com/GamingonPhone/status/2004891542922899945 (single-source figures, unaudited)
28. Dataintelo, mobile MOBA market 2025 to 2034 (regional split): https://dataintelo.com/report/global-mobile-moba-games-market
29. Udonis, mobile MOBA market trends: https://www.blog.udonis.co/mobile-marketing/mobile-games/moba-games-market
30. ONE Esports, Wild Rift hands-on (joystick plus pseudo-joystick aim, 15 to 18 minute matches): https://www.oneesports.gg/lol/hands-on-wild-rift-is-the-perfect-league-of-legends-experience-on-mobile/ ; Sportskeeda, Wild Rift vs Mobile Legends: https://www.sportskeeda.com/esports/wild-rift-vs-mobile-legends-which-mobile-moba-fit-playstyle-best
31. Wikipedia, "Pokémon Unite" (10-minute cap, goals): https://en.wikipedia.org/wiki/Pok%C3%A9mon_Unite ; "First thoughts on Pokémon Unite" (auto-lock design): https://tiny.write.as/ftwynn/first-thoughts-on-pokemon-unite ; Brawl Stars control guides: https://lootbar.gg/blog/en/best-brawl-stars-control-settings-and-layout-guide.html
32. Godot forum, built-in multiplayer lacks NAT punch-through, TURN relays: https://forum.godotengine.org/t/multiplayer-in-godot-is-easier-than-you-think-a-comprehensive-beginners-guide/138341 ; GSoC 2018 WebRTC for Godot: https://summerofcode.withgoogle.com/archive/2018/projects/6620865563197440
33. ExpressTURN, Godot 4 WebRTC with STUN/TURN and per-session credentials (vendor documentation): https://www.expressturn.com/docs/godot

Mandible sources: `README.md`, `src/Sim/Lanes.cs`, `src/Game/LaneAI.cs`, `src/UI/LaneHud.cs`, `src/Sim/Config.cs`, `src/Sim/Sim.cs`, `src/Game/GameSession.cs`, `src/Game/PlayerInput.cs`, `src/Net/Lockstep.cs`, `src/Game/Commands.cs`, `src/Main.cs`, `tools/lanebalance.ps1` at commit `beac140`, plus the uncommitted armour and hero-card work in the main checkout on 1 November 2026.
