import hashlib,json,pathlib,xml.etree.ElementTree as ET
ROOT=pathlib.Path(__file__).resolve().parents[4]
BASE=ROOT/'artifacts/cache/v07-extra-screens-021213'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))
viewed={
'S11PurchaseReplacementAndFundSale':['replacement-net-income','replacement-new-instance','fund-sale-confirmation'],
'S11RefreshAndExpandOnlyTheirOwnShelves':['paid-refresh-before','expand-O05-after','expand-O06-after','free-refresh-before','free-refresh-1'],
'S13UpgradeTargetsCancelAndRandomRange':['upgrade-no-target','upgrade-selected-face','random-upgrade-range','whole-die-upgrade-preview','glass-with-smooth-face','upgrade-already-owned'],
'S13RemoteTargetsCancelAndSingleUse':['remote-empty','remote-selected','remote-after-real-roll'],
'S22RealSaveFaultRetryAbandonAndBackupRecovery':['save-error-expanded','save-retry-committed','save-abandoned-checkpoint','backup-recovery-dialog','backup-recovered-board']}
out={'reviewer':'/root/v07_spec_audit','scope':'Read-only evidence audit; no Unity execution; visual review only where marked','reports':[],'batches':[]}
for p in sorted(BASE.glob('*/extra-report.json')):
 d=read(p); rows=[]
 for s in d['screens']:
  f=p.parent/s['png']; meta=f.with_suffix('.json')
  rows.append({'id':s['id'],'png':str(f.relative_to(ROOT)).replace('\\','/'),'sha256':sha(f),'shaMatches':sha(f)==s['pngSha256'],'readOnlyCapture':s['unchanged'] and all(s[a]==s[b] for a,b in [('stateHashBefore','stateHashAfter'),('saveShaBefore','saveShaAfter'),('pendingShaBefore','pendingShaAfter')]),'viewed':s['id'] in viewed[p.parent.name.replace('PLAY_ART07_ExtraCapture_','')],'dimensions':[s['width'],s['height']],'fontScale':s['fontScale']})
 nonmut=[a for a in d['actions'] if not a['mutationAllowed']]
 out['reports'].append({'path':str(p.relative_to(ROOT)).replace('\\','/'),'sha256':sha(p),'runtimeMvid':d['runtimeMvid'],'coreMvid':d['coreMvid'],'testMvid':d['testMvid'],'runtimeAssemblySha256':d['runtimeAssemblySha256'],'sourceManifestSha256':sha(p.parent/'source-snapshot.sha256'),'sourceManifestMatches':sha(p.parent/'source-snapshot.sha256')==d['sourceManifestSha256'],'actionCount':len(d['actions']),'nonmutatingActions':len(nonmut),'nonmutatingActionsPreserved':all(a['stateBefore']==a['stateAfter'] and a['saveBefore']==a['saveAfter'] and a['beforeSequence']==a['afterSequence'] for a in nonmut),'screens':rows})
for stamp in ('021213','021306','021533','021950'):
 p=ROOT/f'evidence/v07/unity/nunit-20261101-{stamp}.binding.json';d=read(p);xml=ROOT/d['report']['path'];x=ET.parse(xml).getroot()
 currentDiff=[f['path'] for f in d['started']['files'] if not (ROOT/f['path']).exists() or sha(ROOT/f['path'])!=f['sha256']]
 out['batches'].append({'id':stamp,'bindingSha256':sha(p),'reportSha256':sha(xml),'reportShaMatches':sha(xml)==d['report']['sha256'],'result':x.attrib,'sourceFiles':len(d['started']['files']),'sourceStable':d['started']['files']==d['finished']['files'],'assembliesStable':d['started']['assemblies']==d['finished']['assemblies'],'assemblies':d['started']['assemblies'],'currentChangedFiles':currentDiff})
out['totals']={'screens':sum(len(r['screens']) for r in out['reports']),'viewed':sum(s['viewed'] for r in out['reports'] for s in r['screens']),'actions':sum(r['actionCount'] for r in out['reports']),'nonmutatingActions':sum(r['nonmutatingActions'] for r in out['reports'])}
dest=pathlib.Path(__file__).parent/'evidence-bindings.json';dest.write_text(json.dumps(out,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps({'totals':out['totals'],'checks':[{'name':r['path'],'manifest':r['sourceManifestMatches'],'png':all(s['shaMatches'] for s in r['screens']),'readOnly':all(s['readOnlyCapture'] for s in r['screens']),'nonmutating':r['nonmutatingActionsPreserved']} for r in out['reports']],'batches':[{'id':b['id'],'sha':b['reportShaMatches'],'stable':b['sourceStable'] and b['assembliesStable'],'sources':b['sourceFiles'],'currentDiff':len(b['currentChangedFiles'])} for b in out['batches']]},ensure_ascii=False))
