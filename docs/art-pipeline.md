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

## 4. Review gate (two parts, the owner's choice of 2026-11-03)

The targets are macro photographs, where an ant fills 5-10% of the frame; at RTS play zoom it fills 1-2%. So each
view is judged twice, by different fresh reviewers:

- **Art check** at a close camera matching the target's framing (`<view>-close.png`, set beside the target):
  repetition, primitives, scale, material, light and palette, each scored 1-5.
- **Readability check** at real play zoom (`<view>.png`, on its own): readability scored 1-5 against a short written
  spec for that view (in `tools/art-gate.py`: can a player tell the food types, the trail, the ants and the fog
  edge apart at a glance?). It is not compared with the macro photo.

Food piles are drawn 1.4x the sim's radius, so they read at play zoom; ants and rocks stay true to scale.

1. Render the views (both framings):
   ```
   bash D:/MandibleBuilds/godot-run.sh 600 --path . --audio-driver Dummy -- --art-capture=<frames-dir>
   ```
   This also checks by pixel sampling that unexplored ground is black and the trail is not.
2. Prepare the checks: `python -I tools/art-gate.py <frames-dir> <gate-dir>`. This writes `<view>-art.jpg`,
   `<view>-read.jpg` and `review.md`, which holds one prompt per check.
3. For each section of `review.md`, spawn a **fresh** reviewer agent and give it only that prompt. It opens only
   the one image and must not read code, notes or earlier reviews. It returns JSON.
4. A view passes when its art check scores 4 or 5 on every criterion with no blockers, and its readability check
   scores 4 or 5. Only passing views go to the owner. Record every round in the gate log below.

Do not soften the prompts or the specs to make a frame pass. Two clarifications were made on 2026-11-03, at the
owner's request after round 6, and are not a softening:
- The art reviewer judges only what is in the frame, against the same kind of thing in the target. It does not mark
  down for content the target has and the frame lacks (an ant column, honey, droplets).
- Ant bodies are scored by the ant session's gate, not here.

The owner is the final judge. Views that pass readability go to the owner on a review sheet labelled with their art
scores, even when the art check has not passed (round 6: `D:/MandibleBuilds/review/art-round6.jpg`).

**Ants are judged by the ant session's work.** The ants are code-built (`src/Render/AntMesh.cs`), and a separate
ant session rebuilds them against its own approved target, with the same two-part gate. Until that lands, an art
review's remarks about the ants themselves (body shape, legs, chitin) are recorded but do not block the terrain,
food, tube, map or chamber views; every other criterion still does. That session can use these as they are:
- `--art-capture` (its close framings use `RtsCamera.PitchOverride` and a pushed-in near plane);
- `tools/art-gate.py` (add the ant views to `VIEWS` with a target and a readability spec);
- `tools/asset-process.gd` (cut real or Blender-made meshes to phone budgets with LODs).

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

**2026-11-03, after pass 2 (DOF off, sand grain octave, grass kept off food, glossy berries, sugar cubes, egg heaps). All ten views fail again, with scores about the same.**

| View | Scores | What the reviewer would fix first |
|---|---|---|
| a-opening | 3 3 3 2 3 3 2 | granular soil and litter; the tube should read as cracked glass; ants visible around it |
| a-tube-close | 3 3 2 3 3 3 4 | grainy soil, litter and grass; ground the tube with contact shadows; scale relative to the ants |
| b-yard | 3 3 2 2 3 4 2 | a high-frequency soil and gravel material; visible ant trails; no flat stick cards |
| c-level-map | 3 3 3 4 4 4 3 | vary the chamber shapes and rims; light the lower chambers |
| d-chamber | 3 2 3 2 2 3 3 | true 3D cavities with grainy walls and directional light; organic, translucent brood |
| e-food-crumb | 3 3 3 3 3 4 1 | a large, clearly modelled crumb pile with ants on it; no log in the foreground |
| e-food-seed | 3 3 2 2 3 3 1 | a large, readable seed cluster with ants on it |
| e-food-sugar | 3 3 3 2 3 4 3 | irregular, translucent sugar chunks with sparkle; ants on them |
| e-food-berry | 3 2 3 3 4 4 2 | irregular glossy berries with stems and a juice pool; ants on them |
| f-fog-of-war | 3 3 3 3 3 4 3 | grainy, pebbly soil; warmer directional light; a softer edge |

The scores have plateaued between pass 1 and pass 2. What keeps failing is not the asset choice but three things:
1. **Readability at game zoom.** At play distance an ant is 1-2% of the frame; in the macro targets it is 5-10%. Every food and fog view loses its readability score on this. Either the gate judges material at the target's framing (a close camera) and readability separately at game zoom, or the food piles and ants have to be scaled up beyond life-size.
2. **Soil reads smooth at 40+ units.** A 1-2k texture tiled at real scale cannot show grain at that distance on a 1600-px frame. Parallax or displaced geometry, or bigger stones in the near ground, would show it.
3. **Chambers.** The nest interior is still the old top-down cut with an outline band. It needs a real rebuild of the cavity walls and lighting (the NestInterior and Earth shader), not a texture change.

