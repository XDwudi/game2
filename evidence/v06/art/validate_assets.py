"""Read final v0.6 asset bytes and check spec coverage, alpha, sources and audio.

These checks do not claim Unity import, screenshot legibility, or subjective audio QA.
"""
from pathlib import Path
import hashlib
import json
import math
import re
import struct
import sys
import wave
import xml.etree.ElementTree as ET
from PIL import Image

ROOT=Path(__file__).resolve().parents[3]
ART=ROOT/'DicePrototype/Assets/Game/Art'
OUT=ROOT/'evidence/v06/art'


def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest()


def validate():
    cfg=json.loads((ROOT/'DicePrototype/Assets/Game/Data/P0Config.json').read_text(encoding='utf-8-sig'))
    manifest=json.loads((ART/'asset_map.json').read_text(encoding='utf-8-sig'))
    defined={}
    def walk(value):
        if isinstance(value,dict):
            if re.fullmatch('[CIOU][0-9]{2}',str(value.get('id',''))): defined[value['id']]=value['displayName']
            for v in value.values():walk(v)
        elif isinstance(value,list):
            for v in value:walk(v)
    walk(cfg)
    assert len(defined)==71
    assert manifest['configVersion']==cfg['configVersion']=='p0.6.0'
    assert len(manifest['icons'])==71
    assert {i['contentId'] for i in manifest['icons']}==set(defined)
    report={'configVersion':cfg['configVersion'],'configSha256':sha(ROOT/'DicePrototype/Assets/Game/Data/P0Config.json'),
            'specSha256':sha(ROOT/'docs/specs/P0_v0.6.md'),'assetMapSha256':sha(ART/'asset_map.json'),
            'generatorSha256':sha(ROOT/'art/scripts/build_symbols_v06.py'),'vectorLibrarySha256':sha(ROOT/'art/scripts/build_symbols.py'),
            'validatorSha256':sha(Path(__file__)),
            'icons':[],'enchantments':[],'audio':[],'pipComponents':[],
            'unityImport':'not_run_here','actualGameLegibility':'requires_root_player_verification','subjectiveAudio':'not_tested'}
    def inspect(path,source,size):
        im=Image.open(path);assert im.size==(size,size) and im.mode=='RGBA',(path,im.size,im.mode)
        alpha=im.getchannel('A'); assert alpha.getextrema()==(0,255),path
        assert not any(alpha.getpixel((x,0)) or alpha.getpixel((x,size-1)) for x in range(size)),path
        assert not any(alpha.getpixel((0,y)) or alpha.getpixel((size-1,y)) for y in range(size)),path
        bounds=alpha.getbbox(); assert bounds is not None and bounds[2]-bounds[0]>=size*.4 and bounds[3]-bounds[1]>=size*.4,path
        svg=ET.parse(source).getroot();assert svg.attrib['width']==str(size) and svg.attrib['height']==str(size)
        assert svg.attrib['viewBox']=='0 0 256 256';assert len(svg)>0
        assert all('image' not in element.tag and 'script' not in element.tag for element in svg.iter()),source
        return {'path':str(path.relative_to(ROOT)).replace('\\','/'),'source':str(source.relative_to(ROOT)).replace('\\','/'),
                'dimensions':[size,size],'mode':im.mode,'alphaBounds':bounds,'rgbaSha256':hashlib.sha256(im.tobytes()).hexdigest(),
                'pngSha256':sha(path),'svgSha256':sha(source)}
    for item in manifest['icons']:
        assert item['name']==defined[item['contentId']]
        row=inspect(ROOT/'DicePrototype'/item['path'],ROOT/item['source'],256)
        row['contentId']=item['contentId'];row['name']=item['name'];report['icons'].append(row)
    assert len({r['rgbaSha256'] for r in report['icons']})==71
    assert {e['id'] for e in manifest['enchantments']}=={'slotless','base10','mult2','submit1','roll1'}
    for item in manifest['enchantments']:
        badge=inspect(ROOT/'DicePrototype'/item['badgePath'],ROOT/item['badgeSource'],128)
        frame=inspect(ROOT/'DicePrototype'/item['framePath'],ROOT/item['frameSource'],256)
        # Frames must leave the centre transparent for card text/art.
        im=Image.open(ROOT/'DicePrototype'/item['framePath'])
        assert im.getpixel((128,128))[3]==0
        report['enchantments'].append({'id':item['id'],'shape':item['shape'],'badge':badge,'frame':frame})
    for audio in manifest['audio']:
        p=ROOT/'DicePrototype'/audio['path']
        with wave.open(str(p),'rb') as w:
            assert w.getnchannels()==1 and w.getsampwidth()==2 and w.getframerate()==44100
            n=w.getnframes(); values=struct.unpack('<'+'h'*n,w.readframes(n)); peak=max(map(abs,values)); rms=math.sqrt(sum(x*x for x in values)/n)
            assert 0<peak<32767 and rms>10
            assert values[0]==0 and abs(values[-1])<=1
            duration=n/44100;assert abs(duration-audio['seconds'])<1/44100
            if audio['id'].startswith('enchant_'): assert duration<=.25
            report['audio'].append({'id':audio['id'],'path':audio['path'],'sha256':sha(p),'duration':duration,'sampleRate':44100,'pcmBits':16,'channels':1,'peak':peak,'rms':rms,'startSample':values[0],'endSample':values[-1]})
    assert len(report['audio'])==16 and len({a['sha256'] for a in report['audio']})==16
    # Preserve exact pip geometry across the asset expansion.
    for path in sorted((ART/'Textures').glob('Pip_*.png')):
        im=Image.open(path);alpha=im.getchannel('A');seen=set();components=0
        for y in range(im.height):
            for x in range(im.width):
                if alpha.getpixel((x,y))>=128 and (x,y) not in seen:
                    components+=1;stack=[(x,y)];seen.add((x,y))
                    while stack:
                        a,b=stack.pop()
                        for p,q in [(a+1,b),(a-1,b),(a,b+1),(a,b-1)]:
                            if 0<=p<im.width and 0<=q<im.height and (p,q) not in seen and alpha.getpixel((p,q))>=128:
                                seen.add((p,q));stack.append((p,q))
        assert components==int(path.stem.rsplit('_',1)[1]),path
        report['pipComponents'].append({'file':path.name,'components':components,'sha256':sha(path)})
    assert len(report['pipComponents'])==12
    report['result']='passed_local_asset_checks'
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/'asset_validation.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
    print('V06_ASSET_BYTES_PASS',len(report['icons']),len(report['enchantments']),len(report['audio']),len(report['pipComponents']))


if __name__=='__main__': validate()
