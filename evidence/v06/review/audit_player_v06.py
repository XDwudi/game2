"""Independent read-only inspection of actual Player evidence and raw frame data."""
from pathlib import Path
import csv,hashlib,json,math,statistics,struct,sys
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3];OUT=Path(__file__).resolve().parent
RUN=ROOT/sys.argv[1];target=OUT/(sys.argv[2]+'.json');assert not target.exists()
partial=len(sys.argv)>3 and sys.argv[3]=='--partial'
assert len(sys.argv)==3 or (partial and len(sys.argv)==5), 'Use RUN LABEL [--partial BUILT_PDB_AUDIT]'
sys.path.insert(0,str(ROOT/'tools'))
from macos.v06.compare_replays import validate_report,validate_trace_replay,REQUIRED_V06_CHECKS
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def rows(p):return [json.loads(s.lstrip('\ufeff')) for s in p.read_text(encoding='utf-8-sig').splitlines() if s.strip()]
def snapshot():return {p.relative_to(RUN).as_posix():sha(p) for p in RUN.rglob('*') if p.is_file()}
before=snapshot();r=read(RUN/'player-report.json');errors=[]
def check(ok,note):
    if not ok:errors.append(note)
formal_failures=validate_report(r,'windows')
built_pdb=None
if partial:
    check(r['status']=='partial_passed_native_display_pending' and r['exitCode']==0,'Restricted Player did not finish its exact partial status/0')
    check(r.get('verificationScope')=='full-gameplay-ui-perf_native_display_pending' and r.get('nativeDisplayDeferred') is True,'Restricted scope/flag not explicitly recorded')
    expected_rejections={'player_did_not_pass','v06_ui_sections_missing_or_failed','native_display_sections_missing_or_failed'}
    check({f['reason'] for f in formal_failures}==expected_rejections,'Formal validator did not reject exactly the unresolved native/scope requirements')
    checks=r.get('v06Checks',[]);ids=[x.get('id') for x in checks]
    check(len(ids)==len(set(ids)) and (REQUIRED_V06_CHECKS-{'display-state-isolation'}).issubset(ids)
          and all(x.get('status')=='passed' for x in checks),'A required non-display UI section is missing or failed')
    deferred=r.get('displayChecks',[])
    check(len(deferred)==1 and deferred[0].get('id')=='native-display-verification-deferred' and deferred[0].get('status')=='not_executed','Native omission was misrepresented')
    built_pdb=read(ROOT/sys.argv[4])
    runtime=[a for a in built_pdb['assemblies'] if a['name']=='DiceCraft.Runtime']
    check(built_pdb['mismatches']==0 and len(built_pdb['assemblies'])==2 and len(built_pdb['documents'])==33
          and len(runtime)==1 and runtime[0]['mvid']==r.get('probeAssemblyMvid'),'Actual Player module does not match independent built-source PDB audit')
else:
    check(r['status']=='passed' and r['exitCode']==0,'Player did not finish passed/0')
    errors.extend(str(f) for f in formal_failures)
try:trace=validate_trace_replay(r,RUN,ROOT/'evidence/v06/session/normal-run-trace.json',ROOT/'evidence/v06/session/debug-24-trace.json')
except Exception as e:errors.append('Replay validator: '+str(e));trace=None
reference=read(OUT/'managed-rules-run06.trace-details.json')['steps']
for kind,file in [('normal','normal-replay.jsonl'),('debug','debug24-replay.jsonl')]:
    actual=rows(RUN/file);expected=[x for x in reference if x['trajectory']==kind]
    check(len(actual)==len(expected),kind+' independent step count')
    for i,(a,b) in enumerate(zip(actual,expected)):
        for key in ('phase','gold','diceRngState','shopRngState','effectRngState'):
            # Debug rows keep canonical state hash, while original three RNG fields
            # are independently checked by the production replay against its inputs.
            if key in a:check(a[key]==b[key],f'{kind} #{i} independent {key}')
        check(a['stageIndex']+1==b['stage'] and a['kind']==b['command'],f'{kind} #{i} stage/kind')
        check(a['stageScore' if kind=='normal' else 'score']==b['score'],f'{kind} #{i} score')
        if kind=='debug':check(a['ruleStateSha256']==b['stateHash'],f'debug #{i} full state hash')
        check(a['savedStateMatches'] and a['renderedStateMatches'],f'{kind} #{i} persistence/view')
