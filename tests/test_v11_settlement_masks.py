"""W17 masks at their actual display scales and the optional-override audit boundary."""
import contextlib
import importlib.util
import io
import itertools
import json
import tempfile
import unittest
from pathlib import Path
from unittest.mock import patch
from xml.etree import ElementTree

from PIL import Image

ROOT = Path(__file__).resolve().parents[1]


def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, ROOT / path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


build = load_module("settlement11", "tools/art_v11/default/build_settlement11.py")
audit = load_module("contract11", "tools/art_v11/theme_contract.py")


def number_of_runs(mask, value, box):
    levels = [v >= 128 for v in mask.crop(box).getdata()]
    return sum(1 for level, group in itertools.groupby(levels) if level == value)


class SettlementMaskTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.temp = tempfile.TemporaryDirectory()
        cls.addClassCleanup(cls.temp.cleanup)
        cls.output = Path(cls.temp.name)
        build.build(cls.output)
        cls.rows = json.loads((cls.output / "theme_contract.expected.json").read_text())["sprites"]

    def test_seven_white_alpha_masks_match_contract_and_svg(self):
        self.assertEqual({row["name"] for row in self.rows}, audit.OPTIONAL_UI_OVERRIDES)
        self.assertEqual(len(self.rows), 7)
        for row in self.rows:
            name = row["name"]
            with self.subTest(name=name), Image.open(self.output / "UI" / (name + ".png")) as sprite:
                svg = ElementTree.parse(self.output / "svg" / (name + ".svg")).getroot()
                self.assertEqual(sprite.mode, "RGBA")
                self.assertEqual(sprite.size, (int(svg.get("width")), int(svg.get("height"))))
                self.assertEqual(list(sprite.size), row["size"])
                self.assertTrue(row["optionalOverride"])
                self.assertEqual(row["border"], [0] * 4)
                self.assertEqual(row["pixelsPerUnit"], 200)
                self.assertEqual(sprite.getextrema(), ((255, 255), (255, 255), (255, 255), (0, 255)))
                self.assertEqual(sprite.size, (352,352) if "tier" in name else (96,96))

    def test_frames_keep_clear_window_clear_stamp_and_readable_tier(self):
        stamp = None
        for tier in range(1, 5):
            with Image.open(self.output / "UI" / f"ui_emblem_tier{tier}.png") as sprite:
                alpha = sprite.getchannel("A")
            self.assertIsNone(alpha.crop((32,32,320,320)).getbbox())
            stamp_region = alpha.crop((136,232,352,352)).tobytes()
            if stamp is None:
                stamp = stamp_region
            self.assertEqual(stamp_region, stamp)
            for size in (60, 72, 176):
                with self.subTest(tier=tier, size=size):
                    scaled = alpha.resize((size,size), Image.Resampling.LANCZOS)
                    if tier < 4:
                        y = int(size * .051)
                        gaps = number_of_runs(scaled, False, (int(size*.36),y,int(size*.64),y+1))
                        self.assertEqual(gaps, tier)
                    else:
                        y = int(size * .026)
                        tines = number_of_runs(scaled, True, (int(size*.37),y,int(size*.63),y+1))
                        self.assertEqual(tines, 3)

    def test_chip_silhouettes_keep_flag_pip_and_coin_rims(self):
        for size in (28, 36):
            for name in ("stage", "submit", "interest"):
                with Image.open(self.output / "UI" / f"ui_icon_{name}.png") as sprite:
                    mask = sprite.getchannel("A").resize((size,size), Image.Resampling.LANCZOS)
                def point(x,y):
                    return mask.getpixel((int(x*size/96),int(y*size/96))) >= 128
                if name == "stage":
                    self.assertTrue(point(21,62))
                    self.assertTrue(point(60,32))
                    self.assertFalse(point(80,32))
                elif name == "submit":
                    self.assertTrue(point(37,23))
                    self.assertFalse(point(48,23))
                    self.assertTrue(point(48,83))
                else:
                    x = int(37*size/96)
                    self.assertEqual(number_of_runs(mask, True, (x,0,x+1,size)), 3)
                    self.assertTrue(point(75,22))

    def test_optional_default_is_allowed_but_invalid_override_and_missing_required_fail(self):
        with tempfile.TemporaryDirectory() as tmp:
            folder = Path(tmp)
            ui = folder / "example/UI"
            ui.mkdir(parents=True)
            contract = folder / "contract.json"
            contract.write_text(json.dumps({"sprites": [
                {"name": "ui_icon_stage", "size": [96,96], "referenced": True, "optionalOverride": True},
                {"name": "ui_button_bevel", "size": [16,16], "referenced": True}]}))
            with patch.object(audit, "CONTRACT", contract), contextlib.redirect_stdout(io.StringIO()) as result:
                self.assertEqual(audit.cmd_audit("example", True, folder), 1)
                self.assertIn("FALLBACK ui_button_bevel", result.getvalue())
                Image.new("RGBA",(16,16)).save(ui / "ui_button_bevel.png")
                self.assertEqual(audit.cmd_audit("example", True, folder), 0)
                self.assertIn("OPTIONAL DEFAULT ui_icon_stage", result.getvalue())
                Image.new("RGBA",(16,16)).save(ui / "ui_icon_stage.png")
                self.assertEqual(audit.cmd_audit("example", True, folder), 1)
                self.assertIn("SIZE ui_icon_stage", result.getvalue())
                Image.new("RGBA",(96,96)).save(ui / "ui_icon_stage.png")
                self.assertEqual(audit.cmd_audit("example", True, folder), 0)


if __name__ == "__main__":
    unittest.main()
