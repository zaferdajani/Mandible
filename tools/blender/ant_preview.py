"""Look-dev sheet for the modelled ants: each mesh from the top, the side and the front, coloured by part, so the
anatomy can be checked before baking.

Run: blender --background --factory-startup --python tools/blender/ant_preview.py -- <bin-dir> <out.png> [names...]
<names> are bin basenames (default: every *_near.bin).
"""
import glob
import math
import os
import struct
import sys

import bpy
from mathutils import Vector

args = sys.argv[sys.argv.index("--") + 1:]
src, out = args[0], args[1]
names = args[2:] or sorted(os.path.basename(f)[:-4] for f in glob.glob(os.path.join(src, "*_near.bin")))
PALETTE = {0: (0.35, 0.3, 0.25), 12: (0.45, 0.35, 0.25), 13: (0.3, 0.22, 0.16), 11: (0.05, 0.3, 0.9),
           7: (0.9, 0.7, 0.2), 8: (0.9, 0.7, 0.2), 9: (0.9, 0.2, 0.2), 10: (0.9, 0.2, 0.2)}

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "VERTEX"
scene.render.resolution_x, scene.render.resolution_y = 600 * 3, 420 * len(names)
scene.render.film_transparent = False
world = bpy.data.worlds.new("w"); scene.world = world

views = [((math.pi / 2, 0, 0), "top"), ((0, 0, math.pi / 2), "side"), ((0, 0, 0), "front")]
for row, name in enumerate(names):
    with open(os.path.join(src, name + ".bin"), "rb") as fh:
        assert fh.read(4) == b"ANT1"
        nv, ni = struct.unpack("<II", fh.read(8))
        rows = [struct.unpack("<14f", fh.read(56)) for _ in range(nv)]
        idx = struct.unpack(f"<{ni}I", fh.read(4 * ni))
    # Godot (x, y up, z forward) -> Blender (x, -z, y): the ant faces -Y, so the side view sees its left flank
    verts = [(r[0], -r[2], r[1]) for r in rows]
    tris = [(idx[i], idx[i + 2], idx[i + 1]) for i in range(0, ni, 3)]
    for col, (rot, label) in enumerate(views):
        me = bpy.data.meshes.new(name)
        me.from_pydata(verts, [], tris)
        attr = me.color_attributes.new("c", "FLOAT_COLOR", "POINT")
        for k, r in enumerate(rows):
            part = int(round(r[6] * 255))
            c = (1, 0.85, 0.4) if r[11] > 0.5 else PALETTE.get(part, (0.25, 0.2, 0.18))
            if r[10] > 0.5:
                c = (0.9, 0.55, 0.1)
            attr.data[k].color = (c[0] * (0.4 + 0.6 * r[9]), c[1] * (0.4 + 0.6 * r[9]), c[2] * (0.4 + 0.6 * r[9]), 1)
        ob = bpy.data.objects.new(name, me)
        scene.collection.objects.link(ob)
        ob.rotation_euler = rot
        ob.location = ((col - 1) * 1.5, 0, -row * 1.1)
for p in bpy.data.meshes:
    for poly in p.polygons:
        poly.use_smooth = True
cam = bpy.data.cameras.new("c"); cam.type = "ORTHO"; cam.ortho_scale = 1.5 * 3
co = bpy.data.objects.new("c", cam); scene.collection.objects.link(co); scene.camera = co
co.location = (0, -10, -(len(names) - 1) * 0.55); co.rotation_euler = (math.pi / 2, 0, 0)
cam.ortho_scale = max(4.5, 1.1 * len(names) * 4.5 / 3)
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
