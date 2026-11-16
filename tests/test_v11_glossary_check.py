"""Rule fixtures and input-boundary regression tests for the player text checker."""

from contextlib import redirect_stdout
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

from tools import v11_glossary_check as checker


DATA = checker.load_rules()


class GlossaryRuleTests(unittest.TestCase):
    def test_rules_have_reviewable_sources_and_rationales(self):
        self.assertEqual({rule["category"] for rule in DATA["rules"]},
                         {"核心概念", "用户决定", "计分用语", "写法", "开发信息"})
        for rule in DATA["rules"]:
            self.assertIn("docs/", rule["source"])
            self.assertTrue(checker.CJK.search(rule["rationale"]))
            self.assertTrue(checker.CJK.search(rule["replacement"]))
            self.assertTrue(rule["positive"])
            self.assertTrue(rule["negative"])

    def test_all_glossary_aliases_and_internal_patterns(self):
        aliases = ["有效提交", "本轮", "周期", "结束本轮", "首掷", "剩掷", "空投", "待选", "待锁",
                   "清除待选", "K", "容量 K", "提交容量", "有序提交", "槽", "普通槽", "背包", "免槽",
                   "消耗品", "局内道具", "研究强化", "研习", "强化", "普通卡牌", "罕见", "D001", "F01",
                   "物理面", "原值", "目标面", "活动骰", "点数贡献 +N", "整手基础分 +N", "整手得分×N",
                   "整手得分×n，n从1起", "整手得分的倍率+0.25", "点数贡献为 0", "相对权重", "归一化",
                   "连锁", "剩次奖励", "余次奖励", "未用投掷奖励", "基础奖励", "计息本金", "计息本金上限",
                   "现金", "余额", "净增", "入账", "钱包", "新建挑战", "该骰", "其价格", "控制器", "ITEM_001",
                   "C01", "C153", "Instant", "Fast", "Normal", "p1.1.0"]
        for text in aliases:
            with self.subTest(text=text):
                hits, _ = checker.find_violations([checker.PlayerText("fixture", "1", text)], DATA["rules"], [])
                self.assertTrue(hits)

    def test_legitimate_contexts_do_not_trigger(self):
        text = "普通关、普通栏位、普通挑战、高压挑战、其他、其余、其中、二三其德；不连锁；本手效果作废。"
        hits, _ = checker.find_violations([checker.PlayerText("fixture", "1", text)], DATA["rules"], [])
        self.assertEqual(hits, [])

    def test_internal_run_label_requires_identifier_boundaries(self):
        for text in ("6，整个Run仅可购买1次", "本Run局内"):
            with self.subTest(text=text):
                hits, _ = checker.find_violations([checker.PlayerText("fixture", "1", text)], DATA["rules"], [])
                self.assertEqual([(hit["rule"], hit["match"]) for hit in hits], [("dev-engine-label", "Run")])
        for text in ("Runtime 运行环境", "Runner 运行器", "MyRun 变量", "Run_1 标识符"):
            with self.subTest(text=text):
                hits, _ = checker.find_violations([checker.PlayerText("fixture", "1", text)], DATA["rules"], [])
                self.assertEqual(hits, [])


def _fixture_test(rule, positive):
    def test(self):
        fixture = rule["positive" if positive else "negative"]
        hits, allowed = checker.find_violations([checker.PlayerText("fixture", "1", fixture)], [rule], [])
        self.assertEqual(bool(hits), positive, (rule["id"], fixture, hits))
        self.assertEqual(allowed, [])
    return test


for _rule in DATA["rules"]:
    for _positive in (True, False):
        setattr(GlossaryRuleTests, "test_" + _rule["id"].replace("-", "_") + ("_positive" if _positive else "_negative"),
                _fixture_test(_rule, _positive))


