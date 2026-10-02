// Questions about the whole network: places reachable within a budget,
// connectivity (DFT and BFT) and the critical junctions and roads.

// array stack and queue, as done in the lectures
public class LocationStack
{
    public int[] items;
    public int top;
    public int maxSize;

    public LocationStack(int maxSize)
    {
        this.maxSize = maxSize;
        this.items = new int[maxSize];
        this.top = -1;
    }

    public bool isEmpty()
    {
        return top == -1;
    }

    public bool isFull()
    {
        return top == maxSize - 1;
    }

    public bool Push(int value)
    {
        if (isFull())
        {
            Console.WriteLine("Stack Overflow!! Cannot push location " + value);
            return false;
        }
        top++;
        items[top] = value;
        return true;
    }

    public int Pop()
    {
        if (isEmpty())
        {
            Console.WriteLine("Stack is empty!!");
            return -1;
        }
        int value = items[top];
        top--;
        return value;
    }
}

public class LocationQueue
{
    public int[] items;
    public int front;
    public int rear;
    public int maxSize;

    public LocationQueue(int maxSize)
    {
        this.maxSize = maxSize;
        this.items = new int[maxSize];
        this.front = -1;
        this.rear = -1;
    }

    public bool isEmpty()
    {
        return front == -1;
    }

    public bool isFull()
    {
        return rear == maxSize - 1;
    }

    public bool Enqueue(int value)
    {
        if (isFull())
        {
            Console.WriteLine("Queue is full!! Cannot enqueue location " + value);
            return false;
        }

        if (rear == -1)
        {
            front = 0;
        }
        rear++;
        items[rear] = value;
        return true;
    }

    public int Dequeue()
    {
        if (isEmpty())
        {
            Console.WriteLine("Queue is empty!!");
            return -1;
        }
        int value = items[front];

        if (front == rear)
        {
            front = -1;
            rear = -1;
        }
        else
        {
            front++;
        }
        return value;
    }
}

public class ReachResult
{
    public int start;
    public double budget;
    public string metric;
    public List<HeapItem> reached;
    public bool valid;
    public string message;

    public ReachResult(int start, double budget, string metric)
    {
        this.start = start;
        this.budget = budget;
        this.metric = metric;
        this.reached = new List<HeapItem>();
        this.valid = false;
        this.message = "";
    }

    public int Count()
    {
        return reached.Count;
    }
}

public class CriticalItem
{
    public int id;
    public int from;
    public int to;
    public int lost;

    public CriticalItem(int id, int from, int to, int lost)
    {
        this.id = id;
        this.from = from;
        this.to = to;
        this.lost = lost;
    }
}

public class NetworkAnalyser
{
    public RoadNetwork network;
    public RoutePlanner planner;

    public NetworkAnalyser(RoadNetwork network)
    {
        this.network = network;
        this.planner = new RoutePlanner(network);
    }

    // Dijkstra that stops when the cost goes over the budget
    public ReachResult Reachable(int start, double budget, string metric)
    {
        ReachResult result = new ReachResult(start, budget, metric);

        RouteRequest request = new RouteRequest(start, start, metric);

        if (network.isEmpty())
        {
            result.message = "Network is empty!!";
            return result;
        }
        if (!network.HasLocation(start))
        {
            result.message = "Start location " + start + " not found.";
            return result;
        }
        if (!request.IsValidMetric())
        {
            result.message = "Unknown metric '" + metric + "'.";
            return result;
        }
        if (budget < 0)
        {
            result.message = "Budget cannot be negative.";
            return result;
        }
        result.valid = true;

        if (network.GetLocation(start).isBlocked)
        {
            result.message = "The start location is closed.";
            return result;
        }

        int n = network.ArraySize();
        double[] dist = new double[n];
        bool[] visited = new bool[n];
        for (int v = 0; v < n; v++)
        {
            dist[v] = double.PositiveInfinity;
        }
        dist[start] = 0;

        MinHeap heap = new MinHeap(network.directedRoadCount + 1);
        heap.Insert(start, 0);

        while (!heap.isEmpty())
        {
            HeapItem item = heap.ExtractMin();
            int u = item.locationId;

            if (visited[u])
            {
                continue;
            }

            // costs come out in increasing order, so nothing after this fits the budget
            if (item.cost > budget)
            {
                break;
            }

            visited[u] = true;
            result.reached.Add(new HeapItem(u, item.cost));

            List<Road> roads = network.RoadsFrom(u);
            for (int i = 0; i < roads.Count; i++)
            {
                Road r = roads[i];

                if (!planner.CanUse(r, request))
                {
                    continue;
                }

                double newCost = item.cost + planner.Cost(r, metric);
                if (newCost < dist[r.to])
                {
                    dist[r.to] = newCost;
                    if (!heap.Insert(r.to, newCost))
                    {
                        result.message = "Heap full - search stopped.";
                        return result;
                    }
                }
            }
        }

        return result;
    }

