"""W20: deterministic 彩绘玻璃 (glass) UI kit: gilt frames and leaded glass, 4x raster, one Lanczos downsample (the table and
the card back: a box filter).

Style authority: the five concept pages the user chose at checkpoint 3 (ArtSource/StyleKit/themes/glass/concepts) and the
palette samples of ArtSource/StyleKit/themes/glass/style_kit.json. Their texts, numbers and layouts are not a contract; the
game layout is (docs/specs/v11/PLAY_LAYOUT.md, SHOP_LAYOUT.md). The contract (tools/art_v11/theme_contract.json) owns pixel
sizes, nine-slice borders and pixels per unit. Repository fonts only; no image generation, no Unity files.

The shape language, taken from the concept pages (README.md, "Shapes and materials"; phase 1b after the coordinator's
review of phase 1, phase 2 after the review of 1b):
- the cards wear the concept's gilt frame with a jewel-glass strip: a lead edge, a gold moulding lit from the upper left,
  lead, a strip of backlit cobalt or ruby glass, lead, a gold hairline, lead; the corners are cut at 45 degrees and carry
  gilt four-point star finials (corner squares only);
- the panels (phase 2: every surface that holds text keeps FrameInsets11 within the default's on every side) wear a slim
  gilt frame, the jewel glass moved into a pane in each corner square under a gilt star;
- surfaces the game tints are pieces of glass in lead came (black came, a bevel one shade darker than the glass, a flat
  white face that takes the palette colour), the came thinner on the lit top and left edges; the buttons and chips
  (UiKit.Button, SolidButton11, formula chips, tags) have the concept 重掷's pointed ends; the top bar and the stage card
  keep cut corners with leaded corner lights;
- strips that slice only across end in chevron points (the progress bar, the locked-die ledge, the right end of the shop's
  section caption, whose left end comes out of the section panel's frame);
- the marks (badges, tags, coin, lock, cup, dice view, crown, gems, card back) are leaded glass and gilt: octagonal
  medallions in a gilt rim with faceted backlit glass, marble-white signs edged in lead; the marks the code tints are white
  shapes with a grey bevel at most;
- the play table is an all-over leaded stained-glass mosaic (a uniform texture, no layout shapes); the calm zones are
  flat slate framed by a gilt double line just outside them, and one rose window sits in the action row's always-free
  rectangle (THEME_SPEC 3.1). Table/page_scene.png, behind the non-play screens, is the mosaic everywhere.

Rules the art keeps (checked by tests/test_art_v11_glass.py):
- every nine-slice centre is one flat colour (or transparent) from the border line inward;
- stretched edge bands only change across the edge: every band is a function of the distance to the silhouette and of
  the edge's direction, so it is constant along each straight edge; ornament stays in the corner squares;
- sprites the code multiplies by a palette colour (NEUTRAL_NAMES) stay achromatic, with a pure white face;
- the protected table zones are exactly the felt colour.

    python tools/art_v11/glass/build_glass.py                  # the kit, preview.png and contact_sheet.png
    python tools/art_v11/glass/build_glass.py --report         # the palette's contrast numbers
    python tools/art_v11/glass/build_glass.py --review         # ArtSource/Art11/glass/review/ (review_glass.py)
    python tools/art_v11/glass/build_glass.py --card-review    # ArtSource/Art11/glass/card_template_review.png
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
    # Antique bronze: normal-stage tag, frame edges, empty item rings, disabled faces. Phase 2 deepened it a little (from
    # 6B5632, CIEDE2000 2.3) so a disabled SolidButton11, lerp(buttonSecondary, panelLight, .25), differs from the enabled
    # face by at least 5 CIEDE2000 (coordinator, lesson from the deco kit; README, "buttonSecondary").
    "panelLight": "64502F",
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
# Pending palette keys (coordinator 2026-11-18): code task W20Y adds `buttonSecondary`, the face tint of every
# SolidButton11 (secondary action, Skip, 查看骰子, 上一手明细, score-detail chips, shop buttons, price buttons). This
# worktree's ThemePalette.cs does not declare it yet, so theme.json must not carry it (the schema test would fail); the
# coordinator adds it at merge time. The offline mocks draw it, labelled "simulated buttonSecondary".
# AC8442 (amber glass): label ink 5.15 (LabelOn11, >= 4.95), on felt 4.24, on panel (dark panels, top bar) 4.66, on the
# background 5.48; on paper 2.62 - under 3:1, the chip's black lead outline gives the boundary on parchment (16:1). With
# this palette no face reaches 3:1 on felt and on paper and also takes a 4.95 label. Disabled (a quarter toward
# panelLight): 9A773D, 5.38 CIEDE2000 from the enabled face; LabelOn11(face, Muted) lightens it 2 steps to A48450 (3.33)
# and picks ink like the enabled label (README, "buttonSecondary").
PENDING_TOKENS = {
    "buttonSecondary": "AC8442",
}
THEME_META = {
    "id": "glass",
    "displayName": "彩绘玻璃",
    "description": ("铅条拼成的彩绘玻璃铺满桌面，静区是金线框起的石板；面板与卡牌是嵌一道宝石玻璃条的切角金框、四角金星，"
                    "被代码着色的按钮与筹码是铅条里的玻璃块、两端尖角，行动行常空处一扇玫瑰窗。依据："
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
    if not (np.isfinite(prgb).all() and np.isfinite(alpha).all()):
        raise ValueError("A sprite raster holds NaN or infinite values")
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


def glass_strip(colour):
    """A strip of backlit jewel glass between lead lines (phase 1b, the concept's gilt frames with a glass band): darker
    at both lead edges, glowing along its middle, the lit edges a little brighter. Only changes across the strip."""
    base = np.asarray(colour, np.float32)

    def paint(t, lit):
        t = np.asarray(t, np.float32)
        lit = np.zeros_like(t) if lit is None else lit
        glow = .58 + .42 * np.clip(np.sin(np.pi * t), 0, 1) ** .8   # float32 sin(pi) dips below 0
        rgb = base[None, None, :] * (glow * (.94 + .1 * lit))[..., None]
        hot = np.clip(np.exp(-((t - .42) / .16) ** 2) * (.25 + .2 * np.clip(lit, 0, 1)), 0, 1)
        return mix(np.minimum(rgb, 1.0), np.minimum(base * 1.45 + .1, 1.0), hot)
    return paint


def jewel_bands(glass, strip: float = 5.0):
    """The concept's gilt frame with a jewel-glass strip: lead edge, gold moulding, lead, the glass strip, lead, a gold
    hairline, lead (12 px plus the strip)."""
    return ((1.5, LEAD), (5.0, gilt_moulding), (1.5, LEAD), (strip, glass_strip(glass)), (1.5, LEAD),
            (1.5, gilt_hairline), (1.0, LEAD))


def star_layer(win: Raster, cx: float, cy: float, r: float, waist: float = .24, outline: float = 1.2):
    """A gilt four-point star finial: lead outline, eight facets lit from the upper left (alternately bright and deep)."""
    pts = star4(cx, cy, r, waist)
    d = sd_poly(win.X, win.Y, pts)
    if float(d.min()) > outline + 1:
        return
    win.over(LEAD, coverage(d - outline))
    ang = (np.arctan2(win.Y - cy, win.X - cx) + 2 * math.pi) % (2 * math.pi)
    facet = np.floor(ang / (math.pi / 4)).astype(int)
    # Facets whose outward side faces the upper left are lit.
    mid = (facet + .5) * (math.pi / 4)
    lit = np.cos(mid) * LIGHT[0] + np.sin(mid) * LIGHT[1]
    shade = np.where(facet % 2 == 0, .35, -.35) + .5 * lit
    rgb = mix(np.broadcast_to(GOLD, d.shape + (3,)), GOLD_HI, np.clip(shade, 0, 1))
    rgb = mix(rgb, GOLD_SH, np.clip(-shade, 0, 1))
    win.over(rgb, coverage(d))


def corner_stars(win: Raster, w: int, h: int, offset: float, r: float, top_only: bool = False):
    """Gilt four-point stars on the cut corners (corner squares only): centred `offset` px in from both edges."""
    corners = [(offset, offset), (w - offset, offset)]
    if not top_only:
        corners += [(w - offset, h - offset), (offset, h - offset)]
    for cx, cy in corners:
        star_layer(win, cx, cy, r)


def jewel_corners(win: Raster, d, w: int, h: int, cut: float, inset: float, depth: float, colour, lead: float = 2.0,
                  top_only: bool = False):
    """Phase 2: the panels' jewel glass, moved off the stretched edges into the corner squares. In each corner a pane of
    backlit glass lies along the face's cut, closed by a lead line parallel to the cut `depth` px further in; it glows
    along its middle and darkens at its lead (the concept's frame corners: a glass light under a gilt star)."""
    base = np.asarray(colour, np.float32)
    face = coverage(d + inset)
    c_face = cut + inset * math.sqrt(2)              # the face's cut, in x + y from the corner
    c_line = c_face + depth * math.sqrt(2)
    X, Y = win.X, win.Y
    corners = [(X - .5) + (Y - .5), (w - .5 - X) + (Y - .5)]
    if not top_only:
        corners += [(w - .5 - X) + (h - .5 - Y), (X - .5) + (h - .5 - Y)]
    for u in corners:
        t = np.clip((c_line - u) / (depth * math.sqrt(2)), 0, 1)   # 0 at the lead line, 1 at the face's cut
        glow = .62 + .5 * np.clip(np.sin(np.pi * t), 0, 1) ** .7
        rgb = np.minimum(base[None, None, :] * glow[..., None], 1.0)
        rgb = mix(rgb, np.minimum(base * 1.5 + .12, 1.0), np.clip(np.exp(-((t - .55) / .18) ** 2) * .35, 0, 1))
        win.over(rgb, coverage((u - c_line) / math.sqrt(2)) * face)
        win.over(LEAD, coverage(np.abs(u - c_line) / math.sqrt(2) - lead / 2) * face)


def gilt_tile(w: int, h: int, face, *, cut: float = GILT_CUT, bands=GILT, depth: int = 48, border: float = 40.0,
              open_bottom: bool = False, stars: tuple | None = None, jewel: tuple | None = None) -> Image.Image:
    """A nine-sliced gilt surface: the bands from the silhouette inward, then the flat face; optionally jewel-glass panes
    (colour, depth px) in the corners and gilt four-point stars (offset, radius) on them."""
    frame = sum(wd for wd, _ in bands)
    if corner_reach(cut, frame) > border - 3:
        raise ValueError("gilt corner reaches past its corner square")
    if stars and stars[0] + stars[1] > border - 3:
        raise ValueError("corner star reaches past its corner square")
    # The pane and its lead end, along each side, cut + (frame + depth) * sqrt 2 + lead - frame px from the corner.
    if jewel and cut + (frame + jewel[1]) * math.sqrt(2) + 2.0 - frame > border - 3:
        raise ValueError("jewel pane reaches past its corner square")
    pts = octagon((.5, .5, w - .5, (h + 64.0) if open_bottom else h - .5), (cut, cut, 0 if open_bottom else cut,
                                                                          0 if open_bottom else cut))

    def draw(win):
        d = sd_poly(win.X, win.Y, pts)
        paint_bands(win, d, bands, face, lit_of(d))
        if jewel:
            jewel_corners(win, d, w, h, cut, frame, jewel[1], jewel[0], top_only=open_bottom)
        if stars:
            corner_stars(win, w, h, stars[0], stars[1], top_only=open_bottom)
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


def pointed(w: float, h: float, P: float, Q: float, inset: float = .5):
    """The concept 重掷 outline: straight top and bottom, each end a point made of two slopes that fill the corner
    squares (P px across, Q px down), joined by the straight middle of the side that the slice stretches."""
    a, b, c, e = inset, w - inset, h - inset, inset
    return [(P, e), (w - P, e), (b, Q), (b, h - Q), (w - P, c), (P, c), (a, h - Q), (a, Q)]


def point_reach(P: float, Q: float, frame: float) -> float:
    """How far into its corner square the inner contour of a pointed end reaches (frame px inside the outline)."""
    alpha = math.atan2(P, Q)                       # the slope's angle from the vertical side
    down = Q + frame * math.tan(alpha / 2)         # where the inner contour leaves the slope on the side
    along = P + frame / math.tan((math.pi - (math.pi / 2 - alpha)) / 2)   # ... and on the top edge
    return max(down, along)


def convex_planes(pts):
    """Outward unit normals n and offsets c of a convex polygon's edges (screen coordinates, the vertices listed clockwise
    on screen): n . p - c is the signed distance to each edge's line, negative inside."""
    planes = []
    for (ax, ay), (bx, by) in zip(pts, pts[1:] + pts[:1]):
        dx, dy = bx - ax, by - ay
        length = math.hypot(dx, dy)
        nx, ny = dy / length, -dx / length
        planes.append((nx, ny, nx * ax + ny * ay))
    return planes


def inset_field(X, Y, planes, offsets) -> tuple[np.ndarray, np.ndarray]:
    """The convex polygon moved in by a different depth on each edge (offsets, px): its signed distance inside (max of the
    moved half-planes, mitred corners) and the index of the nearest edge (for that edge's light)."""
    fields = np.stack([(np.float32(nx) * X + np.float32(ny) * Y - np.float32(c) + np.float32(o))
                       for (nx, ny, c), o in zip(planes, offsets)])
    return fields.max(axis=0), fields.argmax(axis=0)


def came_convex(w: int, h: int, pts, came, bevel, *, lo: float, hi: float, depth: int = 48, border: float = 40.0,
                lights: float = 0.0, cut_edges=()) -> Image.Image:
    """A tinted glass piece in lead came whose came and bevel are thinner on the edges that face the light (top and left)
    than on the shaded ones (phase 2: every text-holding surface keeps FrameInsets11 within the default's on every side).
    came / bevel: px per edge, in the polygon's edge order. Black came, the bevel a shade below the glass (lighter on the
    lit edges, constant along each edge), the white face; optionally leaded corner lights `lights` px inside the face's
    cut on the edges listed in cut_edges."""
    planes = convex_planes(pts)
    came_o = list(came)
    face_o = [a + b for a, b in zip(came, bevel)]
    lit = [nx * float(LIGHT[0]) + ny * float(LIGHT[1]) for nx, ny, _ in planes]

    def draw(win):
        d = sd_poly(win.X, win.Y, pts)
        win.over(K, coverage(d))
        d_bev, k = inset_field(win.X, win.Y, planes, came_o)
        grey = lo + (hi - lo) * (.5 + .5 * np.asarray(lit, np.float32)[k])
        win.over(np.repeat(grey[..., None], 3, axis=-1), coverage(d_bev))
        d_face, _ = inset_field(win.X, win.Y, planes, face_o)
        win.over(WHITE, coverage(d_face))
        for e in cut_edges:
            nx, ny, c = planes[e]
            u = np.float32(nx) * win.X + np.float32(ny) * win.Y - np.float32(c) + np.float32(face_o[e])   # 0 on the face's cut
            inside = coverage(d_face)
            win.over(np.full(3, .9, np.float32), coverage(-(u + lights)) * inside)
            win.over(K, coverage(np.abs(u + lights) - 1.25) * inside)
    if max(face_o) + (lights + 3.0 if lights else 0.0) > border - 3:
        raise ValueError("came reaches past its border band")
    return band_render(w, h, depth, draw, WHITE)


def came_pointed(w: int, h: int, point: tuple, came, bevel, *, lo: float, hi: float) -> Image.Image:
    """The concept 重掷's pointed glass piece (two slopes per end, inside the corner squares) with lit-side-thin came."""
    if point_reach(point[0], point[1], max(a + b for a, b in zip(came, bevel))) > 40.0 - 2:
        raise ValueError("pointed end reaches past its corner square")
    return came_convex(w, h, pointed(w, h, *point), came, bevel, lo=lo, hi=hi)


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
CARD_CUT, PANEL_CUT = 12.0, 14.0
# Gilt star finials (offset from both edges, radius) in px: on the cut corners, inside the 40 px corner squares.
CARD_STARS, PANEL_STARS = (12.0, 12.0), (14.0, 14.0)
COBALT_GLASS, RUBY_GLASS = col("2A5BC8"), col("B8302A")
# Pointed ends (the concept's 重掷 and stage tag): P px across, Q px down per slope, inside the 40 px corner squares.
BUTTON_POINT, CHIP_POINT = (28.0, 34.0), (22.0, 30.0)
# Phase 2 (coordinator, lesson from the deco kit): every surface that holds text keeps its FrameInsets11 depth at or under
# the default sprite's on every side (README, "Text room"). The cards keep the phase 1b jewel frame (18 px against the
# default's 22-34). The panels have a slim gilt frame: lead, the gold moulding, and an inner came of night lead that
# lies within FrameInsets11's 48 of the night-glass face (so it is face, not frame, for the text); their jewel glass moves
# into the corner squares (a pane of cobalt or ruby glass under each gilt star). Lit-side-thin came on the tinted pieces:
# the came and bevel are thinner on the top and left edges (which face the light) than on the bottom and right.
NIGHT_LEAD = col("151526")      # inner came of the night-glass panels: 36 from the 1E2036 face (FrameInsets11 threshold 48)
SLIM_DARK = ((.75, LEAD), (3.75, gilt_moulding), (1.5, NIGHT_LEAD))         # 6 px; FrameInsets11 counts 5-6 (default 6-10)
SLIM_PAPER = ((1.0, LEAD), (4.5, gilt_moulding), (1.5, LEAD))               # 7 px on parchment (default 9, 18, 16, 9)
LABEL_BANDS = ((1.25, LEAD), (1.75, GOLD_MID), (1.0, LEAD))                 # 4 px: the value badge (default 5)
PANEL_JEWEL_DEPTH = 12.0        # px: the corner pane of jewel glass, measured in from the face's cut
PANEL_CORNER_STARS = (11.0, 11.0)   # offset, radius: a smaller gilt star on the outer corner, the pane showing beside it
# Per-edge came and bevel (px) in the pointed outline's edge order: top, upper-right slope, right, lower-right slope,
# bottom, lower-left slope, left, upper-left slope.
BUTTON_CAME = (3.0, 3.75, 4.5, 4.5, 4.5, 3.75, 3.0, 3.0)
BUTTON_BEVEL = (3.0, 4.25, 5.5, 5.5, 5.5, 4.25, 3.0, 3.0)                   # FrameInsets11 7, 11, 11, 7 (default 7, 12, 12, 7)
CHIP_CAME = (2.5, 3.0, 3.5, 3.5, 3.5, 3.0, 2.5, 2.5)
CHIP_BEVEL = (2.5, 3.25, 4.0, 4.25, 4.5, 3.5, 2.5, 2.5)                     # 6, 9, 8, 6 (default 6, 16, 10, 6)
# The top bar's octagon (octagon()'s edge order: top-left cut, top, top-right cut, right, bottom-right cut, bottom,
# bottom-left cut, left) with leaded corner lights on its four cuts (edges 0, 2, 4, 6).
BAR_CAME = (3.5, 3.5, 3.75, 4.0, 4.25, 4.5, 4.0, 3.5)
BAR_BEVEL = (3.5, 3.5, 4.25, 5.0, 5.25, 5.5, 4.5, 3.5)                      # 8, 11, 10, 8 (default 8, 30, 11, 8)
BAR_CUTS = (0, 2, 4, 6)
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
    # Phase 1b: the concept's gilt frame with a jewel-glass strip and gilt star finials on the corners. The cards keep it
    # (17 px, FrameInsets11 18 against the default's 22-34).
    if name in ("ui_card_controller", "ui_card_controller_compact", "ui_card_shop"):
        return gilt_tile(w, h, PAPER, cut=CARD_CUT, bands=jewel_bands(COBALT_GLASS), stars=CARD_STARS)
    if name == "ui_card_item":
        # Consumables: a ruby strip instead of cobalt.
        return gilt_tile(w, h, PAPER, cut=CARD_CUT, bands=jewel_bands(RUBY_GLASS), stars=CARD_STARS)
    # Phase 2: the panels' slim gilt frame, the jewel glass in the corner squares (README, "Text room").
    if name == "ui_paper_panel":
        return gilt_tile(w, h, PAPER, cut=PANEL_CUT, bands=SLIM_PAPER, stars=PANEL_CORNER_STARS,
                         jewel=(RUBY_GLASS, PANEL_JEWEL_DEPTH))
    if name in ("ui_panel_hud", "ui_section_panel"):
        return gilt_tile(w, h, PANEL, cut=PANEL_CUT, bands=SLIM_DARK, stars=PANEL_CORNER_STARS,
                         jewel=(COBALT_GLASS, PANEL_JEWEL_DEPTH))
    if name == "ui_tab_bottom":
        return gilt_tile(w, h, PANEL, cut=PANEL_CUT, bands=SLIM_DARK, open_bottom=True, stars=PANEL_CORNER_STARS,
                         jewel=(COBALT_GLASS, PANEL_JEWEL_DEPTH))
    if name == "ui_info_inset":
        return well(w, h)
    if name == "ui_slot_inventory":
        return niche(w, h)
    if name == "ui_chip_label":
        return gilt_tile(w, h, PAPER, cut=7.0, bands=LABEL_BANDS)
    if name == "ui_tray_front":
        return tray_front(w, h)
    # Achromatic, tinted by the game.
    if name == "ui_topbar":
        return came_convex(w, h, octagon((.5, .5, w - .5, h - .5), (BAR_CUT,) * 4), BAR_CAME, BAR_BEVEL, lo=.58, hi=.84,
                           lights=8.0, cut_edges=BAR_CUTS)
    if name == "ui_chip_formula":
        return came_pointed(w, h, CHIP_POINT, CHIP_CAME, CHIP_BEVEL, lo=.6, hi=.86)
    if name == "ui_stage_card":
        return came_tile(w, h, cut=STAGE_CUT, came=5.0, bevel=6.0, lo=.62, hi=.86, depth=56, border=48.0, lights=9.0)
    if name == "ui_button_bevel":
        return came_pointed(w, h, BUTTON_POINT, BUTTON_CAME, BUTTON_BEVEL, lo=.56, hi=.86)
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
    """A sunken well (UiKit.Inset: the score frame, the budget wells): a lead rim with a gold lower lip catching the light.
    The lead lies within FrameInsets11's 48 of the well's face, so only the lip on the bottom and right counts as frame
    (3 px, the default's 0, 3, 3, 0)."""
    pts = octagon((.5, .5, w - .5, h - .5), (8.0,) * 4)

    def lip(t, lit):
        lit = np.zeros_like(np.asarray(t, np.float32)) if lit is None else lit
        return mix(np.broadcast_to(LEAD, lit.shape + (3,)), GOLD_MID, np.clip(-lit, 0, 1) * .85)

    def draw(win):
        d = sd_poly(win.X, win.Y, pts)
        paint_bands(win, d, ((.75, LEAD), (1.5, lip), (1.25, LEAD)), WELL, lit_of(d))
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
# tall, f.w the section panel's top frame inset. Phase 2's slim frame measures 5 px (2.5 units): 63.5 units.
SECTION_PANEL_INSET_UNITS = 2.5
SECTION_HEADER_UNITS = 68.0 - SECTION_PANEL_INSET_UNITS - 2.0
CHEVRON = 11.0         # units: the depth of the banner's pointed end (the title starts 14 units in)
# The strip's top-left corner follows the section panel's cut corner: the panel's face is cut at x + y = (14 + 1 + 6 x
# sqrt 2) px = 11.7 units from the panel's corner, the strip starts 2.5 units in on both axes: 6.7 units; cut at 6.
SECTION_HEADER_CUT = 6.0


def section_header(w: int, h: int) -> Image.Image:
    """Shop section caption (border 40/0/72/0, sliced only across): a banner that comes out of the section panel's frame on
    the left (its face runs to the sprite's left edge, FrameInsets11 0 like the default's) and ends in a chevron point on
    the right; lead came and a bevel along the top and bottom, white so `caption` colours it. Drawn in display units (0.5
    per px across, SECTION_HEADER_UNITS / 128 down) because Unity scales only the height of a horizontally sliced strip."""
    r = Raster(w, h)
    sx, sy = .5, SECTION_HEADER_UNITS / h
    Xu, Yu = r.X * sx, r.Y * sy
    W, H = w * sx, SECTION_HEADER_UNITS
    top, bottom, mid = .5, H - .5, H / 2
    C = SECTION_HEADER_CUT
    body = [(C, top), (W - 8.0 - CHEVRON, top), (W - 8.0, mid), (W - 8.0 - CHEVRON, bottom), (-40.0, bottom),
            (-40.0, top + C + 40.0)]
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


# ---------------------------------------------------------------------------------------------- icons, badges, effects
# Phase 2. Authored (untinted) marks are leaded glass and gilt: a lead outline, a gilt bevelled rim lit from the upper
# left, backlit glass panes in lead, and marble-white symbols edged in lead (triptych.png's octagonal medallions, the
# lock cartouches, card.png's medallion over each card, roll.png's 查看骰子). Marks the code tints (NEUTRAL_NAMES) are white
# shapes with at most a grey bevel, so the tint they take is the colour drawn.
MARBLE_HI, MARBLE, MARBLE_SH = col("FCF7EE"), col("EEE5D4"), col("CDBFA9")
EMERALD_GLASS = col("2A8C5A")     # +10 base (the default badge's green)
AMETHYST_GLASS = col("7C50B4")    # x2 (the default's violet)
SKY_GLASS = col("2F7DCB")         # +1 roll (the default's sky blue, as cobalt-sky glass)
SLATE_GLASS = col("514C60")       # slotless (the default's dark badge, as lead-grey glass)
AMBER_GLASS = col("D39A33")       # +1 submit (triptych.png's gold octagon with a tick)


def regular_octagon(cx: float, cy: float, half: float):
    """A regular octagon with flat top, bottom and sides, `half` px from its centre to each flat (clockwise on screen)."""
    a = half * math.tan(math.pi / 8)
    return [(cx - a, cy - half), (cx + a, cy - half), (cx + half, cy - a), (cx + half, cy + a),
            (cx + a, cy + half), (cx - a, cy + half), (cx - half, cy + a), (cx - half, cy - a)]


def sd_segment(X, Y, ax, ay, bx, by) -> np.ndarray:
    """Distance to a line segment."""
    Xd, Yd = X.astype(np.float64), Y.astype(np.float64)
    ex, ey = bx - ax, by - ay
    t = np.clip(((Xd - ax) * ex + (Yd - ay) * ey) / max(ex * ex + ey * ey, 1e-9), 0, 1)
    return np.hypot(Xd - ax - ex * t, Yd - ay - ey * t).astype(np.float32)


def sd_polyline(X, Y, pts) -> np.ndarray:
    return np.minimum.reduce([sd_segment(X, Y, *a, *b) for a, b in zip(pts, pts[1:])])


def marble_fill(r: Raster, d, ink: float, lit_rgb=MARBLE_HI, mid_rgb=MARBLE, shade_rgb=MARBLE_SH, bevel: float = 0.0):
    """A marble-white shape (signed distance d) edged in lead `ink` px wide: lit from the upper left across the shape, a
    shaded bevel along its inner edge on the lower right."""
    r.over(LEAD, coverage(d - ink))
    span = max(float(r.X.max() - r.X.min()) + float(r.Y.max() - r.Y.min()), 1.0)
    u = np.clip(((r.X - r.X.min()) + (r.Y - r.Y.min())) / span, 0, 1)
    rgb = mix(np.broadcast_to(lit_rgb, d.shape + (3,)), mid_rgb, smoothstep(.2, .9, u))
    if bevel:
        lit = lit_of(d)
        rgb = mix(rgb, shade_rgb, (smoothstep(-bevel, 0, d) * np.clip(-lit, 0, 1)).astype(np.float32))
    r.over(rgb, coverage(d))


def faceted_field(r: Raster, cx: float, cy: float, base, mask, rmax: float, n: int = 8, lead_w: float = 3.0,
                  rot: float = 0.0, ring: float = 0.0):
    """Backlit glass cut into n wedge panes radiating from the centre (the triptych medallions' faceted field), in lead
    lines; alternate panes a shade deeper, each glowing towards its middle; an optional inner ring of lead at `ring` x
    rmax splits each wedge once more."""
    base = np.asarray(base, np.float32)
    X, Y = r.X, r.Y
    rad = np.hypot(X - cx, Y - cy)
    ang = (np.arctan2(Y - cy, X - cx) - rot) % (2 * math.pi)
    step = 2 * math.pi / n
    sector = np.floor(ang / step).astype(int)
    off = (ang - (sector + .5) * step) * rad                      # px from the pane's middle line, along the arc
    half = step * .5 * np.maximum(rad, 1e-3)
    t_arc = np.clip((half - np.abs(off)) / np.maximum(half, 1e-3), 0, 1)
    t_rad = np.clip(1 - rad / rmax, 0, 1)
    shade = np.where(sector % 2 == 0, 1.0, .8).astype(np.float32)
    lo_rgb = base[None, None, :] * (.55 * shade)[..., None]
    hi_rgb = np.minimum(base * 1.25 + .06, 1.0)[None, None, :] * shade[..., None]
    rgb = mix(lo_rgb, hi_rgb, smoothstep(0, 1, np.minimum(t_arc * 1.6, 1) * (.35 + .65 * t_rad)))
    r.over(np.clip(rgb, 0, 1), mask)
    lead = coverage((half - np.abs(off)) - lead_w / 2)
    if ring:
        lead = np.maximum(lead, coverage(np.abs(rad - ring * rmax) - lead_w / 2))
    r.over(LEAD, lead * mask)


def gilt_rim(r: Raster, pts, outer: float, rim: float, inner: float):
    """A polygon medallion's frame from its silhouette inward: lead `outer`, a gilt moulding `rim` wide whose facets are
    lit by their own edge's light (upper left bright, lower right deep), lead `inner`. Returns the silhouette's signed
    distance and the depth at which the field starts."""
    d = sd_poly(r.X, r.Y, pts)
    planes = convex_planes(pts)
    _, k = inset_field(r.X, r.Y, planes, [0.0] * len(planes))
    lit = np.asarray([nx * float(LIGHT[0]) + ny * float(LIGHT[1]) for nx, ny, _ in planes], np.float32)[k]
    r.over(LEAD, coverage(d))
    t = np.clip((-d - outer) / rim, 0, 1)
    r.over(gilt_moulding(t, lit), coverage(d + outer))
    r.over(LEAD, coverage(d + outer + rim))
    return d, outer + rim + inner


def octagon_medallion(r: Raster, cx: float, cy: float, half: float, field, outer: float, rim: float, inner: float,
                      lead_w: float, ring: float = 0.0):
    """triptych.png's medallion: an octagon in a gilt bevelled rim, its field faceted glass in lead."""
    pts = regular_octagon(cx, cy, half)
    d, depth = gilt_rim(r, pts, outer, rim, inner)
    mask = coverage(d + depth)
    faceted_field(r, cx, cy, field, mask, half - depth, n=8, lead_w=lead_w, rot=math.pi / 8, ring=ring)
    return d


def enchant_badge(name: str) -> Image.Image:
    """The five enchantment badges (256 px; drawn about 28-40 units on a card's picture): octagonal leaded-glass
    medallions in a gilt rim, the field in the default badges' meaning colour, the sign in marble white edged in lead."""
    S = 256
    r = Raster(S, S)
    c = S / 2
    field = {"base10": EMERALD_GLASS, "mult2": AMETHYST_GLASS, "roll1": SKY_GLASS, "slotless": SLATE_GLASS,
             "submit1": AMBER_GLASS}[name]
    octagon_medallion(r, c, c, 123.0, field, outer=7.0, rim=19.0, inner=5.0, lead_w=4.0, ring=.0)
    X, Y = r.X, r.Y
    ink = 10.0
    if name in ("base10", "mult2"):
        if name == "mult2":
            u, v = ((X - c) + (Y - c)) / math.sqrt(2), ((X - c) - (Y - c)) / math.sqrt(2)
        else:
            u, v = X - c, Y - c
        arm, half_w = 54.0, 17.0
        d = np.minimum(sd_box(u, v, (-arm, -half_w, arm, half_w)), sd_box(u, v, (-half_w, -arm, half_w, arm)))
        marble_fill(r, d, ink, bevel=6.0)
    elif name == "roll1":
        cup = [(c - 52, c - 34), (c + 52, c - 34), (c + 38, c + 58), (c - 38, c + 58)]
        rim_box = (c - 60, c - 52, c + 60, c - 30)
        d = np.minimum(sd_poly(X, Y, cup), sd_box(X, Y, rim_box))
        marble_fill(r, d, ink, bevel=6.0)
        r.over(LEAD, coverage(np.abs(Y - (c - 30)) - 2.5) * coverage(d + 1))
        pip = sd_circle(X, Y, c, c + 14, 15.0)
        r.over(LEAD, coverage(pip - 3.0))
        r.over(col(PALETTE["blue"]), coverage(pip))
    elif name == "slotless":
        L, t, a = 50.0, 15.0, 36.0          # bracket reach from the centre, thickness, arm length
        parts = []
        for sx in (-1, 1):
            for sy in (-1, 1):
                x0, y0 = c + sx * L, c + sy * L
                parts.append(sd_box(X, Y, (min(x0, x0 - sx * a), min(y0, y0 - sy * t), max(x0, x0 - sx * a),
                                           max(y0, y0 - sy * t))))
                parts.append(sd_box(X, Y, (min(x0, x0 - sx * t), min(y0, y0 - sy * a), max(x0, x0 - sx * t),
                                           max(y0, y0 - sy * a))))
        marble_fill(r, np.minimum.reduce(parts), ink, bevel=5.0)
    elif name == "submit1":
        d = sd_polyline(X, Y, [(c - 50, c + 2), (c - 16, c + 38), (c + 52, c - 40)]) - 17.0
        marble_fill(r, d, ink, bevel=6.0)
    return r.export()


def padlock_parts(X, Y, cx: float, top: float, w: float, h: float):
    """Signed distances of a padlock: the shackle (a U of thickness w * .15) and the body (a box with rounded corners)."""
    body_top = top + h * .42
    bw, bh = w, h - (body_top - top)
    th = w * .16
    rr = w * .5 - th * .5 - w * .1                       # the shackle's centre-line radius
    arc_cy = top + th * .5 + rr
    rad = np.hypot(X - cx, Y - arc_cy)
    upper = np.where(Y <= arc_cy, np.abs(rad - rr) - th * .5, np.inf)
    legs = np.minimum(sd_box(X, Y, (cx - rr - th * .5, arc_cy, cx - rr + th * .5, body_top + 4)),
                      sd_box(X, Y, (cx + rr - th * .5, arc_cy, cx + rr + th * .5, body_top + 4)))
    shackle = np.minimum(upper, legs).astype(np.float32)
    corner = w * .1
    body = sd_box(X, Y, (cx - bw * .5 + corner, body_top + corner, cx + bw * .5 - corner, body_top + bh - corner)) - corner
    return shackle, body.astype(np.float32), body_top, bh


def padlock(r: Raster, cx: float, top: float, w: float, h: float, ink: float, body_rgb=None):
    """A gilt padlock edged in lead with a lead keyhole."""
    shackle, body, body_top, bh = padlock_parts(r.X, r.Y, cx, top, w, h)
    r.over(LEAD, coverage(np.minimum(shackle, body) - ink))
    lit = lit_of(shackle)
    r.over(mix(np.broadcast_to(GOLD, shackle.shape + (3,)), GOLD_HI, np.clip(lit, 0, 1) * .6), coverage(shackle))
    r.over(LEAD, coverage(body - ink))
    t = np.clip((r.Y - body_top) / max(bh, 1), 0, 1)
    rgb = mix(np.broadcast_to(GOLD_HI if body_rgb is None else body_rgb, body.shape + (3,)), GOLD, smoothstep(.1, .7, t))
    rgb = mix(rgb, GOLD_SH, smoothstep(.65, 1.0, t) * .8)
    r.over(rgb, coverage(body))
    ky = body_top + bh * .42
    hole = np.minimum(sd_circle(r.X, r.Y, cx, ky, w * .1), sd_poly(r.X, r.Y, [(cx - w * .05, ky), (cx + w * .05, ky),
                                                                               (cx + w * .08, ky + bh * .34),
                                                                               (cx - w * .08, ky + bh * .34)]))
    r.over(LEAD, coverage(hole))


def lock_icon(size: int = 256) -> Image.Image:
    """ui_icon_lock (drawn 20 x 22 over a Background-filled frame when an item cannot be used): a gilt padlock in lead."""
    r = Raster(size, size)
    padlock(r, size / 2, size * .07, size * .7, size * .86, ink=size * .035)
    return r.export()


def lock_tag(size: int = 128) -> Image.Image:
    """ui_tag_lock (a locked die's tag, 26 x 28 on the slate): triptych.png's lock cartouche as an amber-glass octagon in
    a gilt rim, a lead-black padlock in it."""
    r = Raster(size, size)
    c = size / 2
    octagon_medallion(r, c, c, size * .48, AMBER_GLASS, outer=size * .035, rim=size * .085, inner=size * .025,
                      lead_w=size * .018)
    shackle, body, _, _ = padlock_parts(r.X, r.Y, c, size * .2, size * .42, size * .58)
    r.over(MARBLE_HI, coverage(np.minimum(shackle, body) - size * .03))      # a pale halo keeps the lock off the glass
    r.over(LEAD, coverage(np.minimum(shackle, body)))
    ky = size * .2 + size * .58 * .42 + (size * .58 * .58) * .4
    r.over(AMBER_GLASS, coverage(sd_circle(r.X, r.Y, c, ky, size * .045)))
    return r.export()


def clock_tag(size: int = 128) -> Image.Image:
    """ui_tag_clock (a timed die's tag): a cobalt-glass octagon in a gilt rim, a marble clock face with lead hands."""
    r = Raster(size, size)
    c = size / 2
    octagon_medallion(r, c, c, size * .48, COBALT_GLASS, outer=size * .035, rim=size * .085, inner=size * .025,
                      lead_w=size * .018)
    face = sd_circle(r.X, r.Y, c, c, size * .27)
    marble_fill(r, face, size * .035, bevel=size * .03)
    for a in range(12):
        ang = a * math.pi / 6
        x0, y0 = c + math.cos(ang) * size * .21, c + math.sin(ang) * size * .21
        r.over(LEAD, coverage(sd_circle(r.X, r.Y, x0, y0, size * (.016 if a % 3 else .024))))
    hands = np.minimum(sd_segment(r.X, r.Y, c, c, c, c - size * .18) - size * .022,
                       sd_segment(r.X, r.Y, c, c, c + size * .13, c + size * .06) - size * .022)
    r.over(LEAD, coverage(hands))
    r.over(col(PALETTE["red"]), coverage(sd_circle(r.X, r.Y, c, c, size * .03)))
    return r.export()


def coin(size: int = 96) -> Image.Image:
    """ui_icon_coin: a gilt coin edged in lead, a lead ring inside its rim and a deep-gold four-point star struck in the
    middle (shop.png's coins; the dark star and outline keep it apart from the amber buttonSecondary it sits on)."""
    r = Raster(size, size)
    c = size / 2
    R = size * .46
    rad = sd_circle(r.X, r.Y, c, c, R)
    r.over(LEAD, coverage(rad))
    disc = rad + size * .065
    lit = lit_of(rad)
    u = np.clip(((r.X - c) + (r.Y - c)) / (2 * R) + .5, 0, 1)
    rgb = mix(np.broadcast_to(GOLD_HI, rad.shape + (3,)), GOLD, smoothstep(.15, .6, u))
    rgb = mix(rgb, GOLD_SH, smoothstep(.55, 1.0, u) * .85)
    r.over(rgb, coverage(disc))
    r.over(LEAD, coverage(np.abs(rad + size * .2) - size * .025))
    inner = rad + size * .225
    r.over(mix(np.broadcast_to(GOLD, rad.shape + (3,)), GOLD_HI, np.clip(-lit, 0, 1) * .5), coverage(inner))
    star = sd_poly(r.X, r.Y, star4(c, c, size * .2, .3))
    r.over(GOLD_DEEP, coverage(star))
    return r.export()


def roll_cup(size: int = 256) -> Image.Image:
    """ui_icon_roll (36 units in the roll well, 28 elsewhere): a dice cup of cobalt glass in lead with gilt bands at its
    rim and foot, a marble die tumbling out over the rim."""
    r = Raster(size, size)
    s = size / 256
    X, Y = r.X, r.Y
    body = [(52 * s, 104 * s), (204 * s, 104 * s), (184 * s, 236 * s), (72 * s, 236 * s)]
    d = sd_poly(X, Y, body)
    r.over(LEAD, coverage(d - 9 * s))
    # Three leaded panes of cobalt glass, lit from the left (the pane follows the tapering body).
    down = np.clip((Y - 104 * s) / (132 * s), 0, 1)
    x0, x1 = 52 * s + 20 * s * down, 204 * s - 20 * s * down
    rel = np.clip((X - x0) / np.maximum(x1 - x0, 1e-3), 0, 1)
    pane = np.floor(rel * 3).clip(0, 2)
    tone = np.where(pane == 0, 1.15, np.where(pane == 1, .95, .72)).astype(np.float32)
    glass = np.clip(COBALT_GLASS[None, None, :] * tone[..., None] * (.8 + .35 * smoothstep(236 * s, 110 * s, Y))[..., None],
                    0, 1)
    r.over(glass, coverage(d))
    for k in (1, 2):
        xk0, xk1 = 52 * s + 152 * s * k / 3, 72 * s + 112 * s * k / 3
        r.over(LEAD, coverage(sd_segment(X, Y, xk0, 104 * s, xk1, 236 * s) - 3 * s) * coverage(d))
    for y0, y1 in ((104 * s, 126 * s), (214 * s, 236 * s)):
        band = np.maximum(d, np.maximum(y0 - Y, Y - y1)).astype(np.float32)
        r.over(LEAD, coverage(band - 1 * s))
        r.over(gilt_moulding(np.clip((Y - y0) / (y1 - y0), 0, 1), np.zeros_like(X)), coverage(band + 3 * s))
    rim = sd_box(X, Y, (40 * s, 88 * s, 216 * s, 108 * s)) - 6 * s
    r.over(LEAD, coverage(rim - 8 * s))
    r.over(gilt_moulding(np.clip((Y - 82 * s) / (32 * s), 0, 1), np.zeros_like(X)), coverage(rim))
    die_faces(r, 128 * s, 38 * s, 64 * s, pips=(1, 3, 2), pip_rgb=(col(PALETTE["red"]), LEAD, col(PALETTE["blue"])),
              ink=7 * s)
    return r.export()


def magnifier(r: Raster, cx: float, cy: float, R: float, ink: float):
    """A gilt magnifying glass: a pale glass lens in a gilt ring, a gilt handle, all edged in lead."""
    X, Y = r.X, r.Y
    ring = np.abs(sd_circle(X, Y, cx, cy, R)) - R * .16
    handle = sd_segment(X, Y, cx + R * .78, cy + R * .78, cx + R * 1.85, cy + R * 1.85) - R * .2
    outline = np.minimum(np.minimum(ring, handle), sd_circle(X, Y, cx, cy, R))
    r.over(LEAD, coverage(outline - ink))
    lens = sd_circle(X, Y, cx, cy, R - R * .14)
    u = np.clip(((X - cx) + (Y - cy)) / (2 * R) + .5, 0, 1)
    r.over(mix(np.broadcast_to(col("D8ECF6"), X.shape + (3,)), col("7FB3D9"), u), coverage(lens))
    r.over(col("FFFFFF"), coverage(sd_segment(X, Y, cx - R * .45, cy - R * .2, cx - R * .2, cy - R * .45) - R * .07))
    r.over(gilt_moulding(np.clip(.5 + .5 * np.sin(np.arctan2(Y - cy, X - cx)), 0, 1), lit_of(ring)), coverage(ring))
    r.over(mix(np.broadcast_to(GOLD, X.shape + (3,)), GOLD_SH, smoothstep(cx + R, cx + R * 1.9, X)), coverage(handle))


def dice_view(size: int = 128) -> Image.Image:
    """ui_icon_dice_view (40 units, drawn white on the secondary button; 128 as the new-die emblem): roll.png's 查看骰子, a
    marble die and a gilt magnifying glass."""
    r = Raster(size, size)
    s = size / 128
    die_faces(r, 54 * s, 52 * s, 76 * s, pips=(1, 2, 3), pip_rgb=(col(PALETTE["red"]), col(PALETTE["blue"]), LEAD), ink=5 * s)
    magnifier(r, 82 * s, 76 * s, 21 * s, 4.5 * s)
    return r.export()


def crown(w: int = 192, h: int = 128) -> Image.Image:
    """fx_score_crown (the high-score crown, 48 x 32 on the panels): a gilt crown edged in lead, five points tipped with
    gilt four-point stars, a band set with ruby and cobalt glass in lead."""
    r = Raster(w, h)
    X, Y = r.X, r.Y
    tips = ((18, 46), (56, 34), (96, 26), (136, 34), (174, 46))
    body = [(24, 108), tips[0], (38, 74), tips[1], (76, 68), tips[2], (116, 68), tips[3], (154, 74), tips[4], (168, 108)]
    d = sd_poly(X, Y, body)
    r.over(LEAD, coverage(d - 6))
    t = np.clip((Y - 30) / 78, 0, 1)
    rgb = mix(np.broadcast_to(GOLD_HI, X.shape + (3,)), GOLD, smoothstep(.0, .55, t))
    rgb = mix(rgb, GOLD_SH, smoothstep(.55, 1.0, t) * .7)
    r.over(rgb, coverage(d))
    band = sd_box(X, Y, (22, 84, 170, 112)) - 2
    r.over(LEAD, coverage(band - 4))
    r.over(mix(np.broadcast_to(GOLD, X.shape + (3,)), GOLD_SH, smoothstep(84, 112, Y)), coverage(band))
    for gx, glass in ((56, RUBY_GLASS), (96, COBALT_GLASS), (136, RUBY_GLASS)):
        jewel = sd_poly(X, Y, [(gx, 88), (gx + 11, 98), (gx, 108), (gx - 11, 98)])
        r.over(LEAD, coverage(jewel - 3))
        u = np.clip((Y - 88) / 20, 0, 1)
        r.over(mix(np.broadcast_to(np.minimum(np.asarray(glass) * 1.5 + .1, 1), X.shape + (3,)), glass, u),
               coverage(jewel))
    for i, (tx, ty) in enumerate(tips):
        st = sd_poly(X, Y, star4(tx, ty - 4, 16 if i == 2 else 13, .3))
        r.over(LEAD, coverage(st - 3))
        r.over(GOLD_HI, coverage(st))
    return r.export()


GEM_GREY = (.97, .82, .62)     # facet greys of the tinted gems (lit, mid, shaded); the table is pure white


def gem(name: str, size: int = 128) -> Image.Image:
    """ui_gem_common / advanced / rare (CARD_TEMPLATE 3 and 6: round, four-point diamond, six-point star; light faceted
    glass with an ink outline, tinted by the rarity colour). Cut-glass jewels: a lead outline, facets lit from the upper
    left in greys, a white table in the middle so the tint reads as its own colour."""
    r = Raster(size, size)
    c = size / 2
    X, Y = r.X, r.Y
    if name == "ui_gem_common":
        R = size * .44
        d = sd_circle(X, Y, c, c, R)
        n = 8
        table_d = sd_poly(X, Y, regular_octagon(c, c, R * .5))
    elif name == "ui_gem_advanced":
        R = size * .47
        pts = [(c, c - R), (c + R * .9, c), (c, c + R), (c - R * .9, c)]
        d = sd_poly(X, Y, pts)
        n = 4
        table_d = sd_poly(X, Y, [(c, c - R * .42), (c + R * .38, c), (c, c + R * .42), (c - R * .38, c)])
    else:
        R = size * .48
        pts = []
        for i in range(12):
            a = -math.pi / 2 + i * math.pi / 6
            rr = R if i % 2 == 0 else R * .56
            pts.append((c + math.cos(a) * rr, c + math.sin(a) * rr))
        d = sd_poly(X, Y, pts)
        n = 6
        table_d = sd_poly(X, Y, regular_hexagon(c, c, R * .3))
    r.over(K, coverage(d))
    ink = size * .05
    ang = (np.arctan2(Y - c, X - c) + 2 * math.pi) % (2 * math.pi)
    step = 2 * math.pi / n
    rot = -math.pi / 2 if n != 8 else -math.pi / 2 + math.pi / 8
    sector = np.floor(((ang - rot) % (2 * math.pi)) / step).astype(int)
    mid = rot + (sector + .5) * step
    lit = np.cos(mid) * float(LIGHT[0]) + np.sin(mid) * float(LIGHT[1])
    grey = np.where(lit > .35, GEM_GREY[0], np.where(lit > -.35, GEM_GREY[1], GEM_GREY[2])).astype(np.float32)
    grey = np.where(sector % 2 == 0, grey, grey * .96).astype(np.float32)
    r.over(np.repeat(grey[..., None], 3, axis=-1), coverage(d + ink))
    # Facet edges: thin grey lines along the sector boundaries, from the table out to the ink line.
    rad = np.hypot(X - c, Y - c)
    off = ((ang - rot) % step)
    seam = np.minimum(off, step - off) * rad
    facet_line = coverage(seam - size * .008) * coverage(d + ink) * (1 - coverage(table_d))
    r.over(np.full(3, GEM_GREY[2] * .85, np.float32), facet_line)
    r.over(np.full(3, GEM_GREY[2] * .85, np.float32), coverage(np.abs(table_d) - size * .012) * coverage(d + ink))
    r.over(WHITE, coverage(table_d + size * .012))
    return r.export()


def regular_hexagon(cx: float, cy: float, R: float):
    return [(cx + math.cos(-math.pi / 2 + i * math.pi / 3) * R, cy + math.sin(-math.pi / 2 + i * math.pi / 3) * R)
            for i in range(6)]


def neutral_icon(name: str, w: int, h: int) -> Image.Image:
    """Marks the code tints (the shop's nav icons, the next-stage arrow, dots, sparks, chips, arcs): white shapes, the
    leaded-glass details only as a grey bevel on the shaded side, so the tint is the colour drawn."""
    r = Raster(w, h)
    X, Y = r.X, r.Y
    c = w / 2
    bevel = 0.0
    if name == "ui_icon_arrow":
        s = w / 96
        d = sd_poly(X, Y, [(14 * s, 38 * s), (52 * s, 38 * s), (52 * s, 18 * s), (86 * s, 48 * s), (52 * s, 78 * s),
                           (52 * s, 58 * s), (14 * s, 58 * s)])
        bevel = 4 * s
    elif name == "ui_icon_book":
        s = w / 96
        left = [(8 * s, 22 * s), (44 * s, 28 * s), (44 * s, 80 * s), (8 * s, 74 * s)]
        right = [(52 * s, 28 * s), (88 * s, 22 * s), (88 * s, 74 * s), (52 * s, 80 * s)]
        d = np.minimum(sd_poly(X, Y, left), sd_poly(X, Y, right))
        r.over(WHITE, coverage(d))
        for k in range(3):
            y0 = (38 + k * 11) * s
            r.over(np.full(3, .62, np.float32), coverage(sd_segment(X, Y, 15 * s, y0, 37 * s, y0 + 3 * s) - 1.6 * s))
            r.over(np.full(3, .62, np.float32), coverage(sd_segment(X, Y, 59 * s, y0 + 3 * s, 81 * s, y0) - 1.6 * s))
        return r.export()
    elif name == "ui_icon_gear":
        s = w / 96
        rad = np.hypot(X - c, Y - c)
        ang = np.arctan2(Y - c, X - c)
        outer = 24 * s + 12 * s * smoothstep(.0, .5, np.cos(ang * 8))     # eight teeth
        d = np.maximum(rad - outer, 12 * s - rad).astype(np.float32)
        bevel = 3 * s
    elif name == "ui_icon_refresh":
        # A clockwise arrow round a gap at the upper right: a round tail at -10 degrees, the head at -60 degrees pointing
        # on into the gap (screen angles, y down).
        s = w / 256
        rad = np.hypot(X - c, Y - c)
        ang = np.arctan2(Y - c, X - c)
        R, t = 80 * s, 16 * s
        a_head, a_tail = math.radians(-62), math.radians(-12)
        gap = (ang > a_head) & (ang < a_tail)
        arc = np.where(gap, np.inf, np.abs(rad - R) - t)
        tail = sd_circle(X, Y, c + R * math.cos(a_tail), c + R * math.sin(a_tail), t)
        hx, hy = c + R * math.cos(a_head), c + R * math.sin(a_head)
        tx, ty = -math.sin(a_head), math.cos(a_head)                 # the direction of travel (clockwise on screen)
        nx, ny = math.cos(a_head), math.sin(a_head)
        head = [(hx + nx * (t + 30 * s), hy + ny * (t + 30 * s)), (hx + tx * 52 * s, hy + ty * 52 * s),
                (hx - nx * (t + 30 * s), hy - ny * (t + 30 * s))]
        d = np.minimum(np.minimum(arc, tail), sd_poly(X, Y, head)).astype(np.float32)
        bevel = 8 * s
    elif name == "ui_pip_dot":
        r.over(WHITE, coverage(sd_circle(X, Y, c, h / 2, w * .44)))
        return r.export()
    elif name == "fx_score_spark":
        d = sd_poly(X, Y, star4(c, h / 2, w * .46, .2))
        r.over(WHITE, coverage(d))
        ang = (np.arctan2(Y - h / 2, X - c) + 2 * math.pi) % (2 * math.pi)
        facet = np.floor(ang / (math.pi / 4)).astype(int)
        mid = (facet + .5) * (math.pi / 4)
        lit = np.cos(mid) * float(LIGHT[0]) + np.sin(mid) * float(LIGHT[1])
        grey = np.where(lit > 0, 1.0, .8).astype(np.float32)
        r.over(np.repeat(grey[..., None], 3, axis=-1), coverage(d + w * .02))
        return r.export()
    elif name == "fx_square_chip":
        s = w / 256
        pts = [(40 * s, 52 * s), (206 * s, 36 * s), (220 * s, 200 * s), (56 * s, 218 * s)]
        d = sd_poly(X, Y, pts)
        r.over(np.full(3, .72, np.float32), coverage(d))
        r.over(WHITE, coverage(d + 10 * s))
        r.over(np.full(3, .9, np.float32), coverage(sd_segment(X, Y, 70 * s, 180 * s, 180 * s, 70 * s) - 9 * s) * coverage(d + 10 * s))
        return r.export()
    elif name == "fx_arc_short":
        s = w / 256
        rad = np.hypot(X - 150 * s, Y - 190 * s)
        ang = np.arctan2(Y - 190 * s, X - 150 * s)
        a0, a1 = math.radians(-170), math.radians(-40)
        frac = np.clip((ang - a0) / (a1 - a0), 0, 1)
        width = 2.5 * s + 9 * s * frac
        arc = np.where((ang >= a0) & (ang <= a1), np.abs(rad - 118 * s) - width, np.inf).astype(np.float32)
        tip = (150 * s + 118 * s * math.cos(a1), 190 * s + 118 * s * math.sin(a1))
        d = np.minimum(arc, sd_poly(X, Y, star4(tip[0], tip[1], 30 * s, .26)))
        r.over(WHITE, coverage(d))
        return r.export()
    else:
        raise ValueError(name)
    lit = lit_of(d)
    r.over(WHITE, coverage(d))
    if bevel:
        shade = smoothstep(-bevel, -bevel * .35, d) * np.clip(-lit, 0, 1) * .25
        r.over(np.full(3, .7, np.float32), (coverage(d) * shade).astype(np.float32))
    return r.export()


def die_shadow(w: int = 256, h: int = 128) -> Image.Image:
    """ui_die_shadow: a soft lead-dark ellipse under a die."""
    r = Raster(w, h)
    q = np.hypot((r.X - w / 2) / (w * .42), (r.Y - h / 2) / (h * .3))
    a = np.clip(1 - smoothstep(.45, 1.0, q), 0, 1) * .72
    r.over(np.broadcast_to(col("0E0C14"), a.shape + (3,)), a.astype(np.float32))
    return r.export()


CARD_BACK_W, CARD_BACK_H = 768, 1024


def card_back_layer(r: Raster):
    """ui_card_back: the card's jewel frame at back-of-card scale (lead, gilt moulding, lead, cobalt strip, lead, gilt
    hairline, lead; 45-degree cuts and gilt corner stars), the field a dim leaded mosaic of cobalt and ruby, a rose window
    in the middle with a marble die (pips red and blue) and gilt stars above and below it. Tile-independent (analytic)."""
    w, h = CARD_BACK_W, CARD_BACK_H
    pts = octagon((2.0, 2.0, w - 2.0, h - 2.0), (40.0,) * 4)
    d = sd_poly(r.X, r.Y, pts)
    frame = sum(b[0] for b in CARD_BACK_BANDS)
    paint_bands(r, d, CARD_BACK_BANDS, None, lit_of(d))
    inside = coverage(d + frame)
    if inside.any():
        sub = Raster(r.w, r.h, _views=(r.X, r.Y, np.zeros_like(r.rgb), np.zeros_like(r.a)))
        mosaic_layer(sub)
        sub.rgb[...] *= np.float32(.85)
        r.over_premult(sub.rgb * inside[..., None], sub.a * inside)
    for cx, cy in ((40.0, 40.0), (w - 40.0, 40.0), (w - 40.0, h - 40.0), (40.0, h - 40.0)):
        star_layer(r, cx, cy, 34.0)
    cx, cy = w / 2, h / 2
    if float(r.Y.min()) < cy - 240 and float(r.Y.max()) > cy + 240:      # only the middle strip holds the window whole
        rose_window(r, cx, cy, 230.0, petals=12)
        die_faces(r, cx, cy - 8, 196.0, pips=(5, 2, 3), pip_rgb=(col(PALETTE["red"]), col(PALETTE["blue"]),
                                                                 col(PALETTE["red"])), ink=8.0)
    for sy in (cy - 330, cy + 330):
        if float(r.Y.max()) > sy - 70 and float(r.Y.min()) < sy + 70:
            star_layer(r, cx, sy, 56.0, waist=.24, outline=3.0)


CARD_BACK_BANDS = ((6.0, LEAD), (18.0, gilt_moulding), (5.0, LEAD), (16.0, glass_strip(COBALT_GLASS)), (5.0, LEAD),
                   (5.0, gilt_hairline), (4.0, LEAD))


def card_back() -> Image.Image:
    """Rendered in three strips and box-filtered (no ringing, no seams), alpha from the cut silhouette. The strip seams
    (y 232 and 792) pass only through analytic shapes (the frame, the mosaic, the stars); the rose window and the die,
    whose outlines use a distance transform, lie wholly in the middle strip."""
    w, h = CARD_BACK_W, CARD_BACK_H
    out = np.zeros((h, w, 4), np.float32)
    for y, y_end in ((0, 232), (232, 792), (792, h)):
        hh = y_end - y
        r = Raster(w, hh, 0.0, float(y))
        card_back_layer(r)
        a = r.a.reshape(hh, SS, w, SS).mean(axis=(1, 3))
        rgb = r.rgb.reshape(hh, SS, w, SS, 3).mean(axis=(1, 3))
        out[y:y + hh, :, :3] = np.where(a[..., None] > 0, rgb / np.maximum(a[..., None], 1e-6), 0)
        out[y:y + hh, :, 3] = a
    px = np.zeros((h, w, 4), np.uint8)
    px[..., 3] = np.rint(np.clip(out[..., 3], 0, 1) * 255)
    px[..., :3] = np.where(px[..., 3:] > 0, np.clip(np.rint(out[..., :3] * 255), 0, 255), 0)
    return Image.fromarray(px, "RGBA")


# ---------------------------------------------------------------------------------------------- sprite list
# W14's shop stopped drawing ui_price_chip, ui_pedestal and ui_frame_enchant; the contract regenerated on 2026-11-18 lists
# them as unreferenced, and this kit does not draw them (STATE, W14 decision 4: "黑金与彩绘玻璃按 50 张做").
def contract_sprites() -> list[dict]:
    return [s for s in json.loads(CONTRACT.read_text(encoding="utf-8"))["sprites"] if s["referenced"]]


NEUTRAL_ICONS = ("ui_icon_arrow", "ui_icon_book", "ui_icon_gear", "ui_icon_refresh", "ui_pip_dot", "fx_score_spark",
                 "fx_square_chip", "fx_arc_short")


def render_sprite(spec: dict) -> Image.Image:
    name = spec["name"]
    w, h = spec["size"]
    if any(spec["border"]):
        return surface(spec)
    if name == "ui_wordmark_dicecraft":
        return wordmark()
    if name.startswith("badge_enchant_"):
        return enchant_badge(name[len("badge_enchant_"):])
    if name in ("ui_gem_common", "ui_gem_advanced", "ui_gem_rare"):
        return gem(name, w)
    if name in NEUTRAL_ICONS:
        return neutral_icon(name, w, h)
    renderers = {"ui_icon_coin": coin, "ui_icon_lock": lock_icon, "ui_tag_lock": lock_tag, "ui_tag_clock": clock_tag,
                 "ui_icon_roll": roll_cup, "ui_icon_dice_view": dice_view}
    if name in renderers:
        return renderers[name](w)
    if name == "fx_score_crown":
        return crown(w, h)
    if name == "ui_die_shadow":
        return die_shadow(w, h)
    if name == "ui_card_back":
        return card_back()
    raise NotImplementedError(name)


# ---------------------------------------------------------------------------------------------- table
TABLE_W, TABLE_H = 1920, 1080
FELT = col(PALETTE["felt"])
# Phase 1b (coordinator 2026-11-18): outside the calm zones the play table is an all-over leaded stained-glass mosaic, a
# uniform texture with no layout shapes and no large motif, so panels and cards covering parts of it still leave a
# coherent texture (as comic's Ben-Day page did). The calm zones stay flat slate. Where two zones touch or nearly touch
# they form one slate area - the dice area and the formula row with the 10-unit gap between them; the info and rail zones
# of the submit row, which overlap - and each area is framed by a gilt double line drawn just outside it, never inside.
# Table/page_scene.png is the same mosaic everywhere.
SLATE_AREAS = ((52.0, 374.0, 1460.0, 720.0), (32.0, 740.0, 1480.0, 942.0))   # x0, y0, x1, y1
CALM_FRAME = 7.5            # px outward: lead 1, gilt 3, lead 1, gilt hairline 1.5, lead 1
MOSAIC_CELL = 40.0          # px: the jittered grid of the mosaic's seeds (panes about 25-55 px across)
MOSAIC_LEAD = 2.6           # px: lead lines (2-3 px at 1080p)
MOSAIC_DIM = .6             # the mosaic's light: below the gilt frames and the parchment, so they lead the eye
# (share, colour): led by cobalt and ruby like the concept's windows, gold accents, a little green and violet.
MOSAIC_GLASS = ((.44, col("1F4BA8")), (.31, col("A3261E")), (.15, col("C8902C")), (.05, col("23784A")),
                (.05, col("6A479F")))
MOSAIC_LEAD_RGB = col("0F0E12")    # the style kit's lead
# THEME_SPEC 3.1: large decoration only where no play state puts interface, never cut by a straight line, never inside a
# button's or panel's bounding rectangle. The rose window sits in the action row's rectangle free in every state: right
# of the main button (with its focus outline it ends at x 1279 at 150 % text), left of the end-stage slot (its qualified
# ring starts at 1492), below the submit row (942): x 1280-1488, y 942-1080 (README, action-row table).
ACTION_FREE = (1280.0, 942.0, 1488.0, 1080.0)
ROSE = (1384.0, 1012.0, 54.0)          # centre and radius: x 1330-1438, y 958-1066
BLEED = 3.0   # px kept clear around the rose window
# Bounding rectangles (x, y, w, h) of the play screen's buttons and panels in any state or text size (GameApp.Play11 L11
# at 100 % and 150 %; UiKit.Button's step and its focus outline 3 units out, the qualified ring), which the rose window
# must stay out of. The mosaic is a texture under all of them (coordinator, phase 1b).
PLAY_RECTS = {
    "top bar": (16, 12, 1888, 72), "card row": (29, 93, 1454, 250), "stage card": (1496, 96, 392, 244),
    "tray": (32, 338, 1448, 390), "resources": (1496, 352, 392, 376), "submit row": (32, 740, 1448, 202),
    "settle": (1496, 740, 392, 202), "hint": (32, 958, 600, 88), "secondary": (557, 963, 330, 78),
    "primary": (837, 959, 442, 88), "end": (1492, 958, 400, 89),
}


def _hash2(ix: np.ndarray, iy: np.ndarray, salt: int) -> np.ndarray:
    """A deterministic 32-bit hash of integer grid coordinates."""
    h = (ix.astype(np.int64) * 374761393 + iy.astype(np.int64) * 668265263 + salt * 2246822519 + SEED) & 0xFFFFFFFF
    h = ((h ^ (h >> 13)) * 1274126177) & 0xFFFFFFFF
    return (h ^ (h >> 16)) & 0xFFFFFFFF


def _unit(h: np.ndarray, shift: int) -> np.ndarray:
    return ((h >> shift) & 0xFFFF).astype(np.float64) / 65536.0


def mosaic_layer(r: Raster):
    """The leaded mosaic: a Voronoi tiling of a jittered seed grid (analytic, so render tiles agree), each pane a piece of
    backlit glass (a little grisaille at the lead, a slight streak across it), black lead between the panes."""
    X, Y = r.X.astype(np.float64), r.Y.astype(np.float64)
    G = MOSAIC_CELL
    gx, gy = np.floor(X / G).astype(np.int64), np.floor(Y / G).astype(np.int64)
    d1 = np.full(X.shape, np.inf)
    d2 = np.full(X.shape, np.inf)
    s1x, s1y, s2x, s2y = (np.zeros(X.shape) for _ in range(4))
    i1x, i1y = np.zeros(X.shape, np.int64), np.zeros(X.shape, np.int64)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            cx, cy = gx + dx, gy + dy
            h = _hash2(cx, cy, 1)
            sx, sy = (cx + .06 + .88 * _unit(h, 0)) * G, (cy + .06 + .88 * _unit(h, 16)) * G
            d = (X - sx) ** 2 + (Y - sy) ** 2
            nearer = d < d1
            second = ~nearer & (d < d2)
            d2 = np.where(nearer, d1, np.where(second, d, d2))
            s2x = np.where(nearer, s1x, np.where(second, sx, s2x))
            s2y = np.where(nearer, s1y, np.where(second, sy, s2y))
            d1 = np.where(nearer, d, d1)
            s1x, s1y = np.where(nearer, sx, s1x), np.where(nearer, sy, s1y)
            i1x, i1y = np.where(nearer, cx, i1x), np.where(nearer, cy, i1y)
    edge = (d2 - d1) / (2 * np.maximum(np.hypot(s2x - s1x, s2y - s1y), 1e-6))   # px to the lead line
    h = _hash2(i1x, i1y, 7)
    u, v, w = _unit(h, 0), _unit(h, 16), _unit(_hash2(i1x, i1y, 11), 0)
    base = np.zeros(X.shape + (3,), np.float32)
    lo = 0.0
    for share, colour in MOSAIC_GLASS:
        pick = (u >= lo) & (u < lo + share + (1e-9 if share == MOSAIC_GLASS[-1][0] else 0))
        base[pick] = colour
        lo += share
    base[u >= lo] = MOSAIC_GLASS[-1][1]
    bright = (.8 + .28 * v).astype(np.float32)
    t = np.clip(edge / (G * .3), 0, 1).astype(np.float32)
    glow = (.6 + .4 * smoothstep(0, 1, t)).astype(np.float32)
    ang = w * 2 * math.pi
    streak = 1 + .07 * ((X - s1x) * np.cos(ang) + (Y - s1y) * np.sin(ang)) / G
    rgb = base * (bright * glow * streak.astype(np.float32) * MOSAIC_DIM)[..., None]
    r.over(np.clip(rgb, 0, 1), np.ones(X.shape, np.float32))
    r.over(MOSAIC_LEAD_RGB, coverage(edge - MOSAIC_LEAD / 2))


def calm_layer(r: Raster):
    """The slate areas, exactly the felt colour, each with a gilt double line just outside it (square-cornered)."""
    X, Y = r.X, r.Y
    for x0, y0, x1, y1 in SLATE_AREAS:
        u = np.maximum(np.maximum(np.float32(x0) - X, X - np.float32(x1)), np.maximum(np.float32(y0) - Y, Y - np.float32(y1)))
        if float(u.min()) > CALM_FRAME + 1:
            continue
        gy, gx = np.gradient(u, np.float32(1.0 / SS))
        lit = ((gx * LIGHT[0] + gy * LIGHT[1]) / np.maximum(np.hypot(gx, gy), 1e-6)).astype(np.float32)
        ring = lambda a, b: np.clip(coverage(u - b) - coverage(u - a), 0, 1)
        r.over(LEAD, ring(0.0, CALM_FRAME))
        r.over(gilt_moulding(np.clip((u - 1.0) / 3.0, 0, 1), lit), ring(1.0, 4.0))
        r.over(gilt_hairline(np.clip((u - 5.0) / 1.5, 0, 1), lit), ring(5.0, 6.5))
        r.over(FELT, coverage(u))


def table_layer(r: Raster, play: bool):
    """The mosaic everywhere; on the play table the slate areas with their gilt lines and the rose window."""
    mosaic_layer(r)
    if not play:
        return
    calm_layer(r)
    cx, cy, R = ROSE
    if float(r.X.max()) > cx - R - 6 and float(r.X.min()) < cx + R + 6 and float(r.Y.max()) > cy - R - 6:
        rose_window(r, cx, cy, R)


def _check_decoration_geometry():
    cx, cy, R = ROSE
    x0, y0, x1, y1 = ACTION_FREE
    if cx - R < x0 + 4 or cx + R > x1 - 4 or cy - R < y0 + 4 or cy + R > y1 - 4:
        raise ValueError("The rose window must sit wholly inside the action row's free rectangle")
    for name, (x, y, w, h) in PLAY_RECTS.items():
        if cx - R - BLEED < x + w and cx + R + BLEED > x and cy - R - BLEED < y + h and cy + R + BLEED > y:
            raise ValueError(f"The rose window is inside the bounding rectangle of {name}")
    for x0a, y0a, x1a, y1a in SLATE_AREAS:
        for name, (x, y, w, h) in FELT_ZONES.items():
            inside = x0a <= x and y0a <= y and x + w <= x1a and y + h <= y1a
            apart = x + w <= x0a - CALM_FRAME or x >= x1a + CALM_FRAME or y + h <= y0a - CALM_FRAME or y >= y1a + CALM_FRAME
            if not (inside or apart):
                raise ValueError(f"A gilt calm-zone line would cross the calm zone {name}")
    for name, (x, y, w, h) in FELT_ZONES.items():
        if not any(x0a <= x and y0a <= y and x + w <= x1a and y + h <= y1a for x0a, y0a, x1a, y1a in SLATE_AREAS):
            raise ValueError(f"Calm zone {name} lies outside every slate area")


_check_decoration_geometry()
TABLE_TILE = 240


def downsample_box(prgb: np.ndarray, alpha: np.ndarray, w: int, h: int) -> np.ndarray:
    """The table's filter: the mean of each SS x SS block (no ringing, so an edge drawn on a zone's boundary leaves every
    pixel of the zone exact)."""
    if not (np.isfinite(prgb).all() and np.isfinite(alpha).all()):
        raise ValueError("A table raster holds NaN or infinite values")
    a = alpha.reshape(h, SS, w, SS).mean(axis=(1, 3))
    rgb = prgb.reshape(h, SS, w, SS, 3).mean(axis=(1, 3))
    return np.where(a[..., None] > 0, rgb / np.maximum(a[..., None], 1e-6), 0) * 255.0


def render_tiles(layer) -> Image.Image:
    """A full 1920 x 1080 image rendered in 240 px tiles (the box filter needs no margin) and stitched."""
    canvas = np.zeros((TABLE_H, TABLE_W, 3), np.float32)
    for y in range(0, TABLE_H, TABLE_TILE):
        for x in range(0, TABLE_W, TABLE_TILE):
            w, h = min(TABLE_TILE, TABLE_W - x), min(TABLE_TILE, TABLE_H - y)
            r = Raster(w, h, float(x), float(y))
            layer(r)
            canvas[y:y + h, x:x + w] = downsample_box(r.rgb, r.a, w, h)
    return Image.fromarray(np.clip(np.rint(canvas), 0, 255).astype(np.uint8), "RGB")


def table_scene() -> Image.Image:
    """The play screen's table (THEME_SPEC 3.1): the mosaic, the slate calm areas in gilt lines, the rose window."""
    return render_tiles(lambda r: table_layer(r, True))


def page_scene() -> Image.Image:
    """THEME_SPEC 3 Table/page_scene.png (W20X): the non-play screens' backdrop, the mosaic everywhere."""
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
                 notes: list | None = None, name: str = "solid button", icon: str | None = None, spacing: float = 0,
                 icon_tinted: bool = True):
    """GameApp.SolidButton11: ui_chip_formula times the face tint - W20Y's pending buttonSecondary when the palette carries
    it (simulated in these mocks), else lerp(Background, Panel, .55); a disabled one a quarter toward PanelLight - the label
    LabelOn11 picks (preferring textColor; W20Y2: LabelOn11(face, Muted) when disabled, which may move the face a few 5 %
    steps), centred on the whole chip (FlatButton's label). ShopButton14's nav icon takes the label's colour; the play
    screen's 查看骰子 icon is drawn white (icon_tinted False)."""
    tint = secondary_face(P)
    if not enabled:
        tint = lerp_hex(tint, P["panelLight"], .25)
    face = kit.centre("ui_chip_formula")
    colour, tint, steps = label_on11(face, tint, P, P["textColor"] if enabled else P["muted"])
    c.sprite(kit, "ui_chip_formula", x, y, w, h, tint=tint)
    fill = drawn(face, tint)
    ratio = contrast(colour, fill)
    if notes is not None:
        notes.append(f"{name}{'' if enabled else ' (disabled)'}: face {fill}, label {colour} {ratio:.2f}:1"
                     + (f" (LabelOn11 moved the face {steps} step(s))" if steps else ""))
    if icon:
        s = min(34, h - 24) if icon_tinted else min(40, h - 16)
        tw = text_width(label, size, False)
        if icon_tinted:
            start = (w - (s + 8 + tw)) / 2
            c.sprite(kit, icon, x + start, y + (h - s) / 2, s, s, tint=colour, preserve=True)
            c.text(x + start + s + 8, y, w, h, label, size, colour, "lm")
        else:
            c.sprite(kit, icon, x + 16, y + (h - s) / 2, s, s, preserve=True, alpha=1.0 if enabled else .5)
            c.text(x + 8, y, w - 16, h, label, size, colour, "mm")
    else:
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
    tint = secondary_face(P)
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
    # A boss stage in play (the final-tier boss): the formula row puts the boss factor chip right after the red hand chip.
    "boss_play": dict(primary=("提交 2 颗", True), secondary="重掷 · 还能掷 2 [R]", end="end",
                      reason="按“提交”计分，或者重掷其余的骰子。"),
}
BOSS_STATES = ("c", "boss", "boss_play")
# The felt shader's boss mood (Art07Felt.shader, GameApp.Play11 ApplyTableMood11): the table times (1 - .26) and a
# vignette (1 - .5 x smoothstep(.22, .8, |(screen - (.5, .47)) x (1.18, 1)|)), in linear light, the table only.
BOSS_DIM, BOSS_VIGNETTE = .26, .5


