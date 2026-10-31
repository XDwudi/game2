using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using DiceCraft.Core;
using NUnit.Framework;

namespace DiceCraft.Tests
{
    public sealed class V06SessionTests : SessionTestSupport
    {
        private sealed class FaultStore : ISaveStore
        {
            public bool fail; public int calls; public RunState attempted;
            public void Save(RunState s) { calls++; attempted = s.Clone(); if (fail) throw new IOException("Injected v06 failure"); }
        }
        private GameSession Scene(GameSession source, Action<DebugScene> edit, ISaveStore store = null)
        {
            var result = GameSession.CreateDebugWorkspace(cfg, store ?? new MemorySaveStore(), source.State);
            var draft = result.ExportDebugScene(); edit(draft); Do(result, CommandKind.ApplyDebugScene, c => c.debugScene = draft); return result;
        }
        private string Offer(GameSession session, string id, string enchant, out GameSession changed)
        {
            string offer = null;
            changed = Edit(session, x => { offer = PutOffer(x, "controller", id); x.shop.offers.First(o => o.offerId == offer).controller.enchantmentId = enchant; });
            return offer;
        }

        [Test] public void V06_InitialZeroWalletAndSchema4SeparateColorLevels()
        {
            var s = New().State; Assert.That(s.gold, Is.Zero); Assert.That(s.saveSchemaVersion, Is.EqualTo(4));
            Assert.That(s.handLevels.Count, Is.EqualTo(13)); Assert.That(s.colorLevels.Select(x => x.handId), Is.EquivalentTo(new[]{"all_red","all_blue"}));
            Assert.That(s.stage.target, Is.EqualTo("180")); Assert.That(s.stage.score, Is.EqualTo("0"));
        }

        [Test] public void V06_ReorderOnlyPreviewOrShopNoOpDoesNotSaveOrAdvance()
        {
            var store = new FaultStore(); var s = Edit(New(), x => Equip(x, "C41", "C21", "C22"), store);
            var before = s.State; var same = Command(s, CommandKind.ReorderControllers); same.controllerIds = before.controllers.Select(c => c.instanceId).ToArray();
            Assert.That(s.Execute(same).noChange, Is.True); Assert.That(store.calls, Is.Zero); AssertUnchanged(before, s.State);
            var changed = Command(s, CommandKind.ReorderControllers); changed.controllerIds = same.controllerIds.Reverse().ToArray();
            Assert.That(s.Execute(changed).accepted, Is.True); Assert.That(store.calls, Is.EqualTo(1)); Assert.That(s.State.controllers.Select(c => c.instanceId), Is.EqualTo(changed.controllerIds));
            Assert.That(s.State.shopRngState, Is.EqualTo(before.shopRngState)); Assert.That(s.Execute(changed).duplicate, Is.True);
            Do(s, CommandKind.StartStage); before = s.State;
            var invalid = Command(s, CommandKind.ReorderControllers); invalid.controllerIds = same.controllerIds;
            Assert.That(s.Execute(invalid).accepted, Is.False); AssertUnchanged(before, s.State);
        }

        [Test] public void V06_MimicCopiesCapacityButOnlyOwnBudgetEnchant()
        {
            var s = Edit(New(), x => { Equip(x, "C41", "C21"); x.controllers[0].enchantmentId = "submit1"; x.controllers[1].enchantmentId = "roll1"; });
            Do(s, CommandKind.StartStage); var a = s.State;
            Assert.That(a.stage.capacity, Is.EqualTo(7)); Assert.That(a.stage.remainingSubmissions, Is.EqualTo(4)); Assert.That(a.hand.rollsRemaining, Is.EqualTo(4));
            Assert.That(a.stage.mimicBindings.Single().targetInstanceId, Is.EqualTo(a.controllers[1].instanceId));
            Assert.That(a.stage.submissionBonusSourceIds, Is.EqualTo(new[]{a.controllers[0].instanceId}));
        }

        [Test] public void V06_C05AllSourcesObserveSameZeroBudgetAndNeverRegrant()
        {
            var s = Ready("C41", "C05"); Do(s, CommandKind.Roll); Lock(s,"D001","D002","D003"); Do(s,CommandKind.Roll); Do(s,CommandKind.Roll);
            Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(6)); Assert.That(s.State.hand.c05UsedEmitterIds.Count, Is.EqualTo(2));
            s = new GameSession(cfg, new MemorySaveStore(), s.State);
            for(int i=0;i<6;i++) Do(s,CommandKind.Roll);
            Assert.That(s.State.hand.rollsRemaining, Is.Zero); Assert.That(s.State.hand.c05UsedEmitterIds.Count, Is.EqualTo(2));
        }

