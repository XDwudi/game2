"""v0.73 stepped-corner surfaces, shop pieces, dice tags and high-score FX. Deterministic.

Shape language from the user's 2026-11-12 reference images: two-step notched corners, thick dark
bases, slanted section headers, gold price chips. Everything is flat vector work on a 4x canvas
reduced with Lanczos: crisp at native resolution, no blur, no low-resolution upscaling.

Outputs under DicePrototype/Assets/Art/Art073:
  Surfaces/  nine-slice plates (PPU 200). 512x512 with a 40 px border unless listed in BORDERS.
  UI/        icons, tags, pedestal (PPU 100, not sliced)
  FX/        high-score crown, ray, spark (PPU 100, not sliced)
Backgrounds, word mark, card back, pip dot and progress bars stay in Art072.
"""
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

import importlib.util

# v0.72 canvas helpers and palette. Loaded under its own name: both generators are called build_art.py.
_spec = importlib.util.spec_from_file_location("art072_build_art", Path(__file__).resolve().parents[1] / "art_v072" / "build_art.py")
base = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(base)
(BLUE, BLUE_DARK, GOLD, GOLD_DARK, GOLD_HI, GREEN_DARK, GREY, INK, INK_DEEP, PAPER, PAPER_EDGE, PAPER_HI, PAPER_LINE, PAPER_LO, PAPER_SOFT,
 RED, SHADOW, TEAL_DEEP, TEAL_EDGE, TEAL_HI, TEAL_PANEL, WHITE, Plate, rgb) = (
    base.BLUE, base.BLUE_DARK, base.GOLD, base.GOLD_DARK, base.GOLD_HI, base.GREEN_DARK, base.GREY, base.INK, base.INK_DEEP, base.PAPER,
    base.PAPER_EDGE, base.PAPER_HI, base.PAPER_LINE, base.PAPER_LO, base.PAPER_SOFT, base.RED, base.SHADOW, base.TEAL_DEEP, base.TEAL_EDGE,
    base.TEAL_HI, base.TEAL_PANEL, base.WHITE, base.Plate, base.rgb)

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "DicePrototype" / "Assets" / "Art" / "Art073"
N = 512
STEP = 14            # px per stair at PPU 200 = 7 reference units
# Non-default nine-slice borders in pixels: (left, bottom, right, top). Mirrored by V073Setup.
BORDERS = {"ui_section_header": (40, 0, 72, 0), "ui_tray_front": (40, 0, 40, 0), "fx_score_plate": (104, 104, 104, 104), "fx_formula_chip": (48, 0, 48, 0)}

BRASS, BRASS_HI, BRASS_LO = rgb("C98A3A"), rgb("EDBE6C"), rgb("8F5A22")
TRAY, TRAY_HI, TRAY_LO = rgb("2A3336"), rgb("4A5A60"), rgb("1A2124")
STONE_TOP, STONE_LEFT, STONE_RIGHT, STONE_HI = rgb("3E8F88"), rgb("235B57"), rgb("17403E"), rgb("6FC2B8")


def stair(x0, y0, x1, y1, s=STEP, n=2):
    """Clockwise outline of a rectangle whose corners are cut in n square steps of size s."""
    pts = [(x0, y0 + n * s)]
    for i in range(n):
        pts += [(x0 + (i + 1) * s, y0 + (n - i) * s), (x0 + (i + 1) * s, y0 + (n - i - 1) * s)]
    pts.append((x1 - n * s, y0))
    for i in range(n):
        pts += [(x1 - (n - i) * s, y0 + (i + 1) * s), (x1 - (n - i - 1) * s, y0 + (i + 1) * s)]
    pts.append((x1, y1 - n * s))
    for i in range(n):
        pts += [(x1 - (i + 1) * s, y1 - (n - i) * s), (x1 - (i + 1) * s, y1 - (n - i - 1) * s)]
    pts.append((x0 + n * s, y1))
    for i in range(n):
        pts += [(x0 + (n - i) * s, y1 - (i + 1) * s), (x0 + (n - i - 1) * s, y1 - (i + 1) * s)]
    return pts


def mask_of(plate, points):
    mask = Image.new("L", plate.im.size, 0)
    ImageDraw.Draw(mask).polygon([(x * plate.ss, y * plate.ss) for x, y in points], fill=255)
    return mask


