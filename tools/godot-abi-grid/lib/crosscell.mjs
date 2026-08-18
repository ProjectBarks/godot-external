// Assertions that need MORE THAN ONE CELL, and therefore live above runChecks.
//
// Every check in lib/checks.mjs judges one cell against the authored scene. That is enough to catch
// a wrong reading and not enough to catch a wrong OFFSET: replacing every derived offset with 0x1
// while leaving the readings correct scored 19/19 on the 4.3 cells, because the only check that
// ever compared an offset to anything — profile.agreement — has no profile to compare against
// there. lib/checks.mjs's `offsets.internal_consistency` closes part of that from inside a single
// cell. This file closes the other part from outside it.
//
// Both assertions here are DERIVED, not transcribed. Neither needs an answer key, which is the
// whole point: §13.11's corollary is that copying the calibrator's own output into profiles.json
// would raise the score and destroy the information.
//
//   * BINDING INVARIANCE. `godot.windows.template_release.x86_64.exe` and its mono twin are built
//     from the same C++ with the same member layout; the .NET binding adds a scripting language, not
//     a field to Node. So the same version+template+precision must derive the same offsets under
//     both bindings. Two cells disagreeing is a hard contradiction that no single cell can see.
//
//   * DEBUG/RELEASE DELTA. §4.6/§12.7 measured `debug = release + 8`, every field, no exceptions,
//     independently on 4.5 and 4.3. That is pinned here as a constant rather than re-derived from
//     whatever the run produced — §13.11 #3 is the read-retry test that derived its expected value
//     from the code and then asserted the code matched it, and passed with the feature removed. A
//     pinned premise fails loudly when it stops being true; a self-derived one cannot fail at all.
//     Uniformity is checked as well, because a per-key delta that varies is a different defect from
//     a delta that has moved.

const DEBUG_RELEASE_DELTA = 8;

const passC = (id, title, detail, data) => ({ id, title, status: 'pass', detail, data });
const failC = (id, title, detail, data) => ({ id, title, status: 'fail', detail, data });
const skipC = (id, title, detail, data) => ({ id, title, status: 'skip', detail, data });

const hex = (v) => (v === null || v === undefined ? 'null' : `0x${Number(v).toString(16)}`);

/**
 * Flatten one record's derived numbers into `key -> offset`, offsets and walk links alike.
 *
 * This is a spread merge of two driver-supplied maps, and every other one of those in this tool had
 * the collision hole — a key in both groups silently resolving to the later one. It does not here,
 * and for a reason rather than by luck: the walk links are re-keyed under a `walk:` prefix that no
 * offset key can carry, so the two namespaces cannot intersect. `d.offsets` itself arrives already
 * merged and already collision-checked by lib/checks.mjs's mergeDerivationGroups (calibrate.mjs
 * withholds any key two derivation groups disagreed about), so there is nothing left to resolve
 * here. Both halves of that are load-bearing: drop the prefix, or hand this a raw spread of the
 * three derivation groups, and the hole is back.
 */
function derivedOf(record) {
  const d = record?.derived;
  if (!d) return null;
  const flat = { ...(d.offsets ?? {}) };
  for (const [k, v] of Object.entries(d.walk ?? {})) flat[`walk:${k}`] = v;
  return Object.fromEntries(Object.entries(flat).filter(([, v]) => Number.isInteger(v)));
}

/** Cells that actually produced a judged result. `not built`, `error` and `driver-unavailable` say nothing. */
function measured(records) {
  return (records ?? []).filter((r) => (r.status === 'pass' || r.status === 'fail') && derivedOf(r));
}

export function crossCellChecks(records) {
  const cells = measured(records);
  return [
    checkBindingInvariance(cells),
    checkDebugReleaseDelta(cells),
  ];
}

