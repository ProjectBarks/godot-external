# Godot.External read-path cache benchmark

- host: DESKTOP-1I6F4IL, 16 cores, .NET 9.0.18
- build: Release
- repetitions: 7 (best wall time reported)

attaching to process 418440 (read-only)...
  scanning for node name "AudioManager"...
  root 0x1A2FA9FBFF0 reaches 1562 nodes (status Complete), anchored on "AudioManager" at 0x1A2F8815F00.
  recorded 3,678 pages (14 MiB in memory, 3.0 MiB compressed) to bench/fixtures/sts2-scene.gxfix

## target: fixture (just recorded)

- locality: 1562 nodes across 888 pages (1.76 nodes/4 KiB page); 6.7 % of BFS-consecutive pairs share a page; median gap 25,520 B
- node span derived from profile: 1,224 B (with text fields: 2,688 B)
- workload `walk`: full breadth-first tree walk, reading every node's name
- workload `geometry`: 200x read one node's full geometry and composed global position
- workload `poll`: 20x poll a <=96-node subtree (geometry + names), one snapshot each

## target: live pid 418440 (1562 nodes)

- locality: 1562 nodes across 888 pages (1.76 nodes/4 KiB page); 6.7 % of BFS-consecutive pairs share a page; median gap 25,520 B
- node span derived from profile: 1,224 B (with text fields: 2,688 B)
- workload `walk`: full breadth-first tree walk, reading every node's name
- workload `geometry`: 200x read one node's full geometry and composed global position
- workload `poll`: 20x poll a <=96-node subtree (geometry + names), one snapshot each

## target: synthetic/sequential

- locality: 2341 nodes across 759 pages (3.08 nodes/4 KiB page); 13.2 % of BFS-consecutive pairs share a page; median gap 7,968 B
- node span derived from profile: 1,224 B (with text fields: 2,688 B)
- workload `walk`: full breadth-first tree walk, reading every node's name
- workload `geometry`: 200x read one node's full geometry and composed global position
- workload `poll`: 20x poll a <=96-node subtree (geometry + names), one snapshot each

## target: synthetic/clustered

- locality: 2341 nodes across 761 pages (3.08 nodes/4 KiB page); 11.0 % of BFS-consecutive pairs share a page; median gap 134,147,344 B
- node span derived from profile: 1,224 B (with text fields: 2,688 B)
- workload `walk`: full breadth-first tree walk, reading every node's name
- workload `geometry`: 200x read one node's full geometry and composed global position
- workload `poll`: 20x poll a <=96-node subtree (geometry + names), one snapshot each

## target: synthetic/scattered

- locality: 2341 nodes across 780 pages (3.00 nodes/4 KiB page); 0.6 % of BFS-consecutive pairs share a page; median gap 5,100,274,992 B
- node span derived from profile: 1,224 B (with text fields: 2,688 B)
- workload `walk`: full breadth-first tree walk, reading every node's name
- workload `geometry`: 200x read one node's full geometry and composed global position
- workload `poll`: 20x poll a <=96-node subtree (geometry + names), one snapshot each

## results

### fixture (just recorded) / walk

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 15,616 | 1.000x | 188,632 | 1.00x | 188,632 | 1.00x | 0.0 % | 3.85 | 0 B |
| page-128 | 6,023 | 0.386x | 770,944 | 4.09x | 151,160 | 5.10x | 46.9 % | 3.61 | 753 KiB |
| page-256 | 4,701 | 0.301x | 1,203,456 | 6.38x | 151,160 | 7.96x | 57.6 % | 3.51 | 1.1 MiB |
| page-512 | 3,887 | 0.249x | 1,990,144 | 10.55x | 151,160 | 13.17x | 64.7 % | 3.47 | 1.9 MiB |
| page-1k | 3,220 | 0.206x | 3,297,280 | 17.48x | 151,160 | 21.81x | 70.7 % | 3.46 | 3.1 MiB |
| page-2k | 2,652 | 0.170x | 5,431,296 | 28.79x | 151,160 | 35.93x | 75.9 % | 3.98 | 5.2 MiB |
| page-4k | 1,618 | 0.104x | 6,627,328 | 35.13x | 151,160 | 43.84x | 85.3 % | 3.58 | 6.3 MiB |
| page-16k | 824 | 0.053x | 11,583,488 | 61.41x | 151,160 | 76.63x | 93.9 % | 3.78 | 10.4 MiB |
| span | 4,459 | 0.286x | 2,490,112 | 13.20x | 151,160 | 16.47x | 60.7 % | 6.62 | 2.3 MiB |
| hybrid-1k | 3,041 | 0.195x | 3,634,432 | 19.27x | 151,160 | 24.04x | 73.2 % | 8.08 | 3.4 MiB |
| hybrid-4k | 2,282 | 0.146x | 5,098,752 | 27.03x | 151,160 | 33.73x | 79.9 % | 7.15 | 4.8 MiB |
| hybrid-4k+text | 1,830 | 0.117x | 6,096,512 | 32.32x | 151,160 | 40.33x | 83.9 % | 7.07 | 5.5 MiB |

- `page-128`: 1,562 agree-twice checks suppressed as vacuous
- `page-256`: 1,562 agree-twice checks suppressed as vacuous
- `page-512`: 1,562 agree-twice checks suppressed as vacuous
- `page-1k`: 1,562 agree-twice checks suppressed as vacuous
- `page-2k`: 1,562 agree-twice checks suppressed as vacuous
- `page-4k`: 1,562 agree-twice checks suppressed as vacuous
- `page-16k`: 1,562 agree-twice checks suppressed as vacuous
- `span`: 1,556 span fetches; 4 span over-reads fell back; 1,562 agree-twice checks suppressed as vacuous
- `hybrid-1k`: 1,554 span fetches; 2 span over-reads fell back; 1,562 agree-twice checks suppressed as vacuous
- `hybrid-4k`: 1,554 span fetches; 2 span over-reads fell back; 1,562 agree-twice checks suppressed as vacuous
- `hybrid-4k+text`: 1,102 span fetches; 2 span over-reads fell back; 1,562 agree-twice checks suppressed as vacuous

