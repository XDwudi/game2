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
            if(cfg.controllers.Length!=50||cfg.stages.Length!=24||cfg.handDefinitions.Length!=13)throw new InvalidOperationException("Incomplete v0.6 content.");
            ValidateGate();
            var identity=AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/Resources/BuildIdentity.json");
            if(identity==null)throw new InvalidOperationException("Freeze sourceCommit and write BuildIdentity before building.");
            string output=mac?"../Builds/v06/macOS/DicePrototype.app":"../Builds/v06/Windows/DicePrototype.exe";
            Directory.CreateDirectory(Path.GetDirectoryName(output));
            var report=BuildPipeline.BuildPlayer(new BuildPlayerWithProfileOptions{buildProfile=profile,locationPathName=output,options=BuildOptions.StrictMode});
            string evidence="../evidence/v06/unity/build-"+(mac?"macos":"windows")+"-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss")+".json";
            Directory.CreateDirectory(Path.GetDirectoryName(evidence));
            File.WriteAllText(evidence,JsonUtility.ToJson(new Proof{profile=profilePath,platform=target.ToString(),unity=Application.unityVersion,
                result=report.summary.result.ToString(),errors=report.summary.totalErrors,warnings=report.summary.totalWarnings,
                bytes=report.summary.totalSize,seconds=report.summary.totalTime.TotalSeconds,buildGuid=report.summary.guid.ToString(),buildUtc=DateTime.UtcNow.ToString("O"),output=Path.GetFullPath(output),identity=identity.text},true));
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Player build failed: "+report.summary.result);
            Debug.Log("V06_BUILD_SUCCEEDED "+Path.GetFullPath(output));
        }
        private static void ValidateGate()
        {
            const string path="../evidence/v06/unity/build-gate.json";
            if(!File.Exists(path))throw new InvalidOperationException("Run tests and bind their successful evidence before building.");
            var gate=JsonUtility.FromJson<Gate>(File.ReadAllText(path));
            if(gate==null||gate.schema!="v06-build-gate-1"||gate.editModePassed<775||gate.playModePassed<28||gate.files==null||gate.files.Length==0)throw new InvalidOperationException("Incomplete v0.6 test gate (complete discovered EditMode / PlayMode suites required).");
            foreach(var mode in new[]{"EditMode","PlayMode"})
            {
                var discovery=JsonUtility.FromJson<Discovery>(File.ReadAllText("../evidence/v06/unity/discovery-"+mode+".json"));
                int count=mode=="EditMode"?gate.editModePassed:gate.playModePassed;
                var test=gate.tests?.SingleOrDefault(t=>t.mode==mode);
                if(discovery.mode!=mode||discovery.unity!=Application.unityVersion||discovery.names==null||discovery.names.Length!=count||discovery.names.Distinct().Count()!=count||test==null||test.passed!=count||!discovery.names.OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(test.names.OrderBy(x=>x,StringComparer.Ordinal)))
                    throw new InvalidOperationException("Complete discovered test inventory does not match gate: "+mode);
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
            if(identity.sourceCommit!=gate.sourceCommit||identity.contentConfigSha256!=Hash("Assets/Game/Data/P0Config.json")||identity.goldenCaseSetSha256!=Hash("Assets/Game/Data/GoldenScores.json"))
                throw new InvalidOperationException("Build identity does not match frozen source/config/cases.");
        }
        private static string Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        [Serializable]private sealed class Gate{public string schema=null,sourceCommit=null;public TestProof[] tests=null;public int editModePassed=0,playModePassed=0;public Entry[] files=null;}
        [Serializable]private sealed class Discovery{public string mode=null,unity=null;public string[] names=null;}
        [Serializable]private sealed class TestProof{public string mode=null;public int passed;public string[] names=null;}
        [Serializable]private sealed class Identity{public string sourceCommit=null,contentConfigSha256=null,goldenCaseSetSha256=null;}
        [Serializable]private sealed class Entry{public string path=null,sha256=null;}
        [Serializable]private sealed class Proof{public string profile,platform,unity,result,buildGuid,buildUtc,output,identity;public int errors,warnings;public ulong bytes;public double seconds;}
    }
}
