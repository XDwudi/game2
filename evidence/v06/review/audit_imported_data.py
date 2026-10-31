from pathlib import Path
import hashlib
import json
from spec_input_audit import compare

HERE=Path(__file__).resolve().parent
ROOT=HERE.parents[2]
pairs=[('configuration',HERE/'input-json-0.json',ROOT/'DicePrototype/Assets/Game/Data/P0Config.json'),
       ('golden',HERE/'input-json-1.json',ROOT/'DicePrototype/Assets/Game/Data/GoldenScores.json')]
results=[]
for name,expected,actual in pairs:
    delta=compare(json.loads(expected.read_text(encoding='utf-8-sig')),json.loads(actual.read_text(encoding='utf-8-sig')))
    results.append({'name':name,'actual':str(actual),'sha256':hashlib.sha256(actual.read_bytes()).hexdigest(),
                    'differenceCount':len(delta),'differences':delta})
report={'scope':'Parsed original appendix JSON equality with imported production JSON.','pass':all(r['differenceCount']==0 for r in results),'results':results}
(HERE/'production-data-comparison.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=False))
raise SystemExit(0 if report['pass'] else 1)
