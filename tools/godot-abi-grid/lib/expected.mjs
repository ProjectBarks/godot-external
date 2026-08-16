// Load the authored ground truth and refuse to run against a stale copy.

import { readFileSync } from 'node:fs';
import { createHash } from 'node:crypto';
import { join } from 'node:path';

export function loadExpected(harnessRoot) {
  const expectedPath = join(harnessRoot, 'project', 'expected.json');
  const scenePath = join(harnessRoot, 'project', 'Main.tscn');
  const expected = JSON.parse(readFileSync(expectedPath, 'utf8'));

  const actualSha = createHash('sha256').update(readFileSync(scenePath)).digest('hex');
  if (actualSha !== expected.sourceSha256) {
    throw new Error(
      `expected.json is STALE: Main.tscn hashes ${actualSha.slice(0, 12)}… but expected.json `
      + `records ${String(expected.sourceSha256).slice(0, 12)}….\n`
      + '  Ground truth that has drifted from the scene makes every downstream result a lie.\n'
      + '  Fix: node gen-expected.mjs',
    );
  }

  const byPath = new Map(expected.nodes.map((n) => [n.path, n]));
  const byName = new Map();
  for (const n of expected.nodes) {
    if (byName.has(n.name)) {
      throw new Error(`Main.tscn has duplicate node name "${n.name}" — §8.9 requires distinct ASCII names`);
    }
    byName.set(n.name, n);
  }

  return { ...expected, path: expectedPath, byPath, byName };
}
