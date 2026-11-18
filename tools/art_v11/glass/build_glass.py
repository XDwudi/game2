"""W20: deterministic 彩绘玻璃 (glass) UI kit: gilt frames and leaded glass, 4x raster, one Lanczos downsample.

Style authority: the five concept pages the user chose at checkpoint 3 (ArtSource/StyleKit/themes/glass/concepts) and the
palette samples of ArtSource/StyleKit/themes/glass/style_kit.json. Their texts, numbers and layouts are not a contract; the
game layout is (docs/specs/v11/PLAY_LAYOUT.md, SHOP_LAYOUT.md). The contract (tools/art_v11/theme_contract.json) owns pixel
sizes, nine-slice borders and pixels per unit. Repository fonts only; no image generation, no Unity files.

The shape language, taken from the concept pages (README.md, "Shapes and materials"):
- surfaces the game draws as they are (panels, cards, labels, wells, slots) are framed in gilt: a thin lead edge, a
  gold moulding lit from the upper left, a lead line, a gold hairline and a lead line, so every frame is the concept's
  double gilt fillet; the corners are cut at 45 degrees as on the concept's slots, buttons and panels;
- surfaces the game tints (buttons, chips, the top bar, the stage card, the trays, the rings) are pieces of glass in
  lead came: a black came, a bevel one shade darker than the glass, a flat white face that takes the palette colour;
- strips that slice only across (the shop's section caption, the progress bar, the locked-die ledge) end in chevron
  points, as the concept's banners, stage tag and start button do;
- the table is the concept's dark slate floor; the stained glass lives where no play state puts interface: two narrow
  lancet windows in the side margins and one rose window in the action row's always-free rectangle (THEME_SPEC 3.1).
  Table/page_scene.png, behind the non-play screens, is the plain slate.

Rules the art keeps (checked by tests/test_art_v11_glass.py):
- every nine-slice centre is one flat colour (or transparent) from the border line inward;
- stretched edge bands only change across the edge: every band is a function of the distance to the silhouette and of
  the edge's direction, so it is constant along each straight edge; ornament stays in the corner squares;
- sprites the code multiplies by a palette colour (NEUTRAL_NAMES) stay achromatic, with a pure white face;
- the protected table zones are exactly the felt colour.

`build_glass.py --card-review` writes ArtSource/Art11/glass/card_template_review.png from the shipped sprites (an offline
review, not part of the build).
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
THEME = Path("DicePrototype/Assets/Resources/Themes/glass")
REVIEW = Path("ArtSource/Art11/glass")
CONCEPTS = Path("ArtSource/StyleKit/themes/glass/concepts")
ANCHORS = Path("ArtSource/StyleKit/themes/glass/anchors")
CONCEPT_PAGES = ("roll", "shop", "select", "card", "triptych")
FONT = ROOT / "DicePrototype/Assets/Game/Fonts/NotoSansSC-Bold.otf"
BODY_FONT = ROOT / "DicePrototype/Assets/Game/Fonts/NotoSansCJKsc-Regular.otf"
# CP3-2: the title font the user approved for this style (OFL, W21 download).
HEADING = ROOT / "DicePrototype/Assets/Game/Fonts/Themes/glass/NotoSerifSC-Black.otf"
LATIN = ROOT / "DicePrototype/Assets/TextMesh Pro/Fonts/LiberationSans.ttf"
SS = 4
SEED = 20261118

# ---------------------------------------------------------------------------------------------- palette
# theme.json palette (THEME_SPEC section 2). The generator is the single source: build() writes theme.json from it, and
# the table felt is exactly PALETTE["felt"]. Reasons and contrast numbers: tools/art_v11/glass/README.md.
PALETTE = {
    "background": "121118",   # the darkest slate: page background
    "felt": "292833",         # the concept's slate floor (roll.png dice area median 292831; the plate's 2B2A33)
    "panel": "1E2036",        # night glass: dark panels, the top bar, secondary chips (light text on it)
    "panelLight": "6B5632",   # antique bronze: normal-stage tag, frame edges, empty item rings, disabled faces
    "paper": "F1DFC2",        # parchment (concept 2.1 roll/select parchment F2D4B2, lifted for ink contrast)
    "ink": "1B1720",          # lead black: text on parchment
    "textColor": "F7EFDF",    # marble white: text on the dark panels
    "muted": "BEB4A6",        # worn parchment grey: secondary text on the dark panels
    "gold": "F0BE52",         # gilt: prices, coins, highlights (style kit amber F8C761)
    "teal": "62C894",         # light emerald: done, positive
    # red and blue also colour the 3D dice pips (DiceTable reads UiKit.Red / UiKit.Blue). Pip colour is the game's main
    # functional cue, so every style keeps the default pair (coordinator 2026-11-17, CP3-1; THEME_SPEC section 2).
    "red": "EC655D",
    "blue": "55B6D9",
    "violet": "9C74D6",       # amethyst: enchantments
    "danger": "B0281F",       # ruby: errors, boss warnings
    "shadow": "0B0A0F",
    "ghost": "8E7A5A",        # tarnished bronze: empty slot rings, the tray's inner line, outline buttons, goods' ring
    "primary": "2152C4",      # cobalt glass: the one main action per phase (concept roll "重掷", select "开始挑战")
    "reward": "FFD567",       # bright gilt: reward moments
    "boss": "A8231B",         # ruby glass (concept boss tag A40313)
    "pressure": "EE6A2C",     # vermilion glass: a warning hue, far from the cobalt main button
    "rarityCommon": "B7BDC9", # clear silver glass
    "rarityAdvanced": "2E9A66",  # emerald glass
    "rarityRare": "B2397F",   # rose-window magenta
    "onFelt": "E9DEC8",       # marble white marks drawn straight on the slate
}
# THEME_SPEC 2 W20X keys (implemented by W20X, 2026-11-17).
W20X_TOKENS = {
    "buttonStep": "8C6B33",          # the step under a UiKit.Button is gilt: the glass tile sits on a gold base
    "buttonStepPrimary": "B08A3E",
    "section": "FFFFFF",             # ui_section_panel is authored (gilt frame, night-glass face): drawn as it is
    "caption": "EBD6B0",             # parchment captions: the shop's section strips and the dice-pool tabs
}
THEME_META = {
    "id": "glass",
    "displayName": "彩绘玻璃",
    "description": ("石板桌面上的哥特彩绘玻璃：面板与卡牌是带双道金边的切角金框，被代码着色的按钮与筹码是铅条里的玻璃块，"
                    "分区标题与进度条两端是尖角，彩窗只在两侧窄边与行动行常空处的一扇玫瑰窗。依据："
                    "ArtSource/StyleKit/themes/glass/concepts 与 style_kit.json；界面件与色板由 "
                    "tools/art_v11/glass/build_glass.py 生成（说明见同目录 README.md）"),
    "order": 5,
    "preview": True,
}


def rgb8(value: str) -> tuple[int, int, int]:
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def col(value: str) -> np.ndarray:
    return np.array(rgb8(value), np.float32) / 255.0


def hexc(c) -> str:
    return "".join(f"{int(round(float(v) * 255)):02X}" for v in c)


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


def lerp_hex(a: str, b: str, t: float) -> str:
    """Color.Lerp on the palette's sRGB values (the code mixes tints this way before drawing)."""
    return hexc(col(a) * (1 - t) + col(b) * t)


# ---------------------------------------------------------------------------------------------- art colours
# Authored (untinted) art: lead, gilt and glass. Sampled from the concept pages (concepts/roll.png, shop.png) and the style
# kit's colour roles (lead 0F0E12, amber F8C761 / deep amber E89D1B, ruby BB210D, cobalt 0643A0, sky 31A6EF, violet 7A55A8).
LEAD = col("16131B")
LEAD_SOFT = col("2A2430")
GOLD_HI = col("FFEDB0")
GOLD = col("DDA94A")
GOLD_MID = col("BE8A35")
GOLD_SH = col("7E5420")
GOLD_DEEP = col("4E3214")
PAPER = col(PALETTE["paper"])
PANEL = col(PALETTE["panel"])
WELL = col("15162A")        # sunken wells (score frame, budget wells): a shade below the night glass
SLOT = col("1A1924")        # empty niches (card slots, item slots)
RUBY = col("9E2219")
COBALT = col("1F48A8")
AMBER = col("C98A2A")
EMERALD = col("23784E")
VIOLET = col("6B4598")
WHITE = np.ones(3, np.float32)
K = np.zeros(3, np.float32)   # pure black for tinted (achromatic) sprites
# Light comes from the upper left: edges whose outward normal faces it are lit.
LIGHT = np.array((-.45, -.89), np.float32) / np.float32(math.hypot(.45, .89))

# Sprites the game multiplies by palette colours: they stay achromatic (white face, grey bevel, black came).
NEUTRAL_NAMES = frozenset({
    "ui_button_bevel", "ui_button_ghost", "ui_frame_block",
    "ui_ring", "ui_progress_fill", "ui_progress_track",
    "ui_section_header", "ui_pip_dot", "ui_icon_arrow", "ui_icon_book",
    "ui_icon_gear", "fx_arc_short", "fx_score_spark", "fx_square_chip",
    "ui_dice_tray", "ui_topbar", "ui_chip_formula", "ui_stage_card", "ui_icon_refresh",
    # W13 card template (CARD_TEMPLATE 6): tinted by the rarity colours, or Ghost for goods (GameApp.Card11 TintedSprite13).
    "ui_card_frame_rarity", "ui_gem_common", "ui_gem_advanced", "ui_gem_rare",
})
CARD_TEMPLATE_NAMES = ("ui_card_frame_rarity", "ui_gem_common", "ui_gem_advanced", "ui_gem_rare")
# Calm zones of the play screen (PLAY_LAYOUT 1 and 8, W12b; THEME_SPEC 3.1): one flat felt colour, no texture.
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
    return np.clip(.5 - np.asarray(d) * SS, 0.0, 1.0).astype(np.float32)


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

    def over_premult(self, prgb, alpha):
        a = np.clip(alpha, 0, 1).astype(np.float32)
        self.rgb[...] = prgb + self.rgb * (1 - a[..., None])
        self.a[...] = a + self.a * (1 - a)

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


def outside_distance(mask: np.ndarray) -> np.ndarray:
    return (ndimage.distance_transform_edt(mask <= .5) / SS).astype(np.float32)


def mix(a, b, t):
    t = np.asarray(t, np.float32)
    return a + (b - a) * t[..., None]


def smoothstep(a: float, b: float, x):
    t = np.clip((np.asarray(x) - a) / (b - a), 0.0, 1.0)
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


def octagon(box, cuts):
    """A box with 45-degree corner cuts (top-left, top-right, bottom-right, bottom-left), in px; 0 keeps a corner."""
    x0, y0, x1, y1 = box
    tl, tr, br, bl = cuts
    pts = [(x0, y0 + tl), (x0 + tl, y0)] if tl else [(x0, y0)]
    pts += [(x1 - tr, y0), (x1, y0 + tr)] if tr else [(x1, y0)]
    pts += [(x1, y1 - br), (x1 - br, y1)] if br else [(x1, y1)]
    pts += [(x0 + bl, y1), (x0, y1 - bl)] if bl else [(x0, y1)]
    return pts


def star4(cx, cy, r, waist=.22, rot=0.0):
    """The concept's four-point gilt star: long points on the axes, a narrow waist between them."""
    pts = []
    for i in range(8):
        a = rot + math.pi * i / 4 - math.pi / 2
        rr = r if i % 2 == 0 else r * waist
        pts.append((cx + math.cos(a) * rr, cy + math.sin(a) * rr))
    return pts


def lit_of(d) -> np.ndarray:
    """How much an edge faces the light: the outward normal (gradient of the distance) dotted with LIGHT."""
    gy, gx = np.gradient(d.astype(np.float32), np.float32(1.0 / SS))
    n = np.maximum(np.hypot(gx, gy), 1e-6)
    return ((gx * LIGHT[0] + gy * LIGHT[1]) / n).astype(np.float32)


# ---------------------------------------------------------------------------------------------- frame painting
def paint_bands(win: Raster, d, bands, face=None, lit=None):
    """Bands from the silhouette inward: [(width, paint)], paint an RGB triple or a callable (t, lit) -> RGB array with t
    the position across the band (0 outer, 1 inner). Then the face (flat) over everything deeper than the bands."""
    inset = 0.0
    for width, paint in bands:
        a = coverage(d + inset)
        rgb = paint(np.clip((-d - inset) / width, 0, 1), lit) if callable(paint) else paint
        win.over(rgb, a)
        inset += width
    if face is not None:
        win.over(face, coverage(d + inset))
    return inset


def paint_hollow(win: Raster, d, bands, lit=None):
    """Open frame: [(width, paint, alpha)] from the silhouette inward; transparent beyond the last band (and wherever a
    band's alpha is 0, e.g. the gap of a double line)."""
    prgb = np.zeros(win.rgb.shape, np.float32)
    alpha = np.zeros(win.a.shape, np.float32)
    inset = 0.0
    for width, paint, a in bands:
        m = coverage(d + inset) - coverage(d + inset + width)
        if a > 0:
            rgb = paint(np.clip((-d - inset) / width, 0, 1), lit) if callable(paint) else np.asarray(paint, np.float32)
            prgb += np.asarray(rgb, np.float32) * (m * a)[..., None]
            alpha += m * a
        inset += width
    win.over_premult(prgb, alpha)
    return inset


