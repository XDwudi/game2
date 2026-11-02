"""Read-only plan by default; opt-in, bounded Windows resource/idle diagnostic.

Never replaces formal 180-second performance, display, Mac or release acceptance.
No packaging, publication, network, registry writes or ordinary-save restoration.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import sys

sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[3]
REGISTRY_PATH = r"Software\XDwudi\DiceCraft P0"
FLAG = "--art07-resource-diagnostic"
SCHEMA = "art07-player-resources-1"


def utc():
    return datetime.now(timezone.utc).isoformat()


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def write_new(path, value):
    with Path(path).open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def is_reparse(path):
    info = path.lstat()
    return path.is_symlink() or bool(getattr(info, "st_file_attributes", 0) & stat.FILE_ATTRIBUTE_REPARSE_POINT)


def fresh_cache_directory(directory, repo):
    directory = Path(directory).absolute()
    parent = (repo / "artifacts/cache").resolve(strict=True)
    if directory.exists() or directory.is_symlink():
        raise ValueError("Directory must not already exist")
    resolved = directory.resolve()
    if resolved == parent or not resolved.is_relative_to(parent):
        raise ValueError("Directory must be a new child of the repository task cache")
    for ancestor in directory.parents:
        if ancestor.exists() and is_reparse(ancestor):
            raise ValueError("Output ancestry contains a link/reparse point")
        if ancestor.resolve() == parent:
            break
    return resolved


def tree_guard(root):
    """Every regular file's byte hash, including backups and unknown files. Never read outside root."""
    root = Path(root)
    if not root.exists():
        return {"exists": False, "files": []}
    if not root.is_dir() or is_reparse(root):
        raise ValueError("Guard root must be a real directory")
    files = []
    for current, directories, names in os.walk(root, followlinks=False):
        for name in sorted(directories + names):
            path = Path(current) / name
            if is_reparse(path):
                raise ValueError("Guard refuses a link/reparse point: " + path.relative_to(root).as_posix())
        for name in sorted(names):
            path = Path(current) / name
            if not path.is_file():
                raise ValueError("Guard found a non-regular file")
            before = path.stat()
            fingerprint = sha(path)
            after = path.stat()
            if before.st_size != after.st_size or before.st_mtime_ns != after.st_mtime_ns:
                raise ValueError("File changed during guard hashing: " + path.relative_to(root).as_posix())
            files.append({"path": path.relative_to(root).as_posix(), "bytes": after.st_size, "sha256": fingerprint})
    return {"exists": True, "files": sorted(files, key=lambda row: row["path"])}


def preference_group(raw_name):
    name = re.sub(r"_h[0-9]+$", "", raw_name)
    if name.startswith("art07.") or name in {"fast", "reduced", "shopTab", "volume"}:
        return "game"
    if name.lower().startswith("screenmanager"):
        return "engineScreenmanager"
    return None


def fingerprint_preferences(rows, exists=True):
    """Whitelist names, hash type+value privately; raw values are never returned or logged."""
    groups = {"game": [], "engineScreenmanager": []}
    for raw_name, value, kind in rows:
        group = preference_group(raw_name)
        if group is None:
            continue
        encoded = {"type": kind, "value": {"bytesHex": value.hex()} if isinstance(value, bytes) else value}
        digest = hashlib.sha256(json.dumps(encoded, ensure_ascii=False, sort_keys=True, separators=(",", ":")).encode("utf-8")).hexdigest()
        groups[group].append({"name": raw_name, "type": kind, "sha256": digest})
    return {"registryKeyExists": exists, **{key: sorted(value, key=lambda row: row["name"]) for key, value in groups.items()}}


def read_preferences():
    import ctypes
    from ctypes import wintypes
    import winreg
    rows = []
    enum_names = ctypes.WinDLL("advapi32", use_last_error=True).RegEnumValueW
    enum_names.argtypes = [wintypes.HKEY, wintypes.DWORD, wintypes.LPWSTR, ctypes.POINTER(wintypes.DWORD),
                           ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p, ctypes.c_void_p]
    enum_names.restype = wintypes.LONG
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, REGISTRY_PATH, 0, winreg.KEY_READ) as key:
            count = winreg.QueryInfoKey(key)[1]
            for index in range(count):
                # Enum names without fetching unrelated values. Query only the explicit whitelist.
                size = wintypes.DWORD(32768)
                name = ctypes.create_unicode_buffer(size.value)
                result = enum_names(int(key), index, name, ctypes.byref(size), None, None, None, None)
                if result:
                    raise OSError(result, "Could not enumerate game preference names")
                if preference_group(name.value) is not None:
                    value, kind = winreg.QueryValueEx(key, name.value)
                    rows.append((name.value, value, kind))
    except FileNotFoundError:
        return fingerprint_preferences([], exists=False)
    return fingerprint_preferences(rows)


