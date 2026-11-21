@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D

	var pmag_idx = skel.find_bone("Pmag")
	var pmag2_idx = skel.find_bone("Pmag2")

	for clip in ["Rig|AK_Idle", "Rig|AK_Walk", "Rig|AK_Run", "Rig|AK_Shot", "Rig|AK_Reload", "Rig|AK_Reload_full"]:
		if not ap.has_animation(clip): continue
		var a = ap.get_animation(clip)
		ap.play(clip)
		ap.seek(0.0, true)
		var pose1 = skel.get_bone_global_pose(pmag_idx)
		var pose2 = skel.get_bone_global_pose(pmag2_idx)
		print(clip, " (len ", a.length, "):")
		print("  Pmag  pos=", pose1.origin, " scale=", pose1.basis.get_scale())
		print("  Pmag2 pos=", pose2.origin, " scale=", pose2.basis.get_scale())
		# Check if Pmag or Pmag2 have tracks in this clip
		var has_pmag = false
		var has_pmag2 = false
		for i in range(a.get_track_count()):
			var p = str(a.track_get_path(i))
			if "Pmag:" in p or "Pmag" in p: has_pmag = true
			if "Pmag2:" in p or "Pmag2" in p: has_pmag2 = true
		print("  tracks: Pmag=", has_pmag, " Pmag2=", has_pmag2)

	inst.free()
	quit(0)
