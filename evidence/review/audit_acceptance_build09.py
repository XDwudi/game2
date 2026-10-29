import hashlib,json,re,xml.etree.ElementTree as ET
from pathlib import Path
root=Path(__file__).resolve().parents[2];out=root/'evidence/review'
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
edit=ET.parse(root/'evidence/unity/nunit-20261029-024239.xml').getroot()
play=ET.parse(root/'evidence/unity/nunit-20261029-024334.xml').getroot()
ec=edit.findall('.//test-case');pc=play.findall('.//test-case')
assert len(ec)==341 and len(pc)==20 and all(c.attrib['result']=='Passed' for c in ec+pc)
summary=read(root/'evidence/unity/playmode-11.json')
assert {c.attrib['fullname'] for c in pc}=={c['FullName'] for c in summary['results']}
assert summary['summary']==dict(total=20,passed=20,failed=0,skipped=0,inconclusive=0)
edit_summary=read(root/'evidence/unity/editmode-08.json')
assert {c.attrib['fullname'] for c in ec}=={c['FullName'] for c in edit_summary['results']}
assert edit_summary['summary']==dict(total=341,passed=341,failed=0,skipped=0,inconclusive=0)
flow={f'{letter}{i:02}':[] for letter,n in [('A',12),('B',20),('C',15),('D',8),('E',15)] for i in range(1,n+1)}
for c in ec:
 name=c.attrib['name']
 if name.startswith(('FLOW_','FLOW-')):
  for ident in re.findall(r'(?<![A-Za-z0-9])[A-E]\d\d(?!\d)',name):
   if ident in flow:flow[ident].append(c.attrib['fullname'])
for ident,fragment in [('D01','("none",False,False,'),('D02','("smooth",False,False,'),('D03','("rough",False,False,'),('D04','("none",True,False,')]:
 flow[ident]=[c.attrib['fullname'] for c in ec if 'Probability_FiveContractVectors' in c.attrib['fullname'] and fragment in c.attrib['fullname']]
assert len(flow)==70 and all(flow.values()),[i for i,names in flow.items() if not names]
data=root/'DicePrototype/Assets/Game/Data'
cfg=read(data/'P0Config.json');ids={v['id'] for category in ['controllers','consumables','services','diceUpgrades'] for v in cfg[category]}
mapping=read(root/'DicePrototype/Assets/Game/Art/asset_map.json')
assert len(ids)==38 and {i['contentId'] for i in mapping['icons']}==ids
icons=[]
for icon in mapping['icons']:
 p=root/'DicePrototype'/icon['path'];s=root/icon['source'];assert p.is_file() and s.is_file()
 icons.append(dict(id=icon['contentId'],png=str(p.relative_to(root)),pngSha256=sha(p),editableSource=str(s.relative_to(root)),sourceSha256=sha(s)))
assets=read(root/'evidence/art/asset_validation.json')
assert len(assets['png'])==38 and len(assets['pipComponents'])==12 and len(assets['audio'])==10
assert all(sha(root/a['path'])==a['sha256'] for a in assets['hashes'])
pdb=read(out/'build09-pdb-independent.json');assert pdb['mismatches']==0 and len(pdb['documents'])==22
editorpdb=read(out/'unity08-11-pdb-independent.json');assert editorpdb['mismatches']==0 and len(editorpdb['documents'])==39
result=dict(reviewerRanUnity=False,flowIdMapping=flow,flowCount=70,editReport='nunit-20261029-024239.xml',playReport='nunit-20261029-024334.xml',editCount=341,playCount=20,contentAssetMapping=icons,assetSourceHashesStillMatch=assets['hashes'],currentPlayerPdbAll22Match=True,currentEditorPdbAll39Match=True,buildReport=read(root/'evidence/unity/build-09.json'),acceptanceContract='docs/tasks/P0-IMPLEMENTATION.md',notes=['Flow mapping names are execution traceability, not proof solely from names; source assertions were independently reviewed during the task.','Asset report hashes bind source BLEND and exported FBX; the reviewer did not launch Blender in this final check.','No human experience or mobile acceptance is inferred.'])
(out/'acceptance-build09-independent.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS 70 original flow IDs map to actual passed cases; Edit341/Play20; 38 content icons plus editable sources; 5 model/source hashes; current Editor39/Player22 PDB documents. Human experience pending.')
