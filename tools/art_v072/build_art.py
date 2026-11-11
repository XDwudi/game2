"""v0.72 HD surfaces, table and emblems. Deterministic; rerun to regenerate every file.

Everything is drawn as flat vector shapes on a supersampled canvas and reduced with Lanczos, so
edges are sharp and evenly antialiased at the exported size. No blur, no noise in sliced centres.

Outputs (same sprite names the game already binds by name):
  Surfaces/  512x512 nine-slice plates, PPU 200, border 40 px (20 reference units)
  Surfaces/  ui_progress_*  768x96, border 48 left/right
  UI/        ui_wordmark_dicecraft 1440x400, ui_card_back 768x1024, ui_pip_dot 64x64
  Backgrounds/ bg_table_scene 1920x1080, bg_felt_tile 1024x1024
"""
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "DicePrototype" / "Assets" / "Art" / "Art072"
FONT = ROOT / "DicePrototype" / "Assets" / "Game" / "Fonts" / "NotoSansSC-Bold.otf"
SS = 4


def rgb(value, alpha=255):
    return tuple(int(value[i:i + 2], 16) for i in (0, 2, 4)) + (alpha,)


INK, INK_DEEP, SHADOW = rgb("162629"), rgb("0B1A1D"), rgb("07191C")
PAPER, PAPER_HI, PAPER_LO, PAPER_LINE, PAPER_EDGE = rgb("F4E8CB"), rgb("FFF8E7"), rgb("E3D3AE"), rgb("D9C7A0"), rgb("B09A78")
PAPER_SOFT = rgb("F7EED9")
TEAL_DEEP, TEAL_PANEL, TEAL_EDGE, TEAL_HI = rgb("0F2629"), rgb("1A3A3E"), rgb("315358"), rgb("4A7378")
RED, BLUE, BLUE_DARK, GREEN_DARK = rgb("EC655D"), rgb("55B6D9"), rgb("2F7FA8"), rgb("3F8F6B")
GOLD, GOLD_DARK, GOLD_HI = rgb("F3BA58"), rgb("B56B29"), rgb("FFD98A")
WOOD, WOOD_HI, WOOD_LO = rgb("5E3A22"), rgb("8A5A32"), rgb("3B2417")
WHITE, GREY = (255, 255, 255, 255), (226, 226, 226, 255)


class Plate:
    """Supersampled RGBA canvas addressed in export pixels."""

    def __init__(self, w, h, ss=SS):
        self.w, self.h, self.ss = w, h, ss
        self.im = Image.new("RGBA", (w * ss, h * ss), (0, 0, 0, 0))

    def _layer(self):
        layer = Image.new("RGBA", self.im.size, (0, 0, 0, 0))
        return layer, ImageDraw.Draw(layer)

    def rrect(self, x0, y0, x1, y1, radius, fill=None, outline=None, width=0):
        s = self.ss
        layer, d = self._layer()
        d.rounded_rectangle([x0 * s, y0 * s, x1 * s - 1, y1 * s - 1], radius=radius * s, fill=fill, outline=outline, width=int(round(width * s)))
        self.im.alpha_composite(layer)

    def ring_split(self, x0, y0, x1, y1, radius, width, top_left, bottom_right):
        """Inner bevel line: one colour toward the light (top/left), another away from it."""
        s = self.ss
        mask = Image.new("L", self.im.size, 0)
        ImageDraw.Draw(mask).rounded_rectangle([x0 * s, y0 * s, x1 * s - 1, y1 * s - 1], radius=radius * s, outline=255, width=int(round(width * s)))
        m = np.asarray(mask) > 0
        yy, xx = np.mgrid[0:self.im.size[1], 0:self.im.size[0]]
        lit = (xx - x0 * s) / max(1, (x1 - x0) * s) + (yy - y0 * s) / max(1, (y1 - y0) * s) < 1.0
        a = np.asarray(self.im).copy()
        for region, color in ((m & lit, top_left), (m & ~lit, bottom_right)):
            if color is not None:
                layer = np.zeros_like(a)
                layer[region] = color
                img = Image.fromarray(a, "RGBA")
                img.alpha_composite(Image.fromarray(layer, "RGBA"))
                a = np.asarray(img).copy()
        self.im = Image.fromarray(a, "RGBA")

    def line(self, x0, y0, x1, y1, color):
        s = self.ss
        layer, d = self._layer()
        d.rectangle([x0 * s, y0 * s, x1 * s - 1, y1 * s - 1], fill=color)
        self.im.alpha_composite(layer)

    def polygon(self, points, fill=None, outline=None, width=0):
        s = self.ss
        layer, d = self._layer()
        pts = [(x * s, y * s) for x, y in points]
        if fill:
            d.polygon(pts, fill=fill)
        if outline:
            d.line(pts + [pts[0]], fill=outline, width=int(round(width * s)), joint="curve")
            r = width * s / 2
            for x, y in pts:
                d.ellipse([x - r, y - r, x + r, y + r], fill=outline)
        self.im.alpha_composite(layer)

    def ellipse(self, cx, cy, rx, ry, fill, outline=None, width=0):
        s = self.ss
        layer, d = self._layer()
        d.ellipse([(cx - rx) * s, (cy - ry) * s, (cx + rx) * s, (cy + ry) * s], fill=fill, outline=outline, width=int(round(width * s)))
        self.im.alpha_composite(layer)

    def paste(self, other, x, y):
        self.im.alpha_composite(other.im if isinstance(other, Plate) else other, (int(x * self.ss), int(y * self.ss)))

    def export(self):
        return self.im.resize((self.w, self.h), Image.LANCZOS)


