// The assertion engine — the part of the harness that decides whether a cell
// counts as evidence. Everything §8.9 lists under "What calibrate.mjs asserts
// per build" lives here and nowhere else.
//
//   (a) structural offsets  derived by POINTER IDENTITY alone
//   (b) semantic offsets    derived by KNOWN-VALUE INTERSECTION across samples
//   (c) names and text      decoded exactly, non-ASCII included
//   (d) derived offsets     agree with the shipped profile where one exists;
//                           disagreement is LOUD, never a silent fallback
//   (e) full-tree walk      count matches expected.json
//
// Two rules this file exists to enforce, both easy to lose by accident:
//   * A check that cannot be evaluated is `skip`, never `pass`. A harness that
//     reports 17/17 because 6 checks quietly degraded to no-ops is worse than
//     no harness.
//   * Nothing here ever feeds profiles.json back to the driver. The profile is
//     scored against, not scored with.

import { parseOffset, hex, toVector, normPtr, vectorsEqual, fmtVec, escapeText, codepoints } from './util.mjs';
import { resolveWalkModel } from './rawtree.mjs';

const EPS = 1e-3;

const STRUCTURAL_METHOD = 'pointer-identity';
const SEMANTIC_METHOD = 'known-value-intersection';

// ---------------------------------------------------------------------------
// Driver result normalisation
// ---------------------------------------------------------------------------

export function normaliseResult(raw) {
  const der = raw.derivation ?? {};
  const offsets = (o) => Object.fromEntries(Object.entries(o ?? {}).map(([k, v]) => [k, parseOffset(v)]));

  const nodes = (raw.nodes ?? []).map((n) => ({
    raw: n,
    name: typeof n.name === 'string' ? n.name : null,
    class: n.class ?? n.className ?? null,
    nativePtr: normPtr(n.nativePtr ?? n.ptr ?? n.address),
    parentPtr: normPtr(n.parentPtr ?? n.parent),
    childPtrs: (n.childPtrs ?? n.children ?? []).map(normPtr),
    size: toVector(n.size, 2),
    position: toVector(n.position, 2),
    scale: toVector(n.scale, 2),
    offset: toVector(n.offset, 4),
    anchors: toVector(n.anchors, 4),
    visible: typeof n.visible === 'boolean' ? n.visible : null,
    text: typeof n.text === 'string' ? n.text : null,
    reportedPath: n.path ?? null,
  }));

  return {
    driver: raw.driver ?? 'unknown',
    driverVersion: raw.driverVersion ?? null,
    usedProfile: raw.usedProfile === true,
    profileConsulted: raw.profileConsulted ?? null,
    engineVersion: raw.engineVersion ?? null,
    walkCount: typeof raw.walkCount === 'number' ? raw.walkCount : nodes.length,
    structural: { method: der.structural?.method ?? null, offsets: offsets(der.structural?.offsets), evidence: der.structural?.evidence ?? null },
    semantic: {
      method: der.semantic?.method ?? null,
      offsets: offsets(der.semantic?.offsets),
      samples: der.semantic?.samples ?? null,
      candidates: der.semantic?.candidates ?? null,
    },
    strings: { method: der.strings?.method ?? null, offsets: offsets(der.strings?.offsets) },
    walk: offsets(der.walk?.offsets ?? der.walk),
    scriptInstanceClass: der.walk?.scriptInstanceClass ?? null,
    nodes,
    managedBridge: raw.managedBridge ?? null,
    notes: raw.notes ?? [],
  };
}

/**
 * Rebuild every node's tree path from parentPtr links ALONE. This is not a
 * convenience: reconstructing the path from pointers is how the harness avoids
 * trusting a driver's own idea of the hierarchy. If the parent offset is wrong,
 * path reconstruction collapses and check (a) fails, which is the point.
 */
function reconstructPaths(nodes) {
  const byPtr = new Map();
  for (const n of nodes) if (n.nativePtr) byPtr.set(n.nativePtr, n);

  const roots = nodes.filter((n) => !n.parentPtr || !byPtr.has(n.parentPtr));
  const errors = [];
  if (roots.length !== 1) {
    errors.push(`expected exactly 1 walk root by parent-pointer analysis, found ${roots.length}`);
  }

  const pathOf = new Map();
  const resolvePath = (node, seen = new Set()) => {
    if (pathOf.has(node)) return pathOf.get(node);
    if (seen.has(node)) return null; // cycle
    seen.add(node);
    const parent = node.parentPtr ? byPtr.get(node.parentPtr) : null;
    const parentPath = parent ? resolvePath(parent, seen) : null;
    const path = parent ? (parentPath === null ? null : `${parentPath}/${node.name}`) : node.name;
    pathOf.set(node, path);
    return path;
  };

  for (const n of nodes) n.path = resolvePath(n);
  return { byPtr, roots, errors };
}

// ---------------------------------------------------------------------------
// Check plumbing
// ---------------------------------------------------------------------------

const pass = (id, title, detail, data) => ({ id, title, status: 'pass', detail, data });
const fail = (id, title, detail, data) => ({ id, title, status: 'fail', detail, data });
const skip = (id, title, detail, data) => ({ id, title, status: 'skip', detail, data });

