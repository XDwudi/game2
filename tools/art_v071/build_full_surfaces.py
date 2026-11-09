"""Deterministic v0.71 remaining twelve UI surfaces; source only, never Assets.

Run: python tools/art_v071/build_full_surfaces.py
Pillow exports the same geometric primitives as the editable SVG. It supersamples
at 3x; no old low-resolution texture is sampled or enlarged. Previews simulate
Unity Sliced at referencePixelsPerUnit=100, not a Unity acceptance screenshot.
"""
from __future__ import annotations

import hashlib
import html
import json
from pathlib import Path
import platform
import random
import xml.etree.ElementTree as ET

import numpy as np
from PIL import Image, ImageDraw, ImageFont, __version__ as PIL_VERSION

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "ArtSource/Art071/Full/Surfaces"
SCRIPT = Path(__file__).resolve()
PALETTE = dict(paper="#F4E8CB", ink="#162629", felt="#173E40", deep="#102A2D",
               info="#173538", line="#486365", gold="#D6BC8A", shadow="#07191C",
               blue="#55B6D9", red="#EC655D", focus="#F3BA58", white="#FFFFFF")


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def rgba(color, alpha=1):
    value = PALETTE.get(color, color).lstrip("#")
    return tuple(int(value[i:i+2], 16) for i in (0, 2, 4)) + (round(alpha*255),)


class Canvas:
    def __init__(self, width=512, height=512):
        self.w, self.h, self.s = width, height, 3
        self.image = Image.new("RGBA", (width*self.s, height*self.s))
        self.svg = []

    def rect(self, x, y, w, h, radius, fill=None, stroke=None, sw=1, opacity=1):
        """Inside stroke in both exports, so sliced corner measurements agree."""
        fill_value = PALETTE.get(fill, fill) if fill else "none"
        self.svg.append(f'<rect x="{x}" y="{y}" width="{w}" height="{h}" rx="{radius}" fill="{fill_value}" opacity="{opacity}"/>')
        if stroke:
            self.svg.append(f'<rect x="{x+sw/2}" y="{y+sw/2}" width="{w-sw}" height="{h-sw}" rx="{max(0,radius-sw/2)}" fill="none" stroke="{PALETTE.get(stroke,stroke)}" stroke-width="{sw}" opacity="{opacity}"/>')
        layer = Image.new("RGBA", self.image.size)
        d = ImageDraw.Draw(layer)
        # PIL includes both box endpoints; subtract one supersample pixel.
        box = (round(x*self.s), round(y*self.s), round((x+w)*self.s)-1, round((y+h)*self.s)-1)
        d.rounded_rectangle(box, round(radius*self.s), fill=rgba(fill, opacity) if fill else None,
                            outline=rgba(stroke, opacity) if stroke else None,
                            width=max(1, round(sw*self.s)))
        self.image.alpha_composite(layer)

    def grain(self, seed=71):
        rng = random.Random(seed)
        # Low-contrast stock texture; entirely contained in the stretchable center.
        layer = Image.new("RGBA", self.image.size)
        d = ImageDraw.Draw(layer)
        for _ in range(1400):
            x, y = rng.randrange(36, self.w-36), rng.randrange(36, self.h-36)
            alpha = rng.choice((.018, .024, .032))
            self.svg.append(f'<rect x="{x}" y="{y}" width="0.5" height="0.5" fill="#795F3B" opacity="{alpha}"/>')
            d.rectangle((x*self.s, y*self.s, x*self.s+1, y*self.s+1), fill=rgba("#795F3B",alpha))
        self.image.alpha_composite(layer)

    def finish(self, identity, tint_mode):
        self.svg_path = OUT / (identity + ".svg")
        self.png_path = OUT / (identity + ".png")
        self.svg_path.write_text(f'<svg xmlns="http://www.w3.org/2000/svg" width="{self.w}" height="{self.h}" viewBox="0 0 {self.w} {self.h}"><title>{html.escape(identity)}</title><desc>v0.71 editable vector geometry; no baked dynamic text or values.</desc>'+''.join(self.svg)+'</svg>\n',encoding="utf-8")
        im = self.image.resize((self.w,self.h),Image.Resampling.LANCZOS)
        pixels = np.array(im)
        pixels[pixels[:,:,3] == 0] = 0
        if tint_mode == "white_alpha":
            pixels[pixels[:,:,3] > 0,:3] = 255
        im = Image.fromarray(pixels)
        im.save(self.png_path)
        ET.parse(self.svg_path)
        return im


