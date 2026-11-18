"""Independent asset-contract checks for the W20 deco (黑金装饰艺术) UI kit."""

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
THEME = Path("DicePrototype/Assets/Resources/Themes/deco")
REVIEW = Path("ArtSource/Art11/deco/contact_sheet.png")
CONTRACT = json.loads((ROOT / "tools/art_v11/theme_contract.json").read_text(encoding="utf-8"))
# Built after the contract was regenerated on 2026-11-18: only the referenced sprites ship (W14 decision 4).
SPRITES = [sprite for sprite in CONTRACT["sprites"] if sprite["referenced"]]
BY_NAME = {sprite["name"]: sprite for sprite in SPRITES}

# Written from the game's callers, independently of the generator: pieces the code multiplies by palette colours
# (UiKit.Button/FlatButton/Frame/Ring/Progress, Surface11 in GameApp.Play11*.cs, the shop and the NavIcons), and the
# V1.1 W13 card template, tinted by the rarity colours or Ghost (GameApp.Card11.cs TintedSprite13).
NEUTRAL_NAMES = frozenset({
    "ui_button_bevel", "ui_button_ghost", "ui_frame_block",
    "ui_ring", "ui_progress_fill", "ui_progress_track",
    "ui_section_header", "ui_pip_dot", "ui_icon_arrow", "ui_icon_book",
    "ui_icon_gear", "fx_arc_short", "fx_score_spark", "fx_square_chip",
    "ui_dice_tray", "ui_topbar", "ui_chip_formula", "ui_stage_card", "ui_icon_refresh",
    "ui_card_frame_rarity", "ui_gem_common", "ui_gem_advanced", "ui_gem_rare",
})
GEMS = ("ui_gem_common", "ui_gem_advanced", "ui_gem_rare")
# Tinted surfaces with an opaque face: the label pick reads centre pixel x tint, so the face must be pure white.
WHITE_FACES = ("ui_topbar", "ui_chip_formula", "ui_stage_card", "ui_button_bevel", "ui_frame_block")
# Open frames: the game sets fillCenter=false or shows dice and cards through them.
HOLLOW = ("ui_ring", "ui_button_ghost", "ui_dice_tray", "ui_card_frame_rarity")
# Surfaces drawn far beyond their source size (top bar 1888 units, trays 1448, dialogs, shop sections, cards). Tinted
# ones stay exactly the same along each edge; authored ones may carry soft runs of at most 6 levels a pixel (brief rule 2),
# and texture or ornament only in the corner squares.
STRETCHED_TINT = ("ui_topbar", "ui_dice_tray", "ui_chip_formula", "ui_stage_card", "ui_button_bevel",
                  "ui_button_ghost", "ui_ring", "ui_frame_block", "ui_card_frame_rarity")
STRETCHED_AUTHORED = ("ui_paper_panel", "ui_panel_hud", "ui_section_panel", "ui_tab_bottom", "ui_info_inset",
                      "ui_slot_inventory", "ui_chip_label", "ui_card_controller", "ui_card_controller_compact",
                      "ui_card_item", "ui_card_shop")
DARK_TEXT_SURFACES = ("ui_panel_hud", "ui_section_panel", "ui_tab_bottom", "ui_info_inset", "ui_slot_inventory",
                      "ui_tray_front")
PAPER_TEXT_SURFACES = ("ui_paper_panel", "ui_card_controller", "ui_card_controller_compact", "ui_card_item",
                       "ui_card_shop", "ui_chip_label")
# The concept pages' shape language (ArtSource/StyleKit/themes/deco/concepts): every plate, card, chip, button, ring and
# tray has 45-degree chamfered corners (the default's stepped corners, sancai's arcs and Swiss's square ends do not).
CHAMFERED = ("ui_paper_panel", "ui_panel_hud", "ui_section_panel", "ui_tab_bottom", "ui_info_inset", "ui_slot_inventory",
             "ui_topbar", "ui_stage_card", "ui_card_controller", "ui_card_controller_compact", "ui_card_shop",
             "ui_card_item", "ui_ring", "ui_dice_tray", "ui_frame_block", "ui_card_frame_rarity", "ui_chip_formula",
             "ui_button_bevel", "ui_button_ghost", "ui_chip_label")
# Black lacquer and ivory plates in a gold moulding (roll.png HUD plates, card.png cards); the plates carry crimson
# keystones in their corners (roll.png, shop.png).
GOLD_FRAMED = {"ui_paper_panel": 12, "ui_panel_hud": 12, "ui_section_panel": 12, "ui_tab_bottom": 12,
               "ui_card_controller": 12, "ui_card_controller_compact": 12, "ui_card_shop": 12, "ui_card_item": 8}
KEYSTONED = ("ui_paper_panel", "ui_panel_hud", "ui_section_panel", "ui_tab_bottom")
SLICED_SURFACES = tuple(s["name"] for s in SPRITES if any(s["border"]))
FELT_ZONES = {
    "dice_area": (52, 374, 1408, 276),
    "formula_row": (52, 660, 1408, 60),
    "submission_rail": (344, 740, 1136, 202),
    "submit_info_text150": (32, 740, 360, 202),
}
APPROVED_TITLE_FONT = Path("DicePrototype/Assets/Game/Fonts/Themes/deco/ZCOOLXiaoWei-Regular.ttf")
# Coordinator, phase 1 review (2026-11-18), decision 2: the W20Y key buttonSecondary sets every SolidButton11 face. It
# stays out of theme.json until W20Y declares it in ThemePalette.Hexes; the generator keeps it as a pending key.
BUTTON_SECONDARY = "7A5C30"


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


