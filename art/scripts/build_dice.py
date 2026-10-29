"""Reproducible P0 dice source. Run with Blender 5.2.3 --background --python.

Art geometry carries no game rules; Unity supplies the current physical-face pips.
All output stays under this repository. No external assets are used.
"""
from pathlib import Path
import bpy
import json
import math
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
ART = ROOT / 'art'
MODEL = ROOT / 'DicePrototype/Assets/Game/Art/Models'
EVIDENCE = ROOT / 'evidence/art'
for folder in (ART/'blender', ART/'previews', MODEL, EVIDENCE):
    folder.mkdir(parents=True, exist_ok=True)
if bpy.app.version[:2] != (5, 2):
    raise RuntimeError('P0 source requires Blender 5.2.x; no silent version substitution')

export_settings = {
    'use_selection': True, 'global_scale': 1.0,
    'apply_unit_scale': True, 'apply_scale_options': 'FBX_SCALE_UNITS',
    'axis_forward': '-Z', 'axis_up': 'Y',
    'use_space_transform': True, 'bake_space_transform': False,
    'object_types': {'MESH', 'EMPTY'}, 'use_mesh_modifiers': True,
    'mesh_smooth_type': 'FACE', 'add_leaf_bones': False,
    'bake_anim_use_all_actions': False, 'bake_anim_use_nla_strips': False,
    'bake_anim_step': 1.0, 'bake_anim_simplify_factor': 0.0,
    'bake_anim_force_startend_keying': True,
    'path_mode': 'AUTO', 'embed_textures': False,
}
available = set(bpy.ops.export_scene.fbx.get_rna_type().properties.keys())
missing = set(export_settings) - available
if missing:
    raise RuntimeError('FBX exporter missing required options: '+str(missing))
(EVIDENCE/'fbx_export_preset.json').write_text(json.dumps({k:sorted(v) if isinstance(v,set) else v for k,v in export_settings.items()},indent=2),encoding='utf8')

bpy.ops.object.select_all(action='SELECT')
bpy.ops.object.delete(use_global=False)
for a in list(bpy.data.actions):
    bpy.data.actions.remove(a)
scene=bpy.context.scene
scene.unit_settings.system='METRIC'
scene.unit_settings.scale_length=1.0
scene.render.fps=60
scene.frame_start=1
scene.frame_end=43

def empty(name,parent=None):
    obj=bpy.data.objects.new(name,None)
    scene.collection.objects.link(obj)
    obj.parent=parent
    obj.empty_display_type='PLAIN_AXES'
    obj.empty_display_size=.1
    return obj

root=empty('DiceRoot')
pivot=empty('RollPivot',root)
orientation=empty('FaceOrientation',pivot)
bpy.ops.mesh.primitive_cube_add(size=1.0)
body=bpy.context.object
body.name='DiceBody'
body.data.name='DiceBody_Beveled_1m'
body.parent=orientation
bpy.ops.object.transform_apply(location=False,rotation=True,scale=True)
bevel=body.modifiers.new('Edge bevel 0.07m / 4 segments','BEVEL')
bevel.width=.07
bevel.segments=4
bevel.affect='EDGES'
bpy.context.view_layer.objects.active=body
bpy.ops.object.modifier_apply(modifier=bevel.name)
for face in body.data.polygons:
    face.use_smooth=face.area<.5
normal=body.modifiers.new('Weighted normals','WEIGHTED_NORMAL')
normal.keep_sharp=True
normal.weight=50
bpy.ops.object.modifier_apply(modifier=normal.name)

def material(name,color,metallic,roughness):
    m=bpy.data.materials.new(name)
    m.diffuse_color=(*color,1)
    m.use_nodes=True
    bsdf=m.node_tree.nodes.get('Principled BSDF')
    bsdf.inputs['Base Color'].default_value=(*color,1)
    bsdf.inputs['Metallic'].default_value=metallic
    bsdf.inputs['Roughness'].default_value=roughness
    return m
ivory=material('Prototype_Ivory',(0.86,.87,.82),.05,.26)
glass=material('Prototype_Glass_Opaque',(0.44,.75,.79),.22,.16)
glass.use_fake_user=True
body.data.materials.append(ivory)

