@tool
extends SceneTree

func _init():
	var packed_rig = load("res://art/animations/player/combat/sledge hammer/source/FPS_Anims.glb")
	var packed_knife = load("res://art/weapons/Melee/Karambit.glb")
	if packed_rig and packed_knife:
		var root = Node3D.new()
		get_root().add_child(root)

		var cam = Camera3D.new()
		cam.fov = 75.0
		cam.near = 0.05
		cam.far = 100.0
		cam.current = true
		root.add_child(cam)

		var inst = packed_rig.instantiate()
		cam.add_child(inst)
		inst.transform = Transform3D(Basis(Vector3.UP, PI), Vector3(0.04, -0.16, 0.04))

		var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
		var orig = inst.find_child("SledgeHammer_SledgeHammer_0", true, false) as MeshInstance3D
		if orig: orig.visible = false

		var left_arm = skel.find_bone("upper_arm.L_01")
		if left_arm >= 0: skel.set_bone_pose_scale(left_arm, Vector3.ZERO)

		var attach = BoneAttachment3D.new()
		attach.bone_name = "hand.R_024"
		skel.add_child(attach)

		var knife = packed_knife.instantiate()
		attach.add_child(knife)
		knife.position = Vector3(0.04, 0.08, -0.02)
		knife.rotation_degrees = Vector3(0, 90, 180)
		knife.scale = Vector3(0.8, 0.8, 0.8)

		var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
		ap.play("idle")
		ap.advance(0.1)
		if left_arm >= 0: skel.set_bone_pose_scale(left_arm, Vector3.ZERO)

		var vp = get_root().get_viewport()
		for i in range(10):
			await process_frame
			if left_arm >= 0: skel.set_bone_pose_scale(left_arm, Vector3.ZERO)

		var img = vp.get_texture().get_image()
		if img:
			img.save_png("/home/aauel/Documents/Projects/Games/wraith_run_godot/scripts/tests/karambit_test_aligned.png")
			print("Saved karambit test aligned!")
		inst.free()
		root.free()
	quit()
