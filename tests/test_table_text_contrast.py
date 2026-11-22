"""Checks for tools/art_v11/table_text_contrast.py (W20X item 3: text drawn straight on the table, THEME_SPEC 3.1)."""

import contextlib
import importlib.util
import io
import json
import re
import sys
import tempfile
import unittest
from pathlib import Path

import numpy as np
from PIL import Image

# Test discovery must not leave Python cache files beside production sources.
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
_SPEC = importlib.util.spec_from_file_location("table_text_contrast_under_test", ROOT / "tools/art_v11/table_text_contrast.py")
tool = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(tool)

PALETTE = {"textColor": "F7EED9", "muted": "B8C9C5", "gold": "F3BA58", "ink": "162629"}


def entry(name="Screen/Label", rect=(10, 20, 30, 10), key="textColor", colors=None, screen="menu"):
    e = {"screen": screen, "name": name, "rect": list(rect), "colorKey": key}
    if colors is not None:
        e["colors"] = colors
    return e


class Fixture:
    """A temporary project: a list file, a default table picture and one style folder."""

    def __init__(self, root, entries, page=None, table=None, palette=None):
        self.root = Path(root)
        self.list = self.root / "table_text.json"
        self.list.write_text(json.dumps({"schema": tool.SCHEMA, "palette": PALETTE, "entries": entries}), encoding="utf-8")
        self.default_table = self.root / "bg_table_scene.png"
        Image.new("RGB", tool.DESIGN, (0, 0, 0)).save(self.default_table)
        self.themes = self.root / "Themes"
        folder = self.themes / "probe"
        (folder / "Table").mkdir(parents=True)
        (folder / "theme.json").write_text(json.dumps({"id": "probe", "palette": palette or {}}), encoding="utf-8")
        if page is not None:
            page.save(folder / "Table" / "page_scene.png")
        if table is not None:
            table.save(folder / "Table" / "table_scene.png")

    def check(self, theme="probe"):
        return tool.check(theme, self.list, self.themes, self.default_table)


class ContrastMathTests(unittest.TestCase):
    def test_wcag_reference_values(self):
        self.assertAlmostEqual(float(tool.contrast((0, 0, 0), (1, 1, 1))), 21.0, places=6)
        self.assertAlmostEqual(float(tool.contrast((0.5, 0.2, 0.9), (0.5, 0.2, 0.9))), 1.0, places=9)
        grey = tool.parse_hex("777777")[:3]
        self.assertAlmostEqual(float(tool.contrast(grey, (1, 1, 1))), 4.478, places=3)

    def test_hex_parsing(self):
        self.assertEqual(tool.parse_hex("#FF0000"), (1.0, 0.0, 0.0, 1.0))
        self.assertEqual(tool.parse_hex("00FF0080")[3], 128 / 255)
        for bad in ("12345", "GGGGGG", ""):
            with self.subTest(bad=bad), self.assertRaises(tool.ListError):
                tool.parse_hex(bad)


class BackdropTests(unittest.TestCase):
    def test_page_scene_is_preferred_then_table_scene_and_the_default_draws_its_own(self):
        with tempfile.TemporaryDirectory() as root:
            both = Fixture(root, [entry()], page=Image.new("RGB", tool.DESIGN), table=Image.new("RGB", tool.DESIGN))
            self.assertEqual(tool.backdrop_path("probe", both.themes, both.default_table).name, "page_scene.png")
            (both.themes / "probe" / "Table" / "page_scene.png").unlink()
            self.assertEqual(tool.backdrop_path("probe", both.themes, both.default_table).name, "table_scene.png")
            self.assertEqual(tool.backdrop_path("default", both.themes, both.default_table), both.default_table)
            (both.themes / "probe" / "Table" / "table_scene.png").unlink()
            with self.assertRaises(tool.ListError):
                tool.backdrop_path("probe", both.themes, both.default_table)

    def test_a_16_by_9_picture_of_another_size_is_scaled_and_another_shape_refused(self):
        # The default style's table picture is 1672x941; the felt shader stretches it over the screen.
        with tempfile.TemporaryDirectory() as root:
            f = Fixture(root, [entry()], page=Image.new("RGB", (1280, 720), (0, 0, 0)))
            self.assertEqual(f.check()["entries"], 1)
            Image.new("RGB", (1280, 1024)).save(f.themes / "probe" / "Table" / "page_scene.png")
            with self.assertRaises(tool.ListError):
                f.check()


