extends Control
class_name Probe

# godot-abi-grid probe — GDScript twin of Probe.cs (docs/analysis.md §8.9).
#
# Used for `binding=gdscript` grid cells, where there is no .NET runtime and
# therefore no managed -> native bridge to test. Everything else about the
# target must behave identically, so keep the ready-file contract in lockstep
# with Probe.cs. The harness compares `contract` and fails on a mismatch.
#
# `static var` needs Godot 4.1+; the grid's floor is 4.2.

const READY_FILE_ENV := "GRID_READY_FILE"
const EXIT_AFTER_ENV := "GRID_EXIT_AFTER"
const CONTRACT_VERSION := "godot-abi-grid/ready.v2"

static var instance: Probe
static var harness_root: Node
static var walk_count: int = 0
static var build_tag: String = "godot-abi-grid"

var probe_ascii: String = "GridProbe ASCII 0123"
var probe_unicode: String = "héllo ✦ 日本語"
var probe_int32: int = 613227
var probe_int64: int = 887313409151
var probe_float: float = 40.9151
var probe_bool: bool = true

var _uptime: float = 0.0
var _exit_after: float = 300.0


func _ready() -> void:
	instance = self
	harness_root = self
	walk_count = count_nodes(self)

	var exit_after := OS.get_environment(EXIT_AFTER_ENV)
	if exit_after != "" and exit_after.is_valid_float():
		_exit_after = exit_after.to_float()

	_write_ready_file()
	print("[grid] probe ready, %d nodes under %s" % [walk_count, get_path()])


func _process(delta: float) -> void:
	# Hold the process open for the harness, but never leak a window forever.
	if _exit_after <= 0.0:
		return
	_uptime += delta
	if _uptime > _exit_after:
		get_tree().quit(0)


static func count_nodes(node: Node) -> int:
	var total := 1
	for child in node.get_children():
		total += count_nodes(child)
	return total


# The RAW child list, internal children included (ready.v2). See the long comment
# in Probe.cs: get_children() hides children added with INTERNAL_MODE_FRONT/BACK,
# but a calibrator walking Node::data.children in memory sees them as ordinary
# list entries. RichTextLabel's VScrollBar is one, so the authored scene and the
# in-memory tree do not have the same node count, and only the engine can say so.
static func raw_tree(root: Node) -> Array:
	var rows: Array = []
	_append_raw_tree(root, str(root.name), rows)
	return rows


static func _append_raw_tree(node: Node, path: String, rows: Array) -> void:
	var count := node.get_child_count(true)
	var child_names: Array = []
	for i in range(count):
		child_names.append(str(node.get_child(i, true).name))

	rows.append({
		"path": path,
		"name": str(node.name),
		"class": node.get_class(),
		"children": child_names,
		"internal": node.get_parent() != null and not node.get_parent().get_children().has(node),
	})

	for i in range(count):
		var child := node.get_child(i, true)
		_append_raw_tree(child, path + "/" + str(child.name), rows)


func _write_ready_file() -> void:
	var path := OS.get_environment(READY_FILE_ENV)
	if path == "":
		path = "user://grid-ready.json"

	var tree := raw_tree(self)

	var info := {
		"contract": CONTRACT_VERSION,
		"pid": OS.get_process_id(),
		"engineVersion": Engine.get_version_info()["string"],
		"binding": "gdscript",
		"templateVariant": "debug" if OS.has_feature("template_debug") else "release",
		"precision": "double" if OS.has_feature("double") else "single",
		"isTemplate": OS.has_feature("template"),
		"isEditor": OS.has_feature("editor"),
		"hasMonoFeature": OS.has_feature("mono"),
		"walkRootPath": str(get_path()),
		"walkCount": walk_count,
		"rawWalkCount": tree.size(),
		"rawTree": tree,
		"executable": OS.get_executable_path(),
		"userDataDir": OS.get_user_data_dir(),
		"staticRootType": "Probe",
		"staticRootField": "instance",
		"buildTag": build_tag,
	}

	var file := FileAccess.open(path, FileAccess.WRITE)
	if file == null:
		push_error("[grid] could not write ready file to %s: %d" % [path, FileAccess.get_open_error()])
		return
	file.store_string(JSON.stringify(info, "  "))
	file.flush()
