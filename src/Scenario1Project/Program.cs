// Entry point of the Kandy Routing Engine.
// Loads the data and shows the menu.

using System.Diagnostics;
using System.Globalization;

public class Program
{
    const int Hospital = 12;   // Kandy National Hospital

    static RoadNetwork network;
    static List<Incident> incidents;
    static RoutePlanner planner;
    static NetworkAnalyser analyser;
    static RouteExporter exporter;
    static string dataFolder;

    // kept for menu option 7
    static RouteResult lastRoute;
    static ReachResult lastReach;
    static string lastExportedRoute;

    public static void Main(string[] args)
    {
        dataFolder = NetworkLoader.FindDataFolder();
        if (dataFolder == null)
        {
            Console.WriteLine("Could not find the data folder. Run from the project folder.");
            Environment.ExitCode = 1;
            return;
        }

        // dotnet run -- test / bench / generate skip the menu
        if (args.Length > 0 && args[0] == "test")
        {
            TestRunner tests = new TestRunner(dataFolder);
            if (!tests.RunAll())
            {
                Environment.ExitCode = 1;
            }
            return;
        }

        if (args.Length > 0 && args[0] == "bench")
        {
            Benchmark bench = new Benchmark(dataFolder);
            if (!bench.Run())
            {
                Environment.ExitCode = 1;
            }
            return;
        }

        if (args.Length > 0 && args[0] == "generate")
        {
            Benchmark bench = new Benchmark(dataFolder);
            if (!bench.GenerateAll())
            {
                Environment.ExitCode = 1;
            }
            return;
        }

        if (!LoadEverything())
        {
            return;
        }

        int choice;
        do
        {
            PrintMenu();
            choice = ReadMenuChoice();
            switch (choice)
            {
                case 1: FindRoute(false); break;
                case 2: FindRoute(true); break;
                case 3: CompareAlgorithms(); break;
                case 4: ReachableWithin(); break;
                case 5: NetworkHealth(); break;
                case 6: ManageIncidents(); break;
                case 7: ExportLast(); break;
                case 8: RunTests(); break;
                case 9: RunBenchmark(); break;
                case 0: Console.WriteLine("Exit..."); break;
                default: Console.WriteLine("Please choose a number from 0 to 9."); break;
            }
        } while (choice != 0);
    }

    // loads the Kandy network and incidents, false if the data is missing
    static bool LoadEverything()
    {
        Console.WriteLine("=====Loading Kandy network=====");
        NetworkLoader loader = new NetworkLoader();
        network = loader.LoadNetwork(Path.Combine(dataFolder, "kandy_nodes.csv"),
                                     Path.Combine(dataFolder, "kandy_edges.csv"));
        incidents = loader.LoadIncidents(Path.Combine(dataFolder, "kandy_events.csv"));

        for (int i = 0; i < loader.errors.Count; i++)
        {
            Console.WriteLine("   Error: " + loader.errors[i]);
        }
        for (int i = 0; i < loader.warnings.Count; i++)
        {
            Console.WriteLine("   Warning: " + loader.warnings[i]);
        }

        if (network.isEmpty())
        {
            Console.WriteLine("Network is empty!! Check the files in " + dataFolder);
            return false;
        }

        planner = new RoutePlanner(network);
        analyser = new NetworkAnalyser(network);
        exporter = new RouteExporter(Path.Combine(Directory.GetParent(dataFolder).FullName, "results"));

        Console.WriteLine("Locations: " + network.LocationCount() + ", roads: " + network.roadCount
                          + " (" + network.directedRoadCount + " directed), incidents in file: " + incidents.Count);

        ApplyAndReport();
        return true;
    }

    // resets the network and applies the incidents that are switched on
    static void ApplyAndReport()
    {
        List<string> messages = new List<string>();
        int applied = network.ApplyIncidents(incidents, messages);
        for (int i = 0; i < messages.Count; i++)
        {
            Console.WriteLine("   " + messages[i]);
        }
        Console.WriteLine("Active incidents applied: " + applied);
    }

    static void PrintMenu()
    {
        Console.WriteLine();
        Console.WriteLine("=====Kandy Routing Engine=====");
        Console.WriteLine("1  Find a route");
        Console.WriteLine("2  Find a route with restrictions");
        Console.WriteLine("3  Compare algorithms on one trip");
        Console.WriteLine("4  Reachable within N minutes");
        Console.WriteLine("5  Network health");
        Console.WriteLine("6  Incidents");
        Console.WriteLine("7  Export last route / reach for the map");
        Console.WriteLine("8  Run tests");
        Console.WriteLine("9  Run benchmark");
        Console.WriteLine("0  Exit");
        Console.Write("Choice: ");
    }

