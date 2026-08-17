# godot-abi-grid

The publish gate for `Godot.External` (docs/analysis.md §8.9).

`Godot.External` has measured exactly **one** cell of the Godot compat matrix: 4.5.1, release
template, single precision, .NET binding — and on a *modified* engine (Slay the Spire 2 ships a
customised 4.5.1). Publishing on that basis would mean claiming "supports Godot 4.x" off a single
data point.

Godot ships official export templates for every version, so rather than waiting to find more real
games, this harness **manufactures the matrix**. It exports one authored ground-truth scene across
`version × template × precision × binding` and then makes the calibrator solve each build unaided.
The claim that survives is *"the calibrator solves layouts it has never seen"* — which is testable
— rather than *"we know every Godot layout"* — which is not.

```
project/          the authored ground truth (scene + probe scripts + expected.json)
gen-expected.mjs  regenerates project/expected.json from Main.tscn
build.ps1         exports the grid  -> out/<version>-<template>-<precision>-<binding>/
calibrate.mjs     launches each build, runs a calibration driver, judges the result
selftest.mjs      proves the JUDGE works, with no Godot installed
profiles.json     the §4.6 offset tables, used ONLY as a cross-check
lib/              grid axes, ground-truth loading, the check engine, report rendering
drivers/mock.mjs  synthetic driver for selftest — never a measurement
REPORT.md         generated coverage matrix (committed)
```

---

## Quick start

```powershell
node gen-expected.mjs          # only after editing project/Main.tscn
node selftest.mjs              # validate the harness itself (no Godot needed)
pwsh ./build.ps1 -ListOnly     # what can be built here, and what to install
pwsh ./build.ps1               # export every buildable cell
node calibrate.mjs --report    # judge every built cell, regenerate REPORT.md
node calibrate.mjs --list      # built vs missing, at a glance
```

Requirements: Node 18+, PowerShell 5.1+, the .NET SDK (for `binding=dotnet` cells), plus Godot
editors and export templates as below.

**Do the 4.5 release/single/dotnet cell first.** §8.9: that cell is already known good (§12.3
30/30, §12.3b 21/21, §12.4c 60/60), so it validates the harness before the harness is trusted to
judge anything else. A harness bug found on 4.3-debug reads as a calibrator failure on a version
nobody can easily check, and that is the most expensive mistake this tool can make. `build.ps1`
and `calibrate.mjs` both order the reference cell first for exactly this reason.

---

## Installing Godot editors and export templates

Nothing is vendored — the downloads are large and version-specific. `build.ps1` detects what is
present, skips what is not, and prints the exact install line for every gap.

### Editors

Put the unzipped editors anywhere under `tools/godot-abi-grid/bin/` (or set `GODOT_BIN_DIR`).
`build.ps1` scans recursively for the official archive naming:

```
bin/Godot_v4.5.1-stable_win64.exe               <- GDScript cells
bin/Godot_v4.5.1-stable_mono_win64/…_mono_win64.exe   <- .NET cells
```

Downloads: <https://godotengine.org/download/archive/> — one page per version, e.g.
`https://godotengine.org/download/archive/4.5.1-stable/`.

If your layout differs, pin paths explicitly in `godot-bin.json` (git-ignored):

```json
{
  "4.5.1": {
    "gdscript": "D:/godot/Godot_v4.5.1-stable_win64.exe",
    "dotnet":   "D:/godot/mono/Godot_v4.5.1-stable_mono_win64.exe"
  }
}
```

### Export templates

Templates are a separate download from the editor, and the .NET ones are separate again:

