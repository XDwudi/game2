"""Run offline delivery-tool tests with before/after source binding in reviewer-owned output."""
from pathlib import Path
import datetime,hashlib,json,os,subprocess,sys
ROOT=Path(__file__).resolve().parents[3];OUT=Path(__file__).resolve().parent
label=sys.argv[1];log=OUT/(label+'.log');proof=OUT/(label+'.json')
assert not log.exists() and not proof.exists()
paths=[ROOT/'tools'/x for x in ('package_v06.py','release_v06_manifest.py','publish_v06_release.py','v06_delivery.py','quality_check.py')]
paths+=sorted((ROOT/'tools/macos/v06').glob('*.py'))
paths+=[ROOT/'tools/macos/v06/VerifyMac.command']
def snapshot():return {p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in paths}
before=snapshot();start=datetime.datetime.now(datetime.timezone.utc).isoformat()
env=dict(os.environ,PYTHONDONTWRITEBYTECODE='1',TEMP=str(OUT/'unit-temp'),TMP=str(OUT/'unit-temp'))
cmd=[sys.executable,'-m','unittest','discover','-s','tools/macos/v06','-p','test*.py','-v']
r=subprocess.run(cmd,cwd=ROOT,env=env,stdout=subprocess.PIPE,stderr=subprocess.STDOUT,text=True,encoding='utf8',errors='replace')
log.write_text(r.stdout,encoding='utf8');after=snapshot()
data={'scope':__doc__,'startedUtc':start,'finishedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'command':cmd,
      'exitCode':r.returncode,'python':sys.version,'before':before,'after':after,'sourceStableDuringRun':before==after,
      'logSha256':hashlib.sha256(log.read_bytes()).hexdigest(),'actualUnityOrMacOrGithubExecuted':False}
proof.write_text(json.dumps(data,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(r.stdout);print('Source stable',before==after)
raise SystemExit(r.returncode)
