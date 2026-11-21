@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/sledge hammer/source/FPS_Anims.glb")
	if packed:
		var inst = packed.instantiate()
		get_root().add_child(inst)
		var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
		var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
		ap.play("idle")
		ap.advance(0.1)
		print("After advance 0.1:")
		for b in ["upper_arm.L_01", "hand.L_03", "hand.R_024"]:
			var idx = skel.find_bone(b)
			if idx >= 0:
				print("Bone '", b, "' pose origin: ", skel.get_bone_global_pose(idx).origin)
		var wmesh = inst.find_child("SledgeHammer_SledgeHammer_0", true, false) as MeshInstance3D
		if wmesh:
			print("SledgeHammer_SledgeHammer_0 global transform origin: ", wmesh.global_transform.origin)
		inst.free()
	quit()