metrics={};p=r['performance']
check(p is not None,'Performance not measured')
if p is not None:
    with (RUN/'performance-frames.csv').open(encoding='utf-8-sig',newline='') as f:raw=list(csv.DictReader(f))
    full=raw[1:]
    check(len(full)==p['frameCount'],'Raw frame count does not equal reported complete intervals')
    check(all(int(b['frame'])==int(a['frame'])+1 for a,b in zip(raw,raw[1:])),'Skipped Unity frames in raw CSV')
    check(all(float(b['elapsed_s'])>float(a['elapsed_s']) for a,b in zip(raw,raw[1:])),'Non-monotonic sampled clock')
    check(all(abs((float(b['elapsed_s'])-float(a['elapsed_s']))*1000-float(b['interval_ms']))<1e-7 for a,b in zip(raw,raw[1:])),'CSV interval clock differs')
    definitions=[('interval_ms','frameInterval'),('main_thread_ms','mainThread'),('render_thread_ms','renderThread'),('frame_timing_cpu_ms','frameTimingCpu'),('gpu_ms','gpu'),('gc_alloc_bytes','gcAllocated'),('gc_used_bytes','gcUsed'),('ui_build_ms','uiRebuild'),('ui_build_marker_calls','uiRebuildMarkerCalls'),('mono_used_bytes','monoUsed')]
    for col,key in definitions:
        values=sorted(float(x[col]) for x in full if x[col]!='')
        check(all(math.isfinite(x) and x>=0 for x in values),'Invalid raw '+col)
        computed={'samples':len(values),'expectedFrames':len(full),'validFrameCoverage':len(values)/len(full),
                  'mean':statistics.mean(values) if values else None,'p50':values[math.ceil(len(values)*.5)-1] if values else None,
                  'p95':values[math.ceil(len(values)*.95)-1] if values else None,'max':values[-1] if values else None}
        metrics[key]=computed
        for field,value in computed.items():
            reported=p[key][field]
            check(reported is None if value is None else reported is not None and math.isclose(value,reported,rel_tol=1e-12,abs_tol=1e-7),'Raw metric mismatch '+key+'/'+field)
    check(p['sampleSeconds']>=180 and p['warmupSeconds']==10,'Changed/shortened measurement duration')
    check(p['targetFrameMs']==16.7 and p['frameBudgetMet']==(metrics['frameInterval']['p95']<=16.7),'Frame budget changed or verdict differs')
    check((p['dice'],p['submitSlots'],p['controllers'],p['inventory'])==(10,8,10,5),'Wrong pressure load')
    check(p['status']=='within_frame_budget' and p['targetResolutionMet'],'Performance incomplete or failed')
    check(p['frameBufferGrowthEvents']==0 and p['gpuInvalidSamples']==0 and p['graphicsTimingWarningsDuringSampling']==0,'Measurement gaps/warnings')
    for key in ('mainThread','gpu','gcAllocated','uiRebuild'):check(metrics[key]['validFrameCoverage']>=.95,'Insufficient '+key+' coverage')
check(r['unityErrors']==[],'Unity errors')
check(all(x['passed'] for x in r.get('animationChecks',[])),'Failed actual animation observation')
save=Path(r['normalSave']);check(sha(save)==r['normalSaveSha256BeforeStress'],'Ordinary save changed during debug/stress')
pointers=read(RUN/'v06-target32-raycasts.json')
check(len(pointers)>=20 and all(x['insideScreen'] and x['insideAllMasks'] and x['firstButtonIsTarget'] and x['graphicDepth']>=0 and not x['graphicCulled'] for x in pointers),'Target32 actual hit/visibility failure')
readiness=read(RUN/'v06-ui-render-readiness.json')
check(len(readiness)==len(pointers),'Render readiness count differs from actual pointer dispatches')
for x in readiness:
    samples=x.get('samples',[])
    check(x.get('ready') is True and samples and samples[-1]['depth']>=0 and samples[-1]['graphicEnabled'], 'Unrendered UI target dispatched: '+x['target'])
    if samples:
        check(samples[-1]['realtime']-samples[0]['realtime']<=2 and all(b['frame']>a['frame'] for a,b in zip(samples,samples[1:])), 'Render readiness did not use bounded actual frames: '+x['target'])
