using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
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
internal static class Runner
{
    public static int Main()
    {
        var results = new List<object>(); int passed = 0, failed = 0;
        foreach (var type in Assembly.GetExecutingAssembly().GetTypes().Where(t => t.Namespace == "DiceCraft.Presentation.Tests"))
        foreach (var method in type.GetMethods().Where(m => m.GetCustomAttribute<TestAttribute>() != null || m.GetCustomAttributes<TestCaseAttribute>().Any() || m.GetCustomAttributes<TestCaseSourceAttribute>().Any()))
        {
            var cases = new List<(string name, object[] args)>();
            foreach (var c in method.GetCustomAttributes<TestCaseAttribute>()) cases.Add((method.Name + "(" + string.Join(",", c.Arguments) + ")", c.Arguments));
            foreach (var source in method.GetCustomAttributes<TestCaseSourceAttribute>()) foreach (TestCaseData c in (IEnumerable)type.GetMethod(source.SourceName, BindingFlags.Public | BindingFlags.Static).Invoke(null, null)) cases.Add((c.TestName ?? method.Name, c.Arguments));
            if (cases.Count == 0) cases.Add((method.Name, Array.Empty<object>()));
            foreach (var c in cases)
            {
                try { object instance = Activator.CreateInstance(type); foreach (var setup in type.GetMethods().Where(m => m.GetCustomAttribute<SetUpAttribute>() != null)) setup.Invoke(instance, null); method.Invoke(instance, c.args); passed++; results.Add(new { name = c.name, result = "Passed" }); }
                catch (Exception ex) { var e = ex is TargetInvocationException && ex.InnerException != null ? ex.InnerException : ex; failed++; results.Add(new { name = c.name, result = "Failed", error = e.ToString() }); Console.WriteLine("FAIL " + c.name + ": " + e.Message); }
            }
        }
        string output = "DicePrototype/Assets/Game/Runtime/Presentation/Validation~/managed-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + ".json";
        File.WriteAllText(output, JsonSerializer.Serialize(new { runner = ".NET 10/NUnit reflection preflight, not Unity", utc = DateTime.UtcNow, passed, failed, results }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"Total {passed + failed}, passed {passed}, failed {failed}; {output}"); return failed == 0 ? 0 : 1;
    }
}
