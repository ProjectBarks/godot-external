# Godot ABI grid — measured coverage

<!-- GENERATED FILE. Produced by `node calibrate.mjs --report`. Do not hand-edit: the whole
     point of this table (docs/analysis.md §8.9) is that the numbers in it were measured. -->

- Generated: `2026-08-17T16:49:14.009Z`
- Driver: `dotnet:Godot.External.Calibrator`
- Ground truth: `project/expected.json`, 25 nodes, max depth 7, scene sha256 `82d74c936dbf7c2f`
- Checks per cell: 22 (see the legend below; skipped checks are not counted as passes)

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
| `4.5-release-single-dotnet` | 4.5-stable (official) | yes | 19/22 | 19 pass · 3 fail · 0 n/a | reference cell |
| `4.2-release-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-release-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.2-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.2-debug-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-debug-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.2-debug-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-debug-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.3-release-single-dotnet` | 4.3-stable (official) | yes | 20/21 | 20 pass · 1 fail · 1 n/a | 1 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.3-release-single-gdscript` | 4.3-stable (official) | yes | 20/20 † | 20 pass · 0 fail · 2 n/a | 2 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.3-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.3-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.3-debug-single-dotnet` | 4.3-stable (official) | yes | 20/21 | 20 pass · 1 fail · 1 n/a | 1 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.3-debug-single-gdscript` | 4.3-stable (official) | yes | 20/20 † | 20 pass · 0 fail · 2 n/a | 2 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
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
| `4.5-release-single-gdscript` | 4.5-stable (official) | yes | **21/21** | 21 pass · 0 fail · 1 n/a | 1 n/a |
| `4.5-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.5-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.5-debug-single-dotnet` | 4.5-stable (official) | yes | 21/22 | 21 pass · 1 fail · 0 n/a | — |
| `4.5-debug-single-gdscript` | 4.5-stable (official) | yes | 18/21 | 18 pass · 3 fail · 1 n/a | 1 n/a |
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

**8 of 40 cells measured.**

## Cross-cell assertions

Contradictions no single cell can see. A calibrator can be perfectly self-consistent within one
cell and disagree with the cell next to it — and on cells no shipped profile covers, this and
`offsets.internal_consistency` are the only things standing between a derived offset and nothing
at all. Neither is corroboration by an independent SOURCE; both cells come from one calibrator.

| Check | Status | Detail |
| --- | --- | --- |
| `grid.binding_invariance` | PASS | 50 offset(s) agree across bindings in 4 group(s): 4.5-release-single (dotnet/gdscript, 12 shared key(s)); 4.3-release-single (dotnet/gdscript, 13 shared key(s)); 4.3-debug-single (dotnet/gdscript, 13 shared key(s)); 4.5-debug-single (dotnet/gdscript, 12 shared key(s)). This is corroboration by an independent RUN, not by an answer key — but two cells derived by the same calibrator can also be wrong the same way, so it does not stand in for the shipped profile.<br>    not compared: 4.5-release-single walk:scriptInstance.ownerBackref: implementing classes differ (CSharpInstance vs GDScriptInstanc |
| `grid.debug_release_delta` | PASS | 42 key(s) across 4 release/debug pair(s) all sit exactly 0x8 apart: 4.5-single-dotnet: 10 shared key(s), delta 0x8; 4.3-single-dotnet: 11 shared key(s), delta 0x8; 4.3-single-gdscript: 11 shared key(s), delta 0x8; 4.5-single-gdscript: 10 shared key(s), delta 0x8 |

## Per-cell check detail

### `4.5-release-single-dotnet` — reference cell

Engine: `4.5-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `godot-4.5.x-release-single-x64`

<details><summary>driver notes</summary>

