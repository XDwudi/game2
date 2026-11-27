"""Hand checks, regression baselines and the BX-03 gate for the bare-opening DP (tools/balance_sim/opening_dp.py)."""
import io
import json
import sys
import unittest
from fractions import Fraction
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools" / "balance_sim"))
import opening_dp as od

P110 = od.PRESETS["p110"]
P111 = od.PRESETS["p111"]
_CACHE = {}


def result(rules):
    """Module-level cache: each full DP runs once for the whole file."""
    if rules.name not in _CACHE:
        _CACHE[rules.name] = od.evaluate(rules, (160, 300, 500, 600, 1000))
    return _CACHE[rules.name]


class HandMultTests(unittest.TestCase):
    def test_hand_types(self):
        cases = {
            (6, 6, 6, 6, 6): ("五豹子", 12),
            (3, 3, 3, 3, 1): ("四条", 8),
            (3, 3, 3, 2, 2): ("三条带一对", 6),
            (3, 3, 3, 2): ("三条", 4),
            (3, 3, 2, 2, 6): ("两对", 3),
            (2, 2): ("一对", 2),
            (6,): ("散点", 1),
            (1, 2, 3, 4, 5): ("顺子", 10),
            (2, 3, 4, 5, 6): ("顺子", 10),
            # With 5 dice a straight cannot coexist with a pair; hand_mult still adds them for >5 dice.
            (1, 2, 3, 4, 5, 5): ("一对+顺子", 12),
            (2, 3, 4, 5, 6, 6, 6): ("三条+顺子", 14),
            (2, 3, 4, 5, 5): ("一对", 2),
            (1, 2, 3, 5, 6): ("散点", 1),
        }
        for dice, expected in cases.items():
            with self.subTest(dice=dice):
                self.assertEqual(od.hand_mult(dice), expected)


