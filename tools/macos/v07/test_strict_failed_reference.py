"""Synthetic tooling contracts only: no Player, hardware, credentials or network."""
import copy
import contextlib
import csv
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

import package_v07 as packaging
import publish_v07_release as publishing
import release_v07_manifest as manifesting
import verify_v07_display_recovery as recovery
import run_v07_windows_player as runner
from compare_replays import (validate_report, validate_windows_reference, validate_failed_performance_csv,
                             review_reference_notice, compare_directories, diagnostic_release_status)
from test_mac_tools import report, IDENTITY
import test_diagnostic_candidate as diagnostic_fixtures
import test_review_candidate as review_fixtures
from test_delivery_tools import write_json
diagnostic = diagnostic_fixtures.diagnostic


def strict_failed(source=None):
    source = copy.deepcopy(source or report())
    result = diagnostic(source)
    result.update(verificationScope="full_or_recovery", nativeDisplayDeferred=False, phase="Completed")
    result["displayChecks"] = copy.deepcopy(source["displayChecks"])
    result["uiChecks"].append(next(copy.deepcopy(r) for r in source["uiChecks"] if r["id"] == "display-state-isolation"))
    perf = result["performance"]
    perf.update(frameCount=8, focusedSampleFrames=9, sampleSeconds=180.011, gpuInvalidSamples=0, gpuMissingSamples=2,
                independentFrameClocks={"rawRowsIncludingInitialPartialInterval": 9}, requiredMetricCoverage=.95,
                gpu=dict(available=True, samples=6, expectedFrames=8, validFrameCoverage=.75))
    perf["frameInterval"].update(p95=22500., p50=22500., mean=22500., max=22500., samples=8, expectedFrames=8, validFrameCoverage=1)
    for key in ("mainThread","gcAllocated","uiRebuild"):
        perf[key]=dict(available=True,samples=8,expectedFrames=8,validFrameCoverage=1)
    return result


def raw_csv(directory):
    path = directory / "performance-frames.csv"
    directory.mkdir(parents=True, exist_ok=True)
    with path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.writer(stream)
        writer.writerow(("frame", "elapsed_s", "interval_ms", "gpu_ms", "gpu_sample_status"))
        for index in range(9):
            missing = index in (3, 7)
            writer.writerow((index + 1, .01 + index * 22.5, 10. if index == 0 else 22500., "" if missing else .3, 2 if missing else 1))
    return path


