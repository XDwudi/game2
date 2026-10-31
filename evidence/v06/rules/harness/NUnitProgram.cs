using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using NUnit.Framework;

// Managed preflight adapter only. Unity EditMode execution remains mandatory and is separate.
namespace UnityEngine
{
    public static class Application { public static string dataPath => Path.GetFullPath("DicePrototype/Assets"); }
    public static class JsonUtility
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
        public static T FromJson<T>(string json) => JsonSerializer.Deserialize<T>(json, Options);
        public static string ToJson(object value) => JsonSerializer.Serialize(value, value.GetType(), Options);
    }
}
internal static class RulesNUnitProgram
{
    public static int Main()
    {
        var results=new List<object>();int passed=0,failed=0;
        foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(t=>t.Namespace=="DiceCraft.Core.Tests" && t.GetMethods().Any(m=>m.GetCustomAttribute<SetUpAttribute>()!=null)).OrderBy(t=>t.Name))
        foreach(var method in type.GetMethods().Where(m=>m.GetCustomAttribute<TestAttribute>()!=null||m.GetCustomAttributes<TestCaseAttribute>().Any()||m.GetCustomAttributes<TestCaseSourceAttribute>().Any()).OrderBy(m=>m.Name))
        {
            var cases=new List<(string name,object[] args)>();
            foreach(var c in method.GetCustomAttributes<TestCaseAttribute>())cases.Add((c.TestName??method.Name+"("+string.Join(",",c.Arguments)+")",c.Arguments));
            foreach(var source in method.GetCustomAttributes<TestCaseSourceAttribute>())
            {
                var provider=type.GetMethod(source.SourceName,BindingFlags.Public|BindingFlags.Static);
                foreach(var c in (IEnumerable)provider.Invoke(null,null))
                {
                    var test=(TestCaseData)c;cases.Add((test.TestName??method.Name,test.Arguments));
                }
            }
            if(cases.Count==0)cases.Add((method.Name,Array.Empty<object>()));
            foreach(var c in cases)
            {
                string name=type.Name+"."+c.name;
                try
                {
                    object instance=Activator.CreateInstance(type);
                    foreach(var setup in type.GetMethods().Where(m=>m.GetCustomAttribute<SetUpAttribute>()!=null))setup.Invoke(instance,null);
                    method.Invoke(instance,c.args);passed++;results.Add(new{name,result="Passed"});
                }
                catch(Exception ex)
                {
                    var e=ex is TargetInvocationException&&ex.InnerException!=null?ex.InnerException:ex;
                    failed++;results.Add(new{name,result="Failed",error=e.ToString()});Console.WriteLine("FAIL "+name+"\n"+e);
                }
            }
        }
        var output="evidence/v06/rules/nunit-managed-report.json";
        var files=Directory.GetFiles("DicePrototype/Assets/Game/Core/Rules","*.cs").Concat(Directory.GetFiles("DicePrototype/Assets/Game/Tests/EditMode/Rules","*.cs")).Concat(new[]{"DicePrototype/Assets/Game/Data/P0Config.json","DicePrototype/Assets/Game/Data/GoldenScores.json"});
        var sourceHashes=files.OrderBy(p=>p,StringComparer.Ordinal).Select(p=>new{path=p,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant()}).ToArray();
        File.WriteAllText(output,JsonSerializer.Serialize(new{runner=".NET 10 reflection invoking NUnit assertions; System.Text.Json preflight adapter; not Unity EditMode",runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,utc=DateTime.UtcNow,sourceHashes,passed,failed,total=passed+failed,results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"TOTAL {passed+failed} PASSED {passed} FAILED {failed}");return failed==0?0:1;
    }
}

