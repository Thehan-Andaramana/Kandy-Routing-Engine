// What one trip asks for: start, destination, the metric to use
// and the locations, roads or road types it must avoid.

public class RouteRequest
{
    public const string Distance = "distance";
    public const string Time = "time";
    public const string Balanced = "balanced";   // half time, half distance

    public int start;
    public int destination;
    public string metric;
    public HashSet<int> avoidedLocations;
    public HashSet<int> avoidedRoadIds;
    public HashSet<string> avoidedRoadTypes;

    public RouteRequest(int start, int destination, string metric)
    {
        this.start = start;
        this.destination = destination;
        this.metric = metric;
        this.avoidedLocations = new HashSet<int>();
        this.avoidedRoadIds = new HashSet<int>();
        this.avoidedRoadTypes = new HashSet<string>();
    }

    public void AvoidLocation(int id)
    {
        avoidedLocations.Add(id);
    }

    public void AvoidRoad(int roadId)
    {
        avoidedRoadIds.Add(roadId);
    }

    public void AvoidRoadType(string roadType)
    {
        avoidedRoadTypes.Add(roadType);
    }

    // metric must be distance, time or balanced
    public bool IsValidMetric()
    {
        switch (metric)
        {
            case Distance:
            case Time:
            case Balanced:
                return true;
            default:
                return false;
        }
    }
}
