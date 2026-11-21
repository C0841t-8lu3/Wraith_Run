@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/sledge hammer/source/FPS_Anims.glb")
	if packed:
		var inst = packed.instantiate()
		get_root().add_child(inst)
		var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
		var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
		var idx = skel.find_bone("upper_arm.L_01")
		ap.play("idle")
		ap.advance(0.1)
		print("Scale after anim: ", skel.get_bone_pose_scale(idx))
		inst.free()
	quit()
