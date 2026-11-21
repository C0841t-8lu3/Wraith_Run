@tool
extends SceneTree

func _init():
	var viewport = SubViewport.new()
	viewport.size = Vector2i(1280, 720)
	root.add_child(viewport)

	var env = Environment.new()
	env.background_mode = Environment.BG_COLOR
	env.background_color = Color(0.2, 0.22, 0.25)
	env.ambient_light_color = Color(0.9, 0.9, 0.9)
	env.ambient_light_energy = 1.0
	var world3d = World3D.new()
	world3d.environment = env
	viewport.world_3d = world3d

	var light = DirectionalLight3D.new()
	light.rotation_degrees = Vector3(-45, 45, 0)
	viewport.add_child(light)

	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	viewport.add_child(inst)

	# Find the author's camera inside the FBX
	var cam = inst.find_child("Camera", true, false) as Camera3D
	if cam:
		cam.current = true
		cam.visible = true
		cam.near = 0.01
		cam.far = 100.0
		print("Found FBX author camera: pos=", cam.position, " rot=", cam.rotation_degrees, " fov=", cam.fov)
	else:
		print("NO CAMERA FOUND IN FBX!")
		quit(1)
		return

	# Enable textures
	var albedo_tex = load("res://art/animations/player/combat/AK/textures/ak74m_AlbedoTransparency.png")
	var normal_tex = load("res://art/animations/player/combat/AK/textures/ak74m_Normal.png")
	var metallic_tex = load("res://art/animations/player/combat/AK/textures/ak74m_Metallic.png")
	var rough_tex = load("res://art/animations/player/combat/AK/textures/ak74m_Roughness.png")
	var ao_tex = load("res://art/animations/player/combat/AK/textures/ak74m_AO.png")

	var ak_mat = StandardMaterial3D.new()
	ak_mat.albedo_texture = albedo_tex
	ak_mat.normal_enabled = true
	ak_mat.normal_texture = normal_tex
	ak_mat.metallic = 1.0
	ak_mat.metallic_texture = metallic_tex
	ak_mat.roughness_texture = rough_tex
	ak_mat.ao_enabled = true
	ak_mat.ao_texture = ao_tex
	ak_mat.cull_mode = BaseMaterial3D.CULL_DISABLED

	for node in inst.find_children("*", "MeshInstance3D"):
		var m = node as MeshInstance3D
		if m.name in ["Ak", "Mag_2"]:
			m.material_override = ak_mat
		print("Mesh: ", m.name, " vis=", m.visible)

	var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
	ap.play("Rig|AK_Idle")

	for i in range(10):
		await process_frame

	var img = viewport.get_texture().get_image()
	img.save_png("/home/aauel/Documents/Projects/Games/wraith_run_godot/scripts/tests/AK47_author_cam_idle.png")
	print("Saved AK47_author_cam_idle.png")

	inst.free()
	viewport.free()
	quit(0)
