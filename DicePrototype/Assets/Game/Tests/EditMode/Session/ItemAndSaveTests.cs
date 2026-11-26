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
            // p1.1.1 (RULES_DELTA sections 2, 10 and 11, targets final since W6): one fixed build wins all 24 stages by real
            // scoring. Every pressure stage takes the pressure target, every boss stage keeps the modifier its cycle drew, and no
            // score, target or modifier is written by the test. Seed 1 (the default of New()) draws B05 (first die sealed), B25
            // (two leftmost cards disabled), B17 (red dice sealed), B14 (even dice sealed), B23, B10 (row -1), B06 (enchantments
            // silent) and B39 (base score from pips only) for the final stage. Stage numbers in this comment count from 1, the
            // labels of the assertions from 0.
            // Build: five dice whose six faces are all a red gold 4; every hand and colour at level 12; C31 x3, C87 x2.25, C89
            // x1.3^5, C155 x2 and C101 x3 (which leaves two submissions per stage), each with the +1.5 score enchantment. One
            // hand is base 26 (20 pips + 6 for two unused rolls) x 60 (five of a kind) x 4.5 (red group) x 17.5 (1 + 6.5 colour
            // + 2.5 gold + 7.5 enchantments) x 150.37 = 18,473,404. No card adds to the base score, so B39 changes nothing: the
            // final stage scores the same with or without its modifier, 2.64 times the target of 7,000,000. Every stage is won
            // by its first hand; the closest besides the final one are stage 23 (pressure 9,300,000: 1.99 times), stage 21
            // (B06: 10,556,231, 3.52 times) and stage 18 (B10, four dice: 3,287,862, 4.33 times). With all five dice sealed
            // (stages 9 and 12) the base is the 6 of the unused rolls and the hand 4,263,093.
            // The build stays inside the limits of a normal run (five ordinary slots, no unique multiplier card twice, levels at
            // the cap of 12), so it is loaded as a normal-run save: the session then also checks on every command that the
            // target is the configured one and the boss modifier the planned one.
            var build=New().State;
            foreach(var d in build.dice)foreach(var f in d.faces){f.permanentPip=4;f.baseColor="red";f.isGold=true;}
            Equip(build,"C31","C87","C89","C155","C101");foreach(var c in build.controllers)c.enchantmentId="mult2";
            foreach(var h in build.handLevels)h.level=12;foreach(var h in build.colorLevels)h.level=12;
            Assert.That(build.isDebug,Is.False);var s=new GameSession(cfg,new MemorySaveStore(),build);
            for(int stage=0;stage<24;stage++){
                var def=cfg.stages[stage];string planned=def.stageKind=="boss"?s.State.cycles[stage/3].modifierId:"",label="stage "+stage+" "+planned;
                if(def.riskEnabled)Do(s,CommandKind.ChooseChallenge,c=>c.risk=true);
                Do(s,CommandKind.StartStage);
                // Real play: roll, lock what the row holds, submit; again while the target is not reached and a submission is left.
                while(s.State.phase=="HandReady"&&!s.State.stage.qualified){
                    Do(s,CommandKind.Roll);Lock(s,s.State.dice.Select(x=>x.dieId).Take(s.State.stage.capacity).ToArray());Do(s,CommandKind.Submit);}
                var played=s.State.stage;Assert.That(played.qualified,Is.True,label+": "+played.score+" of "+played.target);
                Assert.That(played.risk,Is.EqualTo(def.riskEnabled),label);Assert.That(played.modifierId??"",Is.EqualTo(planned),label);Assert.That(played.modifierNullified,Is.False,label);
                if(s.State.phase!="StageSettlement")Do(s,CommandKind.EndStage);Assert.That(s.State.phase,Is.EqualTo("StageSettlement"),"stage "+stage);
                // The stage was really won: the settled score is at least the target in force (pressure x1.5, boss TargetPercent).
                var target=System.Numerics.BigInteger.Parse(def.riskEnabled?def.riskTarget:def.normalTarget)*(planned!=""&&cfg.Modifier(planned).effects.Contains("TargetPercent")?cfg.Modifier(planned).amount:100)/100;
                var settled=s.State.lastStageSettlement;Assert.That(settled.target,Is.EqualTo(target.ToString()),label);
                Assert.That(System.Numerics.BigInteger.Parse(settled.score)>=target,Is.True,label+": "+settled.score+" of "+settled.target);
                Do(s,CommandKind.Continue);
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
