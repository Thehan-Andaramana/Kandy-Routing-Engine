# Kandy Routing Engine

This is my project for Scenario 01: Intelligent Urban Routing and Network Analysis System. I built a routing engine for Kandy in C# on .NET 8, with no graph libraries, on a road network of 128 locations and 215 roads. The main trip I built it around is an emergency trip from the Clock Tower to National Hospital Kandy. The second is a commuter trip.

## What it covers

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
.vscode/     tasks.json                                           (VS Code tasks)
```

## Dataset

There are three CSV files in `data/`.

| File | Rows | One row is |
|---|---|---|
| `kandy_nodes.csv` | 128 | a location: id, name, type (junction, hospital, bridge end, ...), latitude, longitude |
| `kandy_edges.csv` | 215 | a road: from, to, distance (km), base time (min), speed limit, road type (A, B, minor, bridge), one-way flag |
| `kandy_events.csv` | 10 | an incident (roadblock, accident or traffic) on a road or junction, switched on and off from the menu |

176 roads are two-way and 39 are one-way. That gives 391 directed roads.

The places, road layout, one-way streets and bridges come from OpenStreetMap. The distances are estimates. I used the straight-line distance times a winding factor, 1.7 in town and 1.25 outside. The times are the distance divided by an average speed for the road type. I checked both against Google Maps driving directions and adjusted them.

Incidents are in their own file so the road data never changes. The program applies them when it runs.

## How to run

You need the .NET 8 SDK. Python 3 with matplotlib is only for the charts and maps. Run every command from the project folder.

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

The menu options are 1 route, 2 route with restrictions, 3 compare algorithms on one trip, 4 reachable within N minutes, 5 network health, 6 incidents, 7 export for the map, 8 tests and 9 benchmark.

Run the benchmark in Release mode. A Debug build is too slow to compare. Menu option 7 writes the CSVs for the maps, and they are not tracked in git.

### Run from VS Code

Terminal > Run Task lists these tasks.

| Task | What it does |
|---|---|
| Build | Builds the project (Ctrl+Shift+B) |
| Run (menu) | Opens the console menu |
| Run tests | Runs the 44 tests |
| Benchmark (Release) | Writes `results/benchmark.csv` |
| Generate networks | Rebuilds `data/generated/` |
| Charts | Draws the benchmark charts |
| Route map | Draws one route exported in menu option 7 |
| Reach map | Draws one reach area exported in menu option 7 |
| Before / after map | Draws two exported routes on one map |

## Results

All the numbers below come from the program. 44 / 44 tests pass.

### Routing and incidents

The trip is Clock Tower to National Hospital Kandy.

| Case | Result |
|---|---|
| Normal, by time | 3 roads, 1.1 km, 4.8 min |
| Traffic incident E8 on the last road (x2) | same route, 7.8 min |
| Closing road 20-94 (on the best route) | new route, 7.9 min |
| Avoid the hospital itself | "No route: the destination is closed or avoided" |
| Block all 7 Mahaweli bridges | a place across the river is unreachable; network reported not connected |
| Same trip, three algorithms (test 3) | BFS has the fewest roads but never beats Dijkstra on km or minutes; both Dijkstras always return the same cost (7,296 trips checked) |

The route maps are `results/route_normal.png` and `results/route_normal_vs_route_blocked.png`.

### Reachability

This is from National Hospital Kandy, by time.

| Budget (min) | 0 | 5 | 10 | 15 | 20 | 30 |
|---|---|---|---|---|---|---|
| Places reached (of 128) | 1 | 12 | 36 | 70 | 98 | 126 |

The count never goes down as the budget grows. The map is `results/reach_hospital_15.png`.

### Connectivity and critical points

The normal network is fully connected. DFT and BFT both reach 128 / 128 places, forwards and backwards.

Remove-and-test from the hospital finds 15 critical junctions and 16 critical roads. The worst junction is Nattarampota, which cuts off 3 places. The worst road is road 58, from Police Hospital Kundasale to Nattarampota, which also cuts off 3. Both are on a dead-end spur.

No single bridge is critical because each river bank has another crossing. Closing all seven splits the network.

### Benchmark

I timed each operation on synthetic connected networks with about 1.7 roads per location. Each figure is the median of 5 timed repetitions after 3 warm-up runs, in milliseconds, on a Release build. Every size uses the same 50 fixed trips.

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

The `edges` column in `benchmark.csv` counts directed roads, so a two-way street counts twice.

The charts are `results/bench_routing.png`, `results/bench_traversal.png`, `results/bench_critical.png` and `results/bench_memory.png`.

The heap and the linear scan are close on the small network, about 1.2x apart at 128 locations. At 5,000 locations the heap is about 15x faster, which is what I expected from O((V + E) log V) against O(V^2 + E). BFS is the fastest, but it counts roads and not km or minutes, so its route can be longer. Reachability stays flat because it depends on how many places fall inside the 15 minutes and not on the size of the network. I only ran the critical junction and road checks up to 1,000 locations because they grow roughly quadratically.

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

- The network is hand-built. Road lengths and speeds are estimates, so times are approximate.
- Incidents are manual. There is no live traffic feed and no time of day.
- There are no turn restrictions or signal delays.
- Remove-and-test does not scale. I only ran it up to 1,000 locations, and only from the hospital.
- I did not measure dense networks. That the linear scan beats the heap when E is close to V^2 comes from the complexity only.
- The benchmark networks are random, not real cities, and 5 repetitions is few. A few microseconds of difference means nothing.
- The 500 row for BFS and Reachable is slower than the 1,000 row. Running with `DOTNET_TieredCompilation=0` removes the bump, so I think it is the JIT and not the algorithm.
- The 250 and 500 critical road figures are out of order. That is timing noise.
- There is one route per query and no nearest hospital search.
- The tests use small cases I built and a fixed set of Kandy trips.

## Better algorithms I did not build

I only built algorithms taught in the module. These are the ones a real system would look at.

| Option | Improvement | Trade-off |
|---|---|---|
| **A\*** | Dijkstra plus a straight-line estimate of the distance left. Usually expands fewer places on one-to-one trips. | Needs an estimate that never overestimates; a few short bridge roads are recorded slightly shorter than the straight line, so a plain distance estimate is not safe. Not always faster. |
| **Tarjan's articulation points and bridges** | Finds every critical junction and road in one pass, O(V + E) instead of O(V (V + E)). | Not in the module; the recursive version needs an explicit stack on very large maps. |
| **Strongly connected components** (Kosaraju or Tarjan) | Lists each connected group rather than a yes/no answer. | Two traversals already answer the yes/no question. |
| **Contraction hierarchies** | Precomputed shortcuts give city-wide queries in milliseconds; used by real navigation apps. | Heavy preprocessing that every incident invalidates. |
| **Compressed sparse row arrays** | Flat arrays for the adjacency list: cache-friendly for millions of roads. | Harder to update than a list. |
| **Bellman-Ford / Floyd-Warshall** | Negative weights / all pairs. | Road costs are never negative, and an all-pairs table needs a full O(V^3) rebuild after every incident. |
