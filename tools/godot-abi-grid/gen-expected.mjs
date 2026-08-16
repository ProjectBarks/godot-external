#!/usr/bin/env node
// gen-expected.mjs — derive project/expected.json from project/Main.tscn.
//
// expected.json IS the ground truth calibrate.mjs diffs against, so it must
// never be hand-maintained alongside the scene: it is generated, and it carries
// a SHA-256 of Main.tscn so calibrate.mjs can refuse to run when the two have
// drifted apart.
//
//   node gen-expected.mjs            regenerate
//   node gen-expected.mjs --check    exit 1 if expected.json is stale
//
// The .tscn subset parsed here is exactly what Main.tscn uses. It is not a
// general Godot scene parser and should not grow into one.

import { readFileSync, writeFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const HERE = dirname(fileURLToPath(import.meta.url));
const SCENE = join(HERE, 'project', 'Main.tscn');
const OUT = join(HERE, 'project', 'expected.json');

// Control-derived classes used by Main.tscn. Anything not listed is treated as
// a plain Node (no rect), which would be a scene-authoring mistake here.
const CONTROL_CLASSES = new Set(['Control', 'ColorRect', 'Panel', 'Label', 'RichTextLabel']);
const TEXT_CLASSES = new Set(['Label', 'RichTextLabel']);

// ---------------------------------------------------------------------------
// .tscn subset parser
// ---------------------------------------------------------------------------

function unescapeString(raw) {
  let out = '';
  for (let i = 0; i < raw.length; i++) {
    const ch = raw[i];
    if (ch !== '\\') {
      out += ch;
      continue;
    }
    const next = raw[++i];
    if (next === 'n') out += '\n';
    else if (next === 't') out += '\t';
    else if (next === 'r') out += '\r';
    else if (next === 'u') {
      out += String.fromCharCode(parseInt(raw.slice(i + 1, i + 5), 16));
      i += 4;
    } else out += next;
  }
  return out;
}

function parseValue(text) {
  const src = text.trim();
  if (src === 'true') return true;
  if (src === 'false') return false;
  if (src === 'null') return null;
  if (src.startsWith('"')) return unescapeString(src.slice(1, -1));

  const call = /^([A-Za-z_][A-Za-z0-9_]*)\((.*)\)$/s.exec(src);
  if (call) {
    const [, fn, argText] = call;
    const args = argText.trim() === '' ? [] : splitArgs(argText).map(parseValue);
    if (fn === 'Vector2' || fn === 'Vector2i' || fn === 'Color' || fn === 'Rect2') return args;
    return { __call: fn, args };
  }

  const num = Number(src);
  if (!Number.isNaN(num)) return num;
  return src;
}

function splitArgs(text) {
  const parts = [];
  let depth = 0;
  let inString = false;
  let current = '';
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (inString) {
      current += ch;
      if (ch === '\\') current += text[++i];
      else if (ch === '"') inString = false;
      continue;
    }
    if (ch === '"') { inString = true; current += ch; continue; }
    if (ch === '(') depth++;
    if (ch === ')') depth--;
    if (ch === ',' && depth === 0) { parts.push(current); current = ''; continue; }
    current += ch;
  }
  if (current.trim() !== '') parts.push(current);
  return parts;
}

function parseTagAttributes(text) {
  const attrs = {};
  const re = /([A-Za-z_][A-Za-z0-9_]*)\s*=\s*("(?:[^"\\]|\\.)*"|[^\s\]]+)/g;
  let m;
  while ((m = re.exec(text)) !== null) attrs[m[1]] = parseValue(m[2]);
  return attrs;
}

function parseScene(source) {
  const lines = source.split(/\r?\n/);
  const nodes = [];
  let current = null;

  for (const rawLine of lines) {
    const line = rawLine.trim();
    if (line === '' || line.startsWith(';')) continue;

    if (line.startsWith('[')) {
      const inner = line.slice(1, line.lastIndexOf(']'));
      const kind = inner.split(/\s+/, 1)[0];
      current = null;
      if (kind !== 'node') continue;
      const attrs = parseTagAttributes(inner.slice(kind.length));
      current = { name: attrs.name, class: attrs.type, scenePath: attrs.parent ?? null, props: {} };
      nodes.push(current);
      continue;
    }

    const eq = line.indexOf('=');
    if (eq < 0 || current === null) continue;
    current.props[line.slice(0, eq).trim()] = parseValue(line.slice(eq + 1));
  }

  return nodes;
}

// ---------------------------------------------------------------------------
// Layout resolution — mirrors Control::_size_changed for anchors+offsets.
// ---------------------------------------------------------------------------

