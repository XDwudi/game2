using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using System.Text.Json;
using DiceCraft.Tests;
using NUnit.Framework;

public static class Program
{
    public static int Main(string[] args)
    {
        var results = new List<object>(); int passed=0,failed=0;
        foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(x=>!x.IsAbstract&&typeof(SessionTestSupport).IsAssignableFrom(x)).OrderBy(x=>x.Name))
        foreach(var method in type.GetMethods().Where(x=>x.GetCustomAttribute<TestAttribute>()!=null||x.GetCustomAttributes<TestCaseAttribute>().Any()).OrderBy(x=>x.Name))
        {
            var cases=method.GetCustomAttributes<TestCaseAttribute>().ToArray();
            var arguments=cases.Length==0?new[]{Array.Empty<object>()}:cases.Select(x=>x.Arguments).ToArray();
            foreach(var values in arguments)
            {
                string name=type.Name+"."+method.Name+(values.Length>0?"("+string.Join(",",values)+")":"");
                try {
                    var instance=(SessionTestSupport)Activator.CreateInstance(type);instance.ReadConfig();method.Invoke(instance,values);
                    passed++;results.Add(new{name,result="Passed"});Console.WriteLine("PASS "+name);
                }catch(Exception exception){
                    var e=exception is TargetInvocationException&&exception.InnerException!=null?exception.InnerException:exception;
                    failed++;results.Add(new{name,result="Failed",error=e.ToString()});Console.WriteLine("FAIL "+name+"\n"+e);
                }
            }
        }
        string output=Path.Combine("DicePrototype/Assets/Game/Core/Session/Validation~","report.json");
        File.WriteAllText(output,JsonSerializer.Serialize(new{runner=".NET 10 reflection invoking NUnit assertion tests; not Unity EditMode",utc=DateTime.UtcNow,passed,failed,total=passed+failed,results},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"TOTAL {passed+failed} PASSED {passed} FAILED {failed}");return failed==0?0:1;
    }
}