def band_render(w: int, h: int, depth: int, draw, fill):
    """Render only the four border bands (the centre is one flat colour or transparent by construction)."""
    r = Raster(w, h)
    D = depth
    if fill is not None:
        r.rgb[D * SS:(h - D) * SS, D * SS:(w - D) * SS] = np.asarray(fill, np.float32)
        r.a[D * SS:(h - D) * SS, D * SS:(w - D) * SS] = 1.0
    for x, y, ww, hh in ((0, 0, w, D), (0, h - D, w, D), (0, D, D, h - 2 * D), (w - D, D, D, h - 2 * D)):
        draw(r.window(x, y, ww, hh))
    return r.export()


def gilt_moulding(t, lit):
    """The gold moulding across its width: a bright ridge a third of the way in, falling to shade at the inner side;
    edges that face the light are brighter, the far edges deeper (constant along every straight edge)."""
    t = np.asarray(t, np.float32)
    lit = np.zeros_like(t) if lit is None else lit
    ridge = np.exp(-((t - .3) / .2) ** 2) * (.62 + .38 * lit)
    out = mix(np.broadcast_to(GOLD, t.shape + (3,)), GOLD_SH, smoothstep(.45, 1.0, t) * (.75 - .25 * lit))
    out = mix(out, GOLD_HI, np.clip(ridge, 0, 1))
    out = mix(out, GOLD_DEEP, np.clip(-lit, 0, 1) * .35)
    return out


def gilt_hairline(t, lit):
    lit = np.zeros_like(np.asarray(t, np.float32)) if lit is None else lit
    return mix(np.broadcast_to(GOLD_MID, lit.shape + (3,)), GOLD_HI, np.clip(lit, 0, 1) * .45)


# The double gilt fillet (concept roll.png slots and panels, about 9 px at 1920 wide): px at PPU 200 (2 px a unit).
GILT = ((2.0, LEAD), (7.0, gilt_moulding), (2.0, LEAD), (2.0, gilt_hairline), (2.0, LEAD))
GILT_CUT = 12.0       # px: the corners of gilt frames (6 units)


def corner_reach(cut: float, frame: float) -> float:
    """How far into its corner square a cut corner's bands reach: the frame's inner contour meets the side line
    cut + frame * (2 - sqrt 2) px from the corner (inward offsets of a cut corner shorten it by 0.586 x the depth)."""
    return max(cut + frame * (math.sqrt(2) - 1), frame)


def gilt_tile(w: int, h: int, face, *, cut: float = GILT_CUT, bands=GILT, depth: int = 48, border: float = 40.0,
              open_bottom: bool = False) -> Image.Image:
    """A nine-sliced gilt surface: lead edge, gold moulding, lead, gold hairline, lead, then the flat face."""
    frame = sum(wd for wd, _ in bands)
    if corner_reach(cut, frame) > border - 3:
        raise ValueError("gilt corner reaches past its corner square")
    pts = octagon((.5, .5, w - .5, (h + 64.0) if open_bottom else h - .5), (cut, cut, 0 if open_bottom else cut,
                                                                          0 if open_bottom else cut))

    def draw(win):
        d = sd_poly(win.X, win.Y, pts)
        paint_bands(win, d, bands, face, lit_of(d))
    return band_render(w, h, depth, draw, face)


# Lead came for the tinted glass pieces: black came, a bevel one shade darker than the glass (lit edges lighter), white.
def came_bevel(lo: float, hi: float):
    def paint(t, lit):
        lit = np.zeros_like(np.asarray(t, np.float32)) if lit is None else lit
        g = lo + (hi - lo) * (.5 + .5 * lit) + (1 - np.asarray(t, np.float32)) * 0.0
        return np.repeat(np.asarray(g, np.float32)[..., None], 3, axis=-1)
    return paint


def corner_lights(win: Raster, d, w: int, h: int, cut: float, inset: float, offset: float, lead: float, shade: float):
    """Leaded corner lights: in each corner, a lead line parallel to the cut, `offset` px inside the face's own cut, closes
    a small triangular pane (a shade below the face) - the corner pieces of a leaded panel. Corner squares only."""
    X, Y = win.X, win.Y
    face = coverage(d + inset)
    c_line = cut + inset * math.sqrt(2) + offset
    for u in ((X - .5) + (Y - .5), (w - .5 - X) + (Y - .5), (w - .5 - X) + (h - .5 - Y), (X - .5) + (h - .5 - Y)):
        piece = coverage((u - (c_line - lead * .7)) / math.sqrt(2)) * face
        win.over(np.full(3, shade, np.float32), piece)
        win.over(K, coverage(np.abs(u - c_line) / math.sqrt(2) - lead / 2) * face)


def came_tile(w: int, h: int, *, cut: float, came: float, bevel: float, lo: float = .62, hi: float = .86,
              depth: int = 48, border: float = 40.0, lights: float = 0.0) -> Image.Image:
    """A tinted glass piece in lead came: black came, a bevel a shade below the glass, the white face; optionally leaded
    corner lights (`lights` px inside the face's cut)."""
    bands = ((came, K), (bevel, came_bevel(lo, hi)))
    reach = corner_reach(cut, came + bevel) + (lights * 1.5 + 3.0 if lights else 0.0)
    if reach > border - 3:
        raise ValueError("came corner reaches past its corner square")
    pts = octagon((.5, .5, w - .5, h - .5), (cut,) * 4)

    def draw(win):
        d = sd_poly(win.X, win.Y, pts)
        inset = paint_bands(win, d, bands, WHITE, lit_of(d))
        if lights:
            corner_lights(win, d, w, h, cut, inset, lights, 2.5, .9)
    return band_render(w, h, depth, draw, WHITE)


def came_hollow(w: int, h: int, *, cut: float, bands, depth: int = 48, border: float = 40.0, inset: float = .5) -> Image.Image:
    """An open tinted frame: [(width, grey or paint, alpha)] from the silhouette inward, the centre transparent."""
    total = sum(b[0] for b in bands)
    if corner_reach(cut, total) > border - 3:
        raise ValueError("open frame corner reaches past its corner square")
    pts = octagon((inset, inset, w - inset, h - inset), (cut,) * 4)

    def draw(win):
        d = sd_poly(win.X, win.Y, pts)
        paint_hollow(win, d, [(wd, (np.full(3, p, np.float32) if np.isscalar(p) else p), a) for wd, p, a in bands],
                     lit_of(d))
    return band_render(w, h, depth, draw, None)


# ---------------------------------------------------------------------------------------------- sliced surfaces
CHIP_CUT, BUTTON_CUT, BAR_CUT, STAGE_CUT, TRAY_CUT, RING_CUT = 8.0, 10.0, 12.0, 14.0, 22.0, 16.0
# ui_ring: a double line (the concept's double gilt slot frames once tinted gold): a black-edged line, a gap, a thin line.
RING_BANDS = ((1.5, 0.0, 1.0), (5.0, came_bevel(.86, 1.0), 1.0), (1.5, 0.0, 1.0), (2.5, 0.0, 0.0), (1.0, 0.0, 1.0),
              (2.5, 1.0, 1.0), (1.0, 0.0, 1.0))
# The W13 rarity ring (CARD_TEMPLATE 6): lead, a band of tinted glass, lead; 10 px (the default ring is 12 px).
RARITY_BANDS = ((2.0, 0.0, 1.0), (6.0, came_bevel(.84, 1.0), 1.0), (2.0, 0.0, 1.0))
RARITY_CUT = 9.0
TRAY_BANDS = ((2.5, 0.0, 1.0), (8.0, came_bevel(.8, 1.0), 1.0), (1.5, 0.0, 1.0))


def surface(spec: dict) -> Image.Image:
    """Nine-sliced and horizontally sliced surfaces. Frames stay in the border bands, centres stay flat."""
    name = spec["name"]
    w, h = spec["size"]
    if name in ("ui_paper_panel", "ui_card_controller", "ui_card_controller_compact", "ui_card_shop"):
        return gilt_tile(w, h, PAPER)
    if name == "ui_card_item":
        # Consumables: the gilt moulding with a cobalt glass fillet inside instead of the gold hairline.
        bands = ((2.0, LEAD), (7.0, gilt_moulding), (2.0, LEAD), (2.0, COBALT), (2.0, LEAD))
        return gilt_tile(w, h, PAPER, bands=bands)
    if name in ("ui_panel_hud", "ui_section_panel"):
        return gilt_tile(w, h, PANEL)
    if name == "ui_tab_bottom":
        return gilt_tile(w, h, PANEL, open_bottom=True)
    if name == "ui_info_inset":
        return well(w, h)
    if name == "ui_slot_inventory":
        return niche(w, h)
    if name == "ui_chip_label":
        bands = ((3.0, LEAD), (2.0, GOLD_MID), (1.5, LEAD))
        return gilt_tile(w, h, PAPER, cut=7.0, bands=bands)
    if name == "ui_tray_front":
        return tray_front(w, h)
    # Achromatic, tinted by the game.
    if name == "ui_topbar":
        return came_tile(w, h, cut=BAR_CUT, came=5.0, bevel=5.0, lo=.58, hi=.84, lights=8.0)
    if name == "ui_chip_formula":
        return came_tile(w, h, cut=CHIP_CUT, came=4.0, bevel=3.0, lo=.6, hi=.86, lights=6.0)
    if name == "ui_stage_card":
        return came_tile(w, h, cut=STAGE_CUT, came=5.0, bevel=6.0, lo=.62, hi=.86, depth=56, border=48.0, lights=9.0)
    if name == "ui_button_bevel":
        return came_tile(w, h, cut=BUTTON_CUT, came=5.0, bevel=4.0, lo=.56, hi=.86, lights=7.0)
    if name == "ui_dice_tray":
        return came_hollow(w, h, cut=TRAY_CUT, bands=TRAY_BANDS, depth=72, border=72.0)
    if name == "ui_ring":
        return came_hollow(w, h, cut=RING_CUT, bands=RING_BANDS)
    if name == "ui_button_ghost":
        return came_hollow(w, h, cut=BUTTON_CUT, bands=((1.0, 0.0, 1.0), (3.0, 1.0, 1.0), (1.0, 0.0, 1.0)))
    if name == "ui_card_frame_rarity":
        return came_hollow(w, h, cut=RARITY_CUT, bands=RARITY_BANDS, inset=0.0)
    if name == "ui_frame_block":
        pts = octagon((.5, .5, w - .5, h - .5), (6.0,) * 4)
        return band_render(w, h, 48, lambda win: win.over(WHITE, coverage(sd_poly(win.X, win.Y, pts))), WHITE)
    if name == "ui_section_header":
        return section_header(w, h)
    if name in ("ui_progress_fill", "ui_progress_track"):
        return progress_bar(w, h, name == "ui_progress_fill")
    raise ValueError("Unimplemented sliced sprite: " + name)


def well(w: int, h: int) -> Image.Image:
    """A sunken well (UiKit.Inset: the score frame, the budget wells): a lead rim with a gold lower lip catching the light,
    a darker inner shadow along the top."""
    pts = octagon((.5, .5, w - .5, h - .5), (8.0,) * 4)

    def lip(t, lit):
        lit = np.zeros_like(np.asarray(t, np.float32)) if lit is None else lit
        return mix(np.broadcast_to(LEAD, lit.shape + (3,)), GOLD_MID, np.clip(-lit, 0, 1) * .85)

    def draw(win):
        d = sd_poly(win.X, win.Y, pts)
        paint_bands(win, d, ((2.0, LEAD), (2.0, lip), (1.0, LEAD)), WELL, lit_of(d))
    return band_render(w, h, 48, draw, WELL)


def niche(w: int, h: int) -> Image.Image:
    """An empty niche (card strips, item slots, the shop's die cells): a dark slate recess in a thin bronze fillet."""
    bands = ((2.0, LEAD), (2.5, gilt_hairline), (2.0, LEAD), (2.0, LEAD_SOFT))
    return gilt_tile(w, h, SLOT, cut=10.0, bands=bands)


def _aniso_coverage(d):
    """Coverage for a distance measured in anisotropic display units: rescale by the local gradient."""
    gy, gx = np.gradient(d, np.float32(1.0 / SS))
    g = np.maximum(np.sqrt(gx * gx + gy * gy), 1e-3)
    return coverage(d / g)


# W14 shop (GameApp.Shop14 Section14): the header strip spans the section panel's inside, ShopHead14 (68) - f.w - 2 units
# tall, f.w the section panel's top frame inset. GILT is 15 px (7.5 units) plus antialiasing: 8 units -> 58 units.
SECTION_HEADER_UNITS = 58.0
CHEVRON = 11.0         # units: the depth of the banner's pointed ends (the title starts 14 units in)


