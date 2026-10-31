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
DEBUG_FIELDS = ("index", "kind", "actionSequence", "stageIndex", "phase", "gold", "score", "risk", "ruleStateSha256")
REQUIRED_V06_CHECKS = {"debug-password-isolation", "debug-editor-full-configuration", "controller-drag-and-cancel",
                       "controller-stage-frozen", "display-state-isolation", "debug24-real-player-replay",
                       "remote-target32-ui", "shop-target32-ui",
                       "shop-controller-refresh-ui", "shop-free-first-refresh-service-ui"}
REVIEW_STATUS = "partial_passed_native_display_pending"
REVIEW_SCOPE = "full-gameplay-ui-perf_native_display_pending"
COMMAND_KINDS = ("NewRun", "ChooseChallenge", "StartStage", "Roll", "ConfirmLocks", "Reorder", "UseItem", "Submit", "Continue",
                 "Buy", "Sell", "Study", "LeaveShop", "EndStage", "ReorderControllers", "RefreshControllers", "ApplyDebugScene")


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
    sequence = "actionSequence"
    for side, rows in (("windows", left), ("macOS", right)):
        for index, row in enumerate(rows):
            missing = [key for key in fields if key not in row]
            if missing:
                failures.append({"stream": kind, "side": side, "row": index, "reason": "missing_fields", "fields": missing})
            if not isinstance(row.get("ruleStateSha256"), str) or not HEX64.fullmatch(row["ruleStateSha256"]):
                failures.append({"stream": kind, "side": side, "row": index, "reason": "invalid_rule_hash"})
            # Both v0.6 traces start from a workspace whose sequence is zero.
            first_sequence = 1
            if type(row.get(sequence)) is not int or row[sequence] != index + first_sequence:
                failures.append({"stream": kind, "side": side, "row": index, "reason": "non_contiguous_sequence"})
            if row.get("index") != index:
                failures.append({"stream": kind, "side": side, "row": index, "reason": "invalid_index"})
            if row.get("savedStateMatches") is not True or row.get("renderedStateMatches") is not True:
                failures.append({"stream": kind, "side": side, "row": index, "reason": "disk_or_ui_not_verified"})
            for field in (("submissionScore", "stageScore", "target") if kind == "normal" else ("score",)):
                if not isinstance(row.get(field), str) or not re.fullmatch(r"0|[1-9][0-9]*", row[field]):
                    failures.append({"stream": kind, "side": side, "row": index, "reason": "noncanonical_big_integer", "field": field})
            if type(row.get("gold")) is not int or not 0 <= row["gold"] <= 9223372036854775807:
                failures.append({"stream": kind, "side": side, "row": index, "reason": "invalid_int64_wallet"})
            if kind == "normal":
                if row.get("isDebug") is not False:
                    failures.append({"stream": kind, "side": side, "row": index, "reason": "normal_trace_is_debug"})
    for index, (a, b) in enumerate(zip(left, right)):
        for key in fields:
            if key in a and key in b and (type(a[key]) is not type(b[key]) or a[key] != b[key]):
                failures.append({"stream": kind, "row": index, "field": key, "windows": a[key], "macOS": b[key]})
    return {"stream": kind, "windowsRecords": len(left), "macOSRecords": len(right),
            "comparedFields": list(fields), "passed": not failures, "failures": failures}


