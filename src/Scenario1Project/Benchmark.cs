// Times the algorithms on networks of increasing size
// and writes the results to results/benchmark.csv.

using System.Diagnostics;
using System.Globalization;

public class Benchmark
{
    const int NetworkSeed = 2026;
    const int PairSeed = 7;
    const int Pairs = 50;   // trips timed per network
    const int ReachStarts = 10;
    const double ReachBudget = 15;
    const int WarmUps = 3;   // runs thrown away before timing
    const int Repeats = 5;
    const int Hub = 0;

    // remove-and-test is too slow above this size
    const int CriticalLimit = 1000;

    public int[] sizes = { 128, 250, 500, 1000, 2000, 5000 };
    public string generatedFolder;
    public string resultsFolder;

    private RoadNetwork network;
    private RoutePlanner planner;
    private NetworkAnalyser analyser;
    private RouteRequest[] trips;
    private int[] reachFrom;
    private bool lastConnected;

    public Benchmark(string dataFolder)
    {
        this.generatedFolder = Path.Combine(dataFolder, "generated");
        this.resultsFolder = Path.Combine(Directory.GetParent(dataFolder).FullName, "results");
    }

    // times are only valid in a Release build
    public static bool IsReleaseBuild()
    {
#if DEBUG
        return false;
#else
        return true;
#endif
    }

    // builds the synthetic network files for every size
    public bool GenerateAll()
    {
        NetworkGenerator generator = new NetworkGenerator(generatedFolder);
        bool ok = true;
        for (int s = 0; s < sizes.Length; s++)
        {
            if (!generator.Generate(sizes[s], NetworkSeed))
            {
                ok = false;
            }
        }
        return ok;
    }

    // times every operation on every network size and saves the CSV
    public bool Run()
    {
        Console.WriteLine("=====Benchmark=====");
        if (!IsReleaseBuild())
        {
            Console.WriteLine("Warning: Debug build - times are not valid. Use: dotnet run -c Release -- bench");
        }

        List<string> lines = new List<string>();
        lines.Add("size,edges,operation,median_ms,avg_expanded,list_entries,matrix_mb");

        string[] operations = { "BFS", "Dijkstra-linear", "Dijkstra-heap", "Reachable",
                                "DFT", "BFT", "CriticalJunctions", "CriticalRoads" };
        NetworkGenerator generator = new NetworkGenerator(generatedFolder);

        for (int s = 0; s < sizes.Length; s++)
        {
            int n = sizes[s];

            if (!generator.Generate(n, NetworkSeed))
            {
                continue;
            }
            NetworkLoader loader = new NetworkLoader();
            network = loader.LoadNetwork(generator.NodesPath(n), generator.EdgesPath(n));

            if (loader.errors.Count > 0 || network.LocationCount() != n)
            {
                Console.WriteLine("net_" + n + " did not load cleanly (" + loader.errors.Count
                                  + " errors) - delete its files to rebuild it. Skipped.");
                continue;
            }
            planner = new RoutePlanner(network);
            analyser = new NetworkAnalyser(network);

            PickTrips(n);

            int v = network.LocationCount();
            int e = network.directedRoadCount;
            long listEntries = ListEntries(v, e);
            double matrixMb = MatrixMb(v);

            Console.WriteLine();
            Console.WriteLine("-----" + v + " locations, " + network.roadCount + " roads (" + e + " directed)-----");

            CheckSameCost();

            Console.WriteLine("Operation".PadRight(20) + "median ms".PadLeft(12) + "avg expanded".PadLeft(15));

            for (int op = 0; op < operations.Length; op++)
            {
                string operation = operations[op];
                bool removeAndTest = operation == "CriticalJunctions" || operation == "CriticalRoads";
                if (removeAndTest && n > CriticalLimit)
                {
                    Console.WriteLine(operation.PadRight(20) + "skipped (above " + CriticalLimit + " locations)");
                    continue;
                }

                double[] measured = Measure(operation);
                double medianMs = measured[0];
                double avgExpanded = measured[1];

                string expandedText = "";
                if (IsRouter(operation))
                {
                    expandedText = Num(avgExpanded, "0.#");
                }

                lines.Add(n + "," + e + "," + operation + "," + Num(medianMs, "0.######") + ","
                          + expandedText + "," + listEntries + "," + Num(matrixMb, "0.###"));
                Console.WriteLine(operation.PadRight(20) + Num(medianMs, "0.0000").PadLeft(12)
                                  + expandedText.PadLeft(15));

                if ((operation == "DFT" || operation == "BFT") && !lastConnected)
                {
                    Console.WriteLine("Warning: " + operation + " says net_" + n + " is not connected.");
                }
            }
            Console.WriteLine("Memory: list " + listEntries + " entries, matrix " + Num(matrixMb, "0.###") + " MB");
        }

        string path = Path.Combine(resultsFolder, "benchmark.csv");
        try
        {
            Directory.CreateDirectory(resultsFolder);
            File.WriteAllLines(path, lines);
        }
        catch (IOException ex)
        {
            Console.WriteLine("Could not write " + path + " - is it open in Excel? " + ex.Message);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            Console.WriteLine("Could not write " + path + " - no permission. " + ex.Message);
            return false;
        }
        Console.WriteLine();
        Console.WriteLine("Saved " + path);
        Console.WriteLine("Charts: python scripts/plot_results.py");
        return true;
    }

