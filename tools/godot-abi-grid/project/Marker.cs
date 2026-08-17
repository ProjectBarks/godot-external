using Godot;

// godot-abi-grid marker — docs/analysis.md §8.9
//
// This script exists to be ATTACHED. It does nothing, and that is deliberate.
//
// `node.scriptInstance` is derived the same way every other member is: find the
// slot that is non-null on the nodes that have a script and null on the nodes
// that do not. With exactly ONE scripted node in the scene that test is
// arithmetically vacuous — any pointer-shaped slot that happens to be non-null
// on that one node satisfies it — and everything downstream of scriptInstance
// (the ScriptInstance owner backref, the GCHandle, the entire managed bridge)
// inherits the same single point of evidence. It is the identical shape to the
// RichTextLabel text problem: a subset test over a one-element set.
//
// `RootHarness` carries Probe; `OmegaMarker` carries this. Two scripted nodes
// out of twenty-five means a candidate slot must be non-null on exactly those
// two and null on the other twenty-three, which is a real constraint.
//
// It is attached to a bare `Node`, not a Control, so it doubles the scene's
// non-CanvasItem sample as well (see DeltaSiblingOne in Main.tscn).
//
// Marker.gd is the GDScript twin. Keep both as empty as the language allows:
// this is a second script INSTANCE, not a second thing to test.
public partial class Marker : Node
{
}
