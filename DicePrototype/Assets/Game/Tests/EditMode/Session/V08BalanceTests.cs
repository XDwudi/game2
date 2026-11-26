using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using DiceCraft.Core;

namespace DiceCraft.Tests
{
    /// <summary>
    /// Rules that p0.8.0 added or changed in code (docs/specs/v08/RULES_DELTA.md): half-price resale,
    /// the C47 coefficient, the C51 per-shop growth cap, the configurable C53 table, and the numbers the
    /// balance plan depends on structurally (interest steps, glass, stage curve).
    /// Expected values are computed here from the rule text, never read back from the code under test.
    /// </summary>
    public sealed class V08BalanceTests : SessionTestSupport
    {
        private static ControllerInstance Card(string id, int serial, string enchant = "none")
            => new ControllerInstance { instanceId = "v8-" + serial, definitionId = id, enchantmentId = enchant, acquiredSerial = serial };
        private SubmissionSnapshot Snap(int[] pips, params ControllerInstance[] cards)
            => new SubmissionSnapshot { configVersion = cfg.configVersion, capacity = Math.Max(5, pips.Length), equippedControllers = cards,
                dice = pips.Select((p, i) => new SubmissionDie { dieId = "d" + i, faceId = "F01", currentPip = p, currentColor = "blue", permanentSixFaceSum = 21 }).ToArray() };
        private static uint SeedWithFirstTicket(int range, int ticket)
        {
            for (uint seed = 1; seed < 200000; seed++) if (new GameRng(seed).NextIndex(range) == ticket) return seed;
            throw new InvalidOperationException("No seed found for ticket " + ticket);
        }
        /// <summary>Turns the current hand into five locked red fours, enough to clear the early stages.</summary>
        private GameSession ForceWinningHand(GameSession s) => Edit(s, x =>
        {
            x.phase = "Selecting"; x.hand.hasRolled = true; x.hand.rollsUsed = 1; x.hand.rollsRemaining = 0; x.hand.orderedLockedIds.Clear();
            foreach (var r in x.hand.currentResults) { r.currentFaceId = "F01"; r.currentPip = 4; r.currentColor = "red"; r.hasRolled = true; r.remoteOverride = true; r.isLocked = true; x.hand.orderedLockedIds.Add(r.dieId); }
        });

        [Test] public void V08_SalePriceIsHalfTheListPriceRoundedDownAndNeverBelowOne()
        {
            int checkedPairs = 0;
            foreach (var def in cfg.controllers)
                foreach (var enchant in cfg.enchantments)
                {
                    if (!ControllerRules.IsEnchantmentLegal(def.id, enchant.id, cfg)) continue;
                    int list = def.buyPrice + enchant.buyPremium; long expected = Math.Max(1, list / 2);
                    Assert.That(ControllerRules.SalePrice(Card(def.id, 1, enchant.id), cfg), Is.EqualTo(expected), def.id + ":" + enchant.id);
                    Assert.That(expected, Is.LessThan(list), "Buying and reselling must always lose gold: " + def.id + ":" + enchant.id);
                    checkedPairs++;
                }
            Assert.That(checkedPairs, Is.EqualTo(123 * 6 + 27 * 4)); // p1.1.1: 123 controllers that take a score enchantment, 27 that do not
            Assert.That(ControllerRules.SalePrice(Card("C13", 1), cfg), Is.EqualTo(1));
            Assert.That(ControllerRules.SalePrice(Card("C42", 1), cfg), Is.EqualTo(3));
            Assert.That(ControllerRules.SalePrice(Card("C47", 1, "slotless"), cfg), Is.EqualTo(6));
            var fund = Card("C52", 1); fund.state.fundAccrual = 6; Assert.That(ControllerRules.SalePrice(fund, cfg), Is.EqualTo(2 + 6));
            var other = Card("C50", 1); other.state.fundAccrual = 6; Assert.That(ControllerRules.SalePrice(other, cfg), Is.EqualTo(2), "Only C41 and C52 sell their fund.");
        }

