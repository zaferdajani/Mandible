"""Models every piece of silk in the game in Blender, headless, and exports them as one glTF (targets S1-S3).

Run: blender --background --factory-startup --python tools/blender/silk_models.py -- assets/silk
Writes <out>/silk.gltf with these objects (game units: a worker ant is 1 unit long; Blender z is up):
  strand_a..c    one taut thread from the origin to (0, 1, 0), beaded with glue; stretched between anchors in the game
  tent_a..c      a snare (S1): a low tent of ropes and sheets, footprint radius 1, standing off the ground
  hammock_a..b   a trap web (S3): a sagging sheet slung between grass stems, trip lines fanning to the ground
  wrap           the bands lashed over a caught insect's body (an ant 1 unit long, heading along y)
  shot           a flung gob of silk with streamers trailing behind it (flying toward -y)
  spinnery       the silk ants' outpost (S2): sheets, funnel doorways and guy-lines strung between five stems
Silk objects end in _silk, grass in _stem. Vertex colour rgb is the tint; alpha says what the surface is:
1 rope (opaque), 0.5 sheet (the shader cuts fibres and holes into it along uv.x), 0 a bead of glue (glossy).
Everything is seeded, so a run always gives the same meshes.
"""
import json
import math
import os
import random
import struct
import sys

import bmesh
import bpy
from mathutils import Vector, noise

OUT = sys.argv[sys.argv.index("--") + 1]
ROPE, SHEET, BEAD = 1.0, 0.5, 0.0
CREAM = (0.86, 0.78, 0.6)
THIN = 0.5  # every rope is this much of its nominal radius: silk, not string


