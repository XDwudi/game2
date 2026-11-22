"""Default | latest-revision pairs for a list of ids in one style.

usage: redo_pairs.py <repo root> <style> <out.png> ID [ID ...]
Two pairs per row, 320 px per picture, picture on its card colour (default: F1E6C8, style: F9EADB for swiss).
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

root, style, out, ids = Path(sys.argv[1]), sys.argv[2], sys.argv[3], sys.argv[4:]
S = 320
BG = {"default": (241, 230, 200), "swiss": (249, 234, 219)}


def latest(cid):
    d = root / "ArtSource/Art11" / style / "illustrations" / cid
    revs = sorted((r for r in d.glob("r*") if (r / "record.json").exists()), key=lambda r: int(r.name[1:]))
    rec = json.loads((revs[-1] / "record.json").read_text(encoding="utf-8"))
    return revs[-1] / "original.png", revs[-1].name, rec.get("runValid")


def tile(path, bg):
    im = Image.open(path).convert("RGBA")
    im.thumbnail((S - 8, S - 8), Image.LANCZOS)
    t = Image.new("RGB", (S, S), bg)
    t.paste(im, ((S - im.width) // 2, (S - im.height) // 2), im)
    return t


rows = (len(ids) + 1) // 2
sheet = Image.new("RGB", (4 * S + 30, rows * (S + 24)), (40, 40, 40))
d = ImageDraw.Draw(sheet)
for k, cid in enumerate(ids):
    r, c = divmod(k, 2)
    x0, y0 = c * (2 * S + 30), r * (S + 24)
    dflt = root / "DicePrototype/Assets/Art/Art071/Illustrations" / f"illustration_{cid.lower()}.png"
    p, rev, ok = latest(cid)
    sheet.paste(tile(dflt, BG["default"]), (x0, y0 + 20))
    sheet.paste(tile(p, BG.get(style, (230, 230, 230))), (x0 + S, y0 + 20))
    d.text((x0 + 4, y0 + 4), f"{cid}   default | {style} {rev}  valid={ok}", fill=(255, 255, 255))
sheet.save(out)
print(out, sheet.size)
