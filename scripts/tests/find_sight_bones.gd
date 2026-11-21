@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var ak_mesh_node = inst.find_child("Ak", true, false) as MeshInstance3D
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	var mesh = ak_mesh_node.mesh
	var mdt = MeshDataTool.new()
	mdt.create_from_surface(mesh, 0)
	var vcount = mdt.get_vertex_count()

	# Find exact front sight vertex (near y=-0.00823, z=0.002031)
	# and rear sight vertex (near y=-0.002905, z=0.002019)
	var fs_idx = -1
	var rs_idx = -1
	var min_fs_dist = 999.0
	var min_rs_dist = 999.0
	var target_fs = Vector3(0.0, -0.00823, 0.002031)
	var target_rs = Vector3(0.0, -0.002905, 0.002019)

	for i in range(vcount):
		var v = mdt.get_vertex(i)
		var d_fs = v.distance_to(target_fs)
		if d_fs < min_fs_dist:
			min_fs_dist = d_fs
			fs_idx = i
		var d_rs = v.distance_to(target_rs)
		if d_rs < min_rs_dist:
			min_rs_dist = d_rs
			rs_idx = i

	print("Front sight vertex [", fs_idx, "]: ", mdt.get_vertex(fs_idx), " dist=", min_fs_dist)
	print("Rear sight vertex [", rs_idx, "]: ", mdt.get_vertex(rs_idx), " dist=", min_rs_dist)

	# Check bone weights
	print("Front sight bones: ", mdt.get_vertex_bones(fs_idx), " weights: ", mdt.get_vertex_weights(fs_idx))
	print("Rear sight bones: ", mdt.get_vertex_bones(rs_idx), " weights: ", mdt.get_vertex_weights(rs_idx))

	for b in mdt.get_vertex_bones(fs_idx):
		print("  FS bone [", b, "] = ", skel.get_bone_name(b))
	for b in mdt.get_vertex_bones(rs_idx):
		print("  RS bone [", b, "] = ", skel.get_bone_name(b))

	inst.free()
	quit(0)
