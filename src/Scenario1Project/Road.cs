// One road going in one direction between two locations.
// A two-way street is stored as two Road objects with the same roadId.

public class Road
{
    public int roadId;   // same id for both directions of a two-way street
    public int from;
    public int to;
    public double distanceKm;
    public double baseTimeMin;
    public int speedLimit;
    public string roadType;
    public bool oneWay;
    public bool isBlocked;
    public double trafficFactor;   // 1.0 = normal, 2.0 = twice as slow

    public Road(int roadId, int from, int to, double distanceKm, double baseTimeMin,
                int speedLimit, string roadType, bool oneWay)
    {
        this.roadId = roadId;
        this.from = from;
        this.to = to;
        this.distanceKm = distanceKm;
        this.baseTimeMin = baseTimeMin;
        this.speedLimit = speedLimit;
        this.roadType = roadType;
        this.oneWay = oneWay;
        this.isBlocked = false;
        this.trafficFactor = 1.0;
    }

    // travel time including traffic and accidents
    public double CurrentTime()
    {
        return baseTimeMin * trafficFactor;
    }

    // puts the road back to normal
    public void ClearIncidents()
    {
        isBlocked = false;
        trafficFactor = 1.0;
    }

    public string Describe()
    {
        string text = "Road " + roadId + ": " + from + " -> " + to + ", " + distanceKm + " km, "
                      + CurrentTime() + " min, " + roadType;
        if (isBlocked)
        {
            text = text + ", BLOCKED";
        }
        return text;
    }
}
