"""Crop style-kit anchors from a theme's concept card page (V1.1 W19).

The four card illustrations on every concept card page (C108 排头兵, C117 点金手, I10 蓝色浪潮,
U10 配重) become the theme's anchors, as the Swiss kit's were. The crop boxes live in
anchor_boxes.json next to this script so every anchor can be reproduced byte for byte from the
pinned concept page. Existing anchor files are never overwritten: a changed box needs a new kit
version (ArtSource/StyleKit/themes/README.md, style_kit.json changeRule).

usage: python tools/art_v11/crop_anchors.py <theme> [--sheet OUT.png] [--dry-run]
"""
import argparse
import hashlib
import json
import sys
from pathlib import Path

from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parents[2]
BOXES = Path(__file__).with_name("anchor_boxes.json")


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def main() -> int:
    ap = argparse.ArgumentParser()
    ap.add_argument("theme")
    ap.add_argument("--sheet", help="write a review sheet of the crops (and the boxes on the page)")
    ap.add_argument("--dry-run", action="store_true", help="only build the sheet, write no anchors")
    args = ap.parse_args()

    spec = json.loads(BOXES.read_text(encoding="utf-8"))[args.theme]
    page_path = ROOT / spec["page"]
    if sha256(page_path) != spec["pageSha256"]:
        print(f"concept page hash differs from anchor_boxes.json: {page_path}", file=sys.stderr)
        return 2
    page = Image.open(page_path).convert("RGB")
    out_dir = ROOT / "ArtSource/StyleKit/themes" / args.theme / "anchors"
    crops = []
    for anchor_id, box in spec["boxes"].items():
        crop = page.crop(tuple(box))
        crops.append((anchor_id, box, crop))
        if args.dry_run:
            continue
        target = out_dir / f"{anchor_id}.png"
        if target.exists():
            print(f"exists, not overwritten: {target.relative_to(ROOT)}")
            continue
        out_dir.mkdir(parents=True, exist_ok=True)
        crop.save(target, optimize=True)
        print(f"{target.relative_to(ROOT)} {crop.size} sha256={sha256(target)}")

    if args.sheet:
        marked = page.copy()
        draw = ImageDraw.Draw(marked)
        for anchor_id, box, _ in crops:
            draw.rectangle(box, outline=(255, 0, 255), width=3)
            draw.text((box[0] + 6, box[1] + 4), anchor_id, fill=(255, 0, 255))
        cell = 380
        strip = Image.new("RGB", (cell * len(crops), cell), (40, 40, 40))
        for i, (_, _, crop) in enumerate(crops):
            c = crop.copy()
            c.thumbnail((cell - 10, cell - 10))
            strip.paste(c, (i * cell + (cell - c.width) // 2, (cell - c.height) // 2))
        w = max(marked.width, strip.width)
        sheet = Image.new("RGB", (w, marked.height + strip.height), (40, 40, 40))
        sheet.paste(marked, (0, 0))
        sheet.paste(strip, (0, marked.height))
        sheet.save(args.sheet)
        print("sheet", args.sheet, sheet.size)
    return 0


if __name__ == "__main__":
    sys.exit(main())
