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

const VECTOR_ARITY = { size: 2, position: 2, scale: 2, offset: 4, anchors: 4 };

/**
 * ABSENT and MALFORMED are different answers and the harness must not confuse them.
 *
 * Every per-node field here used to collapse to `null` on ANY input the parser could not use, so
 * `visible: 1` — the raw CanvasItem byte, which is exactly what a driver publishing an unconverted
 * memory read would emit — became indistinguishable from `visible` never being reported at all.
 * `geometry.absent` and `strings.text.absent` both assert "this node reported nothing"; both were
 * therefore satisfied by a node that reported garbage, which is the opposite of what they exist to
 * catch. Same shape for `size: [7]`, `anchors: [1,2,3]`, `size: "0,0"`, `{x:'a',y:'b'}` and a
 * non-string `text`.
 *
 * So a value that was PRESENT but unusable is recorded in `malformed` and the checks below score it.
 * Withholding a field stays legitimate; publishing rubbish in it never was.
 */
function normaliseNode(n) {
  const malformed = [];

  const vec = (key) => {
    const raw = n[key];
    if (raw === null || raw === undefined) return null;
    const v = toVector(raw, VECTOR_ARITY[key]);
    if (v === null) malformed.push(key);
    return v;
  };
  const bool = (key, raw) => {
    if (raw === null || raw === undefined) return null;
    if (typeof raw === 'boolean') return raw;
    malformed.push(key);
    return null;
  };
  const str = (key, raw) => {
    if (raw === null || raw === undefined) return null;
    if (typeof raw === 'string') return raw;
    malformed.push(key);
    return null;
  };

  return {
    raw: n,
    name: str('name', n.name),
    class: n.class ?? n.className ?? null,
    nativePtr: normPtr(n.nativePtr ?? n.ptr ?? n.address),
    parentPtr: normPtr(n.parentPtr ?? n.parent),
    childPtrs: (n.childPtrs ?? n.children ?? []).map(normPtr),
    size: vec('size'),
    position: vec('position'),
    scale: vec('scale'),
    offset: vec('offset'),
    anchors: vec('anchors'),
    visible: bool('visible', n.visible),
    text: str('text', n.text),
    reportedPath: n.path ?? null,
    malformed,
  };
}

/** How a malformed field is quoted back at the driver, so the defect is legible without the raw JSON. */
export function rawFieldText(node, field) {
  try { return JSON.stringify(node.raw?.[field]); } catch { return String(node.raw?.[field]); }
}

