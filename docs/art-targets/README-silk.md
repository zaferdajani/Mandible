# Silk targets S1-S3

Status: **approved by the owner on 2026-11-15** ("Approve all three"). Built: `tools/blender/silk_models.py` ->
`assets/silk/silk.gltf`, drawn by `src/Render/SilkWorks.cs`; frames by `--silk-capture=<dir>`. The old flat decal
(`Shaders.Web`) and the code-built cocoon on webbed ants are gone. `REVIEW-silk.jpg` shows all three targets on one sheet.

The web post, web tower and trap nest (the owner's later design, same day) have no target of their own: they are
built in the approved S2 style from the same kit and still need the owner's eye.

| Target | File | What it asks of the game |
| --- | --- | --- |
| S1. The snared ant | `s1-snared-ant.jpg` | left: play zoom; right: close-up. Silk with thickness that stands off the ground as a low tent, anchored to grass blades and pebbles; bands lashed over the thorax and legs of the caught insect; taut tension lines; beads of glue on every thread; warm cream silk, never white or grey |
| S2. The spinnery | `s2-spinnery.jpg` | a silk pavilion of woven sheets and funnel doorways strung between grass stems beside the mound, guy-lines down to pebbles, silk ants living on it (in the doorways, on the roof, walking a guy-line); reads as a building from the RTS camera |
| S3. The trap web | `s3-trap-web.jpg` | a low hammock of sticky silk across an approach, anchored to stems on both sides and to pebbles, trip lines fanning over the soil; raiders stuck with legs lifted by the strands; long shadows that show its height |

Generation record: Higgsfield MCP `generate_image`, model `gpt_image_2_6`, 16:9, 2026-11-15, three images.

## Gate log

| Round | Result |
| --- | --- |
| build passes 1-4 (self-checked frames, `D:/MandibleBuilds/review/silk-r1`) | chalk-white rods -> thinner ropes, cream tint; blue-grey sheets -> gauze lit through; box spinnery -> rounded pavilion with funnel doorways and ants |
| 1, independent reviewer (fresh agent, sheet vs targets) | **not passed**: snare 2,2,3,3,3 / read 2; spinnery 3,3,3,3,3 / read 3; trap 3,3,3,3,4 / read 3 (primitives, material, light, palette, anchoring / readability). Blockers: snare reads as straight rods, insects not visibly wrapped, spinnery reads as a smooth lantern |
| 2 (after round 1's fixes; not yet re-reviewed) | threads hang, a veil of silk wraps every held insect, a woolly outer layer on the spinnery, deeper trap belly. Frames: `frames-silk.jpg` (rows 1-3 snare, spinnery, trap, close and play zoom; then the web post, tower and trap nest, the placement ring, the phone layout) |

| 3, independent reviewer (real fibre texture `silk_fibre.png`, wandering ropes with stray fibres, brighter beads; frames `D:/MandibleBuilds/review/silk-r3`) | **not passed**: snare 2,2,2,3,2 / read 2; spinnery 3,3,2,2,3 / read 3; trap 3,3,2,3,3 / read 3. Blockers: near-white/grey sheets, snare unreadable at play zoom, spinnery reads as a bag, stems as thin rods. A warmer tint went in after this round (not re-reviewed) |

**Plateau.** Three rounds of script-built geometry sit at 2-3, the same ceiling the mutation props hit. More rounds of
the same method are unlikely to pass. Routes that could: image-to-3D of the approved concepts for the spinnery (as
`core.glb` was made), photographed silk on alpha cards for the webs, or a hand-sculpted set.
