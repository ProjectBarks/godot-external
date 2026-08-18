# Godot ABI grid — measured coverage

<!-- GENERATED FILE. Produced by `node calibrate.mjs --report`. Do not hand-edit: the whole
     point of this table (docs/analysis.md §8.9) is that the numbers in it were measured. -->

- Generated: `2026-08-18T06:24:30.555Z`
- Driver: `dotnet:Godot.External.Calibrator`
- Ground truth: `project/expected.json`, 25 nodes, max depth 7, scene sha256 `82d74c936dbf7c2f`
- Checks per cell: 23 (see the legend below; skipped checks are not counted as passes)

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
| `4.5-release-single-dotnet` | 4.5-stable (official) | yes | **23/23** | 23 pass · 0 fail · 0 n/a | reference cell |
| `4.2-release-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-release-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.2-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.2-debug-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-debug-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.2-debug-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-debug-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.3-release-single-dotnet` | 4.3-stable (official) | yes | 22/22 † | 22 pass · 0 fail · 1 n/a | 1 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.3-release-single-gdscript` | 4.3-stable (official) | yes | 21/21 † | 21 pass · 0 fail · 2 n/a | 2 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.3-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.3-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.3-debug-single-dotnet` | 4.3-stable (official) | yes | 22/22 † | 22 pass · 0 fail · 1 n/a | 1 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.3-debug-single-gdscript` | 4.3-stable (official) | yes | 20/21 | 20 pass · 1 fail · 2 n/a | 2 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.3-debug-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.3-debug-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.4.1-release-single-dotnet` | 4.4.1-stable (official) | yes | 22/22 † | 22 pass · 0 fail · 1 n/a | 1 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.4.1-release-single-gdscript` | 4.4.1-stable (official) | yes | 20/21 | 20 pass · 1 fail · 2 n/a | 2 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.4-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.4-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.4.1-debug-single-dotnet` | 4.4.1-stable (official) | yes | 22/22 † | 22 pass · 0 fail · 1 n/a | 1 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.4.1-debug-single-gdscript` | 4.4.1-stable (official) | yes | 20/21 | 20 pass · 1 fail · 2 n/a | 2 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.4-debug-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.4-debug-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.5-release-single-gdscript` | 4.5-stable (official) | yes | **22/22** | 22 pass · 0 fail · 1 n/a | 1 n/a |
| `4.5-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.5-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.5-debug-single-dotnet` | 4.5-stable (official) | yes | **23/23** | 23 pass · 0 fail · 0 n/a | — |
| `4.5-debug-single-gdscript` | 4.5-stable (official) | yes | **22/22** | 22 pass · 0 fail · 1 n/a | 1 n/a |
| `4.5-debug-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.5-debug-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.6.3-release-single-dotnet` | 4.6.3-stable (official) | yes | 22/22 † | 22 pass · 0 fail · 1 n/a | 1 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.6.3-release-single-gdscript` | 4.6.3-stable (official) | yes | 21/21 † | 21 pass · 0 fail · 2 n/a | 2 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.6-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.6-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.6.3-debug-single-dotnet` | 4.6.3-stable (official) | yes | 22/22 † | 22 pass · 0 fail · 1 n/a | 1 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.6.3-debug-single-gdscript` | 4.6.3-stable (official) | yes | 21/21 † | 21 pass · 0 fail · 2 n/a | 2 n/a; **offsets uncorroborated** (no shipped profile; internal consistency only) |
| `4.6-debug-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.6-debug-double-gdscript` | — | no | `not built` | — | see Gaps |

**16 of 40 cells measured.**

## Cross-cell assertions

Contradictions no single cell can see. A calibrator can be perfectly self-consistent within one
cell and disagree with the cell next to it — and on cells no shipped profile covers, this and
`offsets.internal_consistency` are the only things standing between a derived offset and nothing
at all. Neither is corroboration by an independent SOURCE; both cells come from one calibrator.

| Check | Status | Detail |
| --- | --- | --- |
| `grid.binding_invariance` | PASS | 100 offset(s) agree across bindings in 8 group(s): 4.5-release-single (dotnet/gdscript, 13 shared key(s)); 4.3-release-single (dotnet/gdscript, 13 shared key(s)); 4.3-debug-single (dotnet/gdscript, 12 shared key(s)); 4.4.1-release-single (dotnet/gdscript, 11 shared key(s)); 4.4.1-debug-single (dotnet/gdscript, 12 shared key(s)); 4.5-debug-single (dotnet/gdscript, 13 shared key(s)); 4.6.3-release-single (dotnet/gdscript, 13 shared key(s)); 4.6.3-debug-single (dotnet/gdscript, 13 shared key(s)). This is corroboration by an independent RUN, not by an answer key — but two cells derived by the same |
| `grid.debug_release_delta` | PASS | 85 key(s) across 8 release/debug pair(s) all sit exactly 0x8 apart: 4.5-single-dotnet: 11 shared key(s), delta 0x8; 4.3-single-dotnet: 11 shared key(s), delta 0x8; 4.3-single-gdscript: 10 shared key(s), delta 0x8; 4.4.1-single-dotnet: 11 shared key(s), delta 0x8; 4.4.1-single-gdscript: 9 shared key(s), delta 0x8; 4.5-single-gdscript: 11 shared key(s), delta 0x8; 4.6.3-single-dotnet: 11 shared key(s), delta 0x8; 4.6.3-single-gdscript: 11 shared key(s), delta 0x8 |

## Per-cell check detail

### `4.5-release-single-dotnet` — reference cell

Engine: `4.5-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `godot-4.5.x-release-single-x64`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x8, derived from 2 name(s) of different lengths over 6 buffer(s) (the 4.2-4.5 header: refcount, size, data).
- walk root 0x15466f3c9e0 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1c0 and node.parent 0x128 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x148/next 0x0, head 0x150/next 0x8. Taking head 0x148 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x15467034010, 0x15466ff0a00
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0xff8 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x15466f55450).
- richTextLabel.text: 0x7f8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x15466f55c50, 0x15466fe24e0).
- richTextLabel.text: 0x800 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x15466f55c50, 0x15466fe24e0).
- richTextLabel.text: 0x828 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x15466f55c50, 0x15466fe24e0).
- richTextLabel.text: 0x858 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x15466f55c50, 0x15466fe24e0).
- richTextLabel.text: 0xff8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x15466f55450).
- richTextLabel.text: 0x1000 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x15466f55450).
- richTextLabel.text: 0x1028 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x15466f55450).
- richTextLabel.text: 0x1058 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x15466f55450).
- richTextLabel.text: 0x1278 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x15466ff1a00).
- richTextLabel.text: 0x1398 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x15466fe24e0).
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.
- getter-disassembly cross-check: ran; 7 of 8 probed field(s) corroborated by name in 1085 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.5-stable (official) release/single/dotnet; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x148, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x128 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 4 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x4c0, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x4b8, 25 samples, 23/23 nodes exact |
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
| `profile.agreement` | PASS | 15 of 17 key(s) in godot-4.5.x-release-single-x64 compared and matching (trust=verified)<br>    not compared: control.globalPosition (a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.); control.anchor (no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the con |
| `offsets.corroboration` | PASS | 7 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 17 candidates, 911 classes walked<br>    corroborated: canvasItem.visible=0x370 (CanvasItem::is_visible @ RVA 0x13c0da0); control.size=0x4c0 (Control::get_size @ RVA 0x151be30); control.position=0x4b8 (Control::get_position @ RVA 0x151be00); control.scale=0x4a8 (Control::get_scale @ RVA 0x151be60); node.parent=0x128 (Node::get_parent @ RVA 0x13f7460); label.text=0x7f8 (Label::get_text @ RVA 0x15f2a30); richTextLabel.text=0xa78 (RichTextLabel::get_text @ RVA 0x168 |
| `bridge.managed` | PASS | Probe.Instance -> NativePtr 0x15466f3c9e0 == walk root, reverse ScriptInstance chain verified (owner backref + GCHandle), 6/6 managed field value(s) exact |

### `4.3-release-single-dotnet`

Engine: `4.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x8, derived from 2 name(s) of different lengths over 6 buffer(s) (the 4.2-4.5 header: refcount, size, data).
- walk root 0x2e8560affc0 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1d0 and node.parent 0xd0 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x150/next 0x0, head 0x158/next 0x8. Taking head 0x150 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 4 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x2e85617f900, 0x2e856182eb0, 0x2e85615ae30, 0x2e856146b70
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x1320 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x2e8560b3b30).
- richTextLabel.text: 0x8f0 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2e8560b4560, 0x2e856130f70).
- richTextLabel.text: 0x8f8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2e8560b4560, 0x2e856130f70).
- richTextLabel.text: 0x918 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2e8560b4560, 0x2e856130f70).
- richTextLabel.text: 0xb68 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2e8560b4560).
- richTextLabel.text: 0x1320 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2e8560b3b30).
- richTextLabel.text: 0x1328 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2e8560b3b30).
- richTextLabel.text: 0x1348 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2e8560b3b30).
- richTextLabel.text: 0x1390 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2e85615d200).
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.
- getter-disassembly cross-check: ran; 5 of 8 probed field(s) corroborated by name in 1088 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.3-stable (official) release/single/dotnet; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x150, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x128 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 4 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x520, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x518, 23 samples, 23/23 nodes exact |
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
| `offsets.corroboration` | PASS | 5 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 10 candidates, 872 classes walked<br>    corroborated: canvasItem.visible=0x418 (CanvasItem::is_visible @ RVA 0xf2ea40); control.size=0x520 (Control::get_size @ RVA 0x10f9f60); control.position=0x518 (Control::get_position @ RVA 0x10faf30); control.scale=0x508 (Control::get_scale @ RVA 0x11567c0); node.parent=0x128 (Node::get_parent @ RVA 0xf921e0)<br>    not compared: node.name=noOpinion (Node::get_name); label.text=noOpinion (Label::get_text); richTextLabel.tex |
| `bridge.managed` | PASS | Probe.Instance -> NativePtr 0x2e8560affc0 == walk root, reverse ScriptInstance chain verified (owner backref + GCHandle), 6/6 managed field value(s) exact |

### `4.3-release-single-gdscript`

Engine: `4.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x8, derived from 2 name(s) of different lengths over 2 buffer(s) (the 4.2-4.5 header: refcount, size, data).
- walk root 0x2a25a404630 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1d0 and node.parent 0x128 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x150/next 0x0, head 0x158/next 0x8. Taking head 0x150 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 3 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x2a25a3c6670, 0x2a25a47e6b0, 0x2a25a3fe170
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x11e0 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x2a25a3bffe0).
- richTextLabel.text: 0x8f0 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2a25a3c08d0, 0x2a25a3c1d80).
- richTextLabel.text: 0x8f8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2a25a3c08d0, 0x2a25a3c1d80).
- richTextLabel.text: 0x918 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2a25a3c08d0, 0x2a25a3c1d80).
- richTextLabel.text: 0x928 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2a25a3ef460).
- richTextLabel.text: 0xb68 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2a25a3c1d80).
- richTextLabel.text: 0xcc8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2a25a3ef0c0).
- richTextLabel.text: 0x1048 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2a25a404f20).
- richTextLabel.text: 0x1060 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2a25a404f20).
- richTextLabel.text: 0x1100 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2a25a404f20).
- richTextLabel.text: 0x11e0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2a25a3bffe0).
- richTextLabel.text: 0x11e8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2a25a3bffe0).
- richTextLabel.text: 0x1208 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2a25a3bffe0).
- richTextLabel.text: 0x1390 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2a25a3c47b0).
- scriptInstance.gcHandle: not derived — this target's scripts are not .NET, and a GDScript ScriptInstance carries no GCHandle to locate
- getter-disassembly cross-check: ran; 5 of 8 probed field(s) corroborated by name in 945 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.3-stable (official) release/single/gdscript; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x150, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x128 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 3 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x520, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x518, 24 samples, 23/23 nodes exact |
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
| `offsets.corroboration` | PASS | 5 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 10 candidates, 869 classes walked<br>    corroborated: canvasItem.visible=0x418 (CanvasItem::is_visible @ RVA 0xf443e0); control.size=0x520 (Control::get_size @ RVA 0x11177c0); control.position=0x518 (Control::get_position @ RVA 0x1118550); control.scale=0x508 (Control::get_scale @ RVA 0x11185e0); node.parent=0x128 (Node::get_parent @ RVA 0xfa6e00)<br>    not compared: node.name=noOpinion (Node::get_name); label.text=noOpinion (Label::get_text); richTextLabel.tex |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.3-debug-single-dotnet`

Engine: `4.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x8, derived from 2 name(s) of different lengths over 6 buffer(s) (the 4.2-4.5 header: refcount, size, data).
- walk root 0x26c750d59f0 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1d8 and node.parent 0x130 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x158/next 0x0, head 0x160/next 0x8. Taking head 0x158 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 4 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x26c7517b8a0, 0x26c751b4650, 0x26c7518f050, 0x26c750f53b0
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x1208 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x26c750e8ef0).
- richTextLabel.text: 0x560 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x26c750f53b0).
- richTextLabel.text: 0x8f8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x26c750e9800, 0x26c75164630).
- richTextLabel.text: 0x900 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x26c750e9800, 0x26c75164630).
- richTextLabel.text: 0x920 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x26c750e9800, 0x26c75164630).
- richTextLabel.text: 0x1208 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x26c750e8ef0).
- richTextLabel.text: 0x1210 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x26c750e8ef0).
- richTextLabel.text: 0x1230 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x26c750e8ef0).
- richTextLabel.text: 0x13b8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x26c751914a0).
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.
- getter-disassembly cross-check: ran; 1 of 8 probed field(s) corroborated by name in 954 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.3-stable (official) debug/single/dotnet; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x158, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x130 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 4 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x528, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x520, 23 samples, 23/23 nodes exact |
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
| `offsets.corroboration` | PASS | 1 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 10 candidates, 872 classes walked<br>    corroborated: node.parent=0x130 (Node::get_parent @ RVA 0xf50560)<br>    not compared: canvasItem.visible=noOpinion (CanvasItem::is_visible); control.size=noOpinion (Control::get_size); control.position=noOpinion (Control::get_position); control.scale=noOpinion (Control::get_scale); node.name=noOpinion (Node::get_name); label.text=noOpinion (Label::get_text); richTextLabel.text=noOpinion (RichTextLabel::get_text) |
| `bridge.managed` | PASS | Probe.Instance -> NativePtr 0x26c750d59f0 == walk root, reverse ScriptInstance chain verified (owner backref + GCHandle), 6/6 managed field value(s) exact |