def validate_report(report, side):
    failures = []
    platform = "WindowsPlayer" if side == "windows" else "OSXPlayer"
    if report.get("schema") != "p0-player-verification-6" or not report.get("probeRevision", "").startswith("p06-"):
        failures.append({"side": side, "reason": "wrong_probe_version"})
    if report.get("configVersion") != "p0.6.0":
        failures.append({"side": side, "reason": "wrong_config_version"})
    for field in ("traceSha256", "debugTraceSha256", "configSha256"):
        if not isinstance(report.get(field), str) or not HEX64.fullmatch(report[field]):
            failures.append({"side": side, "reason": "invalid_hash", "field": field})
    try:
        identity = json.loads(report.get("buildIdentity") or "{}")
    except (ValueError, TypeError):
        identity = {}
    if (identity.get("releaseVersion") != "p0.6.0" or identity.get("contentConfigVersion") != "p0.6.0"
            or type(identity.get("saveSchemaVersion")) is not int or identity["saveSchemaVersion"] != 4
            or identity.get("unityEditorVersion") != "6000.6.7f1" or identity.get("backend") != "Mono"
            or not re.fullmatch("[0-9a-f]{40}", identity.get("sourceCommit") or "")
            or not HEX64.fullmatch(identity.get("goldenCaseSetSha256") or "")
            or identity.get("contentConfigSha256") != report.get("configSha256")):
        failures.append({"side": side, "reason": "invalid_embedded_identity"})
    if report.get("status") != "passed" or type(report.get("exitCode")) is not int or report["exitCode"] != 0:
        failures.append({"side": side, "reason": "player_did_not_pass"})
    environment = report.get("environment") or {}
    if environment.get("platform") != platform or environment.get("isEditor") is not False:
        failures.append({"side": side, "reason": "wrong_platform_or_editor"})
    if environment.get("processArchitecture") != ("X64" if side == "windows" else "Arm64"):
        failures.append({"side": side, "reason": "wrong_process_architecture"})
    if side == "macOS" and environment.get("graphicsApi") != "Metal":
        failures.append({"side": side, "reason": "metal_not_observed"})
    normal, debug = report.get("normalReplay") or {}, report.get("debug24") or {}
    if normal.get("passed") is not True or normal.get("normalIsDebug") is not False or debug.get("passed") is not True or debug.get("isDebug") is not True:
        failures.append({"side": side, "reason": "replay_not_passed_or_isolation_missing"})
    if normal.get("terminalPhase") not in ("RunWon", "RunFailed"):
        failures.append({"side": side, "reason": "normal_run_not_terminal"})
    if type(normal.get("expectedSteps")) is not int or normal["expectedSteps"] <= 0 or normal.get("completedSteps") != normal["expectedSteps"]:
        failures.append({"side": side, "reason": "normal_replay_incomplete"})
    if report.get("stressCompleted") is not True or report.get("handbookPureReadChecks", 0) < 20:
        failures.append({"side": side, "reason": "required_probe_section_missing"})
    performance = report.get("performance") or {}
    p95 = (performance.get("frameInterval") or {}).get("p95")
    if (performance.get("status") != "within_frame_budget" or performance.get("sampleSeconds", 0) < 180
            or type(p95) not in (int, float) or not 0 < p95 <= 16.7
            or type(performance.get("frameCount")) is not int or performance["frameCount"] <= 0
            or performance.get("targetResolutionMet") is not True or performance.get("frameBudgetMet") is not True
            or any(performance.get(key) != value for key, value in (("dice", 10), ("submitSlots", 8), ("controllers", 10), ("inventory", 5)))):
        failures.append({"side": side, "reason": "pressure_gate_not_met"})
    if report.get("unityErrors") != []:
        failures.append({"side": side, "reason": "unity_errors_or_missing_field"})
    if report.get("checkpointExported") is not True:
        failures.append({"side": side, "reason": "cross_platform_checkpoint_not_exported"})
    checks = report.get("v06Checks") or []
    ids = [row.get("id") for row in checks]
    if (len(ids) != len(set(ids)) or not REQUIRED_V06_CHECKS.issubset(ids)
            or any(row.get("status") != "passed" for row in checks)):
        failures.append({"side": side, "reason": "v06_ui_sections_missing_or_failed"})
    displays = report.get("displayChecks") or []
    by_id = {row.get("id"): row for row in displays}
    required = {"mode-Windowed", "mode-FullScreenWindow", "15-second-timeout-revert", "escape-revert",
                "mode-ExclusiveFullScreen" if side == "windows" else "mode-MaximizedWindow"}
    if (len(by_id) != len(displays) or not required.issubset(by_id)
            or any(by_id[key].get("status") != "passed" for key in required if key in by_id)
            or any(row.get("status") not in ("passed", "not_supported", "not_executed")
                   and not (side == "macOS" and row.get("id") == "exclusive-non-native-escape" and row.get("status") == "not_applicable") for row in displays)):
        failures.append({"side": side, "reason": "native_display_sections_missing_or_failed"})
    if side == "macOS" and report.get("importedCheckpointStatus") != "passed":
        failures.append({"side": side, "reason": "windows_checkpoint_not_loaded_and_replayed"})
    return failures


