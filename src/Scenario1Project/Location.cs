// A place in Kandy such as a junction, hospital or bridge end.
// Each location is one node of the road network.

public class Location
{
    public int id;
    public string name;
    public string type;
    public double lat;
    public double lon;
    public bool isBlocked;   // closed by a roadblock incident

    public Location(int id, string name, string type, double lat, double lon)
    {
        this.id = id;
        this.name = name;
        this.type = type;
        this.lat = lat;
        this.lon = lon;
        this.isBlocked = false;
    }

    public string Describe()
    {
        return id + " " + name + " (" + type + ")";
    }
}
