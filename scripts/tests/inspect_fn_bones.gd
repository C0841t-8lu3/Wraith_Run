@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/fn 502/source/FPS_Anims.fbx")
	if packed:
		var inst = packed.instantiate()
		var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
		if skel:
			for i in range(skel.get_bone_count()):
				var bname = skel.get_bone_name(i)
				var parent_idx = skel.get_bone_parent(i)
				var pname = skel.get_bone_name(parent_idx) if parent_idx >= 0 else "none"
				if "hand" in bname.to_lower() or "gun" in bname.to_lower() or "main" in bname.to_lower() or "weapon" in bname.to_lower():
					print("Bone [", i, "] '", bname, "' parent: '", pname, "' gpose: ", skel.get_bone_global_pose(i).origin)
		inst.free()
	quit()
