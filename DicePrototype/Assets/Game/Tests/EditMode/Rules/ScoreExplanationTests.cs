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
            foreach (var golden in GoldenJsonLoader.Read(File.ReadAllText(DataPath("GoldenScores.json"))).cases)
                yield return new TestCaseData(golden).SetName("TRACE-" + golden.id);
        }

        [TestCaseSource(nameof(GoldenTraces))]
        public void Trace_All160GoldenOutputsAndEveryArithmeticLinkRemainExact(GoldenCase golden)
        {
            var snapshot = golden.Snapshot(config);
            string original = JsonUtility.ToJson(snapshot), originalConfig = JsonUtility.ToJson(config);
            var breakdown = ScoreCalculator.Evaluate(snapshot, config);
            string frozen = JsonUtility.ToJson(breakdown);
            var trace = ScoreExplanation.Build(snapshot, breakdown, config);
            GoldenJsonLoader.AssertExpected(golden.expected, breakdown, golden.id);
            Assert.That(trace.dice.Length, Is.EqualTo(snapshot.dice.Length));
            for (int i = 0; i < trace.dice.Length; i++)
            {
                Assert.That(trace.dice[i].dieId, Is.EqualTo(snapshot.dice[i].dieId));
                Assert.That(trace.dice[i].contribution, Is.EqualTo(golden.expected.pipContributions[i]));
                string value = breakdown.effectivePips[i].ToString();
                foreach (var step in trace.dice[i].steps) value = AssertStep(step, value);
                Assert.That(value, Is.EqualTo(trace.dice[i].contribution.ToString()));
            }
            string running = breakdown.pipSum;
            foreach (var step in trace.global) running = AssertStep(step, running);
            Assert.That(running, Is.EqualTo(golden.expected.score));
            CollectionAssert.AreEqual(breakdown.triggeredMultiplierIds, trace.global.Where(s => s.kind == "controller").Select(s => s.sourceId));
            Assert.That(trace.global.Count(s => s.operation == "floor"), Is.EqualTo(1));
            Assert.That(trace.global.Last().kind, Is.EqualTo("floor"));
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(original));
            Assert.That(JsonUtility.ToJson(breakdown), Is.EqualTo(frozen));
            Assert.That(JsonUtility.ToJson(config), Is.EqualTo(originalConfig));
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