function checkBindingInvariance(cells) {
  const id = 'grid.binding_invariance';
  const title = 'the same version+template+precision derives the same offsets under both bindings';

  const groups = new Map();
  for (const c of cells) {
    const a = c.axes ?? {};
    const key = `${a.version}-${a.template}-${a.precision}`;
    if (!groups.has(key)) groups.set(key, []);
    groups.get(key).push(c);
  }

  const comparable = [...groups.entries()].filter(([, g]) => new Set(g.map((c) => c.axes.binding)).size > 1);
  if (!comparable.length) {
    return skipC(id, title,
      'no version+template+precision was measured under more than one binding in this run, so nothing '
      + 'here can be cross-checked. This is a gap in the RUN, not a property of the grid.');
  }

  const problems = [];
  const notComparable = [];
  let comparedKeys = 0;
  const groupSummaries = [];
  for (const [key, group] of comparable) {
    const byKey = new Map();
    for (const c of group) {
      for (const [k, v] of Object.entries(derivedOf(c))) {
        if (!byKey.has(k)) byKey.set(k, []);
        byKey.get(k).push({ cell: c.cell, value: v, owner: c.derived?.scriptInstanceClass ?? null });
      }
    }
    let inGroup = 0;
    for (const [k, seen] of byKey) {
      if (seen.length < 2) continue; // one binding derived it; nothing to disagree with

      // NOT every derived offset is a member of the engine object. `scriptInstance.*` belongs to
      // CSharpInstance or GDScriptInstance — unrelated classes implementing one interface, with the
      // owner pointer measured at 0x8 and 0x10 respectively (§13.10). Pointed at the live results,
      // the first version of this check "found" that difference and called it a contradiction: the
      // premise "a binding cannot change the layout" is true of Node and false of the thing hanging
      // off it. So these keys are compared only between cells whose ScriptInstance is the same C++
      // class, and where the driver did not say, they are NOT COMPARED and say so.
      if (isScriptInstanceMember(k)) {
        const owners = new Set(seen.map((s) => s.owner));
        if (owners.has(null)) {
          notComparable.push(`${key} ${k}: at least one cell did not report derivation.walk.scriptInstanceClass, so it is unknown whether these cells share an implementing class`);
          continue;
        }
        if (owners.size > 1) {
          notComparable.push(`${key} ${k}: implementing classes differ (${[...owners].join(' vs ')}), and this field is a member of THAT class, not of the engine object`);
          continue;
        }
      }

      inGroup++;
      comparedKeys++;
      const distinct = new Set(seen.map((s) => s.value));
      if (distinct.size > 1) {
        problems.push(`${key} ${k}: ${seen.map((s) => `${s.cell}=${hex(s.value)}`).join(', ')}`);
      }
    }
    groupSummaries.push(`${key} (${group.map((c) => c.axes.binding).join('/')}, ${inGroup} shared key(s))`);
  }

  const caveat = notComparable.length ? `\n    not compared: ${notComparable.join('; ')}` : '';

  if (problems.length) {
    return failC(id, title,
      `${problems.length} offset(s) differ between bindings of one build:\n    - ${problems.join('\n    - ')}\n`
      + '    A binding is a scripting layer, not a member of Node. The same engine version, template\n'
      + '    and precision is the same C++ layout under both, so exactly one of these two cells is\n'
      + '    wrong — and no single-cell check can tell which, because each is self-consistent.' + caveat,
      { problems: problems.length });
  }
  if (!comparedKeys) {
    return failC(id, title,
      `${comparable.length} group(s) hold more than one binding, but no key was actually comparable between `
      + `them, so nothing was cross-checked.${caveat}`, { comparedKeys: 0 });
  }
  return passC(id, title,
    `${comparedKeys} offset(s) agree across bindings in ${comparable.length} group(s): ${groupSummaries.join('; ')}. `
    + 'This is corroboration by an independent RUN, not by an answer key — but two cells derived by the '
    + `same calibrator can also be wrong the same way, so it does not stand in for the shipped profile.${caveat}`,
    { comparedKeys });
}

/**
 * Is this key a member of the ScriptInstance implementation rather than of the engine object?
 *
 * The distinction is the one profiles.json already encodes by scoping `scriptInstance.ownerBackref`
 * per implementing class: `node.scriptInstance` is a Node member (the pointer), while
 * `scriptInstance.*` are members of whatever CSharpInstance/GDScriptInstance object it points AT.
 */
function isScriptInstanceMember(key) {
  return key.replace(/^walk:/, '').startsWith('scriptInstance.');
}

function checkDebugReleaseDelta(cells) {
  const id = 'grid.debug_release_delta';
  const title = `debug offsets are release + 0x${DEBUG_RELEASE_DELTA.toString(16)}, uniformly, within a version family`;

  const groups = new Map();
  for (const c of cells) {
    const a = c.axes ?? {};
    const key = `${a.version}-${a.precision}-${a.binding}`;
    if (!groups.has(key)) groups.set(key, new Map());
    groups.get(key).set(a.template, c);
  }

  const pairs = [...groups.entries()]
    .filter(([, byTemplate]) => byTemplate.has('release') && byTemplate.has('debug'))
    .map(([key, byTemplate]) => ({ key, release: byTemplate.get('release'), debug: byTemplate.get('debug') }));

  if (!pairs.length) {
    return skipC(id, title,
      'no version+precision+binding was measured as BOTH a release and a debug template in this run, '
      + 'so the relation has nothing to be checked against here.');
  }

  const problems = [];
  let comparedKeys = 0;
  const summaries = [];
  for (const pair of pairs) {
    const rel = derivedOf(pair.release);
    const dbg = derivedOf(pair.debug);
    const shared = Object.keys(rel).filter((k) => k in dbg);
    // Link offsets inside List<Node*>::Element and ScriptInstance are not members of the engine
    // object whose layout the template flag changes, and §4.6 measures them identical on both.
    const subject = shared.filter((k) => !k.startsWith('walk:'));
    if (!subject.length) continue;
    const deltas = new Map();
    for (const k of subject) {
      const delta = dbg[k] - rel[k];
      if (!deltas.has(delta)) deltas.set(delta, []);
      deltas.get(delta).push(k);
      comparedKeys++;
    }
    summaries.push(`${pair.key}: ${subject.length} shared key(s), delta ${[...deltas.keys()].map(hex).join('/')}`);
    if (deltas.size > 1) {
      problems.push(
        `${pair.key}: the debug/release delta is not uniform — `
        + [...deltas].map(([d, keys]) => `${hex(d)} on ${keys.join(', ')}`).join('; '));
    }
    for (const [delta, keys] of deltas) {
      if (delta !== DEBUG_RELEASE_DELTA) {
        problems.push(
          `${pair.key}: delta ${hex(delta)} on ${keys.join(', ')}, but §4.6/§12.7 measured `
          + `${hex(DEBUG_RELEASE_DELTA)} on 4.5 and independently on 4.3. Either the calibrator is wrong here or `
          + 'that measurement no longer holds for this engine version — both are findings, neither is a '
          + 'reason to re-derive the constant from this run.');
      }
    }
  }

  if (problems.length) {
    return failC(id, title, `${problems.length} problem(s):\n    - ${problems.join('\n    - ')}`, { problems: problems.length });
  }
  if (!comparedKeys) {
    return skipC(id, title, 'release and debug cells were measured but share no derived offset key');
  }
  return passC(id, title,
    `${comparedKeys} key(s) across ${pairs.length} release/debug pair(s) all sit exactly ${hex(DEBUG_RELEASE_DELTA)} apart: `
    + `${summaries.join('; ')}`,
    { comparedKeys });
}
