"""Incremental v0.5 vector-native assets. Does not regenerate legacy art/audio.

Run --samples first, inspect samples.png, then run without arguments.
All geometry is shared by editable SVG and deterministic 4x Pillow raster output.
"""
from pathlib import Path
import argparse
import hashlib
import json
import math
import re
import struct
import wave
from PIL import Image, ImageDraw, ImageFont
from build_symbols import Symbol, ROOT, OUT, SOURCE, AUDIO, S, INK, MINT, RED, BLUE, GOLD, DARK, FONT

EVIDENCE = ROOT / 'evidence/v05/art'
PREVIEWS = ROOT / 'art/previews/v05'
VIOLET = '#ccadee'
MUTED = '#5f7987'


def icon(cid):
    c = Symbol()
    if cid == 'C06':
        # The new condition is three distinct values, not the former four.
        for x, y, n, col in [(37, 92, '1', RED), (98, 44, '2', BLUE), (159, 92, '3', MINT)]:
            c.rect(x, y, 60, 78, 13, outline=col, width=6)
            c.text(x+30, y+37, n, 45, col)
        c.line([(46, 205), (210, 205)], MINT, 8)
    elif cid == 'C21':
        for x in [40, 132]: c.rect(x, 109, 76, 100, 10, outline=INK, width=7)
        c.plus(96, 56, 19, MINT, 8); c.text(151, 55, '1', 48, MINT)
        c.line([(38, 224), (210, 224)], MINT, 7)
    elif cid == 'C22':
        for x in [30, 101, 172]: c.rect(x, 112, 55, 95, 8, outline=INK, width=6)
        c.plus(94, 57, 19, MINT, 8); c.text(151, 56, '2', 48, MINT)
        c.line([(30, 223), (227, 223)], MINT, 7)
    elif cid == 'C23':
        c.arc(127, 126, 83, -140, 133, MINT, 10)
        c.arrow(66, 189, 47, 165, MINT, 9)
        c.dice(88, 87, 78, 1, INK)
    elif cid == 'C24':
        c.dice(28, 77, 73, 1, RED); c.dice(156, 77, 73, 4, RED)
        c.arrow(111, 115, 144, 115, GOLD, 7, 12)
        c.arc(128, 125, 93, 35, 145, RED, 7)
    elif cid == 'C25':
        c.circle(128, 127, 78, outline=RED, width=9)
        c.dice(30, 30, 58, 1, RED); c.dice(168, 30, 58, 4, RED)
        c.dice(97, 168, 62, 5, RED)
        c.circle(128, 112, 18, fill=RED, outline=None)
        c.arrow(86, 85, 105, 99, GOLD, 6, 10); c.arrow(170, 85, 151, 99, GOLD, 6, 10)
    elif cid == 'C26':
        # Six separate face tiles are painted together; no roll or score symbol.
        for x, y in [(84, 28), (84, 77), (35, 126), (84, 126), (133, 126), (84, 175)]:
            c.rect(x, y, 43, 43, 7, outline=RED, width=5)
            c.circle(x+21, y+21, 4, fill=RED, outline=None)
        c.line([(186, 166), (223, 54)], INK, 13)
        c.polygon([(205, 42), (232, 51), (221, 88), (194, 79)], RED)
    elif cid == 'C27':
        c.dice(40, 37, 76, 2, INK); c.dice(140, 37, 76, 2, INK)
        c.plus(94, 177, 24, MINT, 10); c.text(159, 175, '5', 64, MINT)
    elif cid == 'C28':
        for x, y, pip, col in [(39, 35, 2, BLUE), (141, 35, 2, BLUE), (39, 143, 3, MINT), (141, 143, 3, MINT)]:
            c.dice(x, y, 76, pip, col)
    elif cid == 'C29':
        for i in range(4):
            x = 26 + i*55; y = 162-i*35
            c.rect(x, y, 43, 51, 7, outline=MINT, width=5)
            c.text(x+22, y+24, str(i+1), 32)
        c.line([(28, 229), (237, 229)], INK, 6)
    elif cid == 'C30':
        for x in [27, 97, 167]: c.dice(x, 81, 62, 3, INK)
        c.line([(34, 171), (223, 171)], MINT, 8)
        c.text(128, 207, '×3', 39, MINT)
    elif cid == 'C31':
        for x, y in [(42, 34), (142, 34), (42, 131), (142, 131)]: c.dice(x, y, 72, 4, INK)
        c.line([(43, 222), (215, 222)], MINT, 8)
    elif cid == 'C32':
        c.circle(128, 128, 91, outline=MINT, width=8)
        for x, y in [(44, 62), (154, 62), (99, 146)]: c.dice(x, y, 58, 1, INK)
        c.circle(128, 118, 7, fill=MINT, outline=None)
    elif cid == 'C33':
        c.dice(29, 57, 103, 6, GOLD); c.dice(165, 92, 62, 2, INK)
        c.text(128, 204, '≥2', 49, MINT)
        c.arrow(142, 65, 209, 65, MINT, 7, 12)
    elif cid in ('C34', 'C35'):
        high = cid == 'C34'
        c.text(128, 59, '4 5 6' if high else '1 2 3', 49, GOLD if high else MINT)
        for i in range(6):
            selected = (i >= 3) if high else (i < 3)
            height = 81 if selected else 40
            c.rect(28+i*35, 205-height, 22, height, 5, fill=(GOLD if high else MINT) if selected else MUTED, outline=None, width=0)
        c.line([(24, 222), (231, 222)], INK, 6)
    elif cid in ('C36', 'C37'):
        up = cid == 'C36'; col = MINT if up else VIOLET
        for x in [30, 101]:
            c.dice(x, 115, 56, 2 if up else 5, INK)
            c.arrow(x+28, 93 if up else 39, x+28, 39 if up else 93, col, 8, 13)
        c.rect(174, 115, 56, 56, 9, outline=MUTED, width=5)
        c.text(109, 214, '+1' if up else '−1', 45, col)
        c.rect(197, 195, 32, 32, 5, outline=MINT, width=3)
        c.text(213, 210, 'C', 21, MINT)
    elif cid == 'C38':
        c.dice(31, 91, 106, 1, BLUE)
        c.rect(151, 91, 72, 106, 9, outline=MUTED, width=5)
        c.line([(90, 69), (179, 30)], INK, 13)
        c.polygon([(55, 56), (83, 44), (105, 79), (72, 93)], BLUE)
        c.text(84, 224, '1', 28, BLUE)
    elif cid == 'C39':
        c.dice(31, 75, 111, 2, BLUE)
        c.plus(198, 96, 22, MINT, 9); c.text(198, 164, '2', 52, MINT)
    elif cid == 'C40':
        for i, pip in enumerate([2, 4, 6]):
            x=27+i*76; y=152-i*48
            c.dice(x, y, 53, pip, INK)
        c.arrow(43, 228, 219, 118, MINT, 8, 15)
    else:
        raise ValueError(cid)
    return c


