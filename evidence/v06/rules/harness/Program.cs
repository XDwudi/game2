using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using DiceCraft.Core;
internal static class Program
{
    static JsonSerializerOptions opt=new JsonSerializerOptions{IncludeFields=true};
    static List<string> passed=new List<string>(),failures=new List<string>();
    static int Main()
    {
        try
        {
            string raw=File.ReadAllText("DicePrototype/Assets/Game/Data/P0Config.json");
            var cfg=JsonSerializer.Deserialize<GameConfig>(GameConfig.PrepareJsonForUnity(raw),opt);cfg.Validate();passed.Add("CONFIG-50-7-7-7-and-13-2");
            var golden=JsonDocument.Parse(File.ReadAllText("DicePrototype/Assets/Game/Data/GoldenScores.json")).RootElement;
            foreach(var row in golden.EnumerateArray())
            {
                string id=row.GetProperty("id").GetString();
                try
                {
                    var input=row.GetProperty("input");var snap=Snapshot(input,cfg);string before=JsonSerializer.Serialize(snap,opt);
                    var result=ScoreCalculator.Evaluate(snap,cfg);var actual=JsonSerializer.SerializeToElement(result,opt);
                    var expected=row.GetProperty("expected");if(expected.EnumerateObject().Count()!=21)throw new Exception("Expected 21 authoritative fields.");
                    foreach(var e in expected.EnumerateObject())Equal(e.Value,actual.GetProperty(e.Name),id+"."+e.Name);
                    if(before!=JsonSerializer.Serialize(snap,opt))throw new Exception("Input mutation.");
                    var trace=ScoreExplanation.Build(snap,result,cfg);if(trace.global.Last().after!=result.score||trace.global.Count(s=>s.operation=="floor")!=1)throw new Exception("Trace mismatch.");
                    foreach(var die in trace.dice)foreach(var s in die.steps)Link(s);
                    foreach(var s in trace.global)Link(s);
                    var preview=ScoreCalculator.Preview(snap,cfg);if(preview.minimum.score!=result.score||preview.maximum.score!=result.score||preview.isRandom)throw new Exception("Frozen random preview changed.");
                    passed.Add(id+"-all21-plan14-trace-purity");
                }catch(Exception e){failures.Add(id+": "+e);}
            }
        }catch(Exception e){failures.Add(e.ToString());}
        var files=Directory.GetFiles("DicePrototype/Assets/Game/Core/Rules","*.cs").Concat(new[]{"DicePrototype/Assets/Game/Data/P0Config.json","DicePrototype/Assets/Game/Data/GoldenScores.json"}).Select(p=>new{path=p,sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant()}).ToArray();
        File.WriteAllText("evidence/v06/rules/managed-golden-report.json",JsonSerializer.Serialize(new{utc=DateTime.UtcNow,runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,sourceHashes=files,passed,failures},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine("Passed "+passed.Count+", failures "+failures.Count);foreach(var f in failures)Console.WriteLine(f);return failures.Count==0?0:1;
    }
    static void Link(ScoreStep s)
    {
        var before=ExactFraction.Parse(s.before);var operand=ExactFraction.Parse(s.operand);string after=s.operation=="+"?(before+operand).ToString():s.operation=="×"?(before*operand).ToString():ExactScoreMath.SaveDecimal(before.Numerator/before.Denominator);if(after!=s.after)throw new Exception("Broken trace arithmetic: "+s.kind);
    }
    static void Equal(JsonElement a,JsonElement b,string path)
    {
        if(a.ValueKind!=b.ValueKind)throw new Exception(path+": kind "+a+" vs "+b);
        if(a.ValueKind==JsonValueKind.Object){foreach(var p in a.EnumerateObject())Equal(p.Value,b.GetProperty(p.Name),path+"."+p.Name);if(a.EnumerateObject().Count()!=b.EnumerateObject().Count())throw new Exception(path+" count");}
        else if(a.ValueKind==JsonValueKind.Array){var aa=a.EnumerateArray().ToArray();var bb=b.EnumerateArray().ToArray();if(aa.Length!=bb.Length)throw new Exception(path+" length");for(int i=0;i<aa.Length;i++)Equal(aa[i],bb[i],path+"["+i+"]");}
        else if(a.GetRawText()!=b.GetRawText())throw new Exception(path+": expected "+a+", got "+b);
    }
    public static SubmissionSnapshot Snapshot(JsonElement i,GameConfig cfg)
    {
        var controllers=i.GetProperty("controllers").EnumerateArray().Select((c,n)=>JsonSerializer.Deserialize<ControllerInstance>(c.GetRawText(),opt)).ToArray();
        foreach(var c in controllers)if(c.state.turnoverUnits2==0)c.state.turnoverUnits2=3;
        return new SubmissionSnapshot{configVersion=cfg.configVersion,capacity=i.GetProperty("capacity").GetInt32(),rollsRemaining=i.GetProperty("rolls_remaining").GetInt32(),redWave=i.GetProperty("red_wave").GetBoolean(),submissionOrdinalBefore=i.GetProperty("submission_ordinal").GetInt32(),wallet=i.GetProperty("wallet").GetInt64(),previousPrimaryHandId=i.GetProperty("previous_primary").GetString(),debug=i.GetProperty("debug").GetBoolean(),equippedControllers=controllers,
            handLevels=i.GetProperty("levels").EnumerateObject().Select(p=>new HandLevel{handId=p.Name,level=p.Value.GetInt32()}).ToArray(),colorLevels=i.GetProperty("color_levels").EnumerateObject().Select(p=>new HandLevel{handId=p.Name,level=p.Value.GetInt32()}).ToArray(),
            mimicBindings=i.GetProperty("bindings").EnumerateObject().Select(p=>new MimicBinding{sourceInstanceId=p.Name,targetInstanceId=p.Value.GetString()}).ToArray(),resolvedRandomChoices=i.GetProperty("random_units2").EnumerateObject().Select(p=>new RandomMultiplierChoice{ownerInstanceId=p.Name,units2=p.Value.GetInt32()}).ToArray(),
            dice=i.GetProperty("dice").EnumerateArray().Select((d,n)=>new SubmissionDie{dieId="D"+n,faceId="F01",currentPip=d.GetProperty("p").GetInt32(),currentColor=d.GetProperty("c").GetString(),permanentSixFaceSum=d.GetProperty("sum6").GetInt32(),isGlass=d.GetProperty("glass").GetBoolean(),isGold=d.GetProperty("gold").GetBoolean()}).ToArray()};
    }
}
