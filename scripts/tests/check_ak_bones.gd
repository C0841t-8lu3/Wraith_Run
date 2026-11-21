@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	var ak_mesh = inst.find_child("Ak", true, false) as MeshInstance3D

	var mdt = MeshDataTool.new()
	mdt.create_from_surface(ak_mesh.mesh, 0)
	print("Ak vertex count: ", mdt.get_vertex_count())
	var bone_usage = {}
	for i in range(mdt.get_vertex_count()):
		var bones = mdt.get_vertex_bones(i)
		var weights = mdt.get_vertex_weights(i)
		for j in range(4):
			if weights[j] > 0.01:
				var b = bones[j]
				bone_usage[b] = bone_usage.get(b, 0) + 1

	for b in bone_usage.keys():
		print("Ak influenced by bone [", b, "] '", skel.get_bone_name(b), "': ", bone_usage[b], " verts")

	inst.free()
	quit(0)
