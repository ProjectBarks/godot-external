// Driver plumbing.
//
// The harness does NOT contain a calibrator. It contains the judge. The thing
// under test — Godot.External's calibration pass — is invoked through a
// deliberately dumb boundary so that the two can be developed independently and
// so the harness cannot accidentally help:
//
//   stdin   one JSON request object
//   stdout  one JSON result object   (anything on stderr is captured as notes)
//   exit 0  result produced; non-zero: treated as a driver error, not a
//           calibration failure — a cell whose driver crashed is "error", never
//           "fail", because those mean different things in REPORT.md.
//
// The request NEVER contains offsets. It contains the pid, the axes, and the
// authored ground-truth values the calibrator is allowed to use as anchors
// (§12.5: "two or three objects with independently-known values, intersected").
// Handing over the sizes is the technique; handing over the offsets would be
// cheating, and checks.mjs fails any driver that admits to using a profile.

import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { join, resolve, extname } from 'node:path';
import { pathToFileURL } from 'node:url';

export const DRIVER_CONTRACT = 'godot-abi-grid/driver.v1';

/** Ground truth the calibrator IS allowed to see: values, never offsets. */
export function buildRequest({ cell, ready, expected }) {
  return {
    contract: DRIVER_CONTRACT,
    pid: ready?.pid ?? null,
    executable: cell.exePath,
    cell: {
      name: cell.name,
      version: cell.version,
      template: cell.template,
      precision: cell.precision,
      binding: cell.binding,
    },
    runtime: ready ?? null,
    anchors: {
      walkRootPath: expected.walkRootRuntimePath,
      // Known-value anchors for semantic intersection.
      sizes: expected.intersectionAnchors,
      visible: expected.visibility,
      // Structural anchors need no values at all — pointer identity only.
      nodeCount: expected.nodeCount,
      names: expected.strings.names,
      managedStatic: cell.binding === 'dotnet'
        ? { type: expected.managedBridge.staticRootType, field: expected.managedBridge.staticRootField }
        : null,
    },
    require: {
      structuralMethod: 'pointer-identity',
      semanticMethod: 'known-value-intersection',
      reportNodes: true,
      reportText: true,
    },
  };
}

export function resolveDriver(spec, harnessRoot) {
  const chosen = spec ?? process.env.GRID_DRIVER ?? 'auto';

  if (chosen === 'mock') {
    return { kind: 'module', id: 'mock', path: join(harnessRoot, 'drivers', 'mock.mjs'), synthetic: true };
  }

  if (chosen === 'auto') {
    if (process.env.GRID_CALIBRATOR) {
      return { kind: 'exec', id: 'env:GRID_CALIBRATOR', command: process.env.GRID_CALIBRATOR, args: [] };
    }
    // The intended production driver: a thin CLI over Godot.External's
    // calibration pass. It lives outside this directory on purpose — the
    // harness must not be able to reach into the implementation.
    const projectDir = join(harnessRoot, '..', '..', 'src', 'Godot.External.Calibrator');
    if (existsSync(join(projectDir, 'Godot.External.Calibrator.csproj'))) {
      return {
        kind: 'exec',
        id: 'dotnet:Godot.External.Calibrator',
        command: 'dotnet',
        args: ['run', '--project', projectDir, '-c', 'Release', '--'],
      };
    }
    return {
      kind: 'unavailable',
      id: 'auto',
      reason:
        'no calibration driver found. Provide one of:\n'
        + '    * src/Godot.External.Calibrator/Godot.External.Calibrator.csproj  (auto-detected), or\n'
        + '    * GRID_CALIBRATOR=<path to an executable implementing godot-abi-grid/driver.v1>, or\n'
        + '    * --driver <path>\n'
        + '  See README.md > "Driver contract".',
    };
  }

  const asPath = resolve(harnessRoot, chosen);
  if (existsSync(asPath) && ['.mjs', '.js'].includes(extname(asPath))) {
    return { kind: 'module', id: chosen, path: asPath };
  }
  return { kind: 'exec', id: chosen, command: chosen, args: [] };
}

export async function runDriver(driver, request, { timeoutMs = 120000 } = {}) {
  if (driver.kind === 'unavailable') {
    const err = new Error(driver.reason);
    err.code = 'DRIVER_UNAVAILABLE';
    throw err;
  }

  if (driver.kind === 'module') {
    const mod = await import(pathToFileURL(driver.path).href);
    const fn = mod.run ?? mod.default;
    if (typeof fn !== 'function') {
      throw new Error(`module driver ${driver.path} exports neither run() nor a default function`);
    }
    return await fn(request);
  }

  return await new Promise((resolvePromise, rejectPromise) => {
    const child = spawn(driver.command, driver.args, { stdio: ['pipe', 'pipe', 'pipe'], shell: false });
    let out = '';
    let err = '';
    const timer = setTimeout(() => {
      child.kill();
      rejectPromise(new Error(`driver ${driver.id} timed out after ${timeoutMs}ms`));
    }, timeoutMs);

    child.stdout.on('data', (d) => { out += d; });
    child.stderr.on('data', (d) => { err += d; });
    child.on('error', (e) => { clearTimeout(timer); rejectPromise(e); });
    child.on('close', (code) => {
      clearTimeout(timer);
      if (code !== 0) {
        rejectPromise(new Error(`driver ${driver.id} exited ${code}\n${err.trim()}`));
        return;
      }
      try {
        const parsed = JSON.parse(extractJson(out));
        if (err.trim()) parsed.notes = [...(parsed.notes ?? []), ...err.trim().split(/\r?\n/)];
        resolvePromise(parsed);
      } catch (e) {
        rejectPromise(new Error(`driver ${driver.id} did not emit parseable JSON: ${e.message}\n--- stdout ---\n${out.slice(0, 2000)}`));
      }
    });

    child.stdin.end(JSON.stringify(request));
  });
}

/** Tolerate a driver that prints log lines around its JSON object. */
function extractJson(text) {
  const trimmed = text.trim();
  if (trimmed.startsWith('{')) return trimmed;
  const start = trimmed.indexOf('\n{');
  const end = trimmed.lastIndexOf('}');
  if (start >= 0 && end > start) return trimmed.slice(start + 1, end + 1);
  return trimmed;
}
