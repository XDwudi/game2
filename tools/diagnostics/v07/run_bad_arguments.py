"""Four opt-in real-Player argument rejection cases. Plan-only by default; no acceptance claim."""
import argparse
import json
from pathlib import Path
import sys

import run_resource_diagnostic_v3 as R

RUNNER_SHA = "859333e3d91837d136d17d091cddb3449f53b6b9ec7a5dd07d429daa324f07f0"
CASE_TIMEOUT = 45
CASES = (
    ("missing-path", "requires one fresh absolute output directory."),
    ("nonempty-path", "Evidence directory must be fresh or empty."),
    ("mixed-verify", "Resource diagnostic cannot run with verification or pacing modes."),
    ("mixed-pacing", "Resource diagnostic cannot run with verification or pacing modes."),
)


def require_runner():
    if R.sha(Path(R.__file__)) != RUNNER_SHA:
        raise ValueError("The independently reviewed resource wrapper v3 changed; explicitly re-review/rebind")


def command(bound, name, case, trace, debug):
    flags = [R.FLAG] if name == "missing-path" else [R.FLAG, str(case / "resource")]
    if name == "mixed-verify":
        flags += ["--verify-p0", str(trace), "--verify-debug-trace", str(debug), "--evidence-dir", str(case / "legacy-verify")]
    elif name == "mixed-pacing":
        flags += ["--pacing-experiment", str(case / "legacy-pacing")]
    return [str(bound["exe"]), *flags, "-screen-width", "1920", "-screen-height", "1080", "-screen-fullscreen", "0",
            "-logFile", str(case / "Player.log")]


def external_guards(bound):
    return {"build": R.tree_guard(bound["exe"].parent),
            "ordinarySaves": R.tree_guard(Path.home() / "AppData/LocalLow/XDwudi/DiceCraft P0"),
            "preferences": R.read_preferences()}


def rejection_observations(case, process, expected, resource_before):
    failures = []
    if process.get("exitCode") != 1 or any(process.get(key) for key in (
            "timedOut", "ownedProcessTerminated", "ownedProcessStillRunning", "orchestrationError", "terminationError")):
        failures.append("expected_normal_process_exit_1_not_observed")
    log_path = case / "Player.log"
    logs = log_path.read_text(encoding="utf-8-sig", errors="replace") if log_path.is_file() else ""
    if not logs or expected not in logs or "Art07ResourceDiagnostic" not in logs:
        failures.append("specific_resource_argument_rejection_not_observed")
    old_markers = ("P0 verification FAILED", "P0 verification complete", "P0 restricted verification complete",
                   "DiceCraft.Verification.PlayerVerification", "DiceCraft.FramePacingExperiment", "OptInFramePacingExperiment")
    found_old = [marker for marker in old_markers if marker in logs]
    if found_old:
        failures.append("legacy_probe_log_observed")
    crash_markers = ("Fatal error", "Crash!!!", "Unhandled exception", "Access violation", "NullReferenceException",
                     "DllNotFoundException", "FileNotFoundException", "MissingMethodException", "OutOfMemoryException")
    found_crash = [marker for marker in crash_markers if marker in logs]
    if found_crash:
        failures.append("unrelated_crash_or_exception_observed")
    directories = {name: (case / name).exists() for name in ("legacy-verify", "legacy-pacing")}
    if any(directories.values()):
        failures.append("legacy_bootstrap_output_created")
    resource_unchanged = R.tree_guard(case / "resource") == resource_before
    if not resource_unchanged:
        failures.append("rejected_resource_output_was_created_or_changed")
    unexpected = [p.relative_to(case).as_posix() for p in case.rglob("*") if p.is_file()
                  and (p.name.endswith("report.json") or p.suffix.lower() == ".csv")]
    if unexpected:
        failures.append("unexpected_probe_report_or_sampling_output")
    return {"failures": failures, "expectedMessageObserved": expected in logs, "resourceErrorOriginObserved": "Art07ResourceDiagnostic" in logs,
            "logSha256": R.sha(log_path) if log_path.is_file() else None, "legacyDirectoriesCreated": directories,
            "legacyLogMarkers": found_old, "unrelatedCrashMarkers": found_crash, "resourceInputUnchanged": resource_unchanged,
            "unexpectedReportOrCsvFiles": unexpected,
            "legacyScope": "No legacy bootstrap output/log observed; frozen code creates its output directory before its host. No live GameObject enumeration claimed."}


