"""Art review gate, step 2: sets each captured frame beside its approved target.

Usage: python -I tools/art-gate.py <frames-dir> <out-dir>
  <frames-dir> is what `--art-capture=<dir>` wrote. For every view with a target it writes <out-dir>/<view>.jpg
  (target left, game frame right, same height, no captions, so the reviewer judges pictures, not labels) and
  <out-dir>/review.md, the list of pairs plus the reviewer prompt. docs/art-pipeline.md explains the rest.
"""
import os
import sys

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
TARGETS = os.path.join(ROOT, "docs", "art-targets")

# view (frame name) -> approved target, and what the frame must show
VIEWS = {
    "a-opening": ("a-opening-tube.jpg", "level 1 as it opens: the broken lab tube on the woodland trail"),
    "a-tube-close": ("a-opening-tube.jpg", "close view of the broken lab tube"),
    "a-opening-low": ("a-opening-tube.jpg", "level 1 on the Low (phone) tier"),
    "b-yard": ("b-yard-ground.jpg", "the open yard: soil, stones, grass, debris"),
    "c-level-map": ("c-level-map-section.jpg", "the colony level map as a nest cross-section"),
    "d-chamber": ("d-chamber.jpg", "nest chambers seen through the glass earth"),
    "e-food-crumb": ("e-food-small.jpg", "a crumb food pile"),
    "e-food-seed": ("e-food-small.jpg", "a seed food pile"),
    "e-food-sugar": ("e-food-small.jpg", "a sugar food source"),
    "e-food-berry": ("e-food-fruit-protein.jpg", "a berry food source"),
    "f-fog-of-war": ("f-fog-of-war.jpg", "level start under fog of war"),
}

PROMPT = """You are an independent art reviewer for a photoreal macro nature-documentary RTS about ants.
Open ONLY this image: {pair}
Its left half is the approved TARGET; its right half is a FRAME from the game ({what}).
The frame is seen from the game's camera, which may be farther out than the target; judge whether the frame
reaches the target's quality at its own framing, not a pixel match. Ignore any UI.

Score the FRAME 1-5 on each (5 = matches the target's standard):
- repetition: no visible copies of one mesh or texture, no rhythm or grid in placement
- primitives: no shape reads as a code primitive (blob, ellipsoid, cylinder, cone, flat card)
- scale: everything is believable at ant scale relative to the ants
- material: surfaces read as real materials (soil grain, stone mineral, glass refraction, food texture)
- light: warm directional sun, grounded contact shadows, no flat or plastic shading
- palette: warm earth palette, no grey cast, no cartoon saturation
- readability: the gameplay subject (ants, food, tube, chambers, fog edge) reads at a glance

Reply with JSON only:
{{"view": "{view}", "scores": {{"repetition": n, "primitives": n, "scale": n, "material": n, "light": n,
"palette": n, "readability": n}}, "blockers": ["..."], "pass": true|false, "fix_first": "one sentence"}}
pass is true only when every score is 4 or 5 and blockers is empty.
"""


def pair(target_path, frame_path, out_path, height=720):
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
    lines = ["# Art gate pairs", "", "Spawn one fresh reviewer per pair with the prompt below; give it nothing else.", ""]
    for view, (target, what) in VIEWS.items():
        fp = os.path.join(frames, view + ".png")
        if not os.path.exists(fp):
            continue
        op = os.path.abspath(os.path.join(out, view + ".jpg"))
        pair(os.path.join(TARGETS, target), fp, op)
        lines += [f"## {view}", "", "```", PROMPT.format(pair=op, what=what, view=view).strip(), "```", ""]
        print(op)
    open(os.path.join(out, "review.md"), "w", encoding="utf-8").write("\n".join(lines))


if __name__ == "__main__":
    main()