function requireDerivationMethod(id, title, actual, wanted, extra) {
  if (actual === wanted) return null;
  return fail(id, title,
    `derivation method is "${actual ?? 'unreported'}", §8.9 requires "${wanted}". ${extra ?? ''}`.trim());
}

// ---------------------------------------------------------------------------

export function runChecks({ cell, expected, profile, ready, result: rawResult }) {
  const result = normaliseResult(rawResult);
  const { errors: structureErrors } = reconstructPaths(result.nodes);
  const byPath = new Map();
  for (const n of result.nodes) if (n.path) byPath.set(n.path, n);

  const checks = [];
  const controlNodes = expected.nodes.filter((n) => n.isControl);

  // The tree a memory walk actually sees: the authored scene plus whatever
  // internal children the engine added to it (lib/rawtree.mjs).
  const walk = resolveWalkModel(expected, ready);

  // -- runtime axes agree with the cell name --------------------------------
  checks.push(checkRuntimeAxes(cell, expected, ready, walk));

  // -- the calibrator ran unaided -------------------------------------------
  checks.push(checkUnaided(result));

  // -- (a) structural, by pointer identity ----------------------------------
  checks.push(checkChildHead(expected, result, byPath, structureErrors, walk));
  checks.push(checkParent(expected, result, byPath));

  // -- (b) semantic, by known-value intersection ----------------------------
  checks.push(checkVectorAccessor({
    id: 'semantic.size', title: '(b) size derived + read correctly', key: 'size',
    offsetKey: 'control.size', arity: 2, expected, result, byPath, nodes: controlNodes,
    why: '§12.5: a single 200x50 scan gave four candidates; intersecting two differently sized controls left exactly one.',
  }));
  checks.push(checkVectorAccessor({
    id: 'semantic.position', title: '(b) position derived + read correctly', key: 'position',
    offsetKey: 'control.position', arity: 2, expected, result, byPath, nodes: controlNodes,
  }));
  checks.push(checkVectorAccessor({
    id: 'semantic.scale', title: '(b) scale derived + read correctly', key: 'scale',
    offsetKey: 'control.scale', arity: 2, expected, result, byPath, nodes: controlNodes,
    why: 'Non-default scales on AlphaPanel/BetaBranch/OmegaPanel; a zero-reading accessor cannot pass by luck.',
  }));
  checks.push(checkOffsetAccessor(expected, result, byPath));
  checks.push(checkVisible(expected, result, byPath));

  // -- (c) names and text, decoded exactly ----------------------------------
  checks.push(checkNames(expected, result, byPath));
  for (const [kind, id] of [['ascii', 'strings.text.ascii'], ['unicode', 'strings.text.unicode'], ['rich', 'strings.text.rich']]) {
    checks.push(checkText(id, kind, expected, byPath));
  }

  checks.push(checkTextAbsent(expected, result));
  checks.push(checkTextWrong(expected, byPath));
  checks.push(checkGeometryAbsent(expected, byPath));

  // -- distinct nodes are not collapsed -------------------------------------
  checks.push(checkNoCollapse(expected, byPath));

  // -- (e) full-tree walk ----------------------------------------------------
  checks.push(checkWalkCount(expected, result, ready, structureErrors, walk));

  // -- (d) profile agreement -------------------------------------------------
  checks.push(checkProfileAgreement(cell, profile, result));

  // -- managed bridge (dotnet cells only) -----------------------------------
  checks.push(checkManagedBridge(cell, expected, result, byPath));

  const counts = { pass: 0, fail: 0, skip: 0 };
  for (const c of checks) counts[c.status]++;
  return { checks, counts, normalised: result };
}

// ---------------------------------------------------------------------------
// Individual checks
// ---------------------------------------------------------------------------

function checkRuntimeAxes(cell, expected, ready, walk) {
  const id = 'harness.runtime_axes';
  const title = 'target identifies itself as the cell it is filed under';
  if (!ready) {
    return skip(id, title, 'no ready file (target was not launched by this harness)');
  }
  const problems = [];
  if (ready.contract !== expected.readyContract) {
    problems.push(`ready contract "${ready.contract}" != "${expected.readyContract}" (Probe.cs/Probe.gd out of sync)`);
  }
  // The engine's own tree has to agree with the authored one everywhere the
  // authored one speaks; extras are allowed only where the engine calls them
  // internal children. A disagreement here poisons every structural check below.
  for (const problem of walk.problems) {
    problems.push(`raw tree vs authored scene: ${problem}`);
  }
  if (ready.templateVariant !== cell.template) {
    problems.push(`OS.has_feature reports template "${ready.templateVariant}", cell says "${cell.template}"`);
  }
  if (ready.precision !== cell.precision) {
    problems.push(`runtime precision "${ready.precision}", cell says "${cell.precision}"`);
  }
  if (ready.binding !== cell.binding) {
    problems.push(`runtime binding "${ready.binding}", cell says "${cell.binding}"`);
  }
  if (ready.isTemplate === false) {
    problems.push('target is not an export template build (OS.has_feature("template") is false)');
  }
  if (cell.version && ready.engineVersion && !String(ready.engineVersion).startsWith(cell.versionPrefix)) {
    problems.push(`engine reports ${ready.engineVersion}, cell is ${cell.version}`);
  }
  return problems.length
    ? fail(id, title, `mislabelled cell — every result under it is untrustworthy:\n    - ${problems.join('\n    - ')}`, { ready })
    : pass(id, title,
      `${ready.engineVersion} ${ready.templateVariant}/${ready.precision}/${ready.binding}`
      + (walk.available
        ? `; raw tree ${walk.nodeCount} nodes = ${expected.nodeCount} authored + ${walk.internalNames.length} engine-internal`
        + `${walk.internalNames.length ? ` (${walk.internalNames.join(', ')})` : ''}`
        : ''),
      { ready });
}

