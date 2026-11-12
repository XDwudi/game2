using System;
using System.Collections.Generic;
using System.Numerics;
using DiceCraft.Core;

namespace DiceCraft.Presentation
{
    /// <summary>
    /// v0.73 high-score celebration rule (docs/specs/v073/VERSION_PLAN.md section 6). Presentation only:
    /// it reads a committed score and never feeds back into rules, rewards or random numbers.
    /// </summary>
    public static class HighScore073
    {
        public const int None = 0, High = 1, Super = 2, Burst = 3;
        public const float PopSeconds = .12f, MaxCountSeconds = .42f, MinVisibleSeconds = .45f, FadeSeconds = .16f, ReducedFadeSeconds = .12f;

        /// <summary>
        /// 1: hand score reaches half of the stage target, or total multiplier tier 3 (x128).
        /// 2: hand score reaches the target, or tier 4 (x1024). 3: twice the target, or tier 5 (x8192).
        /// </summary>
        public static int Tier(string handScore, string stageTarget, int comboTier)
        {
            if (comboTier < 0 || comboTier > 5) throw new ArgumentOutOfRangeException(nameof(comboTier));
            BigInteger score = ExactScoreMath.Parse(handScore), target = ExactScoreMath.Parse(stageTarget);
            if (score.Sign <= 0) return None;
            int tier = None;
            if (target.Sign > 0)
            {
                if (score * 2 >= target) tier = High;
                if (score >= target) tier = Super;
                if (score >= target * 2) tier = Burst;
            }
            if (comboTier >= 3) tier = Math.Max(tier, High);
            if (comboTier >= 4) tier = Math.Max(tier, Super);
            if (comboTier >= 5) tier = Burst;
            return tier;
        }

        /// <summary>The number shown while counting up: floor(score * progress), exact for any size.</summary>
        public static string CountValue(string handScore, float progress)
        {
            BigInteger score = ExactScoreMath.Parse(handScore);
            if (float.IsNaN(progress) || progress >= 1f) return ExactScoreMath.SaveDecimal(score);
            if (progress <= 0f) return "0";
            int permille = Math.Min(999, Math.Max(0, (int)(progress * 1000f)));
            return ExactScoreMath.SaveDecimal(score * permille / 1000);
        }

        /// <summary>Factors that actually changed the score, in the order the score rail shows them.</summary>
        public static string Formula(ScoreBreakdown b, Func<int, string> factor, Func<string, string, string> ratio, Func<string, string> number)
        {
            if (b == null) return "";
            var parts = new List<string> { number(b.baseTotal) };
            if (b.totalHandMult100 != 100) parts.Add(factor(b.totalHandMult100));
            if (b.colorMult100 != 100) parts.Add(factor(b.colorMult100));
            if (b.redMult100 != 100) parts.Add(factor(b.redMult100));
            if (b.controllerMultiplierNumerator != b.controllerMultiplierDenominator) parts.Add(ratio(b.controllerMultiplierNumerator, b.controllerMultiplierDenominator));
            if (b.enchantMultiplierCount > 0) parts.Add("2^" + b.enchantMultiplierCount);
            return string.Join("  ×  ", parts);
        }
    }
}
