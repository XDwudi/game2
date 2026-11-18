"""W20: deterministic 黑金装饰艺术 (deco) UI kit: black lacquer, polished gold double rules, crimson keystones.

Style authority: the five user-approved concept pages in ArtSource/StyleKit/themes/deco/concepts (roll, shop, select,
card, triptych; CP3) and the palette of ArtSource/StyleKit/themes/deco/style_kit.json. Their texts, numbers and layouts
are not a contract; the game layout is (docs/specs/v11/PLAY_LAYOUT.md, SHOP_LAYOUT.md). The contract
(tools/art_v11/theme_contract.json) owns pixel sizes, nine-slice borders and PPU. Repository fonts only; no image
generation, no Unity files.

The shape language, each taken from the deco concept pages (README "Shapes and materials"):
- symmetric chamfered octagons, 45-degree corner cuts and no lean (every card, panel and button on roll.png, shop.png,
  select.png, card.png);
- the double rule: a polished gold band bevelled and lit from the upper left, a black lacquer gap, a thin gold rule
  (card.png and roll.png card and panel frames);
- crimson keystones: in each corner of a dark or paper panel the thin rule closes a small crimson right triangle with
  a gold diagonal brace (the crimson corner triangles of card.png's art windows and roll.png's corners);
- tinted plaques (chips, buttons, the stage card, the top bar) keep the same octagon with a bevelled rim and an
  engraved line, so any palette colour reads as an enamel or metal plaque (shop.png price chips and buttons);
- a chevron-ended caption strip (shop.png section tabs) for the shop's section header;
- the sunburst fan on a stepped plinth (the fans of roll.png, select.png and card.png) only on the table and the
  wordmark, never on a band the game stretches.

Rules the art keeps (checked by tests/test_art_v11_deco.py in phase 2, and by checks in this generator):
- every nine-slice centre is one flat colour (or transparent) from the border line inward;
- stretched edge bands are the same along the edge: corner features end inside the 40 px corner squares (checked
  after rendering: `_check_slices`);
- sprites the code multiplies by a palette colour (NEUTRAL_NAMES) are achromatic with a pure white face;
- the four calm zones of the table are exactly the felt colour; decoration only where no play state puts interface.

`build_deco.py --review` writes the offline review mocks to ArtSource/Art11/deco/review/ (not part of the build).
"""
from __future__ import annotations

import colorsys
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont
from scipy import ndimage

ROOT = Path(__file__).resolve().parents[3]
CONTRACT = ROOT / "tools/art_v11/theme_contract.json"
THEME = Path("DicePrototype/Assets/Resources/Themes/deco")
REVIEW = Path("ArtSource/Art11/deco")
CONCEPTS = Path("ArtSource/StyleKit/themes/deco/concepts")
CONCEPT_PAGES = ("roll", "shop", "select", "card", "triptych")
FONT = ROOT / "DicePrototype/Assets/Game/Fonts/NotoSansSC-Bold.otf"
BODY_FONT = ROOT / "DicePrototype/Assets/Game/Fonts/NotoSansCJKsc-Regular.otf"
HEADING = ROOT / "DicePrototype/Assets/Game/Fonts/Themes/deco/ZCOOLXiaoWei-Regular.ttf"   # CP3-2, the deco title face
LATIN = ROOT / "DicePrototype/Assets/TextMesh Pro/Fonts/LiberationSans.ttf"
SS = 4
SEED = 20261118

