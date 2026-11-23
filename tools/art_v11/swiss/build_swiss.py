"""W12/W12b: deterministic Swiss UI vectors; 4x rasterization, one Lanczos downsample.

The contract owns dimensions, slice borders and PPU. The descriptor below owns the released palette
and registry metadata. All fonts are repository assets; no OS font fallback.
"""
from __future__ import annotations

import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[3]
CONTRACT = ROOT / "tools/art_v11/theme_contract.json"
THEME = Path("DicePrototype/Assets/Resources/Themes/swiss")
REVIEW = Path("ArtSource/Art11/swiss")
FONT = ROOT / "DicePrototype/Assets/Game/Fonts/NotoSansSC-Bold.otf"
LATIN = ROOT / "DicePrototype/Assets/TextMesh Pro/Fonts/LiberationSans.ttf"
SS = 4
RULE = 10
CHAMFER = 16


DESCRIPTOR = {
    "id": "swiss",
    "displayName": "瑞士现代主义",
    "description": "暖白纸面、黑色块与粗黑线、红色斜切，硬边平涂。依据：ArtSource/StyleKit/themes/swiss/concepts；色板来自界面件生成器的建议（tools/art_v11/swiss/README.md）",
    "order": 1,
    "preview": False,
    "palette": {
        "background": "151618",
        "felt": "FFFFFF",
        "panel": "151618",
        "panelLight": "4E4E4E",
        "paper": "F9EADB",
        "ink": "000000",
        "textColor": "FCF1E0",
        "muted": "C8BEB5",
        "gold": "E80E16",
        "teal": "C8BEB5",
        "red": "EC655D",
        "blue": "55B6D9",
        "violet": "96948E",
        "danger": "D60D0E",
        "shadow": "000000",
        "ghost": "96948E",
        "primary": "E80E16",
        "reward": "E80E16",
        "boss": "E80E16",
        "pressure": "E80E16",
        "rarityCommon": "96948E",
        "rarityAdvanced": "4E4E4E",
        "rarityRare": "E80E16",
        "onFelt": "151618",
        "valueBadge": "151618"
    }
}


def theme_json() -> str:
    return json.dumps(DESCRIPTOR, ensure_ascii=False, indent=2) + "\n"


def rgba(value: str, alpha: int = 255) -> tuple[int, int, int, int]:
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4)) + (alpha,)


PAPER = rgba("F9EADB")
LIGHT = rgba("FCF1E0")
INK = rgba("151618")
BLACK = rgba("000000")
RED = rgba("E80E16")
DEEP_RED = rgba("D60D0E")
GREY = rgba("96948E")
TAN = rgba("C8BEB5")
WHITE = rgba("FFFFFF")
CLEAR = (0, 0, 0, 0)

NEUTRAL_NAMES = frozenset({
    "ui_button_bevel", "ui_button_ghost", "ui_frame_block", "ui_frame_enchant",
    "ui_ring", "ui_price_chip", "ui_progress_fill", "ui_progress_track",
    "ui_section_header", "ui_pip_dot", "ui_icon_arrow", "ui_icon_book",
    "ui_icon_gear", "fx_arc_short", "fx_score_spark", "fx_square_chip",
    "ui_dice_tray", "ui_topbar", "ui_chip_formula", "ui_stage_card",
    "ui_card_frame_rarity", "ui_gem_common", "ui_gem_advanced", "ui_gem_rare",
})
# The original brief lists 17 masks; W11 adds four directly tinted surfaces and
# no longer uses fx_score_ray (see LEGACY_NAMES). W13 adds the card template's
# rarity ring and three gems, tinted by the rarity colours (or Ghost).
CARD_TEMPLATE_NAMES = ("ui_card_frame_rarity", "ui_gem_common", "ui_gem_advanced", "ui_gem_rare")
# Refresh is also neutral because its caller multiplies it by Ink.

# Top-left design coordinates match the shader's contained 1920x1080 table.
# Include the wider info panel at 125/150% text; the smaller rail is a subset.
FELT_ZONES = {
    "dice_area": (52, 374, 1408, 276),
    "formula_row": (52, 660, 1408, 60),
    "submission_rail": (344, 740, 1136, 202),
    "submit_info_text150": (32, 740, 360, 202),
}
MAX_FELT_RGB_DEVIATION = 0
# W11 stopped referencing fx_formula_chip, fx_score_plate, fx_score_ray and
# ui_badge_check by name. Coordinator decision (W12b review, 2026-11-16): the kit
# ships exactly the referenced set (tests/test_art_v11_swiss.py), so these are no
# longer built. Their drawing branches stay below: if the contract marks one as
# referenced again, a rebuild ships it without further changes.
# W14's shop stopped drawing ui_price_chip, ui_pedestal and ui_frame_enchant; the contract regenerated on 2026-11-18
# lists them as unreferenced. Coordinator decision (2026-11-18): the three finished kits keep them, so their outputs stay
# byte-identical and the review sheet keeps working; new kits (deco, glass) build the referenced set only.
# W16 retired the bottom tab from the runtime after all five themes had shipped it.
LEGACY_NAMES = frozenset({"ui_price_chip", "ui_pedestal", "ui_frame_enchant", "ui_tab_bottom"})


