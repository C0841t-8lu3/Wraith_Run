
@tool
extends SceneTree
func _init():
    var scene = load('res://art/animations/player/combat/sledge hammer/source/FPS_Anims.glb')
    var inst = scene.instantiate()
    var skel = inst.find_child('Skeleton3D', true, false)
    var b_hand = skel.get_bone_global_pose(30)
    var b_palm = skel.get_bone_global_pose(31)
    print('hand.R_024 origin: ', b_hand.origin, ' rot: ', b_hand.basis.get_euler() * 180 / PI)
    print('palm.01.R_025 origin: ', b_palm.origin, ' rot: ', b_palm.basis.get_euler() * 180 / PI)
    quit(0)
