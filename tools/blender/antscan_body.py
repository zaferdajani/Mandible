"""The rigid body of a real ant from an Antscan surface mesh (micro-CT, CC-BY 4.0, biomedisa.info/antscan), re-posed
for the game and reduced to its levels of detail, with a normal map baked from the full scan.

Run: blender --background --factory-startup --python tools/blender/antscan_body.py -- <scan.stl> <out-dir> <name>
         [--length L] [--flip-head] [--flip-gaster]

Ethanol-preserved specimens are curled: the head tucked down, the gaster swung forward, the legs folded. So:
  1. the scan is reduced to a working mesh, and each vertex's thickness is measured by casting a ray inward;
     the legs, antennae, mandibles, the neck and the waist are thin, so the thick vertices fall apart into the
     three body masses: head, mesosoma and gaster (the gaster is the biggest, the mesosoma lies between);
  2. every vertex of the full scan joins its nearest mass, or is dropped as limb (the game animates its own legs,
     antennae and mandibles);
  3. each mass gets a frame: forward from the joints (neck, waist), and the minimal rotation that straightens a
     curled head or gaster onto the mesosoma's axis; the mesosoma's down is the side its legs leave from;
  4. the parts are placed in the game's model space (+Z forward, +Y up), the neck and the waist meeting, the
     whole ant `L` long (default 0.78, a worker in AntMesh units), its belly at the body height of ant_models.py;
  5. the eyes are found where a ray from the head's centre leaves it on each side, and the vertices of that bulge
     are labelled eye;
  6. near, mid and far meshes are decimated from the posed scan, unwrapped into one atlas, and the near mesh gets a
     normal map baked from the full-resolution scan (setae sockets, punctures, sutures).
Writes <out-dir>/<name>_{near,mid,far}.body (see write_body), <name>_normal.png and <name>.json (the joints, hip
and socket points, eye centres, gaster tip, in game units) for tools/blender/ant_models.py.
"""
import json
import math
import os
import struct
import sys

import bmesh
import bpy
import numpy as np
from mathutils import Matrix, Vector
from mathutils.bvhtree import BVHTree
from mathutils.kdtree import KDTree

args = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT, NAME = args[0], args[1], args[2]
LENGTH = float(args[args.index("--length") + 1]) if "--length" in args else 0.78
BODY_Y = 0.13
LABELS = ("head", "mesosoma", "gaster")
BUDGET = {"near": 7000, "mid": 2400, "far": 700}


def log(*a):
    print("[antscan-body]", *a, flush=True)


def mesh_arrays(ob):
    me = ob.data
    vs = [v.co.copy() for v in me.vertices]
    me.calc_loop_triangles()
    ts = [tuple(t.vertices) for t in me.loop_triangles]
    return vs, ts


def decimated_copy(ob, faces, name):
    c = ob.copy()
    c.data = ob.data.copy()
    c.name = name
    bpy.context.scene.collection.objects.link(c)
    ratio = min(1.0, faces / max(1, len(c.data.polygons)))
    if ratio < 1.0:
        m = c.modifiers.new("d", "DECIMATE")
        m.ratio = ratio
        m.use_collapse_triangulate = True
        bpy.context.view_layer.objects.active = c
        bpy.ops.object.select_all(action="DESELECT")
        c.select_set(True)
        bpy.ops.object.modifier_apply(modifier="d")
    return c


def components(n, adj, keep):
    """Connected components of the vertices where keep[i], over the adjacency lists."""
    comp = [-1] * n
    out = []
    for s in range(n):
        if not keep[s] or comp[s] >= 0:
            continue
        stack, members = [s], []
        comp[s] = len(out)
        while stack:
            i = stack.pop()
            members.append(i)
            for j in adj[i]:
                if keep[j] and comp[j] < 0:
                    comp[j] = len(out)
                    stack.append(j)
        out.append(members)
    return comp, out