class Piece:
    """One mesh being built: geometry plus per-vertex colour (rgba) and uv."""

    def __init__(self, seed):
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.r = random.Random(seed)
        self.cols = {}
        self.uvs = {}

    def v(self, p, rgba, uv=(0.0, 0.0)):
        vert = self.bm.verts.new(p)
        self.cols[vert] = rgba
        self.uvs[vert] = uv
        return vert

    def face(self, vs):
        try:
            f = self.bm.faces.new(vs)
        except ValueError:
            return
        for loop in f.loops:
            loop[self.uv].uv = self.uvs[loop.vert]

    def tint(self, kind, shade=1.0):
        j = 0.94 + 0.06 * self.r.random()
        return (CREAM[0] * j * shade, CREAM[1] * j * shade, CREAM[2] * j * shade * (0.92 + 0.08 * self.r.random()), kind)

    def tube(self, pts, radii, sides=5, rgba=None, kind=ROPE):
        rings, prev_n = [], None
        base = rgba or self.tint(kind)
        for i, p in enumerate(pts):
            p = Vector(p)
            t = (Vector(pts[min(i + 1, len(pts) - 1)]) - Vector(pts[max(i - 1, 0)])).normalized()
            n = prev_n if prev_n is not None else t.orthogonal().normalized()
            n = (n - t * n.dot(t)).normalized()
            prev_n = n
            b = t.cross(n)
            ring = []
            for k in range(sides):
                a = 2 * math.pi * k / sides + 0.4 * i
                c = base if not callable(base) else base(p)
                ring.append(self.v(p + (n * math.cos(a) + b * math.sin(a)) * radii[i], c))
            rings.append(ring)
        for i in range(len(rings) - 1):
            for k in range(sides):
                self.face((rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]))
        self.face(list(reversed(rings[0])))
        self.face(rings[-1])

    def bead(self, p, rad):
        geom = bmesh.ops.create_icosphere(self.bm, subdivisions=1, radius=rad) if rad > 0.02 else bmesh.ops.create_cone(self.bm, segments=4, radius1=rad, radius2=0.0, depth=rad * 2.2, cap_ends=True)
        c = (1.0, 0.95, 0.8, BEAD)
        for vert in geom["verts"]:
            vert.co = Vector((vert.co.x, vert.co.y, vert.co.z * 1.25)) + Vector(p) - Vector((0, 0, rad * 0.6))
            self.cols[vert] = c
            self.uvs[vert] = (0.0, 0.0)

    def rope(self, a, b, sag=0.05, rad=0.022, n=8, beads=0.3, sides=5):
        """A thread from a to b that hangs a little, thicker where it is tied, with glue beads along it."""
        a, b = Vector(a), Vector(b)
        length = (b - a).length
        wob = Vector((self.r.uniform(-1, 1), self.r.uniform(-1, 1), 0)) * 0.02 * length
        pts = [a.lerp(b, t) + Vector((0, 0, -sag * length * 4 * t * (1 - t))) + wob * math.sin(t * math.pi) for t in (i / (n - 1) for i in range(n))]
        rad *= THIN
        radii = [rad * (1.0 + 0.9 * (abs(2 * i / (n - 1) - 1) ** 3)) * self.r.uniform(0.85, 1.15) for i in range(n)]
        self.tube(pts, radii, sides)
        if beads > 0:
            count = int(length / beads)
            for k in range(count):
                t = (k + self.r.uniform(0.2, 0.8)) / max(count, 1)
                i = min(int(t * (n - 1)), n - 2)
                f = t * (n - 1) - i
                self.bead(pts[i].lerp(pts[i + 1], f), rad * self.r.uniform(1.5, 2.3))
        return pts

    def sheet(self, fn, nu=7, nv=7, shade=1.0, kind=SHEET):
        """A membrane of fibres: fn(u, v) -> point, u along the fibres."""
        grid = []
        seed = self.r.uniform(0, 50)
        for i in range(nu):
            row = []
            for j in range(nv):
                u, v = i / (nu - 1), j / (nv - 1)
                p = Vector(fn(u, v))
                p += Vector((0, 0, 1)) * 0.03 * noise.noise(p * 3.1 + Vector((seed, 0, 0)))
                row.append(self.v(p, self.tint(kind, shade), (u * 3.0 + seed, v + seed * 0.37)))
            grid.append(row)
        for i in range(nu - 1):
            for j in range(nv - 1):
                self.face((grid[i][j], grid[i + 1][j], grid[i + 1][j + 1], grid[i][j + 1]))

    def lash(self, p, rad, turns=3):
        """Silk wound round a stem where a rope is tied."""
        pts = [Vector(p) + Vector((math.cos(a) * rad, math.sin(a) * rad, (a / (2 * math.pi * turns) - 0.5) * rad * 3.2))
               for a in (k * 2 * math.pi * turns / (turns * 7) for k in range(turns * 7 + 1))]
        self.tube(pts, [rad * 0.42] * len(pts), 4)

    def culm(self, base, top, rad, blades=2):
        """A grass stem: kinked at its joints, green at the foot and straw at the tip, with a blade or two."""
        base, top = Vector(base), Vector(top)
        n = 9
        pts = []
        for i in range(n):
            t = i / (n - 1)
            k = Vector((math.sin(i * 2.1 + base.x * 3), math.cos(i * 1.7 + base.y * 3), 0)) * 0.035 * (top - base).length * (0 if i in (0,) else 1)
            pts.append(base.lerp(top, t) + k * math.sin(t * math.pi))
        z0, z1 = base.z, top.z

        def col(p):
            t = (p.z - z0) / max(z1 - z0, 0.01)
            g, s = (0.2, 0.3, 0.07), (0.56, 0.46, 0.2)
            j = 0.9 + 0.1 * noise.noise(p * 4.0)
            return tuple((g[c] + (s[c] - g[c]) * t) * j for c in range(3)) + (1.0,)
        self.tube(pts, [rad * (1.15 - 0.6 * i / (n - 1)) for i in range(n)], 6, rgba=col)
        for b in range(blades):
            i0 = 1 + b * 2 + self.r.randrange(2)
            a = self.r.uniform(0, 6.28)
            out = Vector((math.cos(a), math.sin(a), 0))
            side = Vector((-out.y, out.x, 0))
            length = (top - base).length * self.r.uniform(0.35, 0.55)
            prev = None
            for s in range(7):
                t = s / 6
                c = pts[i0] + out * length * (0.55 * t + 0.25 * t * t) + Vector((0, 0, length * (0.75 * t - 0.75 * t * t * t)))
                w = rad * 3.4 * math.sin(min(1.0, t * 3.0 + 0.25) * math.pi / 2) * (1 - t) ** 0.8 + 0.002
                l, r_ = self.v(c - side * w, col(c)), self.v(c + side * w, col(c))
                if prev:
                    self.face((prev[0], prev[1], r_, l))
                prev = (l, r_)
        return pts

    def finish(self, name):
        me = bpy.data.meshes.new(name)
        self.bm.verts.ensure_lookup_table()
        order = list(self.bm.verts)
        cols = [self.cols.get(vert, (1, 1, 1, 1)) for vert in order]
        bmesh.ops.recalc_face_normals(self.bm, faces=self.bm.faces)
        self.bm.to_mesh(me)
        self.bm.free()
        for p in me.polygons:
            p.use_smooth = True
        attr = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
        for i, c in enumerate(cols):
            attr.data[i].color = c
        me.color_attributes.active_color = attr
        ob = bpy.data.objects.new(name, me)
        bpy.context.scene.collection.objects.link(ob)
        tris = sum(len(p.vertices) - 2 for p in me.polygons)
        print(f"[blender] {name}: {tris} triangles")
        return ob


