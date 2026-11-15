"""Models every ant caste of both colonies in Blender, headless, against the approved target G (docs/art-targets).

Run: blender --background --factory-startup --python tools/blender/ant_models.py -- <out-dir>
Then: godot --headless --path . --script res://tools/ant-bake.gd -- <out-dir> res://assets/ants

Each ant is built in Godot's model space (+Z forward, +Y up, feet on y = 0), part by part, the way the real animal is
segmented: a head with compound eyes, toothed mandibles and elbowed antennae; a three-part mesosoma; one petiole
scale (Lasius) or two nodes (Solenopsis); a gaster of telescoping tergites; six legs of coxa, trochanter, femur,
tibia and a five-segment tarsus with claws. Every part is a lofted cage smoothed by a Catmull-Clark subdivision, so
the surfaces are continuous rather than faceted primitives.

The Ant shader animates the mesh from per-vertex data, so each vertex carries the same contract AntMesh.cs used:
  COLOR  r = part id / 255 (legs 1-6, antennae 7-8, mandibles 9-10, eyes and ocelli 11, head 12, gaster 13, body 0)
         g = weight along a limb (0 at the joint it swings from, 1 at the tip)
         b = region (0 head, 0.5 thorax and waist, 1 gaster): what moves with the head and what a severed part takes
         a = ambient occlusion, baked here by ray casting against the whole ant and the ground
  UV     x = membrane (0 chitin, 1 the stretched membrane of a replete gaster; the pale seams between tergites are
             partway), y = 1 on setae (fine hairs that catch the light)
  UV2    the limb's pivot (x, z): hips for legs, sockets for antennae, hinges for mandibles
A file per caste and level of detail: <species>_<type>_<lod>.bin (see write_bin), plus manifest.json with each
mesh's neck point (where the head turns) and triangle count.
"""
import json
import math
import os
import random
import shutil
import struct
import sys

import bpy
import bmesh
from mathutils import Vector, noise
from mathutils.bvhtree import BVHTree

OUT = sys.argv[sys.argv.index("--") + 1] if "--" in sys.argv else ""

BODY, HEAD, GASTER, EYE = 0, 12, 13, 11
# what CUSTOM0.y says about a surface (the Ant shader reads it): 1 a seta, and for the mutation units' borrowed
# anatomy 2 spider silk, 3 beetle shell (uv.x counts its ridges), 4 a wasp's banded gaster (uv.x runs along it),
# 5 a spider's velvet hair, 6 a fly's red compound eye, 7 a spider's simple eye when it is on an eye part and a
# scorpion's tail anywhere else (the shader's eye branch and body branch never meet, so the two share the number)
SETA, SILK, SHELL, BANDS, VELVET, FLYEYE, SPIDEREYE = 1.0, 2.0, 3.0, 4.0, 5.0, 6.0, 7.0
TAIL = 7.0
HEAD_R, THORAX_R, GASTER_R = 0.0, 0.5, 1.0


class Part:
    """A finished piece of the ant: vertices, normals, triangles and the shader contract."""

    def __init__(self, verts, normals, tris, part, region, pivot=(0.0, 0.0), weights=None, membrane=None, seta=0.0, uv=None):
        self.v, self.n, self.t = verts, normals, tris
        self.part, self.region, self.pivot = part, region, pivot
        self.w = weights or [0.0] * len(verts)
        self.m = membrane or [0.0] * len(verts)
        self.seta = seta
        self.uv = uv  # texture coordinates into the scanned body's normal map, or None
        self.coord = None  # per-vertex (u, v) for a shell's ridges or a gaster's bands, where there is no scan
        self.tier = 0  # a general's growth appears from this mutation tier (0 always; 1-3 at hero levels 4, 7, 10)


# ---------------------------------------------------------------------------------------------------------- lofting

def boxy(n):
    """A superellipse ring: n = 2 is an ellipse, larger n squares the shoulders (an ant's head and thorax are boxier
    than eggs)."""
    return lambda i, ang: (abs(math.cos(ang)) ** n + abs(math.sin(ang)) ** n) ** (-1.0 / n)


def frames(centres, up_hint):
    """Parallel-transported (side, up) frames along a polyline."""
    out = []
    prev_side = None
    for i, c in enumerate(centres):
        a = centres[max(i - 1, 0)]
        b = centres[min(i + 1, len(centres) - 1)]
        t = (b - a)
        t = t.normalized() if t.length > 1e-9 else Vector((0, 0, 1))
        if prev_side is None:
            side = up_hint.cross(t)
            if side.length < 1e-6:
                side = Vector((1, 0, 0)).cross(t)
        else:
            side = prev_side - t * prev_side.dot(t)
        side.normalize()
        up = t.cross(side).normalized()
        out.append((side, up, t))
        prev_side = side
    return out


def loft(sections, sides=8, up_hint=Vector((0, 1, 0)), cap_start=True, cap_end=True, profile=None):
    """A closed tube through `sections` = [(centre, half_width, half_height), ...]. Returns (verts, faces).
    `profile(i, angle)` scales the radius per section and around the ring (grooves, flattened backs)."""
    centres = [s[0] for s in sections]
    fr = frames(centres, up_hint)
    verts, faces = [], []
    for i, (c, a, b) in enumerate(sections):
        side, up, _ = fr[i]
        for k in range(sides):
            ang = 2 * math.pi * k / sides
            r = profile(i, ang) if profile else 1.0
            verts.append(c + side * (math.cos(ang) * a * r) + up * (math.sin(ang) * b * r))
    n = len(sections)
    for i in range(n - 1):
        for k in range(sides):
            k2 = (k + 1) % sides
            faces.append((i * sides + k, i * sides + k2, (i + 1) * sides + k2, (i + 1) * sides + k))
    if cap_start:
        t0 = fr[0][2]
        verts.append(centres[0] - t0 * min(sections[0][1], sections[0][2]) * 0.6)
        p = len(verts) - 1
        for k in range(sides):
            faces.append((p, (k + 1) % sides, k))
    if cap_end:
        t1 = fr[-1][2]
        verts.append(centres[-1] + t1 * min(sections[-1][1], sections[-1][2]) * 0.6)
        p = len(verts) - 1
        base = (n - 1) * sides
        for k in range(sides):
            faces.append((p, base + k, base + (k + 1) % sides))
    return verts, faces


def ellipsoid(c, ax, ay, az, rings=6, sides=8, up=Vector((0, 1, 0)), fwd=Vector((0, 0, 1))):
    """An ellipsoid as a loft along `fwd` (so subdivision keeps it round)."""
    fwd = fwd.normalized()
    secs = []
    for i in range(rings + 1):
        t = -1 + 2 * i / rings
        s = math.sqrt(max(0.0, 1 - t * t))
        secs.append((c + fwd * (az * t), max(ax * s, 1e-4), max(ay * s, 1e-4)))
    secs[0] = (secs[0][0], secs[1][1] * 0.35, secs[1][2] * 0.35)
    secs[-1] = (secs[-1][0], secs[-2][1] * 0.35, secs[-2][2] * 0.35)
    return loft(secs, sides, up_hint=up)


def subdivided(verts, faces, levels):
    """Catmull-Clark through Blender's own modifier; returns (verts, normals, tris)."""
    me = bpy.data.meshes.new("cage")
    me.from_pydata([tuple(v) for v in verts], [], faces)
    me.validate()
    bm = bmesh.new()
    bm.from_mesh(me)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new("cage", me)
    bpy.context.scene.collection.objects.link(ob)
    if levels > 0:
        mod = ob.modifiers.new("sub", "SUBSURF")
        mod.levels = levels
        mod.render_levels = levels
    for poly in me.polygons:
        poly.use_smooth = True
    dg = bpy.context.evaluated_depsgraph_get()
    ev = ob.evaluated_get(dg)
    m = ev.to_mesh()
    m.calc_loop_triangles()
    vs = [v.co.copy() for v in m.vertices]
    ns = [v.normal.copy() for v in m.vertices]
    ts = [tuple(t.vertices) for t in m.loop_triangles]
    ev.to_mesh_clear()
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)
    return vs, ns, ts


def make(verts, faces, levels, part, region, pivot=(0.0, 0.0), weight_path=None, membrane_fn=None, seta=0.0):
    vs, ns, ts = subdivided(verts, faces, levels)
    w = [path_weight(v, weight_path) for v in vs] if weight_path else None
    m = [membrane_fn(v) for v in vs] if membrane_fn else None
    return Part(vs, ns, ts, part, region, pivot, w, m, seta)


def path_weight(p, path):
    """0 at the start of a limb's polyline, 1 at its tip: the projection of p onto the path, by arc length."""
    total = sum((path[i + 1] - path[i]).length for i in range(len(path) - 1))
    best, best_d, run = 0.0, 1e9, 0.0
    for i in range(len(path) - 1):
        a, b = path[i], path[i + 1]
        ab = b - a
        L = ab.length
        t = max(0.0, min(1.0, (p - a).dot(ab) / (L * L))) if L > 1e-9 else 0.0
        d = (a + ab * t - p).length
        if d < best_d:
            best_d, best = d, (run + L * t) / total
        run += L
    return best


def chain(points, radii, beads=None):
    """Sections along a limb polyline, with optional bead pinches (segment joints) between the given points."""
    secs = []
    for i, (p, r) in enumerate(zip(points, radii)):
        rx, ry = (r, r) if not isinstance(r, tuple) else r
        secs.append((p, rx, ry))
    return secs


def resample(points, radii, per):
    """Adds `per` interpolated sections between each pair, so a limb bends smoothly."""
    pts, rs = [], []
    for i in range(len(points) - 1):
        for k in range(per):
            t = k / per
            pts.append(points[i].lerp(points[i + 1], t))
            ra, rb = radii[i], radii[i + 1]
            if isinstance(ra, tuple):
                rs.append((ra[0] + (rb[0] - ra[0]) * t, ra[1] + (rb[1] - ra[1]) * t))
            else:
                rs.append(ra + (rb - ra) * t)
    pts.append(points[-1])
    rs.append(radii[-1])
    return pts, rs


def cone(base, tip, r, sides=4):
    """A thin pointed spike (a tooth, a spine, a seta, a claw)."""
    secs = [(base, r, r), (base.lerp(tip, 0.55), r * 0.55, r * 0.55), (tip, r * 0.08, r * 0.08)]
    return loft(secs, sides, cap_start=True, cap_end=False)


# --------------------------------------------------------------------------------------------------- the anatomy

SPECIES = {
    # Lasius niger: black garden ant. One petiole scale, a 12-segment antenna without a club, an acidopore.
    "lasius": dict(nodes=1, funiculus=11, club=0, eye=1.0, sting=False),
    # Solenopsis invicta: red fire ant. Two nodes, a 10-segment antenna ending in a two-segment club, small eyes, a sting.
    "solenopsis": dict(nodes=2, funiculus=9, club=2, eye=0.62, sting=True),
}

