using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    public sealed partial class GameSession
    {
        public const string UpgradeTargetRejected = "强化目标无效或已具备相同属性。";
        public const string UnknownDieUpgrade = "未知骰子强化。";
        public const string SelfStudyLimitReached = "本店自选研习机会已使用。";
        public const string NewDieUnavailable = "永久骰数量已满或候选无效。";
        public const string ServiceUnavailable = "此服务已达上限或没有合法目标。";
        // v1.1.1
        public const string SelfDieUpgradeLimitReached = "本店自选改造机会已用完。";
        public const string UpgradeFaceCountRejected = "请选同一颗骰子的 1 或 2 个不同的面。";
        public const string UpgradeSingleFaceOnly = "这种改造一次只能选 1 个面。";
        public const string UpgradeTakesWholeDie = "这种改造作用于整颗骰子，不选面。";
        public const string UpgradeFacesInconsistent = "骰面目标前后不一致。";
        public const string UniqueControllerOwned = "已持有同名的乘法卡，卖掉原卡后才能再取得。";
        /// <summary>Section 9.1: in these first shop cycles the low-price guarantee card carries no enchantment and draws no ticket.</summary>
        private const int GuaranteeWithoutEnchantmentCycles = 2;

        private int ShopCapacity(RunState s) { return s.isDebug && s.debugCapacityOverride > 0 ? s.debugCapacityOverride : ControllerRules.Capacity(s.controllers, cfg, false, ActiveBindings(s), ActiveRules(s)); }
        private int PermanentCount(RunState s) { return s.dice.Count(x => x.lifetime == "permanent"); }
        private int Level(RunState s, string id) { return s.handLevels.Concat(s.colorLevels).First(x => x.handId == id).level; }
        private IEnumerable<string> StudyIds(RunState s) { return cfg.handDefinitions.Select(h => h.id).Concat(cfg.colorDefinitions.Select(h => h.id)).Where(id => IsHandLegal(s, id)); }
        private bool IsHandLegal(RunState s, string id)
        {
            var color = cfg.colorDefinitions.FirstOrDefault(x => x.id == id);
            if (color != null) return Level(s, id) < cfg.maxColorLevel;
            var h = cfg.handDefinitions.FirstOrDefault(x => x.id == id);
            int minimum = h != null && h.id == "straight" && Sources(s).Any(x => x.abilityId == "C29") ? cfg.Controller("C29").parameters.minimumStraightLength : h == null ? int.MaxValue : h.minSubmittedDice;
            return h != null && minimum <= ShopCapacity(s)
                && h.requirePermanentDice <= PermanentCount(s) && Level(s, id) < h.maxLevel;
        }

        private int HandPrice(RunState s, string id, bool selfStudy)
        {
            var color = cfg.colorDefinitions.FirstOrDefault(x => x.id == id);
            int price = color == null ? cfg.Hand(id).baseUpgradePrice : color.baseUpgradePrice;
            return checked(price + cfg.shop.upgradePricePerExistingLevel * Level(s, id) + (selfStudy ? cfg.shop.selfStudyPremium : 0));
        }

        private int OfferPrice(RunState s, ShopOffer offer)
        {
            Check(offer != null, "商品不存在。");
            if (!string.IsNullOrEmpty(offer.groupId)) return 0; // v1.0: gift-group offers are free
            switch (offer.category)
            {
                case "controller": return Math.Max(1, checked(cfg.Controller(offer.contentId).buyPrice + ControllerRules.EnchantPrice(offer.controller.enchantmentId, cfg) + BargainDelta(s)));
                case "consumable": return Discount(s, cfg.Consumable(offer.contentId).baseBuyPrice);
                case "hand_upgrade": return HandPrice(s, offer.contentId, false);
                case "die_upgrade": return Math.Max(1, checked(cfg.Upgrade(offer.contentId).buyPrice + BargainDelta(s)));
                case "new_die": return Discount(s, offer.basePrice);
                case "service":
                    int basePrice = cfg.Service(offer.contentId).baseBuyPrice;
                    if (offer.contentId == "O02") return Math.Max(1, 3 - s.itemDiscount);
                    if (offer.contentId == "O01") basePrice = checked(basePrice + 2 * (s.consumableSlots - cfg.initialConsumableSlots));
                    if (offer.contentId == "O05") basePrice = checked(basePrice + 2 * s.controllerOfferPurchases);
                    if (offer.contentId == "O06") basePrice = checked(basePrice + 2 * s.consumableOfferPurchases);
                    if (offer.contentId == "O08") basePrice = checked(basePrice + cfg.controllerSlotPriceStep * s.controllerSlotPurchases);
                    if (offer.contentId == "O09") basePrice = checked(basePrice + 2 * s.bossRerolls);
                    if (offer.contentId == "O10") basePrice = checked(basePrice + 4 * s.interestCapPurchases);
                    if (offer.contentId == "O11") basePrice = checked(basePrice + 3 * s.enchantTablePurchases);
                    return Discount(s, basePrice);
                default: throw new RuleViolation("商品类别无效。");
            }
        }

        /// <summary>
        /// v1.1.1 section 9.1: the cycle of a shop is the cycle of the stage that comes next. After stage number j
        /// (1-based) it is min(8, floor(j / 3) + 1): the shop after stage 2 is cycle 1, the shop after stage 3 (and its
        /// boss loot) is cycle 2. It cannot change while the shop is open, so it is derived, not stored.
        /// </summary>
        private int ShopCycle(RunState s) => Math.Min(cfg.enchantRules.shopChanceByCyclePercent.Length, (s.stage.stageIndex + 1) / 3 + 1);
        /// <summary>
        /// v1.1.1 section 5: the head start of a growth card offered in a shop of this cycle: (cycle - 1) steps of the
        /// card's growth kind (additive 0.5, base 6, multiplier 0.1 per cycle, in the unit of `growth`). 0 for any other card.
        /// </summary>
        private long CatchupFor(string definitionId, int cycle)
        {
            var step = cfg.growthCatchupPerCycle;
            int unit = ControllerRules.IsGrowthAdditive(definitionId) ? step.additive : ControllerRules.IsGrowthMultiplier(definitionId) ? step.multiplier : ControllerRules.IsGrowthBase(definitionId) ? step.@base : 0;
            return checked((long)(cycle - 1) * unit);
        }
        /// <summary>v1.1.1 section 9.2: a self-selected die upgrade costs max(1, base + premium + C98 sources); O02 does not apply.</summary>
        private int CustomUpgradePrice(RunState s, string upgradeId) => Math.Max(1, checked(cfg.Upgrade(upgradeId).buyPrice + cfg.shop.selfDieUpgradePremium + BargainDelta(s)));

        /// <summary>v0.9 C98: a negative price change on controllers and die upgrades, per source.</summary>
        private int BargainDelta(RunState s) => checked(Sources(s).Count(x => x.abilityId == "C98") * cfg.Controller("C98").parameters.priceDelta);
        private int Discount(RunState s, int price) { return Math.Max(cfg.minDiscountedItemPrice, price - s.itemDiscount); }

        /// <summary>
        /// v1.0 C140: while any source of it is held, controllers resell at their full list price — except the ones
        /// acquired in the shop that is open right now, which still resell at half, like an ordinary sale.
        /// v1.1 (user decision D on stacked C98 discounts): a controller bought from the shelf of the open shop resells
        /// for at most the price actually paid for it until the player leaves that shop, so no number of C98 copies makes
        /// a buy-then-sell profit. Free loot keeps its ordinary price. Every sale price the session reports goes through here.
        /// </summary>
        private long SalePrice(RunState s, ControllerInstance c)
        {
            bool fresh = c != null && s.phase == "Shop" && s.shop != null && s.shop.acquiredControllerIds.Contains(c.instanceId);
            long price = ControllerRules.SalePrice(c, cfg, !fresh && Sources(s).Any(x => x.abilityId == "C140"));
            var paid = fresh && s.shop.paidControllerPrices != null ? s.shop.paidControllerPrices.FirstOrDefault(p => p != null && p.instanceId == c.instanceId) : null;
            return paid == null ? price : Math.Min(price, paid.paid);
        }

        private ShopOffer MakeOffer(RunState s, string category, string contentId, int basePrice)
        {
            return new ShopOffer { offerId = "OFFER_" + (s.nextOfferSerial++).ToString("D5"), batchId = s.shop == null ? null : BatchId(s.shop, category), category = category, contentId = contentId, basePrice = basePrice };
        }

        private static string BatchId(ShopState shop, string category) => shop.generationId + "/" + category + (category == "controller" ? "/" + shop.controllerGeneration : "");
        private static bool Shelf(ShopOffer o) => string.IsNullOrEmpty(o.groupId);
        private int ControllerOfferCount(RunState s) => cfg.shop.initialControllerOffers + s.controllerOfferPurchases;
        private int ConsumableOfferCount(RunState s) => cfg.shop.initialConsumableOffers + s.consumableOfferPurchases;
        /// <summary>
        /// v1.1: every definition can be offered whether or not a copy is owned. One exclusion keeps a shop free of two
        /// same-name controller offers (sampling without replacement across the shelf and the gift groups).
        /// v1.1.1 section 9.3: the other one drops a unique multiplier card while a copy of it is held (whatever its
        /// price, enchantment or disabled state). It applies to the first shelf, a refresh, an added shelf slot, L1 and L2.
        /// </summary>
        private ControllerDefinition[] ControllerCandidates(RunState s, IEnumerable<string> excluded = null)
        {
            var omit = new HashSet<string>(excluded ?? Array.Empty<string>(), StringComparer.Ordinal);
            return cfg.controllers.Where(x => !omit.Contains(x.id) && !ControllerRules.OwnsUniqueController(s.controllers, x.id, cfg)).OrderBy(x => x.id, StringComparer.Ordinal).ToArray();
        }

        /// <summary>
        /// v1.1.1 section 9.1: the enchantment of a random shelf offer, from one integer ticket of the shop stream.
        /// With W the sum of the non-empty relative weights of the card's pool (18, 10, 4, 9, 9 for a card that can
        /// carry a scoring enchantment; 50, 0, 0, 25, 25 for the others) and P the percent of the shop cycle, the
        /// ticket is NextIndex(100 x W): the first (100 - P) x W tickets are "none", then P x weight tickets for each
        /// non-empty enchantment in the fixed order slotless, base10, mult2, submit1, roll1 (half-open intervals).
        /// L2, O11 and C114 keep <see cref="DrawControllerEnchantment"/> and do not use the cycle chance.
        /// </summary>
        private string DrawShelfEnchantment(RunState s, string id, GameRng rng, int cycle)
        {
            bool special = cfg.enchantRules.nonSubmitControllerIds.Contains(id); var w = cfg.enchantRules.nonSubmitDrawWeights;
            Func<string, int> weight = e => e == "none" ? 0 : !special ? cfg.Enchantment(e).weight : e == "slotless" ? w.slotless : e == "submit1" ? w.submit1 : e == "roll1" ? w.roll1 : 0;
            int total = cfg.enchantRules.rawDrawOrder.Sum(weight), percent = cfg.enchantRules.shopChanceByCyclePercent[cycle - 1];
            Check(total > 0 && percent >= 0 && percent <= 100, "没有可用附魔候选。");
            var trace = DrawRaw(s, rng, "shop", "ControllerEnchantment", id, checked(100 * total));
            int draw = trace.rawTicket - (100 - percent) * total; string picked = "none";
            if (draw >= 0)
            {
                picked = null;
                foreach (string enchant in cfg.enchantRules.rawDrawOrder) { draw -= percent * weight(enchant); if (draw < 0) { picked = enchant; break; } }
                Check(picked != null, "附魔抽样未覆盖票值。");
            }
            trace.rawValue = trace.appliedValue = picked; return picked;
        }

        private string DrawControllerEnchantment(RunState s, string id, string current, GameRng rng, string stream = "shop")
        {
            bool special = cfg.enchantRules.nonSubmitControllerIds.Contains(id);
            Func<string, int> weight = e => !special ? cfg.Enchantment(e).weight : e == "none" ? 100 : e == "slotless" ? 50 : e == "submit1" || e == "roll1" ? 25 : 0;
            var pool = cfg.enchantRules.rawDrawOrder.Where(e => weight(e) > 0 && (current == null || (e != "none" && e != current))).ToArray();
            Check(pool.Length > 0, "没有可用附魔候选。");
            var trace = DrawRaw(s, rng, stream, "ControllerEnchantment", id, pool.Sum(weight));
            int draw = trace.rawTicket;
            foreach (var enchant in pool) { draw -= weight(enchant); if (draw < 0) { trace.rawValue = trace.appliedValue = enchant; return enchant; } }
            throw new RuleViolation("附魔抽样未覆盖票值。");
        }

        /// <summary>
        /// Adds shelf controllers. v1.1.1 sections 5, 9.1 and 9.3: the first card of a whole batch (first shelf or a
        /// refresh) comes from the candidates of base price 4 or less when there is one; in shop cycles 1 and 2 that
        /// guaranteed card has no enchantment and draws no enchantment ticket. An empty low-price pool falls back to
        /// every candidate and is then an ordinary draw. A slot added by O05 never reopens the guarantee. Every other
        /// card draws <see cref="DrawShelfEnchantment"/>. A growth card gets the catch-up of the shop cycle.
        /// </summary>
        private void AppendControllers(RunState s, GameRng rng, int count, bool firstGuarantee)
        {
            var choices = ControllerCandidates(s, s.shop.offers.Where(o => o.category == "controller").Select(o => o.contentId)).ToList(); int cycle = ShopCycle(s);
            for (int i = 0; i < count && choices.Count > 0; i++)
            {
                var pool = firstGuarantee && i == 0 ? choices.Where(x => x.buyPrice <= cfg.shop.firstControllerGuaranteeMaxBasePrice).ToList() : choices;
                bool guaranteed = firstGuarantee && i == 0 && pool.Count > 0;
                if (pool.Count == 0) pool = choices;
                var trace = DrawRaw(s, rng, "shop", "ControllerId", BatchId(s.shop, "controller"), pool.Sum(x => x.offerWeight));
                int draw = trace.rawTicket; var picked = pool[0];
                foreach (var option in pool) { draw -= option.offerWeight; if (draw < 0) { picked = option; break; } }
                trace.rawValue = trace.appliedValue = picked.id;
                // v1.1: an owned definition draws its enchantment exactly as an unowned one does (no replacement pool).
                string enchant = guaranteed && cycle <= GuaranteeWithoutEnchantmentCycles ? "none" : DrawShelfEnchantment(s, picked.id, rng, cycle);
                var offer = MakeOffer(s, "controller", picked.id, picked.buyPrice); int serial = s.nextItemSerial++;
                offer.controller = new ControllerInstance { instanceId = "ITEM_" + serial.ToString("D5"), acquiredSerial = serial, definitionId = picked.id, enchantmentId = enchant };
                offer.controller.state.growthCatchup = CatchupFor(picked.id, cycle);
                s.shop.offers.Add(offer); choices.Remove(picked);
            }
        }

        private void GenerateShop(RunState s)
        {
            Check(s.shop == null || !s.shop.generated, "本商店已经生成。");
            var shop = new ShopState { shopId = s.stage.stageId + "_SHOP", generationId = s.runId + "/" + s.stage.stageId + "/SHOP", generated = true };
            s.shop = shop; var rng = new GameRng(s.shopRngState);
            AppendControllers(s, rng, ControllerOfferCount(s), true);
            AddUniformOffers(s, shop, rng, cfg.consumables.Select(x => x.id), ConsumableOfferCount(s), "consumable", id => cfg.Consumable(id).baseBuyPrice);
            AddUniformOffers(s, shop, rng, StudyIds(s), cfg.shop.upgradeOffers, "hand_upgrade", id => HandPrice(s, id, false));
            AddUniformOffers(s, shop, rng, cfg.diceUpgrades.Where(x => HasUpgradeTarget(s, x.id)).Select(x => x.id), cfg.shop.diceUpgradeOffers, "die_upgrade", id => cfg.Upgrade(id).buyPrice);
            AddUniformOffers(s, shop, rng, cfg.services.Where(x => x.id != "O03" && ServiceLegal(s, x.id)).Select(x => x.id), cfg.shop.serviceOffers, "service", id => cfg.Service(id).baseBuyPrice);
            if (PermanentCount(s) < cfg.maxPermanentDice)
                for (int i = 0; i < cfg.shop.newDiceOffers; i++)
                {
                    var die = GenerateDie(s, rng); var reasons = new List<string>();
                    var offer = MakeOffer(s, "new_die", "O03", QuoteNewDie(die, reasons));
                    offer.die = die; offer.priceReasons = reasons.ToArray(); shop.offers.Add(offer);
                }
            // v1.0 gift groups, generated after everything else: boss loot first, then controller gifts left to right.
            var stageDef = cfg.stages[s.stage.stageIndex];
            if (stageDef.stageKind == "boss" && s.stage.stageIndex / 3 < s.cycles.Count && !string.IsNullOrEmpty(s.cycles[s.stage.stageIndex / 3].lootKind)) AddLoot(s, rng, s.cycles[s.stage.stageIndex / 3].lootKind);
            var gifts = Sources(s).Where(x => x.abilityId == "C111" || x.abilityId == "C113").ToArray();
            foreach (string ability in gifts.Select(x => x.abilityId).Distinct(StringComparer.Ordinal).ToArray())
            {
                var p = cfg.Controller(ability).parameters; int sources = gifts.Count(x => x.abilityId == ability);
                var candidates = ability == "C111" ? cfg.consumables.Select(x => x.id) : cfg.diceUpgrades.Where(x => HasUpgradeTarget(s, x.id)).Select(x => x.id);
                var picked = PickUniform(s, rng, candidates, p.giftChoices, "Gift" + ability); if (picked.Count == 0) continue;
                var group = NewGroup(s, "GIFT_" + ability, ability, Math.Min(picked.Count, checked(p.giftPicks * sources)));
                foreach (string id in picked) AddGroupOffer(s, group, ability == "C111" ? "consumable" : "die_upgrade", id, ability == "C111" ? cfg.Consumable(id).baseBuyPrice : cfg.Upgrade(id).buyPrice);
            }
            s.shopRngState = rng.State;
        }

        // ---- v1.0 gift groups ----
        private ShopGroup NewGroup(RunState s, string groupId, string kind, int picks)
        {
            var group = new ShopGroup { groupId = groupId, kind = kind, picks = picks }; s.shop.groups.Add(group); return group;
        }
        private ShopOffer AddGroupOffer(RunState s, ShopGroup group, string category, string contentId, int basePrice)
        {
            var offer = MakeOffer(s, category, contentId, basePrice); offer.groupId = group.groupId; offer.batchId = s.shop.generationId + "/" + group.groupId;
            s.shop.offers.Add(offer); return offer;
        }
        private List<string> PickUniform(RunState s, GameRng rng, IEnumerable<string> candidates, int count, string kind)
        {
            var ids = candidates.Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList(); var picked = new List<string>();
            for (int i = 0; i < count && ids.Count > 0; i++)
            {
                var trace = DrawRaw(s, rng, "shop", kind, s.shop.generationId, ids.Count); string id = ids[trace.rawTicket]; trace.rawValue = trace.appliedValue = id;
                ids.RemoveAt(trace.rawTicket); picked.Add(id);
            }
            return picked;
        }
        private void AddLoot(RunState s, GameRng rng, string kind)
        {
            var def = cfg.Loot(kind); var group = NewGroup(s, "LOOT", kind, def.picks); const int choices = 3;
            // v1.1: controller loot may repeat an owned definition; it still never repeats a controller on the shelf.
            // v1.1.1 section 9.3: L1 and L2 never offer a unique multiplier card that is held.
            var shelf = new HashSet<string>(s.shop.offers.Where(o => o.category == "controller").Select(o => o.contentId), StringComparer.Ordinal);
            switch (kind)
            {
                case "L1":
                    foreach (string id in PickUniform(s, rng, cfg.controllers.Where(c => c.rarity == "rare" && !shelf.Contains(c.id) && !ControllerRules.OwnsUniqueController(s.controllers, c.id, cfg)).Select(c => c.id), choices, "LootController"))
                        AddLootController(s, group, id, "none");
                    break;
                case "L2":
                    {
                        var pool = cfg.controllers.Where(c => !shelf.Contains(c.id) && !ControllerRules.OwnsUniqueController(s.controllers, c.id, cfg)).OrderBy(c => c.id, StringComparer.Ordinal).ToList();
                        for (int i = 0; i < choices && pool.Count > 0; i++)
                        {
                            var trace = DrawRaw(s, rng, "shop", "LootController", s.shop.generationId, pool.Sum(c => c.offerWeight)); int draw = trace.rawTicket; var picked = pool[0];
                            foreach (var option in pool) { draw -= option.offerWeight; if (draw < 0) { picked = option; break; } }
                            trace.rawValue = trace.appliedValue = picked.id; pool.Remove(picked);
                            AddLootController(s, group, picked.id, DrawControllerEnchantment(s, picked.id, "none", rng));
                        }
                        break;
                    }
                case "L3":
                    foreach (string id in PickUniform(s, rng, StudyIds(s), choices, "LootHand")) AddGroupOffer(s, group, "hand_upgrade", id, HandPrice(s, id, false)).levels = cfg.lootHandLevels;
                    break;
                case "L4":
                    foreach (string id in PickUniform(s, rng, cfg.diceUpgrades.Where(x => HasUpgradeTarget(s, x.id)).Select(x => x.id), choices, "LootUpgrade")) AddGroupOffer(s, group, "die_upgrade", id, cfg.Upgrade(id).buyPrice);
                    break;
                case "L5":
                    if (PermanentCount(s) < cfg.maxPermanentDice && s.dice.Count < cfg.maxActiveDice)
                        for (int template = 0; template < choices; template++)
                        {
                            var die = GenerateSpecialDie(s, rng, template); var reasons = new List<string>();
                            var offer = AddGroupOffer(s, group, "new_die", "O03", QuoteNewDie(die, reasons)); offer.die = die; offer.priceReasons = reasons.ToArray();
                        }
                    break;
                case "L6":
                    foreach (string id in PickUniform(s, rng, cfg.consumables.Select(x => x.id), choices, "LootItem")) AddGroupOffer(s, group, "consumable", id, cfg.Consumable(id).baseBuyPrice);
                    break;
                default: throw new RuleViolation("未知战利品种类。");
            }
            group.picks = Math.Max(1, Math.Min(group.picks, s.shop.offers.Count(o => o.groupId == group.groupId)));
            AddGroupOffer(s, group, "gold", "GOLD", cfg.lootGoldValue);
        }
        private void AddLootController(RunState s, ShopGroup group, string id, string enchant)
        {
            var offer = AddGroupOffer(s, group, "controller", id, cfg.Controller(id).buyPrice); int serial = s.nextItemSerial++;
            offer.controller = new ControllerInstance { instanceId = "ITEM_" + serial.ToString("D5"), acquiredSerial = serial, definitionId = id, enchantmentId = enchant };
            // v1.1.1 section 5 and A19: loot of a growth card carries the catch-up of the shop it lies in.
            offer.controller.state.growthCatchup = CatchupFor(id, ShopCycle(s));
        }
        /// <summary>v1.0 loot dice: 0 = one pip on all six faces, 1 = one colour on all six faces, 2 = two gold faces.
        /// v1.1.1 section 7.2: each of the two gold faces also gains the gilding weight on top of the template weight.</summary>
        private DieInstance GenerateSpecialDie(RunState s, GameRng rng, int template)
        {
            int[] range = cfg.generation.pipRange; var d = DiceFactory.CreateStandard(cfg, 1); d.dieId = ""; d.createdSerial = 0; d.isGlass = false;
            var faces = d.faces.OrderBy(x => x.faceId, StringComparer.Ordinal).ToArray();
            foreach (var f in faces)
            {
                f.permanentPip = range[0] + DrawRaw(s, rng, "shop", "DiePip", f.faceId, range[1] - range[0] + 1).rawTicket;
                f.baseColor = DrawRaw(s, rng, "shop", "DieColor", f.faceId, 100).rawTicket < cfg.generation.redFaceChancePercent ? "red" : "blue";
                f.texture = "none"; f.isGold = false; f.baseWeight = 1;
            }
            if (template == 0) { int pip = range[0] + DrawRaw(s, rng, "shop", "SpecialPip", "L5", range[1] - range[0] + 1).rawTicket; foreach (var f in faces) f.permanentPip = pip; }
            else if (template == 1) { string color = DrawRaw(s, rng, "shop", "SpecialColor", "L5", 2).rawTicket == 0 ? "red" : "blue"; foreach (var f in faces) f.baseColor = color; }
            else
            {
                int first = DrawRaw(s, rng, "shop", "SpecialGold", "L5", 6).rawTicket, second = DrawRaw(s, rng, "shop", "SpecialGold", "L5", 5).rawTicket;
                if (second >= first) second++;
                Gild(faces[first]); Gild(faces[second]);
            }
            return d;
        }
        private ShopGroup GroupOf(RunState s, ShopOffer offer) => string.IsNullOrEmpty(offer.groupId) ? null : s.shop.groups.FirstOrDefault(g => g.groupId == offer.groupId);
        /// <summary>Gives a random enchantment to one random controller that has none. Returns false when there is no candidate.</summary>
        private bool EnchantRandomController(RunState s, GameRng rng, string sourceId, string excludedInstanceId)
        {
            var candidates = s.controllers.Where(c => c.enchantmentId == "none" && c.instanceId != excludedInstanceId).ToArray(); if (candidates.Length == 0) return false;
            var trace = DrawRaw(s, rng, "effect", "EnchantTarget", sourceId, candidates.Length); var target = candidates[trace.rawTicket]; trace.rawValue = trace.appliedValue = target.instanceId;
            target.enchantmentId = DrawControllerEnchantment(s, target.definitionId, "none", rng, "effect");
            s.lastEffects.Add(new EffectRecord { kind = "ControllerEnchanted", sourceId = sourceId, detail = target.instanceId + ":" + target.enchantmentId });
            return true;
        }
        public ShopGroup[] ShopGroups() { lock (gate) return state == null || state.shop == null ? Array.Empty<ShopGroup>() : state.shop.groups.Select(g => g.Clone()).ToArray(); }

        public int GetRefreshPrice() { lock (gate) { Check(state != null && state.shop != null, "当前没有商店。"); return RefreshPrice(state); } }
        public long GetControllerSalePrice(string instanceId) { lock (gate) { Check(state != null, "没有活动挑战。"); return SalePrice(state, state.controllers.FirstOrDefault(c => c.instanceId == instanceId)); } }

        // ---- v1.1 boss loot first (DEV_PLAN 4.4) ----
        /// <summary>
        /// v1.1: the player has not engaged with the boss loot of this shop yet: the LOOT group is open (cashing out
        /// closes it) and nothing has been picked. While it is pending, the shop only accepts claims from the LOOT group
        /// (the cash-out included), sales and controller reordering. The first pick unlocks the shop; the remaining picks
        /// of a multi-pick group stay claimable in this shop and are forfeited when the player leaves it. The cash-out is
        /// always available before the first pick, so the gate can never lock the shop up.
        /// </summary>
        private bool LootPending(RunState s)
        {
            if (s == null || s.phase != "Shop" || s.shop == null || s.shop.groups == null) return false;
            var group = s.shop.groups.FirstOrDefault(g => g.groupId == "LOOT");
            return group != null && !group.closed && group.picked == 0;
        }
        private void RequireLootResolved(RunState s) { Check(!LootPending(s), "先领取首领战利品。"); }
        /// <summary>v1.1: true while the boss loot must be claimed or cashed out before anything else in the shop.</summary>
        public bool IsLootPending { get { lock (gate) return LootPending(state); } }
        /// <summary>The same query for a detached view of the state.</summary>
        public bool IsLootPendingIn(RunState view) { lock (gate) return LootPending(view); }

        private int RefreshPrice(RunState s) => s.freeFirstRefreshOwned && s.shop.controllerRefreshCount == 0 ? 0 : checked(cfg.shop.firstRefreshPrice + cfg.shop.refreshPriceStep * s.shop.controllerRefreshCount);
        private void RefreshControllers(RunState s)
        {
            RequirePhase(s, "Shop"); RequireLootResolved(s); int price = RefreshPrice(s);
            Check(s.gold >= price, "金币不足。"); Check(ControllerCandidates(s).Length > 0, "没有合法的卡牌候选。");
            s.gold = checked(s.gold - price); s.shop.controllerRefreshCount = checked(s.shop.controllerRefreshCount + 1); s.shop.controllerGeneration = checked(s.shop.controllerGeneration + 1);
            s.shop.offers.RemoveAll(o => o.category == "controller" && Shelf(o)); var rng = new GameRng(s.shopRngState);
            AppendControllers(s, rng, ControllerOfferCount(s), true); s.shopRngState = rng.State;
            s.lastEffects.Add(new EffectRecord { kind = "ControllersRefreshed", oldValue = price, newValue = s.shop.controllerRefreshCount, detail = BatchId(s.shop, "controller") });
        }

        private void AddUniformOffers(RunState s, ShopState shop, GameRng rng, IEnumerable<string> candidates, int count, string category, Func<string, int> price)
        {
            var ids = candidates.Except(shop.offers.Where(o => o.category == category && Shelf(o)).Select(o => o.contentId), StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            for (int i = 0; i < count && ids.Count > 0; i++)
            {
                var trace = DrawRaw(s, rng, "shop", "OfferId", category, ids.Count); int index = trace.rawTicket; string id = ids[index]; trace.rawValue = trace.appliedValue = id; ids.RemoveAt(index);
                shop.offers.Add(MakeOffer(s, category, id, price(id)));
            }
        }

        private DieInstance GenerateDie(RunState s, GameRng rng, string stream = "shop")
        {
            var weights = cfg.generation.newDieTemplateWeights;
            int selection = DrawRaw(s, rng, stream, "DieTemplate", "O03", weights.random + weights.low + weights.high).rawTicket;
            int[] range = selection < weights.random ? cfg.generation.pipRange
                : selection < weights.random + weights.low ? cfg.generation.lowRange : cfg.generation.highRange;
            var d = DiceFactory.CreateStandard(cfg, 1); d.dieId = ""; d.createdSerial = 0;
            foreach (var f in d.faces.OrderBy(x => x.faceId, StringComparer.Ordinal))
            {
                f.permanentPip = range[0] + DrawRaw(s, rng, stream, "DiePip", f.faceId, range[1] - range[0] + 1).rawTicket;
                f.baseColor = DrawRaw(s, rng, stream, "DieColor", f.faceId, 100).rawTicket < cfg.generation.redFaceChancePercent ? "red" : "blue";
                f.texture = "none"; f.isGold = false; f.baseWeight = 1;
            }
            d.isGlass = false; return d;
        }

        public int QuoteNewDie(DieInstance die, List<string> reasons = null)
        {
            int price = cfg.generation.newDieBasePrice;
            if (reasons != null) reasons.Add("基础 " + price);
            int sum = die.faces.Sum(f => f.permanentPip);
            if (sum >= cfg.generation.highSumThreshold) { price += cfg.generation.highSumPremium; if (reasons != null) reasons.Add("高点数 +" + cfg.generation.highSumPremium); }
            if (sum <= cfg.generation.smallSumThreshold) { price += cfg.generation.smallSumPremium; if (reasons != null) reasons.Add("小点数 +" + cfg.generation.smallSumPremium); }
            if (die.faces.GroupBy(f => f.permanentPip).Max(g => g.Count()) >= cfg.generation.maxRepeatThreshold)
            { price += cfg.generation.maxRepeatPremium; if (reasons != null) reasons.Add("重复点数 +" + cfg.generation.maxRepeatPremium); }
            if (die.faces.GroupBy(f => f.baseColor).Max(g => g.Count()) >= cfg.generation.sameColorThreshold)
            { price += cfg.generation.sameColorPremium; if (reasons != null) reasons.Add("颜色集中 +" + cfg.generation.sameColorPremium); }
            return Math.Min(cfg.generation.maxNewDieBasePrice, price);
        }

        private bool ServiceLegal(RunState s, string id)
        {
            switch (id)
            {
                case "O01": return s.consumableSlots < cfg.maxConsumableSlots;
                case "O02": return s.itemDiscount < cfg.maxItemDiscount;
                case "O03": return PermanentCount(s) < cfg.maxPermanentDice && s.dice.Count < cfg.maxActiveDice;
                case "O04": return PermanentCount(s) > 1;
                case "O05": return s.controllerOfferPurchases < cfg.shop.maxOfferExpansionPurchases && ControllerOfferCount(s) < cfg.shop.maxControllerOffers;
                case "O06": return s.consumableOfferPurchases < cfg.shop.maxOfferExpansionPurchases && ConsumableOfferCount(s) < cfg.shop.maxConsumableOffers;
                case "O07": return !s.freeFirstRefreshOwned;
                case "O08": return s.controllerSlots < cfg.maxOrdinaryControllerSlots;
                // v1.0: O09 rerolls the boss of the cycle that contains the next stage; O10 raises the interest cap; O11 enchants.
                case "O09": { int next = s.stage.stageIndex + 1; return next < cfg.stages.Length && ModifierPool(next / 3 + 1, s.cycles.Select(c => c.modifierId)).Length > 0; }
                case "O10": return s.interestCapPurchases < cfg.maxInterestPrincipalPurchases;
                case "O11": return s.controllers.Any(c => c.enchantmentId == "none");
                default: return false;
            }
        }

        private bool UpgradeTargetLegal(DieInstance die, FaceState face, string id)
        {
            if (die == null || die.lifetime != "permanent") return false;
            if (id == "U05") return !die.isGlass;
            if (face == null) return false;
            switch (id)
            {
                case "U01": return face.texture != "smooth";
                case "U02": return face.texture != "rough";
                case "U03": return face.baseColor != "red";
                case "U04": return face.baseColor != "blue";
                case "U06": return !face.isGold;
                case "U07": return true;
                // v1.0
                case "U08": return face.permanentPip < cfg.maxPipValue;
                case "U09": return face.permanentPip > cfg.minPipValue;
                case "U10": return face.baseWeight < cfg.maxFaceWeight;
                case "U11": { var opposite = die.faces.First(f => f.faceId == cfg.Opposite(face.faceId)); return opposite.permanentPip != face.permanentPip || opposite.baseColor != face.baseColor; }
                default: return false;
            }
        }

        private bool HasUpgradeTarget(RunState s, string id)
        {
            return s.dice.Any(d => d.lifetime == "permanent" && (id == "U05" ? !d.isGlass : d.faces.Any(f => UpgradeTargetLegal(d, f, id))));
        }

        public bool IsUpgradeTargetLegal(string contentId, string dieId, string faceId)
        {
            lock (gate)
            {
                if (state == null) return false;
                var d = state.dice.FirstOrDefault(x => x.dieId == dieId);
                return UpgradeTargetLegal(d, d == null ? null : d.faces.FirstOrDefault(f => f.faceId == faceId), contentId);
            }
        }

        public string PurchaseError(string offerId, string dieId = null, string faceId = null)
        {
            lock (gate)
            {
                try { Check(state != null, "没有活动挑战。"); ValidatePurchase(state, offerId, dieId, FaceIds(faceId, null)); return null; }
                catch (RuleViolation e) { return e.Message; }
            }
        }
        /// <summary>v1.1.1: the same check for a die upgrade offer applied to one or two faces of a die (none for U05).</summary>
        public string PurchaseErrorForFaces(string offerId, string dieId, string[] faceIds)
        {
            lock (gate)
            {
                try { Check(state != null, "没有活动挑战。"); ValidatePurchase(state, offerId, dieId, FaceIds(null, faceIds)); return null; }
                catch (RuleViolation e) { return e.Message; }
            }
        }

        /// <summary>
        /// v1.1.1 section 12.1: the faces of a die upgrade command. The old single targetFaceId means a one-element
        /// array; giving both with different content is rejected.
        /// </summary>
        private static string[] FaceIds(string single, string[] many)
        {
            var list = many ?? Array.Empty<string>();
            Check(list.All(f => !string.IsNullOrEmpty(f)), UpgradeTargetRejected);
            if (string.IsNullOrEmpty(single)) return list.ToArray();
            Check(list.Length == 0 || (list.Length == 1 && list[0] == single), UpgradeFacesInconsistent);
            return new[] { single };
        }

        /// <summary>
        /// v1.1.1 section 7.1: the target of one die upgrade, shared by the shelf, the self-selected upgrade, gifts and
        /// loot. A per-face upgrade takes 1 to maxSelectableFaces different faces of one permanent die, each legal in
        /// the state before anything is applied; U05 takes a die and no face; U11 takes one source face. The whole
        /// request is checked before anything is applied.
        /// </summary>
        private void CheckUpgradeTargets(RunState s, string upgradeId, string dieId, string[] faceIds)
        {
            var definition = cfg.diceUpgrades.FirstOrDefault(u => u.id == upgradeId); Check(definition != null, UnknownDieUpgrade);
            var die = s.dice.FirstOrDefault(x => x.dieId == dieId);
            if (definition.maxSelectableFaces <= 0) { Check(faceIds.Length == 0, UpgradeTakesWholeDie); Check(UpgradeTargetLegal(die, null, upgradeId), UpgradeTargetRejected); return; }
            Check(faceIds.Length >= 1 && faceIds.Length <= definition.maxSelectableFaces, definition.maxSelectableFaces == 1 ? UpgradeSingleFaceOnly : UpgradeFaceCountRejected);
            Check(faceIds.Distinct(StringComparer.Ordinal).Count() == faceIds.Length, UpgradeFaceCountRejected);
            foreach (string faceId in faceIds) Check(UpgradeTargetLegal(die, die == null ? null : die.faces.FirstOrDefault(f => f.faceId == faceId), upgradeId), UpgradeTargetRejected);
        }

        private ShopOffer ValidatePurchase(RunState s, string offerId, string dieId, string[] faceIds)
        {
            RequirePhase(s, "Shop");
            var offer = s.shop.offers.FirstOrDefault(x => x.offerId == offerId);
            Check(offer != null && !offer.sold, "商品不存在或已售罄。");
            var group = GroupOf(s, offer);
            if (offer.groupId != "LOOT") RequireLootResolved(s); // v1.1: boss loot first
            if (!string.IsNullOrEmpty(offer.groupId))
            {
                Check(group != null && !group.closed && group.picked < group.picks, "这一组已经领完。");
                Check(offer.category != "gold" || group.picked == 0, "已经领取过战利品，不能再折现。");
            }
            Check(s.gold >= OfferPrice(s, offer), "金币不足。");
            switch (offer.category)
            {
                case "controller":
                    // v1.1: a same-name copy is simply one more card; nothing is replaced or traded in, so the limits are
                    // checked on the current equipment plus the new card.
                    Check(offer.controller != null && offer.controller.definitionId == offer.contentId, "卡牌报价实例无效。");
                    var equipment = s.controllers.Concat(new[] { offer.controller }).ToArray();
                    Check(equipment.Length <= cfg.maxControllerInstances && ControllerRules.OrdinaryUsed(equipment, cfg) <= s.controllerSlots, "普通卡牌栏位或总张数已满，可先出售卡牌。");
                    Check(ControllerRules.IsEnchantmentLegal(offer.contentId, offer.controller.enchantmentId, cfg), "卡牌附魔不合法。");
                    // v1.1.1 section 9.3: a live offer of a unique multiplier card is checked again against what is held
                    // now. The offer stays; it can be taken once the held copy is sold.
                    Check(!ControllerRules.OwnsUniqueController(s.controllers, offer.contentId, cfg), UniqueControllerOwned);
                    break;
                case "consumable": Check(s.inventory.Count < s.consumableSlots, "道具栏位已满，可先出售道具。"); break;
                case "hand_upgrade": Check(IsHandLegal(s, offer.contentId), "牌型已满级，或当前容量/永久骰数量不足。"); break;
                case "die_upgrade": CheckUpgradeTargets(s, offer.contentId, dieId, faceIds); break;
                case "new_die": Check(ServiceLegal(s, "O03") && offer.die != null, NewDieUnavailable); break;
                case "service":
                    Check(ServiceLegal(s, offer.contentId), ServiceUnavailable);
                    if (offer.contentId == "O04") Check(s.dice.Any(x => x.dieId == dieId && x.lifetime == "permanent"), "请选择永久骰子。");
                    break;
                case "gold": Check(group != null && offer.contentId == "GOLD", "折现报价无效。"); break;
                default: throw new RuleViolation("未知商品类型。");
            }
            return offer;
        }

        private void Buy(RunState s, SessionCommand command)
        {
            // Only a die upgrade reads the face array; any other purchase keeps its old target fields untouched.
            var targetOffer = s.shop == null ? null : s.shop.offers.FirstOrDefault(x => x.offerId == command.offerId);
            var faces = targetOffer != null && targetOffer.category == "die_upgrade" ? FaceIds(command.targetFaceId, command.targetFaceIds) : Array.Empty<string>();
            var offer = ValidatePurchase(s, command.offerId, command.targetDieId, faces);
            long price = OfferPrice(s, offer); // Freeze before a discount purchase changes it.
            s.gold = checked(s.gold - price);
            switch (offer.category)
            {
                case "controller":
                    // v1.1: the new card is appended as its own instance; an owned same-name card stays where it is.
                    // v1.1.1 section 5: the card keeps the catch-up fixed on its offer and starts with no grown part.
                    var acquired = offer.controller.Clone(); acquired.state = new ControllerDynamicState { growthCatchup = offer.controller.state == null ? 0 : offer.controller.state.growthCatchup }; acquired.alive = true;
                    if (acquired.definitionId == "C45")
                    {
                        var effect = new GameRng(s.effectRngState); var trace = DrawRaw(s, effect, "effect", "C45Acquire", acquired.instanceId, 2);
                        acquired.state.activeColor = trace.rawValue = trace.appliedValue = trace.rawTicket == 0 ? "red" : "blue";
                        s.effectRngState = effect.State;
                    }
                    s.controllers.Add(acquired); s.shop.acquiredControllerIds.Add(acquired.instanceId);
                    // v1.1: remember what was paid; a free loot claim is not a purchase and keeps its ordinary resale.
                    if (Shelf(offer)) s.shop.paidControllerPrices.Add(new ShopPaidPrice { instanceId = acquired.instanceId, paid = price });
                    break;
                case "consumable": s.inventory.Add(NewOwnedItem(s, offer.contentId)); break;
                case "hand_upgrade":
                    {
                        // v1.0: a free loot upgrade can grant several levels at once, never past the cap.
                        var entry = s.handLevels.Concat(s.colorLevels).First(x => x.handId == offer.contentId); bool isColor = cfg.colorDefinitions.Any(c => c.id == offer.contentId);
                        int applied = Math.Min(Math.Max(1, offer.levels), (isColor ? cfg.maxColorLevel : cfg.Hand(offer.contentId).maxLevel) - entry.level);
                        entry.level = checked(entry.level + applied); Grow(s, "C61", checked((long)cfg.Controller("C61").parameters.growthEvent * applied)); break;
                    }
                case "gold":
                    s.gold = checked(s.gold + offer.basePrice);
                    s.lastEffects.Add(new EffectRecord { kind = "LootGold", sourceId = GroupOf(s, offer)?.kind, newValue = offer.basePrice }); break;
                case "new_die":
                    var d = offer.die.Clone(); d.createdSerial = s.nextDieSerial++; d.dieId = "D" + d.createdSerial.ToString("D3");
                    d.lifetime = "permanent"; d.createdStageId = ""; s.dice.Add(d);
                    s.lastEffects.Add(new EffectRecord { kind = "DieAdded", sourceId = "O03", dieId = d.dieId }); break;
                case "service":
                    if (offer.contentId == "O01") s.consumableSlots++;
                    else if (offer.contentId == "O02") s.itemDiscount++;
                    else if (offer.contentId == "O05" || offer.contentId == "O06")
                    {
                        var rng = new GameRng(s.shopRngState);
                        if (offer.contentId == "O05") { s.controllerOfferPurchases++; AppendControllers(s, rng, 1, false); }
                        else { s.consumableOfferPurchases++; AddUniformOffers(s, s.shop, rng, cfg.consumables.Select(x => x.id), 1, "consumable", id => cfg.Consumable(id).baseBuyPrice); }
                        s.shopRngState = rng.State;
                    }
                    else if (offer.contentId == "O07") s.freeFirstRefreshOwned = true;
                    else if (offer.contentId == "O08") { s.controllerSlots++; s.controllerSlotPurchases++; s.lastEffects.Add(new EffectRecord { kind = "ControllerSlotAdded", sourceId = "O08", oldValue = s.controllerSlots - 1, newValue = s.controllerSlots }); }
                    else if (offer.contentId == "O04") { s.dice.RemoveAll(x => x.dieId == command.targetDieId); s.lastEffects.Add(new EffectRecord { kind = "DieRemoved", sourceId = "O04", dieId = command.targetDieId }); }
                    else if (offer.contentId == "O09")
                    {
                        int cycle = (s.stage.stageIndex + 1) / 3; var stageRng = new GameRng(s.stageRngState); string before = s.cycles[cycle].modifierId;
                        s.cycles[cycle].modifierId = DrawModifier(s, stageRng, cycle + 1, s.cycles.Select(c => c.modifierId)); s.stageRngState = stageRng.State; s.bossRerolls++;
                        s.lastEffects.Add(new EffectRecord { kind = "BossRerolled", sourceId = "O09", detail = before + ":" + s.cycles[cycle].modifierId, newValue = cycle + 1 });
                    }
                    else if (offer.contentId == "O10")
                    {
                        s.interestCapPurchases++;
                        long maxInterest = InterestOnPrincipal(InterestPrincipalCap(s), Sources(s).Count(a => a.abilityId == "C01"));
                        // Earlier saves record only the base plus purchased principal in newValue.
                        // Capture the effective maximum here while the purchased run's equipment is still known.
                        s.lastEffects.Add(new EffectRecord { kind = "InterestCapRaised", sourceId = "O10",
                            newValue = checked(cfg.maxInterestPrincipal + (long)cfg.interestPrincipalStep * s.interestCapPurchases),
                            detail = maxInterest.ToString(System.Globalization.CultureInfo.InvariantCulture) });
                    }
                    else if (offer.contentId == "O11")
                    {
                        var effect = new GameRng(s.effectRngState); Check(EnchantRandomController(s, effect, "O11", null), "没有可以附魔的卡牌。");
                        s.effectRngState = effect.State; s.enchantTablePurchases++;
                    }
                    else throw new RuleViolation("服务不能以此方式购买。");
                    break;
                // v1.1.1 sections 5 and 7.1: one successful transaction grows C60 once, whether it changed one face, two
                // faces, a mirrored face or a whole die, and whether it was paid, a gift or loot.
                case "die_upgrade": ApplyUpgrades(s, offer.contentId, command.targetDieId, faces); Grow(s, "C60", cfg.Controller("C60").parameters.growthEvent); break;
            }
            offer.sold = true;
            var taken = GroupOf(s, offer);
            if (taken != null) { taken.picked++; if (offer.category == "gold" || taken.picked >= taken.picks) taken.closed = true; }
            s.lastEffects.Add(new EffectRecord { kind = "Purchased", sourceId = offer.contentId, detail = offer.offerId, oldValue = price, newValue = s.gold });
        }

        /// <summary>v1.1.1 section 8: every way of getting an item (purchase, gift, loot, C110) creates it with the configured uses: 3 for a key ring, 1 otherwise.</summary>
        private OwnedItem NewOwnedItem(RunState s, string id) { return new OwnedItem { instanceId = "ITEM_" + (s.nextItemSerial++).ToString("D5"), contentId = id, usesRemaining = Math.Max(1, cfg.Consumable(id).initialUses) }; }

        /// <summary>
        /// v1.1.1 section 7.1: applies one upgrade to its already validated targets, face by face in ascending face id.
        /// A random transform draws once per face in that order from the effect stream.
        /// </summary>
        private void ApplyUpgrades(RunState s, string id, string dieId, string[] faceIds)
        {
            if (faceIds.Length == 0) { ApplyUpgrade(s, id, dieId, null); return; }
            foreach (string faceId in faceIds.OrderBy(x => x, StringComparer.Ordinal)) ApplyUpgrade(s, id, dieId, faceId);
        }

        /// <summary>
        /// v1.1.1 section 9.2: the self-selected die upgrade. Any of U01-U11 with a legal target, at most
        /// selfDieUpgradeLimit successful ones per shop, for the shelf price plus the premium. It draws nothing from the
        /// shop stream; a random transform still uses the effect stream. A rejection changes nothing.
        /// </summary>
        private void CustomDieUpgrade(RunState s, SessionCommand command)
        {
            var faces = FaceIds(command.targetFaceId, command.targetFaceIds);
            ValidateCustomUpgrade(s, command.upgradeId, command.targetDieId, faces);
            int price = CustomUpgradePrice(s, command.upgradeId);
            s.gold = checked(s.gold - price); ApplyUpgrades(s, command.upgradeId, command.targetDieId, faces);
            s.shop.selfDieUpgradeUsed = checked(s.shop.selfDieUpgradeUsed + 1); Grow(s, "C60", cfg.Controller("C60").parameters.growthEvent);
            s.lastEffects.Add(new EffectRecord { kind = "CustomDieUpgraded", sourceId = command.upgradeId, dieId = command.targetDieId, detail = string.Join(",", faces.OrderBy(x => x, StringComparer.Ordinal)), oldValue = price, newValue = s.shop.selfDieUpgradeUsed });
        }
        /// <summary>Everything about a self-selected upgrade that does not depend on the target.</summary>
        private void ValidateCustomUpgradeOffer(RunState s, string upgradeId)
        {
            RequirePhase(s, "Shop"); RequireLootResolved(s);
            Check(s.shop != null && cfg.diceUpgrades.Any(u => u.id == upgradeId), UnknownDieUpgrade);
            Check(s.shop.selfDieUpgradeUsed < cfg.shop.selfDieUpgradeLimit, SelfDieUpgradeLimitReached);
            Check(s.gold >= CustomUpgradePrice(s, upgradeId), "金币不足。");
        }
        private void ValidateCustomUpgrade(RunState s, string upgradeId, string dieId, string[] faceIds)
        {
            ValidateCustomUpgradeOffer(s, upgradeId); CheckUpgradeTargets(s, upgradeId, dieId, faceIds);
        }

        // ---- v1.1.1 quotes for the interface and the simulator (sections 3.1, 7.1, 9.2, 9.3) ----
        /// <summary>Self-selected die upgrades still allowed in the open shop (0 outside a shop).</summary>
        public int CustomDieUpgradesRemaining { get { lock (gate) return CustomRemaining(state); } }
        public int CustomDieUpgradesRemainingIn(RunState view) { lock (gate) return CustomRemaining(view); }
        private int CustomRemaining(RunState s) => s == null || s.phase != "Shop" || s.shop == null ? 0 : Math.Max(0, cfg.shop.selfDieUpgradeLimit - s.shop.selfDieUpgradeUsed);
        /// <summary>The price of a self-selected die upgrade right now (premium and C98 included).</summary>
        public int GetCustomDieUpgradePrice(string upgradeId) { lock (gate) { Check(state != null && cfg.diceUpgrades.Any(u => u.id == upgradeId), UnknownDieUpgrade); return CustomUpgradePrice(state, upgradeId); } }
        /// <summary>
        /// The self-selected list of the open shop: every die upgrade in id order with its live price, how many faces it
        /// takes, whether any die has a legal target, and why it cannot be bought at all right now (null when it can).
        /// Empty outside a shop.
        /// </summary>
        public DieUpgradeOption[] CustomDieUpgradeOptions()
        {
            lock (gate)
            {
                if (state == null || state.phase != "Shop" || state.shop == null) return Array.Empty<DieUpgradeOption>();
                return cfg.diceUpgrades.OrderBy(u => u.id, StringComparer.Ordinal).Select(u =>
                {
                    var option = new DieUpgradeOption { upgradeId = u.id, price = CustomUpgradePrice(state, u.id), maxSelectableFaces = u.maxSelectableFaces, hasTarget = HasUpgradeTarget(state, u.id) };
                    try { ValidateCustomUpgradeOffer(state, u.id); Check(option.hasTarget, UpgradeTargetRejected); } catch (RuleViolation e) { option.error = e.Message; }
                    return option;
                }).ToArray();
            }
        }
        /// <summary>Why this self-selected upgrade on these targets would be rejected; null when it would be accepted.</summary>
        public string CustomDieUpgradeError(string upgradeId, string dieId, string[] faceIds)
        {
            lock (gate)
            {
                try { Check(state != null, "没有活动挑战。"); ValidateCustomUpgrade(state, upgradeId, dieId, FaceIds(null, faceIds)); return null; }
                catch (RuleViolation e) { return e.Message; }
            }
        }
        /// <summary>How many faces of one die an upgrade takes: 0 = the whole die (U05), 1 (U11) or 2.</summary>
        public int MaxSelectableFaces(string upgradeId) { lock (gate) { var u = cfg.diceUpgrades.FirstOrDefault(x => x.id == upgradeId); Check(u != null, UnknownDieUpgrade); return u.maxSelectableFaces; } }
        /// <summary>
        /// The faces of this die an upgrade may be applied to, in face id order (shelf, self-selected, gift and loot
        /// share the rule). Empty for a die that is not permanent, for an unknown die and for U05, which takes no face:
        /// ask <see cref="IsUpgradeTargetLegal"/> with a null face for it.
        /// </summary>
        public string[] LegalUpgradeFaces(string upgradeId, string dieId)
        {
            lock (gate)
            {
                var d = state == null ? null : state.dice.FirstOrDefault(x => x.dieId == dieId); var u = cfg.diceUpgrades.FirstOrDefault(x => x.id == upgradeId);
                if (d == null || u == null || u.maxSelectableFaces <= 0) return Array.Empty<string>();
                return d.faces.Where(f => UpgradeTargetLegal(d, f, upgradeId)).Select(f => f.faceId).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            }
        }
        /// <summary>Section 9.3: this card is one of the unique multiplier cards and a copy is held, so shops and loot filter it out and a live offer of it is refused.</summary>
        public bool IsUniqueControllerHeld(string definitionId) { lock (gate) return state != null && ControllerRules.OwnsUniqueController(state.controllers, definitionId, cfg); }
        public bool IsUniqueControllerHeldIn(RunState view, string definitionId) { lock (gate) return view != null && ControllerRules.OwnsUniqueController(view.controllers, definitionId, cfg); }
        /// <summary>Section 3.1: what one level of this hand or colour costs now: base price + current level x step, plus the premium for a self study.</summary>
        public int GetHandUpgradePrice(string handId, bool selfStudy) { lock (gate) { Check(state != null && state.handLevels.Concat(state.colorLevels).Any(x => x.handId == handId), "牌型无效。"); return HandPrice(state, handId, selfStudy); } }
        /// <summary>The cycle of the shop that is open or would open after the current stage (1-8).</summary>
        public int ShopCycleIn(RunState view) { lock (gate) { Check(view != null && view.stage != null, "没有活动挑战。"); return ShopCycle(view); } }

        private void ApplyUpgrade(RunState s, string id, string dieId, string faceId)
        {
            var d = FindDie(s, dieId); var f = d.faces.FirstOrDefault(x => x.faceId == faceId);
            switch (id)
            {
                case "U01": f.texture = "smooth"; break;
                case "U02": f.texture = "rough"; break;
                case "U03": f.baseColor = "red"; break;
                case "U04": f.baseColor = "blue"; break;
                case "U05": d.isGlass = true; break;
                // v1.1.1 section 7.2: gilding goes through the one shared entry (weight +1 up to the cap).
                // It counts the die revision itself, so the common increment below is undone for it.
                case "U06": if (GildFace(s, d, f, "U06")) d.dataRevision--; break;
                case "U07":
                    int old = f.permanentPip; var candidates = Enumerable.Range(cfg.minPipValue, cfg.maxPipValue - cfg.minPipValue + 1).Where(x => x != old).ToArray();
                    var rng = new GameRng(s.effectRngState); f.permanentPip = candidates[DrawRaw(s, rng, "effect", "FaceTransform", dieId + "/" + faceId, candidates.Length).rawTicket]; s.effectRngState = rng.State;
                    s.lastEffects.Add(new EffectRecord { kind = "FaceTransformed", sourceId = id, dieId = dieId, faceId = faceId, oldValue = old, newValue = f.permanentPip });
                    break;
                // v1.0
                case "U08": case "U09":
                    {
                        int before = f.permanentPip; f.permanentPip = before + (id == "U08" ? 1 : -1);
                        s.lastEffects.Add(new EffectRecord { kind = "FaceValueChanged", sourceId = id, dieId = dieId, faceId = faceId, oldValue = before, newValue = f.permanentPip }); break;
                    }
                case "U10":
                    {
                        int before = f.baseWeight; f.baseWeight = before + 1;
                        s.lastEffects.Add(new EffectRecord { kind = "FaceChanged", sourceId = id, dieId = dieId, faceId = faceId, detail = "weightDelta", oldValue = before, newValue = f.baseWeight }); break;
                    }
                case "U11":
                    {
                        var opposite = d.faces.First(x => x.faceId == cfg.Opposite(faceId)); int before = opposite.permanentPip;
                        opposite.permanentPip = f.permanentPip; opposite.baseColor = f.baseColor;
                        s.lastEffects.Add(new EffectRecord { kind = "FaceMirrored", sourceId = id, dieId = dieId, faceId = opposite.faceId, detail = f.baseColor, oldValue = before, newValue = opposite.permanentPip }); break;
                    }
                default: throw new RuleViolation(UnknownDieUpgrade);
            }
            d.dataRevision++;
            s.lastEffects.Add(new EffectRecord { kind = "DieUpgraded", sourceId = id, dieId = dieId, faceId = faceId });
        }

        private void Study(RunState s, string handId)
        {
            RequirePhase(s, "Shop"); RequireLootResolved(s);
            Check(s.shop.selfStudyUsed < cfg.shop.selfStudyLimit, SelfStudyLimitReached);
            Check(IsHandLegal(s, handId), "牌型已满级，或当前容量/永久骰数量不足。");
            int price = HandPrice(s, handId, true); Check(s.gold >= price, "金币不足。");
            s.gold = checked(s.gold - price); s.handLevels.Concat(s.colorLevels).First(x => x.handId == handId).level++; s.shop.selfStudyUsed++; Grow(s, "C61", cfg.Controller("C61").parameters.growthEvent);
            s.lastEffects.Add(new EffectRecord { kind = "HandStudied", sourceId = handId, oldValue = price, newValue = Level(s, handId) });
        }

        private void GrowTurnover(RunState s, string[] beforeIds)
        {
            // v0.8: at most maxGrowthPerShop growth events per shop, so buying and reselling cheap cards cannot farm it.
            int cap = cfg.Controller("C51").parameters.maxGrowthPerShop;
            if (cap > 0 && s.shop != null && s.shop.turnoverGrowthCount >= cap) return;
            bool grown = false;
            foreach (var c in s.controllers.Where(c => c.definitionId == "C51" && beforeIds.Contains(c.instanceId)))
            {
                // v1.1.1 section 5 (reviewed by W3b-1 against the C51 row): the first card sale of a shop that has a real
                // C51 to receive it grows every real C51 held both before and after the sale by growthEvent hundredths,
                // kept in `growth` (turnoverUnits2 is no longer read). The sold instance itself never grows; selling an
                // item, a self-destruction or a sale without a receiver does not use up the shop's allowance.
                long before = c.state.growth; c.state.growth = checked(before + cfg.Controller("C51").parameters.growthEvent);
                s.lastEffects.Add(new EffectRecord { kind = "TurnoverGrown", sourceId = "C51", detail = c.instanceId, oldValue = before, newValue = c.state.growth });
                grown = true;
            }
            if (grown && s.shop != null) s.shop.turnoverGrowthCount = checked(s.shop.turnoverGrowthCount + 1);
        }

        private void Sell(RunState s, string instanceId)
        {
            RequirePhase(s, "Shop");
            var c = s.controllers.FirstOrDefault(x => x.instanceId == instanceId);
            var item = s.inventory.FirstOrDefault(x => x.instanceId == instanceId);
            Check(c != null || item != null, "该物品不存在、已售出或已消耗。");
            long sellPrice;
            string contentId;
            if (c != null) {
                var oldTurnover = s.controllers.Where(x => x.definitionId == "C51").Select(x => x.instanceId).ToArray();
                sellPrice = SalePrice(s, c); contentId = c.contentId; s.controllers.Remove(c); s.shop.acquiredControllerIds.Remove(c.instanceId);
                s.shop.paidControllerPrices.RemoveAll(p => p.instanceId == c.instanceId); GrowTurnover(s, oldTurnover);
            }
            else { s.inventory.Remove(item); sellPrice = cfg.consumableSellPrice; contentId = item.contentId; }
            s.gold = checked(s.gold + sellPrice);
            s.lastEffects.Add(new EffectRecord { kind = "Sold", sourceId = contentId, detail = instanceId, newValue = sellPrice });
        }
    }
}