def enchantment(eid):
    c = Symbol()
    if eid == 'slotless':
        c.rect(77, 54, 102, 128, 16, outline=MINT, width=12)
        c.minus(129, 116, 27, MINT, 13)
        c.line([(34, 98), (56, 83)], INK, 10); c.line([(200, 83), (222, 98)], INK, 10)
        c.line([(95, 218), (161, 218)], MINT, 9)
    elif eid == 'base5':
        c.plus(76, 101, 32, GOLD, 16); c.text(166, 97, '5', 115, GOLD)
        for x, y in [(77, 182), (129, 182), (181, 182), (103, 216), (155, 216)]:
            c.circle(x, y, 8, fill=GOLD, outline=None)
    elif eid == 'mult120':
        c.line([(104, 42), (151, 89)], VIOLET, 13); c.line([(151, 42), (104, 89)], VIOLET, 13)
        c.text(128, 159, '1.2', 84, VIOLET)
        c.line([(53, 218), (203, 218)], INK, 8)
    else:
        raise ValueError(eid)
    return c


def frame(eid):
    c = Symbol()
    col = {'slotless': MINT, 'base5': GOLD, 'mult120': VIOLET}[eid]
    c.rect(9, 9, 238, 238, 22, outline=col, width=6)
    if eid == 'slotless':
        for x in [83, 119, 155]: c.line([(x, 19), (x+18, 19)], col, 4)
        c.line([(26, 236), (49, 236)], col, 5)
    elif eid == 'base5':
        c.plus(26, 26, 11, col, 4); c.plus(230, 230, 11, col, 4)
    else:
        for x, y in [(26, 26), (230, 230)]:
            c.polygon([(x, y-10), (x+10, y), (x, y+10), (x-10, y)], col)
    return c


