# Kandy Routing Engine

A routing and network-analysis engine for Kandy, Sri Lanka, written in C# (.NET 8) with no graph libraries. It answers four kinds of question on a road network of 128 locations and 215 roads:

- the best route between two places (shortest distance, fastest time, or a balance of both);
- the best route when roads, junctions or road types must be avoided, including live incidents;
- which places can be reached within a given number of minutes;
- whether the network is fully connected, and which junctions and roads are critical.

The main use case is an emergency trip to National Hospital Kandy; the second is a commuter trip. A benchmark times every algorithm on synthetic networks of 128 to 5,000 locations, and two Python scripts draw the charts and route maps.

## Requirements covered

| # | Requirement | Where |
|---|---|---|
| FR1 | Network representation | `RoadNetwork`: adjacency list (plus a reverse list for "who can reach me") |
| FR2 | Optimal path by distance, time or balanced | `RoutePlanner`: Dijkstra with own `MinHeap`; Dijkstra with linear scan and BFS for comparison |
| FR3 | Constrained routing | Same planner; `RouteRequest` holds avoided places, road ids and road types; incidents block or slow roads |
| FR4 | Reachability within a budget | `NetworkAnalyser.Reachable`: Dijkstra that stops when the cost passes the budget |
| FR5 | Connectivity and bottlenecks | `NetworkAnalyser`: DFT and BFT (forward and reversed), remove-and-test for critical junctions and roads |
| FR6 | Performance evaluation | `NetworkGenerator` + `Benchmark` -> `results/benchmark.csv` -> charts |

## Project structure

```text
src/Scenario1Project/
  Location.cs  Road.cs  Incident.cs      plain data classes
  RoadNetwork.cs                         adjacency list, incidents applied as flags
  NetworkLoader.cs                       reads and validates the three CSV files
  MinHeap.cs                             own binary min-heap (priority queue)
  RouteRequest.cs                        trip: start, destination, metric, things to avoid
  RoutePlanner.cs                        BFS, Dijkstra (heap), Dijkstra (linear scan)
  NetworkAnalyser.cs                     reachability, connectivity, critical junctions/roads
  RouteExporter.cs                       writes CSVs for the route maps
  NetworkGenerator.cs  Benchmark.cs      synthetic networks and timing
  TestRunner.cs                          44 checks, prints PASS/FAIL
  Program.cs                             console menu
data/        kandy_nodes.csv  kandy_edges.csv  kandy_events.csv   (generated/ is rebuilt on demand)
scripts/     plot_results.py  plot_route.py                       (matplotlib)
tools/       recalibrate_edges.py                                 (data preparation)
results/     benchmark.csv, charts, route maps
```

## Dataset

Three CSV files in `data/`:

| File | Rows | One row is |
|---|---|---|
| `kandy_nodes.csv` | 128 | a location: id, name, type (junction, hospital, bridge end, ...), latitude, longitude |
| `kandy_edges.csv` | 215 | a road: from, to, distance (km), base time (min), speed limit, road type (A, B, minor, bridge), one-way flag |
| `kandy_events.csv` | 10 | an incident (roadblock, accident or traffic) on a road or junction, switched on and off from the menu |

176 roads are two-way and 39 are one-way, which gives 391 directed roads. Places, road layout, one-way streets and bridges come from OpenStreetMap. Distances are estimates: the straight-line distance times a winding factor (1.7 in town, 1.25 outside). Times are the distance divided by an average speed for the road type. Both were checked against Google Maps driving directions and adjusted. Incidents are kept in their own file so the road data never changes; they are applied when the program runs.

## How to run

Requires the .NET 8 SDK. Python 3 with matplotlib is needed only for the charts and maps. Run every command from the project folder.

| Task | Command |
|---|---|
| Build | `dotnet build -c Release` |
| Menu | `dotnet run --project src/Scenario1Project` |
| Tests | `dotnet run --project src/Scenario1Project -- test` (also menu option 8) |
| Benchmark | `dotnet run --project src/Scenario1Project -c Release -- bench` |
| Benchmark charts | `python scripts/plot_results.py` |
| Route map | `python scripts/plot_route.py results/route_normal.csv --network results/network_normal.csv` |
| Before / after map | `python scripts/plot_route.py results/route_normal.csv results/route_blocked.csv --network results/network_blocked.csv` |
| Reach map | `python scripts/plot_route.py --reach results/reach_hospital_15.csv --network results/network_normal.csv` |

