using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Nodes;
using DiceCraft.Core;

// Standalone managed adapter: explicitly not Unity JsonUtility or a player run.
namespace UnityEngine
{
    public static class JsonUtility
    {
        public static T FromJson<T>(string value) => JsonSerializer.Deserialize<T>(value, ReviewProgram.Json);
        public static string ToJson(object value) => JsonSerializer.Serialize(value, value.GetType(), ReviewProgram.Json);
        public static string ToJson(object value, bool prettyPrint) => ToJson(value);
    }
}

internal static class ReviewProgram
{
    internal static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    private static readonly List<object> Results = new List<object>();
    private static int passed, failed;

    private static void Test(string name, Action action)
    {
        try { action(); ++passed; Results.Add(new { name, result = "Passed" }); }
        catch (Exception e) { ++failed; Results.Add(new { name, result = "Failed", error = e.ToString() }); Console.WriteLine("FAIL " + name + "\n" + e); }
    }

    private static void Assert(bool yes, string message) { if (!yes) throw new Exception(message); }
    private static void Reject(Action action,string message) { bool rejected=false;try { action(); } catch(ArgumentException) { rejected=true; } catch(InvalidOperationException) { rejected=true; } Assert(rejected,message); }
    private static JsonElement Property(JsonElement value, string key) => value.GetProperty(key);
    private static bool Has(JsonElement value, string key) => value.ValueKind == JsonValueKind.Object && value.TryGetProperty(key, out _);
    private static int Int(JsonElement value, string key, int fallback = 0) => Has(value,key) ? value.GetProperty(key).GetInt32() : fallback;
    private static string Str(JsonElement value, string key, string fallback = null) => Has(value,key) ? value.GetProperty(key).GetString() : fallback;
    private static bool Bool(JsonElement value, string key, bool fallback = false) => Has(value,key) ? value.GetProperty(key).GetBoolean() : fallback;
    private static HandLevel[] Levels(JsonElement input, string key) => Has(input,key)
        ? input.GetProperty(key).EnumerateObject().Select(p => new HandLevel { handId=p.Name, level=p.Value.GetInt32() }).ToArray()
        : Array.Empty<HandLevel>();

    private static SubmissionSnapshot Snapshot(JsonElement input)
    {
        int serial=0;
        var cards=input.GetProperty("controllers").EnumerateArray().Select((c,i)=> {
            if (c.ValueKind==JsonValueKind.String) return new ControllerInstance { instanceId="ctl_"+(i+1).ToString("00"),definitionId=c.GetString(),acquiredSerial=i+1 };
            var result=new ControllerInstance { instanceId=Str(c,"instanceId","ctl_"+(i+1).ToString("00")),definitionId=Str(c,"definitionId"),
                enchantmentId=Str(c,"enchantmentId","none"),alive=Bool(c,"alive",true),acquiredSerial=i+1 };
            if(Has(c,"state")) {
                var state=c.GetProperty("state");
                result.state=new ControllerDynamicState {activeColor=Str(state,"activeColor"),turnoverUnits2=Int(state,"turnoverUnits2",3),
                    fundAccrual=Has(state,"fundAccrual")?state.GetProperty("fundAccrual").GetInt64():0};
            }
            return result;
        }).ToArray();
        var bindings=Has(input,"bindings")&&input.GetProperty("bindings").ValueKind!=JsonValueKind.Null
            ? input.GetProperty("bindings").EnumerateObject().Select(p=>new MimicBinding {sourceInstanceId=p.Name,targetInstanceId=p.Value.GetString()}).ToArray() : null;
        return new SubmissionSnapshot {
            dice=input.GetProperty("dice").EnumerateArray().Select(d=>new SubmissionDie {dieId="RD"+(++serial),faceId="F01",
                currentPip=Int(d,"p"),currentColor=Str(d,"c"),permanentSixFaceSum=Int(d,"sum6",21),isGlass=Bool(d,"glass"),isGold=Bool(d,"gold")}).ToArray(),
            equippedControllers=cards,capacity=Int(input,"capacity",5),rollsRemaining=Int(input,"rolls_remaining"),
            submissionOrdinalBefore=Int(input,"submission_ordinal"),redWave=Bool(input,"red_wave"),debug=Bool(input,"debug"),
            wallet=Has(input,"wallet")?input.GetProperty("wallet").GetInt64():0,previousPrimaryHandId=Str(input,"previous_primary"),
            handLevels=Levels(input,"levels"),colorLevels=Levels(input,"color_levels"),mimicBindings=bindings,
            resolvedRandomChoices=Has(input,"random_units2") ? input.GetProperty("random_units2").EnumerateObject()
                .Select(p=>new RandomMultiplierChoice {ownerInstanceId=p.Name,units2=p.Value.GetInt32()}).ToArray():Array.Empty<RandomMultiplierChoice>()
        };
    }