# ---------------------------------------------------------------------------------------------- palette
# theme.json palette (THEME_SPEC section 2). The generator is the single source: build() writes theme.json from it, the
# table felt is exactly PALETTE["felt"], and the baked sprites take their lacquer, ivory and crimson from the constants
# below. Reasons and contrast numbers: tools/art_v11/deco/README.md (palette_report() prints them).
PALETTE = {
    "background": "0D0A08",   # deepest lacquer: fades and veils; mixed into the secondary chips
    "felt": "15110D",         # black lacquer (concept tray floor 15110E): the table, flat in every calm zone
    "panel": "332A20",        # smoked lacquer plate: top bar, dialogs, secondary chips (light text on it)
    "panelLight": "6E5434",   # antique bronze: the normal stage tag and header, dividers, unreached points, disabled faces
    "paper": "F4E3C3",        # ivory enamel: cards, paper panels, button faces
    "ink": "18130E",          # lacquer ink: text on ivory
    "textColor": "F7E9CC",    # ivory: text on lacquer
    "muted": "C2AC83",        # old gold: secondary text on lacquer
    "gold": "E2B05A",         # polished gold: prices, coins, highlights
    "teal": "6DC4A0",         # jade: done, positive values
    # red and blue also colour the 3D dice pips (DiceTable reads UiKit.Red / UiKit.Blue). Pip colour is the game's main
    # functional cue, so every style keeps the default pair (coordinator, 2026-11-17, CP3-1; THEME_SPEC section 2).
    "red": "EC655D",
    "blue": "55B6D9",
    "violet": "A783D9",       # amethyst: enchantments
    "danger": "A6262B",       # deep crimson-red: errors, warning badges
    "shadow": "070605",       # lacquer shadow
    "ghost": "B8955A",        # old gold: outline buttons, empty slot rings, the tray's inner line, the goods' card ring
    "primary": "9E1D22",      # crimson (concept: 重掷, 下一关 and 开始挑战 are crimson plaques): the one main action
    "reward": "EDC46A",       # bright gold: reward moments
    "boss": "C4372D",         # scarlet: boss stages
    "pressure": "E8742E",     # vermilion: pressure stages (a warning hue, apart from the crimson and the scarlet)
    "rarityCommon": "BDB6A8", # pewter
    "rarityAdvanced": "3D9FB5",  # teal enamel (style kit teal 0E4A66, lifted to read on ivory and lacquer)
    "rarityRare": "8D57CB",   # amethyst
    "onFelt": "C9A86A",       # antique gold marks drawn straight on the black felt
}
# THEME_SPEC 2 W20X keys, set only where the concept needs them (THEME_SPEC 2; coordinator's brief rule 5).
W20X_TOKENS = {
    "buttonStep": "7A5C34",         # the plinth under a UiKit.Button: bronze (the concept's buttons sit in gold mounts)
    "buttonStepPrimary": "B68A44",  # gold plinth under the crimson main button
    "section": "FFFFFF",            # ui_section_panel stays authored: black lacquer with its gold double rule
    "caption": "8E1B20",            # crimson caption strips: the shop's section headers and the tray's tabs
}
THEME_META = {
    "id": "deco",
    "displayName": "黑金装饰艺术",
    "description": ("黑漆桌面上的金色双线框：面板与卡牌是对称切角的八角形，抛光金边、黑漆间隔与细金线，暗色面板四角嵌深红三角；"
                    "主按钮是金色托座上的深红漆牌，商店分区标题是带箭头端的深红条，桌面只有屏幕金框与动作行空处的一把金色扇形。"
                    "依据：ArtSource/StyleKit/themes/deco/concepts 与 style_kit.json；界面件与色板由 "
                    "tools/art_v11/deco/build_deco.py 生成（说明见同目录 README.md）"),
    "order": 4,
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
    """CIE76 colour difference (the metric 唐三彩 used for the rarity / primary distance)."""
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


def lerp_hex(a: str, b: str, t: float) -> str:
    """Color.Lerp on two palette colours (sRGB values, as the game's code mixes them)."""
    return hexc(col(a) * (1 - t) + col(b) * t)


def best_label(fill: str, palette: dict) -> tuple[str, float]:
    """GameApp.BestLabel11: of ink, paper and textColor, the label with the highest contrast on a drawn fill."""
    key = max(("ink", "paper", "textColor"), key=lambda k: contrast(palette[k], fill))
    return key, contrast(palette[key], fill)


def hexc(c) -> str:
    return "".join(f"{int(round(float(np.clip(v, 0, 1)) * 255)):02X}" for v in c)


# ---------------------------------------------------------------------------------------------- deco materials
# Concept samples (scratch measurements on the concept pages, README "Palette"): lacquer 110E0A-15110E, crimson plates
# 5F0B11-661315, ivory buttons F1D29F, gold plaques F9C366, price chips 2A1C0E. Style kit: ivory F8E5C6, gold D9A44E,
# old gold B7916B, lacquer 15110D, crimson 7A120C, teal 0E4A66, bronze 785C3E.
LACQUER_HEX = "100D0A"            # panel interiors (concept HUD interior 120F0C)
LACQUER = col(LACQUER_HEX)
LACQUER_DEEP = col("0A0806")
IVORY_HEX = PALETTE["paper"]
IVORY = col(IVORY_HEX)
CRIMSON = col("8E1B20")
CRIMSON_DEEP = col("5C0E12")
BRONZE = col("785C3E")
# The polished gold ramp from shadow to specular (style kit gold D9A44E, old gold B7916B, bronze 785C3E).
GOLD_RAMP = ((0.00, col("3A2810")), (0.22, col("6E4F22")), (0.45, col("A97D35")), (0.66, col("D9A44E")),
             (0.84, col("F0C978")), (1.00, col("FFF0C8")))
GOLD_FLAT = col("D2A353")         # the thin inner rules
OLD_GOLD = col("B7916B")
WHITE = np.ones(3, np.float32)
K = np.zeros(3, np.float32)
# Light from the upper left, as on every concept page (screen axes: x right, y down, z toward the viewer).
_L = np.array([-.42, -.58, .70], np.float64)
LIGHT = _L / np.linalg.norm(_L)

# Sprites the game multiplies by palette colours: they stay achromatic (white face, grey bevel, black or transparent).
NEUTRAL_NAMES = frozenset({
    "ui_button_bevel", "ui_button_ghost", "ui_frame_block",
    "ui_ring", "ui_progress_fill", "ui_progress_track",
    "ui_section_header", "ui_pip_dot", "ui_icon_arrow", "ui_icon_book",
    "ui_icon_gear", "fx_arc_short", "fx_score_spark", "fx_square_chip",
    "ui_dice_tray", "ui_topbar", "ui_chip_formula", "ui_stage_card", "ui_icon_refresh",
    "ui_card_frame_rarity", "ui_gem_common", "ui_gem_advanced", "ui_gem_rare",
})
CARD_TEMPLATE_NAMES = ("ui_card_frame_rarity", "ui_gem_common", "ui_gem_advanced", "ui_gem_rare")
# Calm zones of the play screen (THEME_SPEC 3.1): one flat felt colour, no texture.
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
    return np.clip(.5 - np.asarray(d, np.float32) * SS, 0.0, 1.0).astype(np.float32)


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
        a = np.clip(np.asarray(alpha, np.float32), 0, 1)
        rgb = np.asarray(rgb, np.float32)
        if rgb.ndim == 1:
            rgb = np.broadcast_to(rgb, self.rgb.shape)
        self.rgb[...] = rgb * a[..., None] + self.rgb * (1 - a[..., None])
        self.a[...] = a + self.a * (1 - a)

    def cut(self, alpha):
        """Erase (make transparent) where alpha is 1: an inlay gap that shows what lies under the sprite."""
        keep = 1 - np.clip(np.asarray(alpha, np.float32), 0, 1)
        self.rgb[...] *= keep[..., None]
        self.a[...] *= keep

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


def mix(a, b, t):
    t = np.asarray(t, np.float32)
    return np.asarray(a, np.float32) + (np.asarray(b, np.float32) - np.asarray(a, np.float32)) * t[..., None]


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
    return sd_poly_n(X, Y, pts)[0]


def sd_poly_n(X, Y, pts):
    """Exact signed distance to a closed polygon (negative inside, even-odd test) and the outward unit normal of the
    boundary at the nearest point. Along a straight edge the normal is the edge's normal, so a shade computed from it
    is the same all along the edge (what a nine-slice band needs)."""
    Xd, Yd = X.astype(np.float64), Y.astype(np.float64)
    best = np.full(X.shape, np.inf)
    qx = np.zeros(X.shape)
    qy = np.zeros(X.shape)
    inside = np.zeros(X.shape, bool)
    pts = list(pts)
    for (ax, ay), (bx, by) in zip(pts, pts[1:] + pts[:1]):
        ex, ey = bx - ax, by - ay
        wx, wy = Xd - ax, Yd - ay
        t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1)
        px, py = ax + ex * t, ay + ey * t
        dd = (Xd - px) ** 2 + (Yd - py) ** 2
        closer = dd < best
        best = np.where(closer, dd, best)
        qx = np.where(closer, px, qx)
        qy = np.where(closer, py, qy)
        if ay != by:
            inside ^= ((ay > Yd) != (by > Yd)) & (Xd < ax + ex * (Yd - ay) / ey)
    d = np.sqrt(best)
    n = np.maximum(d, 1e-12)
    sgn = np.where(inside, -1.0, 1.0)
    nx = np.where(d > 1e-9, sgn * (Xd - qx) / n, 0.0)
    ny = np.where(d > 1e-9, sgn * (Yd - qy) / n, 0.0)
    return np.where(inside, -d, d).astype(np.float32), nx.astype(np.float32), ny.astype(np.float32)


def octagon(box, cuts):
    """A box with 45-degree corner cuts (top-left, top-right, bottom-right, bottom-left) in px; 0 keeps a corner."""
    x0, y0, x1, y1 = box
    tl, tr, br, bl = (cuts,) * 4 if np.isscalar(cuts) else cuts
    pts = [(x0, y0 + tl), (x0 + tl, y0)] if tl else [(x0, y0)]
    pts += [(x1 - tr, y0), (x1, y0 + tr)] if tr else [(x1, y0)]
    pts += [(x1, y1 - br), (x1 - br, y1)] if br else [(x1, y1)]
    pts += [(x0 + bl, y1), (x0, y1 - bl)] if bl else [(x0, y1)]
    return pts


def band(d, lo: float, hi: float):
    """Coverage of the ring between depth lo and hi inside a shape (d negative inside): 0 <= lo < hi."""
    return np.clip(coverage(d + lo) - coverage(d + hi), 0.0, 1.0)


def poly_mask(r: Raster, pts) -> np.ndarray:
    return r.mask(lambda d, s: d.polygon(s(pts), fill=255))


def fan_wedges(cx, cy, r_out, r_in, n, a0, a1, gap):
    """Sunburst wedges between angles a0 and a1 (radians, screen axes), n rays with a gap fraction between them."""
    out = []
    step = (a1 - a0) / n
    for i in range(n):
        s = a0 + i * step + step * gap / 2
        e = a0 + (i + 1) * step - step * gap / 2
        out.append([(cx + math.cos(s) * r_in, cy + math.sin(s) * r_in), (cx + math.cos(s) * r_out, cy + math.sin(s) * r_out),
                    (cx + math.cos(e) * r_out, cy + math.sin(e) * r_out), (cx + math.cos(e) * r_in, cy + math.sin(e) * r_in)])
    return out


