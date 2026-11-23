using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        // Fixed expectations come from CONTENT_REWRITES, the W10 wording overrides and P0Config p1.1.0.
        // These tests deliberately do not use the formatter or runtime configuration as the text oracle.
        private const string StaffShort24 = "得分倍率从×1.5起，每家店首次卖出卡牌后+0.5。";
        private const string StaffRules24 = "同一家店再卖不再增加；卡牌自毁、被祭坛摧毁不算；被百变模仿复制时共用同一个倍率。";
        private const string BlueShort24 = "提交的骰子里没有红骰时，得分×3。";
        private const string WaveShort24 = "本手提交时，提交的骰子全部按红色计算。";
        private const string WaveRules24 = "锁定骰子后才能使用；之后继续投掷也不失效；只影响本次计分，骰面不变；每手只能用1次，且不能再用蓝色浪潮。";
        private const string SlotShort24 = "道具栏位+1（最多5个）。";
        private const string SlotRules24 = "开局2个；道具打折对价格有效，最低1金币。";
        private const string SmoothShort24 = "这一面变光滑：它的对面更难掷出。";
        private const string SmoothRules24 = "对面的机会×0.8，其余5面×1.04；标准骰对面约13.3%，其余约17.3%。";

        [UnityTest] public IEnumerator PLAY_V24_TX06_ShopOffersAndOwnedDetailsDisplayApprovedContent()
        {
            var fixture = ShopBoundaryFixture();
            fixture.shop.offers.Clear();
            fixture.controllers.Clear();
            fixture.inventory.Clear();
            var owned = BoundaryController(fixture, "C51");
            fixture.controllers.Add(owned);
            string wave = BoundaryItem(fixture, "I01");
            var cases = new[]
            {
                (id:"C51", category:"controller", name:"人员流动", price:4, brief:StaffShort24, rules:StaffRules24),
                (id:"C02", category:"controller", name:"蓝色疯狂", price:7, brief:BlueShort24, rules:""),
                (id:"I01", category:"consumable", name:"红色浪潮", price:2, brief:WaveShort24, rules:WaveRules24),
                (id:"O01", category:"service", name:"道具增加", price:2, brief:SlotShort24, rules:SlotRules24),
                (id:"U01", category:"die_upgrade", name:"光滑骰面", price:1, brief:SmoothShort24, rules:SmoothRules24)
            };
            foreach (var sample in cases) AddBoundaryOffer(fixture, sample.id, sample.category, sample.id);
            yield return W14Load(fixture);
            string state = StateJson();
            byte[] save = File.ReadAllBytes(app.SaveLocation);
            foreach (var sample in cases)
            {
                bool card = sample.category == "controller";
                yield return PageToOffer14(sample.id);
                var row = FindActive<RectTransform>((card ? "Offer_" : "Product_") + sample.id);
                CheckContent24(row, "Buy_" + sample.id + "/Price", sample.price.ToString());
                if (card) CheckContent24(row, "ShortText11", sample.brief);
                yield return CheckOfferTip24(card ? row : row.Find("Inspect"), sample.name, sample.brief,
                    sample.rules, retired: sample.id == "C02" ? "非空提交没有有效红面时，整手得分×3。" : null,
                    existingState: card ? "无附魔" : null);
                if (sample.id == "U01")
                {
                    yield return OpenBoundaryOffer(sample.id);
                    Assert.That(app.FacePickStep14, Is.EqualTo("S1"));
                    yield return VisibleTargetClick("CancelFacePick14");
                    continue;
                }
                yield return OpenBoundaryOffer(sample.id);
                yield return Art073Settle(.2f);
                var dialog = FindActive<RectTransform>("Dialog");
                CheckContent24(dialog, "DialogTitle", sample.name);
                CheckContent24(dialog, "OfferDetail/Viewport/Content/SkillBody", sample.brief + sample.rules);
                var scroll = dialog.Find("OfferDetail").GetComponent<ScrollRect>();
                scroll.verticalNormalizedPosition = 0;
                Canvas.ForceUpdateCanvases();
                CheckContent24(dialog, "OfferDetail/Viewport/Content/PriceBody", "价格：" + sample.price + " 金币（你有 200）");
                Click("CloseDialog");
            }
            yield return LegacyVisibleClick07(Button("Controller_" + owned.instanceId));
            yield return Art073Settle(.2f);
            var held = FindActive<RectTransform>("Dialog");
            CheckContent24(held, "ControllerDetail/Viewport/Content/SkillBody", StaffShort24 + StaffRules24);
            Click("CloseDialog");
            var inventoryButton = Button("ShopItem_" + wave);
            yield return CheckOfferTip24(inventoryButton.transform, "红色浪潮", WaveShort24, WaveRules24, existingState: "出售获得 1 金币");
            yield return LegacyVisibleClick07(inventoryButton);
            yield return Art073Settle(.2f);
            var item = FindActive<RectTransform>("Dialog");
            CheckContent24(item, "ItemDetail/Viewport/Content/SkillBody", WaveShort24 + WaveRules24);
            Click("CloseDialog");
            Assert.That(StateJson(), Is.EqualTo(state));
            CollectionAssert.AreEqual(save, File.ReadAllBytes(app.SaveLocation));
        }

        [UnityTest] public IEnumerator PLAY_V24_TX06_HandUpgradePreservesPriceLevelsAndActualMultiplier()
        {
            var fixture = ShopBoundaryFixture();
            fixture.shop.offers.Clear();
            fixture.handLevels.Single(h => h.handId == "pair").level = 2;
            AddBoundaryOffer(fixture, "pair", "hand_upgrade", "pair");
            yield return W14Load(fixture);
            // pair starts at x2.00, gains x0.75 per level and costs 2 + 2 per existing level.
            const string preview = "Lv.2 → Lv.3 · ×3.5 → ×4.25";
            const string effect = "本局一直有效；每升 1 级倍率 +0.75，最高 8 级。";
            var row = FindActive<RectTransform>("Product_pair");
            CheckContent24(row, "Buy_pair/Price", "6");
            yield return CheckOfferTip24(row.Find("Inspect"), "一对", preview, effect);
            yield return OpenBoundaryOffer("pair");
            yield return Art073Settle(.2f);
            var dialog = FindActive<RectTransform>("Dialog");
            CheckContent24(dialog, "DialogTitle", "一对");
            CheckContent24(dialog, "OfferDetail/Viewport/Content/SkillBody", effect);
            var scroll = dialog.Find("OfferDetail").GetComponent<ScrollRect>();
            scroll.verticalNormalizedPosition = 0;
            Canvas.ForceUpdateCanvases();
            CheckContent24(dialog, "OfferDetail/Viewport/Content/PriceBody", "价格：6 金币（你有 200）");
            Assert.That(app.Session.State.handLevels.Single(h => h.handId == "pair").level, Is.EqualTo(2));
        }

        [UnityTest] public IEnumerator PLAY_V24_TX06_DiceShopAndRewardDetailsUseTheirOwnTerms()
        {
            var shop = ShopBoundaryFixture();
            var candidate = shop.shop.offers.First(o => o.category == "new_die" && string.IsNullOrEmpty(o.groupId));
            yield return W14Load(shop);
            yield return OpenBoundaryOffer(candidate.offerId);
            yield return Art073Settle(.2f);
            var detail = FindActive<RectTransform>("Dialog");
            CheckContent24(detail, "DialogTitle", "购买骰子");
            CheckContent24(detail, "CandidateNotice", "拖动旋转查看六个面");
            var rules = detail.GetComponentsInChildren<TextMeshProUGUI>().Single(t => t.name == "CandidateRules");
            CheckContent24(rules.transform.parent, rules.name, "买下一颗新骰子（永久骰最多8颗）。每家商店提供2颗候选；不增加提交栏格数。");
            CheckContent24(detail, "ConfirmPurchase/Label", "花 " + candidate.basePrice + " 金币购买");
            Click("CloseDialog");

            var loot = LootShop24("L4");
            var upgrade = UpgradeQuote24(loot, "LOOT", "U01");
            yield return W14Load(loot);
            yield return LegacyVisibleClick07(Button("Product_" + upgrade.offerId));
            yield return Art073Settle(.2f);
            detail = FindActive<RectTransform>("Dialog");
            CheckContent24(detail, "DialogTitle", "光滑骰面");
            CheckContent24(detail, "OfferDetail/Viewport/Content/SkillBody", SmoothShort24 + SmoothRules24);
            CheckContent24(detail, "ConfirmPurchase/Label", "免费领取");
            Click("CloseDialog");

            var gift = LootShop24("L5");
            var freeDie = GroupOffers24(gift).First(o => o.category == "new_die");
            yield return W14Load(gift);
            yield return LegacyVisibleClick07(Button("Product_" + freeDie.offerId));
            yield return Art073Settle(.2f);
            detail = FindActive<RectTransform>("Dialog");
            CheckContent24(detail, "DialogTitle", "领取骰子");
            int dice = gift.dice.Count(d => d.lifetime == "permanent");
            CheckContent24(detail, "Source", "领取后：骰子 " + dice + " → " + (dice + 1) + " 颗（提交栏仍是 " + app.Session.EffectiveCapacity + " 格）");
            CheckContent24(detail, "CandidateNotice", "拖动旋转查看六个面");
            CheckContent24(detail, "ConfirmPurchase/Label", "免费领取");
            var giftRules = detail.GetComponentsInChildren<TextMeshProUGUI>().SingleOrDefault(t => t.name == "CandidateRules");
            if (giftRules != null) Assert.That(giftRules.text, Does.Not.Contain("每家商店提供2颗候选"));
        }

        private IEnumerator CheckOfferTip24(Transform anchor, string title, string brief, string rules,
            string retired = null, string existingState = null)
        {
            Assert.That(anchor, Is.Not.Null, title);
            Assert.That(anchor.GetComponent<Art07HoverTip>(), Is.Not.Null, title);
            var rect = (RectTransform)anchor;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            Assert.That(hits, Is.Not.Empty, title);
            Assert.That(hits[0].gameObject.GetComponentInParent<Art07HoverTip>(), Is.EqualTo(anchor.GetComponent<Art07HoverTip>()), title);
            ExecuteEvents.Execute(anchor.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.45f);
            Canvas.ForceUpdateCanvases();
            var tip = FindActive<RectTransform>("ContentTooltip");
            CheckContent24(tip, "Name", title);
            // The two dedicated tip fields also carry enchantment, sale or availability state.
            string state = RenderedContent24(tip, "State");
            string copy = RenderedContent24(tip, "Description") + state;
            string required = PrintedContent24(brief + rules);
            Assert.That(copy, Does.Contain(required), title);
            Assert.That(copy.IndexOf(PrintedContent24(brief), StringComparison.Ordinal),
                Is.EqualTo(copy.LastIndexOf(PrintedContent24(brief), StringComparison.Ordinal)), title);
            if (!string.IsNullOrEmpty(retired)) Assert.That(copy, Does.Not.Contain(PrintedContent24(retired)), title);
            if (existingState != null) Assert.That(state, Does.Contain(PrintedContent24(existingState)), title);
            ExecuteEvents.Execute(anchor.gameObject, pointer, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.10f);
            Assert.That(HasActive("ContentTooltip"), Is.False, title);
        }

        private static string PrintedContent24(string value) => new string(value.Where(PrintedCharacter24).ToArray());

        private static void CheckContent24(Transform scope, string path, string expected)
        {
            Assert.That(RenderedContent24(scope, path), Is.EqualTo(PrintedContent24(expected)), scope.name + "/" + path);
        }

        private static string RenderedContent24(Transform scope, string path)
        {
            var node = scope.Find(path);
            Assert.That(node, Is.Not.Null, scope.name + "/" + path);
            var text = node.GetComponent<TextMeshProUGUI>();
            Assert.That(text != null && text.isActiveAndEnabled, Is.True, scope.name + "/" + path);
            text.ForceMeshUpdate();
            if (!string.IsNullOrWhiteSpace(text.text))
                Assert.That(text.color.a * text.canvasRenderer.GetInheritedAlpha(), Is.GreaterThan(.05f), path);
            Assert.That(text.isTextTruncated, Is.False, path);
            var clip = new Rect(0, 0, Screen.width, Screen.height);
            var scroll = text.GetComponentInParent<ScrollRect>();
            if (scroll != null) clip = V06ScreenRect(scroll.viewport);
            var glyphs = text.textInfo.characterInfo.Take(text.textInfo.characterCount)
                .Where(c => c.isVisible && c.color.a > 0 && PrintedCharacter24(c.character));
            return new string(glyphs.Where(c => clip.Overlaps(GlyphArea24(text.transform, c))).Select(c => c.character).ToArray());
        }
    }
}
