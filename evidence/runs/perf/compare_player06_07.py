"""Compare unchanged full-frame clock series and inferred operations; no acceptance rewrite."""
from pathlib import Path
import csv, hashlib, json, math
import numpy as np
ROOT=Path(__file__).resolve().parents[3]
OUT=Path(__file__).resolve().parent
PHASES={'Roll':0.,'SkipVisual':.25,'Reorder':.5,'TogglePending_Refresh':.7,'ClearPending_Refresh':.85}
CLOCKS=['interval_ms','main_thread_ms','frame_start_delta_ms','present_called_delta_ms','frame_complete_delta_ms']
def stats(values):
    a=np.asarray(values);a=np.sort(a[np.isfinite(a)])
    return None if not len(a) else dict(n=len(a),mean=float(a.mean()),p50=float(a[math.ceil(len(a)*.5)-1]),p95=float(a[math.ceil(len(a)*.95)-1]),max=float(a[-1]))
def analyze(name):
    path=ROOT/'evidence/runs/player'/name
    report=json.loads((path/'player-report.json').read_text(encoding='utf-8-sig'))
    rows=list(csv.DictReader((path/'performance-frames.csv').open(encoding='utf-8-sig')))[1:]
    a={k:np.array([float(r[k]) if r[k] else np.nan for r in rows]) for k in rows[0]}
    events=[]
    for i in np.flatnonzero(a['gc_alloc_bytes']>10000):
        if i<1 or i+5>=len(rows):continue
        t=a['elapsed_s'][i-1];dist={k:abs(((t+.017-v+.5)%1)-.5) for k,v in PHASES.items()};phase=min(dist,key=dist.get)
        if t>179.9 and a['gc_alloc_bytes'][i]<100000:phase='TailRefresh'
        events.append(dict(gc_row=int(i),frame=int(a['frame'][i-1]),elapsed_s=t,phase=phase,phase_distance_s=min(dist.values()),
                           late_ms=a['interval_ms'][i-1],main_ms=a['main_thread_ms'][i],start_ms=a['frame_start_delta_ms'][i+4],
                           present_ms=a['present_called_delta_ms'][i+2],complete_ms=a['frame_complete_delta_ms'][i+2],
                           next_present_ms=a['present_called_delta_ms'][i+3],pair_present_ms=a['present_called_delta_ms'][i+2]+a['present_called_delta_ms'][i+3],
                           gc_bytes=a['gc_alloc_bytes'][i]))
    group={}
    fields=['late_ms','main_ms','start_ms','present_ms','complete_ms','next_present_ms','pair_present_ms','gc_bytes']
    for phase in PHASES:
        e=[x for x in events if x['phase']==phase]
        group[phase]={k:stats([x[k] for x in e]) for k in fields}
        group[phase]['early30']={k:stats([x[k] for x in e if x['elapsed_s']<30]) for k in fields}
        group[phase]['late30']={k:stats([x[k] for x in e if x['elapsed_s']>=150]) for k in fields}
        group[phase]['present_over18']=sum(float(x['present_ms'])>18 for x in e)
        group[phase]['present_over20']=sum(float(x['present_ms'])>20 for x in e)
        group[phase]['long_short_pairs_over18_then_under15_5']=sum(float(x['present_ms'])>18 and float(x['next_present_ms'])<15.5 for x in e)
    x=(a['gc_alloc_bytes']>10000).astype(float);corr={}
    for clock in CLOCKS:
        corr[clock]=[]
        for lag in range(-2,7):
            xx=x[max(0,-lag):len(x)+min(0,-lag)];yy=a[clock][max(0,lag):len(x)+min(0,lag)]
            ok=np.isfinite(yy);corr[clock].append(dict(clock_offset_from_gc=lag,r=float(np.corrcoef(xx[ok],yy[ok])[0,1])))
    with (OUT/(name+'-operations.csv')).open('w',newline='',encoding='utf-8') as f:
        w=csv.DictWriter(f,fieldnames=list(events[0]));w.writeheader();w.writerows(events)
    return dict(directory=name,build_guid=report['environment']['buildGuid'],probe_revision=report['probeRevision'],
                csv_sha256=hashlib.sha256((path/'performance-frames.csv').read_bytes()).hexdigest(),
                report_sha256=hashlib.sha256((path/'player-report.json').read_bytes()).hexdigest(),
                reported_status=report['status'],sample_seconds=report['performance']['sampleSeconds'],frames=len(rows),
                full_clock_stats={k:stats(a[k]) for k in CLOCKS},gc_stats=stats(a['gc_alloc_bytes']),gc_bytes_sum=float(a['gc_alloc_bytes'].sum()),
                full_clock_counts={k:{str(t):int(np.count_nonzero(a[k]>t)) for t in [16.7,18,20,22,24]} for k in CLOCKS},
                groups=group,events=len(events),max_phase_distance_s=max(x['phase_distance_s'] for x in events),correlations=corr,
                metric_coverage={k:v['validFrameCoverage'] for k,v in report['performance'].items() if isinstance(v,dict) and 'validFrameCoverage' in v},
                independent_clock_coverage={k:report['performance']['independentFrameClocks'][k]['validFrameCoverage'] for k in ['frameStart','presentCalled','frameComplete']})

results={k:analyze(n) for k,n in [('Player06','player-20261029-024615-1c6316db'),('Player07','player-20261029-025732-31091505')]}
out={'method':'Original first partial interval excluded exactly as probe. Full stats use every remaining row. Separate diagnostic >10KB event association retains Player06 fixed observed offsets: Late GC-1, Main GC+0, Present/Complete GC+2, FrameStart GC+4; all lag correlations are also retained. No timing series selected to replace verdict. Operation labels inferred with +17ms schedule offset; not method timings.',
     'production_change_as_supplied_by_root':'f060098: GameApp.Refresh -> RebuildView -> Canvas.ForceUpdateCanvases; probe/pacer/threshold unchanged.',
     'runs':results}
(OUT/'player06-vs-player07.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
for k,r in results.items():
    print(k,r['full_clock_counts'])
    for phase,g in r['groups'].items():print(phase,'present',g['present_ms'],'next',g['next_present_ms']['mean'],'pair',g['pair_present_ms']['mean'],'earlylate',(g['early30']['present_ms']['mean'],g['late30']['present_ms']['mean']),'longshort',g['long_short_pairs_over18_then_under15_5'])
