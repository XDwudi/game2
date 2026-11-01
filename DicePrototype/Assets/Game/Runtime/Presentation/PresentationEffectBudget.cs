using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Presentation
{
    public enum EffectPoolKind { NumberLabel, SourceLine, Particle, Fragment, Halo }
    /// <summary>One main group and three auxiliary groups globally, in addition to each pool cap.</summary>
    public sealed class PresentationEffectBudget
    {
        private sealed class Lease { public string Receipt, Group; public bool Main; public Dictionary<EffectPoolKind, int> Counts = new Dictionary<EffectPoolKind, int>(); }
        private readonly Dictionary<string, Lease> leases = new Dictionary<string, Lease>(StringComparer.Ordinal);
        public bool TryBeginGroup(string receiptId, string groupId, bool main)
        {
            string key = Key(receiptId, groupId); if (leases.ContainsKey(key)) return true;
            if (leases.Values.Count(l => l.Main == main) >= (main ? 1 : 3)) return false;
            leases.Add(key, new Lease { Receipt = receiptId, Group = groupId, Main = main }); return true;
        }
        public int Reserve(string receiptId, string groupId, EffectPoolKind pool, int requested, PresentationOptions options)
        {
            if (requested < 0) throw new ArgumentOutOfRangeException(nameof(requested));
            if (!leases.TryGetValue(Key(receiptId, groupId), out var lease)) return 0;
            int used = leases.Values.Sum(l => l.Counts.TryGetValue(pool, out int n) ? n : 0);
            int granted = Math.Min(requested, Math.Max(0, Limit(pool, options ?? new PresentationOptions()) - used));
            lease.Counts[pool] = (lease.Counts.TryGetValue(pool, out int current) ? current : 0) + granted; return granted;
        }
        public void EndGroup(string receiptId, string groupId) => leases.Remove(Key(receiptId, groupId));
        public void ReleaseReceipt(string receiptId) { foreach (string key in leases.Where(p => p.Value.Receipt == receiptId).Select(p => p.Key).ToArray()) leases.Remove(key); }
        public int ActiveGroups => leases.Count;
        public static int Prewarm(EffectPoolKind kind)
        { switch (kind) { case EffectPoolKind.NumberLabel: return 16; case EffectPoolKind.SourceLine: return 12; case EffectPoolKind.Particle: return 256; case EffectPoolKind.Fragment: return 12; case EffectPoolKind.Halo: return 8; default: throw new ArgumentOutOfRangeException(nameof(kind)); } }
        public static int Limit(EffectPoolKind kind, PresentationOptions options)
        {
            switch (kind)
            {
                case EffectPoolKind.NumberLabel: return 8;
                case EffectPoolKind.SourceLine: return 3;
                case EffectPoolKind.Particle: return options.ReducedMotion || options.Speed == PresentationSpeed.Instant ? 0 : options.Quality == PresentationQuality.Low ? 128 : 512;
                case EffectPoolKind.Fragment: return options.ReducedMotion || options.Speed == PresentationSpeed.Instant ? 0 : 12;
                case EffectPoolKind.Halo: return options.Quality == PresentationQuality.Low ? 1 : 3;
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }
        private static string Key(string receipt, string group) => receipt + "\n" + group;
    }

    /// <summary>Prewarmed bounded view pool; reset must clear every visual/binding/completion property.</summary>
    public sealed class PresentationObjectPool<T> where T : class
    {
        private readonly Stack<T> available = new Stack<T>();
        private readonly HashSet<T> leased = new HashSet<T>();
        private readonly Func<T> create;
        private readonly Action<T> reset;
        private readonly int capacity;
        public int CreatedCount { get; private set; }
        public int LeasedCount => leased.Count;
        public PresentationObjectPool(int prewarm, int maximum, Func<T> create, Action<T> reset)
        {
            if (maximum < 0 || prewarm < 0 || prewarm > maximum || create == null || reset == null) throw new ArgumentException("Invalid visual pool contract.");
            capacity = maximum; this.create = create; this.reset = reset;
            for (int i = 0; i < prewarm; i++) { var item = create(); if (item == null) throw new InvalidOperationException("Pool factory returned null."); reset(item); available.Push(item); CreatedCount++; }
        }
        public T Rent()
        {
            T item;
            if (available.Count > 0) item = available.Pop();
            else { if (CreatedCount >= capacity) return null; item = create(); if (item == null) throw new InvalidOperationException("Pool factory returned null."); CreatedCount++; }
            reset(item); leased.Add(item); return item;
        }
        public void Return(T item) { if (item == null || !leased.Remove(item)) return; reset(item); available.Push(item); }
    }
}