def boss_table(table: Image.Image) -> Image.Image:
    """The table as a boss stage shows it (offline approximation of the felt shader's dim and vignette)."""
    a = np.asarray(table.convert("RGB")).astype(np.float32) / 255.0
    h, w = a.shape[:2]
    ys, xs = (np.arange(h, dtype=np.float32) + .5) / h, (np.arange(w, dtype=np.float32) + .5) / w
    X, Y = np.meshgrid(xs, ys)
    v = smoothstep(.22, .8, np.hypot((X - .5) * 1.18, Y - .47))
    k = (1 - BOSS_DIM) * (1 - BOSS_VIGNETTE * v)
    lin = _lin_arr(a) * k[..., None]
    return Image.fromarray(np.clip(np.rint(_gam_arr(lin) * 255), 0, 255).astype(np.uint8), "RGB")


def full_palette(pending: bool = True) -> dict:
    """The palette the composites draw with: theme.json's tokens, plus the pending keys (W20Y's buttonSecondary) unless
    pending is False. theme.json never carries the pending keys."""
    return dict(PALETTE, **W20X_TOKENS, **(PENDING_TOKENS if pending else {}))


def secondary_face(P: dict) -> str:
    """SolidButton11's face tint: the theme's buttonSecondary (W20Y, pending) or the code's lerp(Background, Panel, .55)."""
    return P.get("buttonSecondary") or lerp_hex(P["background"], P["panel"], .55)


