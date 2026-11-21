@tool
extends SceneTree

func _init():
	var packed_rig = load("res://art/animations/player/combat/sledge hammer/source/FPS_Anims.glb")
	var packed_hammer = load("res://art/weapons/Melee/SledgeHammer.glb")
	if packed_rig and packed_hammer:
		var inst = packed_rig.instantiate()
		get_root().add_child(inst)
		var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
		var idx = skel.find_bone("hand.R_024")
		var hammer = packed_hammer.instantiate()
		var attach = BoneAttachment3D.new()
		attach.bone_name = "hand.R_024"
		skel.add_child(attach)
		attach.add_child(hammer)

		var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
		ap.play("idle")
		ap.advance(0.1)

		print("Rig attached hammer OK. Global hammer pos: ", hammer.global_position)
		inst.free()
	quit()