def paint(plate, mask, color):
    layer = Image.new("RGBA", plate.im.size, tuple(color[:3]) + (0,))
    layer.putalpha(mask.point(lambda v: v * color[3] // 255))
    plate.im.alpha_composite(layer)


def band(plate, box, width, s=STEP, n=2):
    """Mask of the strip between a stair outline and the same outline moved inward by width."""
    x0, y0, x1, y1 = box
    outer = np.asarray(mask_of(plate, stair(x0, y0, x1, y1, s, n)))
    inner = np.asarray(mask_of(plate, stair(x0 + width, y0 + width, x1 - width, y1 - width, s, n)))
    return Image.fromarray(np.where(inner > 0, 0, outer).astype(np.uint8), "L")


def sfill(plate, box, color, s=STEP, n=2):
    paint(plate, mask_of(plate, stair(*box, s, n)), color)


def sline(plate, box, width, color, s=STEP, n=2):
    paint(plate, band(plate, box, width, s, n), color)


def ssplit(plate, box, width, top_left, bottom_right, s=STEP, n=2):
    """Bevel line: one colour on the two edges facing the light (top/left), another on the far two."""
    x0, y0, x1, y1 = box
    ring = np.asarray(band(plate, box, width, s, n))
    yy, xx = np.mgrid[0:plate.im.size[1], 0:plate.im.size[0]]
    k = plate.ss
    lit = (xx - x0 * k) / max(1, (x1 - x0) * k) + (yy - y0 * k) / max(1, (y1 - y0) * k) < 1.0
    for region, color in ((lit, top_left), (~lit, bottom_right)):
        if color is not None:
            paint(plate, Image.fromarray(np.where(region, ring, 0).astype(np.uint8), "L"), color)


def inset(box, d):
    return (box[0] + d, box[1] + d, box[2] - d, box[3] - d)


def save(image, group, name):
    path = OUT / group / (name + ".png")
    path.parent.mkdir(parents=True, exist_ok=True)
    (image.export() if isinstance(image, Plate) else image).save(path, optimize=True)


# ---------------------------------------------------------------- nine-slice surfaces
def paper(print_line=None, fill=PAPER, s=STEP, edge=5, shadow=(6, 8, 105), double=False):
    p = Plate(N, N)
    body = (2, 2, N - 2 - shadow[0], N - 2 - shadow[1])
    sfill(p, (body[0] + shadow[0], body[1] + shadow[1], body[2] + shadow[0], body[3] + shadow[1]), SHADOW[:3] + (shadow[2],), s)
    sfill(p, body, fill, s)
    ssplit(p, inset(body, edge), 3, PAPER_HI, PAPER_LO, s)
    if print_line:
        sline(p, inset(body, 17), 2.5, print_line, 8)
    if double:
        sline(p, inset(body, 23), 1.5, PAPER_EDGE[:3] + (150,), 6)
    sline(p, body, edge, INK, s)
    return p


def soft_well(fill, s=10):
    """v0.72 rc.2 recessed readout well, cut to the stepped outline."""
    shaded = base.soft_inset(fill, radius=6)
    p = Plate(N, N)
    alpha = mask_of(p, stair(0, 0, N, N, s)).resize((N, N), Image.LANCZOS)
    shaded.putalpha(alpha)
    return shaded


def surfaces():
    out = {}
    out["ui_paper_panel"] = paper()
    out["ui_card_controller"] = paper(PAPER_LINE)
    out["ui_card_controller_compact"] = paper(PAPER_LINE)
    out["ui_card_item"] = paper(BLUE_DARK[:3] + (190,))
    out["ui_card_upgrade"] = paper(GREEN_DARK[:3] + (190,))
    out["ui_card_shop"] = paper(PAPER_LINE, double=True)

    modal = paper(s=16, edge=6, shadow=(9, 12, 125))
    modal.line(40, 15, N - 51, 19, GOLD)
    modal.line(40, 19, N - 51, 21, GOLD_DARK)
    out["ui_panel_modal"] = modal
    out["ui_tooltip_frame"] = paper(fill=PAPER_SOFT, s=10, edge=4, shadow=(4, 6, 105))

    # One tintable plate serves the button face and its darker base (drawn 8 units lower by UiKit).
    button = Plate(N, N)
    box = (2, 2, N - 2, N - 2)
    sfill(button, box, WHITE)
    ssplit(button, inset(box, 5), 5, None, GREY)
    sline(button, box, 5, INK)
    out["ui_button_bevel"] = button

    chip = Plate(N, N)
    box = (0, 0, N, N)
    sfill(chip, box, WHITE, 8)
    ssplit(chip, inset(box, 4), 4, None, GREY, 8)
    sline(chip, box, 4, INK, 8)
    out["ui_price_chip"] = chip

    out["ui_info_inset"] = soft_well(rgb("0B1E21"))

    hud = Plate(N, N)
    sx, sy = 5, 7
    body = (2, 2, N - 2 - sx, N - 2 - sy)
    sfill(hud, (2 + sx, 2 + sy, N - 2, N - 2), SHADOW[:3] + (120,))
    sfill(hud, body, rgb("102A2D"))
    ssplit(hud, inset(body, 5), 2.5, TEAL_EDGE, INK_DEEP)
    sline(hud, body, 5, INK_DEEP)
    out["ui_panel_hud"] = hud

    section = Plate(N, N)
    box = (0, 0, N, N)
    sfill(section, box, rgb("0D2226", 236), 12)
    sline(section, inset(box, 4), 2.5, TEAL_EDGE[:3] + (170,), 12)
    sline(section, inset(box, 9), 1.5, INK_DEEP[:3] + (200,), 10)
    sline(section, box, 4, INK_DEEP, 12)
    out["ui_section_panel"] = section

    ghost = Plate(N, N)
    sfill(ghost, (0, 0, N, N), (255, 255, 255, 30), 10)
    sline(ghost, (0, 0, N, N), 4, (255, 255, 255, 205), 10)
    out["ui_button_ghost"] = ghost

    ring = Plate(N, N)
    sline(ring, (0, 0, N, N), 5, WHITE, 12)
    out["ui_ring"] = ring

    enchant = Plate(N, N)                       # sits just inside the ink edge of a paper card
    body = (2, 2, N - 8, N - 10)
    sline(enchant, inset(body, 5), 6, WHITE)
    sline(enchant, inset(body, 13), 1.5, (255, 255, 255, 120), 10)
    out["ui_frame_enchant"] = enchant

    tab = Plate(N, N)                           # docked to the bottom rim: only the top corners are cut
    sfill(tab, (0, 0, N, N + 80), rgb("0F2629", 240), 12)
    sline(tab, (4, 4, N - 4, N + 80), 2.5, TEAL_EDGE[:3] + (150,), 12)
    sline(tab, (0, 0, N, N + 80), 4, INK_DEEP, 12)
    out["ui_tab_bottom"] = tab

    plain = Plate(N, N)
    sfill(plain, (0, 0, N, N), TEAL_PANEL, 12)
    ssplit(plain, (4, 4, N - 4, N - 4), 2.5, TEAL_EDGE, TEAL_DEEP, 12)
    sline(plain, (0, 0, N, N), 4, INK_DEEP, 12)
    out["ui_panel_plain"] = plain

    label = Plate(N, N)
    sfill(label, (0, 0, N, N), PAPER, 8)
    ssplit(label, (3, 3, N - 3, N - 3), 2.5, PAPER_HI, PAPER_LO, 8)
    sline(label, (0, 0, N, N), 3, PAPER_EDGE, 8)
    out["ui_chip_label"] = label

    slot = Plate(N, N)
    sfill(slot, (0, 0, N, N), TEAL_DEEP[:3] + (120,), 10)
    ssplit(slot, (0, 0, N, N), 3, INK_DEEP[:3] + (200,), TEAL_EDGE, 10)
    out["ui_slot_dice"] = slot

    inventory = Plate(N, N)
    sfill(inventory, (0, 0, N, N), TEAL_DEEP[:3] + (95,), 10)
    sline(inventory, (0, 0, N, N), 2, TEAL_EDGE[:3] + (150,), 10)
    corner = Plate(N, N)
    sline(corner, (0, 0, N, N), 4, TEAL_HI, 10)
    a = np.asarray(corner.im).copy()
    yy, xx = np.mgrid[0:N * corner.ss, 0:N * corner.ss]
    reach = 34 * corner.ss
    near = ((xx < reach) | (xx >= N * corner.ss - reach)) & ((yy < reach) | (yy >= N * corner.ss - reach))
    a[~near] = 0
    inventory.im.alpha_composite(Image.fromarray(a, "RGBA"))
    out["ui_slot_inventory"] = inventory

    focus = Plate(N, N)
    sline(focus, (0, 0, N, N), 5, PAPER_SOFT)
    out["ui_focus_frame"] = focus

    handle = Plate(N, N)
    sfill(handle, (0, 0, N, N), WHITE, 8)
    out["ui_scroll_handle"] = handle

    block = Plate(N, N)
    sfill(block, (0, 0, N, N), WHITE, 8)
    out["ui_frame_block"] = block
    return out


def section_header():
    """Tintable label band: stepped top-left corner, slanted right end."""
    w, h = 512, 128
    p = Plate(w, h)
    s = 12
    shape = [(0, 2 * s), (s, 2 * s), (s, s), (2 * s, s), (2 * s, 0), (w - 60, 0), (w - 4, h), (0, h)]
    paint(p, mask_of(p, shape), WHITE)
    low = [(0, h - 12), (w - 9, h - 12), (w - 4, h), (0, h)]
    paint(p, mask_of(p, low), GREY)
    return p


def tray_front():
    """Front lip of the dark tray a locked die sits in. The slot number is printed on it by the UI."""
    w, h = 512, 160
    p = Plate(w, h)
    s = 14
    shape = [(0, 0), (w, 0), (w, h - 2 * s), (w - s, h - 2 * s), (w - s, h - s), (w - 2 * s, h - s), (w - 2 * s, h),
             (2 * s, h), (2 * s, h - s), (s, h - s), (s, h - 2 * s), (0, h - 2 * s)]
    paint(p, mask_of(p, shape), INK_DEEP)
    inner = [(5, 5), (w - 5, 5), (w - 5, h - 2 * s - 3), (w - s - 5, h - 2 * s - 3), (w - s - 5, h - s - 3), (w - 2 * s - 3, h - s - 3),
             (w - 2 * s - 3, h - 5), (2 * s + 3, h - 5), (2 * s + 3, h - s - 3), (s + 5, h - s - 3), (s + 5, h - 2 * s - 3), (5, h - 2 * s - 3)]
    paint(p, mask_of(p, inner), TRAY)
    p.line(5, 5, w - 5, 17, TRAY_HI)
    p.line(5, 17, w - 5, 21, TRAY_LO)
    return p


def score_plate():
    """High-score plate: dark field inside a stepped burst outline. White lines take the tier tint."""
    w, h = 768, 384
    p = Plate(w, h)
    box = (6, 6, w - 6, h - 6)
    sfill(p, box, rgb("081619", 238), 14, 6)
    sline(p, box, 7, WHITE, 14, 6)
    sline(p, inset(box, 15), 2.5, (255, 255, 255, 110), 14, 5)
    return p


def formula_chip():
    w, h = 512, 128
    p = Plate(w, h)
    sfill(p, (0, 0, w, h), rgb("081619", 240), 12)
    sline(p, inset((0, 0, w, h), 5), 3, GOLD, 10)
    sline(p, (0, 0, w, h), 4, INK_DEEP, 12)
    return p


# ---------------------------------------------------------------- icons, tags, shop pieces
def pedestal():
    w, h = 384, 192
    p = Plate(w, h)
    p.ellipse(192, 150, 176, 34, SHADOW[:3] + (70,))
    p.ellipse(192, 150, 150, 26, SHADOW[:3] + (110,))
    top = [(40, 66), (192, 16), (344, 66), (192, 116)]
    left = [(40, 66), (192, 116), (192, 158), (40, 108)]
    right = [(192, 116), (344, 66), (344, 108), (192, 158)]
    p.polygon(left, fill=STONE_LEFT)
    p.polygon(right, fill=STONE_RIGHT)
    p.polygon(top, fill=STONE_TOP)
    p.polygon([(70, 66), (192, 26), (314, 66), (192, 106)], outline=STONE_HI[:3] + (150,), width=3)
    for face in (left, right, top):
        p.polygon(face, outline=INK_DEEP, width=5)
    return p


def die_shadow():
    p = Plate(256, 128)
    p.ellipse(128, 64, 120, 44, SHADOW[:3] + (60,))
    p.ellipse(128, 64, 96, 32, SHADOW[:3] + (105,))
    return p


def tag_lock():
    p = Plate(128, 128)
    for width, color in ((22, INK), (11, BRASS_HI)):              # shackle: ink then metal
        s = p.ss
        layer = Image.new("RGBA", p.im.size, (0, 0, 0, 0))
        d = ImageDraw.Draw(layer)
        d.arc([34 * s, 8 * s, 94 * s, 76 * s], 180, 360, fill=color, width=width * s)
        d.rectangle([34 * s, 42 * s, (34 + width) * s - 1, 66 * s], fill=color)
        d.rectangle([(94 - width) * s, 42 * s, 94 * s - 1, 66 * s], fill=color)
        p.im.alpha_composite(layer)
    body = (18, 56, 110, 122)
    sfill(p, body, INK, 8)
    sfill(p, inset(body, 5), BRASS, 6)
    ssplit(p, inset(body, 5), 5, BRASS_HI, BRASS_LO, 6)
    p.ellipse(64, 82, 9, 9, INK)
    p.polygon([(59, 86), (69, 86), (72, 106), (56, 106)], fill=INK)
    return p


def tag_clock():
    p = Plate(128, 128)
    body = (10, 22, 118, 124)
    sfill(p, body, INK, 10)
    sfill(p, inset(body, 5), PAPER, 8)
    ssplit(p, inset(body, 5), 3, PAPER_HI, PAPER_LO, 8)
    p.ellipse(34, 20, 15, 15, None, outline=INK, width=6)          # hanging ring through the tag
    p.ellipse(34, 36, 5, 5, INK)
    p.ellipse(68, 78, 33, 33, PAPER_HI, outline=INK, width=6)
    for k in range(12):
        a = k * math.pi / 6
        r0 = 22 if k % 3 == 0 else 25
        x0, y0, x1, y1 = 68 + math.sin(a) * r0, 78 - math.cos(a) * r0, 68 + math.sin(a) * 28, 78 - math.cos(a) * 28
        p.polygon([(x0, y0), (x1, y1)], outline=INK, width=3 if k % 3 == 0 else 2)
    p.polygon([(68, 78), (56, 64)], outline=INK, width=6)          # 10
    p.polygon([(68, 78), (85, 60)], outline=RED, width=5)          # :10
    p.ellipse(68, 78, 5, 5, INK)
    return p


def badge_check():
    p = Plate(96, 96)
    p.ellipse(48, 48, 44, 44, INK)
    p.ellipse(48, 48, 38, 38, GOLD)
    p.ellipse(48, 48, 38, 38, None, outline=GOLD_HI, width=4)
    stroke(p, [(27, 49), (43, 64), (70, 33)], INK, 11)
    return p


def stroke(plate, points, color, width):
    """Open polyline with round joints and caps."""
    s = plate.ss
    layer = Image.new("RGBA", plate.im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)
    pts = [(x * s, y * s) for x, y in points]
    d.line(pts, fill=color, width=int(round(width * s)), joint="curve")
    r = width * s / 2
    for x, y in pts:
        d.ellipse([x - r, y - r, x + r, y + r], fill=color)
    plate.im.alpha_composite(layer)


def icon_coin():
    p = Plate(96, 96)
    p.ellipse(48, 48, 45, 45, INK)
    p.ellipse(48, 48, 39, 39, GOLD)
    p.ellipse(48, 48, 28, 28, None, outline=GOLD_DARK, width=5)
    stroke(p, [(26, 34), (34, 24), (46, 19)], GOLD_HI, 6)
    p.polygon([(48, 34), (62, 48), (48, 62), (34, 48)], fill=GOLD_DARK)
    p.polygon([(48, 41), (55, 48), (48, 55), (41, 48)], fill=GOLD_HI)
    return p


def icon_dice_view():
    p = Plate(128, 128)
    emblem = base.die_emblem(96, 7).resize((96 * p.ss, 96 * p.ss), Image.LANCZOS)
    p.im.alpha_composite(emblem, (2 * p.ss, 4 * p.ss))
    stroke(p, [(96, 98), (118, 120)], INK, 17)
    stroke(p, [(98, 100), (116, 118)], BRASS, 9)
    p.ellipse(82, 84, 30, 30, INK)
    p.ellipse(82, 84, 23, 23, rgb("CFEFF4"))
    stroke(p, [(68, 80), (74, 70), (84, 66)], WHITE, 5)
    return p


def icon_gear():
    p = Plate(96, 96)
    for k in range(8):
        a = k * math.pi / 4
        c, s = math.cos(a), math.sin(a)
        quad = [(48 + c * 24 - s * 9, 48 + s * 24 + c * 9), (48 + c * 44 - s * 7, 48 + s * 44 + c * 7),
                (48 + c * 44 + s * 7, 48 + s * 44 - c * 7), (48 + c * 24 + s * 9, 48 + s * 24 - c * 9)]
        p.polygon(quad, fill=WHITE)
    p.ellipse(48, 48, 32, 32, WHITE)
    hole = Image.new("L", p.im.size, 0)
    ImageDraw.Draw(hole).ellipse([(48 - 13) * p.ss, (48 - 13) * p.ss, (48 + 13) * p.ss, (48 + 13) * p.ss], fill=255)
    a = np.asarray(p.im).copy()
    a[np.asarray(hole) > 0] = 0
    p.im = Image.fromarray(a, "RGBA")
    return p


def icon_book():
    p = Plate(96, 96)
    p.polygon([(8, 22), (44, 14), (44, 78), (8, 84)], fill=WHITE)
    p.polygon([(52, 14), (88, 22), (88, 84), (52, 78)], fill=WHITE)
    p.polygon([(44, 14), (52, 14), (52, 80), (44, 80)], fill=GREY)
    for y in (32, 44, 56):
        stroke(p, [(17, y + 3), (36, y)], (150, 150, 150, 255), 4)
        stroke(p, [(60, y), (79, y + 3)], (150, 150, 150, 255), 4)
    return p


def icon_arrow():
    p = Plate(96, 96)
    stroke(p, [(34, 18), (64, 48), (34, 78)], WHITE, 16)
    return p


# ---------------------------------------------------------------- high-score FX
def score_ray():
    """Four streaks growing to the right; mirrored by the UI for the left side."""
    w, h = 384, 96
    p = Plate(w, h)
    rows = ((8, 250, 12), (30, 384, 16), (54, 320, 14), (78, 190, 10))
    for y, length, thick in rows:
        third = length / 3
        p.line(0, y, third, y + thick, WHITE)
        p.line(third, y + 2, 2 * third, y + thick - 2, WHITE)
        p.line(2 * third, y + 4, length - 14, y + thick - 4, (255, 255, 255, 215))
        p.line(length - 10, y + 4, length, y + thick - 4, (255, 255, 255, 150))
    return p


def score_crown():
    w, h = 192, 128
    p = Plate(w, h)
    crown = [(22, 104), (14, 40), (58, 70), (96, 16), (134, 70), (178, 40), (170, 104)]
    p.polygon(crown, fill=GOLD)
    p.polygon([(22, 104), (170, 104), (168, 118), (24, 118)], fill=GOLD_DARK)
    stroke(p, [(40, 84), (60, 84)], GOLD_HI, 6)
    p.polygon(crown + [(168, 118), (24, 118)], outline=INK, width=7)
    for cx, cy, color in ((14, 38, RED), (96, 14, PAPER_HI), (178, 38, BLUE)):
        p.ellipse(cx, cy, 11, 11, INK)
        p.ellipse(cx, cy, 6.5, 6.5, color)
    p.polygon([(96, 78), (108, 92), (96, 106), (84, 92)], fill=RED)
    p.polygon([(96, 78), (108, 92), (96, 106), (84, 92)], outline=INK, width=4)
    return p


def score_spark():
    p = Plate(64, 64)
    p.polygon([(32, 4), (44, 32), (32, 60), (20, 32)], fill=WHITE)
    p.polygon([(4, 32), (32, 24), (60, 32), (32, 40)], fill=WHITE)
    return p


def main():
    count = 0
    plates = surfaces()
    plates.update({"ui_section_header": section_header(), "ui_tray_front": tray_front(), "fx_score_plate": score_plate(), "fx_formula_chip": formula_chip()})
    for name, plate in plates.items():
        save(plate, "Surfaces", name)
        count += 1
    for name, plate in {"ui_pedestal": pedestal(), "ui_die_shadow": die_shadow(), "ui_tag_lock": tag_lock(), "ui_tag_clock": tag_clock(),
                        "ui_badge_check": badge_check(), "ui_icon_coin": icon_coin(), "ui_icon_dice_view": icon_dice_view(),
                        "ui_icon_gear": icon_gear(), "ui_icon_book": icon_book(), "ui_icon_arrow": icon_arrow()}.items():
        save(plate, "UI", name)
        count += 1
    for name, plate in {"fx_score_ray": score_ray(), "fx_score_crown": score_crown(), "fx_score_spark": score_spark()}.items():
        save(plate, "FX", name)
        count += 1
    print("ART073_BUILT", count)


if __name__ == "__main__":
    main()
