"""Synthetic diagnostic-candidate boundaries; no Player run or acceptance is fabricated."""
import copy
import contextlib
import hashlib
import io
import json
from pathlib import Path
import sys
import unittest
from unittest.mock import patch

import test_review_candidate as fixtures
from test_mac_tools import IDENTITY, report
from test_delivery_tools import write_json
import package_v06 as packaging
import release_v06_manifest as manifesting
import publish_v06_release as publishing
import verify_v06_display_recovery as recovery_runner
from compare_replays import (validate_report, validate_windows_review_reference, validate_windows_reference,
                             validate_windows_failed_performance_reference, review_reference_notice,
                             compare_directories, failed_performance_notice)


def diagnostic(source=None):
    value = fixtures.partial(source)
    value.update(status="budget_exceeded", exitCode=3, failure=None)
    value["performance"].update(status="budget_exceeded", sampleSeconds=180.004603, frameBudgetMet=False,
        targetFrameMs=16.7, targetResolution="1920x1080", frameCount=9103, focusedSampleFrames=0,
        gpuInvalidSamples=1, gpuMissingSamples=264)
    value["performance"]["frameInterval"].update(available=True, p95=23.0348, samples=9103,
        expectedFrames=9103, validFrameCoverage=1)
    for key in ("handbook-independent-color-tab", "wheel-reach-AddC53", "wheel-reach-RemoteDieD031",
                "wheel-reach-Target_D032", "wheel-reach-OrderCardITEM_00014", "long-mimic-chain-destroyed-owner-ui"):
        if not any(row["id"] == key for row in value["v06Checks"]):
            value["v06Checks"].append({"id": key, "status": "passed"})
    return value


class DiagnosticReferenceTests(unittest.TestCase):
    def test_completed_failed_reference_is_explicit_and_never_mutated_or_passed(self):
        value = diagnostic(); before = copy.deepcopy(value)
        self.assertEqual([], validate_windows_reference(value, True, True))
        self.assertTrue(validate_report(value, "windows"))
        self.assertTrue(validate_windows_review_reference(value))
        self.assertTrue(validate_windows_reference(value, True))
        self.assertEqual(before, value)
        notice = review_reference_notice(value, True)
        self.assertEqual("FAILED", notice["windowsPerformance"]["acceptanceStatus"])
        self.assertEqual(0, notice["windowsPerformance"]["focusedSampleFrames"])
        self.assertEqual(1, notice["windowsPerformance"]["gpuInvalidSamples"])
        self.assertFalse(notice["fullWindowsPassed"]); self.assertFalse(notice["fullReleaseAccepted"])

    def test_diagnostic_switch_requires_review_and_only_exact_failed_run(self):
        with self.assertRaises(ValueError): validate_windows_reference(diagnostic(), False, True)
        self.assertTrue(validate_windows_failed_performance_reference(fixtures.partial()))
        for key, change in (("status", "passed"), ("status", "failed"), ("exitCode", 0), ("exitCode", True),
                            ("verificationScope", "display_only"), ("nativeDisplayDeferred", False), ("failure", "error")):
            value = diagnostic(); value[key] = change
            self.assertTrue(validate_windows_failed_performance_reference(value), (key, change))

    def test_every_actual_non_display_ui_check_is_required(self):
        source = diagnostic()
        self.assertEqual(15, len(source["v06Checks"]))
        for row in source["v06Checks"]:
            for bad in ("remove", "failed", "not_executed"):
                value = copy.deepcopy(source)
                if bad == "remove": value["v06Checks"] = [r for r in value["v06Checks"] if r["id"] != row["id"]]
                else: next(r for r in value["v06Checks"] if r["id"] == row["id"])["status"] = bad
                self.assertTrue(validate_windows_failed_performance_reference(value), (row["id"], bad))

    def test_incomplete_wrong_resolution_load_nan_or_changed_threshold_rejected(self):
        mutations = [lambda x: x.update(sampleSeconds=179.99), lambda x: x.update(sampleSeconds=float("nan")),
            lambda x: x.update(targetFrameMs=25), lambda x: x.update(targetResolution="2560x1440"),
            lambda x: x.update(targetResolutionMet=False), lambda x: x.update(controllers=9),
            lambda x: x.update(frameCount=0), lambda x: x.update(frameBudgetMet=True),
            lambda x: x["frameInterval"].update(p95=float("inf")), lambda x: x["frameInterval"].update(p95=16.7),
            lambda x: x["frameInterval"].update(samples=1), lambda x: x["frameInterval"].update(validFrameCoverage=.9)]
        for mutation in mutations:
            value = diagnostic(); mutation(value["performance"])
            self.assertTrue(validate_windows_failed_performance_reference(value))

    def test_missing_or_impossible_anomaly_context_rejected(self):
        for key in ("focusedSampleFrames", "gpuInvalidSamples", "gpuMissingSamples"):
            for bad in (None, -1, 9104, True, 0.5):
                value = diagnostic(); value["performance"][key] = bad
                self.assertTrue(validate_windows_failed_performance_reference(value), (key, bad))

    def test_unrelated_native_gameplay_error_or_missing_sections_still_rejected(self):
        mutations = [lambda x: x.update(stressCompleted=False), lambda x: x.update(checkpointExported=False),
            lambda x: x.update(unityErrors=["error"]), lambda x: x.update(handbookPureReadChecks=19),
            lambda x: x["normalReplay"].update(completedSteps=0), lambda x: x["debug24"].update(passed=False),
            lambda x: x["displayChecks"].append({"id": "mode-ExclusiveFullScreen", "status": "failed"})]
        for mutation in mutations:
            value = diagnostic(); mutation(value)
            self.assertTrue(validate_windows_failed_performance_reference(value))

    def test_publish_needs_all_three_explicit_switches_before_network(self):
        value = {"reviewCandidate": True, "allowFailedWindowsPerformance": True}
        for action in ("draft", "upload", "download", "inspect", "publish"):
            with self.assertRaises(ValueError): publishing.ensure_publish_allowed(value, action, action == "publish")
            publishing.ensure_publish_allowed(value, action, action == "publish", True)
        with self.assertRaises(ValueError): publishing.ensure_publish_allowed(value, "publish", False, True)
        with patch.object(sys, "argv", ["publish_v06_release.py", "publish", "--review-candidate", "--allow-review-candidate"]), \
                patch.object(publishing.subprocess, "check_output", return_value="https://github.com/XDwudi/game2.git\n"), \
                patch.object(publishing, "release_inputs", return_value=(value, {}, "c" * 40)), \
                patch.object(publishing.subprocess, "run") as credential, \
                patch.object(publishing.urllib.request, "build_opener") as network:
            with self.assertRaises(ValueError): publishing.main()
            credential.assert_not_called(); network.assert_not_called()


