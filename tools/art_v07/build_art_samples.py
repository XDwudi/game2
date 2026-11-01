"""Original art0.7.0 pixel masters; deterministic layers, not gameplay data.

Run with bundled Python/Pillow. ORA intermediates preserve named layers and are
converted by the installed Krita CLI to genuine .kra documents. No external art.
"""
from pathlib import Path
from PIL import Image, ImageDraw, ImageFilter
import json, random, zipfile, io, xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / 'ArtSource/Art07'
RUNTIME = ROOT / 'DicePrototype/Assets/Art/Art07'
MANIFEST = json.loads((ROOT/'docs/specs/v07/dice_art_v0_7_manifest.json').read_text(encoding='utf8'))
C = MANIFEST['theme']['colors'] | {'paperShade':'#C4B491','blueShade':'#28718D','blueLight':'#A3DCE5','goldShade':'#AA7737','redShade':'#A94743','violetShade':'#6B5D9C','white':'#FFFFFF'}

class Pixel:
    def __init__(self, size=(64,64)):
        self.size=size; self.layers=[]; self.new('silhouette')
    def new(self, name):
        self.im=Image.new('RGBA',self.size); self.d=ImageDraw.Draw(self.im); self.layers.append((name,self.im))
    def poly(self, pts, col, outline=None, width=1):
        self.d.polygon(pts,fill=C.get(col,col));
        if outline:self.d.line(pts+[pts[0]],fill=C.get(outline,outline),width=width,joint='curve')
    def rect(self, box, col): self.d.rectangle(box,fill=C.get(col,col))
    def line(self,pts,col,w=1):self.d.line(pts,fill=C.get(col,col),width=w)
    def circle(self,box,col,outline=None,w=1):self.d.ellipse(box,fill=C.get(col,col),outline=C.get(outline,outline) if outline else None,width=w)
    def card(self,x,y,w,h,fill='paper',edge='ink'):
        self.poly([(x+2,y),(x+w-3,y),(x+w-1,y+2),(x+w-1,y+h-3),(x+w-3,y+h-1),(x+2,y+h-1),(x,y+h-3),(x,y+2)],fill,edge,2)
    def cross(self,x,y,size=7,col='paper',diag=False):
        if diag:self.line([(x-size,y-size),(x+size,y+size)],col,3);self.line([(x+size,y-size),(x-size,y+size)],col,3)
        else:self.rect((x-2,y-size,x+2,y+size),col);self.rect((x-size,y-2,x+size,y+2),col)
    def result(self):
        im=Image.new('RGBA',self.size)
        for _,layer in self.layers:im.alpha_composite(layer)
        return im

def c02():
    p=Pixel();p.poly([(13,51),(6,41),(11,25),(18,13),(31,8),(43,14),(55,32),(56,46),(43,54)],'ink')
    p.new('fan rays')
    for pts,c in [([(31,46),(11,39),(7,28)],'blueShade'), ([(31,46),(14,16),(23,20)],'blue'), ([(31,46),(27,9),(36,9)],'blueLight'), ([(31,46),(45,14),(51,23)],'blue'), ([(31,46),(57,33),(52,43)],'blueShade')]:p.poly(pts,c)
    p.new('five blue points')
    for x,y in [(13,35),(21,23),(32,18),(43,24),(50,36)]:
        p.circle((x-4,y-4,x+4,y+4),'blue','ink',2);p.rect((x-2,y-2,x,y-1),'blueLight')
    p.new('multiplication focus');p.poly([(27,33),(36,33),(42,39),(42,48),(36,54),(27,54),(21,48),(21,39)],'blueShade','ink',2);p.cross(31,43,6,'paper',True)
    return p

