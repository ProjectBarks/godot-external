# Read-path cache benchmark

```
dotnet run -c Release --project bench/Godot.External.Bench
```

That is the whole command. With no arguments it builds three synthetic Godot heaps, runs three
workloads under twelve cache variants against each, cross-checks that every variant returned
identical data, and finishes with a demonstration of what the invalidation design does and does not
prevent. It needs no game, no fixture and no elevated privileges.

Optional targets:

```
--fixture bench/fixtures/sts2-scene.gxfix     replay a recorded real game (3.3 MB, in-tree)
--pid <n>                                     attach to a running Godot game, READ ONLY
--record <path>                               record a live target into a replayable fixture
--csv <path>                                  every column, machine-readable
```

`bench/RESULTS.md` is the checked-in output of
`--pid <slay the spire 2> --record bench/fixtures/sts2-scene.gxfix --repetitions 7`.

---

## The answer

**The page cache wins — but not the 4 KiB page cache.** The suspicion that a 4 KiB page is the wrong
unit for an 8-byte pointer read is correct, and the measurements below quantify it: 43.8x
amplification on a tree walk, 314x on a targeted geometry read. The fix is a **smaller block**, not
an object-shaped one. `MemoryCacheOptions.PageSize` therefore defaults to **512**, not to the 4,096
LiveClr's `PageCache` uses.

**The object-span cache — the clever one — is not worth its complexity.** Precisely:

| workload | verdict |
|---|---|
| (a) tree walk | **strictly dominated.** `page-512` uses fewer syscalls (3,887 vs 4,453), reads fewer bytes (1.99 MB vs 2.49 MB) *and* is faster (5.32 vs 6.17 ms). |
| (b) targeted geometry | on the frontier, not ahead of it. Matches `page-4k`'s syscall count with 3.1x fewer bytes, but `page-128` reads 4.9x less again at the same wall time. |
| (c) 4 Hz poll | on the frontier. Dominates `page-512` and `page-1k` on bytes at similar cost; `page-2k` is faster with 3x the bytes, `page-128` reads 2.1x less with 2x the syscalls. |

So it wins nothing outright, loses the workload the brief singled out as the most pointer-chase-heavy,
and costs an object-extent hint threaded through `GodotNode`, `ControlGeometry` and the classifier to
get there. **Ship the page cache.** The span mode stays in the tree as the evidence, selectable by
anyone whose workload reads most of each node.

### Why the span design lost

