"""Read-only independent full-frame comparison; no new acceptance threshold."""
import csv, hashlib, json, math, statistics
from pathlib import Path

root = Path(__file__).resolve().parents[2]
paths = {
    "Player06": root / "evidence/runs/player/player-20261029-024615-1c6316db",
    "Player07": root / "evidence/runs/player/player-20261029-025732-31091505",
}
supplied = json.loads((root / "evidence/runs/perf/player06-vs-player07.json").read_text(encoding="utf-8"))["runs"]
results = {}
for name, path in paths.items():
    source = path / "performance-frames.csv"
    rows = list(csv.DictReader(source.open(encoding="utf-8-sig", newline="")))[1:]
    clocks = {}
    for axis in ["interval_ms", "main_thread_ms", "frame_start_delta_ms", "present_called_delta_ms", "frame_complete_delta_ms"]:
        values = [float(r[axis]) for r in rows if r[axis]]
        ordered = sorted(values)
        result = dict(n=len(values), mean=statistics.mean(values), p95=ordered[math.ceil(.95 * len(values)) - 1], max=max(values))
        for key, value in result.items():
            assert math.isclose(value, supplied[name]["full_clock_stats"][axis][key], rel_tol=1e-11, abs_tol=1e-11), (name, axis, key)
        result["over18ms"] = sum(v > 18 for v in values)
        result["over20ms"] = sum(v > 20 for v in values)
        assert result["over18ms"] == supplied[name]["full_clock_counts"][axis]["18"]
        assert result["over20ms"] == supplied[name]["full_clock_counts"][axis]["20"]
        clocks[axis] = result
    present = [float(r["present_called_delta_ms"]) if r["present_called_delta_ms"] else None for r in rows]
    long_short = sum(a is not None and b is not None and a > 18 and b < 15.5 for a, b in zip(present, present[1:]))
    allocated = sum(float(r["gc_alloc_bytes"]) for r in rows)
    assert allocated == supplied[name]["gc_bytes_sum"]
    results[name] = dict(csvSha256=hashlib.sha256(source.read_bytes()).hexdigest(), frames=len(rows), clocks=clocks,
                         entire_series_adjacent_present_over18_then_under15_5=long_short, totalAllocatedBytes=allocated)

native_log = (root / "evidence/runs/player-console-07.log").read_text(encoding="utf-8-sig")
suspect = [line for line in native_log.splitlines() if any(t in line.lower() for t in ["exception:", "error:", "getframestatistics", "timestamp is broken", "crash!!!"])]
result = dict(results=results, nativeLogSuspectLines=suspect,
              caveat="Counts above 18/20 ms describe retained tails; they are not additional acceptance thresholds. Entire-series adjacent pair counting is independent of operation inference. No native frame identity or physical scanout is asserted.")
(root / "evidence/review/player06-07-comparison-independent.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
print(json.dumps(result, indent=2))
assert not suspect