def validate_inputs(repo, report_path, identity, build):
    """Called only after the current frozen gate succeeds; independently bind output/GUID/feature."""
    exe = (repo / "Builds/v07/Windows/DicePrototype.exe").resolve(strict=True)
    if not exe.is_relative_to(repo / "Builds/v07/Windows"):
        raise ValueError("Built executable resolves outside the expected output directory")
    raw_identity = build.get("identity")
    embedded = json.loads(raw_identity) if isinstance(raw_identity, str) else raw_identity
    guid = build.get("buildGuid")
    if (build.get("result") != "Succeeded" or build.get("errors") != 0 or embedded != identity
            or build.get("platform") != "StandaloneWindows64" or build.get("unity") != identity["unityEditorVersion"]
            or not build.get("profile", "").endswith("/Windows_x64.asset") or not build.get("buildUtc")
            or not re.fullmatch(r"[0-9a-fA-F]{32}", guid or "")
            or Path(build.get("output", "")).resolve() != exe):
        raise ValueError("Build report does not identify the current frozen Windows x64 output")
    boot = exe.parent / "DicePrototype_Data/boot.config"
    boot_guids = [line.partition("=")[2].strip() for line in boot.read_text(encoding="utf-8-sig").splitlines() if line.startswith("build-guid=")]
    if boot_guids != [guid]:
        raise ValueError("Actual boot.config GUID differs from the build report")
    runtime = exe.parent / "DicePrototype_Data/Managed/DiceCraft.Runtime.dll"
    binary = runtime.read_bytes()
    for marker in (FLAG, SCHEMA):
        if marker.encode("utf-16-le") not in binary:
            raise ValueError("Built runtime lacks the resource diagnostic marker: " + marker)
    return {"exe": exe, "runtime": runtime, "buildGuid": guid, "identity": identity,
            "buildReport": report_path, "buildReportSha256": sha(report_path),
            "exeSha256": sha(exe), "runtimeSha256": sha(runtime), "bootSha256": sha(boot)}


def preflight(repo, report_path, directory, timeout):
    if not 240 <= timeout <= 300:
        raise ValueError("Outer timeout must remain within 240–300 seconds")
    directory = fresh_cache_directory(directory, repo)
    report_path = report_path.resolve(strict=True)
    if not report_path.is_relative_to((repo / "evidence/v07/unity").resolve()):
        raise ValueError("Use the actual local v07 Unity build report")
    sys.path.insert(0, str(repo / "tools"))
    import package_v07 as packaging
    if packaging.ROOT.resolve() != repo:
        raise ValueError("Loaded gate validator belongs to a different repository")
    identity, _, _ = packaging.validated_gate()
    build = packaging.validate_build(report_path, "windows", identity)
    bound = validate_inputs(repo, report_path, identity, build)
    bound.update(directory=directory, timeout=timeout)
    return bound


def player_argv(bound):
    outer = bound["directory"]
    return [str(bound["exe"]), FLAG, str(outer / "diagnostic"),
            "-screen-width", "1920", "-screen-height", "1080", "-screen-fullscreen", "0",
            "-logFile", str(outer / "Player.log")]


def validate_launch_binding(bound, build_guard):
    """Refuse a changed preflight input instead of accepting it as the new before-run baseline."""
    rows = {row["path"]: row for row in build_guard["files"]}
    for label, path, expected in (
            ("exe", bound["exe"], bound["exeSha256"]),
            ("runtime", bound["runtime"], bound["runtimeSha256"]),
            ("boot", bound["exe"].parent / "DicePrototype_Data/boot.config", bound["bootSha256"]),
            ("buildReport", bound["buildReport"], bound["buildReportSha256"])):
        if sha(path) != expected:
            raise ValueError("Input changed after preflight: " + label)
        if label != "buildReport":
            row = rows.get(path.relative_to(bound["exe"].parent).as_posix())
            if row is None or row["sha256"] != expected or row["bytes"] != path.stat().st_size:
                raise ValueError("Complete before-build guard differs from preflight input: " + label)


