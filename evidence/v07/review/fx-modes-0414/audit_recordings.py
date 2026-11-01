import concurrent.futures, hashlib, json, pathlib, subprocess
from PIL import Image

ROOT = pathlib.Path(__file__).resolve().parents[4]
BASE = ROOT / 'artifacts/cache/v07-score-modes-0414'
OUT = pathlib.Path(__file__).resolve().parent
FF = ROOT / 'artifacts/cache/v07/tools/krita/krita-x64-5.3.4/bin'
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def audit(mode):
    p=BASE/mode; rec=read(p/'recording.json'); rows=[json.loads(x) for x in (p/'frames.jsonl').read_text(encoding='utf-8-sig').splitlines() if x.strip()]
    frames=[]
    for r in rows:
        f=p/'frames'/pathlib.Path(r['file']).name
        actual=sha(f)
        with Image.open(f) as im: dims=list(im.size)
        frames.append({'index':r['index'],'path':f.relative_to(ROOT).as_posix(),'sha256':actual,'matches':actual==r['pngSha256'],'dimensions':dims})
    post=[r for r in rows if r['index']>=15]
    video=p/'visual-silent.mp4'
    probe=subprocess.run([str(FF/'ffprobe.exe'),'-v','error','-count_frames','-show_streams','-show_format','-of','json',str(video)],capture_output=True,text=True)
    decode=subprocess.run([str(FF/'ffmpeg.exe'),'-hide_banner','-nostdin','-v','error','-i',str(video),'-f','null','-'],capture_output=True,text=True)
    (OUT/(mode+'-decode.log')).write_text(decode.stdout+decode.stderr,encoding='utf-8')
    before=read(p/'state-before-submit.json'); after=read(p/'state-committed.json')
    result={'mode':mode,'fixture':read(p/'fixture.json')['fixture'],'recordingFixture':rec['fixture'],'recordingSha256':sha(p/'recording.json'),'framesJsonlSha256':sha(p/'frames.jsonl'),'runtimeMvid':rec['runtimeAssemblyMvid'],'coreMvid':rec['coreAssemblyMvid'],'frames':frames,'frameCount':len(rows),'indicesContinuous':[r['index'] for r in rows]==list(range(len(rows))),'postCommitDistinct':{key:sorted(set(str(r.get(key)) for r in post)) for key in ['stateHash','saveSha256','normalSaveSha256','receiptId','score','chainCount','comboTier']},'maxReportedHighContrastArea':max(r.get('highContrastAreaRatio',0) for r in rows),'maxReportedRedBlueSwapArea':max(r.get('redBlueSwapAreaRatio',0) for r in rows),'rngBeforeSubmit':{k:before.get(k) for k in ['diceRngState','shopRngState','effectRngState']},'rngCommitted':{k:after.get(k) for k in ['diceRngState','shopRngState','effectRngState']},'cycles':rec['cycles'],'normalSaveUnchanged':rec['normalSaveUnchanged'],'restoredTiming':rec['restoredTiming'],'restoredPreferences':rec['restoredPreferences'],'sourceTickCalls':rec['sourceTickCalls'],'nonblockingTickCalls':rec['nonblockingTickCalls'],'videoSha256':sha(video),'videoProbe':json.loads(probe.stdout) if probe.returncode==0 else probe.stderr,'videoProbeExit':probe.returncode,'videoDecodeExit':decode.returncode,'videoDecodeError':decode.stderr,'inputs':{n:sha(p/n) for n in ['fixture.json','state-before-submit.json','state-committed.json']}}
    (OUT/(mode+'-audit.json')).write_text(json.dumps(result,ensure_ascii=False,indent=2),encoding='utf-8')
    return {k:result[k] for k in ['mode','fixture','frameCount','indicesContinuous','postCommitDistinct','maxReportedHighContrastArea','maxReportedRedBlueSwapArea','rngBeforeSubmit','rngCommitted','videoDecodeExit']}
if __name__=='__main__':
    modes=sorted(p.name for p in BASE.iterdir() if p.is_dir())
    with concurrent.futures.ThreadPoolExecutor(max_workers=3) as pool: results=list(pool.map(audit,modes))
    (OUT/'audit-summary.json').write_text(json.dumps(results,ensure_ascii=False,indent=2),encoding='utf-8')
    print(json.dumps(results,ensure_ascii=False,indent=2))
