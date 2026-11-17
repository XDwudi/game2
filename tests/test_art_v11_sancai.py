"""Independent asset-contract checks for the W20 sancai (唐三彩陶坊) UI kit."""

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
THEME = Path("DicePrototype/Assets/Resources/Themes/sancai")
REVIEW = Path("ArtSource/Art11/sancai/contact_sheet.png")
CONTRACT = json.loads((ROOT / "tools/art_v11/theme_contract.json").read_text(encoding="utf-8"))
SPRITES = [sprite for sprite in CONTRACT["sprites"] if sprite["referenced"]]
BY_NAME = {sprite["name"]: sprite for sprite in SPRITES}

# Written from the game's callers, independently of the generator: pieces the code multiplies by palette colours
# (UiKit.Button/FlatButton/Frame/Ring/Progress, Surface11 in GameApp.Play11*.cs, the shop and the NavIcons).
NEUTRAL_NAMES = frozenset({
    "ui_button_bevel", "ui_button_ghost", "ui_frame_block", "ui_frame_enchant",
    "ui_ring", "ui_price_chip", "ui_progress_fill", "ui_progress_track",
    "ui_section_header", "ui_pip_dot", "ui_icon_arrow", "ui_icon_book",
    "ui_icon_gear", "fx_arc_short", "fx_score_spark", "fx_square_chip",
    "ui_dice_tray", "ui_topbar", "ui_chip_formula", "ui_stage_card", "ui_icon_refresh",
})
# Tinted surfaces with an opaque face: the label pick reads centre pixel x tint, so the face must be pure white.
WHITE_FACES = ("ui_topbar", "ui_chip_formula", "ui_stage_card", "ui_button_bevel", "ui_price_chip", "ui_frame_block")
# Open frames: the game sets fillCenter=false or shows dice and cards through them.
HOLLOW = ("ui_ring", "ui_button_ghost", "ui_dice_tray", "ui_frame_enchant")
# Surfaces drawn far beyond their source size (top bar 1888 units, trays 1448, dialogs, shop sections).
STRETCHED_TINT = ("ui_topbar", "ui_dice_tray", "ui_chip_formula", "ui_stage_card", "ui_button_bevel",
                  "ui_button_ghost", "ui_ring", "ui_frame_block", "ui_frame_enchant", "ui_price_chip")
STRETCHED_AUTHORED = ("ui_paper_panel", "ui_panel_hud", "ui_section_panel", "ui_tab_bottom", "ui_info_inset",
                      "ui_slot_inventory", "ui_chip_label")
DARK_TEXT_SURFACES = ("ui_panel_hud", "ui_section_panel", "ui_tab_bottom", "ui_info_inset", "ui_slot_inventory",
                      "ui_tray_front")
PAPER_TEXT_SURFACES = ("ui_paper_panel", "ui_card_controller", "ui_card_controller_compact", "ui_card_item",
                       "ui_card_shop", "ui_chip_label")
SOFT_CORNERS = ("ui_paper_panel", "ui_panel_hud", "ui_card_controller", "ui_card_shop", "ui_chip_formula",
                "ui_button_bevel", "ui_topbar", "ui_stage_card", "ui_ring", "ui_slot_inventory", "ui_dice_tray")
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


class SancaiArtTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        spec = importlib.util.spec_from_file_location(
            "sancai_asset_builder", ROOT / "tools/art_v11/sancai/build_sancai.py")
        cls.builder = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(cls.builder)
        scratch_parent = ROOT / "ArtSource/Art11/sancai"
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
        self.assertEqual(len(SPRITES), 49, "Review W20 scope if the input contract changes.")
        expected = {sprite["name"] for sprite in SPRITES}
        self.assertEqual({path.stem for path in (ROOT / THEME / "UI").glob("*.png")}, expected)
        expected_paths = {(THEME / "UI" / (name + ".png")).as_posix() for name in expected}
        expected_paths.update({(THEME / "Table/table_scene.png").as_posix(),
                               (THEME / "Table/felt_tile.png").as_posix(),
                               (THEME / "preview.png").as_posix(), REVIEW.as_posix()})
        self.assertEqual(set(self.first_hashes), expected_paths)
        self.assertEqual(len(self.first_hashes), 53)
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
        self.assertEqual(checked, 21)

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
        # corner squares (that is the part Unity stretches).
        return [("top", pixels[:top, left:w - right]), ("bottom", pixels[h - bottom:, left:w - right]),
                ("left", pixels[top:h - bottom, :left].transpose(1, 0, 2)),
                ("right", pixels[top:h - bottom, w - right:].transpose(1, 0, 2))]

    def test_stretched_tint_surfaces_have_edge_bands_invariant_along_the_edge(self):
        for name in STRETCHED_TINT:
            for side, band in self._edge_bands(name):
                with self.subTest(sprite=name, side=side):
                    np.testing.assert_array_equal(band, np.broadcast_to(band[:, :1], band.shape))

    def test_stretched_authored_surfaces_keep_only_soft_glaze_runs_on_their_edges(self):
        # Crackle, streaks and ornaments belong to the corner squares; the stretched middle may carry only long,
        # soft runs of the second glaze that stay constant across the rim.
        for name in STRETCHED_AUTHORED:
            for side, band in self._edge_bands(name):
                with self.subTest(sprite=name, side=side):
                    step = np.abs(np.diff(band, axis=1)).max()
                    self.assertLessEqual(int(step), 10, "A crisp detail in the stretched middle would smear.")

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

    def test_corners_are_circular_arcs_not_steps_or_cuts(self):
        """The style's corners are soft round corners or concave 'haitang' notches: either way one circular arc.
        The default's stepped corners and Swiss's straight cuts do not fit a small circle."""
        def fit(points):
            a = np.c_[points[:, 0], points[:, 1], np.ones(len(points))]
            d, e, f = np.linalg.lstsq(a, -(points[:, 0] ** 2 + points[:, 1] ** 2), rcond=None)[0]
            cx, cy = -d / 2, -e / 2
            radius = math.sqrt(max(cx * cx + cy * cy - f, 0.0))
            return cx, cy, radius, np.abs(np.hypot(points[:, 0] - cx, points[:, 1] - cy) - radius)

        for name in SOFT_CORNERS:
            with self.subTest(sprite=name):
                alpha = load(name)[..., 3].astype(float) / 255.0
                self.assertEqual(float(alpha[0, 0]), 0.0, "The corner vertex itself is open.")
                corner = alpha[:40, :40]
                points = []
                # The outer silhouette only (open frames also have an inner edge): the first sub-pixel crossing of
                # 50 % coverage along each row from the left and along each column from the top.
                for y in range(40):
                    for x in range(39):
                        a, b = corner[y, x], corner[y, x + 1]
                        if (a - .5) * (b - .5) < 0:
                            points.append((x + .5 + (.5 - a) / (b - a), y + .5))
                            break
                for x in range(40):
                    for y in range(39):
                        a, b = corner[y, x], corner[y + 1, x]
                        if (a - .5) * (b - .5) < 0:
                            points.append((x + .5, y + .5 + (.5 - a) / (b - a)))
                            break
                points = np.array([p for p in points if p[0] > 3 and p[1] > 3])
                self.assertGreater(len(points), 12)
                cx, cy, radius, residual = fit(points)
                keep = residual <= np.percentile(residual, 70)       # drop the small tip roundings of a notch
                cx, cy, radius, residual = fit(points[keep])
                self.assertGreaterEqual(radius, 6.0)
                self.assertLessEqual(radius, 60.0, "A straight cut fits no small circle.")
                self.assertLessEqual(float(residual.max()), 1.0, "The corner must be one smooth arc, not steps.")

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
        # The glazed frame lives in the outer margin: the edges are decorated, the field is plain felt.
        edge = np.concatenate([pixels[:, :12].reshape(-1, 3), pixels[:, -12:].reshape(-1, 3)])
        self.assertGreater(float((np.abs(edge.astype(int) - felt).sum(axis=1) > 30).mean()), .5)

    def test_felt_tile_is_seamless_and_even(self):
        with Image.open(ROOT / THEME / "Table/felt_tile.png") as image:
            self.assertEqual((image.size, image.mode), ((1024, 1024), "RGB"))
            px = np.asarray(image).astype(np.float64)
        felt = np.array(hex_rgb(self.palette["felt"]), dtype=np.float64)
        self.assertLessEqual(float(np.abs(px.reshape(-1, 3).mean(axis=0) - felt).max()), 2.0)
        # Wrapping costs no more than an ordinary neighbouring step (no seam), in both directions.
        inner_rows = np.abs(np.diff(px, axis=0)).mean()
        inner_cols = np.abs(np.diff(px, axis=1)).mean()
        self.assertLessEqual(float(np.abs(px[0] - px[-1]).mean()), inner_rows * 1.25)
        self.assertLessEqual(float(np.abs(px[:, 0] - px[:, -1]).mean()), inner_cols * 1.25)
        # No lighting gradient: the four quadrants have the same mean within 1.5 levels.
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
        hue, sat, val = colorsys.rgb_to_hsv(*(v / 255 for v in p["pressure"]))
        self.assertGreaterEqual(sat, .5, "Pressure must be a warning colour, not a neutral.")
        self.assertTrue(hue * 360 <= 40 or hue * 360 >= 340, "Pressure must sit in the red-orange warning range.")
        rarity = [p["rarityCommon"], p["rarityAdvanced"], p["rarityRare"]]
        for i in range(3):
            for j in range(i + 1, 3):
                with self.subTest(pair=(i, j)):
                    self.assertGreaterEqual(float(contrast(rarity[i], rarity[j])), 1.4, "Rarity must differ in lightness.")
        for i, colour in enumerate(rarity):
            with self.subTest(rarity=i):
                self.assertGreaterEqual(float(np.linalg.norm(lab(p["primary"]) - lab(colour))), 40.0,
                                        "The primary action must not share a rarity colour.")

    def test_theme_json_schema(self):
        self.assertEqual(set(self.theme), {"id", "displayName", "description", "order", "preview", "palette"})
        self.assertEqual(self.theme["id"], "sancai")
        self.assertEqual(self.theme["displayName"], "唐三彩陶坊")
        self.assertEqual(self.theme["order"], 2)
        self.assertIs(self.theme["preview"], True)
        self.assertIn("ArtSource/StyleKit/themes/sancai/concepts", self.theme["description"])
        source = (ROOT / "DicePrototype/Assets/Game/UI/Theme/ThemePalette.cs").read_text(encoding="utf-8")
        declared = re.search(r"class Hexes\s*\{\s*public string ([^;]+);", source).group(1)
        keys = {key.strip() for key in declared.replace("\n", " ").split(",")}
        self.assertEqual(len(keys), 24)
        self.assertEqual(set(self.palette), keys)
        for key, value in self.palette.items():
            with self.subTest(token=key):
                self.assertRegex(value, r"^[0-9A-F]{6}([0-9A-F]{2})?$")

    def test_shipped_theme_json_matches_regeneration(self):
        self.assertEqual((ROOT / THEME / "theme.json").read_bytes(), (self.first / THEME / "theme.json").read_bytes())

    def test_concept_pages_match_their_recorded_sources(self):
        table = (ROOT / "ArtSource/StyleKit/themes/README.md").read_text(encoding="utf-8")
        recorded = dict(re.findall(r"\| sancai \| [^|]+ \| concepts/(\w+)\.png \| [^|]+ \| ([0-9a-f]{64}) \|", table))
        self.assertEqual(set(recorded), {"roll", "shop", "select", "card", "triptych"})
        for page, digest in recorded.items():
            with self.subTest(page=page):
                path = ROOT / "ArtSource/StyleKit/themes/sancai/concepts" / (page + ".png")
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