        [Test] public void V06_C53SharesOwnerDrawAndPreviewDoesNotConsumeOverride()
        {
            var s = Scene(Selecting(new[]{2}, new[]{"blue"}, new[]{"C41","C53"}), d => d.randomOverrides.Add(new DebugRandomOverride {kind="C53",sourceId=d.controllers[1].instanceId,value="1"}));
            var before = s.State; var rng = new GameRng(before.effectRngState); int raw = rng.NextIndex(9);
            for(int i=0;i<5;i++) { var preview=s.PreviewScoreRange(); Assert.That(preview.isRandom,Is.False); Assert.That(preview.minimum.score,Is.EqualTo("1")); }
            AssertUnchanged(before,s.State); Assert.That(s.State.randomOverrides.Count, Is.EqualTo(1));
            var result=Do(s,CommandKind.Submit); var trace=result.receipt.randomResults.Single(r=>r.kind=="C53");
            Assert.That(trace.rawTicket,Is.EqualTo(raw)); Assert.That(trace.overridden,Is.True); Assert.That(trace.appliedValue,Is.EqualTo("1"));
            Assert.That(s.State.effectRngState,Is.EqualTo(rng.State)); Assert.That(s.State.lastSubmission.breakdown.score,Is.EqualTo("1")); Assert.That(s.State.randomOverrides,Is.Empty);
        }

        [Test] public void V06_C45SharesFrozenColorAndAlwaysAdvancesOncePerOwner()
        {
            var s=Scene(Selecting(new[]{2},new[]{"blue"},new[]{"C41","C45"}),d=>{d.controllers[1].state.activeColor="red";d.randomOverrides.Add(new DebugRandomOverride{kind="C45",sourceId=d.controllers[1].instanceId,value="blue"});});
            var before=s.PreviewScore(); var rng=new GameRng(s.State.effectRngState);rng.NextIndex(2);
            var result=Do(s,CommandKind.Submit);
            Assert.That(s.State.lastSubmission.snapshot.equippedControllers[1].state.activeColor,Is.EqualTo("red"));
            Assert.That(s.State.lastSubmission.breakdown.score,Is.EqualTo(before.score)); Assert.That(s.State.controllers[1].state.activeColor,Is.EqualTo("blue"));
            Assert.That(result.receipt.randomResults.Count(r=>r.kind=="C45"),Is.EqualTo(1)); Assert.That(s.State.effectRngState,Is.EqualTo(rng.State));
        }

        [Test] public void V06_C42FrozenRiskListSurvivesOwnerDeathAndDoesNotRebind()
        {
            var s=Scene(Selecting(new[]{2},controllers:new[]{"C41","C42","C50"}),d=>{
                d.randomOverrides.Add(new DebugRandomOverride{kind="C42",sourceId=d.controllers[0].instanceId,value="keep"});
                d.randomOverrides.Add(new DebugRandomOverride{kind="C42",sourceId=d.controllers[1].instanceId,value="break"});
            });
            string owner=s.State.controllers[1].instanceId; var rng=new GameRng(s.State.effectRngState);rng.NextIndex(100);rng.NextIndex(100);
            Do(s,CommandKind.Submit);Assert.That(s.State.controllers.Any(c=>c.instanceId==owner),Is.False);Assert.That(s.State.effectRngState,Is.EqualTo(rng.State));
            Assert.That(s.State.stage.mimicBindings.Single().targetInstanceId,Is.EqualTo(owner));
            Assert.That(ControllerRules.ResolveSources(s.State.controllers,s.State.stage.mimicBindings)[0].abilityId,Is.Null);
            s=new GameSession(cfg,new MemorySaveStore(),s.State);Assert.That(s.State.stage.capacity,Is.EqualTo(5));
        }

