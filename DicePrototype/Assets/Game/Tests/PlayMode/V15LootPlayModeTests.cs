using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
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
    }
}
