# Art pipeline

Every session that changes how the game looks follows these steps. The rule behind them: a frame is judged against
an approved target picture by a reviewer that did not build it, never against the previous attempt.

## 1. Targets

`docs/art-targets/` holds one approved image per surface: (A) the level-1 opening with the broken tube, (B) the
yard, (C) the level map, (D) the chambers, (E) the food and (F) the fog of war. The owner's own references are in
`owner/`. A new surface needs a new target, and the owner approves it before anything is built.
`docs/art-targets/README.md` records how each target was made.

## 2. Assets, not primitives

Natural objects come from real scans, not from code shapes (no MeshKit ellipsoids, tubes or noise lumps for stones,
food, twigs or leaves).

- Source: CC0 sets from Poly Haven or ambientCG. Add a row to `docs/asset-credits.md` for each one.
- Meshes: `tools/asset-process.gd` cuts each scan to a phone budget (stones 400-900 triangles, rocks 500-1800,
  food 600-2000), centres each piece on its footprint with its base at y = 0, adds distance LODs and writes
  `assets/nature/meshes/*.res` plus `pieces.json` (real size in metres, kind).
  ```
  godot --headless --path . --script res://tools/asset-process.gd -- <downloads> <out> <manifest.json>
  ```
- Textures: 512 px for small objects and 1024 px for rocks; ground sets are 1024 px (2048 px for the base). Each
  `.import` file is set to VRAM compression (`compress/mode=2`, mipmaps on, `normal_map=1` for `_nor`). Phones get
  ETC2/ASTC and desktops S3TC.
- Hand-made models (the lab tube, crumbs, seeds) are made in Blender and exported as glTF. They follow the same
  budgets and the same folder layout.

## 3. Scatter

Stones, debris and grass are placed by `src/World/Scatter.cs` (see its header). Placement follows the terrain
(hollows, slopes, trail edges) and is seeded per map, so every player sees the same yard. One mesh is never repeated
across an area: each cluster draws from several species and sizes, with random rotation, tilt and how deep it sinks.

## 4. Review gate

Every view that has a target is rendered from a fixed camera, set beside its target, and scored by a fresh reviewer.

1. Render the views:
   ```
   bash D:/MandibleBuilds/godot-run.sh 400 --path . --audio-driver Dummy -- --art-capture=<frames-dir>
   ```
   This also checks by pixel sampling that unexplored ground is black and the trail is not.
2. Pair each frame with its target: `python -I tools/art-gate.py <frames-dir> <gate-dir>`. This writes one image
   per view (target left, frame right) and `review.md`, which holds the reviewer prompt for each pair.
3. For each pair, spawn a **fresh** reviewer agent (the Agent tool, general-purpose). Give it only the prompt from
   `review.md`: it opens only that one image and must not read code, notes or earlier reviews. It scores the frame
   1-5 on repetition, primitive shapes, scale, material realism, light, palette and readability, and returns JSON.
4. A view passes when every score is 4 or 5 and the reviewer lists no blockers. Only passing frames go to the owner.
   A failing view goes back to step 2 or 3 of this document, guided by the reviewer's `fix_first`. Record the scores
   in the PR.

The reviewer prompt asks for the target's standard at the game camera's framing, not a pixel match. Do not soften
the prompt to make a frame pass.

Baseline, 2026-11-02, before this pipeline: the yard scored 2 on repetition, primitives, scale, material, palette
and readability, and 3 on light. The fog of war scored 2-3 on every criterion. Both failed.

## 5. Budgets

- The Low tier must stay fast on phones. `--art-capture` prints fps per view and `a-opening-low` is the Low tier.
- `tools/capture.ps1` must still pass all 12 self-tests.
- Always run Godot with `--audio-driver Dummy`. Run graphical jobs through `D:/MandibleBuilds/godot-run.sh` and
  headless jobs under `timeout`.

## Gate log

**2026-11-02, after the first implementation pass (commit ae906ac). All ten views fail.** Each score is out of 5, in this order: repetition, primitives, scale, material, light, palette, readability.

| View | Scores | What the reviewer would fix first |
|---|---|---|
| a-opening | 3 3 3 3 4 4 2 | make the tube read as broken, and the ant trail readable at this size; add soil grain and litter |
| a-tube-close | 3 3 3 3 4 4 4 | soil grain and litter; glass refraction and highlights |
| b-yard | 3 3 3 2 3 4 2 | grainy, pebbly soil; denser stones and debris; a visible ant trail |
| c-level-map | 3 3 3 4 4 4 3 | vary the chamber shapes; legible labels; lighter lower chambers |
| d-chamber | 2 2 3 2 2 3 3 | carved cavities with relief; varied, translucent brood (the egg ring repeats) |
| e-food-crumb | 3 2 2 3 4 4 1 | a dense pile of small, varied crumbs at this framing, with ants on it |
| e-food-seed | 3 3 2 3 3 4 1 | a bigger, denser pile at this framing, with ants on it |
| e-food-sugar | 3 3 3 3 4 3 1 | make the source visible (grass hides it); glistening crystals with ants on them |
| e-food-berry | 3 2 3 2 4 3 2 | glossy, translucent berries in clear view with ants on them |
| f-fog-of-war | 3 3 3 3 4 4 2 | readable ants; grainier soil; a softer edge |

Common causes, to fix next:
- The capture camera's depth-of-field blur and the 34-unit base tile flatten the soil grain. Next: a finer second octave of the ground set and less depth-of-field blur on the ground plane.
- The capture framing for food and the yard is too far out and has no ants in it. Next: frame each food source closer, after letting a few workers reach it.
- The chamber view (the nest interior, `NestInterior`) still uses the older brood and wall geometry. It needs its own pass.