class ScoreTests(unittest.TestCase):
    def test_p110_hand_examples(self):
        self.assertEqual(od.score((2, 3, 5, 6, 6), 0, P110), 22 * 2 * 3)          # all blue x3
        self.assertEqual(od.score((4, 4, 4), 1, P110), 15 * 4 * 2 * 5 // 2)      # (12+3)x4 x2 red x2.5 = 300
        self.assertEqual(od.score((1, 2, 3, 4, 5), 0, P110), 150)                # straight, two single reds
        self.assertEqual(od.score((1, 1, 2, 3, 6), 0, P110), 45)                 # 13x2x1.75 = 45.5 -> 45
        self.assertEqual(od.score((4, 4, 4, 4, 1), 0, P110), 952)                # 17x8 x2 x3.5

    def test_p111_hand_examples(self):
        self.assertEqual(od.score((1, 1, 2, 3, 6), 0, P111), 26)                 # red group of 2 is x1
        self.assertEqual(od.score((1, 2, 3, 4, 5, 5), 0, P111), 20 * 12)         # pair + straight = 12 (6 dice)
        self.assertEqual(od.score((2, 3, 4, 5, 6), 1, P111), 23 * 10)            # (20+3) x straight 10, one red
        self.assertEqual(od.score((6, 6, 5), 2, P111), 57)                       # (17+6)x2x1.25 = 57.5 -> 57
        self.assertEqual(od.score((4, 4, 4, 4, 1), 0, P111), 612)                # 17x8 x1.5 x3
        self.assertEqual(od.score((1, 1, 1, 2, 3), 0, P111), 8 * 4 * 2)          # red group 3 = x2

    def test_unused_roll_bonus_and_colour_off(self):
        self.assertEqual(od.score((6,), 2, od.COLOUR_OFF), 12)
        self.assertEqual(od.score((4, 4, 4, 4, 4), 0, od.COLOUR_OFF), 240)
        self.assertEqual(od.score((), 2, P111), 0)

    def test_roll_probabilities_sum_to_one(self):
        for n in range(6):
            with self.subTest(n=n):
                outs = od.roll_outcomes(n)
                self.assertEqual(sum(p for _, p in outs), Fraction(1))
        self.assertEqual(len(od.roll_outcomes(5)), 252)


class RegressionTests(unittest.TestCase):
    """Baselines from the scratch scripts; colour-blind p110 numbers differ only by tie-breaking (see report)."""

    def test_colour_off_ev(self):
        self.assertAlmostEqual(result(P110)["colourOffEv"], 114.7, delta=0.05)

    def test_p110_optimal(self):
        r = result(P110)["optimal"]
        self.assertAlmostEqual(r["ev"], 337.8, delta=0.05)
        self.assertAlmostEqual(r["shares"]["all_blue"] * 100, 71, delta=0.5)
        self.assertAlmostEqual(r["pass3"]["300"] * 100, 99.6, delta=0.05)
        self.assertAlmostEqual(r["pass3"]["600"] * 100, 82.5, delta=0.05)
        self.assertAlmostEqual(r["pass3"]["1000"] * 100, 41.3, delta=0.05)

    def test_p110_colour_blind(self):
        c = result(P110)["colourBlind"]
        # Deterministic tie-break of this module; the scratch script's set-order tie-break gave 255.5 / 60.1 / 22.4.
        self.assertAlmostEqual(c["ev"], 255.8, delta=0.05)
        self.assertAlmostEqual(c["pctOfOptimal"], 76, delta=0.5)
        self.assertAlmostEqual(c["pass3"]["300"] * 100, 92.3, delta=0.05)
        self.assertAlmostEqual(c["pass3"]["600"] * 100, 60.2, delta=0.05)
        self.assertAlmostEqual(c["pass3"]["1000"] * 100, 22.5, delta=0.05)
        # The old baseline 255.5 must lie inside the tie-breaking range.
        self.assertLessEqual(c["tieRange"]["evMin"], 255.55)
        self.assertGreaterEqual(c["tieRange"]["evMax"], 255.45)

    def test_p111(self):
        res = result(P111)
        o, c = res["optimal"], res["colourBlind"]
        self.assertAlmostEqual(o["ev"], 185, delta=0.5)
        self.assertAlmostEqual(o["shares"]["all_blue"] * 100, 26, delta=0.5)
        self.assertAlmostEqual(o["shares"]["all_red"] * 100, 22, delta=0.5)
        self.assertAlmostEqual(o["shares"]["red_group_3plus"] * 100, 35, delta=0.5)
        self.assertAlmostEqual(o["shares"]["no_colour_bonus"] * 100, 38, delta=0.5)
        self.assertAlmostEqual(o["shares"]["straight"] * 100, 5.4, delta=0.05)
        self.assertAlmostEqual(c["ev"], 156, delta=0.5)
        self.assertAlmostEqual(c["pctOfOptimal"], 85, delta=0.5)
        self.assertAlmostEqual(o["pass3"]["160"] * 100, 97, delta=0.5)
        self.assertAlmostEqual(o["pass3"]["500"] * 100, 42, delta=0.5)

    def test_colour_partition_sums_to_one(self):
        for rules in (P110, P111):
            sh = result(rules)["optimal"]["shares"]
            total = sh["all_blue"] + sh["all_red"] + sh["red_group_only"] + sh["no_colour_bonus"]
            self.assertAlmostEqual(total, 1.0, places=12)

    def test_cli_json_is_utf8(self):
        buf = io.BytesIO()

        class Out:
            buffer = buf
        old = sys.stdout
        sys.stdout = Out()
        try:
            od.main(["--rules", "p111", "--targets", "160", "--json"])
        finally:
            sys.stdout = old
        data = json.loads(buf.getvalue().decode("utf-8"))
        self.assertEqual(data["rules"]["name"], "p111")
        self.assertIn("160", data["optimal"]["pass3"])


class GateBX03Tests(unittest.TestCase):
    """BX-03 (p111): all-blue <= 35%, no-colour-bonus >= 30%, colour-blind EV >= 84.5% of optimal.

    The third line read 85% when written; that figure was the plan's rounded estimate. The exact value with the
    confirmed colour numbers is 84.68%. On 2026-11-27 the user chose to keep the numbers and record the line as
    84.5% (task file P0-V111, Q1), instead of lowering the five-red group from x4.5 to x4.
    """

    def test_all_blue_at_most_35pct(self):
        self.assertLessEqual(result(P111)["optimal"]["shares"]["all_blue"], 0.35)

    def test_no_colour_bonus_at_least_30pct(self):
        self.assertGreaterEqual(result(P111)["optimal"]["shares"]["no_colour_bonus"], 0.30)

    def test_colour_blind_at_least_84_5pct_of_optimal(self):
        self.assertGreaterEqual(result(P111)["colourBlind"]["pctOfOptimal"], 84.5)


if __name__ == "__main__":
    unittest.main()
