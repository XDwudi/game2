"""W20: deterministic 爆彩连环漫画 (comic) UI kit: flat pop-art comic surfaces, 4x raster, one Lanczos downsample.

Style authority: the five user-chosen concept pages in ArtSource/StyleKit/themes/comic/concepts and the palette samples
of ArtSource/StyleKit/themes/comic/style_kit.json. Their texts, numbers and layouts are not a contract; the game layout
is (docs/specs/v11/PLAY_LAYOUT.md). The contract (tools/art_v11/theme_contract.json) owns pixel sizes, nine-slice
borders and PPU. Repository fonts only; no image generation, no Unity files.

The shape language (rework round 2, coordinator 2026-11-17, after comparing the composite with concepts/roll.png):
- heavy ink outlines (10 px = 5 units on panels, cards, buttons, slots and trays; 8 px on chips) and flat fills;
- a hard ink shadow down and to the right under panels, cards, buttons and chips;
- big 45-degree corner cuts that use the whole 40 px corner square: octagonal panels, cards, slots and secondary
  buttons; the button face (the primary) leans forward with big top-left and bottom-right cuts; the code's step under
  UiKit.Button is the slab (a theme token for its colour is coming; ink for comic);
- comic-panel frames on the dark panels: ink line, a cream gutter band, an ink line, then the fill;
- the shop's section header is a parallelogram caption with full-height slanted ends (it only slices horizontally);
- the table is the concept's red Ben-Day page; the calm zones and the trays' floors are flat blue felt, so the gaps
  between them read as red comic gutters; one whole starburst sits in the action row's always-free rectangle (round 3,
  THEME_SPEC 3.1: no decoration under the changing card row, none cut straight, none inside a button's or panel's
  bounding rectangle); Table/page_scene.png is the plain page for the non-play screens (W20X).
Halftone appears only where its dots are at least about 3 px at 1080p (the page and large decoration); fills under
text are flat.

Rules the art keeps (checked by tests/test_art_v11_comic.py):
- every nine-slice centre is one flat colour (or transparent) from the border line inward;
- surfaces the game stretches keep their edge bands invariant along the edge: every cut, band and shadow corner ends
  inside the corner squares (the budget: cut + shadow + 0.414 x frame thickness <= 37 px of the 40 px square);
- sprites the code multiplies by a palette colour (NEUTRAL_NAMES) stay achromatic: white faces, black ink and shadows;
- the protected table zones are exactly the felt colour;
- the W13 card template: a rarity ring no wider than the default's, cut to sit inside the card face's cream, and three
  gems whose shapes (round, diamond, six-point star) tell the rarity apart without colour.

`build_comic.py --card-review` writes ArtSource/Art11/comic/card_template_review.png from the shipped comic and Swiss
sprites (an offline review, not part of the build).
"""
from __future__ import annotations

import colorsys
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[3]
CONTRACT = ROOT / "tools/art_v11/theme_contract.json"
THEME = Path("DicePrototype/Assets/Resources/Themes/comic")
REVIEW = Path("ArtSource/Art11/comic")
CONCEPTS = Path("ArtSource/StyleKit/themes/comic/concepts")
CONCEPT_PAGES = ("roll", "shop", "select", "card", "triptych")
FONT = ROOT / "DicePrototype/Assets/Game/Fonts/NotoSansSC-Bold.otf"
BODY_FONT = ROOT / "DicePrototype/Assets/Game/Fonts/NotoSansCJKsc-Regular.otf"
HEADING = ROOT / "DicePrototype/Assets/Game/Fonts/Themes/comic/ZCOOLKuaiLe-Regular.ttf"
LATIN = ROOT / "DicePrototype/Assets/TextMesh Pro/Fonts/LiberationSans.ttf"
SS = 4
SEED = 20261117

# ---------------------------------------------------------------------------------------------- palette
# theme.json palette (THEME_SPEC section 2). The generator is the single source: build() writes theme.json from it,
# and the table felt is exactly PALETTE["felt"]. Reasons and contrast numbers: tools/art_v11/comic/README.md.
PALETTE = {
    "background": "021A44",   # deep navy page
    "felt": "0250C6",         # process blue (the concept's dice tray): the table, flat in every calm zone
    "panel": "032F7A",        # navy: top bar, dark panels, secondary chips (light text on it)
    "panelLight": "0D56CC",   # blue: dividers, the normal-stage tag, tray rim mix
    "paper": "F9F1D9",        # newsprint cream: cards, paper panels, button faces
    "ink": "0A080A",          # ink black: text on cream and every outline
    "textColor": "FFF8E4",    # light cream: text on navy
    "muted": "A9C6F0",        # pale blue: secondary text on navy
    "gold": "FBDC0F",         # comic yellow: prices, coins, highlights
    "teal": "33C25A",         # pop green: done, positive
    # red and blue also colour the 3D dice pips (DiceTable reads UiKit.Red / UiKit.Blue). Pip colour is the game's main
    # functional cue, so every style keeps the default pair (coordinator, 2026-11-17, CP3-1; THEME_SPEC section 2).
    "red": "EC655D",
    "blue": "55B6D9",
    "violet": "9D4EDD",       # pop purple: enchantments (as in the default palette, violet is also the rare colour)
    "danger": "A80F1A",       # deep red: errors, boss warnings
    "shadow": "0A080A",       # ink: the hard drop shadows
    "ghost": "7FCFF5",        # light cyan: outline buttons, empty slot rings, the goods' card ring
    "primary": "FBDC0F",      # comic yellow (= gold, as in the default): the one main action per phase, label ink
    "reward": "FFB21A",       # amber yellow: reward moments
    "boss": "C8102E",         # crimson
    "pressure": "F26B1D",     # comic orange: pressure stages (a warning hue, far from the yellow main button)
    "rarityCommon": "C8CBD0", # newsprint grey
    "rarityAdvanced": "0250C6",  # process blue
    "rarityRare": "9D4EDD",   # pop purple
    "onFelt": "F9F1D9",       # cream marks drawn straight on the blue felt
}
# THEME_SPEC 2 W20X keys (coordinator 2026-11-17; code task W20X, after W14). Until W20X lands the game ignores them:
# GameTheme reads theme.json with JsonUtility into ThemePalette.Hexes, which skips unknown fields.
W20X_TOKENS = {
    "buttonStep": "0A080A",         # the slab under UiKit.Button is ink, as on the concept's buttons
    "buttonStepPrimary": "0A080A",
    "section": "FFFFFF",            # ui_section_panel stays authored (the concept's blue section with its cream gutter)
    "caption": "FBDC0F",            # the concept's yellow captions: shop section headers and the dice-pool tab
}
# The table's red Ben-Day page (art, not a palette token), measured on concepts/roll.png's clean gutters.
PAGE_HEX, PAGE_DOT_HEX = "FC2830", "260306"
THEME_META = {
    "id": "comic",
    "displayName": "爆彩连环漫画",
    "description": ("红色网点漫画页上的蓝色骰池：粗黑墨线、大切角的面板与按钮带右下硬投影与墨色厚底，主按钮前倾，"
                    "黄色平行四边形标题条，主按钮旁一个爆炸星，字下面一律平涂。依据：ArtSource/StyleKit/themes/comic/concepts 与 "
                    "style_kit.json；界面件与色板由 tools/art_v11/comic/build_comic.py 生成（说明见同目录 README.md）"),
    "order": 3,
    "preview": False,
    "fonts": {"title": "Fonts/title", "roles": ["screenTitle", "stageNumber", "bigNumber", "bossName", "primaryButton", "sectionTitle"]},
}


def rgb8(value: str) -> tuple[int, int, int]:
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def col(value: str) -> np.ndarray:
    return np.array(rgb8(value), np.float32) / 255.0


def _lin(c: float) -> float:
    c /= 255.0
    return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4


def _gam(c: float) -> float:
    c = max(c, 0.0)
    return c * 12.92 if c <= .0031308 else 1.055 * c ** (1 / 2.4) - .055


def luminance(value: str) -> float:
    r, g, b = rgb8(value)
    return .2126 * _lin(r) + .7152 * _lin(g) + .0722 * _lin(b)


def contrast(a: str, b: str) -> float:
    x, y = luminance(a), luminance(b)
    return (max(x, y) + .05) / (min(x, y) + .05)


def lab(value: str) -> tuple[float, float, float]:
    r, g, b = (_lin(v) for v in rgb8(value))
    x = (.4124 * r + .3576 * g + .1805 * b) / .95047
    y = .2126 * r + .7152 * g + .0722 * b
    z = (.0193 * r + .1192 * g + .9505 * b) / 1.08883
    f = lambda t: t ** (1 / 3) if t > 216 / 24389 else (24389 / 27 * t + 16) / 116
    return 116 * f(y) - 16, 500 * (f(x) - f(y)), 200 * (f(y) - f(z))


def delta_e(a: str, b: str) -> float:
    return math.dist(lab(a), lab(b))


def ciede2000(lab1, lab2) -> float:
    """CIEDE2000 colour difference (Sharma, Wu and Dalal 2005; kL = kC = kH = 1)."""
    L1, a1, b1 = lab1
    L2, a2, b2 = lab2
    c7 = ((math.hypot(a1, b1) + math.hypot(a2, b2)) / 2) ** 7
    g = .5 * (1 - math.sqrt(c7 / (c7 + 25 ** 7)))
    a1, a2 = a1 * (1 + g), a2 * (1 + g)
    C1, C2 = math.hypot(a1, b1), math.hypot(a2, b2)
    h1 = math.degrees(math.atan2(b1, a1)) % 360 if C1 else 0.0
    h2 = math.degrees(math.atan2(b2, a2)) % 360 if C2 else 0.0
    dh = 0.0 if C1 * C2 == 0 else (h2 - h1 + 180) % 360 - 180
    dL, dC, dH = L2 - L1, C2 - C1, 2 * math.sqrt(C1 * C2) * math.sin(math.radians(dh / 2))
    L, C = (L1 + L2) / 2, (C1 + C2) / 2
    if C1 * C2 == 0:
        h = h1 + h2
    elif abs(h1 - h2) <= 180:
        h = (h1 + h2) / 2
    else:
        h = (h1 + h2 + 360) / 2 if h1 + h2 < 360 else (h1 + h2 - 360) / 2
    t = (1 - .17 * math.cos(math.radians(h - 30)) + .24 * math.cos(math.radians(2 * h))
         + .32 * math.cos(math.radians(3 * h + 6)) - .2 * math.cos(math.radians(4 * h - 63)))
    sl = 1 + .015 * (L - 50) ** 2 / math.sqrt(20 + (L - 50) ** 2)
    sc, sh = 1 + .045 * C, 1 + .015 * C * t
    rt = -math.sin(math.radians(60 * math.exp(-((h - 275) / 25) ** 2))) * 2 * math.sqrt(C ** 7 / (C ** 7 + 25 ** 7))
    return math.sqrt((dL / sl) ** 2 + (dC / sc) ** 2 + (dH / sh) ** 2 + rt * (dC / sc) * (dH / sh))


def de00(a: str, b: str) -> float:
    return ciede2000(lab(a), lab(b))


def drawn(fill: str, tint: str) -> str:
    """A sprite pixel tinted as Unity UI draws it in this linear-colour-space project (GameApp.RenderedFill11)."""
    return "".join(f"{int(round(_gam(_lin(f) * _lin(t)) * 255)):02X}" for f, t in zip(rgb8(fill), rgb8(tint)))


def best_label(fill: str, palette: dict) -> tuple[str, float]:
    """GameApp.BestLabel11: of ink, paper and textColor, the label with the highest contrast on a drawn fill."""
    key = max(("ink", "paper", "textColor"), key=lambda k: contrast(palette[k], fill))
    return key, contrast(palette[key], fill)


def hexc(c) -> str:
    return "".join(f"{int(round(float(v) * 255)):02X}" for v in c)


def palette_report(palette: dict | None = None) -> dict[str, float]:
    """Contrast numbers quoted in the README and asserted by the tests (WCAG 2 sRGB relative luminance)."""
    p = palette or PALETTE
    chip = hexc(LABEL_FILL)
    value_fill, warning_fill = drawn(chip, p.get("valueBadge", p["gold"])), drawn(chip, p["danger"])
    out = {
        "textColor/panel": contrast(p["textColor"], p["panel"]),
        "ink/paper": contrast(p["ink"], p["paper"]),
        "muted/panel": contrast(p["muted"], p["panel"]),
        "onFelt/felt": contrast(p["onFelt"], p["felt"]),
        "textColor/felt": contrast(p["textColor"], p["felt"]),
        f"{best_label(p['primary'], p)[0]}/primary (label pick)": best_label(p["primary"], p)[1],
        f"{best_label(p['panelLight'], p)[0]}/panelLight (label pick)": best_label(p["panelLight"], p)[1],
        f"{best_label(p['boss'], p)[0]}/boss (label pick)": best_label(p["boss"], p)[1],
        f"{best_label(p['pressure'], p)[0]}/pressure (label pick)": best_label(p["pressure"], p)[1],
        "gold/panel": contrast(p["gold"], p["panel"]),
        "teal/panel": contrast(p["teal"], p["panel"]),
        "danger/paper": contrast(p["danger"], p["paper"]),
        "ghost/felt": contrast(p["ghost"], p["felt"]),
        "valueBadge/danger badge dE00 (drawn)": ciede2000(lab(value_fill), lab(warning_fill)),
        "valueBadge/danger badge contrast (drawn)": contrast(value_fill, warning_fill),
        f"{best_label(value_fill, p)[0]}/valueBadge (drawn)": best_label(value_fill, p)[1],
        f"{best_label(warning_fill, p)[0]}/danger badge (drawn)": best_label(warning_fill, p)[1],
        "primary/pressure dE00": de00(p["primary"], p["pressure"]),
        "pressure/boss dE00": de00(p["pressure"], p["boss"]),
    }
    rar = ("rarityCommon", "rarityAdvanced", "rarityRare")
    for i, a in enumerate(rar):
        for b in rar[i + 1:]:
            out[f"{a}/{b}"] = contrast(p[a], p[b])
        out[f"primary/{a} dE"] = delta_e(p["primary"], p[a])
        out[f"primary/{a} dE00"] = de00(p["primary"], p[a])
    out["pressure saturation"] = colorsys.rgb_to_hsv(*(v / 255 for v in rgb8(p["pressure"])))[1]
    # The red Ben-Day page (table art, not a token): what a word drawn straight on it would get, dots included.
    for key in ("textColor", "onFelt", "ink", "gold"):
        out[f"{key}/page red"] = contrast(p[key], PAGE_HEX)
        out[f"{key}/page dots"] = contrast(p[key], PAGE_DOT_HEX)
    # W20X tokens: the shop section and the dice style page's cells (panel centre x section), the yellow captions (white
    # strip or tab x caption, label picked by LabelOn11).
    w = dict(W20X_TOKENS, **{k: v for k, v in p.items() if k in W20X_TOKENS})
    cell = drawn(SECTION_HEX, w["section"])
    out["muted/section cell"] = contrast(p["muted"], cell)
    out["textColor/section cell"] = contrast(p["textColor"], cell)
    caption = drawn("FFFFFF", w["caption"])
    out[f"{best_label(caption, p)[0]}/caption (label pick)"] = best_label(caption, p)[1]
    return out


# Comic colours (sRGB floats). Style kit samples (style_kit.json) plus the shades the art needs.
INK = col(PALETTE["ink"])
CREAM = col(PALETTE["paper"])
YELLOW = col("FBDC0F")
YELLOW_SH = col("E3B30B")     # cel shade of yellow
AMBER = col("E39F0C")
RED = col("F12022")
RED_SH = col("B5121A")        # cel shade / dots on red
BLUE = col("0250C6")
BLUE_LT = col("2E73E2")
BLUE_DOT = col("013C9A")      # dots on the blue felt
NAVY = col("032F7A")
NAVY_DK = col("021F55")
CYAN = col("28B1F1")
CYAN_LT = col(PALETTE["ghost"])
GREEN = col("33C25A")
PURPLE = col("9D4EDD")
GREY = col("C8CBD0")
WHITE = np.ones(3, np.float32)
K = np.zeros(3, np.float32)   # pure black for tinted (achromatic) sprites

# Sprites the game multiplies by palette colours: they stay achromatic (white face, black ink and shadows).
NEUTRAL_NAMES = frozenset({
    "ui_button_bevel", "ui_button_ghost", "ui_frame_block", "ui_frame_enchant",
    "ui_ring", "ui_price_chip", "ui_progress_fill", "ui_progress_track",
    "ui_section_header", "ui_pip_dot", "ui_icon_arrow", "ui_icon_book",
    "ui_icon_gear", "fx_arc_short", "fx_score_spark", "fx_square_chip",
    "ui_dice_tray", "ui_topbar", "ui_chip_formula", "ui_stage_card", "ui_icon_refresh",
    # W13 card template (CARD_TEMPLATE 6): tinted by the rarity colours, or Ghost for goods (GameApp.Card11 TintedSprite13).
    "ui_card_frame_rarity", "ui_gem_common", "ui_gem_advanced", "ui_gem_rare",
})
CARD_TEMPLATE_NAMES = ("ui_card_frame_rarity", "ui_gem_common", "ui_gem_advanced", "ui_gem_rare")
# Calm zones of the play screen (PLAY_LAYOUT 1 and 8, W12b): one flat felt colour, no texture.
FELT_ZONES = {
    "dice_area": (52, 374, 1408, 276),
    "formula_row": (52, 660, 1408, 60),
    "submission_rail": (344, 740, 1136, 202),
    "submit_info_text150": (32, 740, 360, 202),
}
MAX_FELT_RGB_DEVIATION = 0


# ---------------------------------------------------------------------------------------------- raster
def grid(w: int, h: int, x0: float = 0.0, y0: float = 0.0):
    xs = ((np.arange(w * SS, dtype=np.float64) + .5) / SS + x0).astype(np.float32)
    ys = ((np.arange(h * SS, dtype=np.float64) + .5) / SS + y0).astype(np.float32)
    return np.meshgrid(xs, ys)


def coverage(d):
    """Antialiased silhouette at the supersampled density (d in output pixels, negative inside)."""
    return np.clip(.5 - d * SS, 0.0, 1.0).astype(np.float32)


class Raster:
    """Premultiplied RGBA at SS x the output size; coordinates are output pixels."""

    def __init__(self, w: int, h: int, x0: float = 0.0, y0: float = 0.0, _views=None):
        self.w, self.h = w, h
        if _views is not None:
            self.X, self.Y, self.rgb, self.a = _views
            return
        self.X, self.Y = grid(w, h, x0, y0)
        self.rgb = np.zeros((h * SS, w * SS, 3), np.float32)
        self.a = np.zeros((h * SS, w * SS), np.float32)

    def window(self, x0: int, y0: int, w: int, h: int) -> "Raster":
        x0, y0 = max(0, int(x0)), max(0, int(y0))
        w, h = min(int(w), self.w - x0), min(int(h), self.h - y0)
        ys, xs = slice(y0 * SS, (y0 + h) * SS), slice(x0 * SS, (x0 + w) * SS)
        return Raster(w, h, _views=(self.X[ys, xs], self.Y[ys, xs], self.rgb[ys, xs], self.a[ys, xs]))

    def over(self, rgb, alpha):
        a = np.clip(alpha, 0, 1).astype(np.float32)
        rgb = np.asarray(rgb, np.float32)
        if rgb.ndim == 1:
            rgb = np.broadcast_to(rgb, self.rgb.shape)
        self.rgb[...] = rgb * a[..., None] + self.rgb * (1 - a[..., None])
        self.a[...] = a + self.a * (1 - a)

    @property
    def origin(self) -> tuple[int, int]:
        return int(round(float(self.X[0, 0]) - .5 / SS)), int(round(float(self.Y[0, 0]) - .5 / SS))

    def mask(self, draw_fn) -> np.ndarray:
        """Binary SS mask in this raster's frame; draw_fn(d, s) gets an ImageDraw and a point scaler."""
        ox = float(self.X[0, 0]) - .5 / SS
        oy = float(self.Y[0, 0]) - .5 / SS
        im = Image.new("L", (self.w * SS, self.h * SS), 0)
        d = ImageDraw.Draw(im)
        draw_fn(d, lambda pts: [((x - ox) * SS, (y - oy) * SS) for x, y in pts])
        return np.asarray(im, np.float32) / 255.0

    def export(self) -> Image.Image:
        return downsample(self.rgb, self.a, self.w, self.h)


