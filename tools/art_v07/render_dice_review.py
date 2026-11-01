"""Actual Blender source previews; explicitly not Unity screenshots."""
from pathlib import Path
import bpy,math,json
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2];SRC=ROOT/'ArtSource/Art07/Dice';OUT=SRC/'Review'
bpy.ops.wm.open_mainfile(filepath=str(SRC/'dice_master.blend'))
scene=bpy.context.scene;scene.render.engine='CYCLES';scene.cycles.device='CPU';scene.cycles.samples=24
scene.render.resolution_x=384;scene.render.resolution_y=384;scene.render.resolution_percentage=100;scene.render.image_settings.file_format='PNG';scene.render.image_settings.color_mode='RGBA';scene.render.film_transparent=True
scene.view_settings.view_transform='Standard';scene.view_settings.look='None';scene.world.color=(.12,.17,.17)
body=bpy.data.objects['DiceBody']
def lin(v):return v/12.92 if v<=.04045 else ((v+.055)/1.055)**2.4
def mat(name,col,rough=.45,metal=0):
 m=bpy.data.materials.new(name);m.use_nodes=True;rgb=tuple(lin(int(col[i:i+2],16)/255) for i in [0,2,4]);m.diffuse_color=(*rgb,1);n=m.node_tree.nodes['Principled BSDF'];n.inputs['Base Color'].default_value=(*rgb,1);n.inputs['Roughness'].default_value=rough;n.inputs['Metallic'].default_value=metal;return m
ceramic=mat('Review_Ceramic','F4E8CB');ink=mat('Review_Ink','162629');red=mat('Review_Red','EC655D');blue=mat('Review_Blue','55B6D9');gold=mat('Review_Gold','F3BA58',.3,.55);glass=mat('Review_Stylized_Glass','55B6D9',.24,.15)
body.data.materials.clear();body.data.materials.append(ceramic)
normals=[(0,0,1),(0,-1,0),(1,0,0),(-1,0,0),(0,1,0),(0,0,-1)]
positions={1:[(0,0)],2:[(-1,-1),(1,1)],3:[(-1,-1),(0,0),(1,1)],4:[(-1,-1),(1,-1),(-1,1),(1,1)],5:[(-1,-1),(1,-1),(0,0),(-1,1),(1,1)],6:[(-1,-1),(1,-1),(-1,0),(1,0),(-1,1),(1,1)]}
for i,n in enumerate(normals,1):
 n=Vector(n);t=Vector((1,0,0)) if abs(n.x)<.5 else Vector((0,1,0));b=n.cross(t)
 for j,(x,y) in enumerate(positions[i]):
  for radius,depth,offset,m in [(.068,.001,.502,ink),(.058,.001,.503,red if i in (1,4) else blue)]:
   bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=radius,depth=depth,location=n*offset+t*(x*.22)+b*(y*.22));o=bpy.context.object;o.name=f'Review_F{i:02}_Pip{j+1}';o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference(n);o.data.materials.append(m)
bpy.ops.object.camera_add();cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=1.45;scene.camera=cam
bpy.ops.object.light_add(type='AREA');key=bpy.context.object;key.data.energy=320;key.data.size=3
bpy.ops.object.light_add(type='AREA');fill=bpy.context.object;fill.data.energy=70;fill.data.size=3
def look(o,p=(0,0,0)):o.rotation_euler=(Vector(p)-o.location).to_track_quat('-Z','Y').to_euler()
for i,n in enumerate(normals,1):
 n=Vector(n);t=Vector((1,0,0)) if abs(n.x)<.5 else Vector((0,1,0));b=n.cross(t);cam.location=n*4;look(cam);key.location=n*2-t*1.6+b*1.8;look(key);fill.location=n*2+t*2;look(fill);scene.render.filepath=str(OUT/f'face_f{i:02}.png');bpy.ops.render.render(write_still=True)
cam.location=(2.2,-3,5.2);cam.data.ortho_scale=1.75;look(cam);key.location=(-3,-4,6);look(key);fill.location=(4,-1,2);look(fill)
for label,material in [('ceramic',ceramic),('glass',glass)]:
 body.data.materials[0]=material;scene.render.filepath=str(OUT/f'hero_{label}.png');bpy.ops.render.render(write_still=True)
body.data.materials[0]=ceramic
# Narrow selected-face border; protect all pips, do not turn the body gold.
for x,y,sx,sy in [(0,.435,.87,.018),(0,-.435,.87,.018),(.435,0,.018,.87),(-.435,0,.018,.87)]:
 bpy.ops.mesh.primitive_cube_add(size=1,location=(x,y,.504));o=bpy.context.object;o.name='Review_GoldEdge';o.scale=(sx,sy,.004);o.data.materials.append(gold)
scene.render.filepath=str(OUT/'hero_gold.png');bpy.ops.render.render(write_still=True)
print('ART07_SOURCE_REVIEW_RENDERED')