### fixture (just recorded) / geometry

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 18,200 | 1.000x | 145,800 | 1.00x | 145,800 | 1.00x | 0.0 % | 2.17 | 0 B |
| page-128 | 7,200 | 0.396x | 921,600 | 6.32x | 145,800 | 6.32x | 60.4 % | 1.47 | 4 KiB |
| page-256 | 7,200 | 0.396x | 1,843,200 | 12.64x | 145,800 | 12.64x | 60.4 % | 1.35 | 9 KiB |
| page-512 | 7,000 | 0.385x | 3,584,000 | 24.58x | 145,800 | 24.58x | 61.5 % | 1.49 | 18 KiB |
| page-1k | 6,600 | 0.363x | 6,758,400 | 46.35x | 145,800 | 46.35x | 63.7 % | 1.63 | 33 KiB |
| page-2k | 3,800 | 0.209x | 7,782,400 | 53.38x | 145,800 | 53.38x | 79.1 % | 1.38 | 38 KiB |
| page-4k | 3,400 | 0.187x | 13,926,400 | 95.52x | 145,800 | 95.52x | 81.3 % | 1.61 | 68 KiB |
| page-16k | 3,400 | 0.187x | 45,875,200 | 314.64x | 145,800 | 314.64x | 85.7 % | 2.23 | 208 KiB |
| span | 3,600 | 0.198x | 4,582,400 | 31.43x | 145,800 | 31.43x | 81.3 % | 1.97 | 21 KiB |
| hybrid-1k | 3,600 | 0.198x | 4,761,600 | 32.66x | 145,800 | 32.66x | 81.3 % | 1.99 | 22 KiB |
| hybrid-4k | 3,600 | 0.198x | 5,376,000 | 36.87x | 145,800 | 36.87x | 81.3 % | 2.23 | 25 KiB |
| hybrid-4k+text | 3,600 | 0.198x | 10,342,400 | 70.94x | 145,800 | 70.94x | 81.3 % | 2.66 | 44 KiB |

- `span`: 3,400 span fetches; 200 span over-reads fell back
- `hybrid-1k`: 3,400 span fetches; 200 span over-reads fell back
- `hybrid-4k`: 3,400 span fetches; 200 span over-reads fell back
- `hybrid-4k+text`: 3,400 span fetches; 200 span over-reads fell back

### fixture (just recorded) / poll

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 105,740 | 1.000x | 889,140 | 1.00x | 889,140 | 1.00x | 0.0 % | 2.98 | 0 B |
| page-128 | 8,320 | 0.079x | 1,064,960 | 1.20x | 862,100 | 1.24x | 91.9 % | 4.90 | 52 KiB |
| page-256 | 7,180 | 0.068x | 1,838,080 | 2.07x | 862,100 | 2.13x | 93.0 % | 5.00 | 90 KiB |
| page-512 | 5,980 | 0.057x | 3,061,760 | 3.44x | 862,100 | 3.55x | 94.2 % | 4.92 | 150 KiB |
| page-1k | 4,800 | 0.045x | 4,915,200 | 5.53x | 862,100 | 5.70x | 95.3 % | 4.99 | 240 KiB |
| page-2k | 3,340 | 0.032x | 6,840,320 | 7.69x | 862,100 | 7.93x | 96.7 % | 4.82 | 334 KiB |
| page-4k | 2,340 | 0.022x | 9,584,640 | 10.78x | 862,100 | 11.12x | 97.7 % | 4.99 | 468 KiB |
| page-16k | 1,440 | 0.014x | 20,643,840 | 23.22x | 862,100 | 23.95x | 98.8 % | 5.35 | 960 KiB |
| span | 4,140 | 0.039x | 2,265,600 | 2.55x | 862,100 | 2.63x | 96.0 % | 5.35 | 108 KiB |
| hybrid-1k | 3,480 | 0.033x | 4,029,440 | 4.53x | 862,100 | 4.67x | 96.6 % | 5.87 | 194 KiB |
| hybrid-4k | 2,780 | 0.026x | 7,531,520 | 8.47x | 862,100 | 8.74x | 97.3 % | 6.67 | 366 KiB |
| hybrid-4k+text | 2,780 | 0.026x | 9,571,840 | 10.77x | 862,100 | 11.10x | 97.3 % | 9.04 | 438 KiB |

- `page-128`: 1,140 agree-twice checks suppressed as vacuous
- `page-256`: 1,140 agree-twice checks suppressed as vacuous
- `page-512`: 1,140 agree-twice checks suppressed as vacuous
- `page-1k`: 1,140 agree-twice checks suppressed as vacuous
- `page-2k`: 1,140 agree-twice checks suppressed as vacuous
- `page-4k`: 1,140 agree-twice checks suppressed as vacuous
- `page-16k`: 1,140 agree-twice checks suppressed as vacuous
- `span`: 1,400 span fetches; 20 span over-reads fell back; 1,140 agree-twice checks suppressed as vacuous
- `hybrid-1k`: 1,400 span fetches; 20 span over-reads fell back; 1,140 agree-twice checks suppressed as vacuous
- `hybrid-4k`: 1,400 span fetches; 20 span over-reads fell back; 1,140 agree-twice checks suppressed as vacuous
- `hybrid-4k+text`: 1,400 span fetches; 20 span over-reads fell back; 1,140 agree-twice checks suppressed as vacuous

