import hashlib,json,re,xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path
root=Path(__file__).resolve().parents[2]
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
results=[]
for jn,xn,count in [('editmode-07.json','nunit-20261029-023222.xml',339),('playmode-10.json','nunit-20261029-023256.xml',20)]:
    jp,xp=root/'evidence/unity'/jn,root/'evidence/unity'/xn
    j=read(jp);x=ET.parse(xp).getroot();cases=x.findall('.//test-case')
    assert len(cases)==count and j['summary']==dict(total=count,passed=count,failed=0,skipped=0,inconclusive=0)
    assert Counter(c.attrib['fullname'] for c in cases)==Counter(r['FullName'] for r in j['results'])
    assert all(c.attrib['result']=='Passed' for c in cases) and all(r['Status']=='Passed' for r in j['results'])
    names=[c.attrib['fullname'] for c in cases]
    coverage={}
    if jn.startswith('edit'):
        assert {c.attrib['name'] for c in cases if c.attrib['name'].startswith('GOLD-')}=={f'GOLD-S{i:02}' for i in range(1,53)}
        assert {c.attrib['name'] for c in cases if c.attrib['name'].startswith('TRACE-')}=={f'TRACE-S{i:02}' for i in range(1,53)}
        for label,n in [('FramePacerTests',14),('AtomicSaveStoreTests',11),('ScoreExplanationTests',57)]:
            selected=[name for name in names if label in name];assert len(selected)==n;coverage[label]=selected
    else:
        required=['PLAY_ExistingRunRollAndSubmitSaveFailureRetriesExactCandidateAndPresentsOnce','PLAY_PooledRefreshKeepsNodeIdentitiesAndResetsScrollInputAndSlider','PLAY_PooledPurchaseAndReadonlyFacesCannotRetainOldTargetsOrListeners','PLAY_ShopCapacityDiscountAndStudyUiRefreshAcrossTransactions','PLAY_ShopTargetCancellationAndPermanentDiceBoundariesPreserveOffers']
        assert all(any(n in name for name in names) for n in required)
        coverage['checked_regressions']=required
    results.append(dict(file=jn,xml=xn,total=count,passed=count,jsonSha256=sha(jp),xmlSha256=sha(xp),started=x.attrib['start-time'],ended=x.attrib['end-time'],coverage=coverage))
    print('PASS',jn,count,'actual cases, complete JSON/XML agreement')
spec=root/'docs/specs/P0_v0.4.md';original=Path('C:/Users/f/Downloads/骰子构筑游戏_AI开发规格_P0_v0.4.md')
assert sha(spec)==sha(original)
blocks=[]
for raw in re.findall(r'```json\s*\n(.*?)\n```',spec.read_text(encoding='utf-8-sig'),re.S):
    try:blocks.append(json.loads(raw))
    except json.JSONDecodeError:pass
assert read(root/'DicePrototype/Assets/Game/Data/P0Config.json') in blocks
assert read(root/'DicePrototype/Assets/Game/Data/GoldenScores.json')['cases'] in blocks
editor=read(root/'evidence/review/unity07-10-pdb-independent.json');player=read(root/'evidence/review/build08-pdb-independent.json')
assert editor['mismatches']==player['mismatches']==0
assert len(editor['documents'])==39 and len(editor['assemblies'])==5
assert len(player['documents'])==22 and len(player['assemblies'])==2
for manifest in [editor,player]:
    assert any(d['source'].replace('\\','/').endswith('/Runtime/FramePacer.cs') for d in manifest['documents'])
    assert any(d['source'].replace('\\','/').endswith('/Runtime/Verification/FramePacingExperiment.cs') for d in manifest['documents'])
build=read(root/'evidence/unity/build-08-deadline-experiment.json')
assert build['result']=='Succeeded' and build['errors']==build['warnings']==0
spike=read(root/'evidence/runs/pacing/spike-03-deadline/pacing-report.json')
assert spike['assemblyMvid']==next(a['mvid'] for a in player['assemblies'] if a['name']=='DiceCraft.Runtime')
histories=[]
for n in ['editmode-01.json','editmode-02-stale.json','playmode-01.json','playmode-02.json','playmode-03.json']:
    p=root/'evidence/unity'/n;r=read(p);assert r['summary']['failed']>0
    histories.append(dict(file=n,sha256=sha(p),summary=r['summary']))
(root/'evidence/review/unity07-10-independent-audit.json').write_text(json.dumps(dict(reviewerRanUnity=False,actualReports=results,sourcePdbManifests=['unity07-10-pdb-independent.json','build08-pdb-independent.json'],editorProjectDocuments=len(editor['documents']),playerProjectDocuments=len(player['documents']),build=build,actualSpikeRuntimeMvid=spike['assemblyMvid'],specSha256=sha(spec),goldenUnchanged=True,failuresRetained=histories),ensure_ascii=False,indent=2),encoding='utf-8')
print('PASS original config/goldens unchanged; Editor39 + Player22 PDB documents match current source; spike MVID matches Player; failed history retained')