function checkUnaided(result) {
  const id = 'calibration.unaided';
  const title = 'calibrator solved the layout without a shipped profile';
  if (result.usedProfile) {
    return fail(id, title,
      'driver reports usedProfile=true. §8.9 exists to show the calibrator solves layouts it has '
      + 'NEVER SEEN; a profile-assisted run is not evidence of that, whatever it scores.');
  }
  if (result.profileConsulted) {
    return fail(id, title, `driver consulted profile "${result.profileConsulted}"`);
  }
  return pass(id, title, 'no shipped offsets consumed');
}

function checkChildHead(expected, result, byPath, structureErrors, walk) {
  const id = 'structural.child_head';
  const title = '(a) child-list head derived by pointer identity';
  const methodFail = requireDerivationMethod(id, title, result.structural.method, STRUCTURAL_METHOD,
    'A structural offset that was guessed, scanned by value, or looked up is not the §12.5 result.');
  if (methodFail) return methodFail;

  const head = result.structural.offsets['node.childListHead'];
  if (head === null || head === undefined) {
    return fail(id, title, 'driver did not report derivation.structural.offsets["node.childListHead"]');
  }
  if (structureErrors.length) {
    return fail(id, title, `child-list offset ${hex(head)} reported, but the walk is not a tree: ${structureErrors.join('; ')}`);
  }

  const problems = [];
  for (const exp of expected.nodes) {
    const got = byPath.get(exp.path);
    if (!got) { problems.push(`${exp.path}: not reached by the walk`); continue; }
    const gotNames = got.childPtrs.map((p) => result.nodes.find((n) => n.nativePtr === p)?.name ?? `<unresolved ${p}>`);
    // The child list in memory carries the engine's internal children too, and
    // rawtree.mjs has already proved that list contains the authored children in
    // the authored order. Comparing against it keeps the ordering assertion whole
    // instead of excusing a mismatch (lib/rawtree.mjs).
    const wantNames = walk.childrenByPath.get(exp.path) ?? exp.childPaths.map((p) => p.split('/').pop());
    if (gotNames.length !== wantNames.length || gotNames.some((n, i) => n !== wantNames[i])) {
      problems.push(`${exp.path}: children [${gotNames.join(', ')}] != [${wantNames.join(', ')}]`);
    }
  }
  return problems.length
    ? fail(id, title, `child list at ${hex(head)} does not reproduce the authored tree:\n    - ${problems.slice(0, 8).join('\n    - ')}`, { head })
    : pass(id, title, `head ${hex(head)}, next ${hex(result.walk['childList.next'])}, node ${hex(result.walk['childList.node'])} — ${expected.nodeCount} nodes, sibling counts ${expected.siblingCounts.map((s) => s.children).join('/')}`, { head });
}

function checkParent(expected, result, byPath) {
  const id = 'structural.parent';
  const title = '(a) parent pointer derived by pointer identity';
  const methodFail = requireDerivationMethod(id, title, result.structural.method, STRUCTURAL_METHOD);
  if (methodFail) return methodFail;

  const parentOff = result.structural.offsets['node.parent'];
  if (parentOff === null || parentOff === undefined) {
    return fail(id, title, 'driver did not report derivation.structural.offsets["node.parent"]');
  }

  const problems = [];
  for (const exp of expected.nodes) {
    const got = byPath.get(exp.path);
    if (!got) { problems.push(`${exp.path}: missing`); continue; }
    if (exp.parentPath === null) continue;
    const wantParent = byPath.get(exp.parentPath);
    if (!wantParent) { problems.push(`${exp.path}: parent ${exp.parentPath} missing`); continue; }
    if (got.parentPtr !== wantParent.nativePtr) {
      problems.push(`${exp.path}: parent ${got.parentPtr} != ${wantParent.nativePtr}`);
    }
  }
  return problems.length
    ? fail(id, title, `parent at ${hex(parentOff)} disagrees with the child lists:\n    - ${problems.slice(0, 8).join('\n    - ')}`, { parentOff })
    : pass(id, title, `parent ${hex(parentOff)} round-trips against the child list for all ${expected.nodeCount} nodes`, { parentOff });
}

function semanticPreamble(id, title, result, offsetKey) {
  const methodFail = requireDerivationMethod(id, title, result.semantic.method, SEMANTIC_METHOD,
    'A hardcoded or single-sample offset is not the §12.5 technique.');
  if (methodFail) return { stop: methodFail };

  const samples = result.semantic.samples?.[offsetKey] ?? result.semantic.samples;
  const sampleCount = Array.isArray(samples) ? samples.length : (typeof samples === 'number' ? samples : null);
  if (sampleCount !== null && sampleCount < 2) {
    return {
      stop: fail(id, title,
        `derived from ${sampleCount} sample(s). §12.5: one control gave four candidate offsets; `
        + 'intersection across at least two is what makes the answer unique.'),
    };
  }
  const off = result.semantic.offsets[offsetKey];
  if (off === null || off === undefined) {
    return { stop: fail(id, title, `driver did not report derivation.semantic.offsets["${offsetKey}"]`) };
  }
  return { off, sampleCount };
}

