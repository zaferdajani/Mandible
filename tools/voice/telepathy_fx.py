"""Telepathy voice chain for Mandible's speaking characters (the Glass Queen, later the generals).

Reproducible, free and offline: numpy + scipy only, fixed random seeds, one profile per character.
Input: a dry TTS line (wav). Output: the processed line (wav, mono 24 kHz, 16-bit) for the game.

The chain (docs/queen-voice.md explains the why):
  1. main voice: a small pitch-and-formant drop (resample + WSOLA time-restore), so she sounds larger than the speaker
  2. "hive" double: the same line an octave down, quiet, a few ms late: the colony speaking under her
  3. whisper layer: the line's spectral envelope driven by noise, slightly ahead: the thought before the word
  4. chorus: two slow modulated delays, which smear the edges like many mouths at once
  5. close-mic presence boost and a low cut; then a short dark room tail (synthetic, seeded) for the inner-skull space
  6. loudness: RMS to a fixed target, peak-limited at -1 dBFS, so every line in the game plays at the same level

Usage:  python -I telepathy_fx.py <profile> <in.wav> <out.wav>       (profiles: see PROFILES)
        python -I telepathy_fx.py <profile> --dir <in_dir> <out_dir>  (every .wav in a folder)
"""
import os
import sys

import numpy as np
from scipy import signal
from scipy.io import wavfile

SR = 24000

# Each stage grows with the queen (see the voice spec): `stage1` is the broken-tube survivor, `stage3` the empress.
PROFILES = {
    # The owner's brief (2026-11-07): mature, deep, resonant, cold and arrogant, slow, with a dual-tone or robotic
    # resonance under her words. `res_db` is that resonance: a ring-modulated copy of the octave layer plus a short
    # metallic comb, felt more than heard. 0-dB defaults for the generals keep it off.
    #            pitch       octave      whisper     chorus       reverb                 presence     resonance
    "queen1":   dict(semis=-1.5, oct_db=-14, wh_db=-19, ch_mix=0.24, rv_mix=0.16, rv_t=1.5, pres_db=2.5, res_db=-17),
    "queen2":   dict(semis=-2.0, oct_db=-12, wh_db=-17, ch_mix=0.28, rv_mix=0.19, rv_t=1.8, pres_db=2.5, res_db=-15),
    "queen3":   dict(semis=-2.5, oct_db=-10, wh_db=-15, ch_mix=0.34, rv_mix=0.22, rv_t=2.1, pres_db=2.0, res_db=-12),
    # generals: less telepathic, more body (they are speaking through her link, not with it)
    "warlord":  dict(semis=-2.5, oct_db=-20, wh_db=-30, ch_mix=0.10, rv_mix=0.10, rv_t=0.9, pres_db=2.0),
    "matron":   dict(semis=-1.0, oct_db=-24, wh_db=-14, ch_mix=0.18, rv_mix=0.12, rv_t=1.1, pres_db=3.5),
    "marshal":  dict(semis=0.5,  oct_db=-30, wh_db=-24, ch_mix=0.25, rv_mix=0.12, rv_t=1.0, pres_db=4.0),
}


