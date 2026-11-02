"""Only the observed JsonUtility empty-string compatibility; original error remains unchanged."""
import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("resource_runner_v3", Path(__file__).with_name("run_resource_diagnostic_v3.py"))
R3 = importlib.util.module_from_spec(spec)
spec.loader.exec_module(R3)


class JsonUtilityErrorTests(unittest.TestCase):
    def summary(self, error):
        return {"errors": [], "process": {"exitCode": 0, "timedOut": False},
                "actualDiagnostic": {"status": "diagnostic_completed_not_acceptance", "error": error, "idleObservationComplete": True},
                "wholeReleaseAccepted": False, "formalPerformanceAcceptance": False}

    def test_jsonutility_empty_error_is_completed_without_rewriting_value(self):
        value = self.summary(""); before = copy.deepcopy(value)
        self.assertEqual(R3.classify(value), ("diagnostic_completed_not_acceptance", 0))
        self.assertEqual(value, before)

    def test_null_error_remains_supported_without_rewriting_value(self):
        value = self.summary(None); before = copy.deepcopy(value)
        self.assertEqual(R3.classify(value), ("diagnostic_completed_not_acceptance", 0))
        self.assertEqual(value, before)

    def test_nonempty_error_even_whitespace_is_never_promoted(self):
        for error in (" ", "actual diagnostic failed", False, 0):
            with self.subTest(error=error):
                value = self.summary(error); before = copy.deepcopy(value)
                self.assertEqual(R3.classify(value), ("diagnostic_failed_or_incomplete", 1))
                self.assertEqual(value, before)


if __name__ == "__main__":
    unittest.main(verbosity=2)