**2026-11-03, round 3: the first run of the two-part gate (commit 53dc1ee).** Art scores, out of 5: repetition, primitives, scale, material, light, palette. Readability is out of 5.

| View | Art | Readability |
|---|---|---|
| a-opening | 3 2 2 2 3 3 fail | 3 fail: the queen does not read larger than the workers; no trail line |
| b-yard | 3 1 2 2 3 4 fail (a near-lens shard) | 2 fail: no ant trail; sticks and stones blend into the soil |
| c-level-map | 3 3 3 3 3 4 fail | 3 fail: locked chambers and names too dark; no selection highlight |
| d-chamber | 2 2 3 2 2 3 fail | **4 pass** |
| e-food-crumb | 3 2 2 3 3 4 fail | 1 fail: the crumbs read as stones |
| e-food-seed | 2 2 3 2 3 3 fail | **4 pass** |
| e-food-sugar | 3 2 3 2 3 3 fail | **5 pass** |
| e-food-berry | 2 2 2 2 3 2 fail | **5 pass** |
| f-fog-of-war | 3 3 2 3 3 4 fail | 3 fail: dimmed and clear ground too alike |

Readability now passes for four views. Every art check fails. Two findings recur in almost every art review and are bigger than this pipeline's assets:
- **The ants themselves read as primitives** (ellipsoid bodies, stick legs). They are code-built in `AntMesh.cs`. The graphics council plan (`graphics-council.md` in the shared notes) covers them. The art check cannot pass while the subject of every frame is a primitive.
- **Soil material at close range:** one tiled 1-2k photo set cannot match a macro photograph at ant framing. It needs a macro soil layer (photoscanned pebbles and crumb as a decal or displacement) close to the camera.

After round 3: the chamber cavities are rebuilt (a deeper excavation at 3.0, steep scalloped banks on a finer mesh, a soft shade over the lip instead of the outline band, and a warm unshadowed light low in each room on High and Ultra; 42 fps). The berry cut that showed an empty shell is removed.

## Lighting (merged from PR #11, 2026-11-03)

PR #11 (`claude/lighting-tunnels-leaves`) is merged into this branch. The lighting-audit checklist is in `.claude/skills/lighting-audit/SKILL.md`. The underground has its own light:
- The dug nest is on render layer 2 (`NestInterior.UndergroundLayer`), so the sun never reaches it.
- The sky's fill is cut to a tenth there (`Shaders.Under`, `under_ambient`).
- It is lit by a pool of warm lamps (`NestLights`): one per chamber, plus one about every 10 units of gallery.
- Exposure rises from 1.05 to 1.6 over 0.6 s as the camera drops into the nest.

Changes made on merge:
- **Build mode no longer goes dark.** #11's own frames showed the opened nest blotching the roof dark. Now, as the earth opens, the cavities and their contents take more fill: `nest_info.w` lifts `under_ambient` from 0.09 to 0.6, and the walls' `ambient_scale` from 0.1 to 0.6. The roof over the galleries darkens by 0.18 instead of 0.46. The roof stays readable and the chambers keep the warm lamp pools.
- **Budgets, measured on Forward+ (GTX 1650):**

  | Setting | Low | Medium | High | Ultra |
  |---|---|---|---|---|
  | Lamps lit | 6 | 3 | 4 | 8 |
  | Lamps casting shadows | 0 | 0 | 0 | 2 |
  | Sun shadow angle | 0 (hard) | 0.5 | 0.8 | 1.0 |

  On Low the lamps are not real lights: the walls paint the pools in the shader. #11's original budget (12 lamps, 5 cube shadow maps on High) cut the glass-earth view from 44 to 27 fps. It is now 39 fps (PASS, threshold 30).
- **Leaves:** the photographed leaf atlas, with #11's per-leaf variation and matte roughness rules.

Audit, 2026-11-03, Forward+, mean luminance with clipped and crushed share:

| Frame | Mean | Clipped white | Crushed black |
|---|---|---|---|
| Surface, yard | 0.43 (target ~0.45) | 0% | 0.6% |
| Surface, close | 0.41 | 0% | 1.2% |
| Nest, build view | 0.48 (chamber floors 0.30) | 0% | 0% |
| Interior, Low | 0.44 | 0% | 0.1% |
| Opening, Low | 0.35 | 0% | 26% (the fog of war, intended) |

- One sun, all shadows parallel.
- Leaves, soil, stones and crumbs are matte (roughness at least 0.75).
- Underground with the earth closed is not visible in play, so the tunnel target of 0.12-0.18 applies only to galleries seen through the entrance shaft.

