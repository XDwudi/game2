using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    public sealed partial class GameSession
    {
        private int ShopCapacity(RunState s) { return s.isDebug && s.debugCapacityOverride > 0 ? s.debugCapacityOverride : ControllerRules.Capacity(s.controllers, cfg, false, ActiveBindings(s)); }
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
            switch (offer.category)
            {
                case "controller": return checked(cfg.Controller(offer.contentId).buyPrice + ControllerRules.EnchantPrice(offer.controller.enchantmentId, cfg));
                case "consumable": return Discount(s, cfg.Consumable(offer.contentId).baseBuyPrice);
                case "hand_upgrade": return HandPrice(s, offer.contentId, false);
                case "die_upgrade": return cfg.Upgrade(offer.contentId).buyPrice;
                case "new_die": return Discount(s, offer.basePrice);
                case "service":
                    int basePrice = cfg.Service(offer.contentId).baseBuyPrice;
                    if (offer.contentId == "O02") return Math.Max(1, 3 - s.itemDiscount);
                    if (offer.contentId == "O01") basePrice = checked(basePrice + 2 * (s.consumableSlots - cfg.initialConsumableSlots));
                    if (offer.contentId == "O05") basePrice = checked(basePrice + 2 * s.controllerOfferPurchases);
                    if (offer.contentId == "O06") basePrice = checked(basePrice + 2 * s.consumableOfferPurchases);
                    return Discount(s, basePrice);
                default: throw new RuleViolation("商品类别无效。");
            }
        }

        private int Discount(RunState s, int price) { return Math.Max(cfg.minDiscountedItemPrice, price - s.itemDiscount); }
        private long OfferTradeIn(RunState s, ShopOffer offer)
        {
            var old = offer != null && offer.category == "controller" ? s.controllers.FirstOrDefault(c => c.definitionId == offer.contentId) : null;
            return old == null ? 0 : ControllerRules.SalePrice(old, cfg);
        }

        private ShopOffer MakeOffer(RunState s, string category, string contentId, int basePrice)
        {
            return new ShopOffer { offerId = "OFFER_" + (s.nextOfferSerial++).ToString("D5"), batchId = s.shop == null ? null : BatchId(s.shop, category), category = category, contentId = contentId, basePrice = basePrice };
        }

        private static string BatchId(ShopState shop, string category) => shop.generationId + "/" + category + (category == "controller" ? "/" + shop.controllerGeneration : "");
        private int ControllerOfferCount(RunState s) => cfg.shop.initialControllerOffers + s.controllerOfferPurchases;
        private int ConsumableOfferCount(RunState s) => cfg.shop.initialConsumableOffers + s.consumableOfferPurchases;
        private ControllerDefinition[] ControllerCandidates(RunState s, IEnumerable<string> excluded = null)
        {
            var omit = new HashSet<string>(excluded ?? Array.Empty<string>(), StringComparer.Ordinal);
            return cfg.controllers.Where(x => !omit.Contains(x.id) && (!Has(s, x.id) || cfg.enchantments.Any(e => e.id != "none"
                && e.id != s.controllers.First(c => c.definitionId == x.id).enchantmentId && ControllerRules.IsEnchantmentLegal(x.id, e.id, cfg))))
                .OrderBy(x => x.id, StringComparer.Ordinal).ToArray();
        }

        private string DrawControllerEnchantment(RunState s, string id, string current, GameRng rng)
        {
            bool special = cfg.enchantRules.nonSubmitControllerIds.Contains(id);
            Func<string, int> weight = e => !special ? cfg.Enchantment(e).weight : e == "none" ? 100 : e == "slotless" ? 50 : e == "submit1" || e == "roll1" ? 25 : 0;
            var pool = cfg.enchantRules.rawDrawOrder.Where(e => weight(e) > 0 && (current == null || (e != "none" && e != current))).ToArray();
            Check(pool.Length > 0, "没有可用附魔候选。");
            var trace = DrawRaw(s, rng, "shop", "ControllerEnchantment", id, pool.Sum(weight));
            int draw = trace.rawTicket;
            foreach (var enchant in pool) { draw -= weight(enchant); if (draw < 0) { trace.rawValue = trace.appliedValue = enchant; return enchant; } }
            throw new RuleViolation("附魔抽样未覆盖票值。");
        }

        private void AppendControllers(RunState s, GameRng rng, int count, bool firstGuarantee)
        {
            var choices = ControllerCandidates(s, s.shop.offers.Where(o => o.category == "controller").Select(o => o.contentId)).ToList();
            for (int i = 0; i < count && choices.Count > 0; i++)
            {
                var pool = firstGuarantee && i == 0 ? choices.Where(x => !Has(s, x.id) && x.buyPrice <= cfg.shop.firstControllerGuaranteeMaxBasePrice).ToList() : choices;
                if (pool.Count == 0) pool = choices;
                var trace = DrawRaw(s, rng, "shop", "ControllerId", BatchId(s.shop, "controller"), pool.Sum(x => x.offerWeight));
                int draw = trace.rawTicket; var picked = pool[0];
                foreach (var option in pool) { draw -= option.offerWeight; if (draw < 0) { picked = option; break; } }
                trace.rawValue = trace.appliedValue = picked.id;
                var owned = s.controllers.FirstOrDefault(c => c.definitionId == picked.id);
                string enchant = DrawControllerEnchantment(s, picked.id, owned == null ? null : owned.enchantmentId, rng);
                var offer = MakeOffer(s, "controller", picked.id, picked.buyPrice); int serial = s.nextItemSerial++;
                offer.controller = new ControllerInstance { instanceId = "ITEM_" + serial.ToString("D5"), acquiredSerial = serial, definitionId = picked.id, enchantmentId = enchant };
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
            s.shopRngState = rng.State;
        }

        public int GetRefreshPrice() { lock (gate) { Check(state != null && state.shop != null, "当前没有商店。"); return RefreshPrice(state); } }
        public long GetControllerSalePrice(string instanceId) { lock (gate) { Check(state != null, "没有活动挑战。"); return ControllerRules.SalePrice(state.controllers.FirstOrDefault(c => c.instanceId == instanceId), cfg); } }
        private int RefreshPrice(RunState s) => s.freeFirstRefreshOwned && s.shop.controllerRefreshCount == 0 ? 0 : checked(cfg.shop.firstRefreshPrice + cfg.shop.refreshPriceStep * s.shop.controllerRefreshCount);
        private void RefreshControllers(RunState s)
        {
            RequirePhase(s, "Shop"); int price = RefreshPrice(s);
            Check(s.gold >= price, "金币不足。"); Check(ControllerCandidates(s).Length > 0, "没有合法控制器候选。");
            s.gold = checked(s.gold - price); s.shop.controllerRefreshCount = checked(s.shop.controllerRefreshCount + 1); s.shop.controllerGeneration = checked(s.shop.controllerGeneration + 1);
            s.shop.offers.RemoveAll(o => o.category == "controller"); var rng = new GameRng(s.shopRngState);
            AppendControllers(s, rng, ControllerOfferCount(s), true); s.shopRngState = rng.State;
            s.lastEffects.Add(new EffectRecord { kind = "ControllersRefreshed", oldValue = price, newValue = s.shop.controllerRefreshCount, detail = BatchId(s.shop, "controller") });
        }

        private void AddUniformOffers(RunState s, ShopState shop, GameRng rng, IEnumerable<string> candidates, int count, string category, Func<string, int> price)
        {
            var ids = candidates.Except(shop.offers.Where(o => o.category == category).Select(o => o.contentId), StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToList();
            for (int i = 0; i < count && ids.Count > 0; i++)
            {
                var trace = DrawRaw(s, rng, "shop", "OfferId", category, ids.Count); int index = trace.rawTicket; string id = ids[index]; trace.rawValue = trace.appliedValue = id; ids.RemoveAt(index);
                shop.offers.Add(MakeOffer(s, category, id, price(id)));
            }
        }

        private DieInstance GenerateDie(RunState s, GameRng rng)
        {
            var weights = cfg.generation.newDieTemplateWeights;
            int selection = DrawRaw(s, rng, "shop", "DieTemplate", "O03", weights.random + weights.low + weights.high).rawTicket;
            int[] range = selection < weights.random ? cfg.generation.pipRange
                : selection < weights.random + weights.low ? cfg.generation.lowRange : cfg.generation.highRange;
            var d = DiceFactory.CreateStandard(cfg, 1); d.dieId = ""; d.createdSerial = 0;
            foreach (var f in d.faces.OrderBy(x => x.faceId, StringComparer.Ordinal))
            {
                f.permanentPip = range[0] + DrawRaw(s, rng, "shop", "DiePip", f.faceId, range[1] - range[0] + 1).rawTicket;
                f.baseColor = DrawRaw(s, rng, "shop", "DieColor", f.faceId, 100).rawTicket < cfg.generation.redFaceChancePercent ? "red" : "blue";
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
                try { Check(state != null, "没有活动挑战。"); ValidatePurchase(state, offerId, dieId, faceId); return null; }
                catch (RuleViolation e) { return e.Message; }
            }
        }

        private ShopOffer ValidatePurchase(RunState s, string offerId, string dieId, string faceId)
        {
            RequirePhase(s, "Shop");
            var offer = s.shop.offers.FirstOrDefault(x => x.offerId == offerId);
            Check(offer != null && !offer.sold, "商品不存在或已售罄。");
            Check(s.gold >= checked(OfferPrice(s, offer) - OfferTradeIn(s, offer)), "金币不足。");
            switch (offer.category)
            {
                case "controller":
                    Check(offer.controller != null && offer.controller.definitionId == offer.contentId, "控制器报价实例无效。");
                    var old = s.controllers.FirstOrDefault(c => c.definitionId == offer.contentId);
                    Check(old == null || (offer.controller.enchantmentId != "none" && offer.controller.enchantmentId != old.enchantmentId), "同名控制器只能替换为不同的非空附魔。");
                    var equipment = s.controllers.Where(c => c != old).Concat(new[] { offer.controller }).ToArray();
                    Check(equipment.Length <= cfg.maxControllerInstances && ControllerRules.OrdinaryUsed(equipment) <= cfg.initialControllerSlots, "普通控制器槽或总件数已满，可先出售控制器。");
                    Check(equipment.Select(c => c.definitionId).Distinct(StringComparer.Ordinal).Count() == equipment.Length, "控制器定义重复。");
                    Check(ControllerRules.IsEnchantmentLegal(offer.contentId, offer.controller.enchantmentId, cfg), "控制器附魔不合法。");
                    break;
                case "consumable": Check(s.inventory.Count < s.consumableSlots, "道具槽位已满，可先出售道具。"); break;
                case "hand_upgrade": Check(IsHandLegal(s, offer.contentId), "牌型已满级，或当前容量/永久骰数量不足。"); break;
                case "die_upgrade":
                    var d = s.dice.FirstOrDefault(x => x.dieId == dieId);
                    var f = d == null ? null : d.faces.FirstOrDefault(x => x.faceId == faceId);
                    Check(UpgradeTargetLegal(d, f, offer.contentId), "强化目标无效或已具备相同属性。");
                    break;
                case "new_die": Check(ServiceLegal(s, "O03") && offer.die != null, "永久骰数量已满或候选无效。"); break;
                case "service":
                    Check(ServiceLegal(s, offer.contentId), "此服务已达上限或没有合法目标。");
                    if (offer.contentId == "O04") Check(s.dice.Any(x => x.dieId == dieId && x.lifetime == "permanent"), "请选择永久骰子。");
                    break;
                default: throw new RuleViolation("未知商品类型。");
            }
            return offer;
        }

        private void Buy(RunState s, SessionCommand command)
        {
            var offer = ValidatePurchase(s, command.offerId, command.targetDieId, command.targetFaceId);
            long price = checked(OfferPrice(s, offer) - OfferTradeIn(s, offer)); // Freeze before discount or replacement changes.
            s.gold = checked(s.gold - price);
            switch (offer.category)
            {
                case "controller":
                    var old = s.controllers.FirstOrDefault(c => c.definitionId == offer.contentId);
                    var retainedTurnover = s.controllers.Where(c => c.definitionId == "C51").Select(c => c.instanceId).ToArray();
                    int location = old == null ? s.controllers.Count : s.controllers.IndexOf(old);
                    if (old != null) { s.controllers.Remove(old); s.lastEffects.Add(new EffectRecord { kind = "ControllerReplaced", sourceId = old.definitionId, detail = old.instanceId, newValue = ControllerRules.SalePrice(old, cfg) }); }
                    var acquired = offer.controller.Clone(); acquired.state = new ControllerDynamicState(); acquired.alive = true;
                    if (acquired.definitionId == "C45")
                    {
                        var effect = new GameRng(s.effectRngState); var trace = DrawRaw(s, effect, "effect", "C45Acquire", acquired.instanceId, 2);
                        acquired.state.activeColor = trace.rawValue = trace.appliedValue = trace.rawTicket == 0 ? "red" : "blue";
                        s.effectRngState = effect.State;
                    }
                    s.controllers.Insert(location, acquired);
                    if (old != null) GrowTurnover(s, retainedTurnover);
                    break;
                case "consumable": s.inventory.Add(NewOwnedItem(s, offer.contentId)); break;
                case "hand_upgrade": s.handLevels.Concat(s.colorLevels).First(x => x.handId == offer.contentId).level++; break;
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
                    else if (offer.contentId == "O04") { s.dice.RemoveAll(x => x.dieId == command.targetDieId); s.lastEffects.Add(new EffectRecord { kind = "DieRemoved", sourceId = "O04", dieId = command.targetDieId }); }
                    else throw new RuleViolation("服务不能以此方式购买。");
                    break;
                case "die_upgrade": ApplyUpgrade(s, offer.contentId, command.targetDieId, command.targetFaceId); break;
            }
            offer.sold = true;
            s.lastEffects.Add(new EffectRecord { kind = "Purchased", sourceId = offer.contentId, detail = offer.offerId, oldValue = price, newValue = s.gold });
        }

        private OwnedItem NewOwnedItem(RunState s, string id) { return new OwnedItem { instanceId = "ITEM_" + (s.nextItemSerial++).ToString("D5"), contentId = id }; }

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
                case "U06": f.isGold = true; break;
                case "U07":
                    int old = f.permanentPip; var candidates = Enumerable.Range(cfg.minPipValue, cfg.maxPipValue - cfg.minPipValue + 1).Where(x => x != old).ToArray();
                    var rng = new GameRng(s.effectRngState); f.permanentPip = candidates[DrawRaw(s, rng, "effect", "FaceTransform", dieId + "/" + faceId, candidates.Length).rawTicket]; s.effectRngState = rng.State;
                    s.lastEffects.Add(new EffectRecord { kind = "FaceTransformed", sourceId = id, dieId = dieId, faceId = faceId, oldValue = old, newValue = f.permanentPip });
                    break;
                default: throw new RuleViolation("未知骰子强化。");
            }
            d.dataRevision++;
            s.lastEffects.Add(new EffectRecord { kind = "DieUpgraded", sourceId = id, dieId = dieId, faceId = faceId });
        }

        private void Study(RunState s, string handId)
        {
            RequirePhase(s, "Shop");
            Check(s.shop.selfStudyUsed < cfg.shop.selfStudyLimit, "本店自选研习机会已使用。");
            Check(IsHandLegal(s, handId), "牌型已满级，或当前容量/永久骰数量不足。");
            int price = HandPrice(s, handId, true); Check(s.gold >= price, "金币不足。");
            s.gold = checked(s.gold - price); s.handLevels.Concat(s.colorLevels).First(x => x.handId == handId).level++; s.shop.selfStudyUsed++;
            s.lastEffects.Add(new EffectRecord { kind = "HandStudied", sourceId = handId, oldValue = price, newValue = Level(s, handId) });
        }

        private void GrowTurnover(RunState s, string[] beforeIds)
        {
            foreach (var c in s.controllers.Where(c => c.definitionId == "C51" && beforeIds.Contains(c.instanceId)))
            {
                c.state.turnoverUnits2 = checked(c.state.turnoverUnits2 + 1);
                s.lastEffects.Add(new EffectRecord { kind = "TurnoverGrown", sourceId = "C51", detail = c.instanceId, newValue = c.state.turnoverUnits2 });
            }
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
                sellPrice = ControllerRules.SalePrice(c, cfg); contentId = c.contentId; s.controllers.Remove(c); GrowTurnover(s, oldTurnover);
            }
            else { s.inventory.Remove(item); sellPrice = cfg.consumableSellPrice; contentId = item.contentId; }
            s.gold = checked(s.gold + sellPrice);
            s.lastEffects.Add(new EffectRecord { kind = "Sold", sourceId = contentId, detail = instanceId, newValue = sellPrice });
        }
    }
}