        [Test] public void V06_ZeroScoreStillConsumesDrawsIncomeAndConsecutiveGrowth()
        {
            var s=Scene(Selecting(new[]{2,2},controllers:new[]{"C49","C01","C43","C53"}),d=>{d.previousPrimaryHandId="pair";d.randomOverrides.Add(new DebugRandomOverride{kind="C53",sourceId=d.controllers[3].instanceId,value="16"});});
            var before=s.State;Do(s,CommandKind.Submit);
            Assert.That(s.State.lastSubmission.breakdown.score,Is.EqualTo("0"));Assert.That(s.State.stage.remainingSubmissions,Is.EqualTo(before.stage.remainingSubmissions-1));
            Assert.That(s.State.gold,Is.EqualTo(before.gold+1));Assert.That(s.State.handLevels.Single(h=>h.handId=="pair").level,Is.EqualTo(1));
            Assert.That(s.State.previousPrimaryHandId,Is.EqualTo("pair"));Assert.That(s.State.effectRngState,Is.Not.EqualTo(before.effectRngState));
        }

        [Test] public void V06_PrimaryHistoryExistsWithoutC43AndSurvivesStageBoundaries()
        {
            var s=Selecting(new[]{4,4,4,4,4},new[]{"red","red","red","red","red"});Do(s,CommandKind.Submit);
            Assert.That(s.State.previousPrimaryHandId,Is.EqualTo("five_kind"));Do(s,CommandKind.EndStage);Do(s,CommandKind.Continue);Do(s,CommandKind.LeaveShop);
            Assert.That(s.State.previousPrimaryHandId,Is.EqualTo("five_kind"));
        }

        [Test] public void V06_FundsAccrueForEachEmitterOnlyOnceAtFinalize()
        {
            var s=Selecting(new[]{4,4,4,4,4},new[]{"red","red","red","red","red"},new[]{"C41","C52"});
            s=Edit(s,x=>x.controllers[1].state.fundAccrual=30);Do(s,CommandKind.Submit);
            Assert.That(s.State.controllers[0].state.fundAccrual,Is.Zero);var finish=Command(s,CommandKind.EndStage);Assert.That(s.Execute(finish).accepted,Is.True);
            Assert.That(s.State.controllers.Select(c=>c.state.fundAccrual),Is.EqualTo(new long[]{3,33}));
            Assert.That(s.Execute(finish).duplicate,Is.True);Do(s,CommandKind.Continue);
            Assert.That(s.State.controllers.Select(c=>c.state.fundAccrual),Is.EqualTo(new long[]{3,33}));
            Assert.That(s.GetControllerSalePrice(s.State.controllers[0].instanceId),Is.EqualTo(4));
        }

        [Test] public void V06_MatureFundReplacementCreditsNegativeNetKeepsPositionAndNewState()
        {
            var s=Edit(Shop(),x=>{x.gold=0;Equip(x,"C51","C52","C41");x.controllers[1].state.fundAccrual=30;x.controllers[2].state.fundAccrual=6;});
            string old=s.State.controllers[1].instanceId;string offer=Offer(s,"C52","slotless",out s);var quote=s.State.shop.offers.Single(o=>o.offerId==offer);
            Assert.That(s.GetOfferNetPrice(quote),Is.EqualTo(-24));Do(s,CommandKind.Buy,c=>c.offerId=offer);
            Assert.That(s.State.gold,Is.EqualTo(24));Assert.That(s.State.controllers[1].definitionId,Is.EqualTo("C52"));Assert.That(s.State.controllers[1].instanceId,Is.Not.EqualTo(old));
            Assert.That(s.State.controllers[1].state.fundAccrual,Is.Zero);Assert.That(s.State.controllers[2].state.fundAccrual,Is.EqualTo(6));Assert.That(s.State.controllers[0].state.turnoverUnits2,Is.EqualTo(4));
        }

        [Test] public void V06_C51SelfReplacementDoesNotGrowNewInstanceAndItemSaleDoesNotGrow()
        {
            var s=Edit(Shop(),x=>{x.gold=100;Equip(x,"C51","C41");x.controllers[0].state.turnoverUnits2=20;AddItem(x,"I01");});
            string offer=Offer(s,"C51","mult2",out s);Do(s,CommandKind.Buy,c=>c.offerId=offer);
            Assert.That(s.State.controllers[0].state.turnoverUnits2,Is.EqualTo(3));Do(s,CommandKind.Sell,c=>c.itemInstanceId=s.State.inventory[0].instanceId);
            Assert.That(s.State.controllers[0].state.turnoverUnits2,Is.EqualTo(3));Do(s,CommandKind.Sell,c=>c.itemInstanceId=s.State.controllers[1].instanceId);
            Assert.That(s.State.controllers[0].state.turnoverUnits2,Is.EqualTo(4));
        }

