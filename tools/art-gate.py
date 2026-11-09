"""Art review gate, step 2: prepares both checks of the two-part gate for fresh reviewers.

Usage: python -I tools/art-gate.py <frames-dir> <out-dir>
  <frames-dir> is what `--art-capture=<dir>` wrote. For every view it writes:
  - <out-dir>/<view>-art.jpg: the approved target (left) beside the close frame `<view>-close.png` (right), for the
    ART check: repetition, primitives, scale, material, light and palette, at the target's own framing;
  - <out-dir>/<view>-read.jpg: the play-zoom frame `<view>.png` alone, for the READABILITY check against the
    written spec below (a player's question, not a comparison with the macro photo);
  - <out-dir>/review.md: one reviewer prompt per check. docs/art-pipeline.md explains the rest.
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TARGETS = os.path.join(ROOT, "docs", "art-targets")

# view -> (approved target, what the frame shows, readability spec at play zoom)
VIEWS = {
    "a-opening": ("a-opening-tube.jpg", "level 1 as it opens: the broken lab tube on the woodland trail",
                  "A player can see at once that a glass tube lies broken at the start of the trail, that the black "
                  "ants (the queen larger) have come out of it, and which way the trail leads."),
    "b-yard": ("b-yard-ground.jpg", "the open yard: soil, stones, grass, debris, ants foraging",
               "Ants stand out from the soil; the ant trail is visible; stones, grass, sticks and any food are told "
               "apart from each other and from the soil."),
    "c-level-map": ("c-level-map-section.jpg", "the colony level map as a nest cross-section",
                    "Each chamber is a separate, recognisable place; a player can tell which chambers are dug or won "
                    "and which are still locked; the numerals and names can be read; the selected chamber stands out."),
    "d-chamber": ("d-chamber.jpg", "nest chambers seen through the glass earth",
                  "Chambers read as hollows in the earth; eggs, larvae and cocoons are told apart; the queen and the "
                  "worker ants are visible and distinct from the brood."),
    "e-food-crumb": ("e-food-small.jpg", "a crumb food pile", "The pile is recognisably bread crumbs, stands out from the soil, and is not mistaken for stones or seeds."),
    "e-food-seed": ("e-food-small.jpg", "a seed food pile", "The pile is recognisably seeds, stands out from the soil, and is not mistaken for stones or crumbs."),
    "e-food-sugar": ("e-food-small.jpg", "a sugar food source", "The source is recognisably sugar, stands out from the soil, and is not mistaken for stones."),
    "e-food-berry": ("e-food-fruit-protein.jpg", "a berry food source", "The source is recognisably fruit or berries, stands out from the soil, and is not mistaken for stones."),
    # the ants (target G): close views are art-checked with ANT_ART; g-play is the readability check at play zoom
    "g-castes-black": ("g-ant-lasius-castes.jpg", "black garden ant castes: worker, soldier, major, acid gunner, winged ant, queen", ""),
    "g-castes-red": ("g-ant-solenopsis-castes.jpg", "red fire ant castes: worker, soldier, major, acid gunner, winged ant, queen", ""),
    "g-warlord": ("g-ant-warlord.jpg", "the Warlord hero of each colony, black left and red right", ""),
    "g-matron": ("g-ant-matron-black.jpg", "the Acid Matron hero of each colony, black left and red right", ""),
    "g-marshal": ("g-ant-marshal.jpg", "the Sky Marshal hero of each colony, black left and red right", ""),
    "g-eyes": ("g-ant-eyes-worker.jpg", "a black garden ant worker's head, close", ""),
    "g-eyes-queen": ("g-ant-eyes-alate.jpg", "a black garden ant queen's head, close", ""),
    # the mutated generals (target J) at level 10, every growth in
    "g-warlord-l10": ("j-general-warlord-black.jpg", "the Warlord general of each colony at level 10, black left and red right", ""),
    "g-matron-l10": ("j-general-matron-black.jpg", "the Acid Matron general of each colony at level 10, black left and red right", ""),
    "g-marshal-l10": ("j-general-marshal-black.jpg", "the Sky Marshal general of each colony at level 10, black left and red right", ""),
    "g-play": ("", "a black foraging column meeting a red raiding party",
               "Each ant reads as an ant (head, body, legs) and not as a dot or a blob; the black and the red colonies "
               "are told apart at a glance; soldiers and majors read larger than workers; the ants stand out from the "
               "soil and the litter."),
    # level 1's teamwork lesson (targets K1-K3)
    "k1-pond": ("k1-pond-island.jpg", "level 1's rain pond: a dirt island with a dead pale queen ant of another strain and a second broken glass tube, straws on the shore",
                "A player sees at once that water blocks the way to an island; the dead queen and the glass tube on the island are visible; "
                "the straws on the shore read as loose objects, not as part of the ground."),
    "k2-straw-crew": ("k2-straw-crew.jpg", "a crew of worker ants carrying one long straw toward the pond",
                      "The straw reads as one long object; the ants holding it are visible along it; a player can tell the crew carries it together."),
    "k3-straw-bridge": ("k3-straw-bridge.jpg", "a finished bridge of five straws from the shore to the island, workers crossing to the dead queen",
                        "The bridge reads as straws laid side by side across the water from shore to island; the ants and the dead queen on the island are visible."),
    "f-fog-of-war": ("f-fog-of-war.jpg", "level start under fog of war",
                     "Ground never seen is black, ground seen before is dimmed, ground in sight is clear: the three "
                     "are told apart; the ants and the trail inside the visible area read; the edge is soft, not a hard cut."),
}

ART = """You are an independent art reviewer for a photoreal macro nature-documentary RTS about ants.
Open ONLY this image: {img}
Its left half is the approved TARGET; its right half is a close FRAME from the game ({what}) at about the
target's framing. Judge whether what the frame shows reaches the quality of the same kind of thing in the target.
It need not match the composition, and it need not contain everything the target contains: do NOT mark down for
content that is absent (an ant column, honey, droplets, extra food types) and do NOT ask for it. Judge only the
things that are in the frame. Ants are rebuilt by a separate session: do not score the ants' own bodies (shape,
legs, chitin); judge everything else. Ignore any UI.