### live pid 418440 (1562 nodes) / walk

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 15,616 | 1.000x | 188,632 | 1.00x | 188,632 | 1.00x | 0.0 % | 11.51 | 0 B |
| page-128 | 6,023 | 0.386x | 770,944 | 4.09x | 151,160 | 5.10x | 46.9 % | 6.86 | 753 KiB |
| page-256 | 4,701 | 0.301x | 1,203,456 | 6.38x | 151,160 | 7.96x | 57.6 % | 5.80 | 1.1 MiB |
| page-512 | 3,887 | 0.249x | 1,990,144 | 10.55x | 151,160 | 13.17x | 64.7 % | 5.32 | 1.9 MiB |
| page-1k | 3,220 | 0.206x | 3,297,280 | 17.48x | 151,160 | 21.81x | 70.7 % | 4.27 | 3.1 MiB |
| page-2k | 2,652 | 0.170x | 5,431,296 | 28.79x | 151,160 | 35.93x | 75.9 % | 4.15 | 5.2 MiB |
| page-4k | 1,618 | 0.104x | 6,627,328 | 35.13x | 151,160 | 43.84x | 85.3 % | 3.66 | 6.3 MiB |
| page-16k | 824 | 0.053x | 11,583,488 | 61.41x | 151,160 | 76.63x | 93.9 % | 3.16 | 10.4 MiB |
| span | 4,453 | 0.285x | 2,486,784 | 13.18x | 151,160 | 16.45x | 60.7 % | 6.17 | 2.3 MiB |
| hybrid-1k | 3,039 | 0.195x | 3,632,384 | 19.26x | 151,160 | 24.03x | 73.2 % | 5.57 | 3.4 MiB |
| hybrid-4k | 2,280 | 0.146x | 5,090,560 | 26.99x | 151,160 | 33.68x | 79.9 % | 6.28 | 4.8 MiB |
| hybrid-4k+text | 1,828 | 0.117x | 6,088,320 | 32.28x | 151,160 | 40.28x | 83.9 % | 5.67 | 5.5 MiB |

- `page-128`: 1,562 agree-twice checks suppressed as vacuous
- `page-256`: 1,562 agree-twice checks suppressed as vacuous
- `page-512`: 1,562 agree-twice checks suppressed as vacuous
- `page-1k`: 1,562 agree-twice checks suppressed as vacuous
- `page-2k`: 1,562 agree-twice checks suppressed as vacuous
- `page-4k`: 1,562 agree-twice checks suppressed as vacuous
- `page-16k`: 1,562 agree-twice checks suppressed as vacuous
- `span`: 1,554 span fetches; 1,562 agree-twice checks suppressed as vacuous
- `hybrid-1k`: 1,554 span fetches; 1,562 agree-twice checks suppressed as vacuous
- `hybrid-4k`: 1,554 span fetches; 1,562 agree-twice checks suppressed as vacuous
- `hybrid-4k+text`: 1,102 span fetches; 1,562 agree-twice checks suppressed as vacuous

### live pid 418440 (1562 nodes) / geometry

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 18,200 | 1.000x | 145,800 | 1.00x | 145,800 | 1.00x | 0.0 % | 11.55 | 0 B |
| page-128 | 7,200 | 0.396x | 921,600 | 6.32x | 145,800 | 6.32x | 60.4 % | 6.08 | 4 KiB |
| page-256 | 7,200 | 0.396x | 1,843,200 | 12.64x | 145,800 | 12.64x | 60.4 % | 5.95 | 9 KiB |
| page-512 | 7,000 | 0.385x | 3,584,000 | 24.58x | 145,800 | 24.58x | 61.5 % | 5.99 | 18 KiB |
| page-1k | 6,600 | 0.363x | 6,758,400 | 46.35x | 145,800 | 46.35x | 63.7 % | 6.54 | 33 KiB |
| page-2k | 3,800 | 0.209x | 7,782,400 | 53.38x | 145,800 | 53.38x | 79.1 % | 5.46 | 38 KiB |
| page-4k | 3,400 | 0.187x | 13,926,400 | 95.52x | 145,800 | 95.52x | 81.3 % | 5.53 | 68 KiB |
| page-16k | 3,400 | 0.187x | 45,875,200 | 314.64x | 145,800 | 314.64x | 85.7 % | 8.16 | 208 KiB |
| span | 3,400 | 0.187x | 4,556,800 | 31.25x | 145,800 | 31.25x | 81.3 % | 6.33 | 22 KiB |
| hybrid-1k | 3,400 | 0.187x | 4,556,800 | 31.25x | 145,800 | 31.25x | 81.3 % | 7.25 | 22 KiB |
| hybrid-4k | 3,400 | 0.187x | 4,556,800 | 31.25x | 145,800 | 31.25x | 81.3 % | 5.89 | 22 KiB |
| hybrid-4k+text | 3,400 | 0.187x | 9,523,200 | 65.32x | 145,800 | 65.32x | 81.3 % | 7.28 | 43 KiB |

- `span`: 3,400 span fetches
- `hybrid-1k`: 3,400 span fetches
- `hybrid-4k`: 3,400 span fetches
- `hybrid-4k+text`: 3,400 span fetches