def soft_shadow(a):
    # Nested low-alpha silhouettes approximate a short soft shadow; no bevel foot.
    for extent, opacity in ((5,.025),(4,.035),(3,.05),(2,.07),(1,.09)):
        a.rect(6-extent/2, 9-extent/2, 496+extent, 493+extent, 22+extent/2, "shadow", opacity=opacity)


def paper(a, kind):
    if kind in ("card", "modal", "tooltip"):
        soft_shadow(a)
    weight = 3 if kind in ("card", "modal") else 1.6
    a.rect(4,4,494,491,21,"paper","ink",weight)
    if kind == "card":
        a.rect(12.5,12.5,477,474,16,None,"gold",2.5)
        a.rect(20,20,462,459,13,None,"ink",1.25)
    elif kind == "modal":
        a.rect(14,14,474,471,13,None,"gold",1.5)
    elif kind == "tooltip":
        a.rect(11,11,480,477,15,None,"gold",1)
    a.grain()


def draw(identity):
    # 512-square assets match the existing five Art071 surfaces' import convention.
    a = Canvas()
    if identity in ("ui_card_item", "ui_card_upgrade"):
        paper(a,"card")
    elif identity == "ui_panel_modal":
        paper(a,"modal")
    elif identity == "ui_tooltip_frame":
        paper(a,"tooltip")
    elif identity == "ui_chip_label":
        paper(a,"chip")
    elif identity == "ui_panel_plain":
        a.rect(4,4,504,504,20,"info","line",1.8)
        a.rect(9,9,494,494,16,None,"#112D30",2)
    elif identity in ("ui_slot_dice", "ui_slot_inventory"):
        # Empty slots do not paint a panel over the physical dice camera.
        a.rect(5,5,502,502,23,None,"#0C2528",3,.8)
        a.rect(8,7,496,496,20,None,"#577275",2.2,.75)
        if identity == "ui_slot_inventory":
            a.rect(14,13,484,484,15,None,"#486365",1,.36)
    elif identity == "ui_focus_frame":
        # Also sampled by the selection material; never bake ink/gold into this.
        a.rect(5,5,502,502,24,None,"white",4)
    elif identity == "ui_scroll_handle":
        a.rect(5,5,502,502,24,"#FFFFFF", "#B9C8C7",2)
        a.rect(11,11,490,490,18,None,"#E8EEEA",1)
    elif identity in ("ui_progress_fill", "ui_progress_track"):
        # These consumers use Image.Filled/Simple, not Sliced. A strip avoids the
        # enormous native aspect change of a square while keeping HD endpoints.
        a = Canvas(768,96)
        if identity == "ui_progress_fill":
            a.rect(4,4,760,88,20,"white")
        else:
            a.rect(4,4,760,88,20,"#DCE3E0","#F6F7F2",3)
            a.rect(8,8,752,80,16,None,"#7B8F90",2)
    else:
        raise ValueError(identity)
    return a


ENTRIES = (
    ("ui_card_item","content","palette","item-card prefab Surface, Art07AssetLibrary.cs InterfaceView"),
    ("ui_card_upgrade","content","palette","upgrade-card prefab Surface, Art07AssetLibrary.cs InterfaceView"),
    ("ui_chip_label","information","palette","fx_value_ticket / fx_cash_ticket Surface"),
    ("ui_focus_frame","action_feedback","white_alpha","dice-selection LocalFx material; target bracket / slot expand / face-change effects"),
    ("ui_panel_modal","content","palette","modal prefab Dialog; separate InputShield owns screen dimming"),
    ("ui_panel_plain","information","palette","legacy template compatibility; no current literal code consumer"),
    ("ui_progress_fill","information","white_alpha","probability bar and probability effect Filled images"),
    ("ui_progress_track","information","neutral_rgb","probability bar Track tinted UiKit.Panel"),
    ("ui_scroll_handle","information","neutral_rgb","legacy template compatibility; no current literal code consumer"),
    ("ui_slot_dice","information","palette","dice-slot prefab Surface"),
    ("ui_slot_inventory","information","palette","legacy template compatibility; no current literal code consumer"),
    ("ui_tooltip_frame","content","palette","tooltip prefab Surface"),
)


