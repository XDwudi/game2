"""Bind actual inspected edge PNGs and their source version; does not run Unity."""
from pathlib import Path
import datetime, hashlib, json, shutil, subprocess
from PIL import Image
ROOT=Path(__file__).resolve().parents[4]
OUT=Path(__file__).resolve().parent
CAP=ROOT/'artifacts/cache/v07-edge-screens-183152'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
binding_path=ROOT/'evidence/v07/unity/nunit-20261031-183152.binding.json'
binding=json.loads(binding_path.read_text(encoding='utf-8'))
runtime=next(x for x in binding['started']['assemblies'] if x['name']=='DiceCraft.Runtime')
source_map={x['path']:x['sha256'] for x in binding['started']['files']}
images=[]
for p in sorted(CAP.glob('*.png')):
    meta=json.loads(p.with_suffix('.json').read_text(encoding='utf-8'))
    with Image.open(p) as im: size=im.size; im.verify()
    images.append({'path':p.relative_to(ROOT).as_posix(),'sha256':sha(p),'metadataSha256':sha(p.with_suffix('.json')),'size':size,'reportedSizeMatches':list(size)==[meta['width'],meta['height']],'runtimeMvidMatches':meta['mvid']==runtime['mvid'],'metadata':{k:v for k,v in meta.items() if k!='savePath'},'actuallyViewed':True})
names=['UI/GameApp.Art07Results.cs','UI/Art07ScreenReveal.cs','UI/GameApp.Art07Guide.cs','UI/GameApp.Debug.cs','UI/GameApp.Controllers.cs','UI/GameApp.Art07.cs','UI/GameApp.Art07Interactions.cs','UI/GameApp.Display.cs','UI/GameApp.Art07Settings.cs','Runtime/GameApp.cs','Runtime/DiceTable.cs','Tests/PlayMode/Art07EdgeScreensPlayModeTests.cs','Tests/PlayMode/Art07InformationPlayModeTests.cs','Tests/PlayMode/Art07FxIntegrationPlayModeTests.cs']
files=[]
for name in names:
    p=ROOT/'DicePrototype/Assets/Game'/name;rel=p.relative_to(ROOT).as_posix();target=OUT/'source'/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
    files.append({'path':rel,'sha256':sha(p),'captureSourceSha256':source_map.get(rel),'equalsCaptureSource':sha(p)==source_map.get(rel)})
doc={'createdUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'input':'art0.7.0','head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'workingTree':'uncommitted; source copies and hashes bind static review','imageCount':len(images),'images':images,'files':files,'testReportSha256':sha(ROOT/'evidence/v07/unity/nunit-20261031-183152.xml'),'testBindingSha256':sha(binding_path),'captureRuntime':runtime,'captureBindingStable':binding['started']['files']==binding['finished']['files'] and binding['started']['assemblies']==binding['finished']['assemblies']}
(OUT/'evidence-bindings.json').write_text(json.dumps(doc,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'images':len(images),'allReportedSizesMatch':all(x['reportedSizeMatches'] for x in images),'allMvidsMatch':all(x['runtimeMvidMatches'] for x in images),'captureBindingStable':doc['captureBindingStable'],'sourceChangedSinceCapture':[x['path'] for x in files if not x['equalsCaptureSource']]},ensure_ascii=False))
