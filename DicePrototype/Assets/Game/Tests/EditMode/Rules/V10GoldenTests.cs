using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Core.Tests
{
    // GoldenScoresV10.json is produced by tools/v10_golden.py, an independent reference scorer written from
    // docs/specs/v10/RULES_SPEC.md without reading this C# code (evidence/v10/rules/golden-provenance.json).
    [Serializable] public sealed class V10GoldenFile { public string schema, configVersion; public V10GoldenCase[] cases; }
    [Serializable] public sealed class V10GoldenCase { public string id, description; public V10GoldenInput input; public V10GoldenExpected expected; public override string ToString() => id; }
    [Serializable] public sealed class V10GoldenDie { public string dieId, faceId, lifetime; public int pip; public bool glass; public V09GoldenFace[] faces; }
    [Serializable]
    public sealed class V10GoldenInput
    {
        public V10GoldenDie[] dice; public V09GoldenPoolDie[] pool; public V09GoldenController[] controllers;
        public int capacity, rollsRemaining, rollsUsed, submissionsRemainingBefore, permanentDiceCount, submissionOrdinal, inventoryCount, ordinaryControllerSlots, scoreItemMultiplier;
        public long wallet; public bool redWave, blueWave; public string previousPrimary, stageKind, modifierId;
        public string[] disabledControllerIds, stageHandsUsed;
        public HandLevel[] handLevels, colorLevels; public RandomMultiplierChoice[] randomChoices;
    }
    [Serializable] public sealed class V10GoldenFactor { public string emitterId, abilityId, numerator, denominator; }
    [Serializable] public sealed class V10GoldenFaceChange { public string sourceId, emitterId, dieId, faceId, kind, text; public int amount; }
    [Serializable]
    public sealed class V10GoldenExpected
    {
        public string score, baseTotal, pipSum, primaryHandId, colorId, controllerMultiplierNumerator, controllerMultiplierDenominator;
        public long[] pipContributions; public int[] pipRetriggers; public string[] triggeredSourceIds; public bool[] sealedDice;
        public int totalHandMult100, handMultAdd100, handMultBeforeModifier100, colorMult100, redCount, redPoolCount, redBonusCount, redMult100, goldSpent;
        public V09GoldenBaseAdd[] controllerBaseAdds; public V09GoldenGrowth[] growthChanges; public V10GoldenFactor[] globalFactors; public V10GoldenFaceChange[] faceChanges;
    }

    public class V10GoldenTests
    {
        private GameConfig config;
        private static string DataPath(string name) => Path.Combine(Application.dataPath, "Game", "Data", name);
        private static V10GoldenFile ReadFile() => JsonUtility.FromJson<V10GoldenFile>(File.ReadAllText(DataPath("GoldenScoresV10.json")));
        [SetUp] public void Setup()
        {
            string json = File.ReadAllText(DataPath("P0Config.json")); GameConfig.ValidateJson(json);
            config = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json)); config.Validate();
        }
        public static IEnumerable Cases()
        {
            foreach (var item in ReadFile().cases) yield return new TestCaseData(item).SetName("GOLD10-" + item.id);
        }
        private static SubmissionSnapshot Snapshot(V10GoldenCase example, GameConfig cfg)
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

        // Spec 5.2 step 4: a controller disabled for the stage plans no post-score state change at all. The colour
        // rotation (C45) and the random-factor redraw (C53) are not golden fields, so they are pinned here.
        [Test]
        public void V10_DisabledControllersPlanNoGrowthNoColourRotationAndNoRandomRedraw()
        {
            var template = Snapshot(ReadFile().cases.First(c => c.id == "P001"), config);
            ControllerInstance Card(string instanceId, string definitionId, int serial) => new ControllerInstance
            {
                instanceId = instanceId, definitionId = definitionId, enchantmentId = "none", acquiredSerial = serial,
                state = new ControllerDynamicState { activeColor = definitionId == "C45" ? "red" : null }
            };
            SubmissionSnapshot With(string[] disabled)
            {
                var s = template.Clone();
                s.equippedControllers = new[] { Card("a45", "C45", 1), Card("a53", "C53", 2), Card("a55", "C55", 3) };
                s.disabledControllerIds = disabled;
                s.resolvedRandomChoices = disabled.Contains("a53") ? Array.Empty<RandomMultiplierChoice>() : new[] { new RandomMultiplierChoice { ownerInstanceId = "a53", units2 = 2 } };
                return s;
            }
            var live = ScoreCalculator.Evaluate(With(Array.Empty<string>()), config).afterScorePlan;
            CollectionAssert.AreEqual(new[] { "a45" }, live.nextColorOwnerIds); CollectionAssert.AreEqual(new[] { "a53" }, live.randomFactorOwnerIds);
            CollectionAssert.AreEqual(new[] { "a55" }, live.growthChanges.Select(g => g.ownerInstanceId));
            var result = ScoreCalculator.Evaluate(With(new[] { "a45", "a53", "a55" }), config); var plan = result.afterScorePlan;
            Assert.That(plan.nextColorOwnerIds, Is.Empty); Assert.That(plan.randomFactorOwnerIds, Is.Empty); Assert.That(plan.growthChanges, Is.Empty);
            Assert.That(result.triggeredSourceIds, Is.Empty);
            var partly = ScoreCalculator.Evaluate(With(new[] { "a45" }), config).afterScorePlan;
            Assert.That(partly.nextColorOwnerIds, Is.Empty); CollectionAssert.AreEqual(new[] { "a53" }, partly.randomFactorOwnerIds);
        }

        [TestCaseSource(nameof(Cases))]
        public void GoldenV10_EveryExpectedFieldMatchesTheIndependentReference(V10GoldenCase example)
        {
            var snapshot = Snapshot(example, config); string before = JsonUtility.ToJson(snapshot);
            var actual = ScoreCalculator.Evaluate(snapshot, config); var e = example.expected; string id = example.id + " " + example.description;
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
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before), "Scoring mutated its input.");
            // The step-by-step explanation must rebuild the same numbers (it throws on any mismatch).
            var trace = ScoreExplanation.Build(snapshot, actual, config);
            Assert.That(trace.dice.Select(d => d.retriggers), Is.EqualTo(actual.pipRetriggers), id + " trace retriggers");
        }

        [Test]
        public void GoldenV10_FileCoversEveryNewScoringControllerAndEveryScoringModifierEffect()
        {
            var file = ReadFile();
            // v1.1: the vectors stay frozen at the version they were generated for (same rule as the v0.9 file);
            // p1.1.0 changes no scoring parameter, so every case below must still reproduce field for field.
            Assert.That(file.schema, Is.EqualTo("v10-golden-1")); Assert.That(file.configVersion, Is.EqualTo("p1.0.0"), "the v1.0 vectors stay frozen; p1.1.0 must still reproduce them");
            Assert.That(file.cases.Length, Is.GreaterThanOrEqualTo(300)); Assert.That(file.cases.Select(c => c.id).Distinct().Count(), Is.EqualTo(file.cases.Length));
            var present = new Dictionary<string, int>(); var triggered = new Dictionary<string, int>(); var missed = new Dictionary<string, int>(); var modifiers = new HashSet<string>();
            foreach (var example in file.cases)
            {
                var snapshot = Snapshot(example, config); var actual = ScoreCalculator.Evaluate(snapshot, config);
                if (!string.IsNullOrEmpty(snapshot.modifierId)) modifiers.Add(snapshot.modifierId);
                var sources = ControllerRules.ResolveSources(snapshot.equippedControllers, snapshot.mimicBindings, snapshot.disabledControllerIds);
                foreach (var source in sources.Where(s => s.abilityId != null))
                {
                    present[source.abilityId] = present.TryGetValue(source.abilityId, out int n) ? n + 1 : 1;
                    var bucket = actual.triggeredSourceIds.Contains(source.emitterId) ? triggered : missed;
                    bucket[source.abilityId] = bucket.TryGetValue(source.abilityId, out int m) ? m + 1 : 1;
                }
            }
            // These always count as triggered by rule, so a "miss" case cannot exist for them.
            var always = new HashSet<string> { "C108", "C117", "C118", "C119", "C122", "C123", "C124", "C125", "C129", "C132", "C134", "C141" };
            foreach (var definition in config.controllers.Where(c => int.Parse(c.id.Substring(1)) >= 104))
            {
                if (config.enchantRules.nonSubmitControllerIds.Contains(definition.id))
                {
                    Assert.That(triggered.ContainsKey(definition.id), Is.False, definition.id + " is not a scoring controller and must never trigger.");
                    continue;
                }
                Assert.That(present.TryGetValue(definition.id, out int seen) ? seen : 0, Is.GreaterThanOrEqualTo(2), definition.id + " cases");
                Assert.That(triggered.ContainsKey(definition.id), Is.True, definition.id + " has no triggered case");
                if (!always.Contains(definition.id)) Assert.That(missed.ContainsKey(definition.id), Is.True, definition.id + " has no untriggered case");
            }
            foreach (var modifier in config.bossModifiers) Assert.That(modifiers.Contains(modifier.id), Is.True, modifier.id + " never appears in a golden case");
        }
    }
}
