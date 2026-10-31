"""Synthetic build-gate integrity probes. No Unity or production writes; no git mutation.

Fixture DLL/PDB files are deliberately just bytes because this test targets the gate's
consumption of an audit proof, not the PE auditor itself. A mocked git read reports the
fixture root clean. These are negative checker tests, never game acceptance evidence.
"""
from pathlib import Path
import contextlib
import copy
import hashlib
import importlib.util
import io
import json
import os
import sys
import types
import xml.etree.ElementTree as ET

sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3]
OUT=Path(__file__).resolve().parent
LABEL=sys.argv[1] if len(sys.argv)>1 else 'gate-run01'
DEST=OUT/LABEL
assert DEST.resolve().parent==OUT.resolve()
assert not DEST.exists(), 'Preserve prior evidence'
DEST.mkdir()
source=ROOT/'tools/v06_delivery.py'
sys.path.insert(0,str(ROOT/'tools'))
spec=importlib.util.spec_from_file_location('v06_gate_under_review',source)
gate=importlib.util.module_from_spec(spec);spec.loader.exec_module(gate)
HEAD='1'*40
NAMES=['DiceCraft.Core','DiceCraft.Runtime','DiceCraft.EditMode','DiceCraft.PlayModeTests','DiceCraft.Editor']

def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def put(root,path,content):
    p=root/path;p.parent.mkdir(parents=True,exist_ok=True)
    p.write_text(content if isinstance(content,str) else json.dumps(content,ensure_ascii=False,indent=2),encoding='utf8')
    return p
def fixture(name):
    root=DEST/name;root.mkdir()
    config=put(root,gate.CONFIG,{'configVersion':'p0.6.0','saveSchemaVersion':4})
    golden=put(root,gate.GOLDEN,[])
    put(root,'DicePrototype/Packages/manifest.json',{})
    put(root,'DicePrototype/ProjectSettings/ProjectVersion.txt','m_EditorVersion: 6000.6.7f1')
    for mode,count in [('EditMode',775),('PlayMode',28)]:
        names=[mode+'.Fixture'+str(i) for i in range(count)]
        put(root,f'evidence/v06/unity/discovery-{mode}.json',{'mode':mode,'unity':'6000.6.7f1','names':names})
        xml=ET.Element('test-run',{'result':'Passed','testcasecount':str(count),'total':str(count),'passed':str(count),'failed':'0','skipped':'0','inconclusive':'0','start-time':'2026-10-31 13:00:00Z','end-time':'2026-10-31 13:00:10Z'})
        suite=ET.SubElement(xml,'test-suite',{'type':'Assembly','name':'DiceCraft.'+mode,'result':'Passed','total':str(count),'passed':str(count),'failed':'0','skipped':'0','inconclusive':'0'})
        props=ET.SubElement(suite,'properties');ET.SubElement(props,'property',{'name':'platform','value':mode})
        for full in names:ET.SubElement(suite,'test-case',{'fullname':full,'result':'Passed'})
        put(root,mode+'.xml',ET.tostring(xml,encoding='unicode'))
    put(root,gate.IDENTITY,{'releaseVersion':'p0.6.0','sourceCommit':HEAD,'contentConfigVersion':'p0.6.0','saveSchemaVersion':4,
                          'contentConfigSha256':sha(config),'goldenCaseSetSha256':sha(golden),'unityEditorVersion':'6000.6.7f1','backend':'Mono'})
    proof={'mismatches':0,'assemblies':[],'documents':[]}
    for index,name in enumerate(NAMES):
        dll=put(root,f'DicePrototype/Library/ScriptAssemblies/{name}.dll',name+' fixture dll')
        pdb=put(root,f'DicePrototype/Library/ScriptAssemblies/{name}.pdb',name+' fixture pdb')
        src=put(root,f'DicePrototype/Assets/Game/Fixture/{name}.cs','// '+name+' current fixture source')
        proof['assemblies'].append({'name':name,'mvid':str(index),'dllSha256':sha(dll),'pdbSha256':sha(pdb),'pdbMatchesDll':True,'projectDocuments':1,'dllLastWriteUtc':'2026-10-31T12:00:00Z'})
        proof['documents'].append({'assembly':name,'source':str(src),'expectedChecksum':sha(src),'currentChecksum':sha(src),'matches':True})
    put(root,'pdb.json',proof)
    files=sorted([{'path':p.relative_to(root).as_posix(),'sha256':sha(p)} for scope in gate.SCOPES for p in (root/scope).rglob('*') if p.is_file() and p.relative_to(root).as_posix()!=gate.IDENTITY],key=lambda f:f['path'])
    for mode in ('EditMode','PlayMode'):
        snapshot={'utc':'2026-10-31T13:00:00Z','assemblies':[{key:a[key] for key in ('name','mvid','dllSha256')} for a in proof['assemblies']],'files':files}
        put(root,mode+'.binding.json',{'schema':'v06-test-binding-1','report':{'path':mode+'.xml','sha256':sha(root/(mode+'.xml'))},'started':snapshot,'finished':copy.deepcopy(snapshot)})
    return root
def edit_xml(root,mode,mutate):
    path=root/(mode+'.xml');tree=ET.parse(path);mutate(tree.getroot());tree.write(path,encoding='unicode')
def edit_proof(root,mutate):
    path=root/'pdb.json';obj=json.loads(path.read_text(encoding='utf8'));mutate(obj);put(root,'pdb.json',obj)
