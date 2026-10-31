"""Offline restricted-candidate tests. No approval, package publication or native pass is created."""
import copy
import argparse
import contextlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
import zipfile
from unittest.mock import patch

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
import package_v06 as packaging
import publish_v06_release as publishing
import release_v06_manifest as manifesting
import verify_v06_display_recovery as recovery_runner
from compare_replays import (validate_report, validate_windows_review_reference, compare_directories,
                             review_reference_notice, REQUIRED_V06_CHECKS, REVIEW_STATUS, REVIEW_SCOPE)
import test_mac_tools as mac_fixtures
import test_delivery_tools as delivery_fixtures
from test_mac_tools import report, IDENTITY
from test_delivery_tools import write_json


def partial(source=None):
    value = copy.deepcopy(report(False) if source is None else source)
    value.update(status=REVIEW_STATUS, verificationScope=REVIEW_SCOPE, nativeDisplayDeferred=True)
    value["v06Checks"] = [r for r in value["v06Checks"] if r["id"] != "display-state-isolation"]
    value["displayChecks"] = [{"id": "native-display-verification-deferred", "status": "not_executed"}]
    return value


class ReviewReferenceTests(unittest.TestCase):
    def test_explicit_complete_partial_reference_validates_without_mutating_or_formal_pass(self):
        value = partial(); before = copy.deepcopy(value)
        self.assertEqual([], validate_windows_review_reference(value))
        self.assertTrue(validate_report(value, "windows"))
        self.assertEqual(before, value)
        self.assertFalse(review_reference_notice(value)["fullWindowsPassed"])
        self.assertFalse(review_reference_notice(value)["fullReleaseAccepted"])

    def test_every_non_display_check_including_both_p19_remains_required(self):
        for key in REQUIRED_V06_CHECKS - {"display-state-isolation"}:
            for mutation in ("missing", "failed"):
                with self.subTest(key=key, mutation=mutation):
                    value = partial()
                    if mutation == "missing": value["v06Checks"] = [r for r in value["v06Checks"] if r["id"] != key]
                    else: next(r for r in value["v06Checks"] if r["id"] == key)["status"] = "failed"
                    self.assertTrue(validate_windows_review_reference(value))

    def test_status_scope_defer_and_exitcode_are_exact(self):
        for key, change in (("status", "passed"), ("status", "failed"), ("verificationScope", "display_only_no_replay_or_stress"),
                            ("nativeDisplayDeferred", False), ("exitCode", 1), ("exitCode", False)):
            with self.subTest(key=key, change=change):
                value = partial(); value[key] = change
                self.assertTrue(validate_windows_review_reference(value))

    def test_native_failure_or_missing_defer_marker_is_not_relabelled_as_partial(self):
        for rows in ([], [{"id": "native-display-verification-deferred", "status": "failed"}],
                     [{"id": "native-display-verification-deferred", "status": "not_executed"}, {"id": "mode-ExclusiveFullScreen", "status": "failed"}],
                     [{"id": "native-display-verification-deferred", "status": "not_executed"}, {"id": "unknown-native-bypass", "status": "passed"}]):
            value = partial(); value["displayChecks"] = rows
            self.assertTrue(validate_windows_review_reference(value))
        value = partial(); value["v06Checks"].append({"id": "display-state-isolation", "status": "failed"})
        self.assertTrue(validate_windows_review_reference(value))

    def test_existing_pressure_and_replay_requirements_cannot_be_skipped(self):
        mutations = [lambda x: x["performance"].update(sampleSeconds=179.99),
                     lambda x: x["performance"]["frameInterval"].update(p95=16.71),
                     lambda x: x["performance"].update(controllers=9), lambda x: x.update(stressCompleted=False),
                     lambda x: x.update(unityErrors=["real error"]), lambda x: x.update(handbookPureReadChecks=19),
                     lambda x: x["normalReplay"].update(completedSteps=0), lambda x: x["debug24"].update(passed=False),
                     lambda x: x.update(checkpointExported=False)]
        for mutate in mutations:
            value = partial(); mutate(value)
            self.assertTrue(validate_windows_review_reference(value))

    def test_only_display_state_isolation_may_be_explicitly_unexecuted(self):
        value = partial(); value["v06Checks"].append({"id": "display-state-isolation", "status": "not_executed"})
        self.assertEqual([], validate_windows_review_reference(value))
        value["v06Checks"][0]["status"] = "not_executed"
        self.assertTrue(validate_windows_review_reference(value))

    def test_review_publish_needs_explicit_scope_switch(self):
        value = {"reviewCandidate": True}
        for action in ("draft", "upload", "download", "inspect"):
            publishing.ensure_publish_allowed(value, action)
        with self.assertRaises(ValueError): publishing.ensure_publish_allowed(value, "publish")
        publishing.ensure_publish_allowed(value, "publish", True)
        publishing.ensure_publish_allowed({"reviewCandidate": False}, "publish")
        with self.assertRaises(ValueError): publishing.ensure_publish_allowed({"reviewCandidate": False}, "publish", True)
        with self.assertRaises(ValueError): publishing.ensure_publish_allowed(value, "upload", True)

    def test_missing_scope_switch_rejects_before_credentials_or_network(self):
        with patch.object(sys, "argv", ["publish_v06_release.py", "publish", "--review-candidate"]), \
                patch.object(publishing.subprocess, "check_output", return_value="https://github.com/XDwudi/game2.git\n"), \
                patch.object(publishing, "release_inputs", return_value=({"reviewCandidate": True}, {}, "c" * 40)), \
                patch.object(publishing.subprocess, "run") as credential, \
                patch.object(publishing.urllib.request, "build_opener") as network:
            with self.assertRaises(ValueError): publishing.main()
            credential.assert_not_called(); network.assert_not_called()

    def test_review_paths_and_schema_are_separate_from_default_artifacts(self):
        self.assertEqual("Releases/p0.6.0", packaging.candidate_directory())
        self.assertEqual("Releases/p0.6.0/review-candidate", packaging.candidate_directory(True))
        self.assertNotEqual(packaging.release_manifest_path(), packaging.release_manifest_path(True))
        self.assertNotEqual(packaging.package_proof_name("macos"), packaging.package_proof_name("macos", True))
        self.assertNotEqual(packaging.kit_proof_name(), packaging.kit_proof_name(True))