def c41():
    p=Pixel();p.card(12,12,30,39,'shadow');p.card(23,24,30,32,'shadow')
    p.new('rear tracing sheet');p.card(9,9,30,39,'violetShade');p.card(11,11,26,35,'paper');p.line([(15,16),(31,16)],'paperShade',2);p.line([(15,20),(27,20)],'paperShade',2)
    p.new('front tracing sheet');p.card(21,20,30,34,'violet');p.card(23,22,26,30,'paper');p.rect((26,26,45,43),'violetShade');p.poly([(27,35),(34,29),(43,33),(43,39),(36,45),(28,40)],'violet','ink',1);p.line([(34,30),(34,37),(42,39)],'paper',1)
    p.new('source offset and direction');p.line([(16,26),(16,41),(19,41)],'violet',2);p.line([(33,15),(48,15)],'ink',6);p.line([(33,15),(48,15)],'violet',2);p.poly([(46,9),(55,15),(46,21)],'violet','ink',2);p.line([(26,48),(43,48)],'paperShade',1)
    return p

def c45():
    p=Pixel();p.circle((9,11,56,57),'shadow');p.circle((7,7,54,54),'ink');p.circle((10,10,51,51),'paperShade');p.circle((12,12,49,49),'paper')
    p.new('split dial');p.d.pieslice((14,14,47,47),90,270,fill=C['red']);p.d.pieslice((14,14,47,47),270,450,fill=C['blue']);p.line([(30,14),(30,46)],'ink',2)
    for x,y in [(30,11),(30,49),(11,30),(49,30)]:p.rect((x-1,y-1,x+1,y+1),'ink')
    p.new('color symbols');p.circle((16,25,24,33),'redShade','ink');p.circle((18,26,23,31),'paper');p.poly([(41,24),(47,30),(41,36),(35,30)],'blueLight','ink',2)
    p.new('fixed pointer');p.poly([(28,34),(33,35),(36,17),(30,22)],'ink');p.poly([(30,31),(32,31),(33,23)],'paper');p.circle((26,28,34,36),'ink');p.circle((29,30,32,33),'gold');p.line([(16,17),(20,14)],'text',2)
    return p

def c52():
    p=Pixel();p.card(12,9,36,47,'shadow');p.new('fund certificate');p.card(9,7,36,47,'paperShade');p.card(9,7,34,44,'paper');p.line([(14,13),(36,13)],'ink',2);p.line([(14,17),(28,17)],'paperShade',2);p.line([(14,22),(37,22)],'paperShade',1);p.line([(14,26),(24,26)],'paperShade',1)
    p.new('growth bars');
    for x,y in [(15,38),(23,33),(31,28)]:p.rect((x,y,x+5,43),'goldShade');p.rect((x,y,x+4,y+2),'gold');p.line([(x,44),(x+5,44)],'ink',1)
    p.new('coin value ticks');
    for x,ys in [(39,[47]),(46,[47,42]),(53,[47,42,37])]:
        for y in ys:p.circle((x-5,y-2,x+4,y+4),'ink');p.rect((x-4,y-1,x+3,y+2),'goldShade');p.circle((x-5,y-4,x+4,y+1),'gold','ink',1);p.line([(x-2,y-2),(x+1,y-2)],'paper',1)
    p.new('certificate corner');p.poly([(35,7),(42,7),(42,14)],'paperShade');p.line([(35,8),(35,14),(41,14)],'ink',1)
    return p

def ora(path,layers,size):
    path.parent.mkdir(parents=True,exist_ok=True)
    root=ET.Element('image',w=str(size[0]),h=str(size[1]),name=path.stem,version='0.0.1'); stack=ET.SubElement(root,'stack')
    merged=Image.new('RGBA',size)
    with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED) as z:
        z.writestr('mimetype','image/openraster',compress_type=zipfile.ZIP_STORED)
        for i,(name,im) in enumerate(layers):
            if im.size!=size:im=im.resize(size,Image.Resampling.NEAREST)
            merged.alpha_composite(im); b=io.BytesIO();im.save(b,format='PNG');fn=f'data/layer{i}.png';z.writestr(fn,b.getvalue())
        for i,(name,_) in reversed(list(enumerate(layers))):ET.SubElement(stack,'layer',name=name,src=f'data/layer{i}.png',x='0',y='0',opacity='1.0',visibility='visible',**{'composite-op':'svg:src-over'})
        z.writestr('stack.xml',ET.tostring(root,encoding='utf-8'));b=io.BytesIO();merged.save(b,format='PNG');z.writestr('mergedimage.png',b.getvalue())
    return merged