class DiagnosticMacTests(unittest.TestCase):
    setUp = fixtures.ReviewMacComparisonTests.setUp
    tearDown = fixtures.ReviewMacComparisonTests.tearDown
    write = fixtures.ReviewMacComparisonTests.write

    def test_windows_failure_is_reference_only_mac_performance_native_and_trace_remain_strict(self):
        self.write(self.windows, "player-report.json", diagnostic())
        self.assertFalse(compare_directories(self.windows, self.mac, True)["passed"])
        result = compare_directories(self.windows, self.mac, True, True)
        self.assertTrue(result["passed"])
        self.assertEqual("FAILED", result["windowsReference"]["windowsPerformance"]["acceptanceStatus"])
        for mutate in (lambda x: x["performance"]["frameInterval"].update(p95=23),
                       lambda x: x.update(displayChecks=[]), lambda x: x["debug24"].update(passed=False)):
            value = report(True); mutate(value); self.write(self.mac, "player-report.json", value)
            self.assertFalse(compare_directories(self.windows, self.mac, True, True)["passed"])


class DiagnosticTraceTests(unittest.TestCase):
    setUp = fixtures.ReviewTraceTests.setUp
    tearDown = fixtures.ReviewTraceTests.tearDown
    save_rows = fixtures.ReviewTraceTests.save_rows

    def test_failed_budget_flag_does_not_allow_truncated_actual_input_trace(self):
        self.report = diagnostic(self.report)
        write_json(self.root / "evidence/v06/session/normal-run-trace.json", self.normal)
        write_json(self.root / "evidence/v06/session/debug-24-trace.json", self.debug)
        write_json(self.root / "player-report.json", self.report)
        before = (self.root / "player-report.json").read_bytes()
        with patch.object(packaging, "ROOT", self.root):
            self.assertEqual("budget_exceeded", packaging.validate_windows_report(self.root, IDENTITY, True, True)["status"])
            for flags in ((False, False), (True, False), (False, True)):
                with self.assertRaises(ValueError): packaging.validate_windows_report(self.root, IDENTITY, *flags)
            self.debug_rows.pop(); self.save_rows()
            with self.assertRaises(ValueError): packaging.validate_windows_report(self.root, IDENTITY, True, True)
        self.assertEqual(before, (self.root / "player-report.json").read_bytes())


