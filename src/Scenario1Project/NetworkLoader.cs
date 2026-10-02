// Reads the nodes, edges and events CSV files and builds the road network.
// Bad rows are skipped and reported instead of crashing the program.

using System.Globalization;

public class NetworkLoader
{
    public List<string> errors;
    public List<string> warnings;

    public NetworkLoader()
    {
        this.errors = new List<string>();
        this.warnings = new List<string>();
    }

    // builds the network from the two CSV files
    public RoadNetwork LoadNetwork(string nodesPath, string edgesPath)
    {
        RoadNetwork network = new RoadNetwork();
        LoadLocations(network, nodesPath);
        LoadRoads(network, edgesPath);
        CheckIsolatedLocations(network);
        return network;
    }

    // reads the locations, bad rows are skipped and reported
    private void LoadLocations(RoadNetwork network, string path)
    {
        string[] lines = ReadFile(path);
        if (lines == null)
        {
            return;
        }

        // line 0 is the header
        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "")
            {
                continue;
            }

            string[] f = lines[i].Split(',');
            string rowInfo = "kandy_nodes line " + (i + 1);

            if (f.Length != 5)
            {
                errors.Add(rowInfo + ": expected 5 columns, found " + f.Length);
                continue;
            }

            int id;
            double lat;
            double lon;
            if (!int.TryParse(f[0].Trim(), out id))
            {
                errors.Add(rowInfo + ": bad id '" + f[0] + "'");
                continue;
            }
            if (!TryReadDouble(f[3], out lat) || !TryReadDouble(f[4], out lon))
            {
                errors.Add(rowInfo + ": bad lat/lon");
                continue;
            }
            if (lat < -90 || lat > 90 || lon < -180 || lon > 180)
            {
                errors.Add(rowInfo + ": lat/lon out of range");
                continue;
            }