    private static void Same(JsonElement expected, JsonElement actual, string path)
    {
        Assert(actual.ValueKind==expected.ValueKind,path+" kind "+actual.ValueKind+" != "+expected.ValueKind);
        if(expected.ValueKind==JsonValueKind.Object)
            foreach(var field in expected.EnumerateObject()) {
                Assert(actual.TryGetProperty(field.Name,out var got),path+" missing "+field.Name);
                Same(field.Value,got,path+"."+field.Name);
            }
        else if(expected.ValueKind==JsonValueKind.Array) {
            Assert(actual.GetArrayLength()==expected.GetArrayLength(),path+" length");
            for(int i=0;i<expected.GetArrayLength();i++)Same(expected[i],actual[i],path+"["+i+"]");
        }
        else Assert(actual.ToString()==expected.ToString(),path+": "+actual+" != "+expected);
    }

    private static void CheckScore(JsonElement item, GameConfig cfg, string prefix)
    {
        string label=prefix+item.GetProperty("id").GetString();
        Test(label,()=> {
            var snapshot=Snapshot(item.GetProperty("input"));
            string before=JsonSerializer.Serialize(snapshot,Json);
            var actual=ScoreCalculator.Evaluate(snapshot,cfg);
            Same(item.GetProperty("expected"),JsonSerializer.SerializeToElement(actual,Json),label);
            Assert(JsonSerializer.Serialize(snapshot,Json)==before,"Score evaluation mutated its input.");
            // Explanation has its own exact representation; no float comparison.
            var trace=ScoreExplanation.Build(snapshot,actual,cfg);
            Assert(trace.global.Count(s=>s.operation=="floor")==1,"Explanation must floor only once.");
            Assert(trace.global.Last().after.ToString()==actual.score,"Explanation final value differs from score.");
        });
    }

    private static uint IndependentIndex(ref uint state,int count)
    {
        if(state==0)state=0x9e3779b9;
        uint limit=(uint.MaxValue/(uint)count)*(uint)count;
        uint sample;
        do {
            unchecked { state^=state<<13;state^=state>>17;state^=state<<5; }
            sample=state-1;
        } while(sample>=limit);
        return sample%(uint)count;
    }

