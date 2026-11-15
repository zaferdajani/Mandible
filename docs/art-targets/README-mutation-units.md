# Mutation unit targets P1-P6

Status: **approved by the owner on 2026-11-15** ("Approve with B": the second versions of the web ant and the armoured ant). `REVIEW-mutation-units.jpg` shows all of them on one sheet.
**Built on 2026-11-15** (see "The build" below): no stand-ins are left. In-game frames beside the targets:
`frames-mutation-units.jpg`. The automated gate has not passed; the owner is the final judge.

Art rule (docs/progression.md): every mutation unit is one body that merges the ant with its source insect.

| Target | File | What it asks of the game |
| --- | --- | --- |
| P1. Winged ant | `p1-winged-ant.jpg` | an ant with a fly's large red eyes, one pair of clear veined wings with halteres, a bristly flight thorax |
| P2. Stinger ant | `p2-stinger-ant.jpg` | the winged ant with a wasp's amber-banded gaster and a long curved sting with a bead of venom |
| P3. Web ant | `p3-web-ant.jpg` | a black ant with a spider's extra eyes, eight hairy legs, a hairy gaster with spinnerets, and a net of silk held ready to throw (version B, the second pass after review) |
| P4. Armoured ant | `p4-armoured-ant.jpg` | an ant under a beetle's pronotum shield and ridged wing cases, plated legs, bronze-black, six legs (version B, the second pass after review) |
| P5. The fly in the web | `p5-fly-in-web.jpg` | chapter 5's core: a dead bloated fly wrapped at the hub of a web, its abdomen ruptured and glowing, the web spider on its catch, ants below |
| P6. The evolve tree | `p6-evolve-tree.jpg` | the tree screen with six tunnels rising from the queen's chamber, one per branch (queen, generals, winged, stinger, web, armoured), plates stacked on each; bought violet-green, affordable amber, locked dark |

Generation record: Higgsfield MCP `generate_image`, model `gpt_image_2_6`, 16:9, text prompts only, 2026-11-15.

## Independent review before the owner (one fresh reviewer, images only; brief / reads as ant / other insect / palette, 1-5)
| Target | Scores | Defects named |
| --- | --- | --- |
| P1 | 4 / 4 / 5 / 5 | thorax reads as two lumps; sparse bristles; thin purple seam lines |
| P2 | 5 / 4 / 5 / 5 | sixth leg only implied; antennae curved rather than elbowed; sting thick, wings static |
| P3 first version (replaced) | 4 / 3 / 5 / 4 | brown, leans spider; rear legs seem to sprout from the gaster; one antenna visible |
| P4 first version (replaced) | 4 / 4 / 4 / 4 | only four legs clearly visible; a third antenna-like appendage; wing cases cover the gaster only |
| P5 | 4 / - / - / 4 | fly barely wrapped; the rupture reads as a glowing cracked egg; heavy violet |
| P6 | 4 / - / - / 5 | plates stacked unevenly (2/3/3/3/2/2) and not true hexagons; spinneret emblem unclear |

The approved P3 and P4 were generated to answer the review and were not re-scored. The sheet still shows both versions (A and B).

## The build (2026-11-15, branch claude/progression-rework)

| Target | What is in the game | Made by |
| --- | --- | --- |
| P1 winged ant | `assets/ants/{lasius,solenopsis}_Flyer_{near,mid,far}.res`: the alate's scanned body with a fly's red eye domes, halteres and thorax bristles; one broad wing pair (`AntMesh.Wings`). The black colony's gaster is the modelled one (the male specimen's scanned gaster is torn open) | `tools/blender/ant_models.py` `merged()` kind `fly` |
| P2 stinger ant | `*_Stinger_*.res`: the winged ant with a wasp's banded gaster, curved sting and venom bead. Drawn for every winged ant of a colony that has `Mutation.Stinger` (`AntRenderer.BatchOf`, `AntMesh.Stinger`); the old separate sting mesh is gone | kind `wasp` |
| P3 web ant | `*_WebAnt_*.res`: the soldier's scanned head and thorax, two more pairs of eyes, eight hairy legs, a velvet gaster with spinnerets, a net of silk held in the left front leg | kind `spider`, `legs8` in `build()` |
| P4 armoured ant | `*_Armoured_*.res`: the major's scanned head and thorax under a pronotum shield, ridged wing cases over a soft abdomen, plated spined legs | kind `beetle`, `plated` in `build()` |
| P5 the fly | `assets/mutation/fly/fly.glb` (generated scan, 12,000 triangles, `fly_glow.jpg` lights its veins) at the web's hub, with `assets/mutation/fly_hub` (hub spiral, wrapping loops, a rope of silk strung with dew). `MutagenProps.IsFly`: a core stored as a wasp's that gives the winged ants | Higgsfield `gpt_image_2_6` reference, `image_to_3d` (Meshy); `tools/blender/mutation_models.py` `fly_hub()` |
| P6 the evolve tree | `assets/ui/mutation/evolve_bg.jpg` (six tunnels) and ten emblems `em_*.png`, one per node; `src/UI/EvolveTree.cs` puts each column on its tunnel (`Tunnel0`, `TunnelStep`, measured on the backdrop) | Higgsfield `gpt_image_2_6` with P6 as the reference |