class ReviewMacComparisonTests(unittest.TestCase):
    setUp = mac_fixtures.ReplayTests.setUp
    tearDown = mac_fixtures.ReplayTests.tearDown
    write = mac_fixtures.ReplayTests.write

    def test_windows_partial_reference_does_not_relax_actual_mac(self):
        self.write(self.windows, "player-report.json", partial())
        self.assertFalse(compare_directories(self.windows, self.mac)["passed"])
        result = compare_directories(self.windows, self.mac, True)
        self.assertTrue(result["passed"])
        self.assertEqual("PENDING_P22_NOT_PASSED", result["windowsReference"]["windowsNativeDisplayStatus"])
        self.assertFalse(result["fullReleaseAccepted"])
        value = report(True); value["displayChecks"] = []
        self.write(self.mac, "player-report.json", value)
        self.assertFalse(compare_directories(self.windows, self.mac, True)["passed"])
        value = partial(report(True)); self.write(self.mac, "player-report.json", value)
        self.assertFalse(compare_directories(self.windows, self.mac, True)["passed"])


class ReviewTraceTests(unittest.TestCase):
    setUp = delivery_fixtures.TraceCoverageTests.setUp
    tearDown = delivery_fixtures.TraceCoverageTests.tearDown
    save_rows = delivery_fixtures.TraceCoverageTests.save_rows

    def test_review_route_still_reads_every_input_trace_command(self):
        self.report = partial(self.report)
        normal = self.root / "evidence/v06/session/normal-run-trace.json"
        debug = self.root / "evidence/v06/session/debug-24-trace.json"
        write_json(normal, self.normal); write_json(debug, self.debug)
        write_json(self.root / "player-report.json", self.report)
        with patch.object(packaging, "ROOT", self.root):
            self.assertEqual(REVIEW_STATUS, packaging.validate_windows_report(self.root, IDENTITY, True)["status"])
            with self.assertRaises(ValueError): packaging.validate_windows_report(self.root, IDENTITY)
            self.debug_rows.pop(); self.save_rows()
            with self.assertRaises(ValueError): packaging.validate_windows_report(self.root, IDENTITY, True)


class ReviewManifestTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="synthetic-review-manifest-")
        self.root = Path(self.temp.name)
        self.patches = [patch.object(module, "ROOT", self.root) for module in (packaging, manifesting)]
        for item in self.patches: item.start()
        self.report = partial()
        self.report["normalReplay"].update(seed=42, terminalStage=8)
        self.report["environment"]["buildGuid"] = "windows-guid"
        self.windows = self.root / "windows-results"
        write_json(self.windows / "player-report.json", self.report)
        write_json(self.root / packaging.GATE, {"synthetic": "not an actual Unity gate"})
        self.build_paths = {}; self.builds = {}
        self.proofs = []
        for platform, suffix in (("windows", "win-x64"), ("macos", "macos-universal")):
            build_path = self.root / (platform + "-build.json")
            build = {"profile": platform + ".asset", "buildUtc": "2026-10-31T00:00:00Z", "buildGuid": platform + "-guid", "warnings": 0}
            write_json(build_path, build); self.build_paths[platform] = build_path; self.builds[platform] = build
            package = self.root / packaging.candidate_directory(True) / ("DicePrototype-p0.6.0-" + suffix + ".zip")
            proof = self.make_zip(package)
            proof.update(identity=IDENTITY, reviewCandidate=True, buildReport={"sha256": packaging.sha(build_path)})
            write_json(self.root / "evidence/v06/packages" / packaging.package_proof_name(platform, True), proof)
            self.proofs.append(proof)
        self.kit_path = self.root / "evidence/v06/packages" / packaging.kit_proof_name(True)
        self.kit = self.make_zip(self.root / packaging.candidate_directory(True) / "DicePrototype-p0.6.0-MacVerificationKit.zip")
        import hashlib
        notice_hash = hashlib.sha256(json.dumps(packaging.candidate_notice(IDENTITY), ensure_ascii=False, indent=2).encode("utf-8")).hexdigest()
        self.kit.update(identity=IDENTITY, reviewCandidate=True, windowsResults="windows-results", inputs=[
            {"path": "inputs/normal-run-trace.json", "sha256": self.report["traceSha256"]},
            {"path": "inputs/debug-24-trace.json", "sha256": self.report["debugTraceSha256"]},
            {"path": "inputs/windows-results/player-report.json", "sha256": packaging.sha(self.windows / "player-report.json")},
            {"path": "inputs/macos.zip", "sha256": self.proofs[1]["sha256"]},
            {"path": "inputs/review-candidate.json", "sha256": notice_hash}])
        write_json(self.kit_path, self.kit)
        self.patches += [patch.object(manifesting, "validated_gate", return_value=(IDENTITY, {}, {"syntheticTestsOnly": True})),
                         patch.object(manifesting, "validate_windows_report", return_value=self.report),
                         patch.object(manifesting, "validate_build", side_effect=lambda path, platform, identity: self.builds[platform])]
        for item in self.patches[2:]: item.start()

    def tearDown(self):
        for item in reversed(self.patches): item.stop()
        self.temp.cleanup()

    def make_zip(self, path):
        path.parent.mkdir(parents=True, exist_ok=True)
        with zipfile.ZipFile(path, "x") as archive: archive.writestr("synthetic-fixture.txt", "Tool metadata test only; no executable")
        return {"packagePath": path.relative_to(self.root).as_posix(), "sha256": packaging.sha(path), "byteSize": path.stat().st_size}

    def create(self, recovery=None, review=True):
        return manifesting.create_manifest(self.windows, self.build_paths["windows"], self.build_paths["macos"], recovery, review)

    def test_review_manifest_keeps_pending_status_and_separate_paths(self):
        target = self.create(); value = packaging.read_json(target)
        self.assertEqual("v06-restricted-review-candidate-1", value["schema"])
        self.assertEqual(packaging.release_manifest_path(True), target)
        self.assertFalse(packaging.release_manifest_path().exists())
        self.assertFalse(value["fullReleaseAccepted"])
        self.assertEqual("not_executed_native_display_pending", value["windowsSeparateProcessDisplayRecovery"]["status"])
        self.assertEqual("AUTOMATED_GAMEPLAY_UI_PERF_ONLY_NATIVE_DISPLAY_PENDING", value["platforms"][0]["smokeStatus"])
        self.assertIn("User-authorized", value["deliveryAuthorization"])
        self.assertNotIn("publicationApproval", value)
        self.assertEqual(REVIEW_STATUS, packaging.read_json(self.windows / "player-report.json")["status"])
        manifesting.validate_windows_report.assert_called_once_with(self.windows, IDENTITY, True, False)

    def test_formal_manifest_still_requires_recovery(self):
        with self.assertRaises(ValueError): self.create(review=False)
        self.assertFalse(packaging.release_manifest_path().exists())

    def test_supplied_bad_recovery_is_not_silently_replaced_with_pending(self):
        with patch.object(manifesting, "validate_recovery", side_effect=ValueError("actual recovery failed")), self.assertRaises(ValueError):
            self.create(self.root / "failed-recovery")

    def test_review_still_rejects_changed_package_bytes(self):
        (self.root / self.proofs[0]["packagePath"]).write_bytes(b"changed")
        with self.assertRaises(ValueError): self.create()

    def test_review_still_requires_bound_kit_notice_and_report(self):
        for name in ("inputs/review-candidate.json", "inputs/windows-results/player-report.json"):
            value = copy.deepcopy(self.kit)
            next(row for row in value["inputs"] if row["path"] == name)["sha256"] = "0" * 64
            write_json(self.kit_path, value)
            with self.assertRaises(ValueError): self.create()


class ReviewRecoveryRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="synthetic-review-recovery-")
        self.root = Path(self.temp.name)
        self.args = argparse.Namespace(exe=self.root / "synthetic.exe", trace=self.root / "normal.json",
            debug_trace=self.root / "debug.json", identity=self.root / "identity.json", build_report=self.root / "build.json",
            main_results=self.root / "main", evidence_dir=self.root / "evidence/v06/recovery", raw_dir=None,
            timeout=120, review_candidate=True)
        self.args.main_results.mkdir()
        for name in ("exe", "trace", "debug_trace", "identity", "build_report"):
            getattr(self.args, name).write_text("{}", encoding="utf-8")
        self.report = partial()
        self.report["environment"]["buildGuid"] = "synthetic-guid"
        self.report["traceSha256"] = packaging.sha(self.args.trace)
        self.report["debugTraceSha256"] = packaging.sha(self.args.debug_trace)
        write_json(self.args.main_results / "player-report.json", self.report)

    def tearDown(self): self.temp.cleanup()

    def test_partial_reference_requires_explicit_runner_option_and_never_launches_in_preflight(self):
        def validate(directory, identity, review=False, allow_failed=False):
            self.assertFalse(allow_failed)
            failures = validate_windows_review_reference(self.report) if review else validate_report(self.report, "windows")
            if failures: raise ValueError("reference rejected")
            return self.report
        with patch.object(recovery_runner, "ROOT", self.root), patch.object(recovery_runner.sys, "platform", "win32"), \
                patch.object(recovery_runner, "validate_identity", return_value=IDENTITY), \
                patch.object(recovery_runner, "validate_build", return_value={"buildGuid": "synthetic-guid"}), \
                patch.object(recovery_runner, "validate_windows_report", side_effect=validate), \
                patch.object(recovery_runner.subprocess, "Popen") as player:
            recovery_runner.prepare(self.args)
            self.args.review_candidate = False
            with self.assertRaises(ValueError): recovery_runner.prepare(self.args)
            player.assert_not_called()

    def test_independent_recovery_result_does_not_become_full_windows_pass(self):
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
        self.assertFalse(result["windowsReference"]["fullWindowsPassed"])
        self.assertEqual("PENDING_P22_NOT_PASSED", result["windowsReference"]["windowsNativeDisplayStatus"])
        self.assertEqual(REVIEW_STATUS, packaging.read_json(self.args.main_results / "player-report.json")["status"])


if __name__ == "__main__": unittest.main()
