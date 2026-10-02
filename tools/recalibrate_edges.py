# Recomputes distance_km and base_time_min in data/kandy_edges.csv from the
# coordinates, a winding factor and an average speed.
#
# python tools/recalibrate_edges.py
# python tools/recalibrate_edges.py --out edges_test.csv

import argparse
import csv
import math

TOWN_RADIUS_KM = 1.5     # distance from node 0 (Clock Tower) that counts as town
WINDING_TOWN, WINDING_OUT = 1.7, 1.25
AVG_SPEED_TOWN = {'A': 15, 'B': 14, 'minor': 12, 'bridge': 15}   # km/h
AVG_SPEED_OUT = {'A': 35, 'B': 28, 'minor': 22, 'bridge': 25}

# one-way corrections from the Google Maps check
REMOVE = {(1, 0), (87, 0), (100, 0)}   # no direct road in this direction
FLIP = {(0, 94), (4, 12)}              # becomes one-way, second -> first

# roads that are straighter than the zone factor assumes
WINDING_OVERRIDE = {(94, 12): 1.1, (14, 3): 1.35, (98, 12): 1.2}
# average speeds fitted to Google Maps trips
SPEED_OVERRIDE = {(14, 3): 24, (98, 12): 12}


# straight-line distance in km between two coordinates
def haversine_km(a, b):
    r = 6371.0
    p1, p2 = math.radians(a[0]), math.radians(b[0])
    h = (math.sin((p2 - p1) / 2) ** 2
         + math.cos(p1) * math.cos(p2) * math.sin(math.radians(b[1] - a[1]) / 2) ** 2)
    return 2 * r * math.asin(math.sqrt(h))


def main():
    ap = argparse.ArgumentParser(description='Recompute distance_km and base_time_min in kandy_edges.csv')
    ap.add_argument('--nodes', default='data/kandy_nodes.csv')
    ap.add_argument('--edges', default='data/kandy_edges.csv')
    ap.add_argument('--out', default=None, help='output file (default: overwrite --edges)')
    args = ap.parse_args()

    with open(args.nodes, newline='', encoding='utf-8') as fh:
        nodes = {int(r['id']): (float(r['lat']), float(r['lon'])) for r in csv.DictReader(fh)}
    with open(args.edges, newline='', encoding='utf-8') as fh:
        rows = list(csv.DictReader(fh))

    out = []
    for r in rows:
        f, t, one_way = int(r['from']), int(r['to']), r['one_way']
        if (f, t) in REMOVE:
            continue
        if (f, t) in FLIP and one_way == 'false':
            f, t, one_way = t, f, 'true'
        in_town = (haversine_km(nodes[0], nodes[f]) <= TOWN_RADIUS_KM
                   and haversine_km(nodes[0], nodes[t]) <= TOWN_RADIUS_KM)
        winding = WINDING_TOWN if in_town else WINDING_OUT
        winding = WINDING_OVERRIDE.get((f, t), winding)
        speed = (AVG_SPEED_TOWN if in_town else AVG_SPEED_OUT)[r['road_type']]
        speed = SPEED_OVERRIDE.get((f, t), speed)
        if speed > float(r['speed_limit_kmh']):
            raise SystemExit('average speed %s above speed_limit_kmh on edge %d,%d' % (speed, f, t))
        dist = max(0.1, round(haversine_km(nodes[f], nodes[t]) * winding, 1))
        minutes = round(dist / speed * 60, 1)
        out.append([f, t, dist, minutes, r['speed_limit_kmh'], r['road_type'], one_way])

    target = args.out or args.edges
    with open(target, 'w', newline='', encoding='utf-8') as fh:
        w = csv.writer(fh, lineterminator='\n')
        w.writerow(['from', 'to', 'distance_km', 'base_time_min', 'speed_limit_kmh', 'road_type', 'one_way'])
        w.writerows(out)
    print('read %d rows, wrote %d rows to %s' % (len(rows), len(out), target))


if __name__ == '__main__':
    main()
