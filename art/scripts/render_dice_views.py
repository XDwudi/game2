"""Offline source QA only; these images are not Unity/game verification."""
from pathlib import Path
import bpy,json,math
from mathutils import Vector
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'art/previews'
bpy.ops.wm.open_mainfile(filepath=str(ROOT/'art/blender/Dice_P0.blend'))
scene=bpy.context.scene
scene.frame_set(1)
scene.render.engine='CYCLES'
scene.cycles.device='CPU'
scene.cycles.samples=20
scene.render.resolution_x=384
scene.render.resolution_y=384
scene.render.resolution_percentage=100
scene.render.image_settings.file_format='PNG'
scene.render.image_settings.color_mode='RGBA'
scene.render.film_transparent=False
scene.world.color=(.05,.08,.1)
scene.view_settings.view_transform='AgX'
normals=[(0,0,1),(0,-1,0),(1,0,0),(-1,0,0),(0,1,0),(0,0,-1)]
positions={1:[(0,0)],2:[(-1,-1),(1,1)],3:[(-1,-1),(0,0),(1,1)],4:[(-1,-1),(1,-1),(-1,1),(1,1)],5:[(-1,-1),(1,-1),(0,0),(-1,1),(1,1)],6:[(-1,-1),(1,-1),(-1,0),(1,0),(-1,1),(1,1)]}
def mat(name,col):
 m=bpy.data.materials.new(name);m.diffuse_color=(*col,1);m.use_nodes=True
 m.node_tree.nodes['Principled BSDF'].inputs['Base Color'].default_value=(*col,1)
 m.node_tree.nodes['Principled BSDF'].inputs['Roughness'].default_value=.3
 return m
red=mat('QA_Pip_Red',(.65,.025,.05));blue=mat('QA_Pip_Blue',(.02,.19,.57))
for i,n in enumerate(normals,1):
 n=Vector(n);t=Vector((1,0,0)) if abs(n.x)<.5 else Vector((0,1,0));b=n.cross(t)
 for j,(x,y) in enumerate(positions[i]):
  bpy.ops.mesh.primitive_cylinder_add(vertices=24,radius=.06,depth=.005,location=n*.501+t*(x*.225)+b*(y*.225))
  o=bpy.context.object;o.name=f'QA_F{i:02}_Pip{j+1}';o.rotation_mode='QUATERNION';o.rotation_quaternion=Vector((0,0,1)).rotation_difference(n)
  o.data.materials.append(red if i in (1,4) else blue)
bpy.ops.object.camera_add()
cam=bpy.context.object;cam.data.type='ORTHO';cam.data.ortho_scale=1.46;scene.camera=cam
bpy.ops.object.light_add(type='AREA')
light=bpy.context.object;light.data.energy=210;light.data.shape='DISK';light.data.size=3
bpy.ops.object.light_add(type='AREA')
fill=bpy.context.object;fill.data.energy=75;fill.data.size=2
def look(obj,p):obj.rotation_euler=(Vector(p)-obj.location).to_track_quat('-Z','Y').to_euler()
for i,n in enumerate(normals,1):
 n=Vector(n);t=Vector((1,0,0)) if abs(n.x)<.5 else Vector((0,1,0));b=n.cross(t)
 cam.location=n*4;look(cam,(0,0,0))
 light.location=n*2+t*1.6+b*1.4;look(light,(0,0,0));fill.location=n*2-t*1.3;look(fill,(0,0,0))
 scene.render.filepath=str(OUT/f'Face_F{i:02}.png')
 bpy.ops.render.render(write_still=True)
cam.location=(2.2,-3,3);look(cam,(0,0,0));cam.data.ortho_scale=1.8
light.location=(1,-3,4);look(light,(0,0,0));fill.location=(-3,-1,2);look(fill,(0,0,0))
scene.render.filepath=str(OUT/'DiceSourceHero.png');bpy.ops.render.render(write_still=True)
print('P0_SIX_VIEWS_RENDERED')
