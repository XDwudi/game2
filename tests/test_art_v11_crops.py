"""W13a compact-icon crop table: schema, coverage, bounds, determinism and the override table.

The committed table must be exactly what the script produces from the committed illustrations, so a run of the
script is compared byte for byte with it (and with a second run).
"""

import importlib.util
import json
from pathlib import Path
import re
import shutil
import subprocess
import sys
import tempfile
import unittest

from PIL import Image

# Test discovery must not leave Python cache files beside production sources.
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "tools/art_v11/crops/make_crops.py"
TABLE = ROOT / "DicePrototype/Assets/Resources/IconCrops/default.json"
SOURCE = "DicePrototype/Assets/Art/Art071/Illustrations"
SWISS = ROOT / "DicePrototype/Assets/Resources/Themes/swiss/Art"
CONFIG = ROOT / "DicePrototype/Assets/Game/Data/P0Config.json"
CONTENT_LISTS = ("controllers", "consumables", "services", "diceUpgrades")
TOP_KEYS = ["schema", "theme", "source", "generatedBy", "imageSize", "units", "crops"]
REQUIRED = {"x", "y", "size", "confidence", "method"}
OPTIONAL = {"note", "imageSize"}
METHODS = {"alpha-core-1", "override"}
MIN_SIZE = 0.40          # a crop smaller than 40% of the short side would be an over-zoom on this art
FOUR = re.compile(r"^\d\.\d{4}$")


def load_module(name="make_crops_under_test"):
    spec = importlib.util.spec_from_file_location(name, SCRIPT)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def file_ids(folder):
    found = {}
    for path in folder.glob("*.png"):
        match = re.match(r"^illustration_([a-z]+\d+)\.png$", path.name, re.IGNORECASE)
        if match:
            found[match.group(1).upper()] = path
    return found


def raw_numbers(text):
    """For each crop line of the table text: id -> {field: number literal as written}."""
    out = {}
    for line in text.splitlines():
        match = re.match(r'^    "([A-Z]+\d+)": \{(.*)\},?$', line)
        if match:
            out[match.group(1)] = dict(re.findall(r'"(x|y|size|confidence)": ([0-9.]+)', match.group(2)))
    return out


def check_inside(test, cid, crop, size):
    W, H = size
    S = min(W, H)
    test.assertGreaterEqual(crop["x"], 0.0, cid)
    test.assertGreaterEqual(crop["y"], 0.0, cid)
    test.assertLessEqual(crop["size"], 1.0, cid)
    # Exact in table units: x*W + size*S <= W and y*H + size*S <= H.
    xi, yi, si = (round(crop[k] * 10000) for k in ("x", "y", "size"))
    test.assertLessEqual(xi * W + si * S, 10000 * W, cid)
    test.assertLessEqual(yi * H + si * S, 10000 * H, cid)


class CropTableTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.text = TABLE.read_text(encoding="utf-8")
        cls.table = json.loads(cls.text)
        cls.pairs = json.loads(cls.text, object_pairs_hook=list)
        cls.files = file_ids(ROOT / SOURCE)
        config = json.loads(CONFIG.read_text(encoding="utf-8"))
        cls.config_ids = {item["id"] for key in CONTENT_LISTS for item in config[key]}

    def test_schema(self):
        self.assertEqual([key for key, _ in self.pairs], TOP_KEYS)
        t = self.table
        self.assertEqual(t["schema"], "icon-crops-1")
        self.assertEqual(t["theme"], "default")
        self.assertEqual(t["source"], SOURCE)
        self.assertEqual(t["generatedBy"], "tools/art_v11/crops/make_crops.py")
        self.assertEqual(t["imageSize"], [1254, 1254])
        self.assertIsInstance(t["units"], str)
        for cid, crop in t["crops"].items():
            with self.subTest(cid=cid):
                self.assertTrue(REQUIRED <= set(crop) <= REQUIRED | OPTIONAL, sorted(crop))
                for key in ("x", "y", "size", "confidence"):
                    self.assertIsInstance(crop[key], float)
                self.assertIn(crop["method"], METHODS)
                self.assertTrue(0.0 <= crop["confidence"] <= 1.0)
                if "note" in crop:
                    self.assertIsInstance(crop["note"], str)
                    self.assertTrue(crop["note"].strip())
                if "imageSize" in crop:
                    self.assertNotEqual(crop["imageSize"], t["imageSize"])
                    self.assertTrue(all(isinstance(v, int) and v > 0 for v in crop["imageSize"]))

    def test_keys_sorted_and_fixed_four_decimals(self):
        crops = dict(self.pairs)["crops"]
        keys = [key for key, _ in crops]
        self.assertEqual(keys, sorted(keys))
        for cid, fields in crops:
            self.assertEqual([k for k, _ in fields], sorted(k for k, _ in fields), cid)
        numbers = raw_numbers(self.text)
        self.assertEqual(set(numbers), set(keys))
        for cid, fields in numbers.items():
            self.assertEqual(set(fields), {"x", "y", "size", "confidence"}, cid)
            for key, literal in fields.items():
                self.assertRegex(literal, FOUR, f"{cid}.{key}")

    def test_all_ids_match_illustrations_and_config(self):
        crops = set(self.table["crops"])
        self.assertEqual(len(self.files), 184, "Review W13a scope if the illustration set changes.")
        self.assertEqual(crops, set(self.files))
        self.assertEqual(crops, self.config_ids & set(self.files))
        self.assertTrue(set(self.files) <= self.config_ids, sorted(set(self.files) - self.config_ids))
        self.assertTrue(all(cid == cid.upper() for cid in crops))

    def test_squares_inside_the_real_images(self):
        default = tuple(self.table["imageSize"])
        for cid, crop in self.table["crops"].items():
            with self.subTest(cid=cid):
                with Image.open(self.files[cid]) as image:
                    size = image.size
                self.assertEqual(tuple(crop.get("imageSize", default)), size)
                check_inside(self, cid, crop, size)

    def test_size_minimum(self):
        for cid, crop in self.table["crops"].items():
            self.assertGreaterEqual(crop["size"], MIN_SIZE, cid)

    def test_override_table_well_formed(self):
        module = load_module()
        overrides = module.OVERRIDES
        self.assertIsInstance(overrides, dict)
        self.assertIn("default", overrides)
        for theme, table in overrides.items():
            self.assertIsInstance(theme, str)
            self.assertIsInstance(table, dict)
            self.assertLessEqual(len(table), 10, f"{theme}: overrides are for a handful of ids; edit the art instead")
            for cid, entry in table.items():
                with self.subTest(theme=theme, cid=cid):
                    self.assertRegex(cid, r"^[A-Z]+\d+$")
                    self.assertEqual(set(entry), {"x", "y", "size", "reason"})
                    for key in ("x", "y", "size"):
                        self.assertIsInstance(entry[key], float)
                        self.assertTrue(0.0 <= entry[key] <= 1.0)
                    self.assertGreaterEqual(entry["size"], MIN_SIZE)
                    self.assertIsInstance(entry["reason"], str)
                    self.assertGreaterEqual(len(entry["reason"].strip()), 15, "write why the square was replaced")
        for cid, entry in overrides["default"].items():
            with self.subTest(default_override=cid):
                self.assertIn(cid, self.files)
                with Image.open(self.files[cid]) as image:
                    check_inside(self, cid, entry, image.size)
                crop = self.table["crops"][cid]
                self.assertEqual(crop["method"], "override")
                self.assertTrue(crop["note"].startswith("override: " + entry["reason"]))
        for cid, crop in self.table["crops"].items():
            if crop["method"] == "override":
                self.assertIn(cid, overrides["default"])


