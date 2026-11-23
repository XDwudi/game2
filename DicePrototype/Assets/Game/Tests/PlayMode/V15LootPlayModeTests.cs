using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using TMPro;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private RunState BeforeBossLoot15(string kind,bool gifts=false)
        {
            var fixture=NewBoundaryFixture("w15-fixed-"+kind+(gifts?"-gifts":""));
            fixture.cycles[0].lootKind=kind;fixture.cycles[0].modifierId="B11";
            if(gifts)Equip09(fixture,"C111","C113");
            foreach(var d in fixture.dice)foreach(var f in d.faces){f.permanentPip=4;f.baseColor="red";}
            var memory=new MemorySaveStore();var session=new GameSession(app.Config,memory,fixture);
            for(int stage=0;stage<3;stage++)
            {
                if(stage>0){CoreShopStep(session,CommandKind.Continue);CoreShopStep(session,CommandKind.LeaveShop);}
                CoreShopStep(session,CommandKind.StartStage);
                var start=session.State;start.stage.target="1";session=new GameSession(app.Config,memory,start);
                CoreShopStep(session,CommandKind.Roll);
                foreach(var d in session.State.dice)Assert.That(session.TogglePending(d.dieId),Is.True);
                CoreShopStep(session,CommandKind.ConfirmLocks);CoreShopStep(session,CommandKind.Submit);CoreShopStep(session,CommandKind.EndStage);
            }
            return session.State;
        }

        [UnityTest] public IEnumerator PLAY_V17_DefaultLootIllustrationsInReceiptAndPicker()
        {
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w17","loot",
                Screen.width+"x"+Screen.height));
            Directory.CreateDirectory(dir);
            string previous=app.ThemeId11;
            try
            {
                foreach(var kind in app.Config.lootKinds)
                {
                    var fixture=BeforeBossLoot15(kind.id);
                    foreach(int scale in new[]{100,150})
                    {
                        yield return LoadBoundaryFixture(fixture);
                        ResumeBoundaryFixture();
                        FinalOptions07("Instant",false);
                        app.ApplyTheme11(GameTheme.DefaultId);
                        W11TextScale(scale);
                        foreach(bool receipt in new[]{true,false})
                        {
                            if(!receipt){Click("SettlementContinue");yield return WaitForIdle();}
                            yield return new WaitForEndOfFrame();
                            var picture=FindActive<UnityEngine.UI.Image>("LootPicture15");
                            Assert.That(picture.sprite,Is.Not.Null,kind.id);
                            Assert.That(picture.sprite.name,Is.EqualTo("illustration_"+kind.id));
                            Assert.That(picture.rectTransform.rect.width,Is.GreaterThanOrEqualTo(144));
                            var shot=ScreenCapture.CaptureScreenshotAsTexture();
                            try { File.WriteAllBytes(Path.Combine(dir,$"{kind.id}-{(receipt?"receipt":"picker")}-{scale}.png"),shot.EncodeToPNG()); }
                            finally { UnityEngine.Object.Destroy(shot); }
                        }
                    }
                }
            }
            finally { W11TextScale(100);app.ApplyTheme11(previous,false); }
        }

        [UnityTest] public IEnumerator PLAY_V15_SE08_SixLootKindsUseTheirRealOffersAndClaimExactlyOnce()
        {
            foreach(string kind in new[]{"L1","L2","L3","L4","L5","L6"})
            {
                yield return LoadBoundaryFixture(BeforeBossLoot15(kind));ResumeBoundaryFixture();
                FinalOptions07("Instant",false);
                Click("SettlementContinue");yield return WaitForIdle();
                Assert.That(app.LootPage15,Is.True,kind);
                Assert.That(HasActive("ControllerShelf")||HasActive("ShopProducts"),Is.False,kind);
                Assert.That(Button("LootEnterShop15").interactable,Is.False,kind);
                Assert.That(Text10("LootName15"),Is.EqualTo(app.Config.Loot(kind).displayName));
                Assert.That(HasActive("LootCashOut15"),Is.True,kind);
                var before=app.Session.State;
                var offers=before.shop.offers.Where(o=>o.groupId=="LOOT"&&o.category!="gold").ToArray();
                Assert.That(offers.Length,Is.EqualTo(3),kind);
                var first=offers.First(o=>Button("Buy_"+o.offerId).interactable);
                if(kind=="L5")Assert.That(FindActive<Transform>("Product_"+first.offerId).Find("FaceStrip14"),Is.Not.Null);
                long sequence=app.Session.ActionSequence;
                Click("Buy_"+first.offerId);
                if(kind=="L4")
                {
                    Assert.That(app.FacePickStep14,Is.EqualTo("S1"));
                    var d=before.dice.First(die=>die.faces.Any(f=>app.Session.IsUpgradeTargetLegal(first.contentId,die.dieId,first.contentId=="U05"?null:f.faceId)));
                    Click("Target_"+d.dieId);
                    if(first.contentId!="U05")Click("Face_"+d.faces.First(f=>app.Session.IsUpgradeTargetLegal(first.contentId,d.dieId,f.faceId)).faceId);
                    Assert.That(Text10("FacePickPrice14"),Is.EqualTo("免费"));
                    Assert.That(Button("ConfirmPurchase").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("确认领取"));
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
                    Click("ConfirmPurchase");
                }
                yield return WaitForIdle();
                Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+1),kind);
                Assert.That(app.Session.State.gold,Is.EqualTo(before.gold),kind);
                Assert.That(app.LootPage15,Is.True);
                Assert.That(HasActive("LootCashOut15"),Is.False);
                bool multiple=kind=="L4"||kind=="L6";
                Assert.That(Text10("LootStatus15"),Is.EqualTo(multiple?"还可以领 2 件":"已领完"));
                Assert.That(HasActive("Buy_"+offers.First(o=>o.offerId!=first.offerId).offerId),Is.EqualTo(multiple));
                Click("LootEnterShop15");
                Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+1));
                Assert.That(HasActive("ControllerShelf"),Is.True);
                if(multiple)
                {
                    Assert.That(Button("ViewGifts").GetComponentInChildren<TextMeshProUGUI>().text,Does.Contain("还可领 2 件"));
                    Click("ViewGifts");Assert.That(app.LootFromShop15,Is.True);
                    Assert.That(Button("LootEnterShop15").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("返回商店"));
                    Click("LootEnterShop15");
                }
                Click("LeaveShop");Assert.That(app.Session.State.phase,Is.EqualTo("StagePreview"));
            }
        }

        [UnityTest] public IEnumerator PLAY_V15_SE09_P0AndP1ResumeWithThePersistedPickCount()
        {
            var core=new GameSession(app.Config,new MemorySaveStore(),BeforeBossLoot15("L6",true));CoreShopStep(core,CommandKind.Continue);
            yield return W14Load(core.State);
            Assert.That(app.LootPage15,Is.True);Assert.That(app.LootRevealPlaying15,Is.False);
            string id=app.Session.State.shop.offers.First(o=>o.groupId=="LOOT"&&o.category=="consumable").offerId;
            Click("Buy_"+id);yield return WaitForIdle();Assert.That(app.LootPage15,Is.True);
            yield return W14Load(app.Session.State);
            Assert.That(app.LootPage15,Is.False);Assert.That(HasActive("ControllerShelf"),Is.True);
            Click("ViewGifts");
            foreach(string group in new[]{"LOOT","GIFT_C111","GIFT_C113"})Assert.That(HasActive("GiftTab_"+group),Is.True);
            Click("GiftTab_GIFT_C113");
            var upgrade=app.Session.State.shop.offers.First(o=>o.groupId=="GIFT_C113"&&Button("Buy_"+o.offerId).interactable);
            Click("Buy_"+upgrade.offerId);Assert.That(app.FacePickStep14,Is.EqualTo("S1"));
            Click("CancelFacePick14");Assert.That(app.LootFromShop15,Is.True);
            Click("GiftTab_GIFT_C111");Assert.That(Text10("LootName15"),Does.Contain(app.Config.Controller("C111").displayName));
        }

        [UnityTest] public IEnumerator PLAY_V15_SE15_NewPageOnlyRejectsEarlyPointerClicks()
        {
            yield return LoadBoundaryFixture(BeforeBossLoot15("L6"));ResumeBoundaryFixture();FinalOptions07("Instant",false);
            yield return ShopEntranceFrame07();ShopEntrancePointer07("SettlementContinue");
            Assert.That(app.LootPage15,Is.True);
            long sequence=app.Session.ActionSequence;
            yield return ShopEntranceFrame07();
            Assert.That(GameApp.PagePointerGuard15,Is.True,"The first rendered loot page must still reject an early pointer click.");
            ShopEntrancePointer07("LootCashOut15");Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
            yield return new WaitForSecondsRealtime(.31f);ShopEntrancePointer07("LootCashOut15");yield return WaitForIdle();
            Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+1));
            Assert.That(app.Session.State.gold,Is.GreaterThan(0));
            var checkpoint=File.ReadAllBytes(app.SaveLocation);
            ShopEntrancePointer07("LootEnterShop15");
            Assert.That(app.LootPage15,Is.False);
            yield return ShopEntranceFrame07();
            Assert.That(GameApp.PagePointerGuard15,Is.True,"The first rendered shop page must still reject an early pointer click.");
            ShopEntrancePointer07("LeaveShop");
            Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));
            CollectionAssert.AreEqual(checkpoint,File.ReadAllBytes(app.SaveLocation));
            yield return new WaitForSecondsRealtime(.31f);ShopEntrancePointer07("LeaveShop");yield return WaitForIdle();
            Assert.That(app.Session.State.phase,Is.EqualTo("StagePreview"));
        }

        [UnityTest] public IEnumerator PLAY_V15_SE11_LootEntranceSkipsWithoutReplayingOrMutatingTheSave()
        {
            foreach(string mode in new[]{"Normal","Fast","Reduced","Instant"})
            {
                yield return LoadBoundaryFixture(BeforeBossLoot15("L1"));ResumeBoundaryFixture();
                FinalOptions07(mode=="Reduced"?"Normal":mode,mode=="Reduced");
                if(app.SettlementRevealPlaying07)app.SkipVisual();
                Click("SettlementContinue");Assert.That(app.LootPage15,Is.True);
                var disk=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;
                Assert.That(app.LootRevealPlaying15,Is.EqualTo(mode!="Instant"));
                if(mode!="Instant")
                {
                    float expected=mode=="Fast"?.18f:mode=="Reduced"?.12f:.36f;
                    Assert.That(app.LootRevealDuration15,Is.EqualTo(expected).Within(.001f));
                    Click("SkipLootReveal15");Assert.That(app.LootRevealPlaying15,Is.False);
                }
                app.Refresh();Assert.That(app.LootRevealPlaying15,Is.False);
                Assert.That(app.Session.ActionSequence,Is.EqualTo(seq));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
        }

        private static Vector2 LootCenter15(RectTransform rect) =>
            RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));

        [UnityTest, Timeout(120000)] public IEnumerator PLAY_V15_SE11_CashOutCoinsStartOnButtonAndReachWallet()
        {
            foreach(string mode in new[]{"Normal","Fast","Reduced","Instant"})
            {
                yield return LoadBoundaryFixture(BeforeBossLoot15("L6"));ResumeBoundaryFixture();
                FinalOptions07("Instant",false);
                yield return new WaitForSecondsRealtime(.31f);yield return ShopEntranceFrame07();
                ShopEntrancePointer07("SettlementContinue");FinalOptions07(mode=="Reduced"?"Normal":mode,mode=="Reduced");
                yield return new WaitForSecondsRealtime(.31f);yield return ShopEntranceFrame07();
                var from=LootCenter15(TransactionRect07("LootCashOut15"));long gold=app.Session.State.gold,seq=app.Session.ActionSequence;
                ShopEntrancePointer07("LootCashOut15");
                Assert.That(app.Session.State.gold-gold,Is.EqualTo(app.Config.lootGoldValue));
                Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));
                var bytes=File.ReadAllBytes(app.SaveLocation);
                app.enabled=false;
                try
                {
                    var motion=app.canvas.transform.Find("Art07TransactionMotion").GetComponent<Art07TransactionMotion>();
                    var coins=motion.transform.Find("CommittedTransactionView").GetComponentsInChildren<RectTransform>(true)
                        .Where(r=>r.name.StartsWith("LootCoin15_")).OrderBy(r=>r.name).ToArray();
                    bool flies=mode=="Normal"||mode=="Fast";
                    Assert.That(coins.Length,Is.EqualTo(flies?5:0),mode);
                    Assert.That(motion.IsPlaying,Is.EqualTo(mode!="Instant"));
                    if(flies)
                    {
                        float scale=mode=="Fast"?.5f:1;
                        Assert.That(motion.Duration,Is.EqualTo(.34f*scale).Within(.001f));
                        Assert.That(Vector2.Distance(LootCenter15(coins[0]),from),Is.LessThan(2));
                        var to=LootCenter15(TransactionRect07("MoneyHover14"));
                        motion.Tick(.12f*scale,false);
                        Assert.That(coins.Count(c=>c.gameObject.activeInHierarchy),Is.EqualTo(5));
                        Assert.That(Vector2.Distance(LootCenter15(coins[0]),from),Is.GreaterThan(10));
                        Assert.That(Vector2.Distance(LootCenter15(coins[0]),to),Is.GreaterThan(10));
                        if(mode=="Normal")yield return LootDeliveryPhoto15("cash-five-coins");
                        motion.Tick(.121f*scale,false);
                        Assert.That(coins[0].gameObject.activeSelf,Is.False);
                        Assert.That(Vector2.Distance(LootCenter15(coins[0]),to),Is.LessThan(2));
                    }
                    else if(mode=="Reduced")Assert.That(motion.Duration,Is.EqualTo(.12f).Within(.001f));
                    if(motion.IsPlaying)motion.Tick(1,false);
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));
                    CollectionAssert.AreEqual(bytes,File.ReadAllBytes(app.SaveLocation));
                }
                finally {app.enabled=true;}
            }
        }

        private IEnumerator LootDeliveryPhoto15(string name)
        {
            yield return new WaitForEndOfFrame();
            var dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w15","delivery",Screen.width+"x"+Screen.height));
            Directory.CreateDirectory(dir);
            var image=ScreenCapture.CaptureScreenshotAsTexture();
            try {File.WriteAllBytes(Path.Combine(dir,name+".png"),image.EncodeToPNG());}
            finally {Object.Destroy(image);}
        }

        [UnityTest, Timeout(240000)] public IEnumerator PLAY_V15_SE11_LootDeliveryUsesCommittedInstanceAndRealDestination()
        {
            foreach(string kind in new[]{"L1","L6","L5"})
            foreach(string mode in new[]{"Normal","Fast","Reduced","Instant","Skip"})
            {
                yield return LoadBoundaryFixture(BeforeBossLoot15(kind));ResumeBoundaryFixture();
                FinalOptions07("Instant",false);
                yield return new WaitForSecondsRealtime(.31f);yield return ShopEntranceFrame07();
                ShopEntrancePointer07("SettlementContinue");
                FinalOptions07(mode=="Reduced"||mode=="Skip"?"Normal":mode,mode=="Reduced");
                yield return new WaitForSecondsRealtime(.31f);yield return ShopEntranceFrame07();
                var before=app.Session.State;long seq=app.Session.ActionSequence;
                var offer=before.shop.offers.First(o=>o.groupId=="LOOT"&&o.category!="gold"&&Button("Buy_"+o.offerId).IsInteractable());
                string prefix=kind=="L1"?"Offer_":"Product_";
                var origin=LootCenter15(TransactionRect07(prefix+offer.offerId));
                ShopEntrancePointer07("Buy_"+offer.offerId);
                Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1),kind+" "+mode);
                var committed=app.Session.State;string hash=GameSession.StateHash(committed);var disk=File.ReadAllBytes(app.SaveLocation);
                string dieId=kind=="L5"?committed.dice.Single(d=>before.dice.All(p=>p.dieId!=d.dieId)).dieId:null;
                string destination=kind=="L1"?"Controller_"+committed.controllers.Single(c=>before.controllers.All(p=>p.instanceId!=c.instanceId)).instanceId:
                    kind=="L6"?"ShopItem_"+committed.inventory.Single(c=>before.inventory.All(p=>p.instanceId!=c.instanceId)).instanceId:"OwnedDie_"+dieId;
                var owned=TransactionRect07(destination);
                Assert.That(owned.IsChildOf(TransactionRect07(kind=="L5"?"OwnedStrip":"Build14")),Is.True,destination);
                var end=LootCenter15(owned);
                Assert.That(new Rect(0,0,Screen.width,Screen.height).Contains(end),Is.True,destination);
                Assert.That(app.TransactionVisualPlaying07,Is.EqualTo(mode!="Instant"),mode);
                var motion=app.canvas.transform.Find("Art07TransactionMotion").GetComponent<Art07TransactionMotion>();
                // Freeze the host's Update only; the renderer and real pointer route keep running.
                // Sampling the production clock avoids slow screenshot readbacks changing phase.
                app.enabled=false;
                try
                {
                    if(mode!="Instant")
                    {
                        float duration=mode=="Reduced"?.12f:mode=="Fast"?.25f:.50f;
                        Assert.That(motion.Duration,Is.EqualTo(duration).Within(.001f),mode);
                        Assert.That(motion.AnimatedTargets,Does.Contain(destination));Assert.That(motion.CapturedGraphicCount,Is.GreaterThan(0));
                        var shot=(RectTransform)motion.transform.Find("CommittedOldMeshes");var alpha=shot.GetComponent<CanvasGroup>();
                        Assert.That(alpha.blocksRaycasts||alpha.interactable,Is.False);
                        Assert.That(Vector2.Distance(LootCenter15(shot),origin),Is.LessThan(2),kind+" origin");
                        Assert.That(owned.GetComponent<CanvasGroup>().alpha,Is.Zero);
                        if(mode=="Reduced")
                        {
                            motion.Tick(.06f,false);
                            Assert.That(Vector2.Distance(LootCenter15(shot),origin),Is.LessThan(2));
                            Assert.That(shot.localScale,Is.EqualTo(Vector3.one));
                            Assert.That(owned.GetComponent<CanvasGroup>().alpha,Is.EqualTo(.5f).Within(.03f));
                        }
                        else if(kind!="L5")
                        {
                            float halfTravel=mode=="Fast"?.085f:.17f;
                            motion.Tick(halfTravel,false);
                            Assert.That(Vector2.Distance(LootCenter15(shot),Vector2.Lerp(origin,end,.5f)),Is.LessThan(2),kind+" in flight");
                            Assert.That(alpha.alpha,Is.EqualTo(1));Assert.That(shot.localScale.x,Is.LessThan(1));
                            Assert.That(owned.GetComponent<CanvasGroup>().alpha,Is.Zero);
                            if(mode=="Normal")yield return LootDeliveryPhoto15(kind+"-in-flight");
                            if(mode!="Skip")
                            {
                                motion.Tick(halfTravel,false);
                                Assert.That(Vector2.Distance(LootCenter15(shot),end),Is.LessThan(2),kind+" arrival");
                                Assert.That(alpha.alpha,Is.EqualTo(1).Within(.001f));
                                if(mode=="Normal")yield return LootDeliveryPhoto15(kind+"-arrival");
                            }
                        }
                        else
                        {
                            motion.Tick(duration*.5f,false);
                            Assert.That(owned.GetComponent<CanvasGroup>().alpha,Is.Zero);
                            Assert.That(HasActive("AcquiredInstance07"),Is.False,"The die belongs in the real OwnedStrip.");
                        }
                        if(mode=="Skip")app.SkipVisual();else motion.Tick(duration+1,false);
                    }
                    Assert.That(motion.IsPlaying,Is.False);Assert.That(app.Session.Busy,Is.False);
                    yield return ShopEntranceFrame07();
                    owned=TransactionRect07(destination);Assert.That(Vector2.Distance(LootCenter15(owned),end),Is.LessThan(2));
                    Assert.That(owned.GetComponentsInParent<CanvasGroup>().All(g=>g.alpha>=.999f),Is.True);
                    if(kind=="L5")
                    {
                        var view=owned.GetComponent<Art073DieView>();
                        Assert.That(view.IsShown,Is.True);
                        Assert.That(app.UiDice073.GetFaceOrientation("$static-preview:owned-"+dieId)?.gameObject.activeInHierarchy,Is.True);
                    }
                    if(mode=="Normal")yield return LootDeliveryPhoto15(kind+"-settled");
                    Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));
                    Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(hash));
                    CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                }
                finally {app.enabled=true;}
            }
        }
    }
}
