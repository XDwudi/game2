using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using DiceCraft.Core;

internal static class SessionChecks
{
    private static GameConfig cfg;
    private static Action<string,Action> test;
    private static Action<bool,string> check;
    private static int serial;
    private static string Dump(object value)=>JsonSerializer.Serialize(value,ReviewProgram.Json);
    private static CommandResult Send(GameSession session,CommandKind kind,DebugScene scene=null,string id=null)
    {
        return session.Execute(new SessionCommand {kind=kind,debugScene=scene,commandId=id??"REVIEW-CMD-"+(++serial),expectedActionSequence=session.ActionSequence,seed=20261031});
    }
    private static void Accepted(CommandResult result,string detail)=>check(result.accepted,detail+": "+result.error);
    private static ControllerInstance Card(string id,int index,string enchant="none")
        =>new ControllerInstance {instanceId="ITEM_"+index.ToString("D5"),acquiredSerial=index,definitionId=id,enchantmentId=enchant,
            state=new ControllerDynamicState {activeColor=id=="C45"?"red":null}};
    private static DebugScene Draft(GameSession session,params ControllerInstance[] controllers)
    {
        var d=session.ExportDebugScene();d.controllers=controllers.ToList();d.gold=100;d.capacityOverride=5;d.target="999999999999999999999999";
        d.currentResults=d.dice.Select(x=>new DieHandState {dieId=x.dieId,currentFaceId="F06",currentPip=6,currentColor="blue",hasRolled=true,isLocked=true}).ToList();
        d.orderedLockedIds=d.dice.Select(x=>x.dieId).ToList();d.remainingSubmissions=20;d.rollsRemaining=0;
        d.mimicBindings=null;return d;
    }
    public static void Run(GameConfig config,Action<string,Action> testCase,Action<bool,string> assertion)
    {
        cfg=config;test=testCase;check=assertion;
        foreach(string pure in cfg.enchantRules.nonSubmitControllerIds)
        foreach(string enchant in new[]{"base10","mult2"}) {
        string selected=pure,selectedEnchant=enchant;
        test("SESSION-DEBUG-NONSUBMIT-EXPERIMENT-CAN-ENTER-SHOP-"+selected+"-"+selectedEnchant,()=> {
            var s=GameSession.CreateDebugWorkspace(cfg,new MemorySaveStore());
            var d=Draft(s,Card(selected,1,selectedEnchant));d.target="180";d.score="180";
            Accepted(Send(s,CommandKind.ApplyDebugScene,d),"Apply legal debug-only enchant");
            Accepted(Send(s,CommandKind.EndStage),"End qualified debug stage");
            Accepted(Send(s,CommandKind.Continue),"Generate ordinary quotes from debug equipment");
            check(s.State.phase=="Shop","Debug valid combination cannot traverse normal shop.");
            check(s.State.shop.offers.Where(o=>o.category=="controller").All(o=>ControllerRules.IsEnchantmentLegal(o.contentId,o.controller.enchantmentId,cfg)),"Debug equipment polluted normal offer pool.");
        });
        }
        test("SESSION-DEBUG-THIRTYTWO-K-RESULTS-AND-ORDINAL-HISTORY",()=> {
            var s=GameSession.CreateDebugWorkspace(cfg,new MemorySaveStore());
            var d=Draft(s,Card("C43",1));d.capacityOverride=32;d.previousPrimaryHandId="five_kind";
            d.handLevels.Single(l=>l.handId=="five_kind").level=99;
            string firstHand=s.State.hand?.handId;
            Accepted(Send(s,CommandKind.ApplyDebugScene,d),"Apply 32 K independent from 5 dice");
            var before=s.State;check(before.stage.capacity==32&&before.hand.orderedLockedIds.Count==5&&before.hand.hasRolled,"Current results discarded or K clamped.");
            Accepted(Send(s,CommandKind.Submit),"Submit edited current results");
            var after=s.State;check(after.handLevels.Single(l=>l.handId=="five_kind").level==100,"C43 did not use previous primary.");
            check(after.previousPrimaryHandId=="five_kind"&&after.stage.submissionOrdinal==1,"Global primary or ordinal not updated.");
            check(after.stage.capacity==32,"K changed after PrepareHand.");
        });
        test("SESSION-SHARED-RNG-ORDER-OVERRIDES-AND-PURE-PREVIEW",()=> {
            var s=GameSession.CreateDebugWorkspace(cfg,new MemorySaveStore());
            var d=Draft(s,Card("C41",1),Card("C53",2),Card("C41",3),Card("C45",4),Card("C41",5),Card("C42",6));
            d.randomOverrides=new List<DebugRandomOverride>{new DebugRandomOverride {kind="C53",sourceId="ITEM_00002",value="1"},
                new DebugRandomOverride {kind="C45",sourceId="ITEM_00004",value="blue"},new DebugRandomOverride {kind="C42",sourceId="ITEM_00005",value="keep"},
                new DebugRandomOverride {kind="C42",sourceId="ITEM_00006",value="break"}};
            Accepted(Send(s,CommandKind.ApplyDebugScene,d),"Apply shared-owner scene");
            string original=Dump(s.State);
            var preview=s.PreviewScoreRange();s.PreviewScoreRange();s.PreviewSnapshot();s.PreviewSubmissionEffects();
            check(Dump(s.State)==original,"Read-only preview changed state/overrides/RNG.");
            check(!preview.isRandom,"Specified shared C53 override still shown as random.");
            uint state=s.State.effectRngState;var expected=new GameRng(state);expected.NextIndex(9);expected.NextIndex(2);expected.NextIndex(100);expected.NextIndex(100);
            Accepted(Send(s,CommandKind.Submit),"Submit with overrides");
            var result=s.State;
            check(result.effectRngState==expected.State,"Owner/emitter draws were duplicated or skipped.");
            check(result.lastRandomResults.Select(x=>x.kind).SequenceEqual(new[]{"C53","C45","C42","C42"}),"Random subphase ordering changed.");
            check(result.lastRandomResults.All(x=>x.overridden)&&result.randomOverrides.Count==0,"Override consumed incorrectly.");
            check(result.lastSubmission.breakdown.score==preview.minimum.score,"Preview forced branch differs from commit.");
            check(result.controllers.Any(c=>c.instanceId=="ITEM_00005")&&!result.controllers.Any(c=>c.instanceId=="ITEM_00006"),"Wrong risk bearer destroyed.");
            var source=ControllerRules.ResolveSources(result.controllers,result.stage.mimicBindings).Single(x=>x.emitterId=="ITEM_00005");
            check(source.abilityId==null,"Lost C42 target retargeted after destruction.");
        });
        test("SESSION-DEBUG-ENTER-DOES-NOT-MUTATE-NORMAL-CHECKPOINT",()=> {
            var normalStore=new MemorySaveStore();var normal=new GameSession(cfg,normalStore);
            Accepted(Send(normal,CommandKind.NewRun),"New normal");Accepted(Send(normal,CommandKind.StartStage),"Start normal");
            Accepted(Send(normal,CommandKind.Roll),"Roll normal");string before=Dump(normal.State),saved=Dump(normalStore.Saved);
            var debugStore=new MemorySaveStore();var debug=GameSession.CreateDebugWorkspace(cfg,debugStore,normal.State);
            check(debug.State.debugWorkspace&&!debug.State.isDebug,"Read-only workspace prematurely changes debug mark.");
            check(debug.State.runId!=normal.State.runId&&debug.State.sourceRunId==normal.State.runId,"Debug identity not isolated.");
            Accepted(Send(debug,CommandKind.ApplyDebugScene,Draft(debug,Card("C49",1))),"First debug mutation");
            check(debug.State.isDebug&&debugStore.Saved.isDebug,"First mutation not marked.");
            check(Dump(normal.State)==before&&Dump(normalStore.Saved)==saved,"Debug wrote normal state/save.");
        });
        test("SESSION-STALE-UI-REQUEST-DOES-NOT-REPLAY-RANDOM",()=> {
            var s=GameSession.CreateDebugWorkspace(cfg,new MemorySaveStore());Accepted(Send(s,CommandKind.ApplyDebugScene,Draft(s,Card("C53",1))),"Apply");
            string id="REPLAY-RANDOM";var first=Send(s,CommandKind.Submit,null,id);Accepted(first,"Submit");string committed=Dump(s.State);
            var repeat=s.Execute(new SessionCommand {kind=CommandKind.Submit,commandId=id,expectedActionSequence=-100});
            check(repeat.accepted&&repeat.duplicate&&Dump(first.receipt)==Dump(repeat.receipt),"Duplicate did not return original receipt before stale/phase checks.");
            check(Dump(s.State)==committed,"Duplicate changed state.");
        });
    }
}
