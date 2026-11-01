"""Inventory all 224 planned records. Presence is never verification."""
from pathlib import Path
import json,hashlib,datetime
ROOT=Path(__file__).resolve().parents[2];plan=json.loads((ROOT/'docs/specs/v07/dice_art_v0_7_manifest.json').read_text(encoding='utf8'))
mappings_path=ROOT/'ArtSource/Art07/path_mappings.json';mappings=json.loads(mappings_path.read_text(encoding='utf8')) if mappings_path.exists() else {'records':[]};overrides={m['assetId']:m for m in mappings['records']}
def file(path):
 p=ROOT/('DicePrototype/'+path if path.startswith('Assets/') else path)
 return {'path':path,'exists':p.is_file(),'sha256':hashlib.sha256(p.read_bytes()).hexdigest() if p.is_file() else None,'bytes':p.stat().st_size if p.is_file() else 0}
records=[]
for a in plan['sharedResources']['assets']:
 mapping=overrides.get(a['assetId']);src=file(mapping['sourcePath'] if mapping else a['sourcePath']);runtime=file(mapping['runtimePath'] if mapping else a['runtimePath']);complete=src['exists'] and runtime['exists']
 records.append({'assetId':a['assetId'],'category':a['category'],'assetType':a['assetType'],'optional':a['assetId']=='dice_body_lod_optional','status':'generated_pending_import_review' if complete else 'source_or_runtime_partial' if src['exists'] or runtime['exists'] else 'planned','plannedSourcePath':a['sourcePath'],'plannedRuntimePath':a['runtimePath'],'pathMappingApplied':mapping is not None,'mappingEvidence':'ArtSource/Art07/path_mappings.json' if mapping else None,'source':src,'runtime':runtime,'importEvidence':None,'verificationEvidence':None})
for c in plan['contentResources']:
 src=file(c['iconSource']);runtime=file(c['iconExport']);complete=src['exists'] and runtime['exists']
 records.append({'assetId':c['icon_id'],'contentId':c['id'],'optional':False,'status':'generated_pending_import_review' if complete else 'source_or_runtime_partial' if src['exists'] or runtime['exists'] else 'planned','source':src,'runtime':runtime,'importEvidence':None,'verificationEvidence':None})
counts={s:sum(r['status']==s for r in records) for s in sorted({r['status'] for r in records})}
result={'artVersion':'art0.7.0','snapshotUtc':datetime.datetime.now(datetime.timezone.utc).isoformat(),'plannedRecords':len(records),'requiredRecords':sum(not r['optional'] for r in records),'counts':counts,'warning':'File/hash inventory only. Existence or .meta does not mark imported or verified. Root must bind actual Unity and visual evidence separately.','assets':records}
(ROOT/'ArtSource/Art07/production_inventory.json').write_text(json.dumps(result,indent=2),encoding='utf8');print(json.dumps(counts))
gaps=[r for r in records if not r['optional'] and r['status']!='generated_pending_import_review'];grouped={}
for a in gaps:
 grouped.setdefault(a.get('assetType','content_icon'),[]).append({'assetId':a['assetId'],'plannedSourcePath':a.get('plannedSourcePath',a['source']['path']),'plannedRuntimePath':a.get('plannedRuntimePath',a['runtime']['path']),'resolvedSourcePath':a['source']['path'],'resolvedRuntimePath':a['runtime']['path'],'sourceExists':a['source']['exists'],'runtimeExists':a['runtime']['exists'],'requiredAction':'Root create and bind real runtime asset or document actual implementation mapping; no empty stand-ins.'})
(ROOT/'ArtSource/Art07/required_gaps.json').write_text(json.dumps({'artVersion':'art0.7.0','requiredCount':223,'remainingExactMappedFileGaps':len(gaps),'inventoryTimestamp':result['snapshotUtc'],'caution':'File existence only; generated assets still require actual import, behavior and visual evidence. No acceptance threshold changed.','byAssetType':grouped},indent=2),encoding='utf8')
planned_gaps={}
for r in records:
 if r['optional'] or 'plannedSourcePath' not in r:continue
 if file(r['plannedSourcePath'])['exists'] and file(r['plannedRuntimePath'])['exists']:continue
 planned_gaps.setdefault(r['assetType'],[]).append({'assetId':r['assetId'],'plannedSourcePath':r['plannedSourcePath'],'plannedRuntimePath':r['plannedRuntimePath'],'actualSourcePath':r['source']['path'],'actualRuntimePath':r['runtime']['path'],'explicitMapping':r['pathMappingApplied'],'mappedFilesPresent':r['status']=='generated_pending_import_review','verificationEvidence':None})
(ROOT/'ArtSource/Art07/integration_work_items.json').write_text(json.dumps({'artVersion':'art0.7.0','originalPlanPathGapCount':sum(map(len,planned_gaps.values())),'remainingAfterExplicitMappings':len(gaps),'note':'Original exact-path differences include resolved engineering mappings and outstanding assets. A mapping is not verification.','byAssetType':planned_gaps},indent=2),encoding='utf8')
