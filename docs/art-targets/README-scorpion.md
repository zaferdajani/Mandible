# Scorpion mutation targets P7-P8

Status: **approved by the owner on 2026-11-15, both B versions** (`p7b-scorpion.jpg`, `p8b-scorpion-ant.jpg`); the owner also chose a new caste, the piercing numbing sting and chapter 11. `REVIEW-scorpion.jpg` shows both versions of each and the proposed rule.
Nothing is built. The rule is proposed in `docs/progression.md` 1d and `docs/level-design.md` section 5.

| Target | Files | What it asks of the game |
| --- | --- | --- |
| P7. The Stone Mother | `p7-scorpion.jpg` (A), `p7b-scorpion.jpg` (B) | a large chestnut scorpion at her burrow under a flat stone on dry ground, claws open, tail arched with a venom bead, pale young on her back, garden ants for scale |
| P8. Scorpion ant | `p8-scorpion-ant.jpg` (A), `p8b-scorpion-ant.jpg` (B) | a garden ant soldier whose gaster runs on into a scorpion's five-segment tail arched over its back, bulb and sting with a venom bead; ant head, jaws, waist and six legs kept; no claws |

Generation record: Higgsfield MCP `generate_image`, model `gpt_image_2_6`, 16:9, text prompts only, 2026-11-15 (four images, 1.0 credit).

The owner, 2026-11-15: "stingers start black but upgrading them turned yellow and stronger". So P8-B is the scorpion ant as it
hatches and P8-A's amber tail is the same ant with its evolve branch fully bought; the tail's colour runs between the two.

## Independent review before the owner (one fresh reviewer, images only; version A)
| Target | Scores | Defects named |
| --- | --- | --- |
| P7-A | 4 / 3 / 4 / 5 (brief, real scorpion, scale and young, palette) | two large crab-like eyes on the face; five or six legs readable, not eight; pedipalps with too many joints; tail root hidden under the young, four segments visible, paler than the body; young tangled; ants thread-thin |
| P8-A | 4 / 4 / 4 / 4 (brief, reads as ant, scorpion merged, palette) | five legs traceable; oversized eye; red serrated jaws; abrupt change from hairy black to glossy amber at mid-gaster ("ant with a tail attached"); tail as thick as the gaster |

The B versions were generated to answer the review and were not re-scored. Known in B: P7-B's far-side legs are mostly hidden;
P8-B's sting is a smaller mark than A's.

## The build (2026-11-15, branch claude/scorpion on top of claude/progression-rework)

| Target | What is in the game | Made by |
| --- | --- | --- |
| P8-B scorpion ant | `assets/ants/{lasius,solenopsis}_ScorpionAnt_{near,mid,far}.res`: the soldier's scanned body with its own gaster; a scorpion's tail of five barrel segments swells out of the gaster's tip and arches up and forward, ending in the bulb, the curved sting and a bead of venom; fine hairs on the tail in the near mesh | `tools/blender/ant_models.py` `merged()` kind `scorpion` |
| P8-A, the same ant upgraded | no second mesh: the tail is surface kind 7 in CUSTOM0.y, its coordinate running from root to point. The Ant shader draws it in the ant's own dark shell warming to chestnut (P8-B) and turns it amber-yellow with the uniform `tail_amber`, the colony's ranks in the scorpion branch out of five (`AntRenderer.TailAmber`) | `src/Render/Shaders.cs`, `AntRenderer` |
| P7-B the Stone Mother | `assets/bugs/scorpion/scorpion.glb` (generated scan, 13,316 triangles, its own colour map, six young on her back). Drawn for `AntType.Scorpion` and, small and pale, for `AntType.Scorpling`, by its own shader: a scan has no per-vertex parts, so legs, tail and claws are found by where a vertex lies and walk, whip and grip from the same instance data the ants use | Higgsfield `gpt_image_2_6` isolated reference, `tripo_h3_1_image_to_3d`; `tools/blender/scorpion_model.py`; `src/Render/ScorpionModel.cs` |
| the evolve tree's new branch | `assets/ui/mutation/evolve_bg.jpg` redrawn from the approved P6 backdrop (the generator drew eight tunnels for the seven asked: the eighth stands empty for the mantis ants), emblems `em_deepsting.png` and `em_nervevenom.png` | Higgsfield `gpt_image_2_6`; `src/UI/EvolveTree.cs` (`Tunnel0`, `TunnelStep` measured again) |

- Rebuild the ant: `blender --background --factory-startup --python tools/blender/ant_models.py -- <bin> ScorpionAnt`, then
  `godot --headless --path . --script res://tools/ant-bake.gd -- <bin> res://assets/ants`.
- Budgets (triangles, near / mid / far), inside the soldier's it grew from: black 10489 / 3247 / 2467 (limits 10700 / 3296 / 2482),
  red 10489 / 3245 / 1814 (limits 10700 / 3295 / 1830). Metadata `neck`, `gaster_tip` (the point of the sting), `waist` as on every caste.
- Frames: `MANDIBLE_MUTCAP=scorpion`, `--mutation-capture=<dir>`: `unit-scorpionant[-amber][-red]-close.png`, `unit-scorpion-close.png`,
  `scorpion-play.png`, `scorpion-play-near.png`, `evolve-tree-scorpion.png`. Sheet beside the targets: `frames-scorpion.jpg`.
- Higgsfield spent on the build: one reference image and two tree images (0.75), a single-image reconstruction that came back as a
  flat card and was thrown away (1), the Tripo scan (9). Meshy, which made the fly, costs 30 and the account had 22.
