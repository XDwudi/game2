"""W13a compact icons: crop tables (provenance) and baked icons for the default theme and the five styles.

The committed tables and icons must be exactly what the script produces from the committed illustrations, so the
script is run and its output compared byte for byte with them (and with a second run).
V1.1 W22i: the five styles carry their whole sets (184 each; Swiss had its 7 trial redraws until then), with the
per-style overrides of make_crops.OVERRIDES.
"""

import importlib.util
import json
from pathlib import Path
import re
import shutil
import struct
import subprocess
import sys
import tempfile
import unittest

from PIL import Image

# Test discovery must not leave Python cache files beside production sources.
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
SCRIPT = ROOT / "tools/art_v11/crops/make_crops.py"
CONFIG = ROOT / "DicePrototype/Assets/Game/Data/P0Config.json"
ICONS = ROOT / "DicePrototype/Assets/Art/Art11/Icons"
THEMES = {
    "default": {"source": "DicePrototype/Assets/Art/Art071/Illustrations",
                "table": ROOT / "DicePrototype/Assets/Resources/IconCrops/default.json", "count": 184},
}
# V1.1 W22i: Swiss was 7 (its trial redraws); every style now ships the whole set.
for _style in ("swiss", "sancai", "comic", "deco", "glass"):
    THEMES[_style] = {"source": f"DicePrototype/Assets/Resources/Themes/{_style}/Art",
                      "table": ROOT / f"DicePrototype/Assets/Resources/IconCrops/{_style}.json", "count": 184}
STYLES = [theme for theme in THEMES if theme != "default"]
CONTENT_LISTS = ("controllers", "consumables", "services", "diceUpgrades")
TOP_KEYS = ["schema", "theme", "source", "generatedBy", "imageSize", "units", "crops"]
REQUIRED = {"x", "y", "size", "confidence", "method"}
OPTIONAL = {"note", "imageSize", "pad"}
METHODS = {"alpha-core-1", "override"}
MIN_SIZE = 0.40          # a crop smaller than 40% of the short side would be an over-zoom on this art
ICON_SIZE = 160
FOUR = re.compile(r"^-?\d\.\d{4}$")
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


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


def icon_files(folder):
    return {path.name[len("icon_"):-len(".png")]: path for path in folder.glob("icon_*.png")}


def png_chunks(data):
    """Chunk types of a PNG file, in order."""
    assert data[:8] == PNG_SIGNATURE
    types, at = [], 8
    while at < len(data):
        length, kind = struct.unpack(">I4s", data[at:at + 8])
        types.append(kind.decode("ascii"))
        at += 12 + length
    return types


def raw_numbers(text):
    """For each crop line of the table text: id -> {field: number literal as written}."""
    out = {}
    for line in text.splitlines():
        match = re.match(r'^    "([A-Z]+\d+)": \{(.*)\},?$', line)
        if match:
            out[match.group(1)] = dict(re.findall(r'"(x|y|size|confidence)": (-?[0-9.]+)', match.group(2)))
    return out


def check_square(test, cid, crop, size):
    """Inside the image; a padded square only has to stay inside along the long side, and must overhang the short
    side (it exists because the subject is wider than the picture is tall)."""
    W, H = size
    S = min(W, H)
    xi, yi, si = (round(crop[k] * 10000) for k in ("x", "y", "size"))
    inside_x = xi >= 0 and xi * W + si * S <= 10000 * W
    inside_y = yi >= 0 and yi * H + si * S <= 10000 * H
    if crop.get("pad"):
        test.assertNotEqual(W, H, cid)
        test.assertTrue(inside_x if W > H else inside_y, cid)
        test.assertGreater(si * S, 10000 * S, f"{cid}: a padded square is larger than the short side")
    else:
        test.assertTrue(inside_x and inside_y, cid)
        test.assertLessEqual(crop["size"], 1.0, cid)


class CropTableTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.texts = {theme: spec["table"].read_text(encoding="utf-8") for theme, spec in THEMES.items()}
        cls.tables = {theme: json.loads(text) for theme, text in cls.texts.items()}
        cls.files = {theme: file_ids(ROOT / spec["source"]) for theme, spec in THEMES.items()}
        config = json.loads(CONFIG.read_text(encoding="utf-8"))
        cls.config_ids = {item["id"] for key in CONTENT_LISTS for item in config[key]}

    def test_schema(self):
        for theme, text in self.texts.items():
            with self.subTest(theme=theme):
                self.assertEqual([key for key, _ in json.loads(text, object_pairs_hook=list)], TOP_KEYS)
                t = self.tables[theme]
                self.assertEqual(t["schema"], "icon-crops-1")
                self.assertEqual(t["theme"], theme)
                self.assertEqual(t["source"], THEMES[theme]["source"])
                self.assertEqual(t["generatedBy"], "tools/art_v11/crops/make_crops.py")
                self.assertEqual(t["imageSize"], [1254, 1254])
                self.assertIsInstance(t["units"], str)
                for cid, crop in t["crops"].items():
                    self.assertTrue(REQUIRED <= set(crop) <= REQUIRED | OPTIONAL, f"{cid}: {sorted(crop)}")
                    for key in ("x", "y", "size", "confidence"):
                        self.assertIsInstance(crop[key], float, cid)
                    self.assertIn(crop["method"], METHODS, cid)
                    self.assertTrue(0.0 <= crop["confidence"] <= 1.0, cid)
                    if "note" in crop:
                        self.assertTrue(crop["note"].strip(), cid)
                    if "imageSize" in crop:
                        self.assertNotEqual(crop["imageSize"], t["imageSize"], cid)
                    if "pad" in crop:
                        self.assertIs(crop["pad"], True, cid)

    def test_keys_sorted_and_fixed_four_decimals(self):
        for theme, text in self.texts.items():
            with self.subTest(theme=theme):
                crops = dict(json.loads(text, object_pairs_hook=list))["crops"]
                keys = [key for key, _ in crops]
                self.assertEqual(keys, sorted(keys))
                for cid, fields in crops:
                    self.assertEqual([k for k, _ in fields], sorted(k for k, _ in fields), cid)
                numbers = raw_numbers(text)
                self.assertEqual(set(numbers), set(keys))
                for cid, fields in numbers.items():
                    self.assertEqual(set(fields), {"x", "y", "size", "confidence"}, cid)
                    for key, literal in fields.items():
                        self.assertRegex(literal, FOUR, f"{cid}.{key}")

    def test_ids_match_illustrations_and_config(self):
        default = set(self.tables["default"]["crops"])
        self.assertEqual(len(self.files["default"]), 184, "Review W13a scope if the illustration set changes.")
        self.assertEqual(default, set(self.files["default"]))
        self.assertEqual(default, self.config_ids & set(self.files["default"]))
        self.assertTrue(set(self.files["default"]) <= self.config_ids)
        self.assertTrue(all(cid == cid.upper() for cid in default))
        # V1.1 W22i: every style, not Swiss alone (whose 7 trial redraws became the whole set).
        for theme in STYLES:
            with self.subTest(theme=theme):
                crops = set(self.tables[theme]["crops"])
                self.assertEqual(len(crops), THEMES[theme]["count"])
                self.assertEqual(crops, set(self.files[theme]))
                self.assertTrue(crops <= default, "a style's redraws replace default ids")
                self.assertTrue(all(cid == cid.upper() for cid in crops))

    def test_squares_inside_the_real_images(self):
        for theme, table in self.tables.items():
            default = tuple(table["imageSize"])
            for cid, crop in table["crops"].items():
                with self.subTest(theme=theme, cid=cid):
                    with Image.open(self.files[theme][cid]) as image:
                        size = image.size
                    self.assertEqual(tuple(crop.get("imageSize", default)), size)
                    check_square(self, cid, crop, size)

    def test_size_minimum(self):
        for theme, table in self.tables.items():
            for cid, crop in table["crops"].items():
                self.assertGreaterEqual(crop["size"], MIN_SIZE, f"{theme}/{cid}")

    def test_every_crop_has_an_icon_160_rgba_without_metadata(self):
        for theme, table in self.tables.items():
            folder = ICONS / theme
            icons = icon_files(folder)
            self.assertEqual(set(icons), set(table["crops"]), theme)
            others = sorted(path.name for path in folder.iterdir()
                            if not (path.name.startswith("icon_") and path.suffix in (".png", ".meta")))
            self.assertEqual(others, [], f"{theme}: only icon PNGs (and the .meta Unity adds later)")
            for cid, path in icons.items():
                with self.subTest(theme=theme, cid=cid):
                    chunks = png_chunks(path.read_bytes())
                    self.assertEqual(chunks[0], "IHDR")
                    self.assertEqual(chunks[-1], "IEND")
                    self.assertEqual(set(chunks[1:-1]), {"IDAT"}, "no text, time, gamma or colour-profile chunks")
                    with Image.open(path) as image:
                        self.assertEqual(image.size, (ICON_SIZE, ICON_SIZE))
                        self.assertEqual(image.mode, "RGBA")
                        self.assertEqual(image.info, {})

    def test_padded_icons_are_letterboxed(self):
        padded = 0
        for theme, table in self.tables.items():
            for cid, crop in table["crops"].items():
                if not crop.get("pad"):
                    continue
                padded += 1
                with self.subTest(theme=theme, cid=cid):
                    with Image.open(ICONS / theme / f"icon_{cid}.png") as image:
                        alpha = image.getchannel("A")
                    W, H = crop.get("imageSize", table["imageSize"])
                    edge = alpha.crop((0, 0, ICON_SIZE, 1)) if W > H else alpha.crop((0, 0, 1, ICON_SIZE))
                    self.assertEqual(edge.getextrema(), (0, 0), "outside the picture stays transparent")
        self.assertGreaterEqual(padded, 1, "C08 (1774x887 boat) is baked padded")

    def test_override_table_well_formed(self):
        module = load_module()
        overrides = module.OVERRIDES
        self.assertIsInstance(overrides, dict)
        self.assertIn("default", overrides)
        allowed = {"x", "y", "size", "reason", "pad"}
        for theme, table in overrides.items():
            self.assertIsInstance(theme, str)
            self.assertIsInstance(table, dict)
            self.assertLessEqual(len(table), 10, f"{theme}: overrides are for a handful of ids; edit the art instead")
            for cid, entry in table.items():
                with self.subTest(theme=theme, cid=cid):
                    self.assertRegex(cid, r"^[A-Z]+\d+$")
                    self.assertTrue({"x", "y", "size", "reason"} <= set(entry) <= allowed, sorted(entry))
                    for key in ("x", "y", "size"):
                        self.assertIsInstance(entry[key], float)
                    self.assertIsInstance(entry.get("pad", False), bool)
                    self.assertGreaterEqual(entry["size"], MIN_SIZE)
                    self.assertIsInstance(entry["reason"], str)
                    self.assertGreaterEqual(len(entry["reason"].strip()), 15, "write why the square was replaced")
        for theme, table in self.tables.items():
            for cid, entry in overrides.get(theme, {}).items():
                with self.subTest(table=theme, override=cid):
                    self.assertIn(cid, self.files[theme])
                    with Image.open(self.files[theme][cid]) as image:
                        check_square(self, cid, entry, image.size)
                    crop = table["crops"][cid]
                    self.assertEqual(crop["method"], "override")
                    self.assertEqual((crop["x"], crop["y"], crop["size"]), (entry["x"], entry["y"], entry["size"]))
                    self.assertEqual(crop.get("pad", False), entry.get("pad", False))
                    self.assertTrue(crop["note"].startswith("override: " + entry["reason"]))
            for cid, crop in table["crops"].items():
                if crop["method"] == "override":
                    self.assertIn(cid, overrides.get(theme, {}))