class Canvas:
    """Coordinates use output pixels; rectangle right/bottom edges are exclusive."""

    def __init__(self, w: int, h: int, fill=CLEAR):
        self.size = (w, h)
        self.im = Image.new("RGBA", (w * SS, h * SS), fill)
        self.draw = ImageDraw.Draw(self.im)

    def rect(self, box, fill):
        x0, y0, x1, y1 = (round(v * SS) for v in box)
        self.draw.rectangle((x0, y0, x1 - 1, y1 - 1), fill=fill)

    def frame(self, box, width, fill):
        x0, y0, x1, y1 = box
        self.rect((x0, y0, x1, y0 + width), fill)
        self.rect((x0, y1 - width, x1, y1), fill)
        self.rect((x0, y0 + width, x0 + width, y1 - width), fill)
        self.rect((x1 - width, y0 + width, x1, y1 - width), fill)

    def poly(self, points, fill):
        self.draw.polygon([(round(x * SS), round(y * SS)) for x, y in points], fill=fill)

    def chamfer(self, box, cut, fill):
        """Single straight corner cut; exclusive right/bottom matches rect()."""
        x0, y0, x1, y1 = box
        x1 -= 1 / SS
        y1 -= 1 / SS
        self.poly([(x0 + cut, y0), (x1 - cut, y0), (x1, y0 + cut),
                   (x1, y1 - cut), (x1 - cut, y1), (x0 + cut, y1),
                   (x0, y1 - cut), (x0, y0 + cut)], fill)

    def chamfer_frame(self, box, width, cut, fill, edge=BLACK):
        self.chamfer(box, cut, edge)
        x0, y0, x1, y1 = box
        # Offset the diagonal by the same perpendicular distance as straight edges.
        inner_cut = max(0, cut - (2 - math.sqrt(2)) * width)
        self.chamfer((x0 + width, y0 + width, x1 - width, y1 - width), inner_cut, fill)

    def line(self, points, fill, width):
        self.draw.line([(round(x * SS), round(y * SS)) for x, y in points],
                       fill=fill, width=round(width * SS), joint="curve")

    def ellipse(self, box, fill):
        self.draw.ellipse(tuple(round(v * SS) for v in box), fill=fill)

    def text(self, xy, text, size, fill=INK, latin=False):
        font = ImageFont.truetype(str(LATIN if latin else FONT), round(size * SS))
        self.draw.text(tuple(round(v * SS) for v in xy), text, font=font, fill=fill, anchor="lt")

    def export(self) -> Image.Image:
        return self.im.resize(self.size, Image.Resampling.LANCZOS)


def contract_sprites() -> list[dict]:
    return [s for s in json.loads(CONTRACT.read_text(encoding="utf-8"))["sprites"]
            if (s["referenced"] or s["name"] in LEGACY_NAMES) and not s.get("optionalOverride")]


