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

const HARNESS_ROOT = dirname(fileURLToPath(import.meta.url));
const CELL = parseCellName('4.5.1-release-single-dotnet');

const expected = loadExpected(HARNESS_ROOT);
const profiles = loadProfiles(HARNESS_ROOT);
const profile = selectProfile(profiles, CELL);

function readyFor(overrides = {}) {
  return {
    contract: expected.readyContract,
    pid: 4242,
    engineVersion: '4.5.1.stable',
    binding: 'dotnet',
    templateVariant: 'release',
    precision: 'single',
    isTemplate: true,
    isEditor: false,
    walkRootPath: expected.walkRootRuntimePath,
    walkCount: expected.nodeCount,
    ...overrides,
  };
}

async function evaluate(faults, readyOverrides) {
  process.env.GRID_MOCK_FAULTS = faults.join(',');
  const ready = readyFor(readyOverrides);
  const request = buildRequest({ cell: CELL, ready, expected });
  const result = await mockRun(request);
  return runChecks({ cell: CELL, expected, profile, ready, result });
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
  { name: 'managed address handed over instead of NativePtr (§4.6)', faults: ['bridge-managed-addr'], mustFail: ['bridge.managed'] },
  { name: 'cell directory mislabels the template variant', faults: [], readyOverrides: { templateVariant: 'debug' }, mustFail: ['harness.runtime_axes'] },
  { name: 'target counted a different number of nodes than expected.json', faults: [], readyOverrides: { walkCount: 19 }, mustFail: ['structure.walk_count'] },
];

const C = { reset: '\x1b[0m', red: '\x1b[31m', green: '\x1b[32m', dim: '\x1b[2m', bold: '\x1b[1m' };
const paint = (c, t) => (process.stdout.isTTY ? `${c}${t}${C.reset}` : t);

let failures = 0;
const scenarioResults = [];

console.log(`${C.bold}godot-abi-grid harness selftest${C.reset}`);
console.log(`  scene: ${expected.nodeCount} nodes, depth ${expected.maxDepth}, sha256 ${expected.sourceSha256.slice(0, 12)}…`);
console.log(`  cell:  ${CELL.name}   profile: ${profile?.id ?? 'none'} (trust=${profile?.trust})`);
console.log(`  faults available: ${Object.keys(FAULTS).length}\n`);

for (const scenario of SCENARIOS) {
  const { checks } = await evaluate(scenario.faults, scenario.readyOverrides);
  const failed = new Set(checks.filter((c) => c.status === 'fail').map((c) => c.id));
  const passed = new Set(checks.filter((c) => c.status === 'pass').map((c) => c.id));
  const problems = [];

  if (scenario.mustPass === 'all') {
    if (failed.size) problems.push(`expected zero failures, got: ${[...failed].join(', ')}`);
    const skipped = checks.filter((c) => c.status === 'skip').map((c) => c.id);
    if (skipped.length) problems.push(`unexpected skips: ${skipped.join(', ')}`);
  } else if (Array.isArray(scenario.mustPass)) {
    for (const id of scenario.mustPass) {
      if (!passed.has(id)) problems.push(`${id} should still pass but did not`);
    }
  }
  for (const id of scenario.mustFail ?? []) {
    if (!failed.has(id)) problems.push(`${id} should have FAILED but did not — the harness is blind to this`);
  }

  scenarioResults.push({
    name: scenario.name,
    faults: scenario.faults,
    caught: [...failed],
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
    const detail = scenario.mustPass === 'all'
      ? `${passed.size} checks pass`
      : `caught: ${(scenario.mustFail ?? []).join(', ')}`;
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
  total: SCENARIOS.length,
  passed: SCENARIOS.length - failures,
  scenarios: scenarioResults,
}, null, 2) + '\n');

console.log();
if (failures) {
  console.log(paint(C.red, `${failures}/${SCENARIOS.length} selftest scenarios failed — do not trust calibrate.mjs results.`));
  process.exit(1);
}
console.log(paint(C.green, `${SCENARIOS.length}/${SCENARIOS.length} selftest scenarios passed.`));
console.log(paint(C.dim, '  (harness self-validation only — this is NOT compatibility evidence)'));