        [Test] public void V06_C45AcquisitionDrawOccursOnlyAfterSuccessfulValidation()
        {
            var s=Edit(Shop(),x=>x.gold=0);string offer=Offer(s,"C45","none",out s);var before=s.State;
            Assert.That(s.State.shop.offers.Single(o=>o.offerId==offer).controller.state.activeColor,Is.Null);
            var denied=Command(s,CommandKind.Buy);denied.offerId=offer;Assert.That(s.Execute(denied).accepted,Is.False);AssertUnchanged(before,s.State);
            s=Edit(s,x=>x.gold=100);var rng=new GameRng(s.State.effectRngState);int ticket=rng.NextIndex(2);Do(s,CommandKind.Buy,c=>c.offerId=offer);
            Assert.That(s.State.controllers.Single().state.activeColor,Is.EqualTo(ticket==0?"red":"blue"));Assert.That(s.State.effectRngState,Is.EqualTo(rng.State));
        }

        [Test] public void V06_RefreshReplacesOnlyControllerBatchAndInvalidatesOldClick()
        {
            var s=Edit(Shop(),x=>x.gold=100);var before=s.State;var others=before.shop.offers.Where(o=>o.category!="controller").Select(o=>o.offerId+":"+o.sold).ToArray();
            string old=before.shop.offers.First(o=>o.category=="controller").offerId;var command=Command(s,CommandKind.RefreshControllers);Assert.That(s.Execute(command).accepted,Is.True);
            Assert.That(s.State.gold,Is.EqualTo(97));Assert.That(s.GetRefreshPrice(),Is.EqualTo(5));Assert.That(s.State.shop.controllerRefreshCount,Is.EqualTo(1));
            Assert.That(s.State.shop.offers.Where(o=>o.category!="controller").Select(o=>o.offerId+":"+o.sold),Is.EqualTo(others));
            Assert.That(s.State.shop.offers.Any(o=>o.offerId==old),Is.False);var bad=Command(s,CommandKind.Buy);bad.offerId=old;Assert.That(s.Execute(bad).accepted,Is.False);
            Assert.That(s.Execute(command).duplicate,Is.True);Assert.That(s.State.gold,Is.EqualTo(97));Do(s,CommandKind.RefreshControllers);Assert.That(s.GetRefreshPrice(),Is.EqualTo(7));
        }

        [Test] public void V06_FreeRefreshUsesIndexZeroAndLatePurchaseNeverRefunds()
        {
            var s=Edit(Shop(),x=>{x.gold=100;x.freeFirstRefreshOwned=true;});Assert.That(s.GetRefreshPrice(),Is.Zero);Do(s,CommandKind.RefreshControllers);
            Assert.That(s.State.gold,Is.EqualTo(100));Assert.That(s.GetRefreshPrice(),Is.EqualTo(5));Do(s,CommandKind.RefreshControllers);Assert.That(s.GetRefreshPrice(),Is.EqualTo(7));
            s=Edit(Shop(),x=>x.gold=100);Do(s,CommandKind.RefreshControllers);string offer=null;s=Edit(s,x=>offer=PutOffer(x,"service","O07"));Do(s,CommandKind.Buy,c=>c.offerId=offer);
            Assert.That(s.State.gold,Is.EqualTo(91));Assert.That(s.State.shop.controllerRefreshCount,Is.EqualTo(1));Assert.That(s.GetRefreshPrice(),Is.EqualTo(5));
        }

        [TestCase("O05","controller")] [TestCase("O06","consumable")]
        public void V06_ExpansionAppendsExactlyOneWithoutRefillingSoldOrOtherOffers(string service,string category)
        {
            var s=Edit(Shop(),x=>x.gold=100);var bought=s.State.shop.offers.First(o=>o.category==category);Do(s,CommandKind.Buy,c=>c.offerId=bought.offerId);
            string serviceOffer=null;s=Edit(s,x=>serviceOffer=PutOffer(x,"service",service));var before=s.State;var old=before.shop.offers.Where(o=>o.category==category).ToArray();
            Do(s,CommandKind.Buy,c=>c.offerId=serviceOffer);var now=s.State.shop.offers.Where(o=>o.category==category).ToArray();
            Assert.That(now.Length,Is.EqualTo(old.Length+1));Assert.That(now.Single(o=>o.offerId==bought.offerId).sold,Is.True);
            Assert.That(now.Select(o=>o.contentId).Distinct().Count(),Is.EqualTo(now.Length));Assert.That(now.Take(old.Length).Select(o=>o.offerId),Is.EqualTo(old.Select(o=>o.offerId)));
        }

