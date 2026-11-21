@tool
extends SceneTree

func print_nodes(node: Node, depth: int):
	var indent = "  ".repeat(depth)
	if node is MeshInstance3D:
		print(indent, "Mesh: '", node.name, "' visible: ", node.visible, " aabb: ", node.get_aabb())
	else:
		print(indent, "Node: '", node.name, "' (", node.get_class(), ")")
	for child in node.get_children():
		print_nodes(child, depth + 1)

func _init():
	var packed = load("res://art/animations/player/combat/sledge hammer/source/FPS_Anims.glb")
	if packed:
		var inst = packed.instantiate()
		print_nodes(inst, 0)
		inst.free()
	quit()
