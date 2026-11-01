import hashlib,json,pathlib
R=pathlib.Path(__file__).resolve().parents[4]
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
out={'reviewer':'/root/v07_spec_audit','note':'Read-only record of copied actual captures. Edge metadata has no original PNG hash; these SHA values freeze the reviewed copies rather than asserting an unavailable producer hash.','batches':[]}
for stamp in ('021306','021533','021950'):
 b=read(R/f'evidence/v07/unity/nunit-20261101-{stamp}.binding.json');a=next(a for a in b['started']['assemblies'] if a['name']=='DiceCraft.Runtime');rows=[]
 for p in sorted((R/f'artifacts/cache/nunit-20261101-{stamp}-screens').glob('*.png')):
  m=read(p.with_suffix('.json'))
  viewed=p.stem=='S18-win' if stamp=='021306' else p.stem.endswith('font100' if stamp=='021533' else 'font150')
  rows.append({'path':str(p.relative_to(R)).replace('\\','/'),'sha256':sha(p),'metadataSha256':sha(p.with_suffix('.json')),'mvidMatches':m['mvid']==a['mvid'],'metadata':m,'visuallyOpened':viewed})
 paths=['DicePrototype/Assets/Game/UI/GameApp.Art07Guide.cs','DicePrototype/Assets/Game/Tests/PlayMode/Art07GuideLayoutPlayModeTests.cs'] if stamp!='021306' else ['DicePrototype/Assets/Game/UI/GameApp.Art07Results.cs','DicePrototype/Assets/Game/Tests/PlayMode/Art07EdgeScreensPlayModeTests.cs']
 sources=[]
 for path in paths:
  bound=next((f['sha256'] for f in b['started']['files'] if f['path']==path),None)
  sources.append({'path':path,'boundSha256':bound,'currentSha256':sha(R/path),'matchesBound':bound==sha(R/path)})
 out['batches'].append({'stamp':stamp,'runtime':a,'sources':sources,'screens':rows})
out['totals']={'inventory':sum(len(b['screens']) for b in out['batches']),'visuallyOpened':sum(s['visuallyOpened'] for b in out['batches'] for s in b['screens'])}
dest=pathlib.Path(__file__).parent/'guide-c36-rework-binding.json';dest.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'totals':out['totals'],'allMvidMatch':all(s['mvidMatches'] for b in out['batches'] for s in b['screens']),'sourceMatches':[(b['stamp'],all(s['matchesBound'] for s in b['sources'])) for b in out['batches']]},ensure_ascii=False))
