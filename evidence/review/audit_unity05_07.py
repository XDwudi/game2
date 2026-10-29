import hashlib,json,re,xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path
root=Path(__file__).resolve().parents[2]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
results=[]
for jn,xn,count in [('editmode-05.json','nunit-20261029-011333.xml',325),('playmode-07.json','nunit-20261029-011413.xml',18)]:
    jp,xp=root/'evidence/unity'/jn,root/'evidence/unity'/xn
    j=read(jp);x=ET.parse(xp).getroot();cases=x.findall('.//test-case')
    assert len(cases)==count and j['summary']==dict(total=count,passed=count,failed=0,skipped=0,inconclusive=0)
    assert Counter(c.attrib['fullname'] for c in cases)==Counter(r['FullName'] for r in j['results'])
    assert all(c.attrib['result']=='Passed' for c in cases) and all(r['Status']=='Passed' for r in j['results'])
    if jn.startswith('edit'):
        assert {c.attrib['name'] for c in cases if c.attrib['name'].startswith('GOLD-')}=={f'GOLD-S{i:02}' for i in range(1,53)}
        assert {c.attrib['name'] for c in cases if c.attrib['name'].startswith('TRACE-')}=={f'TRACE-S{i:02}' for i in range(1,53)}
        assert sum('ScoreExplanationTests' in c.attrib['fullname'] for c in cases)==57
    else:
        required=['PLAY_ShopCapacityDiscountAndStudyUiRefreshAcrossTransactions','PLAY_ShopTargetCancellationAndPermanentDiceBoundariesPreserveOffers']
        assert all(any(n in c.attrib['fullname'] for c in cases) for n in required)
    results.append(dict(file=jn,xml=xn,total=count,passed=count,jsonSha256=sha(jp),xmlSha256=sha(xp),started=x.attrib['start-time'],ended=x.attrib['end-time']))
    print('PASS',jn,count,'actual nonzero test cases, JSON/XML agree')
spec=root/'docs/specs/P0_v0.4.md';original=Path('C:/Users/f/Downloads/骰子构筑游戏_AI开发规格_P0_v0.4.md')
assert sha(spec)==sha(original)
blocks=[]
for raw in re.findall(r'```json\s*\n(.*?)\n```',spec.read_text(encoding='utf-8-sig'),re.S):
    try:blocks.append(json.loads(raw))
    except json.JSONDecodeError:pass
assert read(root/'DicePrototype/Assets/Game/Data/P0Config.json') in blocks
assert read(root/'DicePrototype/Assets/Game/Data/GoldenScores.json')['cases'] in blocks
pdb=read(root/'evidence/review/unity05-07-pdb-independent.json')
assert pdb['mismatches']==0 and len(pdb['documents'])==35 and len(pdb['assemblies'])==5
print('PASS original spec/config/goldens unchanged; 35 current project PDB checksums match')
histories=[]
for n in ['editmode-01.json','editmode-02-stale.json','playmode-01.json','playmode-02.json','playmode-03.json']:
    p=root/'evidence/unity'/n;r=read(p);assert r['summary']['failed']>0
    histories.append(dict(file=n,sha256=sha(p),summary=r['summary']))
(root/'evidence/review/unity05-07-independent-audit.json').write_text(json.dumps(dict(reviewerRanUnity=False,actualReports=results,sourcePdbManifest='unity05-07-pdb-independent.json',specSha256=sha(spec),goldenUnchanged=True,failuresRetained=histories),ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS failure histories retained')
