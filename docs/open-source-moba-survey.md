# Open-source MOBA codebases: what is worth borrowing for Mandible

*Survey, 1 November 2026. Companion to [moba-plan.md](moba-plan.md).*

## The short answer

There is no open-source MOBA that Mandible should adopt wholesale. Mandible's core, a deterministic 30 Hz lockstep simulation in C# over Godot with its own generated art, is already further along than every open project surveyed below on the two things that are hardest to retrofit (cross-platform determinism and an original, finished presentation). What the open projects do offer is **reference material**: data-driven ability and item schemas, jungle and objective rules, bot behaviour, mobile control layouts and server-side career services. The recommendation is to keep Mandible's engine and borrow designs, not code, with two exceptions noted under "Code worth reading".

## Projects surveyed

| Project | Stack | What it has | Licence | Health | Verdict for Mandible |
|---|---|---|---|---|---|
| **Open Moba (omoba-bevy)** | Rust, Bevy; authoritative UDP server; PostgreSQL career API | Lane minions, towers, respawning jungle camps, items, five hero classes (warrior, mage, ranger, cleric, warden/jungler), server bots that fill seats and hand over to humans, parties vs bots, touch joystick + right-thumb attack button + directional targeting on phones, iOS/Android build tooling, EN/zh localisation, balance and map-customisation guides | Server and combat: AGPL-3.0; client: MPL-2.0; docs/art: CC-BY-4.0 | 420 commits, 8 stars; active but tiny | **Best design reference.** Its bots-fill-seats-and-humans-take-over model is exactly Mandible's "3v3 with AI allies" plan; its phone layout matches the plan's Thumbstick mode. Its code is Rust and its netcode is server-authoritative, so nothing ports; read its balance docs and camp rules. AGPL on combat code means never copying logic verbatim. |
| **MOBA_CSharp_Unity** | C#; Unity client; standalone Mono/Linux server; ENet-CSharp, MessagePack, SharpNav, VelcroPhysics, CSV/YAML data | Scriptable heroes, skills, buffs and items; navmesh pathing; team-shared sight and bushes; stats in CSV | MIT | 5 commits, ~300 stars, dormant since 2019 | **Code worth reading** for its skill/buff/item scripting shape, which is C# and MIT. Its physics and pathing are irrelevant (Mandible has its own). No lanes, towers or matchmaking. |
| **Gielinor Legends / "Wilderness Rift" (redgouf)** | Godot 4.7.2, GDScript | 5v5 three-lane replica of a well-known map with river, brush, jungle camps with buffs, two boss pits, towers, inhibitor-style altars and a core; 11 heroes with passive + four abilities; 70+ items with build paths and actives; fog of war; bots for all seats; NPC-vs-NPC spectator; JSON data; headless tests and a release self-updater | No code licence stated; non-commercial; uses a third party's IP and CC BY-NC-SA icons | 41 commits, 0 stars | Useful only as a **Godot 4.7 architecture reference** (JSON-driven champions and items, headless self-play smoke test, map builder). Its content is someone else's IP and the licence is unclear, so nothing may be reused. |
| **shotcaller-godot** | Godot, GDScript | 2D top-down 1v1 MOBA where the player gives high-level orders to autonomous units | MIT (per listing) | ~261 stars, last update Oct 2023 | Interesting **design cousin**: a MOBA where units are autonomous and the player is the shot-caller, which is close to Mandible's RTS roots and to the Colony War hybrid. Code is 2D GDScript; nothing ports. |
| **LeagueSandbox GameServer** and forks | C# server emulator for a commercial MOBA's client | Full hero, spell, item, minion, turret and inhibitor logic; packet protocol | AGPL-3.0; the original repository is closed to contributions; it reimplements a commercial game's rules | Forks alive; legal position contested | **Do not use.** Copying its rules would import both AGPL obligations and another company's game design. It can inform how a C# MOBA structures spell scripts, nothing more. |
| **Lanefall (k1m1hasgithub/moba)**, **Heroes Arena (Kotlin)**, **Honor MOBA (Godot 4 prototype)** | Web / Android / Godot | Browser 5v5 with 10 heroes and three-tier crafting; an Android 5v5; a Godot 3D three-lane whitebox | Mixed or unstated | Hobby scale | Nothing Mandible lacks. |
| **Eul's original Defense of the Ancients map** | Warcraft III map script | The genre's origin; released publicly by its author | Public release of the map script only | Historical | The lineage was later acquired by Valve; treat all Dota-derived rules as design history, not as reusable assets. |

Vainglory itself has no public source: Super Evil Megacorp's GitHub holds only API documentation, the engine is proprietary, and the Community Edition shipped as binaries.

## Code worth reading (not copying)

1. **MOBA_CSharp_Unity's skill and buff scripting** (MIT, C#). The pattern of a data row per skill plus a small C# behaviour class per effect kind (damage, heal, root, buff) maps directly onto the ability-system prerequisite in the plan (section 3.2), where Mandible's heroes grow from one auto-cast ability to two or three with cast kinds. Mandible should implement its own version against `Ant` and `Sim.HeroAbility`, keeping everything deterministic (no floating-point order dependence across platforms).
2. **Open Moba's guides on balance tuning, map customisation and camp pacing** (CC-BY-4.0 docs). These are the best written-down numbers for camp respawn, tower damage ramps and bot aggression among the open projects. Read for pacing ideas when tuning the Old Stag, lairs and the aphid pasture; cite the guide if a number is taken.

## What none of them solve, and Mandible already has

- Deterministic lockstep verified across Windows x64, macOS ARM64 and Android, with adaptive input delay and periodic state checksums.
- Mobile builds on both stores' platforms with rotation and touch gestures.
- An original setting whose objectives come from real biology (aphid farming, trap-jaw leaps, honeypot repletes), which no licensed-IP or generic-fantasy project can offer.
- A headless balance harness (`--lanetest`, `tools/lanebalance.ps1`) and a scripted screenshot and input self-test.

## How to "reach Vainglory level"

Not by importing a codebase. Vainglory's quality came from (1) controls designed for touch first, (2) a compact format with a living jungle and one decisive boss, (3) best-in-class hero balance and readability, (4) a fair economy, and (5) presentation. Items 1 to 4 are the P0 roadmap in `moba-plan.md`; item 5 is the same art and audio bar the nest work in v0.6.0 already met, applied to every new object. The open projects above shorten the design work for 2 and 3 and confirm the control layout for 1; the rest is Mandible's own work.

## Sources

- Open Moba: https://github.com/o-moba/omoba-bevy
- MOBA_CSharp_Unity: https://github.com/yasgamesdev/MOBA_CSharp_Unity
- Gielinor Legends (listed as MOBA-FANTASY-GAME): https://github.com/redgouf/MOBA-FANTASY-GAME
- shotcaller-godot (via LibHunt's MOBA topic): https://www.libhunt.com/topic/moba
- LeagueSandbox GameServer forks and licence: https://github.com/Dokman/GameServer , https://gitee.com/chtd/GameServer , https://github.com/games/GameServer
- Lanefall: https://github.com/k1m1hasgithub/moba ; Heroes Arena: https://github.com/ferikurniawan75/Heroes-Arena ; Honor MOBA: https://github.com/wdd143/Honor-MOBA-Project
- Vainglory public repositories (API docs only): https://github.com/SuperEvilMegacorp/vainglory-assets
- Defense of the Ancients history: https://en.wikipedia.org/wiki/Defense_of_the_Ancients ; https://liquipedia.net/dota2/Dota_History/Part_1
