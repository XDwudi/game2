"""W20: deterministic 爆彩连环漫画 (comic) UI kit: flat pop-art comic surfaces, 4x raster, one Lanczos downsample.

Style authority: the five user-chosen concept pages in ArtSource/StyleKit/themes/comic/concepts and the palette samples
of ArtSource/StyleKit/themes/comic/style_kit.json. Their texts, numbers and layouts are not a contract; the game layout
is (docs/specs/v11/PLAY_LAYOUT.md). The contract (tools/art_v11/theme_contract.json) owns pixel sizes, nine-slice
borders and PPU. Repository fonts only; no image generation, no Unity files.

The shape language (not the default's steps, Swiss's four cut corners or sancai's round glaze):
- thick ink outlines and flat colour fills, cel shading in flat steps;
- a hard offset drop shadow down and to the right under panels, cards, buttons and chips;
- comic-panel frames: ink line, a cream gutter band, an ink line, then the fill;
- buttons, chips and tags are sheared (a "/" lean: the top-left and bottom-right corners cut on a steep slant);
- Ben-Day halftone dots, starbursts and yellow speed rays as accents in corners, on the table's edges, the card back
  and the larger icons.

Rules the art keeps (checked by tests/test_art_v11_comic.py):
- every nine-slice centre is one flat colour (or transparent) from the border line inward;
- surfaces the game stretches keep their edge bands invariant along the edge; halftone and other detail live in the
  corner squares, which are never stretched;
- sprites the code multiplies by a palette colour (NEUTRAL_NAMES) stay achromatic: white faces, black ink and shadows;
- the protected table zones are exactly the felt colour;
- the W13 card template: a rarity ring no wider than the default's, and three gems whose shapes (round, diamond,
  six-point star) tell the rarity apart without colour.

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
THEME_META = {
    "id": "comic",
    "displayName": "爆彩连环漫画",
    "description": ("粗黑墨线、平涂的红黄蓝与米白报纸色：斜切的标签与按钮带右下硬投影，漫画分格框、网点、爆炸星与速度线只放在边角与装饰；"
                    "蓝色桌面，黄色主按钮。依据：ArtSource/StyleKit/themes/comic/concepts 与 style_kit.json；界面件与色板由 "
                    "tools/art_v11/comic/build_comic.py 生成（说明见同目录 README.md）"),
    "order": 3,
    "preview": True,
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
    """The kit's sheared box: a "/" lean made by cutting the top-left and bottom-right corners on a steep slant."""
    x0, y0, x1, y1 = box
    return [(x0 + cut_w, y0), (x1, y0), (x1, y1 - cut_h), (x1 - cut_w, y1), (x0, y1), (x0, y0 + cut_h)]


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


def comic_tile(w: int, h: int, *, shape: str = "rect", cut=(0.0, 0.0), ink: float = 5.0, bands=(), fill=WHITE,
               shadow: float = 8.0, hollow: bool = False, inset: float = 1.0, depth: int = 48, tinted: bool = True,
               shadow_alpha: float = 1.0, accent=None) -> Image.Image:
    """A nine-sliced comic surface. tinted: black ink and shadow (the sprite stays achromatic); otherwise ink colour."""
    r = Raster(w, h)
    k = K if tinted else INK
    box = (inset, inset, w - inset - shadow, h - inset - shadow)
    pts = sheared(box, *cut) if shape == "sheared" else None
    sdf = (lambda X, Y: sd_poly(X, Y, pts)) if pts else (lambda X, Y: sd_box(X, Y, box))
    _band_render(r, w, h, depth, lambda win: comic_layers(
        win, sdf, ink=ink, bands=bands, fill=fill, shadow=shadow, ink_rgb=k, shadow_rgb=k, shadow_alpha=shadow_alpha,
        hollow=hollow, extra=accent), None if hollow else fill)
    return r.export()


CORNER_REACH = 34.0   # px: accents stay this close to a sprite corner, so the 40 px corner square holds them whole


def in_corner(X, Y, cx: float, cy: float) -> np.ndarray:
    """1 inside the corner square at (cx, cy) (a sprite corner), less the Lanczos footprint; 0 elsewhere."""
    return ((np.abs(X - np.float32(cx)) < CORNER_REACH) & (np.abs(Y - np.float32(cy)) < CORNER_REACH)).astype(np.float32)