def validate_windows_review_reference(report):
    """Explicit restricted reference only. Never mutates evidence or relaxes validate_report."""
    failures = [row for row in validate_report(report, "windows") if row["reason"] not in
                {"player_did_not_pass", "v06_ui_sections_missing_or_failed", "native_display_sections_missing_or_failed"}]
    if (report.get("status") != REVIEW_STATUS or report.get("verificationScope") != REVIEW_SCOPE
            or report.get("nativeDisplayDeferred") is not True or type(report.get("exitCode")) is not int
            or report["exitCode"] != 0):
        failures.append({"side": "windows", "reason": "not_explicit_completed_review_reference"})
    checks = report.get("v06Checks") or []
    ids = [row.get("id") for row in checks]
    required = REQUIRED_V06_CHECKS - {"display-state-isolation"}
    if (len(ids) != len(set(ids)) or not required.issubset(ids)
            or any(row.get("status") != "passed" and not
                   (row.get("id") == "display-state-isolation" and row.get("status") == "not_executed") for row in checks)):
        failures.append({"side": "windows", "reason": "review_non_display_ui_missing_or_failed"})
    displays = report.get("displayChecks") or []
    by_id = {row.get("id"): row for row in displays}
    known = {"native-display-verification-deferred", "mode-Windowed", "mode-FullScreenWindow", "mode-ExclusiveFullScreen",
             "15-second-timeout-revert", "escape-revert", "exclusive-non-native-escape", "crash-pending-recovery"}
    if (len(by_id) != len(displays) or by_id.get("native-display-verification-deferred", {}).get("status") != "not_executed"
            or any(row.get("id") not in known or row.get("status") not in ("passed", "not_executed", "not_supported") for row in displays)):
        failures.append({"side": "windows", "reason": "review_native_defer_marker_missing_or_failed_display"})
    return failures


def validate_windows_failed_performance_reference(report):
    """Diagnostic gameplay reference with a completed, FAILED performance run.

    This is deliberately separate from both strict validators above. It cannot
    accept an incomplete run, a differently failed probe, or pretend a pass.
    """
    import math
    failures = [row for row in validate_windows_review_reference(report) if row["reason"] not in
                {"not_explicit_completed_review_reference", "pressure_gate_not_met"}]
    diagnostic_ui = (REQUIRED_V06_CHECKS - {"display-state-isolation"}) | {
        "handbook-independent-color-tab", "wheel-reach-AddC53", "wheel-reach-RemoteDieD031",
        "wheel-reach-Target_D032", "wheel-reach-OrderCardITEM_00014", "long-mimic-chain-destroyed-owner-ui"}
    if not diagnostic_ui.issubset({row.get("id") for row in report.get("v06Checks") or []}):
        failures.append({"side": "windows", "reason": "diagnostic_gameplay_ui_coverage_incomplete"})
    if (report.get("status") != "budget_exceeded" or report.get("verificationScope") != REVIEW_SCOPE
            or report.get("nativeDisplayDeferred") is not True or type(report.get("exitCode")) is not int
            or report["exitCode"] != 3 or report.get("failure") is not None):
        failures.append({"side": "windows", "reason": "not_explicit_completed_failed_performance_reference"})
    perf = report.get("performance") or {}
    interval = perf.get("frameInterval") or {}
    frames = perf.get("frameCount")
    def finite(value):
        return type(value) in (int, float) and math.isfinite(value)
    if (perf.get("status") != "budget_exceeded" or not finite(perf.get("sampleSeconds")) or perf["sampleSeconds"] < 180
            or type(frames) is not int or frames <= 0 or not finite(interval.get("p95")) or interval["p95"] <= 16.7
            or perf.get("frameBudgetMet") is not False or perf.get("targetFrameMs") != 16.7
            or perf.get("targetResolution") != "1920x1080" or perf.get("targetResolutionMet") is not True
            or any(perf.get(key) != value for key, value in (("dice", 10), ("submitSlots", 8), ("controllers", 10), ("inventory", 5)))
            or interval.get("available") is not True or interval.get("samples") != frames
            or interval.get("expectedFrames") != frames or interval.get("validFrameCoverage") != 1):
        failures.append({"side": "windows", "reason": "diagnostic_performance_run_incomplete_or_not_failed_budget"})
    for key in ("focusedSampleFrames", "gpuInvalidSamples", "gpuMissingSamples"):
        value = perf.get(key)
        if type(value) is not int or value < 0 or type(frames) is not int or value > frames:
            failures.append({"side": "windows", "reason": "missing_or_invalid_performance_observation", "field": key})
    return failures


