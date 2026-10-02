// The road network of Kandy stored as an adjacency list.
// Holds the locations, the roads leaving and entering each one, and applies incidents.

public class RoadNetwork
{
    public Dictionary<int, Location> locations;
    public Dictionary<int, List<Road>> adjacency;   // roads leaving each location
    public Dictionary<int, List<Road>> reverseAdjacency;   // roads arriving at each location
    public int roadCount;
    public int directedRoadCount;

    public RoadNetwork()
    {
        this.locations = new Dictionary<int, Location>();
        this.adjacency = new Dictionary<int, List<Road>>();
        this.reverseAdjacency = new Dictionary<int, List<Road>>();
        this.roadCount = 0;
        this.directedRoadCount = 0;
    }

    public bool isEmpty()
    {
        return locations.Count == 0;
    }

    public int LocationCount()
    {
        return locations.Count;
    }

    // largest id + 1, so arrays can be indexed by location id
    public int ArraySize()
    {
        int maxId = -1;
        foreach (int id in locations.Keys)
        {
            if (id > maxId)
            {
                maxId = id;
            }
        }
        return maxId + 1;
    }

    public bool HasLocation(int id)
    {
        return locations.ContainsKey(id);
    }

    public Location GetLocation(int id)
    {
        if (!locations.ContainsKey(id))
        {
            return null;
        }
        return locations[id];
    }

    // adds a location, false if the id is already used
    public bool AddLocation(Location loc)
    {
        if (locations.ContainsKey(loc.id))
        {
            return false;
        }

        locations.Add(loc.id, loc);

        adjacency.Add(loc.id, new List<Road>());
        reverseAdjacency.Add(loc.id, new List<Road>());

        return true;
    }

    // adds one directed road to both adjacency lists
    public bool AddRoad(Road road)
    {
        if (!locations.ContainsKey(road.from) || !locations.ContainsKey(road.to))
        {
            return false;
        }

        if (road.from == road.to)
        {
            return false;
        }

        if (FindRoad(road.from, road.to) != null)
        {
            return false;
        }

        if (!road.oneWay && FindRoad(road.to, road.from) != null)
        {
            return false;
        }

        adjacency[road.from].Add(road);
        reverseAdjacency[road.to].Add(road);
        directedRoadCount++;

        // a two-way street also gets a road in the other direction
        if (!road.oneWay)
        {
            Road reverse = new Road(road.roadId, road.to, road.from, road.distanceKm, road.baseTimeMin,
                                    road.speedLimit, road.roadType, false);
            adjacency[road.to].Add(reverse);
            reverseAdjacency[road.from].Add(reverse);
            directedRoadCount++;
        }

        roadCount++;

        return true;
    }

    // roads leaving a location
    public List<Road> RoadsFrom(int id)
    {
        if (!adjacency.ContainsKey(id))
        {
            return null;
        }
        return adjacency[id];
    }

    // roads arriving at a location
    public List<Road> RoadsInto(int id)
    {
        if (!reverseAdjacency.ContainsKey(id))
        {
            return null;
        }
        return reverseAdjacency[id];
    }

    // the road from one location to the next, null if there is none
    public Road FindRoad(int from, int to)
    {
        if (!adjacency.ContainsKey(from))
        {
            return null;
        }

        List<Road> roads = adjacency[from];
        for (int i = 0; i < roads.Count; i++)
        {
            if (roads[i].to == to)
            {
                return roads[i];
            }
        }
        return null;
    }

    // removes every roadblock and delay
    public void ClearIncidents()
    {
        foreach (int id in locations.Keys)
        {
            locations[id].isBlocked = false;

            List<Road> roads = adjacency[id];
            for (int i = 0; i < roads.Count; i++)
            {
                roads[i].ClearIncidents();
            }
        }
    }

    // clears the old incidents first, then applies the active ones
    public int ApplyIncidents(List<Incident> incidents, List<string> messages)
    {
        ClearIncidents();

        int applied = 0;

        for (int i = 0; i < incidents.Count; i++)
        {
            Incident inc = incidents[i];
            if (!inc.active)
            {
                continue;
            }

            if (ApplyOne(inc, messages))
            {
                applied++;
            }
        }

        return applied;
    }

    // applies one incident to its road or junction
    private bool ApplyOne(Incident inc, List<string> messages)
    {
        if (inc.IsNodeIncident())
        {
            if (!locations.ContainsKey(inc.node))
            {
                messages.Add(inc.eventId + ": location " + inc.node + " does not exist - ignored.");
                return false;
            }
            BlockLocation(inc.node);
            return true;
        }

        Road forward = FindRoad(inc.from, inc.to);
        Road backward = FindRoad(inc.to, inc.from);
        if (forward == null && backward == null)
        {
            messages.Add(inc.eventId + ": no road between " + inc.from + " and " + inc.to + " - ignored.");
            return false;
        }

        switch (inc.type)
        {
            case "roadblock":
                CloseRoad(forward, backward);
                break;

            case "accident":
                // an accident with no multiplier closes the road
                if (inc.timeMultiplier > 0)
                {
                    SlowRoad(forward, backward, inc.timeMultiplier);
                }
                else
                {
                    CloseRoad(forward, backward);
                }
                break;

            case "traffic":
                SlowRoad(forward, backward, inc.timeMultiplier);
                break;

            default:
                messages.Add(inc.eventId + ": unknown type '" + inc.type + "' - ignored.");
                return false;
        }
        return true;
    }

    // closing a location also closes every road in or out of it
    private void BlockLocation(int id)
    {
        locations[id].isBlocked = true;

        foreach (int other in adjacency.Keys)
        {
            List<Road> roads = adjacency[other];
            for (int i = 0; i < roads.Count; i++)
            {
                if (roads[i].from == id || roads[i].to == id)
                {
                    roads[i].isBlocked = true;
                }
            }
        }
    }

    private void CloseRoad(Road forward, Road backward)
    {
        if (forward != null)
        {
            forward.isBlocked = true;
        }
        if (backward != null)
        {
            backward.isBlocked = true;
        }
    }

    private void SlowRoad(Road forward, Road backward, double multiplier)
    {
        if (forward != null)
        {
            forward.trafficFactor = forward.trafficFactor * multiplier;
        }
        if (backward != null)
        {
            backward.trafficFactor = backward.trafficFactor * multiplier;
        }
    }
}
