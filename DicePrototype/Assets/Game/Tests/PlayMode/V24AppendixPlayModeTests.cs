using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DiceCraft.Core;
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
        [Serializable] private sealed class AppendixField
        {
            public string id, field, path, text, printed, expected, problem;
            public int scrollPositions;
        }

        [Serializable] private sealed class AppendixRun
        {
            public string caseName, head, started, finished, unity, theme;
            public int width, height, percent;
            public List<AppendixField> fields = new List<AppendixField>();
        }

        private AppendixRun StartAppendix(string caseName)
        {
            app.ApplyTheme11("default", false);
            W11TextScale(100);
            return new AppendixRun
            {
                caseName = caseName, head = IdentityHead20X(), started = DateTime.UtcNow.ToString("o"),
                unity = Application.unityVersion, theme = app.ThemeId11,
                width = Screen.width, height = Screen.height, percent = Mathf.RoundToInt(100 * UiKit.FontScale)
            };
        }

        // Keep partial evidence even if fixture preparation or a real pointer route fails before the final row.
        private static void SaveAppendix(AppendixRun run)
        {
            string directory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..",
                "artifacts", "cache", "v11", "w25", "tx06-matrix", "results", run.width + "x" + run.height));
            Directory.CreateDirectory(directory);
            run.finished = DateTime.UtcNow.ToString("o");
            string file = run.caseName + "-" + DateTime.UtcNow.ToString("yyyyMMddTHHmmssfffffffZ") + ".json";
            var utf8 = new System.Text.UTF8Encoding(false);
            byte[] bytes = utf8.GetBytes(JsonUtility.ToJson(run, true));
            File.WriteAllBytes(Path.Combine(directory, file), bytes);
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                string digest = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                string receipt = "TX06_CAPTURE " + file + " " + digest;
                TestContext.Out.WriteLine(receipt);
                Debug.Log(receipt);
            }
        }

        private IEnumerator LoadAppendix(AppendixRun run, RunState fixture)
        {
            yield return W14Load(fixture);
            app.ApplyTheme11("default", false);
            W11TextScale(100);
            yield return W11Settle();
            Assert.That((app.ThemeId11, Mathf.RoundToInt(100 * UiKit.FontScale), Screen.width, Screen.height),
                Is.EqualTo((run.theme, run.percent, run.width, run.height)));
        }
        private static void FinishAppendix(AppendixRun run)
        {
            var errors = run.fields.Where(r => !string.IsNullOrEmpty(r.problem))
                .Select(r => r.id + "/" + r.field + ": " + r.problem).ToArray();
            Assert.That(run.fields, Is.Not.Empty);
            Assert.That(errors, Is.Empty, string.Join("\n", errors));
        }

        private static Transform NodeAppendix(Transform root, string name)
        {
            return root.GetComponentsInChildren<Transform>(false)
                .SingleOrDefault(t => t.name == name && t.gameObject.activeInHierarchy);
        }

        // Union the ink actually visible through every active ancestor mask across contiguous scroll windows.
        // The raw TMP string is recorded independently of its glyphs. Hidden suffixes and ellipses fail.
        private static void ReadAppendix(AppendixRun run, string id, string field, Transform root,
            string name, string expected, bool absentWhenEmpty = false)
        {
            var sample = new AppendixField { id = id, field = field, path = root.name + "/" + name, expected = expected };
            run.fields.Add(sample);
            try
            {
                var node = NodeAppendix(root, name);
                if (node == null && absentWhenEmpty && string.IsNullOrEmpty(expected))
                {
                    sample.printed = sample.text = "";
                    sample.path += " (empty, omitted)";
                    return;
                }
                Assert.That(node, Is.Not.Null, sample.path);
                var text = node.GetComponent<TextMeshProUGUI>();
                Assert.That(text != null && text.isActiveAndEnabled, Is.True, sample.path);
                var path = node.name;
                for (var p = node.parent; p != null && p != root.parent; p = p.parent) path = p.name + "/" + path;
                sample.path = path;
                Canvas.ForceUpdateCanvases();
                text.ForceMeshUpdate();
                sample.text = text.text;
                if (PrintedContent24(expected).Length != 0)
                    Assert.That(text.color.a * text.canvasRenderer.GetInheritedAlpha(), Is.GreaterThan(.05f), path);
                Assert.That(text.isTextTruncated, Is.False, path);
                var scroll = text.GetComponentInParent<ScrollRect>();
                float original = scroll == null ? 1 : scroll.verticalNormalizedPosition;
                int intervals = scroll == null || !scroll.vertical ? 0 :
                    Mathf.CeilToInt(Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height) /
                        Mathf.Max(1, scroll.viewport.rect.height * .5f));
                var painted = new SortedDictionary<int, char>();
                try
                {
                    for (int i = 0; i <= intervals; i++)
                    {
                        if (scroll != null && intervals > 0) scroll.verticalNormalizedPosition = 1f - (float)i / intervals;
                        Canvas.ForceUpdateCanvases();
                        text.ForceMeshUpdate();
                        var masks = text.GetComponentsInParent<RectMask2D>(false).Where(m => m.isActiveAndEnabled)
                            .Select(m => V06ScreenRect(m.rectTransform)).ToList();
                        masks.Add(new Rect(0, 0, Screen.width, Screen.height));
                        masks.Add(V06ScreenRect(text.rectTransform));
                        if (scroll != null && scroll.viewport != null) masks.Add(V06ScreenRect(scroll.viewport));
                        for (int n = 0; n < text.textInfo.characterCount; n++)
                        {
                            var glyph = text.textInfo.characterInfo[n];
                            if (!glyph.isVisible || glyph.color.a == 0 || !PrintedCharacter24(glyph.character)) continue;
                            var bounds = GlyphArea24(text.transform, glyph);
                            if (masks.All(m => Art073Inside(bounds, m, 1.5f))) painted[glyph.index] = glyph.character;
                        }
                        sample.scrollPositions++;
                    }
                }
                finally
                {
                    if (scroll != null && intervals > 0) scroll.verticalNormalizedPosition = original;
                    Canvas.ForceUpdateCanvases();
                }
                sample.printed = new string(painted.Values.ToArray());
                Assert.That(PrintedContent24(sample.text), Is.EqualTo(PrintedContent24(expected)), path + " TMP text");
                Assert.That(sample.printed, Is.EqualTo(PrintedContent24(expected)), path + " complete visible ink");
            }
            catch (Exception error) { sample.problem = error.Message; }
        }

        private static string CombinedAppendix(string shortText, string details)
            => string.IsNullOrEmpty(details) || details == shortText ? shortText : shortText + "\n" + details;

        [UnityTest, Timeout(600000)]
        public IEnumerator PLAY_V24_TX06_MatrixCardsA() => CardsAppendix(nameof(PLAY_V24_TX06_MatrixCardsA), 0);
        [UnityTest, Timeout(600000)]
        public IEnumerator PLAY_V24_TX06_MatrixCardsB() => CardsAppendix(nameof(PLAY_V24_TX06_MatrixCardsB), 50);
        [UnityTest, Timeout(600000)]
        public IEnumerator PLAY_V24_TX06_MatrixCardsC() => CardsAppendix(nameof(PLAY_V24_TX06_MatrixCardsC), 100);

        private IEnumerator CardsAppendix(string name, int skip)
        {
            var run = StartAppendix(name);
            try
            {
                var defs = app.Config.controllers.OrderBy(x => int.Parse(x.id.Substring(1), CultureInfo.InvariantCulture))
                    .Skip(skip).Take(50).ToArray();
                Assert.That(defs.Length, Is.EqualTo(50));
                var baseline = ShopBoundaryFixture();
                for (int i = 0; i < defs.Length; i += 4)
                {
                    var fixture = baseline.Clone();
                    fixture.shop.offers.Clear();
                    var batch = defs.Skip(i).Take(4).ToArray();
                    foreach (var def in batch) AddBoundaryOffer(fixture, def.id, "controller", def.id);
                    yield return LoadAppendix(run, fixture);
                    foreach (var def in batch)
                    {
                        var card = FindActive<RectTransform>("Offer_" + def.id);
                        ReadAppendix(run, def.id, "short", card, "ShortText11", def.shortText);
                        yield return OpenBoundaryOffer(def.id);
                        yield return Art073Settle(.18f);
                        ReadAppendix(run, def.id, "body", FindActive<RectTransform>("Dialog"), "SkillBody",
                            CombinedAppendix(def.shortText, def.details));
                        Click("CloseDialog");
                    }
                }
            }
            finally { SaveAppendix(run); }
            FinishAppendix(run);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator PLAY_V24_TX06_MatrixChapterAndLoot()
        {
            var run = StartAppendix(nameof(PLAY_V24_TX06_MatrixChapterAndLoot));
            try
            {
                var plan = ShopAfterStage10(0).cycles;
                foreach (var def in app.Config.bossModifiers)
                {
                    // The actual saved chapter must permit this modifier. Reuse its existing slot if
                    // present or substitute it only in an eligible distinct slot (tier 4 is chapter 8).
                    int cycle = plan.FindIndex(c => c.modifierId == def.id);
                    if (cycle < 0) cycle = def.tier == 4 ? plan.Count - 1 : Math.Max(0, def.minCycle - 1);
                    var fixture = ShopAfterStage10(cycle * 3);
                    fixture.cycles[cycle].modifierId = def.id;
                    yield return LoadAppendix(run, fixture);
                    Assert.That(app.Session.CycleView().Last().modifierId, Is.EqualTo(def.id));
                    Click("ViewCycle");
                    yield return W11Settle();
                    var card = FindActive<RectTransform>("Dialog").Find("CycleCard2");
                    ReadAppendix(run, def.id, "short", card, "ModifierText", def.shortText);
                    if (!string.IsNullOrEmpty(def.details))
                    {
                        var hit = NodeAppendix(card, "ModifierDetails16");
                        yield return TipAppendix(hit);
                        ReadAppendix(run, def.id, "details", FindActive<RectTransform>("ContentTooltip"),
                            "Description", def.details);
                        yield return EndTipAppendix(hit);
                    }
                    Click("CloseCycle");
                }
                foreach (var def in app.Config.lootKinds)
                {
                    yield return LoadAppendix(run, LootShop24(def.id));
                    var info = FindActive<RectTransform>("LootInfo15");
                    ReadAppendix(run, def.id, "short", info, "LootShort15", def.shortText);
                    ReadAppendix(run, def.id, "details", info, "LootDetails15", def.details, true);
                }
            }
            finally { SaveAppendix(run); }
            FinishAppendix(run);
        }

        [UnityTest, Timeout(300000)]
        public IEnumerator PLAY_V24_TX06_MatrixRetainedNames()
        {
            var run = StartAppendix(nameof(PLAY_V24_TX06_MatrixRetainedNames));
            try
            {
                yield return LoadAppendix(run, ShopBoundaryFixture());
                Click("Handbook");
                Click("HandbookHands");
                yield return W11Settle();
                foreach (var def in app.Config.handDefinitions)
                    ReadAppendix(run, def.id, "name", FindActive<RectTransform>("HandbookRow_" + def.id), "Name", def.displayName);
                Click("HandbookColors");
                yield return W11Settle();
                foreach (var def in app.Config.colorDefinitions)
                {
                    int level = app.Session.State.colorLevels.Single(c => c.handId == def.id).level;
                    ReadAppendix(run, def.id, "name", FindActive<RectTransform>("HandbookRow_" + def.id),
                        "Name", (def.id == "all_red" ? "● " : "◆ ") + def.displayName + "   Lv." + level);
                }
                for (int i = 0; i < app.Config.redRewards.Length; i++)
                {
                    var red = app.Config.redRewards[i];
                    string factor = (red.factor100 / 100m).ToString("0.##", CultureInfo.InvariantCulture);
                    ReadAppendix(run, "R" + red.count, "redValue", FindActive<RectTransform>("RedReward" + i),
                        "Value", red.count + " 颗  ×" + factor);
                }
                // W10 retained six configured names. The purchase UI actually prints an effect sentence.
                foreach (var enchant in app.Config.enchantments)
                {
                    var shop = ShopBoundaryFixture();
                    shop.shop.offers.Clear();
                    var card = app.Config.controllers.First(d => ControllerRules.IsEnchantmentLegal(d.id, enchant.id, app.Config));
                    AddBoundaryOffer(shop, card.id, "controller", card.id);
                    shop.shop.offers[0].controller.enchantmentId = enchant.id;
                    yield return LoadAppendix(run, shop);
                    yield return OpenBoundaryOffer(card.id);
                    yield return Art073Settle(.18f);
                    string expected;
                    switch (enchant.id)
                    {
                        case "none": expected = "无附魔"; break;
                        case "slotless": expected = "不占栏位：不占卡牌栏位（仍算在 " + app.Config.maxControllerInstances + " 张上限里）"; break;
                        case "base10": expected = "基础 +10：这张卡生效时基础分 +10"; break;
                        case "mult2": expected = "得分 ×2：这张卡生效时得分 ×2"; break;
                        case "submit1": expected = "每关提交 +1"; break;
                        case "roll1": expected = "每手掷骰 +1"; break;
                        default: throw new ArgumentOutOfRangeException(enchant.id);
                    }
                    ReadAppendix(run, enchant.id, "enchantEffect", FindActive<RectTransform>("Dialog"), "EnchantBody", expected);
                    Click("CloseDialog");
                }
            }
            finally { SaveAppendix(run); }
            FinishAppendix(run);
        }

        private IEnumerator TipAppendix(Transform anchor)
        {
            var rect = (RectTransform)anchor;
            var pointer = new PointerEventData(EventSystem.current)
            {
                position = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(pointer, hits);
            var hover = anchor.GetComponent<Art07HoverTip>();
            Assert.That(hover, Is.Not.Null, anchor.name);
            Assert.That(hits, Is.Not.Empty, anchor.name);
            Assert.That(hits[0].gameObject.GetComponentInParent<Art07HoverTip>(), Is.SameAs(hover), anchor.name);
            ExecuteEvents.Execute(anchor.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.46f);
            Canvas.ForceUpdateCanvases();
        }

        private static IEnumerator EndTipAppendix(Transform anchor)
        {
            ExecuteEvents.Execute(anchor.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.09f);
        }

        [UnityTest, Timeout(600000)]
        public IEnumerator PLAY_V24_TX06_MatrixShopGoods()
            => RunShopAppendix(ShopGoodsAppendix());

        // Unity Test Framework drives yielded iterators outside their parent iterator. If a nested
        // real click or fixture throws, dispose the entire chain here so the outer finally writes
        // partial measurements once; let the original failure and stack reach NUnit unchanged.
        private static IEnumerator RunShopAppendix(IEnumerator body)
        {
            var active = new Stack<IEnumerator>();
            active.Push(body);
            try
            {
                while (active.Count > 0)
                {
                    var current = active.Peek();
                    if (!current.MoveNext())
                    {
                        (active.Pop() as IDisposable)?.Dispose();
                        continue;
                    }
                    if (current.Current is IEnumerator child && !(child is CustomYieldInstruction))
                        active.Push(child);
                    else
                        yield return current.Current;
                }
            }
            finally
            {
                while (active.Count > 0) (active.Pop() as IDisposable)?.Dispose();
            }
        }

        private IEnumerator ShopGoodsAppendix()
        {
            var run = StartAppendix(nameof(PLAY_V24_TX06_MatrixShopGoods));
            try
            {
                var defs = app.Config.consumables.Select(d => (d.id, d.shortText, d.details, category: "consumable"))
                    .Concat(app.Config.services.Where(d => d.id != "O03").Select(d => (d.id, d.shortText, d.details, category: "service"))).ToArray();
                var baseline = ShopBoundaryFixture();
                // Ordinary initial shops have two consumable quotes and two service quotes.
                // Use the configured starting capacity even when one whole batch is the same category.
                int batchSize = Math.Min(app.Config.shop.initialConsumableOffers, app.Config.shop.serviceOffers);
                Assert.That(batchSize, Is.Positive, "The ordinary shop needs space for these quotes.");
                for (int i = 0; i < defs.Length; i += batchSize)
                {
                    var batch = defs.Skip(i).Take(batchSize).ToArray();
                    var fixture = baseline.Clone();
                    fixture.shop.offers.Clear();
                    foreach (var d in batch) AddBoundaryOffer(fixture, d.id, d.category, d.id);
                    yield return LoadAppendix(run, fixture);
                    foreach (var d in batch)
                    {
                        yield return PageToOffer14(d.id);
                        var row = FindActive<RectTransform>("Product_" + d.id);
                        if (d.category == "service")
                            ReadAppendix(run, d.id, "price", row, "Price", app.Config.Service(d.id).baseBuyPrice.ToString());
                        string rules = CombinedAppendix(d.shortText, d.details);
                        if (d.id == "O04")
                        {
                            var hit = NodeAppendix(row, "Inspect");
                            yield return TipAppendix(hit);
                            ReadAppendix(run, d.id, "body", FindActive<RectTransform>("ContentTooltip"), "Description", rules);
                            yield return EndTipAppendix(hit);
                        }
                        else
                        {
                            yield return OpenBoundaryOffer(d.id);
                            yield return Art073Settle(.18f);
                            ReadAppendix(run, d.id, "body", FindActive<RectTransform>("Dialog"), "SkillBody", rules);
                            Click("CloseDialog");
                        }
                    }
                }
                yield return LoadAppendix(run, baseline);
                var die = app.Session.State.shop.offers.First(o => o.category == "new_die" && string.IsNullOrEmpty(o.groupId));
                yield return PageToOffer14(die.offerId);
                var product = FindActive<RectTransform>("Product_" + die.offerId);
                ReadAppendix(run, "O03", "price", product, "Price", die.basePrice.ToString());
                yield return OpenBoundaryOffer(die.offerId);
                yield return Art073Settle(.18f);
                var service = app.Config.Service("O03");
                ReadAppendix(run, "O03", "body", FindActive<RectTransform>("Dialog"), "CandidateRules",
                    CombinedAppendix(service.shortText, service.details));
                Click("CloseDialog");
                foreach (var def in app.Config.diceUpgrades)
                {
                    var shop = LootShop24("L4");
                    var quote = UpgradeQuote24(shop, "LOOT", def.id);
                    yield return LoadAppendix(run, shop);
                    yield return LegacyVisibleClick07(Button("Product_" + quote.offerId));
                    yield return Art073Settle(.18f);
                    ReadAppendix(run, def.id, "body", FindActive<RectTransform>("Dialog"), "SkillBody",
                        CombinedAppendix(def.shortText, def.details));
                    Click("CloseDialog");
                }
            }
            finally { SaveAppendix(run); }
            FinishAppendix(run);
        }
    }
}
