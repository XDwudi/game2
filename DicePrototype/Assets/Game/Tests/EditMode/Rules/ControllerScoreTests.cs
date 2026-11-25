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
            // p1.1.1 rewrite (docs/specs/v111/RULES_DELTA.md sections 2-4; see docs/specs/v111/TEST_CHANGES.md).
            // No rolls are left, levels are 0: pair x2, two pair x3, trips x4, straight x10; all blue adds 0.25 and
            // all red 0.5 to the additive zone; a red group of 1 or 2 dice is x1. The final value is floored once.
            yield return Case("C02", "hit", "2,3", "BB", 18);                          // 5 x 1.25 x 3 = 18.75
            yield return Case("C02", "miss-red", "2,3", "BR", 5);
            yield return Case("C02", "boundary-single", "2", "B", 7);                  // 2 x 1.25 x 3 = 7.5
            yield return Case("C03", "hit", "4,4", "BB", 16, expectedColors: "RB");    // mixed: 8 x 2
            yield return Case("C03", "not-equipped", "4,4", "BB", 20, equipped: false, expectedColors: "BB"); // 8 x 2 x 1.25
            yield return Case("C03", "boundary-single", "2", "B", 3, expectedColors: "R"); // all red: 2 x 1.5
            yield return Case("C06", "hit", "1,2,3,4", "RBBR", 40);                    // +3: 10 x 4
            yield return Case("C06", "miss-two-distinct", "1,1,2", "RBB", 8);
            yield return Case("C06", "boundary-duplicate-with-four-distinct", "1,2,3,3,4", "RBBBR", 104); // 13 x 2 x 4
            yield return Case("C08", "hit", "3", "B", 9);                              // +2: 3 x 3.25 = 9.75
            yield return Case("C08", "miss-full", "1,2,3,4,6", "RBBRB", 16);
            yield return Case("C08", "boundary-one-empty-slot", "2,2,3,3", "BBBB", 97); // 10 x 3 x 3.25 = 97.5
            yield return Case("C09", "hit", "2", "B", 6, capacity: 4);                 // 2 x 1.25 x 2.5 = 6.25
            yield return Case("C09", "not-equipped", "2", "B", 2, equipped: false);    // 2 x 1.25 = 2.5
            yield return Case("C09", "boundary-full-reduced-capacity", "1,2,3,4", "RBBR", 25, capacity: 4); // 10 x 2.5
            yield return Case("C10", "hit", "1", "R", 24);                             // (1 + 15) x 1.5
            yield return Case("C10", "miss-pip-two", "2", "B", 2);                     // 2 x 1.25 = 2.5
            yield return Case("C10", "boundary-contribution-does-not-change-pair", "1,1", "RB", 64); // 32 x 2
            yield return Case("C11", "hit", "2,2,3,3", "BBBB", 277);                   // +8: 10 x 3 x 9.25 = 277.5
            yield return Case("C11", "miss-one-three", "2,2,3,4", "BBBR", 22);
            yield return Case("C11", "boundary-extra-die-allowed", "2,2,3,3,1", "BBBBR", 297); // 11 x 3 x 9
            yield return Case("C12", "hit", "1,2", "RB", 8, expectedPips: "2,2");
            yield return Case("C12", "miss-even-unchanged", "2,4", "BB", 7, expectedPips: "2,4"); // 6 x 1.25 = 7.5
            yield return Case("C12", "boundary-five-to-six", "5,6", "BB", 30, expectedPips: "6,6"); // 12 x 2 x 1.25
            yield return Case("C13", "hit", "3", "B", 13);                             // (3 + 8) x 1.25 = 13.75
            yield return Case("C13", "miss-even", "4", "B", 5);                        // 4 x 1.25
            yield return Case("C13", "boundary-red-group-uses-original-one", "1,1", "RR", 54); // 18 x 2 x 1.5, red group of 2 is x1
            yield return Case("C14", "hit", "3", "B", 15);                             // +4: 3 x 5.25 = 15.75
            yield return Case("C14", "miss-mixed-parity", "3,2", "BB", 6);             // 5 x 1.25 = 6.25
            yield return Case("C14", "boundary-single-one", "1", "R", 5);              // 1 x (1 + 0.5 + 4) = 5.5
            yield return Case("C17", "hit", "3", "B", 15);                             // (3 + 9) x 1.25
            yield return Case("C17", "miss-four", "4", "B", 5);                        // 4 x 1.25
            yield return Case("C17", "boundary-all-three-small-values", "1,2,3", "RBB", 33); // 10 + 11 + 12
            yield return Case("C18", "hit", "2,2", "RB", 48);                          // +5: 4 x 2 x 6
            yield return Case("C18", "miss-unequal", "2,2,2", "RRB", 24);              // 6 x 4, red group of 2 is x1
            yield return Case("C18", "boundary-two-different-pips", "3,6", "RB", 54);  // 9 x 6
            // C19 is a new card in p1.1.1: +2.5 when the smallest submitted pip is 1, +1.5 when it is 2; the six
            // permanent faces no longer matter. The three old fixtures are kept and renamed after what they show now.
            yield return Case("C19", "hit-min-one", "1", "R", 4, permanentSum: 13);    // 1 x (1 + 0.5 + 2.5)
            yield return Case("C19", "permanent-sum-no-longer-matters", "1", "R", 4, permanentSum: 14);
            yield return Case("C19", "miss-min-six", "6", "B", 7, permanentSum: 6);    // 6 x 1.25 = 7.5
            yield return Case("C20", "hit", "1,2,3,4,6", "RBBRB", 40);                 // 1 + 2 + 3 + 4 + (6 + 24)
            yield return Case("C20", "miss-last-five", "1,2,3,4,5", "RBBRB", 150);
            yield return Case("C20", "boundary-last-occupied-not-capacity-slot", "6", "B", 7); // 6 x 1.25 = 7.5
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
            // p1.1.1 rewrite (docs/specs/v111/TEST_CHANGES.md): all blue adds 0.25, all red 0.5, to the additive zone.
            // Multipliers multiply per copy: 5 x 1.25 (all blue) x 3 x 3 = 56.25.
            yield return Copies("C02", 2, "multiplier-per-copy", "2,3", "BB", 56);
            // Per-die adds add per copy: (1 + 15 + 15) x 1.5 (all red) = 46.5.
            yield return Copies("C10", 2, "die-add-per-copy", "1", "R", 46);
            // Whole-hand base adds add per copy: (2 + 2 + 20 + 20) x 2 (pair) x 1.25 (all blue).
            yield return Copies("C27", 2, "base-add-per-copy", "2,2", "BB", 110);
            // Retriggers add per copy: the first die counts 1 + 1 + 1 times: 6 x 3 x 1.25 (all blue) = 22.5.
            yield return Copies("C64", 2, "retrigger-per-copy", "6", "B", 22);
            // The middle die is counted 1 + 2 + 2 times: (1 + 2 x 5 + 3) x 1.25 (all blue) = 17.5.
            yield return Copies("C109", 2, "middle-retrigger-per-copy", "1,2,3", "BBB", 17);
            // Hand multiplier adds add per copy: 6 x (2 + 2 + 2) x 1.25 (all blue).
            yield return Copies("C84", 2, "hand-add-per-copy", "3,3", "BB", 45);
            // The last-slot add applies per copy: 1 + 2 + 3 + 4 + (6 + 24 + 24), scatter, mixed colours, no red group.
            yield return Copies("C20", 2, "slot-add-per-copy", "1,2,3,4,6", "RBBRB", 64);
            // Four copies of C47, each adding 4 cards x 0.6 = 2.4: 2 x (1 + 0.25 + 9.6) = 21.7.
            yield return Copies("C47", 4, "count-multiplier-per-copy", "2", "B", 21);
            // Switches apply once: odd -> even (+1 only once): pair of 2s, 4 x 2 (pair), mixed colours, red group of 1.
            yield return Copies("C12", 2, "switch-applies-once", "1,2", "RB", 8, expectedPips: "2,2");
            // The first die turns red once: pair of 4s, 8 x 2 (pair), mixed colours, red group of 1.
            yield return Copies("C03", 2, "switch-applies-once", "4,4", "BB", 16, expectedColors: "RB");
            // The red group counts one extra die once: 8 x 2 (pair) x 2 (red group 2 + 1 = 3) x 1.5 (all red).
            yield return Copies("C146", 2, "red-bonus-applies-once", "4,4", "RR", 48);
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
            // p1.1.1 rewrite (docs/specs/v111/TEST_CHANGES.md). Growth is per instance: C54 starts at x1.25 and one copy has
            // grown 0.1: x1.35 = 27/20 and x1.25 = 5/4 on a blue 2: 2 x 1.25 x 1.35 x 1.25 = 4.21875.
            var grown = one(2, "blue"); var a = card("C54", 1); a.state.growth = 10; grown.equippedControllers = new[] { a, card("C54", 2) };
            var g = ScoreCalculator.Evaluate(grown, config);
            Assert.That(g.score, Is.EqualTo("4")); Assert.That(g.multiplierFactors.Select(f => f.numerator + "/" + f.denominator), Is.EqualTo(new[] { "27/20", "5/4" }));
            // Each C45 multiplies the dice of its own colour: red 4 x 3 and blue 4 x 3, then a pair (x2) in mixed colours: 24 x 2 = 48.
            var colours = new SubmissionSnapshot { configVersion = config.configVersion, capacity = 5,
                dice = new[] { new SubmissionDie { dieId = "D001", faceId = "F01", currentPip = 4, currentColor = "red", permanentSixFaceSum = 21 }, new SubmissionDie { dieId = "D002", faceId = "F01", currentPip = 4, currentColor = "blue", permanentSixFaceSum = 21 } } };
            var red = card("C45", 1); red.state.activeColor = "red"; var blue = card("C45", 2); blue.state.activeColor = "blue";
            colours.equippedControllers = new[] { red, blue }; Assert.That(ScoreCalculator.Evaluate(colours, config).score, Is.EqualTo("48"));
            // Both red: the red die is multiplied twice (4 x 9 = 36), the blue one stays 4: (36 + 4) x 2 = 80.
            blue.state.activeColor = "red"; colours.equippedControllers = new[] { red, blue }; Assert.That(ScoreCalculator.Evaluate(colours, config).score, Is.EqualTo("80"));
            // Each real C53 has its own frozen outcome: x2 and x3 on a blue 2: 2 x 1.25 x 2 x 3 = 15.
            var random = one(2, "blue"); random.equippedControllers = new[] { card("C53", 1), card("C53", 2) };
            random.resolvedRandomChoices = new[] { new RandomMultiplierChoice { ownerInstanceId = "C53-1", units2 = 4 }, new RandomMultiplierChoice { ownerInstanceId = "C53-2", units2 = 6 } };
            Assert.That(ScoreCalculator.Evaluate(random, config).score, Is.EqualTo("15"));
            // C138 copies share the frozen wallet left to right. It costs 2 gold and gives x2.5 now, so the wallets move
            // from 5 and 6 to 3 and 4: 3 gold pays for one (2 x 1.25 x 2.5 = 6.25, 2 spent), 4 gold for both
            // (2 x 1.25 x 6.25 = 15.625, 4 spent).
            var spend = one(2, "blue"); spend.equippedControllers = new[] { card("C138", 1), card("C138", 2) };
            spend.wallet = 3; var five = ScoreCalculator.Evaluate(spend, config); Assert.That(five.score, Is.EqualTo("6")); Assert.That(five.afterScorePlan.goldSpent, Is.EqualTo(2));
            spend.wallet = 4; var six = ScoreCalculator.Evaluate(spend, config); Assert.That(six.score, Is.EqualTo("15")); Assert.That(six.afterScorePlan.goldSpent, Is.EqualTo(4));
            // After-score growth is planned for each instance: C55 grows by 3 points per submission.
            var plan = one(2, "blue"); plan.equippedControllers = new[] { card("C55", 1), card("C55", 2) };
            Assert.That(ScoreCalculator.Evaluate(plan, config).afterScorePlan.growthChanges.Select(c => c.ownerInstanceId + ":" + c.delta), Is.EqualTo(new[] { "C55-1:3", "C55-2:3" }));
        }
    }
}
