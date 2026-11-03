## Cuts CC0 photoscans down to phone budgets and stores them as Godot meshes with LODs.
## Run: godot --headless --path . --script res://tools/asset-process.gd -- <src-dir> <out-dir> <manifest.json>
## The manifest lists, per source asset: {"src": "namaqualand_stones_01", "tris": 900, "kind": "rock"}.
## Every mesh node of a source becomes its own piece: re-centred on its footprint with its base at y = 0, its real
## size (metres) recorded, decimated with Godot's own simplifier to `tris`, then given an LOD chain for distance.
## Writes <out-dir>/<src>_<n>.res plus <out-dir>/pieces.json, which the scatter reads.
extends SceneTree

func _init() -> void:
	var args := OS.get_cmdline_user_args()
	var src_dir: String = args[0]
	var out_dir: String = args[1]
	var manifest: Array = JSON.parse_string(FileAccess.get_file_as_string(args[2]))
	DirAccess.make_dir_recursive_absolute(out_dir)
	var pieces := []
	for entry in manifest:
		var name: String = entry["src"]
		var path := src_dir.path_join(name).path_join(name + ".gltf")
		var doc := GLTFDocument.new()
		var state := GLTFState.new()
		if doc.append_from_file(path, state) != OK:
			push_error("cannot read " + path)
			continue
		var root: Node = doc.generate_scene(state)
		var n := 0
		for mi in _meshes(root):
			var arrays := _baked(mi)
			if arrays.is_empty():
				continue
			var reduced := _decimate(arrays, int(entry["tris"]))
			var size := _recentre(reduced)
			if entry.get("radial_normals", false):
				_radial_normals(reduced) # round fruit: smooth normals from the centre, whatever the decimation left
			var im := ImporterMesh.new()
			im.add_surface(Mesh.PRIMITIVE_TRIANGLES, reduced)
			im.generate_lods(25.0, 60.0, [])
			var mesh: ArrayMesh = im.get_mesh()
			var file := "%s_%d.res" % [name, n]
			ResourceSaver.save(mesh, out_dir.path_join(file), ResourceSaver.FLAG_COMPRESS)
			var tris := (reduced[Mesh.ARRAY_INDEX] as PackedInt32Array).size() / 3
			pieces.append({"file": file, "src": name, "kind": entry["kind"], "size": [size.x, size.y, size.z], "tris": tris})
			print("[assets] %s  %d tris  %.3f x %.3f x %.3f m" % [file, tris, size.x, size.y, size.z])
			n += 1
		root.free()
	var f := FileAccess.open(out_dir.path_join("pieces.json"), FileAccess.WRITE)
	f.store_string(JSON.stringify(pieces, "\t"))
	f.close()
	quit()

func _meshes(n: Node) -> Array:
	var out := []
	if n is MeshInstance3D:
		out.append(n)
	for c in n.get_children():
		out += _meshes(c)
	return out

## One surface with the node's transform baked in (sets place each stone away from the origin).
func _baked(mi: MeshInstance3D) -> Array:
	var mesh: Mesh = mi.mesh
	if mesh == null or mesh.get_surface_count() == 0:
		return []
	var xf := Transform3D.IDENTITY
	var p: Node = mi
	while p != null and p is Node3D:
		xf = (p as Node3D).transform * xf
		p = p.get_parent()
	var a: Array = mesh.surface_get_arrays(0)
	var v: PackedVector3Array = a[Mesh.ARRAY_VERTEX]
	var nr: PackedVector3Array = a[Mesh.ARRAY_NORMAL]
	var t: PackedFloat32Array = a[Mesh.ARRAY_TANGENT] if a[Mesh.ARRAY_TANGENT] != null else PackedFloat32Array()
	var basis := xf.basis
	for i in v.size():
		v[i] = xf * v[i]
		nr[i] = (basis * nr[i]).normalized()
	for i in range(0, t.size(), 4):
		var tv := (basis * Vector3(t[i], t[i + 1], t[i + 2])).normalized()
		t[i] = tv.x; t[i + 1] = tv.y; t[i + 2] = tv.z
	var out := []
	out.resize(Mesh.ARRAY_MAX)
	out[Mesh.ARRAY_VERTEX] = v
	out[Mesh.ARRAY_NORMAL] = nr
	if t.size() > 0:
		out[Mesh.ARRAY_TANGENT] = t
	out[Mesh.ARRAY_TEX_UV] = a[Mesh.ARRAY_TEX_UV]
	out[Mesh.ARRAY_COLOR] = a[Mesh.ARRAY_COLOR] # vertex colours mark parts (a berry's skin vs its calyx)
	out[Mesh.ARRAY_INDEX] = a[Mesh.ARRAY_INDEX]
	return out

