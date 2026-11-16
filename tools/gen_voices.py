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
# Owner, 2026-11-16: the calls sounded like spitting (breath and acid hiss in every one). The ants are rebuilt from
# three ingredients with no breath or hiss in them: cricket chirps (chirp), slow chitin creaks (creak) and wet,
# sticky gurgles (goo), over a low growl for the big ones (growl): crickety, gooey, monstrous.
# What tells the castes apart, by ear: the chirp's pitch (worker highest, Warlord lowest), its rhythm, and how much
# creak, goo and growl sits under it.
SOLDIER = [(1700, 10, 1.0), (2900, 8, 0.6), (4300, 6, 0.3)]
MAJOR = [(420, 9, 1.0), (760, 8, 0.7), (1300, 6, 0.4)]
WARLORD = [(300, 10, 1.0), (560, 9, 0.8), (980, 7, 0.5), (2100, 5, 0.25)]


def chirp(r, carrier, syllables, syl=0.03, gap=0.022, teeth=9, q=28, drift=0.0, harsh=0.0):
    """A cricket chirp: each syllable is one stroke of the file, a short run of teeth ringing a wing that sings
    at `carrier`. drift bends the pitch across the chirp; harsh adds a rougher second mode a fifth above."""
    out = []
    for i in range(syllables):
        n = int(syl * SR * r.uniform(0.85, 1.15))
        e = np.zeros(n + int(0.03 * SR))
        step = n / teeth
        for k in range(teeth):
            s = int(k * step * r.uniform(0.9, 1.1))
            if s + 3 <= len(e):
                e[s:s + 3] += r.uniform(0.6, 1.0) * np.array([1.0, -0.6, 0.2])
        f = carrier * (1 + drift * (i / max(1, syllables - 1) - 0.5)) * r.uniform(0.985, 1.015)
        y = nrm(resonator(e, f, q))
        if harsh:
            y = nrm(y + nrm(resonator(e, f * 1.5, q * 0.6)) * harsh)
        y[:n] *= np.sin(np.pi * np.linspace(0, 1, n)) ** 0.5
        y[n:] *= np.exp(-np.linspace(0, 6, len(y) - n))
        out.append((i * (syl + gap) * r.uniform(0.95, 1.05), y, r.uniform(0.8, 1.0)))
    return nrm(mix(out))