    // returns { median ms per query, average locations expanded }
    private double[] Measure(string operation)
    {
        int queries = QueriesPerRun(operation);

        for (int w = 0; w < WarmUps; w++)
        {
            RunOnce(operation);
        }

        double[] times = new double[Repeats];
        long expanded = 0;
        Stopwatch watch = new Stopwatch();
        for (int r = 0; r < Repeats; r++)
        {
            watch.Restart();
            expanded = RunOnce(operation);
            watch.Stop();
            times[r] = watch.Elapsed.TotalMilliseconds / queries;
        }

        double[] result = new double[2];
        result[0] = Median(times);
        result[1] = (double)expanded / queries;
        return result;
    }

    // runs one operation over all the test trips
    private long RunOnce(string operation)
    {
        long expanded = 0;
        switch (operation)
        {
            case "BFS":
                for (int i = 0; i < trips.Length; i++)
                {
                    expanded = expanded + planner.FewestRoads(trips[i]).expanded;
                }
                break;
            case "Dijkstra-linear":
                for (int i = 0; i < trips.Length; i++)
                {
                    expanded = expanded + planner.FastestOrShortestNoHeap(trips[i]).expanded;
                }
                break;
            case "Dijkstra-heap":
                for (int i = 0; i < trips.Length; i++)
                {
                    expanded = expanded + planner.FastestOrShortest(trips[i]).expanded;
                }
                break;
            case "Reachable":
                for (int i = 0; i < reachFrom.Length; i++)
                {
                    analyser.Reachable(reachFrom[i], ReachBudget, RouteRequest.Time);
                }
                break;
            case "DFT":
                lastConnected = analyser.IsConnected();
                break;
            case "BFT":
                lastConnected = analyser.IsConnectedBFT();
                break;
            case "CriticalJunctions":
                analyser.CriticalJunctions(Hub);
                break;
            case "CriticalRoads":
                analyser.CriticalRoads(Hub);
                break;
        }
        return expanded;
    }

    private int QueriesPerRun(string operation)
    {
        if (IsRouter(operation))
        {
            return Pairs;
        }
        if (operation == "Reachable")
        {
            return ReachStarts;
        }
        return 1;
    }

    private bool IsRouter(string operation)
    {
        return operation == "BFS" || operation == "Dijkstra-linear" || operation == "Dijkstra-heap";
    }

    // picks the same random trips every run, so the results can be repeated
    private void PickTrips(int n)
    {
        Random random = new Random(PairSeed);

        trips = new RouteRequest[Pairs];
        for (int i = 0; i < Pairs; i++)
        {
            int start = random.Next(n);
            int destination;
            do
            {
                destination = random.Next(n);
            } while (destination == start);
            trips[i] = new RouteRequest(start, destination, RouteRequest.Time);
        }

        reachFrom = new int[ReachStarts];
        for (int i = 0; i < ReachStarts; i++)
        {
            reachFrom[i] = random.Next(n);
        }
    }

    // both Dijkstras must agree before their times are compared
    private void CheckSameCost()
    {
        int differences = 0;
        for (int i = 0; i < trips.Length; i++)
        {
            RouteResult heap = planner.FastestOrShortest(trips[i]);
            RouteResult linear = planner.FastestOrShortestNoHeap(trips[i]);
            if (heap.found != linear.found || Math.Abs(heap.totalCost - linear.totalCost) > 0.000001)
            {
                differences++;
                Console.WriteLine("Warning: trip " + trips[i].start + " -> " + trips[i].destination
                                  + " heap " + heap.totalCost + " vs linear " + linear.totalCost);
            }
        }
        if (differences == 0)
        {
            Console.WriteLine("Dijkstra-heap = Dijkstra-linear on all " + trips.Length + " trips.");
        }
    }

    // middle value of the timings, so one slow run does not affect the result
    public double Median(double[] values)
    {
        if (values.Length == 0)
        {
            return 0;
        }

        double[] sorted = new double[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            sorted[i] = values[i];
        }

        // insertion sort
        for (int i = 1; i < sorted.Length; i++)
        {
            double current = sorted[i];
            int j = i - 1;
            while (j >= 0 && sorted[j] > current)
            {
                sorted[j + 1] = sorted[j];
                j--;
            }
            sorted[j + 1] = current;
        }

        int middle = sorted.Length / 2;
        if (sorted.Length % 2 == 1)
        {
            return sorted[middle];
        }
        return (sorted[middle - 1] + sorted[middle]) / 2;
    }

    // adjacency list: one entry per location and per directed road
    public long ListEntries(int v, int directedRoads)
    {
        return (long)v + directedRoads;
    }

    // adjacency matrix: V x V doubles of 8 bytes
    public double MatrixMb(int v)
    {
        return (double)v * v * 8 / 1000000;
    }

    private string Num(double value, string format)
    {
        return value.ToString(format, CultureInfo.InvariantCulture);
    }
}