## Godot's mesh simplifier (the one behind import LODs): take the first LOD at or under the budget, then drop the
## vertices it no longer uses.
func _decimate(a: Array, budget: int) -> Array:
	var idx: PackedInt32Array = a[Mesh.ARRAY_INDEX]
	if idx.size() / 3 <= budget:
		return a
	var im := ImporterMesh.new()
	im.add_surface(Mesh.PRIMITIVE_TRIANGLES, a)
	im.generate_lods(25.0, 60.0, [])
	var best := idx
	for l in im.get_surface_lod_count(0):
		var li := im.get_surface_lod_indices(0, l)
		best = li
		if li.size() / 3 <= budget:
			break
	var remap := {}
	var out := []
	out.resize(Mesh.ARRAY_MAX)
	var nv := PackedVector3Array(); var nn := PackedVector3Array(); var nuv := PackedVector2Array(); var nt := PackedFloat32Array()
	var v: PackedVector3Array = a[Mesh.ARRAY_VERTEX]
	var n: PackedVector3Array = a[Mesh.ARRAY_NORMAL]
	var uv: PackedVector2Array = a[Mesh.ARRAY_TEX_UV]
	var t = a[Mesh.ARRAY_TANGENT]
	var col = a[Mesh.ARRAY_COLOR]
	var nc := PackedColorArray()
	var ni := PackedInt32Array()
	ni.resize(best.size())
	for i in best.size():
		var o: int = best[i]
		if not remap.has(o):
			remap[o] = nv.size()
			nv.append(v[o]); nn.append(n[o]); nuv.append(uv[o])
			if col != null:
				nc.append(col[o])
			if t != null:
				nt.append_array([t[o * 4], t[o * 4 + 1], t[o * 4 + 2], t[o * 4 + 3]])
		ni[i] = remap[o]
	out[Mesh.ARRAY_VERTEX] = nv
	out[Mesh.ARRAY_NORMAL] = nn
	out[Mesh.ARRAY_TEX_UV] = nuv
	if t != null:
		out[Mesh.ARRAY_TANGENT] = nt
	if col != null:
		out[Mesh.ARRAY_COLOR] = nc
	out[Mesh.ARRAY_INDEX] = ni
	return out

## Centre on the footprint, base at y = 0. Returns the size in metres.
func _recentre(a: Array) -> Vector3:
	var v: PackedVector3Array = a[Mesh.ARRAY_VERTEX]
	var lo := v[0]; var hi := v[0]
	for p in v:
		lo = lo.min(p); hi = hi.max(p)
	var off := Vector3((lo.x + hi.x) * 0.5, lo.y, (lo.z + hi.z) * 0.5)
	for i in v.size():
		v[i] -= off
	a[Mesh.ARRAY_VERTEX] = v
	return hi - lo

## Normals pointing out from the piece's middle: for round fruit, so decimation never leaves it faceted.
func _radial_normals(a: Array) -> void:
	var v: PackedVector3Array = a[Mesh.ARRAY_VERTEX]
	var n: PackedVector3Array = a[Mesh.ARRAY_NORMAL]
	var lo := v[0]; var hi := v[0]
	for p in v:
		lo = lo.min(p); hi = hi.max(p)
	var c := (lo + hi) * 0.5
	for i in v.size():
		var r := (v[i] - c).normalized()
		n[i] = r if n[i].dot(r) > 0.2 else n[i] # keep the calyx and stalk's own normals
	a[Mesh.ARRAY_NORMAL] = n
