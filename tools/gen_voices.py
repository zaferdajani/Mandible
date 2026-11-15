"""Creature voices: what each creature says when it is clicked (voc_*) and when it takes an order (ack_*).

Run: python tools/gen_voices.py [type ...]   ->  assets/audio/voc_<type>_<0-2>.wav, ack_<type>_<0-1>.wav
No speech and no steady tones: each creature has a throat of its own (voice(): irregular pulses through a vocal
tract, with its own pitch, movement and length) over its mechanics (stridulation scrapes, mandible clacks, wing
strokes, wet hisses), all with jittered timing, so no two takes are alike. --table prints how far apart they sit.
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


def voice(r, dur, f0, f1, formants, rough=0.3, jitter=0.04, breath=0.2, vib=0.0, vib_hz=6.0, trem=0.0, trem_hz=20.0,
          attack=0.02, release=0.08, bend=1.0):
    """A throat: irregular pulses (never a steady tone) through a vocal tract. This is what makes a creature a
    creature and not a noise: each one has its own pitch, its own way of moving it, its own mouth.
    f0 -> f1 over the call (bend < 1 moves early, > 1 late); rough = every other pulse weaker (a growl's
    subharmonic); vib = pitch wobble; trem = loudness flutter (purr, gargle, trill)."""
    n = int(dur * SR)
    x = np.zeros(n + 2000)
    t = r.uniform(0, 0.003)
    k = 0
    while t < dur:
        u = (t / dur) ** bend
        f = (f0 + (f1 - f0) * u) * (1 + vib * np.sin(2 * np.pi * vib_hz * t))
        per = (1.0 / f) * (1 + r.normal(0, jitter))
        ln = max(6, int(per * 0.38 * SR))
        s = int(t * SR)
        a = r.uniform(0.8, 1.0) * (1 - rough if k % 2 else 1.0)
        x[s:s + ln] += np.hanning(ln) * a
        # the closing snap of the folds: the bright edge of every pulse
        x[s + ln - 1:s + ln + 1] -= 0.5 * a
        t += per
        k += 1
    x = hp(x, 40)
    air = bp(r.standard_normal(len(x)), 1200, 9000) * (0.4 + np.abs(nrm(x)))
    y = nrm(body(x, formants) + nrm(x) * 0.12) + nrm(air) * breath
    tt = np.arange(len(y)) / SR
    env = np.minimum(1, tt / attack) * np.clip((dur - tt) / release, 0, 1)
    env *= 1 - trem * 0.5 * (1 + np.sin(2 * np.pi * trem_hz * tt + r.uniform(0, 6)))
    return nrm(y * env)


# ------------------------------------------------------------------ the kingdom
# Every caste has a throat of its own on top of its mechanics (owner, 2026-11-15: "ant sounds should be unique to each").
# What tells them apart, by ear: pitch (worker highest, stag lowest), how the pitch moves, how long, and the rhythm.
SOLDIER = [(1700, 10, 1.0), (2900, 8, 0.6), (4300, 6, 0.3)]
MAJOR = [(420, 9, 1.0), (760, 8, 0.7), (1300, 6, 0.4)]
WARLORD = [(300, 10, 1.0), (560, 9, 0.8), (980, 7, 0.5), (2100, 5, 0.25)]


def worker(r, v, ack):
    # small and eager: two or three high pips, each sliding up
    mouth = [(2400, 8, 1), (3800, 7, 0.6)]
    def pip(f, d=0.055):
        return voice(r, d, f, f * 1.45, mouth, 0.05, 0.02, 0.1, attack=0.006, release=0.03)
    if ack:
        return mix([(0, pip(1250 + 90 * v, 0.07), 1.0)])
    notes = ((1050, 1300), (1150, 1000, 1350), (980, 1220, 1500))[v]
    return mix([(i * 0.085 + r.uniform(0, 0.008), pip(f), 1.0 - 0.12 * (i % 2)) for i, f in enumerate(notes)])


def soldier(r, v, ack):
    # a short hard bark with a jaw snap in front of it: "kra!"
    mouth = [(900, 6, 1), (1700, 7, 0.8), (2900, 6, 0.4)]
    def bark(d, f=300):
        return voice(r, d, f, f * 0.62, mouth, 0.45, 0.06, 0.3, attack=0.008, release=0.06, bend=0.6)
    if ack:
        return mix([(0, clack(r, SOLDIER), 0.6), (0.03, bark(0.13, 320 + 20 * v), 1.0)])
    if v == 0:
        return mix([(0, clack(r, SOLDIER), 0.6), (0.04, bark(0.2), 1.0)])
    if v == 1:
        return mix([(0, bark(0.11, 330), 1.0), (0.16, bark(0.17, 290), 1.0), (0.14, clack(r, SOLDIER), 0.5)])
    return mix([(0, clack(r, SOLDIER), 0.6), (0.09, clack(r, SOLDIER), 0.5), (0.13, bark(0.22, 280), 1.0)])


def silk(r, v, ack):
    # the web ant: a thin rising "zip" of silk through the spinneret, twice, over a fast dry trill
    trill = scrape(r, 0.1 if ack else 0.16, 70, 150, [(5200, 14, 1), (7400, 10, 0.6)], 0.15, 0.001, (3500, 12000))
    def zip_(f, d=0.12):
        return voice(r, d, f, f * 2.3, [(3000, 9, 1), (4600, 8, 0.6)], 0.0, 0.015, 0.08, attack=0.01, release=0.02, bend=1.6)
    if ack:
        return mix([(0, zip_(1500 + 100 * v, 0.1), 1.0), (0.02, trill, 0.35)])
    f = (1400, 1650, 1250)[v]
    return mix([(0, trill, 0.4), (0.03, zip_(f), 1.0), (0.2, zip_(f * 1.2, 0.15), 1.0)])


def major(r, v, ack):
    # heavy: a slow low grunt from a great head, and the thick clack of oversized jaws
    mouth = [(340, 6, 1), (620, 6, 0.8), (1150, 5, 0.35)]
    def grunt(d, f=92):
        return voice(r, d, f, f * 0.74, mouth, 0.5, 0.07, 0.22, attack=0.03, release=0.14)
    if ack:
        return mix([(0, clack(r, MAJOR, 0.005, 0.35), 0.7), (0.05, grunt(0.22, 98), 1.0)])
    if v == 0:
        return mix([(0, clack(r, MAJOR, 0.005, 0.35), 0.7), (0.07, grunt(0.5), 1.0)])
    if v == 1:
        return mix([(0, grunt(0.26, 100), 1.0), (0.3, grunt(0.38, 84), 1.0), (0.66, clack(r, MAJOR, 0.005, 0.35), 0.7)])
    return mix([(0, grunt(0.55, 86), 1.0), (0.5, clack(r, MAJOR, 0.005, 0.35), 0.7)])


FLY = [(900, 4, 0.6), (2400, 5, 0.5), (4200, 5, 0.3)]


def flyer(r, v, ack):
    # wings first, then a bright trilled whine that climbs: "prrrEE"
    def trill(d, f=560):
        return voice(r, d, f, f * 1.35, [(1500, 6, 1), (2700, 7, 0.7)], 0.1, 0.02, 0.12, trem=0.85, trem_hz=31, attack=0.015, release=0.05)
    if ack:
        return mix([(0, buzz(r, 0.14, 205, 250, FLY, flutter=0.3), 0.5), (0.03, trill(0.16, 600 + 30 * v), 1.0)])
    if v == 0:
        return mix([(0, buzz(r, 0.3, 195, 238, FLY, flutter=0.35), 0.55), (0.1, trill(0.3), 1.0)])
    if v == 1:
        return mix([(0, trill(0.18, 520), 1.0), (0.22, trill(0.24, 640), 1.0), (0.05, buzz(r, 0.36, 230, 190, FLY, flutter=0.4, flutter_hz=17), 0.45)])
    return mix([(0, buzz(r, 0.18, 200, 225, FLY, flutter=0.2), 0.55), (0.16, trill(0.34, 500), 1.0)])


def gunner(r, v, ack):
    # wet: a gargling croak from the acid gland, then the spray sizzling
    def croak(d, f=165):
        return voice(r, d, f, f * 0.85, [(520, 5, 1), (1050, 5, 0.7), (2300, 5, 0.3)], 0.35, 0.08, 0.3, trem=0.9, trem_hz=17, attack=0.02, release=0.08)
    if ack:
        return mix([(0, croak(0.16, 180), 1.0), (0.12, hiss(r, 0.14, (2500, 9000), (1500, 6000), 2, attack=0.006), 0.6)])
    if v == 0:
        return mix([(0, croak(0.32), 1.0), (0.26, hiss(r, 0.36, (2500, 9000), (1200, 6000), 6), 0.7)])
    if v == 1:
        return mix([(0, hiss(r, 0.3, (1200, 5000), (3000, 10000), 5), 0.6), (0.2, croak(0.36, 150), 1.0)])
    return mix([(0, croak(0.18, 175), 1.0), (0.22, croak(0.24, 155), 1.0), (0.4, hiss(r, 0.28, (2800, 9500), (1300, 5500), 4, attack=0.008), 0.65)])


def armoured(r, v, ack):
    # an ant inside a beetle's shell: a hollow knock, then a low purr rattling the plates
    shell = [(310, 16, 1), (560, 12, 0.6), (980, 9, 0.35)]
    def purr(d, f=62):
        return voice(r, d, f, f * 1.08, [(240, 7, 1), (480, 6, 0.7), (900, 5, 0.3)], 0.2, 0.05, 0.12, trem=0.95, trem_hz=26, attack=0.03, release=0.1)
    if ack:
        return mix([(0, clack(r, shell, 0.004, 0.25), 0.9), (0.06, purr(0.2, 68), 1.0)])
    if v == 0:
        return mix([(0, clack(r, shell, 0.004, 0.25), 0.9), (0.08, purr(0.42), 1.0)])
    if v == 1:
        return mix([(0, purr(0.4, 58), 1.0), (0.42, clack(r, shell, 0.004, 0.25), 0.9), (0.54, clack(r, shell, 0.004, 0.25), 0.7)])
    return mix([(0, clack(r, shell, 0.004, 0.25), 0.9), (0.12, clack(r, shell, 0.004, 0.25), 0.7), (0.2, purr(0.36, 66), 1.0)])


def warlord(r, v, ack):
    # the Warlord: his jaws crack, then a long roar that falls
    def jaws():
        return mix([(0, clack(r, [(64, 5, 1)], 0.008, 0.0), 0.8), (0, clack(r, WARLORD, 0.006, 0.4), 1.0)])
    mouth = [(420, 5, 1), (780, 5, 0.9), (1500, 5, 0.5), (2600, 4, 0.25)]
    def roar(d, a=150, b=78):
        return voice(r, d, a, b, mouth, 0.6, 0.09, 0.4, vib=0.03, vib_hz=9, attack=0.04, release=0.25, bend=0.7)
    if ack:
        return mix([(0, jaws(), 0.8), (0.05, roar(0.34, 160, 105), 1.0)])
    if v == 0:
        return mix([(0, jaws(), 0.8), (0.08, roar(0.85), 1.0)])
    if v == 1:
        return mix([(0, roar(0.3, 120, 150), 1.0), (0.32, roar(0.6, 160, 72), 1.0), (0.3, jaws(), 0.7)])
    return mix([(0, jaws(), 0.8), (0.12, jaws(), 0.7), (0.2, roar(0.75, 140, 70), 1.0)])


def matron(r, v, ack):
    # the Acid Matron: a breathy, wavering wail over the seethe of her gland
    def wail(d, a=340, b=250):
        return voice(r, d, a, b, [(700, 5, 1), (1250, 6, 0.8), (2900, 5, 0.4)], 0.15, 0.03, 0.55, vib=0.05, vib_hz=6.5, attack=0.08, release=0.25)
    if ack:
        return mix([(0, wail(0.32, 360, 300), 1.0), (0.04, hiss(r, 0.26, (1800, 8000), (900, 4500), 3, (250, 700), 0.55), 0.5)])
    a, b = ((340, 250), (270, 350), (360, 230))[v]
    seethe = hiss(r, 0.8, (1800, 8000), (700, 4500), 8, (250, 700), 0.6, 0.06)
    return mix([(0, seethe, 0.5), (0.05, wail(0.8, a, b), 1.0)])


SKY = [(380, 4, 0.8), (900, 4, 0.6), (2000, 5, 0.4)]


def marshal(r, v, ack):
    # the Sky Marshal: a raptor's falling screech over great slow wings
    def screech(d, a=1150, b=720):
        return voice(r, d, a, b, [(1900, 6, 1), (3100, 7, 0.9), (4600, 6, 0.4)], 0.4, 0.05, 0.3, attack=0.015, release=0.15, bend=0.8)
    if ack:
        return mix([(0, screech(0.24, 1200, 900), 1.0), (0, buzz(r, 0.26, 130, 165, SKY, 0.04), 0.45)])
    if v == 0:
        return mix([(0, buzz(r, 0.7, 120, 152, SKY, 0.04, 0.45), 0.5), (0.12, screech(0.5), 1.0)])
    if v == 1:
        return mix([(0, screech(0.2, 1050, 900), 1.0), (0.26, screech(0.42, 1250, 700), 1.0), (0, buzz(r, 0.6, 150, 112, SKY, 0.04, 0.45), 0.45)])
    return mix([(0, buzz(r, 0.3, 162, 130, SKY, 0.04, 0.45), 0.5), (0.25, screech(0.55, 1100, 640), 1.0)])


# ------------------------------------------------------------------ the wild
def beetle(r, v, ack):
    # hollow knocks on a thick shell, and a short low hoot from under it
    shell = [(240, 18, 1), (410, 14, 0.6), (690, 10, 0.3)]
    hoot = voice(r, 0.22, (118, 108, 126)[v], 104, [(230, 9, 1), (450, 7, 0.5)], 0.1, 0.03, 0.1, attack=0.04, release=0.12)
    knocks = [(i * (0.13 + 0.02 * v), clack(r, shell, 0.004, 0.2), 0.9 - 0.2 * i) for i in range((2, 3, 2)[v])]
    return mix(knocks + [(0.36 if v != 2 else 0.3, hoot, 1.0)])


def stag(r, v, ack):
    # the stag beetle: a bellow from very deep down, and a crack of antlers
    p = (1.0, 0.9, 1.1)[v]
    bellow = voice(r, 1.0, 58 * p, 44 * p, [(130, 7, 1), (270, 7, 0.9), (540, 6, 0.5), (1000, 5, 0.2)], 0.55, 0.08, 0.3, attack=0.1, release=0.4)
    crack = mix([(0, clack(r, [(50, 5, 1)], 0.01, 0.0), 0.8), (0, clack(r, [(180, 20, 1), (330, 15, 0.7), (600, 10, 0.4)], 0.005, 0.3), 1.0)])
    return mix([(0, bellow, 1.0), ((0.85, 0.0, 0.4)[v], crack, 0.8)])


def spider(r, v, ack):
    # dry legs pattering faster and faster, then a thin dry shriek
    tap = [(2600, 6, 1), (4200, 5, 0.5)]
    parts, t = [], 0.0
    count = (8, 9, 7)[v]
    for i in range(count):
        parts.append((t, clack(r, tap, 0.001, 0.5), r.uniform(0.4, 0.8)))
        t += 0.06 - 0.035 * i / count + r.uniform(-0.006, 0.006)
    shriek = voice(r, 0.26, (760, 820, 700)[v], 610, [(1700, 5, 1), (3300, 6, 0.9), (5200, 5, 0.5)], 0.7, 0.12, 0.6, attack=0.02, release=0.12)
    parts.append((t + 0.02, shriek, 1.0))
    return mix(parts)


def wasp(r, v, ack):
    # an angry, harsh buzz with a nasal whine riding on it, and the tick of a sting
    w = [(600, 3, 0.7), (1500, 4, 0.7), (3200, 4, 0.5)]
    tick = clack(r, [(3800, 8, 1)], 0.001, 0.6)
    def whine(d, a, b):
        return voice(r, d, a, b, [(1100, 9, 1), (2300, 8, 0.6)], 0.2, 0.03, 0.15, vib=0.04, vib_hz=13, attack=0.04, release=0.1)
    if v == 2:
        return mix([(0, buzz(r, 0.16, 160, 175, w, 0.06, 0.6, 0.4, 30, 0.05), 0.7), (0.2, buzz(r, 0.28, 172, 150, w, 0.06, 0.6, 0.4, 30, 0.05), 0.7),
                    (0.2, whine(0.28, 430, 380), 1.0), (0.48, tick, 0.4)])
    a, b = ((150, 172), (176, 148))[v]
    return mix([(0, buzz(r, 0.45, a, b, w, 0.06, 0.6, 0.4, 30, 0.06), 0.7), (0.05, whine(0.4, 2.6 * a, 2.6 * b), 1.0), (0.45, tick, 0.4)])


# The queen is not here any more: she answers in words (tools/voice/queen_barks.py writes voc_queen_*/ack_queen_*).
VOICES = [(worker, True), (armoured, True), (soldier, True), (silk, True), (major, True), (flyer, True), (gunner, True),
          (warlord, True), (matron, True), (marshal, True), (beetle, False), (stag, False), (spider, False), (wasp, False)]


def describe(x):
    """Pitch of the throat (autocorrelation peak, Hz), brightness (spectral centroid, Hz), length (s)."""
    w = x * np.hanning(len(x))
    sp = np.abs(np.fft.rfft(w)) ** 2
    fr = np.fft.rfftfreq(len(w), 1 / SR)
    cen = float((sp * fr).sum() / (sp.sum() + 1e-12))
    ac = np.fft.irfft(sp)[:SR // 30]
    neg = np.nonzero(ac < 0)[0]
    lo = max(SR // 2000, int(neg[0]) if len(neg) else 0)  # past the first dip, or the zero-lag peak wins
    lag = lo + int(np.argmax(ac[lo:]))
    return SR / lag, cen, len(x) / SR


if __name__ == "__main__":
    table = "--table" in sys.argv
    only = [a for a in sys.argv[1:] if a != "--table"]
    rows = []
    for fn, orders in VOICES:
        name = fn.__name__
        if only and name not in only:
            continue
        print(name)
        for kind, count in (("voc", 3), ("ack", 2 if orders else 0)):
            for v in range(count):
                r = np.random.default_rng(zlib.crc32(f"{name}/{kind}/{v}".encode()))
                x = finish(r, fn(r, v, kind == "ack"))
                rows.append((name, kind, v) + describe(x))
                save(f"{kind}_{name}_{v}", x, -4.0)
    if table:
        print("\ncaste      pitch Hz (3 calls)        centroid Hz   length s")
        for name in dict.fromkeys(q[0] for q in rows):
            c = [q for q in rows if q[0] == name and q[1] == "voc"]
            print(f"{name:10s} {' '.join(f'{q[3]:6.0f}' for q in c):24s}  {np.mean([q[4] for q in c]):8.0f}     {np.mean([q[5] for q in c]):5.2f}")