# head, mandible, gaster, thorax, leg; eye scale, ocelli, flight thorax, extra anatomy
CASTES = {
    "Worker": dict(H=1.0, M=1.0, G=1.0, T=1.0, L=1.0, eye=1.0),
    "Soldier": dict(H=1.32, M=1.3, G=1.0, T=1.05, L=1.0, eye=0.95),
    "Major": dict(H=1.85, M=1.6, G=1.05, T=1.1, L=0.95, eye=0.9, groove=0.5),
    "Gunner": dict(H=1.05, M=0.85, G=1.4, T=1.0, L=1.0, eye=1.0),
    "Queen": dict(H=1.15, M=1.0, G=1.6, T=1.45, L=0.95, eye=1.3, ocelli=True, flight=True, scars=True),
    # the mutation units (targets P1-P4, docs/art-targets/README-mutation-units.md): each is one body that merges the
    # ant with the insect whose core the queen ate; merged() adds the other insect's anatomy to the scanned ant
    "Flyer": dict(H=1.0, M=0.9, G=1.0, T=1.35, L=0.95, eye=1.55, ocelli=True, flight=True, merge="fly"),
    "Stinger": dict(H=1.0, M=0.9, G=1.0, T=1.35, L=0.95, eye=1.55, ocelli=True, flight=True, merge="wasp"),
    "WebAnt": dict(H=1.32, M=1.3, G=1.0, T=1.05, L=1.24, eye=0.95, merge="spider", legs8=True),
    "Armoured": dict(H=1.85, M=1.6, G=1.05, T=1.1, L=0.95, eye=0.9, groove=0.5, merge="beetle", plated=True),
    # the scorpion ant (target P8-B, docs/art-targets/README-scorpion.md): the soldier, its gaster running on into a scorpion's tail
    "ScorpionAnt": dict(H=1.32, M=1.3, G=1.0, T=1.05, L=1.0, eye=0.95, merge="scorpion"),
    "Warlord": dict(H=1.8, M=2.4, G=1.1, T=1.25, L=1.05, eye=0.85, groove=1.0, spines=True, teeth=6, setae=1.6),
    "Matron": dict(H=1.15, M=1.0, G=1.0, T=1.15, L=1.0, eye=1.05, replete=True, setae=1.4),
    "Marshal": dict(H=1.2, M=1.3, G=1.1, T=1.55, L=0.98, eye=1.5, ocelli=True, flight=True, setae=1.4),
}

BODY_Y = 0.13


