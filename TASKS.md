# Remaining work

Tracks both `godot-external` and `LiveClr`. Updated as tasks land.

**Rules for this file**

1. A task is `DONE` only when its **verifier** has run and passed — never on a self-report. Four
   self-reports in one session claimed success and were disproven by measurement.
2. Every task states **how it can fail**. A task with no failure mode is not specified yet.
3. `BLOCKED-ON-YOU` items are decisions, not work. Do not guess them.
4. When a task changes a measured number, record the before and after.

---

## Status

| | |
| --- | --- |
| `godot-external` | `feature/calibrator-rtti-class-identity`, pushed |
| `LiveClr` | `docs/analysis-sync`, pushed, **365 tests**, statics implemented and verified live |
| Grid | 22 checks + 2 cross-cell, **12 cells** across 4.3 / 4.4.1 / 4.5. `bridge.managed` now 30/30 |
| Absent-never-wrong | Holds — 648 records, 94/94 byte-exact, 0 invented |

### Task board

| | Task | State |
| --- | --- | --- |
| **T1** | **Statics in LiveClr** | **DONE** |
| **T2** | **`bridge.managed` reads six values** | **DONE** |
| **T3** | **Withhold paths declare themselves** | **DONE** |
| **T4** | **Godot 4.4 support** | **DONE** |
| **T8** | **LiveClr `IsString` dead on live targets** *(new, found by T2)* | **DONE** |
| T9 | LiveClr `IsList` — same defect, proved not fixed *(new, found by T8)* | OPEN |
| **T5a** | **Reflection defects + `HashMap` contradiction** | **DONE** |
| **T5b** | **Wire ClassDB cross-check** | **DONE** |
| **T6** | **Godot 4.6 support** | **DONE** |
| **T7** | **Derivation-group merge collision** | **DONE** |

### T1 — DONE, verified independently

Verifier run by the orchestrator: reverting the back-pointer anchor (`StaticsCalibration.cs:159`)
fails **exactly** `RefusesATypeWhoseBackPointerAnchorDoesNotClose`; restored, **359/359 pass**.

| | before | after |
| --- | --- | --- |
| LiveClr tests | 332 | **359** |
| Static field addresses | not implemented (delegated to ClrMD) | **implemented, derived, verified live** |

Live against PID 418440: `Environment.s_processId` → **418440**, the target's real PID. Control —
821 reads through the wrong base → **0** accidentally-valid objects. Anchor **3033/3033** gate-set,
**0/9250** gate-clear. All 14 hostile inputs refused while the real `System.String` MT is still
accepted. Of 28,003 statics, 23,985 resolved with **0 garbage** across 18,542 GC reads.

**Nothing on the path is hardcoded.** The auxiliary slot and the `MTFlags2` gate bit are derived
*jointly* — the one (slot, bit) pair where "the anchor closes" and "the bit is set" agree on every
sampled type. Neither is derivable alone: a slot sweep without a gate finds the anchor on an
unexplained subset, and a bit search without a slot has nothing to correlate against. Which
`DynamicStaticsInfo` member is the GC base is **measured**, not read off the struct — both orderings
are scored and the winner must have zero garbage while the loser resolves zero objects, so the
control this task demanded is computed at attach.

Found and fixed in passing: **boxed value-type statics.** `DateTime.MinValue` reported ticks of
`1798517794032` — a heap address. The slot holds a reference; it is now followed after validation.

**§14 Correction 3 was over-attributed** and is fixed in the doc: only **83 of 3,747** storage-less
statics are generic. The rest are ordinary types whose class initialiser never ran — a transient
state that resolves the moment the target touches the class, not a permanent capability limit. They
now have their own status.

Honest gap the agent flagged: the two thread-static guards **shadow each other** — removing either
alone leaves the test green, removing both turns it red. By design (metadata is the authority, the
derived bit a second opinion), but neither is individually falsifiable.

### T2 / T3 — DONE, verified on the live grid

| | before | after |
| --- | --- | --- |
| `bridge.managed` | **0/12** .NET cell-runs (fields `{}` in all 224 historical results) | **30/30** across 5 grid runs |
| Reference cell | 21/22 | **22/22** |
| `profile.agreement` undeclared-withhold failures | 2 | **0** across 5 runs |
| Calibrator tests | 338 | **349** |

