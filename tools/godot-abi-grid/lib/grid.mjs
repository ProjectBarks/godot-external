// Grid axes and cell-directory conventions (docs/analysis.md §8.9).
//
// A cell directory is named  <version>-<template>-<precision>-<binding>
// e.g.  out/4.5.1-release-single-dotnet/
// and contains grid.exe, grid.pck and cell.json (written by build.ps1).

import { readdirSync, readFileSync, existsSync, statSync } from 'node:fs';
import { join } from 'node:path';

export const AXES = {
  version: ['4.2', '4.3', '4.4', '4.5', '4.6'],
  template: ['release', 'debug'],
  precision: ['single', 'double'],
  binding: ['gdscript', 'dotnet'],
};

/** The cell §12 already measured. Ordered first everywhere so a harness bug
 *  shows up on known-good ground rather than on a version nobody can check. */
export const REFERENCE_CELL = { versionPrefix: '4.5', template: 'release', precision: 'single', binding: 'dotnet' };

export function parseCellName(name) {
  const m = /^(\d+\.\d+(?:\.\d+)?)-(release|debug)-(single|double)-(gdscript|dotnet)$/.exec(name);
  if (!m) return null;
  const [, version, template, precision, binding] = m;
  return {
    name,
    version,
    versionPrefix: version.split('.').slice(0, 2).join('.'),
    template,
    precision,
    binding,
  };
}

export function cellName({ version, template, precision, binding }) {
  return `${version}-${template}-${precision}-${binding}`;
}

export function isReferenceCell(cell) {
  return cell.versionPrefix === REFERENCE_CELL.versionPrefix
    && cell.template === REFERENCE_CELL.template
    && cell.precision === REFERENCE_CELL.precision
    && cell.binding === REFERENCE_CELL.binding;
}

/** Sort so the reference cell runs first, then by version/template/precision. */
export function sortCells(cells) {
  const rank = (c) => [
    isReferenceCell(c) ? 0 : 1,
    c.version,
    c.template === 'release' ? 0 : 1,
    c.precision === 'single' ? 0 : 1,
    c.binding === 'dotnet' ? 0 : 1,
  ];
  return [...cells].sort((a, b) => {
    const ra = rank(a); const rb = rank(b);
    for (let i = 0; i < ra.length; i++) {
      if (ra[i] < rb[i]) return -1;
      if (ra[i] > rb[i]) return 1;
    }
    return 0;
  });
}

/** Every cell the grid WANTS, independent of what was actually built. */
export function fullGrid() {
  const cells = [];
  for (const version of AXES.version) {
    for (const template of AXES.template) {
      for (const precision of AXES.precision) {
        for (const binding of AXES.binding) {
          cells.push({
            name: cellName({ version, template, precision, binding }),
            version,
            versionPrefix: version,
            template,
            precision,
            binding,
          });
        }
      }
    }
  }
  return sortCells(cells);
}

/** Discover built cells under outDir. Missing directory is not an error. */
export function discoverCells(outDir) {
  if (!existsSync(outDir)) return [];
  const cells = [];
  for (const entry of readdirSync(outDir)) {
    const dir = join(outDir, entry);
    if (!statSync(dir).isDirectory()) continue;
    const parsed = parseCellName(entry);
    if (!parsed) continue;

    let manifest = null;
    const manifestPath = join(dir, 'cell.json');
    if (existsSync(manifestPath)) {
      try { manifest = JSON.parse(readFileSync(manifestPath, 'utf8')); } catch { manifest = null; }
    }

    const exePath = manifest?.executable
      ? join(dir, manifest.executable)
      : join(dir, 'grid.exe');

    cells.push({ ...parsed, dir, manifest, exePath, exeExists: existsSync(exePath) });
  }
  return sortCells(cells);
}

/** Load out/availability.json if build.ps1 produced one. */
export function loadAvailability(outDir) {
  const path = join(outDir, 'availability.json');
  if (!existsSync(path)) return null;
  try { return JSON.parse(readFileSync(path, 'utf8')); } catch { return null; }
}
