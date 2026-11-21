@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	for i in range(skel.get_bone_count()):
		var bname = skel.get_bone_name(i)
		var pidx = skel.get_bone_parent(i)
		var pname = skel.get_bone_name(pidx) if pidx >= 0 else "ROOT"
		if "mag" in bname.to_lower() or "pmag" in bname.to_lower() or i in [58, 59, 60, 61, 62, 63, 80, 81, 82]:
			print("Bone [", i, "] '", bname, "' parent: [", pidx, "] '", pname, "'")

	inst.free()
	quit(0)
