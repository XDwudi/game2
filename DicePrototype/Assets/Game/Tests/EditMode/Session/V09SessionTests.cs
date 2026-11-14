using System;
using System.Linq;
using NUnit.Framework;
using DiceCraft.Core;

namespace DiceCraft.Tests
{
    /// <summary>
    /// Session rules added by p0.9.0 (docs/specs/v09/RULES_SPEC.md): slots and the O08 service, roll and submission
    /// budgets, the frozen hand context, growth timing, the altar, stage-clear income and the shop discount.
    /// Expected numbers are worked out here from the rule text, never read back from the code under test.
    /// </summary>
    public sealed class V09SessionTests : SessionTestSupport
    {
        private static uint SeedWithFirstTicket(int range, int ticket)
        {
            for (uint seed = 1; seed < 200000; seed++) if (new GameRng(seed).NextIndex(range) == ticket) return seed;
            throw new InvalidOperationException("No seed found for ticket " + ticket);
        }
        /// <summary>Replaces the current hand with the first dice locked at the given pips and physical colours, one roll used, none left.</summary>
        private GameSession Force(GameSession s, int[] pips, string color) => Edit(s, x =>
        {
            x.phase = "Selecting"; x.hand.hasRolled = true; x.hand.rollsUsed = 1; x.hand.rollsRemaining = 0; x.hand.orderedLockedIds.Clear();
            for (int i = 0; i < x.hand.currentResults.Count; i++)
            {
                var r = x.hand.currentResults[i]; bool locked = i < pips.Length;
                x.dice.First(d => d.dieId == r.dieId).faces[0].baseColor = color;
                r.currentFaceId = "F01"; r.currentPip = locked ? pips[i] : 1; r.currentColor = color; r.hasRolled = true; r.remoteOverride = true; r.isLocked = locked;
                if (locked) x.hand.orderedLockedIds.Add(r.dieId);
            }
        });
        private static readonly int[] Fours = { 4, 4, 4, 4, 4 };
        private static readonly string[] Reds = { "red", "red", "red", "red", "red" };
        private static long Growth(GameSession s, string id) => s.State.controllers.Single(c => c.definitionId == id).state.growth;
        private string Enchanted(RunState state, string id, string enchantment)
        {
            string offer = PutOffer(state, "controller", id);
            state.shop.offers.Single(o => o.offerId == offer).controller.enchantmentId = enchantment;
            return offer;
        }

        [Test] public void V09_RunStartsWithFiveSlotsAndTheSlotServiceRaisesThemToSevenForEightThenFourteen()
        {
            Assert.That(New().State.controllerSlots, Is.EqualTo(5));
            string offer = null;
            var s = Edit(Shop(), x => { x.gold = 100; offer = PutOffer(x, "service", "O08"); });
            Assert.That(s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(8));
            Do(s, CommandKind.Buy, c => c.offerId = offer);
            Assert.That(s.State.controllerSlots, Is.EqualTo(6)); Assert.That(s.State.controllerSlotPurchases, Is.EqualTo(1)); Assert.That(s.State.gold, Is.EqualTo(92));
            s = Edit(s, x => offer = PutOffer(x, "service", "O08"));
            Assert.That(s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(14));
            Do(s, CommandKind.Buy, c => c.offerId = offer);
            Assert.That(s.State.controllerSlots, Is.EqualTo(7)); Assert.That(s.State.gold, Is.EqualTo(78));
            s = Edit(s, x => offer = PutOffer(x, "service", "O08"));
            var third = Command(s, CommandKind.Buy); third.offerId = offer; var before = s.State;
            Assert.That(s.Execute(third).accepted, Is.False, "Seven ordinary slots is the limit."); AssertUnchanged(before, s.State);
            Assert.That(new GameSession(cfg, new MemorySaveStore(), s.State).State.controllerSlots, Is.EqualTo(7), "The purchased slots survive restore.");
        }

