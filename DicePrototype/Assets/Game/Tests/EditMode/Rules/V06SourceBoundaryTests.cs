using System;
using System.IO;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Core.Tests
{
    public sealed class V06SourceBoundaryTests
    {
        private GameConfig cfg;
        [SetUp] public void Setup() { cfg = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText(Path.Combine(Application.dataPath, "Game/Data/P0Config.json")))); cfg.Validate(); }
        private static ControllerInstance Card(string id, int serial, string enchant = "none") => new ControllerInstance { definitionId = id, instanceId = "c" + serial, acquiredSerial = serial, enchantmentId = enchant };
        private SubmissionSnapshot Snap(int[] pips, params ControllerInstance[] cards) => new SubmissionSnapshot { configVersion = cfg.configVersion, capacity = Math.Max(5, pips.Length), equippedControllers = cards, dice = pips.Select((p, i) => new SubmissionDie { dieId = "d" + i, faceId = "F01", currentPip = p, currentColor = "blue", permanentSixFaceSum = 21 }).ToArray() };
        [Test]
        public void FrozenMimicChainReadsFinalOwnerAndNeverRetargetsLostTarget()
        {
            var a = Card("C41", 1); var b = Card("C41", 2); var target = Card("C51", 3); target.state.turnoverUnits2 = 7; var later = Card("C46", 4);
            var cards = new[] { a, b, target, later }; var bindings = ControllerRules.FreezeBindings(cards); var sources = ControllerRules.ResolveSources(cards, bindings);
            CollectionAssert.AreEqual(new[] { "C51", "C51", "C51", "C46" }, sources.Select(s => s.abilityId));
            CollectionAssert.AreEqual(new[] { "c3", "c3", "c3", "c4" }, sources.Select(s => s.stateOwnerId));
            sources[0].state.turnoverUnits2 = 99; Assert.That(target.state.turnoverUnits2, Is.EqualTo(7));
            var lost = new[] { a, b, later }; var after = ControllerRules.ResolveSources(lost, bindings); Assert.That(after[0].abilityId, Is.Null); Assert.That(after[1].abilityId, Is.Null);
            Assert.That(ControllerRules.ResolveSources(lost)[0].abilityId, Is.EqualTo("C46"), "Only an explicit new preview may bind current adjacency.");
        }
        [Test]
        public void CyclesDuplicateBindingsAndDuplicateInstanceIdsReject()
        {
            var a = Card("C41", 1); var b = Card("C41", 2); var cycle = new[] { new MimicBinding { sourceInstanceId = "c1", targetInstanceId = "c2" }, new MimicBinding { sourceInstanceId = "c2", targetInstanceId = "c1" } };
            Assert.Throws<ArgumentException>(() => ControllerRules.ResolveSources(new[] { a, b }, cycle));
            Assert.Throws<ArgumentException>(() => ControllerRules.ResolveSources(new[] { a, b }, new[] { cycle[0], cycle[0] }));
            Assert.Throws<ArgumentException>(() => ControllerRules.ResolveSources(new[] { a, a.Clone() }));
            Assert.Throws<ArgumentException>(() => ControllerRules.ResolveSources(new[] { Card("C04", 1) }));
        }
        [Test]
        public void CopiedC45SharesColorButAppliesEachSourceAndPlansOwnerOnce()
        {
            var mimic = Card("C41", 1, "mult2"); var owner = Card("C45", 2, "base10"); owner.state.activeColor = "blue"; var snap = Snap(new[] { 2 }, mimic, owner);
            var r = ScoreCalculator.Evaluate(snap, cfg); Assert.That(r.pipContributions[0], Is.EqualTo(18)); Assert.That(r.enchantBaseAdd, Is.EqualTo(10)); Assert.That(r.score, Is.EqualTo("168"));
            CollectionAssert.AreEqual(new[] { "c2" }, r.afterScorePlan.nextColorOwnerIds); Assert.That(owner.state.activeColor, Is.EqualTo("blue"));
            snap.dice[0].currentColor = "red"; r = ScoreCalculator.Evaluate(snap, cfg); Assert.That(r.enchantBaseAdd, Is.EqualTo(10)); Assert.That(r.triggeredSourceIds.Length, Is.EqualTo(2));
        }
        [Test]
        public void C53PreviewUsesSharedUnknownOwnerRangeAndPreservesExplicitBranch()
        {
            var snap = Snap(new[] { 5 }, Card("C41", 1), Card("C53", 2)); string before = JsonUtility.ToJson(snap); var p = ScoreCalculator.Preview(snap, cfg);
            Assert.That(p.isRandom, Is.True); CollectionAssert.AreEqual(new[] { "c2" }, p.randomOwnerIds); Assert.That(p.minimum.score, Is.EqualTo("3")); Assert.That(p.maximum.score, Is.EqualTo("960")); Assert.That(JsonUtility.ToJson(snap), Is.EqualTo(before));
            Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(snap, cfg)); snap.resolvedRandomChoices = new[] { new RandomMultiplierChoice { ownerInstanceId = "c2", units2 = 6 } };
            p = ScoreCalculator.Preview(snap, cfg); Assert.That(p.isRandom, Is.False); Assert.That(p.minimum.score, Is.EqualTo("135")); Assert.That(p.maximum.score, Is.EqualTo("135"));
        }
        [Test]
        public void ZeroFactorStillTriggersEnchantIncomeAndFrozenPostPlan()
        {
            var snap = Snap(new[] { 2 }, Card("C49", 1, "base10"), Card("C01", 2), Card("C38", 3)); var r = ScoreCalculator.Evaluate(snap, cfg);
            Assert.That(r.score, Is.EqualTo("0")); Assert.That(r.controllerMultiplierNumerator, Is.EqualTo("0")); Assert.That(r.enchantBaseAdd, Is.EqualTo(10)); Assert.That(r.afterScorePlan.earnedGold, Is.EqualTo(1)); CollectionAssert.AreEqual(new[] { 0 }, r.afterScorePlan.blueSpreadIndices);
        }
        [Test]
        public void C43StructuralPrimaryDiffersFromUpgradedWinnerAndClampsAfterScore()
        {
            var snap = Snap(new[] { 2, 2, 2, 3, 3, 3 }, Card("C41", 1, "base10"), Card("C43", 2, "mult2")); snap.previousPrimaryHandId = "two_trips"; snap.handLevels = new[] { new HandLevel { handId = "pair", level = 100 }, new HandLevel { handId = "two_trips", level = 99 } }; snap.debug = true;
            var r = ScoreCalculator.Evaluate(snap, cfg); Assert.That(r.primaryHandId, Is.EqualTo("two_trips")); Assert.That(r.afterScorePlan.upgradeRequested, Is.EqualTo(2)); Assert.That(r.afterScorePlan.upgradeApplied, Is.EqualTo(1)); Assert.That(snap.handLevels[1].level, Is.EqualTo(99));
            snap.handLevels = new[] { new HandLevel { handId = "pair", level = 100 }, new HandLevel { handId = "two_trips", level = 0 } }; r = ScoreCalculator.Evaluate(snap, cfg); Assert.That(r.chosenGroupId, Is.EqualTo("pair")); Assert.That(r.primaryHandId, Is.EqualTo("two_trips"));
        }
        [Test]
        public void ColorSingleDieIndependentAndExcludedFromPrimary()
        {
            var snap = Snap(new[] { 6 }); var r = ScoreCalculator.Evaluate(snap, cfg); Assert.That(r.score, Is.EqualTo("18")); Assert.That(r.colorId, Is.EqualTo("all_blue")); Assert.That(r.primaryHandId, Is.EqualTo("scatter")); Assert.That(r.totalHandMult100, Is.EqualTo(100));
            snap.dice[0].currentColor = "red"; snap.colorLevels = new[] { new HandLevel { handId = "all_red", level = 8 } }; r = ScoreCalculator.Evaluate(snap, cfg); Assert.That(r.score, Is.EqualTo("36")); Assert.That(r.primaryHandId, Is.EqualTo("scatter"));
        }
        [Test]
        public void MultiPairShapesNeverSplitSamePointAndDebugCapsDefinedRedReward()
        {
            CollectionAssert.DoesNotContain(HandEvaluator.SameValueCandidates(new System.Collections.Generic.Dictionary<int, int> { { 6, 6 } }), "two_trips");
            CollectionAssert.DoesNotContain(HandEvaluator.SameValueCandidates(new System.Collections.Generic.Dictionary<int, int> { { 6, 8 } }), "four_pair");
            var snap = Snap(Enumerable.Repeat(6, 32).ToArray()); snap.debug = true; foreach (var d in snap.dice) d.currentColor = "red";
            var r = ScoreCalculator.Evaluate(snap, cfg); Assert.That(r.primaryHandId, Is.EqualTo("eight_kind")); Assert.That(r.redCount, Is.EqualTo(32)); Assert.That(r.redMult100, Is.EqualTo(1400)); Assert.That(r.redRewardCapped, Is.True);
        }
        [Test]
        public void C44ConvertsWalletBeforeMultiplicationAndExactScoreExceedsInt64()
        {
            var snap = Snap(new[] { 6 }, Card("C44", 1)); snap.wallet = long.MaxValue; var r = ScoreCalculator.Evaluate(snap, cfg);
            Assert.That(r.walletBaseAdd, Is.EqualTo("18446744073709551614")); Assert.That(ExactScoreMath.Parse(r.score), Is.GreaterThan(new BigInteger(long.MaxValue)));
            Assert.That(ScoreExplanation.Build(snap, r, cfg).global.Last().after, Is.EqualTo(r.score));
        }
        [Test]
        public void StateLegalityDebugDuplicatesAndRetiredIdsAreDistinctBoundaries()
        {
            var duplicate = new[] { Card("C46", 1), Card("C46", 2) }; Assert.Throws<ArgumentException>(() => ControllerRules.ValidateEquipment(duplicate, cfg)); Assert.DoesNotThrow(() => ControllerRules.ValidateEquipment(duplicate, cfg, true));
            foreach (string id in new[] { "C04", "C16", "C23" }) Assert.Throws<ArgumentException>(() => ControllerRules.ValidateEquipment(new[] { Card(id, 1) }, cfg, true));
            foreach (int bad in new[] { 0, 1, 2 }) { var c = Card("C51", 1); c.state.turnoverUnits2 = bad; Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(Snap(new[] { 1 }, c), cfg)); }
            var passive = Card("C05", 1, "base10"); Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(Snap(new[] { 1 }, passive), cfg)); var debug = Snap(new[] { 1 }, passive); debug.debug = true; Assert.That(ScoreCalculator.Evaluate(debug, cfg).enchantBaseAdd, Is.Zero);
        }
        [Test]
        public void OwnFundSaleDoesNotReadBoundTargetsAndOverflowIsRejected()
        {
            var mimic = Card("C41", 1); var fund = Card("C52", 2); fund.state.fundAccrual = 30;
            Assert.That(ControllerRules.SalePrice(mimic, cfg), Is.EqualTo(1)); Assert.That(ControllerRules.SalePrice(fund, cfg), Is.EqualTo(31)); mimic.state.fundAccrual = 9; Assert.That(ControllerRules.SalePrice(mimic, cfg), Is.EqualTo(10));
            fund.state.fundAccrual = 1; Assert.Throws<ArgumentException>(() => ControllerRules.SalePrice(fund, cfg));
        }
        [TestCase(2, new[] { 66667, 66667, 66667, 266667, 266666, 266666 })]
        [TestCase(3, new[] { 16667, 16667, 16667, 316667, 316666, 316666 })]
        [TestCase(4, new[] { 0, 0, 0, 333334, 333333, 333333 })]
        public void NetProbabilitySourcesStackAndSaturate(int high, int[] want)
        { var faces = DiceFactory.CreateStandard(cfg, 1).faces; CollectionAssert.AreEqual(want, ProbabilityCalculator.BuildUnits(faces, false, false, cfg, high, 0)); CollectionAssert.AreEqual(want, ProbabilityCalculator.BuildUnits(faces, false, false, cfg, high + 2, 2)); }
        [Test]
        public void AllNewAndReplacementEnchantTicketsMatchIndependentPoolTables()
        {
            string[] order = { "none", "slotless", "base10", "mult2", "submit1", "roll1" }; int[] ordinary = { 50, 18, 10, 4, 9, 9 }, special = { 100, 50, 0, 0, 25, 25 }; string[] passive = { "C05", "C21", "C22", "C34", "C35", "C52" }; int fresh = 0, replacements = 0, pools = 0;
            foreach (var def in cfg.controllers)
            {
                var weights = passive.Contains(def.id) ? special : ordinary;
                foreach (string current in new[] { (string)null }.Concat(order.Where((x, i) => weights[i] > 0)))
                {
                    var indexes = Enumerable.Range(0, 6).Where(i => weights[i] > 0 && (current == null || (order[i] != "none" && order[i] != current))).ToArray(); int total = indexes.Sum(i => weights[i]); pools += current == null ? 0 : 1;
                    var seeds = new uint[total]; int found = 0; for (uint seed = 1; found < total && seed < 100000; seed++) { int ticket = new GameRng(seed).NextIndex(total); if (seeds[ticket] == 0) { seeds[ticket] = seed; found++; } }
                    Assert.That(found, Is.EqualTo(total));
                    for (int ticket = 0; ticket < total; ticket++)
                    {
                        int remaining = ticket; string expected = null; foreach (int i in indexes) { if (remaining < weights[i]) { expected = order[i]; break; } remaining -= weights[i]; }
                        var rng = new GameRng(seeds[ticket]); var baseline = new GameRng(seeds[ticket]); baseline.NextIndex(total);
                        Assert.That(ControllerRules.DrawEnchantment(def.id, current, rng, cfg), Is.EqualTo(expected), def.id + "/" + current + "/" + ticket); Assert.That(rng.State, Is.EqualTo(baseline.State)); if (current == null) fresh++; else replacements++;
                    }
                }
            }
            Assert.That(fresh, Is.EqualTo(5600)); Assert.That(pools, Is.EqualTo(288)); Assert.That(replacements, Is.EqualTo(12800));
        }
        [Test]
        public void NumericConfigKeysPreserveValuesAndRejectMissingUnknownOrWrongTypes()
        {
            string raw = File.ReadAllText(Path.Combine(Application.dataPath, "Game/Data/P0Config.json")); string changed = raw.Replace("\"2\": 2", "\"2\": 3"); Assert.That(changed, Is.Not.EqualTo(raw));
            var decoded = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(changed)); decoded.Validate(); Assert.That(decoded.Controller("C11").parameters.requiredCounts.pip2, Is.EqualTo(3));
            Assert.Throws<ArgumentException>(() => GameConfig.PrepareJsonForUnity(raw.Replace("\"2\": 2,", "")));
            Assert.Throws<ArgumentException>(() => GameConfig.PrepareJsonForUnity(raw.Replace("\"2\": 2", "\"pip2\": 2")));
            Assert.Throws<ArgumentException>(() => GameConfig.PrepareJsonForUnity(raw.Replace("\"2\": 2", "\"2\": \"2\"")));
        }
        [Test]
        public void HandbookUsesMimicAbilitiesAndSeparatesColorFromFourteenHandRows()
        {
            var cards = new[] { Card("C41", 1), Card("C12", 2), Card("C03", 3) };
            var rows = HandbookRules.Build(cfg, Array.Empty<HandLevel>(), new[] { new HandLevel { handId = "all_blue", level = 2 } }, cards, 8, 8, 8);
            Assert.That(rows.Count(r => r.category == "hand"), Is.EqualTo(14)); Assert.That(rows.Count(r => r.category == "color"), Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "scatter" }.Concat(cfg.handDefinitions.OrderBy(h => h.structuralPriority).Select(h => h.id)), rows.Where(r => r.category == "hand").Select(r => r.handId));
            Assert.That(rows.Single(r => r.handId == "four_pair").canForm, Is.False); Assert.That(rows.Single(r => r.handId == "straight").canForm, Is.False);
            Assert.That(rows.Single(r => r.handId == "all_blue").canForm, Is.False); Assert.That(rows.Single(r => r.handId == "all_blue").currentMult100, Is.EqualTo(400));
        }
        [Test]
        public void BigIntegerParserRejectsFloatingSignedWhitespaceAndEmptyData()
        {
            foreach (string s in new[] { null, "", " ", "-1", "+1", "1e10", "1.0", "NaN", "Infinity", " 1", "1 " }) Assert.Throws<ArgumentException>(() => ExactScoreMath.Parse(s));
            Assert.That(ExactScoreMath.SaveDecimal(ExactScoreMath.Parse("000123")), Is.EqualTo("123"));
        }
    }
}
