using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Core.Tests
{
    // GoldenScoresV09.json is produced by tools/v09_golden.py, an independent reference scorer written from
    // docs/specs/v09/RULES_SPEC.md without reading this C# code (evidence/v09/rules/golden-provenance.json).
    [Serializable] public sealed class V09GoldenFile { public string schema, configVersion; public V09GoldenCase[] cases; }
    [Serializable] public sealed class V09GoldenCase { public string id, description; public V09GoldenInput input; public V09GoldenExpected expected; public override string ToString() => id; }
    [Serializable] public sealed class V09GoldenFace { public string faceId, color, texture; public int pip; public bool gold; }
    [Serializable] public sealed class V09GoldenDie { public string dieId, faceId; public int pip; public bool glass; public V09GoldenFace[] faces; }
    [Serializable] public sealed class V09GoldenPoolDie { public string dieId, faceId, color; public int pip; }
    [Serializable] public sealed class V09GoldenController { public string instanceId, definitionId, enchantmentId, activeColor; public long growth; public int turnoverUnits2; }
    [Serializable]
    public sealed class V09GoldenInput
    {
        public V09GoldenDie[] dice; public V09GoldenPoolDie[] pool; public V09GoldenController[] controllers;
        public int capacity, rollsRemaining, rollsUsed, submissionsRemainingBefore, permanentDiceCount, submissionOrdinal;
        public long wallet; public bool redWave; public string previousPrimary;
        public HandLevel[] handLevels, colorLevels; public RandomMultiplierChoice[] randomChoices;
    }
    [Serializable] public sealed class V09GoldenBaseAdd { public string emitterId, abilityId, amount; }
    [Serializable] public sealed class V09GoldenGrowth { public string ownerInstanceId, abilityId; public long delta; public bool reset; }
    [Serializable]
    public sealed class V09GoldenExpected
    {
        public string score, baseTotal, pipSum, primaryHandId, colorId, controllerMultiplierNumerator, controllerMultiplierDenominator;
        public long[] pipContributions; public int[] pipRetriggers; public string[] triggeredSourceIds;
        public int totalHandMult100, handMultAdd100, colorMult100, redCount, redPoolCount, redMult100;
        public V09GoldenBaseAdd[] controllerBaseAdds; public V09GoldenGrowth[] growthChanges;
    }

    public class V09GoldenTests
    {
        private GameConfig config;
        private static string DataPath(string name) => Path.Combine(Application.dataPath, "Game", "Data", name);
        private static V09GoldenFile ReadFile() => JsonUtility.FromJson<V09GoldenFile>(File.ReadAllText(DataPath("GoldenScoresV09.json")));
        [SetUp] public void Setup()
        {
            string json = File.ReadAllText(DataPath("P0Config.json")); GameConfig.ValidateJson(json);
            config = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json)); config.Validate();
        }
        public static IEnumerable Cases()
        {
            foreach (var item in ReadFile().cases) yield return new TestCaseData(item).SetName("GOLD09-" + item.id);
        }
        private static SubmissionSnapshot Snapshot(V09GoldenCase example, GameConfig cfg)
        {
            var input = example.input;
            return new SubmissionSnapshot
            {
                configVersion = cfg.configVersion, capacity = input.capacity, rollsRemaining = input.rollsRemaining, rollsUsed = input.rollsUsed,
                submissionsRemainingBefore = input.submissionsRemainingBefore, permanentDiceCount = input.permanentDiceCount, submissionOrdinalBefore = input.submissionOrdinal,
                wallet = input.wallet, redWave = input.redWave, previousPrimaryHandId = string.IsNullOrEmpty(input.previousPrimary) ? null : input.previousPrimary,
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
                        permanentSixFaceSum = faces.Sum(f => f.permanentPip), permanentFacesCopy = faces, lifetime = "permanent" };
                }).ToArray(),
                poolDice = (input.pool ?? Array.Empty<V09GoldenPoolDie>()).Select(p => new PoolDie { dieId = p.dieId, faceId = p.faceId, currentPip = p.pip, currentColor = p.color }).ToArray()
            };
        }

        [TestCaseSource(nameof(Cases))]
        public void GoldenV09_EveryExpectedFieldMatchesTheIndependentReference(V09GoldenCase example)
        {
            var snapshot = Snapshot(example, config); string before = JsonUtility.ToJson(snapshot);
            var actual = ScoreCalculator.Evaluate(snapshot, config); var e = example.expected; string id = example.id + " " + example.description;
            Assert.That(actual.score, Is.EqualTo(e.score), id + " score");
            Assert.That(actual.baseTotal, Is.EqualTo(e.baseTotal), id + " baseTotal");
            Assert.That(actual.pipSum, Is.EqualTo(e.pipSum), id + " pipSum");
            CollectionAssert.AreEqual(e.pipContributions, actual.pipContributions, id + " pipContributions");
            CollectionAssert.AreEqual(e.pipRetriggers, actual.pipRetriggers, id + " pipRetriggers");
            Assert.That(actual.primaryHandId, Is.EqualTo(e.primaryHandId), id + " primaryHandId");
            Assert.That(actual.totalHandMult100, Is.EqualTo(e.totalHandMult100), id + " totalHandMult100");
            Assert.That(actual.handMultAdd100, Is.EqualTo(e.handMultAdd100), id + " handMultAdd100");
            Assert.That(actual.colorId ?? "", Is.EqualTo(e.colorId ?? ""), id + " colorId");
            Assert.That(actual.colorMult100, Is.EqualTo(e.colorMult100), id + " colorMult100");
            Assert.That(actual.redCount, Is.EqualTo(e.redCount), id + " redCount");
            Assert.That(actual.redPoolCount, Is.EqualTo(e.redPoolCount), id + " redPoolCount");
            Assert.That(actual.redMult100, Is.EqualTo(e.redMult100), id + " redMult100");
            Assert.That(actual.controllerMultiplierNumerator, Is.EqualTo(e.controllerMultiplierNumerator), id + " multiplier numerator");
            Assert.That(actual.controllerMultiplierDenominator, Is.EqualTo(e.controllerMultiplierDenominator), id + " multiplier denominator");
            CollectionAssert.AreEqual(e.triggeredSourceIds, actual.triggeredSourceIds, id + " triggeredSourceIds");
            CollectionAssert.AreEqual((e.controllerBaseAdds ?? Array.Empty<V09GoldenBaseAdd>()).Select(a => a.emitterId + "/" + a.abilityId + "/" + a.amount),
                actual.controllerBaseAdds.Select(a => a.emitterId + "/" + a.abilityId + "/" + a.amount), id + " controllerBaseAdds");
            CollectionAssert.AreEqual((e.growthChanges ?? Array.Empty<V09GoldenGrowth>()).Select(g => g.ownerInstanceId + "/" + g.abilityId + "/" + g.delta + "/" + g.reset),
                actual.afterScorePlan.growthChanges.Select(g => g.ownerInstanceId + "/" + g.abilityId + "/" + g.delta + "/" + g.reset), id + " growthChanges");
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before), "Scoring mutated its input.");
            // The step-by-step explanation must rebuild the same numbers (it throws on any mismatch).
            var trace = ScoreExplanation.Build(snapshot, actual, config);
            Assert.That(trace.dice.Select(d => d.retriggers), Is.EqualTo(actual.pipRetriggers), id + " trace retriggers");
        }

        [Test]
        public void GoldenV09_FileCoversEveryScoringNewControllerBothTriggeredAndPresent()
        {
            var file = ReadFile();
            Assert.That(file.schema, Is.EqualTo("v09-golden-1")); Assert.That(file.configVersion, Is.EqualTo(config.configVersion));
            Assert.That(file.cases.Length, Is.GreaterThanOrEqualTo(150)); Assert.That(file.cases.Select(c => c.id).Distinct().Count(), Is.EqualTo(file.cases.Length));
            var present = new Dictionary<string, int>(); var triggered = new Dictionary<string, int>(); var missed = new Dictionary<string, int>();
            foreach (var example in file.cases)
            {
                var snapshot = Snapshot(example, config); var actual = ScoreCalculator.Evaluate(snapshot, config);
                var sources = ControllerRules.ResolveSources(snapshot.equippedControllers, snapshot.mimicBindings);
                foreach (var source in sources.Where(s => s.abilityId != null))
                {
                    present[source.abilityId] = present.TryGetValue(source.abilityId, out int n) ? n + 1 : 1;
                    var bucket = actual.triggeredSourceIds.Contains(source.emitterId) ? triggered : missed;
                    bucket[source.abilityId] = bucket.TryGetValue(source.abilityId, out int m) ? m + 1 : 1;
                }
            }
            // These always count as triggered by rule, so a "miss" case cannot exist for them.
            var always = new HashSet<string> { "C54", "C55", "C56", "C57", "C58", "C59", "C60", "C61", "C62", "C63", "C64", "C90", "C94", "C100", "C101" };
            foreach (var definition in config.controllers.Where(c => int.Parse(c.id.Substring(1)) >= 54))
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
        }
    }
}
