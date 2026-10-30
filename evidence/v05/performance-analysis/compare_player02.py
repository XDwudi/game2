"""Compare retained Player evidence only; never starts Unity or rewrites input files."""
import hashlib
import json
import subprocess

from analyze_player01 import ROOT, OUTPUT, analyze, digest


RUNS = {
    "Player01": ROOT / "evidence/v05/player/player-20261030-052509-0a70fdd0",
    "Player02": ROOT / "evidence/v05/player/player-20261030-054053-169bea91",
}
UNCHANGED = (
    "DicePrototype/Assets/Game/Runtime/FramePacer.cs",
    "DicePrototype/Assets/Game/Runtime/Verification/PlayerVerification.cs",
    "DicePrototype/Assets/Game/Core/Rules/ScoreCalculator.cs",
    "DicePrototype/Assets/Game/Core/Session/GameSession.Validation.cs",
    "DicePrototype/Assets/Game/Core/Session/GameSession.cs",
    "DicePrototype/Assets/Game/Data/P0Config.json",
    "DicePrototype/Assets/Game/Data/GoldenScores.json",
    "DicePrototype/ProjectSettings/QualitySettings.asset",
    "DicePrototype/ProjectSettings/ProjectSettings.asset",
    "DicePrototype/Assets/Settings/BuildProfiles/Windows_x64.asset",
    "DicePrototype/Packages/manifest.json",
    "DicePrototype/Packages/packages-lock.json",
)
CONTRACT_FIELDS = (
    "targetFrameMs", "targetFps", "targetResolution", "targetResolutionMet",
    "isDebugFixture", "dice", "submitSlots", "controllers", "inventory",
    "warmupSeconds", "requiredMetricCoverage", "operationSchedule",
    "operationsIncludingWarmup", "frameBufferMinimumCapacity",
    "frameBufferCapacitySafetyFactor", "productionFramePacing",
    "productionFramePacingAfterSampling",
)


def load(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def git(*arguments):
    return subprocess.check_output(["git", *arguments], cwd=ROOT)


def main():
    reports = {name: load(path / "player-report.json") for name, path in RUNS.items()}
    identities = {name: json.loads(report["buildIdentity"]) for name, report in reports.items()}
    commits = {name: identity["sourceCommit"] for name, identity in identities.items()}
    before, after = commits["Player01"], commits["Player02"]
    result = {
        "scope": "Independent read-only comparison of actual retained Player01/02; no Player run, threshold/schedule change or frame selection.",
        "sourceCommits": commits,
        "unchangedSourceAndConfiguration": [],
        "actualRuntimeContract": [],
        "sourcePathsChanged": git("diff", "--name-only", before, after, "--", "DicePrototype/Assets/Game", "DicePrototype/ProjectSettings", "DicePrototype/Packages").decode().splitlines(),
        "replayComparison": [],
    }
    for path in UNCHANGED:
        old = hashlib.sha256(git("show", before + ":" + path)).hexdigest()
        new = hashlib.sha256(git("show", after + ":" + path)).hexdigest()
        assert old == new, path
        result["unchangedSourceAndConfiguration"].append({"path": path, "beforeSha256": old, "afterSha256": new, "identical": True})
    for field in CONTRACT_FIELDS:
        old, new = (reports[name]["performance"][field] for name in RUNS)
        assert old == new, field
        result["actualRuntimeContract"].append({"field": field, "before": old, "after": new, "identical": True})
    for name in ("normal-replay.jsonl", "debug24-replay.jsonl"):
        lines = [[json.loads(line) for line in (path / name).read_text(encoding="utf-8-sig").splitlines() if line] for path in RUNS.values()]
        normalized = [[{k: v for k, v in row.items() if k != "commandId"} for row in rows] for rows in lines]
        assert normalized[0] == normalized[1], name
        result["replayComparison"].append({
            "name": name, "beforeRows": len(lines[0]), "afterRows": len(lines[1]),
            "sameGameplayFieldsAndCanonicalHashes": True, "excludedField": "commandId", "differences": [],
            "inputSha256": {label: digest(path / name) for label, path in RUNS.items()},
        })
    for name, path in RUNS.items():
        result[name] = analyze(path)
        result[name]["buildIdentity"] = identities[name]
        result[name]["probeRevision"] = reports[name]["probeRevision"]
        result[name]["probeAssemblyMvid"] = reports[name]["probeAssemblyMvid"]
        result[name]["exitCode"] = reports[name]["exitCode"]
        result[name]["traceSha256"] = reports[name]["traceSha256"]
        result[name]["functionalSummary"] = {
            "normalReplayRows": reports[name]["normalReplay"]["completedSteps"],
            "normalFinalPhase": load(path / "normal-final-state.json")["phase"],
            "debug24Passed": reports[name]["debug24"]["passed"],
            "debug24Rows": len(reports[name]["debug24"]["steps"]),
            "handbookPureReadChecks": reports[name]["handbookPureReadChecks"],
            "unityErrors": reports[name]["unityErrors"],
        }
    old, new = result["Player01"], result["Player02"]
    assert new["reportedVerdict"] == "passed"
    assert new["completeFrameSummary"]["p95"] <= 16.7
    result["delta"] = {
        "allocationTotalBytes": new["allocationTotalBytes"] - old["allocationTotalBytes"],
        "allocationReductionPercent": (1 - new["allocationTotalBytes"] / old["allocationTotalBytes"]) * 100,
        "transactionAllocationSlopeReductionPercent": (1 - new["transactionAllocationLinearFit"]["slopePerTransaction"] / old["transactionAllocationLinearFit"]["slopePerTransaction"]) * 100,
        "averageFpsFromMeanInterval": 1000 / new["completeFrameSummary"]["mean"],
    }
    result["conclusion"] = {
        "specificationCompliance": "Windows V05-PERF original full-frame P95 gate passed; Mac is outside this review.",
        "quality": "Measured transaction allocation and slow-frame reductions are consistent with the exact-byte known-good save cache. Long-history allocation and occasional slow frames remain.",
        "attributionLimit": "Same machine and workload, one retained run per source version; timing variation and the concurrent small layout correction prevent assigning every timing difference to the cache.",
        "notAllFrames60Fps": True,
        "acceptanceUsesAllCompleteFrames": True,
        "originalFirstPartialOnlyExclusion": True,
        "diagnosticGroupsAreNotAcceptance": True,
    }
    (OUTPUT / "PLAYER02_COMPARISON.json").write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"sourceUnchanged": len(UNCHANGED), "runtimeContractIdentical": len(CONTRACT_FIELDS), "replaysIdentical": True, "delta": result["delta"]}, ensure_ascii=False))


if __name__ == "__main__":
    main()