def polar(r, a, z=0.0):
    return Vector((r * math.cos(a), r * math.sin(a), z))


def strand(k):
    p = Piece(100 + k)
    p.rope((0, 0, 0), (0, 1, 0), sag=0.015 + 0.02 * k, rad=0.02, n=7, beads=0.22, sides=4)
    p.finish(f"strand_{'abc'[k]}_silk")


def tent(k):
    """S1: a low tangle thrown over a creature: many fine threads from feet on the soil and from two grass blades,
    crossing over knots above its back, with wisps of sheet between some of them."""
    p, stems = Piece(200 + k), Piece(250 + k)
    r = p.r
    nf = 8 + k
    feet = [polar(r.uniform(0.75, 1.15), i * 2 * math.pi / nf + r.uniform(-0.3, 0.3)) for i in range(nf)]
    # two of the anchors are grass blades: the web stands up where it is tied to them
    for i in (0, nf // 2 + k % 2):
        base = feet[i] * 1.05
        pts = stems.culm(base, base + Vector((r.uniform(-0.25, 0.25), r.uniform(-0.25, 0.25), r.uniform(1.0, 1.45))), 0.03, blades=2)
        feet[i] = pts[3 + r.randrange(2)].copy()
        p.lash(feet[i], 0.04, turns=2)
    hubs = [Vector((r.uniform(-0.3, 0.3), r.uniform(-0.3, 0.3), r.uniform(0.3, 0.46))) for _ in range(4)]
    near = [min(hubs, key=lambda h: (h - Vector((f.x, f.y, h.z))).length) for f in feet]
    for f, h in zip(feet, near):
        p.rope(f, h, sag=r.uniform(0.0, 0.06), rad=0.022, beads=0.22)
        p.rope(f + (h - f) * 0.3, r.choice(hubs), sag=r.uniform(0.0, 0.05), rad=0.012, n=6, beads=0.3, sides=3)
        if f.z < 0.05:
            for s in range(3):
                p.rope(f + (h - f) * 0.1, f + polar(r.uniform(0.08, 0.2), r.uniform(0, 6.28)), sag=0.0, rad=0.012, n=3, beads=0, sides=3)
    for i in range(4):
        for j in range(i + 1, 4):
            p.rope(hubs[i], hubs[j], sag=0.05, rad=0.016, n=5, beads=0.18)
    # fine threads thrown right across, side to side, over the top and low down
    for i in range(nf * 2):
        a, b = feet[i % nf], feet[(i % nf + nf // 2 + r.randrange(-1, 2)) % nf]
        mid = (a + b) * 0.5 + Vector((r.uniform(-0.2, 0.2), r.uniform(-0.2, 0.2), 0))
        mid.z = r.uniform(0.18, 0.44)
        p.rope(a, mid, sag=-0.05, rad=0.011, n=6, beads=0.3, sides=3)
        p.rope(mid, b, sag=-0.05, rad=0.011, n=6, beads=0.3, sides=3)
    # wisps of sheet in some sectors, drooping between their ropes
    for i in range(nf):
        if r.random() < 0.4:
            continue
        a, b, h = feet[i], feet[(i + 1) % nf], near[i]
        lo, hi = r.uniform(0.05, 0.3), r.uniform(0.6, 0.95)

        def fn(u, v, a=a, b=b, h=h, lo=lo, hi=hi):
            base = a.lerp(b, v)
            q = h.lerp(base, lo + (hi - lo) * u)
            return q + Vector((0, 0, -0.07 * math.sin(v * math.pi) * math.sin(u * math.pi)))
        p.sheet(fn, 5, 5)
    p.finish(f"tent_{'abc'[k]}_silk")
    stems.finish(f"tent_{'abc'[k]}_stem")


def hammock(k):
    """S3: a sheet slung low between stems on both sides of a path (the path runs along y), trip lines to the soil."""
    silk, stems = Piece(300 + k), Piece(350 + k)
    r = silk.r
    posts = []
    for sx in (-1, 1):
        for sy in ((-1, 1) if k == 0 else (-1, 0.2, 1)):
            if sx == 1 and sy == 0.2:
                continue
            base = Vector((sx * r.uniform(0.98, 1.12), sy * r.uniform(0.3, 0.55), 0))
            top = base + Vector((sx * r.uniform(0.0, 0.25), r.uniform(-0.2, 0.2), r.uniform(1.35, 1.85)))
            pts = stems.culm(base, top, 0.034)
            tie = pts[3] if r.random() < 0.5 else pts[4]
            silk.lash(tie, 0.05)
            posts.append((tie, sx, sy))
    left = sorted([q for q in posts if q[1] < 0], key=lambda q: q[2])
    right = sorted([q for q in posts if q[1] > 0], key=lambda q: q[2])
    la, lb, ra, rb = left[0][0], left[-1][0], right[0][0], right[-1][0]

    def fn(u, v):
        # u across the path (left to right), v along it
        l, rr = la.lerp(lb, v), ra.lerp(rb, v)
        q = l.lerp(rr, u)
        belly = math.sin(u * math.pi) * (0.75 + 0.25 * math.sin(v * math.pi))
        q.z = q.z * (1 - belly) + 0.2 * belly
        q.y += (v - 0.5) * 0.5 * math.sin(u * math.pi)
        return q
    silk.sheet(fn, 12, 8)
    # the edge ropes and the cross ties that carry the sheet
    for a, b in ((la, ra), (lb, rb)):
        silk.rope(a, b, sag=0.22, rad=0.026, n=10, beads=0.22)
    silk.rope(la, lb, sag=0.03, rad=0.02, n=5, beads=0.25)
    silk.rope(ra, rb, sag=0.03, rad=0.02, n=5, beads=0.25)
    silk.rope(la, rb, sag=0.2, rad=0.016, n=9, beads=0.3)
    silk.rope(lb, ra, sag=0.2, rad=0.016, n=9, beads=0.3)
    if len(left) > 2:
        silk.rope(left[1][0], fn(0.45, 0.6), sag=0.05, rad=0.018, n=6, beads=0.3)
    # a looser layer of fine threads over and under the sheet
    for i in range(14):
        a, b = Vector(fn(r.uniform(0.0, 0.3), r.random())), Vector(fn(r.uniform(0.7, 1.0), r.random()))
        silk.rope(a + Vector((0, 0, r.uniform(-0.03, 0.08))), b + Vector((0, 0, r.uniform(-0.03, 0.08))), sag=r.uniform(0.05, 0.2), rad=0.01, n=8, beads=0.25, sides=3)
    # trip lines from the sheet down to the soil, up and down the path
    for i in range(14):
        u, side = r.uniform(0.12, 0.88), (i % 2) * 2 - 1
        start = Vector(fn(u, 0.5 + side * 0.5))
        end = Vector((start.x + r.uniform(-0.25, 0.25), side * r.uniform(1.0, 1.45), 0))
        silk.rope(start, end, sag=0.02, rad=0.013, n=6, beads=0.28)
    # guys from the stems out to the soil
    for tie, sx, sy in posts:
        silk.rope(tie, Vector((tie.x + sx * r.uniform(0.3, 0.5), tie.y + r.uniform(-0.4, 0.4), 0)), sag=0.01, rad=0.015, n=5, beads=0.4)
    silk.finish(f"hammock_{'ab'[k]}_silk")
    stems.finish(f"hammock_{'ab'[k]}_stem")


def wrap():
    """Bands lashed over a caught insect: fine loops round a body 1 long (along y), tilted every which way."""
    p = Piece(400)
    r = p.r
    for i in range(9):
        y = -0.42 + 0.84 * i / 8 + r.uniform(-0.04, 0.04)
        girth = 0.17 * math.sqrt(max(0.15, 1 - (y / 0.56) ** 2)) + 0.03
        tilt, roll = r.uniform(-0.7, 0.7), r.uniform(-0.4, 0.4)
        pts = []
        for s in range(13):
            a = s * 2 * math.pi / 12
            x, z = math.cos(a) * girth * 1.1, math.sin(a) * girth * 0.95
            pts.append(Vector((x, y + x * tilt + z * roll, 0.2 + z)))
        p.tube(pts, [r.uniform(0.008, 0.016) for _ in pts], 3)
        if i % 3 == 0:
            p.bead(pts[3], 0.022)
    for sx in (-1, 1):
        p.rope((sx * 0.2, -0.45, 0.12), (-sx * 0.2, 0.45, 0.3), sag=-0.25, rad=0.02, n=7, beads=0.3, sides=3)
    p.finish("wrap_silk")


def shot():
    p = Piece(500)
    r = p.r
    for i in range(6):
        ax = Vector((r.uniform(-1, 1), r.uniform(-1, 1), r.uniform(-1, 1))).normalized()
        u = ax.orthogonal().normalized()
        w = ax.cross(u)
        rad = r.uniform(0.2, 0.3)
        pts = [u * math.cos(a) * rad + w * math.sin(a) * rad * r.uniform(0.8, 1.1) for a in (s * 2 * math.pi / 10 for s in range(11))]
        p.tube(pts, [0.035] * len(pts), 4)
    for i in range(4):
        end = Vector((r.uniform(-0.25, 0.25), r.uniform(0.7, 1.2), r.uniform(-0.2, 0.2)))
        p.rope((0, 0.15, 0), end, sag=0.06, rad=0.02, n=6, beads=0.3, sides=4)
    p.finish("shot_silk")


def spinnery():
    """S2: five grass stems drawn together at the top like a sheaf, a silk pavilion woven between them from just
    above the soil to over the ants' heads, narrowing upward, two funnel doorways, guy-lines to the soil.
    In game units as it stands (about 4 across at the foot, the stems 6 tall)."""
    silk, stems = Piece(600), Piece(650)
    r = silk.r
    n = 5
    cul = []
    for i in range(n):
        a = i * 2 * math.pi / n + r.uniform(-0.25, 0.25)
        base = polar(r.uniform(1.6, 2.1), a)
        top = polar(r.uniform(0.5, 1.0), a + r.uniform(-0.5, 0.5), r.uniform(5.0, 6.6))
        cul.append(stems.culm(base, top, 0.055, blades=3))
    for i in range(4):  # shorter grass round its foot
        a = r.uniform(0, 6.28)
        base = polar(r.uniform(2.2, 2.8), a)
        stems.culm(base, base + polar(r.uniform(0.2, 0.6), a + 1.0, r.uniform(1.4, 2.4)), 0.035, blades=2)

    def at(i, z):
        pts = cul[i % n]
        for j in range(len(pts) - 1):
            if pts[j + 1].z >= z:
                f = (z - pts[j].z) / max(pts[j + 1].z - pts[j].z, 1e-4)
                return pts[j].lerp(pts[j + 1], f)
        return pts[-1]
    lo, hi = 0.45, 3.5
    centre_top = Vector((r.uniform(-0.2, 0.2), r.uniform(-0.2, 0.2), hi + 0.9))
    doors = (0, 2)
    perches = [centre_top + Vector((0, 0, 0.02))]
    for i in range(n):
        zl, zh = lo + r.uniform(-0.15, 0.25), hi + r.uniform(-0.4, 0.3)
        a0, a1, b0, b1 = at(i, zl), at(i, zh), at(i + 1, zl), at(i + 1, zh)
        mid_out = (a0 + b0) * 0.5
        mid_out.z = 0
        mid_out.normalize()
        bulge = r.uniform(0.45, 0.8)

        def wall(u, v, a0=a0, a1=a1, b0=b0, b1=b1, mid_out=mid_out, bulge=bulge):
            # u up the wall, v from one stem to the next; it bellies between the stems, each wall its own way
            q = a0.lerp(a1, u).lerp(b0.lerp(b1, u), v)
            return q + mid_out * bulge * math.sin(v * math.pi) ** 0.7 * (0.35 + 0.65 * math.sin(u * math.pi) ** 0.6)
        silk.sheet(wall, 10, 8, kind=0.68)
        for z in (zl, zh, (zl + zh) / 2 + r.uniform(-0.4, 0.4)):
            silk.lash(at(i, z), 0.08)
        silk.rope(a0, b0, sag=0.1, rad=0.04, n=7, beads=0.4)
        silk.rope(a1, b1, sag=0.12, rad=0.04, n=7, beads=0.4)
        for t in range(7):  # fine threads laid over the wall every which way
            p0, p1 = Vector(wall(r.random(), 0.0)), Vector(wall(r.random(), 1.0))
            silk.rope(p0, p1, sag=r.uniform(0.02, 0.12), rad=0.018, n=7, beads=0.45, sides=3)

        def roof(u, v, a1=a1, b1=b1):
            edge = a1.lerp(b1, v)
            q = edge.lerp(centre_top, u * 0.97)
            return q + Vector((0, 0, -0.3 * math.sin(u * math.pi)))
        silk.sheet(roof, 6, 6, kind=0.68)
        silk.rope(a1, centre_top, sag=0.12, rad=0.03, n=6, beads=0.5)

        def floor(u, v, a0=a0, b0=b0):
            return a0.lerp(b0, v).lerp(Vector((0, 0, lo + 0.25)), u * 0.96)
        silk.sheet(floor, 4, 5, shade=0.85, kind=0.68)
        if i in doors:
            # a funnel doorway woven into this wall: a horn of silk opening outward, dark inside
            c = Vector(wall(0.42, 0.5))
            rings = []
            for s in range(7):
                t = s / 6
                rad = 0.32 + 0.5 * t * t
                rings.append((c + mid_out * (0.9 * t - 0.3) + Vector((0, 0, -0.1 * t)), rad, 0.08 + 0.92 * t * t))
            side = Vector((-mid_out.y, mid_out.x, 0))
            prev = None
            for cc, rad, shade in rings:
                ring = [silk.v(cc + (side * math.cos(a) + Vector((0, 0, 1)) * math.sin(a)) * rad * (1 + 0.12 * math.sin(a * 3 + rad * 9)), silk.tint(ROPE, shade))
                        for a in (k * 2 * math.pi / 12 for k in range(12))]
                if prev:
                    for k in range(12):
                        silk.face((prev[k], prev[(k + 1) % 12], ring[(k + 1) % 12], ring[k]))
                prev = ring
            perches.append(rings[-1][0] - Vector((0, 0, rings[-1][1] * 0.85)))
            for t in range(6):  # threads from the lip of the funnel out to the stems either side
                a = r.uniform(0, 6.28)
                lipp = rings[-1][0] + (side * math.cos(a) + Vector((0, 0, 1)) * math.sin(a)) * rings[-1][1]
                silk.rope(lipp, at(i + (t % 2), r.uniform(zl, zh)), sag=0.05, rad=0.02, n=6, beads=0.4, sides=3)
        # guy-lines from each stem down to the soil, and long ones from higher up
        tie = at(i, zh - 0.4)
        out = Vector((tie.x, tie.y, 0)).normalized()
        for g in range(2):
            end = tie + (out * r.uniform(1.6, 2.8)) + Vector((r.uniform(-0.8, 0.8), r.uniform(-0.8, 0.8), 0))
            end.z = 0
            silk.rope(tie, end, sag=0.02, rad=0.024, n=8, beads=0.4)
        tie2 = at(i, zh + r.uniform(0.6, 1.4))
        silk.lash(tie2, 0.07)
        silk.rope(tie2, at(i + 1, zh + r.uniform(0.3, 1.2)), sag=0.14, rad=0.02, n=7, beads=0.4)
        silk.rope(tie2, at(i + 2, zh + r.uniform(0.3, 1.2)), sag=0.1, rad=0.016, n=7, beads=0.4, sides=3)
        if i % 2 == 0:
            end = polar(r.uniform(3.2, 4.2), math.atan2(out.y, out.x) + r.uniform(-0.5, 0.5))
            silk.rope(tie2, end, sag=0.02, rad=0.02, n=9, beads=0.45)
    perches.append(Vector((cul[1][0].x * 1.25, cul[1][0].y * 1.25, 0)))
    for i, q in enumerate(perches):
        e = bpy.data.objects.new(f"perch_{i}", None)
        e.location = q
        bpy.context.scene.collection.objects.link(e)
    silk.finish("spinnery_silk")
    stems.finish("spinnery_stem")


def float_colours(path):
    """Blender writes vertex colours as normalized integers, which Godot 4.7 reads unscaled: rewrite COLOR_0 as floats."""
    g = json.load(open(path))
    binp = os.path.join(os.path.dirname(path), g["buffers"][0]["uri"])
    data = bytearray(open(binp, "rb").read())
    done = set()
    for mesh in g["meshes"]:
        for prim in mesh["primitives"]:
            ai = prim["attributes"].get("COLOR_0")
            if ai is None or ai in done:
                continue
            acc = g["accessors"][ai]
            if acc["componentType"] == 5126:
                continue
            view = g["bufferViews"][acc["bufferView"]]
            start = view.get("byteOffset", 0) + acc.get("byteOffset", 0)
            count, comps = acc["count"], 4 if acc["type"] == "VEC4" else 3
            scale = 65535.0 if acc["componentType"] == 5123 else 255.0
            fmt = "H" if acc["componentType"] == 5123 else "B"
            size = struct.calcsize(fmt)
            stride = view.get("byteStride", size * comps)
            vals = [struct.unpack_from("<" + fmt, data, start + i * stride + c * size)[0] / scale for i in range(count) for c in range(comps)]
            while len(data) % 4:
                data.append(0)
            off = len(data)
            data += struct.pack("<%df" % len(vals), *vals)
            g["bufferViews"].append({"buffer": 0, "byteOffset": off, "byteLength": len(vals) * 4})
            acc["bufferView"] = len(g["bufferViews"]) - 1
            acc["byteOffset"] = 0
            acc["componentType"] = 5126
            acc.pop("normalized", None)
            done.add(ai)
    g["buffers"][0]["byteLength"] = len(data)
    open(binp, "wb").write(data)
    json.dump(g, open(path, "w"))


bpy.ops.wm.read_factory_settings(use_empty=True)
for i in range(3):
    strand(i)
    tent(i)
for i in range(2):
    hammock(i)
wrap()
shot()
spinnery()
objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
for o in bpy.context.scene.objects:
    o.select_set(True)
bpy.context.view_layer.objects.active = objs[0]
os.makedirs(OUT, exist_ok=True)
target = os.path.join(OUT, "silk.gltf")
bpy.ops.export_scene.gltf(filepath=target, export_format="GLTF_SEPARATE", use_selection=True, export_apply=True,
                          export_normals=True, export_texcoords=True, export_materials="NONE", export_vertex_color="ACTIVE")
float_colours(target)
print(f"[blender] wrote {target}: {len(objs)} objects")