def run_case(bound, name, expected, trace, debug, guard=external_guards, launch=R.process_run):
    require_runner()
    before = guard(bound)
    R.validate_launch_binding(bound, before["build"])
    case = bound["directory"] / name
    case.mkdir(exist_ok=False)
    if name == "nonempty-path":
        (case / "resource").mkdir()
        (case / "resource/sentinel.txt").write_text("Nonempty rejection fixture; must remain byte-identical.\n", encoding="utf-8")
    resource_before = R.tree_guard(case / "resource")
    result = {"case": name, "expectedRejection": expected, "startedUtc": R.utc(), "passed": False,
              "wholeReleaseAccepted": False, "errors": [], "argv": command(bound, name, case, trace, debug)}
    R.write_new(case / "guards-before.json", before)
    try:
        R.validate_launch_binding(bound, before["build"])
        result["process"] = launch(result["argv"], case, CASE_TIMEOUT)
        result["observations"] = rejection_observations(case, result["process"], expected, resource_before)
        result["errors"].extend(result["observations"]["failures"])
    except BaseException as error:
        result["errors"].append(type(error).__name__ + ": " + str(error))
    finally:
        try:
            after = guard(bound)
            R.write_new(case / "guards-after.json", after)
            result["buildUnchanged"] = before["build"] == after["build"]
            result["ordinarySavesUnchanged"] = before["ordinarySaves"] == after["ordinarySaves"]
            result["gamePrefsUnchanged"] = before["preferences"]["game"] == after["preferences"]["game"]
            result["engineScreenmanagerUnchanged"] = before["preferences"]["engineScreenmanager"] == after["preferences"]["engineScreenmanager"]
            for key in ("buildUnchanged", "ordinarySavesUnchanged", "gamePrefsUnchanged"):
                if not result[key]:
                    result["errors"].append(key + "_failed")
        except BaseException as error:
            result["errors"].append("post_guard_unavailable:" + type(error).__name__)
        result["endedUtc"] = R.utc()
        result["passed"] = not result["errors"]
        R.write_new(case / "case-result.json", result)
    return result


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-report", type=Path, required=True)
    parser.add_argument("--directory", type=Path, required=True)
    parser.add_argument("--execute", action="store_true")
    args = parser.parse_args(argv)
    require_runner()
    bound = R.preflight(R.ROOT, args.build_report, args.directory, 270)
    trace, debug = (R.ROOT / ("evidence/v07/session/" + name) for name in ("normal-run-trace.json", "debug-24-trace.json"))
    trace_hashes = {p.name: R.sha(p) for p in (trace, debug)}
    planned = [{"case": name, "expectedRejection": expected, "argv": command(bound, name, bound["directory"] / name, trace, debug)} for name, expected in CASES]
    if not args.execute:
        print(json.dumps({"status": "plan_only", "cases": planned, "caseTimeoutSeconds": CASE_TIMEOUT,
                          "wholeReleaseAccepted": False, "writesOrProcessesStarted": False}, ensure_ascii=False, indent=2))
        return 0
    if sys.platform != "win32":
        raise ValueError("Actual Windows required")
    initial = external_guards(bound)
    R.validate_launch_binding(bound, initial["build"])
    bound["directory"].mkdir(parents=True, exist_ok=False)
    summary = {"schema": "v07-resource-bad-arguments-1", "startedUtc": R.utc(), "boundIdentity": bound["identity"],
               "boundBuildGuid": bound["buildGuid"], "traceHashes": trace_hashes, "runnerV3Sha256": RUNNER_SHA,
               "scope": "Only explicit argument rejection and legacy bootstrap exclusion; no resource/performance/display/whole-release acceptance",
               "wholeReleaseAccepted": False, "cases": [], "errors": []}
    R.write_new(bound["directory"] / "suite-guards-before.json", initial)
    try:
        for name, expected in CASES:
            result = run_case(bound, name, expected, trace, debug)
            summary["cases"].append(result)
            if not result["passed"]:
                break
    except BaseException as error:
        summary["errors"].append(type(error).__name__ + ": " + str(error))
    finally:
        try:
            final = external_guards(bound)
            R.write_new(bound["directory"] / "suite-guards-after.json", final)
            for key in ("build", "ordinarySaves"):
                if initial[key] != final[key]:
                    summary["errors"].append("suite_" + key + "_changed")
            if initial["preferences"]["game"] != final["preferences"]["game"]:
                summary["errors"].append("suite_game_preferences_changed")
            summary["engineScreenmanagerUnchanged"] = initial["preferences"]["engineScreenmanager"] == final["preferences"]["engineScreenmanager"]
            if trace_hashes != {p.name: R.sha(p) for p in (trace, debug)}:
                summary["errors"].append("trace_inputs_changed")
        except BaseException as error:
            summary["errors"].append("suite_post_guard_unavailable:" + type(error).__name__)
        summary["remainingCasesNotRun"] = [name for name, _ in CASES[len(summary["cases"]):]]
        summary["passed"] = len(summary["cases"]) == 4 and all(row["passed"] for row in summary["cases"]) and not summary["errors"]
        summary["endedUtc"] = R.utc()
        R.write_new(bound["directory"] / "bad-arguments-summary.json", summary)
    print(json.dumps({"passed": summary["passed"], "wholeReleaseAccepted": False, "summary": str(bound["directory"] / "bad-arguments-summary.json")}))
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, R.subprocess.CalledProcessError) as error:
        print("Preflight refused: " + type(error).__name__ + ": " + str(error), file=sys.stderr)
        raise SystemExit(2)
