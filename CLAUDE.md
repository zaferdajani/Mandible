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
Bump `src/Game/Updater.cs` Version, commit, tag vX, push main + tag; run `macos-build.yml` and `ios-build.yml`
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
  chapter reveals), `Mutation.cs` (mutagen glands: `AddMutagen`, `CmdExtract`, carry home, `Mutate`; royal larva/cocoon;
  each gland is a radiating core of `Strength` 1-3 that becomes `Colony.Power`, +hp/bite for every ant, saved in the roster).
  Mutation VFX: `src/Render/MutationBurst.cs` (burst, cracks, sky beam), sounds `mutate`/`mutate_queen` (`tools/gen_audio.py mutate`).
- Level 1 (The First Nest) is the confined founding lesson: `Expedition.cs` stage machine (ClearTrail → EscortQueen →
  Provision → DigEntrance → RoyalChamber → Incubator → Store → RaiseWorkers → Bridge → Fragment → Complete). Walkable area is
  `Mission.Constrain` (trail x 40-144, y 250-262, clearing radius 27 at (144,256), the path east to the pond at
  (200,256), its shore and island; the water opens only once the bridge is built). Bridge/Fragment is the teamwork
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
