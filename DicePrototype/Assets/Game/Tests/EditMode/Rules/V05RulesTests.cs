using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Core.Tests
{
    public sealed class V05RulesTests
    {
        private GameConfig cfg;
        [SetUp] public void Setup()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Game/Data/P0Config.json"));
            GameConfig.ValidateJson(json); cfg = JsonUtility.FromJson<GameConfig>(json); cfg.Validate();
        }
        private ControllerInstance Card(string id, string enchant = "none", int serial = 1) => new ControllerInstance
            { instanceId = "C-" + serial, definitionId = id, enchantmentId = enchant, acquiredSerial = serial };
        private SubmissionSnapshot Snapshot(string pips, string colors, string id, string enchant = "none", int capacity = 5)
            => new SubmissionSnapshot { configVersion = cfg.configVersion, capacity = capacity, equippedControllers = new[] { Card(id, enchant) }, dice = pips.Split(',').Select((p, i) => new SubmissionDie { dieId = "D-" + i, faceId = "F01", currentPip = int.Parse(p), currentColor = colors[i] == 'R' ? "red" : "blue", permanentSixFaceSum = 21 }).ToArray() };

        public static IEnumerable Triggers()
        {
            string[] rows = {
                "C02|2,3|BB|2,3|BR", "C03|2|B||", "C06|1,2,3|RBB|1,1,2|RBB", "C07|2,3|BB|2,2|BB",
                "C08|2,3|BB|1,2,3,4,5|BBBBB", "C09|2|B||", "C10|1,2|RB|2,3|BB", "C11|2,2,3,3|BBBB|2,2,2,3|BBBB",
                "C12|1,3,5|RBB|2,4,6|RBB", "C13|1,3,4|RBR|2,4|RR", "C14|1,3,5|RBB|1,3,6|RBB", "C15|1,2,3|RBR|1,2,3|RRR",
                "C17|1,2,3|RBB|4,5,6|RBB", "C18|1,2|RB|1,2,3|RBB", "C19|1|R||", "C20|1,2,3,4,6|RBBRB|1,2,3,6|RBBB",
                "C24|1,4|RR|1,4|BR", "C25|1,2,6|RRR|1,2|BB", "C26|2|B|2,3|BB", "C27|2,2|BB|2,3|BB",
                "C28|2,2,3,3|BBBB|2,2,2,2|BBBB", "C29|1,2,3,4|RBBR|1,2,3,5|RBBB", "C30|2,2,2,5|BBBB|2,2,5|BBB",
                "C31|2,2,2,2|BBBB|2,2,2,5|BBBB", "C32|4|R|4,4,5|RRB", "C33|6,4|BR|5,4|BR",
                "C36|2,5|BB|2|B", "C37|2,5|BB|2|B", "C38|2|B||", "C39|2,3,4|BRB|2,3|RR", "C40|1,3,5|RBB|1,2,4|RBR"
            };
            foreach (string row in rows)
            {
                var r = row.Split('|');
                yield return new TestCaseData(r[0], r[1], r[2], true).SetName("V05-TRIGGER-" + r[0] + "-hit");
                if (r[3] != "") yield return new TestCaseData(r[0], r[3], r[4], false).SetName("V05-TRIGGER-" + r[0] + "-miss");
            }
        }
        [TestCaseSource(nameof(Triggers))]
        public void AllSubmissionPredicatesHavePositiveAndNegativeEvidence(string id, string pips, string colors, bool expected)
        {
            var snapshot = Snapshot(pips, colors, id, "base5");
            if (id == "C19") snapshot.dice[0].permanentSixFaceSum = 13;
            var score = ScoreCalculator.Evaluate(snapshot, cfg);
            Assert.That(score.triggeredControllerIds.Contains(id), Is.EqualTo(expected));
            Assert.That(score.enchantBaseAdd, Is.EqualTo(expected ? 5 : 0));
            Assert.That(score.enchantBaseCount, Is.EqualTo(expected ? 1 : 0));
        }

        [Test] public void UnequippedControllersNeverTriggerAndNonSubmitCardsCannotScoreEnchant()
        {
            foreach (var def in cfg.controllers)
            {
                var s = Snapshot("1,2,3,4,6", "RBRBR", def.id);
                s.equippedControllers = Array.Empty<ControllerInstance>();
                Assert.That(ScoreCalculator.Evaluate(s, cfg).triggeredControllerIds, Is.Empty, def.id);
                if (!def.scoreEnchantEligible)
                {
                    s.equippedControllers = new[] { Card(def.id) };
                    Assert.That(ScoreCalculator.Evaluate(s, cfg).triggeredControllerIds, Is.Empty);
                    s.equippedControllers[0].enchantmentId = "base5";
                    Assert.Throws<ArgumentException>(() => ScoreCalculator.Evaluate(s, cfg));
                }
            }
        }
        [Test] public void BoundaryTriggersReadFrozenOriginalAndEffectiveLayers()
        {
            var copy = Snapshot("2,3", "BB", "C07", "base5"); copy.submissionOrdinalBefore = 1;
            Assert.That(ScoreCalculator.Evaluate(copy, cfg).enchantBaseAdd, Is.Zero);
            var odd = Snapshot("1,3,5", "RBB", "C12", "base5");
            odd.equippedControllers = new[] { Card("C12", "base5"), Card("C06", "base5", 2), Card("C40", "mult120", 3), Card("C13", "base5", 4) };
            var result = ScoreCalculator.Evaluate(odd, cfg);
            CollectionAssert.AreEqual(new[] { "C06", "C12", "C40" }, result.triggeredControllerIds);
            Assert.That(result.enchantBaseAdd, Is.EqualTo(10));
            Assert.That(result.enchantMultiplierCount, Is.EqualTo(1));
            var dye = Snapshot("1,2,3", "RBR", "C15", "base5"); dye.redWave = true;
            Assert.That(ScoreCalculator.Evaluate(dye, cfg).enchantBaseAdd, Is.Zero);
            var gold = Snapshot("1,2,3,4,6", "RBBRB", "C20", "base5"); gold.dice[4].isGold = gold.dice[4].isGlass = true;
            var contribution = ScoreCalculator.Evaluate(gold, cfg);
            Assert.That(contribution.pipContributions[4], Is.EqualTo(48));
            Assert.That(contribution.enchantBaseAdd, Is.EqualTo(5));
        }
        [Test] public void BaseEnchantIsOncePerCardAndPairAddIsNotPerDieOrMultipliedByGold()
        {
            var s = Snapshot("1,1,1", "BBB", "C10", "base5"); s.equippedControllers = new[] { Card("C10", "base5"), Card("C27", "base5", 2) };
            foreach (var d in s.dice) d.isGold = true;
            var b = ScoreCalculator.Evaluate(s, cfg);
            Assert.That(b.pipSum, Is.EqualTo(30)); Assert.That(b.pairBaseAdd, Is.EqualTo(5)); Assert.That(b.enchantBaseAdd, Is.EqualTo(10));
            Assert.That(b.baseTotal, Is.EqualTo(45)); Assert.That(b.score, Is.EqualTo(180));
        }
        [Test] public void MultipleMultiplierEnchantmentsUseOneFinalFloor()
        {
            var s = Snapshot("1", "B", "C10", "mult120"); s.equippedControllers = new[] { Card("C10", "mult120"), Card("C39", "mult120", 2) };
            var b = ScoreCalculator.Evaluate(s, cfg);
            Assert.That(b.baseTotal, Is.EqualTo(7)); Assert.That(b.enchantMultiplierCount, Is.EqualTo(2));
            Assert.That(b.scoreNumerator, Is.EqualTo("2520000")); Assert.That(b.scoreDenominator, Is.EqualTo("250000")); Assert.That(b.score, Is.EqualTo(10));
        }

        [TestCase(false, false, true, false, new[] {116667,116667,116667,216667,216666,216666})]
        [TestCase(false, false, false, true, new[] {216667,216667,216667,116667,116666,116666})]
        [TestCase(false, false, true, true, new[] {166667,166667,166667,166667,166666,166666})]
        [TestCase(true, false, true, false, new[] {103846,103846,103846,203846,203846,280770})]
        [TestCase(true, false, false, true, new[] {203846,203846,203846,110989,110989,166484})]
        public void GroupTransferFixedVectors(bool large, bool small, bool high, bool low, int[] expected)
            => CollectionAssert.AreEqual(expected, ProbabilityCalculator.BuildUnits(DiceFactory.CreateStandard(cfg, 1).faces, large, small, cfg, high, low));
        [Test] public void MissingRepeatedAndExhaustedDonorDigitsMatchReferenceVectors()
        {
            var faces = DiceFactory.CreateStandard(cfg, 1).faces;
            int[] pips = { 1, 2, 4, 4, 5, 6 }; for (int i=0;i<6;i++) faces[i].permanentPip=pips[i];
            CollectionAssert.AreEqual(new[] {91667,91667,191667,191667,216666,216666}, ProbabilityCalculator.BuildUnits(faces,false,false,cfg,true));
            for(int i=1;i<6;i++) faces[i].permanentPip=6;
            var units=ProbabilityCalculator.BuildUnits(faces,true,false,cfg,true);
            CollectionAssert.AreEqual(new[] {0,200000,200000,200000,200000,200000}, units);
            var rng=new GameRng(1); for(int i=0;i<1000;i++) Assert.That(ProbabilityCalculator.Sample(faces,units,rng,cfg).faceId, Is.Not.EqualTo("F01"));
            faces[0].permanentPip=6;
            CollectionAssert.AreEqual(new[] {166667,166667,166667,166667,166666,166666},ProbabilityCalculator.BuildUnits(faces,false,false,cfg,true));
        }
        [Test] public void GroupTransferIsExactStableUnderFacePermutationAndDoesNotMutate()
        {
            var faces=DiceFactory.CreateStandard(cfg,1).faces; faces[0].baseWeight=17; faces[1].baseWeight=3; faces[3].texture="rough"; faces[4].texture="smooth";
            string before=JsonUtility.ToJson(new FaceContainer {faces=faces});
            var expected=ProbabilityCalculator.BuildUnits(faces,true,true,cfg,true);
            CollectionAssert.AreEqual(expected.Reverse(),ProbabilityCalculator.BuildUnits(faces.Reverse().ToArray(),true,true,cfg,true));
            CollectionAssert.AreEqual(ProbabilityCalculator.BuildUnits(faces,true,true,cfg),ProbabilityCalculator.BuildUnits(faces,true,true,cfg,true,true));
            Assert.That(JsonUtility.ToJson(new FaceContainer {faces=faces}),Is.EqualTo(before));
        }
        [Serializable] private sealed class FaceContainer { public FaceState[] faces; }

        [TestCase(79,"none")][TestCase(80,"slotless")][TestCase(91,"slotless")][TestCase(92,"base5")][TestCase(97,"base5")][TestCase(98,"mult120")][TestCase(99,"mult120")]
        public void RawEnchantmentBoundariesAndNonSubmitFallbackConsumeExactlyOneDraw(int draw,string expected)
        {
            uint seed=1; for(;seed<100000;seed++) if(new GameRng(seed).NextIndex(100)==draw) break;
            Assert.That(seed,Is.LessThan(100000u));
            var baseline=new GameRng(seed); baseline.NextIndex(100);
            var scoreRng=new GameRng(seed); Assert.That(ControllerRules.DrawEnchantment("C20",null,scoreRng,cfg),Is.EqualTo(expected)); Assert.That(scoreRng.State,Is.EqualTo(baseline.State));
            var nonRng=new GameRng(seed); Assert.That(ControllerRules.DrawEnchantment("C01",null,nonRng,cfg),Is.EqualTo(expected=="slotless"?"slotless":"none")); Assert.That(nonRng.State,Is.EqualTo(baseline.State));
        }
        [Test] public void ReplacementPoolsExcludeNoneAndCurrentAndOneChoiceStillAdvancesRng()
        {
            CollectionAssert.AreEqual(new[] {"slotless","base5","mult120"},ControllerRules.ReplacementEnchantments("C20","none",cfg));
            CollectionAssert.AreEqual(new[] {"base5","mult120"},ControllerRules.ReplacementEnchantments("C20","slotless",cfg));
            CollectionAssert.AreEqual(new[] {"slotless","mult120"},ControllerRules.ReplacementEnchantments("C20","base5",cfg));
            CollectionAssert.AreEqual(new[] {"slotless","base5"},ControllerRules.ReplacementEnchantments("C20","mult120",cfg));
            Assert.That(ControllerRules.ReplacementEnchantments("C01","slotless",cfg),Is.Empty);
            var rng=new GameRng(1); Assert.That(ControllerRules.DrawEnchantment("C01","none",rng,cfg),Is.EqualTo("slotless")); Assert.That(rng.State,Is.Not.EqualTo(1u));
            Assert.Throws<ArgumentException>(()=>ControllerRules.DrawEnchantment("C01","slotless",rng,cfg));
        }
        [Test] public void EquipmentSeparatesFourOrdinarySlotsTenInstancesAndDuplicateIds()
        {
            var all=Enumerable.Range(1,10).Select(i=>Card("C"+i.ToString("D2"),i<=4?"none":"slotless",i)).ToArray();
            Assert.DoesNotThrow(()=>ControllerRules.ValidateEquipment(all,cfg)); Assert.That(ControllerRules.OrdinaryUsed(all),Is.EqualTo(4));
            all[4].enchantmentId="none"; Assert.Throws<ArgumentException>(()=>ControllerRules.ValidateEquipment(all,cfg)); all[4].enchantmentId="slotless";
            Assert.Throws<ArgumentException>(()=>ControllerRules.ValidateEquipment(all.Concat(new[]{Card("C11","slotless",11)}),cfg));
            all[9].definitionId="C01"; Assert.Throws<ArgumentException>(()=>ControllerRules.ValidateEquipment(all,cfg));
        }
        [Test] public void BudgetsAndCapacityAreIndependentOfDiceAndCombineAdditively()
        {
            var all=new[]{Card("C04"),Card("C16","none",2),Card("C23","none",3)};
            Assert.That(ControllerRules.RollBudget(all,cfg),Is.EqualTo(5)); Assert.That(ControllerRules.SubmissionBudget(all,cfg),Is.EqualTo(5)); Assert.That(ControllerRules.Capacity(all,cfg),Is.EqualTo(5));
            all=new[]{Card("C21"),Card("C22","none",2)}; Assert.That(ControllerRules.Capacity(all,cfg),Is.EqualTo(8));
            Assert.That(ControllerRules.Capacity(all.Concat(new[]{Card("C09","none",3)}),cfg),Is.EqualTo(7));
        }
        [Test] public void HandbookAlwaysContainsThirteenRowsAndSeparatesTemporaryFormationFromUpgradeEligibility()
        {
            var cards=new[]{Card("C21"),Card("C22","none",2)}; var levels=new[]{new HandLevel{handId="pair",level=8}};
            var rows=HandbookRules.Build(cfg,levels,cards,8,5,8); Assert.That(rows.Length,Is.EqualTo(13));
            Assert.That(rows[0].handId,Is.EqualTo("scatter")); Assert.That(rows[0].upgradeEligible,Is.False);
            var eight=rows.Single(x=>x.handId=="eight_kind"); Assert.That(eight.canForm,Is.True); Assert.That(eight.upgradeEligible,Is.False);
            var pair=rows.Single(x=>x.handId=="pair"); Assert.That(pair.currentMult100,Is.EqualTo(800)); Assert.That(pair.upgradeEligible,Is.False);
            Assert.That(levels[0].level,Is.EqualTo(8)); Assert.That(cards.Length,Is.EqualTo(2));
        }
        [Test] public void HandbookC29ChangesOnlyStraightThresholdAndNextReadRefreshes()
        {
            var levels=Array.Empty<HandLevel>(); var cards=new[]{Card("C29")};
            var row=HandbookRules.Build(cfg,levels,cards,4,4,4).Single(x=>x.handId=="straight");
            Assert.That(row.minimumDice,Is.EqualTo(4)); Assert.That(row.canForm,Is.True); Assert.That(row.upgradeEligible,Is.True);
            var without=HandbookRules.Build(cfg,levels,Array.Empty<ControllerInstance>(),4,4,4).Single(x=>x.handId=="straight"); Assert.That(without.canForm,Is.False); Assert.That(without.upgradeEligible,Is.False);
            var c12=HandbookRules.Build(cfg,levels,cards.Concat(new[]{Card("C12","none",2)}),4,4,4).Single(x=>x.handId=="straight"); Assert.That(c12.canForm,Is.False); Assert.That(c12.upgradeEligible,Is.True);
        }
    }
}
