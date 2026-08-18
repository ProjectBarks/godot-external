#!/usr/bin/env node
// mutate-verify.mjs — is each fix actually ARMED?
//
// §13.11: "The only defence that has actually worked here is the crude one: revert the change and
// confirm the test goes red. Every fix in the later rounds was validated that way, and it caught #2
// and #3 directly. Static reading of the test never did."
//
// So this does exactly that, mechanically. For every fix in this round it copies the harness to a
// scratch directory, applies a MUTATION that reverts that one fix to the behaviour it had before,
// runs selftest.mjs there, and asserts the selftest fails AND names the scenario that was supposed
// to catch it. A mutation that leaves the selftest green means the scenario is decorative.
//
//   node mutate-verify.mjs
//
// This is not a coverage number and it is not evidence about any calibrator. It answers one
// question: would the selftest notice if these fixes were undone?

import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { cpSync, mkdtempSync, readFileSync, writeFileSync, rmSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { spawnSync } from 'node:child_process';

const HARNESS_ROOT = dirname(fileURLToPath(import.meta.url));

/**
 * Each entry reverts ONE fix.
 *
 * `expect` names the selftest scenario(s) that must break. Requiring the name — not merely "the
 * selftest went red" — is the difference between "something noticed" and "the thing written to
 * notice this noticed it". A mutation caught only by an unrelated scenario is a mutation whose own
 * scenario is not armed.
 */
const MUTATIONS = [
  {
    id: 'offsets-consistency',
    what: '#1 the internal-consistency check on derived offsets',
    file: 'lib/checks.mjs',
    from: 'checks.push(checkOffsetConsistency(cell, result));',
    to: "checks.push(pass('offsets.internal_consistency', 'neutered', 'neutered'));",
    expect: ['every derived offset is 0x1', 'every offset is 0x1 on a cell NO shipped profile covers'],
  },
  {
    id: 'bridge-fields',
    what: '#2 bridge.managed comparing the six expected managed field values',
    file: 'lib/checks.mjs',
    from: 'if (!(key in fields)) { absent.push(key); continue; }',
    to: 'if (!(key in fields)) { continue; }',
    expect: ['none of its field values are read'],
  },
  {
    id: 'unresolvable-scored',
    what: '#3 an unresolvable profile key scored instead of silently benign',
    file: 'lib/checks.mjs',
    from: '  if (unresolvable.length) {',
    to: '  if (false && unresolvable.length) {',
    expect: ['never reported, so ownerBackref goes uncompared'],
  },
  {
    id: 'owner-case',
    what: '#3 case-insensitive implementing-class lookup',
    file: 'lib/checks.mjs',
    from: '? Object.keys(wantRaw).find((k) => k.toLowerCase() === owner.trim().toLowerCase())',
    to: '? Object.keys(wantRaw).find((k) => k === owner)',
    expect: ['"csharpinstance" is still compared'],
  },
  {
    id: 'geometry-malformed',
    what: '#4 geometry.absent rejecting malformed geometry, not only absent geometry',
    file: 'lib/checks.mjs',
    from: '      if (got.malformed.includes(field)) {',
    to: '      if (false && got.malformed.includes(field)) {',
    expect: ['raw memory byte 1 on a bare Node'],
  },
  {
    id: 'text-malformed',
    what: '#5 strings.text.absent rejecting a non-string invented value',
    file: 'lib/checks.mjs',
    from: "    if (got.malformed.includes('text')) {",
    to: "    if (false && got.malformed.includes('text')) {",
    expect: ['non-string value invented for a node with no text member'],
  },
  {
    id: 'used-profile-type',
    what: '#6 calibration.unaided requiring usedProfile to be a boolean',
    file: 'lib/checks.mjs',
    from: "    usedProfile: Object.hasOwn(raw, 'usedProfile') ? raw.usedProfile : undefined,",
    to: '    usedProfile: raw.usedProfile === true,',
    expect: ['usedProfile reported as the string "true"', 'usedProfile omitted entirely'],
  },
  {
    id: 'profile-consulted-empty',
    what: '#6 the never-exercised empty profileConsulted clause',
    file: 'lib/checks.mjs',
    // The faithful revert is the ORIGINAL truthiness test, not merely deleting the empty-string
    // branch: `if (result.profileConsulted)` is falsy for '' and skips the whole block. Disabling
    // the branch alone still fails, through the clause below it — which is why the first attempt at
    // this mutation passed and proved nothing. A mutation that does not restore the old BEHAVIOUR
    // is not a test of the fix, it is a test of the diff.
    from: '  if (consulted !== undefined && consulted !== null) {',
    to: '  if (consulted) {',
    expect: ['profileConsulted reported as an empty string'],
  },
  {
    id: 'names-tautology',
    what: '#7 strings.names comparing against a source other than node.name',
    file: 'lib/checks.mjs',
    from: "    const wantName = path.split('/').pop();",
    to: '    const wantName = got.name;',
    expect: ['plausible but wrong name for one authored node', 'engine-internal child .* wrong name'],
  },
  {
    id: 'parent-positional',
    what: '#8 structural.parent identifying nodes by child list rather than by parentPtr',
    file: 'lib/checks.mjs',
    from: '  const roots = [...byPtr.keys()].filter((p) => !claimed.has(p));',
    to: '  const roots = result.nodes.filter((n) => !n.parentPtr || !byPtr.has(n.parentPtr)).map((n) => n.nativePtr);',
    expect: ['wrong parent pointer is reported as a wrong parent'],
  },
  {
    id: 'notderived-cap',
    what: '#9 the ceiling on how much of the profile a driver may excuse',
    file: 'lib/checks.mjs',
    from: '  if (notDerived.length > excusable) {',
    to: '  if (false && notDerived.length > excusable) {',
    expect: ['excuses nine of seventeen profile keys'],
  },
  {
    id: 'notderived-reason',
    what: '#9 requiring a usable reason on a notDerived declaration',
    file: 'lib/checks.mjs',
    from: '  if (badReasons.length) {',
    to: '  if (false && badReasons.length) {',
    expect: ['empty reason string'],
  },
  {
    id: 'anchor-denominator',
    what: '#9 refusing a partial anchor publication',
    file: 'lib/checks.mjs',
    from: '  if (anchorsPublished > 0 && anchorsPublished < controls.length) {',
    to: '  if (false) {',
    expect: ['only half the Controls'],
  },
  {
    id: 'hollow-reverse',
    what: '#10 requiring the reverse ScriptInstance chain to resolve to something',
    file: 'lib/checks.mjs',
    // The whole of the old behaviour: absent reverse was fine, and a reverse whose pointers were
    // null was fine, because the only clause was `if (back.ownerBackref && ...)`. Reverting one
    // third of that leaves the other two catching the fault, which proves nothing.
    edits: [
      { from: '  if (!back) {', to: '  if (false) {' },
      { from: '    if (!backPtr) {', to: '    if (back.ownerBackref && false) {' },
      { from: '    } else if (rootPtr && backPtr !== rootPtr) {', to: '    } else if (back.ownerBackref && rootPtr && backPtr !== rootPtr) {' },
      { from: '    if (!handlePtr) {', to: '    if (false) {' },
    ],
    expect: ['reverse ScriptInstance chain of nulls'],
  },
  {
    id: 'sample-guard',
    what: '#11 the >= 2-sample guard firing on an unreadable sample count',
    file: 'lib/checks.mjs',
    from: '  if (sampleCount.error) {',
    to: '  if (false && sampleCount.error) {',
    expect: ['per-key sample counts that omit the key'],
  },
  {
    id: 'walkcount-coercion',
    what: '#12 walkCount not being silently replaced by nodes.length',
    file: 'lib/checks.mjs',
    from: '    walkCount: Number.isInteger(raw.walkCount) ? raw.walkCount : null,',
    to: "    walkCount: typeof raw.walkCount === 'number' ? raw.walkCount : nodes.length,",
    expect: ['walkCount reported as the string'],
  },
  {
    id: 'offset-group-collision',
    what: '#13 a key reported in two derivation groups with different values is not silently merged',
    file: 'lib/checks.mjs',
    // The old behaviour is the spread merge in BOTH places — last group wins, nothing complains.
    // Reverting one site alone leaves the other reddening the scenario, which would prove nothing
    // about the site that was reverted; see hollow-reverse for the same trap. Restoring the literal
    // spread is also the point: replacing the call with a helper that still reports conflicts would
    // be testing the diff rather than the behaviour.
    edits: [
      {
        from: '  const { merged: derived, conflicts, duplicates } = mergeDerivationGroups(result);',
        to: '  const derived = { ...result.structural.offsets, ...result.semantic.offsets, ...result.strings.offsets };\n'
          + '  const conflicts = [];\n  const duplicates = [];',
      },
      {
        from: '  const { merged: derived, conflicts: mergeConflicts } = mergeDerivationGroups(result);',
        to: '  const derived = { ...result.structural.offsets, ...result.semantic.offsets, ...result.strings.offsets };\n'
          + '  const mergeConflicts = [];',
      },
    ],
    expect: ['one offset key reported by two derivation groups with different values'],
  },
  {
    id: 'corroboration-name-required',
    what: 'T5b an agreement must NAME the getter it decoded (§13.2)',
    file: 'lib/checks.mjs',
    from: '    if (!named) {',
    to: '    if (false) {',
    expect: ['no getter name attached'],
  },
  {
    id: 'corroboration-value-only-on-agree',
    what: 'T5b no verdict but `agree` may carry a value',
    file: 'lib/checks.mjs',
    from: '      if (published !== null && published !== undefined) {',
    to: '      if (false) {',
    expect: ['value published on a record whose two routes did not corroborate'],
  },
  {
    id: 'corroboration-matches-derivation',
    what: 'T5b a corroborated value must match the same result\'s own derived offset',
    file: 'lib/checks.mjs',
    from: '    if (own !== published) {',
    to: '    if (false) {',
    expect: ['contradicts the same result'],
  },
  {
    id: 'corroboration-disagreement-scored',
    what: 'T5b two derivations disagreeing FAILS the cell rather than being a caveat',
    file: 'lib/checks.mjs',
    from: "      if (verdict === 'disagree') {",
    to: '      if (false) {',
    expect: ['two independent derivations disagree'],
  },
  {
    id: 'visibility-anchors',
    what: 'Species 1 — visibility anchors that intersect rather than spelling out the answer',
    file: 'lib/driver.mjs',
    from: '      visible: visibilityAnchors(expected),',
    to: '      visible: expected.visibility,',
    expect: ['visibility anchors intersect'],
  },
  {
    id: 'binding-invariance',
    what: '#1(b) cross-cell — bindings of one build must derive the same offsets',
    file: 'lib/crosscell.mjs',
    from: '      if (distinct.size > 1) {',
    to: '      if (false) {',
    expect: ['cells of one build disagree on node.name'],
  },
  {
    id: 'script-instance-scoping',
    what: '#1(b) cross-cell — scriptInstance.* is scoped to the implementing class, not the build',
    file: 'lib/crosscell.mjs',
    from: '      if (isScriptInstanceMember(k)) {',
    to: '      if (false) {',
    expect: ['DIFFERENT implementing classes, legitimately', 'no implementing class reported'],
  },
  {
    id: 'debug-delta-uniform',
    what: '#1(a) cross-cell — the debug/release delta must be uniform across keys',
    file: 'lib/crosscell.mjs',
    from: '    if (deltas.size > 1) {',
    to: '    if (false) {',
    expect: ['drifts off the uniform'],
  },
  {
    id: 'debug-delta-pinned',
    what: '#1(a) cross-cell — the delta is the PINNED §4.6 constant, not one re-derived from the run',
    file: 'lib/crosscell.mjs',
    from: '      if (delta !== DEBUG_RELEASE_DELTA) {',
    to: '      if (false) {',
    expect: ['uniform, but not the measured relation'],
  },
  {
    id: 'check-catalog',
    what: 'the reporting defect — CHECK_CATALOG and the checks actually run cannot drift',
    file: 'lib/checks.mjs',
    from: "  ['geometry.absent', '(b) nodes with no CanvasItem report no geometry, not garbage'],",
    to: '',
    // Not a scenario: the guard throws, so EVERY scenario dies. That is the intended blast radius —
    // a catalogue that does not match the checks must not be able to produce a report at all.
    expectThrow: 'CHECK_CATALOG and the checks actually run have drifted',
  },
];

const C = { reset: '\x1b[0m', red: '\x1b[31m', green: '\x1b[32m', dim: '\x1b[2m', bold: '\x1b[1m' };
const paint = (c, t) => (process.stdout.isTTY ? `${c}${t}${C.reset}` : t);

console.log(`${C.bold}godot-abi-grid mutation check${C.reset}  — revert each fix, confirm the selftest goes red\n`);

const scratch = mkdtempSync(join(tmpdir(), 'grid-mutate-'));
let failures = 0;
const records = [];

for (const m of MUTATIONS) {
  const dir = join(scratch, m.id);
  cpSync(HARNESS_ROOT, dir, {
    recursive: true,
    filter: (src) => !src.includes(`${'out'}${'/'}`) && !/[\\/](out|results|build-staging|bin|undefined|node_modules)([\\/]|$)/.test(src.slice(HARNESS_ROOT.length)),
  });

  const target = join(dir, m.file);
  let source = readFileSync(target, 'utf8');
  const edits = m.edits ?? [{ from: m.from, to: m.to }];
  let anchorProblem = null;
  for (const edit of edits) {
    const occurrences = source.split(edit.from).length - 1;
    if (occurrences !== 1) { anchorProblem = `anchor matched ${occurrences} time(s), not 1: ${edit.from}`; break; }
    source = source.replace(edit.from, edit.to);
  }
  if (anchorProblem) {
    failures++;
    console.log(`${paint(C.red, 'FAIL')} ${m.id} — ${anchorProblem}`);
    records.push({ id: m.id, what: m.what, ok: false, problems: [anchorProblem] });
    continue;
  }
  writeFileSync(target, source);

  const run = spawnSync(process.execPath, ['selftest.mjs'], { cwd: dir, encoding: 'utf8', env: { ...process.env, GRID_MOCK_FAULTS: '' } });
  const output = `${run.stdout ?? ''}${run.stderr ?? ''}`;
  const problems = [];

  if (run.status === 0) {
    problems.push('the selftest still passed with this fix reverted — nothing is testing it');
  }
  if (m.expectThrow) {
    if (!output.includes(m.expectThrow)) problems.push(`expected the harness to refuse to run with "${m.expectThrow}"`);
  } else {
    const brokenScenarios = [...output.matchAll(/^FAIL (.+)$/gm)].map((x) => x[1]);
    for (const want of m.expect) {
      if (!brokenScenarios.some((s) => new RegExp(want).test(s))) {
        problems.push(`no scenario matching /${want}/ broke; broken were: ${brokenScenarios.join(' | ') || 'none'}`);
      }
    }
  }

  records.push({ id: m.id, what: m.what, ok: problems.length === 0, problems });
  if (problems.length) {
    failures++;
    console.log(`${paint(C.red, 'FAIL')} ${m.id}  ${paint(C.dim, m.what)}`);
    for (const p of problems) console.log(`       ${p}`);
  } else {
    console.log(`${paint(C.green, 'ok  ')} ${m.id}  ${paint(C.dim, m.what)}`);
  }
}

rmSync(scratch, { recursive: true, force: true });

writeFileSync(join(HARNESS_ROOT, 'results', 'mutations.json'), JSON.stringify({
  $schema: 'godot-abi-grid/mutations.v1',
  generatedAt: new Date().toISOString(),
  isCoverageEvidence: false,
  total: MUTATIONS.length,
  passed: MUTATIONS.length - failures,
  mutations: records,
}, null, 2) + '\n');

console.log();
if (failures) {
  console.log(paint(C.red, `${failures}/${MUTATIONS.length} fixes are NOT demonstrably armed.`));
  process.exit(1);
}
console.log(paint(C.green, `${MUTATIONS.length}/${MUTATIONS.length} fixes go red when reverted.`));
