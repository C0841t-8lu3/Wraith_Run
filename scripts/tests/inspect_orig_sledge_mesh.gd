@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/sledge hammer/source/FPS_Anims.glb")
	if packed:
		var inst = packed.instantiate()
		var mesh_inst = inst.find_child("SledgeHammer_SledgeHammer_0", true, false) as MeshInstance3D
		if mesh_inst and mesh_inst.mesh:
			var m = mesh_inst.mesh
			var arrays = m.surface_get_arrays(0)
			var verts = arrays[Mesh.ARRAY_VERTEX] as PackedVector3Array
			var min_v = Vector3(1e9, 1e9, 1e9)
			var max_v = Vector3(-1e9, -1e9, -1e9)
			for v in verts:
				min_v = min_v.min(v)
				max_v = max_v.max(v)
			print("SledgeHammer_0 Min vert: ", min_v, " Max vert: ", max_v, " Size: ", max_v - min_v)
		inst.free()
	quit()
