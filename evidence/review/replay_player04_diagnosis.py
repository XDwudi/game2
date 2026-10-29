"""Read-only Player04 CSV diagnosis. Does not alter measured evidence or acceptance."""
from pathlib import Path
import csv
import hashlib
import json
import math
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
OUT = Path(__file__).resolve().parent
RUN = ROOT / 'evidence/runs/player/player-20261029-012720-f50658f6'
raw = list(csv.DictReader((RUN / 'performance-frames.csv').open(encoding='utf-8-sig')))
# Match the probe's already-established exclusion of its initial partial interval.
rows = raw[1:]
data = {k: np.array([float(r[k]) if r[k] else np.nan for r in rows]) for k in rows[0]}

def stats(values):
    a = np.asarray(values)
    a = np.sort(a[np.isfinite(a)])
    return {'count': len(a), 'mean': float(np.mean(a)),
            'p50': float(a[math.ceil(len(a)*.5)-1]),
            'p95': float(a[math.ceil(len(a)*.95)-1]),
            'max': float(a[-1])} if len(a) else {'count': 0}

def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

report = json.loads((RUN / 'player-report.json').read_text(encoding='utf-8-sig'))
phases = {'Roll': 0., 'SkipVisual': .25, 'Reorder': .50,
          'TogglePending_Refresh': .70, 'ClearPending_Refresh': .85}
events = []
for gc_i in np.flatnonzero(data['gc_alloc_bytes'] > 100000):
    # CSV demonstrates a completed-frame recorder one row after the long interval.
    # First measured GC row is observer-preallocation contamination, retained separately.
    if gc_i == 0:
        continue
    op_i = gc_i - 1
    t = data['elapsed_s'][op_i]
    distances = {k: abs(((t-v+.5) % 1)-.5) for k, v in phases.items()}
    kind = min(distances, key=distances.get)
    events.append({'frame': int(data['frame'][op_i]), 'elapsed_s': t,
                   'phase_inferred': kind, 'phase_distance_s': distances[kind],
                   'interval_ms': data['interval_ms'][op_i],
                   'next_interval_ms': data['interval_ms'][gc_i],
                   'completed_frame_gc_bytes': data['gc_alloc_bytes'][gc_i],
                   'completed_frame_main_ms': data['main_thread_ms'][gc_i]})

correlations = []
for lag in range(-4, 5):
    # Positive lag pairs interval[t] to GC[t+lag]. Boundary transient omitted only here.
    x, y = data['gc_alloc_bytes'][10:], data['interval_ms'][10:]
    x, y = x[max(0,lag):len(x)+min(0,lag)], y[max(0,-lag):len(y)+min(0,-lag)]
    correlations.append({'gc_row_offset_from_interval': lag,
                         'pearson_r': float(np.corrcoef(x, y)[0, 1])})

by_phase = {}
for phase in phases:
    es = [e for e in events if e['phase_inferred'] == phase]
    early = [e for e in es if e['elapsed_s'] < 30]
    late = [e for e in es if e['elapsed_s'] >= 150]
    by_phase[phase] = {
        'events': len(es), 'interval_ms': stats([e['interval_ms'] for e in es]),
        'next_interval_ms': stats([e['next_interval_ms'] for e in es]),
        'gc_bytes': stats([e['completed_frame_gc_bytes'] for e in es]),
        'first_30s_mean_gc_bytes': float(np.mean([e['completed_frame_gc_bytes'] for e in early])),
        'last_30s_mean_gc_bytes': float(np.mean([e['completed_frame_gc_bytes'] for e in late])),
        'first_30s_mean_interval_ms': float(np.mean([e['interval_ms'] for e in early])),
        'last_30s_mean_interval_ms': float(np.mean([e['interval_ms'] for e in late])),
        'completed_frame_main_ms': stats([e['completed_frame_main_ms'] for e in es]),
        'gc_linear_slope_bytes_per_second': float(np.polyfit([e['elapsed_s'] for e in es], [e['completed_frame_gc_bytes'] for e in es], 1)[0]),
    }