class StrictFailedContracts(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="synthetic-strict-failed-")
        self.root = Path(self.temp.name); self.value = strict_failed(); self.csv = raw_csv(self.root)
    def tearDown(self): self.temp.cleanup()

    def test_complete_strict_failure_retains_native_facts_and_never_passes(self):
        before = copy.deepcopy(self.value)
        self.assertEqual([], validate_windows_reference(self.value, True, True))
        self.assertTrue(validate_report(self.value, "windows"))
        notice = review_reference_notice(self.value, True)
        self.assertEqual("FAILED", notice["windowsPerformance"]["acceptanceStatus"])
        self.assertEqual("INCOMPLETE_OR_UNAVAILABLE", notice["windowsPerformance"]["measurementCoverageStatus"])
        self.assertEqual("AUTOMATED_CHECKS_PASSED_PHYSICAL_EVENTS_UNTESTED", notice["windowsNativeDisplayStatus"])
        self.assertFalse(notice["fullWindowsPassed"]); self.assertFalse(notice["fullReleaseAccepted"])
        self.assertEqual(before, self.value)

    def test_full_raw_counter_bound_includes_exactly_one_partial_row(self):
        proof = validate_failed_performance_csv(self.value, self.root)
        self.assertEqual((9,8,1), (proof["rawRows"],proof["validIntervals"],proof["excludedInitialPartialRows"]))
        for raw, focus in ((8,9),(10,9),(9,10),(None,9),(True,1)):
            value = copy.deepcopy(self.value); value["performance"]["independentFrameClocks"]["rawRowsIncludingInitialPartialInterval"] = raw
            value["performance"]["focusedSampleFrames"] = focus
            self.assertTrue(validate_windows_reference(value, True, True), (raw,focus))

    def test_incomplete_or_non_budget_or_wrong_scope_or_real_error_rejected(self):
        for key, value in (("phase","DebugStressSampling"),("status","failed"),("exitCode",2),("stressCompleted",False),
                           ("failure","error"),("nativeDisplayDeferred",True),("verificationScope","display_only")):
            candidate = copy.deepcopy(self.value); candidate[key] = value
            self.assertTrue(validate_windows_reference(candidate, True, True), (key,value))

    def test_strict_native_failures_cannot_be_excused_by_diagnostic_flag(self):
        for change in ("missing", "failed", "wrong_actual", "duplicate", "missing_isolation"):
            candidate = copy.deepcopy(self.value)
            if change == "missing": candidate["displayChecks"] = []
            elif change == "failed": candidate["displayChecks"][0]["status"] = "failed"
            elif change == "wrong_actual": next(r for r in candidate["displayChecks"] if r["id"]=="window-2560x1440")["actualNativeReadback"]["height"] = 1417
            elif change == "duplicate": candidate["displayChecks"].append(candidate["displayChecks"][0])
            else: candidate["uiChecks"] = [r for r in candidate["uiChecks"] if r["id"] != "display-state-isolation"]
            self.assertTrue(validate_windows_reference(candidate, True, True), change)

    def test_lower_coverage_threshold_or_inconsistent_metrics_never_claim_complete(self):
        for mutate in (lambda p:p.update(requiredMetricCoverage=.5), lambda p:p["gpu"].update(validFrameCoverage=2),
                       lambda p:p["gpu"].update(validFrameCoverage=.95), lambda p:p["mainThread"].update(samples=9),
                       lambda p:p["uiRebuild"].update(available=False), lambda p:p["gcAllocated"].update(expectedFrames=9)):
            value=copy.deepcopy(self.value);mutate(value["performance"])
            self.assertTrue(validate_windows_reference(value,True,True))
        value=copy.deepcopy(self.value);value["performance"]["requiredMetricCoverage"]=.5
        self.assertEqual("INCOMPLETE_OR_UNAVAILABLE",review_reference_notice(value,True)["windowsPerformance"]["measurementCoverageStatus"])

    def test_additive_requested_fields_and_consistent_probe21_are_supported(self):
        value = copy.deepcopy(self.value); value["probeRevision"] = "p07-player-art-ui-21"; value["environment"]["probeRevision"] = value["probeRevision"]
        for row in value["uiChecks"] + value["displayChecks"]: row["probeRevision"] = value["probeRevision"]
        for row in value["displayChecks"]: row.update(requested={"width":2560,"height":1440},requestWorkArea={"width":2560,"height":1440})
        self.assertEqual([], validate_windows_reference(value, True, True))

    def test_missing_or_truncated_or_clock_inconsistent_csv_rejected(self):
        original = self.csv.read_bytes()
        self.csv.unlink()
        with self.assertRaises(OSError): validate_failed_performance_csv(self.value,self.root)
        for data in (b"\n".join(original.splitlines()[:-1])+b"\n", original.replace(b"22.51", b"22.52", 1),
                     original.replace(b"2,22.51", b"3,22.51", 1)):
            self.csv.write_bytes(data)
            with self.assertRaises(ValueError): validate_failed_performance_csv(self.value,self.root)

    def test_forged_metric_or_gpu_counts_rejected_without_rewriting_raw(self):
        original = self.csv.read_bytes()
        for mutate in (lambda p:p["frameInterval"].update(p95=22501),lambda p:p.update(gpuMissingSamples=1),
                       lambda p:p["gpu"].update(validFrameCoverage=1),lambda p:p.update(sampleSeconds=181.)):
            candidate = copy.deepcopy(self.value); mutate(candidate["performance"])
            # A late close by <=one real interval is permitted; use 300s to exceed actual coverage.
            if candidate["performance"]["sampleSeconds"] == 181.: candidate["performance"]["sampleSeconds"] = 300.
            with self.assertRaises(ValueError): validate_failed_performance_csv(candidate,self.root)
        self.assertEqual(original,self.csv.read_bytes())


