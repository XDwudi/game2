"""Synthetic rejection-driver checks; no actual Player/Editor/Registry access."""
import contextlib
import copy
import io
from pathlib import Path
import unittest
from unittest import mock

import run_bad_arguments as D
import test_resource_runner as fixtures


class BadArgumentDriverTests(unittest.TestCase):
    def setUp(self):
        self.fixture = fixtures.ResourceRunnerTests()
        self.fixture.setUp()
        self.bound = self.fixture.bound
        self.trace = self.fixture.root / "normal-trace.json"
        self.debug = self.fixture.root / "debug-trace.json"
        self.trace.write_text("{}", encoding="utf-8"); self.debug.write_text("{}", encoding="utf-8")
        self.launches = []

    def tearDown(self):
        self.fixture.tearDown()

    def guard(self, bound):
        return {"build": D.R.tree_guard(bound["exe"].parent), "ordinarySaves": {"exists": False, "files": []},
                "preferences": copy.deepcopy(self.fixture.prefs)}

    def run_fake(self, name, expected, result=None, message=None, extra=None, guard=None):
        self.bound["directory"].mkdir(parents=True, exist_ok=True)
        def launch(argv, case, timeout):
            self.launches.append((argv, timeout))
            if message is not False:
                content = message if isinstance(message, str) else ("System.ArgumentException: " + expected + "\n  at DiceCraft.Verification.Art07ResourceDiagnostic.Configure()\n")
                (case / "Player.log").write_text(content, encoding="utf-8")
            if extra:
                extra(case)
            return result or {"exitCode": 1, "timedOut": False, "ownedProcessTerminated": False, "ownedProcessStillRunning": False}
        return D.run_case(self.bound, name, expected, self.trace, self.debug, guard=guard or self.guard, launch=launch)

    def test_four_expected_rejections_need_signature_exit_and_no_legacy_output(self):
        for name, expected in D.CASES:
            with self.subTest(case=name):
                result = self.run_fake(name, expected)
                self.assertTrue(result["passed"], result["errors"])
                self.assertFalse(result["wholeReleaseAccepted"])
                self.assertTrue(result["observations"]["resourceInputUnchanged"])
                self.assertFalse(any(result["observations"]["legacyDirectoriesCreated"].values()))
        self.assertEqual(len(self.launches), 4)
        self.assertTrue(all(timeout == 45 for _, timeout in self.launches))

    def test_exit_one_without_specific_resource_error_is_not_pass(self):
        result = self.run_fake("missing-path", D.CASES[0][1], message="unrelated startup failure")
        self.assertFalse(result["passed"])
        self.assertIn("specific_resource_argument_rejection_not_observed", result["errors"])

    def test_timeout_or_launch_failure_never_counts_as_expected_rejection(self):
        for index, process in enumerate(({"exitCode": None, "timedOut": True, "ownedProcessTerminated": True},
                                         {"exitCode": None, "orchestrationError": "FileNotFoundError: synthetic missing executable"},
                                         {"exitCode": 0}, {"exitCode": -1})):
            self.bound["directory"] = self.fixture.root / ("artifacts/cache/process-negative-" + str(index))
            result = self.run_fake("missing-path", D.CASES[0][1], result=process)
            self.assertFalse(result["passed"])
            self.assertIn("expected_normal_process_exit_1_not_observed", result["errors"])

    def test_old_probe_directory_or_log_is_not_hidden_by_expected_resource_message(self):
        expected = D.CASES[2][1]
        result = self.run_fake("mixed-verify", expected, extra=lambda case: (case / "legacy-verify").mkdir())
        self.assertFalse(result["passed"]); self.assertIn("legacy_bootstrap_output_created", result["errors"])
        result = self.run_fake("mixed-pacing", expected,
                               message="System.ArgumentException: " + expected + " Art07ResourceDiagnostic\nDiceCraft.FramePacingExperiment.Start")
        self.assertFalse(result["passed"]); self.assertIn("legacy_probe_log_observed", result["errors"])

    def test_crash_with_expected_message_still_fails(self):
        expected = D.CASES[0][1]
        result = self.run_fake("missing-path", expected,
                               message="Art07ResourceDiagnostic " + expected + "\nNullReferenceException: unrelated crash")
        self.assertFalse(result["passed"]); self.assertIn("unrelated_crash_or_exception_observed", result["errors"])

    def test_nonempty_input_modification_or_sampling_output_still_fails(self):
        result = self.run_fake("nonempty-path", D.CASES[1][1], extra=lambda case: (case / "resource/sentinel.txt").write_bytes(b"changed"))
        self.assertFalse(result["passed"]); self.assertIn("rejected_resource_output_was_created_or_changed", result["errors"])
        result = self.run_fake("mixed-verify", D.CASES[2][1], extra=lambda case: (case / "player-report.json").write_text("{}", encoding="utf-8"))
        self.assertFalse(result["passed"]); self.assertIn("unexpected_probe_report_or_sampling_output", result["errors"])

    def test_changed_game_preference_fails_without_rewriting_it(self):
        count = 0
        def guard(bound):
            nonlocal count
            value = self.guard(bound); count += 1
            if count > 1:
                value["preferences"]["game"][0]["sha256"] = "0" * 64
            return value
        result = self.run_fake("missing-path", D.CASES[0][1], guard=guard)
        self.assertFalse(result["passed"]); self.assertIn("gamePrefsUnchanged_failed", result["errors"])

    def test_default_plan_only_does_not_launch_or_prepare_sentinel(self):
        folder = self.fixture.root / "evidence/v07/session"; folder.mkdir(parents=True)
        for name in ("normal-run-trace.json", "debug-24-trace.json"):
            (folder / name).write_text("{}", encoding="utf-8")
        with mock.patch.object(D.R, "ROOT", self.fixture.root), mock.patch.object(D.R, "preflight", return_value=self.bound), \
                mock.patch.object(D, "run_case") as run, mock.patch.object(D, "external_guards") as guard, \
                contextlib.redirect_stdout(io.StringIO()):
            self.assertEqual(D.main(["--build-report", str(self.fixture.build_report), "--directory", str(self.bound["directory"])]), 0)
        run.assert_not_called(); guard.assert_not_called()
        self.assertFalse(self.bound["directory"].exists())


if __name__ == "__main__":
    unittest.main(verbosity=2)
