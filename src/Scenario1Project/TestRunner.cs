// Runs the test cases and prints PASS or FAIL for each one.
// Started from menu option 8 or with the "test" argument.

public class TestRunner
{
    const int ClockTower = 0;
    const int Hospital = 12;

    public string dataFolder;
    public int passed;
    public int total;

    public TestRunner(string dataFolder)
    {
        this.dataFolder = dataFolder;
        this.passed = 0;
        this.total = 0;
    }

    public bool RunAll()
    {
        passed = 0;
        total = 0;
        Console.WriteLine("=====Tests=====");

        // every test loads its own copy so one test cannot affect another
        RoadNetwork check = LoadKandy();
        if (check.isEmpty())
        {
            Console.WriteLine("Kandy network could not be loaded from " + dataFolder);
            Check("Load Kandy data", false, "network is empty");
            return PrintSummary();
        }

        Test01MetricsGiveValidRoutes();
        Test02HeapEqualsLinear();
        Test03BfsVsDijkstra();
        Test04StartIsDestination();
        Test05UnknownLocation();
        Test06RoadblockOnBestRoute();
        Test07TrafficOnBestRoute();
        Test08AvoidMinorRoads();
        Test09AvoidDestination();
        Test10BlockAllBridges();
        Test11ReachableBudgets();
        Test12BaselineConnected();
        Test13CriticalJunctionsAndRoads();
        Test14InactiveIncident();
        Test15OneWayRoads();

        Console.WriteLine();
        Console.WriteLine("-----Extra edge cases-----");
        TestEmptyNetwork();
        TestHeapGuards();
        TestLazyReinsertsFit();
        TestClosedJunction();
        TestRemoveAndTestRestoresFlags();
        TestClearIncidentsGivesBase();
        TestIncidentEffects();
        TestOneWayOnlyIn();
        TestBadCsvRows();
        TestNegativeBudget();
        TestAllRoadTypesAvoided();

        Console.WriteLine();
        Console.WriteLine("-----Generator and benchmark-----");
        TestGeneratedNetworkValid();
        TestGeneratorRepeatable();
        TestGeneratorSkipsExisting();
        TestGeneratorTooSmall();
        TestHeapEqualsLinearGenerated();
        TestMedianAndMemory();

        return PrintSummary();
    }

