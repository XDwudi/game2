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
        public static AbilitySource[] ResolveSources(IEnumerable<ControllerInstance> instances, MimicBinding[] bindings = null, IEnumerable<string> disabledIds = null)
        {
            // v1.0: a controller disabled for the stage has no ability and cannot be copied; it still occupies its place.
            var disabled = new HashSet<string>(disabledIds ?? Array.Empty<string>(), StringComparer.Ordinal);
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
                var owner = disabled.Contains(emitter.instanceId) ? null : emitter; var seen = new HashSet<string>(StringComparer.Ordinal);
                while (owner != null && owner.definitionId == "C41")
                {
                    if (!seen.Add(owner.instanceId) || seen.Count > 32) throw new ArgumentException("Cyclic or excessive mimic chain.");
                    owner = map.TryGetValue(owner.instanceId, out string target) && !string.IsNullOrEmpty(target) && lookup.TryGetValue(target, out var candidate) && candidate.alive && !disabled.Contains(candidate.instanceId) ? candidate : null;
                }
                sources.Add(new AbilitySource { emitterId = emitter.instanceId, emitterDefinitionId = emitter.definitionId, abilityId = owner?.definitionId, stateOwnerId = owner?.instanceId, state = owner?.state?.Clone(), enchantmentId = emitter.enchantmentId, orderIndex = i });
            }
            return sources.ToArray();
        }
        public static int SourceCount(IEnumerable<AbilitySource> sources, string abilityId) => sources.Count(s => s.abilityId == abilityId);
        private static bool IsActiveDefinition(string id)
            => id != null && id.Length >= 3 && id.Length <= 4 && id[0] == 'C' && int.TryParse(id.Substring(1), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int number) && number >= 1 && number <= LastControllerNumber && id == "C" + number.ToString("D2") && Array.IndexOf(RetiredControllerNumbers, number) < 0;
        /// <summary>v1.1.1: controller ids run C01-C156; these numbers are retired for good and never accepted.</summary>
        public const int LastControllerNumber = 156;
        private static readonly int[] RetiredControllerNumbers = { 4, 16, 23, 90, 128, 145 };
        /// <summary>v1.1.1: stable enchantment ids. base10 adds enchantRules.baseAdd to the base score, mult2 adds
        /// enchantRules.scoreAdd100 to the additive zone, slotless frees the ordinary slot.</summary>
        public const string BaseEnchantmentId = "base10", ScoreEnchantmentId = "mult2", SlotlessEnchantmentId = "slotless";
        private static readonly HashSet<string> GrowthAdditives = new HashSet<string>(new[] { "C51", "C56", "C57", "C60", "C61", "C129", "C132", "C134" }, StringComparer.Ordinal);
        private static readonly HashSet<string> GrowthMultipliers = new HashSet<string>(new[] { "C54", "C58", "C100", "C125" }, StringComparer.Ordinal);
        private static readonly HashSet<string> GrowthBases = new HashSet<string>(new[] { "C55", "C59", "C62" }, StringComparer.Ordinal);
        /// <summary>v1.1.1 growth cards (section 5). Additive: adds GrowthValue hundredths to the additive zone.
        /// Multiplier: multiplies the score by GrowthValue / 100. Base: adds GrowthValue points to the base score.</summary>
        public static bool IsGrowthAdditive(string id) => GrowthAdditives.Contains(id);
        public static bool IsGrowthMultiplier(string id) => GrowthMultipliers.Contains(id);
        public static bool IsGrowthBase(string id) => GrowthBases.Contains(id);
        public static bool HasGrowth(string id) => GrowthAdditives.Contains(id) || GrowthMultipliers.Contains(id) || GrowthBases.Contains(id);
        /// <summary>v1.1.1: what a growth card is worth now = configured start + catch-up + grown part, in the unit of the
        /// card. `halved` reads the grown part at half, rounded down (boss effect GrowthHalf); nothing is stored.</summary>
        public static long GrowthValue(GameConfig cfg, string abilityId, ControllerDynamicState state, bool halved = false)
        {
            if (!HasGrowth(abilityId) || state == null) throw new ArgumentException("Not a growth card.");
            return checked(cfg.Controller(abilityId).parameters.growthStart + state.growthCatchup + (halved ? state.growth / 2 : state.growth));
        }
        /// <summary>v1.1.1 section 9.3: a multiplier card that is never offered again while one is owned.</summary>
        public static bool IsUniqueController(string id, GameConfig cfg) => cfg.shop.uniqueControllerIds.Contains(id);
        /// <summary>True when an offer of `id` must be filtered out because a live copy of that unique card is owned.</summary>
        public static bool OwnsUniqueController(IEnumerable<ControllerInstance> instances, string id, GameConfig cfg)
            => IsUniqueController(id, cfg) && instances.Any(c => c != null && c.alive && c.definitionId == id);
        /// <summary>v1.0: C141 counts cleared stages in the same field without being a growth card.</summary>
        public static bool HasGrowthCounter(string id) => HasGrowth(id) || id == "C141";
        public static bool ModifierHas(GameConfig cfg, string modifierId, string effect) => !string.IsNullOrEmpty(modifierId) && cfg.Modifier(modifierId).effects.Contains(effect);
        /// <summary>
        /// v0.9 C100: which controllers the altars destroy when the next stage starts, as (altar, victim) pairs in the
        /// order they happen. Each real altar, left to right, destroys its immediate right neighbour; a destroyed altar
        /// does not act. Pure: StartStage applies exactly this plan and the interface shows it beforehand.
        /// </summary>
        public static KeyValuePair<string, string>[] AltarPlan(IEnumerable<ControllerInstance> instances)
        {
            var order = instances.Where(c => c != null && c.alive).ToList(); var plan = new List<KeyValuePair<string, string>>();
            for (int i = 0; i + 1 < order.Count; i++)
            {
                if (order[i].definitionId != "C100") continue;
                plan.Add(new KeyValuePair<string, string>(order[i].instanceId, order[i + 1].instanceId)); order.RemoveAt(i + 1);
            }
            return plan.ToArray();
        }
        /// <summary>The controllers that will still exist once the next stage has started.</summary>
        public static ControllerInstance[] AfterAltars(IEnumerable<ControllerInstance> instances)
        {
            var all = instances.ToArray(); var victims = new HashSet<string>(AltarPlan(all).Select(p => p.Value), StringComparer.Ordinal);
            return all.Where(c => !victims.Contains(c.instanceId)).ToArray();
        }
        public static bool Has(IEnumerable<ControllerInstance> instances, string id) => instances.Any(c => c.alive && c.definitionId == id);
        public static int Capacity(IEnumerable<ControllerInstance> instances, GameConfig cfg, bool debug = false, MimicBinding[] bindings = null, StageRules rules = null)
        {
            rules = rules ?? StageRules.None; int n = cfg.initialSubmitCapacity;
            foreach (var s in ResolveSources(instances, bindings, rules.disabledIds)) if (s.abilityId == "C09" || s.abilityId == "C21" || s.abilityId == "C22") n = checked(n + cfg.Controller(s.abilityId).parameters.capacityDelta);
            return Math.Max(1, Math.Min(debug ? cfg.debug.maxSubmitCapacity : cfg.maxSubmitCapacity, checked(n + rules.capacityDelta)));
        }
        public static int RollBudget(IEnumerable<ControllerInstance> instances, GameConfig cfg, int stageBonus = 0, MimicBinding[] bindings = null, StageRules rules = null)
        {
            // v0.9: C78 adds rolls per source (a mimic of C78 counts as another source).
            // v1.0: boss modifiers change the base budget; item bonuses of the stage are added on top.
            rules = rules ?? StageRules.None; var all = instances.ToArray();
            if (rules.rollsFixed > 0) return checked(rules.rollsFixed + stageBonus);
            int extra = checked(SourceCount(ResolveSources(all, bindings, rules.disabledIds), "C78") * cfg.Controller("C78").parameters.rollsPerHandDelta);
            int enchants = rules.silence ? 0 : all.Count(c => c.alive && c.enchantmentId == "roll1" && !rules.disabledIds.Contains(c.instanceId)) * cfg.enchantRules.rollBonus;
            return checked(Math.Max(1, checked(cfg.totalRollsPerHand + enchants + extra + rules.rollsDelta)) + stageBonus);
        }
        public static int SubmissionBudget(IEnumerable<ControllerInstance> instances, GameConfig cfg, MimicBinding[] bindings = null, StageRules rules = null)
        {
            // v0.9: C79 adds and C101 removes submissions per source; a stage always keeps at least one.
            rules = rules ?? StageRules.None; var all = instances.ToArray(); var sources = ResolveSources(all, bindings, rules.disabledIds);
            int delta = checked(SourceCount(sources, "C79") * cfg.Controller("C79").parameters.submissionsDelta + SourceCount(sources, "C101") * cfg.Controller("C101").parameters.submissionsDelta);
            int enchants = rules.silence ? 0 : all.Count(c => c.alive && c.enchantmentId == "submit1" && !rules.disabledIds.Contains(c.instanceId)) * cfg.enchantRules.submissionBonus;
            return Math.Max(1, checked(cfg.submissionsPerStage + enchants + delta + rules.submissionsDelta));
        }
        /// <summary>v1.0: which controllers a boss modifier disables for the stage, for the order that enters the stage.</summary>
        public static string[] DisabledBy(IEnumerable<ControllerInstance> instances, GameConfig cfg, string modifierId)
        {
            if (string.IsNullOrEmpty(modifierId)) return Array.Empty<string>();
            var m = cfg.Modifier(modifierId); var alive = instances.Where(c => c != null && c.alive).ToArray(); var ids = new List<string>();
            if (m.effects.Contains("DisableLeftmost")) ids.AddRange(alive.Take(m.amount).Select(c => c.instanceId));
            // v1.1.1: only the leftmost `amount` cards of that rarity, by the card's own rarity (a mimic counts as itself).
            if (m.effects.Contains("DisableRarity")) ids.AddRange(alive.Where(c => cfg.Controller(c.definitionId).rarity == m.filter).Take(m.amount).Select(c => c.instanceId));
            return ids.Distinct(StringComparer.Ordinal).ToArray();
        }
        public static int RollsPerHand(IEnumerable<ControllerInstance> instances, GameConfig cfg) => RollBudget(instances, cfg);
        public static int SubmissionsPerStage(IEnumerable<ControllerInstance> instances, GameConfig cfg) => SubmissionBudget(instances, cfg);
        /// <summary>v1.1.1 section 6: the leftmost live cultivation card without a slotless enchantment takes no ordinary
        /// slot, disabled or not. Decided from the current order every time; null when there is no such card.</summary>
        public static string FreeCultivationInstanceId(IEnumerable<ControllerInstance> instances, GameConfig cfg)
            => instances.FirstOrDefault(c => c != null && c.alive && c.enchantmentId != SlotlessEnchantmentId && cfg.Controller(c.definitionId).isCultivation)?.instanceId;
        /// <summary>Ordinary slots in use = live cards - slotless cards - 1 when a cultivation card rides for free.</summary>
        public static int OrdinaryUsed(IEnumerable<ControllerInstance> instances, GameConfig cfg)
        {
            var all = instances.ToArray();
            return all.Count(c => c.alive && c.enchantmentId != SlotlessEnchantmentId) - (FreeCultivationInstanceId(all, cfg) != null ? 1 : 0);
        }
        // TRANSITIONAL-V111: the p1.1.0 count without the free cultivation slot; removed with its last caller.
        public static int OrdinaryUsed(IEnumerable<ControllerInstance> instances) => instances.Count(c => c.alive && c.enchantmentId != "slotless");
        public static long SalePrice(ControllerInstance instance, GameConfig cfg, bool fullPrice = false)
        {
            if (instance == null || !instance.alive) throw new ArgumentException("Cannot sell absent controller.");
            var definition = cfg.Controller(instance.definitionId);
            long fund = instance.state?.fundAccrual ?? 0;
            if (fund < 0 || fund % 3 != 0) throw new ArgumentException("Invalid fund accrual.");
            // v0.8: a controller sells for its list price (base + enchant premium) divided by controllerSellDivisor,
            // rounded down and never below baseControllerSellPrice. Fund accrual still adds on top for C41/C52.
            long listPrice = checked((long)definition.buyPrice + EnchantPrice(string.IsNullOrEmpty(instance.enchantmentId) ? "none" : instance.enchantmentId, cfg));
            long resale = Math.Max(cfg.baseControllerSellPrice, fullPrice ? listPrice : listPrice / cfg.controllerSellDivisor); // v1.0 C140: full list price
            long saleBonus = instance.state?.saleBonus ?? 0;
            if (saleBonus < 0) throw new ArgumentException("Invalid sale bonus.");
            // v1.1.1 section 6: a cultivation card gains 1 per cultivationFacesPerSaleBonus rewritten faces, up to the cap.
            long cultivated = instance.state?.cultivationFacesChanged ?? 0;
            if (cultivated < 0) throw new ArgumentException("Invalid cultivation count.");
            long cultivation = definition.isCultivation ? Math.Min(cfg.cultivationMaxSaleBonus, cultivated / cfg.cultivationFacesPerSaleBonus) : 0;
            return checked(resale + saleBonus + cultivation + ((instance.definitionId == "C41" || instance.definitionId == "C52") ? fund : 0));
        }
        public static int EnchantPrice(string enchantmentId, GameConfig cfg) => cfg.Enchantment(enchantmentId).buyPremium;
        public static bool IsEnchantmentLegal(string controllerId, string enchantmentId, GameConfig cfg)
            => cfg.controllers.Any(c => c.id == controllerId) && cfg.enchantments.Any(e => e.id == enchantmentId) && (!cfg.enchantRules.nonSubmitControllerIds.Contains(controllerId) || (enchantmentId != "base10" && enchantmentId != "mult2"));
        public static void ValidateEquipment(IEnumerable<ControllerInstance> instances, GameConfig cfg, bool debug = false, int ordinarySlots = -1) => ValidateInstances(instances, cfg, debug, true, ordinarySlots);
        internal static void ValidateInstances(IEnumerable<ControllerInstance> instances, GameConfig cfg, bool debug, bool enforceSlots, int ordinarySlots = -1)
        {
            if (instances == null) throw new ArgumentNullException(nameof(instances)); var all = instances.ToArray();
            if (all.Any(c => c == null) || all.Count(c => c.alive) > (debug ? cfg.debug.maxControllerInstances : cfg.maxControllerInstances) || all.Any(c => string.IsNullOrEmpty(c.instanceId)) || all.Select(c => c.instanceId).Distinct(StringComparer.Ordinal).Count() != all.Length) throw new ArgumentException("Invalid controller identities or limits.");
            // v1.1: same-name copies are legal in a normal run; only the slot and instance limits apply.
            foreach (var c in all) cfg.Controller(c.definitionId);
            if (enforceSlots && !debug && OrdinaryUsed(all, cfg) > (ordinarySlots < 0 ? cfg.initialControllerSlots : ordinarySlots)) throw new ArgumentException("Normal equipment limits.");
            foreach (var c in all)
            {
                if (!cfg.enchantments.Any(e => e.id == c.enchantmentId) || (!debug && !IsEnchantmentLegal(c.definitionId, c.enchantmentId, cfg))) throw new ArgumentException("Illegal controller enchantment.");
                if (c.state == null || c.state.fundAccrual < 0 || c.state.fundAccrual % 3 != 0 || c.state.turnoverUnits2 < 3 || c.state.growth < 0 || c.state.saleBonus < 0 || (c.definitionId == "C141" && c.state.growth >= cfg.Controller("C141").parameters.expireGrowth)) throw new ArgumentException("Invalid controller state.");
                // v1.1.1: catch-up belongs to growth cards, the star pip to C137, the rewrite count to cultivation cards.
                if (c.state.growthCatchup < 0 || (c.state.growthCatchup != 0 && !HasGrowth(c.definitionId)) || c.state.starPip < 0 || c.state.starPip > cfg.maxPipValue || (c.state.starPip != 0 && c.definitionId != "C137")
                    || c.state.cultivationFacesChanged < 0 || c.state.cultivationFacesChanged > cfg.cultivationFacesPerSaleBonus * cfg.cultivationMaxSaleBonus || (c.state.cultivationFacesChanged != 0 && !cfg.Controller(c.definitionId).isCultivation)) throw new ArgumentException("Invalid v1.1.1 controller state.");
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
        /// <summary>The non-empty enchantments other than <paramref name="current"/>. v1.1: shop offers of an owned card no
        /// longer use this pool (they draw like any other offer); it remains a pure helper.</summary>
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
    /// <summary>v1.0: how the active boss modifier changes a stage's budgets. None = no modifier.</summary>
    public sealed class StageRules
    {
        public static readonly StageRules None = new StageRules();
        public string[] disabledIds = Array.Empty<string>();
        public bool silence;
        public int rollsDelta, rollsFixed, submissionsDelta, capacityDelta;
        public static StageRules For(GameConfig cfg, string modifierId, IEnumerable<string> disabledIds)
        {
            var rules = new StageRules { disabledIds = (disabledIds ?? Array.Empty<string>()).ToArray() };
            if (string.IsNullOrEmpty(modifierId)) return rules;
            var m = cfg.Modifier(modifierId);
            rules.silence = m.effects.Contains("EnchantSilence");
            if (m.effects.Contains("RollsDelta")) rules.rollsDelta = m.amount;
            if (m.effects.Contains("RollsFixed")) rules.rollsFixed = m.amount;
            if (m.effects.Contains("SubmissionsDelta")) rules.submissionsDelta = m.amount;
            if (m.effects.Contains("CapacityDelta")) rules.capacityDelta = m.amount;
            return rules;
        }
    }
}