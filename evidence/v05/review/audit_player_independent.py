"""Read-only actual Player evidence audit; all output stays in review directory."""
import csv, hashlib, json, math, pathlib, statistics, sys
from collections import Counter

ROOT = pathlib.Path(__file__).resolve().parents[3]
RUN = ROOT / sys.argv[1]
OUT = ROOT / 'evidence/v05/review' / sys.argv[2]
if OUT.exists():
    raise SystemExit('Retain prior evidence; choose a new output name')
def load(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()
def lines(p):
    return [json.loads(s) for s in p.read_text(encoding='utf-8-sig').splitlines() if s.strip()]
def summary(values):
    a=sorted(values)
    return dict(samples=len(a), mean=statistics.mean(a), p50=a[math.ceil(len(a)*.5)-1], p95=a[math.ceil(len(a)*.95)-1], max=max(a))
r=load(RUN/'player-report.json'); p=r['performance']
t=load(ROOT/'evidence/v05/session/normal-run-trace.json'); n=lines(RUN/'normal-replay.jsonl'); d=lines(RUN/'debug24-replay.jsonl')
errors=[]
def check(ok,message):
    if not ok: errors.append(message)
keys=['kind','phase','actionSequence','submissionScore','stageScore','target','gold','stageIndex','rollsRemaining','remainingSubmissions']
check(len(n)==len(t['steps'])==r['normalReplay']['completedSteps'], 'normal trace count mismatch')
for i,(a,b) in enumerate(zip(n,t['steps'])):
    for k in keys: check(a[k]==b[k],f'normal step {i}: {k}')
    check(a['index']==i and a['actionSequence']==i+1,f'normal sequence {i}')
    check(a['savedStateMatches'] and a['renderedStateMatches'] and not a['isDebug'],f'normal state flags {i}')
check(d==r['debug24']['steps'],'debug JSONL/report mismatch')
check([x['sequence'] for x in d]==list(range(2,len(d)+2)),'debug sequence gap')
check([x['stage'] for x in d if x['kind']=='StartStage']==list(range(1,25)),'debug stage coverage')
check(len([x for x in d if x['kind']=='LeaveShop'])==23,'debug shop count')
check([x['stage'] for x in d if x['kind']=='ChooseChallenge' and x['risk']]==[2,5,8,11,14,17,20,23],'debug risk stages')
check(d[-1]['phase']=='RunWon','debug terminal')
with (RUN/'performance-frames.csv').open(encoding='utf-8-sig',newline='') as f: rows=list(csv.DictReader(f))
full=rows[1:]
check(len(full)==p['frameCount'],'raw frame count')
check(all(int(b['frame'])==int(a['frame'])+1 for a,b in zip(rows,rows[1:])),'Unity frame gap')
check(all(float(b['elapsed_s'])>float(a['elapsed_s']) for a,b in zip(rows,rows[1:])),'elapsed clock not monotonic')
check(all(abs((float(b['elapsed_s'])-float(a['elapsed_s']))*1000-float(b['interval_ms']))<1e-7 for a,b in zip(rows,rows[1:])),'raw interval clock mismatch')
metrics={}
for col,reported in [('interval_ms','frameInterval'),('main_thread_ms','mainThread'),('render_thread_ms','renderThread'),('frame_timing_cpu_ms','frameTimingCpu'),('gpu_ms','gpu'),('gc_alloc_bytes','gcAllocated'),('gc_used_bytes','gcUsed'),('ui_build_ms','uiRebuild'),('mono_used_bytes','monoUsed')]:
    vals=[float(x[col]) for x in full if x[col]!='']
    check(all(math.isfinite(x) and x>=0 for x in vals),f'invalid metric {col}')
    metrics[col]=summary(vals)
    for key,value in metrics[col].items(): check(math.isclose(value,p[reported][key],rel_tol=1e-12,abs_tol=1e-7),f'report metric {reported}/{key}')
for col,reported in [('frame_start_delta_ms','frameStart'),('present_called_delta_ms','presentCalled'),('frame_complete_delta_ms','frameComplete')]:
    vals=[float(x[col]) for x in full if x[col]!='']; metrics[col]=summary(vals)
    for key,value in metrics[col].items(): check(math.isclose(value,p['independentFrameClocks'][reported][key],rel_tol=1e-12,abs_tol=1e-7),f'independent clock {col}/{key}')
check(p['targetFrameMs']==16.7,'frozen threshold changed')
check(p['frameBudgetMet']==(metrics['interval_ms']['p95']<=16.7),'frame verdict disagrees')
normal=load(RUN/'normal-final-state.json'); final=load(RUN/'debug24-final-state.json')
check(normal['phase']=='RunFailed' and normal['stage']['stageIndex']==8,'normal terminal expected stage9 failure')
check(final['phase']=='RunWon' and final['stage']['stageIndex']==23,'debug terminal state')
save_checks=[]
for f in sorted(RUN.rglob('active-run.json*')):
    s=load(f);good=hashlib.sha256(s['payload'].encode('utf8')).hexdigest()==s['sha256'];check(good,f'save integrity {f}')
    save_checks.append(dict(file=str(f.relative_to(RUN)),fileSha256=sha(f),payloadSha256=s['sha256'],payloadDigestMatches=good))
check(sha(RUN/'normal-save/active-run.json')==r['normalSaveSha256BeforeStress'],'normal save modified by stress')
check(sha(ROOT/'evidence/v05/session/normal-run-trace.json')==r['traceSha256'],'trace hash mismatch')
for a in r['animationChecks']:check(a['passed'],'animation failure '+a['check'])
check(not r['unityErrors'] and not r['graphicsTimingWarnings'] and not r['rejectedScreenshots'],'recorded errors or black frames')
bindings=[dict(path=str(f.relative_to(ROOT)),sha256=sha(f)) for f in [RUN/'player-report.json',RUN/'normal-replay.jsonl',RUN/'debug24-replay.jsonl',RUN/'performance-frames.csv',RUN/'normal-final-state.json',RUN/'debug24-final-state.json']]
out=dict(scope='Independent read of actual Player evidence; reviewer did not run Player or Mac',run=str(RUN.relative_to(ROOT)),environment=r['environment'],buildIdentity=json.loads(r['buildIdentity']),probeAssemblyMvid=r['probeAssemblyMvid'],errors=errors,normalReplay=dict(steps=len(n),fieldsCompared=keys,terminal=normal['phase'],stage=normal['stage']['stageIndex']+1,isDebug=normal['isDebug']),debugReplay=dict(steps=len(d),commands=dict(Counter(x['kind'] for x in d)),terminal=final['phase'],stage=final['stage']['stageIndex']+1,isDebug=final['isDebug']),performance=dict(status=p['status'],rawRows=len(rows),fullRows=len(full),sampleSeconds=p['sampleSeconds'],threshold=16.7,budgetMet=p['frameBudgetMet'],metrics=metrics,rowsAbove16_7=sum(float(x['interval_ms'])>16.7 for x in full),gcCollections=p['gcCollections'],gpuMissingRows=sum(x['gpu_ms']=='' for x in full)),saveChecks=save_checks,localCheckpointVerification=load(RUN/'local-checkpoint-reload/verification.json'),animationChecks=len(r['animationChecks']),handbookPureReadChecks=r['handbookPureReadChecks'],bindings=bindings)
OUT.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps(dict(errors=errors,normalSteps=len(n),debugSteps=len(d),fullFrames=len(full),p95=metrics['interval_ms']['p95'],budgetMet=p['frameBudgetMet'],output=str(OUT)),ensure_ascii=False))
raise SystemExit(bool(errors))
