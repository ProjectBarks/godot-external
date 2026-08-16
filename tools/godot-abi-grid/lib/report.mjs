// REPORT.md generation.
//
// §8.9's whole argument is that the published README should quote MEASURED
// coverage — "4.5.1 release: 112/112 checks; 4.3 debug: calibrated, 9/11
// accessors" — instead of "supports Godot 4.x". So this file has one hard rule:
//
//   a cell that was not measured is printed as `not run` or `not built`,
//   never as a blank, never as a pass, never as an inference from a
//   neighbouring cell.
//
// Synthetic (mock-driver) results are excluded from the matrix entirely unless
// explicitly asked for, and then labelled on every row. They are harness
// self-tests; publishing them as coverage would be fabrication.

import { writeFileSync, existsSync, readFileSync } from 'node:fs';
import { join } from 'node:path';
import { fullGrid, parseCellName } from './grid.mjs';

const CHECK_ORDER = [
  ['harness.runtime_axes', 'target self-identifies as the cell it is filed under'],
  ['calibration.unaided', 'no shipped profile consumed'],
  ['structural.child_head', '(a) child-list head by pointer identity'],
  ['structural.parent', '(a) parent by pointer identity'],
  ['semantic.size', '(b) size by known-value intersection'],
  ['semantic.position', '(b) position'],
  ['semantic.scale', '(b) scale'],
  ['semantic.offset', '(b) anchor offsets'],
  ['semantic.visible', '(b) visible flag'],
  ['strings.names', '(c) node names exact'],
  ['strings.text.ascii', '(c) ASCII label text exact'],
  ['strings.text.unicode', '(c) non-ASCII label text exact'],
  ['strings.text.rich', '(c) RichTextLabel text exact (astral codepoint)'],
  ['structure.no_collapse', 'duplicated size does not collapse nodes'],
  ['structure.walk_count', '(e) full-tree walk count'],
  ['profile.agreement', '(d) agreement with the shipped §4.6 profile'],
  ['bridge.managed', 'managed static root -> NativePtr -> walk root'],
];

export function writeReport({ harnessRoot, summary, expected, outDir, reportPath, includeSynthetic = false }) {
  writeFileSync(reportPath, renderReport({ harnessRoot, summary, expected, outDir, includeSynthetic }));
}

