extends SceneTree

var scene

func _initialize() -> void:
	scene = load("res://Main.tscn").instantiate()
	root.add_child(scene)

func _process(_delta: float) -> bool:
	var rows: Array = []
	_walk(scene, str(scene.name), rows)
	var out := OS.get_environment("VERIFY_OUT")
	var f := FileAccess.open(out, FileAccess.WRITE)
	f.store_string(JSON.stringify(rows, "  "))
	f.flush()
	print("[verify] wrote ", out, " rows=", rows.size())
	quit()
	return true

func _walk(node: Node, path: String, rows: Array) -> void:
	var row := {"path": path, "class": node.get_class(), "script": node.get_script() != null}
	if node is Control:
		row["position"] = [node.position.x, node.position.y]
		row["size"] = [node.size.x, node.size.y]
		row["scale"] = [node.scale.x, node.scale.y]
		row["offset"] = [node.offset_left, node.offset_top, node.offset_right, node.offset_bottom]
		row["anchors"] = [node.anchor_left, node.anchor_top, node.anchor_right, node.anchor_bottom]
	if node is CanvasItem:
		row["visible"] = node.visible
		row["visible_in_tree"] = node.is_visible_in_tree()
	if node is Label or node is RichTextLabel:
		row["text"] = node.text
	rows.append(row)
	for i in range(node.get_child_count(true)):
		var c := node.get_child(i, true)
		_walk(c, path + "/" + str(c.name), rows)
