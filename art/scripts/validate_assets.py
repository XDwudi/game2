"""Validate generated artifact bytes, including connected-component pip counts."""
from pathlib import Path
from PIL import Image,ImageDraw,ImageFont
import json,hashlib,wave,struct

root=Path(__file__).resolve().parents[2]
art=root/'DicePrototype/Assets/Game/Art'
# Preserve the old checker for v0.4, but route the public entry point to the
# current spec without overwriting historical evidence/art reports.
if json.loads((art/'asset_map.json').read_text(encoding='utf-8-sig')).get('configVersion')=='p0.5.0':
    from validate_v05_assets import validate
    validate()
    raise SystemExit(0)
report={'png':[],'pipComponents':[],'audio':[],'hashes':[]}
for p in sorted((art/'Icons').glob('*.png')):
    im=Image.open(p)
    assert im.size==(256,256) and im.mode=='RGBA'
    assert im.getpixel((0,0))[3]==0
    report['png'].append({'id':p.stem,'size':list(im.size),'alphaCorner':0})
assert len(report['png'])==38
for p in sorted((art/'Textures').glob('Pip_*.png')):
    im=Image.open(p);pixels=im.getchannel('A').load();seen=set();count=0
    for y in range(im.height):
        for x in range(im.width):
            if pixels[x,y]>=128 and (x,y) not in seen:
                count+=1;stack=[(x,y)];seen.add((x,y))
                while stack:
                    a,b=stack.pop()
                    for dx,dy in [(1,0),(-1,0),(0,1),(0,-1)]:
                        c,d=a+dx,b+dy
                        if 0<=c<im.width and 0<=d<im.height and pixels[c,d]>=128 and (c,d) not in seen:
                            seen.add((c,d));stack.append((c,d))
    assert count==int(p.stem.split('_')[-1]),(p,count)
    report['pipComponents'].append({'file':p.name,'opaqueConnectedComponents':count})
assert len(report['pipComponents'])==12
for p in sorted((root/'DicePrototype/Assets/Game/Audio').glob('*.wav')):
    with wave.open(str(p),'rb') as w:
        assert w.getnchannels()==1 and w.getsampwidth()==2
        n=w.getnframes();rate=w.getframerate()
        values=struct.unpack('<'+'h'*n,w.readframes(n));peak=max(map(abs,values))
        assert rate==44100 and 0<peak<32767
        report['audio'].append({'id':p.stem,'frames':n,'sampleRate':rate,'seconds':n/rate,'peakPcm16':peak,'noClipping':True})
assert len(report['audio'])==10
for p in sorted(list((root/'art/blender').glob('*.blend'))+list((art/'Models').glob('*'))):
    if p.is_file() and p.suffix!='.meta':
        report['hashes'].append({'path':str(p.relative_to(root)).replace('\\','/'),'sha256':hashlib.sha256(p.read_bytes()).hexdigest()})
(root/'evidence/art/asset_validation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
views=[root/f'art/previews/Face_F{i+1:02}.png' for i in range(6)]
if all(p.exists() for p in views):
    sheet=Image.new('RGB',(1152,820),(17,31,41));d=ImageDraw.Draw(sheet)
    font=ImageFont.truetype('C:/Windows/Fonts/segoeuib.ttf',24)
    for i,p in enumerate(views):
        im=Image.open(p).convert('RGB');x=(i%3)*384;y=(i//3)*410
        sheet.paste(im,(x,y));d.text((x+192,y+395),f'F{i+1:02} / {i+1} / '+('RED' if i+1 in (1,4) else 'BLUE'),font=font,fill='white',anchor='mm')
    sheet.save(root/'art/previews/DiceSixViews.png')
print('ASSET_VALIDATION_PASS',len(report['png']),len(report['pipComponents']),len(report['audio']))
