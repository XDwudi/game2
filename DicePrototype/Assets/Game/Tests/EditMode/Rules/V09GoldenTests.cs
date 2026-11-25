using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DiceCraft.Core.Tests
{
    // p1.1.1 (W4b): the inputs of GoldenScoresV09.json live on as the L09- slice of GoldenScoresV111.json, recomputed
    // under p1.1.1 by tools/v111_golden.py (an independent reference scorer; evidence/v111/rules/golden-provenance.json).
    // The p0.9.0 file itself is frozen at tools/balance_sim/baseline/GoldenScoresV09.p0.9.0.json and is no longer read here.
    public class V09GoldenTests
    {
        private const string Prefix = "L09-";
        private GameConfig config;
        [SetUp] public void Setup() { config = V111Golden.ReadConfig(); }
        public static IEnumerable Cases()
        {
            foreach (var item in V111Golden.Slice(Prefix)) yield return new TestCaseData(item.id).SetName("GOLD09-" + item.derivedId);
        }

        [TestCaseSource(nameof(Cases))]
        public void GoldenV09_EveryExpectedFieldMatchesTheIndependentReference(string id)
        {
            var actual = V111Golden.AssertCase(V111Golden.Case(id), config, out SubmissionSnapshot snapshot);
            // The step-by-step explanation must rebuild the same numbers (it throws on any mismatch).
            var trace = ScoreExplanation.Build(snapshot, actual, config);
            Assert.That(trace.dice.Select(d => d.retriggers), Is.EqualTo(actual.pipRetriggers), id + " trace retriggers");
        }

        /// <summary>How often each ability is present, triggered and not triggered over a set of cases. The reference
        /// and the game must agree on every trigger, otherwise the case does not count.</summary>
        internal static void Tally(IEnumerable<V111GoldenCase> cases, GameConfig cfg, Dictionary<string, int> present, Dictionary<string, int> triggered, Dictionary<string, int> missed, HashSet<string> modifiers = null)
        {
            foreach (var example in cases)
            {
                var snapshot = V111Golden.Snapshot(example, cfg); var actual = ScoreCalculator.Evaluate(snapshot, cfg);
                CollectionAssert.AreEqual(V111Golden.Strings(example.expected["triggeredSourceIds"]), actual.triggeredSourceIds, example.id + " triggeredSourceIds");
                if (modifiers != null && !string.IsNullOrEmpty(snapshot.modifierId)) modifiers.Add(snapshot.modifierId);
                var sources = ControllerRules.ResolveSources(snapshot.equippedControllers, snapshot.mimicBindings, snapshot.disabledControllerIds);
                foreach (var source in sources.Where(s => s.abilityId != null))
                {
                    present[source.abilityId] = present.TryGetValue(source.abilityId, out int n) ? n + 1 : 1;
                    var bucket = actual.triggeredSourceIds.Contains(source.emitterId) ? triggered : missed;
                    bucket[source.abilityId] = bucket.TryGetValue(source.abilityId, out int m) ? m + 1 : 1;
                }
            }
        }

        [Test]
        public void GoldenV09_FileCoversEveryScoringNewControllerBothTriggeredAndPresent()
        {
            var slice = V111Golden.Slice(Prefix);
            Assert.That(V111Golden.Str(V111Golden.Header, "schema"), Is.EqualTo(V111Golden.Schema)); Assert.That(V111Golden.Str(V111Golden.Header, "configVersion"), Is.EqualTo(config.configVersion));
            Assert.That(slice.Length, Is.GreaterThanOrEqualTo(257), "the p0.9.0 inputs that are still legal in p1.1.1"); Assert.That(slice.Select(c => c.derivedId).Distinct().Count(), Is.EqualTo(slice.Length));
            Assert.That(slice.All(c => c.id == Prefix + c.derivedId && c.derivedFile.Contains("GoldenScoresV09")), Is.True);
            var present = new Dictionary<string, int>(); var triggered = new Dictionary<string, int>(); var missed = new Dictionary<string, int>();
            Tally(slice, config, present, triggered, missed);
            // p1.1.1 replaced the condition of C91 (RULES_DELTA 4.4): none of the p0.9.0 inputs satisfies the new one,
            // so its triggered case is looked up in the whole file (the per-card matrix has it).
            var wholePresent = new Dictionary<string, int>(); var wholeTriggered = new Dictionary<string, int>(); var wholeMissed = new Dictionary<string, int>();
            Tally(V111Golden.Cases.Where(c => c.cardId == "C91"), config, wholePresent, wholeTriggered, wholeMissed);
            Assert.That(triggered.ContainsKey("C91"), Is.False, "C91 now triggers in the p0.9.0 inputs; drop the whole-file exception.");
            // These always count as triggered by rule, so a "miss" case cannot exist for them (C90 left the game in p1.1.1).
            var always = new HashSet<string> { "C54", "C55", "C56", "C57", "C58", "C59", "C60", "C61", "C62", "C63", "C64", "C94", "C100", "C101" };
            int checkedCards = 0;
            foreach (var definition in config.controllers.Where(c => int.Parse(c.id.Substring(1)) >= 54 && int.Parse(c.id.Substring(1)) <= 103)) // C104+ are covered by the L10- slice
            {
                if (config.enchantRules.nonSubmitControllerIds.Contains(definition.id))
                {
                    Assert.That(triggered.ContainsKey(definition.id), Is.False, definition.id + " is not a scoring controller and must never trigger.");
                    continue;
                }
                checkedCards++;
                Assert.That(present.TryGetValue(definition.id, out int seen) ? seen : 0, Is.GreaterThanOrEqualTo(2), definition.id + " cases");
                Assert.That((definition.id == "C91" ? wholeTriggered : triggered).ContainsKey(definition.id), Is.True, definition.id + " has no triggered case");
                if (!always.Contains(definition.id)) Assert.That(missed.ContainsKey(definition.id), Is.True, definition.id + " has no untriggered case");
            }
            Assert.That(checkedCards, Is.EqualTo(42), "scoring controllers C54-C103 of p1.1.1");
        }
    }
}