def corner_dots(colour, corner: str, reach: float = 48.0, spacing: float = 7.0, rmax: float = 3.0, angle: float = 45.0,
                w: int = 512, h: int = 512):
    """Accent: a Ben-Day dot fade on the fill in one corner (dots shrink away from it), kept inside the corner square."""
    def extra(win: Raster, d, total):
        X, Y = win.X, win.Y
        cx = float(w) if corner[1] == "r" else 0.0
        cy = float(h) if corner[0] == "b" else 0.0
        t = 1 - np.clip((np.abs(X - np.float32(cx)) + np.abs(Y - np.float32(cy))) / np.float32(reach), 0, 1)
        dots = halftone(X, Y, spacing, rmax * smoothstep(.0, .6, t), angle)
        win.over(colour, dots * coverage(d + total + 1.0) * in_corner(X, Y, cx, cy))
    return extra


# ---------------------------------------------------------------------------------------------- sliced surfaces
PANEL_FILL = NAVY
SECTION_FILL = col("03276A")
INSET_FILL = NAVY_DK
SLOT_FILL = col("021638")
LABEL_FILL = CREAM
SHADOW = 8.0


def card(frame, dots, band: float = 9.0) -> Image.Image:
    """Card face: ink, a coloured frame band with Ben-Day corners, a thin ink line, flat cream; hard shadow."""
    def accent(win, d, total):
        # Dots on the frame band only, fading out of the top-left and bottom-right corners.
        X, Y = win.X, win.Y
        on_band = coverage(d + 5.0) * (1 - coverage(d + 5.0 + band))
        for cx, cy in ((0.0, 0.0), (512.0, 512.0)):
            t = 1 - np.clip((np.abs(X - cx) + np.abs(Y - cy)) / 60.0, 0, 1)
            win.over(dots, halftone(X, Y, 6.0, 2.7 * smoothstep(0, .55, t), 45.0) * on_band * in_corner(X, Y, cx, cy))
    return comic_tile(512, 512, ink=5.0, bands=((band, frame), (2.0, INK)), fill=CREAM, tinted=False, accent=accent)


DOT_ON_NAVY = col("3A7DEB")


def dark_panel(fill, top_band: float = 6.0, corner: str = "tr") -> Image.Image:
    return comic_tile(512, 512, ink=5.0, bands=((top_band, CREAM), (2.0, INK)), fill=fill, tinted=False,
                      accent=corner_dots(DOT_ON_NAVY, corner))


def surface(spec: dict) -> Image.Image:
    """Nine-sliced and horizontally sliced surfaces. Frames stay in the border bands, centres stay flat."""
    name = spec["name"]
    w, h = spec["size"]
    if name == "ui_paper_panel":
        return comic_tile(w, h, ink=5.0, fill=CREAM, tinted=False, accent=corner_dots(col("F2B6B0"), "tr"))
    if name in ("ui_card_controller", "ui_card_controller_compact"):
        return card(YELLOW, RED)
    if name == "ui_card_item":
        return card(RED, RED_SH)
    if name == "ui_card_shop":
        return card(YELLOW, BLUE, band=7.0)
    if name == "ui_chip_label":
        return comic_tile(w, h, shape="sheared", cut=(12.0, 24.0), ink=4.0, fill=LABEL_FILL, shadow=5.0, tinted=False)
    if name == "ui_panel_hud":
        return dark_panel(PANEL_FILL)
    if name == "ui_section_panel":
        # Shop sections draw a header strip from 5 units in: the frame stays inside 10 px at the top.
        return comic_tile(w, h, ink=4.0, bands=((3.0, CREAM), (2.0, INK)), fill=SECTION_FILL, tinted=False,
                          accent=corner_dots(DOT_ON_NAVY, "tr"))
    if name == "ui_tab_bottom":
        return dark_panel(PANEL_FILL, corner="tl")
    if name == "ui_info_inset":
        # A well: ink line, the fill, and a hard inner shadow along the top and left (light from the upper left).
        def well(win, d, total):
            X, Y = win.X, win.Y
            edge = np.minimum(Y - np.float32(1.0 + total), X - np.float32(1.0 + total))
            win.over(INK, coverage(d + total) * coverage(edge - 5.0) * .55)
        return comic_tile(w, h, ink=4.0, fill=INSET_FILL, shadow=0.0, tinted=False, accent=well)
    if name == "ui_slot_inventory":
        return comic_tile(w, h, ink=4.0, bands=((2.0, CREAM), (2.0, INK)), fill=SLOT_FILL, shadow=0.0, tinted=False)
    if name == "ui_tray_front":
        return tray_front(w, h)
    # Achromatic, tinted by the game.
    if name == "ui_topbar":
        return comic_tile(w, h, ink=5.0, fill=WHITE, shadow=SHADOW)
    if name == "ui_chip_formula":
        return comic_tile(w, h, shape="sheared", cut=(16.0, 28.0), ink=4.0, fill=WHITE, shadow=6.0)
    if name == "ui_stage_card":
        return comic_tile(w, h, ink=5.0, fill=WHITE, shadow=SHADOW, depth=56,
                          accent=corner_dots(np.full(3, .8, np.float32), "tr", reach=34))
    if name == "ui_dice_tray":
        return comic_tile(w, h, ink=4.0, bands=((14.0, WHITE), (3.0, K)), fill=None, shadow=SHADOW, hollow=True, depth=72)
    if name == "ui_button_bevel":
        # The cut stays clear of the 40 px corner line together with the inner edge of its ink line.
        return comic_tile(w, h, shape="sheared", cut=(18.0, 25.0), ink=5.0, fill=WHITE, shadow=SHADOW)
    if name == "ui_button_ghost":
        return comic_tile(w, h, shape="sheared", cut=(18.0, 25.0), ink=3.0, bands=((5.0, WHITE), (2.0, K)), fill=None,
                          shadow=0.0, hollow=True)
    if name == "ui_ring":
        return comic_tile(w, h, ink=3.0, bands=((6.0, WHITE), (2.0, K)), fill=None, shadow=0.0, hollow=True)
    if name == "ui_frame_enchant":
        # Sits on the cream card face just inside the card's frame, so every enchantment hue reads on cream.
        return comic_tile(w, h, ink=2.0, bands=((4.0, WHITE), (2.0, K)), fill=None, shadow=0.0, hollow=True, inset=19.0)
    if name == "ui_frame_block":
        return comic_tile(w, h, ink=3.0, fill=WHITE, shadow=0.0)
    if name == "ui_price_chip":
        return comic_tile(w, h, shape="sheared", cut=(14.0, 26.0), ink=4.0, fill=WHITE, shadow=5.0)
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


