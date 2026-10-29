import csv,hashlib,json
from pathlib import Path
import numpy as np
root=Path(__file__).resolve().parents[2]
path=root/'evidence/runs/player/player-20261029-024615-1c6316db/performance-frames.csv'
rows=list(csv.DictReader(path.open(encoding='utf-8-sig')))[1:]
keys=['interval_ms','main_thread_ms','frame_start_delta_ms','present_called_delta_ms','frame_complete_delta_ms','gc_alloc_bytes']
a={k:np.array([float(r[k]) if r[k] else np.nan for r in rows]) for k in keys}
metrics={}
for key in keys[:-1]:
    pairs=[]
    for lag in range(-6,7):
        x,y=a['gc_alloc_bytes'],a[key]
        x,y=x[max(0,lag):len(x)+min(0,lag)],y[max(0,-lag):len(y)+min(0,-lag)]
        finite=np.isfinite(x)&np.isfinite(y)
        pairs.append(dict(gcIndexOffset=lag,pearson=float(np.corrcoef(x[finite],y[finite])[0,1]),longOver18=int(np.sum(y>18)),longNearAllocationEvent=int(np.sum((x>10000)&(y>18)))))
    metrics[key]=dict(over18ms=int(np.sum(a[key]>18)),lags=pairs,best=max(pairs,key=lambda v:v['pearson']))
result=dict(inputCsvSha256=hashlib.sha256(path.read_bytes()).hexdigest(),scope='Diagnostic only; all original performance frames remain in the verdict. Lag aligns observed counters, not exact native frame ownership. Allocation events >10KB infer refresh/transaction work, not measured TMP method cost.',frames=len(rows),allocationEvents=int(np.sum(a['gc_alloc_bytes']>10000)),metrics=metrics)
(root/'evidence/review/player06-timing-diagnosis.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
print(json.dumps({k:v['best'] for k,v in metrics.items()},indent=2))
