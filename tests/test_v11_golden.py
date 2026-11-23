"""Regression guards for the independent duplicate-vector contract."""
import copy
import sys
import unittest
from unittest.mock import patch

sys.dont_write_bytecode = True
from tools import v11_golden as golden


class V11GoldenContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cfg = golden.read(golden.CONFIG)
        cls.cases = golden.build_cases(cls.cfg)

    def test_historical_inventory_agrees_with_rule_card_tables(self):
        inventory = golden.historical_inventory()
        self.assertEqual(122, len(set().union(*(set(v) for v in inventory.values()))))
        self.assertEqual(83, len(golden.CONDITIONAL))
        self.assertEqual(39, len(golden.ALWAYS))

    def test_existing_golden_payloads_are_not_inputs_to_new_expectations(self):
        # Construction uses only config and explicit recipes, not an existing oracle.
        with patch.object(golden, "read", side_effect=AssertionError("file read during generation")):
            self.assertEqual(self.cases, golden.build_cases(self.cfg))

    def test_missing_card_and_mislabeled_trigger_are_rejected(self):
        missing = [c for c in self.cases if c["id"] != "C46-two_hit"]
        with self.assertRaisesRegex(ValueError, "missing two_hit"):
            golden.coverage(missing)
        mutated = copy.deepcopy(self.cases)
        next(c for c in mutated if c["id"] == "C02-two_miss")["expected"]["triggeredSourceIds"] = ["ctl_01"]
        with self.assertRaisesRegex(ValueError, "claimed trigger"):
            golden.coverage(mutated)

    def test_duplicate_definition_is_legal_but_duplicate_identity_is_not(self):
        inp = golden.make_input(self.cfg, "C46", 2, {})
        golden.validate(inp, self.cfg)
        inp["controllers"][1]["instanceId"] = inp["controllers"][0]["instanceId"]
        with self.assertRaisesRegex(ValueError, "IDs/count"):
            golden.validate(inp, self.cfg)

    def test_budget_and_fake_empty_slot_misses_are_rejected(self):
        inp = golden.make_input(self.cfg, "C09", 3, {"dice": "2b"})
        self.assertEqual(2, inp["capacity"])
        inp["capacity"] = 4
        with self.assertRaisesRegex(ValueError, "C09 capacity"):
            golden.validate(inp, self.cfg)
        inp = golden.make_input(self.cfg, "C151", 2, {})
        inp["ordinaryControllerSlots"] = 2
        with self.assertRaisesRegex(ValueError, "ordinary slots"):
            golden.validate(inp, self.cfg)

    def test_config_normalization_keeps_scoring_parameters(self):
        baseline = golden.read(golden.BASELINE)
        historical = golden.normalized_config(baseline)
        self.assertEqual(historical, golden.normalized_config(self.cfg))
        for key, value in (("priceRule", "6（整局限买2次）"), ("priceRule", "未知"), ("baseBuyPrice", 7)):
            with self.subTest(service="O07", key=key, value=value):
                modified = copy.deepcopy(self.cfg)
                next(s for s in modified["services"] if s["id"] == "O07")[key] = value
                self.assertNotEqual(historical, golden.normalized_config(modified))
        mutated = copy.deepcopy(self.cfg)
        next(c for c in mutated["controllers"] if c["id"] == "C46")["parameters"]["scoreMultiplier"] += 1
        self.assertNotEqual(golden.normalized_config(baseline), golden.normalized_config(mutated))

    def test_published_vectors_match_reference_and_full_coverage(self):
        self.assertEqual(self.cases, golden.read(golden.GOLDEN)["cases"])
        self.assertEqual(golden.SCORING, set(golden.coverage(self.cases)))


if __name__ == "__main__":
    unittest.main()
