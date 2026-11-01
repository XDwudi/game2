"""Explicit engineering paths; never silently rename authored asset IDs."""
from pathlib import Path
import json
ROOT=Path(__file__).resolve().parents[2];source=ROOT/'ArtSource/Art07';plan=json.loads((ROOT/'docs/specs/v07/dice_art_v0_7_manifest.json').read_text(encoding='utf8'));planned={a['assetId']:a for a in plan['sharedResources']['assets']};recipe=json.loads((source/'Audio/recipe.json').read_text(encoding='utf8'));records=[];extras=[]
for a in recipe['assets']:
 runtime=a.get('runtimePath','DicePrototype/Assets/Art/Art07/Audio/'+a['assetId']+'.wav').removeprefix('DicePrototype/');src=a.get('sourcePath','ArtSource/Art07/Audio/'+a['assetId']+'.wav');assert (ROOT/'DicePrototype'/runtime).is_file() and (ROOT/src).is_file(),a['assetId'];p=planned.get(a['assetId']);r={'assetId':a['assetId'],'sourcePath':src,'runtimePath':runtime,'sourceEvidence':'ArtSource/Art07/Audio/recipe.json','reason':'Original individual WAV masters and deterministic recipe; stable IDs resolve at Audio top level in the actual AudioSetup/runtime router. No moved files or broken GUID references.','approval':'root engineering integration instruction 2026-10-31'}
 if p:r.update({'plannedSourcePath':p['sourcePath'],'plannedRuntimePath':p['runtimePath']});records.append(r)
 else:extras.append(r)
for id,path,reason in [
 ('shader_dice_three_tone','Assets/Game/Runtime/Shaders/Art07Ceramic.shader','Equivalent project ShaderLab/HLSL implementation of three-tone ceramic/opaque readable glass. Editable source is code rather than ShaderGraph.'),
 ('shader_dice_pips','Assets/Game/Runtime/Shaders/VertexFace.shader','Existing unlit vertex-color physical face implementation preserves red/blue pips from lighting; reused in the actual DiceTable.'),
 ('shader_ui_local_fx','Assets/Game/Runtime/Shaders/Art07LocalFx.shader','Editable UI-compatible alpha ShaderLab, with stencil, RectMask clipping and vertex tint. No screen-wide effects or independent random sampling.'),
 ('font_cjk_source','Assets/Game/Fonts/NotoSansCJKsc-Regular.otf','Reuse existing original Noto source rather than duplicate font binaries.'),
 ('font_cjk_license','Assets/Game/Fonts/LICENSE.txt','Reuse the original Noto OFL license colocated with the source.'),
 ('font_cjk_tmp','Assets/Game/Fonts/DiceChinese.asset','Reuse existing CJK TMP asset and its fallback chain; numeric TMP remains independently generated.')]:
 p=planned[id];records.append({'assetId':id,'sourcePath':path,'runtimePath':path,'plannedSourcePath':p['sourcePath'],'plannedRuntimePath':p['runtimePath'],'reason':reason,'approval':'root engineering integration instruction 2026-10-31','sourceEvidence':'DicePrototype/Assets/Game/Editor/V07Setup.cs; DicePrototype/Assets/Game/Runtime/DiceTable.cs'})
for id in ('atlas_ui_pixel','atlas_content_icons_pixel','atlas_fx_soft'):
 p=planned[id];path=p['runtimePath']+'v2';records.append({'assetId':id,'sourcePath':path,'runtimePath':path,'plannedSourcePath':p['sourcePath'],'plannedRuntimePath':p['runtimePath'],'sourceEvidence':'DicePrototype/Assets/Game/Editor/Art07AssetLibrary.cs:BuildAtlases','reason':'Native SpriteAtlas V2 authoring format required by Unity 6.6. Built-in delivery; pixel and soft FX have separate filters; original manifest remains unchanged.','approval':'root asset-library implementation assignment 2026-10-31'})
# Retain explicit integrations recorded by other owners instead of erasing them on refresh.
previous=source/'path_mappings.json'
if previous.is_file():
 known={r['assetId'] for r in records}
 records.extend(r for r in json.loads(previous.read_text(encoding='utf8')).get('records',[]) if r['assetId'] not in known)
result={'artVersion':'art0.7.0','scope':'Explicit production implementation mapping; original planned manifest stays immutable','mappingDoesNotGrantVerification':True,'records':records,'additionalAssets':extras}
(source/'path_mappings.json').write_text(json.dumps(result,indent=2),encoding='utf8');print(json.dumps({'mapped':len(records),'extra':len(extras)}))