function checkVectorAccessor({ id, title, key, offsetKey, arity, expected, result, byPath, nodes, why }) {
  const pre = semanticPreamble(id, title, result, offsetKey);
  if (pre.stop) return pre.stop;

  const problems = [];
  for (const exp of nodes) {
    const got = byPath.get(exp.path);
    if (!got) { problems.push(`${exp.path}: not reached`); continue; }
    const wanted = exp[key];
    const actual = got[key];
    if (!vectorsEqual(actual, wanted, EPS)) {
      problems.push(`${exp.path}: ${fmtVec(actual)} != ${fmtVec(wanted)}`);
    }
  }
  const detail = `${offsetKey} ${hex(pre.off)}${pre.sampleCount ? `, ${pre.sampleCount} samples` : ''}`;
  return problems.length
    ? fail(id, title, `${detail} — ${problems.length}/${nodes.length} nodes disagree:\n    - ${problems.slice(0, 8).join('\n    - ')}${why ? `\n    ${why}` : ''}`, { offset: pre.off })
    : pass(id, title, `${detail}, ${nodes.length}/${nodes.length} nodes exact`, { offset: pre.off });
}

function checkOffsetAccessor(expected, result, byPath) {
  const id = 'semantic.offset';
  const title = '(b) anchor offsets derived + read correctly';
  const pre = semanticPreamble(id, title, result, 'control.offset');
  if (pre.stop) return pre.stop;

  const controls = expected.nodes.filter((n) => n.isControl);
  const problems = [];
  for (const exp of controls) {
    const got = byPath.get(exp.path);
    if (!got) { problems.push(`${exp.path}: not reached`); continue; }
    if (!vectorsEqual(got.offset, exp.offset, EPS)) {
      problems.push(`${exp.path}: ${fmtVec(got.offset)} != ${fmtVec(exp.offset)}`);
    }
  }

  // The anchor/offset confusion trap. Control::Data lays anchor[4] immediately
  // after offset[4] (§4.6). On a scene where every anchor is 0, reading the
  // wrong one of the two looks fine. AnchoredWide has anchors of 0.5 and
  // offsets that are NOT its rect, so it separates them.
  const anchored = expected.nodes.filter((n) => n.anchored);
  const trap = [];
  for (const exp of anchored) {
    const got = byPath.get(exp.path);
    if (!got) continue;
    if (vectorsEqual(got.offset, exp.anchors, EPS)) {
      trap.push(`${exp.path}: read ${fmtVec(got.offset)} which is Data.anchor[4], not Data.offset[4]`);
    }
    if (vectorsEqual(got.offset, [...exp.position, ...exp.size], EPS)) {
      trap.push(`${exp.path}: read the resolved rect instead of the raw offsets`);
    }
  }

  if (problems.length || trap.length) {
    return fail(id, title,
      `control.offset ${hex(pre.off)} — ${problems.length}/${controls.length} nodes disagree`
      + (problems.length ? `:\n    - ${problems.slice(0, 8).join('\n    - ')}` : '')
      + (trap.length ? `\n    ANCHOR/OFFSET CONFUSION:\n    - ${trap.join('\n    - ')}` : ''),
      { offset: pre.off });
  }
  return pass(id, title,
    `control.offset ${hex(pre.off)}, ${controls.length}/${controls.length} nodes exact, `
    + `including ${anchored.length} node(s) with non-zero anchors that separate Data.offset from Data.anchor`,
    { offset: pre.off });
}

function checkVisible(expected, result, byPath) {
  const id = 'semantic.visible';
  const title = '(b) visible flag derived + read correctly';
  const pre = semanticPreamble(id, title, result, 'canvasItem.visible');
  if (pre.stop) return pre.stop;

  // `visible` lives on CanvasItem. expected.json records null for a node that has
  // no CanvasItem in it at all (the bare-Node parent tie-breaker), and asserting
  // a flag that does not exist would be a bug in the harness, not a finding.
  const canvasItems = expected.nodes.filter((n) => n.visible !== null);
  const problems = [];
  for (const exp of canvasItems) {
    const got = byPath.get(exp.path);
    if (!got) { problems.push(`${exp.path}: not reached`); continue; }
    if (got.visible !== exp.visible) problems.push(`${exp.path}: ${got.visible} != ${exp.visible}`);
  }

  const hidden = byPath.get(expected.visibility.hiddenPath);
  const visible = byPath.get(expected.visibility.visiblePath);
  if (hidden && visible && hidden.visible === visible.visible) {
    problems.push('HiddenTwin and VisibleTwin read the same — the flag byte was not actually located');
  }
  return problems.length
    ? fail(id, title, `canvasItem.visible ${hex(pre.off)} disagrees:\n    - ${problems.slice(0, 8).join('\n    - ')}`, { offset: pre.off })
    : pass(id, title, `canvasItem.visible ${hex(pre.off)}, ${canvasItems.length}/${canvasItems.length} CanvasItem nodes exact (Hidden/Visible twins separated)`, { offset: pre.off });
}