        // p1.1.1 section 4.2: C47 is a bonus of +0.6 per live held card, itself included; it no longer multiplies.
        // One blue 6 at level 0: 6 x (1 + 0.25 + 0.6 n), floored once: n = 1 -> 11.1, 2 -> 14.7, 3 -> 18.3, 4 -> 21.9,
        // 5 -> 25.5, 8 -> 36.3, 10 -> 43.5.
        [TestCase(1, "11")] [TestCase(2, "14")] [TestCase(3, "18")] [TestCase(4, "21")]
        [TestCase(5, "25")] [TestCase(8, "36")] [TestCase(10, "43")]
        public void V08_C47AddsSixTenthsPerHeldController(int instances, string score)
        {
            string[] passive = { "C21", "C22", "C34", "C35", "C52", "C05", "C07", "C36", "C37" };
            var cards = new[] { Card("C47", 1) }.Concat(passive.Take(instances - 1).Select((id, i) => Card(id, i + 2))).ToArray();
            var result = ScoreCalculator.Evaluate(Snap(new[] { 6 }, cards), cfg);
            Assert.That(result.controllerMultiplierNumerator, Is.EqualTo("1")); Assert.That(result.controllerMultiplierDenominator, Is.EqualTo("1"));
            Assert.That(result.additiveEntries.Single(e => e.abilityId == "C47").amount100, Is.EqualTo(60 * instances));
            Assert.That(result.score, Is.EqualTo(score), "6 pips x (1 + 0.25 all-blue + 0.6 per controller)");
        }

        [Test] public void V08_C47CopiedByMimicAppliesTwiceButCountsEachInstanceOnce()
        {
            // Four cards: the real C47 and its mimic each add 4 x 0.6 = 2.4 (the mimic is one card, its copied ability is
            // not a fifth). 6 x (1 + 0.25 + 4.8) = 36.3 -> 36.
            var cards = new[] { Card("C41", 1), Card("C47", 2), Card("C21", 3), Card("C22", 4) };
            var result = ScoreCalculator.Evaluate(Snap(new[] { 6 }, cards), cfg);
            Assert.That(result.additiveEntries.Where(e => e.abilityId == "C47").Select(e => e.amount100), Is.EqualTo(new long[] { 240, 240 }));
            Assert.That(result.controllerMultiplierNumerator, Is.EqualTo("1")); Assert.That(result.controllerMultiplierDenominator, Is.EqualTo("1"));
            Assert.That(result.score, Is.EqualTo("36"));
        }

        [Test] public void V08_C51GrowsOncePerShopHoweverManyControllersAreSoldAndAgainInTheNextShop()
        {
            string flip = null;
            var s = Edit(Shop(), x => { x.gold = 100; Equip(x, "C51", "C13", "C19", "C20"); flip = PutOffer(x, "controller", "C38"); });
            Func<string, string> id = definition => s.State.controllers.Single(c => c.definitionId == definition).instanceId;
            // p1.1.1 section 5: C51 keeps what it has grown in `growth`, in hundredths: +0.8 for the first card sale of a shop.
            Func<long> units = () => s.State.controllers.Single(c => c.definitionId == "C51").state.growth;
            Assert.That(units(), Is.EqualTo(0)); Assert.That(s.State.shop.turnoverGrowthCount, Is.Zero);
            Do(s, CommandKind.Sell, c => c.itemInstanceId = id("C13"));
            Assert.That(units(), Is.EqualTo(80)); Assert.That(s.State.shop.turnoverGrowthCount, Is.EqualTo(1));
            Do(s, CommandKind.Sell, c => c.itemInstanceId = id("C19"));
            Assert.That(units(), Is.EqualTo(80), "A second sale in the same shop must not grow it again.");
            Do(s, CommandKind.Buy, c => c.offerId = flip); Do(s, CommandKind.Sell, c => c.itemInstanceId = id("C38"));
            Assert.That(units(), Is.EqualTo(80), "Buying a cheap card only to resell it must not grow it."); Assert.That(s.State.shop.turnoverGrowthCount, Is.EqualTo(1));
            var reloaded = new GameSession(cfg, new MemorySaveStore(), s.State);
            Assert.That(reloaded.State.shop.turnoverGrowthCount, Is.EqualTo(1), "The cap survives save and restore.");

            Do(s, CommandKind.LeaveShop); Do(s, CommandKind.StartStage); s = ForceWinningHand(s);
            Do(s, CommandKind.Submit); Do(s, CommandKind.EndStage); Do(s, CommandKind.Continue);
            Assert.That(s.State.phase, Is.EqualTo("Shop")); Assert.That(s.State.shop.turnoverGrowthCount, Is.Zero);
            Do(s, CommandKind.Sell, c => c.itemInstanceId = id("C20"));
            Assert.That(units(), Is.EqualTo(160), "A new shop allows one more growth.");
            Assert.That(ControllerRules.GrowthValue(cfg, "C51", s.State.controllers.Single(c => c.definitionId == "C51").state), Is.EqualTo(260), "+1 at the start, +0.8 twice");
        }