def save(image, group, name):
    path = OUT / group / (name + ".png")
    path.parent.mkdir(parents=True, exist_ok=True)
    (image.export() if isinstance(image, Plate) else image).save(path, optimize=True)


# ---------------------------------------------------------------- nine-slice surfaces
N = 512


def paper(print_line=None, fill=PAPER, radius=22, edge=5, shadow=(6, 8, 105)):
    p = Plate(N, N)
    x1, y1 = N - 2 - shadow[0], N - 2 - shadow[1]
    p.rrect(2 + shadow[0], 2 + shadow[1], x1 + shadow[0], y1 + shadow[1], radius, fill=SHADOW[:3] + (shadow[2],))
    p.rrect(2, 2, x1, y1, radius, fill=fill)
    p.ring_split(2 + edge, 2 + edge, x1 - edge, y1 - edge, radius - edge, 3, PAPER_HI, PAPER_LO)
    if print_line:
        p.rrect(20, 20, x1 - 18, y1 - 18, 10, outline=print_line, width=2.5)
    p.rrect(2, 2, x1, y1, radius, outline=INK, width=edge)
    return p


def surfaces():
    out = {}
    out["ui_paper_panel"] = paper()
    out["ui_card_controller"] = paper(PAPER_LINE)
    out["ui_card_controller_compact"] = paper(PAPER_LINE)
    out["ui_card_item"] = paper(BLUE_DARK[:3] + (190,))
    out["ui_card_upgrade"] = paper(GREEN_DARK[:3] + (190,))

    modal = paper(radius=24, edge=6, shadow=(9, 12, 125))
    modal.line(26, 15, N - 37, 19, GOLD)
    modal.line(26, 19, N - 37, 21, GOLD_DARK)
    out["ui_panel_modal"] = modal

    out["ui_tooltip_frame"] = paper(fill=PAPER_SOFT, radius=16, edge=4, shadow=(4, 6, 105))

    button = Plate(N, N)
    button.rrect(2, 2, N - 2, N - 2, 18, fill=WHITE)
    button.ring_split(7, 7, N - 7, N - 7, 13, 4, None, GREY)
    button.rrect(2, 2, N - 2, N - 2, 18, outline=INK, width=5)
    out["ui_button_bevel"] = button

    inset = Plate(N, N)
    inset.rrect(0, 0, N, N, 16, fill=TEAL_DEEP)
    inset.ring_split(0, 0, N, N, 16, 4, INK_DEEP, TEAL_EDGE[:3] + (190,))
    out["ui_info_inset"] = inset

    plain = Plate(N, N)
    plain.rrect(0, 0, N, N, 18, fill=TEAL_PANEL)
    plain.ring_split(4, 4, N - 4, N - 4, 14, 2.5, TEAL_EDGE, TEAL_DEEP)
    plain.rrect(0, 0, N, N, 18, outline=INK_DEEP, width=4)
    out["ui_panel_plain"] = plain

    chip = Plate(N, N)
    chip.rrect(0, 0, N, N, 14, fill=PAPER)
    chip.ring_split(3, 3, N - 3, N - 3, 11, 2.5, PAPER_HI, PAPER_LO)
    chip.rrect(0, 0, N, N, 14, outline=PAPER_EDGE, width=3)
    out["ui_chip_label"] = chip

    slot = Plate(N, N)
    slot.rrect(0, 0, N, N, 16, fill=TEAL_DEEP[:3] + (120,))
    slot.ring_split(0, 0, N, N, 16, 3, INK_DEEP[:3] + (200,), TEAL_EDGE)
    out["ui_slot_dice"] = slot

    inventory = Plate(N, N)
    inventory.rrect(0, 0, N, N, 16, fill=TEAL_DEEP[:3] + (95,), outline=TEAL_EDGE[:3] + (150,), width=2)
    corner = Plate(N, N)
    corner.rrect(0, 0, N, N, 16, outline=TEAL_HI, width=4)
    a = np.asarray(corner.im).copy()
    yy, xx = np.mgrid[0:N * SS, 0:N * SS]
    reach = 34 * SS
    near = ((xx < reach) | (xx >= N * SS - reach)) & ((yy < reach) | (yy >= N * SS - reach))
    a[~near] = 0
    inventory.im.alpha_composite(Image.fromarray(a, "RGBA"))
    out["ui_slot_inventory"] = inventory

    focus = Plate(N, N)
    focus.rrect(0, 0, N, N, 20, outline=PAPER_SOFT, width=5)
    out["ui_focus_frame"] = focus

    handle = Plate(N, N)
    handle.rrect(0, 0, N, N, 14, fill=WHITE)
    out["ui_scroll_handle"] = handle

    block = Plate(N, N)
    block.rrect(0, 0, N, N, 12, fill=WHITE)
    out["ui_frame_block"] = block
    return out