def surface(spec: dict) -> Image.Image:
    """Keep every rule in the border bands; centers carry no scale-sensitive detail."""
    name = spec["name"]
    w, h = spec["size"]
    p = Canvas(w, h)
    box = (0, 0, w, h)
    if name == "ui_dice_tray":
        # A 10 px black rule plus a 10 px tintable rim, both within border 72.
        # The open center reveals the table and the 3D dice below the Canvas.
        p.chamfer_frame(box, RULE, CHAMFER, WHITE)
        p.chamfer((20, 20, w - 20, h - 20), 8, CLEAR)
    elif name in {"ui_topbar", "ui_chip_formula", "ui_stage_card"}:
        # Surface11 multiplies Panel, semantic chip colors, or Paper respectively.
        # Pure white preserves those colors; content and headers remain in code.
        p.chamfer_frame(box, RULE, CHAMFER, WHITE)
    elif name == "ui_frame_block":
        p.chamfer(box, CHAMFER, WHITE)
    elif name in {"ui_progress_fill", "ui_progress_track", "ui_section_header"}:
        p.rect(box, WHITE)
    elif name in {"ui_button_bevel", "ui_price_chip"}:
        p.chamfer_frame(box, RULE, CHAMFER, WHITE)
    elif name in {"ui_button_ghost", "ui_ring"}:
        p.chamfer_frame(box, RULE, CHAMFER, CLEAR, WHITE)
    elif name == "ui_frame_enchant":
        p.chamfer_frame((14, 14, w - 14, h - 14), 6, 8, CLEAR, WHITE)
    elif name in {"ui_paper_panel", "ui_card_controller", "ui_card_controller_compact",
                  "ui_card_item", "ui_card_shop", "ui_chip_label"}:
        p.chamfer_frame(box, RULE, CHAMFER, PAPER if name != "ui_chip_label" else LIGHT)
        # Tiny diagonal signatures stop before the 40 px slice boundaries.
        if name.startswith("ui_card_"):
            p.poly([(w - 36, h - 14), (w - 14, h - 36), (w - 14, h - 14)], RED)
    elif name in {"ui_info_inset", "ui_panel_hud", "ui_section_panel", "ui_slot_inventory", "ui_tab_bottom"}:
        p.chamfer_frame(box, RULE, CHAMFER, INK)
        # Solid dark blocks carry TextColor/Muted; accents stay in corner patches.
        if name == "ui_panel_hud":
            p.poly([(w - 36, h - 14), (w - 14, h - 36), (w - 14, h - 14)], RED)
    elif name == "fx_score_plate":
        # The caller multiplies the WHOLE image by Blue or Gold: neutral outer
        # rule takes that tint; an ink-black center preserves score readability.
        p.chamfer_frame(box, RULE, 20, INK, WHITE)
    elif name in {"fx_formula_chip", "ui_tray_front"}:
        # Border top/bottom == 0: only the fixed left/right caps contain detail.
        p.chamfer(box, CHAMFER, INK)
        if name == "fx_formula_chip":
            p.poly([(14, 14), (34, 14), (14, 34)], RED)
    elif name == "ui_card_frame_rarity":
        # W13: a flat 14 px band (tinted by rarity) closed by a 4 px black rule, laid
        # just inside the card's own black rule: rule, colour, rule. Open centre.
        p.chamfer(box, CHAMFER, WHITE)
        p.chamfer((14, 14, w - 14, h - 14), CHAMFER - 8, BLACK)
        p.chamfer((18, 18, w - 18, h - 18), CHAMFER - 10, CLEAR)
    else:
        raise ValueError("Unimplemented sliced sprite: " + name)
    return p.export()


def gem(name: str, size: tuple[int, int]) -> Image.Image:
    """W13 rarity gems: one flat white shape (tinted at runtime) inside a bold black
    outline; round, diamond and six-point star stay distinct without colour."""
    w, h = size
    p = Canvas(w, h)
    c = w / 2
    if name == "ui_gem_common":
        p.ellipse((6, 6, w - 6, h - 6), BLACK)
        p.ellipse((17, 17, w - 17, h - 17), WHITE)
    elif name == "ui_gem_advanced":
        r = c - 4
        p.poly([(c, c - r), (c + r, c), (c, c + r), (c - r, c)], BLACK)
        inner = r - 11 * math.sqrt(2)
        p.poly([(c, c - inner), (c + inner, c), (c, c + inner), (c - inner, c)], WHITE)
    elif name == "ui_gem_rare":
        def star(outer: float, inner: float):
            return [(c + math.cos(math.pi * 2 * i / 12 - math.pi / 2) * (outer if i % 2 == 0 else inner),
                     c + math.sin(math.pi * 2 * i / 12 - math.pi / 2) * (outer if i % 2 == 0 else inner))
                    for i in range(12)]
        p.poly(star(c - 3, (c - 3) * .55), BLACK)
        p.poly(star(c - 19, (c - 19) * .48), WHITE)
    else:
        raise ValueError(name)
    return p.export()


def arrow(p: Canvas, x: float, y: float, size: float, color=WHITE):
    p.poly([(x, y + size * .35), (x + size * .58, y + size * .35),
            (x + size * .58, y), (x + size, y + size * .5),
            (x + size * .58, y + size), (x + size * .58, y + size * .65),
            (x, y + size * .65)], color)


def die(p: Canvas, x, y, size, fill=LIGHT, edge=INK, pip=RED):
    p.rect((x, y, x + size, y + size), fill)
    p.frame((x, y, x + size, y + size), size * .065, edge)
    for xx, yy in ((.29, .29), (.71, .71), (.5, .5)):
        r = size * .065
        p.ellipse((x + size * xx - r, y + size * yy - r,
                   x + size * xx + r, y + size * yy + r), pip)


def cup(p: Canvas, x, y, size, color=LIGHT):
    p.poly([(x + size * .12, y), (x + size * .88, y),
            (x + size * .72, y + size), (x + size * .28, y + size)], color)
    p.rect((x + size * .13, y, x + size * .87, y + size * .12), RED)
    p.rect((x + size * .44, y + size * .32, x + size * .56, y + size * .72), INK)


