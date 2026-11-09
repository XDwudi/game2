"""Editable HD print symbols / branding. Writes ONLY ArtSource/Art071/Full.

SVG is the geometric source; PNG is an antialiased matching export. Gameplay
numbers and variable text remain runtime-owned. Requires Pillow, fontTools, numpy.
Usage: python tools/art_v071/build_full_ui.py --representatives | --all
"""
from __future__ import annotations
import argparse
import hashlib
import html
import json
import math
from pathlib import Path
import re
import sys
import xml.etree.ElementTree as ET
import numpy as np

from PIL import Image, ImageDraw, ImageFont, __version__ as PIL_VERSION
from fontTools import __version__ as FONTTOOLS_VERSION
from fontTools.ttLib import TTFont
from fontTools.pens.svgPathPen import SVGPathPen

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "ArtSource/Art071/Full"
FONT = ROOT / "DicePrototype/Assets/Game/Fonts/NotoSansSC-Bold.otf"
SCRIPT = Path(__file__).resolve()
C = dict(felt="#173E40", deep="#102A2D", paper="#F4E8CB", ink="#162629",
         red="#EC655D", blue="#55B6D9", gold="#F3BA58", mint="#79C99C",
         violet="#AD92DB", white="#FFFFFF", shadow="#07191C")


