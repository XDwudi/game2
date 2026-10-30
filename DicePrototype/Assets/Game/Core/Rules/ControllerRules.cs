using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    /// <summary>Shared equipment, budget and deterministic enchantment policy.</summary>
    public static class ControllerRules
    {
        public static ControllerInstance[] Resolve(SubmissionSnapshot s)
        {
            if (s.equippedControllers != null && s.equippedControllers.Length > 0) return s.equippedControllers;
            return (s.equippedControllerIds ?? Array.Empty<string>()).Select((id, i) => new ControllerInstance
                { definitionId = id, instanceId = "score-" + i, enchantmentId = "none", acquiredSerial = i + 1 }).ToArray();
        }

        public static bool Has(IEnumerable<ControllerInstance> instances, string id) => instances.Any(c => c.definitionId == id);
        public static int Capacity(IEnumerable<ControllerInstance> instances, GameConfig cfg)
        {
            var ids = new HashSet<string>(instances.Select(c => c.definitionId), StringComparer.Ordinal);
            int result = cfg.initialSubmitCapacity;
            foreach (var id in new[] { "C09", "C21", "C22" }) if (ids.Contains(id)) result += cfg.Controller(id).parameters.capacityDelta;
            return Math.Max(1, Math.Min(cfg.maxSubmitCapacity, result));
        }
        public static int RollBudget(IEnumerable<ControllerInstance> instances, GameConfig cfg, int stageBonus = 0)
        {
            var ids = new HashSet<string>(instances.Select(c => c.definitionId), StringComparer.Ordinal);
            return checked(cfg.totalRollsPerHand + stageBonus + new[] { "C04", "C16", "C23" }.Where(ids.Contains).Sum(id => cfg.Controller(id).parameters.rollBonus));
        }
        public static int SubmissionBudget(IEnumerable<ControllerInstance> instances, GameConfig cfg)
            => checked(cfg.submissionsPerStage + (Has(instances, "C04") ? cfg.Controller("C04").parameters.submissionBonus : 0));
        public static int OrdinaryUsed(IEnumerable<ControllerInstance> instances) => instances.Count(c => c.enchantmentId != "slotless");
        public static int EnchantPrice(string enchantmentId, GameConfig cfg) => cfg.Enchantment(enchantmentId).buyPremium;
        public static bool IsEnchantmentLegal(string controllerId, string enchantmentId, GameConfig cfg)
            => cfg.controllers.Any(c => c.id == controllerId) && cfg.enchantments.Any(e => e.id == enchantmentId) &&
               (!cfg.enchantRules.nonSubmitControllerIds.Contains(controllerId) || enchantmentId == "none" || enchantmentId == "slotless");

        public static void ValidateEquipment(IEnumerable<ControllerInstance> instances, GameConfig cfg, bool debug = false)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            var all = instances.ToArray();
            if (all.Length > cfg.maxControllerInstances || all.Any(c => c == null) ||
                all.Select(c => c.definitionId).Distinct(StringComparer.Ordinal).Count() != all.Length ||
                (!debug && OrdinaryUsed(all) > cfg.initialControllerSlots)) throw new ArgumentException("Invalid controller equipment limits.");
            foreach (var c in all)
            {
                cfg.Controller(c.definitionId);
                if (!IsEnchantmentLegal(c.definitionId, c.enchantmentId, cfg)) throw new ArgumentException("Illegal controller enchantment.");
            }
        }

        public static string[] ReplacementEnchantments(string id, string current, GameConfig cfg)
        {
            cfg.Controller(id);
            if (!IsEnchantmentLegal(id, current, cfg)) throw new ArgumentException("Invalid current enchantment.");
            return cfg.enchantments.Where(e => e.id != "none" && e.id != current && IsEnchantmentLegal(id, e.id, cfg)).Select(e => e.id).ToArray();
        }
        public static string NormalizeNewEnchantment(string controllerId, string raw, GameConfig cfg)
        {
            cfg.Controller(controllerId);
            if (!cfg.enchantments.Any(e => e.id == raw)) throw new ArgumentException("Unknown enchantment.");
            return IsEnchantmentLegal(controllerId, raw, cfg) ? raw : cfg.enchantRules.ineligibleScoreEnchantFallback;
        }
        public static string DrawEnchantment(string controllerId, string ownedEnchantment, GameRng rng, GameConfig cfg)
        {
            cfg.Controller(controllerId);
            if (ownedEnchantment != null && !IsEnchantmentLegal(controllerId, ownedEnchantment, cfg)) throw new ArgumentException("Invalid current enchantment.");
            var pool = ownedEnchantment == null ? cfg.enchantments : cfg.enchantments
                .Where(e => e.id != "none" && e.id != ownedEnchantment && IsEnchantmentLegal(controllerId, e.id, cfg)).ToArray();
            if (pool.Length == 0) throw new ArgumentException("No legal replacement enchantment.");
            int draw = rng.NextIndex(pool.Sum(e => e.weight));
            foreach (var e in pool)
            {
                if (draw < e.weight) return ownedEnchantment == null ? NormalizeNewEnchantment(controllerId, e.id, cfg) : e.id;
                draw -= e.weight;
            }
            throw new InvalidOperationException("Enchantment CDF did not cover draw.");
        }
    }
}
