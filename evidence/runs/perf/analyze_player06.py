"""Keep full Player06 data; diagnose observed clock lag and refresh-associated jitter."""
import csv, hashlib, json, math
from pathlib import Path
import numpy as np
ROOT=Path(__file__).resolve().parents[3]
OUT=Path(__file__).resolve().parent
RUN=ROOT/'evidence/runs/player/player-20261029-024615-1c6316db'
rows=list(csv.DictReader((RUN/'performance-frames.csv').open(encoding='utf-8-sig')))[1:]
a={k:np.array([float(r[k]) if r[k] else np.nan for r in rows]) for k in rows[0]}
report=json.loads((RUN/'player-report.json').read_text(encoding='utf-8-sig'))
def stats(values):
    v=np.asarray(values);v=np.sort(v[np.isfinite(v)])
    return None if not len(v) else dict(n=len(v),mean=float(v.mean()),p50=float(v[math.ceil(len(v)*.5)-1]),p95=float(v[math.ceil(len(v)*.95)-1]),max=float(v[-1]))

keys=['interval_ms','main_thread_ms','frame_start_delta_ms','present_called_delta_ms','frame_complete_delta_ms']
allocation_event=(a['gc_alloc_bytes']>10000).astype(float)
lags={}
for key in keys:
    lags[key]=[]
    for offset in range(-4,8):
        x=allocation_event[max(0,-offset):len(rows)+min(0,-offset)]
        y=a[key][max(0,offset):len(rows)+min(0,offset)]
        valid=np.isfinite(y)
        lags[key].append(dict(clock_row_offset_from_gc_row=offset,pearson=float(np.corrcoef(x[valid],y[valid])[0,1]),
                              allocation_event_clock_ms=stats(y[x>0])))

phases={'Roll':0.,'SkipVisual':.25,'Reorder':.5,'TogglePending_Refresh':.7,'ClearPending_Refresh':.85}
events=[]
for i in np.flatnonzero(allocation_event):
    if i<1 or i+5>=len(rows):continue
    t=a['elapsed_s'][i-1]
    distances={k:abs(((t+.017-v+.5)%1)-.5) for k,v in phases.items()}
    label=min(distances,key=distances.get)
    if t>179.9 and a['gc_alloc_bytes'][i]<100000:label='TailRefresh'
    events.append(dict(gc_row=int(i),operation_frame=int(a['frame'][i-1]),elapsed_s=t,inferred_phase=label,
                       late_operation_ms=a['interval_ms'][i-1],completed_main_ms=a['main_thread_ms'][i],gc_bytes=a['gc_alloc_bytes'][i],
                       present_operation_ms=a['present_called_delta_ms'][i+2],present_next_ms=a['present_called_delta_ms'][i+3],
                       present_pair_ms=a['present_called_delta_ms'][i+2]+a['present_called_delta_ms'][i+3],
                       complete_operation_ms=a['frame_complete_delta_ms'][i+2],
                       frame_start_operation_ms=a['frame_start_delta_ms'][i+4]))

groups={}
for label in phases:
    es=[e for e in events if e['inferred_phase']==label]
    fields=['late_operation_ms','completed_main_ms','gc_bytes','present_operation_ms','present_next_ms','present_pair_ms','complete_operation_ms','frame_start_operation_ms']
    groups[label]={k:stats([e[k] for e in es]) for k in fields}
    groups[label]['first30']={k:stats([e[k] for e in es if e['elapsed_s']<30]) for k in fields}
    groups[label]['last30']={k:stats([e[k] for e in es if e['elapsed_s']>=150]) for k in fields}
present_mask=np.zeros(len(rows),dtype=bool)
for e in events:present_mask[e['gc_row']+2]=True
after=np.roll(present_mask,1);after[0]=False
sources=['DicePrototype/Assets/Game/Runtime/GameApp.cs','DicePrototype/Assets/Game/Runtime/FramePacer.cs',
         'DicePrototype/Assets/Game/UI/UiKit.cs','DicePrototype/Assets/Game/Runtime/DiceTable.cs',
         'DicePrototype/Library/PackageCache/com.unity.ugui@eb824e41fbc7/Runtime/UGUI/UI/Core/CanvasUpdateRegistry.cs',
         'DicePrototype/Library/PackageCache/com.unity.ugui@eb824e41fbc7/Runtime/TMP/TMP_UpdateManager.cs',
         'DicePrototype/Library/PackageCache/com.unity.ugui@eb824e41fbc7/Runtime/TMP/TextMeshProUGUI.cs']
out=dict(run=str(RUN.relative_to(ROOT)),build_guid=report['environment']['buildGuid'],
         csv_sha256=hashlib.sha256((RUN/'performance-frames.csv').read_bytes()).hexdigest(),
         method='All original full-frame samples retained. Diagnostic event threshold >10KB. Labels inferred from source operation schedule with +17ms phase offset. Clock lags chosen from reported full cross-correlation table: MainThread GC+0, Present/Complete GC+2, FrameStart GC+4. These observed lags are not assumed universal Unity API guarantees.',
         correlations=lags,groups=groups,events=len(events),
         full_metrics={k:stats(a[k]) for k in keys},
         other_present_ms=stats(a['present_called_delta_ms'][~(present_mask|after)]),
         long_present={str(t):dict(all=int(np.count_nonzero(a['present_called_delta_ms']>t)),
                       assigned_operation=int(np.count_nonzero((a['present_called_delta_ms']>t)&present_mask))) for t in [16.7,18,18.752,20]},
         source_sha256={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in sources})
(OUT/'player06-analysis.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
with (OUT/'player06-operation-events.csv').open('w',newline='',encoding='utf-8') as f:
    w=csv.DictWriter(f,fieldnames=list(events[0]));w.writeheader();w.writerows(events)
for phase,g in groups.items():print(phase,{k:g[k] for k in ['late_operation_ms','present_operation_ms','present_next_ms','present_pair_ms','gc_bytes']})
print('other present',out['other_present_ms']);print('long present',out['long_present']);print('events',out['events'])
