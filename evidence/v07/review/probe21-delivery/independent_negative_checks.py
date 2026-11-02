"""Read-only real evidence validation; mutations are in-memory or isolated copies."""
import copy, hashlib, json, pathlib, sys, tempfile

ROOT=pathlib.Path('E:/agent_workspace/gpt/game2')
HERE=ROOT/'artifacts/cache/v07-probe21-delivery-review'
CANDIDATE=ROOT/'artifacts/cache/v07-probe21-delivery-readiness/candidate'
sys.dont_write_bytecode=True
sys.path[:0]=[str(CANDIDATE/'tools'),str(CANDIDATE/'tools/macos/v07')]
import compare_replays as v
load=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
base=ROOT/'evidence/v07/player-4f020f3-1308/strict-full/player-20261101-130814-a15fc99d'
source=load(base/'player-report.json')
old={name:sha(base/name) for name in ('player-report.json','performance-frames.csv','normal-replay.jsonl','debug24-replay.jsonl')}
rows=[]
assert v.validate_windows_reference(source)==[x for x in v.validate_report(source,'windows')]
assert v.validate_windows_reference(source)
assert not v.validate_windows_reference(source,True,True)
raw=v.validate_failed_performance_csv(source,base)
notice=v.review_reference_notice(source,True)
assert notice['fullWindowsPassed'] is False and notice['fullReleaseAccepted'] is False
assert notice['windowsPerformance']['measurementCoverageStatus']=='INCOMPLETE_OR_UNAVAILABLE'
assert notice['windowsPerformance']['acceptanceStatus']=='FAILED'
rows.append({'case':'actual_strict_failed_only_explicit_diagnostic','accepted':True,'raw':raw,'notice':notice})

mutations={
 'lowered_coverage_threshold':lambda d:d['performance'].update(requiredMetricCoverage=.5),
 'coverage_above_one':lambda d:d['performance']['mainThread'].update(validFrameCoverage=1.1),
 'sample_count_disagrees_with_coverage':lambda d:d['performance']['gcAllocated'].update(samples=1),
 'gpu_marked_unavailable_with_samples':lambda d:d['performance']['gpu'].update(available=False),
 'focus_exceeds_verified_raw_rows':lambda d:d['performance'].update(focusedSampleFrames=9431),
 'raw_frame_count_not_valid_plus_one':lambda d:d['performance']['independentFrameClocks'].update(rawRowsIncludingInitialPartialInterval=9431),
 'short_sample':lambda d:d['performance'].update(sampleSeconds=179.9),
 'not_completed':lambda d:d.update(phase='V07NativeDisplay'),
 'failure_payload':lambda d:d.update(failure='synthetic failure'),
 'missing_actual_config_hash':lambda d:d.update(configSha256=None),
 'failed_ui':lambda d:d['uiChecks'][0].update(status='failed'),
 'wrong_confirmed_window_height':lambda d:next(x for x in d['displayChecks'] if x['id']=='window-1280x720')['actualNativeReadback'].update(height=719),
}
for name,mutate in mutations.items():
    d=copy.deepcopy(source);mutate(d);failures=v.validate_windows_reference(d,True,True)
    assert failures,name
    try:v.validate_failed_performance_csv(d,base)
    except ValueError:pass
    else:raise AssertionError('CSV wrapper did not reject '+name)
    rows.append({'case':name,'rejected':True,'failures':failures,'coverageNotice':v.failed_performance_notice(d)['measurementCoverageStatus']})

for name,mutate in {
 'raw_gpu_missing_count_mismatch':lambda d:d['performance'].update(gpuMissingSamples=d['performance']['gpuMissingSamples']+1),
 'raw_p95_mismatch':lambda d:d['performance']['frameInterval'].update(p95=d['performance']['frameInterval']['p95']+.01)
}.items():
    d=copy.deepcopy(source);mutate(d)
    try:v.validate_failed_performance_csv(d,base)
    except ValueError as e: rows.append({'case':name,'rejected':True,'error':str(e)})
    else:raise AssertionError('Raw mismatch accepted: '+name)

with tempfile.TemporaryDirectory(dir=HERE/'tmp') as temp:
    p=pathlib.Path(temp);data=(base/'performance-frames.csv').read_bytes()
    for name,raw_data in [('missing_csv',None),('truncated_csv',b'\n'.join(data.splitlines()[:-1])+b'\n')]:
        if raw_data is not None:(p/'performance-frames.csv').write_bytes(raw_data)
        try:v.validate_failed_performance_csv(source,p)
        except (OSError,ValueError) as e: rows.append({'case':name,'rejected':True,'error':str(e)})
        else:raise AssertionError(name)
assert old=={name:sha(base/name) for name in old}

frozen=load(ROOT/'artifacts/cache/v07-probe21-delivery-readiness/FROZEN_CANDIDATE.json')
assert len(frozen['files'])==9
for name,row in frozen['files'].items():assert sha(CANDIDATE/name)==row['afterSha256'],name
result={'schema':'v07-independent-diagnostic-tool-negative-checks-1','candidateFrozenFiles':frozen['files'],'realInputsUnchanged':old,'cases':rows,'caseCount':len(rows),'scope':'Python consumers and synthetic negative mutations only; no Player, Editor, network or publication'}
(HERE/'final-negative-results.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps({'caseCount':len(rows),'realInputsUnchanged':True,'frozenFilesMatched':len(frozen['files']),'strictPerformanceAcceptance':notice['windowsPerformance']['acceptanceStatus'],'strictMeasurementCoverage':notice['windowsPerformance']['measurementCoverageStatus']}))