def section_header(w: int, h: int) -> Image.Image:
    """Shop section title band (border 40/0/72/0, drawn 40 units tall): a caption banner whose right end leans "/",
    with a hard shadow. Designed in display units (0.5 x, 0.3125 y units per px) because only the height is scaled."""
    r = Raster(w, h)
    sx, sy = .5, 40.0 / h
    Xu, Yu = r.X * sx, r.Y * sy
    W = w * sx
    # The slanted end lies wholly beyond 222 units (444 px), clear of the 440 px border line.
    body = [(.5, .5), (W - 4.0, .5), (W - 13.0, 36.5), (.5, 36.5)]
    sh = 3.0
    ds = sd_poly(Xu - sh, Yu - sh, body)
    r.over(K, _aniso_coverage(ds))
    d = sd_poly(Xu, Yu, body)
    r.over(K, _aniso_coverage(d))
    r.over(WHITE, _aniso_coverage(d + 2.0))
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
    inner = _aniso_coverage(d + 1.2)
    r.over(NAVY_DK, inner)
    r.over(YELLOW, inner * (Yu < 4.6))
    return r.export()


# ---------------------------------------------------------------------------------------------- card template (W13)
# CARD_TEMPLATE sections 3 and 6: the rarity ring and the three gems, neutral so TintedSprite13 colours them with the
# rarity colours (Ghost for goods). Flat comic cel shading: white is the tint itself, the shade a flat step below it.
RING = (2.0, 6.0, 2.0)   # px: ink, tinted band, ink; the band ends 10 px in (5 units; the default ring is 12 px)
GEM_SIZE = 128
GEM_INK = 9.0


def card_frame_rarity(w: int, h: int) -> Image.Image:
    """Rarity ring (border 40; transparent centre, TintedSprite13 draws it with fillCenter off): an ink line, a flat
    band that takes the rarity colour and an ink line, square-cornered like a comic panel's inner frame."""
    ink, band, inner = RING
    return comic_tile(w, h, ink=ink, bands=((band, WHITE), (inner, K)), fill=None, shadow=0.0, hollow=True, inset=0.0)


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
        dots = halftone(r.X, r.Y, 9.0, 2.6, 30.0)
        r.over(NAVY, dots * lm)
        r.over(NAVY_DK, dots * rm)
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
    edge_t = np.clip(1 - (-di - 6) / 160.0, 0, 1)
    r.over(RED_SH, field * (1 - rays) * halftone(X, Y, 14.0, 5.2 * edge_t, 45.0))
    burst = poly_mask(r, burst_pts(cx, cy, 250.0, 175.0, 14, SEED + 7, .12))
    ink_fill(r, burst, BLUE, 9.0)
    r.over(BLUE_DOT, burst * halftone(X, Y, 12.0, 3.4 * smoothstep(120, 220, rad), 15.0))
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
    r.over(YELLOW_SH, m * halftone(r.X, r.Y, 14.0, 4.2 * smoothstep(1100, 1400, r.X), 45.0))
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


def contract_sprites() -> list[dict]:
    return [s for s in json.loads(CONTRACT.read_text(encoding="utf-8"))["sprites"] if s["referenced"]]


