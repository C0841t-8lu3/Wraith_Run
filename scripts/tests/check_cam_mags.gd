@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var cam = inst.find_child("Camera", true, false) as Camera3D
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	var pmag_idx = skel.find_bone("Pmag")
	var pmag2_idx = skel.find_bone("Pmag2")

	var pose1 = skel.get_bone_global_pose(pmag_idx)
	var pose2 = skel.get_bone_global_pose(pmag2_idx)
	var cam_inv = cam.global_transform.affine_inverse() if cam else Transform3D.IDENTITY
	print("Pmag in Cam space: ", cam_inv * (skel.global_transform * pose1).origin)
	print("Pmag2 in Cam space: ", cam_inv * (skel.global_transform * pose2).origin)

	inst.free()
	quit(0)
