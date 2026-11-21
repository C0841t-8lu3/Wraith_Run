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

	var cam = Camera3D.new()
	cam.fov = 75.0
	cam.near = 0.01
	cam.far = 100.0
	cam.current = true
	viewport.add_child(cam)

	var data = load("res://data/viewmodels/AK47.tres")
	# Temporary test position
	var vm_script = load("res://scripts/player/ViewModelBase.cs")
	var vm = Node3D.new()
	vm.set_script(vm_script)
	vm.call("Configure", data)
	cam.add_child(vm)

	# Set good hipfire framing:
	# pos = (0.08, -0.22, -0.12), rot = (0, 180, 0)
	vm.set("PositionOffset", Vector3(0.08, -0.22, -0.12))
	vm.set("RotationOffsetDegrees", Vector3(0, 180, 0))

	# Wait a bit
	for i in range(10):
		await process_frame

	var ap = vm.find_child("AnimationPlayer", true, false) as AnimationPlayer
	ap.play("Rig|AK_Reload")

	var sample_times = [0.0, 0.4, 0.8, 1.2, 1.5, 1.8, 2.2, 2.6]
	for idx in range(sample_times.size()):
		var t = sample_times[idx]
		ap.seek(t, true)
		for f in range(2):
			await process_frame
		var img = viewport.get_texture().get_image()
		if img:
			var path = "/home/aauel/Documents/Projects/Games/wraith_run_godot/scripts/tests/ak_reload_frame_" + str(idx) + ".png"
			img.save_png(path)
			print("Saved ", path, " at t=", t)

	inst_free(viewport)
	quit(0)

func inst_free(node):
	node.queue_free()
