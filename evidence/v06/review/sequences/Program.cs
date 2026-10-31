using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using DiceCraft.Core;

internal static class SequenceReview
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions {IncludeFields=true,WriteIndented=true};
    private static GameConfig cfg;
    private static int serial;
    private static readonly List<object> results = new List<object>();
    private static List<object> steps;
    private static int failures;
    private static void Check(bool value,string message) { if(!value)throw new InvalidOperationException(message); }
    private static SessionCommand Command(GameSession s,CommandKind kind) => new SessionCommand {kind=kind,commandId="SEQUENCE-"+(++serial),expectedActionSequence=s.ActionSequence};
    private static CommandResult Send(GameSession s,CommandKind kind,Action<SessionCommand> configure=null)
    {
        var c=Command(s,kind); configure?.Invoke(c); var r=s.Execute(c);
        Check(r.accepted,kind+": "+r.error); Record(s,kind.ToString()); return r;
    }
    private static void Record(GameSession s,string action)
    {
        var x=s.State;
        steps.Add(new {action,sequence=x.actionSequence,phase=x.phase,stage=x.stage.stageIndex,primary=x.previousPrimaryHandId,
            score=x.stage.score,target=x.stage.target,funds=x.controllers.Select(c=>new {c.instanceId,c.definitionId,c.state.fundAccrual,sale=s.GetControllerSalePrice(c.instanceId)}).ToArray(),
            tripsLevel=x.handLevels.Single(l=>l.handId=="trips").level,hash=GameSession.StateHash(x),x.diceRngState,x.shopRngState,x.effectRngState});
    }
    private static GameSession Initial(string[] ids,int pip,string target,int stage=0,bool strong=false)
    {
        var s=GameSession.CreateDebugWorkspace(cfg,new MemorySaveStore());
        var d=s.ExportDebugScene(); d.stageIndex=stage;d.target=target;d.score="0";d.gold=1000000;d.remainingSubmissions=3;
        d.currentResults=null;d.orderedLockedIds.Clear();d.mimicBindings=null;d.previousPrimaryHandId=null;
        d.controllers=ids.Select((id,i)=>new ControllerInstance {instanceId="ITEM_"+(i+1).ToString("D5"),definitionId=id,acquiredSerial=i+1,enchantmentId="none"}).ToList();
        foreach(var die in d.dice)foreach(var f in die.faces){f.permanentPip=pip;f.baseColor="blue";}
        if(strong)d.handLevels.Single(l=>l.handId=="five_kind").level=100;
        if(ids.Contains("C52"))d.controllers.Single(c=>c.definitionId=="C52").state.fundAccrual=9;
        Send(s,CommandKind.ApplyDebugScene,c=>c.debugScene=d);return s;
    }
    private static void RollLockSubmit(GameSession s,int count)
    {
        Send(s,CommandKind.Roll);
        foreach(string id in s.State.dice.Take(count).Select(d=>d.dieId))Check(s.TogglePending(id),"Cannot choose die");
        Send(s,CommandKind.ConfirmLocks); Send(s,CommandKind.Submit);
    }
    private static void NextStage(GameSession s)
    {
        Send(s,CommandKind.LeaveShop);Send(s,CommandKind.StartStage);
    }
    private static void Finish(GameSession s)
    {
        var cmd=Command(s,CommandKind.EndStage);var r=s.Execute(cmd);Check(r.accepted,r.error);Record(s,"EndStage");
        string before=GameSession.StateHash(s.State);var duplicate=s.Execute(cmd);
        Check(duplicate.accepted&&duplicate.duplicate&&GameSession.StateHash(s.State)==before,"Finalize retried changed state");
        Record(s,"DuplicateEndStage");
    }
    private static void Test(string id,Action run)
    {
        steps=new List<object>();try{run();results.Add(new {id,status="passed",steps});}
        catch(Exception e){failures++;results.Add(new {id,status="failed",error=e.ToString(),steps});Console.WriteLine(id+": "+e);}
    }
    public static int Main(string[] args)
    {
        string raw=File.ReadAllText("DicePrototype/Assets/Game/Data/P0Config.json");GameConfig.ValidateJson(raw);
        cfg=JsonSerializer.Deserialize<GameConfig>(GameConfig.PrepareJsonForUnity(raw),Json);cfg.Validate();
        Test("E011-trips-scatter-trips",()=>{
            var s=Initial(new[]{"C43"},2,"999999999999999999999");
            foreach(var expected in new[]{("trips",3),("scatter",1),("trips",3)}){
                RollLockSubmit(s,expected.Item2);Check(s.State.previousPrimaryHandId==expected.Item1,"History did not follow committed hand");
                Check(s.State.handLevels.Single(l=>l.handId=="trips").level==0,"Scatter failed to break consecutive trips");
            }
        });
        foreach(bool acquireLater in new[]{false,true}){
            bool later=acquireLater;
            Test(later?"E013-history-before-acquiring-C43":"E012-consecutive-primary-across-stages",()=>{
                var s=Initial(later?Array.Empty<string>():new[]{"C43"},2,"1");RollLockSubmit(s,3);Finish(s);Send(s,CommandKind.Continue);
                if(later){
                    for(int tries=0;!s.State.shop.offers.Any(o=>o.category=="controller"&&o.contentId=="C43"&&!o.sold)&&tries<200;tries++)Send(s,CommandKind.RefreshControllers);
                    var offer=s.State.shop.offers.FirstOrDefault(o=>o.category=="controller"&&o.contentId=="C43"&&!o.sold);Check(offer!=null,"No C43 offer in bounded legal refreshes");
                    Send(s,CommandKind.Buy,c=>c.offerId=offer.offerId);
                }
                Check(s.State.previousPrimaryHandId=="trips","Shop/acquisition erased previous primary");NextStage(s);RollLockSubmit(s,3);
                Check(s.State.handLevels.Single(l=>l.handId=="trips").level==1,"First matching cross-stage hand did not upgrade after score");
                Check(s.State.lastSubmission.breakdown.primaryHandId=="trips","Unexpected primary");
            });
        }
        Test("E049-E052-C41-fund-rebind-and-return",()=>{
            var s=Initial(new[]{"C41","C52","C46"},6,"1",strong:true);
            string mimic=s.State.controllers[0].instanceId,fund=s.State.controllers[1].instanceId,mult=s.State.controllers[2].instanceId;
            Check(s.GetControllerSalePrice(mimic)==1&&s.GetControllerSalePrice(fund)==10,"Binding copied pre-existing target fund");
            RollLockSubmit(s,5);Finish(s);
            Check(s.GetControllerSalePrice(mimic)==4&&s.GetControllerSalePrice(fund)==13,"First per-emitter fund growth incorrect");
            Send(s,CommandKind.Continue);Send(s,CommandKind.ReorderControllers,c=>c.controllerIds=new[]{fund,mimic,mult});NextStage(s);
            Check(s.State.stage.mimicBindings.Single(b=>b.sourceInstanceId==mimic).targetInstanceId==mult,"Second-stage binding not C46");
            RollLockSubmit(s,5);Finish(s);Check(s.GetControllerSalePrice(mimic)==4,"Fund changed while copying non-fund source");
            Send(s,CommandKind.Continue);Send(s,CommandKind.ReorderControllers,c=>c.controllerIds=new[]{mimic,fund,mult});NextStage(s);
            Check(s.State.stage.mimicBindings.Single(b=>b.sourceInstanceId==mimic).targetInstanceId==fund,"Third-stage binding not restored C52");
            RollLockSubmit(s,5);Finish(s);Check(s.GetControllerSalePrice(mimic)==7,"Rebinding reset/copied fund rather than adding3");
        });
        Test("E053-final-stage-fund-once-no-final-shop",()=>{
            var s=Initial(new[]{"C41","C52"},6,"1",stage:23,strong:true);RollLockSubmit(s,5);Finish(s);
            Check(s.State.phase=="StageSettlement"&&s.State.stage.settlementContinuation=="RunWon"&&s.State.shop==null,"Final settlement successor incorrect");
            Send(s,CommandKind.Continue);
            Check(s.State.phase=="RunWon"&&s.State.shop==null,"Final continuation generated a shop or missed victory");
            Check(s.State.controllers[0].state.fundAccrual==3&&s.State.controllers[1].state.fundAccrual==12,"Final-stage fund wrong");
            string before=GameSession.StateHash(s.State);var c=Command(s,CommandKind.Continue);Check(!s.Execute(c).accepted&&GameSession.StateHash(s.State)==before,"Victory continue paid again");
        });
        var report=new {scope="Independent managed exact E-sequences. One isolated debug initial fixture followed by actual commands. No Unity, Player, balance, normal-run or platform acceptance.",passed=results.Count-failures,failed=failures,total=results.Count,results};
        File.WriteAllText(args[0],JsonSerializer.Serialize(report,Json));Console.WriteLine($"{results.Count-failures}/{results.Count} passed");return failures==0?0:1;
    }
}
