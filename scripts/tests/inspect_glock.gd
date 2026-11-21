
@tool
extends SceneTree
func _init():
    var scene = load('res://art/weapons/Pistols/Glock 19.glb')
    var inst = scene.instantiate()
    for child in inst.get_children():
        print_mat(child)
    quit(0)
func print_mat(n):
    if n is MeshInstance3D and n.mesh:
        for s in range(n.mesh.get_surface_count()):
            var mat = n.mesh.surface_get_material(s)
            print(n.name, ' surf ', s, ' mat: ', mat)
            if mat is StandardMaterial3D:
                print('  albedo_texture: ', mat.albedo_texture)
                print('  albedo_color: ', mat.albedo_color)
    for c in n.get_children():
        print_mat(c)
