import csv,hashlib,json,pathlib,collections,datetime
ROOT=pathlib.Path(__file__).resolve().parents[3]
OUT=pathlib.Path(__file__).parent
base=ROOT/'evidence/v07/resources/v07-resource-player-dca7a26-0243/diagnostic'
report=json.loads((base/'resource-report.json').read_text(encoding='utf-8-sig'))
with (base/'resource-frames.csv').open(encoding='utf-8-sig',newline='') as f: rows=list(csv.DictReader(f))
facts={'schema':'v07-drawcall-readonly-analysis-1','utc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'buildGuid':report['buildGuid'],'runtimeMvid':report['runtimeMvid'],'identity':json.loads(report['buildIdentity']),'display':{k:report[k] for k in ['width','height','displayMode','graphicsApi']},'phases':[]}
for phase in report['phases']:
    data=[r for r in rows if int(r['phase'])==phase['index']]
    counts=[int(r['draws']) for r in data if r['draws']!='']
    steady=[r for r in data if float(r['realtime_s'])-phase['realtime']>=1.0]
    facts['phases'].append({**phase,'rowCount':len(data),'missingDraws':len(data)-len(counts),'drawCountHistogram':dict(collections.Counter(counts)),'minDraws':min(counts) if counts else None,'maxDraws':max(counts) if counts else None,'rowsAbove180':sum(v>180 for v in counts),'afterOneSecondDrawHistogram':dict(collections.Counter(int(r['draws']) for r in steady if r['draws']!='')),'tailTen':data[-10:]})
facts['allRows']=len(rows)
facts['above180Total']=sum(r['draws']!='' and int(r['draws'])>180 for r in rows)
facts['snapshotCoverage']=[{'name':s['name'],'frame':s['frame'],'phase':s['phase'],'textures':len(s['textures']),'fonts':len(s['fonts']),'meshes':len(s['meshes'])} for s in report['snapshots']]
facts['actualFontsLoaded']=report['snapshots'][0]['fonts']
savepath=base/'isolated-saves/debug-run.json'
envelope=json.loads(savepath.read_text(encoding='utf-8-sig'))
assert hashlib.sha256(envelope['payload'].encode()).hexdigest()==envelope['sha256']
saved=json.loads(envelope['payload']); receipts=saved['processedReceipts']
assert [r['actionSequence'] for r in receipts]==[1,2,3,4,5]
scene=receipts[0]['command']['debugScene']; submitted=receipts[1]['submission']['snapshot']; settlement=receipts[2]['settlement']
facts['actualSavedRoute']={'receipts':[{'sequence':r['actionSequence'],'commandKind':r['command']['kind'],'phase':r['phase'],'effects':[e['kind'] for e in r['effects']]} for r in receipts],
 'fixture':scene['note'],'diceCount':len(scene['dice']),'capacity':scene['capacityOverride'],'controllers':[{'definitionId':c['definitionId'],'enchantment':c['enchantmentId']} for c in scene['controllers']],
 'inventory':[i['contentId'] for i in scene['inventory']],'submittedDice':[{'dieId':d['dieId'],'pip':d['currentPip'],'color':d['currentColor'],'isGold':d['isGold'],'isGlass':d['isGlass']} for d in submitted['dice']],
 'settlement':{k:settlement[k] for k in ['goldBefore','goldAfter','baseReward','unusedSubmissionReward','interest','remainingSubmissions','score','target']},
 'note':'Persisted original receipts survive subsequent MaximumTable Apply. This binds actual earlier gameplay fixture, not render-material/Canvas inventory.'}
(OUT/'numeric.json').write_text(json.dumps(facts,indent=2,ensure_ascii=False),encoding='utf-8')
for p in facts['phases']:
 print(p['index'],p['name'],'rows',p['rowCount'],'draws',p['drawCountHistogram'],'after1sec',p['afterOneSecondDrawHistogram'])
print('above180',facts['above180Total'])