def main():
    os.makedirs(OUT, exist_ok=True)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.wm.stl_import(filepath=SRC)
    scan = bpy.context.selected_objects[0]
    scan.name = "scan"
    log("scan", len(scan.data.polygons), "triangles")
    if len(scan.data.polygons) > 1500000:
        # more than a 1024-px normal map can hold: reduce first, it keeps the sculpture and saves most of the time
        scan = decimated_copy(scan, 1500000, "scan_reduced")
        bpy.data.objects.remove(bpy.data.objects["scan"])
        scan.name = "scan"

    # 1. thickness on a working mesh
    work = decimated_copy(scan, 120000, "work")
    wv, wt = mesh_arrays(work)
    bvh = BVHTree.FromPolygons(wv, wt)
    bm = bmesh.new()
    bm.from_mesh(work.data)
    bm.verts.ensure_lookup_table()
    normals = [v.normal.copy() for v in bm.verts]
    adj = [[e.other_vert(v).index for e in v.link_edges] for v in bm.verts]
    bm.free()
    size = max(work.dimensions)
    thick = []
    for p, nrm in zip(wv, normals):
        hit = bvh.ray_cast(p - nrm * size * 1e-4, -nrm, size)
        thick.append((hit[0] - p).length if hit[0] is not None else 0.0)
    ts = sorted(thick)
    big = ts[int(len(ts) * 0.97)]
    floor_limb = 0.17 * big

    def limbs_on(m):
        """How many thin (limb) vertices touch this mass: the thorax carries six legs (and a queen's wing stubs),
        the head its antennae and mandibles, the gaster none."""
        ms = set(m)
        return sum(1 for i in m for j in adj[i] if j not in ms and thick[j] < floor_limb)

    def centre(m):
        return sum((wv[i] for i in m), Vector()) / len(m)

    def order_of(ms):
        """(head, mesosoma, gaster) for three masses: the mesosoma lies between the other two, and of the two ends the
        gaster is the heavier. None when no mass lies between the others."""
        c = [centre(m) for m in ms]
        meso = min(range(3), key=lambda k: sum((c[k] - c[j]).length for j in range(3)))
        ends = [k for k in range(3) if k != meso]
        if (c[ends[0]] - c[ends[1]]).length < 1.1 * max((c[ends[0]] - c[meso]).length, (c[ends[1]] - c[meso]).length):
            return None
        gas = max(ends, key=lambda k: sum(thick[i] for i in ms[k]))
        head = [k for k in ends if k != gas][0]
        return head, meso, gas

    def cut(piece):
        """Two halves of a fused piece, parted at its narrowest section along its own long axis."""
        P = np.array([tuple(wv[i]) for i in piece])
        c0 = P.mean(0)
        w, vecs = np.linalg.eigh(np.cov((P - c0).T))
        axis = vecs[:, 2]
        t = (P - c0) @ axis
        radial = np.linalg.norm((P - c0) - np.outer(t, axis), axis=1)
        bins = np.linspace(np.percentile(t, 15), np.percentile(t, 85), 25)
        width = [radial[(t >= bins[k]) & (t < bins[k + 1])].max(initial=0) for k in range(24)]
        k = int(np.argmin(width))
        at = 0.5 * (bins[k] + bins[k + 1])
        return [piece[j] for j in range(len(piece)) if t[j] < at], [piece[j] for j in range(len(piece)) if t[j] >= at]

    masses, order = None, None
    fracs = [float(args[args.index("--frac") + 1])] if "--frac" in args else [0.32 + 0.04 * k for k in range(14)]
    for frac in fracs:
        keep = [t > frac * big for t in thick]
        comp, comps = components(len(wv), adj, keep)
        comps.sort(key=len, reverse=True)
        log(f"opening {frac:.2f}: pieces {[len(c) for c in comps[:5]]}")
        if len(comps) >= 3 and len(comps[2]) > 0.03 * len(wv):
            o = order_of(comps[:3])
            if o:
                masses, order = comps[:3], o
                break
    if masses is None:
        # a queen's broad waist (or a stout neck) keeps two masses fused at every opening: free the third, then cut
        # whichever piece gives an ant with its legged thorax in the middle
        keep = [t > 0.32 * big for t in thick]
        comp, comps = components(len(wv), adj, keep)
        comps.sort(key=len, reverse=True)
        if len(comps) < 2 or len(comps[1]) < 0.03 * len(wv):
            raise SystemExit("could not part the body into head, mesosoma and gaster")
        for k in (0, 1):
            a_side, b_side = cut(comps[k])
            ms = [a_side, b_side, comps[1 - k]]
            o = order_of(ms)
            if o:
                masses, order = ms, o
                log(f"cut piece {k} at its narrowest section ({len(a_side)} | {len(b_side)})")
                break
        if masses is None:
            raise SystemExit("could not part the body into head, mesosoma and gaster")
    cents = [centre(m) for m in masses]
    log("masses (head, mesosoma, gaster):", [len(masses[k]) for k in order], "limbs on each:", [limbs_on(masses[k]) for k in order])

    # 2. grow each mass over the working mesh through thick surface only: the growth climbs the body's flanks and
    # stops where a leg, an antenna or a mandible leaves it; then every scan vertex takes its nearest working
    # vertex's label, or is dropped as limb
    wl = [-1] * len(wv)
    frontier = []
    for label, k in enumerate(order):
        for i in masses[k]:
            wl[i] = label
            frontier.append(i)
    floor = 0.17 * big
    while frontier:
        nxt = []
        for i in frontier:
            for j in adj[i]:
                if wl[j] < 0 and thick[j] > floor:
                    wl[j] = wl[i]
                    nxt.append(j)
        frontier = nxt
    wkd = KDTree(len(wv))
    for i, p in enumerate(wv):
        wkd.insert(p, i)
    wkd.balance()
    sv, st = mesh_arrays(scan)
    labels = []
    for p in sv:
        co, idx, dist = wkd.find(p)
        labels.append(wl[idx])
    log("kept", sum(1 for x in labels if x >= 0), "of", len(labels), "scan vertices as body")

    pts = {k: [sv[i] for i in range(len(sv)) if labels[i] == k] for k in range(3)}
    C = {k: sum(pts[k], Vector()) / len(pts[k]) for k in range(3)}

    def joint(a, b):
        near_b = KDTree(len(pts[b]))
        for i, p in enumerate(pts[b]):
            near_b.insert(p, i)
        near_b.balance()
        close = sorted(pts[a], key=lambda p: near_b.find(p)[2])[:200]
        return sum(close, Vector()) / len(close)

    neck, waist = joint(0, 1), joint(2, 1)
    limb = [sv[i] for i in range(len(sv)) if labels[i] == -1]
    legs = [p for p in limb if (p - C[1]).length < 1.6 * (neck - waist).length]
    down = (sum(legs, Vector()) / len(legs) - C[1]) if legs else Vector((0, 0, -1))

    # 3. frames: the mesosoma's from the joints and the legs; the head's and the gaster's straightened onto it
    fwd = (neck - waist).normalized()
    up = -(down - fwd * down.dot(fwd)).normalized()
    side = fwd.cross(up)
    to_game = Matrix((side, up, fwd))  # rows: game x, y, z of a scan vector

    def straighten(part_fwd):
        return part_fwd.rotation_difference(fwd).to_matrix()

    head_fwd = (C[0] - neck).normalized()
    gas_fwd = (waist - C[2]).normalized()
    R = {0: to_game @ straighten(head_fwd), 1: to_game, 2: to_game @ straighten(gas_fwd)}
    def level(k):
        pts_k = [R[k] @ (p - pivot_of[k]) for p in pts[k][::7]]
        cx = sum(p.x for p in pts_k) / len(pts_k)
        cy = sum(p.y for p in pts_k) / len(pts_k)
        sxx = sum((p.x - cx) ** 2 for p in pts_k)
        syy = sum((p.y - cy) ** 2 for p in pts_k)
        sxy = sum((p.x - cx) * (p.y - cy) for p in pts_k)
        ang = 0.5 * math.atan2(2 * sxy, sxx - syy)  # the widest direction's angle from game x
        return Matrix.Rotation(-ang, 3, "Z") @ R[k]

    pivot_of = {0: neck, 1: C[1], 2: waist}
    R[0], R[2] = level(0), level(2)
    if "--flip-head" in args:
        R[0] = Matrix.Rotation(math.pi, 3, "Z") @ R[0]
    if "--flip-gaster" in args:
        R[2] = Matrix.Rotation(math.pi, 3, "Z") @ R[2]
    # pivots: the head turns about the neck, the gaster about the waist
    pivot = {0: neck, 1: C[1], 2: waist}

    def place(k, p):
        return R[k] @ (p - pivot[k]) + (to_game @ (pivot[k] - C[1]))

    posed = [place(labels[i], sv[i]) if labels[i] >= 0 else None for i in range(len(sv))]
    allp = [p for p in posed if p is not None]
    zmin = min(p.z for p in allp)
    zmax = max(p.z for p in allp)
    s = LENGTH / (zmax - zmin)
    ymid = sum(p.y for p in allp) / len(allp)
    shift = Vector((0, BODY_Y - ymid * s, -(zmin * s) - LENGTH * 0.62))  # the head's front ends ~0.38 ahead of the origin

    def game(p):
        return p * s + shift

    # 4. the posed body as one object (limbs dropped), keeping its triangles
    keepv = [i for i in range(len(sv)) if labels[i] >= 0]
    remap = {i: n for n, i in enumerate(keepv)}
    faces = [(remap[a], remap[b], remap[c]) for a, b, c in st if a in remap and b in remap and c in remap
             and labels[a] == labels[b] == labels[c]]
    me = bpy.data.meshes.new("body")
    me.from_pydata([tuple(game(posed[i])) for i in keepv], [], faces)
    lab_attr = me.attributes.new("part", "INT", "POINT")
    for n, i in enumerate(keepv):
        lab_attr.data[n].value = labels[i]
    body = bpy.data.objects.new("body", me)
    bpy.context.scene.collection.objects.link(body)
    bpy.data.objects.remove(scan)
    bpy.data.objects.remove(work)
    bpy.context.view_layer.objects.active = body
    bpy.ops.object.select_all(action="DESELECT")
    body.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.separate(type="LOOSE")
    bpy.ops.object.mode_set(mode="OBJECT")
    pieces = sorted(bpy.context.selected_objects, key=lambda o: len(o.data.vertices), reverse=True)
    total = sum(len(o.data.vertices) for o in pieces)
    keep = [o for o in pieces if len(o.data.vertices) > 0.01 * total]
    for o in pieces:
        if o not in keep:
            bpy.data.objects.remove(o)
    bpy.ops.object.select_all(action="DESELECT")
    for o in keep:
        o.select_set(True)
    bpy.context.view_layer.objects.active = keep[0]
    bpy.ops.object.join()
    body = keep[0]
    me = body.data
    lab_attr = me.attributes["part"]
    # the scans are wound inside out: turn every face outward (this also flips the triangles' winding)
    bm2 = bmesh.new()
    bm2.from_mesh(me)
    # close the openings the cut limbs, the neck and the waist leave in the cuticle (the fill takes its neighbours'
    # part, so a socket stays with its head or thorax)
    edges = [e for e in bm2.edges if e.is_boundary]
    filled = bmesh.ops.holes_fill(bm2, edges=edges, sides=0)
    bmesh.ops.triangulate(bm2, faces=filled["faces"])
    bm2.verts.ensure_lookup_table()
    lay = bm2.verts.layers.int.get("part")
    flipped = []
    for lab in range(3):
        faces = [f for f in bm2.faces if f.verts[0][lay] == lab]
        if not faces:
            continue
        c = sum((f.calc_center_median() for f in faces), Vector()) / len(faces)
        out = sum(1 for f in faces if f.normal.dot(f.calc_center_median() - c) > 0) / len(faces)
        if out < 0.5:
            bmesh.ops.reverse_faces(bm2, faces=faces)
            flipped.append(LABELS[lab])
    bm2.normal_update()
    bm2.to_mesh(me)
    bm2.free()
    me.update()
    lab_attr = me.attributes["part"]  # writing the mesh back rebuilt its attributes
    log("faces turned outward on:", flipped)
    log("posed body:", len(me.vertices), "vertices,", len(me.polygons), "triangles in", len(keep), "pieces")

    # 5. the joints and landmarks in game units
    bv, bt = mesh_arrays(body)
    bbvh = BVHTree.FromPolygons(bv, bt)
    head_c = game(place(0, C[0]))
    meso_c = game(place(1, C[1]))
    gas_c = game(place(2, C[2]))

    def out_from(at, d):
        hit = bbvh.ray_cast(at + d.normalized() * 0.4, -d.normalized(), 0.8)  # from outside, back in
        return hit[0] if hit[0] is not None else at

    eyes = [out_from(head_c, Vector((sx, 0.3, 0.05))) for sx in (1, -1)]

    def tip_point(c):
        return out_from(c, Vector((0, -0.1, -1)))
    hips = []
    for zf in (0.32, 0.05, -0.22):
        for sx in (1, -1):
            at = meso_c + Vector((0, 0, zf * (game(place(1, neck)).z - game(place(1, waist)).z)))
            hips.append(out_from(at, Vector((sx * 0.5, -1, 0))))
    sockets = [out_from(head_c + Vector((0, 0, 0.02)), Vector((sx * 0.45, 0.6, 1))) for sx in (1, -1)]
    hinges = [out_from(head_c, Vector((sx * 0.5, -0.45, 1))) for sx in (1, -1)]

    def mirrored(a, b, mid):
        x = (abs(a.x - mid) + abs(b.x - mid)) / 2
        y, z = (a.y + b.y) / 2, (a.z + b.z) / 2
        return Vector((mid + x, y, z)), Vector((mid - x, y, z))

    mx = meso_c.x
    for i in (0, 2, 4):
        hips[i], hips[i + 1] = mirrored(hips[i], hips[i + 1], mx)
        # a hip sits a little inside the body's flank, where the coxa leaves it
        for j in (i, i + 1):
            hips[j] = Vector((mx + (hips[j].x - mx) * 0.8, hips[j].y + 0.006, hips[j].z))
    sockets = list(mirrored(sockets[0], sockets[1], head_c.x))
    hinges = list(mirrored(hinges[0], hinges[1], head_c.x))
    eyes = list(mirrored(eyes[0], eyes[1], head_c.x))
    lift = Vector((-mx, (BODY_Y - 0.012) - sum(h.y for h in hips) / 6, 0))
    for ob_v in body.data.vertices:
        ob_v.co += lift
    head_c, meso_c, gas_c, tip = head_c + lift, meso_c + lift, gas_c + lift, tip_point(gas_c) + lift
    hips = [h + lift for h in hips]
    sockets = [x + lift for x in sockets]
    hinges = [x + lift for x in hinges]
    eyes = [x + lift for x in eyes]
    neck_g, waist_g = game(place(1, neck)) + lift, game(place(1, waist)) + lift
    # the joints can land on a flank of the part (the nearest contact, not its centre): centre the head and the
    # gaster on the body's axis, as a living ant carries them
    for lab, centre in ((0, head_c), (2, gas_c)):
        dx = -centre.x
        for n, v in enumerate(body.data.vertices):
            if lab_attr.data[n].value == lab:
                v.co.x += dx
        if lab == 0:
            head_c = head_c + Vector((dx, 0, 0))
            sockets = [x + Vector((dx, 0, 0)) for x in sockets]
            hinges = [x + Vector((dx, 0, 0)) for x in hinges]
            eyes = [x + Vector((dx, 0, 0)) for x in eyes]
            neck_g.x = 0.0
        else:
            gas_c = gas_c + Vector((dx, 0, 0))
            tip = tip + Vector((dx, 0, 0))
            waist_g.x = 0.0
    info = {"neck": list(neck_g), "waist": list(waist_g), "head": list(head_c),
            "mesosoma": list(meso_c), "gaster": list(gas_c), "eyes": [list(e) for e in eyes],
            "hips": [list(hp) for hp in hips], "sockets": [list(x) for x in sockets], "hinges": [list(x) for x in hinges],
            "gaster_tip": list(tip), "head_width": 2 * abs(eyes[0].x), "scale": s}
    json.dump(info, open(os.path.join(OUT, NAME + ".json"), "w"), indent=1)

    # eyes: the bulge around each eye point
    eye_r = 0.11 * info["head_width"]
    for n, v in enumerate(body.data.vertices):
        if lab_attr.data[n].value == 0 and min((v.co - e).length for e in eyes) < eye_r:
            lab_attr.data[n].value = 3

    # 6. levels of detail, one atlas, a normal map baked from the full posed scan
    hi = body
    lods = {}
    for lod, budget in BUDGET.items():
        lo = decimated_copy(hi, budget, "lo_" + lod)
        lods[lod] = lo
    near = lods["near"]
    bpy.context.view_layer.objects.active = near
    bpy.ops.object.select_all(action="DESELECT")
    near.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(80), island_margin=0.003, area_weight=0.6)
    bpy.ops.object.mode_set(mode="OBJECT")
    bake_normals(hi, near, os.path.join(OUT, NAME + "_normal.png"))
    # mid and far take the near mesh's atlas (decimated from it, so their UVs follow)
    for lod in ("mid", "far"):
        bpy.data.objects.remove(lods[lod])
        lods[lod] = decimated_copy(near, BUDGET[lod], "lo_" + lod)
    # decimation does not carry the labels faithfully: take each reduced vertex's from the nearest full-scan vertex
    hkd = KDTree(len(hi.data.vertices))
    for n, v in enumerate(hi.data.vertices):
        hkd.insert(v.co, n)
    hkd.balance()
    hl = hi.data.attributes["part"].data
    for lod, ob in lods.items():
        a = ob.data.attributes.get("part") or ob.data.attributes.new("part", "INT", "POINT")
        for n, v in enumerate(ob.data.vertices):
            a.data[n].value = hl[hkd.find(v.co)[1]].value
    for lod, ob in lods.items():
        write_body(os.path.join(OUT, f"{NAME}_{lod}.body"), ob)
        log(lod, len(ob.data.polygons), "triangles")


