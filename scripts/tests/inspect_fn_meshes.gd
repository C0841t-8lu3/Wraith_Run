@tool
extends SceneTree

func print_meshes(node: Node):
	if node is MeshInstance3D:
		print("Mesh: '", node.name, "' parent: '", node.get_parent().name, "' aabb: ", node.get_aabb())
	for child in node.get_children():
		print_meshes(child)

func _init():
	var packed = load("res://art/animations/player/combat/fn 502/source/FPS_Anims.fbx")
	if packed:
		var inst = packed.instantiate()
		print_meshes(inst)
		inst.free()
	quit()