Menu: 1 route · 2 route with restrictions · 3 compare algorithms on one trip · 4 reachable within N minutes · 5 network health · 6 incidents (switch on/off) · 7 export for the map · 8 tests · 9 benchmark.

Always benchmark in Release mode: a Debug build gives times that are too slow to compare, and the menu warns about it. The route-map CSVs are written by menu option 7 and are not tracked in git, except the `results/*.png` pictures.

Incidents are read from `data/kandy_events.csv` (road block, accident, traffic, with an `active` flag) and applied at run time as flags on the loaded network. The data files are never modified; switching an incident on in menu option 6 changes the next search immediately.

## Results

All numbers below come from the program. Tests: **44 / 44 pass**.

### Routing and incidents (Clock Tower -> National Hospital Kandy)

| Case | Result |
|---|---|
| Normal, by time | 3 roads, 1.1 km, 4.8 min |
| Traffic incident E8 on the last road (x2) | same route, 7.8 min |
| Closing road 20-94 (on the best route) | new route, 7.9 min |
| Avoid the hospital itself | "No route: the destination is closed or avoided" |
| Block all 7 Mahaweli bridges | a place across the river is unreachable; network reported not connected |
| Same trip, three algorithms (test 3) | BFS has the fewest roads but never beats Dijkstra on km or minutes; both Dijkstras always return the same cost (7,296 trips checked) |

Route maps: `results/route_normal.png`, `results/route_normal_vs_route_blocked.png`.

### Reachability from National Hospital Kandy (by time)

| Budget (min) | 0 | 5 | 10 | 15 | 20 | 30 |
|---|---|---|---|---|---|---|
| Places reached (of 128) | 1 | 12 | 36 | 70 | 98 | 126 |

The count never decreases as the budget grows. Map: `results/reach_hospital_15.png`.

### Connectivity and critical infrastructure

The baseline network is fully connected: depth-first and breadth-first traversal both reach 128 / 128 places, forwards and backwards. Remove-and-test from the hospital finds **15 critical junctions and 16 critical roads**. The worst junction is **Nattarampota**, which cuts off 3 places, and the worst road is **road 58 (Police Hospital Kundasale - Nattarampota)**, also 3. Both sit on a dead-end spur, so closing them leaves places with only that single way in. No single bridge is critical (each river bank has another crossing), but closing all seven splits the network.

### Benchmark (median milliseconds for one operation, Release build)

Synthetic connected networks with about 1.7 roads per location. Each figure is the median of 5 timed repetitions after 3 warm-up runs, on the same 50 fixed trips per size.

| Operation | 128 | 250 | 500 | 1,000 | 2,000 | 5,000 |
|---|---|---|---|---|---|---|
| BFS (fewest roads) | 0.010 | 0.016 | 0.044 | 0.039 | 0.059 | 0.185 |
| Dijkstra, linear scan | 0.023 | 0.041 | 0.138 | 0.421 | 1.166 | 6.142 |
| Dijkstra, MinHeap | 0.019 | 0.026 | 0.055 | 0.077 | 0.141 | 0.410 |
| Reachable (15 min) | 0.019 | 0.027 | 0.033 | 0.013 | 0.014 | 0.026 |
| DFT | 0.026 | 0.045 | 0.063 | 0.082 | 0.174 | 0.546 |
| BFT | 0.026 | 0.061 | 0.083 | 0.102 | 0.179 | 0.554 |
| Critical junctions | 3.18 | 9.32 | 15.44 | 68.03 | skipped | skipped |
| Critical roads | 4.76 | 34.08 | 30.09 | 120.70 | skipped | skipped |
| Matrix memory (calculated) | 0.13 MB | 0.5 MB | 2 MB | 8 MB | 32 MB | 200 MB |
| List entries (V + E) | 566 | 1,116 | 2,286 | 4,488 | 8,900 | 22,390 |

Charts: `results/bench_routing.png`, `results/bench_traversal.png`, `results/bench_critical.png`, `results/bench_memory.png`.

How to read it:

