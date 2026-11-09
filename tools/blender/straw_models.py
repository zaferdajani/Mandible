"""Models dry grass straws (level 1's bridge, art targets K1-K3) in Blender, headless, and exports each as glTF.

Run: blender --background --factory-startup --python tools/blender/straw_models.py -- <out-dir>
Writes <out-dir>/straw_<n>/straw_<n>.gltf. A straw is a dead grass culm: hollow, tapering a little toward the tip,
swollen at its nodes, gently bowed, crushed flat in places, cut on a slant at the base and split into frayed strips
at the tip, with a papery leaf sheath still wrapped round one node. Real size (metres): 45 mm long, 2.2 mm across.
UVs run the texture's fibres (Poly Haven bamboo_veneer, also a grass culm) along the length; vertex colours darken the
nodes and pale the sheath. Each straw is built from a seed, so a run always gives the same meshes.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Vector, noise

OUT = sys.argv[sys.argv.index("--") + 1]
LEN, RAD = 0.045, 0.00075


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def export(name, objs):
    for o in bpy.context.scene.objects:
        o.select_set(o in objs)
    bpy.context.view_layer.objects.active = objs[0]
    d = os.path.join(OUT, name)
    os.makedirs(d, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(d, name + ".gltf"), export_format="GLTF_SEPARATE",
                              use_selection=True, export_apply=True, export_normals=True, export_texcoords=True,
                              export_materials="NONE", export_vertex_color="ACTIVE")
    print("[blender] wrote", name)


def tube(bm, col, uv, rings, around, at, radius, colour, inner=False, arc=1.0, fray=None):
    """A ring-swept surface: at(t) -> (centre, frame x/y), radius(t, a) -> r. Returns nothing; adds faces to bm."""
    verts = []
    for i in range(rings + 1):
        t = i / rings
        c, fx, fy = at(t)
        row = []
        for j in range(around + 1):
            a = (j / around) * arc * 2 * math.pi
            r = radius(t, a)
            if fray:
                r, c2 = fray(t, a, r, c)
            else:
                c2 = c
            p = c2 + (fx * math.cos(a) + fy * math.sin(a)) * r
            row.append(bm.verts.new(p))
        verts.append(row)
    for i in range(rings):
        for j in range(around):
            q = [verts[i][j], verts[i][j + 1], verts[i + 1][j + 1], verts[i + 1][j]]
            if inner:
                q.reverse()
            f = bm.faces.new(q)
            for k, l in enumerate(f.loops):
                ii = i + (1 if k in (2, 3) else 0)
                jj = j + (1 if k in (1, 2) else 0)
                if inner:
                    ii = i + (1 if k in (0, 1) else 0)
                l[uv].uv = (ii / rings * 2.6, jj / around * 0.22 + (0.5 if inner else 0))
                l[col] = colour(ii / rings, jj / around)
    return verts


def straw(seed):
    rnd = random.Random(seed)
    bm = bmesh.new()
    col = bm.loops.layers.color.new("Col")
    uv = bm.loops.layers.uv.new("UVMap")
    nodes = sorted([rnd.uniform(0.22, 0.34), rnd.uniform(0.58, 0.72)])
    bow = rnd.uniform(0.0004, 0.0009) * rnd.choice((-1, 1))
    lift = rnd.uniform(0.0002, 0.0005)
    flutes = rnd.choice((8, 9, 11))
    crush_at, crush_w = rnd.uniform(0.4, 0.85), rnd.uniform(0.05, 0.1)
    tip_split = rnd.uniform(0.88, 0.93)
    strips = rnd.choice((3, 4))
    off = rnd.uniform(0, 100)

    def centre(t):
        x = t * LEN
        y = math.sin(t * math.pi) * lift + noise.noise(Vector((t * 3 + off, 0, 0))) * 0.0002
        z = math.sin(t * math.pi) * bow + noise.noise(Vector((t * 2.3 + off, 5, 0))) * 0.0004
        return Vector((x, y, z))

    def at(t):
        c = centre(t)
        d = (centre(min(1, t + 0.01)) - centre(max(0, t - 0.01))).normalized()
        fx = d.cross(Vector((0, 0, 1))).normalized()
        fy = d.cross(fx).normalized()
        return c, fx, fy

    def node_bulge(t):
        return max(0.0, 1 - min(abs(t - n) for n in nodes) / 0.012)

    def crush(t, a):
        k = max(0.0, 1 - abs(t - crush_at) / crush_w)
        return 1 - k * 0.42 * abs(math.cos(a))  # squashed flat across one axis

    def radius(t, a):
        r = RAD * (1 - 0.22 * t) * (1 + 0.07 * node_bulge(t)) * crush(t, a)
        r *= 1 + 0.045 * math.cos(a * flutes + t * 1.3)  # the culm's fine longitudinal ridges
        return r * (1 + 0.04 * noise.noise(Vector((t * 40 + off, a * 3, 1))))

    def fray(t, a, r, c):
        if t < tip_split:
            return r, c
        k = (t - tip_split) / (1 - tip_split)
        s = (a / (2 * math.pi)) * strips
        gap = abs(s - round(s)) < 0.12  # the splits between the strips open up
        spread = k * k * (0.00025 + 0.0002 * math.sin(round(s) * 2.1 + seed))
        return r * (1 + k * 0.05) + spread - (0.0005 * k if gap else 0), c

    def colour(t, u):
        b = node_bulge(t)
        n = 0.92 + 0.08 * noise.noise(Vector((t * 9 + off, u * 4, 2)))
        base = (0.98 * n, 0.9 * n, 0.72 * n)
        tip = max(0.0, (t - 0.85) / 0.15) * 0.18  # weathered grey-brown toward the frayed end
        return (base[0] * (1 - 0.15 * b) - tip * 0.6, base[1] * (1 - 0.18 * b) - tip * 0.7, base[2] * (1 - 0.24 * b) - tip * 0.6, 1)

    rings, around = 120, 12
    tube(bm, col, uv, rings, around, at, radius, colour, fray=fray)
    tube(bm, col, uv, rings, around, at, lambda t, a: radius(t, a) * 0.72, colour, inner=True, fray=fray)
    # the base: cut on a slant, the wall's thickness showing as a ring
    c0, fx, fy = at(0)
    ring_o, ring_i = [], []
    for j in range(around):
        a = j / around * 2 * math.pi
        dirv = fx * math.cos(a) + fy * math.sin(a)
        slant = Vector((-0.0008 * math.cos(a), 0, 0))
        ring_o.append(bm.verts.new(c0 + dirv * radius(0, a) + slant))
        ring_i.append(bm.verts.new(c0 + dirv * radius(0, a) * 0.72 + slant))
    for j in range(around):
        f = bm.faces.new([ring_o[j], ring_i[j], ring_i[(j + 1) % around], ring_o[(j + 1) % around]])
        for l in f.loops:
            l[col] = (0.86, 0.76, 0.56, 1)
            l[uv].uv = (0.01, j / around * 0.2)
    # a papery leaf sheath wrapped three-quarters round the first node, its free edge torn
    s0, s1 = nodes[0], nodes[0] + rnd.uniform(0.18, 0.26)
    sheath_arc = rnd.uniform(0.7, 0.82)
    rot = rnd.uniform(0, 2 * math.pi)

    def sheath_at(t):
        return at(s0 + (s1 - s0) * t)

    def sheath_r(t, a):
        torn = 0.00012 * max(0.0, t - 0.6) * 4 * (0.5 + 0.5 * noise.noise(Vector((a * 6 + off, t * 9, 3))))
        return radius(s0 + (s1 - s0) * t, a) * 1.08 + 0.00006 + torn

    def sheath_col(t, u):
        n = 0.9 + 0.1 * noise.noise(Vector((t * 12 + off, u * 6, 4)))
        return (1.0 * n, 0.95 * n, 0.8 * n, 1)

    def sheath_frame(t):
        c, fx, fy = sheath_at(t)
        ca, sa = math.cos(rot), math.sin(rot)
        return c, fx * ca + fy * sa, fy * ca - fx * sa

    tube(bm, col, uv, 24, 14, sheath_frame, sheath_r, sheath_col, arc=sheath_arc)
    tube(bm, col, uv, 24, 14, sheath_frame, lambda t, a: sheath_r(t, a) - 0.00006, sheath_col, inner=True, arc=sheath_arc)
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-7)
    bm.normal_update()
    me = bpy.data.meshes.new(f"straw_{seed}")
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = True
    ob = bpy.data.objects.new(f"straw_{seed}", me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


reset()
for s in range(6):
    export(f"straw_{s}", [straw(700 + s)])
print("[blender] done")
