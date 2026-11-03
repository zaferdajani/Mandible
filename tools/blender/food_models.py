"""Models the small food pieces and the lab tube in Blender, headless, and exports each as glTF.

Run: blender --background --factory-startup --python tools/blender/food_models.py -- <out-dir>
Writes <out-dir>/<name>/<name>.gltf for each model, the layout tools/asset-process.gd reads. Every piece is
built from a seed, so a run always gives the same meshes. Sizes are real (metres): a sugar crystal ~1 mm, a
sunflower seed ~10 mm, a currant ~8 mm, the formicarium tube 16 mm across and 150 mm long.
"""
import math
import os
import random
import sys

import bmesh
import bpy
from mathutils import Matrix, Vector, noise

OUT = sys.argv[sys.argv.index("--") + 1]


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


def mesh_object(name, bm):
    me = bpy.data.meshes.new(name)
    bm.to_mesh(me)
    bm.free()
    ob = bpy.data.objects.new(name, me)
    bpy.context.scene.collection.objects.link(ob)
    return ob


def smart_uv(ob):
    bpy.context.view_layer.objects.active = ob
    ob.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT")
    bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.uv.smart_project(angle_limit=math.radians(66), island_margin=0.02)
    bpy.ops.object.mode_set(mode="OBJECT")
    ob.select_set(False)


def shade(ob, smooth, angle=35):
    for p in ob.data.polygons:
        p.use_smooth = smooth
    if smooth:
        mod = ob.modifiers.new("autosmooth", "EDGE_SPLIT")
        mod.split_angle = math.radians(angle)


# ------------------------------------------------------------------ sugar: irregular cleaved crystal chunks
def sugar(seed):
    rng = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    # a crystal cleaves along planes: cut the cube by a few random planes, keep the inside
    for _ in range(rng.randint(3, 6)):
        n = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))).normalized()
        d = rng.uniform(0.25, 0.45)
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=n * d, plane_no=n, clear_outer=True)
        edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        if edges:
            bmesh.ops.edgeloop_fill(bm, edges=edges)
    bmesh.ops.scale(bm, vec=Vector((rng.uniform(0.8, 1.2), rng.uniform(0.7, 1.1), rng.uniform(0.85, 1.25))) * 0.001, verts=bm.verts)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    ob = mesh_object(f"sugar_{seed}", bm)
    shade(ob, False)
    smart_uv(ob)
    return ob


# ------------------------------------------------------------------ crumbs: torn bread, soft lumpy chunks with a crust side
def crumb(seed):
    rng = random.Random(seed)
    bm = bmesh.new()
    bmesh.ops.create_cube(bm, size=1.0)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=6, use_grid_fill=True)
    # torn: cut by a few planes, then the surface pulled and pitted so no face stays flat
    for _ in range(rng.randint(2, 4)):
        nrm = Vector((rng.uniform(-1, 1), rng.uniform(-1, 1), rng.uniform(-1, 1))).normalized()
        geom = bm.verts[:] + bm.edges[:] + bm.faces[:]
        res = bmesh.ops.bisect_plane(bm, geom=geom, plane_co=nrm * rng.uniform(0.2, 0.42), plane_no=nrm, clear_outer=True)
        edges = [e for e in res["geom_cut"] if isinstance(e, bmesh.types.BMEdge)]
        if edges:
            bmesh.ops.edgeloop_fill(bm, edges=edges)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    bmesh.ops.subdivide_edges(bm, edges=bm.edges[:], cuts=2)
    off = Vector((seed * 1.7, seed * 0.3, 0))
    for v in bm.verts:
        p = v.co
        # soft rounding toward a blob, big lumps, and fine pits like torn crumb
        r = p.length
        p = p.lerp(p.normalized() * 0.45, 0.6)
        p += p.normalized() * (0.13 * noise.noise(p * 3 + off) + 0.05 * noise.noise(p * 9 + off) + 0.02 * noise.noise(p * 23 + off))
        v.co = p
    s = rng.uniform(0.006, 0.012)
    bmesh.ops.scale(bm, vec=Vector((rng.uniform(0.8, 1.25), rng.uniform(0.6, 0.95), rng.uniform(0.85, 1.3))) * s, verts=bm.verts)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    ob = mesh_object(f"crumb_{seed}", bm)
    shade(ob, True, 60)
    smart_uv(ob)
    return ob


