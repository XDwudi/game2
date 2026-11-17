"""Evidence for the comic kit's round 3 (coordinator 2026-11-17: THEME_SPEC 3.1 decoration rules and the W20X keys).

    python tools/art_v11/comic/review_r3.py

Reads the shipped comic kit (run build_comic.py first) and writes ArtSource/Art11/comic/rework_r3/:
- play_cards.png: the play composite with 0, 3, 5, 7 and 10 cards (GameApp.Play11 DrawCards11 positions), and
  card_row.png, each card row at full size: nothing but the cards, the "+" strips and the plain page;
- action_row.png: the action row in every DrawActions11 state at 100 % and 150 % text, with the rectangle that is
  free in all of them outlined; action_row.txt lists each state's rectangles, the free gap and the burst's box;
- shop_w14_now.png: the W14 shop (GameApp.Shop14 layout and tints as the code draws them today: section x panel,
  caption strip x panelLight, the hard-coded steps) over Table/page_scene.png;
- shop_after_w20x.png: the same shop simulated after W20X with comic's tokens (section FFFFFF, caption yellow with
  the label LabelOn11 picks, ink steps);
- style_page.png: the dice style page (GameApp.Art073StyleLegend: eight ui_section_panel cells with Muted notes) with
  comic's section token; section FFFFFF draws the same today and after W20X.
Offline Python emulation (sprites sampled as the game's UI sprites, blended in linear light), not Unity captures.
Names, texts and pictures are stand-ins.
"""
from __future__ import annotations

import importlib.util
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve()
ROOT = HERE.parents[3]
OUT = ROOT / "ArtSource/Art11/comic/rework_r3"
THEME = ROOT / "DicePrototype/Assets/Resources/Themes/comic"
ANCHORS = ROOT / "ArtSource/StyleKit/themes/comic/anchors"

_SPEC = importlib.util.spec_from_file_location("comic_build", HERE.parent / "build_comic.py")
bc = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(bc)


def load_kit():
    specs = bc.contract_sprites()
    borders = {s["name"]: s["border"] for s in json.loads(bc.CONTRACT.read_text(encoding="utf-8"))["sprites"]}
    sprites = {}
    for s in specs:
        with Image.open(THEME / "UI" / (s["name"] + ".png")) as source:
            sprites[s["name"]] = source.convert("RGBA")
    with Image.open(THEME / "Table/table_scene.png") as source:
        table = source.convert("RGB")
    with Image.open(THEME / "Table/page_scene.png") as source:
        page = source.convert("RGB")
    palette = json.loads((THEME / "theme.json").read_text(encoding="utf-8"))["palette"]
    arts = {}
    for name in ("C108", "I10", "C117", "U10"):
        with Image.open(ANCHORS / (name + ".png")) as source:
            arts[name] = source.convert("RGBA")
    return specs, borders, sprites, table, page, palette, arts


def caption_bar(im: Image.Image, text: str, font, colour=(246, 240, 222)) -> Image.Image:
    out = Image.new("RGB", (im.width, im.height + 34), (20, 24, 44))
    ImageDraw.Draw(out).text((12, 7), text, fill=colour, font=font)
    out.paste(im, (0, 34))
    return out


def stack(images, gap=0):
    out = Image.new("RGB", (max(i.width for i in images), sum(i.height for i in images) + gap * (len(images) - 1)),
                    (20, 24, 44))
    y = 0
    for im in images:
        out.paste(im, (0, y))
        y += im.height + gap
    return out


def yellowish(px: np.ndarray) -> np.ndarray:
    return (px[..., 0] > 200) & (px[..., 1] > 150) & (px[..., 2] < 120)


# ---------------------------------------------------------------------------------------------- play: card counts
CARD_COUNTS = (0, 3, 5, 7, 10)


def play_evidence(specs, sprites, table, font):
    x, y, w, h = bc.PLAY_RECTS["card row"]
    under = int(yellowish(np.asarray(table)[y:y + h, x:x + w]).sum())
    full, rows, lines = [], [], []
    for n in CARD_COUNTS:
        on_page = []
        mock = bc.play_mock(sprites, specs, table, on_page, cards=n, state="f")
        mode = "two rows of compact cards" if n > 7 else "no card, five + strips" if n == 0 else \
            f"{n} full cards" + (f" and {5 - n} + strips" if n < 5 else "")
        note = "no word or mark on the page" if not on_page else "on the page: " + "; ".join(i["what"] for i in on_page)
        label = f"{n} cards ({mode}); table under the card row: {under} yellow px; {note}"
        lines.append(label)
        full.append(caption_bar(mock.resize((960, 540), Image.Resampling.LANCZOS), label, font))
        rows.append(caption_bar(mock.crop((0, 84, 1920, 352)), label, font))
    return stack(full, 8), stack(rows, 8), lines


