# Mutation targets H1-H5 (awaiting the owner's approval)

The visuals of the mutation track (`docs/level-design.md`, section 5). Nothing is built against them until the owner approves them; until then the game draws placeholders only (`src/Render/MutagenGlow.cs`). `REVIEW-mutation.jpg` shows all five on one sheet. Kept apart from the art pipeline's `README.md` (branch claude/art-pipeline-de350c) so the two merge cleanly.

| Target | File | What it asks of the game |
| --- | --- | --- |
| H1. The queen wasp in the web | `h1-wasp-queen-web.jpg` | a real dead paper-wasp queen wrapped in orb-web silk between grass stems and a bramble, the spider at the hub in shadow, dew on the threads; the gland shows through her cuticle with a faint violet-green glint, not a lamp |
| H2. The mutagen haul | `h2-mutagen-haul.jpg` | a wet, veined amber gland the size of a seed, glowing faintly from inside and lighting the ants' jaws and the soil grains; a faint heat-shimmer ripple for the sixth sense; soldiers walking guard |
| H3. The royal nursery | `h3-royal-larva-chamber.jpg` | one giant pearly, segmented royal larva on chewed fibre, nurses feeding it remains, and the living gauge: a heap of remains and seeds piled along a curved ledge |
| H4. The womb cocoon | `h4-womb-cocoon.jpg` | a translucent silk cocoon lit from inside, the curled hero pressing against it, silk splitting at the top with light spilling out; nurses with antennae on it |
| H5. The venom flyer | `h5-venom-flyer.jpg` | a winged Lasius alate with a short curved dark-amber wasp sting at the gaster tip, a bead of venom, the last segments faintly banded; biological, not armour |

Generation record: Higgsfield MCP `generate_image`, model `gpt_image_2_5`, 16:9, 2026-11-04, five images. H1 and H4 also stand in as the two chapters' comic beats (`assets/ui/comic/b-web.jpg`, `b-cocoon.jpg`), and a crop of H2 is the mutagen icon (`assets/ui/mutagen.png`); all three are replaced if the owner rejects the targets.

Ownership: the stinger, the royal larva and the cocoon meshes belong to the ant-rebuild session (asked on 2026-11-04); the wasp-in-web, the gland and the nursery dressing follow the art pipeline (real or Blender-modelled assets, the two-part gate).
