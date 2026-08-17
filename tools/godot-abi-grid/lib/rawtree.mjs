// Reconciling the AUTHORED scene with the engine's own child lists.
//
// The grid's premise is an authored ground-truth scene "we control completely"
// (§8.9). Godot does not fully honour that: nodes may add children of their own
// with INTERNAL_MODE_FRONT/BACK, which `Node.get_children()` and a .tscn parse
// both hide, but which sit in `Node::data.children` as ordinary entries. A
// calibrator walking that list in memory sees them and cannot do otherwise.
//
// On this scene that is RichTextLabel: `ZetaRich` has zero authored children and
// exactly one — a VScrollBar — in memory. Stating "20 nodes" to a calibrator that
// will walk 21 is not a harmless off-by-one. `expected.json`'s count and name set
// are what a search uses to RECOGNISE the child list, so a wrong count makes the
// CORRECT layout unacceptable; the observed consequence on 4.5-release-single-
// dotnet was that the calibrator rejected the real child list at 0x148 and
// accepted an unrelated intrusive list that threads every node in the scene.
//
// The engine is the only party that knows its internal children — their names
// carry a global instantiation counter (`@VScrollBar@2`) that no static file can
// predict and that moves between engine versions. So `Probe.cs`/`Probe.gd` report
// the raw tree in the ready file (ready.v2) and this module reconciles it:
//
//   * the anchors handed to the calibrator describe the tree it will actually
//     walk (raw count, raw names);
//   * every AUTHORED node must still be present, at its authored path, with its
//     authored children in their authored ORDER once the internal ones are
//     filtered out — so nothing about the authored assertion is given up;
//   * any node in the raw tree that is not authored must be flagged internal by
//     the engine. An unexplained extra node is a failure, not an allowance.

/** Nothing engine-side to reconcile: fall back to the authored scene exactly. */
function authoredOnly(expected) {
  return {
    available: false,
    nodeCount: expected.nodeCount,
    names: expected.strings.names,
    childrenByPath: new Map(expected.nodes.map((n) => [n.path, n.childPaths.map((p) => p.split('/').pop())])),
    internalNames: [],
    problems: [],
  };
}

/**
 * Build the walk model the harness judges against.
 *
 * With a ready.v2 target this is the engine's raw tree, checked against the
 * authored scene. Without one (`--no-launch`, or the selftest's synthetic ready)
 * it is the authored scene, unchanged.
 */
export function resolveWalkModel(expected, ready) {
  const rows = Array.isArray(ready?.rawTree) ? ready.rawTree : null;
  if (!rows || rows.length === 0) return authoredOnly(expected);

  const problems = [];
  const childrenByPath = new Map();
  const byPath = new Map();
  for (const row of rows) {
    if (typeof row?.path !== 'string') {
      problems.push('rawTree row has no path');
      continue;
    }
    const children = Array.isArray(row.children) ? row.children.map(String) : [];
    childrenByPath.set(row.path, children);
    byPath.set(row.path, row);
  }

  const authoredNames = new Set(expected.nodes.map((n) => n.name));

  // Every authored node must be present, and its authored children must survive
  // in order once engine-internal siblings are removed.
  for (const node of expected.nodes) {
    const raw = childrenByPath.get(node.path);
    if (!raw) {
      problems.push(`authored node ${node.path} is absent from the target's own raw tree`);
      continue;
    }
    const want = node.childPaths.map((p) => p.split('/').pop());
    const got = raw.filter((name) => authoredNames.has(name));
    if (got.length !== want.length || got.some((n, i) => n !== want[i])) {
      problems.push(`${node.path}: engine child list [${raw.join(', ')}] does not contain the authored children [${want.join(', ')}] in order`);
    }
  }

  // Anything extra has to be an engine-internal child, by the engine's own say-so.
  const internalNames = [];
  for (const row of rows) {
    if (authoredNames.has(row.name) && byPath.has(row.path) && expected.byPath.has(row.path)) continue;
    if (row.internal === true) {
      internalNames.push(row.name);
      continue;
    }
    problems.push(`raw tree contains ${row.path} ("${row.name}"), which is neither authored nor flagged internal by the engine`);
  }

  return {
    available: true,
    nodeCount: rows.length,
    names: rows.map((r) => String(r.name)),
    childrenByPath,
    internalNames,
    problems,
  };
}
