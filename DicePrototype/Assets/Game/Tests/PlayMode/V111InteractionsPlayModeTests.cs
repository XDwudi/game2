using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using DiceCraft.Presentation;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1.1 task W7b (docs/specs/v111/briefs/W7B_UI_INTERACTIONS.md item 9): the new player actions of p1.1.1 through
    /// the real interface - the entry is clicked, the die and the faces are chosen, the confirmation is pressed, and
    /// the session state and the words on screen are checked. Each test also saves the screens the coordinator reviews
    /// (W7bShot) to artifacts/cache/v111/w7b/&lt;width&gt;x&lt;height&gt;/: run the file at 1920x1080 and at 1280x720.
    /// The glass shatter after a submission is checked in Art07PostEffectsPlayModeTests
    /// (PLAY_ART07_POST_ThirdAndFourthBrokenDiceKeepOriginalOutlineAndOnlyTwelveFragments); the run result page in
    /// Art07ResultRowsPlayModeTests.
    /// Written without a Unity run (W7b could not start the editor): compiled only.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private string W7bOutput
        {
            get
            {
                string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v111","w7b",Screen.width+"x"+Screen.height));
                Directory.CreateDirectory(dir);return dir;
            }
        }
        /// <summary>Saves the screen as the player sees it (the debug-workspace markers hidden), then restores the view.</summary>
        private IEnumerator W7bShot(string name)
        {
            bool clean=GameApp.CleanCapture11;GameApp.CleanCapture11=true;
            try
            {
                app.Refresh();Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                var texture=ScreenCapture.CaptureScreenshotAsTexture();
                try{File.WriteAllBytes(Path.Combine(W7bOutput,name+".png"),texture.EncodeToPNG());}
                finally{UObj.Destroy(texture);}
            }
            finally{GameApp.CleanCapture11=clean;}
            app.Refresh();Canvas.ForceUpdateCanvases();
        }
        private string W7bText(string name)=>FindActive<TextMeshProUGUI>(name).text.Replace("\n","");
        private string W7bButtonText(string name)
        {
            var button=Button(name);var label=button.transform.Find("Label");
            return (label!=null?label.GetComponent<TextMeshProUGUI>():button.GetComponentInChildren<TextMeshProUGUI>()).text.Replace("\n","");
        }
        private CommandReceipt W7bLastReceipt=>app.Session.State.processedReceipts.Last();
        private static bool W7bOnTable(Transform pivot)=>pivot!=null&&pivot.gameObject.activeInHierarchy;

        // ================================================================ one or two faces (RULES_DELTA 7.1)
        [UnityTest]
        public IEnumerator PLAY_V111_TwoFaceUpgradeCostsOnePriceAndAnIllegalFaceBlocksThePair()
        {
            var fixture=ShopBoundaryFixture();
            AddBoundaryOffer(fixture,"w7b-gold-a","die_upgrade","U06");AddBoundaryOffer(fixture,"w7b-gold-b","die_upgrade","U06");
            var first=fixture.dice.OrderBy(d=>d.createdSerial).First();var second=fixture.dice.OrderBy(d=>d.createdSerial).Skip(1).First();
            // One face of the first die is gold already: gilding cannot take it.
            first.faces.First(f=>f.faceId=="F03").isGold=true;
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();
            string die=first.dieId,other=second.dieId;
            Assert.That(app.Session.MaxSelectableFaces("U06"),Is.EqualTo(2));
            CollectionAssert.DoesNotContain(app.Session.LegalUpgradeFaces("U06",die),"F03");
            var offer=app.Session.State.shop.offers.Single(o=>o.offerId=="w7b-gold-a");long price=app.Session.GetOfferNetPrice(offer);
            yield return OpenBoundaryOffer("w7b-gold-a");
            Assert.That(app.FacePickStep14,Is.EqualTo("S1"));
            Click("Target_"+die);Assert.That(app.FacePickStep14,Is.EqualTo("S2"));
            Assert.That(Button("Face_F03").transform.Find("Unavailable14")?.gameObject.activeSelf,Is.True,"The face gilding cannot change is dimmed.");
            Assert.That(Button("Face_F01").transform.Find("Unavailable14")?.gameObject.activeSelf??false,Is.False);
            string before=StateJson();
            Click("Face_F01");Assert.That(app.FacePickStep14,Is.EqualTo("S3"));CollectionAssert.AreEqual(new[]{"F01"},app.FacePickFaces14);
            Assert.That(Button("ConfirmPurchase").interactable,Is.True);Assert.That(HasActive("AddFace14"),Is.True,"An upgrade that takes two faces offers the second.");
            string onePrice=W7bText("FacePickPrice14");
            // One legal and one illegal face: the pair is refused, the reason names the face, nothing is sent.
            Click("AddFace14");Assert.That(app.FacePickStep14,Is.EqualTo("S2"));Assert.That(W7bText("FacePickPrompt14"),Does.StartWith("再选一个面"));
            Click("Face_F03");Assert.That(app.FacePickStep14,Is.EqualTo("S3"));CollectionAssert.AreEqual(new[]{"F01","F03"},app.FacePickFaces14);
            Assert.That(Button("ConfirmPurchase").interactable,Is.False);
            Assert.That(W7bText("PurchaseUnavailable"),Does.Contain("已经镀金"));
            Assert.That(app.Session.PurchaseErrorForFaces("w7b-gold-a",die,new[]{"F01","F03"}),Is.EqualTo(GameSession.UpgradeTargetRejected));
            Assert.That(HasActive("AddFace14"),Is.False,"Two faces are the limit.");
            Assert.That(StateJson(),Is.EqualTo(before),"Choosing faces sends nothing.");
            yield return W7bShot("face-pick-pair-rejected");
            // 换一面 starts the faces afresh; two legal faces preview both changes for the same price.
            Click("ChangeFace14");Assert.That(app.FacePickStep14,Is.EqualTo("S2"));Assert.That(app.FacePickFaces14,Is.Empty);
            Click("Face_F02");Click("AddFace14");
            Assert.That(Button("Face_F02").transform.Find("Selected14")?.gameObject.activeSelf,Is.True,"The face chosen first stays ringed while the second is added.");
            yield return W7bShot("face-pick-adding-second");
            Click("Face_F01");CollectionAssert.AreEqual(new[]{"F01","F02"},app.FacePickFaces14);
            Assert.That(FindActive<TextMeshProUGUI>("PurchasePreviewBody").text.Split('\n').Count(line=>line.Contains("→")),Is.GreaterThanOrEqualTo(2),"Both faces are previewed.");
            Assert.That(W7bText("FacePickPrice14"),Is.EqualTo(onePrice),"The price does not change with the number of faces.");
            Assert.That(Button("ConfirmPurchase").interactable,Is.True);
            yield return W7bShot("face-pick-two-faces");
            long gold=app.Session.State.gold;
            Click("ConfirmPurchase");yield return WaitForIdle();app.SkipVisual();yield return WaitForIdle();
            var after=app.Session.State;var changed=after.dice.Single(d=>d.dieId==die);
            Assert.That(changed.faces.Where(f=>f.faceId=="F01"||f.faceId=="F02").All(f=>f.isGold),Is.True);
            Assert.That(after.gold,Is.EqualTo(gold-price),"One purchase, one price.");
            Assert.That(after.shop.offers.Single(o=>o.offerId=="w7b-gold-a").sold,Is.True);
            CollectionAssert.AreEqual(new[]{"F01","F02"},W7bLastReceipt.command.targetFaceIds);
            Assert.That(W7bLastReceipt.effects.Count(e=>e.kind=="DieUpgraded"),Is.EqualTo(2));
            // Another die starts its faces afresh.
            yield return OpenBoundaryOffer("w7b-gold-b");
            Click("Target_"+die);Click("Face_F04");CollectionAssert.AreEqual(new[]{"F04"},app.FacePickFaces14);
            Click("Target_"+other);Assert.That(app.FacePickDie14,Is.EqualTo(other));Assert.That(app.FacePickFaces14,Is.Empty);Assert.That(app.FacePickStep14,Is.EqualTo("S2"));
            Click("CancelFacePick14");Assert.That(app.FacePickStep14,Is.EqualTo("S0"));
        }

        // ================================================================ the self-selected upgrade (RULES_DELTA 9.2)
        [UnityTest]
        public IEnumerator PLAY_V111_CustomUpgradeListBuysTwiceThenTheEntryIsGreyed()
        {
            var fixture=ShopBoundaryFixture();
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();
            var die=app.Session.State.dice.OrderBy(d=>d.createdSerial).First();string id=die.dieId;
            Assert.That(app.Session.CustomDieUpgradesRemaining,Is.EqualTo(2));
            Assert.That(Button("CustomUpgrade111").interactable,Is.True);Assert.That(W7bButtonText("CustomUpgrade111"),Is.EqualTo("自选改造 · 还剩 2 次"));
            Assert.That(HasActive("Study"),Is.True,"It stands beside the hand study entry.");
            Click("CustomUpgrade111");
            var options=app.Session.CustomDieUpgradeOptions();Assert.That(options.Length,Is.EqualTo(11));
            foreach(var option in options)
            {
                // Every row shows the session's live price; a row that can be bought has its price button on.
                Assert.That(W7bButtonText("CustomBuy"+option.upgradeId),Is.EqualTo(option.price+" 金币"),option.upgradeId);
                Assert.That(Button("CustomBuy"+option.upgradeId).interactable,Is.EqualTo(option.error==null),option.upgradeId);
                Assert.That(option.price,Is.EqualTo(app.Session.GetCustomDieUpgradePrice(option.upgradeId)));
            }
            yield return W7bShot("custom-upgrade-list");
            // First purchase: 骰子染红 on two faces of one die.
            var faces=app.Session.LegalUpgradeFaces("U03",id).Take(2).ToArray();Assert.That(faces.Length,Is.EqualTo(2),"The standard die has two faces that are not red.");
            long price=app.Session.GetCustomDieUpgradePrice("U03"),gold=app.Session.State.gold;
            Click("CustomBuyU03");
            Assert.That(app.FacePickCustom14,Is.True);Assert.That(app.FacePickStep14,Is.EqualTo("S1"));Assert.That(W7bText("FacePickCategory14"),Is.EqualTo("自选改造"));
            Click("Target_"+id);Click("Face_"+faces[0]);Click("AddFace14");Click("Face_"+faces[1]);
            CollectionAssert.AreEqual(faces.OrderBy(f=>f,StringComparer.Ordinal),app.FacePickFaces14);
            Assert.That(W7bText("FacePickPrice14"),Does.StartWith("价格 "+price));
            yield return W7bShot("custom-upgrade-two-faces");
            Click("ConfirmPurchase");yield return WaitForIdle();app.SkipVisual();yield return WaitForIdle();
            var after=app.Session.State;
            Assert.That(W7bLastReceipt.command.kind,Is.EqualTo(CommandKind.CustomDieUpgrade));Assert.That(W7bLastReceipt.command.upgradeId,Is.EqualTo("U03"));
            Assert.That(W7bLastReceipt.effects.Any(e=>e.kind=="CustomDieUpgraded"),Is.True);
            Assert.That(after.dice.Single(d=>d.dieId==id).faces.Where(f=>faces.Contains(f.faceId)).All(f=>f.baseColor=="red"),Is.True);
            Assert.That(after.gold,Is.EqualTo(gold-price));Assert.That(after.shop.selfDieUpgradeUsed,Is.EqualTo(1));
            Assert.That(W7bButtonText("CustomUpgrade111"),Is.EqualTo("自选改造 · 还剩 1 次"),"The entry shows what is left after a purchase.");
            // Second purchase: a whole-die upgrade takes no face.
            Click("CustomUpgrade111");Click("CustomBuyU05");
            Click("Target_"+id);Assert.That(app.FacePickStep14,Is.EqualTo("S3"));Assert.That(HasActive("AddFace14"),Is.False);
            Click("ConfirmPurchase");yield return WaitForIdle();app.SkipVisual();yield return WaitForIdle();
            Assert.That(app.Session.State.dice.Single(d=>d.dieId==id).isGlass,Is.True);Assert.That(app.Session.State.shop.selfDieUpgradeUsed,Is.EqualTo(2));
            // The allowance is used up: the entry is greyed and says so, and the session refuses a third.
            Assert.That(app.Session.CustomDieUpgradesRemaining,Is.Zero);
            Assert.That(Button("CustomUpgrade111").interactable,Is.False);Assert.That(W7bButtonText("CustomUpgrade111"),Is.EqualTo("本店自选改造已用完"));
            yield return W7bShot("custom-upgrade-used-up");
            string state=StateJson();
            var refused=app.Send(new SessionCommand{kind=CommandKind.CustomDieUpgrade,upgradeId="U04",targetDieId=id,targetFaceIds=new[]{faces[0]}});
            Assert.That(refused.accepted,Is.False);Assert.That(refused.error,Is.EqualTo(GameSession.SelfDieUpgradeLimitReached));Assert.That(StateJson(),Is.EqualTo(state));
            yield return null;
            Assert.That(ToastLabels07().Any(label=>label.text=="本店自选改造已用完"),Is.True,"The refusal reads in the player's words.");
        }

        [UnityTest]
        public IEnumerator PLAY_V111_CustomUpgradeListGivesTheReasonWhenGoldIsShort()
        {
            var fixture=ShopBoundaryFixture();fixture.gold=0;
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();
            Click("CustomUpgrade111");
            foreach(var option in app.Session.CustomDieUpgradeOptions())
            {
                Assert.That(option.error,Is.Not.Null,option.upgradeId);
                Assert.That(Button("CustomBuy"+option.upgradeId).interactable,Is.False,option.upgradeId);
                Assert.That(W7bText("CustomNote"+option.upgradeId),Is.EqualTo("现在不能买：还差 "+option.price+" 金币"),option.upgradeId);
            }
            yield return W7bShot("custom-upgrade-list-no-gold");
        }

        // ================================================================ the key ring and the unlock (RULES_DELTA 8)
        /// <summary>A rolled hand with `locked` locked dice, the given cards, and (optionally) one key ring with three uses.</summary>
        private DebugScene W7bUnlockScene(string[] cards,int locked,bool keyRing,string modifier="",int stageIndex=3)
        {
            var scene=W11Scene(cards,new[]{4,4,4,2,6},"bbbbr",locked,true,stageIndex:stageIndex,modifier:modifier,items:0);
            if(keyRing)
            {
                int serial=app.Session.State.nextItemSerial+cards.Length;
                scene.inventory.Add(new OwnedItem{instanceId="ITEM_"+serial.ToString("D5"),contentId="I13",usesRemaining=3});
            }
            return scene;
        }
        private OwnedItem W7bKeyRing=>app.Session.State.inventory.FirstOrDefault(i=>i.contentId=="I13");
        private string W7bItemUses(string instanceId)=>Button("Item_"+instanceId).transform.Find("Uses111/Count").GetComponent<TextMeshProUGUI>().text;

        [UnityTest]
        public IEnumerator PLAY_V111_KeyRingTakesThreeLockedDiceBackThenLeavesItsSlot()
        {
            StartRun("71111");
            W11Apply(W7bUnlockScene(new string[0],3,true));yield return W11Settle();
            string keyName=app.Config.Consumable("I13").displayName,ring=W7bKeyRing.instanceId;
            var locked=app.Session.State.hand.orderedLockedIds.ToArray();Assert.That(locked.Length,Is.EqualTo(3));
            Assert.That(W7bItemUses(ring),Is.EqualTo("3"),"The icon shows the uses left.");
            Assert.That(W7bText("LockRule"),Is.EqualTo("已锁定 · 点一颗骰子可以撤回"));
            yield return W7bShot("items-key-ring-uses");
            // The key ring's own button sends nothing: it asks for a die and marks the ones that can come back.
            string before=StateJson();
            Click("Item_"+ring);Assert.That(W7bButtonText("Use"),Is.EqualTo("去选要撤回的骰子"));Click("Use");
            Assert.That(app.UnlockArmed111,Is.True);Assert.That(StateJson(),Is.EqualTo(before),"The key ring's button is not a command.");
            Assert.That(W7bText("ActionReason"),Is.EqualTo("点一颗已锁定的骰子来撤回。"));
            foreach(string id in locked)Assert.That(HasActive("UnlockTarget111_"+id),Is.True,"Every die the session would take back is marked.");
            yield return W7bShot("unlock-key-ring-armed");
            // A click on a locked die only selects it; the explicit button names the source and sends the command.
            ClickDie(locked[1]);
            Assert.That(app.UnlockPick111,Is.EqualTo(locked[1]));Assert.That(StateJson(),Is.EqualTo(before),"Selecting a locked die sends nothing.");
            Assert.That(HasActive("UnlockPick111_"+locked[1]),Is.True);
            Assert.That(W7bButtonText("UnlockDie111"),Is.EqualTo("撤回 · "+keyName+"（剩 3 次）"));
            Assert.That(Button("UnlockDie111").interactable,Is.True);Assert.That(HasActive("UnlockCancel111"),Is.True);
            yield return W7bShot("unlock-by-key-ring");
            Click("UnlockCancel111");Assert.That(app.UnlockPick111,Is.Null);Assert.That(HasActive("UnlockDie111"),Is.False);Assert.That(StateJson(),Is.EqualTo(before));
            int rolls=app.Session.State.hand.rollsRemaining;
            for(int step=0;step<3;step++)
            {
                // Take back the middle die first, then the first, then the last one.
                string id=step==0?locked[1]:step==1?locked[0]:locked[2];int usesBefore=3-step;
                var remaining=app.Session.State.hand.orderedLockedIds.Where(x=>x!=id).ToArray();
                ClickDie(id);Assert.That(W7bButtonText("UnlockDie111"),Is.EqualTo("撤回 · "+keyName+"（剩 "+usesBefore+" 次）"));
                Click("UnlockDie111");yield return WaitForIdle();yield return W11Settle();
                var s=app.Session.State;var receipt=W7bLastReceipt;
                Assert.That(receipt.command.kind,Is.EqualTo(CommandKind.UnlockDie));Assert.That(receipt.command.targetDieId,Is.EqualTo(id));
                CollectionAssert.AreEqual(new[]{"DieUnlocked","ItemUsed"},receipt.effects.Where(e=>e.kind=="DieUnlocked"||e.kind=="ItemUsed").Select(e=>e.kind),"The unlock is recorded first, then the use of the key ring.");
                Assert.That(receipt.effects.First(e=>e.kind=="DieUnlocked").sourceId,Is.EqualTo("I13"));
                var use=receipt.effects.First(e=>e.kind=="ItemUsed");Assert.That(use.oldValue,Is.EqualTo(usesBefore));Assert.That(use.newValue,Is.EqualTo(usesBefore-1));
                CollectionAssert.AreEqual(remaining,s.hand.orderedLockedIds,"The other locked dice keep their order.");
                Assert.That(s.hand.currentResults.Single(r=>r.dieId==id).isLocked,Is.False);Assert.That(s.hand.rollsRemaining,Is.EqualTo(rolls),"An unlock spends no roll.");
                Assert.That(app.UnlockPick111,Is.Null,"The selection ends with the command.");
                // Both records have their line.
                Assert.That(app.LastTransactionSummary07,Does.Contain("已撤回"+GameApp.DieName11(id)).And.Contain(keyName));
                if(usesBefore>1)
                {
                    Assert.That(W7bKeyRing.usesRemaining,Is.EqualTo(usesBefore-1));Assert.That(W7bItemUses(ring),Is.EqualTo((usesBefore-1).ToString()));
                    Assert.That(app.LastTransactionSummary07,Does.Contain("还剩 "+(usesBefore-1)+" 次"));
                }
                else
                {
                    // The last use: the item leaves the inventory and its slot is free at once.
                    Assert.That(W7bKeyRing,Is.Null);CollectionAssert.Contains(s.consumedItemIds,ring);
                    Assert.That(HasActive("Item_"+ring),Is.False,"The icon is gone.");Assert.That(HasActive("ItemEmpty0"),Is.True,"The slot is empty again.");
                    Assert.That(app.LastTransactionSummary07,Does.Contain("用完了"));
                }
                // The die is back in the pool in creation order among the dice that are not locked.
                var pool=s.dice.Where(d=>!s.hand.orderedLockedIds.Contains(d.dieId)).OrderBy(d=>d.createdSerial).Select(d=>d.dieId).ToArray();
                CollectionAssert.Contains(pool,id);
                var xs=pool.Select(p=>FindActive<DiePointer>("Die_"+p).GetComponent<RectTransform>().anchoredPosition.x).ToArray();
                for(int i=1;i<xs.Length;i++)Assert.That(xs[i],Is.GreaterThan(xs[i-1]),"Pool dice stand in creation order after the unlock (one row at this count).");
                if(step==0)yield return W7bShot("unlock-after-first");
            }
            Assert.That(app.Session.State.hand.orderedLockedIds,Is.Empty,"Unlocking the last locked die is allowed.");
            yield return W7bShot("unlock-key-ring-used-up");
        }

        [UnityTest]
        public IEnumerator PLAY_V111_FreeUnlockOfTheHandComesBeforeTheKeyRing()
        {
            StartRun("71112");
            W11Apply(W7bUnlockScene(new[]{"C154"},3,true));yield return W11Settle();
            string cardName=app.Config.Controller("C154").displayName,keyName=app.Config.Consumable("I13").displayName;
            var locked=app.Session.State.hand.orderedLockedIds.ToArray();var card=app.Session.State.controllers.Single(c=>c.definitionId=="C154");
            bool CardSays(string text)=>FindActive<Transform>("Controller_"+card.instanceId).GetComponentsInChildren<TextMeshProUGUI>(false).Any(t=>t.text.Replace("\n","")==text);
            Assert.That(app.Session.UnlockQuote(locked[0]).sourceId,Is.EqualTo("C154"));
            Assert.That(CardSays("本手免费撤回可用"),Is.True,"The card shows that this hand's free unlock is still there.");
            ClickDie(locked[0]);
            Assert.That(W7bButtonText("UnlockDie111"),Is.EqualTo("撤回 · "+cardName+"（本手免费）"),"The rules pick the source; the free unlock comes first.");
            yield return W7bShot("unlock-by-free-card");
            Click("UnlockDie111");yield return WaitForIdle();yield return W11Settle();
            var receipt=W7bLastReceipt;
            Assert.That(receipt.effects.Single(e=>e.kind=="DieUnlocked").sourceId,Is.EqualTo("C154"));
            Assert.That(receipt.effects.Any(e=>e.kind=="ItemUsed"),Is.False,"The free unlock uses no item.");
            Assert.That(W7bKeyRing.usesRemaining,Is.EqualTo(3));Assert.That(app.Session.State.hand.unlockCardUsed,Is.True);
            Assert.That(CardSays("本手免费撤回已用"),Is.True);
            Assert.That(app.LastTransactionSummary07,Does.Contain(cardName).And.Not.Contain("还剩"));
            // The next unlock of the same hand is the key ring's.
            ClickDie(locked[1]);
            Assert.That(W7bButtonText("UnlockDie111"),Is.EqualTo("撤回 · "+keyName+"（剩 3 次）"));
            Click("UnlockDie111");yield return WaitForIdle();
            Assert.That(W7bLastReceipt.effects.Single(e=>e.kind=="DieUnlocked").sourceId,Is.EqualTo("I13"));Assert.That(W7bKeyRing.usesRemaining,Is.EqualTo(2));
        }

        [UnityTest]
        public IEnumerator PLAY_V111_BareHandsBlocksTheKeyRingWithItsReasonAndNoSourceOpensTheDie()
        {
            StartRun("71113");
            string noItems=app.Config.bossModifiers.First(m=>ControllerRules.ModifierHas(app.Config,m.id,"NoItems")).id;
            // The boss effect that forbids items: the key ring cannot pay, and no free unlock is held.
            W11Apply(W7bUnlockScene(new string[0],2,true,noItems,8));yield return W11Settle();
            var locked=app.Session.State.hand.orderedLockedIds.ToArray();
            Assert.That(app.Session.UnlockQuote(locked[0]).available,Is.False);Assert.That(app.Session.UnlockQuote(locked[0]).error,Is.EqualTo(GameSession.ItemsForbidden));
            Assert.That(W7bText("LockRule"),Is.EqualTo("已锁定 · 本手不可移回骰池"));
            string before=StateJson();
            ClickDie(locked[0]);
            Assert.That(app.UnlockPick111,Is.EqualTo(locked[0]),"Holding a key ring, the locked die is selected and the reason is given.");
            Assert.That(Button("UnlockDie111").interactable,Is.False);Assert.That(W7bButtonText("UnlockDie111"),Is.EqualTo("撤回"));
            Assert.That(W7bText("ActionReason"),Does.StartWith("不能撤回"+GameApp.DieName11(locked[0])).And.Contain("徒手"));
            yield return W7bShot("unlock-blocked-bare-hands");
            var refused=app.Send(new SessionCommand{kind=CommandKind.UnlockDie,targetDieId=locked[0]});
            Assert.That(refused.accepted,Is.False);Assert.That(refused.error,Is.EqualTo(GameSession.ItemsForbidden));
            Assert.That(StateJson(),Is.EqualTo(before),"A refused unlock uses nothing.");Assert.That(W7bKeyRing.usesRemaining,Is.EqualTo(3));
            // The same boss effect leaves a free unlock usable.
            W11Apply(W7bUnlockScene(new[]{"C154"},2,true,noItems,8));yield return W11Settle();
            locked=app.Session.State.hand.orderedLockedIds.ToArray();
            ClickDie(locked[0]);
            Assert.That(Button("UnlockDie111").interactable,Is.True);Assert.That(W7bButtonText("UnlockDie111"),Does.Contain("本手免费"));
            Click("UnlockDie111");yield return WaitForIdle();
            Assert.That(W7bLastReceipt.effects.Single(e=>e.kind=="DieUnlocked").sourceId,Is.EqualTo("C154"));
            // With the free unlock used and the key ring blocked, the reason says both.
            locked=app.Session.State.hand.orderedLockedIds.ToArray();ClickDie(locked[0]);
            Assert.That(Button("UnlockDie111").interactable,Is.False);
            Assert.That(W7bText("ActionReason"),Does.Contain("徒手").And.Contain(app.Config.Controller("C154").displayName));
            // Without any source a click on a locked die opens its details, as before p1.1.1.
            W11Apply(W7bUnlockScene(new string[0],2,false));yield return W11Settle();
            locked=app.Session.State.hand.orderedLockedIds.ToArray();
            ClickDie(locked[0]);
            Assert.That(app.UnlockPick111,Is.Null);Assert.That(HasActive("UnlockDie111"),Is.False);
            Assert.That(HasActive("CloseDialog"),Is.True,"The die's details opened.");
            Click("CloseDialog");
        }

        // ================================================================ records (RULES_DELTA 12.1 and 13 row 4)
        [UnityTest]
        public IEnumerator PLAY_V111_SettlementShowsTheRunRecordsAndTheMenuTheFormalBest()
        {
            StartRun("71114");
            string formalBefore=app.HistoricalHighestHandScore111;Assert.That(formalBefore,Is.EqualTo("0"),"A fresh save folder has no formal record.");
            // A debug scene that clears its stage with one hand.
            var scene=W11Scene(new string[0],new[]{4,4,4,4,2},"bbbbr",5,true,target:"1");scene.remainingSubmissions=1;
            W11Apply(scene);yield return W11Settle();
            Click("Submit");app.SkipVisual();yield return WaitForIdle();
            Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));
            if(app.SettlementRevealPlaying07)Click("SkipSettlementReveal07");
            var state=app.Session.State;
            Assert.That(state.highestHandScore,Is.Not.EqualTo("0"));Assert.That(state.stage.handRecordBroken,Is.True);
            Assert.That(RunRecords111.ClearRatioBroken(state),Is.True,"The first cleared stage sets the clear ratio.");
            Assert.That(W7bText("SettleRecordHand111"),Is.EqualTo("本局最高单手 "+PlayerNumberFormat.Exact(state.highestHandScore)+" · 本关新纪录"));
            Assert.That(W7bText("SettleRecordRatio111"),Is.EqualTo("最高过关倍数 "+RunRecords111.ClearRatio(state)+" · 本关新纪录"));
            Assert.That(RunRecords111.ClearRatio(state),Does.Match(@"^×[0-9,]+\.[0-9]$"),"One decimal, always written.");
            yield return W7bShot("settlement-records");
            // The records stay beside 明细 when the details are open.
            Click("SettlementDetailsToggle15");Assert.That(HasActive("SettleRecordHand111"),Is.True);Click("SettlementDetailsToggle15");
            // A debug run never feeds the formal record.
            Assert.That(app.HistoricalHighestHandScore111,Is.EqualTo(formalBefore));Assert.That(File.Exists(app.FormalRecordLocation111),Is.False);
            // The menu shows the formal record: none yet, then the value of the record file.
            W11Invoke("ReturnMenu");yield return null;
            Assert.That(W7bText("MenuRecord111"),Is.EqualTo("历史最高单手 暂无记录"));
            File.WriteAllText(app.FormalRecordLocation111,"{\"schema\":1,\"historicalHighestHandScore\":\"123456\"}");
            typeof(GameApp).GetField("formalRecords111",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(app,null);
            app.Refresh();yield return null;
            Assert.That(app.HistoricalHighestHandScore111,Is.EqualTo("123456"));
            Assert.That(W7bText("MenuRecord111"),Is.EqualTo("历史最高单手 123,456"));
            yield return Art073Settle(.8f);
            yield return W7bShot("menu-formal-record");
        }

        [UnityTest]
        public IEnumerator PLAY_V111_AFormalRunFeedsTheFormalRecordOnlyWhenItsBestHandRises()
        {
            StartRun("71115");Click("StartStage");Click("Roll");app.SkipVisual();yield return WaitForIdle();
            Assert.That(app.Session.State.isDebug,Is.False);Assert.That(app.HistoricalHighestHandScore111,Is.EqualTo("0"));
            string die=app.Session.State.dice.OrderBy(d=>d.createdSerial).First().dieId;
            ClickDie(die);Click("Confirm");yield return WaitForIdle();
            Click("Submit");app.SkipVisual();yield return WaitForIdle();
            var state=app.Session.State;
            Assert.That(state.highestHandScore,Is.Not.EqualTo("0"),"One die always scores at least its pips.");
            Assert.That(app.HistoricalHighestHandScore111,Is.EqualTo(state.highestHandScore),"The saved formal run raised the all-time record.");
            Assert.That(FormalRecordStore.TryParseRecord(File.ReadAllText(app.FormalRecordLocation111),out var stored),Is.True);
            Assert.That(stored.ToString(),Is.EqualTo(state.highestHandScore),"And it is on disk, beside the run save.");
            Assert.That(Path.GetDirectoryName(Path.GetFullPath(app.FormalRecordLocation111)),Is.EqualTo(Path.GetDirectoryName(Path.GetFullPath(app.SaveLocation))));
            // The same folder read again after a restart still has it, and the restored run does not change it.
            byte[] bytes=File.ReadAllBytes(app.FormalRecordLocation111);
            yield return LoadBootstrap();
            Assert.That(app.HistoricalHighestHandScore111,Is.EqualTo(state.highestHandScore));
            CollectionAssert.AreEqual(bytes,File.ReadAllBytes(app.FormalRecordLocation111),"Restoring the same run rewrites nothing.");
            Assert.That(W7bText("MenuRecord111"),Is.EqualTo("历史最高单手 "+PlayerNumberFormat.Exact(state.highestHandScore)));
        }

        // ================================================================ score tiers (RULES_DELTA 13 row 5)
        [UnityTest]
        public IEnumerator PLAY_V111_ScoreTiersStartAtOneThreeAndTenTargets()
        {
            StartRun("71116");
            // The hand is fixed (no cards, four blue fours and a two): read its score once, then set the target around it.
            W11Apply(W11Scene(new string[0],new[]{4,4,4,4,2},"bbbbr",5,true,target:"4000"));yield return W11Settle();
            FinalOptions07("Normal",false);
            var preview=app.Session.PreviewScoreRange();Assert.That(preview.maximum.score,Is.EqualTo(preview.minimum.score),"The sample hand has no random part.");
            var score=System.Numerics.BigInteger.Parse(preview.minimum.score);Assert.That(score>=10,Is.True,"The sample hand must reach ten points for a ten-target case.");
            var cases=new[]
            {
                (name:"below",target:(score+1).ToString(),tier:HighScore073.None,word:""),
                (name:"one",target:score.ToString(),tier:HighScore073.High,word:"一手达标！"),
                (name:"three",target:(score/3).ToString(),tier:HighScore073.Super,word:"三倍目标！"),
                (name:"ten",target:(score/10).ToString(),tier:HighScore073.Burst,word:"十倍目标！"),
            };
            foreach(var c in cases)
            {
                W11Apply(W11Scene(new string[0],new[]{4,4,4,4,2},"bbbbr",5,true,target:c.target));yield return W11Settle();
                Assert.That(app.Session.State.stage.target,Is.EqualTo(c.target));
                var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                Assert.That(result.receipt.submission.snapshot.stageTarget,Is.EqualTo(c.target),"The hand freezes its target.");
                Assert.That(result.receipt.submission.breakdown.score,Is.EqualTo(score.ToString()));
                yield return WaitFxPhase07(PresentationPhase.FinalScore);yield return null;
                Assert.That(app.HighScoreTierShown073,Is.EqualTo(c.tier),c.name+": "+score+" against "+c.target);
                if(c.tier==HighScore073.None)
                {
                    Assert.That(HasActive("TierWord11"),Is.False,"Below the target there is no tier word.");Assert.That(HasActive("TopGlow"),Is.False);
                }
                else
                {
                    Assert.That(W7bText("TierWord11"),Is.EqualTo(c.word));Assert.That(HasActive("TopGlow"),Is.True);
                    Assert.That(HasActive("TopGlowOuter"),Is.EqualTo(c.tier>=HighScore073.Super));Assert.That(HasActive("Crown"),Is.EqualTo(c.tier==HighScore073.Burst));
                }
                yield return W7bShot("score-tier-"+c.name);
                app.SkipVisual();yield return WaitForIdle();
            }
        }
    }
}
