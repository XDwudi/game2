"""Run the real Windows v0.6 writer/reader display-recovery protocol.

Only processes created by this runner are terminated. Raw Unity/stdout logs stay
under artifacts/cache/v06. No preferences or receipts are manufactured or edited.
"""
import argparse
from datetime import datetime, timezone
import json
from pathlib import Path
import subprocess
import sys
import time
import traceback
import uuid

from package_v06 import ROOT, read_json, sha, validate_identity, validate_build, validate_windows_report
from macos.v06.compare_replays import validate_recovery


def utc():
    return datetime.now(timezone.utc).isoformat()


def write_json(path, value):
    with Path(path).open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def new_path(path, allowed_root):
    resolved = Path(path).expanduser().resolve()
    allowed = Path(allowed_root).resolve()
    if resolved == allowed or not resolved.is_relative_to(allowed):
        raise ValueError("Output must be a new child of the designated evidence/cache directory")
    if resolved.exists():
        raise FileExistsError("Existing output is never overwritten or reused")
    return resolved


def prepare(args):
    if sys.platform != "win32":
        raise ValueError("This runner requires an actual Windows host")
    for name in ("exe", "trace", "debug_trace", "identity", "build_report", "main_results"):
        setattr(args, name, Path(getattr(args, name)).expanduser().resolve(strict=True))
    if not args.exe.is_file() or args.exe.suffix.lower() != ".exe":
        raise ValueError("Expected a built Windows Player executable")
    identity = validate_identity(read_json(args.identity))
    build = validate_build(args.build_report, "windows", identity)
    main_report = validate_windows_report(args.main_results, identity, getattr(args, "review_candidate", False),
                                          getattr(args, "allow_failed_windows_performance", False))
    if main_report["environment"].get("buildGuid") != build["buildGuid"]:
        raise ValueError("The successful main Player did not run this build GUID")
    if main_report["traceSha256"] != sha(args.trace) or main_report["debugTraceSha256"] != sha(args.debug_trace):
        raise ValueError("Explicit recovery traces differ from the successful main Player inputs")
    if args.timeout < 30 or args.timeout > 600:
        raise ValueError("Per-process startup/finish timeout must be30–600seconds; confirmation timeout is unchanged")
    shared = new_path(args.evidence_dir, ROOT / "evidence/v06")
    cache_root = ROOT / "artifacts/cache/v06"
    raw = new_path(args.raw_dir or cache_root / ("display-recovery-" + datetime.now(timezone.utc).strftime("%Y%m%d-%H%M%S-") + uuid.uuid4().hex[:8]), cache_root)
    return identity, build, main_report, shared, raw


def command_for(args, role, shared, output, raw):
    if role not in ("write", "read"):
        raise ValueError("Unknown recovery process role")
    return [str(args.exe), "--verify-p0", str(args.trace), "--verify-debug-trace", str(args.debug_trace),
            "--evidence-dir", str(output), "--verify-display-recovery-" + role, str(shared),
            "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0",
            "-logFile", str(raw / (role + "-Player.log"))]


def visible_player_startup():
    """Do not inherit a hidden helper's first-window setting into the game."""
    if sys.platform != "win32":
        return {}  # Importable for mocked offline tests; prepare rejects other hosts.
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = 1  # Win32 SW_SHOWNORMAL; no extra helper/console window.
    return {"startupinfo": startup}


def stop_owned(process):
    """Use Popen's original process handle; never enumerate or kill by name/PID search."""
    if process.poll() is None:
        process.kill()
        process.wait(timeout=10)
        return True
    return False


