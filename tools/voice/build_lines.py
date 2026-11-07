"""Builds every voiced line of the script from its dry takes.

    python -I build_lines.py <dry_dir> <repo_root>

For each row of assets/voice/lines.tsv whose speaker has an approved voice:
  queen:  <dry_dir>/<id>_F.wav (lead, voice F) + <id>_G.wav (layered under it at -5 dB, voice G), the owner's
          choice of 2026-11-07 ("FG2"), then telepathy_fx with the row's profile (queen1/2/3).
Output: assets/voice/<speaker>/<id>.wav. Lines without dry takes are skipped (the game shows their text alone).
Everything is deterministic: rebuild one line by deleting its output and running this again.
"""
import csv
import os
import sys

import numpy as np
from scipy.io import wavfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import layer  # noqa: E402
import telepathy_fx as fx  # noqa: E402

LAYER_GAIN_DB = -5.0   # FG2


def main(argv):
    dry, root = argv[0], argv[1]
    rows = list(csv.DictReader(open(os.path.join(root, "assets/voice/lines.tsv"), encoding="utf-8"), delimiter="\t"))
    built = skipped = 0
    for r in rows:
        if r["speaker"] != "queen":
            continue
        f, g = os.path.join(dry, r["id"] + "_F.wav"), os.path.join(dry, r["id"] + "_G.wav")
        out = os.path.join(root, "assets/voice", r["speaker"], r["id"] + ".wav")
        if not (os.path.exists(f) and os.path.exists(g)):
            skipped += 1
            print("missing takes:", r["id"])
            continue
        rep = []
        mix = layer.layer(layer.load(f), layer.load(g), LAYER_GAIN_DB, rep)
        v = fx.process(mix, fx.PROFILES[r["profile"]])
        fx.save(out, v)
        built += 1
        print(f"{r['id']:18s} {len(v) / fx.SR:5.2f}s  match {rep[0]:.2f}{'  CHECK' if rep[0] < 0.75 else ''}")
    print(f"built {built}, skipped {skipped}")
    return 0


if __name__ == "__main__":
    import warnings
    warnings.filterwarnings("ignore")
    sys.exit(main(sys.argv[1:]))