# ------------------------------------------------------------------ seeds: sunflower seed shells, whole and split
def seed_shell(seed, half=False):
    rng = random.Random(seed)
    bm = bmesh.new()
    rings, sides = 18, 16
    L, W, T = rng.uniform(0.009, 0.012), rng.uniform(0.0045, 0.0055), rng.uniform(0.0026, 0.0032)
    verts = []
    for i in range(rings + 1):
        v = i / rings
        # a teardrop: blunt at the base, pointed where it held to the flower head, slightly asymmetric
        prof = math.sin(math.pi * v ** 0.8) ** 0.75 * (1 + 0.08 * math.sin(v * 9 + seed))
        row = []
        for j in range(sides):
            a = j / sides * math.tau
            if half and math.sin(a) < -0.05:
                a = math.atan2(-0.05, math.cos(a))
            # ridges run the length of the shell
            ridge = 1 + 0.035 * math.cos(a * 8)
            x = math.cos(a) * W * 0.5 * prof * ridge
            y = math.sin(a) * T * 0.5 * prof * ridge * (1 + 0.15 * math.cos(a))
            z = (v - 0.5) * L
            row.append(bm.verts.new((x, y, z)))
        verts.append(row)
    for i in range(rings):
        for j in range(sides):
            a, b = verts[i][j], verts[i][(j + 1) % sides]
            c, d = verts[i + 1][(j + 1) % sides], verts[i + 1][j]
            if len({a, b, c, d}) == 4:
                try:
                    bm.faces.new((a, b, c, d))
                except ValueError:
                    pass
    bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-6)
    # dents and a little twist, so no two seeds are the same
    for vtx in bm.verts:
        p = vtx.co * 400
        vtx.co += vtx.normal * 0.00012 * noise.noise(p + Vector((seed, 0, 0)))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    ob = mesh_object(("seedhalf_" if half else "seed_") + str(seed), bm)
    shade(ob, True, 50)
    # UV: u around the shell (stripes), v along it, so the shader can paint the stripes in UV space
    me = ob.data
    uv = me.uv_layers.new(name="UV")
    for loop in me.loops:
        co = me.vertices[loop.vertex_index].co
        uv.data[loop.index].uv = ((math.atan2(co.y, co.x) / math.tau) % 1.0, co.z / L + 0.5)
    return ob