function resolve(parsed, viewport) {
  const byScenePath = new Map(); // "AlphaPanel/BetaBranch" -> node
  const resolved = [];

  for (const raw of parsed) {
    const isRoot = raw.scenePath === null;
    if (!isRoot && raw.scenePath !== '.' && !byScenePath.has(raw.scenePath)) {
      throw new Error(`node "${raw.name}" declares parent "${raw.scenePath}" before it exists`);
    }

    const parent = isRoot ? null : raw.scenePath === '.' ? resolved[0] : byScenePath.get(raw.scenePath);
    const p = raw.props;
    const isControl = CONTROL_CLASSES.has(raw.class);

    const anchors = [
      p.anchor_left ?? 0, p.anchor_top ?? 0, p.anchor_right ?? 0, p.anchor_bottom ?? 0,
    ];
    const offset = [
      p.offset_left ?? 0, p.offset_top ?? 0, p.offset_right ?? 0, p.offset_bottom ?? 0,
    ];

    // Parent rect the anchors resolve against: the parent Control's size, or
    // the viewport for the scene root.
    const parentRect = parent && parent.size ? parent.size : viewport;
    const left = anchors[0] * parentRect[0] + offset[0];
    const top = anchors[1] * parentRect[1] + offset[1];
    const right = anchors[2] * parentRect[0] + offset[2];
    const bottom = anchors[3] * parentRect[1] + offset[3];

    const node = {
      name: raw.name,
      class: raw.class,
      path: isRoot ? raw.name : `${parent.path}/${raw.name}`,
      scenePath: raw.scenePath,
      depth: isRoot ? 1 : parent.depth + 1,
      parentPath: isRoot ? null : parent.path,
      childPaths: [],
      isControl,
      anchors,
      anchored: anchors.some((a) => a !== 0),
      offset: isControl ? offset : null,
      position: isControl ? [left, top] : null,
      size: isControl ? [right - left, bottom - top] : null,
      scale: isControl ? (p.scale ?? [1, 1]) : null,
      visible: p.visible ?? true,
      visibleInTree: (p.visible ?? true) && (parent ? parent.visibleInTree : true),
      text: TEXT_CLASSES.has(raw.class) ? (p.text ?? '') : null,
      hasScript: Object.hasOwn(p, 'script'),
    };

    if (parent) parent.childPaths.push(node.path);
    resolved.push(node);
    byScenePath.set(isRoot ? '.' : `${raw.scenePath === '.' ? '' : raw.scenePath + '/'}${raw.name}`, node);
  }

  return resolved;
}

// ---------------------------------------------------------------------------
// String facts. §4.6: scry truncates char32_t -> byte when building strings,
// which is silent and lossy. Record what a lossy decoder WOULD produce so the
// harness can name the bug instead of just reporting "text mismatch".
// ---------------------------------------------------------------------------

function stringFacts(text) {
  const codepoints = [...text].map((c) => c.codePointAt(0));
  return {
    text,
    codepoints,
    utf16Length: text.length,
    codepointCount: codepoints.length,
    maxCodepoint: codepoints.length ? Math.max(...codepoints) : 0,
    hasNonAscii: codepoints.some((c) => c > 0x7f),
    hasAstral: codepoints.some((c) => c > 0xffff),
    // What a char32_t -> byte truncating decoder emits (the §4.6 scry bug).
    lossyByteTruncation: String.fromCharCode(...codepoints.map((c) => c & 0xff)),
  };
}

// ---------------------------------------------------------------------------