function checkNames(expected, result, byPath) {
  const id = 'strings.names';
  const title = '(c) every node name decoded exactly';
  const nameOff = result.strings.offsets['node.name'];
  const problems = [];
  for (const exp of expected.nodes) {
    const got = byPath.get(exp.path);
    if (!got) { problems.push(`${exp.path}: not reached`); continue; }
    if (got.name !== exp.name) problems.push(`${exp.path}: "${escapeText(got.name)}" != "${exp.name}"`);
  }
  return problems.length
    ? fail(id, title, `node.name ${hex(nameOff)}:\n    - ${problems.slice(0, 8).join('\n    - ')}`, { offset: nameOff })
    : pass(id, title, `node.name ${hex(nameOff)}, ${expected.nodeCount}/${expected.nodeCount} StringNames exact`, { offset: nameOff });
}

/**
 * Every node the scene authors WITHOUT text must report null.
 *
 * The three Zeta* checks only ever look at the nodes that DO have text, so a driver that invents a
 * string for a node with no text member scores exactly the same as one that does not. That is not a
 * hypothetical: a real series published "res://Probe.gd", "_process" and "@implicit_new" on plain
 * Controls, and in one run another node's NAME, while the same result reported no text offset at
 * all. Sixteen green cells would not have shown it.
 *
 * A wrong value is worse than a missing one — it is the difference between a table you can quote and
 * one you cannot — so this is scored, not merely noted.
 */
function checkTextAbsent(expected, result) {
  const id = 'strings.text.absent';
  const title = '(c) nodes with no text member report null';

  // Iterate the WALK, not expected.json. A driver reports every node it reached, and the walk
  // legitimately contains engine-internal children the authored scene never mentions — scrollbars
  // inside a RichTextLabel, and so on. Those have no authored text either, so they must report null
  // like any other; checking only the authored list left exactly those nodes uninspected, and the
  // literal defect this check exists for (`@VScrollBar@2` handed another node's NAME) sailed past it.
  const problems = [];
  let inspected = 0;

  for (const got of result.nodes) {
    if (!got.path) continue; // an unreconstructable path is structure's business, not this check's
    const exp = expected.byPath.get(got.path);
    if (exp && exp.text !== null) continue; // this one is supposed to have text
    inspected++;
    if (got.text !== null) {
      const what = exp ? exp.class : 'engine-internal, not in expected.json';
      problems.push(`${got.path} (${what}) has no text member but the driver reported "${escapeText(got.text)}"`);
    }
  }

  if (!inspected) {
    return skip(id, title, 'the walk reached no text-less node; nothing to assert');
  }

  // Denominator from what was actually inspected. Hardcoding the authored count let a driver drop
  // sixteen of seventeen text-less nodes from its walk and still be told "17/17 reported null".
  return problems.length
    ? fail(id, title,
      `${problems.length}/${inspected} text-less nodes were given a string:\n    - ${problems.slice(0, 8).join('\n    - ')}\n`
      + '    A value invented for a node that cannot have one costs more than a missing one: it means no\n'
      + '    reading in the result can be taken at face value.', { count: problems.length, inspected })
    : pass(id, title, `${inspected}/${inspected} walked text-less nodes reported null`);
}

/**
 * A node that HAS authored text must report it byte-exactly, or report null.
 *
 * The three Zeta* checks score "wrong" and "missing" identically, so the property that actually
 * decides whether this table can be quoted — absent, never wrong — was not measurable from a
 * result. It had to be reconstructed by eye from per-node values, and a series that published six
 * wrong strings against two correct ones read as a coverage problem rather than a correctness one.
 *
 * Missing text passes here on purpose: it is already penalised by the checks above, and conflating
 * the two is what hid this.
 */
/**
 * A node with no CanvasItem base must report no geometry and no visibility.
 *
 * The geometry checks all filter to `expected.nodes.filter(n => n.isControl)`, so the non-Controls
 * were never looked at — and a driver that invented `visible=true`, `size=[0,0]`, `scale=[0,0]` for
 * an object with no CanvasItem at all scored full marks through an entire series. Zeros are the
 * shape of garbage that plausibility cannot catch, which is precisely why this needs asserting
 * rather than assuming.
 *
 * Unlike the text equivalent this iterates the AUTHORED nodes only: "not in expected.json" implies
 * no authored text, but it does not imply no geometry — the engine-internal children in the walk are
 * mostly Controls and legitimately have some.
 */
function checkGeometryAbsent(expected, byPath) {
  const id = 'geometry.absent';
  const title = '(b) nodes with no CanvasItem report no geometry';
  const bare = expected.nodes.filter((n) => !n.isControl);
  if (!bare.length) {
    return skip(id, title, 'every authored node is a Control; nothing to assert');
  }

  const fields = ['visible', 'size', 'position', 'scale', 'offset'];
  const problems = [];
  let inspected = 0;

  for (const exp of bare) {
    const got = byPath.get(exp.path);
    if (!got) continue;
    inspected++;
    for (const field of fields) {
      if (got[field] !== null && got[field] !== undefined) {
        const shown = Array.isArray(got[field]) ? fmtVec(got[field]) : String(got[field]);
        problems.push(`${exp.path} (${exp.class}) has no CanvasItem but reported ${field}=${shown}`);
      }
    }
  }

  if (!inspected) {
    return skip(id, title, 'no non-Control node was reached by the walk');
  }

  return problems.length
    ? fail(id, title,
      `${problems.length} field(s) invented across ${inspected} non-Control node(s):\n    - ${problems.slice(0, 8).join('\n    - ')}\n`
      + '    Reading a Control field off a non-Control succeeds and returns garbage (§12.4c), and the\n'
      + '    garbage is often all zeros — which no plausibility test can reject.', { inspected })
    : pass(id, title, `${inspected}/${inspected} non-Control node(s) reported no geometry`, { inspected });
}

