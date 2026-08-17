// Shipped-profile lookup for assertion (d): "derived values agree with the §4.6
// profile WHERE ONE EXISTS — disagreement is a loud failure, never a silent
// fallback." (§8.9)
//
// There is deliberately no fallback path in this module. It answers exactly one
// question — is there a profile for this cell, and what does it claim — and the
// caller decides. Nothing here is ever handed to the driver.

import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { parseOffset } from './util.mjs';

// A profile value is normally a scalar offset. `scriptInstance.ownerBackref` is instead a map from
// the C++ class that OWNS the field to its offset, because CSharpInstance and GDScriptInstance are
// unrelated implementations of one interface and no single number is right for both. Normalise one
// level into such a map rather than through it: parseOffset({...}) yields null, which silently
// turned every per-class entry into "profile null" and made checks.mjs's resolution unreachable.
const normalizeEntry = (v) => (v !== null && typeof v === 'object' && !Array.isArray(v)
  ? Object.fromEntries(Object.entries(v).map(([owner, off]) => [owner, parseOffset(off)]))
  : parseOffset(v));

export function loadProfiles(harnessRoot) {
  const raw = JSON.parse(readFileSync(join(harnessRoot, 'profiles.json'), 'utf8'));
  return raw.profiles.map((p) => ({
    ...p,
    offsets: Object.fromEntries(Object.entries(p.offsets).map(([k, v]) => [k, normalizeEntry(v)])),
    walk: Object.fromEntries(Object.entries(p.walk ?? {}).map(([k, v]) => [k, normalizeEntry(v)])),
  }));
}

export function selectProfile(profiles, cell) {
  return profiles.find((p) => {
    const m = p.match;
    if (m.versionPrefix && cell.versionPrefix !== m.versionPrefix) return false;
    if (m.template && cell.template !== m.template) return false;
    if (m.precision && cell.precision !== m.precision) return false;
    if (m.binding && cell.binding !== m.binding) return false;
    return true;
  }) ?? null;
}