def _resize_f(channel: np.ndarray, w: int, h: int) -> np.ndarray:
    return np.asarray(Image.fromarray(np.ascontiguousarray(channel, np.float32), "F").resize((w, h), Image.Resampling.LANCZOS))


def downsample(prgb: np.ndarray, alpha: np.ndarray, w: int, h: int) -> Image.Image:
    """One Lanczos pass on premultiplied float channels, then unpremultiply and quantise."""
    a = np.clip(_resize_f(alpha, w, h), 0, 1)
    a8 = np.rint(a * 255)
    out = np.zeros((h, w, 4), np.uint8)
    safe = np.maximum(a, 1e-6)
    for c in range(3):
        p = _resize_f(prgb[..., c], w, h)
        out[..., c] = np.where(a8 > 0, np.clip(np.rint(p / safe * 255), 0, 255), 0)
    out[..., 3] = a8
    return Image.fromarray(out, "RGBA")


def inside_distance(mask: np.ndarray) -> np.ndarray:
    return (ndimage.distance_transform_edt(mask > .5) / SS).astype(np.float32)


def outside_distance(mask: np.ndarray) -> np.ndarray:
    return (ndimage.distance_transform_edt(mask <= .5) / SS).astype(np.float32)


def shift(mask: np.ndarray, dx: float, dy: float) -> np.ndarray:
    """A mask moved by (dx, dy) output pixels, zero filled."""
    sx, sy = int(round(dx * SS)), int(round(dy * SS))
    out = np.zeros_like(mask)
    h, w = mask.shape
    out[max(0, sy):h + min(0, sy), max(0, sx):w + min(0, sx)] = mask[max(0, -sy):h - max(0, sy), max(0, -sx):w - max(0, sx)]
    return out


def mix(a, b, t):
    t = np.asarray(t, np.float32)
    return a + (b - a) * t[..., None]


def smoothstep(a: float, b: float, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


# ---------------------------------------------------------------------------------------------- geometry
def sd_box(X, Y, box):
    """Exact signed distance to an axis-aligned rectangle (negative inside)."""
    x0, y0, x1, y1 = box
    qx = np.abs(X - np.float32((x0 + x1) / 2)) - np.float32((x1 - x0) / 2)
    qy = np.abs(Y - np.float32((y0 + y1) / 2)) - np.float32((y1 - y0) / 2)
    return (np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0)).astype(np.float32)


def sd_circle(X, Y, cx, cy, r):
    return (np.hypot(X - np.float32(cx), Y - np.float32(cy)) - np.float32(r)).astype(np.float32)


def sd_poly(X, Y, pts) -> np.ndarray:
    """Exact signed distance to a closed polygon (negative inside; even-odd inside test)."""
    Xd, Yd = X.astype(np.float64), Y.astype(np.float64)
    best = np.full(X.shape, np.inf)
    inside = np.zeros(X.shape, bool)
    pts = list(pts)
    for (ax, ay), (bx, by) in zip(pts, pts[1:] + pts[:1]):
        ex, ey = bx - ax, by - ay
        wx, wy = Xd - ax, Yd - ay
        t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1)
        best = np.minimum(best, (wx - ex * t) ** 2 + (wy - ey * t) ** 2)
        if ay != by:
            inside ^= ((ay > Yd) != (by > Yd)) & (Xd < ax + ex * (Yd - ay) / ey)
    d = np.sqrt(best)
    return np.where(inside, -d, d).astype(np.float32)


def sheared(box, cut_w: float, cut_h: float):
    """A "/" lean made by cutting the top-left and bottom-right corners on a steep slant (unsliced art only)."""
    x0, y0, x1, y1 = box
    return [(x0 + cut_w, y0), (x1, y0), (x1, y1 - cut_h), (x1 - cut_w, y1), (x0, y1), (x0, y0 + cut_h)]


def octagon(box, cuts):
    """A box with 45-degree corner cuts (top-left, top-right, bottom-right, bottom-left), in px; 0 keeps a corner."""
    x0, y0, x1, y1 = box
    tl, tr, br, bl = cuts
    pts = [(x0, y0 + tl), (x0 + tl, y0)] if tl else [(x0, y0)]
    pts += [(x1 - tr, y0), (x1, y0 + tr)] if tr else [(x1, y0)]
    pts += [(x1, y1 - br), (x1 - br, y1)] if br else [(x1, y1)]
    pts += [(x0 + bl, y1), (x0, y1 - bl)] if bl else [(x0, y1)]
    return pts


def corner_reach(w: float, h: float, box, cuts, shadow: float, frame: float, hollow: bool) -> float:
    """How far into its corner square (px) the furthest corner feature of a comic tile reaches, measured from the sprite
    edges: a cut's inner contour meets the inner edge line 0.414 x frame beyond the cut; the shadow is the face moved
    (shadow, shadow), and an open frame's shadow shows its inner contour too."""
    x0, y0, x1, y1 = box
    tl, tr, br, bl = cuts
    lean = (math.sqrt(2) - 1) * frame
    left, top, right, bottom = x0, y0, w - x1, h - y1
    reach = [left + tl + lean, top + tl + lean,                        # top-left
             right + tr + lean, top + tr + lean, top + shadow + tr,    # top-right (face, shadow silhouette)
             right + br + lean, bottom + br + lean,                    # bottom-right
             left + bl + lean, bottom + bl + lean, left + shadow + bl,  # bottom-left
             right + frame, bottom + frame, left + frame, top + frame]
    if hollow and shadow:
        reach += [left + shadow + tl + lean, top + shadow + tl + lean, top + shadow + tr + lean, left + shadow + bl + lean]
    return max(reach)


def star_pts(cx, cy, ro, ri, n, rot=-math.pi / 2):
    return [(cx + math.cos(rot + math.pi * i / n) * (ro if i % 2 == 0 else ri),
             cy + math.sin(rot + math.pi * i / n) * (ro if i % 2 == 0 else ri)) for i in range(2 * n)]


def burst_pts(cx, cy, ro, ri, n, seed, jitter=.18, rot=-math.pi / 2):
    """A comic 'POW' burst: spikes of uneven length and spacing."""
    rng = np.random.RandomState(seed)
    pts = []
    for i in range(2 * n):
        a = rot + math.pi * i / n + rng.uniform(-.25, .25) * math.pi / n
        rr = (ro * (1 - jitter * rng.uniform(0, 1))) if i % 2 == 0 else ri * (1 - .5 * jitter * rng.uniform(0, 1))
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    return pts


def halftone(X, Y, spacing: float, radius, angle: float = 45.0):
    """Ben-Day dot screen: coverage of dots on a lattice of `spacing` px turned by `angle` degrees, radius per sample."""
    a = math.radians(angle)
    u = (X * np.float32(math.cos(a)) + Y * np.float32(math.sin(a))) / np.float32(spacing)
    v = (-X * np.float32(math.sin(a)) + Y * np.float32(math.cos(a))) / np.float32(spacing)
    dist = np.hypot(u - np.round(u), v - np.round(v)) * np.float32(spacing)
    return coverage(dist - np.asarray(radius, np.float32))


def halftone_whole(r: "Raster", mask: np.ndarray, spacing: float, radius: float) -> np.ndarray:
    """Ben-Day dots on a square lattice, only those whose whole disc lies inside `mask` (no slivers at its edges)."""
    X, Y = r.X, r.Y
    cx = np.round(X / np.float32(spacing)) * np.float32(spacing)
    cy = np.round(Y / np.float32(spacing)) * np.float32(spacing)
    inside = inside_distance(mask) >= radius + .5
    ox, oy = r.origin
    ix = np.clip(np.floor((cx - ox) * SS).astype(int), 0, X.shape[1] - 1)
    iy = np.clip(np.floor((cy - oy) * SS).astype(int), 0, X.shape[0] - 1)
    return coverage(np.hypot(X - cx, Y - cy) - np.float32(radius)) * inside[iy, ix]


def iso_cube(cx, cy, size):
    """Three visible faces of a cube in a three-quarter view (top, left, right)."""
    a = size * .5
    top = [(cx, cy - a), (cx + a, cy - a * .5), (cx, cy), (cx - a, cy - a * .5)]
    left = [(cx - a, cy - a * .5), (cx, cy), (cx, cy + a * 1.1), (cx - a, cy + a * .6)]
    right = [(cx, cy), (cx + a, cy - a * .5), (cx + a, cy + a * .6), (cx, cy + a * 1.1)]
    return top, left, right


# ---------------------------------------------------------------------------------------------- comic drawing
def ink_fill(r: Raster, m: np.ndarray, fill, ink: float = 0.0, shadow=None, shadow_rgb=None, ink_rgb=None):
    """Flat fill of a binary SS mask with an ink outline `ink` px wide around it and an optional hard offset shadow."""
    k = INK if ink_rgb is None else ink_rgb
    whole = np.maximum(m, (outside_distance(m) < ink).astype(np.float32)) if ink else m
    if shadow:
        r.over(k if shadow_rgb is None else shadow_rgb, shift(whole, *shadow))
    if ink:
        r.over(k, whole)
    r.over(fill, m)


def poly_mask(r: Raster, pts) -> np.ndarray:
    return r.mask(lambda d, s: d.polygon(s(pts), fill=255))


def ell_mask(r: Raster, cx, cy, rx, ry=None) -> np.ndarray:
    ry = rx if ry is None else ry
    return r.mask(lambda d, s: d.ellipse([c for p in s([(cx - rx, cy - ry), (cx + rx, cy + ry)]) for c in p], fill=255))


def line_mask(r: Raster, pts, width) -> np.ndarray:
    def draw(d, s):
        q = s(pts)
        d.line(q, fill=255, width=max(1, int(round(width * SS))), joint="curve")
        for x, y in (q[0], q[-1]):
            rr = width * SS / 2
            d.ellipse((x - rr, y - rr, x + rr, y + rr), fill=255)
    return r.mask(draw)


def comic_layers(win: Raster, sdf, *, ink, bands, fill, shadow, ink_rgb, shadow_rgb, shadow_alpha, hollow, extra=None):
    """Hard offset shadow, ink outline, inward bands (width, colour), then the fill; or an open frame when hollow."""
    X, Y = win.X, win.Y
    total = ink + sum(w for w, _ in bands)
    if shadow:
        ds = sdf(X - np.float32(shadow), Y - np.float32(shadow))
        a = coverage(ds)
        if hollow:
            a = a * (1 - coverage(ds + total))
        win.over(shadow_rgb, a * shadow_alpha)
    d = sdf(X, Y)
    if hollow:
        rgb = np.broadcast_to(np.asarray(ink_rgb, np.float32), X.shape + (3,)).copy()
        inset = ink
        for width, colour in bands:
            rgb = mix(rgb, np.asarray(colour, np.float32), coverage(d + inset))
            inset += width
        win.over(rgb, coverage(d) * (1 - coverage(d + total)))
    else:
        win.over(ink_rgb, coverage(d))
        inset = ink
        for width, colour in bands:
            win.over(colour, coverage(d + inset))
            inset += width
        if fill is not None:
            win.over(fill, coverage(d + inset))
    if extra is not None:
        extra(win, d, total)


def _band_render(r: Raster, w: int, h: int, depth: int, draw, fill):
    """Render only the four border bands (the centre is one flat colour or transparent by construction)."""
    D = depth
    if fill is not None:
        r.rgb[D * SS:(h - D) * SS, D * SS:(w - D) * SS] = np.asarray(fill, np.float32)
        r.a[D * SS:(h - D) * SS, D * SS:(w - D) * SS] = 1.0
    for x, y, ww, hh in ((0, 0, w, D), (0, h - D, w, D), (0, D, D, h - 2 * D), (w - D, D, D, h - 2 * D)):
        draw(r.window(x, y, ww, hh))


def comic_tile(w: int, h: int, *, cuts=(0.0, 0.0, 0.0, 0.0), ink: float = 10.0, bands=(), fill=WHITE,
               shadow: float = 10.0, hollow: bool = False, inset: float = 1.0, depth: int = 48, tinted: bool = True,
               shadow_alpha: float = 1.0, accent=None, box=None, border: float = 40.0) -> Image.Image:
    """A nine-sliced comic surface: an octagon (cuts TL, TR, BR, BL) with its hard ink shadow, ink line, inward bands
    and fill. tinted: black ink and shadow (the sprite stays achromatic); otherwise the ink colour."""
    frame = ink + sum(width for width, _ in bands)
    box = box or (inset, inset, w - inset - shadow, h - inset - shadow)
    reach = corner_reach(w, h, box, cuts, shadow, frame, hollow)
    if reach > border - 3.0 + 1e-6:
        raise ValueError(f"Corner features reach {reach:.1f} px into a {border:g} px corner square")
    r = Raster(w, h)
    k = K if tinted else INK
    pts = octagon(box, cuts)
    _band_render(r, w, h, depth, lambda win: comic_layers(
        win, lambda X, Y: sd_poly(X, Y, pts), ink=ink, bands=bands, fill=fill, shadow=shadow, ink_rgb=k, shadow_rgb=k,
        shadow_alpha=shadow_alpha, hollow=hollow, extra=accent), None if hollow else fill)
    return r.export()


# ---------------------------------------------------------------------------------------------- sliced surfaces
# Frame weights and corner cuts in sprite px (PPU 200: 2 px = 1 unit at 1080p). The ink is about twice round 1's and
# close to the concept's 5 px at 1080p; every cut uses its corner square up to the budget (comic_tile refuses a corner
# that would reach past 37 px of a 40 px square: the stretched edge bands must stay the same along their length).
INK_W = 10.0          # panels, cards, buttons, slots, trays, the top bar and the stage card
INK_C = 8.0           # chips, tags and labels
SHADOW = 10.0         # the hard ink shadow, down and right (5 units)
SHADOW_C = 8.0        # under chips and the button face (4 units)
PANEL_FILL = NAVY
# The concept's blue shop sections (shop.png: section bodies about (3, 46-78, 101-170), median about (3, 55, 115)). The
# panel stays authored and the W20X `section` token is FFFFFF; the dice style page shows the same cell, where Muted notes
# need 4.5:1 on its centre (palette_report: muted/section).
SECTION_HEX = "043A80"
SECTION_FILL = col(SECTION_HEX)
INSET_FILL = NAVY_DK
SLOT_FILL = col("021638")
LABEL_FILL = CREAM
GUTTER = ((5.0, CREAM), (2.0, INK))   # the dark panels' comic-panel gutter: a cream band and a thin ink line
CARD_CUT = 21.0       # cards and paper panels: 10.5 units; the card's cream then starts 15.1 px in from the inner rect
PANEL_CUT = 18.0      # dark panels with the gutter band (frame 17 px)
TRAY_CUT = 32.0       # the open trays (border 72): 16 units, so the accent ring (7 units in, 1.6x) stays inside it
RING_CUT = 30.0       # rings: inside the trays' and the stage card's cuts at their callers' offsets (1.6x at the tray)
RARITY_RING_CUT = 16.0   # the W13 ring inside a card face: just inside the face's cream cut (15.1 px from the inner rect)
TRAY_FRAME = ((8.0, WHITE), (3.0, K))
CHIP_CUTS = (32.0, 24.0, 24.0, 24.0)       # secondary buttons, formula chips, tabs: octagonal ends
BUTTON_CUTS = (31.0, 6.0, 23.0, 6.0)       # the button face: big top-left and bottom-right cuts, a forward lean
LABEL_CUTS = (32.0, 6.0, 26.0, 6.0)        # captions and badges lean the same way


def surface(spec: dict) -> Image.Image:
    """Nine-sliced and horizontally sliced surfaces. Frames stay in the border bands, centres stay flat."""
    name = spec["name"]
    w, h = spec["size"]
    card_face = dict(cuts=(CARD_CUT,) * 4, ink=INK_W, fill=CREAM, shadow=SHADOW, tinted=False)
    if name in ("ui_paper_panel", "ui_card_controller", "ui_card_controller_compact", "ui_card_shop"):
        # Cream face in a heavy ink line (roll and shop pages); the W13 rarity ring inside gives a card its colour.
        return comic_tile(w, h, **card_face)
    if name == "ui_card_item":
        return comic_tile(w, h, cuts=(PANEL_CUT,) * 4, ink=INK_W, bands=((6.0, RED), (2.0, INK)), fill=CREAM,
                          shadow=SHADOW, tinted=False)
    if name == "ui_chip_label":
        return comic_tile(w, h, cuts=LABEL_CUTS, ink=INK_C, fill=LABEL_FILL, shadow=6.0, tinted=False)
    if name in ("ui_panel_hud", "ui_tab_bottom", "ui_section_panel"):
        return comic_tile(w, h, cuts=(PANEL_CUT,) * 4, ink=INK_W, bands=GUTTER,
                          fill=SECTION_FILL if name == "ui_section_panel" else PANEL_FILL, shadow=SHADOW, tinted=False)
    if name == "ui_info_inset":
        # A well: ink line, the fill, and a hard inner shadow along the top and left (light from the upper left).
        def well(win, d, total):
            X, Y = win.X, win.Y
            edge = np.minimum(Y - np.float32(1.0 + total), X - np.float32(1.0 + total))
            win.over(INK, coverage(d + total) * coverage(edge - 5.0) * .55)
        return comic_tile(w, h, cuts=(14.0,) * 4, ink=INK_C, fill=INSET_FILL, shadow=0.0, tinted=False, accent=well)
    if name == "ui_slot_inventory":
        return comic_tile(w, h, cuts=(22.0,) * 4, ink=INK_C, bands=((3.0, CREAM), (2.0, INK)), fill=SLOT_FILL,
                          shadow=SHADOW_C, tinted=False)
    if name == "ui_tray_front":
        return tray_front(w, h)
    # Achromatic, tinted by the game.
    if name == "ui_topbar":
        return comic_tile(w, h, cuts=(CARD_CUT,) * 4, ink=INK_W, fill=WHITE, shadow=SHADOW)
    if name == "ui_chip_formula":
        return comic_tile(w, h, cuts=CHIP_CUTS, ink=INK_C, fill=WHITE, shadow=SHADOW_C)
    if name == "ui_stage_card":
        return comic_tile(w, h, cuts=(29.0,) * 4, ink=INK_W, fill=WHITE, shadow=SHADOW, depth=56, border=48.0)
    if name == "ui_dice_tray":
        return comic_tile(w, h, cuts=(TRAY_CUT,) * 4, ink=INK_W, bands=TRAY_FRAME, fill=None, shadow=SHADOW, hollow=True,
                          depth=72, border=72.0)
    if name == "ui_button_bevel":
        return comic_tile(w, h, cuts=BUTTON_CUTS, ink=INK_W, fill=WHITE, shadow=SHADOW_C)
    if name == "ui_button_ghost":
        return comic_tile(w, h, cuts=(26.0,) * 4, ink=6.0, bands=((6.0, WHITE), (2.0, K)), fill=None, shadow=0.0,
                          hollow=True)
    if name == "ui_ring":
        return comic_tile(w, h, cuts=(RING_CUT,) * 4, ink=6.0, bands=((6.0, WHITE), (2.0, K)), fill=None, shadow=0.0,
                          hollow=True)
    if name == "ui_frame_enchant":
        # On the cream card face just inside the card's ink: 7 units in from the left and top, 12 from the right and
        # bottom (the card's ink and shadow), its cuts inside the face's cream cuts.
        return comic_tile(w, h, cuts=(16.0, 8.0, 8.0, 8.0), ink=3.0, bands=((6.0, WHITE), (3.0, K)), fill=None,
                          shadow=0.0, hollow=True, box=(14.0, 14.0, w - 24.0, h - 24.0))
    if name == "ui_frame_block":
        return comic_tile(w, h, cuts=(16.0,) * 4, ink=6.0, fill=WHITE, shadow=0.0)
    if name == "ui_price_chip":
        return comic_tile(w, h, cuts=(32.0, 26.0, 26.0, 26.0), ink=INK_C, fill=WHITE, shadow=6.0)
    if name == "ui_progress_fill":
        return progress_bar(w, h, fill=True)
    if name == "ui_progress_track":
        return progress_bar(w, h, fill=False)
    if name == "ui_section_header":
        return section_header(w, h)
    if name == "ui_card_frame_rarity":
        return card_frame_rarity(w, h)
    raise ValueError("Unimplemented sliced sprite: " + name)


