
@tool
extends SceneTree
func _init():
    var scene = load('res://art/weapons/Melee/Karambit.glb')
    var inst = scene.instantiate()
    var mesh = inst.get_child(0).mesh
    print('Raw mesh AABB: ', mesh.get_aabb())
    print('Transformed by child transform: ', inst.get_child(0).transform * mesh.get_aabb())
    quit(0)
