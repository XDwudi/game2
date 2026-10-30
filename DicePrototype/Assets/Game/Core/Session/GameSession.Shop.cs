using System;
using System.Collections.Generic;
using System.Linq;

namespace DiceCraft.Core
{
    public sealed partial class GameSession
    {
        private int ShopCapacity(RunState s) { return ControllerRules.Capacity(s.controllers, cfg); }
        private int PermanentCount(RunState s) { return s.dice.Count(x => x.lifetime == "permanent"); }
        private int Level(RunState s, string id) { return s.handLevels.First(x => x.handId == id).level; }
        private bool IsHandLegal(RunState s, string id)
        {
            var h = cfg.handDefinitions.FirstOrDefault(x => x.id == id);
            int minimum = h != null && h.id == "straight" && Has(s, "C29") ? cfg.Controller("C29").parameters.minimumStraightLength : h == null ? int.MaxValue : h.minSubmittedDice;
            return h != null && h.enabledInNormalShop && minimum <= ShopCapacity(s)
                && h.requirePermanentDice <= PermanentCount(s) && Level(s, id) < h.maxLevel;
        }

        private int HandPrice(RunState s, string id, bool selfStudy)
        {
            return checked(cfg.Hand(id).baseUpgradePrice + cfg.shop.upgradePricePerExistingLevel * Level(s, id) + (selfStudy ? cfg.shop.selfStudyPremium : 0));
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
                    if (offer.contentId == "O01") basePrice = checked(basePrice + 3 * (s.consumableSlots - cfg.initialConsumableSlots));
                    return Discount(s, basePrice);
                default: throw new RuleViolation("商品类别无效。");
            }
        }

        private int Discount(RunState s, int price) { return Math.Max(cfg.minDiscountedItemPrice, price - s.itemDiscount); }
        private int OfferTradeIn(RunState s, ShopOffer offer)
        {
            return offer != null && offer.category == "controller" && Has(s, offer.contentId) ? cfg.controllerSellPrice : 0;
        }

        private ShopOffer MakeOffer(RunState s, string category, string contentId, int basePrice)
        {
            return new ShopOffer { offerId = "OFFER_" + (s.nextOfferSerial++).ToString("D5"), category = category, contentId = contentId, basePrice = basePrice };
        }

        private void GenerateShop(RunState s)
        {
            Check(s.shop == null || !s.shop.generated, "本商店已经生成，不能刷新。");
            var shop = new ShopState { shopId = s.stage.stageId + "_SHOP", generated = true };
            var rng = new GameRng(s.shopRngState);
            var controllers = cfg.controllers.Where(x => !Has(s, x.id) || ControllerRules.ReplacementEnchantments(x.id, s.controllers.First(c => c.definitionId == x.id).enchantmentId, cfg).Length > 0)
                .OrderBy(x => x.id, StringComparer.Ordinal).ToList();
            for (int i = 0; i < cfg.shop.controllerOffers && controllers.Count > 0; i++)
            {
                var pool = i == 0 ? controllers.Where(x => !Has(s, x.id) && x.buyPrice <= cfg.shop.firstControllerGuaranteeMaxPrice).ToList() : controllers;
                if (pool.Count == 0) pool = controllers;
                int draw = rng.NextIndex(pool.Sum(x => x.offerWeight)), sum = 0;
                var picked = pool[0];
                foreach (var candidate in pool) { sum += candidate.offerWeight; if (draw < sum) { picked = candidate; break; } }
                var owned = s.controllers.FirstOrDefault(c => c.definitionId == picked.id);
                string enchantment = ControllerRules.DrawEnchantment(picked.id, owned == null ? null : owned.enchantmentId, rng, cfg);
                var offer = MakeOffer(s, "controller", picked.id, picked.buyPrice);
                int serial = s.nextItemSerial++;
                offer.controller = new ControllerInstance { instanceId = "ITEM_" + serial.ToString("D5"), definitionId = picked.id, enchantmentId = enchantment, acquiredSerial = serial };
                shop.offers.Add(offer); controllers.Remove(picked);
            }
            AddUniformOffers(s, shop, rng, cfg.consumables.Select(x => x.id), cfg.shop.consumableOffers, "consumable", id => cfg.Consumable(id).baseBuyPrice);
            AddUniformOffers(s, shop, rng, cfg.handDefinitions.Where(x => IsHandLegal(s, x.id)).Select(x => x.id), cfg.shop.handUpgradeOffers, "hand_upgrade", id => cfg.Hand(id).baseUpgradePrice);
            AddUniformOffers(s, shop, rng, cfg.diceUpgrades.Where(x => HasUpgradeTarget(s, x.id)).Select(x => x.id), cfg.shop.diceUpgradeOffers, "die_upgrade", id => cfg.Upgrade(id).buyPrice);
            AddUniformOffers(s, shop, rng, cfg.services.Where(x => x.id != "O03" && ServiceLegal(s, x.id)).Select(x => x.id), cfg.shop.serviceOffers, "service", id => cfg.Service(id).baseBuyPrice);
            if (PermanentCount(s) < cfg.maxPermanentDice)
            {
                for (int i = 0; i < cfg.shop.newDiceOffers; i++)
                {
                    var die = GenerateDie(rng); var reasons = new List<string>();
                    int price = QuoteNewDie(die, reasons);
                    var offer = MakeOffer(s, "new_die", "O03", price);
                    offer.die = die; offer.priceReasons = reasons.ToArray(); shop.offers.Add(offer);
                }
            }
            s.shop = shop; s.shopRngState = rng.State;
        }

