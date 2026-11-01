"""Independent, read-only source/icon/report binding; no Unity execution."""
from pathlib import Path
import hashlib, json, shutil, subprocess, datetime, xml.etree.ElementTree as ET
ROOT=Path(__file__).resolve().parents[4]
OUT=Path(__file__).resolve().parent
def digest(path): return hashlib.sha256(path.read_bytes()).hexdigest()
paths=[ROOT/'docs/specs/v07/ART_SPEC.md', ROOT/'docs/specs/v07/dice_art_v0_7_manifest.json']
paths += list((ROOT/'DicePrototype/Assets/Game/UI').glob('*.cs'))
paths += [ROOT/'DicePrototype/Assets/Game/Runtime/GameApp.cs',ROOT/'DicePrototype/Assets/Game/Runtime/DiceTable.cs']
paths += list((ROOT/'DicePrototype/Assets/Game/Tests/PlayMode').glob('Art07*Tests.cs'))
paths += list((ROOT/'DicePrototype/Assets/Game/Core/Session').glob('GameSession*.cs'))
paths += [ROOT/'DicePrototype/Assets/Game/Core/Rules/ControllerRules.cs',ROOT/'DicePrototype/Assets/Game/Runtime/Verification/Art07Fixtures.cs']
records=[]
for p in paths:
    rel=p.relative_to(ROOT).as_posix()
    if p.suffix=='.cs':
        target=OUT/'source'/rel;target.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(p,target)
    records.append({'path':rel,'sha256':digest(p),'bytes':p.stat().st_size})
icon_dir=ROOT/'ArtSource/Art07/Review/icon-c07-c42-rework-20261031-142245'
art=json.loads((icon_dir/'report.json').read_text(encoding='utf-8'))
icon_checks=[]
for item in art['records']:
    current=ROOT/'DicePrototype'/item['runtimePath']
    source=ROOT/item['targetSourcePath']
    icon_checks.append({'id':item['id'],'runtimeSha256':digest(current),'matchesAuthorRuntime':digest(current)==item['runtimeSha256'],'sourceSha256':digest(source),'matchesAuthorSource':digest(source)==item['sourceSha256'],'layerCountReported':item['layerCount']})
    for p in [current,source,icon_dir/f"after-icon_{item['id'].lower()}.png"]:
        records.append({'path':p.relative_to(ROOT).as_posix(),'sha256':digest(p),'bytes':p.stat().st_size})
old=json.loads((ROOT/'evidence/v07/review/ui-scan/source-bindings.json').read_text(encoding='utf-8'))
old_records=old.get('files',old.get('sources',[]))
old_map={r['path']:r['sha256'] for r in old_records}
unchanged=[]
for item in art['unchanged69Exports']:
    found=list((ROOT/'DicePrototype/Assets/Art/Art07/ContentIcons').rglob('icon_'+item['id'].lower()+'.png'))
    p=found[0];rel=p.relative_to(ROOT).as_posix()
    unchanged.append({'id':item['id'],'matchesAuthor':digest(p)==item['sha256'],'matchesIndependentPrior':old_map.get(rel)==digest(p)})
reports=[]
for stamp in ['182910','183152']:
    p=ROOT/f'evidence/v07/unity/nunit-20261031-{stamp}.xml'
    doc=ET.parse(p).getroot();binding=json.loads(p.with_suffix('.binding.json').read_text(encoding='utf-8'))
    start=binding['started'];end=binding.get('finished',binding.get('ended',{}))
    reports.append({'path':p.relative_to(ROOT).as_posix(),'sha256':digest(p),'reportBindingMatches':binding['report']['sha256']==digest(p),'attributes':doc.attrib,'tests':[{'name':n.attrib['fullname'],'result':n.attrib['result'],'failure':n.findtext('failure/message')} for n in doc.findall('.//test-case')],'startedAssemblies':start['assemblies'],'endKeys':list(end),'bindingKeys':list(binding)})
data={'createdUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'scope':'Independent static rework review; no Unity, no source mutations','head':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),'workingTree':'uncommitted and changing; file-level bindings authoritative','files':records,'icons':icon_checks,'unchanged69':unchanged,'reports':reports}
(OUT/'source-bindings.json').write_text(json.dumps(data,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'files':len(records),'iconChecks':icon_checks,'unchanged69Author':sum(x['matchesAuthor'] for x in unchanged),'unchanged69Independent':sum(x['matchesIndependentPrior'] for x in unchanged),'reports':[{'path':r['path'],'result':r['attributes']['result'],'total':r['attributes']['total'],'binding':r['reportBindingMatches']} for r in reports]},ensure_ascii=False))