    private static void BoundaryChecks(GameConfig cfg,string configJson)
    {
        Test("CONFIG-NUMERIC-KEYS-EXACT",()=> {
            var data=JsonNode.Parse(configJson);
            var c11=data["controllers"].AsArray().Single(x=>x["id"].GetValue<string>()=="C11");
            c11["parameters"]["requiredCounts"]["2"]=3;
            c11["parameters"]["requiredCounts"]["3"]=4;
            string modified=data.ToJsonString();
            GameConfig.ValidateJson(modified);
            var loaded=JsonSerializer.Deserialize<GameConfig>(GameConfig.PrepareJsonForUnity(modified),Json);
            loaded.Validate();
            Assert(loaded.Controller("C11").parameters.requiredCounts.pip2==3&&loaded.Controller("C11").parameters.requiredCounts.pip3==4,"Numeric JSON values silently defaulted.");
            c11["parameters"]["requiredCounts"].AsObject().Remove("2");
            Reject(()=>GameConfig.PrepareJsonForUnity(data.ToJsonString()),"Missing numeric key accepted.");
            c11["parameters"]["requiredCounts"]["pip2"]=3;
            Reject(()=>GameConfig.PrepareJsonForUnity(data.ToJsonString()),"Private transformed DTO key accepted as original config.");
        });
        Test("SCORE-STRING-INVALID-FORMATS",()=> {
            foreach(string value in new[]{null,"","-1","1.0","1e30","+1"," 1","1 ","NaN","Infinity","１２"})
                Reject(()=>ExactScoreMath.Parse(value),"Illegal decimal score accepted: "+value);
            string giant="1"+new string('0',200);
            Assert(ExactScoreMath.SaveDecimal(ExactScoreMath.Parse(giant))==giant,"Large decimal altered.");
        });
        Test("MIMIC-FROZEN-LOST-TARGET-AND-CLONED-STATE",()=> {
            var mimic=new ControllerInstance {instanceId="M",definitionId="C41",enchantmentId="base10"};
            var target=new ControllerInstance {instanceId="A",definitionId="C45",state=new ControllerDynamicState {activeColor="red"}};
            var next=new ControllerInstance {instanceId="B",definitionId="C46"};
            var cards=new[]{mimic,target,next};var bindings=ControllerRules.FreezeBindings(cards);
            var source=ControllerRules.ResolveSources(cards,bindings).Single(x=>x.emitterId=="M");
            target.state.activeColor="blue";
            Assert(source.state.activeColor=="red","Resolved source retains mutable original state.");
            Assert(source.enchantmentId=="base10"&&source.stateOwnerId=="A","Mimic identity/enchantment/state owner wrong.");
            target.alive=false;
            var invalidated=ControllerRules.ResolveSources(cards,bindings).Single(x=>x.emitterId=="M");
            Assert(invalidated.abilityId==null&&invalidated.stateOwnerId==null,"Lost target automatically retargeted.");
            Reject(()=>ControllerRules.ResolveSources(new[]{mimic},new[]{new MimicBinding {sourceInstanceId="M",targetInstanceId="M"}}),"Mimic cycle accepted.");
        });
        foreach(string id in cfg.enchantRules.nonSubmitControllerIds)
        foreach(string enchant in new[]{"base10","mult2"}) {
            string cid=id,e=enchant;
            Test("NORMAL-DEBUG-ENCHANT-"+cid+"-"+e,()=> {
                var controller=new ControllerInstance {instanceId="D",definitionId=cid,enchantmentId=e};
                Reject(()=>ControllerRules.ValidateEquipment(new[]{controller},cfg),"Normal illegal enchant accepted.");
                ControllerRules.ValidateEquipment(new[]{controller},cfg,true);
                var s=new SubmissionSnapshot {debug=true,capacity=5,equippedControllers=new[]{controller},
                    dice=new[]{new SubmissionDie {dieId="X",faceId="F01",currentPip=6,currentColor="blue",permanentSixFaceSum=21}}};
                var with=ScoreCalculator.Evaluate(s,cfg);controller.enchantmentId="none";var without=ScoreCalculator.Evaluate(s,cfg);
                Assert(with.score==without.score&&with.enchantBaseCount==0&&with.enchantMultiplierCount==0,"Pure non-submit debug enchant scored.");
            });
        }
        Test("DYNAMIC-STATE-MINIMUMS",()=> {
            foreach(int illegal in new[]{-1,0,1,2}) {
                var c=new ControllerInstance {instanceId="X",definitionId="C51",state=new ControllerDynamicState {turnoverUnits2=illegal}};
                Reject(()=>ControllerRules.ValidateEquipment(new[]{c},cfg,true),"C51 n below 1.5 accepted.");
            }
            foreach(long illegal in new long[]{-3,1,2,4}) {
                var c=new ControllerInstance {instanceId="X",definitionId="C52",state=new ControllerDynamicState {fundAccrual=illegal}};
                Reject(()=>ControllerRules.ValidateEquipment(new[]{c},cfg,true),"Fund not nonnegative multiple of 3 accepted.");
            }
        });
        Test("PREVIEW-SHARED-OWNER-NO-CONSUMPTION",()=> {
            var s=new SubmissionSnapshot {capacity=5,equippedControllers=new[]{new ControllerInstance {instanceId="M",definitionId="C41"},new ControllerInstance {instanceId="R",definitionId="C53"}},
                dice=new[]{new SubmissionDie {dieId="X",faceId="F01",currentPip=5,currentColor="blue",permanentSixFaceSum=21}}};
            string before=JsonSerializer.Serialize(s,Json);
            var preview=ScoreCalculator.Preview(s,cfg);
            Assert(preview.randomOwnerIds.SequenceEqual(new[]{"R"})&&preview.isRandom,"Shared C53 owner counted more than once.");
            Assert(preview.minimum.score=="3"&&preview.maximum.score=="960","Shared C53 branch range did not square the same q.");
            Assert(before==JsonSerializer.Serialize(s,Json),"Preview mutated input.");
        });
        foreach(var definition in cfg.controllers) {
            string cid=definition.id;
            var defaultPool=cfg.enchantRules.nonSubmitControllerIds.Contains(cid)
                ? new[]{("none",100),("slotless",50),("submit1",25),("roll1",25)}
                : cfg.enchantments.Select(e=>(e.id,e.weight)).ToArray();
            foreach(string old in new string[]{null}.Concat(defaultPool.Select(p=>p.Item1))) {
                string prior=old;
                Test("ENCHANT-CDF-"+cid+"-"+(prior??"NEW"),()=> {
                    var pool=prior==null?defaultPool:defaultPool.Where(p=>p.Item1!="none"&&p.Item1!=prior).ToArray();
                    int total=pool.Sum(p=>p.Item2);var visited=new HashSet<uint>();
                    for(uint seed=1;visited.Count<total&&seed<100000;seed++) {
                        uint expectedState=seed;uint ticket=IndependentIndex(ref expectedState,total);
                        if(!visited.Add(ticket))continue;
                        int t=(int)ticket;string expected=null;
                        foreach(var entry in pool) {if(t<entry.Item2) {expected=entry.Item1;break;}t-=entry.Item2;}
                        var actualRng=new GameRng(seed);
                        string actual=ControllerRules.DrawEnchantment(cid,prior,actualRng,cfg);
                        Assert(actual==expected,"Wrong exact ticket at "+ticket);
                        Assert(actualRng.State==expectedState,"RNG advanced incorrectly at "+ticket);
                    }
                    Assert(visited.Count==total,"Ticket coverage incomplete.");
                });
            }
        }
    }