def lock(p: Canvas, size, dark=INK, body=INK):
    p.frame((size * .30, size * .12, size * .70, size * .61), size * .09, dark)
    p.rect((size * .18, size * .42, size * .82, size * .88), body)
    p.rect((size * .45, size * .56, size * .55, size * .74), RED)


def badge(name: str, size: tuple[int, int]) -> Image.Image:
    p = Canvas(*size)
    p.chamfer_frame((20, 20, 236, 236), 10, 12, LIGHT)
    p.poly([(204, 34), (222, 34), (222, 52)], RED)
    if name == "badge_enchant_base10":
        p.rect((61, 116, 195, 146), INK)
        p.rect((113, 64, 143, 198), INK)
    elif name == "badge_enchant_mult2":
        p.poly([(82, 63), (61, 84), (174, 197), (195, 176)], INK)
        p.poly([(174, 63), (195, 84), (82, 197), (61, 176)], INK)
    elif name == "badge_enchant_roll1":
        cup(p, 64, 67, 128, INK)
        p.rect((119, 113, 137, 159), LIGHT)
    elif name == "badge_enchant_submit1":
        p.rect((65, 70, 191, 85), INK)
        p.rect((65, 102, 160, 117), INK)
        p.poly([(74, 159), (91, 142), (118, 169), (169, 130),
                (185, 150), (116, 202)], RED)
    elif name == "badge_enchant_slotless":
        for x, y, dx, dy in ((64, 72, 1, 1), (192, 72, -1, 1),
                              (64, 200, 1, -1), (192, 200, -1, -1)):
            p.rect((min(x, x + dx * 36) - 8, y - 8,
                    max(x, x + dx * 36) + 8, y + 8), INK)
            p.rect((x - 8, min(y, y + dy * 36) - 8,
                    x + 8, max(y, y + dy * 36) + 8), INK)
    else:
        raise ValueError(name)
    return p.export()


def icon(spec: dict) -> Image.Image:
    name = spec["name"]
    w, h = spec["size"]
    p = Canvas(w, h)
    if name == "ui_icon_arrow":
        arrow(p, 14, 18, 64)
    elif name == "ui_icon_book":
        p.poly([(10, 18), (43, 22), (43, 79), (10, 75)], WHITE)
        p.poly([(53, 22), (86, 18), (86, 75), (53, 79)], WHITE)
        p.rect((10, 81, 86, 86), WHITE)
        for y in (34, 48, 62):
            p.rect((17, y, 36, y + 4), CLEAR)
            p.rect((60, y, 79, y + 4), CLEAR)
    elif name == "ui_icon_gear":
        points = []
        for i in range(32):
            angle = math.pi * 2 * i / 32 - math.pi / 32
            radius = 39 if i % 4 < 2 else 29
            points.append((48 + math.cos(angle) * radius, 48 + math.sin(angle) * radius))
        p.poly(points, WHITE)
        p.ellipse((34, 34, 62, 62), CLEAR)
    elif name == "ui_icon_coin":
        p.ellipse((8, 8, 88, 88), INK)
        p.ellipse((15, 15, 81, 81), LIGHT)
        p.rect((39, 26, 57, 70), RED)
    elif name == "ui_icon_dice_view":
        die(p, 10, 10, 80)
        # W11 puts the 40 px glyph on a dark button: outline the dark lens/handle.
        p.line([(89, 89), (118, 118)], LIGHT, 21)
        p.line([(89, 89), (118, 118)], INK, 13)
        p.ellipse((58, 58, 116, 116), LIGHT)
        p.ellipse((62, 62, 112, 112), INK)
        p.ellipse((70, 70, 104, 104), LIGHT)
    elif name == "ui_icon_lock":
        # Its caller places this icon on Background, which is also near-black.
        # A light carrier preserves the black/red silhouette at 22x24 UI pixels.
        p.chamfer_frame((16, 16, 240, 240), 8, 12, LIGHT)
        lock(p, w)
    elif name == "ui_icon_roll":
        cup(p, 41, 34, 174)
        p.rect((80, 222, 176, 233), RED)
    elif name == "ui_icon_refresh":
        # A white silhouette remains readable when multiplied by the caller's Ink.
        p.ellipse((38, 38, 218, 218), WHITE)
        p.ellipse((65, 65, 191, 191), CLEAR)
        p.rect((164, 21, 239, 122), CLEAR)
        p.poly([(159, 21), (226, 93), (140, 95)], WHITE)
    elif name == "ui_badge_check":
        p.chamfer((7, 7, 89, 89), 6, INK)
        p.poly([(69, 13), (83, 13), (83, 27)], RED)
        p.poly([(21, 46), (32, 35), (44, 48), (67, 23), (79, 35), (44, 72)], LIGHT)
    elif name == "ui_tag_lock":
        p.chamfer_frame((3, 3, 125, 125), 6, 8, LIGHT)
        lock(p, 128)
    elif name == "ui_tag_clock":
        p.chamfer_frame((3, 3, 125, 125), 6, 8, LIGHT)
        p.frame((22, 22, 106, 106), 8, INK)
        p.rect((59, 36, 68, 68), RED)
        p.rect((59, 60, 88, 69), RED)
    elif name == "ui_pip_dot":
        p.ellipse((4, 4, 60, 60), WHITE)
    elif name == "ui_die_shadow":
        # R2 requests a broad, flat poster footprint: 30% black with hard edges.
        p.poly([(20, 22), (209, 22), (239, 87), (226, 105),
                (48, 105), (17, 40)], rgba("000000", 77))
    elif name == "ui_pedestal":
        p.poly([(52, 37), (332, 37), (364, 92), (20, 92)], LIGHT)
        p.poly([(20, 92), (364, 92), (364, 143), (20, 143)], INK)
        p.rect((20, 92, 364, 100), RED)
    elif name == "fx_score_crown":
        # W11 moves this 48x32 mark onto the near-black top bar. A light contour
        # separates its black silhouette from both dark HUD and pale table.
        p.poly([(3, 14), (54, 43), (96, 0), (138, 43), (189, 14),
                (169, 108), (23, 108)], LIGHT)
        p.poly([(14, 30), (57, 56), (96, 10), (135, 56), (178, 30),
                (160, 100), (32, 100)], INK)
        p.rect((32, 108, 160, 119), RED)
    elif name == "fx_arc_short":
        # Angular motion bracket replaces the old curved flare.
        p.poly([(37, 208), (37, 105), (106, 36), (207, 36), (207, 52),
                (113, 52), (53, 112), (53, 208)], WHITE)
    elif name == "fx_score_ray":
        p.poly([(8, 39), (346, 12), (346, 33), (8, 55)], WHITE)
        p.poly([(30, 68), (376, 62), (376, 82), (30, 79)], WHITE)
    elif name == "fx_score_spark":
        p.rect((26, 5, 38, 59), WHITE)
        p.rect((5, 26, 59, 38), WHITE)
    elif name == "fx_square_chip":
        p.rect((70, 70, 186, 186), WHITE)
    else:
        raise ValueError("Unimplemented icon: " + name)
    return p.export()


