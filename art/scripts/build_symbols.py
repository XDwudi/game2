"""Deterministic vector-native symbols, exact pip atlas, and short synthesized SFX.

Python 3 + Pillow. Source SVGs and generation code remain editable. Original assets;
no stock artwork, samples, trademarks or external media are used.
"""
from pathlib import Path
import math,json,re,wave,struct,random,hashlib,html
from PIL import Image,ImageDraw,ImageFont

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'DicePrototype/Assets/Game/Art'
SOURCE=ROOT/'art/source_icons'
EVIDENCE=ROOT/'evidence/art'
AUDIO=ROOT/'DicePrototype/Assets/Game/Audio'
for d in [OUT/'Icons',OUT/'Textures',SOURCE,EVIDENCE,AUDIO,ROOT/'art/previews']:d.mkdir(parents=True,exist_ok=True)
S=4
INK='#edf3e9'; MINT='#68dbbe'; RED='#eb6a77'; BLUE='#79b5f2'; GOLD='#edc676'; DARK='#172b35'
FONT='C:/Windows/Fonts/segoeuib.ttf'

class Symbol:
    def __init__(self):
        self.im=Image.new('RGBA',(256*S,256*S),(0,0,0,0));self.d=ImageDraw.Draw(self.im);self.svg=[]
    def line(self,points,fill=INK,width=9):
        self.d.line([(round(x*S),round(y*S)) for x,y in points],fill=fill,width=round(width*S),joint='curve')
        r=width*S/2
        for x,y in [points[0],points[-1]]:self.d.ellipse((x*S-r,y*S-r,x*S+r,y*S+r),fill=fill)
        self.svg.append('<polyline points="'+' '.join(f'{x},{y}' for x,y in points)+f'" fill="none" stroke="{fill}" stroke-width="{width}" stroke-linecap="round" stroke-linejoin="round"/>')
    def polygon(self,pts,fill=INK):
        self.d.polygon([(x*S,y*S) for x,y in pts],fill=fill)
        self.svg.append('<polygon points="'+' '.join(f'{x},{y}' for x,y in pts)+f'" fill="{fill}"/>')
    def circle(self,x,y,r,fill=None,outline=INK,width=8):
        self.d.ellipse(((x-r)*S,(y-r)*S,(x+r)*S,(y+r)*S),fill=fill,outline=outline,width=int(width*S))
        self.svg.append(f'<circle cx="{x}" cy="{y}" r="{r}" fill="{fill or "none"}" stroke="{outline or "none"}" stroke-width="{width}"/>')
    def rect(self,x,y,w,h,r=12,fill=None,outline=INK,width=8):
        self.d.rounded_rectangle((x*S,y*S,(x+w)*S,(y+h)*S),r*S,fill=fill,outline=outline,width=int(width*S))
        self.svg.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{r}" fill="{fill or "none"}" stroke="{outline or "none"}" stroke-width="{width}"/>')
    def text(self,x,y,t,size=48,color=INK):
        font=ImageFont.truetype(FONT,round(size*S))
        self.d.text((x*S,y*S),t,fill=color,font=font,anchor='mm')
        self.svg.append(f'<text x="{x}" y="{y}" fill="{color}" font-family="Segoe UI, sans-serif" font-weight="700" font-size="{size}" dominant-baseline="central" text-anchor="middle">{html.escape(t)}</text>')
    def arc(self,cx,cy,r,start,end,color=INK,width=8):
        pts=[(cx+r*math.cos(math.radians(a)),cy+r*math.sin(math.radians(a))) for a in [start+(end-start)*i/40 for i in range(41)]]
        self.line(pts,color,width)
    def plus(self,x,y,r=20,color=MINT,width=9):self.line([(x-r,y),(x+r,y)],color,width);self.line([(x,y-r),(x,y+r)],color,width)
    def minus(self,x,y,r=20,color=RED,width=9):self.line([(x-r,y),(x+r,y)],color,width)
    def arrow(self,x1,y1,x2,y2,color=MINT,width=9,head=16):
        self.line([(x1,y1),(x2,y2)],color,width)
        a=math.atan2(y2-y1,x2-x1)
        self.line([(x2-head*math.cos(a-.65),y2-head*math.sin(a-.65)),(x2,y2),(x2-head*math.cos(a+.65),y2-head*math.sin(a+.65))],color,width)
    def dice(self,x,y,s=72,pip=1,color=INK,body=True):
        if body:self.rect(x,y,s,s,r=s*.16,outline=color,width=6)
        for px,py in PIPS[pip]:self.circle(x+s*(.5+px*.26),y+s*(.5+py*.26),s*.075,fill=color,outline=None,width=0)
    def save(self,name,dest):
        self.im.resize((256,256),Image.Resampling.LANCZOS).save(dest/(name+'.png'))
        (SOURCE/(name+'.svg')).write_text('<svg xmlns="http://www.w3.org/2000/svg" width="256" height="256" viewBox="0 0 256 256">\n'+'\n'.join(self.svg)+'\n</svg>',encoding='utf8')

