"""Synthesizes every Mandible sound into assets/audio as 16-bit WAV.

Run: python tools/gen_audio.py
All sounds are original (procedural); no third-party samples.
"""
import os
import sys
import wave

import numpy as np
from scipy import signal

SR = 44100
OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "assets", "audio")
rng = np.random.default_rng(7)


# ------------------------------------------------------------------ helpers
def t_axis(dur):
    return np.arange(int(dur * SR)) / SR


def noise(n):
    return rng.standard_normal(n)


def pink(n):
    # Voss-ish: filter white noise with a -3 dB/oct approximation
    b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
    a = [1, -2.494956002, 2.017265875, -0.522189400]
    return signal.lfilter(b, a, noise(n)) * 8


def bp(x, lo, hi, order=2):
    sos = signal.butter(order, [lo, hi], btype="band", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def lp(x, f, order=2):
    sos = signal.butter(order, f, btype="low", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def lp_tv(x, cut, blk=512):
    """Time-varying lowpass: cutoff array sampled once per block."""
    y = np.zeros_like(x)
    zi = None
    for i in range(0, len(x), blk):
        sos = signal.butter(2, float(np.clip(cut[i], 60, SR * 0.45)), "low", fs=SR, output="sos")
        if zi is None:
            zi = np.zeros((sos.shape[0], 2))
        y[i:i + blk], zi = signal.sosfilt(sos, x[i:i + blk], zi=zi)
    return y


def hp(x, f, order=2):
    sos = signal.butter(order, f, btype="high", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def env_exp(n, decay):
    return np.exp(-np.arange(n) / (decay * SR))


def adsr(n, a, r):
    e = np.ones(n)
    na, nr = int(a * SR), int(r * SR)
    if na:
        e[:na] = np.linspace(0, 1, na)
    if nr:
        e[-nr:] *= np.linspace(1, 0, nr)
    return e


def reverb_ir(dur, decay, stereo=True, damp=4000):
    n = int(dur * SR)
    chans = []
    for _ in range(2 if stereo else 1):
        ir = noise(n) * np.exp(-np.arange(n) / (decay * SR))
        ir = lp(ir, damp)
        ir[0] = 0
        chans.append(ir / np.sqrt(np.sum(ir ** 2)))
    return chans


def add_reverb(x, wet=0.3, dur=2.5, decay=0.7, damp=4000):
    """x: mono (n,) or stereo (n,2) -> stereo with tail appended."""
    if x.ndim == 1:
        x = np.stack([x, x], axis=1)
    irs = reverb_ir(dur, decay, True, damp)
    n = x.shape[0] + len(irs[0]) - 1
    out = np.zeros((n, 2))
    for c in range(2):
        out[: x.shape[0], c] += x[:, c] * (1 - wet)
        out[:, c] += signal.fftconvolve(x[:, c], irs[c])[:n] * wet
    return out


def loopify(x, length):
    """Wrap everything past `length` samples back onto the start for a seamless loop."""
    n = int(length * SR)
    out = x[:n].copy()
    tail = x[n:]
    while len(tail):
        m = min(len(tail), n)
        out[:m] += tail[:m]
        tail = tail[m:]
    return out


def pan(x, p):
    """p -1..1"""
    l = np.cos((p + 1) * np.pi / 4)
    r = np.sin((p + 1) * np.pi / 4)
    return np.stack([x * l, x * r], axis=1)


def normalize(x, peak_db):
    peak = np.max(np.abs(x)) + 1e-9
    return x * (10 ** (peak_db / 20) / peak)


def fade(x, fi=0.002, fo=0.01):
    n = x.shape[0]
    e = np.ones(n)
    a, b = int(fi * SR), int(fo * SR)
    if a:
        e[:a] = np.linspace(0, 1, a)
    if b:
        e[-b:] = np.linspace(1, 0, b)
    return x * (e[:, None] if x.ndim == 2 else e)


def save(name, x, peak_db=-3.0):
    x = normalize(x, peak_db)
    x = np.clip(x, -1, 1)
    data = (x * 32767).astype(np.int16)
    ch = 1 if data.ndim == 1 else data.shape[1]
    path = os.path.join(OUT, name + ".wav")
    with wave.open(path, "wb") as w:
        w.setnchannels(ch)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(data.tobytes())
    print(f"  {name}.wav  {x.shape[0] / SR:.2f}s  {'stereo' if ch == 2 else 'mono'}")


# ------------------------------------------------------------------ instruments
def click(n_len, f_lo, f_hi, decay):
    n = int(n_len * SR)
    x = noise(n) * env_exp(n, decay)
    return bp(x, f_lo, f_hi)


def resonator(x, f, q):
    b, a = signal.iirpeak(f, q, fs=SR)
    return signal.lfilter(b, a, x)


def karplus(freq, dur, bright=0.5, damping=0.996, pluck_pos=0.18):
    n = int(dur * SR)
    period = SR / freq
    p = int(period)
    frac = period - p
    buf = noise(p) * 0.8
    buf = lp(buf, 1500 + bright * 6000, 1) if p > 12 else buf
    # pluck position comb
    k = max(1, int(p * pluck_pos))
    buf = buf - np.roll(buf, k)
    out = np.zeros(n)
    y = np.concatenate([buf, np.zeros(n)])
    for i in range(p + 1, n + p):
        a = y[i - p]
        b = y[i - p - 1]
        y[i] = damping * ((1 - frac) * (0.5 * (a + b)) + frac * (0.5 * (b + y[i - p - 2])))
    out = y[p:p + n]
    return out * adsr(n, 0.002, 0.05)


def oud(freq, dur, vel=1.0):
    x = karplus(freq, dur, bright=0.55, damping=0.9965)
    # body resonance
    body = resonator(x, 220, 3) * 0.4 + resonator(x, 470, 4) * 0.25
    return (x + body) * vel


def frame_drum(kind, vel=1.0):
    if kind == "doum":
        n = int(0.7 * SR)
        t = np.arange(n) / SR
        f = 58 + 40 * np.exp(-t * 30)
        ph = 2 * np.pi * np.cumsum(f) / SR
        x = np.sin(ph) * env_exp(n, 0.22)
        x += lp(noise(n), 600) * env_exp(n, 0.03) * 0.5
    elif kind == "tek":
        n = int(0.25 * SR)
        x = bp(noise(n), 1800, 7000) * env_exp(n, 0.035)
        x += np.sin(2 * np.pi * 380 * np.arange(n) / SR) * env_exp(n, 0.04) * 0.4
    else:  # ka (rim)
        n = int(0.2 * SR)
        x = bp(noise(n), 900, 4000) * env_exp(n, 0.025)
    return x * vel


def saw_pad(freqs, dur, cutoff=900, detune=0.006):
    t = t_axis(dur)
    x = np.zeros_like(t)
    for f in freqs:
        for d in (-detune, 0, detune):
            ph = rng.random()
            x += signal.sawtooth(2 * np.pi * f * (1 + d) * t + ph * 6.28)
    lfo = 0.5 + 0.5 * np.sin(2 * np.pi * 0.11 * t + rng.random() * 6)
    x = lp_tv(x, cutoff * (0.6 + 0.6 * lfo))
    return x * adsr(len(t), min(2.5, dur * 0.35), min(2.5, dur * 0.35)) / (len(freqs) * 3)


def fm_bell(freq, dur, ratio=3.5, index=2.0, decay=0.4):
    t = t_axis(dur)
    mod = np.sin(2 * np.pi * freq * ratio * t) * index * np.exp(-t / (decay * 0.5))
    x = np.sin(2 * np.pi * freq * t + mod) * np.exp(-t / decay)
    return x * adsr(len(t), 0.002, 0.02)


def note(name):
    names = {"C": -9, "C#": -8, "Db": -8, "D": -7, "D#": -6, "Eb": -6, "E": -5, "F": -4, "F#": -3, "Gb": -3,
             "G": -2, "G#": -1, "Ab": -1, "A": 0, "A#": 1, "Bb": 1, "B": 2}
    pitch, octave = name[:-1], int(name[-1])
    return 440 * 2 ** ((names[pitch] + (octave - 4) * 12) / 12)


# ------------------------------------------------------------------ sounds
def amb_yard():
    L = 48.0
    n = int((L + 3) * SR)
    t = np.arange(n) / SR
    out = np.zeros((n, 2))
    # wind bed
    for c in range(2):
        w = lp(pink(n), 420, 2)
        gust = 0.55 + 0.45 * np.sin(2 * np.pi * (0.045 + c * 0.01) * t + c) * np.sin(2 * np.pi * 0.017 * t + 2)
        out[:, c] += w * gust * 0.5
    # leaf rustles
    for _ in range(14):
        start = rng.uniform(0, L)
        d = rng.uniform(0.4, 1.4)
        m = int(d * SR)
        r = bp(noise(m), 1800, 7500) * np.sin(np.linspace(0, np.pi, m)) ** 2
        r *= 0.5 + 0.5 * np.abs(lp(noise(m), 30))
        s = int(start * SR)
        out[s:s + m] += pan(r * rng.uniform(0.06, 0.16), rng.uniform(-0.9, 0.9))[: n - s]
    # crickets (distant, reverberant)
    crick = np.zeros((n, 2))
    for k in range(3):
        f = rng.uniform(4200, 5000)
        period = rng.uniform(0.55, 0.85)
        p = rng.uniform(-0.8, 0.8)
        x = np.zeros(n)
        tt = rng.uniform(0, 0.5)
        while tt < L + 2:
            for pulse in range(rng.integers(3, 5)):
                s = int((tt + pulse * 0.028) * SR)
                m = int(0.016 * SR)
                if s + m < n:
                    x[s:s + m] += np.sin(2 * np.pi * f * np.arange(m) / SR) * np.sin(np.linspace(0, np.pi, m))
            tt += period * rng.uniform(0.95, 1.05)
        swell = 0.5 + 0.5 * np.sin(2 * np.pi * rng.uniform(0.02, 0.05) * t + rng.random() * 6)
        crick += pan(x * swell * 0.05, p)
    crick = add_reverb(crick, wet=0.55, dur=1.6, decay=0.35, damp=6000)[:n]
    out += crick
    # a couple of distant birds
    for _ in range(3):
        s = int(rng.uniform(2, L - 2) * SR)
        bird = np.zeros(int(1.2 * SR))
        tt = 0
        for _ in range(rng.integers(3, 6)):
            m = int(rng.uniform(0.06, 0.14) * SR)
            f0, f1 = rng.uniform(2500, 3800), rng.uniform(2000, 4200)
            f = np.linspace(f0, f1, m)
            seg = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.sin(np.linspace(0, np.pi, m)) ** 2
            i0 = int(tt * SR)
            bird[i0:i0 + m] += seg[: len(bird) - i0]
            tt += rng.uniform(0.12, 0.22)
        b = add_reverb(pan(bird * 0.025, rng.uniform(-1, 1)), wet=0.6, dur=2.0, decay=0.5, damp=5000)
        out[s:s + len(b)] += b[: n - s]
    save("amb_yard", loopify(out, L), -12)


def swarm():
    L = 6.0
    n = int((L + 0.2) * SR)
    out = np.zeros((n, 2))
    times = np.sort(rng.uniform(0, L, 2600))
    for tt in times:
        m = int(rng.uniform(0.0008, 0.0025) * SR)
        c = noise(m) * env_exp(m, 0.0006)
        s = int(tt * SR)
        out[s:s + m] += pan(c * rng.uniform(0.2, 1.0), rng.uniform(-0.8, 0.8))
    for c in range(2):
        out[:, c] = bp(out[:, c], 3500, 11000)
        out[:, c] += bp(pink(n), 1500, 4000) * 0.04
    save("swarm", loopify(out, L), -8)


def bites():
    for k in range(5):
        n = int(0.14 * SR)
        x = np.zeros(n)
        for j in range(rng.integers(2, 4)):
            s = int(rng.uniform(0, 0.05) * SR)
            c = click(rng.uniform(0.02, 0.05), 1200, 9000, rng.uniform(0.004, 0.012))
            c = resonator(c, rng.uniform(1800, 2600), 6) + resonator(c, rng.uniform(3200, 4600), 8) * 0.7 + c * 0.6
            x[s:s + len(c)] += c[: n - s] * rng.uniform(0.6, 1.0)
        x += np.sin(2 * np.pi * rng.uniform(500, 700) * np.arange(n) / SR) * env_exp(n, 0.01) * 0.2
        save(f"bite_{k}", fade(x), -4)


def deaths():
    for k in range(3):
        n = int(0.35 * SR)
        x = np.zeros(n)
        times = np.sort(rng.uniform(0, 0.16, 9))
        for tt in times:
            c = click(0.03, 900, 8000, rng.uniform(0.003, 0.008))
            c = resonator(c, rng.uniform(1500, 3000), 5) + c
            s = int(tt * SR)
            x[s:s + len(c)] += c[: n - s] * rng.uniform(0.4, 1.0)
        squish = lp(noise(n), 700) * env_exp(n, 0.06)
        f = np.linspace(260, 120, n)
        squish += np.sin(2 * np.pi * np.cumsum(f) / SR) * env_exp(n, 0.07) * 0.6
        x += squish * 0.5
        save(f"death_{k}", fade(x), -4)


def small_sfx():
    n = int(0.06 * SR)
    x = click(0.06, 2500, 9000, 0.003) + np.sin(2 * np.pi * 2100 * np.arange(n) / SR) * env_exp(n, 0.006) * 0.3
    save("pickup", fade(x), -6)
    n = int(0.25 * SR)
    t = np.arange(n) / SR
    x = np.sin(2 * np.pi * 330 * t) * env_exp(n, 0.05) + lp(noise(n), 1200) * env_exp(n, 0.01) * 0.3
    save("deliver", fade(x), -8)
    n = int(0.9 * SR)
    x = bp(noise(n), 700, 3200) * np.sin(np.linspace(0, np.pi, n)) ** 1.5
    for tt in rng.uniform(0, 0.8, 40):
        c = click(0.01, 2000, 8000, 0.002)
        s = int(tt * SR)
        x[s:s + len(c)] += c * 0.5
    save("spawn", fade(x), -8)


def ui():
    n = int(0.09 * SR)
    t = np.arange(n) / SR
    x = fm_bell(1400, 0.09, ratio=2.01, index=1.2, decay=0.025) + click(0.09, 2000, 8000, 0.002) * 0.3
    save("ui_click", fade(x), -6)
    x = fm_bell(2200, 0.05, ratio=2.0, index=0.6, decay=0.012)
    save("ui_hover", fade(x), -14)
    n = int(0.42 * SR)
    t = np.arange(n) / SR
    sq = signal.square(2 * np.pi * 130 * t) * ((t < 0.13) | ((t > 0.2) & (t < 0.33)))
    x = lp(sq, 900) * env_exp(n, 0.4)
    save("ui_error", fade(x), -8)
    a = fm_bell(note("A4"), 0.6, ratio=4.0, index=1.6, decay=0.25)
    b = fm_bell(note("E5"), 0.8, ratio=4.0, index=1.6, decay=0.3)
    x = np.zeros(int(1.0 * SR))
    x[: len(a)] += a
    s = int(0.11 * SR)
    x[s:s + len(b)] += b[: len(x) - s]
    save("ui_ready", fade(add_reverb(x, 0.25, 1.2, 0.3)), -6)
    x = fm_bell(note("D5"), 0.4, ratio=4.0, index=1.0, decay=0.15)
    save("ui_queue", fade(x), -9)


def horn(f0, dur, vib=5.0):
    t = t_axis(dur)
    f = f0 * (1 + 0.006 * np.sin(2 * np.pi * vib * t)) * (1 + 0.03 * np.exp(-t * 6))
    ph = 2 * np.pi * np.cumsum(f) / SR
    x = signal.sawtooth(ph) + 0.5 * signal.sawtooth(ph * 2.003)
    cut = 300 + 1400 * np.minimum(1, t / 0.35) * np.exp(-np.maximum(0, t - 0.5) * 1.2)
    return lp_tv(x, cut) * adsr(len(t), 0.12, 0.4)


def alerts():
    x = horn(note("D2"), 1.5) + horn(note("A2"), 1.5) * 0.5
    save("alert", fade(add_reverb(x, 0.4, 2.5, 0.6, 2500)), -4)
    # victory: rising oud + horn chord
    out = np.zeros(int(5 * SR))
    for i, nn in enumerate(["D3", "F#3", "A3", "D4", "F#4", "A4"]):
        p = oud(note(nn), 2.5, 0.8)
        s = int(i * 0.14 * SR)
        out[s:s + len(p)] += p
    h = horn(note("D3"), 3.2, 4) + horn(note("A3"), 3.2, 4) * 0.6 + horn(note("F#3"), 3.2, 4) * 0.4
    s = int(0.8 * SR)
    out[s:s + len(h)] += h[: len(out) - s] * 0.5
    save("victory", fade(add_reverb(out, 0.35, 3, 0.9), 0.01, 0.5), -3)
    out = np.zeros(int(5 * SR))
    for i, nn in enumerate(["A3", "G3", "F3", "Eb3", "D3"]):
        p = oud(note(nn), 2.5, 0.8)
        s = int(i * 0.32 * SR)
        out[s:s + len(p)] += p
    h = horn(note("D2"), 3.5, 3)
    out[: len(h)] += h * 0.5
    save("defeat", fade(add_reverb(out, 0.4, 3, 1.0), 0.01, 0.5), -3)


# D Phrygian dominant (Hijaz): D Eb F# G A Bb C
HIJAZ = ["D", "Eb", "F#", "G", "A", "Bb", "C"]


def scale_note(deg, octave):
    o = octave + deg // 7
    return note(HIJAZ[deg % 7] + str(o))


def music_calm():
    bpm = 76
    beat = 60 / bpm
    bars = 16
    L = bars * 4 * beat
    n = int((L + 6) * SR)
    out = np.zeros((n, 2))
    chords = [["D3", "F#3", "A3"], ["Eb3", "G3", "Bb3"], ["D3", "F#3", "A3"], ["C3", "Eb3", "G3"],
              ["D3", "F#3", "A3"], ["Eb3", "G3", "Bb3"], ["G2", "Bb2", "D3"], ["D3", "F#3", "A3"]]
    for i, ch in enumerate(chords):
        dur = 8 * beat
        pad = saw_pad([note(c) for c in ch] + [note(ch[0][:-1] + "2")], dur + 3, cutoff=750)
        s = int(i * dur * SR)
        seg = pad[: n - s]
        out[s:s + len(seg), 0] += seg * 0.55
        out[s:s + len(seg), 1] += np.roll(seg, 300) * 0.55
    # oud melody: sparse phrases
    phrase = [(0, 0, 1.0), (1.5, 1, 0.7), (2, 2, 0.8), (3, 1, 0.6), (4, 0, 0.9), (6, 4, 0.7), (6.5, 3, 0.6), (7, 2, 0.8),
              (8, 1, 0.8), (9.5, 0, 0.7), (10, -1, 0.6), (12, 0, 0.9)]
    for rep in range(4):
        for (b, deg, v) in phrase:
            if rng.random() < 0.15:
                continue
            tt = (rep * 16 + b * 1.0) * beat
            p = oud(scale_note(deg + 7, 3), 2.2, v * 0.5)
            s = int(tt * SR)
            out[s:s + len(p)] += pan(p, rng.uniform(-0.3, 0.3))[: n - s]
    # soft frame drum heartbeat
    for b in range(bars * 4):
        if b % 2 == 0:
            d = frame_drum("doum", 0.35)
            s = int(b * beat * SR)
            out[s:s + len(d)] += pan(d, 0)[: n - s]
        if b % 4 == 3:
            d = frame_drum("tek", 0.12)
            s = int((b + 0.5) * beat * SR)
            out[s:s + len(d)] += pan(d, 0.3)[: n - s]
    out = add_reverb(out, wet=0.35, dur=3.0, decay=0.9, damp=3500)
    save("music_calm", loopify(out, L), -7)


def music_battle():
    bpm = 118
    beat = 60 / bpm
    bars = 16
    L = bars * 4 * beat
    n = int((L + 5) * SR)
    out = np.zeros((n, 2))
    # maqsum-like pattern per bar (in 8ths): D t _ t D _ t _
    pat = [("doum", 0), ("tek", 1), ("tek", 3), ("doum", 4), ("tek", 6), ("ka", 7), ("ka", 5)]
    for bar in range(bars):
        for kind, e in pat:
            v = {"doum": 0.9, "tek": 0.45, "ka": 0.22}[kind] * rng.uniform(0.85, 1.0)
            d = frame_drum(kind, v)
            s = int((bar * 4 + e * 0.5) * beat * SR)
            out[s:s + len(d)] += pan(d, {"doum": 0, "tek": 0.25, "ka": -0.3}[kind])[: n - s]
        if bar % 4 == 3:
            for e in range(8):
                d = frame_drum("tek", 0.3 + e * 0.05)
                s = int((bar * 4 + 2 + e * 0.25) * beat * SR)
                out[s:s + len(d)] += pan(d, 0.1)[: n - s]
    # low oud ostinato
    ost = [0, 0, 1, 0, 2, 1, 0, -1]
    for bar in range(bars):
        root = [0, 0, 1, 0][bar % 4]
        for e, deg in enumerate(ost):
            p = oud(scale_note(deg + root, 2), 0.6, 0.55)
            s = int((bar * 4 + e * 0.5) * beat * SR)
            out[s:s + len(p)] += pan(p, -0.2)[: n - s]
    # tension pad
    for i in range(bars // 4):
        ch = [["D2", "A2", "D3", "Eb3"], ["Eb2", "Bb2", "Eb3", "G3"], ["D2", "A2", "D3", "F#3"], ["C2", "G2", "C3", "Eb3"]][i % 4]
        dur = 16 * beat
        pad = saw_pad([note(c) for c in ch], dur + 2, cutoff=1300, detune=0.009)
        s = int(i * dur * SR)
        seg = pad[: n - s]
        out[s:s + len(seg), 0] += seg * 0.35
        out[s:s + len(seg), 1] += np.roll(seg, 220) * 0.35
    # horn calls every 8 bars
    for bar in (0, 8):
        h = horn(note("D3"), 2.0) + horn(note("A3"), 2.0) * 0.4
        s = int(bar * 4 * beat * SR)
        out[s:s + len(h)] += pan(h * 0.35, 0)[: n - s]
    out = add_reverb(out, wet=0.25, dur=2.2, decay=0.6, damp=4500)
    save("music_battle", loopify(out, L), -6)


if __name__ == "__main__":
    os.makedirs(OUT, exist_ok=True)
    jobs = [amb_yard, swarm, bites, deaths, small_sfx, ui, alerts, music_calm, music_battle]
    only = sys.argv[1:]
    for j in jobs:
        if only and j.__name__ not in only:
            continue
        print(j.__name__)
        j()
