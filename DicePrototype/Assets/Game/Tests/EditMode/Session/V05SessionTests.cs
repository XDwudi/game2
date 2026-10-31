using System;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;

namespace DiceCraft.Tests
{
    public sealed class V05SessionTests : SessionTestSupport
    {
        private sealed class FaultStore : ISaveStore
        {
            public bool fail = true;
            public RunState attempted;
            public int writes;
            public void Save(RunState snapshot) { writes++; attempted = snapshot.Clone(); if (fail) throw new IOException("Injected v05 write failure"); }
        }

        private GameSession Qualified(params string[] controllers)
        {
            var s = Selecting(new[] { 4, 4, 4, 4, 4 }, new[] { "red", "red", "red", "red", "red" }, controllers);
            Do(s, CommandKind.Submit);
            Assert.That(s.State.stage.qualified, Is.True);
            Assert.That(s.State.phase, Is.EqualTo("HandReady"));
            return s;
        }

        private string EnchantedOffer(RunState state, string id, string enchantment)
        {
            string offer = PutOffer(state, "controller", id);
            state.shop.offers.Single(o => o.offerId == offer).controller.enchantmentId = enchantment;
            return offer;
        }

        [Test] public void V05_QualifiedDoesNotPayOrCreateShopAndReadQuotesArePure()
        {
            var s = Qualified("C01"); var before = s.State;
            Assert.That(before.gold, Is.EqualTo(1)); Assert.That(before.shop, Is.Null); Assert.That(before.stage.settled, Is.False);
            Assert.That(before.stage.remainingSubmissions, Is.EqualTo(2)); Assert.That(before.stage.submissionOrdinal, Is.EqualTo(1));
            for (int i = 0; i < 25; i++)
            {
                var quote = s.PreviewStageSettlement(); Assert.That(quote.goldAfter, Is.EqualTo(8)); quote.goldAfter = 99999;
            }
            AssertUnchanged(before, s.State); Assert.That(s.State.stage.submissionOrdinal, Is.EqualTo(1));
            Do(s, CommandKind.EndStage); Assert.That(s.State.gold, Is.EqualTo(8));
        }

        [Test] public void V05_ContinueAfterQualifiedKeepsOrdinalAndPaysOnlyRemainingAtEnd()
        {
            var s = Qualified("C07"); int score = (int)ExactScoreMath.Parse(s.State.stage.score);
            Do(s, CommandKind.Roll); Lock(s, "D001"); Do(s, CommandKind.Submit);
            Assert.That(s.State.stage.qualified, Is.True); Assert.That(s.State.gold, Is.EqualTo(0));
            Assert.That(s.State.stage.submissionOrdinal, Is.EqualTo(2)); Assert.That(ExactScoreMath.Parse(s.State.stage.score) > score, Is.True);
            Assert.That(s.State.lastEffects.Any(e => e.sourceId == "C07"), Is.False);
            Do(s, CommandKind.EndStage); Assert.That(s.State.gold, Is.EqualTo(6));
        }

        [Test] public void V05_EndStageWithLockedHandDoesNotSubmitOrRevertUsedActions()
        {
            var s = Qualified("C38"); Do(s, CommandKind.Roll); Lock(s, "D002");
            var before = s.State; var command = Command(s, CommandKind.EndStage);
            Assert.That(s.Execute(command).accepted, Is.True); var after = s.State;
            Assert.That(after.stage.score, Is.EqualTo(before.stage.score)); Assert.That(after.stage.remainingSubmissions, Is.EqualTo(2));
            Assert.That(after.stage.submissionOrdinal, Is.EqualTo(1)); Assert.That(after.diceRngState, Is.EqualTo(before.diceRngState));
            Assert.That(after.dice.Select(d => d.dataRevision), Is.EqualTo(before.dice.Select(d => d.dataRevision)));
            Assert.That(after.lastEffects.Any(e => e.sourceId == "C38"), Is.False);
            Assert.That(s.Execute(command).duplicate, Is.True); AssertUnchanged(after, s.State);
            var duplicate = Command(s, CommandKind.EndStage); Assert.That(s.Execute(duplicate).accepted, Is.False);
        }

