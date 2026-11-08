"""Reproducible art0.7.1 ceramic master. Run with Blender 5.2 --background.

Only writes ArtSource/Art071/Dice and cache/v071/dice. The review pips are
explicitly excluded from FBX; the game remains the owner of face values/colors.
"""
from pathlib import Path
import bpy
import json
import math
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Art071/Dice"
EXPORT = ROOT / "cache/v071/dice/Export"
for folder in (SOURCE / "Review", EXPORT):
    folder.mkdir(parents=True, exist_ok=True)
assert bpy.app.version[:2] == (5, 2), bpy.app.version_string

PARAMS = {
    "edge": 1.0, "bevel": 0.10, "bevel_segments": 4,
    "ceramic_srgb": "F4E8CB", "roughness": 0.32, "metallic": 0,
    "pip_radius": 0.075, "pip_spacing": 0.22, "pip_segments": 24,
    "face_offset": 0.502, "key_size": 3.5, "key_energy": 700,
}
UNITY_NORMALS = [(0, 1, 0), (0, 0, 1), (1, 0, 0), (-1, 0, 0), (0, 0, -1), (0, -1, 0)]
BLENDER_NORMALS = [(0, 0, 1), (0, -1, 0), (1, 0, 0), (-1, 0, 0), (0, 1, 0), (0, 0, -1)]
PIPS = {1: [(0, 0)], 2: [(-1, 1), (1, -1)], 3: [(-1, 1), (0, 0), (1, -1)],
        4: [(-1, 1), (1, -1), (1, 1), (-1, -1)],
        5: [(-1, 1), (1, -1), (1, 1), (-1, -1), (0, 0)],
        6: [(-1, 1), (1, -1), (1, 1), (-1, -1), (-1, 0), (1, 0)]}

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
scene = bpy.context.scene
scene.unit_settings.system = "METRIC"
scene.unit_settings.scale_length = 1

def empty(name, parent=None):
    obj = bpy.data.objects.new(name, None)
    scene.collection.objects.link(obj)
    obj.parent = parent
    obj.empty_display_size = 0.08
    return obj

def linear(c):
    return c / 12.92 if c <= .04045 else ((c + .055) / 1.055) ** 2.4

def material(name, hex_color, roughness=.32):
    color = tuple(linear(int(hex_color[i:i+2], 16) / 255) for i in (0, 2, 4))
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat.diffuse_color = (*color, 1)
    node = mat.node_tree.nodes["Principled BSDF"]
    node.inputs["Base Color"].default_value = (*color, 1)
    node.inputs["Roughness"].default_value = roughness
    node.inputs["Metallic"].default_value = 0
    return mat

root = empty("DiceRoot")
pivot = empty("RollPivot", root)
orientation = empty("FaceOrientation", pivot)
bpy.ops.mesh.primitive_cube_add(size=PARAMS["edge"])
body = bpy.context.object
body.name = "DiceBody"
body.parent = orientation
bevel = body.modifiers.new("Ceramic roundover", "BEVEL")
bevel.width = PARAMS["bevel"]
bevel.segments = PARAMS["bevel_segments"]
bpy.ops.object.modifier_apply(modifier=bevel.name)
for poly in body.data.polygons:
    poly.use_smooth = True
weighted = body.modifiers.new("Weighted ceramic normals", "WEIGHTED_NORMAL")
weighted.keep_sharp = True
weighted.weight = 50
bpy.ops.object.modifier_apply(modifier=weighted.name)
triangulate = body.modifiers.new("Fixed export triangulation", "TRIANGULATE")
bpy.ops.object.modifier_apply(modifier=triangulate.name)
ceramic = material("Art071_Ceramic", PARAMS["ceramic_srgb"], PARAMS["roughness"])
body.data.materials.append(ceramic)
body["runtime_pips"] = "Independent procedural face layer. No fixed pip geometry or rule colors in this body."
body["bevel_width"] = PARAMS["bevel"]

face_map, markers = [], []
for i, (unity_normal, blender_normal) in enumerate(zip(UNITY_NORMALS, BLENDER_NORMALS), 1):
    normal = Vector(blender_normal)
    marker = empty(f"Face_F{i:02}", orientation)
    marker.location = normal * PARAMS["face_offset"]
    marker.rotation_mode = "QUATERNION"
    marker.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(normal)
    marker["faceId"] = f"F{i:02}"
    markers.append(marker)
    face_map.append({"faceId": f"F{i:02}", "unityNormal": unity_normal,
                     "blenderNormal": blender_normal, "unityCenter": [v * .502 for v in unity_normal],
                     "oppositeFaceId": f"F{7-i:02}", "markerObject": marker.name})

export_objects = [root, pivot, orientation, body] + markers
bpy.ops.object.select_all(action="DESELECT")
for obj in export_objects:
    obj.select_set(True)
bpy.context.view_layer.objects.active = body
preset = dict(use_selection=True, global_scale=1.0, apply_unit_scale=True,
              apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y",
              use_space_transform=True, bake_space_transform=False, object_types={"MESH", "EMPTY"},
              use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
              bake_anim=False, path_mode="AUTO", embed_textures=False)