**T2's root cause was two defects.** `ClrManagedProbe.TryDescribe` only ever called `AsString()`, so
the four numeric/bool fields were never attempted — fixing that got 4 of 6. The remaining two
exposed a **LiveClr production defect**, now tracked as T8.

**T3's task named two undeclared withhold paths; there were seven.** All now route through
`Decline()`, which is **first-wins** — a later, vaguer observation must not overwrite the operative
cause, which is exactly the "declares a reason that is not the real one" failure the task warned
about. `Build` withdraws a declaration for any key a later pass settles.

**10 reversions**, each reddening its own test alone, including two driven through the live grid.

The agent **refused to add a tie-breaker**, correctly: §13.11 records that a discriminator added here
has been wrong three times. It noted the `canvasItem.visible` obstacle names its own remedy —
*"another sample with a different expected value is needed"* — which is a question about the
**harness's anchor set**, not a calibrator discriminator. That is the only proposal worth pursuing.

**Another vacuous test found:** the pre-existing `AnEvenSplitOnOneNodeAlsoWithholds` passes *without
reaching the branch it names* — with one `RichTextLabel` in the scene, `OfClass` refuses before
candidates are ever weighed. And the agent's own first `Build`-reconciliation test passed with the
fix reverted, which is what led it to the `node.scriptInstance` deferral gap.

### T4 — DONE, verified

**The code change §15.6 predicted does not exist to be written.** The calibrator carries no
version-keyed offset table, and `build.ps1`/`lib/grid.mjs` already knew about 4.4. The calibration
run *was* the task. **4.4 works because nothing aliases.**

| | before | after |
| --- | --- | --- |
| built cells | 8 | **12** |
| versions measured | 4.3, 4.5 | **4.3, 4.4.1, 4.5** |
| cross-cell groups | 4 | **6** |

**§15.1's shift figure was refuted: the inherited band moves `+0x10`, not `+8`.** A second 8 enters
above `Node::data.parent`, consistent with §15.1's own aside about `Node::Data` gaining bitfields —
an observation that never reached its number. **Patching 4.3's table with "+8" would have produced a
table wrong on every `Node` field and plausible enough to ship.** Only the wrong half was actionable:
a derived calibrator is unharmed, a hand-patched table is silently broken. Full table in §15.7.

Confirmed to the byte: `visible` moves with the `Node` band, and the `Control` block moves **exactly
96** beyond the inherited shift. No 4.4.1 column was written into `profiles.json` — that would be the
pass-by-construction §13.11 forbids.

### T8 — DONE, verified independently

Verifier run by the orchestrator: reverting the fixture's string norm type to the old
`ELEMENT_TYPE_STRING` fails **exactly** `TheFixtureWritesTheNormTypeARealRuntimeWrites`; restored,
**365/365 pass** (was 359).

`AsString()` returned null for **every string on every real target**. Across 12,283 live
MethodTables, `EEClass.InternalCorElementType == ELEMENT_TYPE_STRING` appears **zero times** —
`System.String` reports `ELEMENT_TYPE_CLASS`.

**The category-mask theory I supplied was also wrong.** String's category bits are `0x00000000`,
identical to an ordinary class, because CoreCLR's own `MethodTable::IsString` is a **shape** test,
not a category test. No measured constant would have worked. (The `0x000F0000` field *does* carry
identity for other kinds — valuetype, primitive, interface, szarray — confirmed over ~1,400 types.)

The fix is **descriptor-published**: the `StringMethodTable` global, validated to round-trip as a
method table, compared by pointer. Where a runtime omits that global, the fallback demands **two
independent signals** — the ECMA-335 name *and* a component stride of 2 — each selecting exactly 1
of 12,283 live MTs, and the same one.

Live ground truth: `ReleaseInfoManager._instance` now reads `Commit "59260271"`,
`Version "v0.107.1"` — matching `release_info.json` on disk. Before the fix: three nulls. Controls
hold — `Environment.s_processId` is *not* decoded as a string, `String.Empty` reads `""` not null.

**Fixture honesty limit, recorded rather than papered over:** it now writes what .NET 9 writes for
String's norm type and `MTFlags`, but still does not synthesize the `MTFlags` **category** bits
(live `ObjectArray` is `0x810A0008`, fixture writes `0x80000008`). Nothing reads them today; a future
category-based predicate must measure before trusting the fixture there.