def build(species, caste, lod):
    sp, cs = SPECIES[species], CASTES[caste]
    # near: close-ups and the hero cards; mid: play zoom; far: the crowd beyond FarDist
    near = lod == "near"
    lv = {"near": 1, "mid": 1, "far": 0}[lod]        # subdivision levels on the body masses
    sides = {"near": 8, "mid": 6, "far": 5}[lod]     # cage ring sides
    lsides = {"near": 6, "mid": 4, "far": 3}[lod]    # limb ring sides
    llv = 1 if near else 0
    fine = lod != "far"                               # bead joints on the limbs
    H, M, G, T, L = cs["H"], cs["M"], cs["G"], cs["T"], cs["L"]
    rnd = random.Random(hash((species, caste)) & 0xffff)
    parts = []
    roots = {}  # limb part id -> the point it hangs from (hip, antennal socket, mandible hinge)
    build.knees_local = {}

    # ---- mesosoma: pronotum dome, mesonotum, the metanotal groove, then the propodeum's rounded declivity
    flight = cs.get("flight", False)
    def ms(z, y, w, h):
        return (Vector((0, BODY_Y + (y - BODY_Y) * T, z * T + 0.01 * (1 - T))), w * T * 1.12, h * T)
    if flight:
        # a queen's or alate's thorax: a great mesonotal dome of flight muscle over the middle legs
        msecs = [ms(0.125, 0.135, 0.03, 0.028), ms(0.105, 0.143, 0.05, 0.05), ms(0.08, 0.152, 0.058, 0.06),
                 ms(0.05, 0.16, 0.06, 0.07), ms(0.015, 0.162, 0.058, 0.07), ms(-0.02, 0.152, 0.05, 0.058),
                 ms(-0.048, 0.14, 0.046, 0.046), ms(-0.07, 0.132, 0.04, 0.038), ms(-0.085, 0.126, 0.022, 0.02)]
    else:
        msecs = [ms(0.125, 0.135, 0.03, 0.028), ms(0.105, 0.143, 0.05, 0.048), ms(0.078, 0.15, 0.056, 0.055),
                 ms(0.05, 0.148, 0.05, 0.05), ms(0.02, 0.143, 0.043, 0.045), ms(-0.008, 0.136, 0.037, 0.038),
                 ms(-0.024, 0.13, 0.034, 0.03), ms(-0.042, 0.137, 0.041, 0.042), ms(-0.065, 0.134, 0.04, 0.04),
                 ms(-0.085, 0.124, 0.022, 0.02)]
    v, f = loft(msecs, sides, profile=boxy(2.4))
    parts.append(make(v, f, lv, BODY, THORAX_R))
    meso_rear = msecs[-1][0]
    pro = ms(0.083, 0.152 if not flight else 0.148, 0.0, 0.0)[0]
    v, f = ellipsoid(pro, 0.059 * T, 0.05 * T, 0.042 * T, 5, sides)
    parts.append(make(v, f, lv, BODY, THORAX_R))
    prop = ms(-0.055, 0.137, 0.0, 0.0)[0]
    v, f = ellipsoid(prop, 0.043 * T, 0.04 * T, 0.03 * T, 5, sides)
    parts.append(make(v, f, lv, BODY, THORAX_R))
    if cs.get("scars"):
        # the scars where her wings were shed: low dark nubs on the flanks of the flight thorax
        for s in (-1, 1):
            v, f = ellipsoid(Vector((s * 0.05 * T, BODY_Y + 0.045 * T, 0.05 * T)), 0.012, 0.008, 0.02, 3, 5)
            parts.append(make(v, f, llv, BODY, THORAX_R))
    if cs.get("spines"):
        # propodeal spines and pronotal shoulder spines, real cuticle like Polyrhachis, not armour
        for s in (-1, 1):
            base = Vector((s * 0.03 * T, BODY_Y + 0.035 * T, -0.06 * T))
            v, f = cone(base, base + Vector((s * 0.05, 0.12, -0.09)) * T, 0.016 * T, lsides)
            parts.append(make(v, f, llv, BODY, THORAX_R))
            parts[-1].spine = ("propodeal", base)
            base = Vector((s * 0.045 * T, BODY_Y + 0.03 * T, 0.09 * T))
            v, f = cone(base, base + Vector((s * 0.08, 0.06, 0.035)) * T, 0.015 * T, lsides)
            parts.append(make(v, f, llv, BODY, THORAX_R))
            parts[-1].spine = ("pronotal", base)

    # ---- waist: Lasius a single upright scale; Solenopsis a petiole and a postpetiole node
    gz_front = -0.115 - (G - 1) * 0.02
    stalk = [(meso_rear + Vector((0, -0.005, 0.01)), 0.012, 0.012), (Vector((0, BODY_Y - 0.01, -0.105)), 0.011, 0.011),
             (Vector((0, BODY_Y - 0.005, gz_front)), 0.013, 0.013)]
    v, f = loft(stalk, lsides)
    parts.append(make(v, f, llv, BODY, THORAX_R))
    if sp["nodes"] == 1:
        # the scale: thin front to back, tall, its crest notched
        scale = [(Vector((0, BODY_Y - 0.015, -0.1)), 0.016, 0.009), (Vector((0, BODY_Y + 0.02, -0.1)), 0.022, 0.009),
                 (Vector((0, BODY_Y + 0.045, -0.103)), 0.02, 0.007), (Vector((0, BODY_Y + 0.055, -0.106)), 0.012, 0.005)]
        v, f = loft(scale, sides, up_hint=Vector((0, 0, 1)))
        parts.append(make(v, f, lv, BODY, THORAX_R))
    else:
        for z, h in ((-0.096, 0.035), (-0.122 - (G - 1) * 0.01, 0.028)):
            v, f = ellipsoid(Vector((0, BODY_Y + 0.008, z)), 0.02, h * 0.6, 0.014, 4, sides)
            parts.append(make(v, f, lv, BODY, THORAX_R))
            gz_front -= 0.012

    # ---- gaster
    gaster_tip = None
    if cs.get("replete"):
        # a replete: the gaster swollen with acid until the tergites are islands on taut membrane
        R = 0.2
        c = Vector((0, BODY_Y + 0.07, gz_front - R * 0.95))
        v, f = ellipsoid(c, R, R * 0.92, R, 8, sides + 2)
        parts.append(make(v, f, lv, GASTER, GASTER_R, membrane_fn=lambda p: 1.0))
        # four dorsal tergite plates and three ventral sternites, each a shallow cap riding the membrane
        for i, (lat, lon, ext) in enumerate([(1, 0.95, 0.36), (1, 0.42, 0.42), (1, -0.12, 0.44), (1, -0.68, 0.38),
                                            (-1, 0.55, 0.34), (-1, 0.0, 0.38), (-1, -0.55, 0.32)]):
            parts.append(plate(c, R * 1.012, lat, lon, ext, 0.6, sides, lv))
        gaster_tip = c + Vector((0, -R * 0.35, -R * 0.93))
        gaster_c = c
    else:
        # four tergites that telescope: each rear rim stands proud over the pale seam of the one behind
        L0 = 0.3 * G
        cy = BODY_Y + 0.008
        gsecs, mem = [], []
        nseg = 4
        gn = 24 if near else 16 if fine else 12
        for i in range(gn + 1):
            t = i / gn
            z = gz_front - L0 * t
            x = 2 * t - 1.1                                   # the widest point a little behind the middle
            env = max(0.0, 1 - abs(x / (0.95 if x < 0 else 1.0)) ** 2.3) ** 0.5
            env = max(env, 0.22 * (1 - t))                     # the front meets the waist in a broad collar
            seg = (t * nseg * 0.98)
            f = seg - math.floor(seg)
            lip = 1 + 0.05 * (1 - f) ** 3 if 0.08 < t < 0.92 else 1.0
            w = 0.118 * G * env * lip + 0.008
            h = 0.098 * G * env * lip + 0.008
            gsecs.append((Vector((0, cy + 0.02 * G * math.sin(math.pi * t) - 0.012 * t, z)), w, h))
        v, f = loft(gsecs, sides + 2)
        zs = [s[0].z for s in gsecs]
        def seam(p):
            t = (gz_front - p.z) / L0
            if t < 0.08 or t > 0.92:
                return 0.0
            seg = t * nseg * 0.98
            fr = seg - math.floor(seg)
            return 0.3 * max(0.0, 1 - fr / 0.06)
        parts.append(make(v, f, lv, GASTER, GASTER_R, membrane_fn=seam))
        gaster_tip = gsecs[-1][0] + Vector((0, 0, -0.01))
        gaster_c = gsecs[gn // 2][0]
    if sp["sting"]:
        v, f = cone(gaster_tip + Vector((0, 0, 0.012)), gaster_tip + Vector((0, -0.006, -0.035)), 0.006, 4)
        parts.append(make(v, f, 0, EYE, GASTER_R))
    elif near:
        # the acidopore: a short fringe of setae around the gaster tip
        for k in range(7):
            a = 2 * math.pi * k / 7
            base = gaster_tip + Vector((math.cos(a) * 0.012, math.sin(a) * 0.01, 0.004))
            v, f = cone(base, base + Vector((math.cos(a) * 0.012, math.sin(a) * 0.012, -0.022)), 0.0018, 3)
            parts.append(make(v, f, 0, GASTER, GASTER_R, seta=1.0))

    # ---- head
    hc = Vector((0, BODY_Y + 0.022, 0.225 + 0.08 * (H - 1)))
    hw, hh, hl = 0.094 * H, 0.052 * H, 0.1 * (1 + 0.6 * (H - 1))
    groove = cs.get("groove", 0.0)
    hsecs = []
    for i in range(9):
        t = -1 + 2 * i / 8                        # -1 back of the head, +1 the clypeus
        s = math.sqrt(max(0.0, 1 - t * t))
        # widest across the rear third (the heart shape of an ant's head), narrowing toward the mandibles
        broad = 1 + 0.12 * math.exp(-((t + 0.25) / 0.55) ** 2) - 0.12 * max(0.0, t) ** 2
        z = hc.z + hl * t
        hsecs.append((Vector((0, hc.y + 0.008 * t - 0.01 * max(0.0, t) ** 2, z)), max(hw * s ** 0.75 * broad, 0.004), max(hh * s ** 0.85, 0.003)))
    def head_profile(i, ang):
        t = -1 + 2 * i / 8
        top = max(0.0, math.sin(ang))
        flat = 1 - 0.1 * top ** 2                               # a flattened crown
        notch = 1 - (0.14 + 0.22 * groove) * math.exp(-((ang - math.pi / 2) / (0.35 + 0.25 * groove)) ** 2) * max(0.0, -t) ** 1.3
        grv = 1 - 0.14 * groove * math.exp(-((ang - math.pi / 2) / 0.13) ** 2) * (0.4 + 0.6 * max(0.0, -t))
        sq = (abs(math.cos(ang)) ** 2.7 + abs(math.sin(ang)) ** 2.7) ** (-1.0 / 2.7)
        return flat * notch * grv * sq
    v, f = loft(hsecs, sides + 4, profile=head_profile)
    parts.append(make(v, f, lv, HEAD, HEAD_R))
    capsule = parts[-1]
    capsule_bvh = BVHTree.FromPolygons(capsule.v, capsule.t)

    def on_head(direction, at):
        """Where a ray from inside the head along `direction` leaves the capsule (eyes and ocelli sit there)."""
        hit = capsule_bvh.ray_cast(at, direction.normalized(), 1.0)
        return hit[0] if hit[0] is not None else at + direction.normalized() * hw
    neck = Vector((0, hc.y - 0.02, hc.z - hl * 0.95))

    # clypeus: a convex shield over the mouth, its front edge overhanging the mandibles
    v, f = ellipsoid(on_head(Vector((0, 0.35, 1)), hc) - Vector((0, 0.006 * H, 0.012 * H)), 0.042 * H, 0.02 * H, 0.034 * H, 4, sides)
    parts.append(make(v, f, llv, HEAD, HEAD_R))
    for s in (-1, 1):
        a = on_head(Vector((s * 0.25, 1, 0.55)), hc + Vector((s * 0.018 * H, 0, 0))) - Vector((0, 0.003, 0))
        b = on_head(Vector((s * 0.2, 1, -0.1)), hc + Vector((s * 0.014 * H, 0, 0))) - Vector((0, 0.003, 0))
        v, f = loft([(a, 0.006 * H, 0.004 * H), (a.lerp(b, 0.5), 0.007 * H, 0.005 * H), (b, 0.003 * H, 0.002 * H)], 5)
        parts.append(make(v, f, llv, HEAD, HEAD_R))

    # compound eyes: oval, set on the sides a little ahead of the middle, bulging from the head capsule
    es = sp["eye"] * cs["eye"]
    for s in (-1, 1):
        out = Vector((s, 0.25, 0.05)).normalized()
        surf = on_head(out, Vector((0, hc.y + 0.008 * H, hc.z - hl * 0.08)))
        ec = surf - out * (0.0045 * es + 0.001)  # a low dome: only its outer half stands proud of the capsule
        # an oval eye, longer than tall, flattened against the head capsule
        v, f = ellipsoid(ec, 0.016 * es + 0.004, 0.022 * es + 0.004, 0.0085 * es + 0.002, 4, sides, up=Vector((0, 0, 1)), fwd=out)
        parts.append(make(v, f, lv, EYE, HEAD_R))
    if cs.get("ocelli"):
        for (x, z) in ((0.0, 0.25), (-0.016, 0.0), (0.016, 0.0)):
            c = on_head(Vector((0, 1, 0)), Vector((x * H, hc.y, hc.z + hl * (0.1 + 0.25 * z)))) - Vector((0, 0.002, 0))
            v, f = ellipsoid(c, 0.007, 0.004, 0.007, 3, 5)
            parts.append(make(v, f, llv, EYE, HEAD_R))

    # mandibles: flattened triangular blades hinged at the front corners, crossing in front of the clypeus,
    # a row of teeth along the inner (masticatory) margin
    for s in (-1, 1):
        pid = 9 if s > 0 else 10
        M = cs["M"] * (0.75 + 0.25 * H)
        hinge = Vector((s * 0.046 * H, hc.y - 0.024 * H, hc.z + hl * 0.84))
        roots[pid] = hinge.copy()
        mid = hinge + Vector((s * 0.016 * M, -0.008, 0.075 * M))
        tip = mid + Vector((-s * 0.058 * M, -0.004, 0.032 * M))
        pts, rs = resample([hinge, mid, tip], [(0.024 * M, 0.0095 * M), (0.019 * M, 0.0065 * M), (0.004, 0.0025)], 3 if fine else 1)
        v, f = loft([(p, a, b) for p, (a, b) in zip(pts, rs)], lsides, cap_start=True, cap_end=True)
        path = [hinge, mid, tip]
        parts.append(make(v, f, llv, pid, HEAD_R, (hinge.x, hinge.z), weight_path=path))
        if near:
            nt = cs.get("teeth", 4)
            for k in range(nt):
                at = mid.lerp(tip, (k + 0.6) / (nt + 0.5))
                inward = Vector((-s, 0, 0.35)).normalized()
                v, f = cone(at, at + inward * (0.011 * M) * (1.25 if k == nt - 1 else 1.0), 0.0045 * M, 3)
                parts.append(make(v, f, 0, pid, HEAD_R, (hinge.x, hinge.z), weight_path=path))

    # antennae: a long scape to the elbow, then a funiculus of beaded segments (a two-segment club in Solenopsis)
    for s in (-1, 1):
        pid = 7 if s > 0 else 8
        root = Vector((s * 0.028 * H, hc.y + 0.018 * H, hc.z + hl * 0.62))
        roots[pid] = root.copy()
        sl = 0.13 * (0.85 + 0.15 * H)
        elbow = root + Vector((s * 0.75, 0.55, -0.38)).normalized() * sl
        tip = elbow + Vector((s * 0.55, -0.12, 1.0)).normalized() * 0.21
        path = [root, elbow, tip]
        pts, rs = resample([root, elbow], [0.0085, 0.0075], 4 if fine else 1)
        secs = [(p, r, r) for p, r in zip(pts, rs)]
        n = sp["funiculus"] + sp["club"] if fine else 3 + sp["club"]
        for k in range(1, n + 1):
            u = k / n
            a = elbow.lerp(tip, (k - 0.5) / n) + Vector((0, 0.025, 0)) * math.sin(u * math.pi)
            b = elbow.lerp(tip, k / n) + Vector((0, 0.025, 0)) * math.sin(u * math.pi)
            club = k > n - sp["club"]
            r = 0.0062 + 0.0018 * u
            if club:
                r = 0.0105
            if not sp["club"] and k == n:
                r = 0.0085
            if near:
                secs.append((a, r, r))
                secs.append((b, r * 0.72, r * 0.72))
            else:
                secs.append((b, r, r))
        v, f = loft(secs, lsides)
        parts.append(make(v, f, 0 if not near else 0, pid, HEAD_R, (root.x, root.z), weight_path=path))

    # ---- legs: coxa, trochanter, femur up and out to the knee, tibia down, a five-segment tarsus on the ground
    hips = [0.092, 0.024, -0.04]
    targets = [  # trochanter offset, knee, ankle, foot (x signed by side later)
        (Vector((0.022, -0.03, 0.015)), Vector((0.11, 0.235, 0.17)), Vector((0.17, 0.022, 0.27)), Vector((0.195, 0.003, 0.33))),
        (Vector((0.025, -0.03, 0.0)), Vector((0.165, 0.245, 0.04)), Vector((0.265, 0.022, 0.03)), Vector((0.3, 0.003, 0.0))),
        (Vector((0.025, -0.03, -0.012)), Vector((0.16, 0.25, -0.11)), Vector((0.255, 0.022, -0.25)), Vector((0.285, 0.003, -0.32))),
    ]
    gait = [0, 1, 2]         # which of the shader's three gait slots each leg of a side walks in
    along = [0.0, 1.0, 2.0]  # where its hip sits along the specimen's three hips
    if cs.get("legs8"):
        # a spider's eight: four a side, fanned from straight ahead to straight behind. The shader animates three leg
        # slots a side in two alternating groups, so the fourth leg walks in the second slot: 0 1 0 1 down the flank
        hips = [0.092, 0.04, -0.012, -0.05]
        targets = [
            (Vector((0.022, -0.03, 0.015)), Vector((0.1, 0.25, 0.2)), Vector((0.16, 0.022, 0.32)), Vector((0.18, 0.003, 0.39))),
            (Vector((0.025, -0.03, 0.006)), Vector((0.175, 0.265, 0.1)), Vector((0.28, 0.022, 0.13)), Vector((0.315, 0.003, 0.125))),
            (Vector((0.025, -0.03, -0.006)), Vector((0.18, 0.265, -0.07)), Vector((0.285, 0.022, -0.13)), Vector((0.32, 0.003, -0.15))),
            (Vector((0.022, -0.03, -0.012)), Vector((0.14, 0.26, -0.17)), Vector((0.215, 0.022, -0.33)), Vector((0.24, 0.003, -0.41))),
        ]
        gait = [0, 1, 2, 1]
        along = [0.0, 0.7, 1.4, 2.0]
    plated = cs.get("plated", False)
    hairy = cs.get("legs8", False)
    for s in (-1, 1):
        for i in range(len(hips)):
            pid = (1 if s > 0 else 4) + gait[i]
            first = len(parts)
            def sx(p):
                return Vector((p.x * s, p.y, p.z))
            hip = Vector((s * 0.034 * T, BODY_Y - 0.012, hips[i] * T))
            own = i < 3 and gait[i] == i
            if own:
                roots[pid] = hip.copy()
            tro_off, knee, ankle, foot = targets[i]
            troch = hip + sx(tro_off)
            L2 = L * 1.25
            knee = Vector((s * knee.x * L2, BODY_Y + (knee.y - BODY_Y) * L, hips[i] * T + (knee.z - hips[i]) * L2))
            ankle = Vector((s * ankle.x * L2, ankle.y, hips[i] * T + (ankle.z - hips[i]) * L2))
            foot = Vector((s * foot.x * L2, foot.y, hips[i] * T + (foot.z - hips[i]) * L2))
            path = [hip, troch, knee, ankle, foot]
            if own:
                build.knees_local[pid] = knee.copy()
            # coxa to the trochanter: stout; the femur slightly flattened and thickest past its middle
            fe1 = troch.lerp(knee, 0.35) + Vector((0, 0.008, 0))
            fe2 = troch.lerp(knee, 0.7) + Vector((0, 0.006, 0))
            ti1 = knee.lerp(ankle, 0.3)
            ti2 = knee.lerp(ankle, 0.75)
            r = 1.0 + 0.15 * (T - 1)
            pts = [hip, hip.lerp(troch, 0.5), troch, fe1, fe2, knee, ti1, ti2, ankle]
            rad = [0.02 * r, 0.018 * r, 0.011, 0.0145, 0.0135, 0.0092, 0.0078, 0.008, 0.0072]
            if plated:
                # a beetle's legs: broad flattened femora and tibiae, each a plate, the tibia widening to its spurred end
                rad = [(0.03 * r, 0.03 * r), (0.026 * r, 0.026 * r), (0.017, 0.017), (0.036, 0.021), (0.033, 0.02), (0.017, 0.017), (0.022, 0.013), (0.027, 0.014), (0.015, 0.015)]
            elif hairy:
                # a spider's legs: stouter than an ant's, swelling at the knee and the ankle
                rad = [0.022 * r, 0.02 * r, 0.013, 0.019, 0.0175, 0.0155, 0.0115, 0.011, 0.0115]
            if near:
                pts, rad = resample(pts, rad, 2)
            elif not fine:
                pts = [hip, troch, fe2, knee, ankle]
                rad = [0.029 * r, 0.017, (0.033, 0.02), 0.017, 0.015] if plated else [0.021 * r, 0.013, 0.0175, 0.0155, 0.0115] if hairy else [0.019 * r, 0.011, 0.0135, 0.0092, 0.0072]
            secs = [(p, rr[0], rr[1]) if isinstance(rr, tuple) else (p, rr, rr) for p, rr in zip(pts, rad)]
            # tarsus: five beads, the first long, the claw segment last
            seglen = [0.4, 0.17, 0.14, 0.12, 0.17] if near else [0.55, 0.45]
            acc = 0.0
            tk = 1.45 if plated else 1.0
            for k, sl in enumerate(seglen):
                a = ankle.lerp(foot, acc + sl * 0.15)
                acc += sl
                b = ankle.lerp(foot, acc)
                rr = (0.0062 - 0.0006 * k) * tk
                if near:
                    secs.append((a, rr, rr))
                    secs.append((b, rr * 0.7, rr * 0.7))
                else:
                    secs.append((b, rr, rr))
            v, f = loft(secs, lsides)
            parts.append(make(v, f, 0, pid, THORAX_R, (hip.x, hip.z), weight_path=path))
            if plated:
                parts[-1].seta = SHELL
            if near:
                # the apical spur of the tibia and the paired tarsal claws
                v, f = cone(ankle + Vector((0, 0.004, 0)), ankle + (ankle - knee).normalized() * 0.018 + Vector((0, -0.004, 0)), 0.0026 * tk, 3)
                parts.append(make(v, f, 0, pid, THORAX_R, (hip.x, hip.z), weight_path=path))
                d = (foot - ankle).normalized()
                for c in (-1, 1):
                    v, f = cone(foot, foot + d * 0.008 + Vector((c * 0.006, -0.004, 0)), 0.0018 * tk, 3)
                    parts.append(make(v, f, 0, pid, THORAX_R, (hip.x, hip.z), weight_path=path))
            if plated and fine:
                # a row of short spines down the outer edge of each tibia
                for k in range(3 if near else 2):
                    at = knee.lerp(ankle, 0.3 + 0.25 * k)
                    v, f = cone(at, at + Vector((s * 0.02, 0.006, 0.0)) + (ankle - knee).normalized() * 0.012, 0.0045, 3)
                    parts.append(make(v, f, 0, pid, THORAX_R, (hip.x, hip.z), weight_path=path))
            if hairy and fine:
                # a spider's leg hair: fine bristles standing off the femur and the tibia
                hr = random.Random(pid * 31 + i)
                for a0, b0, cnt in ((troch, knee, 10 if near else 2), (knee, ankle, 11 if near else 2)):
                    for k in range(cnt):
                        at = a0.lerp(b0, (k + 0.6) / (cnt + 0.4))
                        out = Vector((s * hr.uniform(0.2, 1.0), hr.uniform(0.3, 1.0), hr.uniform(-0.6, 0.6))).normalized()
                        v, f = cone(at, at + out * hr.uniform(0.028, 0.046) + (b0 - a0).normalized() * 0.014, 0.0017 if near else 0.0028, 3)
                        parts.append(make(v, f, 0, pid, THORAX_R, (hip.x, hip.z), weight_path=path, seta=SETA))
            for q in parts[first:]:
                q.leg = (s, along[i], hip.copy(), [x.copy() for x in path])

    # ---- setae: erect hairs over the gaster, thorax and head that catch the light
    if near:
        dens = cs.get("setae", 1.0)
        counts = [(GASTER, GASTER_R, int(70 * dens)), (BODY, THORAX_R, int(18 * dens)), (HEAD, HEAD_R, int(14 * dens))]
        body_parts = {p.part: p for p in parts if p.part in (GASTER, BODY, HEAD)}
        for pid, reg, cnt in counts:
            src = [p for p in parts if p.part == pid and p.seta == 0 and len(p.v) > 60]
            if not src:
                continue
            surf = max(src, key=lambda p: len(p.v))
            cand = [k for k, (pp, nn) in enumerate(zip(surf.v, surf.n)) if nn.y > 0.15]
            for _ in range(cnt):
                k = rnd.choice(cand)
                p0, n0 = surf.v[k], surf.n[k]
                d = (n0 + Vector((0, 0.3, -0.9))).normalized()
                ln = rnd.uniform(0.012, 0.026) * (1.2 if pid == GASTER else 1.0)
                v, f = cone(p0 - n0 * 0.001, p0 + d * ln, 0.0008, 3)
                parts.append(make(v, f, 0, pid, reg, seta=1.0))

    build.roots = roots
    return parts, neck, gaster_tip, gaster_c



# ------------------------------------------------------------------------------------------- real specimens (Antscan)

# Which Antscan specimen (micro-CT, CC-BY 4.0; see docs/asset-credits.md) gives each caste its body, made by
# tools/blender/antscan_body.py. The head, mesosoma, waist and gaster come from the scan; the legs, antennae and
# mandibles are the animated ones above, hung from the scan's own hips, sockets and hinges. `head` and `gaster` scale
# those masses about the neck and the waist where a caste departs from its specimen (Lasius has no majors).
SCANS = {
    ("lasius", "Worker"): dict(body="lasius_worker"),
    ("lasius", "Gunner"): dict(body="lasius_worker", gaster=1.25),
    ("lasius", "Soldier"): dict(body="camponotus_minor"),
    ("lasius", "Major"): dict(body="camponotus_major"),
    ("lasius", "Warlord"): dict(body="camponotus_major", head=1.3),
    ("lasius", "Matron"): dict(body="lasius_worker", replete=True),
    # (the black queen keeps her modelled body: the Lasius queen scans tried so far did not part cleanly at her
    # broad waist; see docs/art-pipeline.md)
    # (the male's scanned gaster is torn open, so the winged ant and the stinger do not use it: the modelled gaster hangs there)
    ("lasius", "Flyer"): dict(body="lasius_male", own_gaster=True, proc_gaster=True),
    ("lasius", "Marshal"): dict(body="lasius_male", head=1.1),
    ("solenopsis", "Worker"): dict(body="solenopsis_minor"),
    ("solenopsis", "Gunner"): dict(body="solenopsis_minor", gaster=1.25),
    ("solenopsis", "Soldier"): dict(body="solenopsis_major", head=0.85),
    ("solenopsis", "Major"): dict(body="solenopsis_major"),
    ("solenopsis", "Warlord"): dict(body="solenopsis_major", head=1.3),
    ("solenopsis", "Matron"): dict(body="solenopsis_minor", replete=True),
    ("solenopsis", "Queen"): dict(body="solenopsis_queen"),
    ("solenopsis", "Flyer"): dict(body="solenopsis_queen"),
    ("solenopsis", "Marshal"): dict(body="solenopsis_queen"),
    # the mutation units: the ant half of each merged body is the caste it grew from
    ("lasius", "Stinger"): dict(body="lasius_male", own_gaster=True),
    ("solenopsis", "Stinger"): dict(body="solenopsis_queen", own_gaster=True),
    ("lasius", "WebAnt"): dict(body="camponotus_minor", own_gaster=True),
    ("solenopsis", "WebAnt"): dict(body="solenopsis_major", head=0.85, own_gaster=True),
    ("lasius", "Armoured"): dict(body="camponotus_major", own_gaster=True),
    ("solenopsis", "Armoured"): dict(body="solenopsis_major", own_gaster=True),
    # the scorpion ant keeps the soldier's own scanned gaster: the tail grows out of its tip
    ("lasius", "ScorpionAnt"): dict(body="camponotus_minor"),
    ("solenopsis", "ScorpionAnt"): dict(body="solenopsis_major", head=0.85),
}
BODIES = os.environ.get("ANT_BODIES", "D:/Temp/ant-rebuild/bodies")
LABEL_PART = {0: (HEAD, HEAD_R), 1: (BODY, THORAX_R), 2: (GASTER, GASTER_R), 3: (EYE, HEAD_R)}


def read_body(path):
    with open(path, "rb") as fh:
        assert fh.read(4) == b"BDY1"
        nv, nt = struct.unpack("<II", fh.read(8))
        rows = [struct.unpack("<8fi", fh.read(36)) for _ in range(nv)]
        tris = [struct.unpack("<3I", fh.read(12)) for _ in range(nt)]
    return rows, tris


def build_scanned(species, caste, lod):
    """The caste's body from its specimen scan, with the modelled limbs moved onto it; None when no scan is baked."""
    cfg = SCANS.get((species, caste))
    if not cfg:
        return None
    path = os.path.join(BODIES, f"{cfg['body']}_{lod}.body")
    meta_path = os.path.join(BODIES, cfg["body"] + ".json")
    if not (os.path.exists(path) and os.path.exists(meta_path)):
        return None
    meta = json.load(open(meta_path))
    neck, waist = Vector(meta["neck"]), Vector(meta["waist"])
    hs, gs = cfg.get("head", 1.0), cfg.get("gaster", 1.0)

    def moved(p, label):
        if label in (0, 3):
            return neck + (p - neck) * hs
        if label == 2:
            return waist + (p - waist) * gs
        return p

    rows, tris = read_body(path)
    groups = {}
    for t in tris:
        labs = [rows[i][8] for i in t]
        lab = 3 if labs.count(3) >= 2 else max(set(labs), key=labs.count)
        if (cfg.get("replete") or cfg.get("own_gaster")) and lab == 2:
            continue  # a replete's gaster is the modelled, swollen one; a wasp's, a spider's or a beetle's is merged()'s
        groups.setdefault(lab, []).append(t)
    parts = []
    for lab, ts in groups.items():
        index, vs, ns, uvs, out = {}, [], [], [], []
        for t in ts:
            tri = []
            for i in t:
                if i not in index:
                    index[i] = len(vs)
                    r = rows[i]
                    vs.append(moved(Vector(r[0:3]), r[8]))
                    ns.append(Vector(r[3:6]).normalized())
                    uvs.append((r[6], r[7]))
                tri.append(index[i])
            out.append(tuple(tri))
        part, region = LABEL_PART[lab]
        parts.append(Part(vs, ns, out, part, region, uv=uvs))

    # the modelled ant of the same caste supplies the limbs (and a replete's gaster); move each onto the scan
    proc, _, proc_tip, _ = build(species, caste, lod)
    roots = build.roots
    hips = [Vector(h) for h in meta["hips"]]  # front left, front right, middle left, middle right, hind left, hind right
    targets = {}
    for i in range(3):
        targets[1 + i] = hips[2 * i]
        targets[4 + i] = hips[2 * i + 1]
    targets[7], targets[8] = [moved(Vector(x), 0) for x in meta["sockets"]]
    targets[9], targets[10] = [moved(Vector(x), 0) for x in meta["hinges"]]
    hang = waist - Vector((0, BODY_Y - 0.01, -0.105))
    meso = [p for p in parts if p.part == BODY]
    meso_bvh = BVHTree.FromPolygons([v for p in meso for v in p.v],
                                    [tuple(i + sum(len(q.v) for q in meso[:k]) for i in t) for k, p in enumerate(meso) for t in p.t]) if meso else None
    knees = {}
    for pid, kn in build.knees_local.items():
        if kn is not None and pid in targets and pid in roots:
            knees[pid] = kn + (targets[pid] - roots[pid])
    for p in proc:
        if getattr(p, "spine", None) and (meso_bvh is None or caste in ("Warlord", "Matron", "Marshal")):
            continue  # a general's thorns come from mutate()
        if getattr(p, "spine", None) and meso_bvh:
            kind, base = p.spine
            # the pronotal pair sits behind the neck, the propodeal pair in front of the waist
            z = neck.z - 0.25 * (neck.z - waist.z) if kind == "pronotal" else waist.z + 0.2 * (neck.z - waist.z)
            hit = meso_bvh.ray_cast(Vector((base.x * 0.8, 1.0, z)), Vector((0, -1, 0)), 2.0)[0]
            if hit is not None:
                d = hit - base - Vector((0, 0.004, 0))
                p.v = [v + d for v in p.v]
                parts.append(p)
            continue
        if getattr(p, "leg", None) and CASTES[caste].get("legs8"):
            # eight legs share the specimen's three hips a side: each hangs where it falls between them
            side, t, root, path = p.leg
            hp = [hips[2 * k + (0 if side > 0 else 1)] for k in range(3)]
            k = min(int(t), 1)
            d = hp[k].lerp(hp[k + 1], t - k) - root
            p.v = [v + d for v in p.v]
            p.pivot = (p.pivot[0] + d.x, p.pivot[1] + d.z)
            p.leg = (side, t, root + d, [x + d for x in path])
            parts.append(p)
        elif p.part in targets and p.part in roots:
            d = targets[p.part] - roots[p.part]
            if getattr(p, "leg", None):
                p.leg = (p.leg[0], p.leg[1], p.leg[2] + d, [x + d for x in p.leg[3]])
            p.v = [v + d for v in p.v]
            p.pivot = (p.pivot[0] + d.x, p.pivot[1] + d.z)
            parts.append(p)
        elif (cfg.get("replete") or cfg.get("proc_gaster")) and p.part == GASTER:
            # the swollen gaster (and its setae) hangs from the scan's waist instead of the modelled one
            p.v = [v + hang for v in p.v]
            parts.append(p)
    tip = proc_tip + hang if cfg.get("replete") or cfg.get("proc_gaster") else waist + (Vector(meta["gaster_tip"]) - waist) * gs
    if caste in ("Warlord", "Matron", "Marshal"):
        parts.extend(mutate(parts, species, caste, lod, meta, targets, knees))
    if CASTES[caste].get("merge"):
        # the modelled ant's own gaster parts (setae, sting) never came across: merged() makes the new one
        extra, new_tip = merged(parts, species, caste, lod, meta, cfg, moved)
        parts.extend(extra)
        tip = new_tip or tip
        fit(parts, lod, cfg["body"])
    return parts, neck, tip, Vector(meta["gaster"]), waist


def tangents(part):
    """Per-vertex tangent (xyz, handedness) from the UV derivatives, for the baked normal map."""
    acc = [Vector() for _ in part.v]
    bit = [Vector() for _ in part.v]
    for a, b, c in part.t:
        p0, p1, p2 = part.v[a], part.v[b], part.v[c]
        u0, u1, u2 = part.uv[a], part.uv[b], part.uv[c]
        e1, e2 = p1 - p0, p2 - p0
        du1, dv1, du2, dv2 = u1[0] - u0[0], u1[1] - u0[1], u2[0] - u0[0], u2[1] - u0[1]
        r = du1 * dv2 - du2 * dv1
        if abs(r) < 1e-12:
            continue
        r = 1.0 / r
        t = (e1 * dv2 - e2 * dv1) * r
        bt = (e2 * du1 - e1 * du2) * r
        for i in (a, b, c):
            acc[i] += t
            bit[i] += bt
    out = []
    for n, t, bt in zip(part.n, acc, bit):
        t = t - n * n.dot(t)
        t = t.normalized() if t.length > 1e-9 else n.orthogonal().normalized()
        w = -1.0 if n.cross(t).dot(bt) < 0 else 1.0
        out.append((t.x, t.y, t.z, w))
    return out



# ------------------------------------------------------------------------------------------- the mutated generals (J)

# Target J (docs/art-targets/j-general-*.jpg): the heroes are a lab strain's mutants. Each growth carries a tier
# (CUSTOM0.w): 0 always, 1, 2, 3 grow in at hero levels 4, 7 and 10; the Ant shader collapses a growth whose tier is
# above the instance's (AntRenderer packs the tier where the evolution stage goes for the rank and file).

def thorn(base, direction, length, radius, sides, part, region, tier, pivot=(0.0, 0.0), weight_path=None):
    """A curved thorn: out along `direction`, bending back and up toward the tip."""
    d = direction.normalized()
    back = Vector((0, 0.35, -1)).normalized()
    pts = [base - d * radius * 0.6, base + d * length * 0.35, base + (d * 0.85 + back * 0.25).normalized() * length * 0.7,
           base + (d * 0.7 + back * 0.45).normalized() * length]
    rs = [radius, radius * 0.7, radius * 0.38, radius * 0.06]
    v, f = loft([(p, r, r) for p, r in zip(pts, rs)], sides, cap_start=True, cap_end=False)
    pr = make(v, f, 0, part, region, pivot, weight_path=weight_path)
    pr.tier = tier
    return pr


def mutate(parts, species, caste, lod, meta, targets, knees):
    """Thorns, spurs and wing-cases on a scanned hero body. Returns the new parts."""
    sides = 5 if lod == "near" else 3 if lod == "mid" else 3
    body = [p for p in parts if p.uv is not None]  # the scanned masses
    verts, tris = [], []
    for p in body:
        o = len(verts)
        verts.extend(p.v)
        tris.extend((a + o, b + o, c + o) for a, b, c in p.t)
    bvh = BVHTree.FromPolygons(verts, tris)
    neck, waist = Vector(meta["neck"]), Vector(meta["waist"])
    gas = Vector(meta["gaster"])
    out = []

    def top(x, z):
        """The body's surface straight down from above at (x, z), and its normal there."""
        hit = bvh.ray_cast(Vector((x, 2.0, z)), Vector((0, -1, 0)), 4.0)
        return (hit[0], hit[1]) if hit[0] is not None else (None, None)

    def row(z0, z1, n, spread, length, radius, part, region, tier0, every=1):
        for k in range(n):
            t = (k + 0.5) / n
            z = z0 + (z1 - z0) * t
            for s in (-1, 1):
                at, nrm = top(s * spread, z)
                if at is None:
                    continue
                d = (nrm + Vector((s * 0.6, 0.4, 0))).normalized()
                tier = tier0 if k % every == 0 else min(3, tier0 + 1)
                out.append(thorn(at - nrm * 0.002, d, length, radius, sides, part, region, tier))

    span = neck.z - waist.z
    if caste == "Warlord":
        # thorax: two rows of long thorns, a second row grows in with the levels
        row(waist.z + 0.15 * span, neck.z - 0.1 * span, 3, 0.03, 0.11, 0.016, BODY, THORAX_R, 0)
        row(waist.z + 0.25 * span, neck.z - 0.2 * span, 2, 0.05, 0.08, 0.012, BODY, THORAX_R, 1)
        # a crown of horns along the back of the head
        head = Vector(meta["head"])
        hz = head.z - (head.z - neck.z) * 0.35
        row(hz - 0.04, hz + 0.04, 2, 0.04, 0.09, 0.014, HEAD, HEAD_R, 0)
        row(hz - 0.07, hz - 0.03, 1, 0.075, 0.13, 0.016, HEAD, HEAD_R, 3)
        # the gaster's back bristles with short spikes at the top tiers
        row(gas.z - 0.1, gas.z + 0.08, 3, 0.05, 0.06, 0.012, GASTER, GASTER_R, 2)
    elif caste == "Matron":
        # a crown of short thorns on the thorax
        row(waist.z + 0.2 * span, neck.z - 0.15 * span, 2, 0.03, 0.06, 0.011, BODY, THORAX_R, 0)
        row(waist.z + 0.1 * span, neck.z - 0.3 * span, 2, 0.045, 0.05, 0.01, BODY, THORAX_R, 2)
    elif caste == "Marshal":
        row(waist.z + 0.25 * span, neck.z - 0.25 * span, 2, 0.03, 0.07, 0.012, BODY, THORAX_R, 1)
        row(gas.z - 0.08, gas.z + 0.04, 2, 0.045, 0.05, 0.01, GASTER, GASTER_R, 2)
        # wing-cases: two hardened chitin plates over the forewings' bases, curving back over the waist
        for s in (-1, 1):
            root, nrm = top(s * 0.045, neck.z - 0.45 * span)
            if root is None:
                continue
            secs = []
            for k in range(6):
                t = k / 5
                c = root + Vector((s * (0.02 + 0.03 * t), 0.015 - 0.012 * t, -0.42 * t * span - 0.05 * t))
                secs.append((c, 0.035 * (1 - 0.6 * t) + 0.004, 0.009 * (1 - 0.5 * t) + 0.002))
            v, f = loft(secs, 6)
            pr = make(v, f, 1 if lod != "far" else 0, BODY, THORAX_R)
            pr.tier = 0
            out.append(pr)
    # spurs at every knee, for the Warlord from the start and the others at the top tier
    if lod != "far":
        for pid, knee in knees.items():
            if pid not in targets:
                continue
            leg = next((p for p in parts if p.part == pid), None)
            if leg is None:
                continue
            side = 1 if pid <= 3 else -1
            d = Vector((side * 0.4, 1, 0.2)).normalized()
            pr = thorn(knee, d, 0.04, 0.008, sides, pid, THORAX_R, 0 if caste == "Warlord" else 3, leg.pivot)
            pr.w = [0.45] * len(pr.v)
            out.append(pr)
    return out


# ------------------------------------------------------------------------------------------- the mutation units (P1-P4)

# Targets P1-P4 (docs/art-targets/p1-winged-ant.jpg ... p4-armoured-ant.jpg). The ant half of each unit is its caste's
# real specimen; merged() grows the other insect's anatomy out of it: a fly's eyes, halteres and bristles; a wasp's
# banded gaster and sting; a spider's extra eyes, hairy gaster, spinnerets and a net of silk held in a front leg (its
# eight legs come from build()); a beetle's pronotum shield and ridged wing cases (its plated legs come from build()).

# triangles per level of detail (near, mid, far): what the caste each unit grew from already costs on its specimen
BUDGET = {"lasius_male": (10700, 5221, 4673), "solenopsis_queen": (10700, 4742, 4218), "camponotus_minor": (10700, 3296, 2482),
          "solenopsis_major": (10700, 3295, 1830), "camponotus_major": (10700, 4384, 3836)}


def decimated(part, ratio):
    """A scanned mass at a lower triangle count, its normal-map coordinates carried along."""
    me = bpy.data.meshes.new("dec")
    me.from_pydata([tuple(v) for v in part.v], [], [tuple(t) for t in part.t])
    layer = me.uv_layers.new(name="uv")
    for loop in me.loops:
        layer.data[loop.index].uv = part.uv[loop.vertex_index]
    for poly in me.polygons:
        poly.use_smooth = True
    ob = bpy.data.objects.new("dec", me)
    bpy.context.scene.collection.objects.link(ob)
    mod = ob.modifiers.new("dec", "DECIMATE")
    mod.ratio = ratio
    ev = ob.evaluated_get(bpy.context.evaluated_depsgraph_get())
    m = ev.to_mesh()
    m.calc_loop_triangles()
    uv = [None] * len(m.vertices)
    for loop in m.loops:
        if uv[loop.vertex_index] is None:
            uv[loop.vertex_index] = tuple(m.uv_layers[0].data[loop.index].uv)
    part.v = [v.co.copy() for v in m.vertices]
    part.n = [v.normal.copy() for v in m.vertices]
    part.t = [tuple(t.vertices) for t in m.loop_triangles]
    part.uv = [u or (0.0, 0.0) for u in uv]
    part.w = [0.0] * len(part.v)
    part.m = [0.0] * len(part.v)
    ev.to_mesh_clear()
    bpy.data.objects.remove(ob)
    bpy.data.meshes.remove(me)


def fit(parts, lod, body):
    """Keeps a merged body inside its level of detail's budget: a third of the setae may go, then the scanned masses
    give up triangles (they are the densest part of the mesh), then more setae."""
    budget = BUDGET[body][("near", "mid", "far").index(lod)]
    total = lambda: sum(len(q.t) for q in parts)
    if total() <= budget:
        return
    hairs = [q for q in parts if q.seta == SETA or getattr(q, "hair", False)]
    random.Random(11).shuffle(hairs)

    def thin(limit):
        drop = set()
        over = total() - budget
        for q in hairs[:limit]:
            if over <= 0:
                break
            if id(q) in gone:
                continue
            over -= len(q.t)
            drop.add(id(q))
        gone.update(drop)
        parts[:] = [q for q in parts if id(q) not in drop]

    gone = set()
    thin(len(hairs) // 3)
    over = total() - budget
    scans = [q for q in parts if q.uv is not None and q.part != EYE]
    have = sum(len(q.t) for q in scans)
    if over > 0 and have > 0:
        ratio = max(0.2, 1 - over / have * 1.06)
        for q in scans:
            if len(q.t) > 40:
                decimated(q, ratio)
    thin(len(hairs))
    if total() > budget:
        print(f"[ants] WARNING: {total() - budget} triangles over the {lod} budget of {budget}")


def shell(centre, radius, th0, th1, nz, nth, part, region, ridges=0.0, thick=0.007):
    """A curved chitin plate with a real edge: a grid along the body (t = 0..1) and round it (u = 0..1 from angle th0
    to th1, pi/2 straight up). centre(t) is the axis point, radius(t, u) the distance from it."""
    verts, faces, coord = [], [], []
    for layer in (0, 1):
        for i in range(nz + 1):
            t = i / nz
            c = centre(t)
            for j in range(nth + 1):
                u = j / nth
                th = th0 + (th1 - th0) * u
                r = max(radius(t, u) - layer * thick, 0.002)
                verts.append(c + Vector((math.cos(th) * r, math.sin(th) * r, 0)))
                coord.append((u * ridges, t))
    n1 = (nz + 1) * (nth + 1)
    for i in range(nz):
        for j in range(nth):
            a = i * (nth + 1) + j
            faces.append((a, a + 1, a + nth + 2, a + nth + 1))
            faces.append((n1 + a, n1 + a + nth + 1, n1 + a + nth + 2, n1 + a + 1))
    ring = [j for j in range(nth + 1)] + [i * (nth + 1) + nth for i in range(1, nz + 1)] + \
           [nz * (nth + 1) + j for j in range(nth - 1, -1, -1)] + [i * (nth + 1) for i in range(nz - 1, 0, -1)]
    for k in range(len(ring)):
        a, b = ring[k], ring[(k + 1) % len(ring)]
        faces.append((a, n1 + a, n1 + b, b))
    vs, ns, ts = subdivided(verts, faces, 0)
    pr = Part(vs, ns, ts, part, region, seta=SHELL)
    pr.coord = coord
    return pr


def strand(a, b, r, sides=3):
    return loft([(a, r, r), (b, r, r)], sides, cap_start=False, cap_end=False)


def merged(parts, species, caste, lod, meta, cfg, moved):
    """The other insect's anatomy on a scanned ant. Returns (new parts, the new gaster tip or None)."""
    kind = CASTES[caste]["merge"]
    near, far = lod == "near", lod == "far"
    sides = {"near": 8, "mid": 6, "far": 5}[lod]
    lv = 0 if far else 1
    body = [q for q in parts if q.uv is not None]
    verts, tris = [], []
    for q in body:
        o = len(verts)
        verts.extend(q.v)
        tris.extend((a + o, b + o, c + o) for a, b, c in q.t)
    bvh = BVHTree.FromPolygons(verts, tris)
    neck, waist = Vector(meta["neck"]), Vector(meta["waist"])
    head = moved(Vector(meta["head"]), 0)
    hw = meta["head_width"] * cfg.get("head", 1.0)
    span = neck.z - waist.z
    rnd = random.Random(sum(ord(ch) for ch in species + caste))
    out, tip = [], None

    def top(x, z):
        hit = bvh.ray_cast(Vector((x, 2.0, z)), Vector((0, -1, 0)), 4.0)
        return (hit[0], hit[1]) if hit[0] is not None else (None, None)

    def reach(c, th):
        """How far the body reaches from the axis point c at angle th round it."""
        d = Vector((math.cos(th), math.sin(th), 0))
        hit = bvh.ray_cast(c + d, -d, 1.0)
        return 1.0 - hit[3] if hit[0] is not None else 0.0

    def bristles(count, z0, z1, spread, length, radius):
        for _ in range(count):
            at, nrm = top(rnd.uniform(-spread, spread), rnd.uniform(z0, z1))
            if at is None or nrm.y < 0.2:
                continue
            d = (nrm + Vector((0, 0.25, -0.7))).normalized()
            v, f = cone(at - nrm * 0.002, at + d * rnd.uniform(length * 0.65, length), radius, 3)
            out.append(make(v, f, 0, BODY, THORAX_R, seta=SETA))

    def lofted_gaster(length, width, height, peak, droop, rise, n, flag, stalk=0.0, segs=0):
        """A gaster hung from the waist: a narrow stalk over the first `stalk` of its length, a swell (widest at `peak`
        of the rest) and a tapering tip; `segs` tergites, each rear rim standing proud of the next."""
        secs = []
        z0 = waist.z - 0.012
        for i in range(n + 1):
            t = i / n
            u = (t - stalk) / (1 - stalk)
            env = math.sin(math.pi * u ** (math.log(0.5) / math.log(peak))) ** 0.75 if 0 < u < 1 else 0.0
            if segs and 0.06 < u < 0.94:
                fr = u * segs - math.floor(u * segs)
                env *= 1 + 0.07 * (1 - fr) ** 3
            c = Vector((0, waist.y - 0.006 + rise * math.sin(math.pi * t) - droop * t * t, z0 - length * t))
            secs.append((c, 0.012 + width * env, 0.012 + height * env))
        v, f = loft(secs, sides + 2)
        pr = make(v, f, lv, GASTER, GASTER_R)
        pr.seta = flag
        pr.coord = [(min(1.0, max(0.0, (z0 - q.z) / length)), 0.0) for q in pr.v]
        out.append(pr)
        return pr, secs

    if kind in ("fly", "wasp"):
        # a fly's eyes: two great red domes that take the whole flank of the head
        for s, e in zip((1, -1), meta["eyes"]):
            ec = moved(Vector(e), 0)
            o = Vector((s, 0.18, 0.1)).normalized()
            c = ec - o * 0.07 * hw + Vector((0, 0.02 * hw, 0.03 * hw))
            v, f = ellipsoid(c, 0.36 * hw, 0.31 * hw, 0.23 * hw, 6 if near else 4, sides, up=Vector((0, 0, 1)), fwd=o)
            pr = make(v, f, lv, EYE, HEAD_R)
            pr.seta = FLYEYE
            out.append(pr)
        if not far:
            # halteres: the fly's second wing pair, shrunk to two knobbed stalks behind the wings
            for s in (-1, 1):
                base, nrm = top(s * 0.035, waist.z + 0.2 * span)
                if base is None:
                    continue
                knob = base + Vector((s * 0.075, 0.045, -0.045))
                v, f = loft([(base - nrm * 0.003, 0.006, 0.006), (base.lerp(knob, 0.6), 0.004, 0.004), (knob, 0.004, 0.004)], 4)
                out.append(make(v, f, 0, BODY, THORAX_R))
                v, f = ellipsoid(knob, 0.016, 0.016, 0.02, 4, 6, fwd=knob - base)
                out.append(make(v, f, lv, BODY, THORAX_R, membrane_fn=lambda q: 1.0))
            # the bristles of a fly's flight thorax, and a few on the crown
            bristles(70 if near else 16, waist.z + 0.12 * span, waist.z + 0.92 * span, 0.05, 0.065, 0.0022 if near else 0.0036)
            bristles(10 if near else 0, head.z - 0.3 * (head.z - neck.z), head.z + 0.3 * (head.z - neck.z), 0.03, 0.035, 0.0014)

    if kind == "wasp":
        # a wasp's gaster: spindle-shaped, hung low from a narrow waist, banded amber and black, tapering to the sting
        n = 16 if near else 10 if not far else 6
        n = 20 if near else 12 if not far else 7
        pr, secs = lofted_gaster(0.46, 0.09, 0.084, 0.36, 0.12, 0.012, n, BANDS, stalk=0.15, segs=5)
        # the fine pale hair of a wasp's gaster
        cand = [k for k, nn in enumerate(pr.n) if nn.y > -0.2 and pr.coord[k][0] > 0.2]
        for _ in range(50 if near else 0):
            k = rnd.choice(cand)
            d = (pr.n[k] + Vector((0, 0.1, -0.8))).normalized()
            v, f = cone(pr.v[k] - pr.n[k] * 0.002, pr.v[k] + d * rnd.uniform(0.014, 0.024), 0.001, 3)
            out.append(make(v, f, 0, GASTER, GASTER_R, seta=SETA))
        end = secs[-1][0]
        pts = [end + Vector((0, 0.004, 0.02)), end + Vector((0, -0.028, -0.035)), end + Vector((0, -0.07, -0.045)),
               end + Vector((0, -0.108, -0.022)), end + Vector((0, -0.124, 0.02))]
        rs = [0.02, 0.016, 0.0115, 0.007, 0.0016]
        if not far:
            pts, rs = resample(pts, rs, 2)
        v, f = loft([(q, r, r) for q, r in zip(pts, rs)], 5 if near else 4, cap_start=True, cap_end=False)
        out.append(make(v, f, 0, EYE, GASTER_R))
        tip = pts[-1]
        if not far:
            # the bead of venom gathering at its point
            v, f = ellipsoid(tip + Vector((0, -0.012, 0.004)), 0.014, 0.018, 0.014, 4, 6)
            out.append(make(v, f, lv, GASTER, GASTER_R, membrane_fn=lambda q: 1.0))

    if kind == "spider":
        # a spider's simple eyes: two more pairs on the brow, between and above the ant's own
        hl = head.z - neck.z
        for x, zf, r in ((0.13, 0.62, 0.095), (0.29, 0.3, 0.08)):
            for s in (-1, 1):
                at, nrm = top(s * x * hw, head.z + zf * hl)
                if at is None:
                    continue
                v, f = ellipsoid(at - nrm * r * hw * 0.1, r * hw, r * hw, r * hw * 0.85, 3 if not near else 4, 6 if not far else 4, fwd=nrm, up=Vector((0, 0, 1)))
                out.append(make(v, f, lv, EYE, HEAD_R))
                out[-1].seta = SPIDEREYE
        # a spider's abdomen where the gaster was: a soft hairy ovoid
        n = 14 if near else 9 if not far else 6
        pr, secs = lofted_gaster(0.4, 0.122, 0.112, 0.5, 0.02, 0.035, n, VELVET, stalk=0.08)
        cand = [k for k, nn in enumerate(pr.n) if nn.y > -0.45]
        for _ in range(230 if near else 30 if not far else 0):
            k = rnd.choice(cand)
            p0, n0 = pr.v[k], pr.n[k]
            d = (n0 + Vector((0, 0.15, -0.55))).normalized()
            v, f = cone(p0 - n0 * 0.002, p0 + d * rnd.uniform(0.028, 0.05), 0.0012 if near else 0.0026, 3)
            out.append(make(v, f, 0, GASTER, GASTER_R, seta=VELVET))  # dark like the pelt they stand in, not an ant's golden setae
            out[-1].hair = True
        # spinnerets: a pair and a median one, pale, at its tip
        end = secs[-1][0]
        tip = end + Vector((0, -0.012, -0.02))
        for dx, dy in ((-0.024, -0.004), (0.024, -0.004), (0.0, -0.028), (-0.012, 0.016), (0.012, 0.016)):
            a = end + Vector((dx, dy, 0.03))
            v, f = loft([(a, 0.016, 0.016), (a + Vector((dx * 0.4, -0.014, -0.045)), 0.0125, 0.0125), (a + Vector((dx * 0.7, -0.024, -0.078)), 0.005, 0.005)], 5 if not far else 3)
            out.append(make(v, f, 0, GASTER, GASTER_R, membrane_fn=lambda q: 0.5))
        bristles(26 if near else 8 if not far else 0, waist.z + 0.1 * span, waist.z + 0.9 * span, 0.04, 0.035, 0.0013 if near else 0.0026)
        # the net: an orb of silk held ready in the left front leg, to be thrown
        leg = next((q for q in parts if q.part == 1 and getattr(q, "leg", None) and q.leg[1] == 0.0), None)
        if leg is not None:
            path = leg.leg[3]
            ankle, foot = path[3], path[4]
            c = foot + Vector((0.0, 0.165, 0.035))
            ax_u = Vector((0.72, 0.0, 0.69)).normalized()
            ax_v = Vector((-0.1, 1.0, 0.1)).normalized()
            spokes = 10 if near else 5
            rings = (0.25, 0.45, 0.64, 0.82, 1.0) if near else (0.5, 1.0) if not far else (1.0,)
            r = 0.0009 if near else 0.0018 if not far else 0.0032
            rim = [c + ax_u * (math.cos(2 * math.pi * k / spokes + 0.3) * 0.17) + ax_v * (math.sin(2 * math.pi * k / spokes + 0.3) * 0.15) for k in range(spokes)]
            # the lowest point of its rim is gathered into the claws of the foot that holds it
            lowest = min(range(spokes), key=lambda k: rim[k].y)
            rim[lowest] = foot + Vector((0, 0.012, 0))
            silk = []
            for k in range(spokes):
                silk.append(strand(c, rim[k], r))
                for g in rings:
                    a, b = c.lerp(rim[k], g), c.lerp(rim[(k + 1) % spokes], g)
                    mid = a.lerp(b, 0.5).lerp(c, 0.1)  # each thread sags toward the hub
                    silk.append(strand(a, mid, r))
                    silk.append(strand(mid, b, r))
            for k in ((lowest - 1) % spokes, (lowest + 1) % spokes):
                silk.append(strand(rim[k], foot.lerp(ankle, 0.5), r))  # the guy threads run up the tarsus
            silk.append(strand(c, foot.lerp(ankle, 0.25), r))
            for v, f in silk:
                pr = make(v, f, 0, 1, THORAX_R, leg.pivot)
                pr.w = [0.92] * len(pr.v)
                pr.seta = SILK
                out.append(pr)

    if kind == "beetle":
        cy = Vector(meta["mesosoma"]).y
        # the pronotum: a broad shield over the front of the thorax, its margins flared like a ground beetle's
        zA, zB = neck.z + 0.035, neck.z - 0.62 * span
        th0, th1 = 0.22, math.pi - 0.22
        grid = {}
        def pro_r(t, u):
            key = (round(t, 4), round(u, 4))
            if key not in grid:
                c = Vector((0, cy, zA + (zB - zA) * min(max(t, 0.12), 0.95)))
                th = th0 + (th1 - th0) * u
                dome = math.sin(math.pi * t) ** 0.45 if 0 < t < 1 else 0.0
                flare = 0.012 * abs(math.cos(th)) ** 2 + 0.03 * abs(2 * u - 1) ** 5
                lip = 0.014 * max(abs(2 * u - 1), abs(2 * t - 1)) ** 10  # the raised margin all round the shield
                grid[key] = max(reach(c, th), 0.03) + 0.003 + (0.007 + flare) * (0.3 + 0.7 * dome) + lip
            return grid[key]
        out.append(shell(lambda t: Vector((0, cy, zA + (zB - zA) * t)), pro_r, th0, th1,
                         10 if near else 6 if not far else 3, 22 if near else 12 if not far else 6, BODY, THORAX_R, thick=0.012))
        # the wing cases: two ridged elytra closed over the abdomen, meeting at the suture
        L0, W, Hh = 0.45, 0.165, 0.135
        zf = waist.z - 0.03
        def axis(t):
            return Vector((0, waist.y - 0.012 + 0.03 * math.sin(math.pi * t) - 0.035 * t * t, zf - L0 * t))
        e0, e1 = -0.55, math.pi + 0.55
        def ely_r(t, u):
            th = e0 + (e1 - e0) * u
            env = max(math.sin(math.pi * min(max(t, 0.0), 1.0) ** 0.72) ** 0.6 if 0 < t < 1 else 0.0, 0.1)
            r = env * W * Hh / math.sqrt((Hh * math.cos(th)) ** 2 + (W * math.sin(th)) ** 2)
            return r * (1 - 0.04 * math.exp(-((th - math.pi / 2) / 0.06) ** 2))
        out.append(shell(axis, ely_r, e0, e1, 14 if near else 8 if not far else 5, 38 if near else 20 if not far else 12,
                         GASTER, GASTER_R, ridges=26.0, thick=0.006))
        # the soft abdomen under them
        v, f = ellipsoid(axis(0.47) + Vector((0, -0.028, 0)), W * 0.78, Hh * 0.6, L0 * 0.44, 6 if not far else 4, sides)
        out.append(make(v, f, lv, GASTER, GASTER_R))
        tip = axis(1.0)
        bristles(14 if near else 0, zB, waist.z + 0.1 * span, 0.03, 0.03, 0.0013)

    if kind == "scorpion":
        # a scorpion's tail grown out of the tip of the ant's own gaster: five barrel segments arching up and forward
        # over the back, a swollen bulb, a curved sting and its bead of venom. One body: the tail's coordinate runs
        # from 0 at the root to 1 at the point, and the shader carries the ant's own dark shell along it (TAIL)
        G = waist + (Vector(meta["gaster_tip"]) - waist) * cfg.get("gaster", 1.0)
        s = max((G - waist).length, 0.2)
        b, u = Vector((0, 0, -1)), Vector((0, 1, 0))
        P = [G - b * 0.34 * s, G + b * 0.18 * s + u * 0.08 * s, G + b * 0.50 * s + u * 0.36 * s, G + b * 0.58 * s + u * 0.80 * s,
             G + b * 0.40 * s + u * 1.20 * s, G + b * 0.03 * s + u * 1.44 * s]
        R = [0.175, 0.15, 0.136, 0.124, 0.114]
        total = 6.4
        ss = 10 if near else 8 if not far else 5
        tl = lv if near else 0   # the mid mesh keeps its budget: the barrels are lofted fine enough without a subdivision

        def tail_part(v, f, a, bb, k0, k1, level):
            pr = make(v, f, level, GASTER, GASTER_R)
            pr.seta = TAIL
            ab = bb - a
            L2 = max(ab.length_squared, 1e-9)
            pr.coord = []
            for q in pr.v:
                tl = min(1.0, max(0.0, (q - a).dot(ab) / L2))
                pr.coord.append(((k0 + (k1 - k0) * tl) / total, tl))
            out.append(pr)
            return pr

        for i in range(5):
            a, bb, r = P[i], P[i + 1], R[i] * s
            if far:
                secs = [(a, r * 0.8, r * 0.8), (a.lerp(bb, 0.5), r, r), (bb, r * 0.78, r * 0.78)]
            else:
                r0 = r * (1.55 if i == 0 else 0.66)   # the first segment swells out of the gaster itself: no joint, one body
                secs = [(a, r0, r0), (a.lerp(bb, 0.14), r * max(0.97, r0 / r * 0.82), r * max(1.0, r0 / r * 0.82)), (a.lerp(bb, 0.5), r * 1.0, r * 1.04),
                        (a.lerp(bb, 0.86), r * 1.02, r * 1.05), (bb, r * 0.68, r * 0.68)]
            v, f = loft(secs, ss, up_hint=Vector((1, 0, 0)))
            tail_part(v, f, a, bb, i, i + 1, tl)
            if near:
                # the tail's own fine hairs, as on the gaster it grew from
                d = (bb - a).normalized()
                side = Vector((1, 0, 0))
                upv = d.cross(side).normalized()
                for _ in range(7):
                    th = rnd.uniform(0, 2 * math.pi)
                    o = (side * math.cos(th) + upv * math.sin(th))
                    base = a.lerp(bb, rnd.uniform(0.2, 0.85)) + o * r * 0.96
                    hv, hf = cone(base, base + (o + d * 0.5).normalized() * rnd.uniform(0.035, 0.06) * s * 2.2, 0.0016, 3)
                    out.append(make(hv, hf, 0, GASTER, GASTER_R, seta=SETA))
        # the bulb: swollen, hung forward of the last joint
        fwd = (P[5] - P[4]).normalized()
        bc = P[5] + (-b) * 0.15 * s - u * 0.02 * s
        v, f = ellipsoid(bc, 0.13 * s, 0.125 * s, 0.17 * s, 6 if near else 4, ss, fwd=-b)
        tail_part(v, f, bc + b * 0.17 * s, bc - b * 0.17 * s, 5.0, 5.8, tl)
        # the sting: a curved needle from the front of the bulb, down towards the ant's own head
        s0 = bc - b * 0.14 * s - u * 0.03 * s
        pts = [s0, s0 - b * 0.09 * s - u * 0.045 * s, s0 - b * 0.14 * s - u * 0.13 * s, s0 - b * 0.13 * s - u * 0.23 * s]
        rs = [0.06 * s, 0.04 * s, 0.021 * s, 0.004 * s]
        if not far:
            pts, rs = resample(pts, rs, 2)
        v, f = loft([(q, r, r) for q, r in zip(pts, rs)], 5 if near else 4, cap_start=True, cap_end=False)
        tail_part(v, f, pts[0], pts[-1], 5.8, 6.4, 0)
        if not far:
            v, f = ellipsoid(pts[-1] + Vector((0, -0.008, 0)), 0.0085, 0.0105, 0.0085, 4, 6)
            out.append(make(v, f, lv, GASTER, GASTER_R, membrane_fn=lambda q: 1.0))
        tip = pts[-1]
    return out, tip



def plate(c, R, lat, lon, ext, thick, sides, lv):
    """A curved tergite plate on a sphere of radius R about c: latitude/longitude of its centre (radians-ish),
    angular extent; a thin slab so its rim reads from the side."""
    faces = []
    n = 5
    verts = []
    for i in range(n + 1):
        for j in range(n + 1):
            th = (math.pi / 2 if lat > 0 else -math.pi / 2) + (i / n - 0.5) * ext * 2.2   # around the long axis, from the top
            ph = lon + (j / n - 0.5) * ext                                                 # along the long axis
            d = Vector((math.cos(th) * math.cos(ph), math.sin(th) * math.cos(ph), math.sin(ph)))
            verts.append(c + d * R)
    inner = len(verts)
    for i in range(n + 1):
        for j in range(n + 1):
            p = verts[i * (n + 1) + j]
            verts.append(c + (p - c).normalized() * (R - 0.006))
    for i in range(n):
        for j in range(n):
            a = i * (n + 1) + j
            faces.append((a, a + 1, a + n + 2, a + n + 1) if lat > 0 else (a, a + n + 1, a + n + 2, a + 1))
            b = inner + a
            faces.append((b, b + n + 1, b + n + 2, b + 1) if lat > 0 else (b, b + 1, b + n + 2, b + n + 1))
    # rim
    ring = [i * (n + 1) for i in range(n + 1)] + [n * (n + 1) + j for j in range(1, n + 1)] + \
           [i * (n + 1) + n for i in range(n - 1, -1, -1)] + [j for j in range(n - 1, 0, -1)]
    for k in range(len(ring)):
        a, b = ring[k], ring[(k + 1) % len(ring)]
        faces.append((a, inner + a, inner + b, b) if lat > 0 else (a, b, inner + b, inner + a))
    vs, ns, ts = subdivided(verts, faces, min(lv, 1))
    return Part(vs, ns, ts, GASTER, GASTER_R)


# ------------------------------------------------------------------------------------------- occlusion and output

def bake_ao(parts, rays=20, reach=0.12):
    verts, tris = [], []
    for p in parts:
        o = len(verts)
        verts.extend(p.v)
        tris.extend((a + o, b + o, c + o) for a, b, c in p.t)
    bvh = BVHTree.FromPolygons(verts, tris)
    rnd = random.Random(7)
    dirs = []
    while len(dirs) < rays:
        d = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1)))
        if 0.1 < d.length <= 1:
            dirs.append(d.normalized())
    out = []
    for p in parts:
        ao = []
        for v, n in zip(p.v, p.n):
            hit, tot = 0.0, 0.0
            for d in dirs:
                c = d.dot(n)
                if c <= 0.05:
                    continue
                tot += c
                o = v + n * 0.002
                h = bvh.ray_cast(o, d, reach)
                ground = d.y < 0 and (-o.y / d.y) < reach
                if h[0] is not None or ground:
                    hit += c
            occ = hit / tot if tot > 0 else 0
            ao.append(max(0.0, 1 - occ * 0.85))
        out.append(ao)
    return out