def bake_normals(hi, lo, path, res=1024):
    sc = bpy.context.scene
    sc.render.engine = "CYCLES"
    sc.cycles.device = "CPU"
    sc.cycles.samples = 1
    img = bpy.data.images.new("nrm", res, res, alpha=False, float_buffer=False)
    img.colorspace_settings.name = "Non-Color"
    mat = bpy.data.materials.new("bake")
    mat.use_nodes = True
    node = mat.node_tree.nodes.new("ShaderNodeTexImage")
    node.image = img
    mat.node_tree.nodes.active = node
    lo.data.materials.clear()
    lo.data.materials.append(mat)
    for p in lo.data.polygons:
        p.use_smooth = True
    for p in hi.data.polygons:
        p.use_smooth = True
    bpy.ops.object.select_all(action="DESELECT")
    hi.select_set(True)
    lo.select_set(True)
    bpy.context.view_layer.objects.active = lo
    sc.render.bake.use_selected_to_active = True
    sc.render.bake.cage_extrusion = 0.004
    sc.render.bake.max_ray_distance = 0.012
    sc.render.bake.margin = 6
    bpy.ops.object.bake(type="NORMAL", normal_space="TANGENT")
    img.filepath_raw = path
    img.file_format = "PNG"
    img.save()


def write_body(path, ob):
    """'BDY1', vertex count, triangle count; per vertex position(3) normal(3) uv(2) as float32 and the part label
    (0 head, 1 mesosoma, 2 gaster, 3 eye) as int32; then the triangles as uint32. Split per UV seam."""
    me = ob.data
    me.calc_loop_triangles()
    uv = me.uv_layers.active.data if me.uv_layers.active else None
    lab = me.attributes["part"].data if "part" in me.attributes else None
    for p in me.polygons:
        p.use_smooth = True
    corner_n = [l.normal.copy() for l in me.loops] if hasattr(me, "loops") else None
    verts, index, tris = [], {}, []
    for t in me.loop_triangles:
        tri = []
        for li, vi in zip(t.loops, t.vertices):
            u = tuple(round(c, 6) for c in uv[li].uv) if uv else (0.0, 0.0)
            key = (vi, u)
            if key not in index:
                index[key] = len(verts)
                v = me.vertices[vi]
                n = me.vertices[vi].normal
                verts.append((v.co.x, v.co.y, v.co.z, n.x, n.y, n.z, u[0], u[1], lab[vi].value if lab else 0))
            tri.append(index[key])
        tris.append(tri)
    with open(path, "wb") as fh:
        fh.write(b"BDY1")
        fh.write(struct.pack("<II", len(verts), len(tris)))
        for r in verts:
            fh.write(struct.pack("<8fi", *r))
        for t in tris:
            fh.write(struct.pack("<3I", *t))


main()