def card_back() -> Image.Image:
    p = Canvas(768, 1024)
    p.chamfer_frame((0, 0, 768, 1024), 16, 24, PAPER)
    p.rect((40, 40, 728, 984), INK)
    p.poly([(40, 40), (728, 40), (728, 420), (40, 900)], RED)
    p.frame((68, 68, 700, 956), 4, PAPER)
    # A single abstract four-square mark; no rule values or labels are baked in.
    for x, y in ((180, 310), (396, 310), (180, 526), (396, 526)):
        p.rect((x, y, x + 192, y + 192), PAPER)
        p.rect((x + 68, y + 68, x + 124, y + 124), INK)
    p.rect((98, 884, 258, 896), PAPER)
    return p.export()


def wordmark() -> Image.Image:
    p = Canvas(1440, 400)
    # A paper carrier supports the unchanged menu and remains legible on either table.
    p.chamfer_frame((0, 32, 1440, 368), 12, 16, PAPER)
    p.rect((44, 74, 260, 290), RED)
    for x, y in ((77, 107), (167, 107), (77, 197), (167, 197)):
        p.rect((x, y, x + 60, y + 60), PAPER)
    p.text((298, 67), "骰子构筑", 262, INK)
    p.rect((298, 337, 1396, 347), INK)
    return p.export()


def render_sprite(spec: dict) -> Image.Image:
    name = spec["name"]
    if any(spec["border"]):
        return surface(spec)
    if name.startswith("badge_enchant_"):
        return badge(name, tuple(spec["size"]))
    if name.startswith("ui_gem_"):
        return gem(name, tuple(spec["size"]))
    if name == "ui_wordmark_dicecraft":
        return wordmark()
    if name == "ui_card_back":
        return card_back()
    return icon(spec)


