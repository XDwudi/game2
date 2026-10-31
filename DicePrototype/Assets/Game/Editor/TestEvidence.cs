using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.TestTools.TestRunner.Api;
using UnityEngine;

namespace DiceCraft.Editor
{
    [InitializeOnLoad]
    public static class TestEvidence
    {
        static readonly TestRunnerApi Api;
        static TestEvidence(){Api=ScriptableObject.CreateInstance<TestRunnerApi>();Api.RegisterCallbacks(new Capture());}
        [MenuItem("DiceCraft/Record Complete Test Discovery")]
        public static void DiscoverAll()
        {
            foreach(var mode in new[]{TestMode.EditMode,TestMode.PlayMode})
                Api.RetrieveTestList(mode,tree=>{
                    var names=new List<string>();Collect(tree,names);names.Sort(StringComparer.Ordinal);
                    if(names.Count==0||names.Distinct().Count()!=names.Count)throw new InvalidOperationException("Invalid complete test discovery.");
                    string dir=Path.GetFullPath("../evidence/v06/unity");Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir,"discovery-"+mode+".json"),JsonUtility.ToJson(new Discovery{mode=mode.ToString(),unity=Application.unityVersion,names=names.ToArray()},true));
                });
        }
        static void Collect(ITestAdaptor node,List<string> names){if(!node.IsSuite)names.Add(node.FullName);foreach(var child in node.Children)Collect(child,names);}
        [Serializable]private sealed class Discovery{public string mode,unity;public string[] names;}
        static readonly string[] AssemblyNames={"DiceCraft.Core","DiceCraft.Runtime","DiceCraft.EditMode","DiceCraft.PlayModeTests","DiceCraft.Editor"};
        [Serializable]private sealed class FileProof{public string path,sha256;}
        [Serializable]private sealed class AssemblyProof{public string name,mvid,dllSha256;}
        [Serializable]private sealed class Snapshot{public string utc;public AssemblyProof[] assemblies;public FileProof[] files;}
        [Serializable]private sealed class Binding{public string schema="v06-test-binding-1";public FileProof report;public Snapshot started,finished;}
        static string Hash(string path){using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
        static Snapshot CaptureSnapshot()
        {
            var assemblies=AppDomain.CurrentDomain.GetAssemblies().Where(a=>AssemblyNames.Contains(a.GetName().Name)).OrderBy(a=>a.GetName().Name,StringComparer.Ordinal).ToArray();
            if(assemblies.Length!=AssemblyNames.Length)throw new InvalidOperationException("Missing loaded test assembly.");
            var files=new[]{"Assets","Packages","ProjectSettings"}.SelectMany(scope=>Directory.GetFiles(scope,"*",SearchOption.AllDirectories)).Select(p=>p.Replace('\\','/'))
                .Where(p=>!p.Split('/').Any(part=>part.EndsWith("~",StringComparison.Ordinal))&&p!="Assets/Resources/BuildIdentity.json"&&!p.StartsWith("Assets/InitTestScene",StringComparison.Ordinal)&&!p.StartsWith("Assets/Screenshots",StringComparison.Ordinal))
                .OrderBy(p=>p,StringComparer.Ordinal).Select(p=>new FileProof{path="DicePrototype/"+p,sha256=Hash(p)}).ToArray();
            return new Snapshot{utc=DateTime.UtcNow.ToString("O"),assemblies=assemblies.Select(a=>new AssemblyProof{name=a.GetName().Name,mvid=a.ManifestModule.ModuleVersionId.ToString(),dllSha256=Hash(a.Location)}).ToArray(),files=files};
        }
        private sealed class Capture:ICallbacks
        {
            const string Pending="Temp/v06-active-test-binding.json";
            public void RunStarted(ITestAdaptor tests){File.WriteAllText(Pending,JsonUtility.ToJson(CaptureSnapshot(),true));}
            public void TestStarted(ITestAdaptor test){}
            public void TestFinished(ITestResultAdaptor result){}
            public void RunFinished(ITestResultAdaptor result)
            {
                string dir=Path.GetFullPath("../evidence/v06/unity");Directory.CreateDirectory(dir);
                string stamp=DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
                string report=Path.Combine(dir,"nunit-"+stamp+".xml");
                TestRunnerApi.SaveResultToFile(result,report);
                if(!File.Exists(Pending))throw new InvalidOperationException("Missing actual test-start assembly binding.");
                var binding=new Binding{report=new FileProof{path=Path.GetRelativePath("..",report).Replace('\\','/'),sha256=Hash(report)},started=JsonUtility.FromJson<Snapshot>(File.ReadAllText(Pending)),finished=CaptureSnapshot()};
                File.WriteAllText(Path.ChangeExtension(report,"binding.json"),JsonUtility.ToJson(binding,true));
            }
        }
    }
}
