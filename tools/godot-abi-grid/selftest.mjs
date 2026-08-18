#!/usr/bin/env node
// selftest.mjs — does the judge actually judge?
//
// §8.9 says to make the harness work against 4.5.1 release FIRST, because that
// cell is already known good and therefore validates the harness before the
// harness is trusted to judge anything else. On a machine with no Godot export
// templates that cell cannot be run — so this is the next best thing that is
// still honest: drive checks.mjs with the 4.5.1-release-single-dotnet §4.6
// offsets and the authored scene, then deliberately break one thing at a time
// and assert that the corresponding check FAILS.
//
// A green selftest says "the harness detects these failure modes". It says
// nothing whatsoever about any calibrator, and it produces no coverage numbers.
//
//   node selftest.mjs

import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { mkdirSync, writeFileSync } from 'node:fs';
import { loadExpected } from './lib/expected.mjs';
import { loadProfiles, selectProfile } from './lib/profiles.mjs';
import { runChecks } from './lib/checks.mjs';
import { buildRequest } from './lib/driver.mjs';
import { parseCellName } from './lib/grid.mjs';
import { run as mockRun, FAULTS } from './drivers/mock.mjs';
import { crossCellChecks } from './lib/crosscell.mjs';

const HARNESS_ROOT = dirname(fileURLToPath(import.meta.url));
const CELL = parseCellName('4.5.1-release-single-dotnet');

const expected = loadExpected(HARNESS_ROOT);
const profiles = loadProfiles(HARNESS_ROOT);
const profile = selectProfile(profiles, CELL);

const CELL_DEFAULTS = {
  '4.5.1-release-single-dotnet': { engineVersion: '4.5.1.stable', binding: 'dotnet', templateVariant: 'release', precision: 'single' },
  '4.3.0-release-single-gdscript': { engineVersion: '4.3.0.stable', binding: 'gdscript', templateVariant: 'release', precision: 'single' },
};

/**
 * The engine's own child lists, internal children included — a ready.v2 target reports these and
 * lib/rawtree.mjs reconciles them against the authored scene.
 *
 * Every ready file this selftest built until now omitted `rawTree`, so `resolveWalkModel` returned
 * `authoredOnly` in all 24 scenarios and lib/rawtree.mjs had ZERO selftest coverage — a module whose
 * whole job is to stop the harness stating a node count the in-memory walk cannot reproduce, with
 * nothing exercising a single line of it. `@VScrollBar@2` is the real shape: RichTextLabel adds a
 * scrollbar with INTERNAL_MODE_* that `get_children()` and a .tscn parse both hide, and whose name
 * carries a global instantiation counter no static file can predict.
 */
function rawTreeFor({ extraRows = [] } = {}) {
  const internalParent = expected.nodes.find((n) => n.class === 'RichTextLabel');
  const rows = expected.nodes.map((n) => ({
    path: n.path,
    name: n.name,
    class: n.class,
    internal: false,
    children: n.childPaths.map((p) => p.split('/').pop()),
  }));
  const host = rows.find((r) => r.path === internalParent.path);
  host.children = [...host.children, '@VScrollBar@2'];
  rows.push({
    path: `${internalParent.path}/@VScrollBar@2`,
    name: '@VScrollBar@2',
    class: 'VScrollBar',
    internal: true,
    children: [],
  });
  return [...rows, ...extraRows];
}

function readyFor(cell, overrides = {}, { rawTree = null } = {}) {
  return {
    contract: expected.readyContract,
    pid: 4242,
    isTemplate: true,
    isEditor: false,
    walkRootPath: expected.walkRootRuntimePath,
    walkCount: expected.nodeCount,
    ...CELL_DEFAULTS[cell.name],
    ...(rawTree ? { rawTree, rawWalkCount: rawTree.length } : {}),
    ...overrides,
  };
}

async function evaluate(scenario) {
  const cell = scenario.cell ? parseCellName(scenario.cell) : CELL;
  const cellProfile = selectProfile(profiles, cell);
  process.env.GRID_MOCK_FAULTS = (scenario.faults ?? []).join(',');
  const rawTree = scenario.rawTree ? rawTreeFor(scenario.rawTreeOptions) : null;
  const ready = readyFor(cell, scenario.readyOverrides, { rawTree });
  const request = buildRequest({ cell, ready, expected });
  const result = await mockRun(request);
  return { ...runChecks({ cell, expected, profile: cellProfile, ready, result }), request };
}

