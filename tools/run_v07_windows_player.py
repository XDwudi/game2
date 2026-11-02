"""Candidate orchestration only. Default prints a plan; --execute needs a new v07 build.

Never edits raw Player reports, ordinary saves, production sources or packages.
The visible GUI Player is required for rendered/native/performance evidence;
Python helpers have no console window. Only owned process handles are stopped.
"""
import argparse
import contextlib
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import subprocess
import sys
import traceback

sys.dont_write_bytecode = True


def utc():
    return datetime.now(timezone.utc).isoformat()


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write_new(path, value):
    with Path(path).open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def new_child(value, parent):
    path, parent = Path(value).resolve(), Path(parent).resolve()
    if path == parent or not path.is_relative_to(parent) or path.exists():
        raise ValueError("Output must be a nonexistent child of " + str(parent))
    return path


def local_build_files(exe):
    return [{"path": p.relative_to(exe.parent).as_posix(), "sha256": sha(p)}
            for p in sorted(exe.parent.rglob("*")) if p.is_file()
            and not any("BackUpThisFolder" in s or "BurstDebugInformation" in s for s in p.parts)]


def ordinary_save_guard():
    # Read-only guard; no backup is written into the player's real directory.
    # This path is the current ProjectSettings company/product, not an output target.
    root = Path.home() / "AppData/LocalLow/XDwudi/DiceCraft P0"
    names = ("active-run.json", "debug-run.json", "debug-scene.json", "display-settings.json",
             "normal-actions.jsonl", "debug-actions.jsonl")
    files = [p for p in root.glob("*") if p.is_file()
             and any(p.name == n or p.name.startswith(n + ".") for n in names)]
    return {p.name: sha(p) for p in sorted(files)}