        [Test] public void V06_RefreshFailureRetryUsesSameCandidateAndReceipts()
        {
            var store=new FaultStore{fail=true};var s=Edit(Shop(),x=>x.gold=100,store);var before=s.State;var cmd=Command(s,CommandKind.RefreshControllers);
            Assert.That(s.Execute(cmd).pendingSave,Is.True);AssertUnchanged(before,s.State);var attempted=store.attempted;Assert.That(s.Execute(Command(s,CommandKind.RefreshControllers)).pendingSave,Is.True);
            store.fail=false;Assert.That(s.RetrySave().accepted,Is.True);Assert.That(GameSession.StateHash(s.State),Is.EqualTo(GameSession.StateHash(attempted)));Assert.That(store.calls,Is.EqualTo(2));
            Assert.That(s.Execute(cmd).duplicate,Is.True);Assert.That(s.State.gold,Is.EqualTo(97));
        }

        [Test] public void V06_RefreshInsufficientBalanceIsFullyAtomic()
        {
            var s=Edit(Shop(),x=>x.gold=2);var before=s.State;Assert.That(s.Execute(Command(s,CommandKind.RefreshControllers)).accepted,Is.False);
            Assert.That(GameSession.StateHash(s.State),Is.EqualTo(GameSession.StateHash(before)));
        }

        [Test] public void V06_ColorStudyAvailableAtOneDieWithoutBecomingHand()
        {
            var s=Edit(Shop(),x=>{x.gold=100;x.dice.RemoveRange(1,x.dice.Count-1);});Assert.That(s.LegalStudyHands(),Does.Contain("all_red"));
            Do(s,CommandKind.Study,c=>c.handId="all_red");Assert.That(s.State.colorLevels.Single(h=>h.handId=="all_red").level,Is.EqualTo(1));Assert.That(s.State.handLevels.Any(h=>h.handId=="all_red"),Is.False);
        }

        [Test] public void V06_DebugWorkspaceIsolatedAndFirstMutationRebuildsExplicitCurrentHand()
        {
            var normal=New();Do(normal,CommandKind.StartStage);Do(normal,CommandKind.Roll);Lock(normal,"D001");var checkpoint=normal.State;var store=new FaultStore();
            var debug=GameSession.CreateDebugWorkspace(cfg,store,checkpoint);Assert.That(debug.State.isDebug,Is.False);Assert.That(debug.State.debugWorkspace,Is.True);Assert.That(store.calls,Is.Zero);
            string oldHand=debug.State.hand.handId;var draft=debug.ExportDebugScene();draft.gold=999;draft.capacityOverride=32;draft.rollsRemaining=0;
            draft.orderedLockedIds=new List<string>{"D002","D001"};foreach(var r in draft.currentResults){r.hasRolled=true;r.currentFaceId="F01";r.remoteOverride=true;r.currentPip=6;}
            Do(debug,CommandKind.ApplyDebugScene,c=>c.debugScene=draft);Assert.That(debug.State.isDebug,Is.True);Assert.That(debug.State.hand.handId,Is.Not.EqualTo(oldHand));
            Assert.That(debug.State.hand.orderedLockedIds,Is.EqualTo(draft.orderedLockedIds));Assert.That(debug.State.hand.rollsRemaining,Is.Zero);Assert.That(debug.State.stage.capacity,Is.EqualTo(32));
            Assert.That(debug.State.hand.currentResults.All(r=>r.currentPip==6),Is.True);Assert.That(GameSession.StateHash(normal.State),Is.EqualTo(GameSession.StateHash(checkpoint)));
            Assert.That(normal.Execute(new SessionCommand{commandId="illegal-debug",expectedActionSequence=normal.ActionSequence,kind=CommandKind.ApplyDebugScene,debugScene=draft}).accepted,Is.False);
        }