def _aniso_coverage(d):
    """Coverage for a distance measured in anisotropic display units: rescale by the local gradient."""
    gy, gx = np.gradient(d, np.float32(1.0 / SS))
    g = np.maximum(np.sqrt(gx * gx + gy * gy), 1e-3)
    return coverage(d / g)


# W14 shop (GameApp.Shop14 Section14): the header strip spans the section's inside, (f.x, f.w) to the inner right edge,
# ShopHead14 (68) - f.w - 2 units tall; f.w is the section panel's top frame inset (18 px = 9 units) -> 57 units.
SECTION_HEADER_UNITS = 57.0
CAPTION_SLANT = 10.0   # units: the title starts 14 units into the strip, clear of the slanted end at its height


def section_header(w: int, h: int) -> Image.Image:
    """Shop section caption (border 40/0/72/0, only sliced horizontally): a parallelogram with full-height "/" ends, an
    ink line and a hard shadow, white so the code's tint colours it. Designed in display units for its W14 height
    (0.5 units per px across, 57/128 down) because Unity scales only the height uniformly."""
    r = Raster(w, h)
    sx, sy = .5, SECTION_HEADER_UNITS / h
    Xu, Yu = r.X * sx, r.Y * sy
    W, H = w * sx, SECTION_HEADER_UNITS
    sh, ink, top = 3.0, 4.0, .6
    bottom = H - sh - .6
    # The left end stays inside the 20-unit left cap and the right end inside the 36-unit right cap, both 2 units clear of
    # the cap line; the top-right corner sits 8 units in, inside the section panel's own corner cut.
    body = [(1.0 + CAPTION_SLANT, top), (W - 8.0, top), (W - 8.0 - CAPTION_SLANT, bottom), (1.0, bottom)]
    ds = sd_poly(Xu - sh, Yu - sh, body)
    r.over(K, _aniso_coverage(ds))
    d = sd_poly(Xu, Yu, body)
    r.over(K, _aniso_coverage(d))
    r.over(WHITE, _aniso_coverage(d + ink))
    return r.export()


def progress_bar(w: int, h: int, fill: bool) -> Image.Image:
    """A flat bar with "/" ends (border 48/0/48/0; drawn with a uniform scale, so the ends keep their angle). The fill
    is two flat steps of cel shading; the track is a darker groove with a hard inner shadow along its top."""
    r = Raster(w, h)
    X, Y = r.X, r.Y
    body = [(25.0, 3.0), (w - 3.0, 3.0), (w - 25.0, h - 3.0), (3.0, h - 3.0)]
    d = sd_poly(X, Y, body)
    r.over(K, coverage(d))
    if fill:
        tone = np.where(Y < h * .55, 1.0, .82).astype(np.float32)
    else:
        tone = np.where(Y < 26.0, .3, .62).astype(np.float32)
    r.over(np.repeat(tone[..., None], 3, axis=2), coverage(d + 8.0))
    return r.export()


def tray_front(w: int, h: int) -> Image.Image:
    """Front lip of a locked-die slot (border 40/0/40/0; drawn 20 units tall, so designed in display units: 0.5 x,
    0.125 y units per px): an ink bar with a yellow top edge and "/" ends."""
    r = Raster(w, h)
    sx, sy = .5, 20.0 / h
    Xu, Yu = r.X * sx, r.Y * sy
    W = w * sx
    body = [(6.0, .4), (W - .4, .4), (W - 6.0, 19.6), (.4, 19.6)]
    d = sd_poly(Xu, Yu, body)
    r.over(INK, _aniso_coverage(d))
    inner = _aniso_coverage(d + 2.0)
    r.over(NAVY_DK, inner)
    r.over(YELLOW, inner * (Yu < 5.4))
    return r.export()


# ---------------------------------------------------------------------------------------------- card template (W13)
# CARD_TEMPLATE sections 3 and 6: the rarity ring and the three gems, neutral so TintedSprite13 colours them with the
# rarity colours (Ghost for goods). Flat comic cel shading: white is the tint itself, the shade a flat step below it.
RING = (2.0, 6.0, 2.0)   # px: ink, tinted band, ink; the band ends 10 px in (5 units; the default ring is 12 px)
GEM_SIZE = 128
GEM_INK = 9.0


def card_frame_rarity(w: int, h: int) -> Image.Image:
    """Rarity ring (border 40; transparent centre, TintedSprite13 draws it with fillCenter off): an ink line, a flat
    band that takes the rarity colour and an ink line, its corners cut like the card face's cream so it sits inside it."""
    ink, band, inner = RING
    return comic_tile(w, h, cuts=(RARITY_RING_CUT,) * 4, ink=ink, bands=((band, WHITE), (inner, K)), fill=None,
                      shadow=0.0, hollow=True, inset=0.0)


def gem_sdf(name: str, X, Y, size: int = GEM_SIZE) -> np.ndarray:
    c = size / 2
    if name == "ui_gem_common":
        return sd_circle(X, Y, c, c, 57.0)
    if name == "ui_gem_advanced":
        return sd_poly(X, Y, [(c, c - 61), (c + 61, c), (c, c + 61), (c - 61, c)])
    if name == "ui_gem_rare":
        return sd_poly(X, Y, star_pts(c, c, 62.0, 62.0 * .56, 6))
    raise ValueError(name)


def gem(name: str, size: int = GEM_SIZE) -> Image.Image:
    """Rarity gem (128 px, not sliced): a flat cel-shaded seal inside a heavy ink line. The upper-left half is the tint
    itself, the lower-right half one flat step darker, and a hard white glint sits on the lit side."""
    r = Raster(size, size)
    X, Y = r.X, r.Y
    c = size / 2
    d = gem_sdf(name, X, Y, size)
    r.over(K, coverage(d))
    face = coverage(d + GEM_INK)
    # Cel shade: a straight split from the lower left to the upper right, offset toward the lower right.
    split = ((X - c) + (Y - c)) / math.sqrt(2) - (8.0 if name != "ui_gem_rare" else 4.0)
    lit = coverage(split)
    grey = (.9 * lit + .64 * (1 - lit)).astype(np.float32)
    r.over(np.repeat(grey[..., None], 3, axis=2), face)
    glint = {"ui_gem_common": sd_circle(X, Y, c - 22, c - 22, 9.0),
             "ui_gem_advanced": sd_poly(X, Y, [(c - 30, c - 12), (c - 12, c - 30), (c - 6, c - 24), (c - 24, c - 6)]),
             "ui_gem_rare": sd_circle(X, Y, c - 9, c - 13, 6.5)}[name]
    r.over(WHITE, coverage(glint) * face)
    return r.export()


# ---------------------------------------------------------------------------------------------- icons and marks
def die_faces(r: Raster, cx, cy, size, pips=(1, 2, 3), pip_rgb=(INK, INK, INK), ink=4.0, shadow=None):
    """A comic die: three flat cel steps (top white, left cream, right grey) inside ink lines, ink pips."""
    top, left, right = iso_cube(cx, cy, size)
    whole = np.maximum.reduce([poly_mask(r, f) for f in (top, left, right)])
    ink_fill(r, whole, WHITE, ink, shadow=shadow)
    for face, tone in ((left, CREAM), (right, GREY)):
        r.over(tone, poly_mask(r, face))
    seam = line_mask(r, [top[3], top[2], top[1]], ink * .55)
    r.over(INK, np.maximum(seam, line_mask(r, [top[2], left[2]], ink * .55)))
    layouts = {1: [(.5, .5)], 2: [(.28, .28), (.72, .72)], 3: [(.25, .25), (.5, .5), (.75, .75)],
               4: [(.28, .28), (.72, .28), (.28, .72), (.72, .72)],
               5: [(.26, .26), (.74, .26), (.5, .5), (.26, .74), (.74, .74)],
               6: [(.28, .22), (.28, .5), (.28, .78), (.72, .22), (.72, .5), (.72, .78)]}
    for face, n, colour in zip((top, left, right), pips, pip_rgb):
        p0, p1, p2, p3 = face
        for u, v in layouts[n]:
            x = p0[0] + (p1[0] - p0[0]) * u + (p3[0] - p0[0]) * v
            y = p0[1] + (p1[1] - p0[1]) * u + (p3[1] - p0[1]) * v
            rr = size * .075
            r.over(colour, ell_mask(r, x, y, rr, rr * .8))


def icon(spec: dict) -> Image.Image:
    name = spec["name"]
    w, h = spec["size"]
    r = Raster(w, h)
    if name == "ui_icon_coin":
        # Comic coin: yellow disc, heavy ink line, an amber inner disc, a star and a hard glint (26-36 units).
        ink_fill(r, ell_mask(r, 48, 48, 39.5), YELLOW, 5.5)
        ink_fill(r, ell_mask(r, 48, 48, 27.0), AMBER, 3.0)
        ink_fill(r, poly_mask(r, star_pts(48, 49, 17.0, 7.5, 5)), YELLOW, 2.5)
        r.over(CREAM, poly_mask(r, [(22, 34), (34, 18), (40, 22), (28, 38)]))
        return r.export()
    if name == "ui_icon_roll":
        # A red dice cup with a yellow band, and a die jumping out of it (36 units on a dark inset).
        cup = poly_mask(r, [(52, 104), (204, 104), (182, 226), (74, 226)])
        ink_fill(r, cup, RED, 10.0)
        r.over(RED_SH, cup * poly_mask(r, [(150, 100), (210, 100), (186, 232), (140, 232)]))
        r.over(YELLOW, cup * poly_mask(r, [(40, 156), (220, 156), (220, 178), (40, 178)]))
        r.over(INK, cup * (line_mask(r, [(40, 156), (220, 156)], 5) + line_mask(r, [(40, 178), (220, 178)], 5)))
        rim = ell_mask(r, 128, 104, 82, 22)
        ink_fill(r, rim, YELLOW, 8.0)
        r.over(INK, ell_mask(r, 128, 104, 64, 12))
        die_faces(r, 150, 56, 70, pips=(1, 2, 3), ink=7.0)
        for x0, y0, x1, y1 in ((70, 40, 96, 60), (62, 66, 92, 76)):
            r.over(INK, line_mask(r, [(x0, y0), (x1, y1)], 7))
        return r.export()
    if name == "ui_icon_dice_view":
        # A die and a magnifier: yellow ring, light cyan lens, ink handle (40 units on a dark chip).
        die_faces(r, 50, 56, 72, pips=(1, 2, 3), ink=4.5)
        handle = line_mask(r, [(100, 100), (120, 120)], 13)
        ring = ell_mask(r, 84, 84, 25)
        ink_fill(r, np.maximum(handle, ring), INK, 4.0)
        r.over(YELLOW, np.clip(ring - ell_mask(r, 84, 84, 16), 0, 1))
        r.over(CYAN_LT, ell_mask(r, 84, 84, 16))
        r.over(WHITE, poly_mask(r, [(74, 80), (80, 72), (86, 75), (78, 84)]))
        return r.export()
    if name == "ui_icon_lock":
        # Yellow padlock with a grey shackle; on the near-black Background the colours carry the silhouette (20-22 units).
        shackle = r.mask(lambda d, s: d.arc([c for p in s([(68, 30), (188, 150)]) for c in p], 180, 360, fill=255,
                                           width=int(30 * SS)))
        legs = np.maximum(poly_mask(r, [(68, 88), (98, 88), (98, 132), (68, 132)]),
                          poly_mask(r, [(158, 88), (188, 88), (188, 132), (158, 132)]))
        ink_fill(r, np.maximum(shackle, legs), GREY, 9.0)
        body = poly_mask(r, [(40, 116), (216, 116), (216, 238), (40, 238)])
        ink_fill(r, body, YELLOW, 10.0)
        r.over(YELLOW_SH, body * poly_mask(r, [(160, 110), (222, 110), (222, 244), (160, 244)]))
        key = np.maximum(ell_mask(r, 128, 166, 17), poly_mask(r, [(117, 170), (139, 170), (146, 214), (110, 214)]))
        r.over(INK, key)
        return r.export()
    if name in ("ui_tag_lock", "ui_tag_clock"):
        burst = poly_mask(r, star_pts(64, 64, 60.0, 47.0, 12))
        ink_fill(r, burst, YELLOW if name == "ui_tag_lock" else CYAN, 4.0)
        ink_fill(r, ell_mask(r, 64, 64, 33), CREAM, 4.0)
        if name == "ui_tag_lock":
            arc = r.mask(lambda d, s: d.arc([c for p in s([(50, 36), (78, 64)]) for c in p], 180, 360, fill=255,
                                            width=int(8 * SS)))
            legs = np.maximum(poly_mask(r, [(50, 50), (58, 50), (58, 62), (50, 62)]),
                              poly_mask(r, [(70, 50), (78, 50), (78, 62), (70, 62)]))
            r.over(INK, np.maximum(arc, legs))
            r.over(INK, poly_mask(r, [(42, 58), (86, 58), (86, 90), (42, 90)]))
            r.over(YELLOW, ell_mask(r, 64, 72, 5))
        else:
            ring = r.mask(lambda d, s: d.ellipse([c for p in s([(40, 40), (88, 88)]) for c in p], outline=255,
                                                 width=int(6 * SS)))
            r.over(INK, np.maximum(ring, np.maximum(line_mask(r, [(64, 64), (64, 48)], 6),
                                                    line_mask(r, [(64, 64), (76, 70)], 6))))
        return r.export()
    if name == "ui_icon_arrow":
        m = poly_mask(r, [(14, 38), (52, 38), (48, 18), (84, 48), (44, 78), (48, 58), (10, 58)])
        r.over(WHITE, m)
        return r.export()
    if name == "ui_icon_book":
        pages = np.maximum.reduce([poly_mask(r, [(8, 20), (45, 26), (45, 80), (8, 74)]),
                                   poly_mask(r, [(51, 26), (88, 20), (88, 74), (51, 80)]),
                                   poly_mask(r, [(6, 76), (90, 76), (90, 86), (6, 86)])])
        lines = np.maximum.reduce([line_mask(r, [(15, y), (38, y + 4)], 3.6) for y in (36, 48, 60)] +
                                  [line_mask(r, [(58, y + 4), (81, y)], 3.6) for y in (36, 48, 60)])
        r.over(WHITE, np.clip(pages - lines, 0, 1))
        return r.export()
    if name == "ui_icon_gear":
        th = np.linspace(0, 2 * np.pi, 9)[:-1]
        pts = []
        for t in th:
            for da, rr in ((-.2, 30.0), (-.13, 42.0), (.13, 42.0), (.2, 30.0)):
                pts.append((48 + rr * math.cos(t + da), 48 + rr * math.sin(t + da)))
        gear = poly_mask(r, pts)
        r.over(WHITE, np.clip(gear - ell_mask(r, 48, 48, 13), 0, 1))
        return r.export()
    if name == "ui_icon_refresh":
        R, start = 74.0, -25.0
        arc = r.mask(lambda d, s: d.arc([c for p in s([(128 - R - 17, 128 - R - 17), (128 + R + 17, 128 + R + 17)]) for c in p],
                                        start, 245, fill=255, width=int(34 * SS)))
        th = math.radians(start)
        ex, ey = 128 + R * math.cos(th), 128 + R * math.sin(th)
        tx, ty = math.sin(th), -math.cos(th)
        nx, ny = math.cos(th), math.sin(th)
        head = poly_mask(r, [(ex + tx * 60, ey + ty * 60), (ex + nx * 44, ey + ny * 44), (ex - nx * 44, ey - ny * 44)])
        r.over(WHITE, np.maximum(arc, head))
        return r.export()
    if name == "ui_pip_dot":
        r.over(K, ell_mask(r, 32, 32, 29))
        r.over(WHITE, ell_mask(r, 32, 32, 24))
        return r.export()
    if name == "fx_score_spark":
        burst = poly_mask(r, star_pts(32, 32, 29.0, 11.0, 8))
        r.over(K, np.maximum(burst, (outside_distance(burst) < 3.0).astype(np.float32)))
        r.over(WHITE, burst)
        return r.export()
    if name == "fx_square_chip":
        chip = poly_mask(r, sheared((66, 66, 178, 178), 20, 34))
        ink_fill(r, chip, WHITE, 8.0, shadow=(12, 12), ink_rgb=K)
        return r.export()
    if name == "fx_arc_short":
        tt = np.linspace(0, 1, 40)
        strokes = []
        for k_, off in enumerate((0.0, 30.0, 56.0)):
            xs = 40 + 160 * tt + off * .5
            ys = 210 - 160 * np.sin(tt * np.pi * .5) - off
            width = (24 if k_ == 0 else 9) * (1 - .55 * tt)
            strokes.append((xs, ys, width))
        m = np.zeros((h * SS, w * SS), np.float32)
        for xs, ys, ws in strokes:
            for i in range(len(xs) - 1):
                m = np.maximum(m, line_mask(r, [(xs[i], ys[i]), (xs[i + 1], ys[i + 1])], (ws[i] + ws[i + 1]) / 2))
        ink_fill(r, m, WHITE, 5.0, ink_rgb=K)
        return r.export()
    if name == "fx_score_crown":
        # A yellow comic crown with a red jewel and cream pearls (48 x 32 units on the navy top bar).
        body = np.maximum(poly_mask(r, [(28, 104), (36, 40), (66, 74), (96, 18), (126, 74), (156, 40), (164, 104)]),
                          poly_mask(r, [(24, 90), (168, 90), (168, 118), (24, 118)]))
        ink_fill(r, body, YELLOW, 7.0)
        r.over(YELLOW_SH, body * poly_mask(r, [(110, 10), (180, 10), (180, 124), (110, 124)]))
        band = poly_mask(r, [(24, 90), (168, 90), (168, 118), (24, 118)])
        ink_fill(r, band, AMBER, 4.0)
        ink_fill(r, ell_mask(r, 96, 104, 10), RED, 3.5)
        for x, y in ((36, 36), (96, 14), (156, 36)):
            ink_fill(r, ell_mask(r, x, y, 9), CREAM, 3.5)
        return r.export()
    if name == "ui_pedestal":
        # A blue comic block under the shop's dice (160 x 80 units on a cream card): flat faces, ink lines, dots.
        top = [(30, 66), (192, 22), (354, 66), (192, 110)]
        left = [(30, 66), (192, 110), (192, 166), (30, 122)]
        right = [(192, 110), (354, 66), (354, 122), (192, 166)]
        whole = np.maximum.reduce([poly_mask(r, f) for f in (top, left, right)])
        ink_fill(r, whole, BLUE_LT, 6.0, shadow=(10, 8))
        lm, rm = poly_mask(r, left), poly_mask(r, right)
        r.over(BLUE, lm)
        r.over(NAVY, rm)
        # Whole dots 8 px across: about 3.3 px when the block is drawn 160 units wide (0.42 x).
        r.over(NAVY, halftone_whole(r, lm, 16.0, 4.0))
        r.over(NAVY_DK, halftone_whole(r, rm, 16.0, 4.0))
        r.over(INK, np.maximum(line_mask(r, [top[0], top[3], top[2]], 4.0), line_mask(r, [top[3], left[2]], 4.0)) * whole)
        return r.export()
    if name == "ui_die_shadow":
        # A flat, hard-edged comic shadow.
        d = np.hypot((r.X - 128) / 112, (r.Y - 66) / 42) - 1
        r.over(INK, coverage(d * 40) * .34)
        return r.export()
    raise ValueError("Unimplemented icon: " + name)


