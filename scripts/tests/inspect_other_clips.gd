@tool
extends SceneTree

func list_clips(path: String):
	var packed = load(path)
	if packed:
		var inst = packed.instantiate()
		var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
		if ap:
			print("=== Clips in ", path, " ===")
			for anim in ap.get_animation_list():
				var a = ap.get_animation(anim)
				print("  ", anim, " len: ", a.length)
		inst.free()

func _init():
	list_clips("res://art/animations/player/combat/M4/source/m4_assult_rfile.fbx")
	list_clips("res://art/animations/player/combat/Vector/source/Vector.fbx")
	list_clips("res://art/animations/player/combat/P90/source/FPS_Animated_P90.glb")
	quit()