class PaletteTests(unittest.TestCase):
    def test_style_overrides_and_defaults(self):
        with tempfile.TemporaryDirectory() as root:
            f = Fixture(root, [entry()], page=Image.new("RGB", tool.DESIGN), palette={"muted": "112233", "notAKey": "445566"})
            palette = tool.theme_palette("probe", PALETTE, f.themes)
            self.assertEqual(palette["muted"], "112233")
            self.assertEqual(palette["textColor"], PALETTE["textColor"])
            self.assertNotIn("notAKey", palette)
            self.assertEqual(tool.theme_palette("default", PALETTE, f.themes), PALETTE)

    def test_recorded_colour_wins_over_the_palette_key(self):
        e = entry(key="muted", colors={"probe": "FF0000"})
        self.assertEqual(tool.text_colour(e, "probe", PALETTE), ((1.0, 0.0, 0.0, 1.0), "drawn"))
        self.assertEqual(tool.text_colour(e, "other", PALETTE)[1], "palette")
        with self.assertRaises(tool.ListError):
            tool.text_colour(entry(key="custom"), "other", PALETTE)


class WorstCaseTests(unittest.TestCase):
    def test_the_worst_pixel_of_the_rectangle_decides(self):
        # Black page with one white pixel column inside the rectangle and a white block just outside it.
        picture = np.zeros((tool.DESIGN[1], tool.DESIGN[0], 3), dtype=np.uint8)
        picture[20:30, 25, :] = 255
        picture[20:30, 40:60, :] = 255
        with tempfile.TemporaryDirectory() as root:
            f = Fixture(root, [entry(rect=(10, 20, 30, 10), key="textColor")], page=Image.fromarray(picture))
            result = f.check()
        row = result["rows"][0]
        white = float(tool.contrast(tool.parse_hex(PALETTE["textColor"])[:3], (1, 1, 1)))
        self.assertAlmostEqual(row["worst"], round(white, 2))
        self.assertEqual(row["pixels"], 300)
        self.assertAlmostEqual(row["shareBelow"], round(10 / 300, 4))
        self.assertTrue(row["below"])
        self.assertEqual(result["below"], 1)

    def test_a_see_through_text_is_blended_over_each_pixel_in_linear_light(self):
        black = np.zeros((10, 10, 3))
        worst, _, _ = tool.worst_contrast(black, (0, 0, 10, 10), (1.0, 1.0, 1.0, 0.5))
        # Half white over black in linear light is linear 0.5, sRGB 0.7354 (luminance 0.5): (0.55 / 0.05) = 11.
        self.assertAlmostEqual(worst, 11.0, places=4)
        self.assertAlmostEqual(float(tool.blend((1, 1, 1), 0.5, np.zeros(3))[0]), 0.7354, places=4)

    def test_a_scrim_between_table_and_text_is_blended_first(self):
        white = np.ones((10, 10, 3))
        plain, _, _ = tool.worst_contrast(white, (0, 0, 10, 10), (0.0, 0.0, 0.0, 1.0))
        veiled, _, _ = tool.worst_contrast(white, (0, 0, 10, 10), (0.0, 0.0, 0.0, 1.0), ["00000080"])
        self.assertAlmostEqual(plain, 21.0, places=6)
        # Black at 128/255 over white leaves linear 1 - 0.502 = 0.498 under black text: (0.548 / 0.05).
        self.assertAlmostEqual(veiled, (1 - 128 / 255 + 0.05) / 0.05, places=4)

    def test_a_text_found_on_a_surface_in_a_captured_style_is_skipped_there(self):
        picture = Image.new("RGB", tool.DESIGN, (0, 0, 0))
        entries = [entry(name="A"), dict(entry(name="B"), themes=["other"])]
        with tempfile.TemporaryDirectory() as root:
            f = Fixture(root, entries, page=picture)
            data = json.loads(f.list.read_text(encoding="utf-8"))
            data["themes"] = ["probe", "other"]
            f.list.write_text(json.dumps(data), encoding="utf-8")
            self.assertEqual([r["name"] for r in f.check()["rows"]], ["A"])
            data["themes"] = ["other"]
            f.list.write_text(json.dumps(data), encoding="utf-8")
            self.assertEqual(sorted(r["name"] for r in f.check()["rows"]), ["A", "B"])

    def test_a_rectangle_outside_the_picture_is_an_error(self):
        with self.assertRaises(tool.ListError):
            tool.worst_contrast(np.zeros((10, 10, 3)), (20, 20, 5, 5), (1, 1, 1, 1))


