"""Checks for the V1.1 default-theme surfaces (tools/art_v11/default/build_default11.py, W11 and W13)."""

import importlib.util
from pathlib import Path
import re
import sys
import unittest

import numpy as np
from PIL import Image
from tools.art_v11.theme_contract import OPTIONAL_UI_OVERRIDES


sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
SURFACES = ROOT / "DicePrototype/Assets/Art/Art11/Surfaces"
SETUP = ROOT / "DicePrototype/Assets/Game/Editor/V11Setup.cs"
CARD_TEMPLATE = ("ui_card_frame_rarity", "ui_gem_common", "ui_gem_advanced", "ui_gem_rare")


class DefaultSurfaceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        spec = importlib.util.spec_from_file_location("art11_default_builder", ROOT / "tools/art_v11/default/build_default11.py")
        cls.builder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.builder)
        b = cls.builder
        # The plates main() writes, drawn in memory: the test never writes into Assets.
        cls.plates = {"ui_dice_tray": b.dice_tray(), "ui_topbar": b.topbar(), "ui_chip_formula": b.chip_formula(),
                      "ui_stage_card": b.stage_card(), "ui_card_frame_rarity": b.card_frame_rarity(),
                      "ui_gem_common": b.gem_common(), "ui_gem_advanced": b.gem_advanced(), "ui_gem_rare": b.gem_rare()}
        cls.images = {name: np.asarray(plate.export().convert("RGBA")) for name, plate in cls.plates.items()}

    def test_every_border_has_a_plate_and_a_shipped_file(self):
        self.assertEqual(set(self.builder.BORDERS), set(self.plates))
        self.assertEqual({p.stem for p in SURFACES.glob("*.png")}, set(self.plates) | OPTIONAL_UI_OVERRIDES)

    def test_shipped_pixels_match_regeneration(self):
        for name, pixels in self.images.items():
            with self.subTest(sprite=name):
                with Image.open(SURFACES / (name + ".png")) as shipped:
                    np.testing.assert_array_equal(np.asarray(shipped.convert("RGBA")), pixels)

    def test_editor_setup_mirrors_the_borders(self):
        # V11Setup.Borders: {"name",new Vector4(l,b,r,t)} or {"name",Vector4.zero}.
        text = SETUP.read_text(encoding="utf-8")
        mirrored = {}
        for name, values, zero in re.findall(r'\{"(\w+)",(?:new Vector4\(([^)]*)\)|(Vector4\.zero))\}', text):
            mirrored[name] = (0, 0, 0, 0) if zero else tuple(int(v) for v in values.split(","))
        borders = {name: tuple(border) for name, border in self.builder.BORDERS.items()}
        borders.update({name: (0, 0, 0, 0) for name in OPTIONAL_UI_OVERRIDES})
        self.assertEqual(mirrored, borders)

    def test_card_template_sprites_are_tintable_shapes(self):
        for name in CARD_TEMPLATE:
            with self.subTest(sprite=name):
                pixels = self.images[name]
                visible = pixels[..., 3] > 0
                self.assertTrue(visible.any())
                rgb = pixels[..., :3].astype(int)
                # The lit and shaded body is neutral (white and greys), so the runtime tint carries the colour; only the
                # dark print ink of the outline (the default look's ink, below 96 in every channel) may carry a hue.
                body = visible & (rgb.max(axis=2) >= 96)
                self.assertTrue(body.any())
                self.assertLessEqual(int((rgb.max(axis=2) - rgb.min(axis=2))[body].max()), 24)
                if name.startswith("ui_gem_"):
                    ink = (pixels[..., 3] >= 200) & (rgb.max(axis=2) < 96)
                    self.assertGreater(float(ink.sum()) / float(visible.sum()), .08, "The gem keeps its ink outline.")

    def test_rarity_ring_is_an_open_band_inside_its_slice_border(self):
        pixels = self.images["ui_card_frame_rarity"]
        left, bottom, right, top = self.builder.BORDERS["ui_card_frame_rarity"]
        alpha = pixels[..., 3]
        self.assertEqual(int(alpha[alpha.shape[0] // 2, alpha.shape[1] // 2]), 0, "The ring's centre is open.")
        # The band (and its print line) stays within the slice border on every side.
        mid = alpha.shape[0] // 2
        for side in (alpha[mid], alpha[mid, ::-1], alpha[:, mid], alpha[::-1, mid]):
            opaque = np.flatnonzero(side >= 128)
            self.assertGreater(len(opaque), 0)
            band = opaque[opaque < alpha.shape[0] // 2]
            self.assertLess(int(band.max()), min(left, bottom, right, top))

    def test_gems_are_whole_shapes_that_differ(self):
        masks = {}
        for name in CARD_TEMPLATE[1:]:
            with self.subTest(sprite=name):
                alpha = self.images[name][..., 3]
                self.assertEqual(alpha.shape, (self.builder.GEM, self.builder.GEM))
                self.assertEqual(self.builder.BORDERS[name], (0, 0, 0, 0))
                for corner in (alpha[0, 0], alpha[0, -1], alpha[-1, 0], alpha[-1, -1]):
                    self.assertEqual(int(corner), 0)
                masks[name] = alpha >= 128
        # Round, diamond and star: each pair of silhouettes differs in at least a tenth of their union.
        names = sorted(masks)
        for i in range(len(names)):
            for j in range(i + 1, len(names)):
                a, b = masks[names[i]], masks[names[j]]
                self.assertGreater(float((a ^ b).sum()) / float((a | b).sum()), .10, names[i] + " / " + names[j])


if __name__ == "__main__":
    unittest.main()
