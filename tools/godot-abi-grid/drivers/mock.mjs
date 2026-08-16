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
    'label.text': 0x800,
    'richTextLabel.text': 0xa78,
  };
  const offsets = { ...base };
  if (faults.has('profile-mismatch')) offsets['control.size'] += 8;

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
      anchors: n.anchors,
      visible: faults.has('visible-blind') ? true : n.visible,
      text,
    };
  });

  const rootPtr = ptrByPath.get(expected.walkRoot);

  return {
    driver: 'mock',
    driverVersion: '0.1.0-synthetic',
    synthetic: true,
    usedProfile: faults.has('used-profile'),
    engineVersion: request.runtime?.engineVersion ?? `${cell.version}.stable`,
    walkCount: nodes.length,
    derivation: {
      structural: {
        method: faults.has('wrong-structural-method') ? 'value-scan' : 'pointer-identity',
        offsets: {
          'node.parent': offsets['node.parent'],
          'node.childListHead': offsets['node.childListHead'],
          'node.scriptInstance': offsets['node.scriptInstance'],
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
        samples: faults.has('single-sample') ? 1 : request.anchors.sizes.length,
        candidates: { 'control.size': [offsets['control.size']] },
      },
      strings: {
        method: 'cowdata-bulk-utf32',
        offsets: {
          'node.name': offsets['node.name'],
          'label.text': offsets['label.text'],
          'richTextLabel.text': offsets['richTextLabel.text'],
        },
      },
      walk: {
        offsets: {
          'childList.next': 0x0,
          'childList.node': 0x18,
          'scriptInstance.ownerBackref': 0x8,
          'scriptInstance.gcHandle': 0x20,
        },
      },
    },
    nodes,
    managedBridge: cell.binding === 'dotnet'
      ? {
        staticRootType: expected.managedBridge.staticRootType,
        staticRootField: expected.managedBridge.staticRootField,
        nativePtr: faults.has('bridge-managed-addr') ? fakePtr(4242) : rootPtr,
        reverse: { ownerBackref: rootPtr, gcHandle: fakePtr(5000) },
        fields: expected.managedBridge.fields,
      }
      : null,
    notes: faults.size ? [`synthetic faults injected: ${[...faults].join(', ')}`] : [],
  };
}

export default run;
