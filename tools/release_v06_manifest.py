"""Bind the three v0.6 candidate ZIPs to real Unity and Windows Player evidence."""
import argparse
import hashlib
import json
from pathlib import Path

from package_v06 import (ROOT, RELEASE, GATE, VERSION, TAG, read_json, sha, record, relative,
                         validated_gate, validate_build, validate_windows_report, check_package,
                         package_proof_name, kit_proof_name, release_manifest_path, candidate_notice)
from macos.v06.compare_replays import validate_recovery, review_reference_notice, failed_performance_notice


def create_manifest(windows_results, windows_build, macos_build, display_recovery=None, review_candidate=False, allow_failed_windows_performance=False):
    identity, _, tests = validated_gate()
    report = validate_windows_report(windows_results, identity, review_candidate, allow_failed_windows_performance)
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
        proof_path = ROOT / "evidence/v06/packages" / package_proof_name(platform, review_candidate)
        proof = read_json(proof_path)
        if (proof.get("identity") != identity or proof.get("buildReport", {}).get("sha256") != sha(build_path)
                or bool(proof.get("reviewCandidate")) != review_candidate
                or bool(proof.get("allowFailedWindowsPerformance")) != allow_failed_windows_performance
                or allow_failed_windows_performance and proof.get("windowsReportSha256") != sha(windows_results / "player-report.json")):
            raise ValueError(platform + ": package proof differs from actual build report")
        package = check_package(proof)
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
            "smokeStatus": ("GAMEPLAY_UI_ONLY_PERFORMANCE_FAILED_NATIVE_PENDING" if allow_failed_windows_performance else
                            "AUTOMATED_GAMEPLAY_UI_PERF_ONLY_NATIVE_DISPLAY_PENDING" if review_candidate else "AUTOMATED_WINDOWS_PASSED") if platform == "windows" else "NOT_RUN_ON_MAC",
            "packagingStatus": "CRC and safe archive paths verified; fresh remote extraction/runtime verified separately" if platform == "windows" else
                               "Windows-created transfer candidate; native ditto/fresh-extraction/Metal/M5 run pending",
            "intelRuntimeStatus": "not_applicable" if platform == "windows" else "NOT_TESTED: x86_64 slice included; runtime unverified"})
    kit = read_json(ROOT / "evidence/v06/packages" / kit_proof_name(review_candidate))
    if (kit.get("identity") != identity or kit.get("windowsResults") != relative(windows_results)
            or bool(kit.get("reviewCandidate")) != review_candidate
            or bool(kit.get("allowFailedWindowsPerformance")) != allow_failed_windows_performance):
        raise ValueError("M5 kit identity/Windows reference does not match this manifest")
    check_package(kit)
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
    perf = report["performance"]
    manifest = {"schema": "v06-restricted-review-candidate-1" if review_candidate else "v06-release-candidate-1", **identity, "releaseTag": TAG,
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
            "Password/debug32/controller-drag/display-confirmation manual review", "Fresh GitHub asset download and extracted Windows run", "Human playtesting"],
        "note": "sourceCommit identifies the real frozen gameplay Git tree. Packaging and automated Windows tests do not sign off Mac or human acceptance."}
    if review_candidate:
        manifest["windowsReference"] = review_reference_notice(report, allow_failed_windows_performance)
        manifest["deliveryAuthorization"] = "User-authorized GitHub test-candidate channel; explicit review switches prevent accidental scope claims, not an additional user approval"
        manifest["requiredBeforeFullAcceptance"].insert(0, "Windows P22 native display and display-state isolation, including independent recovery when pending")
    if allow_failed_windows_performance:
        manifest.update(schema="v06-diagnostic-review-candidate-1", allowFailedWindowsPerformance=True,
                        releaseStatus="DIAGNOSTIC_TEST_CANDIDATE_WINDOWS_PERFORMANCE_FAILED_NATIVE_PENDING",
                        windowsPerformance=failed_performance_notice(report))
        manifest["windowsPerformance"]["originalPerformanceReport"] = perf
        manifest["requiredBeforeFullAcceptance"].insert(0, "Windows performance FAILED: repeat the unchanged 180-second/P95<=16.7ms condition with valid focus/timing evidence")
        manifest = {"diagnosticSummary": review_reference_notice(report, True), **manifest}
    target = release_manifest_path(review_candidate)
    record(target, manifest)
    return target


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--windows-results", required=True, type=Path)
    parser.add_argument("--windows-build", required=True, type=Path)
    parser.add_argument("--macos-build", required=True, type=Path)
    parser.add_argument("--display-recovery", type=Path, help="Actual Windows writer/reader shared evidence directory; required for the formal path")
    parser.add_argument("--review-candidate", action="store_true", help="Separate review-only manifest with explicit Windows native limitation, not a formal pass")
    parser.add_argument("--allow-failed-windows-performance", action="store_true", help="Diagnostic review only: retain completed Windows performance failure as FAILED")
    args = parser.parse_args()
    if args.display_recovery is None and not args.review_candidate:
        parser.error("Formal manifest requires --display-recovery")
    print(create_manifest(args.windows_results.resolve(), args.windows_build.resolve(), args.macos_build.resolve(),
                          args.display_recovery.resolve() if args.display_recovery else None, args.review_candidate, args.allow_failed_windows_performance))


if __name__ == "__main__":
    main()
