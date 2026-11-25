"""Guards for the independent p1.1.1 reference scorer and its golden vectors (tools/v111_golden.py).

The numbers asserted here are the worked examples of docs/specs/v111/RULES_DELTA.md, written out by hand.
Nothing in this file reads or runs the game's C# code.
"""
import copy
import sys
import unittest
from fractions import Fraction
from unittest.mock import patch

sys.dont_write_bytecode = True
from tools import v111_golden as golden


class V111GoldenTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.cfg = golden.load(golden.CONFIG)
        cls.built = golden.build_all(cls.cfg)
        cls.cases, cls.infos, cls.excluded, cls.seen, cls.tags = cls.built
        cls.by_id = {c["id"]: c for c in cls.cases}

    def case(self, start):
        """The one new case whose description starts with `start`."""
        found = [c for c in self.cases if c["source"] == "new" and c["description"].startswith(start)]
        self.assertEqual(1, len(found), start)
        return found[0]

    def expect(self, start, **fields):
        expected = self.case(start)["expected"]
        for key, value in fields.items():
            where = expected["afterScorePlan"] if key in expected["afterScorePlan"] else expected
            self.assertEqual(value, where[key], f"{start}: {key}")
        return expected

    # ---- files on disk, counts, coverage ----

    def test_check_files_match_the_generator(self):
        text, provenance = golden.artifacts(self.cfg, self.built)
        self.assertEqual(text, golden.GOLDEN.read_bytes().decode("utf-8"))
        self.assertEqual(provenance, golden.EVIDENCE.read_bytes().decode("utf-8"))
        published = golden.load(golden.GOLDEN)
        self.assertEqual((golden.SCHEMA, "p1.1.1", len(self.cases)), (published["schema"], published["configVersion"], published["caseCount"]))
        self.assertEqual(self.cases, published["cases"])

    def test_case_count_floor_and_sources(self):
        by_source = {}
        for c in self.cases:
            by_source[c["source"]] = by_source.get(c["source"], 0) + 1
        self.assertGreaterEqual(len(self.cases), 2100)
        self.assertGreaterEqual(by_source["new"], 250)
        self.assertGreaterEqual(by_source["matrix"], 550)
        totals = {"GoldenScores.json": 160, "GoldenScoresV09.json": 260, "GoldenScoresV10.json": 446, "GoldenScoresV11.json": 459}
        for name, total in totals.items():
            dropped = [e for e in self.excluded if e["file"].endswith("/" + name)]
            self.assertEqual(total, by_source[name] + len(dropped), name)
        # Every dropped input was dropped because of a removed card, and says so.
        self.assertEqual(8, len(self.excluded))
        self.assertTrue(all("已移除" in e["reason"] for e in self.excluded))
        self.assertEqual(len(self.cases), len(self.by_id))

    def test_every_case_is_a_legal_snapshot_with_the_full_field_set(self):
        expected_keys = set(self.cases[0]["expected"])
        plan_keys = set(self.cases[0]["expected"]["afterScorePlan"])
        for c in self.cases:
            self.assertEqual(golden.INPUT_KEYS, set(c["input"]), c["id"])
            self.assertEqual(expected_keys, set(c["expected"]), c["id"])
            self.assertEqual(plan_keys, set(c["expected"]["afterScorePlan"]), c["id"])
            self.assertFalse({k["definitionId"] for k in c["input"]["controllers"]} & golden.REMOVED, c["id"])
            if c["source"] not in ("new", "matrix"):
                self.assertIn("derivedFrom", c)
        for key in ("colorAdd100", "additiveEntries", "additiveTotal100", "enchantAdditiveCount", "multiplierFactors", "scoreNumerator", "scoreDenominator"):
            self.assertIn(key, expected_keys)
        for key in ("growthChanges", "glassRiskDieIds", "faceChanges", "cultivationSourceBindings", "goldSpent"):
            self.assertIn(key, plan_keys)
        self.assertNotIn("colorMult100", expected_keys)
        self.assertNotIn("enchantMultiplierCount", expected_keys)

    def test_coverage_of_changed_cards_and_section_14(self):
        table, problems = golden.coverage(self.cfg, self.cases, self.infos, self.tags)
        self.assertEqual([], problems)
        self.assertEqual(golden.SCORING, set(table))
        for card in golden.CHANGED:
            self.assertGreaterEqual(table[card]["hit"], 1, card)
            self.assertGreaterEqual(table[card]["miss"] + table[card]["disabled"], 1, card)
            if card in golden.CONDITIONAL:
                self.assertGreaterEqual(table[card]["miss"], 1, card)
        for name, members in golden.CATEGORY.items():
            for card in members:
                for kind in ("duplicate", "mimic", "disabled"):
                    self.assertGreaterEqual(table[card][kind], 1, f"{name} {card} {kind}")
        for tag in golden.SPEC14_TAGS:
            self.assertTrue(self.tags.get(tag), tag)

    def test_coverage_check_notices_a_missing_case(self):
        keep = [(c, i) for c, i in zip(self.cases, self.infos) if not any(k["definitionId"] == "C155" for k in c["input"]["controllers"])]
        _, problems = golden.coverage(self.cfg, [c for c, _ in keep], [i for _, i in keep], self.tags)
        self.assertIn("C155: no triggering case", problems)
        tags = dict(self.tags, 红组7=[])
        _, problems = golden.coverage(self.cfg, self.cases, self.infos, tags)
        self.assertIn("section 14 item without a case: 红组7", problems)

    def test_new_cases_do_not_read_any_file(self):
        with patch.object(golden, "load", side_effect=AssertionError("file read while building new cases")):
            cases, _, _ = golden.build_new_cases(self.cfg)
        self.assertEqual([c for c in self.cases if c["source"] in ("new", "matrix")], cases)

    def test_hand_checks_fail_when_the_scorer_is_wrong(self):
        with patch.object(golden, "halve_hand_mult", lambda value: max(100, (value + 1) // 2)):
            with self.assertRaisesRegex(ValueError, "hand-computed"):
                golden.build_new_cases(self.cfg)

    def test_config_contract_and_snapshot_legality_are_enforced(self):
        golden.check_config(self.cfg)
        for card, key, value in (("C46", "additiveBonus100", 300), ("C53", "factorUnits2", [1, 2, 3, 2, 4, 4, 6, 6, 8]), ("C54", "growthEvent", 15)):
            broken = copy.deepcopy(self.cfg)
            next(c for c in broken["controllers"] if c["id"] == card)["parameters"][key] = value
            with self.assertRaisesRegex(ValueError, f"{card}.{key}"):
                golden.check_config(broken)
        old = copy.deepcopy(self.cfg)
        old["configVersion"] = "p1.1.0"
        with self.assertRaisesRegex(ValueError, "stop and report"):
            golden.check_config(old)
        base = self.case("§4.4 C137 明星 4")["input"]
        for edit, why in ((lambda i: i["controllers"][0].update(starPip=0), "starPip"),
                          (lambda i: i["controllers"][0].update(definitionId="C90"), "removed controller"),
                          (lambda i: i["controllers"][0].update(turnoverUnits2=5), "turnoverUnits2"),
                          (lambda i: i.update(stageTarget="0"), "target"),
                          (lambda i: i["handLevels"].append({"handId": "pair", "level": 13}), "hand levels")):
            inp = copy.deepcopy(base)
            edit(inp)
            with self.assertRaisesRegex(ValueError, why):
                golden.validate("T", inp, self.cfg)

    # ---- RULES_DELTA 2: the formula ----

    def test_section_2_examples(self):
        self.assertEqual(1200, golden.final_score(20, 300, 200, 700, Fraction(5, 2), Fraction(1, 2))[0])
        e = self.expect("§2 例 基础分20", score="1200", baseTotal="20", totalHandMult100=300, redMult100=200, additiveTotal100=700,
                        controllerMultiplierNumerator="5", controllerMultiplierDenominator="2")
        self.assertEqual([("gold", 50), ("controller", 200), ("controller", 300), ("enchantment", 150)], [(x["kind"], x["amount100"]) for x in e["additiveEntries"]])
        self.expect("§2 例 续：黄金骰再计一次", score="1440", baseTotal="24", additiveTotal100=700)
        self.assertEqual(137, golden.halve_hand_mult(275))
        self.assertEqual(274, golden.final_score(200, 137, 100, 0)[0])
        self.expect("§2 例 1 级一对", score="274", baseTotal="200", handMultBeforeModifier100=275, totalHandMult100=137)

    def test_section_12_2_example(self):
        total, exact = golden.final_score(13, 112, 100, 25 + 50 + 300)
        self.assertEqual((69, Fraction(6916, 100)), (total, exact))
        self.assertEqual(Fraction(475, 100), Fraction(100 + 375, 100))

    def test_section_14_example(self):
        e = self.expect("§14 手算例 4、4、4、4、1", score="3060", baseTotal="17", totalHandMult100=800, redCount=4, redMult100=300, additiveTotal100=150,
                        triggeredSourceIds=["ctl_01", "ctl_03"])
        self.assertEqual([("C155", "2", "1"), ("C53", "3", "2")], [(f["abilityId"], f["numerator"], f["denominator"]) for f in e["multiplierFactors"]])
        self.expect("§14 手算例 续", score="1530")
        self.assertEqual((3060, 1530), (17 * 8 * 3 * 5 // 2 * 2 * 3 // 2, 17 * 8 * 3 * 5 * 2 * 3 // 8))

    # ---- RULES_DELTA 3: colours, levels, the red group ----

    def test_section_3_1_colour_examples(self):
        self.expect("§3.1 例 3 级全蓝", colorAdd100=175, score="104")
        self.expect("§3.1 例 12 级全红", colorAdd100=650, score="135")
        self.expect("无卡 全蓝 0 级", colorAdd100=25, score="47", scoreNumerator="95", scoreDenominator="2")
        self.expect("无卡 全红 0 级", colorAdd100=50, score="27")
        self.expect("§3.1 例 11 级有两次 C43", upgradeRequested=2, upgradeApplied=1)
        self.expect("实际 12 级的一对在打回原形下", totalHandMult100=200, upgradeRequested=1, upgradeApplied=0)

    def test_section_3_2_red_group_example(self):
        self.expect("§3.2 例 提交红1、红4、蓝6", redGroupKey=4, redCount=1, redMult100=100, score="11")
        self.expect("§3.2 例 C24 把红 1 并入", redGroupKey=4, redCount=2, redMult100=100)
        self.expect("§3.2 例 C24 + C73：", redCount=3, redPoolCount=1, redMult100=200, score="22")
        self.expect("§3.2 例 C24 + C73 + C146", redCount=4, redBonusCount=1, redMult100=300, score="33")
        self.expect("§3.2 例 再加 C25", redGroupKey=0, redCount=4, redMult100=300)
        self.expect("§3.2 例 C69 只为", pipRetriggers=[1, 1, 0], colorId="", score="48")
        self.expect("§3.2 例 提交红 4、4", redCount=2, redMult100=100, score="24")
        table = {"红组 3 颗": 200, "红组 4 颗": 300, "红组 5 颗": 450, "红组 6 颗": 600, "红组 7 颗": 800, "红组 8 颗": 1100, "红组 5+3+1=9": 1100}
        for start, mult in table.items():
            self.expect(start, redMult100=mult)

    # ---- RULES_DELTA 4: card examples ----

    def test_section_4_card_examples(self):
        self.expect("§4.1 C02 全蓝一颗", score="22", controllerMultiplierNumerator="3")
        self.expect("§4.1 C31 五颗 3", score="675")
        self.expect("§4.1 C33 5、2 满足", triggeredSourceIds=["ctl_01"], score="26")
        self.expect("§4.1 C33 2、5 不满足", triggeredSourceIds=[], score="8")
        self.expect("§4.1 C41 复制 ×3", controllerMultiplierNumerator="9", score="67")
        self.expect("§4.1 C41 复制 +3", additiveTotal100=600, multiplierFactors=[], score="42")
        self.expect("§4.1 C53 票 0", controllerMultiplierNumerator="1", controllerMultiplierDenominator="2", score="17")
        self.expect("§4.1 C53 票 8", controllerMultiplierNumerator="4", score="136")
        self.expect("§4.1 C71 两颗留池 6 点", controllerMultiplierNumerator="9", controllerMultiplierDenominator="4")
        self.expect("§4.1 C87 五同", controllerMultiplierNumerator="9", controllerMultiplierDenominator="4", score="506")
        self.expect("§7.2 例 两颗黄金面", additiveTotal100=125, controllerMultiplierNumerator="169", controllerMultiplierDenominator="100", score="91")
        self.expect("§4.1 C103 3 永久 + 2 临时", controllerMultiplierNumerator="3")
        self.expect("§4.1 C103 恰为 4 颗永久", controllerMultiplierNumerator="2")
        self.expect("§4.1 C138 3 金、两个来源", triggeredSourceIds=["ctl_01"], goldSpent=2, score="85")
        self.expect("§4.2 C47 5 张 +3", additiveTotal100=300, score="136")
        self.expect("§4.2 C49 两张、两个 5", additiveTotal100=625, controllerMultiplierDenominator="1", score="188")
        self.expect("§4.2 C49 两张、零个 5", controllerMultiplierNumerator="1", controllerMultiplierDenominator="4", triggeredSourceIds=["ctl_01", "ctl_02"], score="5")
        self.expect("§4.2 C63 5 永久 + 1 临时", additiveTotal100=250)
        self.expect("§4.2 C139 69 金", additiveTotal100=300)
        self.expect("§4.2 C139 9 金", additiveTotal100=0, triggeredSourceIds=[])
        self.expect("§4.2 C150 第 4 手", additiveTotal100=600)
        self.expect("§4.2 C151 五栏、占三", additiveTotal100=250)
        self.expect("§4.2 C152 两张普通", additiveTotal100=120)
        self.expect("§4.3 C10 两颗有效 1 点", pipContributions=[16, 16, 3])
        self.expect("§4.3 C20 满五", pipContributions=[3, 3, 5, 4, 30])
        self.expect("§4.3 C27 四同 +20", baseTotal="28")
        self.expect("§4.3 C27 双三条 +40", baseTotal="61")
        self.expect("§4.3 C50 点数合计 20", baseTotal="50")
        self.expect("§4.3 C80 1、1、6", baseTotal="38")
        self.expect("§4.3 C93 贡献 5", pipContributions=[15])
        self.expect("§4.3 C107 2、3、4", pipContributions=[2, 23, 24])
        self.expect("§4.3 C143 2、2、3、3、3", pipRetriggers=[2, 2, 0, 0, 0], pipSum="21")
        self.expect("§4.3 C84 三同", handMultAdd100=200, totalHandMult100=600)
        self.expect("§4.4 C19 1、2、5", additiveTotal100=250)
        self.expect("§4.4 C19 不要奇数", additiveTotal100=150)
        self.expect("§4.4 C91 6、2、5、1、4", additiveTotal100=250)
        self.expect("§4.4 C91 2、2、1", triggeredSourceIds=[])
        self.expect("§4.4 C137 明星 4", additiveTotal100=250, score="63")
        self.expect("§4.5 C155 4、4、4、4、1、1、6", controllerMultiplierNumerator="4", score="2304")
        self.expect("§4.5 C155 三颗 4 和两颗 1", multiplierFactors=[], score="252")
        self.expect("§4.5 C156 有效目标 321、累计 160", additiveTotal100=500)
        self.expect("§4.5 C156 目标 321、累计 161", triggeredSourceIds=[])
        self.expect("§4.5 C156 目标 320、累计 160", triggeredSourceIds=[])
        self.expect("§11 例 厚墙目标 2430、累计 1214", additiveTotal100=500)
        self.expect("§11 例 厚墙目标 2430、累计 1215", additiveTotal100=0)
        for decay in range(4):
            self.expect(f"§4.2 / §5.1 C141 已衰减 {decay} 次", additiveTotal100=400 - 100 * decay)

    # ---- RULES_DELTA 5: growth ----

    def test_section_5_streak_chain_175_160_152(self):
        first, second, third = (self.by_id[f"CHAIN-C54-{k}"] for k in (1, 2, 3))
        factor = lambda c: Fraction(int(c["expected"]["controllerMultiplierNumerator"]), int(c["expected"]["controllerMultiplierDenominator"]))  # noqa: E731
        self.assertEqual([Fraction(175, 100), Fraction(160, 100), Fraction(152, 100)], [factor(first), factor(second), factor(third)])
        self.assertEqual(golden.catchup_for(self.cfg, "C54", 3), first["input"]["controllers"][0]["growthCatchup"])
        # The plan of each hand, applied the way section 5 describes, gives the state the next hand starts from.
        after_first = golden.apply_growth_plan(first["input"]["controllers"], first["expected"]["afterScorePlan"]["growthChanges"])
        self.assertEqual(second["input"]["controllers"], after_first)
        after_second = golden.apply_growth_plan(second["input"]["controllers"], second["expected"]["afterScorePlan"]["growthChanges"])
        self.assertEqual(third["input"]["controllers"], after_second)
        self.assertEqual([30, 15, 7], [c["input"]["controllers"][0]["growth"] for c in (first, second, third)])
        for c in (first, second):
            self.assertEqual([{"ownerInstanceId": "ctl_01", "abilityId": "C54", "delta": 0, "reset": False, "halve": True}], c["expected"]["afterScorePlan"]["growthChanges"])

    def test_section_5_growth_reads_and_growth_half(self):
        self.expect("§5.1 / §10 返璞归真 +12 的积少成多", controllerBaseAdds=[{"emitterId": "ctl_01", "abilityId": "C55", "amount": "13"}], score="60")
        self.expect("§5.1 返璞归真 深蓝已长 0.45", additiveTotal100=25 + 122)
        self.expect("§10 返璞归真 3 级全红", colorAdd100=50)
        self.expect("§5 C56 第 5 周期货架起步", additiveTotal100=25 + 300)
        self.expect("§5 C56 下手 +3.4", additiveTotal100=25 + 340)
        self.expect("§5 C57 下一手 +1.8", additiveTotal100=180)
        self.expect("§5 C58 两关后", controllerMultiplierNumerator="31", controllerMultiplierDenominator="20")
        self.expect("§4.1 C100 吃掉基础价 6", controllerMultiplierNumerator="43", controllerMultiplierDenominator="20")
        self.expect("§5 C55 首手 +12", baseTotal="29", growthChanges=[{"ownerInstanceId": "ctl_01", "abilityId": "C55", "delta": 3, "reset": False, "halve": False}])
        self.expect("§5 C55 下手 +15", baseTotal="32")
        self.expect("§5 C59 剩 2 次", baseTotal="41")
        self.expect("§5 C129 一次首领后", additiveTotal100=350)
        self.expect("§5.1 停滞", growthChanges=[{"ownerInstanceId": "ctl_01", "abilityId": "C55", "delta": 3, "reset": False, "halve": False}])
        self.assertEqual((0, 12, 70, 350), tuple(golden.catchup_for(self.cfg, card, cycle) for card, cycle in (("C56", 1), ("C55", 3), ("C58", 8), ("C129", 8))))
        self.assertEqual([1, 1, 2, 2, 8, 8], [golden.cycle_of_shop(j) for j in (1, 2, 3, 5, 21, 23)])

    def test_section_5_3_guest_star(self):
        e = self.expect("§5.3 例 两张真实卡明星 1、6", score="64")
        self.assertEqual([200, 100], [x["amount100"] for x in e["additiveEntries"]])
        self.expect("§5.3 例 复制第一张", additiveTotal100=500)
        self.expect("§5.3 例 第一张失效", additiveTotal100=100, triggeredSourceIds=["ctl_03"])

    # ---- RULES_DELTA 6.1 / 7: plans ----

    def test_section_6_1_and_7_plans(self):
        self.expect("§6.1 C15 红蓝红蓝红", redSpreadIndices=[1, 3])
        e = self.expect("§6.1 C36 例 容量 5 交四颗", growthNetDelta=2, growthIndices=[0, 1, 2])
        self.assertEqual(["ctl_01", "ctl_02"], [b["emitterId"] for b in e["afterScorePlan"]["cultivationSourceBindings"]])
        e = self.expect("§6.1 C123 单卡计划")
        self.assertEqual([1, -2, -2, -2], [f["amount"] for f in e["afterScorePlan"]["faceChanges"]])
        e = self.expect("§6.1 C119 第一颗朝上 6")
        self.assertEqual([("F03", "pipSet", 6), ("F04", "pipSet", 6)], [(f["faceId"], f["kind"], f["amount"]) for f in e["afterScorePlan"]["faceChanges"]])
        e = self.expect("§6.1 C122 原 1 直接到 5")
        self.assertEqual([("pipSet", 5)], [(f["kind"], f["amount"]) for f in e["afterScorePlan"]["faceChanges"]])
        self.expect("§7.3 例 提交两颗冻结玻璃", glassRiskDieIds=["D001", "D002"])
        self.expect("玻璃骰贡献 ×3", pipContributions=[18, 3, 3], glassRiskDieIds=["D001"])
        self.expect("黄金面朝上并提交 +0.5", pipContributions=[6, 3, 3], additiveTotal100=75)

    def test_removed_cards_are_rewritten_or_dropped_with_a_reason(self):
        dropped = {(e["file"].rsplit("/", 1)[-1], e["id"]) for e in self.excluded}
        self.assertIn(("GoldenScoresV09.json", "N038"), dropped)
        self.assertIn(("GoldenScoresV10.json", "P214"), dropped)
        self.assertIn(("GoldenScoresV11.json", "C90-two_hit"), dropped)
        rewritten = [c for c in self.cases if any("去掉已移除的卡" in r for r in c.get("derivedFrom", {}).get("rewrites", []))]
        self.assertGreaterEqual(len(rewritten), 4)


if __name__ == "__main__":
    unittest.main()
