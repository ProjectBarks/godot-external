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
| `structural.parent` | (a) | must round-trip against the child lists for all 20 nodes |
| `semantic.size` | (b) | known-value intersection, ≥ 2 samples |
| `semantic.position` | (b) | |
| `semantic.scale` | (b) | non-default scales, so a zeroing accessor cannot pass by luck |
| `semantic.offset` | (b) | plus an explicit `Data.anchor[4]` vs `Data.offset[4]` trap |
| `semantic.visible` | (b) | the Hidden/Visible twins must read differently |
| `strings.names` | (c) | all 20 StringNames exact |
| `strings.text.*` | (c) | including non-ASCII and an astral codepoint |
| `structure.no_collapse` | — | the duplicated size must not merge two nodes |
| `structure.walk_count` | (e) | driver count, node records and the target's own count must agree |
| `profile.agreement` | (d) | loud failure on disagreement, never a fallback |
| `bridge.managed` | — | `.NET` cells: managed static → `NativePtr` → the native walk root |

Scene design (`project/Main.tscn`) — every number is load-bearing:

- **613×227, 409×151, 887×313 and friends.** §12.5 probe 15 got *four* candidate offsets from a
  single `200×50` control because round numbers recur throughout memory. Odd, mutually distinct
  sizes make one scan near-unique and a two-control intersection certain. The viewport size
  (1920×1080) is deliberately **excluded** from the anchor list for the same reason.
- **One duplicated size** (409×151 on `AlphaLeaf` and `GammaNest`) so a calibrator that keys nodes
  by geometry gets caught collapsing them.
- **7 levels, sibling counts 2/3/4/3/2/3** — uneven on purpose, to exercise the intrusive
  child-list walk and the parent pointer in both directions.
- **`ZetaLabelUnicode` = `héllo ✦ 日本語`, `ZetaRich` = `ρich ✦ テキスト 𝄞 RTL`.** §4.6 found scry
  truncates `char32_t` → byte, silently. `expected.json` records what such a decoder *would*
  produce, so the harness names that bug specifically instead of reporting a generic mismatch. The
  `𝄞` is U+1D11E: one `char32_t`, two UTF-16 units — it catches surrogate-pair handling too.
- **`VisibleTwin` / `HiddenTwin`** with different sizes, so each is individually identifiable while
  still bracketing the visible flag.
- **`AnchoredWide`** has anchors of 0.5 and offsets `[-431, -197, 182, -46]`, resolving to position
  `(12.5, -40.5)`, size `613×151`. Upstream `Control::Data` puts `anchor[4]` immediately after
  `offset[4]`; on a scene where every anchor is zero, reading the wrong one looks correct. This
  node is the tie-breaker. It is also the shape that produced StS2's `BgContainer`
  `offset[-960,-516,1600,684]` in §4.6.
- **`RichTextLabel`** because §4.6 gives it a different text offset from `Label`
  (`0xa78` vs `0x800`).
- **`Probe.cs`** forces a .NET build and publishes `public static Probe Instance` — plain static
  fields, not auto-properties, so the bridge test is about the bridge and not about backing-field
  name mangling.

`expected.json` is generated from the scene and carries its SHA-256; `calibrate.mjs` refuses to run
if the two have drifted.

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
  templates are MSVC-built, so this grid measures one compiler.
- **Windows x86_64 only.** The export preset and template discovery are Windows-specific, matching
  `Godot.External`'s current reach.
- **Double precision is unmeasurable without custom templates** (above).
