@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/P90/source/FPS_Animated_P90.glb")
	if packed:
		var inst = packed.instantiate()
		for child in inst.get_children():
			if child is Camera3D:
				print("P90 Camera: ", child.name, " pos: ", child.position, " rot: ", child.rotation_degrees)
		var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
		if skel:
			print("P90 bone count: ", skel.get_bone_count())
			for i in range(skel.get_bone_count()):
				var bname = skel.get_bone_name(i)
				if "cam" in bname.to_lower() or "root" in bname.to_lower():
					print("P90 Bone: ", bname, " pose: ", skel.get_bone_global_pose(i))
		inst.free()
	quit()