# ---------------------------------------------------------------------------------------------- deco shading
def _ramp(v, ramp=GOLD_RAMP) -> np.ndarray:
    v = np.clip(np.asarray(v, np.float32), 0, 1)
    out = np.zeros(v.shape + (3,), np.float32)
    for (t0, c0), (t1, c1) in zip(ramp, ramp[1:]):
        m = (v >= t0) & (v <= t1)
        f = ((v - t0) / (t1 - t0))[m]
        out[m] = c0 + (c1 - c0) * f[:, None]
    return out


def bevel_light(t, nx, ny, height: float = 1.0):
    """Lambert + a tight Blinn highlight on a ridge across a band: t = 0 at the band's outer edge, 1 at its inner edge;
    (nx, ny) the outward normal. The outer half faces outward, the inner half inward (a raised moulding)."""
    slope = np.cos(np.pi * np.clip(t, 0, 1)) * np.float32(height)
    Nx, Ny, Nz = nx * slope, ny * slope, np.ones_like(slope)
    inv = 1.0 / np.sqrt(Nx * Nx + Ny * Ny + Nz * Nz)
    Nx, Ny, Nz = Nx * inv, Ny * inv, Nz * inv
    diff = np.clip(Nx * LIGHT[0] + Ny * LIGHT[1] + Nz * LIGHT[2], 0, 1)
    hx, hy, hz = LIGHT[0], LIGHT[1], LIGHT[2] + 1.0
    hn = math.sqrt(hx * hx + hy * hy + hz * hz)
    spec = np.clip((Nx * hx + Ny * hy + Nz * hz) / hn, 0, 1) ** 48
    return diff.astype(np.float32), spec.astype(np.float32)


def gold_band_rgb(t, nx, ny):
    """Polished gold across a bevelled band: a crisp specular line on the lit (upper-left) faces, warm mid gold on the
    flat, bronze in the shade, a fine dark line at both edges of the band."""
    diff, spec = bevel_light(t, nx, ny, 1.35)
    v = .30 + .62 * diff ** 1.6
    rgb = _ramp(v)
    rgb = rgb + (col("FFF7DC") - rgb) * np.clip(spec * 1.6, 0, 1)[..., None]
    edge = np.minimum(np.clip(t * 12, 0, 1), np.clip((1 - t) * 12, 0, 1))
    return rgb * (.62 + .38 * edge)[..., None]


def grey_band(t, nx, ny, lo: float = .58):
    """The achromatic bevel of a tinted sprite: never brighter than its white face (drawn colour <= the tint)."""
    diff, spec = bevel_light(t, nx, ny, 1.0)
    v = lo + (1 - lo) * np.clip(diff * 1.05 + spec * .3, 0, 1)
    edge = np.minimum(np.clip(t * 7, 0, 1), np.clip((1 - t) * 7, 0, 1))
    return np.clip(v * (.78 + .22 * edge), 0, 1)


# ---------------------------------------------------------------------------------------------- nine-slice tiles
def _band_render(r: Raster, w: int, h: int, depth: int, draw, fill):
    """Render only the four border bands (the centre is one flat colour or transparent by construction)."""
    D = depth
    if fill is not None:
        r.rgb[D * SS:(h - D) * SS, D * SS:(w - D) * SS] = np.asarray(fill, np.float32)
        r.a[D * SS:(h - D) * SS, D * SS:(w - D) * SS] = 1.0
    for x, y, ww, hh in ((0, 0, w, D), (0, h - D, w, D), (0, D, D, h - 2 * D), (w - D, D, D, h - 2 * D)):
        draw(r.window(x, y, ww, hh))


def keystone_parts(X, Y, w, h, a: float, leg: float, brace: float):
    """The deco corner: in each corner of the rectangle inset by a, a right triangle with legs `leg` along the rule
    lines and a diagonal brace `brace` px wide on its hypotenuse. Returns (triangle coverage, brace coverage)."""
    tri = np.zeros(X.shape, np.float32)
    br = np.zeros(X.shape, np.float32)
    corners = ((a, a, 1, 1), (w - a, a, -1, 1), (w - a, h - a, -1, -1), (a, h - a, 1, -1))
    for cx, cy, sx, sy in corners:
        u = (X - np.float32(cx)) * sx          # distance into the panel along x from the corner
        v = (Y - np.float32(cy)) * sy
        diag = (u + v - np.float32(leg)) / np.float32(math.sqrt(2))   # signed distance to the hypotenuse
        inside = np.minimum(np.minimum(coverage(-u), coverage(-v)), coverage(diag))
        tri = np.maximum(tri, inside)
        near = np.minimum(coverage(np.abs(diag) - brace / 2), np.minimum(coverage(-u + .5), coverage(-v + .5)))
        br = np.maximum(br, near)
    return tri, br


def keystone_frame(w: int, h: int, *, cut: float, gold: float, gap: float, gap_rgb, rule: float, face, leg: float,
                   leg_rgb, border: float = 40.0, inset: float = 1.0) -> Image.Image:
    """deco_frame with the crimson keystones drawn in the right order: gap, face inside the rule, crimson triangles in
    the rule's corners, the rule rectangle and the braces, then the bevelled band on top."""
    box = (inset, inset, w - inset, h - inset)
    pts = octagon(box, cut)
    a = inset + gold + gap
    depth = int(math.ceil(border + 8))

    def draw(win: Raster):
        d, nx, ny = sd_poly_n(win.X, win.Y, pts)
        X, Y = win.X, win.Y
        inner = coverage(d + gold)
        win.over(gap_rgb, inner)
        rect_out = coverage(sd_box(X, Y, (a, a, w - a, h - a)))
        rect_in = coverage(sd_box(X, Y, (a + rule, a + rule, w - a - rule, h - a - rule)))
        win.over(face, rect_in)
        tri, brace = keystone_parts(X, Y, w, h, a + rule, leg, rule)
        win.over(leg_rgb, tri * rect_in)
        win.over(GOLD_FLAT, np.clip(rect_out - rect_in, 0, 1) * inner)
        win.over(GOLD_FLAT, brace * rect_in)
        t = np.clip(-d / gold, 0, 1)
        win.over(gold_band_rgb(t, nx, ny), band(d, 0.0, gold))

    r = Raster(w, h)
    _band_render(r, w, h, depth, draw, face)
    return r.export()


def plain_frame(w: int, h: int, *, cut: float, gold: float, gap: float, gap_rgb, face, border: float = 40.0,
                inset: float = 1.0) -> Image.Image:
    """Cards: the bevelled gold band, a lacquer (or coloured) gap and the flat face; the W13 rarity ring takes the place
    of the concept's thin inner rule right inside the gap."""
    box = (inset, inset, w - inset, h - inset)
    pts = octagon(box, cut)
    depth = int(math.ceil(border + 8))

    def draw(win: Raster):
        d, nx, ny = sd_poly_n(win.X, win.Y, pts)
        win.over(gap_rgb, coverage(d + gold))
        win.over(face, coverage(d + gold + gap))
        t = np.clip(-d / gold, 0, 1)
        win.over(gold_band_rgb(t, nx, ny), band(d, 0.0, gold))

    r = Raster(w, h)
    _band_render(r, w, h, depth, draw, face)
    return r.export()


