@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/sledge hammer/source/FPS_Anims.glb")
	if packed:
		var root = Node3D.new()
		get_root().add_child(root)
		var inst = packed.instantiate()
		root.add_child(inst)
		var cam = inst.find_child("Camera", true, false) as Camera3D
		cam.current = true
		cam.far = 200.0
		var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
		ap.play("idle")
		ap.advance(0.1)

		var vp = get_root().get_viewport()
		for i in range(10):
			await process_frame

		var img = vp.get_texture().get_image()
		if img:
			img.save_png("/home/aauel/Documents/Projects/Games/wraith_run_godot/scripts/tests/sledge_native_cam.png")
			print("Saved native cam render!")
		inst.free()
		root.free()
	quit()
