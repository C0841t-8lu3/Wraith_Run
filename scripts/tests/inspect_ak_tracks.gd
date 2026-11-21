@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	
	for anim_name in ap.get_animation_list():
		var a = ap.get_animation(anim_name)
		print("\n=== Anim: ", anim_name, " (len: ", a.length, ") ===")
		var camera_tracks = []
		var head_cam_tracks = []
		var pbody_tracks = []
		var pmag_tracks = []
		var root_tracks = []
		var arm_tracks = []
		for t in range(a.get_track_count()):
			var p = str(a.track_get_path(t))
			if "Camera" in p:
				camera_tracks.append(p)
			if "Head_Cam" in p:
				head_cam_tracks.append(p)
			if "PBody" in p:
				pbody_tracks.append(p)
			if "Pmag" in p:
				pmag_tracks.append(p)
			if "Root" in p:
				root_tracks.append(p)
			if "Arm" in p:
				arm_tracks.append(p)
		print("  Camera tracks: ", camera_tracks)
		print("  Head_Cam tracks: ", head_cam_tracks)
		print("  Root tracks: ", root_tracks)
		print("  Pmag tracks: ", pmag_tracks)
		print("  PBody tracks: ", pbody_tracks)
		print("  Arm tracks count: ", arm_tracks.size())

	inst.free()
	quit(0)