PIPS={1:[(0,0)],2:[(-1,-1),(1,1)],3:[(-1,-1),(0,0),(1,1)],4:[(-1,-1),(1,-1),(-1,1),(1,1)],5:[(-1,-1),(1,-1),(0,0),(-1,1),(1,1)],6:[(-1,-1),(1,-1),(-1,0),(1,0),(-1,1),(1,1)]}

def content_symbol(cid):
    c=Symbol()
    if cid=='C01':
        c.rect(59,86,138,114,32,outline=GOLD);c.line([(98,91),(158,91)],GOLD,7);c.circle(128,55,24,outline=GOLD);c.line([(128,44),(128,66)],GOLD,6)
    elif cid=='C02':
        c.polygon([(137,24),(75,133),(119,133),(102,225),(187,101),(141,101)],BLUE)
        c.line([(45,91),(27,91)],BLUE,6);c.line([(205,165),(226,165)],BLUE,6)
    elif cid=='C03':
        c.line([(80,210),(80,42)],INK,9);c.polygon([(83,47),(198,60),(160,97),(83,112)],RED);c.text(121,167,'1',48)
    elif cid=='C04':
        c.rect(35,42,80,95,10,outline=MINT);c.plus(75,87,20);c.dice(137,131,78,3,BLUE);c.minus(173,70,23)
    elif cid=='C05':
        c.arc(128,127,81,-135,140,MINT,10);c.arrow(69,191,49,169,MINT);c.text(130,131,'3',80)
    elif cid=='C06':
        for x,y,n,col in [(41,40,'1',RED),(142,40,'2',BLUE),(41,142,'3',MINT),(142,142,'4',GOLD)]:c.rect(x,y,72,72,16,outline=col,width=5);c.text(x+36,y+34,n,47,col)
    elif cid=='C07':
        c.dice(34,55,85,3,INK);c.dice(140,124,85,3,MINT);c.arrow(133,59,204,100,MINT)
    elif cid=='C08':
        for i in range(3):c.rect(25+i*75,91,58,76,9,outline=INK if i<2 else '#4c6977',width=6)
        c.circle(54,130,8,fill=MINT,outline=None);c.circle(129,130,8,fill=MINT,outline=None);c.line([(30,191),(151,191)],MINT,8)
    elif cid=='C09':
        c.rect(38,77,65,100,10);c.rect(117,77,65,100,10);c.minus(207,129,21);c.text(110,210,'×3',31,MINT)
    elif cid=='C10':
        c.dice(39,77,112,1);c.plus(199,104,22);c.text(198,160,'4',46,MINT)
    elif cid=='C11':
        c.text(79,91,'2',75,BLUE);c.text(176,91,'2',75,BLUE);c.text(79,178,'3',75,MINT);c.text(176,178,'3',75,MINT)
    elif cid=='C12':
        c.text(63,112,'1',69,RED);c.arrow(100,120,155,120,MINT);c.text(199,112,'2',69,BLUE);c.text(128,191,'+1',42,MINT)
    elif cid=='C13':
        c.text(83,129,'3',110);c.plus(185,104,23);c.text(186,166,'2',45,MINT)
    elif cid=='C14':
        c.text(128,104,'1 3 5',52);c.arc(128,129,91,15,165,MINT,9);c.text(128,191,'×2',37,MINT)
    elif cid=='C15':
        for x,col in [(29,RED),(102,BLUE),(175,RED)]:c.dice(x,66,53,1,col)
        c.arrow(54,160,125,160,RED);c.arrow(203,160,135,160,RED);c.circle(129,197,14,fill=RED,outline=None)
    elif cid=='C16':
        c.arc(127,125,82,-140,133,MINT,10);c.arrow(65,189,47,165,MINT);c.text(124,121,'+2',65)
    elif cid=='C17':
        c.dice(37,53,68,1,RED);c.dice(148,53,68,2,BLUE);c.dice(92,139,68,3,MINT)
    elif cid=='C18':
        c.line([(128,47),(128,211)],INK,9);c.line([(53,86),(203,86)],INK,8);c.line([(88,211),(168,211)],INK,8)
        c.line([(59,89),(37,147),(81,147),(59,89)],RED,7);c.line([(197,89),(175,147),(219,147),(197,89)],BLUE,7)
    elif cid=='C19':
        c.dice(37,81,83,2,INK);c.text(185,98,'Σ',66,MINT);c.text(186,164,'13',45,MINT)
    elif cid=='C20':
        c.rect(30,66,45,128,8,outline='#536b76',width=5);c.rect(90,66,45,128,8,outline='#536b76',width=5);c.dice(151,104,74,6,GOLD);c.arrow(150,64,213,64,GOLD,7)
    elif cid=='I01':
        for y in [80,125,170]:c.line([(30,y),(60,y-18),(95,y+12),(131,y-18),(166,y+12),(202,y-18),(226,y)],RED,10)
    elif cid=='I02':
        c.dice(36,48,125,4,MINT);c.circle(182,178,43,fill=DARK,outline=GOLD,width=7);c.line([(182,151),(182,179),(202,189)],GOLD,7)
    elif cid=='I03':
        c.rect(36,48,72,159,19,outline=MINT);c.circle(71,83,12,outline=MINT,width=6);c.circle(71,122,8,fill=INK,outline=None);c.dice(152,50,66,1,RED);c.dice(152,143,66,6,BLUE);c.line([(120,84),(138,84)],MINT,6);c.line([(120,175),(138,175)],MINT,6)
    elif cid in ('I04','I05'):
        c.dice(40,94,116,6 if cid=='I04' else 1,BLUE if cid=='I04' else RED);c.arrow(200,193,200,51,MINT,10,23)
    elif cid=='I06':
        c.rect(42,38,119,174,13);c.line([(67,78),(135,78)],MINT,7);c.line([(67,110),(122,110)],MINT,7);c.plus(191,161,27)
    elif cid=='I07':
        c.polygon([(47,69),(156,69),(174,206),(29,206)],MINT);c.line([(60,85),(52,186)],DARK,8);c.dice(171,36,48,3,INK);c.plus(203,144,23,GOLD)
    elif cid=='O01':
        c.rect(29,84,68,104,12,outline=MINT);c.rect(111,84,68,104,12,outline=MINT);c.plus(210,136,23,GOLD)
    elif cid=='O02':
        c.line([(42,85),(139,36),(215,112),(116,211),(42,135),(42,85)],GOLD,9);c.circle(78,108,10,outline=GOLD,width=6);c.minus(144,133,28,MINT,9)
    elif cid=='O03':
        c.dice(35,83,115,5,INK);c.plus(199,100,26,MINT);c.arrow(181,198,218,162,MINT,7)
    elif cid=='O04':
        c.dice(35,61,116,4,INK);c.arrow(168,130,220,130,RED,10);c.line([(216,183),(216,207),(75,207)],RED,8)
    elif cid=='U01':
        c.rect(42,42,171,171,30,outline=BLUE,width=7);c.line([(72,161),(152,81)],BLUE,12);c.line([(113,170),(174,109)],INK,6);c.plus(73,70,17,INK,5)
    elif cid=='U02':
        c.rect(42,42,171,171,30,outline=GOLD,width=7)
        for y in range(72,192,31):
            for x in range(72,192,31):c.circle(x+(7 if y%2 else 0),y,5,fill=GOLD,outline=None)
    elif cid in ('U03','U04'):
        col=RED if cid=='U03' else BLUE
        c.line([(69,182),(174,77)],INK,25);c.polygon([(147,69),(181,35),(221,75),(187,109)],col);c.polygon([(67,163),(31,213),(86,198)],col)
    elif cid=='U05':
        c.rect(46,43,165,169,27,outline=BLUE,width=8);c.line([(132,47),(105,97),(148,130),(110,173),(133,211)],INK,8);c.line([(107,97),(63,111)],BLUE,6)
    elif cid=='U06':
        c.rect(41,41,174,174,26,outline=GOLD,width=13);c.dice(73,73,110,4,BLUE,False);c.plus(208,40,21,GOLD,5)
    elif cid=='U07':
        c.dice(33,46,79,1,INK);c.dice(144,136,79,5,MINT);c.arrow(141,62,211,104,MINT);c.arrow(112,198,43,155,INK)
    else:raise ValueError(cid)
    return c

