using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DiceCraft.Core;

namespace DiceCraft.Presentation
{
    /// <summary>One displayed factor of a p1.1.1 score: base x hand x red group x (1 + bonus) x cards x item x boss.</summary>
    public sealed class ScoreFactor111
    {
        /// <summary>"base", "hand", "red", "additive", "controller", "item" or "boss".</summary>
        public string Key;
        /// <summary>The item or boss modifier of an "item" / "boss" factor; null otherwise.</summary>
        public string SourceId;
        /// <summary>The reduced value. The base is its integer over 1.</summary>
        public BigInteger Numerator, Denominator;
        /// <summary>Only on "additive": the bonuses that add up to A, in score order (colour, gold faces, cards, enchantments).</summary>
        public AdditiveEntry[] Entries = Array.Empty<AdditiveEntry>();
        public bool IsIdentity => Key != "base" && Numerator == Denominator;
    }

    /// <summary>
    /// v1.1.1 (RULES_DELTA sections 2 and 13): the one list of factors every score readout is built from - the chips
    /// under the dice tray, the formula lines of the breakdown, of the high-score celebration and of the debug panel,
    /// and the run statistics. Presentation only: it reads a frozen breakdown and never feeds back into the rules.
    /// The product of the list is the score before the final floor (scoreNumerator / scoreDenominator).
    /// </summary>
    public static class ScoreFactors111
    {
        /// <summary>The factors in the order the score applies them. The colour bonus, gold faces, card bonuses and the
        /// score enchantment are one "additive" factor (100 + additiveTotal100) / 100 with its entries; the cards'
        /// multipliers are one "controller" factor. With omitIdentity, factors of exactly x1 are left out (never the base).</summary>
        public static List<ScoreFactor111> List(ScoreBreakdown b, bool omitIdentity = false)
        {
            var list = new List<ScoreFactor111>(); if (b == null) return list;
            list.Add(Make("base", ExactScoreMath.Parse(b.baseTotal), 1));
            list.Add(Make("hand", b.totalHandMult100, 100));
            list.Add(Make("red", b.redMult100, 100));
            var additive = Make("additive", 100 + (BigInteger)b.additiveTotal100, 100);
            additive.Entries = (b.additiveEntries ?? Array.Empty<AdditiveEntry>()).Select(e => e.Clone()).ToArray();
            list.Add(additive);
            list.Add(Make("controller", ExactScoreMath.Parse(b.controllerMultiplierNumerator), ExactScoreMath.Parse(b.controllerMultiplierDenominator)));
            foreach (var f in b.globalFactors ?? Array.Empty<MultiplierFactor>())
            {
                var global = Make(f.emitterId == "ITEM" ? "item" : "boss", ExactScoreMath.Parse(f.numerator), ExactScoreMath.Parse(f.denominator));
                global.SourceId = f.abilityId; list.Add(global);
            }
            if (omitIdentity) list.RemoveAll(f => f.IsIdentity);
            return list;
        }
        /// <summary>The exact product of displayed factors.</summary>
        public static ExactFraction Product(IEnumerable<ScoreFactor111> factors)
        {
            BigInteger n = 1, d = 1;
            foreach (var f in factors) { n *= f.Numerator; d *= f.Denominator; }
            return new ExactFraction(n, d);
        }
        /// <summary>Everything the base is multiplied by: hand x red group x (1 + bonus) x cards, and with
        /// includeGlobal also the item and boss factors (then base x this = the score before the floor).</summary>
        public static ExactFraction Multiplier(ScoreBreakdown b, bool includeGlobal)
            => Product(List(b).Where(f => f.Key != "base" && (includeGlobal || f.Key != "item" && f.Key != "boss")));
        /// <summary>The bonus total A as an exact fraction (additiveTotal100 / 100).</summary>
        public static ExactFraction Bonus(long amount100) => new ExactFraction(amount100, 100);

        private static ScoreFactor111 Make(string key, BigInteger numerator, BigInteger denominator)
        {
            var reduced = new ExactFraction(numerator, denominator);
            return new ScoreFactor111 { Key = key, Numerator = reduced.Numerator, Denominator = reduced.Denominator };
        }
    }
}
