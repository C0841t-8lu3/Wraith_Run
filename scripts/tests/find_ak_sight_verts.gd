@tool
extends SceneTree

func _init():
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	root.add_child(inst)
	var ak_mesh_node = inst.find_child("Ak", true, false) as MeshInstance3D
	var skel = inst.find_child("Skeleton3D", true, false) as Skeleton3D
	var ap = inst.find_child("AnimationPlayer", true, false) as AnimationPlayer
	
	ap.play("Rig|AK_Idle")
	ap.seek(0.0, true)
	inst.force_update_transform()

	var mesh = ak_mesh_node.mesh
	var mdt = MeshDataTool.new()
	mdt.create_from_surface(mesh, 0)
	var vcount = mdt.get_vertex_count()

	# Let's find vertices in skel space by evaluating bone weights
	# Or let's inspect the mesh vertices directly
	# Find vertices with highest Y (highest point above bore) at different Z stations (front vs middle)
	# In world space after inst has rotation (0, 180, 0) and pos (0,0,0)
	inst.rotation_degrees = Vector3(0, 180, 0)
	inst.position = Vector3.ZERO
	inst.force_update_transform()

	# Let's sample along Z:
	# Front sight is near the muzzle (most negative Z in camera space)
	# Rear sight is near the receiver (less negative Z)
	# Let's find for each Z band (say 5cm slices from -0.8 to +0.2), the vertex with maximum Y!
	print("--- AK Height Profile along Z ---")
	var slices = {}
	for i in range(vcount):
		# We can get vertex in bone space or mesh space
		var v = mdt.get_vertex(i)
		# Mesh local v: in FBX, Y is the long axis!
		# Let's find max Z in mesh local space for each Y slice
		var y_slice = round(v.y * 1000.0) / 1000.0 # 1mm bins
		# Actually let's bin by 0.0005 (0.5mm)
		var bin = int(v.y * 2000.0)
		if not slices.has(bin):
			slices[bin] = {"max_z": -999.0, "min_z": 999.0, "v": v, "y": v.y}
		if v.z > slices[bin]["max_z"]:
			slices[bin]["max_z"] = v.z
			slices[bin]["v_max"] = v
		if v.z < slices[bin]["min_z"]:
			slices[bin]["min_z"] = v.z
			slices[bin]["v_min"] = v

	var sorted_bins = slices.keys()
	sorted_bins.sort()
	for b in sorted_bins:
		var s = slices[b]
		# print peaks
		# In FBX mesh local space:
		# v.y runs from -0.0095 to +0.0042
		# Let's check when max_z is unusually high (e.g. front sight post, rear sight leaf)
		if s["max_z"] > 0.0015:
			print("Peak at local y=", s["y"], " max_z=", s["max_z"], " v=", s["v_max"])

	inst.queue_free()
	quit(0)