def plaque(w: int, h: int, *, cut: float, rim: float, line_at: float = 0.0, line_w: float = 0.0, inlay_at: float = 0.0,
           inlay_w: float = 0.0, hollow: bool = False, inner_w: float = 0.0, border: float = 40.0, inset: float = 1.0,
           depth: int | None = None, lo: float = .58) -> Image.Image:
    """A tinted (achromatic) octagon plaque: a bevelled grey rim `rim` px wide, then white; optionally an engraved grey
    line (line_at, line_w), a transparent inlay gap (inlay_at, inlay_w) that shows the host, or an open centre
    (hollow: the plaque is only the rim and an optional inner hairline `inner_w` after the inlay)."""
    box = (inset, inset, w - inset, h - inset)
    pts = octagon(box, cut)
    depth = depth or int(math.ceil(border + 8))

    def draw(win: Raster):
        d, nx, ny = sd_poly_n(win.X, win.Y, pts)
        t = np.clip(-d / rim, 0, 1)
        g = grey_band(t, nx, ny, lo)
        if hollow:
            win.over(np.repeat(g[..., None], 3, axis=2), band(d, 0.0, rim))
            if inner_w > 0:
                win.over(WHITE, band(d, inlay_at + inlay_w, inlay_at + inlay_w + inner_w))
            return
        win.over(WHITE, coverage(d))
        win.over(np.repeat(g[..., None], 3, axis=2), band(d, 0.0, rim))
        if line_w > 0:
            win.over(np.full(3, .52, np.float32), band(d, line_at, line_at + line_w))
        if inlay_w > 0:
            win.cut(band(d, inlay_at, inlay_at + inlay_w))

    r = Raster(w, h)
    _band_render(r, w, h, depth, draw, None if hollow else WHITE)
    return r.export()


def _check_slices(name: str, im: Image.Image, border, tinted: bool):
    """Fail on a centre that is not one flat colour, or a stretched band that changes along the edge (tinted pieces
    exactly the same, baked pieces at most 6 levels per pixel, the brief's soft-band rule)."""
    px = np.asarray(im).astype(np.int16)
    h, w = px.shape[:2]
    left, bottom, right, top = (int(v) for v in border)
    if left and right and top and bottom:
        centre = px[top:h - bottom, left:w - right]
        if (centre != centre[0, 0]).any():
            raise ValueError(f"{name}: the nine-slice centre is not one flat colour")
    limit = 0 if tinted else 6
    bands = []
    if left and right:
        bands += [px[:top, left:w - right].transpose(1, 0, 2), px[h - bottom:, left:w - right].transpose(1, 0, 2)] if top or bottom else \
                 [px[:, left:w - right].transpose(1, 0, 2)]
    if top and bottom:
        bands += [px[top:h - bottom, :left], px[top:h - bottom, w - right:]]
    for b in bands:
        if b.shape[0] > 1:
            step = int(np.abs(np.diff(b, axis=0)).max())
            if step > limit:
                raise ValueError(f"{name}: a stretched band changes by {step} levels along its edge (limit {limit})")


# ---------------------------------------------------------------------------------------------- surfaces
# Sprite px (PPU 200: 2 px = 1 unit at 1080p). The rule rectangle's corner must sit inside the band's chamfer:
# PANEL_CUT <= (2 - sqrt 2) * PANEL_GOLD + 2 * PANEL_GAP (checked below), so the keystone's right angle shows.
PANEL_CUT, PANEL_GOLD, PANEL_GAP, PANEL_RULE, PANEL_LEG = 14.0, 10.0, 4.5, 2.0, 16.0
CARD_CUT, CARD_GOLD, CARD_GAP = 14.0, 10.0, 2.5
if PANEL_CUT > (2 - math.sqrt(2)) * PANEL_GOLD + 2 * PANEL_GAP:
    raise ValueError("The panel's rule corner would cross its gold band")
CHIP_CUT, CHIP_RIM = 20.0, 5.0
BUTTON_CUT, BUTTON_RIM = 18.0, 6.0
RING_CUT = 24.0
TRAY_CUT = 23.0


def surface(spec: dict) -> Image.Image:
    """Nine-sliced and horizontally sliced surfaces."""
    name = spec["name"]
    w, h = spec["size"]
    if name in ("ui_panel_hud", "ui_section_panel", "ui_tab_bottom"):
        # Black lacquer in the gold double rule with crimson keystones (roll.png HUD plates, shop.png sections).
        return keystone_frame(w, h, cut=PANEL_CUT, gold=PANEL_GOLD, gap=PANEL_GAP, gap_rgb=LACQUER, rule=PANEL_RULE,
                              face=LACQUER, leg=PANEL_LEG, leg_rgb=CRIMSON)
    if name == "ui_paper_panel":
        # Ivory in the same double rule (card.png's frames; the main menu's wordmark panel, paper dialogs).
        return keystone_frame(w, h, cut=PANEL_CUT, gold=PANEL_GOLD, gap=PANEL_GAP, gap_rgb=LACQUER, rule=PANEL_RULE,
                              face=IVORY, leg=PANEL_LEG, leg_rgb=CRIMSON)
    if name in ("ui_card_controller", "ui_card_controller_compact", "ui_card_shop"):
        return plain_frame(w, h, cut=CARD_CUT, gold=CARD_GOLD, gap=CARD_GAP, gap_rgb=LACQUER, face=IVORY)
    if name == "ui_card_item":
        # Consumables: a crimson gap under the gold band (concept: consumables and the shop's crimson accents).
        return plain_frame(w, h, cut=8.0, gold=7.0, gap=3.0, gap_rgb=CRIMSON, face=IVORY)
    if name == "ui_info_inset":
        # A lacquer well with a fine old-gold rule (roll.png's score readouts).
        return _well(w, h)
    if name == "ui_slot_inventory":
        return _slot(w, h)
    # Achromatic, tinted by the game.
    if name == "ui_chip_formula":
        return plaque(w, h, cut=CHIP_CUT, rim=CHIP_RIM, line_at=7.5, line_w=1.6)
    if name == "ui_button_bevel":
        return plaque(w, h, cut=BUTTON_CUT, rim=BUTTON_RIM, line_at=9.0, line_w=1.8)
    if name == "ui_topbar":
        return plaque(w, h, cut=14.0, rim=4.0, inlay_at=4.0, inlay_w=3.0)
    if name == "ui_stage_card":
        return plaque(w, h, cut=18.0, rim=5.0, inlay_at=5.0, inlay_w=3.5, border=48.0)
    if name == "ui_ring":
        return plaque(w, h, cut=RING_CUT, rim=6.0, inlay_at=6.0, inlay_w=2.5, hollow=True, inner_w=2.0)
    if name == "ui_dice_tray":
        return plaque(w, h, cut=TRAY_CUT, rim=10.0, inlay_at=10.0, inlay_w=0.0, hollow=True, border=72.0, depth=80,
                      lo=.50)
    if name == "ui_section_header":
        return section_header(w, h)
    if name == "ui_card_frame_rarity":
        return rarity_ring(w, h)
    raise ValueError("Unimplemented sliced sprite: " + name)


RING_BAND = (1.5, 4.0, 1.2)      # px: lacquer line, tinted band, lacquer line (the concept's thin inner rule, in the rarity colour)
RARITY_RING_CUT = 8.0            # 4 units: the ring's corner stays on the card's ivory, inside the face's chamfer


def rarity_ring(w: int, h: int) -> Image.Image:
    """ui_card_frame_rarity (W13, CARD_TEMPLATE 6): the thin inner rule of the concept's cards, tinted by the rarity
    colour (Ghost for goods), between two lacquer hairlines so a pale rarity still reads on ivory. Hollow, achromatic."""
    box = (.5, .5, w - .5, h - .5)
    pts = octagon(box, RARITY_RING_CUT)
    a, b, c = RING_BAND

    def draw(win: Raster):
        d, _, _ = sd_poly_n(win.X, win.Y, pts)
        win.over(K, band(d, 0.0, a + b + c))
        win.over(WHITE, band(d, a, a + b))

    r = Raster(w, h)
    _band_render(r, w, h, 48, draw, None)
    return r.export()