# ------------------------------------------------------------------ berries: currants with skin, calyx and stalk
def berry(seed):
    rng = random.Random(seed)
    bm = bmesh.new()
    r = rng.uniform(0.0035, 0.0048)
    bmesh.ops.create_uvsphere(bm, u_segments=48, v_segments=30, radius=r)
    for v in bm.verts:
        n = v.co.normalized()
        lump = 1 + 0.04 * noise.noise(n * 3 + Vector((seed, seed, 0)))
        flat = 1 - 0.1 * max(0, -n.z)  # it sags on the soil
        dimple = 1 - 0.14 * math.exp(-((1 - n.z) / 0.05) ** 2)  # the calyx sits in a dimple at the crown
        v.co = n * r * lump * flat * dimple * Vector((1, rng.uniform(0.92, 1.05), rng.uniform(0.9, 1.0)))
    bmesh.ops.triangulate(bm, faces=bm.faces)
    ob = mesh_object(f"berry_{seed}", bm)
    shade(ob, True, 80)
    # the dried calyx: a small five-pointed star of withered sepals at the crown
    cb = bmesh.new()
    centre = cb.verts.new((0, 0, r * 0.9))
    ring = []
    for k in range(10):
        a = k / 10 * math.tau
        rr = r * (0.24 if k % 2 == 0 else 0.09)
        ring.append(cb.verts.new((math.cos(a) * rr, math.sin(a) * rr, r * (0.96 if k % 2 == 0 else 0.9))))
    for k in range(10):
        cb.faces.new((centre, ring[k], ring[(k + 1) % 10]))
    calyx = mesh_object(f"calyx_{seed}", cb)
    # a short broken stalk
    sb = bmesh.new()
    bmesh.ops.create_cone(sb, cap_ends=True, segments=8, radius1=r * 0.05, radius2=r * 0.035, depth=r * rng.uniform(0.4, 0.9))
    bmesh.ops.translate(sb, vec=Vector((0, 0, r * 1.25)), verts=sb.verts)
    bmesh.ops.rotate(sb, cent=Vector((0, 0, r)), matrix=Matrix.Rotation(rng.uniform(0.3, 0.8), 3, "X"), verts=sb.verts)
    stalk = mesh_object(f"stalk_{seed}", sb)
    # vertex colour marks the parts for the shader, set per piece before they are joined: skin 1, calyx and stalk 0
    for o, part in ((ob, 1.0), (calyx, 0.0), (stalk, 0.0)):
        col = o.data.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
        o.data.color_attributes.active_color = col
        for li in range(len(o.data.loops)):
            col.data[li].color = (part, part, part, 1)
    for o in (calyx, stalk):
        o.select_set(True)
    ob.select_set(True)
    bpy.context.view_layer.objects.active = ob
    bpy.ops.object.join()
    smart_uv(ob)
    return ob


# ------------------------------------------------------------------ blackberry: a cluster of glossy drupelets
def blackberry(seed):
    rng = random.Random(seed)
    r = rng.uniform(0.006, 0.0075)
    bm = bmesh.new()
    bmesh.ops.create_uvsphere(bm, u_segments=20, v_segments=12, radius=r * 0.8)
    core = mesh_object(f"core_{seed}", bm)
    parts = [core]
    n = 46
    for i in range(n):
        y = 1 - 2 * (i + 0.5) / n
        if y < -0.7:
            continue
        rr = math.sqrt(max(0, 1 - y * y)); th = i * 2.39996 + rng.uniform(-0.2, 0.2)
        d = Vector((math.cos(th) * rr, math.sin(th) * rr, y * 1.12))
        db = bmesh.new()
        dr = r * rng.uniform(0.24, 0.3)
        bmesh.ops.create_uvsphere(db, u_segments=10, v_segments=7, radius=dr)
        bmesh.ops.translate(db, vec=d * r * 0.88, verts=db.verts)
        parts.append(mesh_object(f"drupe_{seed}_{i}", db))
    for o in parts:
        o.select_set(True)
        for p in o.data.polygons:
            p.use_smooth = True
    bpy.context.view_layer.objects.active = core
    bpy.ops.object.join()
    col = core.data.color_attributes.new("Col", "BYTE_COLOR", "CORNER")
    core.data.color_attributes.active_color = col
    for li in range(len(core.data.loops)):
        col.data[li].color = (1, 1, 1, 1)
    smart_uv(core)
    return core


