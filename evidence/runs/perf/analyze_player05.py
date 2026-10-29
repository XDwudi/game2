"""Read-only post-pooling phase diagnosis; never filters the acceptance dataset."""
from pathlib import Path
import csv, hashlib, json, math
import numpy as np

ROOT = Path(__file__).resolve().parents[3]
OUT = Path(__file__).resolve().parent
RUN = ROOT / 'evidence/runs/player/player-20261029-015104-31d8ee23'
raw = list(csv.DictReader((RUN/'performance-frames.csv').open(encoding='utf-8-sig')))
rows = raw[1:]  # Same initial partial interval already excluded by the formal probe.
a = {k: np.array([float(r[k]) if r[k] else np.nan for r in rows]) for k in rows[0]}
report = json.loads((RUN/'player-report.json').read_text(encoding='utf-8-sig'))

def stats(v):
    v = np.sort(np.asarray(v)[np.isfinite(v)])
    return None if not len(v) else dict(n=len(v), mean=float(v.mean()), p50=float(v[math.ceil(len(v)*.5)-1]),
                                       p95=float(v[math.ceil(len(v)*.95)-1]), max=float(v[-1]))

phases = {'Roll':0, 'SkipVisual':.25, 'Reorder':.5, 'TogglePending_Refresh':.7, 'ClearPending_Refresh':.85}
events = []
for gc_i in np.flatnonzero(a['gc_alloc_bytes'] > 10000):
    if gc_i == 0: continue
    i = gc_i - 1
    t = a['elapsed_s'][i]
    # Two-frame pre-sampling drain shifts the apparent one-second phase by ~30ms.
    dist = {k:abs(((t+.03-v+.5)%1)-.5) for k,v in phases.items()}
    label = min(dist, key=dist.get)
    if t > 179.9 and a['gc_alloc_bytes'][gc_i] < 100000: label = 'TailRefresh'
    events.append(dict(frame=int(a['frame'][i]), elapsed_s=t, inferred_phase=label,
                       phase_distance_s=min(dist.values()), interval_ms=a['interval_ms'][i],
                       next_interval_ms=a['interval_ms'][gc_i],
                       pair_total_ms=a['interval_ms'][i]+a['interval_ms'][gc_i],
                       gc_bytes=a['gc_alloc_bytes'][gc_i], completed_main_ms=a['main_thread_ms'][gc_i]))

groups = {}
for label in list(phases)+['TailRefresh']:
    es=[e for e in events if e['inferred_phase']==label]
    groups[label]={key:stats([e[key] for e in es]) for key in ['interval_ms','next_interval_ms','pair_total_ms','gc_bytes','completed_main_ms']}
    if label!='TailRefresh':
        groups[label]['first30']={key:stats([e[key] for e in es if e['elapsed_s']<30]) for key in ['interval_ms','pair_total_ms','gc_bytes','completed_main_ms']}
        groups[label]['last30']={key:stats([e[key] for e in es if e['elapsed_s']>=150]) for key in ['interval_ms','pair_total_ms','gc_bytes','completed_main_ms']}
        groups[label]['gc_growth_bytes_per_second']=float(np.polyfit([e['elapsed_s'] for e in es],[e['gc_bytes'] for e in es],1)[0])
    groups[label]['completed_main_over_16_7']=int(sum(e['completed_main_ms']>16.7 for e in es))
    groups[label]['completed_main_over_20']=int(sum(e['completed_main_ms']>20 for e in es))

opframes={e['frame'] for e in events}
op=np.array([int(f) in opframes for f in a['frame']])
after=np.array([int(f)-1 in opframes for f in a['frame']])
correlations=[]
for lag in range(-4,5):
    x,y=a['gc_alloc_bytes'],a['interval_ms']
    x,y=x[max(0,lag):len(x)+min(0,lag)],y[max(0,-lag):len(y)+min(0,-lag)]
    correlations.append(dict(gc_offset=lag,pearson=float(np.corrcoef(x,y)[0,1])))
source_paths=['DicePrototype/Assets/Game/Runtime/GameApp.cs','DicePrototype/Assets/Game/UI/UiKit.cs',
              'DicePrototype/Assets/Game/Runtime/AtomicSaveStore.cs','DicePrototype/Assets/Game/Core/Session/GameSession.cs',
              'DicePrototype/Assets/Game/Core/Session/GameSession.Submission.cs','DicePrototype/Assets/Game/Core/Session/SessionModels.cs']
out=dict(run=str(RUN.relative_to(ROOT)), environment=report['environment'], probe_revision=report['probeRevision'],
         input_version_named_by_root='27e4b34', method='Diagnostic allocation events >10KB, paired one row earlier. Closest nominal phase with +30ms drain offset; labels inferred, not method timings. Two low-allocation end events classified TailRefresh from source loop. All frames retained for formal metrics.',
         csv_sha256=hashlib.sha256((RUN/'performance-frames.csv').read_bytes()).hexdigest(),
         report_sha256=hashlib.sha256((RUN/'player-report.json').read_bytes()).hexdigest(),
         frames=len(rows), phases=groups, events=len(events), correlations=correlations,
         all_interval_ms=stats(a['interval_ms']), other_interval_ms=stats(a['interval_ms'][~(op|after)]),
         total_gc_bytes=float(a['gc_alloc_bytes'].sum()),
         long_frames={str(t):{'all':int(np.count_nonzero(a['interval_ms']>t)),
                       'assigned_operation':int(np.count_nonzero((a['interval_ms']>t)&op))} for t in [16.7,18,18.3322,20,25,30,35]},
         source_sha256={p:hashlib.sha256((ROOT/p).read_bytes()).hexdigest() for p in source_paths})
(OUT/'player05-analysis.json').write_text(json.dumps(out,ensure_ascii=False,indent=2),encoding='utf-8')
with (OUT/'player05-operation-events.csv').open('w',newline='',encoding='utf-8') as f:
    w=csv.DictWriter(f,fieldnames=list(events[0]));w.writeheader();w.writerows(events)
print(json.dumps({'events':out['events'],'phases':groups,'long_frames':out['long_frames'],'other_frames':out['other_interval_ms'],'gc_total':out['total_gc_bytes']},indent=2))