        [Test] public void V08_C53OutcomeTableComesFromConfigurationAndFollowsTheRawTicket()
        {
            int[] table = cfg.Controller("C53").parameters.factorUnits2;
            // p1.1.1 section 4.1: tickets 0-8 index [0.5, 1, 1, 1.5, 2, 2, 3, 3, 4]; in halves [1, 2, 2, 3, 4, 4, 6, 6, 8].
            CollectionAssert.AreEqual(new[] { 1, 2, 2, 3, 4, 4, 6, 6, 8 }, table);
            Assert.That(table.Sum(), Is.EqualTo(36), "Mean outcome is 36 / 9 / 2 = x2.");
            for (int ticket = 0; ticket < table.Length; ticket++)
            {
                var s = Edit(Selecting(new[] { 6 }, new[] { "blue" }, new[] { "C53" }), x => x.effectRngState = SeedWithFirstTicket(table.Length, ticket));
                var result = Do(s, CommandKind.Submit); var trace = result.receipt.randomResults.Single(r => r.kind == "C53");
                Assert.That(trace.range, Is.EqualTo(table.Length)); Assert.That(trace.rawTicket, Is.EqualTo(ticket));
                Assert.That(trace.appliedValue, Is.EqualTo(table[ticket].ToString()));
                // 6 pips x (1 + 0.25 all-blue) x units/2 = 15 x units / 4, floored once: 3, 7, 7, 11, 15, 15, 22, 22, 30.
                Assert.That(s.State.lastSubmission.breakdown.score, Is.EqualTo((15 * table[ticket] / 4).ToString()), "ticket " + ticket);
            }
        }

        [TestCase(0, false, 0)] [TestCase(4, false, 0)] [TestCase(5, false, 1)] [TestCase(9, false, 1)] [TestCase(24, false, 4)]
        [TestCase(25, false, 5)] [TestCase(26, false, 5)] [TestCase(500, false, 5)] [TestCase(4, true, 1)] [TestCase(500, true, 5)]
        public void V08_InterestIsOnePerFiveGoldCappedAtFiveAndThePiggyBankNoLongerAddsToIt(int gold, bool pig, int interest)
        {
            var s = Selecting(new[] { 4, 4, 4, 4, 4 }, new[] { "red", "red", "red", "red", "red" }, pig ? new[] { "C01" } : null);
            s = Edit(s, x => x.gold = gold); Do(s, CommandKind.Submit); Do(s, CommandKind.EndStage);
            var settlement = s.State.lastStageSettlement; int balance = gold + (pig ? 1 : 0);
            Assert.That(settlement.goldBefore, Is.EqualTo(balance)); Assert.That(settlement.interestPrincipal, Is.EqualTo(Math.Min(balance, 25)));
            Assert.That(settlement.interest, Is.EqualTo(interest));
            Assert.That(s.State.gold, Is.EqualTo(balance + 5 + 2 + interest), "balance + stage reward 5 + two unused submissions + interest");
        }

        [TestCase("pair", 2)] [TestCase("three_pair", 3)] [TestCase("eight_kind", 5)] [TestCase("all_blue", 2)]
        // p1.1.1 section 3.1: price = base + current level x 1 (+1 for a self study), for every level below the cap of 12.
        public void V08_UpgradePriceRisesByOnePerExistingLevel(string hand, int basePrice)
        {
            for (int level = 0; level < 12; level++)
            {
                var s = Edit(Shop(), x => { x.gold = 1000; Equip(x, "C21", "C22"); for (int i = 6; i <= 8; i++) x.dice.Add(DiceFactory.CreateStandard(cfg, i)); x.nextDieSerial = 9; x.handLevels.Concat(x.colorLevels).Single(h => h.handId == hand).level = level; });
                Assert.That(s.GetStudyPrice(hand), Is.EqualTo(basePrice + level + 1), hand + " level " + level);
                string offer = null; s = Edit(s, x => offer = PutOffer(x, "hand_upgrade", hand));
                Assert.That(s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(basePrice + level));
            }
        }

