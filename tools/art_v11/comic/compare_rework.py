"""Evidence for the comic kit's rework round 2: the approved concept page beside the new and the old offline composite.

    python tools/art_v11/comic/compare_rework.py --old 088c5a27

Reads the old generator, sprites and table from git (`--old`, the round 1 commit; set GIT to git's path if it is not on
PATH) and the new ones from the worktree (run build_comic.py first). Writes ArtSource/Art11/comic/rework_r2/:
- hud_column.png, button_row.png, tray_gutters.png, card_row.png: concept (scaled to 1920 wide) / new / old crops;
- mock_new.png, mock_old.png: the full composites at 1920 x 1080;
- shop_w14.png: the W14 shop's sections (GameApp.Shop14 S14 at 100 % text) drawn over the new table, to show where the
  play screen's felt shows through the shop's gutters.
Offline Python emulation, not Unity captures.
"""
from __future__ import annotations

import argparse
import importlib.util
import io
import os
import subprocess
import sys
import types
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFont

sys.dont_write_bytecode = True
HERE = Path(__file__).resolve()
ROOT = HERE.parents[3]
OUT = ROOT / "ArtSource/Art11/comic/rework_r2"
THEME = "DicePrototype/Assets/Resources/Themes/comic"
GIT = os.environ.get("GIT", "git")


def git_bytes(rev: str, path: str) -> bytes:
    return subprocess.run([GIT, "-C", str(ROOT), "show", f"{rev}:{path}"], check=True, capture_output=True).stdout