class DiagnosticManifestTests(unittest.TestCase):
    tearDown = fixtures.ReviewManifestTests.tearDown
    make_zip = fixtures.ReviewManifestTests.make_zip

    def setUp(self):
        fixtures.ReviewManifestTests.setUp(self)
        self.report.clear(); self.report.update(diagnostic())
        self.report["normalReplay"].update(seed=42, terminalStage=8)
        self.report["environment"]["buildGuid"] = "windows-guid"
        write_json(self.windows / "player-report.json", self.report)
        for platform, proof in zip(("windows", "macos"), self.proofs):
            proof.update(allowFailedWindowsPerformance=True, windowsReportSha256=packaging.sha(self.windows / "player-report.json"))
            write_json(self.root / "evidence/v06/packages" / packaging.package_proof_name(platform, True), proof)
        self.kit.update(allowFailedWindowsPerformance=True)
        for row in self.kit["inputs"]:
            if row["path"] == "inputs/windows-results/player-report.json": row["sha256"] = packaging.sha(self.windows / "player-report.json")
            if row["path"] == "inputs/review-candidate.json":
                row["sha256"] = hashlib.sha256(json.dumps(packaging.candidate_notice(IDENTITY, self.report, True), ensure_ascii=False, indent=2).encode()).hexdigest()
        write_json(self.kit_path, self.kit)

    def create(self, allow=True):
        return manifesting.create_manifest(self.windows, self.build_paths["windows"], self.build_paths["macos"], None, True, allow)

    def test_manifest_preserves_failed_observations_exact_report_hash_and_distinct_schema(self):
        before = (self.windows / "player-report.json").read_bytes()
        result = packaging.read_json(self.create())
        self.assertEqual("v06-diagnostic-review-candidate-1", result["schema"])
        self.assertTrue(result["allowFailedWindowsPerformance"])
        self.assertEqual("FAILED", result["windowsPerformance"]["acceptanceStatus"])
        self.assertEqual(self.report["performance"], result["windowsPerformance"]["originalPerformanceReport"])
        self.assertEqual("GAMEPLAY_UI_ONLY_PERFORMANCE_FAILED_NATIVE_PENDING", result["platforms"][0]["smokeStatus"])
        self.assertEqual(hashlib.sha256(before).hexdigest(), result["windowsReportSha256"])
        self.assertEqual(before, (self.windows / "player-report.json").read_bytes())
        body_input = copy.deepcopy(result)
        body_input["tests"] = {"unityEditMode": {"passed": 1}, "unityPlayMode": {"passed": 1}, "syntheticOnly": True}
        self.assertIn("FAILED", publishing.release_body(body_input))
        self.assertIn("23.0348", publishing.release_body(body_input))
        self.assertIn("ACCEPTED", publishing.release_body(body_input))

    def test_wrong_package_scope_or_report_binding_is_rejected(self):
        with self.assertRaises(ValueError): self.create(False)
        proof = self.proofs[0]; proof["windowsReportSha256"] = "0" * 64
        write_json(self.root / "evidence/v06/packages" / packaging.package_proof_name("windows", True), proof)
        with self.assertRaises(ValueError): self.create()

    def test_publication_revalidates_failure_notice_and_original_report(self):
        path = self.create()
        with patch.object(publishing, "ROOT", self.root), \
                patch.object(publishing, "validated_gate", return_value=(IDENTITY, {}, {"syntheticTestsOnly": True})), \
                patch.object(publishing, "validate_windows_report", return_value=self.report), \
                patch.object(publishing.subprocess, "run") as git:
            git.return_value.returncode = 1
            publishing.release_inputs(True, True)
            with self.assertRaises(ValueError): publishing.release_inputs(True)
            value = packaging.read_json(path); value["windowsPerformance"]["acceptanceStatus"] = "PASSED"
            write_json(path, value)
            with self.assertRaises(ValueError): publishing.release_inputs(True, True)


class DiagnosticRecoveryTests(unittest.TestCase):
    setUp = fixtures.ReviewRecoveryRunnerTests.setUp
    tearDown = fixtures.ReviewRecoveryRunnerTests.tearDown

    def test_recovery_preflight_forwards_explicit_failure_selection_without_launch(self):
        self.report = diagnostic(self.report); self.args.allow_failed_windows_performance = True
        def validate(directory, identity, review=False, allow_failed=False):
            failures = validate_windows_reference(self.report, review, allow_failed)
            if failures: raise ValueError("reference rejected")
            return self.report
        with patch.object(recovery_runner, "ROOT", self.root), patch.object(recovery_runner.sys, "platform", "win32"), \
                patch.object(recovery_runner, "validate_identity", return_value=IDENTITY), \
                patch.object(recovery_runner, "validate_build", return_value={"buildGuid": "synthetic-guid"}), \
                patch.object(recovery_runner, "validate_windows_report", side_effect=validate), \
                patch.object(recovery_runner.subprocess, "Popen") as player:
            recovery_runner.prepare(self.args)
            self.args.allow_failed_windows_performance = False
            with self.assertRaises(ValueError): recovery_runner.prepare(self.args)
            player.assert_not_called()

    def test_real_recovery_protocol_can_use_failed_reference_without_claiming_windows_performance_pass(self):
        self.report = diagnostic(self.report); self.args.allow_failed_windows_performance = True
        shared = self.args.evidence_dir; raw = self.root / "artifacts/cache/v06/recovery"
        with patch.object(recovery_runner, "ROOT", self.root), \
                patch.object(recovery_runner, "prepare", return_value=(IDENTITY, {"buildGuid": "synthetic-guid"}, self.report, shared, raw)), \
                patch.object(recovery_runner, "run_writer", return_value={"pid": 1}), \
                patch.object(recovery_runner, "run_reader", return_value={"pid": 2}), \
                patch.object(recovery_runner, "check_actual_recovery", return_value={"status": "passed", "synthetic": True}), \
                contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(0, recovery_runner.execute(self.args))
        result = packaging.read_json(shared / "runner-summary.json")
        self.assertTrue(result["passed"])
        self.assertEqual("FAILED", result["windowsReference"]["windowsPerformance"]["acceptanceStatus"])
        self.assertFalse(result["windowsReference"]["fullWindowsPassed"])


if __name__ == "__main__": unittest.main()
