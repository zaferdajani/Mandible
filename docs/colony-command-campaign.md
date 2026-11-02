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

Episodes 2-4 and saved campaign progress are implemented (see below). Dedicated egg transport/hatching animation is handled separately. Test long matches, defeat/restart cases, multiplayer with this branch on both peers, touch input on a real phone or tablet, all graphics tiers and export builds before release. On computers the HUD scales the reference composition as one; phones and tablets (landscape only) use a touch layout with the panels pinned to the screen edges.

GitHub reads worked, but the connector reported unavailable after reconnecting and local push authentication failed with Windows credential access denied. This work is handed off as a local branch, a Git bundle, source ZIP, logs and actual screenshots. No remote branch, PR merge or release is claimed.

## Episodes 2-4 and saved progress

Rules live in `src/Sim/Campaign.cs` (episode text, objectives, gates, raids, the herd) on the shared `Expedition` mission object; Episode 1 keeps its `ExpeditionStage` flow. Episodes 2-4 play on the full Colony War yard (seed 20261031) and start from `Sim.BeginEpisode(n)` right after construction.

Design rule: the victory in every episode with a rival is killing the enemy queen in her royal chamber (`Kill` sets the winner as in Colony War); the other objectives are ordered steps toward it, shown under a permanent **Main goal** line, and killing her early still wins (the end card counts the steps finished). Losing your own queen always loses. Episode 1 keeps its founding ending and foreshadows the red queen.

| Episode | Start | Steps (in order) | Lose |
| --- | --- | --- | --- |
| 2 The Foraging Season | royal chamber + 1 incubator, 12 workers, 12 soldiers; a young red colony (1 incubator, 6 soldiers, passive AI) | dig a store; kill the herd's ground beetle and keep 3 ants at the clover stem for 45 s (+250 food, then honeydew while 2 ants tend it); forage 800 food; survive a 4-bug lair raid (`Ant.Raider`: wild bugs that enter the nest for the queen); **kill the young red queen** | queen dies |
| 3 The Rival Colony | 2 incubators + store, 18 workers, 12 soldiers; red colony with a passive AI | scout within 45 of the red mound; two red raids (11 and 16 ants) after the scout is seen; research Matriarch, Forge I or Armory I; destroy a 3-guardian outpost placed clear of wild lairs; **kill the red queen** | queen dies |
| 4 The Nuptial Flight | Matriarch queen, 30 workers, 24 soldiers, 3 majors; red colony with mound guardians, passive AI and 4 scripted raids | raise a hero; beat the Old Stag (the stag lord, made a level-2 boss) so it joins you; break the red mound guardians (their queen is shielded while they stand, the Lane War siege rule); **kill the red queen** | queen dies, or the princesses fly at 30:00 |

Unlocks grow per episode (`Expedition.AllowsProd/AllowsChamber/AllowsResearch`): 1-2 workers and soldiers, brood and storage; 3 adds majors, royal jelly up to Matriarch, Forge I and Armory I; 4 opens everything. In Episodes 2-4 the wild lairs along the nest-to-nest line are cleared (the foragers' highway; the finale keeps the Old Stag). The rival AI (`AIController.CampaignRival`) is passive with a lean food multiplier; the episode script decides its raids, which stop once the finale's guardians fall.

Progress: `src/Game/CampaignProgress.cs` stores completed episodes in `user://campaign.cfg` (same ConfigFile pattern as `Settings`). An episode is unlocked when the previous one is complete; Continue starts the first unlocked, unfinished one. Winning an episode records it; the end card offers the next episode. `--episode=N` starts an episode directly.

Shared sim changes (protocol bumped to `mandible-mp-9`): `Ant.Raider`, `RaidNest` and layering for raiders, `QueenShielded` honouring the finale, `SpawnGuardian` returning its id, campaign scaling of guardian and boss strength, a campaign branch in `Rouse`, and `SpatialHash` starting empty (a renderer reading it before the first tick looped forever on zeroed heads; this froze Episodes 2-4 behind the paused briefing).

### Validation (this branch)

- `--campaign-test`: Episode 1 as before, then scripted players finish every step and win Episodes 2, 3 and 4 by killing the red queen in her chamber (317 s, 319 s, 285 s simulated; asserted through `!Col[1].Alive`), an idle finale loses to the nuptial flight, and progress is saved, reloaded and unlocked in order (scratch file). Set `MANDIBLE_CAMPAIGN_TRACE=1` for a periodic trace while tuning.
- `--command-capture`: adds the campaign list (`06`), Episode 2-4 briefings (`07-09`) and objective panels (`10-12`). One of five runs failed the pre-existing real-mouse room-placement step; the other four passed.
- `tools/capture.ps1`: all 12 self-tests pass. `--nettest=host,240` / `--nettest=join:127.0.0.1,240`: 241 checks, 0 desyncs (in some runs the guest reaches 4:00 first and closes, so the host log ends at its 3-minute line, still 0 desyncs).
- The scripted times show the objectives are reachable; they are not human playtests. Balance for human play (especially the finale's assault on the red queen) still needs playtesting.
