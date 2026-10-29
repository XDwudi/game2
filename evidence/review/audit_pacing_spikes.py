"""Read-only recomputation of the two original idle timing experiments."""
import csv
import hashlib
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).with_name('pacing-spikes-independent.json')

def digest(p):
    return hashlib.sha256(p.read_bytes()).hexdigest()

def stats(values):
    a = sorted(values)
    return dict(samples=len(a), meanMs=sum(a)/len(a),
                p50Ms=a[math.ceil(len(a)*.5)-1],
                p95Ms=a[math.ceil(len(a)*.95)-1], maxMs=a[-1])

results = []
for folder in ['spike-01', 'spike-02-bitblt']:
    base = ROOT / 'evidence/runs/pacing' / folder
    cp = base / 'windowed-software-60.csv'
    rp = base / 'pacing-report.json'
    rows = list(csv.DictReader(cp.open(newline='', encoding='utf-8-sig')))
    report = json.loads(rp.read_text(encoding='utf-8-sig'))
    mode = report['modes'][0]
    checks = []
    def check(name, value):
        checks.append(dict(name=name, passed=bool(value)))
    check('actual nonzero 1197 rows and 1196 intervals', len(rows) == 1197)
    check('all focused', all(r['focused'] == '1' for r in rows))
    check('all native timings unique', all(r['timing_status'] == '4' for r in rows))
    check('unity frame IDs consecutive', all(int(b['unity_frame'])-int(a['unity_frame']) == 1 for a,b in zip(rows, rows[1:])))
    check('10MHz CPU clock consistent', all(int(r['cpu_timer_frequency_hz']) == report['cpuTimerFrequencyHz'] == 10000000 for r in rows))
    check('exclusive failure retained', report['status'] == 'experiment_failed' and report['failurePhase'] == 'exclusive-requested-60hz-vsync1/switch')
    check('restoration confirmed', report['restorationConfirmedBeforeExit'] and report['restoredEnvironment']['mode'] == 'Windowed' and report['restoredEnvironment']['targetFrameRate'] == 60 and report['restoredEnvironment']['vSyncCount'] == 0)
    metrics = {}
    fields = [('elapsed_s','late_interval_ms','lateUpdateInterval',1000),
              ('frame_start_timestamp','frame_start_delta_ms','frameStartInterval',1000/report['cpuTimerFrequencyHz']),
              ('present_called_timestamp','present_called_delta_ms','presentCalledInterval',1000/report['cpuTimerFrequencyHz']),
              ('frame_complete_timestamp','frame_complete_delta_ms','frameCompleteInterval',1000/report['cpuTimerFrequencyHz'])]
    for raw, delta, key, factor in fields:
        parse = float if raw == 'elapsed_s' else int
        values = [(parse(b[raw])-parse(a[raw]))*factor for a,b in zip(rows,rows[1:])]
        check(key+' all positive raw adjacent deltas', all(x>0 for x in values))
        check(key+' first partial delta blank', rows[0][delta] == '')
        check(key+' raw recomputation matches every CSV delta', all(abs(x-float(r[delta]))<1e-8 for x,r in zip(values,rows[1:])))
        metrics[key] = stats(values)
        check(key+' all report statistics match', all(abs(mode[key][k]-v)<1e-8 for k,v in metrics[key].items()))
    results.append(dict(folder=folder, csvSha256=digest(cp), reportSha256=digest(rp),
                        buildGuid=report['buildGuid'], assemblyMvid=report['assemblyMvid'],
                        rows=len(rows), metrics=metrics, checks=checks,
                        passCount=sum(x['passed'] for x in checks), failCount=sum(not x['passed'] for x in checks)))

result = dict(kind='independent-idle-pacing-recalculation', sourceOnly=True,
              actualPlayerLaunchedByReviewer=False, thresholdChanged=False,
              limitations='20-second idle experiments; no AC-PERF verdict. Present call and GPU completion are not physical scanout.',
              runs=results, totalChecks=sum(len(r['checks']) for r in results),
              failedChecks=sum(r['failCount'] for r in results))
OUT.write_text(json.dumps(result, ensure_ascii=False, indent=2)+'\n', encoding='utf-8')
print(json.dumps(dict(totalChecks=result['totalChecks'],failedChecks=result['failedChecks'],
                     p95s={r['folder']:{k:v['p95Ms'] for k,v in r['metrics'].items()} for r in results}), indent=2))
raise SystemExit(bool(result['failedChecks']))
