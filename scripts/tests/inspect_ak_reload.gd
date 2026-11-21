@tool
extends SceneTree

func _init():
	print("--- Inspecting AK Magazine and Reload animations ---")
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	var mag_mesh = inst.find_child("Mag_2", true, false) as MeshInstance3D

	var mdt = MeshDataTool.new()
	mdt.create_from_surface(mag_mesh.mesh, 0)
	print("Mag_2 vertex count: ", mdt.get_vertex_count())
	var bone_usage = {}
	for i in range(mdt.get_vertex_count()):
		var bones = mdt.get_vertex_bones(i)
		var weights = mdt.get_vertex_weights(i)
		for j in range(4):
			if weights[j] > 0.01:
				var b = bones[j]
				bone_usage[b] = bone_usage.get(b, 0) + 1

	for b in bone_usage.keys():
		print("Mag_2 influenced by bone [", b, "] '", skel.get_bone_name(b), "': ", bone_usage[b], " verts")

	# Check Pmag vs Pmag2 in Reload and Reload_full
	for reload_clip in ["Rig|AK_Reload", "Rig|AK_Reload_full"]:
		var a = ap.get_animation(reload_clip)
		print("\n=== ", reload_clip, " (len: ", a.length, "s) ===")
		var pmag_idx = skel.find_bone("Pmag")
		var pmag2_idx = skel.find_bone("Pmag2")
		ap.play(reload_clip)

		# Sample at intervals
		var num_samples = 15
		for s in range(num_samples + 1):
			var t = (a.length / float(num_samples)) * float(s)
			ap.seek(t, true)
			var pose_pmag = skel.get_bone_pose(pmag_idx)
			var pose_pmag2 = skel.get_bone_pose(pmag2_idx)
			print("  t=", String.num(t, 2), "s: Pmag pos=", pose_pmag.origin, " scale=", pose_pmag.basis.get_scale(), " | Pmag2 pos=", pose_pmag2.origin, " scale=", pose_pmag2.basis.get_scale())

	inst.free()
	quit(0)