def edit_binding(root,phase,mutate):
    path=root/'EditMode.binding.json';obj=json.loads(path.read_text(encoding='utf8'));mutate(obj[phase] if phase else obj);put(root,'EditMode.binding.json',obj)
def run(root):
    gate.ROOT=root
    original=gate.subprocess.check_output
    def git_read(cmd,**kwargs):
        if cmd[:3]==['git','rev-parse','HEAD']:return HEAD+'\n'
        if cmd[:3]==['git','status','--porcelain']:return ''
        raise AssertionError('Unexpected process call in synthetic probe: '+str(cmd))
    gate.subprocess.check_output=git_read
    try:
        with contextlib.redirect_stdout(io.StringIO()):gate.gate(types.SimpleNamespace(edit='EditMode.xml',play='PlayMode.xml',pdb='pdb.json'))
        return True,None
    except Exception as e:return False,type(e).__name__+': '+str(e)
    finally:gate.subprocess.check_output=original

results=[]
def probe(name,should_reject,mutation=lambda p:None):
    root=fixture(name);mutation(root);accepted,error=run(root)
    results.append({'name':name,'expected':'reject' if should_reject else 'accept','actual':'accept' if accepted else 'reject','matchedExpectation':accepted!=should_reject,'error':error})
probe('valid_complete_fixture',False)
probe('changed_discovered_name',True,lambda p:edit_xml(p,'EditMode',lambda x:x.findall('.//test-case')[0].set('fullname','Renamed.Test')))
probe('skipped_leaf',True,lambda p:edit_xml(p,'EditMode',lambda x:x.findall('.//test-case')[0].set('result','Skipped')))
probe('changed_compiled_source',True,lambda p:(p/'DicePrototype/Assets/Game/Fixture/DiceCraft.Core.cs').write_text('// changed source',encoding='utf8'))
probe('changed_current_dll',True,lambda p:(p/'DicePrototype/Library/ScriptAssemblies/DiceCraft.Core.dll').write_bytes(b'changed DLL'))
probe('old_test_loaded_mvid_after_recompile',True,lambda p:edit_binding(p,'started',lambda x:x['assemblies'][0].update(mvid='older-loaded-module')))
probe('failed_nested_suite_with_passed_leaves',True,lambda p:edit_xml(p,'EditMode',lambda x:x.find('test-suite').set('result','Failed')))
probe('namespaced_failed_case_hidden_from_counts',True,lambda p:edit_xml(p,'EditMode',lambda x:ET.SubElement(x.find('test-suite'),'{urn:unexpected}test-case',{'fullname':'Hidden.Failure','result':'Failed'})))
probe('wrong_root_tag',True,lambda p:edit_xml(p,'EditMode',lambda x:setattr(x,'tag','not-a-test-report')))
probe('mixed_platform_labels',True,lambda p:edit_xml(p,'EditMode',lambda x:ET.SubElement(x.find('.//properties'),'property',{'name':'platform','value':'PlayMode'})))
def duplicate(p):
    edit_proof(p,lambda x:x.update(assemblies=[copy.deepcopy(x['assemblies'][0]) for _ in range(5)],documents=x['documents'][:1]))
probe('five_duplicate_core_assemblies_omit_runtime_and_tests',True,duplicate)
probe('proof_omits_runtime_source_documents',True,lambda p:edit_proof(p,lambda x:x.update(documents=[d for d in x['documents'] if d['assembly']!='DiceCraft.Runtime'])))
probe('no_test_execution_binding',True,lambda p:(p/'EditMode.binding.json').unlink())
probe('wrong_test_report_sha_in_binding',True,lambda p:edit_binding(p,None,lambda x:x['report'].update(sha256='0'*64)))
probe('end_loaded_mvid_changed',True,lambda p:edit_binding(p,'finished',lambda x:x['assemblies'][0].update(mvid='different-at-end')))
probe('missing_loaded_runtime_assembly',True,lambda p:edit_binding(p,'started',lambda x:x.update(assemblies=[a for a in x['assemblies'] if a['name']!='DiceCraft.Runtime'])))
probe('duplicate_loaded_core_assembly',True,lambda p:edit_binding(p,'finished',lambda x:x.update(assemblies=[copy.deepcopy(x['assemblies'][0]) for _ in range(5)])))
probe('changed_source_during_test',True,lambda p:edit_binding(p,'finished',lambda x:x['files'][0].update(sha256='0'*64)))
probe('source_not_in_test_snapshot',True,lambda p:put(p,'DicePrototype/Assets/Untested.asset','new asset'))
probe('duplicate_pdb_document',True,lambda p:edit_proof(p,lambda x:x['documents'].append(copy.deepcopy(x['documents'][0]))))
probe('missing_pdb_current_source',True,lambda p:put(p,'DicePrototype/Assets/Game/Fixture/Untested.cs','// uncompiled'))
report={'scope':__doc__,'gateSha256':sha(source),'probeSha256':sha(Path(__file__)),'count':len(results),
        'expectationsMet':sum(r['matchedExpectation'] for r in results),'unexpectedAcceptances':[r['name'] for r in results if not r['matchedExpectation']], 'results':results}
(OUT/(LABEL+'.json')).write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
for r in results:print(('PASS' if r['matchedExpectation'] else 'GATE_WEAKNESS')+' '+r['name']+' => '+r['actual'])
print('Synthetic integrity expectations',report['expectationsMet'],'/',report['count'])
