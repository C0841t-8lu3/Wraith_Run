@tool
extends SceneTree

func _init():
	print("--- Inspecting AK Walk and Run motions ---")
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	var pbody_idx = skel.find_bone("PBody")
	var hand_r_idx = skel.find_bone("Hand_R")

	for clip_name in ["Rig|AK_Idle", "Rig|AK_Walk", "Rig|AK_Run"]:
		var a = ap.get_animation(clip_name)
		print("\n=== ", clip_name, " (length: ", a.length, "s) ===")
		var pbody_min = Vector3(999, 999, 999)
		var pbody_max = Vector3(-999, -999, -999)
		var hand_r_min = Vector3(999, 999, 999)
		var hand_r_max = Vector3(-999, -999, -999)

		ap.play(clip_name)
		var steps = 20
		for step in range(steps + 1):
			var t = (a.length / float(steps)) * float(step)
			ap.seek(t, true)
			skel.force_update_transform()
			var p_pbody = skel.get_bone_pose(pbody_idx).origin
			var p_hand_r = skel.get_bone_pose(hand_r_idx).origin
			pbody_min = pbody_min.min(p_pbody)
			pbody_max = pbody_max.max(p_pbody)
			hand_r_min = hand_r_min.min(p_hand_r)
			hand_r_max = hand_r_max.max(p_hand_r)

		print("  PBody range: ", pbody_max - pbody_min, " (min=", pbody_min, " max=", pbody_max, ")")
		print("  Hand_R range: ", hand_r_max - hand_r_min, " (min=", hand_r_min, " max=", hand_r_max, ")")

	inst.free()
	quit(0)
