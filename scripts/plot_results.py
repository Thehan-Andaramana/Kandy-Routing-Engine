# Draws the benchmark charts from results/benchmark.csv:
# routing times, traversal times, critical junction / road times and memory.
#
# python scripts/plot_results.py

import csv
import os
import sys

import matplotlib.pyplot as plt

RESULTS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "results")
CSV_PATH = os.path.join(RESULTS, "benchmark.csv")

BYTES_PER_ENTRY = 8   # same size as one matrix cell


# prints the error and ends the script
def stop(message):
    print("Error: " + message)
    sys.exit(1)


# reads benchmark.csv
def read_rows(path):
    if not os.path.exists(path):
        stop(path + " not found. Run the benchmark first: dotnet run --project src/Scenario1Project -c Release -- bench")
    rows = []
    with open(path, newline="") as f:
        reader = csv.DictReader(f)
        for row in reader:
            rows.append(row)
    if len(rows) == 0:
        stop(path + " has no data rows.")
    return rows


# sizes and median times of one operation
def series(rows, operation):
    sizes = []
    times = []
    for row in rows:
        if row["operation"] == operation:
            sizes.append(int(row["size"]))
            times.append(float(row["median_ms"]))
    return sizes, times


# line chart of time against network size, one line per operation
def time_chart(rows, operations, title, file_name):
    plt.figure(figsize=(8, 5))
    drawn = 0
    for operation in operations:
        sizes, times = series(rows, operation)
        if len(sizes) == 0:
            print("No rows for " + operation + " - left out of " + file_name)
            continue
        plt.plot(sizes, times, marker="o", label=operation)
        drawn = drawn + 1

    if drawn == 0:
        plt.close()
        print("Nothing to draw for " + file_name)
        return

    plt.title(title)
    plt.xlabel("Number of locations (V)")
    plt.ylabel("Median time per run (ms)")
    plt.grid(True, alpha=0.3)
    plt.legend()
    save(file_name)


# adjacency matrix against adjacency list memory, on a log scale
def memory_chart(rows, file_name):
    sizes = []
    matrix_mb = []
    list_mb = []
    for row in rows:
        size = int(row["size"])
        if size in sizes:
            continue   # one row per size is enough
        sizes.append(size)
        matrix_mb.append(float(row["matrix_mb"]))
        list_mb.append(int(row["list_entries"]) * BYTES_PER_ENTRY / 1000000.0)

    plt.figure(figsize=(8, 5))
    plt.plot(sizes, matrix_mb, marker="o", label="Adjacency matrix (V x V cells)")
    plt.plot(sizes, list_mb, marker="o", label="Adjacency list (V + E entries)")
    plt.yscale("log")
    plt.title("Memory: adjacency matrix vs adjacency list")
    plt.xlabel("Number of locations (V)")
    plt.ylabel("Memory (MB, log scale)")
    plt.grid(True, which="both", alpha=0.3)
    plt.legend()

    # label the values of the largest network
    last = len(sizes) - 1
    plt.annotate(str(round(matrix_mb[last], 1)) + " MB", (sizes[last], matrix_mb[last]),
                 textcoords="offset points", xytext=(-40, 8))
    plt.annotate(str(round(list_mb[last], 2)) + " MB", (sizes[last], list_mb[last]),
                 textcoords="offset points", xytext=(-40, 8))
    save(file_name)


# saves the current chart into results/
def save(file_name):
    path = os.path.join(RESULTS, file_name)
    plt.tight_layout()
    plt.savefig(path, dpi=150)
    plt.close()
    print("Saved " + os.path.normpath(path))


def main():
    rows = read_rows(CSV_PATH)

    time_chart(rows, ["BFS", "Dijkstra-linear", "Dijkstra-heap"],
               "Route finding: time of one trip vs network size", "bench_routing.png")
    time_chart(rows, ["DFT", "BFT", "Reachable"],
               "Traversals (connectivity check) and Reachable (15 min)", "bench_traversal.png")
    time_chart(rows, ["CriticalJunctions", "CriticalRoads"],
               "Remove-and-test (critical junctions / roads), up to 1000 locations", "bench_critical.png")
    memory_chart(rows, "bench_memory.png")


main()