class CropScriptRunTests(unittest.TestCase):
    """Runs the command line twice per theme; committed tables and icons must be reproduced byte for byte."""

    @classmethod
    def setUpClass(cls):
        cls.scratch = tempfile.TemporaryDirectory(prefix="w13a-crops-")
        cls.addClassCleanup(cls.scratch.cleanup)
        cls.runs = {}
        for theme, spec in THEMES.items():
            for run in ("first", "second"):
                base = Path(cls.scratch.name) / run
                out = base / f"{theme}.json"
                result = subprocess.run(
                    [sys.executable, "-B", str(SCRIPT), "--theme", theme, "--source", spec["source"],
                     "--out", str(out), "--icons-dir", str(base / "icons")],
                    cwd=ROOT, capture_output=True, text=True, timeout=1800)
                if result.returncode != 0:
                    raise AssertionError(result.stdout + result.stderr)
                icons = {cid: path.read_bytes() for cid, path in icon_files(base / "icons" / theme).items()}
                cls.runs[theme, run] = (out.read_bytes(), icons)

    def test_two_runs_identical(self):
        for theme in THEMES:
            with self.subTest(theme=theme):
                self.assertEqual(self.runs[theme, "first"][0], self.runs[theme, "second"][0])
                self.assertEqual(self.runs[theme, "first"][1], self.runs[theme, "second"][1])

    def test_committed_tables_and_icons_are_reproduced(self):
        for theme, spec in THEMES.items():
            with self.subTest(theme=theme):
                table, icons = self.runs[theme, "first"]
                self.assertEqual(table, spec["table"].read_bytes())
                committed = {cid: path.read_bytes() for cid, path in icon_files(ICONS / theme).items()}
                self.assertEqual(set(icons), set(committed))
                for cid in sorted(icons):
                    self.assertEqual(icons[cid], committed[cid], f"{theme}/icon_{cid}.png")