### live pid 418440 (1562 nodes) / poll

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 105,740 | 1.000x | 889,140 | 1.00x | 889,140 | 1.00x | 0.0 % | 68.24 | 0 B |
| page-128 | 8,320 | 0.079x | 1,064,960 | 1.20x | 862,100 | 1.24x | 91.9 % | 10.98 | 52 KiB |
| page-256 | 7,180 | 0.068x | 1,838,080 | 2.07x | 862,100 | 2.13x | 93.0 % | 10.25 | 90 KiB |
| page-512 | 5,980 | 0.057x | 3,061,760 | 3.44x | 862,100 | 3.55x | 94.2 % | 9.87 | 150 KiB |
| page-1k | 4,800 | 0.045x | 4,915,200 | 5.53x | 862,100 | 5.70x | 95.3 % | 10.94 | 240 KiB |
| page-2k | 3,340 | 0.032x | 6,840,320 | 7.69x | 862,100 | 7.93x | 96.7 % | 8.14 | 334 KiB |
| page-4k | 2,340 | 0.022x | 9,584,640 | 10.78x | 862,100 | 11.12x | 97.7 % | 7.91 | 468 KiB |
| page-16k | 1,440 | 0.014x | 20,643,840 | 23.22x | 862,100 | 23.95x | 98.8 % | 7.82 | 960 KiB |
| span | 4,120 | 0.039x | 2,263,040 | 2.55x | 862,100 | 2.63x | 96.0 % | 8.74 | 109 KiB |
| hybrid-1k | 3,460 | 0.033x | 4,008,960 | 4.51x | 862,100 | 4.65x | 96.6 % | 8.88 | 195 KiB |
| hybrid-4k | 2,760 | 0.026x | 7,449,600 | 8.38x | 862,100 | 8.64x | 97.3 % | 9.83 | 364 KiB |
| hybrid-4k+text | 2,760 | 0.026x | 9,489,920 | 10.67x | 862,100 | 11.01x | 97.3 % | 10.17 | 436 KiB |

- `page-128`: 1,140 agree-twice checks suppressed as vacuous
- `page-256`: 1,140 agree-twice checks suppressed as vacuous
- `page-512`: 1,140 agree-twice checks suppressed as vacuous
- `page-1k`: 1,140 agree-twice checks suppressed as vacuous
- `page-2k`: 1,140 agree-twice checks suppressed as vacuous
- `page-4k`: 1,140 agree-twice checks suppressed as vacuous
- `page-16k`: 1,140 agree-twice checks suppressed as vacuous
- `span`: 1,400 span fetches; 1,140 agree-twice checks suppressed as vacuous
- `hybrid-1k`: 1,400 span fetches; 1,140 agree-twice checks suppressed as vacuous
- `hybrid-4k`: 1,400 span fetches; 1,140 agree-twice checks suppressed as vacuous
- `hybrid-4k+text`: 1,400 span fetches; 1,140 agree-twice checks suppressed as vacuous

### synthetic/sequential / walk

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 23,406 | 1.000x | 252,796 | 1.00x | 252,796 | 1.00x | 0.0 % | 1.30 | 0 B |
| page-128 | 6,731 | 0.288x | 861,568 | 3.41x | 196,628 | 4.38x | 60.3 % | 1.70 | 841 KiB |
| page-256 | 4,536 | 0.194x | 1,161,216 | 4.59x | 196,628 | 5.91x | 72.8 % | 1.73 | 1.1 MiB |
| page-512 | 3,439 | 0.147x | 1,760,768 | 6.97x | 196,628 | 8.95x | 79.2 % | 1.72 | 1.7 MiB |
| page-1k | 2,890 | 0.123x | 2,959,360 | 11.71x | 196,628 | 15.05x | 82.4 % | 1.76 | 2.8 MiB |
| page-2k | 1,646 | 0.070x | 3,371,008 | 13.33x | 196,628 | 17.14x | 90.0 % | 1.71 | 3.2 MiB |
| page-4k | 823 | 0.035x | 3,371,008 | 13.33x | 196,628 | 17.14x | 95.0 % | 1.65 | 3.2 MiB |
| page-16k | 210 | 0.009x | 3,391,488 | 13.42x | 196,628 | 17.25x | 98.7 % | 1.55 | 3.2 MiB |
| span | 4,389 | 0.188x | 3,409,536 | 13.49x | 196,628 | 17.34x | 74.1 % | 2.68 | 3.2 MiB |
| hybrid-1k | 2,597 | 0.111x | 3,408,384 | 13.48x | 196,628 | 17.33x | 84.7 % | 2.54 | 3.2 MiB |
| hybrid-4k | 2,405 | 0.103x | 3,408,384 | 13.48x | 196,628 | 17.33x | 85.8 % | 2.51 | 3.2 MiB |
| hybrid-4k+text | 1,415 | 0.060x | 4,043,008 | 15.99x | 196,628 | 20.56x | 91.7 % | 2.85 | 3.2 MiB |

- `page-128`: 2,341 agree-twice checks suppressed as vacuous
- `page-256`: 2,341 agree-twice checks suppressed as vacuous
- `page-512`: 2,341 agree-twice checks suppressed as vacuous
- `page-1k`: 2,341 agree-twice checks suppressed as vacuous
- `page-2k`: 2,341 agree-twice checks suppressed as vacuous
- `page-4k`: 2,341 agree-twice checks suppressed as vacuous
- `page-16k`: 2,341 agree-twice checks suppressed as vacuous
- `span`: 2,341 span fetches; 2,341 agree-twice checks suppressed as vacuous
- `hybrid-1k`: 2,341 span fetches; 2,341 agree-twice checks suppressed as vacuous
- `hybrid-4k`: 2,341 span fetches; 2,341 agree-twice checks suppressed as vacuous
- `hybrid-4k+text`: 1,351 span fetches; 2,341 agree-twice checks suppressed as vacuous

