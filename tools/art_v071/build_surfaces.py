"""Reproducible geometric print surfaces, separate from generated illustrations.

New masters are deterministic drawing instructions (editable Python), rendered at
2x then downsampled. No prices, card names, rule dice or gameplay data are baked.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import json, random, hashlib

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/Art071/UI'
OUTPUT = ROOT / 'DicePrototype/Assets/Art/Art071/UI'
PAPER = '#F4E8CB'
INK = '#162629'


def surface(name, kind):
    im = Image.new('RGBA', (1024, 1024))
    d = ImageDraw.Draw(im)
    if kind == 'card':
        shadow = Image.new('RGBA', im.size)
        ImageDraw.Draw(shadow).rounded_rectangle((22, 28, 1006, 1008), radius=42, fill=(7, 25, 28, 100))
        im.alpha_composite(shadow.filter(ImageFilter.GaussianBlur(7)))
        d = ImageDraw.Draw(im)
        d.rounded_rectangle((8, 8, 996, 990), radius=42, fill=PAPER, outline=INK, width=8)
        d.rounded_rectangle((25, 25, 979, 973), radius=32, outline='#D6BC8A', width=5)
        d.rounded_rectangle((40, 40, 964, 958), radius=26, outline=INK, width=3)
        rng = random.Random(71)
        for _ in range(18000):
            x, y = rng.randrange(46, 958), rng.randrange(46, 952)
            shade = rng.choice([(90, 65, 35, 5), (255, 255, 255, 14)])
            # Tiny, low contrast print grain, never on text as a separate overlay.
            base = im.getpixel((x, y))
            a = shade[3] / 255
            im.putpixel((x, y), tuple(round(base[i]*(1-a)+shade[i]*a) for i in range(3))+(255,))
    elif kind == 'button':
        d.rounded_rectangle((6, 6, 1017, 1017), radius=64, fill='#FFFFFF', outline=INK, width=10)
        d.rounded_rectangle((22, 22, 1001, 1001), radius=48, outline='#C3C3C3', width=5)
        d.rounded_rectangle((34, 34, 989, 989), radius=36, outline='#FFFFFF', width=3)
    else:
        d.rounded_rectangle((2, 2, 1021, 1021), radius=46, fill='#173538', outline='#486365', width=4)
        d.rounded_rectangle((10, 10, 1013, 1013), radius=40, outline='#112D30', width=5)
    im = im.resize((512, 512), Image.Resampling.LANCZOS)
    im.save(SOURCE / (name+'.png')); im.save(OUTPUT / (name+'.png'))


def main():
    SOURCE.mkdir(parents=True, exist_ok=True); OUTPUT.mkdir(parents=True, exist_ok=True)
    for name, kind in [('ui_card_controller', 'card'), ('ui_card_controller_compact', 'card'),
                       ('ui_paper_panel', 'card'), ('ui_button_bevel', 'button'), ('ui_info_inset', 'info')]:
        surface(name, kind)
    rows = [{'id':p.stem, 'source':str(p.relative_to(ROOT)).replace('\\','/'),
             'runtime':str((OUTPUT/p.name).relative_to(ROOT)).replace('\\','/'),
             'sha256':hashlib.sha256(p.read_bytes()).hexdigest(), 'border':[32,32,32,32],
             'pixelsPerUnit':200, 'filter':'Bilinear', 'editableSource':'tools/art_v071/build_surfaces.py'}
            for p in sorted(SOURCE.glob('*.png'))]
    (SOURCE/'manifest.json').write_text(json.dumps(rows,indent=2),encoding='utf-8')
    print(json.dumps({'surfaces':len(rows),'output':str(OUTPUT)}))


if __name__ == '__main__':
    main()
