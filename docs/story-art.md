# Story art: the colony from the glass

The level map tells one story in four acts (see `docs/level-design.md` and `Levels.Intro`/`Outro`). The story plays as cinematic panels: four panels open the game before Level 1, one panel leads into each later level, and one more is the epilogue after the finale. `src/UI/Comic.cs` holds the captions and the viewer (`ComicStrip`). A level's panel plays once before its briefing, the colony map's **Watch the story** button replays it, and the last level's result card offers the epilogue.

## The owner's direction

The story art must sit at the same level as the game. Mandible is a 3D strategy game, not a cartoon. A first pass rendered posed game meshes with an ink-and-halftone treatment, and it read as cartoon, so it was dropped. The panels are now photoreal, cinematic macro stills in the game's warm sand and amber palette.

## Provenance

| | |
| --- | --- |
| Tool | Higgsfield (MCP connector), model `gpt_image_2_5` (GPT Image 2.5, OpenAI), quality `high`, resolution `2k`, aspect 3:2 (2048 × 1360) |
| Date | 2 November 2026 |
| References | The game's own hero renders `assets/ui/heroes/warlord_black_card.png`, `matron_black_card.png`, `warlord_red_card.png`, and one gameplay frame (Level 6, Acid Matron in a sunlit clearing). They keep the ants consistent with the game's models and lighting. |
| Post | Downloaded at 2048 × 1360 and saved as 1600 × 1063 JPEG (quality 88) in `assets/ui/comic/<file>.jpg`; no other edits. Captions are drawn by the game, not baked into the art. |

Every prompt begins with "Cinematic photoreal (macro) still from a premium 3D strategy game cutscene" and ends with "No text, no letters". The prompts were:

| File | Before | Prompt (abridged to its subject) |
| --- | --- | --- |
| `o1-tube` | Level 1 | A corked glass collector's tube half full of damp sand in a worn canvas backpack pocket; inside, a black garden ant queen, a grizzled soldier with big mandibles, workers. Golden evening light, amber rim light. |
| `o2-fall` | Level 1 | Ant's-eye view: the tube tumbles from the open pocket of a huge backpack; a giant hiking boot steps past, out of focus. |
| `o3-break` | Level 1 | The tube shattered on stones; corked half, shards, spilled sand and cotton; the queen climbs out, the soldier at her side, workers behind. |
| `o4-woods` | Level 1 | The queen leads a column into tall grass, glass glinting behind; the old soldier at the head; the hiker walking away, blurred. |
| `b-rain` | Sweet Rain | After rain: a clover stem with aphids and a golden honeydew drop; two black workers tend them. |
| `b-scout` | The Foraging Season | A red fire ant scout on a pebble watches black ants carry seeds along a trail. |
| `b-log` | The Hollow Log | A hornet hovers over a rotting mossy log; a black worker and soldier look up. |
| `b-cinder` | Queen Cinder | A red fire ant mound at evening; the red queen on its summit, soldiers pouring down the slopes. |
| `b-vetch` | The Matron's Road | A wounded acid matron sheltering by a curved glass shard; a black scout finds her. |
| `b-hornet` | The Hornet Mother | A paper wasp nest at dusk; the giant Hornet Mother crawls out of the entrance, hornets circling. |
| `b-drought` | The Dry Spell | Cracked parched earth, a shriveled berry, a lone black worker in the heat haze. |
| `b-wings` | Wings and Acid | In an underground chamber a newly emerged winged black ant spreads its wings; workers, eggs and cocoons. |
| `b-princesses` | The Nuptial Flight | Winged red princesses climb grass stems on a red mound at sunset, one lifting off. |
| `b-march` | The Long March | A black ant army marches through autumn leaves at dusk, led by an armored warlord. |
| `b-empress` | The Ember Empress | A vast red royal chamber; the enormous red Empress on her eggs, flanked by red majors. |
| `e-home` | Epilogue | The black queen lays eggs beside a kept glass shard; the warlord and acid matron stand watch. |

To replace a panel, generate it at 3:2 with the same references and a prompt in the same voice. Save it as a 1600 × 1063 JPEG under the same file name and run the Godot import step (`godot --headless --audio-driver Dummy --path . --import`).
