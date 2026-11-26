using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private static readonly string[] LootStyles24={"default","swiss","sancai","comic","deco","glass"};
        private string LootFolder24 => Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w15",
            "loot-coverage",Screen.width+"x"+Screen.height));

        private RunState LootShop24(string kind,bool gifts=false,bool cashOnly=false)
        {
            var receipt=BeforeBossLoot15(kind,gifts);
            receipt.consumableSlots=app.Config.maxConsumableSlots;
            if(cashOnly)
                while(receipt.dice.Count<app.Config.maxPermanentDice)receipt.dice.Add(DiceFactory.CreateStandard(app.Config,receipt.nextDieSerial++));
            var session=new GameSession(app.Config,new MemorySaveStore(),receipt);
            CoreShopStep(session,CommandKind.Continue);
            return session.State;
        }

        private static ShopOffer[] GroupOffers24(RunState s,string group="LOOT") =>
            s.shop.offers.Where(o=>o.groupId==group&&o.category!="gold").ToArray();

        // Seed the identity of a quoted face product without changing the number of quotes or the group's other fields.
        private ShopOffer UpgradeQuote24(RunState shop,string group,string upgrade)
        {
            var offers=GroupOffers24(shop,group);
            var existing=offers.FirstOrDefault(o=>o.contentId==upgrade);
            if(existing!=null)return existing;
            var offer=offers.First(o=>o.category=="die_upgrade");
            offer.contentId=upgrade;
            offer.basePrice=app.Config.Upgrade(upgrade).buyPrice;
            return offer;
        }

        private IEnumerator LootShot24(string name)
        {
            Directory.CreateDirectory(LootFolder24);
            yield return new WaitForEndOfFrame();
            var shot=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Path.Combine(LootFolder24,name+".png"),shot.EncodeToPNG());}
            finally{UnityEngine.Object.Destroy(shot);}
        }

        private void ChooseLootDie24(ShopOffer o)
        {
            bool whole=o.contentId=="U05";
            var die=app.Session.State.dice.First(d=>d.lifetime=="permanent"&&d.faces.Any(f=>app.Session.IsUpgradeTargetLegal(o.contentId,d.dieId,whole?null:f.faceId)));
            Click("Target_"+die.dieId);
        }
        private void ChooseLootFace24(ShopOffer o)
        {
            var die=app.Session.State.dice.First(d=>d.dieId==app.FacePickDie14);
            Click("Face_"+die.faces.First(f=>app.Session.IsUpgradeTargetLegal(o.contentId,die.dieId,f.faceId)).faceId);
        }

        [UnityTest] public IEnumerator PLAY_V15_SE05_ClaimInFlightAndRetryKeepEnterShopDisabled()
        {
            yield return W14Load(LootShop24("L6"));
            FinalOptions07("Instant",false);
            var quotes=GroupOffers24(app.Session.State);
            Click("Buy_"+quotes[0].offerId);
            yield return WaitForIdle();
            Assert.That(Button("LootEnterShop15").interactable,Is.True);
            Assert.That(main_PrimaryCount(),Is.EqualTo(1));
            string before=StateJson();
            byte[] checkpoint=File.ReadAllBytes(app.SaveLocation);
            long seq=app.Session.ActionSequence;
            int inventory=app.Session.State.inventory.Count;
            app.Session.Busy=true;
            try
            {
                app.Refresh();
                Assert.That(Button("LootEnterShop15").interactable,Is.False);
                Assert.That(main_PrimaryCount(),Is.Zero);
                Assert.That(StateJson(),Is.EqualTo(before));
            }
            finally {app.Session.Busy=false;app.Refresh();}
            using(new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                Click("Buy_"+quotes[1].offerId);
                Assert.That(app.Session.HasPendingSave,Is.True);
                Assert.That(Button("LootEnterShop15").interactable,Is.False);
                Assert.That(main_PrimaryCount(),Is.Zero);
                Assert.That(StateJson(),Is.EqualTo(before));
                CollectionAssert.AreEqual(checkpoint,File.ReadAllBytes(app.SaveLocation));
            }
            Click("RetrySave");
            yield return WaitForIdle();
            Assert.That(app.Session.HasPendingSave,Is.False);
            Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));
            var after=app.Session.State;
            Assert.That(after.inventory.Count,Is.EqualTo(inventory+1));
            Assert.That(GroupOffers24(after).Count(o=>o.sold),Is.EqualTo(2));
            Assert.That(Button("LootEnterShop15").interactable,Is.True);
            Assert.That(main_PrimaryCount(),Is.EqualTo(1));
            var saved=new AtomicSaveStore(app.SaveLocation,app.Config,SaveSlotPolicy.Debug).Load();
            Assert.That(GameSession.StateHash(saved),Is.EqualTo(GameSession.StateHash(after)));
        }

        [UnityTest] public IEnumerator PLAY_V15_SE09_FullInventorySaleMakesTheSamePendingOfferClaimable()
        {
            foreach(string kind in new[]{"L1","L2","L6"})
            {
                var fixture=LootShop24(kind);
                string owned;
                if(kind=="L6")
                {
                    while(fixture.inventory.Count<fixture.consumableSlots)BoundaryItem(fixture,"I01");
                    owned=fixture.inventory[0].instanceId;
                }
                else
                {
                    while(ControllerRules.OrdinaryUsed(fixture.controllers,app.Config)<fixture.controllerSlots)
                        fixture.controllers.Add(BoundaryController(fixture,"C02"));
                    if(kind=="L2")
                        while(fixture.controllers.Count<app.Config.maxControllerInstances)
                            fixture.controllers.Add(BoundaryController(fixture,"C02","slotless"));
                    owned=fixture.controllers[0].instanceId;
                }
                yield return W14Load(fixture);FinalOptions07("Instant",false);
                var quote=GroupOffers24(app.Session.State).First(o=>!Button("Buy_"+o.offerId).interactable);
                Assert.That(Button("Buy_"+quote.offerId).transform.Find("Label").GetComponent<TextMeshProUGUI>().text,Does.Contain("满"));
                Assert.That(HasActive("LootRoomHint15"),Is.True);
                long gold=app.Session.State.gold,seq=app.Session.ActionSequence;
                long price=kind=="L6"?1:app.Session.GetControllerSalePrice(owned);
                if(kind=="L6"){Click("ShopItem_"+owned);Click("Sell");}
                else
                {
                    Click("Controller_"+owned);Click("SellController");
                    Assert.That(Button("ConfirmSale").transform.Find("Label").GetComponent<TextMeshProUGUI>().text,Does.Contain(price.ToString()));
                    Click("ConfirmSale");
                }
                yield return WaitForIdle();
                Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));Assert.That(app.Session.State.gold,Is.EqualTo(gold+price));
                Assert.That(app.LootPage15&&app.Session.IsLootPending,Is.True);
                Assert.That(Button("Buy_"+quote.offerId).interactable,Is.True);
                Click("Buy_"+quote.offerId);yield return WaitForIdle();
                Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+2));Assert.That(app.Session.State.gold,Is.EqualTo(gold+price));
                var after=app.Session.State;
                Assert.That(after.shop.offers.Single(o=>o.offerId==quote.offerId).sold,Is.True);
                if(kind=="L6")Assert.That(after.inventory.Any(i=>i.instanceId==owned),Is.False);
                else
                {
                    Assert.That(after.controllers.Any(c=>c.instanceId==owned),Is.False);
                    Assert.That(after.controllers.Any(c=>c.instanceId==quote.controller.instanceId),Is.True);
                    Assert.That(after.shop.paidControllerPrices.Any(p=>p.instanceId==quote.controller.instanceId),Is.False);
                }
                string saved=GameSession.StateHash(after);
                yield return LoadBootstrap();ResumeBoundaryFixture();Assert.That(app.LootPage15,Is.False);
                Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(saved));
                if(kind=="L6")Assert.That(Button("ViewGifts").transform.Find("Label").GetComponent<TextMeshProUGUI>().text,Does.Contain("还可领 2 件"));
            }
        }

        [UnityTest] public IEnumerator PLAY_V15_SE10_FaceAndWholeDieClaimsCancelAndPersistForBothSources()
        {
            foreach(string group in new[]{"LOOT","GIFT_C113"})foreach(string upgrade in new[]{"U06","U05"})
            {
                var shop=LootShop24("L4",group!="LOOT");
                var quote=UpgradeQuote24(shop,group,upgrade);
                yield return W14Load(shop);FinalOptions07("Instant",false);
                if(group!="LOOT")
                {
                    Click("LootCashOut15");yield return WaitForIdle();
                    Click("LootEnterShop15");Click("ViewGifts");Click("GiftTab_GIFT_C113");
                }
                string original=StateJson();byte[] bytes=File.ReadAllBytes(app.SaveLocation);
                bool whole=upgrade=="U05";
                foreach(string step in whole?new[]{"S1","S3"}:new[]{"S1","S2","S3"})
                {
                    Click("Buy_"+quote.offerId);Assert.That(app.FacePickStep14,Is.EqualTo("S1"));
                    if(step!="S1")ChooseLootDie24(quote);
                    if(step=="S3"&&!whole)ChooseLootFace24(quote);
                    Assert.That(app.FacePickStep14,Is.EqualTo(step));W14AssertNoIds(group+" "+upgrade+" "+step);
                    yield return LootShot24("se10-"+group+"-"+upgrade+"-"+step);
                    Click("CancelFacePick14");Assert.That(app.FacePickStep14,Is.EqualTo("S0"));
                    Assert.That(app.LootFromShop15,Is.EqualTo(group!="LOOT"));Assert.That(StateJson(),Is.EqualTo(original));
                    CollectionAssert.AreEqual(bytes,File.ReadAllBytes(app.SaveLocation));
                }
                Click("Buy_"+quote.offerId);ChooseLootDie24(quote);if(!whole)ChooseLootFace24(quote);
                string die=app.FacePickDie14,face=app.FacePickFace14;
                Assert.That(Text10("FacePickPrice14"),Is.EqualTo("免费"));
                Assert.That(Button("ConfirmPurchase").transform.Find("Label").GetComponent<TextMeshProUGUI>().text,Is.EqualTo("确认领取"));
                long seq=app.Session.ActionSequence,gold=app.Session.State.gold;
                Click("ConfirmPurchase");yield return WaitForIdle();
                Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));Assert.That(app.Session.State.gold,Is.EqualTo(gold));
                Assert.That(app.FacePickStep14,Is.EqualTo("S0"));
                var after=app.Session.State;
                Assert.That(after.shop.offers.Single(o=>o.offerId==quote.offerId).sold,Is.True);
                var changed=after.dice.Single(d=>d.dieId==die);
                if(whole)Assert.That(changed.isGlass,Is.True);
                else Assert.That(changed.faces.Single(f=>f.faceId==face).isGold,Is.True);
                var stored=new AtomicSaveStore(app.SaveLocation,app.Config,SaveSlotPolicy.Debug).Load();
                Assert.That(GameSession.StateHash(stored),Is.EqualTo(GameSession.StateHash(after)));
            }
        }

        [UnityTest,Timeout(120000)] public IEnumerator PLAY_V15_SE11_UpgradeFastClockForLootBlacksmithAndPaidShelf()
        {
            foreach(string source in new[]{"LOOT","GIFT_C113","shelf"})
            foreach(string upgrade in new[]{"U06","U05"})
            {
                bool paid=source=="shelf",whole=upgrade=="U05";
                var shop=paid?ShopBoundaryFixture():LootShop24("L4",source=="GIFT_C113");
                ShopOffer quote;
                if(paid)
                {
                    shop.shop.offers.Clear();AddBoundaryOffer(shop,"standard-face-service","die_upgrade",upgrade);
                    quote=shop.shop.offers.Single();
                }
                else quote=UpgradeQuote24(shop,source,upgrade);
                yield return W14Load(shop);FinalOptions07("Instant",false);
                if(source=="GIFT_C113")
                {
                    Click("LootCashOut15");yield return WaitForIdle();Click("LootEnterShop15");Click("ViewGifts");Click("GiftTab_GIFT_C113");
                }
                FinalOptions07("Fast",false);
                yield return new WaitForSecondsRealtime(.31f);yield return ShopEntranceFrame07();
                ShopEntrancePointer07("Buy_"+quote.offerId);ChooseLootDie24(quote);if(!whole)ChooseLootFace24(quote);
                Assert.That(app.FacePickStep14,Is.EqualTo("S3"));
                var die=app.FacePickDie14;var face=app.FacePickFace14;
                long oldGold=app.Session.State.gold,seq=app.Session.ActionSequence;
                long price=app.Session.GetOfferNetPrice(quote);
                yield return ShopEntranceFrame07();ShopEntrancePointer07("ConfirmPurchase");
                Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));
                Assert.That(app.Session.State.gold,Is.EqualTo(oldGold-(paid?price:0)));
                var committed=app.Session.State;var bytes=File.ReadAllBytes(app.SaveLocation);
                var changed=committed.dice.Single(d=>d.dieId==die);
                if(whole)Assert.That(changed.isGlass,Is.True);
                else Assert.That(changed.faces.Single(f=>f.faceId==face).isGold,Is.True);
                var motion=app.canvas.transform.Find("Art07TransactionMotion").GetComponent<DiceCraft.Presentation.Art07TransactionMotion>();
                float expected=paid?.15f:whole?.21f:.18f;
                app.enabled=false;
                try
                {
                    Assert.That(motion.Kind,Is.EqualTo("die_upgrade"));Assert.That(motion.IsPlaying,Is.True);
                    Assert.That(motion.Duration,Is.EqualTo(expected).Within(.0001f),source+" "+upgrade);
                    motion.Tick(expected*.5f,false);
                    Assert.That(app.table.GetFaceOrientation("$die-preview")?.gameObject.activeInHierarchy,Is.True);
                    yield return LootShot24("se11-fast-"+source+"-"+upgrade);
                    motion.Tick(expected*.5f-.001f,false);Assert.That(motion.IsPlaying,Is.True);
                    Assert.That(motion.Elapsed,Is.EqualTo(expected-.001f).Within(.0002f));
                    motion.Tick(.0012f,false);Assert.That(motion.IsPlaying||app.Session.Busy,Is.False);
                    Assert.That(app.FacePickStep14,Is.EqualTo("S0"));Assert.That(app.LootPage15,Is.EqualTo(!paid));
                    Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(GameSession.StateHash(committed)));
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(app.SaveLocation));
                }
                finally {app.enabled=true;}
            }
        }

        private float LootTextFloor24(TextMeshProUGUI text)
        {
            switch(text.name)
            {
                case "Money":return 31.5f;
                case "LootName15":return 32;
                case "LootShort15":return 24*UiKit.FontScale;
                case "LootDetails15":return 19.5f*UiKit.FontScale;
                case "LootCashNote15":return 20*UiKit.FontScale;
                case "Price" when text.transform.parent.name=="LootCashOut15":return 22*UiKit.FontScale;
                case "Label" when text.transform.parent.name=="LootCashOut15":return 20*UiKit.FontScale;
                case "Label" when text.transform.parent.name=="LootEnterShop15":
                    return (app.LootFromShop15||UiKit.FontScale>1.01f?20:34)*UiKit.FontScale;
                default:return 19.5f;
            }
        }

        private IEnumerator CheckLootView24(string name,List<string> report,List<string> failures)
        {
            yield return W11Settle();
            string state=StateJson();
            var disk=File.ReadAllBytes(app.SaveLocation);
            bool large=UiKit.FontScale>1.01f,picking=app.FacePickActive14;
            var layout=W14Expected(large);
            var expected=new Dictionary<string,Rect>{
                {"Stats07",layout["Stats07"]},{"LootChoices15",new Rect(16,92,1404,large?803:827)},
                {picking?"FacePick14":"LootInfo15",layout["DiceCounter"]},{"OwnedStrip",layout["OwnedStrip"]},
                {"Build14",layout["Build14"]},{"LootEnterShop15",layout["LeaveShop"]}
            };
            var bounds=new List<Rect>();
            foreach(var pair in expected)
            {
                RectTransform element=FindActive<RectTransform>(pair.Key);
                Rect at=W14Design(element);
                if(Vector2.Distance(at.position,pair.Value.position)>.6f||Vector2.Distance(at.size,pair.Value.size)>.6f)
                    failures.Add(name+" "+pair.Key+" expected "+pair.Value+" measured "+at);
                if(!Art073Inside(V06ScreenRect(element),new Rect(0,0,Screen.width,Screen.height))||bounds.Any(b=>Art073Overlap(at,b,.5f)))
                    failures.Add(name+" section outside screen or overlapping another section: "+pair.Key);
                bounds.Add(at);
            }
            var list=FindActive<RectTransform>("LootChoices15");
            var cards=list.Cast<Transform>().Where(t=>t.gameObject.activeInHierarchy&&(t.name.StartsWith("Offer_")||t.name.StartsWith("Product_"))).ToArray();
            var cardRects=new List<Rect>();
            foreach(var card in cards)
            {
                var rect=(RectTransform)card;
                if(rect.rect.size!=new Vector2(220,large?364:348)||cardRects.Any(b=>Art073Overlap(b,W14Design(rect),.5f))
                    ||!Art073Inside(V06ScreenRect(rect),V06ScreenRect(list)))failures.Add(name+" wrong shop-size card "+card.name);
                cardRects.Add(W14Design(rect));
                var id=card.name.Substring(card.name.IndexOf('_')+1);
                var offer=app.Session.State.shop.offers.Single(o=>o.offerId==id);
                var body=card.Find("ShortText11")?.GetComponent<TextMeshProUGUI>();
                Assert.That(body,Is.Not.Null,name+" "+id);
                string flat=body.text.Replace("\n","");
                string authored=null;
                switch(offer.category)
                {
                    case "controller":authored=app.Config.Controller(offer.contentId).shortText;break;
                    case "consumable":authored=app.Config.Consumable(offer.contentId).shortText;break;
                    case "die_upgrade":authored=app.Config.Upgrade(offer.contentId).shortText;break;
                    // p1.1.1 (W7a; RULES_DELTA 3.1): a hand upgrade changes a multiplier, a colour upgrade a bonus ("加成 +X → +Y").
                    case "hand_upgrade":Assert.That(flat,Does.Contain("直接升级：Lv.").And.Contain(app.Config.colorDefinitions.Any(d=>d.id==offer.contentId)?"加成 +":"倍率 ×"));break;
                    case "new_die":
                        Assert.That(card.Find("FaceStrip14"),Is.Not.Null);Assert.That(card.GetComponentInChildren<Art073DieView>(),Is.Not.Null);
                        Assert.That(flat,Does.Contain("点数和 "+offer.die.faces.Sum(f=>f.permanentPip)));break;
                }
                if(authored!=null&&flat!=authored)failures.Add(name+" "+id+" short text "+flat+" expected "+authored);
            }

            var panel=picking?FindActive<RectTransform>("FacePick14"):null;
            var dice=FindActive<RectTransform>("OwnedStrip");
            bool Foreground(Transform t)=>!picking||t.IsChildOf(panel)||t.IsChildOf(dice);
            var main=W14Main;var graphics=app.canvas.GetComponentsInChildren<Graphic>(false);
            var visible=main.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.enabled&&Foreground(t.transform)&&!string.IsNullOrWhiteSpace(t.text)).ToArray();
            Assert.That(visible,Is.Not.Empty);
            if(!picking)
            {
                var tiles=FindActive<RectTransform>("Build14").Cast<Transform>().Where(c=>c.gameObject.activeInHierarchy&&c.name.StartsWith("Controller_",StringComparison.Ordinal));
                foreach(var tile in tiles)
                    if(!visible.Any(t=>t.name=="Ordinal"&&t.transform.parent==tile))
                        failures.Add(name+" missing equipped-card sequence number on "+tile.name);
            }
            foreach(var t in visible)
            {
                var glyphs=W13GlyphCheck(t,name,graphics,failures);
                float minimum=LootTextFloor24(t);
                var tile=t.transform.parent;
                if(t.name=="Ordinal"&&tile.parent!=null&&tile.parent.name=="Build14")
                {
                    // CARD_TEMPLATE E3: the sequence number lives in a 20-unit disc. Value badges remain body text.
                    var disc=tile.Find("OrdinalDisc11") as RectTransform;
                    int at=app.Session.State.controllers.FindIndex(c=>"Controller_"+c.instanceId==tile.name);
                    if(at<0||t.text!=(at+1).ToString()||disc==null||!disc.gameObject.activeInHierarchy
                        ||disc.rect.size!=new Vector2(20,20)||glyphs.Any(g=>!Art073Inside(g,V06ScreenRect(disc))))
                        failures.Add(name+" invalid equipped-card sequence marker "+W11bPath(t.transform));
                    minimum=12;
                }
                if(t.fontSize<minimum-.01f)failures.Add(name+" "+W11bPath(t.transform)+" text size "+t.fontSize+" below "+minimum);
                if(t.name=="LootEnterNote15")
                {
                    var title=t.transform.parent.Find("Label").GetComponent<TextMeshProUGUI>();
                    if(W11bGlyphRects(title).Any(a=>glyphs.Any(g=>W13Overlap(a,g))))
                        failures.Add(name+" shop button title overlaps its second line");
                }
            }
            var controls=main.GetComponentsInChildren<Button>(false).Where(b=>Foreground(b.transform)).ToArray();
            foreach(var b in controls)
            {
                Rect hit=V06ScreenRect((RectTransform)b.transform);
                if(hit.width<40-.5f||hit.height<40-.5f)failures.Add(name+" "+W11bPath(b.transform)+" hit "+hit.size+" below 40px");
            }
            foreach(var die in main.GetComponentsInChildren<Art073DieView>(false).Where(d=>Foreground(d.transform)))
            {
                Rect hit=V06ScreenRect((RectTransform)die.transform);
                if(hit.width<40-.5f||hit.height<40-.5f)failures.Add(name+" "+W11bPath(die.transform)+" die hit "+hit.size+" below 40px");
            }
            bool primary=!picking&&!app.LootFromShop15&&!app.Session.IsLootPending&&!app.IsBusy&&!app.Session.HasPendingSave;
            var marked=controls.Where(b=>b.targetGraphic!=null&&W11PrimaryColoured(b.targetGraphic.color)).Select(b=>b.name).ToArray();
            if(!marked.SequenceEqual(primary?new[]{"LootEnterShop15"}:Array.Empty<string>()))
                failures.Add(name+" primary buttons ("+primary+" expected): "+string.Join(",",marked));
            int first=report.Count;
            yield return W11bContrastPass(name,report,failures,main,picking?(Func<Transform,bool>)Foreground:null);
            Assert.That(report.Count,Is.GreaterThan(first),name+" did not sample any real text background");
            foreach(var t in visible)
            {
                var button=t.transform.parent.GetComponent<Button>();
                if(button==null||!button.IsInteractable())continue;
                string key=name+" "+W11bPath(t.transform)+" on ";
                if(!report.Skip(first).Any(line=>line.StartsWith(key,StringComparison.Ordinal)))
                    failures.Add(name+" no captured background for active button text "+W11bPath(t.transform));
            }
            W14AssertNoIds(name);
            report.Add(name+" sections "+bounds.Count+", cards "+cards.Length+", visible text "+visible.Length+", buttons "+controls.Length);
            yield return LootShot24(name);
            Assert.That(StateJson(),Is.EqualTo(state),name);CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation),name);
        }

        private void CheckLootDieTargets15(ShopOffer[] offers)
        {
            Canvas.ForceUpdateCanvases();
            foreach(var offer in offers)
            {
                var card=FindActive<RectTransform>("Product_"+offer.offerId);
                var die=card.GetComponentInChildren<Art073DieView>();
                var cell=(RectTransform)die.transform;
                var claim=Button("Buy_"+offer.offerId);
                Assert.That(claim.IsInteractable(),Is.True,offer.offerId);
                Assert.That(Art073Inside(V06ScreenRect(cell),V06ScreenRect(card)),Is.True,offer.offerId);
                foreach(float x in new[]{-29.5f,29.5f})
                    foreach(float y in new[]{-29.5f,29.5f})
                    {
                        var corner=cell.rect.center+new Vector2(x,y);
                        var point=RectTransformUtility.WorldToScreenPoint(null,cell.TransformPoint(corner));
                        Assert.That(Art071RaycastTarget(point),Is.SameAs(die.gameObject),offer.offerId+" preview "+corner);
                    }
                var claimBox=V06ScreenRect((RectTransform)claim.transform);
                Assert.That(Art073Overlap(V06ScreenRect(cell),claimBox,0),Is.False,offer.offerId);
                Assert.That(Art071RaycastTarget(claimBox.center),Is.SameAs(claim.gameObject),offer.offerId+" claim");
            }
            var first=FindActive<RectTransform>("Product_"+offers[0].offerId).GetComponentInChildren<Art073DieView>();
            var firstCell=(RectTransform)first.transform;
            var edge=RectTransformUtility.WorldToScreenPoint(null,firstCell.TransformPoint(firstCell.rect.center+Vector2.right*29.5f));
            var target=Art071RaycastTarget(edge);
            var pointer=new PointerEventData(EventSystem.current){position=edge,button=PointerEventData.InputButton.Left};
            Assert.That(target,Is.SameAs(first.gameObject));
            ExecuteEvents.Execute(target,pointer,ExecuteEvents.pointerClickHandler);
            Assert.That(HasActive("CandidateFaces"),Is.True,"Clicking the die opens its details without claiming it.");
            Click("CloseDialog");
        }

        [UnityTest] public IEnumerator PLAY_V15_SE13_ThemeDefaultAndMissingUseTheRenderedFallbacks()
        {
            yield return LoadBoundaryFixture(BeforeBossLoot15("L1"));ResumeBoundaryFixture();FinalOptions07("Instant",false);
            string previous=app.ThemeId11,probeId="w15-loot-probe";
            Sprite[] originals=UiKit.Templates,loot=app.lootIllustrations15,boss=app.defaultBossEmblems15;
            var modifier=app.Config.Modifier(app.Session.State.stage.modifierId);
            string family="emblem_"+GameApp.BossEmblemFamily15(modifier.id,modifier.tier),tier="ui_emblem_tier"+modifier.tier;
            string[] icons={"ui_icon_stage","ui_icon_submit","ui_icon_interest"};
            string[] chipNames={"SettlementBaseReward","SettlementUnusedReward","SettlementInterest"};
            var templates=new Dictionary<string,Sprite>();var art=new Dictionary<string,Sprite>();
            var probe=GameTheme.CreateForTests(probeId,new ThemePalette());
            // Simulate a style with and without specific bundled pieces, using distinct real sprites as identity witnesses.
            // Clear loaned art before leaving the theme so its Resources cache never unloads the default atlas.
            Type type=typeof(GameTheme);const BindingFlags fields=BindingFlags.NonPublic|BindingFlags.Instance;
            type.GetField("templates",fields).SetValue(probe,templates);type.GetField("artFolder",fields).SetValue(probe,"Tests/W15Missing/");
            type.GetField("contentArt",fields).SetValue(probe,art);
            var logs=new List<string>();
            void Log(string message,string trace,LogType kind){if(message.StartsWith("W15_MISSING_ART "))logs.Add(message);}
            Application.logMessageReceived+=Log;ThemeRegistry.RegisterForTests(probe);
            try
            {
                var realLoot=loot.Single(s=>s!=null&&s.name=="illustration_L1");var realBoss=boss.Single(s=>s!=null&&s.name==family);
                var defaultTemplates=icons.Concat(new[]{tier}).ToDictionary(n=>n,n=>originals.Single(s=>s!=null&&s.name==n));
                var overrideSprite=loot.Single(s=>s!=null&&s.name=="illustration_L2");
                string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
                foreach(string source in new[]{"theme","default","placeholder","restored"})
                {
                    art.Clear();templates.Clear();
                    bool missing=source=="placeholder",themed=source=="theme";
                    if(themed)
                    {
                        art["illustration_L1"]=overrideSprite;art[family]=overrideSprite;
                        foreach(string key in defaultTemplates.Keys)templates[key]=overrideSprite;
                    }
                    app.lootIllustrations15=missing?Array.Empty<Sprite>():loot;
                    app.defaultBossEmblems15=missing?Array.Empty<Sprite>():boss;
                    UiKit.Templates=missing?originals.Where(s=>s!=null&&!defaultTemplates.ContainsKey(s.name)).ToArray():originals;
                    app.ApplyTheme11(probeId);yield return W11Settle();
                    var image=FindActive<Image>("LootPicture15");var mark=FindActive<RectTransform>("BossEmblem15");
                    Assert.That(image.sprite,Is.SameAs(missing?null:themed?overrideSprite:realLoot));
                    Assert.That(Art073Inside(V06ScreenRect(image.rectTransform),V06ScreenRect(FindActive<RectTransform>("LootPlaque15"))),Is.True);
                    var emblem=mark.Find("BossFamily15").GetComponent<Image>();
                    Assert.That(emblem.sprite,Is.SameAs(missing?null:themed?overrideSprite:realBoss));
                    Assert.That(Art073Inside(V06ScreenRect(emblem.rectTransform),V06ScreenRect(mark)),Is.True);
                    if(missing)
                    {
                        Assert.That(Text10("MissingPicture15"),Is.EqualTo(app.Config.Loot("L1").displayName.Substring(0,1)));
                        Assert.That(HasActive("MissingFamilyTier0"),Is.True);
                        Assert.That(mark.Find("TierFrameFallback15").GetComponent<Image>().sprite,Is.SameAs(UiKit.Template("ui_ring")));
                        Assert.That(mark.Find("TierFrameFallback15/TierTick"+(modifier.tier-1)),Is.Not.Null);
                    }
                    else
                    {
                        Assert.That(mark.Find("TierFrame15").GetComponent<Image>().sprite,Is.SameAs(themed?overrideSprite:defaultTemplates[tier]));
                        Assert.That(HasActive("MissingPicture15")||HasActive("TierFrameFallback15"),Is.False);
                    }
                    for(int i=0;i<icons.Length;i++)
                    {
                        var chip=FindActive<RectTransform>(chipNames[i]+"Chip");
                        var icon=chip.GetComponentsInChildren<Image>(false).SingleOrDefault(img=>img.name=="Icon");
                        if(missing)
                        {
                            Assert.That(icon,Is.Null);
                            Assert.That(chip.Find(chipNames[i]).GetComponent<RectTransform>().anchoredPosition.x,Is.EqualTo(12));
                        }
                        else
                        {
                            Assert.That(icon.sprite,Is.SameAs(themed?overrideSprite:defaultTemplates[icons[i]]));
                            Assert.That(Art073Inside(V06ScreenRect(icon.rectTransform),V06ScreenRect(chip)),Is.True);
                        }
                    }
                    Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                }
                foreach(string asset in new[]{"illustration_L1",family,tier})Assert.That(logs.Any(l=>l.EndsWith("/"+asset+" placeholder")),Is.True,asset);
                foreach(string asset in icons)Assert.That(logs.Any(l=>l.EndsWith("/"+asset+" text-only")),Is.True,asset);
                Directory.CreateDirectory(LootFolder24);File.WriteAllLines(Path.Combine(LootFolder24,"se13-missing-art.txt"),logs);
                // Exercise the same fallback after switching from the receipt to the actual picker's info panel.
                app.lootIllustrations15=Array.Empty<Sprite>();Click("SettlementContinue");yield return W11Settle();
                Assert.That(FindActive<RectTransform>("LootInfo15").Find("LootPicture15/MissingPicture15").GetComponent<TextMeshProUGUI>().text,
                    Is.EqualTo(app.Config.Loot("L1").displayName.Substring(0,1)));
            }
            finally
            {
                Application.logMessageReceived-=Log;art.Clear();UiKit.Templates=originals;app.lootIllustrations15=loot;app.defaultBossEmblems15=boss;
                app.ApplyTheme11(previous);ThemeRegistry.UnregisterForTests(probeId);
            }
        }

        [UnityTest] public IEnumerator PLAY_V15_SE01_SE07_LootForegroundLayoutAndInk([ValueSource(nameof(LootStyles24))] string theme)
        {
            Assert.That(new[]{new Vector2Int(1920,1080),new Vector2Int(1280,720)},Does.Contain(new Vector2Int(Screen.width,Screen.height)));
            Directory.CreateDirectory(LootFolder24);
            var report=new List<string>{"Unity "+Application.unityVersion+" source "+IdentityHead20X()};var failures=new List<string>();
            var previousTheme=app.ThemeId11;bool had=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey),oldClean=GameApp.CleanCapture11;
            int previous=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey);
            PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);GameApp.CleanCapture11=true;
            try
            {
                foreach(int percent in new[]{100,150})
                {
                    IEnumerator Load(RunState fixture)
                    {
                        yield return W14Load(fixture);FinalOptions07("Instant",false);app.ApplyTheme11(theme,false);
                        W11TextScale(percent);yield return W11Settle();Assert.That(app.ThemeId11,Is.EqualTo(theme));
                    }
                    IEnumerator Check(string state)=>CheckLootView24(theme+"-"+percent+"-"+state,report,failures);
                    foreach(string kind in new[]{"L1","L2","L3","L4","L5","L6"})
                    {
                        var shop=LootShop24(kind);
                        if(kind=="L4")UpgradeQuote24(shop,"LOOT","U06");
                        yield return Load(shop);yield return Check(kind+"-P0");
                        var offers=GroupOffers24(shop);
                        if(kind=="L5")
                        {
                            string state=StateJson();
                            CheckLootDieTargets15(offers);
                            yield return W11Settle();
                            Assert.That(StateJson(),Is.EqualTo(state),"Previewing a die must not claim it.");
                        }
                        var first=kind=="L4"?offers.First(o=>o.contentId=="U06"):offers[0];
                        foreach(var offer in new[]{first}.Concat(offers.Where(o=>o.offerId!=first.offerId)))
                        {
                            if(!HasActive("Buy_"+offer.offerId)||!Button("Buy_"+offer.offerId).interactable)continue;
                            if(kind=="L5")Art071PointerClick("Buy_"+offer.offerId);
                            else Click("Buy_"+offer.offerId);
                            if(kind=="L4")
                            {
                                bool sample=offer.offerId==first.offerId;
                                if(sample)yield return Check("L4-S1");
                                ChooseLootDie24(offer);
                                if(offer.contentId!="U05")
                                {
                                    if(sample)yield return Check("L4-S2");
                                    ChooseLootFace24(offer);
                                }
                                if(sample)yield return Check("L4-S3");
                                Click("ConfirmPurchase");
                            }
                            yield return WaitForIdle();
                            var g=app.Session.State.shop.groups.Single(x=>x.groupId=="LOOT");
                            if(!g.closed&&g.picked==1)yield return Check(kind+"-P1");
                            if(g.closed)break;
                        }
                        Assert.That(app.Session.State.shop.groups.Single(g=>g.groupId=="LOOT").closed,Is.True,kind);
                        yield return Check(kind+"-P2");
                    }
                    var cash=LootShop24("L5",cashOnly:true);
                    yield return Load(cash);Assert.That(GroupOffers24(cash),Is.Empty);Assert.That(HasActive("LootOnlyCash15"),Is.True);
                    yield return Check("cash-only");long initial=app.Session.State.gold;
                    Click("LootCashOut15");yield return WaitForIdle();Assert.That(app.Session.State.gold,Is.EqualTo(initial+app.Config.lootGoldValue));
                    Assert.That(HasActive("LootCashOut15"),Is.False);
                    foreach(bool gifts in new[]{false,true})
                    {
                        var shop=LootShop24("L6",gifts);
                        while(shop.inventory.Count<shop.consumableSlots)BoundaryItem(shop,"I01");
                        yield return Load(shop);yield return Check(gifts?"three-groups-full":"full-inventory");
                        Click("ShopItem_"+shop.inventory[0].instanceId);Click("Sell");yield return WaitForIdle();
                        Click("Buy_"+GroupOffers24(shop)[0].offerId);yield return WaitForIdle();
                        Click("LootEnterShop15");Click("ViewGifts");
                        foreach(var group in shop.shop.groups)
                        {
                            if(gifts)Click("GiftTab_"+group.groupId);
                            yield return Check((gifts?"three-groups-":"single-group-")+group.groupId);
                        }
                    }
                }
            }
            finally
            {
                File.WriteAllLines(Path.Combine(LootFolder24,theme+"-layout-ink.txt"),report.Concat(failures.Select(f=>"FAIL "+f)));
                W11TextScale(100);app.ApplyTheme11(previousTheme,false);GameApp.CleanCapture11=oldClean;
                if(had)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previous);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
            }
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(50)));
        }
    }
}
