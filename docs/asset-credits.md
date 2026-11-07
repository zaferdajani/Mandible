# Asset credits

Every third-party asset in the game, its source and its licence. Add a row whenever an asset comes in;
`docs/art-pipeline.md` explains how assets are processed.

## Poly Haven (CC0 1.0)

All Poly Haven assets are [CC0](https://polyhaven.com/license): public domain, no attribution required. They are
credited anyway. Meshes are cut down to phone budgets with `tools/asset-process.gd` (`assets/nature/meshes`). Textures
are resized to 512-2048 px (`assets/nature/tex`, `assets/nature/ground`) and imported VRAM-compressed with mipmaps.

| Asset | Source | Use | Authors |
|---|---|---|---|
| Bark Debris 01 | [bark_debris_01](https://polyhaven.com/a/bark_debris_01) | model | Greg Zaal (photography), Jenelle van Heerden (modeling) |
| Boulder 01 | [boulder_01](https://polyhaven.com/a/boulder_01) | model | Rico Cilliers (All) |
| Croissant | [croissant](https://polyhaven.com/a/croissant) | model | Greg Zaal (Processing), Dario Barresi (Scanning) |
| Dead Quiver Branch 01 | [dead_quiver_branch_01](https://polyhaven.com/a/dead_quiver_branch_01) | model | Greg Zaal (Photography), Jenelle van Heerden (modeling) |
| Dead Quiver Branch 02 | [dead_quiver_branch_02](https://polyhaven.com/a/dead_quiver_branch_02) | model | Greg Zaal (Photography), Jenelle van Heerden (modeling) |
| Dry Branches Medium 01 | [dry_branches_medium_01](https://polyhaven.com/a/dry_branches_medium_01) | model | Rico Cilliers (All) |
| Dry Quiver Leaf | [dry_quiver_leaf](https://polyhaven.com/a/dry_quiver_leaf) | model | Greg Zaal (photography), Rico Cilliers (modeling) |
| Food Apple 01 | [food_apple_01](https://polyhaven.com/a/food_apple_01) | model | Oliver Harries (All) |
| Food Lychee 01 | [food_lychee_01](https://polyhaven.com/a/food_lychee_01) | model | Oliver Harries (All) |
| Pears Asian 01 | [food_pears_asian_01](https://polyhaven.com/a/food_pears_asian_01) | model | Amal Kumar (All) |
| Food Pomegranate 01 | [food_pomegranate_01](https://polyhaven.com/a/food_pomegranate_01) | model | Oliver Harries (All) |
| Hamburger Buns | [hamburger_buns](https://polyhaven.com/a/hamburger_buns) | model | Alexander Shulha (All) |
| Namaqualand Boulder 02 | [namaqualand_boulder_02](https://polyhaven.com/a/namaqualand_boulder_02) | model | Greg Zaal (Photography), Rico Cilliers (modeling) |
| Namaqualand Boulder 03 | [namaqualand_boulder_03](https://polyhaven.com/a/namaqualand_boulder_03) | model | Jenelle van Heerden (modeling), Dario Barresi (Photography) |
| Namaqualand Boulder 04 | [namaqualand_boulder_04](https://polyhaven.com/a/namaqualand_boulder_04) | model | Jenelle van Heerden (All) |
| Namaqualand Boulder 05 | [namaqualand_boulder_05](https://polyhaven.com/a/namaqualand_boulder_05) | model | Jenelle van Heerden (modeling), Dario Barresi (Photography) |
| Namaqualand Boulder 06 | [namaqualand_boulder_06](https://polyhaven.com/a/namaqualand_boulder_06) | model | Greg Zaal (Photography), Jenelle van Heerden (modeling) |
| Namaqualand Rocks 01 | [namaqualand_rocks_01](https://polyhaven.com/a/namaqualand_rocks_01) | model | Greg Zaal (Photography), Jenelle van Heerden (modeling) |
| Namaqualand Stones 01 | [namaqualand_stones_01](https://polyhaven.com/a/namaqualand_stones_01) | model | Greg Zaal (Photography), Jenelle van Heerden (modeling) |
| Rock 07 | [rock_07](https://polyhaven.com/a/rock_07) | model | Jenelle van Heerden (All) |
| Rock 09 | [rock_09](https://polyhaven.com/a/rock_09) | model | Jenelle van Heerden (All) |
| Rock Moss Set 01 | [rock_moss_set_01](https://polyhaven.com/a/rock_moss_set_01) | model | Kless Gyzen (All) |
| Rock Moss Set 02 | [rock_moss_set_02](https://polyhaven.com/a/rock_moss_set_02) | model | Kless Gyzen (All) |
| Root Cluster 01 | [root_cluster_01](https://polyhaven.com/a/root_cluster_01) | model | Jenelle van Heerden (Photography), Rico Cilliers (photography, processing) |
| Single Root | [single_root](https://polyhaven.com/a/single_root) | model | Jenelle van Heerden (All) |
| Stone 01 | [stone_01](https://polyhaven.com/a/stone_01) | model | Dario Barresi (Photography), Rico Cilliers (photography, processing) |
| Gravelly Sand | [gravelly_sand](https://polyhaven.com/a/gravelly_sand) | ground set | Dario Barresi (All) |
| Dry Ground Rocks | [dry_ground_rocks](https://polyhaven.com/a/dry_ground_rocks) | ground set | Rob Tuytel (All) |
| Brown Mud Dry | [brown_mud_dry](https://polyhaven.com/a/brown_mud_dry) | ground set | Rob Tuytel (All) |
| Forest Leaves 04 | [forest_leaves_04](https://polyhaven.com/a/forest_leaves_04) | ground set | Rob Tuytel (All) |
| Dry Decay Leaves | [dry_decay_leaves](https://polyhaven.com/a/dry_decay_leaves) | ground set | Amal Kumar (All) |
| Red Mud Stones | [red_mud_stones](https://polyhaven.com/a/red_mud_stones) | ground set | Rob Tuytel (All) |
| Rock Face 03 | [rock_face_03](https://polyhaven.com/a/rock_face_03) | nest walls (albedo, normal, ARM, displacement baked into the bank) | Dario Barresi (Photography), Rico Cilliers (Processing) |
| Coast Sand 05 | [coast_sand_05](https://polyhaven.com/a/coast_sand_05) | nest floors | Rob Tuytel (Photography), Dario Barresi (Processing) |

## Generated art

The approved target images in `docs/art-targets/` were generated with Higgsfield `gpt_image_2_5`. The prompts are
listed in `docs/art-targets/README.md`. They are references for review, not shipped in the game.

## Antscan (CC BY 4.0)

The ants' bodies are real specimens from [Antscan](https://www.antscan.info), synchrotron micro-CT of ethanol-preserved
ants scanned at the KIT Light Source (Katzke, van de Kamp, Economo et al., "High-throughput phenomics of global ant
biodiversity", bioRxiv 2025, doi:10.1101/2025.11.29.689474). The surface meshes come from
[Biomedisa](https://biomedisa.info/antscan) and are licensed [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/).
`tools/blender/antscan_body.py` keeps the head, mesosoma, waist and gaster, straightens the curled specimen, reduces it
to the game's levels of detail and bakes a normal map from the full scan; `tools/blender/ant_models.py` hangs the
game's animated legs, antennae and mandibles on it (`assets/ants`). Changes were made: the specimens are re-posed,
decimated and recoloured, and their limbs replaced.

| Specimen | Antscan dataset | Use |
|---|---|---|
| Lasius niger worker, CASENT0745065 | [10.35097/eb1pckwn5jtjg5f8](https://dx.doi.org/10.35097/eb1pckwn5jtjg5f8) | black worker, acid gunner, Acid Matron (head and thorax) |
| Lasius niger male, CASENT0745068 | [10.35097/zsnrhpxf87n1c3ya](https://dx.doi.org/10.35097/zsnrhpxf87n1c3ya) | black flying ant |
| Lasius flavus alate queen, CASENT0745013 | [10.35097/vt63mr7fzxgkr7ma](https://dx.doi.org/10.35097/vt63mr7fzxgkr7ma) | black queen, Sky Marshal |
| Camponotus japonicus workers, OKENT0105312, OKENT0105313 | [10.35097/vcdzdwuqzcnb77y9](https://dx.doi.org/10.35097/vcdzdwuqzcnb77y9), [10.35097/8xdv23v8yww929b8](https://dx.doi.org/10.35097/8xdv23v8yww929b8) | black soldier, major, Warlord |
| Solenopsis invicta minor worker, CASENT0744526 | [10.35097/ne8ttte36gsk2puk](https://dx.doi.org/10.35097/ne8ttte36gsk2puk) | red worker, acid gunner, Acid Matron (head and thorax) |
| Solenopsis invicta major worker, CASENT0744090 | [10.35097/5z5r1fvxvkqhmhz4](https://dx.doi.org/10.35097/5z5r1fvxvkqhmhz4) | red soldier, major, Warlord |
| Solenopsis invicta alate queen, OKENT0105174 | [10.35097/knfknksdr12pcnsf](https://dx.doi.org/10.35097/knfknksdr12pcnsf) | red queen, flying ant, Sky Marshal |

## Fonts

Cinzel: SIL Open Font License, see `assets/fonts/Cinzel-OFL.txt`.