class CommandTests(unittest.TestCase):
    def run_main(self, args):
        out = io.StringIO()
        with contextlib.redirect_stdout(out):
            code = tool.main(args)
        return code, out.getvalue()

    def test_strict_fails_only_when_a_text_is_under_the_minimum(self):
        dark = Image.new("RGB", tool.DESIGN, (0, 0, 0))
        with tempfile.TemporaryDirectory() as root:
            f = Fixture(root, [entry(key="textColor")], page=dark)
            tool_themes, tool_default = tool.THEMES, tool.DEFAULT_TABLE
            tool.THEMES, tool.DEFAULT_TABLE = f.themes, f.default_table
            try:
                code, text = self.run_main(["probe", "--list", str(f.list), "--strict"])
                self.assertEqual(code, 0, text)
                self.assertIn("TABLE_TEXT_OK", text)
                Image.new("RGB", tool.DESIGN, (255, 255, 255)).save(f.themes / "probe" / "Table" / "page_scene.png")
                code, text = self.run_main(["probe", "--list", str(f.list), "--strict"])
                self.assertEqual(code, 1, text)
                self.assertIn("| BELOW |", text)
                code, _ = self.run_main(["probe", "--list", str(f.list)])
                self.assertEqual(code, 0)
            finally:
                tool.THEMES, tool.DEFAULT_TABLE = tool_themes, tool_default

    def test_a_broken_list_is_reported(self):
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / "bad.json"
            path.write_text(json.dumps({"schema": "other", "palette": PALETTE, "entries": []}), encoding="utf-8")
            code, text = self.run_main(["default", "--list", str(path)])
        self.assertEqual(code, 2)
        self.assertIn("TABLE_TEXT_ERROR", text)


class ShippedListTests(unittest.TestCase):
    """The list the PlayMode capture wrote (tools/art_v11/table_text.json) is well formed and every shipped style checks."""

    def test_the_list_loads_and_every_shipped_style_runs(self):
        data = tool.load_list(tool.LIST)
        themes = ["default"] + sorted(p.name for p in tool.THEMES.iterdir() if (p / "theme.json").exists())
        self.assertCountEqual(data["themes"], themes)
        prefix = "PLAY_W20X_03_TableTextOnNonPlayScreens 1920x1080, text 100 %, "
        self.assertTrue(data["source"].startswith(prefix))
        counts, separator, screen_list = data["source"][len(prefix):].partition(". Screens: ")
        self.assertTrue(separator)
        screens = screen_list.split(", ")
        self.assertEqual(len(screens), 56)
        self.assertNotIn("", screens)
        self.assertEqual(len(set(screens)), len(screens))
        observed = []
        for item in counts.split(", "):
            match = re.fullmatch(r"([a-z]+)=(\d+) screens/(\d+) label samples", item)
            self.assertIsNotNone(match, item)
            theme, pages, samples = match.groups()
            self.assertEqual(int(pages), len(screens))
            self.assertGreaterEqual(int(samples), len(screens))
            observed.append(theme)
        self.assertCountEqual(observed, themes)
        self.assertEqual(data["entries"], [], "Every foreground label on all measured pages belongs on a surface.")
        for theme in themes:
            for mapping in ("authored", "drawn"):
                with self.subTest(theme=theme, mapping=mapping):
                    result = tool.check(theme, mapping=mapping)
                    self.assertEqual((result["entries"], result["below"], result["rows"]), (0, 0, []))

    def test_the_drawn_mapping_squeezes_a_scaled_picture_like_the_felt_shader(self):
        # A 1920x1080 picture imported at 2048x1024 (nPOTScale 1) is fitted by the 2:1 aspect: 960 rows of picture between
        # two bands of felt x 0.24 (60 rows each at 1080).
        with tempfile.TemporaryDirectory() as root:
            png = Path(root) / "table_scene.png"
            Image.new("RGB", tool.DESIGN, (255, 255, 255)).save(png)
            (Path(root) / "table_scene.png.meta").write_text("TextureImporter:\n  nPOTScale: 1\n", encoding="utf-8")
            self.assertEqual(tool.imported_size(png), (2048, 1024))
            drawn = tool.load_backdrop(png, "drawn", "FFFFFF")
            authored = tool.load_backdrop(png, "authored")
        self.assertTrue(np.allclose(authored, 1.0))
        dim = float(tool._srgb(0.24))
        self.assertTrue(np.allclose(drawn[:59], dim) and np.allclose(drawn[-59:], dim))
        self.assertTrue(np.allclose(drawn[61:1019], 1.0))


if __name__ == "__main__":
    unittest.main()