    // menu 1 and 2: best route, with or without restrictions
    static void FindRoute(bool withRestrictions)
    {
        if (withRestrictions)
        {
            Console.WriteLine("=====Route with restrictions=====");
        }
        else
        {
            Console.WriteLine("=====Find a route=====");
        }

        RouteRequest request = ReadTrip();
        if (request == null)
        {
            return;
        }

        if (withRestrictions)
        {
            ReadRestrictions(request);
        }

        RouteResult result = planner.FastestOrShortest(request);
        if (!result.found)
        {
            Console.WriteLine(result.message);
            return;
        }
        PrintRoute(result);

        lastRoute = result;
    }

    // asks for start, destination and metric, null if cancelled
    static RouteRequest ReadTrip()
    {
        int start = ReadLocation("Start (id or part of the name): ");
        if (start == -1)
        {
            return null;
        }
        int destination = ReadLocation("Destination (id or part of the name): ");
        if (destination == -1)
        {
            return null;
        }
        string metric = ReadMetric();
        if (metric == null)
        {
            return null;
        }
        return new RouteRequest(start, destination, metric);
    }

    static string ReadMetric()
    {
        while (true)
        {
            Console.Write("Metric 1 distance / 2 time / 3 balanced (Enter = cancel): ");
            string text = Console.ReadLine();
            if (text == null || text.Trim() == "")
            {
                Console.WriteLine("Cancelled.");
                return null;
            }
            switch (text.Trim())
            {
                case "1": return RouteRequest.Distance;
                case "2": return RouteRequest.Time;
                case "3": return RouteRequest.Balanced;
                default: Console.WriteLine("Please type 1, 2 or 3."); break;
            }
        }
    }

    // asks which locations, roads and road types to avoid
    static void ReadRestrictions(RouteRequest request)
    {
        Console.WriteLine("Locations to avoid, one per line (Enter on an empty line = done):");
        int loc = ReadLocation("   Avoid location: ");
        while (loc != -1)
        {
            request.AvoidLocation(loc);
            Console.WriteLine("   Avoiding " + network.GetLocation(loc).Describe());
            loc = ReadLocation("   Avoid location: ");
        }

        Console.Write("Road ids to avoid, separated by commas (Enter = none): ");
        string[] roadParts = SplitList(Console.ReadLine());
        for (int i = 0; i < roadParts.Length; i++)
        {
            int roadId;
            if (!int.TryParse(roadParts[i], out roadId))
            {
                Console.WriteLine("   '" + roadParts[i] + "' is not a number - skipped.");
            }
            else if (!RoadIdExists(roadId))
            {
                Console.WriteLine("   Road " + roadId + " not found - skipped.");
            }
            else
            {
                request.AvoidRoad(roadId);
                Console.WriteLine("   Avoiding road " + roadId);
            }
        }

        Console.Write("Road types to avoid (A, B, minor, bridge), separated by commas (Enter = none): ");
        string[] typeParts = SplitList(Console.ReadLine());
        for (int i = 0; i < typeParts.Length; i++)
        {
            string type = RoadTypeName(typeParts[i]);
            if (type == null)
            {
                Console.WriteLine("   '" + typeParts[i] + "' is not a road type - skipped.");
            }
            else
            {
                request.AvoidRoadType(type);
                Console.WriteLine("   Avoiding " + type + " roads");
            }
        }
    }