- walk root 0x1ca2f11e1e0 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1c0 and node.parent 0x128 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x148/next 0x0, head 0x150/next 0x8. Taking head 0x148 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x1ca2f23fbe0, 0x1ca2f2323f0
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- canvasItem.visible: not derived — 2 candidates survived (0x370, 0x400); another sample with a different expected value is needed.
- canvasItem.visible: every nominated byte was eliminated. Which rule did it, per candidate — 0x36f rejected on the visible twin: not 8-aligned; CanvasItem::visible always is; 0x401 rejected on the visible twin: not 8-aligned; CanvasItem::visible always is
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x1138 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x1ca2f136c50).
- richTextLabel.text: 0x7f8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1ca2f137590, 0x1ca2f225600).
- richTextLabel.text: 0x800 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1ca2f137590, 0x1ca2f225600).
- richTextLabel.text: 0x828 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1ca2f137590, 0x1ca2f225600).
- richTextLabel.text: 0x858 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1ca2f137590, 0x1ca2f225600).
- richTextLabel.text: 0x1138 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1ca2f136c50).
- richTextLabel.text: 0x1140 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1ca2f136c50).
- richTextLabel.text: 0x1168 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1ca2f136c50).
- richTextLabel.text: 0x1198 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1ca2f136c50).
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.5-stable (official) release/single/dotnet; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x148, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x128 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 10 offset(s) across 5 class band(s) (Object < Node < Control < Label < RichTextLabel) + 4 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent source (the s |
| `semantic.size` | PASS | control.size 0x4c0, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x4b8, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x4a8, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x470, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | FAIL | the driver reported per-key sample counts but none for "canvasItem.visible" (it has: node.parent, node.childListHead, node.scriptInstance, control.size, control.offset, control.position, control.scale, node.name, label.text, richTextLabel.text). §12.5: one control gave four candidate offsets, so "how many samples "were intersected" is the precondition for reading anything into this offset at all. A count the harness cannot read is not a count. |
| `strings.names` | PASS | node.name 0x1c0, 27/27 StringNames exact against their position in the child lists (including 2 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 23/23 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 27/27 nodes walked (25 authored + 2 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | FAIL | 1 of 15 comparable key(s) were neither derived nor explained, so godot-4.5.x-release-single-x64 could not be checked against them:<br>    - canvasItem.visible<br>    A driver that cannot derive one of these is entitled to say so in derivation.notDerived,<br>    with a reason. What it may not do is leave it absent and unremarked, because that is<br>    indistinguishable from having derived it correctly.<br>    not compared: control.globalPosition (a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watc |
| `bridge.managed` | FAIL | 6 of 6 expected managed field(s) were not read: ProbeAscii, ProbeUnicode, ProbeInt32, ProbeInt64, ProbeFloat, ProbeBool. expected.json names them precisely so the driver cannot choose which ones count. |

### `4.3-release-single-dotnet`

Engine: `4.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- walk root 0x24d8e724f10 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1d0 and node.parent 0x128 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x150/next 0x0, head 0x158/next 0x8. Taking head 0x150 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x24d8e7f9d70, 0x24d8e7d28a0
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x11e0 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x24d8e728940).
- richTextLabel.text: 0x8f0 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x24d8e729230, 0x24d8e732e90).
- richTextLabel.text: 0x8f8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x24d8e729230, 0x24d8e732e90).
- richTextLabel.text: 0x918 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x24d8e729230, 0x24d8e732e90).
- richTextLabel.text: 0xa28 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x24d8e729230, 0x24d8e732e90).
- richTextLabel.text: 0x1030 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24d8e732e90).
- richTextLabel.text: 0x10e0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24d8e732e90).
- richTextLabel.text: 0x11e0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24d8e728940).
- richTextLabel.text: 0x11e8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24d8e728940).
- richTextLabel.text: 0x1208 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24d8e728940).
- richTextLabel.text: 0x1318 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24d8e728940).
- richTextLabel.text: 0x1390 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24d8e7d4c70).
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.3-stable (official) release/single/dotnet; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x150, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x128 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 4 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x520, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x518, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x508, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x4d8, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x418, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x1d0, 27/27 StringNames exact against their position in the child lists (including 2 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 23/23 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 27/27 nodes walked (25 authored + 2 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.3-release-single-dotnet — nothing to cross-check against, and nothing to fall back to |
| `bridge.managed` | FAIL | 6 of 6 expected managed field(s) were not read: ProbeAscii, ProbeUnicode, ProbeInt32, ProbeInt64, ProbeFloat, ProbeBool. expected.json names them precisely so the driver cannot choose which ones count. |

### `4.3-release-single-gdscript`

Engine: `4.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- walk root 0x1cbb0901150 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1d0 and node.parent 0x128 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x150/next 0x0, head 0x158/next 0x8. Taking head 0x150 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x1cbb098a440, 0x1cbb08cabe0
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x11e0 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x1cbb08c1cb0).
- richTextLabel.text: 0x7d0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1cbb08ca2e0).
- richTextLabel.text: 0x8f0 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1cbb08c25a0, 0x1cbb08c53f0).
- richTextLabel.text: 0x8f8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1cbb08c25a0, 0x1cbb08c53f0).
- richTextLabel.text: 0x918 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1cbb08c25a0, 0x1cbb08c53f0).
- richTextLabel.text: 0x928 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1cbb09161d0).
- richTextLabel.text: 0xaf0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1cbb08c99e0).
- richTextLabel.text: 0xb68 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1cbb08c53f0).
- richTextLabel.text: 0xcd8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1cbb08c6af0).
- richTextLabel.text: 0x10d0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1cbb08c99e0).
- richTextLabel.text: 0x11e0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1cbb08c1cb0).
- richTextLabel.text: 0x11e8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1cbb08c1cb0).
- richTextLabel.text: 0x1200 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1cbb0903520).
- richTextLabel.text: 0x1208 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1cbb08c1cb0).
- richTextLabel.text: 0x1390 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1cbb08ccfb0).
- richTextLabel.text: 0x13e0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1cbb08c90f0).
- scriptInstance.gcHandle: not derived — this target's scripts are not .NET, and a GDScript ScriptInstance carries no GCHandle to locate

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.3-stable (official) release/single/gdscript; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x150, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x128 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 3 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x520, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x518, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x508, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x4d8, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x418, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x1d0, 27/27 StringNames exact against their position in the child lists (including 2 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 23/23 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 27/27 nodes walked (25 authored + 2 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.3-release-single-gdscript — nothing to cross-check against, and nothing to fall back to |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.3-debug-single-dotnet`

Engine: `4.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- walk root 0x19990173b40 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1d8 and node.parent 0x130 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x158/next 0x0, head 0x160/next 0x8. Taking head 0x158 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x19990252bb0, 0x19990219130
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x1208 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x19990187040).
- richTextLabel.text: 0x8f8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x19990187950, 0x199901927e0).
- richTextLabel.text: 0x900 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x19990187950, 0x199901927e0).
- richTextLabel.text: 0x920 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x19990187950, 0x199901927e0).
- richTextLabel.text: 0x1208 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19990187040).
- richTextLabel.text: 0x1210 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19990187040).
- richTextLabel.text: 0x1230 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19990187040).
- richTextLabel.text: 0x13b8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1999022f640).
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.3-stable (official) debug/single/dotnet; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x158, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x130 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 4 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x528, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x520, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x510, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x4e0, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x420, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x1d8, 27/27 StringNames exact against their position in the child lists (including 2 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 23/23 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 27/27 nodes walked (25 authored + 2 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.3-debug-single-dotnet — nothing to cross-check against, and nothing to fall back to |
| `bridge.managed` | FAIL | 6 of 6 expected managed field(s) were not read: ProbeAscii, ProbeUnicode, ProbeInt32, ProbeInt64, ProbeFloat, ProbeBool. expected.json names them precisely so the driver cannot choose which ones count. |

### `4.3-debug-single-gdscript`

Engine: `4.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- walk root 0x267a3925d50 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1d8 and node.parent 0x130 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x158/next 0x0, head 0x160/next 0x8. Taking head 0x158 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x267a398f430, 0x267a3998a10
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x1208 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x267a3979240).
- richTextLabel.text: 0x6e8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x267a398f430).
- richTextLabel.text: 0x8f8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x267a3979b50, 0x267a397a9b0).
- richTextLabel.text: 0x900 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x267a3979b50, 0x267a397a9b0).
- richTextLabel.text: 0x920 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x267a3979b50, 0x267a397a9b0).
- richTextLabel.text: 0xc60 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x267a397bfb0).
- richTextLabel.text: 0xfb0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x267a3925d50).
- richTextLabel.text: 0x1008 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x267a398eb10).
- richTextLabel.text: 0x1018 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x267a3925d50).
- richTextLabel.text: 0x1208 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x267a3979240).
- richTextLabel.text: 0x1210 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x267a3979240).
- richTextLabel.text: 0x1230 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x267a3979240).
- scriptInstance.gcHandle: not derived — this target's scripts are not .NET, and a GDScript ScriptInstance carries no GCHandle to locate

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.3-stable (official) debug/single/gdscript; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x158, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x130 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 3 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x528, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x520, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x510, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x4e0, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x420, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x1d8, 27/27 StringNames exact against their position in the child lists (including 2 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 23/23 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 27/27 nodes walked (25 authored + 2 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.3-debug-single-gdscript — nothing to cross-check against, and nothing to fall back to |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.5-release-single-gdscript`

Engine: `4.5-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `godot-4.5.x-release-single-x64`

<details><summary>driver notes</summary>

- walk root 0x268b06dec00 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1c0 and node.parent 0x128 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x148/next 0x0, head 0x150/next 0x8. Taking head 0x148 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 3 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x268b0767420, 0x268b06f7d50, 0x268b07df070
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0xff8 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x268b06e5c50).
- richTextLabel.text: 0x7f8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x268b06e6450, 0x268b06e7270).
- richTextLabel.text: 0x800 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x268b06e6450, 0x268b06e7270).
- richTextLabel.text: 0x828 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x268b06e6450, 0x268b06e7270).
- richTextLabel.text: 0x858 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x268b06e6450, 0x268b06e7270).
- richTextLabel.text: 0x890 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0x940 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0x9f0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0xa58 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06e7270).
- richTextLabel.text: 0xa70 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06e7270).
- richTextLabel.text: 0xaa0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0xb50 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0xc00 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0xc98 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b0772470).
- richTextLabel.text: 0xcb0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0xd60 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0xdb8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06e7270).
- richTextLabel.text: 0xde8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06e7270).
- richTextLabel.text: 0xe10 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0xe18 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06e7270).
- richTextLabel.text: 0xe88 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b0772470).
- richTextLabel.text: 0xe90 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06e7270).
- richTextLabel.text: 0xec0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0xed8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06e7270).
- richTextLabel.text: 0xef0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06e7270).
- richTextLabel.text: 0xf70 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0xff8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06e5c50).
- richTextLabel.text: 0x1000 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06e5c50).
- richTextLabel.text: 0x1020 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0x1028 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06e5c50).
- richTextLabel.text: 0x1058 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06e5c50).
- richTextLabel.text: 0x1078 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b0772470).
- richTextLabel.text: 0x1090 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06dec00).
- richTextLabel.text: 0x10d0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0x1140 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06dec00).
- richTextLabel.text: 0x1180 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0x11f0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06dec00).
- richTextLabel.text: 0x1230 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0x1278 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b0765840).
- richTextLabel.text: 0x12a0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06dec00).
- richTextLabel.text: 0x12e0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- richTextLabel.text: 0x1350 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06dec00).
- richTextLabel.text: 0x1390 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268b06df400).
- scriptInstance.gcHandle: not derived — this target's scripts are not .NET, and a GDScript ScriptInstance carries no GCHandle to locate

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.5-stable (official) release/single/gdscript; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x148, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x128 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 3 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x4c0, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x4b8, 24 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x4a8, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x470, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x370, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x1c0, 27/27 StringNames exact against their position in the child lists (including 2 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 23/23 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 27/27 nodes walked (25 authored + 2 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | PASS | 14 of 17 key(s) in godot-4.5.x-release-single-x64 compared and matching (trust=verified)<br>    not compared: control.globalPosition (a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.); control.anchor (no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the con |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.5-debug-single-dotnet`

Engine: `4.5-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `godot-4.5.x-debug-single-x64`

<details><summary>driver notes</summary>

- walk root 0x19dcac48a70 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1c8 and node.parent 0x130 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x150/next 0x0, head 0x158/next 0x8. Taking head 0x150 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x19dcacfb790, 0x19dcacf7050
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x1010 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x19dcac5cfc0).
- richTextLabel.text: 0x800 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x19dcac5d7d0, 0x19dcaceb430).
- richTextLabel.text: 0x808 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x19dcac5d7d0, 0x19dcaceb430).
- richTextLabel.text: 0x830 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x19dcac5d7d0, 0x19dcaceb430).
- richTextLabel.text: 0x860 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x19dcac5d7d0, 0x19dcaceb430).
- richTextLabel.text: 0xb48 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19dcac48a70).
- richTextLabel.text: 0xd98 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19dcac48a70).
- richTextLabel.text: 0xda0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19dcac48a70).
- richTextLabel.text: 0xed8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19dcaceb430).
- richTextLabel.text: 0xee0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19dcaceb430).
- richTextLabel.text: 0xff8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19dcac48a70).
- richTextLabel.text: 0x1010 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19dcac5cfc0).
- richTextLabel.text: 0x1018 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19dcac5cfc0).
- richTextLabel.text: 0x1040 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19dcac5cfc0).
- richTextLabel.text: 0x1070 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19dcac5cfc0).
- richTextLabel.text: 0x1290 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x19dcacfa080).
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.5-stable (official) debug/single/dotnet; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x150, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x130 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 4 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x4c8, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x4c0, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x4b0, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x478, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x378, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x1c8, 27/27 StringNames exact against their position in the child lists (including 2 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 23/23 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 27/27 nodes walked (25 authored + 2 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | PASS | 15 of 17 key(s) in godot-4.5.x-debug-single-x64 compared and matching (trust=measured)<br>    not compared: control.globalPosition (a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.); control.anchor (no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confu |
| `bridge.managed` | FAIL | 6 of 6 expected managed field(s) were not read: ProbeAscii, ProbeUnicode, ProbeInt32, ProbeInt64, ProbeFloat, ProbeBool. expected.json names them precisely so the driver cannot choose which ones count. |

### `4.5-debug-single-gdscript`

Engine: `4.5-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `godot-4.5.x-debug-single-x64`

<details><summary>driver notes</summary>

- walk root 0x18973f94f80 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1c8 and node.parent 0x130 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x150/next 0x0, head 0x158/next 0x8. Taking head 0x150 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x18973ffc1f0, 0x18973ff7960
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- richTextLabel.text: 0x800 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x18973fab360, 0x18973fffdd0).
- richTextLabel.text: 0x808 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x18973fab360, 0x18973fffdd0).
- richTextLabel.text: 0x830 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x18973fab360, 0x18973fffdd0).
- richTextLabel.text: 0x860 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x18973fab360, 0x18973fffdd0).
- richTextLabel.text: 0x968 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x18973fffdd0).
- richTextLabel.text: 0xd90 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x18974006b20).
- richTextLabel.text: 0x1070 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x18973f94f80).
- richTextLabel.text: 0x1318 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x18974006b20).
- richTextLabel.text: candidates split evenly on node 0x18974006e30, with nothing to prefer between them, so no class offset is reported and NO text is read through these candidates — 0xa80="[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode", 0x1008="ρich ✦ テキスト 𝄞 RTL"
- scriptInstance.gcHandle: not derived — this target's scripts are not .NET, and a GDScript ScriptInstance carries no GCHandle to locate

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.5-stable (official) debug/single/gdscript; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x150, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x130 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 10 offset(s) across 5 class band(s) (Object < Node < CanvasItem < Control < Label) + 3 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent source (the ship |
| `semantic.size` | PASS | control.size 0x4c8, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x4c0, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x4b0, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x478, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x378, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x1c8, 27/27 StringNames exact against their position in the child lists (including 2 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | FAIL | RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaRich: driver reported no text |
| `strings.text.richBbcode` | FAIL | RootHarness/OmegaPanel/OmegaRich: driver reported no text |
| `strings.text.absent` | PASS | 23/23 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 2/2 reported string(s) byte-exact (2 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 27/27 nodes walked (25 authored + 2 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | FAIL | 1 of 14 comparable key(s) were neither derived nor explained, so godot-4.5.x-debug-single-x64 could not be checked against them:<br>    - richTextLabel.text<br>    A driver that cannot derive one of these is entitled to say so in derivation.notDerived,<br>    with a reason. What it may not do is leave it absent and unremarked, because that is<br>    indistinguishable from having derived it correctly.<br>    not compared: control.globalPosition (a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watche |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

## Harness self-validation — NOT coverage

`node selftest.mjs` at `2026-08-17T16:28:33.834Z`: **55/55** scenarios.

This drives the check engine with a synthetic driver carrying the §4.6 `godot-4.5.x-release-single-x64` offsets and the authored scene, then breaks one thing at a time and asserts the right check fails.
It says the harness detects these failure modes. It says **nothing** about any calibrator, and
contributes nothing to the matrix above.

| Injected fault | Checks that caught it | |
| --- | --- | --- |
| _(none — baseline)_ | _nothing (expected)_ | ok |
| `lossy-text` | `strings.text.unicode`, `strings.text.rich`, `strings.text.richBbcode`, `strings.text.wrong` | ok |
| `truncate-text` | `strings.text.rich`, `strings.text.wrong` | ok |
| `collapse-dup` | `structural.child_head`, `structural.parent`, `structure.no_collapse` | ok |
| `drop-node` | `structural.child_head`, `structural.parent`, `semantic.size`, `semantic.position`, `semantic.scale`, `semantic.offset`, `semantic.visible`, `strings.names`, `structure.walk_count` | ok |
| `bad-parent` | `structural.child_head`, `structural.parent`, `semantic.size`, `semantic.position`, `semantic.scale`, `semantic.offset`, `semantic.visible`, `structure.walk_count` | ok |
| `profile-mismatch` | `profile.agreement` | ok |
| `used-profile` | `calibration.unaided` | ok |
| `wrong-structural-method` | `structural.child_head`, `structural.parent` | ok |
| `single-sample` | `semantic.size`, `semantic.position`, `semantic.scale`, `semantic.offset`, `semantic.visible` | ok |
| `anchor-confusion` | `semantic.offset` | ok |
| `visible-blind` | `semantic.visible`, `geometry.absent` | ok |
| `phantom-text` | `strings.text.absent` | ok |
| `phantom-geometry` | `geometry.absent` | ok |
| `drop-bbcode-text` | `strings.text.richBbcode` | ok |
| `drop-gchandle` | `profile.agreement` | ok |
| `phantom-anchors` | `geometry.absent` | ok |
| `wrong-anchors` | `semantic.offset` | ok |
| `drop-bare-node` | `structural.child_head`, `structural.parent`, `strings.names`, `geometry.absent`, `structure.walk_count` | ok |
| `wrong-text` | `strings.text.ascii`, `strings.text.wrong` | ok |
| `phantom-text-internal` | `structural.parent`, `strings.names`, `strings.text.absent`, `structure.walk_count` | ok |
| `bridge-managed-addr` | `bridge.managed` | ok |
| _(none — baseline)_ | `harness.runtime_axes` | ok |
| _(none — baseline)_ | `structure.walk_count` | ok |
| `flat-offsets` | `offsets.internal_consistency`, `profile.agreement` | ok |
| `flat-offsets` | `offsets.internal_consistency` | ok |
| `bridge-no-fields` | `bridge.managed` | ok |
| `bridge-hollow-reverse` | `bridge.managed` | ok |
| `no-script-instance-class` | `profile.agreement` | ok |
| `lowercase-script-instance-class` | _nothing (expected)_ | ok |
| `visible-as-byte` | `geometry.absent` | ok |
| `text-as-number` | `strings.text.absent` | ok |
| `used-profile-string` | `calibration.unaided` | ok |
| `no-used-profile` | `calibration.unaided` | ok |
| `profile-consulted-empty` | `calibration.unaided` | ok |
| `declare-everything-notderived` | `semantic.visible`, `profile.agreement` | ok |
| `empty-notderived-reason` | `profile.agreement` | ok |
| `samples-map-omits-key` | `semantic.visible` | ok |
| `walkcount-as-string` | `structure.walk_count` | ok |
| `mangle-name` | `structural.child_head`, `semantic.size`, `semantic.position`, `semantic.scale`, `semantic.offset`, `semantic.visible`, `strings.names`, `strings.text.unicode`, `strings.text.absent` | ok |
| `partial-anchors` | `semantic.offset` | ok |
| `bad-parent` | `structural.child_head`, `structural.parent`, `semantic.size`, `semantic.position`, `semantic.scale`, `semantic.offset`, `semantic.visible`, `structure.walk_count` | ok |
| _(none — baseline)_ | _nothing (expected)_ | ok |
| `mangle-internal-name` | `structural.child_head`, `strings.names` | ok |
| _(none — baseline)_ | `harness.runtime_axes`, `structural.parent`, `strings.names` | ok |
| _(none — baseline)_ | _nothing (expected)_ | ok |
| _(none — baseline)_ | _nothing (expected)_ | ok |
| _(none — baseline)_ | _nothing (expected)_ | ok |
| _(none — baseline)_ | _nothing (expected)_ | ok |
| _(none — baseline)_ | _nothing (expected)_ | ok |
| _(none — baseline)_ | _nothing (expected)_ | ok |
| _(none — baseline)_ | _nothing (expected)_ | ok |
| _(none — baseline)_ | _nothing (expected)_ | ok |
| _(none — baseline)_ | _nothing (expected)_ | ok |
| _(none — baseline)_ | _nothing (expected)_ | ok |

## Gaps and how to close them

`build.ps1` skipped 32 cell(s) on `DESKTOP-1I6F4IL` at
`2026-08-17T12:29:34.0100393-04:00`. Grouped by what is missing:

| Missing | Cells |
| --- | --- |
| no double-precision export template | `4.5-release-double-gdscript`, `4.5-release-double-dotnet`, `4.5-debug-double-gdscript`, `4.5-debug-double-dotnet`, `4.3-release-double-gdscript`, `4.3-release-double-dotnet`, `4.3-debug-double-gdscript`, `4.3-debug-double-dotnet` |
| no Godot 4.2 editor (gdscript); no 4.2 gdscript release export template | `4.2-release-single-gdscript` |
| no Godot 4.2 editor (dotnet); no 4.2 dotnet release export template | `4.2-release-single-dotnet` |
| no Godot 4.2 editor (gdscript); no double-precision export template | `4.2-release-double-gdscript`, `4.2-debug-double-gdscript` |
| no Godot 4.2 editor (dotnet); no double-precision export template | `4.2-release-double-dotnet`, `4.2-debug-double-dotnet` |
| no Godot 4.2 editor (gdscript); no 4.2 gdscript debug export template | `4.2-debug-single-gdscript` |
| no Godot 4.2 editor (dotnet); no 4.2 dotnet debug export template | `4.2-debug-single-dotnet` |
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
- Download Godot_v4.2-stable_mono_win64.zip from https://godotengine.org/download/archive/4.2-stable/ (GitHub release asset name: Godot_v4.2-stable_mono_win64.zip) and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.2-stable_win64.zip from https://godotengine.org/download/archive/4.2-stable/ (GitHub release asset name: Godot_v4.2-stable_win64.exe.zip) and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.4-stable_export_templates.tpz from https://godotengine.org/download/archive/4.4-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.4.stable)
- Download Godot_v4.4-stable_mono_export_templates.tpz from https://godotengine.org/download/archive/4.4-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.4.stable.mono)
- Download Godot_v4.4-stable_mono_win64.zip from https://godotengine.org/download/archive/4.4-stable/ (GitHub release asset name: Godot_v4.4-stable_mono_win64.zip) and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.4-stable_win64.zip from https://godotengine.org/download/archive/4.4-stable/ (GitHub release asset name: Godot_v4.4-stable_win64.exe.zip) and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.6-stable_export_templates.tpz from https://godotengine.org/download/archive/4.6-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.6.stable)
- Download Godot_v4.6-stable_mono_export_templates.tpz from https://godotengine.org/download/archive/4.6-stable/ then Editor > Manage Export Templates > Install from File (or unzip its 'templates' folder to C:\Users\Brandon\AppData\Roaming\Godot\export_templates\4.6.stable.mono)
- Download Godot_v4.6-stable_mono_win64.zip from https://godotengine.org/download/archive/4.6-stable/ (GitHub release asset name: Godot_v4.6-stable_mono_win64.zip) and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
- Download Godot_v4.6-stable_win64.zip from https://godotengine.org/download/archive/4.6-stable/ (GitHub release asset name: Godot_v4.6-stable_win64.exe.zip) and unzip it under C:\Users\Brandon\godot-external\tools\godot-abi-grid\bin
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
| `n/n †` | every applicable check passed, but no shipped profile covers this cell, so **no independent source corroborates its offsets**. `offsets.internal_consistency` shows the derived numbers hang together — a uniformly wrong layout also hangs together. Not evidence of correct offsets until the §13.2 getter decoder or a measured profile covers it. |
| `n/m` | the calibrator ran and got some checks wrong — see the per-cell detail |
| `not built` | no export exists for this cell (missing engine or export template) |
| `not run` | built, but `calibrate.mjs` has not judged it |
| `driver unavailable` | built, but no calibration driver was configured — nothing was tested |
| `error` | the target or the driver fell over; NOT a calibrator verdict |

| Check | Asserts |
| --- | --- |
| `harness.runtime_axes` | target self-identifies as the cell it is filed under |
| `calibration.unaided` | no shipped profile consumed, and the driver states so as a boolean |
| `structural.child_head` | (a) child-list head by pointer identity |
| `structural.parent` | (a) parent round-trips against the child list |
| `offsets.internal_consistency` | derived offsets are mutually consistent with single inheritance |
| `semantic.size` | (b) size by known-value intersection |
| `semantic.position` | (b) position |
| `semantic.scale` | (b) scale |
| `semantic.offset` | (b) anchor offsets |
| `semantic.visible` | (b) visible flag |
| `strings.names` | (c) node names exact, matched by child-list position |
| `strings.text.ascii` | (c) ASCII label text exact |
| `strings.text.unicode` | (c) non-ASCII label text exact |
| `strings.text.rich` | (c) RichTextLabel text exact (astral codepoint) |
| `strings.text.richBbcode` | (c) raw BBCode source exact, not the rendered text |
| `strings.text.absent` | (c) nodes with no text member report null, not garbage |
| `strings.text.wrong` | (c) authored text is exact where present, never invented |
| `geometry.absent` | (b) nodes with no CanvasItem report no geometry, not garbage |
| `structure.no_collapse` | duplicated size does not collapse nodes |
| `structure.walk_count` | (e) full-tree walk count |
| `profile.agreement` | (d) agreement with the shipped §4.6 profile |
| `bridge.managed` | managed static root -> NativePtr -> walk root, and the field values |

Letters `(a)`–`(e)` map to the five assertions listed in docs/analysis.md §8.9.