- **Heap vs linear scan.** The two grow apart as the network grows: about 1.2x at 128 locations and about 15x at 5,000, which fits O((V + E) log V) against O(V^2 + E). The linear scan is not slower everywhere in theory (see limitations).
- **BFS is fastest but answers a different question.** It minimises the number of roads, not km or minutes, so its route can be longer in both.
- **Critical junctions and roads** grow roughly quadratically (O(V(V + E))), so they are only run up to 1,000 locations. Timing noise matters at the small sizes: the 250 and 500 critical-road figures are not monotonic.
- **Reachability** depends on how many places fall inside the 15-minute area, not on the total network size, so it stays flat.
- **A small-size timing artefact.** In the default Release configuration the 500-location row of BFS (and Reachable) is slower than the 1,000 row. Running the same benchmark with .NET's tiered JIT compilation switched off (`DOTNET_TieredCompilation=0`) removes the bump, which points to code still being optimised during the warm-up rather than to the algorithm. The charts therefore show a slightly noisy small-size region; the heap vs linear-scan gap is the same either way.
- The `edges` column in `benchmark.csv` counts directed roads (a two-way street counts twice).

## Complexity

V = locations, E = directed roads, d = roads leaving one location (about 3 here), V', E' = the part of the network actually reached.

| Operation | Time | Extra space |
|---|---|---|
| Add a road (with duplicate check) | O(d) | O(1) |
| Roads leaving / arriving at a place | O(d) | O(1) |
| Is there a road u -> v? | O(d) | O(1) |
| Whole network stored | | O(V + E); a matrix would be O(V^2) |
| Dijkstra with MinHeap | O((V + E) log V) | O(V + E) (heap holds up to E + 1 items because of lazy re-inserts) |
| Dijkstra with linear scan | O(V^2 + E) | O(V) |
| BFS (fewest roads) | O(V + E) | O(V) |
| Constrained route | same as Dijkstra; each avoid check is O(1) | same |
| Reachable within a budget | O((V' + E') log V') | O(V') |
| Connectivity (DFT or BFT, forward + reversed) | O(V + E) | O(V) |
| Critical junctions (remove-and-test) | O(V (V + E)) | O(V) |
| Critical roads (remove-and-test) | O(E (V + E)) | O(V) |
| Apply incidents | O(V + E) to clear, then O(incidents x d) | O(1) |

## Limitations

- **Static, hand-built network.** 128 locations from OpenStreetMap places with estimated road lengths and speeds (checked against a sample in Google Maps). Real road geometry, lane counts and signals are missing, so times are approximate.
- **Incidents are manual.** There is no live traffic feed and no time-of-day model: a traffic multiplier is a fixed number from the CSV.
- **No turn restrictions or signal delays.** A junction costs nothing to cross.
- **Remove-and-test does not scale.** It is quadratic, was benchmarked only up to 1,000 locations, and each check is measured from one centre (the hospital) rather than between all pairs.
- **The dense-network claim is theoretical.** Only sparse, road-like networks were benchmarked. The expectation that the linear scan beats the heap on a dense network (E close to V^2) is derived from the complexity, not measured here.
- **Benchmark scope.** The networks are random geometric graphs, not real cities, trips are random pairs, and the 5 repetitions are few. Differences of a few microseconds should not be read as real.
- **Single route per query.** No alternative routes and no "nearest hospital" search.
- **Tests build their own small cases** and a fixed set of Kandy trips; there is no property-based or large-scale fuzz testing beyond the 7,296-trip heap-vs-linear comparison.

## Better algorithms that were not built

Only algorithms taught in the module are implemented. These are the upgrades a production system would consider:

| Option | Improvement | Trade-off |
|---|---|---|
| **A\*** | Dijkstra plus a straight-line estimate of the distance left. Usually expands fewer places on one-to-one trips. | Needs an estimate that never overestimates; a few short bridge roads are recorded slightly shorter than the straight line, so a plain distance estimate is not safe. Not always faster. |
| **Tarjan's articulation points and bridges** | Finds every critical junction and road in one pass, O(V + E) instead of O(V (V + E)). | Not in the module; the recursive version needs an explicit stack on very large maps. |
| **Strongly connected components** (Kosaraju or Tarjan) | Lists each connected group rather than a yes/no answer. | Two traversals already answer the yes/no question. |
| **Contraction hierarchies** | Precomputed shortcuts give city-wide queries in milliseconds; used by real navigation apps. | Heavy preprocessing that every incident invalidates. |
| **Compressed sparse row arrays** | Flat arrays for the adjacency list: cache-friendly for millions of roads. | Harder to update than a list. |
| **Bellman-Ford / Floyd-Warshall** | Negative weights / all pairs. | Road costs are never negative, and an all-pairs table needs a full O(V^3) rebuild after every incident. |