function checkTextWrong(expected, byPath) {
  const id = 'strings.text.wrong';
  const title = '(c) authored text is exact where present, never invented';
  const authored = expected.nodes.filter((n) => n.text !== null);
  if (!authored.length) {
    return skip(id, title, 'the scene authors no text; nothing to assert');
  }

  const problems = [];
  let present = 0;
  for (const exp of authored) {
    const got = byPath.get(exp.path);
    if (!got || got.text === null) continue; // absent is the other checks' business
    present++;
    if (got.text !== exp.text) {
      problems.push(`${exp.path}: reported "${escapeText(got.text)}", authored "${escapeText(exp.text)}"`);
    }
  }

  if (problems.length) {
    return fail(id, title,
      `${problems.length}/${present} reported string(s) are not what the scene authored:\n    - ${problems.join('\n    - ')}\n`
      + '    A wrong string is a different failure from a missing one — it means the offset behind it is\n'
      + '    wrong, and every other reading taken through that offset is suspect too.',
      { present, wrong: problems.length });
  }

  // With nothing published there is nothing to be exact ABOUT. Reporting that as a pass is the
  // failure mode this harness's own honesty rule names first, and it made a cell that published no
  // text at all indistinguishable from one that published every string correctly.
  if (!present) {
    return skip(id, title, `no text was reported at all; ${authored.length} authored string(s) withheld`, { present: 0 });
  }

  return pass(id, title,
    `${present}/${present} reported string(s) byte-exact (${authored.length - present} withheld)`,
    { present, wrong: 0 });
}

function checkText(id, kind, expected, byPath) {
  const spec = expected.strings[kind];
  const title = `(c) ${kind} text decoded exactly${spec.hasNonAscii ? ' (non-ASCII)' : ''}`;
  const got = byPath.get(spec.path);
  if (!got) return fail(id, title, `${spec.path} was not reached by the walk`);
  if (got.text === null) return fail(id, title, `${spec.path}: driver reported no text`);
  if (got.text === spec.text) {
    return pass(id, title,
      `"${escapeText(spec.text)}" — ${spec.codepointCount} codepoints, max U+${spec.maxCodepoint.toString(16).toUpperCase()}`
      + `${spec.hasAstral ? ', includes an astral codepoint (surrogate pair in UTF-16)' : ''}`);
  }

  // Name the §4.6 bug specifically if that is what happened, rather than
  // reporting a generic mismatch a reader has to diagnose by eye.
  let diagnosis = '';
  if (got.text === spec.lossyByteTruncation) {
    diagnosis = '\n    DIAGNOSIS: the decoder truncates char32_t -> byte, exactly the §4.6 scry bug. '
      + 'Godot String is CowData<char32_t>; bulk-read len*4 bytes and decode UTF-32 properly.';
  } else if ([...got.text].every((c) => c.codePointAt(0) < 0x80) && spec.hasNonAscii) {
    diagnosis = '\n    DIAGNOSIS: all non-ASCII was dropped — the decoder is treating the buffer as ASCII/UTF-8.';
  } else if (
    // A result that is BOTH far too short AND shares no prefix with the expected
    // text is a wrong ADDRESS, not a decoding bug. Observed live: ZetaRich decoded
    // to "�", "�翶", "�翶���" and "Ā" on
    // successive runs against an UNCHANGING scene. Varying garbage at a fixed target
    // means a different wrong candidate was selected each time. Test this BEFORE the
    // astral check below, which would otherwise fire on the same input and send the
    // reader to the surrogate-pair code — the wrong file entirely.
    got.text.length > 0
    && got.text.length * 4 < spec.utf16Length
    && got.text[0] !== spec.text[0]
  ) {
    diagnosis = `\n    DIAGNOSIS: got ${got.text.length} unit(s) against ${spec.utf16Length} expected, `
      + 'sharing no prefix — this is a WRONG ADDRESS, not a decoding bug. The candidate offset '
      + 'differs from the real one; if it varies between runs against the same scene, the '
      + 'calibrator is picking a different wrong candidate each time rather than truncating a '
      + 'correct read.';
  } else if (spec.hasAstral && got.text.length !== spec.utf16Length) {
    diagnosis = '\n    DIAGNOSIS: length differs on a string with an astral codepoint — check UTF-32 -> UTF-16 '
      + 'surrogate-pair conversion.';
  } else if (got.text === spec.text.slice(0, got.text.length)) {
    diagnosis = `\n    DIAGNOSIS: truncated at ${got.text.length} of ${spec.utf16Length} UTF-16 units — `
      + 'CowData stores [refcount][size] AHEAD of the buffer; length is read at buf-8 (§4.6).';
  }

  return fail(id, title,
    `${spec.path}\n    expected: "${escapeText(spec.text)}"  cp=${JSON.stringify(spec.codepoints)}\n`
    + `    actual:   "${escapeText(got.text)}"  cp=${JSON.stringify(codepoints(got.text))}${diagnosis}`);
}

