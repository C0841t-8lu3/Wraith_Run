@tool
extends SceneTree

func _init():
	var a = Animation.new()
	for m in a.get_method_list():
		if "track" in m.name.to_lower() or "interpolate" in m.name.to_lower():
			print(m.name)
	quit()