def draw_top_bar(c: Canvas, kit: Kit, P: dict, large: bool, stage_tint: str, stage_label: str, caption: str,
                 notes: list, score: bool = True, pressure: bool = False) -> dict:
    """GameApp.Play11 TopBar11: the bar (ui_topbar x Panel), the stage tag (ui_chip_formula x the stage tint), the chapter
    points and caption, the score frame (play and settlement only), the gold and the two navigation buttons."""
    fs = 1.5 if large else 1.0
    meas = {}
    tx_, ty_, tw_, th_ = 16, 12, 1888, 72
    c.sprite(kit, "ui_topbar", tx_, ty_, tw_, th_, tint=P["panel"])
    bar_fill = drawn(kit.centre("ui_topbar"), P["panel"])
    fl, fb, fr, ft = kit.insets("ui_topbar")
    tag_w = 176 if large else 160
    colour, tag_tint, steps = label_on11(kit.centre("ui_chip_formula"), stage_tint, P)
    c.sprite(kit, "ui_chip_formula", tx_ + 8, ty_ + 8, tag_w, 56, tint=tag_tint)
    tag_fill = drawn(kit.centre("ui_chip_formula"), tag_tint)
    ratio = contrast(colour, tag_fill)
    notes.append(f"stage tag {tag_fill}: label {colour} {ratio:.2f}:1" + (f" (LabelOn11 {steps} step(s))" if steps else ""))
    c.text(tx_ + 8, ty_ + 10, tag_w, 48, stage_label, (24 if large else 28) * fs, colour, "mm", bold=True)
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
    muted_on_bar = label_on(bar_fill, P, P["muted"])[0]
    meas["chapter caption"] = c.text(tx_ + dots_x, ty_ + cap_top, 400, 32, caption, cap_size, muted_on_bar, "lt")
    cap_w = text_width(caption, cap_size, False)
    gold_w = 232 if large else 176
    gold_x = 1668 - 16 - gold_w
    if score:
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
        target_colour = P["pressure"] if pressure and contrast(P["pressure"], well_fill) >= 4.5 else \
            label_on(well_fill, P, P["muted"])[0]
        c.text(fx_ + target_x, fy_ + 4, fw - target_x - 26, 44, "/ 4,000", (26 if large else 32) * fs, target_colour, "lm")
        c.sprite(kit, "ui_progress_track", fx_ + 14, fy_ + 48, fw - 28, 7, tint="07191C", mult=48 / 7)
        c.sprite(kit, "ui_progress_fill", fx_ + 14, fy_ + 48, max(7, (fw - 28) * .62), 7,
                 tint=P["pressure"] if pressure else P["blue"], mult=48 / 7)
    c.sprite(kit, "ui_icon_coin", gold_x + 6, ty_ + 18, 36, 36, preserve=True)
    c.text(gold_x + 48, ty_ + 6, gold_w - 48 - (52 if large else 44), 60, "28", (30 if large else 34) * fs,
           label_on(bar_fill, P, P["gold"])[0], "rm", bold=True)
    c.text(gold_x + gold_w - (50 if large else 42), ty_ + 6, 60, 60, "金币", body11(17 if large else 20, fs) * fs, muted_on_bar,
           "lm")
    for x, label in ((1668, "手册"), (1788, "暂停")):
        button(c, kit, P, x, 18, 108, 60, P["paper"], label, 23 * fs, notes=notes if x == 1668 else None, name="top-bar button")
    return meas