        [Test] public void V05_EndStageRejectsUnqualifiedPendingBusyAndSavePending()
        {
            var unqualified = Ready(); Assert.That(unqualified.CanEndStage, Is.False); Assert.That(unqualified.Execute(Command(unqualified, CommandKind.EndStage)).accepted, Is.False);
            var s = Qualified(); Do(s, CommandKind.Roll); s.TogglePending("D001");
            Assert.That(s.CanEndStage, Is.False); Assert.That(s.Execute(Command(s, CommandKind.EndStage)).accepted, Is.False);
            s.ClearPending(); s.Busy = true; Assert.That(s.CanEndStage, Is.False); Assert.That(s.Execute(Command(s, CommandKind.EndStage)).accepted, Is.False);
            s.Busy = false; Assert.That(s.CanEndStage, Is.True);
            var store = new FaultStore(); s = new GameSession(cfg, store, s.State); var command = Command(s, CommandKind.EndStage);
            Assert.That(s.Execute(command).pendingSave, Is.True); Assert.That(s.CanEndStage, Is.False); Assert.That(s.State.gold, Is.EqualTo(0));
            long expected = store.attempted.gold; store.fail = false; Assert.That(s.RetrySave().accepted, Is.True);
            Assert.That(s.State.gold, Is.EqualTo(expected)); Assert.That(store.writes, Is.EqualTo(2)); Assert.That(s.Execute(command).duplicate, Is.True);
        }

        [Test] public void V05_FinalSubmissionAutoEndsAlreadyQualifiedStage()
        {
            var s = Qualified();
            for (int i = 0; i < 2; i++) { Do(s, CommandKind.Roll); Lock(s, "D001"); Do(s, CommandKind.Submit); }
            Assert.That(s.State.phase, Is.EqualTo("StageSettlement")); Assert.That(s.State.stage.remainingSubmissions, Is.Zero);
            Assert.That(s.State.lastStageSettlement.unusedSubmissionReward, Is.Zero); Assert.That(s.State.gold, Is.EqualTo(5));
        }

        [TestCase(false, 112)] [TestCase(true, 117)] public void V05_InterestCapsPrincipalNotBalance(bool pig, int goldAfter)
        {
            var s = Qualified(pig ? new[] { "C01" } : Array.Empty<string>()); s = Edit(s, x => x.gold = 100);
            var preview = s.PreviewStageSettlement(); Assert.That(preview.interestPrincipal, Is.EqualTo(50)); Assert.That(preview.goldAfter, Is.EqualTo(goldAfter));
            Do(s, CommandKind.EndStage); Assert.That(s.State.gold, Is.EqualTo(goldAfter));
        }

        [Test] public void V05_QualifiedI06AddsRewardAndRestoreDoesNotPay()
        {
            string item = null; var s = Edit(Qualified(), x => item = AddItem(x, "I06"));
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = item); var before = s.State;
            for (int i = 0; i < 5; i++) s = new GameSession(cfg, new MemorySaveStore(), s.State);
            AssertUnchanged(before, s.State); Assert.That(s.State.stage.qualified, Is.True);
            Do(s, CommandKind.EndStage); Assert.That(s.State.gold, Is.EqualTo(8));
        }

        [TestCase(false)] [TestCase(true)] public void V05_AllGlassLostAfterQualificationUsesSingleSettlementAndFinalPriority(bool final)
        {
            var s = Qualified(); uint rng = 1; while (new GameRng(rng).NextIndex(100) >= 15) rng++;
            s = Edit(s, x => {
                x.dice.RemoveRange(1, x.dice.Count - 1); x.hand.currentResults.RemoveRange(1, x.hand.currentResults.Count - 1);
                x.dice[0].isGlass = true; x.effectRngState = rng;
                if (final) { x.stage.stageIndex = 23; x.stage.stageId = cfg.stages[23].stageId; }
            });
            Do(s, CommandKind.Roll); Assert.That(s.State.phase, Is.EqualTo("StageSettlement")); Assert.That(s.State.dice, Is.Empty);
            Assert.That(s.State.lastStageSettlement.remainingSubmissions, Is.EqualTo(2)); var after = s.State;
            Do(s, CommandKind.Continue); Assert.That(s.State.phase, Is.EqualTo(final ? "RunWon" : "RunFailed")); Assert.That(s.State.gold, Is.EqualTo(after.gold));
        }

