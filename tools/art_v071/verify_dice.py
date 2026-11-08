"""Actual Blender reopen + FBX import inspection, deliberately no Unity claims."""
from pathlib import Path
import bpy
import bmesh
import json
import hashlib

ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "ArtSource/Art071/Dice"
EXPORT = ROOT / "cache/v071/dice/Export/dice_body_bevel.fbx"
bpy.ops.wm.open_mainfile(filepath=str(SOURCE / "dice_master.blend"))
old = json.loads((ROOT / "ArtSource/Art07/Dice/face_map.json").read_text(encoding="utf-8"))
new = json.loads((SOURCE / "face_map.json").read_text(encoding="utf-8"))
checks = []
def check(name, ok, observed):
    checks.append(dict(name=name, passed=bool(ok), observed=observed))
    assert ok, (name, observed)

body = bpy.data.objects["DiceBody"]
body.data.calc_loop_triangles()
check("source_dimensions_1m", all(abs(d-1)<1e-5 for d in body.dimensions), list(body.dimensions))
check("source_body_under_existing_1200tri_budget", len(body.data.loop_triangles)<=1200, len(body.data.loop_triangles))
bm = bmesh.new()
bm.from_mesh(body.data)
check("source_closed_manifold", all(e.is_manifold for e in bm.edges), {"edges":len(bm.edges),"volume":bm.calc_volume()})
bm.free()
check("source_single_neutral_material", len(body.data.materials)==1 and body.data.materials[0].name=="Art071_Ceramic", [m.name for m in body.data.materials])
check("source_same_face_mapping_as_v07", all(a["faceId"]==b["faceId"] and a["unityNormal"]==b["unityNormal"] and a["oppositeFaceId"]==b["oppositeFaceId"] for a,b in zip(old["faces"],new["faces"])) and len(new["faces"])==6, new["faces"])
source_centers={f["markerObject"]:list(bpy.data.objects[f["markerObject"]].matrix_world.translation) for f in new["faces"]}
for f in new["faces"]:
    loc=source_centers[f["markerObject"]]
    check("source_marker_"+f["faceId"], all(abs(loc[i]-.502*f["blenderNormal"][i])<1e-5 for i in range(3)), loc)
for i in range(1,7):
    pips=[o.name for o in bpy.data.objects if o.name.startswith(f"Review_F{i:02}_Pip") and o.name.endswith("_Enamel")]
    check(f"source_preview_{i}_pips",len(pips)==i,pips)

bpy.ops.object.select_all(action="SELECT")
bpy.ops.object.delete(use_global=False)
bpy.ops.import_scene.fbx(filepath=str(EXPORT))
meshes=[o for o in bpy.context.scene.objects if o.type=="MESH"]
check("fbx_blank_body_only_no_review_pips", len(meshes)==1 and meshes[0].name=="DiceBody", [o.name for o in meshes])
imported=meshes[0]
check("fbx_body_dimensions_1m", all(abs(d-1)<1e-5 for d in imported.dimensions), list(imported.dimensions))
check("fbx_hierarchy_preserved", imported.parent.name=="FaceOrientation" and imported.parent.parent.name=="RollPivot" and imported.parent.parent.parent.name=="DiceRoot", [imported.parent.name,imported.parent.parent.name,imported.parent.parent.parent.name])
for name,position in source_centers.items():
    loc=list(bpy.data.objects[name].matrix_world.translation)
    check("fbx_roundtrip_"+name,all(abs(a-b)<1e-5 for a,b in zip(loc,position)),loc)
report={"tool":bpy.app.version_string,"buildHash":bpy.app.build_hash.decode(),"checks":checks,"passed":all(c["passed"] for c in checks),
        "sourceSha256":hashlib.sha256((SOURCE/"dice_master.blend").read_bytes()).hexdigest(),
        "fbxSha256":hashlib.sha256(EXPORT.read_bytes()).hexdigest(),"unityValidated":False}
(SOURCE/"Review/roundtrip.json").write_text(json.dumps(report,indent=2),encoding="utf-8")
print("ART071_SOURCE_ROUNDTRIP",json.dumps(report))
