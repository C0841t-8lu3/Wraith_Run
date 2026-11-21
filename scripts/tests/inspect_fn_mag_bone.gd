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
				if "mag" in bname.to_lower() or "clip" in bname.to_lower() or "bullet" in bname.to_lower() or "extra" in bname.to_lower():
					print("Bone [", i, "] '", bname, "'")
		inst.free()
	quit()
