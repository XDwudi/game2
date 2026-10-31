"""Offline synthetic tooling tests. No Unity, Mac process, credentials or network are used."""
import copy
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET

sys.path.insert(0, str(Path(__file__).resolve().parents[2]))
import package_v06 as packaging
import publish_v06_release as publishing
from compare_replays import validate_recovery, validate_trace_replay, COMMAND_KINDS, sha256
from test_mac_tools import IDENTITY, report, normal_row, debug_row
from verify_mac import metadata_inventory


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value), encoding="utf-8")


def nunit(path, mode, count):
    root = ET.Element("test-run", result="Passed", total=str(count), passed=str(count), testcasecount=str(count), failed="0", skipped="0", inconclusive="0")
    suite = ET.SubElement(root, "test-suite", result="Passed")
    ET.SubElement(ET.SubElement(suite, "properties"), "property", name="platform", value=mode)
    for index in range(count):
        ET.SubElement(suite, "test-case", fullname=mode + ".ActualCase" + str(index), result="Passed")
    path.parent.mkdir(parents=True, exist_ok=True)
    ET.ElementTree(root).write(path, encoding="utf-8", xml_declaration=True)


class EvidenceGateTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.root_patch = patch.object(packaging, "ROOT", self.root)
        self.root_patch.start()
        self.git_patch = patch.object(packaging, "validate_git_source")
        self.git_check = self.git_patch.start()  # This class tests gate bytes; it does not claim a real Git freeze.
        write_json(self.root / packaging.CONFIG, {"configVersion": "p0.6.0", "saveSchemaVersion": 4})
        write_json(self.root / packaging.GOLDEN, {"testFixture": True})
        self.identity = dict(IDENTITY, contentConfigSha256=packaging.sha(self.root / packaging.CONFIG), goldenCaseSetSha256=packaging.sha(self.root / packaging.GOLDEN))
        write_json(self.root / packaging.IDENTITY, self.identity)
        assemblies, documents = [], []
        for name in sorted(packaging.ASSEMBLIES):
            path = self.root / "DicePrototype/Assets/Game" / (name + ".cs")
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("// synthetic gate fixture " + name, encoding="utf-8")
            checksum = packaging.sha(path)
            assemblies.append({"name": name, "mvid": "12345678-abcd-abcd-abcd-123456789abc", "dllSha256": "d" * 64,
                               "pdbSha256": "e" * 64, "pdbMatchesDll": True, "projectDocuments": 1})
            documents.append({"source": str(path), "assembly": name, "expectedChecksum": checksum, "currentChecksum": checksum, "matches": True})
        audit_path = self.root / "evidence/audit.json"
        write_json(audit_path, {"mismatches": 0, "assemblies": assemblies, "documents": documents})
        tests = []
        discoveries = []
        for mode, count in (("EditMode", 7), ("PlayMode", 3)):
            path = self.root / ("evidence/" + mode + ".xml")
            nunit(path, mode, count)
            names = sorted(mode + ".ActualCase" + str(index) for index in range(count))
            tests.append({"mode": mode, "passed": count, "names": names, "report": {"path": packaging.relative(path), "sha256": packaging.sha(path)}})
            discovery = self.root / "evidence/v06/unity" / ("discovery-" + mode + ".json")
            write_json(discovery, {"mode": mode, "unity": "6000.6.7f1", "names": names})
            discoveries.append({"path": packaging.relative(discovery), "sha256": packaging.sha(discovery)})
        files = [{"path": path, "sha256": packaging.sha(self.root / path)} for path in packaging.source_paths()]
        snapshot = {"files": [row for row in files if row["path"] != packaging.IDENTITY],
                    "assemblies": [{key: row[key] for key in ("name", "mvid", "dllSha256")} for row in assemblies]}
        for test in tests:
            binding = (self.root / test["report"]["path"]).with_suffix(".binding.json")
            write_json(binding, {"schema": "v06-test-binding-1", "report": test["report"], "started": snapshot, "finished": snapshot})
            test["binding"] = {"path": packaging.relative(binding), "sha256": packaging.sha(binding)}
        files += [test["report"] for test in tests]
        files += [test["binding"] for test in tests] + discoveries
        audit_entry = {"path": packaging.relative(audit_path), "sha256": packaging.sha(audit_path)}
        files.append(audit_entry)
        self.gate = {"schema": "v06-build-gate-1", "identity": self.identity, "sourceCommit": self.identity["sourceCommit"],
                     "tests": tests, "editModePassed": 7, "playModePassed": 3, "files": files, "pdbAudit": audit_entry}
        self.save_gate()

    def tearDown(self):
        self.git_patch.stop()
        self.root_patch.stop()
        self.temp.cleanup()

    def save_gate(self):
        write_json(self.root / packaging.GATE, self.gate)

    def test_counts_are_read_from_actual_nunit_not_old_release(self):
        identity, gate, tests = packaging.validated_gate()
        self.assertEqual(self.identity, identity)
        self.assertEqual(7, tests["unityEditMode"]["passed"])
        self.assertEqual(3, tests["unityPlayMode"]["passed"])
        self.git_check.assert_called_once_with(self.identity)

    def test_source_hash_change_rejects_gate(self):
        (self.root / packaging.GOLDEN).write_text("{}", encoding="utf-8")
        with self.assertRaises(ValueError):
            packaging.validated_gate()

    def test_report_hash_change_rejects_gate(self):
        nunit(self.root / "evidence/EditMode.xml", "EditMode", 8)
        with self.assertRaises(ValueError):
            packaging.validated_gate()

    def test_missing_duplicate_or_traversal_gate_entries_rejected(self):
        original = copy.deepcopy(self.gate)
        for change in ("missing", "duplicate", "traversal"):
            self.gate = copy.deepcopy(original)
            if change == "missing":
                self.gate["files"].pop(0)
            elif change == "duplicate":
                self.gate["files"].append(self.gate["files"][0])
            else:
                self.gate["files"][0]["path"] = "../outside"
            self.save_gate()
            with self.subTest(change=change), self.assertRaises(ValueError):
                packaging.validated_gate()

    def test_old_gate_schema_or_declared_count_cannot_pass(self):
        self.gate["schema"] = "v05-build-gate-1"
        self.save_gate()
        with self.assertRaises(ValueError):
            packaging.validated_gate()
        self.gate["schema"] = "v06-build-gate-1"
        self.gate["editModePassed"] = 554
        self.save_gate()
        with self.assertRaises(ValueError):
            packaging.validated_gate()

    def test_no_skip_failure_or_duplicate_test_can_be_hidden(self):
        path = self.root / "evidence/EditMode.xml"
        for change in ("skip", "duplicate", "failure", "wrong-mode"):
            nunit(path, "EditMode", 7)
            tree = ET.parse(path)
            root = tree.getroot()
            if change == "skip":
                root.set("skipped", "1")
            elif change == "duplicate":
                root.findall(".//test-case")[1].set("fullname", root.findall(".//test-case")[0].attrib["fullname"])
            elif change == "failure":
                root.findall(".//test-case")[0].set("result", "Failed")
            else:
                root.find(".//property").set("value", "PlayMode")
            tree.write(path)
            with self.subTest(change=change), self.assertRaises(ValueError):
                packaging.verify_nunit(path, "EditMode", 7)

    def test_missing_discovery_pdb_or_runtime_binding_cannot_pass(self):
        original = copy.deepcopy(self.gate)
        for change in ("audit", "binding", "discovery"):
            self.gate = copy.deepcopy(original)
            if change == "audit":
                del self.gate["pdbAudit"]
            elif change == "binding":
                del self.gate["tests"][0]["binding"]
            else:
                self.gate["files"] = [row for row in self.gate["files"] if "discovery-EditMode" not in row["path"]]
            self.save_gate()
            with self.subTest(change=change), self.assertRaises(ValueError):
                packaging.validated_gate()

    def test_rehashed_binding_cannot_hide_different_loaded_assembly(self):
        proof = self.gate["tests"][0]["binding"]
        binding = packaging.read_json(self.root / proof["path"])
        binding["finished"]["assemblies"][0]["mvid"] = "87654321-abcd-abcd-abcd-123456789abc"
        write_json(self.root / proof["path"], binding)
        proof["sha256"] = packaging.sha(self.root / proof["path"])
        self.save_gate()
        with self.assertRaises(ValueError):
            packaging.validated_gate()

    def test_rehashed_discovery_cannot_omit_executed_test(self):
        proof = next(row for row in self.gate["files"] if "discovery-EditMode" in row["path"])
        discovery = packaging.read_json(self.root / proof["path"])
        discovery["names"].pop()
        write_json(self.root / proof["path"], discovery)
        proof["sha256"] = packaging.sha(self.root / proof["path"])
        self.save_gate()
        with self.assertRaises(ValueError):
            packaging.validated_gate()

    def test_v05_identity_and_schema_boolean_rejected(self):
        for field, value in (("releaseVersion", "p0.5.0"), ("saveSchemaVersion", True), ("contentConfigVersion", "p0.5.0"), ("sourceCommit", "pending")):
            identity = dict(self.identity)
            identity[field] = value
            with self.subTest(field=field), self.assertRaises(ValueError):
                packaging.validate_identity(identity)

    def test_records_never_overwrite_prior_evidence(self):
        path = self.root / "evidence/proof.json"
        packaging.record(path, {"first": True})
        with self.assertRaises(FileExistsError):
            packaging.record(path, {"replacement": True})
        self.assertEqual({"first": True}, packaging.read_json(path))


