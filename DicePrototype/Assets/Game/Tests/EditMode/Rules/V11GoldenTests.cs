using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace DiceCraft.Core.Tests
{
    // p1.1.1 (W4b): the inputs of GoldenScoresV11.json live on as the L11- slice of GoldenScoresV111.json, recomputed
    // under p1.1.1 by tools/v111_golden.py (an independent reference scorer; evidence/v111/rules/golden-provenance.json).
    // The p1.1.0 file itself is frozen at tools/balance_sim/baseline/GoldenScoresV11.p1.1.0.json and is no longer read here.
    // The old file declared its own card lists; the new one classifies cases by family instead: an L11- id is
    // "L11-<card>-<family>" (two_hit, two_miss, three_hit, mimic_hit, ...) and a matrix case carries cardId and family.
    public class V11GoldenTests
    {
        private const string Prefix = "L11-";
        // Rule-text classifications: P0 v0.6 Appendix C / section 12 (with v0.8 numerical changes), v0.9 section 4,
        // v1.0 section 6 and RULES_DELTA (p1.1.1) section 4. Kept here so metadata cannot silently reduce coverage.
        // p1.1.1 removed C90.
        private static readonly HashSet<string> AlwaysTriggered = new HashSet<string>(
            "C01,C03,C09,C38,C42,C45,C46,C47,C49,C50,C51,C53,C54,C55,C56,C57,C58,C59,C60,C61,C62,C63,C64,C94,C100,C101,C108,C117,C118,C119,C122,C123,C124,C125,C129,C132,C134,C141".Split(','));
        // The cards that added per die or multiplied the whole score in p1.1.0. Most of the multipliers are additive
        // bonuses in p1.1.1; the lists still name the cards whose stacking needs three copies. C90 left the game.
        private static readonly string[] RequiredPerDieAdditions =
            "C10,C13,C17,C19,C20,C39,C91,C92,C94,C107,C137".Split(',');
        private static readonly string[] RequiredGlobalMultipliers =
            "C02,C06,C08,C09,C11,C14,C18,C28,C30,C31,C32,C33,C40,C42,C46,C47,C49,C51,C53,C54,C56,C57,C58,C60,C61,C63,C71,C74,C75,C77,C81,C82,C83,C86,C87,C88,C89,C100,C101,C102,C103,C104,C105,C106,C129,C130,C132,C134,C138,C139,C141,C144,C147,C148,C149,C150,C151,C152,C153".Split(',');
        // p1.1.1 changed these conditions so that the p1.1.0 input no longer shows what its family name says; the
        // per-card matrix of the new file covers them instead (docs/specs/v111/TEST_CHANGES.md, W4b).
        //   C19, C127, C138: the old two-copy miss now triggers (C19 reads the smallest pip, C127 is no longer limited
        //   to the first hand, C138 costs 2). C91: its condition was replaced, the old inputs never satisfy it.
        private static readonly HashSet<string> MissFromMatrix = new HashSet<string> { "C19", "C127", "C138" };
        private const string Replaced = "C91";
        private GameConfig config;
        private readonly Dictionary<string, ScoreBreakdown> scored = new Dictionary<string, ScoreBreakdown>(StringComparer.Ordinal);
        [SetUp] public void Setup() { config = V111Golden.ReadConfig(); scored.Clear(); }
        public static IEnumerable Cases()
        {
            foreach (var item in V111Golden.Slice(Prefix)) yield return new TestCaseData(item.id).SetName("GOLD11-" + item.derivedId);
        }

        [TestCaseSource(nameof(Cases))]
        public void GoldenV11_EveryExpectedFieldMatchesTheIndependentReference(string id)
        {
            var actual = V111Golden.AssertCase(V111Golden.Case(id), config, out SubmissionSnapshot snapshot);
            // The step-by-step explanation must rebuild the same numbers (it throws on any mismatch).
            var trace = ScoreExplanation.Build(snapshot, actual, config);
            Assert.That(trace.dice.Select(d => d.retriggers), Is.EqualTo(actual.pipRetriggers), id + " trace retriggers");
        }

        private static string Card(V111GoldenCase example) => example.source == "matrix" ? example.cardId : example.derivedId.Substring(0, example.derivedId.IndexOf('-'));
        private static string Family(V111GoldenCase example) => example.source == "matrix" ? example.family : example.derivedId.Substring(example.derivedId.IndexOf('-') + 1);
        private static V111GoldenCase[] Slice() => V111Golden.Slice(Prefix);
        private static V111GoldenCase[] Matrix() => V111Golden.Cases.Where(c => c.source == "matrix").ToArray();
        private ScoreBreakdown Score(V111GoldenCase example)
        {
            if (!scored.TryGetValue(example.id, out var result)) scored[example.id] = result = ScoreCalculator.Evaluate(V111Golden.Snapshot(example, config), config);
            return result;
        }
        private bool HasCopiesCase(V111GoldenCase example, string card, int copies, bool triggered, string family)
            => Card(example) == card && Family(example) == family && V111Golden.AllInstances(example, card, copies, triggered, false, Score(example));

        // The inputs of the three older files, rather than the L11- slice's own names, anchor the required inventory:
        // every card that is a triggered source (as emitter or as the copied ability) in the L08-, L09- or L10- slice.
        // Resolving a mimic here is only fixture bookkeeping: it never computes any expected score.
        private HashSet<string> HistoricalScoringCards()
        {
            var cards = new HashSet<string>(StringComparer.Ordinal);
            foreach (var example in V111Golden.Cases.Where(c => c.id.StartsWith("L08-", StringComparison.Ordinal) || c.id.StartsWith("L09-", StringComparison.Ordinal) || c.id.StartsWith("L10-", StringComparison.Ordinal)))
            {
                var snapshot = V111Golden.Snapshot(example, config);
                var triggered = new HashSet<string>(V111Golden.Strings(example.expected["triggeredSourceIds"]), StringComparer.Ordinal);
                foreach (var source in ControllerRules.ResolveSources(snapshot.equippedControllers, snapshot.mimicBindings, snapshot.disabledControllerIds))
                {
                    if (!triggered.Contains(source.emitterId)) continue;
                    Assert.That(source.abilityId, Is.Not.Null, example.id + " triggers a source without an ability: " + source.emitterId);
                    cards.Add(source.emitterDefinitionId); cards.Add(source.abilityId);
                }
            }
            return cards;
        }

        [Test]
        public void GoldenV11_CoversEveryHistoricalScoringCardWithTwoTriggeringCopies()
        {
            var slice = Slice(); var historical = HistoricalScoringCards();
            Assert.That(V111Golden.Str(V111Golden.Header, "schema"), Is.EqualTo(V111Golden.Schema));
            Assert.That(V111Golden.Str(V111Golden.Header, "configVersion"), Is.EqualTo("p1.1.1"));
            Assert.That(config.configVersion, Is.EqualTo("p1.1.1"));
            Assert.That(slice.Length, Is.GreaterThanOrEqualTo(455));
            Assert.That(slice.Select(c => c.derivedId).Distinct().Count(), Is.EqualTo(slice.Length), "Golden case IDs must be unique.");
            Assert.That(historical.Count, Is.GreaterThanOrEqualTo(120), "The historical scoring inventory must not shrink.");
            var declared = slice.Where(c => Family(c) == "two_hit").Select(Card).ToArray();
            Assert.That(declared.Distinct().Count(), Is.EqualTo(declared.Length));
            // The slice names one more card than the older inputs can still trigger: C91, whose condition was replaced.
            CollectionAssert.AreEquivalent(historical.Concat(new[] { Replaced }), declared, "The historical scoring inventory must not shrink.");
            CollectionAssert.IsSubsetOf(historical.Where(c => c != "C41"), Matrix().Select(c => c.cardId).Distinct().ToArray(), "Every historical scoring card needs its matrix cases.");
            foreach (string card in historical)
                Assert.That(slice.Any(c => HasCopiesCase(c, card, 2, true, "two_hit")), Is.True,
                    card + " needs two distinct real instances that both trigger in the reference and the game.");
            Assert.That(Matrix().Any(c => HasCopiesCase(c, Replaced, 2, true, "two_hit")), Is.True, Replaced + " needs two real copies that both trigger under its new condition.");
            foreach (var example in slice)
            {
                var ids = V111Golden.List(example.input["controllers"]).Select(V111Golden.Obj).Select(c => V111Golden.Str(c, "instanceId")).ToArray();
                Assert.That(ids.Distinct().Count(), Is.EqualTo(ids.Length), example.id + " contains reused instance IDs");
            }
        }

        [Test]
        public void GoldenV11_ConditionalCardsAlsoHaveTwoUntriggeredCopies()
        {
            var slice = Slice(); var conditional = HistoricalScoringCards().Except(AlwaysTriggered).Concat(new[] { Replaced }).ToArray();
            Assert.That(conditional, Is.Not.Empty);
            CollectionAssert.AreEquivalent(conditional, slice.Where(c => Family(c) == "two_miss").Select(Card).ToArray(),
                "Every conditionally triggered scoring card requires a natural miss.");
            foreach (string card in conditional)
            {
                if (MissFromMatrix.Contains(card))
                {
                    Assert.That(slice.Any(c => HasCopiesCase(c, card, 2, false, "two_miss")), Is.False, card + " misses again with two copies; drop the matrix exception.");
                    Assert.That(Matrix().Any(c => HasCopiesCase(c, card, 1, false, "miss")), Is.True, card + " needs a natural condition miss in the matrix, without disabling the instance.");
                    continue;
                }
                Assert.That(slice.Any(c => HasCopiesCase(c, card, 2, false, "two_miss")), Is.True,
                    card + " needs a natural condition miss for both copies, without disabling either instance.");
            }
        }

        [Test]
        public void GoldenV11_MultiplierAndPerDieAdditionCardsHaveThreeTriggeringCopies()
        {
            var slice = Slice(); var declared = slice.Where(c => Family(c) == "three_hit").Select(Card).ToArray();
            CollectionAssert.IsSubsetOf(RequiredGlobalMultipliers, declared, "Global multiplier coverage cannot shrink.");
            CollectionAssert.IsSubsetOf(RequiredPerDieAdditions, declared, "Per-die addition coverage cannot shrink.");
            foreach (string card in declared.Distinct())
            {
                if (card == Replaced)
                {
                    // No case of the new file holds three copies of C91 that trigger under its new condition; two do.
                    Assert.That(slice.Any(c => HasCopiesCase(c, card, 3, true, "three_hit")), Is.False, card + " triggers again with three copies; drop the exception.");
                    Assert.That(Matrix().Any(c => HasCopiesCase(c, card, 2, true, "two_hit")), Is.True, card + " needs two real copies that both trigger.");
                    continue;
                }
                Assert.That(slice.Any(c => HasCopiesCase(c, card, 3, true, "three_hit")), Is.True, card + " needs three real copies that all trigger.");
            }
        }

        [Test]
        public void GoldenV11_MixesRealCopiesWithMimicsOfTheSameAbility()
        {
            var mixed = Slice().Concat(Matrix()).Where(c => Family(c) == "mimic_hit").ToArray();
            Assert.That(mixed, Is.Not.Empty);
            foreach (string card in HistoricalScoringCards().Concat(new[] { Replaced }))
                Assert.That(mixed.Any(c => Card(c) == card), Is.True, card + " needs a mixed mimic case");
            int checkedCases = 0;
            foreach (var example in mixed)
            {
                // The p1.1.0 input for C91 no longer satisfies its condition; the matrix case of C91 is checked below.
                if (example.id == "L11-" + Replaced + "-mimic_hit") continue;
                checkedCases++;
                string cardId = Card(example);
                var equipped = V111Golden.List(example.input["controllers"]).Select(V111Golden.Obj).ToArray();
                Func<int, string> definition = i => V111Golden.Str(equipped[i], "definitionId"); Func<int, string> instance = i => V111Golden.Str(equipped[i], "instanceId");
                Assert.That(example.input["bindings"], Is.Null, example.id + " must use adjacency");
                var real = equipped.Where(c => V111Golden.Str(c, "definitionId") == cardId).ToArray();
                Assert.That(real, Is.Not.Empty, example.id + " needs a real copy");
                var actual = Score(example);
                var expectedTriggers = new HashSet<string>(V111Golden.Strings(example.expected["triggeredSourceIds"]));
                var actualTriggers = new HashSet<string>(actual.triggeredSourceIds);
                if (cardId == "C41")
                {
                    // A mimic chain resolves to its terminal real card, never to the C41 definition itself.
                    Assert.That(real.Length, Is.GreaterThanOrEqualTo(2), example.id + " needs multiple real mimics");
                    string sharedTarget = null;
                    for (int i = 0; i < equipped.Length; ++i)
                    {
                        if (definition(i) != "C41") continue;
                        int target = i + 1;
                        while (target < equipped.Length && definition(target) == "C41") ++target;
                        Assert.That(target, Is.LessThan(equipped.Length), example.id + " each mimic needs a real terminal target");
                        string targetId = instance(target);
                        if (sharedTarget == null) sharedTarget = targetId;
                        Assert.That(targetId, Is.EqualTo(sharedTarget), example.id + " mimics must share the same real target");
                        foreach (string source in new[] { instance(i), targetId })
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
                    if (definition(i) != "C41") continue;
                    int target = i + 1;
                    while (target < equipped.Length && definition(target) == "C41") ++target;
                    if (target == equipped.Length || definition(target) != cardId) continue;
                    foreach (string source in new[] { instance(i), instance(target) })
                    {
                        Assert.That(expectedTriggers.Contains(source), Is.True, example.id + " reference must trigger " + source);
                        Assert.That(actualTriggers.Contains(source), Is.True, example.id + " game must trigger " + source);
                    }
                    hasMatchingMimic = true;
                }
                Assert.That(hasMatchingMimic, Is.True, example.id + " needs a triggered mimic of its named card");
            }
            Assert.That(checkedCases, Is.GreaterThanOrEqualTo(242));
        }
    }
}
