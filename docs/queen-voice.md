# The Glass Queen's voice: spec and production

## What the owner asked for

A queen alien voice that feels close to the queen-of-the-swarm character in a well-known space RTS: a woman's voice
made alien and regal, heard in the player's head rather than in the room. The goal is the *feeling*, made for an
original character. No real actor's voice is cloned, imitated or named here, no existing voice is used as a
reference, and nothing from that game (names, lines, sounds) is copied.

## The owner's brief (2026-11-07, verbatim)

> A mature, female alien queen with a powerful, commanding, and sinister tone. Her voice is deep, resonant, and
> dripping with royal arrogance and cold malice. It has a subtle, eerie extraterrestrial overlay—like a dual-tonal or
> robotic resonance underneath her words. She speaks slowly, deliberately, and with absolute authority. Dark, regal,
> and menacing.

This brief takes precedence over everything below. Chosen voice: **FG2**, two preset TTS voices speaking together,
time-aligned word for word (`tools/voice/layer.py`). Voice F (deep) leads, and voice G sits 5 dB under it. That is
the "dual-tonal" part. The resonance is the ring-modulated and comb layer (`res_db`) in `telepathy_fx.py`.

## What people hear in that kind of voice (research, 2026-11-07)

Few sources describe the voice in detail, but they agree on these points:

- **It is doubled.** The original game's corrupted voice was made by layering the actor's performance with itself,
  so one mouth seems to speak with two voices. (Wikipedia, "Sarah Kerrigan")
- **It echoes because she speaks two ways at once.** Fans describe the echo as the character talking aloud and
  telepathically at the same moment. They also say the earlier, non-echo version of the voice sounded less like the
  queen. (TV Tropes character page; NamuWiki)
- **The change is in the attitude more than in the sound.** The original actor described the change as a change of
  personality: the same woman, now sure of her power. (Wikipedia)
- **Players dislike it when it is overplayed.** Forum players criticise the later performance as "way overacted",
  and they praise the earlier one for being restrained. The menace works best when it is quiet. (GameFAQs;
  Team Liquid)
- This is the general "voice of the legion" trope: a reverberating, layered voice that tells the listener the
  speaker is more than one person. (TV Tropes, "Voice of the Legion")

The takeaway: the voice sounds alien because of a **second layer and an inner-space tail**, and it sounds regal
because of **calm delivery**. Pitch tricks are not what makes it work. The performance must stay intimate and
unhurried. All the strangeness comes from the processing.

