"""Independent read-only validation of failed-performance reference and negative boundaries. No publication."""
from pathlib import Path
import copy,hashlib,json,sys
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3];OUT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'tools/macos/v06'));sys.path.insert(0,str(ROOT/'tools'))
from compare_replays import validate_report,validate_windows_review_reference,validate_windows_reference,validate_trace_replay,review_reference_notice
from publish_v06_release import ensure_publish_allowed
from package_v06 import candidate_notice
run=ROOT/'evidence/v06/player/player-20261031-154556-f25586f0';path=run/'player-report.json'
raw=path.read_bytes();r=json.loads(raw);sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
manifest=json.loads((ROOT/'evidence/v06/session/diagnostic-candidate-source-manifest.json').read_text(encoding='utf8'))
inputs={p['path']:sha(ROOT/p['path']) for p in manifest['files']}
assert all(inputs[p['path']]==p['sha256'] for p in manifest['files'])
cases=[]
assert validate_report(r,'windows') and validate_windows_review_reference(r) and not validate_windows_reference(r,True,True)
assert r['status']=='budget_exceeded' and r['exitCode']==3
trace=validate_trace_replay(r,run,ROOT/'evidence/v06/session/normal-run-trace.json',ROOT/'evidence/v06/session/debug-24-trace.json')
cases.append('original actual report accepted ONLY as diagnostic failed reference')
mutations={
 'unrelated_failure':lambda x:x.update(status='failed'),
 'invented_pass':lambda x:x.update(status='passed'),
 'partial_instead_of_failed':lambda x:x.update(status='partial_passed_native_display_pending'),
 'hidden_exception':lambda x:x.update(failure='actual exception'),
 'wrong_exit':lambda x:x.update(exitCode=0),
 'defer_not_boolean':lambda x:x.update(nativeDisplayDeferred=1),
 'wrong_scope':lambda x:x.update(verificationScope='display_only_no_replay_or_stress'),
 'short_sample':lambda x:x['performance'].update(sampleSeconds=179.999),
 'infinite_sample':lambda x:x['performance'].update(sampleSeconds=float('inf')),
 'infinite_p95':lambda x:x['performance']['frameInterval'].update(p95=float('inf')),
 'not_budget_failure':lambda x:x['performance']['frameInterval'].update(p95=16.7),
 'wrong_budget':lambda x:x['performance'].update(targetFrameMs=24),
 'reduced_load':lambda x:x['performance'].update(controllers=9),
 'coverage_gap_in_frame_clock':lambda x:x['performance']['frameInterval'].update(validFrameCoverage=.99),
 'missing_focus_observation':lambda x:x['performance'].pop('focusedSampleFrames'),
 'invalid_gpu_count':lambda x:x['performance'].update(gpuInvalidSamples=-1),
 'ordinary_debug':lambda x:x['normalReplay'].update(normalIsDebug=True),
 'incomplete_debug':lambda x:x['debug24'].update(passed=False),
 'incomplete_stress':lambda x:x.update(stressCompleted=False),
 'native_failed_not_deferred':lambda x:x['displayChecks'][0].update(status='failed'),
}
for name,mutate in mutations.items():
    x=copy.deepcopy(r);mutate(x);assert validate_windows_reference(x,True,True),name;cases.append(name+' rejected')
for key in ('shop-controller-refresh-ui','shop-free-first-refresh-service-ui','wheel-reach-AddC53','long-mimic-chain-destroyed-owner-ui'):
    x=copy.deepcopy(r);x['v06Checks']=[q for q in x['v06Checks'] if q['id']!=key]
    assert validate_windows_reference(x,True,True);cases.append('missing '+key+' rejected')
for flags in [(False,False),(True,False)]:
    assert validate_windows_reference(r,*flags);cases.append('default route '+str(flags)+' rejected')
try:validate_windows_reference(r,False,True)
except ValueError:cases.append('diagnostic without review flag rejected')
else:raise AssertionError('Missing review flag accepted')
for scope in [(False,False),(True,False),(False,True)]:
    try:ensure_publish_allowed({'reviewCandidate':True,'allowFailedWindowsPerformance':True},'publish',*scope)
    except ValueError:cases.append('publish missing flag '+str(scope)+' rejected')
    else:raise AssertionError('Missing publish scope accepted')
ensure_publish_allowed({'reviewCandidate':True,'allowFailedWindowsPerformance':True},'publish',True,True)
notice=candidate_notice(json.loads(r['buildIdentity']),r,True)
assert notice['windowsPerformance']['acceptanceStatus']=='FAILED' and notice['windowsPerformance']['p95FrameIntervalMs']==r['performance']['frameInterval']['p95']
assert notice['windowsPerformance']['focusedSampleFrames']==0 and notice['windowsPerformance']['gpuInvalidSamples']==1 and notice['fullReleaseAccepted'] is False
assert raw==path.read_bytes() and inputs=={p:sha(ROOT/p) for p in inputs}
out=OUT/'diagnostic-candidate-independent01.json';assert not out.exists()
out.write_text(json.dumps({'scope':__doc__,'status':'diagnostic_reference_validated_with_explicit_failures','sourceStable':True,'checks':cases,
 'originalReport':{'path':path.relative_to(ROOT).as_posix(),'sha256':sha(path),'status':r['status'],'exitCode':r['exitCode']},
 'referenceNotice':review_reference_notice(r,True),'trace':trace,'performanceAccepted':False,'nativeDisplayAccepted':False,'fullReleaseAccepted':False,
 'tools':inputs,'actualNetworkOrMacExecution':False},indent=2)+'\n',encoding='utf8')
print(len(cases),'diagnostic checks passed; original FAILED report unchanged; no acceptance granted.')