class ConfigScanTests(unittest.TestCase):
    def test_config_scan_reads_exactly_player_facing_fields(self):
        # Keep this independent of the production catalog: accidental additions fail.
        expected = {name: ["displayName", "shortText", "details"] for name in
                    ("controllers", "consumables", "services", "diceUpgrades", "bossModifiers", "lootKinds")}
        expected["services"].append("priceRule")
        expected.update(redRewards=["title"], enchantments=["displayName"], handDefinitions=["displayName"],
                        colorDefinitions=["displayName"], stages=["normalTarget", "riskTarget"])
        self.assertEqual(DATA["configFields"], expected)
        fixture = {category: [{"id": "C01", "stageId": "STAGE_01", "count": 0, "description": "控制器",
                               "rarity": "普通", "trigger": "控制器", "title": "控制器", "priceRule": "控制器",
                               "displayName": "控制器", "shortText": "控制器", "details": "控制器",
                               "normalTarget": "1000", "riskTarget": "1500", "unexpected": "控制器",
                               "parameters": {"displayName": "控制器", "shortText": "控制器"}}]
                   for category in expected}
        fixture.update(configVersion="p1.1.0", debug={"unlockPhrase": "控制器"},
                       unrelated=[{"displayName": "控制器", "description": "控制器"}])
        texts = list(checker.config_texts(fixture, DATA["configFields"]))
        self.assertEqual({text.location for text in texts},
                         {f"{category}[C01].{field}" for category, fields in expected.items() for field in fields})
        self.assertEqual(len(texts), sum(map(len, expected.values())))
        self.assertFalse(any("description" in item.location or "parameters" in item.location for item in texts))

    def test_config_does_not_require_cjk(self):
        texts = list(checker.config_texts({"controllers": [{"id": "C01", "displayName": "ITEM_00012"}]}, DATA["configFields"]))
        hits, _ = checker.find_violations(texts, DATA["rules"], [])
        self.assertEqual([item["rule"] for item in hits], ["dev-item-id"])

    def test_null_target_is_not_a_string(self):
        texts = list(checker.config_texts({"stages": [{"stageId": "STAGE_01", "normalTarget": "100", "riskTarget": None}]}, DATA["configFields"]))
        self.assertEqual([text.location for text in texts], ["stages[STAGE_01].normalTarget"])


class CSharpScanTests(unittest.TestCase):
    def strings(self, source):
        return [text.text for text in checker.csharp_texts(source, "fixture.cs")]

    def test_comments_characters_and_english_only_are_excluded(self):
        source = '// "控制器"\n/* "消耗品" */ char c=\'其\'; string a="Normal"; string b="卡牌";'
        self.assertEqual(self.strings(source), ["卡牌"])

    def test_regular_escapes_unicode_and_cjk_extensions(self):
        source = r'string a="控制器\n\"卡牌\""; string b="\u5361\u724c"; string c="\U00020000";'
        self.assertEqual(self.strings(source), ['控制器\n"卡牌"', "卡牌", "\U00020000"])

    def test_utf16_surrogate_pairs_are_cjk_and_json_encodable(self):
        source = r'var a="\uD840\uDC00\u63a7\u5236\u5668"; var b="\uD840\uDC00";'
        values = self.strings(source)
        self.assertEqual(values, ["\U00020000控制器", "\U00020000"])
        self.assertTrue(json.dumps(values, ensure_ascii=False).encode("utf-8"))

    def test_unpaired_surrogates_remain_visible_in_utf8_reports(self):
        values = self.strings(r'var a="卡牌\uD840";')
        self.assertEqual(values, [r"卡牌\uD840"])
        self.assertTrue(json.dumps(values, ensure_ascii=False).encode("utf-8"))

    def test_verbatim_string_preserves_backslashes_and_doubled_quote(self):
        self.assertEqual(self.strings('var s=@"控制器\\n""卡牌""";'), ['控制器\\n"卡牌"'])

    def test_interpolation_does_not_scan_code_identifiers(self):
        source = 'var s=$"卡牌 {Config.Controller("C01").displayName}，数量 {items.Count}";'
        texts = list(checker.csharp_texts(source, "fixture.cs"))
        self.assertEqual([text.text for text in texts], ["卡牌 {…}，数量 {…}"])
        hits, _ = checker.find_violations(texts, DATA["rules"], [])
        self.assertEqual(hits, [])

    def test_interpolation_nested_chinese_literals_and_braces(self):
        source = 'var s=$"数量 {{固定}} {ok ? "控制器" : $"道具 {new int[] {1,2}.Length}"}";'
        self.assertEqual(self.strings(source), ["数量 {固定} {…}", "控制器", "道具 {…}"])

    def test_both_verbatim_interpolation_prefixes(self):
        self.assertEqual(self.strings('var a=$@"卡牌 {id}"; var b=@$"道具 {id}";'), ["卡牌 {…}", "道具 {…}"])

    def test_raw_and_interpolated_raw_strings(self):
        source = 'var a="""卡牌 "名称"。"""; var b=$$"""骰子 {固定} {{id}}""";'
        self.assertEqual(self.strings(source), ['卡牌 "名称"。', "骰子 {固定} {…}"])

    def test_source_locations_follow_multiline_literals(self):
        texts = list(checker.csharp_texts('var a=@"卡牌\n道具";\nvar b="骰子";', "fixture.cs"))
        self.assertEqual([(item.line, item.location) for item in texts], [(1, "1:7"), (3, "3:7")])


