// Finds routes between two locations.
// Dijkstra with the min-heap, Dijkstra with a linear scan, and BFS for comparison.

public class RouteResult
{
    public string algorithm;
    public string metric;
    public List<int> path;
    public double totalCost;
    public double totalKm;
    public double totalMinutes;
    public int expanded;   // locations processed by the search
    public bool found;
    public string message;

    public RouteResult(string algorithm, string metric)
    {
        this.algorithm = algorithm;
        this.metric = metric;
        this.path = new List<int>();
        this.found = false;
        this.message = "";
    }
}

public class RoutePlanner
{
    public RoadNetwork network;

    public RoutePlanner(RoadNetwork network)
    {
        this.network = network;
    }

    // false if the road is closed or the request avoids it
    public bool CanUse(Road road, RouteRequest request)
    {
        if (road.isBlocked)
        {
            return false;
        }

        Location next = network.GetLocation(road.to);
        if (next == null || next.isBlocked)
        {
            return false;
        }

        if (request.avoidedLocations.Contains(road.to))
        {
            return false;
        }
        if (request.avoidedRoadIds.Contains(road.roadId))
        {
            return false;
        }
        if (request.avoidedRoadTypes.Contains(road.roadType))
        {
            return false;
        }
        return true;
    }

    // weight of a road for the chosen metric
    public double Cost(Road road, string metric)
    {
        switch (metric)
        {
            case RouteRequest.Distance:
                return road.distanceKm;
            case RouteRequest.Time:
                return road.CurrentTime();
            case RouteRequest.Balanced:
                // distance is turned into minutes at 30 km/h (1 km = 2 min)
                return 0.5 * road.CurrentTime() + 0.5 * (road.distanceKm * 2);
            default:
                return -1;
        }
    }

    // false if the location is closed or avoided
    private bool CanEnter(int id, RouteRequest request)
    {
        Location loc = network.GetLocation(id);
        if (loc == null || loc.isBlocked)
        {
            return false;
        }
        if (request.avoidedLocations.Contains(id))
        {
            return false;
        }
        return true;
    }

    // checks done before every search
    private bool ReadyToSearch(RouteRequest request, RouteResult result)
    {
        if (network.isEmpty())
        {
            result.message = "Network is empty!!";
            return false;
        }
        if (!network.HasLocation(request.start))
        {
            result.message = "Start location " + request.start + " not found.";
            return false;
        }
        if (!network.HasLocation(request.destination))
        {
            result.message = "Destination " + request.destination + " not found.";
            return false;
        }
        if (!request.IsValidMetric())
        {
            result.message = "Unknown metric '" + request.metric + "'.";
            return false;
        }
        if (!CanEnter(request.start, request))
        {
            result.message = "No route: the start location is closed or avoided.";
            return false;
        }
        if (!CanEnter(request.destination, request))
        {
            result.message = "No route: the destination is closed or avoided.";
            return false;
        }

        if (request.start == request.destination)
        {
            result.path.Add(request.start);
            result.expanded = 1;
            result.found = true;
            result.message = "Start and destination are the same.";
            return false;
        }
        return true;
    }

    // follows prev[] back from the destination, the stack puts it in the right order
    private void BuildPath(RouteResult result, int[] prev, int destination)
    {
        Stack<int> stack = new Stack<int>();
        int current = destination;
        while (current != -1)
        {
            stack.Push(current);
            current = prev[current];
        }
        while (stack.Count > 0)
        {
            result.path.Add(stack.Pop());
        }
    }

    // adds up the km and minutes along the path
    private void AddTotals(RouteResult result)
    {
        for (int i = 0; i < result.path.Count - 1; i++)
        {
            Road road = network.FindRoad(result.path[i], result.path[i + 1]);
            result.totalKm = result.totalKm + road.distanceKm;
            result.totalMinutes = result.totalMinutes + road.CurrentTime();
        }
    }

    // builds the result once the search has ended
    private RouteResult FinishDijkstra(RouteResult result, RouteRequest request, double[] dist, int[] prev)
    {
        if (dist[request.destination] == double.PositiveInfinity)
        {
            result.message = "No route from " + request.start + " to " + request.destination
                             + " with these restrictions.";
            return result;
        }
        BuildPath(result, prev, request.destination);
        result.totalCost = dist[request.destination];
        AddTotals(result);
        result.found = true;
        return result;
    }

