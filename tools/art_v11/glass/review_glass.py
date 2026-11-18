"""Offline review mocks for the glass (彩绘玻璃) kit: Python emulations of the game's drawing, not Unity captures.

    python tools/art_v11/glass/build_glass.py --review         # ArtSource/Art11/glass/review/ (reads the shipped kit)
    python tools/art_v11/glass/build_glass.py --card-review    # ArtSource/Art11/glass/card_template_review.png

build_glass.py also calls build_outputs() during a build for preview.png and contact_sheet.png. The palette key
buttonSecondary (code task W20Y) is set in theme.json since W20Y landed, and every caption that draws it names it
("simulated" while it was pending); a disabled SolidButton11 takes its label from LabelOn11(face, Muted) (code task
W20Y2). Names, texts, pictures (the glass StyleKit anchors) and dice (2D stand-ins for the 3D dice) are stand-ins.
"""
from __future__ import annotations

import importlib.util
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve()
_SPEC = importlib.util.spec_from_file_location("glass_build", HERE.parent / "build_glass.py")
bg = importlib.util.module_from_spec(_SPEC)
_SPEC.loader.exec_module(bg)
ROOT = bg.ROOT
OUT = ROOT / bg.REVIEW / "review"
THEME = ROOT / bg.THEME
LABEL, NOTE, DARK_BG = (246, 240, 222), (200, 192, 176), (20, 20, 28)
SECONDARY = ("buttonSecondary " + bg.W20Y_TOKENS["buttonSecondary"] +
             " (W20Y key, set in theme.json): every SolidButton11 face; disabled labels by LabelOn11(face, Muted) (W20Y2)")
PLAY_STATES = ("a", "b3", "c", "f", "rolling", "qualified", "boss", "pressure", "boss_play")
STATE_NAMES = {"a": "a: dice selected", "b3": "b3: scoring", "c": "c: boss stage, first roll", "f": "f: 7 cards, 2 locked",
               "rolling": "rolling", "qualified": "target reached", "boss": "boss (full-tray rule, two-line hint)",
               "pressure": "pressure stage", "boss_play": "boss in play (final tier, boss factor in the formula)"}


def palette_of(theme_json: Path | None = None) -> dict:
    """theme.json's palette (buttonSecondary included since W20Y landed)."""
    path = theme_json or THEME / "theme.json"
    return json.loads(path.read_text(encoding="utf-8"))["palette"]


def load():
    kit = bg.Kit(folder=THEME / "UI")
    with Image.open(THEME / "Table/table_scene.png") as source:
        table = source.convert("RGB")
    with Image.open(THEME / "Table/page_scene.png") as source:
        page = source.convert("RGB")
    return kit, table, page, palette_of()


def font(size: int, cjk: bool = False):
    return ImageFont.truetype(str(bg.BODY_FONT if cjk else bg.LATIN), size)


def caption_bar(im: Image.Image, lines: list[str], colour=LABEL) -> Image.Image:
    f = font(18, cjk=True)
    out = Image.new("RGB", (im.width, im.height + 10 + 24 * len(lines)), DARK_BG)
    d = ImageDraw.Draw(out)
    for i, line in enumerate(lines):
        d.text((12, 6 + 24 * i), line, fill=colour, font=f)
    out.paste(im, (0, 10 + 24 * len(lines)))
    return out


def fallback_note(kit: bg.Kit) -> str:
    names = sorted(kit.fallback)
    return ("default sprites (not drawn by this kit, as UiKit.Template falls back): " + ", ".join(names)) if names else \
        "every sprite drawn is the glass kit's"


def save(im: Image.Image, path: Path):
    path.parent.mkdir(parents=True, exist_ok=True)
    im.save(path, format="PNG", optimize=False, compress_level=9)


# ---------------------------------------------------------------------------------------------- the W14 shop
SHOP_TOP, SHOP_GAP, SHOP_HEAD, SHOP_PAD, SHOP_FOOT, ROW_GAP = 92, 8, 68, 12, 7, 6


def shop_layout(large: bool = False) -> dict:
    """GameApp.Shop14 S14."""
    card_h = 364 if large else 348
    tile_h, caption, leave = (116, 40, 72) if large else (104, 28, 96)
    avail = 1072 - SHOP_TOP - 2 * SHOP_GAP
    cards, build = SHOP_HEAD + card_h + SHOP_FOOT, 4 + caption + 2 + tile_h + SHOP_FOOT
    goods = avail - cards - build
    owned = SHOP_HEAD + 2 * 104 + 6 + SHOP_FOOT
    dice = avail - owned - leave
    y = SHOP_TOP
    return {"top": (16, 12, 1888, 72), "cards": (16, y, 1404, cards), "goods": (16, y + cards + SHOP_GAP, 1404, goods),
            "build": (16, y + cards + goods + 2 * SHOP_GAP, 1404, build), "dice": (1432, y, 472, dice),
            "owned": (1432, y + dice + SHOP_GAP, 472, owned), "leave": (1432, y + dice + owned + 2 * SHOP_GAP, 472, leave),
            "card_h": card_h, "tile_h": tile_h, "caption": caption}


def tile(c: bg.Canvas, kit: bg.Kit, P: dict, x, y, w, h, rarity, title, art, ordinal: int):
    """GameApp.Card11 DrawTile13 (W14 E3): the compact face at half its frame, the framed icon block with the gem inside
    its foot, the name under it, the ordinal disc top left."""
    c.sprite(kit, "ui_card_controller_compact", x, y, w, h, mult=2.0)
    fl, fb, fr, ft = kit.insets("ui_card_controller_compact", mult=2.0)
    content = (x + fl + 2, y + ft + 2, w - fl - fr - 4, h - ft - fb - 4)
    name_h = math.ceil(19.5 * 1.46) + 2
    lead = 22 if ordinal else 0
    block = max(24, min(content[2] - lead, content[3] - name_h - 2))
    bx = content[0] + lead if ordinal else content[0] + (content[2] - block) / 2
    by = content[1]
    tint = P[bg.RARITY_KEY[rarity]]
    c.sprite(kit, "ui_card_frame_rarity", bx, by, block, block, tint=tint)
    rl, rb, rr, rt = kit.ring_insets("ui_card_frame_rarity")
    c.picture(bg.anchor_art(art), bx + rl + 1, by + rt + 1, min(block - rl - rr, block - rt - rb) - 2)
    if rarity:
        gem = min(16, block * .3)
        c.sprite(kit, "ui_gem_" + rarity, bx + block / 2 - gem / 2, by + block - gem - 1, gem, gem, tint=tint)
    if ordinal:
        c.sprite(kit, "ui_pip_dot", content[0], content[1], 20, 20, tint=P["ink"], preserve=True)
        c.text(content[0], content[1], 20, 20, str(ordinal), 12, bg.label_on(P["ink"], P)[0], "mm", bold=True)
    face = kit.centre("ui_card_controller_compact")
    c.text(content[0], content[1] + content[3] - name_h, content[2], name_h, title, 19.5, bg.label_on(face, P, P["ink"])[0],
           "mm", bold=True)


