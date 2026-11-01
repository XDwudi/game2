"""Exact standard pips, protected material masks, and reusable local FX textures."""
from pathlib import Path
from PIL import Image,ImageDraw
import json,math,random
from build_art_samples import ROOT,SOURCE,RUNTIME,MANIFEST,C,Pixel,save_asset,dilate

POSITIONS={1:[(0,0)],2:[(-1,-1),(1,1)],3:[(-1,-1),(0,0),(1,1)],4:[(-1,-1),(1,-1),(-1,1),(1,1)],5:[(-1,-1),(1,-1),(0,0),(-1,1),(1,1)],6:[(-1,-1),(1,-1),(-1,0),(1,0),(-1,1),(1,1)]}
out=RUNTIME/'Dice/Textures';out.mkdir(parents=True,exist_ok=True)
atlas=Image.new('RGBA',(1024,1024));uv=[];records=[]
for ci,color in enumerate(['red','blue']):
 for pip,points in POSITIONS.items():
  image=Image.new('RGBA',(256,256));d=ImageDraw.Draw(image)
  for x,y in points:
   cx=128+x*60;cy=128+y*60;d.ellipse((cx-17,cy-17,cx+17,cy+17),fill=C[color],outline=C['ink'],width=3)
  image=dilate(image);idx=ci*8+pip-1;col=idx%4;row=idx//4;atlas.paste(image,(col*256,row*256));image.save(out/f'dice_pips_{color}_{pip}.png')
  uv.append({'color':color,'pip':pip,'index':idx,'uvRect':[col*.25,1-(row+1)*.25,.25,.25],'sourcePixelCenters':[[128+x*60,128+y*60] for x,y in points],'circleRadius':17,'inkOutline':3})
atlas.save(out/'dice_pips_red_blue.png');(RUNTIME/'Dice/Data').mkdir(parents=True,exist_ok=True);(RUNTIME/'Dice/Data/dice_pip_uv_map.json').write_text(json.dumps({'atlasSize':[1024,1024],'cellSize':256,'origin':'Unity bottom-left','entries':uv},indent=2),encoding='utf8')

def mask(id,w,h):
 p=Pixel((w,h));rng=random.Random(7301)
 for y in range(h):
  for x in range(w):
   nx=x/w;ny=y/h;v=0
   if id=='dice_smooth_mask':v=int(160*max(0,1-abs((nx-ny)-.25)/.075))
   elif id=='dice_rough_mask':
    safe=all((nx-(.5+px*60/256))**2+(ny-(.5+py*60/256))**2>.105**2 for px in (-1,0,1) for py in (-1,0,1))
    v=rng.randrange(25,70) if safe and rng.random()<.035 else 0
   elif id=='dice_gold_edge_mask':v=255 if .055<min(nx,ny,1-nx,1-ny)<.085 else 0
   elif id=='dice_contact_shadow':v=int(90*max(0,1-(((nx-.5)/.48)**2+((ny-.5)/.37)**2))**2)
   p.im.putpixel((x,y),(255,255,255,v))
 return p

def fx(id,w,h):
 p=Pixel((w,h));cx=w/2;cy=h/2
 if id=='fx_square_chip':p.rect((w*.3,h*.3,w*.7,h*.7),'white')
 elif id=='fx_star_spark':p.poly([(cx,h*.08),(w*.59,h*.4),(w*.92,cy),(w*.59,h*.6),(cx,h*.92),(w*.41,h*.6),(w*.08,cy),(w*.41,h*.4)],'white')
 elif id=='fx_crack_card':p.line([(w*.30,0),(w*.49,h*.23),(w*.34,h*.44),(w*.60,h*.59),(w*.49,h*.78),(w*.62,h)],'white',4);p.line([(w*.36,h*.44),(w*.13,h*.40)],'white',2)
 elif id=='fx_short_flame':p.poly([(w*.08,h*.85),(w*.16,h*.56),(w*.20,h*.65),(w*.32,h*.21),(w*.37,h*.49),(w*.52,h*.08),(w*.56,h*.45),(w*.69,h*.25),(w*.72,h*.64),(w*.82,h*.49),(w*.92,h*.86)],'white')
 else:
  for y in range(h):
   for x in range(w):
    r=math.sqrt(((x-cx)/cx)**2+((y-cy)/cy)**2)
    if id=='fx_ring_soft':v=max(0,1-abs(r-.72)/.13)
    elif id=='fx_line_soft':v=max(0,1-abs(y-cy)/(h*.14))*max(0,1-abs(x-cx)/cx)
    else:v=max(0,1-r)**2*.7
    p.im.putpixel((x,y),(255,255,255,int(v*255)))
 return p

for a in MANIFEST['sharedResources']['assets']:
 id=a['assetId'];p=None
 if id in ['dice_smooth_mask','dice_rough_mask','dice_gold_edge_mask','dice_contact_shadow']:p=mask(id,*a['dimensionsPx'])
 elif a.get('assetType')=='texture' and id.startswith('fx_'):p=fx(id,*a['dimensionsPx'])
 if p:records.append(save_asset(id,p,a['sourcePath'],a['runtimePath'],tuple(a['dimensionsPx'])))
(SOURCE/'dice_texture_manifest.json').write_text(json.dumps({'artVersion':'art0.7.0','status':'produced_pending_engine_review','pips':uv,'assets':records},indent=2),encoding='utf8')
print(json.dumps({'pipCount':12,'generatedShared':len(records)}))
