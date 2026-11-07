"""The mutation track's models (targets H3-H5, docs/art-targets on claude/mutation), in the ant pipeline's format:
the venom sting of a mutated black flying ant, the royal larva and the hero's womb cocoon.

Run: blender --background --factory-startup --python tools/blender/brood_models.py -- <bin-dir>
after tools/blender/ant_models.py has written <bin-dir>/manifest.json (the sting sits at the flying ant's gaster tip).
Appends to the manifest and writes:
  lasius_Flyer_sting.bin   the sting and its venom bead, in the black flying ant's model space. Its vertices carry the
                           gaster's part and region, so the Ant shader nods and curls it with the gaster
  royal_larva.bin          a curled, segmented grub about 1 unit long (the renderer scales it), the head at +Z
  womb_cocoon.bin          a lumpy silk ovoid about 1 unit long, finely subdivided so the Womb shader can throb it
tools/ant-bake.gd turns them into assets/ants/*.res like the ants.
"""
import json
import math
import os
import random
import sys

import bpy
from mathutils import Vector

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import ant_models as am  # noqa: E402  (the lofting, subdivision, occlusion and writer of the ant pipeline)

OUT = sys.argv[sys.argv.index("--") + 1]


def sting(tip, lod_sides=6):
    """A short, curved, dark-amber sting leaving the gaster tip, bent down and back, with a bead of venom."""
    pts = [tip + Vector((0, 0.004, 0.014)), tip + Vector((0, -0.006, -0.018)), tip + Vector((0, -0.024, -0.045)),
           tip + Vector((0, -0.05, -0.062)), tip + Vector((0, -0.078, -0.066))]
    radii = [0.016, 0.012, 0.0085, 0.005, 0.0014]
    v, f = am.loft([(p, r, r) for p, r in zip(pts, radii)], lod_sides, cap_start=True, cap_end=True)
    shaft = am.make(v, f, 1, am.GASTER, am.GASTER_R, membrane_fn=lambda p: 0.7)
    v, f = am.ellipsoid(pts[-1] + Vector((0, -0.009, -0.001)), 0.009, 0.0105, 0.009, 4, 6)
    bead = am.make(v, f, 1, am.GASTER, am.GASTER_R, membrane_fn=lambda p: 1.0)
    return [shaft, bead]


def royal_larva():
    """A curled grub of thirteen swollen segments, the head capsule small at the front, the body pearly and plump."""
    n_seg = 13
    secs = []
    for i in range(n_seg * 4 + 1):
        t = i / (n_seg * 4)
        # a gentle C: the body curls down at the tail
        ang = -0.9 + 1.5 * t
        c = Vector((0, 0.18 * math.cos(ang) - 0.1, 0.42 * math.sin(ang) + 0.05))
        env = math.sin(math.pi * min(1.0, 0.06 + 0.97 * t)) ** 0.55
        seg = (t * n_seg) % 1.0
        groove = 1 - 0.07 * math.exp(-((seg - 0.0) / 0.12) ** 2) - 0.07 * math.exp(-((seg - 1.0) / 0.12) ** 2)
        r = (0.035 + 0.17 * env) * groove
        secs.append((c, r * 1.05, r * 0.95))
    v, f = am.loft(secs, 12)
    body = am.make(v, f, 1, am.GASTER, am.GASTER_R)
    head_at = secs[-1][0] + Vector((0, -0.005, 0.03))
    v, f = am.ellipsoid(head_at, 0.05, 0.045, 0.045, 5, 8)
    head = am.make(v, f, 1, am.HEAD, am.HEAD_R)
    jaws = []
    for s in (-1, 1):
        a = head_at + Vector((s * 0.018, -0.02, 0.035))
        v, f = am.cone(a, a + Vector((-s * 0.012, -0.012, 0.022)), 0.008, 4)
        jaws.append(am.make(v, f, 0, am.EYE, am.HEAD_R))
    return [body, head] + jaws


def womb_cocoon():
    """A silk ovoid, lumpy where the strands bunch, finely divided so it can bulge and throb in the shader."""
    rnd = random.Random(11)
    bumps = [(Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))).normalized(), rnd.uniform(0.02, 0.05))
             for _ in range(14)]
    secs = []
    for i in range(17):
        t = -1 + 2 * i / 16
        s = math.sqrt(max(0.0, 1 - t * t))
        secs.append((Vector((0, 0.36, 0.5 * t)), max(0.34 * s ** 0.9, 0.01), max(0.32 * s ** 0.9, 0.01)))

    def lumps(i, ang):
        t = -1 + 2 * i / 16
        d = Vector((math.cos(ang), math.sin(ang), t)).normalized()
        return 1 + sum(h * math.exp(-((1 - d.dot(b)) / 0.06)) for b, h in bumps)
    v, f = am.loft(secs, 16, profile=lumps)
    return [am.make(v, f, 2, am.BODY, am.THORAX_R)]


def main():
    manifest_path = os.path.join(OUT, "manifest.json")
    manifest = json.load(open(manifest_path))
    tip = Vector(manifest["lasius_Flyer_near"]["gaster_tip"])
    waist = Vector(manifest["lasius_Flyer_near"].get("waist", [0, 0.14, -0.105]))
    for name, parts in (("lasius_Flyer_sting", sting(tip)), ("royal_larva", royal_larva()), ("womb_cocoon", womb_cocoon())):
        ao = am.bake_ao(parts, 16)
        tris = am.write_bin(os.path.join(OUT, name + ".bin"), parts, ao)
        manifest[name] = dict(neck=[0, 0.14, 0.13], gaster_tip=list(tip), gaster_centre=list(tip), waist=list(waist),
                              detail="", tris=tris)
        print(f"[brood] {name}: {tris} triangles")
    json.dump(manifest, open(manifest_path, "w"), indent=1)


main()