def draw_card_row(c: Canvas, kit: Kit, P: dict, n_cards: int, large: bool, notes: list):
    """GameApp.Play11 DrawCards11: full cards (Table size; Text cards with enlarged text) and 56-wide "+" strips for the
    empty slots; two rows of compact mini cards from eight cards (six with enlarged text)."""
    fs = 1.5 if large else 1.0
    cx0, cy0, cw0, ch0 = 32, 96, 1448, 244
    mini = n_cards > 7 or (large and n_cards > 5)
    if mini:
        cw, chh = (cw0 - 4 * 12) / 5, (ch0 - 10) / 2
        for i in range(min(n_cards, 9 if n_cards > 10 else 10)):
            rarity, title, text, art = MOCK_CARDS[i % len(MOCK_CARDS)]
            compact_card(c, kit, P, cx0 + i % 5 * (cw + 12), cy0 + i // 5 * (chh + 10), cw, chh, rarity, title, text, art, fs,
                         ordinal=i + 1, notes=notes)
        return
    slots = 5 if n_cards <= 5 else 7
    empty = max(0, slots - n_cards)
    card_w = 0 if n_cards == 0 else min(280 if large else 216, (cw0 - empty * 68 - 12 * (n_cards - 1)) / n_cards)
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
        if x + 56 > cx0 + cw0 + .5:
            break
        c.sprite(kit, "ui_slot_inventory", x, cy0 + 8, 56, ch0 - 16)
        c.text(x, cy0 + 8, 56, ch0 - 16, "+", 30 * fs, P["muted"], "mm", font=BODY_FONT)
        x += 68


