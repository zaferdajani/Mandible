# The Glass Queen: persona, style guide, generals

The script is in `assets/voice/lines.tsv`: one line per row, one audio file per line. The voice is described in
`docs/queen-voice.md`. The telepathy panel in the game is `src/UI/Telepathy.cs`.

## Who she is

A *Lasius niger* queen from a collector's laboratory stock. She was bred and kept under glass, and she was changed by
whatever the lab did to her line. She has never known the open woods. When the tube broke on the path, she lost
everything: her stores, most of her daughters and her certainty about the world. She kept one thing, which is the
link: a faint sense, stronger in her than in any wild queen, of every ant she has borne.

She is not a mother hen. She is a **sovereign who has just discovered she can be one**: mature, arrogant, and
coldly certain that the woods will belong to her. In Act I she is careful and economical, but she never sounds
afraid. She has nothing to spare. Each level she grows larger, colder
and more certain. By the time she has eaten a mutated wasp queen's mutagen, the player should feel a small chill when
she says "we".

### The player

The player is not a commander she hired. The player is **her will made separate**, the part of the colony's mind
that can see the whole yard at once. She calls the player **"my chosen"**. She speaks to the player as the one being
she trusts completely, and she assumes obedience without ever demanding it. She never says "please". She never says
"you must". She states what will happen, and then it does.

### Voice on the page (tone rules)

1. **We and ours** for the colony; **I** only for her own body (her chamber, her eggs, her wounds).
2. **Short.** One or two sentences, three at most. Every line must work as text with the sound off.
3. **Each line contains one instruction**, when the moment needs one, stated as plainly as the tutorial text.
4. **Concrete nouns** from the ant's world: jaws, scent, shaft, brood, glass, remains. No sci-fi words, no "units",
   no "resources", no game terms.
5. **Calm in a crisis.** When she is dying she says so plainly. She never screams, and there are no exclamation marks.
6. **Proprietary toward her own, contemptuous toward enemies.** She names enemy queens and never insults them. Saying their names is
   threat enough.
7. **No borrowed lines.** Nothing that echoes a famous villain's catchphrase or any other game's dialogue.

### How she changes across the campaign

| Act | She sounds | Example |
|---|---|---|
| I, The Broken Tube | Careful, economical, intimate. She is still afraid of the open sky. | "I am home. Dark and close and safe." |
| II, Red Neighbours | Cool and decisive. She has killed a queen, and she found it easy. | "Ashling is dead. Her mother will hear of it. Good. Let her come." |
| III, Hard Times | Hungry and patient. She plans past the current fight. | "Somewhere in her body is a gift for us. I can taste it already." |
| IV, The Ember Empress | Vast. She is changed by the mutagen and sounds more like a hive than one queen. | "Everything we have become since the glass broke, use it now." |

The audio follows the same arc through the `queen1`, `queen2` and `queen3` profiles. The words change before the
voice does, so the player notices the words first.

## Generals

In the campaign, the colony's heroes come only from a royal larva that the queen lays. Player-facing text calls them
**generals**. The code keeps the identifier `Hero`. Generals speak to the player too, briefly, through the queen's
link. A general's lines are always shorter than hers, and the queen always speaks first in a scene.

| General | Personality | How it talks | First line |
|---|---|---|---|
| **Warlord** (the campaign's first is Hemlock) | Born for war and enjoys it. Loyal to the queen as a blade is loyal to a hand. | Clipped, a little amused, never more than one sentence. Calls the queen "Mother". | "I am awake, Mother. Point me at something." |
| **Acid Matron** (Vetch, the tube's old Matron) | Old, survived the fall alone, sardonic, a chemist. The only one who teases the queen. | Dry, precise, with a rasp. | "Vetch, at your service. Bring me something that burns." |
| **Sky Marshal** | Restless and fast. Sees the whole yard from above and is impatient with ants that walk. | Quick and bright, a little breathless. | "The sky is ours. Show me where to strike." |

Each general needs its own voice candidate, approved by the owner like the queen's, before its lines are generated.
The processing profiles are already in `tools/voice/telepathy_fx.py`.
