"""W20: deterministic 唐三彩陶坊 (sancai) UI kit: procedural lead glaze, 4x raster, one Lanczos downsample.

Style authority: the five user-chosen concept pages in ArtSource/StyleKit/themes/sancai/concepts and the glaze
samples of ArtSource/StyleKit/themes/sancai/style_kit.json. Their texts, numbers and layouts are not a contract;
the game layout is (docs/specs/v11/PLAY_LAYOUT.md). The contract (tools/art_v11/theme_contract.json) owns pixel
sizes, nine-slice borders and PPU. Repository fonts only; no image generation, no Unity files.

Rules the art keeps (checked by tests/test_art_v11_sancai.py):
- every nine-slice centre is one flat colour (or transparent) from the border line inward;
- surfaces that the game stretches far keep their edge bands invariant along the edge; glaze runs, crackle and
  ornaments live in the corner squares, which are never stretched;
- sprites the code multiplies by a palette colour (NEUTRAL_NAMES) stay achromatic;
- the protected table zones are exactly the felt colour;
- the W13 card template (CARD_TEMPLATE_NAMES): a rarity ring no wider than the default's, and three gems whose shapes
  (round, diamond, six-point star) tell the rarity apart without colour.

`build_sancai.py --card-review` writes ArtSource/Art11/sancai/card_template_review.png from the shipped sancai and Swiss
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
THEME = Path("DicePrototype/Assets/Resources/Themes/sancai")
REVIEW = Path("ArtSource/Art11/sancai")
CONCEPTS = Path("ArtSource/StyleKit/themes/sancai/concepts")
CONCEPT_PAGES = ("roll", "shop", "select", "card", "triptych")
FONT = ROOT / "DicePrototype/Assets/Game/Fonts/NotoSansSC-Bold.otf"
BRUSH = ROOT / "DicePrototype/Assets/Game/Fonts/Themes/sancai/MaShanZheng-Regular.ttf"
LATIN = ROOT / "DicePrototype/Assets/TextMesh Pro/Fonts/LiberationSans.ttf"
SS = 4
SEED = 20261117

# ---------------------------------------------------------------------------------------------- palette
# theme.json palette (THEME_SPEC section 2). The generator is the single source: build() writes theme.json from it,
# and the table felt below is exactly PALETTE["felt"]. Reasons and contrast numbers: tools/art_v11/sancai/README.md.
PALETTE = {
    "background": "24160C",   # page base: darkest umber clay
    "felt": "4A2A18",         # table felt, the flat colour of every protected zone
    "panel": "31441C",        # dark copper-green glaze: top bar, dark panels, secondary chips
    "panelLight": "4B6A28",   # lighter copper green: dividers, normal-stage tag, tray rim mix
    "paper": "F2DCB0",        # cream glaze: cards, paper panels, button faces
    "ink": "3A2211",          # umber: text on cream
    "textColor": "F8ECD2",    # light cream: text on dark glaze
    "muted": "CDB78F",        # pale clay: secondary text on dark glaze
    "gold": "EBA53F",         # amber glaze: prices, coins, highlights
    "teal": "9BC766",         # light copper green: done, positive
    # red and blue also colour the 3D dice pips (DiceTable reads UiKit.Red / UiKit.Blue). Pip colour is the game's main
    # functional cue, so every style keeps the default pair (coordinator, 2026-11-17, CP3-1; THEME_SPEC section 2).
    "red": "EC655D",
    "blue": "55B6D9",
    "violet": "B58BC9",       # manganese aubergine glaze, thinned: enchantments
    "danger": "A0301E",       # deep iron red: errors, boss warnings
    "shadow": "140B05",
    "ghost": "D2AC74",        # pale amber: outline buttons, empty slot rings
    # Coordinator decision 2026-11-17: cobalt, the rarest glaze, marks rare cards; the one primary action per phase takes
    # the amber-gold glaze, as the default style's main button is gold. primary == gold, as in the default palette.
    "primary": "EBA53F",      # amber-gold glaze: the one primary action per phase (label: ink)
    "reward": "F2B64E",       # bright amber: reward moments
    "boss": "A93F1C",         # iron red
    "pressure": "F07436",     # persimmon orange: pressure stages (a warning hue, never grey)
    "rarityCommon": "D3C7AA", # plain ivory glaze
    "rarityAdvanced": "4F7D2B",  # copper-green glaze
    "rarityRare": "2C4E9A",   # cobalt glaze (COBALT below): the concept card page's "稀有" seal
    "onFelt": "EBD3A4",       # light cream marks drawn straight on the felt
}
THEME_META = {
    "id": "sancai",
    "displayName": "唐三彩陶坊",
    "description": ("琥珀、铜绿、乳白三色铅釉：圆润软棱的釉面陶板、海棠委角、祥云卷角、细密开片与流釉，深褐陶胎桌面；"
                    "主按钮用琥珀金釉，深钴蓝只留给稀有。"
                    "依据：ArtSource/StyleKit/themes/sancai/concepts 与 style_kit.json；界面件与色板由 "
                    "tools/art_v11/sancai/build_sancai.py 生成（说明见同目录 README.md）"),
    "order": 2,
    "preview": False,
    "fonts": {
        "title": "Fonts/title",
        "roles": ["screenTitle", "stageNumber", "bigNumber", "bossName", "primaryButton"],
    },
}


def rgb8(value: str) -> tuple[int, int, int]:
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4))


def col(value: str) -> np.ndarray:
    return np.array(rgb8(value), np.float32) / 255.0


def _lin(c: float) -> float:
    c /= 255.0
    return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4


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


def _gam(c: float) -> float:
    c = max(c, 0.0)
    return c * 12.92 if c <= .0031308 else 1.055 * c ** (1 / 2.4) - .055


def drawn(fill: str, tint: str) -> str:
    """A sprite pixel tinted as Unity UI draws it in this linear-colour-space project (GameApp.RenderedFill11)."""
    return "".join(f"{int(round(_gam(_lin(f) * _lin(t)) * 255)):02X}" for f, t in zip(rgb8(fill), rgb8(tint)))


def best_label(fill: str, palette: dict) -> tuple[str, float]:
    """GameApp.BestLabel11: of ink, paper and textColor, the label with the highest contrast on a drawn fill."""
    key = max(("ink", "paper", "textColor"), key=lambda k: contrast(palette[k], fill))
    return key, contrast(palette[key], fill)


def palette_report(palette: dict | None = None) -> dict[str, float]:
    """Contrast numbers quoted in the README and asserted by the tests (WCAG 2 sRGB relative luminance)."""
    p = palette or PALETTE
    # THEME_SPEC section 2 valueBadge (unset: gold) and danger, drawn on the ui_chip_label face (ValueBadge13).
    chip = "".join(f"{int(round(float(v) * 255)):02X}" for v in LABEL_FILL)
    value_fill, warning_fill = drawn(chip, p.get("valueBadge", p["gold"])), drawn(chip, p["danger"])
    out = {
        "textColor/panel": contrast(p["textColor"], p["panel"]),
        "ink/paper": contrast(p["ink"], p["paper"]),
        "muted/panel": contrast(p["muted"], p["panel"]),
        "onFelt/felt": contrast(p["onFelt"], p["felt"]),
        f"{best_label(p['primary'], p)[0]}/primary (label pick)": best_label(p["primary"], p)[1],
        "valueBadge/danger badge dE00 (drawn)": ciede2000(lab(value_fill), lab(warning_fill)),
        "valueBadge/danger badge contrast (drawn)": contrast(value_fill, warning_fill),
        f"{best_label(value_fill, p)[0]}/valueBadge (drawn)": best_label(value_fill, p)[1],
        f"{best_label(warning_fill, p)[0]}/danger badge (drawn)": best_label(warning_fill, p)[1],
        "textColor/boss": contrast(p["textColor"], p["boss"]),
        "ink/pressure": contrast(p["ink"], p["pressure"]),
        "textColor/panelLight": contrast(p["textColor"], p["panelLight"]),
        "ink/gold": contrast(p["ink"], p["gold"]),
        "gold/panel": contrast(p["gold"], p["panel"]),
        "teal/panel": contrast(p["teal"], p["panel"]),
        "textColor/danger": contrast(p["textColor"], p["danger"]),
        "danger/paper": contrast(p["danger"], p["paper"]),
    }
    rar = ("rarityCommon", "rarityAdvanced", "rarityRare")
    for i, a in enumerate(rar):
        for b in rar[i + 1:]:
            out[f"{a}/{b}"] = contrast(p[a], p[b])
        out[f"primary/{a} dE"] = delta_e(p["primary"], p[a])
    out["pressure saturation"] = colorsys.rgb_to_hsv(*(v / 255 for v in rgb8(p["pressure"])))[1]
    return out


# Glaze colours (sRGB floats). Sampled from style_kit.json and the concept pages, then tuned for small sizes.
CREAM = col(PALETTE["paper"])
CREAM_HI = col("FBF0DC")
CREAM_SH = col("DDBE8E")
AMBER = col("D98E2B")
AMBER_HI = col("F3BC5E")
AMBER_DEEP = col("9E5916")
GREEN = col("557A2C")
GREEN_HI = col("86A64B")
GREEN_DEEP = col("2C4515")
COBALT = col("2C4E9A")
COBALT_HI = col("6F95DA")
IRON = col("B5481E")
IRON_DEEP = col("7A2A10")
UMBER = col("5A3614")
UMBER_DEEP = col(PALETTE["ink"])
AUBERGINE = col("6E3F78")
FELT = col(PALETTE["felt"])
PANEL = col(PALETTE["panel"])
WHITE = np.ones(3, np.float32)
OUTLINE = col("3B2312")

# Sprites the game multiplies by palette colours: they stay achromatic (white face, grey relief only).
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
# Sliced surfaces the game stretches far beyond their source size: edge bands invariant along the edge.
STRETCH_NAMES = frozenset({
    "ui_topbar", "ui_dice_tray", "ui_chip_formula", "ui_stage_card", "ui_button_bevel", "ui_button_ghost",
    "ui_ring", "ui_frame_block", "ui_frame_enchant", "ui_price_chip", "ui_paper_panel", "ui_panel_hud",
    "ui_section_panel", "ui_tab_bottom", "ui_info_inset", "ui_slot_inventory", "ui_chip_label", "ui_card_frame_rarity",
})
# Calm zones of the play screen (PLAY_LAYOUT 1 and 8, W12b): one flat felt colour, no texture.
FELT_ZONES = {
    "dice_area": (52, 374, 1408, 276),
    "formula_row": (52, 660, 1408, 60),
    "submission_rail": (344, 740, 1136, 202),
    "submit_info_text150": (32, 740, 360, 202),
}
MAX_FELT_RGB_DEVIATION = 0


# ---------------------------------------------------------------------------------------------- noise
def _hash(ix: np.ndarray, iy: np.ndarray, seed: int) -> np.ndarray:
    x = ix.astype(np.int64).astype(np.uint32)
    y = iy.astype(np.int64).astype(np.uint32)
    h = x * np.uint32(0x8DA6B343) + y * np.uint32(0xD8163841) + np.uint32((seed * 0x9E3779B1) & 0xFFFFFFFF)
    h ^= h >> np.uint32(15)
    h *= np.uint32(0x2C1B3C6D)
    h ^= h >> np.uint32(12)
    h *= np.uint32(0x297A2D39)
    h ^= h >> np.uint32(15)
    return h


def _rand01(ix, iy, seed: int) -> np.ndarray:
    return (_hash(ix, iy, seed) >> np.uint32(8)).astype(np.float32) * np.float32(1.0 / (1 << 24))


def smoothstep(a: float, b: float, x):
    t = np.clip((x - a) / (b - a), 0.0, 1.0)
    return t * t * (3.0 - 2.0 * t)


def value_noise(X, Y, scale: float, seed: int, period: int | None = None):
    gx = X / np.float32(scale)
    gy = Y / np.float32(scale)
    fx0 = np.floor(gx)
    fy0 = np.floor(gy)
    fx = gx - fx0
    fy = gy - fy0
    ix = fx0.astype(np.int64)
    iy = fy0.astype(np.int64)
    ix1, iy1 = ix + 1, iy + 1
    if period:
        ix, iy, ix1, iy1 = ix % period, iy % period, ix1 % period, iy1 % period
    ux = fx * fx * fx * (fx * (fx * 6 - 15) + 10)
    uy = fy * fy * fy * (fy * (fy * 6 - 15) + 10)
    a = _rand01(ix, iy, seed)
    b = _rand01(ix1, iy, seed)
    c = _rand01(ix, iy1, seed)
    d = _rand01(ix1, iy1, seed)
    return a + (b - a) * ux + (c - a) * uy + (a - b - c + d) * ux * uy


def fbm(X, Y, scale: float, seed: int, octaves: int = 3, gain: float = .5, period: int | None = None):
    total = np.zeros(np.shape(X), np.float32)
    amp, norm = 1.0, 0.0
    for o in range(octaves):
        p = period * 2 ** o if period else None
        total += np.float32(amp) * value_noise(X, Y, scale / 2 ** o, seed + 101 * o, p)
        norm += amp
        amp *= gain
    return total / np.float32(norm)


def worley(X, Y, cell: float, seed: int, jitter: float = .9):
    gx = X / np.float32(cell)
    gy = Y / np.float32(cell)
    bx = np.floor(gx)
    by = np.floor(gy)
    fx = gx - bx
    fy = gy - by
    ix = bx.astype(np.int64)
    iy = by.astype(np.int64)
    f1 = np.full(np.shape(X), 9.0, np.float32)
    f2 = np.full(np.shape(X), 9.0, np.float32)
    for dy in (-1, 0, 1):
        for dx in (-1, 0, 1):
            cx, cy = ix + dx, iy + dy
            px = dx + .5 + (_rand01(cx, cy, seed) - .5) * jitter
            py = dy + .5 + (_rand01(cx, cy, seed + 7) - .5) * jitter
            dist = np.hypot(px - fx, py - fy)
            f2 = np.where(dist < f1, f1, np.minimum(f2, dist))
            f1 = np.minimum(f1, dist)
    return f1 * cell, f2 * cell


def crackle(X, Y, cell: float, width: float, seed: int, warp: float = 1.6):
    """Crazing network: wobbly Voronoi cell borders, 1 on a line, 0 between lines."""
    wx = X + (fbm(X, Y, cell * 1.3, seed + 11, 2) - .5) * (2 * warp)
    wy = Y + (fbm(X, Y, cell * 1.3, seed + 23, 2) - .5) * (2 * warp)
    f1, f2 = worley(wx, wy, cell, seed)
    edge = (f2 - f1) * .5
    return 1.0 - smoothstep(0.0, width, edge)


def crazing(X, Y, scale: float, seed: int):
    """Primary crackle plus a fainter secondary network that shows in patches, as on lead glaze."""
    main = crackle(X, Y, 9.0 * scale, .55 * scale, seed)
    fine = crackle(X, Y, 4.2 * scale, .38 * scale, seed + 5) * smoothstep(.45, .7, fbm(X, Y, 24 * scale, seed + 9, 2))
    return np.maximum(main, .55 * fine)


# ---------------------------------------------------------------------------------------------- geometry
def grid(w: int, h: int, x0: float = 0.0, y0: float = 0.0):
    xs = ((np.arange(w * SS, dtype=np.float64) + .5) / SS + x0).astype(np.float32)
    ys = ((np.arange(h * SS, dtype=np.float64) + .5) / SS + y0).astype(np.float32)
    return np.meshgrid(xs, ys)


def sd_round_box(X, Y, box, r: float):
    x0, y0, x1, y1 = box
    cx, cy = (x0 + x1) * .5, (y0 + y1) * .5
    hx, hy = (x1 - x0) * .5 - r, (y1 - y0) * .5 - r
    qx = np.abs(X - np.float32(cx)) - np.float32(hx)
    qy = np.abs(Y - np.float32(cy)) - np.float32(hy)
    return (np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0) - np.float32(r))


def sd_notch_box(X, Y, box, notch: float, r: float = 0.0, ear: float = 0.0):
    """Rectangle with concave quarter-circle corners (the 'begonia' corner of the concept's slots and banners).
    `ear` rounds the two convex tips of every notch, so the corner reads as a soft inward curl, not a cut."""
    x0, y0, x1, y1 = box
    if ear:
        x0, y0, x1, y1 = x0 + ear, y0 + ear, x1 - ear, y1 - ear
    d = sd_round_box(X, Y, (x0, y0, x1, y1), r)
    for cx, cy in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
        d = np.maximum(d, np.float32(notch + ear) - np.hypot(X - np.float32(cx), Y - np.float32(cy)))
    return d - np.float32(ear) if ear else d


def sd_circle(X, Y, cx, cy, r):
    return np.hypot(X - np.float32(cx), Y - np.float32(cy)) - np.float32(r)


def coverage(d):
    """Antialiased silhouette at the supersampled density (d in output pixels, negative inside)."""
    return np.clip(.5 - d * SS, 0.0, 1.0)


def corner_weight(X, Y, w: int, h: int, inner: float = 20.0, outer: float = 33.0):
    """1 near each corner, 0 once a pixel is `outer` px from the corner along either axis (stays out of the
    stretched middle of every edge band and keeps a Lanczos margin before a 40 px border line)."""
    ax = np.minimum(X, w - X)
    ay = np.minimum(Y, h - Y)
    return (1 - smoothstep(inner, outer, ax)) * (1 - smoothstep(inner, outer, ay))


# ---------------------------------------------------------------------------------------------- light
LIGHT = np.array([-.5, -.62, .6], np.float64)
LIGHT /= np.linalg.norm(LIGHT)
HALF = LIGHT + np.array([0, 0, 1.0])
HALF /= np.linalg.norm(HALF)
HALF2 = np.array([-.15, -.55, 1.0])
HALF2 /= np.linalg.norm(HALF2)


def lighting(H, height: float, ambient: float = .42, gloss: float = .55, power: float = 70.0):
    """Lambert relative to a flat face (flat = 1.0) and a glossy lead-glaze specular (flat = 0.0)."""
    gy, gx = np.gradient(H.astype(np.float32), np.float32(1.0 / SS))
    nx = -gx * np.float32(height)
    ny = -gy * np.float32(height)
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + 1.0)
    nx *= inv
    ny *= inv
    nz = inv
    ndl = nx * LIGHT[0] + ny * LIGHT[1] + nz * LIGHT[2]
    diffuse = ambient + (1 - ambient) * np.clip(ndl / LIGHT[2], 0, None)
    spec = np.zeros_like(H, dtype=np.float32)
    for half, base_power, k in ((HALF, power, gloss), (HALF2, power * 1.6, gloss * .6)):
        ndh = np.clip(nx * half[0] + ny * half[1] + nz * half[2], 0, 1)
        flat = half[2] ** base_power
        spec += np.float32(k) * np.clip((ndh ** base_power - flat) / (1 - flat), 0, 1)
    return diffuse.astype(np.float32), spec


def rim_height(t_out, d_in, crest: float, face: float = .5, groove: float = .22, groove_w: float = 1.1, soft: float = 6.0):
    """Raised glaze bead: rounded outer slope over `crest` px, inner slope down to the face over `soft` px,
    a thin groove where bead meets face. t_out: depth below the silhouette; d_in: signed distance to the face edge."""
    up = np.sin(np.clip(t_out / np.float32(crest), 0, 1) * np.float32(np.pi / 2))
    down = face + (1 - face) * smoothstep(0.0, soft, d_in)
    h = np.minimum(up, down)
    h = h - groove * np.exp(-(d_in / np.float32(groove_w)) ** 2)
    return np.where(t_out < 0, 0.0, h).astype(np.float32)


# ---------------------------------------------------------------------------------------------- raster
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
        """A view on part of this raster (output-pixel box); drawing into it writes through."""
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

    def mask(self, draw_fn) -> np.ndarray:
        """Binary SS mask in this raster's frame; draw_fn(d, s) gets an ImageDraw and a point scaler."""
        x0 = float(self.X[0, 0]) - .5 / SS
        y0 = float(self.Y[0, 0]) - .5 / SS
        return mask_from_draw(self.w, self.h, draw_fn, x0, y0)

    def strokes(self, strokes) -> np.ndarray:
        x0 = float(self.X[0, 0]) - .5 / SS
        y0 = float(self.Y[0, 0]) - .5 / SS
        return stroke_mask(self.w, self.h, strokes, x0, y0)

    @property
    def origin(self) -> tuple[int, int]:
        return int(round(float(self.X[0, 0]) - .5 / SS)), int(round(float(self.Y[0, 0]) - .5 / SS))

    def window_abs(self, x0: float, y0: float, w: float, h: float):
        """Window in absolute output coordinates (this raster may itself be an offset tile); None if outside."""
        ox, oy = self.origin
        lx0, ly0 = int(math.floor(x0)) - ox, int(math.floor(y0)) - oy
        lx1, ly1 = int(math.ceil(x0 + w)) - ox, int(math.ceil(y0 + h)) - oy
        lx0, ly0, lx1, ly1 = max(0, lx0), max(0, ly0), min(self.w, lx1), min(self.h, ly1)
        if lx1 <= lx0 or ly1 <= ly0:
            return None
        return self.window(lx0, ly0, lx1 - lx0, ly1 - ly0)

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


def downsample_rgb(rgb: np.ndarray, w: int, h: int) -> np.ndarray:
    out = np.zeros((h, w, 3), np.float32)
    for c in range(3):
        out[..., c] = _resize_f(rgb[..., c], w, h)
    return out


def mask_from_draw(w: int, h: int, draw_fn, x0: float = 0.0, y0: float = 0.0) -> np.ndarray:
    """Binary silhouette drawn with PIL at SS x; draw_fn(d, s) gets an ImageDraw and a point scaler."""
    im = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(im)
    s = lambda pts: [((x - x0) * SS, (y - y0) * SS) for x, y in pts]
    draw_fn(d, s)
    return np.asarray(im, np.float32) / 255.0


def inside_distance(mask: np.ndarray) -> np.ndarray:
    """Distance (output px) from each covered sample to the silhouette edge; 0 outside."""
    return (ndimage.distance_transform_edt(mask > .5) / SS).astype(np.float32)


def outside_distance(mask: np.ndarray) -> np.ndarray:
    return (ndimage.distance_transform_edt(mask <= .5) / SS).astype(np.float32)


# ---------------------------------------------------------------------------------------------- colour helpers
def mix(a, b, t):
    t = np.asarray(t, np.float32)
    return a + (b - a) * t[..., None]


def ramp(u, stops):
    xs = [s[0] for s in stops]
    cs = np.stack([np.asarray(s[1], np.float32) for s in stops])
    out = np.empty(np.shape(u) + (3,), np.float32)
    for c in range(3):
        out[..., c] = np.interp(u, xs, cs[:, c])
    return out


def mean_colour(*colours):
    return np.mean(np.stack(colours), axis=0).astype(np.float32)


# Rim glaze schemes: an invariant profile across the rim (u: 0 at the silhouette, 1 at the face edge) and, where
# texture is allowed, three-colour mottling and runs. They take point arrays, so the noise runs only where needed.
SCHEMES = {
    "amber": dict(profile=[(0, AMBER_DEEP), (.22, AMBER), (.42, AMBER_HI), (.7, AMBER), (1, AMBER_DEEP)],
                  green=.42, cream=.3, run=.5),
    "amber_cream": dict(profile=[(0, AMBER_DEEP), (.25, AMBER), (.45, CREAM_HI), (.72, AMBER_HI), (1, AMBER_DEEP)],
                        green=.38, cream=.2, run=.4),
    "three": dict(profile=[(0, AMBER_DEEP), (.2, AMBER), (.42, AMBER_HI), (.68, AMBER), (1, AMBER_DEEP)],
                  green=.62, cream=.42, run=.8),
    "iron": dict(profile=[(0, IRON_DEEP), (.22, IRON), (.42, col("D8794A")), (.7, IRON), (1, IRON_DEEP)],
                 green=.0, cream=.32, run=.7, accent=AMBER),
    "green": dict(profile=[(0, GREEN_DEEP), (.22, GREEN), (.42, GREEN_HI), (.7, GREEN), (1, GREEN_DEEP)],
                  green=.0, cream=.25, run=.6, accent=AMBER),
    "cream": dict(profile=[(0, AMBER_DEEP), (.18, CREAM_SH), (.45, CREAM_HI), (.75, CREAM_SH), (1, AMBER)],
                  green=.25, cream=.0, run=.3),
}


def rim_colour(u, X, Y, wtex, seed, scheme: str, pool=None, size=None):
    """Three-colour glaze on a rim. Streaks follow the rim (vertical on the sides, horizontal on top and bottom),
    as glaze runs along a tile edge; broad patches carry the second glaze; cream runs catch the light."""
    s = SCHEMES[scheme]
    rgb = ramp(u, s["profile"])
    if X.size == 0:
        return rgb
    if size is not None:
        w, h = size
        dv = np.minimum(X, w - X)
        dh = np.minimum(Y, h - Y)
        vertical = smoothstep(-5.0, 5.0, dh - dv)
    else:
        vertical = np.full(X.shape, .5, np.float32)
    streak = fbm(X, Y * .22, 5.0, seed + 3, 2) * vertical + fbm(X * .22, Y, 5.0, seed + 4, 2) * (1 - vertical)
    blot = fbm(X, Y, 17.0, seed, 3)
    second = s.get("accent", GREEN)
    second_prof = np.clip(ramp(u, [(0, second * .6), (.42, second * 1.1), (1, second * .72)]), 0, 1)
    amount = smoothstep(.5, .7, blot * .62 + streak * .5 * s["run"] + .08 * (1 - s["run"])) * s["green"]
    if pool is not None:
        amount = np.maximum(amount, pool)
    rgb = mix(rgb, second_prof, amount * wtex)
    streak2 = fbm(X, Y * .25, 4.0, seed + 8, 2) * vertical + fbm(X * .25, Y, 4.0, seed + 9, 2) * (1 - vertical)
    cream = smoothstep(.6, .78, streak2) * s["cream"]
    rgb = mix(rgb, CREAM_HI * .97, cream * wtex * smoothstep(.1, .4, u) * (1 - smoothstep(.8, 1, u)))
    return rgb


def apply_crackle(rgb, X, Y, amount, seed, scale: float = 1.0):
    if X.size == 0:
        return rgb
    lines = crazing(X, Y, scale, seed)
    dark = mix(rgb, rgb * col("8F6A48") * 1.15, lines * amount)
    return dark


# ---------------------------------------------------------------------------------------------- glazed tiles
def _face_sdf(X, Y, box, inner, radius, notch, rim, ear: float = 0.0):
    if notch:
        d = sd_round_box(X, Y, inner, 1.0)
        x0, y0, x1, y1 = (box[0] + ear, box[1] + ear, box[2] - ear, box[3] - ear)
        reach = notch + float(np.mean(rim))
        for cx, cy in ((x0, y0), (x1, y0), (x0, y1), (x1, y1)):
            d = np.maximum(d, np.float32(reach) - np.hypot(X - np.float32(cx), Y - np.float32(cy)))
        return d
    return sd_round_box(X, Y, inner, max(1.5, radius - min(rim) * .75))


def _band_render(r: Raster, w: int, h: int, depth: int, draw, fill):
    """Render only the four border bands (the centre is one flat colour or transparent by construction)."""
    D = depth
    if fill is not None:
        r.rgb[D * SS:(h - D) * SS, D * SS:(w - D) * SS] = np.asarray(fill, np.float32)
        r.a[D * SS:(h - D) * SS, D * SS:(w - D) * SS] = 1.0
    for x, y, ww, hh in ((0, 0, w, D), (0, h - D, w, D), (0, D, D, h - 2 * D), (w - D, D, D, h - 2 * D)):
        draw(r.window(x, y, ww, hh))


def glazed_tile(w: int, h: int, bands: int | None = None, **kw) -> Image.Image:
    r = Raster(w, h)
    if bands:
        ornaments = kw.pop("ornaments", ())
        _band_render(r, w, h, bands + 8, lambda win: draw_glazed(win, w, h, **kw), kw.get("fill"))
        for orn in ornaments:
            orn(r)
    else:
        draw_glazed(r, w, h, **kw)
    return r.export()


def draw_glazed(r: Raster, w: int, h: int, *, radius: float, rim, fill, scheme: str, seed: int, inset: float = 1.0,
                notch: float = 0.0, ear: float = 0.0, texture: str = "corners", crest: float | None = None,
                soft: float | None = None, height: float = 3.4, outline: float = 1.5, crackle_amt: float = .34,
                face_level: float = .5, sunken: bool = False, lip=None, corner=(20.0, 33.0),
                pool_corners: float = .9, drips: int = 0, ornaments=(), gloss: float = .7, face_crackle: float = 0.0,
                face_map=None, flow: float = 0.0, box=None):
    """Authored glazed surface (baked colour, shown with a white tint), drawn into r (which may be a window).

    rim = (left, top, right, bottom) widths in px. texture 'corners' keeps fine detail (crackle, streaks, pools) in
    the corner squares; 'full' textures the whole rim (cards, never stretched more than about 1.5x). `flow` adds
    long, soft runs of the second glaze along every edge: constant across the rim and low-frequency along it, so a
    stretched edge only gets longer runs. face_crackle with face_map(r) crazes the face (sprites never sliced)."""
    X, Y = r.X, r.Y
    box = box or (inset, inset, w - inset, h - inset)
    l, t, rr, b = rim
    inner = (box[0] + l, box[1] + t, box[2] - rr, box[3] - b)
    d_out = sd_notch_box(X, Y, box, notch, 0.0, ear) if notch else sd_round_box(X, Y, box, radius)
    d_in = _face_sdf(X, Y, box, inner, radius, notch, rim, ear)
    t_out = -d_out
    rim_w = min(rim)
    crest = crest or rim_w * .42
    soft = soft or rim_w * .55
    if sunken:
        # A well: the surface edge stays high and drops into the face; light catches the lower lip.
        drop = smoothstep(0.0, soft, d_in)
        H = (face_level + (1 - face_level) * drop).astype(np.float32)
        H = np.where(t_out < 0, 1.0, H).astype(np.float32)
    else:
        H = rim_height(t_out, d_in, crest, face_level, soft=soft)
    diffuse, spec = lighting(H, height, gloss=gloss, power=40.0)
    if texture == "corners":
        wtex = corner_weight(X, Y, w, h, *corner)
    else:
        wtex = np.ones_like(X)
    u = np.clip(t_out / np.maximum(t_out + np.maximum(d_in, 0), 1e-3), 0, 1)
    rim_zone = (t_out > -1.0) & (d_in > -1.5)
    base = ramp(u, SCHEMES[scheme]["profile"])
    if flow and rim_zone.any():
        sel = rim_zone
        xs, ys = X[sel], Y[sel]
        vertical = smoothstep(-5.0, 5.0, np.minimum(ys, h - ys) - np.minimum(xs, w - xs))
        # Sample the noise on the edge's centre line only, so the run is constant across the rim.
        lane_x = np.where(xs < w * .5, 0.0, 1.0).astype(np.float32) * 997.0
        lane_y = np.where(ys < h * .5, 0.0, 1.0).astype(np.float32) * 991.0
        along = fbm(lane_x, ys, 150.0, seed + 61, 2) * vertical + fbm(xs, lane_y, 150.0, seed + 62, 2) * (1 - vertical)
        amount = smoothstep(.5, .74, along) * flow
        second = SCHEMES[scheme].get("accent", GREEN)
        second_prof = np.clip(ramp(u[sel], [(0, second * .6), (.42, second * 1.1), (1, second * .72)]), 0, 1)
        base[sel] = mix(base[sel], second_prof, amount)
    tex = rim_zone & (wtex > 1e-6)
    if tex.any():
        pool = None
        if pool_corners:
            cw = corner_weight(X[tex], Y[tex], w, h, 6.0, 26.0) * pool_corners
            pool = cw * smoothstep(.35, .62, fbm(X[tex], Y[tex], 9.0, seed + 41, 2) + .25 * cw)
        base[tex] = rim_colour(u[tex], X[tex], Y[tex], wtex[tex], seed, scheme, pool, (w, h))
        if crackle_amt:
            base[tex] = apply_crackle(base[tex], X[tex], Y[tex], crackle_amt * wtex[tex], seed + 31)
    if drips and texture == "full":
        base = _drips(base, X, Y, t_out, d_in, w, drips, seed, scheme)
    if lip is not None:
        # A thin lighter lip just outside the face (concept: the cream bead inside the coloured rim).
        lip_col, lip_at, lip_w = lip
        band = np.exp(-((d_in - lip_at) / np.float32(lip_w)) ** 2) * (d_in > 0)
        base = mix(base, lip_col, band * .85)
    face_amt = coverage(d_in)
    if fill is not None:
        fill_rgb = np.asarray(fill, np.float32)
        if face_crackle:
            lines = face_map(r) if face_map is not None else crazing(X, Y, 1.6, seed + 77)
            face = mix(np.broadcast_to(fill_rgb, base.shape), fill_rgb * col("8F6A48") * 1.15, lines * face_crackle)
            base = base * (1 - face_amt)[..., None] + face * face_amt[..., None]
        else:
            base = mix(base, fill_rgb, face_amt)
    ol = 1 - smoothstep(outline * .45, outline, t_out)
    base = mix(base, OUTLINE, ol * .92)
    rim_only = (1 - face_amt)
    lit = base * diffuse[..., None] + (spec * rim_only)[..., None] * CREAM_HI
    lit = np.clip(lit, 0, 1)
    alpha = coverage(d_out)
    if fill is None:
        alpha = alpha * rim_only
    elif not face_crackle:
        flat = (d_in < -2.0) & (wtex < 1e-6)
        lit[flat] = np.asarray(fill, np.float32)
    r.over(lit, alpha)
    for orn in ornaments:
        orn(r)


def _drips(base, X, Y, t_out, d_in, w, count, seed, scheme):
    """Glaze runs hanging from the top rim toward the face (cards only; kept inside the border band)."""
    rng = np.random.RandomState(seed)
    second = SCHEMES[scheme].get("accent", GREEN)
    for i in range(count):
        cx = 60 + (w - 120) * (i + rng.uniform(.15, .85)) / count
        length = rng.uniform(6.0, 11.0)
        width = rng.uniform(2.2, 3.6)
        top = 4.0
        dx = (X - cx) / width
        prof = np.clip(1 - dx * dx, 0, 1)
        tip = top + length * np.sqrt(np.clip(prof, 0, 1))
        inside = (Y > top) & (Y < tip) & (np.abs(X - cx) < width) & (d_in > 0.6)
        if inside.any():
            colour = second if i % 2 == 0 else AMBER_DEEP
            amt = np.clip(prof * 1.2, 0, 1)
            base[inside] = mix(base[inside], colour, amt[inside] * .75)
    return base


def neutral_tile(w: int, h: int, *, ornaments=(), bands: int | None = None, **kw) -> Image.Image:
    r = Raster(w, h)
    if bands and not kw.get("flat"):
        _band_render(r, w, h, bands + 8, lambda win: draw_neutral(win, w, h, **kw),
                     None if kw.get("hollow") else WHITE)
    else:
        draw_neutral(r, w, h, **kw)
    for orn in ornaments:
        orn(r)
    return r.export()


def draw_neutral(r: Raster, w: int, h: int, *, radius: float, rim=(10, 10, 10, 10), inset: float = 1.0,
                 notch: float = 0.0, ear: float = 0.0, hollow: bool = False, crest: float | None = None,
                 soft: float | None = None, height: float = 3.0, outline: float = 1.5, outline_tone: float = .42,
                 inner_bead=None, flat: bool = False, sunken: bool = False):
    """Achromatic surface for sprites the game multiplies by a palette colour: white face, grey relief.

    The face is exactly white, so the drawn colour is exactly the tint and the label pick (centre pixel x tint)
    sees the palette value."""
    X, Y = r.X, r.Y
    box = (inset, inset, w - inset, h - inset)
    l, t, rr, b = rim
    inner = (inset + l, inset + t, w - inset - rr, h - inset - b)
    d_out = sd_notch_box(X, Y, box, notch, 0.0, ear) if notch else sd_round_box(X, Y, box, radius)
    alpha = coverage(d_out)
    if flat:
        r.over(WHITE, alpha)
        return
    d_in = _face_sdf(X, Y, box, inner, radius, notch, rim, ear)
    t_out = -d_out
    rim_w = min(rim)
    crest = crest or rim_w * .42
    soft = soft or rim_w * .55
    if sunken:
        drop = smoothstep(0.0, soft, d_in)
        H = np.where(t_out < 0, 1.0, .5 + .5 * drop).astype(np.float32)
    else:
        H = rim_height(t_out, d_in, crest, .5, soft=soft)
    if inner_bead is not None:
        at, bw, amp = inner_bead
        H = H + amp * np.exp(-((d_in + at) / np.float32(bw)) ** 2) * (d_in < 0)
    diffuse, _ = lighting(H, height, ambient=.5)
    u = np.clip(t_out / np.maximum(t_out + np.maximum(d_in, 0), 1e-3), 0, 1)
    tone = np.interp(u, [0, .2, .45, .8, 1], [.80, .9, 1.0, .9, .78]).astype(np.float32)
    face_amt = coverage(d_in)
    grey = tone * (1 - face_amt) + face_amt
    if inner_bead is not None:
        at, bw, amp = inner_bead
        grey = grey * (1 - .1 * np.exp(-((d_in + at + bw * 1.4) / np.float32(bw * .6)) ** 2) * (d_in < 0))
    grey = np.clip(grey * diffuse, 0, 1)
    ol = 1 - smoothstep(outline * .45, outline, t_out)
    grey = grey * (1 - ol) + outline_tone * ol
    if hollow:
        alpha = alpha * (1 - face_amt)
    else:
        face = (d_in < -2.0)
        if inner_bead is not None:
            face &= (d_in < -(inner_bead[0] + 4 * inner_bead[1]))
        grey[face] = 1.0
    r.over(np.repeat(grey[..., None], 3, axis=2), alpha)


# ---------------------------------------------------------------------------------------------- relief shapes
def relief(r: Raster, mask: np.ndarray, colour, *, bevel: float = 2.0, height: float = 2.6, ambient: float = .45,
           gloss: float = .6, outline: float = 0.0, outline_colour=OUTLINE, crackle_amt: float = 0.0, seed: int = 0,
           crackle_scale: float = 1.0, neutral: bool = False, shade_floor: float = 0.0):
    """Raised glaze on a binary SS mask: rounded edges, Lambert plus gloss; optional dark outline outside the shape.

    colour: an RGB triple, an array shaped like the mask, or a callable f(X, Y) -> (n, 3) evaluated only on the
    shape. Work is limited to the shape's bounding box."""
    ys, xs = np.nonzero(mask > 0)
    if ys.size == 0:
        return
    pad = int(math.ceil((outline + bevel * 1.5 + 4) * SS))
    y0, y1 = max(0, ys.min() - pad), min(mask.shape[0], ys.max() + pad + 1)
    x0, x1 = max(0, xs.min() - pad), min(mask.shape[1], xs.max() + pad + 1)
    y0, x0 = y0 - y0 % SS, x0 - x0 % SS
    y1, x1 = min(mask.shape[0], y1 + (-y1) % SS), min(mask.shape[1], x1 + (-x1) % SS)
    sub = r.window(x0 // SS, y0 // SS, (x1 - x0) // SS, (y1 - y0) // SS)
    m = mask[y0:y1, x0:x1]
    if outline > 0:
        ring = (outside_distance(m) < outline).astype(np.float32)
        sub.over(outline_colour, np.maximum(ring, m))
    dist = inside_distance(m)
    H = np.sin(np.clip(dist / np.float32(bevel), 0, 1) * np.float32(np.pi / 2)).astype(np.float32)
    # The distance field is stepped at the supersampled grid: soften it so the bead shades without striations.
    H = ndimage.gaussian_filter(H, sigma=max(1.0, min(bevel * SS * .18, 3.0))).astype(np.float32)
    diffuse, spec = lighting(H, height, ambient=ambient, gloss=gloss)
    sel = m > 0
    if callable(colour):
        c = np.zeros(m.shape + (3,), np.float32)
        c[sel] = colour(sub.X[sel], sub.Y[sel])
    else:
        c = np.asarray(colour, np.float32)
        c = np.broadcast_to(c, m.shape + (3,)).copy() if c.ndim == 1 else c[y0:y1, x0:x1].copy()
    if crackle_amt:
        c[sel] = apply_crackle(c[sel], sub.X[sel], sub.Y[sel], crackle_amt, seed, crackle_scale)
    if neutral:
        grey = np.clip(np.maximum(diffuse, shade_floor), 0, 1)
        lit = np.repeat(grey[..., None], 3, axis=2) * c
    else:
        lit = np.clip(c * diffuse[..., None] + spec[..., None] * CREAM_HI, 0, 1)
    sub.over(lit, m)


def stroke_mask(w: int, h: int, strokes, x0: float = 0.0, y0: float = 0.0) -> np.ndarray:
    """Variable-width round-capped strokes: strokes = [(xs, ys, widths), ...] in output pixels."""
    im = Image.new("L", (w * SS, h * SS), 0)
    d = ImageDraw.Draw(im)
    for xs, ys, ws in strokes:
        pts = [((x - x0) * SS, (y - y0) * SS) for x, y in zip(xs, ys)]
        for i in range(len(pts) - 1):
            wd = max(1, int(round((ws[i] + ws[i + 1]) * .5 * SS)))
            d.line([pts[i], pts[i + 1]], fill=255, width=wd)
        for (px, py), wv in zip(pts, ws):
            rad = wv * SS * .5
            d.ellipse((px - rad, py - rad, px + rad, py + rad), fill=255)
    return np.asarray(im, np.float32) / 255.0


def spiral(cx, cy, r0, r1, a0, turn, n=56):
    th = a0 + np.linspace(0.0, turn, n)
    rr = np.linspace(r0, r1, n)
    return cx + rr * np.cos(th), cy + rr * np.sin(th)


def cloud_scroll(cx: float, cy: float, s: float, flip: bool = False, width: float = .2, tail: float = 1.0,
                 small: bool = True):
    """A ruyi cloud scroll (xiangyun) as strokes: two spirals sitting on one base line that runs out into a
    tapering tail (concept pages: button feet, slot feet, tile corners). (cx, cy) is the centre of the large
    spiral, s its radius; the base line is at cy + s. flip mirrors left-right."""
    f = -1.0 if flip else 1.0
    strokes = []
    xs, ys = spiral(0.0, 0.0, 1.0, .16, math.radians(90), math.radians(90 + 470))
    strokes.append((xs, ys, np.linspace(width * 1.15, width * .62, len(xs))))
    left = -1.0
    if small:
        xs, ys = spiral(-1.5, .4, .6, .12, math.radians(90), math.radians(-440))
        strokes.append((xs, ys, np.linspace(width * 1.05, width * .58, len(xs))))
        left = -1.5
    tt = np.linspace(0.0, 1.0, 44)
    span = 1.0 + 1.9 * tail - left
    bx = left + tt * span
    knee = (0 - left) / span
    by = 1.0 - .34 * tail * np.clip((tt - knee) / max(1e-3, 1 - knee), 0, 1) ** 1.5
    bw = width * np.where(tt < knee, 1.12, 1.12 - 1.0 * (tt - knee) / max(1e-3, 1 - knee))
    strokes.append((bx, by, np.clip(bw, width * .16, None)))
    return [(cx + f * np.asarray(x) * s, cy + np.asarray(y) * s, np.asarray(wd) * s) for x, y, wd in strokes]


def quatrefoil_sdf(X, Y, cx, cy, r, lobe: float = .48):
    """Four-lobed (haitang) tile: four circles on the axes, joined by a rounded square."""
    a = r * lobe
    rad = r - a
    d = sd_round_box(X, Y, (cx - r * .78, cy - r * .78, cx + r * .78, cy + r * .78), r * .3)
    for ox, oy in ((a, 0), (-a, 0), (0, a), (0, -a)):
        d = np.minimum(d, sd_circle(X, Y, cx + ox, cy + oy, rad))
    return d


def sdf_mask(d) -> np.ndarray:
    return (d < 0).astype(np.float32)


def glaze_field(X, Y, base, second, seed, amount=.45, scale=14.0, vertical=True):
    """Body glaze for objects: base colour with runs of a second glaze (vertical streaks, as glaze flows down)."""
    sy = .3 if vertical else 1.0
    n = fbm(X, Y * sy, scale * .5, seed + 1, 2) * .55 + fbm(X, Y, scale, seed, 3) * .45
    t = smoothstep(.52, .7, n) * amount
    return mix(np.broadcast_to(np.asarray(base, np.float32), X.shape + (3,)), np.asarray(second, np.float32), t)


def glaze(base, second, seed, amount=.45, scale=14.0, vertical=True):
    """glaze_field as a colour callable for relief(): evaluated only on the shape's samples."""
    return lambda X, Y: glaze_field(X, Y, base, second, seed, amount, scale, vertical)


# ---------------------------------------------------------------------------------------------- ornaments
def corner_cloud(corner: str, s: float, w: int, h: int, ox: float, oy: float, colour=AMBER, *, neutral=False,
                 small=False, tail=.8, width=.27, outline=1.0, bevel=None, outline_colour=OUTLINE):
    """Ornament: a relief cloud scroll nestled in one corner ('tl', 'tr', 'bl', 'br'). Its base line sits oy px in
    from the horizontal edge and its spiral centre ox px in from the vertical edge; the tail runs along the edge."""
    right, bottom = corner[1] == "r", corner[0] == "b"
    cx = w - ox if right else ox
    base = h - oy if bottom else oy
    cy = base - s if bottom else base + s
    strokes = cloud_scroll(cx, cy, s, flip=right, width=width, tail=tail, small=small)
    if not bottom:
        strokes = [(xs, 2 * cy - ys, ws) for xs, ys, ws in strokes]
    xs = np.concatenate([st[0] for st in strokes])
    ys = np.concatenate([st[1] for st in strokes])
    pad = s * width + outline + 4

    def draw(r: Raster):
        win = r.window_abs(xs.min() - pad, ys.min() - pad, xs.max() - xs.min() + 2 * pad, ys.max() - ys.min() + 2 * pad)
        if win is None:
            return
        m = win.strokes(strokes)
        relief(win, m, WHITE if neutral else colour, bevel=bevel or max(.9, s * width * .55), height=2.2,
               outline=outline, outline_colour=(np.full(3, .55, np.float32) if neutral else outline_colour),
               neutral=neutral, shade_floor=.55 if neutral else 0.0)
    return draw


def _clouds(w, h, s, ox, oy, corners=("bl", "br"), colour=AMBER, **kw):
    return [corner_cloud(c, s, w, h, ox, oy, colour, **kw) for c in corners]


# ---------------------------------------------------------------------------------------------- sliced surfaces
def _card(scheme: str, seed: int, rim=(22, 22, 22, 26), green_pool: float = .9, drips: int = 6) -> Image.Image:
    return glazed_tile(512, 512, bands=40, radius=26, rim=rim, fill=CREAM, scheme=scheme, seed=seed, texture="full",
                       drips=drips, lip=(CREAM_HI, 2.2, 1.0), pool_corners=green_pool, crackle_amt=.38,
                       ornaments=_clouds(512, 512, 8.0, 15, 10, tail=.7))


def _dark_panel(fill, seed: int, rim=(18, 18, 18, 20), clouds=("br",), radius=20.0, scheme="amber_cream"):
    return glazed_tile(512, 512, bands=40, radius=radius, rim=rim, fill=fill, scheme=scheme, seed=seed, flow=.75,
                       lip=(GREEN_DEEP * .8, 1.6, .9), ornaments=_clouds(512, 512, 7.5, 14, 9, clouds))


SCHEMES["well"] = dict(profile=[(0, AMBER_HI * .92), (.14, AMBER), (.3, GREEN_DEEP * .9), (1, col("1B2610"))],
                       green=.0, cream=.0, run=.3)
SCHEMES["slot"] = dict(profile=[(0, CREAM_HI * .95), (.16, AMBER_HI), (.3, AMBER_DEEP), (.5, col("2A1D0B")), (1, col("1E1508"))],
                       green=.3, cream=.0, run=.3)

INSET_FILL = col("26331A")
SECTION_FILL = col("2B3C18")
SLOT_FILL = col("33240F")
LABEL_FILL = col("F7E9CB")
TRAY_FRONT_FILL = col("2E2410")


def surface(spec: dict) -> Image.Image:
    """Nine-sliced and horizontally sliced surfaces. Frames stay in the border bands, centres stay flat."""
    name = spec["name"]
    w, h = spec["size"]
    if name == "ui_paper_panel":
        return glazed_tile(w, h, bands=40, radius=22, rim=(16, 16, 16, 18), fill=CREAM, scheme="amber", seed=101,
                           flow=.7, lip=(CREAM_HI, 2.0, 1.0), ornaments=_clouds(w, h, 7.5, 14, 9))
    if name in ("ui_card_controller", "ui_card_controller_compact"):
        return _card("three", 111)
    if name == "ui_card_item":
        return _card("iron", 121)
    if name == "ui_card_shop":
        return _card("amber", 131, rim=(18, 18, 18, 22), green_pool=1.0, drips=4)
    if name == "ui_chip_label":
        return glazed_tile(w, h, bands=40, radius=34, rim=(9, 9, 9, 10), fill=LABEL_FILL, scheme="amber", seed=141,
                           crackle_amt=.25, flow=.5, lip=(CREAM_HI, 1.4, .8))
    if name == "ui_panel_hud":
        return _dark_panel(PANEL, 151)
    if name == "ui_section_panel":
        # Shop sections draw a header strip from 5 units in: the top rim stays inside 10 px.
        return _dark_panel(SECTION_FILL, 161, rim=(14, 9, 14, 16), clouds=("bl", "br"), radius=16)
    if name == "ui_tab_bottom":
        return _dark_panel(PANEL, 171, rim=(14, 12, 14, 14), clouds=("bl",), radius=18)
    if name == "ui_info_inset":
        return glazed_tile(w, h, bands=40, radius=16, rim=(9, 9, 9, 9), fill=INSET_FILL, scheme="well", seed=181,
                           sunken=True, crackle_amt=.0, pool_corners=0, outline=1.0, height=2.6)
    if name == "ui_slot_inventory":
        return glazed_tile(w, h, bands=40, radius=0, notch=15, ear=3, rim=(10, 10, 10, 10), fill=SLOT_FILL,
                           scheme="slot", seed=191, sunken=True, crackle_amt=.15, pool_corners=.6, outline=1.2,
                           height=2.8)
    if name == "ui_tray_front":
        return tray_front(w, h)
    # Achromatic, tinted by the game.
    if name == "ui_topbar":
        return neutral_tile(w, h, bands=40, radius=24, rim=(12, 12, 12, 16))
    if name == "ui_chip_formula":
        return neutral_tile(w, h, bands=40, radius=26, rim=(8, 8, 8, 10), crest=3.4, height=2.6)
    if name == "ui_stage_card":
        return neutral_tile(w, h, bands=48, radius=26, rim=(15, 15, 15, 17), inner_bead=(5.0, 1.1, .45),
                            ornaments=_clouds(w, h, 7.0, 16, 9, neutral=True))
    if name == "ui_dice_tray":
        # Tonal clouds ride on the top and bottom rim just past each notch (the notch itself is open).
        orn = [corner_cloud(c, 7.0, w, h, 40, 4, neutral=True) for c in ("tl", "tr", "bl", "br")]
        return neutral_tile(w, h, bands=72, radius=0, notch=20, ear=4, rim=(22, 22, 22, 22), hollow=True,
                            ornaments=orn, crest=8.0, height=3.4)
    if name == "ui_button_bevel":
        return neutral_tile(w, h, bands=40, radius=28, rim=(11, 10, 11, 14))
    if name == "ui_button_ghost":
        return neutral_tile(w, h, bands=40, radius=28, rim=(10, 10, 10, 10), hollow=True)
    if name == "ui_ring":
        return neutral_tile(w, h, bands=40, radius=0, notch=17, ear=3, rim=(9, 9, 9, 9), hollow=True, crest=3.8)
    if name == "ui_frame_enchant":
        # Sits on the cream face just inside the card rim (cards: 22 px rim), so every enchantment hue reads on cream
        # instead of vanishing into the amber glaze.
        return neutral_tile(w, h, bands=40, radius=0, notch=6, ear=2, inset=20, rim=(5, 5, 5, 5), hollow=True,
                            crest=2.2, outline=.8)
    if name == "ui_frame_block":
        return neutral_tile(w, h, radius=16, flat=True, inset=.5)
    if name == "ui_price_chip":
        return neutral_tile(w, h, bands=40, radius=24, rim=(8, 8, 8, 10), inner_bead=(4.0, 1.0, .35), crest=3.4,
                            height=2.6)
    if name == "ui_progress_fill":
        return progress_bar(w, h, fill=True)
    if name == "ui_progress_track":
        return progress_bar(w, h, fill=False)
    if name == "ui_section_header":
        return section_header(w, h)
    if name == "ui_card_frame_rarity":
        return card_frame_rarity(w, h)
    raise ValueError("Unimplemented sliced sprite: " + name)


def _aniso_coverage(d, X):
    """Coverage for a distance measured in anisotropic display units: rescale by the local gradient."""
    gy, gx = np.gradient(d, np.float32(1.0 / SS))
    g = np.maximum(np.sqrt(gx * gx + gy * gy), 1e-3)
    return coverage(d / g)


def _aniso_lighting(H, sx: float, sy: float, height: float, **kw):
    """Lighting for art the game squashes: slopes measured in display units (sx, sy = display units per px)."""
    gy, gx = np.gradient(H.astype(np.float32), np.float32(1.0 / SS))
    # Rebuild a height field whose finite differences equal the display-space slopes, then light it.
    nx = -(gx / np.float32(sx)) * np.float32(height)
    ny = -(gy / np.float32(sy)) * np.float32(height)
    inv = 1.0 / np.sqrt(nx * nx + ny * ny + 1.0)
    ndl = (nx * LIGHT[0] + ny * LIGHT[1] + LIGHT[2]) * inv
    ambient = kw.get("ambient", .45)
    diffuse = ambient + (1 - ambient) * np.clip(ndl / LIGHT[2], 0, None)
    ndh = np.clip((nx * HALF[0] + ny * HALF[1] + HALF[2]) * inv, 0, 1)
    flat = HALF[2] ** 40
    spec = kw.get("gloss", .6) * np.clip((ndh ** 40 - flat) / (1 - flat), 0, 1)
    return diffuse.astype(np.float32), spec.astype(np.float32)


def tray_front(w: int, h: int) -> Image.Image:
    """Front lip of a locked-die slot (border 40/0/40/0). Play11 draws it 20 units tall, so the art is designed in
    display units (x 0.5, y 0.125 units per px): a rounded glazed lip with an amber bead along its top."""
    r = Raster(w, h)
    sx, sy = .5, 20.0 / h
    Xu, Yu = r.X * sx, r.Y * sy
    tall = h * sy
    d = sd_round_box(Xu, Yu, (.4, .3, w * sx - .4, tall - .3), tall * .5 - .3)
    t = -d
    H = np.sin(np.clip(t / 3.2, 0, 1) * np.pi / 2).astype(np.float32)
    bead = np.exp(-((Yu - 3.2) / 1.4) ** 2)
    H = H + .45 * bead
    diffuse, spec = _aniso_lighting(H, sx, sy, 2.6, gloss=.7)
    v = np.clip(Yu / tall, 0, 1)
    base = ramp(v, [(0, AMBER_HI), (.16, AMBER), (.3, AMBER_DEEP * .8), (.42, TRAY_FRONT_FILL), (.85, TRAY_FRONT_FILL),
                    (1, col("1C1408"))])
    ol = 1 - smoothstep(.25, .6, t)
    base = mix(base, OUTLINE, ol)
    lit = np.clip(base * diffuse[..., None] + spec[..., None] * CREAM_HI, 0, 1)
    r.over(lit, _aniso_coverage(d, r.X))
    return r.export()


def progress_bar(w: int, h: int, fill: bool) -> Image.Image:
    """Glossy glaze bar (fill) or sunken groove (track); semicircular caps in the 48 px end borders."""
    r = Raster(w, h)
    # Ends rounded inside the 48 px end borders with a 5 px straight lead-in, so the stretched middle (and the
    # Lanczos footprint of its first columns) never sees the curve.
    R = 42.0
    d = sd_round_box(r.X, r.Y, (1.0, 1.0, w - 1.0, h - 1.0), R)
    t = -d
    if fill:
        H = np.sqrt(np.clip(1 - (1 - np.clip(t / R, 0, 1)) ** 2, 0, 1)).astype(np.float32)
        diffuse, _ = lighting(H, 7.0, ambient=.55)
        v = np.clip(r.Y / h, 0, 1)
        tone = np.interp(v, [0, .18, .32, .55, .85, 1], [.86, 1.0, 1.0, .9, .78, .7]).astype(np.float32)
        grey = np.clip(tone * np.minimum(diffuse, 1.0), 0, 1)
    else:
        H = (1 - np.sin(np.clip(t / (R * .55), 0, 1) * np.pi / 2)).astype(np.float32)
        diffuse, _ = lighting(H, 6.0, ambient=.5)
        grey = np.clip(.86 * np.minimum(diffuse, 1.1), 0, 1)
    ol = 1 - smoothstep(.7, 1.6, t)
    grey = grey * (1 - ol) + .45 * ol
    r.over(np.repeat(grey[..., None], 3, axis=2), coverage(d))
    return r.export()


def section_header(w: int, h: int) -> Image.Image:
    """Shop section title band (border 40/0/72/0, drawn 40 units tall): a glazed band whose right end rolls up into
    a scroll curl. Designed in display units (0.5 x, 0.3125 y units per px) because only the height is scaled."""
    r = Raster(w, h)
    sx, sy = .5, 40.0 / h
    Xu, Yu = r.X * sx, r.Y * sy
    W = w * sx
    # Left end: radius 17 units inside the 20-unit end border (straight from 17.5 units = 35 px). Right end: a full
    # semicircle rolled into a scroll, all of it beyond 222 units (444 px), clear of the 440 px border line.
    cx, cy = W - 20.0, 20.0
    band = np.maximum(sd_round_box(Xu, Yu, (.5, .5, cx + 40.0, 39.5), 17.0), Xu - cx)
    d = np.minimum(band, sd_circle(Xu, Yu, cx, cy, 19.5))
    t = -d
    H = np.sin(np.clip(t / 5.0, 0, 1) * np.pi / 2).astype(np.float32)
    ang = np.arctan2(Yu - cy, Xu - cx)
    rad = np.hypot(Xu - cx, Yu - cy)
    turn = (ang + np.pi) / (2 * np.pi)
    groove = np.zeros_like(rad)
    for k in range(3):
        ring_r = 11.0 - 3.6 * (turn + k)
        groove = np.maximum(groove, np.exp(-((rad - ring_r) / .9) ** 2) * (ring_r > 1.2))
    groove *= (Xu >= 223.0)
    H = H - .55 * groove
    diffuse, _ = _aniso_lighting(H, sx, sy, 2.4)
    v = np.clip(Yu / 40.0, 0, 1)
    tone = np.interp(v, [0, .12, .3, .7, 1], [.8, 1.0, 1.0, .9, .74]).astype(np.float32)
    grey = np.clip(tone * np.minimum(diffuse, 1.0) * (1 - .25 * groove), 0, 1)
    ol = 1 - smoothstep(.3, .75, t)
    grey = grey * (1 - ol) + .42 * ol
    r.over(np.repeat(grey[..., None], 3, axis=2), _aniso_coverage(d, r.X))
    return r.export()


# ---------------------------------------------------------------------------------------------- icons and marks
def _ell(d, s, cx, cy, rx, ry=None, fill=255):
    ry = rx if ry is None else ry
    d.ellipse([c for p in s([(cx - rx, cy - ry), (cx + rx, cy + ry)]) for c in p], fill=fill)


def _rrect(d, s, box, rad, fill=255):
    pts = s([(box[0], box[1]), (box[2], box[3])])
    d.rounded_rectangle([pts[0][0], pts[0][1], pts[1][0], pts[1][1]], radius=rad * SS, fill=fill)


def _poly(d, s, pts, fill=255):
    d.polygon(s(pts), fill=fill)


def _line(d, s, pts, width, fill=255):
    d.line(s(pts), fill=fill, width=max(1, int(round(width * SS))), joint="curve")
    for x, y in (pts[0], pts[-1]):
        _ell(d, s, x, y, width / 2, fill=fill)


def die_faces(cx, cy, size):
    """Three visible faces of a cube in a three-quarter view, as polygons (top, left, right)."""
    a = size * .5
    top = [(cx, cy - a * 1.0), (cx + a, cy - a * .5), (cx, cy), (cx - a, cy - a * .5)]
    left = [(cx - a, cy - a * .5), (cx, cy), (cx, cy + a * 1.1), (cx - a, cy + a * .6)]
    right = [(cx, cy), (cx + a, cy - a * .5), (cx + a, cy + a * .6), (cx, cy + a * 1.1)]
    return top, left, right


def draw_die(r: Raster, cx, cy, size, pips=(1, 2, 3), colours=(IRON, COBALT, COBALT), outline=1.6):
    """A cream-glazed die in three-quarter view with glossy pips (light from the upper left)."""
    top, left, right = die_faces(cx, cy, size)
    whole = r.mask(lambda d, s: [_poly(d, s, top), _poly(d, s, left), _poly(d, s, right)])
    if outline:
        ring = (outside_distance(whole) < outline).astype(np.float32)
        r.over(OUTLINE, np.maximum(ring, whole))
    for face, tone in ((top, CREAM_HI), (left, CREAM), (right, CREAM_SH)):
        m = r.mask(lambda d, s, f=face: _poly(d, s, f))
        relief(r, m, tone, bevel=size * .07, height=1.8, gloss=.5)
    a = size * .5

    def pip(face, u, v, colour):
        p0, p1, p2, p3 = face
        x = p0[0] + (p1[0] - p0[0]) * u + (p3[0] - p0[0]) * v
        y = p0[1] + (p1[1] - p0[1]) * u + (p3[1] - p0[1]) * v
        rr = size * .075
        m = r.mask(lambda d, s: _ell(d, s, x, y, rr, rr * .8))
        relief(r, m, colour, bevel=rr * .7, height=-1.6, gloss=.8)

    layouts = {1: [(.5, .5)], 2: [(.28, .28), (.72, .72)], 3: [(.25, .25), (.5, .5), (.75, .75)],
               4: [(.28, .28), (.72, .28), (.28, .72), (.72, .72)],
               5: [(.26, .26), (.74, .26), (.5, .5), (.26, .74), (.74, .74)],
               6: [(.28, .22), (.28, .5), (.28, .78), (.72, .22), (.72, .5), (.72, .78)]}
    for face, n, colour in zip((top, left, right), pips, colours):
        for u, v in layouts[n]:
            pip(face, u, v, colour)


def icon(spec: dict) -> Image.Image:
    name = spec["name"]
    w, h = spec["size"]
    r = Raster(w, h)
    if name == "ui_icon_coin":
        # A square-holed cash coin in amber glaze (concept: select page coins), dark rim for the amber price chip.
        body = r.mask(lambda d, s: _ell(d, s, 48, 48, 41))
        relief(r, body, AMBER, bevel=5.5, height=3.2, outline=3.6, outline_colour=UMBER_DEEP, gloss=.75)
        field = r.mask(lambda d, s: _ell(d, s, 48, 48, 30.5))
        relief(r, field, AMBER_DEEP * 1.12, bevel=2.2, height=-2.2, gloss=.4)
        frame = r.mask(lambda d, s: _rrect(d, s, (31.5, 31.5, 64.5, 64.5), 3.5))
        relief(r, frame, AMBER_HI, bevel=2.4, height=2.6, gloss=.8)
        hole = r.mask(lambda d, s: _rrect(d, s, (37.0, 37.0, 59.0, 59.0), 2.0))
        r.over(UMBER_DEEP, hole)
        return r.export()
    if name == "ui_icon_roll":
        # Tang cup in three-colour glaze with a die resting in its mouth (the roll budget, 36 units on dark green).
        cup = r.mask(lambda d, s: [_poly(d, s, [(40, 96), (216, 96), (184, 206), (72, 206)]),
                                   _ell(d, s, 128, 206, 56, 16), _ell(d, s, 128, 96, 88, 26)])
        foot = r.mask(lambda d, s: [_rrect(d, s, (90, 206, 166, 232), 8)])
        whole = np.maximum(cup, foot)
        ring = (outside_distance(whole) < 8.0).astype(np.float32)
        r.over(UMBER_DEEP, np.maximum(ring, whole))
        relief(r, foot, AMBER_DEEP, bevel=6, height=2.5)
        body = glaze(AMBER, GREEN, 211, amount=.45, scale=40)   # amber-led: it sits on dark green wells
        relief(r, cup, body, bevel=16, height=3.2, gloss=.75)
        rim_all = r.mask(lambda d, s: _ell(d, s, 128, 96, 82, 22))
        mouth = r.mask(lambda d, s: _ell(d, s, 128, 98, 66, 15))
        lip = rim_all * (1 - mouth)
        relief(r, lip, CREAM_HI, bevel=5, height=2.2)
        r.over(col("2A1A0C"), mouth)
        # The die rests in the mouth: drawn over the far lip, then the near lip is drawn over the die.
        die = r.mask(lambda d, s: _poly(d, s, [(128, 30), (180, 58), (128, 88), (76, 58)]))
        r.over(UMBER_DEEP, (outside_distance(die) < 5.0).astype(np.float32))
        relief(r, die, CREAM_HI, bevel=6, height=2.2)
        pip = r.mask(lambda d, s: _ell(d, s, 128, 58, 12, 7.5))
        relief(r, pip, IRON, bevel=4, height=-1.5, gloss=.8)
        near = lip * (r.Y > 97.0)
        relief(r, near, CREAM_HI, bevel=5, height=2.2)
        return r.export()
    if name == "ui_icon_dice_view":
        # Glazed die and an amber-ringed lens: reads on the dark chip (40 units) and on paper (128 units).
        draw_die(r, 52, 58, 76, outline=3.2)
        handle = r.mask(lambda d, s: _line(d, s, [(98, 98), (119, 119)], 13))
        ring = r.mask(lambda d, s: _ell(d, s, 84, 84, 26))
        whole = np.maximum(handle, ring)
        r.over(UMBER_DEEP, (outside_distance(whole) < 3.2).astype(np.float32))
        relief(r, handle, col("6B3A18"), bevel=4, height=2.4)
        relief(r, ring, AMBER, bevel=5, height=3.0, gloss=.8)
        lens = r.mask(lambda d, s: _ell(d, s, 84, 84, 16.5))
        r.over(col("DCE6EA"), lens * .92)
        glint = r.mask(lambda d, s: _ell(d, s, 78, 77, 6, 4))
        r.over(WHITE, glint * .85)
        return r.export()
    if name == "ui_icon_lock":
        # Amber-bronze padlock; at 20-22 units on the dark Background its glaze body is the carrier.
        shackle = r.mask(lambda d, s: d.arc([c for p in s([(70, 34), (186, 150)]) for c in p], 180, 360, fill=255,
                                           width=int(30 * SS)))
        legs = r.mask(lambda d, s: [_rrect(d, s, (70, 90, 100, 140), 6), _rrect(d, s, (156, 90, 186, 140), 6)])
        shackle = np.maximum(shackle, legs)
        body = r.mask(lambda d, s: _rrect(d, s, (44, 114, 212, 236), 28))
        whole = np.maximum(shackle, body)
        r.over(UMBER_DEEP, (outside_distance(whole) < 9.0).astype(np.float32))
        relief(r, shackle, col("C7832E"), bevel=12, height=3.0, gloss=.8)
        relief(r, body, glaze(AMBER, AMBER_HI, 231, .5, 50), bevel=20, height=3.0, gloss=.8)
        key = r.mask(lambda d, s: [_ell(d, s, 128, 164, 18), _poly(d, s, [(116, 170), (140, 170), (148, 212), (108, 212)])])
        relief(r, key, UMBER_DEEP, bevel=4, height=-2.0)
        return r.export()
    if name in ("ui_tag_lock", "ui_tag_clock"):
        tile = sdf_mask(quatrefoil_sdf(r.X, r.Y, 64, 64, 60))
        r.over(UMBER_DEEP, (outside_distance(tile) < 3.0).astype(np.float32))
        rim = AMBER if name == "ui_tag_lock" else GREEN
        relief(r, tile, rim, bevel=9, height=3.0, gloss=.75)
        face = sdf_mask(quatrefoil_sdf(r.X, r.Y, 64, 64, 48))
        relief(r, face, CREAM_HI if name == "ui_tag_lock" else CREAM, bevel=3, height=-1.8, gloss=.4)
        if name == "ui_tag_lock":
            sh = r.mask(lambda d, s: [d.arc([c for p in s([(47, 30), (81, 66)]) for c in p], 180, 360, fill=255,
                                            width=int(9 * SS)), _rrect(d, s, (47, 46, 56, 62), 2), _rrect(d, s, (72, 46, 81, 62), 2)])
            relief(r, sh, UMBER, bevel=3, height=2.0)
            body = r.mask(lambda d, s: _rrect(d, s, (38, 56, 90, 96), 9))
            relief(r, body, UMBER, bevel=5, height=2.2, gloss=.6)
            key = r.mask(lambda d, s: [_ell(d, s, 64, 72, 6), _rrect(d, s, (61, 72, 67, 86), 2)])
            r.over(AMBER_HI, key)
        else:
            face_ring = r.mask(lambda d, s: d.ellipse([c for p in s([(34, 34), (94, 94)]) for c in p], outline=255,
                                                      width=int(7 * SS)))
            relief(r, face_ring, UMBER, bevel=2.5, height=2.0)
            hands = r.mask(lambda d, s: [_line(d, s, [(64, 64), (64, 42)], 6), _line(d, s, [(64, 64), (80, 72)], 6)])
            relief(r, hands, UMBER_DEEP, bevel=2.0, height=1.8)
            hub = r.mask(lambda d, s: _ell(d, s, 64, 64, 5))
            r.over(IRON, hub)
        return r.export()
    if name == "ui_icon_arrow":
        m = r.mask(lambda d, s: [_line(d, s, [(36, 22), (66, 48), (36, 74)], 15), _line(d, s, [(16, 48), (60, 48)], 13)])
        relief(r, m, WHITE, bevel=3.0, height=2.0, neutral=True, shade_floor=.62)
        return r.export()
    if name == "ui_icon_book":
        pages = r.mask(lambda d, s: [_poly(d, s, [(9, 22), (45, 27), (45, 78), (9, 73)]),
                                     _poly(d, s, [(51, 27), (87, 22), (87, 73), (51, 78)]),
                                     _rrect(d, s, (8, 76, 88, 84), 3)])
        lines = r.mask(lambda d, s: [_line(d, s, [(16, y), (38, y + 3)], 3.4) for y in (37, 49, 61)] +
                       [_line(d, s, [(58, y + 3), (80, y)], 3.4) for y in (37, 49, 61)])
        relief(r, pages * (1 - lines), WHITE, bevel=2.2, height=1.6, neutral=True, shade_floor=.62)
        return r.export()
    if name == "ui_icon_gear":
        th = np.linspace(0, 2 * np.pi, 361)[:-1]
        tooth = np.clip(np.cos(8 * th) * 1.9, -1, 1)
        rad = 31 + 8.5 * (tooth + 1) / 2
        pts = [(48 + rr * math.cos(t), 48 + rr * math.sin(t)) for rr, t in zip(rad, th)]
        gear = r.mask(lambda d, s: _poly(d, s, pts))
        hole = r.mask(lambda d, s: _ell(d, s, 48, 48, 12))
        relief(r, gear * (1 - hole), WHITE, bevel=3.0, height=1.8, neutral=True, shade_floor=.62)
        return r.export()
    if name == "ui_icon_refresh":
        # A rolled clay coil turning anticlockwise; the head sits on the coil's open end and points along it.
        R, start = 74.0, -25.0
        arc = r.mask(lambda d, s: d.arc([c for p in s([(128 - R - 16, 128 - R - 16), (128 + R + 16, 128 + R + 16)]) for c in p],
                                        start, 245, fill=255, width=int(32 * SS)))
        th = math.radians(start)
        ex, ey = 128 + R * math.cos(th), 128 + R * math.sin(th)
        tx, ty = math.sin(th), -math.cos(th)          # anticlockwise tangent (screen y points down)
        nx, ny = math.cos(th), math.sin(th)
        head_pts = [(ex + tx * 58, ey + ty * 58), (ex + nx * 40, ey + ny * 40), (ex - nx * 40, ey - ny * 40)]
        head = r.mask(lambda d, s: _poly(d, s, head_pts))
        relief(r, np.maximum(arc, head), WHITE, bevel=8, height=2.2, neutral=True, shade_floor=.62)
        return r.export()
    if name == "ui_pip_dot":
        m = r.mask(lambda d, s: _ell(d, s, 32, 32, 27.5))
        relief(r, m, WHITE, bevel=7, height=1.6, neutral=True, shade_floor=.72)
        return r.export()
    if name == "fx_score_spark":
        pts = []
        for k in range(8):
            a = k * math.pi / 4 - math.pi / 2
            rr = 29 if k % 2 == 0 else 7.5
            pts.append((32 + rr * math.cos(a), 32 + rr * math.sin(a)))
        m = r.mask(lambda d, s: _poly(d, s, pts))
        relief(r, m, WHITE, bevel=3, height=1.5, neutral=True, shade_floor=.7)
        return r.export()
    if name == "fx_square_chip":
        ang = math.radians(6)
        xr = (r.X - 128) * math.cos(ang) + (r.Y - 128) * math.sin(ang)
        yr = -(r.X - 128) * math.sin(ang) + (r.Y - 128) * math.cos(ang)
        m = sdf_mask(sd_round_box(xr, yr, (-58, -58, 58, 58), 16))
        relief(r, m, WHITE, bevel=10, height=1.6, neutral=True, shade_floor=.7)
        return r.export()
    if name == "fx_arc_short":
        tt = np.linspace(0, 1, 60)
        xs = 46 + 150 * tt + 10 * np.sin(tt * np.pi)
        ys = 206 - 150 * np.sin(tt * np.pi * .5) - 10 * tt
        ws = 22 * (1 - .7 * tt)
        cx, cy = xs[-1] - 4, ys[-1] + 18
        sx_, sy_ = spiral(cx, cy, 18, 4, math.radians(-90), math.radians(300), 30)
        strokes = [(xs, ys, ws), (sx_, sy_, np.linspace(ws[-1], 4.5, 30))]
        m = r.strokes(strokes)
        relief(r, m, WHITE, bevel=5, height=1.8, neutral=True, shade_floor=.68)
        return r.export()
    if name == "fx_score_crown":
        # A cloud crown: three rounded peaks on an amber band, a cobalt jewel (48 x 32 units on the dark top bar).
        body = r.mask(lambda d, s: [_poly(d, s, [(26, 104), (34, 58), (62, 82), (96, 30), (130, 82), (158, 58), (166, 104)]),
                                    _ell(d, s, 34, 54, 13), _ell(d, s, 96, 26, 16), _ell(d, s, 158, 54, 13),
                                    _rrect(d, s, (22, 88, 170, 116), 10)])
        r.over(UMBER_DEEP, (outside_distance(body) < 4.5).astype(np.float32))
        relief(r, body, glaze(AMBER, AMBER_HI, 241, .6, 30), bevel=8, height=3.0, gloss=.85)
        band = r.mask(lambda d, s: _rrect(d, s, (28, 92, 164, 112), 7))
        relief(r, band, AMBER_DEEP * 1.1, bevel=4, height=2.0, gloss=.6)
        jewel = r.mask(lambda d, s: _ell(d, s, 96, 101, 9))
        relief(r, jewel, COBALT, bevel=4, height=2.6, gloss=.9)
        for x in (52, 140):
            pearl = r.mask(lambda d, s, xx=x: _ell(d, s, xx, 101, 5))
            relief(r, pearl, CREAM_HI, bevel=3, height=2.0)
        return r.export()
    if name == "ui_pedestal":
        # Jade-green glazed plinth under the shop's dice (160 x 80 units on a cream card).
        shadow = r.mask(lambda d, s: _ell(d, s, 192, 150, 178, 30))
        r.over(UMBER_DEEP, ndimage.gaussian_filter(shadow, 6 * SS) * .45)
        side = r.mask(lambda d, s: [_ell(d, s, 192, 118, 166, 44), _rrect(d, s, (26, 76, 358, 118), 2)])
        r.over(UMBER_DEEP, (outside_distance(side) < 2.4).astype(np.float32))
        relief(r, side, glaze(GREEN_DEEP * 1.05, AMBER_DEEP, 251, .55, 22), bevel=10, height=2.4)
        top = r.mask(lambda d, s: _ell(d, s, 192, 76, 166, 44))
        relief(r, top, glaze(GREEN, GREEN_HI, 252, .5, 60, vertical=False), bevel=14, height=2.8,
               gloss=.8, crackle_amt=.25, seed=253, crackle_scale=1.4)
        lip = r.mask(lambda d, s: d.ellipse([c for p in s([(50, 42), (334, 110)]) for c in p], outline=255, width=int(5 * SS)))
        relief(r, lip, CREAM_HI * .95, bevel=2, height=1.6)
        return r.export()
    if name == "ui_die_shadow":
        rr = np.sqrt(((r.X - 128) / 112) ** 2 + ((r.Y - 66) / 42) ** 2)
        a = .44 * (1 - smoothstep(.45, 1.0, rr))
        r.over(UMBER_DEEP * .7, a)
        return r.export()
    raise ValueError("Unimplemented icon: " + name)


BADGE_GLAZE = {
    "badge_enchant_base10": (GREEN, GREEN_HI), "badge_enchant_mult2": (AMBER, AMBER_HI),
    "badge_enchant_roll1": (COBALT, COBALT_HI), "badge_enchant_slotless": (AUBERGINE, col("9A6AA6")),
    "badge_enchant_submit1": (CREAM, CREAM_HI),
}


def badge(name: str) -> Image.Image:
    """Enchantment badge: a haitang (quatrefoil) glazed tile with a raised symbol; reads at 28-32 units."""
    r = Raster(256, 256)
    body, light = BADGE_GLAZE[name]
    tile = sdf_mask(quatrefoil_sdf(r.X, r.Y, 128, 128, 116))
    r.over(UMBER_DEEP, (outside_distance(tile) < 6.0).astype(np.float32))
    rim = AMBER if name != "badge_enchant_mult2" else GREEN
    if name == "badge_enchant_submit1":
        rim = GREEN
    relief(r, tile, rim, bevel=16, height=3.2, gloss=.8)
    face = sdf_mask(quatrefoil_sdf(r.X, r.Y, 128, 128, 94))
    relief(r, face, glaze(body, light, 261, .35, 60), bevel=6, height=-2.0, gloss=.5)
    ink, edge = CREAM_HI, UMBER_DEEP
    if name == "badge_enchant_mult2":
        ink, edge = UMBER_DEEP, CREAM_HI * .9
    if name == "badge_enchant_base10":
        sym = r.mask(lambda d, s: [_line(d, s, [(76, 128), (180, 128)], 34), _line(d, s, [(128, 76), (128, 180)], 34)])
    elif name == "badge_enchant_mult2":
        sym = r.mask(lambda d, s: [_line(d, s, [(86, 86), (170, 170)], 32), _line(d, s, [(170, 86), (86, 170)], 32)])
    elif name == "badge_enchant_roll1":
        sym = r.mask(lambda d, s: [_poly(d, s, [(80, 92), (176, 92), (160, 178), (96, 178)]), _ell(d, s, 128, 92, 48, 14)])
        hole = r.mask(lambda d, s: _ell(d, s, 128, 93, 36, 8))
        sym = sym * (1 - hole)
    elif name == "badge_enchant_slotless":
        sym = r.mask(lambda d, s: [_line(d, s, pts, 16) for pts in (
            [(78, 110), (78, 80), (108, 80)], [(148, 80), (178, 80), (178, 110)],
            [(78, 146), (78, 176), (108, 176)], [(148, 176), (178, 176), (178, 146)])])
    else:
        sym = r.mask(lambda d, s: [_line(d, s, [(82, 132), (114, 164), (176, 94)], 26)])
        ink, edge = GREEN_DEEP * 1.2, CREAM_HI * .9
    r.over(edge, (outside_distance(sym) < 3.0).astype(np.float32) * .85)
    relief(r, sym, ink, bevel=7, height=2.6, gloss=.7)
    return r.export()


# ---------------------------------------------------------------------------------------------- card template (W13)
# docs/specs/v11/CARD_TEMPLATE.md sections 3 and 6. All four are neutral grey (NEUTRAL_NAMES): GameApp.Card11
# TintedSprite13 multiplies them by rarityCommon / rarityAdvanced / rarityRare, or by Ghost for goods. White is the tint
# itself, so the lightest glaze shows the rarity colour exactly and every shade below it is the same hue, darker.
# No crackle on these four: the theme's UI sprites have no mipmaps, and fine lines in a 128 px gem drawn 18-34 units
# wide, or across a 5.5-unit band, would sample as sparkle; the card face around them carries the crackle.
RING_EDGE = .6         # px: outer silhouette inset; the first pixel stays under half cover, the second is the groove
RING_BAND = 10.4       # px: the band ends 11 px in (RingInsets13: 5.5 units, the default ring 6), so on the sancai card
#                        face DrawCard13's content box stays at least as wide as the default style's
RING_RADIUS = 18.0     # px: a round corner that stays inside the card face's own inner corner (radius 9.5 px) and
#                        clears the compact icon's corner
RING_GROOVE = (2.4, 2.0)   # px: dark glaze grooves along the outer and inner edge of the bead
GEM_SIZE = 128
GEM_INK = 8.5          # px: ink line of the gems (about 1.2 px at 18 units, 2.3 px at 34)


def card_frame_rarity(w: int, h: int) -> Image.Image:
    """Rarity ring (border 40; transparent centre, since TintedSprite13 draws it with fillCenter off). A raised, glossy
    glaze bead between two dark glaze grooves, with round corners. DrawCard13 lays it just inside the card face's frame
    (where the cream lip ends) and around the compact icon block. Its section is the same all along every edge, so
    slicing it over a 330-unit shop card or a 74-unit icon block changes only its length."""
    r = Raster(w, h)
    band = RING_BAND
    groove_out, groove_in = RING_GROOVE

    def draw(win: Raster):
        d = sd_round_box(win.X, win.Y, (RING_EDGE, RING_EDGE, w - RING_EDGE, h - RING_EDGE), RING_RADIUS)
        t = -d
        s = np.clip(t / band, 0, 1)
        crest = .45
        up = np.sin(np.clip(s / crest, 0, 1) * np.pi / 2)
        down = np.sin(np.clip((1 - s) / (1 - crest), 0, 1) * np.pi / 2)
        H = (np.minimum(up, down) ** .8).astype(np.float32)
        diffuse, _ = lighting(H, 2.2, ambient=.5)
        # Light glaze (most of the band shows the tint itself), the far slope a little darker, dark grooves at both edges.
        grey = .95 + .05 * np.clip((diffuse - 1) / .3, 0, 1) - .24 * np.clip((1 - diffuse) / .5, 0, 1)
        outer = 1 - smoothstep(groove_out * .45, groove_out, t)
        inner = smoothstep(band - groove_in, band - groove_in * .45, t)
        grey = grey * (1 - outer) + .30 * outer
        grey = grey * (1 - inner) + .36 * inner
        alpha = coverage(d) * (1 - coverage(d + band))
        win.over(np.repeat(np.clip(grey, 0, 1)[..., None], 3, axis=2), alpha)

    _band_render(r, w, h, 40, draw, None)
    return r.export()


def _polygon_sdf(X, Y, pts) -> np.ndarray:
    """Exact signed distance to a closed polygon (negative inside; even-odd inside test)."""
    Xd, Yd = X.astype(np.float64), Y.astype(np.float64)
    best = np.full(X.shape, np.inf)
    inside = np.zeros(X.shape, bool)
    for (ax, ay), (bx, by) in zip(pts, pts[1:] + pts[:1]):
        ex, ey = bx - ax, by - ay
        wx, wy = Xd - ax, Yd - ay
        t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey), 0, 1)
        best = np.minimum(best, (wx - ex * t) ** 2 + (wy - ey * t) ** 2)
        if ay != by:
            inside ^= ((ay > Yd) != (by > Yd)) & (Xd < ax + ex * (Yd - ay) / ey)
    d = np.sqrt(best)
    return np.where(inside, -d, d).astype(np.float32)