### synthetic/sequential / geometry

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 4,200 | 1.000x | 33,800 | 1.00x | 33,800 | 1.00x | 0.0 % | 0.18 | 0 B |
| page-128 | 1,400 | 0.333x | 179,200 | 5.30x | 33,800 | 5.30x | 66.7 % | 0.34 | 896 B |
| page-256 | 1,200 | 0.286x | 307,200 | 9.09x | 33,800 | 9.09x | 71.4 % | 0.35 | 2 KiB |
| page-512 | 1,000 | 0.238x | 512,000 | 15.15x | 33,800 | 15.15x | 76.2 % | 0.35 | 2 KiB |
| page-1k | 800 | 0.190x | 819,200 | 24.24x | 33,800 | 24.24x | 81.0 % | 0.36 | 4 KiB |
| page-2k | 400 | 0.095x | 819,200 | 24.24x | 33,800 | 24.24x | 90.5 % | 0.34 | 4 KiB |
| page-4k | 400 | 0.095x | 1,638,400 | 48.47x | 33,800 | 48.47x | 90.5 % | 0.41 | 8 KiB |
| page-16k | 400 | 0.095x | 6,553,600 | 193.89x | 33,800 | 193.89x | 90.5 % | 0.64 | 32 KiB |
| span | 600 | 0.143x | 819,200 | 24.24x | 33,800 | 24.24x | 85.7 % | 0.42 | 4 KiB |
| hybrid-1k | 600 | 0.143x | 819,200 | 24.24x | 33,800 | 24.24x | 85.7 % | 0.42 | 4 KiB |
| hybrid-4k | 600 | 0.143x | 819,200 | 24.24x | 33,800 | 24.24x | 85.7 % | 0.44 | 4 KiB |
| hybrid-4k+text | 600 | 0.143x | 1,689,600 | 49.99x | 33,800 | 49.99x | 85.7 % | 0.59 | 5 KiB |

- `span`: 600 span fetches
- `hybrid-1k`: 600 span fetches
- `hybrid-4k`: 600 span fetches
- `hybrid-4k+text`: 600 span fetches

### synthetic/sequential / poll

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 2,480 | 1.000x | 22,640 | 1.00x | 22,640 | 1.00x | 0.0 % | 0.10 | 0 B |
| page-128 | 620 | 0.250x | 79,360 | 3.51x | 20,080 | 3.95x | 71.8 % | 0.15 | 4 KiB |
| page-256 | 460 | 0.185x | 117,760 | 5.20x | 20,080 | 5.86x | 78.9 % | 0.15 | 6 KiB |
| page-512 | 340 | 0.137x | 174,080 | 7.69x | 20,080 | 8.67x | 84.4 % | 0.18 | 8 KiB |
| page-1k | 220 | 0.089x | 225,280 | 9.95x | 20,080 | 11.22x | 89.9 % | 0.18 | 11 KiB |
| page-2k | 140 | 0.056x | 286,720 | 12.66x | 20,080 | 14.28x | 93.6 % | 0.18 | 14 KiB |
| page-4k | 100 | 0.040x | 409,600 | 18.09x | 20,080 | 20.40x | 95.4 % | 0.18 | 20 KiB |
| page-16k | 60 | 0.024x | 983,040 | 43.42x | 20,080 | 48.96x | 97.2 % | 0.20 | 48 KiB |
| span | 240 | 0.097x | 176,640 | 7.80x | 20,080 | 8.80x | 89.1 % | 0.22 | 8 KiB |
| hybrid-1k | 160 | 0.065x | 202,240 | 8.93x | 20,080 | 10.07x | 92.7 % | 0.23 | 10 KiB |
| hybrid-4k | 160 | 0.065x | 325,120 | 14.36x | 20,080 | 16.19x | 92.7 % | 0.27 | 16 KiB |
| hybrid-4k+text | 120 | 0.048x | 386,560 | 17.07x | 20,080 | 19.25x | 94.5 % | 0.27 | 17 KiB |

- `page-128`: 120 agree-twice checks suppressed as vacuous
- `page-256`: 120 agree-twice checks suppressed as vacuous
- `page-512`: 120 agree-twice checks suppressed as vacuous
- `page-1k`: 120 agree-twice checks suppressed as vacuous
- `page-2k`: 120 agree-twice checks suppressed as vacuous
- `page-4k`: 120 agree-twice checks suppressed as vacuous
- `page-16k`: 120 agree-twice checks suppressed as vacuous
- `span`: 120 span fetches; 120 agree-twice checks suppressed as vacuous
- `hybrid-1k`: 120 span fetches; 120 agree-twice checks suppressed as vacuous
- `hybrid-4k`: 120 span fetches; 120 agree-twice checks suppressed as vacuous
- `hybrid-4k+text`: 80 span fetches; 120 agree-twice checks suppressed as vacuous