    // all three metrics give a valid route, each best on its own measure
    private void Test01MetricsGiveValidRoutes()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);

        RouteResult byDistance = planner.FastestOrShortest(new RouteRequest(ClockTower, Hospital, RouteRequest.Distance));
        RouteResult byTime = planner.FastestOrShortest(new RouteRequest(ClockTower, Hospital, RouteRequest.Time));
        RouteResult byBalanced = planner.FastestOrShortest(new RouteRequest(ClockTower, Hospital, RouteRequest.Balanced));

        bool ok = IsValidRoute(network, byDistance, ClockTower, Hospital)
                  && IsValidRoute(network, byTime, ClockTower, Hospital)
                  && IsValidRoute(network, byBalanced, ClockTower, Hospital);
        Check("#1 Clock Tower -> hospital, 3 metrics", ok,
              "distance " + Km(byDistance) + ", time " + Min(byTime) + ", balanced " + Km(byBalanced) + " / " + Min(byBalanced));

        bool best = ok && byDistance.totalKm <= byTime.totalKm + 0.000001
                       && byTime.totalMinutes <= byDistance.totalMinutes + 0.000001;
        Check("#1 Each metric wins on its own measure", best,
              "shortest " + Km(byDistance) + " vs fastest's " + Km(byTime)
              + "; fastest " + Min(byTime) + " vs shortest's " + Min(byDistance)
              + "; same path: " + SamePath(byDistance.path, byTime.path));
    }

    // both Dijkstra versions must give the same cost on many trips
    private void Test02HeapEqualsLinear()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);
        string[] metrics = { RouteRequest.Distance, RouteRequest.Time, RouteRequest.Balanced };

        int trips = 0;
        int differences = 0;
        string firstProblem = "";

        for (int start = 0; start < network.ArraySize(); start = start + 7)
        {
            for (int dest = 0; dest < network.ArraySize(); dest++)
            {
                for (int m = 0; m < metrics.Length; m++)
                {
                    RouteRequest request = new RouteRequest(start, dest, metrics[m]);
                    RouteResult heap = planner.FastestOrShortest(request);
                    RouteResult linear = planner.FastestOrShortestNoHeap(request);
                    trips++;
                    if (heap.found != linear.found || Math.Abs(heap.totalCost - linear.totalCost) > 0.000001)
                    {
                        differences++;
                        if (firstProblem == "")
                        {
                            firstProblem = start + " -> " + dest + " " + metrics[m] + ": "
                                           + heap.totalCost + " vs " + linear.totalCost;
                        }
                    }
                }
            }
        }
        string reason = trips + " trips, same cost on all";
        if (differences > 0)
        {
            reason = differences + " of " + trips + " trips differ, first: " + firstProblem;
        }
        Check("#2 Dijkstra heap = Dijkstra linear", differences == 0, reason);
    }

    // BFS has the fewest roads, Dijkstra the lowest cost
    private void Test03BfsVsDijkstra()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);

        RouteResult bfs = planner.FewestRoads(new RouteRequest(ClockTower, Hospital, RouteRequest.Time));
        RouteResult shortest = planner.FastestOrShortest(new RouteRequest(ClockTower, Hospital, RouteRequest.Distance));
        RouteResult fastest = planner.FastestOrShortest(new RouteRequest(ClockTower, Hospital, RouteRequest.Time));

        bool ok = IsValidRoute(network, bfs, ClockTower, Hospital)
                  && bfs.path.Count <= shortest.path.Count
                  && bfs.path.Count <= fastest.path.Count
                  && bfs.totalKm >= shortest.totalKm - 0.000001
                  && bfs.totalMinutes >= fastest.totalMinutes - 0.000001;
        Check("#3 BFS fewer roads, Dijkstra cheaper", ok,
              "BFS " + (bfs.path.Count - 1) + " roads " + Km(bfs) + " " + Min(bfs)
              + "; shortest " + (shortest.path.Count - 1) + " roads " + Km(shortest)
              + "; fastest " + (fastest.path.Count - 1) + " roads " + Min(fastest));
    }

    private void Test04StartIsDestination()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);
        RouteRequest request = new RouteRequest(Hospital, Hospital, RouteRequest.Time);

        RouteResult a = planner.FastestOrShortest(request);
        RouteResult b = planner.FastestOrShortestNoHeap(request);
        RouteResult c = planner.FewestRoads(request);

        bool ok = IsZeroRoute(a, Hospital) && IsZeroRoute(b, Hospital) && IsZeroRoute(c, Hospital);
        Check("#4 Start == destination", ok, "path " + a.path.Count + " location(s), cost " + a.totalCost);
    }

    private void Test05UnknownLocation()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);
        NetworkAnalyser analyser = new NetworkAnalyser(network);

        RouteResult badStart = planner.FastestOrShortest(new RouteRequest(999, Hospital, RouteRequest.Time));
        RouteResult badDest = planner.FewestRoads(new RouteRequest(ClockTower, -5, RouteRequest.Time));
        RouteResult badLinear = planner.FastestOrShortestNoHeap(new RouteRequest(999, -5, RouteRequest.Time));
        ReachResult badReach = analyser.Reachable(999, 10, RouteRequest.Time);
        int dft = analyser.DFT(999, false);

        bool ok = !badStart.found && badStart.message != ""
                  && !badDest.found && badDest.message != ""
                  && !badLinear.found && badLinear.message != ""
                  && !badReach.valid && badReach.message != ""
                  && dft == 0;
        Check("#5 Unknown location id", ok, "messages: '" + badStart.message + "', '" + badDest.message + "'");
    }

    // closing a road on the best route forces a detour
    private void Test06RoadblockOnBestRoute()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);
        RouteRequest request = new RouteRequest(ClockTower, Hospital, RouteRequest.Time);

        RouteResult before = planner.FastestOrShortest(request);
        int mid = before.path.Count / 2;
        int a = before.path[mid - 1];
        int b = before.path[mid];

        List<Incident> list = new List<Incident>();
        list.Add(new Incident("T6", "roadblock", a, b, -1, 0, true, "test"));
        network.ApplyIncidents(list, new List<string>());
        RouteResult after = planner.FastestOrShortest(request);

        bool ok = IsValidRoute(network, after, ClockTower, Hospital)
                  && !UsesStreet(after.path, a, b)
                  && after.totalCost >= before.totalCost - 0.000001
                  && !SamePath(before.path, after.path);
        Check("#6 Roadblock on the best route", ok,
              "closed " + a + "-" + b + ": " + Min(before) + " -> " + Min(after));
    }

    // heavy traffic on the best route makes the planner switch roads
    private void Test07TrafficOnBestRoute()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);
        RouteRequest request = new RouteRequest(ClockTower, Hospital, RouteRequest.Time);

        RouteResult before = planner.FastestOrShortest(request);
        int mid = before.path.Count / 2;
        int a = before.path[mid - 1];
        int b = before.path[mid];

        List<Incident> list = new List<Incident>();
        list.Add(new Incident("T7", "traffic", a, b, -1, 3.0, true, "test"));
        network.ApplyIncidents(list, new List<string>());
        RouteResult after = planner.FastestOrShortest(request);

        bool switched = !UsesStreet(after.path, a, b);
        bool slower = after.totalMinutes > before.totalMinutes + 0.000001;
        bool ok = IsValidRoute(network, after, ClockTower, Hospital)
                  && (switched || slower)
                  && after.totalMinutes >= before.totalMinutes - 0.000001;
        Check("#7 Traffic x3 on the best route", ok,
              "road " + a + "-" + b + ": " + Min(before) + " -> " + Min(after) + ", switched: " + switched);
    }

    private void Test08AvoidMinorRoads()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);

        RouteResult free = planner.FastestOrShortest(new RouteRequest(ClockTower, Hospital, RouteRequest.Time));
        RouteRequest request = new RouteRequest(ClockTower, Hospital, RouteRequest.Time);
        request.AvoidRoadType("minor");
        RouteResult result = planner.FastestOrShortest(request);

        bool ok = IsValidRoute(network, result, ClockTower, Hospital)
                  && CountRoadsOfType(network, result.path, "minor") == 0
                  && result.totalCost >= free.totalCost - 0.000001;
        Check("#8 Avoid road type minor", ok,
              "minor roads used: " + CountRoadsOfType(network, result.path, "minor")
              + " (unrestricted route used " + CountRoadsOfType(network, free.path, "minor") + "), "
              + Min(free) + " -> " + Min(result));
    }

    private void Test09AvoidDestination()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);
        RouteRequest request = new RouteRequest(ClockTower, Hospital, RouteRequest.Time);
        request.AvoidLocation(Hospital);

        RouteResult a = planner.FastestOrShortest(request);
        RouteResult b = planner.FastestOrShortestNoHeap(request);
        RouteResult c = planner.FewestRoads(request);

        bool ok = !a.found && !b.found && !c.found;
        Check("#9 Avoid the destination", ok, "'" + a.message + "'");
    }

    // with every bridge closed the two sides of the river are cut off
    private void Test10BlockAllBridges()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);
        NetworkAnalyser analyser = new NetworkAnalyser(network);

        List<Incident> list = new List<Incident>();
        HashSet<int> bridgeIds = new HashSet<int>();
        foreach (int id in network.locations.Keys)
        {
            List<Road> roads = network.RoadsFrom(id);
            for (int i = 0; i < roads.Count; i++)
            {
                Road r = roads[i];
                if (r.roadType == "bridge" && !bridgeIds.Contains(r.roadId))
                {
                    bridgeIds.Add(r.roadId);
                    list.Add(new Incident("B" + r.roadId, "roadblock", r.from, r.to, -1, 0, true, "test"));
                }
            }
        }
        List<string> messages = new List<string>();
        int applied = network.ApplyIncidents(list, messages);

        int across = -1;
        for (int id = 0; id < network.ArraySize() && across == -1; id++)
        {
            if (network.HasLocation(id))
            {
                RouteResult r = planner.FastestOrShortest(new RouteRequest(ClockTower, id, RouteRequest.Time));
                if (!r.found)
                {
                    across = id;
                }
            }
        }

        bool connected = analyser.IsConnected();

        network.ClearIncidents();
        bool foundBefore = false;
        if (across != -1)
        {
            foundBefore = planner.FastestOrShortest(new RouteRequest(ClockTower, across, RouteRequest.Time)).found;
        }

        bool ok = bridgeIds.Count > 0 && applied == bridgeIds.Count && across != -1 && foundBefore && !connected;
        Check("#10 Block all bridges", ok,
              bridgeIds.Count + " bridges closed; no route 0 -> " + across + " (route before: " + foundBefore
              + "); connected: " + connected);
    }

    // a bigger budget never reaches fewer places
    private void Test11ReachableBudgets()
    {
        RoadNetwork network = LoadKandy();
        NetworkAnalyser analyser = new NetworkAnalyser(network);
        RoutePlanner planner = new RoutePlanner(network);
        double[] budgets = { 0, 5, 10, 15, 20, 30 };

        bool ok = true;
        string counts = "";
        int previous = 0;
        ReachResult last = null;
        for (int i = 0; i < budgets.Length; i++)
        {
            ReachResult reach = analyser.Reachable(Hospital, budgets[i], RouteRequest.Time);
            counts = counts + budgets[i] + "min=" + reach.Count() + " ";
            if (!reach.valid || reach.Count() < previous)
            {
                ok = false;
            }
            for (int k = 0; k < reach.reached.Count; k++)
            {
                if (reach.reached[k].cost > budgets[i] + 0.000001)
                {
                    ok = false;
                }
            }
            previous = reach.Count();
            last = reach;
        }
        Check("#11 Reach count never decreases", ok, counts.Trim());

        ReachResult zero = analyser.Reachable(Hospital, 0, RouteRequest.Time);
        bool onlyStart = zero.Count() == 1 && zero.reached[0].locationId == Hospital && zero.reached[0].cost == 0;
        Check("#11 Budget 0 -> only the start", onlyStart, zero.Count() + " place(s)");

        int mismatches = 0;
        for (int k = 0; k < last.reached.Count; k++)
        {
            HeapItem item = last.reached[k];
            RouteResult r = planner.FastestOrShortest(new RouteRequest(Hospital, item.locationId, RouteRequest.Time));
            if (!r.found || Math.Abs(r.totalCost - item.cost) > 0.000001)
            {
                mismatches++;
            }
        }
        Check("#11 Reach costs = Dijkstra costs", mismatches == 0,
              last.Count() + " places at 30 min checked, " + mismatches + " different");
    }

    private void Test12BaselineConnected()
    {
        RoadNetwork network = LoadKandy();
        NetworkAnalyser analyser = new NetworkAnalyser(network);
        int v = network.LocationCount();

        int dftF = analyser.DFT(0, false);
        int dftB = analyser.DFT(0, true);
        int bftF = analyser.BFT(0, false);
        int bftB = analyser.BFT(0, true);

        bool ok = dftF == v && dftB == v && bftF == v && bftB == v
                  && analyser.IsConnected() && analyser.IsConnectedBFT();
        Check("#12 Baseline connectivity", ok,
              "DFT " + dftF + "/" + dftB + ", BFT " + bftF + "/" + bftB + " of " + v);
    }

    // a reported critical road really cuts places off when closed
    private void Test13CriticalJunctionsAndRoads()
    {
        RoadNetwork network = LoadKandy();
        NetworkAnalyser analyser = new NetworkAnalyser(network);

        List<CriticalItem> junctions = analyser.CriticalJunctions(Hospital);
        List<CriticalItem> roads = analyser.CriticalRoads(Hospital);

        bool ok = junctions.Count > 0 && roads.Count > 0 && IsSortedAndPositive(junctions) && IsSortedAndPositive(roads);
        string top = "none";
        if (junctions.Count > 0)
        {
            top = "top junction " + junctions[0].id + " cuts off " + junctions[0].lost;
        }
        Check("#13 Critical junctions / roads listed", ok,
              junctions.Count + " junctions, " + roads.Count + " roads, " + top);

        bool confirmed = false;
        string reason = "no junction to check";
        if (junctions.Count > 0)
        {
            int beforeF = analyser.DFT(Hospital, false);
            int beforeB = analyser.DFT(Hospital, true);
            network.GetLocation(junctions[0].id).isBlocked = true;
            int afterF = analyser.DFT(Hospital, false);
            int afterB = analyser.DFT(Hospital, true);
            network.GetLocation(junctions[0].id).isBlocked = false;

            confirmed = (beforeF - afterF > 1) || (beforeB - afterB > 1);
            reason = "DFT from hospital: forwards " + beforeF + " -> " + afterF + ", backwards " + beforeB + " -> " + afterB;
        }
        Check("#13 Top junction confirmed by DFT", confirmed, reason);
    }

    private void Test14InactiveIncident()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);
        List<Incident> list = LoadKandyIncidents();
        RouteRequest request = new RouteRequest(ClockTower, Hospital, RouteRequest.Time);
        RouteResult before = planner.FastestOrShortest(request);

        for (int i = 0; i < list.Count; i++)
        {
            list[i].active = false;
        }
        int applied = network.ApplyIncidents(list, new List<string>());
        RouteResult after = planner.FastestOrShortest(request);

        bool ok = list.Count > 0 && applied == 0 && NoFlagsSet(network)
                  && Math.Abs(before.totalCost - after.totalCost) < 0.000001 && SamePath(before.path, after.path);
        Check("#14 Inactive incidents", ok, list.Count + " incidents, " + applied + " applied, " + Min(before) + " = " + Min(after));
    }

    // a one-way road can only be used in its direction
    private void Test15OneWayRoads()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);

        int checkedCount = 0;
        int wrong = 0;
        foreach (int id in network.locations.Keys)
        {
            List<Road> roads = network.RoadsFrom(id);
            for (int i = 0; i < roads.Count; i++)
            {
                Road r = roads[i];
                if (!r.oneWay || network.FindRoad(r.to, r.from) != null)
                {
                    continue;
                }
                checkedCount++;
                RouteRequest request = new RouteRequest(r.to, r.from, RouteRequest.Distance);
                RouteResult a = planner.FastestOrShortest(request);
                RouteResult b = planner.FewestRoads(request);

                if ((a.found && (!IsValidRoute(network, a, r.to, r.from) || ContainsStep(a.path, r.to, r.from)))
                    || (b.found && (!IsValidRoute(network, b, r.to, r.from) || ContainsStep(b.path, r.to, r.from))))
                {
                    wrong++;
                }
            }
        }
        Check("#15 One-way roads", checkedCount > 0 && wrong == 0,
              checkedCount + " one-way roads tested in reverse, " + wrong + " driven the wrong way");
    }

    private void TestEmptyNetwork()
    {
        RoadNetwork network = new RoadNetwork();
        RoutePlanner planner = new RoutePlanner(network);
        NetworkAnalyser analyser = new NetworkAnalyser(network);

        RouteResult r = planner.FastestOrShortest(new RouteRequest(0, 1, RouteRequest.Time));
        RouteResult b = planner.FewestRoads(new RouteRequest(0, 1, RouteRequest.Time));
        ReachResult reach = analyser.Reachable(0, 10, RouteRequest.Time);
        bool connected = analyser.IsConnected();
        List<CriticalItem> critical = analyser.CriticalJunctions(0);

        bool ok = network.isEmpty() && network.ArraySize() == 0 && !r.found && !b.found
                  && !reach.valid && !connected && critical.Count == 0;
        Check("Empty network", ok, "'" + r.message + "'");
    }

    private void TestHeapGuards()
    {
        MinHeap heap = new MinHeap(5);
        bool emptyAtStart = heap.isEmpty() && heap.ExtractMin() == null;

        double[] costs = { 7.5, 2.0, 9.1, 0.5, 2.0 };
        bool allIn = true;
        for (int i = 0; i < costs.Length; i++)
        {
            if (!heap.Insert(i, costs[i]))
            {
                allIn = false;
            }
        }
        bool fullGuard = heap.isFull() && !heap.Insert(99, 1.0) && heap.Count() == 5;

        bool inOrder = true;
        double previous = -1;
        while (!heap.isEmpty())
        {
            HeapItem item = heap.ExtractMin();
            if (item.cost < previous)
            {
                inOrder = false;
            }
            previous = item.cost;
        }
        bool emptyAtEnd = heap.isEmpty() && heap.ExtractMin() == null;

        Check("Heap empty / full guards", emptyAtStart && allIn && fullGuard && emptyAtEnd,
              "empty ExtractMin -> null, 6th insert into size 5 refused");
        Check("Heap gives smallest first", inOrder, "5 items extracted in order");
    }

    // the heap of E + 1 slots never fills up during a search
    private void TestLazyReinsertsFit()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);
        int runs = 0;
        int full = 0;
        for (int start = 0; start < network.ArraySize(); start = start + 5)
        {
            for (int dest = 0; dest < network.ArraySize(); dest++)
            {
                RouteResult r = planner.FastestOrShortest(new RouteRequest(start, dest, RouteRequest.Balanced));
                runs++;
                if (r.message == "Heap full - search stopped.")
                {
                    full++;
                }
            }
        }
        Check("Lazy re-inserts fit in E + 1", full == 0, runs + " searches, heap size " + (network.directedRoadCount + 1) + ", " + full + " overflowed");
    }

    private void TestClosedJunction()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);
        NetworkAnalyser analyser = new NetworkAnalyser(network);
        List<Incident> list = LoadKandyIncidents();

        Incident nodeIncident = null;
        for (int i = 0; i < list.Count; i++)
        {
            list[i].active = false;
            if (nodeIncident == null && list[i].IsNodeIncident())
            {
                nodeIncident = list[i];
            }
        }
        if (nodeIncident == null)
        {
            Check("Closed junction never entered", false, "no node incident in kandy_events.csv");
            return;
        }
        nodeIncident.active = true;
        network.ApplyIncidents(list, new List<string>());
        int closed = nodeIncident.node;

        int entered = 0;
        for (int dest = 0; dest < network.ArraySize(); dest++)
        {
            if (dest == closed)
            {
                continue;
            }
            RouteResult a = planner.FastestOrShortest(new RouteRequest(ClockTower, dest, RouteRequest.Time));
            RouteResult b = planner.FewestRoads(new RouteRequest(ClockTower, dest, RouteRequest.Time));
            if (a.path.Contains(closed) || b.path.Contains(closed))
            {
                entered++;
            }
        }
        ReachResult reach = analyser.Reachable(ClockTower, 60, RouteRequest.Time);
        for (int k = 0; k < reach.reached.Count; k++)
        {
            if (reach.reached[k].locationId == closed)
            {
                entered++;
            }
        }

        bool traversalOk = analyser.DFT(ClockTower, false) < network.LocationCount();

        RouteResult fromClosed = planner.FastestOrShortest(new RouteRequest(closed, Hospital, RouteRequest.Time));

        Check("Closed junction never entered", entered == 0 && traversalOk,
              nodeIncident.eventId + " closes " + closed + ": entered " + entered + " times");
        Check("Trip from a closed junction", !fromClosed.found, "'" + fromClosed.message + "'");
    }

    // the critical tests must leave the network as they found it
    private void TestRemoveAndTestRestoresFlags()
    {
        RoadNetwork network = LoadKandy();
        NetworkAnalyser analyser = new NetworkAnalyser(network);

        List<CriticalItem> j1 = analyser.CriticalJunctions(Hospital);
        List<CriticalItem> r1 = analyser.CriticalRoads(Hospital);
        bool clean = NoFlagsSet(network);
        List<CriticalItem> j2 = analyser.CriticalJunctions(Hospital);
        List<CriticalItem> r2 = analyser.CriticalRoads(Hospital);

        bool same = SameCritical(j1, j2) && SameCritical(r1, r2);
        Check("Remove-and-test restores flags", clean && same,
              "flags clean: " + clean + ", second run identical: " + same);
    }

    private void TestClearIncidentsGivesBase()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);
        List<Incident> list = LoadKandyIncidents();
        RouteRequest request = new RouteRequest(ClockTower, Hospital, RouteRequest.Time);
        RouteResult baseRoute = planner.FastestOrShortest(request);

        for (int i = 0; i < list.Count; i++)
        {
            list[i].active = true;
        }
        int applied = network.ApplyIncidents(list, new List<string>());
        RouteResult withAll = planner.FastestOrShortest(request);

        network.ClearIncidents();
        RouteResult cleared = planner.FastestOrShortest(request);

        bool ok = applied > 0 && NoFlagsSet(network) && cleared.found
                  && Math.Abs(cleared.totalCost - baseRoute.totalCost) < 0.000001 && SamePath(cleared.path, baseRoute.path);
        string during = "no route";
        if (withAll.found)
        {
            during = Min(withAll);
        }
        Check("ClearIncidents = base result", ok,
              applied + " incidents applied (" + during + "), after clear " + Min(cleared) + " = base " + Min(baseRoute));
    }

    private void TestIncidentEffects()
    {
        RoadNetwork network = SmallNetwork();
        Road road = network.FindRoad(0, 1);

        List<Incident> list = new List<Incident>();
        list.Add(new Incident("S1", "traffic", 0, 1, -1, 2.0, true, "test"));
        list.Add(new Incident("S2", "accident", 0, 1, -1, 1.5, true, "test"));
        list.Add(new Incident("S3", "accident", 1, 2, -1, 0, true, "test"));
        list.Add(new Incident("S4", "roadblock", 5, 6, -1, 0, true, "test"));
        list.Add(new Incident("S5", "roadblock", -1, -1, 42, 0, true, "test"));
        List<string> messages = new List<string>();
        int applied = network.ApplyIncidents(list, messages);

        bool multiplied = Math.Abs(road.trafficFactor - 3.0) < 0.000001
                          && Math.Abs(network.FindRoad(1, 0).trafficFactor - 3.0) < 0.000001;
        Check("Two slowdowns multiply", multiplied, "factor " + road.trafficFactor + " (2.0 x 1.5)");
        Check("Accident without multiplier closes", network.FindRoad(1, 2).isBlocked, "road 1 -> 2 blocked");
        Check("Incident on missing road / location", applied == 3 && messages.Count == 2,
              applied + " applied, " + messages.Count + " messages");
    }

    private void TestOneWayOnlyIn()
    {
        RoadNetwork network = SmallNetwork();
        NetworkAnalyser analyser = new NetworkAnalyser(network);
        int forward = analyser.DFT(0, false);
        int backward = analyser.DFT(0, true);
        bool connected = analyser.IsConnected();
        bool connectedBft = analyser.IsConnectedBFT();

        bool ok = forward == 3 && backward == 2 && !connected && !connectedBft;
        Check("One-way only in -> not connected", ok,
              "forwards " + forward + "/3, backwards " + backward + "/3, connected: " + connected);
    }

    // broken rows are skipped and reported, the rest still loads
    private void TestBadCsvRows()
    {
        string folder = Path.Combine(Path.GetTempPath(), "kandy_tests");
        Directory.CreateDirectory(folder);
        string nodes = Path.Combine(folder, "nodes.csv");
        string edges = Path.Combine(folder, "edges.csv");
        string events = Path.Combine(folder, "events.csv");

        string[] nodeLines =
        {
            "id,name,type,lat,lon",
            "0,A,junction,7.29,80.63",
            "1,B,junction,7.30,80.64",
            "2,C,junction,7.31,80.65",
            "3,Too,few,7.3",
            "x,Bad id,junction,7.3,80.6",
            "1,Again,junction,7.3,80.6",
            "4,Far,junction,95,80.6"
        };
        string[] edgeLines =
        {
            "from,to,distance_km,base_time_min,speed_limit_kmh,road_type,one_way",
            "0,1,1.0,3.0,40,A,false",
            "1,2,1.0,3.0,40,B,true",
            "0,2,1.0,3.0,40",
            "0,2,abc,3.0,40,A,false",
            "0,2,0,3.0,40,A,false",
            "0,9,1.0,3.0,40,A,false",
            "1,0,1.0,3.0,40,A,true",
            "2,2,1.0,3.0,40,A,false"
        };
        string[] eventLines =
        {
            "event_id,type,from,to,node,time_multiplier,active,note",
            "E1,traffic,0,1,,2.0,false,good row",
            "E2,traffic,0,1,,0.5,false,multiplier below 1",
            "E3,flood,0,1,,,false,unknown type",
            "E4,roadblock,,,,,false,no target",
            "E1,roadblock,0,1,,,false,duplicate id",
            "E5,accident,,,1,,false,node only allowed for roadblock"
        };
        File.WriteAllLines(nodes, nodeLines);
        File.WriteAllLines(edges, edgeLines);
        File.WriteAllLines(events, eventLines);

        NetworkLoader loader = new NetworkLoader();
        RoadNetwork network = loader.LoadNetwork(nodes, edges);
        int networkErrors = loader.errors.Count;
        List<Incident> list = loader.LoadIncidents(events);
        int eventErrors = loader.errors.Count - networkErrors;

        Check("Bad location rows skipped", network.LocationCount() == 3,
              network.LocationCount() + " of 3 good locations loaded");
        Check("Bad road rows skipped", network.roadCount == 2 && networkErrors == 4 + 6,
              network.roadCount + " of 2 good roads loaded, " + networkErrors + " of 10 errors reported");
        Check("Bad incident rows skipped", list.Count == 1 && eventErrors == 5,
              list.Count + " of 1 good incident loaded, " + eventErrors + " of 5 errors reported");
    }

    private void TestNegativeBudget()
    {
        RoadNetwork network = LoadKandy();
        NetworkAnalyser analyser = new NetworkAnalyser(network);
        ReachResult reach = analyser.Reachable(Hospital, -5, RouteRequest.Time);
        Check("Negative budget rejected", !reach.valid && reach.Count() == 0, "'" + reach.message + "'");
    }

    private void TestAllRoadTypesAvoided()
    {
        RoadNetwork network = LoadKandy();
        RoutePlanner planner = new RoutePlanner(network);
        RouteRequest request = new RouteRequest(ClockTower, Hospital, RouteRequest.Distance);
        request.AvoidRoadType("A");
        request.AvoidRoadType("B");
        request.AvoidRoadType("minor");
        request.AvoidRoadType("bridge");
        RouteResult r = planner.FastestOrShortest(request);
        Check("All road types avoided -> no route", !r.found, "'" + r.message + "'");
    }

    private void TestGeneratedNetworkValid()
    {
        int n = 250;
        RoadNetwork network = GenerateFresh("valid", n, 2026);
        NetworkAnalyser analyser = new NetworkAnalyser(network);

        bool loaded = network.LocationCount() == n;
        double roadsPerLocation = (double)network.roadCount / n;
        bool sparse = roadsPerLocation >= 1.5 && roadsPerLocation <= 1.9;
        bool twoWay = network.directedRoadCount == 2 * network.roadCount;
        bool connected = analyser.IsConnected() && analyser.IsConnectedBFT();

        Check("Generated network valid and connected", loaded && sparse && twoWay && connected,
              network.LocationCount() + " locations, " + network.roadCount + " roads ("
              + Math.Round(roadsPerLocation, 2) + " per location), all two-way: " + twoWay
              + ", connected: " + connected);
    }

    // same seed gives the same files, a different seed does not
    private void TestGeneratorRepeatable()
    {
        int n = 128;
        string folderA = TestFolder("seed_a");
        string folderB = TestFolder("seed_b");
        string folderC = TestFolder("seed_c");
        NetworkGenerator a = new NetworkGenerator(folderA);
        NetworkGenerator b = new NetworkGenerator(folderB);
        NetworkGenerator c = new NetworkGenerator(folderC);
        a.Generate(n, 2026);
        b.Generate(n, 2026);
        c.Generate(n, 2027);

        bool same = File.ReadAllText(a.NodesPath(n)) == File.ReadAllText(b.NodesPath(n))
                    && File.ReadAllText(a.EdgesPath(n)) == File.ReadAllText(b.EdgesPath(n));
        bool different = File.ReadAllText(a.EdgesPath(n)) != File.ReadAllText(c.EdgesPath(n));
        Check("Generator repeatable with a fixed seed", same && different,
              "seed 2026 twice identical: " + same + ", seed 2027 different: " + different);
    }

    private void TestGeneratorSkipsExisting()
    {
        int n = 128;
        string folder = TestFolder("skip");
        NetworkGenerator generator = new NetworkGenerator(folder);
        generator.Generate(n, 2026);
        DateTime firstWrite = File.GetLastWriteTimeUtc(generator.EdgesPath(n));

        bool ok = generator.Generate(n, 2027);
        DateTime secondWrite = File.GetLastWriteTimeUtc(generator.EdgesPath(n));

        Check("Generator skips existing files", ok && firstWrite == secondWrite,
              "returned " + ok + ", file unchanged: " + (firstWrite == secondWrite));
    }

    private void TestGeneratorTooSmall()
    {
        string folder = TestFolder("small");
        NetworkGenerator generator = new NetworkGenerator(folder);
        bool refused = !generator.Generate(1, 2026) && !generator.Generate(0, 2026) && !generator.Generate(-5, 2026);
        bool noFiles = !File.Exists(generator.NodesPath(1)) && !File.Exists(generator.NodesPath(0));
        Check("Generator refuses fewer than 2 locations", refused && noFiles,
              "1, 0 and -5 refused: " + refused + ", no files: " + noFiles);

        RoadNetwork two = GenerateFresh("two", 2, 2026);
        NetworkAnalyser analyser = new NetworkAnalyser(two);
        bool ok = two.LocationCount() == 2 && two.roadCount == 1 && analyser.IsConnected();
        Check("Generator with 2 locations", ok, two.LocationCount() + " locations, " + two.roadCount + " road");
    }

    private void TestHeapEqualsLinearGenerated()
    {
        int n = 250;
        RoadNetwork network = GenerateFresh("dijkstra", n, 2026);
        RoutePlanner planner = new RoutePlanner(network);
        Random random = new Random(7);

        int differences = 0;
        int notFound = 0;
        for (int i = 0; i < 50; i++)
        {
            RouteRequest request = new RouteRequest(random.Next(n), random.Next(n), RouteRequest.Time);
            RouteResult heap = planner.FastestOrShortest(request);
            RouteResult linear = planner.FastestOrShortestNoHeap(request);
            if (!heap.found)
            {
                notFound++;
            }
            if (heap.found != linear.found || Math.Abs(heap.totalCost - linear.totalCost) > 0.000001)
            {
                differences++;
            }
        }
        Check("Generated network: heap = linear", differences == 0 && notFound == 0,
              "50 trips, " + differences + " differ, " + notFound + " without a route");
    }

    private void TestMedianAndMemory()
    {
        Benchmark bench = new Benchmark(dataFolder);
        double[] odd = { 5, 1, 4, 2, 3 };
        double[] even = { 4, 1, 3, 2 };
        double[] empty = { };

        bool median = bench.Median(odd) == 3 && bench.Median(even) == 2.5 && bench.Median(empty) == 0;
        bool untouched = odd[0] == 5 && odd[4] == 3;
        Check("Median", median && untouched,
              "odd " + bench.Median(odd) + ", even " + bench.Median(even) + ", empty " + bench.Median(empty)
              + ", input unchanged: " + untouched);

        bool memory = Math.Abs(bench.MatrixMb(5000) - 200) < 0.000001 && bench.ListEntries(5000, 15000) == 20000;
        Check("Matrix vs list memory", memory,
              "V = 5000: matrix " + bench.MatrixMb(5000) + " MB, list " + bench.ListEntries(5000, 15000) + " entries");
    }

    private string TestFolder(string name)
    {
        string folder = Path.Combine(Path.GetTempPath(), "kandy_gen_tests", name);
        if (Directory.Exists(folder))
        {
            Directory.Delete(folder, true);
        }
        Directory.CreateDirectory(folder);
        return folder;
    }

    private RoadNetwork GenerateFresh(string name, int n, int seed)
    {
        NetworkGenerator generator = new NetworkGenerator(TestFolder(name));
        generator.Generate(n, seed);
        NetworkLoader loader = new NetworkLoader();
        RoadNetwork network = loader.LoadNetwork(generator.NodesPath(n), generator.EdgesPath(n));
        if (loader.errors.Count > 0)
        {
            Console.WriteLine("   first load error: " + loader.errors[0]);
        }
        return network;
    }

    // prints PASS or FAIL for one check and counts it
    private void Check(string name, bool ok, string reason)
    {
        total++;
        if (ok)
        {
            passed++;
            Console.WriteLine("PASS  " + name + " - " + reason);
        }
        else
        {
            Console.WriteLine("FAIL  " + name + " - " + reason);
        }
    }

    private bool PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine(passed + " / " + total + " passed");
        return passed == total;
    }

    private RoadNetwork LoadKandy()
    {
        NetworkLoader loader = new NetworkLoader();
        return loader.LoadNetwork(Path.Combine(dataFolder, "kandy_nodes.csv"),
                                  Path.Combine(dataFolder, "kandy_edges.csv"));
    }

    private List<Incident> LoadKandyIncidents()
    {
        NetworkLoader loader = new NetworkLoader();
        return loader.LoadIncidents(Path.Combine(dataFolder, "kandy_events.csv"));
    }

    private RoadNetwork SmallNetwork()
    {
        RoadNetwork network = new RoadNetwork();
        network.AddLocation(new Location(0, "A", "junction", 7.0, 80.0));
        network.AddLocation(new Location(1, "B", "junction", 7.0, 80.1));
        network.AddLocation(new Location(2, "C", "junction", 7.1, 80.1));
        network.AddRoad(new Road(1, 0, 1, 1.0, 3.0, 40, "A", false));
        network.AddRoad(new Road(2, 1, 2, 1.0, 3.0, 40, "B", true));
        return network;
    }

    // the path starts and ends right and every step is a real road
    private bool IsValidRoute(RoadNetwork network, RouteResult result, int start, int destination)
    {
        if (result == null || !result.found || result.path.Count == 0)
        {
            return false;
        }

        if (result.path[0] != start || result.path[result.path.Count - 1] != destination)
        {
            return false;
        }

        for (int i = 0; i < result.path.Count - 1; i++)
        {
            Road road = network.FindRoad(result.path[i], result.path[i + 1]);
            if (road == null || road.isBlocked || network.GetLocation(road.to).isBlocked)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsZeroRoute(RouteResult r, int id)
    {
        return r.found && r.path.Count == 1 && r.path[0] == id && r.totalCost == 0;
    }

    private bool UsesStreet(List<int> path, int a, int b)
    {
        return ContainsStep(path, a, b) || ContainsStep(path, b, a);
    }

    private bool ContainsStep(List<int> path, int a, int b)
    {
        for (int i = 0; i < path.Count - 1; i++)
        {
            if (path[i] == a && path[i + 1] == b)
            {
                return true;
            }
        }
        return false;
    }

    private bool SamePath(List<int> a, List<int> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private int CountRoadsOfType(RoadNetwork network, List<int> path, string type)
    {
        int count = 0;
        for (int i = 0; i < path.Count - 1; i++)
        {
            Road road = network.FindRoad(path[i], path[i + 1]);
            if (road != null && road.roadType == type)
            {
                count++;
            }
        }
        return count;
    }

    private bool NoFlagsSet(RoadNetwork network)
    {
        foreach (int id in network.locations.Keys)
        {
            if (network.locations[id].isBlocked)
            {
                return false;
            }
            List<Road> roads = network.RoadsFrom(id);
            for (int i = 0; i < roads.Count; i++)
            {
                if (roads[i].isBlocked || roads[i].trafficFactor != 1.0)
                {
                    return false;
                }
            }
        }
        return true;
    }

    private bool IsSortedAndPositive(List<CriticalItem> items)
    {
        for (int i = 0; i < items.Count; i++)
        {
            if (items[i].lost <= 0)
            {
                return false;
            }
            if (i > 0 && items[i].lost > items[i - 1].lost)
            {
                return false;
            }
        }
        return true;
    }

    private bool SameCritical(List<CriticalItem> a, List<CriticalItem> b)
    {
        if (a.Count != b.Count)
        {
            return false;
        }
        for (int i = 0; i < a.Count; i++)
        {
            if (a[i].id != b[i].id || a[i].lost != b[i].lost)
            {
                return false;
            }
        }
        return true;
    }

    private string Km(RouteResult r)
    {
        return Math.Round(r.totalKm, 2) + " km";
    }

    private string Min(RouteResult r)
    {
        return Math.Round(r.totalMinutes, 2) + " min";
    }
}
