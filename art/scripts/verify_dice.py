"""Reopen source, verify dimensions/markers/animation endpoints, save evidence."""
from pathlib import Path
import bpy,json,math
ROOT=Path(__file__).resolve().parents[2]
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/blender/Dice_P0.blend'))
body=bpy.data.objects['DiceBody']
body.data.calc_loop_triangles()
assert len(body.data.loop_triangles)<=2000
assert max(abs(d-1) for d in body.dimensions)<1e-5
pivot=bpy.data.objects['RollPivot']
checks=[]
for name in ['Roll_A','Roll_B']:
    action=bpy.data.actions[name]
    pivot.animation_data.action=action
    for frame in [1,43]:
        bpy.context.scene.frame_set(frame)
        loc=list(pivot.location); rot=list(pivot.rotation_euler); scale=list(pivot.scale)
        assert max(map(abs,loc+rot))<1e-6,(name,frame,loc,rot)
        assert max(abs(s-1) for s in scale)<1e-6
        checks.append({'action':name,'frame':frame,'location':loc,'rotation':rot,'scale':scale})
for i in range(1,7):
    assert 'Face_F%02d'%i in bpy.data.objects
report={'sourceReopened':True,'bodyTriangles':len(body.data.loop_triangles),'faceMarkers':6,'animationEndpointChecks':checks,'blenderVersion':bpy.app.version_string,'unityImportValidated':False}
(ROOT/'evidence/art/dice_reopen.json').write_text(json.dumps(report,indent=2),encoding='utf8')
print('P0_DICE_REOPEN_PASS',json.dumps(report))