export function renderReport({ harnessRoot, summary, expected, outDir, includeSynthetic = false }) {
  const records = new Map();
  const excludedSynthetic = [];
  for (const rec of summary?.cells ?? []) {
    if (rec.synthetic && !includeSynthetic) { excludedSynthetic.push(rec.cell); continue; }
    const parsed = parseCellName(rec.cell);
    if (!parsed) continue;
    records.set(`${parsed.versionPrefix}-${parsed.template}-${parsed.precision}-${parsed.binding}`, { rec, parsed });
  }

  const availability = summary?.availability ?? loadAvailabilityFile(outDir);
  // Only entries build.ps1 actually EXPORTED count as built. In -ListOnly mode
  // it records "buildable" rows, and a buildable cell is still not built.
  const built = new Set(
    (availability?.built ?? [])
      .filter((b) => b.status === 'built')
      .map((b) => keyOf(b.cell ?? b.name)),
  );

  const lines = [];
  const L = (s = '') => lines.push(s);

  L('# Godot ABI grid — measured coverage');
  L();
  L('<!-- GENERATED FILE. Produced by `node calibrate.mjs --report`. Do not hand-edit: the whole');
  L('     point of this table (docs/analysis.md §8.9) is that the numbers in it were measured. -->');
  L();
  L(`- Generated: \`${summary?.generatedAt ?? 'never — calibrate.mjs has not been run'}\``);
  L(`- Driver: \`${summary?.driver ?? 'n/a'}\``);
  L(`- Ground truth: \`project/expected.json\`, ${expected.nodeCount} nodes, max depth ${expected.maxDepth}, scene sha256 \`${expected.sourceSha256.slice(0, 16)}\``);
  L(`- Checks per cell: ${CHECK_ORDER.length} (see the legend below; skipped checks are not counted as passes)`);
  L();
  L('## What this table is');
  L();
  L('One row per cell of the compat matrix. `Godot.External` has measured exactly **one** layout for');
  L('real (Godot 4.5.1, release template, single precision, .NET binding — and on a *modified* engine,');
  L('per §4.6/§12.3). This grid manufactures the rest from official export templates so the claim being');
  L('published can be *"the calibrator solves layouts it has never seen"* rather than *"we know every');
  L('Godot layout"*. Stock templates are not a modified engine, so this validates the **calibrator**,');
  L('not a lookup table — which is the correct thing to validate.');
  L();
  L('A cell is evidence only if it says `n/n`. Everything else is an honest gap.');
  L();

  L('## Coverage matrix');
  L();
  L('| Cell | Engine | Built | Result | Checks | Notes |');
  L('| --- | --- | --- | --- | --- | --- |');

  let measured = 0;
  for (const want of fullGrid()) {
    const key = keyOf(want.name);
    const entry = records.get(key);
    const isBuilt = built.has(key) || Boolean(entry?.rec && entry.rec.status !== 'not-built');
    const row = renderRow(want, entry, isBuilt);
    if (entry?.rec?.status === 'pass' || entry?.rec?.status === 'fail') measured++;
    L(row);
  }
  L();
  L(`**${measured} of ${fullGrid().length} cells measured.**`);
  if (measured === 0) {
    L();
    L('> No cell has been measured. Nothing in this file may be quoted as compatibility evidence.');
    L('> This is the expected state on a machine with no Godot export templates installed;');
    L('> `pwsh ./build.ps1 -ListOnly` prints exactly what is missing.');
  }
  if (excludedSynthetic.length) {
    L();
    L(`> ${excludedSynthetic.length} synthetic (mock-driver) result(s) were EXCLUDED from the matrix:`);
    L(`> \`${excludedSynthetic.join('`, `')}\`. Those runs test the harness, not a calibrator.`);
  }
  L();

  // ---- per-cell detail ----
  const detailed = [...records.values()].filter(({ rec }) => rec.checks?.length);
  if (detailed.length) {
    L('## Per-cell check detail');
    L();
    for (const { rec } of detailed) {
      L(`### \`${rec.cell}\`${rec.isReferenceCell ? ' — reference cell' : ''}${rec.synthetic ? ' — **SYNTHETIC, not a measurement**' : ''}`);
      L();
      L(`Engine: \`${rec.engineVersion ?? 'unknown'}\` · driver: \`${rec.driver}\` · profile: \`${rec.profile?.id ?? 'none'}\``);
      L();
      L('| Check | Status | Detail |');
      L('| --- | --- | --- |');
      for (const [id] of CHECK_ORDER) {
        const check = rec.checks.find((c) => c.id === id);
        if (!check) { L(`| \`${id}\` | — | not reported |`); continue; }
        L(`| \`${id}\` | ${check.status.toUpperCase()} | ${mdCell(check.detail)} |`);
      }
      L();
    }
  }

  // ---- harness self-validation ----
  const selftest = loadJson(join(harnessRoot, 'results', 'selftest.json'));
  L('## Harness self-validation — NOT coverage');
  L();
  if (!selftest) {
    L('`results/selftest.json` is absent — run `node selftest.mjs`. Until it exists there is no');
    L('evidence that the check engine above can detect anything at all.');
  } else {
    L(`\`node selftest.mjs\` at \`${selftest.generatedAt}\`: **${selftest.passed}/${selftest.total}** scenarios.`);
    L();
    L('This drives the check engine with a synthetic driver carrying the §4.6 `' + (selftest.profile?.id ?? 'n/a')
      + '` offsets and the authored scene, then breaks one thing at a time and asserts the right check fails.');
    L('It says the harness detects these failure modes. It says **nothing** about any calibrator, and');
    L('contributes nothing to the matrix above.');
    L();
    L('| Injected fault | Checks that caught it | |');
    L('| --- | --- | --- |');
    for (const s of selftest.scenarios ?? []) {
      const faults = s.faults?.length ? s.faults.map((f) => `\`${f}\``).join(', ') : '_(none — baseline)_';
      const caught = s.caught?.length ? s.caught.map((c) => `\`${c}\``).join(', ') : '_nothing (expected)_';
      L(`| ${faults} | ${caught} | ${s.ok ? 'ok' : '**SELFTEST FAILED**'} |`);
    }
  }
  L();

  // ---- gaps ----
  L('## Gaps and how to close them');
  L();
  if (availability?.missing?.length) {
    L(`\`build.ps1\` skipped ${availability.missing.length} cell(s) on \`${availability.machine ?? 'this machine'}\` at`);
    L(`\`${availability.generatedAt ?? 'unknown time'}\`. Grouped by what is missing:`);
    L();
    const byReason = new Map();
    for (const m of availability.missing) {
      if (!byReason.has(m.missing)) byReason.set(m.missing, []);
      byReason.get(m.missing).push(m.cell);
    }
    L('| Missing | Cells |');
    L('| --- | --- |');
    for (const [reason, cells] of byReason) {
      L(`| ${mdCell(reason)} | ${cells.map((c) => `\`${c}\``).join(', ')} |`);
    }
    L();
    L('Install actions, de-duplicated:');
    L();
    const actions = new Set();
    for (const m of availability.missing) {
      for (const part of String(m.install ?? '').split(' | ')) if (part.trim()) actions.add(part.trim());
    }
    for (const action of [...actions].sort()) L(`- ${action}`);
  } else {
    L('`out/availability.json` has not been produced yet — run `pwsh ./build.ps1` (or');
    L('`pwsh ./build.ps1 -ListOnly` to see the requirements without building anything).');
  }
  L();
  L('### Known structural gaps in the grid itself');
  L();
  L('- **Double precision has no official export templates.** Godot ships single-precision templates');
  L('  only; `precision=double` requires building the engine from source with `scons precision=double`.');
  L('  Those eight cells stay unmeasurable until someone supplies custom templates');
  L('  (`build.ps1 -DoubleTemplateDir`). They are listed rather than dropped, because `real_t` width');
  L('  changes every float offset and is therefore the axis most likely to break a calibrator.');
  L('- **Compiler is a fifth axis (§8.9, from Zolt-Dump\'s table): MSVC vs GCC/MinGW.** Official');
  L('  Windows templates are MSVC-built, so this grid measures one compiler only.');
  L('- **Stock templates are not a modified engine.** StS2 runs a customised 4.5.1. A green row here');
  L('  says the calibrator solved a layout it had not seen; it does not say the shipped profile is');
  L('  right for anyone else\'s fork.');
  L();

  L('## Legend');
  L();
  L('| Result | Meaning |');
  L('| --- | --- |');
  L('| `n/n` | every applicable check passed; this cell is evidence |');
  L('| `n/m` | the calibrator ran and got some checks wrong — see the per-cell detail |');
  L('| `not built` | no export exists for this cell (missing engine or export template) |');
  L('| `not run` | built, but `calibrate.mjs` has not judged it |');
  L('| `driver unavailable` | built, but no calibration driver was configured — nothing was tested |');
  L('| `error` | the target or the driver fell over; NOT a calibrator verdict |');
  L();
  L('| Check | Asserts |');
  L('| --- | --- |');
  for (const [id, what] of CHECK_ORDER) L(`| \`${id}\` | ${what} |`);
  L();
  L('Letters `(a)`–`(e)` map to the five assertions listed in docs/analysis.md §8.9.');
  L();

  return lines.join('\n') + '\n';
}

