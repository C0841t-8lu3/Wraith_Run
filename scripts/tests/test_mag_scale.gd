@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	var pmag_idx = skel.find_bone("Pmag")
	var pmag2_idx = skel.find_bone("Pmag2")

	print("Pmag bone idx: ", pmag_idx)
	print("Pmag2 bone idx: ", pmag2_idx)

	# Test setting scale
	skel.set_bone_pose_scale(pmag_idx, Vector3.ZERO)
	skel.set_bone_pose_scale(pmag2_idx, Vector3.ZERO)
	print("Set both to ZERO successfully")

	inst.free()
	quit(0)