### T9 — OPEN. `IsList` has the same defect, proved and deliberately not fixed

`IsList` is a name-prefix match, and names come from inverting `TypeDefToMethodTableMap`, which holds
**typical instantiations only**. Live: 75 of 555 walked objects have no name at all, and **9 of 9
slots whose ECMA-335 signature declares `List<T>` returned null from `AsList()`**
(`Sentry.SentryOptions.ExceptionProcessors` among them).

The canonical MT (`List<__Canon>`) is not in the map either, and shares **no EEClass** with the
typical instantiation (0 of 75 matched) — so there is no cheap descriptor route back to the
definition. The agent declined to invent one, which is right. The fixture hid it the same way
`IsString` was hidden: `DefineInstantiatedType` pointed at the *mapped* typical MT, a shape no
runtime produces. It can now produce the live shape, and
`GenericInstantiationsAreNotNamedOnALiveShapedTarget` pins the refusal as a **documented limitation
with a falsifiable test**.

`AsObject`, `AsArray` and `IsArray` were checked on the same run and are **not** affected — arrays do
report `SZARRAY` live, 555 objects typed cleanly, and `System.String[]` elements decode.

### T5b — DONE, verified independently. T1–T7 are now complete.

Verifier run by the orchestrator: disabling the name gate in `OffsetCrossCheck` fails **exactly the
four tests written for it** and nothing else; restored, **392/392**. `npm test` **60/60 scenarios,
27/27 armed**.

| | before | after |
| --- | --- | --- |
| selftest scenarios | 56 | **60** |
| armed fixes | 23 | **27** |
| C# tests | 366 | **392** |
| independent corroboration | none wired | **live, every cell, every run** |

Agreement per cell — **zero disagreements anywhere**, across all 16 cells:

| cell | agreed | |
| --- | --- | --- |
| 4.3-release | **5/8** | 4.3-debug 1/8 |
| 4.4.1-release | **7/8** | 4.4.1-debug 3/8 |
| 4.5-release | **7/8** | 4.5-debug 3/8 |
| 4.6.3-release | **7/8** | 4.6.3-debug 3/8 |

§13.2's *corrected* RVAs come back byte-for-byte on 4.5-release. 4.4.1 and 4.6.3 are new, and the
4.6 pair confirms §17's `+0x7d8` / `+0x348` at the default window.

**No offsets were written to `profiles.json` or `GodotAbiProfiles.cs`.** The corroboration lives in
the comparison. All four 4.3 cells still **SKIP** `profile.agreement`.

`OffsetAgreement` gains **`NotCompared`**, returned *before looking at either value* when there is no
name — so silence cannot be read as corroboration. Proved live in both directions: a deliberately
perturbed offset produced `disagree` with **no `offset` key at all** while `derivation` still
published the true `0x370` (which is also the one-way-input proof), and probing a misspelled getter
plus an *inherited* `Control::get_text` both produced `notCompared`.

**Three findings.**

*The 869-vs-870 class count was never a transcription slip — it is `--headless`.* Same binary, same
cell, same code: 4.5-release-gdscript walks **908 windowed and 907 headless**, reproducibly. §16.1's
guess was wrong about the cause.

*The version parse was a real trap.* Targets report `4.5-stable (official)`, not `4.5.stable`. A
strict split yielded `(0,0)` and the route then reported *"Godot 0.x is unsupported"* — which reads
exactly like the version gate doing its job rather than a parse failure. Pinned by a test.

*The rule paid for itself.* On the run where `4.4.1-debug-dotnet` hit the known `node.parent` tie
(candidates `0x140`, `0x510`) and withheld, the getter route independently decoded **`+0x140`** — one
of the two survivors. It published `noOpinion` rather than breaking the tie, and the agent **refused
to wire that back into derivation**: §13.11 records a discriminator added there being wrong three
times.

**Cost: ~1.0 s per cell** (0.92–1.11 s across all 16) against a 180 s driver budget — cheaper than
the root-location scan the calibrator already runs. It stays in every calibration.

### T5a — DONE, verified independently

Verifier run by the orchestrator: reverting `DefaultProbeSlots` 12 → 8 fails **exactly the four
named tests** (`TheProbeFindsTheMethodPointer…(slot: 9)` and `(slot: 11)`,
`TheDefaultWindowReachesTheDebugMethodBind…`, `TwoCodePointersInTheProbeWindowRefuse`); restoring it
returns 22/22.