def load(path):
    sr, x = wavfile.read(path)
    x = x.astype(np.float64)
    if x.ndim > 1:
        x = x.mean(axis=1)
    peak = np.abs(x).max() or 1.0
    x /= peak
    if sr != SR:
        g = np.gcd(sr, SR)
        x = signal.resample_poly(x, SR // g, sr // g)
    return x


def wsola(x, rate, frame=1024, hop=256, tol=256):
    """Time-stretch by `rate` (>1 = longer) keeping pitch; waveform-similarity overlap-add."""
    win = np.hanning(frame)
    out_len = int(len(x) * rate) + frame
    y = np.zeros(out_len)
    wsum = np.zeros(out_len)
    xp = np.pad(x, (tol, frame + tol))
    prev = None
    n_out = 0
    k = 0
    while True:
        pos_in = int(k * hop / rate) + tol
        if pos_in + frame + tol >= len(xp) or n_out + frame >= out_len:
            break
        if prev is None:
            best = pos_in
        else:
            seg = xp[pos_in - tol:pos_in + frame + tol]
            c = signal.correlate(seg, prev, mode="valid")
            best = pos_in - tol + int(np.argmax(c))
        frame_x = xp[best:best + frame]
        y[n_out:n_out + frame] += frame_x * win
        wsum[n_out:n_out + frame] += win
        prev = xp[best + hop:best + hop + frame]
        if len(prev) < frame:
            break
        n_out += hop
        k += 1
    wsum[wsum < 1e-6] = 1.0
    return y / wsum


def pitch_shift(x, semis):
    """Pitch and formants together (a larger body), length kept."""
    if abs(semis) < 1e-3:
        return x
    f = 2.0 ** (semis / 12.0)
    stretched = wsola(x, f)                       # longer by f at the same pitch
    n = int(round(len(stretched) / f))
    y = signal.resample(stretched, n)             # squeeze back: pitch * f
    return fit(y, len(x))


def fit(y, n):
    return y[:n] if len(y) >= n else np.pad(y, (0, n - len(y)))


def whisper(x, seed):
    """The line's spectral envelope on noise: an unvoiced shadow of every word."""
    rng = np.random.default_rng(seed)
    f, t, Z = signal.stft(x, SR, nperseg=1024)
    mag = np.abs(Z)
    phase = rng.uniform(-np.pi, np.pi, Z.shape)
    _, y = signal.istft(mag * np.exp(1j * phase), SR, nperseg=1024)
    b, a = signal.butter(2, [1800 / (SR / 2), 9000 / (SR / 2)], "band")
    return fit(signal.lfilter(b, a, y), len(x))


def mod_delay(x, base_ms, depth_ms, rate_hz, phase=0.0):
    n = np.arange(len(x))
    d = (base_ms + depth_ms * np.sin(2 * np.pi * rate_hz * n / SR + phase)) * SR / 1000.0
    idx = n - d
    i0 = np.clip(np.floor(idx).astype(int), 0, len(x) - 1)
    i1 = np.clip(i0 + 1, 0, len(x) - 1)
    frac = idx - np.floor(idx)
    y = x[i0] * (1 - frac) + x[i1] * frac
    y[idx < 0] = 0
    return y


def delay(x, ms):
    k = int(SR * ms / 1000.0)
    return np.concatenate([np.zeros(k), x])[:len(x)] if k >= 0 else np.concatenate([x[-k:], np.zeros(-k)])


def room(t60, seed):
    """A dark, dense synthetic tail: seeded noise, exponential decay, darker as it dies."""
    rng = np.random.default_rng(seed)
    n = int(SR * t60)
    tt = np.arange(n) / SR
    ir = rng.standard_normal(n) * np.exp(-6.9 * tt / t60)
    b, a = signal.butter(1, 3500 / (SR / 2))
    ir = signal.lfilter(b, a, ir)
    ir[: int(SR * 0.025)] = 0  # pre-delay: the tail starts behind the voice
    return ir / np.sqrt(np.sum(ir ** 2))


def peaking(x, f0, gain_db, q=0.9):
    A = 10 ** (gain_db / 40)
    w = 2 * np.pi * f0 / SR
    al = np.sin(w) / (2 * q)
    b = [1 + al * A, -2 * np.cos(w), 1 - al * A]
    a = [1 + al / A, -2 * np.cos(w), 1 - al / A]
    return signal.lfilter(b, a, x)


def resonance(x, octv):
    """The alien undertone: the octave layer ring-modulated at a low, slightly drifting rate (a machine-like buzz on
    every vowel), and the voice through a short comb (a metallic, hollow-throat ring at ~180 Hz)."""
    n = np.arange(len(x)) / SR
    carrier = np.sin(2 * np.pi * (62 + 3 * np.sin(2 * np.pi * 0.13 * n)) * n)
    ring = octv * carrier
    k = int(SR / 180)
    comb = signal.lfilter([1.0], np.r_[1.0, np.zeros(k - 1), -0.72], x) * 0.28
    b, a = signal.butter(2, [120 / (SR / 2), 3200 / (SR / 2)], "band")
    return signal.lfilter(b, a, 0.6 * ring + comb)


def db(v):
    return 10 ** (v / 20.0)


def process(x, p, seed=7):
    tail = np.zeros(int(SR * p["rv_t"]))
    x = np.concatenate([x, tail])
    main = pitch_shift(x, p["semis"])
    octv = pitch_shift(x, p["semis"] - 12.0)
    wh = whisper(main, seed)
    v = main + db(p["oct_db"]) * delay(octv, 9) + db(p["wh_db"]) * delay(wh, -14)
    if "res_db" in p:
        r = resonance(main, octv)
        v += db(p["res_db"]) * r / (np.sqrt(np.mean(r ** 2)) + 1e-9) * np.sqrt(np.mean(main ** 2))
    ch = 0.5 * (mod_delay(v, 14, 3.5, 0.31) + mod_delay(v, 21, 4.5, 0.19, 1.7))
    v = (1 - p["ch_mix"]) * v + p["ch_mix"] * ch
    b, a = signal.butter(2, 90 / (SR / 2), "high")
    v = signal.lfilter(b, a, v)
    v = peaking(v, 3200, p["pres_db"])
    wet = signal.fftconvolve(v, room(p["rv_t"], seed + 1))[: len(v)]
    v = (1 - p["rv_mix"]) * v + p["rv_mix"] * wet * 0.6
    # level: RMS of the voiced part to -20 dBFS, then a soft limit at -1 dBFS
    voiced = v[np.abs(v) > 0.02 * np.abs(v).max()]
    v *= db(-20) / (np.sqrt(np.mean(voiced ** 2)) + 1e-9)
    ceiling = db(-1)
    v = np.tanh(v / ceiling) * ceiling
    # trim the tail's silence, keep a short fade
    alive = np.where(np.abs(v) > db(-60))[0]
    end = min(len(v), (alive[-1] if len(alive) else len(v)) + int(SR * 0.05))
    v = v[:end]
    fade = int(SR * 0.04)
    v[-fade:] *= np.linspace(1, 0, fade)
    return v


def save(path, v):
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    wavfile.write(path, SR, (np.clip(v, -1, 1) * 32767).astype(np.int16))


def main(argv):
    if len(argv) < 3 or argv[0] not in PROFILES:
        print(__doc__)
        return 2
    p = PROFILES[argv[0]]
    if argv[1] == "--dir":
        src, dst = argv[2], argv[3]
        for name in sorted(os.listdir(src)):
            if name.lower().endswith(".wav"):
                save(os.path.join(dst, name), process(load(os.path.join(src, name)), p))
                print("done", name)
    else:
        save(argv[2], process(load(argv[1]), p))
        print("done", argv[2])
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
