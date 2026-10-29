"""Read-only independent artifact checks; never launches Unity or edits source evidence."""
import csv, hashlib, json, math, statistics, xml.etree.ElementTree as ET
from collections import Counter
from pathlib import Path
from PIL import Image, ImageStat

root = Path(__file__).resolve().parents[2]
run = root / 'evidence/runs/player/player-20261029-005142-873bffd3'
out = root / 'evidence/review'
sha = lambda p: hashlib.sha256(p.read_bytes()).hexdigest()
read = lambda p: json.loads(p.read_text(encoding='utf-8-sig'))
report = read(run / 'player-report.json')
checks = []

def check(name, ok, **detail):
    checks.append(dict(name=name, passed=bool(ok), **detail))
    print(('PASS' if ok else 'FAIL'), name)
    if not ok: raise AssertionError(name)

trace = read(root / 'evidence/runs/normal-win.json')
replay = [json.loads(s) for s in (run / 'normal-replay.jsonl').read_text(encoding='utf-8-sig').splitlines() if s.strip()]
fields = ['kind','phase','actionSequence','submissionScore','stageScore','target','gold','stageIndex','rollsRemaining','remainingSubmissions']
check('actual_97_step_records_equal_reference_trace', len(replay)==len(trace['steps'])==97 and all(all(r[k]==t[k] for k in fields) for r,t in zip(replay,trace['steps'])))
check('records_contiguous_not_debug_saved_and_rendered_equal', all(r['index']==i and r['actionSequence']==i+1 and not r['isDebug'] and r['savedStateMatches'] and r['renderedStateMatches'] for i,r in enumerate(replay)))
check('trace_and_config_hash_binding', sha(root/'evidence/runs/normal-win.json')==report['traceSha256'] and sha(root/'DicePrototype/Assets/Game/Data/P0Config.json')==report['configSha256'])
check('normal_save_unchanged_by_debug_stress', sha(run/'normal-save/active-run.json')==report['normalSaveSha256BeforeStress'])
normal,debug = read(run/'normal-final-state.json'),read(run/'debug-final-state.json')
check('normal_debug_run_ids_and_flags_isolated', not normal['isDebug'] and debug['isDebug'] and normal['runId']!=debug['runId'])

animation=report['animationChecks']
landed=[x for x in animation if x['check']=='landed']
check('31_landed_roll_records_have_exact_final_trs_and_upface', len(landed)==31 and all(d['positionError']==0 and d['rotationErrorDegrees']==0 and d['scaleError']==0 and d['faceNormalUpDot']>=.999 for a in landed for d in a['dice']))
motion=next(a for a in animation if a['check']=='first-normal-roll-motion')
check('first_roll_motion_032_045_different', .3<=motion['sampleASeconds']<motion['sampleBSeconds']<=.5 and all(d['rotationDeltaDegrees']>1 or d['positionDelta']>.001 for d in motion['dice']))

# All images are measured for integrity, but only the named subset was visually inspected.
visually_reviewed={
 'normal-selecting-1280x720.png','normal-selecting-1920x1080.png',
 'normal-shop-1280x720.png','normal-shop-2560x1440.png',
 'normal-die-detail-permanent-preview-1280x720.png','debug-die-detail-permanent-preview-2560x1440.png',
 'normal-score-preview-1280x720.png','normal-score-preview-bottom-2560x1440.png',
 'debug-score-preview-1280x720.png','debug-score-preview-bottom-1280x720.png','debug-score-preview-bottom-2560x1440.png',
 'debug-ten-dice-unlocked-pool-1280x720.png','debug-ten-dice-unlocked-pool-2560x1440.png',
 'debug-stress-capacity-1280x720.png','debug-stress-capacity-1920x1080.png','normal-run-won.png'
}
images=[]
for p in sorted(run.glob('*.png')):
    with Image.open(p) as im:
        rgb=im.convert('RGB'); stat=ImageStat.Stat(rgb)
        images.append(dict(file=p.name,sha256=sha(p),size=list(im.size),rgbExtrema=rgb.getextrema(),rgbMean=stat.mean,visuallyInspected=p.name in visually_reviewed))
black=[x['file'] for x in images if all(hi==0 for lo,hi in x['rgbExtrema'])]
check('known_two_black_captures_reproduced',set(black)=={'main-menu-1920x1080.png','normal-selecting-1920x1080.png'},black=black)

with (run/'performance-frames.csv').open(newline='',encoding='utf-8-sig') as f: raw_rows=list(csv.DictReader(f))
# Probe deliberately excludes the first partial mid-frame interval from statistics,
# while preserving that raw sample. Do the same explicit selection for recomputation.
rows=raw_rows[1:]
count=len(rows)
check('raw_minus_initial_partial_frame_count_matches_report',count==report['performance']['frameCount']==286832,rawRows=len(raw_rows),excludedInitialPartialFrame=raw_rows[0])
check('raw_frame_ids_contiguous',all(int(b['frame'])==int(a['frame'])+1 for a,b in zip(raw_rows,raw_rows[1:])))
ui=sum(bool(r['ui_build_ms']) for r in rows)
gpu=[float(r['gpu_ms']) for r in rows if r['gpu_ms']]
elapsed=[float(r['elapsed_s']) for r in rows]
anomalies=[dict(frame=int(r['frame']),elapsed=float(r['elapsed_s']),gpuMs=float(r['gpu_ms'])) for r in rows if r['gpu_ms'] and float(r['gpu_ms'])>180000]
check('ui_raw_coverage_matches_report',ui==report['performance']['uiRebuild']['samples'] and ui/count<.95,samples=ui,coverage=ui/count)
check('gpu_impossible_sample_preserved_in_raw',len(anomalies)>0 and math.isclose(max(gpu),report['performance']['gpu']['max'],rel_tol=1e-12),anomalies=anomalies)
check('report_correctly_does_not_claim_complete_pass',report['status']=='measured_with_gaps' and report['exitCode']==2)

unity=[]
for jn,xn,expected in [('editmode-04.json','nunit-20261029-004842.xml',268),('playmode-06.json','nunit-20261029-010244.xml',16)]:
    jp,xp=root/'evidence/unity'/jn,root/'evidence/unity'/xn
    j=read(jp);x=ET.parse(xp).getroot(); cases=x.findall('.//test-case')
    check(jn+'_nonzero_all_pass_and_same_test_names',len(cases)==expected and j['summary']==dict(total=expected,passed=expected,failed=0,skipped=0,inconclusive=0) and all(c.attrib['result']=='Passed' for c in cases) and Counter(c.attrib['fullname'] for c in cases)==Counter(r['FullName'] for r in j['results']))
    unity.append(dict(json=jn,xml=xn,jsonSha256=sha(jp),xmlSha256=sha(xp),started=x.attrib['start-time'],ended=x.attrib['end-time'],total=expected,cases=[c.attrib['fullname'] for c in cases]))

result=dict(reviewerRanUnity=False,playerReportSha256=sha(run/'player-report.json'),environment=report['environment'],checks=checks,images=images,metrics=dict(frameCount=count,elapsedFirst=elapsed[0],elapsedLast=elapsed[-1],framesPerSecond=count/report['performance']['sampleSeconds'],uiCoverage=ui/count,gpuAnomalies=anomalies),unityReports=unity,conclusion='Artifact consistency checked. Visual defects and performance gaps remain; not overall acceptance.')
(out/'player02-independent-artifact-audit.json').write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
print('Artifact checks:',len(checks),'PNG integrity:',len(images),'Visually inspected:',len(visually_reviewed))