| | before | after |
| --- | --- | --- |
| `MethodBindProbe` live resolution | **0/16 probes** — all refused | **16/16**, one `.text` hit each, never two |
| Release cells | — | slot 9, `sizeof(MethodBind) = 0x48` |
| Debug cells | — | slot 11, `sizeof(MethodBind) = 0x58` |

Decoded offsets match the known-correct answers, and the RVAs match §13.2's *corrected* table:
4.5-release `Label::get_text` +0x7f8 (RVA `0x15d11b0`), `is_visible` +0x370 (RVA `0x139f520`);
4.3-release +0x8f0 and +0x418; 4.5-debug `get_text` +0x800, with debug `is_visible` abstaining per
§16.2's structural limit.

**The `HashMap` contradiction: §15.3 (source) was right, §16.5 (measurement) was wrong — and the
code needed no value change.** `HashMapSize = 48` was **correct as shipped; it was never a defect.**
§16.5's basis was *"`num_elements` at head+0x14, head at map+16"* — the first half is
version-invariant, the second assumed the 4.5 layout onto 4.3, so `0x10 + 0x14 + 4 = 40` falls out
**by construction on any version**. Arithmetic, not measurement: another §13.11 result that could not
come out otherwise.

Measured live by walking every registered `ClassInfo` and accepting a head only where
`head_element`, `tail_element` and `num_elements` all agree with a fully traversed chain — it
reproduces `ClassInfo` member-for-member, not just an average stride:

| | 4.3 (869 classes) | 4.5 (908 classes) |
| --- | --- | --- |
| `head_element` within `HashMap` | **+0x18** | **+0x10** |
| `sizeof(HashMap)` | **48** | **40** |

The real defect was **naming**: §16.5's "`method_map` at +0x30 / +0x38" are that map's
`head_element` offsets; `method_map` itself is at `ClassInfo+0x20` on **both**. Fixed by adding
`ClassDbLayout.HashMapHeadElement` beside `HashMapSize`, with a test asserting they are not the same
number.

**The `cname` item — the brief's premise was half wrong too.** Which of `cname`/`name` is populated
is decided by the **interning route**, not the version (`StaticCString` sets `cname`; the
`String`/`const char*` ctors set `name`). Measured on 4.3, it cuts both ways *inside one walk*:
class-name keys populate `name`, method-name keys populate `cname`. So a `name`-only 4.3 reader
reads every class name correctly and every method name as `""`, resolving no bind at all. §13.7's
rule is right; §16.5's "backwards" was right only about the keys it happened to look at.

**Another check that could not fail, found in passing:** the old probe test placed the method pointer
at slot 4 or 5 — inside the broken 8-slot window and nowhere near either real layout — so it passed
green while the shipped default refused 100% of live probes. It now uses the measured slots and calls
with the *shipped default*, which is what arms the reversion above.

**Unreconciled, recorded rather than overwritten:** §16.1 says 870 classes on 4.3; T5a measures
**869** on all four 4.3 cells, from a chain that round-trips cleanly end to end (a truncated walk
would have failed, not come up one short). 4.5's 908 reproduces exactly. One of the two is a
transcription slip and there is no evidence saying which.

### T7 — DONE, verified independently

Verifier run by the orchestrator, not accepted from the subagent: `npm test` exit 0,
**56/56 selftest scenarios**, **23/23 fixes go red when reverted** including
`offset-group-collision #13`.

| | before | after |
| --- | --- | --- |
| selftest scenarios | 55 | **56** |
| armed fixes (`mutate-verify`) | 22 | **23** |

The audit reported the hole in `checkOffsetConsistency`. The worse instance was in
**`checkProfileAgreement`**: the coin-flip survivor of the merge was compared against the shipped
profile, so a self-contradicting driver had a **50/50 chance of a green "matches"** that would then
be quoted as corroboration. Both call sites plus `calibrate.mjs`'s `derived.offsets` record now go
through `mergeDerivationGroups`, which separates **conflicts** (different values — scored as a
failure) from **duplicates** (same value — disclosed, not scored).