    internal static int Main(string[] args)
    {
        string configJson=File.ReadAllText("DicePrototype/Assets/Game/Data/P0Config.json");
        GameConfig.ValidateJson(configJson);
        var cfg=JsonSerializer.Deserialize<GameConfig>(GameConfig.PrepareJsonForUnity(configJson),Json);
        cfg.Validate();
        using var vectors=JsonDocument.Parse(File.ReadAllText("evidence/v06/review/review-vectors.json"));
        using var golden=JsonDocument.Parse(File.ReadAllText("evidence/v06/review/input-json-1.json"));
        foreach(var item in golden.RootElement.EnumerateArray())CheckScore(item,cfg,"GOLDEN-");
        foreach(var item in vectors.RootElement.GetProperty("scores").EnumerateArray())CheckScore(item,cfg,"DIFF-");
        foreach(var item in vectors.RootElement.GetProperty("probabilities").EnumerateArray()) {
            string id=item.GetProperty("id").GetString();
            Test(id,()=> {
                var faces=JsonSerializer.Deserialize<FaceState[]>(item.GetProperty("faces").GetRawText(),Json);
                string before=JsonSerializer.Serialize(faces,Json);
                var actual=ProbabilityCalculator.BuildUnits(faces,item.GetProperty("large").GetBoolean(),item.GetProperty("small").GetBoolean(),cfg,
                    item.GetProperty("high").GetInt32(),item.GetProperty("low").GetInt32());
                Assert(actual.SequenceEqual(item.GetProperty("expected").EnumerateArray().Select(v=>v.GetInt32())),"Independent probability vector differs.");
                Assert(actual.Sum()==1000000&&actual.Min()>=0,"Probability range or total invalid.");
                Assert(before==JsonSerializer.Serialize(faces,Json),"Probability evaluation mutated inputs.");
            });
        }
        BoundaryChecks(cfg,configJson);
        SessionChecks.Run(cfg,Test,Assert);
        PersistenceChecks.Run(cfg,Test,Assert);
        string output=args.Length>0?args[0]:"evidence/v06/review/managed-rules-latest.json";
        TraceChecks.Run(cfg,Test,Assert,Path.ChangeExtension(output,"trace-details.json"));
        var report=new {scope="Reviewer managed rules, sessions, persistence IO and normal/debug trajectory execution; System.Text.Json adapter, not Unity or player acceptance.",utc=DateTime.UtcNow,passed,failed,total=passed+failed,results=Results};
        File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions {WriteIndented=true}));
        Console.WriteLine($"TOTAL {passed+failed} PASSED {passed} FAILED {failed}");
        return failed==0?0:1;
    }
}
