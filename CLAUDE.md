# Mandible: notes for Claude sessions

Ant-war RTS, Godot 4.7.2 .NET (C#), public repo github.com/zaferdajani/Mandible (owner's choice, 2026-11-10), checkout D:\Mandible.
Releases live in this repo only; the updater reads its latest release.
Read this instead of re-surveying the codebase. Keep it short and current.

## Token budget (owner's rule)
- Small, targeted changes. Do not re-read whole files or re-survey systems this file already describes.
- While iterating, run only the test for what changed (see below). The full suite, the 4-platform build and the
  GitHub release happen once per release batch, not per change.
- No screenshot or art-review loops unless the change is visual. New visual objects still need an approved target
  (docs/art-pipeline.md).
- One feature per session; start a fresh session for a new feature rather than growing a long one.

## Build and test (always muted: --audio-driver Dummy)
- Godot: `G=/d/Tools/Godot/Godot_v4.7.2-stable_mono_win64/Godot_v4.7.2-stable_mono_win64_console.exe`
- Compile check: `dotnet build -nologo -v q`. Godot does NOT rebuild C#: always dotnet build before a Godot run.
- Headless runs always under `timeout`. Windowed runs: `bash D:/MandibleBuilds/godot-run.sh <secs> <godot args>` (GPU queue).
- Targeted campaign: `MANDIBLE_LEVELS=<ids|mutation|flight|ep2|chain|checks> timeout 900 $G --headless --audio-driver Dummy --path . -- --campaign-test`
  (level ids, not numbers; `MANDIBLE_CAMPAIGN_TRACE=1` prints progress). Full campaign is ~25 min: release only.
- Others: `--savetest` (GPU queue), `--transport-test`, `--carrion-test`, `--generaltest`, two-process `--nettest=host,120` + `--nettest=join,120`,
  screenshot tour `--capture=<dir>` (12 self-tests). Launch timing: `--startup-probe [--still-menu] [--probe-shot=path@frame]`.
- Godot import rewrites many `.import` files with LF endings only: discard them (`git checkout -- '*.import'`), never commit.

## Release recipe (only when the owner asks)
Platforms for now (owner, 2026-11-13): **Windows and iOS only**; skip macOS and Android.
Bump `src/Game/Updater.cs` Version, commit, tag vX, push main + tag; run `ios-build.yml` (and `macos-build.yml` only if the owner asks again)
(`/d/Tools/gh/bin/gh.exe workflow run ... --ref vX`; private repo: macOS minutes count 10x against the free Actions quota); `git worktree add D:/MandibleBuilds/src-X vX`, import, export
"Windows Desktop" and "Android" (JAVA_HOME=D:\Tools\jdk17, keystore env vars, see past releases). If export says
"Failed to build project", create `D:/GodotData/mono/build_logs/<hash>_ExportRelease` (hash from a --verbose export).
Assets must be named Mandible-Windows.zip / Mandible-macOS.zip / Mandible-Android.apk / Mandible-iOS-unsigned.ipa
(the updater looks for them). Publish with `gh release create vX -R zaferdajani/Mandible ...`. Then `powershell -File tools/updatetest.ps1`. End every reply with the release link.

## Architecture in one screen
- Deterministic 30 Hz lockstep sim (`src/Sim`, DMath only, no Mathf trig in sim). Player actions go through `Cmd`
  (`src/Game/Commands.cs`) so saves (order log replay) and multiplayer work. Changing sim state layout: add the field
  to the checksum in Sim.cs and bump `Version` in `src/Net/Lockstep.cs`.
- Ants are `struct Ant` in `Sim.Ants[]`; movement `Sim.Move` = look-ahead steering around circular `Obstacle`s
  (`World.Obstacles`, `W.Blocked`), no pathfinding. No water exists anywhere yet.
- Campaign: `Levels.cs` (setup + objectives per level id, `Levels.Order` maps ids to numbers), `Expedition` objectives
  and steps, `CampaignColony.cs` (persistent roster, veterans; Burr is tracked by name), `CampaignReveal.cs` (what each
  chapter reveals), `Mutation.cs` (cores: `AddCore`, `GiveCore` for a living keeper, `CmdExtract`, carry home, `TakeCore` mutates the queen at once;
  royal larva/cocoon), `Evolve.cs` (the evolve tree: `Evolve.Nodes`, `CmdKind.Evolve`, `Colony.Evo`, `LevelGeneral`, `Ant.Brood`;
  the queen's level is `Upgrade.Queen`, paid in organic matter), `src/UI/EvolveTree.cs` (the tree screen, key P, N1's look).
  PROGRESSION RULES ARE IN `docs/progression.md`; how each chapter uses them (core challenges, the rival's race for a core)
  is `docs/level-design.md` section 5. Mutation units: winged ants = `AntType.Flyer`, stingers = flyers with `Mutation.Stinger`,
  `WebAnt`, `Armoured`. Their merged bodies (targets P1-P4) are built by `tools/blender/ant_models.py` `merged()` on the castes' scans
  (`assets/ants/*_{Flyer,Stinger,WebAnt,Armoured}_*.res`; the stinger body is drawn for each winged ant with `Ant.Sting`, `AntRenderer.BatchOf`; the live portrait too, `Subject.Sting`; a dead stinger still lies as a winged ant);
  the borrowed anatomy is marked per vertex in CUSTOM0.y (silk, shell, bands, velvet, fly eye) and coloured by the Ant shader. Chapter 5's fly: `MutagenProps.IsFly`,
  `assets/mutation/fly`, `fly_hub`. Build record: `docs/art-targets/README-mutation-units.md`. Mutation VFX: `src/Render/MutationBurst.cs` (burst, cracks, sky beam), sounds `mutate`/`mutate_queen` (`tools/gen_audio.py mutate`).
- Body parts (`Parts.cs`, progression.md 1c): every mutation ant hatched costs one part of its insect, so a brood costs its squad size
  (`Sim.PartCount`): `Part` Wings/Sting/Gland/Plates, `Colony.Parts[]` (in the roster, format `p3`), `BodyParts.Needs(ProdKind, mutations)`
  (a winged ant always costs wings). Stingers are per ant (`Ant.Sting`, not the colony's flag): with the wasp's core a brood of winged ants
  also takes up to one sting per ant from the store (`StingsFor`, `ProdItem.StingN`) and that many hatch as stingers; none in store never blocks it. A dead wasp/spider/beetle keeps its part in `FoodSource.Part`; the first
  worker to cut a load takes it (`Ant.PartP1`), stored when she delivers. `CanEnqueue` -> `QueueResult.NoPart`, `CanHatchSilk` -> `SilkResult.NoPart`.
  Campaign only: a carcass that crumbles with its part still in it leaves a **husk** (`FoodSource.Husk`: a separate one-load body that never
  rots; foragers and eaters ignore it, so old levels play as before), and `PartErrands` sends foragers for parts only while the objective is
  a hatch that is short of them. There is no fly creature: wings come from dead flies a spider caught (`AddRemains`, `AddLarder`; they use
  the wasp's carcass until the fly has a body). Skirmish: `SeedSkirmishCores` lays 4 flies by each fly core and in the 2 spider lairs
  nearest each nest; `Recruit` fetches a husk (one worker, no dragging) when the queen uses its part and no hunter is within 24. The Dry Spell tips over if ordinary foraging changes at all: keep husks invisible to it.
- Silk above ground (`Spinnery.cs`, owner's design 2026-11-15): a colony whose queen has the spider's core (`Mutation.Web`, `Sim.HasSilk`)
  builds on the surface from **straw** (`Colony.Straw`; `FoodKind.Straw` piles seeded round each mound by `SeedStraw`, fetched by foragers while
  `WantsStraw`, never counted as food). Building: a site is marked out; the nearest worker walks in and becomes its weaver for good (`Ant.WeaveP1`, `SilkWork.WeaverIn`, removed from the colony), and straw carriers deliver straight to the site (`DeliveryFor`, `SilkWork.Got/Need`); she weaves only as fast as straw arrives. The store by the mound (`Colony.Straw`, cap 60) pays for improving posts. `Sim.Works` (cap 2 + `SilkRank` = 1 + the Spinneret node, placed within `SilkReach` of the mound):
  **Spinnery** = the web ants' building: it raises web ants (`AntType.WebAnt`, the same caste the queen lays: all pin flyers with `ThrowWeb`;
  those raised at the Spinnery carry `Ant.Silk` and also snare ground enemies with `ThrowSnare`); **Web Post** =
  defence, keeps one trap web and is improved once into a **Web Tower** (flings webs at ground and air in reach) or a
  **Trap Nest** (three trap webs, re-aimed). Orders: `CmdKind.Spin` (Arg kind, P), `SilkUp`, `SilkHatch`, `Trap`.
  `Sim.Traps`: root 3 s, +30% bites (`Ant.Snared`), 3 catches, respun; majors rip them, acid burns them, Silk Cutters
  halve the hold. Red has silk once the story gives it the spider's core; the AI builds all of it (`AIController.Silk`). Player UI: the Silk panel
  in `ColonyCommandHud` (keys K, comma, period: P and M belong to the evolve tree and armoured ants), placement ghost and reach ring in `SilkWorks`.
- Silk visuals: `src/Render/SilkWorks.cs` draws every web and structure from `assets/silk/silk.gltf`
  (`tools/blender/silk_models.py`; targets S1-S3, `docs/art-targets/README-silk.md`; art review not passed yet). Frames: `--silk-capture=<dir>` (add `--mobile` for the HUD views).
- How creatures step and turn is render only (the sim's `Heading`/`Gait` are untouched): `AntRenderer.TurnOnLegs` draws every body turning no
  faster than its legs can step (`TurnRate` per type; flyers bank instead), `Stride` advances a render-side gait with distance AND angle turned,
  and the Ant shader's leg branch plants each foot against the body's travel and turn. Foot rest positions come from each mesh
  (`AntMesh.LegsOf`); shader uniforms `legs`/`leg_count`/`stride`/`turn_arc`; turn share in INSTANCE_CUSTOM.x (64s), travel direction in .w (2048s).
  Check: `--gait-capture=<dir>` on the GPU queue (all 15 creatures: turn sheets + a planted-foot self-test; `MANDIBLE_GAIT_ONLY=Spider,Worker`).
- Creature voices: `tools/gen_voices.py`, `AudioDirector.Voice/VoiceOf/VoiceRank`, `PlayerInput.Speak` (web ants use the "silk" call, armoured ants borrow the major's).
  Every ant speaks at its own pitch (`PlayerInput.PitchOf`). The queen answers in words: `tools/voice/queen_barks.py` cuts `voc_queen_*`/`ack_queen_*` from her approved takes.
- Live portrait (`src/UI/LivePortrait.cs`): the selection panel draws the real model of the highest-ranking selected creature (else the inspected one, else the queen) in one 208x256 viewport over the still card; per-creature rigs in `RigOf`; `ColonyCommandHud.RefreshLive` feeds it health, bite, hit flash, lost parts and calls. `--portrait-capture=<dir>` (sheet, panel shots, self-tests, on/off `[perf]`; `--no-perf` skips the timing), `--no-live-portrait` turns it off.
- Level 1 (The First Nest) is the confined founding lesson: `Expedition.cs` stage machine (Alone → Gather → ClearTrail → FreeQueen → EscortQueen →
  Provision → DigEntrance → RoyalChamber → Incubator → Store → RaiseWorkers → Bridge → Fragment → Complete). Walkable area is
  `Mission.Constrain` (trail x 40-144, y 250-262, clearing radius 27 at (144,256), the path east to the pond at
  (200,256), its shore and island; the water opens only once the bridge is built). The opening (`TickOpening`): old Burr wakes alone at
  `Expedition.Wake`; his 5 soldiers lie stunned along the alley (`Ant.Dormant`: no orders, no sight, untargetable, not in
  `Colony.Count`) and wake when an awake ant comes within `WakeRadius`; the alley west of `GateX` is closed until all are
  woken. The queen and the workers are alive INSIDE the tube (`InTube`, also Dormant; nothing lies on them, owner's rule);
  its mouth is stopped with cotton and sand (`QueenTrap.cs`) and is dug open by every awake ant within `DigRadius` of `DigSite` (`Dug`, `DigRate`).
  `Sim.SkipOpening()` for tests and captures that start later. Bridge/Fragment is the teamwork
  lesson in `Bridge.cs`: a straw needs 2+ workers (`CmdKind.Straw`, `Ant.StrawP1`), 5 laid make the bridge, then a
  story-only gland (Units 0) from the dead queen is carried home. UI: `ExpeditionGuide.cs` (objective card + guide arrows), `FoundingPanel.cs` (top panel with bars).
- Combat scale: per-type attack/defence/HP with per-type growth per level (`Config.GrowthOf`, `DefenceAt`); table in
  `docs/combat-scale.md` (regenerate when numbers change). Changing it needs the full campaign test.
- Group hauling of carcasses: `Carrion.cs` (`Haulers`, `Config.HaulNeed`) is the model for multi-ant carrying. Level 1's founders drag the
  beetle whole to the clearing (`FounderDrag`); it is stored when it arrives.
- Fog of war is one screen pass that rebuilds world positions from depth (`FogOverlay.cs`). Anything drawn in the
  transparent pass (the build-view roof, `Shaders.TerrainRoof`) is not in that depth: the pass projects such pixels to the ground.
- Startup/menu: `Main.cs` (`Swap` builds sessions behind `LoadingCurtain`; phones use a still menu backdrop),
  graphics tiers and render scale in `Settings.cs`, applied in `GameSession.ApplyQuality`.

## Story rules (owner)
Real ant behaviour, macro nature-documentary realism, warm palette, no grey, no code-primitive art. Gradual reveal
(red colony from Act II). Generals (never "heroes" in player text). Old Burr goes into the royal cocoon and becomes
the Warlord (Hemlock only if Burr fell first). Ground ants cannot hit airborne wasps. Autosave only at milestones.
Progression: `docs/progression.md` is the only rule (core -> new unit; organic matter -> levels via the evolve tree).