# ---------------------------------------------------------------------------------------------- table
TABLE_W, TABLE_H = 1920, 1080
EDGE = {"left": 14.0, "right": 14.0, "top": 10.0, "bottom": 24.0}   # red Ben-Day page border, outside every play area
RAY_FOCUS = (1040.0, 1004.0)    # behind the primary action (PLAY_LAYOUT 1: 840..1240 x 962..1035)
BURST_AT = (1876.0, 98.0)       # behind the stage card's top-right corner (the target score)


def table_layer(r: Raster):
    X, Y = r.X, r.Y
    felt = col(PALETTE["felt"])
    # Right column (behind the stage card and the resource panels; seen in the gaps): darker Ben-Day dots on the felt.
    right = smoothstep(1484.0, 1700.0, X) * (X >= 1484.0) * (Y >= 84.0) * (Y < 944.0)
    r.over(BLUE_DOT, halftone(X, Y, 11.0, 3.6 * right, 45.0) * right)
    # Speed rays from behind the main button, seen in the action row's gap and the bottom margin.
    fx, fy = RAY_FOCUS
    ang = np.arctan2(Y - fy, X - fx)
    rays = smoothstep(.48, .56, np.cos((ang - .35) * 22.0)) * (ang > -.55) * (ang < .9)
    zone = (X >= 1240.0) * (Y >= 944.0) * smoothstep(1244.0, 1290.0, X)
    r.over(YELLOW, rays * zone)
    # A burst behind the stage card's corner.
    bx, by = BURST_AT
    d = sd_poly(X, Y, burst_pts(bx, by, 94.0, 58.0, 11, SEED + 11, .2))
    r.over(INK, coverage(d))
    r.over(YELLOW, coverage(d + 4.0))
    # The red page border with dots and an ink line on its inner edge.
    inner = sd_box(X, Y, (EDGE["left"], EDGE["top"], TABLE_W - EDGE["right"], TABLE_H - EDGE["bottom"]))
    band = coverage(-inner)
    r.over(RED, band)
    r.over(RED_SH, band * halftone(X, Y, 8.0, 2.4, 45.0))
    r.over(INK, coverage(np.abs(inner) - 1.6))
    return felt


TABLE_REGIONS = (  # non-overlapping cores rendered with a margin; everything else is the flat felt
    (0, 0, TABLE_W, 96), (0, TABLE_H - 44, TABLE_W, 44), (0, 96, 28, TABLE_H - 140), (TABLE_W - 28, 96, 28, TABLE_H - 140),
    (1484, 96, TABLE_W - 28 - 1484, 848), (1240, 944, TABLE_W - 28 - 1240, TABLE_H - 44 - 944),
)


def table_scene() -> Image.Image:
    felt = np.array(rgb8(PALETTE["felt"]), np.float32)
    canvas = np.broadcast_to(felt, (TABLE_H, TABLE_W, 3)).copy()
    margin = 8
    for x, y, w, h in TABLE_REGIONS:
        r = Raster(w + 2 * margin, h + 2 * margin, float(x - margin), float(y - margin))
        r.rgb[...] = col(PALETTE["felt"])
        r.a[...] = 1.0
        table_layer(r)
        im = np.asarray(r.export()).astype(np.float32)
        core = im[margin:margin + h, margin:margin + w, :3]
        canvas[y:y + h, x:x + w] = core
    return Image.fromarray(np.clip(np.rint(canvas), 0, 255).astype(np.uint8), "RGB")


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
    """A 1920x1080 offline composite of the play screen at W11 design sizes (not a Unity capture)."""

    def __init__(self, sprites, specs, table):
        self.sprites, self.specs = sprites, {s["name"]: s for s in specs}
        self.im = table.convert("RGBA")
        self.font = str(FONT)

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
        self.im.alpha_composite(im, (round(x), round(y)))

    def icon(self, name, x, y, w, h, colour=None, alpha=1.0):
        im = self.sprites[name].copy()
        im.thumbnail((round(w), round(h)), Image.Resampling.LANCZOS)
        if colour is not None or alpha < 1:
            im = tint(im, colour if colour is not None else WHITE, alpha)
        self.im.alpha_composite(im, (round(x + (w - im.width) / 2), round(y + (h - im.height) / 2)))

    def text(self, x, y, value, size, colour, anchor="lm", font=None):
        d = ImageDraw.Draw(self.im)
        d.text((x, y), value, font=ImageFont.truetype(font or self.font, round(size)), fill=tuple(rgb8(colour)) + (255,),
               anchor=anchor)

    def art(self, path, x, y, size):
        with Image.open(path) as src:
            im = src.convert("RGBA")
        im.thumbnail((size, size), Image.Resampling.LANCZOS)
        self.im.alpha_composite(im, (round(x + (size - im.width) / 2), round(y + (size - im.height) / 2)))