def gem_sdf(name: str, X, Y, size: int = GEM_SIZE) -> np.ndarray:
    """Silhouettes that differ without colour (CARD_TEMPLATE 3): round, diamond and six-point star, all soft-tipped."""
    c = size / 2
    if name == "ui_gem_common":
        return sd_circle(X, Y, c, c, 57.0)
    if name == "ui_gem_advanced":
        # A square on its point: tips 61 px from the centre, rounded with radius 7.
        tip, rr = 61.0, 7.0
        s = (tip - rr) / math.sqrt(2) + rr
        u = (X - c + (Y - c)) / np.float32(math.sqrt(2))
        v = (Y - c - (X - c)) / np.float32(math.sqrt(2))
        return sd_round_box(u, v, (-s, -s, s, s), rr)
    if name == "ui_gem_rare":
        # A hexagram: six points reaching 62 px, notches near 58 % of that (the regular six-point star) once the outline
        # is rounded by 4 px; the blur softens the notches a little more.
        outer, ratio, rr = 62.0, .54, 4.0
        reach = outer - rr
        pts = [(c + math.cos(-math.pi / 2 + math.pi * i / 6) * (reach if i % 2 == 0 else reach * ratio),
                c + math.sin(-math.pi / 2 + math.pi * i / 6) * (reach if i % 2 == 0 else reach * ratio)) for i in range(12)]
        d = _polygon_sdf(X, Y, pts) - np.float32(rr)
        return ndimage.gaussian_filter(d, 1.6 * SS).astype(np.float32)
    raise ValueError(name)