def dilate(im):
    # Straight alpha with four source pixels of color bleed; alpha is unchanged.
    a=im.getchannel('A');rgb=im.convert('RGB');mask=a.point(lambda q:255 if q else 0)
    for _ in range(4):
        nx=mask.filter(ImageFilter.MaxFilter(3)); grown=rgb.filter(ImageFilter.MaxFilter(3));rgb.paste(grown,(0,0),Image.eval(nx,lambda v:v));mask=nx
    # Preserve every original opaque color; only write RGB into alpha==0 fringe.
    out=Image.new('RGBA',im.size);out.paste(rgb);out.putalpha(a)
    # Bleed only fully transparent texels. Using fractional alpha as the paste
    # mask here would apply alpha twice and darken translucent glass/ghosts.
    out.paste(im,(0,0),a.point(lambda q:255 if q else 0))
    return out

def save_asset(asset_id,p,source,runtime,size):
    src=ROOT/source; dst=ROOT/'DicePrototype'/runtime
    dst.parent.mkdir(parents=True,exist_ok=True)
    merged=ora(src.with_suffix('.ora'),p.layers,size)
    dilate(merged).save(dst)
    return {'assetId':asset_id,'sourcePath':source,'oraIntermediate':str(src.with_suffix('.ora').relative_to(ROOT)).replace('\\','/'),'runtimePath':runtime,'dimensionsPx':list(size),'status':'produced_pending_engine_review','method':'original deterministic layered pixel drawing','sourceGenerator':'tools/art_v07/build_art_samples.py','liveTextBaked':False}

def templates(a):
    w,h=a['dimensionsPx'];p=Pixel((w,h));id=a['assetId'];b=16 if 'card' in id or 'button' in id or 'slot' in id or 'focus' in id else 24
    if id=='ui_focus_frame':
        p.line([(8,2),(w-9,2),(w-3,8),(w-3,h-9),(w-9,h-3),(8,h-3),(2,h-9),(2,8),(8,2)],'paper',2)
        for x,y,sx,sy in [(2,2,1,1),(w-3,2,-1,1),(2,h-3,1,-1),(w-3,h-3,-1,-1)]:
            p.line([(x+sx*10,y),(x,y),(x,y+sy*10)],'white',3)
        return p
    if id in ('ui_slot_dice','ui_slot_inventory'):
        p.line([(8,3),(w-9,3),(w-4,8),(w-4,h-13),(w-9,h-8),(8,h-8),(3,h-13),(3,8),(8,3)],'ink',4)
        p.line([(8,5),(w-9,5)],'muted',2);p.line([(8,h-6),(w-8,h-6)],'shadow',4)
        return p
    if 'progress' in id:b=8
    color='paper' if 'card' in id or 'panel' in id or 'tooltip' in id else 'white'
    if id in ('ui_slot_dice','ui_slot_inventory','ui_focus_frame'):color='panel'
    def shape(y,bottom):return [(8,y),(w-9,y),(w-1,y+8),(w-1,bottom-8),(w-9,bottom),(8,bottom),(0,bottom-8),(0,y+8)]
    p.poly(shape(6,h-1),'shadow','ink',4);p.new('body');p.poly(shape(0,h-8),color,'ink',4);p.line([(b,5),(w-b-1,5)],'text',2)
    if 'card' in id:
        p.line([(8,14),(8,h-18),(w-9,h-18),(w-9,14)],'paperShade',2)
        for x,y in [(13,13),(w-14,13),(13,h-22),(w-14,h-22)]:p.rect((x-2,y-2,x+2,y+2),'ink')
    if 'slot' in id:
        p.rect((8,8,w-9,h-14),'bgDeep');p.line([(9,h-15),(w-10,h-15)],'muted',1)
    return p

