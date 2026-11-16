"""Evidence integrity checks for the strict v1.1 paired comparison."""
import copy
import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location("tables", Path(__file__).with_name("v10_report_tables.py"))
tables = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tables)


class PairedEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.base = {"firstSeed": 5000, "seeds": 2, "runs": 2, "errors": 0,
                     "logs": [{"seed": 5000, "won": False}, {"seed": 5001, "won": True}]}
        self.current = copy.deepcopy(self.base)

    def test_pairs_by_seed_not_file_order(self):
        self.current["logs"].reverse()
        baseline, current, seeds = tables.checked_seed_pair(self.base, self.current)
        self.assertEqual(seeds, [5000, 5001])
        self.assertEqual(baseline[5001], current[5001])

    def test_rejects_incomplete_duplicate_or_error_logs(self):
        for mutation in (
            lambda d: d["logs"].pop(),
            lambda d: d["logs"].append(d["logs"][0]),
            lambda d: d.update(errors=1),
            lambda d: d["logs"][0].update(fail="ERROR:Rejected Buy"),
            lambda d: d.update(rejectedShopCommands=1),
            lambda d: d.update(plannerSelfCheck={"mispredicted": 1}),
        ):
            with self.subTest(mutation=mutation):
                bad = copy.deepcopy(self.current)
                mutation(bad)
                with self.assertRaises(ValueError):
                    tables.checked_seed_pair(self.base, bad)

    def test_rejects_different_seed_sets(self):
        self.current["firstSeed"] += 1
        for row in self.current["logs"]:
            row["seed"] += 1
        with self.assertRaisesRegex(ValueError, "same nonempty seed set"):
            tables.checked_seed_pair(self.base, self.current)

    def test_rejects_empty_evidence(self):
        empty = {"firstSeed": 5000, "seeds": 0, "runs": 0, "errors": 0, "logs": []}
        with self.assertRaisesRegex(ValueError, "nonempty"):
            tables.checked_seed_pair(empty, empty)


if __name__ == "__main__":
    unittest.main()