        [Test] public void V09_ASixthOrdinaryControllerNeedsAPurchasedSlot()
        {
            string card = null, slot = null;
            var s = Edit(Shop(), x => { x.gold = 100; Equip(x, "C01", "C02", "C03", "C06", "C08"); card = PutOffer(x, "controller", "C13"); slot = PutOffer(x, "service", "O08"); });
            var blocked = Command(s, CommandKind.Buy); blocked.offerId = card; var before = s.State;
            Assert.That(s.Execute(blocked).accepted, Is.False); AssertUnchanged(before, s.State);
            Do(s, CommandKind.Buy, c => c.offerId = slot); Do(s, CommandKind.Buy, c => c.offerId = card);
            Assert.That(s.State.controllers.Count, Is.EqualTo(6)); Assert.That(s.OrdinaryControllerSlotsUsed, Is.EqualTo(6));
        }

        [Test] public void V09_SlotCountMustMatchPurchasesInANormalSave()
        {
            var state = New().State; state.controllerSlots = 6;
            Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception, "A sixth slot without a recorded purchase is not a legal save.");
            state.controllerSlotPurchases = 1;
            Assert.That(new GameSession(cfg, new MemorySaveStore(), state).State.controllerSlots, Is.EqualTo(6));
            state.controllerSlots = 8; state.controllerSlotPurchases = 3;
            Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception, "Eight ordinary slots exceed the limit.");
        }

        [TestCase(new[] { "C78" }, 4)] [TestCase(new[] { "C41", "C78" }, 5)] [TestCase(new[] { "C78", "C13" }, 4)] [TestCase(new[] { "C13" }, 3)]
        public void V09_C78AddsARollPerSource(string[] controllers, int rolls)
        {
            var s = Ready(controllers);
            Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(rolls));
        }

        [TestCase(new[] { "C79" }, 4)] [TestCase(new[] { "C101" }, 2)] [TestCase(new[] { "C41", "C101" }, 1)] [TestCase(new[] { "C41", "C101", "C79" }, 2)]
        [TestCase(new[] { "C101", "C41", "C79" }, 4)] [TestCase(new[] { "C13" }, 3)]
        public void V09_C79AndC101ChangeSubmissionsPerSourceWithAFloorOfOne(string[] controllers, int submissions)
        {
            var s = Ready(controllers);
            Assert.That(s.State.stage.remainingSubmissions, Is.EqualTo(submissions));
        }

        [Test] public void V09_C101AndTwoMimicsNeverDropBelowOneSubmission()
        {
            var s = Edit(New(), x => { Equip(x, "C41", "C41", "C101"); });
            Do(s, CommandKind.StartStage);
            Assert.That(s.State.stage.remainingSubmissions, Is.EqualTo(1), "3 - 3 would be 0; a stage always keeps one submission.");
        }

        [Test] public void V09_SubmissionFreezesRollsSubmissionsPermanentDiceAndPoolDice()
        {
            var s = Selecting(new[] { 6, 6, 3, 2, 1, 5, 4 }, null, null, 3);
            Do(s, CommandKind.Submit);
            var snapshot = s.State.lastSubmission.snapshot;
            Assert.That(snapshot.rollsUsed, Is.EqualTo(1)); Assert.That(snapshot.submissionsRemainingBefore, Is.EqualTo(3)); Assert.That(snapshot.permanentDiceCount, Is.EqualTo(7));
            Assert.That(snapshot.dice.Select(d => d.dieId), Is.EqualTo(new[] { "D001", "D002", "D003" }));
            Assert.That(snapshot.poolDice.Select(d => d.dieId), Is.EqualTo(new[] { "D004", "D005", "D006", "D007" }));
            Assert.That(snapshot.poolDice.Select(d => d.currentPip), Is.EqualTo(new[] { 2, 1, 5, 4 }));
            Assert.That(snapshot.poolDice.All(d => d.currentColor == "blue" && d.faceId == "F01"), Is.True);
        }

        [Test] public void V09_PoolCardsScoreFromTheDiceLeftBehind()
        {
            // Submit 6,6 blue; leave 6,5,1 in the pool. C70: +6 x 3. C72: +(6+5+1) x 2. C71: x1.5 for the one pool six. C74: pool 3 >= 2 submitted, x3.
            var s = Selecting(new[] { 6, 6, 6, 5, 1 }, null, new[] { "C70", "C72", "C71", "C74" }, 2);
            Do(s, CommandKind.Submit);
            var b = s.State.lastSubmission.breakdown;
            Assert.That(b.baseTotal, Is.EqualTo((12 + 18 + 24).ToString()));
            Assert.That(b.controllerMultiplierNumerator, Is.EqualTo("9")); Assert.That(b.controllerMultiplierDenominator, Is.EqualTo("2"));
            // 54 x pair 2 x all-blue 3 x 9/2 = 1458
            Assert.That(b.score, Is.EqualTo("1458"));
        }

        [Test] public void V09_SubmitGrowthUsesTheOldValueThenGrowsAndC54ResetsOnADifferentHand()
        {
            var s = Selecting(Fours, Reds, new[] { "C55", "C54", "C56", "C57" });
            Do(s, CommandKind.Submit);
            // 20 pips, five of a kind x12, all red x2, red group of five x5; every growth card still at its start value.
            Assert.That(s.State.lastSubmission.breakdown.score, Is.EqualTo("2400"));
            Assert.That(Growth(s, "C55"), Is.EqualTo(2)); Assert.That(Growth(s, "C54"), Is.Zero, "No previous hand: C54 stays at x1.");
            Assert.That(Growth(s, "C56"), Is.Zero, "Red dice were submitted."); Assert.That(Growth(s, "C57"), Is.EqualTo(3));

            s = Force(s, Fours, "red"); Do(s, CommandKind.Submit);
            // base 20 + C55 growth 2 = 22; x12 x2 x5 = 2640; C57 x23/20 = 3036. C54 only grows after this hand.
            Assert.That(s.State.lastSubmission.breakdown.score, Is.EqualTo("3036"));
            Assert.That(Growth(s, "C55"), Is.EqualTo(4)); Assert.That(Growth(s, "C54"), Is.EqualTo(3)); Assert.That(Growth(s, "C57"), Is.EqualTo(6));

            s = Force(s, new[] { 6, 6, 5, 5, 3 }, "blue"); var result = Do(s, CommandKind.Submit);
            // base 25 + 4 = 29; two pair x3; all blue x3 = 261; C54 x23/20; C57 x26/20 -> floor(261 x 598 / 400) = 390.
            Assert.That(s.State.lastSubmission.breakdown.score, Is.EqualTo("390"));
            Assert.That(Growth(s, "C54"), Is.Zero, "A different primary hand resets C54.");
            Assert.That(Growth(s, "C55"), Is.EqualTo(6)); Assert.That(Growth(s, "C56"), Is.EqualTo(2)); Assert.That(Growth(s, "C57"), Is.EqualTo(6));
            Assert.That(result.receipt.effects.Any(e => e.kind == "ControllerGrowthReset" && e.sourceId == "C54" && e.oldValue == 3 && e.newValue == 0), Is.True);
        }

        [Test] public void V09_MimicUsesTheOwnersGrowthButOnlyTheRealCardGrows()
        {
            var s = Selecting(Fours, Reds, new[] { "C41", "C55" });
            Do(s, CommandKind.Submit);
            Assert.That(s.State.lastSubmission.breakdown.score, Is.EqualTo("2400")); Assert.That(Growth(s, "C55"), Is.EqualTo(2), "One real card, one growth step.");
            s = Force(s, Fours, "red"); Do(s, CommandKind.Submit);
            // base 20 + 2 (real) + 2 (mimic reads the same value) = 24; x120 = 2880.
            Assert.That(s.State.lastSubmission.breakdown.score, Is.EqualTo("2880")); Assert.That(Growth(s, "C55"), Is.EqualTo(4));
            Assert.That(s.State.controllers.Single(c => c.definitionId == "C41").state.growth, Is.Zero);
        }

        [Test] public void V09_C62GrowsOnEveryRollAndC90OnEveryGlassBreak()
        {
            var s = Ready("C62"); Do(s, CommandKind.Roll); Assert.That(Growth(s, "C62"), Is.EqualTo(1));
            Do(s, CommandKind.Roll); Assert.That(Growth(s, "C62"), Is.EqualTo(2));

            var broken = Edit(Ready("C90"), x => { x.dice[0].isGlass = true; x.effectRngState = SeedWithFirstTicket(100, 0); });
            Do(broken, CommandKind.Roll);
            Assert.That(broken.State.dice.Count, Is.EqualTo(4)); Assert.That(Growth(broken, "C90"), Is.EqualTo(15), "+0.75 per broken glass die");
            var kept = Edit(Ready("C90"), x => { x.dice[0].isGlass = true; x.effectRngState = SeedWithFirstTicket(100, 99); });
            Do(kept, CommandKind.Roll);
            Assert.That(kept.State.dice.Count, Is.EqualTo(5)); Assert.That(Growth(kept, "C90"), Is.Zero);
        }

        [Test] public void V09_StageClearGrowsC58C59PaysC95WidensInterestWithC96AndRaisesResaleWithC99()
        {
            var s = Selecting(Fours, Reds, new[] { "C58", "C59", "C99", "C95", "C96" });
            s = Edit(s, x => x.gold = 40); Do(s, CommandKind.Submit); Do(s, CommandKind.EndStage);
            var r = s.State.lastStageSettlement;
            Assert.That(r.interestPrincipalCap, Is.EqualTo(50)); Assert.That(r.interestPrincipal, Is.EqualTo(40)); Assert.That(r.interest, Is.EqualTo(8));
            Assert.That(r.controllerIncome, Is.EqualTo(3)); Assert.That(r.unusedSubmissionReward, Is.EqualTo(2));
            Assert.That(r.goldAfter, Is.EqualTo(40 + 5 + 2 + 8 + 3)); Assert.That(s.State.gold, Is.EqualTo(58));
            Assert.That(Growth(s, "C58"), Is.EqualTo(4), "+0.2 per cleared stage"); Assert.That(Growth(s, "C59"), Is.EqualTo(8), "+4 for each of the two unused submissions");
            Assert.That(s.State.controllers.All(c => c.state.saleBonus == 1), Is.True, "C99 raises every owned controller, itself included.");
            Do(s, CommandKind.Continue);
            string id = s.State.controllers.Single(c => c.definitionId == "C58").instanceId;
            Assert.That(s.GetControllerSalePrice(id), Is.EqualTo(8 / 2 + 1));
            Do(s, CommandKind.Sell, c => c.itemInstanceId = id); Assert.That(s.State.gold, Is.EqualTo(63));
            Assert.That(new GameSession(cfg, new MemorySaveStore(), s.State).State.gold, Is.EqualTo(63), "The settlement with the new income fields restores.");
        }

        [Test] public void V09_C60GrowsOnDieUpgradesAndC61OnEveryLevelGained()
        {
            string upgrade = null, hand = null;
            var s = Edit(Shop(), x => { x.gold = 100; Equip(x, "C60", "C61"); upgrade = PutOffer(x, "die_upgrade", "U01"); hand = PutOffer(x, "hand_upgrade", "pair"); });
            Do(s, CommandKind.Buy, c => { c.offerId = upgrade; c.targetDieId = s.State.dice[0].dieId; c.targetFaceId = "F02"; });
            Assert.That(Growth(s, "C60"), Is.EqualTo(2)); Assert.That(Growth(s, "C61"), Is.Zero);
            Do(s, CommandKind.Buy, c => c.offerId = hand); Assert.That(Growth(s, "C61"), Is.EqualTo(2));
            Do(s, CommandKind.Study, c => c.handId = "trips"); Assert.That(Growth(s, "C61"), Is.EqualTo(4));
            Assert.That(Growth(s, "C60"), Is.EqualTo(2), "Hand upgrades are not die upgrades.");

            var upgraded = Edit(Selecting(Fours, Reds, new[] { "C43", "C61" }), x => x.previousPrimaryHandId = "five_kind");
            Do(upgraded, CommandKind.Submit);
            Assert.That(upgraded.State.handLevels.Single(h => h.handId == "five_kind").level, Is.EqualTo(1));
            Assert.That(Growth(upgraded, "C61"), Is.EqualTo(2), "The level granted by C43 counts.");
        }

        [Test] public void V09_C97PaysWhenOneSubmissionReachesHalfTheTargetAtMostTwiceAStage()
        {
            var weak = Selecting(new[] { 1 }, new[] { "blue" }, new[] { "C97" });
            Do(weak, CommandKind.Submit);
            // 1 pip x all blue 3 = 3, far below half of 300.
            Assert.That(weak.State.gold, Is.Zero); Assert.That(weak.State.stage.warIncomeCount, Is.Zero);

            var s = Selecting(Fours, Reds, new[] { "C97" });
            Do(s, CommandKind.Submit); Assert.That(s.State.gold, Is.EqualTo(3)); Assert.That(s.State.stage.warIncomeCount, Is.EqualTo(1));
            s = Force(s, Fours, "red"); Do(s, CommandKind.Submit); Assert.That(s.State.gold, Is.EqualTo(6));
            s = Force(s, Fours, "red"); Do(s, CommandKind.Submit);
            Assert.That(s.State.stage.warIncomeCount, Is.EqualTo(2), "The third qualifying submission pays nothing.");
            Assert.That(s.State.lastStageSettlement.goldBefore, Is.EqualTo(6));
        }

        [Test] public void V09_C97ComparesAgainstExactlyHalfTheTarget()
        {
            // Stage 1 target 300. One blue 5 with C50 (+20): (5 + 20) x 3 = 75 < 150. Two blue 5s: (10 + 20) x pair 2 x 3 = 180 >= 150.
            var below = Selecting(new[] { 5 }, new[] { "blue" }, new[] { "C97", "C50" }); Do(below, CommandKind.Submit);
            Assert.That(below.State.lastSubmission.breakdown.score, Is.EqualTo("75")); Assert.That(below.State.gold, Is.Zero);
            // Exactly 150: one blue 5 with C50 and C46 (x2): 25 x 3 x 2 = 150.
            var exact = Selecting(new[] { 5 }, new[] { "blue" }, new[] { "C97", "C50", "C46" }); Do(exact, CommandKind.Submit);
            Assert.That(exact.State.lastSubmission.breakdown.score, Is.EqualTo("150")); Assert.That(exact.State.gold, Is.EqualTo(3));
        }

        [Test] public void V09_C98LowersControllerAndDieUpgradePricesPerSourceWithAFloorOfOne()
        {
            string cheap = null, dear = null, smooth = null, gold = null, swap = null;
            var s = Edit(Shop(), x => { x.gold = 100; Equip(x, "C98", "C02"); cheap = PutOffer(x, "controller", "C13"); dear = PutOffer(x, "controller", "C42");
                smooth = PutOffer(x, "die_upgrade", "U01"); gold = PutOffer(x, "die_upgrade", "U06"); swap = Enchanted(x, "C02", "mult2"); });
            Func<string, ShopOffer> offer = id => s.State.shop.offers.Single(o => o.offerId == id);
            Assert.That(s.GetOfferPrice(offer(cheap)), Is.EqualTo(1)); Assert.That(s.GetOfferPrice(offer(dear)), Is.EqualTo(5));
            Assert.That(s.GetOfferPrice(offer(smooth)), Is.EqualTo(1), "1 - 1 stays at the floor of 1."); Assert.That(s.GetOfferPrice(offer(gold)), Is.EqualTo(2));
            // Replacement: list 7 + mult2 premium 6 - 1 = 12; the old C02 still trades in at half of 7.
            Assert.That(s.GetOfferPrice(offer(swap)), Is.EqualTo(12)); Assert.That(s.GetOfferTradeIn(offer(swap)), Is.EqualTo(3)); Assert.That(s.GetOfferNetPrice(offer(swap)), Is.EqualTo(9));

            var twice = Edit(Shop(), x => { x.gold = 100; Equip(x, "C41", "C98"); cheap = PutOffer(x, "controller", "C13"); dear = PutOffer(x, "controller", "C42"); });
            Assert.That(twice.GetOfferPrice(twice.State.shop.offers.Single(o => o.offerId == cheap)), Is.EqualTo(1));
            Assert.That(twice.GetOfferPrice(twice.State.shop.offers.Single(o => o.offerId == dear)), Is.EqualTo(4), "A mimic of C98 is a second source.");
            long before = twice.State.gold; Do(twice, CommandKind.Buy, c => c.offerId = dear); Assert.That(twice.State.gold, Is.EqualTo(before - 4));
        }

        [Test] public void V09_AltarDestroysItsRightNeighbourAtStageStartAndGainsATenthOfItsPrice()
        {
            var s = Edit(New(), x => Equip(x, "C13", "C100", "C50", "C46"));
            var start = Do(s, CommandKind.StartStage);
            Assert.That(s.State.controllers.Select(c => c.definitionId), Is.EqualTo(new[] { "C13", "C100", "C46" }));
            Assert.That(Growth(s, "C100"), Is.EqualTo(8), "C50 costs 4: +0.4 is eight steps of 0.05.");
            Assert.That(start.receipt.effects.Count(e => e.kind == "ControllerSacrificed"), Is.EqualTo(1));
            Assert.That(s.State.stage.controllerOrderSnapshot.Length, Is.EqualTo(3), "Order is frozen after the sacrifice.");

            var last = Edit(New(), x => Equip(x, "C13", "C100")); Do(last, CommandKind.StartStage);
            Assert.That(last.State.controllers.Count, Is.EqualTo(2)); Assert.That(Growth(last, "C100"), Is.Zero, "The rightmost altar has nothing to destroy.");
        }

        [Test] public void V09_AnAltarDestroyedByAnotherAltarDoesNotAct()
        {
            var s = Edit(New(), x => Equip(x, "C100", "C100", "C13"));
            Do(s, CommandKind.StartStage);
            // The left altar destroys the second altar (price 6: +12). The survivor's new right neighbour is C13, but each altar acts once.
            Assert.That(s.State.controllers.Select(c => c.definitionId), Is.EqualTo(new[] { "C100", "C13" }));
            Assert.That(s.State.controllers[0].state.growth, Is.EqualTo(12));
        }

        [Test] public void V09_MimicOfTheAltarSharesItsMultiplierButNeverSacrifices()
        {
            var s = Edit(New(), x => Equip(x, "C41", "C100", "C13"));
            Do(s, CommandKind.StartStage);
            Assert.That(s.State.controllers.Select(c => c.definitionId), Is.EqualTo(new[] { "C41", "C100" }), "Only the real altar destroyed its neighbour.");
            Assert.That(Growth(s, "C100"), Is.EqualTo(4), "C13 costs 2.");
            s = Force(s, Fours, "red"); Do(s, CommandKind.Submit);
            // 2400 x (24/20) x (24/20) = 3456
            Assert.That(s.State.lastSubmission.breakdown.score, Is.EqualTo("3456"));
        }

        [Test] public void V09_GrowthBelongsOnlyToGrowthCardsAndNeverToShopOffers()
        {
            var state = Edit(New(), x => Equip(x, "C13")).State; state.isDebug = false; state.debugWorkspace = false;
            state.controllers[0].state.growth = 5;
            Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception, "C13 has no growth value.");

            string offer = null; var shop = Edit(Shop(), x => { offer = PutOffer(x, "controller", "C55"); }).State;
            shop.shop.offers.Single(o => o.offerId == offer).controller.state.growth = 2;
            Assert.That(() => new GameSession(cfg, new MemorySaveStore(), shop), Throws.Exception, "An offer starts at zero growth.");
        }

        [Test] public void V09_SellingAGrowthCardAndBuyingItAgainStartsFromZero()
        {
            string offer = null;
            var s = Edit(Shop(), x => { x.gold = 100; Equip(x, "C55"); x.controllers[0].state.growth = 40; offer = PutOffer(x, "controller", "C55"); });
            Do(s, CommandKind.Sell, c => c.itemInstanceId = s.State.controllers.Single().instanceId);
            Do(s, CommandKind.Buy, c => c.offerId = offer);
            Assert.That(Growth(s, "C55"), Is.Zero);
        }
    }
}
