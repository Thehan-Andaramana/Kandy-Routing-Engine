# Draws the Kandy road network with a route, a before / after pair of routes,
# or the places reachable within a budget. Reads the CSV files from RouteExporter.cs.
#
# python scripts/plot_route.py results/route_a.csv --network results/network_a.csv
# python scripts/plot_route.py results/route_a.csv results/route_b.csv --network results/network_b.csv
# python scripts/plot_route.py --reach results/reach_a.csv --network results/network_a.csv

import argparse
import csv
import os
import sys

import matplotlib.pyplot as plt

ROUTE_COLOURS = ["tab:blue", "tab:orange"]
MARGIN = 0.006        # space around the drawn places, in degrees
LABEL_GAP = 0.0004    # names closer than this to one already drawn are skipped

# reach bands in minutes: 0-5, 5-10, 10-20, 20+
BAND_LIMITS = [5, 10, 20]
BAND_COLOURS = ["green", "gold", "darkorange", "purple"]

# number of values on the first "#" line of each file
ROUTE_INFO = 5        # label,algorithm,metric,total_km,total_min
REACH_INFO = 3        # start,metric,budget


# prints the error and ends the script
def stop(message):
    print("Error: " + message)
    sys.exit(1)


def is_number(text):
    try:
        float(text)
        return True
    except ValueError:
        return False


def number_text(value):
    # 15.0 -> "15", 2.5 -> "2.5"
    if value == int(value):
        return str(int(value))
    return str(value)


# reads one exported CSV file
def read_csv(path, info_count):
    # info_count = 0 means the file has no "#" line
    f = open(path, newline="", encoding="utf-8")
    lines = f.read().splitlines()
    f.close()

    info = []
    if info_count > 0:
        if len(lines) == 0 or not lines[0].startswith("#"):
            stop(path + ": the first line must be the '#' summary line. Export the file again.")
        info = lines[0][1:].strip().split(",")
        if len(info) != info_count:
            stop(path + ": the '#' line has " + str(len(info)) + " values, expected " + str(info_count) + ".")
        lines = lines[1:]

    rows = []
    for row in csv.DictReader(lines):
        rows.append(row)
    if len(rows) == 0:
        stop(path + ": no data rows.")
    return info, rows


# the totals on the "#" line must be numbers
def check_route_info(path, info):
    if not is_number(info[3]) or not is_number(info[4]):
        stop(path + ": total km / total min in the '#' line are not numbers.")


# the budget on the "#" line must be a number
def check_reach_info(path, info):
    if not is_number(info[2]):
        stop(path + ": budget in the '#' line is not a number.")


# the part of the map to show
def view_area(places):
    xs = []
    ys = []
    for p in places:
        xs.append(float(p["lon"]))
        ys.append(float(p["lat"]))
    return min(xs) - MARGIN, max(xs) + MARGIN, min(ys) - MARGIN, max(ys) + MARGIN


# true if one end of the road is inside the shown area
def in_view(road, area):
    min_x, max_x, min_y, max_y = area
    for end in ["from", "to"]:
        x = float(road[end + "_lon"])
        y = float(road[end + "_lat"])
        if x >= min_x and x <= max_x and y >= min_y and y <= max_y:
            return True
    return False


# all roads in grey, bridges thicker, blocked roads in dashed red
def draw_network(ax, roads, area):
    bridge_shown = False
    blocked_shown = False
    for r in roads:
        xs = [float(r["from_lon"]), float(r["to_lon"])]
        ys = [float(r["from_lat"]), float(r["to_lat"])]
        if r["blocked"] == "true":
            ax.plot(xs, ys, color="red", linestyle="--", linewidth=2.5, zorder=5)
            if in_view(r, area):
                blocked_shown = True
        elif r["road_type"] == "bridge":
            ax.plot(xs, ys, color="darkgrey", linewidth=3.5, zorder=1)
            if in_view(r, area):
                bridge_shown = True
        else:
            ax.plot(xs, ys, color="lightgrey", linewidth=1, zorder=1)

    # legend entries only for what is visible on this map
    if bridge_shown:
        ax.plot([], [], color="darkgrey", linewidth=3.5, label="bridge")
    if blocked_shown:
        ax.plot([], [], color="red", linestyle="--", linewidth=2.5, label="blocked road")


# writes a place name unless another name is already next to it
def add_label(ax, name, x, y, placed, dx, dy):
    for p in placed:
        if abs(x - p[0]) < LABEL_GAP and abs(y - p[1]) < LABEL_GAP:
            return
    placed.append([x, y])
    ax.annotate(name, (x, y), xytext=(dx, dy), textcoords="offset points", fontsize=7, zorder=7,
                bbox=dict(facecolor="white", alpha=0.85, edgecolor="none", pad=1.5))


# draws one route with its start and destination markers
def draw_route(ax, rows, colour, legend_text, placed):
    xs = []
    ys = []
    for r in rows:
        xs.append(float(r["lon"]))
        ys.append(float(r["lat"]))

    last = len(rows) - 1
    ax.plot(xs, ys, color=colour, linewidth=4, alpha=0.8, zorder=4, label=legend_text)
    ax.plot(xs[0], ys[0], marker="^", markersize=14, color=colour, markeredgecolor="black", zorder=6)
    ax.plot(xs[last], ys[last], marker="*", markersize=20, color=colour, markeredgecolor="black", zorder=6)

    # start and destination first, so their names are never the ones skipped
    add_label(ax, rows[0]["name"], xs[0], ys[0], placed, -12, 14)
    add_label(ax, rows[last]["name"], xs[last], ys[last], placed, 10, 6)
    for i in range(1, last):
        add_label(ax, rows[i]["name"], xs[i], ys[i], placed, 6, 4)