def nine_slice(im, size, border, ppu=200):
    """Preview only: source-pixel borders converted at canvas reference PPU 100."""
    l,b,r,t = border
    w,h = size
    dl,db,dr,dt = [round(v*100/ppu) for v in border]
    if w < dl+dr:
        dl=round(dl*w/(dl+dr));dr=w-dl
    if h < dt+db:
        dt=round(dt*h/(dt+db));db=h-dt
    xs, ys = [0,l,im.width-r,im.width], [0,t,im.height-b,im.height]
    xd, yd = [0,dl,w-dr,w], [0,dt,h-db,h]
    out = Image.new("RGBA",size)
    for yy in range(3):
        for xx in range(3):
            tile=im.crop((xs[xx],ys[yy],xs[xx+1],ys[yy+1]))
            if tile.width and tile.height and xd[xx+1]>xd[xx] and yd[yy+1]>yd[yy]:
                out.paste(tile.resize((xd[xx+1]-xd[xx],yd[yy+1]-yd[yy]),Image.Resampling.LANCZOS),(xd[xx],yd[yy]))
    return out


def tint(im, color):
    pixels=np.array(im).astype(np.uint16)
    rgb=np.array(rgba(color)[:3],dtype=np.uint16)
    pixels[:,:,:3]=pixels[:,:,:3]*rgb//255
    return Image.fromarray(pixels.astype(np.uint8))


def previews(images, records):
    font_path=ROOT/"DicePrototype/Assets/Game/Fonts/NotoSansSC-Bold.otf"
    font=ImageFont.truetype(str(font_path),18)
    small=ImageFont.truetype(str(font_path),15)
    sheet=Image.new("RGBA",(1500,1950))
    d=ImageDraw.Draw(sheet)
    for side,bg in enumerate(("deep","paper")):
        d.rectangle((side*750,0,side*750+749,1949),fill=rgba(bg))
        fg="paper" if side==0 else "ink"
        d.text((side*750+22,16),"DEEP FELT" if side==0 else "LIGHT PAPER",font=font,fill=rgba(fg))
        for i,record in enumerate(records):
            row,col=divmod(i,2);x=side*750+22+col*364;y=58+row*312
            d.text((x,y),record["id"],font=small,fill=rgba(fg))
            im=images[record["id"]]
            if "progress" in record["id"]:
                large=tint(im,"blue" if "fill" in record["id"] else "#305158").resize((320,24),Image.Resampling.LANCZOS)
                little=tint(im,"red" if "fill" in record["id"] else "#305158").resize((160,16),Image.Resampling.LANCZOS)
            else:
                big_size=(160,224) if "card_" in record["id"] else (320,200) if "modal" in record["id"] or "tooltip" in record["id"] else (152,124) if "slot_" in record["id"] else (280,64) if "chip" in record["id"] else (26,160) if "scroll" in record["id"] else (152,124)
                little_size=(80,112) if "card_" in record["id"] else (160,64) if "modal" in record["id"] or "tooltip" in record["id"] else (76,62) if "slot_" in record["id"] else (160,32) if "chip" in record["id"] else (12,64) if "scroll" in record["id"] else (76,62)
                # The scrollbar narrow test deliberately compresses corners as Unity
                # does when the RectTransform is smaller than the combined border.
                large=nine_slice(im,big_size,record["importer"]["border"])
                little=nine_slice(im,little_size,record["importer"]["border"])
                if record["tint_mode"] == "white_alpha":
                    large=tint(large,"focus");little=tint(little,"blue")
                elif "scroll" in record["id"]:
                    large=tint(large,"#8EA6A5");little=tint(little,"#8EA6A5")
            if "slot_" in record["id"] or "focus" in record["id"]:
                # Paint the object first, then the sprite, so this preview actually
                # exposes unwanted opacity over the physical-dice layer.
                cx=x+large.width//2;cy=y+28+large.height//2
                d.rounded_rectangle((cx-14,cy-14,cx+14,cy+14),radius=5,fill=rgba("paper"),outline=rgba("ink"),width=1)
                d.ellipse((cx-3,cy-3,cx+3,cy+3),fill=rgba("red"))
            sheet.alpha_composite(large,(x,y+28))
            little_x=x+large.width+12 if large.width+little.width+12<=340 else x
            little_y=y+30 if little_x>x else y+32+large.height+8
            sheet.alpha_composite(little,(little_x,little_y))
    sheet.save(OUT/"preview-deep-light-scale.png")


