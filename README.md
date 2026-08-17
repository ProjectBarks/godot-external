# Godot.External

**Read a running Godot 4 game's scene tree from another process — no mod, no injection, no code
inside the game.**

[![CI](https://github.com/ProjectBarks/godot-external/actions/workflows/ci.yml/badge.svg)](https://github.com/ProjectBarks/godot-external/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](LICENSE)
[![.NET 9](https://img.shields.io/badge/.NET-9.0%2B-512BD4)](https://dotnet.microsoft.com/)
[![Godot 4](https://img.shields.io/badge/Godot-4.x-478CBF)](https://godotengine.org/)

Nothing is loaded into the target, no code runs inside it, and it is never suspended or written to.
Everything is `ReadProcessMemory` against a live, **unmodified** game.

> ### Status: not published, and not ready to be
> Validated against exactly **one cell** of the compatibility matrix — Godot 4.5.1, release
> template, single precision, .NET binding, on one *modified* engine — at 111/112 offset checks.
> Publishing that as general Godot support would be dishonest. See [Coverage](#coverage).

## What it does

| Layer | |
| --- | --- |
| `Abi/` | Offset profiles keyed by version × template × precision, with per-field calibration provenance |
| `Values/` | `CowData` (UTF-32 strings), `StringName`, the intrusive child-list walk |
| `Bridge/` | Managed↔native crossing — `NativePtr` ↔ `ManagedPtr` via the `ScriptInstance` → GCHandle chain |
| `Scene/` | `SceneEpoch` lifetime ownership, bounded cycle-checked tree walking, node classification |
| `Objects/` | `GodotNode` / `GodotControl` / `GodotLabel` / `GodotRichTextLabel` |

Pairs with [LiveClr](https://github.com/ProjectBarks/LiveCLR) for the managed half: LiveClr reads
the C# object graph, this reads the native engine structures, and `Bridge/` crosses between them.

## Three findings that shape the design

**A traversal can tear while every read succeeds.** One walk in 26 came back ten children short
during a scene mutation, with no read failure anywhere — status `Complete`, silently wrong. Retry
logic keyed on read errors cannot see it. Hence `WalkStable` (agree-twice) and a
structural-suspicion status on every result rather than a bare list.

**Native pointers outlive the GC but not the scene.** Godot frees a node and reuses the allocation,
so a stale pointer addresses a *different, entirely plausible* node. `SceneEpoch` owns that
lifetime; handles cannot be built outside a live epoch.

**Reading a `Control` field off a non-`Control` succeeds.** It returns a denormal like `2.6e-38`
and reports success. Composing a global position therefore *requires* an ancestor gate — there is
deliberately no convenient overload that walks to the root unchecked.

## Coverage

| Axis | Covered |
| --- | --- |
| Engine version | 4.5.1 only |
| Template | release only — the debug column ships `Unvalidated` and is known self-inconsistent |
| Precision | single only |
| Binding | .NET only |

`tools/godot-abi-grid/` is the gate: it exports a known-ground-truth project across the grid and
requires the calibrator to derive every offset **unaided**. Publication waits on that going green,
so the claim becomes *"the calibrator solves layouts it has never seen"* rather than *"we know
Godot's layout"*.

Note official export templates are **single-precision only** — the `precision=double` cells need an
engine built from source, and `real_t` width moves every float offset.

## Offsets are derived, not just hardcoded

The shipped table is a fast path and a cross-check. Calibration recovers offsets at connect from
independently-known ground truth — pointer identity for structural fields, known-value intersection
for semantic ones. A single sample is not enough for the semantic case and the API refuses it:
scanning one control for its size yields four candidates; intersecting with a second control of a
different size leaves exactly one.

## Prior art

[bbfox0703/Zolt-Dump](https://github.com/bbfox0703/Zolt-Dump) is substantial prior art for live
Godot memory introspection — `ObjectDB`, `ClassDB`, `SceneTree`, Variants, Godot 3.5 through 4.6 —
and covers far more of the engine than this does. It reaches that capability by injecting a native
DLL plus a CLR reflector into the target: the opposite architectural choice, and the reason this
project exists separately.

It is GPL-3.0 and archived. **No code from it was used or read.** Everything here derives from
Godot's own MIT source plus independent analysis.

## Licence

MIT.
