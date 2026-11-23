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
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W14 (docs/specs/v11/SHOP_LAYOUT.md): the shop page of art proposal A3. Captures and reports go to
    /// artifacts/cache/v11/w14/&lt;width&gt;x&lt;height&gt;.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private string W14Output
        {
            get
            {
                string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w14",Screen.width+"x"+Screen.height));
                Directory.CreateDirectory(dir);return dir;
            }
        }

        /// <summary>
        /// E1 (SHOP_LAYOUT 12.4): with the 60-unit price button, every card of the configuration keeps its short text
        /// complete (TX-03) on a Shop-size card of <see cref="GameApp.ShopCardHeight14"/>, with the plain price button and
        /// with the longest reason (卡牌已满 10 张), in both themes at 100 % and 150 %. The report gives, per theme and text
        /// size, the shortest height that would still keep every short text complete, the smallest picture at the chosen
        /// height and, at 100 %, any card whose name had to shrink (it never may there, CARD_TEMPLATE 9 row 11).
        /// </summary>
        [UnityTest] public IEnumerator PLAY_V14_E1_ShopCardHeightKeepsEveryShortTextComplete()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Run at actual 1920x1080 or 1280x720.");
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();var failures=new List<string>();
            string longest="卡牌已满 "+app.Config.maxControllerInstances+" 张";
            var removed=new HashSet<string>(app.Config.removedControllerIds??new string[0]);
            var ids=app.Config.controllers.Select(c=>c.id).Where(id=>!removed.Contains(id)).ToArray();
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);GameApp.CleanCapture11=true;
                StartRun("71401");
                foreach(string theme in new[]{"default","swiss","sancai","comic","deco","glass"})
                {
                    app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));
                    foreach(int percent in new[]{100,150})
                    {
                        W11TextScale(percent);yield return null;
                        float height=app.ShopCardHeight14,needed=0,smallestArt=float.MaxValue;string neededBy="",smallestBy="";int below72=0,cards=0;
                        foreach(string id in ids)foreach(string reason in new[]{null,longest})
                        {
                            cards++;string at=theme+" "+percent+"% "+id+" "+(reason??"购买");
                            var fit=app.MeasureShopCard14(id,reason,height);
                            if(!fit.complete)failures.Add(at+": short text ends at "+fit.textBottom.ToString("0.#",CultureInfo.InvariantCulture)+", price button starts at "+fit.priceTop.ToString("0.#",CultureInfo.InvariantCulture)+" on a card "+height+" high");
                            if(percent==100&&fit.namePx<22*UiKit.FontScale-.01f)failures.Add(at+": the name shrank to "+fit.namePx+" at 100 %");
                            if(fit.art<smallestArt){smallestArt=fit.art;smallestBy=id+" "+(reason??"购买");}
                            if(fit.art<72-.01f)below72++;
                            // The shortest height that still keeps this short text complete (2-unit resolution).
                            float lo=240,hi=height;
                            if(fit.complete)
                            {
                                while(hi-lo>2){float mid=Mathf.Round((lo+hi)*.25f)*2;if(app.MeasureShopCard14(id,reason,mid).complete)hi=mid;else lo=mid;}
                                if(hi>needed){needed=hi;neededBy=id+" "+(reason??"购买");}
                            }
                        }
                        report.Add(theme+" text "+percent+"%: card 220x"+height+", "+cards+" cards; shortest height keeping every short text complete "+needed+" ("+neededBy+"); smallest picture "+smallestArt.ToString("0.#",CultureInfo.InvariantCulture)+" ("+smallestBy+"); pictures under 72: "+below72);
                        yield return null;
                    }
                    W11TextScale(100);
                }
            }
            finally
            {
                app.HideCardGallery13();W11TextScale(100);GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                File.WriteAllLines(Path.Combine(W14Output,"e1-shop-card-heights.txt"),report.Concat(new[]{"","PROBLEMS "+failures.Count}).Concat(failures));
            }
            foreach(string line in report)TestContext.WriteLine(line);
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(20)));
        }

        // ---------------------------------------------------------------- fixtures
        /// <summary>The common shop (four cards, two items, the generated goods) or the full one of SH-01: six cards, five
        /// items, two hand upgrades, two services, two new dice, two face upgrades, ten equipped cards (three slotless), eight
        /// dice and five items held.</summary>
        private RunState W14ShopFixture(bool full)
        {
            var fixture=ShopBoundaryFixture();
            Assert.That(fixture.shop.offers.Count(o=>o.category=="controller"),Is.EqualTo(app.Config.shop.initialControllerOffers));
            if(!full)return fixture;
            fixture.controllerOfferPurchases=app.Config.shop.maxControllerOffers-app.Config.shop.initialControllerOffers;
            fixture.consumableOfferPurchases=app.Config.shop.maxConsumableOffers-app.Config.shop.initialConsumableOffers;
            var offered=new HashSet<string>(fixture.shop.offers.Select(o=>o.contentId));int added=0;
            foreach(var definition in app.Config.controllers.Where(c=>!offered.Contains(c.id)&&!(app.Config.removedControllerIds??new string[0]).Contains(c.id)))
            {if(fixture.shop.offers.Count(o=>o.category=="controller")>=app.Config.shop.maxControllerOffers)break;AddBoundaryOffer(fixture,"w14-card-"+added++,"controller",definition.id);}
            foreach(var definition in app.Config.consumables.Where(c=>!offered.Contains(c.id)))
            {if(fixture.shop.offers.Count(o=>o.category=="consumable")>=app.Config.shop.maxConsumableOffers)break;AddBoundaryOffer(fixture,"w14-item-"+added++,"consumable",definition.id);}
            fixture.controllers.Clear();
            string[] held={"C02","C06","C13","C42","C55","C58","C01","C45","C51","C52"};
            for(int i=0;i<held.Length;i++){var card=BoundaryController(fixture,held[i],i>=7?"slotless":i==1?"mult2":"none");if(held[i]=="C45")card.state.activeColor="blue";fixture.controllers.Add(card);}
            fixture.controllerSlots=app.Config.maxOrdinaryControllerSlots;fixture.consumableSlots=app.Config.maxConsumableSlots;
            while(fixture.dice.Count<app.Config.maxPermanentDice)fixture.dice.Add(DiceFactory.CreateStandard(app.Config,fixture.nextDieSerial++));
            fixture.inventory.Clear();foreach(var item in app.Config.consumables.Take(app.Config.maxConsumableSlots))BoundaryItem(fixture,item.id);
            foreach(string category in new[]{"hand_upgrade","service","new_die","die_upgrade"})
                Assert.That(fixture.shop.offers.Count(o=>o.category==category||category=="hand_upgrade"&&o.category=="color_upgrade"),Is.EqualTo(2),category);
            return fixture;
        }
        private IEnumerator W14Load(RunState fixture)
        {
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();
            Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));
            yield return Art073Settle(.6f);
        }
        private IEnumerator W14Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try{Assert.That(texture.width,Is.EqualTo(Screen.width));File.WriteAllBytes(Path.Combine(W14Output,name+".png"),texture.EncodeToPNG());}
            finally{UnityEngine.Object.Destroy(texture);}
        }
        private RectTransform W14Main=>(RectTransform)typeof(GameApp).GetField("main",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(app);
        /// <summary>The section rectangles recorded in SHOP_LAYOUT 2 (design units: x, y, w, h).</summary>
        private static Dictionary<string,Rect> W14Expected(bool large)=>large?new Dictionary<string,Rect>
        {
            {"Stats07",new Rect(16,12,1888,72)},{"ControllerShelf",new Rect(16,92,1404,439)},{"ShopProducts",new Rect(16,539,1404,356)},{"Build14",new Rect(16,903,1404,169)},
            {"DiceCounter",new Rect(1432,92,472,603)},{"OwnedStrip",new Rect(1432,703,472,289)},{"LeaveShop",new Rect(1432,1000,472,72)}
        }:new Dictionary<string,Rect>
        {
            {"Stats07",new Rect(16,12,1888,72)},{"ControllerShelf",new Rect(16,92,1404,423)},{"ShopProducts",new Rect(16,523,1404,396)},{"Build14",new Rect(16,927,1404,145)},
            {"DiceCounter",new Rect(1432,92,472,579)},{"OwnedStrip",new Rect(1432,679,472,289)},{"LeaveShop",new Rect(1432,976,472,96)}
        };
        private static Rect W14Design(RectTransform r)=>new Rect(r.anchoredPosition.x,-r.anchoredPosition.y,r.sizeDelta.x,r.sizeDelta.y);
        /// <summary>What a good's price button says: 购买, 已购买 or why it cannot be bought (CARD_TEMPLATE §5).</summary>
        private string PriceWord14(string offerId)=>Button("Buy_"+offerId).transform.Find("PriceWord13").GetComponent<TextMeshProUGUI>().text;
        /// <summary>Q3 A: a good on another page of its section (升级与服务 with enlarged text or more than nine goods; 骰子与骰面
        /// with more than four rows, a debug workspace only) is reached with that section's real page buttons: back to the
        /// first page, then forward until the good shows.</summary>
        private IEnumerator PageToOffer14(string offerId)
        {
            var offer=app.Session.State.shop.offers.SingleOrDefault(o=>o.offerId==offerId);
            if(offer==null||offer.category=="controller"||!string.IsNullOrEmpty(offer.groupId))yield break;
            bool counter=offer.category=="new_die"||offer.category=="die_upgrade";
            string next=counter?"CounterNext14":"ProductsNext14",prev=counter?"CounterPrev14":"ProductsPrev14",row="Product_"+offerId;
            for(int clicks=0;!HasActive(row)&&HasActive(prev)&&Button(prev).interactable&&clicks<16;clicks++){yield return VisibleTargetClick(prev);yield return null;}
            for(int clicks=0;!HasActive(row)&&HasActive(next)&&Button(next).interactable&&clicks<16;clicks++){yield return VisibleTargetClick(next);yield return null;}
        }

        /// <summary>SH-01: sections at their rectangles, inside the screen, not overlapping; goods inside their sections; no
        /// scroll view; at 100 % every good shows, with enlarged text 升级与服务 pages by six. SH-04: exactly one primary-coloured
        /// button (下一关) and no price button in that colour. SH-06: every button and die at least 40 px at 720p (60 units).</summary>
        private void W14CheckLayout(string scope,List<string> failures)
        {
            Canvas.ForceUpdateCanvases();var main=W14Main;bool large=UiKit.FontScale>1.01f;
            Rect screen=new Rect(0,0,1920,1080);
            foreach(var pair in W14Expected(large))
            {
                var r=main.Find(pair.Key) as RectTransform;
                if(r==null||!r.gameObject.activeInHierarchy){failures.Add(scope+": no "+pair.Key);continue;}
                var at=W14Design(r);
                if(Mathf.Abs(at.x-pair.Value.x)>.5f||Mathf.Abs(at.y-pair.Value.y)>.5f||Mathf.Abs(at.width-pair.Value.width)>.5f||Mathf.Abs(at.height-pair.Value.height)>.5f)
                    failures.Add(scope+": "+pair.Key+" at "+at+" instead of "+pair.Value+" (SHOP_LAYOUT 2)");
                if(at.xMin<-.5f||at.yMin<-.5f||at.xMax>screen.xMax+.5f||at.yMax>screen.yMax+.5f)failures.Add(scope+": "+pair.Key+" leaves the screen");
            }
            var names=W14Expected(large).Keys.ToArray();
            for(int a=0;a<names.Length;a++)for(int b=a+1;b<names.Length;b++)
            {
                var ra=main.Find(names[a]) as RectTransform;var rb=main.Find(names[b]) as RectTransform;if(ra==null||rb==null)continue;
                if(Art073Overlap(W14Design(ra),W14Design(rb),.5f))failures.Add(scope+": "+names[a]+" overlaps "+names[b]);
            }
            if(main.GetComponentsInChildren<UnityEngine.UI.ScrollRect>(false).Any(s=>s.gameObject.activeInHierarchy))failures.Add(scope+": a scroll view on the shop page");
            var s=app.Session.State;
            var cards=s.shop.offers.Where(o=>o.category=="controller"&&string.IsNullOrEmpty(o.groupId)).ToArray();
            var shelf=V06ScreenRect((RectTransform)main.Find("ControllerShelf"));
            foreach(var o in cards)
            {
                var card=FindActive<RectTransform>("Offer_"+o.offerId);
                if(!Art073Inside(V06ScreenRect(card),shelf))failures.Add(scope+": Offer_"+o.offerId+" leaves 卡牌");
                if(card.Find("Buy_"+o.offerId)==null)failures.Add(scope+": Offer_"+o.offerId+" has no price button");
            }
            var goods=s.shop.offers.Where(o=>string.IsNullOrEmpty(o.groupId)&&(o.category=="hand_upgrade"||o.category=="color_upgrade"||o.category=="consumable"||o.category=="service")).ToArray();
            var products=(RectTransform)main.Find("ShopProducts");
            int shown=products.Cast<Transform>().Count(t=>t.gameObject.activeSelf&&t.name.StartsWith("Product_",StringComparison.Ordinal));
            int expected=large?Math.Min(6,goods.Length-app.GoodsPage14*6):goods.Length;
            if(shown!=expected)failures.Add(scope+": 升级与服务 shows "+shown+" goods, expected "+expected);
            bool paging=main.GetComponentsInChildren<RectTransform>(false).Any(r=>r.name=="ProductsNext14");
            if(paging!=(large&&goods.Length>6))failures.Add(scope+": paging "+(paging?"shown":"missing")+" for "+goods.Length+" goods at "+(large?"150":"100")+" %");
            foreach(Transform row in products)if(row.gameObject.activeSelf&&row.name.StartsWith("Product_",StringComparison.Ordinal)&&!Art073Inside(V06ScreenRect((RectTransform)row),V06ScreenRect(products)))failures.Add(scope+": "+row.name+" leaves 升级与服务");
            var counter=(RectTransform)main.Find("DiceCounter");
            foreach(Transform row in counter)if(row.gameObject.activeSelf&&row.name.StartsWith("Product_",StringComparison.Ordinal)&&!Art073Inside(V06ScreenRect((RectTransform)row),V06ScreenRect(counter)))failures.Add(scope+": "+row.name+" leaves 骰子与骰面");
            // SH-04 (CT-03, AX-02).
            var primary=main.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.gameObject.activeInHierarchy&&b.targetGraphic!=null&&W11PrimaryColoured(b.targetGraphic.color)).Select(b=>b.name).ToArray();
            bool leave=Button("LeaveShop").interactable;
            if(leave&&!primary.SequenceEqual(new[]{"LeaveShop"}))failures.Add(scope+": primary-coloured buttons "+string.Join(",",primary)+" (expected only LeaveShop)");
            if(!leave&&primary.Length>0)failures.Add(scope+": a disabled 下一关 leaves primary-coloured buttons "+string.Join(",",primary));
            // SH-06: 60 units, 40 px at 720p.
            float minimum=60*app.canvas.scaleFactor-.5f;
            foreach(var b in main.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.gameObject.activeInHierarchy))
            {var r=V06ScreenRect((RectTransform)b.transform);if(r.width<minimum||r.height<minimum)failures.Add(scope+": button "+b.name+" is "+r.width.ToString("0.#")+"x"+r.height.ToString("0.#")+" px (< 60 units)");}
            foreach(var v in main.GetComponentsInChildren<Art073DieView>(false).Where(v=>v.gameObject.activeInHierarchy))
            {var r=V06ScreenRect((RectTransform)v.transform);if(r.width<minimum||r.height<minimum)failures.Add(scope+": die "+v.name+" is "+r.width.ToString("0.#")+"x"+r.height.ToString("0.#")+" px (< 60 units)");}
        }
        /// <summary>SH-02 and SH-03: every text on the page complete, inside its own box and its drawn frame, not covered by an
        /// image drawn after it; every card on the shelf shows its whole short text.</summary>
        private void W14CheckTexts(string scope,List<string> failures,params string[] roots)
        {
            Canvas.ForceUpdateCanvases();var main=W14Main;var order=app.canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(false);
            // While a face is chosen (S1–S3) the rest of the page is under the veil on purpose: only the panel and 我的骰子 are read.
            var under=roots.Length==0?new Transform[]{main}:roots.Select(n=>main.Find(n)).Where(r=>r!=null).ToArray();
            Assert.That(under,Is.Not.Empty,scope);
            foreach(var root in under)foreach(var t in root.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(t.text)))W13GlyphCheck(t,scope,order,failures);
            if(roots.Length>0)return;
            foreach(var o in app.Session.State.shop.offers.Where(o=>o.category=="controller"&&string.IsNullOrEmpty(o.groupId)))
            {
                var body=FindActive<RectTransform>("Offer_"+o.offerId).Find("ShortText11")?.GetComponent<TextMeshProUGUI>();
                if(body==null||!body.gameObject.activeInHierarchy){failures.Add(scope+": Offer_"+o.offerId+" shows no short text (TX-03)");continue;}
                string configured=app.Config.Controller(o.contentId).shortText;
                if(body.text.Replace("\n","")!=configured)failures.Add(scope+": Offer_"+o.offerId+" short text differs from the configuration");
                CheckCardLines21(body,configured,scope,failures);
            }
        }

        [UnityTest] public IEnumerator PLAY_V14_SH01_SH06_ShopPageInBothThemesAtTwoTextSizes()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Run at actual 1920x1080 or 1280x720.");
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();var failures=new List<string>();var contrast=new List<string>();var contrastFailures=new List<string>();
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);GameApp.CleanCapture11=true;
                foreach(bool full in new[]{false,true})
                {
                    yield return W14Load(W14ShopFixture(full));
                    string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
                    foreach(string theme in new[]{"default","swiss"})
                    {
                        app.ApplyTheme11(theme);
                        foreach(int percent in new[]{100,150})
                        {
                            W11TextScale(percent);yield return null;yield return null;
                            string scope=(full?"full":"common")+"-"+theme+"-text"+percent;int before=failures.Count;
                            W14CheckLayout(scope,failures);W14CheckTexts(scope,failures);
                            yield return W14Shot("shop-"+scope);
                            yield return W11bContrastPass(scope,contrast,contrastFailures,W14Main);
                            if(full&&percent==150&&HasActive("ProductsNext14"))
                            {
                                // Q3 A: the second page of 升级与服务.
                                Click("ProductsNext14");yield return null;Assert.That(app.GoodsPage14,Is.EqualTo(1));
                                W14CheckLayout(scope+"-page2",failures);W14CheckTexts(scope+"-page2",failures);
                                yield return W14Shot("shop-"+scope+"-page2");
                                Click("ProductsPrev14");yield return null;
                            }
                            report.Add(scope+": "+(failures.Count-before)+" problems");
                        }
                        W11TextScale(100);
                    }
                    app.ApplyTheme11(GameTheme.DefaultId);
                    if(full)
                    {
                        // R4: frame time on the full shop (eight owned dice and two dice for sale drawn by the interface dice layer).
                        yield return null;var times=new List<double>();double last=Time.realtimeSinceStartupAsDouble;
                        for(int i=0;i<120;i++){yield return null;double now=Time.realtimeSinceStartupAsDouble;times.Add((now-last)*1000);last=now;}
                        times.Sort();
                        report.Add("R4 full shop frame time over 120 frames: median "+times[60].ToString("0.0",CultureInfo.InvariantCulture)+" ms, 95th "+times[113].ToString("0.0",CultureInfo.InvariantCulture)+" ms, max "+times[119].ToString("0.0",CultureInfo.InvariantCulture)+" ms; interface dice "+app.UiDice073.ActiveVisualCount);
                    }
                    Assert.That(StateJson(),Is.EqualTo(state),"Theme, text size and paging are view operations.");CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                }
            }
            finally
            {
                W11TextScale(100);GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                File.WriteAllLines(Path.Combine(W14Output,"sh01-sh06-layout.txt"),report.Concat(new[]{"","PROBLEMS "+failures.Count}).Concat(failures));
                File.WriteAllLines(Path.Combine(W14Output,"sh05-contrast.txt"),contrast.Concat(new[]{"","PROBLEMS "+contrastFailures.Count}).Concat(contrastFailures));
            }
            foreach(string line in report)TestContext.WriteLine(line);
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(30)));
            Assert.That(contrastFailures,Is.Empty,string.Join("\n",contrastFailures.Take(30)));
        }

        /// <summary>SH-04 with the loot (DEV_PLAN 4.4, coordinator's correction): while nothing of the boss loot is taken,
        /// 下一关 is disabled and says 先领取首领战利品 (no primary colour); after the first of three items it is the primary
        /// button again and says the rest is void on leaving; a pending save disables it without a reason.</summary>
        [UnityTest] public IEnumerator PLAY_V14_SH04_NextStageWithLootLeftAndWithAPendingSave()
        {
            var fixture=NewBoundaryFixture("w14-boss-shop");fixture.cycles[0].lootKind="L6";fixture.cycles[0].modifierId="B11";
            foreach(var die in fixture.dice)foreach(var face in die.faces){face.permanentPip=4;face.baseColor="red";}
            var memory=new MemorySaveStore();var session=new GameSession(app.Config,memory,fixture);
            for(int stage=0;stage<3;stage++)
            {
                if(stage>0)CoreShopStep(session,CommandKind.LeaveShop);
                CoreShopStep(session,CommandKind.StartStage);
                var started=session.State;started.stage.target="1";session=new GameSession(app.Config,memory,started);
                CoreShopStep(session,CommandKind.Roll);
                foreach(var die in session.State.dice)Assert.That(session.TogglePending(die.dieId),Is.True);
                CoreShopStep(session,CommandKind.ConfirmLocks);CoreShopStep(session,CommandKind.Submit);CoreShopStep(session,CommandKind.EndStage);CoreShopStep(session,CommandKind.Continue);
            }
            var shop=session.State;Assert.That(shop.phase,Is.EqualTo("Shop"));Assert.That(shop.shop.groups.Single(g=>g.groupId=="LOOT").picks,Is.EqualTo(3));
            shop.gold=50;yield return W14Load(shop);
            Assert.That(app.Session.IsLootPending,Is.True);
            var failures=new List<string>();
            var leave=Button("LootEnterShop15");Assert.That(leave.interactable,Is.False);
            Assert.That(FindActive<TextMeshProUGUI>("LootEnterNote15").text,Is.EqualTo("先领取或折现"));
            Assert.That(HasActive("ControllerShelf")||HasActive("ViewGifts"),Is.False);
            Assert.That(Text10("LootStatus15"),Is.EqualTo("还可以领 3 件"));
            yield return W14Shot("shop-loot-pending");
            // Take the first of three items: the shop unlocks; two are left and void on leaving.
            var item=app.Session.State.shop.offers.First(o=>o.groupId=="LOOT"&&o.category=="consumable");
            Click("Buy_"+item.offerId);yield return WaitForIdle();if(HasActive("CloseDialog"))Click("CloseDialog");yield return Art073Settle(.3f);
            Assert.That(app.Session.IsLootPending,Is.False);
            Assert.That(Text10("LootEnterNote15"),Is.EqualTo("剩下 2 件在商店右上角也能领"));
            Click("LootEnterShop15");leave=Button("LeaveShop");Assert.That(leave.interactable,Is.True);
            var entrance=W14Main.GetComponent<Art07ShopEntrance>();Assert.That(entrance,Is.Not.Null);
            double visibleBy=Time.realtimeSinceStartupAsDouble+5;
            while(entrance.IsPlaying&&Time.realtimeSinceStartupAsDouble<visibleBy)yield return null;
            Assert.That(entrance.IsPlaying,Is.False,"Measure the actual shop and its leave note after the shelf has appeared.");
            Assert.That(FindActive<TextMeshProUGUI>("LeaveShopNote14").text,Is.EqualTo("还有 2 件战利品未领，离开后作废"));
            Assert.That(Button("ViewGifts").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("首领战利品（还可领 2 件）"));
            W14CheckLayout("loot-left",failures);W14CheckTexts("loot-left",failures);
            var contrast=new List<string>();var contrastFailures=new List<string>();
            yield return W11bContrastPass("loot-left",contrast,contrastFailures,W14Main);
            Assert.That(contrastFailures,Is.Empty,string.Join("\n",contrastFailures));
            Assert.That(contrast.Any(line=>line.Contains("LeaveShopNote14")),Is.True,"The note on 下一关 is measured: "+string.Join(" | ",contrast.Where(line=>line.Contains("LeaveShop"))));
            yield return W14Shot("shop-loot-left");
            // A pending save disables 下一关 and gives no reason (as on the play screen).
            string before=GameSession.StateHash(app.Session.State);
            using(new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                var result=app.Send(new SessionCommand{kind=CommandKind.RefreshControllers});Assert.That(result.accepted,Is.False);Assert.That(app.Session.HasPendingSave,Is.True);
                var saving=Button("LeaveShop");Assert.That(saving.interactable,Is.False);
                Assert.That(main_PrimaryCount(),Is.Zero,"A disabled 下一关 is not primary-coloured.");
                Assert.That(HasActive("LeaveShopNote14")?FindActive<TextMeshProUGUI>("LeaveShopNote14").text:"",Does.Not.Contain("先领取"),"A pending save gives no loot reason.");
                Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(before));
            }
            Click("RetrySave");Assert.That(app.Session.HasPendingSave,Is.False);yield return TransactionDone07();
            Assert.That(failures,Is.Empty,string.Join("\n",failures));
        }
        private int main_PrimaryCount()=>W14Main.GetComponentsInChildren<UnityEngine.UI.Button>(false).Count(b=>b.gameObject.activeInHierarchy&&b.targetGraphic!=null&&W11PrimaryColoured(b.targetGraphic.color));

        /// <summary>No face or die id on a player screen (TX-02): D001, F01 and the like.</summary>
        private void W14AssertNoIds(string scope)
        {
            var overlay=(Transform)typeof(GameApp).GetField("overlay",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).GetValue(app);
            foreach(var root in new[]{(Transform)W14Main,overlay})
                foreach(var t in root.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.gameObject.activeInHierarchy))
                    Assert.That(System.Text.RegularExpressions.Regex.IsMatch(t.text,@"\b[DF]0\d\d?\b"),Is.False,scope+": "+t.name+" shows an id: "+t.text);
        }

        /// <summary>SH-08 (SHOP_LAYOUT 7): the face pick on the table. Every step and every cancel in both themes; cancelling
        /// never spends, saves or draws; the confirmation sends one command. Face class U06, Die class U05 and O04.</summary>
        [UnityTest] public IEnumerator PLAY_V14_SH08_FacePickOnTheTableStepByStep()
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var failures=new List<string>();
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);GameApp.CleanCapture11=true;
                foreach(string theme in new[]{"default","swiss"})
                {
                    var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();
                    AddBoundaryOffer(fixture,"pick-gold","die_upgrade","U06");AddBoundaryOffer(fixture,"pick-glass","die_upgrade","U05");AddBoundaryOffer(fixture,"pick-remove","service","O04");
                    yield return W14Load(fixture);app.ApplyTheme11(theme);yield return null;
                    var dice=app.Session.State.dice.OrderBy(d=>d.createdSerial).ToArray();
                    string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence,gold=app.Session.State.gold;
                    void Unchanged(string step){Assert.That(StateJson(),Is.EqualTo(state),theme+" "+step+": the state is unchanged");CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation),theme+" "+step+": the save is unchanged");Assert.That(app.Session.ActionSequence,Is.EqualTo(seq));}
                    Assert.That(app.FacePickStep14,Is.EqualTo("S0"));
                    // S1 from the price button; cancel.
                    Click("Buy_pick-gold");yield return null;
                    Assert.That(app.FacePickStep14,Is.EqualTo("S1"));Assert.That(HasActive("FacePickVeil14"),Is.True);Assert.That(HasActive("ShopTargetList"),Is.False,"No dice sheet in the shop.");
                    Assert.That(HasActive("DialogTitle"),Is.False,"The face pick is not a modal.");
                    foreach(var d in dice)Assert.That(Button("Target_"+d.dieId).interactable,Is.True,d.dieId);
                    Assert.That(FindActive<TextMeshProUGUI>("FacePickPrompt14").text.Replace("\n",""),Does.StartWith("选一颗骰子，再选要改的面"));
                    W14AssertNoIds(theme+" S1");Unchanged("S1");
                    foreach(int percent in new[]{100,150}){W11TextScale(percent);yield return null;yield return W14Shot("pick-S1-"+theme+"-text"+percent);W14CheckTexts(theme+"-S1-"+percent,failures,"FacePick14","OwnedStrip");W14ButtonsInside("FacePick14",theme+"-S1-"+percent,failures);}
                    W11TextScale(100);
                    Click("CancelFacePick14");yield return null;Assert.That(app.FacePickStep14,Is.EqualTo("S0"));Assert.That(HasActive("FacePickVeil14"),Is.False);Unchanged("cancel at S1");
                    // S1 from the card itself, then S2; change the die; cancel.
                    Click("Product_pick-gold");yield return null;Assert.That(app.FacePickStep14,Is.EqualTo("S1"));
                    Click("Target_"+dice[0].dieId);yield return null;
                    Assert.That(app.FacePickStep14,Is.EqualTo("S2"));Assert.That(app.FacePickDie14,Is.EqualTo(dice[0].dieId));
                    Assert.That(FindActive<Art073DieView>("FacePickDie14"),Is.Not.Null,"The chosen die is the real die.");
                    for(int f=1;f<=6;f++)Assert.That(Button("Face_F0"+f).interactable,Is.True,"face "+f);
                    Assert.That(FindActive<TextMeshProUGUI>("FacePickDieLabel14").text,Does.StartWith("第 1 颗 · "));
                    W14AssertNoIds(theme+" S2");Unchanged("S2");
                    foreach(int percent in new[]{100,150}){W11TextScale(percent);yield return null;yield return W14Shot("pick-S2-"+theme+"-text"+percent);W14CheckTexts(theme+"-S2-"+percent,failures,"FacePick14","OwnedStrip");W14ButtonsInside("FacePick14",theme+"-S2-"+percent,failures);}
                    W11TextScale(100);
                    Click("ChangeDie14");yield return null;Assert.That(app.FacePickStep14,Is.EqualTo("S1"));
                    Click("Target_"+dice[1].dieId);yield return null;Assert.That(app.FacePickDie14,Is.EqualTo(dice[1].dieId));
                    Click("CancelFacePick14");yield return null;Assert.That(app.FacePickStep14,Is.EqualTo("S0"));Unchanged("cancel at S2");
                    // S3; change the face; cancel.
                    Click("Buy_pick-gold");Click("Target_"+dice[0].dieId);Click("Face_F02");yield return null;
                    Assert.That(app.FacePickStep14,Is.EqualTo("S3"));Assert.That(app.FacePickFace14,Is.EqualTo("F02"));
                    var f2=dice[0].faces.First(x=>x.faceId=="F02");
                    string faceText=f2.permanentPip+" 点"+(f2.baseColor=="red"?"红":"蓝")+"面";
                    Assert.That(FindActive<TextMeshProUGUI>("PurchasePreviewBody").text.Replace("\n",""),Does.StartWith(faceText).And.EndWith("（黄金）"));
                    Assert.That(FindActive<TextMeshProUGUI>("FacePickNote14").text.Replace("\n",""),Is.EqualTo("朝上提交时这颗计分 ×2"));
                    Assert.That(FindActive<TextMeshProUGUI>("FacePickPrice14").text.Replace("\n",""),Is.EqualTo("价格 "+app.Config.Upgrade("U06").buyPrice+"（你有 "+PlayerNumberFormat.Gold(gold)+"）"));
                    Assert.That(Button("ConfirmPurchase").interactable,Is.True);Assert.That(W11PrimaryColoured(Button("ConfirmPurchase").targetGraphic.color),Is.False,"Second-level style.");
                    W14AssertNoIds(theme+" S3");Unchanged("S3");
                    foreach(int percent in new[]{100,150}){W11TextScale(percent);yield return null;yield return W14Shot("pick-S3-"+theme+"-text"+percent);W14CheckTexts(theme+"-S3-"+percent,failures,"FacePick14","OwnedStrip");W14ButtonsInside("FacePick14",theme+"-S3-"+percent,failures);}
                    W11TextScale(100);
                    Click("ChangeFace14");yield return null;Assert.That(app.FacePickStep14,Is.EqualTo("S2"));
                    Click("Face_F02");yield return null;Assert.That(app.FacePickStep14,Is.EqualTo("S3"));
                    Click("CancelFacePick14");yield return null;Assert.That(app.FacePickStep14,Is.EqualTo("S0"));Unchanged("cancel at S3");
                    // S3 → confirm: one command, the price paid, the face gilded (S4 plays the existing receipt).
                    Click("Buy_pick-gold");Click("Target_"+dice[0].dieId);Click("Face_F02");yield return null;
                    Click("ConfirmPurchase");
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));Assert.That(app.Session.State.processedReceipts.Last().command.kind,Is.EqualTo(CommandKind.Buy));
                    Assert.That(app.Session.State.gold,Is.EqualTo(gold-app.Config.Upgrade("U06").buyPrice));
                    Assert.That(app.Session.State.dice.Single(d=>d.dieId==dice[0].dieId).faces.Single(x=>x.faceId=="F02").isGold,Is.True);
                    Assert.That(app.TransactionVisualKind07,Is.EqualTo("die_upgrade"));Assert.That(app.FacePickStep14,Is.EqualTo("S0"));
                    yield return TransactionDone07();yield return null;
                    Assert.That(Button("Buy_pick-gold").interactable,Is.False);Assert.That(Button("Buy_pick-gold").transform.Find("PriceWord13").GetComponent<TextMeshProUGUI>().text,Is.EqualTo("已购买"));
                    // Die class: U05 goes from the die straight to S3.
                    seq=app.Session.ActionSequence;gold=app.Session.State.gold;
                    Click("Buy_pick-glass");Click("Target_"+dice[1].dieId);yield return null;
                    Assert.That(app.FacePickStep14,Is.EqualTo("S3"));Assert.That(HasActive("Face_F01"),Is.False,"A whole-die upgrade has no faces to choose.");
                    Assert.That(FindActive<TextMeshProUGUI>("PurchasePreviewBody").text.Replace("\n",""),Does.StartWith("第 2 颗 · ").And.EndWith(" → 玻璃骰"));
                    Assert.That(FindActive<TextMeshProUGUI>("FacePickNote14").text.Replace("\n",""),Is.EqualTo("计分 ×3；每次掷到它有 "+app.Session.GlassBreakPercentIn(app.Session.State)+"% 概率碎掉，锁定后不会碎"));
                    Click("ChangeDie14");yield return null;Assert.That(app.FacePickStep14,Is.EqualTo("S1"));
                    Click("Target_"+dice[1].dieId);Click("ConfirmPurchase");
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));Assert.That(app.Session.State.dice.Single(d=>d.dieId==dice[1].dieId).isGlass,Is.True);
                    yield return TransactionDone07();
                    // O04: S3 says what is deleted; confirming removes that die only.
                    seq=app.Session.ActionSequence;int count=app.Session.State.dice.Count;
                    Click("Buy_pick-remove");Click("Target_"+dice[2].dieId);yield return null;
                    Assert.That(app.FacePickStep14,Is.EqualTo("S3"));
                    Assert.That(FindActive<TextMeshProUGUI>("PurchasePreviewBody").text.Replace("\n",""),Does.StartWith("删除 第 3 颗 · "));
                    Assert.That(FindActive<TextMeshProUGUI>("FacePickNote14").text.Replace("\n",""),Is.EqualTo("连同它的改造一起删除；提交栏仍是 "+app.Session.EffectiveCapacity+" 格"));
                    Assert.That(Button("ConfirmPurchase").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("确认删除"));
                    Click("ConfirmPurchase");
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));Assert.That(app.Session.State.dice.Count,Is.EqualTo(count-1));Assert.That(app.Session.State.dice.Any(d=>d.dieId==dice[2].dieId),Is.False);
                    yield return TransactionDone07();
                }
            }
            finally
            {
                W11TextScale(100);GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
            }
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(20)));
        }

        /// <summary>SH-08: an illegal target is named and blocks the confirmation (a gilded face for U06), and Esc-like
        /// cancelling from the app (CancelFacePick14) returns to S0.</summary>
        [UnityTest] public IEnumerator PLAY_V14_SH08_IllegalTargetIsNamedAndBlocksTheConfirmation()
        {
            var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();AddBoundaryOffer(fixture,"pick-gold","die_upgrade","U06");
            fixture.dice[0].faces.First(f=>f.faceId=="F03").isGold=true;
            yield return W14Load(fixture);string state=StateJson();
            Click("Buy_pick-gold");Click("Target_"+fixture.dice[0].dieId);Click("Face_F03");yield return null;
            Assert.That(app.FacePickStep14,Is.EqualTo("S3"));
            Assert.That(FindActive<TextMeshProUGUI>("PurchaseUnavailable").text.Replace("\n",""),Is.EqualTo("这一面已经镀金，换一个面"));
            Assert.That(Button("ConfirmPurchase").interactable,Is.False);
            app.CancelFacePick14();yield return null;Assert.That(app.FacePickStep14,Is.EqualTo("S0"));Assert.That(StateJson(),Is.EqualTo(state));
        }

        /// <summary>SH-08: every button of a panel lies inside it (the face-pick panel's row of buttons with enlarged text).</summary>
        private void W14ButtonsInside(string root,string scope,List<string> failures)
        {
            var panel=FindActive<RectTransform>(root);Rect area=V06ScreenRect(panel);
            foreach(var b in panel.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.gameObject.activeInHierarchy))
            {
                Rect r=V06ScreenRect((RectTransform)b.transform);
                if(r.xMin<area.xMin-.5f||r.xMax>area.xMax+.5f||r.yMin<area.yMin-.5f||r.yMax>area.yMax+.5f)failures.Add(scope+": "+b.name+" "+r+" leaves "+root+" "+area);
            }
        }

        /// <summary>SH-08 with the longest S3 texts: a face with a texture, gold and a weight, so that U01's change and its
        /// note on the opposite face, and U11's reason, are written out in full; both styles at 100 % and 150 %. Every text is
        /// whole and clear of what is drawn after it (the buttons), and every button stays inside the panel.</summary>
        [UnityTest] public IEnumerator PLAY_V14_SH08_LongestChangeTextsFitWithEnlargedText()
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var failures=new List<string>();
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);GameApp.CleanCapture11=true;
                var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();
                AddBoundaryOffer(fixture,"long-smooth","die_upgrade","U01");AddBoundaryOffer(fixture,"long-mirror","die_upgrade","U11");
                var die=fixture.dice.OrderBy(d=>d.createdSerial).First();
                foreach(var f in die.faces){f.texture="rough";f.isGold=true;f.baseWeight=2;}
                yield return W14Load(fixture);string state=StateJson();
                foreach(string theme in new[]{"default","swiss"})
                {
                    app.ApplyTheme11(theme);
                    foreach(string offer in new[]{"long-smooth","long-mirror"})
                        foreach(int percent in new[]{100,150})
                        {
                            W11TextScale(percent);yield return null;
                            Click("Buy_"+offer);Click("Target_"+die.dieId);Click("Face_F02");yield return null;
                            Assert.That(app.FacePickStep14,Is.EqualTo("S3"),offer);
                            string scope=theme+"-"+offer+"-text"+percent;
                            W14CheckTexts(scope,failures,"FacePick14","OwnedStrip");W14ButtonsInside("FacePick14",scope,failures);
                            yield return W14Shot("pick-S3-long-"+scope);
                            Click("CancelFacePick14");yield return null;Assert.That(StateJson(),Is.EqualTo(state));
                        }
                    W11TextScale(100);
                }
            }
            finally
            {
                W11TextScale(100);GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
            }
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(20)));
        }

        /// <summary>SH-10 (D5, C98): three 砍价高手 bring a card of list price 4 to 1; bought here, it sells for 1 and says
        /// why, on the tile's hover and in its details; selling gives +1.</summary>
        [UnityTest] public IEnumerator PLAY_V14_SH10_SameShopSaleCapIsShownOnTheTileAndInTheDetails()
        {
            var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();Equip09(fixture,"C98","C98","C98");
            AddBoundaryOffer(fixture,"C52","controller","C52");
            yield return W14Load(fixture);
            Assert.That(app.Config.Controller("C52").buyPrice,Is.EqualTo(4));
            Click("Buy_C52");yield return WaitForIdle();yield return TransactionDone07();
            var bought=app.Session.State.controllers.Single(c=>c.definitionId=="C52");
            Assert.That(app.Session.State.shop.paidControllerPrices.Single(p=>p.instanceId==bought.instanceId).paid,Is.EqualTo(1));
            Assert.That(app.Session.GetControllerSalePrice(bought.instanceId),Is.EqualTo(1));
            var tile=Button("Controller_"+bought.instanceId);var hover=tile.GetComponent<Art07HoverTip>();Assert.That(hover,Is.Not.Null);
            hover.show();var tip=FindActive<RectTransform>("ContentTooltip");
            Assert.That(tip.Find("State").GetComponent<TextMeshProUGUI>().text,Does.Contain("卖价 1（本店买入，不超过买价 1，离店后恢复）"));
            hover.hide();yield return W14Shot("sh10-c98");
            Click("Controller_"+bought.instanceId);
            Assert.That(FindActive<TextMeshProUGUI>("PriceBody").text,Does.StartWith("卖价 1（本店买入，不超过买价 1，离店后恢复）。"));
            Assert.That(Button("SellController").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("卖出 +1 金币"));
            long gold=app.Session.State.gold;Click("SellController");Click("ConfirmSale");yield return WaitForIdle();
            Assert.That(app.Session.State.gold,Is.EqualTo(gold+1));Assert.That(app.Session.State.controllers.Any(c=>c.instanceId==bought.instanceId),Is.False);
        }

        /// <summary>SH-12 (R5): the top bar's next stage includes the boss modifier, the number 查看本章 shows.</summary>
        [UnityTest] public IEnumerator PLAY_V14_SH12_TopBarTargetIncludesTheBossModifier()
        {
            const string modifier="B11";
            var fixture=NewBoundaryFixture("w14-boss-next");fixture.cycles[0].modifierId=modifier;
            foreach(var die in fixture.dice)foreach(var face in die.faces){face.permanentPip=4;face.baseColor="red";}
            var memory=new MemorySaveStore();var session=new GameSession(app.Config,memory,fixture);
            for(int stage=0;stage<2;stage++)
            {
                if(stage>0)CoreShopStep(session,CommandKind.LeaveShop);
                CoreShopStep(session,CommandKind.StartStage);
                var started=session.State;started.stage.target="1";session=new GameSession(app.Config,memory,started);
                CoreShopStep(session,CommandKind.Roll);foreach(var die in session.State.dice)Assert.That(session.TogglePending(die.dieId),Is.True);
                CoreShopStep(session,CommandKind.ConfirmLocks);CoreShopStep(session,CommandKind.Submit);CoreShopStep(session,CommandKind.EndStage);CoreShopStep(session,CommandKind.Continue);
            }
            yield return W14Load(session.State);
            string scaled=PlayerNumberFormat.Exact(ExactScoreMath.SaveDecimal(ExactScoreMath.Parse(app.Config.stages[2].normalTarget)*app.Config.Modifier(modifier).amount/100));
            Assert.That(scaled,Is.Not.EqualTo(PlayerNumberFormat.Exact(app.Config.stages[2].normalTarget)));
            Assert.That(FindActive<TextMeshProUGUI>("ShopTitle").text,Is.EqualTo("商店 · 第 3 关前"));
            Assert.That(FindActive<TextMeshProUGUI>("NextStage14").text.Replace("\n"," "),Is.EqualTo("下一关：第 3 关 · 首领 · 目标 "+scaled));
            Assert.That(HasActive("BossMark14"),Is.True);
            Click("ViewCycle");
            Assert.That(FindActive<RectTransform>("CycleCard2").Find("Goal").GetComponent<TextMeshProUGUI>().text,Is.EqualTo(scaled),"查看本章 shows the same number.");
            Click("CloseCycle");
        }
    }
}
