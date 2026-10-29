using System.Security.Cryptography;
using System.Text.Json;
using DiceCraft.Core;

static class Program
{
    static readonly JsonSerializerOptions J = new() { IncludeFields = true, WriteIndented = true };
    static string Enc<T>(T x) => JsonSerializer.Serialize(x,J);
    static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
    static SubmissionSnapshot Parse(JsonElement e,GameConfig cfg)
    {
        int n=0;
        return new SubmissionSnapshot {
            runId="audit",stageId="STAGE_01",handId=e.GetProperty("id").GetString(),configVersion=cfg.configVersion,
            capacity=e.GetProperty("capacity").GetInt32(),rollsRemaining=e.GetProperty("rollsRemaining").GetInt32(),redWave=e.GetProperty("redWave").GetBoolean(),
            equippedControllerIds=e.GetProperty("controllerIds").EnumerateArray().Select(x=>x.GetString()).ToArray(),
            handLevels=e.GetProperty("levels").EnumerateObject().Select(x=>new HandLevel {handId=x.Name,level=x.Value.GetInt32()}).ToArray(),
            dice=e.GetProperty("dice").EnumerateArray().Select(d=>new SubmissionDie {
                dieId="D"+(++n),faceId="F01",currentPip=d.GetProperty("p").GetInt32(),currentColor=d.GetProperty("c").GetString(),
                permanentSixFaceSum=d.TryGetProperty("sum6",out var sum)?sum.GetInt32():21,
                isGlass=d.TryGetProperty("glass",out var glass)&&glass.GetBoolean(),isGold=d.TryGetProperty("gold",out var gold)&&gold.GetBoolean()
            }).ToArray()
        };
    }
    static void VerifyTrace(SubmissionSnapshot s,ScoreBreakdown b,GameConfig cfg)
    {
        string sbefore=Enc(s),bbefore=Enc(b); var t=ScoreExplanation.Build(s,b,cfg);
        Check(Enc(s)==sbefore&&Enc(b)==bbefore,"Trace mutated frozen inputs");
        for(int i=0;i<t.dice.Length;i++) {
            decimal v=b.effectivePips[i];
            foreach(var step in t.dice[i].steps) { Check(step.before==v,"Disconnected die step"); v=step.operation=="+"?v+step.operand:v*step.operand;Check(v==step.after,"Wrong die step arithmetic"); }
            Check(v==b.pipContributions[i],"Die trace final mismatch");
        }
        decimal g=b.pipSum;int floors=0;
        foreach(var step in t.global) {
            Check(step.before==g,"Disconnected global step");
            if(step.operation=="floor") {g=decimal.Floor(g);floors++;} else g=step.operation=="+"?g+step.operand:g*step.operand;
            Check(g==step.after,"Wrong global arithmetic");
        }
        Check(g==b.score&&floors==1,"Global result/single floor mismatch");
        Check(t.global[^2].kind=="floor"&&t.global[^1].kind=="final_add","Final ordering");
        Check(t.global.Where(x=>x.kind=="controller").Select(x=>x.sourceId).SequenceEqual(b.triggeredMultiplierIds),"Controller sequence");
        string expected=Enc(t);t.global[0].after=-9;t.dice[0].contribution=-7;
        Check(Enc(ScoreExplanation.Build(s,b,cfg))==expected,"Returned trace aliases future trace");
    }
    static int Main(string[] args)
    {
        string root=Path.GetFullPath(args[0]);
        var cfg=JsonSerializer.Deserialize<GameConfig>(File.ReadAllText(Path.Combine(root,"DicePrototype/Assets/Game/Data/P0Config.json")),J);cfg.Validate();
        int goldens=0,directed=0;bool fractional=false,multi=false,flat=false;
        foreach(var e in JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"DicePrototype/Assets/Game/Data/GoldenScores.json"))).RootElement.GetProperty("cases").EnumerateArray()) {
            var s=Parse(e,cfg);var b=ScoreCalculator.Evaluate(s,cfg);var actual=JsonSerializer.SerializeToElement(b,J);
            foreach(var expected in e.GetProperty("expected").EnumerateObject())Check(JsonSerializer.Serialize(actual.GetProperty(expected.Name))==JsonSerializer.Serialize(expected.Value),e.GetProperty("id")+" "+expected.Name);
            VerifyTrace(s,b,cfg);var t=ScoreExplanation.Build(s,b,cfg);
            fractional|=t.global.Any(x=>x.after!=decimal.Floor(x.after));multi|=t.global.Count(x=>x.kind=="controller")>=2;flat|=b.finalFlatAdd==6;goldens++;
        }
        // Independent oracle encodes original P0 additions, not implementation calls.
        string[] ids={"C10","C13","C17","C19"};
        for(int mask=0;mask<16;mask++)for(int p=1;p<=6;p++)foreach(int sum in new[]{6,13,14,36})for(int layers=0;layers<4;layers++)for(int c12=0;c12<2;c12++) {
            var controllers=ids.Where((id,i)=>(mask&(1<<i))!=0).ToList();if(c12==1)controllers.Add("C12");
            var s=new SubmissionSnapshot {runId="audit",stageId="STAGE_01",handId="layers",configVersion=cfg.configVersion,capacity=5,rollsRemaining=2,equippedControllerIds=controllers.ToArray(),handLevels=Array.Empty<HandLevel>(),dice=new[]{new SubmissionDie{dieId="D1",faceId="F01",currentPip=p,currentColor="blue",permanentSixFaceSum=sum,isGlass=(layers&1)!=0,isGold=(layers&2)!=0}}};
            int effective=c12==1&&p%2==1?p+1:p;
            int wanted=effective;var sources=new List<string>();
            if((mask&1)!=0&&effective==1){wanted+=4;sources.Add("C10");}
            if((mask&2)!=0&&effective%2==1){wanted+=2;sources.Add("C13");}
            if((mask&4)!=0&&effective<=3){wanted+=3;sources.Add("C17");}
            if((mask&8)!=0&&sum<=13){wanted+=5;sources.Add("C19");}
            if((layers&1)!=0){wanted*=2;sources.Add("glass");}if((layers&2)!=0){wanted*=2;sources.Add("gold");}
            var b=ScoreCalculator.Evaluate(s,cfg);Check(b.pipContributions[0]==wanted,"Independent contribution oracle");
            var t=ScoreExplanation.Build(s,b,cfg);Check(t.dice[0].steps.Select(x=>x.sourceId??x.kind).SequenceEqual(sources),"Contribution step source/order");VerifyTrace(s,b,cfg);directed++;
        }
        Check(goldens==52&&fractional&&multi&&flat,"Golden trace coverage missing");
        var manifest=Directory.GetFiles(Path.Combine(root,"DicePrototype/Assets/Game/Core/Rules"),"*.cs").Select(p=>new{file=Path.GetFileName(p),sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant()}).ToArray();
        File.WriteAllText(Path.Combine(root,"evidence/review/score-trace-independent-results.json"),Enc(new{goldens,fieldsPerGolden=18,directed,passed=true,unityExecuted=false,fractional,multipleGlobalMultipliers=multi,finalSix=flat,manifest}));
        Console.WriteLine($"PASS {goldens} original goldens x18 fields + trace; {directed} independent contribution/layer cases; fractional/multiple-global/C20/snapshot-purity verified. Unity not executed.");
        return 0;
    }
}
