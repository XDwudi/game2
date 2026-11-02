"""Synthetic wrapper checks only. Never launches a Player or reads/writes real user preferences."""
import contextlib
import copy
import importlib.util
import io
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest import mock

HERE = Path(__file__).resolve().parent
spec = importlib.util.spec_from_file_location("resource_runner_candidate", HERE / "run_resource_diagnostic_v3.py")
R = importlib.util.module_from_spec(spec)
spec.loader.exec_module(R)


class ResourceRunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="unit-", dir=HERE)
        self.root = Path(self.temp.name).resolve()
        assert self.root.is_relative_to(HERE.resolve()) and self.root != HERE.resolve()
        (self.root / "artifacts/cache").mkdir(parents=True)
        self.identity = {"sourceCommit": "a" * 40, "contentConfigSha256": "b" * 64,
                         "unityEditorVersion": "6000.6.7f1", "contentConfigVersion": "p0.6.0"}
        self.exe = self.root / "Builds/v07/Windows/DicePrototype.exe"
        self.exe.parent.mkdir(parents=True)
        self.exe.write_bytes(b"synthetic executable fixture, never executed")
        self.runtime = self.exe.parent / "DicePrototype_Data/Managed/DiceCraft.Runtime.dll"
        self.runtime.parent.mkdir(parents=True)
        self.runtime.write_bytes((R.FLAG + "\0" + R.SCHEMA).encode("utf-16-le"))
        self.guid = "c" * 32
        (self.runtime.parent.parent / "boot.config").write_text("build-guid=" + self.guid + "\n", encoding="utf-8")
        self.build = {"result": "Succeeded", "errors": 0, "identity": json.dumps(self.identity),
                      "platform": "StandaloneWindows64", "unity": "6000.6.7f1", "profile": "Assets/Windows_x64.asset",
                      "buildGuid": self.guid, "buildUtc": "2026-11-02T00:00:00Z", "output": str(self.exe)}
        self.build_report = self.root / "build.json"
        self.build_report.write_text(json.dumps(self.build), encoding="utf-8")
        self.bound = R.validate_inputs(self.root, self.build_report, self.identity, self.build)
        self.bound.update(directory=self.root / "artifacts/cache/new-run", timeout=270)
        self.prefs = R.fingerprint_preferences([("volume_h1", 0.25, 3), ("Screenmanager Resolution Width_h2", 1920, 4)])

    def tearDown(self):
        self.temp.cleanup()

    def report(self, status="diagnostic_completed_not_acceptance"):
        return {"schema": R.SCHEMA, "status": status, "error": None, "buildIdentity": json.dumps(self.identity),
                "buildGuid": self.guid, "editor": False, "wholeReleaseAccepted": False, "startupDisplaySuppressed": True,
                "unity": "6000.6.7f1", "platform": "WindowsPlayer", "configVersion": "p0.6.0", "configSha256": "b" * 64,
                "width": 1920, "height": 1080, "displayMode": "Windowed", "output": str(self.bound["directory"] / "diagnostic"),
                "saveDirectory": str(self.bound["directory"] / "diagnostic/isolated-saves"),
                "limitation": "Resource/idle only; not formal 180-second performance or acceptance.",
                "phases": [{"index": 0, "name": "baseline-menu"}, {"index": 1, "name": "after-idle"}],
                "idle": {"observationComplete": status == "diagnostic_completed_not_acceptance"}}

    def execute_fake(self, data=None, mutate=None, exit_code=0, timed_out=False, preferences=None):
        def launch(argv, outer, timeout):
            self.assertEqual(timeout, 270)
            self.assertFalse((outer / "diagnostic").exists())
            self.assertEqual(argv, R.player_argv(self.bound))
            if data is not None:
                (outer / "diagnostic").mkdir()
                (outer / "diagnostic/resource-report.json").write_text(json.dumps(data), encoding="utf-8")
                for name in ("idle-frames.csv", "resource-frames.csv"):
                    (outer / "diagnostic" / name).write_text("synthetic raw row\n", encoding="utf-8")
            if mutate:
                mutate()
            return {"exitCode": exit_code, "timedOut": timed_out, "ownedProcessTerminated": timed_out}
        reader = preferences or (lambda: copy.deepcopy(self.prefs))
        with mock.patch.object(R, "ROOT", self.root), mock.patch.object(R.sys, "platform", "win32"), \
                mock.patch.object(Path, "home", return_value=self.root / "fake-home"), contextlib.redirect_stdout(io.StringIO()):
            code = R.execute(self.bound, preferences_reader=reader, launch=launch)
        return code, R.read_json(self.bound["directory"] / "runner-summary.json")

    def test_input_binding_accepts_matching_guid_feature_but_rejects_old_runtime(self):
        self.assertEqual(self.bound["buildGuid"], self.guid)
        self.runtime.write_bytes(b"old runtime")
        with self.assertRaisesRegex(ValueError, "lacks"):
            R.validate_inputs(self.root, self.build_report, self.identity, self.build)

    def test_input_binding_rejects_boot_guid_identity_and_output_tampering(self):
        for key, value in (("buildGuid", "d" * 32), ("identity", "{}"), ("output", str(self.root / "wrong.exe")), ("errors", 1)):
            with self.subTest(field=key):
                changed = dict(self.build, **{key: value})
                with self.assertRaises(ValueError):
                    R.validate_inputs(self.root, self.build_report, self.identity, changed)

    def test_execute_refuses_each_changed_preflight_input_before_launch_or_output(self):
        for label, path in (("exe", self.exe), ("runtime", self.runtime),
                            ("boot", self.runtime.parent.parent / "boot.config"), ("buildReport", self.build_report)):
            with self.subTest(input=label):
                original = path.read_bytes()
                try:
                    path.write_bytes(original + b" changed after preflight")
                    with mock.patch.object(R, "ROOT", self.root), mock.patch.object(R.sys, "platform", "win32"), \
                            mock.patch.object(Path, "home", return_value=self.root / "fake-home"):
                        launch = mock.Mock()
                        with self.assertRaisesRegex(ValueError, "Input changed after preflight: " + label):
                            R.execute(self.bound, preferences_reader=lambda: self.prefs, launch=launch)
                        launch.assert_not_called()
                    self.assertFalse(self.bound["directory"].exists())
                finally:
                    path.write_bytes(original)

    def test_launch_binding_requires_full_guard_to_agree_with_current_input_hashes(self):
        guard = R.tree_guard(self.exe.parent)
        R.validate_launch_binding(self.bound, guard)
        changed = copy.deepcopy(guard)
        next(row for row in changed["files"] if row["path"] == "DicePrototype.exe")["sha256"] = "0" * 64
        with self.assertRaisesRegex(ValueError, "before-build guard"):
            R.validate_launch_binding(self.bound, changed)

    def test_output_and_timeout_boundaries_reject_reuse_escape_and_short_window(self):
        parent = self.root / "artifacts/cache"
        self.assertEqual(R.fresh_cache_directory(parent / "ok", self.root), parent / "ok")
        for bad in (parent, self.root / "escape", parent / "../escape"):
            with self.assertRaises(ValueError):
                R.fresh_cache_directory(bad, self.root)
        for timeout in (0, 239, 301, 1800):
            with self.assertRaisesRegex(ValueError, "240"):
                R.preflight(self.root, self.build_report, parent / "ok", timeout)
        self.assertFalse((parent / "ok").exists())

    def test_plan_only_does_not_create_directory_launch_or_read_registry(self):
        stream = io.StringIO()
        with mock.patch.object(R, "preflight", return_value=self.bound), mock.patch.object(R, "execute") as execute, \
                mock.patch.object(R, "read_preferences") as prefs, contextlib.redirect_stdout(stream):
            self.assertEqual(R.main(["--build-report", str(self.build_report), "--directory", str(self.bound["directory"])]), 0)
        execute.assert_not_called(); prefs.assert_not_called()
        self.assertFalse(self.bound["directory"].exists())
        plan = json.loads(stream.getvalue())
        self.assertEqual(plan["status"], "plan_only")
        self.assertFalse(plan["writesOrProcessesStarted"])
        self.assertNotIn("--verify-p0", plan["argv"])
        self.assertEqual(plan["argv"][1:3], [R.FLAG, str(self.bound["directory"] / "diagnostic")])

    def test_tree_guard_includes_backup_and_unknown_build_files_and_detects_new_bytes(self):
        backup = self.exe.parent / "DontShip_BackUpThisFolder/payload"
        backup.parent.mkdir(); backup.write_bytes(b"preserve every build byte")
        a = R.tree_guard(self.exe.parent)
        self.assertTrue(any(row["path"].endswith("BackUpThisFolder/payload") for row in a["files"]))
        backup.write_bytes(b"changed")
        self.assertNotEqual(a, R.tree_guard(self.exe.parent))
        self.assertNotIn("changed", json.dumps(R.tree_guard(self.exe.parent)))

    def test_preferences_hash_only_allowlist_and_keep_engine_separate(self):
        secret = "must-never-appear-as-raw-value"
        values = R.fingerprint_preferences([("art07.key.Roll_h7", secret, 1), ("fast_h4", 1, 4),
                                           ("Screenmanager Resolution Width_h2", 1920, 4), ("unrelated-account", secret, 1)])
        output = json.dumps(values)
        self.assertNotIn(secret, output); self.assertNotIn("unrelated-account", output)
        self.assertEqual(len(values["game"]), 2); self.assertEqual(len(values["engineScreenmanager"]), 1)
        self.assertNotEqual(values["game"][0]["sha256"], R.fingerprint_preferences([("art07.key.Roll_h7", "other", 1)])["game"][0]["sha256"])

    def test_actual_binding_rejects_editor_config_dimensions_and_acceptance_overclaim(self):
        original = self.report()
        self.assertEqual(R.report_binding_failures(original, self.bound, self.bound["directory"] / "diagnostic"), [])
        for key, value in (("editor", True), ("wholeReleaseAccepted", True), ("configSha256", "0" * 64),
                           ("width", 1280), ("displayMode", "ExclusiveFullScreen"), ("buildGuid", "0" * 32),
                           ("saveDirectory", str(self.root / "ordinary-save")), ("phases", []), ("limitation", "")):
            with self.subTest(field=key):
                self.assertTrue(R.report_binding_failures(dict(original, **{key: value}), self.bound, self.bound["directory"] / "diagnostic"))

    def test_complete_diagnostic_preserves_scope_and_raw_report_never_acceptance(self):
        data = self.report(); code, result = self.execute_fake(data)
        self.assertEqual(code, 0); self.assertEqual(result["status"], "diagnostic_completed_not_acceptance")
        self.assertEqual(result["actualDiagnostic"]["phaseHistory"], data["phases"])
        self.assertEqual(result["actualDiagnostic"]["scope"], data["limitation"])
        self.assertFalse(result["wholeReleaseAccepted"]); self.assertFalse(result["formalPerformanceAcceptance"])
        self.assertEqual(R.read_json(self.bound["directory"] / "diagnostic/resource-report.json"), data)

    def test_timeout_missing_report_preserves_failure_and_guards(self):
        code, result = self.execute_fake(None, exit_code=None, timed_out=True)
        self.assertEqual(code, 1); self.assertTrue(result["process"]["timedOut"])
        self.assertIn("resource_report_missing", result["errors"])
        self.assertTrue(result["buildFilesUnchanged"])
        self.assertTrue((self.bound["directory"] / "external-guards-after.json").exists())

    def test_external_build_mutation_cannot_be_hidden_by_completed_report(self):
        code, result = self.execute_fake(self.report(), mutate=lambda: self.exe.write_bytes(b"changed"))
        self.assertEqual(code, 1); self.assertFalse(result["buildFilesUnchanged"])
        self.assertEqual(result["actualDiagnostic"]["status"], "diagnostic_completed_not_acceptance")

    def test_ordinary_save_mutation_is_detected_and_never_restored_by_wrapper(self):
        save = self.root / "fake-home/AppData/LocalLow/XDwudi/DiceCraft P0/active-run.json"
        save.parent.mkdir(parents=True); save.write_bytes(b"original private fixture")
        code, result = self.execute_fake(self.report(), mutate=lambda: save.write_bytes(b"changed private fixture"))
        self.assertEqual(code, 1); self.assertFalse(result["ordinarySaveFilesUnchanged"])
        self.assertEqual(save.read_bytes(), b"changed private fixture")
        self.assertNotIn("private fixture", (self.bound["directory"] / "runner-summary.json").read_text(encoding="utf-8"))

    def test_actual_wrong_guid_fails_without_rewriting_raw_status_or_report(self):
        data = self.report(); data["buildGuid"] = "d" * 32
        code, result = self.execute_fake(data)
        self.assertEqual(code, 1); self.assertIn("actual_buildGuid_mismatch", result["errors"])
        self.assertEqual(result["actualDiagnostic"]["status"], data["status"])
        self.assertEqual(R.read_json(self.bound["directory"] / "diagnostic/resource-report.json"), data)

    def test_changed_game_preference_fails_even_when_raw_status_completed(self):
        after = copy.deepcopy(self.prefs); after["game"][0]["sha256"] = "0" * 64
        values = iter([self.prefs, after])
        code, result = self.execute_fake(self.report(), preferences=lambda: next(values))
        self.assertEqual(code, 1); self.assertFalse(result["ordinaryGamePreferenceValuesUnchanged"])

    def test_engine_display_preference_change_is_separate_diagnostic_fact(self):
        after = copy.deepcopy(self.prefs); after["engineScreenmanager"][0]["sha256"] = "0" * 64
        values = iter([self.prefs, after])
        code, result = self.execute_fake(self.report(), preferences=lambda: next(values))
        self.assertEqual(code, 0); self.assertFalse(result["engineScreenmanagerValuesUnchanged"])
        self.assertTrue(result["ordinaryGamePreferenceValuesUnchanged"])
        self.assertFalse(result["wholeReleaseAccepted"])

    def test_incomplete_raw_status_is_preserved_not_promoted_on_exit_zero(self):
        code, result = self.execute_fake(self.report("diagnostic_incomplete"))
        self.assertEqual(code, 1); self.assertEqual(result["actualDiagnostic"]["status"], "diagnostic_incomplete")

    def test_owned_process_timeout_kills_only_handle_with_no_shell(self):
        outer = self.root / "process-fixture"; outer.mkdir()
        process = mock.Mock(pid=4321)
        process.wait.side_effect = [subprocess.TimeoutExpired("synthetic", 270), -1]
        process.poll.side_effect = [None, -1]
        popen = mock.Mock(return_value=process)
        result = R.process_run([str(self.exe), R.FLAG, str(outer / "diagnostic")], outer, 270, popen=popen)
        process.kill.assert_called_once_with()
        self.assertTrue(result["timedOut"]); self.assertTrue(result["ownedProcessTerminated"])
        self.assertFalse(result["ownedProcessStillRunning"])
        self.assertFalse(popen.call_args.kwargs["shell"])
        self.assertEqual(result["pid"], 4321)


if __name__ == "__main__":
    unittest.main(verbosity=2)