def section_header(w: int, h: int) -> Image.Image:
    """Shop section caption (border 40/0/72/0, sliced only across): a banner with chevron ends, lead came and a bevel,
    white so `caption` colours it. Drawn in display units (0.5 per px across, 58/128 down) because Unity scales only the
    height of a horizontally sliced strip."""
    r = Raster(w, h)
    sx, sy = .5, SECTION_HEADER_UNITS / h
    Xu, Yu = r.X * sx, r.Y * sy
    W, H = w * sx, SECTION_HEADER_UNITS
    top, bottom, mid = .5, H - .5, H / 2
    body = [(1.0, mid), (1.0 + CHEVRON, top), (W - 8.0 - CHEVRON, top), (W - 8.0, mid), (W - 8.0 - CHEVRON, bottom),
            (1.0 + CHEVRON, bottom)]
    d = sd_poly(Xu, Yu, body)
    gy, gx = np.gradient(d, np.float32(1.0 / SS))
    lit = ((gx * LIGHT[0] * sx + gy * LIGHT[1] * sy) / np.maximum(np.hypot(gx * sx, gy * sy), 1e-6)).astype(np.float32)
    r.over(K, _aniso_coverage(d))
    bev = came_bevel(.62, .9)(np.zeros_like(d), lit)
    r.over(bev, _aniso_coverage(d + 2.0))
    r.over(WHITE, _aniso_coverage(d + 3.5))
    return r.export()


def progress_bar(w: int, h: int, fill: bool) -> Image.Image:
    """A slim bar with chevron ends (border 48/0/48/0, drawn at a uniform scale: pixelsPerUnitMultiplier 48 / height). The
    fill is lit glass (white with a darker lower half); the track a dark groove in lead."""
    r = Raster(w, h)
    X, Y = r.X, r.Y
    body = [(3.0, h / 2), (3.0 + 36.0, 3.0), (w - 3.0 - 36.0, 3.0), (w - 3.0, h / 2), (w - 3.0 - 36.0, h - 3.0),
            (3.0 + 36.0, h - 3.0)]
    d = sd_poly(X, Y, body)
    r.over(K, coverage(d))
    if fill:
        tone = np.where(Y < h * .55, 1.0, .8).astype(np.float32)
    else:
        tone = np.where(Y < h * .4, .32, .55).astype(np.float32)
    r.over(np.repeat(tone[..., None], 3, axis=2), coverage(d + 9.0))
    return r.export()


def tray_front(w: int, h: int) -> Image.Image:
    """Front ledge of a locked-die slot (border 40/0/40/0; drawn 20 units tall, so designed in display units: 0.5 x,
    0.125 y units per px): a bronze ledge with a gilt top edge and chevron ends."""
    r = Raster(w, h)
    sx, sy = .5, 20.0 / h
    Xu, Yu = r.X * sx, r.Y * sy
    W = w * sx
    body = [(.6, 10.0), (8.0, .5), (W - 8.0, .5), (W - .6, 10.0), (W - 8.0, 19.5), (8.0, 19.5)]
    d = sd_poly(Xu, Yu, body)
    r.over(LEAD, _aniso_coverage(d))
    inner = _aniso_coverage(d + 1.4)
    r.over(col("3A2A1A"), inner)
    r.over(GOLD, inner * smoothstep(5.6, 4.8, Yu))
    r.over(GOLD_HI, inner * smoothstep(3.4, 2.6, Yu) * smoothstep(1.4, 2.2, Yu))
    return r.export()


# ---------------------------------------------------------------------------------------------- wordmark
def die_faces(r: Raster, cx, cy, size, pips=(1, 2, 3), pip_rgb=None, ink=4.0):
    """A marble die in a three-quarter view: lead outline, pale marble faces lit from the upper left, dark pips (the style
    kit: dice are pale marble, pips dark unless the subject names red or blue)."""
    a = size * .5
    top = [(cx, cy - a), (cx + a, cy - a * .5), (cx, cy), (cx - a, cy - a * .5)]
    left = [(cx - a, cy - a * .5), (cx, cy), (cx, cy + a * 1.1), (cx - a, cy + a * .6)]
    right = [(cx, cy), (cx + a, cy - a * .5), (cx + a, cy + a * .6), (cx, cy + a * 1.1)]
    whole = np.maximum.reduce([poly_mask(r, f) for f in (top, left, right)])
    r.over(LEAD, np.maximum(whole, (outside_distance(whole) < ink).astype(np.float32)))
    for face, tone in ((top, col("FBF6EC")), (left, col("E6DCCB")), (right, col("CFC3B0"))):
        r.over(tone, poly_mask(r, face))
    seam = line_mask(r, [top[3], top[2], top[1]], ink * .5)
    r.over(LEAD, np.maximum(seam, line_mask(r, [top[2], left[2]], ink * .5)))
    layouts = {1: [(.5, .5)], 2: [(.28, .28), (.72, .72)], 3: [(.25, .25), (.5, .5), (.75, .75)],
               4: [(.28, .28), (.72, .28), (.28, .72), (.72, .72)],
               5: [(.26, .26), (.74, .26), (.5, .5), (.26, .74), (.74, .74)],
               6: [(.28, .22), (.28, .5), (.28, .78), (.72, .22), (.72, .5), (.72, .78)]}
    colours = pip_rgb or (LEAD, LEAD, LEAD)
    for face, n, colour in zip((top, left, right), pips, colours):
        p0, p1, p2, p3 = face
        for u, v in layouts[n]:
            x = p0[0] + (p1[0] - p0[0]) * u + (p3[0] - p0[0]) * v
            y = p0[1] + (p1[1] - p0[1]) * u + (p3[1] - p0[1]) * v
            rr = size * .072
            r.over(colour, ell_mask(r, x, y, rr, rr * .8))


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


def glass_pane(r: Raster, m: np.ndarray, base, t, glow: float = .45, edge: float = .5):
    """A backlit glass piece filling mask m. t is an analytic depth into the piece (0 at its lead, 1 at its middle), so a
    piece cut by a render tile shades the same in both tiles: brighter towards the middle, grisaille-dark at the lead."""
    base = np.asarray(base, np.float32)
    t = np.clip(np.asarray(t, np.float32), 0, 1)
    rgb = mix(np.broadcast_to(base * (1 - edge * .6), m.shape + (3,)), base, smoothstep(0, .5, t))
    rgb = mix(rgb, np.minimum(base * 1.3 + .1, 1.0), smoothstep(.4, 1.0, t) * glow)
    r.over(rgb, m)


def rose_window(r: Raster, cx: float, cy: float, R: float, petals: int = 8):
    """A rose window: gilt rim, a ring of alternating ruby and cobalt petals in lead, an amber roundel with a gilt
    four-point star. Shading is analytic (tile-independent)."""
    X, Y = r.X, r.Y
    rad = np.hypot(X - cx, Y - cy)
    ang = np.arctan2(Y - cy, X - cx)
    r.over(LEAD, coverage(rad - R))
    rim = coverage(rad - (R - 1.6)) * (1 - coverage(rad - (R - 1.6 - .1 * R)))
    r.over(mix(np.broadcast_to(GOLD, rad.shape + (3,)), GOLD_HI, np.clip(-(np.sin(ang) * .89 + np.cos(ang) * .45), 0, 1) * .6),
           rim)
    inner_r, outer_r = R * .42, R * .86
    mid_r = (inner_r + outer_r) / 2
    step = 2 * math.pi / petals
    phase = (ang + math.pi / 2 + step / 2) % (2 * math.pi)
    sector = np.floor(phase / step).astype(int)
    off = (phase - (sector + .5) * step) * rad                   # px from the petal's middle line, along the arc
    half_arc = step * .5 * rad
    t_arc = (half_arc - np.abs(off)) / np.maximum(half_arc, 1e-3)
    lw = max(.9, .012 * R)
    for k in range(petals):
        for lo, hi, shade in ((inner_r, mid_r, .82), (mid_r, outer_r, 1.0)):
            m = (sector == k).astype(np.float32) * ((rad > lo) & (rad < hi))
            if m.any():
                t_r = np.minimum(rad - lo, hi - rad) / max((hi - lo) * .5, 1e-3)
                glass_pane(r, m, (RUBY if k % 2 == 0 else COBALT) * shade, np.minimum(t_r, t_arc * 2.0), glow=.4)
    lead = coverage((half_arc - np.abs(off)) - lw) * ((rad < outer_r + .5) & (rad > inner_r - .5))
    for ring_r in (outer_r, inner_r, mid_r):
        lead = np.maximum(lead, coverage(np.abs(rad - ring_r) - lw))
    r.over(LEAD, lead)
    core = coverage(rad - (inner_r - 1.2))
    glass_pane(r, core, AMBER, 1 - rad / max(inner_r, 1e-3), glow=.55)
    star = poly_mask(r, star4(cx, cy, inner_r * .8, .26))
    r.over(LEAD, np.maximum(star, (outside_distance(star) < max(.8, R * .008)).astype(np.float32)) * core)
    r.over(GOLD_HI, star * core)


def wordmark() -> Image.Image:
    """Title wordmark: 骰子构筑 in Noto Serif SC Black (CP3-2, this style's title font): lead-black letters edged in gilt,
    and a rose window with a marble die on the left; a gilt four-point star at the end. Transparent ground (the menu draws
    it on a parchment panel)."""
    W, H = 1440, 400
    r = Raster(W, H)
    rose_window(r, 196, 200, 172, petals=12)
    die_faces(r, 196, 190, 150, pips=(1, 4, 5), ink=6.0)
    text = "骰子构筑"
    probe = ImageFont.truetype(str(HEADING), 100)
    bx = probe.getbbox(text)
    size = int(min(980 / (bx[2] - bx[0]), 236 / (bx[3] - bx[1])) * 100)
    font = ImageFont.truetype(str(HEADING), size * SS)
    b = font.getbbox(text)
    tw, th = (b[2] - b[0]) / SS, (b[3] - b[1]) / SS
    tx, ty = 400 + (1400 - 400 - tw) * .5, (H - th) * .5 - 6
    letters = r.mask(lambda d, s: d.text((tx * SS - b[0], ty * SS - b[1]), text, font=font, fill=255))
    edge = outside_distance(letters)
    r.over(GOLD_DEEP, coverage(edge - 5.5))
    r.over(GOLD, coverage(edge - 4.0))
    r.over(GOLD_HI, coverage(edge - 2.0) * (1 - coverage(edge - .8)) * .55)
    r.over(LEAD, letters)
    star = poly_mask(r, star4(1405, 92, 30, .2))
    r.over(GOLD_DEEP, np.maximum(star, (outside_distance(star) < 2.5).astype(np.float32)))
    r.over(GOLD, star)
    r.over(GOLD_HI, poly_mask(r, star4(1405, 92, 16, .2)))
    return r.export()


# ---------------------------------------------------------------------------------------------- sprite list
# W14's shop stopped drawing ui_price_chip, ui_pedestal and ui_frame_enchant; the contract regenerated on 2026-11-18 lists
# them as unreferenced, and this kit does not draw them (STATE, W14 decision 4: "黑金与彩绘玻璃按 50 张做").
def contract_sprites() -> list[dict]:
    return [s for s in json.loads(CONTRACT.read_text(encoding="utf-8"))["sprites"] if s["referenced"]]


def render_sprite(spec: dict) -> Image.Image:
    name = spec["name"]
    if any(spec["border"]):
        return surface(spec)
    if name == "ui_wordmark_dicecraft":
        return wordmark()
    raise NotImplementedError(name)


def implemented(spec: dict) -> bool:
    return any(spec["border"]) or spec["name"] == "ui_wordmark_dicecraft"


# ---------------------------------------------------------------------------------------------- table
TABLE_W, TABLE_H = 1920, 1080
FELT = col(PALETTE["felt"])
# THEME_SPEC 3.1 (coordinator 2026-11-17): large decoration only where no play state puts interface, never cut by a
# straight line, never inside a button's or panel's bounding rectangle. The play screen leaves the side margins (left of
# x 32 below the top bar, right of x 1888) and, in the action row, the rectangle right of the main button (its focus
# outline ends at x 1279 with 150 % text) and left of the end-stage slot (its qualified ring starts at 1492): x 1280-1488,
# y 942-1080 in every DrawActions11 state (README, action-row table).
ACTION_FREE = (1280.0, 942.0, 1488.0, 1080.0)
ROSE = (1384.0, 1012.0, 54.0)          # centre and radius: x 1330-1438, y 958-1066
# The two lancet windows (x0, y0 = apex, x1, y1 = sill): the left one clears the first card's focus outline (x 29), the
# right one the end-stage slot's qualified ring (x 1892); both start below the top bar (y 84).
WINDOWS = ((3.0, 94.0, 25.0, 1066.0), (1895.0, 94.0, 1917.0, 1066.0))
BLEED = 3.0   # px: antialiasing and the Lanczos footprint around every drawn edge
# Bounding rectangles (x, y, w, h) of the play screen's buttons and panels in any state or text size (GameApp.Play11 L11
# at 100 % and 150 %; UiKit.Button's step and its focus outline 3 units out, the qualified ring), which decoration must
# stay out of.
PLAY_RECTS = {
    "top bar": (16, 12, 1888, 72), "card row": (29, 93, 1454, 250), "stage card": (1496, 96, 392, 244),
    "tray": (32, 338, 1448, 390), "resources": (1496, 352, 392, 376), "submit row": (32, 740, 1448, 202),
    "settle": (1496, 740, 392, 202), "hint": (32, 958, 600, 88), "secondary": (557, 963, 330, 78),
    "primary": (837, 959, 442, 88), "end": (1492, 958, 400, 89),
}