def progress():
    track = Plate(768, 96)
    track.rrect(0, 0, 768, 96, 22, fill=GREY)
    track.ring_split(0, 0, 768, 96, 22, 6, (188, 188, 188, 255), WHITE)
    fill = Plate(768, 96)
    fill.rrect(0, 0, 768, 96, 22, fill=WHITE)
    fill.ring_split(0, 0, 768, 96, 22, 6, None, GREY)
    return {"ui_progress_track": track, "ui_progress_fill": fill}


# ---------------------------------------------------------------- emblems
def die_emblem(size, line=7):
    """Flat isometric die: lit top, mid left, shaded right; red single pip on top."""
    p = Plate(size, size, 2)
    u = size / 100.0
    top = [(50 * u, 6 * u), (92 * u, 28 * u), (50 * u, 50 * u), (8 * u, 28 * u)]
    left = [(8 * u, 28 * u), (50 * u, 50 * u), (50 * u, 94 * u), (8 * u, 72 * u)]
    right = [(50 * u, 50 * u), (92 * u, 28 * u), (92 * u, 72 * u), (50 * u, 94 * u)]
    p.polygon(top, fill=PAPER_HI)
    p.polygon(left, fill=PAPER)
    p.polygon(right, fill=PAPER_LINE)
    for face in (top, left, right):
        p.polygon(face, outline=INK, width=line * u)
    p.ellipse(50 * u, 28 * u, 8 * u, 5 * u, RED)
    for cx, cy in ((20, 46), (38, 74)):
        p.ellipse(cx * u, cy * u, 5 * u, 6.5 * u, BLUE_DARK)
    for cx, cy in ((62, 56), (80, 46), (62, 78), (80, 68)):
        p.ellipse(cx * u, cy * u, 4.5 * u, 6 * u, rgb("B5403F"))
    return p.export()


