using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using DiceCraft.Core;

namespace DiceCraft.Presentation
{
    /// <summary>
    /// v1.1.1 (task W7b, RULES_DELTA 12.1 and 13 row 4): how a run's own records read on the settlement and result
    /// pages. Display only; the values are the session's (RunState.highestHandScore, highestClearRatio*, the stage's
    /// handRecordBroken and the "ClearRatioRecord" effect of the settlement receipt).
    /// </summary>
    public static class RunRecords111
    {
        /// <summary>
        /// The highest clear ratio as "×3.2": one decimal, always written, rounded down (never up to a ratio that was
        /// not reached). Null when no stage has been cleared yet (0/1) or the fraction cannot be read.
        /// </summary>
        public static string ClearRatio(string numerator, string denominator)
        {
            if (!BigInteger.TryParse(numerator ?? "", NumberStyles.None, CultureInfo.InvariantCulture, out var n)) return null;
            if (!BigInteger.TryParse(denominator ?? "", NumberStyles.None, CultureInfo.InvariantCulture, out var d)) return null;
            if (d.Sign <= 0 || n.Sign <= 0) return null;
            var tenths = BigInteger.Divide(n * 10, d);
            var whole = BigInteger.DivRem(tenths, 10, out var digit);
            return "×" + whole.ToString("N0", CultureInfo.InvariantCulture) + "." + ((int)digit).ToString(CultureInfo.InvariantCulture);
        }
        public static string ClearRatio(RunState state) => state == null ? null : ClearRatio(state.highestClearRatioNumerator, state.highestClearRatioDenominator);

        /// <summary>True when the stage shown by this state raised the run's highest hand (kept until the stage changes).</summary>
        public static bool HandRecordBroken(RunState state) => state != null && state.stage != null && state.stage.handRecordBroken;

        /// <summary>
        /// True when the settlement the state shows raised the run's highest clear ratio: the receipt that carries this
        /// settlement recorded "ClearRatioRecord". An equal ratio keeps the old record, so only the effect tells.
        /// </summary>
        public static bool ClearRatioBroken(RunState state)
        {
            var settlement = state == null ? null : state.lastStageSettlement;
            if (settlement == null || state.processedReceipts == null) return false;
            var receipt = state.processedReceipts.LastOrDefault(r => r != null && r.settlement != null && r.settlement.settlementId == settlement.settlementId);
            return receipt != null && receipt.effects != null && receipt.effects.Any(e => e.kind == "ClearRatioRecord");
        }
    }
}
