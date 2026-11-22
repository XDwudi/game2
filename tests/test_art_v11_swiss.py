"""Independent asset-contract checks for the W12 Swiss UI kit."""

import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

import numpy as np
from PIL import Image


# Test discovery must not leave Python cache files beside production sources.
sys.dont_write_bytecode = True
ROOT = Path(__file__).resolve().parents[1]
THEME = Path("DicePrototype/Assets/Resources/Themes/swiss")
REVIEW = Path("ArtSource/Art11/swiss/contact_sheet.png")
CONTRACT = json.loads((ROOT / "tools/art_v11/theme_contract.json").read_text(encoding="utf-8"))
# W14's shop stopped drawing these three; the contract regenerated on 2026-11-18 lists them as unreferenced (53 -> 50).
# This kit was built before that and still ships them, so its checks keep covering them (STATE, W14 decision 4).
KEPT_UNREFERENCED = {"ui_price_chip", "ui_pedestal", "ui_frame_enchant"}
SPRITES = [sprite for sprite in CONTRACT["sprites"] if (sprite["referenced"] or sprite["name"] in KEPT_UNREFERENCED) and not sprite.get("optionalOverride")]

# Deliberately independent of the generator's dispatch and colour conventions.
NEUTRAL_NAMES = frozenset({
    "ui_button_bevel", "ui_button_ghost", "ui_frame_block", "ui_frame_enchant",
    "ui_ring", "ui_price_chip", "ui_progress_fill", "ui_progress_track",
    "ui_section_header", "ui_pip_dot", "ui_icon_arrow", "ui_icon_book",
    "ui_icon_gear", "fx_arc_short", "fx_score_spark", "fx_square_chip",
    # V1.1 W11 surfaces: tinted by palette colours at runtime (GameApp.Play11.cs Surface11).
    "ui_dice_tray", "ui_topbar", "ui_chip_formula", "ui_stage_card",
    # V1.1 W13 card template: tinted by the rarity colours or Ghost (GameApp.Card11.cs TintedSprite13).
    "ui_card_frame_rarity", "ui_gem_common", "ui_gem_advanced", "ui_gem_rare",
})
# fx_score_ray left this list when W11 stopped using it; the kit no longer ships it.
PAPER_SURFACES = (
    "ui_paper_panel", "ui_card_controller", "ui_card_controller_compact",
    "ui_card_item", "ui_card_shop", "ui_chip_label",
)
DARK_SURFACES = (
    "ui_panel_hud", "ui_section_panel", "ui_slot_inventory", "ui_tab_bottom",
    "ui_info_inset", "ui_tray_front",
)


def png_hashes(folder):
    return {path.relative_to(folder).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in folder.rglob("*.png")}


class SwissArtTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        spec = importlib.util.spec_from_file_location(
            "swiss_asset_builder", ROOT / "tools/art_v11/swiss/build_swiss.py")
        cls.builder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.builder)
        scratch_parent = ROOT / "ArtSource/Art11/swiss"
        scratch_parent.mkdir(parents=True, exist_ok=True)
        cls.scratch = tempfile.TemporaryDirectory(prefix="test-rebuild-", dir=scratch_parent)
        cls.addClassCleanup(cls.scratch.cleanup)
        cls.first = Path(cls.scratch.name) / "first"
        cls.second = Path(cls.scratch.name) / "second"
        cls.builder.build(cls.first)
        cls.builder.build(cls.second)
        cls.first_hashes = png_hashes(cls.first)
        cls.second_hashes = png_hashes(cls.second)

    def test_exact_referenced_inventory(self):
        # W13 (CARD_TEMPLATE section 6) added the rarity ring and three gems to the contract: 49 -> 53 sprites, 53 -> 57 files.
        self.assertEqual(len(SPRITES), 53, "Review W12 scope if the input contract changes.")
        expected = {sprite["name"] for sprite in SPRITES}
        self.assertEqual({path.stem for path in (ROOT / THEME / "UI").glob("*.png")}, expected)
        expected_paths = {(THEME / "UI" / (name + ".png")).as_posix() for name in expected}
        expected_paths.update({(THEME / "Table/table_scene.png").as_posix(),
                               (THEME / "Table/felt_tile.png").as_posix(),
                               (THEME / "preview.png").as_posix(), REVIEW.as_posix()})
        self.assertEqual(set(self.first_hashes), expected_paths)
        self.assertEqual(len(self.first_hashes), 57)

    def test_all_sprite_sizes_and_rgba_match_defaults(self):
        for sprite in SPRITES:
            with self.subTest(sprite=sprite["name"]):
                with Image.open(ROOT / THEME / "UI" / (sprite["name"] + ".png")) as image:
                    self.assertEqual(image.mode, "RGBA")
                    self.assertEqual(image.size, tuple(sprite["size"]))
                    with Image.open(ROOT / sprite["default"]) as default:
                        self.assertEqual(image.size, default.size)

    def test_no_sprite_is_fully_transparent(self):
        for sprite in SPRITES:
            with self.subTest(sprite=sprite["name"]):
                with Image.open(ROOT / THEME / "UI" / (sprite["name"] + ".png")) as image:
                    self.assertIsNotNone(image.getchannel("A").getbbox())

    def test_neutral_sprites_remain_tintable(self):
        self.assertTrue(NEUTRAL_NAMES.issubset({sprite["name"] for sprite in SPRITES}))
        for name in sorted(NEUTRAL_NAMES):
            with self.subTest(sprite=name):
                with Image.open(ROOT / THEME / "UI" / (name + ".png")) as image:
                    pixels = np.asarray(image)
                    saturation = np.asarray(image.convert("RGB").convert("HSV"))[..., 1]
                    visible = pixels[..., 3] > 0
                    self.assertTrue(visible.any())
                    self.assertLess(float(saturation[visible].mean()), 8.0)

    def test_unavailable_lock_remains_legible_on_its_dark_host(self):
        # GameApp.Art07.DrawItems07 draws this icon at 22x24 over Background.
        # Test the composited small image, independently of its source geometry.
        background = (21, 22, 24, 255)
        with Image.open(ROOT / THEME / "UI/ui_icon_lock.png") as image:
            icon = image.resize((22, 24), Image.Resampling.LANCZOS)
            rendered = Image.alpha_composite(Image.new("RGBA", icon.size, background), icon)
            rgb = np.asarray(rendered)[..., :3].astype(np.float64) / 255.0

        def luminance(srgb):
            linear = np.where(srgb <= .04045, srgb / 12.92,
                              ((srgb + .055) / 1.055) ** 2.4)
            return linear @ np.array((.2126, .7152, .0722))

        foreground_luminance = luminance(rgb)
        background_luminance = luminance(np.array(background[:3]) / 255.0)
        contrast = ((np.maximum(foreground_luminance, background_luminance) + .05)
                    / (np.minimum(foreground_luminance, background_luminance) + .05))
        for name, half in (("shackle", contrast[:12]), ("body", contrast[12:])):
            with self.subTest(zone=name):
                self.assertGreaterEqual(float((half >= 4.5).mean()), .10,
                                        "The lock must remain visible on its actual dark host.")

    def test_table_scene_size_and_rgb(self):
        with Image.open(ROOT / THEME / "Table/table_scene.png") as image:
            self.assertEqual(image.size, (1920, 1080))
            self.assertEqual(image.mode, "RGB")

    def test_table_play_area_keeps_uninterrupted_paper(self):
        with Image.open(ROOT / THEME / "Table/table_scene.png") as image:
            play_area = np.asarray(image)[330:950, 360:1500, :3]
            paper = np.array((249, 234, 219), dtype=np.uint8)
            np.testing.assert_array_equal(play_area, np.broadcast_to(paper, play_area.shape))

    def test_paper_surfaces_share_bold_black_rules(self):
        widths = []
        for name in PAPER_SURFACES:
            with self.subTest(sprite=name):
                with Image.open(ROOT / THEME / "UI" / (name + ".png")) as image:
                    pixels = np.asarray(image)
                    # Read inward from each side, away from the corner geometry.
                    side_samples = (pixels[image.height // 2],
                                    pixels[image.height // 2, ::-1],
                                    pixels[:, image.width // 2],
                                    pixels[::-1, image.width // 2])
                    for side in side_samples:
                        dark = (side[:, :3].max(axis=1) <= 64) & (side[:, 3] == 255)
                        self.assertTrue(dark[0], "The rule must reach the outer side.")
                        self.assertTrue((~dark).any(), "The paper field is missing.")
                        width = int(np.flatnonzero(~dark)[0])
                        self.assertGreaterEqual(width, 8)
                        self.assertLessEqual(width, 10)
                        widths.append(width)
                        # The inner rule core is opaque near-black, not a grey bevel.
                        self.assertLessEqual(int(side[2:6, :3].max()), 40)
                        np.testing.assert_array_equal(side[2:6, 3], np.full(4, 255))
                    centre = pixels[40:-40, 40:-40]
                    self.assertGreaterEqual(int(centre[..., :3].min()), 180)
                    self.assertTrue(np.all(centre[..., 3] == 255))
        self.assertLessEqual(max(widths) - min(widths), 1,
                             "All six paper surfaces must share one visual rule weight.")

    def test_panel_chamfers_and_dark_fields(self):
        by_name = {sprite["name"]: sprite for sprite in SPRITES}
        for name in PAPER_SURFACES + DARK_SURFACES:
            with self.subTest(sprite=name):
                with Image.open(ROOT / THEME / "UI" / (name + ".png")) as image:
                    pixels = np.asarray(image)
                    alpha = pixels[..., 3]
                    left, _, right, _ = map(int, by_name[name]["border"])
                    cap = min(left, right)
                    for corner in (alpha, alpha[:, ::-1], alpha[::-1], alpha[::-1, ::-1]):
                        self.assertEqual(int(corner[0, 0]), 0,
                                         "Chamfered corner vertices must be transparent.")
                        self.assertEqual(int(corner[0, cap]), 255)
                        self.assertEqual(int(corner[cap, 0]), 255)
                        top_start = int(np.flatnonzero(corner[0] >= 128)[0])
                        self.assertGreaterEqual(top_start, 4)
                        self.assertLessEqual(top_start, cap - 3)
                        # A straight diagonal has constant x+y, unlike a rounded corner.
                        diagonal = [x + int(np.flatnonzero(corner[:, x] >= 128)[0])
                                    for x in range(top_start - 2)]
                        self.assertLessEqual(max(diagonal) - min(diagonal), 2)
                    for point in ((0, image.width // 2), (-1, image.width // 2),
                                  (image.height // 2, 0), (image.height // 2, -1)):
                        self.assertEqual(int(alpha[point]), 255)
                    if name in DARK_SURFACES:
                        centre = pixels[image.height // 2, image.width // 2]
                        self.assertLessEqual(int(centre[:3].max()), 32)
                        self.assertEqual(int(centre[3]), 255)

    def test_die_shadow_is_a_broad_flat_translucent_footprint(self):
        with Image.open(ROOT / THEME / "UI/ui_die_shadow.png") as image:
            pixels = np.asarray(image)
            alpha = pixels[..., 3]
            # 25--35% opacity is alpha 64--89; AA may be fainter at the perimeter.
            plateau = (alpha >= 64) & (alpha <= 89)
            self.assertGreater(float(plateau.mean()), .20)
            self.assertLessEqual(int(alpha.max()), 89)
            self.assertLessEqual(int(pixels[plateau, :3].max()), 40)
            occupied = np.argwhere(alpha > 0)
            self.assertGreater(int(occupied[:, 0].max() - occupied[:, 0].min() + 1), 50)
            # Erode the visible mask beyond the Lanczos support to inspect only its core.
            padded = np.pad(alpha > 0, 6, constant_values=False)
            core = np.ones(alpha.shape, dtype=bool)
            for dy in range(13):
                for dx in range(13):
                    core &= padded[dy:dy + image.height, dx:dx + image.width]
            self.assertGreater(float(core.mean()), .10)
            self.assertEqual(len(np.unique(alpha[core])), 1,
                             "The footprint interior must be flat, with no blurred falloff.")
            self.assertTrue(np.all(plateau[core]))

    def test_paper_tile_size_and_matching_seams(self):
        with Image.open(ROOT / THEME / "Table/felt_tile.png") as image:
            self.assertEqual(image.size, (1024, 1024))
            pixels = np.asarray(image)
            np.testing.assert_array_equal(pixels[0], pixels[-1])
            np.testing.assert_array_equal(pixels[:, 0], pixels[:, -1])

    def test_picker_preview_size(self):
        with Image.open(ROOT / THEME / "preview.png") as image:
            self.assertEqual(image.size, (480, 270))

    def test_nine_slice_centres_are_flat(self):
        checked = 0
        for sprite in SPRITES:
            left, bottom, right, top = map(int, sprite["border"])
            if not all((left, bottom, right, top)):
                continue
            checked += 1
            with self.subTest(sprite=sprite["name"]):
                with Image.open(ROOT / THEME / "UI" / (sprite["name"] + ".png")) as image:
                    pixels = np.asarray(image)
                    # Ignore only the three-pixel Lanczos support at the border boundary.
                    centre = pixels[top + 3:image.height - bottom - 3,
                                    left + 3:image.width - right - 3]
                    self.assertGreater(centre.size, 0)
                    np.testing.assert_array_equal(centre, np.broadcast_to(centre[0, 0], centre.shape))
        self.assertGreater(checked, 0, "No two-axis nine-slice assets were checked.")

    def test_horizontal_slices_do_not_distort_central_art(self):
        checked = 0
        for sprite in SPRITES:
            left, bottom, right, top = map(int, sprite["border"])
            if not (left and right and bottom == 0 and top == 0):
                continue
            checked += 1
            with self.subTest(sprite=sprite["name"]):
                with Image.open(ROOT / THEME / "UI" / (sprite["name"] + ".png")) as image:
                    centre = np.asarray(image)[:, left + 3:image.width - right - 3]
                    self.assertGreater(centre.size, 0)
                    np.testing.assert_array_equal(centre, np.broadcast_to(centre[:, :1], centre.shape))
        self.assertGreater(checked, 0, "No horizontal-only sliced assets were checked.")

    def test_full_build_is_byte_deterministic(self):
        self.assertEqual(self.first_hashes, self.second_hashes)

    def test_shipped_assets_match_regeneration(self):
        for relative, digest in self.first_hashes.items():
            with self.subTest(asset=relative):
                self.assertEqual(hashlib.sha256((ROOT / relative).read_bytes()).hexdigest(), digest)


if __name__ == "__main__":
    unittest.main()