def _mock_die(m: Mock, cx, cy, size, pips):
    r = Raster(int(size * 1.4), int(size * 1.4))
    die_faces(r, size * .7, size * .66, size, pips=pips, pip_rgb=(col(PALETTE["red"]), col(PALETTE["blue"]), col(PALETTE["blue"])),
              ink=size * .045)
    im = r.export()
    shadow = m.sprites["ui_die_shadow"].resize((round(size * 1.3), round(size * .5)), Image.Resampling.LANCZOS)
    m.im.alpha_composite(shadow, (round(cx - size * .55), round(cy + size * .4)))
    m.im.alpha_composite(im, (round(cx - size * .7), round(cy - size * .66)))


def label_on(fill) -> str:
    """The palette text colour the game would pick on a drawn fill (GameApp.BestLabel11, without the 5 % steps)."""
    fill_hex = hexc(fill)
    return max((PALETTE["ink"], PALETTE["paper"], PALETTE["textColor"]), key=lambda c: contrast(c, fill_hex))


def play_mock(sprites, specs, table) -> Image.Image:
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
    m.icon("ui_icon_coin", 1446, 30, 36, 36)
    m.text(1540, 48, "28", 34, P["gold"], "rm")
    m.text(1548, 48, "金币", 20, P["muted"], "lm")
    for x, label in ((1572, "手册"), (1692, "暂停")):
        m.put("ui_chip_formula", x, 20, 108, 56, sec)
        m.text(x + 54, 48, label, 21, P["textColor"], "mm")
    # Cards.
    arts = ["C108", "C117", "U10"]
    for i, art in enumerate(arts):
        x = 32 + i * 228
        m.put("ui_card_controller", x, 96, 216, 244)
        m.put("ui_pip_dot", x + 22, 106, 22, 22, col(P["ink"]))
        m.text(x + 33, 117, str(i + 1), 14, P["paper"], "mm")
        for k in range(i + 1):
            m.im.alpha_composite(Image.new("RGBA", (8, 5), tuple(rgb8([P["rarityCommon"], P["rarityAdvanced"], P["rarityRare"]][i])) + (255,)),
                                 (x + 52 + k * 11, 115))
        m.text(x + 108, 148, ["排头兵", "点金手", "配重"][i], 22, P["ink"], "mm")
        m.art(ROOT / "ArtSource/StyleKit/themes/comic/anchors" / f"{art}.png", x + 42, 164, 132)
        m.text(x + 108, 310, ["首颗点数 ×4", "末颗镀金", "目标面权重 +1"][i], 20, P["ink"], "mm")
    m.put("ui_frame_enchant", 32 + 228, 96, 216, 244, col(P["gold"]))
    for i in range(2):
        m.put("ui_slot_inventory", 716 + i * 68, 104, 56, 228)
        m.text(744 + i * 68, 218, "+", 30, P["muted"], "mm")
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
    m.put("ui_chip_formula", 56, 338, 100, 34, col(P["panel"]))
    m.text(104, 355, "骰池", 20, P["muted"], "mm")
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
    m.put("ui_panel_hud", 32, 958, 512, 88)
    m.text(50, 1000, "按“提交”计分，或者重掷其余的骰子。", 21, P["textColor"], "lm")
    m.put("ui_chip_formula", 560, 966, 264, 72, sec)
    m.text(690, 1000, "重掷 · 还能掷 2", 21, P["textColor"], "mm")
    m.put("ui_button_bevel", 842, 970, 396, 74, col("B56B29"))
    m.put("ui_button_bevel", 840, 962, 400, 73, col(P["primary"]))
    m.text(1036, 996, "提交 2 颗", 28, label_on(col(P["primary"])), "mm")   # the label LabelOn11 picks
    m.put("ui_button_bevel", 1498, 970, 388, 74, col("8F453A"))
    m.put("ui_button_bevel", 1496, 962, 392, 73, col(P["paper"]))
    m.text(1688, 996, "结束本关 · 结算", 24, P["ink"], "mm")
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
    m = Mock(sprites, specs, Image.new("RGB", (1752, 330), rgb8(P["felt"])))
    rim = col(P["panel"]) * .45 + col(P["panelLight"]) * .55
    for i, (kind, tint_key) in enumerate((("普通关", "panelLight"), ("高压关", "pressure"), ("首领关", "boss"))):
        x0 = 16 + i * 584
        tag = col(P[tint_key])
        m.put("ui_chip_formula", x0, 12, 160, 56, tag)
        m.text(x0 + 78, 39, f"第 {4 + i} 关", 28, label_on(tag), "mm")
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


