"""Offline review mocks for the glass (彩绘玻璃) kit: Python emulations of the game's drawing, not Unity captures.

    python tools/art_v11/glass/review_glass.py            # phase 1: play a / f at 100 % and 150 %, the W14 shop
    python tools/art_v11/glass/review_glass.py --all      # also every action-row state and the card-row coverage

Reads the shipped kit (run build_glass.py first) and writes ArtSource/Art11/glass/review/. Sprites the kit does not ship
yet are drawn with the default sprite, as UiKit.Template falls back; each image's caption lists them. Names, texts,
pictures (the glass StyleKit anchors) and dice (2D stand-ins for the 3D dice) are stand-ins.
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


def load():
    kit = bg.Kit(folder=THEME / "UI")
    with Image.open(THEME / "Table/table_scene.png") as source:
        table = source.convert("RGB")
    page_path = THEME / "Table/page_scene.png"
    with Image.open(page_path if page_path.exists() else THEME / "Table/table_scene.png") as source:
        page = source.convert("RGB")
    palette = json.loads((THEME / "theme.json").read_text(encoding="utf-8"))["palette"]
    return kit, table, page, palette


def caption_bar(im: Image.Image, lines: list[str], colour=(246, 240, 222)) -> Image.Image:
    font = ImageFont.truetype(str(bg.LATIN), 18)
    out = Image.new("RGB", (im.width, im.height + 10 + 24 * len(lines)), (20, 20, 28))
    d = ImageDraw.Draw(out)
    for i, line in enumerate(lines):
        d.text((12, 6 + 24 * i), line, fill=colour, font=font)
    out.paste(im, (0, 10 + 24 * len(lines)))
    return out


def fallback_note(kit: bg.Kit) -> str:
    names = sorted(kit.fallback)
    return ("default sprites (not drawn yet, as UiKit.Template falls back): " + ", ".join(names)) if names else \
        "every sprite is the glass kit's"


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
    bg.solid_button(c, kit, P, *hand, "手册", 20, icon="ui_icon_book")
    fill = bg.solid_button(c, kit, P, *cycle, "", 20, notes=notes, name="shop solid buttons")
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


# ---------------------------------------------------------------------------------------------- main
def hint_room(meas: dict, kit: bg.Kit) -> str:
    """The hint's inked box against the hint panel's inner frame (FrameInsets11), design units."""
    x, y, w, h = meas["hint panel"]
    l, b, r, t = kit.insets("ui_panel_hud")
    gx0, gy0, gx1, gy1 = meas["hint"]
    return (f"hint {meas['hint lines']} line(s): inked {gy0 - y:.1f}-{gy1 - y:.1f} units in the {h}-unit panel; inner frame "
            f"{t:.1f} / {h - b:.1f}: room above {gy0 - y - t:.1f}, below {h - b - (gy1 - y):.1f} (offline estimate)")


def main(argv: list[str]):
    kit, table, page, palette = load()
    OUT.mkdir(parents=True, exist_ok=True)
    report = []
    for state in ("a", "f"):
        for large in (False, True):
            notes: list[str] = []
            im, meas = bg.play_mock(kit, table, state, large, notes, palette)
            tag = f"play_{state}_{150 if large else 100}"
            lines = [f"OFFLINE MOCK (Python emulation of GameApp.Play11, not a Unity capture): state {state}, "
                     f"{150 if large else 100} % text, over Table/table_scene.png",
                     hint_room(meas, kit), fallback_note(kit)]
            caption_bar(im, lines).save(OUT / (tag + ".png"), optimize=False, compress_level=9)
            report.append(f"[{tag}] " + hint_room(meas, kit))
            report.extend(f"[{tag}] {n}" for n in dict.fromkeys(notes))
    notes = []
    shop = shop_mock(kit, page, palette, notes)
    caption_bar(shop, ["OFFLINE MOCK (Python emulation of GameApp.Shop14 W14, 100 % text, not a Unity capture) over "
                       "Table/page_scene.png, with the W20X tints: section, caption, buttonStepPrimary",
                       fallback_note(kit)]).save(OUT / "shop_100.png", optimize=False, compress_level=9)
    report.extend(f"[shop_100] {n}" for n in dict.fromkeys(notes))
    (OUT / "notes.txt").write_text("\n".join(report) + "\n", encoding="utf-8", newline="\n")
    for line in report:
        print("GLASS_REVIEW", line)
    print("GLASS_REVIEW_OUT", OUT.relative_to(ROOT).as_posix(), sorted(p.name for p in OUT.iterdir()))


if __name__ == "__main__":
    main(sys.argv[1:])
