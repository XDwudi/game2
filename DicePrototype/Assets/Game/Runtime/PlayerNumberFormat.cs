using System;
using System.Globalization;
using System.Numerics;

namespace DiceCraft
{
    /// <summary>
    /// v1.1 (task W09, text proposal section 3): the single place that turns exact rule numbers into player text.
    /// Ratios show at most two decimals with trailing zeros removed, and "≈" when the decimal is rounded.
    /// Integer parts use thousands separators from four digits. An integer part longer than fullDigits is shown as
    /// "约 1.23×10^19", the exponent as a TextMeshPro superscript (the bundled font has no superscript digits 5-9 and 0).
    /// Display only: rule values and saves keep their exact reduced fractions.
    /// </summary>
    public static class PlayerNumberFormat
    {
        /// <summary>Up to this many integer digits are shown in full (text proposal section 3.3).</summary>
        public const int FullDigits = 15;
        /// <summary>Never abbreviate: exact breakdown views, where every digit stays readable and no markup is allowed.</summary>
        public const int Unlimited = int.MaxValue;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public static string Integer(BigInteger value, int fullDigits = FullDigits) => Ratio(value, BigInteger.One, fullDigits);
        public static string Gold(long value) => Integer(value);

        /// <summary>numerator/denominator as at most two decimals, "≈" when rounded, "约 m×10^e" beyond fullDigits integer digits.</summary>
        public static string Ratio(BigInteger numerator, BigInteger denominator, int fullDigits = FullDigits)
        {
            if (denominator.IsZero) return "—";
            if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
            bool negative = numerator.Sign < 0;
            var magnitude = BigInteger.Abs(numerator);
            var hundredths = magnitude * 100;
            var scaled = BigInteger.DivRem(hundredths, denominator, out var remainder);
            bool exact = remainder.IsZero;
            if (remainder * 2 >= denominator) scaled += 1;
            var whole = BigInteger.DivRem(scaled, 100, out var cents);
            if (Digits(whole) > fullDigits) return Scientific(magnitude, denominator, negative);
            if (scaled.IsZero) negative = false;
            string text = whole.ToString("N0", Invariant);
            if (!cents.IsZero) text += "." + ((int)cents).ToString("00", Invariant).TrimEnd('0');
            return (exact ? "" : "≈") + (negative ? "−" : "") + text;
        }

        /// <summary>An exact rule string ("n" or "n/d") as player text; any other value (colour, list) is returned unchanged.</summary>
        public static string Exact(string value, int fullDigits = FullDigits)
            => TryParseExact(value, out var numerator, out var denominator) ? Ratio(numerator, denominator, fullDigits) : value;

        /// <summary>A multiplier with its sign: "×3.9", "≈×0.33", "约 ×4.29×10^9". The approximation mark always comes first.</summary>
        public static string Times(BigInteger numerator, BigInteger denominator, int fullDigits = FullDigits)
        {
            string value = Ratio(numerator, denominator, fullDigits);
            if (value.StartsWith("≈", StringComparison.Ordinal)) return "≈×" + value.Substring(1);
            if (value.StartsWith("约 ", StringComparison.Ordinal)) return "约 ×" + value.Substring(2);
            return "×" + value;
        }
        public static string Times(string value, int fullDigits = FullDigits)
            => TryParseExact(value, out var numerator, out var denominator) ? Times(numerator, denominator, fullDigits) : "×" + value;

        /// <summary>A probability: numerator/denominator percent with one decimal and no trailing zero ("16.7%", "0%").</summary>
        public static string Percent(BigInteger numerator, BigInteger denominator)
        {
            if (denominator.IsZero) return "—";
            if (denominator.Sign < 0) { numerator = -numerator; denominator = -denominator; }
            bool negative = numerator.Sign < 0;
            var tenths = BigInteger.DivRem(BigInteger.Abs(numerator) * 10, denominator, out var remainder);
            if (remainder * 2 >= denominator) tenths += 1;
            var whole = BigInteger.DivRem(tenths, 10, out var digit);
            if (tenths.IsZero) negative = false;
            return (negative ? "−" : "") + whole.ToString("N0", Invariant) + (digit.IsZero ? "" : "." + digit.ToString(Invariant)) + "%";
        }

        public static bool TryParseExact(string value, out BigInteger numerator, out BigInteger denominator)
        {
            numerator = BigInteger.Zero; denominator = BigInteger.One;
            if (string.IsNullOrEmpty(value)) return false;
            var parts = value.Split('/');
            if (parts.Length > 2) return false;
            if (!BigInteger.TryParse(parts[0], NumberStyles.AllowLeadingSign, Invariant, out numerator)) return false;
            if (parts.Length == 2 && (!BigInteger.TryParse(parts[1], NumberStyles.None, Invariant, out denominator) || denominator.IsZero)) return false;
            return true;
        }

        private static int Digits(BigInteger value) => value.IsZero ? 1 : BigInteger.Abs(value).ToString(Invariant).Length;

        // Three significant digits, rounded half up; the decimal point of the mantissa follows the same trimming rule.
        private static string Scientific(BigInteger magnitude, BigInteger denominator, bool negative)
        {
            int exponent = Digits(magnitude / denominator) - 1;
            if (exponent < 2) exponent = 2;
            var unit = denominator * BigInteger.Pow(10, exponent - 2);
            var hundredths = (magnitude * 2 + unit) / (unit * 2);
            if (hundredths >= 1000) { hundredths /= 10; exponent++; }
            var whole = BigInteger.DivRem(hundredths, 100, out var cents);
            string mantissa = whole.ToString(Invariant) + (cents.IsZero ? "" : "." + ((int)cents).ToString("00", Invariant).TrimEnd('0'));
            return "约 " + (negative ? "−" : "") + mantissa + "×10<sup>" + exponent.ToString(Invariant) + "</sup>";
        }
    }
}
