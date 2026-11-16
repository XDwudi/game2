"""V1.1 default-theme surfaces for the new play screen (task W11, docs/specs/v11/PLAY_LAYOUT.md section 6). Deterministic.

The default look keeps the v0.73 stepped-corner print style (docs/specs/v11/DEV_PLAN.md 4.2 item 2): two-step notched
corners, ink outlines, a lit top-left bevel and a darker far edge. Unlike most v0.73 plates, these four are drawn as
neutral (white and grey) shapes with ink lines, and the game tints them with the active theme's palette at draw time
(UiKit colours are read at draw time, THEME_SPEC section 1). A theme that does not ship its own copy therefore still
gets its own colours on the default shapes.

Outputs under DicePrototype/Assets/Art/Art11/Surfaces (nine-slice, PPU 200; borders in BORDERS, mirrored by
Editor/V11Setup.cs):
  ui_dice_tray     open tray frame for the dice pool and the submission rail; the centre stays transparent because the
                   real 3D dice are drawn behind the interface canvas
  ui_topbar        the run status bar across the top of the play screen
  ui_chip_formula  one named factor of the score formula ("四条 ×8"); tinted per factor, dark when not yet lit
  ui_stage_card    the current stage's card in the top right (normal, pressure or boss); the boss frame is a tinted ring
"""
from pathlib import Path
import importlib.util

import numpy as np
from PIL import Image

_spec = importlib.util.spec_from_file_location("art073_build_art", Path(__file__).resolve().parents[2] / "art_v073" / "build_art.py")
art = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(art)
Plate, rgb, stair, mask_of, paint, band, sfill, sline, ssplit, inset = (
    art.Plate, art.rgb, art.stair, art.mask_of, art.paint, art.band, art.sfill, art.sline, art.ssplit, art.inset)
INK, INK_DEEP, WHITE = art.INK, art.INK_DEEP, art.WHITE

ROOT = Path(__file__).resolve().parents[3]
OUT = ROOT / "DicePrototype" / "Assets" / "Art" / "Art11" / "Surfaces"
N = 512
# Nine-slice borders in pixels (left, bottom, right, top). Mirrored by Editor/V11Setup.cs.
BORDERS = {"ui_dice_tray": (72, 72, 72, 72), "ui_topbar": (40, 40, 40, 40), "ui_chip_formula": (40, 40, 40, 40), "ui_stage_card": (48, 48, 48, 48)}
LIGHT = (255, 255, 255, 255)      # lit bevel: stays the tint itself
SHADE = (176, 176, 176, 255)      # far bevel: about two thirds of the tint
DEEP = (120, 120, 120, 255)       # inner lip: about half of the tint


def inner_shadow(plate, box, depth, strength, s, n=2):
    """A soft shade that falls from a stepped inner edge into the open centre (alpha only, so it darkens any table)."""
    k = plate.ss
    outline = np.asarray(mask_of(plate, stair(*box, s, n))) > 0
    h, w = outline.shape
    # Distance inward from the edge, approximated per axis on the supersampled grid (cheap and smooth enough).
    yy, xx = np.mgrid[0:h, 0:w].astype(np.float32)
    x0, y0, x1, y1 = (v * k for v in box)
    d = np.minimum.reduce([xx - x0, x1 - xx, yy - y0, y1 - yy]) / k
    alpha = np.clip(1 - d / depth, 0, 1) ** 1.8 * strength
    alpha[~outline] = 0
    layer = np.zeros((h, w, 4), dtype=np.uint8)
    layer[..., :3] = (7, 25, 28)
    layer[..., 3] = (alpha * 255).astype(np.uint8)
    plate.im.alpha_composite(Image.fromarray(layer, "RGBA"))


def dice_tray():
    """Open tray frame: a raised rim (tinted at runtime) with ink edges and a shade falling onto the table inside it."""
    p = Plate(N, N)
    s = 14                       # one stair size for every concentric outline, so the corners stay parallel
    outer = (3, 3, N - 3, N - 3)
    rim = 30
    inner = inset(outer, rim)
    # Rim body: white is replaced by the tint; the lit half of the outer bevel stays the tint, the far half is darker,
    # and the inner bevel is reversed (the rim slopes down toward the table).
    paint(p, band(p, outer, rim, s), WHITE)
    ssplit(p, inset(outer, 5), 5, LIGHT, SHADE, s)
    ssplit(p, inset(outer, rim - 7), 4, SHADE, LIGHT, s)
    # A thin print line along the middle of the rim, the stepped-corner signature of the default look.
    sline(p, inset(outer, 14), 2, DEEP[:3] + (170,), s)
    # Ink: outer edge and the inner lip where the rim meets the table.
    sline(p, outer, 5, INK_DEEP, s)
    sline(p, inset(outer, rim - 3), 3, INK_DEEP, s)
    inner_shadow(p, inner, depth=24, strength=0.55, s=s)
    return p


def topbar():
    p = Plate(N, N)
    box = (3, 3, N - 3, N - 6)
    sfill(p, (box[0] + 3, box[1] + 5, box[2] + 3, box[3] + 5), art.SHADOW[:3] + (110,), 12)
    sfill(p, box, WHITE, 12)
    ssplit(p, inset(box, 5), 3, LIGHT, SHADE, 10)
    # Bottom print line: a darker band inside the lower edge.
    p.line(18, N - 30, N - 18, N - 26, DEEP[:3] + (170,))
    sline(p, box, 5, INK_DEEP, 12)
    return p


def chip_formula():
    p = Plate(N, N)
    box = (2, 2, N - 2, N - 8)
    sfill(p, (box[0] + 2, box[1] + 6, box[2], box[3] + 6), (90, 90, 90, 255), 9)    # the lip under the chip
    sfill(p, box, WHITE, 9)
    ssplit(p, inset(box, 4), 4, LIGHT, SHADE, 8)
    sline(p, box, 4, INK_DEEP, 9)
    return p


def stage_card():
    p = Plate(N, N)
    body = (2, 2, N - 10, N - 12)
    sfill(p, (body[0] + 8, body[1] + 10, body[2] + 8, body[3] + 10), art.SHADOW[:3] + (110,), 14)
    sfill(p, body, WHITE, 14)
    ssplit(p, inset(body, 5), 3, LIGHT, SHADE, 12)
    sline(p, inset(body, 15), 2, DEEP[:3] + (160,), 9)
    sline(p, body, 5, INK, 14)
    return p


def main():
    OUT.mkdir(parents=True, exist_ok=True)
    plates = {"ui_dice_tray": dice_tray(), "ui_topbar": topbar(), "ui_chip_formula": chip_formula(), "ui_stage_card": stage_card()}
    for name, plate in plates.items():
        plate.export().save(OUT / (name + ".png"), optimize=True)
    print("ART11_DEFAULT_BUILT", len(plates))


if __name__ == "__main__":
    main()