            if (!network.AddLocation(new Location(id, f[1].Trim(), f[2].Trim(), lat, lon)))
            {
                errors.Add(rowInfo + ": duplicate location id " + id);
            }
        }
    }

    // reads the roads, a two-way road is added in both directions
    private void LoadRoads(RoadNetwork network, string path)
    {
        string[] lines = ReadFile(path);
        if (lines == null)
        {
            return;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "")
            {
                continue;
            }

            string[] f = lines[i].Split(',');
            string rowInfo = "kandy_edges line " + (i + 1);

            if (f.Length != 7)
            {
                errors.Add(rowInfo + ": expected 7 columns, found " + f.Length);
                continue;
            }

            int from;
            int to;
            double distance;
            double time;
            int speed;
            bool oneWay;
            if (!int.TryParse(f[0].Trim(), out from) || !int.TryParse(f[1].Trim(), out to))
            {
                errors.Add(rowInfo + ": bad from/to id");
                continue;
            }
            if (!TryReadDouble(f[2], out distance) || !TryReadDouble(f[3], out time)
                || !int.TryParse(f[4].Trim(), out speed))
            {
                errors.Add(rowInfo + ": bad distance/time/speed");
                continue;
            }
            if (!bool.TryParse(f[6].Trim(), out oneWay))
            {
                errors.Add(rowInfo + ": one_way must be true or false");
                continue;
            }
            if (distance <= 0 || time <= 0 || speed <= 0)
            {
                errors.Add(rowInfo + ": distance, time and speed must be above 0");
                continue;
            }

            // the time cannot be faster than the speed limit allows (0.05 covers rounding)
            double fastestTime = distance / speed * 60;
            if (time < fastestTime - 0.05)
            {
                errors.Add(rowInfo + ": time " + time + " min is faster than the speed limit allows ("
                           + Math.Round(fastestTime, 1) + " min at " + speed + " km/h)");
                continue;
            }

            if (!network.HasLocation(from) || !network.HasLocation(to))
            {
                errors.Add(rowInfo + ": road " + from + " -> " + to + " uses a missing location id");
                continue;
            }

            Road road = new Road(i, from, to, distance, time, speed, f[5].Trim(), oneWay);
            if (!network.AddRoad(road))
            {
                if (from == to)
                {
                    errors.Add(rowInfo + ": road starts and ends at location " + from);
                }
                else
                {
                    errors.Add(rowInfo + ": duplicate road " + from + " -> " + to);
                }
            }
        }
    }

    // warns about locations that have no roads
    private void CheckIsolatedLocations(RoadNetwork network)
    {
        foreach (int id in network.locations.Keys)
        {
            if (network.RoadsFrom(id).Count == 0 && network.RoadsInto(id).Count == 0)
            {
                warnings.Add("location " + network.locations[id].Describe() + " has no roads in or out");
            }
        }
    }

    // reads the incidents file
    public List<Incident> LoadIncidents(string path)
    {
        List<Incident> incidents = new List<Incident>();
        HashSet<string> usedIds = new HashSet<string>();

        string[] lines = ReadFile(path);
        if (lines == null)
        {
            return incidents;
        }

        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Trim() == "")
            {
                continue;
            }

            // only 8 parts, so a comma inside the note is kept
            string[] f = lines[i].Split(',', 8);
            string rowInfo = "kandy_events line " + (i + 1);

            if (f.Length != 8)
            {
                errors.Add(rowInfo + ": expected 8 columns, found " + f.Length);
                continue;
            }

            string eventId = f[0].Trim();
            if (usedIds.Contains(eventId))
            {
                errors.Add(rowInfo + ": duplicate incident id " + eventId);
                continue;
            }
            usedIds.Add(eventId);

            string type = f[1].Trim().ToLower();
            if (type != "roadblock" && type != "accident" && type != "traffic")
            {
                errors.Add(rowInfo + ": unknown type '" + f[1] + "'");
                continue;
            }

            int from;
            int to;
            int node;
            double multiplier;
            bool active;
            if (!TryReadOptionalInt(f[2], out from) || !TryReadOptionalInt(f[3], out to)
                || !TryReadOptionalInt(f[4], out node))
            {
                errors.Add(rowInfo + ": bad from/to/node id");
                continue;
            }
            if (!TryReadOptionalDouble(f[5], out multiplier))
            {
                errors.Add(rowInfo + ": bad time_multiplier");
                continue;
            }
            if (!bool.TryParse(f[6].Trim(), out active))
            {
                errors.Add(rowInfo + ": active must be true or false");
                continue;
            }

            bool hasRoad = from != -1 && to != -1;
            if (node == -1 && !hasRoad)
            {
                errors.Add(rowInfo + ": " + eventId + " needs a node or a from/to pair");
                continue;
            }

            if (node != -1 && type != "roadblock")
            {
                errors.Add(rowInfo + ": " + eventId + " " + type + " must be on a road (from/to), only a roadblock can use node");
                continue;
            }
            if (type == "traffic" && multiplier == 0)
            {
                errors.Add(rowInfo + ": " + eventId + " traffic needs a time_multiplier");
                continue;
            }

            if (multiplier != 0 && multiplier < 1)
            {
                errors.Add(rowInfo + ": " + eventId + " time_multiplier must be 1 or more");
                continue;
            }

            incidents.Add(new Incident(eventId, type, from, to, node, multiplier, active, f[7].Trim()));
        }
        return incidents;
    }

    // looks for the data folder here and in the parent folders
    public static string FindDataFolder()
    {
        string folder = Directory.GetCurrentDirectory();
        for (int level = 0; level < 5; level++)
        {
            string candidate = Path.Combine(folder, "data");
            if (File.Exists(Path.Combine(candidate, "kandy_nodes.csv")))
            {
                return candidate;
            }

            DirectoryInfo parent = Directory.GetParent(folder);
            if (parent == null)
            {
                return null;
            }
            folder = parent.FullName;
        }
        return null;
    }

    // returns the lines of a file, or null if it cannot be read
    private string[] ReadFile(string path)
    {
        if (!File.Exists(path))
        {
            errors.Add("File not found: " + path);
            return null;
        }
        return File.ReadAllLines(path);
    }

    private bool TryReadDouble(string text, out double value)
    {
        return double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    // an empty column is allowed and gives -1
    private bool TryReadOptionalInt(string text, out int value)
    {
        if (text.Trim() == "")
        {
            value = -1;
            return true;
        }
        return int.TryParse(text.Trim(), out value);
    }

    private bool TryReadOptionalDouble(string text, out double value)
    {
        if (text.Trim() == "")
        {
            value = 0;
            return true;
        }
        return TryReadDouble(text, out value);
    }
}
