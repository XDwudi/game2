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
        /// 1: hand score reaches half of the stage target. 2: reaches the target. 3: twice the target.
        /// p0.9.0 (docs/specs/v09/RULES_SPEC.md section 0): the total-multiplier tier no longer raises the celebration,
        /// so growing multipliers cannot make every hand a top-tier one. comboTier is still validated for callers.
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

        /// <summary>Factors that actually changed the score, in the order the score applies them.</summary>
        public static string Formula(ScoreBreakdown b, Func<int, string> factor, Func<string, string, string> ratio, Func<string, string> number)
            => Formula(b, factor, ratio, number, null, null);

        /// <summary>
        /// v1.1 (task W09): label puts a name before each value. v1.1.1 (W7a): the factors are ScoreFactors111's -
        /// keys "base", "hand", "red", "additive" (one factor 1 + A for the colour, gold faces, card bonuses and the
        /// score enchantment), "controller"; global, when given, appends the item and boss factors. A factor of
        /// exactly x1 is left out, so the product of what is written is always the score before the final floor.
        /// </summary>
        public static string Formula(ScoreBreakdown b, Func<int, string> factor, Func<string, string, string> ratio, Func<string, string> number,
            Func<string, string, string> label, Func<MultiplierFactor, string> global)
        {
            if (b == null) return "";
            Func<string, string, string> term = label ?? ((key, value) => value);
            var parts = new List<string>();
            foreach (var f in ScoreFactors111.List(b, true))
            {
                string n = ExactScoreMath.SaveDecimal(f.Numerator), d = ExactScoreMath.SaveDecimal(f.Denominator);
                switch (f.Key)
                {
                    case "base": parts.Add(term("base", number(b.baseTotal))); break;
                    case "hand": parts.Add(term("hand", factor(b.totalHandMult100))); break;
                    case "red": parts.Add(term("red", factor(b.redMult100))); break;
                    case "item": case "boss": if (global != null) parts.Add(global(new MultiplierFactor { emitterId = f.Key == "item" ? "ITEM" : "BOSS", abilityId = f.SourceId, numerator = n, denominator = d })); break;
                    default: parts.Add(term(f.Key, ratio(n, d))); break;
                }
            }
            return string.Join("  ×  ", parts);
        }
    }
}