class StrictTraceContracts(unittest.TestCase):
    setUp = review_fixtures.ReviewTraceTests.setUp
    tearDown = review_fixtures.ReviewTraceTests.tearDown
    save_rows = review_fixtures.ReviewTraceTests.save_rows
    def test_packaging_requires_actual_trace_and_csv_under_failed_switch(self):
        self.report = strict_failed(self.report)
        write_json(self.root/"evidence/v07/session/normal-run-trace.json",self.normal)
        write_json(self.root/"evidence/v07/session/debug-24-trace.json",self.debug)
        write_json(self.root/"player-report.json",self.report); csv_path=raw_csv(self.root)
        with patch.object(packaging,"ROOT",self.root):
            self.assertEqual("budget_exceeded",packaging.validate_windows_report(self.root,IDENTITY,True,True)["status"])
            csv_path.unlink()
            with self.assertRaises(OSError): packaging.validate_windows_report(self.root,IDENTITY,True,True)
            raw_csv(self.root); self.debug_rows.pop();self.save_rows()
            with self.assertRaises(ValueError): packaging.validate_windows_report(self.root,IDENTITY,True,True)


class StrictMacContracts(unittest.TestCase):
    setUp = review_fixtures.ReviewMacComparisonTests.setUp
    tearDown = review_fixtures.ReviewMacComparisonTests.tearDown
    write = review_fixtures.ReviewMacComparisonTests.write
    def test_mac_remains_strict_and_missing_windows_csv_is_rejected(self):
        self.write(self.windows,"player-report.json",strict_failed());raw_csv(self.windows)
        value = compare_directories(self.windows,self.mac,True,True)
        self.assertTrue(value["passed"]);self.assertFalse(value["fullReleaseAccepted"])
        (self.windows/"performance-frames.csv").unlink()
        self.assertFalse(compare_directories(self.windows,self.mac,True,True)["passed"])
        raw_csv(self.windows);bad=report(True);bad["performance"]["frameInterval"]["p95"]=23
        self.write(self.mac,"player-report.json",bad)
        self.assertFalse(compare_directories(self.windows,self.mac,True,True)["passed"])


class StrictManifestContracts(unittest.TestCase):
    tearDown = diagnostic_fixtures.DiagnosticManifestTests.tearDown
    make_zip = diagnostic_fixtures.DiagnosticManifestTests.make_zip
    create = diagnostic_fixtures.DiagnosticManifestTests.create
    def setUp(self):
        diagnostic_fixtures.DiagnosticManifestTests.setUp(self)
        self.report.clear();self.report.update(strict_failed())
        self.report["normalReplay"].update(seed=42,terminalStage=8);self.report["environment"]["buildGuid"]="windows-guid"
        write_json(self.windows/"player-report.json",self.report);csv_path=raw_csv(self.windows)
        for platform,proof in zip(("windows","macos"),self.proofs):
            proof.update(windowsReportSha256=packaging.sha(self.windows/"player-report.json"))
            write_json(self.root/"evidence/v07/packages"/packaging.package_proof_name(platform,True),proof)
        for row in self.kit["inputs"]:
            if row["path"]=="inputs/windows-results/player-report.json":row["sha256"]=packaging.sha(self.windows/"player-report.json")
            if row["path"]=="inputs/review-candidate.json":row["sha256"]=hashlib.sha256(json.dumps(packaging.candidate_notice(IDENTITY,self.report,True),ensure_ascii=False,indent=2).encode()).hexdigest()
        self.kit["inputs"].append({"path":"inputs/windows-results/performance-frames.csv","sha256":packaging.sha(csv_path)})
        write_json(self.kit_path,self.kit)

    def test_strict_manifest_and_body_keep_failed_performance_and_native_limits(self):
        target=self.create();value=packaging.read_json(target)
        self.assertEqual("DIAGNOSTIC_TEST_CANDIDATE_WINDOWS_PERFORMANCE_FAILED",value["releaseStatus"])
        self.assertEqual("GAMEPLAY_UI_NATIVE_AUTOMATED_PERFORMANCE_FAILED",value["platforms"][0]["smokeStatus"])
        self.assertEqual(packaging.sha(self.windows/"performance-frames.csv"),value["windowsPerformanceRawEvidence"]["sha256"])
        self.assertFalse(value["fullReleaseAccepted"])
        self.assertTrue(any("Windows separate-process display recovery pending" in item for item in value["requiredBeforeFullAcceptance"]))
        value["tests"]={"unityEditMode":{"passed":1},"unityPlayMode":{"passed":1},"syntheticOnly":True}
        body=publishing.release_body(value)
        self.assertIn("性能 FAILED",body);self.assertIn("自动原生显示检查通过",body);self.assertIn("物理焦点/显示器变更未验",body)
        self.assertNotIn("原生独占 P22 未通过",body)

    def test_missing_kit_csv_binding_rejects_manifest(self):
        self.kit["inputs"]=[r for r in self.kit["inputs"] if r["path"]!="inputs/windows-results/performance-frames.csv"]
        write_json(self.kit_path,self.kit)
        with self.assertRaises(ValueError):self.create()