class PublishSafetyTests(unittest.TestCase):
    def test_redirect_strips_authorization_across_host_and_port(self):
        for url in ("https://release-assets.githubusercontent.com/data", "https://api.github.com:444/data"):
            request = urllib.request.Request("https://api.github.com/repos/XDwudi/game2/releases/assets/123", headers={"Authorization": "Bearer synthetic"})
            redirected = publishing.SafeRedirect().redirect_request(request, None, 302, "Found", {}, url)
            self.assertIsNone(redirected.get_header("Authorization"))

    def test_same_host_https_redirect_retains_auth(self):
        request = urllib.request.Request("https://api.github.com/repos/XDwudi/game2/a", headers={"Authorization": "Bearer synthetic"})
        redirected = publishing.SafeRedirect().redirect_request(request, None, 302, "Found", {}, "https://api.github.com/repos/XDwudi/game2/b")
        self.assertEqual("Bearer synthetic", redirected.get_header("Authorization"))

    def test_http_redirect_and_untrusted_authenticated_urls_rejected(self):
        request = urllib.request.Request("https://api.github.com/repos/XDwudi/game2/a")
        with self.assertRaises(ValueError):
            publishing.SafeRedirect().redirect_request(request, None, 302, "Found", {}, "http://example.com/a")
        for url in ("http://api.github.com/repos/XDwudi/game2/a", "https://evil.invalid/repos/XDwudi/game2/a", "https://api.github.com:444/repos/XDwudi/game2/a", "https://u@api.github.com/repos/XDwudi/game2/a", "https://api.github.com/repos/other/repo/a"):
            with self.subTest(url=url), self.assertRaises(ValueError):
                publishing.authenticated_url(url)
        publishing.authenticated_url("https://uploads.github.com/repos/XDwudi/game2/releases/1/assets")

    def test_tag_uses_remote_actual_commit_including_annotated_tags(self):
        commit = "a" * 40
        self.assertEqual(commit, publishing.remote_tag_target(lambda url: {"object": {"type": "commit", "sha": commit}}))
        calls = []
        def annotated(url):
            calls.append(url)
            return {"object": {"type": "tag" if len(calls) == 1 else "commit", "sha": "b" * 40 if len(calls) == 1 else commit}}
        self.assertEqual(commit, publishing.remote_tag_target(annotated))
        self.assertEqual(2, len(calls))
        def missing(url):
            raise urllib.error.HTTPError(url, 404, "Not Found", {}, None)
        self.assertIsNone(publishing.remote_tag_target(missing))

    def test_release_body_uses_manifest_counts_and_honest_mac_status(self):
        manifest = {"tests": {"unityEditMode": {"passed": 7}, "unityPlayMode": {"passed": 3}},
                    "windowsPerformance": {"sampleSeconds": 181.1, "p95FrameIntervalMs": 16.5},
                    "platforms": [], "macVerificationKit": {"packagePath": "kit.zip", "byteSize": 123, "sha256": "a" * 64},
                    "sourceCommit": "c" * 40, "ruleReproduction": {"normalOutcome": "RunFailed", "normalFinalStage": 8}}
        body = publishing.release_body(manifest)
        self.assertIn("EditMode 7", body)
        self.assertIn("PlayMode 3", body)
        self.assertIn("仍待实机验证", body)
        self.assertIn("RunFailed", body)
        self.assertNotIn("554", body)


class TraceCoverageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.normal_rows = [normal_row(0), normal_row(1)]
        self.normal_rows[-1].update(phase="RunFailed", stageIndex=7)
        self.debug_rows = [debug_row(0), debug_row(1)]
        self.normal = {"configVersion": "p0.6.0", "isDebug": False, "seed": 42, "expectedFinalPhase": "RunFailed", "expectedFinalStage": 8, "steps": self.normal_rows}
        self.debug = {"configVersion": "p0.6.0", "debug": True, "expectedFinalPhase": "RunWon", "stages": 24, "shops": 23, "risks": 8,
                      "steps": [dict(actionSequence=row["actionSequence"], phase=row["phase"], gold=row["gold"], stageIndex=row["stageIndex"],
                                     stageScore=row["score"], stateHash=row["ruleStateSha256"], command={"kind": COMMAND_KINDS.index(row["kind"])}) for row in self.debug_rows]}
        self.normal_path, self.debug_path = self.root / "normal.json", self.root / "debug.json"
        write_json(self.normal_path, self.normal)
        write_json(self.debug_path, self.debug)
        self.report = report(False)
        self.report["traceSha256"], self.report["debugTraceSha256"] = sha256(self.normal_path), sha256(self.debug_path)
        self.report["normalReplay"].update(seed=42, terminalStage=8, expectedSteps=2, completedSteps=2)
        self.report["debug24"]["steps"] = self.debug_rows
        self.save_rows()

    def tearDown(self):
        self.temp.cleanup()

    def save_rows(self):
        for name, rows in (("normal-replay.jsonl", self.normal_rows), ("debug24-replay.jsonl", self.debug_rows)):
            (self.root / name).write_text("\n".join(json.dumps(row) for row in rows), encoding="utf-8")

    def validate(self):
        return validate_trace_replay(self.report, self.root, self.normal_path, self.debug_path)

    def test_full_input_derived_counts_and_fields_match(self):
        proof = self.validate()
        self.assertEqual((2, 2), (proof["normalSteps"], proof["debugSteps"]))

    def test_shortened_rows_and_self_declared_counts_cannot_pass(self):
        self.normal_rows = self.normal_rows[:1]
        self.debug_rows = self.debug_rows[:1]
        self.report["normalReplay"].update(expectedSteps=1, completedSteps=1)
        self.report["debug24"]["steps"] = self.debug_rows
        self.save_rows()
        with self.assertRaises(ValueError):
            self.validate()

    def test_same_count_wrong_score_kind_or_debug_hash_rejected(self):
        original = copy.deepcopy(self.debug_rows)
        for field, value in (("score", "999"), ("kind", "Submit"), ("ruleStateSha256", "c" * 64)):
            self.debug_rows = copy.deepcopy(original)
            self.debug_rows[0][field] = value
            self.report["debug24"]["steps"] = self.debug_rows
            self.save_rows()
            with self.subTest(field=field), self.assertRaises(ValueError):
                self.validate()

    def test_report_debug_steps_must_equal_jsonl(self):
        self.report["debug24"]["steps"] = []
        with self.assertRaises(ValueError):
            self.validate()

    def test_mac_return_package_requires_all_three_original_metadata_files(self):
        for name in ("BUILD_IDENTITY.json", "Licenses/NotoSansCJK-OFL.txt", "开始游玩.txt"):
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text("synthetic metadata", encoding="utf-8")
        self.assertEqual(3, len(metadata_inventory(self.root)))
        (self.root / "Licenses/NotoSansCJK-OFL.txt").unlink()
        with self.assertRaises(ValueError):
            metadata_inventory(self.root)


class RecoveryEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name).resolve()
        self.report_dir = self.root / "reader/player-synthetic"
        confirmed = {"width": 1280, "height": 720, "mode": 3}
        self.ready = dict(schema="p06-native-display-recovery-1", status="awaiting_external_process_termination",
            writerProcessId=100, readerProcessId=0, confirmed=confirmed, candidate={"width": 1920, "height": 1080, "mode": 1},
            actual={"width": 1920, "height": 1080, "mode": 1}, settingsPath=str(self.root / "display-settings.json"),
            pendingSha256="f" * 64, buildIdentity=json.dumps(IDENTITY), evidenceDirectory=str(self.root / "writer"))
        self.receipt = dict(self.ready, status="passed", readerProcessId=200, actual=confirmed, evidenceDirectory=str(self.report_dir))
        write_json(self.root / "ready-to-terminate.json", self.ready)
        write_json(self.root / "recovery-read-receipt.json", self.receipt)
        write_json(self.root / "display-settings.json", {"pending": False})
        self.report = {"status": "recovery_passed", "exitCode": 0, "schema": "p0-player-verification-6", "unityErrors": [],
                       "buildIdentity": json.dumps(IDENTITY), "environment": {"isEditor": False, "platform": "WindowsPlayer"}}
        write_json(self.report_dir / "player-report.json", self.report)

    def tearDown(self):
        self.temp.cleanup()

    def test_separate_process_receipt_is_independent_from_main_probe(self):
        result = validate_recovery(self.root, IDENTITY)
        self.assertEqual("passed", result["status"])
        self.assertEqual(4, len(result["files"]))

    def test_same_pid_stale_hash_wrong_native_size_and_missing_fields_fail(self):
        for field, value in (("readerProcessId", 100), ("pendingSha256", "0" * 64), ("actual", {"width": 1920, "height": 1080, "mode": 1}), ("actual", {}), ("buildIdentity", "{}"), ("status", "not_run")):
            changed = dict(self.receipt)
            changed[field] = value
            write_json(self.root / "recovery-read-receipt.json", changed)
            with self.subTest(field=field), self.assertRaises(ValueError):
                validate_recovery(self.root, IDENTITY)

    def test_pending_or_editor_cannot_be_recovery_pass(self):
        write_json(self.root / "display-settings.json", {"pending": True})
        with self.assertRaises(ValueError):
            validate_recovery(self.root, IDENTITY)
        write_json(self.root / "display-settings.json", {"pending": False})
        self.report["environment"]["isEditor"] = True
        write_json(self.report_dir / "player-report.json", self.report)
        with self.assertRaises(ValueError):
            validate_recovery(self.root, IDENTITY)


if __name__ == "__main__":
    unittest.main()
