#!/usr/bin/env python3
"""Prepare, re-extract and run the p0.5 Universal package on a real Apple Silicon Mac."""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import platform
import plistlib
import re
import shutil
import stat
import subprocess
import sys
import time
import traceback

from compare_replays import compare_directories, load_json, sha256
from package_checks import macho_info, safe_archive_inventory

VERSION = "p05-macos-verifier-1"
REQUIRED_TOOLS = ("/usr/bin/ditto", "/usr/bin/arch", "/usr/bin/sw_vers", "/usr/bin/sysctl", "/usr/sbin/system_profiler",
                  "/usr/bin/codesign", "/usr/sbin/spctl", "/usr/bin/xattr", "/usr/bin/sample")


def utc():
    return datetime.now(timezone.utc).isoformat()


def write_json(path, data):
    Path(path).write_text(json.dumps(data, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")


def run_record(command, timeout=60):
    """Never pass an input path or argument through a shell."""
    started = utc()
    try:
        result = subprocess.run([str(x) for x in command], text=True, encoding="utf-8", errors="replace",
                                capture_output=True, timeout=timeout, check=False)
        return {"argv": [str(x) for x in command], "startedUtc": started, "exitCode": result.returncode,
                "stdout": result.stdout, "stderr": result.stderr}
    except (OSError, subprocess.TimeoutExpired) as error:
        return {"argv": [str(x) for x in command], "startedUtc": started, "exitCode": None, "error": str(error)}


def must_run(command, evidence, name, timeout=300):
    result = run_record(command, timeout)
    write_json(evidence / (name + ".json"), result)
    if result.get("exitCode") != 0:
        raise RuntimeError(f"{name} failed; see evidence/{name}.json")
    return result


def quarantine(path):
    return run_record(["/usr/bin/xattr", "-p", "com.apple.quarantine", path])


def security_report(app):
    return {"display": run_record(["/usr/bin/codesign", "-d", "--verbose=4", app]),
            "verify": run_record(["/usr/bin/codesign", "--verify", "--deep", "--strict", "--verbose=4", app]),
            "assessment": run_record(["/usr/sbin/spctl", "--assess", "--type", "execute", "--verbose=4", app]),
            "quarantine": quarantine(app),
            "policy": "Record only. No signing, notarization, quarantine removal, spctl disable, or alternate launch to evade an OS rejection."}


def app_from(root):
    apps = [p for p in root.rglob("*.app") if p.is_dir() and not any(x.suffix == ".app" for x in p.relative_to(root).parents)]
    if len(apps) != 1 or apps[0].name != "DicePrototype.app":
        raise ValueError("Expected exactly one top-level DicePrototype.app in the package")
    app = apps[0]
    if app.is_symlink():
        raise ValueError("Top-level .app cannot be a symlink")
    with (app / "Contents/Info.plist").open("rb") as stream:
        info = plistlib.load(stream)
    executable = info.get("CFBundleExecutable")
    if not isinstance(executable, str) or not executable or Path(executable).name != executable:
        raise ValueError("Invalid CFBundleExecutable")
    main = app / "Contents/MacOS" / executable
    if not main.is_file() or not (app / "Contents/Resources").is_dir():
        raise ValueError("Incomplete .app: executable or Resources missing")
    return app, main, info


def inventory(app, repair_execute=False):
    result, native, changes = [], [], []
    base = app.resolve()
    for path in sorted(app.rglob("*")):
        relative = path.relative_to(app).as_posix()
        if not path.resolve().is_relative_to(base):
            raise ValueError(f"App contains an external/broken escaping symlink: {relative}")
        if path.is_symlink():
            if not path.exists():
                raise ValueError(f"App contains a broken symlink: {relative}")
            result.append({"path": relative, "kind": "symlink", "target": os.readlink(path)})
            continue
        if path.is_dir():
            result.append({"path": relative, "kind": "directory", "mode": oct(stat.S_IMODE(path.stat().st_mode))})
            continue
        details = macho_info(path)
        if details:
            if set(details["architectures"]) != {"arm64", "x86_64"}:
                raise ValueError(f"Native component is not Universal arm64+x86_64: {relative}: {details['architectures']}")
            before = stat.S_IMODE(path.stat().st_mode)
            # Only Mach-O MH_EXECUTE files need this Windows-export repair. Dylib data is not chmodded.
            if repair_execute and any(s["filetype"] == 2 for s in details["slices"]) and not before & stat.S_IXUSR:
                after = before | stat.S_IXUSR
                path.chmod(after)
                changes.append({"path": relative, "before": oct(before), "after": oct(after), "reason": "MH_EXECUTE lacked owner execute bit"})
            native.append({"path": relative, **details, "mode": oct(stat.S_IMODE(path.stat().st_mode)), "sha256": sha256(path)})
        result.append({"path": relative, "kind": "file", "mode": oct(stat.S_IMODE(path.stat().st_mode)),
                       "byteSize": path.stat().st_size, "sha256": sha256(path)})
    if not native:
        raise ValueError("No Mach-O components in app")
    return {"files": result, "nativeComponents": native, "executeBitRepairs": changes}


def load_identity(path):
    identity = load_json(path)
    commit = identity.get("sourceCommit")
    if not isinstance(commit, str) or not re.fullmatch(r"[0-9a-fA-F]{40}", commit):
        raise ValueError("Identity requires a real 40-character sourceCommit")
    for field in ("contentConfigSha256", "goldenCaseSetSha256"):
        if not isinstance(identity.get(field), str) or not re.fullmatch(r"[0-9a-fA-F]{64}", identity[field]):
            raise ValueError("Identity requires a real " + field)
    return identity


def find_report(base):
    reports = list(base.glob("player-*/player-report.json"))
    if len(reports) != 1:
        raise RuntimeError(f"Expected one actual Player report; found {len(reports)}")
    return reports[0]


def launch_probe(main, trace, checkpoint, output, evidence, timeout):
    command = ["/usr/bin/arch", "-arm64", str(main), "--verify-p0", str(trace), "--evidence-dir", str(output),
               "--verify-import-save", str(checkpoint),
               "-screen-width", "1920", "-screen-height", "1080", "-screen-fullscreen", "0", "-logFile", str(evidence / "Player.log")]
    result = {"argv": command, "startedUtc": utc(), "exitCode": None, "requestedArchitecture": "arm64"}
    print("Running the extracted app natively. Keep its window visible; do not interact during the automatic probe.", flush=True)
    with (evidence / "player-stdout.log").open("w", encoding="utf-8") as log:
        process = subprocess.Popen(command, stdout=log, stderr=subprocess.STDOUT)
        result["pid"] = process.pid
        start = time.monotonic()
        try:
            # One startup sample records the actual process architecture before the stress sample.
            time.sleep(1)
            sample = run_record(["/usr/bin/sample", str(process.pid), "1", "-file", evidence / "native-process-sample.txt"], timeout=20)
            write_json(evidence / "native-process-sample-command.json", sample)
            sample_path = evidence / "native-process-sample.txt"
            text = sample_path.read_text(encoding="utf-8", errors="replace") if sample_path.exists() else ""
            match = re.search(r"^Code Type:\s*(.+)$", text, re.MULTILINE)
            result["sampleCodeType"] = match.group(1).strip() if match else None
            result["sampleNativeArm64Observed"] = bool(match and re.search(r"ARM[- ]?64", match.group(1), re.IGNORECASE) and "Translated" not in match.group(1))
            while process.poll() is None:
                if time.monotonic() - start > timeout:
                    raise TimeoutError(f"Player exceeded {timeout} seconds; own process will be stopped and failure retained")
                print(f"Player running: {int(time.monotonic() - start)} s", flush=True)
                try:
                    process.wait(timeout=15)
                except subprocess.TimeoutExpired:
                    pass
            result["exitCode"] = process.returncode
        finally:
            if process.poll() is None:
                process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()
                result["stoppedByVerifier"] = True
            result["elapsedSeconds"] = time.monotonic() - start
            result["endedUtc"] = utc()
            write_json(evidence / "player-process.json", result)
    return result


def execute(args, work, evidence, summary):
    if sys.platform != "darwin":
        raise RuntimeError("This execution script requires a real macOS device; comparison tests can run separately on Windows")
    if sys.version_info < (3, 9):
        raise RuntimeError("Python 3.9+ is required; no installer is run automatically")
    missing = [p for p in REQUIRED_TOOLS if not os.access(p, os.X_OK)]
    if missing:
        raise RuntimeError("Required system tools unavailable: " + ", ".join(missing))
    hardware_arm = must_run(["/usr/bin/sysctl", "-n", "hw.optional.arm64"], evidence, "hardware-arm64")
    if hardware_arm["stdout"].strip() != "1":
        raise RuntimeError("Apple Silicon hardware is required; Intel execution is a separate unverified target")
    device = {"reportedByUser": "macOS 26.6.1 / Apple Silicon M5", "pythonVersion": sys.version,
              "pythonProcessMachine": platform.machine(), "os": run_record(["/usr/bin/sw_vers"]),
              "cpu": run_record(["/usr/bin/sysctl", "-n", "machdep.cpu.brand_string"]),
              "memory": run_record(["/usr/bin/sysctl", "-n", "hw.memsize"]),
              "graphics": run_record(["/usr/sbin/system_profiler", "SPDisplaysDataType", "-json"], 120)}
    write_json(evidence / "device.json", device)
    identity = load_identity(args.identity)
    summary["identity"] = identity
    summary["inputPackage"] = {"path": str(args.package), "byteSize": args.package.stat().st_size, "sha256": sha256(args.package), "quarantine": quarantine(args.package)}
    summary["trace"] = {"sha256": sha256(args.trace), "path": str(args.trace)}
    write_json(evidence / "input-archive.json", safe_archive_inventory(args.package))
    inputs = work / "inputs"
    inputs.mkdir()
    # Keep the received bytes and metadata unchanged, including any quarantine attribute.
    original = inputs / "received-macos.zip"
    must_run(["/usr/bin/ditto", args.package, original], evidence, "preserve-input-package")
    if sha256(original) != summary["inputPackage"]["sha256"]:
        raise RuntimeError("Input preservation hash mismatch")
    shutil.copy2(args.trace, inputs / "normal-run-trace.json")
    shutil.copy2(args.identity, inputs / "BUILD_IDENTITY.json")
    windows = evidence / "windows-reference"
    windows.mkdir()
    for name in ("player-report.json", "normal-replay.jsonl", "debug24-replay.jsonl"):
        shutil.copy2(args.windows_results / name, windows / name)
    checkpoint = windows / "cross-platform-checkpoint"
    checkpoint.mkdir()
    for name in ("active-run.json", "command.json", "expected.json"):
        shutil.copy2(args.windows_results / "cross-platform-checkpoint" / name, checkpoint / name)
    unpack = work / "received-extracted"
    unpack.mkdir()
    must_run(["/usr/bin/ditto", "-x", "-k", original, unpack], evidence, "extract-received")
    app, main, plist = app_from(unpack)
    write_json(evidence / "info-plist.json", plist)
    write_json(evidence / "security-before.json", security_report(app))
    prepared = inventory(app, repair_execute=True)
    write_json(evidence / "prepared-app-inventory.json", prepared)
    if not os.access(main, os.X_OK):
        raise RuntimeError("Main executable still lacks executable permission")
    package = work / "DicePrototype-p0.5.0-macos-universal.zip"
    must_run(["/usr/bin/ditto", "-c", "-k", "--keepParent", app, package], evidence, "ditto-final-package")
    summary["finalPackage"] = {"path": str(package), "byteSize": package.stat().st_size, "sha256": sha256(package),
                               "packaging": "macOS system ditto -c -k --keepParent", "signingModified": False}
    write_json(evidence / "final-archive.json", safe_archive_inventory(package))
    fresh = work / "final-package-fresh-extract"
    fresh.mkdir()
    must_run(["/usr/bin/ditto", "-x", "-k", package, fresh], evidence, "extract-final-package")
    app, main, _ = app_from(fresh)
    restored = inventory(app)
    write_json(evidence / "fresh-app-inventory.json", restored)
    if prepared["files"] != restored["files"] or prepared["nativeComponents"] != restored["nativeComponents"]:
        raise RuntimeError("Final ZIP extraction changed file bytes, Unix modes, native slices or symlink targets")
    security = security_report(app)
    write_json(evidence / "security-final.json", security)
    summary["signing"] = security
    input_quarantined = summary["inputPackage"]["quarantine"].get("exitCode") == 0
    app_quarantined = security["quarantine"].get("exitCode") == 0
    if input_quarantined and not app_quarantined:
        raise RuntimeError("Quarantine did not propagate to the fresh app; stop rather than bypass the original OS review")
    if security["assessment"].get("exitCode") != 0 and (input_quarantined or app_quarantined):
        summary["status"] = "blocked_by_macos_security_assessment"
        raise RuntimeError("macOS assessment did not accept the quarantined app. System approval/signing must be resolved on the Mac; this script does not remove quarantine or bypass Gatekeeper")
    output = evidence / "player"
    output.mkdir()
    process = launch_probe(main, inputs / "normal-run-trace.json", checkpoint, output, evidence, args.timeout)
    summary["playerProcess"] = process
    report_path = find_report(output)
    report = load_json(report_path)
    summary["actualPlayerReport"] = str(report_path)
    summary["playerStatus"] = report.get("status")
    summary["performance"] = report.get("performance")
    if report.get("configSha256", "").lower() != identity["contentConfigSha256"].lower():
        raise RuntimeError("Actual Player config hash does not match the supplied build identity")
    if report.get("traceSha256") != summary["trace"]["sha256"]:
        raise RuntimeError("Actual Player used a different trace")
    embedded_identity = json.loads(report.get("buildIdentity") or "{}")
    for field in ("sourceCommit", "contentConfigSha256", "goldenCaseSetSha256"):
        if embedded_identity.get(field) != identity[field]:
            raise RuntimeError("Actual Player embedded identity mismatch: " + field)
    summary["importWindowsSchema3Save"] = report.get("importedCheckpointStatus", "not_run")
    summary["exportedMacCheckpoint"] = str(report_path.parent / "cross-platform-checkpoint") if report.get("checkpointExported") else None
    summary["nativeArm64Observed"] = report.get("environment", {}).get("processArchitecture") == "Arm64"
    comparison = compare_directories(windows, report_path.parent)
    write_json(evidence / "cross-platform-comparison.json", comparison)
    summary["crossPlatformRulesPassed"] = comparison["passed"]
    summary["automaticChecksPassed"] = process.get("exitCode") == 0 and summary["nativeArm64Observed"] and comparison["passed"]
    summary["status"] = "automated_checks_passed_manual_checks_pending" if summary["automaticChecksPassed"] else "automated_checks_failed"
    return 0 if summary["automaticChecksPassed"] else 1


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", required=True, type=Path)
    parser.add_argument("--trace", required=True, type=Path)
    parser.add_argument("--windows-results", required=True, type=Path, help="Actual Windows player-* directory")
    parser.add_argument("--identity", required=True, type=Path, help="Actual BUILD_IDENTITY.json or release manifest with source/config/golden SHA fields")
    parser.add_argument("--output", type=Path, help="New directory; existing directories are refused")
    parser.add_argument("--timeout", type=int, default=1800, help="Total Player timeout, seconds; stress duration is unchanged")
    args = parser.parse_args()
    for name in ("package", "trace", "windows_results", "identity"):
        setattr(args, name, getattr(args, name).expanduser().resolve(strict=True))
    if args.timeout < 240:
        parser.error("--timeout must be at least 240; the probe includes a 180-second pressure sample")
    work = args.output.expanduser().absolute() if args.output else Path.home() / "Desktop" / ("DicePrototype-MacVerification-" + datetime.now().strftime("%Y%m%d-%H%M%S"))
    work.mkdir(parents=True, exist_ok=False)
    evidence = work / "evidence"
    evidence.mkdir()
    summary = {"schema": VERSION, "startedUtc": utc(), "status": "started", "automaticChecksPassed": False,
               "intelRuntimeStatus": "not_tested: Universal contains x86_64; no Intel runtime claim",
               "importWindowsSchema3Save": "not_run", "importMacCheckpointOnWindows": "pending_return_to_windows",
               "manualChecks": {"mouseAndTrackpad": "not_run", "stableStateQuitAndResume": "not_run",
                                "visualAndFontReview": "not_run", "versionPageMatchesIdentity": "not_run"},
               "scope": "Automated Mac package/probe preparation; full release acceptance requires manual checks and independent review."}
    code = 1
    try:
        code = execute(args, work, evidence, summary)
    except KeyboardInterrupt:
        summary["status"] = "interrupted"
        summary["failure"] = "User interrupted verification; partial evidence retained"
        code = 130
    except Exception as error:
        if summary["status"] == "started":
            summary["status"] = "failed"
        summary["failure"] = str(error)
        (evidence / "verifier-error.txt").write_text(traceback.format_exc(), encoding="utf-8")
        print(str(error), file=sys.stderr)
    finally:
        summary["endedUtc"] = utc()
        write_json(evidence / "mac-verification-summary.json", summary)
        if sys.platform == "darwin" and os.access("/usr/bin/ditto", os.X_OK):
            result = run_record(["/usr/bin/ditto", "-c", "-k", "--keepParent", evidence, work / "MacVerification-Evidence.zip"], 300)
            write_json(work / "evidence-package-command.json", result)
            if result.get("exitCode") == 0:
                package = work / "MacVerification-Evidence.zip"
                write_json(work / "evidence-package-sha256.json", {"path": package.name, "sha256": sha256(package), "byteSize": package.stat().st_size})
            else:
                print("Evidence ZIP failed; retain the evidence directory and command result.", file=sys.stderr)
                code = 1
        print("Status: " + summary["status"] + "\nOutput: " + str(work))
    return code


if __name__ == "__main__":
    sys.exit(main())