        [Test] public void V06_DebugLimits32Dice32Cards32ItemsAndScoreBeyondInt64RoundTrip()
        {
            var s=Scene(New(),d=>{
                d.dice.Clear();d.controllers.Clear();d.inventory.Clear();d.capacityOverride=32;d.consumableSlots=32;d.currentResults=new List<DieHandState>();d.orderedLockedIds.Clear();
                for(int i=1;i<=32;i++){
                    var die=DiceFactory.CreateStandard(cfg,i);foreach(var f in die.faces){f.permanentPip=6;f.baseColor="red";f.isGold=true;}die.isGlass=true;d.dice.Add(die);
                    d.controllers.Add(new ControllerInstance{instanceId="ITEM_"+i.ToString("D5"),acquiredSerial=i,definitionId="C46",enchantmentId="mult2"});
                    d.inventory.Add(new OwnedItem{instanceId="ITEM_"+(i+32).ToString("D5"),contentId="I01"});
                    d.currentResults.Add(new DieHandState{dieId=die.dieId,currentFaceId="F01",currentPip=6,currentColor="red",hasRolled=true});d.orderedLockedIds.Add(die.dieId);
                }
                foreach(var h in d.handLevels)h.level=100;foreach(var h in d.colorLevels)h.level=100;d.rollsRemaining=999;d.remainingSubmissions=999;
            });
            var expected=s.PreviewScoreRange().minimum.score;Assert.That(ExactScoreMath.Parse(expected)>long.MaxValue,Is.True);Do(s,CommandKind.Submit);
            Assert.That(s.State.stage.score,Is.EqualTo(expected));Assert.That(s.State.controllers.Count,Is.EqualTo(32));Assert.That(s.State.inventory.Count,Is.EqualTo(32));
            s=new GameSession(cfg,new MemorySaveStore(),s.State);Assert.That(s.State.stage.capacity,Is.EqualTo(32));Assert.That(s.State.stage.score,Is.EqualTo(expected));
        }

        [TestCase("duplicate")] [TestCase("retired")] [TestCase("cycle")] [TestCase("missing")] [TestCase("fund")] [TestCase("turnover")] [TestCase("decimal")] [TestCase("dice33")] [TestCase("budget1000")]
        [TestCase("faces-null")] [TestCase("faces-five")] [TestCase("face-null")] [TestCase("face-bad-color")]
        public void V06_DebugInvalidSceneIsAtomic(string invalid)
        {
            var s=GameSession.CreateDebugWorkspace(cfg,new MemorySaveStore(),Ready("C41","C51").State);var draft=s.ExportDebugScene();
            if(invalid=="duplicate")draft.controllers.Add(draft.controllers[0].Clone());
            if(invalid=="retired")draft.controllers[1].definitionId="C04";
            if(invalid=="cycle")draft.mimicBindings=new[]{new MimicBinding{sourceInstanceId=draft.controllers[0].instanceId,targetInstanceId=draft.controllers[0].instanceId}};
            if(invalid=="missing")draft.mimicBindings=new[]{new MimicBinding{sourceInstanceId=draft.controllers[0].instanceId,targetInstanceId="ITEM_00999"}};
            if(invalid=="fund")draft.controllers[0].state.fundAccrual=4;
            if(invalid=="turnover")draft.controllers[1].state.turnoverUnits2=2;
            if(invalid=="decimal")draft.target="1e50";
            if(invalid=="dice33")for(int i=6;i<=33;i++)draft.dice.Add(DiceFactory.CreateStandard(cfg,i));
            if(invalid=="budget1000")draft.rollsRemaining=1000;
            if(invalid=="faces-null")draft.dice[0].faces=null;
            if(invalid=="faces-five")draft.dice[0].faces=draft.dice[0].faces.Take(5).ToArray();
            if(invalid=="face-null")draft.dice[0].faces[0]=null;
            if(invalid=="face-bad-color")draft.dice[0].faces[0].baseColor="purple";
            var before=s.State;var cmd=Command(s,CommandKind.ApplyDebugScene);cmd.debugScene=draft;Assert.That(s.Execute(cmd).accepted,Is.False);Assert.That(GameSession.StateHash(s.State),Is.EqualTo(GameSession.StateHash(before)));
        }

