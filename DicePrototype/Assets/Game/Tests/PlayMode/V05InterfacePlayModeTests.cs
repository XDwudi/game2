using System;
using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_V05_HandbookTwentyReadsPreservePendingSaveAndAllRuleState()
        {
            StartRun("42");Click("StartStage");Click("Roll");Click("Skip");ClickDie("D001");
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            var pending=app.Session.PendingIds;
            Click("Handbook");Click("HandbookColors");
            Assert.That(HasActive("HandbookRow_all_red"),Is.True);Click("CloseDialog");
            for(int i=0;i<20;i++)
            {
                Click("Handbook");Click("HandbookHands");
                var rows=app.canvas.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("HandbookRow_")).ToArray();
                Assert.That(rows.Length,Is.EqualTo(14));
                Assert.That(rows.Any(t=>t.name=="HandbookRow_eight_kind"),Is.True);
                foreach(var row in rows.Where(r=>r.name!="HandbookRow_scatter"))
                    Assert.That(row.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name=="Level").text,Is.EqualTo("Lv.0 / "+app.Config.Hand(row.name.Substring("HandbookRow_".Length)).maxLevel));
                Click("CloseDialog");
                Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(pending,app.Session.PendingIds);
                CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
            using(var keyboard=new RoutedKeyboard())
            {
                yield return keyboard.Press(Key.H);Assert.That(HasActive("HandbookScroll"),Is.True);
                yield return keyboard.Press(Key.Enter);yield return keyboard.Press(Key.Space);yield return keyboard.Press(Key.E);
                Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(pending,app.Session.PendingIds);
                yield return keyboard.Press(Key.Escape);Assert.That(HasActive("HandbookScroll"),Is.False);
            }
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(pending,app.Session.PendingIds);
        }

        [UnityTest]
        public IEnumerator PLAY_V05_QualifiedContinuesAndEndDiscardsUnsubmittedHandWithOneReward()
        {
            Click("MenuHandbook");Click("Tutorial");Click("StartStage");Click("Roll");Click("Skip");
            foreach(var die in app.Session.State.dice)ClickDie(die.dieId);
            Click("Confirm");yield return WaitForIdle();long gold = app.Session.State.gold;Click("Submit");yield return WaitForIdle();
            Assert.That(app.Session.State.phase,Is.EqualTo("HandReady"));Assert.That(app.Session.State.stage.qualified,Is.True);
            Assert.That(app.Session.State.gold,Is.EqualTo(gold));Assert.That(Button("EndStage").interactable,Is.True);
            Click("Roll");Click("Skip");ClickDie("D001");Assert.That(Button("EndStage").interactable,Is.False);
            Click("Confirm");yield return WaitForIdle();var before=app.Session.State;var expected=app.Session.PreviewStageSettlement();
            Click("EndStage");var after=app.Session.State;
            Assert.That(after.phase,Is.EqualTo("StageSettlement"));Assert.That(after.stage.remainingSubmissions,Is.EqualTo(before.stage.remainingSubmissions));
            Assert.That(after.stage.submissionOrdinal,Is.EqualTo(before.stage.submissionOrdinal));Assert.That(after.stage.score,Is.EqualTo(before.stage.score));
            Assert.That(after.gold,Is.EqualTo(expected.goldAfter));
            var duplicate=app.Session.Execute(after.processedReceipts.Last().command);
            Assert.That(duplicate.duplicate,Is.True);Assert.That(app.Session.State.gold,Is.EqualTo(after.gold));
            Click("SettlementContinue");Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));
        }

        [UnityTest]
        public IEnumerator PLAY_V05_HandbookChangesWithCapacitySimpleStraightUpgradeAndReload()
        {
            var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();
            AddBoundaryOffer(fixture,"grow1","controller","C21");AddBoundaryOffer(fixture,"grow2","controller","C22");
            AddBoundaryOffer(fixture,"simple","controller","C29");AddBoundaryOffer(fixture,"pair-up","hand_upgrade","pair");
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();
            yield return BuyBoundaryOffer("grow1");yield return BuyBoundaryOffer("grow2");yield return BuyBoundaryOffer("simple");yield return BuyBoundaryOffer("pair-up");
            Assert.That(app.Session.EffectiveCapacity,Is.EqualTo(8));
            Click("Handbook");
            var pair=FindActive<Transform>("HandbookRow_pair");
            Assert.That(pair.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name=="Level").text,Does.Contain("Lv.1 / 100"));
            var straight=FindActive<Transform>("HandbookRow_straight");
            Assert.That(straight.Cast<Transform>().Count(t=>t.gameObject.activeSelf&&t.name.StartsWith("Example")),Is.EqualTo(4),"Simplified straight must show four actual dice examples.");
            var eight=FindActive<Transform>("HandbookRow_eight_kind");
            Click("HandbookRow_eight_kind");
            Assert.That(FindActive<TextMeshProUGUI>("HandbookSelection").text,Does.Contain("永久骰门槛"));
            Assert.That(eight.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name=="Reachable").text,Is.EqualTo("数量受限"));
            Click("CloseDialog");string before=StateJson();yield return LoadBootstrap();ResumeBoundaryFixture();Click("Handbook");
            Assert.That(StateJson(),Is.EqualTo(before));Assert.That(FindActive<Transform>("HandbookRow_pair").GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name=="Level").text,Does.Contain("Lv.1 / 100"));
        }

        [UnityTest]
        public IEnumerator PLAY_V05_EnchantedReplacementDisplaysNetPriceAndPersistsInstance()
        {
            var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();
            fixture.controllers.Add(BoundaryController(fixture, "C39"));
            AddBoundaryOffer(fixture,"replace-c39","controller","C39");
            fixture.shop.offers[0].controller.enchantmentId="base10";
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();yield return OpenBoundaryOffer("replace-c39");
            Assert.That(DialogLabel("TradeBody"),Does.Contain("旧卡回收 1"));Assert.That(FindActive<Transform>("NewCard").GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name=="Enchant").text,Does.Contain("+10"));
            long gold = app.Session.State.gold;long net=app.Session.GetOfferNetPrice(app.Session.State.shop.offers[0]);Click("ConfirmPurchase");
            Assert.That(app.Session.State.gold,Is.EqualTo(gold-net));Assert.That(app.Session.State.controllers.Count,Is.EqualTo(1));
            Assert.That(app.Session.State.controllers.Single().enchantmentId,Is.EqualTo("base10"));
            string before=StateJson();yield return LoadBootstrap();ResumeBoundaryFixture();
            Assert.That(StateJson(),Is.EqualTo(before));string controllerTitle=FindActive<TextMeshProUGUI>("ControllerCapacity07").text;
            Assert.That(controllerTitle,Does.Contain("调试配置"));Assert.That(controllerTitle,Does.Contain("普通占槽 1"));Assert.That(controllerTitle,Does.Not.Contain("/"));
        }
    }
}
