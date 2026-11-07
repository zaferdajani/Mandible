"""Two voices as one: the Glass Queen speaks with two throats at once.

Two TTS takes of the same line never have the same timing, so a plain sum turns into two people talking over each
other. This script time-aligns the second take to the first, word for word (DTW on log-spectral features, then a
WSOLA re-synthesis that follows the alignment path), and mixes them. The result is a single dry voice with two
timbres and two pitches moving together. It then goes through telepathy_fx.py like any other dry line.

Usage:  python -I layer.py <lead.wav> <second.wav> <out.wav> [second_gain_db=-3]
"""
import sys
import warnings

import numpy as np
from scipy import signal
from scipy.io import wavfile

warnings.filterwarnings("ignore", category=wavfile.WavFileWarning)
SR = 24000
HOP = 120          # 5 ms analysis hop
WIN = 384


def load(path):
    sr, x = wavfile.read(path)
    x = x.astype(np.float64)
    if x.ndim > 1:
        x = x.mean(axis=1)
    if sr != SR:
        g = np.gcd(sr, SR)
        x = signal.resample_poly(x, SR // g, sr // g)
    return trim(x / (np.abs(x).max() or 1.0))


def trim(x, thresh=0.01):
    idx = np.where(np.abs(x) > thresh)[0]
    if len(idx) == 0:
        return x
    a = max(0, idx[0] - int(0.03 * SR))
    b = min(len(x), idx[-1] + int(0.08 * SR))
    return x[a:b]


def features(x):
    """Log mel-ish band energies, mean-normalised: comparable across two different speakers."""
    _, _, Z = signal.stft(x, SR, nperseg=WIN, noverlap=WIN - HOP, boundary=None)
    mag = np.abs(Z) ** 2
    nb = 40
    edges = np.geomspace(80, 7600, nb + 1)
    freqs = np.fft.rfftfreq(WIN, 1 / SR)
    bands = np.stack([mag[(freqs >= edges[i]) & (freqs < edges[i + 1])].sum(axis=0) for i in range(nb)])
    f = np.log(bands + 1e-8)
    f -= f.mean(axis=1, keepdims=True)
    f /= f.std(axis=1, keepdims=True) + 1e-6
    # add the frame energy so pauses line up with pauses
    e = np.log(mag.sum(axis=0) + 1e-8)
    e = (e - e.mean()) / (e.std() + 1e-6)
    return np.vstack([f, 3 * e]).T


def dtw(a, b, band=0.15):
    """Path through the cost matrix, constrained to a diagonal band (both takes say the same words in order)."""
    n, m = len(a), len(b)
    an = a / (np.linalg.norm(a, axis=1, keepdims=True) + 1e-9)
    bn = b / (np.linalg.norm(b, axis=1, keepdims=True) + 1e-9)
    cost = 1 - an @ bn.T
    w = int(max(n, m) * band) + abs(n - m)
    D = np.full((n + 1, m + 1), np.inf)
    D[0, 0] = 0
    for i in range(1, n + 1):
        c = i * m / n
        lo, hi = max(1, int(c - w)), min(m, int(c + w))
        for j in range(lo, hi + 1):
            D[i, j] = cost[i - 1, j - 1] + min(D[i - 1, j - 1], D[i - 1, j] + 0.3, D[i, j - 1] + 0.3)
    i, j, path = n, m, []
    while i > 0 and j > 0:
        path.append((i - 1, j - 1))
        k = np.argmin([D[i - 1, j - 1], D[i - 1, j], D[i, j - 1]])
        if k == 0:
            i, j = i - 1, j - 1
        elif k == 1:
            i -= 1
        else:
            j -= 1
    return np.array(path[::-1])


def warp(x, path, out_len, frame=1024, hop=256, tol=160):
    """WSOLA that reads the second take at the time the alignment says, for each output frame of the lead."""
    lead_t = path[:, 0] * HOP
    src_t = path[:, 1] * HOP
    # monotone map lead sample -> source sample
    uniq, first = np.unique(lead_t, return_index=True)
    src_of = np.interp(np.arange(out_len), uniq, src_t[first])
    src_of = signal.savgol_filter(src_of, 2001, 2) if out_len > 2001 else src_of
    win = np.hanning(frame)
    y = np.zeros(out_len + frame)
    ws = np.zeros(out_len + frame)
    xp = np.pad(x, (tol + frame, frame + tol))
    off = tol + frame
    prev = None
    for o in range(0, out_len, hop):
        p = int(src_of[min(o, out_len - 1)]) + off - frame // 2
        p = min(max(p, tol), len(xp) - 2 * frame - tol - 1)
        if prev is None or len(prev) < frame:
            best = p
        else:
            seg = xp[p - tol:p + frame + tol]
            c = signal.correlate(seg, prev, mode="valid")
            best = p - tol + int(np.argmax(c))
        y_start = o - frame // 2
        fx = xp[best:best + frame] * win
        a = max(0, y_start)
        y[a:y_start + frame] += fx[a - y_start:]
        ws[a:y_start + frame] += win[a - y_start:]
        prev = xp[best + hop:best + hop + frame]
    ws[ws < 1e-6] = 1
    return (y / ws)[:out_len]


def envelope_match(a, b):
    """How well two takes rise and fall together (1 = identical loudness contour); below ~0.75 the layer smears."""
    env = lambda x: np.convolve(np.abs(x), np.ones(600) / 600, "same")[::120]
    n = min(len(a), len(b))
    return float(np.corrcoef(env(a[:n]), env(b[:n]))[0, 1])


def layer(lead, second, gain_db=-3.0, report=None):
    path = dtw(features(lead), features(second))
    second_w = warp(second, path, len(lead))
    if report is not None:
        report.append(envelope_match(lead, second_w))
    rms = lambda v: np.sqrt(np.mean(v[np.abs(v) > 0.02] ** 2)) + 1e-9
    second_w *= rms(lead) / rms(second_w) * 10 ** (gain_db / 20)
    mix = lead + second_w
    return mix / np.abs(mix).max() * 0.9


def main(argv):
    if len(argv) < 3:
        print(__doc__)
        return 2
    g = float(argv[3]) if len(argv) > 3 else -3.0
    out = layer(load(argv[0]), load(argv[1]), g)
    wavfile.write(argv[2], SR, (out * 32767).astype(np.int16))
    print("done", argv[2])
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
