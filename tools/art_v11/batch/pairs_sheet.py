"""Batch review sheet: default | style pairs for every id that has a latest valid revision in the style.

usage: pairs_sheet.py <repo root> <style> <out.png> [--from ID] [--max N]
Each pair: default illustration on default paper, the style's latest revision on the style's backdrop, id label and
revision under it; ids with an invalid latest run are marked in red.
"""
import json
import sys
from pathlib import Path
from PIL import Image, ImageDraw, ImageFont

root, style, out = Path(sys.argv[1]), sys.argv[2], Path(sys.argv[3])
args = sys.argv[4:]
start = args[args.index("--from") + 1].upper() if "--from" in args else None
limit = int(args[args.index("--max") + 1]) if "--max" in args else 60
BG = {"swiss": (0xF9, 0xEA, 0xDB), "sancai": (0xF1, 0xE2, 0xC6), "comic": (0xF9, 0xF1, 0xD9), "deco": (0x15, 0x11, 0x0D), "glass": (0x2B, 0x2A, 0x33)}
F = ImageFont.truetype(r"C:\Windows\Fonts\msyh.ttc", 16)
table = json.loads((root / "ArtSource/Art11/subjects.json").read_text(encoding="utf-8"))["subjects"]
ids = sorted(table)
if start:
    ids = ids[ids.index(start):]
items = []
for cid in ids:
    revs = sorted((root / "ArtSource/Art11" / style / "illustrations" / cid).glob("r*/record.json"))
    if not revs:
        continue
    rec = json.loads(revs[-1].read_text(encoding="utf-8"))
    items.append((cid, revs[-1].parent, rec.get("runValid")))
    if len(items) >= limit:
        break
C, PER = 150, 6
rows = (len(items) + PER - 1) // PER
sheet = Image.new("RGB", (PER * (2 * C + 12), max(1, rows) * (C + 24)), (40, 40, 40))
d = ImageDraw.Draw(sheet)
for i, (cid, rev, valid) in enumerate(items):
    x, y = (i % PER) * (2 * C + 12), (i // PER) * (C + 24)
    left = Image.new("RGB", (C, C), (0xF4, 0xE8, 0xCB))
    dp = root / table[cid]["picture"]
    im = Image.open(dp).convert("RGBA"); im.thumbnail((C - 8, C - 8), Image.LANCZOS)
    left.paste(im, ((C - im.width) // 2, (C - im.height) // 2), im)
    right = Image.new("RGB", (C, C), BG[style])
    if (rev / "original.png").exists():
        im = Image.open(rev / "original.png").convert("RGBA"); im.thumbnail((C - 8, C - 8), Image.LANCZOS)
        right.paste(im, ((C - im.width) // 2, (C - im.height) // 2), im)
    sheet.paste(left, (x, y)); sheet.paste(right, (x + C, y))
    d.text((x + 4, y + C + 2), f"{cid} {rev.name}" + ("" if valid else " INVALID"), font=F,
           fill=(230, 230, 230) if valid else (255, 90, 90))
sheet.save(out)
print(out, sheet.size, len(items), "items, last", items[-1][0] if items else None)
