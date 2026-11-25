using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Core.Tests
{
    public sealed class ScoreExplanationTests
    {
        private GameConfig config;
        private static string DataPath(string name) => Path.Combine(Application.dataPath, "Game/Data", name);
        [SetUp]
        public void Setup()
        {
            string json = File.ReadAllText(DataPath("P0Config.json"));
            GameConfig.ValidateJson(json); config = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json)); config.Validate();
        }
        public static IEnumerable GoldenTraces()
        {
            // p1.1.1 (W4b): the same 160 inputs, recomputed under p1.1.1 by the independent reference (ids L08-G001...).
            foreach (var golden in V111Golden.Slice("L08-"))
                yield return new TestCaseData(golden.id).SetName("TRACE-" + golden.derivedId);
        }

        [TestCaseSource(nameof(GoldenTraces))]
        public void Trace_All160GoldenOutputsAndEveryArithmeticLinkRemainExact(string id)
        {
            string originalConfig = JsonUtility.ToJson(config);
            AssertTrace(V111Golden.Case(id));
            Assert.That(JsonUtility.ToJson(config), Is.EqualTo(originalConfig));
        }

        // p1.1.1 (W4b): the same chain of checks on every other input of the p1.1.1 file (the reworked L09-L11 inputs,
        // the new cases and the per-card matrix), so that the additive zone and the new cards are explained too.
        [Test]
        public void Trace_EveryOtherP111GoldenInputAlsoRebuildsStepByStep()
        {
            var others = V111Golden.Cases.Where(c => !c.id.StartsWith("L08-", StringComparison.Ordinal)).ToArray();
            Assert.That(others.Length, Is.GreaterThanOrEqualTo(1992));
            var failures = new System.Collections.Generic.List<string>();
            foreach (var golden in others)
            {
                try { AssertTrace(golden); }
                catch (Exception ex) { failures.Add(golden.id + ": " + ex.Message.Split('\n')[0].Trim()); }
            }
            Assert.That(failures, Is.Empty, failures.Count + " inputs, first: " + string.Join(" | ", failures.Take(10)));
        }

        private void AssertTrace(V111GoldenCase golden)
        {
            var snapshot = V111Golden.Snapshot(golden, config);
            string original = JsonUtility.ToJson(snapshot);
            var breakdown = ScoreCalculator.Evaluate(snapshot, config);
            string frozen = JsonUtility.ToJson(breakdown);
            var trace = ScoreExplanation.Build(snapshot, breakdown, config);
            Assert.That(V111Golden.Differences(golden, breakdown, config), Is.Empty, golden.id + " differs from the independent reference");
            var contributions = V111Golden.List(golden.expected["pipContributions"]);
            Assert.That(trace.dice.Length, Is.EqualTo(snapshot.dice.Length));
            for (int i = 0; i < trace.dice.Length; i++)
            {
                Assert.That(trace.dice[i].dieId, Is.EqualTo(snapshot.dice[i].dieId));
                Assert.That(trace.dice[i].contribution, Is.EqualTo((long)contributions[i]));
                Assert.That(trace.dice[i].retriggers, Is.EqualTo(breakdown.pipRetriggers[i]));
                string value = breakdown.effectivePips[i].ToString();
                foreach (var step in trace.dice[i].steps) value = AssertStep(step, value);
                // The chain of a die ends at its total: the contribution, plus one "+ contribution x count" step per
                // retrigger source (v0.9). Without retriggers the total is the contribution itself.
                Assert.That(value, Is.EqualTo(trace.dice[i].total));
                Assert.That(trace.dice[i].total, Is.EqualTo((trace.dice[i].contribution * (1 + trace.dice[i].retriggers)).ToString()));
            }
            string running = breakdown.pipSum;
            foreach (var step in trace.global) running = AssertStep(step, running);
            Assert.That(running, Is.EqualTo(V111Golden.Str(golden.expected, "score")));
            CollectionAssert.AreEqual(breakdown.triggeredMultiplierIds, trace.global.Where(s => s.kind == "controller").Select(s => s.sourceId));
            Assert.That(trace.global.Count(s => s.operation == "floor"), Is.EqualTo(1));
            Assert.That(trace.global.Last().kind, Is.EqualTo("floor"));
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(original));
            Assert.That(JsonUtility.ToJson(breakdown), Is.EqualTo(frozen));
        }

        [Test]
        public void Trace_StackedContributionsAddBeforeGlassAndGoldMovesToTheAdditiveLine()
        {
            // p1.1.1 rewrite of Trace_StackedContributionsAddBeforeGlassThenGold (docs/specs/v111/TEST_CHANGES.md):
            // C10 +15, C13 +8 and C17 +9 add to the pip, glass x3; C19 and the gold face are additive bonuses now.
            var snapshot = Snapshot(new[] { 1 }, new[] { "blue" }, 5, "C10", "C13", "C17", "C19");
            snapshot.dice[0].permanentSixFaceSum = 6;
            snapshot.dice[0].isGlass = true; snapshot.dice[0].isGold = true;
            var breakdown = ScoreCalculator.Evaluate(snapshot, config);
            var trace = ScoreExplanation.Build(snapshot, breakdown, config); var die = trace.dice.Single();
            CollectionAssert.AreEqual(new[] { "C10", "C13", "C17", null }, die.steps.Select(s => s.sourceId));
            CollectionAssert.AreEqual(new[] { "contribution", "contribution", "contribution", "glass" }, die.steps.Select(s => s.kind));
            CollectionAssert.AreEqual(new string[] { "1", "16", "24", "33" }, die.steps.Select(s => s.before));
            CollectionAssert.AreEqual(new string[] { "15", "8", "9", "3" }, die.steps.Select(s => s.operand));
            CollectionAssert.AreEqual(new string[] { "16", "24", "33", "99" }, die.steps.Select(s => s.after));
            Assert.That(die.contribution, Is.EqualTo(99));
            // A = all blue 0.25 + gold face 0.5 + C19 (smallest pip 1) 2.5 = 3.25: 99 x 4.25 = 420.75.
            CollectionAssert.AreEqual(new[] { "color:25", "gold:50", "controller:250" }, trace.global.Single(s => s.kind == "additive").entries.Select(e => e.kind + ":" + e.amount100));
            Assert.That(breakdown.score, Is.EqualTo("420"));
        }

        [Test]
        public void Trace_C12EffectivePipsSuppressOldOddAndOnePointAdditions()
        {
            var snapshot = Snapshot(new[] { 1 }, new[] { "red" }, 5, "C10", "C12", "C13", "C17");
            var breakdown = ScoreCalculator.Evaluate(snapshot, config);
            var die = ScoreExplanation.Build(snapshot, breakdown, config).dice.Single();
            Assert.That(breakdown.effectivePips[0], Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] { "C17" }, die.steps.Select(s => s.sourceId));
            Assert.That(die.steps[0].before, Is.EqualTo("2"));
            Assert.That(die.steps[0].after, Is.EqualTo("11")); // p1.1.1: C17 adds 9
        }

        [Test]
        public void Trace_GlobalFractionsSurviveEveryControllerAndC20AddsBeforeMultipliers()
        {
            var snapshot = Snapshot(new[] { 1, 1, 3, 6 }, new[] { "red", "red", "blue", "blue" }, 4, "C09", "C18", "C20");
            snapshot.handLevels = new[] { new HandLevel { handId = "pair", level = 1 } };
            var breakdown = ScoreCalculator.Evaluate(snapshot, config);
            var steps = ScoreExplanation.Build(snapshot, breakdown, config).global;
            // p1.1.1 rewrite (docs/specs/v111/TEST_CHANGES.md). C20 adds 24 to the last die of a full row: base
            // 1 + 1 + 3 + 30 = 35. Level 1 pair x2.75 = 11/4; two red 1s are a red group of 2 = x1; two red and two
            // blue is mixed, so no colour line; C18 is an additive +5 (x6); C09 x2.5 = 5/2.
            // 35 x 11/4 = 385/4, x6 = 1155/2, x5/2 = 5775/4 = 1443.75.
            CollectionAssert.AreEqual(new[] { "unused_rolls", "hand", "red_group", "additive", "controller", "floor" }, steps.Select(s => s.kind));
            CollectionAssert.AreEqual(new string[] { "35", "35", "385/4", "385/4", "1155/2", "5775/4" }, steps.Select(s => s.before));
            CollectionAssert.AreEqual(new string[] { "35", "385/4", "385/4", "1155/2", "5775/4", "1443" }, steps.Select(s => s.after));
            Assert.That(steps[4].sourceId, Is.EqualTo("C09"));
            CollectionAssert.AreEqual(new[] { "C18" }, steps[3].entries.Select(e => e.abilityId));
            Assert.That(ScoreExplanation.Build(snapshot, breakdown, config).dice.Last().steps.Single().sourceId, Is.EqualTo("C20"));
            Assert.That(breakdown.score, Is.EqualTo("1443"));
            decimal prematurelyRounded = decimal.Floor(decimal.Floor(35m * 2.75m) * 6m) * 2.5m;
            Assert.That(prematurelyRounded, Is.EqualTo(1440));
            Assert.That(steps.Last().after, Is.Not.EqualTo(prematurelyRounded.ToString()), "The explanation must not introduce intermediate floors.");
        }

        [TestCase("contribution")]
        [TestCase("total")]
        public void Trace_RejectsInconsistentFrozenBreakdown(string field)
        {
            var snapshot = Snapshot(new[] { 2 }, new[] { "blue" }, 5);
            var breakdown = ScoreCalculator.Evaluate(snapshot, config);
            if (field == "contribution") breakdown.pipContributions[0]++;
            else breakdown.score = ExactScoreMath.Add(breakdown.score, "1");
            Assert.Throws<ArgumentException>(() => ScoreExplanation.Build(snapshot, breakdown, config));
        }

        private SubmissionSnapshot Snapshot(int[] pips, string[] colors, int capacity, params string[] controllers)
        {
            return new SubmissionSnapshot
            {
                runId = "trace",
                stageId = "STAGE_01",
                handId = "H001",
                configVersion = config.configVersion,
                capacity = capacity,
                equippedControllerIds = controllers,
                dice = pips.Select((p, i) => new SubmissionDie { dieId = "D" + (i + 1).ToString("D3"), faceId = "F01", currentPip = p, currentColor = colors[i], permanentSixFaceSum = 21, lifetime = "permanent" }).ToArray()
            };
        }
        private static string AssertStep(ScoreStep step, string previous)
        {
            Assert.That(step.before, Is.EqualTo(previous), step.kind + " links to previous exact value.");
            var value = ExactFraction.Parse(previous); var operand = ExactFraction.Parse(step.operand);
            string expected = step.operation == "+" ? (value + operand).ToString() : step.operation == "×" ? (value * operand).ToString() : ExactScoreMath.SaveDecimal(value.Numerator / value.Denominator);
            Assert.That(step.after, Is.EqualTo(expected), step.kind); return step.after;
        }
    }
}
