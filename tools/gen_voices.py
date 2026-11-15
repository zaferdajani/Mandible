"""Creature voices: what each creature says when it is clicked (voc_*) and when it takes an order (ack_*).

Run: python tools/gen_voices.py [type ...]   ->  assets/audio/voc_<type>_<0-2>.wav, ack_<type>_<0-1>.wav
No speech and no oscillator tones: every call is noise driven through chitin and body resonators with jittered
timing (stridulation scrapes, mandible clacks, wing strokes, wet hisses), so no two takes are alike.
All sounds are original (procedural); no third-party samples.
"""
import sys
import zlib

import numpy as np
from scipy import signal

from gen_audio import SR, bp, fade, hp, lp, resonator, save


def nrm(x):
    return x / (np.max(np.abs(x)) + 1e-9)


def body(x, formants):
    y = np.zeros_like(x)
    for f, q, g in formants:
        y += nrm(resonator(x, f, q)) * g
    return y


def scrape(r, dur, rate0, rate1, formants, jitter=0.25, tooth=0.004, bright=(800, 9000), swell=None):
    """Stridulation: a file dragged over a scraper. Each tooth is a tiny noise burst; the body rings."""
    n = int(dur * SR)
    x = np.zeros(n + SR // 8)
    t = 0.0
    while t < dur:
        k = t / dur
        ln = max(8, int(tooth * SR * r.uniform(0.7, 1.3)))
        c = r.standard_normal(ln) * np.exp(-np.linspace(0, 5, ln))
        amp = r.uniform(0.5, 1.0) * (swell(k) if swell else np.sin(np.pi * min(1, k + 0.02)) ** 0.6)
        s = int(t * SR)
        x[s:s + ln] += c * amp
        rate = rate0 + (rate1 - rate0) * k
        t += (1.0 / rate) * (1 + r.uniform(-jitter, jitter))
    x = bp(x, bright[0], min(bright[1], SR / 2 - 200))
    return nrm(body(x, formants) + nrm(x) * 0.18)


def clack(r, modes, burst=0.003, snap=0.6):
    """A mandible snap or a knock on a shell: one noise burst into a few chitin modes."""
    n = int(0.3 * SR)
    e = np.zeros(n)
    ln = int(burst * SR)
    e[:ln] = r.standard_normal(ln) * np.exp(-np.linspace(0, 4, ln))
    y = np.zeros(n)
    for f, q, g in modes:
        y += nrm(resonator(e, f * r.uniform(0.95, 1.05), q)) * g
    return nrm(y + nrm(hp(e, 2500)) * snap)


def buzz(r, dur, f0, f1, formants, jitter=0.03, breath=0.35, flutter=0.0, flutter_hz=22, wob=0.0):
    """Wings: irregular strokes (never a steady tone), two wing pairs a hair apart, air torn on every stroke."""
    n = int(dur * SR)
    x = np.zeros(n + 4000)
    for det in (1.0, 1.017):
        t = r.uniform(0, 0.004)
        while t < dur:
            k = t / dur
            f = (f0 + (f1 - f0) * k) * det * (1 + wob * np.sin(2 * np.pi * 7 * t + det))
            per = (1.0 / f) * (1 + r.normal(0, jitter))
            ln = max(8, int(per * 0.42 * SR))
            s = int(t * SR)
            x[s:s + ln] += np.hanning(ln) * r.uniform(0.75, 1.0)
            t += per
    air = bp(r.standard_normal(len(x)), 1500, 8000) * x
    x = hp(x, 60)
    y = nrm(body(x, formants) + nrm(x) * 0.35) + nrm(air) * breath
    t = np.arange(len(y)) / SR
    env = np.minimum(1, t / 0.03) * np.clip((dur - t) / 0.07, 0, 1)
    env *= 1 - flutter * 0.5 * (1 + np.sin(2 * np.pi * flutter_hz * t + r.uniform(0, 6)))
    return nrm(y * env)


def hiss(r, dur, band0, band1, bubbles=0, bubble_f=(600, 1800), sputter=0.4, attack=0.03):
    """Acid: a hiss whose colour slides from band0 to band1, sputtering, with wet bubbles bursting in it."""
    n = int(dur * SR)
    nz = r.standard_normal(n)
    k = np.linspace(0, 1, n)
    x = nrm(bp(nz, *band0)) * (1 - k) + nrm(bp(nz, *band1)) * k
    x *= (1 - sputter) + sputter * np.abs(nrm(lp(r.standard_normal(n), 38)))
    t = np.arange(n) / SR
    x *= np.minimum(1, t / attack) * np.clip((dur - t) / (dur * 0.45), 0, 1) ** 1.3
    x = nrm(x)
    for _ in range(bubbles):
        ln = int(r.uniform(0.012, 0.032) * SR)
        s = int(r.uniform(0.02, max(0.03, dur - 0.08)) * SR)
        if s + ln >= n:
            continue
        tt = np.arange(ln) / SR
        f = r.uniform(*bubble_f)
        ph = 2 * np.pi * f * (tt + 0.9 * tt * tt / (ln / SR))  # a bubble's pitch rises as it bursts
        x[s:s + ln] += np.sin(ph) * np.hanning(ln) ** 0.5 * np.exp(-tt * 60) * r.uniform(0.35, 0.8)
    return nrm(x)


def squeak(r, dur, f0, f1, bw=70):
    """Silk drawn tight: a narrow band of noise gliding in pitch."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    ph = 2 * np.pi * np.cumsum(f0 * (f1 / f0) ** (t / dur)) / SR
    x = lp(r.standard_normal(n), bw) * np.sin(ph) + lp(r.standard_normal(n), bw) * np.cos(ph)
    return nrm(x * np.sin(np.pi * t / dur) ** 0.5)


def mix(parts):
    """parts: (start seconds, signal, gain)."""
    n = max(int(s * SR) + len(x) for s, x, _ in parts)
    out = np.zeros(n)
    for s, x, g in parts:
        i = int(s * SR)
        out[i:i + len(x)] += x * g
    return out


def near(r, x, wet=0.14):
    """The close, small space of a macro shot: 45 ms of damped early reflections."""
    n = int(0.045 * SR)
    ir = lp(r.standard_normal(n) * np.exp(-np.arange(n) / (0.012 * SR)), 5000)
    return x + nrm(signal.fftconvolve(x, ir)[:len(x)]) * wet * np.max(np.abs(x))


def finish(r, x):
    x = near(r, np.concatenate([x, np.zeros(int(0.05 * SR))]))
    x = nrm(hp(x, 30))
    # level the takes: a bare clack peaks high but carries little energy, so it is pressed (softly) up to the rest
    k = int(0.05 * SR)
    loud50 = np.sqrt(np.convolve(x * x, np.ones(k) / k, "valid").max()) if len(x) > k else 1.0
    g = float(np.clip(0.22 / (loud50 + 1e-9), 1.0, 3.0))
    x = np.tanh(x * g) / np.tanh(g)
    loud = np.nonzero(np.abs(x) > 0.004)[0]  # trim the silent tail: these files are short and mono
    x = x[:min(len(x), loud[-1] + int(0.02 * SR))]
    return fade(x, 0.001, 0.02)


# ------------------------------------------------------------------ the kingdom
QUEEN = [(92, 6, 1.0), (205, 8, 0.75), (470, 6, 0.5), (880, 6, 0.3)]  # the upper two carry her on small speakers
SOLDIER = [(1700, 10, 1.0), (2900, 8, 0.6), (4300, 6, 0.3)]
MAJOR = [(420, 9, 1.0), (760, 8, 0.7), (1300, 6, 0.4)]
WARLORD = [(300, 10, 1.0), (560, 9, 0.8), (980, 7, 0.5), (2100, 5, 0.25)]


def queen(r, v, ack):
    # the mother: a deep, slow thrum from a huge abdomen, and an old slow creak over it
    p = (1.0, 0.92, 1.07)[v]
    f = [(a * p, q, g) for a, q, g in QUEEN]
    if ack:
        thr = scrape(r, 0.42, 38 * p, 50 * p, f, 0.12, 0.009, (35, 1800))
        return mix([(0, thr, 1.0), (0.3, clack(r, [(260, 9, 1), (520, 7, 0.5)], 0.005, 0.15), 0.45)])
    two = (lambda k: abs(np.sin(2 * np.pi * k)) ** 0.7 * (1 - 0.3 * k)) if v == 2 else None
    thr = scrape(r, 1.0, 44 * p, 33 * p, f, 0.12, 0.009, (35, 1800), two)
    creak = scrape(r, 0.7, 11, 7, [(620 * p, 9, 1), (1250 * p, 7, 0.5)], 0.3, 0.006, (300, 4000))
    return mix([(0, thr, 1.0), (0.2 if v != 1 else 0.05, creak, 0.45)])


def worker(r, v, ack):
    # light and quick: two or three bright little chirps
    def chirp(p, up=1.0):
        return scrape(r, 0.045, 240, 300 * up, [(3600 * p, 10, 1), (5200 * p, 8, 0.6)], 0.2, 0.0015, (2000, 10000))
    if ack:
        return mix([(0, chirp(1.0 + 0.08 * v, 1.3), 1.0), (0.07, clack(r, [(4200, 6, 1)], 0.001, 0.4), 0.3)])
    pitches = ((1.0, 1.1), (1.05, 0.97, 1.12), (0.95, 1.18))[v]
    return mix([(i * 0.072 + r.uniform(0, 0.008), chirp(p), 1.0 - 0.15 * (i % 2)) for i, p in enumerate(pitches)])


def soldier(r, v, ack):
    # sharp mandible clacks, and a short dry rasp
    rasp = scrape(r, 0.07, 110, 150, [(1900, 8, 1), (2700, 7, 0.6)], 0.2, 0.002, (1000, 8000))
    if ack:
        return mix([(0, clack(r, SOLDIER), 1.0), (0.05 + 0.02 * v, rasp, 0.4)])
    if v == 0:
        return mix([(0, clack(r, SOLDIER), 1.0), (0.1, clack(r, SOLDIER), 0.9)])
    if v == 1:
        return mix([(0, clack(r, SOLDIER), 1.0), (0.085, clack(r, SOLDIER), 0.8), (0.15, clack(r, SOLDIER), 0.95)])
    return mix([(0, rasp, 0.55), (0.09, clack(r, SOLDIER), 1.0), (0.2, clack(r, SOLDIER), 0.7)])


def silk(r, v, ack):
    # the web-throwing strain: a clack, a fast spinneret trill, and silk drawn tight
    hi = [(f * 1.15, q, g) for f, q, g in SOLDIER]
    trill = scrape(r, 0.1 if ack else 0.16, 70, 150, [(5200, 14, 1), (7400, 10, 0.6)], 0.15, 0.001, (3500, 12000))
    pull = squeak(r, 0.13, (2100, 2500, 1800)[v], (3400, 3900, 3100)[v])
    if ack:
        return mix([(0, trill, 0.9), (0.08, pull, 0.5)])
    if v == 1:
        return mix([(0, trill, 0.9), (0.15, pull, 0.55), (0.3, clack(r, hi), 0.9)])
    return mix([(0, clack(r, hi), 1.0), (0.07, trill, 0.85), (0.2, pull, 0.55)])


def major(r, v, ack):
    # heavy and low: a slow growl from a big head, a thick clack of oversized jaws
    def growl(d):
        return scrape(r, d, 30, 22, [(260, 7, 1), (540, 6, 0.7), (1100, 5, 0.3)], 0.2, 0.007, (120, 3000))
    if ack:
        return mix([(0, clack(r, MAJOR, 0.005, 0.35), 1.0), (0.06, growl(0.18), 0.6)])
    if v == 0:
        return mix([(0, clack(r, MAJOR, 0.005, 0.35), 1.0), (0.08, growl(0.42), 0.7)])
    if v == 1:
        return mix([(0, growl(0.4), 0.7), (0.4, clack(r, MAJOR, 0.005, 0.35), 1.0)])
    return mix([(0, clack(r, MAJOR, 0.005, 0.35), 1.0), (0.2, clack(r, MAJOR, 0.005, 0.35), 0.85), (0.26, growl(0.25), 0.5)])


FLY = [(900, 4, 0.6), (2400, 5, 0.5), (4200, 5, 0.3)]


def flyer(r, v, ack):
    # a quick wing buzz, fluttering, with a small chirp to finish
    chirp = scrape(r, 0.04, 220, 280, [(3000, 10, 1), (4400, 8, 0.5)], 0.2, 0.0015, (1800, 9000))
    if ack:
        return buzz(r, 0.18, 205 + 10 * v, 260, FLY, flutter=0.3)
    if v == 0:
        return mix([(0, buzz(r, 0.36, 195, 238, FLY, flutter=0.35), 1.0), (0.37, chirp, 0.45)])
    if v == 1:
        return mix([(0, buzz(r, 0.4, 230, 190, FLY, flutter=0.4, flutter_hz=17), 1.0), (0.02, chirp, 0.4)])
    return mix([(0, buzz(r, 0.15, 200, 225, FLY, flutter=0.2), 0.9), (0.2, buzz(r, 0.2, 215, 250, FLY, flutter=0.3), 1.0)])


def gunner(r, v, ack):
    # a wet acid hiss: the gland squelches, the spray sizzles and bubbles
    glk = clack(r, [(500, 4, 1), (900, 3, 0.6)], 0.006, 0.05)
    if ack:
        return mix([(0, glk, 0.5), (0.02, hiss(r, 0.16, (2500, 9000), (1500, 6000), 2, attack=0.006), 1.0)])
    if v == 0:
        return mix([(0, glk, 0.55), (0.04, hiss(r, 0.45, (2500, 9000), (1200, 6000), 6), 1.0)])
    if v == 1:
        return mix([(0, hiss(r, 0.42, (1200, 5000), (3000, 10000), 5), 1.0), (0.4, glk, 0.5)])
    return mix([(0, glk, 0.5), (0.03, hiss(r, 0.18, (2200, 8000), (1500, 6000), 3, attack=0.008), 0.9),
                (0.24, hiss(r, 0.28, (2800, 9500), (1300, 5500), 4, attack=0.008), 1.0)])


def warlord(r, v, ack):
    # the Warlord: a blow you feel, great jaws, and a war growl that climbs
    def jaws():
        return mix([(0, clack(r, [(64, 5, 1)], 0.008, 0.0), 0.8), (0, clack(r, WARLORD, 0.006, 0.4), 1.0)])
    def growl(d, a, b, sw=None):
        return scrape(r, d, a, b, [(210, 8, 1), (430, 7, 0.8), (900, 6, 0.5), (1700, 5, 0.25)], 0.18, 0.008, (80, 4500), sw)
    rise = lambda k: min(1, 0.25 + k) ** 1.5 * min(1, (1 - k) * 12)
    if ack:
        return mix([(0, jaws(), 1.0), (0.07, growl(0.25, 30, 42), 0.7)])
    if v == 0:
        return mix([(0, jaws(), 1.0), (0.14, jaws(), 0.9), (0.2, growl(0.6, 26, 44, rise), 0.8)])
    if v == 1:
        return mix([(0, growl(0.5, 24, 40, rise), 0.8), (0.5, jaws(), 1.0), (0.61, jaws(), 0.8), (0.72, jaws(), 0.95)])
    return mix([(0, jaws(), 1.0), (0.1, growl(0.7, 44, 22), 0.8)])


def matron(r, v, ack):
    # the Acid Matron: a long sizzle over deep gurgling, and a slow warning rattle
    gland = clack(r, [(140, 5, 1), (330, 4, 0.6)], 0.01, 0.0)
    def rattle(d, a, b):
        return scrape(r, d, a, b, [(700, 8, 1), (1500, 6, 0.5)], 0.2, 0.004, (400, 5000))
    if ack:
        return mix([(0, gland, 0.6), (0.02, hiss(r, 0.3, (1800, 8000), (900, 4500), 3, (250, 700), 0.55), 1.0), (0.05, rattle(0.18, 20, 26), 0.45)])
    a, b = ((16, 26), (26, 14), (18, 22))[v]
    seethe = hiss(r, 0.8, (1800, 8000), (700, 4500), 8, (250, 700), 0.6, 0.06)
    parts = [(0, gland, 0.7), (0.03, seethe, 1.0), (0.08, rattle(0.7, a, b), 0.65)]
    if v == 2:
        parts.append((0.55, hiss(r, 0.25, (2500, 9000), (1500, 6000), 3, (300, 800), 0.5, 0.01), 0.7))
    return mix(parts)


SKY = [(380, 4, 0.8), (900, 4, 0.6), (2000, 5, 0.4)]


def marshal(r, v, ack):
    # the Sky Marshal: great slow wings beating down past you, and a hunting screech
    def screech(d):
        return scrape(r, d, 320, 200, [(2600, 12, 1), (3900, 10, 0.6)], 0.15, 0.0012, (1800, 9000))
    if ack:
        return mix([(0, buzz(r, 0.28, 130, 165, SKY, 0.04), 1.0), (0.2, screech(0.08), 0.45)])
    if v == 0:
        return mix([(0, buzz(r, 0.75, 120, 152, SKY, 0.04, 0.45), 1.0), (0.5, screech(0.22), 0.5)])
    if v == 1:
        return mix([(0, buzz(r, 0.28, 150, 135, SKY, 0.04, 0.45), 1.0), (0.32, buzz(r, 0.34, 135, 112, SKY, 0.04, 0.45), 1.0), (0.62, screech(0.2), 0.5)])
    return mix([(0, screech(0.16), 0.5), (0.1, buzz(r, 0.75, 162, 108, SKY, 0.04, 0.45), 1.0)])


# ------------------------------------------------------------------ the wild
def armoured(r, v, ack):
    # an ant inside a beetle's shell: a dry rasp of plate on plate, then the hollow knock of the shell settling
    shell = [(310, 16, 1), (560, 12, 0.6), (980, 9, 0.35)]
    def rasp(d):
        return scrape(r, d, 70, 48, [(620, 7, 1), (1500, 6, 0.6), (2900, 5, 0.3)], 0.3, 0.005, (350, 5000))
    if ack:
        return mix([(0, clack(r, shell, 0.004, 0.25), 1.0), (0.09, clack(r, shell, 0.004, 0.25), 0.7), (0.15, rasp(0.14), 0.5)])
    if v == 0:
        return mix([(0, rasp(0.3), 0.7), (0.3, clack(r, shell, 0.004, 0.25), 1.0)])
    if v == 1:
        return mix([(0, clack(r, shell, 0.004, 0.25), 1.0), (0.12, rasp(0.2), 0.6), (0.36, clack(r, shell, 0.004, 0.25), 0.8)])
    return mix([(0, rasp(0.16), 0.6), (0.2, rasp(0.16), 0.7), (0.4, clack(r, shell, 0.004, 0.25), 1.0)])


def beetle(r, v, ack):
    # hollow knocks on a thick shell and a creak of plates
    shell = [(240, 18, 1), (410, 14, 0.6), (690, 10, 0.3)]
    creak = scrape(r, 0.25, 45, 30, [(500, 8, 1), (900, 6, 0.5)], 0.25, 0.005, (250, 3000))
    knocks = [(i * (0.13 + 0.02 * v), clack(r, shell, 0.004, 0.2), 1.0 - 0.2 * i) for i in range((2, 3, 2)[v])]
    return mix(knocks + [(0.1 if v == 2 else 0.3, creak, 0.45)])


def stag(r, v, ack):
    # the stag beetle: a groan from very deep down and a crack of antlers
    p = (1.0, 0.9, 1.1)[v]
    groan = scrape(r, 0.7, 20 * p, 14 * p, [(120 * p, 8, 1), (250 * p, 7, 0.8), (520, 6, 0.4)], 0.2, 0.012, (50, 1800))
    crack = mix([(0, clack(r, [(50, 5, 1)], 0.01, 0.0), 0.8), (0, clack(r, [(180, 20, 1), (330, 15, 0.7), (600, 10, 0.4)], 0.005, 0.3), 1.0)])
    return mix([(0, groan, 1.0), ((0.62, 0.0, 0.3)[v], crack, 0.9)])


def spider(r, v, ack):
    # dry legs pattering faster and faster, and a low breath
    tap = [(2600, 6, 1), (4200, 5, 0.5)]
    parts, t = [], 0.0
    count = (8, 9, 7)[v]
    for i in range(count):
        parts.append((t, clack(r, tap, 0.001, 0.5), r.uniform(0.5, 1.0)))
        t += 0.06 - 0.035 * i / count + r.uniform(-0.006, 0.006)
    parts.append((0.08, hiss(r, 0.34, (500, 2500), (300, 1500), 0, sputter=0.3, attack=0.08), 0.35))
    return mix(parts)


def wasp(r, v, ack):
    # an angry, harsh buzz that wavers, and the tick of a sting
    w = [(600, 3, 0.7), (1500, 4, 0.7), (3200, 4, 0.5)]
    tick = clack(r, [(3800, 8, 1)], 0.001, 0.6)
    if v == 2:
        return mix([(0, buzz(r, 0.16, 160, 175, w, 0.06, 0.6, 0.4, 30, 0.05), 1.0), (0.2, buzz(r, 0.28, 172, 150, w, 0.06, 0.6, 0.4, 30, 0.05), 1.0), (0.48, tick, 0.4)])
    a, b = ((150, 172), (176, 148))[v]
    return mix([(0, buzz(r, 0.45, a, b, w, 0.06, 0.6, 0.4, 30, 0.06), 1.0), (0.45, tick, 0.4)])


# The queen is not here any more: she answers in words (tools/voice/queen_barks.py writes voc_queen_*/ack_queen_*).
VOICES = [(worker, True), (armoured, True), (soldier, True), (silk, True), (major, True), (flyer, True), (gunner, True),
          (warlord, True), (matron, True), (marshal, True), (beetle, False), (stag, False), (spider, False), (wasp, False)]

if __name__ == "__main__":
    only = sys.argv[1:]
    for fn, orders in VOICES:
        name = fn.__name__
        if only and name not in only:
            continue
        print(name)
        for kind, count in (("voc", 3), ("ack", 2 if orders else 0)):
            for v in range(count):
                r = np.random.default_rng(zlib.crc32(f"{name}/{kind}/{v}".encode()))
                save(f"{kind}_{name}_{v}", finish(r, fn(r, v, kind == "ack")), -4.0)
