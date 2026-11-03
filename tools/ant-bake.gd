## Turns the ant meshes modelled by tools/blender/ant_models.py into Godot mesh resources.
## Run: godot --headless --path . --script res://tools/ant-bake.gd -- <bin-dir> <out-dir>
## Reads every <species>_<type>_<lod>.bin and manifest.json, writes <out-dir>/<species>_<type>_<lod>.res: one surface
## with the vertex colour, UV and UV2 channels the Ant shader animates from, and the manifest's neck and gaster tip as
## metadata (AntMesh reads them back for the head's turning point and the acid's origin).
extends SceneTree

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	var src: String = args[0]
	var out: String = args[1]
	DirAccess.make_dir_recursive_absolute(out)
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(src.path_join("manifest.json")))
	for name in manifest.keys():
		var f := FileAccess.open(src.path_join(name + ".bin"), FileAccess.READ)
		if f == null or f.get_buffer(4).get_string_from_ascii() != "ANT1":
			push_error("cannot read " + name)
			continue
		var nv := f.get_32()
		var ni := f.get_32()
		var pos := PackedVector3Array(); pos.resize(nv)
		var nor := PackedVector3Array(); nor.resize(nv)
		var col := PackedColorArray(); col.resize(nv)
		var uv := PackedVector2Array(); uv.resize(nv)
		var uv2 := PackedVector2Array(); uv2.resize(nv)
		for i in nv:
			pos[i] = Vector3(f.get_float(), f.get_float(), f.get_float())
			nor[i] = Vector3(f.get_float(), f.get_float(), f.get_float())
			col[i] = Color(f.get_float(), f.get_float(), f.get_float(), f.get_float())
			uv[i] = Vector2(f.get_float(), f.get_float())
			uv2[i] = Vector2(f.get_float(), f.get_float())
		var idx := PackedInt32Array(); idx.resize(ni)
		for i in ni:
			idx[i] = f.get_32()
		var arrays := []
		arrays.resize(Mesh.ARRAY_MAX)
		arrays[Mesh.ARRAY_VERTEX] = pos
		arrays[Mesh.ARRAY_NORMAL] = nor
		arrays[Mesh.ARRAY_COLOR] = col
		arrays[Mesh.ARRAY_TEX_UV] = uv
		arrays[Mesh.ARRAY_TEX_UV2] = uv2
		arrays[Mesh.ARRAY_INDEX] = idx
		var mesh := ArrayMesh.new()
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES, arrays)
		var m: Dictionary = manifest[name]
		mesh.set_meta("neck", Vector3(m["neck"][0], m["neck"][1], m["neck"][2]))
		mesh.set_meta("gaster_tip", Vector3(m["gaster_tip"][0], m["gaster_tip"][1], m["gaster_tip"][2]))
		ResourceSaver.save(mesh, out.path_join(name + ".res"), ResourceSaver.FLAG_COMPRESS)
		print("[ant-bake] %s: %d vertices, %d triangles" % [name, nv, ni / 3])
	quit()
