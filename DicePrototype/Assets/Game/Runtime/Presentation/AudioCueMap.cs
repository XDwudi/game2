using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Presentation
{
    public sealed class AudioCueDefinition
    {
        public string Id { get; }
        public string Bus { get; }
        public IReadOnlyList<string> AssetIds { get; }
        public int MinimumIntervalMs { get; }
        public int MaxConcurrent { get; }
        public int Priority { get; }
        public AudioCueDefinition(string id, string bus, string[] assets, int interval = 120, int concurrent = 8, int priority = 1)
        { Id = id; Bus = bus; AssetIds = Array.AsReadOnly((string[])assets.Clone()); MinimumIntervalMs = interval; MaxConcurrent = concurrent; Priority = priority; }
    }

    public static class AudioCueMap
    {
        private static readonly AudioCueDefinition[] cues = {
            new AudioCueDefinition("SFX_UI_HOVER", "UI", new[]{"sfx_ui_hover"}, 200, 1, 0),
            new AudioCueDefinition("SFX_UI_PRESS", "UI", new[]{"sfx_ui_press"}, 0, 8, 2),
            new AudioCueDefinition("SFX_UI_DENIED", "UI", new[]{"sfx_ui_denied"}, 300, 1, 2),
            new AudioCueDefinition("SFX_DICE_ROLL", "Dice", new[]{"sfx_dice_roll_01","sfx_dice_roll_02","sfx_dice_roll_03"}, 120, 2),
            new AudioCueDefinition("SFX_DICE_LAND", "Dice", new[]{"sfx_dice_land_01","sfx_dice_land_02","sfx_dice_land_03"}),
            new AudioCueDefinition("SFX_LOCK", "Dice", new[]{"sfx_lock"}, 120, 3),
            new AudioCueDefinition("SFX_CARD_TRIGGER", "Score", new[]{"sfx_card_trigger"}),
            new AudioCueDefinition("SFX_SCORE_STEP", "Score", new[]{"sfx_score_step"}),
            new AudioCueDefinition("SFX_SCORE_BURST", "Score", new[]{"sfx_score_burst_l3","sfx_score_burst_l4","sfx_score_burst_l5"}, 120, 1, 3),
            new AudioCueDefinition("SFX_COIN", "UI", new[]{"sfx_coin"}, 120, 2, 2),
            new AudioCueDefinition("SFX_GROW", "Score", new[]{"sfx_grow"}),
            new AudioCueDefinition("SFX_DYE_COPY", "Score", new[]{"sfx_dye","sfx_copy"}),
            new AudioCueDefinition("SFX_BREAK", "Dice", new[]{"sfx_dice_break","sfx_card_break"}, 120, 2, 2),
            new AudioCueDefinition("SFX_REFRESH", "UI", new[]{"sfx_refresh"}, 120, 1, 2),
            new AudioCueDefinition("SFX_QUALIFIED", "Score", new[]{"sfx_qualified"}, 120, 1, 3),
            new AudioCueDefinition("SFX_RUN_END", "Score", new[]{"sfx_run_win","sfx_run_fail"}, 120, 1, 3)
        };
        public static IReadOnlyList<AudioCueDefinition> Cues { get; } = Array.AsReadOnly(cues);
        public static AudioCueDefinition Find(string id) => Array.Find(cues, c => c.Id == id);
        public static string AssetFor(string cue, int variantIndex = 0, int tier = 0, string semanticVariant = null)
        {
            var def = Find(cue); if (def == null) return null;
            switch (cue)
            {
                case "SFX_SCORE_BURST": return tier < 3 ? null : def.AssetIds[Math.Min(2, tier - 3)];
                case "SFX_DYE_COPY": return def.AssetIds[semanticVariant == "copy" || semanticVariant == "DieCopied" ? 1 : 0];
                case "SFX_BREAK": return def.AssetIds[semanticVariant == "card" || semanticVariant == "ControllerDestroyed" ? 1 : 0];
                case "SFX_RUN_END": return def.AssetIds[semanticVariant == "RunWon" || semanticVariant == "win" ? 0 : 1];
                default: return def.AssetIds[(int)((uint)variantIndex % def.AssetIds.Count)];
            }
        }
        public static float ScorePitch(int stage) => (float)Math.Pow(2f, new[] { 0, 2, 4, 7, 9, 12 }[Math.Max(0, Math.Min(5, stage))] / 12f);
    }

    /// <summary>Pure admission logic: at most 8 voices, 120ms same-source merge, scoped cleanup.</summary>
    public sealed class AudioVoiceBudget
    {
        private sealed class Voice { public string Cue, Source, Receipt; public double End; public int Priority; }
        private readonly Voice[] voices = new Voice[8];
        private readonly Dictionary<string, double> recent = new Dictionary<string, double>(StringComparer.Ordinal);
        public int ActiveCount(double now) { Expire(now); return voices.Count(v => v != null); }
        public int Admit(string cue, string source, string receiptId, double now, double durationSeconds)
        {
            if (double.IsNaN(now) || double.IsInfinity(now) || double.IsNaN(durationSeconds) || double.IsInfinity(durationSeconds) || durationSeconds < 0) throw new ArgumentOutOfRangeException();
            var def = AudioCueMap.Find(cue); if (def == null) return -1; Expire(now);
            string key = cue + "\n" + ((cue == "SFX_UI_HOVER" || cue == "SFX_UI_DENIED") ? "global" : source ?? "global");
            if (recent.TryGetValue(key, out double last) && now - last < def.MinimumIntervalMs / 1000d) return -1;
            if (voices.Count(v => v != null && v.Cue == cue) >= def.MaxConcurrent) return -1;
            int slot = Array.FindIndex(voices, v => v == null);
            if (slot < 0)
            {
                int low = 0; for (int i = 1; i < voices.Length; i++) if (voices[i].Priority < voices[low].Priority) low = i;
                if (voices[low].Priority >= def.Priority) return -1; slot = low;
            }
            voices[slot] = new Voice { Cue = cue, Source = source, Receipt = receiptId, End = now + durationSeconds, Priority = def.Priority };
            recent[key] = now; return slot;
        }
        public int[] ReleaseReceipt(string receiptId)
        {
            var result = new List<int>(); for (int i = 0; i < voices.Length; i++) if (voices[i]?.Receipt == receiptId) { voices[i] = null; result.Add(i); }
            return result.ToArray();
        }
        private void Expire(double now)
        {
            for (int i = 0; i < voices.Length; i++) if (voices[i] != null && voices[i].End <= now) voices[i] = null;
            // Bounded replay history; old sources cannot accumulate for a long Run.
            if (recent.Count > 256) foreach (string key in recent.Where(p => now - p.Value > 1).Select(p => p.Key).ToArray()) recent.Remove(key);
        }
    }

}