def validate_windows_reference(report, review_candidate=False, allow_failed_windows_performance=False):
    if allow_failed_windows_performance:
        if not review_candidate:
            raise ValueError("--allow-failed-windows-performance requires --review-candidate")
        return validate_windows_failed_performance_reference(report)
    return validate_windows_review_reference(report) if review_candidate else validate_report(report, "windows")


def failed_performance_notice(report):
    perf = report["performance"]
    return {"acceptanceStatus": "FAILED", "status": perf["status"], "observedStatus": perf["status"], "sampleSeconds": perf["sampleSeconds"],
            "p95FrameIntervalMs": perf["frameInterval"]["p95"], "thresholdMs": 16.7, "frames": perf["frameCount"],
            "focusedSampleFrames": perf["focusedSampleFrames"], "gpuInvalidSamples": perf["gpuInvalidSamples"],
            "gpuMissingSamples": perf["gpuMissingSamples"],
            "note": "Completed observation, not a performance pass or pending result. Focus and GPU anomalies remain uncorrected evidence."}


def review_reference_notice(report, allow_failed_windows_performance=False):
    if allow_failed_windows_performance:
        return {"status": report.get("status"), "verificationScope": report.get("verificationScope"),
                "use": "Gameplay/UI/trace reference ONLY; Windows performance FAILED and native P22 NOT PASSED",
                "allowFailedWindowsPerformance": True, "windowsPerformance": failed_performance_notice(report),
                "windowsNativeDisplayStatus": "PENDING_P22_NOT_PASSED", "macRuntimeStatus": "NOT_RUN_ON_MAC",
                "fullWindowsPassed": False, "fullReleaseAccepted": False,
                "publication": "User-authorized diagnostic test candidate; no performance, native-display or whole-release acceptance"}
    return {"status": report.get("status"), "verificationScope": report.get("verificationScope"),
            "use": "Gameplay/UI/trace/performance reference only", "windowsNativeDisplayStatus": "PENDING_P22_NOT_PASSED",
            "fullWindowsPassed": False, "fullReleaseAccepted": False,
            "publication": "User-authorized test candidate channel; technical scope remains restricted and the full release is not accepted"}


def validate_trace_replay(report, directory, normal_path, debug_path):
    """Derive required command coverage from the actual inputs, never report self-declaration."""
    directory = Path(directory)
    normal, debug = load_json(normal_path), load_json(debug_path)
    if (normal.get("configVersion") != "p0.6.0" or normal.get("isDebug") is not False
            or normal.get("expectedFinalPhase") not in ("RunWon", "RunFailed") or not normal.get("steps")
            or debug.get("configVersion") != "p0.6.0" or debug.get("debug") is not True or not debug.get("steps")
            or debug.get("expectedFinalPhase") != "RunWon" or (debug.get("stages"), debug.get("shops"), debug.get("risks")) != (24, 23, 8)
            or report.get("traceSha256") != sha256(normal_path) or report.get("debugTraceSha256") != sha256(debug_path)):
        raise ValueError("Player trace hash/header differs from the required v0.6 inputs")
    normal_rows = load_rows(directory / "normal-replay.jsonl")
    debug_rows = load_rows(directory / "debug24-replay.jsonl")
    for rows, trace, fields, kind in ((normal_rows, normal, NORMAL_FIELDS, "normal"), (debug_rows, debug, DEBUG_FIELDS, "debug24")):
        if len(rows) != len(trace["steps"]) or not compare_stream(rows, rows, fields, kind)["passed"]:
            raise ValueError("Player did not execute every required " + kind + " trace command")
        for index, (row, step) in enumerate(zip(rows, trace["steps"])):
            if kind == "normal":
                keys = ("kind", "actionSequence", "phase", "submissionScore", "stageScore", "target", "gold", "stageIndex", "rollsRemaining", "remainingSubmissions")
                expected = {key: step[key] for key in keys}
            else:
                command_kind = step["command"]["kind"]
                if type(command_kind) is not int or not 0 <= command_kind < len(COMMAND_KINDS):
                    raise ValueError("Debug trace command kind is invalid")
                expected = {key: step[key] for key in ("actionSequence", "phase", "gold", "stageIndex")}
                expected.update(kind=COMMAND_KINDS[command_kind], score=step["stageScore"], ruleStateSha256=step["stateHash"])
            if any(type(row.get(key)) is not type(value) or row[key] != value for key, value in expected.items()):
                raise ValueError("Player " + kind + " row differs from input trace at " + str(index))
    summary = report.get("normalReplay") or {}
    if (summary.get("expectedSteps") != len(normal_rows) or summary.get("completedSteps") != len(normal_rows)
            or summary.get("seed") != normal["seed"] or summary.get("terminalPhase") != normal["expectedFinalPhase"]
            or summary.get("terminalStage") != normal["expectedFinalStage"] or normal_rows[-1]["phase"] != normal["expectedFinalPhase"]
            or normal_rows[-1]["stageIndex"] + 1 != normal["expectedFinalStage"]):
        raise ValueError("Normal report outcome/count differs from actual input trace")
    if (report.get("debug24", {}).get("steps") != debug_rows or debug_rows[-1]["phase"] != "RunWon" or debug_rows[-1]["stageIndex"] != 23):
        raise ValueError("Debug report rows or final state differ from actual input trace")
    return {"normalSteps": len(normal_rows), "debugSteps": len(debug_rows), "normalTraceSha256": sha256(normal_path), "debugTraceSha256": sha256(debug_path)}