def write_bin(path, parts, ao):
    """Little-endian: 'ANT2', vertex count, index count; then per vertex position(3) normal(3) color(4) uv(2) uv2(2)
    custom0(4: membrane, seta, textured, 0) tangent(4) as float32; then the triangle indices as uint32."""
    vbuf, ibuf = [], []
    nv = 0
    for p, a in zip(parts, ao):
        tan = tangents(p) if p.uv else None
        for k, (v, n) in enumerate(zip(p.v, p.n)):
            uv = p.uv[k] if p.uv else p.coord[k] if p.coord else (0.0, 0.0)
            tg = tan[k] if tan else (1.0, 0.0, 0.0, 1.0)
            vbuf.append((v.x, v.y, v.z, n.x, n.y, n.z, p.part / 255.0, p.w[k], p.region, a[k], uv[0], uv[1],
                         p.pivot[0], p.pivot[1], p.m[k], p.seta, 1.0 if p.uv else 0.0, float(p.tier)) + tuple(tg))
        for t in p.t:
            ibuf.append((t[0] + nv, t[1] + nv, t[2] + nv))
        nv += len(p.v)
    with open(path, "wb") as fh:
        fh.write(b"ANT2")
        fh.write(struct.pack("<II", len(vbuf), len(ibuf) * 3))
        for row in vbuf:
            fh.write(struct.pack("<22f", *row))
        for t in ibuf:
            # Godot's front faces wind clockwise
            fh.write(struct.pack("<3I", t[0], t[2], t[1]))
    return len(ibuf)