# splits the budget into time bands for the colours
def make_bands(budget):
    bands = []
    low = 0
    for i in range(len(BAND_LIMITS)):
        high = BAND_LIMITS[i]
        if high >= budget:
            bands.append([low, budget, BAND_COLOURS[i]])
            return bands
        bands.append([low, high, BAND_COLOURS[i]])
        low = high
    bands.append([low, budget, BAND_COLOURS[len(BAND_LIMITS)]])
    return bands


# draws the reachable places, coloured by time band
def draw_reach(ax, info, rows):
    start = info[0]
    metric = info[1]
    budget = float(info[2])
    unit = "min"
    if metric == "distance":
        unit = "km"

    bands = make_bands(budget)
    for b in range(len(bands)):
        low = bands[b][0]
        high = bands[b][1]
        colour = bands[b][2]
        is_last = (b == len(bands) - 1)

        xs = []
        ys = []
        for r in rows:
            cost = float(r["cost"])
            if cost >= low and (cost < high or (is_last and cost <= high)):
                xs.append(float(r["lon"]))
                ys.append(float(r["lat"]))

        if len(xs) > 0:
            text = number_text(low) + "-" + number_text(high) + " " + unit + " (" + str(len(xs)) + " places)"
            ax.scatter(xs, ys, s=40, color=colour, edgecolors="black", zorder=5, label=text)

    start_name = start
    for r in rows:
        if r["id"] == start:
            start_name = r["name"]
            ax.plot(float(r["lon"]), float(r["lat"]), marker="s", markersize=12, linestyle="none",
                    color="black", zorder=6, label="start")

    return ("Reachable from " + start_name + " within " + number_text(budget) + " " + unit
            + " (" + str(len(rows)) + " places)")


# file name without folder and extension
def file_stem(path):
    return os.path.splitext(os.path.basename(path))[0]


def main():
    parser = argparse.ArgumentParser(description="Draw routes / reachable places on the Kandy network.")
    parser.add_argument("routes", nargs="*", help="one route_*.csv, or two: before then after")
    parser.add_argument("--reach", help="a reach_*.csv file")
    parser.add_argument("--network", required=True, help="network_*.csv in the same state as the routes")
    args = parser.parse_args()

    if len(args.routes) == 0 and args.reach is None:
        stop("give at least one route file or a --reach file.")
    if len(args.routes) > 2:
        stop("at most two route files (before and after).")

    files = []
    for path in args.routes:
        files.append(path)
    if args.reach is not None:
        files.append(args.reach)
    files.append(args.network)
    for path in files:
        if not os.path.exists(path):
            stop("file not found: " + path)

    routes = []
    shown = []
    for path in args.routes:
        info, rows = read_csv(path, ROUTE_INFO)
        check_route_info(path, info)
        routes.append([info, rows])
        for r in rows:
            shown.append(r)

    reach = None
    if args.reach is not None:
        info, rows = read_csv(args.reach, REACH_INFO)
        check_reach_info(args.reach, info)
        reach = [info, rows]
        for r in rows:
            shown.append(r)

    area = view_area(shown)
    network_info, roads = read_csv(args.network, 0)

    fig, ax = plt.subplots(figsize=(12, 9))
    draw_network(ax, roads, area)

    titles = []
    placed = []
    if len(routes) == 1:
        info = routes[0][0]
        totals = info[3] + " km, " + info[4] + " min"
        draw_route(ax, routes[0][1], ROUTE_COLOURS[0], info[0] + ": " + totals, placed)
        titles.append(info[0] + " - " + totals)
    elif len(routes) == 2:
        before = routes[0][0]
        after = routes[1][0]
        draw_route(ax, routes[0][1], ROUTE_COLOURS[0],
                   "BEFORE (" + before[0] + "): " + before[3] + " km, " + before[4] + " min", placed)
        draw_route(ax, routes[1][1], ROUTE_COLOURS[1],
                   "AFTER (" + after[0] + "): " + after[3] + " km, " + after[4] + " min", placed)
        titles.append("Before vs after: " + before[0] + " -> " + after[0] + " (network shown after)")
    if reach is not None:
        titles.append(draw_reach(ax, reach[0], reach[1]))

    ax.set_title("\n".join(titles))
    ax.set_xlim(area[0], area[1])
    ax.set_ylim(area[2], area[3])
    ax.set_aspect("equal")
    ax.set_xlabel("longitude")
    ax.set_ylabel("latitude")
    ax.grid(True, linewidth=0.3)
    ax.legend(loc="best", fontsize=8)

    # the PNG is saved next to the CSV with the same name
    png_name = ""
    for path in args.routes:
        if png_name == "":
            png_name = file_stem(path)
        else:
            png_name = png_name + "_vs_" + file_stem(path)
    if args.reach is not None:
        if png_name == "":
            png_name = file_stem(args.reach)
        else:
            png_name = png_name + "_with_" + file_stem(args.reach)
    png_path = os.path.join(os.path.dirname(files[0]), png_name + ".png")

    fig.savefig(png_path, dpi=150, bbox_inches="tight")
    plt.close(fig)
    print("Saved: " + png_path)


if __name__ == "__main__":
    main()