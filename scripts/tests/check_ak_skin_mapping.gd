@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	var ak_mesh = inst.find_child("Ak", true, false) as MeshInstance3D

	print("Skeleton bone count: ", skel.get_bone_count())
	print("Ak Skin: ", ak_mesh.skin)
	var skin = ak_mesh.skin
	for i in range(skin.get_bind_count()):
		var bname = skin.get_bind_name(i)
		var bbone = skin.get_bind_bone(i)
		var resolved = skel.find_bone(bname) if bname != StringName() else bbone
		if "mag" in str(bname).to_lower() or i in [62, 63]:
			print("Skin bind [", i, "] name='", bname, "' bone_idx=", bbone, " -> resolved in skel: [", resolved, "] '", skel.get_bone_name(resolved) if resolved >= 0 else "NONE", "'")

	inst.free()
	quit(0)
