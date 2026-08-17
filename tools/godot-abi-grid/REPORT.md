# Godot ABI grid — measured coverage

<!-- GENERATED FILE. Produced by `node calibrate.mjs --report`. Do not hand-edit: the whole
     point of this table (docs/analysis.md §8.9) is that the numbers in it were measured. -->

- Generated: `2026-08-17T05:37:59.683Z`
- Driver: `dotnet:Godot.External.Calibrator`
- Ground truth: `project/expected.json`, 20 nodes, max depth 7, scene sha256 `c11d70fade7e1fd0`
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
| `4.5-release-single-dotnet` | 4.5-stable (official) | yes | 15/17 | 15 pass · 2 fail · 0 n/a | reference cell |
| `4.2-release-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-release-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.2-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.2-debug-single-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-debug-single-gdscript` | — | no | `not built` | — | see Gaps |
| `4.2-debug-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.2-debug-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.3-release-single-dotnet` | 4.3-stable (official) | yes | 15/16 | 15 pass · 1 fail · 1 n/a | 1 n/a |
| `4.3-release-single-gdscript` | 4.3-stable (official) | yes | 13/15 | 13 pass · 2 fail · 2 n/a | 2 n/a |
| `4.3-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.3-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.3-debug-single-dotnet` | 4.3-stable (official) | yes | 14/16 | 14 pass · 2 fail · 1 n/a | 1 n/a |
| `4.3-debug-single-gdscript` | 4.3-stable (official) | yes | 13/15 | 13 pass · 2 fail · 2 n/a | 2 n/a |
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
| `4.5-release-single-gdscript` | 4.5-stable (official) | yes | 14/16 | 14 pass · 2 fail · 1 n/a | 1 n/a |
| `4.5-release-double-dotnet` | — | no | `not built` | — | see Gaps |
| `4.5-release-double-gdscript` | — | no | `not built` | — | see Gaps |
| `4.5-debug-single-dotnet` | 4.5-stable (official) | yes | 15/17 | 15 pass · 2 fail · 0 n/a | — |
| `4.5-debug-single-gdscript` | 4.5-stable (official) | yes | 13/16 | 13 pass · 3 fail · 1 n/a | 1 n/a |
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

## Per-cell check detail

### `4.5-release-single-dotnet` — reference cell

