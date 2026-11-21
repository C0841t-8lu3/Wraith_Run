@tool
extends SceneTree

func _init():
	var scene = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var root = Node3D.new()
	get_root().add_child(root)
	var cam = Camera3D.new()
	root.add_child(cam)
	var inst: Node3D = scene.instantiate()
	cam.add_child(inst)

	var ap: AnimationPlayer = inst.find_child("AnimationPlayer", true, false)
	var skel: Skeleton3D = inst.find_child("Skeleton3D", true, false)
	var ak_mesh: MeshInstance3D = inst.find_child("Ak", true, false)

	print("--- Raw inst transform: ", inst.transform)
	print("--- Skel transform: ", skel.transform)
	print("--- Ak transform: ", ak_mesh.transform)

	# Let's inspect bone poses in idle animation
	ap.play("Rig|AK_Idle")
	ap.seek(0.0, true)
	inst.force_update_transform()

	var pbody_idx = skel.find_bone("PBody")
	var rif_idx = skel.find_bone("Rif")
	var hand_r_idx = skel.find_bone("Hand_R")
	var hand_l_idx = skel.find_bone("Hand_L")

	print("PBody global pose in skel: ", skel.get_bone_global_pose(pbody_idx))
	print("Rif global pose in skel: ", skel.get_bone_global_pose(rif_idx))
	print("Hand_R global pose in skel: ", skel.get_bone_global_pose(hand_r_idx))
	print("Hand_L global pose in skel: ", skel.get_bone_global_pose(hand_l_idx))

	# Now let's test aligning by rotating inst by (0, 180, 0)
	inst.rotation_degrees = Vector3(0, 180, 0)
	inst.position = Vector3(0.06, -0.261, -0.043)
	inst.force_update_transform()

	var pbody_world = skel.global_transform * skel.get_bone_global_pose(pbody_idx)
	var pbody_in_cam = cam.global_transform.affine_inverse() * pbody_world
	print("Current PBody in cam: pos=", pbody_in_cam.origin, " basis=", pbody_in_cam.basis)
	print("PBody forward dir (-Z): ", -pbody_in_cam.basis.z)
	print("PBody up dir (+Y): ", pbody_in_cam.basis.y)
	print("PBody right dir (+X): ", pbody_in_cam.basis.x)

	quit()
