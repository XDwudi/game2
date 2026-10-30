"""Read-only comparison of the two retained real Player traces; no Player execution."""
import csv
import hashlib
import json
import math
from pathlib import Path
import statistics
import subprocess

ROOT = Path(__file__).resolve().parents[3]
OUTPUT = Path(__file__).resolve().parent
INPUTS = {
    "v05_player01": "evidence/v05/player/player-20261030-052509-0a70fdd0",
    "v04_player07": "evidence/runs/player/player-20261029-025732-31091505",
}


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def summary(values):
    values = sorted(values)
    if not values:
        return {"count": 0}
    return {"count": len(values), "mean": statistics.mean(values), "p50": values[math.ceil(len(values) * .5) - 1],
            "p95": values[math.ceil(len(values) * .95) - 1], "p99": values[math.ceil(len(values) * .99) - 1],
            "min": values[0], "max": values[-1]}


def regression(values):
    x = list(range(len(values)))
    xm, ym = statistics.mean(x), statistics.mean(values)
    slope = sum((a - xm) * (b - ym) for a, b in zip(x, values)) / sum((a - xm) ** 2 for a in x)
    intercept = ym - slope * xm
    residual = sum((b - (intercept + slope * a)) ** 2 for a, b in zip(x, values))
    total = sum((b - ym) ** 2 for b in values)
    return {"slopePerTransaction": slope, "intercept": intercept, "rSquared": 1 - residual / total}


def analyze(path):
    report = json.loads((path / "player-report.json").read_text(encoding="utf-8-sig"))
    rows = list(csv.DictReader((path / "performance-frames.csv").open(encoding="utf-8-sig")))
    raw_count = len(rows)
    # The original acceptance code excludes only its first partial interval.
    # Match that existing rule; retain all 180-second complete-frame observations.
    data = [{key: float(value) if value else None for key, value in row.items()} for row in rows[1:]]
    p = report["performance"]
    measured = summary([row["interval_ms"] for row in data])
    assert measured["count"] == p["frameCount"]
    assert abs(measured["p95"] - p["frameInterval"]["p95"]) < 1e-9
    groups = {}
    transaction_indices = [i for i, row in enumerate(data) if row["gc_alloc_bytes"] >= 200000]
    transaction_execution_indices = {i - 1 for i in transaction_indices if i > 0}
    for label, low, high in (("nontransaction_refresh_50KB_to_200KB", 50000, 200000),
                             ("transaction_over_200KB", 200000, float("inf"))):
        indices = [i for i, row in enumerate(data) if low <= row["gc_alloc_bytes"] < high]
        group = {"count": len(indices), "allocationBytes": summary([data[i]["gc_alloc_bytes"] for i in indices]),
                 "allocationTotalBytes": sum(data[i]["gc_alloc_bytes"] for i in indices), "intervalByRowOffset": {}}
        for lag in (-1, 0, 1):
            values = [data[i + lag]["interval_ms"] for i in indices if 0 <= i + lag < len(data)]
            group["intervalByRowOffset"][str(lag)] = {**summary(values), "over16_7": sum(x > 16.7 for x in values),
                                                       "over20": sum(x > 20 for x in values)}
        groups[label] = group
    initial = json.loads((path / "debug-initial-state.json").read_text(encoding="utf-8-sig"))
    final = json.loads((path / "debug-final-state.json").read_text(encoding="utf-8-sig"))
    kinds = {}
    for receipt in final["processedReceipts"]:
        kind = str(receipt["command"]["kind"])
        kinds[kind] = kinds.get(kind, 0) + 1
    bindings = [{"path": pth.relative_to(ROOT).as_posix(), "sha256": digest(pth), "byteSize": pth.stat().st_size}
                for pth in (path / "player-report.json", path / "performance-frames.csv", path / "debug-initial-state.json", path / "debug-final-state.json")]
    return {"reportedVerdict": report["status"], "reportedPerformance": p, "environment": report["environment"],
            "rawRowCount": raw_count, "completeFrameSummary": measured,
            "allCompleteFramesOverThreshold": {str(limit): sum(row["interval_ms"] > limit for row in data) for limit in (16.7, 16.77, 17, 18, 20, 25, 30, 40)},
            "allocationTotalBytes": sum(row["gc_alloc_bytes"] or 0 for row in data),
            "uiBuildTotalMs": sum(row["ui_build_ms"] or 0 for row in data),
            "diagnosticGroupsNotAlternativeAcceptance": groups,
            "over20FramesLinkedToNextRowTransactionAllocation": sum(row["interval_ms"] > 20 and i in transaction_execution_indices for i, row in enumerate(data)),
            "over16_7OutsideTransactionExecutionRows": sum(row["interval_ms"] > 16.7 and i not in transaction_execution_indices for i, row in enumerate(data)),
            "transactionAllocationLinearFit": regression([data[i]["gc_alloc_bytes"] for i in transaction_indices]),
            "quarters": [{"seconds": [quarter * 45, (quarter + 1) * 45], **summary([row["interval_ms"] for row in data if quarter * 45 <= row["elapsed_s"] < (quarter + 1) * 45])} for quarter in range(4)],
            "top20IntervalsWithAdjacentProfilerRows": [{"previous": data[i - 1] if i else None, "current": data[i],
                                                         "next": data[i + 1] if i + 1 < len(data) else None}
                                                        for i in sorted(range(len(data)), key=lambda i: data[i]["interval_ms"], reverse=True)[:20]],
            "history": {"initialReceiptCount": len(initial["processedReceipts"]), "finalReceiptCount": len(final["processedReceipts"]),
                        "finalCommandKindCounts": kinds, "initialJsonBytes": (path / "debug-initial-state.json").stat().st_size,
                        "finalJsonBytes": (path / "debug-final-state.json").stat().st_size}, "inputBindings": bindings}