        [Test] public void V06_DebugEditSaveFailureDoesNotLeakAndRetryDoesNotRebuildAgain()
        {
            var normal=New();var store=new FaultStore{fail=true};var s=GameSession.CreateDebugWorkspace(cfg,store,normal.State);var draft=s.ExportDebugScene();draft.gold=20;
            var cmd=Command(s,CommandKind.ApplyDebugScene);cmd.debugScene=draft;var before=s.State;Assert.That(s.Execute(cmd).pendingSave,Is.True);AssertUnchanged(before,s.State);Assert.That(s.State.isDebug,Is.False);
            var candidate=store.attempted;store.fail=false;Assert.That(s.RetrySave().accepted,Is.True);Assert.That(s.State.hand.handId,Is.EqualTo(candidate.hand.handId));Assert.That(s.State.isDebug,Is.True);Assert.That(normal.State.gold,Is.Zero);
        }

        [Test] public void V06_WalletOverflowRejectsWithoutChangingScoreRngOrIncome()
        {
            var s=Edit(Selecting(new[]{2},controllers:new[]{"C01","C53"}),x=>x.gold=long.MaxValue);var before=s.State;
            Assert.That(s.Execute(Command(s,CommandKind.Submit)).accepted,Is.False);Assert.That(GameSession.StateHash(s.State),Is.EqualTo(GameSession.StateHash(before)));
        }

        [TestCase("")] [TestCase("-1")] [TestCase("NaN")] [TestCase("1.0")] [TestCase("1E20")]
        public void V06_InvalidPersistedDecimalNeverLoads(string value)
        {
            var state=Ready().State;state.stage.score=value;Assert.That(()=>new GameSession(cfg,new MemorySaveStore(),state),Throws.Exception);
        }

        [Test] public void V06_AllHistoricalSubmitReceiptsValidatePhysicalDataAndExactPlan()
        {
            var s=Selecting(new[]{2,2});Do(s,CommandKind.Submit);Do(s,CommandKind.Roll);Lock(s,"D001");Do(s,CommandKind.Submit);
            var state=s.State;var old=state.processedReceipts.First(r=>r.submission!=null);
            Assert.That(old.submission.snapshot.dice.Length,Is.EqualTo(2));
            old.submission.breakdown.afterScorePlan.earnedGold++;
            Assert.That(()=>new GameSession(cfg,new MemorySaveStore(),state),Throws.Exception);
            state=s.State;old=state.processedReceipts.First(r=>r.submission!=null);old.submission.snapshot.dice[0].permanentFacesCopy[0].baseColor="red";
            Assert.That(()=>new GameSession(cfg,new MemorySaveStore(),state),Throws.Exception);
        }

        [Test] public void V06_AbandonPendingSaveKeepsCommittedStateAndAllowsNewCommand()
        {
            var store=new FaultStore{fail=true};var s=Edit(Shop(),x=>x.gold=100,store);var before=s.State;
            Assert.That(s.Execute(Command(s,CommandKind.RefreshControllers)).pendingSave,Is.True);
            Assert.That(s.AbandonPendingSave().accepted,Is.True);Assert.That(s.HasPendingSave,Is.False);AssertUnchanged(before,s.State);
            store.fail=false;Do(s,CommandKind.RefreshControllers);Assert.That(s.State.gold,Is.EqualTo(97));
        }

        [Test] public void V06_UnityEmptyNestedPlaceholdersProduceIdenticalNextCommandHash()
        {
            var source=Selecting(new[]{2});Do(source,CommandKind.Submit);var restored=source.State;
            restored.shop=new ShopState();restored.lastStageSettlement=new StageSettlement();
            var reloaded=new GameSession(cfg,new MemorySaveStore(),restored);
            Assert.That(GameSession.StateHash(reloaded.State),Is.EqualTo(GameSession.StateHash(source.State)));
            var cmd=new SessionCommand{commandId="same-after-placeholder",expectedActionSequence=source.ActionSequence,kind=CommandKind.Roll};
            var a=source.Execute(cmd);var b=reloaded.Execute(cmd);Assert.That(a.accepted&&b.accepted,Is.True);
            Assert.That(a.receipt.stateHash,Is.EqualTo(b.receipt.stateHash));
        }