class ReportingTests(unittest.TestCase):
    def test_empty_rules_do_not_pass_as_zero_violations(self):
        with tempfile.TemporaryDirectory(dir=checker.ROOT / "tools/v11_text") as folder:
            path = Path(folder) / "rules.json"
            path.write_text(json.dumps({"rules": [], "configFields": DATA["configFields"]}), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "must not be empty"):
                checker.load_rules(path)

    def test_allowlist_requires_exact_scope_and_retains_reason(self):
        item = checker.PlayerText("fixture.cs", "1:1", "控制器")
        entry = {"rule": "term-controller", "file": item.file, "location": item.location,
                 "text": item.text, "reason": "此处指硬件设备，不是游戏卡牌。"}
        hits, allowed = checker.find_violations([item], DATA["rules"], [entry])
        self.assertEqual(hits, [])
        self.assertEqual(allowed[0]["reason"], entry["reason"])
        for key in ("rule", "file", "location", "text"):
            with self.subTest(key=key):
                hits, allowed = checker.find_violations([item], DATA["rules"], [{**entry, key: "different"}])
                self.assertEqual(len(hits), 1)
                self.assertEqual(allowed, [])

    def test_allowlist_missing_reason_is_rejected(self):
        with tempfile.TemporaryDirectory(dir=checker.ROOT / "tools/v11_text") as folder:
            path = Path(folder) / "allowlist.json"
            path.write_text(json.dumps({"entries": [{"rule": "term-controller", "file": "f", "location": "1", "text": "控制器"}]}), encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "reason"):
                checker.load_allowlist(path)

    def test_validation_and_debug_files_are_excluded(self):
        with tempfile.TemporaryDirectory(dir=checker.ROOT / "tools/v11_text") as folder:
            root = Path(folder)
            for relative in ("UI/Normal.cs", "UI/GameApp.Debug.cs", "UI/Validation~/Nested/Test.cs", "Runtime/Normal.cs", "Runtime/Validation~/Test.cs"):
                path = root / "DicePrototype/Assets/Game" / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text('var s="控制器";', encoding="utf-8")
            paths = [path.relative_to(root).as_posix() for path in checker.source_files(root)]
            self.assertEqual(paths, [checker.CONFIG, "DicePrototype/Assets/Game/Runtime/Normal.cs", "DicePrototype/Assets/Game/UI/Normal.cs"])

    def test_missing_scan_directory_is_not_success(self):
        with tempfile.TemporaryDirectory(dir=checker.ROOT / "tools/v11_text") as folder:
            with self.assertRaisesRegex(ValueError, "No eligible C#"):
                checker.source_files(Path(folder))

    def test_empty_player_scan_is_not_success(self):
        with tempfile.TemporaryDirectory(dir=checker.ROOT / "tools/v11_text") as folder:
            root = Path(folder)
            for relative, source in ((checker.CONFIG, '{"configVersion":"p1.1.0"}'),
                                     ("DicePrototype/Assets/Game/UI/Test.cs", 'var s="卡牌";'),
                                     ("DicePrototype/Assets/Game/Runtime/Test.cs", 'var s="道具";')):
                path = root / relative
                path.parent.mkdir(parents=True, exist_ok=True)
                path.write_text(source, encoding="utf-8")
            with self.assertRaisesRegex(ValueError, "Empty config or CJK"):
                checker.scan(root)

    def test_cli_exit_codes_json_and_report_only(self):
        fixture = {"violationCount": 1, "example": "卡牌"}
        for arguments, code in ((["--json"], 1), (["--json", "--report-only"], 0)):
            with self.subTest(arguments=arguments), patch.object(checker, "scan", return_value=fixture), redirect_stdout(io.StringIO()) as output:
                self.assertEqual(checker.main(arguments), code)
                self.assertEqual(json.loads(output.getvalue()), fixture)
        with patch.object(checker, "scan", return_value={"violationCount": 0}), redirect_stdout(io.StringIO()):
            self.assertEqual(checker.main(["--json"]), 0)
        with patch.object(checker, "scan", side_effect=ValueError("empty scan")), redirect_stdout(io.StringIO()) as output:
            self.assertEqual(checker.main(["--json", "--report-only"]), 2)
            self.assertEqual(json.loads(output.getvalue()), {"error": "empty scan"})


if __name__ == "__main__":
    unittest.main()
