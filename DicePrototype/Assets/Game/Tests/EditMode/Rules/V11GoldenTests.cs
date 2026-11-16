using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Core.Tests
{
    // Generated solely from the rules by tools/v11_golden.py; production code is only the system under test.
    [Serializable]
    public sealed class V11GoldenFile
    {
        public string schema, configVersion;
        public string[] scoringCards, conditionalCards, pureMultiplierCards, perDieAdditionCards;
        public V11GoldenCase[] cases;
    }
    [Serializable]
    public sealed class V11GoldenCase
    {
        public string id, description, cardId, family;
        public V10GoldenInput input;
        public V10GoldenExpected expected;
        public V11GoldenAfterScorePlan afterScorePlan;
        public override string ToString() => id;
    }
    [Serializable]
    public sealed class V11GoldenAfterScorePlan
    {
        public long earnedGold;
        public bool copyFirstToSecond, dyeAllFacesRed;
        public int growthNetDelta, upgradeRequested, upgradeApplied;
        public int[] redSpreadIndices, growthIndices, blueSpreadIndices;
        public string upgradeHandId, nextPreviousPrimary;
        public string[] nextColorOwnerIds, randomFactorOwnerIds, cannonRiskEmitterIds;
    }

    public class V11GoldenTests
    {
        // Rule-text classifications: P0 v0.6 Appendix C / section 12 (with v0.8 numerical changes),
        // v0.9 section 4 and v1.0 section 6. Kept here so metadata cannot silently reduce coverage.
        private static readonly HashSet<string> AlwaysTriggered = new HashSet<string>(
            "C01,C03,C09,C38,C42,C45,C46,C47,C49,C50,C51,C53,C54,C55,C56,C57,C58,C59,C60,C61,C62,C63,C64,C90,C94,C100,C101,C108,C117,C118,C119,C122,C123,C124,C125,C129,C132,C134,C141".Split(','));
        private static readonly string[] RequiredPerDieAdditions =
            "C10,C13,C17,C19,C20,C39,C91,C92,C94,C107,C137".Split(',');
        private static readonly string[] RequiredGlobalMultipliers =
            "C02,C06,C08,C09,C11,C14,C18,C28,C30,C31,C32,C33,C40,C42,C46,C47,C49,C51,C53,C54,C56,C57,C58,C60,C61,C63,C71,C74,C75,C77,C81,C82,C83,C86,C87,C88,C89,C90,C100,C101,C102,C103,C104,C105,C106,C129,C130,C132,C134,C138,C139,C141,C144,C147,C148,C149,C150,C151,C152,C153".Split(',');
        private GameConfig config;
        private static string DataPath(string name) => Path.Combine(Application.dataPath, "Game", "Data", name);
        private static V11GoldenFile ReadFile() => JsonUtility.FromJson<V11GoldenFile>(File.ReadAllText(DataPath("GoldenScoresV11.json")));
        [SetUp] public void Setup()
        {
            string json = File.ReadAllText(DataPath("P0Config.json")); GameConfig.ValidateJson(json);
            config = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json)); config.Validate();
        }
        public static IEnumerable Cases()
        {
            foreach (var item in ReadFile().cases) yield return new TestCaseData(item).SetName("GOLD11-" + item.id);
        }
        private static SubmissionSnapshot Snapshot(V11GoldenCase example, GameConfig cfg)
        {
            var input = example.input;
            return new SubmissionSnapshot
            {
                configVersion = cfg.configVersion, capacity = input.capacity, rollsRemaining = input.rollsRemaining, rollsUsed = input.rollsUsed,
                submissionsRemainingBefore = input.submissionsRemainingBefore, permanentDiceCount = input.permanentDiceCount, submissionOrdinalBefore = input.submissionOrdinal,
                wallet = input.wallet, redWave = input.redWave, blueWave = input.blueWave, previousPrimaryHandId = string.IsNullOrEmpty(input.previousPrimary) ? null : input.previousPrimary,
                stageKind = input.stageKind, modifierId = input.modifierId ?? "", inventoryCount = input.inventoryCount, ordinaryControllerSlots = input.ordinaryControllerSlots,
                scoreItemMultiplier = input.scoreItemMultiplier, disabledControllerIds = (string[])(input.disabledControllerIds ?? Array.Empty<string>()).Clone(),
                stageHandsUsed = (string[])(input.stageHandsUsed ?? Array.Empty<string>()).Clone(),
                mimicBindings = null, // adjacency: a mimic copies its right neighbour
                handLevels = (input.handLevels ?? Array.Empty<HandLevel>()).Select(h => h.Clone()).ToArray(), colorLevels = (input.colorLevels ?? Array.Empty<HandLevel>()).Select(h => h.Clone()).ToArray(),
                resolvedRandomChoices = (input.randomChoices ?? Array.Empty<RandomMultiplierChoice>()).Select(c => c.Clone()).ToArray(),
                equippedControllers = (input.controllers ?? Array.Empty<V09GoldenController>()).Select((c, i) => new ControllerInstance
                {
                    instanceId = c.instanceId, definitionId = c.definitionId, enchantmentId = string.IsNullOrEmpty(c.enchantmentId) ? "none" : c.enchantmentId, acquiredSerial = i + 1,
                    state = new ControllerDynamicState { growth = c.growth, turnoverUnits2 = c.turnoverUnits2 == 0 ? 3 : c.turnoverUnits2, activeColor = string.IsNullOrEmpty(c.activeColor) ? null : c.activeColor }
                }).ToArray(),
                dice = input.dice.Select(d =>
                {
                    var faces = d.faces.Select(f => new FaceState { faceId = f.faceId, permanentPip = f.pip, baseColor = f.color, texture = f.texture, isGold = f.gold, baseWeight = 1 }).ToArray();
                    var up = faces.Single(f => f.faceId == d.faceId);
                    return new SubmissionDie { dieId = d.dieId, faceId = d.faceId, currentPip = d.pip, currentColor = up.baseColor, isGold = up.isGold, isGlass = d.glass,
                        permanentSixFaceSum = faces.Sum(f => f.permanentPip), permanentFacesCopy = faces, lifetime = string.IsNullOrEmpty(d.lifetime) ? "permanent" : d.lifetime };
                }).ToArray(),
                poolDice = (input.pool ?? Array.Empty<V09GoldenPoolDie>()).Select(p => new PoolDie { dieId = p.dieId, faceId = p.faceId, currentPip = p.pip, currentColor = p.color }).ToArray()
            };
        }

        [TestCaseSource(nameof(Cases))]
        public void GoldenV11_EveryExpectedFieldMatchesTheIndependentReference(V11GoldenCase example)
        {
            var snapshot = Snapshot(example, config); string before = JsonUtility.ToJson(snapshot);
            var actual = ScoreCalculator.Evaluate(snapshot, config); var e = example.expected; string id = example.id + " " + example.description;
            {
                Assert.That(actual.score, Is.EqualTo(e.score), id + " score");
                Assert.That(actual.baseTotal, Is.EqualTo(e.baseTotal), id + " baseTotal");
                Assert.That(actual.pipSum, Is.EqualTo(e.pipSum), id + " pipSum");
                CollectionAssert.AreEqual(e.pipContributions, actual.pipContributions, id + " pipContributions");
                CollectionAssert.AreEqual(e.pipRetriggers, actual.pipRetriggers, id + " pipRetriggers");
                CollectionAssert.AreEqual(e.sealedDice, actual.sealedDice, id + " sealedDice");
                Assert.That(actual.primaryHandId, Is.EqualTo(e.primaryHandId), id + " primaryHandId");
                Assert.That(actual.handMultBeforeModifier100, Is.EqualTo(e.handMultBeforeModifier100), id + " handMultBeforeModifier100");
                Assert.That(actual.totalHandMult100, Is.EqualTo(e.totalHandMult100), id + " totalHandMult100");
                Assert.That(actual.handMultAdd100, Is.EqualTo(e.handMultAdd100), id + " handMultAdd100");
                Assert.That(actual.colorId ?? "", Is.EqualTo(e.colorId ?? ""), id + " colorId");
                Assert.That(actual.colorMult100, Is.EqualTo(e.colorMult100), id + " colorMult100");
                Assert.That(actual.redCount, Is.EqualTo(e.redCount), id + " redCount");
                Assert.That(actual.redPoolCount, Is.EqualTo(e.redPoolCount), id + " redPoolCount");
                Assert.That(actual.redBonusCount, Is.EqualTo(e.redBonusCount), id + " redBonusCount");
                Assert.That(actual.redMult100, Is.EqualTo(e.redMult100), id + " redMult100");
                Assert.That(actual.controllerMultiplierNumerator, Is.EqualTo(e.controllerMultiplierNumerator), id + " multiplier numerator");
                Assert.That(actual.controllerMultiplierDenominator, Is.EqualTo(e.controllerMultiplierDenominator), id + " multiplier denominator");
                CollectionAssert.AreEqual(e.triggeredSourceIds, actual.triggeredSourceIds, id + " triggeredSourceIds");
                CollectionAssert.AreEqual((e.controllerBaseAdds ?? Array.Empty<V09GoldenBaseAdd>()).Select(a => a.emitterId + "/" + a.abilityId + "/" + a.amount),
                    actual.controllerBaseAdds.Select(a => a.emitterId + "/" + a.abilityId + "/" + a.amount), id + " controllerBaseAdds");
                CollectionAssert.AreEqual((e.growthChanges ?? Array.Empty<V09GoldenGrowth>()).Select(g => g.ownerInstanceId + "/" + g.abilityId + "/" + g.delta + "/" + g.reset),
                    actual.afterScorePlan.growthChanges.Select(g => g.ownerInstanceId + "/" + g.abilityId + "/" + g.delta + "/" + g.reset), id + " growthChanges");
                CollectionAssert.AreEqual((e.globalFactors ?? Array.Empty<V10GoldenFactor>()).Select(f => f.emitterId + "/" + f.abilityId + "/" + f.numerator + "/" + f.denominator),
                    actual.globalFactors.Select(f => f.emitterId + "/" + f.abilityId + "/" + f.numerator + "/" + f.denominator), id + " globalFactors");
                Assert.That(actual.afterScorePlan.goldSpent, Is.EqualTo(e.goldSpent), id + " goldSpent");
                CollectionAssert.AreEqual((e.faceChanges ?? Array.Empty<V10GoldenFaceChange>()).Select(f => f.sourceId + "/" + f.emitterId + "/" + f.dieId + "/" + f.faceId + "/" + f.kind + "/" + f.amount + "/" + (f.text ?? "")),
                    actual.afterScorePlan.faceChanges.Select(f => f.sourceId + "/" + f.emitterId + "/" + f.dieId + "/" + f.faceId + "/" + f.kind + "/" + f.amount + "/" + (f.text ?? "")), id + " faceChanges");
                var plan = actual.afterScorePlan; var ep = example.afterScorePlan;
                Assert.That(ep, Is.Not.Null, id + " requires legacy post-score fields too");
                Assert.That(plan.earnedGold, Is.EqualTo(ep.earnedGold), id + " earnedGold");
                Assert.That(plan.copyFirstToSecond, Is.EqualTo(ep.copyFirstToSecond), id + " copyFirstToSecond");
                CollectionAssert.AreEqual(ep.redSpreadIndices, plan.redSpreadIndices, id + " redSpreadIndices");
                Assert.That(plan.dyeAllFacesRed, Is.EqualTo(ep.dyeAllFacesRed), id + " dyeAllFacesRed");
                Assert.That(plan.growthNetDelta, Is.EqualTo(ep.growthNetDelta), id + " growthNetDelta");
                CollectionAssert.AreEqual(ep.growthIndices, plan.growthIndices, id + " growthIndices");
                CollectionAssert.AreEqual(ep.blueSpreadIndices, plan.blueSpreadIndices, id + " blueSpreadIndices");
                Assert.That(plan.upgradeHandId ?? "", Is.EqualTo(ep.upgradeHandId ?? ""), id + " upgradeHandId");
                Assert.That(plan.upgradeRequested, Is.EqualTo(ep.upgradeRequested), id + " upgradeRequested");
                Assert.That(plan.upgradeApplied, Is.EqualTo(ep.upgradeApplied), id + " upgradeApplied");
                Assert.That(plan.nextPreviousPrimary, Is.EqualTo(ep.nextPreviousPrimary), id + " nextPreviousPrimary");
                CollectionAssert.AreEqual(ep.nextColorOwnerIds, plan.nextColorOwnerIds, id + " nextColorOwnerIds");
                CollectionAssert.AreEqual(ep.randomFactorOwnerIds, plan.randomFactorOwnerIds, id + " randomFactorOwnerIds");
                CollectionAssert.AreEqual(ep.cannonRiskEmitterIds, plan.cannonRiskEmitterIds, id + " cannonRiskEmitterIds");
                Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before), "Scoring mutated its input.");
            }
            // The step-by-step explanation must rebuild the same numbers (it throws on any mismatch).
            var trace = ScoreExplanation.Build(snapshot, actual, config);
            Assert.That(trace.dice.Select(d => d.retriggers), Is.EqualTo(actual.pipRetriggers), id + " trace retriggers");
        }

        // Historical vectors, rather than the new file's own declaration, anchor the required inventory.
        // Resolving a mimic here is only fixture bookkeeping: it never computes any expected score.
        private static void AddHistoricalSources(HashSet<string> cards, V09GoldenController[] equipped,
            string[] triggered, Dictionary<string, string> bindings = null)
        {
            var byId = equipped.ToDictionary(c => c.instanceId);
            foreach (string emitterId in triggered ?? Array.Empty<string>())
            {
                Assert.That(byId.ContainsKey(emitterId), Is.True, "Historical trigger has no instance: " + emitterId);
                var source = byId[emitterId]; cards.Add(source.definitionId);
                var seen = new HashSet<string>();
                while (source.definitionId == "C41")
                {
                    Assert.That(seen.Add(source.instanceId), Is.True, "Historical mimic cycle");
                    string target;
                    if (bindings != null) bindings.TryGetValue(source.instanceId, out target);
                    else
                    {
                        int index = Array.IndexOf(equipped, source);
                        target = index + 1 < equipped.Length ? equipped[index + 1].instanceId : null;
                    }
                    if (string.IsNullOrEmpty(target) || !byId.TryGetValue(target, out source)) break;
                    cards.Add(source.definitionId);
                }
            }
        }

        private static HashSet<string> HistoricalScoringCards()
        {
            var cards = new HashSet<string>();
            foreach (string name in new[] { "GoldenScoresV09.json", "GoldenScoresV10.json" })
            {
                var file = JsonUtility.FromJson<V10GoldenFile>(File.ReadAllText(DataPath(name)));
                foreach (var example in file.cases)
                    AddHistoricalSources(cards, example.input.controllers ?? Array.Empty<V09GoldenController>(), example.expected.triggeredSourceIds);
            }
            foreach (var example in GoldenJsonLoader.Read(File.ReadAllText(DataPath("GoldenScores.json"))).cases)
            {
                var equipped = (example.input.controllers ?? Array.Empty<ControllerInstance>()).Select(c =>
                    new V09GoldenController { instanceId = c.instanceId, definitionId = c.definitionId }).ToArray();
                var bindings = example.input.bindings == null ? null : example.input.bindings.ToBindings()
                    .ToDictionary(b => b.sourceInstanceId, b => b.targetInstanceId);
                AddHistoricalSources(cards, equipped, example.expected.triggeredSourceIds, bindings);
            }
            return cards;
        }

        private bool HasCopiesCase(V11GoldenCase example, string card, int copies, bool triggered, string family)
        {
            if (example.cardId != card || example.family != family) return false;
            var instances = example.input.controllers.Where(c => c.definitionId == card).ToArray();
            if (instances.Length != copies) return false;
            var disabled = new HashSet<string>(example.input.disabledControllerIds ?? Array.Empty<string>());
            if (instances.Any(c => disabled.Contains(c.instanceId))) return false;
            var expected = new HashSet<string>(example.expected.triggeredSourceIds);
            var actual = new HashSet<string>(ScoreCalculator.Evaluate(Snapshot(example, config), config).triggeredSourceIds);
            return instances.All(c => expected.Contains(c.instanceId) == triggered && actual.Contains(c.instanceId) == triggered);
        }

        [Test]
        public void GoldenV11_CoversEveryHistoricalScoringCardWithTwoTriggeringCopies()
        {
            var file = ReadFile(); var historical = HistoricalScoringCards();
            Assert.That(file.schema, Is.EqualTo("v11-golden-1"));
            Assert.That(file.configVersion, Is.EqualTo("p1.1.0"));
            Assert.That(config.configVersion, Is.EqualTo(file.configVersion));
            Assert.That(file.cases, Is.Not.Empty);
            Assert.That(file.cases.Select(c => c.id).Distinct().Count(), Is.EqualTo(file.cases.Length), "Golden case IDs must be unique.");
            CollectionAssert.AreEquivalent(historical, file.scoringCards, "The historical scoring inventory must not shrink.");
            Assert.That(file.scoringCards.Distinct().Count(), Is.EqualTo(file.scoringCards.Length));
            foreach (string card in historical)
                Assert.That(file.cases.Any(c => HasCopiesCase(c, card, 2, true, "two_hit")), Is.True,
                    card + " needs two distinct real instances that both trigger in the reference and the game.");
            foreach (var example in file.cases)
                Assert.That(example.input.controllers.Select(c => c.instanceId).Distinct().Count(), Is.EqualTo(example.input.controllers.Length),
                    example.id + " contains reused instance IDs");
        }

        [Test]
        public void GoldenV11_ConditionalCardsAlsoHaveTwoUntriggeredCopies()
        {
            var file = ReadFile();
            Assert.That(file.conditionalCards, Is.Not.Empty);
            CollectionAssert.AreEquivalent(HistoricalScoringCards().Except(AlwaysTriggered), file.conditionalCards,
                "Every conditionally triggered scoring card requires a natural miss.");
            foreach (string card in file.conditionalCards)
            {
                Assert.That(file.scoringCards.Contains(card), Is.True, card + " conditional card outside scoring inventory");
                Assert.That(file.cases.Any(c => HasCopiesCase(c, card, 2, false, "two_miss")), Is.True,
                    card + " needs a natural condition miss for both copies, without disabling either instance.");
            }
        }

        [Test]
        public void GoldenV11_MultiplierAndPerDieAdditionCardsHaveThreeTriggeringCopies()
        {
            var file = ReadFile();
            Assert.That(file.pureMultiplierCards, Is.Not.Empty); Assert.That(file.perDieAdditionCards, Is.Not.Empty);
            CollectionAssert.IsSubsetOf(RequiredGlobalMultipliers, file.pureMultiplierCards, "Global multiplier coverage cannot shrink.");
            CollectionAssert.IsSubsetOf(RequiredPerDieAdditions, file.perDieAdditionCards, "Per-die addition coverage cannot shrink.");
            foreach (string card in file.pureMultiplierCards.Concat(file.perDieAdditionCards).Distinct())
                Assert.That(file.cases.Any(c => HasCopiesCase(c, card, 3, true, "three_hit")), Is.True,
                    card + " needs three real copies that all trigger.");
        }

        [Test]
        public void GoldenV11_MixesRealCopiesWithMimicsOfTheSameAbility()
        {
            var file = ReadFile();
            var mixed = file.cases.Where(c => c.family == "mimic_hit").ToArray();
            Assert.That(mixed, Is.Not.Empty);
            foreach (string card in HistoricalScoringCards())
                Assert.That(mixed.Any(c => c.cardId == card), Is.True, card + " needs a mixed mimic case");
            foreach (var example in mixed)
            {
                var equipped = example.input.controllers;
                var real = equipped.Where(c => c.definitionId == example.cardId).ToArray();
                Assert.That(real, Is.Not.Empty, example.id + " needs a real copy");
                var actual = ScoreCalculator.Evaluate(Snapshot(example, config), config);
                var expectedTriggers = new HashSet<string>(example.expected.triggeredSourceIds);
                var actualTriggers = new HashSet<string>(actual.triggeredSourceIds);
                if (example.cardId == "C41")
                {
                    // A mimic chain resolves to its terminal real card, never to the C41 definition itself.
                    Assert.That(real.Length, Is.GreaterThanOrEqualTo(2), example.id + " needs multiple real mimics");
                    string sharedTarget = null;
                    for (int i = 0; i < equipped.Length; ++i)
                    {
                        if (equipped[i].definitionId != "C41") continue;
                        int target = i + 1;
                        while (target < equipped.Length && equipped[target].definitionId == "C41") ++target;
                        Assert.That(target, Is.LessThan(equipped.Length), example.id + " each mimic needs a real terminal target");
                        string targetId = equipped[target].instanceId;
                        if (sharedTarget == null) sharedTarget = targetId;
                        Assert.That(targetId, Is.EqualTo(sharedTarget), example.id + " mimics must share the same real target");
                        foreach (string source in new[] { equipped[i].instanceId, targetId })
                        {
                            Assert.That(expectedTriggers.Contains(source), Is.True, example.id + " reference must trigger " + source);
                            Assert.That(actualTriggers.Contains(source), Is.True, example.id + " game must trigger " + source);
                        }
                    }
                    continue;
                }
                bool hasMatchingMimic = false;
                for (int i = 0; i + 1 < equipped.Length; ++i)
                {
                    if (equipped[i].definitionId != "C41") continue;
                    int target = i + 1;
                    while (target < equipped.Length && equipped[target].definitionId == "C41") ++target;
                    if (target == equipped.Length || equipped[target].definitionId != example.cardId) continue;
                    foreach (string source in new[] { equipped[i].instanceId, equipped[target].instanceId })
                    {
                        Assert.That(expectedTriggers.Contains(source), Is.True, example.id + " reference must trigger " + source);
                        Assert.That(actualTriggers.Contains(source), Is.True, example.id + " game must trigger " + source);
                    }
                    hasMatchingMimic = true;
                }
                Assert.That(hasMatchingMimic, Is.True, example.id + " needs a triggered mimic of its named card");
            }
        }

    }
}
