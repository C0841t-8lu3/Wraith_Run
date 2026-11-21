@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/fn 502/source/FPS_Anims.fbx")
	if packed:
		var inst = packed.instantiate()
		var anim_player = inst.find_child("AnimationPlayer", true, false)
		if anim_player:
			for anim in anim_player.get_animation_list():
				var a = anim_player.get_animation(anim)
				print("Clip: ", anim, " length: ", a.length)
		inst.free()
	quit()
