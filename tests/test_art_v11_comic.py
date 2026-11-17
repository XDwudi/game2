"""Independent asset-contract checks for the W20 comic (爆彩连环漫画) UI kit."""

import colorsys
import hashlib
import importlib.util
import json
import math
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
THEME = Path("DicePrototype/Assets/Resources/Themes/comic")
REVIEW = Path("ArtSource/Art11/comic/contact_sheet.png")
CONTRACT = json.loads((ROOT / "tools/art_v11/theme_contract.json").read_text(encoding="utf-8"))
SPRITES = [sprite for sprite in CONTRACT["sprites"] if sprite["referenced"]]
BY_NAME = {sprite["name"]: sprite for sprite in SPRITES}

# Written from the game's callers, independently of the generator: pieces the code multiplies by palette colours
# (UiKit.Button/FlatButton/Frame/Ring/Progress, Surface11 in GameApp.Play11*.cs, the shop and the NavIcons), and the
# V1.1 W13 card template, tinted by the rarity colours or Ghost (GameApp.Card11.cs TintedSprite13).
NEUTRAL_NAMES = frozenset({
    "ui_button_bevel", "ui_button_ghost", "ui_frame_block", "ui_frame_enchant",
    "ui_ring", "ui_price_chip", "ui_progress_fill", "ui_progress_track",
    "ui_section_header", "ui_pip_dot", "ui_icon_arrow", "ui_icon_book",
    "ui_icon_gear", "fx_arc_short", "fx_score_spark", "fx_square_chip",
    "ui_dice_tray", "ui_topbar", "ui_chip_formula", "ui_stage_card", "ui_icon_refresh",
    "ui_card_frame_rarity", "ui_gem_common", "ui_gem_advanced", "ui_gem_rare",
})
GEMS = ("ui_gem_common", "ui_gem_advanced", "ui_gem_rare")
# Tinted surfaces with an opaque face: the label pick reads centre pixel x tint, so the face must be pure white.
WHITE_FACES = ("ui_topbar", "ui_chip_formula", "ui_stage_card", "ui_button_bevel", "ui_price_chip", "ui_frame_block")
# Open frames: the game sets fillCenter=false or shows dice and cards through them.
HOLLOW = ("ui_ring", "ui_button_ghost", "ui_dice_tray", "ui_frame_enchant", "ui_card_frame_rarity")
# Surfaces drawn beyond their source size (top bar 1888 units, trays 1448, dialogs, shop sections, cards, the compact
# icon block): their edge bands must stay the same all along each edge. A comic surface is flat ink and colour, so this
# holds exactly for the authored ones too; dots and other detail belong to the corner squares.
STRETCHED = ("ui_topbar", "ui_dice_tray", "ui_chip_formula", "ui_stage_card", "ui_button_bevel", "ui_button_ghost",
             "ui_ring", "ui_frame_block", "ui_frame_enchant", "ui_price_chip", "ui_card_frame_rarity",
             "ui_paper_panel", "ui_panel_hud", "ui_section_panel", "ui_tab_bottom", "ui_info_inset", "ui_slot_inventory",
             "ui_chip_label", "ui_card_controller", "ui_card_controller_compact", "ui_card_item", "ui_card_shop")
DARK_TEXT_SURFACES = ("ui_panel_hud", "ui_section_panel", "ui_tab_bottom", "ui_info_inset", "ui_slot_inventory",
                      "ui_tray_front")
PAPER_TEXT_SURFACES = ("ui_paper_panel", "ui_card_controller", "ui_card_controller_compact", "ui_card_item",
                       "ui_card_shop", "ui_chip_label")
# The concept pages' shape language: a hard drop shadow down and to the right under panels, cards, buttons and chips;
# buttons and chips lean (sheared: the top-left and bottom-right corners cut on one slant); panels and frames keep
# square corners like comic panels. Not the default's steps, Swiss's four cuts or sancai's round corners.
SHADOWED = ("ui_topbar", "ui_stage_card", "ui_button_bevel", "ui_chip_formula", "ui_price_chip", "ui_chip_label",
            "ui_paper_panel", "ui_panel_hud", "ui_section_panel", "ui_tab_bottom", "ui_card_controller", "ui_card_shop",
            "ui_card_item", "ui_dice_tray")
SHEARED = ("ui_chip_formula", "ui_button_bevel", "ui_button_ghost", "ui_price_chip", "ui_chip_label")
SQUARE = ("ui_paper_panel", "ui_panel_hud", "ui_section_panel", "ui_tab_bottom", "ui_info_inset", "ui_slot_inventory",
          "ui_topbar", "ui_stage_card", "ui_card_controller", "ui_card_shop", "ui_card_item", "ui_ring", "ui_dice_tray",
          "ui_frame_block", "ui_card_frame_rarity")
FELT_ZONES = {
    "dice_area": (52, 374, 1408, 276),
    "formula_row": (52, 660, 1408, 60),
    "submission_rail": (344, 740, 1136, 202),
    "submit_info_text150": (32, 740, 360, 202),
}


