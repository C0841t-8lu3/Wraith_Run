@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/sledge hammer/source/FPS_Anims.glb")
	if packed:
		var inst = packed.instantiate()
		var cam = inst.find_child("Camera", true, false) as Camera3D
		if cam:
			print("Rig Camera pos: ", cam.position, " rot: ", cam.rotation_degrees, " scale: ", cam.scale)
			print("Rig Camera global pos: ", cam.global_position)
		var armature = inst.find_child("Armature", true, false) as Node3D
		if armature:
			print("Armature pos: ", armature.position, " rot: ", armature.rotation_degrees, " scale: ", armature.scale)
		inst.free()
	quit()
