@tool
extends SceneTree

func inspect(path: String):
	var packed = load(path)
	if packed:
		var inst = packed.instantiate()
		print("Inspecting: ", path)
		for child in inst.get_children():
			if child is MeshInstance3D:
				print("  Mesh '", child.name, "' aabb: ", child.get_aabb())
		inst.free()

func _init():
	inspect("res://art/weapons/Melee/SledgeHammer.glb")
	inspect("res://art/weapons/Melee/sledge_hammer.glb")
	quit()