function build() {
  const source = readFileSync(SCENE);
  const sha256 = createHash('sha256').update(source).digest('hex');
  const parsed = parseScene(source.toString('utf8'));

  const projectSource = readFileSync(join(HERE, 'project', 'project.godot'), 'utf8');
  const vw = Number(/window\/size\/viewport_width=(\d+)/.exec(projectSource)?.[1] ?? 1920);
  const vh = Number(/window\/size\/viewport_height=(\d+)/.exec(projectSource)?.[1] ?? 1080);

  const nodes = resolve(parsed, [vw, vh]);

  const sizeKey = (s) => `${s[0]}x${s[1]}`;
  const bySize = new Map();
  for (const n of nodes) {
    if (!n.size) continue;
    const key = sizeKey(n.size);
    if (!bySize.has(key)) bySize.set(key, []);
    bySize.get(key).push(n.path);
  }

  const duplicateSizeGroups = [];
  const uniqueSizes = [];
  for (const [key, paths] of bySize) {
    const [w, h] = key.split('x').map(Number);
    if (paths.length > 1) duplicateSizeGroups.push({ size: [w, h], paths });
    else uniqueSizes.push({ size: [w, h], path: paths[0] });
  }

  const round = (n) => (Number.isInteger(n) ? n : Number(n.toFixed(4)));
  const roundVec = (v) => (v ? v.map(round) : null);

  const unicodeLabel = nodes.find((n) => n.name === 'ZetaLabelUnicode');
  const asciiLabel = nodes.find((n) => n.name === 'ZetaLabelAscii');
  const richLabel = nodes.find((n) => n.name === 'ZetaRich');

  // Anchors for known-value intersection (§12.5): non-round, mutually distinct,
  // and each backed by exactly one node so a single scan is near-unique.
  //
  // The viewport size is deliberately EXCLUDED even though §12.5 used it as its
  // first anchor: 1920x1080 recurs all over a Godot process (window, viewport,
  // render targets, project settings) and is exactly the round-number trap §8.9
  // warns about. Odd components in both axes are the cheapest proxy for "does
  // not recur naturally in memory".
  const isViewport = (s) => s[0] === vw && s[1] === vh;
  const oddness = (s) => (s[0] % 2) + (s[1] % 2);
  const intersectionAnchors = uniqueSizes
    .filter(({ size }) => !isViewport(size) && size[0] > 100 && size[1] > 40 && size[0] !== size[1])
    .sort((a, b) => oddness(b.size) - oddness(a.size) || b.size[0] * b.size[1] - a.size[0] * a.size[1])
    .slice(0, 6);

  return {
    $schema: 'godot-abi-grid/expected.v1',
    generatedBy: 'gen-expected.mjs',
    generatedFrom: 'project/Main.tscn',
    sourceSha256: sha256,
    readyContract: 'godot-abi-grid/ready.v1',
    walkRoot: nodes[0].path,
    walkRootRuntimePath: `/root/${nodes[0].path}`,
    nodeCount: nodes.length,
    maxDepth: Math.max(...nodes.map((n) => n.depth)),
    viewport: [vw, vh],
    siblingCounts: nodes.filter((n) => n.childPaths.length > 0)
      .map((n) => ({ path: n.path, children: n.childPaths.length })),
    duplicateSizeGroups,
    intersectionAnchors,
    strings: {
      ascii: { path: asciiLabel.path, ...stringFacts(asciiLabel.text) },
      unicode: { path: unicodeLabel.path, ...stringFacts(unicodeLabel.text) },
      rich: { path: richLabel.path, ...stringFacts(richLabel.text) },
      names: nodes.map((n) => n.name),
    },
    visibility: {
      visiblePath: nodes.find((n) => n.name === 'VisibleTwin').path,
      hiddenPath: nodes.find((n) => n.name === 'HiddenTwin').path,
    },
    managedBridge: {
      staticRootType: 'Probe',
      staticRootField: 'Instance',
      staticRootFieldGdscript: 'instance',
      resolvesToPath: nodes[0].path,
      fields: {
        ProbeAscii: 'GridProbe ASCII 0123',
        ProbeUnicode: 'héllo ✦ 日本語',
        ProbeInt32: 613227,
        ProbeInt64: 887313409151,
        ProbeFloat: 40.9151,
        ProbeBool: true,
      },
    },
    nodes: nodes.map((n) => ({
      path: n.path,
      name: n.name,
      class: n.class,
      depth: n.depth,
      parentPath: n.parentPath,
      childPaths: n.childPaths,
      childCount: n.childPaths.length,
      isControl: n.isControl,
      anchored: n.anchored,
      anchors: roundVec(n.anchors),
      offset: roundVec(n.offset),
      position: roundVec(n.position),
      size: roundVec(n.size),
      scale: roundVec(n.scale),
      visible: n.visible,
      visibleInTree: n.visibleInTree,
      text: n.text,
      hasScript: n.hasScript,
    })),
  };
}

const expected = build();
const serialised = JSON.stringify(expected, null, 2) + '\n';

if (process.argv.includes('--check')) {
  let existing = null;
  try { existing = readFileSync(OUT, 'utf8'); } catch { /* missing */ }
  if (existing !== serialised) {
    console.error('expected.json is STALE relative to Main.tscn — run: node gen-expected.mjs');
    process.exit(1);
  }
  console.log('expected.json is current.');
  process.exit(0);
}

writeFileSync(OUT, serialised);
console.log(
  `wrote ${OUT}\n  ${expected.nodeCount} nodes, max depth ${expected.maxDepth}, ` +
  `${expected.duplicateSizeGroups.length} duplicate-size group(s), ` +
  `scene sha256 ${expected.sourceSha256.slice(0, 12)}…`,
);
