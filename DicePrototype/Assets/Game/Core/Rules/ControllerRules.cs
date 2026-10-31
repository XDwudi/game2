using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    /// <summary>Pure source resolution, equipment validation and explicitly supplied RNG enchant draws.</summary>
    public static class ControllerRules
    {
        public static ControllerInstance[] Resolve(SubmissionSnapshot s)
        {
            if (s.equippedControllers != null && s.equippedControllers.Length > 0) return s.equippedControllers;
            return (s.equippedControllerIds ?? Array.Empty<string>()).Select((id, i) => new ControllerInstance { definitionId = id, instanceId = "score-" + i, acquiredSerial = i + 1 }).ToArray();
        }
        public static MimicBinding[] FreezeBindings(IEnumerable<ControllerInstance> instances)
        {
            var all = instances.ToArray();
            return all.Select((c, i) => new { c, i }).Where(x => x.c.alive && x.c.definitionId == "C41")
                .Select(x => new MimicBinding { sourceInstanceId = x.c.instanceId, targetInstanceId = x.i + 1 < all.Length ? all[x.i + 1].instanceId : null }).ToArray();
        }
        public static AbilitySource[] ResolveSources(IEnumerable<ControllerInstance> instances, MimicBinding[] bindings = null)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances));
            var all = instances.ToArray();
            if (all.Any(c => c == null || string.IsNullOrEmpty(c.instanceId)) || all.Select(c => c.instanceId).Distinct(StringComparer.Ordinal).Count() != all.Length) throw new ArgumentException("Invalid controller instance IDs.");
            if (all.Any(c => !IsActiveDefinition(c.definitionId))) throw new ArgumentException("Unknown or retired controller definition.");
            var lookup = all.ToDictionary(c => c.instanceId, StringComparer.Ordinal);
            var frozen = bindings ?? FreezeBindings(all);
            if (frozen.Any(b => b == null || string.IsNullOrEmpty(b.sourceInstanceId)) || frozen.Select(b => b.sourceInstanceId).Distinct(StringComparer.Ordinal).Count() != frozen.Length) throw new ArgumentException("Invalid mimic bindings.");
            var map = frozen.ToDictionary(b => b.sourceInstanceId, b => b.targetInstanceId, StringComparer.Ordinal);
            var sources = new List<AbilitySource>();
            for (int i = 0; i < all.Length; i++)
            {
                var emitter = all[i]; if (!emitter.alive) continue;
                var owner = emitter; var seen = new HashSet<string>(StringComparer.Ordinal);
                while (owner != null && owner.definitionId == "C41")
                {
                    if (!seen.Add(owner.instanceId) || seen.Count > 32) throw new ArgumentException("Cyclic or excessive mimic chain.");
                    owner = map.TryGetValue(owner.instanceId, out string target) && !string.IsNullOrEmpty(target) && lookup.TryGetValue(target, out var candidate) && candidate.alive ? candidate : null;
                }
                sources.Add(new AbilitySource { emitterId = emitter.instanceId, emitterDefinitionId = emitter.definitionId, abilityId = owner?.definitionId, stateOwnerId = owner?.instanceId, state = owner?.state?.Clone(), enchantmentId = emitter.enchantmentId, orderIndex = i });
            }
            return sources.ToArray();
        }
        public static int SourceCount(IEnumerable<AbilitySource> sources, string abilityId) => sources.Count(s => s.abilityId == abilityId);
        private static bool IsActiveDefinition(string id)
            => id != null && id.Length == 3 && id[0] == 'C' && int.TryParse(id.Substring(1), out int number) && number >= 1 && number <= 53 && id == "C" + number.ToString("D2") && number != 4 && number != 16 && number != 23;
        public static bool Has(IEnumerable<ControllerInstance> instances, string id) => instances.Any(c => c.alive && c.definitionId == id);
        public static int Capacity(IEnumerable<ControllerInstance> instances, GameConfig cfg, bool debug = false, MimicBinding[] bindings = null)
        {
            int n = cfg.initialSubmitCapacity;
            foreach (var s in ResolveSources(instances, bindings)) if (s.abilityId == "C09" || s.abilityId == "C21" || s.abilityId == "C22") n = checked(n + cfg.Controller(s.abilityId).parameters.capacityDelta);
            return Math.Max(1, Math.Min(debug ? cfg.debug.maxSubmitCapacity : cfg.maxSubmitCapacity, n));
        }
        public static int RollBudget(IEnumerable<ControllerInstance> instances, GameConfig cfg, int stageBonus = 0, MimicBinding[] bindings = null)
            => checked(cfg.totalRollsPerHand + stageBonus + instances.Count(c => c.alive && c.enchantmentId == "roll1") * cfg.enchantRules.rollBonus);
        public static int SubmissionBudget(IEnumerable<ControllerInstance> instances, GameConfig cfg, MimicBinding[] bindings = null)
            => checked(cfg.submissionsPerStage + instances.Count(c => c.alive && c.enchantmentId == "submit1") * cfg.enchantRules.submissionBonus);
        public static int RollsPerHand(IEnumerable<ControllerInstance> instances, GameConfig cfg) => RollBudget(instances, cfg);
        public static int SubmissionsPerStage(IEnumerable<ControllerInstance> instances, GameConfig cfg) => SubmissionBudget(instances, cfg);
        public static int OrdinaryUsed(IEnumerable<ControllerInstance> instances) => instances.Count(c => c.alive && c.enchantmentId != "slotless");
        public static long SalePrice(ControllerInstance instance, GameConfig cfg)
        {
            if (instance == null || !instance.alive) throw new ArgumentException("Cannot sell absent controller.");
            cfg.Controller(instance.definitionId);
            long fund = instance.state?.fundAccrual ?? 0;
            if (fund < 0 || fund % 3 != 0) throw new ArgumentException("Invalid fund accrual.");
            return checked((long)cfg.baseControllerSellPrice + ((instance.definitionId == "C41" || instance.definitionId == "C52") ? fund : 0));
        }
        public static int EnchantPrice(string enchantmentId, GameConfig cfg) => cfg.Enchantment(enchantmentId).buyPremium;
        public static bool IsEnchantmentLegal(string controllerId, string enchantmentId, GameConfig cfg)
            => cfg.controllers.Any(c => c.id == controllerId) && cfg.enchantments.Any(e => e.id == enchantmentId) && (!cfg.enchantRules.nonSubmitControllerIds.Contains(controllerId) || (enchantmentId != "base10" && enchantmentId != "mult2"));
        public static void ValidateEquipment(IEnumerable<ControllerInstance> instances, GameConfig cfg, bool debug = false) => ValidateInstances(instances, cfg, debug, true);
        internal static void ValidateInstances(IEnumerable<ControllerInstance> instances, GameConfig cfg, bool debug, bool enforceSlots)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances)); var all = instances.ToArray();
            if (all.Any(c => c == null) || all.Count(c => c.alive) > (debug ? cfg.debug.maxControllerInstances : cfg.maxControllerInstances) || all.Any(c => string.IsNullOrEmpty(c.instanceId)) || all.Select(c => c.instanceId).Distinct(StringComparer.Ordinal).Count() != all.Length) throw new ArgumentException("Invalid controller identities or limits.");
            if (enforceSlots && !debug && (all.Where(c => c.alive).Select(c => c.definitionId).Distinct(StringComparer.Ordinal).Count() != all.Count(c => c.alive) || OrdinaryUsed(all) > cfg.initialControllerSlots)) throw new ArgumentException("Normal equipment limits.");
            foreach (var c in all)
            {
                cfg.Controller(c.definitionId);
                if (!cfg.enchantments.Any(e => e.id == c.enchantmentId) || (!debug && !IsEnchantmentLegal(c.definitionId, c.enchantmentId, cfg))) throw new ArgumentException("Illegal controller enchantment.");
                if (c.state == null || c.state.fundAccrual < 0 || c.state.fundAccrual % 3 != 0 || c.state.turnoverUnits2 < 3) throw new ArgumentException("Invalid controller state.");
                if (!string.IsNullOrEmpty(c.state.activeColor) && c.state.activeColor != "red" && c.state.activeColor != "blue") throw new ArgumentException("Invalid dynamic color.");
                if (c.definitionId == "C45" && c.state.activeColor != "red" && c.state.activeColor != "blue") throw new ArgumentException("C45 requires persisted active color.");
            }
        }
        private static int Weight(string controllerId, string enchant, GameConfig cfg)
        {
            if (!cfg.enchantRules.nonSubmitControllerIds.Contains(controllerId)) return cfg.Enchantment(enchant).weight;
            var w = cfg.enchantRules.nonSubmitDrawWeights;
            switch (enchant) { case "none": return w.none; case "slotless": return w.slotless; case "submit1": return w.submit1; case "roll1": return w.roll1; default: return 0; }
        }
        public static string[] ReplacementEnchantments(string id, string current, GameConfig cfg)
        {
            cfg.Controller(id); if (!IsEnchantmentLegal(id, current, cfg)) throw new ArgumentException("Invalid current enchantment.");
            return cfg.enchantRules.rawDrawOrder.Where(e => e != "none" && e != current && Weight(id, e, cfg) > 0).ToArray();
        }
        public static string NormalizeNewEnchantment(string controllerId, string raw, GameConfig cfg)
        {
            if (!IsEnchantmentLegal(controllerId, raw, cfg)) throw new ArgumentException("Illegal enchantment; v0.6 never falls back or redraws."); return raw;
        }
        public static string DrawEnchantment(string controllerId, string ownedEnchantment, GameRng rng, GameConfig cfg)
        {
            cfg.Controller(controllerId);
            var pool = ownedEnchantment == null ? cfg.enchantRules.rawDrawOrder.Where(e => Weight(controllerId, e, cfg) > 0).ToArray() : ReplacementEnchantments(controllerId, ownedEnchantment, cfg);
            if (pool.Length == 0) throw new ArgumentException("No legal replacement enchantment.");
            int draw = rng.NextIndex(pool.Sum(e => Weight(controllerId, e, cfg)));
            foreach (string e in pool) { int weight = Weight(controllerId, e, cfg); if (draw < weight) return e; draw -= weight; }
            throw new InvalidOperationException("Enchantment CDF uncovered draw.");
        }
    }
}