def wait_for_ready(process, shared, timeout):
    started = time.monotonic()
    ready_path = shared / "ready-to-terminate.json"
    while process.poll() is None and time.monotonic() - started < timeout:
        if ready_path.is_file():
            try:
                ready = read_json(ready_path)
            except (OSError, ValueError):
                ready = None  # A short File.WriteAllText may still be completing.
            if ready is not None:
                if (ready.get("schema") != "p06-native-display-recovery-1"
                        or ready.get("status") != "awaiting_external_process_termination"
                        or ready.get("writerProcessId") != process.pid):
                    raise ValueError("Ready receipt does not identify the live writer launched by this runner")
                if Path(ready.get("settingsPath") or "").resolve() != shared / "display-settings.json":
                    raise ValueError("Writer pointed outside the isolated settings path")
                return ready
        time.sleep(.025)
    raise RuntimeError("Writer exited or timed out before producing its durable ready receipt")


def run_writer(args, shared, raw, identity):
    output = shared / "writer"
    output.mkdir()
    result = {"role": "writer", "startedUtc": utc(), "terminatedOnlyOwnedHandle": False}
    with (raw / "write-stdout.log").open("xb") as log:
        process = subprocess.Popen(command_for(args, "write", shared, output, raw), cwd=args.exe.parent,
                                   stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT, shell=False,
                                   **visible_player_startup())
        result["pid"] = process.pid
        try:
            ready = wait_for_ready(process, shared, args.timeout)
            observed = time.monotonic()
            result["readyObservedUtc"] = utc()
            # Kill immediately after the complete receipt. Inspect identity/hash after
            # termination so slow disk validation cannot consume the15-second window.
            if not stop_owned(process):
                raise RuntimeError("Writer exited before the external termination")
            result["terminatedOnlyOwnedHandle"] = True
            result["readyToTerminationMilliseconds"] = (time.monotonic() - observed) * 1000
            result["exitCode"] = process.returncode
            if result["readyToTerminationMilliseconds"] >= 15000:
                raise RuntimeError("External termination exceeded the real confirmation window")
            settings = shared / "display-settings.json"
            if (json.loads(ready.get("buildIdentity") or "{}") != identity
                    or ready.get("pendingSha256") != sha(settings) or read_json(settings).get("pending") is not True):
                raise ValueError("Pending settings/identity changed or timed out before external termination")
            result["readyReceiptSha256"] = sha(shared / "ready-to-terminate.json")
            result["pendingSettingsSha256"] = sha(settings)
        finally:
            result["cleanupTerminatedOwnedHandle"] = stop_owned(process)
            result["endedUtc"] = utc()
            write_json(shared / "writer-process.json", result)
    return result


def run_reader(args, shared, raw):
    output = shared / "reader"
    output.mkdir()
    result = {"role": "reader", "startedUtc": utc()}
    with (raw / "read-stdout.log").open("xb") as log:
        process = subprocess.Popen(command_for(args, "read", shared, output, raw), cwd=args.exe.parent,
                                   stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT, shell=False,
                                   **visible_player_startup())
        result["pid"] = process.pid
        try:
            result["exitCode"] = process.wait(timeout=args.timeout)
            if result["exitCode"] != 0:
                raise RuntimeError("Reader Player exited with failure; inspect its actual report/raw logs")
        finally:
            result["cleanupTerminatedOwnedHandle"] = stop_owned(process)
            result["endedUtc"] = utc()
            write_json(shared / "reader-process.json", result)
    return result


def check_actual_recovery(shared, identity, build, main_report, writer, reader):
    receipt = read_json(shared / "recovery-read-receipt.json")
    report_dir = Path(receipt.get("evidenceDirectory") or "").resolve()
    if not report_dir.is_relative_to(shared / "reader"):
        raise ValueError("Recovery reader report is outside its own evidence directory")
    if receipt.get("writerProcessId") != writer["pid"] or receipt.get("readerProcessId") != reader["pid"]:
        raise ValueError("Recovery receipt PIDs differ from the two actual launched processes")
    proof = validate_recovery(shared, identity)
    report = read_json(report_dir / "player-report.json")
    environment = proof["environment"]
    if (environment.get("platform") != "WindowsPlayer" or environment.get("processArchitecture") != "X64"
            or environment.get("buildGuid") != build["buildGuid"]
            or report.get("probeRevision") != main_report.get("probeRevision")
            or report.get("probeAssemblyMvid") != main_report.get("probeAssemblyMvid")):
        raise ValueError("Recovery Player platform/build/probe differs from the successful main run")
    # The source Player report remains untouched. The runner summary omits even
    # benign launch arguments; raw Unity startup output exists only in cache.
    proof["environment"] = {key: value for key, value in environment.items() if key != "probeArguments"}
    return proof