        [Test] public void V05_BudgetsAndCapacityUseIndependentControllerRules()
        {
            var s = Edit(New(), x => { Equip(x, "C01", "C02", "C03", "C21", "C22"); x.controllers[0].enchantmentId = "submit1"; x.controllers[1].enchantmentId = "submit1"; x.controllers[2].enchantmentId = "roll1"; x.controllers[3].enchantmentId = "roll1"; }); Do(s, CommandKind.StartStage);
            Assert.That(s.State.stage.remainingSubmissions, Is.EqualTo(5)); Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(5));
            Assert.That(s.State.stage.capacity, Is.EqualTo(8)); Assert.That(s.State.dice.Count, Is.EqualTo(5));
            s = Ready("C21", "C22", "C09"); Assert.That(s.State.stage.capacity, Is.EqualTo(7));
        }

        [Test] public void V05_C26ThenC38LeavesFiveRedOneBlueAndScoreFrozen()
        {
            var s = Selecting(new[] { 2 }, new[] { "blue" }, new[] { "C26", "C38" });
            s = Edit(s, x => { x.hand.currentResults[0].currentFaceId = "F02"; foreach (var c in x.controllers) c.enchantmentId = "base10"; });
            var expected = s.PreviewScore(); var before = s.State;
            for (int i = 0; i < 5; i++) Assert.That(s.PreviewSubmissionEffects().Length, Is.EqualTo(2));
            AssertUnchanged(before, s.State); Do(s, CommandKind.Submit);
            var die = s.State.dice[0]; Assert.That(die.faces.Count(f => f.baseColor == "red"), Is.EqualTo(5)); Assert.That(die.faces[1].baseColor, Is.EqualTo("blue"));
            Assert.That(s.State.lastSubmission.breakdown.score, Is.EqualTo(expected.score)); Assert.That(expected.enchantBaseCount, Is.EqualTo(2));
        }

        [TestCase(1)] [TestCase(6)] public void V05_GrowthShrinkCancelBeforeSingleClampAtBothBoundaries(int pip)
        {
            var s = Selecting(new[] { 2, 3 }, controllers: new[] { "C36", "C37" });
            s = Edit(s, x => { foreach (var d in x.dice) d.faces[0].permanentPip = pip; foreach (var c in x.controllers) c.enchantmentId = "base10"; });
            var expected = s.PreviewScore(); Do(s, CommandKind.Submit);
            Assert.That(s.State.dice.Select(d => d.faces[0].permanentPip), Is.EqualTo(new[] { pip, pip }));
            Assert.That(expected.enchantBaseCount, Is.EqualTo(2)); Assert.That(s.State.lastEffects.Count(e => e.kind == "FaceValueChanged"), Is.EqualTo(2));
        }

        [Test] public void V05_CopyThenGrowthUsesCopiedTargetFaceValueAndSourceIsIndependent()
        {
            var s = Selecting(new[] { 1, 2 }, new[]{"red","blue"}, new[] { "C07", "C36", "C38" });
            s = Edit(s, x => { x.hand.currentResults[1].currentFaceId = "F02"; x.dice[0].faces[1].permanentPip = 4; x.dice[0].faces[1].baseColor = "red"; x.dice[1].faces[1].permanentPip = 1; });
            Do(s, CommandKind.Submit); var state = s.State;
            Assert.That(state.dice[1].faces[1].permanentPip, Is.EqualTo(5)); Assert.That(state.dice[0].faces[1].permanentPip, Is.EqualTo(4));
            Assert.That(state.dice[0].faces[0].baseColor, Is.EqualTo("blue")); Assert.That(state.dice[1].faces[0].baseColor, Is.EqualTo("red"));
            Assert.That(state.lastEffects.Select(e => e.sourceId), Is.EqualTo(new[] { "C07", "C36", "C36", "C38", "C38" }));
        }

        [Test] public void V05_RemoteGrowthChangesFrozenPhysicalFaceAndNeverOverridePip()
        {
            var s = Selecting(new[] { 6, 5 }, controllers: new[] { "C36" });
            s = Edit(s, x => { foreach (var r in x.hand.currentResults) { r.currentFaceId = "F02"; r.remoteOverride = true; } });
            Do(s, CommandKind.Submit); Assert.That(s.State.dice.Select(d => d.faces[1].permanentPip), Is.EqualTo(new[] { 3, 3 }));
            Assert.That(s.State.lastSubmission.breakdown.effectivePips, Is.EqualTo(new[] { 6, 5 }));
        }

        [TestCase("I04")] [TestCase("I05")] public void V05_BiasItemRequiresFutureLegalRollOnMatchingUnlockedDie(string id)
        {
            string item = null; var s = Edit(Selecting(new[] { 1, 2 }, lockedCount: 1), x => item = AddItem(x, id));
            var command = Command(s, CommandKind.UseItem); command.itemInstanceId = item; var before = s.State;
            Assert.That(s.Execute(command).accepted, Is.False); AssertUnchanged(before, s.State);
            s = Edit(s, x => { x.hand.rollsRemaining = 1; foreach (var f in x.dice[1].faces) f.permanentPip = 3; });
            command = Command(s, CommandKind.UseItem); command.itemInstanceId = item; Assert.That(s.Execute(command).accepted, Is.False);
        }

        [Test] public void V05_SlotlessFitsBeyondFourAndReplaceUsesFinalEquipment()
        {
            string offer = null;
            var s = Edit(Shop(), x => { x.gold = 100; Equip(x, "C01", "C02", "C03", "C05"); x.isDebug = false; offer = EnchantedOffer(x, "C06", "slotless"); });
            Do(s, CommandKind.Buy, c => c.offerId = offer); Assert.That(s.State.controllers.Count, Is.EqualTo(5)); Assert.That(s.OrdinaryControllerSlotsUsed, Is.EqualTo(4));
            s = Edit(s, x => { x.isDebug = false; offer = EnchantedOffer(x, "C06", "base10"); });
            var before = s.State; var bad = Command(s, CommandKind.Buy); bad.offerId = offer;
            Assert.That(s.Execute(bad).accepted, Is.False); AssertUnchanged(before, s.State);
            Assert.That(s.State.controllers.Single(c => c.definitionId == "C06").enchantmentId, Is.EqualTo("slotless"));
            Do(s, CommandKind.Sell, c => c.itemInstanceId = s.State.controllers.Single(v => v.definitionId == "C01").instanceId);
            var quoted = s.State.shop.offers.Single(o => o.offerId == offer); Assert.That(s.GetOfferPrice(quoted), Is.EqualTo(6)); Assert.That(s.GetOfferNetPrice(quoted), Is.EqualTo(5));
            long gold = s.State.gold; Do(s, CommandKind.Buy, c => c.offerId = offer);
            Assert.That(s.State.gold, Is.EqualTo(gold - 5)); Assert.That(s.State.controllers.Count, Is.EqualTo(4));
        }

        [Test] public void V05_ReplacingAtTenDoesNotCreateEleventhInstanceAndNewEleventhRejects()
        {
            string offer = null;
            var s = Edit(Shop(), x => { x.gold = 100; Equip(x, "C01", "C02", "C03", "C05", "C06", "C07", "C08", "C09", "C10", "C11"); x.controllerSlots = 4; x.isDebug = false; foreach (var c in x.controllers.Skip(4)) c.enchantmentId = "slotless"; offer = EnchantedOffer(x, "C02", "base10"); });
            Do(s, CommandKind.Buy, c => c.offerId = offer); Assert.That(s.State.controllers.Count, Is.EqualTo(10));
            s = Edit(s, x => { x.isDebug = false; offer = EnchantedOffer(x, "C12", "slotless"); });
            var before = s.State; var bad = Command(s, CommandKind.Buy); bad.offerId = offer;
            Assert.That(s.Execute(bad).accepted, Is.False); AssertUnchanged(before, s.State);
        }

        [Test] public void V05_SellingOldCardBeforeReplacementPaysFullNewPrice()
        {
            string offer = null; var s = Edit(Shop(), x => { x.gold = 100; Equip(x, "C02"); offer = EnchantedOffer(x, "C02", "mult2"); });
            Do(s, CommandKind.Sell, c => c.itemInstanceId = s.State.controllers.Single().instanceId);
            var quoted = s.State.shop.offers.Single(o => o.offerId == offer); Assert.That(s.GetOfferTradeIn(quoted), Is.Zero); Assert.That(s.GetOfferNetPrice(quoted), Is.EqualTo(10));
            Do(s, CommandKind.Buy, c => c.offerId = offer); Assert.That(s.State.gold, Is.EqualTo(91));
        }

        [Test] public void V05_ReplacementInsufficientMoneyAndWriteFailureAreAtomic()
        {
            string offer = null; var s = Edit(Shop(), x => { x.gold = 5; Equip(x, "C02"); offer = EnchantedOffer(x, "C02", "mult2"); });
            var before = s.State; var command = Command(s, CommandKind.Buy); command.offerId = offer;
            Assert.That(s.Execute(command).accepted, Is.False); AssertUnchanged(before, s.State);
            var store = new FaultStore(); s = Edit(s, x => x.gold = 100, store); before = s.State;
            command = Command(s, CommandKind.Buy); command.offerId = offer; Assert.That(s.Execute(command).pendingSave, Is.True); AssertUnchanged(before, s.State);
            var expected = store.attempted; store.fail = false; Assert.That(s.RetrySave().accepted, Is.True);
            Assert.That(s.State.controllers.Single().instanceId, Is.EqualTo(expected.controllers.Single().instanceId)); Assert.That(s.State.gold, Is.EqualTo(91));
            Assert.That(s.State.shopRngState, Is.EqualTo(before.shopRngState)); Assert.That(s.Execute(command).duplicate, Is.True);
        }

        [Test] public void V05_ControllerOfferPersistenceAndOriginalInstanceSurviveReadBuyAndReload()
        {
            var s = Edit(Shop(), x => x.gold = 100); var offer = s.State.shop.offers.First(o => o.category == "controller");
            var before = s.State;
            for (int i = 0; i < 20; i++) { s = new GameSession(cfg, new MemorySaveStore(), s.State); s.GetOfferPrice(s.State.shop.offers.First(o => o.offerId == offer.offerId)); }
            AssertUnchanged(before, s.State); Do(s, CommandKind.Buy, c => c.offerId = offer.offerId);
            var owned = s.State.controllers.Single(); Assert.That(owned.instanceId, Is.EqualTo(offer.controller.instanceId)); Assert.That(owned.enchantmentId, Is.EqualTo(offer.controller.enchantmentId));
            owned.enchantmentId = "broken"; Assert.That(s.State.controllers.Single().enchantmentId, Is.EqualTo(offer.controller.enchantmentId));
        }

        [Test] public void V05_C29ReenablesFourSlotStraightStudyWithoutColorHands()
        {
            var s = Edit(Shop(), x => { x.gold = 100; Equip(x, "C09"); }); Assert.That(s.LegalStudyHands(), Does.Not.Contain("straight"));
            string offer = null; s = Edit(s, x => offer = PutOffer(x, "controller", "C29")); Do(s, CommandKind.Buy, c => c.offerId = offer);
            Assert.That(s.LegalStudyHands(), Does.Contain("straight")); Assert.That(s.LegalStudyHands(), Does.Contain("all_red")); Assert.That(s.LegalStudyHands(), Does.Contain("all_blue"));
            Do(s, CommandKind.Study, c => c.handId = "straight");
            Do(s, CommandKind.Sell, c => c.itemInstanceId = s.State.controllers.Single(v => v.definitionId == "C29").instanceId);
            Assert.That(s.LegalStudyHands(), Does.Not.Contain("straight")); Assert.That(s.State.handLevels.Single(h => h.handId == "straight").level, Is.EqualTo(1));
        }

        [Test] public void V05_EightKindRequiresEightPermanentDiceAndCapacityAndLv8StopsSales()
        {
            var s = Edit(Shop(), x => { x.gold = 500; Equip(x, "C21", "C22"); for (int i = 6; i <= 8; i++) x.dice.Add(DiceFactory.CreateStandard(cfg, i)); x.nextDieSerial = 9; });
            Assert.That(s.LegalStudyHands(), Does.Contain("eight_kind"));
            s = Edit(s, x => x.handLevels.Single(h => h.handId == "eight_kind").level = 7);
            Assert.That(s.GetStudyPrice("eight_kind"), Is.EqualTo(13)); Do(s, CommandKind.Study, c => c.handId = "eight_kind");
            Assert.That(s.State.handLevels.Single(h => h.handId == "eight_kind").level, Is.EqualTo(8)); Assert.That(s.LegalStudyHands(), Does.Not.Contain("eight_kind"));
            s = Edit(s, x => { x.handLevels.Single(h => h.handId == "eight_kind").level = 0; x.dice[7].lifetime = "stage_temporary"; x.dice[7].createdStageId = x.stage.stageId; });
            Assert.That(s.LegalStudyHands(), Does.Not.Contain("eight_kind"));
        }

        [Test] public void V05_ProbabilityControllersUseSameFinalVectorAsActualRoll()
        {
            var s = Ready("C34"); Assert.That(s.ProbabilityUnits("D001"), Is.EqualTo(new[] { 116667, 116667, 116667, 216667, 216666, 216666 }));
            var rng = new GameRng(s.State.diceRngState); int draw = rng.NextIndex(1000000); int[] units = s.ProbabilityUnits("D001"); int index = 0, ceiling = units[0]; while (draw >= ceiling) ceiling += units[++index];
            Do(s, CommandKind.Roll); Assert.That(s.State.hand.currentResults.First(r => r.dieId == "D001").currentFaceId, Is.EqualTo("F0" + (index + 1)));
            s = Ready("C34", "C35"); Assert.That(s.ProbabilityUnits("D001"), Is.EqualTo(new[] { 166667, 166667, 166667, 166667, 166666, 166666 }));
        }

        [Test] public void V05_RestoreRejectsMismatchedQualificationIllegalEnchantAndOfferIdentity()
        {
            var s = Qualified(); var state = s.State; state.stage.qualified = false; Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception);
            s = Ready("C05"); state = s.State; state.isDebug = false; state.debugWorkspace = false; state.controllers[0].enchantmentId = "base10"; Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception);
            s = Shop(); state = s.State; state.shop.offers.First(o => o.category == "controller").controller.instanceId = "invalid"; Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception);
            state = s.State; state.baseSubmitCapacity = 6; Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception);
        }

        [TestCase(1u, "C26:none,C13:none,C27:none")]
        [TestCase(0u, "C48:submit1,C27:none,C11:none")]
        [TestCase(20261030u, "C50:slotless,C28:none,C19:slotless")]
        [TestCase(4294967295u, "C34:none,C01:slotless,C15:base10")]
        public void V05_ShopInterleavesIdAndEnchantmentWithFrozenReferenceVectors(uint seed, string expected)
        {
            // v0.6 vectors independently generated by tools/v06_session_vectors.py from Appendix D, not copied from observed C# output.
            var s = Qualified(); Do(s, CommandKind.EndStage);
            s = Edit(s, x => x.shopRngState = new GameRng(seed ^ 0xC8013EA4u).State);
            Do(s, CommandKind.Continue);
            Assert.That(string.Join(",", s.State.shop.offers.Where(o => o.category == "controller").Select(o => o.contentId + ":" + o.controller.enchantmentId)), Is.EqualTo(expected));
        }

        [Test] public void V05_ShopExcludesSlotlessNonSubmitOwnedButCanOfferDifferentScoringEnchant()
        {
            int replacementOffers = 0;
            for (uint seed = 1; seed <= 80; seed++)
            {
                var s = Qualified(); Do(s, CommandKind.EndStage);
                s = Edit(s, x => { Equip(x, "C05", "C02"); foreach (var c in x.controllers) c.enchantmentId = "slotless"; x.shopRngState = new GameRng(seed ^ 0xC8013EA4u).State; });
                Do(s, CommandKind.Continue);
                Assert.That(s.State.shop.offers.Where(o => o.category == "controller" && o.contentId == "C05").All(o => o.controller.enchantmentId == "submit1" || o.controller.enchantmentId == "roll1"), Is.True);
                foreach (var offer in s.State.shop.offers.Where(o => o.category == "controller" && o.contentId == "C02"))
                {
                    replacementOffers++; Assert.That(offer.controller.enchantmentId, Is.EqualTo("base10").Or.EqualTo("mult2").Or.EqualTo("submit1").Or.EqualTo("roll1"));
                }
            }
            Assert.That(replacementOffers, Is.GreaterThan(0), "This fixture must exercise a same-ID replacement, not only exclusion.");
        }
    }
}
