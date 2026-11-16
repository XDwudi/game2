"""Regression checks for the frozen input, appendix extraction and text-only delta."""
import copy
import io
import json
import sys
import tempfile
import unittest
from contextlib import redirect_stdout
from pathlib import Path
from unittest import mock

ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(ROOT / "tools"))
import v10_config
import v11_config
from v11_text import extract


class V11ConfigTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.base_bytes = extract.BASE.read_bytes()
        cls.base = json.loads(cls.base_bytes)
        cls.markdown = extract.MARKDOWN.read_text(encoding="utf-8")
        cls.content, cls.audit = extract.extract()

    def test_frozen_base_is_byte_exact_v10_output(self):
        self.assertEqual(self.base_bytes, v10_config.render().encode("utf-8"))

    def test_appendix_full_counts_and_six_category_ids(self):
        self.assertEqual(sum(c["rows"] for c in self.audit["counts"].values()), 270)
        self.assertEqual(sum(c["fields"] for c in self.audit["counts"].values()), 499)
        self.assertEqual(self.audit["entryCount"], 229)
        self.assertEqual(self.audit["textFieldCount"], 458)
        for category in extract.CATEGORIES:
            self.assertEqual([r["id"] for r in self.content[category]], [r["id"] for r in self.base[category]])

    def test_fallbacks_and_no_problem_rows_are_explicit(self):
        self.assertEqual(len(self.audit["noProblemIds"]), 54)
        self.assertEqual(len(self.audit["reusedCurrentTextIds"]), 50)
        # v1.1 coordinator decision: an empty appendix detail stays empty instead of repeating the short text.
        self.assertEqual(len(self.audit["emptyDetailsIds"]), 75)
        overridden = {(o["id"], o["field"]) for o in self.audit["overrides"]}
        for category in self.content.values():
            for row in category:
                if (row["id"], "details") in overridden:
                    continue  # coordinator overrides (tools/v11_text/overrides.json) are checked separately
                if row["id"] in self.audit["emptyDetailsIds"]:
                    self.assertEqual(row["details"], "")
                else:
                    self.assertTrue(row["details"].strip())
        self.assertEqual({r["id"] for r in self.audit["normalizations"]}, {"U08", "U09", "U10", "U11"})

    def test_only_authorized_delta_and_key_order(self):
        output = v11_config.build()
        restored = copy.deepcopy(output)
        restored["configVersion"] = "p1.0.0"
        changed_names = []
        for category in extract.CATEGORIES:
            for old, new in zip(self.base[category], restored[category]):
                keys = list(new)
                anchor = "description" if "description" in new else "displayName"
                self.assertEqual(keys[keys.index(anchor) + 1:keys.index(anchor) + 3], ["shortText", "details"])
                details = new.pop("details")
                self.assertIsInstance(details, str)
                self.assertEqual(details, details.strip() if details else "")
                self.assertLessEqual(len(new.pop("shortText")), 28)
                if old["displayName"] != new["displayName"]:
                    changed_names.append(old["id"])
                    self.assertIn("控制器", old["displayName"])
                    self.assertEqual(new["displayName"], old["displayName"].replace("控制器", "卡牌"))
                    new["displayName"] = old["displayName"]
        self.assertEqual(changed_names, ["L1", "L2"])
        # p1.1.0 also removes the fields that only described the old replacement rule (RULES_DELTA section 9).
        expected = copy.deepcopy(self.base)
        for entry in expected["controllers"]:
            for key in v11_config.REMOVED_CONTROLLER_FIELDS:
                self.assertIn(key, entry)
                del entry[key]
        for key in v11_config.REMOVED_SHOP_FIELDS:
            self.assertIn(key, expected["shop"])
            del expected["shop"][key]
        for entry in output["controllers"]:
            self.assertFalse(set(v11_config.REMOVED_CONTROLLER_FIELDS) & set(entry))
        self.assertFalse(set(v11_config.REMOVED_SHOP_FIELDS) & set(output["shop"]))
        self.assertEqual(restored, expected)

    def test_overrides_apply_exactly_and_are_audited(self):
        applied = {(o["id"], o["field"]): o for o in self.audit["overrides"]}
        self.assertEqual(set(applied), {("C51", "details"), ("C98", "details"), ("L1", "details"), ("L2", "details")})
        rows = {row["id"]: row for group in self.content.values() for row in group}
        for (item_id, field), record in applied.items():
            self.assertEqual(rows[item_id][field], record["after"])
            self.assertTrue(record["reason"].strip())
        self.assertNotIn("以旧换新", rows["C51"]["details"] + rows["C98"]["details"])

    def test_stale_or_unexplained_overrides_are_rejected(self):
        good = {"id": "C98", "field": "details", "from": "不影响卖价和以旧换新的抵扣。", "to": "不影响卖价。", "reason": "test"}
        for bad in (dict(good, **{"from": "不是原文"}), dict(good, reason=" "), dict(good, id="C999"),
                    dict(good, field="displayName"), dict(good, to=" "), dict(good, field="shortText", to="")):
            with self.subTest(bad=bad), self.assertRaises(ValueError):
                extract.extract(overrides=[bad])
        with self.assertRaises(ValueError):
            extract.extract(overrides=[good, good])
        content, audit = extract.extract(overrides=[good])
        self.assertEqual([o["id"] for o in audit["overrides"]], ["C98"])

    def test_generated_files_are_reproducible_utf8_lf(self):
        for path, value in ((extract.CONTENT, self.content), (extract.AUDIT, self.audit),
                            (v11_config.TARGET, v11_config.build())):
            raw = path.read_bytes()
            self.assertEqual(raw, extract.render(value).encode("utf-8"))
            self.assertNotIn(b"\r", raw)
            self.assertTrue(raw.endswith(b"\n"))
            self.assertFalse(raw.startswith(b"\xef\xbb\xbf"))

    def test_d2_d4_substitutions_preserve_nonrarity_normal(self):
        changes, normalizations = [], []
        result = extract.normalize("fixture", "details", "普通控制器、罕见卡牌；普通关、普通栏位、普通投掷", changes, normalizations)
        self.assertEqual(result, "常见卡牌、进阶卡牌；普通关、普通栏位、普通投掷")
        self.assertEqual([row["decision"] for row in changes], ["D2", "D4", "D4"])
        self.assertTrue(all(row["before"] != row["after"] for row in changes))
        self.assertFalse(any(row["decision"] == "D4" for row in self.audit["substitutions"]))

    def test_duplicate_or_missing_appendix_id_rejected(self):
        line = next(line for line in self.markdown.splitlines() if line.startswith("| C01 |"))
        for changed in (self.markdown.replace(line, line + "\n" + line), self.markdown.replace(line, "")):
            with self.subTest(change=changed.count(line)), self.assertRaises(ValueError):
                extract.extract(changed, self.base)

    def test_unknown_id_and_changed_source_text_rejected(self):
        for changed in (self.markdown.replace("| C01 |", "| C999 |", 1),
                        self.markdown.replace("每次有效提交计分后获得1金币。", "每次有效提交计分后获得2金币。", 1)):
            with self.assertRaises(ValueError):
                extract.extract(changed, self.base)

    def test_changed_summary_and_oversize_appendix_rejected(self):
        for changed in (self.markdown.replace("| 300 | 300 | 150 |", "| 301 | 300 | 150 |", 1),
                        self.markdown.replace("每次提交后获得1金币。", "字" * 29, 1)):
            with self.assertRaises(ValueError):
                extract.extract(changed, self.base)

    def test_generator_rejects_missing_extra_duplicate_ids(self):
        for kind in ("missing", "extra", "duplicate"):
            text = copy.deepcopy(self.content)
            if kind == "missing":
                text["controllers"].pop()
            else:
                extra = copy.deepcopy(text["controllers"][0])
                if kind == "extra":
                    extra["id"] = "C999"
                text["controllers"].append(extra)
            with self.subTest(kind=kind), self.assertRaises(ValueError):
                v11_config.build(content=text)

    def test_generator_rejects_invalid_text_for_each_category(self):
        for category in extract.CATEGORIES:
            for field, value in (("shortText", ""), ("shortText", "字" * 29), ("details", " \t"), ("details", None)):
                content = copy.deepcopy(self.content)
                content[category][0][field] = value
                with self.subTest(category=category, field=field, value=value), self.assertRaises(ValueError):
                    v11_config.build(content=content)

    def test_generator_accepts_empty_details_for_each_category(self):
        for category in extract.CATEGORIES:
            content = copy.deepcopy(self.content)
            content[category][0]["details"] = ""
            with self.subTest(category=category):
                output = v11_config.build(content=content)
                self.assertEqual(output[category][0]["details"], "")

    def test_unicode_code_point_limit_and_missing_description_anchor(self):
        base, content = copy.deepcopy(self.base), copy.deepcopy(self.content)
        del base["controllers"][0]["description"]
        content["controllers"][0]["shortText"] = "🎲" * 28
        result = v11_config.build(base, content)
        keys = list(result["controllers"][0])
        self.assertEqual(keys[keys.index("displayName") + 1:keys.index("displayName") + 3], ["shortText", "details"])
        self.assertEqual(len(result["controllers"][0]["shortText"]), 28)

    def test_check_detects_byte_drift_without_writing(self):
        with tempfile.TemporaryDirectory(dir=ROOT / "tools/v11_text") as folder:
            target = Path(folder) / "P0Config.json"
            target.write_bytes(v11_config.render().replace("\n", "\r\n").encode("utf-8"))
            before = target.read_bytes()
            with mock.patch.object(v11_config, "TARGET", target), mock.patch.object(sys, "argv", ["v11_config.py", "--check"]), redirect_stdout(io.StringIO()):
                self.assertEqual(v11_config.main(), 1)
            self.assertEqual(target.read_bytes(), before)

    def test_build_does_not_mutate_inputs_and_rejects_wrong_version(self):
        before = copy.deepcopy(self.base)
        v11_config.build(self.base, self.content)
        self.assertEqual(self.base, before)
        before["configVersion"] = "p1.1.0"
        with self.assertRaises(ValueError):
            v11_config.build(before, self.content)


if __name__ == "__main__":
    unittest.main()