def main():
    OUT.mkdir(parents=True,exist_ok=True)
    records=[];images={}
    for identity,layer,tint_mode,consumers in ENTRIES:
        a=draw(identity);im=a.finish(identity,tint_mode);alpha=im.getchannel("A")
        edge=np.concatenate((np.array(alpha)[0],np.array(alpha)[-1],np.array(alpha)[:,0],np.array(alpha)[:,-1]))
        assert alpha.getbbox() and max(edge)==0,(identity,"clipped image edge")
        transparent_center=identity in ("ui_focus_frame","ui_slot_dice","ui_slot_inventory")
        if transparent_center:
            assert alpha.crop((32,32,im.width-32,im.height-32)).getextrema()==(0,0),(identity,"opaque slot center")
        if tint_mode=="white_alpha":
            px=np.array(im);assert np.all(px[px[:,:,3]>0,:3]==255),(identity,"tint contamination")
        progress="progress" in identity
        border=[48,0,48,0] if progress else [32,32,32,32]
        records.append(dict(id=identity,source=a.svg_path.relative_to(ROOT).as_posix(),export=a.png_path.relative_to(ROOT).as_posix(),source_sha256=sha(a.svg_path),sha256=sha(a.png_path),dimensions=list(im.size),layer=layer,tint_mode=tint_mode,transparent_center=transparent_center,alpha_bounds=list(alpha.getbbox()),alpha_range=list(alpha.getextrema()),edge_alpha_max=int(max(edge)),consumers=consumers,importer=dict(textureType="Sprite",spriteMode="Single",filterMode="Bilinear",wrapMode="Clamp",mipmapEnabled=False,alphaIsTransparency=True,sRGBTexture=True,compression="Uncompressed",pivot=[.5,.5],meshType="FullRect",border=border,pixelsPerUnit=200),image_usage=dict(type="Filled" if identity=="ui_progress_fill" else "Simple" if identity=="ui_progress_track" else "Sliced",preserveAspect=False,fillCenter=not transparent_center,color="runtime tint" if tint_mode!="palette" else "Color.white")))
        images[identity]=im
    previews(images,records)
    manifest=dict(schema="dicecraft-v071-full-surfaces-1",status="source_export_review_pending_independent_and_unity",editable_source=SCRIPT.relative_to(ROOT).as_posix(),script_sha256=sha(SCRIPT),tools=dict(python=platform.python_version(),Pillow=PIL_VERSION,numpy=np.__version__),palette=PALETTE,reference="docs/specs/v071/handoff/references/battle_master_v071.png",reference_sha256=sha(ROOT/"docs/specs/v071/handoff/references/battle_master_v071.png"),family_reference="tools/art_v071/build_surfaces.py",family_reference_sha256=sha(ROOT/"tools/art_v071/build_surfaces.py"),assets=records,previews=["ArtSource/Art071/Full/Surfaces/preview-deep-light-scale.png"],notes=["Asset geometry is newly drawn at HD resolution; old textures are neither enlarged nor sampled.","Item and upgrade use the same approved paper stock geometry; category identity remains runtime illustration/text.","Bilinear, Clamp, no mipmaps, FullRect; normal surfaces 512 square, PPU200 and L/B/R/T border32.","Progress strips 768x96 with L/B/R/T border48/0/48/0; Filled/Simple consumers must set preserveAspect=false; fillAmount remains runtime-owned.","Focus frame is white RGB with alpha; works with LocalFx material/Graphic tint. Slots have fully transparent inner area and no baked capacity or ordinal.","Palette sprites use Color.white; neutral/white-alpha sprites accept runtime tint. Reset fillCenter correctly on pooled Images.","Source preview is a Pillow slice simulation at canvas reference PPU100; it does not prove Unity runtime appearance or input behavior."])
    (OUT/"manifest.json").write_text(json.dumps(manifest,ensure_ascii=False,indent=2)+"\n",encoding="utf-8")
    print(json.dumps(dict(assets=len(records),manifest=str(OUT/"manifest.json"),preview=str(OUT/"preview-deep-light-scale.png"))))


if __name__=="__main__":
    main()
