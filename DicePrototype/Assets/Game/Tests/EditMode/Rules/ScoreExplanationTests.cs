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
        [SetUp] public void Setup()
        {
            string json = File.ReadAllText(DataPath("P0Config.json"));
            GameConfig.ValidateJson(json); config = JsonUtility.FromJson<GameConfig>(json); config.Validate();
        }
        public static IEnumerable GoldenTraces()
        {
            foreach (var golden in GoldenJsonLoader.Read(File.ReadAllText(DataPath("GoldenScores.json"))).cases)
                yield return new TestCaseData(golden).SetName("TRACE-" + golden.id);
        }

        [TestCaseSource(nameof(GoldenTraces))]
        public void Trace_All52GoldenOutputsAndEveryArithmeticLinkRemainExact(GoldenCase golden)
        {
            var snapshot = golden.Snapshot(config);
            string original = JsonUtility.ToJson(snapshot), originalConfig = JsonUtility.ToJson(config);
            var breakdown = ScoreCalculator.Evaluate(snapshot, config);
            string frozen = JsonUtility.ToJson(breakdown);
            var trace = ScoreExplanation.Build(snapshot, breakdown, config);
            string[] fields = {"effectivePips", "effectiveColors", "pipContributions", "pipSum", "unusedRollBonus", "matchedGroupIds", "chosenGroupId", "groupMult100", "straightMult100", "colorMult100", "totalHandMult100", "redValue", "redCount", "redMult100", "controllerMultiplier", "triggeredMultiplierIds", "finalFlatAdd", "score"};
            foreach (string name in fields)
            {
                var field = typeof(ScoreBreakdown).GetField(name);
                if (field.FieldType.IsArray) CollectionAssert.AreEqual((IEnumerable)field.GetValue(golden.expected), (IEnumerable)field.GetValue(breakdown), golden.id + "." + name);
                else Assert.That(field.GetValue(breakdown), Is.EqualTo(field.GetValue(golden.expected)), golden.id + "." + name);
            }
            Assert.That(trace.dice.Length, Is.EqualTo(snapshot.dice.Length));
            for (int i = 0; i < trace.dice.Length; i++)
            {
                Assert.That(trace.dice[i].dieId, Is.EqualTo(snapshot.dice[i].dieId));
                Assert.That(trace.dice[i].contribution, Is.EqualTo(golden.expected.pipContributions[i]));
                decimal value = breakdown.effectivePips[i];
                foreach (var step in trace.dice[i].steps) value = AssertStep(step, value);
                Assert.That(value, Is.EqualTo((decimal)trace.dice[i].contribution));
            }
            decimal running = golden.expected.pipSum;
            foreach (var step in trace.global) running = AssertStep(step, running);
            Assert.That(running, Is.EqualTo((decimal)golden.expected.score));
            CollectionAssert.AreEqual(golden.expected.triggeredMultiplierIds, trace.global.Where(s => s.kind == "controller").Select(s => s.sourceId));
            Assert.That(trace.global.Count(s => s.operation == "floor"), Is.EqualTo(1));
            Assert.That(trace.global[trace.global.Length - 2].kind, Is.EqualTo("floor"));
            Assert.That(trace.global.Last().kind, Is.EqualTo("final_add"));
            Assert.That(trace.global.Last().operand, Is.EqualTo((decimal)golden.expected.finalFlatAdd));
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(original));
            Assert.That(JsonUtility.ToJson(breakdown), Is.EqualTo(frozen));
            Assert.That(JsonUtility.ToJson(config), Is.EqualTo(originalConfig));
        }

        [Test] public void Trace_StackedContributionsAddBeforeGlassThenGold()
        {
            var snapshot = Snapshot(new[] {1}, new[] {"blue"}, 5, "C10", "C13", "C17", "C19");
            snapshot.dice[0].permanentSixFaceSum = 6;
            snapshot.dice[0].isGlass = true; snapshot.dice[0].isGold = true;
            var breakdown = ScoreCalculator.Evaluate(snapshot, config);
            var die = ScoreExplanation.Build(snapshot, breakdown, config).dice.Single();
            CollectionAssert.AreEqual(new[] {"C10", "C13", "C17", "C19", null, null}, die.steps.Select(s => s.sourceId));
            CollectionAssert.AreEqual(new[] {"contribution", "contribution", "contribution", "contribution", "glass", "gold"}, die.steps.Select(s => s.kind));
            CollectionAssert.AreEqual(new decimal[] {1, 5, 7, 10, 15, 30}, die.steps.Select(s => s.before));
            CollectionAssert.AreEqual(new decimal[] {4, 2, 3, 5, 2, 2}, die.steps.Select(s => s.operand));
            CollectionAssert.AreEqual(new decimal[] {5, 7, 10, 15, 30, 60}, die.steps.Select(s => s.after));
            Assert.That(die.contribution, Is.EqualTo(60));
            Assert.That(breakdown.score, Is.EqualTo(60));
        }

        [Test] public void Trace_C12EffectivePipsSuppressOldOddAndOnePointAdditions()
        {
            var snapshot = Snapshot(new[] {1}, new[] {"red"}, 5, "C10", "C12", "C13", "C17");
            var breakdown = ScoreCalculator.Evaluate(snapshot, config);
            var die = ScoreExplanation.Build(snapshot, breakdown, config).dice.Single();
            Assert.That(breakdown.effectivePips[0], Is.EqualTo(2));
            CollectionAssert.AreEqual(new[] {"C17"}, die.steps.Select(s => s.sourceId));
            Assert.That(die.steps[0].before, Is.EqualTo(2m));
            Assert.That(die.steps[0].after, Is.EqualTo(5m));
        }

        [Test] public void Trace_GlobalFractionsSurviveEveryControllerAndC20AddsOnlyAfterFloor()
        {
            var snapshot = Snapshot(new[] {1, 1, 3, 6}, new[] {"red", "red", "blue", "blue"}, 4, "C09", "C18", "C20");
            snapshot.handLevels = new[] {new HandLevel {handId = "pair", level = 1}};
            var breakdown = ScoreCalculator.Evaluate(snapshot, config);
            var steps = ScoreExplanation.Build(snapshot, breakdown, config).global;
            CollectionAssert.AreEqual(new[] {"unused_rolls", "hand", "red_group", "controller", "controller", "floor", "final_add"}, steps.Select(s => s.kind));
            CollectionAssert.AreEqual(new decimal[] {11, 11, 30.25m, 37.8125m, 113.4375m, 226.875m, 226}, steps.Select(s => s.before));
            CollectionAssert.AreEqual(new decimal[] {11, 30.25m, 37.8125m, 113.4375m, 226.875m, 226, 232}, steps.Select(s => s.after));
            Assert.That(steps[3].sourceId, Is.EqualTo("C09"));
            Assert.That(steps[4].sourceId, Is.EqualTo("C18"));
            Assert.That(steps[6].sourceId, Is.EqualTo("C20"));
            Assert.That(steps[6].operand, Is.EqualTo(6m));
            Assert.That(breakdown.score, Is.EqualTo(232));
            decimal prematurelyRounded = decimal.Floor(decimal.Floor(11m * 2.75m) * 1.25m) * 3 * 2 + 6;
            Assert.That(prematurelyRounded, Is.EqualTo(228));
            Assert.That(steps.Last().after, Is.Not.EqualTo(prematurelyRounded), "The explanation must not introduce intermediate floors.");
        }

        [TestCase("contribution")]
        [TestCase("total")]
        public void Trace_RejectsInconsistentFrozenBreakdown(string field)
        {
            var snapshot = Snapshot(new[] {2}, new[] {"blue"}, 5);
            var breakdown = ScoreCalculator.Evaluate(snapshot, config);
            if (field == "contribution") breakdown.pipContributions[0]++;
            else breakdown.score++;
            Assert.Throws<ArgumentException>(() => ScoreExplanation.Build(snapshot, breakdown, config));
        }

        private SubmissionSnapshot Snapshot(int[] pips, string[] colors, int capacity, params string[] controllers)
        {
            return new SubmissionSnapshot
            {
                runId = "trace", stageId = "STAGE_01", handId = "H001", configVersion = config.configVersion,
                capacity = capacity, equippedControllerIds = controllers,
                dice = pips.Select((p, i) => new SubmissionDie {dieId = "D" + (i + 1).ToString("D3"), faceId = "F01", currentPip = p, currentColor = colors[i], permanentSixFaceSum = 21, lifetime = "permanent"}).ToArray()
            };
        }
        private static decimal AssertStep(ScoreStep step, decimal previous)
        {
            Assert.That(step.before, Is.EqualTo(previous), step.kind + " must link to the previous exact value.");
            decimal expected;
            if (step.operation == "+") expected = previous + step.operand;
            else if (step.operation == "×") expected = previous * step.operand;
            else { Assert.That(step.operation, Is.EqualTo("floor")); expected = decimal.Floor(previous); }
            Assert.That(step.after, Is.EqualTo(expected), step.kind);
            return step.after;
        }
    }
}
