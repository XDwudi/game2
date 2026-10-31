"""Read-only independent verification of real Unity XML, loaded-module bindings and PDB audit."""
from pathlib import Path
import hashlib,json,sys,xml.etree.ElementTree as ET
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3];OUT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'tools'))
from quality_check import check_nunit
label,edit,play,pdb=sys.argv[1:5]
target=OUT/(label+'.json');assert not target.exists()
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
audit=read(ROOT/pdb)
modules=sorted([{k:a[k] for k in ('name','mvid','dllSha256')} for a in audit['assemblies']],key=lambda a:a['name'])
expected={'DiceCraft.Core','DiceCraft.Runtime','DiceCraft.EditMode','DiceCraft.PlayModeTests','DiceCraft.Editor'}
assert len(modules)==5 and {m['name'] for m in modules}==expected and audit['mismatches']==0
def source_snapshot():
    result={}
    for scope in ('Assets','Packages','ProjectSettings'):
        for p in (ROOT/'DicePrototype'/scope).rglob('*'):
            if not p.is_file():continue
            rel=p.relative_to(ROOT).as_posix()
            if any(x.endswith('~') for x in p.relative_to(ROOT).parts):continue
            if rel=='DicePrototype/Assets/Resources/BuildIdentity.json' or rel.startswith(('DicePrototype/Assets/InitTestScene','DicePrototype/Assets/Screenshots')):continue
            result[rel]=sha(p)
    return result
before=source_snapshot();reports=[];errors=[]
for mode,file in [('EditMode',edit),('PlayMode',play)]:
    path=ROOT/file;xml=ET.parse(path).getroot();cases=xml.findall('.//test-case')
    discovery=read(ROOT/f'evidence/v06/unity/discovery-{mode}.json')
    strict=check_nunit(ROOT,file,len(discovery['names']),discovery['names'])
    assert sorted(x.attrib['fullname'] for x in cases)==sorted(discovery['names'])
    assert len(cases)==int(xml.attrib['passed'])==int(xml.attrib['total'])==len(discovery['names'])
    assert {p.attrib['value'] for p in xml.findall('.//property') if p.attrib.get('name')=='platform'}=={mode}
    binding=read(path.with_suffix('.binding.json'))
    assert binding['report']=={'path':file,'sha256':sha(path)}
    phases=[]
    for phase in ('started','finished'):
        snap=binding[phase];actual=sorted(snap['assemblies'],key=lambda a:a['name'])
        if actual!=modules:errors.append(mode+' '+phase+' loaded module mismatch')
        bound={f['path']:f['sha256'] for f in snap['files']};assert len(bound)==len(snap['files'])
        changed=sorted(k for k in set(bound)|set(before) if bound.get(k)!=before.get(k))
        if changed:errors.append(mode+' '+phase+' source files differ: '+str(changed))
        phases.append({'phase':phase,'utc':snap['utc'],'loadedModulesMatchPdb':actual==modules,'files':len(bound),'changedSources':changed})
    reports.append({'mode':mode,'report':file,'reportSha256':sha(path),'bindingSha256':sha(path.with_suffix('.binding.json')),'cases':len(cases),'failed':int(xml.attrib['failed']),'skipped':int(xml.attrib['skipped']),'fullnamesMatchCompleteDiscovery':True,'phases':phases})
after=source_snapshot()
if before!=after:errors.append('Source changed during independent read')
result={'scope':'Real Unity reports read independently; reviewer did not start the Editor','pdbAudit':pdb,'pdbSha256':sha(ROOT/pdb),'projectCsharpDocuments':len(audit['documents']),'modules':modules,'reports':reports,'sourceStableDuringRead':before==after,'sourceFiles':len(before),'errors':errors,'status':'passed' if not errors else 'needs_rebind'}
target.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({k:result[k] for k in ('status','sourceFiles','errors')}));print([(r['mode'],r['cases']) for r in reports])
