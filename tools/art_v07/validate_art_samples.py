"""Measure source/export samples. This never substitutes for in-game visual QA."""
from pathlib import Path
from PIL import Image,ImageChops
import json,zipfile,io,hashlib,xml.etree.ElementTree as ET,datetime,sys,PIL
ROOT=Path(__file__).resolve().parents[2];SRC=ROOT/'ArtSource/Art07';OUT=ROOT/'DicePrototype/Assets/Art/Art07';checks=[];inputs=[]
def check(name,ok,detail):
 checks.append({'check':name,'passed':bool(ok),'detail':detail})
 if not ok:print('FAIL',name,detail)
assets={}
for name in ['samples_manifest.json','dice_texture_manifest.json','public_symbols_manifest.json','content_manifest.json']:
 for asset in json.loads((SRC/name).read_text(encoding='utf8'))['assets']:assets[asset['assetId']]=asset
for asset in assets.values():
  path=ROOT/'DicePrototype'/asset['runtimePath'];im=Image.open(path).convert('RGBA');check(asset['assetId']+'/size',list(im.size)==asset['dimensionsPx'],list(im.size));src=ROOT/asset['sourcePath'];check(asset['assetId']+'/kra_exists',src.exists(),str(src.relative_to(ROOT)))
  if not src.exists():continue
  inputs.append({'assetId':asset['assetId'],'sourceSha256':hashlib.sha256(src.read_bytes()).hexdigest(),'runtimeSha256':hashlib.sha256(path.read_bytes()).hexdigest()})
  with zipfile.ZipFile(src) as z:
   xml=ET.fromstring(z.read('maindoc.xml'));layers=xml.findall('.//{*}layer');merged=Image.open(io.BytesIO(z.read('mergedimage.png'))).convert('RGBA');check(asset['assetId']+'/layers',len(layers)>=1,len(layers));check(asset['assetId']+'/source_alpha',ImageChops.difference(im.getchannel('A'),merged.getchannel('A')).getbbox() is None,'actual Krita merged image alpha equals PNG')
   diff=ImageChops.difference(im.convert('RGB'),merged.convert('RGB'));diff.putalpha(im.getchannel('A'));extreme=max(v for pair in diff.convert('RGB').getextrema() for v in pair);opaque=Image.new('RGB',im.size);opaque.paste(diff,(0,0),im.getchannel('A'));check(asset['assetId']+'/source_visible_pixels',not opaque.getbbox(),{'maxVisibleDiff':max(v for pair in opaque.getextrema() for v in pair)})
  if asset['assetId'].startswith('icon_'):
   bbox=im.getchannel('A').getbbox();check(asset['assetId']+'/safe_border',bbox[0]>=24 and bbox[1]>=24 and bbox[2]<=232 and bbox[3]<=232,bbox)
   small=im.resize((64,64),Image.Resampling.NEAREST).resize((256,256),Image.Resampling.NEAREST);check(asset['assetId']+'/logical_grid',ImageChops.difference(im.getchannel('A'),small.getchannel('A')).getbbox() is None,'64px silhouette grid')

def components(image):
 a=image.getchannel('A');seen=set();n=0
 for y in range(a.height):
  for x in range(a.width):
   if (x,y) in seen or a.getpixel((x,y))==0:continue
   n+=1;stack=[(x,y)];seen.add((x,y))
   while stack:
    xx,yy=stack.pop()
    for dx,dy in [(1,0),(-1,0),(0,1),(0,-1)]:
     q=(xx+dx,yy+dy)
     if 0<=q[0]<a.width and 0<=q[1]<a.height and q not in seen and a.getpixel(q)>0:seen.add(q);stack.append(q)
 return n
for col in ['red','blue']:
 for pip in range(1,7):
  image=Image.open(OUT/f'Dice/Textures/dice_pips_{col}_{pip}.png');count=components(image);check(f'pips/{col}/{pip}',count==pip,count)
g=json.loads((SRC/'Dice/Review/generation.json').read_text());check('dice/triangles',g['bodyTriangles']<=1200,g['bodyTriangles']);check('dice/unit_size',g['bodyDimensions']==[1,1,1],g['bodyDimensions']);check('dice/fragments',g['fragmentTriangles']<=800,g['fragmentTriangles'])
m=json.loads((OUT/'Dice/Data/dice_face_map.json').read_text());check('dice/opposites',all(tuple(-v for v in f['unityNormal'])==tuple(m['faces'][6-i]['unityNormal']) for i,f in enumerate(m['faces'],1)),[f['oppositeFaceId'] for f in m['faces']]);check('dice/markers',len({f['markerObject'] for f in m['faces']})==6,6)
content=json.loads((SRC/'content_manifest.json').read_text(encoding='utf8'))['assets'];plan=json.loads((ROOT/'docs/specs/v07/dice_art_v0_7_manifest.json').read_text(encoding='utf8'))
check('content/stable_ids',sorted(a['contentId'] for a in content)==sorted(a['id'] for a in plan['contentResources']),[a['contentId'] for a in content]);hashes=[hashlib.sha256((ROOT/'DicePrototype'/a['runtimePath']).read_bytes()).hexdigest() for a in content];check('content/unique_exports',len(set(hashes))==71,len(set(hashes)))
result={'stage':'offline_source_and_export_only','unityVerified':False,'pythonVersion':sys.version,'pillowVersion':PIL.__version__,'checkerSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'assetInputs':inputs,'passed':all(c['passed'] for c in checks),'passedCount':sum(c['passed'] for c in checks),'total':len(checks),'checks':checks}
stamp=datetime.datetime.now().strftime('%Y%m%d-%H%M%S');(SRC/'Review').mkdir(exist_ok=True);(SRC/f'Review/sample-validation-{stamp}.json').write_text(json.dumps(result,indent=2),encoding='utf8');(SRC/'sample_validation.json').write_text(json.dumps(result,indent=2),encoding='utf8');print(json.dumps({k:v for k,v in result.items() if k not in ['checks','assetInputs']}));raise SystemExit(0 if result['passed'] else 1)
