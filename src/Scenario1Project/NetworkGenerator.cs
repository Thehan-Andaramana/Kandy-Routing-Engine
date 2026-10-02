// Builds random connected road networks of a given size.
// Used only by the benchmark to test how the algorithms scale.

using System.Globalization;

public class NetworkGenerator
{
    const double CentreLat = 7.29;
    const double CentreLon = 80.63;
    const double SpacingDeg = 0.006;   // about 650 m between neighbours
    const double RoadFactor = 1.25;   // road length = straight line x 1.25
    const int SpeedKmh = 30;
    const double ThirdNeighbourChance = 0.25;

    public string folder;

    public NetworkGenerator(string folder)
    {
        this.folder = folder;
    }

    public string NodesPath(int n)
    {
        return Path.Combine(folder, "net_" + n + "_nodes.csv");
    }

    public string EdgesPath(int n)
    {
        return Path.Combine(folder, "net_" + n + "_edges.csv");
    }

    // writes the node and edge CSV files of a random network with n locations
    public bool Generate(int n, int seed)
    {
        if (n < 2)
        {
            Console.WriteLine("A network needs at least 2 locations (asked for " + n + ").");
            return false;
        }

        if (File.Exists(NodesPath(n)) && File.Exists(EdgesPath(n)))
        {
            Console.WriteLine("net_" + n + " already exists - skipped.");
            return true;
        }

        // same seed gives the same network every time
        Random random = new Random(seed);

        // the area grows with n so the density stays the same
        double side = Math.Sqrt(n) * SpacingDeg;

        double[] lat = new double[n];
        double[] lon = new double[n];
        for (int i = 0; i < n; i++)
        {
            lat[i] = Math.Round(CentreLat - side / 2 + random.NextDouble() * side, 5);
            lon[i] = Math.Round(CentreLon - side / 2 + random.NextDouble() * side, 5);
        }

        List<int>[] links = new List<int>[n];
        for (int i = 0; i < n; i++)
        {
            links[i] = new List<int>();
        }
        List<string> roadLines = new List<string>();
        roadLines.Add("from,to,distance_km,base_time_min,speed_limit_kmh,road_type,one_way");

        double[] d = new double[n];

        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                d[j] = Haversine(lat[i], lon[i], lat[j], lon[j]);
            }

            // joining to an earlier location keeps the whole network connected
            if (i > 0)
            {
                int nearestEarlier = 0;
                for (int j = 1; j < i; j++)
                {
                    if (d[j] < d[nearestEarlier])
                    {
                        nearestEarlier = j;
                    }
                }
                AddLink(links, roadLines, i, nearestEarlier, d[nearestEarlier]);
            }

            // then 2 or 3 nearest neighbours, like a real junction
            int k = 2;
            if (random.NextDouble() < ThirdNeighbourChance)
            {
                k = 3;
            }

            d[i] = double.PositiveInfinity;
            for (int t = 0; t < k; t++)
            {
                int nearest = -1;
                for (int j = 0; j < n; j++)
                {
                    if (d[j] != double.PositiveInfinity && (nearest == -1 || d[j] < d[nearest]))
                    {
                        nearest = j;
                    }
                }
                if (nearest == -1)
                {
                    break;
                }
                AddLink(links, roadLines, i, nearest, d[nearest]);
                d[nearest] = double.PositiveInfinity;
            }
        }

        List<string> nodeLines = new List<string>();
        nodeLines.Add("id,name,type,lat,lon");
        for (int i = 0; i < n; i++)
        {
            nodeLines.Add(i + ",Place " + i + ",junction," + Num(lat[i]) + "," + Num(lon[i]));
        }

        if (!WriteFile(NodesPath(n), nodeLines) || !WriteFile(EdgesPath(n), roadLines))
        {
            return false;
        }
        Console.WriteLine("net_" + n + ": " + n + " locations, " + (roadLines.Count - 1) + " roads written.");

        return true;
    }

    // adds a two-way road between a and b, unless it is already there
    private void AddLink(List<int>[] links, List<string> roadLines, int a, int b, double straightKm)
    {
        for (int x = 0; x < links[a].Count; x++)
        {
            if (links[a][x] == b)
            {
                return;
            }
        }
        links[a].Add(b);
        links[b].Add(a);

        double distance = Math.Round(straightKm * RoadFactor, 3);
        if (distance < 0.001)
        {
            distance = 0.001;
        }

        double time = Math.Round(distance / SpeedKmh * 60, 2);
        if (time < 0.01)
        {
            time = 0.01;
        }

        roadLines.Add(a + "," + b + "," + Num(distance) + "," + Num(time) + "," + SpeedKmh + ",minor,false");
    }

    // straight-line distance in km between two coordinates
    private double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        double earthRadiusKm = 6371.0;
        double dLat = (lat2 - lat1) * Math.PI / 180;
        double dLon = (lon2 - lon1) * Math.PI / 180;
        double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                   + Math.Cos(lat1 * Math.PI / 180) * Math.Cos(lat2 * Math.PI / 180)
                   * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
        return 2 * earthRadiusKm * Math.Asin(Math.Sqrt(a));
    }

    private string Num(double value)
    {
        return value.ToString("0.#####", CultureInfo.InvariantCulture);
    }

    private bool WriteFile(string path, List<string> lines)
    {
        try
        {
            Directory.CreateDirectory(folder);
            File.WriteAllLines(path, lines);
        }
        catch (IOException e)
        {
            Console.WriteLine("Could not write " + path + ": " + e.Message);
            return false;
        }
        catch (UnauthorizedAccessException e)
        {
            Console.WriteLine("Could not write " + path + " - no permission. " + e.Message);
            return false;
        }
        return true;
    }
}
