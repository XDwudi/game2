"""Independent reconstruction of raw FTM chains; no new acceptance threshold."""
import csv,hashlib,json,math,statistics
from collections import Counter
from pathlib import Path
root=Path(__file__).resolve().parents[2]
run=root/'evidence/runs/player/player-20261029-025732-31091505'
rows=list(csv.DictReader((run/'performance-frames.csv').open(newline='',encoding='utf-8-sig')))
report=json.loads((run/'player-report.json').read_text(encoding='utf-8-sig'))
assert report['status']!='running'
seen=0;previous=[0,0,0];previous_frame=0;derived=[];mismatches=[]
for n,r in enumerate(rows):
    frame=int(r['frame']);frequency=int(r['cpu_timer_frequency_hz'])
    axes=[int(r[c]) for c in ['frame_start_timestamp','present_called_timestamp','frame_complete_timestamp']]
    received=int(r['gpu_sample_status'])!=0
    start=axes[0];status=0 if not received else 1 if start==0 else 2 if start==seen else 3 if seen and start<seen else 4
    ds=[None,None,None];ss=[0,0,0]
    if status==4:
        if previous_frame and frame!=previous_frame+1:status=5;previous=[0,0,0]
        for i,(x,p) in enumerate(zip(axes,previous)):
            ss[i]=0 if x==0 else 4 if frequency==0 else 1 if p==0 else 3 if x<=p else 2
            if ss[i]==2:ds[i]=(x-p)*1000/frequency
        seen=start;previous=[start,0 if ss[1]==3 else axes[1],0 if ss[2]==3 else axes[2]]
    else:previous=[0,0,0]
    previous_frame=frame
    if status!=int(r['timing_observation_status']):mismatches.append([n,'observationStatus'])
    for i,(dcol,scol) in enumerate(zip(['frame_start_delta_ms','present_called_delta_ms','frame_complete_delta_ms'],['frame_start_delta_status','present_delta_status','complete_delta_status'])):
        actual=float(r[dcol]) if r[dcol] else None
        if ss[i]!=int(r[scol]) or ((actual is None)!=(ds[i] is None)) or (actual is not None and abs(actual-ds[i])>1e-8):mismatches.append([n,dcol])
    derived.append((status,ss,ds))
clocks=report['performance']['independentFrameClocks'];metrics={}
for i,key in enumerate(['frameStart','presentCalled','frameComplete']):
    values=sorted(x[2][i] for x in derived[1:] if x[2][i] is not None)
    metric=dict(samples=len(values),expectedFrames=len(rows)-1,validFrameCoverage=len(values)/(len(rows)-1),p95=values[math.ceil(len(values)*.95)-1] if values else None,mean=statistics.mean(values) if values else None,max=values[-1] if values else None)
    for k,v in metric.items():
        expected=clocks[key][k]
        if (v is None)!=(expected is None) or (v is not None and not math.isclose(v,expected,rel_tol=1e-10,abs_tol=1e-10)):mismatches.append([key,k])
    metrics[key]=metric
for i,key in enumerate(['frameStartDeltaStatusCounts','presentDeltaStatusCounts','completeDeltaStatusCounts']):
    if dict(Counter(str(x[1][i]) for x in derived[1:]))!=clocks[key]:mismatches.append([key,'counts'])
if dict(Counter(str(x[0]) for x in derived[1:]))!=clocks['observationStatusCounts']:mismatches.append(['observations','counts'])
output=dict(reviewerRanUnity=False,newPerformanceThreshold=False,rawRows=len(rows),csvSha256=hashlib.sha256((run/'performance-frames.csv').read_bytes()).hexdigest(),metrics=metrics,mismatches=mismatches,limitations='FTM delayed adjacent observations may skip native records. Present call and frame completion are not physical scanout. Diagnostic P95 values do not replace the original LateUpdate verdict.')
(root/'evidence/review/player07-clocks-independent.json').write_text(json.dumps(output,indent=2)+'\n',encoding='utf-8')
print(json.dumps(dict(rawRows=len(rows),mismatches=mismatches,metrics=metrics),indent=2))
raise SystemExit(bool(mismatches))
