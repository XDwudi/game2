using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Core.Tests
{
    public sealed class ControllerScoreTests
    {
        private GameConfig config;
        [SetUp]
        public void Setup()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Game", "Data", "P0Config.json"));
            GameConfig.ValidateJson(json);
            config = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json));
            config.Validate();
        }
        private static TestCaseData Case(string id, string label, string pips, string colors, long score,
            int capacity = 5, int permanentSum = 21, bool equipped = true, string expectedPips = null, string expectedColors = null)
            => new TestCaseData(id, pips, colors, score, capacity, permanentSum, equipped, expectedPips, expectedColors).SetName("CONTENT-" + id + "-" + label);
        public static IEnumerable Cases()
        {
            yield return Case("C02", "hit", "2,3", "BB", 45);
            yield return Case("C02", "miss-red", "2,3", "BR", 5);
            yield return Case("C02", "boundary-single", "2", "B", 18);
            yield return Case("C03", "hit", "4,4", "BB", 16, expectedColors: "RB");
            yield return Case("C03", "not-equipped", "4,4", "BB", 48, equipped: false, expectedColors: "BB");
            yield return Case("C03", "boundary-single", "2", "B", 4, expectedColors: "R");
            yield return Case("C06", "hit", "1,2,3,4", "RBBR", 30);
            yield return Case("C06", "miss-two-distinct", "1,1,2", "RBB", 8);
            yield return Case("C06", "boundary-duplicate-with-four-distinct", "1,2,3,3,4", "RBBBR", 78);
            yield return Case("C08", "hit", "3", "B", 18);
            yield return Case("C08", "miss-full", "1,2,3,4,6", "RBBRB", 16);
            yield return Case("C08", "boundary-one-empty-slot", "2,2,3,3", "BBBB", 180);
            yield return Case("C09", "hit", "2", "B", 18, capacity: 4);
            yield return Case("C09", "not-equipped", "2", "B", 6, equipped: false);
            yield return Case("C09", "boundary-full-reduced-capacity", "1,2,3,4", "RBBR", 30, capacity: 4);
            yield return Case("C10", "hit", "1", "R", 22);
            yield return Case("C10", "miss-pip-two", "2", "B", 6);
            yield return Case("C10", "boundary-contribution-does-not-change-pair", "1,1", "RB", 44);
            yield return Case("C11", "hit", "2,2,3,3", "BBBB", 540);
            yield return Case("C11", "miss-one-three", "2,2,3,4", "BBBR", 22);
            yield return Case("C11", "boundary-extra-die-allowed", "2,2,3,3,1", "BBBBR", 198);
            yield return Case("C12", "hit", "1,2", "RB", 8, expectedPips: "2,2");
            yield return Case("C12", "miss-even-unchanged", "2,4", "BB", 18, expectedPips: "2,4");
            yield return Case("C12", "boundary-five-to-six", "5,6", "BB", 72, expectedPips: "6,6");
            yield return Case("C13", "hit", "3", "B", 21);
            yield return Case("C13", "miss-even", "4", "B", 12);
            yield return Case("C13", "boundary-red-group-uses-original-one", "1,1", "RR", 70);
            yield return Case("C14", "hit", "3", "B", 27);
            yield return Case("C14", "miss-mixed-parity", "3,2", "BB", 15);
            yield return Case("C14", "boundary-single-one", "1", "R", 6);
            yield return Case("C17", "hit", "3", "B", 27);
            yield return Case("C17", "miss-four", "4", "B", 12);
            yield return Case("C17", "boundary-all-three-small-values", "1,2,3", "RBB", 24);
            yield return Case("C18", "hit", "2,2", "RB", 32);
            yield return Case("C18", "miss-unequal", "2,2,2", "RRB", 42);
            yield return Case("C18", "boundary-two-different-pips", "3,6", "RB", 36);
            yield return Case("C19", "hit-threshold", "1", "R", 18, permanentSum: 13);
            yield return Case("C19", "miss-above-threshold", "1", "R", 2, permanentSum: 14);
            yield return Case("C19", "boundary-remote-current-six-permanent-ones", "6", "B", 42, permanentSum: 6);
            yield return Case("C20", "hit", "1,2,3,4,6", "RBBRB", 31);
            yield return Case("C20", "miss-last-five", "1,2,3,4,5", "RBBRB", 150);
            yield return Case("C20", "boundary-last-occupied-not-capacity-slot", "6", "B", 18);
        }

        [TestCaseSource(nameof(Cases))]
        public void ExplicitControllerOutcomes(string id, string pipText, string colors, long expectedScore,
            int capacity, int permanentSum, bool equipped, string expectedPips, string expectedColors)
        {
            int[] pips = pipText.Split(',').Select(int.Parse).ToArray();
            var snapshot = new SubmissionSnapshot
            {
                configVersion = config.configVersion,
                capacity = capacity,
                equippedControllerIds = equipped ? new[] { id } : Array.Empty<string>(),
                dice = pips.Select((p, i) => new SubmissionDie
                {
                    dieId = "D" + (i + 1).ToString("D3"),
                    faceId = "F01",
                    currentPip = p,
                    currentColor = colors[i] == 'R' ? "red" : "blue",
                    permanentSixFaceSum = permanentSum
                }).ToArray()
            };
            string before = JsonUtility.ToJson(snapshot);
            ScoreBreakdown actual = ScoreCalculator.Evaluate(snapshot, config);
            Assert.That(actual.score, Is.EqualTo(expectedScore.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            if (expectedPips != null) CollectionAssert.AreEqual(expectedPips.Split(',').Select(int.Parse), actual.effectivePips);
            if (expectedColors != null) CollectionAssert.AreEqual(expectedColors.Select(c => c == 'R' ? "red" : "blue"), actual.effectiveColors);
            Assert.That(JsonUtility.ToJson(snapshot), Is.EqualTo(before));
        }

        // ---- p1.1.0 (docs/specs/v11/RULES_DELTA.md): several copies of one card, worked out from the rule text ----
        // No die rolls are left, so there is no unused-roll bonus; levels are 0 (pair x2, all blue x3, all red x2).
        private static TestCaseData Copies(string id, int copies, string label, string pips, string colors, long score, int capacity = 5, string expectedPips = null, string expectedColors = null)
            => new TestCaseData(id, copies, pips, colors, score, capacity, expectedPips, expectedColors).SetName("CONTENT-DUP-" + id + "x" + copies + "-" + label);
        public static IEnumerable DuplicateCases()
        {
            // Multipliers multiply per copy: 5 x 3 (all blue) x 3 x 3.
            yield return Copies("C02", 2, "multiplier-per-copy", "2,3", "BB", 135);
            // Per-die adds add per copy: (1 + 10 + 10) x 2 (all red).
            yield return Copies("C10", 2, "die-add-per-copy", "1", "R", 42);
            // Whole-hand base adds add per copy: (2 + 2 + 10 + 10) x 2 (pair) x 3 (all blue).
            yield return Copies("C27", 2, "base-add-per-copy", "2,2", "BB", 144);
            // Retriggers add per copy: the first die counts 1 + 1 + 1 times: 6 x 3 x 3 (all blue).
            yield return Copies("C64", 2, "retrigger-per-copy", "6", "B", 54);
            // The middle die is counted 1 + 2 + 2 times: (1 + 2 x 5 + 3) x 3 (all blue).
            yield return Copies("C109", 2, "middle-retrigger-per-copy", "1,2,3", "BBB", 42);
            // Hand multiplier adds add per copy: 6 x (2 + 1.5 + 1.5) x 3 (all blue).
            yield return Copies("C84", 2, "hand-add-per-copy", "3,3", "BB", 90);
            // The last-slot add applies per copy: 1 + 2 + 3 + 4 + (6 + 15 + 15), scatter, mixed colours, no red group.
            yield return Copies("C20", 2, "slot-add-per-copy", "1,2,3,4,6", "RBBRB", 46);
            // Four copies of C47, each x(4 / 2): 2 x 3 (all blue) x 2^4.
            yield return Copies("C47", 4, "count-multiplier-per-copy", "2", "B", 96);
            // Switches apply once: odd -> even (+1 only once): pair of 2s, 4 x 2 (pair), mixed colours, red group of 1.
            yield return Copies("C12", 2, "switch-applies-once", "1,2", "RB", 8, expectedPips: "2,2");
            // The first die turns red once: pair of 4s, 8 x 2 (pair), mixed colours, red group of 1.
            yield return Copies("C03", 2, "switch-applies-once", "4,4", "BB", 16, expectedColors: "RB");
            // The red group counts one extra die once: 8 x 2 (pair) x 2 (all red) x 2.5 (red group 2 + 1 = 3).
            yield return Copies("C146", 2, "red-bonus-applies-once", "4,4", "RR", 80);
        }

        [TestCaseSource(nameof(DuplicateCases))]
        public void SeveralCopiesOfOneCardStackPerCopyOrApplyOnce(string id, int copies, string pipText, string colors, long expectedScore,
            int capacity, string expectedPips, string expectedColors)
        {
            int[] pips = pipText.Split(',').Select(int.Parse).ToArray();
            var snapshot = new SubmissionSnapshot
            {
                configVersion = config.configVersion, capacity = capacity,
                equippedControllers = Enumerable.Range(1, copies).Select(i => new ControllerInstance { definitionId = id, instanceId = "copy-" + i, acquiredSerial = i }).ToArray(),
                dice = pips.Select((p, i) => new SubmissionDie { dieId = "D" + (i + 1).ToString("D3"), faceId = "F01", currentPip = p, currentColor = colors[i] == 'R' ? "red" : "blue", permanentSixFaceSum = 21 }).ToArray()
            };
            ScoreBreakdown actual = ScoreCalculator.Evaluate(snapshot, config);
            Assert.That(actual.score, Is.EqualTo(expectedScore.ToString(System.Globalization.CultureInfo.InvariantCulture)));
            Assert.That(actual.triggeredSourceIds, Is.EqualTo(Enumerable.Range(1, copies).Select(i => "copy-" + i)), "every copy is its own triggered source");
            if (expectedPips != null) CollectionAssert.AreEqual(expectedPips.Split(',').Select(int.Parse), actual.effectivePips);
            if (expectedColors != null) CollectionAssert.AreEqual(expectedColors.Select(c => c == 'R' ? "red" : "blue"), actual.effectiveColors);
            Assert.That(ScoreExplanation.Build(snapshot, actual, config).global.Last().after, Is.EqualTo(actual.score), "the explanation attributes every copy");
        }

        [Test]
        public void CopiesKeepTheirOwnGrowthColourRandomOutcomeAndGoldBudget()
        {
            Func<int, string, SubmissionSnapshot> one = (pip, color) => new SubmissionSnapshot
            {
                configVersion = config.configVersion, capacity = 5,
                dice = new[] { new SubmissionDie { dieId = "D001", faceId = "F01", currentPip = pip, currentColor = color, permanentSixFaceSum = 21 } }
            };
            Func<string, int, ControllerInstance> card = (id, i) => new ControllerInstance { definitionId = id, instanceId = id + "-" + i, acquiredSerial = i };
            // Growth is per instance: x(20 + 10) / 20 and x(20 + 0) / 20 on a blue 2: 2 x 3 x 1.5 = 9.
            var grown = one(2, "blue"); var a = card("C54", 1); a.state.growth = 10; grown.equippedControllers = new[] { a, card("C54", 2) };
            var g = ScoreCalculator.Evaluate(grown, config);
            Assert.That(g.score, Is.EqualTo("9")); Assert.That(g.multiplierFactors.Select(f => f.numerator + "/" + f.denominator), Is.EqualTo(new[] { "3/2", "1/1" }));
            // Each C45 multiplies the dice of its own colour: red 4 x 3 and blue 4 x 3, then a pair (x2) in mixed colours: 24 x 2 = 48.
            var colours = new SubmissionSnapshot { configVersion = config.configVersion, capacity = 5,
                dice = new[] { new SubmissionDie { dieId = "D001", faceId = "F01", currentPip = 4, currentColor = "red", permanentSixFaceSum = 21 }, new SubmissionDie { dieId = "D002", faceId = "F01", currentPip = 4, currentColor = "blue", permanentSixFaceSum = 21 } } };
            var red = card("C45", 1); red.state.activeColor = "red"; var blue = card("C45", 2); blue.state.activeColor = "blue";
            colours.equippedControllers = new[] { red, blue }; Assert.That(ScoreCalculator.Evaluate(colours, config).score, Is.EqualTo("48"));
            // Both red: the red die is multiplied twice (4 x 9 = 36), the blue one stays 4: (36 + 4) x 2 = 80.
            blue.state.activeColor = "red"; colours.equippedControllers = new[] { red, blue }; Assert.That(ScoreCalculator.Evaluate(colours, config).score, Is.EqualTo("80"));
            // Each real C53 has its own frozen outcome: x2 and x3 on a blue 2: 2 x 3 x 2 x 3 = 36.
            var random = one(2, "blue"); random.equippedControllers = new[] { card("C53", 1), card("C53", 2) };
            random.resolvedRandomChoices = new[] { new RandomMultiplierChoice { ownerInstanceId = "C53-1", units2 = 4 }, new RandomMultiplierChoice { ownerInstanceId = "C53-2", units2 = 6 } };
            Assert.That(ScoreCalculator.Evaluate(random, config).score, Is.EqualTo("36"));
            // C138 copies share the frozen wallet left to right: 5 gold pays for one (x3, 3 spent), 6 gold for both (x9, 6 spent).
            var spend = one(2, "blue"); spend.equippedControllers = new[] { card("C138", 1), card("C138", 2) };
            spend.wallet = 5; var five = ScoreCalculator.Evaluate(spend, config); Assert.That(five.score, Is.EqualTo("18")); Assert.That(five.afterScorePlan.goldSpent, Is.EqualTo(3));
            spend.wallet = 6; var six = ScoreCalculator.Evaluate(spend, config); Assert.That(six.score, Is.EqualTo("54")); Assert.That(six.afterScorePlan.goldSpent, Is.EqualTo(6));
            // After-score growth is planned for each instance.
            var plan = one(2, "blue"); plan.equippedControllers = new[] { card("C55", 1), card("C55", 2) };
            Assert.That(ScoreCalculator.Evaluate(plan, config).afterScorePlan.growthChanges.Select(c => c.ownerInstanceId + ":" + c.delta), Is.EqualTo(new[] { "C55-1:2", "C55-2:2" }));
        }
    }
}