| Cell binding | File | Installs to |
| --- | --- | --- |
| `gdscript` | `Godot_v<ver>-stable_export_templates.tpz` | `%APPDATA%\Godot\export_templates\<ver>.stable\` |
| `dotnet` | `Godot_v<ver>-stable_mono_export_templates.tpz` | `%APPDATA%\Godot\export_templates\<ver>.stable.mono\` |

Easiest route: open the matching editor, **Editor → Manage Export Templates → Download and
Install**. Or unzip the `.tpz` (it is a zip) and copy its `templates/` contents into the directory
above. `build.ps1` needs `windows_release_x86_64.exe` and/or `windows_debug_x86_64.exe` to be
present there.

### Double precision — no official templates exist

This is a real gap in the plan, not an oversight in the script: Godot publishes **single-precision
templates only**. `precision=double` requires building the engine yourself:

```
scons platform=windows target=template_release precision=double
scons platform=windows target=template_debug   precision=double
# add module_mono_enabled=yes for the .NET cells
```

Then lay the results out as `<dir>/<version>/windows_release_x86_64[.mono].exe` and pass
`-DoubleTemplateDir <dir>` (or set `GODOT_DOUBLE_TEMPLATES`). `build.ps1` wires them in through
the export preset's `custom_template/*` fields.

Those eight cells are worth the effort eventually: `real_t` width changes **every** float offset,
so double precision is the axis most likely to break a calibrator that has quietly hardcoded
4-byte strides. Until someone builds those templates they stay `not built` in REPORT.md.

---

## Running the grid

```
node calibrate.mjs [options]

  --out <dir>            built cells                      (default ./out)
  --results <dir>        per-cell JSON output             (default ./results)
  --driver <spec>        auto | mock | <path.mjs> | <exe> (default auto)
  --only <substring>     run a subset, e.g. --only 4.5.1-release
  --headless             pass --headless to the target
  --timeout <ms>         wait for a target to become ready (default 60000)
  --driver-timeout <ms>  allowance for the calibration driver (default 180000)
  --report               regenerate REPORT.md afterwards
  --list                 print build status and exit
  --no-launch            do not launch targets (driver finds its own process)
  --mock-faults <list>   fault injection for --driver mock
```

Per cell, `calibrate.mjs`:

1. launches `out/<cell>/grid.exe` with `GRID_READY_FILE` pointing at a scratch path;
2. waits for `Probe.cs` / `Probe.gd` to write that file — it carries the pid plus the engine's own
   view of its version, template variant and precision, so a mislabelled cell directory is caught
   rather than silently poisoning the matrix;
3. hands the driver the pid and the authored ground-truth **values**;
4. judges the reply against `project/expected.json`;
5. kills the target and writes `results/<cell>.json`.

Exit code is non-zero if any cell failed or errored.

---

## Driver contract — `godot-abi-grid/driver.v1`

The harness contains the judge, not the calibrator. The thing under test is invoked over a
deliberately dumb boundary: **one JSON request on stdin, one JSON result on stdout, exit 0**.

`calibrate.mjs --driver auto` looks for, in order:

1. `GRID_CALIBRATOR` — path to any executable implementing this contract;
2. `src/Godot.External.Calibrator/Godot.External.Calibrator.csproj` — run via `dotnet run`;
3. otherwise every built cell is reported `driver unavailable`, which is **not** a pass.

### Request

```jsonc
{
  "contract": "godot-abi-grid/driver.v1",
  "pid": 12345,
  "executable": "…/out/4.5.1-release-single-dotnet/grid.exe",
  "cell":    { "name": "4.5.1-release-single-dotnet", "version": "4.5.1",
               "template": "release", "precision": "single", "binding": "dotnet" },
  "runtime": { /* the target's own ready-file report */ },
  "anchors": {
    "walkRootPath": "/root/RootHarness",
    "sizes": [ { "size": [887, 313], "path": "RootHarness/AlphaPanel/BetaBranch" }, … ],
    "visible": { "visiblePath": "…/VisibleTwin", "hiddenPath": "…/HiddenTwin" },
    "nodeCount": 20,
    "names": ["RootHarness", "AlphaPanel", …],
    "managedStatic": { "type": "Probe", "field": "Instance" }
  },
  "require": { "structuralMethod": "pointer-identity",
               "semanticMethod": "known-value-intersection" }
}
```

`anchors` are **values**, never offsets. That is the §12.5 technique — "two or three objects with
independently-known values, intersected". Offsets are never sent, and a driver that reports
`usedProfile: true` fails `calibration.unaided` no matter what else it scores.

### Result

```jsonc
{
  "driver": "godot-external", "driverVersion": "0.1.0",
  "usedProfile": false,
  "engineVersion": "4.5.1.stable",
  "walkCount": 20,
  "derivation": {
    "structural": { "method": "pointer-identity",
                    "offsets": { "node.parent": "0x128", "node.childListHead": "0x148",
                                 "node.scriptInstance": "0x68" },
                    "evidence": { "childListHead": "only slot p where *(p+0x18) is a known child" } },
    "semantic":   { "method": "known-value-intersection",
                    "offsets": { "control.size": "0x4c0", "control.position": "0x4b8",
                                 "control.scale": "0x4a8", "control.offset": "0x470",
                                 "canvasItem.visible": "0x370" },
                    "samples": 6,
                    "candidates": { "control.size": ["0x4c0"] } },
    "strings":    { "method": "cowdata-bulk-utf32",
                    "offsets": { "node.name": "0x1c0", "label.text": "0x800",
                                 "richTextLabel.text": "0xa78" } },
    "walk":       { "offsets": { "childList.next": "0x0", "childList.node": "0x18",
                                 "scriptInstance.ownerBackref": "0x8",
                                 "scriptInstance.gcHandle": "0x20" } }
  },
  "nodes": [
    { "name": "AlphaPanel", "class": "Control",
      "nativePtr": "0x1a9204c5580", "parentPtr": "0x1a9204c5000",
      "childPtrs": ["0x…", "0x…"],
      "size": [613, 227], "position": [37, 53], "scale": [1.25, 0.75],
      "offset": [37, 53, 650, 280], "visible": true, "text": null }
  ],
  "managedBridge": {
    "staticRootType": "Probe", "staticRootField": "Instance",
    "nativePtr": "0x…",
    "reverse": { "ownerBackref": "0x…", "gcHandle": "0x…" },
    "fields": { "ProbeInt32": 613227, "ProbeUnicode": "héllo ✦ 日本語" }
  }
}
```

Notes:

- Offsets may be hex strings or integers. Pointers **must** be strings — a 64-bit pointer through a
  JS Number is a silent corruption, and the harness rejects unsafe integers rather than guessing.
- Vectors may be arrays, `{x,y}`, or array-likes `{0:…,1:…}` (§12.6).
- `path` on a node is optional and ignored: the harness reconstructs every path from `parentPtr`
  alone, so a wrong parent offset collapses the tree instead of being papered over.
- Anything on stderr is captured into `notes`. A non-zero exit is recorded as `error`, which is
  deliberately distinct from `fail` — a crashed driver is not a calibrator verdict.

---

## What is asserted, and why the scene looks like that

| Check | §8.9 | Notes |
| --- | --- | --- |
| `harness.runtime_axes` | — | the target's own `OS.has_feature` view must match the cell name |
| `calibration.unaided` | — | no shipped profile consumed |
| `structural.child_head` | (a) | derived by pointer identity; must reproduce the authored child order |
| `structural.parent` | (a) | must round-trip against the child lists for all 25 nodes |
| `semantic.size` | (b) | known-value intersection, ≥ 2 samples |
| `semantic.position` | (b) | |
| `semantic.scale` | (b) | non-default scales, so a zeroing accessor cannot pass by luck |
| `semantic.offset` | (b) | plus an explicit `Data.anchor[4]` vs `Data.offset[4]` trap |
| `semantic.visible` | (b) | the Hidden/Visible twins must read differently |
| `strings.names` | (c) | all 25 StringNames exact |
| `strings.text.*` | (c) | including non-ASCII and an astral codepoint |
| `structure.no_collapse` | — | the duplicated size must not merge two nodes |
| `structure.walk_count` | (e) | driver count, node records and the target's own count must agree |
| `profile.agreement` | (d) | loud failure on disagreement, never a fallback |
| `bridge.managed` | — | `.NET` cells: managed static → `NativePtr` → the native walk root |

### The two-instance rule

Everything below obeys one rule, and it is worth stating before the list because it is the rule the
scene kept breaking:

> **Every property the calibrator has to discriminate must be backed by at least two nodes that can
> disagree with each other.**

The calibrator publishes an offset only when decode-set ⊆ property-set with a non-empty decode-set.
Over a **one-element** property-set that test is arithmetically vacuous — any field of the right
shape on that one node satisfies it — so the calibrator withholds rather than publish on no
evidence. That withhold is correct. The consequence is that the field is never measured *on any
cell, in any series*, and a blank column looks exactly like a column nobody got round to.

`richTextLabel.text` sat at 0/24 for eight series that way, while `label.text` — two instances —
derived on 23 of 24 cell-runs. When that was fixed, three more one-element sets were found behind
it. All four are now doubled:

| property-set | was | now | what it measures |
| --- | --- | --- | --- |
| `RichTextLabel` | `ZetaRich` | + `OmegaRich` | `richTextLabel.text` |
| scripted node | `RootHarness` | + `OmegaMarker` | `node.scriptInstance`, and the whole managed bridge behind it |
| `visible = false` | `HiddenTwin` | + `OmegaHidden` | `canvasItem.visible` |
| non-zero anchors | `AnchoredWide` | + `OmegaAnchored` | `Data.offset[4]`, separated from `Data.anchor[4]` |

**`gen-expected.mjs` now refuses to generate** a scene in which any class, or any of the six
discriminators it tracks, drops below two instances — the census is written into
`expected.json.fixture` so the count is visible rather than remembered. This is enforced on the
fixture, not on the calibrator: nothing about the withhold rule is relaxed, and a property that
cannot be measured should fail at generation time rather than quietly produce a green cell with a
hole in it.

Doubling alone is not enough: two identical instances are one measurement taken twice. Each pair
below differs *in kind* — different branch, different mode, different values — so the second
instance can contradict the first.

Scene design (`project/Main.tscn`) — every number is load-bearing:

- **613×227, 409×151, 887×313 and friends.** §12.5 probe 15 got *four* candidate offsets from a
  single `200×50` control because round numbers recur throughout memory. Odd, mutually distinct
  sizes make one scan near-unique and a two-control intersection certain. The viewport size
  (1920×1080) is deliberately **excluded** from the anchor list for the same reason.
- **One duplicated size** (409×151 on `AlphaLeaf` and `GammaNest`) so a calibrator that keys nodes
  by geometry gets caught collapsing them.
- **7 levels, sibling counts 2/3/4/3/2/3 down the Alpha branch** — uneven on purpose, to exercise
  the intrusive child-list walk and the parent pointer in both directions. Second instances are
  added to the *Omega* branch precisely so this chain stays as it is; `OmegaPanel` carries the fan
  (5 children) instead.
- **`ZetaLabelUnicode` = `héllo ✦ 日本語`, `ZetaRich` = `ρich ✦ テキスト 𝄞 RTL`.** §4.6 found scry
  truncates `char32_t` → byte, silently. `expected.json` records what such a decoder *would*
  produce, so the harness names that bug specifically instead of reporting a generic mismatch. The
  `𝄞` is U+1D11E: one `char32_t`, two UTF-16 units — it catches surrogate-pair handling too.
- **`VisibleTwin` / `HiddenTwin`** with different sizes, so each is individually identifiable while
  still bracketing the visible flag.
- **`OmegaHidden` is the second hidden node, and `OmegaShadow` under it is the trap.** One
  `visible = false` in twenty is a boolean discriminated by a single sample — and Godot 4's
  `CanvasItem` keeps **two** adjacent booleans, `visible` and `parent_visible_in_tree`, which on a
  scene where nothing inherits invisibility are true everywhere except one byte. `OmegaHidden` is
  hidden under a visible parent; `OmegaShadow` is itself **visible** and inherits invisibility from
  `OmegaHidden`. The two booleans therefore disagree at two nodes in *opposite* directions, and a
  reader that returns `is_visible_in_tree()` where `visible` was asked for now fails on
  `OmegaShadow`, where `expected.json` says `true` and the tree says `false`. Its rect is still
  resolved — layout is not gated on visibility — so it is an ordinary geometry sample as well.
- **`AnchoredWide`** has anchors of 0.5 and offsets `[-431, -197, 182, -46]`, resolving to position
  `(12.5, -40.5)`, size `613×151`. Upstream `Control::Data` puts `anchor[4]` immediately after
  `offset[4]`; on a scene where every anchor is zero, reading the wrong one looks correct. This
  node is the tie-breaker. It is also the shape that produced StS2's `BgContainer`
  `offset[-960,-516,1600,684]` in §4.6.
- **`OmegaAnchored` is the second one, and deliberately not a copy.** One anchored node can only say
  "these two regions differ"; it cannot say *which is which* without being taken on faith. This one
  is `0.125/0.375/0.625/0.875` with fractional offsets `[-19.875, 3.125, -19.375, -16.375]`, against
  `AnchoredWide`'s uniform `0.5` and integer offsets, so a reader that takes `anchor[4]` for
  `offset[4]` now produces two *different* wrong answers that disagree with each other. Every value
  is exactly representable in binary32 — `0.125 × 289 = 36.125`, `0.875 × 161 = 140.875` against
  `OmegaPanel`'s `289×161` — resolving to position `(16.25, 63.5)` and size `145×61`, so single and
  double precision must agree to the bit and the rect cannot be reconstructed by rounding.
- **`OmegaMarker` is the second scripted node**, and a bare `Node` rather than a `Control`.
  `node.scriptInstance` is found by locating the slot that is non-null exactly where a script is
  attached; with `RootHarness` alone that ranged over one node, and *every* managed-bridge reading
  (`+0x08` owner backref, `+0x20` GCHandle, §4.6) hangs off it. Being a non-`CanvasItem` it also
  doubles the sample behind every "this is not a Control" rejection — `DeltaSiblingOne` was likewise
  alone — and the two differ in kind: one is an unscripted leaf deep in the Alpha branch, the other
  is scripted and shallow in the Omega branch. `Marker.cs` / `Marker.gd` are deliberately empty; the
  script exists to be attached, not to do anything. (`build.ps1` already staged and rewrote these
  two filenames long before the files existed.)
- **`RichTextLabel`, and two of them.** §4.6 gives it a different text offset from `Label`
  (`0xa78` vs `0x800`), so one instance is needed to measure it at all — but one is not enough to
  *publish* it. The calibrator only publishes a text offset when decode-set ⊆ class-set with a
  non-empty decode-set, and on a class with a single instance that subset test is arithmetically
  vacuous: any string-shaped field on that one node satisfies it, so the calibrator withholds
  rather than publish on no evidence. That is the correct behaviour, and it is why
  `richTextLabel.text` was **never derived on any cell in eight grid series** while `label.text`
  — two instances — derived on 23 of 24 cell-runs. `OmegaRich` is the second instance, and it is
  deliberately in the *other* branch of the tree, so the second reading is independent of the
  first's neighbours in memory.
- **`OmegaRich` has `bbcode_enabled = true`, the opposite of `ZetaRich`.** §4.6: `RichTextLabel`
  stores the **raw BBCode source** in its single `String` member and keeps the *rendered* text in a
  separate item tree. With bbcode off the two are identical and a reading taken from the wrong one
  cannot be told apart; with it on, and a `[b]…[/b]` pair in the string, they differ by exactly the
  tags. `expected.json` therefore records what is **stored**, not what is displayed — and any check
  written against rendered text would be wrong, not merely unlucky.
- **`Probe.cs`** forces a .NET build and publishes `public static Probe Instance` — plain static
  fields, not auto-properties, so the bridge test is about the bridge and not about backing-field
  name mangling.

`expected.json` is generated from the scene and carries its SHA-256; `calibrate.mjs` refuses to run
if the two have drifted.

### Known gaps in the fixture, left open on purpose

Both of these are real and both were deliberately *not* taken, because each needs a decision from
whoever takes it rather than a quiet edit. They are written down so the next person decides rather
than rediscovers.

- **No node authors an empty string.** Godot's empty `String` is a **null `CowData` pointer**, which
  is a genuinely different decode path from a populated one, and nothing here exercises it — a
  decoder that dereferences it blind, or invents garbage for it, would score clean today. The
  problem is where it lands: `strings.text.absent` asserts that nodes with **no text member** report
  `null`, and `strings.text.wrong` asserts that authored text is **exact where present**. A `Label`
  with `text = ""` is neither shape — it has a text member, and its correct reading is `""`, not
  `null`. Adding it without first deciding which of the two checks owns it, and what a driver
  reporting `null` for it should score, risks corrupting the one property that took eight series to
  establish: *absent, never wrong*. Decide the semantics first, then add the node.
- **Every node name is ASCII.** `lib/expected.mjs` enforces distinct ASCII names, so `strings.names`
  proves only the ASCII path — and `StringName` does not have the same storage as `String`, so the
  UTF-32 decoding proved by `strings.text.*` does not transfer to it. A non-ASCII node name would be
  a cheap independent test of exactly that, but it contradicts an invariant the harness states
  explicitly, so it is a deliberate design change and not a gap to fill quietly.

---

## Honesty rules

These are enforced in code, not just documented:

1. **A check that cannot be evaluated is `skip`, never `pass`.** Skips are reported separately and
   never counted toward `n/n`.
2. **A cell that was not measured prints `not built` / `not run` / `driver unavailable`.** REPORT.md
   never leaves a blank and never infers a cell from its neighbours.
3. **Synthetic (mock-driver) results are excluded from the coverage matrix**, and labelled on every
   row if someone forces them in. They test the harness; publishing them as coverage would be
   fabrication.
4. **Profile disagreement is a failure, not a fallback.** The harness prints both readings — the
   calibrator may be wrong, or the shipped table may be — because §4.6's numbers came from a
   *modified* 4.5.1 engine and its debug column contains contradictions it admits to. Either way it
   must be resolved before the cell is quoted as evidence.

### Known limits of the grid itself

- **Stock templates are not a modified engine.** A green row says the calibrator solved a layout it
  had not seen. It does not say the shipped profile is right for anyone else's fork.
- **Compiler is a fifth axis** (§8.9, learned from Zolt-Dump's table: MSVC vs GCC). Official Windows
  templates are **MinGW-GCC**-built, not MSVC, so this grid measures one compiler — that one. Measured,
  not assumed: every cell's `type_info` blocks carry Itanium RTTI and share a single
  `__class_type_info` vtable, with Itanium-mangled names (`5Label`, `13RichTextLabel`), which an MSVC
  build would not emit. A template built with MSVC would need its own RTTI decoding and is untested.
- **Windows x86_64 only.** The export preset and template discovery are Windows-specific, matching
  `Godot.External`'s current reach.
- **Double precision is unmeasurable without custom templates** (above).