# ---------------------------------------------------------------------------------------------- play: action row
def action_evidence(specs, sprites, table, font):
    small = ImageFont.truetype(str(bc.LATIN), 15)
    x0, y0, x1, y1 = bc.ACTION_FREE
    xs, ys = zip(*bc.burst_polygon())
    box = (min(xs) - bc.BURST_INK, min(ys) - bc.BURST_INK, max(xs) + bc.BURST_INK, max(ys) + bc.BURST_INK)
    lines, strips = [], []
    for large in (False, True):
        rects = bc.action_rects(large)
        for state, s in bc.ACTION_STATES.items():
            mock = bc.play_mock(sprites, specs, table, None, cards=3, state=state, large_actions=large)
            crop = mock.crop((0, 940, 1920, 1080))
            d = ImageDraw.Draw(crop)
            d.rectangle((x0, y0 - 940, x1 - 1, y1 - 940 - 1), outline=(0, 255, 120), width=2)
            used = {"hint": rects["hint"]}
            if s["secondary"]:
                used["secondary"] = rects["secondary"]
            if s["primary"]:
                px, py, pw, ph = rects["primary"]
                used["primary+step"] = (px, py, pw, ph + 2)   # UiKit.Button's step ends 2 units below the rect
            ex, ey, ew, eh = rects["end"]
            if s["end"] == "skip":
                used["skip"] = rects["end"]
            else:
                used["end+step"] = (ex, ey, ew, eh + 2)
                if s["end"] == "qualified":
                    used["reward ring"] = (ex - 4, ey - 4, ew + 8, eh + 4)
            right = max(x + w for x, y, w, h in used.values() if x < x0)
            left = min(x for x, y, w, h in used.values() if x >= x0)
            inside = box[0] >= right and box[2] <= left and box[1] >= y0
            text = (f"{state:>9} {'150%' if large else '100%'}: "
                    + ", ".join(f"{k} {x},{y},{w},{h}" for k, (x, y, w, h) in used.items())
                    + f" | free x {right:g}-{left:g} y 956-1080 | burst box x {box[0]:.0f}-{box[2]:.0f} y {box[1]:.0f}-{box[3]:.0f}"
                    + (" inside" if inside else " OUTSIDE"))
            lines.append(text)
            bar = Image.new("RGB", (1920, 24), (20, 24, 44))
            ImageDraw.Draw(bar).text((8, 4), text, fill=(246, 240, 222) if inside else (255, 120, 120), font=small)
            strips.append(stack([bar, crop]))
    return stack(strips, 6), lines