class CropScriptGeneralTests(unittest.TestCase):
    """Other themes: either id case, non-square sources, padding, per-theme overrides, stale icons."""

    def test_mixed_case_ids_non_square_source_pad_and_override(self):
        module = load_module("make_crops_probe")
        source = ROOT / THEMES["default"]["source"]
        with tempfile.TemporaryDirectory(prefix="w13a-probe-") as folder:
            folder = Path(folder)
            shutil.copyfile(source / "illustration_c39.png", folder / "illustration_C39.png")
            shutil.copyfile(source / "illustration_c08.png", folder / "illustration_c08.png")
            module.OVERRIDES = {"probe": {"C39": {"x": 0.1, "y": 0.2, "size": 0.6,
                                                  "reason": "unit test probe square"}}}
            icons = folder / "icons"
            (icons / "probe").mkdir(parents=True)
            (icons / "probe" / "icon_X99.png").write_bytes(b"stale")
            out = folder / "probe.json"
            module.build("probe", folder, out, None, icons, log=lambda *args: None)
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
            self.assertIs(wide.get("pad"), True, "the 1774x887 boat loses too much to any square inside it")
            check_square(self, "C08", wide, (1774, 887))
            self.assertEqual(set(icon_files(icons / "probe")), {"C08", "C39"}, "stale icons are removed")
            with Image.open(icons / "probe" / "icon_C08.png") as image:
                self.assertEqual((image.size, image.mode), ((ICON_SIZE, ICON_SIZE), "RGBA"))
                self.assertEqual(image.getchannel("A").crop((0, 0, ICON_SIZE, 1)).getextrema(), (0, 0))
            module.OVERRIDES = {"probe": {"C99": {"x": 0.1, "y": 0.1, "size": 0.5, "reason": "no such picture"}}}
            with self.assertRaises(SystemExit):
                module.build("probe", folder, folder / "bad.json", None, log=lambda *args: None)
            module.OVERRIDES = {"probe": {"C39": {"x": 0.6, "y": 0.1, "size": 0.5, "reason": "leaves the image"}}}
            with self.assertRaises(SystemExit):
                module.build("probe", folder, folder / "bad.json", None, log=lambda *args: None)


if __name__ == "__main__":
    unittest.main()