bpy.ops.export_scene.fbx(filepath=str(EXPORT / "dice_body_bevel.fbx"), **preset)

# Independent source-only proof of accurate 1-6 pip positions. Not an exported
# gameplay model. Optical recessed enamel rings stay above the blank body, so
# arbitrary runtime face values remain possible without a fixed drilled pattern.
review = bpy.data.collections.new("Review_ONLY_not_exported")
scene.collection.children.link(review)
def review_object(obj):
    for col in list(obj.users_collection):
        col.objects.unlink(obj)
    review.objects.link(obj)
    obj["reviewOnly"] = True
    return obj

ink = material("Review_Socket_Shade", "314B4A", .4)
rim = material("Review_Socket_Lip", "F7EED9", .3)
red = material("Review_Red_ExampleOnly", "D84D48", .22)
blue = material("Review_Blue_ExampleOnly", "1686B0", .22)
for i, normal in enumerate(BLENDER_NORMALS, 1):
    normal = Vector(normal)
    tangent = Vector((1, 0, 0)) if abs(normal.x) < .5 else Vector((0, 1, 0))
    bitangent = normal.cross(tangent)
    for j, (px, py) in enumerate(PIPS[i], 1):
        center = tangent * (px * PARAMS["pip_spacing"]) + bitangent * (py * PARAMS["pip_spacing"])
        for suffix, radius, offset, mat in (("Lip", .082, .502, rim), ("Well", .078, .503, ink), ("Enamel", .071, .504, red if i in (1, 4) else blue)):
            bpy.ops.mesh.primitive_uv_sphere_add(segments=24, ring_count=8, radius=1, location=center + normal * offset)
            obj = review_object(bpy.context.object)
            obj.name = f"Review_F{i:02}_Pip{j}_{suffix}"
            obj.rotation_mode = "QUATERNION"
            obj.rotation_quaternion = Vector((0, 0, 1)).rotation_difference(normal)
            obj.scale = (radius, radius, .0015)
            obj.data.materials.append(mat)
            for poly in obj.data.polygons:
                poly.use_smooth = True

felt = material("Review_Felt", "173E40", .9)
bpy.ops.mesh.primitive_plane_add(size=200, location=(0, 0, -.501))
plane = review_object(bpy.context.object)
plane.name = "Review_Table"
plane.data.materials.append(felt)
def look_at(obj, point=(0, 0, 0)):
    obj.rotation_euler = (Vector(point) - obj.location).to_track_quat("-Z", "Y").to_euler()
bpy.ops.object.camera_add(location=(2.2, -3.7, 4.8))
camera = review_object(bpy.context.object)
camera.name = "Review_Camera"
camera.data.type = "ORTHO"
camera.data.ortho_scale = 1.85
look_at(camera)
scene.camera = camera
for name, location, energy, size, color in (
        ("Review_Key", (-3, -4, 6), PARAMS["key_energy"], PARAMS["key_size"], (1, .92, .82)),
        ("Review_Fill", (4, 1, 3), 80, 4, (.65, .86, 1))):
    bpy.ops.object.light_add(type="AREA", location=location)
    light = review_object(bpy.context.object)
    light.name = name
    light.data.energy, light.data.size, light.data.color = energy, size, color
    look_at(light)
scene.world.use_nodes = True
scene.world.node_tree.nodes["Background"].inputs["Color"].default_value = (.12, .16, .17, 1)
scene.world.node_tree.nodes["Background"].inputs["Strength"].default_value = .5
scene.render.engine = "CYCLES"
scene.cycles.device = "CPU"
scene.cycles.samples = 32
scene.render.resolution_x = scene.render.resolution_y = 768
scene.render.resolution_percentage = 100
scene.render.image_settings.file_format = "PNG"
scene.view_settings.view_transform = "AgX"
scene.view_settings.exposure = .6
scene.render.filepath = str(SOURCE / "Review/source_ceramic.png")

data = {"artVersion": "art0.7.1", "units": "metres", "referenceEdgeLength": 1,
        "surfaceOffset": .002, "runtimeConvention": "Unity local +Y up; dynamic pips never identify faceId",
        "faces": face_map, "pipPositions": PIPS, "params": PARAMS,
        "sourcePreviewOnly": "Review collection excluded from export; initial review pip color never enters gameplay"}
(SOURCE / "face_map.json").write_text(json.dumps(data, indent=2), encoding="utf-8")
(SOURCE / "export_preset.json").write_text(json.dumps({k: sorted(v) if isinstance(v, set) else v for k, v in preset.items()}, indent=2), encoding="utf-8")
bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE / "dice_master.blend"))
bpy.ops.render.render(write_still=True)
body.data.calc_loop_triangles()
summary = {"blenderVersion": bpy.app.version_string, "buildHash": bpy.app.build_hash.decode(),
           "bodyTriangles": len(body.data.loop_triangles), "bodyVertices": len(body.data.vertices),
           "dimensions": list(body.dimensions), "exportObjects": [obj.name for obj in export_objects],
           "hasBakedRulePips": False, "unityValidated": False}
(SOURCE / "Review/generation.json").write_text(json.dumps(summary, indent=2), encoding="utf-8")
print("ART071_DICE_GENERATED", json.dumps(summary))