def validate_recovery(directory, identity):
    """Validate separate real-process evidence without rewriting the main report."""
    directory = Path(directory).resolve()
    ready = load_json(directory / "ready-to-terminate.json")
    receipt = load_json(directory / "recovery-read-receipt.json")
    report = load_json(Path(receipt["evidenceDirectory"]) / "player-report.json")
    writer, reader = receipt.get("writerProcessId"), receipt.get("readerProcessId")
    if (receipt.get("schema") != "p06-native-display-recovery-1" or receipt.get("status") != "passed"
            or ready.get("status") != "awaiting_external_process_termination"
            or type(writer) is not int or writer <= 0 or type(reader) is not int or reader <= 0 or reader == writer
            or ready.get("writerProcessId") != writer or receipt.get("pendingSha256") != ready.get("pendingSha256")
            or not HEX64.fullmatch(receipt.get("pendingSha256") or "")
            or json.loads(receipt.get("buildIdentity") or "{}") != identity
            or json.loads(ready.get("buildIdentity") or "{}") != identity
            or report.get("status") != "recovery_passed" or report.get("exitCode") != 0
            or report.get("schema") != "p0-player-verification-6" or report.get("unityErrors") != []
            or json.loads(report.get("buildIdentity") or "{}") != identity):
        raise ValueError("Missing/stale/non-passing independent display recovery evidence")
    for field in ("width", "height", "mode"):
        expected = ready.get("confirmed", {}).get(field)
        if type(expected) is not int or expected < (0 if field == "mode" else 1) or receipt.get("actual", {}).get(field) != expected:
            raise ValueError("Fresh process display differs from prior confirmed value")
    if any(receipt.get(key) != ready.get(key) for key in ("confirmed", "candidate", "settingsPath")):
        raise ValueError("Reader recovery receipt changed writer inputs")
    settings = directory / "display-settings.json"
    if Path(receipt["settingsPath"]).resolve() != settings:
        raise ValueError("Recovery settings are outside the named isolated directory")
    if load_json(settings).get("pending") is not False:
        raise ValueError("Recovery preferences remain pending")
    environment = report.get("environment") or {}
    if environment.get("isEditor") is not False or environment.get("platform") not in ("WindowsPlayer", "OSXPlayer"):
        raise ValueError("Recovery was not run in an actual desktop Player")
    paths = [directory / "ready-to-terminate.json", directory / "recovery-read-receipt.json", settings,
             Path(receipt["evidenceDirectory"]) / "player-report.json"]
    return {"status": "passed", "writerProcessId": writer, "readerProcessId": reader,
            "actual": receipt["actual"], "environment": environment,
            "files": [{"path": str(path), "sha256": sha256(path)} for path in paths],
            "note": "Separate process result; the main probe's not_executed field is preserved."}