BADGE_SEAL = {
    "badge_enchant_base10": BLUE, "badge_enchant_mult2": RED, "badge_enchant_roll1": GREEN,
    "badge_enchant_slotless": PURPLE, "badge_enchant_submit1": YELLOW,
}


def badge(name: str) -> Image.Image:
    """Enchantment badge: a serrated comic seal in a flat colour, a cream symbol in ink line; reads at 28-32 units."""
    r = Raster(256, 256)
    seal = poly_mask(r, star_pts(128, 128, 120.0, 104.0, 16))
    ink_fill(r, seal, BADGE_SEAL[name], 8.0)
    ring = r.mask(lambda d, s: d.ellipse([c for p in s([(36, 36), (220, 220)]) for c in p], outline=255, width=int(5 * SS)))
    r.over(INK, ring * .9)
    symbol_fill = INK if name == "badge_enchant_submit1" else CREAM
    if name == "badge_enchant_base10":
        sym = np.maximum(line_mask(r, [(78, 128), (178, 128)], 34), line_mask(r, [(128, 78), (128, 178)], 34))
    elif name == "badge_enchant_mult2":
        sym = np.maximum(line_mask(r, [(88, 88), (168, 168)], 32), line_mask(r, [(168, 88), (88, 168)], 32))
    elif name == "badge_enchant_roll1":
        sym = np.maximum(poly_mask(r, [(82, 96), (174, 96), (158, 176), (98, 176)]), ell_mask(r, 128, 96, 46, 14))
        sym = np.clip(sym - ell_mask(r, 128, 97, 34, 8), 0, 1)
    elif name == "badge_enchant_slotless":
        sym = np.maximum.reduce([line_mask(r, pts, 16) for pts in (
            [(80, 110), (80, 80), (110, 80)], [(146, 80), (176, 80), (176, 110)],
            [(80, 146), (80, 176), (110, 176)], [(146, 176), (176, 176), (176, 146)])])
    else:
        sym = line_mask(r, [(84, 132), (114, 162), (174, 94)], 26)
    if symbol_fill is INK:
        r.over(INK, sym)
    else:
        ink_fill(r, sym, symbol_fill, 6.0)
    return r.export()


# ---------------------------------------------------------------------------------------------- large art
def card_back() -> Image.Image:
    """Menu card back: an ink frame with a cream gutter; inside, a red Ben-Day field split by yellow speed rays, and a blue
    burst carrying a white die. No rule values or labels."""
    W, H = 768, 1024
    r = Raster(W, H)
    X, Y = r.X, r.Y
    outer = sd_box(X, Y, (2, 2, W - 2, H - 2))
    r.over(INK, coverage(outer))
    r.over(CREAM, coverage(outer + 12))
    inner_box = (40, 40, W - 40, H - 40)
    di = sd_box(X, Y, inner_box)
    r.over(INK, coverage(di))
    field = coverage(di + 6)
    r.over(RED, field)
    cx, cy = W / 2, H / 2 + 20
    ang = np.arctan2(Y - cy, X - cx)
    rad = np.hypot(X - cx, Y - cy)
    rays = ((np.cos(ang * 12) > .55) & (rad > 140)).astype(np.float32)
    r.over(YELLOW, field * rays)
    # Whole dots 12 px across: about 3.2 px on the menu, where the back is drawn about 204 units wide (0.27 x).
    r.over(RED_SH, halftone_whole(r, field * (1 - rays), 18.0, 6.0))
    burst = poly_mask(r, burst_pts(cx, cy, 250.0, 175.0, 14, SEED + 7, .12))
    ink_fill(r, burst, BLUE, 9.0)
    r.over(BLUE_DOT, halftone_whole(r, burst * (rad > 150), 18.0, 6.0))
    ink_fill(r, ell_mask(r, cx, cy, 128), CREAM, 7.0)
    die_faces(r, cx, cy - 12, 170, pips=(1, 4, 6), ink=8.0)
    for sx, sy in ((92, 92), (W - 92, 92), (92, H - 92), (W - 92, H - 92)):
        ink_fill(r, poly_mask(r, star_pts(sx, sy, 26.0, 11.0, 5)), YELLOW, 4.0)
    return r.export()


def wordmark() -> Image.Image:
    """Title wordmark: 骰子构筑 in ZCOOL KuaiLe (CP3-2, the comic heading font) in ink on a yellow sheared caption banner
    with a hard shadow, and a red burst carrying a die."""
    W, H = 1440, 400
    r = Raster(W, H)
    banner = sheared((300, 70, 1400, 330), 70, 150)
    m = poly_mask(r, banner)
    ink_fill(r, m, YELLOW, 10.0, shadow=(16, 16))
    # Whole dots 8.4 px across in the banner's tail: about 3.7 px on the menu (drawn about 0.43 x).
    r.over(YELLOW_SH, halftone_whole(r, m * (r.X > 1150), 14.0, 4.2))
    text = "骰子构筑"
    probe = ImageFont.truetype(str(HEADING), 100)
    bx = probe.getbbox(text)
    size = int(min(900 / (bx[2] - bx[0]), 210 / (bx[3] - bx[1])) * 100)
    font = ImageFont.truetype(str(HEADING), size * SS)
    b = font.getbbox(text)
    tw, th = (b[2] - b[0]) / SS, (b[3] - b[1]) / SS
    tx, ty = 330 + (1370 - 330 - tw) * .5, (H - th) * .5 - 4
    letters = r.mask(lambda d, s: d.text((tx * SS - b[0], ty * SS - b[1]), text, font=font, fill=255))
    r.over(RED, shift(letters, 9, 9))
    r.over(CREAM, (outside_distance(letters) < 5.0).astype(np.float32))
    r.over(INK, letters)
    burst = poly_mask(r, burst_pts(180, 200, 178.0, 118.0, 13, SEED + 3, .14))
    ink_fill(r, burst, RED, 9.0)
    die_faces(r, 178, 194, 168, pips=(1, 4, 5), ink=8.0)
    return r.export()


def render_sprite(spec: dict) -> Image.Image:
    name = spec["name"]
    if any(spec["border"]):
        return surface(spec)
    if name.startswith("badge_enchant_"):
        return badge(name)
    if name.startswith("ui_gem_"):
        return gem(name, spec["size"][0])
    if name == "ui_wordmark_dicecraft":
        return wordmark()
    if name == "ui_card_back":
        return card_back()
    return icon(spec)


# W14's shop stopped drawing these three; the contract regenerated on 2026-11-18 lists them as unreferenced.
# Coordinator decision (2026-11-18): this finished kit keeps them, so its outputs stay byte-identical.
# W16 retired the bottom tab from the runtime after all five themes had shipped it.
KEPT_UNREFERENCED = frozenset({"ui_price_chip", "ui_pedestal", "ui_frame_enchant", "ui_tab_bottom"})


def contract_sprites() -> list[dict]:
    return [s for s in json.loads(CONTRACT.read_text(encoding="utf-8"))["sprites"]
            if (s["referenced"] or s["name"] in KEPT_UNREFERENCED) and not s.get("optionalOverride")]


# ---------------------------------------------------------------------------------------------- table
TABLE_W, TABLE_H = 1920, 1080
# The concept's red Ben-Day page, measured on roll.png's clean gutters (1672 px wide): page (252, 40, 48), dots near
# black (20-37, 1-3, 1-3), a square grid of 9-10 px with dots 3.9-4.2 px across. At 1920 px: an 11 px grid of 4.8 px
# dots (at least 3 px at 1080p, as the coordinator asked for any halftone).
PAGE = col(PAGE_HEX)
PAGE_DOT = col(PAGE_DOT_HEX)
PAGE_PITCH = 11.0
PAGE_DOT_R = 2.4
# Blue felt (GameApp.Play11 L11; the same rects at every text size): the dice tray's floor, and the submit row, whose
# whole rect is calm (the info and rail zones meet), drawn as a square blue mat with its own ink line and shadow.
TRAY_RECT = (32.0, 352.0, 1448.0, 376.0)
SUBMIT_RECT = (32.0, 740.0, 1448.0, 202.0)
FELT_BLEED = 3.5          # px: the felt runs this far past every calm zone, beyond the Lanczos footprint (3 px)
MAT_INK, MAT_SHADOW = 5.0, 5.0
# THEME_SPEC 3.1 (coordinator 2026-11-17): large decoration only where no play state puts interface, never cut by a
# straight line, and never inside a button's or panel's bounding rectangle (it would show through the cut corners).
# The card row changes with the card count (strips, up to seven full cards, two compact rows from eight): no decoration.
# The action row (GameApp.Play11 DrawActions11, L11 at 100 % and 150 % text): the main button's rect ends at x 1240
# (1276 at 150 %), the end-stage slot (EndStage or Skip) starts at 1496 and its qualified ring at 1492; above it the
# submit mat's shadow ends at y 956. So the one rectangle free in every state is 1276-1492 x 956-1080; the burst keeps
# 4 units inside it.
ACTION_FREE = (1276.0, 956.0, 1492.0, 1080.0)
BURST = (1384.0, 1019.0, 96.0, 52.0)             # centre and radii
BURST_INK = 3.0
# Bounding rectangles (x, y, w, h) of the play screen's buttons and panels in any state or text size (L11 at 100 % and
# 150 %, UiKit.Button's step, the qualified ring), which decoration must stay out of.
PLAY_RECTS = {
    "top bar": (16, 12, 1888, 72), "card row": (32, 96, 1448, 244), "stage card": (1496, 96, 392, 244),
    "tray": (32, 352, 1448, 376), "resources": (1496, 352, 392, 376), "submit row": (32, 740, 1448, 202),
    "settle": (1496, 740, 392, 202), "hint": (32, 958, 600, 88), "secondary": (560, 966, 324, 72),
    "primary": (840, 962, 436, 82), "end": (1492, 958, 400, 88),
}


def burst_polygon():
    bx, by, rx, ry = BURST
    return [(px, by + (py - by) * ry / rx) for px, py in burst_pts(bx, by, rx, rx * .62, 12, SEED + 11, .14)]


def _check_burst():
    xs, ys = zip(*burst_polygon())
    x0, y0, x1, y1 = ACTION_FREE
    if (min(xs) - BURST_INK < x0 + 4 or max(xs) + BURST_INK > x1 - 4 or min(ys) - BURST_INK < y0 + 4
            or max(ys) + BURST_INK > y1 - 4):
        raise ValueError("The burst must sit wholly inside the action row's free rectangle")


_check_burst()


def tray_face_units():
    """The dice tray sprite's face octagon in screen units at L11.tray (its shadow, ink and cut halved to units)."""
    x, y, w, h = TRAY_RECT
    return octagon((x + .5, y + .5, x + w - .5 - SHADOW / 2, y + h - .5 - SHADOW / 2), (TRAY_CUT / 2,) * 4)


def felt_mask(X, Y) -> np.ndarray:
    """Coverage of the flat blue felt: the dice tray's floor (its face less 3 units, under the tray's own ink), the submit
    mat, and every calm zone with FELT_BLEED around it."""
    tray = coverage(sd_poly(X, Y, tray_face_units()) + 3.0)
    x, y, w, h = SUBMIT_RECT
    mat = coverage(sd_box(X, Y, (x - FELT_BLEED, y - FELT_BLEED, x + w + FELT_BLEED, y + h + FELT_BLEED)))
    zones = [coverage(sd_box(X, Y, (zx - FELT_BLEED, zy - FELT_BLEED, zx + zw + FELT_BLEED, zy + zh + FELT_BLEED)))
             for zx, zy, zw, zh in FELT_ZONES.values()]
    return np.maximum.reduce([tray, mat] + zones)


def page_layer(r: Raster):
    """The concept's red Ben-Day page: flat red with near-black dots on a square grid."""
    r.over(PAGE, np.ones_like(r.X))
    r.over(PAGE_DOT, halftone(r.X, r.Y, PAGE_PITCH, PAGE_DOT_R, 0.0))


def table_layer(r: Raster):
    X, Y = r.X, r.Y
    page_layer(r)
    # One readable starburst in the action row's free rectangle: a closed shape with its whole ink line, never clipped.
    x0, y0, x1, y1 = ACTION_FREE
    if float(X.max()) > x0 and float(X.min()) < x1 and float(Y.max()) > y0:
        d = sd_poly(X, Y, burst_polygon())
        r.over(INK, coverage(d - BURST_INK))
        r.over(YELLOW, coverage(d))
    # The submit mat's ink line and hard shadow, outside the felt bleed.
    x, y, w, h = SUBMIT_RECT
    mat = (x - FELT_BLEED - MAT_INK, y - FELT_BLEED - MAT_INK, x + w + FELT_BLEED + MAT_INK, y + h + FELT_BLEED + MAT_INK)
    r.over(INK, coverage(sd_box(X - MAT_SHADOW, Y - MAT_SHADOW, mat)))
    r.over(INK, coverage(sd_box(X, Y, mat)))
    # The flat blue felt last, so nothing above crosses it.
    r.over(col(PALETTE["felt"]), felt_mask(X, Y))


TABLE_TILE = 240


def render_tiles(layer) -> Image.Image:
    """A full 1920 x 1080 image rendered in tiles with an 8 px margin (beyond the Lanczos footprint) and stitched."""
    canvas = np.zeros((TABLE_H, TABLE_W, 3), np.float32)
    margin = 8
    for y in range(0, TABLE_H, TABLE_TILE):
        for x in range(0, TABLE_W, TABLE_TILE):
            w, h = min(TABLE_TILE, TABLE_W - x), min(TABLE_TILE, TABLE_H - y)
            r = Raster(w + 2 * margin, h + 2 * margin, float(x - margin), float(y - margin))
            layer(r)
            im = np.asarray(r.export()).astype(np.float32)
            canvas[y:y + h, x:x + w] = im[margin:margin + h, margin:margin + w, :3]
    return Image.fromarray(np.clip(np.rint(canvas), 0, 255).astype(np.uint8), "RGB")


def table_scene() -> Image.Image:
    """The play screen's table (THEME_SPEC 3.1): the page, the felt and the submit mat, and one burst."""
    return render_tiles(table_layer)


def page_scene() -> Image.Image:
    """THEME_SPEC 3 Table/page_scene.png (W20X): the backdrop of the non-play screens, the plain red Ben-Day page with no
    layout shapes and no large decoration."""
    return render_tiles(page_layer)


def check_felt_zones(table: Image.Image) -> dict[str, int]:
    """Fail on any RGB-channel deviation from the felt colour in the four protected design zones."""
    if table.mode != "RGB" or table.size != (TABLE_W, TABLE_H):
        raise ValueError("Table must be RGB 1920x1080 before checking zones")
    pixels = np.asarray(table).astype(np.int16)
    expected = np.array(rgb8(PALETTE["felt"]), dtype=np.int16)
    results = {}
    for name, (x, y, w, h) in FELT_ZONES.items():
        delta = int(np.abs(pixels[y:y + h, x:x + w] - expected).max())
        results[name] = delta
        passed = delta <= MAX_FELT_RGB_DEVIATION
        print(f"COMIC_ZONE {name} rect={x},{y},{w},{h} max_rgb_delta={delta} "
              f"limit={MAX_FELT_RGB_DEVIATION} {'PASS' if passed else 'FAIL'}")
        if not passed:
            raise ValueError(f"Table decoration entered protected zone {name}: {delta}")
    return results


def felt_tile() -> Image.Image:
    """Periodic newsprint grain on the felt colour, drawn at 4x and wrap-filtered; mean is the felt colour, no gradient."""
    n = 1024
    rng = np.random.RandomState(SEED)
    grain = rng.randint(-5, 6, (n * SS, n * SS)).astype(np.float32)
    padded = np.pad(grain, ((16, 16), (16, 16)), mode="wrap")
    small = _resize_f(padded, n + 8, n + 8)[4:4 + n, 4:4 + n]
    base = np.array(rgb8(PALETTE["felt"]), np.float32)
    rgb = base[None, None, :] + small[..., None] * np.array([1.6, 1.6, 1.6], np.float32)
    return Image.fromarray(np.clip(np.rint(rgb), 0, 255).astype(np.uint8), "RGB")