def play_mock(kit: Kit, table: Image.Image, state: str = "f", large: bool = False, notes: list | None = None,
              palette: dict | None = None, n_cards: int | None = None) -> tuple[Image.Image, dict]:
    """The play screen as GameApp.Play11 lays it out at 100 % or 150 % text, over table_scene. State a: three cards, five
    dice with two selected; f: seven cards, eight dice with two locked (W11's capture states); the boss states dim the
    table as the felt shader does. Returns the composite and measurements (hint glyph box, rectangles)."""
    P = dict(palette or full_palette())
    fs = 1.5 if large else 1.0
    L = play_layout(large)
    s = ACTION_STATES[state]
    boss, pressure = state in BOSS_STATES, state == "pressure"
    stage_tint = P["boss"] if boss else P["pressure"] if pressure else P["panelLight"]
    n_cards = (7 if state == "f" else 3) if n_cards is None else n_cards
    pool = (8 if state == "f" else 5)
    locked = 2 if state in ("f", "qualified", "pressure", "boss_play") else 0
    c = Canvas(1920, 1080)
    c.backdrop(boss_table(table) if boss else table)
    notes = notes if notes is not None else []
    meas: dict = {}
    # ---------------------------------------------------------------- top bar and cards
    kind = "首领关" if boss else "高压关" if pressure else "普通关"
    meas.update(draw_top_bar(c, kit, P, large, stage_tint, "第 4 关", "第 2 章 · " + kind, notes, score=True,
                             pressure=pressure))
    well_fill = kit.centre("ui_info_inset")
    draw_card_row(c, kit, P, n_cards, large, notes)
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
    head_label, head_tint, _ = label_on11(kit.centre("ui_chip_formula"), stage_tint, P)
    c.sprite(kit, "ui_chip_formula", sx + 14, sy + 14, inner, head_h, tint=head_tint)
    c.text(sx + 26, sy + 14, inner - 24, head_h, ("首领减益 · 终局" if state == "boss_play" else "首领减益 · 二档") if boss else kind,
           body11(18 if large else 22, fs) * fs, head_label, "lm")
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
    if state in ("a", "c"):          # nothing scored and no preview yet (the hand's first roll is still to come in c)
        c.sprite(kit, "ui_chip_formula", kx, ky, kw, kh, tint=cap_tint)
        c.text(kx + 10, ky, kw - 20, kh, "本关还没有得分", body11(17 if large else 20, fs) * fs, label_on(cap_fill, P, P["muted"])[0], "mm")
    else:
        caption_text = "预计"
        cap_w = min(420 if large else 330, max(120 if large else 96, text_width(caption_text, body11(17 if large else 20, fs) * fs, False) + 32))
        c.sprite(kit, "ui_chip_formula", kx, ky, cap_w, kh, tint=cap_tint)
        c.text(kx + 10, ky, cap_w - 20, kh, caption_text, body11(17 if large else 20, fs) * fs, label_on(cap_fill, P, P["muted"])[0], "mm")
        if state == "boss_play":
            # GameApp.Play11Score Factors11: base, hand (UiKit.Red), then the global factors; a boss factor is UiKit.Boss.
            factors = (("基础分", "26", "blue"), ("一对", "×2", "red"), ("满员", "×0.8", "boss"))
        else:
            factors = (("基础分", "26", "blue"), ("一对", "×2", "red"), ("全蓝", "×3", "blue"), ("卡牌", "×1.5", "gold"))
        font, sep, pad = (22 if large else 26) * fs, (26 if large else 30), 30
        x = kx + cap_w + 12
        for i, (name, value, key) in enumerate(factors):
            wv = text_width(value, font) + text_width(name, 19.5 * fs, False) + pad
            ink, chip_tint, steps = label_on11(kit.centre("ui_chip_formula"), P[key], P)
            c.sprite(kit, "ui_chip_formula", x, ky, wv, kh, tint=chip_tint)
            ffill = drawn(kit.centre("ui_chip_formula"), chip_tint)
            meas.setdefault("factor chips", []).append((name, key, ffill, ink, steps))
            nw = text_width(name, 19.5 * fs, False) + 2
            c.text(x + 10, ky, nw, kh, name, 19.5 * fs, ink, "lm")
            c.text(x + 10 + nw + 2, ky, wv - nw - 20, kh, value, font, ink, "rm", bold=True)
            x += wv
            c.text(x, ky, sep, kh, "×" if i < len(factors) - 1 else "=", (20 if large else 24) * fs, P["onFelt"], "mm")
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
    solid_button(c, kit, P, px_ + 14, iy + 34 + 64 + 12, pw_ - 28, 60, "查看骰子", 22 * fs, notes=notes, name="查看骰子",
                 icon="ui_icon_dice_view", icon_tinted=False)
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
    # Boss (coordinator, lesson from the deco kit): at least 15 CIEDE2000 from primary, from the red pip colour (the formula
    # row puts the boss factor chip beside the red hand chip) and from pressure, as the chips are drawn (LabelOn11 may
    # move a tint).
    chip_white = "FFFFFF"
    boss_label, boss_plate, boss_steps = label_on11(chip_white, p["boss"], p)
    red_label, red_plate, red_steps = label_on11(chip_white, p["red"], p)
    out["boss/primary dE00"] = de00(p["boss"], p["primary"])
    out["boss/red dE00"] = de00(p["boss"], p["red"])
    out["boss/pressure dE00"] = de00(p["boss"], p["pressure"])
    out[f"boss chip {boss_plate} / red chip {red_plate} dE00 (drawn, steps {boss_steps}/{red_steps})"] = \
        de00(boss_plate, red_plate)
    out[f"boss chip {boss_plate} / primary dE00 (drawn)"] = de00(boss_plate, p["primary"])
    out[f"boss chip label {boss_label} (drawn)"] = contrast(boss_label, boss_plate)
    out["boss/danger dE00"] = de00(p["boss"], p["danger"])
    # W20Y's pending buttonSecondary (not in theme.json): the SolidButton11 face on the table (felt and mosaic), the dark
    # panels and the top bar (panel), the page background, the parchment; its label (LabelOn11 preferring textColor);
    # the disabled face, a quarter toward panelLight, with W20Y2's LabelOn11(face, Muted) label.
    sec = p.get("buttonSecondary") or PENDING_TOKENS.get("buttonSecondary")
    if sec:
        out[f"buttonSecondary {sec}/felt (pending)"] = contrast(sec, p["felt"])
        out[f"buttonSecondary {sec}/panel = top bar, dark panels (pending)"] = contrast(sec, p["panel"])
        out[f"buttonSecondary {sec}/background (pending)"] = contrast(sec, p["background"])
        out[f"buttonSecondary {sec}/paper (pending)"] = contrast(sec, p["paper"])
        on_label, on_tint, on_steps = label_on11(chip_white, sec, p, p["textColor"])
        out[f"buttonSecondary label {on_label} (LabelOn11, steps {on_steps})"] = contrast(on_label, drawn(chip_white, on_tint))
        off = lerp_hex(sec, p["panelLight"], .25)
        off_label, off_tint, off_steps = label_on11(chip_white, off, p, p["muted"])
        out[f"disabled buttonSecondary {off}: dE00 to the enabled face"] = de00(sec, off)
        out[f"disabled buttonSecondary drawn {off_tint} (LabelOn11 steps {off_steps}): dE00 to the enabled face"] = \
            de00(sec, off_tint)
        out[f"disabled buttonSecondary label {off_label} (LabelOn11 Muted)"] = contrast(off_label, drawn(chip_white, off_tint))
        out["disabled buttonSecondary: label differs from the enabled label"] = float(off_label != on_label)
        out[f"muted/disabled buttonSecondary {off} (today's label)"] = contrast(p["muted"], off)
    return out


