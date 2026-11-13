# Mutation core targets M1-M3

Status: **approved by the owner on 2026-11-13** ("Approve all three"). `REVIEW-mutation-core.jpg` shows all three on one sheet.

| Target | File | What it asks of the game |
| --- | --- | --- |
| M1. The mutated carcass | `m1-mutated-carcass.jpg` | a dead insect whose body holds mutagen shows a ruptured patch that radiates violet-green, with glowing veins branching out under the cuticle and a pool of light on the soil; the strength of the core sets how intense it is (dim = weak, blazing = strong) |
| M2. The core in the jaws | `m2-core-carried.jpg` | the carved-out core is a seed-sized, faceted, organic crystal seed, half-translucent, with a bright green heart and fine veins, violet edges; it pulses and lights the carrier's head and the soil; it reads at play zoom |
| M3. The queen's sky beam | `m3-queen-sky-beam.jpg` | the queen's mutation: a soft violet-green column of light rising from the nest into the sky with motes climbing it, a shockwave ring across the soil, glowing ground cracks, grass and leaves pushed outward; epic but not blinding |

Generation record: Higgsfield MCP `generate_image`, model `gpt_image_2_6`, 16:9, 2026-11-13. In-game frames: `frames-mutation-core.jpg`.

## Built (2026-11-13)
- M2 core: `assets/mutation/core/core.glb`, Meshy image_to_3d (textured, 4k tris) from an isolated reference generated from the M2 target. Drawn by `MutagenGlow` once the gland is carved free: the scan's own texture glows from within, it pulses by core strength, and it grows with camera distance so it reads at play zoom.
- M1 patch: `assets/mutation/core/veins.png`, a generated vein map. While the core is still in the carcass, it radiates round the gland inside the camera-facing halo, drawn in front of the body. It is also projected as a decal onto bodies and soil below. Its reach and fire scale with strength.
- M3 and the general's burst: `src/Render/MutationBurst.cs` (shaders and particles only).

## Review log (independent reviewer, frames beside targets; art / readability)
| Round | M1 carcass | M2 core | M3 beam / general burst | Main blockers |
| --- | --- | --- | --- | --- |
| 1 | 2 / 2 | 3 / 2 | 3 / 3, burst 2 / 2 | the patch read as a glass shard and was too small; the core was a 6-px dot at play zoom; the beam was blue-white with no ring or cracks; the burst ring was pastel rainbow |
| 2-3 (fixes, not re-scored) | patch 1.5x bigger, veins hotter | core and halo bigger at play zoom, stronger light pool | ring and cracks mixed (not additive) and drawn over the nest mound; beam re-tinted violet with a green heart | |

Still open: the patch is a glow over the body, not a rupture cut into the wasp's cuticle (that would need the wasp model edited); the crack pattern reads a little like a net up close; the beam is fainter than the target against a bright yard.

## N1. The queen's mutation tree screen
Target `n1-mutation-tree.jpg`, **approved by the owner on 2026-11-13** ("Approve N1, build it"): the trees as ant tunnels in the nest's earth rooted at the queen's chamber, every skill a hexagonal chitin plate with its emblem and name, taken = violet-green, open = amber, locked = dark. Built in `src/UI/MutationTree.cs` from generated art in `assets/ui/mutation/` (background, plate, ten emblems, made with N1 as the reference). Frame: `frames-mutation-tree.jpg`. The build keeps the game's rung order, so Venom Stingers is the root of The Bite (N1 drew that tree upside down).
