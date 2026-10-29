import json,xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path
root=Path(__file__).resolve().parents[2];out=root/'evidence/review'
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
old=read(out/'build09-pdb-independent.json');new=read(out/'build10-pdb-independent.json')
old_docs={(d['assembly'],d['source']):d['currentChecksum'] for d in old['documents']}
new_docs={(d['assembly'],d['source']):d['currentChecksum'] for d in new['documents']}
assert old_docs.keys()==new_docs.keys() and new['mismatches']==0 and len(new_docs)==22
changed=[dict(assembly=k[0],source=k[1],before=old_docs[k],after=v) for k,v in new_docs.items() if old_docs[k]!=v]
assert len(changed)==1 and changed[0]['source'].replace('\\','/').endswith('/Runtime/GameApp.cs')
old_editor=read(out/'unity08-11-pdb-independent.json');editor=read(out/'unity08-12-pdb-independent.json')
assert editor['mismatches']==0 and len(editor['documents'])==39
before={(d['assembly'],d['source']):d['currentChecksum'] for d in old_editor['documents']}
assert all(before[(d['assembly'],d['source'])]==d['currentChecksum'] for d in editor['documents'] if d['assembly']!='DiceCraft.Runtime')
core=lambda manifest:next(a for a in manifest['assemblies'] if a['name']=='DiceCraft.Core')
assert core(old)['dllSha256']==core(new)['dllSha256']
j=read(root/'evidence/unity/playmode-12.json');x=ET.parse(root/'evidence/unity/nunit-20261029-025518.xml').getroot();cases=x.findall('.//test-case')
assert len(cases)==20 and all(c.attrib['result']=='Passed' for c in cases)
assert j['summary']==dict(total=20,passed=20,failed=0,skipped=0,inconclusive=0)
assert Counter(c.attrib['fullname'] for c in cases)==Counter(c['FullName'] for c in j['results'])
build=read(root/'evidence/unity/build-10.json');assert build['result']=='Succeeded' and build['errors']==build['warnings']==0
commit=(root/'.git/refs/heads/main').read_text().strip();assert commit=='f060098533d26e99e7eba7d0360ecfea78cbe517'
result=dict(sourceCommit=commit,reviewerRanUnity=False,build=build,changedProductionDocuments=changed,playerPdbDocuments=22,editorPdbDocuments=39,currentSourcePdbMismatches=0,actualPlayMode=20,inheritedEditMode=341,editModeReuseReason='Only GameApp.cs changed; Core DLL and every EditMode test/Core/AtomicSaveStore/FramePacer source unchanged. Affected GameApp/UI actual PlayMode suite reran 20/20.',playerAssemblies=new['assemblies'],player06FailureHistoryRetained=(out/'PLAYER06_REVIEW.md').is_file())
(out/'build10-binding-independent.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print('PASS f060098; only GameApp changed; current Player22/Editor39 PDB match; actual PlayMode12 20/20; EditMode08 341 source reuse justified.')
