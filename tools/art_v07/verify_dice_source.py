"""Reopen .blend and evaluate actual transforms, geometry and face anchors."""
from pathlib import Path
import bpy,json,math,datetime,hashlib
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];SRC=ROOT/'ArtSource/Art07/Dice';bpy.ops.wm.open_mainfile(filepath=str(SRC/'dice_master.blend'))
scene=bpy.context.scene;pivot=bpy.data.objects['RollPivot'];body=bpy.data.objects['DiceBody'];checks=[]
def check(name,ok,detail):checks.append({'check':name,'passed':bool(ok),'detail':detail})
body.data.calc_loop_triangles();check('body_budget',len(body.data.loop_triangles)<=1200,len(body.data.loop_triangles));check('body_size',all(abs(v-1)<1e-6 for v in body.dimensions),list(body.dimensions));check('body_center',body.location.length<1e-6,list(body.location))
normals=[(0,0,1),(0,-1,0),(1,0,0),(-1,0,0),(0,1,0),(0,0,-1)]
for i,n in enumerate(normals,1):
 o=bpy.data.objects[f'Face_F{i:02}'];actual=o.rotation_quaternion@Vector((0,0,1));check(f'face{i}_normal',(actual-Vector(n)).length<1e-5,list(actual));check(f'face{i}_offset',(o.location-Vector(n)*.502).length<1e-6,list(o.location))
for name in ['dice_roll_a','dice_roll_b','dice_lock_lift','dice_submit_tap']:
 a=bpy.data.actions.get(name);check(name+'_exists',a is not None,name)
 if not a:continue
 pivot.animation_data_create();pivot.animation_data.action=a;frames=28 if 'roll' in name else 7;rot=[];movement=[]
 for f in range(1,frames+1):scene.frame_set(f);rot.append(Vector(pivot.rotation_euler).length);movement.append(pivot.location.length+sum(abs(v-1) for v in pivot.scale))
 check(name+'_changes',max(rot+movement)>.01,{'rotationMax':max(rot),'movementMax':max(movement)})
 for f in [1,frames]:
  scene.frame_set(f);check(name+f'_identity_{f}',pivot.location.length<1e-5 and Vector(pivot.rotation_euler).length<1e-5 and all(abs(v-1)<1e-5 for v in pivot.scale),{'position':list(pivot.location),'rotation':list(pivot.rotation_euler),'scale':list(pivot.scale)})
 if 'roll' in name:
  for f in [25,26,27,28]:scene.frame_set(f);check(name+f'_result_aligned_{f}',Vector(pivot.rotation_euler).length<1e-5,list(pivot.rotation_euler))
fragments=[bpy.data.objects[f'Fragment_{i:02}'] for i in range(1,7)];check('six_fragments',len(fragments)==6,6);check('fragment_actions',all(bpy.data.actions.get(f'dice_break_fragment_{i:02}') for i in range(1,7)),6)
result={'sourceReopened':True,'sourceSha256':hashlib.sha256((SRC/'dice_master.blend').read_bytes()).hexdigest(),'checkerSha256':hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),'blender':bpy.app.version_string,'unityVerified':False,'passed':all(c['passed'] for c in checks),'checks':checks};stamp=datetime.datetime.now().strftime('%Y%m%d-%H%M%S');(SRC/f'Review/source-check-{stamp}.json').write_text(json.dumps(result,indent=2),encoding='utf8');print(json.dumps({'passed':result['passed'],'count':len(checks)}));assert result['passed']
