@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/Sniper/source/BoltActionRifle.fbx")
	if packed:
		var inst = packed.instantiate()
		var cam = inst.find_child("Camera", true, false) as Camera3D
		print("Camera pos: ", cam.position, " rot: ", cam.rotation_degrees, " scale: ", cam.scale)
		inst.free()
	quit()
