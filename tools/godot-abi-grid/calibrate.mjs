#!/usr/bin/env node
// calibrate.mjs — the publish gate for Godot.External (docs/analysis.md §8.9).
//
// For every built grid cell: launch it, hand a calibration driver the pid and
// the authored ground-truth VALUES (never offsets), and judge what comes back
// against project/expected.json.
//
//   node calibrate.mjs                       run every built cell
//   node calibrate.mjs --only 4.5.1-release  run one
//   node calibrate.mjs --driver mock         self-test the harness (synthetic)
//   node calibrate.mjs --report              also regenerate REPORT.md
//   node calibrate.mjs --list                show what is built vs missing
//
// The reference cell (4.5.x release single dotnet) always runs FIRST. §8.9:
// that cell is already known good, so a failure there is a harness bug, and a
// harness bug misread as a calibrator failure on 4.3-debug is the single most
// expensive mistake this tool can make.

import { mkdirSync, writeFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { setTimeout as delay } from 'node:timers/promises';

import { discoverCells, fullGrid, isReferenceCell, loadAvailability } from './lib/grid.mjs';
import { loadExpected } from './lib/expected.mjs';
import { loadProfiles, selectProfile } from './lib/profiles.mjs';
import { runChecks, mergeDerivationGroups } from './lib/checks.mjs';
import { crossCellChecks } from './lib/crosscell.mjs';
import { launchTarget, killTarget, readyFilePathFor } from './lib/target.mjs';
import { resolveDriver, runDriver, buildRequest } from './lib/driver.mjs';
import { writeReport } from './lib/report.mjs';

const HARNESS_ROOT = dirname(fileURLToPath(import.meta.url));

// ---------------------------------------------------------------------------

function parseArgs(argv) {
  const opts = {
    out: join(HARNESS_ROOT, 'out'),
    results: join(HARNESS_ROOT, 'results'),
    driver: null,
    only: null,
    headless: process.env.GRID_HEADLESS === '1',
    timeout: 60000,
    driverTimeout: 180000,
    report: false,
    list: false,
    launch: true,
    // Pause between "target is ready" and "run the driver". DEFAULT 0.
    //
    // The history is worth keeping: 2500ms here once made
    // Godot.External.Calibrator fail to locate a walk root on 18 consecutive
    // attempts, because RootLocator collapsed every name slot's
    // "_Data -> buffer" distance onto one value taken from whichever slot a
    // Dictionary enumerated last. The longer a target ran, the more decoy copies
    // of the node names its heap held, and the more reliably a decoy won.
    //
    // RootLocator now carries that distance per slot and orders its results by
    // address, so root location no longer depends on how long the scene has been
    // running. Letting the scene settle is otherwise the right thing to measure
    // -- some semantic readings do move over the first frames -- so the option
    // stays available, but 0 remains the default until a run demonstrates that a
    // settled scene measures something the immediate attach does not.
    settle: 0,
  };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    const next = () => argv[++i];
    switch (a) {
      case '--out': opts.out = join(HARNESS_ROOT, next()); break;
      case '--results': opts.results = join(HARNESS_ROOT, next()); break;
      case '--driver': opts.driver = next(); break;
      case '--only': opts.only = next(); break;
      case '--headless': opts.headless = true; break;
      case '--timeout': opts.timeout = Number(next()); break;
      case '--driver-timeout': opts.driverTimeout = Number(next()); break;
      case '--report': opts.report = true; break;
      case '--list': opts.list = true; break;
      case '--no-launch': opts.launch = false; break;
      case '--settle': opts.settle = Math.max(0, Number(next())); break;
      case '--mock-faults': process.env.GRID_MOCK_FAULTS = next(); break;
      case '-h': case '--help': printHelp(); process.exit(0); break;
      default:
        console.error(`unknown argument: ${a}`);
        printHelp();
        process.exit(2);
    }
  }
  return opts;
}

function printHelp() {
  console.log(`godot-abi-grid calibrate

  --out <dir>            built cells (default ./out)
  --results <dir>        where per-cell JSON is written (default ./results)
  --driver <spec>        auto | mock | <path.mjs> | <executable>   (default auto)
  --only <substring>     run only cells whose name contains this
  --headless             pass --headless to the target
  --timeout <ms>         how long to wait for a target to become ready
  --driver-timeout <ms>  how long to allow the calibration driver
  --report               regenerate REPORT.md from results afterwards
  --list                 print the grid's build status and exit
  --no-launch            do not launch targets (driver finds its own process)
  --mock-faults <list>   fault injection for --driver mock (see drivers/mock.mjs)
`);
}

// ---------------------------------------------------------------------------

const C = {
  reset: '\x1b[0m', dim: '\x1b[2m', bold: '\x1b[1m',
  red: '\x1b[31m', green: '\x1b[32m', yellow: '\x1b[33m', cyan: '\x1b[36m',
};
const paint = (color, text) => (process.stdout.isTTY ? `${color}${text}${C.reset}` : text);

function statusGlyph(status) {
  if (status === 'pass') return paint(C.green, 'PASS');
  if (status === 'fail') return paint(C.red, 'FAIL');
  return paint(C.yellow, 'SKIP');
}

