"""Exercise production ValidateGate without running Unity or BuildPlayer."""
from pathlib import Path
import hashlib,json,shutil,subprocess,sys
ROOT=Path(__file__).resolve().parents[3];OUT=Path(__file__).resolve().parent
LABEL=sys.argv[1] if len(sys.argv)>1 else 'gate-consumer-run01';DEST=OUT/LABEL
assert not DEST.exists();DEST.mkdir()
base=OUT/'gate-run03/valid_complete_fixture'
exe=OUT/'GateConsumer/bin/Debug/net10.0/GateConsumer.dll'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def alter(root,fn):
    p=root/'evidence/v06/unity/build-gate.json';j=json.loads(p.read_text(encoding='utf8'));fn(j);p.write_text(json.dumps(j),encoding='utf8')
def drop(root,path):
    alter(root,lambda j:j.update(files=[f for f in j['files'] if f['path']!=path]));(root/path).unlink()
results=[]
def probe(name,reject,mutation=lambda p:None):
    root=DEST/name;shutil.copytree(base,root);mutation(root)
    output=subprocess.check_output(['dotnet',str(exe),str(root)],text=True,encoding='utf8',errors='replace');r=json.loads(output)
    results.append({'name':name,'expected':'reject' if reject else 'accept','actual':'accept' if r['accepted'] else 'reject','matchedExpectation':r['accepted']!=reject,'error':r['error']})
probe('complete_generated_gate',False)
probe('changed_asset_rejected',True,lambda p:(p/'DicePrototype/Packages/manifest.json').write_text('{"changed":true}',encoding='utf8'))
probe('added_asset_rejected',True,lambda p:(p/'DicePrototype/Assets/added.asset').write_text('new',encoding='utf8'))
probe('missing_source_manifest_entry',True,lambda p:alter(p,lambda j:j.update(files=[f for f in j['files'] if not f['path'].endswith('DiceCraft.Core.cs')])))
probe('missing_nunit_report_and_manifest_entry',True,lambda p:drop(p,'EditMode.xml'))
probe('missing_execution_binding_and_manifest_entry',True,lambda p:drop(p,'EditMode.binding.json'))
probe('missing_pdb_proof_and_manifest_entry',True,lambda p:drop(p,'pdb.json'))
probe('duplicate_bound_source',True,lambda p:alter(p,lambda j:j['files'].append(j['files'][0])))
def escape(p):
    external=DEST/'outside.txt';external.write_text('outside fixture',encoding='utf8')
    alter(p,lambda j:j['files'].append({'path':'../outside.txt','sha256':sha(external)}))
probe('parent_path_escape_rejected',True,escape)
report={'scope':__doc__,'prototypeBuildSha256':sha(ROOT/'DicePrototype/Assets/Game/Editor/PrototypeBuild.cs'),'probeSha256':sha(Path(__file__)),'count':len(results),'expectationsMet':sum(r['matchedExpectation'] for r in results),'results':results}
(OUT/(LABEL+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
for r in results:print(('PASS' if r['matchedExpectation'] else 'GATE_WEAKNESS')+' '+r['name']+' => '+r['actual'])