Sources: [Wikipedia: Sarah Kerrigan](https://en.wikipedia.org/wiki/Sarah_Kerrigan),
[TV Tropes: Sarah Kerrigan](https://tvtropes.org/pmwiki/pmwiki.php/Characters/SarahKerrigan),
[TV Tropes: Voice of the Legion](https://www.tvtropes.org/pmwiki/pmwiki.php/Main/VoiceOftheLegion),
[NamuWiki](https://en.namu.wiki/w/%EC%82%AC%EB%9D%BC%20%EC%BC%80%EB%A6%AC%EA%B1%B4),
[GameFAQs thread](https://gamefaqs.gamespot.com/boards/954721-starcraft-ii-heart-of-the-swarm/65721586),
[Team Liquid thread](https://tl.net/forum/starcraft-2-hots/139443-anyone-else-disappointed-by-kerrigans-voice?page=2).

## Voice spec: the Glass Queen

**Base performance (the dry TTS line)**
- Adult female, low-middle register (median pitch around 130-155 Hz before processing), with a little breath.
- Slow and certain. She never rushes, never shouts and never asks the player's permission. Her sentences end going
  down, not up.
- She sounds close to the microphone, as if speaking from inside the listener's ear: soft consonants and no
  projection.
- Royal arrogance and cold malice. She owns the colony ("my chosen", "my daughters") rather than loving it, and she
  is contemptuous of enemies. The menace is in the stillness and the certainty, never in a growl.
- No accent comedy, no vocal fry used for effect, no sing-song.

**Processing (what makes her telepathic)**, made by `tools/voice/telepathy_fx.py`:
0. Two voices as one (FG2): the second take is time-aligned to the first and mixed 5 dB under it.
1. Main voice lowered by 1.5 semitones (2.5 by the last act), pitch and formants together, so a slightly larger body seems to speak.
2. A *hive double*: the same line an octave lower, 17 dB down and 9 ms late. It is felt more than heard. It is the
   colony speaking under her.
3. A *whisper shadow*: the line's spectral envelope driven by noise (unvoiced), 19 dB down and 14 ms early. The thought
   arrives a moment before the word.
4. Chorus: two slow modulated delays (14 ms and 21 ms, 0.2-0.3 Hz), mixed at 22%. The edges smear a little, like many
   mouths.
5. A close-mic presence lift at 3.2 kHz and a low cut at 90 Hz. A dark synthetic room tail (1.4 s, darkens as it
   decays, 25 ms pre-delay) at 16%: the inside of a skull, not a cathedral.
6. Resonance (the brief's "robotic resonance underneath"): the octave layer ring-modulated at about 62 Hz (drifting
   slowly) plus a short metallic comb at about 180 Hz, band-limited, 17 dB under the voice (12 dB in Act IV).
7. Level: the voiced RMS goes to -20 dBFS with a soft limit at -1 dBFS. Every line plays at the same loudness.

The text must be readable at that speed. The processing must never smear words past understanding: if a word gets
lost, turn the chorus and whisper down rather than the main voice.

## How the voice grows (the mutation arc)

| Stage | When | Profile | What changes |
|---|---|---|---|
| Survivor | Act I (levels 1-3) | `queen1` | Alone, newly free, careful. The double is barely there. |
| Matriarch to Empress | Acts II-III | `queen2` | The colony is large, so the double and the chorus rise. She is more sure of herself and colder and more contemptuous. |
| Mutated empress | after the wasp-queen mutagen, Act IV | `queen3` | Lower again, and the hive double is clearly audible. The whisper shadow sounds faintly like an insect. The tail is longer. |

The same dry voice is used throughout. Only the profile changes, so she stays the same person.

## Generals (the heroes, called generals in player-facing text)

They speak *through* the queen's link, so they get less telepathic processing than she does. Each needs its own voice
candidate, approved later in the same way as hers.

| General | Voice character | Profile |
|---|---|---|
| **Warlord** (Hemlock, born from the royal larva) | Male, low, clipped, a veteran's economy of words; amused by danger; never says more than an order needs | `warlord`: pitch down 2.5 semitones, very little chorus, short tail |
| **Acid Matron** (Vetch) | Female, older, dry and sardonic, with a rasp; a chemist's precision | `matron`: a strong whisper layer (a hiss), little double |
| **Sky Marshal** | Female or androgynous, quick and bright, a little breathless; she speaks from the air | `marshal`: a slight pitch up, wide chorus (wingbeat shimmer), strong presence |

## Production rules

- One line per file, named by its line id (`assets/voice/queen/<id>.wav`), so any line can be replaced alone.
- The script lives in one place: `assets/voice/lines.tsv` (id, speaker, profile, text). The game, the generation
  script and this doc all read it.
- Dry takes are kept in `D:/Temp/queen-voice/dry/` outside the repo. Re-running the chain reproduces every shipped
  file exactly (fixed seeds, no random state).
- Credits: candidates first (cheap engine, about 0.04 credits a line). The full script is generated only after the
  owner chooses a voice.

## Rebuilding the lines

- Dry takes: Higgsfield `seed_audio`, wav at 44.1 kHz, `speech_rate -15`, `pitch_rate -2`. Voice F is the preset
  "Vesper" (`c3204739-4084-41a3-9dc5-c805b307ec18`) and voice G is the preset "Elena"
  (`ca83ca7f-c186-493d-bd69-0d765fa861b2`). Each line costs about 1.2 credits (two takes). Save them as
  `<dry>/<id>_F.wav` and `<id>_G.wav`.
- Build: `python -I tools/voice/build_lines.py <dry> .`. For each line it prints the length and how well the two
  takes match (the correlation of their loudness envelopes after alignment). Below 0.75 it prints CHECK; regenerate
  one of the two takes. The 2026-11-07 set ranges from 0.75 to 0.90.
- Output: mono 24 kHz 16-bit wav, imported with QOA compression (Godot's default for this project).
- Face: `assets/voice/queen/face-idle.ogv` and `face-speak.ogv`, 320x320 at 24 fps, made with the Higgsfield
  `minimax_h3_max` model from the approved still Q2 (`face.jpg`), with that still as both the first and the last
  frame so the loop is seamless. They are encoded without ffmpeg by Godot's movie writer
  (`tools/video/frames_to_movie.gd`; run it from a bare project whose window is 320x320, because this project's
  window override wins otherwise).
- Generals: their three lines are text only until the owner approves a voice for each.
