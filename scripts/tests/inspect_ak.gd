@tool
extends SceneTree

func _init():
	print("--- Inspecting AK FBX ---")
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	if not packed:
		print("Failed to load FBX")
		quit(1)
		return

	var inst = packed.instantiate()
	
	# Check animation player
	var anim_player = inst.find_child("AnimationPlayer", true, false)
	if anim_player:
		print("Animations:")
		for anim in anim_player.get_animation_list():
			var a = anim_player.get_animation(anim)
			print("  - ", anim, " (len: ", a.length, "s, loop: ", a.loop_mode, ")")
			# Check tracks in animation
			# print("    tracks: ", a.get_track_count())

	# Check Skeleton3D
	var skel = inst.find_child("Skeleton3D", true, false)
	if skel:
		print("\nBones in Skeleton3D (count: ", skel.get_bone_count(), "):")
		for b in range(skel.get_bone_count()):
			var bname = skel.get_bone_name(b)
			var parent = skel.get_bone_parent(b)
			var parent_name = skel.get_bone_name(parent) if parent >= 0 else "ROOT"
			var pose = skel.get_bone_pose(b)
			print("  [", b, "] ", bname, " (parent: ", parent_name, ") pos=", pose.origin)

	# Check Meshes
	print("\nNodes in scene:")
	_print_tree(inst, "  ")

	inst.free()
	quit(0)

func _print_tree(node: Node, indent: String):
	var extra = ""
	if node is MeshInstance3D:
		var aabb = node.get_aabb()
		extra = " [MeshInstance3D] aabb=" + str(aabb) + " vis=" + str(node.visible)
	elif node is Camera3D:
		extra = " [Camera3D] pos=" + str(node.position) + " rot=" + str(node.rotation_degrees)
	elif node is BoneAttachment3D:
		extra = " [BoneAttachment3D bone=" + str(node.bone_name) + "]"
	print(indent, node.name, " (", node.get_class(), ")", extra)
	for child in node.get_children():
		_print_tree(child, indent + "  ")
