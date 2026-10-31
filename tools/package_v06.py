"""Package frozen v0.6 players; native Mac acceptance is a separate real-device step."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import xml.etree.ElementTree as ET
import zipfile
from quality_check import check_nunit

from macos.v06.package_checks import macho_info, safe_archive_inventory

ROOT = Path(__file__).resolve().parents[1]
VERSION = "p0.6.0"
TAG = VERSION + "-rc.1"
IDENTITY = "DicePrototype/Assets/Resources/BuildIdentity.json"
CONFIG = "DicePrototype/Assets/Game/Data/P0Config.json"
GOLDEN = "DicePrototype/Assets/Game/Data/GoldenScores.json"
SCOPES = ("DicePrototype/Assets", "DicePrototype/Packages", "DicePrototype/ProjectSettings")
GATE = "evidence/v06/unity/build-gate.json"
RELEASE = "Releases/p0.6.0"
REVIEW_RELEASE = RELEASE + "/review-candidate"
ASSEMBLIES = {"DiceCraft.Core", "DiceCraft.Runtime", "DiceCraft.EditMode", "DiceCraft.PlayModeTests", "DiceCraft.Editor"}


def sha(path):
    value = hashlib.sha256()
    with Path(path).open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            value.update(block)
    return value.hexdigest()


def read_json(path):
    return json.loads(Path(path).read_text(encoding="utf-8-sig"))


def local_path(value):
    path = (ROOT / value).resolve()
    if not path.is_relative_to(ROOT.resolve()):
        raise ValueError("Evidence/package path escapes repository")
    return path


def relative(path):
    return Path(path).resolve().relative_to(ROOT.resolve()).as_posix()


def record(path, value):
    path = Path(path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with path.open("x", encoding="utf-8", newline="\n") as stream:
        json.dump(value, stream, ensure_ascii=False, indent=2)
        stream.write("\n")


def source_paths():
    return sorted(p.relative_to(ROOT).as_posix() for scope in SCOPES for p in (ROOT / scope).rglob("*")
                  if p.is_file() and not any(part.endswith("~") for part in p.relative_to(ROOT).parts))


def validate_identity(value):
    for field, length in (("sourceCommit", 40), ("contentConfigSha256", 64), ("goldenCaseSetSha256", 64)):
        if not isinstance(value.get(field), str) or not re.fullmatch("[0-9a-f]{%d}" % length, value[field]):
            raise ValueError("Invalid build identity: " + field)
    if (value.get("releaseVersion") != VERSION or value.get("contentConfigVersion") != VERSION
            or type(value.get("saveSchemaVersion")) is not int or value["saveSchemaVersion"] != 4
            or value.get("unityEditorVersion") != "6000.6.7f1" or value.get("backend") != "Mono"):
        raise ValueError("Expected frozen v0.6/schema4/Unity 6000.6.7f1/Mono identity")
    return value


def validate_git_source(identity):
    commit = identity["sourceCommit"]
    subprocess.run(["git", "cat-file", "-e", commit + "^{commit}"], cwd=ROOT, check=True, capture_output=True)
    subprocess.run(["git", "merge-base", "--is-ancestor", commit, "HEAD"], cwd=ROOT, check=True, capture_output=True)
    subprocess.run(["git", "diff", "--quiet", commit, "--", *SCOPES, ":(exclude)" + IDENTITY], cwd=ROOT, check=True, capture_output=True)
    tracked = subprocess.check_output(["git", "ls-tree", "-r", "--name-only", "-z", commit, "--", *SCOPES], cwd=ROOT).decode("utf-8").split("\0")
    tracked = sorted(p for p in tracked if p and p != IDENTITY and not any(x.endswith("~") for x in p.split("/")))
    current = sorted(p for p in source_paths() if p != IDENTITY)
    if tracked != current:
        raise ValueError("Buildable file set differs from the actual frozen Git source tree")


def verify_nunit(path, mode, expected):
    if type(expected) is not int or expected <= 0:
        raise ValueError("NUnit gate count must be a positive integer")
    check_nunit(ROOT, relative(path), expected, [])
    root = ET.parse(path).getroot()
    leaves = root.findall(".//test-case")
    names = [row.attrib.get("fullname") for row in leaves]
    if (root.attrib.get("result") != "Passed" or len(leaves) != expected or None in names
            or len(set(names)) != expected or any(row.attrib.get("result") != "Passed" for row in leaves)):
        raise ValueError(mode + ": incomplete/non-passing NUnit report")
    if any(int(root.attrib.get(key, -1)) != expected for key in ("total", "passed", "testcasecount")):
        raise ValueError(mode + ": inconsistent NUnit totals")
    if any(int(root.attrib.get(key, 0)) for key in ("failed", "skipped", "inconclusive")):
        raise ValueError(mode + ": failed/skipped/inconclusive test")
    platforms = [row.attrib.get("value") for row in root.findall(".//property") if row.attrib.get("name") == "platform"]
    if set(platforms) != {mode}:
        raise ValueError(mode + ": wrong NUnit platform")
    return {"passed": expected, "failed": 0, "reportPath": relative(path), "reportSha256": sha(path)}


def validate_frozen_test_bindings(gate):
    """Validate frozen runtime/PDB/discovery evidence, not DLLs changed by a later platform switch."""
    rows = gate["files"]
    audit_entry = gate.get("pdbAudit")
    if not audit_entry or audit_entry not in rows:
        raise ValueError("Frozen gate is missing its bound PDB audit")
    audit = read_json(local_path(audit_entry["path"]))
    assemblies = audit.get("assemblies") or []
    if audit.get("mismatches") != 0 or len(assemblies) != len(ASSEMBLIES) or {row["name"] for row in assemblies} != ASSEMBLIES:
        raise ValueError("PDB audit does not cover the exact five passing project assemblies")
    for row in assemblies:
        if (row.get("pdbMatchesDll") is not True or type(row.get("projectDocuments")) is not int or row["projectDocuments"] <= 0
                or not re.fullmatch(r"[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}", row.get("mvid") or "")
                or any(not re.fullmatch("[0-9a-f]{64}", row.get(key) or "") for key in ("dllSha256", "pdbSha256"))):
            raise ValueError("Invalid frozen assembly/PDB identity")
    seen, group_counts = set(), {name: 0 for name in ASSEMBLIES}
    for document in audit.get("documents") or []:
        source = Path(document["source"])
        if not source.is_absolute():
            source = ROOT / "DicePrototype" / source
        source = source.resolve()
        expected = document.get("expectedChecksum") or ""
        if (not source.is_relative_to((ROOT / "DicePrototype/Assets/Game").resolve()) or source in seen
                or document.get("assembly") not in ASSEMBLIES or document.get("matches") is not True
                or not re.fullmatch(r"[0-9a-f]{40}|[0-9a-f]{64}", expected)):
            raise ValueError("Missing/duplicate/invalid frozen PDB source document")
        actual = hashlib.new("sha1" if len(expected) == 40 else "sha256", source.read_bytes()).hexdigest()
        if actual != expected or document.get("currentChecksum") != expected:
            raise ValueError("Frozen PDB document differs from current source: " + str(source))
        seen.add(source)
        group_counts[document["assembly"]] += 1
    all_cs = {path.resolve() for path in (ROOT / "DicePrototype/Assets/Game").rglob("*.cs") if not any(part.endswith("~") for part in path.parts)}
    if seen != all_cs or any(group_counts[row["name"]] != row["projectDocuments"] for row in assemblies):
        raise ValueError("Frozen PDB does not cover every current project C# file")
    expected_assemblies = sorted(({key: row[key] for key in ("name", "mvid", "dllSha256")} for row in assemblies), key=lambda row: row["name"])
    expected_files = sorted((row for row in rows if any(row["path"].startswith(scope + "/") for scope in SCOPES)
                            and row["path"] != IDENTITY and not row["path"].startswith(("DicePrototype/Assets/InitTestScene", "DicePrototype/Assets/Screenshots"))), key=lambda row: row["path"])
    for test in gate["tests"]:
        mode = test["mode"]
        discovery_path = "evidence/v06/unity/discovery-" + mode + ".json"
        if not any(row["path"] == discovery_path for row in rows):
            raise ValueError("Missing bound full Unity test discovery: " + mode)
        discovery = read_json(local_path(discovery_path))
        names = discovery.get("names") or []
        executed = sorted(row.attrib["fullname"] for row in ET.parse(local_path(test["report"]["path"])).getroot().findall(".//test-case"))
        if (discovery.get("mode") != mode or discovery.get("unity") != "6000.6.7f1" or not names
                or len(names) != len(set(names)) or executed != sorted(names) or test.get("names") != executed):
            raise ValueError("NUnit report is not the complete frozen Unity discovery: " + mode)
        binding_entry = test.get("binding")
        if not binding_entry or binding_entry not in rows or local_path(binding_entry["path"]) != local_path(test["report"]["path"]).with_suffix(".binding.json"):
            raise ValueError("Missing bound actual runtime test snapshot: " + mode)
        binding = read_json(local_path(binding_entry["path"]))
        if binding.get("schema") != "v06-test-binding-1" or binding.get("report") != test["report"]:
            raise ValueError("Test binding belongs to a different report")
        for phase in ("started", "finished"):
            snapshot = binding.get(phase) or {}
            if (sorted(snapshot.get("assemblies") or [], key=lambda row: row["name"]) != expected_assemblies
                    or sorted(snapshot.get("files") or [], key=lambda row: row["path"]) != expected_files):
                raise ValueError("Actual loaded assemblies/source snapshot differs at test " + phase)


def validated_gate():
    identity = validate_identity(read_json(ROOT / IDENTITY))
    config = read_json(ROOT / CONFIG)
    if (identity["contentConfigSha256"] != sha(ROOT / CONFIG) or identity["goldenCaseSetSha256"] != sha(ROOT / GOLDEN)
            or config["configVersion"] != VERSION or config["saveSchemaVersion"] != 4):
        raise ValueError("Build identity differs from current configuration/golden cases")
    validate_git_source(identity)
    gate = read_json(ROOT / GATE)
    if gate.get("schema") != "v06-build-gate-1" or gate.get("identity") != identity or gate.get("sourceCommit") != identity["sourceCommit"]:
        raise ValueError("Test gate is bound to a different build identity")
    rows = gate.get("files") or []
    paths = [row["path"] for row in rows]
    if not paths or len(paths) != len(set(paths)):
        raise ValueError("Empty/duplicate build gate files")
    for row in rows:
        path = local_path(row["path"])
        if not path.is_file() or sha(path) != row["sha256"]:
            raise ValueError("Frozen source/evidence changed: " + row["path"])
    bound_sources = sorted(p for p in paths if any(p.startswith(scope + "/") for scope in SCOPES))
    if source_paths() != bound_sources:
        raise ValueError("Buildable source set changed after passing Unity tests")
    tests = {}
    reports = gate.get("tests") or []
    for mode, count_key in (("EditMode", "editModePassed"), ("PlayMode", "playModePassed")):
        matches = [test for test in reports if test.get("mode") == mode]
        if len(matches) != 1 or matches[0].get("passed") != gate.get(count_key):
            raise ValueError("Missing/inconsistent " + mode + " gate")
        proof = matches[0]["report"]
        if proof not in rows:
            raise ValueError("NUnit report is not in the frozen gate file list")
        tests["unity" + mode] = verify_nunit(local_path(proof["path"]), mode, matches[0]["passed"])
    if len(reports) != 2:
        raise ValueError("Gate must contain exactly EditMode and PlayMode")
    validate_frozen_test_bindings(gate)
    return identity, gate, tests


def validate_build(path, platform, identity):
    value = read_json(path)
    raw = value.get("identity")
    embedded = json.loads(raw) if isinstance(raw, str) else raw
    expected_platform = "StandaloneOSX" if platform == "macos" else "StandaloneWindows64"
    expected_profile = "macOS_Universal.asset" if platform == "macos" else "Windows_x64.asset"
    if (value.get("result") != "Succeeded" or value.get("errors") != 0 or embedded != identity
            or value.get("platform") != expected_platform or value.get("unity") != identity["unityEditorVersion"]
            or not value.get("profile", "").endswith("/" + expected_profile) or not value.get("buildGuid") or not value.get("buildUtc")):
        raise ValueError(platform + ": build did not succeed with this frozen identity/profile")
    return value


def validate_windows_report(directory, identity, review_candidate=False, allow_failed_windows_performance=False):
    from macos.v06.compare_replays import validate_windows_reference, validate_trace_replay
    value = read_json(directory / "player-report.json")
    failures = validate_windows_reference(value, review_candidate, allow_failed_windows_performance)
    if failures or json.loads(value.get("buildIdentity") or "{}") != identity:
        raise ValueError("Windows Player verification is missing/stale/non-passing: " + repr(failures))
    validate_trace_replay(value, directory, ROOT / "evidence/v06/session/normal-run-trace.json", ROOT / "evidence/v06/session/debug-24-trace.json")
    return value


def candidate_directory(review_candidate=False):
    return REVIEW_RELEASE if review_candidate else RELEASE


def release_manifest_path(review_candidate=False):
    return ROOT / candidate_directory(review_candidate) / ("review-candidate-manifest.json" if review_candidate else "release-manifest.json")


def package_proof_name(platform, review_candidate=False):
    return platform + ("-package-review-candidate.json" if review_candidate else "-package-candidate.json")


def kit_proof_name(review_candidate=False):
    return "mac-verification-kit-review-candidate.json" if review_candidate else "mac-verification-kit.json"


def candidate_notice(identity, report=None, allow_failed_windows_performance=False):
    if allow_failed_windows_performance:
        from macos.v06.compare_replays import validate_windows_failed_performance_reference, review_reference_notice
        if not report or validate_windows_failed_performance_reference(report):
            raise ValueError("Diagnostic notice requires the actual validated failed-performance report")
        return {"schema": "v06-diagnostic-review-notice-1", "identity": identity, "reviewCandidate": True,
                **review_reference_notice(report, True)}
    return {"schema": "v06-restricted-review-notice-1", "identity": identity, "reviewCandidate": True,
            "windowsNativeDisplayStatus": "PENDING_P22_NOT_PASSED", "macRuntimeStatus": "NOT_RUN_ON_MAC",
            "fullWindowsPassed": False, "fullReleaseAccepted": False,
            "publication": "Test candidate in the user-authorized delivery channel; P22 pending and full release not accepted"}


def check_package(proof):
    path = local_path(proof["packagePath"])
    if path.stat().st_size != proof["byteSize"] or sha(path) != proof["sha256"]:
        raise ValueError("Package differs from its recorded bytes")
    with zipfile.ZipFile(path) as archive:
        if archive.testzip():
            raise ValueError("Package CRC failed")
    safe_archive_inventory(path)
    return path


def verification_kit(windows_results, review_candidate=False, allow_failed_windows_performance=False):
    identity, _, _ = validated_gate()
    report = validate_windows_report(windows_results, identity, review_candidate, allow_failed_windows_performance)
    mac_proof = read_json(ROOT / "evidence/v06/packages" / package_proof_name("macos", review_candidate))
    if (mac_proof.get("identity") != identity or bool(mac_proof.get("reviewCandidate")) != review_candidate
            or bool(mac_proof.get("allowFailedWindowsPerformance")) != allow_failed_windows_performance
            or allow_failed_windows_performance and mac_proof.get("windowsReportSha256") != sha(windows_results / "player-report.json")):
        raise ValueError("Mac transfer package belongs to another source")
    mac_package = check_package(mac_proof)
    output = ROOT / candidate_directory(review_candidate) / "DicePrototype-p0.6.0-MacVerificationKit.zip"
    prefix = "DicePrototype-MacVerification/"
    inputs = [(ROOT / "tools/macos/v06" / name, name) for name in
              ("VerifyMac.command", "verify_mac.py", "package_checks.py", "compare_replays.py")]
    inputs += [(ROOT / "docs/tasks/P0-V06-MAC-VERIFICATION.md", "执行说明.md"),
               (ROOT / IDENTITY, "inputs/BUILD_IDENTITY.json"), (mac_package, "inputs/macos.zip")]
    for name in ("normal-run-trace.json", "debug-24-trace.json"):
        inputs.append((ROOT / "evidence/v06/session" / name, "inputs/" + name))
    for name in ("player-report.json", "normal-replay.jsonl", "debug24-replay.jsonl", "cross-platform-checkpoint/active-run.json",
                 "cross-platform-checkpoint/command.json", "cross-platform-checkpoint/expected.json"):
        inputs.append((windows_results / name, "inputs/windows-results/" + name))
    if any(not path.is_file() for path, _ in inputs):
        raise ValueError("Verification kit input missing; no package was created")
    notice = json.dumps(candidate_notice(identity, report, allow_failed_windows_performance), ensure_ascii=False, indent=2).encode("utf-8") if review_candidate else None
    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(output, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path, name in inputs:
            archive.write(path, prefix + name, compress_type=zipfile.ZIP_STORED if name == "inputs/macos.zip" else zipfile.ZIP_DEFLATED)
        if review_candidate:
            archive.writestr(prefix + "inputs/review-candidate.json", notice)
    proof = {"identity": identity, "packagePath": relative(output), "sha256": sha(output), "byteSize": output.stat().st_size,
             "sourceCommit": identity["sourceCommit"], "windowsResults": relative(windows_results), "crc": "passed", "reviewCandidate": review_candidate,
             "inputs": [{"path": name, "sha256": sha(path)} for path, name in inputs], "macRuntimeStatus": "NOT_RUN_ON_MAC"}
    if allow_failed_windows_performance:
        proof.update(allowFailedWindowsPerformance=True, windowsReportSha256=sha(windows_results / "player-report.json"))
    if review_candidate:
        proof["inputs"].append({"path": "inputs/review-candidate.json", "sha256": hashlib.sha256(notice).hexdigest(), "generatedNotice": True})
    check_package(proof)
    record(ROOT / "evidence/v06/packages" / kit_proof_name(review_candidate), proof)
    return proof


def package(platform, build_report, review_candidate=False, allow_failed_windows_performance=False, windows_results=None):
    identity, _, _ = validated_gate()
    report = None
    if allow_failed_windows_performance:
        if not review_candidate or windows_results is None:
            raise ValueError("Diagnostic packaging requires --review-candidate and --windows-results")
        report = validate_windows_report(windows_results, identity, True, True)
    build = validate_build(build_report, platform, identity)
    mac = platform == "macos"
    directory = ROOT / ("Builds/v06/macOS" if mac else "Builds/v06/Windows")
    main = directory / ("DicePrototype.app" if mac else "DicePrototype.exe")
    required = main / "Contents/Info.plist" if mac else main
    if not required.is_file() or Path(build["output"]).resolve() != main.resolve():
        raise ValueError("Build report output does not match complete player directory")
    output = ROOT / candidate_directory(review_candidate) / ("DicePrototype-p0.6.0-macos-universal.zip" if mac else "DicePrototype-p0.6.0-win-x64.zip")
    output.parent.mkdir(parents=True, exist_ok=True)
    files = sorted(p for p in directory.rglob("*") if (p.is_file() or p.is_symlink())
                   and not any("BackUpThisFolder" in part or "BurstDebugInformation" in part for part in p.parts))
    native, members = [], []
    for path in files:
        name = path.relative_to(directory).as_posix()
        info = macho_info(path) if mac and not path.is_symlink() else None
        if info:
            if set(info["architectures"]) != {"arm64", "x86_64"}:
                raise ValueError("Not Universal: " + name)
            native.append({"path": name, **info})
        mode = stat.S_IFREG | (0o755 if info and any(s["filetype"] == 2 for s in info["slices"]) else 0o644)
        if path.is_symlink():
            mode = stat.S_IFLNK | 0o777
        members.append((path, name, mode))
    if mac and not native:
        raise ValueError("Mac build has no verified Universal native components")
    with zipfile.ZipFile(output, "x", compression=zipfile.ZIP_DEFLATED, compresslevel=6) as archive:
        for path, name, mode in members:
            item = zipfile.ZipInfo(name)
            item.create_system = 3
            item.external_attr = mode << 16
            item.compress_type = zipfile.ZIP_DEFLATED
            archive.writestr(item, os.readlink(path).encode("utf-8") if path.is_symlink() else path.read_bytes())
        archive.writestr("BUILD_IDENTITY.json", json.dumps(identity, ensure_ascii=False, indent=2))
        archive.write(ROOT / "DicePrototype/Assets/Game/Fonts/LICENSE.txt", "Licenses/NotoSansCJK-OFL.txt")
        archive.write(ROOT / "docs/PLAYER_README_V06.txt", "开始游玩.txt")
        if review_candidate:
            archive.writestr("REVIEW_CANDIDATE.json", json.dumps(candidate_notice(identity, report, allow_failed_windows_performance), ensure_ascii=False, indent=2))
    with zipfile.ZipFile(output) as archive:
        records = [{"path": item.filename, "byteSize": item.file_size, "sha256": hashlib.sha256(archive.read(item)).hexdigest(),
                    "zipMode": oct(item.external_attr >> 16)} for item in archive.infolist()]
    proof = {"identity": identity, "platform": platform, "packagePath": relative(output), "sha256": sha(output), "reviewCandidate": review_candidate,
             "byteSize": output.stat().st_size, "files": records, "nativeComponents": native, "crc": "passed",
             "buildReport": {"path": relative(build_report), "sha256": sha(build_report), "buildGuid": build["buildGuid"]},
             "acceptance": "pending_player_and_fresh_remote_restore",
             "macOSPackaging": "Windows-created transfer candidate; macOS ditto/fresh-extraction/native-M5-run pending" if mac else None}
    if allow_failed_windows_performance:
        proof.update(allowFailedWindowsPerformance=True, windowsReportSha256=sha(windows_results / "player-report.json"))
    check_package(proof)
    record(ROOT / "evidence/v06/packages" / package_proof_name(platform, review_candidate), proof)
    return proof


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("platform", choices=("windows", "macos", "kit"))
    parser.add_argument("--windows-results", type=Path)
    parser.add_argument("--build-report", type=Path)
    parser.add_argument("--review-candidate", action="store_true", help="Separate restricted candidate paths; does not claim full Windows acceptance")
    parser.add_argument("--allow-failed-windows-performance", action="store_true", help="Diagnostic review only; requires actual failed Windows results and explicitly labels performance FAILED")
    args = parser.parse_args()
    if args.platform == "kit":
        if args.windows_results is None:
            parser.error("kit requires --windows-results")
        proof = verification_kit(args.windows_results.resolve(), args.review_candidate, args.allow_failed_windows_performance)
    else:
        if args.build_report is None:
            parser.error("player package requires --build-report")
        proof = package(args.platform, args.build_report.resolve(), args.review_candidate, args.allow_failed_windows_performance,
                        args.windows_results.resolve() if args.windows_results else None)
    print(json.dumps({key: proof[key] for key in ("packagePath", "sha256", "byteSize", "crc")}, ensure_ascii=False))


if __name__ == "__main__":
    main()
