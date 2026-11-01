"""Blender 5.2 source/export for art0.7.0; no collision-driven game result."""
from pathlib import Path
import bpy, json, math
from mathutils import Vector

ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'ArtSource/Art07/Dice'
OUT=ROOT/'DicePrototype/Assets/Art/Art07/Dice'
for p in (SRC,OUT/'Models',OUT/'Animation',OUT/'Data',SRC/'Review'):p.mkdir(parents=True,exist_ok=True)
assert bpy.app.version[:2]==(5,2), bpy.app.version_string
bpy.ops.object.select_all(action='SELECT');bpy.ops.object.delete(use_global=False)
scene=bpy.context.scene;scene.unit_settings.system='METRIC';scene.unit_settings.scale_length=1
scene.render.fps=30;scene.frame_start=1;scene.frame_end=28

def empty(name,parent=None):
 o=bpy.data.objects.new(name,None);scene.collection.objects.link(o);o.parent=parent;o.empty_display_size=.08;return o
def mat(name,hexcol,roughness=.45,metallic=0):
 col=tuple(int(hexcol[i:i+2],16)/255 for i in (0,2,4));m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);m.use_nodes=True;n=m.node_tree.nodes.get('Principled BSDF');n.inputs['Base Color'].default_value=(*col,1);n.inputs['Roughness'].default_value=roughness;n.inputs['Metallic'].default_value=metallic;return m

root=empty('DiceRoot');pivot=empty('RollPivot',root);orient=empty('FaceOrientation',pivot)
bpy.ops.mesh.primitive_cube_add(size=1);body=bpy.context.object;body.name='DiceBody';body.parent=orient
b=body.modifiers.new('Ceramic bevel 0.055 / 3','BEVEL');b.width=.055;b.segments=3;bpy.ops.object.modifier_apply(modifier=b.name)
for f in body.data.polygons:f.use_smooth=f.area<.5
n=body.modifiers.new('Weighted ceramic normals','WEIGHTED_NORMAL');n.keep_sharp=True;n.weight=50;bpy.ops.object.modifier_apply(modifier=n.name)
t=body.modifiers.new('Fixed export triangulation','TRIANGULATE');bpy.ops.object.modifier_apply(modifier=t.name)
ceramic=mat('Art07_Ceramic','F4E8CB');glass=mat('Art07_Glass_Stylized','55B6D9',.28,.05);glass.use_fake_user=True;body.data.materials.append(ceramic)
face_map=[];markers=[];vertices=[];faces=[]
for i,(un,bn) in enumerate(zip([(0,1,0),(0,0,1),(1,0,0),(-1,0,0),(0,0,-1),(0,-1,0)],[(0,0,1),(0,-1,0),(1,0,0),(-1,0,0),(0,1,0),(0,0,-1)]),1):
 normal=Vector(bn);tan=Vector((1,0,0)) if not bn[0] else Vector((0,1,0));bit=normal.cross(tan);o=empty(f'Face_F{i:02}',orient);o.location=normal*.502;o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference(normal);o['faceId']=f'F{i:02}';markers.append(o)
 face_map.append({'faceId':f'F{i:02}','unityNormal':un,'unityCenter':[v*.502 for v in un],'blenderNormal':bn,'oppositeFaceId':f'F{7-i:02}','markerObject':o.name,'uvTangentBlender':list(tan),'uvBitangentBlender':list(bit),'initialPip':i,'initialColor':'red' if i in (1,4) else 'blue'})
 base=len(vertices);vertices.extend([normal*.502+tan*x+bit*y for x,y in [(-.43,-.43),(.43,-.43),(.43,.43),(-.43,.43)]]);faces.append(tuple(range(base,base+4)))
mesh=bpy.data.meshes.new('DiceFaces_IndependentUV');mesh.from_pydata(vertices,[],faces);mesh.update();face_obj=bpy.data.objects.new('DiceFaces',mesh);scene.collection.objects.link(face_obj);face_obj.parent=orient
uv=mesh.uv_layers.new(name='FaceUV')
for poly in mesh.polygons:
 for li,coord in zip(poly.loop_indices,[(0,0),(1,0),(1,1),(0,1)]):uv.data[li].uv=coord
face_obj.hide_render=True

fragments=[];fragment_root=empty('DiceFragments')
for i in range(6):
 a=2*math.pi*i/6;bpy.ops.mesh.primitive_ico_sphere_add(subdivisions=1,radius=.21,location=(.18*math.cos(a),.18*math.sin(a),.1));f=bpy.context.object;f.name=f'Fragment_{i+1:02}';f.parent=fragment_root;f.scale=(1,.65,.8);f.data.materials.append(ceramic);bpy.ops.object.transform_apply(location=False,rotation=False,scale=True);fragments.append(f)
fragment_root.hide_render=True
for f in fragments:f.hide_render=True;f.hide_viewport=True

actions=[]
def make_motion(name,seconds,sample):
 pivot.animation_data_clear();count=round(seconds*30)
 for f in range(count+1):
  pos,rot,scale=sample(f/count);pivot.location=pos;pivot.rotation_euler=rot;pivot.scale=scale
  for channel in ['location','rotation_euler','scale']:pivot.keyframe_insert(data_path=channel,frame=f+1)
 a=pivot.animation_data.action;a.name=name;a.use_fake_user=True;actions.append((a,seconds,count+1));return a