def table_scene() -> Image.Image:
    p = Canvas(1920, 1080, PAPER)
    # W11 has transparent trays and a variable card count. Keep their entire
    # lanes plain; Swiss diagonals now occupy the perimeter and opaque panels.
    p.poly([(1260, 8), (1892, 8), (1892, 70)], RED)  # Under the top bar.
    p.poly([(1520, 112), (1872, 112), (1872, 324)], RED)  # Under the stage card.
    p.poly([(1896, 96), (1920, 72), (1920, 1080), (1896, 1056)], RED)
    p.poly([(520, 1074), (1920, 1048), (1920, 1074)], RED)
    p.rect((0, 0, 1920, 6), BLACK)
    p.rect((0, 1074, 1920, 1080), BLACK)
    p.rect((12, 352, 18, 942), BLACK)  # Outer margin, never inside an open tray.
    return p.export().convert("RGB")


def check_felt_zones(table: Image.Image) -> dict[str, int]:
    """Fail on any RGB-channel deviation in the four protected design zones."""
    if table.mode != "RGB" or table.size != (1920, 1080):
        raise ValueError("Table must be RGB 1920x1080 before checking zones")
    pixels = np.asarray(table).astype(np.int16)
    expected = np.array(PAPER[:3], dtype=np.int16)
    results = {}
    for name, (x, y, w, h) in FELT_ZONES.items():
        delta = int(np.abs(pixels[y:y + h, x:x + w] - expected).max())
        results[name] = delta
        passed = delta <= MAX_FELT_RGB_DEVIATION
        print(f"SWISS_ZONE {name} rect={x},{y},{w},{h} max_rgb_delta={delta} "
              f"limit={MAX_FELT_RGB_DEVIATION} {'PASS' if passed else 'FAIL'}")
        if not passed:
            raise ValueError(f"Table decoration entered protected zone {name}: {delta}")
    return results


def felt_tile() -> Image.Image:
    """Stationary paper grain; wrap-filtered at 4x, no seams or lighting gradient."""
    rng = np.random.RandomState(11012)
    noise = rng.randint(-7, 8, (1024 * SS, 1024 * SS)).astype(np.int16)
    raw = np.clip(np.array(PAPER[:3], dtype=np.int16) + noise[..., None], 0, 255).astype(np.uint8)
    # Padding lets Lanczos see periodic neighbors across both texture boundaries.
    padded = np.pad(raw, ((16, 16), (16, 16), (0, 0)), mode="wrap")
    im = Image.fromarray(padded, "RGB").resize((1032, 1032), Image.Resampling.LANCZOS)
    out = np.asarray(im.crop((4, 4, 1028, 1028))).copy()
    out[-1, :] = out[0, :]
    out[:, -1] = out[:, 0]
    return Image.fromarray(out, "RGB")


def preview() -> Image.Image:
    with Image.open(ROOT / REVIEW / "preview-gameplay.png") as frame:
        if frame.size != (1920, 1080):
            raise ValueError("Expected a full 1920x1080 gameplay capture for the theme picker")
        return frame.convert("RGB").resize((480, 270), Image.Resampling.LANCZOS)


def concept_preview() -> Image.Image:
    p = Canvas(480, 270, PAPER)
    p.poly([(474, 30), (480, 22), (480, 270), (474, 262)], RED)
    p.poly([(130, 268), (480, 258), (480, 270), (130, 270)], RED)
    p.rect((0, 0, 480, 2), BLACK)
    p.rect((0, 268, 480, 270), BLACK)
    p.chamfer_frame((8, 8, 472, 34), 2.5, 4, INK)
    p.rect((20, 18, 92, 23), LIGHT)
    p.rect((340, 18, 358, 23), RED)
    p.chamfer_frame((374, 44, 468, 100), 2.5, 4, PAPER)
    p.chamfer_frame((384, 52, 458, 66), 2.5, 4, INK)
    p.chamfer_frame((8, 112, 364, 236), 2.5, 4, PAPER)
    p.chamfer_frame((374, 112, 468, 236), 2.5, 4, INK)
    for x in (8, 98, 188):
        p.chamfer_frame((x, 44, x + 80, 100), 2.5, 4, PAPER)
        p.rect((x + 12, 57, x + 64, 61), INK)
        p.rect((x + 12, 74, x + 50, 78), GREY)
    for x, color in ((20, INK), (82, RED), (144, INK)):
        p.chamfer_frame((x, 208, x + 54, 226), 2.5, 4, color)
    p.chamfer_frame((178, 245, 302, 264), 2.5, 4, RED)
    arrow(p, 230, 248, 12, BLACK)
    die(p, 100, 136, 50)
    die(p, 218, 136, 50)
    p.rect((386, 128, 451, 133), LIGHT)
    p.rect((386, 148, 427, 153), TAN)
    return p.export().convert("RGB")