`crosscell.mjs`'s fourth spread-merge was examined and found **not** to have the hole — walk links
are re-keyed under a `walk:` prefix no offset key can carry, and its input arrives already
collision-checked. Documented rather than given an unarmed check.

The mutation reverts **both** call sites at once; reverting one alone leaves the other reddening the
scenario and proves nothing. Confirmed by hand: with the fix reverted exactly one scenario goes red,
and pre-fix the fault is entirely invisible because the `strings` copy wins the spread and the
surviving value happens to be correct.

### Toolchain — T4/T6 unblocked

**4.4.1-stable** and **4.6.3-stable** installed, standard + mono, editors under
`tools/godot-abi-grid/bin/` (gitignored) and templates in `%APPDATA%\Godot\export_templates\`.
All 8 downloads verified against the official per-release `SHA512-SUMS.txt` (8/8), all 4 editors
confirmed running headlessly. Actual download was **~4.9 GB**, not the ~2 GB estimated — templates
are ~1.2 GB *each*.

Tag enumeration confirmed exactly two 4.4.x tags and four 4.6.x, so the newest of each was taken.
`build.ps1 -ListOnly` now reports **16 buildable cells, 0 errors** — the eight new ones are
`4.4.1-*` and `4.6.3-*`, since cell names use the **resolved** version, not `4.4-*`/`4.6-*`.
`VersionMatrix` already carried entries for both, so no source change was needed.

Still unbuildable, unchanged and out of scope: **4.2** (no editor or templates) and **every
`precision=double` cell at every version** — Godot publishes single-precision templates only, so
those need an engine built from source.

---

## Ready to delegate

### T1 — Implement statics in LiveClr
**Why now:** §14 is verified live (3033/3033 anchor, real PID read back). LiveClr's README says
"not implemented", no longer "impossible". This unblocks the sidecar and cutting `readerProcess.ts`.

Chain: `MTFlags2 & 0x0002` gate → derive `m_pAuxiliaryData` **once** by unanimity over ≥100 gate-set
types → `aux-0x18` / `aux-0x10`, masked `& ~1`.

**Must handle, all three found by measurement:**
- Slot 40 aliases the anchor for 26 types. **Derive once and freeze — never sweep per lookup.**
- `FieldDesc` bit 25 = thread-local static. Passes gate *and* anchor; aux bases do not apply.
  **Returns a confident wrong address if unchecked.**
- Open generic definitions have no statics storage (raw base reads `1`). Refuse.
- `0x0002` is not descriptor-published — **derive** it (the `MTFlags2` bit that perfectly predicts
  the anchor), do not hardcode.

**How it fails:** publishes an address for a thread static; sweeps per-type and picks slot 40; treats
"class not initialized" as "null".
**Verifier:** run against a live .NET 9 process; read `Environment.s_processId` and assert it equals
the target PID. Control: same fields through the *wrong* base must yield zero valid objects.

### T2 — `bridge.managed`: read the six managed values
Currently fails 12/12 .NET cell-runs. `managedBridge.fields` is `{}` in all 224 historical results —
the calibrator resolves the managed object and reads nothing off it. Expected:
`ProbeAscii`, `ProbeUnicode` (non-ASCII), `ProbeInt32`, `ProbeInt64`, `ProbeFloat`, `ProbeBool`.

**How it fails:** reads through a wrong offset and reports plausible values.
**Verifier:** the check is already proven sound both ways — correct values → 21/21, one wrong value →
fails naming `ProbeInt32: 613228 != 613227`. Grid run.

### T3 — Withhold paths must declare themselves
Two paths (`richTextLabel.text`, `canvasItem.visible`) call `_notes.Add` directly instead of routing
through `Decline()`, so `derivation.notDerived` goes unpopulated and `profile.agreement` correctly
reports an unexplained absence. **Converts 4 of 6 run-3 failures into clean, explained skips.**
The underlying candidate-tie flakiness is separate — do not paper over it.

**How it fails:** declares a reason that is not the real one; or suppresses the tie instead of
reporting it.
**Verifier:** `mutate-verify` scenario + grid run showing skip-with-reason rather than fail.

### T4 — Godot 4.4 support
**Cheapest real win available.** Every reader 4.4 needs already exists for 4.3 — the `cname`-bearing
`StringName::_Data`, the 48-byte `HashMap` with `head_element` at `+0x18`, the `HashMap` walker,
unchanged `MethodBind` and `ObjectSlot`. RTTI already covers class identity.

Work: a version-gate entry mapping 4.4/4.4.1 onto the **4.3-family parsers**, plus grid cells and a
calibration run. **Matrix widens to 4.2–4.5.**

**The one way to get this wrong:** treating 4.4 as an alias for 4.3's *offsets*. It is not —
`Object` gains `_translation_domain` (+8) and `CanvasItem` gains two `HashMap`s, shifting the whole
`Control` block ~96 bytes. Fields survive; addresses move.
**Verifier:** new grid cells; `offsets.internal_consistency` and `grid.debug_release_delta` must pass.

### T5 — Wire the ClassDB cross-check as live corroboration
Q3 proved the seed works: unique (1 of 17 / 1 of 10), 908 classes enumerated, **zero disagreements**.
4.5-release corroborates 7/8 fields, 4.3-release 5/8.

**Do NOT write these into `profiles.json`** — that recreates pass-by-construction. The corroboration
lives in the comparison, computed in the same run.

Two shipped defects to fix first:
- `MethodBindProbe.DefaultProbeSlots = 8` **refuses 100% of probes on all 8 cells**.
  `sizeof(MethodBind)` is `0x48`/`0x58`, so the pointer is at slot 9 or 11. Needs ≥ 12.
- `ClassDbLayout.Godot43.HashMapSize = 48` measures **40**. Note this contradicts the source-derived
  48→40 boundary in §15.3 — **that discrepancy is unresolved, resolve it rather than picking a side.**

**How it fails:** agrees without a name attached (§13.2's `Label::get_text` row was misattributed
exactly this way).
**Verifier:** disagreement must publish neither side; a getter with no bind must report "not compared".

### T6 — Godot 4.6 support
One `AHashMap` walker (24-byte header + dense scan) — *simpler* than the linked-list walk it
replaces. Everything else carries over from 4.5. Optionally swap class identity to `_gdtype_ptr`,
which gives the name **and** the full inheritance chain in one pointer chase, better than RTTI.

### T7 — Close the derivation-group merge hole
`checkOffsetConsistency` merges groups with spread syntax, so a driver reporting the same key in two
groups has the later silently win, with no collision complaint. Latent; not exploitable by the
current calibrator. Assert on collision.

---

## BLOCKED-ON-YOU — decisions, not work

| | |
| --- | --- |
| **`Godot.External` namespace** | Collides with GodotSharp's `Godot` root. Rename gets more expensive the longer it waits. |
| **`Iced` dependency** | First NuGet dep in the library, quarantined to `Reflection/`. Keep, or lift that folder into its own project before packaging? |
| **Publish targets** | Neither repo is on NuGet. README status blocks say "not ready" — still accurate. |
| **`spectra-overlay`** | On `main` with unrelated Run Journal work modified. Docs left uncommitted rather than entangled. Want a docs-only branch? |
| **`readerProcess.ts` cutover** | Touches the actual app. Gated on T1. |

---

## Known limits — recorded, not bugs

- **4.3 offsets are uncorroborated by any independent table**, by choice. A profile column copied
  from the calibrator would pass by construction forever. The report renders those cells as
  uncorroborated rather than clean. T5 is the honest fix.
- **4.3-debug stays capped** at 1/8 cross-checked. Debug codegen spills `this` to the stack, so the
  getter decoder correctly abstains. Fixable with stack-slot tracking; not implemented.
- **Class identity is unauthenticated.** A forged vtable yields a forged class name, and text
  publication is gated on it. Fine for a cooperating unmodified game; not a security boundary.
- **Windows x64 only.** Linux templates use System V (RDI) and Itanium pointer-to-member — a second
  decoder.
- `tools/godot-abi-grid/undefined/verify-proj/` — 25K of debris from a typo'd path. Untracked.

---

## The verification rule

Every expensive mistake in this project was **a check with no way to come out other than the way it
came out** — six in the harness, eleven in LiveClr, one in the verification method itself, and one
in a fix *for* the pattern. Writing the lesson beside the code did not stop it recurring three lines
later.

`mutate-verify.mjs` is what worked: revert a fix, require **the scenario written for that fix** to
break, not merely that something breaks. It caught two fixes that were not armed. It runs in
`npm test`.

**Ask of every green check: what input would make this red, and has anything like it ever run?**
