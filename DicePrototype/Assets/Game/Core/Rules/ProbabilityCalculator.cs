using System;
using System.Linq;
using System.Numerics;

namespace DiceCraft.Core
{
    public static class ProbabilityCalculator
    {
        /// <summary>Returns units in the supplied faces' order; CDF sampling uses face-ID order.</summary>
        public static int[] BuildUnits(FaceState[] faces, bool largeBuff, bool smallBuff, GameConfig config, bool highController = false, bool lowController = false)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));
            config.ValidateFaces(faces);
            if (config.probabilityUnits <= 0) throw new InvalidOperationException("Probability precision missing.");
            var weights = faces.Select(f => new BigInteger(f.baseWeight)).ToArray();
            for (int tag = 0; tag < faces.Length; tag++)
            {
                if (faces[tag].texture == "none") continue;
                string opposite = config.Opposite(faces[tag].faceId);
                bool smooth = faces[tag].texture == "smooth";
                for (int i = 0; i < faces.Length; i++)
                {
                    int factor = faces[i].faceId == opposite
                        ? (smooth ? config.smoothOppositeWeight100 : config.roughOppositeWeight100)
                        : (smooth ? config.smoothOtherWeight100 : config.roughOtherWeight100);
                    if (factor <= 0) throw new InvalidOperationException("Non-positive probability factor.");
                    weights[i] *= factor;
                }
            }
            for (int i = 0; i < faces.Length; i++)
            {
                if (largeBuff) weights[i] *= faces[i].permanentPip == config.maxPipValue ? config.temporaryPipBiasWeight100 : 100;
                if (smallBuff) weights[i] *= faces[i].permanentPip == config.minPipValue ? config.temporaryPipBiasWeight100 : 100;
            }
            BigInteger total = weights.Aggregate(BigInteger.Zero, (sum, value) => sum + value);
            if (total <= 0) throw new InvalidOperationException("Probability total must be positive.");
            var probabilities = weights.Select(w => new Rational(w, total)).ToArray();
            if (highController != lowController)
            {
                var parameters = config.Controller(highController ? "C34" : "C35").parameters;
                int[] target = Enumerable.Range(0, faces.Length).Where(i => parameters.targetPips.Contains(faces[i].permanentPip)).ToArray();
                int[] donor = Enumerable.Range(0, faces.Length).Except(target).ToArray();
                if (target.Length > 0 && donor.Length > 0)
                {
                    Rational mass = donor.Select(i => probabilities[i]).Aggregate(Rational.Zero, (a, b) => a + b);
                    Rational cap = new Rational(parameters.maxTransferUnits, config.probabilityUnits);
                    Rational transfer = mass.CompareTo(cap) < 0 ? mass : cap;
                    var before = (Rational[])probabilities.Clone();
                    foreach (int i in donor) probabilities[i] = before[i] * (mass - transfer) / mass;
                    var groups = target.GroupBy(i => faces[i].permanentPip).ToArray();
                    foreach (var group in groups)
                    {
                        Rational bucket = group.Select(i => before[i]).Aggregate(Rational.Zero, (a, b) => a + b);
                        foreach (int i in group) probabilities[i] = before[i] + transfer / new Rational(groups.Length, 1) * before[i] / bucket;
                    }
                }
            }
            var units = new int[faces.Length];
            var remainder = new Rational[faces.Length];
            int assigned = 0;
            for (int i = 0; i < faces.Length; i++)
            {
                BigInteger scaled = probabilities[i].Numerator * config.probabilityUnits;
                units[i] = checked((int)BigInteger.DivRem(scaled, probabilities[i].Denominator, out BigInteger rest));
                remainder[i] = new Rational(rest, probabilities[i].Denominator);
                assigned = checked(assigned + units[i]);
            }
            int[] rank = Enumerable.Range(0, faces.Length)
                .OrderByDescending(i => remainder[i]).ThenBy(i => faces[i].faceId, StringComparer.Ordinal).ToArray();
            int left = config.probabilityUnits - assigned;
            if (left < 0 || left >= faces.Length) throw new InvalidOperationException("Invalid quantization remainder.");
            for (int i = 0; i < left; i++) units[rank[i]]++;
            if (units.Any(u => u < 0) || units.Sum() != config.probabilityUnits)
                throw new InvalidOperationException("Illegal probability vector: nonnegative mass must equal the declared precision.");
            return units;
        }

        /// <summary>Samples one physical face. Caller owns and persists rng; no other source is used.</summary>
        public static FaceState Sample(FaceState[] faces, int[] units, GameRng rng, GameConfig config)
        {
            if (units == null || faces == null || units.Length != faces.Length || units.Any(v => v < 0) || units.Sum() != config.probabilityUnits)
                throw new ArgumentException("Invalid CDF units.");
            int choice = rng.NextIndex(config.probabilityUnits);
            int cumulative = 0;
            foreach (int index in Enumerable.Range(0, faces.Length).OrderBy(i => faces[i].faceId, StringComparer.Ordinal))
            {
                cumulative += units[index];
                if (choice < cumulative) return faces[index];
            }
            throw new InvalidOperationException("CDF failed to cover its declared total.");
        }

        private readonly struct Rational : IComparable<Rational>
        {
            public readonly BigInteger Numerator, Denominator;
            public static Rational Zero => new Rational(0, 1);
            public Rational(BigInteger numerator, BigInteger denominator)
            {
                if (denominator <= 0) throw new ArgumentOutOfRangeException(nameof(denominator));
                BigInteger gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
                Numerator = numerator / gcd; Denominator = denominator / gcd;
            }
            public int CompareTo(Rational b) => (Numerator * b.Denominator).CompareTo(b.Numerator * Denominator);
            public static Rational operator +(Rational a, Rational b) => new Rational(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);
            public static Rational operator -(Rational a, Rational b) => new Rational(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);
            public static Rational operator *(Rational a, Rational b) => new Rational(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
            public static Rational operator /(Rational a, Rational b) => new Rational(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
        }
    }
}