- Rebuild the units: `blender --background --factory-startup --python tools/blender/ant_models.py -- <bin> Flyer Stinger WebAnt Armoured`
  (needs the specimen bodies, `ANT_BODIES`), then `godot --headless --path . --script res://tools/ant-bake.gd -- <bin> res://assets/ants`.
  `tools/blender/ant_look.py` renders a look sheet of the baked meshes.
- Budgets (triangles, near / mid / far), each inside the caste it grew from (`BUDGET`, `fit()`): black winged 10622 / 5161 / 2446, black stinger
  10637 / 5207 / 2412, black web ant 10687 / 3293 / 2284, black armoured 10637 / 4295 / 2561; red winged 10558 / 4664 / 4211, red stinger
  10619 / 4712 / 2489, red web ant 10696 / 3290 / 1816, red armoured 10567 / 3230 / 1819. All inside their limits. Metadata `neck`, `gaster_tip`, `waist` as on every caste.
- The borrowed anatomy is marked per vertex in CUSTOM0.y (1 seta, 2 silk, 3 shell, 4 wasp bands, 5 velvet, 6 fly eye) and coloured by the Ant
  shader; no material differs from the caste's own.
- Eight legs and the gait: the shader walks three leg slots a side in two alternating groups, so the web ant's fourth leg shares the second
  slot (0 1 0 1 down the flank) with its own hip as pivot. Frames `units-walk-1/2.png`; a severed second leg takes the fourth with it.
- Frames: `MANDIBLE_MUTCAP=units|fly` or nothing, `--mutation-capture=<dir>` (`unit-<name>[-red]-close.png`, `units.png`, `fly-close.png`,
  `fly.png`, `evolve-tree.png`); `tools/art-gate.py` holds their prompts and readability specs.
- The first fly reference (a ruptured abdomen) was refused by the generator's content filter; the built one has an intact abdomen with
  glowing veins, so the rupture of P5 is not modelled.

## Gate (fresh reviewer per check, images only)

Unit art scores: brief, merge, primitives, material, light, palette. Fly art: repetition, primitives, scale, material, light, palette.
Tree art: layout, emblems, states, material, palette, legibility.

| View | Round 1 | Round 2 | Round 3 (second art pass) | What the round-3 reviewer names |
| --- | --- | --- | --- | --- |
| P1 winged ant, art | 2 2 1 2 2 3 | not run | 2 2 2 2 3 3 fail | wings read as a faint ghost; halteres and bristles not seen; eye a red blob |
| P2 stinger ant, art | 2 3 2 2 2 3 | not run | 2 2 2 2 3 3 fail | waist and wings not read; sting short; legs sticks |
| P3 web ant, art | 2 2 2 2 3 3 | not run | 2 2 1 2 3 3 fail | net reads as a white overlay, not held; gaster a grey ball; head not readable |
| P4 armoured ant, art | 2 2 2 2 3 3 | not run | 2 2 1 2 3 3 fail | shield reads as an angular card; wing-case ridges faint; legs not plated |
| Units at play zoom, read | 3 | 3 | 3 fail | the kinds are now told apart, but stingers read as wasps and web ants as spiders; bronze weak on the soil |
| P5 fly, art | 2 2 3 2 2 2 | not run | 3 2 3 2 2 2 fail | daylight, not the target's dusk; abdomen smooth and violet; beads evenly spaced |
| P5 fly, read | 3 | not run | **4 pass** | ring could be more clearly beaded; glow could be stronger |
| P6 tree, art | 3 3 3 3 4 2 | 3 4 4 4 5 3 | 3 4 4 3 4 3 fail | most columns hold one plate, the target three; tunnels above the plates read as thin lines |
| P6 tree, read (phone size) | 3 | 3 | 3 fail | locked emblems dark; headings and plate lines still small; bought and affordable differ mainly in the label |

All scores fail unless marked. Round 2 re-ran three checks after fixes to the tree's footer and states and to play-zoom contrast.

The second art pass (owner: "Another art pass", 2026-11-15) changed: stouter spider legs swelling at knee and ankle with longer hair; the net
enlarged and its rim gathered into the foot that holds it; larger amber simple eyes and five spinnerets; the wasp gaster hung on a
stalk with tergite lips, fine hair and a larger venom bead; larger halteres and stouter bristles; a rimmed, thicker pronotum shield and
broader leg plates; resting wings raised off the back (`rest_lift`) with darker veins; thicker ring rope, larger dew beads and more wrapping
on the fly, its glow breathing to green; larger text and a violet label for bought plates on the tree; the unit frames shot with the lit
flank toward the lens. One check moved to a pass (the fly's readability); the art scores stayed at 1-3, as in every earlier gate in
`docs/art-pipeline.md`, and the reviewers now pull in opposite directions (bronze too dark and too weak; web ants too pale and not pale enough).

Known and not fixed: resting wings still flutter, so a frame can catch them low; the web ant's net is thin white strands with no
translucency; plate text on the tree is 11-12 px at phone scale; the tree shows as many plates as the rules have nodes (one to three a
column); P5's rupture is not modelled; the Sky Marshal still uses the torn male gaster.
