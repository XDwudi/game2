using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Numerics;
using DiceCraft.Core;

namespace DiceCraft.Presentation
{
    public enum PresentationSpeed { Normal, Fast, Instant }
    public enum PresentationPhase { Receipt, DiceContributions, BaseAndStructure, ControllerAndEnchant, FinalScore, PostScore, TargetState }
    public enum PresentationQuality { High, Standard, Low }

    public sealed class PresentationOptions
    {
        public PresentationSpeed Speed { get; }
        public bool ReducedMotion { get; }
        public PresentationQuality Quality { get; }
        public PresentationOptions(PresentationSpeed speed = PresentationSpeed.Normal, bool reducedMotion = false, PresentationQuality quality = PresentationQuality.Standard)
        { Speed = speed; ReducedMotion = reducedMotion; Quality = quality; }
    }

    /// <summary>Detached read-only display data. No object here holds a GameSession or an RNG.</summary>
    public sealed class PresentationEvent
    {
        public string EventId { get; }
        public int EventOrdinal { get; }
        public PresentationPhase Phase { get; }
        public string EffectFamily { get; }
        public string EmitterId { get; }
        public string StateOwnerId { get; }
        public string AbilityId { get; }
        public IReadOnlyList<string> SourceEmitterIds { get; }
        public IReadOnlyList<string> TargetDieIds { get; }
        public IReadOnlyList<string> TargetFaceIds { get; }
        public string BeforeValue { get; }
        public string Operand { get; }
        public string AfterValue { get; }
        public string Operation { get; }
        public string Detail { get; }
        public bool CountsForChain { get; }
        public bool IsScoring { get; }
        public string AudioCue { get; }
        internal PresentationEvent(string id, int ordinal, PresentationPhase phase, string family,
            string emitter = null, string owner = null, string ability = null, IEnumerable<string> sourceEmitters = null,
            IEnumerable<string> dice = null, IEnumerable<string> faces = null, string before = null,
            string operand = null, string after = null, string operation = null, string detail = null,
            bool countsForChain = false, bool isScoring = false, string audioCue = null)
        {
            EventId = id; EventOrdinal = ordinal; Phase = phase; EffectFamily = family;
            EmitterId = emitter; StateOwnerId = owner; AbilityId = ability;
            SourceEmitterIds = Copy(sourceEmitters ?? (emitter == null ? Array.Empty<string>() : new[] { emitter }));
            TargetDieIds = Copy(dice); TargetFaceIds = Copy(faces); BeforeValue = before; Operand = operand;
            AfterValue = after; Operation = operation; Detail = detail; CountsForChain = countsForChain;
            IsScoring = isScoring; AudioCue = audioCue;
        }
        private static IReadOnlyList<string> Copy(IEnumerable<string> values) => new ReadOnlyCollection<string>((values ?? Array.Empty<string>()).Where(x => x != null).ToArray());
    }

    public sealed class PresentationReceipt
    {
        public string GameReceiptId { get; }
        public string RunId { get; }
        public string HandId { get; }
        public string ScoreDecimal { get; }
        public string MultiplierNumeratorDecimal { get; }
        public string MultiplierDenominatorDecimal { get; }
        public string StageScoreBefore { get; }
        public string StageScoreAfter { get; }
        public string StageTarget { get; }
        public bool QualifiedBefore { get; }
        public bool QualifiedAfter { get; }
        public int ComboTier { get; }
        public IReadOnlyList<string> ScoringEmitterIds { get; }
        public int ChainCount => ScoringEmitterIds.Count;
        public IReadOnlyList<PresentationEvent> Events { get; }
        public bool IsSubmission { get; }
        public string FinalPhase { get; }
        public string FallbackReason { get; }
        internal PresentationReceipt(string receiptId, string runId, string handId, string score, ExactFraction multiplier,
            string stageBefore, string stageAfter, string target, bool qualifiedBefore, bool qualifiedAfter,
            IEnumerable<PresentationEvent> events, bool isSubmission, string finalPhase, string fallbackReason = null)
        {
            GameReceiptId = receiptId; RunId = runId; HandId = handId; ScoreDecimal = score;
            MultiplierNumeratorDecimal = ExactScoreMath.SaveDecimal(multiplier.Numerator);
            MultiplierDenominatorDecimal = ExactScoreMath.SaveDecimal(multiplier.Denominator);
            StageScoreBefore = stageBefore; StageScoreAfter = stageAfter; StageTarget = target;
            QualifiedBefore = qualifiedBefore; QualifiedAfter = qualifiedAfter;
            var array = events.ToArray(); Events = new ReadOnlyCollection<PresentationEvent>(array);
            ScoringEmitterIds = new ReadOnlyCollection<string>(array.Where(e => e.CountsForChain).SelectMany(e => e.SourceEmitterIds).Distinct(StringComparer.Ordinal).ToArray());
            ComboTier = ScorePresentationTier.Resolve(score, MultiplierNumeratorDecimal, MultiplierDenominatorDecimal);
            IsSubmission = isSubmission; FinalPhase = finalPhase; FallbackReason = fallbackReason;
        }
    }

    public static class ScorePresentationTier
    {
        private static readonly int[] Thresholds = { 8, 32, 128, 1024, 8192 };
        private static readonly int[] Durations = { 180, 260, 340, 420, 520, 600 };
        private static readonly int[] Particles = { 0, 4, 8, 16, 24, 36 };
        public static int Resolve(string score, string numerator, string denominator)
            => Resolve(ExactScoreMath.Parse(score), ExactScoreMath.Parse(numerator), ExactScoreMath.Parse(denominator));
        public static int Resolve(BigInteger score, BigInteger numerator, BigInteger denominator)
        {
            if (score < 0 || numerator < 0 || denominator <= 0) throw new ArgumentOutOfRangeException();
            if (score.IsZero || numerator.IsZero) return 0;
            int tier = 0;
            for (int i = 0; i < Thresholds.Length; i++) if (numerator >= denominator * Thresholds[i]) tier = i + 1;
            return tier;
        }
        public static int NormalDurationMs(int tier) => Durations[Checked(tier)];
        public static int ParticleCount(int tier, PresentationOptions options) => options.ReducedMotion || options.Speed == PresentationSpeed.Instant || options.Quality == PresentationQuality.Low ? 0 : Particles[Checked(tier)];
        public static float ScalePeak(int tier, PresentationOptions options) => options.ReducedMotion || options.Speed == PresentationSpeed.Instant ? 1f : new[] { 1f, 1.04f, 1.06f, 1.08f, 1.10f, 1.12f }[Checked(tier)];
        public static int ShakePixels(int tier, PresentationOptions options) => options.ReducedMotion || options.Speed != PresentationSpeed.Normal ? 0 : Math.Max(0, Checked(tier) - 2) * 2;
        private static int Checked(int tier) => tier >= 0 && tier <= 5 ? tier : throw new ArgumentOutOfRangeException(nameof(tier));
    }
}
