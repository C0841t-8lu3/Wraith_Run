@tool
extends SceneTree

func _init():
	print("--- Inspecting AK Skin Resources ---")
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	var mag_mesh = inst.find_child("Mag_2", true, false) as MeshInstance3D
	var ak_mesh = inst.find_child("Ak", true, false) as MeshInstance3D
	var arms_mesh = inst.find_child("arms", true, false) as MeshInstance3D
	var anim_player = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
	print("Found AnimationPlayer: ", anim_player != null)
	if anim_player:
		var ak_skin = ak_mesh.skin
		for i in range(ak_skin.get_bind_count()):
			var bidx = ak_skin.get_bind_bone(i)
			var bname = ak_skin.get_bind_name(i)
			print("Ak bind ", i, ": name='", bname, "' bone_index=", bidx, " skel_bone=", (skel.get_bone_name(bidx) if bidx >= 0 else "NONE"))

	inst.free()
	quit(0)