function keyOf(cellName) {
  const parsed = parseCellName(cellName);
  if (!parsed) return cellName;
  return `${parsed.versionPrefix}-${parsed.template}-${parsed.precision}-${parsed.binding}`;
}

function renderRow(want, entry, isBuilt) {
  const cellLabel = `\`${want.name}\``;
  if (!entry) {
    const builtCol = isBuilt ? 'yes' : 'no';
    const result = isBuilt ? '`not run`' : '`not built`';
    return `| ${cellLabel} | — | ${builtCol} | ${result} | — | ${isBuilt ? 'run `node calibrate.mjs`' : 'see Gaps'} |`;
  }
  const { rec, parsed } = entry;
  const { pass = 0, fail = 0, skip = 0 } = rec.counts ?? {};
  const total = pass + fail;
  const notes = [];
  if (rec.synthetic) notes.push('**SYNTHETIC**');
  if (rec.isReferenceCell) notes.push('reference cell');
  if (skip) notes.push(`${skip} n/a`);
  if (rec.error) notes.push(mdCell(rec.error));

  let result;
  if (rec.status === 'pass') result = `**${pass}/${total}**`;
  else if (rec.status === 'fail') result = `${pass}/${total}`;
  else result = `\`${rec.status.replace('-', ' ')}\``;

  const checks = total ? `${pass} pass · ${fail} fail · ${skip} n/a` : '—';
  return `| \`${parsed.name}\` | ${rec.engineVersion ?? '—'} | yes | ${result} | ${checks} | ${notes.join('; ') || '—'} |`;
}

function mdCell(text) {
  if (text === null || text === undefined) return '—';
  return String(text).replace(/\|/g, '\\|').replace(/\r?\n/g, '<br>').slice(0, 600);
}

function loadAvailabilityFile(outDir) {
  if (!outDir) return null;
  return loadJson(join(outDir, 'availability.json'));
}

function loadJson(path) {
  if (!existsSync(path)) return null;
  try { return JSON.parse(readFileSync(path, 'utf8')); } catch { return null; }
}
