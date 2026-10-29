using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Reflection;
using DiceCraft.Core;
using DiceCraft;

internal static class ReviewHarness
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true };
    private static readonly List<string> Passed = new List<string>(), Failed = new List<string>();
    private static GameConfig cfg;
    private static long serial;
    private static string root, reportDir;
    private static void Check(bool yes, string message) { if (!yes) throw new Exception(message); }
    private static string Enc(object o) => JsonSerializer.Serialize(o, o.GetType(), Json);
    private static SessionCommand Command(GameSession s, CommandKind kind) => new SessionCommand { commandId = "review_" + (++serial), expectedActionSequence = s.State?.actionSequence ?? 0, kind = kind };
    private static CommandResult Do(GameSession s, CommandKind kind, Action<SessionCommand> set = null)
    { var c = Command(s, kind); set?.Invoke(c); var r = s.Execute(c); Check(r.accepted, kind + ": " + r.error); return r; }
    private static GameSession Fresh(ISaveStore save = null, uint seed = 4321)
    { var s = new GameSession(cfg, save ?? new MemorySaveStore()); Do(s, CommandKind.NewRun, c => c.seed = seed); return s; }
    private static void Test(string id, Action action)
    { try { action(); Passed.Add(id); Console.WriteLine("PASS " + id); } catch (Exception e) { Failed.Add(id + ": " + e); Console.WriteLine("FAIL " + id + ": " + e.Message); } }
    public static int Main(string[] args)
    {
        root = Path.GetFullPath(args.Length == 0 ? "." : args[0]); reportDir = Path.Combine(root, "evidence/review");
        string config = File.ReadAllText(Path.Combine(root,"DicePrototype/Assets/Game/Data/P0Config.json"));
        GameConfig.ValidateJson(config); cfg = JsonSerializer.Deserialize<GameConfig>(config, Json); cfg.Validate();
        Test("GOLD-52-all-fields", Golden);
        Test("CORE-invalid-commands-have-no-effects", RejectionImmutability);
        Test("CORE-receipt-replay-survives-restore", Replay);
        Test("CORE-save-retry-keeps-candidate-rng-and-events", SaveRetry);
        Test("CORE-preview-does-not-consume-rng-or-mutate", PreviewPurity);
        Test("SHOP-independent-python-exact-oracle", ShopOracle);
        Test("SHOP-qualification-and-prices-rechecked-no-restock", ShopChanges);
        Test("SESSION-author-NUnit-cases-independently-rerun", AuthorTests);
        Test("IO-recovered-backup-survives-next-commit", () => BackupPreservation(true));
        Test("IO-corrupt-active-not-promoted-without-load", () => BackupPreservation(false));
        Test("IO-checksum-mismatch-falls-back", () => InvalidEnvelope("checksum"));
        Test("IO-schema-incompatible-falls-back", () => InvalidEnvelope("schema"));
        Test("IO-semantic-corruption-falls-back", () => InvalidEnvelope("semantic"));
        Test("IO-both-invalid-return-notice-no-throw", () => InvalidEnvelope("both"));
        var hashes = Directory.GetFiles(Path.Combine(root,"DicePrototype/Assets/Game/Core"),"*.cs",SearchOption.AllDirectories)
            .Where(p=>!p.Contains("Validation~"))
            .Append(Path.Combine(root,"DicePrototype/Assets/Game/Runtime/AtomicSaveStore.cs"))
            .ToDictionary(p=>Path.GetRelativePath(root,p),p=>Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant());
        File.WriteAllText(Path.Combine(reportDir,"review-results.json"),JsonSerializer.Serialize(new { utc=DateTime.UtcNow, runtime=System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, limitation="Pure C# and real file IO; JsonUtility shim; no Unity Player verification", passed=Passed, failed=Failed, sourceHashes=hashes },new JsonSerializerOptions {WriteIndented=true}));
        Console.WriteLine($"REVIEW {Passed.Count} passed / {Failed.Count} failed"); return Failed.Count==0?0:1;
    }
    private static void Golden()
    {
        var cases=JsonDocument.Parse(File.ReadAllText(Path.Combine(root,"DicePrototype/Assets/Game/Data/GoldenScores.json"))).RootElement.GetProperty("cases");
        int count=0;
        foreach(var t in cases.EnumerateArray())
        {
            var snap=new SubmissionSnapshot { configVersion=cfg.configVersion, capacity=t.GetProperty("capacity").GetInt32(), rollsRemaining=t.GetProperty("rollsRemaining").GetInt32(), redWave=t.GetProperty("redWave").GetBoolean(), equippedControllerIds=t.GetProperty("controllerIds").EnumerateArray().Select(x=>x.GetString()).ToArray(), handLevels=t.GetProperty("levels").EnumerateObject().Select(x=>new HandLevel {handId=x.Name,level=x.Value.GetInt32()}).ToArray(), dice=t.GetProperty("dice").EnumerateArray().Select((x,i)=>new SubmissionDie {dieId="D"+i,faceId="F01",currentPip=x.GetProperty("p").GetInt32(),currentColor=x.GetProperty("c").GetString(),permanentSixFaceSum=x.TryGetProperty("sum6",out var sum)?sum.GetInt32():21,isGold=x.TryGetProperty("gold",out var gold)&&gold.GetBoolean(),isGlass=x.TryGetProperty("glass",out var glass)&&glass.GetBoolean()}).ToArray() };
            string before=Enc(snap);var actual=JsonSerializer.SerializeToElement(ScoreCalculator.Evaluate(snap,cfg),Json);
            foreach(var exp in t.GetProperty("expected").EnumerateObject()) Check(Enc(actual.GetProperty(exp.Name))==Enc(exp.Value), t.GetProperty("id")+" "+exp.Name);
            Check(Enc(snap)==before,"score mutated input");count++;
        }
        Check(count==52,"not 52 cases");
    }
    private static void RejectionImmutability()
    {
        var s=Fresh();Do(s,CommandKind.StartStage);var before=Enc(s.State);
        foreach(var kind in new[]{CommandKind.Submit,CommandKind.Buy,CommandKind.Sell,CommandKind.Reorder,CommandKind.ConfirmLocks,CommandKind.UseItem,CommandKind.StartStage,CommandKind.Continue})
        { var r=s.Execute(Command(s,kind));Check(!r.accepted&&!r.pendingSave,"invalid accepted "+kind);Check(Enc(s.State)==before,"invalid changed state "+kind); }
        Do(s,CommandKind.Roll);Check(s.TogglePending("D002"),"select");before=Enc(s.State);
        foreach(var kind in new[]{CommandKind.Roll,CommandKind.Submit,CommandKind.UseItem,CommandKind.Reorder}) {Check(!s.Execute(Command(s,kind)).accepted,"pending action accepted");Check(Enc(s.State)==before,"pending action changed state");}
        Do(s,CommandKind.ConfirmLocks);before=Enc(s.State);Check(!s.TogglePending("D002"),"lock removable");Check(Enc(s.State)==before,"toggle altered locked state");
    }
    private static void Replay()
    {
        var s=Fresh();Do(s,CommandKind.StartStage);var roll=Command(s,CommandKind.Roll);var first=s.Execute(roll);Check(first.accepted,"first roll rejected");
        string before=Enc(s.State);int eventCount=0;s.Committed+=r=>eventCount++;
        for(int i=0;i<4;i++){var r=s.Execute(roll);Check(r.accepted&&r.duplicate,"duplicate not replayed");Check(Enc(r.receipt)==Enc(first.receipt),"receipt differs");}
        Check(Enc(s.State)==before&&eventCount==0,"duplicate mutated state/emitted");s=new GameSession(cfg,new MemorySaveStore(),s.State);Check(s.Execute(roll).duplicate,"restore lost receipt");
        var stale=Command(s,CommandKind.Roll);stale.expectedActionSequence--;Check(!s.Execute(stale).accepted,"stale accepted");Check(Enc(s.State)==before,"stale changed state");
    }
    private sealed class FailingStore:ISaveStore {public bool fail;public string candidate;public void Save(RunState s){string value=Enc(s);if(fail){if(candidate!=null)Check(value==candidate,"candidate changed between attempts");candidate=value;throw new IOException("injected");}if(candidate!=null)Check(value==candidate,"successful retry changed candidate");}}
    private static void SaveRetry()
    {
        var store=new FailingStore();var s=Fresh(store);Do(s,CommandKind.StartStage);var before=Enc(s.State);var cmd=Command(s,CommandKind.Roll);int events=0;s.Committed+=r=>events++;store.fail=true;
        var r=s.Execute(cmd);Check(!r.accepted&&r.pendingSave&&s.HasPendingSave,"not pending save");Check(Enc(s.State)==before&&events==0,"failed save published");
        Check(s.Execute(Command(s,CommandKind.Roll)).pendingSave,"new commands not blocked");Check(s.RetrySave().pendingSave,"failed retry missing");store.fail=false;r=s.RetrySave();Check(r.accepted&&events==1&&!s.HasPendingSave,"retry not committed once");
        Check(s.Execute(cmd).duplicate&&events==1,"retry did not retain receipt");Check(s.State.hand.rollsUsed==1,"retry rolled twice");
    }
    private static void PreviewPurity()
    {
        var s=Fresh();Do(s,CommandKind.StartStage);Do(s,CommandKind.Roll);s.TogglePending("D001");s.TogglePending("D003");Do(s,CommandKind.ConfirmLocks);string before=Enc(s.State);
        for(int i=0;i<100;i++){s.PreviewScore();s.ProbabilityUnits("D001");s.LegalStudyHands();}
        Check(Enc(s.State)==before,"preview mutated state");
    }
    private static void BackupPreservation(bool loadFirst)
    {
        string folder=Path.Combine(reportDir,"io-"+(loadFirst?"recovered":"unloaded")+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string path=Path.Combine(folder,"run.json");
        var store=new AtomicSaveStore(path,cfg);var s=Fresh(store);Do(s,CommandKind.StartStage);var next=s.State;
        Check(File.Exists(path+".backup"),"setup missing backup");string validBackup=File.ReadAllText(path+".backup");File.WriteAllText(path,"broken active");
        if(loadFirst){var recovered=store.Load();Check(recovered!=null,"backup recovery setup failed");next=recovered;}
        store.Save(next);
        Check(File.ReadAllText(path+".backup")==validBackup,"valid backup replaced with corrupt old active");
        File.WriteAllText(path,"broken active again");Check(store.Load()!=null,"both save and backup unusable after second corruption");
    }
    private static GameSession ShopFixture()
    {
        var s=Fresh();Do(s,CommandKind.StartStage);Do(s,CommandKind.Roll);var st=s.State;st.isDebug=true;
        foreach(var r in st.hand.currentResults){r.currentFaceId="F04";r.currentPip=4;r.currentColor="red";r.isLocked=true;st.hand.orderedLockedIds.Add(r.dieId);}
        s=new GameSession(cfg,new MemorySaveStore(),st);Do(s,CommandKind.Submit);Do(s,CommandKind.Continue);return s;
    }
    private static void ShopOracle()
    {
        var s=ShopFixture();var oracle=JsonDocument.Parse(File.ReadAllText(Path.Combine(reportDir,"shop-oracle-seed4321.json"))).RootElement;var st=s.State;
        Check(st.shopRngState==oracle.GetProperty("expectedFinalState").GetUInt32(),"shop RNG draw order differs from independent oracle");int i=0;
        foreach(var expected in oracle.GetProperty("offers").EnumerateArray())
        {
            var actual=st.shop.offers[i++];Check(actual.category==expected.GetProperty("category").GetString()&&actual.contentId==expected.GetProperty("id").GetString(),"offer order/ID differs");
            if(actual.category=="new_die")
            {
                Check(actual.basePrice==expected.GetProperty("price").GetInt32(),"new die quote differs");int j=0;
                foreach(var face in expected.GetProperty("faces").EnumerateArray()){var f=actual.die.faces[j++];Check(f.permanentPip==face.GetProperty("p").GetInt32()&&f.baseColor==face.GetProperty("c").GetString(),"new die faces/order differs");}
            }
        }
        Check(i==st.shop.offers.Count,"offer count differs");string before=Enc(st);s=new GameSession(cfg,new MemorySaveStore(),JsonSerializer.Deserialize<RunState>(Enc(st),Json));
        for(int n=0;n<40;n++){foreach(var offer in s.State.shop.offers){s.GetOfferPrice(offer);s.PurchaseError(offer.offerId);}s.LegalStudyHands();}
        Check(Enc(s.State)==before,"shop preview/reload changes supply");
    }
    private static void InvalidEnvelope(string mode)
    {
        string folder=Path.Combine(reportDir,"io-"+mode+"-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);string path=Path.Combine(folder,"run.json");
        var store=new AtomicSaveStore(path,cfg);var s=Fresh(store);long priorSequence=s.State.actionSequence;Do(s,CommandKind.StartStage);
        var envelope=JsonNode.Parse(File.ReadAllText(path));
        if(mode=="checksum") envelope["sha256"]="wrong-hash";
        if(mode=="schema") envelope["schema"]=1;
        if(mode=="semantic")
        {
            var payload=JsonNode.Parse(envelope["payload"].GetValue<string>());payload["dice"][1]["dieId"]="D001";
            string text=payload.ToJsonString();envelope["payload"]=text;
            envelope["sha256"]=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
        }
        if(mode=="both"){File.WriteAllText(path,"broken");File.WriteAllText(path+".backup","also broken");}
        else File.WriteAllText(path,envelope.ToJsonString());
        string unchanged=File.ReadAllText(path);var loaded=store.Load();Check(File.ReadAllText(path)==unchanged,"Load modified rejected original");
        if(mode=="both")Check(loaded==null&&!string.IsNullOrWhiteSpace(store.LastLoadNotice),"invalid pair did not report recoverable failure");
        else Check(loaded!=null&&loaded.actionSequence==priorSequence&&!string.IsNullOrWhiteSpace(store.LastLoadNotice),"did not recover previous valid backup");
    }
    private static void ShopChanges()
    {
        var s=ShopFixture();var st=s.State;st.gold=200;st.itemDiscount=2;st.shop.offers.Clear();
        string Add(string cat,string id){var oid="OFFER_"+(st.nextOfferSerial++).ToString("D5");st.shop.offers.Add(new ShopOffer{offerId=oid,category=cat,contentId=id});return oid;}
        var discount=Add("service","O02");var simplify=Add("controller","C09");var straight=Add("hand_upgrade","straight");var pair=Add("hand_upgrade","pair");var consumable=Add("consumable","I04");
        s=new GameSession(cfg,new MemorySaveStore(),st);uint rng=s.State.shopRngState;int offers=s.State.shop.offers.Count;
        Do(s,CommandKind.Buy,c=>c.offerId=discount);Check(s.State.gold==195&&s.State.itemDiscount==3,"O02 used new discount for its own payment");
        Check(s.GetOfferPrice(s.State.shop.offers.First(x=>x.offerId==consumable))==2,"discount minimum");
        Do(s,CommandKind.Buy,c=>c.offerId=simplify);Check(s.State.gold==181&&s.EffectiveCapacity==4,"controller incorrectly discounted/capacity");
        var before=Enc(s.State);var bad=Command(s,CommandKind.Buy);bad.offerId=straight;Check(!s.Execute(bad).accepted&&Enc(s.State)==before,"old hand offer eligibility not rechecked");
        Do(s,CommandKind.Study,c=>c.handId="pair");Check(s.State.gold==173,"self study price");Check(s.GetOfferPrice(s.State.shop.offers.First(x=>x.offerId==pair))==8,"displayed hand price not recomputed after study");
        Do(s,CommandKind.Buy,c=>c.offerId=pair);Check(s.State.gold==165&&s.State.handLevels.First(x=>x.handId=="pair").level==2,"random hand price stale");
        var controller=s.State.controllers.Single().instanceId;Do(s,CommandKind.Sell,c=>c.itemInstanceId=controller);Check(s.State.gold==166&&s.EffectiveCapacity==5,"sell not one gold/capacity restored");
        Do(s,CommandKind.Buy,c=>c.offerId=straight);Check(s.State.gold==161,"former invalid offer not purchasable after legal again");
        Check(s.State.shop.offers.Count==offers&&s.State.shopRngState==rng,"trades restocked/rerolled");
    }
    private static void AuthorTests()
    {
        var results=new List<object>();int count=0;var errors=new List<string>();
        foreach(var type in Assembly.GetExecutingAssembly().GetTypes().Where(t=>!t.IsAbstract&&typeof(DiceCraft.Tests.SessionTestSupport).IsAssignableFrom(t)).OrderBy(t=>t.Name))
        foreach(var method in type.GetMethods().Where(m=>m.GetCustomAttribute<NUnit.Framework.TestAttribute>()!=null||m.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().Any()).OrderBy(m=>m.Name))
        {
            var cs=method.GetCustomAttributes<NUnit.Framework.TestCaseAttribute>().ToArray();var values=cs.Length==0?new[]{Array.Empty<object>()}:cs.Select(c=>c.Arguments).ToArray();
            foreach(var args in values){string name=type.Name+"."+method.Name+"("+string.Join(",",args)+")";count++;
                try{var test=(DiceCraft.Tests.SessionTestSupport)Activator.CreateInstance(type);test.ReadConfig();method.Invoke(test,args);results.Add(new{name,result="Passed"});}
                catch(Exception e){var err=e is TargetInvocationException&&e.InnerException!=null?e.InnerException:e;errors.Add(name+": "+err.Message);results.Add(new{name,result="Failed",error=err.ToString()});}
            }
        }
        File.WriteAllText(Path.Combine(reportDir,"session-independent-rerun.json"),JsonSerializer.Serialize(new{utc=DateTime.UtcNow,total=count,failures=errors.Count,results},new JsonSerializerOptions{WriteIndented=true}));
        Check(count>=102,"zero/incomplete author tests");Check(errors.Count==0,string.Join("\n",errors));Console.WriteLine("Independent Session NUnit rerun count="+count);
    }
}