def process_run(argv, outer, timeout, popen=None):
    """Own exactly one handle; no image-name/taskkill/other-process control."""
    popen = subprocess.Popen if popen is None else popen
    result = {"startedUtc": utc(), "exitCode": None, "timedOut": False, "ownedProcessTerminated": False}
    process = None
    try:
        with (outer / "process-output.log").open("xb") as output:
            process = popen(argv, cwd=str(Path(argv[0]).parent), shell=False, stdin=subprocess.DEVNULL,
                            stdout=output, stderr=subprocess.STDOUT, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
            result["pid"] = process.pid
            try:
                result["exitCode"] = process.wait(timeout=timeout)
            except subprocess.TimeoutExpired:
                result["timedOut"] = True
    except BaseException as error:
        result["orchestrationError"] = type(error).__name__ + ": " + str(error)
    finally:
        if process is not None and process.poll() is None:
            try:
                process.kill()
                result["ownedProcessTerminated"] = True
                result["terminationExitCode"] = process.wait(timeout=10)
            except BaseException as error:
                result["terminationError"] = type(error).__name__ + ": " + str(error)
        result["ownedProcessStillRunning"] = process is not None and process.poll() is None
        result["endedUtc"] = utc()
        write_new(outer / "process.json", result)
    return result


def report_binding_failures(data, bound, output):
    failures = []
    try:
        identity = json.loads(data.get("buildIdentity") or "{}") if isinstance(data.get("buildIdentity"), str) else data.get("buildIdentity")
        if identity != bound["identity"]:
            failures.append("actual_build_identity_mismatch")
    except (TypeError, ValueError):
        failures.append("invalid_actual_build_identity")
    for key, expected in (("schema", SCHEMA), ("buildGuid", bound["buildGuid"]), ("editor", False),
                          ("wholeReleaseAccepted", False), ("startupDisplaySuppressed", True),
                          ("unity", bound["identity"]["unityEditorVersion"]), ("platform", "WindowsPlayer"),
                          ("configVersion", bound["identity"]["contentConfigVersion"]),
                          ("configSha256", bound["identity"]["contentConfigSha256"]),
                          ("width", 1920), ("height", 1080), ("displayMode", "Windowed")):
        if data.get(key) != expected or isinstance(expected, bool) and type(data.get(key)) is not bool:
            failures.append("actual_" + key + "_mismatch")
    for key, expected in (("output", output), ("saveDirectory", output / "isolated-saves")):
        try:
            if Path(data.get(key, "")).resolve() != expected.resolve():
                failures.append("actual_" + key + "_mismatch")
        except (TypeError, ValueError):
            failures.append("invalid_actual_" + key)
    if not isinstance(data.get("limitation"), str) or not data["limitation"]:
        failures.append("actual_scope_missing")
    if not isinstance(data.get("phases"), list) or not data["phases"]:
        failures.append("actual_phase_history_missing")
    return failures


def classify(summary):
    # A completed diagnostic is still explicitly NOT formal performance or whole-release acceptance.
    if summary.get("errors"):
        return "diagnostic_failed_or_incomplete", 1
    process = summary["process"]
    actual = summary.get("actualDiagnostic") or {}
    if (process.get("exitCode") == 0 and not process.get("timedOut") and not process.get("orchestrationError")
            and not process.get("terminationError") and not process.get("ownedProcessStillRunning")
            and actual.get("status") == "diagnostic_completed_not_acceptance"
            and actual.get("error") in (None, "") and actual.get("idleObservationComplete") is True):
        return "diagnostic_completed_not_acceptance", 0
    return "diagnostic_failed_or_incomplete", 1


def execute(bound, preferences_reader=read_preferences, launch=process_run):
    if sys.platform != "win32":
        raise ValueError("Actual Windows required for execution")
    outer = fresh_cache_directory(bound["directory"], ROOT)
    ordinary = Path.home() / "AppData/LocalLow/XDwudi/DiceCraft P0"
    before_build = tree_guard(bound["exe"].parent)
    before_save = tree_guard(ordinary)
    before_prefs = preferences_reader()
    validate_launch_binding(bound, before_build)
    outer.mkdir(parents=True, exist_ok=False)
    # The child directory stays nonexistent until the opt-in diagnostic itself creates it.
    summary = {"schema": "v07-resource-launcher-1", "startedUtc": utc(), "wholeReleaseAccepted": False,
               "formalPerformanceAcceptance": False, "purpose": "Independent resource/idle diagnostic only",
               "identity": bound["identity"], "buildGuid": bound["buildGuid"], "timeoutSeconds": bound["timeout"],
               "inputHashes": {key: bound[key] for key in ("buildReportSha256", "exeSha256", "runtimeSha256", "bootSha256")},
               "argv": player_argv(bound), "errors": [], "process": {}, "actualDiagnostic": None}
    write_new(outer / "external-guards-before.json", {"build": before_build, "ordinarySaves": before_save, "preferences": before_prefs})
    try:
        validate_launch_binding(bound, before_build)
        summary["process"] = launch(player_argv(bound), outer, bound["timeout"])
        path = outer / "diagnostic/resource-report.json"
        if not path.is_file():
            summary["errors"].append("resource_report_missing")
        else:
            data = read_json(path)
            phases = data.get("phases")
            summary["actualDiagnostic"] = {"path": str(path), "sha256": sha(path), "status": data.get("status"),
                "error": data.get("error"), "phaseHistory": phases, "lastPhase": phases[-1] if isinstance(phases, list) and phases else None,
                "scope": data.get("limitation"), "geometryScope": data.get("geometryScope"), "memoryMethod": data.get("memoryMethod"),
                "idleObservationComplete": (data.get("idle") or {}).get("observationComplete")}
            summary["errors"].extend(report_binding_failures(data, bound, outer / "diagnostic"))
            for name in ("idle-frames.csv", "resource-frames.csv"):
                if not (path.parent / name).is_file() or (path.parent / name).stat().st_size == 0:
                    summary["errors"].append("missing_or_empty_" + name)
    except BaseException as error:
        summary["errors"].append(type(error).__name__ + ": " + str(error))
    finally:
        after = {}
        for key, getter in (("build", lambda: tree_guard(bound["exe"].parent)), ("ordinarySaves", lambda: tree_guard(ordinary)), ("preferences", preferences_reader)):
            try:
                after[key] = getter()
            except BaseException as error:
                summary["errors"].append(key + "_guard_unreadable:" + type(error).__name__)
        write_new(outer / "external-guards-after.json", after)
        summary["buildFilesUnchanged"] = after.get("build") == before_build
        summary["ordinarySaveFilesUnchanged"] = after.get("ordinarySaves") == before_save
        prefs_after = after.get("preferences")
        summary["ordinaryGamePreferenceValuesUnchanged"] = prefs_after is not None and prefs_after["game"] == before_prefs["game"]
        summary["engineScreenmanagerValuesUnchanged"] = prefs_after is not None and prefs_after["engineScreenmanager"] == before_prefs["engineScreenmanager"]
        summary["engineScreenmanagerScope"] = "Unity engine persisted display values only; recorded separately, never merged into the game-preference guard"
        for key in ("buildFilesUnchanged", "ordinarySaveFilesUnchanged", "ordinaryGamePreferenceValuesUnchanged"):
            if not summary[key]:
                summary["errors"].append(key + "_failed")
        summary["endedUtc"] = utc()
        summary["status"], code = classify(summary)
        write_new(outer / "runner-summary.json", summary)
    # Only a compact local status is printed, never preference values, ordinary saves or log content.
    print(json.dumps({"status": summary["status"], "wholeReleaseAccepted": False, "summary": str(outer / "runner-summary.json")}, ensure_ascii=False))
    return code


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build-report", required=True, type=Path)
    parser.add_argument("--directory", required=True, type=Path)
    parser.add_argument("--timeout", type=int, default=270, help="Outer process limit, 240–300s; does not alter diagnostic samples")
    parser.add_argument("--execute", action="store_true", help="Explicitly launch the bound visible windowed Player")
    args = parser.parse_args(argv)
    bound = preflight(ROOT, args.build_report, args.directory, args.timeout)
    if not args.execute:
        print(json.dumps({"status": "plan_only", "identity": bound["identity"], "buildGuid": bound["buildGuid"],
                          "argv": player_argv(bound), "timeoutSeconds": bound["timeout"], "wholeReleaseAccepted": False,
                          "writesOrProcessesStarted": False}, ensure_ascii=False, indent=2))
        return 0
    return execute(bound)


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (OSError, ValueError, subprocess.CalledProcessError) as error:
        print("Preflight refused: " + type(error).__name__ + ": " + str(error), file=sys.stderr)
        raise SystemExit(2)
