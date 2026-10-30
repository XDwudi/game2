#!/usr/bin/env python3
"""Compare actual Windows/macOS Player evidence. Uses only Python's standard library."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import sys

HEX64 = re.compile(r"^[0-9a-f]{64}$")
NORMAL_FIELDS = ("index", "kind", "actionSequence", "phase", "submissionScore", "stageScore", "target", "gold",
                 "stageIndex", "rollsRemaining", "remainingSubmissions", "diceRngState", "shopRngState",
                 "effectRngState", "isDebug", "qualified", "ruleStateSha256")
DEBUG_FIELDS = ("kind", "sequence", "stage", "phase", "qualified", "gold", "score", "risk", "ruleStateSha256")


def sha256(path):
    value = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def load_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def load_rows(path):
    rows = []
    for number, line in enumerate(Path(path).read_text(encoding="utf-8-sig").splitlines(), 1):
        if not line.strip():
            continue
        # Older Unity AppendAllText implementations can prefix individual records with a BOM.
        row = json.loads(line.lstrip("\ufeff"))
        if not isinstance(row, dict):
            raise ValueError(f"{path}: line {number} is not an object")
        rows.append(row)
    if not rows:
        raise ValueError(f"{path}: no records")
    return rows


def compare_stream(left, right, fields, kind):
    failures = []
    if len(left) != len(right):
        failures.append({"stream": kind, "reason": "record_count", "windows": len(left), "macOS": len(right)})
    sequence = "actionSequence" if kind == "normal" else "sequence"
    for side, rows in (("windows", left), ("macOS", right)):
        for index, row in enumerate(rows):
            missing = [key for key in fields if key not in row]
            if missing:
                failures.append({"stream": kind, "side": side, "row": index, "reason": "missing_fields", "fields": missing})
            if not isinstance(row.get("ruleStateSha256"), str) or not HEX64.fullmatch(row["ruleStateSha256"]):
                failures.append({"stream": kind, "side": side, "row": index, "reason": "invalid_rule_hash"})
            # Debug24's saved initial fixture already contains NewRun (sequence 1).
            first_sequence = 1 if kind == "normal" else 2
            if type(row.get(sequence)) is not int or row[sequence] != index + first_sequence:
                failures.append({"stream": kind, "side": side, "row": index, "reason": "non_contiguous_sequence"})
            if kind == "normal":
                if row.get("index") != index:
                    failures.append({"stream": kind, "side": side, "row": index, "reason": "invalid_index"})
                if row.get("savedStateMatches") is not True or row.get("renderedStateMatches") is not True:
                    failures.append({"stream": kind, "side": side, "row": index, "reason": "disk_or_ui_not_verified"})
                if row.get("isDebug") is not False:
                    failures.append({"stream": kind, "side": side, "row": index, "reason": "normal_trace_is_debug"})
    for index, (a, b) in enumerate(zip(left, right)):
        for key in fields:
            if key in a and key in b and (type(a[key]) is not type(b[key]) or a[key] != b[key]):
                failures.append({"stream": kind, "row": index, "field": key, "windows": a[key], "macOS": b[key]})
    return {"stream": kind, "windowsRecords": len(left), "macOSRecords": len(right),
            "comparedFields": list(fields), "passed": not failures, "failures": failures}


def compare_directories(windows_dir, mac_dir):
    windows_dir, mac_dir = Path(windows_dir), Path(mac_dir)
    windows = load_json(windows_dir / "player-report.json")
    mac = load_json(mac_dir / "player-report.json")
    failures = []
    if windows.get("configVersion") != "p0.5.0" or mac.get("configVersion") != "p0.5.0":
        failures.append({"field": "configVersion", "reason": "wrong_release_version"})
    for key in ("schema", "probeRevision", "traceSha256", "configSha256", "configVersion"):
        if not windows.get(key) or windows.get(key) != mac.get(key):
            failures.append({"field": key, "windows": windows.get(key), "macOS": mac.get(key)})
    identities = []
    for report in (windows, mac):
        raw = report.get("buildIdentity")
        identities.append(json.loads(raw) if isinstance(raw, str) and raw else {})
    for key in ("sourceCommit", "contentConfigSha256", "goldenCaseSetSha256"):
        if not identities[0].get(key) or identities[0].get(key) != identities[1].get(key):
            failures.append({"field": "buildIdentity." + key, "windows": identities[0].get(key), "macOS": identities[1].get(key)})
    for side, report, platform in (("windows", windows, "WindowsPlayer"), ("macOS", mac, "OSXPlayer")):
        if report.get("status") != "passed" or report.get("exitCode") != 0:
            failures.append({"side": side, "reason": "player_did_not_pass", "status": report.get("status"), "exitCode": report.get("exitCode")})
        if report.get("environment", {}).get("platform") != platform or report.get("environment", {}).get("isEditor") is not False:
            failures.append({"side": side, "reason": "wrong_platform_or_editor"})
        expected_arch = "X64" if side == "windows" else "Arm64"
        if report.get("environment", {}).get("processArchitecture") != expected_arch:
            failures.append({"side": side, "reason": "wrong_process_architecture"})
        if report.get("normalReplay", {}).get("passed") is not True or report.get("debug24", {}).get("passed") is not True:
            failures.append({"side": side, "reason": "replay_not_passed"})
        if report.get("stressCompleted") is not True or report.get("handbookPureReadChecks", 0) < 20:
            failures.append({"side": side, "reason": "required_probe_section_missing"})
        performance = report.get("performance") or {}
        if (performance.get("status") != "within_frame_budget" or performance.get("sampleSeconds", 0) < 180
                or performance.get("targetResolutionMet") is not True or performance.get("frameBudgetMet") is not True
                or any(performance.get(key) != value for key, value in (("dice", 10), ("submitSlots", 8), ("controllers", 10), ("inventory", 5)))):
            failures.append({"side": side, "reason": "pressure_gate_not_met"})
        if report.get("unityErrors") != []:
            failures.append({"side": side, "reason": "unity_errors_or_missing_field"})
        if report.get("checkpointExported") is not True:
            failures.append({"side": side, "reason": "cross_platform_checkpoint_not_exported"})
    if mac.get("importedCheckpointStatus") != "passed":
        failures.append({"side": "macOS", "reason": "windows_checkpoint_not_loaded_and_replayed"})
    normal = compare_stream(load_rows(windows_dir / "normal-replay.jsonl"), load_rows(mac_dir / "normal-replay.jsonl"), NORMAL_FIELDS, "normal")
    debug = compare_stream(load_rows(windows_dir / "debug24-replay.jsonl"), load_rows(mac_dir / "debug24-replay.jsonl"), DEBUG_FIELDS, "debug24")
    for side, report, directory in (("windows", windows, windows_dir), ("macOS", mac, mac_dir)):
        rows = load_rows(directory / "normal-replay.jsonl")
        expected = report.get("normalReplay", {}).get("expectedSteps")
        completed = report.get("normalReplay", {}).get("completedSteps")
        if type(expected) is not int or expected != len(rows) or completed != expected:
            failures.append({"side": side, "reason": "normal_report_count_mismatch", "expected": expected, "completed": completed, "records": len(rows)})
        steps = load_rows(directory / "debug24-replay.jsonl")
        if steps[-1].get("stage") != 24 or steps[-1].get("phase") != "RunWon":
            failures.append({"side": side, "reason": "debug24_final_state_not_won_24"})
    inputs = []
    for side, directory in (("windows", windows_dir), ("macOS", mac_dir)):
        for name in ("player-report.json", "normal-replay.jsonl", "debug24-replay.jsonl"):
            path = directory / name
            inputs.append({"side": side, "file": name, "sha256": sha256(path), "byteSize": path.stat().st_size})
    return {"schema": "p05-cross-platform-comparison-1", "passed": not failures and normal["passed"] and debug["passed"],
            "scope": "Actual replay commands, canonical gameplay hashes and Windows checkpoint loaded on Mac; does not replace reverse checkpoint, manual input, OS restart, or Intel testing.",
            "reportFailures": failures, "normal": normal, "debug24": debug, "inputs": inputs}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--windows-results", required=True, type=Path)
    parser.add_argument("--mac-results", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    try:
        result = compare_directories(args.windows_results, args.mac_results)
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        result = {"schema": "p05-cross-platform-comparison-1", "passed": False, "error": str(error)}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Cross-platform comparison: " + ("PASS" if result["passed"] else "FAIL"))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
