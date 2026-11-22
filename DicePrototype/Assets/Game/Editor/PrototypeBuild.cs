using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DiceCraft.Core;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DiceCraft.Editor
{
    /// <summary>Both menus and batch builds consume the same active profile and verified source gate.</summary>
    public static class PrototypeBuild
    {
        [MenuItem("DiceCraft/Build Active Verified Profile")]
        public static void BuildActiveProfile()
        {
            var profile=BuildProfile.GetActiveBuildProfile();
            if(profile==null)throw new InvalidOperationException("Select a build profile or use -activeBuildProfile.");
            string profilePath=AssetDatabase.GetAssetPath(profile);
            bool mac=profilePath.EndsWith("/macOS_Universal.asset",StringComparison.Ordinal);
            bool win=profilePath.EndsWith("/Windows_x64.asset",StringComparison.Ordinal);
            if(!mac&&!win)throw new InvalidOperationException("Unsupported delivery profile: "+profilePath);
            var target=mac?BuildTarget.StandaloneOSX:BuildTarget.StandaloneWindows64;
            if(EditorUserBuildSettings.activeBuildTarget!=target)throw new InvalidOperationException("Launch a separate Editor with the correct active profile/platform.");
            var cfg=JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText("Assets/Game/Data/P0Config.json")));cfg.Validate();
            if(cfg.controllers.Length!=150||cfg.stages.Length!=24||cfg.handDefinitions.Length!=13||cfg.bossModifiers.Length!=39)throw new InvalidOperationException("Incomplete p1.1.0 content.");
            ValidateGate();
            var identity=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/BuildIdentity.json");
            if(identity==null)throw new InvalidOperationException("Freeze sourceCommit and write BuildIdentity before building.");
            string output=mac?"../Builds/v11/macOS/DicePrototype.app":"../Builds/v11/Windows/DicePrototype.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions{buildProfile=profile,locationPathName=output,options=BuildOptions.StrictMode | BuildOptions.DetailedBuildReport});
            string evidence="../evidence/v11/unity/build-"+(mac?"macos":"windows")+"-"+report.summary.guid+".json";
            Directory.CreateDirectory(Path.GetDirectoryName(evidence));
            var proof=new Proof{profile=profilePath,platform=target.ToString(),unity=Application.unityVersion,
                result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,
                bytes=report.summary.totalSize,seconds=report.summary.totalTime.TotalSeconds,buildGuid=report.summary.guid.ToString(),
                buildUtc=report.summary.buildEndedAt.ToUniversalTime().ToString("O"),output=Path.GetFullPath(output),identity=identity.text,
                gateSha256=Hash("../evidence/v11/unity/build-gate.json")};
            if(report.summary.result==BuildResult.Succeeded)
            {
                string native=Path.ChangeExtension(evidence,"buildreport");
                File.Copy("Library/LastBuild.buildreport",native,false);
                proof.nativeBuildReport=new Entry{path=Path.GetRelativePath("..",native).Replace('\\','/'),sha256=Hash(native)};
                var directory=Path.GetDirectoryName(proof.output);
                proof.playerFiles=report.GetFiles().Select(f=>Path.GetRelativePath(directory,Path.GetFullPath(f.path)).Replace('\\','/'))
                    .Where(p=>!p.Split('/').Any(segment=>segment.EndsWith("_BackUpThisFolder_ButDontShipItWithYourGame",StringComparison.Ordinal)
                        ||segment.EndsWith("_BurstDebugInformation_DoNotShip",StringComparison.Ordinal)))
                    .OrderBy(p=>p,StringComparer.Ordinal).Select(p=>new Entry{path=p,sha256=Hash(Path.Combine(directory,p))}).ToArray();
            }
            File.WriteAllText(evidence,JsonUtility.ToJson(proof,true));
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Player build failed: "+report.summary.result);
            Debug.Log("V11_BUILD_SUCCEEDED "+Path.GetFullPath(output));
        }
        private static void ValidateGate()
        {
            const string path="../evidence/v11/unity/build-gate.json";
            if(!File.Exists(path))throw new InvalidOperationException("Run tests and bind their successful evidence before building.");
            var gate=JsonUtility.FromJson<Gate>(File.ReadAllText(path));
            if(gate==null||gate.schema!="v11-build-gate-1"||gate.editModePassed<797||gate.playModePassed<30||gate.files==null||gate.files.Length==0)throw new InvalidOperationException("Incomplete p1.1.0 test gate.");
            Action<Entry> requireEvidence=e=>{if(e==null||string.IsNullOrEmpty(e.path)||string.IsNullOrEmpty(e.sha256)||!gate.files.Any(f=>f.path==e.path&&f.sha256==e.sha256))throw new InvalidOperationException("Required verification evidence is absent from the bound files.");};
            requireEvidence(gate.pdbAudit);
            if(gate.tests==null||gate.tests.Length!=3)throw new InvalidOperationException("One complete EditMode and two complete PlayMode reports are required.");
            foreach(var mode in new[]{"EditMode","PlayMode"})
            {
                string discoveryPath="evidence/v11/unity/discovery-"+mode+".json";
                requireEvidence(gate.files.SingleOrDefault(f=>f.path==discoveryPath));
                var discovery=JsonUtility.FromJson<Discovery>(File.ReadAllText("../"+discoveryPath));
                int count=mode=="EditMode"?gate.editModePassed:gate.playModePassed;
                foreach(string resolution in mode=="EditMode"?new[]{""}:new[]{"1920x1080","1280x720"})
                {
                    var test=gate.tests.SingleOrDefault(t=>t.mode==mode&&(t.resolution??"")==resolution);
                    if(discovery.mode!=mode||discovery.unity!=Application.unityVersion||discovery.names==null||discovery.names.Length!=count||discovery.names.Distinct().Count()!=count||test==null||test.passed!=count||!discovery.names.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(test.names.OrderBy(x=>x,StringComparer.Ordinal)))
                        throw new InvalidOperationException("Complete discovery mismatch: "+mode+" "+resolution);
                    requireEvidence(test.report);requireEvidence(test.binding);
                }
            }
            string[] scopes={"DicePrototype/Assets","DicePrototype/Packages","DicePrototype/ProjectSettings"};
            var current=scopes.SelectMany(scope=>Directory.EnumerateFiles(Path.Combine("..",scope),"*",SearchOption.AllDirectories))
                .Select(path=>Path.GetRelativePath("..",path).Replace('\\','/'))
                .Where(path=>!path.Split('/').Any(part=>part.EndsWith("~",StringComparison.Ordinal))).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
            var bound=gate.files.Where(f=>scopes.Any(scope=>f.path.StartsWith(scope+"/",StringComparison.Ordinal))).Select(f=>f.path).OrderBy(path=>path,StringComparer.Ordinal).ToArray();
            if(!current.SequenceEqual(bound))throw new InvalidOperationException("Buildable source file set changed after verification.");
            if(gate.files.Select(f=>f.path).Distinct().Count()!=gate.files.Length)throw new InvalidOperationException("Duplicate build-gate file.");
            foreach(var file in gate.files)
            {
                string full=Path.GetFullPath(Path.Combine("..",file.path));
                if(!full.StartsWith(Path.GetFullPath("..")+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(full)||Hash(full)!=file.sha256)
                    throw new InvalidOperationException("Verification source/evidence changed: "+file.path);
            }
            var identity=JsonUtility.FromJson<Identity>(File.ReadAllText("Assets/Resources/BuildIdentity.json"));
            // v1.1: rules p1.1.0 on save schema 7; the art version follows GameApp.ArtVersion (tools/v11_delivery.py ART_VERSION).
            if(identity.releaseVersion!="p1.1.0"||identity.artVersion!=GameApp.ArtVersion||identity.contentConfigVersion!="p1.1.0"||identity.saveSchemaVersion!=7)
                throw new InvalidOperationException("Release identity must be p1.1.0 rules on save schema 7 with the "+GameApp.ArtVersion+" presentation.");
            if(identity.sourceCommit!=gate.sourceCommit||identity.contentConfigSha256!=Hash("Assets/Game/Data/P0Config.json")||identity.goldenCaseSetSha256!=Hash("Assets/Game/Data/GoldenScores.json"))
                throw new InvalidOperationException("Build identity does not match frozen source/config/cases.");
        }
        private static string Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        [Serializable]private sealed class Gate{public string schema=null,sourceCommit=null;public TestProof[] tests=null;public Entry pdbAudit=null;public int editModePassed=0,playModePassed=0;public Entry[] files=null;}
        [Serializable]private sealed class Discovery{public string mode=null,unity=null;public string[] names=null;}
        [Serializable]private sealed class TestProof{public string mode=null,resolution=null;public int passed;public string[] names=null;public Entry report=null,binding=null;}
        [Serializable]private sealed class Identity{public string sourceCommit=null,contentConfigSha256=null,goldenCaseSetSha256=null,releaseVersion=null,artVersion=null,contentConfigVersion=null;public int saveSchemaVersion;}
        [Serializable]private sealed class Entry{public string path=null,sha256=null;}
        [Serializable]private sealed class Proof{public string schema="v11-player-build-1",gateSha256;public Entry nativeBuildReport;public Entry[] playerFiles;public string profile,platform,unity,result,buildGuid,buildUtc,output,identity;public int errors,warnings;public ulong bytes;public double seconds;}
    }
}
