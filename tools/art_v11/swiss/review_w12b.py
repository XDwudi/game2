"""Offline W11-size review using exported PNGs, not a replacement Unity capture."""
from __future__ import annotations

import json
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

import build_swiss as kit

OUT = kit.ROOT / "artifacts/cache/v11/swiss-w12b"
ZONES = {
    "TOP BAR": (16, 12, 1888, 72),
    "CARDS": (32, 96, 1448, 244),
    "STAGE": (1496, 96, 392, 244),
    "DICE TRAY": (32, 352, 1448, 376),
    "RESOURCES": (1496, 352, 392, 376),
    "INFO": (32, 740, 300, 202),
    "RAIL": (344, 740, 1136, 202),
    "SETTLE": (1496, 740, 392, 202),
}


def render() -> dict[str, Image.Image]:
    specs = {s["name"]: s for s in kit.contract_sprites()}
    sprites = {}
    for name in specs:
        with Image.open(kit.ROOT / kit.THEME / "UI" / (name + ".png")) as source:
            sprites[name] = source.convert("RGBA")
    palette = json.loads((kit.ROOT / kit.THEME / "theme.json").read_text(encoding="utf-8"))["palette"]
    color = {name: kit.rgba(value.lstrip("#")) for name, value in palette.items()}
    with Image.open(kit.ROOT / kit.THEME / "Table/table_scene.png") as source:
        table = source.convert("RGB")
    kit.check_felt_zones(table)
    proof = table.copy()

    def text(x, y, value, size=22, fill=kit.INK):
        ImageDraw.Draw(proof).text((x, y), value, font=ImageFont.truetype(str(kit.LATIN), size),
                                  fill=fill[:3], anchor="lt")

    def paste(name, rect, tint=kit.WHITE):
        x, y, w, h = map(int, rect)
        spec = specs[name]
        # Unity's 100 reference PPU / source PPU 200 gives 20 UI unit borders
        # for a 40 px sprite border. Preserve that density before rasterizing.
        ratio = spec["pixelsPerUnit"] / 100
        if any(spec["border"]):
            im = kit.nine_slice(sprites[name], spec["border"], (round(w * ratio), round(h * ratio)))
        else:
            im = sprites[name]
        im = im.resize((w, h), Image.Resampling.LANCZOS)
        pixels = np.asarray(im).copy()
        pixels = (pixels.astype(np.uint16) * np.array(tint, dtype=np.uint16) // 255).astype(np.uint8)
        im = Image.fromarray(pixels, "RGBA")
        proof.paste(im, (x, y), im)

    paste("ui_topbar", ZONES["TOP BAR"], color["panel"])
    paste("ui_chip_formula", (28, 20, 160, 56), color["panelLight"])
    text(49, 36, "STAGE 4", 24, kit.LIGHT)
    text(230, 36, "0 / 4,000", 30, kit.LIGHT)
    paste("ui_icon_coin", (1510, 30, 36, 36))
    text(1558, 36, "28", 26, kit.LIGHT)
    paste("fx_score_crown", (1640, 16, 48, 32))
    text(1705, 34, "CROWN 48x32", 18, kit.TAN)
    for i in range(3):
        x = 32 + i * 228
        paste("ui_card_controller", (x, 96, 216, 244))
        text(x + 20, 119, "PAPER CARD", 22)
        text(x + 20, 162, "Flat content area", 18)
    for i in range(2):
        paste("ui_slot_inventory", (716 + i * 68, 104, 56, 228))
        text(732 + i * 68, 201, "+", 32, kit.TAN)
    text(890, 121, "W12b / OFFLINE ART PROOF", 26)
    text(890, 164, "Exported textures at W11 design sizes", 21)
    text(890, 198, "PPU 200 / 10 source px = 5 UI units", 21)
    text(890, 232, "Sample labels only; no Unity simulation", 21)
    text(890, 266, "Cyan = layout, blue = protected felt", 20)

    paste("ui_stage_card", ZONES["STAGE"], color["paper"])
    paste("ui_chip_formula", (1510, 108, 356, 40), color["panelLight"])
    text(1525, 119, "NORMAL STAGE", 20, kit.LIGHT)
    text(1516, 176, "4,000", 42)
    text(1516, 245, "Paper tint / opaque center", 20)

    rim = tuple(round(a * .45 + b * .55) for a, b in zip(color["panel"], color["panelLight"]))
    for zone in ("DICE TRAY", "RAIL"):
        paste("ui_dice_tray", ZONES[zone], rim)
    paste("ui_chip_formula", (56, 338, 100, 34), color["panel"])
    text(72, 346, "POOL", 19, kit.LIGHT)
    text(245, 464, "Transparent center: unchanged #F9EADB table", 30)
    text(245, 514, "No baked stripe, header, shadow or diagonal", 24)
    chips = [(52, 112, "PREDICT", "panel"), (180, 178, "BASE 26", "blue"),
             (374, 160, "PAIR x2", "red"), (550, 168, "BLUE x3", "blue"),
             (734, 184, "CARDS x1.5", "gold"), (934, 220, "TOTAL 234", "gold")]
    for x, w, label, tint in chips:
        paste("ui_chip_formula", (x, 666, w, 48), color[tint])
        text(x + 12, 681, label, 20, kit.LIGHT if tint == "panel" else kit.BLACK)
    paste("ui_chip_formula", (1244, 666, 196, 48), color["panel"])
    text(1258, 681, "DETAILS", 20, kit.LIGHT)
    for zone in ("RESOURCES", "INFO", "SETTLE"):
        paste("ui_panel_hud", ZONES[zone])
    text(1516, 376, "RESOURCES", 22, kit.LIGHT)
    for y, icon, label in ((428, "ui_icon_roll", "ROLLS  2"), (490, "ui_icon_lock", "LOCK"),
                           (552, "ui_icon_dice_view", "VIEW DICE")):
        paste(icon, (1516, y, 40, 40))
        text(1570, y + 10, label, 22, kit.LIGHT)
    paste("ui_card_item", (1516, 620, 64, 64))
    text(1596, 641, "ITEM 64x64", 20, kit.LIGHT)
    text(48, 763, "SUBMIT 2 / 5", 25, kit.LIGHT)
    text(48, 809, "Opaque info panel", 20, kit.TAN)
    text(1516, 763, "TO GOAL", 22, kit.LIGHT)
    text(1516, 811, "4,000", 42, kit.LIGHT)
    for i in range(5):
        x = 362 + i * 218
        paste("ui_ring", (x, 758, 208, 164), color["ghost"])
        if i < 2:
            paste("ui_tag_lock", (x + 166, 779, 28, 30))
            paste("ui_tray_front", (x + 4, 914, 200, 20))
    paste("ui_panel_hud", (32, 958, 512, 88))
    text(50, 990, "HINT / calm felt beneath all open zones", 21, kit.LIGHT)
    paste("ui_chip_formula", (560, 966, 264, 72), color["panel"])
    text(608, 991, "SECONDARY", 22, kit.LIGHT)
    paste("ui_button_bevel", (840, 962, 400, 80), color["primary"])
    text(966, 989, "CONFIRM", 28, kit.BLACK)
    paste("ui_chip_formula", (1496, 962, 392, 80), color["panelLight"])
    text(1615, 989, "END STAGE", 24, kit.LIGHT)

    annotated = proof.copy()
    zone_map = table.copy()
    for target in (annotated, zone_map):
        d = ImageDraw.Draw(target)
        font = ImageFont.truetype(str(kit.LATIN), 16)
        for label, (x, y, w, h) in ZONES.items():
            d.rectangle((x, y, x + w - 1, y + h - 1), outline=(0, 145, 145), width=1)
            if target is zone_map:
                d.text((x + 8, y + 8), label, font=font, fill=(0, 100, 100))
        for label, (x, y, w, h) in kit.FELT_ZONES.items():
            d.rectangle((x, y, x + w - 1, y + h - 1), outline=(40, 95, 210), width=2)
            if target is zone_map:
                d.text((x + 8, y + h - 25), label, font=font, fill=(25, 65, 155))
    # Contain exactly as Display.cs and Art07Felt.shader do with a full safe area.
    def contain(size):
        scale = min(size[0] / 1920, size[1] / 1080)
        fitted = proof.resize((round(1920 * scale), round(1080 * scale)), Image.Resampling.LANCZOS)
        im = Image.new("RGB", size, (134, 134, 134))
        im.paste(fitted, ((size[0] - fitted.width) // 2, (size[1] - fitted.height) // 2))
        return im
    return {"table-zones-1920x1080.png": zone_map,
            "review-1920x1080.png": annotated,
            "review-1280x720.png": annotated.resize((1280, 720), Image.Resampling.LANCZOS),
            "review-clean-1920x1080.png": proof,
            "review-contain-1920x1200.png": contain((1920, 1200)),
            "review-contain-2520x1080.png": contain((2520, 1080))}


if __name__ == "__main__":
    OUT.mkdir(parents=True, exist_ok=True)
    images = render()
    for name, im in images.items():
        im.save(OUT / name, optimize=False, compress_level=9)
    print(f"SWISS_REVIEW {len(images)} PNGs / {OUT.relative_to(kit.ROOT).as_posix()}")