        private void AddUniformOffers(RunState s, ShopState shop, GameRng rng, IEnumerable<string> candidates, int count, string category, Func<string, int> price)
        {
            var ids = candidates.OrderBy(x => x, StringComparer.Ordinal).ToList();
            for (int i = 0; i < count && ids.Count > 0; i++)
            {
                int index = rng.NextIndex(ids.Count); string id = ids[index]; ids.RemoveAt(index);
                shop.offers.Add(MakeOffer(s, category, id, price(id)));
            }
        }

        private DieInstance GenerateDie(GameRng rng)
        {
            var weights = cfg.generation.newDieTemplateWeights;
            int selection = rng.NextIndex(weights.random + weights.low + weights.high);
            int[] range = selection < weights.random ? cfg.generation.pipRange
                : selection < weights.random + weights.low ? cfg.generation.lowRange : cfg.generation.highRange;
            var d = DiceFactory.CreateStandard(cfg, 1); d.dieId = ""; d.createdSerial = 0;
            foreach (var f in d.faces.OrderBy(x => x.faceId, StringComparer.Ordinal))
            {
                f.permanentPip = range[0] + rng.NextIndex(range[1] - range[0] + 1);
                f.baseColor = rng.NextIndex(100) < cfg.generation.redFaceChancePercent ? "red" : "blue";
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
            return price;
        }

        private bool ServiceLegal(RunState s, string id)
        {
            switch (id)
            {
                case "O01": return s.consumableSlots < cfg.maxConsumableSlots;
                case "O02": return s.itemDiscount < cfg.maxItemDiscount;
                case "O03": return PermanentCount(s) < cfg.maxPermanentDice && s.dice.Count < cfg.maxActiveDice;
                case "O04": return PermanentCount(s) > 1;
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
                    Check(equipment.Length <= cfg.maxControllerInstances && (s.isDebug ? ControllerRules.OrdinaryUsed(equipment) <= s.controllerSlots : ControllerRules.OrdinaryUsed(equipment) <= cfg.initialControllerSlots), "普通控制器槽或总件数已满，可先出售控制器。");
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
            int price = checked(OfferPrice(s, offer) - OfferTradeIn(s, offer)); // Freeze before discount or replacement changes.
            s.gold -= price;
            switch (offer.category)
            {
                case "controller":
                    var old = s.controllers.FirstOrDefault(c => c.definitionId == offer.contentId);
                    if (old != null) { s.controllers.Remove(old); s.lastEffects.Add(new EffectRecord { kind = "ControllerReplaced", sourceId = old.definitionId, detail = old.instanceId, newValue = cfg.controllerSellPrice }); }
                    s.controllers.Add(offer.controller.Clone()); break;
                case "consumable": s.inventory.Add(NewOwnedItem(s, offer.contentId)); break;
                case "hand_upgrade": s.handLevels.First(x => x.handId == offer.contentId).level++; break;
                case "new_die":
                    var d = offer.die.Clone(); d.createdSerial = s.nextDieSerial++; d.dieId = "D" + d.createdSerial.ToString("D3");
                    d.lifetime = "permanent"; d.createdStageId = ""; s.dice.Add(d);
                    s.lastEffects.Add(new EffectRecord { kind = "DieAdded", sourceId = "O03", dieId = d.dieId }); break;
                case "service":
                    if (offer.contentId == "O01") s.consumableSlots++;
                    else if (offer.contentId == "O02") s.itemDiscount++;
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
                    var rng = new GameRng(s.effectRngState); f.permanentPip = candidates[rng.NextIndex(candidates.Length)]; s.effectRngState = rng.State;
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
            s.gold -= price; s.handLevels.First(x => x.handId == handId).level++; s.shop.selfStudyUsed++;
            s.lastEffects.Add(new EffectRecord { kind = "HandStudied", sourceId = handId, oldValue = price, newValue = Level(s, handId) });
        }

        private void Sell(RunState s, string instanceId)
        {
            RequirePhase(s, "Shop");
            var c = s.controllers.FirstOrDefault(x => x.instanceId == instanceId);
            var item = s.inventory.FirstOrDefault(x => x.instanceId == instanceId);
            Check(c != null || item != null, "该物品不存在、已售出或已消耗。");
            int sellPrice;
            string contentId;
            if (c != null) { s.controllers.Remove(c); sellPrice = cfg.controllerSellPrice; contentId = c.contentId; }
            else { s.inventory.Remove(item); sellPrice = cfg.consumableSellPrice; contentId = item.contentId; }
            s.gold = checked(s.gold + sellPrice);
            s.lastEffects.Add(new EffectRecord { kind = "Sold", sourceId = contentId, detail = instanceId, newValue = sellPrice });
        }
    }
}
