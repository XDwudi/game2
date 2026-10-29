import hashlib, json, re, xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path

root = Path(__file__).resolve().parents[2]
out = root / 'evidence/review'
checks = []
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def check(label, ok, **detail):
    checks.append(dict(id=label, passed=bool(ok), **detail))
    if not ok: raise AssertionError((label, detail))
    print('PASS', label)

pairs = [('editmode-03.json', 'nunit-20261029-003107.xml', 268), ('playmode-04.json', 'nunit-20261029-004024.xml', 13)]
for jn, xn, expected in pairs:
    jp, xp = root/'evidence/unity'/jn, root/'evidence/unity'/xn
    summary = json.loads(jp.read_text(encoding='utf-8-sig'))
    xml = ET.parse(xp).getroot(); cases = xml.findall('.//test-case')
    jnames = Counter(r['FullName'] for r in summary['results'])
    xnames = Counter(c.attrib['fullname'] for c in cases)
    check(jn + '_json_xml_same_cases', jnames == xnames and len(cases) == expected, expected=expected, actual=len(cases), jsonSha256=sha(jp), xmlSha256=sha(xp), started=xml.attrib['start-time'], ended=xml.attrib['end-time'])
    check(jn + '_all_pass_no_skip', summary['summary'] == dict(total=expected,passed=expected,failed=0,skipped=0,inconclusive=0) and all(r['Status']=='Passed' for r in summary['results']) and all(c.attrib['result']=='Passed' for c in cases))
    if jn.startswith('edit'):
        goldens = {c.attrib['name'] for c in cases if c.attrib['name'].startswith('GOLD-')}
        check('all_52_golden_ids_executed', goldens == {f'GOLD-S{i:02}' for i in range(1,53)}, count=len(goldens))
        check('null_adapter_and_real_save_cases_executed', any('GoldenJson_PreservesNullEmptyAndNonempty' in n for n in xnames) and sum('AtomicSaveStoreTests' in n for n in xnames)==11, saveCases=sum('AtomicSaveStoreTests' in n for n in xnames))
    else:
        required = ['PLAY_FirstNewRunSaveFailure', 'PLAY_ReadOnlyActionLog', 'PLAY_ShopPurchaseFooter', 'PLAY_RealEnterKey', 'PLAY_PreviousSubmission']
        check('all_five_ui_regressions_executed', all(any(r in n for n in xnames) for r in required), required=required)

histories=[]
for name in ['editmode-01.json','editmode-02-stale.json','playmode-01.json','playmode-02.json','playmode-03.json']:
    p=root/'evidence/unity'/name; report=json.loads(p.read_text(encoding='utf-8-sig'))
    histories.append(dict(file=name,summary=report['summary'],sha256=sha(p)))
check('failed_history_retained', any(x['summary']['failed']>0 for x in histories), history=histories)

spec = root/'docs/specs/P0_v0.4.md'
source = Path('C:/Users/f/Downloads/骰子构筑游戏_AI开发规格_P0_v0.4.md')
check('spec_original_unchanged', sha(spec)==sha(source), sha256=sha(spec))
blocks=[]
for raw in re.findall(r'```json\s*\n(.*?)\n```', spec.read_text(encoding='utf-8-sig'), re.S):
    try: blocks.append(json.loads(raw))
    except json.JSONDecodeError: pass
config=json.loads((root/'DicePrototype/Assets/Game/Data/P0Config.json').read_text(encoding='utf-8-sig'))
golden=json.loads((root/'DicePrototype/Assets/Game/Data/GoldenScores.json').read_text(encoding='utf-8-sig'))['cases']
check('config_equals_spec_json', config in blocks, sha256=sha(root/'DicePrototype/Assets/Game/Data/P0Config.json'))
check('golden_equals_spec_json', golden in blocks and len(golden)==52, sha256=sha(root/'DicePrototype/Assets/Game/Data/GoldenScores.json'))

pdb=json.loads((out/'unity-pdb-source-check.json').read_text(encoding='utf-8-sig'))
differences=[e for e in pdb if not e['matches']]
print('PDB latest-source differences:', len(differences))
for e in differences: print(e['assembly'], e['source'])
(out/'unity-report-independent-audit.json').write_text(json.dumps(dict(checks=checks,pdbCurrentDifferences=differences,reviewerRanUnity=False), ensure_ascii=False, indent=2), encoding='utf-8')
