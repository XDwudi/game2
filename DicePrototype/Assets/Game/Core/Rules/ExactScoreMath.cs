using System;
using System.Globalization;
using System.Numerics;

namespace DiceCraft.Core
{
    public static class ExactScoreMath
    {
        public static BigInteger Parse(string value)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("Missing decimal score.");
            foreach (char c in value) if (c < '0' || c > '9') throw new ArgumentException("Score requires nonnegative decimal digits.");
            return BigInteger.Parse(value, NumberStyles.None, CultureInfo.InvariantCulture);
        }
        public static string SaveDecimal(BigInteger value) { if (value < 0) throw new ArgumentOutOfRangeException(nameof(value)); return value.ToString(CultureInfo.InvariantCulture); }
        public static string Add(string a, string b) => SaveDecimal(Parse(a) + Parse(b));
        public static int Compare(string a, string b) => Parse(a).CompareTo(Parse(b));
        public static BigInteger FinalScore(BigInteger baseTotal, int handMult100, int colorMult100, int redMult100, BigInteger controllerNumerator, BigInteger controllerDenominator, int multiplierEnchantCount)
        {
            if (baseTotal < 0 || handMult100 < 100 || colorMult100 < 100 || redMult100 < 100 || controllerNumerator < 0 || controllerDenominator <= 0 || multiplierEnchantCount < 0) throw new ArgumentOutOfRangeException();
            return baseTotal * handMult100 * colorMult100 * redMult100 * controllerNumerator * BigInteger.Pow(2, multiplierEnchantCount) / (1000000 * controllerDenominator);
        }
    }
}