def gem(name: str, size: int = GEM_SIZE) -> Image.Image:
    """Rarity gem (128 px, not sliced): a glossy glaze drop inside an ink line. The face is pillowed (a rounded bevel off
    the ink line and a low dome, softened so the diamond and the star do not read as cut facets), lit from the upper left
    like the rest of the kit. A tinted sprite cannot be lighter than its tint, so the gloss is a crisp-edged highlight at
    full tint on a face a little below it; the far side of the bevel is shaded, with a little reflected light at its foot
    so the drop reads as glaze rather than metal."""
    r = Raster(size, size)
    d = gem_sdf(name, r.X, r.Y, size)
    inside = inside_distance((d < 0).astype(np.float32))
    body = np.clip(inside - GEM_INK, 0, None)
    top = max(float(body.max()), 1e-3)
    H = (5.0 * np.sin(np.clip(body / 12.0, 0, 1) * np.pi / 2) +
         .25 * top * np.sin(np.clip(body / top, 0, 1) * np.pi / 2)).astype(np.float32)
    H = ndimage.gaussian_filter(H, 2.5 * SS).astype(np.float32)
    diffuse, spec = lighting(H, 1.0, ambient=.45, gloss=1.0, power=14.0)
    shade = np.clip((1 - diffuse) / .5, 0, 1)
    grey = .9 + .06 * np.clip((diffuse - 1) / .3, 0, 1) - .22 * shade + .14 * shade * np.exp(-(body / 4.0) ** 2)
    glint = smoothstep(.38, .5, spec)
    grey = np.clip(grey * (1 - glint) + glint, 0, 1)
    ink = 1 - smoothstep(GEM_INK - 1.2, GEM_INK + .4, inside)
    grey = grey * (1 - ink) + .2 * ink
    r.over(np.repeat(grey[..., None], 3, axis=2), coverage(d))
    return r.export()