Score the FRAME 1-5 on each (5 = matches the target's standard):
- repetition: no visible copies of one mesh or texture, no rhythm or grid in placement
- primitives: no shape reads as a code primitive (blob, ellipsoid, cylinder, cone, flat card)
- scale: everything is believable at ant scale relative to the ants
- material: surfaces read as real materials (soil grain, stone mineral, glass refraction, food texture)
- light: warm directional sun, grounded contact shadows, no flat or plastic shading
- palette: warm earth palette, no grey cast, no cartoon saturation

Reply with JSON only:
{{"view": "{view}", "check": "art", "scores": {{"repetition": n, "primitives": n, "scale": n, "material": n,
"light": n, "palette": n}}, "blockers": ["..."], "pass": true|false, "fix_first": "one sentence"}}
pass is true only when every score is 4 or 5 and blockers is empty.
"""

ANT_ART = """You are an independent art reviewer for a photoreal macro nature-documentary RTS about ants.
Open ONLY this image: {img}
Its left half is the approved TARGET (a macro photograph of real ants); its right half is a close FRAME from the
game ({what}) at about the target's framing. Judge whether the ants in the frame reach the standard of the ants in
the target. It need not match the composition or the pose. Judge the ants only, not the ground. Ignore any UI.

Score the FRAME's ants 1-5 on each (5 = matches the target's standard):
- anatomy: correct real ant anatomy and proportions (head with eyes, mandibles and elbowed antennae; a three-part
  thorax; the waist node(s); a segmented gaster; six jointed legs with femur, tibia and tarsus)
- primitives: no part reads as a code primitive (blob, ellipsoid, sausage, stick, cone)
- eyes: compound eyes read as real eyes (dark, oval, set on the sides, a small live catchlight), not cartoon beads
- material: chitin reads as real cuticle (matte to satin, fine sculpture, setae catching the light), not plastic
- light: warm directional sun, grounded contact shadows, no flat or plastic shading
- palette: warm natural colour, no grey cast, no cartoon saturation

Reply with JSON only:
{{"view": "{view}", "check": "art", "scores": {{"anatomy": n, "primitives": n, "eyes": n, "material": n,
"light": n, "palette": n}}, "blockers": ["..."], "pass": true|false, "fix_first": "one sentence"}}
pass is true only when every score is 4 or 5 and blockers is empty.
"""

READ = """You are an independent playtester for a real-time strategy game about an ant colony.
Open ONLY this image: {img}
It is a frame from the game at the normal play zoom ({what}). Ignore any UI.
Judge only READABILITY against this spec:
  {spec}
Score readability 1-5 (5 = everything in the spec reads at a glance, 3 = readable with effort, 1 = not readable).

Reply with JSON only:
{{"view": "{view}", "check": "read", "readability": n, "unreadable": ["what cannot be told apart"], "pass": true|false,
"fix_first": "one sentence"}}
pass is true only when readability is 4 or 5.
"""


def side_by_side(target_path, frame_path, out_path, height=720):
    t = Image.open(target_path).convert("RGB")
    f = Image.open(frame_path).convert("RGB")
    t = t.resize((round(t.width * height / t.height), height), Image.LANCZOS)
    f = f.resize((round(f.width * height / f.height), height), Image.LANCZOS)
    im = Image.new("RGB", (t.width + 12 + f.width, height), (0, 0, 0))
    im.paste(t, (0, 0))
    im.paste(f, (t.width + 12, 0))
    im.save(out_path, quality=90)


def main():
    frames, out = sys.argv[1], sys.argv[2]
    os.makedirs(out, exist_ok=True)
    lines = ["# Art gate", "", "Spawn one fresh reviewer per section with its prompt; give it nothing else.", ""]
    for view, (target, what, spec) in VIEWS.items():
        close = os.path.join(frames, view + "-close.png")
        play = os.path.join(frames, view + ".png")
        if os.path.exists(close):
            img = os.path.abspath(os.path.join(out, view + "-art.jpg"))
            side_by_side(os.path.join(TARGETS, target), close, img)
            prompt = ANT_ART if view.startswith("g-") else ART
            lines += [f"## {view} art", "", "```", prompt.format(img=img, what=what, view=view).strip(), "```", ""]
            print(img)
        if os.path.exists(play):
            img = os.path.abspath(os.path.join(out, view + "-read.jpg"))
            Image.open(play).convert("RGB").save(img, quality=90)
            lines += [f"## {view} read", "", "```", READ.format(img=img, what=what, view=view, spec=spec).strip(), "```", ""]
            print(img)
    open(os.path.join(out, "review.md"), "w", encoding="utf-8").write("\n".join(lines))


if __name__ == "__main__":
    main()
