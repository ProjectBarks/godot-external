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
      // `anchors` is a Control field, exactly like the four below it. Emitting [0,0,0,0] for a bare
      // Node was a spec contradiction rather than a harmless default: geometry.absent asserts that a
      // node with no CanvasItem reports null for every geometry field including this one, so a
      // driver publishing precisely what the ground truth stated would FAIL. Ground truth and the
      // check cannot disagree about the same field.
      anchors: isControl ? anchors : null,
      anchored: isControl && anchors.some((a) => a !== 0),
      offset: isControl ? offset : null,
      position: isControl ? [left, top] : null,
      size: isControl ? [right - left, bottom - top] : null,
      scale: isControl ? (p.scale ?? [1, 1]) : null,
      // `visible` is a CanvasItem field. A bare Node (DeltaSiblingOne, the
      // parent-pointer tie-breaker) simply does not have one, and recording
      // `true` for it would ask a calibrator to read a field that is not there.
      visible: isControl ? (p.visible ?? true) : null,
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
// The two-instance rule.
//
// The calibrator publishes an offset only when decode-set ⊆ property-set with a
// non-empty decode-set. Over a ONE-element property-set that is arithmetically
// vacuous — any field of the right shape on that one node satisfies it — so the
// calibrator withholds, correctly, and the field is never measured. Eight grid
// series went by with richTextLabel.text at 0/24 for exactly this reason, and
// the fixture had three more one-element sets behind it: the only scripted node
// (which the whole managed bridge hangs off), the only hidden node, and the only
// node with non-zero anchors.
//
// This is a FIXTURE property, so it is enforced here rather than in checks.mjs:
// a scene that cannot support a measurement should fail at generation time, not
// quietly produce a green cell with a blank column in it. Withholding on thin
// evidence stays the calibrator's correct behaviour — nothing here relaxes it.
//
// Traps are counted but NOT enforced: a decoy only has to exist once (the
// duplicated 409x151 size, the visible-but-not-in-tree node). They are listed so
// the distinction is written down rather than remembered.
// ---------------------------------------------------------------------------

function fixtureCensus(nodes) {
  const paths = (f) => nodes.filter(f).map((n) => n.path);

  const byClass = new Map();
  for (const n of nodes) byClass.set(n.class, (byClass.get(n.class) ?? 0) + 1);
  const classes = [...byClass].map(([cls, count]) => ({ class: cls, count }))
    .sort((a, b) => b.count - a.count || a.class.localeCompare(b.class));

  const discriminators = [
    { id: 'script', measures: 'node.scriptInstance, and every bridge reading behind it',
      paths: paths((n) => n.hasScript) },
    { id: 'not-canvas-item', measures: 'Node::data.parent, separated from parent_item/parent_control',
      paths: paths((n) => !n.isControl) },
    { id: 'hidden', measures: 'canvasItem.visible',
      paths: paths((n) => n.visible === false) },
    { id: 'anchored', measures: 'Control Data.offset[4], separated from Data.anchor[4]',
      paths: paths((n) => n.anchored) },
    { id: 'scaled', measures: 'control.scale, against an accessor that returns identity',
      paths: paths((n) => n.scale && (n.scale[0] !== 1 || n.scale[1] !== 1)) },
    { id: 'text', measures: 'label.text / richTextLabel.text',
      paths: paths((n) => n.text !== null) },
  ].map((d) => ({ ...d, count: d.paths.length }));

  const traps = [
    { id: 'visible-not-in-tree', catches: 'a reader that returns is_visible_in_tree() for `visible`',
      paths: paths((n) => n.visible === true && n.visibleInTree === false) },
  ].map((t) => ({ ...t, count: t.paths.length }));

  const thin = [
    ...classes.filter((c) => c.count < 2).map((c) => `class ${c.class} has ${c.count} instance(s)`),
    ...discriminators.filter((d) => d.count < 2)
      .map((d) => `discriminator "${d.id}" (${d.measures}) has ${d.count} instance(s): ${d.paths.join(', ') || 'none'}`),
  ];
  if (thin.length) {
    throw new Error(
      'Main.tscn breaks the two-instance rule:\n  - ' + thin.join('\n  - ')
      + '\n\n  A property backed by ONE node cannot be derived: decode-set ⊆ property-set is'
      + '\n  vacuously true for any field of the right shape, so the calibrator withholds and the'
      + '\n  field is never measured on any cell. Add a second instance that can DISAGREE with the'
      + '\n  first — a different value, a different parent, a different mode — rather than a copy.'
      + '\n  Do not relax this by relaxing the calibrator; the withhold is correct.',
    );
  }

  return { rule: 'every class and every discriminator needs >= 2 instances', classes, discriminators, traps };
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
  // The SECOND RichTextLabel. A class with one instance cannot be published by
  // a calibrator whose rule is decode-set ⊆ class-set, decode-set ≠ ∅: with one
  // member the subset test is vacuously true for any string-shaped field, so it
  // withholds — correctly — and richTextLabel.text went underived on every cell
  // of eight grid series while label.text (two instances) derived on 23/24.
  //
  // Its text is the RAW BBCode source, because that is what RichTextLabel stores
  // in its String member (§4.6); the rendered text has the tags stripped and is
  // held in a separate item tree. What is recorded here is what is STORED.
  const richBbcodeLabel = nodes.find((n) => n.name === 'OmegaRich');

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
    // ready.v2 added rawTree/rawWalkCount: the engine's own child lists, internal
    // children included. Cells exported against a v1 probe must fail loudly rather
    // than fall back to a node count the in-memory walk cannot reproduce.
    readyContract: 'godot-abi-grid/ready.v2',
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
      richBbcode: { path: richBbcodeLabel.path, bbcode: true, ...stringFacts(richBbcodeLabel.text) },
      names: nodes.map((n) => n.name),
    },
    fixture: fixtureCensus(nodes),
    visibility: {
      // visiblePath/hiddenPath are the original single pair and are load-bearing
      // in checks.mjs; they stay. The plural forms are what a calibrator needs to
      // INTERSECT — one hidden node cannot separate `visible` from any other byte
      // that happens to be zero on it.
      visiblePath: nodes.find((n) => n.name === 'VisibleTwin').path,
      hiddenPath: nodes.find((n) => n.name === 'HiddenTwin').path,
      hiddenPaths: nodes.filter((n) => n.visible === false).map((n) => n.path),
      visiblePaths: nodes.filter((n) => n.visible === true).map((n) => n.path),
      // Visible in itself, invisible in the tree: Godot 4's CanvasItem keeps
      // `visible` and `parent_visible_in_tree` next to each other, and this is the
      // only node where the two disagree in that direction. `visible` is TRUE here.
      visibleButNotInTreePaths: nodes
        .filter((n) => n.visible === true && n.visibleInTree === false).map((n) => n.path),
    },
    managedBridge: {
      staticRootType: 'Probe',
      staticRootField: 'Instance',
      staticRootFieldGdscript: 'instance',
      resolvesToPath: nodes[0].path,
      // Every node carrying a script. node.scriptInstance must be non-null on
      // exactly these and null on all the others; with one entry that was not a
      // constraint. NOTE: lib/driver.mjs does not forward this to the driver yet,
      // so a calibrator still has to discover it structurally.
      scriptedPaths: nodes.filter((n) => n.hasScript).map((n) => n.path),
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
