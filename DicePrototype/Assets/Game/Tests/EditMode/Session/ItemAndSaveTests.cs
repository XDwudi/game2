using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using DiceCraft.Core;

namespace DiceCraft.Tests
{
    public sealed class ItemAndSaveTests : SessionTestSupport
    {
        private sealed class FailingStore : ISaveStore
        {
            public bool fail;
            public int attempts;
            public RunState firstAttempt, saved;
            public void Save(RunState snapshot)
            {
                attempts++; if(firstAttempt==null)firstAttempt=snapshot.Clone();
                if(fail)throw new IOException("Injected disk-full error");saved=snapshot.Clone();
            }
        }

        [TestCase("I01")] [TestCase("I02")] [TestCase("I03")] [TestCase("I04")] [TestCase("I05")] [TestCase("I06")] [TestCase("I07")]
        public void CONTENT_ItemSuccessfulUseAndDuplicateReceiptDoNotConsumeAgain(string id)
        {
            var s=id=="I01"?Selecting(new[]{1,2,3,4,5},lockedCount:1):Ready();string item=null;
            s=Edit(s,x=>item=AddItem(x,id));var c=Command(s,CommandKind.UseItem);c.itemInstanceId=item;
            c.dieIds=new[]{"D001","D002"};c.targetPips=new[]{6,1};
            Assert.That(s.Execute(c).accepted,Is.True,id);var after=s.State;Assert.That(s.State.inventory,Is.Empty);
            Assert.That(s.State.consumedItemIds,Does.Contain(item));Assert.That(s.Execute(c).duplicate,Is.True);AssertUnchanged(after,s.State);
            c=Command(s,CommandKind.UseItem);c.itemInstanceId=item;Assert.That(s.Execute(c).accepted,Is.False);
        }
        [TestCase("I01")] [TestCase("I02")] [TestCase("I03")] [TestCase("I04")] [TestCase("I05")] [TestCase("I06")] [TestCase("I07")]
        public void CONTENT_ItemRejectsOutsideWindowWithoutConsumption(string id)
        {
            string item=null;var s=Edit(New(),x=>item=AddItem(x,id));var before=s.State;var c=Command(s,CommandKind.UseItem);c.itemInstanceId=item;
            Assert.That(s.Execute(c).accepted,Is.False);AssertUnchanged(before,s.State);
        }
        [TestCase("I01")] [TestCase("I04")] [TestCase("I05")]
        public void CONTENT_HandBuffDoesNotStack(string id)
        {
            var s=Selecting(new[]{1,2,3,4,5},lockedCount:1);string first=null,second=null;
            s=Edit(s,x=>{x.hand.rollsRemaining=1;first=AddItem(x,id);second=AddItem(x,id);});
            Do(s,CommandKind.UseItem,c=>c.itemInstanceId=first);var before=s.State;var c2=Command(s,CommandKind.UseItem);c2.itemInstanceId=second;
            Assert.That(s.Execute(c2).accepted,Is.False);AssertUnchanged(before,s.State);Assert.That(s.State.inventory.Single().instanceId,Is.EqualTo(second));
        }
        // v1.1 (text proposal §5 error 1, TX-08): the second use in one hand names the item that is already active; I05
        // used to name the card C37 ("缩小射线") instead of the item 小数多来.
        [TestCase("I01","本手红色浪潮已经生效。")] [TestCase("I04","本手大数多来已经生效。")] [TestCase("I05","本手小数多来已经生效。")]
        [TestCase("I09","本手灵光一闪已经生效。")] [TestCase("I10","本手蓝色浪潮已经生效。")]
        public void TX08_ASecondUseInOneHandNamesTheItemAlreadyActive(string id, string expected)
        {
            var s=Selecting(new[]{1,2,3,4,5},lockedCount:1);string first=null,second=null;
            s=Edit(s,x=>{x.hand.rollsRemaining=1;first=AddItem(x,id);second=AddItem(x,id);});
            Do(s,CommandKind.UseItem,c=>c.itemInstanceId=first);var c2=Command(s,CommandKind.UseItem);c2.itemInstanceId=second;
            Assert.That(s.Execute(c2).error,Is.EqualTo(expected));
        }
        [Test] public void ItemWindowsAndAbsentPermanentPipRejected()
        {
            foreach(string id in new[]{"I01","I04","I05"}){
                string item=null;var s=Edit(Ready(),x=>{item=AddItem(x,id);foreach(var d in x.dice)foreach(var f in d.faces)f.permanentPip=3;});
                var c=Command(s,CommandKind.UseItem);c.itemInstanceId=item;Assert.That(s.Execute(c).accepted,Is.False,id);
            }
            string temporary=null;var selecting=Edit(Selecting(new[]{1,2,3}),x=>temporary=AddItem(x,"I02"));
            var temp=Command(selecting,CommandKind.UseItem);temp.itemInstanceId=temporary;Assert.That(selecting.Execute(temp).accepted,Is.False);
        }
        [Test] public void FLOW_D05_D06_ProbabilityBuffsUsePermanentPipsAndDoNotStack()
        {
            string large=null,small=null;var s=Edit(Ready(),x=>{large=AddItem(x,"I04");small=AddItem(x,"I05");});
            Do(s,CommandKind.UseItem,c=>c.itemInstanceId=large);Do(s,CommandKind.UseItem,c=>c.itemInstanceId=small);
            Assert.That(s.ProbabilityUnits("D001"),Is.EqualTo(new[]{214286,142857,142857,142857,142857,214286}));
            s=Edit(s,x=>{var r=x.hand.currentResults[0];r.currentPip=6;r.remoteOverride=true;});
            Assert.That(s.ProbabilityUnits("D001"),Is.EqualTo(new[]{214286,142857,142857,142857,142857,214286}));
        }
        [Test] public void FLOW_D07_TextureReplacementPreservesOtherAttributes()
        {
            string offer=null;var s=Edit(Shop(),x=>{x.gold=100;var f=x.dice[0].faces[0];f.texture="smooth";f.isGold=true;f.baseColor="blue";offer=PutOffer(x,"die_upgrade","U02");});
            Do(s,CommandKind.Buy,c=>{c.offerId=offer;c.targetDieId="D001";c.targetFaceId="F01";});var face=s.State.dice[0].faces[0];
            Assert.That(face.texture,Is.EqualTo("rough"));Assert.That(face.isGold,Is.True);Assert.That(face.baseColor,Is.EqualTo("blue"));
        }
        [Test] public void FLOW_D08_RepeatedLoadsNeverCompoundProbabilityWeights()
        {
            var s=Edit(Ready(),x=>{x.hand.largeBuff=true;x.hand.smallBuff=true;for(int i=0;i<6;i++)x.dice[0].faces[i].texture=i%2==0?"rough":"smooth";});
            var expected=s.ProbabilityUnits("D001");
            for(int i=0;i<10;i++){s=new GameSession(cfg,new MemorySaveStore(),s.State);Assert.That(s.ProbabilityUnits("D001"),Is.EqualTo(expected));}
        }
        [Test] public void FLOW_E13_TransformSaveFailureRetainsExactCandidateAndBlocksNewActions()
        {
            string offer=null;var store=new FailingStore{fail=true};
            var s=Edit(Shop(),x=>{x.gold=100;offer=PutOffer(x,"die_upgrade","U07");},store);
            var c=Command(s,CommandKind.Buy);c.offerId=offer;c.targetDieId="D001";c.targetFaceId="F01";var before=s.State;
            var result=s.Execute(c);Assert.That(result.pendingSave,Is.True);Assert.That(result.accepted,Is.False);AssertUnchanged(before,s.State);
            Assert.That(s.Execute(Command(s,CommandKind.LeaveShop)).pendingSave,Is.True);Assert.That(store.attempts,Is.EqualTo(1));
            var expected=store.firstAttempt;store.fail=false;Assert.That(s.RetrySave().accepted,Is.True);
            Assert.That(s.State.gold,Is.EqualTo(expected.gold));Assert.That(s.State.effectRngState,Is.EqualTo(expected.effectRngState));Assert.That(s.State.dice[0].faces[0].permanentPip,Is.EqualTo(expected.dice[0].faces[0].permanentPip));
            Assert.That(s.Execute(c).duplicate,Is.True);Assert.That(store.attempts,Is.EqualTo(2));
        }
        [Test] public void FLOW_E13_CopySaveFailureDoesNotApplyUntilCommitOrRepeatOnRetry()
        {
            var store=new FailingStore{fail=true};var s=Edit(Selecting(new[]{1,2},controllers:new[]{"C07"}),x=>x.dice[0].faces[3].permanentPip=1,store);
            var c=Command(s,CommandKind.Submit);var before=s.State;Assert.That(s.Execute(c).pendingSave,Is.True);AssertUnchanged(before,s.State);
            Assert.That(s.State.dice[1].faces[3].permanentPip,Is.EqualTo(4));store.fail=false;Assert.That(s.RetrySave().accepted,Is.True);
            Assert.That(s.State.dice[1].faces[3].permanentPip,Is.EqualTo(1));Assert.That(s.State.dice[1].dataRevision,Is.EqualTo(1));
            Assert.That(s.Execute(c).duplicate,Is.True);Assert.That(s.State.dice[1].dataRevision,Is.EqualTo(1));
        }
        [Test] public void FLOW_E14_LocksOrderBuffsAndRngSurviveRestore()
        {
            string wave=null,bias=null;var s=Edit(Ready("C05"),x=>{wave=AddItem(x,"I01");bias=AddItem(x,"I04");});
            Do(s,CommandKind.Roll);Lock(s,"D003","D001");Do(s,CommandKind.Reorder,c=>c.dieIds=new[]{"D001","D003"});
            Do(s,CommandKind.UseItem,c=>c.itemInstanceId=wave);Do(s,CommandKind.UseItem,c=>c.itemInstanceId=bias);
            s.TogglePending("D004");var before=s.State;var restored=new GameSession(cfg,new MemorySaveStore(),before);
            Assert.That(restored.PendingIds,Is.Empty);Assert.That(restored.State.hand.orderedLockedIds,Is.EqualTo(before.hand.orderedLockedIds));
            Assert.That(restored.State.hand.redWave&&restored.State.hand.largeBuff,Is.True);AssertUnchanged(before,restored.State);
            Assert.That(restored.PreviewScore().score,Is.EqualTo(s.PreviewScore().score));
        }
        [Test] public void PreviewDoesNotAdvanceAnyRngOrApplyCopyOrDye()
        {
            var s=Selecting(new[]{1,2},controllers:new[]{"C07"});var before=s.State;
            for(int i=0;i<50;i++){s.PreviewScore();s.PreviewSnapshot();s.ProbabilityUnits("D001");}
            AssertUnchanged(before,s.State);Assert.That(s.State.dice[1].dataRevision,Is.Zero);
        }
        [Test] public void DuplicateReceiptPrecedesSequenceAndBusyChecks()
        {
            var s=Ready();var c=Command(s,CommandKind.Roll);Assert.That(s.Execute(c).accepted,Is.True);s.Busy=true;
            Assert.That(s.Execute(c).duplicate,Is.True);var old=Command(s,CommandKind.Roll);old.expectedActionSequence=0;s.Busy=false;
            var before=s.State;Assert.That(s.Execute(old).accepted,Is.False);AssertUnchanged(before,s.State);
        }
        [Test] public void StateCopiesCannotMutateAuthoritativeData()
        {
            var s=Ready();var copy=s.State;copy.gold=999;copy.dice[0].faces[0].permanentPip=6;
            Assert.That(s.State.gold,Is.EqualTo(0));Assert.That(s.State.dice[0].faces[0].permanentPip,Is.EqualTo(1));
        }
        [Test] public void RestoreRejectsWrongSchemaUnknownContentAndDuplicateDice()
        {
            var state=Ready().State;state.saveSchemaVersion=1;Assert.That(()=>new GameSession(cfg,new MemorySaveStore(),state),Throws.Exception);
            state=Ready().State;state.inventory.Add(new OwnedItem{instanceId="BROKEN",contentId="I99"});Assert.That(()=>new GameSession(cfg,new MemorySaveStore(),state),Throws.Exception);
            state=Ready().State;state.dice[1]=state.dice[0].Clone();Assert.That(()=>new GameSession(cfg,new MemorySaveStore(),state),Throws.Exception);
        }
        [Test] public void IdenticalSeedAndCommandsReproduceResultsShopAndRng()
        {
            var a=New(20261028);var b=New(20261028);Do(a,CommandKind.StartStage);Do(b,CommandKind.StartStage);
            for(int i=0;i<3;i++){
                Do(a,CommandKind.Roll);Do(b,CommandKind.Roll);
                Assert.That(a.State.hand.currentResults.Select(x=>x.currentFaceId),Is.EqualTo(b.State.hand.currentResults.Select(x=>x.currentFaceId)));
                Assert.That(a.State.diceRngState,Is.EqualTo(b.State.diceRngState));
            }
        }
        [Test] public void TwentyFourStageFlowSupportsEightRiskBranchesAndNoFinalShop()
        {
            var s=Edit(New(),x=>{foreach(var d in x.dice)foreach(var f in d.faces){f.permanentPip=4;f.baseColor="red";f.isGold=true;}Equip(x,"C25","C30","C31","C32","C46");/* p0.9.0: the fifth card lifts this one-submission fixture from 11.7M to 46.6M, above the new 15M final target */foreach(var h in x.handLevels)h.level=8;foreach(var h in x.colorLevels)h.level=8;foreach(var c in x.controllers)c.enchantmentId="mult2";});
            for(int stage=0;stage<24;stage++){
                if(cfg.stages[stage].riskEnabled)Do(s,CommandKind.ChooseChallenge,c=>c.risk=true);
                // p1.1.1 rewrite: this is a flow test. With the p1.1.1 numbers and a boss plan that depends on the seed the build above
                // no longer clears every stage in one hand (a boss may also shrink the row or the submission budget), so the
                // stage score is preset to the target; the hand locks what the row holds and is still rolled, scored and submitted.
                Do(s,CommandKind.StartStage);s=Edit(s,x=>{x.stage.score=x.stage.target;x.stage.qualified=true;});
                Do(s,CommandKind.Roll);Lock(s,s.State.dice.Select(x=>x.dieId).Take(s.State.stage.capacity).ToArray());Do(s,CommandKind.Submit);
                if(s.State.phase!="StageSettlement")Do(s,CommandKind.EndStage);Assert.That(s.State.phase,Is.EqualTo("StageSettlement"),"stage "+stage);Do(s,CommandKind.Continue);
                if(stage<23){
                    Assert.That(s.State.phase,Is.EqualTo("Shop"));
                    // v1.1 rewrite (RULES_DELTA): after each of the seven boss stages the loot must be claimed or cashed out first.
                    bool boss=cfg.stages[stage].stageKind=="boss";Assert.That(s.IsLootPending,Is.EqualTo(boss),"stage "+stage);
                    if(boss){var early=s.Execute(Command(s,CommandKind.LeaveShop));Assert.That(early.accepted,Is.False);Assert.That(early.error,Is.EqualTo("先领取首领战利品。"));
                        Do(s,CommandKind.Buy,c=>c.offerId=s.State.shop.offers.Single(o=>o.groupId=="LOOT"&&o.category=="gold").offerId);}
                    Do(s,CommandKind.LeaveShop);}
            }
            Assert.That(s.State.phase,Is.EqualTo("RunWon"));Assert.That(s.State.shop,Is.Null);
            Do(s,CommandKind.NewRun,c=>c.seed=99);Assert.That(s.State.gold,Is.EqualTo(0));Assert.That(s.State.controllers,Is.Empty);Assert.That(s.State.baseSubmitCapacity,Is.EqualTo(5));
        }
    }
}