# ---------------------------------------------------------------------------------------------- large art
def render_tiled(w: int, h: int, draw_fn, tile: int = 192, margin: int = 28) -> Image.Image:
    """Render a large sprite in full-width strips with overlap; every drawing call works in absolute coordinates,
    so strips agree and the cropped margins hide the strip edges from gradients, distance fields and Lanczos."""
    out = np.zeros((h, w, 4), np.uint8)
    for ty in range(0, h, tile):
        th = min(tile, h - ty)
        y0, y1 = max(0, ty - margin), min(h, ty + th + margin)
        r = Raster(w, y1 - y0, 0.0, float(y0))
        draw_fn(r)
        out[ty:ty + th] = np.asarray(r.export())[ty - y0:ty - y0 + th]
    return Image.fromarray(out, "RGBA")


def card_back() -> Image.Image:
    """Menu card back: a cream crackle-glazed tile, three-colour rim, a haitang frame line, a cobalt medallion with a
    die on a cloud, and cloud scrolls in the corners."""
    W, H = 768, 1024
    clouds = [corner_cloud(c, 15, W, H, 104, 98, AMBER, small=True, tail=.62, width=.24, outline=2.2)
              for c in ("tl", "tr", "bl", "br")]
    # Face crazing is computed once at 2x and shared by the strips (fine lines need no 4x evaluation).
    xs = ((np.arange(W * 2) + .5) / 2).astype(np.float32)
    ys = ((np.arange(H * 2) + .5) / 2).astype(np.float32)
    X2, Y2 = np.meshgrid(xs, ys)
    crack2 = crazing(X2, Y2, 1.6, 378)
    del X2, Y2

    def face_map(win: Raster):
        ox, oy = win.origin
        sub = crack2[oy * 2:(oy + win.h) * 2, ox * 2:(ox + win.w) * 2]
        return np.repeat(np.repeat(sub, SS // 2, axis=0), SS // 2, axis=1)

    kw = dict(radius=58, rim=(40, 40, 40, 46), fill=CREAM, scheme="three", seed=301, texture="full", drips=9,
              lip=(CREAM_HI, 3.0, 1.4), face_crackle=.3, face_map=face_map, crackle_amt=.4, height=4.0,
              pool_corners=1.0)
    r = Raster(W, H)
    # The flat face is the crazed cream glaze without relief: write it directly, then light only the border bands.
    lines = face_map(r)
    r.rgb[...] = mix(np.broadcast_to(CREAM, r.rgb.shape), CREAM * col("8F6A48") * 1.15, lines * .3)
    r.a[...] = 1.0
    del lines
    D = 120
    for x, y, ww, hh in ((0, 0, W, D), (0, H - D, W, D), (0, D, D, H - 2 * D), (W - D, D, D, H - 2 * D)):
        win = r.window(x, y, ww, hh)
        win.rgb[...] = 0.0
        win.a[...] = 0.0
        draw_glazed(win, W, H, **kw)
        frame_d = sd_notch_box(win.X, win.Y, (64, 64, W - 64, H - 64), 34, 0, 6)
        relief(win, ((frame_d < 0) & (frame_d > -7)).astype(np.float32), GREEN, bevel=2.6, height=2.2, gloss=.7)
    for orn in clouds:
        orn(r)
    win = r.window_abs(384 - 222, 512 - 222, 444, 444)
    tile = sdf_mask(quatrefoil_sdf(win.X, win.Y, 384, 512, 200))
    win.over(OUTLINE, (outside_distance(tile) < 4.0).astype(np.float32))
    relief(win, tile, glaze(AMBER, GREEN, 311, .55, 60), bevel=18, height=3.4, gloss=.85)
    field = sdf_mask(quatrefoil_sdf(win.X, win.Y, 384, 512, 166))
    relief(win, field, glaze(COBALT, COBALT_HI, 312, .4, 70), bevel=8, height=-2.4, gloss=.7,
           crackle_amt=.28, seed=313, crackle_scale=1.5)
    cloud = win.strokes(cloud_scroll(384 + 30, 566, 42, width=.24, tail=.7))
    win.over(OUTLINE, (outside_distance(cloud) < 2.6).astype(np.float32))
    relief(win, cloud, CREAM_HI, bevel=4, height=2.4, gloss=.7)
    draw_die(win, 384, 486, 150, pips=(1, 4, 6), colours=(IRON, IRON, COBALT), outline=3.0)
    return r.export()


def wordmark() -> Image.Image:
    """Title wordmark on the cream menu panel: brush-written 骰子构筑 (Ma Shan Zheng, approved for sancai headings,
    CP3-2) as raised umber glaze with an amber edge, and a glazed die resting on a cloud."""
    W, H = 1440, 400
    text = "骰子构筑"
    probe = ImageFont.truetype(str(BRUSH), 100)
    bx = probe.getbbox(text)
    size = int(min(1010 / (bx[2] - bx[0]), 300 / (bx[3] - bx[1])) * 100)
    font = ImageFont.truetype(str(BRUSH), size * SS)
    b = font.getbbox(text)
    tw, th = (b[2] - b[0]) / SS, (b[3] - b[1]) / SS
    tx, ty = 372 + (W - 372 - 24 - tw) * .5, (H - th) * .5 - 6

    def letters(r: Raster) -> np.ndarray:
        ox, oy = r.origin
        return r.mask(lambda d, s: d.text(((tx - ox) * SS - b[0], (ty - oy) * SS - b[1]), text, font=font, fill=255))

    def draw(r: Raster):
        m = letters(r)
        blurred = ndimage.gaussian_filter(m, 3.0 * SS)
        dy, dx = int(5 * SS), int(4 * SS)
        shadow = np.zeros_like(blurred)
        shadow[dy:, dx:] = blurred[:-dy, :-dx]   # offset down-right without wrapping
        r.over(UMBER_DEEP * .8, shadow * .35)
        r.over(AMBER_DEEP, (outside_distance(m) < 4.2).astype(np.float32))
        body = glaze(col("4E2A12"), IRON_DEEP, 401, .55, 50)
        relief(r, m, body, bevel=5.5, height=3.2, gloss=.9, crackle_amt=.18, seed=402, crackle_scale=1.3)
        cloud = r.strokes(cloud_scroll(214, 262, 44, width=.25, tail=.85))
        if cloud.any():
            r.over(OUTLINE, (outside_distance(cloud) < 3.0).astype(np.float32))
            relief(r, cloud, glaze(AMBER, GREEN, 403, .6, 30), bevel=5, height=2.6, gloss=.8)
        win = r.window_abs(60, 40, 280, 300)
        if win is not None:
            draw_die(win, 190, 168, 196, pips=(1, 4, 5), colours=(IRON, IRON, COBALT), outline=4.0)

    return render_tiled(W, H, draw, tile=200, margin=32)


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
COIL_HALF = 14.0
COIL_BOX = (6.0, 2.0, TABLE_W - 6.0, TABLE_H - 10.0)   # centre line of the glazed coil around the table
COIL_RADIUS = 46.0


def _coil(r: Raster):
    """Three-colour glazed clay coil framing the table (concept: roll, select and card pages), with its soft shadow
    on the felt. Everything stays in the outer margin: at most 31 px in from the left edge."""
    X, Y = r.X, r.Y
    wob = (fbm(X, Y, 90.0, 501, 2) - .5) * 4.0
    hw = COIL_HALF + (fbm(X, Y, 60.0, 502, 2) - .5) * 5.0
    dc = np.abs(sd_round_box(X, Y, COIL_BOX, COIL_RADIUS) + wob) - hw
    t = np.clip(-dc / hw, 0, 1)
    tube = np.sqrt(np.clip(1 - (1 - t) ** 2, 0, 1)).astype(np.float32)
    inside = dc < 1.0
    shade_out = np.exp(-np.maximum(dc, 0) / 3.2) * (dc > 0)
    felt = np.broadcast_to(FELT, X.shape + (3,))
    r.over(felt * .55, np.clip(shade_out * .55, 0, 1).astype(np.float32))
    diffuse, spec = lighting(tube, 7.0, ambient=.42, gloss=.9, power=36.0)
    rgb = glaze_field(X, Y, AMBER, GREEN, 511, .9, 46)
    sel = inside
    if sel.any():
        cream = smoothstep(.62, .8, fbm(X[sel], Y[sel] * .5, 14.0, 513, 3))
        rgb[sel] = mix(rgb[sel], CREAM_HI * .97, cream * .85)
        rgb[sel] = mix(rgb[sel], AMBER_DEEP * .75, (1 - t[sel]) ** 3 * .6)
        rgb[sel] = apply_crackle(rgb[sel], X[sel], Y[sel], .3, 514, 1.3)
    lit = np.clip(rgb * diffuse[..., None] + spec[..., None] * CREAM_HI, 0, 1)
    r.over(lit, coverage(dc))


DRIPS = ((180, 22), (820, 16), (1290, 26), (1700, 18))   # (x, length) from the top coil, clear of the shop title


def _drips_top(r: Raster):
    for i, (x, length) in enumerate(DRIPS):
        win = r.window_abs(x - 14, 0, 28, 20 + length + 6)
        if win is None:
            continue
        xs = np.full(24, float(x)) + np.sin(np.linspace(0, 2.4, 24)) * 1.5
        ys = np.linspace(10.0, 14.0 + length, 24)
        ws = 7.0 - 3.2 * np.linspace(0, 1, 24) ** 1.8
        m = win.strokes([(xs, ys, ws)])
        tip = win.mask(lambda d, s, yy=14.0 + length: _ell(d, s, x + 1.2, yy, 4.6, 5.4))
        m = np.maximum(m, tip)
        win.over(FELT * .5, (outside_distance(m) < 1.6).astype(np.float32) * .6)
        relief(win, m, GREEN if i % 2 == 0 else AMBER, bevel=2.6, height=2.4, gloss=.9)


def _table_clouds(r: Raster):
    for corner in ("tl", "tr", "bl", "br"):
        right, bottom = corner[1] == "r", corner[0] == "b"
        s = 21.0
        ox = 40.0
        oy = 34.0 if bottom else 30.0
        orn = corner_cloud(corner, s, TABLE_W, TABLE_H, ox, oy, AMBER, small=True, tail=.72, width=.26, outline=2.4)
        orn(r)


def table_layer(r: Raster):
    _coil(r)
    _drips_top(r)
    _table_clouds(r)


TABLE_REGIONS = (  # non-overlapping cores rendered with a margin: top, bottom, left, right bands
    (0, 0, TABLE_W, 72), (0, TABLE_H - 96, TABLE_W, 96), (0, 72, 64, TABLE_H - 168), (TABLE_W - 64, 72, 64, TABLE_H - 168),
)


def table_scene() -> Image.Image:
    canvas = np.broadcast_to(np.array(rgb8(PALETTE["felt"]), np.float32), (TABLE_H, TABLE_W, 3)).copy()
    margin = 8
    for x, y, w, h in TABLE_REGIONS:
        r = Raster(w + 2 * margin, h + 2 * margin, float(x - margin), float(y - margin))
        table_layer(r)
        im = np.asarray(r.export()).astype(np.float32)
        core = im[margin:margin + h, margin:margin + w]
        a = core[..., 3:] / 255.0
        cy0, cy1 = max(0, y), min(TABLE_H, y + h)
        cx0, cx1 = max(0, x), min(TABLE_W, x + w)
        part = core[cy0 - y:cy1 - y, cx0 - x:cx1 - x]
        pa = a[cy0 - y:cy1 - y, cx0 - x:cx1 - x]
        canvas[cy0:cy1, cx0:cx1] = part[..., :3] * pa + canvas[cy0:cy1, cx0:cx1] * (1 - pa)
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
        print(f"SANCAI_ZONE {name} rect={x},{y},{w},{h} max_rgb_delta={delta} "
              f"limit={MAX_FELT_RGB_DEVIATION} {'PASS' if passed else 'FAIL'}")
        if not passed:
            raise ValueError(f"Table decoration entered protected zone {name}: {delta}")
    return results


def felt_tile() -> Image.Image:
    """Periodic umber felt: fibre grain drawn at 4x and wrap-filtered, plus low mottling on a 1024-periodic lattice.
    Mean is the felt colour; no lighting gradient, so it tiles without seams."""
    n = 1024
    rng = np.random.RandomState(20261117)
    grain = rng.randint(-6, 7, (n * SS, n * SS)).astype(np.float32)
    grain = ndimage.uniform_filter1d(grain, size=7, axis=1, mode="wrap")   # short horizontal fibres
    padded = np.pad(grain, ((16, 16), (16, 16)), mode="wrap")
    small = _resize_f(padded, n + 8, n + 8)[4:4 + n, 4:4 + n]
    ys, xs = np.mgrid[0:n, 0:n].astype(np.float32) + .5
    mottle = (fbm(xs, ys, 128.0, 601, 3, period=8) - .5) * 7.0
    base = np.array(rgb8(PALETTE["felt"]), np.float32)
    tone = small * 1.4 + mottle
    rgb = base[None, None, :] + tone[..., None] * np.array([1.0, .85, .7], np.float32)
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
    draw_die(r, size * .7, size * .66, size, pips=pips, colours=(IRON, COBALT, COBALT), outline=1.2)
    im = r.export()
    shadow = m.sprites["ui_die_shadow"].resize((round(size * 1.3), round(size * .5)), Image.Resampling.LANCZOS)
    m.im.alpha_composite(shadow, (round(cx - size * .65), round(cy + size * .38)))
    m.im.alpha_composite(im, (round(cx - size * .7), round(cy - size * .66)))


def play_mock(sprites, specs, table) -> Image.Image:
    P = PALETTE
    m = Mock(sprites, specs, table)
    lerp = lambda a, b, t: (col(a) * (1 - t) + col(b) * t)
    sec = lerp(P["background"], P["panel"], .55)
    # Top bar.
    m.put("ui_topbar", 16, 12, 1888, 72, col(P["panel"]))
    m.put("ui_chip_formula", 24, 20, 160, 56, col(P["panelLight"]))
    m.text(104, 47, "第 4 关", 28, P["textColor"], "mm")
    for i in range(8):
        cur = i == 1
        size = 20 if cur else 14
        x = 204 + i * 24 + (20 - size) / 2
        m.put("ui_pip_dot", x, 26 + (20 - size) / 2, size, size,
              col(P["panelLight"]) if cur else col(P["gold"]) if i < 1 else col(P["panelLight"]))
        if cur:
            m.put("ui_ring", x - 3, 23, size + 6, size + 6, col(P["paper"]), mult=6)
    m.text(204, 63, "第 2 章 · 普通关", 20, P["muted"], "lm")
    m.put("ui_info_inset", 452, 18, 560, 60)
    m.text(470, 44, "2,480", 34, P["textColor"], "lm")
    m.text(580, 44, "/ 4,000", 32, P["muted"], "lm")
    m.put("ui_progress_track", 466, 66, 532, 7, col("07191C"), mult=48 / 7)
    m.put("ui_progress_fill", 466, 66, 330, 7, col(P["blue"]), mult=48 / 7)
    # Gold and the two buttons where the game puts them (GameApp.Play11 TopBar11: gold group 1476-1652; GameApp.Art07
    # DrawNavigation07: handbook 1668 and pause 1788, 108 x 60 UiKit.Button = paper face on the code's 8F453A step).
    m.icon("ui_icon_coin", 1482, 30, 36, 36)
    m.text(1608, 48, "28", 34, P["gold"], "rm")
    m.text(1610, 48, "金币", 20, P["muted"], "lm")
    for x, label in ((1668, "手册"), (1788, "暂停")):
        m.put("ui_button_bevel", x + 2, 26, 104, 54, col("8F453A"))
        m.put("ui_button_bevel", x, 18, 108, 53, col(P["paper"]))
        m.text(x + 54, 44, label, 23, P["ink"], "mm")
    # Cards.
    arts = ["C108", "C117", "U10"]
    for i, art in enumerate(arts):
        x = 32 + i * 228
        m.put("ui_card_controller", x, 96, 216, 244)
        m.put("ui_pip_dot", x + 12, 104, 22, 22, col(P["ink"]))
        m.text(x + 23, 115, str(i + 1), 14, P["paper"], "mm")
        for k in range(i + 1):
            m.im.alpha_composite(Image.new("RGBA", (8, 5), tuple(rgb8([P["rarityCommon"], P["rarityAdvanced"], P["rarityRare"]][i])) + (255,)),
                                 (x + 42 + k * 11, 113))
        m.text(x + 108, 145, ["排头兵", "点金手", "配重"][i], 22, P["ink"], "mm")
        m.art(ROOT / "ArtSource/StyleKit/themes/sancai/anchors" / f"{art}.png", x + 40, 160, 136)
        m.text(x + 108, 312, ["首颗点数 ×4", "末颗镀金", "目标面权重 +1"][i], 20, P["ink"], "mm")
    m.put("ui_frame_enchant", 32 + 228, 96, 216, 244, col(P["gold"]))
    for i in range(2):
        m.put("ui_slot_inventory", 716 + i * 68, 104, 56, 228)
        m.text(744 + i * 68, 218, "+", 30, P["muted"], "mm")
    # Stage card.
    m.put("ui_stage_card", 1496, 96, 392, 244, col(P["paper"]))
    m.put("ui_chip_formula", 1510, 110, 356, 42, col(P["panelLight"]))
    m.text(1522, 131, "普通关", 22, P["textColor"], "lm")
    m.text(1510, 172, "目标分数", 20, P["ink"], "lm")
    m.text(1510, 218, "4,000", 42, P["ink"], "lm")
    m.text(1510, 266, "过关奖励 +5 金币", 20, P["ink"], "lm")
    m.text(1510, 300, "提交栏 5 格 · 每手掷 3 次", 17, P["ink"], "lm")
    # Tray, dice and formula chips.
    rim = lerp(P["panel"], P["panelLight"], .55)
    m.put("ui_dice_tray", 32, 352, 1448, 376, rim)
    m.put("ui_ring", 39, 359, 1434, 362, col(P["ghost"]), alpha=.75, mult=1.6)
    m.put("ui_chip_formula", 56, 338, 100, 34, col(P["panel"]))
    m.text(106, 355, "骰池", 20, P["muted"], "mm")
    for i, pips in enumerate(((1, 2, 3), (4, 5, 6), (2, 3, 1), (6, 2, 4), (3, 1, 5))):
        _mock_die(m, 250 + i * 250, 500, 104, pips)
    m.put("ui_chip_formula", 668, 410, 104, 32, col(P["gold"]))
    m.text(720, 426, "已选 1", 18, P["ink"], "mm")
    chips = [(52, 150, "基础分 26", "blue"), (214, 150, "一对 ×2", "red"), (376, 160, "全蓝 ×3", "blue"),
             (548, 170, "卡牌 ×1.5", "gold"), (730, 200, "= 234", "reward")]
    for x, w, label, token in chips:
        m.put("ui_chip_formula", x, 666, w, 48, col(P[token]))
        m.text(x + w / 2, 690, label, 20, P["ink"], "mm")
    m.text(942, 690, "×", 24, P["onFelt"], "mm")
    # Resources.
    m.put("ui_panel_hud", 1496, 352, 392, 376)
    for i, (label, value, token) in enumerate((("还能掷", "2", "teal"), ("还能提交", "3", "gold"))):
        y = 366 + i * 96
        m.put("ui_info_inset", 1510, y, 364, 86)
        if i == 0:
            m.icon("ui_icon_roll", 1524, y + 25, 36, 36)
        m.text(1570, y + 43, label, 24, P["muted"], "lm")
        m.text(1860, y + 43, value, 52, P[token], "rm")
    m.text(1510, 578, "道具 1 / 3", 20, P["muted"], "lm")
    m.put("ui_card_item", 1510, 596, 64, 64)
    m.art(ROOT / "ArtSource/StyleKit/themes/sancai/anchors/I10.png", 1516, 601, 52)
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
    m.text(48, 768, "提交栏 2 / 5", 24, P["textColor"], "lm")
    m.text(48, 804, "已锁定 · 本手不可移回骰池", 18, P["gold"], "lm")
    m.text(48, 846, "一对 · 全蓝", 24, P["textColor"], "lm")
    m.put("ui_dice_tray", 344, 740, 1136, 202, rim)
    for i in range(5):
        x = 376 + i * 216
        locked = i < 2
        ring = lerp(P["gold"], P["paper"], .15) if locked else col(P["ghost"])
        m.put("ui_ring", x, 756, 200, 170, ring, alpha=1.0 if locked else .75)
        m.text(x + 188, 774, str(i + 1), 16, P["onFelt"], "mm")
        if locked:
            _mock_die(m, x + 72, 846, 96, ((5, 2, 3), (2, 6, 1))[i])
            m.put("ui_tray_front", x + 4, 920, 192, 20)
            m.icon("ui_tag_lock", x + 168, 800, 28, 30)
        else:
            m.text(x + 100, 840, "·  ·  ·", 20, P["onFelt"], "mm")
    m.put("ui_panel_hud", 1496, 740, 392, 202)
    m.text(1512, 770, "离目标还差", 22, P["muted"], "lm")
    m.text(1512, 820, "1,520", 44, P["textColor"], "lm")
    m.text(1512, 878, "达标后结束：过关奖励 +5 金币", 18, P["muted"], "lm")
    # Actions.
    m.put("ui_panel_hud", 32, 958, 512, 88)
    m.text(48, 1002, "按“提交”计分，或者重掷其余的骰子。", 21, P["textColor"], "lm")
    m.put("ui_chip_formula", 560, 966, 264, 72, sec)
    m.text(692, 1002, "重掷 · 还能掷 2", 21, P["textColor"], "mm")
    m.put("ui_button_bevel", 842, 970, 396, 74, col("B56B29"))
    m.put("ui_button_bevel", 840, 962, 400, 73, col(P["primary"]))
    m.text(1040, 998, "提交 2 颗", 28, label_on(col(P["primary"])), "mm")   # the label LabelOn11 picks
    m.put("ui_button_bevel", 1498, 970, 388, 74, col("8F453A"))
    m.put("ui_button_bevel", 1496, 962, 392, 73, col(P["paper"]))
    m.text(1692, 998, "结束本关 · 结算", 24, P["ink"], "mm")
    return m.im.convert("RGB")


SMALL_PROOFS = (  # (sprite, display w, h, host colour, tint) at the sizes and hosts the game uses
    ("ui_icon_coin", 26, 26, "gold", None), ("ui_icon_coin", 36, 36, "panel", None),
    ("ui_icon_roll", 36, 36, "26331A", None), ("ui_icon_roll", 28, 28, "panel", None),
    ("ui_icon_lock", 20, 22, "background", None), ("ui_icon_dice_view", 40, 40, "2B3A17", None),
    ("ui_tag_lock", 28, 30, "felt", None), ("ui_tag_clock", 64, 64, "2B3C18", None),
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
    im = Image.new("RGB", (len(SMALL_PROOFS) * cell, 2 * cell + 40), (40, 26, 16))
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
        d.text((x + 4, 2 * cell + 4), name.replace("ui_", "").replace("badge_enchant_", "b_")[:13], fill=(220, 200, 160), font=small)
        d.text((x + 4, 2 * cell + 20), f"{w}x{h}", fill=(170, 150, 120), font=small)
    return im


def label_on(fill) -> str:
    """The palette text colour the game would pick on a drawn fill (GameApp.BestLabel11, without the 5 % steps)."""
    fill_hex = "".join(f"{int(round(float(v) * 255)):02X}" for v in fill)
    return max((PALETTE["ink"], PALETTE["paper"], PALETTE["textColor"]), key=lambda c: contrast(c, fill_hex))


def state_variants(sprites, specs, table) -> Image.Image:
    """Normal, pressure and boss cues as GameApp.Play11 draws them: stage tag, stage card (header chip and rings)
    and the tray's accent ring, on the table. Offline composite."""
    P = PALETTE
    m = Mock(sprites, specs, Image.new("RGB", (1752, 330), rgb8(P["felt"])))
    rim = col(P["panel"]) * .45 + col(P["panelLight"]) * .55
    for i, (kind, tint_key) in enumerate((("普通关", "panelLight"), ("高压关", "pressure"), ("首领关", "boss"))):
        x0 = 16 + i * 584
        tag = col(P[tint_key])
        m.put("ui_chip_formula", x0, 12, 160, 56, tag)
        m.text(x0 + 80, 40, f"第 {4 + i} 关", 28, label_on(tag), "mm")
        m.put("ui_stage_card", x0, 80, 392, 236, col(P["paper"]))
        if kind == "首领关":
            m.put("ui_ring", x0 + 4, 84, 376, 218, col(P["boss"]))
            m.put("ui_ring", x0 + 9, 89, 366, 208, col(P["boss"]), alpha=.55)
        elif kind == "高压关":
            m.put("ui_ring", x0 + 5, 85, 374, 216, col(P["pressure"]))
        m.put("ui_chip_formula", x0 + 14, 94, 356, 42, tag)
        m.text(x0 + 26, 115, kind if kind != "首领关" else "首领减益 · 二档", 22, label_on(tag), "lm")
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


def review_sheet(sprites, specs, table, mock) -> Image.Image:
    """Default and sancai pieces side by side, slice proofs at PPU 200, the in-situ composite, the table, the
    preview, the palette with its contrast numbers and the five concept pages. Offline review, not Unity."""
    cw, ch, columns = 600, 262, 3
    rows = math.ceil(len(specs) / columns)
    bg = (52, 33, 20)
    ink = (243, 228, 200)
    sub = (205, 183, 143)
    grid_h = 170 + rows * ch
    sheet = Image.new("RGB", (columns * cw, grid_h + 4600), bg)   # generous; cropped to the content at the end
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype(str(LATIN), 19)
    small = ImageFont.truetype(str(LATIN), 15)
    title = ImageFont.truetype(str(LATIN), 44)
    cjk = ImageFont.truetype(str(FONT), 22)
    d.text((24, 20), "SANCAI / 唐三彩陶坊 UI KIT".replace("唐三彩陶坊 ", ""), fill=ink, font=title)
    d.text((560, 26), "唐三彩陶坊", fill=ink, font=ImageFont.truetype(str(BRUSH), 46))
    d.text((25, 82), f"{len(specs)} referenced contract sprites   /   DEFAULT | SANCAI | SANCAI x palette tint (TINT pieces only)",
           fill=ink, font=font)
    d.text((25, 112), "Offline source review: nine-slice, tint and the composite below are Python emulations, not Unity screenshots.",
           fill=sub, font=small)
    d.text((25, 136), "Checker = transparency. TINT = the game multiplies the piece by a palette colour (shown with its main caller's tint).",
           fill=sub, font=small)
    tints = TINT_PROOF
    for n, spec in enumerate(specs):
        x, y = (n % columns) * cw, 170 + (n // columns) * ch
        d.line((x + 18, y, x + cw - 18, y), fill=(120, 90, 60), width=2)
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
                    im = tint(im, col(PALETTE[tints.get(name, "panel")]) if tints.get(name, "panel") in PALETTE else col(tints[name]))
            im.thumbnail((bw - 8, 164), Image.Resampling.LANCZOS)
            sheet.paste(im, (box[0] + (bw - im.width) // 2, box[1] + (172 - im.height) // 2), im)
            label = ("DEFAULT", "SANCAI", "x " + tints.get(name, "panel"))[col_i]
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
        sheet.paste(im, (px, py), im)
        d.text((px, py + h + 4), label, fill=sub, font=small)
        px += step
        row_h = max(row_h, h)
    y = py + row_h + 60
    d.text((24, y), "TABLE / 1920 x 1080 (calm zones outlined) and PREVIEW / 480 x 270", fill=ink, font=font)
    proof = table.copy()
    pd = ImageDraw.Draw(proof)
    for zx, zy, zw, zh in FELT_ZONES.values():
        pd.rectangle((zx, zy, zx + zw - 1, zy + zh - 1), outline=(120, 190, 220), width=3)
    sheet.paste(proof.resize((1152, 648), Image.Resampling.LANCZOS), (24, y + 36))
    sheet.paste(preview(mock), (1200, y + 36))
    sy = y + 330
    rep = palette_report()
    for i, key in enumerate(PALETTE):
        xx, yy = 1200 + (i % 4) * 140, sy + (i // 4) * 54
        d.rectangle((xx, yy, xx + 34, yy + 34), fill=rgb8(PALETTE[key]), outline=(20, 12, 6))
        d.text((xx + 40, yy), key, fill=ink, font=small)
        d.text((xx + 40, yy + 17), PALETTE[key], fill=sub, font=small)
    y += 36 + 648 + 24
    lines = [f"{k} {v:.2f}" for k, v in rep.items()]
    for i in range(0, len(lines), 4):
        d.text((24, y + (i // 4) * 22), "     ".join(lines[i:i + 4]), fill=sub, font=small)
    y += 22 * math.ceil(len(lines) / 4) + 30
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


TINT_PROOF = {
    "ui_topbar": "panel", "ui_chip_formula": "panelLight", "ui_stage_card": "paper", "ui_dice_tray": "panelLight",
    "ui_button_bevel": "primary", "ui_button_ghost": "ghost", "ui_ring": "ghost", "ui_frame_enchant": "gold",
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
            shade = (118, 104, 92) if ((xx - x0) // 12 + (yy - y0) // 12) % 2 else (96, 84, 74)
            d.rectangle((xx, yy, min(xx + 11, x1 - 1), min(yy + 11, y1 - 1)), fill=shade)


# ---------------------------------------------------------------------------------------------- card template review
# Offline review of the W13 card template (not a Unity capture and not part of build()): the shipped ring and gems of
# sancai and of Swiss, tinted with each theme's rarity colours (Ghost for goods), composited as GameApp.Card11 DrawCard13
# lays them out in the shop (220 x 330), table (216 x 244) and compact (280 x 117) sizes, at 1080p and 720p. Sprites are
# sampled like the game's UI sprites (bilinear, no mipmaps) and blended in linear light; text and pictures are stand-ins.
CARD_REVIEW = REVIEW / "card_template_review.png"
CARD_REVIEW_CARDS = (  # (rarity or None for goods, name, short text, StyleKit anchor picture)
    ("common", "排头兵", "第一颗提交的骰子点数×4，加到基础分。", "C108"),
    ("advanced", "蓝色浪潮", "本手提交时，提交的骰子全部按蓝色计算。", "I10"),
    ("rare", "点金手", "每次提交后，最后一颗骰子朝上的面永久镀金。", "C117"),
    (None, "配重", "这一面权重+1（上限5），更容易掷出这一面。", "U10"),
)
CARD_REVIEW_THEMES = ("sancai", "swiss")
BODY_FONT = ROOT / "DicePrototype/Assets/Game/Fonts/NotoSansCJKsc-Regular.otf"


def _lin_texture(im: Image.Image) -> np.ndarray:
    """Premultiplied linear-light RGBA, which is what bilinear filtering of an sRGB sprite imported with
    alphaIsTransparency blends (closely enough for a review)."""
    a = np.asarray(im.convert("RGBA")).astype(np.float32) / 255.0
    out = np.empty_like(a)
    out[..., 3] = a[..., 3]
    out[..., :3] = _lin_arr(a[..., :3]) * a[..., 3:]
    return out


def _slice_axis(n_dst: int, n_src: int, lo: float, hi: float, lo_dst: float, hi_dst: float) -> np.ndarray:
    """Texel coordinate seen by each destination pixel centre along one axis of a sliced sprite (lo/hi: border texels at
    the start and end of the axis, lo_dst/hi_dst: their size on screen). Unity shrinks borders that do not fit."""
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
        """An Image at a design-unit rect: sliced when it has a border (l, b, r, t in sprite px), else simple."""
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
        """A stand-in illustration fitted into a design-unit square (Lanczos)."""
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
    """GameApp.Card11 RingInsets13 (left, bottom, right, top) in sprite px: the last pixel with alpha >= 128 along the
    middle row and column, within the slice border."""
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
    tint = col(palette[key])
    ink = palette["ink"]
    if size == "compact":
        content = (inner[0] + 4, inner[1] + 4, inner[2] - 8, inner[3] - 8)
        block, gem_size = min(content[3] - 8, 76), 18
        bx, by = content[0], content[1] + max(0, (content[3] - block - gem_size * .5) * .5)
        c.sprite(ring, bx, by, block, block, tint=tint, border=borders["ui_card_frame_rarity"])
        pic = (bx + rl, by + rt, block - rl - rr, block - rt - rb)
        c.picture(art, pic[0] + 1, pic[1] + 1, min(pic[2], pic[3]) - 2)
        if not goods:
            c.sprite(kit["ui_gem_" + rarity], bx + block * .5 - gem_size * .5, by + block - gem_size * .5, gem_size,
                     gem_size, tint=tint)
        tx = bx + block + 10
        c.text(tx, content[1] + 2, title, 20, ink, FONT)
        for i, line in enumerate(_wrap(text, BODY_FONT, 18, content[0] + content[2] - tx)[:2]):
            c.text(tx, content[1] + 34 + i * 24, line, 18, ink, BODY_FONT)
        return
    c.sprite(ring, *inner, tint=tint, border=borders["ui_card_frame_rarity"])
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
        c.sprite(kit["ui_gem_" + rarity], x + w * .5 - gem_size * .5, gem_y - gem_size * .5, gem_size, gem_size, tint=tint)


def card_template_review() -> Image.Image:
    """Sancai beside Swiss: shop, table and compact cards (common, advanced, rare, goods) at 1080p and 720p, then the
    three gems at 12-34 px on the card's own top rim with a 4x nearest enlargement."""
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
    panel_w, gap, ink, sub = 960, 40, (243, 228, 200), (205, 183, 143)
    sheet = Image.new("RGB", (2 * panel_w + gap + 48, 3000), (36, 30, 26))   # cropped to the content at the end
    d = ImageDraw.Draw(sheet)
    font, small, title = (ImageFont.truetype(str(LATIN), s) for s in (19, 15, 34))
    d.text((24, 16), "CARD TEMPLATE / W13 rarity ring and gems   -   SANCAI (left)   |   SWISS (right)", fill=ink, font=title)
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
                canvas = UnityCanvas(panel_w, ph, palette["felt"] if theme == "sancai" else "F9EADB", scale)
                for i, (rarity, name, text, art) in enumerate(CARD_REVIEW_CARDS):
                    sx, sy = 8 + (i % per_row) * (sw + 16), 8 + (i // per_row) * (sh + 30)
                    draw_card13(canvas, kit, borders, palette, sx / scale, sy / scale, cw, chh, size, rarity, name, text,
                                arts[art])
                sheet.paste(canvas.image(), (px, y + 26))
                note = sub if luminance(palette["felt"] if theme == "sancai" else "F9EADB") < .2 else (70, 64, 58)
                for i, (rarity, *_rest) in enumerate(CARD_REVIEW_CARDS):
                    sx, sy = 8 + (i % per_row) * (sw + 16), 8 + (i // per_row) * (sh + 30)
                    d.text((px + sx, y + 26 + sy + sh + 5), f"{rarity or 'goods'}  {palette[keys[rarity]]}", fill=note, font=small)
            y += 26 + ph + 18
    # The three gems at their display sizes on the card's own top rim and ring: 12 (compact, 720p), 18 (compact), 20/23
    # (table/shop, 720p), 22 (tall compact), 30/34 (table/shop). Right: the same pixels 2x nearest.
    sizes = (12, 18, 20, 22, 23, 30, 34)
    for p, theme in enumerate(CARD_REVIEW_THEMES):
        kit, palette, _ = themes[theme]
        px = 24 + p * (panel_w + gap)
        d.text((px, y), f"{theme.upper()}   GEMS at {' / '.join(map(str, sizes))} px on the card's top rim and ring; right: 2x nearest",
               fill=ink, font=font)
        face = kit["ui_card_controller"]
        fl, fb, fr, ft = (v / 2 for v in frame_insets_px(np.asarray(face), borders["ui_card_controller"]))
        ring = kit["ui_card_frame_rarity"]
        rl, rb, rr, rt = (v / 2 for v in ring_insets_px(np.asarray(ring), borders["ui_card_frame_rarity"]))
        strip = UnityCanvas(len(sizes) * 42 + 16, 3 * 60 + 8, palette["felt"] if theme == "sancai" else "F9EADB", 1.0)
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
        raise ValueError("Output root must stay inside the sancai worktree")
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
        # Reads the shipped sancai and Swiss sprites; run it after a build.
        print("SANCAI_CARD_REVIEW", write_card_template_review().relative_to(ROOT).as_posix())
        raise SystemExit(0)
    started = time.time()
    paths = build()
    specs = contract_sprites()
    pngs = [p for p in paths if p.suffix == ".png"]
    print(f"SANCAI_BUILT {len(specs)} UI sprites / {len(pngs)} PNGs + theme.json / supersampling {SS}x / "
          f"{time.time() - started:.1f}s")
    for key, value in palette_report().items():
        print(f"SANCAI_PALETTE {key} {value:.3f}")
