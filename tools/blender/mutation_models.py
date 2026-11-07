"""Models the mutation track's props in Blender, headless, and exports each as glTF (targets H1-H3).

Run: blender --background --factory-startup --python tools/blender/mutation_models.py -- <out-dir>
Writes <out-dir>/<name>/<name>.gltf: the queen wasp's glowing gland, the dead queen wasp, the orb web with its grass
stems and bramble twig (and the silk that wraps her), the nursery's bed of chewed fibre and the living gauge: a heap
of remains and seeds whose pieces are named heap_000.. in the order the nurses pile them. Everything is in game
units (1 unit = 5 mm, a worker ant is 1 unit long) and seeded, so a run always gives the same meshes. Colours are
vertex colours; Godot gives each part its material by object name (src/Render/MutagenProps.cs).
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

OUT = sys.argv[sys.argv.index("--") + 1]
R = random.Random(7)


def reset():
    bpy.ops.wm.read_factory_settings(use_empty=True)


def export(name):
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    for o in objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    d = os.path.join(OUT, name)
    os.makedirs(d, exist_ok=True)
    bpy.ops.export_scene.gltf(filepath=os.path.join(d, name + ".gltf"), export_format="GLTF_SEPARATE",
                              use_selection=True, export_apply=True, export_normals=True, export_texcoords=True,
                              export_materials="NONE", export_vertex_color="ACTIVE")
    float_colours(os.path.join(d, name + ".gltf"))
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs)
    print(f"[blender] wrote {name}: {len(objs)} objects, {tris} triangles")


def float_colours(path):
    """Blender writes vertex colours as normalized 16-bit integers, which Godot 4.7's importer reads unscaled (thousands
    of times too bright). Rewrite every COLOR_0 as plain floats, appended to the buffer."""
    import json
    import struct
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
            n, comps = acc["count"], 4 if acc["type"] == "VEC4" else 3
            scale = 65535.0 if acc["componentType"] == 5123 else 255.0
            fmt = "H" if acc["componentType"] == 5123 else "B"
            size = struct.calcsize(fmt)
            stride = view.get("byteStride", size * comps)
            vals = []
            for i in range(n):
                for c in range(comps):
                    vals.append(struct.unpack_from("<" + fmt, data, start + i * stride + c * size)[0] / scale)
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


def obj(name, bm, smooth=True):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    for p in me.polygons:
        p.use_smooth = smooth
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def paint(ob, fn):
    """Vertex colours from fn(position, normal) -> (r, g, b)."""
    me = ob.data
    attr = me.color_attributes.new("Col", "FLOAT_COLOR", "POINT")
    for v in me.vertices:
        c = fn(v.co, v.normal)
        attr.data[v.index].color = (c[0], c[1], c[2], 1.0)
    me.color_attributes.active_color = attr


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(x + (y - x) * t for x, y in zip(a, b))


def blob(bm, centre, radii, subdiv=3, rough=0.06, seed=0.0, rot=Matrix.Identity(3)):
    """A noise-displaced ellipsoid: no two are alike, none is a clean primitive."""
    geom = bmesh.ops.create_icosphere(bm, subdivisions=subdiv, radius=1.0)
    verts = [e for e in geom["verts"]]
    for v in verts:
        p = v.co.copy()
        n = noise.noise(p * 2.3 + Vector((seed, seed * 1.7, seed * 0.3)))
        p = Vector((p.x * radii[0], p.y * radii[1], p.z * radii[2])) * (1 + rough * n)
        v.co = rot @ p + Vector(centre)
    return verts


def tube(bm, pts, radii, sides=6, cap=True, twist=0.0, ridges=0, ridge_depth=0.0):
    """A swept tube along a polyline (legs, antennae, threads, fibres, stems); `ridges` flutes it lengthwise."""
    rings = []
    prev_n = None
    for i, p in enumerate(pts):
        p = Vector(p)
        t = (Vector(pts[min(i + 1, len(pts) - 1)]) - Vector(pts[max(i - 1, 0)])).normalized()
        n = prev_n if prev_n is not None else (t.orthogonal().normalized())
        n = (n - t * n.dot(t)).normalized()
        prev_n = n
        b = t.cross(n)
        ring = []
        for k in range(sides):
            a = 2 * math.pi * k / sides + twist * i
            rr = radii[i] * (1 + ridge_depth * math.cos(ridges * a)) if ridges else radii[i]
            ring.append(bm.verts.new(p + (n * math.cos(a) + b * math.sin(a)) * rr))
        rings.append(ring)
    for i in range(len(rings) - 1):
        for k in range(sides):
            bm.faces.new((rings[i][k], rings[i][(k + 1) % sides], rings[i + 1][(k + 1) % sides], rings[i + 1][k]))
    if cap:
        bm.faces.new(list(reversed(rings[0])))
        bm.faces.new(rings[-1])


def bezier(a, b, c, n):
    a, b, c = Vector(a), Vector(b), Vector(c)
    return [a * (1 - t) ** 2 + b * 2 * t * (1 - t) + c * t * t for t in (i / (n - 1) for i in range(n))]


# ---------------------------------------------------------------------------------------------- the gland (H2)
def gland():
    reset()
    bm = bmesh.new()
    verts = blob(bm, (0, 0, 0), (0.42, 0.29, 0.26), subdiv=5, rough=0.09, seed=3.1)
    # a pear: the stalk end narrows, the far end sags under its own weight
    for v in verts:
        x = v.co.x
        if x < 0:
            v.co.y *= 1 + 0.35 * x
            v.co.z *= 1 + 0.35 * x
        v.co.z -= 0.05 * max(0.0, x) ** 2 * 6
    # the torn stalk it was cut from, with a ragged end
    pts = [(-0.4, 0, 0.02), (-0.52, 0.02, 0.05), (-0.6, 0.05, 0.03)]
    tube(bm, pts, [0.08, 0.06, 0.05], sides=7)
    ob = obj("gland", bm)

    def col(p, n):
        # amber flesh, veins as dark branching ridges, a paler skin where it bulges
        vein = abs(noise.noise(p * 7 + Vector((1.3, 0, 0))))
        vein2 = abs(noise.noise(p * 14 + Vector((0, 4.2, 0))))
        base = mix((0.86, 0.55, 0.16), (0.98, 0.8, 0.44), 0.5 + 0.5 * n.z)
        c = mix(base, (0.36, 0.1, 0.06), max(0.0, 1 - min(vein, vein2 * 1.3) * 6))
        if p.x < -0.42:
            c = mix((0.55, 0.22, 0.12), (0.8, 0.45, 0.3), noise.noise(p * 30) * 0.5 + 0.5)
        return c
    paint(ob, col)
    # the light inside shows through the veins: branching filaments laid on the skin, which Godot lights from within
    from mathutils.bvhtree import BVHTree
    tree = BVHTree.FromObject(ob, bpy.context.evaluated_depsgraph_get())
    core = bmesh.new()
    rnd = random.Random(21)
    def on_skin(d):
        hit = tree.ray_cast(Vector((0, 0, 0)), d.normalized())
        return hit[0] + hit[1] * 0.004 if hit[0] is not None else None
    for k in range(9):
        d = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.6, 1))).normalized()
        step = Vector((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))).normalized() * 0.16
        pts = []
        for j in range(9):
            q = on_skin(d)
            if q is None:
                break
            pts.append(q)
            d = (d + step + Vector((rnd.uniform(-0.12, 0.12), rnd.uniform(-0.12, 0.12), rnd.uniform(-0.12, 0.12)))).normalized()
            if j == 4 and len(pts) > 2:  # a branch
                b = d.copy(); bstep = step.cross(d).normalized() * 0.16; bp = []
                for jj in range(4):
                    qb = on_skin(b)
                    if qb is None:
                        break
                    bp.append(qb); b = (b + bstep).normalized()
                if len(bp) > 1:
                    tube(core, bp, [0.006 * (1 - 0.5 * t / len(bp)) for t in range(len(bp))], sides=4, cap=False)
        if len(pts) > 1:
            tube(core, pts, [0.009 * (1 - 0.6 * t / len(pts)) for t in range(len(pts))], sides=4, cap=False)
    co = obj("gland_core", core)
    paint(co, lambda p, n: (0.75, 0.55, 1.0))
    export("gland")


# ---------------------------------------------------------------------------------------------- the queen wasp (H1)
STRIPE_Y = (0.86, 0.62, 0.07)
BLACK = (0.035, 0.03, 0.028)
AMBER = (0.62, 0.34, 0.08)


def wasp_parts(bm_body, bm_legs):
    """A dead paper-wasp queen, 22 mm (4.4 units) long, lying along +X with her head at +X, slightly curled."""
    # head: wider than long, with kidney eyes, clypeus and curved mandibles
    head = (1.95, 0, 0.42)
    blob(bm_body, head, (0.3, 0.42, 0.36), subdiv=3, rough=0.05, seed=1.2)
    for s in (-1, 1):
        blob(bm_body, (2.02, s * 0.34, 0.5), (0.2, 0.09, 0.26), subdiv=2, rough=0.04, seed=4 + s)  # compound eyes
        tube(bm_body, bezier((2.18, s * 0.12, 0.25), (2.38, s * 0.12, 0.16), (2.34, -s * 0.02, 0.1), 6), [0.06, 0.055, 0.045, 0.035, 0.025, 0.012], sides=5)
        # antennae: scape then a curled flagellum (dead: they droop)
        pts = bezier((2.12, s * 0.12, 0.62), (2.5, s * 0.28, 0.9), (2.9, s * 0.55, 0.6), 7) + bezier((2.9, s * 0.55, 0.6), (3.15, s * 0.7, 0.35), (3.05, s * 0.95, 0.15), 6)[1:]
        tube(bm_legs, pts, [0.026] * 3 + [0.02] * 9, sides=5)
    # thorax: a compact dome (mesoscutum), the scutellum and propodeum stepping down behind it
    blob(bm_body, (1.3, 0, 0.5), (0.42, 0.33, 0.34), subdiv=3, rough=0.04, seed=2.2)
    blob(bm_body, (0.95, 0, 0.6), (0.13, 0.2, 0.09), subdiv=2, rough=0.06, seed=2.9)
    blob(bm_body, (0.84, 0, 0.44), (0.14, 0.2, 0.17), subdiv=2, rough=0.04, seed=3.1)
    # the wasp waist: a short narrow petiole, then the spindle gaster lathed in one piece, each tergite's rear edge
    # overlapping the next (a sawtooth profile), curled down at the tip as a dead wasp's is
    n_seg, length, rings, sides = 6, 2.05, 66, 14
    prev = None
    first = None
    for i in range(rings + 1):
        u = i / rings
        seg = min(n_seg - 1, int(u * n_seg))
        local = u * n_seg - seg
        if u < 0.06:
            r = 0.07 + 0.05 * u / 0.06
        else:
            w = (u - 0.06) / 0.94
            r = 0.47 * math.sin(math.pi * min(1.0, 0.2 + w * 0.92)) ** 0.75
            r *= 1.0 - 0.07 * local + (0.03 if local < 0.08 else 0.0)
        x = 0.72 - u * length
        z = 0.42 - 0.38 * u ** 2.2
        tilt = -0.5 * u ** 2
        ring = []
        for k in range(sides):
            a = 2 * math.pi * k / sides
            y = math.cos(a) * r
            zz = math.sin(a) * r * 0.86
            ring.append(bm_body.verts.new((x - math.sin(tilt) * zz, y, z + math.cos(tilt) * zz)))
        if prev:
            for k in range(sides):
                bm_body.faces.new((prev[k], prev[(k + 1) % sides], ring[(k + 1) % sides], ring[k]))
        else:
            first = ring
        prev = ring
    bm_body.faces.new(list(reversed(first)))
    bm_body.faces.new(prev)
    # sting sheath at the tip
    tube(bm_body, bezier((-1.22, 0, 0.1), (-1.33, 0, 0.0), (-1.38, 0, -0.12), 5), [0.06, 0.045, 0.03, 0.02, 0.008], sides=5)
    # legs: femur, tibia, tarsus with joints, curled in under the body as a dead insect's do
    for i, x in enumerate((1.5, 1.2, 0.9)):
        for s in (-1, 1):
            j = R.uniform(-0.1, 0.1)
            hip = Vector((x, s * 0.28, 0.25))
            knee = hip + Vector((0.15 - 0.25 * i + j, s * (0.75 - 0.08 * i), 0.15))
            ankle = knee + Vector((0.3 - 0.35 * i, -s * (0.35 + 0.1 * i), -0.35))
            foot = ankle + Vector((0.25 - 0.3 * i, -s * 0.3, -0.05))
            tube(bm_legs, bezier(hip, (hip + knee) / 2 + Vector((0, 0, 0.12)), knee, 5), [0.05, 0.055, 0.05, 0.045, 0.04], sides=6)
            tube(bm_legs, bezier(knee, (knee + ankle) / 2 + Vector((0, s * 0.05, 0)), ankle, 5), [0.036, 0.034, 0.032, 0.03, 0.028], sides=6)
            tube(bm_legs, bezier(ankle, (ankle + foot) / 2 + Vector((0, 0, -0.08)), foot, 6), [0.024, 0.021, 0.02, 0.018, 0.016, 0.008], sides=5)


def wasp_colour(p, n):
    # the gaster's yellow bands with a black front edge and the paired black spots; the head's yellow face
    if p.x < 0.62:
        i = int((0.62 - p.x) / 0.32)
        local = ((0.62 - p.x) / 0.32) % 1.0
        c = BLACK if local < 0.42 else STRIPE_Y
        spot = Vector((p.x, abs(p.y) - 0.18, p.z - 0.35))
        if local >= 0.42 and spot.length < 0.09 and i in (1, 2, 3):
            c = BLACK
        if p.z < 0.08:
            c = mix(c, BLACK, 0.55)  # the underside is darker
        return mix(c, (0.3, 0.2, 0.05), 0.12 * (noise.noise(p * 14) + 0.5))
    if p.x > 1.7:
        face = p.x > 2.1 and p.z < 0.55
        return mix(BLACK, STRIPE_Y, 1.0 if face else 0.0)
    # thorax: black with yellow markings at the shoulders and scutellum
    mark = (abs(p.y) > 0.3 and p.z > 0.55) or (p.x < 0.85 and p.z > 0.68)
    return STRIPE_Y if mark else mix(BLACK, (0.12, 0.08, 0.04), 0.3 * (noise.noise(p * 11) + 0.5))


def wasp_wings():
    """Four wing membranes, crumpled; UV 0..1 along each wing for the vein texture."""
    bm = bmesh.new()
    uv = bm.loops.layers.uv.new("UV")
    for fore, (span, chord, x0) in enumerate(((2.3, 0.62, 1.25), (1.6, 0.45, 1.05))):
        for s in (-1, 1):
            cols, rows = 10, 4
            grid = []
            for i in range(cols + 1):
                row = []
                u = i / cols
                w = chord * math.sin(math.pi * min(1.0, 0.15 + u * 0.95)) ** 0.6
                for k in range(rows + 1):
                    v = k / rows
                    x = x0 - u * span * 0.92 + (v - 0.5) * 0.15
                    y = s * (0.3 + u * 0.55 + (v - 0.5) * w * 0.3)
                    z = 0.85 + (v - 0.5) * w + 0.12 * u + 0.06 * noise.noise(Vector((u * 4, v * 3, fore + s)))
                    row.append((bm.verts.new((x, y, z)), (u, v)))
                grid.append(row)
            for i in range(cols):
                for k in range(rows):
                    f = bm.faces.new((grid[i][k][0], grid[i + 1][k][0], grid[i + 1][k + 1][0], grid[i][k + 1][0]))
                    for loop, (vi, ki) in zip(f.loops, ((i, k), (i + 1, k), (i + 1, k + 1), (i, k + 1))):
                        loop[uv].uv = grid[vi][ki][1]
    return obj("wasp_wings", bm)


def write_wing_texture(path):
    """Veins on clear amber membrane: a stigma, the costal margin and branching cross-veins. Written as a PNG."""
    W, H = 512, 128
    img = bpy.data.images.new("wing", W, H, alpha=True)
    px = [0.0] * (W * H * 4)
    rnd = random.Random(11)
    def put(x, y, a, c=(0.25, 0.14, 0.04)):
        if 0 <= x < W and 0 <= y < H:
            i = (y * W + x) * 4
            if px[i + 3] < a:
                px[i:i + 4] = [c[0], c[1], c[2], a]
    for y in range(H):
        for x in range(W):
            i = (y * W + x) * 4
            px[i:i + 4] = [0.75, 0.55, 0.22, 0.18]
    def line(x0, y0, x1, y1, w=1, a=0.9):
        n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
        for t in range(n + 1):
            x = int(x0 + (x1 - x0) * t / n); y = int(y0 + (y1 - y0) * t / n)
            for dx in range(-w, w + 1):
                for dy in range(-w, w + 1):
                    put(x + dx, y + dy, a)
    line(0, 6, W - 1, 18, 2)            # costal margin
    line(0, 40, W * 0.8, 44, 1)
    line(0, 80, W * 0.7, 95, 1)
    for k in range(9):
        x = 40 + k * 46 + rnd.randint(-8, 8)
        line(x, 8, x + rnd.randint(-30, 30), H - 10, 1, 0.7)
    for y in range(12, 26):                # the stigma: a dark cell near the tip of the leading edge
        for x in range(300, 340):
            put(x, y, 0.95, (0.3, 0.15, 0.03))
    img.pixels = px
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()


def wasp():
    reset()
    body, legs = bmesh.new(), bmesh.new()
    wasp_parts(body, legs)
    ob = obj("wasp_body", body)
    paint(ob, wasp_colour)
    lg = obj("wasp_legs", legs)
    paint(lg, lambda p, n: mix(AMBER, BLACK, 0.35 + 0.3 * noise.noise(p * 8)))
    wasp_wings()
    export("queen_wasp")
    write_wing_texture(os.path.join(OUT, "queen_wasp", "wing_veins.png"))


# ---------------------------------------------------------------------------------------------- the orb web (H1)
def catenary(a, b, sag, n):
    a, b = Vector(a), Vector(b)
    return [a.lerp(b, t) + Vector((0, 0, -sag * 4 * t * (1 - t))) for t in (i / (n - 1) for i in range(n))]


def web():
    """An orb web strung in a vertical plane (x across, z up) between two dry grass stems and a bramble twig,
    its hub 9 units above the ground. Damaged where the wasp struck it; dew on the capture spiral."""
    reset()
    silk, dew, stems, leaves, wrap = bmesh.new(), bmesh.new(), bmesh.new(), bmesh.new(), bmesh.new()
    hub = Vector((0, 0, 9.0))
    # anchors: two stems left and right, the bramble above-left, the ground below
    # culms kink a little at each joint and lean; thin, as dry grass is
    def culm(x0, lean, n, kinks):
        pts, x, y = [], x0, 0.0
        for i in range(n):
            t = i * 0.8
            x += lean * 0.8 + sum(k for z, k in kinks if abs(t - z) < 0.4)
            y += 0.04 * math.sin(t * 0.7)
            pts.append(Vector((x, y, t)))
        return pts
    left = culm(-8.5, 0.016, 25, ((6, 0.18), (13, -0.12)))
    right = culm(7.8, -0.16, 21, ((8, -0.3), (12, -0.45)))
    tube(stems, left, [0.18 * (1 - 0.6 * i / len(left)) for i in range(len(left))], sides=12, ridges=6, ridge_depth=0.12)
    tube(stems, right, [0.16 * (1 - 0.6 * i / len(right)) for i in range(len(right))], sides=12, ridges=6, ridge_depth=0.12)
    # each culm ends in a dry seed head: a nodding spray of spikelets
    for st in (left, right):
        top = st[-1]
        for j in range(14):
            a = j * 2.4
            d = Vector((math.cos(a) * 0.5, math.sin(a) * 0.5, 1.0)).normalized()
            base = top + Vector((0, 0, -0.15 * j))
            tip = base + d * R.uniform(0.6, 1.2) + Vector((0.3, 0, -0.25))
            tube(stems, [base, (base + tip) / 2 + Vector((0, 0, 0.1)), tip], [0.025, 0.02, 0.01], sides=3, cap=False)
            blob(stems, tip, (0.16, 0.06, 0.06), subdiv=1, rough=0.1, seed=j, rot=Matrix.Rotation(a, 3, "Z"))
    # leaf sheaths clasp the culm above each joint, split open down one side and frayed at the top
    for st, zs in ((left, (2.5, 6, 13)), (right, (3, 8))):
        for z in zs:
            k = min(len(st) - 2, int(z / 0.8))
            for j in range(9):
                a0 = 0.5 + j * 0.62
                pts = [st[k] + Vector((math.cos(a0) * 0.3, math.sin(a0) * 0.3, 0)), st[k + 2] + Vector((math.cos(a0) * 0.27, math.sin(a0) * 0.27, 0.2 * R.random()))]
                tube(stems, pts, [0.06, 0.03], sides=3, cap=False)
    # culm joints, slight swellings only
    for st, zs in ((left, (6, 13)), (right, (8,))):
        for z in zs:
            p = st[min(len(st) - 1, int(z / 0.8))]
            blob(stems, p, (0.2, 0.2, 0.05), subdiv=2, rough=0.05, seed=z)
    # a dry, twisted grass blade peeling off each stem and drooping
    for st, z0, side in ((left, 6, 1), (right, 8, -1)):
        base = st[int(z0 / 0.8)]
        prev = None
        for j in range(16):
            u = j / 15
            centre = base + Vector((side * u * 3.2, 0.2 * u, 2.2 * u - 3.4 * u * u))
            w = 0.22 * (1 - u) + 0.02
            tw = u * 2.4
            off = Vector((-math.sin(tw) * w * 0.3, math.cos(tw) * w, 0.0))
            a, b = stems.verts.new(centre - off), stems.verts.new(centre + off)
            if prev:
                stems.faces.new((prev[0], prev[1], b, a))
            prev = (a, b)
    # the bramble: a woody twig across the top with hooked thorns and three serrated leaves
    twig = bezier((-12.0, 2.6, 0.0), (-12.8, 2.2, 15.0), (-4.0, 0.8, 16.6), 14)
    tube(stems, twig, [0.3 - 0.014 * i for i in range(14)], sides=8, ridges=5, ridge_depth=0.06)
    for i in range(2, 13):
        p = twig[i]
        d = Vector((R.uniform(-0.3, 0.3), R.uniform(-1, 1), R.choice((-1, 1)))).normalized()
        tube(stems, bezier(p, p + d * 0.45, p + d * 0.6 + Vector((0.2, 0, 0)), 4), [0.09, 0.06, 0.03, 0.005], sides=5)
    for k, (i, ang) in enumerate(((4, 0.6), (8, -0.9), (11, 2.2))):
        base = twig[i]
        rot = Matrix.Rotation(ang, 3, "Z") @ Matrix.Rotation(0.5, 3, "X")
        pts, ring = [], 9
        for j in range(ring * 2):
            t = j / (ring * 2 - 1)
            for side in (-1, 1):
                pass
        # leaf blade as a grid with serrated edge and a midrib fold
        cols, rows = 12, 4
        grid = []
        for a in range(cols + 1):
            u = a / cols
            w = 1.3 * math.sin(math.pi * u) ** 0.8 * (1 + 0.12 * (a % 2))
            row = []
            for b in range(rows + 1):
                v = b / rows - 0.5
                p = Vector((u * 2.2, v * w * 1.4, -abs(v) * 0.3 + 0.12 * math.sin(u * 3 + k)))
                row.append(leaves.verts.new(base + rot @ p))
            grid.append(row)
        for a in range(cols):
            for b in range(rows):
                leaves.faces.new((grid[a][b], grid[a + 1][b], grid[a + 1][b + 1], grid[a][b + 1]))
    anchors = [Vector((-8.2, 0.2, 13.5)), Vector((-4.2, 0.8, 16.5)), Vector((5.0, 0.0, 13.0)), Vector((6.1, 0.1, 10.0)),
               Vector((5.5, 0.0, 0.0)), Vector((-2.0, 0.0, 0.0)), Vector((-8.2, 0.1, 4.5))]
    # frame threads between the anchors, then radials from the hub to the frame
    frame = []
    for i in range(len(anchors)):
        a, b = anchors[i], anchors[(i + 1) % len(anchors)]
        frame.append((a, b))
        tube(silk, catenary(a, b, 0.25, 12), [0.016] * 12, sides=3, cap=False)
    radials = []
    n_rad = 22
    for k in range(n_rad):
        ang = 2 * math.pi * k / n_rad + R.uniform(-0.12, 0.12)
        d = Vector((math.cos(ang), 0, math.sin(ang)))
        # where the ray meets the frame polygon
        best = None
        for a, b in frame:
            e = b - a
            den = d.x * (-e.z) - d.z * (-e.x)
            if abs(den) < 1e-6:
                continue
            w = a - hub
            t = (w.x * (-e.z) - w.z * (-e.x)) / den
            s = (d.x * w.z - d.z * w.x) / den
            if t > 0 and 0 <= s <= 1 and (best is None or t < best):
                best = t
        if best is None:
            continue
        end = hub + d * best
        radials.append((ang, best))
        tube(silk, catenary(hub, end, R.uniform(0.03, 0.12), 8), [R.uniform(0.007, 0.013)] * 8, sides=3, cap=False)
        if R.random() < 0.25:  # a second strand laid beside the first
            tube(silk, catenary(hub, end + Vector((R.uniform(-0.3, 0.3), 0, R.uniform(-0.3, 0.3))), 0.15, 8), [0.006] * 8, sides=3, cap=False)
    # the capture spiral, rung by rung between neighbouring radials, sagging a little; a torn patch where she hit
    torn_c, torn_r = Vector((1.6, 0, 7.0)), 2.2
    damage = [(torn_c, torn_r), (Vector((-4.0, 0, 11.5)), 1.1), (Vector((3.5, 0, 12.0)), 0.8)]
    r = 1.1
    while r < 9.5:
        step = 0.32 * R.uniform(0.7, 1.4)
        for k in range(len(radials)):
            a0, l0 = radials[k]
            a1, l1 = radials[(k + 1) % len(radials)]
            r0 = r + (k / len(radials)) * step
            r1 = r + ((k + 1) / len(radials)) * step
            if r0 > l0 * 0.92 or r1 > l1 * 0.92:
                continue
            p0 = hub + Vector((math.cos(a0), 0, math.sin(a0))) * r0
            p1 = hub + Vector((math.cos(a1), 0, math.sin(a1))) * r1
            mid = (p0 + p1) / 2
            if any((mid - c).length < cr * (0.8 + 0.4 * R.random()) for c, cr in damage) or R.random() < 0.08:
                continue
            seg = catenary(p0, p1, (0.04 + 0.02 * r) * R.uniform(0.5, 1.8), 4)
            seg = [q + Vector((0, 0.05 * math.sin(q.x * 3 + q.z), 0)) for q in seg]
            tube(silk, seg, [R.uniform(0.005, 0.01)] * 4, sides=3, cap=False)
            if R.random() < 0.12:
                q = seg[R.randint(1, 2)] + Vector((0, 0, -0.04))
                blob(dew, q, (0.035, 0.035, 0.04) if R.random() < 0.75 else (0.06, 0.06, 0.068), subdiv=1, rough=0.0)
        r += step
    # slack threads: broken strands hanging loose from the damage and the frame
    for k in range(16):
        a = hub + Vector((R.uniform(-8, 8), 0, R.uniform(-7, 7)))
        b = a + Vector((R.uniform(-1.5, 1.5), R.uniform(-0.3, 0.3), -R.uniform(1.0, 3.0)))
        tube(silk, catenary(a, b, R.uniform(0.2, 0.6), 7), [0.006] * 7, sides=3, cap=False)
    # the hub's tangle and the silk that wraps the wasp: loose loops thrown round her
    for k in range(20):
        c = torn_c + Vector((R.uniform(-2.2, 2.2), 0, R.uniform(-1.6, 1.6)))
        axis = Vector((R.uniform(-0.3, 0.3), R.uniform(-1, 1), R.uniform(-0.3, 0.3))).normalized()
        perp = axis.orthogonal().normalized()
        rad = R.uniform(0.6, 1.2)
        pts = []
        for j in range(10):
            a = 2 * math.pi * j / 9 * R.uniform(0.8, 1.0)
            q = Matrix.Rotation(a, 3, axis) @ perp
            jit = Vector((R.uniform(-0.12, 0.12), R.uniform(-0.12, 0.12), R.uniform(-0.12, 0.12)))
            pts.append(c + Vector((q.x * rad * 1.4, q.y * rad * 0.6, q.z * rad)) + jit)
        tube(wrap, pts, [0.007] * 10, sides=3, cap=False)
    for k in range(7):
        q = hub + Vector((R.uniform(-0.5, 0.5), R.uniform(-0.1, 0.1), R.uniform(-0.5, 0.5)))
        tube(silk, [hub, (hub + q) / 2 + Vector((0, 0.1, 0)), q], [0.008] * 3, sides=3, cap=False)
    for k in range(7):  # seed husks and leaf crumbs the wind left in the silk
        q = hub + Vector((R.uniform(-6, 6), R.uniform(-0.05, 0.05), R.uniform(-6, 5)))
        blob(stems, q, (R.uniform(0.12, 0.25), 0.05, R.uniform(0.08, 0.15)), subdiv=1, rough=0.25, seed=40 + k, rot=Matrix.Rotation(R.uniform(0, 3), 3, "Y"))
    ob = obj("web_silk", silk); paint(ob, lambda p, n: (0.98, 0.9, 0.76))
    ob = obj("web_wrap", wrap); paint(ob, lambda p, n: (0.96, 0.9, 0.78))
    ob = obj("web_dew", dew); paint(ob, lambda p, n: (0.9, 0.95, 1.0))
    ob = obj("web_stems", stems)
    def stem_colour(p, n):
        if p.x < -9.8 or (p.x < -3 and p.z > 14.5):  # the bramble cane: red-brown bark
            return mix((0.32, 0.16, 0.1), (0.48, 0.26, 0.14), 0.5 + 0.5 * noise.noise(p * 4))
        straw = mix((0.5, 0.42, 0.3), (0.7, 0.6, 0.42), 0.5 + 0.5 * noise.noise(p * 1.7))  # weathered straw, not yellow
        return mix(straw, (0.3, 0.22, 0.14), 0.55 * max(0.0, noise.noise(Vector((math.atan2(p.y, p.x + 8.5) * 9, p.z * 0.4, 0))) * 2 - 0.3))
    paint(ob, stem_colour)
    ob = obj("web_leaves", leaves)
    paint(ob, lambda p, n: mix((0.26, 0.3, 0.12), (0.44, 0.38, 0.16), 0.5 + 0.6 * noise.noise(p * 0.9)))
    export("web")


# ---------------------------------------------------------------------------------------------- the nursery (H3)
def nursery():
    """The royal larva's bed of chewed plant fibre, and the living gauge: remains and seeds the nurses pile on a curved
    ledge (heap_000 is laid first). The bed is centred on the room; the ledge curves round its back."""
    reset()
    # the ledge: a bank of packed clay the nurses raised round the back of the chamber
    ledge = bmesh.new()
    cols, rows = 40, 6
    grid = []
    def ledge_h(u, v):
        return (0.42 * math.sin(math.pi * v) ** 0.7 + 0.06 * noise.noise(Vector((u * 9, v * 3, 1.0)))) * (0.7 + 0.3 * math.sin(math.pi * u))
    for a in range(cols + 1):
        u = a / cols
        ang = math.radians(185 + 170 * u)
        row = []
        for b in range(rows + 1):
            v = b / rows
            rad = 1.35 + 0.7 * v
            row.append(ledge.verts.new((math.cos(ang) * rad * 1.15, math.sin(ang) * rad * 0.95, ledge_h(u, v) - 0.02)))
        grid.append(row)
    for a in range(cols):
        for b in range(rows):
            ledge.faces.new((grid[a][b], grid[a + 1][b], grid[a + 1][b + 1], grid[a][b + 1]))
    lo = obj("nursery_ledge", ledge)
    paint(lo, lambda p, n: mix((0.4, 0.18, 0.08), (0.58, 0.3, 0.14), 0.5 + 0.5 * noise.noise(p * 4)))
    bed = bmesh.new()
    for k in range(1150):
        layer = k % 3
        a = R.uniform(0, 2 * math.pi)
        rr = math.sqrt(R.random())
        c = Vector((math.cos(a) * rr * 1.25, math.sin(a) * rr * 0.85, 0.03 + 0.3 * (1 - rr * rr) + 0.07 * layer + 0.04 * R.random()))
        # a chewed strand wanders: a short random walk that curls, mostly lying flat in the mat
        hair = R.random() < 0.45
        pts, q = [], c.copy()
        heading = R.uniform(0, 2 * math.pi)
        curl = R.uniform(-0.9, 0.9)
        for j in range(7):
            pts.append(q.copy())
            heading += curl * R.uniform(0.5, 1.5)
            q += Vector((math.cos(heading), math.sin(heading), R.uniform(-0.25, 0.3))) * R.uniform(0.12, 0.24)
        w = R.uniform(0.006, 0.014) if hair else R.uniform(0.018, 0.04)
        tube(bed, pts, [w * (1 - 0.3 * j / 6) for j in range(7)], sides=3, cap=False)
    ob = obj("nursery_bed", bed)
    shades = [(0.5, 0.36, 0.18), (0.36, 0.24, 0.12), (0.66, 0.52, 0.3), (0.28, 0.18, 0.09), (0.74, 0.62, 0.4), (0.45, 0.4, 0.22)]
    paint(ob, lambda p, n: mix(shades[int(abs(noise.noise(p * 5.3)) * 60) % 6], (0.9, 0.82, 0.6), 0.1 * (noise.noise(p * 9) + 0.5)))
    # the heap, laid from one end of the ledge to the other and stacked as it grows
    pieces = 170
    for i in range(pieces):
        t = i / pieces
        u = (t * 3.0) % 1.0
        layer = int(t * 3.0)
        uu = 0.05 + 0.9 * u
        ang = math.radians(185 + 170 * uu + R.uniform(-3, 3))
        vv = R.uniform(0.3, 0.7)
        rad = 1.35 + 0.7 * vv
        top = ledge_h(uu, vv)
        base = Vector((math.cos(ang) * rad * 1.15, math.sin(ang) * rad * 0.95, top + 0.08 + 0.17 * layer + R.uniform(0, 0.06)))
        bm = bmesh.new()
        kind = R.random()
        rot = Matrix.Rotation(R.uniform(0, 6.3), 3, "Z") @ Matrix.Rotation(R.uniform(-0.6, 0.6), 3, "X")
        if kind < 0.28:     # sunflower seed: a teardrop, pointed at one end, ridged, grey stripes on near-black
            L = 0.34 * R.uniform(0.85, 1.15)
            vs = blob(bm, Vector(), (L, 0.15, 0.07), subdiv=3, rough=0.03, seed=i)
            for v in vs:
                t = v.co.x / L
                v.co.y *= 0.55 + 0.45 * max(0.0, 1 - max(0.0, t)) ** 0.5 if t > 0 else 1
                v.co.z *= 1 + 0.15 * math.cos(v.co.y * 40)
                v.co = rot @ v.co + base
            col = lambda p, n, b=base, r=rot: mix((0.16, 0.13, 0.1), (0.56, 0.52, 0.44), 1.0 if math.sin(((r.inverted() @ (p - b)).y) * 48) > 0.6 else 0.0)
        elif kind < 0.5:    # grass seed
            blob(bm, base, (0.22, 0.1, 0.08), subdiv=2, rough=0.06, seed=i, rot=rot)
            col = lambda p, n: mix((0.8, 0.66, 0.34), (0.92, 0.8, 0.5), noise.noise(p * 20) + 0.5)
        elif kind < 0.58:   # a beetle's wing case: a curved shell, its rows of pits catching the light
            L = 0.42 * R.uniform(0.8, 1.2)
            vs = blob(bm, Vector(), (L, 0.18, 0.12), subdiv=3, rough=0.02, seed=i)
            for v in vs:
                if v.co.z < 0:
                    v.co.z *= 0.12
                v.co = rot @ v.co + base
            col = lambda p, n, b=base, r=rot: mix((0.05, 0.04, 0.035), (0.3, 0.16, 0.06), 0.35 + 0.3 * math.sin(((r.inverted() @ (p - b)).y) * 70) * math.sin(((r.inverted() @ (p - b)).x) * 70))
        elif kind < 0.72:   # a split seed husk: an empty half shell
            vs = blob(bm, Vector(), (0.24, 0.13, 0.1), subdiv=3, rough=0.05, seed=i)
            for v in vs:
                if v.co.z > 0.02:
                    v.co.z = 0.02 - (v.co.z - 0.02) * 0.6
                v.co = rot @ v.co + base
            col = lambda p, n: mix((0.72, 0.6, 0.38), (0.88, 0.78, 0.55), noise.noise(p * 18) * 0.5 + 0.5)
        elif kind < 0.86:   # a jointed insect leg: femur, tibia, a hooked tarsus
            d = rot @ Vector((1, 0, 0))
            knee = base + d * 0.4 + Vector((0, 0, 0.12))
            ankle = knee + (rot @ Vector((0.7, 0.4, 0))).normalized() * 0.38 - Vector((0, 0, 0.1))
            tube(bm, [base, (base + knee) / 2 + Vector((0, 0, 0.03)), knee], [0.06, 0.07, 0.05], sides=6)
            tube(bm, [knee, (knee + ankle) / 2, ankle], [0.045, 0.04, 0.035], sides=6)
            tube(bm, bezier(ankle, ankle + d * 0.12, ankle + d * 0.2 - Vector((0, 0, 0.06)), 4), [0.03, 0.025, 0.02, 0.008], sides=5)
            col = lambda p, n: mix((0.16, 0.08, 0.03), (0.42, 0.22, 0.07), noise.noise(p * 10) + 0.5)
        else:               # a scrap of veined wing membrane
            uv = bm.loops.layers.uv.new("UV")
            g = [[None] * 4 for _ in range(5)]
            for a in range(5):
                for b in range(4):
                    q = Vector(((a / 4 - 0.5) * 0.7, (b / 3 - 0.5) * 0.35, 0.04 * math.sin(a + b)))
                    g[a][b] = bm.verts.new(rot @ q + base + Vector((0, 0, 0.05)))
            for a in range(4):
                for b in range(3):
                    f = bm.faces.new((g[a][b], g[a + 1][b], g[a + 1][b + 1], g[a][b + 1]))
                    for loop, (ai, bi) in zip(f.loops, ((a, b), (a + 1, b), (a + 1, b + 1), (a, b + 1))):
                        loop[uv].uv = (0.3 + ai / 4 * 0.5, bi / 3)
            col = lambda p, n: (0.8, 0.62, 0.3)
        k_size = 1.35 * R.uniform(0.7, 1.25)
        for v in bm.verts:  # pieces large enough to read from the nest view, and no two alike
            v.co = base + (v.co - base) * k_size
        ob = obj(f"heap_{i:03d}" + ("_wing" if kind >= 0.86 else ""), bm)
        paint(ob, col)
    export("nursery")


gland()
wasp()
web()
nursery()
print("[blender] done")
