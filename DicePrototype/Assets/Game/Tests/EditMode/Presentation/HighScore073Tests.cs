using DiceCraft.Core;
using DiceCraft.Presentation;
using NUnit.Framework;

namespace DiceCraft.Tests
{
    // V73-D: the celebration tier rule of docs/specs/v073/VERSION_PLAN.md section 6.
    public sealed class HighScore073Tests
    {
        [TestCase("0", "180", 0, 0)]
        [TestCase("89", "180", 0, 0)]
        [TestCase("90", "180", 0, 1)]
        [TestCase("179", "180", 0, 1)]
        [TestCase("180", "180", 0, 2)]
        [TestCase("359", "180", 0, 2)]
        [TestCase("360", "180", 0, 3)]
        [TestCase("1", "1000000", 2, 0)]
        [TestCase("1", "1000000", 3, 1)]
        [TestCase("1", "1000000", 4, 2)]
        [TestCase("1", "1000000", 5, 3)]
        [TestCase("967680", "1800000", 5, 3)]
        [TestCase("967680", "1800000", 2, 1)]
        [TestCase("0", "1", 5, 0)]
        public void HighScore_TierFollowsScoreShareOfTargetOrTotalMultiplier(string score, string target, int combo, int expected)
        {
            Assert.That(HighScore073.Tier(score, target, combo), Is.EqualTo(expected));
        }

        [Test]
        public void HighScore_TierIsExactForScoresBeyondSixtyFourBits()
        {
            string target = "100000000000000000000000000000000000000";
            Assert.That(HighScore073.Tier("49999999999999999999999999999999999999", target, 0), Is.EqualTo(HighScore073.None));
            Assert.That(HighScore073.Tier("50000000000000000000000000000000000000", target, 0), Is.EqualTo(HighScore073.High));
            Assert.That(HighScore073.Tier("200000000000000000000000000000000000000", target, 0), Is.EqualTo(HighScore073.Burst));
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
            var plain = new ScoreBreakdown { baseTotal = "23", totalHandMult100 = 100, colorMult100 = 100, redMult100 = 100,
                controllerMultiplierNumerator = "1", controllerMultiplierDenominator = "1", enchantMultiplierCount = 0 };
            Assert.That(HighScore073.Formula(plain, n => (n / 100m).ToString("0.##"), (n, d) => n + "/" + d, n => n), Is.EqualTo("23"));
            var full = new ScoreBreakdown { baseTotal = "36", totalHandMult100 = 2800, colorMult100 = 500, redMult100 = 100,
                controllerMultiplierNumerator = "192", controllerMultiplierDenominator = "1", enchantMultiplierCount = 2 };
            Assert.That(HighScore073.Formula(full, n => (n / 100m).ToString("0.##"), (n, d) => d == "1" ? n : n + "/" + d, n => n),
                Is.EqualTo("36  ×  28  ×  5  ×  192  ×  2^2"));
            Assert.That(HighScore073.Formula(null, n => "", (n, d) => "", n => n), Is.Empty);
        }
    }
}