def sha(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()


def rgba(color, opacity=1):
    if color is None:
        return None
    v = C.get(color, color).lstrip("#")
    return tuple(int(v[i:i + 2], 16) for i in (0, 2, 4)) + (round(opacity * 255),)


class Canvas:
    """Small paired vector/raster drawing layer, no SVG dependency needed."""
    def __init__(self, width=128, height=128, export_scale=2):
        self.w, self.h, self.export_scale = width, height, export_scale
        self.scale = export_scale * 3
        self.im = Image.new("RGBA", (round(width*self.scale), round(height*self.scale)))
        self.svg = []

    def attrs(self, fill, stroke, sw, opacity):
        return (f'fill="{C.get(fill, fill) if fill else "none"}" '
                f'stroke="{C.get(stroke, stroke) if stroke else "none"}" '
                f'stroke-width="{sw}" stroke-linecap="round" stroke-linejoin="round" opacity="{opacity}"')

    def layer(self):
        layer = Image.new("RGBA", self.im.size)
        return layer, ImageDraw.Draw(layer)

    def rect(self, x, y, w, h, fill=None, stroke=None, sw=1, radius=0, opacity=1):
        self.svg.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{radius}" {self.attrs(fill, stroke, sw, opacity)}/>')
        l, d = self.layer(); s = self.scale
        box = tuple(round(t*s) for t in (x, y, x+w, y+h))
        if radius:
            d.rounded_rectangle(box, round(radius*s), fill=rgba(fill, opacity), outline=rgba(stroke, opacity), width=max(1,round(sw*s)))
        else:
            d.rectangle(box, fill=rgba(fill, opacity), outline=rgba(stroke, opacity), width=max(1,round(sw*s)))
        self.im.alpha_composite(l)

    def ellipse(self, x, y, w, h, fill=None, stroke=None, sw=1, opacity=1):
        self.svg.append(f'<ellipse cx="{x+w/2}" cy="{y+h/2}" rx="{w/2}" ry="{h/2}" {self.attrs(fill, stroke, sw, opacity)}/>')
        l, d = self.layer(); s=self.scale
        d.ellipse(tuple(round(t*s) for t in (x,y,x+w,y+h)),fill=rgba(fill,opacity),outline=rgba(stroke,opacity),width=max(1,round(sw*s)))
        self.im.alpha_composite(l)

    def poly(self, points, fill=None, stroke=None, sw=1, opacity=1, closed=True):
        tag="polygon" if closed else "polyline"
        self.svg.append(f'<{tag} points="'+" ".join(f'{x},{y}' for x,y in points)+f'" {self.attrs(fill,stroke,sw,opacity)}/>')
        l,d=self.layer();s=self.scale;p=[(round(x*s),round(y*s)) for x,y in points]
        if fill and closed:d.polygon(p,fill=rgba(fill,opacity))
        if stroke:
            d.line(p+([p[0]] if closed else []),fill=rgba(stroke,opacity),width=max(1,round(sw*s)),joint="curve")
            r=sw*s/2
            for x,y in p:d.ellipse((round(x-r),round(y-r),round(x+r),round(y+r)),fill=rgba(stroke,opacity))
        self.im.alpha_composite(l)

    def line(self, points, color="ink", sw=4, opacity=1):
        self.poly(points,None,color,sw,opacity,False)

    def cubic(self, a,b,c,d,color="white",sw=4,opacity=1):
        self.svg.append(f'<path d="M {a[0]} {a[1]} C {b[0]} {b[1]} {c[0]} {c[1]} {d[0]} {d[1]}" {self.attrs(None,color,sw,opacity)}/>')
        pts=[]
        for i in range(81):
            t=i/80;u=1-t
            pts.append((u*u*u*a[0]+3*u*u*t*b[0]+3*u*t*t*c[0]+t*t*t*d[0],u*u*u*a[1]+3*u*u*t*b[1]+3*u*t*t*c[1]+t*t*t*d[1]))
        # Keep just the true cubic in SVG; raster approximates to subpixel accuracy.
        count=len(self.svg);self.line(pts,color,sw,opacity);self.svg=self.svg[:count]

    def text_outlines(self, text, x, baseline, size, fill="ink"):
        font=TTFont(FONT);gs=font.getGlyphSet();cmap=font.getBestCmap();upem=font['head'].unitsPerEm;s=size/upem
        cursor=x
        for char in text:
            name=cmap[ord(char)];pen=SVGPathPen(gs);gs[name].draw(pen)
            self.svg.append(f'<path d="{pen.getCommands()}" transform="translate({cursor} {baseline}) scale({s} {-s})" fill="{C[fill]}"/>')
            cursor+=gs[name].width*s
        font.close()
        l,d=self.layer();rfont=ImageFont.truetype(str(FONT),round(size*self.scale))
        d.text((round(x*self.scale),round(baseline*self.scale)),text,font=rfont,fill=rgba(fill),anchor="ls")
        self.im.alpha_composite(l)

    def rotate(self, degrees):
        self.svg=[f'<g transform="rotate({-degrees} {self.w/2} {self.h/2})">',*self.svg,'</g>']
        self.im=self.im.rotate(degrees,Image.Resampling.BICUBIC,expand=False,center=(self.im.width/2,self.im.height/2))

    def radial(self,cx,cy,radius,stops):
        identity='gradient'+str(len(self.svg))
        self.svg.append(f'<defs><radialGradient id="{identity}">'+''.join(f'<stop offset="{pos*100}%" stop-color="#FFFFFF" stop-opacity="{alpha}"/>' for pos,alpha in stops)+'</radialGradient></defs>')
        self.svg.append(f'<circle cx="{cx}" cy="{cy}" r="{radius}" fill="url(#{identity})"/>')
        ys,xs=np.ogrid[:self.im.height,:self.im.width]
        distances=np.hypot((xs+.5)/self.scale-cx,(ys+.5)/self.scale-cy)/radius
        alpha=np.interp(distances,[p for p,v in stops],[v for p,v in stops],left=stops[0][1],right=0)
        pixels=np.full((self.im.height,self.im.width,4),255,dtype=np.uint8);pixels[:,:,3]=np.round(alpha*255).astype(np.uint8)
        self.im.alpha_composite(Image.fromarray(pixels))

    def save(self, folder, name, role, consumers, records):
        target=OUT/folder;target.mkdir(parents=True,exist_ok=True)
        svg=target/(name+'.svg');png=target/(name+'.png')
        svg.write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="{round(self.w*self.export_scale)}" height="{round(self.h*self.export_scale)}" viewBox="0 0 {self.w} {self.h}"><title>{html.escape(name)}</title><desc>DiceCraft v0.71; editable deterministic paths; no runtime values.</desc>'+''.join(self.svg)+'</svg>\n',encoding='utf-8')
        im=self.im.resize((round(self.w*self.export_scale),round(self.h*self.export_scale)),Image.Resampling.LANCZOS)
        # Do not leave paper/white RGB in fully transparent pixels (bilinear halo).
        px=im.load()
        for y in range(im.height):
            for x in range(im.width):
                if px[x,y][3]==0:px[x,y]=(0,0,0,0)
        im.save(png)
        alpha=im.getchannel('A');bounds=alpha.getbbox();edge=max(max(alpha.crop((0,0,im.width,1)).getdata()),max(alpha.crop((0,im.height-1,im.width,im.height)).getdata()),max(alpha.crop((0,0,1,im.height)).getdata()),max(alpha.crop((im.width-1,0,im.width,im.height)).getdata()))
        assert bounds and edge==0,(name,'clipped edge',bounds,edge)
        ET.parse(svg)
        records.append(dict(id=name,category=folder,role=role,source=svg.relative_to(ROOT).as_posix(),export=png.relative_to(ROOT).as_posix(),source_sha256=sha(svg),sha256=sha(png),dimensions=list(im.size),alpha_bounds=list(bounds),edge_alpha_max=edge,alpha_range=list(alpha.getextrema()),consumers=consumers,importer=dict(textureType='Sprite',spriteMode='Single',filterMode='Bilinear',wrapMode='Clamp',mipmapEnabled=False,alphaIsTransparency=True,sRGBTexture=True,compression='Uncompressed',pivot=[.5,.5])))
        return im


def cross(a,x=64,y=64,size=24,color="ink",sw=10,multiply=False):
    if multiply:
        a.line([(x-size,y-size),(x+size,y+size)],color,sw);a.line([(x+size,y-size),(x-size,y+size)],color,sw)
    else:
        a.line([(x-size,y),(x+size,y)],color,sw);a.line([(x,y-size),(x,y+size)],color,sw)


def cup(a, color="paper", ink="ink", x=0, y=0, s=1):
    def p(points):return [(x+px*s,y+py*s) for px,py in points]
    a.poly(p([(34,43),(94,43),(84,103),(44,103)]),color,ink,4*s)
    a.rect(x+28*s,y+34*s,72*s,13*s,color,ink,4*s,5*s)
    a.line(p([(45,54),(49,89)]),"white",3*s)
    a.ellipse(x+57*s,y+67*s,14*s,14*s,"blue")


def badge(kind):
    a=Canvas()
    if kind=='slotless':
        # Open center remains transparent: the shape itself says unoccupied slot.
        a.rect(15,17,98,94,None,"ink",7,13)
        a.rect(20,22,88,84,None,"mint",3,9)
        for x,y,sx,sy in [(27,30,1,1),(101,30,-1,1),(27,98,1,-1),(101,98,-1,-1)]:
            a.line([(x+sx*19,y),(x,y),(x,y+sy*19)],"paper",5)
    elif kind=='base10':
        a.poly([(27,10),(101,10),(118,27),(118,101),(101,118),(27,118),(10,101),(10,27)],"mint","ink",5)
        a.rect(19,19,90,90,None,"paper",2,9)
        cross(a,size=25,sw=13)
        for x in (33,64,95):a.ellipse(x-2,25,4,4,"ink")
    elif kind=='mult2':
        a.poly([(12,12),(116,12),(116,96),(94,116),(12,116)],"violet","ink",5)
        a.line([(23,91),(23,23),(103,23)],"paper",2)
        cross(a,size=24,sw=12,multiply=True)
        a.poly([(94,116),(94,96),(116,96)],"paper","ink",3)
    elif kind=='submit1':
        a.poly([(21,16),(107,16),(107,40),(117,48),(117,80),(107,88),(107,112),(21,112),(21,88),(11,80),(11,48),(21,40)],"paper","ink",5)
        a.rect(29,25,70,78,None,"mint",3,4)
        a.line([(43,47),(86,47)],"ink",5);a.line([(43,62),(74,62)],"ink",5)
        a.line([(44,82),(51,89),(66,75)],"mint",6)
        for y in (42,58,74,90):a.ellipse(16,y,3,3,"ink");a.ellipse(109,y,3,3,"ink")
    elif kind=='roll1':
        a.rect(10,10,108,108,"blue","ink",5,18)
        a.rect(18,18,92,92,None,"paper",2,12)
        cup(a)
        a.line([(46,24),(43,17)],"ink",3);a.line([(65,23),(65,14)],"ink",3);a.line([(84,24),(87,17)],"ink",3)
    else:raise ValueError(kind)
    return a


def wordmark():
    a=Canvas(1440,400,1)
    # A fixed original composition from licensed existing glyph outlines.
    a.line([(288,321),(1234,321)],"red",14)
    a.line([(290,346),(435,346)],"blue",6)
    a.line([(1256,321),(1320,321)],"red",14)
    a.text_outlines('骰子构筑',282,286,248)
    a.poly([(90,104),(182,82),(242,136),(222,240),(128,263),(71,208)],"paper","ink",10)
    a.poly([(90,104),(182,82),(242,136),(149,159)],"paper","ink",6)
    a.poly([(149,159),(242,136),(222,240),(128,263)],"blue","ink",6)
    a.poly([(90,104),(149,159),(128,263),(71,208)],"paper","ink",6)
    a.ellipse(147,111,25,19,"red")
    a.ellipse(171,177,20,24,"ink");a.ellipse(194,212,16,20,"ink")
    for x,y in [(95,137),(105,176),(115,216)]:a.ellipse(x,y,11,16,"red")
    a.line([(75,79),(57,58)],"red",8);a.line([(109,65),(103,38)],"red",8)
    a.rotate(1.5)
    return a


def fx(kind):
    a=Canvas(128,128,2)
    if kind=='arc_short':
        for width,alpha in [(12,.05),(8,.10),(4,.35),(1.7,.95)]:a.cubic((15,91),(27,22),(80,19),(111,55),"white",width,alpha)
        a.ellipse(105,50,8,8,"white",opacity=.92)
        a.ellipse(104,49,10,10,"white",opacity=.15)
    elif kind=='star_spark':
        pts=[(64,12),(73,48),(108,64),(75,74),(64,116),(53,76),(16,64),(52,53)]
        a.poly(pts,"white",opacity=.16)
        a.poly([(64,22),(70,56),(99,64),(70,70),(64,106),(58,70),(27,64),(58,58)],"white")
        a.ellipse(95,22,7,7,"white",opacity=.8)
    elif kind=='ring_soft':
        a.radial(64,64,56,[(0,0),(.75,0),(.79,.06),(.82,.23),(.84,.90),(.86,.23),(.89,.06),(1,0)])
    elif kind=='line_soft':
        for width,alpha in [(15,.03),(9,.09),(4,.33),(1.3,.9)]:a.line([(12,64),(116,64)],"white",width,alpha)
    elif kind=='square_chip':
        a.poly([(31,23),(98,29),(94,103),(27,95)],"white")
        a.line([(38,33),(87,38)],"white",3,.6)
    elif kind=='short_flame':
        a.poly([(21,107),(31,81),(43,75),(42,47),(57,61),(76,18),(77,65),(98,52),(95,83),(108,107)],"white",opacity=.87)
    elif kind=='halo_soft':
        a.radial(64,64,56,[(0,.5),(.12,.46),(.25,.35),(.4,.22),(.6,.10),(.8,.025),(1,0)])
    elif kind=='crack_card':
        a.line([(73,10),(52,38),(70,52),(45,77),(63,86),(49,118)],"white",4)
        a.line([(53,39),(30,32)],"white",2);a.line([(47,77),(95,71)],"white",2)
    else:raise ValueError(kind)
    return a


def symbol(kind):
    a=Canvas()
    if kind in ('plus','multiply','close'):
        cross(a,size=34,color='ink',sw=18,multiply=kind!='plus')
        cross(a,size=34,color='paper',sw=13,multiply=kind!='plus')
    elif kind in ('arrow_right','back'):
        pts=[(13,52),(72,52),(72,27),(114,64),(72,101),(72,76),(13,76)]
        if kind=='back':pts=[(128-x,y) for x,y in pts]
        a.poly(pts,'paper','ink',4)
    elif kind=='lock':
        a.cubic((35,57),(29,3),(99,3),(93,57),'ink',15)
        a.cubic((35,57),(29,3),(99,3),(93,57),'paper',7)
        a.rect(21,52,86,62,'paper','ink',5,9)
        a.ellipse(56,69,16,16,'ink');a.rect(60,80,8,19,'ink',radius=3)
    elif kind=='pending':
        for x,y,sx,sy in [(16,16,1,1),(112,16,-1,1),(16,112,1,-1),(112,112,-1,-1)]:
            a.line([(x+sx*23,y),(x,y),(x,y+sy*23)],'gold',5)
        a.line([(35,66),(55,85),(93,43)],'gold',7)
    elif kind=='refresh':
        a.cubic((100,83),(85,116),(22,112),(21,65),'ink',12)
        a.cubic((21,65),(20,26),(66,13),(94,36),'ink',12)
        a.cubic((100,83),(85,116),(22,112),(21,65),'paper',8)
        a.cubic((21,65),(20,26),(66,13),(94,36),'paper',8)
        a.poly([(98,17),(108,51),(75,47)],'paper','ink',3)
    elif kind=='coin':
        a.ellipse(16,20,96,96,'gold','ink',5)
        a.ellipse(16,12,96,96,'gold','ink',5)
        a.ellipse(27,23,74,74,None,'paper',3)
        a.rect(52,43,24,32,'ink',radius=3)
        a.line([(33,33),(45,27)],'paper',4)
    elif kind=='roll':
        cup(a)
        a.cubic((19,75),(4,39),(32,12),(49,15),'blue',4)
        a.poly([(51,9),(59,18),(45,22)],'blue')
    elif kind=='submit':
        a.line([(20,79),(20,107),(108,107),(108,79)],'ink',13)
        a.line([(20,79),(20,107),(108,107),(108,79)],'paper',9)
        a.poly([(54,89),(74,89),(74,48),(96,48),(64,17),(32,48),(54,48)],'paper','ink',4)
    elif kind=='settings':
        pts=[]
        for i in range(32):
            t=i*math.pi/16; r=51 if i%4 in (0,1) else 42
            pts.append((64+r*math.cos(t),64+r*math.sin(t)))
        a.poly(pts,'paper','ink',4);a.ellipse(45,45,38,38,'ink')
        a.ellipse(51,51,26,26,'felt')
    elif kind in ('sound','muted'):
        a.poly([(15,47),(38,47),(68,22),(68,106),(38,81),(15,81)],'paper','ink',4)
        if kind=='sound':
            a.cubic((82,44),(97,53),(97,75),(82,84),'ink',9)
            a.cubic((94,26),(121,44),(121,83),(94,102),'ink',9)
            a.cubic((82,44),(97,53),(97,75),(82,84),'paper',5)
            a.cubic((94,26),(121,44),(121,83),(94,102),'paper',5)
        else:cross(a,97,64,16,'red',7,True)
    elif kind=='info':
        a.ellipse(14,14,100,100,'paper','ink',4)
        a.ellipse(57,31,14,14,'ink');a.line([(61,58),(67,58),(67,94)],'ink',8)
        a.line([(55,94),(78,94)],'ink',6)
    elif kind=='check':a.line([(19,64),(48,94),(109,30)],'mint',12)
    elif kind=='warning':
        a.poly([(64,13),(116,108),(12,108)],'gold','ink',5)
        a.line([(64,43),(64,73)],'ink',9);a.ellipse(59,87,10,10,'ink')
    elif kind=='temporary':
        a.ellipse(16,16,96,96,'paper','ink',5)
        a.ellipse(25,25,78,78,None,'gold',3)
        a.line([(64,36),(64,66),(85,78)],'ink',6);a.ellipse(58,58,12,12,'gold','ink',2)
        for x,y in [(64,25),(103,64),(64,103),(25,64)]:a.ellipse(x-2,y-2,4,4,'ink')
    elif kind=='smooth':
        for y in (37,64,91):a.cubic((14,y),(44,y-27),(76,y+27),(114,y),'blue',7)
    elif kind=='rough':
        for x,y,r in [(34,38,20),(92,37,15),(66,92,23)]:
            a.poly([(x-r,y-4),(x-10,y-r),(x+13,y-r+4),(x+r,y+8),(x+4,y+r),(x-r+3,y+12)],'paper','ink',4)
            a.ellipse(x-3,y-5,5,5,'ink')
    elif kind=='gold':
        a.rect(22,22,84,84,None,'gold',7,8)
        a.line([(9,25),(9,9),(25,9)],'gold',3)
        a.line([(103,119),(119,119),(119,103)],'gold',3)
        a.poly([(101,7),(105,19),(117,23),(105,27),(101,39),(97,27),(85,23),(97,19)],'paper')
    elif kind=='glass':
        a.poly([(37,15),(91,15),(115,47),(64,113),(13,47)],'blue','paper',4)
        a.line([(37,15),(37,47),(64,113),(91,47),(91,15)],'paper',3)
        a.line([(13,47),(115,47)],'ink',3)
        a.line([(37,15),(64,47),(91,15)],'paper',2)
    elif kind=='color_red':a.ellipse(20,20,88,88,'red','ink',5)
    elif kind=='color_blue':a.poly([(64,12),(116,64),(64,116),(12,64)],'blue','ink',5)
    elif kind=='broken_link':
        a.cubic((58,48),(66,15),(14,15),(18,48),'violet',8)
        a.cubic((18,48),(16,72),(42,79),(51,65),'violet',8)
        a.cubic((75,65),(88,51),(114,60),(110,85),'violet',8)
        a.cubic((110,85),(108,112),(65,117),(66,86),'violet',8)
        a.line([(61,29),(67,13)],'gold',4);a.line([(78,45),(94,34)],'gold',4)
    elif kind=='broken':
        a.rect(25,14,78,100,'paper','ink',4,9)
        a.line([(76,14),(51,43),(75,59),(48,84),(67,113)],'red',7)
    elif kind=='eye':
        a.poly([(11,64),(33,40),(64,29),(96,40),(117,64),(96,88),(64,99),(33,88)],'paper','ink',4)
        a.ellipse(43,43,42,42,'ink');a.ellipse(51,47,11,11,'paper')
    elif kind=='unavailable':
        a.ellipse(17,17,94,94,'paper','ink',5)
        a.line([(32,96),(96,32)],'red',10)
    else:raise ValueError(kind)
    return a


def cardback():
    a=Canvas(768,1024,1)
    # Same paper, three-border rhythm and restrained short offset as existing UI.
    a.rect(16,22,736,986,'shadow',radius=34,opacity=.18)
    a.rect(8,8,742,994,'paper','ink',6,32)
    a.rect(22,22,714,966,None,'gold',2,24)
    a.rect(33,33,692,944,None,'ink',2,18)
    a.rect(59,59,640,892,None,'ink',2,12)
    a.rect(74,74,610,862,None,'mint',2,8)
    # Quiet repeat on the back only, no gameplay numbers or card text.
    for y in range(109,924,58):
        for x in range(108,672,58):
            if ((x-384)/258)**2+((y-512)/288)**2<1:continue
            a.poly([(x,y-5),(x+5,y),(x,y+5),(x-5,y)],'ink',opacity=.16)
    a.ellipse(128,256,512,512,None,'ink',6)
    a.ellipse(143,271,482,482,None,'mint',3)
    a.ellipse(158,286,452,452,None,'ink',2)
    # Original double-face dice seal, different from content C29/C48 artwork.
    a.poly([(244,414),(404,370),(532,450),(504,617),(342,665),(219,580)],'paper','ink',12)
    a.poly([(244,414),(404,370),(532,450),(371,497)],'paper','ink',8)
    a.poly([(371,497),(532,450),(504,617),(342,665)],'blue','ink',8)
    a.poly([(244,414),(371,497),(342,665),(219,580)],'paper','ink',8)
    a.ellipse(374,414,40,29,'red')
    for x,y in [(256,459),(281,521),(307,583)]:a.ellipse(x,y,27,35,'red')
    for x,y in [(411,533),(447,582)]:a.ellipse(x,y,31,38,'ink')
    # Engraved flourishes stay outside the recognizable die silhouette.
    a.line([(206,368),(184,342)],'red',9);a.line([(243,344),(230,307)],'red',8)
    a.line([(526,684),(544,710)],'red',8);a.line([(558,662),(588,680)],'red',8)
    a.line([(251,198),(354,198)],'ink',4);a.line([(414,198),(517,198)],'ink',4)
    a.poly([(384,181),(401,198),(384,215),(367,198)],'red','ink',3)
    a.line([(251,827),(354,827)],'ink',4);a.line([(414,827),(517,827)],'ink',4)
    a.poly([(384,810),(401,827),(384,844),(367,827)],'blue','ink',3)
    return a


def consumers(name):
    found=[]
    for p in sorted((ROOT/'DicePrototype/Assets/Game').rglob('*.cs')):
        if '/Tests/' in p.as_posix():continue
        for i,line in enumerate(p.read_text(encoding='utf-8-sig').splitlines(),1):
            if name in line:found.append(p.relative_to(ROOT).as_posix()+':'+str(i))
    return found or ['Serialized app uiTemplates / Art07 prefab image references; exact-name replacement']


def catalogue(records):
    symbols=[r for r in records if r['category']=='Symbols'];cols=7;rows=math.ceil(len(symbols)/cols)
    sheet=Image.new('RGB',(1400,rows*185+70),C['felt']);d=ImageDraw.Draw(sheet)
    font=ImageFont.truetype(str(FONT),15);heading=ImageFont.truetype(str(FONT),25)
    d.text((20,15),'v0.71 · 5 附魔 + 28 公共符号 / 同源高清重绘',font=heading,fill=C['paper'])
    for i,r in enumerate(symbols):
        x=20+i%cols*198;y=65+i//cols*185
        im=Image.open(ROOT/r['export']).convert('RGBA');im.thumbnail((108,108))
        d.rounded_rectangle((x,y,x+172,y+128),10,fill=C['paper'])
        sheet.paste(im,(x+32,y+10),im)
        # Small display on real felt background. Paper symbols need both backdrops.
        small=Image.open(ROOT/r['export']).convert('RGBA').resize((28,28),Image.Resampling.LANCZOS)
        sheet.paste(small,(x+138,y+140),small)
        name=r['id'].replace('badge_enchant_','E:').replace('ui_icon_','')
        d.text((x,y+136),name,font=font,fill=C['paper'])
    sheet.save(OUT/'Symbols/catalogue.png')
    effects=[r for r in records if r['category']=='FX'];sheet=Image.new('RGB',(1200,390),C['felt']);d=ImageDraw.Draw(sheet)
    for i,r in enumerate(effects):
        x=15+(i%4)*295;y=10+(i//4)*190
        im=Image.open(ROOT/r['export']).convert('RGBA').resize((146,146),Image.Resampling.LANCZOS);sheet.paste(im,(x+65,y),im)
        d.text((x+35,y+151),r['id'],font=font,fill=C['paper'])
    sheet.save(OUT/'FX/catalogue.png')


def preview(images, target):
    sheet=Image.new('RGB',(1440,820),C['felt']);d=ImageDraw.Draw(sheet)
    label=ImageFont.truetype(str(FONT),23)
    d.text((28,18),'v0.71 · 代表样张 / 真实导出缩放',font=label,fill=C['paper'])
    for i,(name,im) in enumerate(images[:2]):
        x=30+i*340
        d.rounded_rectangle((x,70,x+300,335),14,fill=C['paper'])
        im=im.copy();im.thumbnail((220,220));sheet.paste(im,(x+40,85),im)
        d.text((x,350),name,font=label,fill=C['paper'])
        small=im.resize((32,32),Image.Resampling.LANCZOS);sheet.paste(small,(x+235,288),small)
    logo=images[2][1].copy();logo.thumbnail((1270,360));
    d.rectangle((20,395,1420,800),fill=C['paper']);sheet.paste(logo,(70,420),logo)
    d.text((760,90),'暖纸 / 深桌面 / 32px角标',font=label,fill=C['paper'])
    for i,(_,im) in enumerate(images[:2]):
        small=im.resize((128,128),Image.Resampling.LANCZOS);sheet.paste(small,(790+i*190,160),small)
    sheet.save(target)


def main():
    parser=argparse.ArgumentParser();g=parser.add_mutually_exclusive_group(required=True)
    g.add_argument('--representatives',action='store_true');g.add_argument('--all',action='store_true')
    args=parser.parse_args();records=[]
    (OUT/'UI').mkdir(parents=True,exist_ok=True)
    for source,name in [('LICENSE.txt','NotoSansSC-Bold-OFL.txt'),('COPYRIGHT.txt','NotoSansSC-Bold-COPYRIGHT.txt')]:
        (OUT/'UI'/name).write_bytes((ROOT/'ArtSource/Art071/Fonts'/source).read_bytes())
    b=badge('base10').save('Symbols','badge_enchant_base10','base10 / mint plus stamp',['GameApp.V05.EnchantBadge','app.enchantIcons'],records)
    f=fx('arc_short').save('FX','fx_arc_short','short tintable trajectory / new consumer required',['NEW: controller-to-score effect; do not replace orthogonal copy-link semantics'],records)
    w=wordmark().save('UI','ui_wordmark_dicecraft','fixed Chinese wordmark; original layout from licensed Noto glyphs',['GameApp.Art071Screens.Menu071/MenuWordmark','GameApp.Art07 Brand optional'],records)
    preview([('base10 · 加号印章',b),('短弧 · 透明可着色',f),('wordmark',w)],OUT/'UI/representatives.png')
    if args.all:
        for kind in ['mult2','slotless','submit1','roll1']:
            name='badge_enchant_'+kind
            badge(kind).save('Symbols',name,'controller enchantment / '+kind,['GameApp.V05.EnchantBadge','app.enchantIcons'],records)
        for p in sorted((ROOT/'DicePrototype/Assets/Art/Art07/UI/Symbols').glob('*.png')):
            kind=p.stem.removeprefix('ui_icon_')
            symbol(kind).save('Symbols',p.stem,'shared status/action / '+kind,consumers(p.stem),records)
        for p in sorted((ROOT/'DicePrototype/Assets/Art/Art07/FX/Textures').glob('*.png')):
            kind=p.stem.removeprefix('fx_')
            fx(kind).save('FX',p.stem,'white RGB tintable FX / '+kind,consumers(p.stem),records)
        cardback().save('UI','ui_card_back','original printed dice seal; same stock and border as v071 paper',['GameApp.Art071Screens.Menu071/MenuCardBack*; preserve card container size'],records)
        catalogue(records)
    manifest=dict(schema='dicecraft-v071-full-ui-1',status='source_export_review_not_integrated' if args.all else 'representatives_only_not_integrated',editable_source=SCRIPT.relative_to(ROOT).as_posix(),script_sha256=sha(SCRIPT),tools={'python':sys.version.split()[0],'Pillow':PIL_VERSION,'fontTools':FONTTOOLS_VERSION,'numpy':np.__version__},palette=C,font=dict(path=FONT.relative_to(ROOT).as_posix(),sha256=sha(FONT),license='ArtSource/Art071/Fonts/LICENSE.txt',copyright='ArtSource/Art071/Fonts/COPYRIGHT.txt',use='fixed title outlines only'),reference='docs/specs/v071/handoff/references/battle_master_v071.png',reference_sha256=sha(ROOT/'docs/specs/v071/handoff/references/battle_master_v071.png'),assets=records)
    (OUT/'UI'/('manifest.json' if args.all else 'representatives.manifest.json')).write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    print(json.dumps({'status':manifest['status'],'assets':len(records),'output':str(OUT)},ensure_ascii=False))


if __name__=='__main__':main()
