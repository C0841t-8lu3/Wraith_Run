@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/sledge hammer/source/FPS_Anims.glb")
	if packed:
		var inst = packed.instantiate()
		var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
		var idx = skel.find_bone("hand.R_024")
		if idx >= 0:
			print("hand.R_024 rest: ", skel.get_bone_rest(idx))
			print("hand.R_024 parent: ", skel.get_bone_name(skel.get_bone_parent(idx)))
			for i in range(skel.get_bone_count()):
				if skel.get_bone_parent(i) == idx:
					print("  child bone: ", skel.get_bone_name(i), " rest origin: ", skel.get_bone_rest(i).origin)
		inst.free()
	quit()
