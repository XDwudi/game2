from pathlib import Path
from collections import Counter
import datetime
import hashlib
import json
import sys
import xml.etree.ElementTree as ET

root=Path(__file__).resolve().parents[3]
out=root/'evidence/v05/review'
label=sys.argv[1]
destination=out/(label+'.json')
if destination.exists():raise SystemExit('Retain prior evidence: '+str(destination))
pairs=[p.split(':',1) for p in sys.argv[2:]]
rows=[]
for stem,xmlname in pairs:
    jp=root/'evidence/v05/unity'/(stem+'.json');xp=root/'evidence/v05/unity'/xmlname
    j=json.loads(jp.read_text(encoding='utf-8-sig'))['data']['result']
    x=ET.parse(xp).getroot();cases=x.findall('.//test-case')
    named={c.get('fullname'):c for c in cases}
    jsonnamed={c['FullName']:c for c in j['results']}
    counts=Counter(c.get('result') for c in cases)
    check={'uniqueXmlNames':len(named)==len(cases),'uniqueJsonNames':len(jsonnamed)==len(j['results']),
           'namesMatch':named.keys()==jsonnamed.keys(),'statusesMatch':all(jsonnamed[n]['Status']==c.get('result') for n,c in named.items()),
           'leafCountEqualsXmlTotal':len(cases)==int(x.get('total')),'leafCountEqualsJsonTotal':len(cases)==j['summary']['total'],
           'passedCountsMatch':counts['Passed']==int(x.get('passed'))==j['summary']['passed'],
           'failedCountsMatch':counts['Failed']==int(x.get('failed'))==j['summary']['failed'],
           'skippedCountsMatch':counts['Skipped']==int(x.get('skipped'))==j['summary']['skipped'],
           'positiveDuration':float(x.get('duration'))>0}
    assert all(check.values()),(stem,check)
    rows.append({'stem':stem,'xml':str(xp.relative_to(root)).replace('\\','/'),'json':str(jp.relative_to(root)).replace('\\','/'),
                 'xmlSha256':hashlib.sha256(xp.read_bytes()).hexdigest(),'jsonSha256':hashlib.sha256(jp.read_bytes()).hexdigest(),
                 'xmlAttributes':x.attrib,'jsonSummary':j['summary'],'checks':check,
                 'fixtureCounts':dict(Counter(c.get('classname') for c in cases)),
                 'failedTests':[{'name':c.get('fullname'),'message':c.findtext('failure/message'),'stackTrace':c.findtext('failure/stack-trace')} for c in cases if c.get('result')!='Passed'],
                 'testNames':list(named)})
history=[]
for before,after in zip(rows,rows[1:]):
    if before['stem'].split('-')[0]!=after['stem'].split('-')[0]:continue
    old=set(before['testNames']);new=set(after['testNames'])
    failed={c['name'] for c in before['failedTests']};still={c['name'] for c in after['failedTests']}
    history.append({'previous':before['stem'],'current':after['stem'],'removedTests':sorted(old-new),'addedTests':sorted(new-old),
                    'previousFailuresStillPresent':sorted(failed&new),'previousFailuresNowPassed':sorted((failed&new)-still)})
binding=json.loads((out/'independent-managed-run06-binding.json').read_text(encoding='utf-8'))
sourceChanges=[p for p,h in binding['after'].items() if hashlib.sha256((root/p).read_bytes()).hexdigest()!=h]
payload={'utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'reviewer':'/root/v05_spec_audit','reviewerLaunchedUnity':False,
         'scope':'Independent inspection of native Unity TestRunner XML and CLI report, source/PDB review separate. Not execution by reviewer.',
         'reports':rows,'history':history,'sourceChangesSinceIndependentManagedRun06':sourceChanges}
destination.write_text(json.dumps(payload,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'reports':[{'stem':r['stem'],**r['jsonSummary']} for r in rows],'sourceChangesSinceRun06':sourceChanges,'output':str(destination)},ensure_ascii=False))