def _well(w: int, h: int) -> Image.Image:
    """ui_info_inset: deep lacquer with an old-gold hairline 1 px in and a bronze one inside it (no bevel: a recess)."""
    box = (1.0, 1.0, w - 1.0, h - 1.0)
    pts = octagon(box, 8.0)

    def draw(win: Raster):
        d, _, _ = sd_poly_n(win.X, win.Y, pts)
        win.over(OLD_GOLD, coverage(d))
        win.over(LACQUER_DEEP, coverage(d + 2.5))
        win.over(BRONZE * .8, band(d, 4.5, 5.5))
    r = Raster(w, h)
    _band_render(r, w, h, 48, draw, LACQUER_DEEP)
    return r.export()


def _slot(w: int, h: int) -> Image.Image:
    """ui_slot_inventory: a lacquer recess with an old-gold rule (empty card strips in play, cells in the shop)."""
    box = (1.0, 1.0, w - 1.0, h - 1.0)
    pts = octagon(box, 12.0)

    def draw(win: Raster):
        d, _, _ = sd_poly_n(win.X, win.Y, pts)
        win.over(OLD_GOLD, coverage(d))
        win.over(LACQUER, coverage(d + 3.0))
        win.over(OLD_GOLD * .55, band(d, 6.0, 7.2))
    r = Raster(w, h)
    _band_render(r, w, h, 48, draw, LACQUER)
    return r.export()


def _aniso_coverage(d):
    """Coverage for a distance measured in anisotropic display units: rescale by the local gradient."""
    gy, gx = np.gradient(d, np.float32(1.0 / SS))
    g = np.maximum(np.sqrt(gx * gx + gy * gy), 1e-3)
    return coverage(d / g)


