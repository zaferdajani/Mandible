# Silk targets S1-S3

Status: **proposed 2026-11-15, waiting for the owner's approval.** Nothing final is built against them yet. Until
then trap webs and the spinnery are drawn with the old flat decal (`Shaders.Web`, marked INTERIM in
`AntRenderer.cs`), which these targets replace everywhere: thrown webs (`WebShot`/`WebLand`, `Sim.Webs`), the
Web Throwing pins (`Ant.PinT`), the standing webs at spider lairs, trap webs (`Sim.Traps`) and the spinnery.
`REVIEW-silk.jpg` shows all three on one sheet.

| Target | File | What it asks of the game |
| --- | --- | --- |
| S1. The snared ant | `s1-snared-ant.jpg` | left: play zoom; right: close-up. Silk with thickness that stands off the ground as a low tent, anchored to grass blades and pebbles; bands lashed over the thorax and legs of the caught insect; taut tension lines; beads of glue on every thread; warm cream silk, never white or grey |
| S2. The spinnery | `s2-spinnery.jpg` | a silk pavilion of woven sheets and funnel doorways strung between grass stems beside the mound, guy-lines down to pebbles, silk ants living on it (in the doorways, on the roof, walking a guy-line); reads as a building from the RTS camera |
| S3. The trap web | `s3-trap-web.jpg` | a low hammock of sticky silk across an approach, anchored to stems on both sides and to pebbles, trip lines fanning over the soil; raiders stuck with legs lifted by the strands; long shadows that show its height |

Generation record: Higgsfield MCP `generate_image`, model `gpt_image_2_6`, 16:9, 2026-11-15, three images.

## Build plan once approved

- One Blender set (`tools/blender/`, exported to `assets/silk/`): a snare tent (3 variants), a wrap that sits on a
  caught body (scaled to the ant), the trap hammock (3 variants), the spinnery, and anchor strands. The older
  `assets/mutation/web/web.gltf` (orb web for target H1, gate plateau at 2-3) is not reused for these.
- Anchors: each web picks the nearest grass stems and stones from `Scatter` at spawn and stretches its strands to
  them, so no two sit the same way.
- Silk material: one shader for all of it (strand normal detail, glue beads as a sparkle mask, warm backlight).
- Two-part gate per `docs/art-pipeline.md`, one screenshot per visual at play zoom and close.