def launch(argv, raw, label, timeout, visible):
    result = {"label": label, "argv": [str(x) for x in argv], "startedUtc": utc(),
              "exitCode": None, "timedOut": False, "ownedProcessTerminated": False}
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = 1 if visible else 0
    process = None
    with (raw / (label + "-stdout.log")).open("xb") as log:
        try:
            process = subprocess.Popen(result["argv"], shell=False, cwd=str(Path(argv[0]).parent),
                                       stdin=subprocess.DEVNULL, stdout=log, stderr=subprocess.STDOUT,
                                       startupinfo=startup, creationflags=subprocess.CREATE_NO_WINDOW)
            result["pid"] = process.pid
            try:
                result["exitCode"] = process.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                result["timedOut"] = True
        except Exception:
            (raw / (label + "-exception.txt")).write_text(traceback.format_exc(), encoding="utf-8")
            result["launchFailure"] = True
        finally:
            if process is not None and process.poll() is None:
                process.kill()  # Original handle only; never kill by image name.
                process.wait(timeout=10)
                result["ownedProcessTerminated"] = True
            result["endedUtc"] = utc()
            write_new(raw / (label + "-process.json"), result)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repo", type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument("--build-report", required=True, type=Path)
    parser.add_argument("--expected-build-guid", required=True)
    parser.add_argument("--evidence-root", required=True, type=Path,
                        help="New child of evidence/v07; never an existing Player directory")
    parser.add_argument("--raw-root", required=True, type=Path,
                        help="New child of artifacts/cache/v07")
    parser.add_argument("--import-checkpoint", type=Path,
                        help="Optional returned M5 checkpoint directory; read-only input")
    parser.add_argument("--timeout", type=int, default=1800,
                        help="Each main Player process limit; never changes 180s/16.7ms/15s")
    parser.add_argument("--execute", action="store_true", help="Actually launch only after independent review and final build")
    args = parser.parse_args()
    repo = args.repo.resolve()
    exe = repo / "Builds/v07/Windows/DicePrototype.exe"
    trace = repo / "evidence/v07/session/normal-run-trace.json"
    debug = repo / "evidence/v07/session/debug-24-trace.json"
    identity_path = repo / "DicePrototype/Assets/Resources/BuildIdentity.json"
    print("Plan: frozen-build preflight -> strict full Player -> independent restricted continuation if native failure -> native-only diagnosis if needed -> eligible separate-process recovery -> immutable summary.")
    print("No packaging/publication; restricted evidence never becomes a strict pass.")
    if not args.execute:
        print("PLAN ONLY. No process, source, save, preference or output modified.")
        return 0
    if sys.platform != "win32" or args.timeout < 600:
        raise ValueError("Actual Windows required; outer timeout must be at least600s, not a shortened acceptance window")
    sys.path.insert(0, str(repo / "tools"))
    import package_v07 as packaging
    import verify_v07_display_recovery as recovery_runner
    evidence = new_child(args.evidence_root, repo / "evidence/v07")
    raw = new_child(args.raw_root, repo / "artifacts/cache/v07")
    identity, _, _ = packaging.validated_gate()
    if read(identity_path) != identity:
        raise ValueError("Frozen identity mismatch")
    build = packaging.validate_build(args.build_report.resolve(strict=True), "windows", identity)
    if build["buildGuid"] != args.expected_build_guid or Path(build["output"]).resolve() != exe.resolve():
        raise ValueError("Expected fresh Windows build GUID/output do not match")
    runtime = exe.parent / "DicePrototype_Data/Managed/DiceCraft.Runtime.dll"
    if not exe.is_file() or not runtime.is_file():
        raise ValueError("Complete built v07 Mono Player missing; no legacy fallback is allowed")
    binary = runtime.read_bytes()
    for marker in ("p0-player-verification-7", "p07-player-art-ui-"):
        if marker.encode("utf-16-le") not in binary:
            raise ValueError("Built runtime does not contain the v07 schema/probe; never launch an old Player")
    for path in (trace, debug):
        if not path.is_file():
            raise ValueError("Required real trace input missing")
    if args.import_checkpoint:
        args.import_checkpoint = args.import_checkpoint.resolve(strict=True)
        for name in ("active-run.json", "command.json", "expected.json"):
            if not (args.import_checkpoint / name).is_file():
                raise ValueError("Returned checkpoint input incomplete")
    evidence.mkdir(parents=True, exist_ok=False)
    raw.mkdir(parents=True, exist_ok=False)
    build_files = local_build_files(exe)
    save_before = ordinary_save_guard()
    summary = {"schema": "v07-windows-serial-candidate-1", "startedUtc": utc(), "identity": identity,
               "buildGuid": build["buildGuid"], "strictWindowsAutomatedPassed": False,
               "wholeReleaseAccepted": False, "macRuntime": "NOT_RUN_ON_MAC", "runs": [],
               "inputs": {"exeSha256": sha(exe), "runtimeSha256": sha(runtime), "buildReportSha256": sha(args.build_report),
                          "normalTraceSha256": sha(trace), "debugTraceSha256": sha(debug)},
               "rawDirectory": str(raw), "buildFiles": build_files}

    def run_player(label, flags):
        base = evidence / label
        base.mkdir()
        argv = [exe, *flags, "--evidence-dir", base, "-screen-width", "1920", "-screen-height", "1080",
                "-screen-fullscreen", "0", "-logFile", raw / (label + "-Player.log")]
        result = launch(argv, raw, label, args.timeout, visible=True)
        found = list(base.glob("player-*/player-report.json"))
        data = None
        if len(found) == 1:
            result.update(report=str(found[0]), reportSha256=sha(found[0]))
            try:
                data = read(found[0])
                if (json.loads(data.get("buildIdentity") or "{}") != identity
                        or (data.get("environment") or {}).get("buildGuid") != build["buildGuid"]
                        or data.get("schema") != "p0-player-verification-7"):
                    raise ValueError("Actual Player identity/GUID/schema mismatch")
                result.update(status=data.get("status"), scope=data.get("verificationScope"), phase=data.get("phase"))
            except Exception:
                data = None
                result["bindingFailure"] = True
                (raw / (label + "-report-error.txt")).write_text(traceback.format_exc(), encoding="utf-8")
        else:
            result["reportCount"] = len(found)
        summary["runs"].append(result)
        return result, data

    def accepted_reference(result, data, restricted=False, budget_failed=False):
        if data is None or result.get("timedOut") or result.get("launchFailure"):
            return False
        try:
            expected_exit = 3 if budget_failed else 0
            if result.get("exitCode") != expected_exit:
                return False
            packaging.validate_windows_report(Path(result["report"]).parent, identity, restricted, budget_failed)
            return True
        except Exception:
            (raw / (result["label"] + "-validation-error.txt")).write_text(traceback.format_exc(), encoding="utf-8")
            return False

    common = ["--verify-p0", trace, "--verify-debug-trace", debug]
    if args.import_checkpoint:
        common += ["--verify-import-save", args.import_checkpoint]
    try:
        strict_result, strict = run_player("strict-full", common)
        if strict_result.get("bindingFailure"):
            raise ValueError("Actual build identity mismatch; stop all further launches of this binary")
        strict_ok = accepted_reference(strict_result, strict)
        summary["strictMainPassed"] = strict_ok
        reference, reference_scope = (strict_result, "strict") if strict_ok else (None, None)
        if not strict_ok and accepted_reference(strict_result, strict, True, True):
            reference, reference_scope = strict_result, "diagnostic_performance_failed_strict_native_automated"
        # A native-mode failure happens before debug24/stress. Finish those only in
        # a NEW explicitly restricted process; never mutate/merge the failed report.
        if reference is None and strict is not None and strict.get("phase") == "V07NativeDisplay":
            continuation_result, continuation = run_player("native-deferred-independent", common + ["--verify-defer-native-display"])
            if accepted_reference(continuation_result, continuation, True):
                reference, reference_scope = continuation_result, "restricted_native_pending"
            elif accepted_reference(continuation_result, continuation, True, True):
                reference, reference_scope = continuation_result, "diagnostic_performance_failed_native_pending"
        elif not strict_ok:
            summary["restrictedContinuation"] = "not_run: selected complete strict diagnostic reference" if reference else "not_run: failure is not a native display interruption; fix the original issue first"
        if not strict_ok and reference_scope != "diagnostic_performance_failed_strict_native_automated":
            # Independent small display route may run even if main UI/replay failed.
            # It is not a substitute for any full suite section or physical focus test.
            run_player("native-display-only", ["--verify-display-only"])
        recovery_passed = False
        if reference is not None:
            helper = repo / "tools/verify_v07_display_recovery.py"
            shared = evidence / "separate-process-display-recovery"
            argv = [sys.executable, helper, "--exe", exe, "--trace", trace, "--debug-trace", debug,
                    "--identity", identity_path, "--build-report", args.build_report.resolve(),
                    "--main-results", Path(reference["report"]).parent, "--evidence-dir", shared,
                    "--raw-dir", raw / "recovery", "--timeout", "120"]
            if reference_scope != "strict":
                argv.append("--review-candidate")
            if reference_scope in ("diagnostic_performance_failed_native_pending", "diagnostic_performance_failed_strict_native_automated"):
                argv.append("--allow-failed-windows-performance")
            # Run the existing bounded helper in-process: it owns both game handles
            # and its finally blocks stop them on failure/interruption. Do not kill
            # a helper subprocess and leave an untracked writer/reader child alive.
            recovery = {"label": "separate-process-recovery", "equivalentArgv": [str(x) for x in argv],
                        "startedUtc": utc(), "method": "existing helper.execute with its public CLI fields"}
            recovery_args = argparse.Namespace(exe=exe, trace=trace, debug_trace=debug, identity=identity_path,
                build_report=args.build_report.resolve(), main_results=Path(reference["report"]).parent,
                evidence_dir=shared, raw_dir=raw / "recovery", timeout=120,
                review_candidate=reference_scope != "strict",
                allow_failed_windows_performance=reference_scope in ("diagnostic_performance_failed_native_pending", "diagnostic_performance_failed_strict_native_automated"))
            with (raw / "separate-process-recovery-stdout.log").open("x", encoding="utf-8") as log:
                with contextlib.redirect_stdout(log), contextlib.redirect_stderr(log):
                    recovery["exitCode"] = recovery_runner.execute(recovery_args)
            recovery["endedUtc"] = utc()
            receipt = shared / "runner-summary.json"
            recovery_passed = recovery.get("exitCode") == 0 and receipt.is_file() and read(receipt).get("passed") is True
            summary.update(referenceScope=reference_scope, referenceReport=reference["report"], recoveryPassed=recovery_passed)
            if receipt.is_file():
                recovery.update(summary=str(receipt), summarySha256=sha(receipt))
            summary["runs"].append(recovery)
        else:
            summary["recovery"] = "not_run: existing strict helper requires a complete validated main/restricted/diagnostic reference"
        summary["strictWindowsAutomatedPassed"] = bool(strict_ok and recovery_passed)
    except Exception:
        summary["orchestrationFailure"] = True
        (raw / "orchestration-error.txt").write_text(traceback.format_exc(), encoding="utf-8")
    finally:
        summary["ordinarySaveFilesUnchanged"] = ordinary_save_guard() == save_before
        summary["buildFilesUnchanged"] = local_build_files(exe) == build_files
        if not summary["ordinarySaveFilesUnchanged"] or not summary["buildFilesUnchanged"]:
            summary["strictWindowsAutomatedPassed"] = False
        summary["endedUtc"] = utc()
        summary["status"] = "automated_windows_scope_passed_manual_art_pending" if summary["strictWindowsAutomatedPassed"] else "failed_or_partial_no_full_acceptance"
        write_new(evidence / "serial-summary.json", summary)
    print(summary["status"] + "; evidence: " + str(evidence))
    return 0 if summary["strictWindowsAutomatedPassed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
