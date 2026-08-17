# Contributing

## Build and test

```bash
dotnet build -warnaserror
dotnet test
node tools/godot-abi-grid/selftest.mjs
```

Windows and .NET 9 SDK. No Godot install is needed for the test suite — everything runs against
synthetic byte arrays.

## The one principle

**A confident wrong answer is worse than a miss.**

This library reads a running game's memory and interprets it. Its characteristic failure is not a
crash; it is a plausible number. Two real examples from building it:

- Reading a `Control`-only field off a non-`Control` ancestor **succeeds** and returns a denormal
  like `2.6e-38`. Nothing errors.
- A scene-tree walk during a node splice returns `Complete` and ten children short, with every
  individual read succeeding.

So: decline rather than guess, fail closed, and never add a guard that cannot fire.

## Offsets are data, not code

Every engine offset lives in a `GodotAbiProfile`, keyed by version × build template × precision.
Nothing outside `Abi/` may hardcode one.

Two rules that follow:

- **Confidence is ordered so it cannot be laundered.** Calibrating one field of an unvalidated
  profile must not promote the other seventeen. `WithCalibratedOffset` only ever demotes.
- **The shipped table is a fast path and a cross-check, not the source of truth.** Calibration
  derives offsets at connect; divergence from the table is a loud warning, never a silent fallback.

## Before adding a profile

A new version/template cell needs evidence, not a guess. `tools/godot-abi-grid/` exports a
known-ground-truth project and requires the calibrator to derive every offset **unaided**. A profile
without a grid row is not supported, and the README says so per cell.

Note the debug column ships marked `Unvalidated` and is known self-inconsistent — its
`getOffset` range overlaps `getPosition`, and `scale` sits below `offset`, inverting the field order
release and upstream `control.h` agree on. There is a test asserting the overlap **persists**, to
stop someone "fixing" it by guessing. Do not fix it without a live measurement.

## Test quality

- **Does the test fail against the old code?** If not, it is documentation.
- **Would it pass if the implementation were subtly wrong?** Offset tests that assert a constant
  against the same constant catch drift but not a shared transcription error.
- Non-ASCII coverage must include an **astral** character. Latin-1 survives byte truncation
  (`U+00E9` → `0xE9` → `é`), so `café` round-trips *through* a lossy decoder and proves nothing.

## Licence and provenance

MIT. This library was written from Godot's own MIT source plus independent analysis. Do not
introduce code, or code closely derived from, GPL-licensed projects in this space — notably
[Zolt-Dump](https://github.com/bbfox0703/Zolt-Dump), which is excellent prior art and
GPL-3.0. Its documentation is a fine reference; its source is not.