export function normaliseResult(raw) {
  const der = raw.derivation ?? {};
  const offsets = (o) => Object.fromEntries(Object.entries(o ?? {}).map(([k, v]) => [k, parseOffset(v)]));

  const nodes = (raw.nodes ?? []).map(normaliseNode);

  return {
    driver: raw.driver ?? 'unknown',
    driverVersion: raw.driverVersion ?? null,
    // Deliberately NOT coerced to a boolean here. `usedProfile === true` made every other value on
    // earth — "true", 1, or the key being absent — mean "unaided", so the one check that certifies
    // this whole grid as unaided evidence could only fail if the driver volunteered the exact literal
    // that fails it. checkUnaided does the typing, and refuses anything that is not a boolean.
    usedProfile: Object.hasOwn(raw, 'usedProfile') ? raw.usedProfile : undefined,
    profileConsulted: Object.hasOwn(raw, 'profileConsulted') ? raw.profileConsulted : undefined,
    engineVersion: raw.engineVersion ?? null,
    // Substituting nodes.length for an unusable walkCount made the two counts identical BY
    // CONSTRUCTION, which is the entire cross-check structure.walk_count exists to perform:
    // `walkCount: "25"` erased it silently. Absent or non-integer is now null and scored.
    walkCount: Number.isInteger(raw.walkCount) ? raw.walkCount : null,
    walkCountRaw: Object.hasOwn(raw, 'walkCount') ? raw.walkCount : undefined,
    structural: { method: der.structural?.method ?? null, offsets: offsets(der.structural?.offsets), evidence: der.structural?.evidence ?? null },
    semantic: {
      method: der.semantic?.method ?? null,
      offsets: offsets(der.semantic?.offsets),
      samples: der.semantic?.samples ?? null,
      candidates: der.semantic?.candidates ?? null,
    },
    strings: { method: der.strings?.method ?? null, offsets: offsets(der.strings?.offsets) },
    walk: offsets(der.walk?.offsets ?? der.walk),
    notDerived: der.notDerived ?? {},
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
// The registered check list — ONE copy, and REPORT.md reads it from here.
//
// lib/report.mjs used to carry a hand-maintained parallel array of the same ids. It listed 17 while
// runChecks scored 21, so REPORT.md's header said "Checks per cell: 17" over cells scoring out of 21
// and the per-cell tables could not show whether `geometry.absent` had passed at all. The four
// missing entries were precisely the ones added to fix §13.11 #6 — the checks written to close a
// blind spot were invisible in the document that reports blind spots.
//
// A second copy of a list is the same species as everything else in §13.11: nothing forced the two
// to agree. runChecks now emits exactly this catalogue, in this order, and throws if it does not —
// a drift is a harness bug and must be loud rather than a quietly shorter table.
// ---------------------------------------------------------------------------

export const CHECK_CATALOG = [
  ['harness.runtime_axes', 'target self-identifies as the cell it is filed under'],
  ['calibration.unaided', 'no shipped profile consumed, and the driver states so as a boolean'],
  ['structural.child_head', '(a) child-list head by pointer identity'],
  ['structural.parent', '(a) parent round-trips against the child list'],
  ['offsets.internal_consistency', 'derived offsets are mutually consistent with single inheritance'],
  ['semantic.size', '(b) size by known-value intersection'],
  ['semantic.position', '(b) position'],
  ['semantic.scale', '(b) scale'],
  ['semantic.offset', '(b) anchor offsets'],
  ['semantic.visible', '(b) visible flag'],
  ['strings.names', '(c) node names exact, matched by child-list position'],
  ['strings.text.ascii', '(c) ASCII label text exact'],
  ['strings.text.unicode', '(c) non-ASCII label text exact'],
  ['strings.text.rich', '(c) RichTextLabel text exact (astral codepoint)'],
  ['strings.text.richBbcode', '(c) raw BBCode source exact, not the rendered text'],
  ['strings.text.absent', '(c) nodes with no text member report null, not garbage'],
  ['strings.text.wrong', '(c) authored text is exact where present, never invented'],
  ['geometry.absent', '(b) nodes with no CanvasItem report no geometry, not garbage'],
  ['structure.no_collapse', 'duplicated size does not collapse nodes'],
  ['structure.walk_count', '(e) full-tree walk count'],
  ['profile.agreement', '(d) agreement with the shipped §4.6 profile'],
  ['bridge.managed', 'managed static root -> NativePtr -> walk root, and the field values'],
];

const CATALOG_IDS = CHECK_CATALOG.map(([id]) => id);

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

  // Node identity taken from the CHILD LISTS alone — see resolvePositions. Two checks below need a
  // handle on a node that was not derived from the thing they are about to assert.
  const positions = resolvePositions(expected, result, walk);

  // -- runtime axes agree with the cell name --------------------------------
  checks.push(checkRuntimeAxes(cell, expected, ready, walk));

  // -- the calibrator ran unaided -------------------------------------------
  checks.push(checkUnaided(result));

  // -- (a) structural, by pointer identity ----------------------------------
  checks.push(checkChildHead(expected, result, byPath, structureErrors, walk));
  checks.push(checkParent(expected, result, positions));

  // -- offsets are internally consistent with the class hierarchy ------------
  checks.push(checkOffsetConsistency(cell, result));

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
  checks.push(checkNames(result, walk, positions));
  // richBbcode was generated by gen-expected and registered nowhere, so OmegaRich's own reading had no
  // check a WITHHOLD could fail: reporting the rendered text instead of the raw source failed
  // strings.text.wrong, but dropping the node entirely scored 20/20. It carries the raw-vs-rendered
  // BBCode trap, which is the one thing it exists to test.
  for (const [kind, id] of [['ascii', 'strings.text.ascii'], ['unicode', 'strings.text.unicode'], ['rich', 'strings.text.rich'], ['richBbcode', 'strings.text.richBbcode']]) {
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

  const ordered = orderByCatalog(checks);
  const counts = { pass: 0, fail: 0, skip: 0 };
  for (const c of ordered) counts[c.status]++;
  return { checks: ordered, counts, normalised: result };
}

/**
 * Emit the checks in CHECK_CATALOG order and refuse to run if the two have drifted.
 *
 * The point is that there is now no way to add a check without it appearing in REPORT.md, and no way
 * for REPORT.md to list a check that is not scored. Both directions are enforced, because the
 * reporting defect this replaces was a MISSING entry, and the mirror-image bug — a legend row for a
 * check nobody runs — reads exactly like coverage.
 */
function orderByCatalog(checks) {
  const byId = new Map();
  for (const c of checks) {
    if (!c) throw new Error('checks.mjs: a check function returned nothing');
    if (byId.has(c.id)) throw new Error(`checks.mjs: duplicate check id "${c.id}"`);
    byId.set(c.id, c);
  }
  const unregistered = [...byId.keys()].filter((id) => !CATALOG_IDS.includes(id));
  const unproduced = CATALOG_IDS.filter((id) => !byId.has(id));
  if (unregistered.length || unproduced.length) {
    throw new Error(
      'checks.mjs: CHECK_CATALOG and the checks actually run have drifted — REPORT.md is generated '
      + 'from the catalogue, so this would publish a table that silently omits or invents checks.'
      + (unregistered.length ? `\n  run but not registered: ${unregistered.join(', ')}` : '')
      + (unproduced.length ? `\n  registered but not run: ${unproduced.join(', ')}` : ''));
  }
  return CATALOG_IDS.map((id) => byId.get(id));
}

/**
 * Identify every walked node by its POSITION in the child lists, never by the name it reports.
 *
 * Two checks claimed to assert something they could not. `strings.names` looked up each node in a
 * map keyed on the reconstructed path — and the path is BUILT from `node.name` (reconstructPaths),
 * so `byPath.get(exp.path).name === exp.name` was a tautology and every failure it ever produced
 * read `not reached`. `structural.parent` had the same shape one layer down: path reconstruction
 * consumes `parentPtr`, so a wrong parent relocated the node and the comparison never ran.
 *
 * This descends the driver's own `childPtrs` from the walk root, pairing each child pointer with the
 * name at that ORDINAL in the tree the target itself reported (lib/rawtree.mjs), and thereby assigns
 * every pointer an expected path without consulting a single name or parent pointer from the driver.
 * The two checks then have an independent handle to compare against.
 *
 * The root is the node no child list mentions — deliberately not "the node with no parent", which
 * is the value `structural.parent` is about to be tested on.
 */
function resolvePositions(expected, result, walk) {
  const byPtr = new Map();
  for (const n of result.nodes) if (n.nativePtr) byPtr.set(n.nativePtr, n);

  const claimed = new Set();
  for (const n of result.nodes) for (const c of n.childPtrs) if (c) claimed.add(c);
  const roots = [...byPtr.keys()].filter((p) => !claimed.has(p));

  const pathByPtr = new Map();
  const ptrByPath = new Map();
  if (roots.length !== 1) {
    return { available: false, reason: `${roots.length} node(s) appear in no child list, so the walk root is not identifiable by position`, pathByPtr, ptrByPath, byPtr };
  }

  const stack = [[roots[0], expected.walkRoot]];
  while (stack.length) {
    const [ptr, path] = stack.pop();
    if (pathByPtr.has(ptr)) continue;
    pathByPtr.set(ptr, path);
    if (!ptrByPath.has(path)) ptrByPath.set(path, ptr);
    const node = byPtr.get(ptr);
    if (!node) continue;
    const wantChildren = walk.childrenByPath.get(path);
    if (!wantChildren) continue;
    node.childPtrs.forEach((childPtr, i) => {
      if (childPtr && i < wantChildren.length) stack.push([childPtr, `${path}/${wantChildren[i]}`]);
    });
  }
  return { available: true, reason: null, pathByPtr, ptrByPath, byPtr };
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

/**
 * The check that certifies this entire grid as unaided evidence — so its type test must be strict in
 * the direction that REFUSES, not the one that passes.
 *
 * It was `raw.usedProfile === true`. Every other value passed: `usedProfile: "true"` passed, and so
 * did omitting the key altogether. A driver that never made the claim was certified as having made
 * it, which is the one thing this check may not do. The empty-string branch of `profileConsulted`
 * had never been exercised at all.
 *
 * A calibrator that cannot state, as a boolean, whether it consumed a shipped profile is not a
 * calibrator whose runs can be published as evidence that it needed none.
 */
function checkUnaided(result) {
  const id = 'calibration.unaided';
  const title = 'calibrator solved the layout without a shipped profile';

  if (result.usedProfile === undefined) {
    return fail(id, title,
      'the driver did not report `usedProfile` at all. Absence is not a claim: this cell cannot be '
      + 'published as evidence that the calibrator needed no shipped offsets, because nothing in the '
      + 'result says it did not use any.');
  }
  if (typeof result.usedProfile !== 'boolean') {
    return fail(id, title,
      `the driver reported usedProfile=${JSON.stringify(result.usedProfile)}, which is not a boolean. `
      + 'The claim being certified here is binary and the driver must state it as one — a string, a '
      + 'number or a null reads as "unaided" to any test written the lenient way, and this check was '
      + 'written the lenient way for 224 runs.');
  }
  if (result.usedProfile === true) {
    return fail(id, title,
      'driver reports usedProfile=true. §8.9 exists to show the calibrator solves layouts it has '
      + 'NEVER SEEN; a profile-assisted run is not evidence of that, whatever it scores.');
  }

  const consulted = result.profileConsulted;
  if (consulted !== undefined && consulted !== null) {
    if (typeof consulted !== 'string') {
      return fail(id, title, `driver reported profileConsulted=${JSON.stringify(consulted)}, which names no profile`);
    }
    if (consulted.trim() === '') {
      return fail(id, title,
        'driver reported profileConsulted as an empty string. That is not "none" — it is a field the '
        + 'driver filled in and could not name, and it is indistinguishable from a profile lookup '
        + 'whose id was lost. Report null when nothing was consulted.');
    }
    return fail(id, title, `driver consulted profile "${consulted}"`);
  }

  return pass(id, title, 'driver states usedProfile=false; no shipped offsets consumed');
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

/**
 * The detail line has always claimed the parent pointer "round-trips against the child list". It did
 * not. Both sides of the old comparison came out of `byPath`, which is keyed on a path reconstructed
 * FROM `parentPtr` — so a wrong parent moved the node to a different path, `byPath.get(exp.path)`
 * returned nothing, and the loop reported `missing` and never reached the comparison at all. The
 * check still reddened, via a different clause, so this was "the assertion you think is running is
 * not" rather than a false green — but the assertion is worth having, so it is now made against
 * `positions`, which is built from the CHILD LISTS and never touches `parentPtr`.
 */
function checkParent(expected, result, positions) {
  const id = 'structural.parent';
  const title = '(a) parent pointer derived by pointer identity';
  const methodFail = requireDerivationMethod(id, title, result.structural.method, STRUCTURAL_METHOD);
  if (methodFail) return methodFail;

  const parentOff = result.structural.offsets['node.parent'];
  if (parentOff === null || parentOff === undefined) {
    return fail(id, title, 'driver did not report derivation.structural.offsets["node.parent"]');
  }
  if (!positions.available) {
    return fail(id, title,
      `parent ${hex(parentOff)} cannot be cross-checked: ${positions.reason}. A parent pointer is only `
      + 'evidence if something other than itself says where the node sits.');
  }

  const problems = [];
  let compared = 0;
  for (const exp of expected.nodes) {
    const ptr = positions.ptrByPath.get(exp.path);
    if (!ptr) { problems.push(`${exp.path}: no node occupies this position in the child lists`); continue; }
    const got = positions.byPtr.get(ptr);
    if (!got) { problems.push(`${exp.path}: child list points at ${ptr}, which the driver reported no record for`); continue; }
    if (exp.parentPath === null) continue;
    const wantPtr = positions.ptrByPath.get(exp.parentPath) ?? null;
    if (!wantPtr) { problems.push(`${exp.path}: parent ${exp.parentPath} has no position in the child lists`); continue; }
    compared++;
    if (got.parentPtr !== wantPtr) {
      problems.push(`${exp.path}: parent ${got.parentPtr ?? 'null'} != ${wantPtr}, the node whose child list holds it`);
    }
  }
  return problems.length
    ? fail(id, title, `parent at ${hex(parentOff)} disagrees with the child lists:\n    - ${problems.slice(0, 8).join('\n    - ')}`, { parentOff })
    : pass(id, title,
      `parent ${hex(parentOff)} round-trips against the child list for ${compared} of ${expected.nodeCount} nodes `
      + '(the root has no parent to check)', { parentOff, compared });
}

// ---------------------------------------------------------------------------
// (f) the derived offsets are internally consistent
//
// THE PROBLEM THIS EXISTS FOR. Replace every derived offset on a 4.3 cell with 0x1 while leaving all
// the READINGS correct and the cell scored 19/19 with zero failures. On the 4.5.1 cell exactly one
// check went red: profile.agreement. So `profile.agreement` carried the entire offset-correctness
// burden of this grid — and it does not run on the four 4.3 cells, where no profile ships. Half the
// matrix was not testing the claim the matrix exists to test.
//
// The mutation is not reachable by a real driver (one that derives offsets and then reads THROUGH
// them cannot easily land on wrong offsets and right readings), so this indicts no past result. It
// indicts the measurement.
//
// The fix cannot be to transcribe the calibrator's own output into profiles.json: §13.11's corollary
// is explicit that a profile built from the thing it checks is anti-evidence, "a check that cannot
// fail, installed deliberately". What CAN be asserted without an answer key is that the numbers are
// mutually consistent with the C++ class hierarchy the target actually has — single inheritance
// orders the bands, member types fix the alignment, and member widths forbid overlap. All three are
// derived from structure, none of them from a table of correct answers.
//
// What this proves and what it does not is stated in the detail line, every run, pass or fail.
// ---------------------------------------------------------------------------

/** Which C++ class each derived offset is a member of. Godot: Object <- Node <- CanvasItem <- Control <- Label. */
const OFFSET_OWNER = {
  'node.scriptInstance': 'Object',
  'node.parent': 'Node',
  'node.childListHead': 'Node',
  'node.name': 'Node',
  'canvasItem.visible': 'CanvasItem',
  'control.globalPosition': 'Control',
  'control.offset': 'Control',
  'control.anchor': 'Control',
  'control.scale': 'Control',
  'control.position': 'Control',
  'control.size': 'Control',
  'label.text': 'Label',
  'richTextLabel.text': 'RichTextLabel',
};

// Label and RichTextLabel are SIBLINGS. Nothing about single inheritance orders one against the
// other, and no object holds both, so they are never compared with each other — only each against
// the chain above it. Encoding them as one ladder would be asserting a relation the hierarchy does
// not imply, which is the mistake §13.10's neighbour clauses made.
const INHERITANCE_CHAINS = [
  ['Object', 'Node', 'CanvasItem', 'Control', 'Label'],
  ['Object', 'Node', 'CanvasItem', 'Control', 'RichTextLabel'],
];

/** Member widths in bytes. `real_t` is the precision axis, which is exactly why the grid has one. */
function memberShape(precision) {
  const t = precision === 'double' ? 8 : 4;
  return {
    'node.scriptInstance': { width: 8, align: 8, what: 'ScriptInstance*' },
    'node.parent': { width: 8, align: 8, what: 'Node*' },
    'node.childListHead': { width: 8, align: 8, what: 'List<Node*>::Element*' },
    'node.name': { width: 8, align: 8, what: 'StringName (_Data*)' },
    'canvasItem.visible': { width: 1, align: 1, what: 'bool' },
    'control.globalPosition': { width: 2 * t, align: t, what: `Point2 (2 x real_t${t})` },
    'control.offset': { width: 4 * t, align: t, what: `real_t${t}[4]` },
    'control.anchor': { width: 4 * t, align: t, what: `real_t${t}[4]` },
    'control.scale': { width: 2 * t, align: t, what: `Vector2 (2 x real_t${t})` },
    'control.position': { width: 2 * t, align: t, what: `Point2 (2 x real_t${t})` },
    'control.size': { width: 2 * t, align: t, what: `Size2 (2 x real_t${t})` },
    'label.text': { width: 8, align: 8, what: 'String (CowData<char32_t>, one pointer)' },
    'richTextLabel.text': { width: 8, align: 8, what: 'String (CowData<char32_t>, one pointer)' },
  };
}

// A member offset inside one engine object. Generous on purpose: this rejects nonsense, not layouts.
const OFFSET_CEILING = 0x4000;

function checkOffsetConsistency(cell, result) {
  const id = 'offsets.internal_consistency';
  const title = '(f) derived offsets are mutually consistent with the class hierarchy';

  const derived = { ...result.structural.offsets, ...result.semantic.offsets, ...result.strings.offsets };
  const shape = memberShape(cell.precision);
  const known = Object.entries(derived)
    .filter(([key, off]) => OFFSET_OWNER[key] && off !== null && off !== undefined)
    .map(([key, off]) => ({ key, off, owner: OFFSET_OWNER[key], ...shape[key] }));

  if (known.length < 2) {
    return fail(id, title,
      `only ${known.length} recognised offset(s) were derived, so there is nothing to be consistent WITH. `
      + 'Mutual consistency is the only offset assertion available on a cell no shipped profile covers, '
      + 'and it needs at least two numbers to make one.');
  }

  const problems = [];

  // 1. Plausibility. A member offset is a small positive number, and 0 is the vtable pointer.
  for (const m of known) {
    if (!Number.isInteger(m.off) || m.off <= 0) {
      problems.push(`${m.key} = ${hex(m.off)} is not a positive member offset (0 is the vtable slot)`);
    } else if (m.off >= OFFSET_CEILING) {
      problems.push(`${m.key} = ${hex(m.off)} is beyond any plausible member offset (ceiling ${hex(OFFSET_CEILING)})`);
    }
  }

  // 2. Alignment, from the member's TYPE. An 8-byte pointer cannot sit on an odd address, whatever
  //    a scan reports — this alone rejects the "every offset is 0x1" mutation.
  for (const m of known) {
    if (m.off % m.align !== 0) {
      problems.push(`${m.key} = ${hex(m.off)} is not ${m.align}-aligned, and ${m.what} must be`);
    }
  }

  // 3. Band ordering. A derived class's members come after every base class's, on both chains.
  for (const chain of INHERITANCE_CHAINS) {
    const present = chain
      .map((cls) => ({ cls, members: known.filter((m) => m.owner === cls) }))
      .filter((band) => band.members.length);
    for (let i = 1; i < present.length; i++) {
      const below = present[i - 1];
      const above = present[i];
      const lowest = above.members.reduce((a, b) => (b.off < a.off ? b : a));
      const highest = below.members.reduce((a, b) => (b.off > a.off ? b : a));
      if (lowest.off <= highest.off) {
        problems.push(
          `${lowest.key} = ${hex(lowest.off)} (${above.cls}) is not above ${highest.key} = ${hex(highest.off)} `
          + `(${below.cls}); ${above.cls} derives from ${below.cls}, so the base subobject comes first`);
      }
    }
  }

  // 4. Overlap. Every member on one chain lives in ONE object, so two of them cannot occupy the same
  //    byte. Widths come from the member types, and from the precision axis for real_t.
  for (const chain of INHERITANCE_CHAINS) {
    const inChain = known.filter((m) => chain.includes(m.owner)).sort((a, b) => a.off - b.off);
    for (let i = 1; i < inChain.length; i++) {
      const prev = inChain[i - 1];
      const cur = inChain[i];
      if (prev.off + prev.width > cur.off) {
        problems.push(
          `${prev.key} (${hex(prev.off)}, ${prev.width} bytes as ${prev.what}) overlaps `
          + `${cur.key} (${hex(cur.off)}) — one ${chain[chain.length - 1]} object cannot hold both there`);
      }
    }
  }

  // 5. The walk offsets, which are members of other objects entirely and are only checked for the
  //    properties their own types imply.
  const walkShape = {
    'childList.next': { align: 8, min: 0, what: 'List Element* link' },
    'childList.node': { align: 8, min: 0, what: 'Node* payload' },
    'scriptInstance.ownerBackref': { align: 8, min: 8, what: 'Object* owner (after the vtable slot)' },
    'scriptInstance.gcHandle': { align: 8, min: 8, what: 'GCHandle slot (after the vtable slot)' },
  };
  const walkKnown = Object.entries(result.walk)
    .filter(([key, off]) => walkShape[key] && off !== null && off !== undefined);
  for (const [key, off] of walkKnown) {
    const s = walkShape[key];
    if (!Number.isInteger(off) || off < s.min || off >= OFFSET_CEILING) {
      problems.push(`${key} = ${hex(off)} is out of range for a ${s.what}`);
    } else if (off % s.align !== 0) {
      problems.push(`${key} = ${hex(off)} is not ${s.align}-aligned, and a ${s.what} must be`);
    }
  }
  // `Number.isInteger`, not `!== null`: a key the driver never derived is `undefined`, and
  // `undefined === undefined` made every pair of ABSENT walk offsets read as "the same slot". A
  // check that fires on two things that are not there is the mirror image of the family this round
  // is fixing, and it showed up the first time this check was pointed at real gdscript results.
  const distinctPairs = [
    ['childList.next', 'childList.node', 'the link and the payload are different members'],
    ['scriptInstance.ownerBackref', 'scriptInstance.gcHandle', 'the owner back-reference and the GCHandle are different members'],
  ];
  for (const [a, b, why] of distinctPairs) {
    if (Number.isInteger(result.walk[a]) && Number.isInteger(result.walk[b]) && result.walk[a] === result.walk[b]) {
      problems.push(`${a} and ${b} are the same slot; ${why}`);
    }
  }

  // Said on EVERY run, pass or fail. A consistency proof is not a correctness proof, and this check
  // is the one most likely to be mistaken for one.
  const disclaimer =
    '\n    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band'
    + '\n    ordering, type alignment and member widths — all read off the structure of the classes, not'
    + '\n    off any table of correct values.'
    + '\n    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent'
    + '\n    wrong layout satisfies every rule here. Corroboration by an independent source (the shipped'
    + '\n    profile via profile.agreement, or the §13.2 getter decoder) is a separate question, and where'
    + '\n    neither covers a cell its offsets remain uncorroborated however green this check is.';

  const bandRank = (cls) => Math.max(...INHERITANCE_CHAINS.map((c) => c.indexOf(cls)));
  const bands = [...new Set(known.map((m) => m.owner))].sort((a, b) => bandRank(a) - bandRank(b));
  return problems.length
    ? fail(id, title,
      `${problems.length} inconsistency/ies across ${known.length} derived offset(s):\n    - ${problems.slice(0, 8).join('\n    - ')}${disclaimer}`,
      { derived: known.length, problems: problems.length })
    : pass(id, title,
      `${known.length} offset(s) across ${bands.length} class band(s) (${bands.join(' < ')}) + ${walkKnown.length} walk `
      + `offset(s): ordering, ${cell.precision}-precision alignment and non-overlap all hold.${disclaimer}`,
      { derived: known.length });
}

function semanticPreamble(id, title, result, offsetKey) {
  const methodFail = requireDerivationMethod(id, title, result.semantic.method, SEMANTIC_METHOD,
    'A hardcoded or single-sample offset is not the §12.5 technique.');
  if (methodFail) return { stop: methodFail };

  // §12.5's precondition, and it used to be disarmed by the SHAPE of `samples` rather than by its
  // value. `samples: {}`, `samples` omitted, or a per-key map that simply lacks this key all produced
  // `sampleCount = null`, and `null < 2` is false, so the guard was skipped for all five semantic
  // checks without a word in the detail line. Live results carry the per-key map and 14 of them omit
  // `canvasItem.visible` from it, so this was one absent key away from firing in production.
  //
  // An unreadable sample count is now a failure, not a bypass: the check is being asked to certify
  // that intersection happened, and it cannot certify that from a number it does not have.
  const sampleCount = resolveSampleCount(result.semantic.samples, offsetKey);
  if (sampleCount.error) {
    return {
      stop: fail(id, title,
        `${sampleCount.error} §12.5: one control gave four candidate offsets, so "how many samples "`
        + 'were intersected" is the precondition for reading anything into this offset at all. A '
        + 'count the harness cannot read is not a count.'),
    };
  }
  if (sampleCount.count < 2) {
    return {
      stop: fail(id, title,
        `derived from ${sampleCount.count} sample(s). §12.5: one control gave four candidate offsets; `
        + 'intersection across at least two is what makes the answer unique.'),
    };
  }
  const off = result.semantic.offsets[offsetKey];
  if (off === null || off === undefined) {
    return { stop: fail(id, title, `driver did not report derivation.semantic.offsets["${offsetKey}"]`) };
  }
  return { off, sampleCount: sampleCount.count };
}

/** `samples` may be a scalar, an array of the samples themselves, or a per-key map of either. */
function resolveSampleCount(samples, offsetKey) {
  if (samples === null || samples === undefined) {
    return { error: 'the driver reported no derivation.semantic.samples.' };
  }
  let entry = samples;
  if (!Array.isArray(samples) && typeof samples === 'object') {
    if (!(offsetKey in samples)) {
      return {
        error: `the driver reported per-key sample counts but none for "${offsetKey}" `
          + `(it has: ${Object.keys(samples).join(', ') || 'nothing at all'}).`,
      };
    }
    entry = samples[offsetKey];
  }
  if (Array.isArray(entry)) return { count: entry.length };
  if (Number.isFinite(entry)) return { count: Number(entry) };
  return { error: `derivation.semantic.samples for "${offsetKey}" is ${JSON.stringify(entry)}, which is not a count.` };
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

  // And where a driver DOES publish anchors, they must be right. Until now `anchors` was parsed on
  // the way in and then compared against nothing anywhere in the harness — the trap above reads
  // exp.anchors, the expected value, never the driver's — so anchors could hold anything at all.
  // Publishing nothing stays acceptable; publishing a wrong quad while offsets read correctly is the
  // near miss this whole check exists to catch.
  // The anchor denominator is the DRIVER's to pick, and that has to be visible rather than silently
  // absorbed. Publishing no anchors at all stays legitimate — the calibrator is not asked to derive
  // control.anchor — but a check that quietly compares zero of 23 quads and prints "23/23 nodes
  // exact" reads as anchor coverage it does not have. So: count what was published, say so in the
  // detail line either way, and refuse a PARTIAL publication, which is the cherry-picking shape.
  let anchorsPublished = 0;
  for (const exp of controls) {
    const got = byPath.get(exp.path);
    if (!got) continue;
    if (got.malformed.includes('anchors')) {
      trap.push(`${exp.path}: reported anchors ${rawFieldText(got, 'anchors')}, which is not a 4-component vector`);
      continue;
    }
    if (got.anchors === null || got.anchors === undefined) continue;
    anchorsPublished++;
    if (!vectorsEqual(got.anchors, exp.anchors, EPS)) {
      trap.push(`${exp.path}: reported anchors ${fmtVec(got.anchors)} != ${fmtVec(exp.anchors)}`);
    }
  }
  if (anchorsPublished > 0 && anchorsPublished < controls.length) {
    trap.push(
      `anchors were published for ${anchorsPublished} of ${controls.length} Controls. A driver may `
      + 'decline to read Data.anchor[4] — it may not read it on the nodes where it agrees and omit it '
      + 'on the rest, because that is a denominator chosen after the answer was known.');
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
    + `including ${anchored.length} node(s) with non-zero anchors that separate Data.offset from Data.anchor; `
    + (anchorsPublished
      ? `${anchorsPublished}/${controls.length} anchor quads published and exact`
      : `NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here`),
    { offset: pre.off, anchorsPublished });
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

/**
 * Published claim (c) is "every node name decoded exactly, non-ASCII included". For names it was not
 * measured at all.
 *
 * The comparison was `byPath.get(exp.path).name === exp.name`, and `byPath` is keyed on a path built
 * FROM `node.name` (reconstructPaths, line ~99). Both sides of that `===` therefore came from the
 * same string: it could not come out false. A mangled name simply moved the node to a path nobody
 * looked up, so every failure this check ever produced said `not reached` — and neutering the entire
 * function left the selftest at 24/24, because no scenario exercised it either.
 *
 * The name is now compared against the ORDINAL position of the node in the child lists, cross-
 * referenced with the tree the target itself reported (lib/rawtree.mjs). That source never passes
 * through `node.name`, so the comparison can disagree. It also covers the engine-internal children,
 * whose names (`@VScrollBar@2`) are not in expected.json and were never checked by anything.
 */
function checkNames(result, walk, positions) {
  const id = 'strings.names';
  const title = '(c) every node name decoded exactly';
  const nameOff = result.strings.offsets['node.name'];

  if (!positions.available) {
    return fail(id, title,
      `names cannot be checked independently: ${positions.reason}. Comparing a name against a lookup `
      + 'keyed on that same name is a tautology, which is what this check used to do.');
  }

  const problems = [];
  let compared = 0;
  for (const [ptr, path] of positions.pathByPtr) {
    const got = positions.byPtr.get(ptr);
    if (!got) { problems.push(`${path}: the child lists reach ${ptr}, for which the driver reported no node`); continue; }
    const wantName = path.split('/').pop();
    if (got.malformed.includes('name')) {
      problems.push(`${path}: reported name ${rawFieldText(got, 'name')}, which is not a string`);
      continue;
    }
    compared++;
    if (got.name !== wantName) {
      problems.push(`${path}: the node at this position in the child lists is named "${escapeText(got.name)}", not "${wantName}"`);
    }
  }

  const unpositioned = result.nodes.filter((n) => n.nativePtr && !positions.pathByPtr.has(n.nativePtr));
  for (const n of unpositioned) {
    problems.push(`${n.nativePtr} ("${escapeText(n.name)}") was reported but sits nowhere in the child lists, so its name has nothing to be checked against`);
  }

  const internal = walk.internalNames.length;
  return problems.length
    ? fail(id, title, `node.name ${hex(nameOff)}:\n    - ${problems.slice(0, 8).join('\n    - ')}`, { offset: nameOff, compared })
    : pass(id, title,
      `node.name ${hex(nameOff)}, ${compared}/${compared} StringNames exact against their position in the `
      + `child lists${internal ? ` (including ${internal} engine-internal child name(s) the authored scene never mentions)` : ''}`,
      { offset: nameOff, compared });
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
    const what = exp ? exp.class : 'engine-internal, not in expected.json';
    // A non-string invented value was invisible here: `text: 12345` or `[72,105]` normalised to null
    // and read as "reported nothing". The check exists because a driver published "res://Probe.gd" on
    // plain Controls; a driver publishing the raw bytes of the same read is the same defect one
    // conversion earlier, and it must not be the one that passes.
    if (got.malformed.includes('text')) {
      problems.push(`${got.path} (${what}) has no text member but the driver reported ${rawFieldText(got, 'text')}, which is not even a string`);
      continue;
    }
    if (got.text !== null) {
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

  // `anchors` belongs here as much as the rest. It is the one geometry field the harness asserted
  // NOWHERE — checkOffsetAccessor reads exp.anchors, the expected value, never the driver's — so a
  // driver publishing plausible anchors on a bare Node scored clean. The anchor/offset confusion is
  // the trap the README singles out, which makes it the last field that should go unchecked.
  const fields = ['visible', 'size', 'position', 'scale', 'offset', 'anchors'];
  const problems = [];

  for (const exp of bare) {
    const got = byPath.get(exp.path);

    // A node that should have been inspected and was not is a hole in this check's coverage, not a
    // node to quietly drop. Skipping it while counting only what remained is the same defect the
    // sibling check's own comment says it fixed: a denominator that shrinks to fit the answer, so
    // half the population goes missing and the report still reads "full coverage".
    if (!got) {
      problems.push(`${exp.path} (${exp.class}) was not reached by the walk, so it could not be inspected`);
      continue;
    }

    for (const field of fields) {
      // "Absent" and "present but unusable" are different answers, and collapsing them made this
      // check satisfiable by MALFORMED geometry rather than only by absent geometry. `visible: 1` is
      // the realistic one: canvasItem.visible is a byte in memory, so a driver that publishes the raw
      // byte instead of a bool defeats the very check written to catch a fabricated `visible=true`.
      // `size: [7]`, `anchors: [1,2,3]`, `size: "0,0"` and `{x:'a',y:'b'}` all had the same effect.
      if (got.malformed.includes(field)) {
        problems.push(
          `${exp.path} (${exp.class}) has no CanvasItem but reported ${field}=${rawFieldText(got, field)}, `
          + 'which the harness could not parse — a value it cannot read is still a value the driver published');
        continue;
      }
      if (got[field] !== null && got[field] !== undefined) {
        const shown = Array.isArray(got[field]) ? fmtVec(got[field]) : String(got[field]);
        problems.push(`${exp.path} (${exp.class}) has no CanvasItem but reported ${field}=${shown}`);
      }
    }
  }

  return problems.length
    ? fail(id, title,
      `${problems.length} problem(s) across the ${bare.length} non-Control node(s) the scene authors:\n    - ${problems.slice(0, 8).join('\n    - ')}\n`
      + '    Reading a Control field off a non-Control succeeds and returns garbage (§12.4c), and the\n'
      + '    garbage is often all zeros — which no plausibility test can reject.', { authored: bare.length })
    : pass(id, title, `${bare.length}/${bare.length} authored non-Control node(s) reported no geometry`, { authored: bare.length });
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
  // The two counts below are a CROSS-CHECK: what the driver says it walked, against how many records
  // it actually produced. normaliseResult used to substitute nodes.length for an unusable walkCount,
  // which made them identical by construction — `walkCount: "25"` erased the comparison rather than
  // failing it, and a driver whose walk and whose reporting disagreed could not be detected.
  if (result.walkCount === null) {
    problems.push(result.walkCountRaw === undefined
      ? 'the driver reported no walkCount at all, so its own count cannot be cross-checked against the node records it produced'
      : `the driver reported walkCount=${JSON.stringify(result.walkCountRaw)}, which is not an integer node count`);
  } else if (result.walkCount !== want) {
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
  const notDerived = [];
  const unresolvable = [];

  // ONE loop over every comparable key the profile holds, offsets and walk alike.
  //
  // There were two, and the second silently dropped any key the driver had not derived — so a .NET
  // driver that never derived scriptInstance.gcHandle, the offset the entire managed bridge hangs
  // off, was indistinguishable from a correct one and the check printed "14/14 offsets match"
  // against sixteen keys. That was the same defect already fixed for control.globalPosition one loop
  // above, which is the point: it had been fixed for a KEY, not for the mechanism.
  //
  // The harness also no longer knows any key by name. Whether a field is deliberately not derived is
  // the DRIVER's claim to make and to justify — it is the only party that knows why — so it says so
  // in `derivation.notDerived` and the reason is printed. Anything else the driver failed to derive
  // is a silent gap and fails.
  const entries = [
    ...Object.entries(profile.offsets).map(([key, want]) => [key, want, derived[key]]),
    ...Object.entries(profile.walk ?? {}).map(([key, want]) => [key, want, result.walk[key]]),
  ];

  // How much a driver may excuse before this check stops being a measurement. `notDerived` is the
  // driver's own denominator: declaring the nine keys it does not use produced a green
  // profile.agreement reading "8/8 offsets match", and the reason string was never validated at all
  // — `''` was accepted. The branch has never run outside the mock in 224 live results, so nothing
  // would have noticed. A refusal has to cost something or it is free.
  const excusable = Math.max(1, Math.floor(entries.length / 3));
  const badReasons = [];

  for (const [key, wantRaw, got] of entries) {
    const declined = result.notDerived[key];
    if (declined !== undefined && (got === null || got === undefined)) {
      if (typeof declined !== 'string' || declined.trim().length < 8) {
        badReasons.push(`${key}: reason ${JSON.stringify(declined)} explains nothing`);
      }
      notDerived.push(`${key} (${declined})`);
      continue;
    }

    if (got === null || got === undefined) { missing.push(key); continue; }

    // A walk entry may be a scalar, or a map from the C++ class that OWNS the field to its offset.
    // scriptInstance.ownerBackref is the only one that needs the second shape: it is a member of
    // CSharpInstance or GDScriptInstance, which are unrelated implementations of one interface, and
    // NOT of the engine object. So no single number can be right for both, and the axis is not the
    // cell's binding either — one mono build hosts .gd and .cs scripted nodes side by side, so the
    // question "which implementation is this node's?" is per-object and can only be answered by the
    // target. The driver reports the class it read off the ScriptInstance's own vtable.
    let want = wantRaw;
    if (wantRaw !== null && typeof wantRaw === 'object') {
      // Case-insensitive on purpose. The class name is read off a vtable by RTTI and a driver
      // reporting `csharpinstance` was silently EXCLUDED from the comparison rather than compared —
      // the same defect as the flattened map one layer up, moved to the lookup.
      const owner = result.scriptInstanceClass;
      const match = typeof owner === 'string'
        ? Object.keys(wantRaw).find((k) => k.toLowerCase() === owner.trim().toLowerCase())
        : undefined;
      if (!owner) {
        unresolvable.push(`${key}: profile scopes it by implementing class, and the driver did not report one`);
        continue;
      }
      if (match === undefined) {
        unresolvable.push(`${key}: the driver read implementing class "${owner}", which this profile does not cover (it has: ${Object.keys(wantRaw).join(', ')})`);
        continue;
      }
      want = wantRaw[match];
    }

    compared.push(key);
    if (got !== want) mismatched.push({ key, got, want });
  }

  if (!compared.length) {
    return fail(id, title, `profile ${profile.id} exists but the driver reported no comparable offsets (missing: ${missing.join(', ')})`);
  }

  // A key the driver has explained it does not derive is not a disagreement — but it must not vanish
  // either, or the table looks more complete than it is.
  const caveat = notDerived.length ? `\n    not compared: ${notDerived.join('; ')}` : '';

  if (badReasons.length) {
    return fail(id, title,
      `${badReasons.length} key(s) were declared derivation.notDerived without a usable reason:\n    - ${badReasons.join('\n    - ')}\n`
      + '    notDerived is the driver excusing itself from a comparison. The reason is the entire cost\n'
      + '    of doing that, and an empty or one-word string is not a reason — it is the same silence\n'
      + '    the field was introduced to replace.', { badReasons });
  }

  if (notDerived.length > excusable) {
    return fail(id, title,
      `the driver declared ${notDerived.length} of ${entries.length} comparable key(s) as notDerived, above the `
      + `ceiling of ${excusable}:\n    - ${notDerived.join('\n    - ')}\n`
      + '    Past this point the check is measuring a denominator the driver chose. Declaring the keys\n'
      + '    it does not use turns "8 of 17 offsets were checked" into a green "8/8 offsets match",\n'
      + '    which is a cell that reads as evidence and is not one.', { notDerived: notDerived.length, excusable });
  }

  // An unresolvable key is NOT benign, and treating it as such is §13.11 #5 one layer above its own
  // fix. That entry was `loadProfiles` flattening the per-class map to null, making the comparison
  // unreachable; the fix moved the requirement to the driver, which must now supply
  // `derivation.walk.scriptInstanceClass` — and the live calibrator emitted nothing there on 155 of
  // 224 results, so the comparison went right back to being unreachable and passing. The profile
  // holds a value for this key on this cell; either it gets compared or the cell is not clean.
  if (unresolvable.length) {
    return fail(id, title,
      `${unresolvable.length} key(s) in ${profile.id} could not be resolved for this target, so they were not `
      + `checked:\n    - ${unresolvable.join('\n    - ')}\n`
      + '    The profile HAS a value here. A key that goes uncompared because an input the driver was\n'
      + '    supposed to report is missing is not a caveat on a passing cell — it is the comparison\n'
      + '    failing to happen, which is precisely how the previous instance of this bug survived.'
      + caveat,
      { unresolvable });
  }

  // A key the driver simply did not derive, and did not explain, IS scored. Silence is the one thing
  // this check cannot interpret: it looks identical whether the offset was withheld for good reason
  // or never attempted, and treating it as benign is what let a missing GCHandle pass for correct.
  if (missing.length) {
    return fail(id, title,
      `${missing.length} of ${missing.length + compared.length} comparable key(s) were neither derived nor `
      + `explained, so ${profile.id} could not be checked against them:\n    - ${missing.join('\n    - ')}\n`
      + '    A driver that cannot derive one of these is entitled to say so in derivation.notDerived,\n'
      + '    with a reason. What it may not do is leave it absent and unremarked, because that is\n'
      + '    indistinguishable from having derived it correctly.' + caveat,
      { missing, compared: compared.length });
  }

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
  return pass(id, title,
    `${compared.length} of ${entries.length} key(s) in ${profile.id} compared and matching (trust=${profile.trust})${caveat}`,
    { profileId: profile.id, trust: profile.trust, compared: compared.length, comparable: entries.length });
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

  // §4.6's native -> managed direction: Node +0x68 -> ScriptInstance, +0x08 owner backref, +0x20
  // GCHandle -> managed object.
  //
  // This was "optional, but scored when reported", which in practice meant: absent -> green, and
  // `reverse.ownerBackref: null` -> green WHILE PRINTING "reverse ScriptInstance chain verified".
  // Nothing was verified; the pass line simply said so. The forward direction alone does not
  // establish the chain — that a managed object's NativePtr equals the root says nothing about
  // whether the ScriptInstance hanging off the root points back at it.
  const back = bridge.reverse ?? null;
  let reverseVerified = false;
  if (!back) {
    problems.push(
      'no reverse ScriptInstance chain was reported. §4.6 asks for both directions: the managed '
      + 'static resolving forward to the root, and the root\'s own ScriptInstance resolving back. Only '
      + 'the second one crosses the offsets this cell derived.');
  } else {
    const backPtr = normPtr(back.ownerBackref);
    const handlePtr = normPtr(back.gcHandle);
    if (!backPtr) {
      problems.push(`reverse.ownerBackref is ${JSON.stringify(back.ownerBackref ?? null)} — the backref was not followed to anything`);
    } else if (rootPtr && backPtr !== rootPtr) {
      problems.push(`ScriptInstance owner backref ${backPtr} != ${rootPtr} — wrong pointer followed`);
    }
    if (!handlePtr) {
      problems.push(`reverse.gcHandle is ${JSON.stringify(back.gcHandle ?? null)} — the GCHandle slot the whole managed bridge hangs off resolved to nothing`);
    }
    reverseVerified = Boolean(backPtr && handlePtr && (!rootPtr || backPtr === rootPtr));
  }

  // The six expected values — two non-ASCII strings, an int32, an int64, a float and a bool — had
  // NEVER been compared, on any cell, in 224 live runs. `if (!(key in fields)) continue;` let the
  // driver pick which of them counted, so `fields: {}` and `fields: {Nope: 1}` both scored green
  // with "0 managed field(s) read" in the detail line, and deleting the comparison outright left the
  // selftest at 24/24. Reading a value out of the managed object is what the managed bridge is FOR;
  // the denominator is expected.json's, not the driver's.
  const wantFields = Object.entries(expected.managedBridge.fields);
  const fields = bridge.fields;
  let comparedFields = 0;
  if (fields === null || fields === undefined || typeof fields !== 'object' || Array.isArray(fields)) {
    problems.push(
      `no managed field values were reported (fields=${JSON.stringify(fields ?? null)}). The bridge resolved a `
      + `managed object; reading ${wantFields.length} known field value(s) off it is the only thing that `
      + 'demonstrates the object is the right one rather than merely a pointer that compares equal.');
  } else {
    const absent = [];
    for (const [key, want] of wantFields) {
      if (!(key in fields)) { absent.push(key); continue; }
      comparedFields++;
      const got = fields[key];
      const same = typeof want === 'number' ? Math.abs(Number(got) - want) <= 1e-3 : got === want;
      if (!same) problems.push(`field ${key}: ${JSON.stringify(got)} != ${JSON.stringify(want)}`);
    }
    if (absent.length) {
      problems.push(
        `${absent.length} of ${wantFields.length} expected managed field(s) were not read: ${absent.join(', ')}. `
        + 'expected.json names them precisely so the driver cannot choose which ones count.');
    }
  }

  return problems.length
    ? fail(id, title, problems.join('\n    - '))
    : pass(id, title, `${bridge.staticRootType}.${bridge.staticRootField} -> NativePtr ${bridgePtr} == walk root`
      + `${reverseVerified ? ', reverse ScriptInstance chain verified (owner backref + GCHandle)' : ''}`
      + `, ${comparedFields}/${wantFields.length} managed field value(s) exact`);
}