shop_transactions=read(RUN/'v06-shop-refresh-transactions.json')
check([x['charged'] for x in shop_transactions]==[3,5,7,0,5],'Visible shop refresh charge sequence differs')
check([x['nextPrice'] for x in shop_transactions]==[5,7,9,5,7],'Visible shop next-refresh prices differ')
for x in shop_transactions:
    check(x['beforeGold']-x['afterGold']==x['charged'],'Shop refresh actual wallet delta differs')
    check(x['otherQuotesBefore']==x['otherQuotesAfter'],'Non-controller shelf changed during refresh')
    check(x['oldOfferIds'] and x['newOfferIds'] and not set(x['oldOfferIds'])&set(x['newOfferIds']),'Controller quote generation did not replace all old IDs')
    check(x['shopRngBefore']!=x['shopRngAfter'] and x['diskMatches'],'Shop refresh missing RNG/persistence observation')
check(sum(x['target']=='RefreshControllers' for x in pointers)>=5,'Five visible refresh button first-hit events were not recorded')
ui_checks={x['id']:x for x in r['v06Checks']}
for name in ('shop-controller-refresh-ui','shop-free-first-refresh-service-ui'):
    x=ui_checks.get(name,{})
    check(x.get('status')=='passed' and x.get('actual',{}).get('saveAndReopenNoRedraw') is True
          and x.get('actual',{}).get('normalCheckpointUnchanged') is True,'Shop visible refresh/reopen check incomplete: '+name)
input_checks=read(RUN/'v06-input-diagnostics.json')
for x in input_checks:
    check(x['injectedEnabledBefore'] and x['injectedCanRunInBackground'] and x['dynamicPressedEdges']>0 and x['currentDeviceEdges']>0,'Synthetic InputSystem route not observed '+x['scenario'])
    check(x['stateUnchanged'] and x['diskUnchanged'] and x['previousDeviceRestored'] and x['globalSettingsUnchanged'],'Input probe changed ordinary state/global routing')
check(any(x['scenario']=='controller-drag-cancel' and not x['dragAfter'] for x in input_checks),'No routed drag Escape cancellation')
transitions=read(RUN/'v06-resolution-transitions.json')
check(transitions and all(x.get('threeStableExactFrames') and x['finalWidth']==x['requestedWidth'] and x['finalHeight']==x['requestedHeight'] and x['finalMode']=='Windowed' for x in transitions),'Native screenshot resolution transition failed')
images=[]
for name in r['screenshots']:
    path=RUN/name;check(path.is_file(),'Missing screenshot '+name)
    if path.is_file():
        data=path.read_bytes();check(data[:8]==b'\x89PNG\r\n\x1a\n','Invalid PNG '+name)
        width,height=struct.unpack('>II',data[16:24]);images.append({'name':name,'width':width,'height':height,'sha256':sha(path)})
after=snapshot();check(before==after,'Player evidence changed while independently reading')
result={'scope':__doc__,'playerPath':RUN.relative_to(ROOT).as_posix(),'buildIdentity':json.loads(r['buildIdentity']),
        'environment':r['environment'],'status':('partial_evidence_verified_native_display_pending' if partial else 'passed') if not errors else 'failed',
        'originalPlayerStatus':r['status'],'formalReleaseAccepted':not formal_failures,'formalReleaseFailures':formal_failures,
        'independentBuiltPdbAudit':sys.argv[4] if partial else None,'errors':errors,'sourceEvidenceStable':before==after,
        'trace':trace,'rawMetrics':metrics,'performance':p,'actualTargetRaycasts':len(pointers),'renderReadiness':readiness,'shopRefreshTransactions':shop_transactions,'syntheticInputChecks':input_checks,
        'displayChecks':r['displayChecks'],'screenshotsInspectedForDimensions':images,'files':before,
        'limitations':['PNG dimension/hash checks are not visual approval; see separate actual image review.','Synthetic routed keyboard is not physical keyboard/focus acceptance.','Mac and human acceptance are not inferred.']
        +(['Windows native display previously failed and is explicitly not executed in this restricted run. No full release pass is granted.'] if partial else [])}
target.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps({'status':result['status'],'errors':errors,'trace':trace,'frameP95':metrics.get('frameInterval',{}).get('p95'),'images':len(images)},ensure_ascii=False))
