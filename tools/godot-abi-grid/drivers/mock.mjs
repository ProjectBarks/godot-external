// SYNTHETIC driver — for testing the HARNESS, never for measuring a calibrator.
//
// It fabricates a driver result straight out of expected.json, so of course it
// passes. Its only job is to answer "would checks.mjs actually catch this?",
// which is why it can inject faults on demand:
//
//   node calibrate.mjs --driver mock --mock-faults lossy-text,collapse-dup
//
// Results produced by this driver are tagged `synthetic: true` and REPORT.md
// refuses to fold them into the coverage matrix without --include-synthetic,
// which then labels every such row loudly. A published number that came from
// here would be a fabricated measurement, which §8.9 is specifically trying to
// stop us from shipping.

import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';
import { loadExpected } from '../lib/expected.mjs';
import { loadProfiles, selectProfile } from '../lib/profiles.mjs';
import { parseCellName } from '../lib/grid.mjs';

const HARNESS_ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');

export const FAULTS = {
  'lossy-text': 'decode char32_t -> byte (the §4.6 scry bug)',
  'truncate-text': 'stop at the wrong CowData length',
  'collapse-dup': 'give both 409x151 nodes the same native pointer',
  'drop-node': 'omit one node from the walk',
  'bad-parent': 'report a wrong parent pointer for one node',
  'profile-mismatch': 'derive control.size 8 bytes off',
  'used-profile': 'admit to using a shipped profile',
  'wrong-structural-method': 'derive structural offsets by value scanning',
  'single-sample': 'derive semantic offsets from one control',
  'anchor-confusion': 'read Data.anchor[4] where Data.offset[4] was wanted',
  'visible-blind': 'report every node as visible',
  'bridge-managed-addr': 'hand back the managed address instead of NativePtr',
  'phantom-text': 'invent text for an authored node that has no text member',
  'phantom-text-internal': 'invent text for an engine-internal node the scene never authored',
  'wrong-text': 'report a plausible but incorrect string for a node that does have text',
  'phantom-geometry': 'invent geometry and visibility for a node with no CanvasItem base',
  'phantom-anchors': 'invent an anchor quad for a node with no CanvasItem base',
  'wrong-anchors': 'report a plausible but incorrect anchor quad for a Control',
  'drop-bare-node': 'omit a non-Control node from the walk',
  'drop-gchandle': 'never derive scriptInstance.gcHandle, and say nothing about it',
  'drop-bbcode-text': 'withhold the raw BBCode node text entirely',
  // Every offset wrong, every reading right. Not reachable by a driver that reads THROUGH the
  // offsets it derived, and that is the point: it isolates what the grid asserts about the offsets
  // themselves from what it asserts about the values read with them. On a cell no profile covers,
  // nothing at all used to notice.
  'flat-offsets': 'derive 0x1 for every offset while reporting every reading correctly',
  'visible-as-byte': 'publish canvasItem.visible as the raw memory byte (1) instead of a bool',
  'text-as-number': 'invent a non-string value for a node with no text member',
  'used-profile-string': 'report usedProfile as the string "true" instead of a boolean',
  'no-used-profile': 'omit usedProfile entirely rather than claiming anything',
  'profile-consulted-empty': 'report profileConsulted as an empty string',
  'bridge-no-fields': 'resolve the managed object but read none of its field values',
  'bridge-hollow-reverse': 'report a reverse chain whose ownerBackref and gcHandle are null',
  'no-script-instance-class': 'never report derivation.walk.scriptInstanceClass',
  'lowercase-script-instance-class': 'report the implementing class as "csharpinstance"',
  'declare-everything-notderived': 'declare every profile key it does not use as notDerived',
  'empty-notderived-reason': 'declare a key notDerived with an empty reason string',
  'samples-map-omits-key': 'report per-key sample counts with canvasItem.visible missing',
  'walkcount-as-string': 'report walkCount as the string "21"',
  'mangle-internal-name': 'rename an engine-internal child the authored scene never mentions',
  'mangle-name': 'report a plausible but wrong name for one authored node',
  'partial-anchors': 'publish correct anchor quads on only half the Controls',
  // The merge hole. Every site that wanted the derived offsets as one flat record spread the three
  // groups together, and a spread keeps the LAST mention of a duplicated key — so a driver that
  // reported one key twice had one of its two answers dropped with no complaint anywhere. This
  // reports node.name in `structural` as well as `strings` with the two values EIGHT APART, which is
  // the shape a real confusion would take (§4.6/§12.7's debug/release delta), and label.text twice
  // with the same value, which is benign and must be disclosed rather than scored.
  'offset-group-collision': 'report node.name in two derivation groups with different values, and label.text in two with the same one',
};

