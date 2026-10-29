import csv,hashlib,json,math
from pathlib import Path
root=Path(__file__).resolve().parents[2]
base=root/'evidence/runs/pacing/spike-04-lifecycle'
report_path=base/'pacing-report.json'
report=json.loads(report_path.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
checks=[];modes=[]
def check(name,value):
    checks.append(dict(name=name,passed=bool(value)))
check('successful experiment explicitly not AC-PERF',report['status']=='experiment_completed_not_AC_PERF' and report['failure'] is None)
check('all nine post-sampling lifecycle checks reported honestly',len(report['lifecycleChecks'])==9 and len(set(report['lifecycleChecks']))==9 and 'not OS window focus changes' in report['lifecycleLimit'])
env=report['restoredEnvironment']
check('original production pacer and native settings restored',report['restorationConfirmedBeforeExit'] and env['mode']=='Windowed' and env['targetFrameRate']==-1 and env['vSyncCount']==0 and env['deadlineFramePacerInstalled'])
check('both intended modes in original order',[m['name'] for m in report['modes']]==['windowed-software-60','windowed-deadline-60'])
for index,mode in enumerate(report['modes']):
    path=base/(mode['name']+'.csv');rows=list(csv.DictReader(path.open(encoding='utf-8-sig',newline='')))
    check(mode['name']+' row count and nonzero duration',len(rows)==mode['rows'] and len(rows)>1000 and mode['completed'] and mode['sampleSeconds']>=20)
    check(mode['name']+' stable visible full capture',all(r['focused']=='1' and r['timing_status']=='4' for r in rows) and mode['observerBufferGrowths']==mode['modeMismatchRows']==0)
    check(mode['name']+' consecutive observed Unity frames',all(int(b['unity_frame'])-int(a['unity_frame'])==1 for a,b in zip(rows,rows[1:])))
    check(mode['name']+' expected caps at both ends',all(e['targetFrameRate']==(60 if index==0 else -1) and e['deadlineFramePacerInstalled']==bool(index) and e['vSyncCount']==0 and e['width']==1920 and e['height']==1080 for e in [mode['environment'],mode['endingEnvironment']]))
    stats={}
    for raw,col,key in [('elapsed_s','late_interval_ms','lateUpdateInterval'),('frame_start_timestamp','frame_start_delta_ms','frameStartInterval'),('present_called_timestamp','present_called_delta_ms','presentCalledInterval'),('frame_complete_timestamp','frame_complete_delta_ms','frameCompleteInterval')]:
        parse=float if raw=='elapsed_s' else int
        factor=1000 if raw=='elapsed_s' else 1000/report['cpuTimerFrequencyHz']
        values=[(parse(b[raw])-parse(a[raw]))*factor for a,b in zip(rows,rows[1:])]
        check(mode['name']+'/'+key+' every raw delta matches CSV',rows[0][col]=='' and all(x>0 and abs(x-float(r[col]))<1e-8 for x,r in zip(values,rows[1:])))
        a=sorted(values);computed=dict(samples=len(a),meanMs=sum(a)/len(a),p50Ms=a[math.ceil(len(a)*.5)-1],p95Ms=a[math.ceil(len(a)*.95)-1],maxMs=a[-1])
        check(mode['name']+'/'+key+' full report statistics match',all(abs(mode[key][k]-v)<1e-8 for k,v in computed.items()))
        computed.update(minMs=a[0],over16_7Count=sum(x>16.7 for x in a),effectiveHz=1000/computed['meanMs'])
        stats[key]=computed
    modes.append(dict(name=mode['name'],csvSha256=sha(path),rows=len(rows),metrics=stats))
result=dict(kind='independent-deadline-idle-review',reviewerRanUnity=False,reportSha256=sha(report_path),buildGuid=report['buildGuid'],runtimeMvid=report['assemblyMvid'],checks=checks,failedChecks=sum(not x['passed'] for x in checks),modes=modes,conclusion='All axes improve; only LateUpdate P95 is <=16.7ms. This idle experiment is not AC-PERF and does not establish physical scanout cadence.')
(root/'evidence/review/pacing-lifecycle-independent.json').write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(checks=len(checks),failed=result['failedChecks'],p95={m['name']:{k:v['p95Ms'] for k,v in m['metrics'].items()} for m in modes}),indent=2))
raise SystemExit(bool(result['failedChecks']))