Engine: `4.5-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `godot-4.5.x-release-single-x64`

<details><summary>driver notes</summary>

- walk root 0x1393c57cdf0 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1c0 and node.parent 0x128 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x148/next 0x0, head 0x150/next 0x8. Taking head 0x148 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- node.scriptInstance: 2 (slot, owner-backref) pairs each fit, all corroborated by the same 1 scripted node(s) — too few to separate them by pointer identity. Deferred to the managed bridge, which can settle it by following each candidate's GCHandle.
- control.position: 1 of 21 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x1393c682290
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- canvasItem.visible: not derived — no offset satisfied every sample.
- label.text: no offset matched Godot's Label layout (String text, String xl_text sharing one allocation, alignment enums behind, autowrap ahead); falling back to picking the per-node-varying strings shared by several nodes.
- richTextLabel.text: no offset matched Godot's RichTextLabel layout (use_bbcode behind, bools ahead, and NO xl_text); falling back to strings unique to one node.
- richTextLabel.text: 3 equally large groups of string offsets cover DIFFERENT node sets, so which one is the class is undecided and no offset is reported.
- node.scriptInstance resolved to 0x68 (owner backref 0x8) by the managed bridge rather than by pointer identity alone; on a scene where only the root is scripted there is not enough pointer evidence to separate the candidates.
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.
- richTextLabel.text: candidates disagree on node 0x1393c595860, so its text is withheld — 0xff8="GridProbe ASCII 0123", 0x1000="GridProbe ASCII 0123", 0x1028="…", 0x1058="\n"

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.5-stable (official) release/single/dotnet; raw tree 21 nodes = 20 authored + 1 engine-internal (@VScrollBar@2) |
| `calibration.unaided` | PASS | no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x148, next 0x0, node 0x18 — 20 nodes, sibling counts 2/3/4/3/2/3/1/1 |
| `structural.parent` | PASS | parent 0x128 round-trips against the child list for all 20 nodes |
| `semantic.size` | PASS | control.size 0x4c0, 6 samples, 19/19 nodes exact |
| `semantic.position` | PASS | control.position 0x4b8, 20 samples, 19/19 nodes exact |
| `semantic.scale` | PASS | control.scale 0x4a8, 17 samples, 19/19 nodes exact |
| `semantic.offset` | PASS | control.offset 0x470, 19/19 nodes exact, including 1 node(s) with non-zero anchors that separate Data.offset from Data.anchor |
| `semantic.visible` | FAIL | driver did not report derivation.semantic.offsets["canvasItem.visible"] |
| `strings.names` | PASS | node.name 0x1c0, 20/20 StringNames exact |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 21/21 nodes walked (20 authored + 1 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | FAIL | 1/13 offsets disagree with godot-4.5.x-release-single-x64:<br>    - label.text: derived 0x7f8, profile 0x800<br>    This profile was live-validated (§12.3/§12.3b/§12.4c) BUT against a MODIFIED 4.5.1 engine, not a stock export template. Two readings: (a) the calibrator is wrong, or (b) the stock template's layout differs from the StS2 fork. Resolve it before quoting this cell as evidence — do NOT fall back to the profile. |
| `bridge.managed` | PASS | Probe.Instance -> NativePtr 0x1393c57cdf0 == walk root, reverse ScriptInstance chain verified, 0 managed field(s) read |

### `4.3-release-single-dotnet`

Engine: `4.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- walk root 0x1511a9d2ab0 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1d0 and node.parent 0x460 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x150/next 0x0, head 0x158/next 0x8. Taking head 0x150 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- node.scriptInstance: 2 (slot, owner-backref) pairs each fit, all corroborated by the same 1 scripted node(s) — too few to separate them by pointer identity. Deferred to the managed bridge, which can settle it by following each candidate's GCHandle.
- control.position: 1 of 21 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x1511aa70010
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- canvasItem.visible: not derived — no offset satisfied every sample.
- label.text: no offset matched Godot's Label layout (String text, String xl_text sharing one allocation, alignment enums behind, autowrap ahead); falling back to picking the per-node-varying strings shared by several nodes.
- richTextLabel.text: no offset matched Godot's RichTextLabel layout (use_bbcode behind, bools ahead, and NO xl_text); falling back to strings unique to one node.
- richTextLabel.text: 3 equally large groups of string offsets cover DIFFERENT node sets, so which one is the class is undecided and no offset is reported.
- node.scriptInstance resolved to 0x68 (owner backref 0x8) by the managed bridge rather than by pointer identity alone; on a scene where only the root is scripted there is not enough pointer evidence to separate the candidates.
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.
- richTextLabel.text: candidates disagree on node 0x1511a9d63e0, so its text is withheld — 0x1320="GridProbe ASCII 0123", 0x1328="GridProbe ASCII 0123", 0x1348="…"

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.3-stable (official) release/single/dotnet; raw tree 21 nodes = 20 authored + 1 engine-internal (@VScrollBar@2) |
| `calibration.unaided` | PASS | no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x150, next 0x0, node 0x18 — 20 nodes, sibling counts 2/3/4/3/2/3/1/1 |
| `structural.parent` | PASS | parent 0x128 round-trips against the child list for all 20 nodes |
| `semantic.size` | PASS | control.size 0x520, 6 samples, 19/19 nodes exact |
| `semantic.position` | PASS | control.position 0x518, 20 samples, 19/19 nodes exact |
| `semantic.scale` | PASS | control.scale 0x508, 17 samples, 19/19 nodes exact |
| `semantic.offset` | PASS | control.offset 0x4d8, 19/19 nodes exact, including 1 node(s) with non-zero anchors that separate Data.offset from Data.anchor |
| `semantic.visible` | FAIL | driver did not report derivation.semantic.offsets["canvasItem.visible"] |
| `strings.names` | PASS | node.name 0x1d0, 20/20 StringNames exact |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 21/21 nodes walked (20 authored + 1 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.3-release-single-dotnet — nothing to cross-check against, and nothing to fall back to |
| `bridge.managed` | PASS | Probe.Instance -> NativePtr 0x1511a9d2ab0 == walk root, reverse ScriptInstance chain verified, 0 managed field(s) read |

### `4.3-release-single-gdscript`

Engine: `4.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- walk root 0x2c61b61f3c0 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1d0 and node.parent 0x128 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x150/next 0x0, head 0x158/next 0x8. Taking head 0x150 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- node.scriptInstance: 2 (slot, owner-backref) pairs each fit, all corroborated by the same 1 scripted node(s) — too few to separate them by pointer identity. Deferred to the managed bridge, which can settle it by following each candidate's GCHandle.
- control.position: 1 of 21 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x2c61b5eebf0
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- canvasItem.visible: not derived — no offset satisfied every sample.
- label.text: no offset matched Godot's Label layout (String text, String xl_text sharing one allocation, alignment enums behind, autowrap ahead); falling back to picking the per-node-varying strings shared by several nodes.
- richTextLabel.text: 5 equally large groups of string offsets cover DIFFERENT node sets, so which one is the class is undecided and no offset is reported.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.3-stable (official) release/single/gdscript; raw tree 21 nodes = 20 authored + 1 engine-internal (@VScrollBar@2) |
| `calibration.unaided` | PASS | no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x150, next 0x0, node 0x18 — 20 nodes, sibling counts 2/3/4/3/2/3/1/1 |
| `structural.parent` | PASS | parent 0x128 round-trips against the child list for all 20 nodes |
| `semantic.size` | PASS | control.size 0x520, 6 samples, 19/19 nodes exact |
| `semantic.position` | PASS | control.position 0x518, 20 samples, 19/19 nodes exact |
| `semantic.scale` | PASS | control.scale 0x508, 17 samples, 19/19 nodes exact |
| `semantic.offset` | PASS | control.offset 0x4d8, 19/19 nodes exact, including 1 node(s) with non-zero anchors that separate Data.offset from Data.anchor |
| `semantic.visible` | FAIL | driver did not report derivation.semantic.offsets["canvasItem.visible"] |
| `strings.names` | PASS | node.name 0x1d0, 20/20 StringNames exact |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | FAIL | RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaRich<br>    expected: "ρich ✦ テキスト 𝄞 RTL"  cp=[961,105,99,104,32,10022,32,12486,12461,12473,12488,32,119070,32,82,84,76]<br>    actual:   "Color"  cp=[67,111,108,111,114]<br>    DIAGNOSIS: all non-ASCII was dropped — the decoder is treating the buffer as ASCII/UTF-8. |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 21/21 nodes walked (20 authored + 1 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.3-release-single-gdscript — nothing to cross-check against, and nothing to fall back to |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.3-debug-single-dotnet`

Engine: `4.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- walk root 0x1efecab3bb0 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1d8 and node.parent 0x130 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x158/next 0x0, head 0x160/next 0x8. Taking head 0x158 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- scriptInstance.ownerBackref derived as 0x8 on a dotnet cell. This one is expected to differ by BINDING rather than by engine version: a GDScript instance and a C# instance are different C++ classes implementing ScriptInstance, so the owner pointer need not sit at the same place in both. It is a per-binding fact, not a per-version one.
- control.position: 2 of 21 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x1efecb7f6c0, 0x1efecad34e0
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- canvasItem.visible: not derived — no offset satisfied every sample.
- label.text: no offset matched Godot's Label layout (String text, String xl_text sharing one allocation, alignment enums behind, autowrap ahead); falling back to picking the per-node-varying strings shared by several nodes.
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.3-stable (official) debug/single/dotnet; raw tree 21 nodes = 20 authored + 1 engine-internal (@VScrollBar@2) |
| `calibration.unaided` | PASS | no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x158, next 0x0, node 0x18 — 20 nodes, sibling counts 2/3/4/3/2/3/1/1 |
| `structural.parent` | PASS | parent 0x130 round-trips against the child list for all 20 nodes |
| `semantic.size` | PASS | control.size 0x528, 6 samples, 19/19 nodes exact |
| `semantic.position` | PASS | control.position 0x520, 19 samples, 19/19 nodes exact |
| `semantic.scale` | PASS | control.scale 0x510, 17 samples, 19/19 nodes exact |
| `semantic.offset` | PASS | control.offset 0x4e0, 19/19 nodes exact, including 1 node(s) with non-zero anchors that separate Data.offset from Data.anchor |
| `semantic.visible` | FAIL | driver did not report derivation.semantic.offsets["canvasItem.visible"] |
| `strings.names` | PASS | node.name 0x1d8, 20/20 StringNames exact |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | FAIL | RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaRich: driver reported no text |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 21/21 nodes walked (20 authored + 1 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.3-debug-single-dotnet — nothing to cross-check against, and nothing to fall back to |
| `bridge.managed` | PASS | Probe.Instance -> NativePtr 0x1efecab3bb0 == walk root, reverse ScriptInstance chain verified, 0 managed field(s) read |

### `4.3-debug-single-gdscript`

Engine: `4.3-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `none`

<details><summary>driver notes</summary>

- walk root 0x17ae635ae80 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1d8 and node.parent 0x130 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x158/next 0x0, head 0x160/next 0x8. Taking head 0x158 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- scriptInstance.ownerBackref derived as 0x10 on a gdscript cell. This one is expected to differ by BINDING rather than by engine version: a GDScript instance and a C# instance are different C++ classes implementing ScriptInstance, so the owner pointer need not sit at the same place in both. It is a per-binding fact, not a per-version one.
- control.position: 1 of 21 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x17ae630d290
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- canvasItem.visible: not derived — no offset satisfied every sample.
- label.text: 2 equally large groups of string offsets cover DIFFERENT node sets, so which one is the class is undecided and no offset is reported.
- richTextLabel.text: 3 equally large groups of string offsets cover DIFFERENT node sets, so which one is the class is undecided and no offset is reported.
- richTextLabel.text: candidates disagree on node 0x17ae62f1380, so its text is withheld — 0x1080="@export_enum", 0x1140="@export_file", 0x1200="@export_dir", 0x12c0="@export_global_file", 0x13d0="@export_flags_3d_render"

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.3-stable (official) debug/single/gdscript; raw tree 21 nodes = 20 authored + 1 engine-internal (@VScrollBar@2) |
| `calibration.unaided` | PASS | no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x158, next 0x0, node 0x18 — 20 nodes, sibling counts 2/3/4/3/2/3/1/1 |
| `structural.parent` | PASS | parent 0x130 round-trips against the child list for all 20 nodes |
| `semantic.size` | PASS | control.size 0x528, 6 samples, 19/19 nodes exact |
| `semantic.position` | PASS | control.position 0x520, 20 samples, 19/19 nodes exact |
| `semantic.scale` | PASS | control.scale 0x510, 17 samples, 19/19 nodes exact |
| `semantic.offset` | PASS | control.offset 0x4e0, 19/19 nodes exact, including 1 node(s) with non-zero anchors that separate Data.offset from Data.anchor |
| `semantic.visible` | FAIL | driver did not report derivation.semantic.offsets["canvasItem.visible"] |
| `strings.names` | PASS | node.name 0x1d8, 20/20 StringNames exact |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | FAIL | RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaLabelUnicode: driver reported no text |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 21/21 nodes walked (20 authored + 1 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | SKIP | no shipped profile covers 4.3-debug-single-gdscript — nothing to cross-check against, and nothing to fall back to |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.5-release-single-gdscript`

Engine: `4.5-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `godot-4.5.x-release-single-x64`

<details><summary>driver notes</summary>

- walk root 0x20712e18800 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1c0 and node.parent 0x128 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x148/next 0x0, head 0x150/next 0x8. Taking head 0x148 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- scriptInstance.ownerBackref derived as 0x10 on a gdscript cell. This one is expected to differ by BINDING rather than by engine version: a GDScript instance and a C# instance are different C++ classes implementing ScriptInstance, so the owner pointer need not sit at the same place in both. It is a per-binding fact, not a per-version one.
- control.position: 2 of 21 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x20712e8f180, 0x20712e383c0
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- canvasItem.visible: not derived — no offset satisfied every sample.
- label.text: no offset matched Godot's Label layout (String text, String xl_text sharing one allocation, alignment enums behind, autowrap ahead); falling back to picking the per-node-varying strings shared by several nodes.
- label.text: 2 equally large groups of string offsets cover DIFFERENT node sets, so which one is the class is undecided and no offset is reported.
- richTextLabel.text: 2 equally large groups of string offsets cover DIFFERENT node sets, so which one is the class is undecided and no offset is reported.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.5-stable (official) release/single/gdscript; raw tree 21 nodes = 20 authored + 1 engine-internal (@VScrollBar@2) |
| `calibration.unaided` | PASS | no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x148, next 0x0, node 0x18 — 20 nodes, sibling counts 2/3/4/3/2/3/1/1 |
| `structural.parent` | PASS | parent 0x128 round-trips against the child list for all 20 nodes |
| `semantic.size` | PASS | control.size 0x4c0, 6 samples, 19/19 nodes exact |
| `semantic.position` | PASS | control.position 0x4b8, 19 samples, 19/19 nodes exact |
| `semantic.scale` | PASS | control.scale 0x4a8, 17 samples, 19/19 nodes exact |
| `semantic.offset` | PASS | control.offset 0x470, 19/19 nodes exact, including 1 node(s) with non-zero anchors that separate Data.offset from Data.anchor |
| `semantic.visible` | FAIL | driver did not report derivation.semantic.offsets["canvasItem.visible"] |
| `strings.names` | PASS | node.name 0x1c0, 20/20 StringNames exact |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 21/21 nodes walked (20 authored + 1 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | FAIL | 1/11 offsets disagree with godot-4.5.x-release-single-x64:<br>    - scriptInstance.ownerBackref: derived 0x10, profile 0x8<br>    This profile was live-validated (§12.3/§12.3b/§12.4c) BUT against a MODIFIED 4.5.1 engine, not a stock export template. Two readings: (a) the calibrator is wrong, or (b) the stock template's layout differs from the StS2 fork. Resolve it before quoting this cell as evidence — do NOT fall back to the profile. |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

### `4.5-debug-single-dotnet`

Engine: `4.5-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `godot-4.5.x-debug-single-x64`

<details><summary>driver notes</summary>

- walk root 0x1a2143b8e80 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1c8 and node.parent 0x130 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x150/next 0x0, head 0x158/next 0x8. Taking head 0x150 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- scriptInstance.ownerBackref derived as 0x8 on a dotnet cell. This one is expected to differ by BINDING rather than by engine version: a GDScript instance and a C# instance are different C++ classes implementing ScriptInstance, so the owner pointer need not sit at the same place in both. It is a per-binding fact, not a per-version one.
- control.position: 1 of 21 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x1a214468800
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- canvasItem.visible: not derived — no offset satisfied every sample.
- label.text: no offset matched Godot's Label layout (String text, String xl_text sharing one allocation, alignment enums behind, autowrap ahead); falling back to picking the per-node-varying strings shared by several nodes.
- richTextLabel.text: no offset matched Godot's RichTextLabel layout (use_bbcode behind, bools ahead, and NO xl_text); falling back to strings unique to one node.
- richTextLabel.text: 3 equally large groups of string offsets cover DIFFERENT node sets, so which one is the class is undecided and no offset is reported.
- bridge.managed: the managed object was reached from the NATIVE side (node -> ScriptInstance -> GCHandle) and its type confirmed against the name the harness supplied. The static field slot itself was not independently resolved — LiveClr does not publish static addresses — so staticRootField is echoed from the request, not derived.
- richTextLabel.text: candidates disagree on node 0x1a2143ccb80, so its text is withheld — 0x1010="GridProbe ASCII 0123", 0x1018="GridProbe ASCII 0123", 0x1040="…", 0x1070="\n", 0x1178="GridProbe ASCII 0123​"

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.5-stable (official) debug/single/dotnet; raw tree 21 nodes = 20 authored + 1 engine-internal (@VScrollBar@2) |
| `calibration.unaided` | PASS | no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x150, next 0x0, node 0x18 — 20 nodes, sibling counts 2/3/4/3/2/3/1/1 |
| `structural.parent` | PASS | parent 0x130 round-trips against the child list for all 20 nodes |
| `semantic.size` | PASS | control.size 0x4c8, 6 samples, 19/19 nodes exact |
| `semantic.position` | PASS | control.position 0x4c0, 20 samples, 19/19 nodes exact |
| `semantic.scale` | PASS | control.scale 0x4b0, 17 samples, 19/19 nodes exact |
| `semantic.offset` | PASS | control.offset 0x478, 19/19 nodes exact, including 1 node(s) with non-zero anchors that separate Data.offset from Data.anchor |
| `semantic.visible` | FAIL | driver did not report derivation.semantic.offsets["canvasItem.visible"] |
| `strings.names` | PASS | node.name 0x1c8, 20/20 StringNames exact |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | PASS | "héllo ✦ 日本語" — 11 codepoints, max U+8A9E |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 21/21 nodes walked (20 authored + 1 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | FAIL | 8/13 offsets disagree with godot-4.5.x-debug-single-x64:<br>    - node.parent: derived 0x130, profile 0x178<br>    - node.childListHead: derived 0x150, profile 0x198<br>    - node.name: derived 0x1c8, profile 0x210<br>    - control.offset: derived 0x478, profile 0x500<br>    - control.scale: derived 0x4b0, profile 0x4f8<br>    - control.position: derived 0x4c0, profile 0x508<br>    - control.size: derived 0x4c8, profile 0x510<br>    - label.text: derived 0x800, profile 0x848<br>    This profile is marked trust="unverified" (none — read out of scry's disassembly, never executed). §4.6 already r |
| `bridge.managed` | PASS | Probe.Instance -> NativePtr 0x1a2143b8e80 == walk root, reverse ScriptInstance chain verified, 0 managed field(s) read |

### `4.5-debug-single-gdscript`

Engine: `4.5-stable (official)` · driver: `dotnet:Godot.External.Calibrator` · profile: `godot-4.5.x-debug-single-x64`

<details><summary>driver notes</summary>

- walk root 0x1cc32f7f3d0 located by UTF-32 scan for "RootHarness" and "AlphaPanel", then pointer identity; the same solve gave node.name 0x1c8 and node.parent 0x130 before either was derived again from the walk.
- 2 node layouts each reproduced the authored scene: head 0x150/next 0x0, head 0x158/next 0x8. Taking head 0x150 — Godot's List<Node *> holds `first` then `last` and links elements both ways, so the higher pair is the same list walked backwards from its tail. The lower offset is `first`, whose chain gives the authored child order; the node set is identical either way, so only the order and the reported offsets differ.
- node.scriptInstance: 2 (slot, owner-backref) pairs each fit, all corroborated by the same 1 scripted node(s) — too few to separate them by pointer identity. Deferred to the managed bridge, which can settle it by following each candidate's GCHandle.
- control.position: 1 of 21 node(s) read a position that is not offset[0..1] — the expected signature of a non-zero anchor, since pos = offset + anchor * parent_size. Those nodes are the reason this derivation counts support instead of demanding unanimity: 0x1cc32fcd370
- control.scale is the weakest derivation reported here. The harness states no scales, so the known value is upstream's declared default Vector2(1,1); it is separated from CanvasItem::modulate (which is Color(1,1,1,1) and offers six more such pairs) by restricting the scan to the region between the derived control.offset and control.position — a base class is laid out before its derived class — and by requiring the field to actually vary.
- canvasItem.visible: not derived — no offset satisfied every sample.
- label.text: 2 equally large groups of string offsets cover DIFFERENT node sets, so which one is the class is undecided and no offset is reported.

</details>

| Check | Status | Detail |
| --- | --- | --- |
| `harness.runtime_axes` | PASS | 4.5-stable (official) debug/single/gdscript; raw tree 21 nodes = 20 authored + 1 engine-internal (@VScrollBar@2) |
| `calibration.unaided` | PASS | no shipped offsets consumed |
| `structural.child_head` | PASS | head 0x150, next 0x0, node 0x18 — 20 nodes, sibling counts 2/3/4/3/2/3/1/1 |
| `structural.parent` | PASS | parent 0x130 round-trips against the child list for all 20 nodes |
| `semantic.size` | PASS | control.size 0x4c8, 6 samples, 19/19 nodes exact |
| `semantic.position` | PASS | control.position 0x4c0, 20 samples, 19/19 nodes exact |
| `semantic.scale` | PASS | control.scale 0x4b0, 17 samples, 19/19 nodes exact |
| `semantic.offset` | PASS | control.offset 0x478, 19/19 nodes exact, including 1 node(s) with non-zero anchors that separate Data.offset from Data.anchor |
| `semantic.visible` | FAIL | driver did not report derivation.semantic.offsets["canvasItem.visible"] |
| `strings.names` | PASS | node.name 0x1c8, 20/20 StringNames exact |
| `strings.text.ascii` | PASS | "GridProbe ASCII 0123" — 20 codepoints, max U+72 |
| `strings.text.unicode` | FAIL | RootHarness/AlphaPanel/BetaBranch/GammaNest/DeltaCore/EpsilonCore/ZetaLabelUnicode: driver reported no text |
| `strings.text.rich` | PASS | "ρich ✦ テキスト 𝄞 RTL" — 17 codepoints, max U+1D11E, includes an astral codepoint (surrogate pair in UTF-16) |
| `structure.no_collapse` | PASS | [409, 151] on 2 distinct nodes |
| `structure.walk_count` | PASS | 21/21 nodes walked (20 authored + 1 engine-internal), 7 distinct depths, max depth 7 |
| `profile.agreement` | FAIL | 8/10 offsets disagree with godot-4.5.x-debug-single-x64:<br>    - node.parent: derived 0x130, profile 0x178<br>    - node.childListHead: derived 0x150, profile 0x198<br>    - node.name: derived 0x1c8, profile 0x210<br>    - control.offset: derived 0x478, profile 0x500<br>    - control.scale: derived 0x4b0, profile 0x4f8<br>    - control.position: derived 0x4c0, profile 0x508<br>    - control.size: derived 0x4c8, profile 0x510<br>    - richTextLabel.text: derived 0xa80, profile 0xb18<br>    This profile is marked trust="unverified" (none — read out of scry's disassembly, never executed). §4.6 a |
| `bridge.managed` | SKIP | gdscript cell — there is no managed bridge to test |

## Harness self-validation — NOT coverage

`node selftest.mjs` at `2026-08-17T05:28:05.893Z`: **15/15** scenarios.

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

`build.ps1` skipped 32 cell(s) on `DESKTOP-1I6F4IL` at
`2026-08-16T23:46:27.3510577-04:00`. Grouped by what is missing:

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