    // reversed = true follows the roads backwards, to find who can reach start
    public int DFT(int start, bool reversed)
    {
        return CountTrue(DFTVisit(start, reversed));
    }

    // same count as DFT, using the queue
    public int BFT(int start, bool reversed)
    {
        return CountTrue(BFTVisit(start, reversed));
    }

    // depth-first traversal with the stack, returns which locations were visited
    private bool[] DFTVisit(int start, bool reversed)
    {
        int n = network.ArraySize();
        bool[] visited = new bool[n];

        if (!network.HasLocation(start))
        {
            Console.WriteLine("Location " + start + " not found.");
            return visited;
        }
        if (network.GetLocation(start).isBlocked)
        {
            return visited;
        }

        LocationStack stack = new LocationStack(n);
        stack.Push(start);
        visited[start] = true;

        while (!stack.isEmpty())
        {
            int u = stack.Pop();

            List<Road> roads;
            if (reversed)
            {
                roads = network.RoadsInto(u);
            }
            else
            {
                roads = network.RoadsFrom(u);
            }

            for (int i = 0; i < roads.Count; i++)
            {
                Road r = roads[i];

                int next;
                if (reversed)
                {
                    next = r.from;
                }
                else
                {
                    next = r.to;
                }

                if (r.isBlocked || network.GetLocation(next).isBlocked)
                {
                    continue;
                }

                if (!visited[next])
                {
                    visited[next] = true;
                    stack.Push(next);
                }
            }
        }

        return visited;
    }

    // breadth-first traversal with the queue, returns which locations were visited
    private bool[] BFTVisit(int start, bool reversed)
    {
        int n = network.ArraySize();
        bool[] visited = new bool[n];

        if (!network.HasLocation(start))
        {
            Console.WriteLine("Location " + start + " not found.");
            return visited;
        }
        if (network.GetLocation(start).isBlocked)
        {
            return visited;
        }

        LocationQueue queue = new LocationQueue(n);
        queue.Enqueue(start);
        visited[start] = true;

        while (!queue.isEmpty())
        {
            int u = queue.Dequeue();

            List<Road> roads;
            if (reversed)
            {
                roads = network.RoadsInto(u);
            }
            else
            {
                roads = network.RoadsFrom(u);
            }

            for (int i = 0; i < roads.Count; i++)
            {
                Road r = roads[i];

                int next;
                if (reversed)
                {
                    next = r.from;
                }
                else
                {
                    next = r.to;
                }

                if (r.isBlocked || network.GetLocation(next).isBlocked)
                {
                    continue;
                }

                if (!visited[next])
                {
                    visited[next] = true;
                    queue.Enqueue(next);
                }
            }
        }

        return visited;
    }

    // with one-way roads, every place must be reachable from 0 and able to get back to 0
    public bool IsConnected()
    {
        if (network.isEmpty())
        {
            Console.WriteLine("Network is empty!!");
            return false;
        }

        int forward = DFT(0, false);
        int backward = DFT(0, true);
        int v = network.LocationCount();
        return forward == v && backward == v;
    }

    // same check as IsConnected, using BFT
    public bool IsConnectedBFT()
    {
        if (network.isEmpty())
        {
            Console.WriteLine("Network is empty!!");
            return false;
        }
        int forward = BFT(0, false);
        int backward = BFT(0, true);
        int v = network.LocationCount();
        return forward == v && backward == v;
    }