const SCENARIOS = [
  {
    name: 'baseline — §4.6 release offsets, authored scene, nothing broken',
    faults: [],
    mustPass: 'all',
  },
  { name: 'lossy UTF-32 decode (the §4.6 scry char32_t -> byte bug)', faults: ['lossy-text'], mustFail: ['strings.text.unicode', 'strings.text.rich'] },
  { name: 'text truncated at the wrong CowData length', faults: ['truncate-text'], mustFail: ['strings.text.rich'] },
  { name: 'two distinct 409x151 nodes collapsed into one', faults: ['collapse-dup'], mustFail: ['structure.no_collapse'] },
  { name: 'a node missing from the walk', faults: ['drop-node'], mustFail: ['structure.walk_count', 'structural.child_head'] },
  { name: 'wrong parent pointer', faults: ['bad-parent'], mustFail: ['structural.parent'] },
  { name: 'derived size offset disagrees with the shipped profile', faults: ['profile-mismatch'], mustFail: ['profile.agreement'], mustPass: ['semantic.size'] },
  { name: 'driver admits to using a shipped profile', faults: ['used-profile'], mustFail: ['calibration.unaided'] },
  { name: 'structural offsets found by value scanning, not pointer identity', faults: ['wrong-structural-method'], mustFail: ['structural.child_head', 'structural.parent'] },
  {
    name: 'semantic offsets derived from a single control (§12.5: four candidates)',
    faults: ['single-sample'],
    mustFail: ['semantic.size', 'semantic.position', 'semantic.scale', 'semantic.offset', 'semantic.visible'],
  },
  { name: 'Data.anchor[4] read where Data.offset[4] was wanted', faults: ['anchor-confusion'], mustFail: ['semantic.offset'] },
  { name: 'visible flag never actually located', faults: ['visible-blind'], mustFail: ['semantic.visible'] },
  {
    name: 'text invented for an authored node that has none',
    faults: ['phantom-text'],
    mustFail: ['strings.text.absent'],
    mustPass: ['strings.text.ascii', 'strings.text.unicode', 'strings.text.rich'],
  },
  {
    name: 'geometry invented for a node with no CanvasItem base',
    faults: ['phantom-geometry'],
    mustFail: ['geometry.absent'],
    mustPass: ['semantic.size', 'semantic.visible'],
  },
  {
    name: 'the raw-BBCode node withheld entirely rather than misread',
    faults: ['drop-bbcode-text'],
    mustFail: ['strings.text.richBbcode'],
  },
  {
    name: 'the GCHandle offset never derived, and never mentioned',
    faults: ['drop-gchandle'],
    mustFail: ['profile.agreement'],
  },
  {
    name: 'an anchor quad invented for a node with no CanvasItem base',
    faults: ['phantom-anchors'],
    mustFail: ['geometry.absent'],
  },
  {
    name: 'a Control given a plausible but wrong anchor quad, offsets still correct',
    faults: ['wrong-anchors'],
    mustFail: ['semantic.offset'],
  },
  {
    name: 'half the non-Control nodes dropped from the walk',
    faults: ['drop-bare-node'],
    mustFail: ['geometry.absent'],
  },
  {
    name: 'a node with real text is given a plausible but wrong string',
    faults: ['wrong-text'],
    mustFail: ['strings.text.wrong'],
  },
  {
    name: 'text invented for an engine-internal node the scene never authored',
    faults: ['phantom-text-internal'],
    mustFail: ['strings.text.absent'],
  },
  { name: 'managed address handed over instead of NativePtr (§4.6)', faults: ['bridge-managed-addr'], mustFail: ['bridge.managed'] },
  { name: 'cell directory mislabels the template variant', faults: [], readyOverrides: { templateVariant: 'debug' }, mustFail: ['harness.runtime_axes'] },
  { name: 'target counted a different number of nodes than expected.json', faults: [], readyOverrides: { walkCount: 19 }, mustFail: ['structure.walk_count'] },

  // -- §13.11 round 2: checks that could not fail ---------------------------------------------
  // Each of these was green before the corresponding fix, and each goes red only because of it.

  {
    // THE headline. Every offset replaced by 0x1, every reading still correct — the audit ran this
    // by hand and the 4.3 cells scored 19/19 with zero failures, because profile.agreement carried
    // the entire offset-correctness burden and does not run where no profile ships.
    name: 'every derived offset is 0x1 while every reading stays correct',
    faults: ['flat-offsets'],
    mustFail: ['offsets.internal_consistency'],
    detailMatch: { 'offsets.internal_consistency': /not 8-aligned/ },
  },
  {
    name: 'every offset is 0x1 on a cell NO shipped profile covers (the 4.3 case)',
    cell: '4.3.0-release-single-gdscript',
    faults: ['flat-offsets'],
    mustFail: ['offsets.internal_consistency'],
    // The point of the scenario: the check that used to be the only offset assertion is not even
    // running here. If profile.agreement ever stops being a skip on this cell, this scenario is
    // no longer testing what it was written to test.
    mustSkip: ['profile.agreement'],
  },
  {
    name: 'the managed object resolves but none of its field values are read',
    faults: ['bridge-no-fields'],
    mustFail: ['bridge.managed'],
    detailMatch: { 'bridge.managed': /expected managed field\(s\) were not read/ },
  },
  {
    name: 'a reverse ScriptInstance chain of nulls, reported as if followed',
    faults: ['bridge-hollow-reverse'],
    mustFail: ['bridge.managed'],
  },
  {
    name: 'the implementing ScriptInstance class never reported, so ownerBackref goes uncompared',
    faults: ['no-script-instance-class'],
    mustFail: ['profile.agreement'],
    detailMatch: { 'profile.agreement': /could not be resolved/ },
  },
  {
    // The fix's other half: a class name that differs only in case was silently EXCLUDED from the
    // comparison, which now costs a failure — so this must PASS, and reverting the fix reddens it.
    name: 'the implementing class reported as "csharpinstance" is still compared',
    faults: ['lowercase-script-instance-class'],
    mustPass: ['profile.agreement'],
  },
  {
    name: 'canvasItem.visible published as the raw memory byte 1 on a bare Node',
    faults: ['visible-as-byte'],
    mustFail: ['geometry.absent'],
    detailMatch: { 'geometry.absent': /visible=1/ },
  },
  {
    name: 'a non-string value invented for a node with no text member',
    faults: ['text-as-number'],
    mustFail: ['strings.text.absent'],
    detailMatch: { 'strings.text.absent': /not even a string/ },
  },
  { name: 'usedProfile reported as the string "true"', faults: ['used-profile-string'], mustFail: ['calibration.unaided'] },
  { name: 'usedProfile omitted entirely — no claim made either way', faults: ['no-used-profile'], mustFail: ['calibration.unaided'] },
  { name: 'profileConsulted reported as an empty string', faults: ['profile-consulted-empty'], mustFail: ['calibration.unaided'] },
  {
    name: 'the driver excuses nine of seventeen profile keys as notDerived',
    faults: ['declare-everything-notderived'],
    mustFail: ['profile.agreement'],
    detailMatch: { 'profile.agreement': /above the ceiling/ },
  },
  {
    name: 'a key declared notDerived with an empty reason string',
    faults: ['empty-notderived-reason'],
    mustFail: ['profile.agreement'],
    detailMatch: { 'profile.agreement': /without a usable reason/ },
  },
  {
    name: 'per-key sample counts that omit the key being checked',
    faults: ['samples-map-omits-key'],
    mustFail: ['semantic.visible'],
    mustPass: ['semantic.size', 'semantic.offset'],
  },
  {
    name: 'walkCount reported as the string "26", erasing the count cross-check',
    faults: ['walkcount-as-string'],
    mustFail: ['structure.walk_count'],
    detailMatch: { 'structure.walk_count': /not an integer node count/ },
  },
  {
    name: 'a plausible but wrong name for one authored node',
    faults: ['mangle-name'],
    mustFail: ['strings.names'],
    // Not `not reached`. The old comparison was a tautology — both sides came from node.name — so
    // every failure it could produce was a lookup miss, and the name itself was never compared.
    detailMatch: { 'strings.names': /is named "ZetaLabelUnicodeX", not "ZetaLabelUnicode"/ },
  },
  {
    name: 'correct anchor quads published on only half the Controls',
    faults: ['partial-anchors'],
    mustFail: ['semantic.offset'],
    detailMatch: { 'semantic.offset': /denominator chosen after the answer was known/ },
  },
  {
    // The merge hole. `{ ...structural, ...semantic, ...strings }` keeps the LAST group that
    // mentions a key, so a driver publishing two different answers for node.name had one of them
    // discarded silently and every check downstream scored the survivor. An audit hit exactly this
    // and the run stayed green — correctly, because the surviving value happened to be right.
    // Nothing would have said so had it been the other one.
    //
    // Both merge sites are asserted, because reverting either one alone leaves the other catching
    // the fault and proves nothing about the one that was reverted.
    name: 'one offset key reported by two derivation groups with different values',
    faults: ['offset-group-collision'],
    mustFail: ['offsets.internal_consistency', 'profile.agreement'],
    detailMatch: {
      'offsets.internal_consistency': [
        // The conflict itself, named, with BOTH values — not a downstream alignment or band-ordering
        // complaint about whichever number the merge kept, which is a different finding entirely.
        /node\.name is claimed by 2 derivation groups with different values \(derivation\.structural\.offsets=0x1c8, derivation\.strings\.offsets=0x1c0\)/,
        // Same key twice with the SAME value is benign — the merge is unambiguous either way — but
        // it must be disclosed, not absorbed. Without this the "identical values" branch would be a
        // line of code nothing has ever executed.
        /label\.text = 0x7f8 in derivation\.structural\.offsets and derivation\.strings\.offsets/,
      ],
      'profile.agreement': /no single derived number to compare against/,
    },
    // The readings are all still correct, like flat-offsets: this fault is about what the driver
    // says it derived, not about what it read.
    mustPass: ['strings.names', 'strings.text.ascii', 'semantic.size'],
  },
  {
    name: 'a wrong parent pointer is reported as a wrong parent, not as a missing node',
    faults: ['bad-parent'],
    mustFail: ['structural.parent'],
    detailMatch: { 'structural.parent': /the node whose child list holds it/ },
  },

  // -- lib/rawtree.mjs, which had no coverage at all -------------------------------------------
  {
    name: 'ready.v2 raw tree with an engine-internal scrollbar — baseline',
    faults: [],
    rawTree: true,
    mustPass: 'all',
  },
  {
    name: 'an engine-internal child the scene never authored is given the wrong name',
    faults: ['mangle-internal-name'],
    rawTree: true,
    // Nothing that iterates expected.json can see this node at all.
    mustFail: ['strings.names'],
    mustPass: ['structure.walk_count'],
  },
  {
    name: 'the raw tree holds a node that is neither authored nor flagged engine-internal',
    faults: [],
    rawTree: true,
    rawTreeOptions: { extraRows: [{ path: 'RootHarness/@Smuggled@9', name: '@Smuggled@9', class: 'Control', internal: false, children: [] }] },
    mustFail: ['harness.runtime_axes'],
  },
];

