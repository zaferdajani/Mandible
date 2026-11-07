# Terrain relief and mine-like colony interiors: asset survey (2026-11-06)

The ants came from real scans (Antscan). This note answers the same question for the ground and the nest: which
free libraries give real 3D relief and a mine-cave look, light enough for a phone and a GTX 1650, and how they
would be used. Licences were read on the source pages; every pick is CC0 unless marked.

## 1. What makes the terrain read as 3D (and what does not)

The yard is a 321x321 heightfield with a tiled normal map. It reads flat because every bump is painted, not built:
nothing casts a shadow, nothing has a silhouette. Three things fix that, in order of value per millisecond:

1. **Real objects with silhouettes, scattered by rules.** Stones, bark, twigs, roots and grass clumps that are true
   meshes cast sun shadows and break the horizon. Poly Haven scans already in `assets/nature/meshes` (boulders,
   Namaqualand stones, root cluster, single root, dry branches, bark debris) are exactly this; the gap is density
   and placement rules (clustering in hollows, half-sunk, along trail edges), not more models.
2. **Terrain relief at two scales.** The heightfield for metre-scale dunes and dips (done), plus a **displacement
   map** from a scanned ground material for the 5 to 30 cm clods and pebble beds. Godot has no built-in tessellation,
   so displacement is applied on the CPU to the mesh vertices at build time (one pass over the grid, free at run time)
   with the matching normal map on top. Candidates with displacement maps: Poly Haven `coast_sand_05` (compacted
   brown sand with gravel), `forest_ground_04` (dry soil with stones), `leaf_scattered_gravel`; ambientCG `Ground054`,
   `Ground080`, `Ground093A/C`, `Ground079S` (sand, dirt, gravel; all with displacement).
3. **Macro shading**: the baked hollow/crest term and SSAO already exist; keep them.

What not to do: add-on terrain systems (Terrain3D, TerraBrush, LiteTerrain). They replace the heightfield the sim
reads, bring native binaries into every export and solve a problem (kilometre-scale streaming) the 320 m yard does not
have. They were rejected in the first terrain pass for the same reasons.

## 2. Nature props: libraries

| Library | What fits | Format | Licence | Note |
|---|---|---|---|---|
| Poly Haven models | scanned boulders, stones, root cluster, single root, bark, branches, leaves; `rock_face_01/02` (20-30k polys) for banks and cave walls | glTF/Blend | CC0 | already the pipeline's source; `tools/asset-process.gd` cuts to 400-1800 tris with LODs |
| Poly Haven textures | `rock_face_03`, `cliff_side`, `aerial_rocks_02` (cliff and cave walls, displacement verified); sand and soil sets above | PBR + displacement | CC0 | |
| ambientCG | `Rock035` (tagged cave), `Rock063` (layered eroded cliff), `Rock058`, `Rock051`; `Ground054/080/093/079S` | PBR + displacement | CC0 | 1-8K JPG/PNG |
| Quaternius Stylized Nature MegaKit | 116 textured models, 27 rocks, grass, bushes | glTF, FBX, OBJ | CC0 | stylised, not photoreal: use only if the art direction moves that way |
| Gobkit Nature Kit | 41 low-poly props on one atlas | GLB | CC0 | same caveat |
| Kenney Nature Kit | 330 files, low-poly trees and rocks | per Kenney | CC0 | same caveat |

The photoreal targets A to F rule out the stylised kits for the yard; they are listed for completeness.

## 3. Mine and cave interiors: libraries

| Library | What fits | Format | Licence | Note |
|---|---|---|---|---|
| **Kenney Modular Cave Kit** (2026) | 40 pieces: snap-together cave rooms and corridors with rock walls, floors, a timber-propped entrance arch, a wooden walkway, a ladder and stairs, pillars, variation sets | per Kenney (usually glTF/FBX/OBJ) | CC0 | the one ready-made kit; preview checked: stylised, chunky, orange sandstone. Good for the *layout grammar* (rooms off corridors, propped mouths), too toy-like to ship next to scanned ants |
| OpenGameArt "free mine assets pack" (rubberduck) | 35+ wooden supports, stones, stalactites, rail system, lamps, mine cart, chest | Blend | CC0 (textures CC0) | site blocked automated fetch; verify by hand |
| KayKit Dungeon Pack | 200 stylised dungeon walls, floors, stairs, props | glTF, FBX, OBJ | CC0 | dungeon, not mine; stylised |
| poly.pizza | single low-poly props: gold ore, coal block, pickaxe, stalactites and gems, cave minerals, crystal cove | glTF | per model (CC0 or CC-BY) | check each model page |
| Poly Haven / ambientCG rock walls (above) | the actual cave surface | textures | CC0 | the right source for a mine wall that matches the scanned ants |
| Sketchfab CC0 scans | cave and mine photogrammetry exists but the search is JS-only and the API refused the query | varies | per model | browse by hand with the CC0 filter if wanted |

Nothing free gives a photoreal, scanned mine kit at ant scale. That is fine: a mine look is mostly materials and a
few repeated props, both of which exist as CC0.

## 4. Recommendation: build the colony as an ant-scale mine

Keep the pit geometry (the walls now stand), and dress it like a worked mine:

- **Walls and floor:** triplanar `rock_face_03` or ambientCG `Rock035` on the banks with their displacement baked into
  the excavation mesh (the signed-distance field already gives wall normals), the sandy ground set on floors, a
  darker damp band at the foot of each wall. Layer colours by depth stay.
- **Timber:** props and lintels at every gallery mouth and along long galleries, from the Poly Haven `dry_branches`
  and `dead_quiver_branch` scans already in the project (ants shore tunnels with twigs; this is true to life and avoids
  importing a stylised kit). Cut to 300-600 tris, placed by the same rule as the roots: two uprights and a crossbeam
  every N units of gallery, scaled to the gallery width.
- **Embedded stones and roots in the walls:** Namaqualand stones half-sunk into the banks, `single_root` crossing a
  gallery, from the existing meshes.
- **Light:** the one sun through the mouth, warm bounce on the floor (exists), and at High/Ultra a few lamp-like
  bioluminescent fungus or resin glints is the only way to get "mine lamps" without inventing electricity; otherwise
  no point lights, to keep the Forward+ cluster budget.
- **Budget:** props by tier (Low: none of the timber, Medium: mouths only, High/Ultra: full); all props share a
  handful of meshes in MultiMeshes, so the added cost is a few draw calls.

Before building: a target image for the mine look goes in `docs/art-targets/` for the owner's approval, as the
pipeline requires; then the two-part gate.