def load_new():
    spec = importlib.util.spec_from_file_location("comic_new", HERE.parent / "build_comic.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def load_old(rev: str):
    module = types.ModuleType("comic_old")
    module.__file__ = str(HERE.parent / "build_comic.py")   # its ROOT resolves to this worktree (fonts, anchors, contract)
    exec(compile(git_bytes(rev, "tools/art_v11/comic/build_comic.py").decode("utf-8"), f"{rev}:build_comic.py", "exec"),
         module.__dict__)
    return module


def kit_from(rev: str | None, names):
    sprites = {}
    for name in names:
        rel = f"{THEME}/UI/{name}.png"
        data = git_bytes(rev, rel) if rev else (ROOT / rel).read_bytes()
        sprites[name] = Image.open(io.BytesIO(data)).convert("RGBA")
    rel = f"{THEME}/Table/table_scene.png"
    table = Image.open(io.BytesIO(git_bytes(rev, rel) if rev else (ROOT / rel).read_bytes())).convert("RGB")
    return sprites, table


def labelled(rows, font, width=1920):
    """Stack (label, image) rows, each scaled to `width`, with a caption bar."""
    scaled = []
    for label, im in rows:
        k = width / im.width
        scaled.append((label, im.resize((width, max(1, round(im.height * k))), Image.Resampling.LANCZOS)))
    out = Image.new("RGB", (width, sum(im.height + 34 for _, im in scaled)), (20, 24, 44))
    d = ImageDraw.Draw(out)
    y = 0
    for label, im in scaled:
        d.text((12, y + 6), label, fill=(246, 240, 222), font=font)
        out.paste(im, (0, y + 34))
        y += im.height + 34
    return out


def side_by_side(rows, font, height=1000):
    """Place tall (label, image) crops next to each other at one height, labels above."""
    scaled = [(label, im.resize((max(1, round(im.width * height / im.height)), height), Image.Resampling.LANCZOS))
              for label, im in rows]
    out = Image.new("RGB", (sum(im.width + 24 for _, im in scaled) + 24, height + 64), (20, 24, 44))
    d = ImageDraw.Draw(out)
    x = 24
    for label, im in scaled:
        for i, part in enumerate(label.split(" (", 1)):
            d.text((x, 6 + i * 26), part if i == 0 else "(" + part, fill=(246, 240, 222), font=font)
        out.paste(im, (x, 64))
        x += im.width + 24
    return out


def shop_w14(new, sprites, specs, table):
    """GameApp.Shop14 S14 (100 % text): sections as the code tints them (panel x Panel, header strip x PanelLight)."""
    P = new.PALETTE
    m = new.Mock(sprites, specs, table)
    m.put("ui_topbar", 16, 12, 1888, 72, new.col(P["panel"]))
    cards_h = 68 + 348 + 7
    build_h = 4 + 28 + 2 + 104 + 7
    goods_h = (1072 - 92 - 16) - cards_h - build_h
    owned_h = 68 + 2 * 104 + 6 + 7
    dice_h = (1072 - 92 - 16) - owned_h - 96
    sections = [("控制器卡牌", (16, 92, 1404, cards_h)), ("升级与服务", (16, 92 + cards_h + 8, 1404, goods_h)),
                ("我的构筑", (16, 92 + cards_h + goods_h + 16, 1404, build_h)), ("骰子与骰面", (1432, 92, 472, dice_h)),
                ("当前持有", (1432, 92 + dice_h + 8, 472, owned_h))]
    lookup = {s["name"]: s for s in specs}
    fl, fb, fr, ft = (v / 2 for v in new.frame_insets_px(np.asarray(sprites["ui_section_panel"]),
                                                          lookup["ui_section_panel"]["border"]))
    for title, (x, y, w, h) in sections:
        m.put("ui_section_panel", x, y, w, h, new.col(P["panel"]))
        m.put("ui_section_header", x + fl, y + ft, w - fl - fr, 68 - ft - 2, new.col(P["panelLight"]))
        m.text(x + fl + 14, y + ft + (68 - ft - 2) / 2, title, 22, new.label_on(new.col(P["panelLight"])), "lm")
    new.mock_button(m, 1432, 92 + dice_h + owned_h + 16, 472, 96, new.col(P["primary"]), "下一关", 28,
                    new.label_on(new.col(P["primary"])))
    return m.im.convert("RGB")


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--old", default="088c5a27")
    args = parser.parse_args()
    new = load_new()
    old = load_old(args.old)
    specs = new.contract_sprites()
    names = [s["name"] for s in specs]
    new_sprites, new_table = kit_from(None, names)
    old_sprites, old_table = kit_from(args.old, names)
    mock_new = new.play_mock(new_sprites, specs, new_table)
    mock_old = old.play_mock(old_sprites, specs, old_table)
    with Image.open(ROOT / "ArtSource/StyleKit/themes/comic/concepts/roll.png") as source:
        concept = source.convert("RGB").resize((1920, round(941 * 1920 / 1672)), Image.Resampling.LANCZOS)
    OUT.mkdir(parents=True, exist_ok=True)
    font = ImageFont.truetype(str(new.LATIN), 20)
    k = 1920 / 1672
    crops = {  # name: (concept box in 1672-wide px, our box in 1920 x 1080 units)
        "hud_column": ((0, 0, 330, 941), (1480, 84, 1920, 1080)),
        "button_row": ((0, 780, 1672, 941), (0, 944, 1920, 1080)),
        "tray_gutters": ((280, 280, 1672, 800), (0, 330, 1500, 962)),
        "card_row": ((300, 0, 1672, 300), (0, 0, 1500, 352)),
    }
    for name, (cbox, ours) in crops.items():
        cb = tuple(round(v * k) for v in cbox)
        rows = [(f"CONCEPT roll.png {cbox} (scaled to 1920 wide)", concept.crop(cb)),
                (f"NEW (round 2) {ours}", mock_new.crop(ours)), (f"OLD (round 1, {args.old}) {ours}", mock_old.crop(ours))]
        if (ours[3] - ours[1]) > (ours[2] - ours[0]):
            image = side_by_side(rows, font)
        else:
            image = labelled(rows, font)
        image.save(OUT / f"{name}.png", optimize=False, compress_level=9)
    mock_new.save(OUT / "mock_new.png", optimize=False, compress_level=9)
    mock_old.save(OUT / "mock_old.png", optimize=False, compress_level=9)
    shop_w14(new, new_sprites, specs, new_table).save(OUT / "shop_w14.png", optimize=False, compress_level=9)
    print("COMIC_REWORK_EVIDENCE", OUT.relative_to(ROOT).as_posix(), sorted(p.name for p in OUT.glob("*.png")))


if __name__ == "__main__":
    main()