class CropScriptRunTests(unittest.TestCase):
    """Runs the command line twice; the committed table must be reproduced byte for byte."""

    @classmethod
    def setUpClass(cls):
        cls.scratch = tempfile.TemporaryDirectory(prefix="w13a-crops-")
        cls.addClassCleanup(cls.scratch.cleanup)
        cls.outputs = []
        for name in ("first.json", "second.json"):
            out = Path(cls.scratch.name) / name
            result = subprocess.run(
                [sys.executable, "-B", str(SCRIPT), "--theme", "default", "--source", SOURCE, "--out", str(out)],
                cwd=ROOT, capture_output=True, text=True, timeout=1800)
            if result.returncode != 0:
                raise AssertionError(result.stdout + result.stderr)
            cls.outputs.append(out.read_bytes())

    def test_two_runs_identical(self):
        self.assertEqual(self.outputs[0], self.outputs[1])

    def test_committed_table_is_reproduced(self):
        self.assertEqual(self.outputs[0], TABLE.read_bytes())


class CropScriptGeneralTests(unittest.TestCase):
    """Other themes: either id case, non-square sources, per-theme overrides."""

    def test_mixed_case_ids_non_square_source_and_override(self):
        module = load_module("make_crops_probe")
        with tempfile.TemporaryDirectory(prefix="w13a-probe-") as folder:
            folder = Path(folder)
            shutil.copyfile(ROOT / SOURCE / "illustration_c39.png", folder / "illustration_C39.png")
            shutil.copyfile(ROOT / SOURCE / "illustration_c08.png", folder / "illustration_c08.png")
            module.OVERRIDES = {"probe": {"C39": {"x": 0.1, "y": 0.2, "size": 0.6,
                                                  "reason": "unit test probe square"}}}
            out = folder / "probe.json"
            module.build("probe", folder, out, None, log=lambda *args: None)
            table = json.loads(out.read_text(encoding="utf-8"))
            self.assertEqual(table["theme"], "probe")
            self.assertEqual(sorted(table["crops"]), ["C08", "C39"])
            probe = table["crops"]["C39"]
            self.assertEqual(probe["method"], "override")
            self.assertEqual((probe["x"], probe["y"], probe["size"]), (0.1, 0.2, 0.6))
            self.assertTrue(probe["note"].startswith("override: unit test probe square"))
            wide = table["crops"]["C08"]
            self.assertEqual(wide["method"], "alpha-core-1")
            self.assertEqual(wide["imageSize"], [1774, 887])
            self.assertEqual(wide["size"], 1.0)
            check_inside(self, "C08", wide, (1774, 887))
            module.OVERRIDES = {"probe": {"C99": {"x": 0.1, "y": 0.1, "size": 0.5, "reason": "no such picture"}}}
            with self.assertRaises(SystemExit):
                module.build("probe", folder, folder / "bad.json", None, log=lambda *args: None)

    def test_swiss_theme_upper_case_sources(self):
        module = load_module("make_crops_swiss")
        expected = file_ids(SWISS)
        self.assertTrue(expected, "the Swiss theme ships illustrations")
        with tempfile.TemporaryDirectory(prefix="w13a-swiss-") as folder:
            out = Path(folder) / "swiss.json"
            module.build("swiss", SWISS, out, None, log=lambda *args: None)
            table = json.loads(out.read_text(encoding="utf-8"))
        self.assertEqual(table["source"], "DicePrototype/Assets/Resources/Themes/swiss/Art")
        self.assertEqual(set(table["crops"]), set(expected))
        default = tuple(table["imageSize"])
        for cid, crop in table["crops"].items():
            with self.subTest(cid=cid):
                with Image.open(expected[cid]) as image:
                    size = image.size
                self.assertEqual(tuple(crop.get("imageSize", default)), size)
                check_inside(self, cid, crop, size)
                self.assertGreaterEqual(crop["size"], MIN_SIZE)


if __name__ == "__main__":
    unittest.main()
