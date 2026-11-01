using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace DiceCraft.Presentation
{
    public sealed class PresentationBeat
    {
        public PresentationPhase Phase { get; }
        public int StartMs { get; }
        public int DurationMs { get; }
        public string FocusEmitterId { get; }
        public bool IsAggregate { get; }
        public IReadOnlyList<PresentationEvent> Events { get; }
        internal PresentationBeat(PresentationPhase phase, int start, int duration, string focus, bool aggregate, IEnumerable<PresentationEvent> events)
        { Phase = phase; StartMs = start; DurationMs = duration; FocusEmitterId = focus; IsAggregate = aggregate; Events = new ReadOnlyCollection<PresentationEvent>(events.ToArray()); }
    }

    public sealed class PresentationSequence
    {
        public PresentationReceipt Receipt { get; }
        public PresentationOptions Options { get; }
        public IReadOnlyList<PresentationBeat> Beats { get; }
        public int TotalDurationMs { get; }
        public int QualifiedNonblockingTailMs => Receipt.QualifiedAfter && !Receipt.QualifiedBefore && Options.Speed == PresentationSpeed.Normal ? 200 : 0;
        private PresentationSequence(PresentationReceipt receipt, PresentationOptions options, List<PresentationBeat> beats, int duration)
        { Receipt = receipt; Options = options; Beats = new ReadOnlyCollection<PresentationBeat>(beats); TotalDurationMs = duration; }
        public static PresentationSequence Create(PresentationReceipt receipt, PresentationOptions options = null)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt)); options = options ?? new PresentationOptions();
            var raw = new List<PresentationBeat>(); int cursor = 0;
            Action<PresentationPhase, int, IEnumerable<PresentationEvent>, string, bool> add = (phase, duration, events, focus, aggregate) =>
            {
                var list = events.ToArray(); if (list.Length == 0) return;
                raw.Add(new PresentationBeat(phase, cursor, duration, focus, aggregate, list)); cursor += duration;
            };
            Func<PresentationPhase, IEnumerable<PresentationEvent>> phaseEvents = phase => receipt.Events.Where(e => e.Phase == phase);
            add(PresentationPhase.Receipt, 100, phaseEvents(PresentationPhase.Receipt), null, false);
            add(PresentationPhase.DiceContributions, Math.Min(420, Math.Max(120, receipt.Events.Where(e => e.Phase == PresentationPhase.DiceContributions).SelectMany(e => e.TargetDieIds).Distinct().Count() * 60)), phaseEvents(PresentationPhase.DiceContributions), null, true);
            add(PresentationPhase.BaseAndStructure, 280, phaseEvents(PresentationPhase.BaseAndStructure), null, true);
            // Four individual main foci; every other source remains in one aggregate beat and in the receipt.
            var allControllerEvents = phaseEvents(PresentationPhase.ControllerAndEnchant).ToArray();
            // Reveal a shared owner once before any copied/original multiplier is presented.
            add(PresentationPhase.ControllerAndEnchant, 140, allControllerEvents.Where(e => e.EffectFamily == "random_reveal"), null, true);
            var controllerEvents = allControllerEvents.Where(e => e.EffectFamily != "random_reveal").ToArray();
            var sourceIds = controllerEvents.Select(e => e.EmitterId ?? e.StateOwnerId).Where(x => !string.IsNullOrEmpty(x)).Distinct(StringComparer.Ordinal).ToArray();
            foreach (string id in sourceIds.Take(4)) add(PresentationPhase.ControllerAndEnchant, 140, controllerEvents.Where(e => (e.EmitterId ?? e.StateOwnerId) == id), id, false);
            var rest = controllerEvents.Where(e => !sourceIds.Take(4).Contains(e.EmitterId ?? e.StateOwnerId)).ToArray();
            add(PresentationPhase.ControllerAndEnchant, 140, rest, null, true);
            add(PresentationPhase.FinalScore, ScorePresentationTier.NormalDurationMs(receipt.ComboTier), phaseEvents(PresentationPhase.FinalScore), null, false);
            add(PresentationPhase.PostScore, 480, phaseEvents(PresentationPhase.PostScore), null, true);
            add(PresentationPhase.TargetState, 280, phaseEvents(PresentationPhase.TargetState), null, false);
            if (cursor > 3200) throw new InvalidOperationException("Normal presentation exceeds its frozen budget.");
            if (options.Speed == PresentationSpeed.Instant)
                return new PresentationSequence(receipt, options, new List<PresentationBeat> { new PresentationBeat(PresentationPhase.FinalScore, 0, 0, null, true, receipt.Events) }, 0);
            if (options.Speed == PresentationSpeed.Fast)
            {
                // Compress phase boundaries, never raise flash frequency. One merged beat per phase.
                var fast = new List<PresentationBeat>(); int elapsed = 0;
                foreach (var group in raw.GroupBy(b => b.Phase))
                {
                    int duration = group.Key == PresentationPhase.FinalScore ? Math.Min(200, ScorePresentationTier.NormalDurationMs(receipt.ComboTier)) : group.Key == PresentationPhase.PostScore ? 160 : group.Key == PresentationPhase.Receipt ? 40 : 100;
                    fast.Add(new PresentationBeat(group.Key, elapsed, duration, null, true, group.SelectMany(b => b.Events))); elapsed += duration;
                }
                if (elapsed > 800) throw new InvalidOperationException("Fast presentation exceeds its frozen budget.");
                return new PresentationSequence(receipt, options, fast, elapsed);
            }
            return new PresentationSequence(receipt, options, raw, cursor);
        }
    }

    public sealed class PresentationClock
    {
        public bool Paused { get; set; }
        public double ElapsedMilliseconds { get; private set; }
        public void Advance(double unscaledSeconds)
        { if (double.IsNaN(unscaledSeconds) || double.IsInfinity(unscaledSeconds) || unscaledSeconds < 0) throw new ArgumentOutOfRangeException(nameof(unscaledSeconds)); if (!Paused) ElapsedMilliseconds += unscaledSeconds * 1000d; }
        public void Reset() { ElapsedMilliseconds = 0; Paused = false; }
    }

    /// <summary>Visual-only playback. Callbacks may update views; no callback is needed to commit game state.</summary>
    public sealed class PresentationPlayer
    {
        private readonly HashSet<string> played = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> completedReceipts = new HashSet<string>(StringComparer.Ordinal);
        private int nextBeat;
        private bool replay;
        public PresentationClock Clock { get; } = new PresentationClock();
        public PresentationSequence Sequence { get; private set; }
        public bool IsPlaying { get; private set; }
        public bool IsReplay => replay;
        public event Action<PresentationBeat> BeatStarted;
        public event Action<PresentationReceipt> FinalStateRequested;
        public event Action<PresentationReceipt> Completed;
        public bool WasPlayed(string receiptId, string eventId) => played.Contains(Key(receiptId, eventId));
        public bool Play(PresentationSequence sequence, bool readOnlyReplay = false)
        {
            if (sequence == null) throw new ArgumentNullException(nameof(sequence));
            if (IsPlaying) Skip();
            Sequence = sequence; replay = readOnlyReplay; nextBeat = 0; Clock.Reset();
            if (!readOnlyReplay && completedReceipts.Contains(sequence.Receipt.GameReceiptId)) { FinalStateRequested?.Invoke(sequence.Receipt); return false; }
            IsPlaying = true;
            if (sequence.Options.Speed == PresentationSpeed.Instant) { Skip(); return true; }
            DispatchReady(); return true;
        }
        public void Tick(double unscaledSeconds)
        {
            if (!IsPlaying) return; Clock.Advance(unscaledSeconds); if (Clock.Paused) return;
            DispatchReady(); if (Clock.ElapsedMilliseconds >= Sequence.TotalDurationMs) Finish();
        }
        public void Skip() { if (!IsPlaying) return; Finish(); }
        public void Cancel() => Skip();
        public void Restore(PresentationReceipt receipt)
        {
            if (receipt == null) throw new ArgumentNullException(nameof(receipt));
            if (IsPlaying) Skip(); MarkComplete(receipt); FinalStateRequested?.Invoke(receipt);
        }
        public void ChangeReducedMotion() => Skip();
        private void DispatchReady()
        {
            while (IsPlaying && nextBeat < Sequence.Beats.Count && Sequence.Beats[nextBeat].StartMs <= Clock.ElapsedMilliseconds)
            {
                var beat = Sequence.Beats[nextBeat++];
                var unseen = replay ? beat.Events.ToArray() : beat.Events.Where(e => !WasPlayed(Sequence.Receipt.GameReceiptId, e.EventId)).ToArray();
                foreach (var e in unseen) if (!replay) played.Add(Key(Sequence.Receipt.GameReceiptId, e.EventId));
                if (unseen.Length > 0) BeatStarted?.Invoke(new PresentationBeat(beat.Phase, beat.StartMs, beat.DurationMs, beat.FocusEmitterId, beat.IsAggregate, unseen));
            }
        }
        private void Finish()
        {
            var receipt = Sequence.Receipt; IsPlaying = false;
            if (!replay) MarkComplete(receipt);
            FinalStateRequested?.Invoke(receipt); Completed?.Invoke(receipt);
        }
        private void MarkComplete(PresentationReceipt receipt)
        { completedReceipts.Add(receipt.GameReceiptId); foreach (var e in receipt.Events) played.Add(Key(receipt.GameReceiptId, e.EventId)); }
        private static string Key(string receipt, string eventId) => receipt + "\n" + eventId;
    }
}
