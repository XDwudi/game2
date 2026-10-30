from pathlib import Path
import datetime
import hashlib
import json
import os
import subprocess
import sys

root=Path(__file__).resolve().parents[3]
out=root/'evidence/v05/review'
label=sys.argv[1]
report=out/f'independent-managed-{label}.json'
if report.exists(): raise SystemExit(f'Refusing to overwrite retained report: {report}')
patterns=['DicePrototype/Assets/Game/Core/Rules/*.cs','DicePrototype/Assets/Game/Core/Session/*.cs',
          'DicePrototype/Assets/Game/Tests/EditMode/Rules/*.cs','DicePrototype/Assets/Game/Tests/EditMode/Session/*.cs',
          'DicePrototype/Assets/Game/Runtime/AtomicSaveStore.cs','DicePrototype/Assets/Game/Data/*.json',
          'evidence/v05/review/harness/*.cs','evidence/v05/review/harness/*.csproj',
          'evidence/v05/review/independent-vectors.json','evidence/v05/review/generate_independent_vectors.py',
          'docs/specs/P0_v0.5.md','docs/specs/P0_v0.5.docx']
def snapshot():
    return {str(p.relative_to(root)).replace('\\','/'):hashlib.sha256(p.read_bytes()).hexdigest()
            for pattern in patterns for p in sorted(root.glob(pattern))}
before=snapshot()
env=dict(os.environ,DOTNET_CLI_HOME=str(out/'.dotnet'),DOTNET_SKIP_FIRST_TIME_EXPERIENCE='1',DOTNET_NOLOGO='1')
cmd=['dotnet','run','--project','evidence/v05/review/harness/ReviewHarness.csproj','--no-launch-profile','--',str(report.relative_to(root)).replace('\\','/')]
started=datetime.datetime.now(datetime.timezone.utc).isoformat()
result=subprocess.run(cmd,cwd=root,env=env,text=True,encoding='utf-8',errors='replace',stdout=subprocess.PIPE,stderr=subprocess.STDOUT)
(out/f'independent-managed-{label}.log').write_text(result.stdout,encoding='utf-8')
after=snapshot()
binding={'startedUtc':started,'completedUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),
         'command':cmd,'cwd':str(root),'scope':'Managed NUnit assertion preflight and reviewer real file IO using System.Text.Json adapter. Not Unity execution.',
         'dotnet':subprocess.check_output(['dotnet','--version'],cwd=root,env=env,text=True).strip(),
         'gitHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=root,text=True).strip(),
         'exitCode':result.returncode,'sourceStableDuringRun':before==after,'before':before,'after':after,
         'reportSha256':hashlib.sha256(report.read_bytes()).hexdigest() if report.exists() else None}
(out/f'independent-managed-{label}-binding.json').write_text(json.dumps(binding,ensure_ascii=False,indent=2),encoding='utf-8')
print(result.stdout)
print('Source snapshot stable:',before==after,'Files:',len(before))
raise SystemExit(result.returncode)