### synthetic/clustered / walk

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 23,406 | 1.000x | 252,796 | 1.00x | 252,796 | 1.00x | 0.0 % | 1.39 | 0 B |
| page-128 | 6,743 | 0.288x | 863,104 | 3.41x | 196,628 | 4.39x | 60.3 % | 2.17 | 843 KiB |
| page-256 | 4,545 | 0.194x | 1,163,520 | 4.60x | 196,628 | 5.92x | 72.8 % | 1.86 | 1.1 MiB |
| page-512 | 3,459 | 0.148x | 1,771,008 | 7.01x | 196,628 | 9.01x | 79.1 % | 2.12 | 1.7 MiB |
| page-1k | 2,912 | 0.124x | 2,981,888 | 11.80x | 196,628 | 15.17x | 82.3 % | 1.88 | 2.8 MiB |
| page-2k | 1,654 | 0.071x | 3,387,392 | 13.40x | 196,628 | 17.23x | 89.9 % | 1.78 | 3.2 MiB |
| page-4k | 831 | 0.036x | 3,403,776 | 13.46x | 196,628 | 17.31x | 94.9 % | 1.72 | 3.2 MiB |
| page-16k | 249 | 0.011x | 3,637,248 | 14.39x | 196,628 | 18.50x | 98.7 % | 1.69 | 3.3 MiB |
| span | 4,402 | 0.188x | 3,411,712 | 13.50x | 196,628 | 17.35x | 74.1 % | 2.75 | 3.2 MiB |
| hybrid-1k | 2,611 | 0.112x | 3,421,440 | 13.53x | 196,628 | 17.40x | 84.6 % | 2.59 | 3.2 MiB |
| hybrid-4k | 2,405 | 0.103x | 3,451,520 | 13.65x | 196,628 | 17.55x | 85.8 % | 2.40 | 3.2 MiB |
| hybrid-4k+text | 1,446 | 0.062x | 4,148,352 | 16.41x | 196,628 | 21.10x | 91.5 % | 2.49 | 3.2 MiB |

- `page-128`: 2,341 agree-twice checks suppressed as vacuous
- `page-256`: 2,341 agree-twice checks suppressed as vacuous
- `page-512`: 2,341 agree-twice checks suppressed as vacuous
- `page-1k`: 2,341 agree-twice checks suppressed as vacuous
- `page-2k`: 2,341 agree-twice checks suppressed as vacuous
- `page-4k`: 2,341 agree-twice checks suppressed as vacuous
- `page-16k`: 2,341 agree-twice checks suppressed as vacuous
- `span`: 2,341 span fetches; 2,341 agree-twice checks suppressed as vacuous
- `hybrid-1k`: 2,341 span fetches; 2,341 agree-twice checks suppressed as vacuous
- `hybrid-4k`: 2,325 span fetches; 2,341 agree-twice checks suppressed as vacuous
- `hybrid-4k+text`: 1,367 span fetches; 2,341 agree-twice checks suppressed as vacuous

### synthetic/clustered / geometry

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 3,200 | 1.000x | 25,800 | 1.00x | 25,800 | 1.00x | 0.0 % | 0.12 | 0 B |
| page-128 | 1,000 | 0.312x | 128,000 | 4.96x | 25,800 | 4.96x | 68.8 % | 0.21 | 640 B |
| page-256 | 800 | 0.250x | 204,800 | 7.94x | 25,800 | 7.94x | 75.0 % | 0.22 | 1 KiB |
| page-512 | 800 | 0.250x | 409,600 | 15.88x | 25,800 | 15.88x | 75.0 % | 0.23 | 2 KiB |
| page-1k | 600 | 0.188x | 614,400 | 23.81x | 25,800 | 23.81x | 81.2 % | 0.23 | 3 KiB |
| page-2k | 400 | 0.125x | 819,200 | 31.75x | 25,800 | 31.75x | 87.5 % | 0.25 | 4 KiB |
| page-4k | 400 | 0.125x | 1,638,400 | 63.50x | 25,800 | 63.50x | 87.5 % | 0.28 | 8 KiB |
| page-16k | 400 | 0.125x | 6,553,600 | 254.02x | 25,800 | 254.02x | 87.5 % | 0.45 | 32 KiB |
| span | 400 | 0.125x | 537,600 | 20.84x | 25,800 | 20.84x | 87.5 % | 0.32 | 3 KiB |
| hybrid-1k | 400 | 0.125x | 537,600 | 20.84x | 25,800 | 20.84x | 87.5 % | 0.32 | 3 KiB |
| hybrid-4k | 400 | 0.125x | 537,600 | 20.84x | 25,800 | 20.84x | 87.5 % | 0.33 | 3 KiB |
| hybrid-4k+text | 400 | 0.125x | 1,126,400 | 43.66x | 25,800 | 43.66x | 87.5 % | 0.39 | 4 KiB |

- `span`: 400 span fetches
- `hybrid-1k`: 400 span fetches
- `hybrid-4k`: 400 span fetches
- `hybrid-4k+text`: 400 span fetches

### synthetic/clustered / poll

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 2,120 | 1.000x | 19,340 | 1.00x | 19,340 | 1.00x | 0.0 % | 0.08 | 0 B |
| page-128 | 620 | 0.292x | 79,360 | 4.10x | 17,260 | 4.60x | 67.4 % | 0.13 | 4 KiB |
| page-256 | 480 | 0.226x | 122,880 | 6.35x | 17,260 | 7.12x | 74.5 % | 0.13 | 6 KiB |
| page-512 | 360 | 0.170x | 184,320 | 9.53x | 17,260 | 10.68x | 80.6 % | 0.13 | 9 KiB |
| page-1k | 220 | 0.104x | 225,280 | 11.65x | 17,260 | 13.05x | 88.2 % | 0.12 | 11 KiB |
| page-2k | 140 | 0.066x | 286,720 | 14.83x | 17,260 | 16.61x | 92.5 % | 0.12 | 14 KiB |
| page-4k | 100 | 0.047x | 409,600 | 21.18x | 17,260 | 23.73x | 94.6 % | 0.13 | 20 KiB |
| page-16k | 160 | 0.075x | 1,638,400 | 84.72x | 17,260 | 94.92x | 95.7 % | 0.16 | 64 KiB |
| span | 280 | 0.132x | 209,920 | 10.85x | 17,260 | 12.16x | 85.3 % | 0.16 | 10 KiB |
| hybrid-1k | 180 | 0.085x | 230,400 | 11.91x | 17,260 | 13.35x | 90.5 % | 0.16 | 11 KiB |
| hybrid-4k | 180 | 0.085x | 353,280 | 18.27x | 17,260 | 20.47x | 90.5 % | 0.19 | 17 KiB |
| hybrid-4k+text | 140 | 0.066x | 442,880 | 22.90x | 17,260 | 25.66x | 92.6 % | 0.18 | 18 KiB |