def main():
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    only = sys.argv[sys.argv.index("--") + 2:] if len(sys.argv) > sys.argv.index("--") + 2 else []
    manifest = {}
    for species in SPECIES:
        for caste in CASTES:
            if only and caste not in only:
                continue
            for lod in ("near", "mid", "far"):
                scanned = build_scanned(species, caste, lod)
                if scanned:
                    parts, neck, tip, gc, waist = scanned
                    body = SCANS[(species, caste)]["body"]
                    png = os.path.join(BODIES, body + "_normal.png")
                    if os.path.exists(png):
                        shutil.copyfile(png, os.path.join(OUT, body + "_normal.png"))
                else:
                    parts, neck, tip, gc = build(species, caste, lod)
                    waist, body = Vector((0, 0.14, -0.105)), None
                ao = bake_ao(parts, 20 if lod != "far" else 10)
                name = f"{species}_{caste}_{lod}"
                tris = write_bin(os.path.join(OUT, name + ".bin"), parts, ao)
                manifest[name] = dict(neck=list(neck), gaster_tip=list(tip), gaster_centre=list(gc), waist=list(waist),
                                      detail=(body + "_normal") if body else "", tris=tris)
                print(f"[ants] {name}: {tris} triangles" + (f" (scan {body})" if body else ""))
    with open(os.path.join(OUT, "manifest.json"), "w") as fh:
        json.dump(manifest, fh, indent=1)


if __name__ == "__main__":
    main()
