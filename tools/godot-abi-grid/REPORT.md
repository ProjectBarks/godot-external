# Godot ABI grid — measured coverage

<!-- GENERATED FILE. Produced by `node calibrate.mjs --report`. Do not hand-edit: the whole
     point of this table (docs/analysis.md §8.9) is that the numbers in it were measured. -->

- Generated: `2026-08-16T23:18:27.207Z`
- Driver: `auto`
- Ground truth: `project/expected.json`, 20 nodes, max depth 7, scene sha256 `b9e5e32b5de06c14`
- Checks per cell: 17 (see the legend below; skipped checks are not counted as passes)

## What this table is

One row per cell of the compat matrix. `Godot.External` has measured exactly **one** layout for
real (Godot 4.5.1, release template, single precision, .NET binding — and on a *modified* engine,
per §4.6/§12.3). This grid manufactures the rest from official export templates so the claim being
published can be *"the calibrator solves layouts it has never seen"* rather than *"we know every
Godot layout"*. Stock templates are not a modified engine, so this validates the **calibrator**,
not a lookup table — which is the correct thing to validate.

A cell is evidence only if it says `n/n`. Everything else is an honest gap.

## Coverage matrix

| Cell | Engine | Built | Result | Checks | Notes |
| --- | --- | --- | --- | --- | --- |
| `4.5-release-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-release-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-release-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.2-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.2-debug-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-debug-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.2-debug-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-debug-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.3-release-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.3-release-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.3-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.3-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.3-debug-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.3-debug-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.3-debug-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.3-debug-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.4-release-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.4-release-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.4-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.4-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.4-debug-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.4-debug-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.4-debug-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.4-debug-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.5-release-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.5-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.5-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.5-debug-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.5-debug-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.5-debug-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.5-debug-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.6-release-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.6-release-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.6-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.6-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.6-debug-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.6-debug-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.6-debug-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.6-debug-double-gdscript` | — | no | `not built` | — | see Gaps |

**0 of 40 cells measured.**

> No cell has been measured. Nothing in this file may be quoted as compatibility evidence.
> This is the expected state on a machine with no Godot export templates installed;
> `pwsh ./build.ps1 -ListOnly` prints exactly what is missing.

## Harness self-validation — NOT coverage

`node selftest.mjs` at `2026-08-16T23:18:27.012Z`: **15/15** scenarios.

This drives the check engine with a synthetic driver carrying the §4.6 `godot-4.5.x-release-single-x64` offsets and the authored scene, then breaks one thing at a time and asserts the right check fails.
It says the harness detects these failure modes. It says **nothing** about any calibrator, and
contributes nothing to the matrix above.

| Injected fault | Checks that caught it | |
| --- | --- | --- |
| _(none — baseline)_ | _nothing (expected)_ | ok |
| `lossy-text` | `strings.text.unicode`, `strings.text.rich` | ok |
| `truncate-text` | `strings.text.rich` | ok |
| `collapse-dup` | `structural.child_head`, `structure.no_collapse` | ok |
| `drop-node` | `structural.child_head`, `structural.parent`, `semantic.size`, `semantic.position`, `semantic.scale`, `semantic.offset`, `semantic.visible`, `strings.names`, `structure.walk_count` | ok |
| `bad-parent` | `structural.child_head`, `structural.parent`, `semantic.size`, `semantic.position`, `semantic.scale`, `semantic.offset`, `semantic.visible`, `strings.names`, `structure.walk_count` | ok |
| `profile-mismatch` | `profile.agreement` | ok |
| `used-profile` | `calibration.unaided` | ok |
| `wrong-structural-method` | `structural.child_head`, `structural.parent` | ok |
| `single-sample` | `semantic.size`, `semantic.position`, `semantic.scale`, `semantic.offset`, `semantic.visible` | ok |
| `anchor-confusion` | `semantic.offset` | ok |
| `visible-blind` | `semantic.visible` | ok |
| `bridge-managed-addr` | `bridge.managed` | ok |
| _(none — baseline)_ | `harness.runtime_axes` | ok |
| _(none — baseline)_ | `structure.walk_count` | ok |

## Gaps and how to close them

`build.ps1` skipped 40 cell(s) on `DESKTOP-1I6F4IL` at
`2026-08-16T19:16:22.3638182-04:00`. Grouped by what is missing:

| Missing | Cells |
| --- | --- |
| no Godot 4.5 editor (gdscript); no 4.5 gdscript release export template | `4.5-release-single-gdscript` |
| no Godot 4.5 editor (dotnet); no 4.5 dotnet release export template | `4.5-release-single-dotnet` |
| no Godot 4.5 editor (gdscript); no double-precision export template | `4.5-release-double-gdscript`, `4.5-debug-double-gdscript` |
| no Godot 4.5 editor (dotnet); no double-precision export template | `4.5-release-double-dotnet`, `4.5-debug-double-dotnet` |
| no Godot 4.5 editor (gdscript); no 4.5 gdscript debug export template | `4.5-debug-single-gdscript` |
| no Godot 4.5 editor (dotnet); no 4.5 dotnet debug export template | `4.5-debug-single-dotnet` |
| no Godot 4.2 editor (gdscript); no 4.2 gdscript release export template | `4.2-release-single-gdscript` |
| no Godot 4.2 editor (dotnet); no 4.2 dotnet release export template | `4.2-release-single-dotnet` |
| no Godot 4.2 editor (gdscript); no double-precision export template | `4.2-release-double-gdscript`, `4.2-debug-double-gdscript` |
| no Godot 4.2 editor (dotnet); no double-precision export template | `4.2-release-double-dotnet`, `4.2-debug-double-dotnet` |
| no Godot 4.2 editor (gdscript); no 4.2 gdscript debug export template | `4.2-debug-single-gdscript` |
| no Godot 4.2 editor (dotnet); no 4.2 dotnet debug export template | `4.2-debug-single-dotnet` |
| no Godot 4.3 editor (gdscript); no 4.3 gdscript release export template | `4.3-release-single-gdscript` |
| no Godot 4.3 editor (dotnet); no 4.3 dotnet release export template | `4.3-release-single-dotnet` |
| no Godot 4.3 editor (gdscript); no double-precision export template | `4.3-release-double-gdscript`, `4.3-debug-double-gdscript` |
| no Godot 4.3 editor (dotnet); no double-precision export template | `4.3-release-double-dotnet`, `4.3-debug-double-dotnet` |
| no Godot 4.3 editor (gdscript); no 4.3 gdscript debug export template | `4.3-debug-single-gdscript` |
| no Godot 4.3 editor (dotnet); no 4.3 dotnet debug export template | `4.3-debug-single-dotnet` |
| no Godot 4.4 editor (gdscript); no 4.4 gdscript release export template | `4.4-release-single-gdscript` |
| no Godot 4.4 editor (dotnet); no 4.4 dotnet release export template | `4.4-release-single-dotnet` |
| no Godot 4.4 editor (gdscript); no double-precision export template | `4.4-release-double-gdscript`, `4.4-debug-double-gdscript` |
| no Godot 4.4 editor (dotnet); no double-precision export template | `4.4-release-double-dotnet`, `4.4-debug-double-dotnet` |
| no Godot 4.4 editor (gdscript); no 4.4 gdscript debug export template | `4.4-debug-single-gdscript` |
| no Godot 4.4 editor (dotnet); no 4.4 dotnet debug export template | `4.4-debug-single-dotnet` |
| no Godot 4.6 editor (gdscript); no 4.6 gdscript release export template | `4.6-release-single-gdscript` |
| no Godot 4.6 editor (dotnet); no 4.6 dotnet release export template | `4.6-release-single-dotnet` |
| no Godot 4.6 editor (gdscript); no double-precision export template | `4.6-release-double-gdscript`, `4.6-debug-double-gdscript` |
| no Godot 4.6 editor (dotnet); no double-precision export template | `4.6-release-double-dotnet`, `4.6-debug-double-dotnet` |
| no Godot 4.6 editor (gdscript); no 4.6 gdscript debug export template | `4.6-debug-single-gdscript` |
| no Godot 4.6 editor (dotnet); no 4.6 dotnet debug export template | `4.6-debug-single-dotnet` |

Install actions, de-duplicated:

- Download Godot_v4.2-stable_export_templates.tpz from https://godotengine.org/download/archive/4.2-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.2.stable)
- Download Godot_v4.2-stable_mono_export_templates.tpz from https://godotengine.org/download/archive/4.2-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.2.stable.mono)
- Download Godot_v4.2-stable_mono_win64.zip from https://godotengine.org/download/archive/4.2-stable/ and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.2-stable_win64.zip from https://godotengine.org/download/archive/4.2-stable/ and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.3-stable_export_templates.tpz from https://godotengine.org/download/archive/4.3-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.3.stable)
- Download Godot_v4.3-stable_mono_export_templates.tpz from https://godotengine.org/download/archive/4.3-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.3.stable.mono)
- Download Godot_v4.3-stable_mono_win64.zip from https://godotengine.org/download/archive/4.3-stable/ and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.3-stable_win64.zip from https://godotengine.org/download/archive/4.3-stable/ and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.4-stable_export_templates.tpz from https://godotengine.org/download/archive/4.4-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.4.stable)
- Download Godot_v4.4-stable_mono_export_templates.tpz from https://godotengine.org/download/archive/4.4-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.4.stable.mono)
- Download Godot_v4.4-stable_mono_win64.zip from https://godotengine.org/download/archive/4.4-stable/ and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.4-stable_win64.zip from https://godotengine.org/download/archive/4.4-stable/ and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.5-stable_export_templates.tpz from https://godotengine.org/download/archive/4.5-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.5.stable)
- Download Godot_v4.5-stable_mono_export_templates.tpz from https://godotengine.org/download/archive/4.5-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.5.stable.mono)
- Download Godot_v4.5-stable_mono_win64.zip from https://godotengine.org/download/archive/4.5-stable/ and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.5-stable_win64.zip from https://godotengine.org/download/archive/4.5-stable/ and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.6-stable_export_templates.tpz from https://godotengine.org/download/archive/4.6-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.6.stable)
- Download Godot_v4.6-stable_mono_export_templates.tpz from https://godotengine.org/download/archive/4.6-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.6.stable.mono)
- Download Godot_v4.6-stable_mono_win64.zip from https://godotengine.org/download/archive/4.6-stable/ and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.6-stable_win64.zip from https://godotengine.org/download/archive/4.6-stable/ and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- No OFFICIAL double-precision export templates exist for any Godot version. Build them from source: scons platform=windows target=template_release (and target=template_debug) precision=double [module_mono_enabled=yes], then place the results at <dir>\4.2\windows_<release|debug>_x86_64[.mono].exe and pass -DoubleTemplateDir <dir>
- No OFFICIAL double-precision export templates exist for any Godot version. Build them from source: scons platform=windows target=template_release (and target=template_debug) precision=double [module_mono_enabled=yes], then place the results at <dir>\4.3\windows_<release|debug>_x86_64[.mono].exe and pass -DoubleTemplateDir <dir>
- No OFFICIAL double-precision export templates exist for any Godot version. Build them from source: scons platform=windows target=template_release (and target=template_debug) precision=double [module_mono_enabled=yes], then place the results at <dir>\4.4\windows_<release|debug>_x86_64[.mono].exe and pass -DoubleTemplateDir <dir>
- No OFFICIAL double-precision export templates exist for any Godot version. Build them from source: scons platform=windows target=template_release (and target=template_debug) precision=double [module_mono_enabled=yes], then place the results at <dir>\4.5\windows_<release|debug>_x86_64[.mono].exe and pass -DoubleTemplateDir <dir>
- No OFFICIAL double-precision export templates exist for any Godot version. Build them from source: scons platform=windows target=template_release (and target=template_debug) precision=double [module_mono_enabled=yes], then place the results at <dir>\4.6\windows_<release|debug>_x86_64[.mono].exe and pass -DoubleTemplateDir <dir>

### Known structural gaps in the grid itself

- **Double precision has no official export templates.** Godot ships single-precision templates
  only; `precision=double` requires building the engine from source with `scons precision=double`.
  Those eight cells stay unmeasurable until someone supplies custom templates
  (`build.ps1 -DoubleTemplateDir`). They are listed rather than dropped, because `real_t` width
  changes every float offset and is therefore the axis most likely to break a calibrator.
- **Compiler is a fifth axis (§8.9, from Zolt-Dump's table): MSVC vs GCC/MinGW.** Official
  Windows templates are MSVC-built, so this grid measures one compiler only.
- **Stock templates are not a modified engine.** StS2 runs a customised 4.5.1. A green row here
  says the calibrator solved a layout it had not seen; it does not say the shipped profile is
  right for anyone else's fork.

## Legend

| Result | Meaning |
| --- | --- |
| `n/n` | every applicable check passed; this cell is evidence |
| `n/m` | the calibrator ran and got some checks wrong — see the per-cell detail |
| `not built` | no export exists for this cell (missing engine or export template) |
| `not run` | built, but `calibrate.mjs` has not judged it |
| `driver unavailable` | built, but no calibration driver was configured — nothing was tested |
| `error` | the target or the driver fell over; NOT a calibrator verdict |

| Check | Asserts |
| --- | --- |
| `harness.runtime_axes` | target self-identifies as the cell it is filed under |
| `calibration.unaided` | no shipped profile consumed |
| `structural.child_head` | (a) child-list head by pointer identity |
| `structural.parent` | (a) parent by pointer identity |
| `semantic.size` | (b) size by known-value intersection |
| `semantic.position` | (b) position |
| `semantic.scale` | (b) scale |
| `semantic.offset` | (b) anchor offsets |
| `semantic.visible` | (b) visible flag |
| `strings.names` | (c) node names exact |
| `strings.text.ascii` | (c) ASCII label text exact |
| `strings.text.unicode` | (c) non-ASCII label text exact |
| `strings.text.rich` | (c) RichTextLabel text exact (astral codepoint) |
| `structure.no_collapse` | duplicated size does not collapse nodes |
| `structure.walk_count` | (e) full-tree walk count |
| `profile.agreement` | (d) agreement with the shipped §4.6 profile |
| `bridge.managed` | managed static root -> NativePtr -> walk root |

Letters `(a)`–`(e)` map to the five assertions listed in docs/analysis.md §8.9.

