using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Tests
{
    // V73-D: the celebration tier rule of docs/specs/v073/VERSION_PLAN.md section 6.
    public sealed class HighScore073Tests
    {
        // p1.1.1 (RULES_DELTA 13 row 5, task W7b): the tiers start at 1, 3 and 10 times the hand's frozen target; a hand
        // below the target has none of them. p0.9.0 to p1.1.0 started them at half the target, the target and twice
        // the target, so the same rows used to expect 0, 0, 1, 1, 2, 2, 3 and (the two 967680 rows) 1.
        [TestCase("0", "180", 0, 0)]
        [TestCase("89", "180", 0, 0)]
        [TestCase("90", "180", 0, 0)]
        [TestCase("179", "180", 0, 0)]
        [TestCase("180", "180", 0, 1)]
        [TestCase("359", "180", 0, 1)]
        [TestCase("360", "180", 0, 1)]
        [TestCase("539", "180", 0, 1)]
        [TestCase("540", "180", 0, 2)]
        [TestCase("1799", "180", 0, 2)]
        [TestCase("1800", "180", 0, 3)]
        [TestCase("1000000", "180", 0, 3)]
        [TestCase("1", "1000000", 2, 0)]
        // p0.9.0 (docs/specs/v09/RULES_SPEC.md section 0): only the share of the stage target raises the tier.
        // The four rows below used to expect 1, 2, 3 and 3 from the total-multiplier tier alone.
        [TestCase("1", "1000000", 3, 0)]
        [TestCase("1", "1000000", 4, 0)]
        [TestCase("1", "1000000", 5, 0)]
        [TestCase("967680", "1800000", 5, 0)]
        [TestCase("967680", "1800000", 2, 0)]
        [TestCase("0", "1", 5, 0)]
        // The example of RULES_DELTA 13 row 5: target 500; 499 none, 500, 1500 and 5000 reach tiers one, two, three.
        [TestCase("499", "500", 0, 0)]
        [TestCase("500", "500", 0, 1)]
        [TestCase("1499", "500", 0, 1)]
        [TestCase("1500", "500", 0, 2)]
        [TestCase("4999", "500", 0, 2)]
        [TestCase("5000", "500", 0, 3)]
        // A target of 0 (no stage) never celebrates.
        [TestCase("100", "0", 0, 0)]
        public void HighScore_TierFollowsScoreShareOfTargetOnly(string score, string target, int combo, int expected)
        {
            Assert.That(HighScore073.Tier(score, target, combo), Is.EqualTo(expected));
        }

        [Test]
        public void HighScore_TierIsExactForScoresBeyondSixtyFourBits()
        {
            // p1.1.1: the boundaries are the target, three targets and ten targets (were half, one and two targets).
            string target = "100000000000000000000000000000000000000";
            Assert.That(HighScore073.Tier("99999999999999999999999999999999999999", target, 0), Is.EqualTo(HighScore073.None));
            Assert.That(HighScore073.Tier("100000000000000000000000000000000000000", target, 0), Is.EqualTo(HighScore073.High));
            Assert.That(HighScore073.Tier("299999999999999999999999999999999999999", target, 0), Is.EqualTo(HighScore073.High));
            Assert.That(HighScore073.Tier("300000000000000000000000000000000000000", target, 0), Is.EqualTo(HighScore073.Super));
            Assert.That(HighScore073.Tier("999999999999999999999999999999999999999", target, 0), Is.EqualTo(HighScore073.Super));
            Assert.That(HighScore073.Tier("1000000000000000000000000000000000000000", target, 0), Is.EqualTo(HighScore073.Burst));
        }

        [Test]
        public void HighScore_TierReadsTheTargetFrozenWithTheHand()
        {
            // RULES_DELTA 13 row 5: T is the effective target of the submission (the snapshot's stageTarget), not the
            // stage target after the command. The receipt carries it; without one it falls back to the stage target.
            var cfg = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText(Path.Combine(Application.dataPath, "Game/Data/P0Config.json")))); cfg.Validate();
            PresentationReceipt Receipt(string frozenTarget, string stageTargetAfter)
            {
                // Five sixes, no cards: the score does not matter here, only which target the tier is read against.
                var snapshot = new SubmissionSnapshot { runId = "run", handId = "hand", configVersion = cfg.configVersion, capacity = 5, equippedControllers = new ControllerInstance[0],
                    stageScoreBefore = frozenTarget == null ? null : "0", stageTarget = frozenTarget,
                    dice = Enumerable.Range(0, 5).Select(i => new SubmissionDie { dieId = "d" + i, faceId = "F01", currentPip = 6, currentColor = "blue", permanentSixFaceSum = 21 }).ToArray() };
                var b = ScoreCalculator.Evaluate(snapshot, cfg);
                return PresentationReceiptAdapter.Build(new CommandReceipt { commandId = "receipt", score = b.score, submission = new SubmissionRecord { snapshot = snapshot, breakdown = b } },
                    new RunState { runId = "run", stage = new StageState { score = "0", target = stageTargetAfter } },
                    new RunState { runId = "run", phase = "HandReady", stage = new StageState { score = b.score, target = stageTargetAfter } }, cfg);
            }
            var plain = Receipt(null, "1800000");
            System.Numerics.BigInteger score = System.Numerics.BigInteger.Parse(plain.ScoreDecimal);
            Assert.That(score > 10, Is.True, "The sample hand must score enough to place the three targets below.");
            Assert.That(plain.HandTarget, Is.EqualTo("1800000"), "Without a frozen target the receipt falls back to the stage target.");
            // The same hand and the same stage target afterwards: only the frozen target moves the tier.
            string reached = score.ToString(), third = (score / 3).ToString(), tenth = (score / 10).ToString(), missed = (score + 1).ToString();
            var cases = new[] { (missed, HighScore073.None), (reached, HighScore073.High), (third, HighScore073.Super), (tenth, HighScore073.Burst) };
            foreach (var (frozen, expected) in cases)
            {
                var receipt = Receipt(frozen, "1");
                Assert.That(receipt.HandTarget, Is.EqualTo(frozen)); Assert.That(receipt.StageTarget, Is.EqualTo("1"));
                Assert.That(HighScore073.Tier(receipt.ScoreDecimal, receipt.HandTarget, receipt.ComboTier), Is.EqualTo(expected), "frozen target " + frozen);
            }
        }

        [Test]
        public void HighScore_CountValueIsMonotonicAndEndsOnTheCommittedScore()
        {
            Assert.That(HighScore073.CountValue("967680", 0f), Is.EqualTo("0"));
            Assert.That(HighScore073.CountValue("967680", 1f), Is.EqualTo("967680"));
            System.Numerics.BigInteger last = -1, final = System.Numerics.BigInteger.Parse("967680");
            for (int i = 0; i <= 200; i++)
            {
                var shown = System.Numerics.BigInteger.Parse(HighScore073.CountValue("967680", i / 200f));
                Assert.That(shown >= last && shown <= final, Is.True, "step " + i);
                last = shown;
            }
            Assert.That(last, Is.EqualTo(final));
        }

        [Test]
        public void HighScore_FormulaListsOnlyFactorsThatChangedTheScore()
        {
            var plain = new ScoreBreakdown { baseTotal = "23", totalHandMult100 = 100, colorAdd100 = 0, redMult100 = 100,
                controllerMultiplierNumerator = "1", controllerMultiplierDenominator = "1", enchantAdditiveCount = 0 };
            Assert.That(HighScore073.Formula(plain, n => (n / 100m).ToString("0.##"), (n, d) => n + "/" + d, n => n), Is.EqualTo("23"));
            // p1.1.1 (W7a, RULES_DELTA 2): the colour (+4) and two score enchantments (+1.5 each) are one bonus factor
            // 1 + 7 = 8, written between the hand and the cards; there is no colour factor and no "2^n" any more.
            // p1.1.0 wrote "36  ×  28  ×  5  ×  192  ×  2^2" for the same hand.
            var full = new ScoreBreakdown { baseTotal = "36", totalHandMult100 = 2800, colorAdd100 = 400, redMult100 = 100,
                controllerMultiplierNumerator = "192", controllerMultiplierDenominator = "1", enchantAdditiveCount = 2, additiveTotal100 = 700 };
            Assert.That(HighScore073.Formula(full, n => (n / 100m).ToString("0.##"), (n, d) => d == "1" ? n : n + "/" + d, n => n),
                Is.EqualTo("36  ×  28  ×  8  ×  192"));
            // A bonus that is not a whole number stays exact: +0.75 is the factor 7/4.
            full.additiveTotal100 = 75; full.redMult100 = 300;
            Assert.That(HighScore073.Formula(full, n => (n / 100m).ToString("0.##"), (n, d) => d == "1" ? n : n + "/" + d, n => n, (key, value) => key + "=" + value, null),
                Is.EqualTo("base=36  ×  hand=28  ×  red=3  ×  additive=7/4  ×  controller=192"));
            Assert.That(HighScore073.Formula(null, n => "", (n, d) => "", n => n), Is.Empty);
        }
    }
}
