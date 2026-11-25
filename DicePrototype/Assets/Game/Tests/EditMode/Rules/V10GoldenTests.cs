using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DiceCraft.Core.Tests
{
    // p1.1.1 (W4b): the inputs of GoldenScoresV10.json live on as the L10- slice of GoldenScoresV111.json, recomputed
    // under p1.1.1 by tools/v111_golden.py (an independent reference scorer; evidence/v111/rules/golden-provenance.json).
    // The p1.0.0 file itself is frozen at tools/balance_sim/baseline/GoldenScoresV10.p1.0.0.json and no longer read here.
    public class V10GoldenTests
    {
        private const string Prefix = "L10-";
        private GameConfig config;
        [SetUp] public void Setup() { config = V111Golden.ReadConfig(); }
        public static IEnumerable Cases()
        {
            foreach (var item in V111Golden.Slice(Prefix)) yield return new TestCaseData(item.id).SetName("GOLD10-" + item.derivedId);
        }

        // Spec 5.2 step 4: a controller disabled for the stage plans no post-score state change at all. The colour
        // rotation (C45) and the random-factor redraw (C53) are not golden fields, so they are pinned here.
        [Test]
        public void V10_DisabledControllersPlanNoGrowthNoColourRotationAndNoRandomRedraw()
        {
            var template = V111Golden.Snapshot(V111Golden.Case("L10-P001"), config);
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
        public void GoldenV10_EveryExpectedFieldMatchesTheIndependentReference(string id)
        {
            var actual = V111Golden.AssertCase(V111Golden.Case(id), config, out SubmissionSnapshot snapshot);
            // The step-by-step explanation must rebuild the same numbers (it throws on any mismatch).
            var trace = ScoreExplanation.Build(snapshot, actual, config);
            Assert.That(trace.dice.Select(d => d.retriggers), Is.EqualTo(actual.pipRetriggers), id + " trace retriggers");
        }

        [Test]
        public void GoldenV10_FileCoversEveryNewScoringControllerAndEveryScoringModifierEffect()
        {
            var slice = V111Golden.Slice(Prefix);
            Assert.That(V111Golden.Str(V111Golden.Header, "schema"), Is.EqualTo(V111Golden.Schema)); Assert.That(V111Golden.Str(V111Golden.Header, "configVersion"), Is.EqualTo(config.configVersion));
            Assert.That(slice.Length, Is.GreaterThanOrEqualTo(445), "the p1.0.0 inputs that are still legal in p1.1.1"); Assert.That(slice.Select(c => c.derivedId).Distinct().Count(), Is.EqualTo(slice.Length));
            Assert.That(slice.All(c => c.source == "GoldenScoresV10.json"), Is.True);
            var present = new Dictionary<string, int>(); var triggered = new Dictionary<string, int>(); var missed = new Dictionary<string, int>(); var modifiers = new HashSet<string>();
            V09GoldenTests.Tally(slice, config, present, triggered, missed, modifiers);
            // C155 and C156 are new in p1.1.1, so no p1.0.0 input can hold them: they are counted over the whole file.
            var newPresent = new Dictionary<string, int>(); var newTriggered = new Dictionary<string, int>(); var newMissed = new Dictionary<string, int>();
            V09GoldenTests.Tally(V111Golden.Cases.Where(c => c.cardId == "C155" || c.cardId == "C156"), config, newPresent, newTriggered, newMissed);
            // These always count as triggered by rule, so a "miss" case cannot exist for them.
            var always = new HashSet<string> { "C108", "C117", "C118", "C119", "C122", "C123", "C124", "C125", "C129", "C132", "C134", "C141" };
            int checkedCards = 0;
            foreach (var definition in config.controllers.Where(c => int.Parse(c.id.Substring(1)) >= 104))
            {
                bool added = int.Parse(definition.id.Substring(1)) >= 154;
                if (config.enchantRules.nonSubmitControllerIds.Contains(definition.id))
                {
                    Assert.That(triggered.ContainsKey(definition.id) || newTriggered.ContainsKey(definition.id), Is.False, definition.id + " is not a scoring controller and must never trigger.");
                    continue;
                }
                checkedCards++;
                Assert.That((added ? newPresent : present).TryGetValue(definition.id, out int seen) ? seen : 0, Is.GreaterThanOrEqualTo(2), definition.id + " cases");
                Assert.That((added ? newTriggered : triggered).ContainsKey(definition.id), Is.True, definition.id + " has no triggered case");
                if (!always.Contains(definition.id)) Assert.That((added ? newMissed : missed).ContainsKey(definition.id), Is.True, definition.id + " has no untriggered case");
            }
            Assert.That(checkedCards, Is.EqualTo(37), "scoring controllers C104-C156 of p1.1.1");
            foreach (var modifier in config.bossModifiers) Assert.That(modifiers.Contains(modifier.id), Is.True, modifier.id + " never appears in a golden case");
        }
    }
}
