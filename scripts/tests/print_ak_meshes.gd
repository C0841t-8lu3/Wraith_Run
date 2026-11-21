@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	for child in inst.get_children():
		print("Child: ", child.name, " type: ", child.get_class())
	for node in inst.find_children("*", "MeshInstance3D", true, false):
		var mesh_inst = node as MeshInstance3D
		print("MeshInstance3D: ", mesh_inst.name, " parent: ", mesh_inst.get_parent().name, " skin: ", mesh_inst.skin != null)
		if mesh_inst.skin:
			print("  Skin bind count: ", mesh_inst.skin.get_bind_count())
			for b in range(mesh_inst.skin.get_bind_count()):
				var bname = mesh_inst.skin.get_bind_name(b)
				var bbone = mesh_inst.skin.get_bind_bone(b)
				if "mag" in str(bname).to_lower() or bbone in [62, 63, 80, 82]:
					print("    Bind ", b, ": name='", bname, "' bone=", bbone, " ('", skel.get_bone_name(bbone), "')")

	inst.free()
	quit(0)