def wordmark():
    w, h = 1440, 400
    p = Plate(w, h, 2)
    font = ImageFont.truetype(str(FONT), 262 * p.ss)
    text_layer = Image.new("RGBA", p.im.size, (0, 0, 0, 0))
    d = ImageDraw.Draw(text_layer)
    x = 268 * p.ss
    for ch in "骰子构筑":
        d.text((x + 9 * p.ss, (10 + 9) * p.ss), ch, font=font, fill=RED)
        x += 288 * p.ss
    x = 268 * p.ss
    for ch in "骰子构筑":
        d.text((x, 10 * p.ss), ch, font=font, fill=INK)
        x += 288 * p.ss
    p.im.alpha_composite(text_layer)
    p.rrect(268, 356, 1210, 368, 6, fill=RED)
    p.rrect(1236, 356, 1300, 368, 6, fill=RED)
    p.rrect(268, 380, 440, 388, 4, fill=BLUE)
    emblem = die_emblem(210).resize((210 * p.ss, 210 * p.ss), Image.LANCZOS)
    p.im.alpha_composite(emblem, (28 * p.ss, 96 * p.ss))
    for x0, y0, x1, y1 in ((14, 72, 40, 84), (62, 40, 74, 70), (104, 30, 116, 58)):
        p.rrect(x0, y0, x1, y1, 5, fill=RED)
    return p


def card_back():
    w, h = 768, 1024
    p = Plate(w, h, 2)
    p.rrect(12, 14, w - 2, h - 2, 36, fill=SHADOW[:3] + (105,))
    p.rrect(2, 2, w - 12, h - 14, 36, fill=PAPER)
    bw, bh = w - 12, h - 14
    for y in range(96, bh - 80, 44):
        for x in range(88, bw - 70, 44):
            if abs(x - bw / 2) / 250 + abs(y - bh / 2) / 330 > 1.12:
                p.ellipse(x, y, 3.2, 3.2, PAPER_LINE)
    p.rrect(38, 38, bw - 36, bh - 36, 20, outline=INK, width=5)
    p.rrect(54, 54, bw - 52, bh - 52, 12, outline=PAPER_EDGE, width=3)
    cx, cy = bw / 2 + 1, bh / 2 + 1
    for rx, ry, fill, outline, width in ((236, 312, PAPER_SOFT, INK, 7), (206, 272, None, PAPER_EDGE, 3)):
        diamond = [(cx, cy - ry), (cx + rx, cy), (cx, cy + ry), (cx - rx, cy)]
        if fill:
            p.polygon(diamond, fill=fill)
        p.polygon(diamond, outline=outline, width=width)
    emblem = die_emblem(250, 6).resize((250 * p.ss, 250 * p.ss), Image.LANCZOS)
    p.im.alpha_composite(emblem, (int((cx - 125) * p.ss), int((cy - 125) * p.ss)))
    for dy, color in ((-372, RED), (372, BLUE)):
        d = 15
        p.polygon([(cx, cy + dy - d), (cx + d, cy + dy), (cx, cy + dy + d), (cx - d, cy + dy)], fill=color)
        p.polygon([(cx, cy + dy - d), (cx + d, cy + dy), (cx, cy + dy + d), (cx - d, cy + dy)], outline=INK, width=4)
    p.rrect(2, 2, bw, bh, 36, outline=INK, width=8)
    return p


# ---------------------------------------------------------------- table
def rounded_field(w, h, x0, y0, x1, y1, r):
    """Signed distance (px, positive inside) to a rounded rectangle."""
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    cx, cy = (x0 + x1) / 2, (y0 + y1) / 2
    qx = np.abs(xx + 0.5 - cx) - ((x1 - x0) / 2 - r)
    qy = np.abs(yy + 0.5 - cy) - ((y1 - y0) / 2 - r)
    outside = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0))
    inside = np.minimum(np.maximum(qx, qy), 0)
    return r - (outside + inside)


def mix(a, b, t):
    return a + (b - a) * t[..., None]


