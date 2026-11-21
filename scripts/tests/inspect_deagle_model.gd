@tool
extends SceneTree

func _init():
	var packed = load("res://art/weapons/Pistols/Desert Eagle.glb")
	if packed == null:
		print("FAILED to load Desert Eagle.glb")
		quit(1)
		return
	var inst = packed.instantiate()
	print("Desert Eagle.glb root: ", inst.name, " class: ", inst.get_class())
	for child in inst.find_children("*", "VisualInstance3D", true, false):
		print("  Child: ", child.name, " type: ", child.get_class())
		if child is MeshInstance3D:
			var m = child.mesh
			print("    Mesh: ", m, " AABB: ", m.get_aabb() if m else "null")
			if child.material_override:
				print("    MaterialOverride: ", child.material_override)
			for s in range(child.get_surface_override_material_count()):
				print("    SurfaceOverride[", s, "]: ", child.get_surface_override_material(s))
			if m:
				for s in range(m.get_surface_count()):
					print("    MeshMaterial[", s, "]: ", m.surface_get_material(s))

	inst.free()
	quit(0)
