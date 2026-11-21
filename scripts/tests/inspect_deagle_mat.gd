@tool
extends SceneTree

func _init():
	var packed = load("res://art/weapons/Pistols/Desert Eagle.glb")
	var inst = packed.instantiate()
	var mesh_inst = inst.find_child("barrel", true, false) as MeshInstance3D
	var mat = mesh_inst.mesh.surface_get_material(0) as StandardMaterial3D
	print("Material name: ", mat.resource_name)
	print("  albedo_color: ", mat.albedo_color)
	print("  albedo_texture: ", mat.albedo_texture)
	print("  metallic: ", mat.metallic)
	print("  metallic_texture: ", mat.metallic_texture)
	print("  roughness: ", mat.roughness)
	print("  roughness_texture: ", mat.roughness_texture)
	print("  normal_enabled: ", mat.normal_enabled)
	print("  normal_texture: ", mat.normal_texture)

	# Check all materials in the glb
	for child in inst.find_children("*", "MeshInstance3D", true, false):
		var mi = child as MeshInstance3D
		if mi.mesh:
			for s in range(mi.mesh.get_surface_count()):
				var m = mi.mesh.surface_get_material(s) as StandardMaterial3D
				if m:
					print(child.name, " surf ", s, " mat: ", m.resource_name, " alb_tex=", m.albedo_texture)

	inst.free()
	quit(0)
