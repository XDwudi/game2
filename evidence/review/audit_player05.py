import csv,hashlib,json,math,statistics
from pathlib import Path
from PIL import Image
root=Path(__file__).resolve().parents[2];run=root/'evidence/runs/player/player-20261029-015104-31d8ee23';out=root/'evidence/review'
read=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
r=read(run/'player-report.json');checks=[]
def check(name,ok,**details):
 checks.append(dict(name=name,passed=bool(ok),**details));print('PASS' if ok else 'FAIL',name)
 if not ok:raise AssertionError(name)
trace=read(root/'evidence/runs/normal-win.json');steps=[json.loads(s) for s in (run/'normal-replay.jsonl').read_text(encoding='utf-8-sig').splitlines() if s.strip()]
keys=['kind','phase','actionSequence','submissionScore','stageScore','target','gold','stageIndex','rollsRemaining','remainingSubmissions']
check('normal97_trace_equal',len(steps)==len(trace['steps'])==97 and all(all(a[k]==b[k] for k in keys) for a,b in zip(steps,trace['steps'])))
check('normal97_saved_rendered_non_debug',all(s['index']==i and s['actionSequence']==i+1 and s['savedStateMatches'] and s['renderedStateMatches'] and not s['isDebug'] for i,s in enumerate(steps)))
check('normal_save_isolated_unchanged',sha(run/'normal-save/active-run.json')==r['normalSaveSha256BeforeStress'] and not read(run/'normal-final-state.json')['isDebug'] and read(run/'debug-final-state.json')['isDebug'])
pdb=read(out/'player05-pdb-independent.json')
check('actual_runtime_mvid_equals_report',next(a['mvid'] for a in pdb['assemblies'] if a['name']=='DiceCraft.Runtime')==r['probeAssemblyMvid'])
check('config_trace_hashes',sha(root/'evidence/runs/normal-win.json')==r['traceSha256'] and sha(root/'DicePrototype/Assets/Game/Data/P0Config.json')==r['configSha256'])
landed=[a for a in r['animationChecks'] if a['check']=='landed']
check('31_final_pose_records_consistent',len(landed)==31 and all(d['positionError']==0 and d['rotationErrorDegrees']==0 and d['scaleError']==0 and d['faceNormalUpDot']>.999 for a in landed for d in a['dice']))
seen={'normal-selecting-1920x1080.png','debug-die-detail-permanent-preview-1280x720.png','debug-ten-dice-unlocked-pool-2560x1440.png','debug-score-preview-1280x720.png','debug-score-preview-middle-1280x720.png','debug-score-preview-bottom-1280x720.png','normal-shop-1920x1080.png','debug-stress-final.png','normal-run-won.png'}
images=[]
for p in sorted(run.glob('*.png')):
 with Image.open(p) as im:images.append(dict(file=p.name,sha256=sha(p),size=list(im.size),rgbExtrema=im.convert('RGB').getextrema(),visuallyInspected=p.name in seen))
check('accepted_images_nonblack_native_size',all(all(any(hi>0 for lo,hi in i['rgbExtrema']) for i in images if i['file']==d['file']) and d['width']==d['screenWidth'] and d['height']==d['screenHeight'] for d in r['screenshotDetails'] if d['accepted']),accepted=len(r['screenshots']),total=len(images),visuallyInspected=len(seen))
check('no_rejected_frames_or_unity_errors',not r['rejectedScreenshots'] and not r['unityErrors'])
with (run/'performance-frames.csv').open(newline='',encoding='utf-8-sig') as f:raw=list(csv.DictReader(f))
rows=raw[1:];perf=r['performance'];n=len(rows)
check('count_duration_contiguous',n==perf['frameCount'] and perf['sampleSeconds']>=180 and all(int(b['frame'])==int(a['frame'])+1 for a,b in zip(raw,raw[1:])))
metric_keys={'interval_ms':'frameInterval','main_thread_ms':'mainThread','render_thread_ms':'renderThread','frame_timing_cpu_ms':'frameTimingCpu','gpu_ms':'gpu','gc_alloc_bytes':'gcAllocated','gc_used_bytes':'gcUsed','ui_build_ms':'uiRebuild','ui_build_marker_calls':'uiRebuildMarkerCalls','mono_used_bytes':'monoUsed'}
metrics={}
for csvkey,reportkey in metric_keys.items():
 vals=sorted(float(x[csvkey]) for x in rows if x[csvkey] and math.isfinite(float(x[csvkey])));m=perf[reportkey]
 calc=dict(samples=len(vals),validFrameCoverage=len(vals)/n,p95=vals[math.ceil(len(vals)*.95)-1] if vals else None,max=max(vals) if vals else None,mean=statistics.mean(vals) if vals else None)
 check('raw_metric_'+reportkey,m['samples']==calc['samples'] and math.isclose(m['validFrameCoverage'],calc['validFrameCoverage'],rel_tol=1e-12) and all(math.isclose(m[k],calc[k],rel_tol=1e-10,abs_tol=1e-10) for k in ['p95','max','mean']))
 metrics[reportkey]=calc
check('UI_stream_no_stale_batches_or_gap',all(int(x['ui_sample_batch']) in (0,1) for x in rows) and perf['uiRebuild']['validFrameCoverage']==1 and perf['uiRebuild']['unavailableReason'] is None)
check('GPU_complete_no_invalid',perf['gpuInvalidSamples']==0 and perf['gpu']['validFrameCoverage']>=.95 and perf['gpuDuplicateTimestamps']==0 and not r['graphicsTimingWarnings'])
check('complete_capture_correctly_reports_budget_failure',perf['frameBufferGrowthEvents']==0 and perf['frameBufferInitialCapacity']==perf['frameBufferFinalCapacity']==60000 and perf['frameInterval']['p95']>16.7 and r['status']=='budget_exceeded' and r['exitCode']==3)
(out/'player05-independent-artifact-audit.json').write_text(json.dumps(dict(reviewerRanUnity=False,playerReportSha256=sha(run/'player-report.json'),environment=r['environment'],checks=checks,images=images,computedMetrics=metrics,conclusion='Functional records and inspected visuals accepted for build06. Capture is complete, but measured frame-time P95 exceeds the unchanged 16.7 ms budget. AC-PERF is failed and requires rework.'),ensure_ascii=False,indent=2),encoding='utf-8')
print('Completed',len(checks),'artifact checks. No overall performance acceptance.')