    // menu 3: runs BFS and both Dijkstras on the same trip
    static void CompareAlgorithms()
    {
        Console.WriteLine("=====Compare algorithms=====");
        RouteRequest request = ReadTrip();
        if (request == null)
        {
            return;
        }

        // run each once first so the first timed one is not slower
        planner.FewestRoads(request);
        planner.FastestOrShortestNoHeap(request);
        planner.FastestOrShortest(request);

        RouteResult[] results = new RouteResult[3];
        double[] times = new double[3];
        Stopwatch watch = new Stopwatch();

        for (int a = 0; a < 3; a++)
        {
            watch.Restart();
            switch (a)
            {
                case 0: results[a] = planner.FewestRoads(request); break;
                case 1: results[a] = planner.FastestOrShortestNoHeap(request); break;
                case 2: results[a] = planner.FastestOrShortest(request); break;
            }
            watch.Stop();
            times[a] = watch.Elapsed.TotalMilliseconds;
        }

        Console.WriteLine();
        Console.WriteLine("Metric for the Dijkstras: " + request.metric + ". One run each, so the times are rough;");
        Console.WriteLine("the benchmark (option 9) repeats runs on bigger networks.");
        for (int a = 0; a < 3; a++)
        {
            RouteResult r = results[a];
            Console.WriteLine();
            Console.WriteLine("--- " + r.algorithm + " ---");
            if (!r.found)
            {
                Console.WriteLine(r.message);
                continue;
            }
            Console.WriteLine("Path: " + PathAsIds(r.path));
            Console.WriteLine("Roads: " + (r.path.Count - 1) + ", " + Math.Round(r.totalKm, 2) + " km, "
                              + Math.Round(r.totalMinutes, 2) + " min");
            Console.WriteLine("Locations expanded: " + r.expanded + ", time: " + Math.Round(times[a], 4) + " ms");
        }

        if (results[1].found && results[2].found)
        {
            bool same = Math.Abs(results[1].totalCost - results[2].totalCost) < 0.000001;
            Console.WriteLine();
            Console.WriteLine("Both Dijkstras give the same cost: " + YesNo(same));
        }

        if (results[2].found)
        {
            lastRoute = results[2];
        }
    }

    // menu 4: places reachable within a number of minutes
    static void ReachableWithin()
    {
        Console.WriteLine("=====Reachable within N minutes=====");
        int start = ReadLocation("Start (id or part of the name): ");
        if (start == -1)
        {
            return;
        }
        double budget = ReadBudget();
        if (budget < 0)
        {
            return;
        }

        ReachResult reach = analyser.Reachable(start, budget, RouteRequest.Time);
        if (!reach.valid)
        {
            Console.WriteLine(reach.message);
            return;
        }
        if (reach.message != "")
        {
            Console.WriteLine(reach.message);
        }

        Console.WriteLine("Places reached from " + network.GetLocation(start).name + " within " + budget
                          + " min: " + reach.Count() + " of " + network.LocationCount());
        for (int i = 0; i < reach.reached.Count; i++)
        {
            HeapItem item = reach.reached[i];
            Console.WriteLine("   " + Math.Round(item.cost, 1) + " min  " + network.GetLocation(item.locationId).Describe());
        }

        lastReach = reach;
    }

    static double ReadBudget()
    {
        while (true)
        {
            Console.Write("Minutes (e.g. 10, Enter = cancel): ");
            string text = Console.ReadLine();
            if (text == null || text.Trim() == "")
            {
                Console.WriteLine("Cancelled.");
                return -1;
            }
            double value;
            if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
            {
                Console.WriteLine("'" + text.Trim() + "' is not a number.");
            }
            else if (value < 0)
            {
                Console.WriteLine("The budget cannot be negative.");
            }
            else
            {
                return value;
            }
        }
    }

    // menu 5: connectivity check and critical junctions and roads
    static void NetworkHealth()
    {
        Console.WriteLine("=====Network health=====");
        int v = network.LocationCount();

        Console.WriteLine("DFT from location 0: forwards " + analyser.DFT(0, false) + " / " + v
                          + ", backwards " + analyser.DFT(0, true) + " / " + v);
        Console.WriteLine("BFT from location 0: forwards " + analyser.BFT(0, false) + " / " + v
                          + ", backwards " + analyser.BFT(0, true) + " / " + v);
        Console.WriteLine("Every place can reach every other place (DFT): " + YesNo(analyser.IsConnected()));
        Console.WriteLine("Every place can reach every other place (BFT): " + YesNo(analyser.IsConnectedBFT()));

        Console.WriteLine();
        Console.Write("Test critical junctions and roads from (Enter = " + Hospital + " "
                      + network.GetLocation(Hospital).name + "): ");
        string text = Console.ReadLine();
        int centre = Hospital;
        if (text != null && text.Trim() != "")
        {
            centre = FindLocation(text.Trim());
            if (centre == -1)
            {
                return;
            }
        }
        Location centreLoc = network.GetLocation(centre);

        Console.WriteLine();
        Console.WriteLine("=====Critical junctions (from " + centreLoc.name + ")=====");
        List<CriticalItem> junctions = analyser.CriticalJunctions(centre);
        if (junctions.Count == 0)
        {
            Console.WriteLine("None: closing any single location cuts nobody off.");
        }
        for (int i = 0; i < junctions.Count; i++)
        {
            Console.WriteLine("   " + network.GetLocation(junctions[i].id).Describe()
                              + " - cuts off " + junctions[i].lost);
        }

        Console.WriteLine();
        Console.WriteLine("=====Critical roads (from " + centreLoc.name + ")=====");
        List<CriticalItem> roads = analyser.CriticalRoads(centre);
        if (roads.Count == 0)
        {
            Console.WriteLine("None: closing any single road cuts nobody off.");
        }
        for (int i = 0; i < roads.Count; i++)
        {
            CriticalItem c = roads[i];
            Console.WriteLine("   Road " + c.id + ": " + network.GetLocation(c.from).name + " - "
                              + network.GetLocation(c.to).name + " - cuts off " + c.lost);
        }
        Console.WriteLine("(\"cuts off\" = places that can no longer both reach " + centreLoc.name
                          + " and be reached from it)");
    }