def main():
    md=(ROOT/'docs/specs/P0_v0.4.md').read_text(encoding='utf-8-sig')
    cfg=json.loads(re.findall(r'```json\s*\n(.*?)\n```',md,re.S)[0])
    # Read display names from the authoritative configuration wherever they live.
    names={}
    def walk(obj):
        if isinstance(obj,dict):
            cid=obj.get('id',obj.get('contentId'))
            if cid and re.fullmatch('[CIOU][0-9]{2}',cid):names[cid]=obj.get('name',obj.get('displayName',cid))
            for v in obj.values():walk(v)
        elif isinstance(obj,list):
            for v in obj:walk(v)
    walk(cfg)
    ids=[f'C{i:02}' for i in range(1,21)]+[f'I{i:02}' for i in range(1,8)]+[f'O{i:02}' for i in range(1,5)]+[f'U{i:02}' for i in range(1,8)]
    icons=[]
    for cid in ids:
        content_symbol(cid).save(cid,OUT/'Icons')
        icons.append({'contentId':cid,'iconId':cid,'name':names.get(cid,cid),'path':f'Assets/Game/Art/Icons/{cid}.png','source':f'art/source_icons/{cid}.svg','category':{'C':'controller','I':'consumable','O':'service','U':'die_upgrade'}[cid[0]]})
    atlas=Image.new('RGBA',(1024,1024),(0,0,0,0));tiles=[]
    for color,col,offset in [('red',RED,0),('blue',BLUE,8)]:
        for pip in range(1,7):
            symbol=Symbol();symbol.dice(0,0,256,pip,col,False)
            name=f'Pip_{color}_{pip}';symbol.save(name,OUT/'Textures')
            tile=Image.open(OUT/'Textures'/f'{name}.png')
            index=offset+pip-1;x=(index%4)*256;y=(index//4)*256
            atlas.alpha_composite(tile,(x,y))
            tiles.append({'color':color,'pip':pip,'tileIndexTopLeft':index,'pixelRectTopLeft':[x,y,256,256],'unityUvRectBottomLeft':[x/1024,1-(y+256)/1024,.25,.25],'path':f'Assets/Game/Art/Textures/{name}.png'})
    atlas.save(OUT/'Textures/PipsAtlas.png')
    # Overlay layers are independent of the rule colour and pip atlas.
    layers={}
    c=Symbol();c.rect(10,10,236,236,30,outline=GOLD,width=10);c.save('Layer_Gold',OUT/'Textures');layers['gold']='Layer_Gold'
    c=Symbol();c.line([(32,180),(175,37)],'#b9e7f0',9);c.line([(73,207),(214,66)],'#86bacb',4);c.save('Layer_Smooth',OUT/'Textures');layers['smooth']='Layer_Smooth'
    c=Symbol()
    for y in range(20,250,24):
        for x in range(20,250,24):c.circle(x,y,2.5,fill='#97acb0',outline=None)
    c.save('Layer_Rough',OUT/'Textures');layers['rough']='Layer_Rough'
    c=Symbol();c.rect(12,12,232,232,30,outline=BLUE,width=6);c.line([(25,160),(160,25)],'#a4dae8',4);c.save('Layer_Glass',OUT/'Textures');layers['glass']='Layer_Glass'
    # Common category frames, transparent centres.
    for name,color in [('Controller',MINT),('Item',GOLD),('Upgrade',BLUE)]:
        c=Symbol();c.rect(5,5,246,246,21,outline=color,width=5);c.save('Frame_'+name,OUT/'Textures')
    audio=[]
    sounds={'roll':(.42,95,220),'land':(.16,90,40),'lock':(.14,650,980),'cancel':(.11,500,320),'use':(.24,500,1200),'purchase':(.24,780,1450),'reject':(.19,240,155),'copy':(.28,420,840),'dye':(.24,300,700),'break':(.38,1800,150)}
    rate=44100
    for index,(name,(duration,f0,f1)) in enumerate(sounds.items()):
        rng=random.Random(400+index);data=[];n=int(rate*duration)
        for i in range(n):
            t=i/rate;u=i/max(1,n-1);freq=f0+(f1-f0)*u
            phase=2*math.pi*(f0*t+(f1-f0)*t*t/(2*duration))
            env=min(1,u/.025)*((1-u)**2)
            tone=math.sin(phase)+.22*math.sin(phase*2)
            noise=(rng.random()*2-1)
            value=(noise*.7+tone*.3) if name in ('roll','land','break') else tone
            if name=='roll':value*=.6+.4*math.sin(u*math.pi*13)**2
            if name=='purchase':value+=.3*math.sin(phase*1.5)
            data.append(int(max(-1,min(1,value*env*.33))*32767))
        with wave.open(str(AUDIO/f'{name}.wav'),'wb') as f:
            f.setnchannels(1);f.setsampwidth(2);f.setframerate(rate);f.writeframes(struct.pack('<'+'h'*len(data),*data))
        audio.append({'id':name,'path':f'Assets/Game/Audio/{name}.wav','seconds':duration,'sampleRate':rate,'channels':1})
    manifest={'configVersion':'p0.4.0','assetSchemaVersion':1,'license':'Original project artwork and synthesized audio; no external asset license','icons':icons,'pipAtlas':{'path':'Assets/Game/Art/Textures/PipsAtlas.png','width':1024,'height':1024,'tiles':tiles},'overlayLayers':layers,'audio':audio}
    (OUT/'asset_map.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf8')
    # Contact sheets are QA references, not substitute game screenshots.
    sheet=Image.new('RGB',(8*256,5*290),(17,31,41));draw=ImageDraw.Draw(sheet)
    font=ImageFont.truetype(FONT,22)
    for i,cid in enumerate(ids):
        x=(i%8)*256;y=(i//8)*290
        sheet.paste(Image.open(OUT/'Icons'/f'{cid}.png'),(x,y),Image.open(OUT/'Icons'/f'{cid}.png'))
        draw.text((x+128,y+266),cid,fill='white',font=font,anchor='mm')
    sheet.save(ROOT/'art/previews/IconContactSheet.png')
    pip_sheet=Image.new('RGB',(1024,1024),(24,39,51));pip_sheet.paste(atlas,(0,0),atlas);pip_sheet.save(ROOT/'art/previews/PipContactSheet.png')
    report={'icons':len(icons),'uniqueIconPixelHashes':len({hashlib.sha256((OUT/'Icons'/f'{i}.png').read_bytes()).hexdigest() for i in ids}),'pipTiles':len(tiles),'pipsPerTile':[{'color':t['color'],'pip':t['pip'],'geometryDotCount':len(PIPS[t['pip']])} for t in tiles],'audioClips':len(audio),'pngSize':[256,256],'transparent':True,'atlasSize':[1024,1024],'sourceSvgCount':len(list(SOURCE.glob('*.svg'))),'unityImportValidated':False}
    assert report['icons']==38 and report['uniqueIconPixelHashes']==38
    assert all(t['pip']==t['geometryDotCount'] for t in report['pipsPerTile'])
    (EVIDENCE/'symbols_generation.json').write_text(json.dumps(report,indent=2),encoding='utf8')
    print(json.dumps(report))

if __name__=='__main__':main()
