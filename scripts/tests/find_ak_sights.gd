@tool
extends SceneTree

func _init():
	print("--- Finding AK sight positions ---")
	var packed = load("res://art/animations/player/combat/AK/source/FpsAnims.fbx")
	var inst = packed.instantiate()
	var ak_mesh_node = inst.find_child("Ak", true, false) as MeshInstance3D
	if not ak_mesh_node:
		print("Ak mesh not found")
		quit(1)
		return

	var mesh = ak_mesh_node.mesh
	print("Mesh surfaces: ", mesh.get_surface_count())
	var mdt = MeshDataTool.new()
	mdt.create_from_surface(mesh, 0)
	var vcount = mdt.get_vertex_count()
	print("Vertex count in Ak: ", vcount)

	# In bind pose / mesh local space, let's find the extent
	var aabb = ak_mesh_node.get_aabb()
	print("Ak local AABB: pos=", aabb.position, " size=", aabb.size)

	# Find highest vertices along the length of the rifle
	# The rifle longest axis:
	# From earlier: size=(0.000966, 0.013844, 0.003855) or similar
	# Let's inspect vertex coordinate ranges
	var min_v = Vector3(1e9, 1e9, 1e9)
	var max_v = Vector3(-1e9, -1e9, -1e9)
	for i in range(vcount):
		var v = mdt.get_vertex(i)
		min_v = min_v.min(v)
		max_v = max_v.max(v)
	print("Min vertex: ", min_v)
	print("Max vertex: ", max_v)

	# In FBX local space, Y seems to be the length of the rifle (from -0.0095 to +0.0042) or Z?
	# Let's sample vertices near the front (muzzle) and near the middle (rear sight)
	# Let's print vertices near the max Y and min Y
	var top_vertices = []
	for i in range(vcount):
		var v = mdt.get_vertex(i)
		# find vertices with high Z (if Z is up) or high X or high Y
		top_vertices.append(v)

	inst.free()
	quit(0)