    // menu 6: switch incidents on and off
    static void ManageIncidents()
    {
        Console.WriteLine("=====Incidents=====");
        if (incidents.Count == 0)
        {
            Console.WriteLine("No incidents were loaded from kandy_events.csv.");
            return;
        }

        while (true)
        {
            Console.WriteLine();
            for (int i = 0; i < incidents.Count; i++)
            {
                string state;
                if (incidents[i].active)
                {
                    state = "ON ";
                }
                else
                {
                    state = "off";
                }
                Console.WriteLine((i + 1) + ". [" + state + "] " + incidents[i].Describe());
            }
            Console.Write("Number to switch on/off (Enter = back): ");
            string text = Console.ReadLine();
            if (text == null || text.Trim() == "")
            {
                return;
            }

            int number;
            if (!int.TryParse(text.Trim(), out number) || number < 1 || number > incidents.Count)
            {
                Console.WriteLine("Please type a number from 1 to " + incidents.Count + ".");
                continue;
            }

            Incident inc = incidents[number - 1];
            inc.active = !inc.active;
            Console.WriteLine(inc.eventId + " is now " + OnOff(inc.active) + ".");
            ApplyAndReport();

            if (lastRoute != null || lastReach != null)
            {
                Console.WriteLine("The network changed, so the last route / reach result was cleared. Run it again to export it.");
                lastRoute = null;
                lastReach = null;
            }
        }
    }

    // menu 7: saves the last route / reach as CSV for the map script
    static void ExportLast()
    {
        Console.WriteLine("=====Export for the map=====");
        if (lastRoute == null && lastReach == null)
        {
            Console.WriteLine("Nothing to export yet. Find a route (1-3) or a reach (4) first.");
            return;
        }

        string label = ReadLabel();
        string networkFile = exporter.ExportNetwork(network, "network_" + label + ".csv");
        if (networkFile == null)
        {
            return;
        }
        Console.WriteLine("Saved: " + networkFile);

        Console.WriteLine();
        Console.WriteLine("Draw the map (run from the project folder):");

        if (lastRoute != null)
        {
            string routeFile = exporter.ExportRoute(network, lastRoute, label, "route_" + label + ".csv");
            if (routeFile != null)
            {
                Console.WriteLine("Saved: " + routeFile);
                Console.WriteLine("python scripts/plot_route.py results/route_" + label + ".csv"
                                  + " --network results/network_" + label + ".csv");

                if (lastExportedRoute != null && lastExportedRoute != "route_" + label + ".csv")
                {
                    Console.WriteLine("Before / after: python scripts/plot_route.py results/" + lastExportedRoute
                                      + " results/route_" + label + ".csv --network results/network_" + label + ".csv");
                }
                lastExportedRoute = "route_" + label + ".csv";
            }
        }

        if (lastReach != null)
        {
            string reachFile = exporter.ExportReach(network, lastReach, "reach_" + label + ".csv");
            if (reachFile != null)
            {
                Console.WriteLine("Saved: " + reachFile);
                Console.WriteLine("python scripts/plot_route.py --reach results/reach_" + label + ".csv"
                                  + " --network results/network_" + label + ".csv");
            }
        }
    }

    static string ReadLabel()
    {
        while (true)
        {
            Console.Write("Name for the files, e.g. before / after (Enter = last): ");
            string text = Console.ReadLine();
            if (text == null || text.Trim() == "")
            {
                return "last";
            }
            text = text.Trim();

            bool ok = true;
            for (int i = 0; i < text.Length; i++)
            {
                if (!char.IsLetterOrDigit(text[i]) && text[i] != '_')
                {
                    ok = false;
                }
            }
            if (ok)
            {
                return text;
            }
            Console.WriteLine("Use only letters, digits and _.");
        }
    }

    static void RunTests()
    {
        TestRunner tests = new TestRunner(dataFolder);
        tests.RunAll();
    }