- `page-128`: 100 agree-twice checks suppressed as vacuous
- `page-256`: 100 agree-twice checks suppressed as vacuous
- `page-512`: 100 agree-twice checks suppressed as vacuous
- `page-1k`: 100 agree-twice checks suppressed as vacuous
- `page-2k`: 100 agree-twice checks suppressed as vacuous
- `page-4k`: 100 agree-twice checks suppressed as vacuous
- `page-16k`: 100 agree-twice checks suppressed as vacuous
- `span`: 140 span fetches; 100 agree-twice checks suppressed as vacuous
- `hybrid-1k`: 140 span fetches; 100 agree-twice checks suppressed as vacuous
- `hybrid-4k`: 140 span fetches; 100 agree-twice checks suppressed as vacuous
- `hybrid-4k+text`: 100 span fetches; 100 agree-twice checks suppressed as vacuous

### synthetic/scattered / walk

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 23,406 | 1.000x | 252,796 | 1.00x | 252,796 | 1.00x | 0.0 % | 1.28 | 0 B |
| page-128 | 6,936 | 0.296x | 887,808 | 3.51x | 196,628 | 4.52x | 59.2 % | 1.83 | 867 KiB |
| page-256 | 4,606 | 0.197x | 1,179,136 | 4.66x | 196,628 | 6.00x | 72.4 % | 1.88 | 1.1 MiB |
| page-512 | 3,711 | 0.159x | 1,900,032 | 7.52x | 196,628 | 9.66x | 77.6 % | 1.92 | 1.8 MiB |
| page-1k | 3,207 | 0.137x | 3,283,968 | 12.99x | 196,628 | 16.70x | 80.5 % | 1.93 | 3.1 MiB |
| page-2k | 1,772 | 0.076x | 3,629,056 | 14.36x | 196,628 | 18.46x | 89.2 % | 1.80 | 3.5 MiB |
| page-4k | 946 | 0.040x | 3,874,816 | 15.33x | 196,628 | 19.71x | 94.2 % | 1.82 | 3.7 MiB |
| page-16k | 960 | 0.041x | 7,716,864 | 30.53x | 196,628 | 39.25x | 98.1 % | 1.80 | 4.8 MiB |
| span | 4,577 | 0.196x | 3,428,096 | 13.56x | 196,628 | 17.43x | 73.1 % | 2.82 | 3.2 MiB |
| hybrid-1k | 2,860 | 0.122x | 3,667,968 | 14.51x | 196,628 | 18.65x | 83.2 % | 3.29 | 3.3 MiB |
| hybrid-4k | 2,381 | 0.102x | 4,094,336 | 16.20x | 196,628 | 20.82x | 86.0 % | 2.86 | 3.7 MiB |
| hybrid-4k+text | 1,651 | 0.071x | 4,927,744 | 19.49x | 196,628 | 25.06x | 90.3 % | 3.06 | 3.6 MiB |

- `page-128`: 2,341 agree-twice checks suppressed as vacuous
- `page-256`: 2,341 agree-twice checks suppressed as vacuous
- `page-512`: 2,341 agree-twice checks suppressed as vacuous
- `page-1k`: 2,341 agree-twice checks suppressed as vacuous
- `page-2k`: 2,341 agree-twice checks suppressed as vacuous
- `page-4k`: 2,341 agree-twice checks suppressed as vacuous
- `page-16k`: 2,341 agree-twice checks suppressed as vacuous
- `span`: 2,341 span fetches; 2,341 agree-twice checks suppressed as vacuous
- `hybrid-1k`: 2,341 span fetches; 2,341 agree-twice checks suppressed as vacuous
- `hybrid-4k`: 2,051 span fetches; 2,341 agree-twice checks suppressed as vacuous
- `hybrid-4k+text`: 1,399 span fetches; 2,341 agree-twice checks suppressed as vacuous

### synthetic/scattered / geometry

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 4,200 | 1.000x | 33,800 | 1.00x | 33,800 | 1.00x | 0.0 % | 0.13 | 0 B |
| page-128 | 1,200 | 0.286x | 153,600 | 4.54x | 33,800 | 4.54x | 71.4 % | 0.26 | 768 B |
| page-256 | 1,200 | 0.286x | 307,200 | 9.09x | 33,800 | 9.09x | 71.4 % | 0.27 | 2 KiB |
| page-512 | 1,000 | 0.238x | 512,000 | 15.15x | 33,800 | 15.15x | 76.2 % | 0.29 | 2 KiB |
| page-1k | 1,000 | 0.238x | 1,024,000 | 30.30x | 33,800 | 30.30x | 76.2 % | 0.31 | 5 KiB |
| page-2k | 800 | 0.190x | 1,638,400 | 48.47x | 33,800 | 48.47x | 81.0 % | 0.31 | 8 KiB |
| page-4k | 600 | 0.143x | 2,457,600 | 72.71x | 33,800 | 72.71x | 85.7 % | 0.35 | 12 KiB |
| page-16k | 1,400 | 0.333x | 13,107,200 | 387.79x | 33,800 | 387.79x | 85.7 % | 0.59 | 48 KiB |
| span | 600 | 0.143x | 793,600 | 23.48x | 33,800 | 23.48x | 85.7 % | 0.40 | 4 KiB |
| hybrid-1k | 600 | 0.143x | 793,600 | 23.48x | 33,800 | 23.48x | 85.7 % | 0.43 | 4 KiB |
| hybrid-4k | 600 | 0.143x | 793,600 | 23.48x | 33,800 | 23.48x | 85.7 % | 0.40 | 4 KiB |
| hybrid-4k+text | 600 | 0.143x | 1,689,600 | 49.99x | 33,800 | 49.99x | 85.7 % | 0.58 | 8 KiB |