# ---------------------------------------------------------------------------------------------- offline proofs
def _lin_arr(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def _gam_arr(c):
    return np.where(c <= .0031308, c * 12.92, 1.055 * np.power(np.clip(c, 0, None), 1 / 2.4) - .055)


def tint(im: Image.Image, colour, alpha: float = 1.0) -> Image.Image:
    """Unity UI in a linear project: texture x tint in linear light."""
    a = np.asarray(im.convert("RGBA")).astype(np.float32) / 255.0
    t = np.asarray(colour, np.float32) if not isinstance(colour, str) else col(colour)
    rgb = _gam_arr(_lin_arr(a[..., :3]) * _lin_arr(t))
    out = np.concatenate([rgb, a[..., 3:] * alpha], axis=2)
    return Image.fromarray(np.clip(np.rint(out * 255), 0, 255).astype(np.uint8), "RGBA")


def nine_slice(im: Image.Image, border, size) -> Image.Image:
    """Offline emulation of a sliced Image at source pixel density; borders shrink when the target is smaller."""
    l, b, r, t = map(int, border)
    w, h = im.size
    tw, th = max(1, int(size[0])), max(1, int(size[1]))
    kx = min(1.0, tw / (l + r)) if l + r else 1.0
    ky = min(1.0, th / (t + b)) if t + b else 1.0
    dl, dr, dt, db = round(l * kx), round(r * kx), round(t * ky), round(b * ky)
    sx, sy = [0, l, w - r, w], [0, t, h - b, h]
    dx, dy = [0, dl, tw - dr, tw], [0, dt, th - db, th]
    out = Image.new("RGBA", (tw, th))
    for j in range(3):
        for i in range(3):
            sw, sh = sx[i + 1] - sx[i], sy[j + 1] - sy[j]
            ww, hh = dx[i + 1] - dx[i], dy[j + 1] - dy[j]
            if min(sw, sh, ww, hh) <= 0:
                continue
            patch = im.crop((sx[i], sy[j], sx[i + 1], sy[j + 1])).resize((ww, hh), Image.Resampling.LANCZOS)
            out.paste(patch, (dx[i], dy[j]))
    return out


class Mock:
    """A 1920x1080 offline composite of the play screen at W11 design sizes (not a Unity capture). It keeps a coverage
    map of everything drawn over the table, so marks and words that land straight on the red page can be listed."""

    def __init__(self, sprites, specs, table):
        self.sprites, self.specs = sprites, {s["name"]: s for s in specs}
        self.table = np.asarray(table.convert("RGB")).astype(np.int16)
        self.im = table.convert("RGBA")
        self.cover = np.zeros(self.table.shape[:2], np.float32)
        self.font = str(FONT)
        self.on_page = []

    def _paste(self, im: Image.Image, x: int, y: int, label: str | None = None):
        if label is not None:
            self._check(np.asarray(im)[..., 3] >= 128, x, y, label, im)
        self.im.alpha_composite(im, (x, y))
        H, W = self.cover.shape
        a = np.asarray(im)[..., 3].astype(np.float32) / 255.0
        x0, y0, x1, y1 = max(0, x), max(0, y), min(W, x + im.width), min(H, y + im.height)
        if x1 > x0 and y1 > y0:
            sub = a[y0 - y:y1 - y, x0 - x:x1 - x]
            self.cover[y0:y1, x0:x1] = 1 - (1 - self.cover[y0:y1, x0:x1]) * (1 - sub)

    def _check(self, mask: np.ndarray, x: int, y: int, label: str, im: Image.Image | None = None, colour=None):
        """Record a word or mark whose opaque pixels fall on uncovered table outside the felt (the red page)."""
        H, W = self.cover.shape
        x0, y0, x1, y1 = max(0, x), max(0, y), min(W, x + mask.shape[1]), min(H, y + mask.shape[0])
        if x1 <= x0 or y1 <= y0:
            return
        sub = mask[y0 - y:y1 - y, x0 - x:x1 - x]
        felt = np.array(rgb8(PALETTE["felt"]), np.int16)
        off_felt = np.abs(self.table[y0:y1, x0:x1] - felt).sum(axis=2) > 6
        hits = sub & (self.cover[y0:y1, x0:x1] < .5) & off_felt
        if hits.any():
            if colour is None and im is not None:
                px = np.asarray(im)[y0 - y:y1 - y, x0 - x:x1 - x][hits][:, :3]
                colour = hexc(px.mean(axis=0) / 255.0)
            self.on_page.append({"what": label, "pixels": int(hits.sum()), "colour": colour,
                                 "vs page": contrast(colour, hexc(PAGE)), "vs dots": contrast(colour, hexc(PAGE_DOT))})

    def put(self, name, x, y, w, h, colour=None, alpha=1.0, mult=1.0):
        spec = self.specs[name]
        sprite = self.sprites[name]
        dens = spec["pixelsPerUnit"] / 100.0 * mult
        if any(spec["border"]):
            im = nine_slice(sprite, spec["border"], (round(w * dens), round(h * dens)))
        else:
            im = sprite.copy()
            im.thumbnail((round(w * 4), round(h * 4)), Image.Resampling.LANCZOS)
        im = im.resize((max(1, round(w)), max(1, round(h))), Image.Resampling.LANCZOS)
        if colour is not None or alpha < 1:
            im = tint(im, colour if colour is not None else WHITE, alpha)
        self._paste(im, round(x), round(y))

    def icon(self, name, x, y, w, h, colour=None, alpha=1.0):
        im = self.sprites[name].copy()
        im.thumbnail((round(w), round(h)), Image.Resampling.LANCZOS)
        if colour is not None or alpha < 1:
            im = tint(im, colour if colour is not None else WHITE, alpha)
        self._paste(im, round(x + (w - im.width) / 2), round(y + (h - im.height) / 2), label=name)

    def text(self, x, y, value, size, colour, anchor="lm", font=None):
        f = ImageFont.truetype(font or self.font, round(size))
        mask = Image.new("L", self.im.size, 0)
        ImageDraw.Draw(mask).text((x, y), value, font=f, fill=255, anchor=anchor)
        self._check(np.asarray(mask) >= 128, 0, 0, "text " + value, colour=colour)
        ImageDraw.Draw(self.im).text((x, y), value, font=f, fill=tuple(rgb8(colour)) + (255,), anchor=anchor)

    def art(self, path, x, y, size):
        with Image.open(path) as src:
            im = src.convert("RGBA")
        im.thumbnail((size, size), Image.Resampling.LANCZOS)
        self._paste(im, round(x + (size - im.width) / 2), round(y + (size - im.height) / 2), label="art " + Path(path).stem)


def _mock_die(m: Mock, cx, cy, size, pips):
    r = Raster(int(size * 1.4), int(size * 1.4))
    die_faces(r, size * .7, size * .66, size, pips=pips, pip_rgb=(col(PALETTE["red"]), col(PALETTE["blue"]), col(PALETTE["blue"])),
              ink=size * .045)
    im = r.export()
    shadow = m.sprites["ui_die_shadow"].resize((round(size * 1.3), round(size * .5)), Image.Resampling.LANCZOS)
    m._paste(shadow, round(cx - size * .55), round(cy + size * .4), label="die shadow")
    m._paste(im, round(cx - size * .7), round(cy - size * .66), label="die")


# The offline composites draw the W20X tokens (THEME_SPEC 2, coordinator 2026-11-17) as comic sets them: the step under
# every UiKit.Button (code today: B56B29 / 8F453A) is buttonStep, ink; the dice-pool tab (code today: panel) is caption,
# yellow. Until W20X lands the game draws today's colours.
STEP_ASSUMED = W20X_TOKENS["buttonStep"]
CAPTION = W20X_TOKENS["caption"]


def mock_button(m: Mock, x, y, w, h, face, label, size, label_colour, enabled=True, step=None):
    """UiKit.Button as drawn: the step (the face sprite) at (2, 8, w - 4, h - 6), the face at (0, 0, w, h - 7); a disabled
    button's step is Shadow."""
    m.put("ui_button_bevel", x + 2, y + 8, w - 4, h - 6, col(step or (STEP_ASSUMED if enabled else PALETTE["shadow"])))
    m.put("ui_button_bevel", x, y, w, h - 7, face)
    m.text(x + w / 2, y + (h - 7) / 2, label, size, label_colour, "mm")


def mock_compact13(m: Mock, x, y, w, h, rarity, title, text, art, ordinal=0):
    """A Compact card (DrawCompactCard13: the play row's mini cards from eight cards on): the framed icon block with the
    gem across its foot, the ordinal disc and the name beside it, the short text only when it fits whole."""
    P = PALETTE
    border = m.specs["ui_card_controller_compact"]["border"]
    m.put("ui_card_controller_compact", x, y, w, h)
    fl, fb, fr, ft = (v / 2 for v in frame_insets_px(np.asarray(m.sprites["ui_card_controller_compact"]), border))
    content = (x + fl + 4, y + ft + 4, w - fl - fr - 8, h - ft - fb - 8)
    ring_border = m.specs["ui_card_frame_rarity"]["border"]
    rl, rb, rr, rt = (v / 2 for v in ring_insets_px(np.asarray(m.sprites["ui_card_frame_rarity"]), ring_border))
    key = {"common": "rarityCommon", "advanced": "rarityAdvanced", "rare": "rarityRare", None: "ghost"}[rarity]
    tint_c = col(P[key])
    block, gem = min(content[3] - 8, 76), 18
    bx, by = content[0], content[1] + max(0, (content[3] - block - gem * .5) * .5)
    m.put("ui_card_frame_rarity", bx, by, block, block, tint_c)
    m.art(ROOT / "ArtSource/StyleKit/themes/comic/anchors" / f"{art}.png", bx + rl + 1, by + rt + 1,
          min(block - rl - rr, block - rt - rb) - 2)
    if rarity:
        m.put("ui_gem_" + rarity, bx + block / 2 - gem / 2, by + block - gem / 2, gem, gem, tint_c)
    tx, ty = bx + block + 10, content[1] + 2
    if ordinal:
        m.put("ui_pip_dot", tx, ty + 4, 20, 20, col(P["ink"]))
        m.text(tx + 10, ty + 14, str(ordinal), 12, P["paper"], "mm")
    m.text(tx + (26 if ordinal else 0), ty + 14, title, 20, P["ink"], "lm")
    lines = _wrap(text, BODY_FONT, 18, content[0] + content[2] - tx)
    if len(lines) * 24 <= content[1] + content[3] - (ty + 32):
        for i, line in enumerate(lines):
            m.text(tx, ty + 32 + i * 24, line, 18, P["ink"], "la", str(BODY_FONT))


# Stand-in cards for the card-row coverage mocks (the four comic StyleKit anchors, cycled).
MOCK_CARDS = (
    ("common", "排头兵", "第一颗提交的骰子点数×4，加到基础分。", "C108"),
    ("advanced", "蓝色浪潮", "本手提交时，提交的骰子全部按蓝色计算。", "I10"),
    ("rare", "点金手", "每次提交后，最后一颗骰子朝上的面永久镀金。", "C117"),
    ("common", "配重", "这一面权重+1（上限5），更容易掷出这一面。", "U10"),
)


def mock_card_row(m: Mock, count: int, slots: int = 5, large: bool = False):
    """GameApp.Play11 DrawCards11 (L11.cards 32, 96, 1448, 244): Full cards (Table size, width capped at 216, 280 with
    enlarged text) followed by 56-wide "+" strips for the empty slots; Mini from eight cards (or six with enlarged text):
    two rows of 280 x 117 Compact cards, nine and an "all" button when there are more than ten."""
    P = PALETTE
    x0, y0, W, H = 32, 96, 1448, 244
    if count > 7 or (large and count > 5):
        shown = 9 if count > 10 else count
        cw, ch = (W - 4 * 12) / 5, (H - 10) / 2
        for i in range(shown):
            rarity, title, text, art = MOCK_CARDS[i % len(MOCK_CARDS)]
            mock_compact13(m, x0 + i % 5 * (cw + 12), y0 + i // 5 * (ch + 10), cw, ch, rarity, title, text, art, ordinal=i + 1)
        return
    empty = max(0, slots - count)
    card_w = 0 if count == 0 else min(280 if large else 216, (W - empty * 68 - 12 * (count - 1)) / count)
    x = x0
    for i in range(count):
        rarity, title, text, art = MOCK_CARDS[i % len(MOCK_CARDS)]
        mock_card13(m, x, y0, card_w, H, rarity, title, text, art, ordinal=i + 1)
        x += card_w + 12
    for _ in range(empty):
        if x + 56 > x0 + W + .5:
            break
        m.put("ui_slot_inventory", x, y0 + 8, 56, H - 16)
        m.text(x + 28, y0 + H / 2, "+", 30, P["muted"], "mm")
        x += 68


# Action-row states (GameApp.Play11 DrawActions11): primary button, secondary button and the end slot. W11's capture
# states: (a) dice selected, (b3) scoring, (c) boss stage start, (f) ten dice and seven cards, plus rolling, target
# reached and a pressure stage.
ACTION_STATES = {
    "a": dict(primary=("锁定 2 颗", True), secondary="取消选择", end="end", reason="按“锁定”留下选中的骰子；再点一次骰子可以取消。"),
    "b3": dict(primary=None, secondary=None, end="skip", reason="计分中…"),
    "c": dict(primary=("掷骰 [R]", True), secondary=None, end="end", reason="按“掷骰”开始这一手。"),
    "f": dict(primary=("提交 2 颗", True), secondary="重掷 · 还能掷 2 [R]", end="end", reason="按“提交”计分，或者重掷其余的骰子。"),
    "rolling": dict(primary=("重掷 · 还能掷 1 [R]", False), secondary=None, end="skip", reason="掷骰中…"),
    "qualified": dict(primary=("提交 2 颗", True), secondary="重掷 · 还能掷 2 [R]", end="qualified",
                      reason="已达标：可以继续提交，也可以结束本关。"),
    "boss": dict(primary=("掷骰 [R]", True), secondary=None, end="end", reason="首领减益“满员”：要放满提交栏（5 颗）才能提交。"),
    "pressure": dict(primary=("提交 2 颗", True), secondary="重掷 · 还能掷 2 [R]", end="end", reason="按“提交”计分，或者重掷其余的骰子。"),
}


def action_rects(large: bool = False) -> dict:
    """L11's action row: hint, secondary, primary and the end slot (x, y, w, h)."""
    return {"hint": (32, 958, 600 if large else 512, 88), "secondary": (644, 966, 240, 72) if large else (560, 966, 264, 72),
            "primary": (896, 962, 380, 80) if large else (840, 962, 400, 80), "end": (1496, 962, 392, 80)}


def balanced_lines(text: str, font, size: float, width: float) -> list[str]:
    """GameApp.BalancedLines11: a text wider than the box breaks once where the halves are most even, preferring a break
    after punctuation (or in place of " · "), at least three characters a line."""
    f = ImageFont.truetype(str(font), int(round(size)))
    if "\n" in text or len(text) < 6 or f.getlength(text) <= width:
        return [text]
    prefix = [0.0]
    for ch in text:
        prefix.append(prefix[-1] + f.getlength(ch))
    total, after, before = prefix[-1], "，。；：、！？）”", "（“"
    best, best_skip, best_score = -1, 0, float("inf")
    for i in range(3, len(text) - 2):
        skip = 3 if text[i:i + 3] == " · " else 0
        if skip == 0 and (text[i] == " " or text[i - 1] == " "):
            continue
        if len(text) - (i + skip) < 3:
            continue
        punctuation = skip > 0 or text[i - 1] in after or text[i] in before
        a, b = prefix[i], total - prefix[i + skip]
        score = max(a, b) + (0 if punctuation else width * .6) + (width * 4 if a > width or b > width else 0)
        if score < best_score:
            best, best_skip, best_score = i, skip, score
    return [text] if best < 0 else [text[:best], text[best + best_skip:]]


def mock_actions(m: Mock, state: str = "f", large: bool = False):
    P = PALETTE
    s = ACTION_STATES[state]
    r = action_rects(large)
    k = 1.5 if large else 1.0
    sec = col(P["background"]) * .45 + col(P["panel"]) * .55
    m.put("ui_panel_hud", *r["hint"])
    # The hint as DrawActions11 sets it: balanced lines at the largest size (21, or 16 x 1.5 with enlarged text), TMP
    # auto-size down to 19.5 drawn units while the block is too tall or a line too wide, line spacing -8.
    hx, hy, hw, hh = r["hint"]
    size = 24.0 if large else 21.0
    lines = balanced_lines(s["reason"], m.font, size, hw - 32)
    while size > 19.5:
        f = ImageFont.truetype(m.font, round(size))
        if len(lines) * size * 1.38 <= hh - 12 and max(f.getlength(line) for line in lines) <= hw - 32:
            break
        size -= .5
    lh = size * 1.38
    for i, line in enumerate(lines):
        m.text(hx + 16, hy + hh / 2 + (i - (len(lines) - 1) / 2) * lh, line, size, P["textColor"], "lm")
    if s["secondary"]:
        x, y, w, h = r["secondary"]
        m.put("ui_chip_formula", x, y, w, h, sec)
        m.text(x + w / 2, y + h / 2, s["secondary"], min(21 * k, 25), P["textColor"], "mm")
    if s["primary"]:
        label, enabled = s["primary"]
        x, y, w, h = r["primary"]
        face = col(P["primary"]) if enabled else col(P["panel"])
        mock_button(m, x, y, w, h, face, label, (22 if large else 28) * (1.0 if not large else 1.2),
                    label_on(face) if enabled else P["textColor"], enabled=enabled)
    x, y, w, h = r["end"]
    if s["end"] == "skip":
        m.put("ui_chip_formula", x, y, w, h, sec)
        m.text(x + w / 2, y + h / 2, "跳过演出", 24, P["textColor"], "mm")
    else:
        qualified = s["end"] == "qualified"
        if qualified:
            mock_button(m, x, y, w, h, col(P["paper"]), "结束本关 · 结算", 24, P["ink"])
            m.put("ui_ring", x - 4, y - 4, w + 8, h + 4, col(P["reward"]))
        else:   # disabled until the target is reached: PanelLight face, Shadow step, Muted label with its reason
            mock_button(m, x, y, w, h, col(P["panelLight"]), "", 24, P["muted"], enabled=False)
            m.text(x + w / 2, y + 26, "结束本关 · 结算", 24, P["muted"], "mm")
            m.text(x + w / 2, y + 52, "达标后可结束", 17, P["muted"], "mm")


def mock_card13(m: Mock, x, y, w, h, rarity, title, text, art, ordinal=0):
    """A Table-size card as GameApp.Card11 DrawTallCard13 lays it out: the face, the rarity ring on the face's inner
    rect (FrameInsets11), the gem across the ring's top, the name, the picture and the short text."""
    P = PALETTE
    face_border = m.specs["ui_card_controller"]["border"]
    m.put("ui_card_controller", x, y, w, h)
    fl, fb, fr, ft = (v / 2 for v in frame_insets_px(np.asarray(m.sprites["ui_card_controller"]), face_border))
    inner = (x + fl, y + ft, w - fl - fr, h - ft - fb)
    ring_border = m.specs["ui_card_frame_rarity"]["border"]
    rl, rb, rr, rt = (v / 2 for v in ring_insets_px(np.asarray(m.sprites["ui_card_frame_rarity"]), ring_border))
    key = {"common": "rarityCommon", "advanced": "rarityAdvanced", "rare": "rarityRare", None: "ghost"}[rarity]
    tint_c = col(P[key])
    m.put("ui_card_frame_rarity", *inner, tint_c)
    content = (inner[0] + rl + 4, inner[1] + rt + 3, inner[2] - rl - rr - 8, inner[3] - rt - rb - 6)
    gem_size, gem_y = 30, inner[1] + (content[1] - 3 - inner[1]) * .5
    top = max(content[1], gem_y + gem_size * .5 + 2) if rarity else content[1]
    name_h = math.ceil(22 * 1.46) + 2
    art_top = top + name_h + 2
    lines = _wrap(text, BODY_FONT, 20, content[2])
    art_size = max(40, min(content[2] - 8, content[1] + content[3] - art_top - 4 - (len(lines) * 26 + 2)))
    m.art(ROOT / "ArtSource/StyleKit/themes/comic/anchors" / f"{art}.png", x + (w - art_size) / 2, art_top, art_size)
    m.text(x + w / 2, top + name_h / 2, title, 22, P["ink"], "mm")
    for i, line in enumerate(lines):
        m.text(x + w / 2, art_top + art_size + 4 + i * 26, line, 20, P["ink"], "ma", str(BODY_FONT))
    if rarity:
        m.put("ui_gem_" + rarity, x + w / 2 - gem_size / 2, gem_y - gem_size / 2, gem_size, gem_size, tint_c)
    if ordinal:
        m.put("ui_pip_dot", content[0], content[1], 22, 22, col(P["ink"]))
        m.text(content[0] + 11, content[1] + 11, str(ordinal), 14, P["paper"], "mm")


def label_on(fill) -> str:
    """The palette text colour the game would pick on a drawn fill (GameApp.BestLabel11, without the 5 % steps)."""
    fill_hex = hexc(fill)
    return max((PALETTE["ink"], PALETTE["paper"], PALETTE["textColor"]), key=lambda c: contrast(c, fill_hex))


def play_mock(sprites, specs, table, on_page: list | None = None, cards: int = 3, state: str = "f",
              large_actions: bool = False) -> Image.Image:
    """The play screen at W11 design sizes over table_scene.png, with `cards` controllers (DrawCards11) and the action
    row in `state` (DrawActions11). on_page, when given, receives every word or mark the composite draws straight on
    the red page (outside the felt and over no surface)."""
    P = PALETTE
    m = Mock(sprites, specs, table)
    lerp = lambda a, b, t: (col(a) * (1 - t) + col(b) * t)
    sec = lerp(P["background"], P["panel"], .55)
    # Top bar.
    m.put("ui_topbar", 16, 12, 1888, 72, col(P["panel"]))
    m.put("ui_chip_formula", 24, 20, 160, 56, col(P["panelLight"]))
    m.text(104, 47, "第 4 关", 28, label_on(col(P["panelLight"])), "mm")
    for i in range(8):
        cur = i == 1
        size = 20 if cur else 14
        x = 204 + i * 24 + (20 - size) / 2
        m.put("ui_pip_dot", x, 26 + (20 - size) / 2, size, size, col(P["gold"]) if i < 1 else col(P["panelLight"]))
        if cur:
            m.put("ui_ring", x - 3, 23, size + 6, size + 6, col(P["paper"]), mult=6)
    m.text(204, 63, "第 2 章 · 普通关", 20, P["muted"], "lm")
    m.put("ui_info_inset", 452, 18, 560, 60)
    m.text(470, 44, "2,480", 34, P["textColor"], "lm")
    m.text(580, 44, "/ 4,000", 32, P["muted"], "lm")
    m.put("ui_progress_track", 466, 66, 532, 7, col("07191C"), mult=48 / 7)
    m.put("ui_progress_fill", 466, 66, 330, 7, col(P["blue"]), mult=48 / 7)
    # Gold and the two buttons where the game puts them (GameApp.Play11 TopBar11: gold group 1476-1652; GameApp.Art07
    # DrawNavigation07: handbook 1668 and pause 1788, 108 x 60 UiKit.Button, a paper face on the step).
    m.icon("ui_icon_coin", 1482, 30, 36, 36)
    m.text(1608, 48, "28", 34, P["gold"], "rm")
    m.text(1610, 48, "金币", 20, P["muted"], "lm")
    for x, label in ((1668, "手册"), (1788, "暂停")):
        mock_button(m, x, 18, 108, 60, col(P["paper"]), label, 23, P["ink"])
    # Cards (W13 template), laid out as DrawCards11 does: five slots to start with, seven at most for ordinary cards.
    mock_card_row(m, cards, slots=5 if cards <= 5 else 7)
    # Stage card.
    m.put("ui_stage_card", 1496, 96, 392, 244, col(P["paper"]))
    m.put("ui_chip_formula", 1510, 110, 356, 42, col(P["panelLight"]))
    m.text(1526, 131, "普通关", 22, label_on(col(P["panelLight"])), "lm")
    m.text(1510, 172, "目标分数", 20, P["ink"], "lm")
    m.text(1510, 218, "4,000", 42, P["ink"], "lm")
    m.text(1510, 266, "过关奖励 +5 金币", 20, P["ink"], "lm")
    m.text(1510, 300, "提交栏 5 格 · 每手掷 3 次", 17, P["ink"], "lm")
    # Tray, dice and formula chips.
    rim = lerp(P["panel"], P["panelLight"], .55)
    m.put("ui_dice_tray", 32, 352, 1448, 376, rim)
    m.put("ui_ring", 39, 359, 1434, 362, col(P["ghost"]), alpha=.75, mult=1.6)
    m.put("ui_chip_formula", 56, 338, 100, 34, col(CAPTION))   # the dice-pool tab takes W20X's caption
    m.text(104, 355, "骰池", 20, label_on(col(CAPTION)), "mm")
    for i, pips in enumerate(((1, 2, 3), (4, 5, 6), (2, 3, 1), (6, 2, 4), (3, 1, 5))):
        _mock_die(m, 250 + i * 250, 500, 104, pips)
    m.put("ui_chip_formula", 668, 410, 104, 32, col(P["gold"]))
    m.text(718, 426, "已选 1", 18, label_on(col(P["gold"])), "mm")
    chips = [(52, 150, "基础分 26", "blue"), (214, 150, "一对 ×2", "red"), (376, 160, "全蓝 ×3", "blue"),
             (548, 170, "卡牌 ×1.5", "gold"), (730, 200, "= 234", "reward")]
    for x, w, label, token in chips:
        m.put("ui_chip_formula", x, 666, w, 48, col(P[token]))
        m.text(x + w / 2 - 2, 688, label, 20, label_on(col(P[token])), "mm")
    m.text(946, 690, "×", 24, P["onFelt"], "mm")
    # Resources.
    m.put("ui_panel_hud", 1496, 352, 392, 376)
    for i, (label, value, token) in enumerate((("还能掷", "2", "teal"), ("还能提交", "3", "gold"))):
        y = 366 + i * 96
        m.put("ui_info_inset", 1510, y, 364, 86)
        if i == 0:
            m.icon("ui_icon_roll", 1524, y + 25, 36, 36)
        m.text(1570, y + 43, label, 24, P["muted"], "lm")
        m.text(1856, y + 43, value, 52, P[token], "rm")
    m.text(1510, 578, "道具 1 / 3", 20, P["muted"], "lm")
    m.put("ui_card_item", 1510, 596, 64, 64)
    m.art(ROOT / "ArtSource/StyleKit/themes/comic/anchors/I10.png", 1517, 603, 48)
    m.put("ui_frame_block", 1548, 596, 26, 28, col(P["panelLight"]))
    m.put("ui_frame_block", 1550, 598, 22, 24, col(P["background"]))
    m.icon("ui_icon_lock", 1551, 599, 20, 22)
    for i in range(2):
        m.put("ui_ring", 1586 + i * 76, 596, 64, 64, col(P["panelLight"]))
    m.put("ui_chip_formula", 1510, 672, 364, 48, sec)
    m.icon("ui_icon_dice_view", 1522, 676, 40, 40)
    m.text(1692, 696, "查看骰子", 22, P["textColor"], "mm")
    # Submit info, rail and settle.
    m.put("ui_panel_hud", 32, 740, 300, 202)
    m.text(50, 770, "提交栏 2 / 5", 24, P["textColor"], "lm")
    m.text(50, 806, "已锁定 · 本手不可移回骰池", 18, P["gold"], "lm")
    m.text(50, 848, "一对 · 全蓝", 24, P["textColor"], "lm")
    m.put("ui_dice_tray", 344, 740, 1136, 202, rim)
    for i in range(5):
        x = 376 + i * 216
        locked = i < 2
        ring = lerp(P["gold"], P["paper"], .15) if locked else col(P["ghost"])
        m.put("ui_ring", x, 756, 200, 170, ring, alpha=1.0 if locked else .75)
        m.text(x + 186, 774, str(i + 1), 16, P["onFelt"], "mm")
        if locked:
            _mock_die(m, x + 72, 846, 96, ((5, 2, 3), (2, 6, 1))[i])
            m.put("ui_tray_front", x + 4, 920, 192, 20)
            m.icon("ui_tag_lock", x + 166, 798, 28, 30)
        else:
            m.text(x + 100, 840, "·  ·  ·", 20, P["onFelt"], "mm")
    m.put("ui_panel_hud", 1496, 740, 392, 202)
    m.text(1514, 772, "离目标还差", 22, P["muted"], "lm")
    m.text(1514, 822, "1,520", 44, P["textColor"], "lm")
    m.text(1514, 878, "达标后结束：过关奖励 +5 金币", 18, P["muted"], "lm")
    # Actions.
    mock_actions(m, state, large_actions)
    if on_page is not None:
        on_page.extend(m.on_page)
    return m.im.convert("RGB")


SMALL_PROOFS = (  # (sprite, display w, h, host colour, tint) at the sizes and hosts the game uses
    ("ui_icon_coin", 26, 26, "gold", None), ("ui_icon_coin", 36, 36, "panel", None),
    ("ui_icon_roll", 36, 36, "021F55", None), ("ui_icon_roll", 28, 28, "panel", None),
    ("ui_icon_lock", 20, 22, "background", None), ("ui_icon_dice_view", 40, 40, "021E50", None),
    ("ui_tag_lock", 28, 30, "felt", None), ("ui_tag_clock", 64, 64, "03276A", None),
    ("fx_score_crown", 48, 32, "panel", None), ("badge_enchant_base10", 28, 28, "paper", None),
    ("badge_enchant_mult2", 28, 28, "paper", None), ("badge_enchant_roll1", 28, 28, "paper", None),
    ("badge_enchant_slotless", 28, 28, "paper", None), ("badge_enchant_submit1", 28, 28, "paper", None),
    ("ui_icon_book", 34, 34, "paper", "ink"), ("ui_icon_gear", 34, 34, "paper", "ink"),
    ("ui_icon_arrow", 42, 42, "gold", "ink"), ("ui_icon_refresh", 72, 72, "blue", "ink"),
    ("ui_pip_dot", 22, 22, "paper", "ink"), ("fx_score_spark", 20, 20, "panel", "gold"),
)


def small_size_proof(sprites) -> Image.Image:
    """Icons and marks at their real display sizes on their real hosts (1080p, 1 unit = 1 px): top row at 1x,
    bottom row the same pixels enlarged 2x (nearest) so single pixels can be judged."""
    cell = 84
    im = Image.new("RGB", (len(SMALL_PROOFS) * cell, 2 * cell + 40), (14, 18, 34))
    d = ImageDraw.Draw(im)
    small = ImageFont.truetype(str(LATIN), 11)
    for i, (name, w, h, host, tint_key) in enumerate(SMALL_PROOFS):
        x = i * cell
        hostc = rgb8(PALETTE[host]) if host in PALETTE else rgb8(host)
        d.rectangle((x + 4, 4, x + cell - 4, cell - 4), fill=hostc)
        sprite = sprites[name]
        if tint_key:
            sprite = tint(sprite, col(PALETTE[tint_key]))
        one = sprite.resize((w, h), Image.Resampling.LANCZOS)
        tile = Image.new("RGBA", (w + 8, h + 8), hostc + (255,))
        tile.alpha_composite(one, (4, 4))
        im.paste(tile.convert("RGB"), (x + (cell - w - 8) // 2, (cell - h - 8) // 2))
        big = tile.resize(((w + 8) * 2, (h + 8) * 2), Image.Resampling.NEAREST)
        if big.width > cell - 8 or big.height > cell - 8:
            cx, cy = big.width // 2, big.height // 2
            half = (cell - 8) // 2
            big = big.crop((max(0, cx - half), max(0, cy - half), min(big.width, cx + half), min(big.height, cy + half)))
        im.paste(big.convert("RGB"), (x + (cell - big.width) // 2, cell + (cell - big.height) // 2))
        d.text((x + 4, 2 * cell + 4), name.replace("ui_", "").replace("badge_enchant_", "b_")[:13], fill=(220, 220, 235), font=small)
        d.text((x + 4, 2 * cell + 20), f"{w}x{h}", fill=(160, 170, 200), font=small)
    return im


def state_variants(sprites, specs, table) -> Image.Image:
    """Normal, pressure and boss cues as GameApp.Play11 draws them: stage tag, stage card (header chip and rings)
    and the tray's accent ring, on the felt. Offline composite."""
    P = PALETTE
    m = Mock(sprites, specs, page_patch(1752, 330))
    rim = col(P["panel"]) * .45 + col(P["panelLight"]) * .55
    for i, (kind, tint_key) in enumerate((("普通关", "panelLight"), ("高压关", "pressure"), ("首领关", "boss"))):
        x0 = 16 + i * 584
        tag = col(P[tint_key])
        m.put("ui_topbar", x0 - 10, 2, 200, 76, col(P["panel"]))
        m.put("ui_chip_formula", x0, 12, 160, 56, tag)
        m.text(x0 + 78, 39, f"第 {4 + i} 关", 28, label_on(tag), "mm")
        m._paste(felt_floor(160, 304), x0 + 404, 12)
        m.put("ui_stage_card", x0, 80, 392, 236, col(P["paper"]))
        if kind == "首领关":
            m.put("ui_ring", x0 + 4, 84, 376, 218, col(P["boss"]))
            m.put("ui_ring", x0 + 9, 89, 366, 208, col(P["boss"]), alpha=.55)
        elif kind == "高压关":
            m.put("ui_ring", x0 + 5, 85, 374, 216, col(P["pressure"]))
        m.put("ui_chip_formula", x0 + 14, 94, 356, 42, tag)
        m.text(x0 + 28, 114, kind if kind != "首领关" else "首领减益 · 二档", 22, label_on(tag), "lm")
        m.text(x0 + 14, 160, "目标分数", 20, P["ink"], "lm")
        goal = P["pressure"] if kind == "高压关" and contrast(P["pressure"], P["paper"]) >= 4.5 else P["ink"]
        m.text(x0 + 14, 204, "6,000" if kind == "高压关" else "4,000", 40, goal, "lm")
        m.text(x0 + 14, 250, "过关奖励 +10 金币", 20, P["ink"], "lm")
        m.put("ui_dice_tray", x0 + 404, 12, 160, 304, rim)
        if kind == "首领关":
            m.put("ui_ring", x0 + 410, 18, 148, 292, col(P["boss"]))
            m.put("ui_ring", x0 + 414, 22, 140, 284, col(P["boss"]), alpha=.6)
        elif kind == "高压关":
            m.put("ui_ring", x0 + 411, 19, 146, 290, col(P["pressure"]), mult=1.6)
        else:
            m.put("ui_ring", x0 + 411, 19, 146, 290, col(P["ghost"]), alpha=.75, mult=1.6)
    return m.im.convert("RGB")


def size_proofs(sprites, specs) -> Image.Image:
    """Buttons at 60, 72 and 80 units (the coordinator's check), chips and tabs at their game heights, panels and the
    W14 section caption, composited on the red page at 1080p density with the labels the game would pick."""
    P = PALETTE
    W, H = 1752, 900
    m = Mock(sprites, specs, page_patch(W, H))
    sec = col(P["background"]) * .45 + col(P["panel"]) * .55
    small = ImageFont.truetype(str(LATIN), 15)
    notes = []
    x = 16
    for k, h in enumerate((60, 72, 80)):
        y = 14 + k * 104
        mock_button(m, x, y, 300, h, col(P["primary"]), f"主按钮 {h}", 24, label_on(col(P["primary"])))
        mock_button(m, x + 320, y, 260, h, col(P["paper"]), f"按钮 {h}", 22, P["ink"])
        m.put("ui_chip_formula", x + 600, y, 260, h, sec)
        m.text(x + 730, y + h / 2, f"次要按钮 {h}", 21, label_on(sec), "mm")
        m.put("ui_button_ghost", x + 880, y, 220, h, col(P["ghost"]))
        m.text(x + 990, y + h / 2, f"描边 {h}", 21, P["textColor"], "mm")   # FlatButton's label: see-through face
        notes.append((x + 1110, y + h / 2 - 8, f"UiKit.Button h={h}: face {h - 7}, step at +8 / SolidButton / FlatButton ghost"))
    y = 330
    chips = [(32, 104, "gold", "已选 1", 17), (34, 100, "panel", "骰池", 20), (48, 150, "blue", "基础分 26", 20),
             (48, 150, "red", "一对 ×2", 20), (48, 170, "gold", "卡牌 ×1.5", 20), (56, 160, "panelLight", "第 4 关", 28)]
    cx = 16
    for h, w, key, label, size in chips:
        m.put("ui_chip_formula", cx, y, w, h, col(P[key]))
        m.text(cx + w / 2, y + h / 2, label, size, label_on(col(P[key])), "mm")
        cx += w + 24
    m.put("ui_price_chip", cx, y, 124, 36, col(P["gold"]))
    m.text(cx + 62, y + 18, "8", 22, label_on(col(P["gold"])), "mm")
    cx += 148
    m.put("ui_chip_label", cx, y, 40, 30, col(P["gold"]))
    m.text(cx + 20, y + 15, "×4", 16, P["ink"], "mm")
    m.put("ui_chip_label", cx + 64, y, 130, 40)
    m.text(cx + 129, y + 20, "排头兵", 20, P["ink"], "mm")
    notes.append((cx + 214, y + 6, "chip_formula at 32 / 34 / 48 / 56, price chip 36, chip_label 30 and 40"))
    y = 410
    m.put("ui_topbar", 16, y, 860, 72, col(P["panel"]))
    m.put("ui_info_inset", 40, y + 6, 420, 60)
    m.text(56, y + 34, "2,480 / 4,000", 30, P["textColor"], "lm")
    m.put("ui_stage_card", 900, y, 392, 244, col(P["paper"]))
    m.put("ui_chip_formula", 914, y + 14, 356, 40, col(P["panelLight"]))
    m.text(928, y + 34, "普通关", 22, label_on(col(P["panelLight"])), "lm")
    m.text(914, y + 90, "目标分数  4,000", 26, P["ink"], "lm")
    m.put("ui_panel_hud", 1316, y, 392, 202)
    m.put("ui_info_inset", 1330, y + 14, 364, 86)
    m.text(1346, y + 57, "还能掷", 24, P["muted"], "lm")
    m.text(1680, y + 57, "2", 52, P["teal"], "rm")
    m.put("ui_paper_panel", 16, y + 96, 300, 150)
    m.put("ui_slot_inventory", 336, y + 96, 60, 60)
    m.put("ui_card_item", 412, y + 96, 60, 60)
    m.put("ui_slot_inventory", 488, y + 96, 56, 150)
    m.put("ui_frame_block", 564, y + 96, 120, 60, col(P["panelLight"]))
    m.put("ui_ring", 700, y + 96, 180, 150, col(P["ghost"]))
    # W14 shop section: the panel, and its header strip on the frame's inside, 68 - f.w - 2 units tall.
    y = 690
    sp = sprites["ui_section_panel"]
    fl, fb, fr, ft = (v / 2 for v in frame_insets_px(np.asarray(sp), {s["name"]: s for s in specs}["ui_section_panel"]["border"]))
    # W14 tints the section panel with Panel (SHOP_LAYOUT 10); the dice style page still draws it untinted.
    m.put("ui_section_panel", 16, y, 900, 200, col(P["panel"]))
    m.put("ui_section_header", 16 + fl, y + ft, 900 - fl - fr, 68 - ft - 2, col(P["panelLight"]))
    m.text(16 + fl + 14, y + ft + (68 - ft - 2) / 2, "升级与服务", 22, label_on(col(P["panelLight"])), "lm")
    m.put("ui_section_panel", 940, y, 472, 200)
    m.put("ui_section_header", 940 + fl, y + ft, 472 - fl - fr, 68 - ft - 2, col(P["panelLight"]))
    m.text(940 + fl + 14, y + ft + (68 - ft - 2) / 2, "骰子与骰面", 22, label_on(col(P["panelLight"])), "lm")
    notes.append((16, y - 24, "W14 section, panel x Panel as Shop14 draws it, caption strip x PanelLight (code tint)"))
    notes.append((940, y - 24, "the same panel untinted, as the dice style page draws it"))
    m.put("ui_tray_front", 1436, y + 20, 192, 20)
    m.put("ui_progress_track", 1436, y + 60, 300, 7, col("07191C"), mult=48 / 7)
    m.put("ui_progress_fill", 1436, y + 60, 180, 7, col(P["blue"]), mult=48 / 7)
    im = m.im.convert("RGB")
    d = ImageDraw.Draw(im)
    for nx, ny, note in notes:
        d.rectangle((nx - 4, ny - 2, nx + d.textlength(note, font=small) + 4, ny + 18), fill=(20, 24, 44))
        d.text((nx, ny), note, fill=(246, 240, 222), font=small)
    return im


def felt_floor(w: int, h: int) -> Image.Image:
    """The blue felt a tray of this size shows inside its frame (the table draws it so), as an RGBA patch."""
    r = Raster(w, h)
    pts = octagon((.5 + 3, .5 + 3, w - .5 - SHADOW / 2 - 3, h - .5 - SHADOW / 2 - 3), (TRAY_CUT / 2,) * 4)
    r.over(col(PALETTE["felt"]), coverage(sd_poly(r.X, r.Y, pts)))
    return r.export()


def page_patch(w: int, h: int) -> Image.Image:
    """A plain piece of the red Ben-Day page (no rays), for the offline proofs."""
    r = Raster(w, h)
    r.over(PAGE, np.ones_like(r.X))
    r.over(PAGE_DOT, halftone(r.X, r.Y, PAGE_PITCH, PAGE_DOT_R, 0.0))
    return r.export().convert("RGB")


def preview(mock: Image.Image) -> Image.Image:
    """Settings preview: the offline play-screen composite reduced 4x with Lanczos (480 x 270)."""
    return mock.resize((480, 270), Image.Resampling.LANCZOS)


TINT_PROOF = {
    "ui_topbar": "panel", "ui_chip_formula": "panelLight", "ui_stage_card": "paper", "ui_dice_tray": "panelLight",
    "ui_button_bevel": "primary", "ui_button_ghost": "ghost", "ui_ring": "ghost", "ui_frame_enchant": "violet",
    "ui_frame_block": "panelLight", "ui_price_chip": "gold", "ui_progress_fill": "blue", "ui_progress_track": "07191C",
    "ui_section_header": "panelLight", "ui_pip_dot": "gold", "ui_icon_arrow": "ink", "ui_icon_book": "ink",
    "ui_icon_gear": "ink", "ui_icon_refresh": "ink", "fx_arc_short": "violet", "fx_score_spark": "gold",
    "fx_square_chip": "red", "ui_card_frame_rarity": "rarityAdvanced", "ui_gem_common": "rarityCommon",
    "ui_gem_advanced": "rarityAdvanced", "ui_gem_rare": "rarityRare",
}


def _checker(sheet: Image.Image, box):
    d = ImageDraw.Draw(sheet)
    x0, y0, x1, y1 = box
    for yy in range(y0, y1, 12):
        for xx in range(x0, x1, 12):
            shade = (112, 116, 128) if ((xx - x0) // 12 + (yy - y0) // 12) % 2 else (88, 92, 104)
            d.rectangle((xx, yy, min(xx + 11, x1 - 1), min(yy + 11, y1 - 1)), fill=shade)


def review_sheet(sprites, specs, table, mock, on_page=(), page=None) -> Image.Image:
    """Default and comic pieces side by side, slice proofs at PPU 200, the in-situ composite, the table, the preview,
    the palette with its contrast numbers and the five concept pages. Offline review, not Unity."""
    cw, ch, columns = 600, 262, 3
    rows = math.ceil(len(specs) / columns)
    bg = (20, 24, 44)
    ink = (246, 240, 222)
    sub = (176, 190, 220)
    grid_h = 170 + rows * ch
    sheet = Image.new("RGB", (columns * cw, grid_h + 6400), bg)   # generous; cropped to the content at the end
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype(str(LATIN), 19)
    small = ImageFont.truetype(str(LATIN), 15)
    title = ImageFont.truetype(str(LATIN), 44)
    d.text((24, 20), "COMIC / UI KIT", fill=ink, font=title)
    d.text((400, 18), "爆彩连环漫画", fill=(251, 220, 15), font=ImageFont.truetype(str(HEADING), 48))
    d.text((25, 82), f"{len(specs)} referenced contract sprites   /   DEFAULT | COMIC | COMIC x palette tint (TINT pieces only)",
           fill=ink, font=font)
    d.text((25, 112), "Offline source review: nine-slice, tint and the composite below are Python emulations, not Unity screenshots.",
           fill=sub, font=small)
    d.text((25, 136), "Checker = transparency. TINT = the game multiplies the piece by a palette colour (shown with its main caller's tint).",
           fill=sub, font=small)
    for n, spec in enumerate(specs):
        x, y = (n % columns) * cw, 170 + (n // columns) * ch
        d.line((x + 18, y, x + cw - 18, y), fill=(70, 80, 120), width=2)
        name = spec["name"]
        d.text((x + 20, y + 8), name, fill=ink, font=font)
        w, h = spec["size"]
        border = "/".join(str(int(v)) for v in spec["border"])
        kind = "TINT" if name in NEUTRAL_NAMES else "AUTHORED"
        d.text((x + 20, y + 33), f"{w}x{h}  L/B/R/T {border}  PPU {spec['pixelsPerUnit']:g}  {kind}", fill=sub, font=small)
        boxes = 3 if name in NEUTRAL_NAMES else 2
        bw = (cw - 40 - (boxes - 1) * 10) // boxes
        for col_i in range(boxes):
            box = (x + 20 + col_i * (bw + 10), y + 58, x + 20 + col_i * (bw + 10) + bw, y + 230)
            _checker(sheet, box)
            if col_i == 0:
                with Image.open(ROOT / spec["default"]) as source:
                    im = source.convert("RGBA")
            else:
                im = sprites[name].copy()
                if col_i == 2:
                    key = TINT_PROOF.get(name, "panel")
                    im = tint(im, col(PALETTE[key]) if key in PALETTE else col(key))
            im.thumbnail((bw - 8, 164), Image.Resampling.LANCZOS)
            sheet.paste(im, (box[0] + (bw - im.width) // 2, box[1] + (172 - im.height) // 2), im)
            label = ("DEFAULT", "COMIC", "x " + TINT_PROOF.get(name, "panel"))[col_i]
            d.text((box[0], y + 236), label, fill=sub, font=small)
    y = grid_h + 20
    d.text((24, y), "IN-SITU OFFLINE COMPOSITE / play screen at W11 design sizes (PLAY_LAYOUT 1, 8) on table_scene.png; "
                    "W20X tokens drawn as comic sets them (ink steps, yellow pool tab)", fill=ink, font=font)
    sheet.paste(mock.resize((1752, 986), Image.Resampling.LANCZOS), (24, y + 36))
    y += 1040
    d.text((24, y), "SMALL SIZES / icons and marks at their display size on their real host colour; lower row 2x nearest",
           fill=ink, font=font)
    proof_small = small_size_proof(sprites)
    sheet.paste(proof_small, (24, y + 34))
    y += 34 + proof_small.height + 30
    d.text((24, y), "STAGE STATES / normal, pressure, boss: stage tag, stage card header and rings, tray accent ring "
           "(as GameApp.Play11 draws them)", fill=ink, font=font)
    variants = state_variants(sprites, specs, table)
    sheet.paste(variants, (24, y + 34))
    y += 34 + variants.height + 30
    d.text((24, y), "SIZE PROOFS / on the red page at PPU 200 and real sizes (units = 1080p px): UiKit.Button at 60, 72 "
           "and 80 units (face h - 7 on the step, drawn ink as the coming token), SolidButton (chip), ghost, chips, panels",
           fill=ink, font=font)
    proofs = size_proofs(sprites, specs)
    sheet.paste(proofs, (24, y + 34))
    y += 34 + proofs.height + 40
    page_note = ("ON THE PAGE: " + "; ".join(f"{i['what']} {i['pixels']} px, {i['vs page']:.2f}:1 / {i['vs dots']:.2f}:1"
                                             for i in on_page)) if on_page else \
        "ON THE PAGE: the composite draws no word or mark straight on the red page (all sit on surfaces or the felt)"
    d.text((24, y), page_note, fill=sub, font=small)
    y += 30
    d.text((24, y), "TABLE / 1920 x 1080 (calm zones outlined) and PREVIEW / 480 x 270", fill=ink, font=font)
    proof = table.copy()
    pd = ImageDraw.Draw(proof)
    for zx, zy, zw, zh in FELT_ZONES.values():
        pd.rectangle((zx, zy, zx + zw - 1, zy + zh - 1), outline=(255, 255, 255), width=3)
    sheet.paste(proof.resize((1152, 648), Image.Resampling.LANCZOS), (24, y + 36))
    sheet.paste(preview(mock), (1200, y + 36))
    sy = y + 330
    rep = palette_report()
    tokens = dict(PALETTE, **W20X_TOKENS)
    for i, key in enumerate(tokens):
        xx, yy = 1200 + (i % 4) * 140, sy + (i // 4) * 54
        d.rectangle((xx, yy, xx + 34, yy + 34), fill=rgb8(tokens[key]), outline=(0, 0, 0))
        d.text((xx + 40, yy), key, fill=ink, font=small)
        d.text((xx + 40, yy + 17), tokens[key], fill=sub, font=small)
    y = max(y + 36 + 648 + 24, sy + 54 * math.ceil(len(tokens) / 4) + 12)
    lines = [f"{k} {v:.2f}" for k, v in rep.items()]
    for i in range(0, len(lines), 3):
        d.text((24, y + (i // 3) * 22), "     ".join(lines[i:i + 3]), fill=sub, font=small)
    y += 22 * math.ceil(len(lines) / 3) + 30
    if page is not None:
        d.text((24, y), "PAGE SCENE / Table/page_scene.png for the non-play screens (THEME_SPEC 3, W20X): the plain page; "
                        "right: 1:1 crop", fill=ink, font=font)
        sheet.paste(page.resize((768, 432), Image.Resampling.LANCZOS), (24, y + 36))
        sheet.paste(page.crop((0, 0, 432, 432)), (816, y + 36))
        y += 36 + 432 + 30
    d.text((24, y), "CONCEPT PAGES / the style authority (style only; texts, numbers and layouts are not a contract)", fill=ink, font=font)
    for i, name in enumerate(CONCEPT_PAGES):
        with Image.open(ROOT / CONCEPTS / (name + ".png")) as source:
            ref = source.convert("RGB")
        ref.thumbnail((340, 230), Image.Resampling.LANCZOS)
        sheet.paste(ref, (24 + i * 352, y + 36))
        d.text((24 + i * 352, y + 270), name.upper(), fill=ink, font=font)
    if y + 310 > sheet.height:
        raise ValueError("Contact sheet content overflowed its canvas")
    return sheet.crop((0, 0, sheet.width, y + 310))


# ---------------------------------------------------------------------------------------------- card template review
# Offline review of the card template (not a Unity capture and not part of build()): the shipped ring and gems of comic
# and of Swiss, tinted with each theme's rarity colours (Ghost for goods), composited as GameApp.Card11 DrawCard13 lays
# them out on the W14 code: shop (220 x 348), table (216 x 244), compact (280 x 117), goods rows with their price button
# (452 x 103, 448 x 121) and tiles (100 x 104), at 1080p and 720p. Sprites are sampled like the game's UI sprites
# (bilinear, no mipmaps) and blended in linear light; text and pictures are stand-ins.
CARD_REVIEW = REVIEW / "card_template_review.png"
CARD_REVIEW_CARDS = (  # (rarity or None for goods, name, short text, StyleKit anchor picture)
    ("common", "排头兵", "第一颗提交的骰子点数×4，加到基础分。", "C108"),
    ("advanced", "蓝色浪潮", "本手提交时，提交的骰子全部按蓝色计算。", "I10"),
    ("rare", "点金手", "每次提交后，最后一颗骰子朝上的面永久镀金。", "C117"),
    (None, "配重", "这一面权重+1（上限5），更容易掷出这一面。", "U10"),
)
CARD_REVIEW_THEMES = ("comic", "swiss")
TABLE_COLOUR = {"comic": "FC2830", "swiss": "F9EADB"}   # what the play row's cards sit on: comic's red page


def _lin_texture(im: Image.Image) -> np.ndarray:
    a = np.asarray(im.convert("RGBA")).astype(np.float32) / 255.0
    out = np.empty_like(a)
    out[..., 3] = a[..., 3]
    out[..., :3] = _lin_arr(a[..., :3]) * a[..., 3:]
    return out


def _slice_axis(n_dst: int, n_src: int, lo: float, hi: float, lo_dst: float, hi_dst: float) -> np.ndarray:
    x = np.arange(n_dst, dtype=np.float64) + .5
    if lo_dst + hi_dst > n_dst:
        k = n_dst / (lo_dst + hi_dst)
        lo_dst, hi_dst = lo_dst * k, hi_dst * k
    u = lo + (x - lo_dst) / max(n_dst - lo_dst - hi_dst, 1e-6) * (n_src - lo - hi)
    head, tail = x < lo_dst, x > n_dst - hi_dst
    u[head] = x[head] / max(lo_dst, 1e-6) * lo
    u[tail] = n_src - (n_dst - x[tail]) / max(hi_dst, 1e-6) * hi
    return u


def _bilinear(tex: np.ndarray, us: np.ndarray, vs: np.ndarray) -> np.ndarray:
    h, w = tex.shape[:2]
    x, y = us - .5, vs - .5
    x0, y0 = np.floor(x).astype(int), np.floor(y).astype(int)
    fx, fy = (x - x0).astype(np.float32), (y - y0).astype(np.float32)
    rows = tex[:, np.clip(x0, 0, w - 1)] * (1 - fx)[None, :, None] + tex[:, np.clip(x0 + 1, 0, w - 1)] * fx[None, :, None]
    return rows[np.clip(y0, 0, h - 1)] * (1 - fy)[:, None, None] + rows[np.clip(y0 + 1, 0, h - 1)] * fy[:, None, None]


class UnityCanvas:
    """Linear-light premultiplied canvas in screen pixels; `scale` is screen px per design unit (1080p 1, 720p 2/3)."""

    def __init__(self, w: int, h: int, background: str, scale: float = 1.0):
        self.scale = scale
        self.buf = np.zeros((h, w, 4), np.float32)
        self.buf[..., :3] = _lin_arr(col(background))
        self.buf[..., 3] = 1.0

    def _over(self, layer: np.ndarray, x0: int, y0: int):
        H, W = self.buf.shape[:2]
        xs, ys = max(0, x0), max(0, y0)
        xe, ye = min(W, x0 + layer.shape[1]), min(H, y0 + layer.shape[0])
        if xe > xs and ye > ys:
            src = layer[ys - y0:ye - y0, xs - x0:xe - x0]
            self.buf[ys:ye, xs:xe] = src + self.buf[ys:ye, xs:xe] * (1 - src[..., 3:4])

    def _rect(self, x, y, w, h):
        s = self.scale
        x0, y0 = int(round(x * s)), int(round(y * s))
        return x0, y0, max(1, int(round((x + w) * s)) - x0), max(1, int(round((y + h) * s)) - y0)

    def sprite(self, im: Image.Image, x, y, w, h, tint=None, border=None, ppu: float = 200.0):
        x0, y0, W, H = self._rect(x, y, w, h)
        tex = _lin_texture(im)
        th, tw = tex.shape[:2]
        if border is not None and any(border):
            l, b, r, t = border
            k = 100.0 / ppu * self.scale
            us, vs = _slice_axis(W, tw, l, r, l * k, r * k), _slice_axis(H, th, t, b, t * k, b * k)
        else:
            us, vs = (np.arange(W) + .5) / W * tw, (np.arange(H) + .5) / H * th
        layer = _bilinear(tex, us, vs)
        if tint is not None:
            layer[..., :3] *= _lin_arr(np.asarray(tint, np.float32))
        self._over(layer, x0, y0)

    def picture(self, im: Image.Image, x, y, size):
        x0, y0, S, _ = self._rect(x, y, size, size)
        pic = im.convert("RGBA")
        pic.thumbnail((S, S), Image.Resampling.LANCZOS)
        self._over(_lin_texture(pic), x0 + (S - pic.width) // 2, y0 + (S - pic.height) // 2)

    def text(self, x, y, value: str, size: float, colour: str, font, anchor: str = "la"):
        f = ImageFont.truetype(str(font), max(6, int(round(size * self.scale))))
        box = ImageDraw.Draw(Image.new("L", (1, 1))).textbbox((0, 0), value, font=f, anchor=anchor)
        layer = Image.new("RGBA", (box[2] - box[0] + 4, box[3] - box[1] + 4), (0, 0, 0, 0))
        ImageDraw.Draw(layer).text((2 - box[0], 2 - box[1]), value, font=f, fill=rgb8(colour) + (255,), anchor=anchor)
        self._over(_lin_texture(layer), int(round(x * self.scale)) + box[0] - 2, int(round(y * self.scale)) + box[1] - 2)

    def image(self) -> Image.Image:
        rgb = _gam_arr(self.buf[..., :3] / np.maximum(self.buf[..., 3:4], 1e-6))
        return Image.fromarray(np.clip(np.rint(rgb * 255), 0, 255).astype(np.uint8), "RGB")


def frame_insets_px(pixels: np.ndarray, border) -> tuple[int, int, int, int]:
    """GameApp.FrameInsets11 (left, bottom, right, top) in sprite px. Unity rows run up."""
    up = pixels[::-1].astype(np.int32)
    h, w = up.shape[:2]
    fill = up[h // 2, w // 2]

    def inset(get, count, limit):
        last = 0
        for i in range(min(count // 2, int(round(limit)))):
            c = get(i)
            if c[3] < 128 or int(np.abs(c[:3] - fill[:3]).sum()) > 48:
                last = i + 1
        return last
    l, b, r, t = border
    return (inset(lambda i: up[h // 2, i], w, l), inset(lambda i: up[i, w // 2], h, b),
            inset(lambda i: up[h // 2, w - 1 - i], w, r), inset(lambda i: up[h - 1 - i, w // 2], h, t))


def ring_insets_px(pixels: np.ndarray, border) -> tuple[int, int, int, int]:
    """GameApp.Card11 RingInsets13 (left, bottom, right, top) in sprite px."""
    up = pixels[::-1]
    h, w = up.shape[:2]

    def inset(get, count, limit):
        last = 0
        for i in range(min(count // 2, int(round(limit)))):
            if get(i)[3] >= 128:
                last = i + 1
        return last
    l, b, r, t = border
    return (inset(lambda i: up[h // 2, i], w, l), inset(lambda i: up[i, w // 2], h, b),
            inset(lambda i: up[h // 2, w - 1 - i], w, r), inset(lambda i: up[h - 1 - i, w // 2], h, t))


def _wrap(text: str, font, size: float, width: float) -> list[str]:
    """Greedy CJK wrap; closing punctuation never starts a line: it takes the previous character down with it."""
    f = ImageFont.truetype(str(font), int(round(size)))
    lines, line = [], ""
    for ch in text:
        if line and f.getlength(line + ch) > width:
            if ch in "。，、；：！？）》”" and len(line) > 1:
                lines.append(line[:-1])
                line = line[-1] + ch
            else:
                lines.append(line)
                line = ch
        else:
            line += ch
    return lines + [line] if line else lines


PRICE_H = 60.0   # W14 E1 (PriceButtonMin14): every price button is at least 60 units high


def _price_button(c: UnityCanvas, kit, borders, palette, x, y, w, h, price: int = 8):
    """PriceButton13 on one row: SolidButton11 (ui_chip_formula times the code's blend of Background and Panel) with
    the coin, the price in gold and the word, centred."""
    sec = col(palette["background"]) * .45 + col(palette["panel"]) * .55
    c.sprite(kit["ui_chip_formula"], x, y, w, h, tint=sec, border=borders["ui_chip_formula"])
    coin, word = 24, "购买"
    total = coin + 4 + 16 + 8 + 40
    sx = x + (w - total) / 2
    c.sprite(kit["ui_icon_coin"], sx, y + (h - coin) / 2, coin, coin)
    c.text(sx + coin + 4, y + h / 2, str(price), 22, palette["gold"], FONT, "lm")
    c.text(sx + coin + 28, y + h / 2, word, 20, palette["textColor"], FONT, "lm")


def draw_card13(c: UnityCanvas, kit: dict, borders: dict, palette: dict, x, y, w, h, size: str, rarity, title, text, art,
                category: str = "骰面改造", price_w: float = 0.0, ordinal: int = 0):
    """The ring and gem as GameApp.Card11 places them on the W14 code (CARD_TEMPLATE 2, rows 15-17): DrawTallCard13 for
    shop and table, DrawCompactCard13 for compact and for a priced goods row (E2), DrawTile13 for a tile (E3), with
    stand-in name, picture, short text and price button. All lengths in 1080p design units."""
    goods = rarity is None
    face_name = "ui_card_shop" if size == "shop" else "ui_card_controller_compact" if size in ("compact", "goods", "tile") \
        else "ui_card_controller"
    face = kit[face_name]
    mult = 2.0 if size == "tile" else 1.0   # W14 E3: the tile draws its face at half the frame thickness
    c.sprite(face, x, y, w, h, border=borders[face_name], ppu=200.0 * mult)
    fl, fb, fr, ft = (v / (2 * mult) for v in frame_insets_px(np.asarray(face), borders[face_name]))
    inner = (x + fl, y + ft, w - fl - fr, h - ft - fb)
    ring = kit["ui_card_frame_rarity"]
    rl, rb, rr, rt = (v / 2 for v in ring_insets_px(np.asarray(ring), borders["ui_card_frame_rarity"]))
    key = {"common": "rarityCommon", "advanced": "rarityAdvanced", "rare": "rarityRare", None: "ghost"}[rarity]
    tint_c = col(palette[key])
    ink = palette["ink"]
    if size == "tile":
        content = (inner[0] + 2, inner[1] + 2, inner[2] - 4, inner[3] - 4)
        name_h = math.ceil(19.5 * 1.46) + 2
        lead = 22 if ordinal else 0
        block = max(24, min(content[2] - lead, content[3] - name_h - 2))
        bx = content[0] + lead if ordinal else content[0] + (content[2] - block) / 2
        by = content[1]
        c.sprite(ring, bx, by, block, block, tint=tint_c, border=borders["ui_card_frame_rarity"])
        c.picture(art, bx + rl + 1, by + rt + 1, min(block - rl - rr, block - rt - rb) - 2)
        if not goods:
            gem = min(16, block * .3)
            c.sprite(kit["ui_gem_" + rarity], bx + block / 2 - gem / 2, by + block - gem - 1, gem, gem, tint=tint_c)
        if ordinal:
            c.sprite(kit["ui_pip_dot"], content[0], content[1], 20, 20, tint=col(palette["ink"]))
            c.text(content[0] + 10, content[1] + 10, str(ordinal), 12, palette["paper"], FONT, "mm")
        c.text(content[0] + content[2] / 2, content[1] + content[3] - name_h / 2, title, 19.5, ink, FONT, "mm")
        return
    if size in ("compact", "goods"):
        content = (inner[0] + 4, inner[1] + 4, inner[2] - 8, inner[3] - 8)
        priced = size == "goods"
        pw = max(price_w, 100) if priced else 0
        limit = content[0] + content[2] - (pw + 8 if priced else 0)
        block, gem_size = min(content[3] - 8, 76), 18
        bx, by = content[0], content[1] + max(0, (content[3] - block - gem_size * .5) * .5)
        c.sprite(ring, bx, by, block, block, tint=tint_c, border=borders["ui_card_frame_rarity"])
        pic = (bx + rl, by + rt, block - rl - rr, block - rt - rb)
        c.picture(art, pic[0] + 1, pic[1] + 1, min(pic[2], pic[3]) - 2)
        if not goods:
            c.sprite(kit["ui_gem_" + rarity], bx + block * .5 - gem_size * .5, by + block - gem_size * .5, gem_size,
                     gem_size, tint=tint_c)
        tx, ty = bx + block + 10, content[1] + 2
        if goods:
            c.text(tx, ty, category, 19.5 if priced else 15, palette["ink"], BODY_FONT)
            ty += 26
        c.text(tx, ty, title, 20, ink, FONT)
        # The short text shows only when it fits whole (PLAY_LAYOUT 8 row 14; W14 E2 keeps the rule for goods rows).
        lines = _wrap(text, BODY_FONT, 19.5 if priced else 18, limit - tx)
        if len(lines) * 24 <= content[1] + content[3] - (ty + 32):
            for i, line in enumerate(lines):
                c.text(tx, ty + 32 + i * 24, line, 19.5 if priced else 18, ink, BODY_FONT)
        if priced:
            _price_button(c, kit, borders, palette, content[0] + content[2] - pw, content[1] + max(0, (content[3] - PRICE_H) / 2),
                          pw, PRICE_H)
        return
    c.sprite(ring, *inner, tint=tint_c, border=borders["ui_card_frame_rarity"])
    content = (inner[0] + rl + 4, inner[1] + rt + 3, inner[2] - rl - rr - 8, inner[3] - rt - rb - 6)
    gem_size = 34 if size == "shop" else 30
    gem_y = inner[1] + (content[1] - 3 - inner[1]) * .5
    top = content[1] if goods else max(content[1], gem_y + gem_size * .5 + 2)
    price = PRICE_H if size == "shop" else 0
    bottom = content[1] + content[3] - (price + 6 if price else 0)
    if goods:
        c.text(x + w / 2, top + 13, category, 16, palette["ink"], BODY_FONT, "mm")
        top += 26
    name_h = math.ceil(22 * 1.46) + 2
    art_top = top + name_h + 2
    lines = _wrap(text, BODY_FONT, 20, content[2])
    art_size = max(40, min(content[2] - 8, bottom - art_top - 4 - (len(lines) * 26 + 2)))
    c.picture(art, x + (w - art_size) * .5, art_top, art_size)
    c.text(x + w / 2, top + name_h / 2, title, 22, ink, FONT, "mm")
    for i, line in enumerate(lines):
        c.text(x + w / 2, art_top + art_size + 4 + i * 26, line, 20, ink, BODY_FONT, "ma")
    if price:
        _price_button(c, kit, borders, palette, content[0], content[1] + content[3] - price, content[2], price)
    if not goods:
        c.sprite(kit["ui_gem_" + rarity], x + w * .5 - gem_size * .5, gem_y - gem_size * .5, gem_size, gem_size, tint=tint_c)


CARD_REVIEW_GOODS = (  # W14 E2 goods rows: (category, name, short text, anchor, row width, price width, row height)
    ("道具", "蓝色浪潮", "下一次有效提交全部骰子临时视为蓝色。", "I10", 452, 180, 103),
    ("骰面改造", "配重", "这一面权重+1（上限5），更容易掷出这一面。", "U10", 448, 150, 121),
)


def _section_fill(kit: dict, palette: dict) -> str:
    """The shop section's centre as drawn: ui_section_panel times the theme's W20X `section` colour, or times Panel where a
    theme sets none (GameApp.Shop14 Section14 today)."""
    px = np.asarray(kit["ui_section_panel"])
    return drawn(hexc(px[px.shape[0] // 2, px.shape[1] // 2, :3] / 255.0), palette.get("section", palette["panel"]))


def card_template_review() -> Image.Image:
    """Comic beside Swiss on the W14 code: shop cards (220 x 348 with the 60-unit price button), table and compact cards
    (common, advanced, rare, goods), the shop's goods rows with their price buttons (452 x 103, 448 x 121) and the tiles of
    我的构筑 (100 x 104), at 1080p and 720p; then the three gems at 12-34 px on the card's own top frame."""
    specs = {s["name"]: s for s in json.loads(CONTRACT.read_text(encoding="utf-8"))["sprites"]}
    names = ("ui_card_shop", "ui_card_controller", "ui_card_controller_compact", "ui_chip_formula", "ui_section_panel",
             "ui_icon_coin", "ui_pip_dot") + CARD_TEMPLATE_NAMES
    borders = {n: specs[n]["border"] for n in names}
    themes = {}
    for theme in CARD_REVIEW_THEMES:
        folder = ROOT / "DicePrototype/Assets/Resources/Themes" / theme
        palette = json.loads((folder / "theme.json").read_text(encoding="utf-8"))["palette"]
        kit = {}
        for n in names:
            with Image.open(folder / "UI" / (n + ".png")) as source:
                kit[n] = source.convert("RGBA")
        arts = {}
        for art in {a for *_r, a in CARD_REVIEW_CARDS} | {g[3] for g in CARD_REVIEW_GOODS}:
            with Image.open(ROOT / "ArtSource/StyleKit/themes" / theme / "anchors" / (art + ".png")) as source:
                arts[art] = source.convert("RGBA")
        themes[theme] = (kit, palette, arts)
    panel_w, gap, ink, sub = 960, 40, (246, 240, 222), (176, 190, 220)
    sheet = Image.new("RGB", (2 * panel_w + gap + 48, 4600), (20, 24, 44))   # cropped to the content at the end
    d = ImageDraw.Draw(sheet)
    font, small, title = (ImageFont.truetype(str(LATIN), s) for s in (19, 15, 34))
    d.text((24, 16), "CARD TEMPLATE / W13 + W14 (E1-E3)   -   COMIC (left)   |   SWISS (right)", fill=ink, font=title)
    d.text((24, 60), "Offline emulation of GameApp.Card11 DrawCard13 on the W14 code: sprites sampled bilinear without mipmaps "
                     "(as the theme UI sprites are imported), tinted and blended in linear light. Not a Unity capture.",
           fill=sub, font=small)
    d.text((24, 80), "Names, short texts, pictures (StyleKit anchors) and price buttons are stand-ins. Goods (no rarity): Ghost "
                     "ring, no gem. Table cards on the table colour, shop sizes on the drawn shop section: panel x section "
                     "(W20X; comic) or x Panel where a theme sets none (swiss, the W14 code today).",
           fill=sub, font=small)
    keys = {"common": "rarityCommon", "advanced": "rarityAdvanced", "rare": "rarityRare", None: "ghost"}
    hosts = {theme: {"table": TABLE_COLOUR[theme], "shop": _section_fill(themes[theme][0], themes[theme][1])}
             for theme in CARD_REVIEW_THEMES}
    y = 112
    for scale, res in ((1.0, "1080p"), (2 / 3, "720p")):
        sections = (("SHOP 220 x 348 (E1: 60-unit price button)", "shop", 220, 348, "shop"),
                    ("TABLE 216 x 244", "table", 216, 244, "table"), ("COMPACT 280 x 117", "compact", 280, 117, "table"),
                    ("GOODS ROWS 452 x 103 (price 180) and 448 x 121 (price 150), E2", "goods", 0, 0, "shop"),
                    ("TILES 100 x 104 (E3: face at half its frame)", "tile", 100, 104, "shop"))
        for label, size, cw, chh, host in sections:
            if size == "goods":
                items = [(None, name, text, art, cat, rw, pw, rh) for cat, name, text, art, rw, pw, rh in CARD_REVIEW_GOODS]
                ph = 8 + sum(round(rh * scale) + 30 for *_r, rh in items)
            else:
                items = [(r, n, t, a, "骰面改造", cw, 0, chh) for r, n, t, a in CARD_REVIEW_CARDS]
                if size == "tile":
                    items = items[:3]
                sw, sh = round(cw * scale), round(chh * scale)
                per_row = max(1, min(4, (panel_w - 8) // (sw + 16)))
                ph = 8 + math.ceil(len(items) / per_row) * (sh + 30)
            for p, theme in enumerate(CARD_REVIEW_THEMES):
                kit, palette, arts = themes[theme]
                px = 24 + p * (panel_w + gap)
                d.text((px, y), f"{theme.upper()}   {label}   {res}", fill=ink, font=font)
                background = hosts[theme][host]
                canvas = UnityCanvas(panel_w, ph, background, scale)
                note = (246, 240, 222) if luminance(background) < .3 else (70, 64, 58)
                labels = []
                oy = 8
                for i, (rarity, name, text, art, cat, iw, pw, ih) in enumerate(items):
                    if size == "goods":
                        sx, sy = 8, oy
                        oy += round(ih * scale) + 30
                    else:
                        sx, sy = 8 + (i % per_row) * (sw + 16), 8 + (i // per_row) * (sh + 30)
                    draw_card13(canvas, kit, borders, palette, sx / scale, sy / scale, iw, ih, size, rarity, name, text,
                                arts[art], category=cat, price_w=pw, ordinal=i + 1 if size == "tile" else 0)
                    note_text = (rarity or "goods") if size == "tile" else f"{rarity or 'goods'}  {palette[keys[rarity]]}"
                    labels.append((sx, sy + round(ih * scale) + 5, note_text))
                sheet.paste(canvas.image(), (px, y + 26))
                for sx, sy, text_ in labels:
                    d.text((px + sx, y + 26 + sy), text_, fill=note, font=small)
            y += 26 + ph + 18
    sizes = (12, 18, 20, 22, 23, 30, 34)
    for p, theme in enumerate(CARD_REVIEW_THEMES):
        kit, palette, _ = themes[theme]
        px = 24 + p * (panel_w + gap)
        d.text((px, y), f"{theme.upper()}   GEMS at {' / '.join(map(str, sizes))} px on the card's top frame and ring; right: 2x nearest",
               fill=ink, font=font)
        face = kit["ui_card_controller"]
        fl, fb, fr, ft = (v / 2 for v in frame_insets_px(np.asarray(face), borders["ui_card_controller"]))
        ring = kit["ui_card_frame_rarity"]
        rl, rb, rr, rt = (v / 2 for v in ring_insets_px(np.asarray(ring), borders["ui_card_frame_rarity"]))
        strip = UnityCanvas(len(sizes) * 42 + 16, 3 * 60 + 8, TABLE_COLOUR[theme], 1.0)
        for k, rarity in enumerate(("common", "advanced", "rare")):
            gy = 8 + k * 60 + 24
            top = gy - (ft + rt / 2)
            strip.sprite(face, -20, top, len(sizes) * 42 + 56, 120, border=borders["ui_card_controller"])
            strip.sprite(ring, -20 + fl, top + ft, len(sizes) * 42 + 56 - fl - fr, 120 - ft - fb, tint=col(palette[keys[rarity]]),
                         border=borders["ui_card_frame_rarity"])
            for j, s in enumerate(sizes):
                strip.sprite(kit["ui_gem_" + rarity], 8 + j * 42 + 21 - s / 2, gy - s / 2, s, s, tint=col(palette[keys[rarity]]))
        im = strip.image()
        sheet.paste(im, (px, y + 26))
        big = im.resize((im.width * 2, im.height * 2), Image.Resampling.NEAREST)
        sheet.paste(big.crop((0, 0, min(big.width, panel_w - im.width - 16), big.height)), (px + im.width + 16, y + 26))
    y += 26 + 2 * (3 * 60 + 8) + 20
    return sheet.crop((0, 0, sheet.width, y))


def write_card_template_review(output_root: Path = ROOT) -> Path:
    path = Path(output_root).resolve() / CARD_REVIEW
    path.parent.mkdir(parents=True, exist_ok=True)
    card_template_review().save(path, format="PNG", optimize=False, compress_level=9)
    return path


# ---------------------------------------------------------------------------------------------- build
def theme_json() -> str:
    data = dict(THEME_META)
    data["palette"] = dict(PALETTE, **W20X_TOKENS)
    return json.dumps(data, ensure_ascii=False, indent=2) + "\n"


def build(output_root: Path = ROOT) -> list[Path]:
    """Write the referenced contract sprites, table, felt tile, preview, contact sheet and theme.json."""
    output_root = Path(output_root).resolve()
    if not output_root.is_relative_to(ROOT):
        raise ValueError("Output root must stay inside the comic worktree")
    written = []

    def save(relative: Path, im: Image.Image):
        path = output_root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        # Explicit parameters, no timestamp/EXIF/ICC or machine-specific metadata.
        im.save(path, format="PNG", optimize=False, compress_level=9)
        written.append(path)

    table = table_scene()
    check_felt_zones(table)  # Fail before writing any exported art.
    specs = contract_sprites()
    sprites: dict[str, Image.Image] = {}
    for spec in specs:
        if spec["name"] == "ui_card_controller_compact" and "ui_card_controller" in sprites:
            im = sprites["ui_card_controller"].copy()
        else:
            im = render_sprite(spec)
        if im.mode != "RGBA" or list(im.size) != spec["size"]:
            raise ValueError("Contract mismatch: " + spec["name"])
        sprites[spec["name"]] = im
        save(THEME / "UI" / (spec["name"] + ".png"), im)
    save(THEME / "Table/table_scene.png", table)
    page = page_scene()
    save(THEME / "Table/page_scene.png", page)
    save(THEME / "Table/felt_tile.png", felt_tile())
    on_page: list[dict] = []
    mock = play_mock(sprites, specs, table, on_page)
    for item in on_page:
        print(f"COMIC_ON_PAGE {item['what']}: {item['pixels']} px of {item['colour']} on the page, "
              f"{item['vs page']:.2f}:1 to the page red, {item['vs dots']:.2f}:1 to its dots")
    if not on_page:
        print("COMIC_ON_PAGE none: the composite draws no word or mark straight on the red page")
    save(THEME / "preview.png", preview(mock))
    save(REVIEW / "contact_sheet.png", review_sheet(sprites, specs, table, mock, on_page, page))
    theme_path = output_root / THEME / "theme.json"
    theme_path.write_text(theme_json(), encoding="utf-8", newline="\n")
    written.append(theme_path)
    return written


if __name__ == "__main__":
    import sys
    import time
    if "--card-review" in sys.argv:
        # Reads the shipped comic and Swiss sprites; run it after a build.
        print("COMIC_CARD_REVIEW", write_card_template_review().relative_to(ROOT).as_posix())
        raise SystemExit(0)
    started = time.time()
    paths = build()
    specs = contract_sprites()
    pngs = [p for p in paths if p.suffix == ".png"]
    print(f"COMIC_BUILT {len(specs)} UI sprites / {len(pngs)} PNGs + theme.json / supersampling {SS}x / "
          f"{time.time() - started:.1f}s")
    for key, value in palette_report().items():
        print(f"COMIC_PALETTE {key} {value:.3f}")