def label_on11(face: str, tint: str, palette: dict, preferred: str | None = None) -> tuple[str, str, int]:
    """GameApp.LabelOn11 on a surface whose centre pixel is `face`, tinted `tint`: a preferred colour stays at 4.5:1; else
    the best of ink, paper and textColor at 4.95:1; else the tint steps 5 % (sRGB) toward black under the lightest of
    them or toward white under the darkest, the fewer steps winning, a tie deepening. Returns (label, drawn tint, steps)."""
    fill = drawn(face, tint)
    if preferred is not None and contrast(preferred, fill) >= 4.5:
        return preferred, tint, 0
    cands = [palette[k] for k in ("ink", "paper", "textColor")]
    best = max(cands, key=lambda c: contrast(c, fill))
    if contrast(best, fill) >= 4.95:
        return best, tint, 0
    light = max(cands, key=luminance)
    dark = min(cands, key=luminance)

    def steps(toward, label):
        t = col(tint).astype(np.float64)
        for i in range(25):
            hx = hexc(t)
            if contrast(label, drawn(face, hx)) >= 4.95:
                return i, hx
            t = t + (np.asarray(toward, np.float64) - t) * .05
        return -1, None
    deepen, deep_tint = steps((0, 0, 0), light)
    lighten, light_tint = steps((1, 1, 1), dark)
    if deepen < 0 and lighten < 0:
        return best, tint, 0
    down = lighten < 0 or 0 <= deepen <= lighten
    return (light, deep_tint, deepen) if down else (dark, light_tint, lighten)