# Blender +Z becomes Unity +Y; Blender -Y becomes Unity +Z.
# The authoritative runtime map is explicitly supplied; importer checks are separate.
unity_normals=[(0,1,0),(0,0,1),(1,0,0),(-1,0,0),(0,0,-1),(0,-1,0)]
blender_normals=[(0,0,1),(0,-1,0),(1,0,0),(-1,0,0),(0,1,0),(0,0,-1)]
face_map=[]
marker_vertices=[]
marker_faces=[]
for i,(un,bn) in enumerate(zip(unity_normals,blender_normals),1):
    fid=f'F{i:02}'
    marker=empty('Face_'+fid,orientation)
    marker.location=Vector(bn)*.5
    marker.rotation_mode='QUATERNION'
    marker.rotation_quaternion=Vector((0,0,1)).rotation_difference(Vector(bn))
    marker['face_id']=fid
    marker['unity_normal']=list(un)
    marker['opposite_face_id']=f'F{7-i:02}'
    face_map.append({'faceId':fid,'unityNormal':list(un),'unityCenter':[v*.501 for v in un], 'blenderNormal':list(bn),'blenderCenter':[v*.5 for v in bn],'oppositeFaceId':f'F{7-i:02}', 'initialPip':i,'initialColor':'red' if i in (1,4) else 'blue','markerObject':marker.name})
    # A separate six-face marker mesh makes the contract inspectable in source.
    n=Vector(bn)
    tangent=Vector((1,0,0)) if abs(n.x)<.5 else Vector((0,1,0))
    bitangent=n.cross(tangent)
    center=n*.501
    begin=len(marker_vertices)
    marker_vertices.extend([center+tangent*x+bitangent*y for x,y in [(-.35,-.35),(.35,-.35),(.35,.35),(-.35,.35)]])
    marker_faces.append(tuple(range(begin,begin+4)))
mesh=bpy.data.meshes.new('SixPhysicalFaceMarkers')
mesh.from_pydata(marker_vertices,[],marker_faces)
mesh.update()
face_mesh=bpy.data.objects.new('FaceMarkers_ReferenceOnly',mesh)
scene.collection.objects.link(face_mesh)
face_mesh.parent=orientation
face_mesh.hide_render=True
face_mesh.hide_viewport=True
face_mesh['purpose']='Source-only reference; exported FBX uses six named Empty markers instead.'

def make_action(name,variant):
    pivot.animation_data_clear()
    for frame in range(1,44):
        u=(frame-1)/42
        envelope=math.sin(math.pi*u)**2
        pivot.location=(.08*math.sin(2*math.pi*u)*envelope, .03*math.sin(4*math.pi*u)*envelope, .38*math.sin(math.pi*u))
        pivot.rotation_euler=(4*math.pi*envelope, (2 if variant==0 else -3)*math.pi*envelope, (1 if variant==0 else -1)*math.pi*math.sin(2*math.pi*u)*envelope)
        if frame in (1,43):
            pivot.location=(0,0,0)
            pivot.rotation_euler=(0,0,0)
        pivot.keyframe_insert(data_path='location',frame=frame)
        pivot.keyframe_insert(data_path='rotation_euler',frame=frame)
    action=pivot.animation_data.action
    action.name=name
    action.use_fake_user=True
    return action

actions=[make_action('Roll_A',0),make_action('Roll_B',1)]
scene.frame_set(1)
export_objects=[root,pivot,orientation,body]+[bpy.data.objects['Face_'+f['faceId']] for f in face_map]
def select_export():
    bpy.ops.object.select_all(action='DESELECT')
    for obj in export_objects: obj.select_set(True)
    bpy.context.view_layer.objects.active=body

select_export()
pivot.animation_data_clear()
scene.frame_set(1)
pivot.location=(0,0,0)
pivot.rotation_euler=(0,0,0)
bpy.ops.export_scene.fbx(filepath=str(MODEL/'DiceBody.fbx'),bake_anim=False,**export_settings)
for action in actions:
    pivot.animation_data_create()
    pivot.animation_data.action=action
    scene.frame_set(1)
    bpy.ops.export_scene.fbx(filepath=str(MODEL/(action.name+'.fbx')),bake_anim=True,**export_settings)
pivot.animation_data.action=actions[0]
scene.frame_set(1)

map_data={'schemaVersion':1,'units':'metres','referenceEdgeLength':1,'runtimeConvention':'Unity local space; +Y is up; pips are dynamic, never used to infer faceId','fbxAxisForward':'-Z','fbxAxisUp':'Y','faces':face_map}
(MODEL/'face_map.json').write_text(json.dumps(map_data,indent=2),encoding='utf8')
body.data.calc_loop_triangles()
triangles=len(body.data.loop_triangles)
assert triangles<=2000,triangles
assert all(abs(v-1)<1e-5 for v in body.dimensions),list(body.dimensions)
summary={'sourceVersion':bpy.app.version_string,'buildHash':bpy.app.build_hash.decode(),'bodyTriangles':triangles,'bodyVertices':len(body.data.vertices),'bodyDimensions':list(body.dimensions),'faceMarkers':[f['markerObject'] for f in face_map],'actions':[{'name':a.name,'seconds':.7,'frameStart':1,'frameEnd':43,'fps':60} for a in actions],'sourceOnlyFaceMarkerTriangles':12,'unityImportValidated':False}
(EVIDENCE/'dice_generation.json').write_text(json.dumps(summary,indent=2),encoding='utf8')
bpy.ops.wm.save_as_mainfile(filepath=str(ART/'blender/Dice_P0.blend'))
print('P0_DICE_GENERATED',json.dumps(summary))
