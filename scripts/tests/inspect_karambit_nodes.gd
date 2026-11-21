
@tool
extends SceneTree
func _init():
    var scene = load('res://art/weapons/Melee/Karambit.glb')
    var inst = scene.instantiate()
    print('Karambit.glb scale: ', inst.scale)
    for c in inst.get_children():
        print('  child: ', c.name, ' scale: ', c.scale, ' transform: ', c.transform)
    quit(0)