/**
 * Not a fault injection — an assertion about the REQUEST.
 *
 * `semantic.visible` asserts 23 of 23 CanvasItem values, and `buildRequest` used to hand the driver
 * `expected.visibility` whole: `visiblePaths` (21) plus `hiddenPaths` (2) is every one of them. A
 * driver that echoed the request back scored full marks, so the check had no discriminating power
 * over any node. §12.5 wants anchors that INTERSECT, not an answer key; `semantic.size` sends 6 of
 * 23 and is the same technique used correctly.
 */
/**
 * lib/crosscell.mjs — the assertions that need more than one cell.
 *
 * Driven directly rather than through the mock, because the fault being injected is a disagreement
 * BETWEEN cells and there is no single driver result that expresses one.
 */
function crossCellScenarios() {
  const rel = (binding, offsets, walk = {}, scriptInstanceClass = null) => ({
    cell: `4.5-release-single-${binding}`,
    status: 'pass',
    axes: { version: '4.5', template: 'release', precision: 'single', binding },
    derived: { offsets, walk, scriptInstanceClass },
  });
  const dbg = (binding, offsets, walk = {}) => ({
    cell: `4.5-debug-single-${binding}`,
    status: 'pass',
    axes: { version: '4.5', template: 'debug', precision: 'single', binding },
    derived: { offsets, walk },
  });
  const RELEASE = { 'node.parent': 0x128, 'node.name': 0x1c0, 'control.size': 0x4c0 };
  const DEBUG = { 'node.parent': 0x130, 'node.name': 0x1c8, 'control.size': 0x4c8 };

  const cases = [
    {
      name: 'cross-cell: two bindings of one build agree on every offset',
      records: [rel('dotnet', RELEASE), rel('gdscript', RELEASE)],
      wantPass: ['grid.binding_invariance'],
    },
    {
      name: 'cross-cell: the .NET and GDScript cells of one build disagree on node.name',
      records: [rel('dotnet', RELEASE), rel('gdscript', { ...RELEASE, 'node.name': 0x1c8 })],
      wantFail: ['grid.binding_invariance'],
    },
    {
      // The premise "a binding cannot change the layout" is true of Node and FALSE of the
      // ScriptInstance hanging off it: 0x8 on CSharpInstance and 0x10 on GDScriptInstance is the
      // measured, correct answer (§13.10). Pointed at the live results, the first version of this
      // check called that a contradiction — a cross-cell check with a wrong premise is just a
      // different way to be confidently wrong.
      name: 'cross-cell: ownerBackref differs between two DIFFERENT implementing classes, legitimately',
      records: [
        rel('dotnet', RELEASE, { 'scriptInstance.ownerBackref': 0x8 }, 'CSharpInstance'),
        rel('gdscript', RELEASE, { 'scriptInstance.ownerBackref': 0x10 }, 'GDScriptInstance'),
      ],
      wantPass: ['grid.binding_invariance'],
      wantDetail: { 'grid.binding_invariance': /implementing classes differ/ },
    },
    {
      name: 'cross-cell: ownerBackref differs between cells reporting the SAME implementing class',
      records: [
        rel('dotnet', RELEASE, { 'scriptInstance.ownerBackref': 0x8 }, 'CSharpInstance'),
        rel('gdscript', RELEASE, { 'scriptInstance.ownerBackref': 0x10 }, 'CSharpInstance'),
      ],
      wantFail: ['grid.binding_invariance'],
    },
    {
      name: 'cross-cell: no implementing class reported, so ownerBackref is not compared and says so',
      records: [
        rel('dotnet', RELEASE, { 'scriptInstance.ownerBackref': 0x8 }),
        rel('gdscript', RELEASE, { 'scriptInstance.ownerBackref': 0x10 }),
      ],
      wantPass: ['grid.binding_invariance'],
      wantDetail: { 'grid.binding_invariance': /did not report derivation.walk.scriptInstanceClass/ },
    },
    {
      name: 'cross-cell: debug sits exactly 0x8 above release on every shared key',
      records: [rel('dotnet', RELEASE), dbg('dotnet', DEBUG)],
      wantPass: ['grid.debug_release_delta'],
    },
    {
      name: 'cross-cell: one debug offset drifts off the uniform +0x8 relation',
      records: [rel('dotnet', RELEASE), dbg('dotnet', { ...DEBUG, 'control.size': 0x4d0 })],
      wantFail: ['grid.debug_release_delta'],
      // Any non-uniform set necessarily contains a delta that is not 0x8, so the uniformity clause
      // can never be the SOLE thing that reddens — it would be dead by the standard this whole round
      // is applying. It stays because "the relation broke on one key" and "the relation moved" are
      // different findings and lead to different investigations, and the message is asserted here so
      // the clause is not merely decorative.
      wantDetail: { 'grid.debug_release_delta': /is not uniform/ },
    },
    {
      // Uniform, so the uniformity clause is silent — only the PINNED §4.6/§12.7 constant catches
      // this. Re-deriving that constant from the run instead of pinning it is §13.11 #3, the
      // read-retry test that agreed with whatever the code did.
      name: 'cross-cell: every debug offset is release + 0x10 — uniform, but not the measured relation',
      records: [rel('dotnet', RELEASE), dbg('dotnet', { 'node.parent': 0x138, 'node.name': 0x1d0, 'control.size': 0x4d0 })],
      wantFail: ['grid.debug_release_delta'],
    },
    {
      name: 'cross-cell: a run with only one binding and one template asserts nothing and says so',
      records: [rel('dotnet', RELEASE)],
      wantSkip: ['grid.binding_invariance', 'grid.debug_release_delta'],
    },
  ];

  return cases.map((c) => {
    const checks = crossCellChecks(c.records);
    const status = (id) => checks.find((x) => x.id === id)?.status;
    const problems = [];
    for (const id of c.wantPass ?? []) if (status(id) !== 'pass') problems.push(`${id} should pass, got ${status(id)}`);
    for (const id of c.wantFail ?? []) if (status(id) !== 'fail') problems.push(`${id} should FAIL, got ${status(id)}`);
    for (const id of c.wantSkip ?? []) if (status(id) !== 'skip') problems.push(`${id} should skip, got ${status(id)}`);
    for (const [id, pattern] of Object.entries(c.wantDetail ?? {})) {
      const detail = checks.find((x) => x.id === id)?.detail ?? '';
      if (!pattern.test(detail)) problems.push(`${id} detail does not match ${pattern}: ${detail.split('\n')[0]}`);
    }
    return {
      name: c.name,
      faults: [],
      kind: 'request',
      problems,
      detail: checks.map((x) => `${x.id}=${x.status}`).join(', '),
    };
  });
}

