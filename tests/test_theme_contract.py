"""Checks for tools/art_v11/theme_contract.py's audit of the optional Table/page_scene.png (THEME_SPEC 3, W20X)."""

import contextlib
import importlib.util
import io
import json
import sys
import tempfile
import unittest
from pathlib import Path

from PIL import Image

# Test discovery must not leave Python cache files beside production sources.
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
_SPEC = importlib.util.spec_from_file_location("theme_contract_under_test", ROOT / "tools/art_v11/theme_contract.py")
contract_tool = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(contract_tool)
CONTRACT = json.loads((ROOT / "tools/art_v11/theme_contract.json").read_text(encoding="utf-8"))


def audit(theme, strict=False, root=None):
    out = io.StringIO()
    with contextlib.redirect_stdout(out):
        code = contract_tool.cmd_audit(theme, strict, root or contract_tool.THEMES)
    return code, out.getvalue()


class PageSceneAuditTests(unittest.TestCase):
    def theme_with(self, root, page_size=None, mode="RGB"):
        """A one-sprite theme folder, with a Table/page_scene.png of the given size when one is given."""
        ui = Path(root) / "probe" / "UI"
        ui.mkdir(parents=True)
        sprite = next(s for s in CONTRACT["sprites"] if s["referenced"])
        Image.new("RGBA", tuple(sprite["size"])).save(ui / (sprite["name"] + ".png"))
        if page_size:
            table = Path(root) / "probe" / "Table"
            table.mkdir()
            Image.new(mode, page_size).save(table / "page_scene.png")

    def test_a_1920_by_1080_page_scene_is_accepted(self):
        with tempfile.TemporaryDirectory() as root:
            self.theme_with(root, (1920, 1080))
            code, text = audit("probe", root=root)
        self.assertEqual(code, 0)
        self.assertIn("  OPTIONAL Table/page_scene 1920x1080\n", text)
        self.assertNotIn("SIZE Table/", text)

    def test_a_page_scene_of_another_size_fails(self):
        for size in ((1920, 1000), (1280, 720)):
            with self.subTest(size=size), tempfile.TemporaryDirectory() as root:
                self.theme_with(root, size)
                code, text = audit("probe", root=root)
                self.assertEqual(code, 1)
                self.assertIn(f"SIZE Table/page_scene: {size[0]}x{size[1]}, expected 1920x1080", text)
                self.assertIn("AUDIT_FAILED", text)

    def test_a_page_scene_must_be_a_colour_image(self):
        with tempfile.TemporaryDirectory() as root:
            self.theme_with(root, (1920, 1080), mode="L")
            code, text = audit("probe", root=root)
        self.assertEqual(code, 1)
        self.assertIn("MODE Table/page_scene: mode L, expected RGB", text)

    def test_a_theme_without_page_scene_audits_as_before(self):
        with tempfile.TemporaryDirectory() as root:
            self.theme_with(root)
            code, text = audit("probe", root=root)
        self.assertEqual(code, 0)
        self.assertNotIn("Table/", text)

    def test_shipped_themes_audit_as_before(self):
        # Swiss and sancai ship no page_scene: their audits print exactly what they printed before the check existed.
        fallbacks = "".join("  OPTIONAL DEFAULT " + s["name"] + "\n" for s in CONTRACT["sprites"]
                            if s.get("optionalOverride"))
        for theme in ("swiss", "sancai"):
            for strict in (False, True):
                with self.subTest(theme=theme, strict=strict):
                    self.assertEqual(audit(theme, strict),
                                     (0, f"theme {theme}: 53 sprites shipped, 0 referenced defaults not overridden\n"
                                      + fallbacks + "AUDIT_OK\n"))
        # The comic kit ships one (W20 round 3).
        self.assertEqual(audit("comic", True), (0, "theme comic: 53 sprites shipped, 0 referenced defaults not overridden\n"
                                                   + fallbacks + "  OPTIONAL Table/page_scene 1920x1080\nAUDIT_OK\n"))


if __name__ == "__main__":
    unittest.main()