# ---------------------------------------------------------------------------------------------- build
def theme_json() -> str:
    data = dict(THEME_META)
    data["palette"] = dict(PALETTE, **W20X_TOKENS)
    return json.dumps(data, ensure_ascii=False, indent=2) + "\n"


def save_png(path: Path, im: Image.Image):
    path.parent.mkdir(parents=True, exist_ok=True)
    # Explicit parameters, no timestamp/EXIF/ICC or machine-specific metadata.
    im.save(path, format="PNG", optimize=False, compress_level=9)


def check_slices(name: str, im: Image.Image, border):
    """Fail before writing: a nine-slice centre is one flat colour from the border lines inward; a horizontally sliced
    strip keeps its middle columns identical; stretched edge bands only change across the edge (tinted pieces exactly,
    authored ones by at most 6 levels a pixel along the edge, brief rule 2)."""
    px = np.asarray(im).astype(np.int16)
    h, w = px.shape[:2]
    l, b, r, t = (int(v) for v in border)
    if l and r and b and t:
        centre = px[t:h - b, l:w - r]
        if (centre != centre[0, 0]).any():
            raise ValueError(f"{name}: the nine-slice centre is not one flat colour")
        bands = (px[:t, l:w - r], px[h - b:, l:w - r], px[t:h - b, :l].transpose(1, 0, 2),
                 px[t:h - b, w - r:].transpose(1, 0, 2))
        limit = 0 if name in NEUTRAL_NAMES else 6
        for band in bands:
            if int(np.abs(np.diff(band, axis=1)).max()) > limit:
                raise ValueError(f"{name}: a stretched edge band changes along the edge")
    elif l and r:
        middle = px[:, l:w - r]
        if (middle != middle[:, :1]).any():
            raise ValueError(f"{name}: the middle columns of a horizontal slice differ")


