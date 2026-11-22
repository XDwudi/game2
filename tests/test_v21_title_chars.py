"""W21 font source and generated text contracts."""

import json
import unittest

from fontTools.ttLib import TTFont
from tools.v21_title_chars import FONTS, GAME, ROOT, character_sets, manifest, sha


class TitleFontInputsTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.title, cls.bold = character_sets()

    def test_versioned_character_files_match_game_inputs(self):
        for chars, name in ((self.title, "title_chars"), (self.bold, "bold_chars")):
            self.assertEqual(chars, "".join(sorted(set(chars))))
            self.assertEqual(chars + "\n", (FONTS / f"Themes/{name}.txt").read_text(encoding="utf-8"))
        self.assertLessEqual(set(self.title), set(self.bold))

    def test_every_configured_name_is_covered(self):
        config = json.loads((GAME / "Data/P0Config.json").read_text(encoding="utf-8"))
        for category, items in config.items():
            if not isinstance(items, list):
                continue
            for item in items:
                if isinstance(item, dict):
                    for key in ("displayName", "title"):
                        with self.subTest(category=category, name=item.get(key)):
                            self.assertLessEqual(set(item.get(key, "")), set(self.title))

    def test_manifest_contains_verified_licenses_and_sources(self):
        current = manifest()
        self.assertEqual(current, json.loads((FONTS / "fonts_manifest.json").read_text(encoding="utf-8")))
        self.assertEqual(6, len(current["fonts"]))
        self.assertEqual(["LiberationSans-OFL.txt"], [f["packageFile"] for f in current["ifIncludedInBuild"]])
        for entry in current["fonts"] + current["ifIncludedInBuild"]:
            with self.subTest(font=entry["name"]):
                self.assertTrue(entry["copyright"] and entry["version"] and entry["source"])
                self.assertEqual(entry["licenseSha256"], sha(ROOT / entry["licenseFile"]))
                self.assertEqual(entry["fontSha256"], sha(ROOT / entry["fontFile"]))

    def test_theme_font_sources_cover_the_current_titles(self):
        entries = manifest()["fonts"][2:]
        fallback = set("²·‹›←↑→↓≈≤≥◆◇●✓")
        expected = {"sancai": fallback, "comic": fallback - {"·"}, "deco": fallback, "glass": set()}
        for entry in entries:
            path = ROOT / entry["fontFile"]
            with self.subTest(theme=path.parent.name), TTFont(path) as font:
                covered = set(map(chr, font.getBestCmap()))
                self.assertEqual(expected[path.parent.name], set(self.title) - covered)


if __name__ == "__main__":
    unittest.main()
