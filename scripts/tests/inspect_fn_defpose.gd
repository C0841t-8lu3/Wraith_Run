@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/fn 502/source/FPS_Anims.fbx")
	if packed:
		var inst = packed.instantiate()
		var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
		var anim = ap.get_animation("Armature|FN_DefPose")
		if anim:
			print("FN_DefPose track count: ", anim.get_track_count())
			for t in range(anim.get_track_count()):
				var path = anim.track_get_path(t)
				if "mag" in str(path).to_lower():
					print("Track: ", path, " key 0 val: ", anim.track_get_key_value(t, 0))
		inst.free()
	quit()
