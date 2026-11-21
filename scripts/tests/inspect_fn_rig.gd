@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/fn 502/source/FPS_Anims.fbx")
	if packed:
		var inst = packed.instantiate()
		var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
		if skel:
			var bone_idx = skel.find_bone("Main")
			print("Bone Main idx: ", bone_idx)
			if bone_idx >= 0:
				print("Bone Main pose: ", skel.get_bone_pose(bone_idx))
				print("Bone Main global pose: ", skel.get_bone_global_pose(bone_idx))
		var wmesh = inst.find_child("502_1_low_001", true, false) as MeshInstance3D
		if wmesh:
			print("502_1_low_001 pos: ", wmesh.position, " rot: ", wmesh.rotation_degrees, " scale: ", wmesh.scale)
			print("502_1_low_001 aabb: ", wmesh.get_aabb())
		inst.free()
	quit()
