"""Look-dev renders of the mutation props (vertex colours, warm sun), one image per model, before Godot import.

Run: blender --background --factory-startup --python tools/blender/mutation_preview.py -- <models-dir> <out-dir>
"""
import math
import os
import sys

import bpy
from mathutils import Vector

src, out = sys.argv[sys.argv.index("--") + 1:][:2]
os.makedirs(out, exist_ok=True)

VIEWS = {  # model: (camera offset direction, distance factor, extra models)
    "gland": (Vector((0.6, -1.0, 0.8)), 2.2, []),
    "queen_wasp": (Vector((0.5, -1.0, 0.9)), 1.6, []),
    "web": (Vector((0.35, -1.0, 0.55)), 1.05, ["queen_wasp", "gland"]),
    "nursery": (Vector((0.2, -0.8, 1.2)), 0.95, []),
}


def material(name, ob):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    nt = mat.node_tree
    bsdf = nt.nodes["Principled BSDF"]
    if ob.data.color_attributes:
        attr = nt.nodes.new("ShaderNodeVertexColor")
        attr.layer_name = ob.data.color_attributes[0].name
        nt.links.new(attr.outputs["Color"], bsdf.inputs["Base Color"])
    bsdf.inputs["Roughness"].default_value = 0.45
    if "gland" in name:
        bsdf.inputs["Subsurface Weight"].default_value = 0.6
        bsdf.inputs["Emission Color"].default_value = (0.7, 0.5, 1.0, 1)
        bsdf.inputs["Emission Strength"].default_value = 0.6
        bsdf.inputs["Roughness"].default_value = 0.22
    if "wings" in name:
        bsdf.inputs["Alpha"].default_value = 0.3
        bsdf.inputs["Base Color"].default_value = (0.8, 0.6, 0.3, 1)
    if "silk" in name or "wrap" in name:
        bsdf.inputs["Roughness"].default_value = 0.25
    if "dew" in name:
        bsdf.inputs["Transmission Weight"].default_value = 1.0
        bsdf.inputs["Roughness"].default_value = 0.02
    ob.data.materials.clear()
    ob.data.materials.append(mat)


def load(name, offset=Vector()):
    before = set(bpy.data.objects)
    bpy.ops.import_scene.gltf(filepath=os.path.join(src, name, name + ".gltf"))
    objs = [o for o in bpy.data.objects if o not in before and o.type == "MESH"]
    for o in objs:
        o.location += offset
        material(o.name, o)
    return objs


for name, (dirn, dist, extra) in VIEWS.items():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.render.engine = "BLENDER_EEVEE_NEXT"
    scene.render.resolution_x, scene.render.resolution_y = 1344, 752
    world = bpy.data.worlds.new("w"); scene.world = world; world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.4, 0.25, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = 0.6
    objs = load(name)
    if name == "web":
        # the wasp hangs in the torn patch, tilted head-down; the gland sits in her gaster
        for o in load("queen_wasp"):
            o.rotation_euler = (math.radians(70), math.radians(-25), math.radians(15))
            o.location = Vector((1.6, -0.2, 7.0))
        for o in load("gland"):
            o.location = Vector((1.25, -0.6, 6.7)); o.scale = (0.8, 0.8, 0.8)
    # ground: a warm soil plane
    bpy.ops.mesh.primitive_plane_add(size=80)
    ground = bpy.context.active_object
    gm = bpy.data.materials.new("soil"); gm.use_nodes = True
    gm.node_tree.nodes["Principled BSDF"].inputs["Base Color"].default_value = (0.42, 0.28, 0.15, 1)
    ground.data.materials.append(gm)
    lo = Vector((1e9,) * 3); hi = Vector((-1e9,) * 3)
    for o in objs:
        for c in o.bound_box:
            w = o.matrix_world @ Vector(c); lo = Vector(map(min, lo, w)); hi = Vector(map(max, hi, w))
    centre = (lo + hi) / 2
    if name in ("gland", "queen_wasp"):
        ground.location.z = lo.z - 0.02
    size = max(hi - lo)
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN"))
    sun.data.energy = 4.5; sun.data.color = (1.0, 0.86, 0.66)
    sun.rotation_euler = (math.radians(55), 0, math.radians(-40))
    scene.collection.objects.link(sun)
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam)
    cam.data.lens = 50
    cam.location = centre + dirn.normalized() * size * dist * 1.6
    cam.rotation_euler = (centre - cam.location).to_track_quat("-Z", "Y").to_euler()
    scene.camera = cam
    scene.render.filepath = os.path.join(out, name + ".png")
    bpy.ops.render.render(write_still=True)
    print("[preview]", name)