- `span`: 600 span fetches
- `hybrid-1k`: 600 span fetches
- `hybrid-4k`: 600 span fetches
- `hybrid-4k+text`: 600 span fetches

### synthetic/scattered / poll

| variant | syscalls | syscalls vs base | bytes read | bytes vs base | useful bytes | amplification | hit rate | wall ms | retained |
|---|--:|--:|--:|--:|--:|--:|--:|--:|--:|
| uncached | 2,380 | 1.000x | 21,840 | 1.00x | 21,840 | 1.00x | 0.0 % | 0.10 | 0 B |
| page-128 | 840 | 0.353x | 107,520 | 4.92x | 19,280 | 5.58x | 60.0 % | 0.15 | 5 KiB |
| page-256 | 680 | 0.286x | 174,080 | 7.97x | 19,280 | 9.03x | 67.0 % | 0.16 | 8 KiB |
| page-512 | 640 | 0.269x | 327,680 | 15.00x | 19,280 | 17.00x | 68.9 % | 0.17 | 16 KiB |
| page-1k | 580 | 0.244x | 593,920 | 27.19x | 19,280 | 30.80x | 71.8 % | 0.21 | 29 KiB |
| page-2k | 500 | 0.210x | 1,024,000 | 46.89x | 19,280 | 53.11x | 75.7 % | 0.24 | 50 KiB |
| page-4k | 480 | 0.202x | 1,966,080 | 90.02x | 19,280 | 101.98x | 76.7 % | 0.31 | 96 KiB |
| page-16k | 1,520 | 0.639x | 12,124,160 | 555.14x | 19,280 | 628.85x | 76.7 % | 0.59 | 384 KiB |
| span | 520 | 0.218x | 243,200 | 11.14x | 19,280 | 12.61x | 75.2 % | 0.24 | 12 KiB |
| hybrid-1k | 480 | 0.202x | 537,600 | 24.62x | 19,280 | 27.88x | 77.1 % | 0.32 | 26 KiB |
| hybrid-4k | 480 | 0.202x | 1,582,080 | 72.44x | 19,280 | 82.06x | 77.1 % | 0.43 | 77 KiB |
| hybrid-4k+text | 480 | 0.202x | 1,786,880 | 81.82x | 19,280 | 92.68x | 77.1 % | 0.56 | 87 KiB |

- `page-128`: 120 agree-twice checks suppressed as vacuous
- `page-256`: 120 agree-twice checks suppressed as vacuous
- `page-512`: 120 agree-twice checks suppressed as vacuous
- `page-1k`: 120 agree-twice checks suppressed as vacuous
- `page-2k`: 120 agree-twice checks suppressed as vacuous
- `page-4k`: 120 agree-twice checks suppressed as vacuous
- `page-16k`: 120 agree-twice checks suppressed as vacuous
- `span`: 140 span fetches; 120 agree-twice checks suppressed as vacuous
- `hybrid-1k`: 140 span fetches; 120 agree-twice checks suppressed as vacuous
- `hybrid-4k`: 140 span fetches; 120 agree-twice checks suppressed as vacuous
- `hybrid-4k+text`: 140 span fetches; 120 agree-twice checks suppressed as vacuous

CSV written to bench/results.csv

## the invalidation trap

A synthetic target whose `size_cache` changes on **every read**, so any check that
works by reading twice must see a difference — unless something is serving the second
read from the first read's bytes.

### 1. agree-twice, inside a coherent snapshot (handled)

- uncached walk: 1,196 logical reads, both traversals performed
- snapshotted walk: 598 logical reads
- `AgreeTwiceSuppressed` = 200

The second traversal would have read the same frozen bytes and could only have agreed.
`ChildListWalk.WalkStable` asks `IsCoherent` and declines to run it, so the mitigation is
*replaced* by the stronger one rather than silently cancelled by it (§6.4).

### 2. a hand-written "read it twice" check (detected, not prevented)

- uncached: the two readings agreed? **False** (correct — the value is changing)
- snapshotted: the two readings agreed? **True** (the check has been defeated)
- `RepeatedReads` = 1 with `DetectRepeatedReads` on

This is the calibrator's bug reproduced deliberately. The library cannot know that a
caller's second read was *meant* to observe change, so it counts them instead: a non-zero
`RepeatedReads` says "you read some address twice and got the same answer by construction".

### 3. a snapshot per poll (correct usage)

- three polls, three snapshots: sizes 1796, 1797, 1798
- distinct values: 3 of 3 — each poll observed its own moment

### 4. one snapshot held across polls (**the misuse that survives**)

- three polls, one snapshot: sizes 1799, 1799, 1799
- distinct values: 1 of 3 — polls two and three saw poll one
- `IsStale` = True, `StaleReads` = 3, `RepeatedReads` = 2, age 0.3 ms
- opening a second snapshot while this one is live was refused: **True**

Nothing here prevents this. A caller who never opens a second snapshot and never looks at
`IsStale` gets the first poll's data forever, silently. The scope, the one-at-a-time rule
and the counters make it awkward and observable; they do not make it impossible.

