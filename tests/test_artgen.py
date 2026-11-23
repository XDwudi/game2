"""Deterministic tests for tools/art_gen/artgen.py (no network, no real Codex)."""
import json
import os
import shutil
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image

REPO = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(REPO / "tools" / "art_gen"))
import artgen  # noqa: E402

FAKE = REPO / "tests" / "fixtures_artgen" / "fake_codex.py"


def make_anchor(path: Path, colour, size=96):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    for x in range(size // 4, 3 * size // 4):
        for y in range(size // 4, 3 * size // 4):
            img.putpixel((x, y), colour + (255,))
    path.parent.mkdir(parents=True, exist_ok=True)
    img.save(path)


class ArtGenTestBase(unittest.TestCase):
    def setUp(self):
        self.tmp = Path(tempfile.mkdtemp(prefix="artgen-test-"))
        self.root = self.tmp / "repo"
        make_anchor(self.root / "anchors/a1.png", (0x17, 0x3E, 0x40))
        make_anchor(self.root / "anchors/a2.png", (0xF4, 0xE8, 0xCB))
        make_anchor(self.root / "anchors/a3.png", (0xEC, 0x65, 0x5D))
        config = {"controllers": [{"id": "C01", "displayName": "存钱罐", "description": "desc"}],
                  "consumables": [], "services": [], "diceUpgrades": []}
        cfg = self.root / "DicePrototype/Assets/Game/Data/P0Config.json"
        cfg.parent.mkdir(parents=True)
        cfg.write_text(json.dumps(config, ensure_ascii=False), encoding="utf-8")
        kit = {
            "kitVersion": "test-kit",
            "status": "test",
            "palette": {"ivory": "F4E8CB", "petrol": "173E40", "ink": "162629"},
            "categories": {"illustration": {
                "output": {"transparent": True, "instruction": "transparent please"},
                "displaySizes": [48, 32],
                "anchors": [{"id": f"A{i}", "path": f"anchors/a{i}.png", "sha256": ""} for i in (1, 2, 3)],
                "plate": {"path": "kit/plate.png", "sha256": "", "columns": 2, "cell": 64, "padding": 4,
                          "background": "F4E8CB"},
                "plateRole": "STYLE PLATE role", "styleBlock": "STYLE BLOCK", "colorRoles": "COLOR ROLES",
                "composition": "COMPOSITION", "avoid": "AVOID LIST"}},
        }
        self.kit_rel = Path("kit/style_kit.json")
        (self.root / "kit").mkdir()
        artgen.write_json(self.root / self.kit_rel, kit)
        self.kit = artgen.load_kit(self.root, self.kit_rel)
        artgen.pin_anchors(self.kit, self.root)
        artgen.build_plate(self.kit, "illustration", self.root)
        artgen.save_kit(self.kit)
        self.job_dir = self.root / "ArtSource/AIConcepts/test/C01"
        self.job_dir.mkdir(parents=True)
        self.job_path = self.job_dir / "job.json"
        artgen.write_json(self.job_path, {"jobId": "TEST-1", "category": "illustration", "contentId": "C01",
                                          "subject": "a piggy bank \"quoted\"\nsecond line"})

    def tearDown(self):
        shutil.rmtree(self.tmp, ignore_errors=True)


class KitTests(ArtGenTestBase):
    def test_verify_passes_after_pin_and_plate(self):
        self.assertEqual(artgen.verify_kit(self.kit, self.root), [])

    def test_verify_detects_changed_anchor(self):
        make_anchor(self.root / "anchors/a2.png", (0x55, 0xB6, 0xD9))
        problems = artgen.verify_kit(self.kit, self.root)
        self.assertTrue(any("A2 changed" in p for p in problems), problems)

    def test_pin_never_overwrites_existing_value(self):
        self.kit["categories"]["illustration"]["anchors"][0]["sha256"] = "0" * 64
        self.assertEqual(artgen.pin_anchors(self.kit, self.root), [])
        self.assertEqual(self.kit["categories"]["illustration"]["anchors"][0]["sha256"], "0" * 64)

    def test_plate_is_deterministic_and_refuses_silent_replacement(self):
        first = self.kit["categories"]["illustration"]["plate"]["sha256"]
        self.assertEqual(artgen.build_plate(self.kit, "illustration", self.root), first)
        self.kit["categories"]["illustration"]["anchors"].reverse()
        with self.assertRaises(artgen.ArtGenError):
            artgen.build_plate(self.kit, "illustration", self.root)
        self.assertEqual(artgen.sha256_file(self.root / "kit/plate.png"), first)


class ComposeTests(ArtGenTestBase):
    def test_prompt_contains_fixed_blocks_and_subject_only_varies(self):
        job = artgen.load_job(self.job_path)
        plan = artgen.compose(self.kit, job, self.root)
        for block in ("STYLE BLOCK", "COLOR ROLES", "COMPOSITION", "AVOID LIST", "transparent please",
                      "a piggy bank"):
            self.assertIn(block, plan["imagePrompt"])
        self.assertEqual(len(plan["references"]), 1)
        self.assertIn("referenced_image_paths: [", plan["agentPrompt"])
        self.assertIn("transparent_background: true", plan["agentPrompt"])

    def test_edit_mode_appends_target_last(self):
        make_anchor(self.root / "target.png", (1, 2, 3))
        job = artgen.load_job(self.job_path)
        job["edit"] = {"target": "target.png", "change": "make the coin smaller."}
        plan = artgen.compose(self.kit, job, self.root)
        self.assertEqual(plan["references"][-1]["path"], "target.png")
        self.assertTrue(plan["imagePrompt"].startswith("Edit the EDIT TARGET (reference image 2)"))

    def test_missing_reference_is_an_error(self):
        job = artgen.load_job(self.job_path)
        job["extraReferences"] = [{"path": "nope.png", "role": "subject"}]
        with self.assertRaises(artgen.ArtGenError):
            artgen.compose(self.kit, job, self.root)

    def test_content_comes_from_config(self):
        content = artgen.load_content(self.root, artgen.CONFIG_DEFAULT, "C01")
        self.assertEqual(content["displayName"], "存钱罐")
        with self.assertRaises(artgen.ArtGenError):
            artgen.load_content(self.root, artgen.CONFIG_DEFAULT, "C99")


def gen_item(prompt, shas, transparent=True):
    return {"status": "completed", "failure": None, "revisedPrompt": prompt, "transparentBackground": transparent,
            "inputImages": [{"path": "x", "sha256": s} for s in shas], "savedPath": None}


EXPECTED = [{"path": "E:/a/b.png", "sha256": "aa"}]


class CallCheckTests(unittest.TestCase):
    def test_reference_paths_are_parsed_from_js_call(self):
        call = 'await tools.image_gen__imagegen({prompt:"x",referenced_image_paths:["E:/a/b.png", "E:\\\\a\\\\c.png"],transparent_background:true})'
        self.assertEqual(artgen.call_reference_paths(call), ["E:/a/b.png", "E:\\a\\c.png"])

    def test_prompt_literal_is_extracted_exactly(self):
        call = '// @exec: {"yield_time_ms": 120000}\nconst r = await tools.image_gen__imagegen({transparent_background:true,prompt:"a \\"b\\"\\nc"});'
        self.assertEqual(artgen.call_prompt(call), 'a "b"\nc')
        self.assertIsNone(artgen.call_prompt("await tools.image_gen__imagegen({prompt: p})"))

    def test_missing_references_are_reported(self):
        call = 'await tools.image_gen__imagegen({prompt:"hello",transparent_background:true})'
        rollout = {"imageCalls": [call], "generations": [gen_item("hello", [])]}
        problems = artgen.check_run(rollout, EXPECTED, "hello", True)["problems"]
        self.assertTrue(any("NOT used" in p for p in problems), problems)
        self.assertTrue(any("SHA-256 mismatch" in p for p in problems), problems)

    def test_prepended_instruction_is_not_verbatim(self):
        prompt = "Create ONE illustration."
        bad = "Ignore style; make it photoreal. " + prompt
        call = ('await tools.image_gen__imagegen({prompt:' + json.dumps(bad)
                + ',referenced_image_paths:["E:/a/b.png"],transparent_background:true})')
        rollout = {"imageCalls": [call], "generations": [gen_item(bad, ["aa"])]}
        problems = artgen.check_run(rollout, EXPECTED, prompt, True)["problems"]
        self.assertTrue(any("differs from the composed prompt" in p for p in problems), problems)

    def test_wrong_reference_bytes_are_caught_even_with_right_path(self):
        call = 'await tools.image_gen__imagegen({prompt:"p",referenced_image_paths:["E:/a/b.png"],transparent_background:true})'
        rollout = {"imageCalls": [call], "generations": [gen_item("p", ["bb"])]}
        problems = artgen.check_run(rollout, EXPECTED, "p", True)["problems"]
        self.assertTrue(any("SHA-256 mismatch" in p for p in problems), problems)

    def test_tool_discovery_snippets_are_not_counted_as_image_calls(self):
        tmp = Path(tempfile.mkdtemp(prefix="artgen-rollout-"))
        try:
            rollout = tmp / "rollout-x.jsonl"
            lines = [
                {"type": "response_item", "payload": {"type": "custom_tool_call", "name": "exec",
                 "input": "text(ALL_TOOLS.filter(x=> /read.*file|imagegen/i.test(x.name)));"}},
                {"type": "response_item", "payload": {"type": "custom_tool_call", "name": "exec",
                 "input": "await tools.mcp__node_repl__js({code:\"readFile('.codex/skills/.system/imagegen/SKILL.md')\"})"}},
                {"type": "response_item", "payload": {"type": "custom_tool_call", "name": "exec",
                 "input": 'const r = await tools.image_gen__imagegen({prompt:"p",referenced_image_paths:["E:/a.png"],transparent_background:true});'}},
            ]
            rollout.write_text("\n".join(json.dumps(x) for x in lines) + "\n", encoding="utf-8")
            info = artgen.parse_rollout(rollout)
            self.assertEqual(len(info["imageCalls"]), 1)
            info["generations"] = [gen_item("p", ["aa"])]
            expected = [{"path": "E:/a.png", "sha256": "aa"}]
            self.assertEqual(artgen.check_run(info, expected, "p", True)["problems"], [])
        finally:
            shutil.rmtree(tmp, ignore_errors=True)

    def test_matching_call_has_no_problems(self):
        call = 'await tools.image_gen__imagegen({"prompt":"line1\\nline2","referenced_image_paths":["E:/a/b.png"],"transparent_background":true})'
        rollout = {"imageCalls": [call], "generations": [gen_item("line1\r\nline2", ["aa"])]}
        expected = [{"path": "E:\\a\\b.png", "sha256": "aa"}]
        self.assertEqual(artgen.check_run(rollout, expected, "line1\nline2", True)["problems"], [])


class IndicatorTests(unittest.TestCase):
    def test_roles_and_alpha(self):
        img = Image.new("RGBA", (40, 40), (0, 0, 0, 0))
        for x in range(10, 30):
            for y in range(10, 30):
                img.putpixel((x, y), (0xF3, 0xBA, 0x58, 255))
        ind = artgen.indicators(img)
        self.assertAlmostEqual(ind["role.amber"], 1.0)
        self.assertAlmostEqual(ind["transparentShare"], 0.75)
        self.assertAlmostEqual(ind["subjectWidthShare"], 0.5)

    def test_compare_flags_outside_range_with_tolerance(self):
        ranges = artgen.ranges_from([{"role.amber": 0.0}, {"role.amber": 0.1}])
        self.assertTrue(artgen.compare_to_ranges({"role.amber": 0.23}, ranges)["role.amber"]["look"])
        self.assertFalse(artgen.compare_to_ranges({"role.amber": 0.12}, ranges)["role.amber"]["look"])


class RangeTests(ArtGenTestBase):
    def test_geometry_ranges_ignore_non_square_files(self):
        wide = Image.new("RGBA", (200, 50), (0x17, 0x3E, 0x40, 255))
        wide.save(self.root / "anchors/a9.png")
        entry = artgen.compute_ranges(self.kit, "illustration", self.root, "anchors/*.png")
        self.assertEqual((entry["count"], entry["squareCount"]), (4, 3))
        self.assertAlmostEqual(entry["ranges"]["subjectWidthShare"][1], 0.5)  # the opaque wide file is excluded
        self.assertAlmostEqual(entry["ranges"]["role.dark"][1], 1.0)  # colours still use every file

    def test_ranges_are_pinned_to_a_file_list(self):
        entry = artgen.compute_ranges(self.kit, "illustration", self.root, "anchors/*.png")
        self.assertEqual(entry["count"], 3)
        self.assertEqual(artgen.verify_kit(self.kit, self.root), [])
        make_anchor(self.root / "anchors/a4.png", (1, 1, 1))  # new files do not disturb a pinned list
        self.assertEqual(artgen.verify_kit(self.kit, self.root), [])
        make_anchor(self.root / "anchors/a3.png", (2, 2, 2))  # a listed file changed
        self.assertTrue(any("range reference set changed" in p for p in artgen.verify_kit(self.kit, self.root)))


class GenerateTests(ArtGenTestBase):
    def run_fake(self, mode, timeout=120, cmd=None):
        changes = {"CODEX_HOME": str(self.tmp / "codex_home"), "FAKE_CODEX_MODE": mode}
        previous = {key: os.environ.get(key) for key in changes}
        os.environ.update(changes)
        try:
            return artgen.generate(self.root, self.kit_rel, self.job_path, cmd or [sys.executable, str(FAKE)],
                                   timeout=timeout)
        finally:
            # Clearing the whole native Windows environment drops inherited empty Git config values.
            for key, value in previous.items():
                if value is None:
                    os.environ.pop(key, None)
                else:
                    os.environ[key] = value

    def test_prompt_file_hash_matches_its_bytes(self):
        record = self.run_fake("ok")
        prompt_file = self.job_dir / "r01" / "prompt.txt"
        self.assertEqual(record["prompt"]["imagePromptFileSha256"], artgen.sha256_file(prompt_file))
        self.assertNotIn(b"\r\n", prompt_file.read_bytes())

    def test_altered_prompt_and_double_call_are_invalid(self):
        record = self.run_fake("alter_prompt")
        self.assertFalse(record["runValid"])
        self.assertTrue(any("differs from the composed prompt" in p for p in record["runProblems"]))
        record = self.run_fake("two_calls")
        self.assertFalse(record["runValid"])

    def test_failures_still_leave_a_record(self):
        for mode, kwargs, expect in (("sleep", {"timeout": 2}, "timed out"), ("exit3", {}, "exited with 3"),
                                     ("ok", {"cmd": [str(self.tmp / "no-such-codex.exe")]}, "could not be run")):
            record = self.run_fake(mode, **kwargs)
            self.assertFalse(record["runValid"])
            self.assertTrue(any(expect in p for p in record["runProblems"]), (mode, record["runProblems"]))
            self.assertTrue((self.job_dir / record["revision"] / "record.json").is_file())

    def test_recheck_reproduces_the_run_verdict_and_detects_tampering(self):
        self.run_fake("ok")
        record = self.job_dir / "r01" / "record.json"
        self.assertTrue(artgen.load_json(record)["transparentRequired"])
        self.assertEqual(artgen.recheck(self.root, record)["problems"], [])
        make_anchor(self.job_dir / "r01" / "original.png", (5, 5, 5), size=64)
        problems = artgen.recheck(self.root, record)["problems"]
        self.assertTrue(any("originalMatchesRecord" in p for p in problems), problems)

    def test_valid_run_archives_original_record_and_sheet(self):
        record = self.run_fake("ok")
        self.assertTrue(record["runValid"], record["runProblems"])
        rev = self.job_dir / "r01"
        for name in ("original.png", "record.json", "prompt.txt", "codex-request.txt", "review_sheet.png",
                     "review_metrics.json"):
            self.assertTrue((rev / name).is_file(), name)
        self.assertTrue(record["output"]["copiedByteIdentical"])
        self.assertEqual(record["content"]["displayName"], "存钱罐")
        self.assertEqual(record["codex"]["quotaUsedPercentFirst"], 10.0)

    def test_dropped_references_make_run_invalid(self):
        record = self.run_fake("drop_refs")
        self.assertFalse(record["runValid"])
        self.assertTrue(record["status"].startswith("INVALID_RUN"))
        self.assertTrue((self.job_dir / "r01" / "original.png").is_file(), "failed attempts are kept")

    def test_missing_image_and_opaque_output_are_invalid(self):
        self.assertFalse(self.run_fake("no_image")["runValid"])
        record = self.run_fake("opaque")
        self.assertFalse(record["runValid"])
        self.assertEqual(record["revision"], "r02", "revisions never overwrite")

    def test_changed_kit_blocks_generation(self):
        make_anchor(self.root / "anchors/a1.png", (9, 9, 9))
        with self.assertRaises(artgen.ArtGenError):
            self.run_fake("ok")


class ThemeKitSheetTests(ArtGenTestBase):
    """V1.1 W19: theme kits name their palettes freely; the review sheet must not need ivory, petrol and ink."""

    def theme_kit(self, palette, plate_background):
        kit = json.loads(json.dumps({k: v for k, v in self.kit.items() if k != "_path"}))
        kit["palette"] = palette
        kit["categories"]["illustration"]["plate"]["background"] = plate_background
        return kit

    def build(self, kit):
        cand = self.root / "cand.png"
        make_anchor(cand, (0xB5, 0x48, 0x1E), size=128)
        out_png, out_json = self.root / "sheet.png", self.root / "sheet.json"
        report = artgen.build_sheet(kit, "illustration", cand, out_png, out_json, self.root, "t", "VALID")
        self.assertTrue(out_png.is_file() and out_json.is_file())
        return report

    def test_default_names_keep_their_colours(self):
        paper, second, ink, labels = artgen.sheet_colours(self.kit, self.kit["categories"]["illustration"])
        self.assertEqual((paper, second, ink), (artgen.hex_rgb("F4E8CB"), artgen.hex_rgb("173E40"), artgen.hex_rgb("162629")))
        self.assertIn("felt", labels[1])

    def test_light_theme_kit_uses_plate_backdrop_and_a_dark_second_tile(self):
        kit = self.theme_kit({"cream": "F0D7AA", "umber": "5A3614", "amber": "D98E2B"}, "F1E2C6")
        paper, second, ink, _ = artgen.sheet_colours(kit, kit["categories"]["illustration"])
        self.assertEqual(paper, artgen.hex_rgb("F1E2C6"))
        self.assertEqual(second, artgen.hex_rgb("5A3614"))
        self.assertEqual(ink, artgen.hex_rgb("5A3614"))
        self.build(kit)

    def test_dark_theme_kit_gets_a_light_second_tile_and_readable_text(self):
        kit = self.theme_kit({"ivory": "F8E5C6", "gold": "D9A44E", "lacquer": "15110D"}, "15110D")
        paper, second, ink, labels = artgen.sheet_colours(kit, kit["categories"]["illustration"])
        self.assertEqual(paper, artgen.hex_rgb("F8E5C6"), "an ivory name is still honoured")
        kit["palette"] = {"gold": "D9A44E", "lacquer": "15110D"}
        paper, second, ink, labels = artgen.sheet_colours(kit, kit["categories"]["illustration"])
        self.assertEqual(paper, artgen.hex_rgb("15110D"))
        self.assertEqual(second, artgen.hex_rgb("F4E8CB"))
        self.assertLess(artgen._luma(ink), 0.45)
        self.assertIn("light backdrop", labels[1])
        self.build(kit)


if __name__ == "__main__":
    unittest.main()