def weave(h, w, seed):
    rng = np.random.default_rng(seed)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    threads = 0.010 * np.sin(xx * 2.05) * np.sin(yy * 2.35) + 0.006 * np.sin((xx + yy) * 0.9)
    return 1.0 + threads + (rng.random((h, w), dtype=np.float32) - 0.5) * 0.012


def table():
    s = 2
    w, h = 1920 * s, 1080 * s
    canvas = np.zeros((h, w, 3), dtype=np.float32)
    canvas[:] = SHADOW[:3]

    def field(inset, radius):
        return rounded_field(w, h, inset * s, inset * s, w - inset * s, h - inset * s, radius * s) / s

    def cover(dist, color, feather=0.75):
        t = np.clip(dist / feather + 0.5, 0, 1)
        canvas[:] = mix(canvas, np.array(color[:3], dtype=np.float32), t)

    outer = field(8, 56)
    cover(outer, WOOD_LO)
    cover(field(11, 53), WOOD)
    band = field(11, 53)
    cover(np.minimum(band - 3, 8 - band), WOOD_HI)          # lit lip
    cover(np.minimum(band - 15, 17.5 - band), GOLD_DARK)    # inlay
    cover(np.minimum(band - 26, 33 - band), WOOD_LO)        # inner shadow edge

    felt = field(44, 26)
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    d = np.hypot((xx / w - 0.57) / 0.62, (yy / h - 0.46) / 0.66)
    light = np.clip(1.0 - d, 0, 1) ** 1.25
    base = mix(np.array(rgb("0F2A2B")[:3], dtype=np.float32) * np.ones((h, w, 3), dtype=np.float32),
               np.array(rgb("25595B")[:3], dtype=np.float32), light)
    base *= weave(h, w, 72072)[..., None]
    base *= (0.80 + 0.20 * np.clip(felt / 30.0, 0, 1) ** 0.7)[..., None]   # soft shade under the rim
    t = np.clip(felt / 0.75 + 0.5, 0, 1)
    canvas[:] = mix(canvas, base, t)

    image = Image.fromarray(np.clip(canvas + 0.5, 0, 255).astype(np.uint8), "RGB").convert("RGBA")
    draw = ImageDraw.Draw(image)
    for sx, sy in ((27, 27), (1893, 27), (27, 1053), (1893, 1053)):
        for radius, color in ((10, WOOD_LO), (7.5, GOLD_DARK), (5, GOLD)):
            draw.ellipse([(sx - radius) * s, (sy - radius) * s, (sx + radius) * s, (sy + radius) * s], fill=color)
        draw.ellipse([(sx - 3.5) * s, (sy - 3.5) * s, (sx - 0.5) * s, (sy - 0.5) * s], fill=GOLD_HI)
    return image.resize((1920, 1080), Image.LANCZOS).convert("RGB")


def felt_tile():
    n = 1024
    base = np.ones((n, n, 3), dtype=np.float32) * np.array(rgb("193F41")[:3], dtype=np.float32)
    yy, xx = np.mgrid[0:n, 0:n].astype(np.float32)
    k = 2 * math.pi / n
    threads = 0.010 * np.sin(xx * k * 334) * np.sin(yy * k * 383) + 0.006 * np.sin((xx + yy) * k * 146)
    base *= (1.0 + threads)[..., None]
    return Image.fromarray(np.clip(base + 0.5, 0, 255).astype(np.uint8), "RGB")


def main():
    count = 0
    for name, plate in {**surfaces(), **progress()}.items():
        save(plate, "Surfaces", name)
        count += 1
    save(wordmark(), "UI", "ui_wordmark_dicecraft")
    save(card_back(), "UI", "ui_card_back")
    dot = Plate(64, 64)
    dot.ellipse(32, 32, 30, 30, WHITE)
    save(dot, "UI", "ui_pip_dot")
    save(table(), "Backgrounds", "bg_table_scene")
    save(felt_tile(), "Backgrounds", "bg_felt_tile")
    print("ART072_HD_BUILT", count + 5)


if __name__ == "__main__":
    main()
