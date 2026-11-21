@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var vp = SubViewport.new()
	vp.size = Vector2i(640, 360)
	vp.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(vp)

	var env = Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.2, 0.22, 0.25)
	env.ambient_light_color = Color(1, 1, 1)
	env.ambient_light_energy = 1.0
	var w3d = World3D.new()
	w3d.environment = env
	vp.world_3d = w3d

	var cam = Camera3D.new()
	cam.position = Vector3(0, 0, 0)
	vp.add_child(cam)

	var light = DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-45, 45, 0)
	vp.add_child(light)

	vp.add_child(inst)
	inst.position = Vector3(0.055, -0.265, -0.025)
	inst.rotation_degrees = Vector3(0, 180, 0)

	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	var pmag_idx = skel.find_bone("Pmag")
	var pmag2_idx = skel.find_bone("Pmag2")

	# Hide Pmag2
	skel.set_bone_pose_scale(pmag2_idx, Vector3.ZERO)

	# Wait a frame and render
	for i in range(5):
		await process_frame

	var img = vp.get_texture().get_image()
	img.save_png("scripts/tests/test_mag_hidden_pmag2.png")
	print("Saved test_mag_hidden_pmag2.png")

	# Now hide Pmag too (entire magazine gone from AK!)
	skel.set_bone_pose_scale(pmag_idx, Vector3.ZERO)
	for i in range(5):
		await process_frame

	img = vp.get_texture().get_image()
	img.save_png("scripts/tests/test_mag_hidden_both.png")
	print("Saved test_mag_hidden_both.png")

	inst.free()
	vp.free()
	quit(0)