def save_symbol(symbol, name, dest, size=256):
    symbol.im.resize((size, size), Image.Resampling.LANCZOS).save(dest / (name+'.png'))
    svg = f'<svg xmlns="http://www.w3.org/2000/svg" width="{size}" height="{size}" viewBox="0 0 256 256">\n'
    svg += '\n'.join(symbol.svg) + '\n</svg>'
    (SOURCE / (name+'.svg')).write_text(svg, encoding='utf8')


def sheet(entries, destination, columns=6, cell=192):
    rows = math.ceil(len(entries) / columns)
    im = Image.new('RGB', (columns*cell, rows*(cell+34)), (17, 31, 41))
    d = ImageDraw.Draw(im); font = ImageFont.truetype(FONT, 19)
    for i, (name, path) in enumerate(entries):
        x = i % columns*cell; y = i // columns*(cell+34)
        tile = Image.open(path).convert('RGBA').resize((cell, cell), Image.Resampling.LANCZOS)
        im.paste(tile, (x, y), tile); d.text((x+cell//2, y+cell+16), name, fill='white', font=font, anchor='mm')
    im.save(destination)


def audio(name, notes, duration):
    rate = 44100; count = int(rate*duration); values = []
    for i in range(count):
        t = i/rate; u = i/(count-1)
        attack = min(1, t/.008); tail = (1-u)**2
        tone = sum(math.sin(2*math.pi*f*t)*amp for f, amp in notes)
        values.append(round(tone*attack*tail*.24*32767))
    with wave.open(str(AUDIO/(name+'.wav')), 'wb') as f:
        f.setnchannels(1); f.setsampwidth(2); f.setframerate(rate)
        f.writeframes(struct.pack('<'+'h'*len(values), *values))
    return {'id':name, 'path':f'Assets/Game/Audio/{name}.wav', 'seconds':duration, 'sampleRate':rate, 'channels':1}


def main():
    parser = argparse.ArgumentParser(); parser.add_argument('--samples', action='store_true'); args = parser.parse_args()
    EVIDENCE.mkdir(parents=True, exist_ok=True); PREVIEWS.mkdir(parents=True, exist_ok=True)
    ids = ['C21', 'C34'] if args.samples else ['C06']+[f'C{i:02}' for i in range(21, 41)]
    for cid in ids: save_symbol(icon(cid), cid, OUT/'Icons')
    entries = [(cid, OUT/'Icons'/(cid+'.png')) for cid in ids]
    for eid in ['slotless', 'base5', 'mult120']:
        save_symbol(enchantment(eid), 'Enchant_'+eid, OUT/'Textures', 128)
        save_symbol(frame(eid), 'Frame_Enchant_'+eid, OUT/'Textures')
        entries.append((eid, OUT/'Textures'/('Enchant_'+eid+'.png')))
    sheet(entries, PREVIEWS/('samples.png' if args.samples else 'NewContentContactSheet.png'))
    if args.samples:
        print('SAMPLES_READY', str(PREVIEWS/'samples.png')); return
    cfg = json.loads((ROOT/'DicePrototype/Assets/Game/Data/P0Config.json').read_text(encoding='utf-8-sig'))
    names={}
    def walk(value):
        if isinstance(value, dict):
            cid=value.get('id')
            if cid and re.fullmatch('[CIOU][0-9]{2}', cid): names[cid]=value.get('displayName',cid)
            for v in value.values(): walk(v)
        elif isinstance(value,list):
            for v in value: walk(v)
    walk(cfg)
    manifest = json.loads((OUT/'asset_map.json').read_text(encoding='utf-8-sig'))
    manifest['configVersion'] = 'p0.5.0'; manifest['assetSchemaVersion'] = 2
    all_ids=[f'C{i:02}' for i in range(1,41)]+[f'I{i:02}' for i in range(1,8)]+[f'O{i:02}' for i in range(1,5)]+[f'U{i:02}' for i in range(1,8)]
    manifest['icons']=[{'contentId':cid,'iconId':cid,'name':names[cid], 'path':f'Assets/Game/Art/Icons/{cid}.png','source':f'art/source_icons/{cid}.svg', 'category':{'C':'controller','I':'consumable','O':'service','U':'die_upgrade'}[cid[0]]} for cid in all_ids]
    manifest['enchantments']=[{'id':eid,'badgePath':f'Assets/Game/Art/Textures/Enchant_{eid}.png','badgeSource':f'art/source_icons/Enchant_{eid}.svg','badgeSize':[128,128],'framePath':f'Assets/Game/Art/Textures/Frame_Enchant_{eid}.png','frameSource':f'art/source_icons/Frame_Enchant_{eid}.svg','tint':col, 'shape':shape} for eid,col,shape in [('slotless',MINT,'hollow-slot-minus'),('base5',GOLD,'plus-five-and-five-dots'),('mult120',VIOLET,'multiply-one-point-two')]]
    sounds=[audio('enchant_base5',[(880,1),(1320,.22)],.16),audio('enchant_mult120',[(1046.5,.65),(1569.75,.45),(2093,.18)],.22)]
    manifest['audio']=[a for a in manifest['audio'] if not a['id'].startswith('enchant_')]+sounds
    (OUT/'asset_map.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    sheet([(cid,OUT/'Icons'/(cid+'.png')) for cid in all_ids], PREVIEWS/'AllContentContactSheet.png',8,128)
    # 64px/32px downsamples reveal symbols that work only at their source size.
    sheet(entries, PREVIEWS/'NewContentThumbnail64.png',6,64)
    badges=Image.new('RGB',(360,86),(17,31,41)); bd=ImageDraw.Draw(badges)
    font=ImageFont.truetype(FONT,15)
    for i,(name,path) in enumerate(entries[-3:]):
        tile=Image.open(path).convert('RGBA').resize((32,32),Image.Resampling.LANCZOS)
        badges.paste(tile,(i*120+44,8),tile)
        bd.text((i*120+60,62),name,fill='white',font=font,anchor='mm')
    badges.save(PREVIEWS/'EnchantmentThumbnail32.png')
    sheet([(eid,OUT/'Textures'/('Frame_Enchant_'+eid+'.png')) for eid in ['slotless','base5','mult120']],PREVIEWS/'EnchantmentFrames.png',3,128)
    report={'configVersion':'p0.5.0','newControllerIcons':20,'updatedExistingIcons':['C06'], 'totalContentIcons':len(all_ids),'enchantmentBadges':3,'enchantmentFrames':3,'newAudioClips':[a['id'] for a in sounds], 'source':'deterministic-original-vector-and-synthesis','rasterizer':'Pillow','sampleScale':S,'unityImportValidated':False}
    (EVIDENCE/'symbols_generation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print(json.dumps(report,ensure_ascii=False))


if __name__ == '__main__': main()