The hypothesis was that per-node locality is high (true: one node's fields sit inside ~1.2 KB) and
cross-node locality is unknown (also true, and now measured). The measurement that killed it is
that **the fields this library reads do not use the node struct evenly — they cluster**:

| operation | offsets touched | span of the cluster |
|---|---|---|
| tree walk | `NodeChildListHead` 0x148, `NodeName` 0x1c0 | 128 B |
| geometry | `CanvasItemVisible` 0x370, `offset[4]` 0x470, `scale` 0x4a8, `pos_cache` 0x4b8, `size_cache` 0x4c0 | 0x370–0x4c8 |

An aligned block of 128–512 bytes fetches only the clusters a given workload actually uses. The
1,224-byte object span fetches all of them, every time, whether or not the caller wanted geometry.
On the tree walk that costs 3.2x the bytes a 128-byte block reads, and 1.25x what a 512-byte block
reads while issuing 13 % more syscalls than it.

### The allocator question, answered

Measured on Slay the Spire 2 (pid attached read-only, 1,562 nodes reachable from the located root):

```
1,562 nodes across 888 pages → 1.76 nodes per 4 KiB page
6.7 % of breadth-first-consecutive node pairs share a page
median address gap between BFS-consecutive nodes: 25,520 B
```

So cross-node locality is **poor but not absent**. A 4 KiB page holds fewer than two node structs,
which is why 4 KiB blocks amplify 43.8x on a walk. It is also why the page cache still works at all:
1.76 > 1, and link nodes and string buffers land in the same pages as the nodes that own them.

---

## Live results — Slay the Spire 2, 1,562 nodes

Uncached is the baseline. Best in each column in **bold**.
"Amplification" is bytes read from the target per byte the library asked for.

### (a) full tree walk — structure and every node's name

| variant | syscalls | vs base | bytes read | amplification | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|
| uncached | 15,616 | 1.000x | 188,632 | **1.00x** | 11.51 | **0 B** |
| page-128 | 6,023 | 0.386x | 770,944 | 5.10x | 6.86 | 753 KiB |
| page-256 | 4,701 | 0.301x | 1,203,456 | 7.96x | 5.80 | 1.1 MiB |
| **page-512** | 3,887 | 0.249x | 1,990,144 | 13.17x | 5.32 | 1.9 MiB |
| page-1k | 3,220 | 0.206x | 3,297,280 | 21.81x | 4.27 | 3.1 MiB |
| page-2k | 2,652 | 0.170x | 5,431,296 | 35.93x | 4.15 | 5.2 MiB |
| page-4k | 1,618 | 0.104x | 6,627,328 | 43.84x | 3.66 | 6.3 MiB |
| page-16k | **824** | **0.053x** | 11,583,488 | 76.63x | **3.16** | 10.4 MiB |
| span | 4,453 | 0.285x | 2,486,784 | 16.45x | 6.17 | 2.3 MiB |
| hybrid-1k | 3,039 | 0.195x | 3,632,384 | 24.03x | 5.57 | 3.4 MiB |
| hybrid-4k | 2,280 | 0.146x | 5,090,560 | 33.68x | 6.28 | 4.8 MiB |
| hybrid-4k+text | 1,828 | 0.117x | 6,088,320 | 40.28x | 5.67 | 5.5 MiB |

`span` is beaten by `page-512` on **all three** of syscalls, bytes and wall time. This is the
workload the design was supposed to be best at, and it is the one it loses outright — because a walk
reads two fields per node, 120 bytes apart, and the span fetches 1,224.

### (b) targeted read of one node's geometry (200 iterations, one snapshot each)

| variant | syscalls | vs base | bytes read | amplification | wall ms |
|---|--:|--:|--:|--:|--:|
| uncached | 18,200 | 1.000x | 145,800 | **1.00x** | 11.55 |
| page-128 | 7,200 | 0.396x | 921,600 | 6.32x | 6.08 |
| page-256 | 7,200 | 0.396x | 1,843,200 | 12.64x | 5.95 |
| **page-512** | 7,000 | 0.385x | 3,584,000 | 24.58x | 5.99 |
| page-1k | 6,600 | 0.363x | 6,758,400 | 46.35x | 6.54 |
| page-2k | 3,800 | 0.209x | 7,782,400 | 53.38x | 5.46 |
| page-4k | **3,400** | **0.187x** | 13,926,400 | 95.52x | 5.53 |
| page-16k | 3,400 | 0.187x | 45,875,200 | 314.64x | 8.16 |
| span | 3,400 | 0.187x | 4,556,800 | 31.25x | 6.33 |
| hybrid-4k | 3,400 | 0.187x | 4,556,800 | 31.25x | **5.89** |

The one place the span design shows its intended strength: it matches `page-4k`'s syscall count
using **3.1x fewer bytes**, because the ancestor chain it walks is exactly "read most of one node,
then jump". It is still beaten on bytes by `page-128`, which reads 4.9x less again for the same
wall time.

`page-16k` is the pure demonstration of the failure mode the brief asked to expose: **314x
amplification** — 45.9 MB pulled to deliver 145.8 KB.

### (c) 4 Hz subtree poll — the actual overlay pattern (20 polls, one snapshot per poll)

| variant | syscalls | vs base | bytes read | amplification | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|
| uncached | 105,740 | 1.000x | 889,140 | **1.00x** | 68.24 | **0 B** |
| **page-128** | 8,320 | 0.079x | 1,064,960 | 1.24x | 10.98 | 52 KiB |
| page-256 | 7,180 | 0.068x | 1,838,080 | 2.13x | 10.25 | 90 KiB |
| page-512 | 5,980 | 0.057x | 3,061,760 | 3.55x | 9.87 | 150 KiB |
| page-1k | 4,800 | 0.045x | 4,915,200 | 5.70x | 10.94 | 240 KiB |
| page-2k | 3,340 | 0.032x | 6,840,320 | 7.93x | 8.14 | 334 KiB |
| page-4k | 2,340 | 0.022x | 9,584,640 | 11.12x | 7.91 | 468 KiB |
| page-16k | **1,440** | **0.014x** | 20,643,840 | 23.95x | **7.82** | 960 KiB |
| span | 4,120 | 0.039x | 2,263,040 | 2.63x | 8.74 | 109 KiB |
| hybrid-4k | 2,760 | 0.026x | 7,449,600 | 8.64x | 9.83 | 364 KiB |

**This is the workload that matters and the result that justifies the whole exercise.** 105,740
syscalls per second of overlay becomes 5,980; 68 ms of a 250 ms budget becomes 10 ms. At 128-byte
blocks the amplification is **1.24x** — a coherent snapshot for almost no wasted bandwidth. Every
cached variant is within 3 ms of every other, so the choice among them is decided by bytes and by
retained memory, not by the clock.

This is also the one workload where `span` is genuinely on the frontier: it strictly dominates
`page-512` and `page-1k`. It is still not worth being the default, because it wins here by less than
`page-128` wins on bytes, and it loses workload (a) outright.

### Synthetic sweep: does poor allocator locality change the answer?

No. Three synthetic heaps — `sequential` (one arena), `clustered` (16 arenas, 85 % locality) and
`scattered` (256 arenas, no locality) — were run to check whether the span design wins when the page
cache's bet on the allocator fails. On the `scattered` walk, `page-256` still beats `span` on both
syscalls (0.205x vs 0.212x of uncached) and bytes (4.85x vs 13.61x). Cross-node locality has to get
much worse than any plausible allocator produces before an object span pays for itself.

The sweep did turn up one real hazard: on `scattered`, `page-16k` issues **more** syscalls than
`page-4k` (1,280 vs 433), because a 16 KiB block straddles unmapped region boundaries and falls into
the per-4 KiB probe path. Large blocks are not monotonically cheaper — they are cheaper only while
the address space stays contiguous.

### Why 512 is the default

**Wall time does not discriminate, and cannot be trusted to.** Every cached variant lands within
3 ms of every other against a 250 ms poll budget, and repeat runs against a live game — which is
busy doing its own work — moved individual figures by up to 25 % without ever changing the syscall
or byte counts, which are deterministic to the last byte. Treat the wall-ms column as evidence that
caching is worth roughly 2x on a walk and 7x on a poll, and nothing finer.

Bytes read and retained memory do discriminate, and they run the other way from syscall count. 512
is the knee — below it the syscall count climbs faster than amplification falls, above it the
reverse — and it keeps peak retention under 2 MiB on a full 1,562-node walk where 4 KiB blocks hold
6.3 MiB. Move along the curve with one line:

```csharp
using var snapshot = epoch.Snapshot(new MemoryCacheOptions { PageSize = 128 });
```

---

## Reproducibility

Every number above is reproducible without the game:

```
dotnet run -c Release --project bench/Godot.External.Bench -- \
    --fixture bench/fixtures/sts2-scene.gxfix --no-synthetic
```

The fixture is a page-granular recording of the live target taken through the same workloads, so the
syscall counts, byte counts and amplification factors are **identical** to the live run — only wall
time differs, because a replay has no syscall in it. Pages the game refused are recorded as absent
rather than as zeroes, so the negative-caching paths run in CI too.

The benchmark asserts that every variant returns the same node count and the same checksum over
everything it read. A cache that is fast and wrong fails loudly rather than posting a good number.
It also cross-checks each cache's self-reported fetch count against a counter installed underneath
it, so a lying statistic shows up as a mismatch in the report.

---

## The invalidation trap

Four demonstrations run at the end of every benchmark, against a target whose `size_cache` changes
on *every read* — so any check that works by reading twice must see a difference, unless something
is serving the second read from the first read's bytes.

**1. Agree-twice inside a snapshot — handled structurally.** `ChildListWalk.WalkStable` asks the
source `IsCoherent()` and, when it is, performs one traversal instead of two and increments
`AgreeTwiceSuppressed`. This is docs/analysis.md §6.4 ("the two mitigations cancel") turned into
code: the weaker mitigation steps aside for the stronger one and says so, rather than being silently
neutralised by it. On the live walk this is 1,562 suppressions and it halves the walk's logical
reads.

**2. A hand-written "read it twice" check — detected, not prevented.** The library cannot know that a
caller's second read was *meant* to observe change. With `DetectRepeatedReads` on it counts them:
`RepeatedReads` is a straight count of "you read an address twice and got the same answer by
construction". This is the calibrator's two-readings bug, reproduced deliberately.

**3. A snapshot per poll — correct.** Three polls, three snapshots, three distinct values.

**4. One snapshot held across polls — the misuse that survives.** Three polls, one snapshot, one
value repeated three times.

### How a caller can still get this wrong

Honestly stated, because the design does not close it:

- **Hold one snapshot across polls.** Nothing prevents it. `Snapshot()` refuses to open a *second*
  snapshot while one is live, so the mistake usually surfaces on the next poll as an exception — but
  a loop that opens one snapshot outside it and never opens another gets the first poll's data
  forever. `IsStale` goes true after `MaxAge` (250 ms by default) and `StaleReads` climbs, and
  nobody is obliged to look.
- **Call `Invalidate()` mid-traversal.** It is public, it does what it says, and it breaks the
  one-image guarantee by design: reads either side of it come from two moments. The doc comment says
  so; the type system does not.
- **Write a temporal check the library cannot recognise.** Only `WalkStable`'s agree-twice is
  detected automatically. A caller comparing two reads of a pointer, or diffing a value across two
  calls inside one snapshot, gets a vacuous comparison. `DetectRepeatedReads` catches it but is off
  by default (it costs a hash-set insert per read and retains every address touched).
- **Cache derived values across snapshots.** The snapshot freezes bytes, not conclusions. A caller
  who stores a node's name in a dictionary keyed by `Node*` has built exactly the §8.8 hazard the
  snapshot was never protecting against — Godot frees a node and reuses the allocation, and the
  epoch, not the snapshot, is what guards that.

---

## What is measured, and where the numbers come from

- **Syscalls and bytes read** come from a counting decorator installed *underneath* the cache, so
  they are what the operating system saw, not what the cache claims.
- **Useful bytes** come from the snapshot's own logical-read counter — what the library asked for.
- **Amplification** is bytes read ÷ useful bytes.
- **Wall time** is the best of *n* repetitions after a discarded warm-up (which is also what faults
  the target's pages in on a live run).
- **Retained** is the peak bytes held by one snapshot, not the sum across snapshots.

Three synthetic heaps make the allocator a parameter rather than an assumption: `sequential` (one
arena, scene instantiated in one burst), `clustered` (16 arenas, 85 % locality) and `scattered` (256
arenas, no locality). All three allocate depth-first, because that is the order Godot instantiates a
scene, while the benchmark walks breadth-first, because that is what `GodotScene.Walk` does — so even
the sequential heap does not hand the walk its nodes in address order.
