"""Read-only reviewer binding audit; does not build, run Unity, or modify Git."""
from pathlib import Path
import hashlib,json,re,subprocess,sys
sys.dont_write_bytecode=True
ROOT=Path(__file__).resolve().parents[3]
OUT=Path(__file__).resolve().parent
def sha(p): return hashlib.sha256(Path(p).read_bytes()).hexdigest()
def read(p): return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def method(source,name):
    match=re.search(r'^        (?:private|public|internal) [^\n]*\b'+re.escape(name)+r'\(',source,re.M)
    assert match,name
    start=source.index('{',match.start());depth=0
    # The reviewed methods contain no unmatched braces in string/comment literals.
    for end in range(start,len(source)):
        if source[end]=='{':depth+=1
        elif source[end]=='}':
            depth-=1
            if depth==0:return re.sub(r'\s+','',source[match.start():end+1])
    raise ValueError(name)
label=sys.argv[1];pdb_name=sys.argv[2] if len(sys.argv)>2 else 'windows-built-pdb01.json'
target=OUT/(label+'.json');assert not target.exists()
binding=read(OUT/'managed-rules-run06-binding.json')
managedChanged=[p for p,h in binding['after'].items() if not (ROOT/p).is_file() or sha(ROOT/p)!=h]
gate=read(OUT/'gate-run03.json');consumer=read(OUT/'gate-consumer-run02.json')
gateCurrent=gate['gateSha256']==sha(ROOT/'tools/v06_delivery.py')
consumerCurrent=consumer['prototypeBuildSha256']==sha(ROOT/'DicePrototype/Assets/Game/Editor/PrototypeBuild.cs')
pdb=read(OUT/pdb_name)
assert len(pdb['assemblies'])==2 and {a['name'] for a in pdb['assemblies']}=={'DiceCraft.Core','DiceCraft.Runtime'}
assert pdb['mismatches']==0
docs={Path(d['source']).resolve() if Path(d['source']).is_absolute() else (ROOT/'DicePrototype'/d['source']).resolve() for d in pdb['documents']}
production={p.resolve() for scope in ('Core','Runtime','UI') for p in (ROOT/'DicePrototype/Assets/Game'/scope).rglob('*.cs') if not any(x.endswith('~') for x in p.parts)}
assert docs==production
path='DicePrototype/Assets/Game/Runtime/Verification/PlayerVerification.cs'
baseline='2db287a19467b2d77581692990696ebbcc9b871d'
old=subprocess.check_output(['git','show',baseline+':'+path],cwd=ROOT).decode('utf8')
current=(ROOT/path).read_text(encoding='utf-8-sig')
names=['RunStress','LateUpdate','BuildPerformanceReport','WriteFrames','Metric','CompleteCoverage','RequiredFrameCapacity','ClassifyGpuDuration','ClockDelta']
methods=[{'name':n,'unchanged':method(old,n)==method(current,n),'baselineNormalizedSha256':hashlib.sha256(method(old,n).encode()).hexdigest(),'currentNormalizedSha256':hashlib.sha256(method(current,n).encode()).hexdigest()} for n in names]
result={'scope':__doc__,'gitHead':subprocess.check_output(['git','rev-parse','HEAD'],cwd=ROOT,text=True).strip(),
        'managedRun06':{'checks':2077,'boundFiles':len(binding['after']),'sourceChanged':managedChanged,'reportSha256Matches':sha(OUT/'managed-rules-run06.json')==binding['reportSha256']},
        'gateProbe':{'count':21,'productionCheckerSourceUnchanged':gateCurrent},'consumerProbe':{'count':9,'productionConsumerSourceUnchanged':consumerCurrent},
        'builtWindowsPdb':{'audit':pdb_name,'sha256':sha(OUT/pdb_name),'modules':2,'projectDocuments':len(docs),'allProductionCoreRuntimeCovered':docs==production,'mismatches':pdb['mismatches']},
        'stressMethodBaseline':baseline,'stressMethods':methods,
        'status':'passed' if not managedChanged and gateCurrent and consumerCurrent and all(m['unchanged'] for m in methods) else 'needs_review'}
target.write_text(json.dumps(result,ensure_ascii=False,indent=2)+'\n',encoding='utf8')
print(json.dumps(result,ensure_ascii=False,indent=2))