def lancet(x0: float, y0: float, x1: float, y1: float, steps: int = 24):
    """A lancet window outline: an equilateral pointed arch (each arc centred on the opposite springing point) with its
    apex at y0, then straight jambs down to a flat sill at y1."""
    w = x1 - x0
    spring = y0 + w * math.sqrt(3) / 2
    pts = [(x1 + w * math.cos(math.radians(180 + 60 * i / steps)), spring + w * math.sin(math.radians(180 + 60 * i / steps)))
           for i in range(steps + 1)]
    pts += [(x0 + w * math.cos(math.radians(300 + 60 * i / steps)), spring + w * math.sin(math.radians(300 + 60 * i / steps)))
            for i in range(1, steps + 1)]
    return pts + [(x1, y1), (x0, y1)]


WINDOW_DIAMONDS = (col("84221C"), col("21489E"))     # alternating ruby and cobalt quarries
WINDOW_FIELD = col("141C40")                          # the deep blue glass between them


def window_layer(r: Raster, box):
    """One narrow lancet window: a gilt edge in lead, then a column of tall diamond quarries, alternately ruby and cobalt,
    on deep blue glass; each piece lit from behind (analytic shading), lead between them. Dim jewel tones, so the windows
    frame the table without pulling the eye from the play."""
    x0, y0, x1, y1 = box
    d = sd_poly(r.X, r.Y, lancet(x0, y0, x1, y1))
    if float(d.min()) > 2.0:
        return
    frame = paint_bands(r, d, ((1.2, LEAD), (2.6, gilt_moulding), (1.2, LEAD)), None, lit_of(d))
    glass = coverage(d + frame)
    cx = (x0 + x1) / 2
    half = (x1 - x0) / 2 - frame + .6
    pitch = 2.0 * half * 2.4
    rel = r.Y - (y0 + 10.0)
    row = np.floor(rel / pitch).astype(int)
    v = rel - (row + .5) * pitch
    dia = np.abs(r.X - cx) / half + np.abs(v) / (pitch / 2)            # < 1 inside the row's diamond
    norm = half * (pitch / 2) / math.hypot(half, pitch / 2)              # dia units -> px from a diamond edge
    inside = (dia <= 1.0).astype(np.float32) * glass
    t_in = (1 - dia) * norm / (half * .5)
    for k, colour in enumerate(WINDOW_DIAMONDS):
        m = inside * (np.mod(row, 2) == k)
        if m.any():
            glass_pane(r, m, colour, t_in, glow=.35, edge=.55)
    field = (dia > 1.0).astype(np.float32) * glass
    glass_pane(r, field, WINDOW_FIELD, np.minimum((dia - 1) * norm, half - np.abs(r.X - cx)) / (half * .35), glow=.2, edge=.4)
    r.over(LEAD, coverage(np.abs(dia - 1.0) * norm - .65) * glass)


def table_layer(r: Raster, play: bool):
    """Slate everywhere; on the play table the two lancet windows and the rose window."""
    r.over(FELT, np.ones_like(r.X))
    if not play:
        return
    for box in WINDOWS:
        x0, y0, x1, y1 = box
        if float(r.X.max()) > x0 - 4 and float(r.X.min()) < x1 + 4 and float(r.Y.max()) > y0 - 4:
            window_layer(r, box)
    cx, cy, R = ROSE
    if float(r.X.max()) > cx - R - 4 and float(r.X.min()) < cx + R + 4 and float(r.Y.max()) > cy - R - 4:
        rose_window(r, cx, cy, R)


def _check_decoration_geometry():
    cx, cy, R = ROSE
    x0, y0, x1, y1 = ACTION_FREE
    if cx - R < x0 + 4 or cx + R > x1 - 4 or cy - R < y0 + 4 or cy + R > y1 - 4:
        raise ValueError("The rose window must sit wholly inside the action row's free rectangle")
    for name, (x, y, w, h) in PLAY_RECTS.items():
        for bx0, by0, bx1, by1 in WINDOWS + ((cx - R, cy - R, cx + R, cy + R),):
            if bx0 - BLEED < x + w and bx1 + BLEED > x and by0 - BLEED < y + h and by1 + BLEED > y:
                raise ValueError(f"Table decoration inside the bounding rectangle of {name}")
    for name, (x, y, w, h) in FELT_ZONES.items():
        for bx0, by0, bx1, by1 in WINDOWS + ((cx - R, cy - R, cx + R, cy + R),):
            if bx0 - BLEED < x + w and bx1 + BLEED > x and by0 - BLEED < y + h and by1 + BLEED > y:
                raise ValueError(f"Table decoration inside the calm zone {name}")


_check_decoration_geometry()
TABLE_TILE = 240


def render_tiles(layer) -> Image.Image:
    """A full 1920 x 1080 image rendered in tiles with an 8 px margin (beyond the Lanczos footprint) and stitched; tiles
    with nothing but slate are filled directly."""
    canvas = np.zeros((TABLE_H, TABLE_W, 3), np.float32)
    canvas[...] = np.array(rgb8(PALETTE["felt"]), np.float32)
    margin = 8
    for y in range(0, TABLE_H, TABLE_TILE):
        for x in range(0, TABLE_W, TABLE_TILE):
            w, h = min(TABLE_TILE, TABLE_W - x), min(TABLE_TILE, TABLE_H - y)
            r = Raster(w + 2 * margin, h + 2 * margin, float(x - margin), float(y - margin))
            layer(r)
            if np.all(r.a >= 1.0) and np.all(np.abs(r.rgb - FELT) < 1e-7):
                continue
            im = np.asarray(r.export()).astype(np.float32)
            canvas[y:y + h, x:x + w] = im[margin:margin + h, margin:margin + w, :3]
    return Image.fromarray(np.clip(np.rint(canvas), 0, 255).astype(np.uint8), "RGB")


def table_scene() -> Image.Image:
    """The play screen's table (THEME_SPEC 3.1): slate, the two lancet windows and the rose window."""
    return render_tiles(lambda r: table_layer(r, True))


def page_scene() -> Image.Image:
    """THEME_SPEC 3 Table/page_scene.png (W20X): the non-play screens' backdrop, the plain slate (no layout shapes)."""
    return render_tiles(lambda r: table_layer(r, False))


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
        print(f"GLASS_ZONE {name} rect={x},{y},{w},{h} max_rgb_delta={delta} "
              f"limit={MAX_FELT_RGB_DEVIATION} {'PASS' if passed else 'FAIL'}")
        if not passed:
            raise ValueError(f"Table decoration entered protected zone {name}: {delta}")
    return results


def felt_tile() -> Image.Image:
    """Periodic slate grain on the felt colour, drawn at 4x and wrap-filtered; mean is the felt colour, no gradient."""
    n = 1024
    rng = np.random.RandomState(SEED)
    grain = rng.randint(-5, 6, (n * SS, n * SS)).astype(np.float32)
    padded = np.pad(grain, ((16, 16), (16, 16)), mode="wrap")
    small = np.array(_resize_f(padded, n + 8, n + 8)[4:4 + n, 4:4 + n], np.float32)
    small -= np.float32(small.mean())
    base = np.array(rgb8(PALETTE["felt"]), np.float32)
    rgb = base[None, None, :] + small[..., None] * np.array([1.5, 1.5, 1.7], np.float32)
    return Image.fromarray(np.clip(np.rint(rgb), 0, 255).astype(np.uint8), "RGB")


# ---------------------------------------------------------------------------------------------- offline composites
# Python emulation of how the game draws (not Unity captures): UI sprites sampled bilinear without mipmaps (the theme UI
# import), sliced at PPU 200, tinted and blended in linear light (the project's colour space). Coordinates are 1080p design
# units. Sprites the theme does not ship fall back to the default sprite, as UiKit.Template does.
def _lin_arr(c):
    return np.where(c <= .04045, c / 12.92, ((c + .055) / 1.055) ** 2.4)


def _gam_arr(c):
    return np.where(c <= .0031308, c * 12.92, 1.055 * np.power(np.clip(c, 0, None), 1 / 2.4) - .055)


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


def frame_insets_px(pixels: np.ndarray, border) -> tuple[int, int, int, int]:
    """GameApp.FrameInsets11 (left, bottom, right, top) in sprite px: along the middle row and column, within the slice
    border, the last pixel that is transparent or differs from the centre fill by more than 48. Unity rows run up."""
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
    """GameApp.Card11 RingInsets13 (left, bottom, right, top) in sprite px: the last pixel with alpha >= 128."""
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