function requestScenarios() {
  const request = buildRequest({ cell: CELL, ready: readyFor(CELL), expected });
  const disclosed = new Set([
    ...(request.anchors.visible.visiblePaths ?? []),
    ...(request.anchors.visible.hiddenPaths ?? []),
    request.anchors.visible.visiblePath,
    request.anchors.visible.hiddenPath,
  ].filter(Boolean));
  const asserted = expected.nodes.filter((n) => n.visible !== null).map((n) => n.path);
  const undisclosed = asserted.filter((p) => !disclosed.has(p));
  const problems = [];
  if (disclosed.size >= asserted.length) {
    problems.push(
      `the request discloses ${disclosed.size} of the ${asserted.length} visibility values semantic.visible `
      + 'asserts — the check cannot disagree with a driver that echoes its own anchors back');
  }
  if (undisclosed.length < 2) {
    problems.push(`only ${undisclosed.length} asserted visibility value(s) were withheld from the driver`);
  }
  if (request.anchors.visible.visibleButNotInTreePaths !== undefined) {
    problems.push('the request names the visible-but-not-in-tree node, handing over the trap that node exists to set');
  }
  return [{
    name: 'the visibility anchors intersect rather than spelling out the answer (§12.5)',
    faults: [],
    kind: 'request',
    problems,
    detail: `${disclosed.size} path(s) disclosed, ${undisclosed.length} of ${asserted.length} asserted values withheld`,
  }];
}