opframes = {e['frame'] for e in events}
op_mask = np.array([int(f) in opframes for f in data['frame']])
after_mask = np.array([int(f)-1 in opframes for f in data['frame']])
blocks = []
for start in range(0,180,30):
    mask = (data['elapsed_s'] >= start) & (data['elapsed_s'] < start + 30)
    blocks.append({'from_s': start, 'to_s': start + 30,
                   'interval_ms': stats(data['interval_ms'][mask]),
                   'gc_bytes_sum': float(np.sum(data['gc_alloc_bytes'][mask])),
                   'gc_bytes': stats(data['gc_alloc_bytes'][mask])})

source_paths = [
    'DicePrototype/Assets/Game/Core/Session/GameSession.cs',
    'DicePrototype/Assets/Game/Core/Session/GameSession.Validation.cs',
    'DicePrototype/Assets/Game/Core/Session/GameSession.Submission.cs',
    'DicePrototype/Assets/Game/Core/Session/SessionModels.cs',
    'DicePrototype/Assets/Game/Runtime/GameApp.cs',
    'DicePrototype/Assets/Game/Runtime/AtomicSaveStore.cs',
    'DicePrototype/Assets/Game/Runtime/Verification/PlayerVerification.cs',
]
result = {
    'source_run': str(RUN.relative_to(ROOT)), 'environment': report['environment'],
    'probe_revision_measured': report['probeRevision'],
    'probe_assembly_mvid_measured': report['probeAssemblyMvid'],
    'raw_csv_sha256': digest(RUN / 'performance-frames.csv'),
    'report_sha256': digest(RUN / 'player-report.json'),
    'method': 'Nearest-rank quantiles. High allocation event >100000 bytes, paired with preceding frame. Phase inferred by closest nominal scheduled operation; not captured method timestamps. Original CSV and acceptance result unchanged.',
    'first_measured_gc_boundary': {k: float(data[k][0]) for k in ('frame','elapsed_s','gc_alloc_bytes','interval_ms')},
    'events': len(events), 'maximum_phase_assignment_distance_s': max(e['phase_distance_s'] for e in events),
    'correlations': correlations, 'by_phase': by_phase, 'blocks_30s': blocks,
    'all_frame_interval_ms': stats(data['interval_ms']),
    'operation_interval_ms': stats(data['interval_ms'][op_mask]),
    'next_frame_interval_ms': stats(data['interval_ms'][after_mask]),
    'other_frame_interval_ms': stats(data['interval_ms'][~(op_mask|after_mask)]),
    'long_frames': {str(threshold): {'total': int(np.count_nonzero(data['interval_ms'] > threshold)),
                   'assigned_operation': int(np.count_nonzero((data['interval_ms'] > threshold) & op_mask))}
                   for threshold in (16.7,20,23.5467,25,30,40)},
    'gc_alloc_bytes_sum': float(np.sum(data['gc_alloc_bytes'])),
    'high_allocation_rows': int(np.count_nonzero(data['gc_alloc_bytes'] > 100000)),
    'read_source_hashes': {p: digest(ROOT / p) for p in source_paths},
    'source_caveat': 'Hashes identify read source at analysis time, not Player04 source bundle. Root is concurrently changing GameApp/UI pooling; measured build GUID and raw evidence identify tested binary. Probe two-frame warmup drain is also newer than Player04.',
}
(OUT/'player04-diagnosis-independent.json').write_text(json.dumps(result, indent=2, ensure_ascii=False), encoding='utf-8')
with (OUT/'player04-diagnosis-events-independent.csv').open('w', newline='', encoding='utf-8') as f:
    writer = csv.DictWriter(f, fieldnames=list(events[0])); writer.writeheader(); writer.writerows(events)
print(json.dumps({'by_phase': by_phase, 'long_frames': result['long_frames'],
                  'other_frames': result['other_frame_interval_ms'],
                  'events': len(events), 'max_phase_distance': result['maximum_phase_assignment_distance_s']}, indent=2))
