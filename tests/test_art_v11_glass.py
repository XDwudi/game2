"""Independent asset-contract checks for the W20 glass (彩绘玻璃) UI kit."""

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
THEME = Path("DicePrototype/Assets/Resources/Themes/glass")
REVIEW = Path("ArtSource/Art11/glass/contact_sheet.png")
CONTRACT = json.loads((ROOT / "tools/art_v11/theme_contract.json").read_text(encoding="utf-8"))
# The bottom tab shipped before W16 retired it. Its original artwork must still rebuild.
SPRITES = [sprite for sprite in CONTRACT["sprites"]
           if (sprite["referenced"] or sprite["name"] == "ui_tab_bottom") and not sprite.get("optionalOverride")]
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
# Coordinator, phase 2 (lesson from the deco kit): every sprite that holds text - buttons, chips, cards, panels, strips -
# keeps its FrameInsets11 depth at or under the default sprite's on every side.
TEXT_HOLDING = ("ui_card_controller", "ui_card_controller_compact", "ui_card_shop", "ui_card_item", "ui_stage_card",
                "ui_topbar", "ui_chip_label", "ui_chip_formula", "ui_button_bevel", "ui_panel_hud", "ui_section_panel",
                "ui_tab_bottom", "ui_paper_panel", "ui_info_inset", "ui_section_header", "ui_slot_inventory")
# The concept pages' shape language (ArtSource/StyleKit/themes/glass/concepts): leaded panels, cards and lights with
# 45-degree cut corners; the buttons and chips end in roll.png's 重掷 points (two slopes per end). The framed panels and
# cards carry a gilt four-point star on each cut corner (their corner squares are tested for the star instead).
CUT_CORNERS = {"ui_topbar": 12, "ui_stage_card": 14, "ui_dice_tray": 22, "ui_ring": 16, "ui_slot_inventory": 10,
               "ui_button_ghost": 10, "ui_card_frame_rarity": 9}
STARRED = ("ui_paper_panel", "ui_panel_hud", "ui_section_panel", "ui_tab_bottom", "ui_card_controller",
           "ui_card_controller_compact", "ui_card_shop", "ui_card_item")
POINTED = {"ui_button_bevel": (28, 34), "ui_chip_formula": (22, 30)}      # px across, px down per slope
SLICED_SURFACES = tuple(s["name"] for s in SPRITES if any(s["border"]))
FELT_ZONES = {
    "dice_area": (52, 374, 1408, 276),
    "formula_row": (52, 660, 1408, 60),
    "submission_rail": (344, 740, 1136, 202),
    "submit_info_text150": (32, 740, 360, 202),
}
APPROVED_TITLE_FONT = Path("DicePrototype/Assets/Game/Fonts/Themes/glass/NotoSerifSC-Black.otf")
# Coordinator, phase 1b review (2026-11-18): the W20Y key buttonSecondary (every SolidButton11 face) is amber glass
# AC8442. It was a pending key until W20Y declared it in ThemePalette.Hexes; since W20Y landed (merged into this branch
# for the kit merge, 2026-11-18) the generator writes it to theme.json.
BUTTON_SECONDARY = "AC8442"


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


def lerp(a, b, t):
    """Color.Lerp on sRGB values."""
    a, b = np.asarray(a, dtype=np.float64), np.asarray(b, dtype=np.float64)
    return a + (b - a) * t


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


def jewel(px, kind):
    """Saturated backlit glass: cobalt (blue well above red and green) or ruby (red well above green and blue)."""
    px = px.astype(int)
    r, g, b = px[..., 0], px[..., 1], px[..., 2]
    if kind == "cobalt":
        return (b >= 110) & (b >= r + 60) & (b >= g + 30)
    return (r >= 110) & (r >= g + 60) & (r >= b + 50)


class GlassArtTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        spec = importlib.util.spec_from_file_location("glass_asset_builder", ROOT / "tools/art_v11/glass/build_glass.py")
        cls.builder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.builder)
        scratch_parent = ROOT / "ArtSource/Art11/glass"
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
        self.assertEqual(checked, 20)

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
        # Brief rule 2: stretched bands carry at most soft along-edge runs (6 levels a pixel); stars, corner panes and
        # other ornament belong to the corner squares.
        for name in STRETCHED_AUTHORED:
            for side, band in self._edge_bands(name):
                with self.subTest(sprite=name, side=side):
                    step = np.abs(np.diff(band, axis=1)).max()
                    self.assertLessEqual(int(step), 6, "A crisp detail in the stretched middle would smear.")

    # ------------------------------------------------------------------ text room
    def test_text_holding_frames_are_no_deeper_than_the_default_on_any_side(self):
        # Coordinator, phase 2 (lesson from the deco kit): FrameInsets11 mirrored, every side at or under the default
        # sprite's. A side whose default is 0 (the info well's lit sides, the caption strip's left end) stays 0; every
        # other side keeps a visible frame.
        for name in TEXT_HOLDING:
            border = BY_NAME[name]["border"]
            ours, theirs = frame_insets(load(name), border), frame_insets(load_default(name), border)
            for side, (a, b) in zip("LBRT", zip(ours, theirs)):
                if border["LBRT".index(side)] == 0:
                    continue
                with self.subTest(sprite=name, side=side):
                    self.assertLessEqual(a, b)
                    if b > 0:
                        self.assertGreaterEqual(a, 1, "The surface needs a visible frame.")

    def test_frames_stay_in_their_bands_and_leave_text_room(self):
        for sprite in SPRITES:
            name = sprite["name"]
            if not any(sprite["border"]) or name in HOLLOW:
                continue
            with self.subTest(sprite=name):
                insets = frame_insets(load(name), sprite["border"])
                for value, limit in zip(insets, sprite["border"]):
                    self.assertLessEqual(value, limit)
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

    # ------------------------------------------------------------------ the glass shapes
    def test_corners_are_45_degree_cuts(self):
        # The first opaque pixel of each row in the top-left corner square lies on one straight 45-degree line (the
        # default's stepped corners, sancai's arcs and Swiss's square ends do not).
        for name, cut in CUT_CORNERS.items():
            with self.subTest(sprite=name):
                alpha = load(name)[..., 3]
                self.assertEqual(int(alpha[1, 1]), 0, "The corner is cut, not square.")
                points = []
                for y in range(2, cut - 1):
                    xs = np.flatnonzero(alpha[y, :40] >= 128)
                    if xs.size and xs[0] > 0:
                        points.append((y, int(xs[0])))
                points = np.array(points, dtype=np.float64)
                self.assertGreaterEqual(len(points), 5)
                slope, offset = np.polyfit(points[:, 0], points[:, 1], 1)
                self.assertAlmostEqual(float(slope), -1.0, delta=.15)
                residual = np.abs(points[:, 0] * slope + offset - points[:, 1])
                self.assertLessEqual(float(residual.max()), 1.0, "One straight cut, not steps or an arc.")
        # The framed panels and cards: the corner is cut, and a gilt star sits on it (bright warm gold in each corner
        # square, lit facets and shaded ones).
        for name in STARRED:
            pixels = load(name)
            h, w = pixels.shape[:2]
            corners = {"tl": pixels[:40, :40], "tr": pixels[:40, w - 40:]}
            if name != "ui_tab_bottom":
                corners.update({"bl": pixels[h - 40:, :40], "br": pixels[h - 40:, w - 40:]})
            self.assertEqual(int(pixels[1, 1, 3]), 0, name + ": the corner is cut, not square")
            for corner, block in corners.items():
                with self.subTest(sprite=name, corner=corner):
                    px = block.astype(int)
                    gold = (px[..., 3] >= 250) & (px[..., 0] >= 200) & (px[..., 0] - px[..., 2] >= 60)
                    self.assertGreaterEqual(int(gold.sum()), 15, "a gilt star on the corner")

    def test_buttons_and_chips_end_in_the_concept_points(self):
        # roll.png's 重掷: each end is a point made of two slopes that fill the corner squares; the side's middle (the
        # stretched slice) is straight. On the face as UiKit.Button draws it (h - 7 units: 53, 65 and 73 units for 60, 72
        # and 80-unit buttons; SolidButton's chip the whole height) the corners are never shrunk.
        for name, (p, q) in POINTED.items():
            with self.subTest(sprite=name):
                alpha = load(name)[..., 3]
                h, w = alpha.shape
                first = lambda y: int(np.flatnonzero(alpha[y] >= 128)[0])
                self.assertLessEqual(first(h // 2), 2, "the straight middle of the side")
                self.assertGreaterEqual(first(1), p - 4, "the slope starts near the top")
                for y in range(4, q - 4, 3):
                    with self.subTest(sprite=name, row=y):
                        self.assertLessEqual(abs(first(y) - p * (1 - y / q)), 2.0, "one straight slope")
                right = lambda y: w - 1 - int(np.flatnonzero(alpha[y] >= 128)[-1])
                self.assertGreaterEqual(right(1), p - 4, "the right end is pointed too")
        for height in (60, 72, 80):
            for name, units in (("ui_button_bevel", height - 7), ("ui_chip_formula", height)):
                with self.subTest(sprite=name, height=height):
                    alpha = sliced(load(name), BY_NAME[name]["border"], 600, units * 2)[..., 3]
                    self.assertGreaterEqual(int(np.flatnonzero(alpha[1] >= 128)[0]), POINTED[name][0] - 4)

    def test_cards_keep_the_jewel_glass_strip_and_panels_carry_it_in_their_corners(self):
        # Coordinator decision B (phase 1b): gilt frames with jewel glass. The cards keep the strip on every edge (cobalt;
        # ruby on the item card); the panels, slimmed to the default's text room in phase 2, carry the glass in a pane
        # in each corner square and none on the stretched edges.
        for name, kind in (("ui_card_controller", "cobalt"), ("ui_card_shop", "cobalt"), ("ui_card_item", "ruby")):
            with self.subTest(sprite=name):
                pixels = load(name)
                row = pixels[pixels.shape[0] // 2, :24, :3]
                self.assertGreaterEqual(int(jewel(row, kind).sum()), 3)
        for name, kind in (("ui_panel_hud", "cobalt"), ("ui_section_panel", "cobalt"), ("ui_tab_bottom", "cobalt"),
                           ("ui_paper_panel", "ruby")):
            pixels = load(name)
            h, w = pixels.shape[:2]
            glass = jewel(pixels[..., :3], kind) & (pixels[..., 3] >= 250)
            corners = {"tl": (slice(0, 40), slice(0, 40)), "tr": (slice(0, 40), slice(w - 40, w))}
            if name != "ui_tab_bottom":
                corners.update({"bl": (slice(h - 40, h), slice(0, 40)), "br": (slice(h - 40, h), slice(w - 40, w))})
            for corner, (ys, xs) in corners.items():
                with self.subTest(sprite=name, corner=corner):
                    self.assertGreaterEqual(int(glass[ys, xs].sum()), 20)
            with self.subTest(sprite=name, part="stretched middle"):
                glass[:40, :40] = glass[:40, w - 40:] = glass[h - 40:, :40] = glass[h - 40:, w - 40:] = False
                self.assertEqual(int(glass.sum()), 0)

    def test_gilt_frames_are_modelled_metal(self):
        # The gilt carries a highlight and a shaded side like the concept's mouldings, not a flat ochre: across each
        # framed surface's band (middle row, from the left edge) a run of warm gold, its brightest pixel at 0.5 relative
        # luminance or more and its darkest under 0.2.
        for name, least in (("ui_card_controller", 4), ("ui_card_item", 4), ("ui_paper_panel", 3), ("ui_panel_hud", 3),
                            ("ui_section_panel", 3)):
            with self.subTest(sprite=name):
                pixels = load(name)
                row = pixels[pixels.shape[0] // 2, :24].astype(int)
                warm = ((row[:, 3] >= 250) & (row[:, 0] - row[:, 2] >= 50) & (row[:, 0] >= row[:, 1])
                        & (row[:, 1] >= row[:, 2]))
                self.assertGreaterEqual(int(warm.sum()), least)
                lum = luminance(row[warm, :3])
                self.assertGreaterEqual(float(lum.max()), .5)
        lit = load("ui_panel_hud")[256, :12].astype(int)        # the left edge faces the light
        shaded = load("ui_panel_hud")[256, ::-1][:12].astype(int)
        self.assertGreater(float(luminance(lit[:, :3]).max()), float(luminance(shaded[:, :3]).max()))

    def test_section_caption_leaves_the_frame_and_meters_end_in_points(self):
        # The W14 caption strip comes out of the section panel's frame on the left (its face reaches the sprite's edge,
        # like the default's) and ends in a chevron point on its middle line on the right; the meters are pointed both
        # ends.
        pixels = load("ui_section_header")
        alpha = pixels[..., 3]
        h, w = alpha.shape
        self.assertTrue((alpha[h // 4:3 * h // 4, 0] >= 250).all(), "the left end is open")
        self.assertTrue((pixels[h // 4:3 * h // 4, 0, :3] >= 250).all())
        spans = []
        for x in range(w - 1, w - 72, -1):
            ys = np.flatnonzero(alpha[:, x] >= 128)
            if ys.size:
                spans.append((x, int(ys[0]), int(ys[-1])))
        self.assertGreaterEqual(len(spans), 20)
        for x, top, bottom in spans:
            with self.subTest(sprite="ui_section_header", column=x):
                self.assertLessEqual(abs(top + bottom - (h - 1)), 3)
        self.assertLessEqual(spans[0][2] - spans[0][1], 16, "a point, not a square end")
        self.assertTrue(all(b[2] - b[1] >= a[2] - a[1] for a, b in zip(spans, spans[1:])))
        for name in ("ui_progress_fill", "ui_progress_track"):
            alpha = load(name)[..., 3]
            h, w = alpha.shape
            for end, columns in (("left", range(0, 48)), ("right", range(w - 1, w - 49, -1))):
                runs = [(x, np.flatnonzero(alpha[:, x] >= 128)) for x in columns]
                runs = [(x, int(ys[0]), int(ys[-1])) for x, ys in runs if ys.size]
                with self.subTest(sprite=name, end=end):
                    self.assertLessEqual(runs[0][2] - runs[0][1], 16)
                    self.assertTrue(all(abs(t + b - (h - 1)) <= 2 for _, t, b in runs))
                    self.assertGreaterEqual(runs[-1][2] - runs[-1][1], h * .7)

    def test_inner_pieces_stay_inside_their_hosts(self):
        # Where the game lays one sprite inside another, the inner one must not stick out of the host's cut corners, at
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
    def _composite(self, name, size, host, tint=None):
        with Image.open(ROOT / THEME / "UI" / (name + ".png")) as image:
            icon = image.convert("RGBA").resize(size, Image.Resampling.LANCZOS)
        if tint is not None:
            px = np.asarray(icon).astype(np.float64)
            px[..., :3] = tinted(px[..., :3], np.asarray(hex_rgb(tint), dtype=np.float64))
            icon = Image.fromarray(np.clip(px, 0, 255).astype(np.uint8), "RGBA")
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
        # Brief rule 6, at real size on the real hosts: SolidButton11's face without buttonSecondary (lerp(Background,
        # Panel, .55)) and with it (W20Y, set in theme.json), the top bar, the roll well, the slate, the paper. Icons the
        # code tints (the shop's nav icons) are drawn in the label colour LabelOn11 picks on the amber face.
        p = self.palette
        solid_today = tuple(np.rint(lerp(hex_rgb(p["background"]), hex_rgb(p["panel"]), .55)).astype(int))
        solid_w20y = hex_rgb(BUTTON_SECONDARY)
        nav = label_on((255, 255, 255), solid_w20y, p, preferred=hex_rgb(p["textColor"]))[0]
        nav_hex = "".join(f"{int(v):02X}" for v in nav)
        cases = (  # sprite, display size, host colour as the game draws it, tint
            ("ui_icon_coin", (26, 26), solid_today, None), ("ui_icon_coin", (26, 26), solid_w20y, None),
            ("ui_icon_coin", (36, 36), hex_rgb(p["panel"]), None),
            ("ui_icon_roll", (36, 36), tuple(load("ui_info_inset")[256, 256, :3]), None),
            ("ui_icon_dice_view", (40, 40), solid_today, None), ("ui_icon_dice_view", (40, 40), solid_w20y, None),
            ("ui_tag_lock", (28, 30), hex_rgb(p["felt"]), None), ("ui_tag_lock", (22, 24), hex_rgb(p["felt"]), None),
            ("ui_tag_clock", (64, 64), tuple(load("ui_section_panel")[256, 256, :3]), None),
            ("fx_score_crown", (48, 32), hex_rgb(p["panel"]), None),
            ("ui_icon_refresh", (34, 34), solid_w20y, nav_hex), ("ui_icon_book", (34, 34), solid_w20y, nav_hex),
            ("ui_icon_gear", (34, 34), solid_w20y, nav_hex),
            ("badge_enchant_base10", (28, 28), hex_rgb(p["paper"]), None),
            ("badge_enchant_mult2", (28, 28), hex_rgb(p["paper"]), None),
            ("badge_enchant_roll1", (28, 28), hex_rgb(p["paper"]), None),
            ("badge_enchant_slotless", (28, 28), hex_rgb(p["paper"]), None),
            ("badge_enchant_submit1", (28, 28), hex_rgb(p["paper"]), None),
        )
        for name, size, host, tint in cases:
            with self.subTest(sprite=name, size=size, host=host):
                ratio = contrast(self._composite(name, size, host, tint), host)
                self.assertGreaterEqual(float((ratio >= 3.0).mean()), .15)

    def test_enchant_badges_keep_their_meaning_colours_and_signs(self):
        # The default badges' meanings: +10 green, x2 violet, +1 roll sky blue, slotless dark, +1 submit a tick (here on
        # the triptych's gold glass). Each badge is an octagonal glass medallion: the field's dominant hue and a pale
        # marble sign in the middle.
        hues = {"base10": (90, 170), "mult2": (250, 300), "roll1": (195, 230), "submit1": (25, 55)}
        for name in ("base10", "mult2", "roll1", "slotless", "submit1"):
            pixels = load("badge_enchant_" + name).astype(int)
            with self.subTest(badge=name):
                self.assertEqual(int(pixels[2, 2, 3]), 0, "an octagon, not a square")
                middle = pixels[64:192, 64:192]
                pale = (middle[..., 3] >= 250) & (middle[..., :3].min(axis=2) >= 200)
                self.assertGreaterEqual(float(pale.mean()), .08, "a pale marble sign in the middle")
                field = pixels[40:216, 40:216]
                solid = field[..., 3] >= 250
                rgb = field[solid][:, :3] / 255.0
                hsv = np.array([colorsys.rgb_to_hsv(*c) for c in rgb[::7]])
                coloured = hsv[(hsv[:, 1] >= .35) & (hsv[:, 2] >= .25)]
                if name == "slotless":
                    self.assertLess(len(coloured) / max(len(hsv), 1), .15, "the slotless badge is lead-grey glass")
                    continue
                self.assertGreater(len(coloured), 50)
                hue = np.degrees(np.angle(np.exp(2j * np.pi * coloured[:, 0]).mean())) % 360
                lo, hi = hues[name]
                self.assertTrue(lo <= hue <= hi, f"field hue {hue:.0f}")

    # ------------------------------------------------------------------ card template (W13, CARD_TEMPLATE 3 and 6)
    @staticmethod
    def _erode(mask, n):
        out = mask.copy()
        for _ in range(n):
            p = np.pad(out, 1, constant_values=False)
            out = out & p[:-2, 1:-1] & p[2:, 1:-1] & p[1:-1, :-2] & p[1:-1, 2:]
        return out

    def test_rarity_ring_is_a_slim_band_between_lead_lines(self):
        # DrawCard13 lays the name, picture and short text inside the ring's band (RingInsets13): a band no wider than the
        # default ring keeps the card's content box as the default style has it. Across the band: lead, the band that
        # shows the tint itself, lead.
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
        self.assertLess(int(dark.min()), int(light.min()), "a lead line outside the band")
        self.assertGreater(int(dark.max()), int(light.max()), "and one inside it")

    def test_rarity_band_reads_against_the_lines_round_it(self):
        # Each rarity colour and Ghost (the goods' ring) keeps 3:1 against the ring's own lead lines.
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
    # THEME_SPEC 3.1 and brief rule 4. Written from the code independently of the generator: the slate areas (the dice
    # area and the formula row with the 10-unit gap between them; the submit row's info and rail zones, which overlap),
    # the play screen's buttons and panels in any state and at 100 % or 150 % text (GameApp.Play11 L11, a card's 10-unit
    # pop above the card row, the tray's tab 14 units above the tray, UiKit.Button's step and 3-unit focus outline, the
    # secondary button at both sizes, the qualified end ring 4 units out), and the action row's one rectangle free in every
    # state: right of the main button with its focus outline (1279 at 150 %), left of the end slot's ring (1492), below the
    # submit row (942).
    SLATE = ((52, 374, 1460, 720), (32, 740, 1480, 942))
    PLAY_RECTS = {
        "top bar": (16, 12, 1888, 72), "card row": (32, 86, 1448, 254), "stage card": (1496, 96, 392, 244),
        "tray": (32, 338, 1448, 390), "resources": (1496, 352, 392, 376), "submit row": (32, 740, 1448, 202),
        "settle": (1496, 740, 392, 202), "hint": (32, 958, 600, 88), "secondary": (557, 963, 330, 78),
        "primary": (837, 959, 442, 88), "end": (1492, 958, 400, 89),
    }
    ACTION_FREE = (1279, 942, 1492, 1080)
    GILT = 8          # px: the gilt double line round each slate area (outward from its edge)

    def _felt_mask(self, px):
        return np.abs(px - np.array(hex_rgb(self.palette["felt"]))).sum(axis=2) == 0

    def _slate_and_frame(self, margin):
        mask = np.zeros((1080, 1920), bool)
        for x0, y0, x1, y1 in self.SLATE:
            mask[max(0, y0 - margin):y1 + margin, max(0, x0 - margin):x1 + margin] = True
        return mask

    def test_table_scene_and_preview_sizes(self):
        with Image.open(ROOT / THEME / "Table/table_scene.png") as image:
            self.assertEqual((image.size, image.mode), ((1920, 1080), "RGB"))
        with Image.open(ROOT / THEME / "Table/page_scene.png") as image:
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

    def test_slate_areas_are_framed_in_gilt_just_outside(self):
        # Coordinator decision A (phase 1b): the calm zones are slate in a gilt double line drawn outside them. Each slate
        # area is exactly the felt colour; the pixel ring just outside it holds no felt; within the frame's width outside
        # each side's middle there is bright gilt.
        px = table()
        felt = self._felt_mask(px)
        for x0, y0, x1, y1 in self.SLATE:
            with self.subTest(area=(x0, y0, x1, y1)):
                self.assertTrue(felt[y0:y1, x0:x1].all())
                ring = np.concatenate([felt[y0 - 1, x0 - 1:x1 + 1], felt[y1, x0 - 1:x1 + 1],
                                       felt[y0:y1, x0 - 1], felt[y0:y1, x1]])
                self.assertEqual(int(ring.sum()), 0)
                for side, line in (("top", px[y0 - self.GILT:y0, (x0 + x1) // 2]),
                                   ("bottom", px[y1:y1 + self.GILT, (x0 + x1) // 2]),
                                   ("left", px[(y0 + y1) // 2, x0 - self.GILT:x0]),
                                   ("right", px[(y0 + y1) // 2, x1:x1 + self.GILT])):
                    with self.subTest(area=(x0, y0), side=side):
                        lum = luminance(line)
                        self.assertGreaterEqual(float(lum.max()), .25, "a gilt line")
                        warm = (line[:, 0] - line[:, 2] >= 40) & (line[:, 0] >= line[:, 1])
                        self.assertGreaterEqual(int(warm.sum()), 2)

    def test_the_table_outside_the_slate_is_a_leaded_mosaic(self):
        # Decision A: outside the calm zones the play table is an all-over leaded stained-glass mosaic led by cobalt and
        # ruby (roll.png, card.png), darker than the gilt and the parchment so they lead the eye.
        px = table()
        outside = ~self._slate_and_frame(self.GILT + 2)
        x0, y0, x1, y1 = self.ACTION_FREE
        outside[y0:y1, x0:x1] = False                      # the rose window
        rgb = px[outside]
        lead = rgb.max(axis=1) <= 30
        self.assertTrue(.05 <= float(lead.mean()) <= .4, f"lead lines {lead.mean():.2f}")
        glass = rgb[~lead]
        blue = (glass[:, 2] > glass[:, 0] + 20) & (glass[:, 2] > glass[:, 1])
        red = (glass[:, 0] > glass[:, 2] + 20) & (glass[:, 0] > glass[:, 1] * 1.6)
        self.assertGreaterEqual(float(blue.mean()), .25, "cobalt panes")
        self.assertGreaterEqual(float(red.mean()), .15, "ruby panes")
        self.assertLess(float(luminance(rgb).mean()), .08)
        self.assertLess(float(self._felt_mask(px)[outside].mean()), .01, "no slate outside the calm areas")

    def test_the_rose_window_is_the_one_figure_and_stays_in_the_free_rectangle(self):
        # Rule 4: large decoration only where no play state puts interface, never inside a button's or panel's bounding
        # rectangle, closed in an outline. Away from the slate areas, the table and the page (the same mosaic) differ only
        # by the rose window; it lies in the action row's free rectangle with 4 px to spare, and its rim is a closed ring
        # of lead.
        play, page = table(), table("page_scene")
        differ = np.abs(play - page).sum(axis=2) > 0
        differ &= ~self._slate_and_frame(self.GILT + 2)
        ys, xs = np.nonzero(differ)
        self.assertGreater(len(xs), 2000, "a readable rose window")
        x0, y0, x1, y1 = self.ACTION_FREE
        self.assertGreaterEqual(int(xs.min()), x0 + 4)
        self.assertLessEqual(int(xs.max()), x1 - 4)
        self.assertGreaterEqual(int(ys.min()), y0 + 4)
        self.assertLessEqual(int(ys.max()), y1 - 4)
        for name, (rx, ry, rw, rh) in self.PLAY_RECTS.items():
            with self.subTest(rect=name):
                self.assertEqual(int(differ[ry:ry + rh, rx:rx + rw].sum()), 0)
        cx, cy = (xs.min() + xs.max()) / 2, (ys.min() + ys.max()) / 2
        outer = (xs.max() - xs.min() + 1) / 2
        darkest = []
        for a in np.linspace(0, 2 * math.pi, 360, endpoint=False):
            samples = [play[int(round(cy + r * math.sin(a))), int(round(cx + r * math.cos(a)))].max()
                       for r in np.arange(outer - 3.0, outer - .25, .25)]
            darkest.append(min(samples))
        self.assertLessEqual(int(max(darkest)), 60, "a closed lead rim all round")

    def test_page_scene_is_the_mosaic_without_layout_shapes(self):
        # THEME_SPEC 3 / 3.1 (decision A): the non-play screens' backdrop is the same mosaic everywhere: no slate, no gilt
        # line, no rose window; outside the slate areas and the rose window it is the play table pixel for pixel.
        page, play = table("page_scene"), table()
        self.assertLess(float(self._felt_mask(page).mean()), .01)
        same = np.abs(page - play).sum(axis=2) == 0
        same[self._slate_and_frame(self.GILT + 2)] = True
        x0, y0, x1, y1 = self.ACTION_FREE
        same[y0:y1, x0:x1] = True
        self.assertEqual(int((~same).sum()), 0)

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

    def test_boss_stands_apart_from_primary_the_red_pips_and_pressure(self):
        # Coordinator (lesson from the deco kit): boss at least 15 CIEDE2000 from primary, from the red pip colour (the
        # formula row puts the boss factor chip right after the red hand chip; deco's two chips were 5.8 apart) and from
        # pressure, also as the chips are drawn (LabelOn11 may move a tint). Its labels reach 4.95:1; its rings keep 3:1
        # on the slate and on the stage card's parchment.
        p = self.palette
        white = np.array((255, 255, 255), dtype=np.float64)
        self.assertEqual(tuple(int(v) for v in load("ui_chip_formula")[256, 256, :3]), (255, 255, 255))
        boss_label, boss_tint, boss_steps = label_on(white, np.array(hex_rgb(p["boss"]), dtype=np.float64), p)
        red_label, red_tint, _ = label_on(white, np.array(hex_rgb(p["red"]), dtype=np.float64), p)
        boss_plate, red_plate = tinted(white, boss_tint), tinted(white, red_tint)
        self.assertGreaterEqual(float(contrast(boss_label, boss_plate)), 4.95)
        self.assertLessEqual(boss_steps, 8, "the plate keeps the boss colour")
        for other in ("primary", "red", "pressure"):
            with self.subTest(against=other):
                self.assertGreaterEqual(de00(hex_rgb(p["boss"]), hex_rgb(p[other])), 15.0)
        self.assertGreaterEqual(de00(boss_plate, red_plate), 15.0)
        self.assertGreaterEqual(de00(boss_plate, hex_rgb(p["primary"])), 15.0)
        self.assertGreaterEqual(float(contrast(hex_rgb(p["boss"]), hex_rgb(p["paper"]))), 3.0)
        self.assertGreaterEqual(float(contrast(hex_rgb(p["boss"]), hex_rgb(p["felt"]))), 2.0)

    def test_button_secondary(self):
        # Coordinator (phase 1b review): amber glass AC8442 for every SolidButton11 face, set in theme.json since W20Y
        # landed (it was a pending key in the generator until the W20Y merge). Its label reaches 4.95:1 through LabelOn11
        # without moving the face; the face keeps 3:1 against the slate and the mosaic, the dark panels and the top bar
        # (both panel), and the page background. On the parchment it is 2.6:1 (known, reported); there the chip's black
        # came gives the boundary.
        p = self.palette
        self.assertEqual(p["buttonSecondary"], BUTTON_SECONDARY)
        face = load("ui_chip_formula")[256, 256, :3].astype(np.float64)
        tint = np.array(hex_rgb(BUTTON_SECONDARY), dtype=np.float64)
        fill = tinted(face, tint)
        for host in ("felt", "panel", "background"):
            with self.subTest(host=host):
                self.assertGreaterEqual(float(contrast(fill, hex_rgb(p[host]))), 3.0)
        label, drawn_tint, steps = label_on(face, tint, p, preferred=np.array(hex_rgb(p["textColor"]), dtype=np.float64))
        self.assertEqual(steps, 0)
        self.assertGreaterEqual(float(contrast(label, fill)), 4.95)
        came = load("ui_chip_formula")[256, :3, :3].astype(np.float64).min(axis=0)
        self.assertGreaterEqual(float(contrast(came, hex_rgb(p["paper"]))), 10.0)

    def test_a_disabled_secondary_button_looks_disabled(self):
        # Coordinator (lesson from the deco kit): SolidButton11 draws a disabled face a quarter toward panelLight, and its
        # label is LabelOn11(face, Muted) (W20Y2), which writes its steps into the face. W20Z2 (2026-11-19): judged as drawn,
        # after those steps - the disabled face differs from the enabled one by 5 CIEDE2000 or more, or it takes the other
        # label (until W20Z2 the test read the lerp face before LabelOn11, 5.38, while the face as drawn was 3.18 from the
        # enabled one with the same ink label). With panelLight 483A22 it does both, as deco 2b does: LabelOn11 deepens the
        # face 3 steps under the marble-white label instead of lightening it under ink. The branch is nowhere near flipping:
        # the deciding contrasts, unquantised as the C# Color holds them, stay 0.05 or more from 4.95, and lightening needs
        # as many steps or more (a tie deepens). panelLight's other uses keep their rules: the normal stage tag's label at
        # 4.95:1 without steps, the disabled UiKit.Button's Muted label at 3:1.
        p = self.palette
        rgb = lambda k: np.array(hex_rgb(p[k]), dtype=np.float64)
        white = np.array((255, 255, 255), dtype=np.float64)
        on = np.array(hex_rgb(BUTTON_SECONDARY), dtype=np.float64)
        off_start = lerp(on, rgb("panelLight"), .25)
        on_label, on_tint, _ = label_on(white, on, p, preferred=rgb("textColor"))
        off_label, off_tint, off_steps = label_on(white, off_start, p, preferred=rgb("muted"))
        on_face, off_face = tinted(white, on_tint), tinted(white, off_tint)
        distinct = de00(on_face, off_face) >= 5.0 or not np.array_equal(on_label, off_label)
        self.assertTrue(distinct, f"as drawn: dE00 {de00(on_face, off_face):.2f}, labels {on_label} / {off_label}")
        self.assertGreaterEqual(de00(on_face, off_face), 5.0)
        self.assertFalse(np.array_equal(on_label, off_label), "the disabled button takes the other label")
        self.assertTrue(np.array_equal(off_label, rgb("textColor")), "deepened under the marble-white label")
        self.assertGreaterEqual(float(contrast(off_label, off_face)), 4.95)

        def exact(tint):
            linear = channel(white) * channel(tint)
            return np.where(linear <= .0031308, linear * 12.92, 1.055 * np.power(linear, 1 / 2.4) - .055) * 255

        def trace(toward, label):
            t, out = off_start / 255.0, []
            for _ in range(12):
                out.append(float(contrast(label, exact(t * 255.0))))
                t = t + (np.asarray(toward, dtype=np.float64) - t) * .05
            return out
        deepen, lighten = trace((0, 0, 0), rgb("textColor")), trace((1, 1, 1), rgb("ink"))
        self.assertGreater(off_steps, 0)
        self.assertGreaterEqual(deepen[off_steps], 4.95 + .05)
        self.assertLessEqual(deepen[off_steps - 1], 4.95 - .05)
        self.assertLessEqual(lighten[off_steps - 1], 4.95 - .05, "lightening needs as many steps or more (a tie deepens)")
        tag_label, _, tag_steps = label_on(white, rgb("panelLight"), p)
        self.assertEqual(tag_steps, 0)
        self.assertGreaterEqual(float(contrast(tag_label, tinted(white, rgb("panelLight")))), 4.95)
        self.assertGreaterEqual(float(contrast(rgb("muted"), rgb("panelLight"))), 3.0)

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
        # THEME_SPEC 2 (W20X): gilt steps under the buttons (the glass tile on a gold base), each apart from its face and
        # visible on the slate; the section panel stays authored (section FFFFFF); parchment captions for the shop headers
        # and the tray tabs, whose labels LabelOn11 picks at 4.95:1 or better; the dice style page's Muted notes keep
        # 4.5:1 on the section cell's centre.
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
        self.assertEqual(set(self.theme), {"id", "displayName", "description", "order", "preview", "fonts", "palette"})
        self.assertEqual(self.theme["fonts"], {"title": "Fonts/title",
                          "roles": ["screenTitle", "stageNumber", "bigNumber", "bossName", "primaryButton", "sectionTitle", "cardName"]})
        self.assertTrue((ROOT / THEME / "Fonts/title.asset").is_file())
        self.assertEqual(self.theme["id"], "glass")
        self.assertEqual(self.theme["displayName"], "彩绘玻璃")
        self.assertEqual(self.theme["order"], 5)
        self.assertIs(self.theme["preview"], False)
        self.assertIn("ArtSource/StyleKit/themes/glass/concepts", self.theme["description"])
        source = (ROOT / "DicePrototype/Assets/Game/UI/Theme/ThemePalette.cs").read_text(encoding="utf-8")
        declared = re.search(r"class Hexes\s*\{\s*public string ([^;]+);", source).group(1)
        keys = {key.strip() for key in declared.replace("\n", " ").split(",")}
        # valueBadge (W13), the W20X keys and W20Y's buttonSecondary may be left out: unset, they fall back to today's
        # drawing (THEME_SPEC 2). The 24 required keys stayed the same when W20Y declared buttonSecondary (29 keys before,
        # 30 since the W20Y merge; coordinator, 2026-11-18); this kit sets it, so the declaration is required here.
        w20x = {"buttonStep", "buttonStepPrimary", "section", "caption"}
        w20y = {"buttonSecondary"}
        optional = {"valueBadge"} | w20x | w20y
        self.assertEqual(len(keys - optional), 24)
        self.assertTrue(w20x <= keys)
        self.assertTrue(w20y <= keys, "W20Y declares buttonSecondary in ThemePalette.Hexes")
        # Every palette key is one ThemePalette.Hexes declares (JsonUtility skips any other); the 24 required keys, the
        # four W20X keys and W20Y's buttonSecondary (set in theme.json since W20Y landed) are set; valueBadge falls back
        # to gold.
        self.assertLessEqual(set(self.palette), keys)
        self.assertLessEqual((keys - optional) | w20x | w20y, set(self.palette))
        self.assertNotIn("valueBadge", self.palette)
        for key, value in self.palette.items():
            with self.subTest(token=key):
                self.assertRegex(value, r"^[0-9A-F]{6}([0-9A-F]{2})?$")

    def test_shipped_theme_json_matches_regeneration(self):
        self.assertEqual((ROOT / THEME / "theme.json").read_bytes(), (self.first / THEME / "theme.json").read_bytes())

    def test_concept_pages_match_their_recorded_sources(self):
        table_md = (ROOT / "ArtSource/StyleKit/themes/README.md").read_text(encoding="utf-8")
        recorded = dict(re.findall(r"\| glass \| [^|]+ \| concepts/(\w+)\.png \| [^|]+ \| ([0-9a-f]{64}) \|", table_md))
        self.assertEqual(set(recorded), {"roll", "shop", "select", "card", "triptych"})
        for page, digest in recorded.items():
            with self.subTest(page=page):
                path = ROOT / "ArtSource/StyleKit/themes/glass/concepts" / (page + ".png")
                self.assertEqual(hashlib.sha256(path.read_bytes()).hexdigest(), digest)

    def test_wordmark_uses_the_approved_title_face(self):
        # Brief rule 7: the wordmark is set in Noto Serif SC Black, the approved glass title font (CP3-2).
        self.assertTrue((ROOT / APPROVED_TITLE_FONT).is_file())
        self.assertEqual(Path(self.builder.HEADING).resolve(), (ROOT / APPROVED_TITLE_FONT).resolve())
        source = (ROOT / "tools/art_v11/glass/build_glass.py").read_text(encoding="utf-8")
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