function checkNoCollapse(expected, byPath) {
  const id = 'structure.no_collapse';
  const title = 'duplicated size does not collapse two distinct nodes';
  if (!expected.duplicateSizeGroups.length) {
    return fail(id, title, 'expected.json has no duplicate-size group — §8.9 requires one deliberately duplicated size');
  }
  const problems = [];
  for (const group of expected.duplicateSizeGroups) {
    const seen = new Map();
    for (const path of group.paths) {
      const got = byPath.get(path);
      if (!got) { problems.push(`${path}: not reached`); continue; }
      if (!got.nativePtr) { problems.push(`${path}: no native pointer reported`); continue; }
      if (seen.has(got.nativePtr)) {
        problems.push(`${path} and ${seen.get(got.nativePtr)} both resolved to ${got.nativePtr}`);
      }
      seen.set(got.nativePtr, path);
      if (!vectorsEqual(got.size, group.size, EPS)) {
        problems.push(`${path}: size ${fmtVec(got.size)} != ${fmtVec(group.size)}`);
      }
    }
  }
  return problems.length
    ? fail(id, title, problems.join('\n    - '))
    : pass(id, title, expected.duplicateSizeGroups
      .map((g) => `${fmtVec(g.size)} on ${g.paths.length} distinct nodes`).join('; '));
}

function checkWalkCount(expected, result, ready, structureErrors, walk) {
  const id = 'structure.walk_count';
  const title = '(e) full-tree walk count matches the authored scene';
  const problems = [];
  // A memory walk reaches the authored scene AND the engine's internal children;
  // walk.nodeCount is that total, and rawtree.mjs has already checked it against
  // the authored scene node by node.
  const want = walk.nodeCount;
  if (result.walkCount !== want) {
    problems.push(`driver walked ${result.walkCount} nodes, the target's child lists hold ${want}`);
  }
  if (result.nodes.length !== want) {
    problems.push(`driver reported ${result.nodes.length} node records, the target's child lists hold ${want}`);
  }
  if (ready && ready.walkCount !== expected.nodeCount) {
    problems.push(`the TARGET counted ${ready.walkCount} authored nodes in-process — expected.json is stale or the scene did not load fully`);
  }
  if (ready && walk.available && ready.rawWalkCount !== want) {
    problems.push(`the TARGET reported rawWalkCount ${ready.rawWalkCount} but listed ${want} nodes in rawTree`);
  }
  if (structureErrors.length) problems.push(...structureErrors);

  const depths = new Set(expected.nodes.map((n) => n.depth));
  if (expected.maxDepth < 6) {
    problems.push(`scene is only ${expected.maxDepth} levels deep; §8.9 requires >= 6`);
  }
  return problems.length
    ? fail(id, title, problems.join('\n    - '))
    : pass(id, title,
      `${want}/${want} nodes walked (${expected.nodeCount} authored`
      + `${walk.internalNames.length ? ` + ${walk.internalNames.length} engine-internal` : ''}), `
      + `${depths.size} distinct depths, max depth ${expected.maxDepth}`);
}

function checkProfileAgreement(cell, profile, result) {
  const id = 'profile.agreement';
  const title = '(d) derived offsets agree with the shipped profile';
  if (!profile) {
    return skip(id, title, `no shipped profile covers ${cell.name} — nothing to cross-check against, and nothing to fall back to`);
  }

  const derived = { ...result.structural.offsets, ...result.semantic.offsets, ...result.strings.offsets };
  const compared = [];
  const mismatched = [];
  const missing = [];
  for (const [key, want] of Object.entries(profile.offsets)) {
    if (key === 'control.anchor') continue; // not an accessor any calibrator is asked to derive
    const got = derived[key];
    if (got === null || got === undefined) { missing.push(key); continue; }
    compared.push(key);
    if (got !== want) mismatched.push({ key, got, want });
  }
  const unresolvable = [];
  for (const [key, wantRaw] of Object.entries(profile.walk ?? {})) {
    const got = result.walk[key];
    if (got === null || got === undefined) continue;

    // A walk entry may be a scalar, or a map from the C++ class that OWNS the field to its offset.
    // scriptInstance.ownerBackref is the only one that needs the second shape: it is a member of
    // CSharpInstance or GDScriptInstance, which are unrelated implementations of one interface, and
    // NOT of the engine object. So no single number can be right for both, and the axis is not the
    // cell's binding either — one mono build hosts .gd and .cs scripted nodes side by side, so the
    // question "which implementation is this node's?" is per-object and can only be answered by the
    // target. The driver reports the class it read off the ScriptInstance's own vtable.
    let want = wantRaw;
    if (wantRaw !== null && typeof wantRaw === 'object') {
      const owner = result.scriptInstanceClass;
      if (!owner) {
        unresolvable.push(`${key}: profile scopes it by implementing class, and the driver did not report one`);
        continue;
      }
      if (!(owner in wantRaw)) {
        unresolvable.push(`${key}: the driver read implementing class "${owner}", which this profile does not cover`);
        continue;
      }
      want = wantRaw[owner];
    }

    compared.push(key);
    if (got !== want) mismatched.push({ key, got, want });
  }

  if (!compared.length) {
    return fail(id, title, `profile ${profile.id} exists but the driver reported no comparable offsets (missing: ${missing.join(', ')})`);
  }

  // A key the profile cannot express for this target is not a disagreement, and must not be scored
  // as one — but it must not vanish either, or the table looks more complete than it is.
  const caveat = unresolvable.length ? `
    not compared: ${unresolvable.join('; ')}` : '';

  if (mismatched.length) {
    const lines = mismatched.map((m) => `${m.key}: derived ${hex(m.got)}, profile ${hex(m.want)}`);
    const reading = profile.trust === 'verified'
      ? 'This profile was live-validated (§12.3/§12.3b/§12.4c) BUT against a MODIFIED 4.5.1 engine, '
        + 'not a stock export template. Two readings: (a) the calibrator is wrong, or (b) the stock '
        + 'template\'s layout differs from the StS2 fork. Resolve it before quoting this cell as evidence — '
        + 'do NOT fall back to the profile.'
      : `This profile is marked trust="${profile.trust}" (${profile.verifiedAgainst}). §4.6 already records `
        + 'internal contradictions in it, so a disagreement here is more likely to indict the TABLE than the '
        + 'calibrator — which is exactly the measurement §8.9 asks for. Still a failure: it must be resolved, '
        + 'not silently accepted.';
    return fail(id, title, `${mismatched.length}/${compared.length} offsets disagree with ${profile.id}:\n    - ${lines.join('\n    - ')}\n    ${reading}`,
      { profileId: profile.id, trust: profile.trust, mismatched });
  }
  return pass(id, title, `${compared.length}/${compared.length} offsets match ${profile.id} (trust=${profile.trust})${caveat}`,
    { profileId: profile.id, trust: profile.trust });
}