def creak(r, dur, rate0, rate1, modes, jitter=0.3):
    """Chitin plates grinding: slow, uneven teeth through low, ringing modes. A door of shell opening."""
    n = int(dur * SR)
    e = np.zeros(n + SR // 6)
    t = r.uniform(0, 0.01)
    while t < dur:
        k = t / dur
        s = int(t * SR)
        ln = int(r.uniform(0.0008, 0.002) * SR)
        e[s:s + ln] += np.hanning(ln) * r.uniform(0.5, 1.0) * np.sin(np.pi * min(1, k + 0.05)) ** 0.5
        t += (1.0 / (rate0 + (rate1 - rate0) * k)) * (1 + r.uniform(-jitter, jitter))
    y = np.zeros_like(e)
    for f, q, g in modes:
        y += nrm(resonator(e, f * r.uniform(0.97, 1.03), q)) * g
    return nrm(lp(y, 5000))


def goo(r, dur, low=180, high=900, pops=6, thick=0.5):
    """Wet and sticky: a thick gurgle whose mouth opens and closes, with bubbles that swell and burst in it.
    No hiss: nothing above a few kHz."""
    n = int(dur * SR)
    t = np.arange(n) / SR
    # the gurgle: a slow irregular flutter of low pulses through a moving formant
    x = np.zeros(n + 2000)
    tt = r.uniform(0, 0.01)
    while tt < dur:
        ln = int(r.uniform(0.004, 0.009) * SR)
        s = int(tt * SR)
        x[s:s + ln] += np.hanning(ln) * r.uniform(0.4, 1.0)
        tt += r.uniform(0.012, 0.03) * (1.3 - thick)
    x = x[:n]
    k = 0.5 + 0.5 * np.sin(2 * np.pi * r.uniform(2.5, 5) * t + r.uniform(0, 6))
    y = nrm(resonator(x, low * 1.6, 5)) * (1 - k) + nrm(resonator(x, high, 6)) * k
    y = nrm(lp(y, 2600))
    for _ in range(pops):
        ln = int(r.uniform(0.02, 0.05) * SR)
        s = int(r.uniform(0, max(0.01, dur - 0.06)) * SR)
        if s + ln >= n:
            continue
        bt = np.arange(ln) / SR
        f = r.uniform(low * 1.5, high * 1.4)
        ph = 2 * np.pi * f * (bt + 1.2 * bt * bt / (ln / SR))  # a bubble rises in pitch until it bursts
        y[s:s + ln] += np.sin(ph) * np.hanning(ln) ** 0.4 * r.uniform(0.4, 0.9)
    env = np.minimum(1, t / 0.02) * np.clip((dur - t) / (dur * 0.35), 0, 1)
    return nrm(y * env)


def growl(r, dur, f0, f1, mouth, trem=0.0, trem_hz=18):
    """The monster under the insect: a rough low throat with every other pulse weak, and no breath in it."""
    return voice(r, dur, f0, f1, mouth, 0.6, 0.08, 0.0, trem=trem, trem_hz=trem_hz, attack=0.03, release=min(0.2, dur * 0.4), bend=0.7)


def snap(r, modes, burst=0.004):
    """Jaws or shell: a knock into chitin modes with no bright spit on top."""
    return clack(r, modes, burst, 0.08)


def worker(r, v, ack):
    # small and eager (the owner likes these as they are: unchanged): two or three high pips, each sliding up
    mouth = [(2400, 8, 1), (3800, 7, 0.6)]
    def pip(f, d=0.055):
        return voice(r, d, f, f * 1.45, mouth, 0.05, 0.02, 0.1, attack=0.006, release=0.03)
    if ack:
        return mix([(0, pip(1250 + 90 * v, 0.07), 1.0)])
    notes = ((1050, 1300), (1150, 1000, 1350), (980, 1220, 1500))[v]
    return mix([(i * 0.085 + r.uniform(0, 0.008), pip(f), 1.0 - 0.12 * (i % 2)) for i, f in enumerate(notes)])


def soldier(r, v, ack):
    # harder and lower: a harsh chirp with a jaw knock in front and a short creak of the head plates
    plates = [(620, 14, 1), (1100, 10, 0.6), (1900, 8, 0.3)]
    if ack:
        return mix([(0, snap(r, SOLDIER), 0.6), (0.03, chirp(r, 3100 + 120 * v, 2, 0.03, 0.02, 8, 22, harsh=0.4), 1.0)])
    if v == 0:
        return mix([(0, snap(r, SOLDIER), 0.6), (0.04, chirp(r, 3000, 3, 0.032, 0.022, 8, 22, harsh=0.4), 1.0), (0.05, creak(r, 0.18, 40, 90, plates), 0.45)])
    if v == 1:
        return mix([(0, creak(r, 0.16, 90, 45, plates), 0.5), (0.1, chirp(r, 3200, 2, 0.034, 0.02, 8, 22, -0.05, 0.4), 1.0), (0.24, snap(r, SOLDIER), 0.55)])
    return mix([(0, snap(r, SOLDIER), 0.6), (0.08, snap(r, SOLDIER), 0.5), (0.13, chirp(r, 2850, 4, 0.028, 0.02, 8, 22, harsh=0.45), 1.0)])


def silk(r, v, ack):
    # the web ant: the thinnest, fastest trill in the colony, and silk drawn tight
    def trill(nn, f=6400):
        return chirp(r, f, nn, 0.014, 0.01, 6, 34)
    if ack:
        return mix([(0, trill(4, 6500 + 150 * v), 1.0), (0.02, squeak(r, 0.09, 2600, 4200), 0.3)])
    f = (6300, 6700, 6000)[v]
    return mix([(0, trill(6, f), 1.0), (0.17, trill(4, f * 1.06), 0.9), (0.03, squeak(r, 0.2, 2400, 4400), 0.3),
                (0.3, goo(r, 0.06, 700, 1800, 1, 0.3), 0.25)])


def major(r, v, ack):
    # heavy: a slow deep chirp like a great cricket, the creak of a huge head, and a growl under it
    mouth = [(340, 6, 1), (620, 6, 0.8), (1150, 5, 0.35)]
    head = [(260, 16, 1), (470, 12, 0.7), (820, 9, 0.4)]
    def big(nn, f=1500):
        return chirp(r, f, nn, 0.05, 0.04, 9, 20, harsh=0.3)
    if ack:
        return mix([(0, snap(r, MAJOR, 0.005), 0.7), (0.04, big(2, 1550), 1.0), (0.03, growl(r, 0.2, 80, 64, mouth), 0.6)])
    if v == 0:
        return mix([(0, snap(r, MAJOR, 0.005), 0.7), (0.06, big(3), 1.0), (0.04, growl(r, 0.42, 76, 58, mouth), 0.7)])
    if v == 1:
        return mix([(0, creak(r, 0.4, 22, 55, head), 0.8), (0.18, big(2, 1400), 1.0), (0.1, growl(r, 0.4, 70, 60, mouth), 0.6), (0.5, snap(r, MAJOR, 0.005), 0.7)])
    return mix([(0, growl(r, 0.5, 82, 56, mouth), 0.8), (0.12, big(3, 1600), 0.9), (0.4, creak(r, 0.2, 50, 24, head), 0.6)])


FLY = [(900, 4, 0.6), (2400, 5, 0.5), (4200, 5, 0.3)]


def flyer(r, v, ack):
    # wings first, then a quick climbing run of chirps: a cicada taking off
    def run(nn, f=5200, d=0.08):
        return chirp(r, f, nn, 0.018, 0.012, 7, 30, drift=d)
    if ack:
        return mix([(0, buzz(r, 0.14, 205, 250, FLY, breath=0.0, flutter=0.3), 0.5), (0.03, run(4, 5300 + 120 * v), 1.0)])
    if v == 0:
        return mix([(0, buzz(r, 0.3, 195, 238, FLY, breath=0.0, flutter=0.35), 0.55), (0.1, run(7), 1.0)])
    if v == 1:
        return mix([(0, run(4, 5000), 1.0), (0.2, run(5, 5500, 0.12), 1.0), (0.05, buzz(r, 0.36, 230, 190, FLY, breath=0.0, flutter=0.4, flutter_hz=17), 0.45)])
    return mix([(0, buzz(r, 0.18, 200, 225, FLY, breath=0.0, flutter=0.2), 0.55), (0.16, run(8, 4900, 0.14), 1.0)])


def gunner(r, v, ack):
    # the gooiest: a thick gurgle from the acid gland with bubbles bursting, and a wet low chirp
    mouth = [(520, 5, 1), (1050, 5, 0.7), (2100, 5, 0.25)]
    def wet(nn, f=2300):
        return chirp(r, f, nn, 0.036, 0.03, 7, 14, harsh=0.2)
    if ack:
        return mix([(0, goo(r, 0.2, 200, 950, 4, 0.7), 1.0), (0.08, wet(2), 0.7)])
    if v == 0:
        return mix([(0, goo(r, 0.5, 180, 900, 9, 0.7), 1.0), (0.12, wet(3), 0.7), (0.0, growl(r, 0.3, 150, 120, mouth, 0.9, 17), 0.45)])
    if v == 1:
        return mix([(0, wet(2, 2500), 0.7), (0.14, goo(r, 0.5, 220, 1100, 10, 0.8), 1.0)])
    return mix([(0, growl(r, 0.22, 160, 125, mouth, 0.9, 15), 0.5), (0.1, goo(r, 0.42, 170, 800, 8, 0.75), 1.0), (0.42, wet(2, 2200), 0.65)])


def armoured(r, v, ack):
    # an ant inside a beetle's shell: a hollow knock, plates creaking, and a low slow chirp from inside
    shell = [(310, 16, 1), (560, 12, 0.6), (980, 9, 0.35)]
    def inside(nn, f=2000):
        return chirp(r, f, nn, 0.04, 0.03, 8, 18)
    if ack:
        return mix([(0, snap(r, shell), 0.9), (0.06, inside(2, 2050 + 60 * v), 0.9)])
    if v == 0:
        return mix([(0, snap(r, shell), 0.9), (0.08, creak(r, 0.3, 28, 60, shell), 0.8), (0.2, inside(3), 0.9)])
    if v == 1:
        return mix([(0, creak(r, 0.34, 60, 26, shell), 0.8), (0.3, inside(2, 1900), 0.9), (0.46, snap(r, shell), 0.8), (0.56, snap(r, shell), 0.6)])
    return mix([(0, snap(r, shell), 0.9), (0.12, snap(r, shell), 0.7), (0.2, inside(3, 2100), 0.9), (0.22, creak(r, 0.24, 30, 50, shell), 0.6)])


def warlord(r, v, ack):
    # the Warlord: the monster of the colony. Jaws crack, a long growl falls, and a huge slow chirp grinds over it
    def jaws():
        return mix([(0, clack(r, [(64, 5, 1)], 0.008, 0.0), 0.8), (0, snap(r, WARLORD, 0.006), 1.0)])
    mouth = [(420, 5, 1), (780, 5, 0.9), (1500, 5, 0.5)]
    plates = [(190, 18, 1), (340, 14, 0.8), (610, 10, 0.5)]
    def roar(d, a=92, b=52):
        return growl(r, d, a, b, mouth)
    def grind(nn, f=1050):
        return chirp(r, f, nn, 0.07, 0.05, 10, 16, -0.08, 0.5)
    if ack:
        return mix([(0, jaws(), 0.8), (0.05, roar(0.34, 100, 70), 1.0), (0.1, grind(2), 0.7)])
    if v == 0:
        return mix([(0, jaws(), 0.8), (0.08, roar(0.85), 1.0), (0.2, grind(3), 0.7), (0.1, creak(r, 0.5, 18, 40, plates), 0.5)])
    if v == 1:
        return mix([(0, creak(r, 0.4, 40, 16, plates), 0.7), (0.1, roar(0.8, 84, 48), 1.0), (0.4, grind(3, 950), 0.75), (0.3, jaws(), 0.7)])
    return mix([(0, jaws(), 0.8), (0.12, jaws(), 0.7), (0.2, roar(0.75, 96, 50), 1.0), (0.32, grind(2, 1150), 0.7), (0.5, goo(r, 0.3, 120, 500, 3, 0.8), 0.4)])


def matron(r, v, ack):
    # the Acid Matron: her gland boils. A slow thick gurgle, a growl through it, and a wet chirp that wavers
    mouth = [(600, 5, 1), (1150, 6, 0.8), (2300, 5, 0.3)]
    def wet(nn, f=2700, d=0.1):
        return chirp(r, f, nn, 0.045, 0.035, 8, 16, d, 0.25)
    if ack:
        return mix([(0, goo(r, 0.3, 160, 800, 5, 0.8), 0.9), (0.06, wet(2), 0.8), (0.0, growl(r, 0.28, 130, 110, mouth, 0.8, 9), 0.5)])
    d = (0.1, -0.1, 0.16)[v]
    return mix([(0, goo(r, 0.85, 150, 850, 14, 0.85), 1.0), (0.05, growl(r, 0.7, (125, 105, 140)[v], 90, mouth, 0.8, 8), 0.6),
                (0.2, wet(4, (2700, 2500, 2900)[v], d), 0.8)])


SKY = [(380, 4, 0.8), (900, 4, 0.6), (2000, 5, 0.4)]


def marshal(r, v, ack):
    # the Sky Marshal: great slow wings, and a long falling run of chirps like a cicada's scream
    def scream(nn, f=4300, d=-0.25):
        return chirp(r, f, nn, 0.022, 0.012, 8, 24, d, 0.35)
    mouth = [(700, 5, 1), (1300, 5, 0.7)]
    if ack:
        return mix([(0, scream(6, 4400), 1.0), (0, buzz(r, 0.26, 130, 165, SKY, 0.04, 0.0), 0.45)])
    if v == 0:
        return mix([(0, buzz(r, 0.7, 120, 152, SKY, 0.04, 0.0), 0.5), (0.12, scream(12), 1.0), (0.15, growl(r, 0.4, 150, 110, mouth), 0.4)])
    if v == 1:
        return mix([(0, scream(5, 4600, -0.1), 1.0), (0.24, scream(10, 4200, -0.3), 1.0), (0, buzz(r, 0.6, 150, 112, SKY, 0.04, 0.0), 0.45)])
    return mix([(0, buzz(r, 0.3, 162, 130, SKY, 0.04, 0.0), 0.5), (0.25, scream(13, 4100, -0.3), 1.0), (0.3, growl(r, 0.4, 140, 100, mouth), 0.4)])


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