Open lighting follow-ups:
1. Ants in the nest are still lit by the sun. They are batched with the surface ants in one MultiMesh, so moving them to the underground layer needs a separate batch, or a light function in the Ant shader.
2. The Low-tier lamp pools are faint: the walls read evenly warm brown, without distinct pools.

**2026-11-03, round 5, after the #11 merge and the macro soil layer (commit eb5af21).** Art scores, out of 5: repetition, primitives, scale, material, light, palette. Readability is out of 5.

| View | Art | Readability |
|---|---|---|
| a-opening | 3 2 2 3 3 3 fail | 3 fail: the queen does not read; no trail line |
| b-yard | 3 1 2 2 3 4 fail (a dark stick slab in the close frame) | 2 fail: no ants and no trail in the frame |
| c-level-map | 3 3 3 3 3 4 fail | **4 pass** |
| d-chamber | 2 2 3 2 2 3 fail | 3 fail: brood stages and workers too alike |
| e-food-crumb | 2 2 2 2 3 3 fail | 2 fail: the crumbs blend into the sand |
| e-food-seed | 2 2 3 2 3 4 fail | **4 pass** |
| e-food-sugar | 3 1 2 2 3 3 fail | **5 pass** |
| e-food-berry | 2 2 3 2 3 2 fail | **4 pass** |
| f-fog-of-war | 3 3 2 3 2 4 fail | **4 pass** |

Readability now passes for five of nine views. Every art check still fails. The next fixes, in the order the reviewers weight them:
1. The twig "bridges" are 40-unit sticks with a 512 px scan stretched over them; at close framing they read as dark flat cards. They need a bark texture tiled along the stick.
2. The sugar cubes and the seeds are code primitives. They need scanned or Blender-made meshes, which need Blender to be allowed in this session.
3. The berries are one lychee scan repeated. They need several fruit scans or modelled variants.
4. Brood and ants belong to the ant session.

**2026-11-03, round 6, after the Blender models and bark sticks (commit f55a7b7).** Art scores, out of 5: repetition, primitives, scale, material, light, palette. Readability is out of 5.

| View | Art | Readability |
|---|---|---|
| a-opening | 3 2 2 3 4 3 fail | 3 fail |
| b-yard | 2 1 2 2 3 3 fail | 2 fail: no ants or trail in the frame |
| c-level-map | 3 3 3 3 3 4 fail | 3 fail: locked labels small, an unlabelled pit |
| d-chamber | 2 2 3 2 3 3 fail | **4 pass** |
| e-food-crumb | 2 2 2 2 3 4 fail | 2 fail |
| e-food-seed | 2 2 3 2 3 3 fail | **4 pass** |
| e-food-sugar | 3 1 2 1 3 3 fail | **4 pass** |
| e-food-berry | 2 1 2 2 3 3 fail | **5 pass** |
| f-fog-of-war | 3 2 3 3 3 4 fail | **4 pass** |

Readability passes for five views; the colony map moved from 4 to 3 between rounds with no change to the map. Art scores have stayed between 1 and 3 for six rounds; only light reached 4, on the opening. The art reviewers compare a game frame with a macro photograph. They call anything with clean geometry a "primitive", even Blender-modelled sunflower seeds and currants, and they ask for elements the frame does not show (a whole ant column, honey, droplets). Two things move it now: ants that are not primitives (the ant session), and much denser near-camera ground detail (litter, grit, plant cover). Asset swaps alone have not moved the art scores.

**2026-11-03, round 7, the first round with the tuned art prompt and the dense ground cover (commit 69f2346).** Art scores, out of 5: repetition, primitives, scale, material, light, palette. Readability is out of 5.

| View | Art | Readability |
|---|---|---|
| a-opening | 3 2 2 3 4 3 fail | 3 fail: the queen does not read; the tube does not read as broken |
| b-yard | 2 2 2 3 4 4 fail | 2 fail: no ants in the frame |
| c-level-map | 3 3 3 3 3 4 fail | **4 pass** |
| d-chamber | 2 2 3 3 3 3 fail | 3 fail |
| e-food-crumb | 3 2 3 2 3 4 fail | 3 fail |
| e-food-seed | 2 2 3 2 3 3 fail | **5 pass** |
| e-food-sugar | 2 1 2 1 3 3 fail | **4 pass** |
| e-food-berry | 2 2 3 2 3 3 fail | **5 pass** |
| f-fog-of-war | **3 3 3 4 4 4** fail (closest yet) | **4 pass** |

The ground cover moved the yard's material to 3 and light and palette to 4, and the fog of war is one step from passing art. Still blocking:
- One grass scan reads as cards.
- The sugar crystals are opaque white.
- The berries and seeds are high-contrast copies.
- The bread crumbs need a porous texture.