function fakePtr(index) {
  return `0x${(0x7ff000000000 + index * 0x1000).toString(16)}`;
}

export async function run(request) {
  const faults = new Set(
    (process.env.GRID_MOCK_FAULTS ?? request.mockFaults ?? '')
      .split(',').map((s) => s.trim()).filter(Boolean),
  );

  const expected = loadExpected(HARNESS_ROOT);
  const cell = parseCellName(request.cell.name) ?? request.cell;
  const profile = selectProfile(loadProfiles(HARNESS_ROOT), cell);

  // Offsets a real calibrator would DERIVE. The mock takes the profile where
  // one exists and otherwise invents a self-consistent set — it is not
  // pretending to know other versions' layouts, it is exercising the judge.
  const base = profile?.offsets ?? {
    'node.scriptInstance': 0x68,
    'node.parent': 0x128,
    'node.childListHead': 0x148,
    'node.name': 0x1c0,
    'canvasItem.visible': 0x370,
    'control.globalPosition': 0x3f8,
    'control.offset': 0x470,
    'control.scale': 0x4a8,
    'control.position': 0x4b8,
    'control.size': 0x4c0,
    // 0x7f8 is `text`; 0x800 is `xl_text`, the translated copy that shares the same CowData
    // allocation when no translation resolves. §4.6 recorded 0x800 and the grid measured 0x7f8
    // across three passes on stock templates, so this mock — which stands in for a CORRECT
    // calibrator — reports the lower slot of the pair.
    'label.text': 0x7f8,
    'richTextLabel.text': 0xa78,
  };
  const offsets = { ...base };
  if (faults.has('profile-mismatch')) offsets['control.size'] += 8;

  const walkOffsets = {
    'childList.next': 0x0,
    'childList.node': 0x18,
    'scriptInstance.ownerBackref': cell.binding === 'dotnet' ? 0x8 : 0x10,
    ...(faults.has('drop-gchandle') ? {} : { 'scriptInstance.gcHandle': 0x20 }),
  };

  // Every offset wrong, every reading right — the mutation the audit ran by hand. A driver deriving
  // offsets and then reading THROUGH them cannot easily reach this state, which is exactly why it
  // isolates what the grid asserts about the offsets from what it asserts about the readings.
  if (faults.has('flat-offsets')) {
    for (const key of Object.keys(offsets)) offsets[key] = 0x1;
    for (const key of Object.keys(walkOffsets)) walkOffsets[key] = 0x1;
  }

  // The driver excusing itself from most of the comparison. Keeps eight offsets and declares the
  // other nine notDerived, which used to score a green "8/8 offsets match" over a 17-key profile.
  const EXCUSED = ['node.scriptInstance', 'canvasItem.visible', 'control.globalPosition', 'control.anchor', 'richTextLabel.text'];
  const EXCUSED_WALK = ['childList.next', 'childList.node', 'scriptInstance.ownerBackref', 'scriptInstance.gcHandle'];
  const notDerived = faults.has('declare-everything-notderived')
    ? Object.fromEntries([...EXCUSED, ...EXCUSED_WALK]
      .map((k) => [k, 'this driver does not read this field and is not asked to']))
    : faults.has('empty-notderived-reason')
      ? { 'control.anchor': '' }
      : { 'control.anchor': 'not an accessor this driver is asked to derive' };
  if (faults.has('declare-everything-notderived')) {
    for (const key of EXCUSED) delete offsets[key];
    for (const key of EXCUSED_WALK) delete walkOffsets[key];
  }

  const ptrByPath = new Map();
  expected.nodes.forEach((n, i) => ptrByPath.set(n.path, fakePtr(i + 1)));
  if (faults.has('collapse-dup')) {
    const group = expected.duplicateSizeGroups[0];
    ptrByPath.set(group.paths[1], ptrByPath.get(group.paths[0]));
  }

  let sourceNodes = expected.nodes;
  if (faults.has('drop-node')) {
    sourceNodes = sourceNodes.filter((n) => n.name !== 'EpsilonSibling');
  }

  // Halves the population geometry.absent is supposed to cover. The check must not simply report
  // full coverage over what is left.
  if (faults.has('drop-bare-node')) {
    const bare = sourceNodes.find((n) => !n.isControl);
    if (bare) sourceNodes = sourceNodes.filter((n) => n !== bare);
  }

  const nodes = sourceNodes.map((n) => {
    let text = n.text;
    if (text !== null && faults.has('lossy-text')) {
      const facts = Object.values(expected.strings).find((s) => s && s.path === n.path);
      if (facts) text = facts.lossyByteTruncation;
    }
    if (text !== null && faults.has('truncate-text') && n.name === 'ZetaRich') {
      text = text.slice(0, 4);
    }

    let parentPtr = n.parentPath ? ptrByPath.get(n.parentPath) : null;
    if (faults.has('bad-parent') && n.name === 'DeltaSiblingTwo') parentPtr = fakePtr(999);

    let offset = n.offset;
    if (offset && faults.has('anchor-confusion') && n.anchored) offset = n.anchors;

    return {
      name: n.name,
      class: n.class,
      path: n.path,
      nativePtr: ptrByPath.get(n.path),
      parentPtr,
      childPtrs: n.childPaths.map((p) => ptrByPath.get(p)).filter(Boolean),
      size: n.size,
      position: n.position,
      scale: n.scale,
      offset,
      // Only a CanvasItem has an anchor quad. expected.json records [0,0,0,0] for every node
      // including the bare ones, because gen-expected emits the field unconditionally — but a
      // correct driver reads nothing there, and this driver's job is to model a correct one.
      anchors: n.isControl ? n.anchors : null,
      visible: faults.has('visible-blind') ? true : n.visible,
      text,
    };
  });

  // The engine's own child lists, internal children included (lib/rawtree.mjs). A real target
  // reports these in ready.v2; every synthetic ready file until now omitted them, so
  // resolveWalkModel fell through to `authoredOnly` in every scenario and the whole module had zero
  // selftest coverage. When the ready file carries a raw tree, this driver walks it — because that
  // is what a memory walk actually sees.
  const rawTree = Array.isArray(request.runtime?.rawTree) ? request.runtime.rawTree : null;
  if (rawTree) {
    let extra = 0;
    for (const row of rawTree) {
      if (expected.byPath.has(row.path)) continue;
      ptrByPath.set(row.path, fakePtr(9000 + extra++));
    }
    const firstInternal = rawTree.find((r) => !expected.byPath.has(r.path));
    for (const row of rawTree) {
      if (expected.byPath.has(row.path)) continue;
      // Only the positional name check can see this: the node is not in expected.json, so every
      // check that iterates the authored scene skips it entirely.
      const name = faults.has('mangle-internal-name') && row === firstInternal ? `${row.name}_RENAMED` : row.name;
      nodes.push({
        name,
        class: row.class ?? 'Node',
        path: row.path,
        nativePtr: ptrByPath.get(row.path),
        parentPtr: ptrByPath.get(row.path.slice(0, row.path.lastIndexOf('/'))) ?? null,
        childPtrs: (row.children ?? []).map((c) => ptrByPath.get(`${row.path}/${c}`)).filter(Boolean),
        size: null,
        position: null,
        scale: null,
        offset: null,
        anchors: null,
        visible: null,
        text: null,
      });
    }
    // Splice each internal child into its parent's child list at the ordinal the engine reports, so
    // the driver's childPtrs reproduce the tree a memory walk would see rather than the authored one.
    const nodeByPath = new Map(nodes.map((n) => [n.path, n]));
    for (const row of rawTree) {
      const parent = nodeByPath.get(row.path);
      if (!parent || !Array.isArray(row.children)) continue;
      parent.childPtrs = row.children.map((c) => ptrByPath.get(`${row.path}/${c}`)).filter(Boolean);
    }
  }

  const rootPtr = ptrByPath.get(expected.walkRoot);

  // Both of these reproduce real defects, and the second reproduces one that a check iterating
  // expected.json alone cannot see: the walk contains engine-internal children the authored scene
  // never mentions, and a driver that invents text for THOSE was scoring clean.
  if (faults.has('phantom-geometry')) {
    // All zeros, which is what a bare Node actually reads as through Control offsets — and is
    // perfectly plausible geometry, so only class identity can reject it.
    const victim = nodes.find((n) => n.size === null);
    if (victim) {
      victim.visible = true;
      victim.size = [0, 0];
      victim.position = [0, 0];
      victim.scale = [0, 0];
      victim.offset = [0, 0, 0, 0];
    }
  }

  if (faults.has('phantom-anchors')) {
    const victim = nodes.find((n) => n.size === null);
    if (victim) victim.anchors = [0, 0, 0, 0];
  }

  // canvasItem.visible is a BYTE in memory. A driver that publishes the raw read without converting
  // it produces `1`, not `true` — and the normaliser's `typeof === 'boolean'` test turned that into
  // null, i.e. "reported nothing", which is exactly what geometry.absent was written to reward. The
  // check built to catch a fabricated `visible=true` on a bare Node was defeated by the more likely
  // version of the same fabrication.
  if (faults.has('visible-as-byte')) {
    const victim = nodes.find((n) => n.size === null);
    if (victim) victim.visible = 1;
  }

  // Same hole in strings.text.absent. The check exists because a driver published "res://Probe.gd"
  // on plain Controls; a non-string invented value was invisible to it.
  if (faults.has('text-as-number')) {
    const victim = nodes.find((n) => n.text === null);
    if (victim) victim.text = 12345;
  }

  if (faults.has('mangle-name')) {
    const victim = nodes.find((n) => n.name === 'ZetaLabelUnicode');
    if (victim) victim.name = 'ZetaLabelUnicodeX';
  }

  // Correct anchors, on some of the Controls. The check compared only what was published and printed
  // "23/23 nodes exact", so a driver could read Data.anchor[4] where it agreed and omit it where it
  // did not, and pick its own denominator after the fact.
  if (faults.has('partial-anchors')) {
    const controls = nodes.filter((n) => n.anchors !== null);
    controls.slice(Math.ceil(controls.length / 2)).forEach((n) => { n.anchors = null; });
  }

  if (faults.has('wrong-anchors')) {
    // Offsets stay correct; only the anchors are wrong. Nothing in the harness looked at them, so
    // this scored clean.
    const victim = nodes.find((n) => n.anchors !== null && n.offset !== null);
    if (victim) victim.anchors = [0.25, 0.25, 0.75, 0.75];
  }

  if (faults.has('drop-bbcode-text')) {
    const victim = nodes.find((n) => n.name === 'OmegaRich');
    if (victim) victim.text = null;
  }

  if (faults.has('wrong-text')) {
    const victim = nodes.find((n) => n.text !== null);
    if (victim) victim.text = 'res://Main.tscn';
  }

  if (faults.has('phantom-text')) {
    const victim = nodes.find((n) => n.text === null);
    if (victim) victim.text = 'res://Probe.gd';
  }

  if (faults.has('phantom-text-internal')) {
    nodes.push({
      name: '@VScrollBar@2',
      class: 'VScrollBar',
      nativePtr: fakePtr(7777),
      parentPtr: rootPtr,
      childPtrs: [],
      size: [8, 100],
      position: [0, 0],
      scale: [1, 1],
      offset: [0, 0, 8, 100],
      anchors: [0, 0, 0, 0],
      visible: true,
      text: 'HiddenTwin',
    });
  }

  // A driver that cannot state, as a boolean, whether it consumed a shipped profile cannot have its
  // runs published as evidence that it needed none. The string and the omission both used to pass.
  const usedProfile = faults.has('used-profile-string') ? 'true' : faults.has('used-profile');

  return {
    driver: 'mock',
    driverVersion: '0.1.0-synthetic',
    synthetic: true,
    ...(faults.has('no-used-profile') ? {} : { usedProfile }),
    ...(faults.has('profile-consulted-empty') ? { profileConsulted: '' } : {}),
    engineVersion: request.runtime?.engineVersion ?? `${cell.version}.stable`,
    walkCount: faults.has('walkcount-as-string') ? String(nodes.length) : nodes.length,
    derivation: {
      structural: {
        method: faults.has('wrong-structural-method') ? 'value-scan' : 'pointer-identity',
        offsets: {
          'node.parent': offsets['node.parent'],
          'node.childListHead': offsets['node.childListHead'],
          'node.scriptInstance': offsets['node.scriptInstance'],
          // Both of these belong to `strings` and are reported there too, a few lines below. The
          // conflicting one is FIRST here on purpose: a spread merge keeps the last group, so the
          // strings copy silently won and the harness saw only the correct value — which is exactly
          // why nothing ever noticed. The readings stay correct throughout, like flat-offsets: this
          // fault is about the offsets the driver publishes, not about what it reads with them.
          ...(faults.has('offset-group-collision')
            ? { 'node.name': offsets['node.name'] + 8, 'label.text': offsets['label.text'] }
            : {}),
        },
        evidence: {
          childListHead: 'only slot p where *(p+0x18) is a known child pointer',
          parent: "only slot in a child equal to the parent's own pointer",
        },
      },
      semantic: {
        method: 'known-value-intersection',
        offsets: {
          'control.size': offsets['control.size'],
          'control.position': offsets['control.position'],
          'control.scale': offsets['control.scale'],
          'control.offset': offsets['control.offset'],
          'canvasItem.visible': offsets['canvasItem.visible'],
          'control.globalPosition': offsets['control.globalPosition'],
        },
        // A per-key map is the shape the LIVE calibrator emits, and a key missing from it used to
        // make the >= 2-sample guard evaporate rather than fire: `samples?.[key] ?? samples` fell
        // back to the map object, which is neither an array nor a number, so sampleCount went null
        // and `null < 2` is false. canvasItem.visible is absent from that map in 14 live results.
        samples: faults.has('samples-map-omits-key')
          ? { 'control.size': 6, 'control.position': 20, 'control.scale': 17, 'control.offset': 6 }
          : faults.has('single-sample') ? 1 : request.anchors.sizes.length,
        candidates: { 'control.size': [offsets['control.size']] },
      },
      strings: {
        method: 'cowdata-bulk-utf32',
        offsets: Object.fromEntries(['node.name', 'label.text', 'richTextLabel.text']
          .filter((k) => offsets[k] !== undefined).map((k) => [k, offsets[k]])),
      },
      // A correct driver EXPLAINS its gaps rather than leaving them silent, because silence is
      // indistinguishable from having derived the offset correctly. The harness no longer knows any
      // key by name — this is the driver's claim to make and to justify. It is also the driver's own
      // denominator, so checks.mjs caps how much of the profile it may excuse this way.
      notDerived,
      walk: {
        // The implementing C++ ScriptInstance class, as a real driver reads it off the instance's
        // own vtable. It is NOT the cell's binding: one mono build hosts .cs and .gd scripted nodes
        // side by side, so the question is per-object. This mock has one script language per cell,
        // so the two coincide here — which is exactly why the grid alone cannot prove the per-class
        // model, and why profiles.json records that ownerBackref is asserted rather than measured.
        ...(faults.has('no-script-instance-class')
          ? {}
          : {
            scriptInstanceClass: faults.has('lowercase-script-instance-class')
              ? 'csharpinstance'
              : (cell.binding === 'dotnet' ? 'CSharpInstance' : 'GDScriptInstance'),
          }),
        offsets: walkOffsets,
      },
    },
    nodes,
    managedBridge: cell.binding === 'dotnet'
      ? {
        staticRootType: expected.managedBridge.staticRootType,
        staticRootField: expected.managedBridge.staticRootField,
        nativePtr: faults.has('bridge-managed-addr') ? fakePtr(4242) : rootPtr,
        // A reverse chain of nulls used to print "reverse ScriptInstance chain verified".
        reverse: faults.has('bridge-hollow-reverse')
          ? { ownerBackref: null, gcHandle: null }
          : { ownerBackref: rootPtr, gcHandle: fakePtr(5000) },
        // The six expected values had never been compared on any cell in 224 live runs, because the
        // comparison skipped any key the driver had not supplied — so supplying none scored green.
        fields: faults.has('bridge-no-fields') ? {} : expected.managedBridge.fields,
      }
      : null,
    notes: faults.size ? [`synthetic faults injected: ${[...faults].join(', ')}`] : [],
  };
}

export default run;
