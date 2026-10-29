import bpy,json
from pathlib import Path
out=Path(r'E:/agent_workspace/gpt/game2/evidence/art')
info={'version':list(bpy.app.version),'version_string':bpy.app.version_string,'cycle':bpy.app.version_cycle,'build_hash':bpy.app.build_hash.decode(),'export_fbx':{}}
try:
 rna=bpy.ops.export_scene.fbx.get_rna_type()
 for p in rna.properties:
  if p.identifier!='rna_type':
   info['export_fbx'][p.identifier]={'type':p.type,'default':str(getattr(p,'default',''))}
except Exception as e: info['error']=str(e)
(out/'blender_probe.json').write_text(json.dumps(info,indent=2),encoding='utf8')
print('BLENDER_PROBE',info['version_string'],len(info['export_fbx']),info.get('error','OK'))