# ---------------------------------------------------------------------------------------------- shop helpers
def drawn_centre(sprite: Image.Image, tint: str) -> str:
    """The drawn colour of a sliced sprite's centre under a tint (GameApp.RenderedFill11)."""
    px = np.asarray(sprite)
    return bc.drawn(bc.hexc(px[px.shape[0] // 2, px.shape[1] // 2, :3] / 255.0), tint)


def label_on11(fill: str, P: dict, preferred: str | None = None) -> tuple[str, float]:
    """GameApp.LabelOn11 on a drawn fill: a preferred colour stays at 4.5:1; otherwise the best of ink, paper and textColor
    (the game deepens or lightens the surface when even that is under 4.95:1; the ratio is returned so callers report it)."""
    if preferred is not None and bc.contrast(P[preferred], fill) >= 4.5:
        return preferred, bc.contrast(P[preferred], fill)
    return bc.best_label(fill, P)


def lerp_hex(a: str, b: str, t: float) -> str:
    return bc.hexc(bc.col(a) * (1 - t) + bc.col(b) * t)   # Color.Lerp on the palette's sRGB values


def width_of(text: str, size: float, font=None) -> float:
    return ImageFont.truetype(str(font or bc.FONT), int(round(size))).getlength(text)


def plain(c, rgba, x, y, w, h):
    """A UiKit.Box without a sprite: a flat rectangle (straight alpha)."""
    c.sprite(Image.new("RGBA", (4, 4), tuple(rgba)), x, y, w, h)


def step_button(c, kit, borders, P, x, y, w, h, face_tint: str, step_tint: str):
    """UiKit.Button's surfaces: the step (2, 8, w - 4, h - 6), the face (0, 0, w, h - 7) and the lip, all on the bevel."""
    c.sprite(kit["ui_button_bevel"], x + 2, y + 8, w - 4, h - 6, tint=bc.col(step_tint), border=borders["ui_button_bevel"])
    c.sprite(kit["ui_button_bevel"], x, y, w, h - 7, tint=bc.col(face_tint), border=borders["ui_button_bevel"])
    plain(c, (255, 255, 255, 89), x + 14, y + 5, w - 28, 1)
    return drawn_centre(kit["ui_button_bevel"], face_tint)


def solid_button(c, kit, borders, P, x, y, w, h, label, icon=None, font=20.0, notes=None):
    """GameApp.SolidButton11 as ShopButton14 draws it: ui_chip_formula x lerp(Background, Panel, .55), the label picked on
    that face, an icon before it in the label colour."""
    tint = lerp_hex(P["background"], P["panel"], .55)
    c.sprite(kit["ui_chip_formula"], x, y, w, h, tint=bc.col(tint), border=borders["ui_chip_formula"])
    key, ratio = label_on11(drawn_centre(kit["ui_chip_formula"], tint), P, "textColor")
    size = max(19.5, font)
    if icon:
        s = min(34, h - 24)
        start = (w - (s + 8 + width_of(label, size))) / 2
        c.sprite(kit[icon], x + start, y + (h - s) / 2, s, s, tint=bc.col(P[key]))
        c.text(x + start + s + 8, y + h / 2, label, size, P[key], bc.FONT, "lm")
    elif label:
        c.text(x + w / 2, y + h / 2, label, size, P[key], bc.FONT, "mm")
    if notes is not None:
        notes.append(f"solid buttons (face {drawn_centre(kit['ui_chip_formula'], tint)}): label {key} {ratio:.2f}:1")
    return key


def die_image(px: int, pips=(1, 2, 3), P=None) -> Image.Image:
    r = bc.Raster(int(px * 1.4), int(px * 1.4))
    bc.die_faces(r, px * .7, px * .66, px, pips=pips, pip_rgb=(bc.col(P["red"]), bc.col(P["blue"]), bc.col(P["blue"])),
                 ink=px * .045)
    return r.export()


# ---------------------------------------------------------------------------------------------- the W14 shop
SHOP_TOP, SHOP_GAP, SHOP_HEAD, SHOP_PAD = 92, 8, 68, 12


def shop_layout():
    """GameApp.Shop14 S14 at 100 % text."""
    avail = 1072 - SHOP_TOP - 2 * SHOP_GAP
    cards, build = SHOP_HEAD + 348 + 7, 4 + 28 + 2 + 104 + 7
    goods = avail - cards - build
    owned = SHOP_HEAD + 2 * 104 + 6 + 7
    leave = 96
    dice = avail - owned - leave
    y = SHOP_TOP
    return {"cards": (16, y, 1404, cards), "goods": (16, y + cards + SHOP_GAP, 1404, goods),
            "build": (16, y + cards + goods + 2 * SHOP_GAP, 1404, build), "dice": (1432, y, 472, dice),
            "owned": (1432, y + dice + SHOP_GAP, 472, owned), "leave": (1432, y + dice + owned + 2 * SHOP_GAP, 472, leave)}


def shop_mock(borders, sprites, page, palette, arts, after_w20x: bool, notes: list) -> Image.Image:
    """The shop with four card offers, nine goods, two new dice and two face upgrades, eight owned dice and six cards in
    我的构筑, drawn as GameApp.Shop14 lays them out (100 % text) over page_scene."""
    P = dict(palette)
    kit = sprites
    c = bc.UnityCanvas(1920, 1080, P["background"], 1.0)
    c.buf[...] = bc._lin_texture(page.convert("RGBA"))
    section = P["section"] if after_w20x else P["panel"]
    caption = P["caption"] if after_w20x else P["panelLight"]
    step_primary = P["buttonStepPrimary"] if after_w20x else "B56B29"
    L = shop_layout()
    # Top bar (ShopTopBar14, ShopTopRight14, ShopTopButtons14).
    c.sprite(kit["ui_topbar"], 16, 12, 1888, 72, tint=bc.col(P["panel"]), border=borders["ui_topbar"])
    bar = drawn_centre(kit["ui_topbar"], P["panel"])
    tl, tb, tr, tt = (v / 2 for v in bc.frame_insets_px(np.asarray(kit["ui_topbar"]), borders["ui_topbar"]))
    mid = 12 + tt + (72 - tt - tb) / 2
    key = label_on11(bar, P, "textColor")[0]
    c.text(16 + tl + 16, mid, "商店 · 第 5 关前", 26, P[key], bc.FONT, "lm")
    c.text(16 + tl + 16 + width_of("商店 · 第 5 关前", 26) + 40, mid, "下一关：第 5 关 · 普通 · 目标 5,200", 20, P[key], bc.FONT, "lm")
    right = 1904 - 8
    icon_w = lambda label: 34 + 8 + width_of(label, 20) + 4 + 28
    pause = (right - icon_w("设置"), 18, icon_w("设置"), 60)
    right = pause[0] - 10
    hand = (right - icon_w("手册"), 18, icon_w("手册"), 60)
    right = hand[0] - 10
    cycle_w = 44 + 8 + width_of("查看本章", 20) + 4 + 28
    cycle = (right - cycle_w, 18, cycle_w, 60)
    right = cycle[0] - 16
    unit_w = width_of("金币", 20) + 4
    money_w = (width_of("19", 34) + 4) * 1.12 + 6
    gold_w = 40 + money_w + unit_w + 6
    gx = right - gold_w
    c.sprite(kit["ui_icon_coin"], gx, 24, 36, 36)
    c.text(gx + gold_w - unit_w - 6, 48, "19", 34, P[label_on11(bar, P, "gold")[0]], bc.FONT, "rm")
    c.text(gx + gold_w - unit_w, 48, "金币", 20, P[label_on11(bar, P, "muted")[0]], bc.FONT, "lm")
    solid_button(c, kit, borders, P, *pause, "设置", "ui_icon_gear")
    solid_button(c, kit, borders, P, *hand, "手册", "ui_icon_book")
    k = solid_button(c, kit, borders, P, *cycle, "")
    for i in range(3):
        size = 12 if i < 1 else 10
        c.sprite(kit["ui_pip_dot"], cycle[0] + 16 + i * 16 + (12 - size) / 2, cycle[1] + (60 - size) / 2, size, size,
                 tint=bc.col(P[k]))
    c.text(cycle[0] + 16 + 44 + 8, cycle[1] + 30, "查看本章", 20, P[k], bc.FONT, "lm")
    # Sections (Section14) and 我的构筑 (ShopBuild14: the section panel without a strip).
    fl, fb, fr, ft = (v / 2 for v in bc.frame_insets_px(np.asarray(kit["ui_section_panel"]), borders["ui_section_panel"]))
    panel_fill = drawn_centre(kit["ui_section_panel"], section)
    strip_fill = drawn_centre(kit["ui_section_header"], caption)
    title_key, title_ratio = label_on11(strip_fill, P, "textColor")
    muted_key, muted_ratio = label_on11(strip_fill, P, "muted")
    notes.append(f"section fill {panel_fill} (ui_section_panel x {section}); caption strip {strip_fill} "
                 f"(ui_section_header x {caption}): titles {title_key} {title_ratio:.2f}:1, notes {muted_key} {muted_ratio:.2f}:1")
    titles = {"cards": "卡牌", "goods": "升级与服务", "dice": "骰子与骰面", "owned": "我的骰子 8/8 · 提交栏 5 格"}
    title_right = {}
    for name, (x, y, w, h) in L.items():
        if name == "leave":
            continue
        c.sprite(kit["ui_section_panel"], x, y, w, h, tint=bc.col(section), border=borders["ui_section_panel"])
        if name in titles:
            c.sprite(kit["ui_section_header"], x + fl, y + ft, w - fl - fr, SHOP_HEAD - ft - 2, tint=bc.col(caption),
                     border=borders["ui_section_header"])
            c.text(x + fl + 14, y + ft + (SHOP_HEAD - ft - 2) / 2, titles[name], 22, P[title_key], bc.FONT, "lm")
            title_right[name] = x + fl + 14 + width_of(titles[name], 22) + 2
    # 卡牌: 在售 and the refresh button in the strip, four offers (220 x 348, stride 232).
    x, y, w, h = L["cards"]
    word_w, price_w = width_of("刷新", 20) + 4, 26 + 4 + width_of("3", 22) + 4 + 4
    refresh_w = 16 + 34 + 8 + word_w + 10 + price_w + 16
    rx = x + w - SHOP_PAD - refresh_w
    k = solid_button(c, kit, borders, P, rx, y + 4, refresh_w, 60, "", None, notes=notes)
    c.sprite(kit["ui_icon_refresh"], rx + 16, y + 4 + 13, 34, 34, tint=bc.col(P[k]))
    c.text(rx + 58, y + 34, "刷新", 20, P[k], bc.FONT, "lm")
    face = drawn_centre(kit["ui_chip_formula"], lerp_hex(P["background"], P["panel"], .55))
    c.sprite(kit["ui_icon_coin"], rx + 58 + word_w + 10, y + 4 + 17, 26, 26)
    c.text(rx + 58 + word_w + 40, y + 34, "3", 22, P[label_on11(face, P, "gold")[0]], bc.FONT, "lm")
    c.text(title_right["cards"] + 16, y + ft + (SHOP_HEAD - ft - 2) / 2, "在售 4/4", 18, P[muted_key], bc.FONT, "lm")
    for i in range(4):
        rarity, title, text, art = bc.MOCK_CARDS[i]
        bc.draw_card13(c, kit, borders, P, x + SHOP_PAD + i * 232, y + SHOP_HEAD, 220, 348, "shop", rarity, title, text, arts[art])
    # 升级与服务: 指定升级 in the strip, nine goods rows (452 x 103, price 180).
    x, y, w, h = L["goods"]
    study = "指定升级 · 加价 2"
    study_w = width_of(study, 20) + 4 + 32
    solid_button(c, kit, borders, P, x + w - SHOP_PAD - study_w, y + 4, study_w, 60, study)
    area = h - SHOP_HEAD - 7
    row_h = int(min(150, (area - 2 * 6) / 3))
    col_w = (w - 2 * SHOP_PAD - 2 * 12) / 3
    goods = (("牌型升级", "一对", "Lv.0 → Lv.1 · ×2 → ×2.5", "U10"), ("牌型升级", "全蓝", "Lv.1 → Lv.2 · ×3 → ×3.5", "U10"),
             ("牌型升级", "顺子", "Lv.0 → Lv.1 · ×4 → ×4.5", "U10"), ("道具", "蓝色浪潮", "下一次有效提交全部骰子临时视为蓝色。", "I10"),
             ("道具", "重来", "重掷一次，不消耗掷骰次数。", "I10"), ("道具", "点金", "选一颗骰子，这一面镀金。", "I10"),
             ("服务", "增加控制", "卡牌栏位 +1。", "C108"), ("服务", "删除骰子", "移除一颗骰子。", "C108"),
             ("服务", "进货", "下一家商店多一张卡。", "C108"))
    for i, (cat, title, text, art) in enumerate(goods):
        bc.draw_card13(c, kit, borders, P, x + SHOP_PAD + (i % 3) * (col_w + 12), y + SHOP_HEAD + (i // 3) * (row_h + 6),
                       col_w, row_h, "goods", None, title, text, arts[art], category=cat, price_w=180)
    # 我的构筑: caption line, six tiles (100 x 104), the items.
    x, y, w, h = L["build"]
    build_key = label_on11(panel_fill, P, "textColor")
    build_muted = label_on11(panel_fill, P, "muted")
    notes.append(f"我的构筑 on the section fill {panel_fill}: caption {build_key[0]} {build_key[1]:.2f}:1, "
                 f"notes {build_muted[0]} {build_muted[1]:.2f}:1")
    cy = y + 4 + 14
    c.text(x + SHOP_PAD, cy, "我的构筑", 18, P[build_key[0]], bc.FONT, "lm")
    tx = x + SHOP_PAD + width_of("我的构筑", 18) + 16
    capacity = "卡牌栏位 5/5 · 共 6/10 张"
    c.text(tx, cy, capacity, 18, P[build_muted[0]], bc.FONT, "lm")
    c.text(tx + width_of(capacity, 18) + 14, cy, "拖动调整顺序", 18, P[build_muted[0]], bc.FONT, "lm")
    items_x = x + w - SHOP_PAD - (5 * 60 + 4 * 6)
    c.text(items_x, cy, "道具 1/3", 18, P[build_key[0]], bc.FONT, "lm")
    for i in range(6):
        rarity, title, text, art = bc.MOCK_CARDS[i % 4]
        bc.draw_card13(c, kit, borders, P, x + SHOP_PAD + i * 104, y + 34, 100, 104, "tile", rarity, title, text, arts[art],
                       ordinal=i + 1)
    for i in range(3):
        ix, iy = items_x + i * 66, y + 34 + 22
        if i == 0:
            c.sprite(kit["ui_card_item"], ix, iy, 60, 60, border=borders["ui_card_item"])
            c.picture(arts["I10"], ix + 8, iy + 6, 44)
        else:
            c.sprite(kit["ui_slot_inventory"], ix, iy, 60, 60, tint=bc.col(P["panelLight"]), border=borders["ui_slot_inventory"])
    # 骰子与骰面: two new dice and two face upgrades (448 x 121, price 150).
    x, y, w, h = L["dice"]
    row_h = int((h - SHOP_HEAD - 7 - 3 * 6) / 4)
    rows = (("新骰子 · 点数和 21", "红蓝各半", "", die_image(120, (5, 2, 3), P)),
            ("新骰子 · 点数和 24", "大点蓝骰", "", die_image(120, (6, 4, 4), P)),
            ("骰面改造", "配重", "这一面权重+1（上限5），更容易掷出这一面。", arts["U10"]),
            ("骰面改造", "抛光", "这一面改为光滑。", arts["U10"]))
    for i, (cat, title, text, art) in enumerate(rows):
        bc.draw_card13(c, kit, borders, P, x + SHOP_PAD, y + SHOP_HEAD + i * (row_h + 6), w - 2 * SHOP_PAD, row_h, "goods",
                       None, title, text, art, category=cat, price_w=150)
    # 我的骰子: eight cells.
    x, y, w, h = L["owned"]
    gap = (w - 2 * SHOP_PAD - 4 * 104) / 3
    for i in range(8):
        cx, cy2 = x + SHOP_PAD + (i % 4) * (104 + gap), y + SHOP_HEAD + (i // 4) * 110
        c.sprite(kit["ui_slot_inventory"], cx, cy2, 104, 104, tint=bc.col(P["panelLight"]), border=borders["ui_slot_inventory"])
        c.picture(die_image(96, ((1, 2, 3), (4, 5, 6), (2, 3, 1), (6, 2, 4))[i % 4], P), cx + 8, cy2 + 6, 88)
    # 下一关 (ShopLeave14): UiKit.ActionButton, primary, label LabelOn11 on the face, 34 bold, the arrow after it.
    x, y, w, h = L["leave"]
    face = step_button(c, kit, borders, P, x, y, w, h, P["primary"], step_primary)
    key, ratio = label_on11(face, P)
    notes.append(f"下一关: face {face}, label {key} {ratio:.2f}:1, step {step_primary}")
    label_w, arrow = width_of("下一关", 34), 42
    sx = x + (w - (label_w + 10 + arrow)) / 2
    c.text(sx, y + (h - 7) / 2, "下一关", 34, P[key], bc.FONT, "lm")
    c.sprite(kit["ui_icon_arrow"], sx + label_w + 10, y + (h - 7 - arrow) / 2, arrow, arrow, tint=bc.col(P[key]))
    return c.image()


# ---------------------------------------------------------------------------------------------- the style page
def style_page(borders, sprites, page, palette, notes: list) -> Image.Image:
    """GameApp.Art073StyleLegend (骰子样式): the dialog (Panel, a paper outline) with eight 354 x 358 ui_section_panel cells,
    each with a die, the name on ui_chip_label and the Muted note over the cell's centre. Today the cells take no tint;
    W20X multiplies them by section, which comic sets to FFFFFF, so the page is the same before and after."""
    P = dict(palette)
    kit = sprites
    c = bc.UnityCanvas(1920, 1080, P["background"], 1.0)
    c.buf[...] = bc._lin_texture(page.convert("RGBA"))
    plain(c, (4, 8, 9, 89), 0, 0, 1920, 1080)                       # the scrim
    X, Y, W, H = 208, 96, 1504, 888
    plain(c, bc.rgb8(P["panel"]) + (255,), X, Y, W, H)
    for rect in ((X, Y, W, 3), (X, Y + H - 3, W, 3), (X, Y, 3, H), (X + W - 3, Y, 3, H)):
        plain(c, bc.rgb8(P["paper"]) + (255,), *rect)
    c.text(X + 24, Y + 8 + 40, "骰子样式", 32, P["paper"], bc.FONT, "lm")
    face = step_button(c, kit, borders, P, X + W - 88, Y + 12, 64, 64, P["paper"], P["buttonStep"])
    c.text(X + W - 88 + 32, Y + 12 + 28, "×", 23, P[label_on11(face, P, "ink")[0]], bc.FONT, "mm")
    c.text(X + 24, Y + 84, "拖动任意一颗骰子可以旋转查看。纹理与黄金按面生效，玻璃与临时按整颗骰子生效。", 20, P["muted"], bc.FONT)
    tint = P.get("section", "FFFFFF")
    cell_fill = drawn_centre(kit["ui_section_panel"], tint)
    ratio = bc.contrast(P["muted"], cell_fill)
    notes.append(f"style cells: ui_section_panel x {tint} = {cell_fill}; Muted {P['muted']} on it {ratio:.2f}:1")
    names = ("普通", "光滑", "粗糙", "黄金", "玻璃", "复合", "临时", "锁定")
    for i in range(8):
        x, y = X + 24 + i % 4 * 366, Y + 136 + i // 4 * 370
        c.sprite(kit["ui_section_panel"], x, y, 354, 358, tint=bc.col(tint), border=borders["ui_section_panel"])
        c.picture(die_image(150, ((1, 2, 3), (4, 5, 6), (2, 3, 1), (6, 2, 4))[i % 4], P), x + 57 + 20, y + 20, 200)
        c.sprite(kit["ui_chip_label"], x + 112, y + 240, 130, 40, border=borders["ui_chip_label"])
        c.text(x + 112 + 65, y + 260, names[i], 22, P["ink"], bc.FONT, "mm")
        for j, line in enumerate(("说明占位：样式说明的第一行，", "第二行，Muted 16 号字。")):
            c.text(x + 177, y + 288 + j * 20, line, 16, P["muted"], bc.BODY_FONT, "ma")
    return c.image()


def main():
    specs, borders, sprites, table, page, palette, arts = load_kit()
    OUT.mkdir(parents=True, exist_ok=True)
    font = ImageFont.truetype(str(bc.LATIN), 20)
    play, rows, card_lines = play_evidence(specs, sprites, table, font)
    play.save(OUT / "play_cards.png", optimize=False, compress_level=9)
    rows.save(OUT / "card_row.png", optimize=False, compress_level=9)
    actions, lines = action_evidence(specs, sprites, table, font)
    actions.save(OUT / "action_row.png", optimize=False, compress_level=9)
    (OUT / "action_row.txt").write_text("\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    now_notes, after_notes, style_notes = [], [], []
    now = shop_mock(borders, sprites, page, palette, arts, False, now_notes)
    caption_bar(now, "W14 SHOP AS THE CODE DRAWS IT TODAY: section panel x panel, caption strip x panelLight, steps "
                     "8F453A / B56B29, over Table/page_scene.png. Offline emulation, stand-in content.",
                font).save(OUT / "shop_w14_now.png", optimize=False, compress_level=9)
    after = shop_mock(borders, sprites, page, palette, arts, True, after_notes)
    caption_bar(after, "SIMULATED AFTER W20X: section FFFFFF (the authored blue panel), caption FBDC0F with the labels "
                       "LabelOn11 picks, ink steps, over Table/page_scene.png. Not the game today.",
                font, (255, 220, 15)).save(OUT / "shop_after_w20x.png", optimize=False, compress_level=9)
    style = style_page(borders, sprites, page, palette, style_notes)
    caption_bar(style, "DICE STYLE PAGE (Art073StyleLegend) with section FFFFFF: the same today (cells untinted) and after "
                       "W20X (cells x section); close button step ink as after W20X. Backdrop simplified.",
                font).save(OUT / "style_page.png", optimize=False, compress_level=9)
    for line in card_lines:
        print("COMIC_CARD_ROW", line)
    for line in lines:
        print("COMIC_ACTION_ROW", line)
    for line in now_notes:
        print("COMIC_SHOP_NOW", line)
    for line in after_notes:
        print("COMIC_SHOP_AFTER_W20X", line)
    for line in style_notes:
        print("COMIC_STYLE_PAGE", line)
    print("COMIC_R3_EVIDENCE", OUT.relative_to(ROOT).as_posix(), sorted(p.name for p in OUT.iterdir()))


if __name__ == "__main__":
    main()
