from pathlib import Path
import datetime
import hashlib
import json
import os
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[3]
OUT=Path(__file__).resolve().parent
label=sys.argv[1]
report=OUT/f'managed-rules-{label}.json'
if report.exists():raise SystemExit('Refusing to overwrite retained report '+str(report))
patterns=['DicePrototype/Assets/Game/Core/Rules/*.cs','DicePrototype/Assets/Game/Core/Session/*.cs','DicePrototype/Assets/Game/Data/P0Config.json',
          'DicePrototype/Assets/Game/Runtime/AtomicSaveStore.cs',
          'evidence/v06/review/harness/*.cs','evidence/v06/review/harness/*.csproj',
          'evidence/v06/review/input-json-1.json','evidence/v06/review/review-vectors.json','docs/specs/P0_v0.6.*']
patterns+=['evidence/v06/session/normal-run-trace.json','evidence/v06/session/normal-run-final-state.json',
           'evidence/v06/session/debug-24-trace.json','evidence/v06/session/debug-24-final-state.json','evidence/v06/session/source_manifest.json']
def snapshot():
    return {str(path.relative_to(ROOT)).replace('\\','/'):hashlib.sha256(path.read_bytes()).hexdigest()
            for pattern in patterns for path in sorted(ROOT.glob(pattern))}
before=snapshot()
env=dict(os.environ,DOTNET_CLI_HOME=str(OUT/'.dotnet'),DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_NOLOGO='1')
command=['dotnet','run','--project','evidence/v06/review/harness/ReviewHarness.csproj','--no-launch-profile','--',str(report.relative_to(ROOT)).replace('\\','/')]
started=datetime.datetime.now(datetime.timezone.utc).isoformat()
result=subprocess.run(command,cwd=ROOT,env=env,text=True,encoding='utf-8',errors='replace',stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
(OUT/f'managed-rules-{label}.log').write_text(result.stdout,encoding='utf-8')
after=snapshot()
binding={'startedUtc':started,'completedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
         'command':command,'cwd':str(ROOT),'scope':'Reviewer rules, focused sessions and AtomicSaveStore disk IO with System.Text.Json adapter. Not Unity execution.',
         'dotnet':subprocess.check_output(['dotnet','--version'],cwd=ROOT,env=env,text=True).strip(),
         'gitHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
         'exitCode':result.returncode,'sourceStableDuringRun':before==after,'before':before,'after':after,
         'reportSha256':hashlib.sha256(report.read_bytes()).hexdigest() if report.exists() else None}
(OUT/f'managed-rules-{label}-binding.json').write_text(json.dumps(binding,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(result.stdout)
print('Source stable:',before==after,'Bound files:',len(before))
raise SystemExit(result.returncode)