    // closes one location at a time and counts the places cut off from the hospital
    public List<CriticalItem> CriticalJunctions(int hospital)
    {
        List<CriticalItem> records = new List<CriticalItem>();

        if (!HospitalOk(hospital))
        {
            return records;
        }

        bool[] before = BothWays(hospital);

        foreach (int x in network.locations.Keys)
        {
            if (x == hospital)
            {
                continue;
            }
            Location loc = network.locations[x];

            if (loc.isBlocked)
            {
                continue;
            }

            loc.isBlocked = true;
            bool[] after = BothWays(hospital);
            loc.isBlocked = false;

            int lost = CountLost(before, after, x);
            if (lost > 0)
            {
                records.Add(new CriticalItem(x, -1, -1, lost));
            }
        }

        SortByLost(records);
        return records;
    }

    // same idea for roads, both directions of a street are closed together
    public List<CriticalItem> CriticalRoads(int hospital)
    {
        List<CriticalItem> records = new List<CriticalItem>();

        if (!HospitalOk(hospital))
        {
            return records;
        }

        bool[] before = BothWays(hospital);

        HashSet<int> tested = new HashSet<int>();
        foreach (int id in network.locations.Keys)
        {
            List<Road> roads = network.RoadsFrom(id);
            for (int i = 0; i < roads.Count; i++)
            {
                Road forward = roads[i];
                if (tested.Contains(forward.roadId))
                {
                    continue;
                }
                tested.Add(forward.roadId);

                Road backward = network.FindRoad(forward.to, forward.from);
                if (backward != null && backward.roadId != forward.roadId)
                {
                    backward = null;
                }

                if (forward.isBlocked && (backward == null || backward.isBlocked))
                {
                    continue;
                }

                bool oldForward = forward.isBlocked;
                bool oldBackward = false;
                if (backward != null)
                {
                    oldBackward = backward.isBlocked;
                }

                forward.isBlocked = true;
                if (backward != null)
                {
                    backward.isBlocked = true;
                }

                bool[] after = BothWays(hospital);

                forward.isBlocked = oldForward;
                if (backward != null)
                {
                    backward.isBlocked = oldBackward;
                }

                int lost = CountLost(before, after, -1);

                if (lost > 0)
                {
                    records.Add(new CriticalItem(forward.roadId, forward.from, forward.to, lost));
                }
            }
        }

        SortByLost(records);
        return records;
    }

    // places that can reach the hospital and be reached from it
    private bool[] BothWays(int hospital)
    {
        bool[] forward = DFTVisit(hospital, false);
        bool[] backward = DFTVisit(hospital, true);

        bool[] both = new bool[forward.Length];
        for (int v = 0; v < forward.Length; v++)
        {
            both[v] = forward[v] && backward[v];
        }
        return both;
    }

    // counts the places that were reachable before the closure but not after
    private int CountLost(bool[] before, bool[] after, int skip)
    {
        int lost = 0;
        for (int v = 0; v < before.Length; v++)
        {
            if (v != skip && before[v] && !after[v])
            {
                lost++;
            }
        }
        return lost;
    }

    // the centre must exist and be open before testing
    private bool HospitalOk(int hospital)
    {
        if (!network.HasLocation(hospital))
        {
            Console.WriteLine("Location " + hospital + " not found.");
            return false;
        }
        if (network.GetLocation(hospital).isBlocked)
        {
            Console.WriteLine("Location " + hospital + " is closed, nothing to test.");
            return false;
        }
        return true;
    }

    // insertion sort, largest first
    private void SortByLost(List<CriticalItem> records)
    {
        for (int i = 1; i < records.Count; i++)
        {
            CriticalItem current = records[i];
            int j = i - 1;

            while (j >= 0 && records[j].lost < current.lost)
            {
                records[j + 1] = records[j];
                j--;
            }

            records[j + 1] = current;
        }
    }

    private int CountTrue(bool[] flags)
    {
        int count = 0;
        for (int v = 0; v < flags.Length; v++)
        {
            if (flags[v])
            {
                count++;
            }
        }
        return count;
    }
}