def hex_rgb(value):
    value = value.lstrip("#")
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def channel(c):
    c = np.asarray(c, dtype=np.float64) / 255.0
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def luminance(rgb):
    lin = channel(rgb)
    return lin[..., 0] * .2126 + lin[..., 1] * .7152 + lin[..., 2] * .0722


def contrast(a, b):
    x, y = luminance(np.asarray(a)), luminance(np.asarray(b))
    return (np.maximum(x, y) + .05) / (np.minimum(x, y) + .05)


def lab(rgb):
    r, g, b = channel(rgb)
    x = (.4124 * r + .3576 * g + .1805 * b) / .95047
    y = .2126 * r + .7152 * g + .0722 * b
    z = (.0193 * r + .1192 * g + .9505 * b) / 1.08883
    f = lambda t: t ** (1 / 3) if t > 216 / 24389 else (24389 / 27 * t + 16) / 116
    return np.array((116 * f(y) - 16, 500 * (f(x) - f(y)), 200 * (f(y) - f(z))))


def ciede2000(lab1, lab2):
    """CIEDE2000 (Sharma, Wu and Dalal 2005), written out step by step from the paper's equations."""
    (l1, a1, b1), (l2, a2, b2) = (tuple(float(x) for x in v) for v in (lab1, lab2))
    c_mean = (math.hypot(a1, b1) + math.hypot(a2, b2)) / 2
    g = 0.5 * (1 - math.sqrt(c_mean ** 7 / (c_mean ** 7 + 25.0 ** 7)))
    a1p, a2p = (1 + g) * a1, (1 + g) * a2
    c1p, c2p = math.hypot(a1p, b1), math.hypot(a2p, b2)
    h1p = 0.0 if a1p == 0 and b1 == 0 else math.degrees(math.atan2(b1, a1p)) % 360
    h2p = 0.0 if a2p == 0 and b2 == 0 else math.degrees(math.atan2(b2, a2p)) % 360
    delta_l = l2 - l1
    delta_c = c2p - c1p
    if c1p * c2p == 0:
        delta_h = 0.0
    elif abs(h2p - h1p) <= 180:
        delta_h = h2p - h1p
    elif h2p - h1p > 180:
        delta_h = h2p - h1p - 360
    else:
        delta_h = h2p - h1p + 360
    delta_hh = 2 * math.sqrt(c1p * c2p) * math.sin(math.radians(delta_h) / 2)
    l_mean = (l1 + l2) / 2
    cp_mean = (c1p + c2p) / 2
    if c1p * c2p == 0:
        hp_mean = h1p + h2p
    elif abs(h1p - h2p) <= 180:
        hp_mean = (h1p + h2p) / 2
    elif h1p + h2p < 360:
        hp_mean = (h1p + h2p + 360) / 2
    else:
        hp_mean = (h1p + h2p - 360) / 2
    t = (1 - 0.17 * math.cos(math.radians(hp_mean - 30)) + 0.24 * math.cos(math.radians(2 * hp_mean))
         + 0.32 * math.cos(math.radians(3 * hp_mean + 6)) - 0.20 * math.cos(math.radians(4 * hp_mean - 63)))
    delta_theta = 30 * math.exp(-((hp_mean - 275) / 25) ** 2)
    r_c = 2 * math.sqrt(cp_mean ** 7 / (cp_mean ** 7 + 25.0 ** 7))
    s_l = 1 + 0.015 * (l_mean - 50) ** 2 / math.sqrt(20 + (l_mean - 50) ** 2)
    s_c = 1 + 0.045 * cp_mean
    s_h = 1 + 0.015 * cp_mean * t
    r_t = -math.sin(math.radians(2 * delta_theta)) * r_c
    return math.sqrt((delta_l / s_l) ** 2 + (delta_c / s_c) ** 2 + (delta_hh / s_h) ** 2
                     + r_t * (delta_c / s_c) * (delta_hh / s_h))


def tinted(face, tint):
    """A face pixel times a tint, in linear light (the project's colour space), back to 8-bit sRGB."""
    linear = channel(face) * channel(tint)
    srgb = np.where(linear <= .0031308, linear * 12.92, 1.055 * np.power(linear, 1 / 2.4) - .055)
    return np.rint(srgb * 255)


def png_hashes(folder):
    return {path.relative_to(folder).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in folder.rglob("*.png")}


def load(name):
    with Image.open(ROOT / THEME / "UI" / (name + ".png")) as image:
        return np.asarray(image.convert("RGBA"))