def shop_mock(kit: bg.Kit, page: Image.Image, palette: dict, notes: list) -> Image.Image:
    """The W14 shop at 100 % text as GameApp.Shop14 lays it out, with the tints the code applies after W20X: sections x
    section, caption strips x caption, the step under 下一关 x buttonStepPrimary. Four card offers, nine goods, two new
    dice and two face upgrades, eight owned dice, six cards and an item in 我的构筑."""
    P = dict(palette)
    L = shop_layout(False)
    c = bg.Canvas(1920, 1080)
    c.backdrop(page)
    # Top bar (ShopTopBar14, ShopTopRight14, ShopTopButtons14).
    tx, ty, tw, th = L["top"]
    c.sprite(kit, "ui_topbar", tx, ty, tw, th, tint=P["panel"])
    bar = bg.drawn(kit.centre("ui_topbar"), P["panel"])
    fl, fb, fr, ft = kit.insets("ui_topbar")
    title_y = ty + ft + max(0, (th - ft - fb - bg.line11(26, 1)) / 2)
    text_c = bg.label_on(bar, P, P["textColor"])[0]
    c.text(tx + fl + 16, title_y, 360, bg.line11(26, 1), "商店 · 第 5 关前", 26, text_c, "lm", bold=True)
    centre_x = tx + fl + 16 + bg.text_width("商店 · 第 5 关前", 26) + 40
    c.text(centre_x, ty, 700, th, "下一关：第 5 关 · 普通 · 目标 5,200", 20, text_c, "lm")
    right = 1904 - 8
    icon_w = lambda label: 34 + 8 + bg.text_width(label, 20, False) + 4 + 28
    pause = (right - icon_w("设置"), 18, icon_w("设置"), 60)
    right = pause[0] - 10
    hand = (right - icon_w("手册"), 18, icon_w("手册"), 60)
    right = hand[0] - 10
    cycle_w = 44 + 8 + bg.text_width("查看本章", 20, False) + 4 + 28
    cycle = (right - cycle_w, 18, cycle_w, 60)
    right = cycle[0] - 16
    money_w = (bg.text_width("19", 34) + 4) * 1.12 + 6
    unit_w = bg.text_width("金币", 20, False) + 4
    gold_w = 40 + money_w + unit_w + 6
    gx = right - gold_w
    c.sprite(kit, "ui_icon_coin", gx, 24, 36, 36, preserve=True)
    c.text(gx + 40, ty, gold_w - 40 - unit_w - 6, th, "19", 34, bg.label_on(bar, P, P["gold"])[0], "rm", bold=True)
    c.text(gx + gold_w - unit_w, ty, unit_w, th, "金币", 20, bg.label_on(bar, P, P["muted"])[0], "lm")
    bg.solid_button(c, kit, P, *pause, "设置", 20, icon="ui_icon_gear")
    bg.solid_button(c, kit, P, *hand, "手册", 20, icon="ui_icon_book", notes=notes, name="shop top-bar buttons")
    fill = bg.solid_button(c, kit, P, *cycle, "", 20)
    k = bg.label_on(fill, P, P["textColor"])[0]
    for i in range(3):
        size = 12 if i < 1 else 10
        c.sprite(kit, "ui_pip_dot", cycle[0] + 16 + i * 16 + (12 - size) / 2, cycle[1] + (60 - size) / 2, size, size, tint=k,
                 preserve=True)
    c.text(cycle[0] + 16 + 44 + 8, cycle[1], cycle_w - 68, 60, "查看本章", 20, k, "lm")
    # Sections (Section14) and 我的构筑 (ShopBuild14: the section panel without a strip).
    section = P.get("section", P["panel"])
    caption = P.get("caption", P["panelLight"])
    sl, sb, sr, st = kit.insets("ui_section_panel")
    panel_fill = bg.drawn(kit.centre("ui_section_panel"), section)
    strip_fill = bg.drawn(kit.centre("ui_section_header"), caption)
    tk, tr, _ = bg.label_on(strip_fill, P, P["textColor"])
    mk, mr, _ = bg.label_on(strip_fill, P, P["muted"])
    notes.append(f"section fill {panel_fill} (ui_section_panel x {section}); caption strip {strip_fill} "
                 f"(ui_section_header x {caption}): titles {tk} {tr:.2f}:1, notes {mk} {mr:.2f}:1")
    titles = {"cards": "卡牌", "goods": "升级与服务", "dice": "骰子与骰面", "owned": "我的骰子 8/8 · 提交栏 5 格"}
    title_right = {}
    for name, rect in L.items():
        if name not in ("cards", "goods", "build", "dice", "owned"):
            continue
        x, y, w, h = rect
        c.sprite(kit, "ui_section_panel", x, y, w, h, tint=section)
        if name in titles:
            strip_h = SHOP_HEAD - st - 2
            c.sprite(kit, "ui_section_header", x + sl, y + st, w - sl - sr, strip_h, tint=caption)
            top = st + (strip_h - bg.line11(22, 1)) * .5
            c.text(x + sl + 14, y + top, 320, bg.line11(22, 1), titles[name], 22, tk, "lm", bold=True)
            title_right[name] = x + sl + 14 + bg.text_width(titles[name], 22) + 2
    # 卡牌: refresh in the strip, 在售, four offers (220 x 348, stride 232).
    x, y, w, h = L["cards"]
    word_w, price_w = bg.text_width("刷新", 20, False) + 4, 26 + 4 + bg.text_width("3", 22) + 4 + 4
    refresh_w = 16 + 34 + 8 + word_w + 10 + price_w + 16
    rx = x + w - SHOP_PAD - refresh_w
    fill = bg.solid_button(c, kit, P, rx, y + 4, refresh_w, 60, "", 20)
    k = bg.label_on(fill, P, P["textColor"])[0]
    c.sprite(kit, "ui_icon_refresh", rx + 16, y + 4 + 13, 34, 34, tint=k, preserve=True)
    c.text(rx + 58, y + 4, word_w, 60, "刷新", 20, k, "lm")
    c.sprite(kit, "ui_icon_coin", rx + 58 + word_w + 10, y + 4 + 17, 26, 26, preserve=True)
    c.text(rx + 58 + word_w + 40, y + 4, 40, 60, "3", 22, bg.label_on(fill, P, P["gold"])[0], "lm", bold=True)
    c.text(title_right["cards"] + 16, y + (SHOP_HEAD - bg.line11(18, 1)) * .5, 200, bg.line11(18, 1), "在售 4/4", 18, mk, "lm")
    for i in range(4):
        rarity, title, text, art = bg.MOCK_CARDS[(0, 1, 2, 7)[i]]
        bg.tall_card(c, kit, P, x + SHOP_PAD + i * 232, y + SHOP_HEAD, 220, L["card_h"], rarity, title, text, art, shop=True,
                     price=(8, 9, 11, 6)[i], value="×1.5" if i == 1 else None)
    # 升级与服务: 指定升级 in the strip, nine goods rows (452 x 103, price 180).
    x, y, w, h = L["goods"]
    study = "指定升级 · 加价 2"
    study_w = bg.text_width(study, 20, False) + 4 + 32
    bg.solid_button(c, kit, P, x + w - SHOP_PAD - study_w, y + 4, study_w, 60, study, 20)
    area = h - SHOP_HEAD - SHOP_FOOT
    row_h = int(min(150, (area - 2 * ROW_GAP) / 3))
    col_w = (w - 2 * SHOP_PAD - 2 * 12) / 3
    goods = (("牌型升级", "一对", "Lv.0 → Lv.1 · ×2 → ×2.5", "U10"), ("牌型升级", "全蓝", "Lv.1 → Lv.2 · ×3 → ×3.5", "U10"),
             ("牌型升级", "顺子", "Lv.0 → Lv.1 · ×4 → ×4.5", "U10"), ("道具", "蓝色浪潮", "下一次有效提交全部骰子临时视为蓝色。", "I10"),
             ("道具", "重来", "重掷一次，不消耗掷骰次数。", "I10"), ("道具", "点金", "选一颗骰子，这一面镀金。", "I10"),
             ("服务", "增加控制", "卡牌栏位 +1。", "C108"), ("服务", "删除骰子", "移除一颗骰子。", "C108"),
             ("服务", "进货", "下一家商店多一张卡。", "C108"))
    for i, (cat, title, text, art) in enumerate(goods):
        bg.compact_card(c, kit, P, x + SHOP_PAD + (i % 3) * (col_w + 12), y + SHOP_HEAD + (i // 3) * (row_h + ROW_GAP), col_w,
                        row_h, None, title, text, art, price=2 + i % 3, price_w=180, category=cat)
    # 我的构筑: caption line, six tiles (100 x 104), the items.
    x, y, w, h = L["build"]
    build_key = bg.label_on(panel_fill, P, P["textColor"])
    build_muted = bg.label_on(panel_fill, P, P["muted"])
    notes.append(f"我的构筑 on the section fill {panel_fill}: caption {build_key[0]} {build_key[1]:.2f}:1, "
                 f"notes {build_muted[0]} {build_muted[1]:.2f}:1")
    cy = y + 4
    c.text(x + SHOP_PAD, cy, 120, 28, "我的构筑", 18, build_key[0], "lm", bold=True)
    tx2 = x + SHOP_PAD + bg.text_width("我的构筑", 18) + 16
    capacity = "卡牌栏位 5/5 · 共 6/10 张"
    c.text(tx2, cy, 400, 28, capacity, 18, build_muted[0], "lm")
    c.text(tx2 + bg.text_width(capacity, 18, False) + 14, cy, 200, 28, "拖动调整顺序", 18, build_muted[0], "lm")
    items_x = x + w - SHOP_PAD - (5 * 60 + 4 * 6)
    c.text(items_x, cy, 120, 28, "道具 1/3", 18, build_key[0], "lm")
    for i in range(6):
        rarity, title, text, art = bg.MOCK_CARDS[i]
        tile(c, kit, P, x + SHOP_PAD + i * 104, y + 34, 100, L["tile_h"], rarity, title, art, i + 1)
    for i in range(3):
        ix, iy = items_x + i * 66, y + 34 + 22
        if i == 0:
            c.sprite(kit, "ui_card_item", ix, iy, 60, 60)
            c.picture(bg.anchor_art("I10"), ix + 8, iy + 6, 44)
        else:
            c.sprite(kit, "ui_slot_inventory", ix, iy, 60, 60, tint=P["panelLight"])
    # 骰子与骰面: two new dice and two face upgrades (448 x 121, price 150).
    x, y, w, h = L["dice"]
    row_h = int((h - SHOP_HEAD - SHOP_FOOT - 3 * ROW_GAP) / 4)
    rows = (("新骰子 · 点数和 21", "红蓝各半", "", "die1"), ("新骰子 · 点数和 24", "大点蓝骰", "", "die2"),
            ("骰面改造", "配重", "这一面权重+1（上限5），更容易掷出这一面。", "U10"), ("骰面改造", "抛光", "这一面改为光滑。", "U10"))
    for i, (cat, title, text, art) in enumerate(rows):
        if art.startswith("die"):
            bg._ANCHOR_CACHE[art] = bg.die_image(120, ((5, 2, 3), (6, 4, 4))[i], (("red", "blue", "red"), ("blue", "blue", "blue"))[i], P)
        bg.compact_card(c, kit, P, x + SHOP_PAD, y + SHOP_HEAD + i * (row_h + ROW_GAP), w - 2 * SHOP_PAD, row_h, None, title, text,
                        art, price=(5, 6, 2, 2)[i], price_w=150, category=cat)
    # 我的骰子: eight cells (ui_slot_inventory x PanelLight).
    x, y, w, h = L["owned"]
    gap = (w - 2 * SHOP_PAD - 4 * 104) / 3
    for i in range(8):
        cx, cy2 = x + SHOP_PAD + (i % 4) * (104 + gap), y + SHOP_HEAD + (i // 4) * 110
        c.sprite(kit, "ui_slot_inventory", cx, cy2, 104, 104, tint=P["panelLight"])
        c.picture(bg.die_image(84, ((1, 2, 3), (4, 5, 6), (2, 3, 1), (6, 2, 4))[i % 4],
                               (("red", "red", "red"), ("blue", "blue", "red"), ("red", "blue", "blue"), ("blue", "red", "blue"))[i % 4], P),
                  cx + 6, cy2 + 4, 92)
    # 下一关 (ShopLeave14): UiKit.ActionButton, primary, label LabelOn11 picks, 34 bold, the arrow after it.
    x, y, w, h = L["leave"]
    face = bg.button(c, kit, P, x, y, w, h, P["primary"], "", 34, primary=True, notes=notes, name="下一关")
    key = bg.label_on(face, P)[0]
    label_w, arrow = bg.text_width("下一关", 34), 42
    sx = x + (w - (label_w + 10 + arrow)) / 2
    c.text(sx, y, label_w + 4, h - 7, "下一关", 34, key, "lm", bold=True)
    c.sprite(kit, "ui_icon_arrow", sx + label_w + 10, y + (h - 7 - arrow) / 2, arrow, arrow, tint=key, preserve=True)
    return c.image()


# ---------------------------------------------------------------------------------------------- the stage select
CYCLE_W, CYCLE_H, CYCLE_GAP = 488, 380, 18      # GameApp.V10 CycleW10, CycleH10, CycleGap10


def cycle_card(c: bg.Canvas, kit: bg.Kit, P: dict, x, y, stage: int, kind: str, current: bool, cleared: bool, notes: list):
    """GameApp.V10 CycleCard10 at 100 % text: a paper frame (ui_paper_panel), the kind strip and seal, the title, then by
    kind: the goal and reward (normal), the two challenge rows (pressure, the current card: FlatButtons), the goal and the
    boss modifier (boss). The seal's -6 degree tilt is not drawn."""
    ink, paper_muted = P["ink"], bg.lerp_hex(P["ink"], P["paper"], .28)
    tint = P["danger"] if kind == "boss" else P["red"] if kind == "pressure" else P["teal"]
    inner = CYCLE_W - 48
    c.sprite(kit, "ui_paper_panel", x, y, CYCLE_W, CYCLE_H)
    if current:
        c.sprite(kit, "ui_ring", x - 4, y - 4, CYCLE_W + 5, CYCLE_H + 4, tint=P["gold"])
    c.fill(x + 24, y + 16, inner, 4, tint)
    c.text(x + 24, y + 24, 280, 58, f"第 {stage} 关", 30, ink, "lm", bold=True)
    for (bx, by, bw, bh) in ((CYCLE_W - 142, 32, 116, 2), (CYCLE_W - 142, 74, 116, 2), (CYCLE_W - 142, 32, 2, 44),
                             (CYCLE_W - 28, 32, 2, 44)):
        c.fill(x + bx, y + by, bw, bh, tint)
    c.text(x + CYCLE_W - 138, y + 32, 108, 44, {"boss": "首领", "pressure": "高压"}.get(kind, "普通"), 18, tint, "mm")
    c.fill(x + 24, y + 90, inner, 2, bg.lerp_hex(ink, P["paper"], .65))
    if kind == "pressure":
        for row_y, title, target, reward, selected, risk in ((100, "普通挑战", "600", 5, True, False),
                                                           (194, "高压挑战", "900", 10, False, True)):
            if current:
                face = bg.lerp_hex(P["paper"], P["gold"], .45) if selected else paper_muted
                c.sprite(kit, "ui_frame_block" if selected else "ui_button_ghost", x + 24, y + row_y, inner, 86, tint=face)
                c.text(x + 24 + inner - 158, y + row_y, 150, 86, "✓ 已选择" if selected else "选择挑战", 17, ink, "mm")
            c.text(x + 38, y + row_y + 4, inner - 178, 38, title, 22, ink, "lm", bold=True)
            c.text(x + 38, y + row_y + 44, inner - 178, 36, f"目标 {target} · +{reward} 金币", 17, ink, "lm")
            if risk:
                c.fill(x + 24 + inner - 6, y + row_y + 10, 4, 66, P["red"])
        c.text(x + 24, y + 292, inner, 72, "选一种挑战再开始。高压：目标 ×1.5，过关金币 +5。", 16, paper_muted, "lt")
    elif kind == "boss":
        c.text(x + 24, y + 96, inner, 28, "目标分数", 16, paper_muted, "lm")
        c.text(x + 24, y + 120, inner, 54, "1,000", 36, ink, "lm", bold=True)
        c.text(x + 24, y + 176, inner, 26, "首领减益", 16, paper_muted, "lm")
        c.text(x + 24, y + 198, inner, 38, "禁顺", 24, P["danger"], "lm", bold=True)
        c.text(x + 24, y + 236, inner, 58, "顺子不计分；本关的顺子牌型倍率为 ×1。", 16, ink, "lt")
        c.fill(x + 24, y + 320, inner, 1, bg.lerp_hex(ink, P["paper"], .65))
        c.text(x + 24, y + 324, inner, 48, "战利品 · 3 个牌型或颜色，选 1 个直接 +2 级。", 15, ink, "lt")
    else:
        c.text(x + 24, y + 104, inner, 34, "目标分数", 18, paper_muted, "lm")
        c.text(x + 24, y + 138, inner, 82, "300", 44, ink, "lm", bold=True)
        c.text(x + 24, y + 224, inner, 50, "过关奖励  +5 金币", 23, ink, "lm")
        c.text(x + 24, y + 292, inner, 60, "没有额外条件。", 16, paper_muted, "lt")
    if cleared:
        c.fill(x + 6, y + 6, CYCLE_W - 16, CYCLE_H - 18, P["paper"], .5)
        c.text(x + 24, y + 60, 200, 28, "✓ 已通过", 16, P["teal"], "lm", bold=True)


def select_mock(kit: bg.Kit, page: Image.Image, palette: dict, notes: list) -> Image.Image:
    """The stage select (GameApp.V10 StagePreview10, 100 % text) over page_scene: the top bar without the score frame, the
    card row, eight chapters of stage ticks, the three stage cards of the chapter (stage 1 cleared, stage 2 the current
    pressure stage with the ordinary challenge chosen, stage 3 the boss), the budget line, the order note and 开始挑战.
    The texts StagePreview10 draws straight on the table are drawn so (W16 moves them onto panels)."""
    P = dict(palette)
    c = bg.Canvas(1920, 1080)
    c.backdrop(page)
    bg.draw_top_bar(c, kit, P, False, P["panelLight"], "第 2 关", "第 1 章 · 高压关", notes, score=False)
    bg.draw_card_row(c, kit, P, 3, False, notes)
    shift = -160
    for chapter in range(8):
        left = 380 + shift + chapter * 188
        cur_ch = chapter == 0
        c.text(left, 340, 164, 40, f"第 {chapter + 1} 章", 17, P["gold"] if cur_ch else P["muted"], "mm", bold=cur_ch,
               label="chapter")
        c.fill(left, 412, 164, 3 if cur_ch else 1, P["gold"] if cur_ch else P["panelLight"])
        for within in range(3):
            index = chapter * 3 + within
            x = left + within * 56
            complete, current, boss = index < 1, index == 1, within == 2
            fill = P["gold"] if current else bg.lerp_hex(P["felt"], P["teal"], .35) if complete else \
                bg.lerp_hex(P["felt"], P["danger"], .28) if boss else P["felt"]
            c.sprite(kit, "ui_frame_block", x, 380, 48, 28, tint=fill, mult=1.6)
            label_colour = P["ink"] if current else P["teal"] if complete else P["paper"] if boss else P["muted"]
            c.text(x, 380, 48, 28, "✓" if complete else str(index + 1), 12, label_colour, "mm")
    left = 380 + shift - 10
    for i, (kind, cleared, current) in enumerate((("normal", True, False), ("pressure", False, True), ("boss", False, False))):
        cycle_card(c, kit, P, left + i * (CYCLE_W + CYCLE_GAP), 432, i + 1, kind, current, cleared, notes)
    c.text(416 + shift, 822, 1380, 48, "投掷 3 次 / 手     提交 3 次 / 关     提交栏 5 格", 26, P["muted"], "lm", label="budgets")
    c.text(416 + shift, 870, 1380, 62, "入关后，控制器顺序固定。", 20, P["gold"], "lt", label="order note")
    # StartStage: UiKit.ActionButton(Primary); its label is the Ink ActionButton sets (StagePreview10 does not re-pick it
    # with LabelOn11 as PrimaryButton11 and ShopLeave14 do; W16's bug, coordinator 2026-11-18).
    face = bg.button(c, kit, P, 600, 936, 720, 76, P["primary"], "开始挑战 →", 23, P["ink"], primary=True)
    notes.append(f"开始挑战: face {face}, label ink (ActionButton, not re-picked; W16) {bg.contrast(P['ink'], face):.2f}:1; "
                 f"LabelOn11 would pick {bg.label_on(face, P)[0]} {bg.label_on(face, P)[1]:.2f}:1")
    return c.image()


# ---------------------------------------------------------------------------------------------- beside the concept
def beside_concept(page: str, mocks: list[tuple[str, Image.Image]]) -> Image.Image:
    """A concept page and the matching mocks side by side at 960 x 540 each, labelled."""
    f = font(18)
    with Image.open(ROOT / bg.CONCEPTS / (page + ".png")) as source:
        concept = source.convert("RGB").resize((960, 540), Image.Resampling.LANCZOS)
    panels = [("CONCEPT " + page + ".png (the user's checkpoint-3 choice)", concept)] + \
             [(label, im.resize((960, 540), Image.Resampling.LANCZOS)) for label, im in mocks]
    out = Image.new("RGB", (len(panels) * 960 + (len(panels) - 1) * 12, 540 + 32), DARK_BG)
    d = ImageDraw.Draw(out)
    for i, (label, im) in enumerate(panels):
        x = i * 972
        d.text((x + 8, 7), label, fill=LABEL, font=f)
        out.paste(im, (x, 32))
    return out


# ---------------------------------------------------------------------------------------------- measurements
def hint_room(meas: dict, kit: bg.Kit) -> str:
    """The hint's inked box against the hint panel's inner frame (FrameInsets11), design units."""
    x, y, w, h = meas["hint panel"]
    l, b, r, t = kit.insets("ui_panel_hud")
    gx0, gy0, gx1, gy1 = meas["hint"]
    return (f"hint {meas['hint lines']} line(s): inked {gy0 - y:.1f}-{gy1 - y:.1f} units in the {h}-unit panel; inner frame "
            f"{t:.1f} / {h - b:.1f}: room above {gy0 - y - t:.1f}, below {h - b - (gy1 - y):.1f} (offline estimate)")


def action_row_table() -> list[str]:
    """Per state and text size: the action row's elements (GameApp.Play11 DrawActions11 as L11 lays them out, UiKit.Button's
    step 2 units below its face and its 3-unit focus outline, the qualified end ring 4 units out) and whether the rose
    window (with its 3 px bleed) stays in the free gap between them."""
    cx, cy, R = bg.ROSE
    rose = (cx - R - bg.BLEED, cy - R - bg.BLEED, cx + R + bg.BLEED, cy + R + bg.BLEED)
    lines = ["state / text: elements left of the window (right edge) | right of it (left edge) | rose window "
             f"x {rose[0]:.0f}-{rose[2]:.0f}, y {rose[1]:.0f}-{rose[3]:.0f} (incl. bleed) | free"]
    ok_all = True
    for state in PLAY_STATES:
        s = bg.ACTION_STATES[state]
        for large in (False, True):
            L = bg.play_layout(large)
            boxes = [("hint", L["hint"])]
            if s["secondary"]:
                boxes.append(("secondary", L["secondary"]))
            if s["primary"]:
                x, y, w, h = L["primary"]
                boxes.append(("primary+focus", (x - 3, y - 3, w + 6, h + 3)))
            x, y, w, h = L["end"]
            boxes.append(("end", (x, y, w, h) if s["end"] != "qualified" else (x - 4, y - 4, w + 8, h + 4)))
            left = max(b[1][0] + b[1][2] for b in boxes if b[1][0] < rose[0])
            right = min(b[1][0] for b in boxes if b[1][0] >= rose[0])
            clear = all(rose[2] <= bx or rose[0] >= bx + bw or rose[3] <= by or rose[1] >= by + bh
                        for _, (bx, by, bw, bh) in boxes)
            ok_all &= clear and left <= rose[0] and right >= rose[2]
            lines.append(f"{state:10s} {150 if large else 100:3d} %: {left:.0f} | {right:.0f} | "
                         f"{'clear' if clear else 'OVERLAP'}")
    lines.append("ACTION_ROW_OK" if ok_all else "ACTION_ROW_FAIL")
    return lines


# ---------------------------------------------------------------------------------------------- composites
def card_rows(kit: bg.Kit, table: Image.Image, palette: dict) -> Image.Image:
    """The card row (GameApp.Play11 DrawCards11) with 0, 3, 5, 7 and 10 cards at 100 % and 150 % text, cropped to y 80-350
    of the play screen (the card row is 32, 96, 1448, 244): nothing on the table under it but the mosaic texture."""
    strips = []
    f = font(17)
    for n in (0, 3, 5, 7, 10):
        row = []
        for large in (False, True):
            notes: list = []
            im, _ = bg.play_mock(kit, table, "f", large, notes, palette, n_cards=n)
            crop = im.crop((0, 76, 1920, 352)).resize((960, 138), Image.Resampling.LANCZOS)
            label = Image.new("RGB", (960, 24), DARK_BG)
            ImageDraw.Draw(label).text((8, 3), f"{n} cards, {150 if large else 100} % text"
                                       + (" (two rows of compact cards)" if n > 7 or (large and n > 5) else ""),
                                       fill=LABEL, font=f)
            col = Image.new("RGB", (960, 162), DARK_BG)
            col.paste(label, (0, 0))
            col.paste(crop, (0, 24))
            row.append(col)
        line = Image.new("RGB", (1932, 162), DARK_BG)
        line.paste(row[0], (0, 0))
        line.paste(row[1], (972, 0))
        strips.append(line)
    out = Image.new("RGB", (1932, len(strips) * 170), DARK_BG)
    for i, s in enumerate(strips):
        out.paste(s, (0, i * 170))
    return out


def boss_stage(kit: bg.Kit, table: Image.Image, palette: dict) -> tuple[Image.Image, list[str]]:
    """The boss stage in play (final tier) at 100 % and 150 %: the dimmed mosaic, the boss tag, the stage card's and the
    tray's boss rings, the boss factor chip right after the red hand chip; with the boss colour's distances."""
    P = dict(palette)
    ims, chips = [], []
    for large in (False, True):
        notes: list = []
        im, meas = bg.play_mock(kit, table, "boss_play", large, notes, P)
        ims.append(im.resize((960, 540), Image.Resampling.LANCZOS))
        chips = meas.get("factor chips", [])
    plate = {key: fill for _, key, fill, _, _ in chips}
    lines = [f"boss {P['boss']}: CIEDE2000 to primary {P['primary']} {bg.de00(P['boss'], P['primary']):.1f}, "
             f"to the red pips {P['red']} {bg.de00(P['boss'], P['red']):.1f}, to pressure {P['pressure']} "
             f"{bg.de00(P['boss'], P['pressure']):.1f} (each >= 15)",
             f"formula row as drawn: red hand chip {plate.get('red')} | boss chip {plate.get('boss')}: CIEDE2000 "
             f"{bg.de00(plate.get('red'), plate.get('boss')):.1f}, contrast {bg.contrast(plate.get('red'), plate.get('boss')):.2f}:1; "
             + "; ".join(f"{name} label {ink} {bg.contrast(ink, fill):.2f}:1" + (f" ({steps} steps)" if steps else "")
                         for name, _, fill, ink, steps in chips)]
    sheet = Image.new("RGB", (960 * 2 + 12, 540), DARK_BG)
    sheet.paste(ims[0], (0, 0))
    sheet.paste(ims[1], (972, 0))
    return sheet, lines


def text_room_frames(kit: bg.Kit, table: Image.Image, page: Image.Image, palette: dict) -> Image.Image:
    """For the coordinator's decision on panel frames: the right column of the play screen (f, 150 % text) and a shop
    corner, with this kit's slim frames (shipped) beside phase 1b's jewel-strip frames (not shipped: FrameInsets11 18 px
    on every side of the panels, over the default's 6-10), redrawn from the generator's phase 1b parameters."""
    specs = {s["name"]: s for s in json.loads(bg.CONTRACT.read_text(encoding="utf-8"))["sprites"]}
    old = {}
    for name in ("ui_panel_hud", "ui_section_panel"):
        w, h = specs[name]["size"]
        old[name] = bg.gilt_tile(w, h, bg.PANEL, cut=bg.PANEL_CUT, bands=bg.jewel_bands(bg.COBALT_GLASS), stars=bg.PANEL_STARS)
    w, h = specs["ui_paper_panel"]["size"]
    old["ui_paper_panel"] = bg.gilt_tile(w, h, bg.PAPER, cut=bg.PANEL_CUT, bands=bg.jewel_bands(bg.RUBY_GLASS),
                                         stars=bg.PANEL_STARS)
    w, h = specs["ui_button_bevel"]["size"]
    old["ui_button_bevel"] = bg.came_pointed(w, h, bg.BUTTON_POINT, (5.0,) * 8, (4.0,) * 8, lo=.56, hi=.86)
    w, h = specs["ui_chip_formula"]["size"]
    old["ui_chip_formula"] = bg.came_pointed(w, h, bg.CHIP_POINT, (4.0,) * 8, (3.0,) * 8, lo=.6, hi=.86)
    w, h = specs["ui_topbar"]["size"]
    old["ui_topbar"] = bg.came_tile(w, h, cut=bg.BAR_CUT, came=5.0, bevel=5.0, lo=.58, hi=.84, lights=8.0)
    old_kit = bg.Kit(sprites={**{n: kit.image(n) for n in kit.own}, **old})
    f = font(18)
    panels = []
    for label, k in (("SHIPPED (phase 2): slim gilt frames, jewel glass in the corners; FrameInsets11 <= default", kit),
                     ("NOT SHIPPED (phase 1b): jewel-strip frames, panels 18 px on every side (default 6-10)", old_kit)):
        play, _ = bg.play_mock(k, table, "f", True, [], palette)
        shop = shop_mock(k, page, palette, [])
        col = Image.new("RGB", (960, 30 + 4 * 300 + 3 * 8), DARK_BG)
        ImageDraw.Draw(col).text((8, 5), label, fill=LABEL, font=f)
        # At 2x: the resources panel's top, the hint and the main button, the shop's dice section, a shop section corner.
        for i, box in enumerate(((1490, 340, 1970, 490), (20, 950, 500, 1100), (1426, 86, 1906, 236), (8, 590, 488, 740))):
            src = play if i < 2 else shop
            crop = Image.new("RGB", (480, 150), DARK_BG)
            crop.paste(src.crop(tuple(min(v, lim) for v, lim in zip(box, (1920, 1080, 1920, 1080)))), (0, 0))
            col.paste(crop.resize((960, 300), Image.Resampling.LANCZOS), (0, 30 + i * 308))
        panels.append(col)
    out = Image.new("RGB", (960 * 2 + 12, panels[0].height), DARK_BG)
    out.paste(panels[0], (0, 0))
    out.paste(panels[1], (972, 0))
    return out


def secondary_states(kit: bg.Kit, table: Image.Image, palette: dict) -> tuple[Image.Image, list[str]]:
    """SolidButton11 enabled and disabled (a quarter toward panelLight; W20Y2's LabelOn11(face, Muted) label), with
    buttonSecondary and, for comparison, unset (the code's lerp(Background, Panel, .55)), on the hosts it sits on: the
    slate, the mosaic, a dark panel, the top bar, the parchment of a card."""
    P = dict(palette)
    unset = {k: v for k, v in P.items() if k != "buttonSecondary"}
    hosts = (("slate (felt)", P["felt"]), ("mosaic (action row)", None), ("dark panel", bg.drawn(kit.centre("ui_panel_hud"), "FFFFFF")),
             ("top bar", bg.drawn(kit.centre("ui_topbar"), P["panel"])), ("parchment (card)", P["paper"]))
    cell_w, cell_h = 300, 96
    c = bg.Canvas(len(hosts) * cell_w, 4 * cell_h)
    c.backdrop(table.crop((100, 0, 100 + len(hosts) * cell_w, 340)))       # the mosaic column keeps this backdrop
    lines = []
    for i, (name, host) in enumerate(hosts):
        x = i * cell_w
        if host is not None:
            c.fill(x, 0, cell_w, 4 * cell_h, host)
        for j, (pal, enabled, tag) in enumerate(((P, True, "buttonSecondary"), (P, False, "buttonSecondary"),
                                                 (unset, True, "unset"), (unset, False, "unset"))):
            notes: list = []
            bg.solid_button(c, kit, pal, x + 18, j * cell_h + 14, cell_w - 36, 68, "重掷 · 还能掷 2", 21, enabled=enabled,
                            notes=notes, name=f"{tag} {'enabled' if enabled else 'disabled'}")
            if i == 0:
                lines.extend(notes)
    im = c.image()
    f = font(16, cjk=True)
    out = Image.new("RGB", (im.width + 260, im.height + 30), DARK_BG)
    d = ImageDraw.Draw(out)
    for i, (name, _) in enumerate(hosts):
        d.text((260 + i * cell_w + 8, 6), name, fill=LABEL, font=f)
    sec, fallback = bg.secondary_face(P), bg.secondary_face(unset)
    for j, label in enumerate((f"buttonSecondary {sec}, on", f"buttonSecondary {sec}, off", f"unset {fallback}, on",
                               f"unset {fallback}, off")):
        d.text((8, 30 + j * cell_h + 36), label, fill=LABEL, font=f)
    out.paste(im, (260, 30))
    return out, lines


# ---------------------------------------------------------------------------------------------- contact sheet
TYPICAL_TINT = {
    "ui_button_bevel": "primary", "ui_button_ghost": "ghost", "ui_frame_block": "panelLight", "ui_ring": "gold",
    "ui_progress_fill": "blue", "ui_progress_track": "panel", "ui_section_header": "caption", "ui_pip_dot": "ink",
    "ui_icon_arrow": "textColor", "ui_icon_book": "ink", "ui_icon_gear": "ink", "fx_arc_short": "gold",
    "fx_score_spark": "reward", "fx_square_chip": "gold", "ui_dice_tray": None, "ui_topbar": "panel",
    "ui_chip_formula": "buttonSecondary", "ui_stage_card": "paper", "ui_icon_refresh": "ink", "ui_card_frame_rarity": "rarityRare",
    "ui_gem_common": "rarityCommon", "ui_gem_advanced": "rarityAdvanced", "ui_gem_rare": "rarityRare",
    "ui_section_panel": "section",
}


def contact_sheet(kit: bg.Kit, table: Image.Image, page: Image.Image, palette: dict, mocks: dict) -> Image.Image:
    """Every referenced sprite: default | glass | glass as the game tints it on its host; four whole-screen composites;
    the small marks at real size on their hosts (1x and 2x); the wordmark on the paper panel; the table and the page with
    the calm zones, the bounding rectangles and the action row's free rectangle marked; the palette; the concept pages."""
    P = dict(palette)
    specs = [s for s in json.loads(bg.CONTRACT.read_text(encoding="utf-8"))["sprites"] if s["referenced"]]
    W = 2400
    sheet = Image.new("RGB", (W, 9000), DARK_BG)
    d = ImageDraw.Draw(sheet)
    title, f, small = font(30, cjk=True), font(16), font(13, cjk=True)
    d.text((24, 14), "W20 GLASS (彩绘玻璃) CONTACT SHEET - offline emulation, not Unity captures", fill=LABEL, font=title)
    d.text((24, 54), SECONDARY, fill=NOTE, font=small)
    y = 84
    cell, per_row = 140, 5
    group_w = 3 * cell + 24
    rim = bg.lerp_hex(P["panel"], P["panelLight"], .55)
    for i, s in enumerate(specs):
        name = s["name"]
        gx, gy = 24 + (i % per_row) * (group_w + 12), y + (i // per_row) * (cell + 44)
        d.text((gx, gy), name, fill=LABEL, font=f)
        tint_key = TYPICAL_TINT.get(name)
        tint = rim if name == "ui_dice_tray" else (P.get(tint_key) if tint_key else None)
        host = P["felt"]
        for j, (im_name, which) in enumerate(((name, "default"), (name, "glass"), (name, "host"))):
            x0, y0 = gx + j * (cell + 8), gy + 22
            for yy in range(0, cell, 10):
                for xx in range(0, cell, 10):
                    shade = (64, 64, 72) if (xx // 10 + yy // 10) % 2 else (44, 44, 52)
                    d.rectangle((x0 + xx, y0 + yy, x0 + xx + 9, y0 + yy + 9), fill=shade)
            if which == "default":
                with Image.open(ROOT / s["default"]) as src:
                    im = src.convert("RGBA")
            else:
                im = kit.image(name).convert("RGBA")
            sliced = any(s["border"])
            if which == "host":
                c = bg.Canvas(cell, cell, host)
                w_, h_ = (cell - 16, cell - 16) if sliced else (cell - 16, cell - 16)
                if name in ("ui_progress_fill", "ui_progress_track"):
                    c.sprite(kit, name, 8, cell / 2 - 8, cell - 16, 16, tint=tint, mult=48 / 16)
                elif name == "ui_section_header":
                    c.sprite(kit, name, 4, cell / 2 - 29, cell - 8, 58, tint=tint)
                elif name == "ui_tray_front":
                    c.sprite(kit, name, 8, cell / 2 - 10, cell - 16, 20)
                else:
                    c.sprite(kit, name, 8, 8, w_, h_, tint=tint, preserve=not sliced)
                sheet.paste(c.image(), (x0, y0))
            else:
                th = im.copy()
                th.thumbnail((cell - 8, cell - 8), Image.Resampling.LANCZOS)
                sheet.paste(th, (x0 + (cell - th.width) // 2, y0 + (cell - th.height) // 2), th)
        d.text((gx, gy + cell + 24), "default | glass | glass" + (f" x {tint_key}" if tint_key else "") + " on the felt",
               fill=NOTE, font=small)
    y += math.ceil(len(specs) / per_row) * (cell + 44) + 20
    # Four whole screens.
    d.text((24, y), "WHOLE SCREENS (offline): play f 100 % | boss in play 100 % | pressure 100 % | play f 150 %", fill=LABEL, font=f)
    y += 26
    for i, key in enumerate(("play_f_100", "play_boss_play_100", "play_pressure_100", "play_f_150")):
        im = mocks[key].resize((576, 324), Image.Resampling.LANCZOS)
        sheet.paste(im, (24 + i * 590, y))
    y += 324 + 24
    # Small marks at real size on their hosts.
    d.text((24, y), "SMALL MARKS at their real size on their hosts (1x, then 2x nearest), as the game draws them", fill=LABEL, font=f)
    y += 26
    sec = bg.secondary_face(P)
    well = kit.centre("ui_info_inset")
    cases = (("ui_icon_lock", (20, 22), P["background"], None, "lock 20x22 on Background (unusable item)"),
             ("ui_icon_coin", (26, 26), sec, None, "coin 26 on the price button (buttonSecondary)"),
             ("ui_icon_coin", (36, 36), P["panel"], None, "coin 36 on the top bar"),
             ("ui_icon_roll", (36, 36), well, None, "roll cup 36 in the roll well"),
             ("ui_icon_dice_view", (40, 40), sec, None, "查看骰子 40 on the secondary face"),
             ("ui_tag_lock", (26, 28), P["felt"], None, "lock tag 26x28 on the slate"),
             ("ui_tag_clock", (64, 64), bg.drawn(kit.centre("ui_section_panel"), P["section"]), None, "clock tag 64 (dice style page)"),
             ("fx_score_crown", (48, 32), P["panel"], None, "crown 48x32 on the panel"),
             ("ui_icon_refresh", (34, 34), sec, bg.label_on11("FFFFFF", sec, P, P["textColor"])[0], "refresh 34 x label"),
             ("ui_icon_book", (34, 34), sec, bg.label_on11("FFFFFF", sec, P, P["textColor"])[0], "book 34 x label"),
             ("ui_icon_gear", (34, 34), sec, bg.label_on11("FFFFFF", sec, P, P["textColor"])[0], "gear 34 x label"),
             ("ui_icon_arrow", (42, 42), P["primary"], P["textColor"], "arrow 42 x label on primary"),
             ("badge_enchant_base10", (28, 28), P["paper"], None, "+10"), ("badge_enchant_mult2", (28, 28), P["paper"], None, "x2"),
             ("badge_enchant_roll1", (28, 28), P["paper"], None, "+1 roll"),
             ("badge_enchant_slotless", (28, 28), P["paper"], None, "slotless"),
             ("badge_enchant_submit1", (28, 28), P["paper"], None, "+1 submit"),
             ("ui_gem_common", (18, 18), P["paper"], P["rarityCommon"], "gem 18"),
             ("ui_gem_advanced", (18, 18), P["paper"], P["rarityAdvanced"], "gem 18"),
             ("ui_gem_rare", (18, 18), P["paper"], P["rarityRare"], "gem 18"),
             ("ui_gem_rare", (12, 12), P["paper"], P["rarityRare"], "gem 12 (720p)"))
    x, row_h = 24, 0
    for name, size, host, tint, label in cases:
        c1 = bg.Canvas(size[0] + 12, size[1] + 12, host)
        c1.sprite(kit, name, 6, 6, size[0], size[1], tint=tint, preserve=True)
        im1 = c1.image()
        im2 = im1.resize((im1.width * 2, im1.height * 2), Image.Resampling.NEAREST)
        cell_w = max(im1.width + im2.width + 16, 8 + int(d.textlength(label, font=small))) + 18
        if x + cell_w > W:
            x = 24
            y += row_h + 10
            row_h = 0
        sheet.paste(im1, (x, y))
        sheet.paste(im2, (x + im1.width + 6, y))
        d.text((x, y + im2.height + 4), label, fill=NOTE, font=small)
        row_h = max(row_h, im2.height + 26)
        x += cell_w
    y += row_h + 14
    # The wordmark on the paper panel (the main menu's title plate).
    d.text((24, y), "WORDMARK on the paper panel (ui_paper_panel), 720 x 220 units as the menu's title plate", fill=LABEL, font=f)
    y += 26
    cw = bg.Canvas(760, 260, P["felt"])
    cw.backdrop(page.crop((200, 200, 960, 460)))
    cw.sprite(kit, "ui_paper_panel", 20, 20, 720, 220)
    cw.sprite(kit, "ui_wordmark_dicecraft", 40, 30, 680, 200, preserve=True)
    sheet.paste(cw.image(), (24, y))
    # The table with its zones and rectangles, the page, the felt tile.
    tb = table.copy().resize((800, 450), Image.Resampling.LANCZOS)
    td = ImageDraw.Draw(tb)
    k = 800 / 1920
    for name, (zx, zy, zw, zh) in bg.FELT_ZONES.items():
        td.rectangle((zx * k, zy * k, (zx + zw) * k, (zy + zh) * k), outline=(80, 255, 120))
    for name, (rx, ry, rw, rh) in bg.PLAY_RECTS.items():
        td.rectangle((rx * k, ry * k, (rx + rw) * k, (ry + rh) * k), outline=(255, 90, 90))
    ax0, ay0, ax1, ay1 = bg.ACTION_FREE
    td.rectangle((ax0 * k, ay0 * k, ax1 * k, ay1 * k), outline=(255, 230, 60))
    sheet.paste(tb, (24 + 780, y))
    sheet.paste(page.resize((800, 450), Image.Resampling.LANCZOS), (24 + 780 + 812, y))
    d.text((24 + 780, y + 456), "table_scene: calm zones (green), button and panel rectangles (red), action row's free "
           "rectangle (yellow)", fill=NOTE, font=small)
    d.text((24 + 780 + 812, y + 456), "page_scene (the non-play screens)", fill=NOTE, font=small)
    y += 480
    # Palette.
    d.text((24, y), "PALETTE (theme.json, W20X keys; buttonSecondary set, W20Y)", fill=LABEL, font=f)
    y += 26
    keys = list(bg.PALETTE) + list(bg.W20X_TOKENS) + list(bg.W20Y_TOKENS)
    for i, key in enumerate(keys):
        sx, sy = 24 + (i % 10) * 236, y + (i // 10) * 64
        value = P[key]
        d.rectangle((sx, sy, sx + 52, sy + 48), fill=bg.rgb8(value), outline=(90, 90, 100))
        d.text((sx + 60, sy + 4), key, fill=LABEL, font=small)
        d.text((sx + 60, sy + 24), value, fill=NOTE, font=small)
    y += math.ceil(len(keys) / 10) * 64 + 10
    report = bg.palette_report(P)
    items = [f"{k}: {v:.2f}" for k, v in report.items()]
    colw = 790
    for i, line in enumerate(items):
        d.text((24 + (i % 3) * colw, y + (i // 3) * 19), line[:110], fill=NOTE, font=small)
    y += math.ceil(len(items) / 3) * 19 + 16
    # The concept pages.
    d.text((24, y), "CONCEPT PAGES (ArtSource/StyleKit/themes/glass/concepts, the user's checkpoint-3 choice)", fill=LABEL, font=f)
    y += 26
    for i, pg in enumerate(bg.CONCEPT_PAGES):
        with Image.open(ROOT / bg.CONCEPTS / (pg + ".png")) as src:
            im = src.convert("RGB").resize((460, 259), Image.Resampling.LANCZOS)
        sheet.paste(im, (24 + i * 472, y))
        d.text((24 + i * 472, y + 262), pg + ".png", fill=NOTE, font=small)
    y += 290
    return sheet.crop((0, 0, W, y))


def build_outputs(sprites: dict, table: Image.Image, page: Image.Image) -> tuple[Image.Image, Image.Image]:
    """For build_glass.build(): the preview's source (play f at 100 %) and the contact sheet, drawn from the sprites the build
    just rendered (theme.json's palette from the generator, buttonSecondary included)."""
    kit = bg.Kit(sprites=sprites)
    palette = bg.full_palette()
    mocks = {}
    for key, state, large in (("play_f_100", "f", False), ("play_boss_play_100", "boss_play", False),
                              ("play_pressure_100", "pressure", False), ("play_f_150", "f", True)):
        mocks[key], _ = bg.play_mock(kit, table, state, large, [], palette)
    if kit.fallback:
        raise ValueError("A referenced sprite fell back to the default: " + ", ".join(sorted(kit.fallback)))
    sheet = contact_sheet(kit, table, page, palette, mocks)
    if kit.fallback:
        raise ValueError("A referenced sprite fell back to the default: " + ", ".join(sorted(kit.fallback)))
    return mocks["play_f_100"], sheet


# ---------------------------------------------------------------------------------------------- card template review
CARDS = (("common", "排头兵", "第一颗提交的骰子点数×4，加到基础分。", "C108"),
         ("advanced", "蓝色浪潮", "本手提交时，提交的骰子全部按蓝色计算。", "I10"),
         ("rare", "点金手", "每次提交后，最后一颗骰子朝上的面永久镀金。", "C117"),
         (None, "配重", "这一面权重+1（上限5），更容易掷出这一面。", "U10"))
SHORT_CARDS = (("common", "增加提交", "提交栏+1格。", "U10"), ("advanced", "大量提交", "提交栏+2格。", "C108"),
               ("rare", "再给一次", "每手投掷次数+1。", "I10"), ("common", "多交一手", "每关提交次数+1。", "C117"))


def default_palette() -> dict:
    """The default style's palette (tools/art_v11/table_text.json, written from UiKit's defaults)."""
    data = json.loads((ROOT / "tools/art_v11/table_text.json").read_text(encoding="utf-8"))
    p = dict(data["palette"])
    p.setdefault("onFelt", p["textColor"])
    p.setdefault("caption", p["panel"])
    return p


def card_template_review() -> tuple[Image.Image, list[str]]:
    """W13 + W14 card template, glass (left) beside the default style (right): shop 220 x 348, table 216 x 244, compact
    280 x 117 x common / advanced / rare / goods; the four shortest short texts on compact cards; the goods rows
    (452 x 103, 448 x 121); at 1080p and 720p; the gems at their sizes."""
    kit, table, page, palette = load()
    dkit, dpal = bg.Kit(), default_palette()
    themes = (("GLASS", kit, palette), ("DEFAULT", dkit, dpal))
    panel_w, gap = 980, 40
    sheet = Image.new("RGB", (2 * panel_w + gap + 48, 7000), DARK_BG)
    d = ImageDraw.Draw(sheet)
    f, small, title, tiny = font(19), font(15), font(30), font(13)
    d.text((24, 14), "CARD TEMPLATE (W13 + W14 E1-E3): GLASS (left) | DEFAULT (right)", fill=LABEL, font=title)
    d.text((24, 54), "Offline emulation of GameApp.Card11 DrawCard13: sprites sampled bilinear without mipmaps, tinted and "
                     "blended in linear light; not a Unity capture. Glass price buttons: " + SECONDARY, fill=NOTE, font=small)
    d.text((24, 74), "Names, short texts, pictures (glass StyleKit anchor crops for both styles) and prices are stand-ins. "
                     "Goods (no rarity): Ghost ring, no gem.", fill=NOTE, font=small)
    d.text((24, 94), "Compact short texts: shown only when the whole text fits at the body size; the four shortest "
                     "(C21, C22, C78, C79) are the W20Xb comparison (offline estimate of TMP's layout).", fill=NOTE, font=small)
    y = 124
    summary = []
    for scale, res in ((1.0, "1080p"), (2 / 3, "720p")):
        sections = (("SHOP 220 x 348 (E1: 60-unit price button)", "shop", 220, 348, CARDS),
                    ("TABLE 216 x 244", "table", 216, 244, CARDS),
                    ("COMPACT 280 x 117", "compact", 280, 117, CARDS),
                    ("COMPACT 280 x 117: C21 C22 C78 C79", "compact", 280, 117, SHORT_CARDS),
                    ("GOODS ROWS 452 x 103 (price 180), 448 x 121 (price 150)", "goods", 0, 0, None))
        for label, size, cw, chh, cards in sections:
            if size == "goods":
                items = [("道具", "蓝色浪潮", "下一次有效提交全部骰子临时视为蓝色。", "I10", 452, 180, 103),
                         ("骰面改造", "配重", "这一面权重+1（上限5），更容易掷出这一面。", "U10", 448, 150, 121)]
                ph = 8 + sum(round(rh * scale) + 30 for *_r, rh in items)
                per_row = 1
            else:
                items = cards
                sw, sh = round(cw * scale), round(chh * scale)
                per_row = max(1, min(4, (panel_w - 8) // (sw + 16)))
                ph = 8 + math.ceil(len(items) / per_row) * (sh + 30)
            for p_i, (theme, tkit, pal) in enumerate(themes):
                px = 24 + p_i * (panel_w + gap)
                d.text((px, y), f"{theme}   {label}   {res}", fill=LABEL, font=f)
                section = np.asarray(tkit.image("ui_section_panel"))
                opaque = section[section.shape[0] // 2, section.shape[1] // 2, 3] >= 250
                host = pal["felt"] if size in ("table", "compact") else \
                    bg.drawn(tkit.centre("ui_section_panel"), pal.get("section", pal["panel"])) if opaque else pal["panel"]
                canvas = bg.Canvas(panel_w, ph, host, scale)
                labels = []
                oy = 8
                for i, item in enumerate(items):
                    if size == "goods":
                        cat, name, text, art, iw, pw, ih = item
                        sx, sy = 8, oy
                        oy += round(ih * scale) + 30
                        bg.compact_card(canvas, tkit, pal, sx / scale, sy / scale, iw, ih, None, name, text, art, price=2,
                                        price_w=pw, category=cat)
                        labels.append((sx, sy + round(ih * scale) + 5, f"goods row {iw} x {ih}"))
                        continue
                    rarity, name, text, art = item
                    sx, sy = 8 + (i % per_row) * (sw + 16), 8 + (i // per_row) * (sh + 30)
                    notes: list = []
                    if size == "shop":
                        bg.tall_card(canvas, tkit, pal, sx / scale, sy / scale, cw, chh, rarity, name, text, art, shop=True,
                                     price=8, value="×1.5" if i == 1 else None,
                                     category="骰面改造" if rarity is None else None)
                    elif size == "table":
                        bg.tall_card(canvas, tkit, pal, sx / scale, sy / scale, cw, chh, rarity, name, text, art, ordinal=i + 1,
                                     value="×1.5" if i == 1 else None, category="骰面改造" if rarity is None else None)
                    else:
                        shown = bg.compact_card(canvas, tkit, pal, sx / scale, sy / scale, cw, chh, rarity, name, text, art,
                                                ordinal=i + 1, notes=notes, category="骰面改造" if rarity is None else None)
                        if cards is SHORT_CARDS:
                            summary.append(f"{theme} {res} {name} {text}: {'shown' if shown else 'hidden'}")
                    note = f"{rarity or 'goods'} {pal[bg.RARITY_KEY[rarity]]}"
                    if size == "compact":
                        note += "  text " + ("shown" if "shown" in (notes[-1] if notes else "") else "hidden")
                    labels.append((sx, sy + sh + 5, note))
                sheet.paste(canvas.image(), (px, y + 26))
                light = bg.luminance(host) < .3
                for sx, sy, text_ in labels:
                    d.text((px + sx, y + 26 + sy), text_, fill=LABEL if light else (70, 64, 58), font=tiny)
            y += 26 + ph + 18
    sizes = (12, 18, 22, 30, 34)
    for p_i, (theme, tkit, pal) in enumerate(themes):
        px = 24 + p_i * (panel_w + gap)
        d.text((px, y), f"{theme}   GEMS at {' / '.join(map(str, sizes))} units on paper; right: 3x nearest", fill=LABEL, font=f)
        strip = bg.Canvas(len(sizes) * 44 + 16, 3 * 48 + 8, pal["paper"])
        for k, rarity in enumerate(("common", "advanced", "rare")):
            for j, s_ in enumerate(sizes):
                strip.sprite(tkit, "ui_gem_" + rarity, 8 + j * 44 + 22 - s_ / 2, 8 + k * 48 + 20 - s_ / 2, s_, s_,
                             tint=pal[bg.RARITY_KEY[rarity]])
        im = strip.image()
        sheet.paste(im, (px, y + 26))
        big = im.resize((im.width * 3, im.height * 3), Image.Resampling.NEAREST)
        sheet.paste(big.crop((0, 0, min(big.width, panel_w - im.width - 16), big.height)), (px + im.width + 16, y + 26))
    y += 26 + 3 * (3 * 48 + 8) + 20
    cjk = font(15, cjk=True)
    for i, line in enumerate(summary):
        d.text((24, y + i * 22), line, fill=LABEL, font=cjk)
    y += len(summary) * 22 + 16
    return sheet.crop((0, 0, sheet.width, y)), summary


def write_card_template_review(output_root: Path = ROOT) -> Path:
    im, summary = card_template_review()
    path = Path(output_root) / bg.REVIEW / "card_template_review.png"
    save(im, path)
    for line in summary:
        print("GLASS_COMPACT_TEXT", line)
    print("GLASS_CARD_REVIEW", path.relative_to(ROOT).as_posix())
    return path


# ---------------------------------------------------------------------------------------------- main
def main(argv: list[str], out: Path = OUT):
    kit, table, page, palette = load()
    out.mkdir(parents=True, exist_ok=True)
    report = []
    plays = {}
    for state in PLAY_STATES:
        for large in (False, True):
            notes: list[str] = []
            im, meas = bg.play_mock(kit, table, state, large, notes, palette)
            tag = f"play_{state}_{150 if large else 100}"
            plays[tag] = im
            lines = [f"OFFLINE MOCK (Python emulation of GameApp.Play11, not a Unity capture): state {STATE_NAMES[state]}, "
                     f"{150 if large else 100} % text, over Table/table_scene.png"
                     + (" dimmed as the felt shader does on a boss stage" if state in bg.BOSS_STATES else ""),
                     SECONDARY, hint_room(meas, kit), fallback_note(kit)]
            save(caption_bar(im, lines), out / (tag + ".png"))
            report.append(f"[{tag}] " + hint_room(meas, kit))
            report.extend(f"[{tag}] {n}" for n in dict.fromkeys(notes))
    # All states on one sheet.
    f = font(16)
    sheet = Image.new("RGB", (2 * 640 + 12, len(PLAY_STATES) * (360 + 26)), DARK_BG)
    sd = ImageDraw.Draw(sheet)
    for i, state in enumerate(PLAY_STATES):
        for j, large in enumerate((False, True)):
            im = plays[f"play_{state}_{150 if large else 100}"].resize((640, 360), Image.Resampling.LANCZOS)
            sheet.paste(im, (j * 652, i * 386 + 26))
            sd.text((j * 652 + 6, i * 386 + 4), f"{STATE_NAMES[state]}, {150 if large else 100} %", fill=LABEL, font=f)
    save(caption_bar(sheet, ["OFFLINE MOCKS: every play state at 100 % (left) and 150 % (right) text", SECONDARY]),
         out / "play_states.png")
    rows = card_rows(kit, table, palette)
    save(caption_bar(rows, ["OFFLINE MOCK: the card row with 0, 3, 5, 7 and 10 cards (GameApp.Play11 DrawCards11), y 76-352 of "
                            "the play screen; under the card row the table is the mosaic texture only (no large decoration)",
                            SECONDARY]), out / "card_rows.png")
    boss, boss_lines = boss_stage(kit, table, palette)
    save(caption_bar(boss, ["OFFLINE MOCK: the boss stage in play (final tier), 100 % | 150 % text; the table dimmed as the felt "
                            "shader does (x .74 and the vignette, approximated)", SECONDARY] + boss_lines), out / "boss_stage.png")
    report.extend("[boss_stage] " + line for line in boss_lines)
    states, state_lines = secondary_states(kit, table, palette)
    save(caption_bar(states, ["OFFLINE MOCK: SolidButton11 enabled and disabled (a quarter toward panelLight; the disabled label "
                              "by LabelOn11(face, Muted), W20Y2, which may lighten the face)", SECONDARY] + state_lines),
         out / "secondary_states.png")
    report.extend("[secondary_states] " + line for line in state_lines)
    action = action_row_table()
    (out / "action_row.txt").write_text("\n".join(action) + "\n", encoding="utf-8", newline="\n")
    notes = []
    shop = shop_mock(kit, page, palette, notes)
    save(caption_bar(shop, ["OFFLINE MOCK (Python emulation of GameApp.Shop14 W14, 100 % text, not a Unity capture) over "
                            "Table/page_scene.png, with the W20X tints: section, caption, buttonStepPrimary", SECONDARY,
                            fallback_note(kit)]), out / "shop_100.png")
    report.extend(f"[shop_100] {n}" for n in dict.fromkeys(notes))
    notes = []
    select = select_mock(kit, page, palette, notes)
    save(caption_bar(select, ["OFFLINE MOCK (Python emulation of GameApp.V10 StagePreview10, 100 % text, not a Unity capture) "
                              "over Table/page_scene.png; the chapter, budget and order texts are drawn on the table as the "
                              "code does (W16 moves them onto panels); 开始挑战 keeps ActionButton's Ink label (W16)", SECONDARY,
                              fallback_note(kit)]), out / "select_100.png")
    report.extend(f"[select_100] {n}" for n in dict.fromkeys(notes))
    save(beside_concept("roll", [("MOCK play state a, 100 % text", plays["play_a_100"]),
                                 ("MOCK play state f, 100 % text", plays["play_f_100"])]), out / "beside_roll.png")
    save(beside_concept("shop", [("MOCK W14 shop, 100 % text", shop)]), out / "beside_shop.png")
    save(beside_concept("select", [("MOCK stage select, 100 % text", select)]), out / "beside_select.png")
    save(beside_concept("triptych", [("MOCK boss in play, 100 % text", plays["play_boss_play_100"]),
                                     ("MOCK pressure stage, 150 % text", plays["play_pressure_150"])]), out / "beside_triptych.png")
    save(caption_bar(text_room_frames(kit, table, page, palette),
                     ["FOR A DECISION: panel frames within the default's FrameInsets11 (shipped) or phase 1b's jewel-strip "
                      "frames (over the default; the coordinator's rule asks before going over)", SECONDARY]),
         out / "text_room_frames.png")
    (out / "notes.txt").write_text("\n".join(report + [""] + action) + "\n", encoding="utf-8", newline="\n")
    for line in report + action:
        print("GLASS_REVIEW", line)
    print("GLASS_REVIEW_OUT", out.relative_to(ROOT).as_posix() if out.is_relative_to(ROOT) else out,
          sorted(p.name for p in out.iterdir()))


if __name__ == "__main__":
    main(sys.argv[1:])