for variant in (0,1):
 def roll(u,v=variant):
  # Last 120ms is already result-aligned: rotation exactly zero.
  r=min(1,u/.8666667);env=math.sin(math.pi*r)**2 if r<1 else 0;lift=.34*math.sin(math.pi*u)**2
  return ((.07*math.sin(2*math.pi*u)*env,.025*math.sin(4*math.pi*u)*env,lift),(4*math.pi*env,(-3 if v else 2)*math.pi*env,(1 if v else -1)*math.pi*math.sin(2*math.pi*r)*env),(1,1,1))
 make_motion('dice_roll_'+('b' if variant else 'a'),.9,roll)
make_motion('dice_lock_lift',.2,lambda u:((0,0,.07*math.sin(math.pi*u)),(0,0,0),(1+.04*math.sin(math.pi*u),)*3))
make_motion('dice_submit_tap',.2,lambda u:((0,0,0),(0,0,0),(1-.03*math.sin(math.pi*u),1-.03*math.sin(math.pi*u),1+.02*math.sin(math.pi*u))))
preset={'use_selection':True,'global_scale':1.0,'apply_unit_scale':True,'apply_scale_options':'FBX_SCALE_UNITS','axis_forward':'-Z','axis_up':'Y','use_space_transform':True,'bake_space_transform':False,'object_types':{'MESH','EMPTY'},'use_mesh_modifiers':True,'mesh_smooth_type':'FACE','add_leaf_bones':False,'bake_anim_use_all_actions':False,'bake_anim_use_nla_strips':False,'bake_anim_step':1.0,'bake_anim_simplify_factor':0.0,'bake_anim_force_startend_keying':True,'path_mode':'AUTO','embed_textures':False}
assert not(set(preset)-set(bpy.ops.export_scene.fbx.get_rna_type().properties.keys()))
def export(path,objects,anim=False):
 bpy.ops.object.select_all(action='DESELECT')
 for o in objects:o.hide_set(False);o.select_set(True)
 bpy.context.view_layer.objects.active=objects[-1]
 bpy.ops.export_scene.fbx(filepath=str(path),bake_anim=anim,**preset)
pivot.animation_data_clear();pivot.location=(0,0,0);pivot.rotation_euler=(0,0,0);pivot.scale=(1,1,1)
base=[root,pivot,orient,body]+markers
export(OUT/'Models/dice_body_bevel.fbx',base)
export(OUT/'Models/dice_faces.fbx',[root,pivot,orient,face_obj]+markers)
for f in fragments:f.hide_viewport=False
export(OUT/'Models/dice_fragments.fbx',[fragment_root]+fragments)
for a,seconds,end in actions:
 scene.frame_end=end;pivot.animation_data_create();pivot.animation_data.action=a;scene.frame_set(1);export(OUT/'Animation'/f'{a.name}.fbx',base,True)
break_actions=[]
for i,f in enumerate(fragments):
 f.hide_viewport=False;f.hide_render=False;start=f.location.copy();ang=2*math.pi*i/6
 for step in range(16):
  u=step/15;f.location=start+Vector((math.cos(ang)*.65*u,math.sin(ang)*.65*u,.25*math.sin(math.pi*u)-.4*u*u));f.rotation_euler=(u*(i+1),u*2,ang*u);f.scale=(max(.05,1-u),)*3
  for channel in ['location','rotation_euler','scale']:f.keyframe_insert(data_path=channel,frame=1+13.5*u)
 f.animation_data.action.name=f'dice_break_fragment_{i+1:02}';f.animation_data.action.use_fake_user=True;break_actions.append(f.animation_data.action)
scene.frame_end=15;scene.frame_set(1);export(OUT/'Animation/dice_break.fbx',[fragment_root]+fragments,True)
for f in fragments:f.animation_data_clear();f.hide_render=True
pivot.animation_data_clear();scene.frame_set(1);pivot.location=(0,0,0);pivot.rotation_euler=(0,0,0);pivot.scale=(1,1,1);scene.frame_end=28
for f in fragments:f.hide_viewport=True
face_obj.hide_viewport=True
data={'schemaVersion':1,'artVersion':'art0.7.0','units':'metres','referenceEdgeLength':1,'surfaceOffset':.002,'runtimeConvention':'Unity local +Y up; dynamic pips never identify faceId','faces':face_map}
(OUT/'Data/dice_face_map.json').write_text(json.dumps(data,indent=2),encoding='utf8');(SRC/'face_map.json').write_text(json.dumps(data,indent=2),encoding='utf8')
body.data.calc_loop_triangles();tri=len(body.data.loop_triangles);assert tri<=1200
summary={'blenderVersion':bpy.app.version_string,'buildHash':bpy.app.build_hash.decode(),'bodyTriangles':tri,'bodyVertices':len(body.data.vertices),'bodyDimensions':list(body.dimensions),'fragmentTriangles':sum(len(f.data.polygons) for f in fragments),'bevelWidth':.055,'bevelSegments':3,'actions':[{'name':a.name,'seconds':seconds,'frames':end,'fps':30} for a,seconds,end in actions],'unityImportValidated':False,'sourceOnlyQA':'Offline source/model checks; final Unity face normals remain unverified','exportPreset':{k:sorted(v) if isinstance(v,set) else v for k,v in preset.items()}}
(SRC/'Review/generation.json').write_text(json.dumps(summary,indent=2),encoding='utf8');bpy.ops.wm.save_as_mainfile(filepath=str(SRC/'dice_master.blend'));print('ART07_DICE_GENERATED',json.dumps(summary))