def main():
    result = {"scope": "Diagnostic grouping of retained real full traces; no rerun, no altered gate, no frame rejection beyond original first partial interval.",
              "comparability": "Same Windows hardware/Unity and 190 each Roll/Reorder/selection including warmup; v05 required K8 replaces v04 K7 and carries new content/enchantments. Cross-version comparison is not an A/B attribution experiment.",
              "runs": {label: analyze(ROOT / relative) for label, relative in INPUTS.items()}}
    source_commit = json.loads(json.loads((ROOT / INPUTS["v05_player01"] / "player-report.json").read_text(encoding="utf-8-sig"))["buildIdentity"])["sourceCommit"]
    result["v05SourceCommit"] = source_commit
    result["sourceBindings"] = []
    for relative in ("DicePrototype/Assets/Game/Runtime/AtomicSaveStore.cs", "DicePrototype/Assets/Game/Core/Session/GameSession.cs",
                     "DicePrototype/Assets/Game/Core/Session/GameSession.Validation.cs", "DicePrototype/Assets/Game/Core/Session/SessionModels.cs",
                     "DicePrototype/Assets/Game/Core/Rules/ScoreCalculator.cs", "DicePrototype/Assets/Game/Core/Rules/ScoreExplanation.cs",
                     "DicePrototype/Assets/Game/Runtime/GameApp.cs", "DicePrototype/Assets/Game/UI/UiKit.cs",
                     "DicePrototype/Assets/Game/Runtime/Verification/PlayerVerification.cs"):
        data = subprocess.check_output(["git", "show", source_commit + ":" + relative], cwd=ROOT)
        result["sourceBindings"].append({"sourceCommit": source_commit, "path": relative, "sha256": hashlib.sha256(data).hexdigest()})
    (OUTPUT / "player01-v04-comparison.json").write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    for key, value in result["runs"].items():
        print(key, json.dumps({"fullFrame": value["completeFrameSummary"], "allocationTotalBytes": value["allocationTotalBytes"],
                              "transactionGrowth": value["transactionAllocationLinearFit"], "linkedOver20": value["over20FramesLinkedToNextRowTransactionAllocation"],
                              "remainingOver16_7": value["over16_7OutsideTransactionExecutionRows"], "history": value["history"]}))


if __name__ == "__main__":
    main()
