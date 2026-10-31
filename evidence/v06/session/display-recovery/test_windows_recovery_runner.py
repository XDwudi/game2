"""Synthetic offline runner tests: all subprocesses are mocked; no Player is launched."""
import argparse
import contextlib
import io
import json
from pathlib import Path
import subprocess
import sys
import tempfile
from types import SimpleNamespace
import unittest
from unittest.mock import patch

ROOT = Path(__file__).resolve().parents[4]
sys.path.insert(0, str(ROOT / "tools"))
import verify_v06_display_recovery as runner

IDENTITY = {"releaseVersion": "p0.6.0", "sourceCommit": "c" * 40, "contentConfigVersion": "p0.6.0",
            "contentConfigSha256": "d" * 64, "goldenCaseSetSha256": "e" * 64, "saveSchemaVersion": 4,
            "unityEditorVersion": "6000.6.7f1", "backend": "Mono"}


def write(path, value):
    Path(path).write_text(json.dumps(value), encoding="utf-8")


class FakeProcess:
    def __init__(self, pid=101, finished=None, timeout=False):
        self.pid, self.returncode, self.timeout = pid, finished, timeout
        self.kill_calls, self.wait_calls = 0, []

    def poll(self):
        return self.returncode

    def kill(self):
        self.kill_calls += 1
        self.returncode = 1

    def wait(self, timeout):
        self.wait_calls.append(timeout)
        if self.timeout and self.returncode is None:
            raise subprocess.TimeoutExpired("synthetic private arguments", timeout)
        if self.returncode is None:
            self.returncode = 0
        return self.returncode


class RunnerTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="synthetic-recovery-runner-")
        self.root = Path(self.temp.name).resolve()
        self.shared, self.raw = self.root / "shared", self.root / "raw"
        self.shared.mkdir(); self.raw.mkdir()
        self.args = argparse.Namespace(exe=self.root / "DicePrototype.exe", trace=self.root / "normal.json",
            debug_trace=self.root / "debug.json", timeout=120)
        self.settings = self.shared / "display-settings.json"
        write(self.settings, {"pending": True, "synthetic": "tool test only"})
        self.ready = {"schema": "p06-native-display-recovery-1", "status": "awaiting_external_process_termination",
                      "writerProcessId": 101, "settingsPath": str(self.settings), "pendingSha256": runner.sha(self.settings),
                      "buildIdentity": json.dumps(IDENTITY)}
        write(self.shared / "ready-to-terminate.json", self.ready)

    def tearDown(self):
        self.temp.cleanup()

    def test_command_uses_argument_array_and_only_cache_log_destination(self):
        cmd = runner.command_for(self.args, "write", self.shared, self.shared / "writer", self.raw)
        self.assertEqual(str(self.args.exe), cmd[0])
        self.assertIn("--verify-display-recovery-write", cmd)
        self.assertEqual(str(self.raw / "write-Player.log"), cmd[cmd.index("-logFile") + 1])
        self.assertEqual("0", cmd[cmd.index("-screen-fullscreen") + 1])
        with self.assertRaises(ValueError):
            runner.command_for(self.args, "anything", self.shared, self.shared, self.raw)

    def test_visible_player_startup_overrides_hidden_inheritance_without_new_console(self):
        startup = SimpleNamespace(dwFlags=0, wShowWindow=0)
        with patch.object(runner.sys, "platform", "win32"), \
                patch.object(runner.subprocess, "STARTUPINFO", return_value=startup, create=True), \
                patch.object(runner.subprocess, "STARTF_USESHOWWINDOW", 1, create=True):
            options = runner.visible_player_startup()
        self.assertEqual({"startupinfo": startup}, options)
        self.assertEqual(1, startup.wShowWindow)
        self.assertEqual(1, startup.dwFlags & 1)
        self.assertNotIn("creationflags", options)

    def test_writer_kills_only_original_handle_and_does_not_edit_pending_bytes(self):
        original = self.settings.read_bytes()
        process = FakeProcess()
        with patch.object(runner.subprocess, "Popen", return_value=process) as popen:
            result = runner.run_writer(self.args, self.shared, self.raw, IDENTITY)
        self.assertEqual(1, process.kill_calls)
        self.assertTrue(result["terminatedOnlyOwnedHandle"])
        self.assertFalse(result["cleanupTerminatedOwnedHandle"])
        self.assertEqual(original, self.settings.read_bytes())
        self.assertFalse(popen.call_args.kwargs["shell"])
        self.assertNotIn("argv", result)
        self.assertNotIn("command", result)

    def test_wrong_ready_pid_cannot_redirect_termination_or_start_reader(self):
        self.ready["writerProcessId"] = 9999
        write(self.shared / "ready-to-terminate.json", self.ready)
        process = FakeProcess()
        with patch.object(runner.subprocess, "Popen", return_value=process), self.assertRaises(ValueError):
            runner.run_writer(self.args, self.shared, self.raw, IDENTITY)
        self.assertEqual(1, process.kill_calls, "Only the mocked original process is cleaned up")
        self.assertFalse((self.shared / "reader").exists())

    def test_ready_settings_path_cannot_escape(self):
        self.ready["settingsPath"] = str(self.root / "user-settings.json")
        write(self.shared / "ready-to-terminate.json", self.ready)
        with self.assertRaises(ValueError):
            runner.wait_for_ready(FakeProcess(), self.shared, 120)

    def test_partial_ready_json_is_retried(self):
        (self.shared / "ready-to-terminate.json").write_text("{", encoding="utf-8")
        with patch.object(runner.time, "sleep", side_effect=lambda _: write(self.shared / "ready-to-terminate.json", self.ready)):
            result = runner.wait_for_ready(FakeProcess(), self.shared, 120)
        self.assertEqual(101, result["writerProcessId"])

    def test_natural_writer_exit_is_failure_and_is_not_killed(self):
        process = FakeProcess(finished=0)
        with patch.object(runner.subprocess, "Popen", return_value=process), self.assertRaises(RuntimeError):
            runner.run_writer(self.args, self.shared, self.raw, IDENTITY)
        self.assertEqual(0, process.kill_calls)

    def test_changed_pending_bytes_and_wrong_build_identity_are_rejected(self):
        original = self.ready.copy()
        for key, value in (("pendingSha256", "0" * 64), ("buildIdentity", "{}")):
            self.ready = dict(original)
            self.ready[key] = value
            write(self.shared / "ready-to-terminate.json", self.ready)
            # Each run has its own output subdirectory, as required in production.
            with tempfile.TemporaryDirectory(dir=self.root) as temp:
                shared = Path(temp)
                raw = shared / "raw"
                raw.mkdir()
                write(shared / "display-settings.json", runner.read_json(self.settings))
                self.ready["settingsPath"] = str(shared / "display-settings.json")
                write(shared / "ready-to-terminate.json", self.ready)
                with patch.object(runner.subprocess, "Popen", return_value=FakeProcess()), self.assertRaises(ValueError):
                    runner.run_writer(self.args, shared, raw, IDENTITY)

    def test_reader_timeout_cleans_only_owned_process_and_retains_failure(self):
        process = FakeProcess(pid=202, timeout=True)
        with patch.object(runner.subprocess, "Popen", return_value=process), self.assertRaises(subprocess.TimeoutExpired):
            runner.run_reader(self.args, self.shared, self.raw)
        self.assertEqual(1, process.kill_calls)
        self.assertTrue(runner.read_json(self.shared / "reader-process.json")["cleanupTerminatedOwnedHandle"])

    def test_reader_nonzero_exit_never_becomes_success(self):
        process = FakeProcess(pid=202, finished=5)
        with patch.object(runner.subprocess, "Popen", return_value=process), self.assertRaises(RuntimeError):
            runner.run_reader(self.args, self.shared, self.raw)
        self.assertEqual(0, process.kill_calls)

    def test_new_outputs_cannot_reuse_or_escape_designated_root(self):
        with self.assertRaises(FileExistsError):
            runner.new_path(self.shared, self.root)
        with self.assertRaises(ValueError):
            runner.new_path(self.root.parent / "outside", self.root)
        with self.assertRaises(ValueError):
            runner.new_path(self.root, self.root)
        self.assertEqual(self.root / "new", runner.new_path(self.root / "new", self.root))

    def test_no_foreign_reader_receipt_is_accepted(self):
        write(self.shared / "recovery-read-receipt.json", {"evidenceDirectory": str(self.root / "other"), "writerProcessId": 101, "readerProcessId": 202})
        with self.assertRaises(ValueError):
            runner.check_actual_recovery(self.shared, IDENTITY, {"buildGuid": "test"}, {}, {"pid": 101}, {"pid": 202})

    def test_only_completed_native_report_from_launched_reader_is_used(self):
        report_dir = self.shared / "reader/player-synthetic"
        report_dir.mkdir(parents=True)
        write(self.shared / "recovery-read-receipt.json", {"evidenceDirectory": str(report_dir), "writerProcessId": 101, "readerProcessId": 202})
        report = {"probeRevision": "p06-test", "probeAssemblyMvid": "mvid"}
        write(report_dir / "player-report.json", report)
        proof = {"environment": {"platform": "WindowsPlayer", "processArchitecture": "X64", "buildGuid": "test", "probeArguments": ["synthetic"]}}
        with patch.object(runner, "validate_recovery", return_value=proof):
            result = runner.check_actual_recovery(self.shared, IDENTITY, {"buildGuid": "test"}, report, {"pid": 101}, {"pid": 202})
        self.assertNotIn("probeArguments", result["environment"])
        self.assertEqual(report, runner.read_json(report_dir / "player-report.json"), "Actual source report is not rewritten")
        proof["environment"]["buildGuid"] = "other"
        with patch.object(runner, "validate_recovery", return_value=proof), self.assertRaises(ValueError):
            runner.check_actual_recovery(self.shared, IDENTITY, {"buildGuid": "test"}, report, {"pid": 101}, {"pid": 202})

    def test_exception_launch_arguments_stay_only_in_raw_cache(self):
        self.args.identity=self.root / "identity.json"
        self.args.build_report=self.root / "build.json"
        self.args.main_results=self.root / "main"
        self.args.main_results.mkdir()
        for path in (self.args.exe,self.args.trace,self.args.debug_trace,self.args.identity,self.args.build_report,self.args.main_results / "player-report.json"):
            path.write_text("synthetic input",encoding="utf-8")
        shared=self.root / "evidence/v06/recovery-synthetic"
        raw=self.root / "artifacts/cache/v06/recovery-synthetic"
        secret="synthetic-launch-token-do-not-publish"
        with patch.object(runner,"ROOT",self.root), patch.object(runner,"prepare",return_value=(IDENTITY,{"buildGuid":"fixture"},{},shared,raw)), \
                patch.object(runner,"run_writer",side_effect=subprocess.TimeoutExpired(secret,120)), contextlib.redirect_stdout(io.StringIO()) as output:
            code=runner.execute(self.args)
        self.assertEqual(1,code)
        summary=(shared / "runner-summary.json").read_text(encoding="utf-8")
        self.assertNotIn(secret,summary)
        self.assertNotIn(secret,output.getvalue())
        self.assertIn(secret,(raw / "runner-error.txt").read_text(encoding="utf-8"))
        self.assertFalse(json.loads(summary)["passed"])


if __name__ == "__main__":
    unittest.main()
