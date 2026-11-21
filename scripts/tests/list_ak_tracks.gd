@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
	
	var a = ap.get_animation("Rig|AK_Idle")
	print("Track count in Idle: ", a.get_track_count())
	for i in range(a.get_track_count()):
		print("  ", a.track_get_path(i), " (type: ", a.track_get_type(i), ")")

	inst.free()
	quit(0)