// ---------------------------------------------------------------------------

/**
 * Run a cell ONCE.
 *
 * This function used to retry, up to 14 times, whenever the driver reported it
 * had failed to locate a walk root. That crutch existed for exactly one defect:
 * RootLocator collapsed each name slot's "_Data -> buffer" distance onto a
 * single value taken from whichever slot a Dictionary happened to enumerate
 * last, so root location succeeded roughly two runs in five on an unchanging
 * 4.5-release-single-dotnet target and essentially never on 4.3 .NET.
 *
 * That defect is fixed: NameSlots now carries the distance per slot and returns
 * deduped, address-ordered results. Two consecutive full-grid runs at one
 * attempt per cell located a walk root on all 16 cell-runs, zero misses. With
 * the coin flip gone the retry has nothing left to correct, and a harness that
 * can silently re-roll a cell is a harness that cannot report nondeterminism it
 * has not yet noticed — so it is gone. One attempt, and the cell reports exactly
 * the result it earned.
 */
async function runCell({ cell, expected, profiles, driver, opts }) {
  const record = {
    cell: cell.name,
    axes: { version: cell.version, template: cell.template, precision: cell.precision, binding: cell.binding },
    isReferenceCell: isReferenceCell(cell),
    driver: driver.id,
    synthetic: driver.synthetic === true,
    startedAt: new Date().toISOString(),
    status: 'error',
    counts: { pass: 0, fail: 0, skip: 0 },
    checks: [],
    error: null,
  };

  let child = null;
  try {
    let ready = null;
    if (opts.launch) {
      const launched = await launchTarget(cell, {
        readyFile: readyFilePathFor(HARNESS_ROOT, cell),
        timeoutMs: opts.timeout,
        headless: opts.headless,
      });
      child = launched.child;
      ready = launched.ready;
      if (opts.settle > 0) await delay(opts.settle);
    }

    const request = buildRequest({ cell, ready, expected });
    const raw = await runDriver(driver, request, { timeoutMs: opts.driverTimeout });
    if (raw?.synthetic) record.synthetic = true;

    const profile = selectProfile(profiles, cell);
    const { checks, counts, normalised } = runChecks({ cell, expected, profile, ready, result: raw });

    record.checks = checks;
    record.counts = counts;
    // Carried on the record, not just in rawResult, because summary.json strips rawResult and the
    // cross-cell checks (lib/crosscell.mjs) read the summary. An offset that only one cell can see
    // is an offset nothing can contradict.
    //
    // The flattening is mergeDerivationGroups' rather than a spread, for the same reason
    // lib/checks.mjs uses it: a key claimed by two groups with two values resolved to whichever one
    // the spread happened to keep, and this record is what the CROSS-CELL checks compare. Feeding
    // grid.binding_invariance an arbitrary winner would have it agree or disagree about a number no
    // driver actually stands behind. offsets.internal_consistency has already reddened the cell by
    // this point; the conflicting keys are withheld from the comparison and named instead.
    const flat = mergeDerivationGroups(normalised);
    const conflictingKeys = new Set(flat.conflicts.map((c) => c.key));
    record.derived = {
      offsets: Object.fromEntries(Object.entries(flat.merged).filter(([k]) => !conflictingKeys.has(k))),
      walk: normalised.walk,
      scriptInstanceClass: normalised.scriptInstanceClass,
      ...(flat.conflicts.length ? { offsetConflicts: flat.conflicts.map((c) => c.text) } : {}),
    };
    record.status = counts.fail > 0 ? 'fail' : 'pass';
    record.engineVersion = ready?.engineVersion ?? raw?.engineVersion ?? null;
    record.profile = profile ? { id: profile.id, trust: profile.trust } : null;
    // Kept on the record itself, not just in rawResult: summary.json strips
    // rawResult, and the driver's own account of what it could not determine is
    // the most useful thing in REPORT.md.
    record.notes = Array.isArray(raw?.notes) ? raw.notes : [];
    record.rawResult = raw;
    record.error = null;
  } catch (err) {
    record.status = err.code === 'DRIVER_UNAVAILABLE' ? 'driver-unavailable' : 'error';
    record.error = err.message;
  } finally {
    await killTarget(child);
    record.finishedAt = new Date().toISOString();
  }

  return record;
}

// ---------------------------------------------------------------------------