def frame_insets_px(pixels: np.ndarray, border) -> tuple[int, int, int, int]:
    """GameApp.FrameInsets11 (left, bottom, right, top) in sprite px: along the middle row and column, within the slice
    border, the last pixel that is transparent (alpha < 128) or differs from the centre by more than 48 (sum of the
    channel differences). Unity rows run up."""
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
    left, bottom, right, top = border
    return (inset(lambda i: up[h // 2, i], w, left), inset(lambda i: up[i, w // 2], h, bottom),
            inset(lambda i: up[h // 2, w - 1 - i], w, right), inset(lambda i: up[h - 1 - i, w // 2], h, top))


_SPECS: dict[str, dict] = {}
_RENDERED: dict[str, Image.Image] = {}


def spec_of(name: str) -> dict:
    if not _SPECS:
        _SPECS.update({s["name"]: s for s in json.loads(CONTRACT.read_text(encoding="utf-8"))["sprites"]})
    return _SPECS[name]


def rendered(name: str) -> Image.Image:
    """A sprite rendered once per run (the section header is sized from the section panel's drawn frame)."""
    if name not in _RENDERED:
        _RENDERED[name] = render_sprite(spec_of(name))
    return _RENDERED[name]


def section_header_units() -> float:
    """W14 (GameApp.Shop14 Section14): the strip spans the section's inside from the panel's frame insets and is
    ShopHead14 (68) - top inset - 2 units tall."""
    top = frame_insets_px(np.asarray(rendered("ui_section_panel")), spec_of("ui_section_panel")["border"])[3] / 2
    return 68.0 - top - 2.0


def section_header(w: int, h: int) -> Image.Image:
    """Shop section caption (border 40/0/72/0, only sliced horizontally): a strip with a chevron right end (shop.png's
    crimson tabs), an engraved line along its edges, white so the caption tint colours it. Drawn in display units for
    its W14 height (0.5 units per px across, section_header_units() / 128 down): Unity scales only the height."""
    SECTION_HEADER_UNITS = section_header_units()
    r = Raster(w, h)
    sx, sy = .5, SECTION_HEADER_UNITS / h
    Xu, Yu = r.X * sx, r.Y * sy
    W, H = w * sx, SECTION_HEADER_UNITS
    top, bottom = .5, H - .5
    mid = (top + bottom) / 2
    tip = W - 3.0                      # the chevron's point, inside the 36-unit right cap
    shoulder = tip - (bottom - top) / 2
    body = [(1.0 + 5.0, top), (shoulder, top), (tip, mid), (shoulder, bottom), (1.0, bottom), (1.0, top + 5.0)]
    d, nx, ny = sd_poly_n(Xu, Yu, body)
    cov = _aniso_coverage(d)
    t = np.clip(-d / 1.6, 0, 1)
    g = grey_band(t, nx, ny, .6)
    r.over(WHITE, cov)
    r.over(np.repeat(g[..., None], 3, axis=2), np.clip(cov - _aniso_coverage(d + 1.6), 0, 1))
    r.over(np.full(3, .5, np.float32), np.clip(_aniso_coverage(d + 3.2) - _aniso_coverage(d + 4.2), 0, 1))
    return r.export()


# ---------------------------------------------------------------------------------------------- wordmark
def _die_layer(r: Raster, cx, cy, size, pips=(1, 2, 3), pip_rgb=None):
    """An ivory enamel die in three-quarter view with lacquer (or given) pips: top, left and right faces."""
    a = size * .5
    top = [(cx, cy - a), (cx + a, cy - a * .5), (cx, cy), (cx - a, cy - a * .5)]
    left = [(cx - a, cy - a * .5), (cx, cy), (cx, cy + a * 1.1), (cx - a, cy + a * .6)]
    right = [(cx, cy), (cx + a, cy - a * .5), (cx + a, cy + a * .6), (cx, cy + a * 1.1)]
    outline = [top[0], top[1], right[2], right[3], left[3], left[0]]
    d = sd_poly(r.X, r.Y, outline)
    r.over(col("5A4020"), coverage(d - size * .025))
    shades = (col("FBF0DA"), col("E6D2AE"), col("CDB58C"))
    pip_rgb = pip_rgb or (col("15110D"),) * 3
    for face, shade, n, pc in zip((top, left, right), shades, pips, pip_rgb):
        r.over(shade, coverage(sd_poly(r.X, r.Y, face) + size * .012))
        (x0, y0), (x1, y1), (x2, y2), (x3, y3) = face
        ux, uy = (x1 - x0), (y1 - y0)
        vx, vy = (x3 - x0), (y3 - y0)
        spots = {1: [(.5, .5)], 2: [(.27, .27), (.73, .73)], 3: [(.25, .25), (.5, .5), (.75, .75)],
                 4: [(.27, .27), (.73, .27), (.27, .73), (.73, .73)],
                 5: [(.25, .25), (.75, .25), (.5, .5), (.25, .75), (.75, .75)],
                 6: [(.27, .22), (.27, .5), (.27, .78), (.73, .22), (.73, .5), (.73, .78)]}[n]
        for su, sv in spots:
            px, py = x0 + ux * su + vx * sv, y0 + uy * su + vy * sv
            rr = size * .075
            dd = np.hypot((r.X - px) / 1.0, (r.Y - py) / .78) - rr
            r.over(pc, coverage(dd))


def _text_mask(r: Raster, text: str, font_path: Path, size_px: float, cx: float, cy: float, spacing: float = 0.0):
    """A glyph coverage mask at the raster's supersampled density, centred on (cx, cy)."""
    f = ImageFont.truetype(str(font_path), int(round(size_px * SS)))
    widths = [f.getlength(ch) for ch in text]
    total = sum(widths) + spacing * SS * (len(text) - 1)
    im = Image.new("L", (r.w * SS, r.h * SS), 0)
    dr = ImageDraw.Draw(im)
    ox = float(r.X[0, 0]) - .5 / SS
    oy = float(r.Y[0, 0]) - .5 / SS
    x = (cx - ox) * SS - total / 2
    for ch, wch in zip(text, widths):
        dr.text((x, (cy - oy) * SS), ch, font=f, fill=255, anchor="lm")
        x += wch + spacing * SS
    return np.asarray(im, np.float32) / 255.0


def around(r: Raster, pts, margin: float = 4.0) -> Raster:
    """The window of a raster (origin 0) that holds a shape and its antialiasing: the same samples, fewer of them."""
    xs, ys = [p[0] for p in pts], [p[1] for p in pts]
    x0, y0 = max(0, int(math.floor(min(xs) - margin))), max(0, int(math.floor(min(ys) - margin)))
    x1, y1 = min(r.w, int(math.ceil(max(xs) + margin))), min(r.h, int(math.ceil(max(ys) + margin)))
    return r.window(x0, y0, max(1, x1 - x0), max(1, y1 - y0))


def wordmark() -> Image.Image:
    """ui_wordmark_dicecraft (1440 x 400): 骰子构筑 in ZCOOL XiaoWei (CP3-2), polished gold letters on a black lacquer
    plaque in the gold double rule, stepped gold shoulders and a sunburst fan crown (card.png's 骰艺工坊 title plate)."""
    W, H = 1440, 400
    r = Raster(W, H)
    cx = W / 2
    plate = (200.0, 132.0, 1240.0, 344.0)
    # Stepped shoulders: three gold bars each side, shortest outermost (ziggurat), bevelled like the frames.
    for side in (-1, 1):
        for i, (reach, top, bottom) in enumerate(((150, 186, 290), (104, 170, 306), (58, 154, 322))):
            x_in = plate[0] + 30 if side < 0 else plate[2] - 30
            x_out = (plate[0] - reach) if side < 0 else (plate[2] + reach)
            box = (min(x_in, x_out), top, max(x_in, x_out), bottom)
            pts = octagon(box, 6)
            w = around(r, pts)
            d, nx, ny = sd_poly_n(w.X, w.Y, pts)
            w.over(col("2A1E10"), coverage(d - 2.0))
            t = np.clip(-d / 9.0, 0, 1)
            w.over(gold_band_rgb(t, nx, ny), coverage(d))
            w.over(LACQUER, coverage(d + 9.0))
    # The sunburst crown behind the plaque's top edge: alternating long and short rays on a crimson half disc.
    fx, fy = cx, plate[1] + 6
    for i, wedge in enumerate(fan_wedges(fx, fy, 128.0, 26.0, 15, math.pi, 2 * math.pi, .28)):
        rr = 1.0 if i % 2 == 0 else .74
        pts = [(fx + (px - fx) * (rr if j in (1, 2) else 1), fy + (py - fy) * (rr if j in (1, 2) else 1))
               for j, (px, py) in enumerate(wedge)]
        w = around(r, pts)
        d, nx, ny = sd_poly_n(w.X, w.Y, pts)
        w.over(col("2A1E10"), coverage(d - 1.5))
        w.over(gold_band_rgb(np.clip(-d / 6.0, 0, 1), nx, ny), coverage(d))
    w = around(r, [(fx - 34, fy - 34), (fx + 34, fy + 34)])
    d = sd_circle(w.X, w.Y, fx, fy, 30.0)
    w.over(col("2A1E10"), coverage(d - 2.0))
    w.over(CRIMSON, coverage(d))
    w.over(GOLD_FLAT, np.clip(coverage(d) - coverage(d + 2.5), 0, 1))
    # The plaque: black lacquer in the double rule.
    pts = octagon(plate, 30.0)
    w = around(r, pts)
    d, nx, ny = sd_poly_n(w.X, w.Y, pts)
    w.over(col("2A1E10"), coverage(d - 2.5))
    w.over(LACQUER, coverage(d))
    w.over(gold_band_rgb(np.clip(-d / 12.0, 0, 1), nx, ny), band(d, 0.0, 12.0))
    w.over(GOLD_FLAT, band(d, 17.0, 20.0))
    # An ivory die at the left inside the plaque, its pips lacquer, red and blue (the game's dice).
    dx, dy, ds = plate[0] + 118, plate[1] + 100, 118
    _die_layer(around(r, [(dx - ds * .6, dy - ds * .6), (dx + ds * .6, dy + ds * .65)]), dx, dy, ds, pips=(5, 3, 2),
               pip_rgb=(col("15110D"), col(PALETTE["red"]), col(PALETTE["blue"])))
    # The letters: gold with a bronze rim, lit from the top (a vertical ramp).
    w = around(r, [(plate[0] + 20, plate[1] + 20), (plate[2] - 20, plate[3] - 20)], 0)
    text = _text_mask(w, "骰子构筑", HEADING, 136.0, cx + 62, plate[1] + 104, spacing=10.0)
    rim = ndimage.grey_dilation(text, size=(9, 9))
    w.over(col("2A1E10"), rim)
    yy = np.clip((w.Y - (plate[1] + 40)) / 130.0, 0, 1)
    w.over(_ramp(.95 - .62 * yy), text)
    return r.export()


# ---------------------------------------------------------------------------------------------- sprites
# Phase 1 (coordinator's brief): the representative pieces below plus the frame variants that share their code; every
# other referenced sprite falls back to the default theme until phase 2.
PHASE1_NAMES = (
    "ui_paper_panel", "ui_panel_hud", "ui_button_bevel", "ui_chip_formula", "ui_card_controller", "ui_section_panel",
    "ui_section_header", "ui_dice_tray", "ui_ring", "ui_topbar", "ui_stage_card", "ui_wordmark_dicecraft",
    # frame variants from the same generators, and the W13 ring every card shows (provisional until phase 2)
    "ui_card_controller_compact", "ui_card_shop", "ui_card_item", "ui_info_inset", "ui_slot_inventory", "ui_tab_bottom",
    "ui_card_frame_rarity",
)


def render_sprite(spec: dict) -> Image.Image:
    name = spec["name"]
    if name == "ui_wordmark_dicecraft":
        return wordmark()
    im = surface(spec)
    _check_slices(name, im, spec["border"], name in NEUTRAL_NAMES)
    return im


def contract_sprites() -> list[dict]:
    """The referenced sprites of the regenerated contract (2026-11-18: 50). Unreferenced ones are not drawn."""
    return [s for s in json.loads(CONTRACT.read_text(encoding="utf-8"))["sprites"] if s["referenced"]]


# ---------------------------------------------------------------------------------------------- table
TABLE_W, TABLE_H = 1920, 1080
# The screen frame: concept pages close the whole screen in a gold double rule. Here a 4 px bevelled band, a lacquer
# gap and a hairline, all within 7.5 px of the screen edge: outside the play layout (top bar from y 12, x 16) and the
# shop's sections (16 px margins, y 12 to 1072).
FRAME_BAND, FRAME_GAP, FRAME_RULE, FRAME_CUT = 4.0, 1.6, 1.2, 14.0
FRAME_INSET = .5
# THEME_SPEC 3.1: large decoration only where no play state puts interface, never cut by a straight line and never
# inside a button's or panel's bounding rectangle. The action row (GameApp.Play11 L11 at 100 % and 150 % text): the main
# button ends at x 1240 (1276 at 150 %), the end slot starts at 1496 and its qualified ring at 1492; the submit row ends
# at y 942. The one rectangle free in every state is x 1276-1492, y 942-1080 (README, action row table); the fan keeps
# 6 units inside it.
ACTION_FREE = (1276.0, 942.0, 1492.0, 1080.0)
FAN_CX, FAN_BASE, FAN_R = 1384.0, 1066.0, 84.0
# Bottom corner fans (roll.png's crimson and gold corner triangles): legs of 24 px in the frame's lower corners, clear of
# the hint panel (x from 32, y to 1046) and the end button with its ring and step (x to 1892, y to 1044).
CORNER_LEG = 24.0
# Bounding rectangles (x, y, w, h) of the play screen's buttons and panels in any state or text size (L11 at 100 % and
# 150 %, UiKit.Button's step, the qualified ring, the tray tabs), which decoration must stay out of.
PLAY_RECTS = {
    "top bar": (16, 12, 1888, 72), "card row": (32, 86, 1448, 254), "stage card": (1496, 96, 392, 244),
    "tray": (32, 338, 1448, 390), "resources": (1496, 352, 392, 376), "submit row": (32, 740, 1448, 202),
    "settle": (1496, 740, 392, 202), "hint": (32, 958, 600, 88), "secondary": (560, 966, 324, 72),
    "primary": (840, 962, 436, 82), "end": (1492, 958, 400, 88),
}


def _touches(r: Raster, x0: float, y0: float, x1: float, y1: float) -> bool:
    return float(r.X.max()) >= x0 and float(r.X.min()) <= x1 and float(r.Y.max()) >= y0 and float(r.Y.min()) <= y1


def frame_layer(r: Raster):
    """The screen's gold double rule (closed octagon at the screen edge)."""
    reach = FRAME_INSET + FRAME_BAND + FRAME_GAP + FRAME_RULE + FRAME_CUT + 4
    if not (float(r.X.min()) < reach or float(r.X.max()) > TABLE_W - reach or float(r.Y.min()) < reach
            or float(r.Y.max()) > TABLE_H - reach):
        return
    box = (FRAME_INSET, FRAME_INSET, TABLE_W - FRAME_INSET, TABLE_H - FRAME_INSET)
    pts = octagon(box, FRAME_CUT)
    d, nx, ny = sd_poly_n(r.X, r.Y, pts)
    r.over(gold_band_rgb(np.clip(-d / FRAME_BAND, 0, 1), nx, ny), band(d, 0.0, FRAME_BAND))
    r.over(GOLD_FLAT, band(d, FRAME_BAND + FRAME_GAP, FRAME_BAND + FRAME_GAP + FRAME_RULE))


def corner_fans(r: Raster):
    """Crimson right triangles with gold edges and three gold rays in the frame's two lower corners."""
    a = FRAME_INSET + FRAME_BAND + FRAME_GAP + FRAME_RULE + 1.5
    for sx, x0 in ((1, a), (-1, TABLE_W - a)):
        y0 = TABLE_H - a
        if not _touches(r, min(x0, x0 + sx * CORNER_LEG) - 4, y0 - CORNER_LEG - 4, max(x0, x0 + sx * CORNER_LEG) + 4, y0 + 4):
            continue
        tri = [(x0, y0), (x0 + sx * CORNER_LEG, y0), (x0, y0 - CORNER_LEG)]
        d, nx, ny = sd_poly_n(r.X, r.Y, tri)
        r.over(CRIMSON, coverage(d))
        r.over(GOLD_FLAT, band(d, 0.0, 1.4))
        for k in (1, 2, 3):
            ang = math.radians(90 * k / 4)
            ex, ey = x0 + sx * math.cos(ang) * CORNER_LEG * .72, y0 - math.sin(ang) * CORNER_LEG * .72
            seg = _segment_sd(r.X, r.Y, x0, y0, ex, ey)
            r.over(GOLD_FLAT, coverage(seg - .55) * coverage(d))


def _segment_sd(X, Y, ax, ay, bx, by):
    ex, ey = bx - ax, by - ay
    t = np.clip(((X - ax) * ex + (Y - ay) * ey) / (ex * ex + ey * ey), 0, 1)
    return np.hypot(X - (ax + ex * t), Y - (ay + ey * t)).astype(np.float32)


def fan_polygons():
    """The bottom-centre sunburst (roll.png): a stepped plinth, a crimson half disc and 13 gold rays in a closed arc."""
    cx, base, R = FAN_CX, FAN_BASE, FAN_R
    plinth = [(cx - 70, base), (cx - 70, base - 8), (cx - 52, base - 8), (cx - 52, base - 15), (cx + 52, base - 15),
              (cx + 52, base - 8), (cx + 70, base - 8), (cx + 70, base)]
    hub_y = base - 15
    rays = []
    for i, wedge in enumerate(fan_wedges(cx, hub_y, R, 22.0, 13, math.pi, 2 * math.pi, .30)):
        rr = 1.0 if i % 2 == 0 else .78
        rays.append([(cx + (px - cx) * (rr if j in (1, 2) else 1), hub_y + (py - hub_y) * (rr if j in (1, 2) else 1))
                     for j, (px, py) in enumerate(wedge)])
    return plinth, (cx, hub_y), rays


def hero_fan(r: Raster):
    X, Y = r.X, r.Y
    x0, y0, x1, y1 = ACTION_FREE
    if float(X.max()) < x0 or float(X.min()) > x1 or float(Y.max()) < y0:
        return
    plinth, (hx, hy), rays = fan_polygons()
    arc = sd_circle(X, Y, hx, hy, FAN_R + 5.0)
    half = Y - np.float32(hy)
    outline = np.maximum(arc, half)                       # the closed half disc that holds the rays
    r.over(col("2A1E10"), coverage(np.abs(outline) - 1.6) * coverage(half - 1.0))
    for ray in rays:
        d, nx, ny = sd_poly_n(X, Y, ray)
        r.over(col("2A1E10"), coverage(d - 1.2))
        r.over(gold_band_rgb(np.clip(-d / 5.0, 0, 1), nx, ny), coverage(d))
    r.over(GOLD_FLAT, coverage(np.abs(arc) - .9) * coverage(half))
    hub = sd_circle(X, Y, hx, hy, 20.0)
    disc = np.maximum(hub, half)
    r.over(CRIMSON, coverage(disc))
    r.over(GOLD_FLAT, coverage(np.abs(hub) - .9) * coverage(half))
    d, nx, ny = sd_poly_n(X, Y, plinth)
    r.over(col("2A1E10"), coverage(d - 1.5))
    r.over(gold_band_rgb(np.clip(-d / 4.0, 0, 1), nx, ny), coverage(d))
    r.over(LACQUER, coverage(d + 4.0))


def page_layer(r: Raster):
    """The non-play backdrop: black lacquer (the felt colour) in the screen's gold double rule; no layout shapes."""
    r.over(col(PALETTE["felt"]), np.ones_like(r.X))
    frame_layer(r)


def table_layer(r: Raster):
    page_layer(r)
    corner_fans(r)
    hero_fan(r)


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
    return render_tiles(table_layer)


def page_scene() -> Image.Image:
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
        print(f"DECO_ZONE {name} rect={x},{y},{w},{h} max_rgb_delta={delta} limit={MAX_FELT_RGB_DEVIATION} "
              f"{'PASS' if passed else 'FAIL'}")
        if not passed:
            raise ValueError(f"Table decoration entered protected zone {name}: {delta}")
    return results


def check_decoration_rects(table: Image.Image) -> dict[str, int]:
    """Fail when any table pixel inside a button's or panel's bounding rectangle differs from the felt."""
    pixels = np.asarray(table).astype(np.int16)
    felt = np.array(rgb8(PALETTE["felt"]), np.int16)
    out = {}
    for name, (x, y, w, h) in PLAY_RECTS.items():
        n = int((np.abs(pixels[y:y + h, x:x + w] - felt).sum(axis=2) > 0).sum())
        out[name] = n
        if n:
            raise ValueError(f"Table decoration inside the {name} rectangle: {n} px")
    return out


def felt_tile() -> Image.Image:
    """Periodic lacquer grain on the felt colour (drawn at 4x, wrap-filtered): mean is the felt colour, no gradient."""
    n = 1024
    rng = np.random.RandomState(SEED)
    grain = rng.randint(-4, 5, (n * SS, n * SS)).astype(np.float32)
    padded = np.pad(grain, ((16, 16), (16, 16)), mode="wrap")
    small = _resize_f(padded, n + 8, n + 8)[4:4 + n, 4:4 + n]
    small = small - small.mean()
    base = np.array(rgb8(PALETTE["felt"]), np.float32)
    rgb = base[None, None, :] + small[..., None] * np.array([1.2, 1.0, .8], np.float32)
    return Image.fromarray(np.clip(np.rint(rgb), 0, 255).astype(np.uint8), "RGB")


# ---------------------------------------------------------------------------------------------- palette report
def palette_report(palette: dict | None = None) -> dict[str, float]:
    """Contrast numbers quoted in the README and asserted by the tests (WCAG 2 sRGB relative luminance)."""
    p = dict(PALETTE, **W20X_TOKENS) if palette is None else palette
    chip_face = LABEL_FILL_HEX
    value_fill, warning_fill = drawn(chip_face, p.get("valueBadge", p["gold"])), drawn(chip_face, p["danger"])
    sec = lerp_hex(p["background"], p["panel"], .55)
    rim = lerp_hex(p["panel"], p["panelLight"], .55)
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
        f"{best_label(p['gold'], p)[0]}/gold (label pick)": best_label(p["gold"], p)[1],
        "muted/panelLight (disabled button)": contrast(p["muted"], p["panelLight"]),
        "gold/panel": contrast(p["gold"], p["panel"]),
        "teal/panel": contrast(p["teal"], p["panel"]),
        "danger/paper": contrast(p["danger"], p["paper"]),
        "ghost/felt": contrast(p["ghost"], p["felt"]),
        "panel/felt (top bar edge)": contrast(p["panel"], p["felt"]),
        f"secondary chip {sec}/felt": contrast(sec, p["felt"]),
        f"textColor/secondary chip {sec}": contrast(p["textColor"], sec),
        f"tray rim {rim}/felt": contrast(rim, p["felt"]),
        "gold/panelLight (done vs unreached points)": contrast(p["gold"], p["panelLight"]),
        "valueBadge/danger badge dE00 (drawn)": ciede2000(lab(value_fill), lab(warning_fill)),
        "valueBadge/danger badge contrast (drawn)": contrast(value_fill, warning_fill),
        f"{best_label(value_fill, p)[0]}/valueBadge (drawn)": best_label(value_fill, p)[1],
        f"{best_label(warning_fill, p)[0]}/danger badge (drawn)": best_label(warning_fill, p)[1],
        "primary/pressure dE00": de00(p["primary"], p["pressure"]),
        "primary/boss dE00": de00(p["primary"], p["boss"]),
        "pressure/boss dE00": de00(p["pressure"], p["boss"]),
    }
    rar = ("rarityCommon", "rarityAdvanced", "rarityRare")
    for i, a in enumerate(rar):
        for b in rar[i + 1:]:
            out[f"{a}/{b}"] = contrast(p[a], p[b])
        out[f"primary/{a} dE"] = delta_e(p["primary"], p[a])
        out[f"primary/{a} dE00"] = de00(p["primary"], p[a])
    out["pressure saturation"] = colorsys.rgb_to_hsv(*(v / 255 for v in rgb8(p["pressure"])))[1]
    out["pressure hue (deg)"] = colorsys.rgb_to_hsv(*(v / 255 for v in rgb8(p["pressure"])))[0] * 360
    # W20X: the section's centre (baked, x section), the crimson caption strips (white strip x caption).
    cell = drawn(LACQUER_HEX, p["section"])
    out["muted/section cell"] = contrast(p["muted"], cell)
    out["textColor/section cell"] = contrast(p["textColor"], cell)
    caption = drawn("FFFFFF", p["caption"])
    out[f"{best_label(caption, p)[0]}/caption (label pick)"] = best_label(caption, p)[1]
    out["muted/caption (tray tab preferred colour)"] = contrast(p["muted"], caption)
    return out


LABEL_FILL_HEX = IVORY_HEX    # ui_chip_label's face (phase 2 bakes it ivory): the value and warning badges' host


# ---------------------------------------------------------------------------------------------- build
def theme_json() -> str:
    data = dict(THEME_META)
    data["palette"] = dict(PALETTE, **W20X_TOKENS)
    return json.dumps(data, ensure_ascii=False, indent=2) + "\n"


def build(output_root: Path = ROOT, names=None) -> list[Path]:
    """Write the implemented sprites, the table and page scenes, the felt tile and theme.json."""
    output_root = Path(output_root).resolve()
    if not output_root.is_relative_to(ROOT):
        raise ValueError("Output root must stay inside the deco worktree")
    _RENDERED.clear()            # every build renders from scratch (two builds must agree on their own)
    written = []

    def save(relative: Path, im: Image.Image):
        path = output_root / relative
        path.parent.mkdir(parents=True, exist_ok=True)
        # Explicit parameters, no timestamp/EXIF/ICC or machine-specific metadata.
        im.save(path, format="PNG", optimize=False, compress_level=9)
        written.append(path)

    table = table_scene()
    check_felt_zones(table)          # fail before writing any exported art
    check_decoration_rects(table)
    specs = contract_sprites()
    wanted = set(names or PHASE1_NAMES)
    sprites: dict[str, Image.Image] = {}
    for spec in specs:
        if spec["name"] not in wanted:
            continue
        if spec["name"] == "ui_card_controller_compact":
            im = rendered("ui_card_controller").copy()      # the compact face is the same frame (W13 reads its insets)
        else:
            im = rendered(spec["name"])
        if im.mode != "RGBA" or list(im.size) != spec["size"]:
            raise ValueError("Contract mismatch: " + spec["name"])
        sprites[spec["name"]] = im
        save(THEME / "UI" / (spec["name"] + ".png"), im)
    save(THEME / "Table/table_scene.png", table)
    save(THEME / "Table/page_scene.png", page_scene())
    save(THEME / "Table/felt_tile.png", felt_tile())
    theme_path = output_root / THEME / "theme.json"
    theme_path.write_text(theme_json(), encoding="utf-8", newline="\n")
    written.append(theme_path)
    return written


if __name__ == "__main__":
    if "--report" in sys.argv:
        for k, v in palette_report().items():
            print(f"{k:58s} {v:8.2f}")
    elif "--review" in sys.argv:
        import importlib.util
        spec_ = importlib.util.spec_from_file_location("deco_review", Path(__file__).with_name("review_deco.py"))
        mod = importlib.util.module_from_spec(spec_)
        spec_.loader.exec_module(mod)
        mod.main(sys.argv[1:])
    else:
        for p in build():
            print("DECO_WROTE", p.relative_to(ROOT).as_posix())