def _review_module():
    import importlib.util
    spec = importlib.util.spec_from_file_location("glass_review", Path(__file__).with_name("review_glass.py"))
    mod = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(mod)
    return mod


def build(output_root: Path = ROOT) -> list[Path]:
    """Write the 50 referenced contract sprites, the table and page scenes, the felt tile, theme.json, and the preview and
    contact sheet (drawn from the sprites just written)."""
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
        if spec["name"] in ("ui_card_controller_compact", "ui_card_shop") and "ui_card_controller" in sprites:
            im = sprites["ui_card_controller"].copy()       # the same frame (W13 reads each face's own insets)
        else:
            im = render_sprite(spec)
        if im.mode != "RGBA" or list(im.size) != spec["size"]:
            raise ValueError("Contract mismatch: " + spec["name"])
        check_slices(spec["name"], im, spec["border"])
        sprites[spec["name"]] = im
        save(THEME / "UI" / (spec["name"] + ".png"), im)
    save(THEME / "Table/table_scene.png", table)
    page = page_scene()
    save(THEME / "Table/page_scene.png", page)
    save(THEME / "Table/felt_tile.png", felt_tile())
    theme_path = output_root / THEME / "theme.json"
    theme_path.parent.mkdir(parents=True, exist_ok=True)
    theme_path.write_text(theme_json(), encoding="utf-8", newline="\n")
    written.append(theme_path)
    review = _review_module()
    mock, sheet = review.build_outputs(sprites, table, page)
    save(THEME / "preview.png", mock.resize((480, 270), Image.Resampling.LANCZOS))
    save(REVIEW / "contact_sheet.png", sheet)
    return written


if __name__ == "__main__":
    import sys
    import time
    if "--report" in sys.argv:
        for key, value in palette_report(full_palette()).items():
            print(f"GLASS_PALETTE {key} {value:.2f}")
    elif "--review" in sys.argv:
        _review_module().main([a for a in sys.argv[1:] if a != "--review"])
    elif "--card-review" in sys.argv:
        _review_module().write_card_template_review()
    else:
        started = time.time()
        paths = build()
        pngs = [p for p in paths if p.suffix == ".png"]
        print(f"GLASS_BUILT {len(pngs)} PNGs + theme.json / supersampling {SS}x / {time.time() - started:.1f}s")
        for key, value in palette_report(full_palette()).items():
            print(f"GLASS_PALETTE {key} {value:.2f}")
