"""Small independent managed sequences; source snapshots and real output bound."""
from pathlib import Path
import hashlib,json,os,subprocess,sys

ROOT=Path(__file__).resolve().parents[3]
OUT=Path(__file__).resolve().parent
label=sys.argv[1]
report=OUT/(label+'.json')
assert not report.exists()
paths=list((ROOT/'DicePrototype/Assets/Game/Core/Rules').glob('*.cs'))+list((ROOT/'DicePrototype/Assets/Game/Core/Session').glob('*.cs'))
paths += [ROOT/'DicePrototype/Assets/Game/Data/P0Config.json',OUT/'sequences/Program.cs',OUT/'sequences/SequenceReview.csproj']
def snapshot():return {p.relative_to(ROOT).as_posix():hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(paths)}
before=snapshot()
env=os.environ.copy();env.update(DOTNET_CLI_HOME=str(OUT/'.dotnet'),DOTNET_NOLOGO='1',DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1')
result=subprocess.run(['dotnet','run','--project',str(OUT/'sequences/SequenceReview.csproj'),'--',str(report)],cwd=ROOT,env=env,capture_output=True,text=True,encoding='utf8',errors='replace')
after=snapshot()
(OUT/(label+'.log')).write_text(result.stdout+'\n'+result.stderr,encoding='utf8')
bound={'scope':'Five exact state-sequence tests only; no Unity/Player launched or full core suite repeated',
       'sourceBefore':before,'sourceAfter':after,'sourceStable':before==after,'exitCode':result.returncode,
       'reportSha256':hashlib.sha256(report.read_bytes()).hexdigest() if report.exists() else None,
       'passed':result.returncode==0 and before==after}
(OUT/(label+'.binding.json')).write_text(json.dumps(bound,indent=2)+'\n',encoding='utf8')
print(result.stdout.strip());print('sourceStable='+str(before==after));sys.exit(0 if bound['passed'] else 1)