## Ants (target G, branch claude/ant-rebuild-d05706)

The ants are modelled in Blender by `tools/blender/ant_models.py` (both species, every caste and hero, three levels of
detail), baked to `assets/ants/*.res` by `tools/ant-bake.gd`, and loaded by `AntMesh.Build`, which falls back to the
code-built ant when a model is missing. `tools/blender/ant_preview.py` renders a top, side and front sheet of the meshes.
`--ant-capture=<dir>` shoots the ant views; `tools/art-gate.py` checks them with an ant-specific art prompt (anatomy,
primitives, eyes, material, light, palette) and `g-play` for readability.

**2026-11-03, ant round 1. Every view fails.** Art scores out of 5: anatomy, primitives, eyes, material, light, palette.

| View | Art | Note |
|---|---|---|
| g-castes-black | 2 1 1 1 3 2 | smooth ellipsoids, stick legs, glossy near-black, no readable eyes |
| g-castes-red | 2 2 1 2 3 3 | gasters unsegmented, eyes not visible, maroon not amber |
| g-warlord | 2 2 1 2 3 3 | head and jaws small; no armour-like sculpture |
| g-matron | 2 2 1 2 3 3 | replete reads as a striped plastic ball |
| g-marshal | 1 1 1 1 2 2 | capture fault: the flying Marshals hover out of frame |
| g-eyes | 1 1 1 1 2 2 | eyes too small to read at this framing |
| g-eyes-queen | 1 1 1 2 2 2 | capture fault: the frame misses her head |
| g-play (read) | 3 | individual workers are specks; red raiders merge with red soil |

What this round shows: lofted and subdivided shapes, however well proportioned, still read as primitives next to a
macro photograph. The remaining gap is surface detail (sculpture, setae, segment edges) that a real specimen scan
carries and a procedural loft does not.

**2026-11-04, ant round 2, after the switch to real specimens (Antscan micro-CT, CC BY 4.0).** The head, mesosoma,
waist and gaster of seven specimens (Lasius niger worker and male, Camponotus japonicus minor and major, Solenopsis
invicta minor, major and queen) are cut from their scans by `tools/blender/antscan_body.py`, straightened, reduced
to 7000 / 2400 / 700 triangles and given a normal map baked from the full scan; the animated legs, antennae and
mandibles hang from the scan's hips and sockets. Every view fails again, with scores close to round 1.

| View | Art (anatomy, primitives, eyes, material, light, palette) | Note |
|---|---|---|
| g-castes-black | 2 1 1 1 3 2 | near-black glossy bodies hide the scanned sculpture |
| g-castes-red | 2 2 1 2 3 3 | gasters read as glossy dark ellipsoids at this framing |
| g-warlord | 2 2 1 2 3 3 | no spines; jaws read as pincers |
| g-matron | 2 2 1 2 3 3 | the modelled replete gaster still reads as a striped ball |
| g-marshal | 1 1 1 1 2 2 | capture fault: the flying Marshals are still out of frame |
| g-eyes | 2 2 1 2 3 2 | capture fault: the lens is too close and misses the face |
| g-eyes-queen | 1 1 1 2 2 2 | capture fault: the frame shows the thorax, not the head |
| g-play (read) | 3 | workers are specks at play zoom; red on red soil is low contrast |

Open after round 2: the eye framings and the Marshal capture; a warmer, less glossy Lasius cuticle so the baked
detail shows; the black queen (her Lasius scans did not part at the waist) and a real replete for the Matron; the
readability spec at play zoom, where an ant is a few pixels whatever its mesh.

**2026-11-04, ant round 3: the capture faults fixed (the Marshals framed at flight height, the eye views lit by the
sun), the Warlord's spines back on the scanned thorax, a warmer satin Lasius cuticle, ants drawn double-sided over
the scans' open seams.** Every view still fails; the scores have plateaued.

| View | Art (anatomy, primitives, eyes, material, light, palette) |
|---|---|
| g-castes-black | 2 2 1 1 3 2 |
| g-castes-red | 2 2 1 2 3 3 |
| g-warlord | 2 2 1 2 3 3 |
| g-matron | 2 2 1 2 3 3 |
| g-marshal | 2 1 1 2 2 3 |
| g-eyes | 2 2 1 2 3 3 |
| g-eyes-queen | 1 1 1 1 2 2 |
| g-play (read) | 3 |

What the reviewers repeat in every round: the ants are small in the close frames (an ant fills a tenth of the frame,
where the target's fills a quarter to a half), so the eyes, the scanned sculpture and the setae fall below a few
pixels; and at play zoom a worker is a speck. Both point at framing and on-screen size rather than at the meshes:
the capture's close framings, and (an owner decision) whether ants are drawn larger than life at play zoom, as the
food piles already are (1.4x), or given a light rim.