async function main() {
  const opts = parseArgs(process.argv.slice(2));
  const expected = loadExpected(HARNESS_ROOT);
  const profiles = loadProfiles(HARNESS_ROOT);
  const built = discoverCells(opts.out);
  const availability = loadAvailability(opts.out);

  if (opts.list) {
    const builtNames = new Set(built.map((c) => c.name));
    console.log(`${C.bold}grid status${C.reset}  (out: ${opts.out})\n`);
    for (const want of fullGrid()) {
      const match = built.find((b) => b.versionPrefix === want.versionPrefix
        && b.template === want.template && b.precision === want.precision && b.binding === want.binding);
      const label = match ? paint(C.green, `built as ${match.name}`) : paint(C.dim, 'not built');
      console.log(`  ${want.name.padEnd(34)} ${label}`);
    }
    if (!builtNames.size) {
      console.log(`\n  nothing built yet — run build.ps1 first (see README.md).`);
    }
    if (availability?.missing?.length) {
      console.log(`\n  build.ps1 reported ${availability.missing.length} skipped cell(s); see out/availability.json`);
    }
    return 0;
  }

  const cells = built.filter((c) => (opts.only ? c.name.includes(opts.only) : true));
  const driver = resolveDriver(opts.driver, HARNESS_ROOT);

  console.log(`${C.bold}godot-abi-grid${C.reset}  driver=${driver.id}${driver.synthetic ? paint(C.yellow, ' (SYNTHETIC — harness self-test, not a measurement)') : ''}`);
  console.log(`  ground truth: ${expected.nodeCount} nodes, depth ${expected.maxDepth}, scene ${expected.sourceSha256.slice(0, 12)}…`);
  console.log(`  cells built:  ${cells.length}${opts.only ? ` (filtered by "${opts.only}")` : ''}\n`);

  if (!cells.length) {
    console.log(paint(C.yellow, '  no built cells found.'));
    console.log('  This is the expected state on a machine without Godot export templates.');
    console.log('  Run build.ps1 -ListOnly to see exactly what must be installed.\n');
  }

  mkdirSync(opts.results, { recursive: true });
  const records = [];

  for (const cell of cells) {
    process.stdout.write(`${C.bold}${cell.name}${C.reset}${isReferenceCell(cell) ? paint(C.cyan, '  [reference cell — validates the harness]') : ''}\n`);
    if (!cell.exeExists) {
      console.log(`  ${paint(C.yellow, 'SKIP')} executable missing: ${cell.exePath}\n`);
      records.push({ cell: cell.name, status: 'not-built', counts: { pass: 0, fail: 0, skip: 0 }, checks: [] });
      continue;
    }

    const record = await runCell({ cell, expected, profiles, driver, opts });
    records.push(record);

    if (record.status === 'driver-unavailable' || record.status === 'error') {
      console.log(`  ${paint(C.red, record.status.toUpperCase())} ${record.error}\n`);
    } else {
      for (const check of record.checks) {
        console.log(`  ${statusGlyph(check.status)} ${check.id.padEnd(26)} ${check.title}`);
        if (check.status !== 'pass' || process.env.GRID_VERBOSE === '1') {
          console.log(`       ${paint(C.dim, check.detail)}`);
        }
      }
      const { pass: p, fail: f, skip: s } = record.counts;
      console.log(`  ${record.status === 'pass' ? paint(C.green, '=>') : paint(C.red, '=>')} ${p}/${p + f} checks passed${s ? `, ${s} skipped` : ''}\n`);
    }

    writeFileSync(join(opts.results, `${cell.name}.json`), JSON.stringify(record, null, 2));

    if (isReferenceCell(cell) && record.status === 'fail') {
      console.log(paint(C.red, '  !! the reference cell failed. §8.9: fix the HARNESS before trusting any other row.\n'));
    }
  }

  // Assertions no single cell can make. A calibrator can be perfectly self-consistent inside one
  // cell and still contradict the cell next to it, and until now nothing looked.
  const gridChecks = crossCellChecks(records);
  if (gridChecks.some((c) => c.status !== 'skip')) {
    console.log(`${C.bold}cross-cell${C.reset}  (assertions that need more than one cell)`);
    for (const check of gridChecks) {
      console.log(`  ${statusGlyph(check.status)} ${check.id.padEnd(26)} ${check.title}`);
      if (check.status !== 'pass' || process.env.GRID_VERBOSE === '1') {
        console.log(`       ${paint(C.dim, check.detail)}`);
      }
    }
    console.log();
  }

  const summary = {
    $schema: 'godot-abi-grid/summary.v1',
    generatedAt: new Date().toISOString(),
    harnessRoot: HARNESS_ROOT,
    driver: driver.id,
    synthetic: driver.synthetic === true || records.some((r) => r.synthetic),
    sceneSha256: expected.sourceSha256,
    expectedNodeCount: expected.nodeCount,
    availability,
    gridChecks,
    cells: records.map(({ rawResult, ...rest }) => rest),
  };
  writeFileSync(join(opts.results, 'summary.json'), JSON.stringify(summary, null, 2));
  console.log(`wrote ${join(opts.results, 'summary.json')}`);

  if (opts.report) {
    const reportPath = join(HARNESS_ROOT, 'REPORT.md');
    writeReport({ harnessRoot: HARNESS_ROOT, summary, expected, outDir: opts.out, reportPath });
    console.log(`wrote ${reportPath}`);
  }

  const failed = records.filter((r) => r.status === 'fail' || r.status === 'error');
  const gridFailed = gridChecks.filter((c) => c.status === 'fail');
  return failed.length || gridFailed.length ? 1 : 0;
}

main().then((code) => process.exit(code), (err) => {
  console.error(`${C.red}calibrate.mjs: ${err.message}${C.reset}`);
  if (process.env.GRID_VERBOSE === '1') console.error(err.stack);
  process.exit(2);
});

export { main };
