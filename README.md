# Godot.External

Read-only, **non-injected**, out-of-process inspection of a running Godot 4 game.

No DLL is loaded into the target, no code runs inside it, and the process is never suspended or
written to. Everything is `ReadProcessMemory` against a live, unmodified game.

> **Not published, and not ready to be.** See [Status](#status). The offset table here is validated
> against exactly one build. Publishing it as general support would be dishonest.

## What it does

| Layer | |
| --- | --- |
| `Abi/` | Version/template/precision-keyed offset profiles, with per-field calibration provenance |
| `Values/` | `CowData` (UTF-32 strings), `StringName`, the intrusive child-list walk |
| `Bridge/` | The managed↔native crossing — `NativePtr` ↔ `ManagedPtr`, via the `ScriptInstance` → GCHandle chain |
| `Scene/` | `SceneEpoch` lifetime ownership, bounded cycle-checked tree walking, node classification |
| `Objects/` | `GodotNode` / `GodotControl` / `GodotLabel` / `GodotRichTextLabel` |

## Two things that shape the whole design

**A traversal can tear while every individual read succeeds.** Measured against a live game: one
walk in 26 returned ten nodes short during a scene mutation, with no read failure anywhere. Retry
logic keyed on read errors cannot see this. Hence `ChildListWalk.WalkStable` (agree-twice) and a
structural-suspicion status on every walk result, rather than a bare list.

**Native pointers outlive the CLR GC but not the scene.** Godot frees a node and reuses the
allocation, so a stale pointer addresses a *different, entirely plausible* node. `SceneEpoch` owns
that lifetime; handles cannot be constructed outside a live epoch and every read re-enters it.

## Status

Validated against **Godot 4.5.1, release template, single precision, .NET binding** — on one
modified engine — at 111/112 offset checks. That is one cell of a large matrix:

| Axis | Covered |
| --- | --- |
| Engine version | 4.5.1 only |
| Template | release only (the debug column ships marked `Unvalidated`, and is known self-inconsistent) |
| Precision | single only |
| Binding | .NET only |

`tools/godot-abi-grid/` is the harness that closes this: it exports a known-ground-truth project
across the grid and requires the calibrator to derive every offset **unaided**. Publication waits
on that going green, so the claim becomes *"the calibrator solves layouts it has never seen"*
rather than *"we know Godot's layout"*.

Note: official export templates are **single-precision only** — the `precision=double` cells need
an engine built from source.

## Prior art

[bbfox0703/Zolt-Dump](https://github.com/bbfox0703/Zolt-Dump) is substantial prior art for live
Godot memory introspection and covers far more of the engine, including Slay the Spire 2. It
reaches that capability by injecting a native DLL plus a CLR reflector into the target — the
opposite architectural choice. It is GPL-3.0 and archived; **no code from it was used or read**,
and everything here derives from Godot's own MIT source plus independent analysis.

## Licence

MIT.