### `4.3-debug-single-gdscript`

Engine: `4.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x8, derived from 2 name(s) of different lengths over 2 buffer(s) (the 4.2-4.5 header: refcount, size, data).
- walk root 0x27884ed5d40 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1d8 and node.parent 0x130 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x158/next 0x0, head 0x160/next 0x8. Taking head 0x158 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x27884f48d10, 0x27884f28460
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- canvasItem.visible: not derived — 3 candidates survived (0x300, 0x420, 0x480); another sample with a different expected value is needed
- canvasItem.visible: every nominated byte was eliminated. Which rule did it, per candidate — 0x1b9 rejected: at or below the derived Node members (0x1d8), so it cannot be a CanvasItem field; 0x1ba rejected: at or below the derived Node members (0x1d8), so it cannot be a CanvasItem field; 0x1f7 rejected on the visible twin: not 8-aligned; CanvasItem::visible always is; 0x1f8 rejected on the visible twin: visible / parent_visible_in_tree are not boolean-valued here; 0x1f9 rejected on the visible twin: not 8-aligned; CanvasItem::visible always is; 0x1fa rejected on the visible twin: not 8-aligned;
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x1208 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x27884ef0710).
- richTextLabel.text: 0x7f0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884ef3a40).
- richTextLabel.text: 0x8f8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x27884ef1020, 0x27884ef1a30).
- richTextLabel.text: 0x900 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x27884ef1020, 0x27884ef1a30).
- richTextLabel.text: 0x920 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x27884ef1020, 0x27884ef1a30).
- richTextLabel.text: 0x1050 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884ef3e00).
- richTextLabel.text: 0x1138 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884ed5d40).
- richTextLabel.text: 0x1200 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884ef3030).
- richTextLabel.text: 0x1208 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884ef0710).
- richTextLabel.text: 0x1210 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884ef0710).
- richTextLabel.text: 0x1230 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884ef0710).
- richTextLabel.text: 0x1258 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884f422d0).
- richTextLabel.text: 0x1260 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884f422d0).
- richTextLabel.text: 0x1308 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884ef1020).
- richTextLabel.text: 0x1310 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884ef1020).
- richTextLabel.text: 0x1330 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884ef1020).
- richTextLabel.text: 0x13b8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884f2a8b0).
- richTextLabel.text: 0x13d8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884ed5d40).
- richTextLabel.text: 0x13f8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27884ed5d40).
- scriptInstance.gcHandle: not derived — this target's scripts are not .NET, and a GDScript ScriptInstance carries no GCHandle to locate
- getter-disassembly cross-check: ran; 1 of 8 probed field(s) corroborated by name in 919 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.3-stable (official) debug/single/gdscript; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x158, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x130 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 10 offset(s) across 5 class band(s) (Object < Node < Control < Label < RichTextLabel) + 3 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent source (the s |
| `semantic.size` | PASS | control.size 0x528, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x520, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x510, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x4e0, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | FAIL | the driver reported per-key sample counts but none for "canvasItem.visible" (it has: node.parent, node.childListHead, node.scriptInstance, control.size, control.offset, control.position, control.scale, node.name, label.text, richTextLabel.text). §12.5: one control gave four candidate offsets, so "how many samples "were intersected" is the precondition for reading anything into this offset at all. A count the harness cannot read is not a count. |
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
| `offsets.corroboration` | PASS | 1 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 10 candidates, 869 classes walked<br>    corroborated: node.parent=0x130 (Node::get_parent @ RVA 0xf27960)<br>    not compared: canvasItem.visible=noOpinion (CanvasItem::is_visible); control.size=noOpinion (Control::get_size); control.position=noOpinion (Control::get_position); control.scale=noOpinion (Control::get_scale); node.name=noOpinion (Node::get_name); label.text=noOpinion (Label::get_text); richTextLabel.text=noOpinion (RichTextLabel::get_text) |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.4.1-release-single-dotnet`

