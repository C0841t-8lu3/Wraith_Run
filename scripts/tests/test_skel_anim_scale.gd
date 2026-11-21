@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	root.add_child(inst)

	var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	var pmag_idx = skel.find_bone("Pmag")
	var pmag2_idx = skel.find_bone("Pmag2")

	ap.play("Rig|AK_Reload")
	ap.seek(1.4, true)

	print("Before setting scale: Pmag scale=", skel.get_bone_pose(pmag_idx).basis.get_scale())
	skel.set_bone_pose_scale(pmag_idx, Vector3.ZERO)
	print("Immediately after set_bone_pose_scale: Pmag scale=", skel.get_bone_pose(pmag_idx).basis.get_scale())

	# Wait a frame
	await process_frame
	print("After 1 frame without re-setting: Pmag scale=", skel.get_bone_pose(pmag_idx).basis.get_scale())

	inst.free()
	quit(0)
