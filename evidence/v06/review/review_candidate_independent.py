"""Independent offline checks of the explicitly restricted candidate route. Never publishes or launches."""
from pathlib import Path
import copy, hashlib, json, sys
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3];OUT=Path(__file__).resolve().parent
sys.path.insert(0,str(ROOT/'tools/macos/v06'));sys.path.insert(0,str(ROOT/'tools'))
from compare_replays import validate_report,validate_windows_review_reference
from test_mac_tools import report
from publish_v06_release import ensure_publish_allowed

inputs=[ROOT/'tools'/p for p in ('package_v06.py','publish_v06_release.py','release_v06_manifest.py','macos/v06/compare_replays.py','macos/v06/verify_mac.py','macos/v06/VerifyMac.command')]
def snap():return {p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in inputs}
before=snap();cases=[]
base=report(False)
base.update(status='partial_passed_native_display_pending',verificationScope='full-gameplay-ui-perf_native_display_pending',nativeDisplayDeferred=True)
base['displayChecks']=[{'id':'native-display-verification-deferred','status':'not_executed'}]
base['v06Checks']=[x for x in base['v06Checks'] if x['id']!='display-state-isolation']
frozen=copy.deepcopy(base)
assert not validate_windows_review_reference(base) and validate_report(base,'windows') and base==frozen
cases.append({'case':'explicit_valid_partial_only', 'passed':True})
mutations={
 'scope_missing':lambda x:x.pop('verificationScope'),
 'defer_numeric_not_bool':lambda x:x.update(nativeDisplayDeferred=1),
 'nonzero_exit':lambda x:x.update(exitCode=3),
 'false_formal_status':lambda x:x.update(status='passed'),
 'unexecuted_pressure':lambda x:x.update(stressCompleted=False),
 'native_failed':lambda x:x['displayChecks'].append({'id':'mode-ExclusiveFullScreen','status':'failed'}),
 'duplicate_native':lambda x:x['displayChecks'].append(copy.deepcopy(x['displayChecks'][0])),
 'unknown_native':lambda x:x['displayChecks'].append({'id':'unknown','status':'passed'}),
 'wrong_defer_status':lambda x:x['displayChecks'][0].update(status='passed'),
 'duplicate_ui':lambda x:x['v06Checks'].append(copy.deepcopy(x['v06Checks'][0])),
 'failed_display_isolation':lambda x:x['v06Checks'].append({'id':'display-state-isolation','status':'failed'}),
 'ordinary_trace_debug':lambda x:x['normalReplay'].update(normalIsDebug=True),
 'debug_trace_ordinary':lambda x:x['debug24'].update(isDebug=False),
 'wrong_identity':lambda x:x.update(buildIdentity='{}'),
 'editor_execution':lambda x:x['environment'].update(isEditor=True),
 'under180_seconds':lambda x:x['performance'].update(sampleSeconds=179.99),
 'over_budget':lambda x:x['performance']['frameInterval'].update(p95=16.7001),
 'reduced_pressure_inventory':lambda x:x['performance'].update(inventory=4),
}
for key,mutate in mutations.items():
    value=copy.deepcopy(base);mutate(value);failures=validate_windows_review_reference(value)
    assert failures,key
    cases.append({'case':key,'passed':True,'rejections':failures})
for required in ('shop-controller-refresh-ui','shop-free-first-refresh-service-ui'):
    for status in ('missing','not_executed','failed'):
        value=copy.deepcopy(base)
        if status=='missing':value['v06Checks']=[r for r in value['v06Checks'] if r['id']!=required]
        else:next(r for r in value['v06Checks'] if r['id']==required)['status']=status
        assert validate_windows_review_reference(value)
        cases.append({'case':required+'_'+status,'passed':True})
for action,scope in [('publish',False),('upload',True),('inspect',True)]:
    try:ensure_publish_allowed({'reviewCandidate':True},action,scope)
    except ValueError:cases.append({'case':f'publish_scope_{action}_{scope}','passed':True})
    else:raise AssertionError((action,scope))
assert before==snap()
output=OUT/'candidate-tools-independent01.json';assert not output.exists()
output.write_text(json.dumps({'scope':__doc__,'status':'passed','cases':cases,'before':before,'after':snap(),
 'limits':['Synthetic report fixtures only, not runtime evidence.','Existing user authorization governs test prerelease; switches prevent technical scope mistakes.','Full Windows/P22, Mac real-device and human acceptance are not granted.']},indent=2)+'\n',encoding='utf8')
print(len(cases),'independent candidate checks passed; no app/network operation.')
