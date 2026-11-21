@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D

	var a = ap.get_animation("Rig|AK_Reload")
	print("Animated bones in Rig|AK_Reload:")
	for i in range(a.get_track_count()):
		var path = str(a.track_get_path(i))
		if "Skeleton3D:" in path:
			var bname = path.split("Skeleton3D:")[1]
			var bidx = skel.find_bone(bname)
			print("  Track ", i, ": ", bname, " (bone idx: ", bidx, ") type: ", a.track_get_type(i))

	inst.free()
	quit(0)