        [Test] public void V08_GlassBreaksOnlyBelowTheFifteenPercentTicketAfterASubmissionAndTriplesItsContribution()
        {
            // p1.1.1 section 7.3: 15%, judged after the valid submission the glass die was part of; a roll never breaks it.
            Assert.That(cfg.glassBreakPercent, Is.EqualTo(15)); Assert.That(cfg.glassContributionMultiplier, Is.EqualTo(3));
            foreach (int ticket in new[] { 0, 14, 15, 99 })
            {
                var rolled = Edit(Ready(), x => { x.dice[0].isGlass = true; x.effectRngState = SeedWithFirstTicket(100, ticket); });
                Do(rolled, CommandKind.Roll);
                Assert.That(rolled.State.dice.Count, Is.EqualTo(5), "a roll never breaks glass, ticket " + ticket);
                // Two dice, the glass one locked and submitted.
                var s = Edit(Selecting(new[] { 4, 4 }, new[] { "blue", "blue" }, null, 1), x => { x.dice[0].isGlass = true; x.effectRngState = SeedWithFirstTicket(100, ticket); });
                Do(s, CommandKind.Submit);
                Assert.That(s.State.dice.Count, Is.EqualTo(ticket < 15 ? 1 : 2), "ticket " + ticket);
            }
            // One blue glass 4: contribution 4 x 3 = 12; 12 x (1 + 0.25) = 15.
            var snapshot = Snap(new[] { 4 }); snapshot.dice[0].isGlass = true;
            var result = ScoreCalculator.Evaluate(snapshot, cfg);
            Assert.That(result.pipContributions[0], Is.EqualTo(12)); Assert.That(result.score, Is.EqualTo("15"));
        }

        [Test] public void V08_StageCurveStartsAt160EndsAt1881334AndGrowsBetween1Point3And2EveryStage()
        {
            Assert.That(cfg.stages.Length, Is.EqualTo(24));
            // p1.1.1 section 11 (provisional until W6): 160, 320, 500, then 500 x 1.48^(stage - 3) to the nearest even number.
            Assert.That(cfg.stages[0].normalTarget, Is.EqualTo("160")); Assert.That(cfg.stages[1].normalTarget, Is.EqualTo("320")); Assert.That(cfg.stages[2].normalTarget, Is.EqualTo("500"));
            Assert.That(cfg.stages[10].normalTarget, Is.EqualTo("11510")); Assert.That(cfg.stages[23].normalTarget, Is.EqualTo("1881334"), "p1.1.1 ends near 1.88 million");
            BigInteger previous = 0; int risky = 0;
            foreach (var stage in cfg.stages)
            {
                BigInteger target = ExactScoreMath.Parse(stage.normalTarget);
                if (previous > 0)
                {
                    Assert.That(target * 100, Is.GreaterThanOrEqualTo(previous * 130), stage.stageId + " grows too slowly");
                    Assert.That(target * 100, Is.LessThanOrEqualTo(previous * 200), stage.stageId + " grows too fast");
                }
                Assert.That(stage.normalReward, Is.EqualTo(5), stage.stageId);
                if (stage.riskEnabled)
                {
                    risky++;
                    Assert.That(ExactScoreMath.Parse(stage.riskTarget) * 2, Is.EqualTo(target * 3), stage.stageId + " risk target is 1.5x (p1.0.0)");
                    Assert.That(stage.riskReward, Is.EqualTo(stage.normalReward + 5), stage.stageId + " pressure reward is +5 (p1.0.0)");
                }
                previous = target;
            }
            Assert.That(risky, Is.EqualTo(8));
        }

        [Test] public void V08_SelfStudyIsAllowedTwicePerShopAndResetsInTheNextShop()
        {
            var s = Edit(Shop(), x => x.gold = 100);
            Do(s, CommandKind.Study, c => c.handId = "trips"); Do(s, CommandKind.Study, c => c.handId = "all_red");
            Assert.That(s.State.shop.selfStudyUsed, Is.EqualTo(2));
            var third = Command(s, CommandKind.Study); third.handId = "pair"; var before = s.State;
            Assert.That(s.Execute(third).accepted, Is.False); AssertUnchanged(before, s.State);
            Assert.That(s.State.gold, Is.EqualTo(100 - (2 + 1) - (2 + 1)));
        }
    }
}
