@tool
extends SceneTree

func _init():
	for name in ["Glock 19.glb", "Desert Eagle.glb"]:
		var packed = load("res://art/weapons/Pistols/" + name)
		var inst = packed.instantiate()
		print("=== ", name, " ===")
		for child in inst.find_children("*", "MeshInstance3D", true, false):
			var m = child.mesh
			if m:
				print("  ", child.name, " aabb: ", m.get_aabb())
		inst.free()
	quit(0)
