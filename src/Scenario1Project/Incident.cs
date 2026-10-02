// One row of kandy_events.csv: a roadblock, an accident or traffic.
// Incidents are applied on top of the road network when it is loaded or toggled.

public class Incident
{
    public string eventId;
    public string type;
    // from, to and node are -1 when the column is empty in the CSV
    public int from;
    public int to;
    public int node;
    public double timeMultiplier;   // 0 when empty
    public bool active;
    public string note;

    public Incident(string eventId, string type, int from, int to, int node,
                    double timeMultiplier, bool active, string note)
    {
        this.eventId = eventId;
        this.type = type;
        this.from = from;
        this.to = to;
        this.node = node;
        this.timeMultiplier = timeMultiplier;
        this.active = active;
        this.note = note;
    }

    // true if the incident is on a junction and not on a road
    public bool IsNodeIncident()
    {
        return node != -1;
    }

    public string Describe()
    {
        string rowInfo;
        if (IsNodeIncident())
        {
            rowInfo = "location " + node;
        }
        else
        {
            rowInfo = "road " + from + " - " + to;
        }

        string effect;
        if (timeMultiplier > 0)
        {
            effect = "time x" + timeMultiplier;
        }
        else
        {
            effect = "closed";
        }

        return eventId + " [" + type + "] " + rowInfo + ", " + effect + " - " + note;
    }
}