const C = { reset: '\x1b[0m', red: '\x1b[31m', green: '\x1b[32m', dim: '\x1b[2m', bold: '\x1b[1m' };
const paint = (c, t) => (process.stdout.isTTY ? `${c}${t}${C.reset}` : t);

let failures = 0;
const scenarioResults = [];

console.log(`${C.bold}godot-abi-grid harness selftest${C.reset}`);
console.log(`  scene: ${expected.nodeCount} nodes, depth ${expected.maxDepth}, sha256 ${expected.sourceSha256.slice(0, 12)}…`);
console.log(`  cell:  ${CELL.name}   profile: ${profile?.id ?? 'none'} (trust=${profile?.trust})`);
console.log(`  faults available: ${Object.keys(FAULTS).length}\n`);

const ALL_SCENARIOS = [...SCENARIOS, ...requestScenarios(), ...crossCellScenarios()];

for (const scenario of ALL_SCENARIOS) {
  let problems = [];
  let checks = [];

  if (scenario.kind === 'request') {
    problems = scenario.problems;
  } else {
    ({ checks } = await evaluate(scenario));
    const failed = new Set(checks.filter((c) => c.status === 'fail').map((c) => c.id));
    const passed = new Set(checks.filter((c) => c.status === 'pass').map((c) => c.id));
    const skipped = new Set(checks.filter((c) => c.status === 'skip').map((c) => c.id));

    if (scenario.mustPass === 'all') {
      if (failed.size) problems.push(`expected zero failures, got: ${[...failed].join(', ')}`);
      if (skipped.size) problems.push(`unexpected skips: ${[...skipped].join(', ')}`);
    } else if (Array.isArray(scenario.mustPass)) {
      for (const id of scenario.mustPass) {
        if (!passed.has(id)) problems.push(`${id} should still pass but did not`);
      }
    }
    for (const id of scenario.mustFail ?? []) {
      if (!failed.has(id)) problems.push(`${id} should have FAILED but did not — the harness is blind to this`);
    }
    for (const id of scenario.mustSkip ?? []) {
      if (!skipped.has(id)) problems.push(`${id} should have been SKIPPED here; this scenario assumes it cannot run`);
    }
    // "The check went red" and "the assertion I fixed went red" are different claims, and only the
    // second one is evidence that a fix is armed. A check with several clauses can redden for the
    // wrong reason and look like proof.
    // A value may be one regex or several: one check's detail line can carry two independent claims
    // (the collision scenario asserts both the conflicting key and the benign duplicate note), and
    // folding those into a single pattern would mean neither is separately armed.
    for (const [id, patterns] of Object.entries(scenario.detailMatch ?? {})) {
      const check = checks.find((c) => c.id === id);
      if (!check) { problems.push(`${id} was not produced at all`); continue; }
      for (const pattern of [patterns].flat()) {
        if (!pattern.test(check.detail ?? '')) {
          problems.push(`${id} did not fail for the expected reason — no match for ${pattern} in:\n         ${(check.detail ?? '').split('\n').slice(0, 3).join('\n         ')}`);
        }
      }
    }
  }

  const failedIds = checks.filter((c) => c.status === 'fail').map((c) => c.id);
  scenarioResults.push({
    name: scenario.name,
    faults: scenario.faults,
    caught: failedIds,
    ok: problems.length === 0,
    problems,
  });

  if (problems.length) {
    failures++;
    console.log(`${paint(C.red, 'FAIL')} ${scenario.name}`);
    for (const p of problems) console.log(`       ${p}`);
    for (const c of checks.filter((c) => c.status === 'fail')) {
      console.log(paint(C.dim, `       [${c.id}] ${c.detail.split('\n')[0]}`));
    }
  } else {
    const detail = scenario.kind === 'request'
      ? scenario.detail
      : scenario.mustPass === 'all'
        ? `${checks.filter((c) => c.status === 'pass').length} checks pass`
        : `caught: ${[...(scenario.mustFail ?? []), ...(scenario.mustSkip ?? []).map((s) => `${s} (skip)`)].join(', ') || 'nothing, as required'}`;
    console.log(`${paint(C.green, 'ok  ')} ${scenario.name}\n       ${paint(C.dim, detail)}`);
  }
}

// Recorded so REPORT.md can state what the harness itself was verified to
// catch — clearly separated from coverage, which this is not.
mkdirSync(join(HARNESS_ROOT, 'results'), { recursive: true });
writeFileSync(join(HARNESS_ROOT, 'results', 'selftest.json'), JSON.stringify({
  $schema: 'godot-abi-grid/selftest.v1',
  generatedAt: new Date().toISOString(),
  isCoverageEvidence: false,
  cell: CELL.name,
  profile: profile ? { id: profile.id, trust: profile.trust } : null,
  sceneSha256: expected.sourceSha256,
  nodeCount: expected.nodeCount,
  total: ALL_SCENARIOS.length,
  passed: ALL_SCENARIOS.length - failures,
  scenarios: scenarioResults,
}, null, 2) + '\n');

console.log();
if (failures) {
  console.log(paint(C.red, `${failures}/${ALL_SCENARIOS.length} selftest scenarios failed — do not trust calibrate.mjs results.`));
  process.exit(1);
}
console.log(paint(C.green, `${ALL_SCENARIOS.length}/${ALL_SCENARIOS.length} selftest scenarios passed.`));
console.log(paint(C.dim, '  (harness self-validation only — this is NOT compatibility evidence)'));