    // Dijkstra using the min-heap
    public RouteResult FastestOrShortest(RouteRequest request)
    {
        RouteResult result = new RouteResult("Dijkstra (MinHeap)", request.metric);

        if (!ReadyToSearch(request, result))
        {
            return result;
        }

        int n = network.ArraySize();
        double[] dist = new double[n];
        int[] prev = new int[n];
        bool[] visited = new bool[n];
        for (int v = 0; v < n; v++)
        {
            dist[v] = double.PositiveInfinity;
            prev[v] = -1;
        }
        dist[request.start] = 0;

        // a location can be inserted again with a lower cost, so the heap needs E + 1 slots
        MinHeap heap = new MinHeap(network.directedRoadCount + 1);
        heap.Insert(request.start, 0);

        while (!heap.isEmpty())
        {
            HeapItem item = heap.ExtractMin();
            int u = item.locationId;

            // old copy of a location that is already done
            if (visited[u])
            {
                continue;
            }

            visited[u] = true;
            result.expanded++;

            if (u == request.destination)
            {
                break;
            }

            List<Road> roads = network.RoadsFrom(u);
            for (int i = 0; i < roads.Count; i++)
            {
                Road r = roads[i];

                if (!CanUse(r, request))
                {
                    continue;
                }

                double newCost = dist[u] + Cost(r, request.metric);
                if (newCost < dist[r.to])
                {
                    dist[r.to] = newCost;
                    prev[r.to] = u;

                    if (!heap.Insert(r.to, newCost))
                    {
                        result.message = "Heap full - search stopped.";
                        return result;
                    }
                }
            }
        }

        return FinishDijkstra(result, request, dist, prev);
    }

    // Dijkstra without a heap: scans every location to find the closest one
    public RouteResult FastestOrShortestNoHeap(RouteRequest request)
    {
        RouteResult result = new RouteResult("Dijkstra (linear scan)", request.metric);

        if (!ReadyToSearch(request, result))
        {
            return result;
        }

        int n = network.ArraySize();
        double[] dist = new double[n];
        int[] prev = new int[n];
        bool[] visited = new bool[n];
        for (int v = 0; v < n; v++)
        {
            dist[v] = double.PositiveInfinity;
            prev[v] = -1;
        }
        dist[request.start] = 0;

        for (int step = 0; step < n; step++)
        {
            int u = -1;
            double best = double.PositiveInfinity;
            for (int v = 0; v < n; v++)
            {
                if (!visited[v] && dist[v] < best)
                {
                    best = dist[v];
                    u = v;
                }
            }

            if (u == -1)
            {
                break;
            }

            visited[u] = true;
            result.expanded++;

            if (u == request.destination)
            {
                break;
            }

            List<Road> roads = network.RoadsFrom(u);
            for (int i = 0; i < roads.Count; i++)
            {
                Road r = roads[i];

                if (!CanUse(r, request))
                {
                    continue;
                }

                double newCost = dist[u] + Cost(r, request.metric);
                if (newCost < dist[r.to])
                {
                    dist[r.to] = newCost;
                    prev[r.to] = u;
                }
            }
        }

        return FinishDijkstra(result, request, dist, prev);
    }

    // BFS: fewest roads, ignores distance and time
    public RouteResult FewestRoads(RouteRequest request)
    {
        RouteResult result = new RouteResult("BFS (fewest roads)", "roads");

        if (!ReadyToSearch(request, result))
        {
            return result;
        }

        int n = network.ArraySize();
        bool[] visited = new bool[n];
        int[] prev = new int[n];
        for (int v = 0; v < n; v++)
        {
            prev[v] = -1;
        }

        Queue<int> queue = new Queue<int>();
        visited[request.start] = true;
        queue.Enqueue(request.start);

        while (queue.Count > 0)
        {
            int u = queue.Dequeue();
            result.expanded++;

            if (u == request.destination)
            {
                break;
            }

            List<Road> roads = network.RoadsFrom(u);
            for (int i = 0; i < roads.Count; i++)
            {
                Road r = roads[i];

                if (CanUse(r, request) && !visited[r.to])
                {
                    visited[r.to] = true;
                    prev[r.to] = u;
                    queue.Enqueue(r.to);
                }
            }
        }

        if (!visited[request.destination])
        {
            result.message = "No route from " + request.start + " to " + request.destination
                             + " with these restrictions.";
            return result;
        }

        BuildPath(result, prev, request.destination);
        result.totalCost = result.path.Count - 1;
        AddTotals(result);
        result.found = true;
        return result;
    }
}