# ------------------------------------------------------------------ the formicarium tube: thick glass, broken
def tube():
    R, wall, L = 0.008, 0.0012, 0.15
    pieces = []
    # the closed half: a thick-walled tube with a rounded end, broken across with a jagged edge
    bpy.ops.mesh.primitive_cylinder_add(vertices=48, radius=R, depth=L, location=(0, 0, 0))
    outer = bpy.context.object
    bpy.ops.mesh.primitive_uv_sphere_add(segments=48, ring_count=24, radius=R, location=(0, 0, -L / 2))
    cap = bpy.context.object
    for o in (outer, cap):
        o.select_set(True)
    bpy.context.view_layer.objects.active = outer
    bpy.ops.object.join()
    sol = outer.modifiers.new("wall", "SOLIDIFY")
    sol.thickness = wall
    sol.offset = -1
    bpy.ops.object.modifier_apply(modifier="wall")
    # the fracture: a jagged cutter (a displaced, subdivided box) cuts the far end away
    bpy.ops.mesh.primitive_cube_add(size=1, location=(0, 0, L * 0.18 + 0.05))
    cut = bpy.context.object
    cut.scale = (0.05, 0.05, 0.1)
    bpy.ops.object.transform_apply(scale=True)
    sub = cut.modifiers.new("sub", "SUBSURF")
    sub.levels = 4
    sub.subdivision_type = "SIMPLE"
    bpy.ops.object.modifier_apply(modifier="sub")
    rng = random.Random(7)
    for v in cut.data.vertices:
        if v.co.z < -0.04:
            a = math.atan2(v.co.y, v.co.x)
            # conchoidal teeth: a few big steps and fine chips
            v.co.z += 0.012 * math.sin(a * 3 + 0.7) + 0.006 * math.sin(a * 7 + 1.3) + rng.uniform(-0.0015, 0.0015)
    boo = outer.modifiers.new("break", "BOOLEAN")
    boo.operation = "DIFFERENCE"
    boo.object = cut
    boo.solver = "EXACT"
    bpy.context.view_layer.objects.active = outer
    bpy.ops.object.modifier_apply(modifier="break")
    bpy.data.objects.remove(cut)
    outer.name = "tube_closed"
    shade(outer, True, 30)
    smart_uv(outer)
    pieces.append(outer)
    return pieces


def shard(seed):
    rng = random.Random(seed)
    R, wall = 0.008, 0.0012
    bm = bmesh.new()
    a0, a1 = rng.uniform(0, math.tau), 0
    span = rng.uniform(0.5, 1.4)
    h = rng.uniform(0.006, 0.02)
    pts = []
    n = 7
    for k in range(n):
        t = k / (n - 1)
        a = a0 + span * t
        top = h * (0.4 + 0.6 * math.sin(math.pi * t)) + rng.uniform(-0.002, 0.002)
        pts.append((a, max(0.002, top), rng.uniform(-0.0015, 0.0015)))
    rows = []
    for rad in (R, R - wall):
        row = []
        for (a, top, bot) in pts:
            row.append((bm.verts.new((math.cos(a) * rad, math.sin(a) * rad, bot)),
                        bm.verts.new((math.cos(a) * rad, math.sin(a) * rad, top))))
        rows.append(row)
    o, i = rows
    for k in range(n - 1):
        bm.faces.new((o[k][0], o[k + 1][0], o[k + 1][1], o[k][1]))
        bm.faces.new((i[k][1], i[k + 1][1], i[k + 1][0], i[k][0]))
        bm.faces.new((o[k][1], o[k + 1][1], i[k + 1][1], i[k][1]))
        bm.faces.new((i[k][0], i[k + 1][0], o[k + 1][0], o[k][0]))
    bm.faces.new((o[0][0], o[0][1], i[0][1], i[0][0]))
    bm.faces.new((i[-1][0], i[-1][1], o[-1][1], o[-1][0]))
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bmesh.ops.triangulate(bm, faces=bm.faces)
    ob = mesh_object(f"shard_{seed}", bm)
    shade(ob, False)
    smart_uv(ob)
    return ob


reset()
for s in range(6):
    export(f"sugar_{s}", [sugar(100 + s)])
reset()
for s in range(8):
    export(f"crumb_{s}", [crumb(700 + s)])
reset()
for s in range(4):
    export(f"seed_{s}", [seed_shell(200 + s)])
for s in range(2):
    export(f"seedhalf_{s}", [seed_shell(300 + s, half=True)])
reset()
for s in range(5):
    export(f"berry_{s}", [berry(400 + s)])
reset()
for s in range(3):
    export(f"blackberry_{s}", [blackberry(600 + s)])
reset()
export("tube_closed", tube())
reset()
for s in range(6):
    export(f"shard_{s}", [shard(500 + s)])
print("[blender] done")
