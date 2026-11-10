"""Bind the three v0.71 candidate ZIPs to real Unity and Windows Player evidence."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import stat
import zipfile

from package_v071 import (ROOT, GATE, TAG, read_json, sha, record, relative,
                         validated_gate, validate_build, validate_windows_report, check_package,
                         package_proof_name, kit_proof_name, candidate_notice, local_path, CONFIG,
                         package_directories, validate_release_tag, validate_proof_tag)
from macos.v071.compare_replays import (validate_recovery, review_reference_notice, failed_performance_notice,
                                      uses_strict_failed_performance_reference, diagnostic_release_status, validate_failed_performance_csv)
from macos.v071.package_checks import macho_info

PACKAGE_DIRECTORY = "Releases/p0.7.1/full-art-candidate"
PROOF_DIRECTORY = "evidence/v071-full/packages"
STAGED_MANIFEST = "DicePrototype/Assets/Art/Art071/FullSourceManifest.json"
FIRST_BATCH_COMMIT = "249d980f2ae3615d573df089403f298a6713ae96"
EXPECTED_NAMES = {"DicePrototype-p0.7.1-win-x64.zip", "DicePrototype-p0.7.1-macos-universal.zip", "DicePrototype-p0.7.1-MacVerificationKit.zip"}
REUSED_ILLUSTRATIONS = {"C03", "C29", "C48", "I07"}


def binding(path):
    path = local_path(path)
    if not path.is_file() or path.stat().st_size == 0:
        raise ValueError("Missing/empty evidence: " + str(path))
    return {"path": relative(path), "sha256": sha(path)}


def checked_binding(row):
    if not isinstance(row, dict) or binding(row.get("path", "")) != {k: row.get(k) for k in ("path", "sha256")}:
        raise ValueError("Evidence SHA/path changed")
    return local_path(row["path"])


def validate_directories(package_directory, proof_directory, release_tag=TAG):
    package_directories(True, package_directory, proof_directory, release_tag)
    packages, proofs = local_path(package_directory), local_path(proof_directory)
    old = local_path("Releases/p0.7.1/review-candidate")
    if (not packages.is_relative_to(local_path("Releases/p0.7.1"))
            or packages == local_path("Releases/p0.7.1") or packages == old or packages.is_relative_to(old)):
        raise ValueError("Full-art packages must use a separate new p0.7.1 directory, never the first-batch review-candidate")
    if proofs == local_path("evidence/v071/packages") or proofs.is_relative_to(local_path("evidence/v071/packages")):
        raise ValueError("Full-art package proofs must not overwrite the first-batch proof directory")
    return packages, proofs


def validate_menu_table(gate):
    """Bind the additional menu/table sources without changing the historical 127-asset catalog."""
    record_path = local_path("ArtSource/Art071/MenuTable/record.json")
    source = read_json(record_path)
    expected = {
        "ArtSource/Art071/MenuTable/menu-concept.png": "ArtSource/Art071/MenuTable/menu-concept-prompt.txt",
        "ArtSource/Art071/MenuTable/table-background.png": "ArtSource/Art071/MenuTable/table-background-prompt.txt",
    }
    rows = source.get("sources") or []
    if len(rows) != 2 or {row.get("path") for row in rows} != set(expected):
        raise ValueError("Menu/table release requires both actual Image2 sources")
    records = []
    for row in rows:
        path = local_path(row["path"])
        if sha(path) != row.get("sha256") or row.get("prompt") != expected[row["path"]]:
            raise ValueError("Menu/table source or prompt mapping changed")
        records.extend([binding(path), binding(row["prompt"])])
    runtime = "DicePrototype/Assets/Art/Art071/Backgrounds/bg_table_scene.png"
    background = next(row for row in rows if row["path"].endswith("table-background.png"))
    if background.get("runtime") != runtime or sha(local_path(runtime)) != background["sha256"]:
        raise ValueError("Menu/table source and runtime background differ")
    required = [runtime, runtime + ".meta", "DicePrototype/Assets/Art/Art071/Full/Materials/felt.mat",
                "DicePrototype/Assets/Game/Runtime/Shaders/Art07Felt.shader",
                "DicePrototype/Assets/Game/UI/GameApp.Art071Screens.cs",
                "DicePrototype/Assets/Game/Editor/V071TableSetup.cs"]
    frozen = {row["path"]: row["sha256"] for row in gate["files"]}
    for path in required:
        if frozen.get(path) != sha(local_path(path)):
            raise ValueError("Menu/table runtime input missing from current gate: " + path)
    return {"record": binding(record_path), "sourcesAndPrompts": records,
            "runtimeInputs": [binding(p) for p in required], "humanArtAccepted": False}


def validate_illustration_source(row):
    identity = row["id"].removeprefix("illustration_").upper()
    source = local_path(row["source"])
    folder = local_path("ArtSource/Art071/IllustrationsFull/" + identity)
    record_path = folder / "record.json"
    if row.get("kind") == "illustration":
        record = read_json(record_path)
        recorded_ids = [record[key] for key in ("id", "contentId") if key in record]
        raw = record.get("path") or record.get("file") or record.get("originalPath")
        selected = Path(raw) if raw else folder / "original.png"
        if not selected.is_absolute():
            selected = ROOT / selected if len(selected.parts) > 1 else folder / selected
        if (not recorded_ids or any(value != identity for value in recorded_ids)
                or not source.is_relative_to(folder) or source != selected.resolve()
                or record.get("sha256") != row["sha256"]):
            raise ValueError("Illustration ID/selected source/record mismatch: " + identity)
        return binding(record_path)
    if row.get("kind") == "illustration_reused" and identity in REUSED_ILLUSTRATIONS:
        catalog_path = local_path("ArtSource/Art071/Illustrations/manifest.json")
        catalog = read_json(catalog_path)
        matches = [r for r in catalog["assets"] if r.get("contentId") == identity]
        expected = local_path("ArtSource/Art071/Illustrations/illustration_" + identity.lower() + ".png")
        if (record_path.exists() or source != expected or len(matches) != 1
                or local_path(matches[0]["path"]) != source or matches[0]["sha256"] != row["sha256"]):
            raise ValueError("Reused illustration does not match the explicit four-source catalog: " + identity)
        return binding(catalog_path)
    raise ValueError("Unknown/reused non-authorized illustration source: " + identity)


def validate_full_art(identity, gate, review_path):
    """Bytes and scope are machine checked; signed visual conclusions remain human/agent evidence."""
    if identity["sourceCommit"] == FIRST_BATCH_COMMIT:
        raise ValueError("First-batch 249d980 packages cannot be published as the full-art release")
    config = read_json(ROOT / CONFIG)
    ids = [r["id"] for kind in ("controllers", "consumables", "services", "diceUpgrades") for r in config[kind]]
    if len(ids) != 71 or len(set(ids)) != 71:
        raise ValueError("Full-art scope must match all 71 active Config IDs")
    expected = {"illustration_" + ident.lower() for ident in ids}
    catalogs = []
    required_sources = {}
    for name, count in (("UI",43),("Surfaces",12)):
        path = ROOT / "ArtSource/Art071/Full" / name / "manifest.json"
        catalog = read_json(path)
        rows = catalog.get("assets") or []
        if len(rows) != count or len({r["id"] for r in rows}) != count or expected & {r["id"] for r in rows}:
            raise ValueError("Incomplete/duplicate public source catalog: " + name)
        for row in rows:
            if sha(local_path(row["export"])) != row["sha256"]:
                raise ValueError("Public export changed: " + row["id"])
            required_sources[row["id"]] = row["export"]
        expected.update(r["id"] for r in rows)
        catalogs.append(binding(path))
    expected.add("bg_felt_tile")
    required_sources["bg_felt_tile"] = "ArtSource/Art071/Full/Backgrounds/bg_felt_tile.png"
    staged = read_json(ROOT / STAGED_MANIFEST)
    rows = staged.get("assets") or []
    if (staged.get("complete") is not True or staged.get("missing") != [] or len(rows) != 127
            or {r["id"] for r in rows} != expected):
        raise ValueError("Staging must contain exactly 71 illustrations + 43 public + 12 surfaces + 1 background")
    frozen = {row["path"]: row["sha256"] for row in gate["files"]}
    if frozen.get(STAGED_MANIFEST) != sha(ROOT / STAGED_MANIFEST):
        raise ValueError("FullSourceManifest was not in the passing source gate")
    seen_runtime = set(); illustration_hashes = set(); illustration_records = {}
    for row in rows:
        source = local_path(row["source"])
        runtime = local_path("DicePrototype/" + row["runtime"])
        if (not source.is_relative_to(local_path("ArtSource"))
                or not runtime.is_relative_to(local_path("DicePrototype/Assets/Art/Art071"))
                or runtime in seen_runtime or sha(source) != row["sha256"] or sha(runtime) != row["sha256"]
                or frozen.get(relative(runtime)) != row["sha256"]):
            raise ValueError("Full-art source/runtime/gate binding failed: " + row["id"])
        if row["id"] in required_sources and row["source"] != required_sources[row["id"]]:
            raise ValueError("Staged public asset uses a different source: " + row["id"])
        if row["id"].startswith("illustration_") and (row.get("kind") not in ("illustration", "illustration_reused")
                or runtime.name != row["id"] + ".png"):
            raise ValueError("Illustration mapping mismatch: " + row["id"])
        if row["id"].startswith("illustration_"):
            illustration_records[row["id"]] = validate_illustration_source(row)
            if row["sha256"] in illustration_hashes:
                raise ValueError("Different illustration IDs reuse identical artwork bytes: " + row["id"])
            illustration_hashes.add(row["sha256"])
        seen_runtime.add(runtime)
    review = read_json(review_path)
    if (review.get("schema") != "v071-full-art-review-1" or review.get("status") != "full_art_review_complete"
            or review.get("identity") != identity or review.get("stagedManifest") != binding(ROOT / STAGED_MANIFEST)
            or review.get("unresolvedBlockingArtDefects") != [] or review.get("humanArtAccepted") is not False):
        raise ValueError("Full-art runtime/independent review is incomplete or belongs to another source")
    evidence = review.get("evidence") or []
    if not {"unity-import", "runtime-visual", "independent-review"}.issubset({r.get("kind") for r in evidence}):
        raise ValueError("Full-art review needs real Unity import, runtime visual and independent review files")
    if len({r.get("path") for r in evidence}) != len(evidence):
        raise ValueError("Duplicate art review evidence")
    for row in evidence:
        checked_binding(row)
    return {"scope": "all_71_active_content_and_public_v071_art", "activeIds": ids,
            "illustrationCount":71, "newPublicResourceCount":56, "stagedManifest":binding(ROOT / STAGED_MANIFEST),
            "sourceCatalogs":catalogs, "illustrationRecords":illustration_records, "review":binding(review_path), "evidence":evidence,
            "reusedResources": "Previously qualified paper family and ceramic mother model remain covered by the source gate and this art review",
            "humanArtAccepted":False}


def validate_zip_proof(proof, identity, package_directory, notice=None, kit=False):
    path = check_package(proof)
    if path.parent != package_directory or path.name not in EXPECTED_NAMES or proof.get("identity") != identity:
        raise ValueError("Unexpected full-art package directory/name/identity")
    with zipfile.ZipFile(path) as archive:
        prefix = "DicePrototype-MacVerification/" if kit else ""
        rows = proof.get("inputs" if kit else "files") or []
        names = {prefix + row["path"] for row in rows}
        if len(names) != len(rows) or names != set(archive.namelist()):
            raise ValueError("ZIP proof does not enumerate every exact member")
        for row in rows:
            name = prefix + row["path"]
            info = archive.getinfo(name)
            with archive.open(name) as stream:
                actual = hashlib.file_digest(stream, "sha256").hexdigest()
            if actual != row["sha256"] or (not kit and (info.file_size != row["byteSize"] or oct(info.external_attr >> 16) != row["zipMode"])):
                raise ValueError("ZIP member differs from package proof: " + name)
        identity_name = prefix + ("inputs/BUILD_IDENTITY.json" if kit else "BUILD_IDENTITY.json")
        required = {identity_name}
        if not kit:
            required.update(("Licenses/NotoSansCJK-OFL.txt", "Licenses/NotoSansSC-Bold-OFL.txt", "Licenses/NotoSansSC-Bold-COPYRIGHT.txt", "开始游玩.txt"))
        if notice is not None:
            required.add(prefix + ("inputs/review-candidate.json" if kit else "REVIEW_CANDIDATE.json"))
        if not required.issubset(names):
            raise ValueError("ZIP omitted identity, limitation notice, README or font licenses")
        if json.loads(archive.read(identity_name)) != identity:
            raise ValueError("ZIP embedded identity differs")
        if notice is not None:
            name = prefix + ("inputs/review-candidate.json" if kit else "REVIEW_CANDIDATE.json")
            if json.loads(archive.read(name)) != notice:
                raise ValueError("ZIP omitted/changed actual diagnostic limitation notice")
        if not kit:
            for source, name in (("DicePrototype/Assets/Game/Fonts/LICENSE.txt","Licenses/NotoSansCJK-OFL.txt"),
                                 ("ArtSource/Art071/Fonts/LICENSE.txt","Licenses/NotoSansSC-Bold-OFL.txt"),
                                 ("ArtSource/Art071/Fonts/COPYRIGHT.txt","Licenses/NotoSansSC-Bold-COPYRIGHT.txt"),
                                 ("docs/PLAYER_README_V071.txt","开始游玩.txt")):
                if hashlib.sha256(archive.read(name)).hexdigest() != sha(ROOT / source):
                    raise ValueError("Player metadata/required font license mismatch: " + name)
    return path


def validate_packaged_build(proof, build, platform):
    """Compare the complete local build, including actual Mach-O slices/modes, with its ZIP proof."""
    directory = Path(build["output"]).resolve().parent
    expected_root = local_path("Builds/v071/macOS" if platform=="macos" else "Builds/v071/Windows")
    if directory != expected_root:
        raise ValueError("Build report is outside the canonical v071 player directory")
    files = sorted(p for p in directory.rglob("*") if (p.is_file() or p.is_symlink())
                   and not any("BackUpThisFolder" in part or "BurstDebugInformation" in part for part in p.parts))
    rows = {r["path"]:r for r in proof["files"]}
    metadata = {"BUILD_IDENTITY.json", "Licenses/NotoSansCJK-OFL.txt", "Licenses/NotoSansSC-Bold-OFL.txt", "Licenses/NotoSansSC-Bold-COPYRIGHT.txt", "开始游玩.txt", "REVIEW_CANDIDATE.json"}
    if {p.relative_to(directory).as_posix() for p in files} != set(rows)-metadata:
        raise ValueError("ZIP does not contain the complete current build file set")
    native = []
    for path in files:
        name = path.relative_to(directory).as_posix()
        info = macho_info(path) if platform=="macos" and not path.is_symlink() else None
        if info:
            if set(info["architectures"]) != {"arm64","x86_64"}:
                raise ValueError("Mac native component is not Universal: " + name)
            native.append({"path":name, **info})
        mode = stat.S_IFREG | (0o755 if info and any(s["filetype"]==2 for s in info["slices"]) else 0o644)
        if path.is_symlink():
            mode = stat.S_IFLNK | 0o777
            actual = hashlib.sha256(os.readlink(path).encode("utf8")).hexdigest()
        else:
            actual = sha(path)
        if rows[name]["sha256"] != actual or rows[name]["zipMode"] != oct(mode):
            raise ValueError("ZIP differs from actual build bytes/native permission: " + name)
    if proof.get("nativeComponents") != native or platform=="macos" and not native:
        raise ValueError("Native component inventory differs from the actual build")


def build_manifest(windows_results, windows_build, macos_build, art_review_evidence, display_recovery=None,
                   review_candidate=False, allow_failed_windows_performance=False,
                   package_directory=PACKAGE_DIRECTORY, package_proof_directory=PROOF_DIRECTORY, release_tag=TAG):
    if allow_failed_windows_performance and not review_candidate:
        raise ValueError("Failed performance requires an explicit review candidate")
    validate_release_tag(release_tag)
    package_directory, package_proof_directory = validate_directories(package_directory, package_proof_directory, release_tag)
    identity, gate, tests = validated_gate()
    full_art = validate_full_art(identity, gate, local_path(art_review_evidence))
    menu_table = validate_menu_table(gate) if release_tag != TAG else None
    report = validate_windows_report(windows_results, identity, review_candidate, allow_failed_windows_performance)
    strict_diagnostic = allow_failed_windows_performance and uses_strict_failed_performance_reference(report)
    if display_recovery is not None:
        recovery = validate_recovery(display_recovery, identity)
        if recovery["environment"].get("platform") != "WindowsPlayer":
            raise ValueError("Manifest requires actual Windows separate-process recovery evidence")
        recovery["files"] = [{"path": relative(row["path"]), "sha256": row["sha256"]} for row in recovery["files"]]
    elif review_candidate:
        recovery = {"status": "not_executed_native_display_pending", "files": [],
                    "note": "Not a pass. An independently passing recovery check may be supplied; it cannot establish full native-display acceptance."}
    else:
        raise ValueError("Formal manifest requires actual Windows separate-process recovery evidence")
    platforms = []
    for platform, build_path, label, arch in (("windows", windows_build, "Windows", "x86_64"),
                                            ("macos", macos_build, "macOS", "arm64+x86_64")):
        build = validate_build(build_path, platform, identity)
        proof_path = package_proof_directory / package_proof_name(platform, review_candidate)
        proof = read_json(proof_path)
        validate_proof_tag(proof, release_tag)
        if (proof.get("identity") != identity or proof.get("buildReport", {}).get("sha256") != sha(build_path)
                or bool(proof.get("reviewCandidate")) != review_candidate
                or bool(proof.get("allowFailedWindowsPerformance")) != allow_failed_windows_performance
                or allow_failed_windows_performance and proof.get("windowsReportSha256") != sha(windows_results / "player-report.json")):
            raise ValueError(platform + ": package proof differs from actual build report")
        package = validate_zip_proof(proof, identity, package_directory,
                                     candidate_notice(identity,report,allow_failed_windows_performance) if review_candidate else None)
        validate_packaged_build(proof,build,platform)
        if platform == "windows" and report.get("environment", {}).get("buildGuid") != build["buildGuid"]:
            raise ValueError("Windows probe did not run this actual build GUID")
        if platform == "windows" and recovery["status"] == "passed" and recovery["environment"].get("buildGuid") != build["buildGuid"]:
            raise ValueError("Windows separate-process display recovery ran another build GUID")
        platforms.append({"platform": label, "architecture": arch, "backend": identity["backend"], "profile": build["profile"],
            "packagePath": relative(package), "sha256": sha(package), "byteSize": package.stat().st_size,
            "buildUtc": build["buildUtc"], "buildGuid": build["buildGuid"], "buildWarnings": build["warnings"],
            "buildReport": {"path": relative(build_path), "sha256": sha(build_path)},
            "packageEvidence": {"path": relative(proof_path), "sha256": sha(proof_path)},
            "signingStatus": "No signing claim: inspect actual platform signature evidence",
            "smokeStatus": (("GAMEPLAY_UI_NATIVE_AUTOMATED_PERFORMANCE_FAILED" if strict_diagnostic else "GAMEPLAY_UI_ONLY_PERFORMANCE_FAILED_NATIVE_PENDING") if allow_failed_windows_performance else
                            "AUTOMATED_GAMEPLAY_UI_PERF_ONLY_NATIVE_DISPLAY_PENDING" if review_candidate else "AUTOMATED_WINDOWS_PASSED") if platform == "windows" else "NOT_RUN_ON_MAC",
            "packagingStatus": "CRC and safe archive paths verified; fresh remote extraction/runtime pending separate verification" if platform == "windows" else
                               "Windows-created transfer candidate; native ditto/fresh-extraction/Metal/M5 run pending",
            "intelRuntimeStatus": "not_applicable" if platform == "windows" else "NOT_TESTED: x86_64 slice included; runtime unverified"})
    kit_proof_path = package_proof_directory / kit_proof_name(review_candidate)
    kit = read_json(kit_proof_path)
    validate_proof_tag(kit, release_tag)
    if (kit.get("identity") != identity or kit.get("windowsResults") != relative(windows_results)
            or bool(kit.get("reviewCandidate")) != review_candidate
            or bool(kit.get("allowFailedWindowsPerformance")) != allow_failed_windows_performance
            or allow_failed_windows_performance and kit.get("windowsReportSha256") != sha(windows_results / "player-report.json")):
        raise ValueError("M5 kit identity/Windows reference does not match this manifest")
    validate_zip_proof(kit, identity, package_directory,
                       candidate_notice(identity,report,allow_failed_windows_performance) if review_candidate else None, kit=True)
    trace_inputs = {item["path"]: item["sha256"] for item in kit["inputs"]}
    if review_candidate and trace_inputs.get("inputs/review-candidate.json") != hashlib.sha256(
            json.dumps(candidate_notice(identity, report, allow_failed_windows_performance), ensure_ascii=False, indent=2).encode("utf-8")).hexdigest():
        raise ValueError("Restricted M5 kit is missing its explicit reference-only notice")
    for name, key in (("normal-run-trace.json", "traceSha256"), ("debug-24-trace.json", "debugTraceSha256")):
        if trace_inputs.get("inputs/" + name) != report[key]:
            raise ValueError("M5 kit is missing the same verified " + name)
    if trace_inputs.get("inputs/windows-results/player-report.json") != sha(windows_results / "player-report.json"):
        raise ValueError("M5 kit Player report changed")
    if trace_inputs.get("inputs/macos.zip") != platforms[1]["sha256"]:
        raise ValueError("M5 kit contains a different Mac transfer package")
    raw_proof = validate_failed_performance_csv(report, windows_results) if strict_diagnostic else None
    if raw_proof and trace_inputs.get("inputs/windows-results/performance-frames.csv") != raw_proof["sha256"]:
        raise ValueError("M5 kit is missing the bound strict diagnostic raw CSV")
    perf = report["performance"]
    manifest = {"schema": "v071-full-art-restricted-review-candidate-1" if review_candidate else "v071-full-art-release-candidate-1", **identity, "releaseTag": release_tag,
        "reviewCandidate": review_candidate, "fullReleaseAccepted": False,
        "buildUtc": max(item["buildUtc"] for item in platforms), "releaseStatus": "RESTRICTED_TEST_CANDIDATE_WINDOWS_NATIVE_PENDING" if review_candidate else "CANDIDATE_MAC_AND_HUMAN_VALIDATION_PENDING",
        "deliveryChannel": "GitHub Release: three complete ZIP attachments; no Git LFS dependency", "platforms": platforms,
        "tests": tests, "buildGate": {"path": GATE, "sha256": sha(ROOT / GATE)},
        "ruleReproduction": {"normalSeed": report["normalReplay"]["seed"], "normalSteps": report["normalReplay"]["completedSteps"],
            "normalOutcome": report["normalReplay"]["terminalPhase"], "normalFinalStage": report["normalReplay"]["terminalStage"],
            "normalTraceSha256": report["traceSha256"], "debugTraceSha256": report["debugTraceSha256"],
            "debug24Passed": report["debug24"]["passed"], "debugPurpose": "Transition integration, not progression/balance evidence",
            "crossPlatformStatus": "NOT_RUN_ON_MAC"},
        "windowsPerformance": {"sampleSeconds": perf["sampleSeconds"], "frames": perf["frameCount"],
            "p95FrameIntervalMs": perf["frameInterval"]["p95"], "thresholdMs": 16.7, "status": perf["status"]},
        "windowsEvidence": relative(windows_results), "windowsReportSha256": sha(windows_results / "player-report.json"),
        "windowsDisplayChecks": report["displayChecks"], "windowsSeparateProcessDisplayRecovery": recovery, "macVerificationKit": kit,
        "remoteRestoreStatus": "PENDING_FRESH_RELEASE_ASSET_DOWNLOAD_AND_WINDOWS_EXTRACTED_RUN",
        "requiredBeforeFullAcceptance": ["M5 native arm64/Metal run and screenshots", "Mac ditto repack and fresh-extraction run",
            "Actual Mac signature/quarantine/Gatekeeper outputs", "Windows-to-Mac checkpoint and identical normal/debug24 replay",
            "Returned Mac checkpoint replayed on Windows", "Physical Mac mouse/trackpad, focus/display changes and quit/resume",
            "Password/debug32/controller-drag/display-confirmation manual review", "Fresh GitHub asset download and extracted Windows run", "v0.71 art0.7.1 human review: readable 720p/150% UI, color assist, reduced motion, dice layers and sound", "Human playtesting"],
        "note": "sourceCommit identifies the real frozen gameplay Git tree. Packaging and automated Windows tests do not sign off Mac or human acceptance."}
    if review_candidate:
        manifest["windowsReference"] = review_reference_notice(report, allow_failed_windows_performance)
        manifest["deliveryAuthorization"] = "User-authorized GitHub test-candidate channel; explicit review switches prevent accidental scope claims, not an additional user approval"
        manifest["requiredBeforeFullAcceptance"].insert(0, "Windows physical focus/display removal remains untested; selected strict automated checks are not full native acceptance" if strict_diagnostic else "Windows P22 native display and display-state isolation, including independent recovery when pending")
    if allow_failed_windows_performance:
        manifest.update(schema="v071-full-art-diagnostic-review-candidate-1", allowFailedWindowsPerformance=True,
                        releaseStatus=diagnostic_release_status(report),
                        windowsPerformance=failed_performance_notice(report))
        manifest["windowsPerformance"]["originalPerformanceReport"] = perf
        if raw_proof: manifest["windowsPerformanceRawEvidence"] = raw_proof
        if strict_diagnostic and recovery["status"] != "passed":
            manifest["requiredBeforeFullAcceptance"].insert(0, "Windows separate-process display recovery pending; selected main report does not establish this check")
        manifest["requiredBeforeFullAcceptance"].insert(0, "Windows performance FAILED: repeat the unchanged 180-second/P95<=16.7ms condition with valid focus/timing evidence")
        manifest = {"diagnosticSummary": review_reference_notice(report, True), **manifest}
    manifest["fullArt"] = full_art
    if menu_table is not None:
        manifest["menuTable"] = menu_table
    manifest["manifestInputs"] = {"windowsResults":relative(windows_results), "windowsBuild":relative(windows_build),
        "macosBuild":relative(macos_build), "artReviewEvidence":relative(art_review_evidence),
        "displayRecovery":relative(display_recovery) if display_recovery else None,
        "packageDirectory":relative(package_directory), "packageProofDirectory":relative(package_proof_directory)}
    manifest["macVerificationKitEvidence"] = binding(kit_proof_path)
    return manifest


def create_manifest(*args, output=None, **kwargs):
    value = build_manifest(*args, **kwargs)
    target = local_path(output or (value["manifestInputs"]["packageDirectory"] + "/full-art-release-manifest.json"))
    record(target, value)
    return target


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--release-tag", default=TAG, type=validate_release_tag)
    parser.add_argument("--windows-results", required=True, type=Path)
    parser.add_argument("--windows-build", required=True, type=Path)
    parser.add_argument("--macos-build", required=True, type=Path)
    parser.add_argument("--art-review-evidence", required=True, type=Path)
    parser.add_argument("--package-directory", default=PACKAGE_DIRECTORY)
    parser.add_argument("--package-proof-directory", default=PROOF_DIRECTORY)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--display-recovery", type=Path, help="Actual Windows writer/reader shared evidence directory; required for the formal path")
    parser.add_argument("--review-candidate", action="store_true", help="Separate review-only manifest with explicit Windows native limitation, not a formal pass")
    parser.add_argument("--allow-failed-windows-performance", action="store_true", help="Diagnostic review only: retain completed Windows performance failure as FAILED")
    args = parser.parse_args()
    if args.display_recovery is None and not args.review_candidate:
        parser.error("Formal manifest requires --display-recovery")
    print(create_manifest(args.windows_results.resolve(), args.windows_build.resolve(), args.macos_build.resolve(),
                          args.art_review_evidence.resolve(), args.display_recovery.resolve() if args.display_recovery else None,
                          args.review_candidate, args.allow_failed_windows_performance,
                          args.package_directory,args.package_proof_directory,args.release_tag,output=args.output))


if __name__ == "__main__":
    main()