        [Test] public void V06_DebugBlankWorkspaceAndPureNonSubmitScoringEnchantRemainInactive()
        {
            var store=new FaultStore();var s=GameSession.CreateDebugWorkspace(cfg,store);Assert.That(s.State.isDebug,Is.False);Assert.That(s.ActionSequence,Is.Zero);Assert.That(store.calls,Is.Zero);
            var draft=s.ExportDebugScene();draft.controllers.Add(new ControllerInstance{definitionId="C05",enchantmentId="mult2",instanceId="ITEM_00001",acquiredSerial=1});
            Do(s,CommandKind.ApplyDebugScene,c=>c.debugScene=draft);Do(s,CommandKind.Roll);Lock(s,"D001");Do(s,CommandKind.Submit);
            Assert.That(s.State.lastSubmission.breakdown.enchantMultiplierCount,Is.Zero);
        }

        [TestCase("C05","base10")] [TestCase("C05","mult2")]
        [TestCase("C21","base10")] [TestCase("C21","mult2")]
        [TestCase("C22","base10")] [TestCase("C22","mult2")]
        [TestCase("C34","base10")] [TestCase("C34","mult2")]
        [TestCase("C35","base10")] [TestCase("C35","mult2")]
        [TestCase("C52","base10")] [TestCase("C52","mult2")]
        public void V06_DebugNonSubmitExperimentalEnchantDoesNotBlockNormalShopPool(string id,string enchant)
        {
            var s=Scene(New(),d=>{
                d.controllers.Add(new ControllerInstance{definitionId=id,enchantmentId=enchant,acquiredSerial=1,instanceId="ITEM_00001"});
                d.score=d.target;
            });
            Do(s,CommandKind.EndStage);Do(s,CommandKind.Continue);
            Assert.That(s.State.phase,Is.EqualTo("Shop"));
            Assert.That(s.State.shop.offers.Where(o=>o.category=="controller").All(o=>ControllerRules.IsEnchantmentLegal(o.contentId,o.controller.enchantmentId,cfg)),Is.True);
        }

        [TestCase(7,8)] [TestCase(8,8)]
        public void V06_NormalConsecutiveSourcesCapAtEightAndKeepBothTriggers(int initial,int expected)
        {
            var s=Edit(Selecting(new[]{3,3,3},controllers:new[]{"C41","C43"}),x=>{
                x.isDebug=false;x.previousPrimaryHandId="trips";x.handLevels.Single(h=>h.handId=="trips").level=initial;
            });
            var preview=s.PreviewScore();Do(s,CommandKind.Submit);
            Assert.That(s.State.lastSubmission.breakdown.score,Is.EqualTo(preview.score));
            Assert.That(s.State.handLevels.Single(h=>h.handId=="trips").level,Is.EqualTo(expected));
            Assert.That(s.State.lastSubmission.breakdown.afterScorePlan.upgradeRequested,Is.EqualTo(2));
            Assert.That(s.State.submissionOrdinalGlobal,Is.EqualTo(1));
        }

        [Test] public void V06_LastFailedSubmissionAppliesAllPostEffectsButNoFundsOrSuccessReward()
        {
            var s=Scene(Selecting(new[]{2,2},controllers:new[]{"C01","C43","C45","C42","C52","C53"}),d=>{
                d.remainingSubmissions=1;d.target="999999999999999999999999999";d.previousPrimaryHandId="pair";
                d.randomOverrides.Add(new DebugRandomOverride{kind="C53",sourceId=d.controllers[5].instanceId,value="2"});
                d.randomOverrides.Add(new DebugRandomOverride{kind="C45",sourceId=d.controllers[2].instanceId,value="blue"});
                d.randomOverrides.Add(new DebugRandomOverride{kind="C42",sourceId=d.controllers[3].instanceId,value="break"});
            });
            var before=s.State;Do(s,CommandKind.Submit);var state=s.State;
            Assert.That(state.phase,Is.EqualTo("RunFailed"));Assert.That(state.gold,Is.EqualTo(before.gold+1));Assert.That(state.lastStageSettlement,Is.Null);
            Assert.That(state.handLevels.Single(h=>h.handId=="pair").level,Is.EqualTo(1));Assert.That(state.controllers.Any(c=>c.definitionId=="C42"),Is.False);
            Assert.That(state.controllers.Single(c=>c.definitionId=="C45").state.activeColor,Is.EqualTo("blue"));Assert.That(state.controllers.Single(c=>c.definitionId=="C52").state.fundAccrual,Is.Zero);
            Assert.That(state.lastRandomResults.Select(r=>r.kind),Is.EqualTo(new[]{"C53","C45","C42"}));
        }
    }
}
