// Writes the network, routes and reachable places to CSV files in results/
// so the Python script can draw them on a map.

using System.Globalization;

public class RouteExporter
{
    public string resultsFolder;

    public RouteExporter(string resultsFolder)
    {
        this.resultsFolder = resultsFolder;
    }

    // every road with its coordinates, for the grey background of the map
    public string ExportNetwork(RoadNetwork network, string file)
    {
        if (network == null || network.isEmpty())
        {
            Console.WriteLine("Network is empty!! Nothing to export.");
            return null;
        }

        List<string> lines = new List<string>();
        lines.Add("road_id,from,to,from_lat,from_lon,to_lat,to_lon,road_type,one_way,blocked");

        // a two-way street is stored twice, write it once
        HashSet<int> written = new HashSet<int>();

        foreach (int id in network.locations.Keys)
        {
            List<Road> roads = network.RoadsFrom(id);

            for (int i = 0; i < roads.Count; i++)
            {
                Road r = roads[i];

                if (written.Contains(r.roadId))
                {
                    continue;
                }

                written.Add(r.roadId);

                bool blocked = r.isBlocked;
                Road other = network.FindRoad(r.to, r.from);
                if (other != null && other.roadId == r.roadId && other.isBlocked)
                {
                    blocked = true;
                }

                Location a = network.GetLocation(r.from);
                Location b = network.GetLocation(r.to);
                lines.Add(r.roadId + "," + r.from + "," + r.to + ","
                          + Num(a.lat) + "," + Num(a.lon) + ","
                          + Num(b.lat) + "," + Num(b.lon) + ","
                          + r.roadType + "," + TrueFalse(r.oneWay) + "," + TrueFalse(blocked));
            }
        }

        return WriteFile(file, lines);
    }

    // the locations of a route in order, with running km and minutes
    public string ExportRoute(RoadNetwork network, RouteResult result, string label, string file)
    {
        if (result == null || !result.found)
        {
            Console.WriteLine("No route to export.");
            return null;
        }

        List<string> lines = new List<string>();
        // first line holds the summary, the Python script reads it for the title
        lines.Add("# " + Clean(label) + "," + Clean(result.algorithm) + "," + result.metric + ","
                  + Num(result.totalKm) + "," + Num(result.totalMinutes));
        lines.Add("step,id,name,lat,lon,cumulative_km,cumulative_min");

        double cumulativeKm = 0;
        double cumulativeMin = 0;

        for (int step = 0; step < result.path.Count; step++)
        {
            if (step > 0)
            {
                Road road = network.FindRoad(result.path[step - 1], result.path[step]);
                if (road == null)
                {
                    Console.WriteLine("Road " + result.path[step - 1] + " -> " + result.path[step]
                                      + " not found. Route not exported.");
                    return null;
                }
                cumulativeKm = cumulativeKm + road.distanceKm;
                cumulativeMin = cumulativeMin + road.CurrentTime();
            }

            Location loc = network.GetLocation(result.path[step]);
            lines.Add(step + "," + loc.id + "," + Clean(loc.name) + "," + Num(loc.lat) + "," + Num(loc.lon)
                      + "," + Num(cumulativeKm) + "," + Num(cumulativeMin));
        }

        return WriteFile(file, lines);
    }

    // the reachable places with their cost
    public string ExportReach(RoadNetwork network, ReachResult reach, string file)
    {
        if (reach == null || !reach.valid)
        {
            Console.WriteLine("No reachability result to export.");
            return null;
        }

        List<string> lines = new List<string>();
        lines.Add("# " + reach.start + "," + reach.metric + "," + Num(reach.budget));
        lines.Add("id,name,lat,lon,cost");

        for (int i = 0; i < reach.reached.Count; i++)
        {
            HeapItem item = reach.reached[i];
            Location loc = network.GetLocation(item.locationId);
            lines.Add(loc.id + "," + Clean(loc.name) + "," + Num(loc.lat) + "," + Num(loc.lon)
                      + "," + Num(item.cost));
        }

        return WriteFile(file, lines);
    }

    private string WriteFile(string file, List<string> lines)
    {
        string path = Path.Combine(resultsFolder, file);
        try
        {
            Directory.CreateDirectory(resultsFolder);
            File.WriteAllLines(path, lines);
        }
        catch (IOException e)
        {
            Console.WriteLine("Could not write " + path + " - is it open in another program (e.g. Excel)? " + e.Message);
            return null;
        }
        catch (UnauthorizedAccessException e)
        {
            Console.WriteLine("Could not write " + path + " - no permission (read-only file or protected folder). "
                              + e.Message);
            return null;
        }
        return path;
    }

    // always a dot as the decimal point, whatever the PC language is
    private string Num(double value)
    {
        return value.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    private string TrueFalse(bool value)
    {
        if (value)
        {
            return "true";
        }
        return "false";
    }

    // commas would break the CSV columns
    private string Clean(string text)
    {
        if (text == null)
        {
            return "";
        }
        return text.Replace(",", " ");
    }
}