def preview(mock: Image.Image) -> Image.Image:
    """Settings preview: the offline play-screen composite reduced 4x with Lanczos (480 x 270)."""
    return mock.resize((480, 270), Image.Resampling.LANCZOS)


TINT_PROOF = {
    "ui_topbar": "panel", "ui_chip_formula": "panelLight", "ui_stage_card": "paper", "ui_dice_tray": "panelLight",
    "ui_button_bevel": "primary", "ui_button_ghost": "ghost", "ui_ring": "ghost", "ui_frame_enchant": "violet",
    "ui_frame_block": "panelLight", "ui_price_chip": "gold", "ui_progress_fill": "blue", "ui_progress_track": "07191C",
    "ui_section_header": "B8433F", "ui_pip_dot": "gold", "ui_icon_arrow": "ink", "ui_icon_book": "ink",
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


def review_sheet(sprites, specs, table, mock) -> Image.Image:
    """Default and comic pieces side by side, slice proofs at PPU 200, the in-situ composite, the table, the preview,
    the palette with its contrast numbers and the five concept pages. Offline review, not Unity."""
    cw, ch, columns = 600, 262, 3
    rows = math.ceil(len(specs) / columns)
    bg = (20, 24, 44)
    ink = (246, 240, 222)
    sub = (176, 190, 220)
    grid_h = 170 + rows * ch
    sheet = Image.new("RGB", (columns * cw, grid_h + 4600), bg)   # generous; cropped to the content at the end
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
    d.text((24, y), "IN-SITU OFFLINE COMPOSITE / play screen at W11 design sizes (PLAY_LAYOUT 1, 8) on table_scene.png", fill=ink, font=font)
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
    d.text((24, y), "SLICE PROOFS / at PPU 200 and real W11 sizes (units), same density as the game", fill=ink, font=font)
    proofs = [("ui_topbar", 860, 72, PALETTE["panel"]), ("ui_panel_hud", 392, 202, None), ("ui_paper_panel", 400, 300, None),
              ("ui_chip_formula", 160, 56, PALETTE["panelLight"]), ("ui_chip_formula", 120, 34, PALETTE["panel"]),
              ("ui_button_bevel", 400, 80, PALETTE["primary"]), ("ui_price_chip", 124, 36, PALETTE["gold"]),
              ("ui_card_shop", 232, 368, None), ("ui_section_panel", 452, 160, None), ("ui_stage_card", 392, 244, PALETTE["paper"]),
              ("ui_slot_inventory", 66, 72, None), ("ui_chip_label", 130, 40, None)]
    lookup = {s["name"]: s for s in specs}
    px, py, row_h = 24, y + 40, 0
    for name, w, h, colour in proofs:
        label = f"{name} {w}x{h}"
        step = max(w, int(d.textlength(label, font=small))) + 24
        if px + step > 1800:
            px, py, row_h = 24, py + row_h + 34, 0
        spec = lookup[name]
        im = nine_slice(sprites[name], spec["border"], (w * 2, h * 2)).resize((w, h), Image.Resampling.LANCZOS)
        if colour is not None:
            im = tint(im, col(colour))
        tile = Image.new("RGBA", im.size, rgb8(PALETTE["felt"]) + (255,))
        tile.alpha_composite(im)
        sheet.paste(tile.convert("RGB"), (px, py))
        d.text((px, py + h + 4), label, fill=sub, font=small)
        px += step
        row_h = max(row_h, h)
    y = py + row_h + 60
    d.text((24, y), "TABLE / 1920 x 1080 (calm zones outlined) and PREVIEW / 480 x 270", fill=ink, font=font)
    proof = table.copy()
    pd = ImageDraw.Draw(proof)
    for zx, zy, zw, zh in FELT_ZONES.values():
        pd.rectangle((zx, zy, zx + zw - 1, zy + zh - 1), outline=(255, 255, 255), width=3)
    sheet.paste(proof.resize((1152, 648), Image.Resampling.LANCZOS), (24, y + 36))
    sheet.paste(preview(mock), (1200, y + 36))
    sy = y + 330
    rep = palette_report()
    for i, key in enumerate(PALETTE):
        xx, yy = 1200 + (i % 4) * 140, sy + (i // 4) * 54
        d.rectangle((xx, yy, xx + 34, yy + 34), fill=rgb8(PALETTE[key]), outline=(0, 0, 0))
        d.text((xx + 40, yy), key, fill=ink, font=small)
        d.text((xx + 40, yy + 17), PALETTE[key], fill=sub, font=small)
    y += 36 + 648 + 24
    lines = [f"{k} {v:.2f}" for k, v in rep.items()]
    for i in range(0, len(lines), 3):
        d.text((24, y + (i // 3) * 22), "     ".join(lines[i:i + 3]), fill=sub, font=small)
    y += 22 * math.ceil(len(lines) / 3) + 30
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
# Offline review of the W13 card template (not a Unity capture and not part of build()): the shipped ring and gems of
# comic and of Swiss, tinted with each theme's rarity colours (Ghost for goods), composited as GameApp.Card11 DrawCard13
# lays them out in the shop (220 x 330), table (216 x 244) and compact (280 x 117) sizes, at 1080p and 720p. Sprites are
# sampled like the game's UI sprites (bilinear, no mipmaps) and blended in linear light; text and pictures are stand-ins.
CARD_REVIEW = REVIEW / "card_template_review.png"
CARD_REVIEW_CARDS = (  # (rarity or None for goods, name, short text, StyleKit anchor picture)
    ("common", "排头兵", "第一颗提交的骰子点数×4，加到基础分。", "C108"),
    ("advanced", "蓝色浪潮", "本手提交时，提交的骰子全部按蓝色计算。", "I10"),
    ("rare", "点金手", "每次提交后，最后一颗骰子朝上的面永久镀金。", "C117"),
    (None, "配重", "这一面权重+1（上限5），更容易掷出这一面。", "U10"),
)
CARD_REVIEW_THEMES = ("comic", "swiss")
TABLE_COLOUR = {"comic": PALETTE["felt"], "swiss": "F9EADB"}


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
    f = ImageFont.truetype(str(font), int(round(size)))
    lines, line = [], ""
    for ch in text:
        if line and f.getlength(line + ch) > width:
            lines.append(line)
            line = ch
        else:
            line += ch
    return lines + [line] if line else lines


def draw_card13(c: UnityCanvas, kit: dict, borders: dict, palette: dict, x, y, w, h, size: str, rarity, title, text, art):
    """The ring and gem as DrawTallCard13 / DrawCompactCard13 place them (CARD_TEMPLATE 2, section 9 rows 1 and 10), with
    stand-in name, picture, short text and price button. All lengths in 1080p design units."""
    goods = rarity is None
    face_name = "ui_card_shop" if size == "shop" else "ui_card_controller_compact" if size == "compact" else "ui_card_controller"
    face = kit[face_name]
    c.sprite(face, x, y, w, h, border=borders[face_name])
    fl, fb, fr, ft = (v / 2 for v in frame_insets_px(np.asarray(face), borders[face_name]))
    inner = (x + fl, y + ft, w - fl - fr, h - ft - fb)
    ring = kit["ui_card_frame_rarity"]
    rl, rb, rr, rt = (v / 2 for v in ring_insets_px(np.asarray(ring), borders["ui_card_frame_rarity"]))
    key = {"common": "rarityCommon", "advanced": "rarityAdvanced", "rare": "rarityRare", None: "ghost"}[rarity]
    tint_c = col(palette[key])
    ink = palette["ink"]
    if size == "compact":
        content = (inner[0] + 4, inner[1] + 4, inner[2] - 8, inner[3] - 8)
        block, gem_size = min(content[3] - 8, 76), 18
        bx, by = content[0], content[1] + max(0, (content[3] - block - gem_size * .5) * .5)
        c.sprite(ring, bx, by, block, block, tint=tint_c, border=borders["ui_card_frame_rarity"])
        pic = (bx + rl, by + rt, block - rl - rr, block - rt - rb)
        c.picture(art, pic[0] + 1, pic[1] + 1, min(pic[2], pic[3]) - 2)
        if not goods:
            c.sprite(kit["ui_gem_" + rarity], bx + block * .5 - gem_size * .5, by + block - gem_size * .5, gem_size,
                     gem_size, tint=tint_c)
        tx = bx + block + 10
        c.text(tx, content[1] + 2, title, 20, ink, FONT)
        for i, line in enumerate(_wrap(text, BODY_FONT, 18, content[0] + content[2] - tx)[:2]):
            c.text(tx, content[1] + 34 + i * 24, line, 18, ink, BODY_FONT)
        return
    c.sprite(ring, *inner, tint=tint_c, border=borders["ui_card_frame_rarity"])
    content = (inner[0] + rl + 4, inner[1] + rt + 3, inner[2] - rl - rr - 8, inner[3] - rt - rb - 6)
    gem_size = 34 if size == "shop" else 30
    gem_y = inner[1] + (content[1] - 3 - inner[1]) * .5
    top = content[1] if goods else max(content[1], gem_y + gem_size * .5 + 2)
    price = 42 if size == "shop" else 0
    bottom = content[1] + content[3] - (price + 6 if price else 0)
    if goods:
        c.text(x + w / 2, top + 13, "骰面改造", 16, palette["ink"], BODY_FONT, "mm")
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
        px, py, pw = content[0], content[1] + content[3] - price, content[2]
        c.sprite(kit["ui_chip_formula"], px, py, pw, price, tint=col(palette["background"]) * .45 + col(palette["panel"]) * .55,
                 border=borders["ui_chip_formula"])
        c.text(px + pw / 2, py + price / 2, "8  购买", 20, palette["textColor"], FONT, "mm")
    if not goods:
        c.sprite(kit["ui_gem_" + rarity], x + w * .5 - gem_size * .5, gem_y - gem_size * .5, gem_size, gem_size, tint=tint_c)


def card_template_review() -> Image.Image:
    """Comic beside Swiss: shop, table and compact cards (common, advanced, rare, goods) at 1080p and 720p, then the
    three gems at 12-34 px on the card's own top frame with a 2x nearest enlargement."""
    specs = {s["name"]: s for s in json.loads(CONTRACT.read_text(encoding="utf-8"))["sprites"]}
    names = ("ui_card_shop", "ui_card_controller", "ui_card_controller_compact", "ui_chip_formula") + CARD_TEMPLATE_NAMES
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
        for _, _, _, art in CARD_REVIEW_CARDS:
            with Image.open(ROOT / "ArtSource/StyleKit/themes" / theme / "anchors" / (art + ".png")) as source:
                arts[art] = source.convert("RGBA")
        themes[theme] = (kit, palette, arts)
    panel_w, gap, ink, sub = 960, 40, (246, 240, 222), (176, 190, 220)
    sheet = Image.new("RGB", (2 * panel_w + gap + 48, 3000), (20, 24, 44))   # cropped to the content at the end
    d = ImageDraw.Draw(sheet)
    font, small, title = (ImageFont.truetype(str(LATIN), s) for s in (19, 15, 34))
    d.text((24, 16), "CARD TEMPLATE / W13 rarity ring and gems   -   COMIC (left)   |   SWISS (right)", fill=ink, font=title)
    d.text((24, 60), "Offline emulation of GameApp.Card11 DrawCard13 geometry: sprites sampled bilinear without mipmaps (as the "
                     "theme UI sprites are imported), tinted and blended in linear light. Not a Unity capture.", fill=sub, font=small)
    d.text((24, 80), "Names, short texts, pictures (StyleKit anchors) and the price button are stand-ins. Goods (no rarity): "
                     "Ghost ring, no gem.", fill=sub, font=small)
    keys = {"common": "rarityCommon", "advanced": "rarityAdvanced", "rare": "rarityRare", None: "ghost"}
    y = 112
    for scale, res in ((1.0, "1080p"), (2 / 3, "720p")):
        for label, size, cw, chh in (("SHOP 220 x 330", "shop", 220, 330), ("TABLE 216 x 244", "table", 216, 244),
                                     ("COMPACT 280 x 117", "compact", 280, 117)):
            sw, sh = round(cw * scale), round(chh * scale)
            per_row = max(1, min(4, (panel_w - 8) // (sw + 16)))
            lines = math.ceil(len(CARD_REVIEW_CARDS) / per_row)
            ph = 8 + lines * (sh + 30)
            for p, theme in enumerate(CARD_REVIEW_THEMES):
                kit, palette, arts = themes[theme]
                px = 24 + p * (panel_w + gap)
                d.text((px, y), f"{theme.upper()}   {label}   {res}", fill=ink, font=font)
                canvas = UnityCanvas(panel_w, ph, TABLE_COLOUR[theme], scale)
                for i, (rarity, name, text, art) in enumerate(CARD_REVIEW_CARDS):
                    sx, sy = 8 + (i % per_row) * (sw + 16), 8 + (i // per_row) * (sh + 30)
                    draw_card13(canvas, kit, borders, palette, sx / scale, sy / scale, cw, chh, size, rarity, name, text,
                                arts[art])
                sheet.paste(canvas.image(), (px, y + 26))
                note = (246, 240, 222) if luminance(TABLE_COLOUR[theme]) < .3 else (70, 64, 58)
                for i, (rarity, *_rest) in enumerate(CARD_REVIEW_CARDS):
                    sx, sy = 8 + (i % per_row) * (sw + 16), 8 + (i // per_row) * (sh + 30)
                    d.text((px + sx, y + 26 + sy + sh + 5), f"{rarity or 'goods'}  {palette[keys[rarity]]}", fill=note, font=small)
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
    data["palette"] = dict(PALETTE)
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
    save(THEME / "Table/felt_tile.png", felt_tile())
    mock = play_mock(sprites, specs, table)
    save(THEME / "preview.png", preview(mock))
    save(REVIEW / "contact_sheet.png", review_sheet(sprites, specs, table, mock))
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