class SerialSelectionContracts(unittest.TestCase):
    setUp = review_fixtures.ReviewTraceTests.setUp
    tearDown = review_fixtures.ReviewTraceTests.tearDown
    save_rows = review_fixtures.ReviewTraceTests.save_rows
    def test_selects_strict_failed_reference_and_only_native_interruption_runs_deferred(self):
        script=Path(runner.__file__)
        write_json(self.root/"evidence/v07/session/normal-run-trace.json",self.normal)
        write_json(self.root/"evidence/v07/session/debug-24-trace.json",self.debug)
        write_json(self.root/"DicePrototype/Assets/Resources/BuildIdentity.json",IDENTITY)
        build=self.root/"build.json";write_json(build,{"syntheticOnly":True})
        exe=self.root/"Builds/v07/Windows/DicePrototype.exe";exe.parent.mkdir(parents=True);exe.write_bytes(b"SYNTHETIC_NOT_EXECUTABLE")
        dll=exe.parent/"DicePrototype_Data/Managed/DiceCraft.Runtime.dll";dll.parent.mkdir(parents=True)
        dll.write_bytes("p0-player-verification-7 p07-player-art-ui-".encode("utf-16-le"))
        for scenario in ("complete-strict", "invalid-csv", "native-interruption"):
            launches=[]
            evidence=self.root/"evidence/v07"/scenario;raw=self.root/"artifacts/cache/v07"/scenario
            def fake_launch(argv, raw_dir, label, timeout, visible):
                launches.append(label)
                value=strict_failed(self.report)
                if scenario=="native-interruption" and label=="strict-full":
                    value.update(status="failed",exitCode=1,phase="V07NativeDisplay",failure="synthetic native failure",stressCompleted=False)
                if label=="native-deferred-independent":value=diagnostic(self.report)
                value["environment"]["buildGuid"]="synthetic-guid"
                folder=Path(argv[argv.index("--evidence-dir")+1])/"player-synthetic";folder.mkdir()
                write_json(folder/"player-report.json",value)
                for name in ("normal-replay.jsonl","debug24-replay.jsonl"):(folder/name).write_bytes((self.root/name).read_bytes())
                raw_csv(folder)
                if scenario=="invalid-csv" and label=="strict-full":(folder/"performance-frames.csv").unlink()
                return {"label":label,"exitCode":value["exitCode"],"timedOut":False}
            def fake_recovery(args):
                write_json(args.evidence_dir/"runner-summary.json",{"passed":True,"syntheticOnly":True})
                return 0
            argv=[str(script),"--repo",str(self.root),"--build-report",str(build),"--expected-build-guid","synthetic-guid",
                  "--evidence-root",str(evidence),"--raw-root",str(raw),"--execute"]
            with patch.object(sys,"argv",argv),patch.object(runner.sys,"platform","win32"),patch.object(runner,"launch",side_effect=fake_launch), \
                 patch.object(runner,"ordinary_save_guard",return_value={}),patch.object(packaging,"ROOT",self.root), \
                 patch.object(packaging,"validated_gate",return_value=(IDENTITY,{},{})), \
                 patch.object(packaging,"validate_build",return_value={"buildGuid":"synthetic-guid","output":str(exe)}), \
                 patch.object(recovery,"execute",side_effect=fake_recovery),contextlib.redirect_stdout(io.StringIO()):
                self.assertEqual(1,runner.main())
            summary=json.loads((evidence/"serial-summary.json").read_text(encoding="utf-8"))
            self.assertFalse(summary["strictWindowsAutomatedPassed"]);self.assertFalse(summary["wholeReleaseAccepted"])
            if scenario=="complete-strict":
                self.assertEqual(["strict-full"],launches)
                self.assertEqual("diagnostic_performance_failed_strict_native_automated",summary["referenceScope"])
            elif scenario=="invalid-csv":self.assertNotIn("native-deferred-independent",launches)
            else:
                self.assertIn("native-deferred-independent",launches)
                self.assertEqual("diagnostic_performance_failed_native_pending",summary["referenceScope"])


if __name__ == "__main__": unittest.main()
