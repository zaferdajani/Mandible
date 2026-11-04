"""Renders a quick look-dev sheet of every glTF in a folder (one tile each), to check shapes before import.

Run: blender --background --factory-startup --python tools/blender/preview.py -- <models-dir> <out.png>
"""
import glob
import math
import os
import sys

import bpy
from mathutils import Vector

src, out = sys.argv[sys.argv.index("--") + 1:][:2]
files = sorted(glob.glob(os.path.join(src, "*", "*.gltf")))
bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_EEVEE_NEXT"
scene.render.resolution_x, scene.render.resolution_y = 1600, 1000
world = bpy.data.worlds.new("w"); scene.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[0].default_value = (0.35, 0.27, 0.2, 1)
cols = 6
for k, f in enumerate(files):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=f)
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c); lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    size = max(hi - lo)
    for o in objs:
        o.location = (o.location - (lo + hi) / 2) / size + Vector(((k % cols) * 1.3, -(k // cols) * 1.3, 0))
        o.scale = o.scale / size
        mat = bpy.data.materials.new("m"); mat.use_nodes = True
        bsdf = mat.node_tree.nodes["Principled BSDF"]
        name = os.path.basename(f)
        bsdf.inputs["Base Color"].default_value = (0.9, 0.9, 0.88, 1) if "sugar" in name else (0.15, 0.13, 0.1, 1) if "seed" in name else (0.55, 0.05, 0.05, 1) if "berry" in name else (0.8, 0.9, 0.85, 1)
        bsdf.inputs["Roughness"].default_value = 0.4
        o.data.materials.clear(); o.data.materials.append(mat)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam)
rows = (len(files) + cols - 1) // cols
cam.location = ((cols - 1) * 0.65, -(rows - 1) * 0.65 - 3.2, 5.5)
cam.rotation_euler = (math.radians(32), 0, 0)
cam.data.lens = 28
scene.camera = cam
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); scene.collection.objects.link(sun)
sun.rotation_euler = (math.radians(40), math.radians(20), math.radians(30)); sun.data.energy = 4
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
print("[blender] preview", len(files))