class Kit:
    """The theme's sprites with the default sprite for each one the theme does not ship (UiKit.Template's fallback)."""

    def __init__(self, sprites: dict | None = None, folder: Path | None = None):
        self.specs = {s["name"]: s for s in json.loads(CONTRACT.read_text(encoding="utf-8"))["sprites"]}
        self.own = dict(sprites or {})
        if folder is not None and folder.exists():
            for path in sorted(folder.glob("*.png")):
                if path.stem not in self.own:
                    with Image.open(path) as source:
                        self.own[path.stem] = source.convert("RGBA")
        self.fallback: set[str] = set()
        self._img: dict[str, Image.Image] = {}
        self._lin: dict[str, np.ndarray] = {}

    def image(self, name: str) -> Image.Image:
        if name not in self._img:
            if name in self.own:
                self._img[name] = self.own[name]
            else:
                with Image.open(ROOT / self.specs[name]["default"]) as source:
                    self._img[name] = source.convert("RGBA")
                self.fallback.add(name)
        return self._img[name]

    def lin(self, name: str) -> np.ndarray:
        if name not in self._lin:
            self._lin[name] = _lin_texture(self.image(name))
        return self._lin[name]

    def border(self, name: str):
        return self.specs[name]["border"]

    def ppu(self, name: str) -> float:
        return self.specs[name]["pixelsPerUnit"]

    def insets(self, name: str, mult: float = 1.0) -> tuple[float, float, float, float]:
        """FrameInsets11 in design units (left, bottom, right, top)."""
        k = self.ppu(name) / 100.0 * mult
        return tuple(v / k for v in frame_insets_px(np.asarray(self.image(name)), self.border(name)))

    def ring_insets(self, name: str) -> tuple[float, float, float, float]:
        k = self.ppu(name) / 100.0
        return tuple(v / k for v in ring_insets_px(np.asarray(self.image(name)), self.border(name)))

    def centre(self, name: str) -> str:
        px = np.asarray(self.image(name))
        return hexc(px[px.shape[0] // 2, px.shape[1] // 2, :3] / 255.0)


_FONTS: dict[tuple, ImageFont.FreeTypeFont] = {}


def font_at(path: Path, px: float) -> ImageFont.FreeTypeFont:
    key = (str(path), max(6, int(round(px))))
    if key not in _FONTS:
        _FONTS[key] = ImageFont.truetype(str(path), key[1])
    return _FONTS[key]


def text_width(text: str, size: float, bold: bool = True) -> float:
    return font_at(FONT if bold else BODY_FONT, size).getlength(text)


class Canvas:
    """Linear-light premultiplied canvas in screen pixels; `scale` is screen px per design unit (1080p 1, 720p 2/3)."""

    def __init__(self, w: int, h: int, background: str = "000000", scale: float = 1.0):
        self.scale = scale
        self.buf = np.zeros((h, w, 4), np.float32)
        self.buf[..., :3] = _lin_arr(col(background))
        self.buf[..., 3] = 1.0
        self.glyphs: list[tuple] = []     # (label, x0, y0, x1, y1) of every text's inked box, design units

    def backdrop(self, im: Image.Image):
        self.buf[...] = _lin_texture(im.convert("RGBA").resize((self.buf.shape[1], self.buf.shape[0]), Image.Resampling.LANCZOS))

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

    def sprite(self, kit: Kit, name: str, x, y, w, h, tint=None, alpha: float = 1.0, mult: float = 1.0,
               sliced: bool | None = None, preserve: bool = False):
        tex = kit.lin(name)
        border = kit.border(name)
        sliced = any(border) if sliced is None else sliced
        th, tw = tex.shape[:2]
        if preserve and not sliced:
            k = min(w / tw, h / th)
            x, y, w, h = x + (w - tw * k) / 2, y + (h - th * k) / 2, tw * k, th * k
        x0, y0, W, H = self._rect(x, y, w, h)
        if sliced:
            l, b, r, t = border
            k = 100.0 / (kit.ppu(name) * mult) * self.scale
            us, vs = _slice_axis(W, tw, l, r, l * k, r * k), _slice_axis(H, th, t, b, t * k, b * k)
        else:
            us, vs = (np.arange(W) + .5) / W * tw, (np.arange(H) + .5) / H * th
        layer = _bilinear(tex, us, vs)
        if tint is not None:
            layer[..., :3] *= _lin_arr(col(tint) if isinstance(tint, str) else np.asarray(tint, np.float32))
        if alpha < 1:
            layer *= alpha
        self._over(layer, x0, y0)

    def picture(self, im: Image.Image, x, y, size, dim: float = 1.0):
        x0, y0, S, _ = self._rect(x, y, size, size)
        pic = im.convert("RGBA")
        pic.thumbnail((S, S), Image.Resampling.LANCZOS)
        layer = _lin_texture(pic)
        if dim < 1:
            layer[..., :3] *= _lin_arr(np.float32(dim))
        self._over(layer, x0 + (S - pic.width) // 2, y0 + (S - pic.height) // 2)

    def fill(self, x, y, w, h, colour: str, alpha: float = 1.0):
        x0, y0, W, H = self._rect(x, y, w, h)
        layer = np.zeros((H, W, 4), np.float32)
        layer[..., :3] = _lin_arr(col(colour)) * alpha
        layer[..., 3] = alpha
        self._over(layer, x0, y0)

    def text(self, x, y, w, h, value: str, size: float, colour: str, align: str = "ml", bold: bool = False,
             spacing: float = 0.0, label: str | None = None, font: Path | None = None):
        """A TMP-like text box: align = horizontal (l, m, r) + vertical (t, m); lines split on \\n; spacing is TMP's
        lineSpacing (em / 100). Returns the inked box in design units."""
        if not value:
            return None
        f = font_at(font or (FONT if bold else BODY_FONT), size * self.scale)
        asc, desc = f.getmetrics()
        lines = value.split("\n")
        line_h = asc + desc
        adv = line_h + spacing / 100.0 * size * self.scale
        block = line_h + adv * (len(lines) - 1)
        X0, Y0 = x * self.scale, y * self.scale
        Wd, Hd = w * self.scale, h * self.scale
        top = Y0 if align[1] == "t" else Y0 + (Hd - block) / 2
        probe = ImageDraw.Draw(Image.new("L", (1, 1)))
        placed, inked = [], []
        for i, line in enumerate(lines):
            lw = f.getlength(line)
            lx = X0 if align[0] == "l" else X0 + (Wd - lw) / 2 if align[0] == "m" else X0 + Wd - lw
            base = top + i * adv + asc
            placed.append((lx, base, line))
            inked.append(probe.textbbox((lx, base), line, font=f, anchor="ls"))
        ox, oy = int(math.floor(min(b[0] for b in inked))) - 2, int(math.floor(min(b[1] for b in inked))) - 2
        lw_, lh_ = int(math.ceil(max(b[2] for b in inked))) + 2 - ox, int(math.ceil(max(b[3] for b in inked))) + 2 - oy
        layer = Image.new("RGBA", (max(1, lw_), max(1, lh_)), (0, 0, 0, 0))
        draw = ImageDraw.Draw(layer)
        for lx, base, line in placed:
            draw.text((lx - ox, base - oy), line, font=f, fill=rgb8(colour) + (255,), anchor="ls")
        a = np.asarray(layer).astype(np.float32) / 255.0
        lay = np.empty_like(a)
        lay[..., 3] = a[..., 3]
        lay[..., :3] = _lin_arr(a[..., :3]) * a[..., 3:]
        self._over(lay, ox, oy)
        box = (min(b[0] for b in inked) / self.scale, min(b[1] for b in inked) / self.scale,
               max(b[2] for b in inked) / self.scale, max(b[3] for b in inked) / self.scale)
        self.glyphs.append((label or value, *box))
        return box

    def image(self) -> Image.Image:
        rgb = _gam_arr(self.buf[..., :3] / np.maximum(self.buf[..., 3:4], 1e-6))
        return Image.fromarray(np.clip(np.rint(rgb * 255), 0, 255).astype(np.uint8), "RGB")


def label_on(fill: str, P: dict, preferred: str | None = None) -> tuple[str, float, int]:
    """GameApp.LabelOn11 on a drawn fill: a meaning colour (hex) stays at 4.5:1; otherwise the best of ink, paper and
    textColor at 4.95:1. Returns (colour, ratio, steps): steps > 0 when the game would deepen the fill instead (the mock
    reports it; the composites in this kit never need it, as the notes print)."""
    if preferred is not None and contrast(preferred, fill) >= 4.5:
        return preferred, contrast(preferred, fill), 0
    key, ratio = best_label(fill, P)
    return P[key], ratio, (0 if ratio >= 4.95 else 1)


def body11(size: float, fs: float) -> float:
    """GameApp.Body11: a body size that ends at no less than 19.5 design units after the text scale."""
    return max(size, 19.5 / fs)


def line11(size: float, fs: float) -> float:
    return math.ceil(size * fs * 1.46) + 2


def wrap_cjk(text: str, size: float, width: float, bold: bool = False) -> list[str]:
    """Greedy CJK wrap; closing punctuation never starts a line."""
    f = font_at(FONT if bold else BODY_FONT, size)
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


def balanced_lines(text: str, size: float, width: float, bold: bool = False) -> list[str]:
    """GameApp.BalancedLines11: break once where the halves are most even, preferring punctuation or a " · "."""
    f = font_at(FONT if bold else BODY_FONT, size)
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


_DIE_CACHE: dict[tuple, Image.Image] = {}


def die_image(px: int, pips=(1, 2, 3), colours=("red", "blue", "blue"), palette: dict | None = None) -> Image.Image:
    """A stand-in for the 3D marble die (the game renders real dice): pips in the dice pip colours."""
    key = (px, tuple(pips), tuple(colours))
    if key not in _DIE_CACHE:
        P = palette or PALETTE
        r = Raster(int(px * 1.4), int(px * 1.45))
        die_faces(r, px * .7, px * .62, px, pips=pips, pip_rgb=tuple(col(P[c]) for c in colours), ink=max(1.5, px * .04))
        _DIE_CACHE[key] = r.export()
    return _DIE_CACHE[key]


def button(c: Canvas, kit: Kit, P: dict, x, y, w, h, face: str, label: str, size: float, label_colour: str | None = None,
           enabled: bool = True, primary: bool = False, notes: list | None = None, name: str = "button"):
    """UiKit.Button: the step (the face sprite) at (2, 8, w - 4, h - 6), the face at (0, 0, w, h - 7), the 35 % lip; the
    label centred on the face (8 units in from each side)."""
    step = (P["buttonStepPrimary"] if primary else P["buttonStep"]) if enabled else P["shadow"]
    c.sprite(kit, "ui_button_bevel", x + 2, y + 8, w - 4, h - 6, tint=step)
    c.sprite(kit, "ui_button_bevel", x, y, w, h - 7, tint=face)
    if enabled:
        c.fill(x + 14, y + 5, w - 28, 1, "FFFFFF", .35)
    fill = drawn(kit.centre("ui_button_bevel"), face)
    colour, ratio, steps = label_on(fill, P) if label_colour is None else (label_colour, contrast(label_colour, fill), 0)
    if notes is not None:
        notes.append(f"{name}: face {fill}, label {colour} {ratio:.2f}:1" + (" (game would adjust the face)" if steps else ""))
    c.text(x + 8, y, w - 16, h - 7, label, size, colour, "mm", bold=False)
    return fill


def solid_button(c: Canvas, kit: Kit, P: dict, x, y, w, h, label: str, size: float, enabled: bool = True,
                 notes: list | None = None, name: str = "solid button", icon: str | None = None, spacing: float = 0):
    """GameApp.SolidButton11: ui_chip_formula times lerp(Background, Panel, .55) (a disabled one a quarter toward
    PanelLight), the label LabelOn11 picks (Muted when disabled), centred on the whole chip (FlatButton's label)."""
    tint = lerp_hex(P["background"], P["panel"], .55)
    if not enabled:
        tint = lerp_hex(tint, P["panelLight"], .25)
    c.sprite(kit, "ui_chip_formula", x, y, w, h, tint=tint)
    fill = drawn(kit.centre("ui_chip_formula"), tint)
    colour, ratio, _ = label_on(fill, P, P["textColor"]) if enabled else (P["muted"], contrast(P["muted"], fill), 0)
    if notes is not None:
        notes.append(f"{name}: face {fill}, label {colour} {ratio:.2f}:1")
    if icon:
        s = min(34, h - 24)
        tw = text_width(label, size, False)
        start = (w - (s + 8 + tw)) / 2
        c.sprite(kit, icon, x + start, y + (h - s) / 2, s, s, tint=colour, preserve=True)
        c.text(x + start + s + 8, y, w, h, label, size, colour, "lm")
    else:
        lines = label.split("\n")
        c.text(x + 8, y, w - 16, h, label, size, colour, "mm", spacing=spacing)
    return fill


# Stand-in cards for the composites (the four glass StyleKit anchors, cycled; names and short texts from the game).
MOCK_CARDS = (
    ("common", "排头兵", "第一颗提交的骰子点数×4，加到基础分。", "C108"),
    ("advanced", "蓝色浪潮", "本手提交时，提交的骰子全部按蓝色计算。", "I10"),
    ("rare", "点金手", "每次提交后，最后一颗骰子朝上的面永久镀金。", "C117"),
    ("common", "增加提交", "提交栏+1格。", "U10"),
    ("advanced", "大量提交", "提交栏+2格。", "C108"),
    ("rare", "再给一次", "每手投掷次数+1。", "I10"),
    ("common", "多交一手", "每关提交次数+1。", "C117"),
    ("advanced", "配重", "这一面权重+1（上限5），更容易掷出这一面。", "U10"),
)
RARITY_KEY = {"common": "rarityCommon", "advanced": "rarityAdvanced", "rare": "rarityRare", None: "ghost"}
_ANCHOR_CACHE: dict[str, Image.Image] = {}


def anchor_art(name: str) -> Image.Image:
    if name not in _ANCHOR_CACHE:
        with Image.open(ROOT / ANCHORS / (name + ".png")) as source:
            _ANCHOR_CACHE[name] = source.convert("RGBA")
    return _ANCHOR_CACHE[name]


def tall_card(c: Canvas, kit: Kit, P: dict, x, y, w, h, rarity, title, text, art, fs: float = 1.0, ordinal: int = 0,
              shop: bool = False, price: int = -1, category: str | None = None, value: str | None = None):
    """GameApp.Card11 DrawTallCard13 (Table and Shop sizes): the face, the rarity ring on the face's inner rect, the gem
    across the ring's top, the ordinal disc, the name, the picture (value badge top left) and the short text; Shop adds
    the 60-unit price button."""
    face = "ui_card_shop" if shop else "ui_card_controller"
    c.sprite(kit, face, x, y, w, h)
    fl, fb, fr, ft = kit.insets(face)
    inner = (x + fl, y + ft, w - fl - fr, h - ft - fb)
    tint = P[RARITY_KEY[rarity]]
    c.sprite(kit, "ui_card_frame_rarity", *inner, tint=tint)
    rl, rb, rr, rt = kit.ring_insets("ui_card_frame_rarity")
    content = (inner[0] + rl + 4, inner[1] + rt + 3, inner[2] - rl - rr - 8, inner[3] - rt - rb - 6)
    gem = 34 if shop else 30
    gem_y = inner[1] + (content[1] - 3 - inner[1]) * .5
    goods = rarity is None
    top = content[1] if goods else max(content[1], gem_y + gem * .5 + 2)
    disc = math.ceil(max(22, 14 * fs * 1.5)) if ordinal and not shop else 0
    if disc:
        c.sprite(kit, "ui_pip_dot", content[0], content[1], disc, disc, tint=P["ink"], preserve=True)
        c.text(content[0], content[1], disc, disc, str(ordinal), min(14, disc * .6 / fs) * fs, label_on(P["ink"], P)[0], "mm",
               bold=True)
    price_h = 60.0 if shop and price >= 0 else 0.0
    bottom = content[1] + content[3] - (price_h + 6 if price_h else 0)
    inset_l = disc + 4 if disc else 0
    yy = top
    face_fill = kit.centre(face)
    ink = label_on(face_fill, P, P["ink"])[0]
    if goods and category:
        c.text(content[0], yy, content[2], line11(16, fs), category, 16 * fs, label_on(face_fill, P, lerp_hex(P["ink"], P["paper"], .28))[0],
               "mt")
        yy += line11(16, fs)
    name_h = line11(22, fs)
    c.text(content[0] + inset_l, yy, content[2] - 2 * inset_l, name_h, title, 22 * fs, ink, "mm", bold=True)
    art_top = yy + name_h + 2
    size = 20 * fs
    lines = wrap_cjk(text, size, content[2])
    text_h = math.ceil(len(lines) * size * 1.448 - (len(lines) - 1) * .14 * size) + 2
    art_size = max(min(40, content[2] - 8), min(content[2] - 8, bottom - art_top - 4 - text_h))
    c.picture(anchor_art(art), x + (w - art_size) / 2, art_top, art_size)
    if value:
        badge(c, kit, P, x + (w - art_size) / 2 + 2, art_top + 2, value, 18 * fs, fs)
    c.text(content[0], art_top + art_size + 4, content[2], text_h, "\n".join(lines), size, ink, "mt", spacing=-14)
    if not goods:
        c.sprite(kit, "ui_gem_" + rarity, x + w / 2 - gem / 2, gem_y - gem / 2, gem, gem, tint=tint)
    if price_h:
        price_button(c, kit, P, content[0], content[1] + content[3] - price_h, content[2], price_h, price)


def badge(c: Canvas, kit: Kit, P: dict, x, y, value: str, size: float, fs: float, warning: bool = False):
    """GameApp.Card11 ValueBadge13: ui_chip_label times valueBadge (gold when unset) or Danger, bold label LabelOn11 picks."""
    tint = P["danger"] if warning else P.get("valueBadge", P["gold"])
    fl, fb, fr, ft = kit.insets("ui_chip_label")
    w = text_width(value, size, True) + 2 + fl + fr + 12
    h = math.ceil(size * 1.25) + 10
    c.sprite(kit, "ui_chip_label", x, y, w, h, tint=tint)
    fill = drawn(kit.centre("ui_chip_label"), tint)
    c.text(x + fl, y + ft, w - fl - fr, h - ft - fb, value, size, label_on(fill, P)[0], "mm", bold=True)


def price_button(c: Canvas, kit: Kit, P: dict, x, y, w, h, price: int):
    """GameApp.Card11 PriceButton13 on one row: SolidButton11 with the coin, the price in gold and 购买."""
    tint = lerp_hex(P["background"], P["panel"], .55)
    c.sprite(kit, "ui_chip_formula", x, y, w, h, tint=tint)
    fill = drawn(kit.centre("ui_chip_formula"), tint)
    word = "购买"
    total = 24 + 4 + text_width(str(price), 22, True) + 8 + text_width(word, 20, False)
    sx = x + (w - total) / 2
    c.sprite(kit, "ui_icon_coin", sx, y + (h - 24) / 2, 24, 24, preserve=True)
    c.text(sx + 28, y, 40, h, str(price), 22, label_on(fill, P, P["gold"])[0], "lm", bold=True)
    c.text(sx + 28 + text_width(str(price), 22, True) + 8, y, 60, h, word, 20, label_on(fill, P, P["textColor"])[0], "lm")


def compact_card(c: Canvas, kit: Kit, P: dict, x, y, w, h, rarity, title, text, art, fs: float = 1.0, ordinal: int = 0,
                 notes: list | None = None, price: int = -1, price_w: float = 0.0, category: str | None = None):
    """GameApp.Card11 DrawCompactCard13: the framed icon block with the gem across its foot, the ordinal disc and the name
    beside it; a mini card shows its short text only when it fits whole at the body size; a tall one (h >= 200) puts it
    under the block. A priced compact card is a shop goods row (W14 E2)."""
    tall = h >= 200
    face = "ui_card_controller" if tall else "ui_card_controller_compact"
    c.sprite(kit, face, x, y, w, h)
    fl, fb, fr, ft = kit.insets(face)
    inner = (x + fl, y + ft, w - fl - fr, h - ft - fb)
    content = (inner[0] + 4, inner[1] + 4, inner[2] - 8, inner[3] - 8)
    priced = price >= 0
    pw = max(price_w, 100) if priced else 0
    limit = content[0] + content[2] - (pw + 8 if priced else 0)
    block = min(84, content[2] * .34) if tall else min(content[3] - 8, 76)
    gem = 22 if tall else 18
    by = content[1] + (6 if tall else max(0, (content[3] - block - gem * .5) * .5))
    tint = P[RARITY_KEY[rarity]]
    c.sprite(kit, "ui_card_frame_rarity", content[0], by, block, block, tint=tint)
    rl, rb, rr, rt = kit.ring_insets("ui_card_frame_rarity")
    pic = min(block - rl - rr, block - rt - rb) - 2
    c.picture(anchor_art(art), content[0] + rl + 1, by + rt + 1, pic)
    if rarity:
        c.sprite(kit, "ui_gem_" + rarity, content[0] + block / 2 - gem / 2, by + block - gem / 2, gem, gem, tint=tint)
    face_fill = kit.centre(face)
    ink = label_on(face_fill, P, P["ink"])[0]
    tx, ty = content[0] + block + 10, content[1] + (4 if tall else 2)
    if category:
        lh = line11(min(15 * fs, 19.5) / fs if priced else 15, fs)
        c.text(tx, ty, limit - tx, lh, category, max(15 * fs, 19.5) if priced else 15 * fs,
               label_on(face_fill, P, lerp_hex(P["ink"], P["paper"], .28))[0], "lm")
        ty += lh
    disc = math.ceil(max(20, 14 * fs * 1.4)) if ordinal else 0
    name_x = tx + (disc + 6 if disc else 0)
    name_size = 20 * fs
    while name_size > 19.5 and text_width(title, name_size, True) > limit - name_x - 1:
        name_size -= 1
    name_h = math.ceil(name_size * 1.46) + 2
    if disc:
        c.sprite(kit, "ui_pip_dot", tx, ty + (name_h - disc) / 2, disc, disc, tint=P["ink"], preserve=True)
        c.text(tx, ty + (name_h - disc) / 2, disc, disc, str(ordinal), min(14, disc * .6 / fs) * fs, label_on(P["ink"], P)[0],
               "mm", bold=True)
    c.text(name_x, ty, limit - name_x, name_h, title, name_size, ink, "lm", bold=True)
    text_top = ty + name_h
    shown = False
    if tall:
        # The whole width under the block: fits at the 19.5 floor (fontSize 19.5, not scaled), then auto-sized up to 20 x
        # the text scale.
        below = max(text_top, by + block + gem * .5 + 4)
        room = content[1] + content[3] - below

        def need(sz):
            ls = wrap_cjk(text, sz, content[2])
            return ls, len(ls) * sz * 1.448 - (len(ls) - 1) * .14 * sz
        lines, h_need = need(19.5)
        if h_need <= room + .5:
            size = 19.5
            while size + .5 <= 20 * fs and need(size + .5)[1] <= room + .5:
                size += .5
            lines, _ = need(size)
            c.text(content[0], below, content[2], room, "\n".join(lines), size, ink, "lt", spacing=-14)
            shown = True
    else:
        room = content[1] + content[3] - text_top
        size = body11(18, fs) * fs
        width = limit - tx
        lines = wrap_cjk(text, size, width)
        need = len(lines) * size * 1.448 - (len(lines) - 1) * .16 * size
        if need <= room + .5:
            c.text(tx, text_top, width, room, "\n".join(lines), size, ink, "lt", spacing=-16)
            shown = True
    if notes is not None:
        notes.append(f"compact {w:.0f}x{h:.0f} {title}: short text {'shown' if shown else 'hidden'}")
    if priced:
        price_button(c, kit, P, content[0] + content[2] - pw, content[1] + max(0, (content[3] - 60) / 2), pw, 60, price)
    return shown


def play_layout(large: bool) -> dict:
    """GameApp.Play11 L11 (1920 x 1080 design units)."""
    info = 360 if large else 300
    return {"top": (16, 12, 1888, 72), "cards": (32, 96, 1448, 244), "stage": (1496, 96, 392, 244),
            "tray": (32, 352, 1448, 376), "dice": (52, 374, 1408, 276), "chips": (52, 660, 1408, 60),
            "resources": (1496, 352, 392, 376), "info": (32, 740, info, 202), "rail": (32 + info + 12, 740, 1448 - info - 12, 202),
            "settle": (1496, 740, 392, 202), "hint": (32, 958, 600 if large else 512, 88),
            "secondary": (644, 966, 240, 72) if large else (560, 966, 264, 72),
            "primary": (896, 962, 380, 80) if large else (840, 962, 400, 80), "end": (1496, 962, 392, 80)}


# Action-row states (GameApp.Play11 DrawActions11). W11's capture states: (a) dice selected, (b3) scoring, (c) boss stage
# start, (f) ten dice and seven cards; plus rolling, target reached and a pressure stage.
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


def full_palette() -> dict:
    return dict(PALETTE, **W20X_TOKENS)


def play_mock(kit: Kit, table: Image.Image, state: str = "f", large: bool = False, notes: list | None = None,
              palette: dict | None = None) -> tuple[Image.Image, dict]:
    """The play screen as GameApp.Play11 lays it out at 100 % or 150 % text, over table_scene. State a: three cards, five
    dice with two selected; f: seven cards, ten dice with two locked (W11's capture states). Returns the composite and
    measurements (hint glyph box, rectangles)."""
    P = dict(palette or full_palette())
    fs = 1.5 if large else 1.0
    L = play_layout(large)
    s = ACTION_STATES[state]
    boss, pressure = state == "boss", state == "pressure"
    stage_tint = P["boss"] if boss else P["pressure"] if pressure else P["panelLight"]
    n_cards = 7 if state == "f" else 3
    pool = (8 if state == "f" else 5)
    locked = 2 if state in ("f", "qualified", "pressure") else 0
    c = Canvas(1920, 1080)
    c.backdrop(table)
    notes = notes if notes is not None else []
    meas: dict = {}
    # ---------------------------------------------------------------- top bar
    tx_, ty_, tw_, th_ = L["top"]
    c.sprite(kit, "ui_topbar", tx_, ty_, tw_, th_, tint=P["panel"])
    bar_fill = drawn(kit.centre("ui_topbar"), P["panel"])
    fl, fb, fr, ft = kit.insets("ui_topbar")
    tag_w = 176 if large else 160
    c.sprite(kit, "ui_chip_formula", tx_ + 8, ty_ + 8, tag_w, 56, tint=stage_tint)
    tag_fill = drawn(kit.centre("ui_chip_formula"), stage_tint)
    colour, ratio, _ = label_on(tag_fill, P)
    notes.append(f"stage tag {tag_fill}: label {colour} {ratio:.2f}:1")
    c.text(tx_ + 8, ty_ + 10, tag_w, 48, "第 4 关", (24 if large else 28) * fs, colour, "mm", bold=True)
    dots_x = 8 + tag_w + 20
    cap_size = body11(17 if large else 20, fs) * fs
    glyph_top, glyph_bottom = .28 * cap_size, 1.28 * cap_size
    cap_top = min(36, 72 - fb - 2 - glyph_bottom)
    dots_limit = cap_top + glyph_top - 6
    dots_top = max(ft + 3, min(12, dots_limit - 20))
    dot = min(20, max(14, dots_limit - dots_top))
    for i in range(8):
        cur = i == 1
        size = dot if cur else dot * .7
        colour_d = stage_tint if cur else P["gold"] if i < 1 else P["panelLight"]
        c.sprite(kit, "ui_pip_dot", tx_ + dots_x + i * 24 + (20 - size) / 2, ty_ + dots_top + (dot - size) / 2, size, size,
                 tint=colour_d, preserve=True)
        if cur:
            c.sprite(kit, "ui_ring", tx_ + dots_x + i * 24 + (20 - dot) / 2 - 3, ty_ + dots_top - 3, dot + 6, dot + 6,
                     tint=P["paper"], mult=6)
    kind = "首领关" if boss else "高压关" if pressure else "普通关"
    cap_text = "第 2 章 · " + kind
    muted_on_bar = label_on(bar_fill, P, P["muted"])[0]
    box = c.text(tx_ + dots_x, ty_ + cap_top, 400, 32, cap_text, cap_size, muted_on_bar, "lt")
    meas["chapter caption"] = box
    cap_w = text_width(cap_text, cap_size, False)
    gold_w = 232 if large else 176
    gold_x = 1668 - 16 - gold_w
    frame_x = max(dots_x + 8 * 24 + 56, dots_x + cap_w + 20)
    frame_right = gold_x - tx_ - 16 - 284
    fw = min(640 if large else 560, frame_right - frame_x)
    fx_, fy_ = tx_ + frame_x, ty_ + 6
    c.sprite(kit, "ui_info_inset", fx_, fy_, fw, 60)
    well_fill = kit.centre("ui_info_inset")
    score_size = (28 if large else 34) * fs
    score_w = min(fw * .55, text_width("2,480", score_size) + 4)
    c.text(fx_ + 14, fy_, score_w, 48, "2,480", score_size, label_on(well_fill, P, P["textColor"])[0], "rm", bold=True)
    target_x = 14 + score_w * 1.14 + 8
    target_colour = P["pressure"] if pressure and contrast(P["pressure"], well_fill) >= 4.5 else label_on(well_fill, P, P["muted"])[0]
    c.text(fx_ + target_x, fy_ + 4, fw - target_x - 26, 44, "/ 4,000", (26 if large else 32) * fs, target_colour, "lm")
    c.sprite(kit, "ui_progress_track", fx_ + 14, fy_ + 48, fw - 28, 7, tint="07191C", mult=48 / 7)
    c.sprite(kit, "ui_progress_fill", fx_ + 14, fy_ + 48, max(7, (fw - 28) * .62), 7,
             tint=P["pressure"] if pressure else P["blue"], mult=48 / 7)
    c.sprite(kit, "ui_icon_coin", gold_x + 6, ty_ + 18, 36, 36, preserve=True)
    c.text(gold_x + 48, ty_ + 6, gold_w - 48 - (52 if large else 44), 60, "28", (30 if large else 34) * fs,
           label_on(bar_fill, P, P["gold"])[0], "rm", bold=True)
    c.text(gold_x + gold_w - (50 if large else 42), ty_ + 6, 60, 60, "金币", body11(17 if large else 20, fs) * fs, muted_on_bar, "lm")
    for x, label in ((1668, "手册"), (1788, "暂停")):
        button(c, kit, P, x, 18, 108, 60, P["paper"], label, 23 * fs, notes=notes if x == 1668 else None, name="top-bar button")
    # ---------------------------------------------------------------- cards
    cx0, cy0, cw0, ch0 = L["cards"]
    mini = n_cards > 7 or (large and n_cards > 5)
    if mini:
        cw, chh = (cw0 - 4 * 12) / 5, (ch0 - 10) / 2
        for i in range(n_cards):
            rarity, title, text, art = MOCK_CARDS[i % len(MOCK_CARDS)]
            compact_card(c, kit, P, cx0 + i % 5 * (cw + 12), cy0 + i // 5 * (chh + 10), cw, chh, rarity, title, text, art, fs,
                         ordinal=i + 1, notes=notes)
    else:
        slots = 5 if n_cards <= 5 else 7
        empty = max(0, slots - n_cards)
        card_w = min(280 if large else 216, (cw0 - empty * 68 - 12 * (n_cards - 1)) / n_cards)
        x = cx0
        for i in range(n_cards):
            rarity, title, text, art = MOCK_CARDS[i % len(MOCK_CARDS)]
            if large:
                compact_card(c, kit, P, x, cy0, card_w, ch0, rarity, title, text, art, fs, ordinal=i + 1, notes=notes)
            else:
                tall_card(c, kit, P, x, cy0, card_w, ch0, rarity, title, text, art, fs, ordinal=i + 1,
                          value="×1.5" if i == 1 else None)
            x += card_w + 12
        for _ in range(empty):
            c.sprite(kit, "ui_slot_inventory", x, cy0 + 8, 56, ch0 - 16)
            c.text(x, cy0 + 8, 56, ch0 - 16, "+", 30 * fs, P["muted"], "mm", font=BODY_FONT)
            x += 68
    # ---------------------------------------------------------------- stage card
    sx, sy, sw, sh = L["stage"]
    c.sprite(kit, "ui_stage_card", sx, sy, sw, sh, tint=P["paper"])
    card_fill = drawn(kit.centre("ui_stage_card"), P["paper"])
    if boss:
        c.sprite(kit, "ui_ring", sx + 4, sy + 4, sw - 16, sh - 18, tint=P["boss"])
        c.sprite(kit, "ui_ring", sx + 9, sy + 9, sw - 26, sh - 28, tint=P["boss"], alpha=.55)
    elif pressure:
        c.sprite(kit, "ui_ring", sx + 5, sy + 5, sw - 18, sh - 20, tint=P["pressure"])
    inner = sw - 36
    head_h = max(40, line11(body11(18 if large else 22, fs), fs))
    c.sprite(kit, "ui_chip_formula", sx + 14, sy + 14, inner, head_h, tint=stage_tint)
    head_fill = drawn(kit.centre("ui_chip_formula"), stage_tint)
    c.text(sx + 26, sy + 14, inner - 24, head_h, "首领减益 · 二档" if boss else kind, body11(18 if large else 22, fs) * fs,
           label_on(head_fill, P)[0], "lm")
    line_size = body11(14 if large else 20, fs)
    goal_size = 28 if large else 42
    ty = sy + 14 + head_h + 1
    paper_muted = label_on(card_fill, P, lerp_hex(P["ink"], P["paper"], .28))[0]
    c.text(sx + 14, ty, inner, line11(line_size, fs), "目标分数", line_size * fs, paper_muted, "lm")
    ty += line11(line_size, fs)
    goal_colour = P["pressure"] if pressure and contrast(P["pressure"], card_fill) >= 4.5 else label_on(card_fill, P, P["ink"])[0]
    c.text(sx + 14, ty, inner, line11(goal_size, fs), "6,000" if pressure else "4,000", goal_size * fs, goal_colour, "lm", bold=True)
    ty += line11(goal_size, fs)
    c.text(sx + 14, ty, inner, line11(line_size, fs), "过关奖励 +5 金币", line_size * fs, label_on(card_fill, P, P["ink"])[0], "lm")
    ty += line11(line_size, fs)
    note_size = body11(14 if large else 17, fs)
    c.text(sx + 14, ty, inner, line11(note_size, fs), "提交栏 5 格 · 每手掷 3 次", note_size * fs, paper_muted, "lm")
    # ---------------------------------------------------------------- tray
    rx, ry, rw, rh = L["tray"]
    rim = lerp_hex(P["panel"], P["panelLight"], .55)
    c.sprite(kit, "ui_dice_tray", rx, ry, rw, rh, tint=rim)
    if boss:
        c.sprite(kit, "ui_ring", rx + 6, ry + 6, rw - 12, rh - 12, tint=P["boss"])
        c.sprite(kit, "ui_ring", rx + 10, ry + 10, rw - 20, rh - 20, tint=P["boss"], alpha=.6)
    elif pressure:
        c.sprite(kit, "ui_ring", rx + 7, ry + 7, rw - 14, rh - 14, tint=P["pressure"], mult=1.6)
    else:
        c.sprite(kit, "ui_ring", rx + 7, ry + 7, rw - 14, rh - 14, tint=P["ghost"], alpha=.75, mult=1.6)
    caption = P.get("caption", P["panel"])
    tab_font = body11(18 if large else 20, fs)
    cl, cb, cr, ct = kit.insets("ui_chip_formula")
    tab_h = max(34, math.ceil(tab_font * fs + cb + ct + 4))
    tab_w = 120 if large else 100
    c.sprite(kit, "ui_chip_formula", rx + 24, ry - 14, tab_w, tab_h, tint=caption)
    tab_fill = drawn(kit.centre("ui_chip_formula"), caption)
    colour, ratio, _ = label_on(tab_fill, P, P["muted"])
    notes.append(f"dice-pool tab {tab_fill}: label {colour} {ratio:.2f}:1")
    centre_y = ct + max(1, tab_h - ct - cb) * .5
    c.text(rx + 24 + cl, ry - 14 + centre_y - tab_h / 2, tab_w - cl - cr, tab_h, "骰池", tab_font * fs, colour, "mm")
    dx, dy, dw, dh = L["dice"]
    cell_w = min(272, dw / pool)
    scale = min(1.75, (cell_w - 30) / 101)
    start = dx + (dw - pool * cell_w) * .5
    cy = dy + dh * .5 - 10
    pips_cycle = ((5, 2, 3), (4, 6, 1), (2, 3, 6), (6, 1, 4), (3, 5, 2), (1, 4, 5), (6, 2, 3), (4, 1, 6))
    colour_cycle = (("red", "blue", "blue"), ("blue", "red", "red"), ("red", "red", "blue"), ("blue", "blue", "red"))
    for j in range(pool):
        cxj = start + j * cell_w + cell_w * .5
        size = 101 * scale
        im = die_image(int(round(size)), pips_cycle[j % len(pips_cycle)], colour_cycle[j % 4], P)
        c.picture(im, cxj - size * .7, cy - size * .62 - size * .1, size * 1.45)
        if state == "a" and j in (1, 3):
            half_w, half_h = scale * 53, scale * 65
            c.sprite(kit, "ui_ring", cxj - half_w - 10, cy - half_h - 14, half_w * 2 + 20, half_h * 2 + 22, tint=P["gold"])
            tag_w2, tag_h2 = min(cell_w - 8, 120 if large else 96), 36 if large else 32
            ax, ay = cxj - tag_w2 * .5, cy + half_h + 10
            c.sprite(kit, "ui_chip_formula", ax, ay, tag_w2, tag_h2, tint=P["gold"])
            gfill = drawn(kit.centre("ui_chip_formula"), P["gold"])
            c.text(ax + cl, ay, tag_w2 - cl - cr, tag_h2, f"已选 {1 if j == 1 else 2}", body11(14 if large else 17, fs) * fs,
                   label_on(gfill, P)[0], "mm")
    # ---------------------------------------------------------------- formula row
    kx, ky, kw, kh = L["chips"]
    cap_tint = lerp_hex(P["background"], P["panel"], .5)
    cap_fill = drawn(kit.centre("ui_chip_formula"), cap_tint)
    if state == "a":
        c.sprite(kit, "ui_chip_formula", kx, ky, kw, kh, tint=cap_tint)
        c.text(kx + 10, ky, kw - 20, kh, "本关还没有得分", body11(17 if large else 20, fs) * fs, label_on(cap_fill, P, P["muted"])[0], "mm")
    else:
        caption_text = "预计"
        cap_w = min(420 if large else 330, max(120 if large else 96, text_width(caption_text, body11(17 if large else 20, fs) * fs, False) + 32))
        c.sprite(kit, "ui_chip_formula", kx, ky, cap_w, kh, tint=cap_tint)
        c.text(kx + 10, ky, cap_w - 20, kh, caption_text, body11(17 if large else 20, fs) * fs, label_on(cap_fill, P, P["muted"])[0], "mm")
        factors = (("基础分", "26", "blue"), ("一对", "×2", "red"), ("全蓝", "×3", "blue"), ("卡牌", "×1.5", "gold"))
        font, sep, pad = (22 if large else 26) * fs, (26 if large else 30), 30
        x = kx + cap_w + 12
        for name, value, key in factors:
            wv = text_width(value, font) + text_width(name, 19.5 * fs, False) + pad
            c.sprite(kit, "ui_chip_formula", x, ky, wv, kh, tint=P[key])
            ffill = drawn(kit.centre("ui_chip_formula"), P[key])
            ink = label_on(ffill, P)[0]
            nw = text_width(name, 19.5 * fs, False) + 2
            c.text(x + 10, ky, nw, kh, name, 19.5 * fs, ink, "lm")
            c.text(x + 10 + nw + 2, ky, wv - nw - 20, kh, value, font, ink, "rm", bold=True)
            x += wv
            c.text(x, ky, sep, kh, "×" if name != "卡牌" else "=", (20 if large else 24) * fs, P["onFelt"], "mm")
            x += sep
        total_w = min(520 if large else 460, max(140 if large else 130, text_width("234", (26 if large else 32) * fs) + 36))
        c.sprite(kit, "ui_chip_formula", x, ky, total_w, kh, tint=P["reward"])
        tfill = drawn(kit.centre("ui_chip_formula"), P["reward"])
        c.text(x + 14, ky, total_w - 28, kh, "234", (26 if large else 32) * fs, label_on(tfill, P)[0], "rm", bold=True)
    # ---------------------------------------------------------------- resources
    px_, py_, pw_, ph_ = L["resources"]
    c.sprite(kit, "ui_panel_hud", px_, py_, pw_, ph_)
    panel_fill = kit.centre("ui_panel_hud")
    for i, (label, value, key) in enumerate((("还能掷", "2", "teal"), ("还能提交", "3", "gold"))):
        wy = py_ + 14 + i * 96
        c.sprite(kit, "ui_info_inset", px_ + 14, wy, pw_ - 28, 86)
        if i == 0:
            c.sprite(kit, "ui_icon_roll", px_ + 28, wy + 25, 36, 36, preserve=True)
        else:
            c.fill(px_ + 30, wy + 31, 32, 24, P["paper"])
            c.fill(px_ + 36, wy + 37, 20, 2, P["ink"])
            c.fill(px_ + 36, wy + 44, 14, 2, P["ink"])
        c.text(px_ + 74, wy, 180, 86, label, (20 if large else 24) * fs, label_on(well_fill, P, P["muted"])[0], "lm")
        c.text(px_ + pw_ - 14 - 130, wy, 116, 86, value, (44 if large else 52) * fs, label_on(well_fill, P, P[key])[0], "rm", bold=True)
    iy = py_ + 210
    c.text(px_ + 14, iy, pw_ - 28, line11(body11(13 if large else 20, fs), fs), "道具 1 / 3", body11(13 if large else 20, fs) * fs,
           label_on(panel_fill, P, P["muted"])[0], "lm")
    for i in range(3):
        ix = px_ + 14 + i * (64 + 11)
        if i == 0:
            c.sprite(kit, "ui_card_item", ix, iy + 34, 64, 64)
            c.picture(anchor_art("I10"), ix + 6, iy + 39, 52)
        else:
            c.sprite(kit, "ui_ring", ix, iy + 34, 64, 64, tint=P["panelLight"])
    solid_button(c, kit, P, px_ + 14, iy + 34 + 64 + 12, pw_ - 28, 60, "查看骰子", 22 * fs, notes=None,
                 icon="ui_icon_dice_view")
    # ---------------------------------------------------------------- submit info, rail, slots
    ix_, iy_, iw_, ih_ = L["info"]
    c.sprite(kit, "ui_panel_hud", ix_, iy_, iw_, ih_)
    text_on_panel = label_on(panel_fill, P, P["textColor"])[0]
    c.text(ix_ + 16, iy_ + 10, iw_ - 32, 36, f"提交栏 {locked} / 5", (20 if large else 24) * fs, text_on_panel, "lm")
    hy = iy_ + 48
    if locked:
        rule = body11(15 if large else 18, fs) * fs
        c.text(ix_ + 16, hy, iw_ - 32, 30, "已锁定 · 本手不可移回骰池", rule, label_on(panel_fill, P, P["gold"])[0], "lt")
        hy += math.ceil(rule * 1.448) + 6
        c.text(ix_ + 16, hy, iw_ - 32, 80, "预计：一对\n全蓝", body11(17 if large else 21, fs) * fs, text_on_panel, "lt", spacing=-10)
    else:
        c.text(ix_ + 16, hy, iw_ - 32, 60, "锁定骰子后显示预计牌型", body11(15 if large else 18, fs) * fs,
               label_on(panel_fill, P, P["muted"])[0], "lt")
    rlx, rly, rlw, rlh = L["rail"]
    c.sprite(kit, "ui_dice_tray", rlx, rly, rlw, rlh, tint=rim)
    inner_w = rlw - 48
    slot_w = min(152, (inner_w - 12 * 4) / 5)
    slot_h = 170
    sx0 = rlx + 24 + (inner_w - (5 * slot_w + 4 * 12)) * .5
    sy0 = rly + 16
    for j in range(5):
        x = sx0 + j * (slot_w + 12)
        is_locked = j < locked
        ring = lerp_hex(P["gold"], P["paper"], .15) if is_locked else P["ghost"]
        c.sprite(kit, "ui_ring", x, sy0, slot_w, slot_h, tint=ring, alpha=1.0 if is_locked else .75)
        c.text(x + slot_w - 30, sy0 + 6, 24, 26, str(j + 1), (14 if large else 16) * fs, P["onFelt"], "mm")
        if is_locked:
            sc = min(1.1, min((slot_w - 38) / 101, (slot_h - 38) / 127))
            half = 50.5 * sc
            size = 101 * sc
            im = die_image(int(round(size)), ((6, 2, 3), (6, 4, 1))[j], (("red", "red", "blue"), ("blue", "red", "blue"))[j], P)
            c.picture(im, x + 2 + half - size * .7, sy0 + 38 + 63.5 * sc - size * .72, size * 1.45)
            c.sprite(kit, "ui_tray_front", x + 4, sy0 + slot_h - 6, slot_w - 8, 20)
            c.sprite(kit, "ui_tag_lock", x + slot_w - 29, sy0 + 36, 26, 28, preserve=True)
        else:
            c.text(x + 8, sy0 + 60, slot_w - 16, 48, "·  ·  ·", 20 * fs, P["onFelt"], "mm")
    # ---------------------------------------------------------------- settle
    stx, sty, stw, sth = L["settle"]
    c.sprite(kit, "ui_panel_hud", stx, sty, stw, sth)
    lab = body11(18 if large else 22, fs)
    gap = 30 if large else 44
    c.text(stx + 16, sty + 12, stw - 32, line11(lab, fs), "离目标还差", lab * fs, label_on(panel_fill, P, P["muted"])[0], "lm")
    c.text(stx + 16, sty + 12 + line11(lab, fs), stw - 32, line11(gap, fs), "1,520", gap * fs,
           P["pressure"] if pressure and contrast(P["pressure"], panel_fill) >= 4.5 else text_on_panel, "lm", bold=True)
    note_y = sty + 12 + line11(lab, fs) + line11(gap, fs) + 2
    note = balanced_lines("达标后结束：过关奖励 +5 金币，剩下的每次提交 +1", body11(15 if large else 18, fs) * fs, stw - 32)
    c.text(stx + 16, note_y, stw - 32, sty + sth - 8 - note_y, "\n".join(note), body11(15 if large else 18, fs) * fs,
           label_on(panel_fill, P, P["muted"])[0], "lt", spacing=-10)
    # ---------------------------------------------------------------- actions
    hx, hy2, hw, hh = L["hint"]
    c.sprite(kit, "ui_panel_hud", hx, hy2, hw, hh)
    size = body11(16 if large else 21, fs) * fs
    lines = balanced_lines(s["reason"], size, hw - 32)
    while size > body11(13 if large else 17, fs) * fs:
        f = font_at(FONT if False else BODY_FONT, size)
        if len(lines) * size * 1.448 - (len(lines) - 1) * .08 * size <= hh - 12 and max(f.getlength(l) for l in lines) <= hw - 32:
            break
        size -= .5
    meas["hint"] = c.text(hx + 16, hy2 + 6, hw - 32, hh - 12, "\n".join(lines), size, label_on(panel_fill, P, P["textColor"])[0],
                          "lm", spacing=-8, label="hint")
    meas["hint lines"] = len(lines)
    if s["secondary"]:
        x, y, w, h = L["secondary"]
        solid_button(c, kit, P, x, y, w, h, s["secondary"], (17 if large else 21) * fs, notes=notes, name="secondary")
    if s["primary"]:
        label, enabled = s["primary"]
        x, y, w, h = L["primary"]
        face = P["primary"] if enabled else P["panel"]
        button(c, kit, P, x, y, w, h, face, label, (22 if large else 28) * fs, None if enabled else P["textColor"],
               enabled=enabled, primary=True, notes=notes, name="primary")
    x, y, w, h = L["end"]
    if s["end"] == "skip":
        solid_button(c, kit, P, x, y, w, h, "跳过演出", 24 * fs)
    elif s["end"] == "qualified":
        button(c, kit, P, x, y, w, h, P["paper"], "结束本关 · 结算", (20 if large else 24) * fs, notes=notes, name="end (qualified)")
        c.sprite(kit, "ui_ring", x - 4, y - 4, w + 8, h + 4, tint=P["reward"])
    else:
        button(c, kit, P, x, y, w, h, P["panelLight"], "", 24, P["muted"], enabled=False)
        big = (20 if large else 24) * fs
        c.text(x + 8, y, w - 16, h - 7, "结束本关 · 结算\n", big, P["muted"], "mm", spacing=-12)
        c.text(x + 8, y + big * .72, w - 16, h - 7, "达标后可结束", big * .72, P["muted"], "mm")
        notes.append(f"end (disabled): face {drawn(kit.centre('ui_button_bevel'), P['panelLight'])}, Muted label "
                     f"{contrast(P['muted'], drawn(kit.centre('ui_button_bevel'), P['panelLight'])):.2f}:1")
    meas["hint panel"] = L["hint"]
    return c.image(), meas


# ---------------------------------------------------------------------------------------------- palette report
def palette_report(palette: dict | None = None, label_face: str | None = None, section_face: str | None = None) -> dict:
    """Contrast numbers quoted in the README and asserted by the tests (WCAG 2 sRGB relative luminance)."""
    p = dict(palette or PALETTE)
    w = dict(W20X_TOKENS, **{k: v for k, v in p.items() if k in W20X_TOKENS})
    chip = label_face or PALETTE["paper"]
    value_fill, warning_fill = drawn(chip, p.get("valueBadge", p["gold"])), drawn(chip, p["danger"])
    secondary = lerp_hex(p["background"], p["panel"], .55)
    rim = lerp_hex(p["panel"], p["panelLight"], .55)
    out = {
        "textColor/panel": contrast(p["textColor"], p["panel"]),
        "ink/paper": contrast(p["ink"], p["paper"]),
        "muted/panel": contrast(p["muted"], p["panel"]),
        "onFelt/felt": contrast(p["onFelt"], p["felt"]),
        "textColor/felt": contrast(p["textColor"], p["felt"]),
        "paper/felt": contrast(p["paper"], p["felt"]),
        "muted/felt": contrast(p["muted"], p["felt"]),
        f"{best_label(p['primary'], p)[0]}/primary (label pick)": best_label(p["primary"], p)[1],
        f"{best_label(p['panelLight'], p)[0]}/panelLight (label pick)": best_label(p["panelLight"], p)[1],
        "muted/panelLight (disabled label)": contrast(p["muted"], p["panelLight"]),
        f"{best_label(p['boss'], p)[0]}/boss (label pick)": best_label(p["boss"], p)[1],
        f"{best_label(p['pressure'], p)[0]}/pressure (label pick)": best_label(p["pressure"], p)[1],
        f"{best_label(p['gold'], p)[0]}/gold (label pick)": best_label(p["gold"], p)[1],
        f"{best_label(p['reward'], p)[0]}/reward (label pick)": best_label(p["reward"], p)[1],
        f"{best_label(secondary, p)[0]}/secondary {secondary} (label pick)": best_label(secondary, p)[1],
        "gold/panel": contrast(p["gold"], p["panel"]),
        "teal/panel": contrast(p["teal"], p["panel"]),
        "pressure/panel": contrast(p["pressure"], p["panel"]),
        "danger/paper": contrast(p["danger"], p["paper"]),
        "ghost/felt": contrast(p["ghost"], p["felt"]),
        f"tray rim {rim}/felt": contrast(rim, p["felt"]),
        "panel/felt": contrast(p["panel"], p["felt"]),
        "valueBadge/danger badge dE00 (drawn)": ciede2000(lab(value_fill), lab(warning_fill)),
        "valueBadge/danger badge contrast (drawn)": contrast(value_fill, warning_fill),
        f"{best_label(value_fill, p)[0]}/valueBadge {value_fill} (drawn)": best_label(value_fill, p)[1],
        f"{best_label(warning_fill, p)[0]}/danger badge {warning_fill} (drawn)": best_label(warning_fill, p)[1],
        "primary/pressure dE00": de00(p["primary"], p["pressure"]),
        "pressure/boss dE00": de00(p["pressure"], p["boss"]),
        "primary/blue dE00": de00(p["primary"], p["blue"]),
    }
    rar = ("rarityCommon", "rarityAdvanced", "rarityRare")
    for i, a in enumerate(rar):
        for b in rar[i + 1:]:
            out[f"{a}/{b}"] = contrast(p[a], p[b])
        out[f"primary/{a} dE"] = delta_e(p["primary"], p[a])
        out[f"primary/{a} dE00"] = de00(p["primary"], p[a])
        out[f"gilt/{a} dE00"] = de00(hexc(GOLD), p[a])
    out["gilt/ghost dE00"] = de00(hexc(GOLD), p["ghost"])
    out["pressure saturation"] = colorsys.rgb_to_hsv(*(v / 255 for v in rgb8(p["pressure"])))[1]
    out["pressure hue"] = colorsys.rgb_to_hsv(*(v / 255 for v in rgb8(p["pressure"])))[0] * 360
    cell = drawn(section_face or PALETTE["panel"], w["section"])
    out["muted/section cell"] = contrast(p["muted"], cell)
    out["textColor/section cell"] = contrast(p["textColor"], cell)
    caption = drawn("FFFFFF", w["caption"])
    out[f"{best_label(caption, p)[0]}/caption (label pick)"] = best_label(caption, p)[1]
    out[f"{best_label(drawn('FFFFFF', w['buttonStepPrimary']), p)[0]}/primary step"] = 0.0
    return out


# ---------------------------------------------------------------------------------------------- build
def theme_json() -> str:
    data = dict(THEME_META)
    data["palette"] = dict(PALETTE, **W20X_TOKENS)
    return json.dumps(data, ensure_ascii=False, indent=2) + "\n"


def save_png(path: Path, im: Image.Image):
    path.parent.mkdir(parents=True, exist_ok=True)
    # Explicit parameters, no timestamp/EXIF/ICC or machine-specific metadata.
    im.save(path, format="PNG", optimize=False, compress_level=9)


def build(output_root: Path = ROOT, only_implemented: bool = True) -> list[Path]:
    """Write the contract sprites drawn so far, the table, the page, the felt tile and theme.json."""
    output_root = Path(output_root).resolve()
    if not output_root.is_relative_to(ROOT):
        raise ValueError("Output root must stay inside the glass worktree")
    written = []

    def save(relative: Path, im: Image.Image):
        path = output_root / relative
        save_png(path, im)
        written.append(path)

    table = table_scene()
    check_felt_zones(table)  # Fail before writing any exported art.
    specs = contract_sprites()
    sprites: dict[str, Image.Image] = {}
    for spec in specs:
        if only_implemented and not implemented(spec):
            continue
        if spec["name"] in ("ui_card_controller_compact", "ui_card_shop") and "ui_card_controller" in sprites:
            im = sprites["ui_card_controller"].copy()
        else:
            im = render_sprite(spec)
        if im.mode != "RGBA" or list(im.size) != spec["size"]:
            raise ValueError("Contract mismatch: " + spec["name"])
        sprites[spec["name"]] = im
        save(THEME / "UI" / (spec["name"] + ".png"), im)
    save(THEME / "Table/table_scene.png", table)
    save(THEME / "Table/page_scene.png", page_scene())
    save(THEME / "Table/felt_tile.png", felt_tile())
    theme_path = output_root / THEME / "theme.json"
    theme_path.parent.mkdir(parents=True, exist_ok=True)
    theme_path.write_text(theme_json(), encoding="utf-8", newline="\n")
    written.append(theme_path)
    return written


if __name__ == "__main__":
    import sys
    import time
    started = time.time()
    paths = build()
    pngs = [p for p in paths if p.suffix == ".png"]
    print(f"GLASS_BUILT {len(pngs)} PNGs + theme.json / supersampling {SS}x / {time.time() - started:.1f}s")
    for key, value in palette_report().items():
        print(f"GLASS_PALETTE {key} {value:.2f}")
