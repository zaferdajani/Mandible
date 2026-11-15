"""The queen answers in words when her nest is clicked or she is given an order (owner, 2026-11-15).

    python -I queen_barks.py <dry_dir> <repo_root>

No new recording: each answer is one sentence cut out of her approved dry takes (<id>_F.wav + <id>_G.wav) at the
pauses between sentences, then layered (FG2) and sent through telepathy_fx exactly as build_lines.py does.
Output replaces the queen's old creature calls: assets/audio/voc_queen_<n>.wav (clicked), ack_queen_<n>.wav (ordered).
"""
import os
import sys

import numpy as np

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import layer  # noqa: E402
import telepathy_fx as fx  # noqa: E402

# output name -> (line id, sentence index within the line, the words)
BARKS = {
    "voc_queen_0": ("l1_alone", 2, "You hear me."),
    "voc_queen_1": ("l1_stocked", 1, "Give the word."),
    "voc_queen_2": ("l1_near", 0, "Closer."),
    "ack_queen_0": ("l1_alone", 6, "Rise."),
    "ack_queen_1": ("open_11", 2, "Do not stop."),
}


def sentences(x, sr):
    """Spans of speech separated by at least 0.22 s of quiet."""
    n = x / (np.abs(x).max() + 1e-9)
    w = int(sr * 0.02)
    on = np.sqrt(np.convolve(n * n, np.ones(w) / w, "same")) > 0.03
    gap = int(sr * 0.22)
    out, i = [], 0
    while i < len(x):
        if on[i]:
            j = i
            while j < len(x) and (on[j] or on[j:j + gap].any()):
                j += 1
            out.append((i, j))
            i = j
        else:
            i += 1
    return out


def cut(path, idx):
    x = layer.load(path)
    a, b = sentences(x, fx.SR)[idx]
    pad = int(fx.SR * 0.07)
    y = x[max(0, a - pad):min(len(x), b + pad)].copy()
    f = int(fx.SR * 0.03)
    y[:f] *= np.linspace(0, 1, f)
    y[-f:] *= np.linspace(1, 0, f)
    return y


def main(argv):
    dry, root = argv[0], argv[1]
    for name, (line, idx, words) in BARKS.items():
        lead, second = cut(os.path.join(dry, line + "_F.wav"), idx), cut(os.path.join(dry, line + "_G.wav"), idx)
        rep = []
        v = fx.process(layer.layer(lead, second, -5.0, rep), fx.PROFILES["queen1"])
        fx.save(os.path.join(root, "assets/audio", name + ".wav"), v)
        print(f"{name}  {len(v) / fx.SR:4.2f}s  match {rep[0]:.2f}  \"{words}\"")
    return 0


if __name__ == "__main__":
    import warnings
    warnings.filterwarnings("ignore")
    sys.exit(main(sys.argv[1:]))
