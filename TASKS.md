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
| `LiveClr` | `docs/analysis-sync`, pushed, 332 tests |
| Grid | 22 checks + 2 cross-cell. Runs 1–2 byte-identical. `bridge.managed` fails 12/12 (real gap) |
| Absent-never-wrong | Holds — 648 records, 94/94 byte-exact, 0 invented |

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