def nine_slice(im: Image.Image, border, size) -> Image.Image:
    """Offline review only; emulate fixed borders at source pixel density."""
    l, b, r, t = map(int, border)
    w, h = im.size
    tw, th = size
    sx, sy = [0, l, w - r, w], [0, t, h - b, h]
    dx, dy = [0, l, tw - r, tw], [0, t, th - b, th]
    out = Image.new("RGBA", size)
    for j in range(3):
        for i in range(3):
            sw, sh = sx[i + 1] - sx[i], sy[j + 1] - sy[j]
            dw, dh = dx[i + 1] - dx[i], dy[j + 1] - dy[j]
            if min(sw, sh, dw, dh) <= 0:
                continue
            patch = im.crop((sx[i], sy[j], sx[i + 1], sy[j + 1]))
            out.paste(patch.resize((dw, dh), Image.Resampling.LANCZOS), (dx[i], dy[j]))
    return out


def review_sheet(sprites: dict[str, Image.Image], specs: list[dict], table: Image.Image) -> Image.Image:
    """Native sprite pairs plus a small set of large stretched component proofs."""
    cw, ch, columns = 600, 252, 3
    rows = math.ceil(len(specs) / columns)
    sheet = Image.new("RGB", (columns * cw, 152 + rows * ch + 1730), PAPER[:3])
    d = ImageDraw.Draw(sheet)
    font = ImageFont.truetype(str(LATIN), 19)
    small = ImageFont.truetype(str(LATIN), 15)
    title = ImageFont.truetype(str(LATIN), 44)
    d.text((24, 22), "SWISS / UI SYSTEM", fill=INK[:3], font=title)
    d.text((25, 80), f"{len(specs)} contract sprites   /   Default > Swiss   /   Raw textures, neutral masks remain untinted", fill=INK[:3], font=font)
    d.text((25, 111), "Offline source review. Stretch and palette previews below are not Unity screenshots.", fill=INK[:3], font=small)
    for n, spec in enumerate(specs):
        x, y = (n % columns) * cw, 152 + (n // columns) * ch
        d.line((x + 24, y, x + cw - 24, y), fill=INK[:3], width=2)
        d.text((x + 24, y + 9), spec["name"], fill=INK[:3], font=font)
        w, h = spec["size"]
        border = "/".join(str(int(v)) for v in spec["border"])
        neutral = spec["name"] in NEUTRAL_NAMES or spec["name"] == "ui_icon_refresh"
        d.text((x + 24, y + 34), f"{w}x{h}  L/B/R/T {border}  PPU {spec['pixelsPerUnit']:g}  {'TINT' if neutral else 'AUTHORED'}",
               fill=INK[:3], font=small)
        for col in range(2):
            box = (x + 24 + col * 285, y + 63, x + 290 + col * 285, y + 224)
            d.rectangle(box, fill=(111, 110, 107))
            if col == 0:
                with Image.open(ROOT / spec["default"]) as source:
                    im = source.convert("RGBA")
            else:
                im = sprites[spec["name"]].copy()
            im.thumbnail((250, 151), Image.Resampling.LANCZOS)
            sheet.paste(im, (box[0] + (266 - im.width) // 2, box[1] + (161 - im.height) // 2), im)
            d.text((box[0], y + 228), "DEFAULT" if col == 0 else "SWISS", fill=INK[:3], font=small)
    y = 152 + rows * ch
    d.text((24, y + 12), "TABLE / 1920 x 1080   -   clear central play area", fill=INK[:3], font=font)
    sheet.paste(table.resize((960, 540), Image.Resampling.LANCZOS), (24, y + 52))
    d.rectangle((24, y + 52, 984, y + 592), outline=INK[:3], width=2)
    sheet.paste(concept_preview(), (1050, y + 52))
    d.text((1050, y + 337), "PICKER / 480 x 270", fill=INK[:3], font=font)
    for i, (label, color) in enumerate((('PAPER', PAPER), ('INK', INK), ('RED', RED), ('GREY', GREY), ('TAN', TAN))):
        xx = 1050 + i * 135
        d.rectangle((xx, y + 396, xx + 110, y + 454), fill=color[:3])
        d.text((xx, y + 464), label, fill=INK[:3], font=small)
    y += 630
    d.text((24, y), "SLICE PROOFS / paper on paper, at PPU 200   -   10 source px = 5 UI px", fill=INK[:3], font=font)
    lookup = {s['name']: s for s in specs}
    for i, name in enumerate(("ui_paper_panel", "ui_card_controller", "ui_panel_hud", "ui_ring")):
        xx = 24 + i * 440
        d.text((xx, y + 42), name, fill=INK[:3], font=font)
        d.rectangle((xx, y + 74, xx + 410, y + 440), fill=PAPER[:3])
        for size, pos in [((406, 110), (xx + 2, y + 76)), ((170, 244), (xx + 2, y + 192))]:
            im = nine_slice(sprites[name], lookup[name]['border'], (size[0] * 2, size[1] * 2))
            im = im.resize(size, Image.Resampling.LANCZOS)
            if name == "ui_ring":
                a = np.asarray(im).copy()
                a[:, :, :3] = 0
                im = Image.fromarray(a, "RGBA")
            sheet.paste(im, pos, im)
    d.text((24, y + 469), "TINT PROOFS / proposed palette only", fill=INK[:3], font=font)
    for i, (name, tint, label) in enumerate((('ui_button_bevel', RED, 'ACTION'), ('ui_price_chip', RED, 'PRICE'),
                                            ('ui_button_ghost', GREY, 'SECONDARY'), ('ui_ring', INK, 'SLOT'))):
        xx = 24 + i * 440
        im = nine_slice(sprites[name], lookup[name]['border'], (380, 86))
        a = np.asarray(im).copy()
        a[:, :, :3] = (a[:, :, :3].astype(np.uint16) * np.array(tint[:3], dtype=np.uint16) // 255).astype(np.uint8)
        im = Image.fromarray(a, 'RGBA')
        sheet.paste(im, (xx, y + 510), im)
        if i < 2:
            d.text((xx + 18, y + 538), label, fill=BLACK[:3], font=font)
        d.text((xx, y + 610), name, fill=INK[:3], font=small)
    y += 700
    d.text((24, y), "R2 REFERENCES / all five committed concepts - style only; not layout or text contracts", fill=INK[:3], font=font)
    for i, name in enumerate(("roll", "shop", "select", "card", "triptych")):
        x = 24 + i * 355
        with Image.open(ROOT / "ArtSource/StyleKit/themes/swiss/concepts" / (name + ".png")) as source:
            ref = source.convert("RGB")
        ref.thumbnail((336, 220), Image.Resampling.LANCZOS)
        sheet.paste(ref, (x, y + 42))
        d.text((x, y + 270), name.upper(), fill=INK[:3], font=font)
    return sheet


def build(output_root: Path = ROOT) -> list[Path]:
    """Write the current contract plus table, tile, picker, and contact sheet."""
    output_root = Path(output_root).resolve()
    if not output_root.is_relative_to(ROOT):
        raise ValueError("Output root must stay inside the Swiss worktree")
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
    sprites = {}
    for spec in specs:
        im = render_sprite(spec)
        if im.mode != "RGBA" or list(im.size) != spec["size"]:
            raise ValueError("Contract mismatch: " + spec["name"])
        sprites[spec["name"]] = im
        save(THEME / "UI" / (spec["name"] + ".png"), im)
    save(THEME / "Table/table_scene.png", table)
    save(THEME / "Table/felt_tile.png", felt_tile())
    save(THEME / "preview.png", preview())
    descriptor = output_root / THEME / "theme.json"
    descriptor.write_text(theme_json(), encoding="utf-8", newline="\n")
    written.append(descriptor)
    save(REVIEW / "contact_sheet.png", review_sheet(sprites, specs, table))
    return written


def build_card_template(output_root: Path = ROOT) -> list[Path]:
    """W13: write only the card template's four sprites into the theme's UI folder.

    The rest of the kit and the review sheet (ArtSource/Art11/swiss) are left as
    they are; a full build() regenerates everything, these four included."""
    output_root = Path(output_root).resolve()
    specs = [s for s in contract_sprites() if s["name"] in CARD_TEMPLATE_NAMES]
    if sorted(s["name"] for s in specs) != sorted(CARD_TEMPLATE_NAMES):
        raise ValueError("The contract lacks the card template sprites: integrate them in Unity "
                         "(V11Setup.Integrate) and run theme_contract.py contract first")
    written = []
    for spec in specs:
        im = render_sprite(spec)
        if im.mode != "RGBA" or list(im.size) != spec["size"]:
            raise ValueError("Contract mismatch: " + spec["name"])
        path = output_root / THEME / "UI" / (spec["name"] + ".png")
        path.parent.mkdir(parents=True, exist_ok=True)
        im.save(path, format="PNG", optimize=False, compress_level=9)
        written.append(path)
    return written


if __name__ == "__main__":
    import sys
    if "--card-template" in sys.argv:
        for path in build_card_template():
            print("SWISS_CARD_TEMPLATE", path.relative_to(ROOT).as_posix())
        raise SystemExit(0)
    paths = build()
    specs = contract_sprites()
    print(f"SWISS_BUILT {len(specs)} UI sprites ({sum(s['referenced'] for s in specs)} referenced, "
          f"{sum(not s['referenced'] for s in specs)} legacy) / {len(paths)} output files / supersampling {SS}x")
