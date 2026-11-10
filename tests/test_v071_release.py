"""Offline synthetic release-boundary tests, not Unity/game/remote-release results."""
import contextlib
import hashlib
import io
import json
from pathlib import Path
import struct
import sys
import tempfile
import unittest
from unittest import mock
import urllib.request
import zipfile

TOOLS=Path(__file__).resolve().parents[1]/"tools"
sys.path.insert(0,str(TOOLS))
import package_v071 as package
import release_v071_manifest as manifest
import publish_v071_release as publisher


class V071ReleaseTests(unittest.TestCase):
    def test_binary_only_anchor_requires_exact_existing_remote_commit(self):
        source, anchor = "a" * 40, "b" * 40
        self.assertEqual(source, publisher.release_anchor(source))
        self.assertEqual(anchor, publisher.release_anchor(source, anchor))
        request = mock.Mock(return_value={"sha": anchor})
        publisher.require_existing_anchor(request, anchor)
        request.assert_called_once_with(publisher.API + "/commits/" + anchor)
        for invalid in ("main", "../other", "b" * 39, "B" * 40):
            with self.subTest(invalid=invalid), self.assertRaises(ValueError):
                publisher.require_existing_anchor(request, invalid)
        request.return_value = {"sha": source}
        with self.assertRaises(ValueError):
            publisher.require_existing_anchor(request, anchor)

    def test_binary_only_body_distinguishes_build_source_from_tag(self):
        value = {"sourceCommit": "a" * 40, "tests": {"unityEditMode": {"passed": 1}, "unityPlayMode": {"passed": 2}},
                 "windowsPerformance": {"sampleSeconds": 180, "p95FrameIntervalMs": 16}, "platforms": [],
                 "macVerificationKit": {"packagePath": "kit.zip", "byteSize": 1, "sha256": "c" * 64},
                 "ruleReproduction": {"normalOutcome": "fixture", "normalFinalStage": 8}}
        before = json.dumps(value, sort_keys=True)
        body = publisher.release_body(value, "b" * 40)
        self.assertIn("未推送本次游戏Git源码提交", body)
        self.assertIn("Mac验证工具包含", body)
        self.assertIn("Source code", body)
        self.assertIn("a" * 40, body)
        self.assertIn("b" * 40, body)
        self.assertEqual(before, json.dumps(value, sort_keys=True))

    def test_binary_only_anchor_does_not_relax_remote_draft_binding(self):
        source, anchor = "a" * 40, "b" * 40
        release = {"tag_name": publisher.TAG, "name": "title", "body": "body", "prerelease": True,
                   "draft": True, "target_commitish": anchor}
        publisher.verify_remote_release(release, anchor, "title", "body", "upload", None)
        with self.assertRaises(ValueError):
            publisher.verify_remote_release(release, source, "title", "body", "upload", None)

    def setUp(self):
        self.temp=tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root=Path(self.temp.name).resolve()
        for module in (package,manifest,publisher):
            patch=mock.patch.object(module,"ROOT",self.root);patch.start();self.addCleanup(patch.stop)
        self.identity={"sourceCommit":"a"*40,"releaseVersion":"p0.7.1","artVersion":"art0.7.1"}

    def put(self,name,data):
        path=self.root/name;path.parent.mkdir(parents=True,exist_ok=True)
        path.write_bytes(data if isinstance(data,bytes) else data.encode("utf8"))
        return path

    def js(self,name,value):
        return self.put(name,json.dumps(value,ensure_ascii=False))

    def bind(self,path):
        return {"path":path.relative_to(self.root).as_posix(),"sha256":package.sha(path)}

    def full_art_fixture(self):
        ids=[f"C{i:02}" for i in range(1,54) if i not in (4,16,23)]
        config={"controllers":[{"id":x} for x in ids]}
        for key,prefix in (("consumables","I"),("services","O"),("diceUpgrades","U")):
            config[key]=[{"id":f"{prefix}{i:02}"} for i in range(1,8)]
            ids += [r["id"] for r in config[key]]
        self.js(package.CONFIG,config)
        staged=[];frozen=[]
        def asset(identity,source,kind):
            path=self.put(source,("source:"+identity).encode())
            runtime="Assets/Art/Art071/Illustrations/"+identity+".png" if kind=="illustration" else "Assets/Art/Art071/Full/"+identity+".png"
            run=self.put("DicePrototype/"+runtime,path.read_bytes())
            staged.append({"id":identity,"source":source,"runtime":runtime,"sha256":package.sha(path),"kind":kind})
            frozen.append(self.bind(run))
            return {"id":identity,"export":source,"sha256":package.sha(path)}
        for identity in ids:
            row=asset("illustration_"+identity.lower(),"ArtSource/Art071/IllustrationsFull/"+identity+"/original.png","illustration")
            self.js("ArtSource/Art071/IllustrationsFull/"+identity+"/record.json",{"id":identity,"file":"original.png","sha256":row["sha256"]})
        for group,count in (("UI",43),("Surfaces",12)):
            rows=[asset(group.lower()+str(i),f"ArtSource/Art071/Full/{group}/asset{i}.png","public") for i in range(count)]
            self.js(f"ArtSource/Art071/Full/{group}/manifest.json",{"assets":rows})
        asset("bg_felt_tile","ArtSource/Art071/Full/Backgrounds/bg_felt_tile.png","background")
        path=self.js(manifest.STAGED_MANIFEST,{"complete":True,"missing":[],"assets":staged})
        frozen.append(self.bind(path))
        evidence=[]
        for kind in ("unity-import","runtime-visual","independent-review"):
            evidence.append({**self.bind(self.put("evidence/"+kind+".txt","synthetic fixture, not a real result")),"kind":kind})
        review={"schema":"v071-full-art-review-1","status":"full_art_review_complete","identity":self.identity,
                "stagedManifest":self.bind(path),"unresolvedBlockingArtDefects":[],"humanArtAccepted":False,"evidence":evidence}
        return {"files":frozen},self.js("evidence/full-art-review.json",review)

    def test_full_scope_includes_real_o_service_ids_and_rejects_missing_art(self):
        gate,review=self.full_art_fixture()
        result=manifest.validate_full_art(self.identity,gate,review)
        self.assertEqual(71,result["illustrationCount"])
        self.assertIn("O07",result["activeIds"])
        staged=package.read_json(self.root/manifest.STAGED_MANIFEST);staged["assets"].pop()
        self.js(manifest.STAGED_MANIFEST,staged)
        with self.assertRaisesRegex(ValueError,"exactly 71"):
            manifest.validate_full_art(self.identity,gate,review)

    def test_first_batch_source_is_rejected_even_with_complete_staging(self):
        gate,review=self.full_art_fixture()
        identity={**self.identity,"sourceCommit":manifest.FIRST_BATCH_COMMIT}
        with self.assertRaisesRegex(ValueError,"249d980"):
            manifest.validate_full_art(identity,gate,review)

    def test_runtime_bytes_and_frozen_gate_are_both_required(self):
        gate,review=self.full_art_fixture()
        target=self.root/gate["files"][0]["path"]
        original=target.read_bytes();target.write_bytes(b"wrong runtime")
        with self.assertRaisesRegex(ValueError,"source/runtime/gate"):
            manifest.validate_full_art(self.identity,gate,review)
        target.write_bytes(original)
        gate["files"][0]["sha256"]="0"*64
        with self.assertRaisesRegex(ValueError,"source/runtime/gate"):
            manifest.validate_full_art(self.identity,gate,review)

    def test_cross_id_art_substitution_rejected_even_with_consistent_gate(self):
        gate,review_path=self.full_art_fixture()
        staged=package.read_json(self.root/manifest.STAGED_MANIFEST)
        first,second=staged["assets"][:2]
        second["source"]=first["source"];second["sha256"]=first["sha256"]
        runtime=self.put("DicePrototype/"+second["runtime"],(self.root/first["source"]).read_bytes())
        gate["files"][1]=self.bind(runtime)
        path=self.js(manifest.STAGED_MANIFEST,staged);gate["files"][-1]=self.bind(path)
        review=package.read_json(review_path);review["stagedManifest"]=self.bind(path);self.js("evidence/full-art-review.json",review)
        with self.assertRaisesRegex(ValueError,"ID/selected source/record mismatch"):
            manifest.validate_full_art(self.identity,gate,review_path)

    def test_independent_review_and_unchanged_evidence_required(self):
        gate,review_path=self.full_art_fixture();review=package.read_json(review_path)
        evidence=review["evidence"].pop();self.js("evidence/full-art-review.json",review)
        with self.assertRaisesRegex(ValueError,"independent review"):
            manifest.validate_full_art(self.identity,gate,review_path)
        review["evidence"].append(evidence);self.js("evidence/full-art-review.json",review)
        self.put(evidence["path"],"changed review")
        with self.assertRaisesRegex(ValueError,"SHA/path changed"):
            manifest.validate_full_art(self.identity,gate,review_path)

    def test_separate_package_paths_reject_old_proofs_and_escape(self):
        p,q=manifest.validate_directories(manifest.PACKAGE_DIRECTORY,manifest.PROOF_DIRECTORY)
        self.assertEqual(self.root/manifest.PACKAGE_DIRECTORY,p)
        for a,b in (("Releases/p0.7.1/review-candidate",manifest.PROOF_DIRECTORY),
                    (manifest.PACKAGE_DIRECTORY,"evidence/v071/packages"),
                    ("../outside",manifest.PROOF_DIRECTORY)):
            with self.subTest(a=a,b=b),self.assertRaises(ValueError):
                manifest.validate_directories(a,b)

    def zip_fixture(self):
        sources={"Licenses/NotoSansCJK-OFL.txt":"DicePrototype/Assets/Game/Fonts/LICENSE.txt",
                 "Licenses/NotoSansSC-Bold-OFL.txt":"ArtSource/Art071/Fonts/LICENSE.txt",
                 "Licenses/NotoSansSC-Bold-COPYRIGHT.txt":"ArtSource/Art071/Fonts/COPYRIGHT.txt",
                 "开始游玩.txt":"docs/PLAYER_README_V071.txt"}
        content={name:self.put(source,"fixture "+name).read_bytes() for name,source in sources.items()}
        content.update({"BUILD_IDENTITY.json":json.dumps(self.identity).encode(),"DicePrototype.exe":b"synthetic executable bytes"})
        path=self.root/manifest.PACKAGE_DIRECTORY/"DicePrototype-p0.7.1-win-x64.zip"
        return self.write_zip(path,content),content

    def write_zip(self,path,content):
        path.parent.mkdir(parents=True,exist_ok=True)
        with zipfile.ZipFile(path,"w") as archive:
            for name,data in content.items():archive.writestr(name,data)
        with zipfile.ZipFile(path) as archive:
            rows=[{"path":i.filename,"sha256":hashlib.sha256(archive.read(i)).hexdigest(),"byteSize":i.file_size,"zipMode":oct(i.external_attr>>16)} for i in archive.infolist()]
        return {"identity":self.identity,"packagePath":path.relative_to(self.root).as_posix(),"sha256":package.sha(path),"byteSize":path.stat().st_size,"files":rows}

    def test_zip_member_hash_detects_tamper_even_if_outer_hash_updated(self):
        proof,content=self.zip_fixture()
        manifest.validate_zip_proof(proof,self.identity,self.root/manifest.PACKAGE_DIRECTORY)
        content["DicePrototype.exe"]=b"different executable"
        changed=self.write_zip(self.root/proof["packagePath"],content)
        proof.update({k:changed[k] for k in ("sha256","byteSize")})
        with self.assertRaisesRegex(ValueError,"ZIP member differs"):
            manifest.validate_zip_proof(proof,self.identity,self.root/manifest.PACKAGE_DIRECTORY)

    def test_missing_bold_license_rejected_with_self_consistent_zip_proof(self):
        proof,content=self.zip_fixture();del content["Licenses/NotoSansSC-Bold-COPYRIGHT.txt"]
        proof=self.write_zip(self.root/proof["packagePath"],content)
        with self.assertRaisesRegex(ValueError,"font licenses"):
            manifest.validate_zip_proof(proof,self.identity,self.root/manifest.PACKAGE_DIRECTORY)

    def test_zip_identity_and_candidate_limitations_are_checked(self):
        proof,content=self.zip_fixture()
        content["BUILD_IDENTITY.json"]=json.dumps({**self.identity,"sourceCommit":"b"*40}).encode()
        proof=self.write_zip(self.root/proof["packagePath"],content)
        with self.assertRaisesRegex(ValueError,"embedded identity"):
            manifest.validate_zip_proof(proof,self.identity,self.root/manifest.PACKAGE_DIRECTORY)
        content["BUILD_IDENTITY.json"]=json.dumps(self.identity).encode();content["REVIEW_CANDIDATE.json"]=b'{"status":"passed"}'
        proof=self.write_zip(self.root/proof["packagePath"],content)
        with self.assertRaisesRegex(ValueError,"diagnostic limitation"):
            manifest.validate_zip_proof(proof,self.identity,self.root/manifest.PACKAGE_DIRECTORY,{"status":"FAILED"})

    def test_native_slices_are_read_from_actual_build_not_just_proof(self):
        # A structurally valid single x86_64 Mach-O must fail Universal even when a
        # caller supplies a fabricated two-architecture inventory in its proof.
        name="DicePrototype.app/Contents/MacOS/DiceCraft P0"
        path=self.put("Builds/v071/macOS/"+name,struct.pack("<IIIIIII",0xfeedfacf,0x01000007,3,2,0,0,0))
        proof={"files":[{"path":name,"sha256":package.sha(path),"zipMode":"0o100755"}],"nativeComponents":[{"architectures":["arm64","x86_64"]}]}
        build={"output":str(self.root/"Builds/v071/macOS/DicePrototype.app")}
        with self.assertRaisesRegex(ValueError,"not Universal"):
            manifest.validate_packaged_build(proof,build,"macos")

    def test_packager_default_paths_and_workspace_boundary(self):
        self.assertEqual((self.root/package.REVIEW_RELEASE,self.root/"evidence/v071/packages"),package.package_directories(True))
        self.assertEqual((self.root/package.RELEASE,self.root/"evidence/v071/packages"),package.package_directories(False))
        for output,proofs in (("../outside",manifest.PROOF_DIRECTORY),(manifest.PACKAGE_DIRECTORY,"../outside")):
            with self.subTest(output=output,proofs=proofs),self.assertRaisesRegex(ValueError,"escapes repository"):
                package.package_directories(True,output,proofs)

    def test_existing_proof_or_zip_blocks_before_creating_new_package(self):
        proof=self.put(manifest.PROOF_DIRECTORY+"/"+package.package_proof_name("windows",True),"preserve old proof")
        output=self.root/manifest.PACKAGE_DIRECTORY/"DicePrototype-p0.7.1-win-x64.zip"
        with mock.patch.object(package,"validated_gate") as gate:
            with self.assertRaisesRegex(ValueError,"already exists"):
                package.package("windows",self.root/"not-needed.json",True,package_directory=manifest.PACKAGE_DIRECTORY,package_proof_directory=manifest.PROOF_DIRECTORY)
            gate.assert_not_called()
        self.assertFalse(output.exists());self.assertEqual("preserve old proof",proof.read_text())
        proof.unlink();self.put(output.relative_to(self.root).as_posix(),b"preserve old ZIP")
        with self.assertRaisesRegex(ValueError,"already exists"):
            package.package("windows",self.root/"not-needed.json",True,package_directory=manifest.PACKAGE_DIRECTORY,package_proof_directory=manifest.PROOF_DIRECTORY)
        self.assertEqual(b"preserve old ZIP",output.read_bytes());self.assertFalse(proof.exists())

    def test_custom_directories_build_two_zips_and_kit_from_actual_fixture_bytes(self):
        for name in ("DicePrototype/Assets/Game/Fonts/LICENSE.txt","ArtSource/Art071/Fonts/LICENSE.txt","ArtSource/Art071/Fonts/COPYRIGHT.txt","docs/PLAYER_README_V071.txt"):
            self.put(name,"synthetic metadata "+name)
        self.js(package.IDENTITY,self.identity)
        self.put("Builds/v071/Windows/DicePrototype.exe",b"synthetic Windows build")
        self.put("Builds/v071/Windows/DicePrototype_Data/Managed/DiceCraft.Core.dll",b"synthetic managed build")
        self.put("Builds/v071/macOS/DicePrototype.app/Contents/Info.plist",b"synthetic plist")
        fat=bytearray(0x200+28)
        fat[:8]=struct.pack(">II",0xcafebabe,2)
        for i,(cpu,offset) in enumerate(((0x01000007,0x100),(0x0100000c,0x200))):
            fat[8+i*20:28+i*20]=struct.pack(">IIIII",cpu,0,offset,28,8)
            fat[offset:offset+28]=struct.pack("<IIIIIII",0xfeedfacf,cpu,0,2,0,0,0)
        self.put("Builds/v071/macOS/DicePrototype.app/Contents/MacOS/DiceCraft P0",bytes(fat))
        builds={}
        for platform,output in (("windows","Builds/v071/Windows/DicePrototype.exe"),("macos","Builds/v071/macOS/DicePrototype.app")):
            builds[platform]={"output":str(self.root/output),"buildGuid":"fixture-"+platform}
            self.js("build-"+platform+".json",builds[platform])
        for name in ("VerifyMac.command","verify_mac.py","package_checks.py","compare_replays.py"):
            self.put("tools/macos/v071/"+name,"synthetic kit source "+name)
        self.put("docs/tasks/P0-V071-MAC-VERIFICATION.md","synthetic instructions")
        for name in ("normal-run-trace.json","debug-24-trace.json"):
            self.put("evidence/v07/session/"+name,"synthetic trace "+name)
        results=self.root/"evidence/fixture-player"
        for name in ("player-report.json","normal-replay.jsonl","debug24-replay.jsonl","cross-platform-checkpoint/active-run.json","cross-platform-checkpoint/command.json","cross-platform-checkpoint/expected.json"):
            self.put("evidence/fixture-player/"+name,"synthetic player "+name)
        historical=self.put("evidence/v071/packages/macos-package-review-candidate.json","must remain untouched")
        with mock.patch.object(package,"validated_gate",return_value=(self.identity,{},{})), \
             mock.patch.object(package,"validate_build",side_effect=lambda path,platform,identity:builds[platform]), \
             mock.patch.object(package,"validate_windows_report",return_value={}):
            proofs={platform:package.package(platform,self.root/("build-"+platform+".json"),True,
                      package_directory=manifest.PACKAGE_DIRECTORY,package_proof_directory=manifest.PROOF_DIRECTORY) for platform in ("windows","macos")}
            kit=package.verification_kit(results,True,package_directory=manifest.PACKAGE_DIRECTORY,package_proof_directory=manifest.PROOF_DIRECTORY)
        self.assertEqual("must remain untouched",historical.read_text())
        for platform,proof in proofs.items():
            self.assertEqual(self.root/manifest.PACKAGE_DIRECTORY,package.check_package(proof).parent)
            self.assertTrue((self.root/manifest.PROOF_DIRECTORY/package.package_proof_name(platform,True)).is_file())
            manifest.validate_packaged_build(proof,builds[platform],platform)
        with zipfile.ZipFile(package.check_package(kit)) as archive:
            self.assertEqual((self.root/proofs["macos"]["packagePath"]).read_bytes(),archive.read("DicePrototype-MacVerification/inputs/macos.zip"))
            for row in kit["inputs"]:
                self.assertEqual(row["sha256"],hashlib.sha256(archive.read("DicePrototype-MacVerification/"+row["path"])).hexdigest())
        self.assertTrue((self.root/manifest.PROOF_DIRECTORY/package.kit_proof_name(True)).is_file())

    def test_authenticated_url_and_cross_host_redirect_strip_token(self):
        publisher.authenticated_url("https://api.github.com/repos/XDwudi/game2/releases")
        for url in ("http://api.github.com/repos/XDwudi/game2/releases","https://api.github.com.evil.test/repos/XDwudi/game2/releases",
                    "https://api.github.com/repos/else/game2/releases","https://user:secret@api.github.com/repos/XDwudi/game2/releases"):
            with self.subTest(url=url),self.assertRaises(ValueError):publisher.authenticated_url(url)
        req=urllib.request.Request("https://api.github.com/repos/XDwudi/game2/releases/assets/1",headers={"Authorization":"Bearer synthetic-token"})
        redirected=publisher.SafeRedirect().redirect_request(req,None,302,"redirect",{},"https://release-assets.githubusercontent.com/object")
        self.assertFalse(redirected.has_header("Authorization"))
        with self.assertRaises(ValueError):publisher.SafeRedirect().redirect_request(req,None,302,"redirect",{},"http://example.test/object")

    def test_existing_release_must_be_exact_owned_draft(self):
        value={"tag_name":package.TAG,"name":"title","body":"bound body","prerelease":True,"draft":True,"target_commitish":"a"*40}
        publisher.verify_remote_release(value,"a"*40,"title","bound body","upload",None)
        for field,new in (("body","unrelated existing draft"),("draft",False),("target_commitish","b"*40)):
            changed={**value,field:new}
            with self.subTest(field=field),self.assertRaises(ValueError):publisher.verify_remote_release(changed,"a"*40,"title","bound body","upload",None)

    def test_publish_requires_download_proof_for_exact_asset_and_manifest(self):
        local=self.put("artifacts/cache/download.zip",b"actual downloaded fixture")
        mp=self.put("manifest.json","frozen manifest")
        item={"sha256":package.sha(local),"byteSize":local.stat().st_size}
        asset={"id":123,"name":"attachment.zip"}
        proof={"assetId":123,"name":"attachment.zip",**item,"httpStatus":200,"crc":"passed","sourceCommit":"a"*40,
               "manifestSha256":package.sha(mp),"matchesFrozenManifest":True,"path":str(local)}
        publisher.verify_download_proof(proof,asset,item,"a"*40,mp)
        for field,new in (("assetId",124),("manifestSha256","0"*64),("matchesFrozenManifest",False)):
            with self.subTest(field=field),self.assertRaises(ValueError):publisher.verify_download_proof({**proof,field:new},asset,item,"a"*40,mp)
        local.write_bytes(b"replaced")
        with self.assertRaisesRegex(ValueError,"bytes changed"):publisher.verify_download_proof(proof,asset,item,"a"*40,mp)

    def test_stale_input_is_rejected_before_credentials_or_network(self):
        with mock.patch.object(sys,"argv",["publish_v071_release.py","draft"]), \
             mock.patch.object(publisher.subprocess,"check_output",return_value="https://github.com/XDwudi/game2.git\n"), \
             mock.patch.object(publisher,"release_inputs",side_effect=ValueError("stale full art")), \
             mock.patch.object(publisher.subprocess,"run") as credential, \
             mock.patch.object(publisher.urllib.request,"build_opener") as network:
            with self.assertRaisesRegex(ValueError,"stale full art"):publisher.main()
            credential.assert_not_called();network.assert_not_called()

    def test_offline_check_never_reads_credentials_or_connects(self):
        mp=self.put("manifest.json","offline fixture")
        with mock.patch.object(sys,"argv",["publish_v071_release.py","check","--manifest",str(mp)]), \
             mock.patch.object(publisher.subprocess,"check_output",return_value="https://github.com/XDwudi/game2.git\n"), \
             mock.patch.object(publisher,"release_inputs",return_value=({}, {}, "a"*40)), \
             mock.patch.object(publisher,"release_title",return_value="fixture"), \
             mock.patch.object(publisher,"release_body",return_value="fixture body"), \
             mock.patch.object(publisher.subprocess,"run") as credential, \
             mock.patch.object(publisher.urllib.request,"build_opener") as network, \
             contextlib.redirect_stdout(io.StringIO()) as output:
            publisher.main()
            credential.assert_not_called();network.assert_not_called()
            self.assertEqual("offline_inputs_verified_no_network",json.loads(output.getvalue())["status"])

    def test_diagnostic_scope_never_silently_becomes_full_pass(self):
        value={"reviewCandidate":True,"allowFailedWindowsPerformance":True}
        with self.assertRaises(ValueError):publisher.ensure_publish_allowed(value,"publish",True,False)
        with self.assertRaises(ValueError):publisher.ensure_publish_allowed(value,"publish",False,True)
        publisher.ensure_publish_allowed(value,"publish",True,True)


if __name__=="__main__":
    unittest.main()