def compare_directories(windows_dir, mac_dir, review_candidate=False, allow_failed_windows_performance=False):
    windows_dir, mac_dir = Path(windows_dir), Path(mac_dir)
    windows = load_json(windows_dir / "player-report.json")
    mac = load_json(mac_dir / "player-report.json")
    failures = []
    if windows.get("configVersion") != "p0.6.0" or mac.get("configVersion") != "p0.6.0":
        failures.append({"field": "configVersion", "reason": "wrong_release_version"})
    for key in ("schema", "probeRevision", "traceSha256", "debugTraceSha256", "configSha256", "configVersion"):
        if not windows.get(key) or windows.get(key) != mac.get(key):
            failures.append({"field": key, "windows": windows.get(key), "macOS": mac.get(key)})
    identities = []
    for report in (windows, mac):
        raw = report.get("buildIdentity")
        identities.append(json.loads(raw) if isinstance(raw, str) and raw else {})
    for key in ("releaseVersion", "sourceCommit", "contentConfigVersion", "contentConfigSha256", "goldenCaseSetSha256", "saveSchemaVersion", "unityEditorVersion", "backend"):
        if not identities[0].get(key) or identities[0].get(key) != identities[1].get(key):
            failures.append({"field": "buildIdentity." + key, "windows": identities[0].get(key), "macOS": identities[1].get(key)})
    for side, report in (("windows", windows), ("macOS", mac)):
        failures.extend(validate_windows_reference(report, review_candidate, allow_failed_windows_performance) if side == "windows" else validate_report(report, side))
    normal = compare_stream(load_rows(windows_dir / "normal-replay.jsonl"), load_rows(mac_dir / "normal-replay.jsonl"), NORMAL_FIELDS, "normal")
    debug = compare_stream(load_rows(windows_dir / "debug24-replay.jsonl"), load_rows(mac_dir / "debug24-replay.jsonl"), DEBUG_FIELDS, "debug24")
    for side, report, directory in (("windows", windows, windows_dir), ("macOS", mac, mac_dir)):
        rows = load_rows(directory / "normal-replay.jsonl")
        expected = report.get("normalReplay", {}).get("expectedSteps")
        completed = report.get("normalReplay", {}).get("completedSteps")
        if type(expected) is not int or expected != len(rows) or completed != expected:
            failures.append({"side": side, "reason": "normal_report_count_mismatch", "expected": expected, "completed": completed, "records": len(rows)})
        steps = load_rows(directory / "debug24-replay.jsonl")
        if steps[-1].get("stageIndex") != 23 or steps[-1].get("phase") != "RunWon":
            failures.append({"side": side, "reason": "debug24_final_state_not_won_24"})
        if report.get("debug24", {}).get("steps") != steps:
            failures.append({"side": side, "reason": "debug24_report_rows_differ"})
    inputs = []
    for side, directory in (("windows", windows_dir), ("macOS", mac_dir)):
        for name in ("player-report.json", "normal-replay.jsonl", "debug24-replay.jsonl"):
            path = directory / name
            inputs.append({"side": side, "file": name, "sha256": sha256(path), "byteSize": path.stat().st_size})
    return {"schema": "p06-cross-platform-comparison-1", "passed": not failures and normal["passed"] and debug["passed"],
            "reviewCandidate": review_candidate, "windowsReference": review_reference_notice(windows, allow_failed_windows_performance) if review_candidate else {"status": windows.get("status")},
            "fullReleaseAccepted": False,
            "scope": "Actual replay commands, canonical gameplay hashes and Windows checkpoint loaded on Mac; does not replace reverse checkpoint, manual input, OS restart, or Intel testing.",
            "reportFailures": failures, "normal": normal, "debug24": debug, "inputs": inputs}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--windows-results", required=True, type=Path)
    parser.add_argument("--mac-results", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--review-candidate", action="store_true", help="Use explicit partial Windows reference; Mac remains strictly validated")
    parser.add_argument("--allow-failed-windows-performance", action="store_true", help="Diagnostic review reference with explicitly FAILED Windows performance; never relaxes Mac")
    args = parser.parse_args()
    try:
        result = compare_directories(args.windows_results, args.mac_results, args.review_candidate, args.allow_failed_windows_performance)
    except (OSError, ValueError, KeyError, TypeError, AttributeError) as error:
        result = {"schema": "p06-cross-platform-comparison-1", "passed": False, "error": str(error)}
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print("Cross-platform comparison: " + ("PASS" if result["passed"] else "FAIL"))
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    sys.exit(main())