def frame_insets(pixels, border):
    """Mirror of GameApp.FrameInsets11: along the middle row and column, within the slice border, the last pixel that
    is transparent or differs from the centre fill by more than 48 (sum of channel differences). Unity rows run up."""
    up = pixels[::-1].astype(np.int32)
    h, w = up.shape[:2]
    fill = up[h // 2, w // 2]
    left, bottom, right, top = border

    def inset(get, count, limit):
        last = 0
        for i in range(min(count // 2, int(round(limit)))):
            c = get(i)
            if c[3] < 128 or int(np.abs(c[:3] - fill[:3]).sum()) > 48:
                last = i + 1
        return last
    return (inset(lambda i: up[h // 2, i], w, left), inset(lambda i: up[i, w // 2], h, bottom),
            inset(lambda i: up[h // 2, w - 1 - i], w, right), inset(lambda i: up[h - 1 - i, w // 2], h, top))


class ComicArtTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        spec = importlib.util.spec_from_file_location(
            "comic_asset_builder", ROOT / "tools/art_v11/comic/build_comic.py")
        cls.builder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.builder)
        scratch_parent = ROOT / "ArtSource/Art11/comic"
        scratch_parent.mkdir(parents=True, exist_ok=True)
        cls.scratch = tempfile.TemporaryDirectory(prefix="test-rebuild-", dir=scratch_parent)
        cls.addClassCleanup(cls.scratch.cleanup)
        cls.first = Path(cls.scratch.name) / "first"
        cls.second = Path(cls.scratch.name) / "second"
        cls.builder.build(cls.first)
        cls.builder.build(cls.second)
        cls.first_hashes = png_hashes(cls.first)
        cls.second_hashes = png_hashes(cls.second)
        cls.theme = json.loads((ROOT / THEME / "theme.json").read_text(encoding="utf-8"))
        cls.palette = cls.theme["palette"]

    # ------------------------------------------------------------------ inventory and format
    def test_exact_referenced_inventory(self):
        self.assertEqual(len(SPRITES), 53, "Review W20 scope if the input contract changes.")
        expected = {sprite["name"] for sprite in SPRITES}
        self.assertEqual({path.stem for path in (ROOT / THEME / "UI").glob("*.png")}, expected)
        expected_paths = {(THEME / "UI" / (name + ".png")).as_posix() for name in expected}
        expected_paths.update({(THEME / "Table/table_scene.png").as_posix(),
                               (THEME / "Table/felt_tile.png").as_posix(),
                               (THEME / "preview.png").as_posix(), REVIEW.as_posix()})
        self.assertEqual(set(self.first_hashes), expected_paths)
        self.assertEqual(len(self.first_hashes), 57)
        self.assertTrue((self.first / THEME / "theme.json").is_file())

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
                self.assertTrue((load(sprite["name"])[..., 3] > 0).any())

    # ------------------------------------------------------------------ tint conventions
    def test_tinted_sprites_are_exactly_achromatic(self):
        self.assertTrue(NEUTRAL_NAMES.issubset(BY_NAME))
        for name in sorted(NEUTRAL_NAMES):
            with self.subTest(sprite=name):
                pixels = load(name).astype(np.int16)
                visible = pixels[..., 3] > 0
                self.assertTrue(visible.any())
                spread = np.abs(pixels[..., 0] - pixels[..., 1]) + np.abs(pixels[..., 1] - pixels[..., 2])
                self.assertEqual(int(spread[visible].max()), 0, "A tinted piece must carry no hue of its own.")

    def test_tinted_faces_are_pure_white(self):
        for name in WHITE_FACES:
            with self.subTest(sprite=name):
                pixels = load(name)
                h, w = pixels.shape[:2]
                self.assertEqual(tuple(int(v) for v in pixels[h // 2, w // 2]), (255, 255, 255, 255))

    def test_open_frames_have_transparent_centres(self):
        for name in HOLLOW:
            with self.subTest(sprite=name):
                pixels = load(name)
                left, bottom, right, top = map(int, BY_NAME[name]["border"])
                centre = pixels[top:pixels.shape[0] - bottom, left:pixels.shape[1] - right, 3]
                self.assertEqual(int(centre.max()), 0)

    # ------------------------------------------------------------------ nine-slice behaviour
    def test_nine_slice_centres_are_flat_from_the_border_lines(self):
        checked = 0
        for sprite in SPRITES:
            left, bottom, right, top = map(int, sprite["border"])
            if not all((left, bottom, right, top)):
                continue
            checked += 1
            with self.subTest(sprite=sprite["name"]):
                pixels = load(sprite["name"])
                centre = pixels[top:pixels.shape[0] - bottom, left:pixels.shape[1] - right]
                self.assertGreater(centre.size, 0)
                np.testing.assert_array_equal(centre, np.broadcast_to(centre[0, 0], centre.shape))
        self.assertEqual(checked, 22)

    def test_horizontal_slices_keep_central_columns_identical(self):
        checked = 0
        for sprite in SPRITES:
            left, bottom, right, top = map(int, sprite["border"])
            if not (left and right and bottom == 0 and top == 0):
                continue
            checked += 1
            with self.subTest(sprite=sprite["name"]):
                centre = load(sprite["name"])[:, left:-right]
                np.testing.assert_array_equal(centre, np.broadcast_to(centre[:, :1], centre.shape))
        self.assertEqual(checked, 4)

    def test_stretched_surfaces_have_edge_bands_invariant_along_the_edge(self):
        for name in STRETCHED:
            pixels = load(name).astype(np.int16)
            h, w = pixels.shape[:2]
            left, bottom, right, top = map(int, BY_NAME[name]["border"])
            # Along-edge sequences: rows of the top and bottom bands, columns of the left and right bands, between the
            # corner squares (the part Unity stretches).
            bands = [("top", pixels[:top, left:w - right]), ("bottom", pixels[h - bottom:, left:w - right]),
                     ("left", pixels[top:h - bottom, :left].transpose(1, 0, 2)),
                     ("right", pixels[top:h - bottom, w - right:].transpose(1, 0, 2))]
            for side, band in bands:
                with self.subTest(sprite=name, side=side):
                    np.testing.assert_array_equal(band, np.broadcast_to(band[:, :1], band.shape))

    def test_frames_stay_in_their_bands_and_leave_text_room(self):
        limits = {  # max inset per side in source px (PPU 200: 2 px = 1 unit) where text sits inside the frame
            "ui_chip_formula": 14, "ui_price_chip": 16, "ui_chip_label": 14, "ui_topbar": 20,
            "ui_card_controller": 30, "ui_card_controller_compact": 30, "ui_card_item": 30, "ui_card_shop": 30,
            "ui_panel_hud": 24, "ui_tab_bottom": 24, "ui_paper_panel": 24, "ui_stage_card": 28, "ui_info_inset": 12,
            "ui_slot_inventory": 12, "ui_button_bevel": 20,
        }
        for sprite in SPRITES:
            name = sprite["name"]
            if not any(sprite["border"]) or name in HOLLOW:
                continue
            with self.subTest(sprite=name):
                insets = frame_insets(load(name), sprite["border"])
                for value, limit in zip(insets, sprite["border"]):
                    self.assertLessEqual(value, limit)
                if name in limits:
                    self.assertLessEqual(max(insets), limits[name])
                    self.assertGreaterEqual(min(insets), 1, "The surface needs a visible frame.")
        # Shop sections draw their header strip 5 units (10 px) inside the panel's top edge.
        self.assertLessEqual(frame_insets(load("ui_section_panel"), BY_NAME["ui_section_panel"]["border"])[3], 10)
        # A 34-unit tab keeps at least 20 units of height for its one-line label.
        chip = frame_insets(load("ui_chip_formula"), BY_NAME["ui_chip_formula"]["border"])
        self.assertLessEqual((chip[1] + chip[3]) / 2, 14)

    # ------------------------------------------------------------------ the comic shapes
    def test_panels_cards_and_buttons_cast_a_hard_shadow_down_and_right(self):
        for name in SHADOWED:
            with self.subTest(sprite=name):
                pixels = load(name)
                alpha, grey = pixels[..., 3], pixels[..., :3].astype(int).max(axis=2)
                h, w = alpha.shape
                # Along the right and bottom edges the silhouette starts later than along the left and top: the
                # shadow is the body moved down and right, so its top-right and bottom-left corners are open.
                right = np.flatnonzero(alpha[:, w - 2] >= 128)
                bottom = np.flatnonzero(alpha[h - 2] >= 128)
                self.assertGreaterEqual(int(right[0]), 4, "The shadow must start below the top edge.")
                self.assertGreaterEqual(int(bottom[0]), 4, "The shadow must start right of the left edge.")
                self.assertLessEqual(int(np.flatnonzero(alpha[h // 2] >= 128)[0]), 2, "No shadow on the left.")
                self.assertLessEqual(int(np.flatnonzero(alpha[:, w // 2] >= 128)[0]), 2, "No shadow on top.")
                # It is a hard, dark shadow: solid ink just inside the outer edge, on the right and at the bottom.
                for y, x in ((h // 2, w - 4), (h - 4, w // 2)):
                    self.assertGreaterEqual(int(alpha[y, x]), 250)
                    self.assertLessEqual(int(grey[y, x]), 40)

    def test_buttons_and_chips_lean_like_comic_captions(self):
        # Sheared: the top-left and bottom-right corners are cut on one straight slant; the other two stay square.
        for name in SHEARED:
            with self.subTest(sprite=name):
                alpha = load(name)[..., 3]
                h, w = alpha.shape
                first = [int(np.flatnonzero(alpha[y] >= 128)[0]) for y in range(2, 22)]
                self.assertGreaterEqual(first[0], 8, "The top-left corner is cut.")
                slope, offset = np.polyfit(np.arange(2, 22), first, 1)
                self.assertLess(slope, -.2, "The cut leans like '/'.")
                self.assertLessEqual(float(np.abs(np.polyval([slope, offset], np.arange(2, 22)) - first).max()), 1.0,
                                     "The cut is one straight slant.")
                self.assertLessEqual(int(np.flatnonzero(alpha[h // 2] >= 128)[0]), 2)
                # Top-right and bottom-left stay square: the silhouette reaches into those corners.
                columns = np.flatnonzero(alpha.max(axis=0) >= 128)
                rows = np.flatnonzero(alpha.max(axis=1) >= 128)
                self.assertGreaterEqual(int(alpha[2, w // 2:].nonzero()[0].max()) + w // 2, int(columns.max()) - 10)
                self.assertGreaterEqual(int(alpha[h // 2:, 2].nonzero()[0].max()) + h // 2, int(rows.max()) - 10)
                # The bottom-right corner is cut too: near the right edge the silhouette ends higher than in the middle.
                bottom_row = [int(np.flatnonzero(alpha[:, x] >= 128)[-1]) for x in (w // 2, w - 8)]
                self.assertLess(bottom_row[1], bottom_row[0] - 4)

    def test_panels_and_frames_keep_square_corners(self):
        for name in SQUARE:
            with self.subTest(sprite=name):
                alpha = load(name)[..., 3]
                self.assertEqual(int(alpha[2, 2]), 255, "A comic panel corner is square, not cut, stepped or round.")

    # ------------------------------------------------------------------ text on surfaces
    def test_surfaces_that_carry_text_meet_the_contrast_floor(self):
        p = self.palette
        for name in DARK_TEXT_SURFACES:
            with self.subTest(sprite=name):
                pixels = load(name)
                fill = pixels[pixels.shape[0] // 2, pixels.shape[1] // 2, :3]
                self.assertGreaterEqual(float(contrast(fill, hex_rgb(p["textColor"]))), 4.5)
                self.assertGreaterEqual(float(contrast(fill, hex_rgb(p["muted"]))), 3.0)
        for name in PAPER_TEXT_SURFACES:
            with self.subTest(sprite=name):
                pixels = load(name)
                fill = pixels[pixels.shape[0] // 2, pixels.shape[1] // 2, :3]
                self.assertGreaterEqual(float(contrast(fill, hex_rgb(p["ink"]))), 4.5)

    # ------------------------------------------------------------------ small marks on their real hosts
    def _composite(self, name, size, host):
        with Image.open(ROOT / THEME / "UI" / (name + ".png")) as image:
            icon = image.convert("RGBA").resize(size, Image.Resampling.LANCZOS)
        rendered = Image.alpha_composite(Image.new("RGBA", size, tuple(host) + (255,)), icon)
        return np.asarray(rendered)[..., :3]

    def test_unavailable_lock_reads_on_its_dark_host(self):
        # GameApp.Play11.DrawItems11 draws this icon at 20x22 over a Background-filled frame (22x24 in the old layout).
        background = hex_rgb(self.palette["background"])
        for size in ((20, 22), (22, 24)):
            rgb = self._composite("ui_icon_lock", size, background)
            ratio = contrast(rgb, background)
            half = size[1] // 2
            for zone, part in (("shackle", ratio[:half]), ("body", ratio[half:])):
                with self.subTest(size=size, zone=zone):
                    self.assertGreaterEqual(float((part >= 4.5).mean()), .10)

    def test_small_marks_stand_out_on_their_hosts(self):
        p = self.palette
        lerp = lambda a, b, t: tuple(round(x + (y - x) * t) for x, y in zip(hex_rgb(a), hex_rgb(b)))
        cases = (  # sprite, display size, host colour as the game draws it
            ("ui_icon_coin", (26, 26), hex_rgb(p["gold"])), ("ui_icon_coin", (36, 36), hex_rgb(p["panel"])),
            ("ui_icon_roll", (36, 36), tuple(load("ui_info_inset")[256, 256, :3])),
            ("ui_icon_dice_view", (40, 40), lerp(p["background"], p["panel"], .55)),
            ("ui_tag_lock", (28, 30), hex_rgb(p["felt"])), ("fx_score_crown", (48, 32), hex_rgb(p["panel"])),
            ("badge_enchant_base10", (28, 28), hex_rgb(p["paper"])), ("badge_enchant_mult2", (28, 28), hex_rgb(p["paper"])),
            ("badge_enchant_roll1", (28, 28), hex_rgb(p["paper"])), ("badge_enchant_slotless", (28, 28), hex_rgb(p["paper"])),
            ("badge_enchant_submit1", (28, 28), hex_rgb(p["paper"])),
        )
        for name, size, host in cases:
            with self.subTest(sprite=name, size=size):
                ratio = contrast(self._composite(name, size, host), host)
                self.assertGreaterEqual(float((ratio >= 3.0).mean()), .15)

    # ------------------------------------------------------------------ card template (W13, CARD_TEMPLATE 3 and 6)
    @staticmethod
    def _ring_insets(pixels, border):
        """Mirror of GameApp.Card11 RingInsets13: along the middle row and column, within the slice border, the last pixel
        whose alpha is at least 128. Unity rows run up."""
        up = pixels[::-1]
        h, w = up.shape[:2]
        left, bottom, right, top = border

        def inset(get, count, limit):
            last = 0
            for i in range(min(count // 2, int(round(limit)))):
                if get(i)[3] >= 128:
                    last = i + 1
            return last
        return (inset(lambda i: up[h // 2, i], w, left), inset(lambda i: up[i, w // 2], h, bottom),
                inset(lambda i: up[h // 2, w - 1 - i], w, right), inset(lambda i: up[h - 1 - i, w // 2], h, top))

    @staticmethod
    def _erode(mask, n):
        out = mask.copy()
        for _ in range(n):
            p = np.pad(out, 1, constant_values=False)
            out = out & p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:]
        return out

    def test_rarity_ring_is_a_slim_band_with_an_ink_edge(self):
        # DrawCard13 lays the name, picture and short text inside the ring's band (RingInsets13): a band no wider than the
        # default ring keeps the card's content box as the default style has it.
        border = BY_NAME["ui_card_frame_rarity"]["border"]
        ours = self._ring_insets(load("ui_card_frame_rarity"), border)
        with Image.open(ROOT / BY_NAME["ui_card_frame_rarity"]["default"]) as image:
            theirs = self._ring_insets(np.asarray(image.convert("RGBA")), border)
        for side, (a, b) in enumerate(zip(ours, theirs)):
            with self.subTest(side=side):
                self.assertGreaterEqual(a, 8, "The ring must stay visible.")
                self.assertLessEqual(a, b)
        # Across the band (middle of the top edge): an ink edge, and a band that shows the tint itself.
        across = load("ui_card_frame_rarity")[:ours[3] + 3, 256].astype(int)
        solid = across[across[:, 3] >= 250, 0]
        self.assertLessEqual(int(solid.min()), 40)
        self.assertGreaterEqual(int(solid.max()), 240)

    def test_rarity_ring_shows_on_every_card_face(self):
        # The card face's own frame must not swallow the ring: on each card face the drawn frame band (the colour just
        # outside the ring) differs clearly from every rarity colour and from Ghost (the goods' ring).
        p = self.palette
        for face in ("ui_card_controller", "ui_card_shop"):
            pixels = load(face)
            insets = frame_insets(pixels, BY_NAME[face]["border"])
            # The frame band's colour: the most common opaque colour along the middle row inside the outline.
            row = pixels[256, 6:insets[0]]
            colours, counts = np.unique(row[row[:, 3] == 255, :3], axis=0, return_counts=True)
            band = colours[counts.argmax()]
            for key in ("rarityCommon", "rarityAdvanced", "rarityRare", "ghost"):
                with self.subTest(face=face, ring=key):
                    self.assertGreaterEqual(ciede2000(lab(band), lab(hex_rgb(p[key]))), 20.0)

    @staticmethod
    def _silhouette_radii(alpha):
        """Outermost radius of the alpha >= 128 silhouette along 720 rays from the image centre."""
        h, w = alpha.shape
        cy, cx = (h - 1) / 2, (w - 1) / 2
        steps = np.arange(0, min(h, w) / 2, .25)
        radii = []
        for k in range(720):
            a = math.pi * k / 360
            xs = np.clip(np.rint(cx + steps * math.cos(a)).astype(int), 0, w - 1)
            ys = np.clip(np.rint(cy + steps * math.sin(a)).astype(int), 0, h - 1)
            hit = np.flatnonzero(alpha[ys, xs] >= 128)
            radii.append(steps[hit[-1]] if hit.size else 0.0)
        return np.array(radii)

    def test_rarity_gems_are_round_diamond_and_six_point_star(self):
        # CARD_TEMPLATE 3: the shape tells the rarity apart without colour. The points of a silhouette are the runs of its
        # radius (seen from the centre) above the middle of the radius range.
        for name, points, depth in (("ui_gem_common", 0, 0.0), ("ui_gem_advanced", 4, .2), ("ui_gem_rare", 6, .3)):
            with self.subTest(sprite=name):
                radii = self._silhouette_radii(load(name)[..., 3])
                spread = float((radii.max() - radii.min()) / radii.max())
                if points == 0:
                    self.assertLess(spread, .06, "The common gem is round.")
                    continue
                self.assertGreaterEqual(spread, depth, "The points must stand well clear of the notches.")
                above = radii > (radii.max() + radii.min()) / 2
                self.assertEqual(int(np.sum(above & ~np.roll(above, 1))), points)

    def test_rarity_gems_stay_apart_at_their_smallest_sizes(self):
        # A compact card draws the gem 18 units wide (12 px at 720p). The theme's UI sprites have no mipmaps, so sample
        # each silhouette at the pixel centres; any two of the three masks must still differ clearly.
        for size in (18, 12):
            masks = {}
            for name in GEMS:
                alpha = load(name)[..., 3]
                index = ((np.arange(size) + .5) * alpha.shape[0] / size).astype(int)
                masks[name] = alpha[np.ix_(index, index)] >= 128
            for i, a in enumerate(GEMS):
                for b in GEMS[i + 1:]:
                    with self.subTest(size=size, pair=(a, b)):
                        union = int((masks[a] | masks[b]).sum())
                        self.assertGreaterEqual(int((masks[a] ^ masks[b]).sum()) / union, .2)

    def test_rarity_gems_have_an_ink_line_round_a_light_face(self):
        # CARD_TEMPLATE 6: light, shaded gems with an ink outline. Most of the face stays light so the rarity tint reads as
        # its own colour; the lightest part is the tint itself.
        for name in GEMS:
            with self.subTest(sprite=name):
                pixels = load(name)
                grey = pixels[..., 0].astype(int)
                solid = pixels[..., 3] >= 250
                rim = solid & ~self._erode(solid, 3)
                core = self._erode(solid, 12)
                self.assertTrue(core.any())
                self.assertLessEqual(float(grey[rim].mean()), 100)
                self.assertGreaterEqual(int(grey[solid].max()), 245)
                self.assertGreaterEqual(float(grey[core].mean()), 170)

    # ------------------------------------------------------------------ table, tile, preview
    def test_table_scene_and_preview_sizes(self):
        with Image.open(ROOT / THEME / "Table/table_scene.png") as image:
            self.assertEqual((image.size, image.mode), ((1920, 1080), "RGB"))
        with Image.open(ROOT / THEME / "preview.png") as image:
            self.assertEqual(image.size, (480, 270))

    def test_calm_zones_are_exactly_the_felt_colour(self):
        felt = np.array(hex_rgb(self.palette["felt"]), dtype=np.uint8)
        with Image.open(ROOT / THEME / "Table/table_scene.png") as image:
            pixels = np.asarray(image)
        for name, (x, y, w, h) in FELT_ZONES.items():
            with self.subTest(zone=name):
                zone = pixels[y:y + h, x:x + w]
                np.testing.assert_array_equal(zone, np.broadcast_to(felt, zone.shape))
        # The comic page border lives in the outer margin: the edges are decorated, the field is plain felt.
        edge = np.concatenate([pixels[:, :12].reshape(-1, 3), pixels[:, -12:].reshape(-1, 3)])
        self.assertGreater(float((np.abs(edge.astype(int) - felt).sum(axis=1) > 30).mean()), .5)

    def test_felt_tile_is_seamless_and_even(self):
        with Image.open(ROOT / THEME / "Table/felt_tile.png") as image:
            self.assertEqual((image.size, image.mode), ((1024, 1024), "RGB"))
            px = np.asarray(image).astype(np.float64)
        felt = np.array(hex_rgb(self.palette["felt"]), dtype=np.float64)
        self.assertLessEqual(float(np.abs(px.reshape(-1, 3).mean(axis=0) - felt).max()), 2.0)
        inner_rows = np.abs(np.diff(px, axis=0)).mean()
        inner_cols = np.abs(np.diff(px, axis=1)).mean()
        self.assertLessEqual(float(np.abs(px[0] - px[-1]).mean()), inner_rows * 1.25)
        self.assertLessEqual(float(np.abs(px[:, 0] - px[:, -1]).mean()), inner_cols * 1.25)
        quads = [px[:512, :512], px[:512, 512:], px[512:, :512], px[512:, 512:]]
        means = np.array([q.mean() for q in quads])
        self.assertLessEqual(float(means.max() - means.min()), 1.5)

    # ------------------------------------------------------------------ palette and theme.json
    def test_palette_meets_the_theme_rules(self):
        p = {k: hex_rgb(v) for k, v in self.palette.items()}
        self.assertGreaterEqual(float(contrast(p["textColor"], p["panel"])), 4.5)
        self.assertGreaterEqual(float(contrast(p["ink"], p["paper"])), 4.5)
        self.assertGreaterEqual(float(contrast(p["muted"], p["panel"])), 3.0)
        self.assertGreaterEqual(float(contrast(p["onFelt"], p["felt"])), 4.5)
        # Legacy pages still write TextColor straight on the table (THEME_SPEC 5).
        self.assertGreaterEqual(float(contrast(p["textColor"], p["felt"])), 4.5)
        # The dice pip colours are a functional cue, the same in every style (THEME_SPEC 2).
        self.assertEqual((self.palette["red"], self.palette["blue"]), ("EC655D", "55B6D9"))
        hue, sat, val = colorsys.rgb_to_hsv(*(v / 255 for v in p["pressure"]))
        self.assertGreaterEqual(sat, .5, "Pressure must be a warning colour, not a neutral.")
        self.assertTrue(hue * 360 <= 40 or hue * 360 >= 340, "Pressure must sit in the red-orange warning range.")
        rarity = [p["rarityCommon"], p["rarityAdvanced"], p["rarityRare"]]
        for i in range(3):
            for j in range(i + 1, 3):
                with self.subTest(pair=(i, j)):
                    self.assertGreaterEqual(float(contrast(rarity[i], rarity[j])), 1.4, "Rarity must differ in lightness.")
        # The one main button must not read as a rarity or as the pressure cue.
        for i, colour in enumerate(rarity):
            with self.subTest(rarity=i):
                self.assertGreaterEqual(float(np.linalg.norm(lab(p["primary"]) - lab(colour))), 40.0)
                self.assertGreaterEqual(ciede2000(lab(p["primary"]), lab(colour)), 20.0)
        self.assertGreaterEqual(ciede2000(lab(p["primary"]), lab(p["pressure"])), 20.0)
        # The primary button's face is white tinted by primary; LabelOn11 picks the best of ink, paper and textColor and
        # must reach 4.95:1 without darkening the face (THEME_SPEC 2).
        self.assertGreaterEqual(max(float(contrast(p[k], p["primary"])) for k in ("ink", "paper", "textColor")), 4.95)

    def test_value_and_warning_badges_stay_apart(self):
        # THEME_SPEC 2 valueBadge: a card's value badge (valueBadge, unset means gold) and its warning badge (danger) are
        # told apart at a glance. Drawn as ValueBadge13 draws them, on the ui_chip_label face: CIEDE2000 at least 20 and
        # luminance contrast at least 1.5:1; each takes a label that reaches 4.95:1 as drawn.
        self.assertAlmostEqual(ciede2000((50, 2.6772, -79.7751), (50, 0, -82.7485)), 2.0425, places=4)
        self.assertAlmostEqual(ciede2000((50, -1.3802, -84.2814), (50, 0, -82.7485)), 1.0, places=4)
        p = {k: np.array(hex_rgb(v), dtype=np.float64) for k, v in self.palette.items()}
        face = load("ui_chip_label")[256, 256, :3]
        value, warning = tinted(face, p.get("valueBadge", p["gold"])), tinted(face, p["danger"])
        for name, fill in (("value", value), ("warning", warning)):
            with self.subTest(badge=name):
                self.assertGreaterEqual(max(float(contrast(p[k], fill)) for k in ("ink", "paper", "textColor")), 4.95)
        self.assertGreaterEqual(ciede2000(lab(value), lab(warning)), 20.0)
        self.assertGreaterEqual(float(contrast(value, warning)), 1.5)

    def test_theme_json_schema(self):
        self.assertEqual(set(self.theme), {"id", "displayName", "description", "order", "preview", "palette"})
        self.assertEqual(self.theme["id"], "comic")
        self.assertEqual(self.theme["displayName"], "爆彩连环漫画")
        self.assertEqual(self.theme["order"], 3)
        self.assertIs(self.theme["preview"], True)
        self.assertIn("ArtSource/StyleKit/themes/comic/concepts", self.theme["description"])
        source = (ROOT / "DicePrototype/Assets/Game/UI/Theme/ThemePalette.cs").read_text(encoding="utf-8")
        declared = re.search(r"class Hexes\s*\{\s*public string ([^;]+);", source).group(1)
        keys = {key.strip() for key in declared.replace("\n", " ").split(",")}
        # valueBadge (W13) may be left out: unset, the value badge takes gold (THEME_SPEC 2).
        optional = {"valueBadge"}
        self.assertEqual(len(keys - optional), 24)
        self.assertEqual(set(self.palette) | (keys & optional), keys)
        for key, value in self.palette.items():
            with self.subTest(token=key):
                self.assertRegex(value, r"^[0-9A-F]{6}([0-9A-F]{2})?$")

    def test_shipped_theme_json_matches_regeneration(self):
        self.assertEqual((ROOT / THEME / "theme.json").read_bytes(), (self.first / THEME / "theme.json").read_bytes())

    def test_concept_pages_match_their_recorded_sources(self):
        table = (ROOT / "ArtSource/StyleKit/themes/README.md").read_text(encoding="utf-8")
        recorded = dict(re.findall(r"\| comic \| [^|]+ \| concepts/(\w+)\.png \| [^|]+ \| ([0-9a-f]{64}) \|", table))
        self.assertEqual(set(recorded), {"roll", "shop", "select", "card", "triptych"})
        for page, digest in recorded.items():
            with self.subTest(page=page):
                path = ROOT / "ArtSource/StyleKit/themes/comic/concepts" / (page + ".png")
                self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), digest)

    # ------------------------------------------------------------------ determinism
    def test_full_build_is_byte_deterministic(self):
        self.assertEqual(self.first_hashes, self.second_hashes)

    def test_shipped_assets_match_regeneration(self):
        for relative, digest in self.first_hashes.items():
            with self.subTest(asset=relative):
                self.assertEqual(hashlib.sha256((ROOT / relative).read_bytes()).hexdigest(), digest)


if __name__ == "__main__":
    unittest.main()