def de00(a, b):
    return ciede2000(lab(np.asarray(a, dtype=np.float64)), lab(np.asarray(b, dtype=np.float64)))


def tinted(face, tint):
    """A face pixel times a tint, in linear light (the project's colour space), back to 8-bit sRGB."""
    linear = channel(face) * channel(tint)
    srgb = np.where(linear <= .0031308, linear * 12.92, 1.055 * np.power(linear, 1 / 2.4) - .055)
    return np.rint(srgb * 255)


def label_on(face, tint, palette, preferred=None):
    """GameApp.LabelOn11 (Play11Contrast.cs), written out: on the fill face x tint a preferred colour stays at 4.5:1;
    else the best of ink, paper and textColor at 4.95:1; else the tint steps 5 % (in sRGB) toward black under the
    lightest candidate or toward white under the darkest, the fewer steps winning and a tie deepening. Returns the label,
    the tint as drawn and the number of steps."""
    tint = np.asarray(tint, dtype=np.float64)
    fill = tinted(face, tint)
    if preferred is not None and float(contrast(preferred, fill)) >= 4.5:
        return np.asarray(preferred, dtype=np.float64), tint, 0
    candidates = [np.array(hex_rgb(palette[k]), dtype=np.float64) for k in ("ink", "paper", "textColor")]
    ratios = [float(contrast(c, fill)) for c in candidates]
    best = candidates[int(np.argmax(ratios))]
    if max(ratios) >= 4.95:
        return best, tint, 0
    light, dark = best, best
    for c in candidates:
        if float(luminance(c)) > float(luminance(light)):
            light = c
        if float(luminance(c)) < float(luminance(dark)):
            dark = c

    def steps(toward, label):
        t = tint / 255.0
        for i in range(25):
            if float(contrast(label, tinted(face, t * 255.0))) >= 4.95:
                return i, t * 255.0
            t = t + (np.asarray(toward, dtype=np.float64) - t) * .05
        return -1, None
    deepen, deep_tint = steps((0, 0, 0), light)
    lighten, light_tint = steps((1, 1, 1), dark)
    if deepen < 0 and lighten < 0:
        return best, tint, 0
    down = lighten < 0 or 0 <= deepen <= lighten
    return (light, deep_tint, deepen) if down else (dark, light_tint, lighten)


def png_hashes(folder):
    return {path.relative_to(folder).as_posix(): hashlib.sha256(path.read_bytes()).hexdigest()
            for path in folder.rglob("*.png")}


def load(name):
    with Image.open(ROOT / THEME / "UI" / (name + ".png")) as image:
        return np.asarray(image.convert("RGBA"))


def load_default(name):
    with Image.open(ROOT / BY_NAME[name]["default"]) as image:
        return np.asarray(image.convert("RGBA"))


def table(name="table_scene"):
    with Image.open(ROOT / THEME / "Table" / (name + ".png")) as image:
        return np.asarray(image.convert("RGB")).astype(int)


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


def ring_insets(pixels, border):
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


def slice_axis(n_src, lo, hi, n_dst, scale=1.0):
    """Source index of each destination pixel along a sliced axis (nearest), as Unity draws a sliced Image: the borders
    at `scale` destination px per source px, shrunk together when the destination is shorter than both."""
    lo_d, hi_d = lo * scale, hi * scale
    if lo_d + hi_d > n_dst:
        k = n_dst / (lo_d + hi_d)
        lo_d, hi_d = lo_d * k, hi_d * k
    x = np.arange(n_dst) + .5
    u = lo + (x - lo_d) / max(n_dst - lo_d - hi_d, 1e-6) * (n_src - lo - hi)
    head, tail = x < lo_d, x > n_dst - hi_d
    u[head] = x[head] / max(lo_d, 1e-6) * lo
    u[tail] = n_src - (n_dst - x[tail]) / max(hi_d, 1e-6) * hi
    return np.clip(u.astype(int), 0, n_src - 1)


def sliced(pixels, border, width, height, scale=1.0):
    """A sliced sprite drawn width x height destination px; border L, B, R, T in source px."""
    left, bottom, right, top = border
    h, w = pixels.shape[:2]
    return pixels[np.ix_(slice_axis(h, top, bottom, height, scale), slice_axis(w, left, right, width, scale))]


def crimson(px):
    px = px.astype(int)
    return (px[..., 0] >= 90) & (px[..., 0] >= 2.5 * px[..., 1]) & (px[..., 0] >= 2.5 * px[..., 2])


class DecoArtTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        spec = importlib.util.spec_from_file_location("deco_asset_builder", ROOT / "tools/art_v11/deco/build_deco.py")
        cls.builder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.builder)
        scratch_parent = ROOT / "ArtSource/Art11/deco"
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
        self.assertEqual(len(SPRITES), 50, "Review W20 scope if the input contract changes.")
        expected = {sprite["name"] for sprite in SPRITES}
        self.assertEqual({path.stem for path in (ROOT / THEME / "UI").glob("*.png")}, expected)
        expected_paths = {(THEME / "UI" / (name + ".png")).as_posix() for name in expected}
        expected_paths.update({(THEME / "Table/table_scene.png").as_posix(),
                               (THEME / "Table/page_scene.png").as_posix(),   # THEME_SPEC 3, W20X (optional)
                               (THEME / "Table/felt_tile.png").as_posix(),
                               (THEME / "preview.png").as_posix(), REVIEW.as_posix()})
        self.assertEqual(set(self.first_hashes), expected_paths)
        self.assertEqual(len(self.first_hashes), 55)
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
        self.assertEqual(checked, 20)   # the 22 of the 53-sprite contract less ui_price_chip and ui_frame_enchant

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

    def _edge_bands(self, name):
        pixels = load(name).astype(np.int16)
        h, w = pixels.shape[:2]
        left, bottom, right, top = map(int, BY_NAME[name]["border"])
        # Along-edge sequences: rows of the top and bottom bands, columns of the left and right bands, between the
        # corner squares (the part Unity stretches).
        return [("top", pixels[:top, left:w - right]), ("bottom", pixels[h - bottom:, left:w - right]),
                ("left", pixels[top:h - bottom, :left].transpose(1, 0, 2)),
                ("right", pixels[top:h - bottom, w - right:].transpose(1, 0, 2))]

    def test_stretched_tint_surfaces_have_edge_bands_invariant_along_the_edge(self):
        for name in STRETCHED_TINT:
            for side, band in self._edge_bands(name):
                with self.subTest(sprite=name, side=side):
                    np.testing.assert_array_equal(band, np.broadcast_to(band[:, :1], band.shape))

    def test_stretched_authored_surfaces_keep_only_soft_runs_on_their_edges(self):
        # Brief rule 2: stretched bands carry at most soft along-edge runs (6 levels a pixel); keystones, braces and other
        # ornament belong to the corner squares.
        for name in STRETCHED_AUTHORED:
            for side, band in self._edge_bands(name):
                with self.subTest(sprite=name, side=side):
                    step = np.abs(np.diff(band, axis=1)).max()
                    self.assertLessEqual(int(step), 6, "A crisp detail in the stretched middle would smear.")

    def test_frames_stay_in_their_bands_and_leave_text_room(self):
        # Brief rule 3: FrameInsets11 mirrored. The card faces, the stage card, the top bar and the value badge keep a
        # frame no deeper than the default style's on every side (their text boxes are as the default's or larger).
        no_deeper_than_default = ("ui_card_controller", "ui_card_controller_compact", "ui_card_shop", "ui_card_item",
                                  "ui_stage_card", "ui_topbar", "ui_chip_label")
        for name in no_deeper_than_default:
            border = BY_NAME[name]["border"]
            ours, theirs = frame_insets(load(name), border), frame_insets(load_default(name), border)
            for side, (a, b) in enumerate(zip(ours, theirs)):
                with self.subTest(sprite=name, side=side):
                    self.assertGreaterEqual(a, 1, "The surface needs a visible frame.")
                    self.assertLessEqual(a, b)
        # Max inset per side in source px (PPU 200: 2 px = 1 unit) where text sits inside the other frames.
        limits = {"ui_chip_formula": 14, "ui_button_bevel": 20, "ui_info_inset": 15, "ui_slot_inventory": 22,
                  "ui_paper_panel": 24, "ui_panel_hud": 24, "ui_tab_bottom": 24, "ui_section_panel": 24}
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
        # W14 (GameApp.Shop14 Section14): the header strip starts at the section panel's frame insets and is
        # ShopHead14 (68) - top inset - 2 units tall; it keeps 50 units for its 22-unit title.
        section = frame_insets(load("ui_section_panel"), BY_NAME["ui_section_panel"]["border"])
        self.assertGreaterEqual(68 - section[3] / 2 - 2, 50)
        # A 34-unit tab keeps at least 20 units of height for its one-line label.
        chip = frame_insets(load("ui_chip_formula"), BY_NAME["ui_chip_formula"]["border"])
        self.assertLessEqual((chip[1] + chip[3]) / 2, 14)
        # The 30-unit value badge (ui_chip_label) keeps 18 units for its label.
        badge = frame_insets(load("ui_chip_label"), BY_NAME["ui_chip_label"]["border"])
        self.assertGreaterEqual(30 - (badge[1] + badge[3]) / 2, 18)

    def test_a_two_line_hint_at_150_percent_keeps_5_units_under_it(self):
        # Brief rule 3. DrawActions11: the hint panel is 88 units tall, its text box 6 units in from the top and bottom,
        # centred (MidlineLeft), Body11(16) x 1.5 = 24 units at 150 % text, lineSpacing -8. Offline TMP estimate: a line
        # advances 1.45 - 0.08 em and a CJK glyph ends 0.42 em below its line's middle (Noto Sans SC). The panel's
        # bottom frame (FrameInsets11) must leave 5 units under the second line.
        size, height = 24.0, 88.0
        advance = size * (1.45 - .08)
        top = 6 + (height - 12 - 2 * advance) / 2
        glyph_bottom = top + advance + advance / 2 + .42 * size
        bottom = frame_insets(load("ui_panel_hud"), BY_NAME["ui_panel_hud"]["border"])[1] / 2
        self.assertGreaterEqual(height - bottom - glyph_bottom, 5.0)

    # ------------------------------------------------------------------ the deco shapes
    def test_corners_are_45_degree_chamfers(self):
        # The first opaque pixel of each row in the top-left corner square lies on one straight line at 45 degrees: a
        # chamfer at least 7 px long (sancai's arcs, the default's steps and square corners fail this).
        for name in CHAMFERED:
            with self.subTest(sprite=name):
                alpha = load(name)[..., 3]
                self.assertEqual(int(alpha[2, 2]), 0, "The corner is cut, not square.")
                points = []
                for y in range(40):
                    xs = np.flatnonzero(alpha[y, :40] >= 128)
                    if xs.size and xs[0] > 1:
                        points.append((y, int(xs[0])))
                points = np.array(points, dtype=np.float64)
                self.assertGreaterEqual(len(points), 7)
                slope, offset = np.polyfit(points[:, 0], points[:, 1], 1)
                self.assertAlmostEqual(float(slope), -1.0, delta=.1)
                residual = np.abs(points[:, 0] * slope + offset - points[:, 1])
                self.assertLessEqual(float(residual.max()), 1.0, "One straight cut, not steps or an arc.")

    def test_gold_frames_are_modelled_metal(self):
        # Coordinator decision 4 (phase 1 review): the gold carries a highlight and a shaded edge like the concept's
        # mouldings, not a flat ochre. Across each framed surface's band (middle row, from the left edge): a run of warm
        # gold, its brightest pixel at 0.70 relative luminance or more and its darkest under 0.12.
        for name, least in GOLD_FRAMED.items():
            with self.subTest(sprite=name):
                pixels = load(name)
                row = pixels[pixels.shape[0] // 2, :24].astype(int)
                warm = ((row[:, 3] >= 250) & (row[:, 0] - row[:, 2] >= 60) & (row[:, 0] >= row[:, 1])
                        & (row[:, 1] >= row[:, 2]))
                self.assertGreaterEqual(int(warm.sum()), least)
                lum = luminance(row[warm, :3])
                self.assertGreaterEqual(float(lum.max()), .70)
                self.assertLess(float(lum.min()), .12)

    def test_plates_carry_crimson_keystones_in_their_corners_only(self):
        for name in KEYSTONED:
            pixels = load(name)
            h, w = pixels.shape[:2]
            red = crimson(pixels[..., :3]) & (pixels[..., 3] >= 250)
            for corner, (ys, xs) in {"tl": (slice(0, 40), slice(0, 40)), "tr": (slice(0, 40), slice(w - 40, w)),
                                     "bl": (slice(h - 40, h), slice(0, 40)),
                                     "br": (slice(h - 40, h), slice(w - 40, w))}.items():
                with self.subTest(sprite=name, corner=corner):
                    self.assertGreaterEqual(int(red[ys, xs].sum()), 20)
            with self.subTest(sprite=name, part="stretched middle"):
                red[:40, :40] = red[:40, w - 40:] = red[h - 40:, :40] = red[h - 40:, w - 40:] = False
                self.assertEqual(int(red.sum()), 0)

    def test_section_caption_and_meters_end_in_points(self):
        # shop.png's crimson section tabs end in a chevron; the meters (ui_progress_*) are pointed at both ends. The
        # point lies on the strip's middle line and the strip widens evenly from it.
        alpha = load("ui_section_header")[..., 3]
        h, w = alpha.shape
        spans = []
        for x in range(w - 1, w - 72, -1):
            ys = np.flatnonzero(alpha[:, x] >= 128)
            if ys.size:
                spans.append((x, int(ys[0]), int(ys[-1])))
        self.assertGreaterEqual(len(spans), 30)
        tip_x = spans[0][0]
        self.assertGreaterEqual(tip_x, w - 12, "the point reaches the strip's right end")
        for x, top, bottom in spans:
            with self.subTest(sprite="ui_section_header", column=x):
                self.assertLessEqual(abs(top + bottom - (h - 1)), 3)
        self.assertLessEqual(spans[0][2] - spans[0][1], 12, "a point, not a square end")
        self.assertTrue(all(b[2] - b[1] >= a[2] - a[1] for a, b in zip(spans, spans[1:])))
        for name in ("ui_progress_fill", "ui_progress_track"):
            alpha = load(name)[..., 3]
            h, w = alpha.shape
            for end, columns in (("left", range(0, 40)), ("right", range(w - 1, w - 41, -1))):
                runs = [(x, np.flatnonzero(alpha[:, x] >= 128)) for x in columns]
                runs = [(x, int(ys[0]), int(ys[-1])) for x, ys in runs if ys.size]
                with self.subTest(sprite=name, end=end):
                    self.assertLessEqual(runs[0][2] - runs[0][1], 12)
                    self.assertTrue(all(abs(t + b - (h - 1)) <= 2 for _, t, b in runs))
                    self.assertGreaterEqual(runs[-1][2] - runs[-1][1], h * .7)

    def test_inner_pieces_stay_inside_their_hosts(self):
        # Where the game lays one sprite inside another, the inner one must not stick out of the host's chamfers, at
        # 2 px a unit: the W13 rarity ring on the card face's inner rect (FrameInsets11); the stage card's boss and
        # pressure rings (DrawStageCard11); the tray's accent rings (1.6x and 1x, DrawTray11); the W14 section caption on
        # its panel (Shop14 Section14).
        from scipy import ndimage

        def draw(name, w_units, h_units, mult=1.0):
            return sliced(load(name), BY_NAME[name]["border"], int(round(w_units * 2)), int(round(h_units * 2)),
                          1.0 / mult)

        def outside(host, inner, at, filled=False):
            x, y = (int(round(v * 2)) for v in at)
            mask = inner[..., 3] >= 128
            ok = ndimage.binary_fill_holes(host[..., 3] >= 128) if filled else host[..., 3] >= 250
            region = ok[y:y + mask.shape[0], x:x + mask.shape[1]]
            self.assertEqual(region.shape, mask.shape)
            return int((mask & ~region).sum())

        for card, w, h in (("ui_card_controller", 216, 244), ("ui_card_shop", 220, 348)):
            face = draw(card, w, h)
            left, bottom, right, top = (v / 2 for v in frame_insets(load(card), BY_NAME[card]["border"]))
            ring = draw("ui_card_frame_rarity", w - left - right, h - top - bottom)
            with self.subTest(host=card, inner="rarity ring"):
                self.assertEqual(outside(face, ring, (left, top)), 0)
        # The stage card's incised line is open (the felt shows in it): measure against its filled silhouette.
        stage = draw("ui_stage_card", 392, 244)
        for at, size in (((4, 4), (376, 226)), ((9, 9), (366, 216)), ((5, 5), (374, 224))):
            with self.subTest(host="ui_stage_card", ring=at):
                self.assertEqual(outside(stage, draw("ui_ring", *size), at, filled=True), 0)
        tray = draw("ui_dice_tray", 400, 300)
        for at, size, mult in (((7, 7), (386, 286), 1.6), ((6, 6), (388, 288), 1.0), ((10, 10), (380, 280), 1.0)):
            with self.subTest(host="ui_dice_tray", ring=at):
                self.assertEqual(outside(tray, draw("ui_ring", *size, mult=mult), at, filled=True), 0)
        panel = draw("ui_section_panel", 900, 200)
        left, bottom, right, top = (v / 2 for v in frame_insets(load("ui_section_panel"), BY_NAME["ui_section_panel"]["border"]))
        strip = draw("ui_section_header", 900 - left - right, 68 - top - 2)
        with self.subTest(host="ui_section_panel", inner="W14 caption strip"):
            self.assertEqual(outside(panel, strip, (left, top)), 0)

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
        rendered = Image.alpha_composite(Image.new("RGBA", size, tuple(int(v) for v in host) + (255,)), icon)
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
        # Brief rule 6, at real size on the real hosts: SolidButton11's face today (lerp(Background, Panel, .55)) and
        # with the pending buttonSecondary (W20Y), the top bar, the score and resource wells, the felt, the paper.
        p = self.palette
        lerp = lambda a, b, t: tuple(round(x + (y - x) * t) for x, y in zip(hex_rgb(a), hex_rgb(b)))
        solid_today, solid_w20y = lerp(p["background"], p["panel"], .55), hex_rgb(BUTTON_SECONDARY)
        cases = (  # sprite, display size, host colour as the game draws it
            ("ui_icon_coin", (26, 26), solid_today), ("ui_icon_coin", (26, 26), solid_w20y),
            ("ui_icon_coin", (36, 36), hex_rgb(p["panel"])),
            ("ui_icon_roll", (36, 36), tuple(load("ui_info_inset")[256, 256, :3])),
            ("ui_icon_dice_view", (40, 40), solid_today), ("ui_icon_dice_view", (40, 40), solid_w20y),
            ("ui_tag_lock", (28, 30), hex_rgb(p["felt"])), ("ui_tag_lock", (22, 24), hex_rgb(p["felt"])),
            ("fx_score_crown", (48, 32), hex_rgb(p["panel"])),
            ("badge_enchant_base10", (28, 28), hex_rgb(p["paper"])), ("badge_enchant_mult2", (28, 28), hex_rgb(p["paper"])),
            ("badge_enchant_roll1", (28, 28), hex_rgb(p["paper"])), ("badge_enchant_slotless", (28, 28), hex_rgb(p["paper"])),
            ("badge_enchant_submit1", (28, 28), hex_rgb(p["paper"])),
        )
        for name, size, host in cases:
            with self.subTest(sprite=name, size=size, host=host):
                ratio = contrast(self._composite(name, size, host), host)
                self.assertGreaterEqual(float((ratio >= 3.0).mean()), .15)

    # ------------------------------------------------------------------ card template (W13, CARD_TEMPLATE 3 and 6)
    @staticmethod
    def _erode(mask, n):
        out = mask.copy()
        for _ in range(n):
            p = np.pad(out, 1, constant_values=False)
            out = out & p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:]
        return out

    def test_rarity_ring_is_a_slim_band_between_lacquer_lines(self):
        # DrawCard13 lays the name, picture and short text inside the ring's band (RingInsets13): a band no wider than the
        # default ring keeps the card's content box as the default style has it. Across the band: a lacquer line, the
        # band that shows the tint itself, a lacquer line (a pale rarity still reads on the ivory).
        border = BY_NAME["ui_card_frame_rarity"]["border"]
        ours = ring_insets(load("ui_card_frame_rarity"), border)
        theirs = ring_insets(load_default("ui_card_frame_rarity"), border)
        for side, (a, b) in enumerate(zip(ours, theirs)):
            with self.subTest(side=side):
                self.assertGreaterEqual(a, 8, "The ring must stay visible.")
                self.assertLessEqual(a, b)
        across = load("ui_card_frame_rarity")[:ours[3] + 3, 256].astype(int)
        solid = across[across[:, 3] >= 250, 0]
        self.assertLessEqual(int(solid.min()), 40)
        self.assertGreaterEqual(int(solid.max()), 240)
        dark = np.flatnonzero((across[:, 0] <= 60) & (across[:, 3] >= 128))
        light = np.flatnonzero((across[:, 0] >= 240) & (across[:, 3] >= 250))
        self.assertTrue(dark.size and light.size)
        self.assertLess(int(dark.min()), int(light.min()), "a lacquer line outside the band")
        self.assertGreater(int(dark.max()), int(light.max()), "and one inside it")

    def test_rarity_band_reads_against_the_lines_round_it(self):
        # The band sits between lacquer lines on every card face: each rarity colour and Ghost (the goods' ring) keeps 3:1
        # against those lines, so the ring shows on the ivory and next to the card frame's lacquer gap alike.
        p = self.palette
        across = load("ui_card_frame_rarity")[:12, 256].astype(int)
        line = across[(across[:, 3] >= 250) & (across[:, 0] <= 60), :3].max(axis=0)
        for key in ("rarityCommon", "rarityAdvanced", "rarityRare", "ghost"):
            with self.subTest(ring=key):
                self.assertGreaterEqual(float(contrast(hex_rgb(p[key]), line)), 3.0)

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
        # CARD_TEMPLATE 6: light, faceted gems with an ink outline. Most of the face stays light so the rarity tint reads as
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

    # ------------------------------------------------------------------ table, page, tile, preview
    # THEME_SPEC 3.1 and brief rule 4. Bounding rectangles (x, y, w, h) of the play screen's buttons and panels in any
    # state and at 100 % or 150 % text, written from the code independently of the generator: GameApp.Play11 L11, a card's
    # 10-unit pop above the card row, the tray's tab 14 units above the tray, UiKit.Button's step (2 units below the
    # face), the secondary button at both sizes, the qualified end ring (4 units out).
    PLAY_RECTS = {
        "top bar": (16, 12, 1888, 72), "card row": (32, 86, 1448, 254), "stage card": (1496, 96, 392, 244),
        "tray": (32, 338, 1448, 390), "resources": (1496, 352, 392, 376), "submit row": (32, 740, 1448, 202),
        "settle": (1496, 740, 392, 202), "hint": (32, 958, 600, 88), "secondary": (560, 966, 324, 72),
        "primary": (840, 962, 436, 82), "end": (1492, 958, 400, 88),
    }
    # The one rectangle of the action row free in every state at both sizes: right of the main button (1276 at 150 %),
    # left of the end slot's qualified ring (1492), below the submit row (942).
    ACTION_FREE = (1276, 942, 1492, 1080)
    # The screen frame: a gold double rule within 12 px of the screen edge (its chamfered corners reach 30 px).
    FRAME = 12
    CORNER = 30

    def _felt_mask(self, px):
        return np.abs(px - np.array(hex_rgb(self.palette["felt"]))).sum(axis=2) == 0

    def test_table_scene_and_preview_sizes(self):
        with Image.open(ROOT / THEME / "Table/table_scene.png") as image:
            self.assertEqual((image.size, image.mode), ((1920, 1080), "RGB"))
        with Image.open(ROOT / THEME / "preview.png") as image:
            self.assertEqual(image.size, (480, 270))

    def test_calm_zones_are_exactly_the_felt_colour(self):
        felt = np.array(hex_rgb(self.palette["felt"]), dtype=np.uint8)
        pixels = table().astype(np.uint8)
        for name, (x, y, w, h) in FELT_ZONES.items():
            with self.subTest(zone=name):
                zone = pixels[y:y + h, x:x + w]
                np.testing.assert_array_equal(zone, np.broadcast_to(felt, zone.shape))

    def test_no_decoration_inside_any_button_or_panel(self):
        # Decoration inside a bounding rectangle would show through the chamfers, and the card row changes with the card
        # count: every pixel inside every rectangle is the felt itself.
        felt = self._felt_mask(table())
        for name, (x, y, w, h) in self.PLAY_RECTS.items():
            with self.subTest(rect=name):
                self.assertEqual(int((~felt[y:y + h, x:x + w]).sum()), 0)

    def test_decoration_is_the_frame_its_corner_fans_and_one_sunburst(self):
        # Rule 4: away from the screen frame and its two lower corner fans, the only decoration is the sunburst in the
        # action row's free rectangle, with at least 4 px to spare on each side.
        px = table()
        other = ~self._felt_mask(px)
        f, c = self.FRAME, self.CORNER
        other[:f], other[-f:], other[:, :f], other[:, -f:] = False, False, False, False
        for ys, xs in ((slice(0, c), slice(0, c)), (slice(0, c), slice(-c, None)), (slice(-c - 20, None), slice(0, c + 20)),
                       (slice(-c - 20, None), slice(-c - 20, None))):
            other[ys, xs] = False
        ys, xs = np.nonzero(other)
        self.assertGreater(len(xs), 2000, "a readable sunburst")
        x0, y0, x1, y1 = self.ACTION_FREE
        self.assertGreaterEqual(int(xs.min()), x0 + 4)
        self.assertLessEqual(int(xs.max()), x1 - 4)
        self.assertGreaterEqual(int(ys.min()), y0 + 4)
        from scipy import ndimage
        self.assertEqual(ndimage.label(other, structure=np.ones((3, 3)))[1], 1, "one figure, standing on the frame")

    def test_decorations_close_in_a_dark_outline(self):
        # Rule 4: a decoration ends in a point or a closed outline, never cut by a straight line. Every bright pixel of the
        # sunburst and of the corner fans lies at least 1.5 px inside a dark outline (never next to the felt).
        from scipy import ndimage
        px = table()
        felt = self._felt_mask(px)
        for name, (ys, xs) in {"sunburst": (slice(940, 1072), slice(1270, 1500)),
                               "left corner fan": (slice(1030, 1074), slice(6, 50)),
                               "right corner fan": (slice(1030, 1074), slice(1870, 1914))}.items():
            with self.subTest(decoration=name):
                region, open_ = px[ys, xs], felt[ys, xs]
                bright = (region.max(axis=2) >= 150) & ~open_
                self.assertGreater(int(bright.sum()), 50)
                distance = ndimage.distance_transform_edt(~open_)
                self.assertGreaterEqual(float(distance[bright].min()), 1.5)

    def test_the_screen_frame_is_a_closed_gold_double_rule(self):
        # roll.png closes the screen in gold: on every side's middle a bright band at the edge, a lacquer gap, a rule.
        px = table()
        for side, line in (("left", px[540, :12]), ("right", px[540, ::-1][:12]), ("top", px[:12, 960]),
                           ("bottom", px[::-1, 960][:12])):
            with self.subTest(side=side):
                lum = luminance(line)
                self.assertGreaterEqual(float(lum[:5].max()), .5, "the gold band")
                gap = int(np.argmax(lum[2:] < .02)) + 2
                self.assertLessEqual(gap, 7)
                self.assertGreaterEqual(float(lum[gap:12].max()), .2, "the inner rule")

    def test_page_scene_is_the_plain_lacquer_page(self):
        # THEME_SPEC 3 / 3.1 (accepted in the phase 1 review): the non-play screens' backdrop is the felt colour in the same
        # screen frame, with no layout shapes and no other decoration.
        page, play = table("page_scene"), table()
        felt = self._felt_mask(page)
        f, c = self.FRAME, self.CORNER
        inner = felt[f:-f, f:-f].copy()
        inner[:c, :c] = inner[:c, -c:] = inner[-c:, :c] = inner[-c:, -c:] = True
        self.assertEqual(int((~inner).sum()), 0)
        same = np.abs(page - play).sum(axis=2) == 0
        x0, y0, x1, y1 = self.ACTION_FREE
        same[y0 - 4:, x0 - 4:x1 + 4] = True                  # the sunburst
        same[-60:, :60] = same[-60:, -60:] = True            # the corner fans
        self.assertEqual(int((~same).sum()), 0, "the frame is the table's frame")

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

    def test_boss_stands_apart_from_primary_and_pressure(self):
        # Coordinator decision 1 (phase 1 review): primary stays crimson; boss moves at least 15 CIEDE2000 from it, also as
        # the plate is drawn (LabelOn11 may lighten or deepen the tint), and stays clearly apart from pressure. Its labels
        # reach 4.95:1 through LabelOn11; its rings on the felt and on the stage card, and the ink tier bars on the
        # emblem, keep 3:1 (non-text contrast).
        p = self.palette
        boss, primary, pressure = (np.array(hex_rgb(p[k]), dtype=np.float64) for k in ("boss", "primary", "pressure"))
        white = np.array((255, 255, 255), dtype=np.float64)
        self.assertEqual(tuple(int(v) for v in load("ui_chip_formula")[256, 256, :3]), (255, 255, 255))
        label, plate_tint, steps = label_on(white, boss, p)
        plate = tinted(white, plate_tint)
        self.assertGreaterEqual(float(contrast(label, plate)), 4.95)
        self.assertLessEqual(steps, 8, "the plate keeps the boss colour")
        self.assertGreaterEqual(de00(primary, boss), 15.0)
        self.assertGreaterEqual(de00(primary, plate), 15.0)
        self.assertGreaterEqual(de00(pressure, boss), 15.0)
        self.assertGreaterEqual(float(contrast(boss, hex_rgb(p["felt"]))), 3.0)
        self.assertGreaterEqual(float(contrast(boss, hex_rgb(p["paper"]))), 3.0)
        self.assertGreaterEqual(float(contrast(hex_rgb(p["ink"]), boss)), 3.0)

    def test_button_secondary(self):
        # Coordinator decision 2 (phase 1 review): a mid-tone bronze plaque for every SolidButton11 face, at least 3:1
        # against the felt and the paper, its label 4.95:1 through LabelOn11. Until the coordinator adds it to theme.json
        # at the W20Y merge, the generator keeps it as a pending key; either way the value is the decided one.
        p = self.palette
        value = p.get("buttonSecondary") or self.builder.PENDING_TOKENS.get("buttonSecondary")
        self.assertEqual(value, BUTTON_SECONDARY)
        face = load("ui_chip_formula")[256, 256, :3].astype(np.float64)
        tint = np.array(hex_rgb(BUTTON_SECONDARY), dtype=np.float64)
        fill = tinted(face, tint)
        self.assertGreaterEqual(float(contrast(fill, hex_rgb(p["felt"]))), 3.0)
        self.assertGreaterEqual(float(contrast(fill, hex_rgb(p["paper"]))), 3.0)
        label, drawn_tint, steps = label_on(face, tint, p, preferred=np.array(hex_rgb(p["textColor"]), dtype=np.float64))
        self.assertEqual(steps, 0)
        self.assertGreaterEqual(float(contrast(label, fill)), 4.95)

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

    def test_w20x_tokens(self):
        # THEME_SPEC 2 (W20X): bronze steps under the ivory buttons and a lighter gold plinth under the crimson main
        # button, each apart from its face and visible on the felt; the section panel stays authored (section FFFFFF);
        # crimson captions for the shop headers and the tray tabs (accepted in the phase 1 review), whose labels LabelOn11
        # picks at 4.95:1 or better; the dice style page's Muted notes keep 4.5:1 on the section cell's centre.
        p = self.palette
        felt = hex_rgb(p["felt"])
        for step, face in (("buttonStep", "paper"), ("buttonStepPrimary", "primary")):
            with self.subTest(step=step):
                self.assertGreaterEqual(de00(hex_rgb(p[step]), hex_rgb(p[face])), 20.0)
                self.assertGreaterEqual(float(contrast(hex_rgb(p[step]), felt)), 2.5)
        self.assertEqual(p["section"], "FFFFFF")
        cell = tinted(load("ui_section_panel")[256, 256, :3], np.array(hex_rgb(p["section"]), dtype=np.float64))
        self.assertGreaterEqual(float(contrast(cell, hex_rgb(p["muted"]))), 4.5)
        self.assertGreaterEqual(float(contrast(cell, hex_rgb(p["textColor"]))), 4.5)
        caption = tinted(load("ui_section_header")[64, 256, :3], np.array(hex_rgb(p["caption"]), dtype=np.float64))
        self.assertGreaterEqual(max(float(contrast(hex_rgb(p[k]), caption)) for k in ("ink", "paper", "textColor")), 4.95)
        tab = tinted(load("ui_chip_formula")[256, 256, :3], np.array(hex_rgb(p["caption"]), dtype=np.float64))
        self.assertGreaterEqual(max(float(contrast(hex_rgb(p[k]), tab)) for k in ("ink", "paper", "textColor")), 4.95)

    def test_theme_json_schema(self):
        self.assertEqual(set(self.theme), {"id", "displayName", "description", "order", "preview", "palette"})
        self.assertEqual(self.theme["id"], "deco")
        self.assertEqual(self.theme["displayName"], "黑金装饰艺术")
        self.assertEqual(self.theme["order"], 4)
        self.assertIs(self.theme["preview"], True)
        self.assertIn("ArtSource/StyleKit/themes/deco/concepts", self.theme["description"])
        source = (ROOT / "DicePrototype/Assets/Game/UI/Theme/ThemePalette.cs").read_text(encoding="utf-8")
        declared = re.search(r"class Hexes\s*\{\s*public string ([^;]+);", source).group(1)
        keys = {key.strip() for key in declared.replace("\n", " ").split(",")}
        # valueBadge (W13), the W20X keys and W20Y's buttonSecondary may be left out: unset, they fall back to today's
        # drawing (THEME_SPEC 2). The 24 required keys are the same before W20Y declares buttonSecondary (29 keys) and
        # after (30; coordinator, 2026-11-18).
        w20x = {"buttonStep", "buttonStepPrimary", "section", "caption"}
        optional = {"valueBadge", "buttonSecondary"} | w20x
        self.assertEqual(len(keys - optional), 24)
        self.assertTrue(w20x <= keys)
        # Every palette key is one ThemePalette.Hexes declares (JsonUtility skips any other), so buttonSecondary stays out
        # until W20Y declares it; the 24 required keys and the four W20X keys are set; valueBadge falls back to gold.
        self.assertLessEqual(set(self.palette), keys)
        self.assertLessEqual((keys - optional) | w20x, set(self.palette))
        self.assertNotIn("valueBadge", self.palette)
        for key, value in self.palette.items():
            with self.subTest(token=key):
                self.assertRegex(value, r"^[0-9A-F]{6}([0-9A-F]{2})?$")

    def test_shipped_theme_json_matches_regeneration(self):
        self.assertEqual((ROOT / THEME / "theme.json").read_bytes(), (self.first / THEME / "theme.json").read_bytes())

    def test_concept_pages_match_their_recorded_sources(self):
        table_md = (ROOT / "ArtSource/StyleKit/themes/README.md").read_text(encoding="utf-8")
        recorded = dict(re.findall(r"\| deco \| [^|]+ \| concepts/(\w+)\.png \| [^|]+ \| ([0-9a-f]{64}) \|", table_md))
        self.assertEqual(set(recorded), {"roll", "shop", "select", "card", "triptych"})
        for page, digest in recorded.items():
            with self.subTest(page=page):
                path = ROOT / "ArtSource/StyleKit/themes/deco/concepts" / (page + ".png")
                self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), digest)

    def test_wordmark_uses_the_approved_title_face(self):
        # Brief rule 7: the wordmark is set in ZCOOL XiaoWei, the approved deco title font.
        self.assertTrue((ROOT / APPROVED_TITLE_FONT).is_file())
        self.assertEqual(Path(self.builder.HEADING).resolve(), (ROOT / APPROVED_TITLE_FONT).resolve())
        source = (ROOT / "tools/art_v11/deco/build_deco.py").read_text(encoding="utf-8")
        wordmark = source[source.index("def wordmark("):]
        wordmark = wordmark[:wordmark.index("\ndef ")]
        self.assertIn("HEADING", wordmark)

    # ------------------------------------------------------------------ determinism
    def test_full_build_is_byte_deterministic(self):
        self.assertEqual(self.first_hashes, self.second_hashes)

    def test_shipped_assets_match_regeneration(self):
        for relative, digest in self.first_hashes.items():
            with self.subTest(asset=relative):
                self.assertEqual(hashlib.sha256((ROOT / relative).read_bytes()).hexdigest(), digest)


if __name__ == "__main__":
    unittest.main()
