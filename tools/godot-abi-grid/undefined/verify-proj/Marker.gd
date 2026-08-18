extends Node

# godot-abi-grid marker — GDScript twin of Marker.cs (docs/analysis.md §8.9).
#
# Deliberately empty. Its only job is to be a SECOND script instance in the
# scene, so that deriving `node.scriptInstance` is a subset test over two nodes
# instead of one. With a single scripted node any pointer-shaped slot that is
# non-null on that node satisfies the test, and every reading taken through
# scriptInstance — the owner backref, the GCHandle, the managed bridge — rests
# on that one piece of evidence.
#
# No `class_name`: nothing refers to this type, and registering a global class
# named Marker would be a needless collision risk across five engine versions.