def execute(args):
    identity, build, main_report, shared, raw = prepare(args)
    shared.mkdir(parents=True, exist_ok=False)
    raw.mkdir(parents=True, exist_ok=False)
    summary = {"schema": "p06-windows-display-recovery-runner-1", "startedUtc": utc(), "status": "started", "passed": False,
               "identity": identity, "buildGuid": build["buildGuid"], "mainReportSha256": sha(args.main_results / "player-report.json"),
               "inputs": {name: {"fileName": getattr(args, name).name, "sha256": sha(getattr(args, name))}
                          for name in ("exe", "trace", "debug_trace", "identity", "build_report")},
               "rawLogDirectory": raw.relative_to(ROOT).as_posix(),
               "scope": "Separate Windows processes with real native display/settings recovery; not a replacement main replay/performance/Mac result"}
    if getattr(args, "review_candidate", False):
        from macos.v06.compare_replays import review_reference_notice
        summary["reviewCandidate"] = True
        summary["windowsReference"] = review_reference_notice(main_report, getattr(args, "allow_failed_windows_performance", False))
    code = 1
    try:
        print("Starting the visible writer Player; waiting for its durable pending receipt.", flush=True)
        writer = run_writer(args, shared, raw, identity)
        summary["writer"] = writer
        print("Owned writer terminated while settings remain pending; starting a new reader Player.", flush=True)
        reader = run_reader(args, shared, raw)
        summary["reader"] = reader
        summary["verification"] = check_actual_recovery(shared, identity, build, main_report, writer, reader)
        summary["status"], summary["passed"], code = "passed", True, 0
    except KeyboardInterrupt:
        summary["status"], summary["failureType"], code = "interrupted", "KeyboardInterrupt", 130
    except Exception as error:
        summary["status"], summary["failureType"] = "failed", type(error).__name__
        # Never copy an exception's command line or raw startup output to deliverable evidence.
        (raw / "runner-error.txt").write_text(traceback.format_exc(), encoding="utf-8")
    finally:
        summary["endedUtc"] = utc()
        write_json(shared / "runner-summary.json", summary)
        print("Display recovery: " + summary["status"] + "; evidence: " + str(shared), flush=True)
        if code:
            print("Failure details are retained only in: " + str(raw), flush=True)
    return code


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    for flag in ("exe", "trace", "debug-trace", "identity", "build-report", "main-results", "evidence-dir"):
        parser.add_argument("--" + flag, required=True, type=Path)
    parser.add_argument("--raw-dir", type=Path, help="New child directory under artifacts/cache/v06; default creates a unique one")
    parser.add_argument("--timeout", type=int, default=120, help="Per-process startup/finish timeout; real15-second confirmation is unchanged")
    parser.add_argument("--review-candidate", action="store_true", help="Allow the explicit validated partial Windows reference; recovery itself remains a real strict check")
    parser.add_argument("--allow-failed-windows-performance", action="store_true", help="Diagnostic review reference only; independent native recovery remains strict")
    args = parser.parse_args()
    try:
        return execute(args)
    except Exception as error:
        # Preflight errors occur before any Player launch. Avoid echoing launch arguments.
        raw = ROOT / "artifacts/cache/v06" / ("display-recovery-preflight-" + uuid.uuid4().hex)
        raw.mkdir(parents=True, exist_ok=False)
        (raw / "runner-error.txt").write_text(traceback.format_exc(), encoding="utf-8")
        print("Display recovery preflight rejected: " + type(error).__name__ + ". Raw details: " + str(raw), file=sys.stderr)
        return 2


if __name__ == "__main__":
    raise SystemExit(main())