Engine: `4.4.1-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x8, derived from 2 name(s) of different lengths over 6 buffer(s) (the 4.2-4.5 header: refcount, size, data).
- walk root 0x20ed96b6260 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1e0 and node.parent 0x138 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x160/next 0x0, head 0x168/next 0x8. Taking head 0x160 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x20ed98352a0, 0x20ed98316c0
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x12d8 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x20ed96c9df0).
- richTextLabel.text: 0x968 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x20ed96ca760, 0x20ed96d3810).
- richTextLabel.text: 0x970 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x20ed96ca760, 0x20ed96d3810).
- richTextLabel.text: 0x990 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x20ed96ca760, 0x20ed96d3810).
- richTextLabel.text: 0x9c0 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x20ed96ca760, 0x20ed96d3810).
- richTextLabel.text: 0x12d8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x20ed96c9df0).
- richTextLabel.text: 0x12e0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x20ed96c9df0).
- richTextLabel.text: 0x1300 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x20ed96c9df0).
- richTextLabel.text: 0x1330 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x20ed96c9df0).
- richTextLabel.text: 0x13e8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x20ed98352a0).
- richTextLabel.text: 0x13f0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x20ed98352a0).
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.
- getter-disassembly cross-check: ran; 7 of 8 probed field(s) corroborated by name in 945 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.4.1-stable (official) release/single/dotnet; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x160, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x138 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 4 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x590, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x588, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x578, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x548, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x428, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x1e0, 27/27 StringNames exact against their position in the child lists (including 2 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 23/23 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 27/27 nodes walked (25 authored + 2 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.4.1-release-single-dotnet — nothing to cross-check against, and nothing to fall back to |
| `offsets.corroboration` | PASS | 7 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 10 candidates, 895 classes walked<br>    corroborated: canvasItem.visible=0x428 (CanvasItem::is_visible @ RVA 0x14537a0); control.size=0x590 (Control::get_size @ RVA 0x15952b0); control.position=0x588 (Control::get_position @ RVA 0x1595280); control.scale=0x578 (Control::get_scale @ RVA 0x15952e0); node.parent=0x138 (Node::get_parent @ RVA 0x1486d80); label.text=0x968 (Label::get_text @ RVA 0x1644260); richTextLabel.text=0xb50 (RichTextLabel::get_text @ RVA 0x16c |
| `bridge.managed` | PASS | Probe.Instance -> NativePtr 0x20ed96b6260 == walk root, reverse ScriptInstance chain verified (owner backref + GCHandle), 6/6 managed field value(s) exact |

### `4.4.1-release-single-gdscript`

Engine: `4.4.1-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x8, derived from 2 name(s) of different lengths over 2 buffer(s) (the 4.2-4.5 header: refcount, size, data).
- walk root 0x2cbbebb3070 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1e0 and node.parent 0x138 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x160/next 0x0, head 0x168/next 0x8. Taking head 0x160 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- scriptInstance.class: the ScriptInstance at node + 0xba0 did not resolve a class name through its vtable after three attempts, so scriptInstance.ownerBackref cannot be scoped to an implementing class and any cross-check of it will report 'not compared' rather than pass.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x2cbbeba5c20, 0x2cbbebc6740
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- canvasItem.visible: not derived — no offset satisfied every sample
- canvasItem.visible: every nominated byte was eliminated. Which rule did it, per candidate — 0xf1 rejected: at or below the derived Node members (0xba0), so it cannot be a CanvasItem field; 0xf2 rejected: at or below the derived Node members (0xba0), so it cannot be a CanvasItem field; 0x119 rejected: at or below the derived Node members (0xba0), so it cannot be a CanvasItem field; 0x11a rejected: at or below the derived Node members (0xba0), so it cannot be a CanvasItem field; 0x147 rejected: at or below the derived Node members (0xba0), so it cannot be a CanvasItem field; 0x148 rejected: at o
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- node.scriptInstance: derived 0xba0, which is at or above the lowest member of a class derived from the one it belongs to (0x138). A base class is laid out before the classes derived from it, so that is structurally impossible and the offset is withdrawn rather than reported.
- label.text: 0x12d8 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x2cbbeb0bb40).
- richTextLabel.text: 0x810 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbebb3070).
- richTextLabel.text: 0x938 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbeb0bb40).
- richTextLabel.text: 0x960 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbebad350).
- richTextLabel.text: 0x968 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2cbbeb0c4b0, 0x2cbbeb074f0).
- richTextLabel.text: 0x970 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2cbbeb0c4b0, 0x2cbbeb074f0).
- richTextLabel.text: 0x990 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2cbbeb0c4b0, 0x2cbbeb074f0).
- richTextLabel.text: 0x9c0 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2cbbeb0c4b0, 0x2cbbeb074f0).
- richTextLabel.text: 0xab8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbebad350).
- richTextLabel.text: 0xde8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbebc83a0).
- richTextLabel.text: 0xe00 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbebc83a0).
- richTextLabel.text: 0xfc8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbebb39e0).
- richTextLabel.text: 0xfe0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbebb39e0).
- richTextLabel.text: 0x1040 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbeb08c00).
- richTextLabel.text: 0x12a8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbeb0b1d0).
- richTextLabel.text: 0x12d8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbeb0bb40).
- richTextLabel.text: 0x12e0 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2cbbebac9d0, 0x2cbbeb0bb40).
- richTextLabel.text: 0x1300 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbeb0bb40).
- richTextLabel.text: 0x1330 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2cbbeb0bb40).
- scriptInstance.gcHandle: not derived — this target's scripts are not .NET, and a GDScript ScriptInstance carries no GCHandle to locate
- getter-disassembly cross-check: ran; 6 of 8 probed field(s) corroborated by name in 999 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.4.1-stable (official) release/single/gdscript; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x160, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x138 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 9 offset(s) across 4 class band(s) (Node < Control < Label < RichTextLabel) + 3 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent source (the shipped<br> |
| `semantic.size` | PASS | control.size 0x590, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x588, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x578, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x548, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | FAIL | the driver reported per-key sample counts but none for "canvasItem.visible" (it has: node.parent, node.childListHead, control.size, control.offset, control.position, control.scale, node.name, label.text, richTextLabel.text). §12.5: one control gave four candidate offsets, so "how many samples "were intersected" is the precondition for reading anything into this offset at all. A count the harness cannot read is not a count. |
| `strings.names` | PASS | node.name 0x1e0, 27/27 StringNames exact against their position in the child lists (including 2 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 23/23 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 27/27 nodes walked (25 authored + 2 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.4.1-release-single-gdscript — nothing to cross-check against, and nothing to fall back to |
| `offsets.corroboration` | PASS | 6 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 10 candidates, 892 classes walked<br>    corroborated: control.size=0x590 (Control::get_size @ RVA 0x1572d30); control.position=0x588 (Control::get_position @ RVA 0x1572d00); control.scale=0x578 (Control::get_scale @ RVA 0x1572d60); node.parent=0x138 (Node::get_parent @ RVA 0x1464800); label.text=0x968 (Label::get_text @ RVA 0x1621ce0); richTextLabel.text=0xb50 (RichTextLabel::get_text @ RVA 0x16a7300)<br>    not compared: canvasItem.visible=noOpinion (CanvasItem |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.4.1-debug-single-dotnet`

Engine: `4.4.1-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x8, derived from 2 name(s) of different lengths over 6 buffer(s) (the 4.2-4.5 header: refcount, size, data).
- walk root 0x24a67e3af40 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1e8 and node.parent 0x140 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x168/next 0x0, head 0x170/next 0x8. Taking head 0x168 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x24a67ee9a00, 0x24a67f3c6e0
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x12f0 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x24a67e4f940).
- richTextLabel.text: 0x970 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x24a67e502c0, 0x24a67e59e50).
- richTextLabel.text: 0x978 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x24a67e502c0, 0x24a67e59e50).
- richTextLabel.text: 0x998 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x24a67e502c0, 0x24a67e59e50).
- richTextLabel.text: 0x9c8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x24a67e502c0, 0x24a67e59e50).
- richTextLabel.text: 0x12f0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24a67e4f940).
- richTextLabel.text: 0x12f8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24a67e4f940).
- richTextLabel.text: 0x1318 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24a67e4f940).
- richTextLabel.text: 0x1348 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24a67e4f940).
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.
- getter-disassembly cross-check: ran; 3 of 8 probed field(s) corroborated by name in 1111 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.4.1-stable (official) debug/single/dotnet; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x168, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x140 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 4 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x598, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x590, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x580, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x550, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x430, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x1e8, 27/27 StringNames exact against their position in the child lists (including 2 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 23/23 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 27/27 nodes walked (25 authored + 2 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.4.1-debug-single-dotnet — nothing to cross-check against, and nothing to fall back to |
| `offsets.corroboration` | PASS | 3 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 10 candidates, 895 classes walked<br>    corroborated: node.parent=0x140 (Node::get_parent @ RVA 0x11fd520); label.text=0x970 (Label::get_text @ RVA 0x13d93d0); richTextLabel.text=0xb58 (RichTextLabel::get_text @ RVA 0x145c4d0)<br>    not compared: canvasItem.visible=noOpinion (CanvasItem::is_visible); control.size=noOpinion (Control::get_size); control.position=noOpinion (Control::get_position); control.scale=noOpinion (Control::get_scale); node.name=noOpinion ( |
| `bridge.managed` | PASS | Probe.Instance -> NativePtr 0x24a67e3af40 == walk root, reverse ScriptInstance chain verified (owner backref + GCHandle), 6/6 managed field value(s) exact |

### `4.4.1-debug-single-gdscript`

Engine: `4.4.1-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x8, derived from 2 name(s) of different lengths over 2 buffer(s) (the 4.2-4.5 header: refcount, size, data).
- walk root 0x26537b7ae90 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1e8 and node.parent 0x140 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x168/next 0x0, head 0x170/next 0x8. Taking head 0x168 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x26537b96430, 0x26537bca4d0
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- canvasItem.visible: not derived — 2 candidates survived (0x310, 0x430); another sample with a different expected value is needed
- canvasItem.visible: every nominated byte was eliminated. Which rule did it, per candidate — 0x311 rejected on the visible twin: not 8-aligned; CanvasItem::visible always is; 0x42f rejected on the visible twin: not 8-aligned; CanvasItem::visible always is
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x12f0 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x26537bceda0).
- richTextLabel.text: 0x970 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x26537bcf720, 0x26537bd01b0).
- richTextLabel.text: 0x978 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x26537bcf720, 0x26537bd01b0).
- richTextLabel.text: 0x998 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x26537bcf720, 0x26537bd01b0).
- richTextLabel.text: 0x9c8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x26537bcf720, 0x26537bd01b0).
- richTextLabel.text: 0x1148 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x26537b7ae90).
- richTextLabel.text: 0x1278 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x26537be5fb0).
- richTextLabel.text: 0x12f0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x26537bceda0).
- richTextLabel.text: 0x12f8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x26537bceda0).
- richTextLabel.text: 0x1318 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x26537bceda0).
- richTextLabel.text: 0x1348 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x26537bceda0).
- richTextLabel.text: 0x1378 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x26537be5fb0).
- scriptInstance.gcHandle: not derived — this target's scripts are not .NET, and a GDScript ScriptInstance carries no GCHandle to locate
- getter-disassembly cross-check: ran; 3 of 8 probed field(s) corroborated by name in 1019 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.4.1-stable (official) debug/single/gdscript; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x168, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x140 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 10 offset(s) across 5 class band(s) (Object < Node < Control < Label < RichTextLabel) + 3 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent source (the s |
| `semantic.size` | PASS | control.size 0x598, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x590, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x580, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x550, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | FAIL | the driver reported per-key sample counts but none for "canvasItem.visible" (it has: node.parent, node.childListHead, node.scriptInstance, control.size, control.offset, control.position, control.scale, node.name, label.text, richTextLabel.text). §12.5: one control gave four candidate offsets, so "how many samples "were intersected" is the precondition for reading anything into this offset at all. A count the harness cannot read is not a count. |
| `strings.names` | PASS | node.name 0x1e8, 27/27 StringNames exact against their position in the child lists (including 2 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 23/23 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 27/27 nodes walked (25 authored + 2 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.4.1-debug-single-gdscript — nothing to cross-check against, and nothing to fall back to |
| `offsets.corroboration` | PASS | 3 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 10 candidates, 892 classes walked<br>    corroborated: node.parent=0x140 (Node::get_parent @ RVA 0x11d8060); label.text=0x970 (Label::get_text @ RVA 0x13b3f10); richTextLabel.text=0xb58 (RichTextLabel::get_text @ RVA 0x1437010)<br>    not compared: canvasItem.visible=noOpinion (CanvasItem::is_visible); control.size=noOpinion (Control::get_size); control.position=noOpinion (Control::get_position); control.scale=noOpinion (Control::get_scale); node.name=noOpinion ( |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.5-release-single-gdscript`

Engine: `4.5-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `godot-4.5.x-release-single-x64`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x8, derived from 2 name(s) of different lengths over 2 buffer(s) (the 4.2-4.5 header: refcount, size, data).
- walk root 0x27a558cb5b0 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1c0 and node.parent 0x128 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x148/next 0x0, head 0x150/next 0x8. Taking head 0x148 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x27a55a16f20, 0x27a55935100
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0xff8 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x27a55944550).
- richTextLabel.text: 0x7f8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x27a55944d50, 0x27a5594b590).
- richTextLabel.text: 0x800 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x27a55944d50, 0x27a5594b590).
- richTextLabel.text: 0x828 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x27a55944d50, 0x27a5594b590).
- richTextLabel.text: 0x858 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x27a55944d50, 0x27a5594b590).
- richTextLabel.text: 0xff8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27a55944550).
- richTextLabel.text: 0x1000 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27a55944550).
- richTextLabel.text: 0x1028 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27a55944550).
- richTextLabel.text: 0x1058 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27a55944550).
- richTextLabel.text: 0x1278 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x27a55936100).
- scriptInstance.gcHandle: not derived — this target's scripts are not .NET, and a GDScript ScriptInstance carries no GCHandle to locate
- getter-disassembly cross-check: ran; 7 of 8 probed field(s) corroborated by name in 958 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.5-stable (official) release/single/gdscript; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x148, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x128 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 3 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x4c0, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x4b8, 25 samples, 23/23 nodes exact |
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
| `offsets.corroboration` | PASS | 7 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 17 candidates, 908 classes walked<br>    corroborated: canvasItem.visible=0x370 (CanvasItem::is_visible @ RVA 0x139f520); control.size=0x4c0 (Control::get_size @ RVA 0x14fa5b0); control.position=0x4b8 (Control::get_position @ RVA 0x14fa580); control.scale=0x4a8 (Control::get_scale @ RVA 0x14fa5e0); node.parent=0x128 (Node::get_parent @ RVA 0x13d5be0); label.text=0x7f8 (Label::get_text @ RVA 0x15d11b0); richTextLabel.text=0xa78 (RichTextLabel::get_text @ RVA 0x166 |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.5-debug-single-dotnet`

Engine: `4.5-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `godot-4.5.x-debug-single-x64`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x8, derived from 2 name(s) of different lengths over 6 buffer(s) (the 4.2-4.5 header: refcount, size, data).
- walk root 0x268c3492c40 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1c8 and node.parent 0x130 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x150/next 0x0, head 0x158/next 0x8. Taking head 0x150 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 3 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x268c354be00, 0x268c354c110, 0x268c35479d0
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x1010 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x268c34a6a90).
- richTextLabel.text: 0x800 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x268c34a72a0, 0x268c353bdb0).
- richTextLabel.text: 0x808 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x268c34a72a0, 0x268c353bdb0).
- richTextLabel.text: 0x830 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x268c34a72a0, 0x268c353bdb0).
- richTextLabel.text: 0x860 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x268c34a72a0, 0x268c353bdb0).
- richTextLabel.text: 0x968 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268c34a72a0).
- richTextLabel.text: 0xaa0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268c35b5500).
- richTextLabel.text: 0xed8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268c353bdb0).
- richTextLabel.text: 0xee0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268c353bdb0).
- richTextLabel.text: 0x1010 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268c34a6a90).
- richTextLabel.text: 0x1018 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268c34a6a90).
- richTextLabel.text: 0x1040 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268c34a6a90).
- richTextLabel.text: 0x1070 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268c34a6a90).
- richTextLabel.text: 0x1178 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268c34a6a90).
- richTextLabel.text: 0x1290 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x268c354aa00).
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.
- getter-disassembly cross-check: ran; 3 of 8 probed field(s) corroborated by name in 1094 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.5-stable (official) debug/single/dotnet; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x150, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x130 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 4 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x4c8, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x4c0, 24 samples, 23/23 nodes exact |
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
| `offsets.corroboration` | PASS | 3 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 17 candidates, 911 classes walked<br>    corroborated: node.parent=0x130 (Node::get_parent @ RVA 0x11186a0); label.text=0x800 (Label::get_text @ RVA 0x13316d0); richTextLabel.text=0xa80 (RichTextLabel::get_text @ RVA 0x13c66d0)<br>    not compared: canvasItem.visible=noOpinion (CanvasItem::is_visible); control.size=noOpinion (Control::get_size); control.position=noOpinion (Control::get_position); control.scale=noOpinion (Control::get_scale); node.name=noOpinion ( |
| `bridge.managed` | PASS | Probe.Instance -> NativePtr 0x268c3492c40 == walk root, reverse ScriptInstance chain verified (owner backref + GCHandle), 6/6 managed field value(s) exact |

### `4.5-debug-single-gdscript`

Engine: `4.5-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `godot-4.5.x-debug-single-x64`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x8, derived from 2 name(s) of different lengths over 2 buffer(s) (the 4.2-4.5 header: refcount, size, data).
- walk root 0x1c27877ca00 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1c8 and node.parent 0x130 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x150/next 0x0, head 0x158/next 0x8. Taking head 0x150 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 27 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x1c278840920, 0x1c2787f4320
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0x1010 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x1c2787fe9b0).
- richTextLabel.text: 0x800 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1c2787ff1c0, 0x1c2787f0b00).
- richTextLabel.text: 0x808 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1c2787ff1c0, 0x1c2787f0b00).
- richTextLabel.text: 0x830 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1c2787ff1c0, 0x1c2787f0b00).
- richTextLabel.text: 0x860 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1c2787ff1c0, 0x1c2787f0b00).
- richTextLabel.text: 0x9b0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c2787f5340).
- richTextLabel.text: 0xa28 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c2787f5340).
- richTextLabel.text: 0xa68 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c27877ca00).
- richTextLabel.text: 0xd90 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c27879a770).
- richTextLabel.text: 0x1010 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c2787fe9b0).
- richTextLabel.text: 0x1018 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c2787fe9b0).
- richTextLabel.text: 0x1040 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c2787fe9b0).
- richTextLabel.text: 0x1070 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c2787fe9b0).
- richTextLabel.text: 0x11c0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c2787f4b30).
- richTextLabel.text: 0x1220 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c27879a770).
- richTextLabel.text: 0x1238 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c2787f4b30).
- scriptInstance.gcHandle: not derived — this target's scripts are not .NET, and a GDScript ScriptInstance carries no GCHandle to locate
- getter-disassembly cross-check: ran; 3 of 8 probed field(s) corroborated by name in 935 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.5-stable (official) debug/single/gdscript; raw tree 27 nodes = 25 authored + 2 engine-internal (@VScrollBar@2, @VScrollBar@3) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x150, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x130 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 3 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
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
| `profile.agreement` | PASS | 14 of 17 key(s) in godot-4.5.x-debug-single-x64 compared and matching (trust=measured)<br>    not compared: control.globalPosition (a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.); control.anchor (no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confu |
| `offsets.corroboration` | PASS | 3 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 17 candidates, 908 classes walked<br>    corroborated: node.parent=0x130 (Node::get_parent @ RVA 0x10f82a0); label.text=0x800 (Label::get_text @ RVA 0x13112d0); richTextLabel.text=0xa80 (RichTextLabel::get_text @ RVA 0x13a62d0)<br>    not compared: canvasItem.visible=noOpinion (CanvasItem::is_visible); control.size=noOpinion (Control::get_size); control.position=noOpinion (Control::get_position); control.scale=noOpinion (Control::get_scale); node.name=noOpinion ( |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.6.3-release-single-dotnet`

Engine: `4.6.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x10, derived from 2 name(s) of different lengths over 6 buffer(s) — NOT the pre-4.6 -0x8; Godot 4.6's CowData header carries a capacity field and aligns the payload to Memory::MAX_ALIGN.
- walk root 0x1c382298200 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x190 and node.parent 0xfc0 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x118/next 0x0, head 0x120/next 0x8. Taking head 0x118 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 4 of 29 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x1c38238ff00, 0x1c382383bc0, 0x1c3822aea70, 0x1c3822b0db0
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0xfb8 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x1c3822d75f0).
- richTextLabel.text: 0x7d8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1c3822d7dd0, 0x1c38234fa50).
- richTextLabel.text: 0x7e0 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1c3822d7dd0, 0x1c38234fa50).
- richTextLabel.text: 0x808 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1c3822d7dd0, 0x1c38234fa50).
- richTextLabel.text: 0x838 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x1c3822d7dd0, 0x1c38234fa50).
- richTextLabel.text: 0xfb8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c3822d75f0).
- richTextLabel.text: 0xfc0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c3822d75f0).
- richTextLabel.text: 0xfe8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c3822d75f0).
- richTextLabel.text: 0x1018 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c3822d75f0).
- richTextLabel.text: 0x1270 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x1c382385b50).
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.
- getter-disassembly cross-check: ran; 7 of 8 probed field(s) corroborated by name in 987 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.6.3-stable (official) release/single/dotnet; raw tree 29 nodes = 25 authored + 4 engine-internal (@VScrollBar@2, @Timer@3, @VScrollBar@4, @Timer@5) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x118, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0xf8 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 4 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x4a0, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x498, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x480, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x448, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x348, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x190, 29/29 StringNames exact against their position in the child lists (including 4 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 25/25 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 29/29 nodes walked (25 authored + 4 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.6.3-release-single-dotnet — nothing to cross-check against, and nothing to fall back to |
| `offsets.corroboration` | PASS | 7 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 17 candidates, 962 classes walked<br>    corroborated: canvasItem.visible=0x348 (CanvasItem::is_visible @ RVA 0x14e6560); control.size=0x4a0 (Control::get_size @ RVA 0x1649030); control.position=0x498 (Control::get_position @ RVA 0x1649000); control.scale=0x480 (Control::get_scale @ RVA 0x1649060); node.parent=0xf8 (Node::get_parent @ RVA 0x151c9e0); label.text=0x7d8 (Label::get_text @ RVA 0x172a660); richTextLabel.text=0xa80 (RichTextLabel::get_text @ RVA 0x17c7 |
| `bridge.managed` | PASS | Probe.Instance -> NativePtr 0x1c382298200 == walk root, reverse ScriptInstance chain verified (owner backref + GCHandle), 6/6 managed field value(s) exact |

### `4.6.3-release-single-gdscript`

Engine: `4.6.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x10, derived from 2 name(s) of different lengths over 2 buffer(s) — NOT the pre-4.6 -0x8; Godot 4.6's CowData header carries a capacity field and aligns the payload to Memory::MAX_ALIGN.
- walk root 0x24edd8284c0 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x190 and node.parent 0xf8 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x118/next 0x0, head 0x120/next 0x8. Taking head 0x118 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 3 of 29 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x24edd985e40, 0x24edd94eed0, 0x24edd895f80
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0xfb8 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x24edd888ef0).
- richTextLabel.text: 0x7d8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x24edd8896d0, 0x24edd891fe0).
- richTextLabel.text: 0x7e0 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x24edd8896d0, 0x24edd891fe0).
- richTextLabel.text: 0x808 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x24edd8896d0, 0x24edd891fe0).
- richTextLabel.text: 0x838 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x24edd8896d0, 0x24edd891fe0).
- richTextLabel.text: 0x938 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24edd891fe0).
- richTextLabel.text: 0xfb8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24edd888ef0).
- richTextLabel.text: 0xfc0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24edd888ef0).
- richTextLabel.text: 0xfe8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24edd888ef0).
- richTextLabel.text: 0x1018 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24edd888ef0).
- richTextLabel.text: 0x1078 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24edd8284c0).
- richTextLabel.text: 0x1270 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24edd950e60).
- richTextLabel.text: 0x12b0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24edd8272f0).
- richTextLabel.text: 0x12d8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24edd8284c0).
- richTextLabel.text: 0x12f8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x24edd8284c0).
- scriptInstance.gcHandle: not derived — this target's scripts are not .NET, and a GDScript ScriptInstance carries no GCHandle to locate
- getter-disassembly cross-check: ran; 7 of 8 probed field(s) corroborated by name in 948 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.6.3-stable (official) release/single/gdscript; raw tree 29 nodes = 25 authored + 4 engine-internal (@VScrollBar@2, @Timer@3, @VScrollBar@4, @Timer@5) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x118, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0xf8 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 3 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x4a0, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x498, 26 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x480, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x448, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x348, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x190, 29/29 StringNames exact against their position in the child lists (including 4 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 25/25 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 29/29 nodes walked (25 authored + 4 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.6.3-release-single-gdscript — nothing to cross-check against, and nothing to fall back to |
| `offsets.corroboration` | PASS | 7 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 17 candidates, 959 classes walked<br>    corroborated: canvasItem.visible=0x348 (CanvasItem::is_visible @ RVA 0x14c4060); control.size=0x4a0 (Control::get_size @ RVA 0x1626b30); control.position=0x498 (Control::get_position @ RVA 0x1626b00); control.scale=0x480 (Control::get_scale @ RVA 0x1626b60); node.parent=0xf8 (Node::get_parent @ RVA 0x14fa4e0); label.text=0x7d8 (Label::get_text @ RVA 0x1708160); richTextLabel.text=0xa80 (RichTextLabel::get_text @ RVA 0x17a4 |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.6.3-debug-single-dotnet`

Engine: `4.6.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x10, derived from 2 name(s) of different lengths over 6 buffer(s) — NOT the pre-4.6 -0x8; Godot 4.6's CowData header carries a capacity field and aligns the payload to Memory::MAX_ALIGN.
- walk root 0x2270d222f30 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x198 and node.parent 0x100 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x120/next 0x0, head 0x128/next 0x8. Taking head 0x120 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 2 of 29 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x2270d2adf90, 0x267e2aefe90
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0xfd0 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x2270d227460).
- richTextLabel.text: 0x7e0 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2270d227c50, 0x2270d231760).
- richTextLabel.text: 0x7e8 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2270d227c50, 0x2270d231760).
- richTextLabel.text: 0x810 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2270d227c50, 0x2270d231760).
- richTextLabel.text: 0x840 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x2270d227c50, 0x2270d231760).
- richTextLabel.text: 0x848 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d223720).
- richTextLabel.text: 0x8c8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d223720).
- richTextLabel.text: 0x948 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d223720).
- richTextLabel.text: 0x9c8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d223720).
- richTextLabel.text: 0xa48 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d223720).
- richTextLabel.text: 0xac8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d223720).
- richTextLabel.text: 0xc60 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d223720).
- richTextLabel.text: 0xcc0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d223720).
- richTextLabel.text: 0xd18 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d223720).
- richTextLabel.text: 0xfd0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d227460).
- richTextLabel.text: 0xfd8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d227460).
- richTextLabel.text: 0x1000 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d227460).
- richTextLabel.text: 0x1030 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d227460).
- richTextLabel.text: 0x1038 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d222f30).
- richTextLabel.text: 0x10b8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d222f30).
- richTextLabel.text: 0x1138 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d222f30).
- richTextLabel.text: 0x11b8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d222f30).
- richTextLabel.text: 0x1238 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d222f30).
- richTextLabel.text: 0x1288 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d2a2fa0).
- richTextLabel.text: 0x12b8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x2270d222f30).
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.
- getter-disassembly cross-check: ran; 3 of 8 probed field(s) corroborated by name in 1053 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.6.3-stable (official) debug/single/dotnet; raw tree 29 nodes = 25 authored + 4 engine-internal (@VScrollBar@2, @Timer@3, @VScrollBar@4, @Timer@5) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x120, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x100 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 4 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x4a8, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x4a0, 27 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x488, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x450, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x350, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x198, 29/29 StringNames exact against their position in the child lists (including 4 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 25/25 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 29/29 nodes walked (25 authored + 4 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.6.3-debug-single-dotnet — nothing to cross-check against, and nothing to fall back to |
| `offsets.corroboration` | PASS | 3 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 17 candidates, 962 classes walked<br>    corroborated: node.parent=0x100 (Node::get_parent @ RVA 0x11ac3a0); label.text=0x7e0 (Label::get_text @ RVA 0x13c63e0); richTextLabel.text=0xa88 (RichTextLabel::get_text @ RVA 0x1462340)<br>    not compared: canvasItem.visible=noOpinion (CanvasItem::is_visible); control.size=noOpinion (Control::get_size); control.position=noOpinion (Control::get_position); control.scale=noOpinion (Control::get_scale); node.name=noOpinion ( |
| `bridge.managed` | PASS | Probe.Instance -> NativePtr 0x2270d222f30 == walk root, reverse ScriptInstance chain verified (owner backref + GCHandle), 6/6 managed field value(s) exact |

### `4.6.3-debug-single-gdscript`

Engine: `4.6.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- CowData element count is at buffer-0x10, derived from 2 name(s) of different lengths over 2 buffer(s) — NOT the pre-4.6 -0x8; Godot 4.6's CowData header carries a capacity field and aligns the payload to Memory::MAX_ALIGN.
- walk root 0x204fc534ee0 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x198 and node.parent 0x100 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x120/next 0x0, head 0x128/next 0x8. Taking head 0x120 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- control.position: 4 of 29 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x204fb35dfd0, 0x204fc290050, 0x204fc28d090, 0x204fc28fd50
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- control.anchor: not derived — no route to it that is not a neighbour assertion — anchor[4] sits immediately after offset[4], which is the confusion the grid exists to catch. Solving it from pos = offset + anchor * parent_size and intersecting across the two differently-anchored controls is the honest derivation, and is not yet implemented
- control.globalPosition: not derived — a cached field, not a computed transform — §4.6 settles from the disassembly that the accessor does two float reads and no arithmetic, and §12.3 watched it return [0,0] for controls with real on-screen positions. Global position is composed from local positions up the tree instead, so deriving this offset would only invite reading it.
- label.text: 0xfd0 discarded — it also decodes on 1 node(s) the engine does not call "Label" (0x204fc542220).
- richTextLabel.text: 0x550 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x568 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x570 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x578 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x580 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x588 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x5a0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x5a8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x5c8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x5d8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x5e8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fc53f6b0).
- richTextLabel.text: 0x5f0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x600 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x630 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x638 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x640 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x648 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x660 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x668 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x678 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x680 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x690 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x698 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x6c0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x6c8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x6d0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x6d8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x6e8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x6f8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x708 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x740 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x748 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x768 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x770 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x780 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x788 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x790 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x7b8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x7e0 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x204fc542a10, 0x204fc2d6ef0).
- richTextLabel.text: 0x7e8 discarded — it also decodes on 3 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0, 0x204fc542a10, 0x204fc2d6ef0).
- richTextLabel.text: 0x7f0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x7f8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x808 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x810 discarded — it also decodes on 3 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0, 0x204fc542a10, 0x204fc2d6ef0).
- richTextLabel.text: 0x818 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x828 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x840 discarded — it also decodes on 2 node(s) the engine does not call "RichTextLabel" (0x204fc542a10, 0x204fc2d6ef0).
- richTextLabel.text: 0x848 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x850 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x870 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x888 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x890 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x8a8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x8b8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x8c8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x8e8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x8f8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x900 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x908 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x918 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x950 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x960 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x968 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x978 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x980 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x988 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x990 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x998 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x9b8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x9e8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x9f0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x9f8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0xa40 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fc53f6b0).
- richTextLabel.text: 0xa78 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fc53f6b0).
- richTextLabel.text: 0xae8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fc531df0).
- richTextLabel.text: 0xb88 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0xf08 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fc53f6b0).
- richTextLabel.text: 0xfd0 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fc542220).
- richTextLabel.text: 0xfd8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fc542220).
- richTextLabel.text: 0x1000 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fc542220).
- richTextLabel.text: 0x1030 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fc542220).
- richTextLabel.text: 0x1188 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fb35dfd0).
- richTextLabel.text: 0x11e8 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fc531df0).
- richTextLabel.text: 0x1278 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fc28e070).
- richTextLabel.text: 0x1398 discarded — it also decodes on 1 node(s) the engine does not call "RichTextLabel" (0x204fc53f6b0).
- scriptInstance.gcHandle: not derived — this target's scripts are not .NET, and a GDScript ScriptInstance carries no GCHandle to locate
- getter-disassembly cross-check: ran; 3 of 8 probed field(s) corroborated by name in 922 ms. This is computed live against the target and is NOT read from any table.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.6.3-stable (official) debug/single/gdscript; raw tree 29 nodes = 25 authored + 4 engine-internal (@VScrollBar@2, @Timer@3, @VScrollBar@4, @Timer@5) |
| `calibration.unaided` | PASS | driver states usedProfile=false; no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x120, next 0x0, node 0x18 — 25 nodes, sibling counts 2/3/4/3/2/3/1/5/1 |
| `structural.parent` | PASS | parent 0x100 round-trips against the child list for 24 of 25 nodes (the root has no parent to check) |
| `offsets.internal_consistency` | PASS | 11 offset(s) across 6 class band(s) (Object < Node < CanvasItem < Control < Label < RichTextLabel) + 3 walk offset(s): ordering, single-precision alignment and non-overlap all hold.<br>    WHAT THIS PROVES: the derived numbers are mutually consistent with single inheritance — band<br>    ordering, type alignment and member widths — all read off the structure of the classes, not<br>    off any table of correct values.<br>    WHAT IT DOES NOT PROVE: that any offset is RIGHT. A uniformly shifted or internally coherent<br>    wrong layout satisfies every rule here. Corroboration by an independent  |
| `semantic.size` | PASS | control.size 0x4a8, 6 samples, 23/23 nodes exact |
| `semantic.position` | PASS | control.position 0x4a0, 25 samples, 23/23 nodes exact |
| `semantic.scale` | PASS | control.scale 0x488, 22 samples, 23/23 nodes exact |
| `semantic.offset` | PASS | control.offset 0x450, 23/23 nodes exact, including 2 node(s) with non-zero anchors that separate Data.offset from Data.anchor; NO anchor quad was published on any node, so Data.anchor[4] itself is unchecked here |
| `semantic.visible` | PASS | canvasItem.visible 0x350, 23/23 CanvasItem nodes exact (Hidden/Visible twins separated) |
| `strings.names` | PASS | node.name 0x198, 29/29 StringNames exact against their position in the child lists (including 4 engine-internal child name(s) the authored scene never mentions) |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.richBbcode` | PASS | "[b]Ωmega[/b] ✧ Кириллица 𝔅 BBCode" — 33 codepoints, max U+1D505, includes an astral codepoint (surrogate pair in UTF-16) |
| `strings.text.absent` | PASS | 25/25 walked text-less nodes reported null |
| `strings.text.wrong` | PASS | 4/4 reported string(s) byte-exact (0 withheld) |
| `geometry.absent` | PASS | 2/2 authored non-Control node(s) reported no geometry |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 29/29 nodes walked (25 authored + 4 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.6.3-debug-single-gdscript — nothing to cross-check against, and nothing to fall back to |
| `offsets.corroboration` | PASS | 3 of 8 probed field(s) corroborated by a second live derivation, each with the getter it decoded named; seed "Label" identified 1 of 17 candidates, 959 classes walked<br>    corroborated: node.parent=0x100 (Node::get_parent @ RVA 0x118b360); label.text=0x7e0 (Label::get_text @ RVA 0x13a53a0); richTextLabel.text=0xa88 (RichTextLabel::get_text @ RVA 0x1441300)<br>    not compared: canvasItem.visible=noOpinion (CanvasItem::is_visible); control.size=noOpinion (Control::get_size); control.position=noOpinion (Control::get_position); control.scale=noOpinion (Control::get_scale); node.name=noOpinion ( |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

## Harness self-validation — NOT coverage

`node selftest.mjs` at `2026-08-18T05:49:15.679Z`: **60/60** scenarios.

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
| `offset-group-collision` | `offsets.internal_consistency`, `profile.agreement` | ok |
| `bad-parent` | `structural.child_head`, `structural.parent`, `semantic.size`, `semantic.position`, `semantic.scale`, `semantic.offset`, `semantic.visible`, `structure.walk_count` | ok |
| `corroborate-unnamed-agreement` | `offsets.corroboration` | ok |
| `corroborate-value-on-refusal` | `offsets.corroboration` | ok |
| `corroborate-contradicts-derivation` | `offsets.corroboration` | ok |
| `corroborate-disagreement` | `offsets.corroboration` | ok |
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

`out/availability.json` has not been produced yet — run `pwsh ./build.ps1` (or
`pwsh ./build.ps1 -ListOnly` to see the requirements without building anything).

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
| `offsets.corroboration` | (d) a second, unrelated live derivation corroborates the bracket, BY NAME |
| `bridge.managed` | managed static root -> NativePtr -> walk root, and the field values |

Letters `(a)`–`(e)` map to the five assertions listed in docs/analysis.md §8.9.

