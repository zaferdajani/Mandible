"""The Stone Mother (target P7-B, docs/art-targets/README-scorpion.md): turns the generated scan of the scorpion into
the game's model. Run: blender --background --factory-startup --python tools/blender/scorpion_model.py -- <scan.glb> <out.glb>

The scan (Tripo image-to-3D from an isolated reference of the target's scorpion) lies along +X with Z up. The game's
creatures face +Z in Godot with Y up and stand on y = 0, one unit long, so this script: drops the scan's loose
floating crumbs, turns it to face Blender's -Y (Godot's +Z after the glTF export), stands it on the ground with the
middle of its body over the origin, and writes it back with its colour map.
"""
import bpy, bmesh, sys
from mathutils import Matrix, Vector
import math

src, out = sys.argv[sys.argv.index("--") + 1:][:2]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
ob = [o for o in bpy.data.objects if o.type == 'MESH'][0]
bpy.context.view_layer.objects.active = ob
ob.select_set(True)
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

bm = bmesh.new()
bm.from_mesh(ob.data)
# the scan is cut along its texture seams: weld them first (the colour map lives on the face corners and survives)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=1e-5)
bm.verts.ensure_lookup_table()
bm.verts.index_update()
seen, islands = set(), []
for v in bm.verts:
    if v.index in seen:
        continue
    stack, isle = [v], []
    seen.add(v.index)
    while stack:
        q = stack.pop()
        isle.append(q)
        for e in q.link_edges:
            o = e.other_vert(q)
            if o.index not in seen:
                seen.add(o.index)
                stack.append(o)
    islands.append(isle)
islands.sort(key=len, reverse=True)
print("[scorpion] islands:", len(islands), [len(i) for i in islands][:12])
main = islands[0]
lo = Vector((min(v.co.x for v in main), min(v.co.y for v in main), min(v.co.z for v in main)))
hi = Vector((max(v.co.x for v in main), max(v.co.y for v in main), max(v.co.z for v in main)))
drop = []
for isle in islands[1:]:
    c = sum((v.co for v in isle), Vector()) / len(isle)
    # crumbs the reconstruction left hanging in the air; the young on her back are part of the body or sit on it
    near = min((c - v.co).length for v in main[::7])
    if len(isle) < 80 and near > 0.035:
        drop.extend(isle)
print("[scorpion] dropped", len(drop), "vertices of floating crumbs")
bmesh.ops.delete(bm, geom=drop, context='VERTS')
# face -Y (claws were at +X), stand on z = 0, body over the origin
rot = Matrix.Rotation(-math.pi / 2, 4, 'Z')
bmesh.ops.transform(bm, matrix=rot, verts=bm.verts)
zmin = min(v.co.z for v in bm.verts)
ys = [v.co.y for v in bm.verts]
bmesh.ops.translate(bm, verts=bm.verts, vec=Vector((0, 0, -zmin)))
bm.to_mesh(ob.data)
bm.free()
ob.data.update()
xs = [v.co.x for v in ob.data.vertices]; ys = [v.co.y for v in ob.data.vertices]; zs = [v.co.z for v in ob.data.vertices]
print("[scorpion] bounds x", round(min(xs), 3), round(max(xs), 3), "y", round(min(ys), 3), round(max(ys), 3), "z", round(min(zs), 3), round(max(zs), 3),
      "triangles", sum(len(p.vertices) - 2 for p in ob.data.polygons))
bpy.ops.export_scene.gltf(filepath=out, export_format='GLB', use_selection=True, export_yup=True, export_apply=True)
