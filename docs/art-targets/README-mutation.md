# Mutation targets H1-H5

Status: **approved by the owner on 2026-11-06** ("Approve all five"). These are the acceptance targets for the visuals of the mutation track (`docs/level-design.md`, section 5). Until the real assets land the game draws placeholders only (`src/Render/MutagenGlow.cs`). `REVIEW-mutation.jpg` shows all five on one sheet. Kept apart from the art pipeline's `README.md` (branch claude/art-pipeline-de350c) so the two merge cleanly.

| Target | File | What it asks of the game |
| --- | --- | --- |
| H1. The queen wasp in the web | `h1-wasp-queen-web.jpg` | a real dead paper-wasp queen wrapped in orb-web silk between grass stems and a bramble, the spider at the hub in shadow, dew on the threads; the gland shows through her cuticle with a faint violet-green glint, not a lamp |
| H2. The mutagen haul | `h2-mutagen-haul.jpg` | a wet, veined amber gland the size of a seed, glowing faintly from inside and lighting the ants' jaws and the soil grains; a faint heat-shimmer ripple for the sixth sense; soldiers walking guard |
| H3. The royal nursery | `h3-royal-larva-chamber.jpg` | one giant pearly, segmented royal larva on chewed fibre, nurses feeding it remains, and the living gauge: a heap of remains and seeds piled along a curved ledge |
| H4. The womb cocoon | `h4-womb-cocoon.jpg` | a translucent silk cocoon lit from inside, the curled hero pressing against it, silk splitting at the top with light spilling out; nurses with antennae on it |
| H5. The venom flyer | `h5-venom-flyer.jpg` | a winged Lasius alate with a short curved dark-amber wasp sting at the gaster tip, a bead of venom, the last segments faintly banded; biological, not armour |

Generation record: Higgsfield MCP `generate_image`, model `gpt_image_2_5`, 16:9, 2026-11-04, five images. H1 and H4 also stand in as the two chapters' comic beats (`assets/ui/comic/b-web.jpg`, `b-cocoon.jpg`), and a crop of H2 is the mutagen icon (`assets/ui/mutagen.png`); approved with the targets, they stay until finished art replaces them.

Ownership: the stinger, the royal larva and the cocoon meshes belong to the ant-rebuild session (asked on 2026-11-04); the wasp-in-web, the gland and the nursery dressing follow the art pipeline (real or Blender-modelled assets, the two-part gate).

## Gate log (two-part review, fresh reviewers; pass = 4-5 on every art criterion and on readability)

Built: `tools/blender/mutation_models.py` (gland, queen wasp, orb web with stems and bramble, nursery bed, ledge and heap), `src/Render/MutagenProps.cs`, frames by `--mutation-capture=<dir>`. The royal larva and cocoon are the ant-rebuild session's and are not in these frames.

Art scores are listed in this order: repetition, primitives, scale, material, light, palette.

| Round | H1 web: art / read | H2 haul: art / read | H3 nursery: art / read | Main blockers |
| --- | --- | --- | --- | --- |
| 1 | 3,2,3,2,2,2 / 2 | 3,3,3,1,3,2 / 1 | 3,2,2,2,2,3 / 3 | the web read as a fence; the wasp was lost; the gland was too small to find; the bed was flat; the heap was missing |
| 2 | 3,3,3,3,3,3 / 3 | 4,2,2,1,2,2 / 2 | 3,2,2,3,2,3 / 3 | the stems read as poles; the gland read as an egg; the ledge sat hidden in the walls |
| 3 | 3,3,3,3,3,2 / 3 | 4,2,2,2,3,3 / 2 | 2,2,2,2,2,3 / 2 | the stems still read as poles; the gland had no visible veins or glow and was a pale dot at play zoom; the heap pieces were too crude to read as seeds and wings |
| 4 | 3,3,3,3,3,2 / 3 | 4,2,3,2,2,2 / 3 | 3,3,3,3,2,3 / 3 | the stem pair still framed the web like a goalpost; a translucent skin showed the core as pink; the heap was cut off at the frame's edge |
| 5 | 3,2,3,2,2,2 / 3 | 3,2,3,2,3,2 / 3 | 3,2,2,3,2,3 / 2 | the bramble twig read as a crossbar; the vein filaments read as hard cracks; the heap was too small and uniform to read as a gauge |

Shared blockers outside these props, named in every round: the yard's cool grey soil and flat light with no warm low sun (the art pipeline's terrain and lighting work), the flat leaf planes and uniform pebbles around the props, and the nest view drawing ants far larger than life. `mutation-gate-r5.jpg` puts each round-5 frame beside its target.

Status after five rounds: the scores plateaued at 2-3 for art and 2-3 for readability. The props improved, from a fence-like web, a seed-like gland and a missing heap to a wasp that reads in an irregular web, a veined amber gland lit through its veins that stands out at play zoom, and a heap and bed that read up close. They are still not at the target, and nothing has passed. Next steps: warm lighting and a soil pass in the yard and the nest (the art pipeline) would lift every view; a real wasp or spider scan would lift H1; a lifelike ant scale in the nest view would lift H3.
