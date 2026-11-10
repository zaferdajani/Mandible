"""Turns a generated textured GLB (Higgsfield/Meshy image-to-3D) into the layout tools/asset-process.gd reads, plus the
three texture maps NatureAssets.MaterialFor expects.

Run: blender --background --factory-startup --python tools/blender/glb_to_asset.py -- <in.glb> <out-dir> <name> <tex-dir>
Writes <out-dir>/<name>/<name>.gltf (mesh only, its longest side along X, real size in metres as given by the
generator's unit cube) and <tex-dir>/<name>_diff.png, _nor.png and _mr.png (glTF metallic-roughness; the caller packs
it into _arm). Nothing is hand-modelled here: the shape and the surface come from the generated scan.
"""
import os
import sys

import bpy

args = sys.argv[sys.argv.index("--") + 1:]
SRC, OUT, NAME, TEX = args[0], args[1], args[2], args[3]

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=SRC)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
for o in bpy.context.scene.objects:
    o.select_set(o in meshes)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1:
    bpy.ops.object.join()
ob = bpy.context.view_layer.objects.active
bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
# the longest side along X (the game lays straws along local +X)
d = ob.dimensions
if d.y >= d.x and d.y >= d.z:
    ob.rotation_euler = (0, 0, 1.5708)
elif d.z >= d.x and d.z >= d.y:
    ob.rotation_euler = (0, 1.5708, 0)
bpy.ops.object.transform_apply(rotation=True)

os.makedirs(TEX, exist_ok=True)
# the maps straight out of the GLB's binary chunk (saving Blender's packed copies wrote empty images)
import io, json, struct
raw = open(SRC, "rb").read()
jl = struct.unpack("<I", raw[12:16])[0]
doc = json.loads(raw[20:20 + jl])
bl = struct.unpack("<I", raw[20 + jl:24 + jl])[0]
blob = raw[28 + jl:28 + jl + bl]
mat = doc["materials"][0]
pbr = mat.get("pbrMetallicRoughness", {})
for kind, tex in (("diff", pbr.get("baseColorTexture")), ("mr", pbr.get("metallicRoughnessTexture")), ("nor", mat.get("normalTexture"))):
    if not tex:
        continue
    img = doc["images"][doc["textures"][tex["index"]]["source"]]
    view = doc["bufferViews"][img["bufferView"]]
    start = view.get("byteOffset", 0)
    ext = ".png" if "png" in img.get("mimeType", "image/png") else ".jpg"
    with open(os.path.join(TEX, f"{NAME}_{kind}{ext}"), "wb") as f:
        f.write(blob[start:start + view["byteLength"]])
    print("[glb] saved", kind)

d = os.path.join(OUT, NAME)
os.makedirs(d, exist_ok=True)
bpy.ops.export_scene.gltf(filepath=os.path.join(d, NAME + ".gltf"), export_format="GLTF_SEPARATE", use_selection=True,
                          export_apply=True, export_normals=True, export_texcoords=True, export_materials="NONE",
                          export_vertex_color="NONE")  # the scan has no paint; an empty channel turns it black
print("[glb] wrote", NAME, tuple(round(v, 4) for v in ob.dimensions))
