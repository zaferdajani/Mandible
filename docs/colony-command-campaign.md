# Colony Command and The First Nest

Integration work on `codex/colony-command-campaign`, based on PR #1's tunnel implementation at `3f477a8e8ea9398b72324086412f02e4cd50d497`. The main baseline was `158911b16ba1359b67d0500881204162451f3acb`. This branch includes PR #1; do not apply its changes twice.

## Implemented behavior

- Colony War, Skirmish and Episode 1 use a live reference-layout command HUD: top resource bar, chamber list, minimap, selection details, Ants/Chambers/Research/Heroes tabs, production queue and command buttons. Lane War retains its existing interface. Portraits use atlas regions of the owner's original reference, preserved in `assets/ui/colony-reference.png`. The world is rendered from game geometry, not from the reference image.
- Seven chamber identities: royal egg ring and amber jelly; separated brood beds; seeds/berries/leaves in storage; winged ants and root perches; dark mandibles/resin; blue shell plates; stretched silk and bundles. These are procedural props. Royal, brood and storage contents do not represent individually simulated egg-carrying workers.
- Episode 1 starts with the queen, ten workers and six soldiers. Three enemy soldiers guard a confined dirt/grass trail. The queen follows the attackers after the patrol is defeated; workers follow her. At the food clearing, selected nearby workers can order the entrance and royal chamber.
- The queen walks underground. Workers visit the royal chamber, then dig and forage. The player builds an incubator, then storage. Four newly hatched workers and six newly hatched soldiers complete the episode. No research or advanced caste can bypass the episode gates through a hotkey. There is no enemy-colony victory condition in this lesson.
- Every completed incubator adds 50 population capacity up to the 460 limit; further construction is rejected at that ceiling. Brood production requires a built incubator and the queen inside the royal chamber. A second incubator is supported. The default placement search checks separation and tunnel connectivity; mouse placement uses the same underground plane as picking.
- Skirmish starts both colonies with all research tiers and seven chamber types unlocked; all five ordinary castes and three hero types can be queued. Food costs, population limits and queen-kill victory still apply.

## Main files

| Files | Responsibility |
| --- | --- |
| `src/UI/ColonyCommandHud.cs`, `Hud.cs` | Live menus, reference atlas, queue, commands, selection and chamber labels |
| `src/Sim/Expedition.cs`, `src/UI/ExpeditionGuide.cs`, `src/World/ExpeditionWorld.cs` | Mission rules, instructions, trail and food clearing |
| `src/Sim/Nest.cs`, `Sim.cs` | Incubators, construction, queen production gate, default placement |
| `src/Render/ChamberIdentity.cs`, `NestInterior.cs`, `NestGeometry.cs`, `Shaders.cs`, `AntRenderer.cs` | Chamber props, enlarged connected floors, roof and material adjustments |
| `src/Game/GameSession.cs`, `PlayerInput.cs`, `src/Main.cs` | Mode entry points, camera, restart and floor placement |
| `src/Game/ColonyValidation.cs`, `src/Capture.cs` | Mission and mouse-driven regression checks; old tutorial capture flag compatibility |

## Validation

Tested on Windows x64 with Godot 4.7.2 .NET, .NET 9.0.22, and a GeForce GTX 1650. Use the complete Godot .NET distribution: `GodotSharp/Api/Debug` must remain beside the executable. An isolated local copy was used after the D: installation failed to load its assemblies in this session. No user-wide runtime settings were changed.

```text
dotnet build --no-restore
godot --headless --audio-driver Dummy --path . -- --campaign-test
godot --headless --audio-driver Dummy --path . -- --nesttest=headless
godot --headless --audio-driver Dummy --path . -- --simtest=90,Normal,Normal
godot --headless --audio-driver Dummy --path . -- --synctest=60
godot --audio-driver Dummy --windowed --resolution 1672x941 --path . -- --gfx=High --command-capture=<absolute-output-directory>
```

`godot` above means the .NET executable; run `dotnet restore` first in a fresh checkout. If using `--log-file`, pass an absolute path on Windows.

The mission regression checks actual simulation ticks and commands, combat, confinement, selected-worker founding, construction order, real food gathering, worker/soldier hatching, victory, additional incubator capacity, the population ceiling, queen-away production pause, and the full skirmish roster. It completes in about 259 simulated seconds including the extra incubator check. The graphical check sends real mouse events to tabs, a worker portrait, worker selection, chamber placement and research start/cancel/refund; captures include the skirmish, research/heroes tabs, surface and episode opening.

Geometry checks cover connected routes, construction, upgrades, caching and empty/full shader capacity. The 90-second AI match and 60-second Colony/Lane War checksum exercises completed. These short checks do not establish campaign balance, full-match stability, multiplayer synchronization or cross-platform determinism. Local final checksums: Colony `4334C4A3`, Lane War `C53B88D9`.

Build warnings remain for pre-existing member hiding/unused variable and unavailable NuGet vulnerability-cache data. Godot graphical shutdowns reported 8–10 leaked ObjectDB instances and 4–5 resources still in use; investigate before release. This is an integration build, not a release.

## Remaining work

The user's requested exact visual match is **not complete**. Compare the supplied reference with the actual captures: room silhouettes and placement, terrain/lighting, ant population and activity within rooms, detailed organic props, panel trim, typography, command icons, hero portraits and minimap composition still differ. Hero cards currently reuse ordinary caste portraits. Do not present the supplied reference as an actual game screenshot or claim pixel-level equivalence.

Only Episode 1 exists. Later missions and progressive unlocks between episodes, save/resume progression, and dedicated egg transport/hatching animation have not been authored. Test long matches, defeat/restart cases, multiplayer with this branch on both peers, mobile/touch/portrait layouts, all graphics tiers and export builds before release. The new HUD currently scales the desktop composition uniformly.

GitHub reads worked, but the connector reported unavailable after reconnecting and local push authentication failed with Windows credential access denied. This work is handed off as a local branch, a Git bundle, source ZIP, logs and actual screenshots. No remote branch, PR merge or release is claimed.