function checkManagedBridge(cell, expected, result, byPath) {
  const id = 'bridge.managed';
  const title = 'managed static root resolves to the native walk root';
  if (cell.binding !== 'dotnet') {
    return skip(id, title, 'gdscript cell — there is no managed bridge to test');
  }
  const bridge = result.managedBridge;
  if (!bridge) {
    // The bridge is reached THROUGH node.scriptInstance. If the driver withheld that offset, this
    // check has no input, and an unevaluatable check is a skip — the harness's first honesty rule.
    // Scoring it as a failure punished a correct withhold and turned "I declined to guess" into
    // "the bridge is broken", which is exactly the pressure that makes a calibrator guess.
    if (result.structural.offsets['node.scriptInstance'] === null
      || result.structural.offsets['node.scriptInstance'] === undefined) {
      return skip(id, title,
        'the driver withheld node.scriptInstance, so there is no chain to follow to a managed object. '
        + 'Withholding an input must not manufacture a downstream failure.');
    }
    return fail(id, title, 'driver reported no managedBridge result for a .NET cell');
  }

  const problems = [];
  if (bridge.staticRootType !== expected.managedBridge.staticRootType) {
    problems.push(`resolved type "${bridge.staticRootType}" != "${expected.managedBridge.staticRootType}"`);
  }
  if (bridge.staticRootField !== expected.managedBridge.staticRootField) {
    problems.push(`resolved static "${bridge.staticRootField}" != "${expected.managedBridge.staticRootField}"`);
  }
  const rootPtr = byPath.get(expected.walkRoot)?.nativePtr ?? null;
  const bridgePtr = normPtr(bridge.nativePtr);
  if (!bridgePtr) problems.push('managed GodotObject.NativePtr was null or unparseable');
  else if (rootPtr && bridgePtr !== rootPtr) {
    problems.push(
      `NativePtr ${bridgePtr} != walk root ${rootPtr}. §4.6: passing the MANAGED address instead of `
      + 'NativePtr yields plausible-looking garbage (it once resolved to the string "is_visible"), '
      + 'so a near-miss here is the expected shape of this bug.');
  }

  // §4.6's native -> managed direction: Node +0x68 -> ScriptInstance, +0x08 owner
  // backref, +0x20 GCHandle -> managed object. Optional, but scored when reported.
  const back = bridge.reverse ?? null;
  if (back) {
    if (back.ownerBackref && normPtr(back.ownerBackref) !== rootPtr) {
      problems.push(`ScriptInstance +0x08 owner backref ${normPtr(back.ownerBackref)} != ${rootPtr} — wrong pointer followed`);
    }
  }

  const fields = bridge.fields ?? null;
  if (fields) {
    for (const [key, want] of Object.entries(expected.managedBridge.fields)) {
      if (!(key in fields)) continue;
      const got = fields[key];
      const same = typeof want === 'number' ? Math.abs(Number(got) - want) <= 1e-3 : got === want;
      if (!same) problems.push(`field ${key}: ${JSON.stringify(got)} != ${JSON.stringify(want)}`);
    }
  }

  return problems.length
    ? fail(id, title, problems.join('\n    - '))
    : pass(id, title, `${bridge.staticRootType}.${bridge.staticRootField} -> NativePtr ${bridgePtr} == walk root`
      + `${back ? ', reverse ScriptInstance chain verified' : ''}`
      + `${fields ? `, ${Object.keys(fields).length} managed field(s) read` : ''}`);
}
