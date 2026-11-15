"""Look-dev sheet for baked ant meshes (ANT2 .bin from ant_models.py): each mesh from the side, the top and three-quarter
front, coloured the way the Ant shader will colour it (chitin, eyes, setae, silk, shell, bands), so a merged body can
be checked against its target before it goes into the game.

Run: blender --background --factory-startup --python tools/blender/ant_look.py -- <bin-dir> <out.png> <names...>
"""
import math
import os
import struct
import sys

import bpy

args = sys.argv[sys.argv.index("--") + 1:]
src, out, names = args[0], args[1], args[2:]
RED = os.environ.get("ANT_LOOK_RED", "solenopsis")

bpy.ops.wm.read_factory_settings(use_empty=True)
scene = bpy.context.scene
scene.render.engine = "BLENDER_WORKBENCH"
scene.display.shading.light = "STUDIO"
scene.display.shading.color_type = "VERTEX"
scene.display.shading.show_cavity = True
scene.display.shading.show_shadows = True
scene.render.resolution_x, scene.render.resolution_y = 640 * 3, 460 * len(names)
world = bpy.data.worlds.new("w")
scene.world = world
scene.display.shading.background_type = "VIEWPORT"
scene.display.shading.background_color = (0.32, 0.22, 0.14)


def colour(r, red):
    part = int(round(r[6] * 255))
    kind = r[15]
    base = (0.5, 0.16, 0.07) if red else (0.06, 0.05, 0.045)
    c = base
    if 1 <= part <= 8:
        c = (0.55, 0.22, 0.1) if red else (0.1, 0.075, 0.06)
    if part == 11:
        c = (0.02, 0.02, 0.02)
    if r[14] > 0.5:
        c = (0.75, 0.48, 0.14)
    elif r[14] > 0.1:
        c = (0.5, 0.4, 0.3)
    if 0.5 < kind < 1.5:
        c = (0.55, 0.42, 0.24)
    elif kind < 2.5 and kind > 1.5:
        c = (0.95, 0.93, 0.88)
    elif kind < 3.5 and kind > 2.5:
        g = 0.55 + 0.45 * abs(math.sin(math.pi * r[10]))
        c = (0.2 * g, 0.12 * g, 0.06 * g)
        if red:
            c = (0.36 * g, 0.15 * g, 0.06 * g)
    elif kind < 4.5 and kind > 3.5:
        band = (r[10] * 5.0 + 0.1) % 1.0
        c = (0.85, 0.5, 0.06) if band > 0.5 and r[10] < 0.93 else (0.03, 0.025, 0.02)
    elif kind < 5.5 and kind > 4.5:
        c = (0.09, 0.075, 0.065) if not red else (0.4, 0.15, 0.07)
    elif kind > 5.5:
        c = (0.5, 0.04, 0.03)
    ao = 0.45 + 0.55 * r[9]
    return (c[0] * ao, c[1] * ao, c[2] * ao, 1)


views = [((0, 0, math.pi / 2), "side"), ((math.pi / 2, 0, 0), "top"), ((math.radians(18), 0, math.radians(-38)), "front")]
for row, name in enumerate(names):
    with open(os.path.join(src, name + ".bin"), "rb") as fh:
        assert fh.read(4) == b"ANT2"
        nv, ni = struct.unpack("<II", fh.read(8))
        rows = [struct.unpack("<22f", fh.read(88)) for _ in range(nv)]
        idx = struct.unpack(f"<{ni}I", fh.read(4 * ni))
    red = name.startswith(RED)
    verts = [(r[0], -r[2], r[1]) for r in rows]
    tris = [(idx[i], idx[i + 2], idx[i + 1]) for i in range(0, ni, 3)]
    for col, (rot, label) in enumerate(views):
        me = bpy.data.meshes.new(name)
        me.from_pydata(verts, [], tris)
        attr = me.color_attributes.new("c", "FLOAT_COLOR", "POINT")
        for k, r in enumerate(rows):
            attr.data[k].color = colour(r, red)
        for poly in me.polygons:
            poly.use_smooth = True
        ob = bpy.data.objects.new(name, me)
        scene.collection.objects.link(ob)
        ob.rotation_euler = rot
        ob.location = ((col - 1) * 1.6, 0, -row * 1.15 + (0.0 if label != "top" else 0.15))
cam = bpy.data.cameras.new("c")
cam.type = "ORTHO"
co = bpy.data.objects.new("c", cam)
scene.collection.objects.link(co)
scene.camera = co
co.location = (0, -10, -(len(names) - 1) * 0.575 + 0.18)
co.rotation_euler = (math.pi / 2, 0, 0)
cam.ortho_scale = max(4.8, 1.15 * len(names) * 4.8 / 3 * (640 * 3) / (460 * 3) * 0.72)
scene.render.filepath = out
bpy.ops.render.render(write_still=True)