def badge(id,size):
    p=Pixel((32,32));p.card(3,5,27,26,'shadow');p.new('badge');col={'slotless':'paper','base10':'positive','mult2':'violet','submit1':'positive','roll1':'blue'}[id];p.card(2,2,27,26,col);p.line([(6,4),(23,4)],'text',1)
    p.new('symbol')
    if id=='slotless':p.line([(8,9),(8,22),(22,22),(22,9)],'ink',2);p.line([(11,8),(19,8)],'ink',2)
    elif id=='base10':p.cross(15,15,7,'ink')
    elif id=='mult2':p.cross(15,15,6,'ink',True)
    elif id=='submit1':p.card(7,8,18,15,'paper');p.line([(10,12),(21,12)],'ink',2);p.line([(10,17),(18,17)],'ink',2)
    else:p.poly([(8,9),(23,9),(20,23),(11,23)],'paper','ink',2);p.line([(10,6),(21,6)],'ink',2)
    return p

def backgrounds(a):
    w,h=a['dimensionsPx'];p=Pixel((w,h));id=a['assetId'];rng=random.Random(7001)
    if id=='bg_felt_tile':
        base=tuple(bytes.fromhex(C['felt'][1:]));p.im.paste((*base,255),(0,0,w,h));
        for y in range(h):
            for x in range(w):
                n=rng.choice([-1,0,0,1]);weave=1 if (x+y)%8==0 else -1 if (x-y)%8==0 else 0;p.im.putpixel((x,y),(*[max(0,min(255,c+n+weave)) for c in base],255))
    elif id=='bg_print_grain':
        for y in range(h):
            for x in range(w):
                n=rng.randrange(256);p.im.putpixel((x,y),(n,n,n,255))
    else:
        for y in range(h):
            for x in range(w):
                r=((x-w/2)/(w/2))**2+((y-h/2)/(h/2))**2;v=int(max(0,min(100,(r-.35)*70)));p.im.putpixel((x,y),(7,25,28,v))
    return p

def main():
    records=[]
    funcs={'C02':c02,'C41':c41,'C45':c45,'C52':c52}
    for c in MANIFEST['contentResources']:
        if c['id'] in funcs:records.append(save_asset(c['icon_id'],funcs[c['id']](),c['iconSource'],c['iconExport'],tuple(c['sourceResolution'])))
    for a in MANIFEST['sharedResources']['assets']:
        id=a['assetId'];p=None
        if id.startswith('bg_'):p=backgrounds(a)
        elif a['category']=='ui_template':p=templates(a)
        elif id.startswith('badge_enchant_'):p=badge(id.removeprefix('badge_enchant_'),128)
        if p:records.append(save_asset(id,p,a['sourcePath'],a['runtimePath'],tuple(a['dimensionsPx'])))
    (SOURCE/'samples_manifest.json').write_text(json.dumps({'artVersion':'art0.7.0','assetRevision':1,'status':'produced_pending_engine_review','assets':records},ensure_ascii=False,indent=2),encoding='utf8')
    preview=Image.new('RGB',(960,440),C['felt']);d=ImageDraw.Draw(preview)
    for i,(id,fn) in enumerate(funcs.items()):
        x=24+i*234;preview.paste(C['paper'],(x,20,x+210,280));im=fn().result().resize((192,192),Image.Resampling.NEAREST);preview.paste(im,(x+9,27),im);d.text((x+12,242),id,fill=C['ink']);small=fn().result();preview.paste(small,(x+30,320),small);small=small.resize((32,32),Image.Resampling.NEAREST);preview.paste(small,(x+138,335),small)
    (SOURCE/'Previews').mkdir(parents=True,exist_ok=True);preview.save(SOURCE/'Previews/four_controller_samples.png')
    print(json.dumps({'produced':len(records),'content':list(funcs),'manifest':str(SOURCE/'samples_manifest.json')}))

if __name__=='__main__':main()
