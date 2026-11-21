
@tool
extends SceneTree
func _init():
    var scene = load('res://art/weapons/Pistols/Desert Eagle.glb')
    var inst = scene.instantiate()
    print_tree_nodes(inst, '')
    quit(0)
func print_tree_nodes(node, prefix):
    print(prefix, node.name, ' (', node.get_class(), ')')
    if node is MeshInstance3D and node.mesh:
        print(prefix, '  AABB:', node.mesh.get_aabb())
    for c in node.get_children():
        print_tree_nodes(c, prefix + '  ')