    static void RunBenchmark()
    {
        if (!Benchmark.IsReleaseBuild())
        {
            Console.WriteLine("Warning: this is a Debug build, so the times will not be valid.");
            Console.WriteLine("For real results run: dotnet run --project src/Scenario1Project -c Release -- bench");
            Console.Write("Run anyway? (y/n): ");
            string answer = Console.ReadLine();
            if (answer == null || answer.Trim().ToLower() != "y")
            {
                Console.WriteLine("Benchmark cancelled.");
                return;
            }
        }
        Benchmark bench = new Benchmark(dataFolder);
        bench.Run();
    }

    static int ReadMenuChoice()
    {
        string text = Console.ReadLine();
        if (text == null)
        {
            return 0;
        }
        int choice;
        if (!int.TryParse(text.Trim(), out choice))
        {
            return -1;
        }
        return choice;
    }

    // keeps asking until a location is found, -1 on an empty line
    static int ReadLocation(string prompt)
    {
        while (true)
        {
            Console.Write(prompt);
            string text = Console.ReadLine();
            if (text == null || text.Trim() == "")
            {
                return -1;
            }
            int id = FindLocation(text.Trim());
            if (id != -1)
            {
                return id;
            }
        }
    }

    // accepts an id or part of a name, returns -1 if not found
    static int FindLocation(string text)
    {
        int id;
        if (int.TryParse(text, out id))
        {
            if (network.HasLocation(id))
            {
                return id;
            }
            Console.WriteLine("Location " + id + " not found. Ids go from 0 to " + (network.ArraySize() - 1) + ".");
            return -1;
        }

        List<int> matches = new List<int>();
        string wanted = text.ToLower();
        foreach (int key in network.locations.Keys)
        {
            if (network.locations[key].name.ToLower().Contains(wanted))
            {
                matches.Add(key);
            }
        }

        if (matches.Count == 0)
        {
            Console.WriteLine("No location name contains '" + text + "'.");
            return -1;
        }

        if (matches.Count == 1)
        {
            Console.WriteLine("   -> " + network.GetLocation(matches[0]).Describe());
            return matches[0];
        }

        Console.WriteLine(matches.Count + " locations match '" + text + "'. Type the id of the one you want:");
        for (int i = 0; i < matches.Count; i++)
        {
            Console.WriteLine("   " + network.GetLocation(matches[i]).Describe());
        }
        return -1;
    }

    static string[] SplitList(string text)
    {
        if (text == null || text.Trim() == "")
        {
            return new string[0];
        }
        string[] parts = text.Split(',');
        List<string> items = new List<string>();
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Trim() != "")
            {
                items.Add(parts[i].Trim());
            }
        }
        return items.ToArray();
    }

    static string RoadTypeName(string text)
    {
        switch (text.ToLower())
        {
            case "a": return "A";
            case "b": return "B";
            case "minor": return "minor";
            case "bridge": return "bridge";
            default: return null;
        }
    }

    static bool RoadIdExists(int roadId)
    {
        foreach (int id in network.locations.Keys)
        {
            List<Road> roads = network.RoadsFrom(id);
            for (int i = 0; i < roads.Count; i++)
            {
                if (roads[i].roadId == roadId)
                {
                    return true;
                }
            }
        }
        return false;
    }

    static void PrintRoute(RouteResult result)
    {
        Console.WriteLine();
        Console.WriteLine(result.algorithm + ", metric: " + result.metric);
        Console.WriteLine("   1. " + network.GetLocation(result.path[0]).Describe());
        for (int i = 1; i < result.path.Count; i++)
        {
            Road road = network.FindRoad(result.path[i - 1], result.path[i]);
            Console.WriteLine("   " + (i + 1) + ". " + network.GetLocation(result.path[i]).Describe()
                              + "   via road " + road.roadId + " (" + road.roadType + ", "
                              + road.distanceKm + " km, " + Math.Round(road.CurrentTime(), 2) + " min)");
        }
        Console.WriteLine("Total: " + Math.Round(result.totalKm, 2) + " km, " + Math.Round(result.totalMinutes, 2)
                          + " min, " + (result.path.Count - 1) + " roads, " + result.expanded + " locations expanded");
    }

    static string PathAsIds(List<int> path)
    {
        string text = "";
        for (int i = 0; i < path.Count; i++)
        {
            if (i > 0)
            {
                text = text + " -> ";
            }
            text = text + path[i];
        }
        return text;
    }

    static string YesNo(bool value)
    {
        if (value)
        {
            return "YES";
        }
        return "NO";
    }

    static string OnOff(bool value)
    {
        if (value)
        {
            return "ON";
        }
        return "off";
    }
}
