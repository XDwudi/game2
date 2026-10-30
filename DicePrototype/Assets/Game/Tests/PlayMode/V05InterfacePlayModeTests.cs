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
            for(int i=0;i<20;i++)
            {
                Click("HeaderHandbook");
                var rows=app.canvas.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("HandbookRow_")).ToArray();
                Assert.That(rows.Length,Is.EqualTo(13));
                Assert.That(rows.Any(t=>t.name=="HandbookRow_eight_kind"),Is.True);
                Assert.That(FindActive<TextMeshProUGUI>("HandbookHint").text,Does.Contain("0级"));
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
            Click("Tutorial");Click("StartStage");Click("Roll");Click("Skip");
            foreach(var die in app.Session.State.dice)ClickDie(die.dieId);
            Click("Confirm");int gold=app.Session.State.gold;Click("Submit");yield return WaitForIdle();
            Assert.That(app.Session.State.phase,Is.EqualTo("HandReady"));Assert.That(app.Session.State.stage.qualified,Is.True);
            Assert.That(app.Session.State.gold,Is.EqualTo(gold));Assert.That(Button("EndStage").interactable,Is.True);
            Click("Roll");Click("Skip");ClickDie("D001");Assert.That(Button("EndStage").interactable,Is.False);
            Click("Confirm");var before=app.Session.State;var expected=app.Session.PreviewStageSettlement();
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
            yield return LoadBoundaryFixture(fixture);Click("ContinueRun");
            BuyBoundaryOffer("grow1");BuyBoundaryOffer("grow2");BuyBoundaryOffer("simple");BuyBoundaryOffer("pair-up");
            Assert.That(app.Session.EffectiveCapacity,Is.EqualTo(8));
            Click("HeaderHandbook");
            var pair=FindActive<Transform>("HandbookRow_pair");
            Assert.That(pair.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name=="Name").text,Does.Contain("1/8"));
            var straight=FindActive<Transform>("HandbookRow_straight");
            Assert.That(straight.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name=="Example").text,Does.Contain("4"));
            var eight=FindActive<Transform>("HandbookRow_eight_kind");
            Assert.That(eight.GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name=="Availability").text,Does.Contain("永久骰"));
            Click("CloseDialog");string before=StateJson();yield return LoadBootstrap();Click("ContinueRun");Click("HeaderHandbook");
            Assert.That(StateJson(),Is.EqualTo(before));Assert.That(FindActive<Transform>("HandbookRow_pair").GetComponentsInChildren<TextMeshProUGUI>().Single(t=>t.name=="Name").text,Does.Contain("1/8"));
        }

        [UnityTest]
        public IEnumerator PLAY_V05_EnchantedReplacementDisplaysNetPriceAndPersistsInstance()
        {
            var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();
            fixture.controllers.Add(BoundaryController(fixture, "C39"));
            AddBoundaryOffer(fixture,"replace-c39","controller","C39");
            fixture.shop.offers[0].controller.enchantmentId="base5";
            yield return LoadBoundaryFixture(fixture);Click("ContinueRun");OpenBoundaryOffer("replace-c39");
            Assert.That(DialogLabel("Capacity"),Does.Contain("旧卡回收 1"));Assert.That(DialogLabel("Enchantment"),Does.Contain("基础分 +5"));
            int gold=app.Session.State.gold;int net=app.Session.GetOfferNetPrice(app.Session.State.shop.offers[0]);Click("ConfirmPurchase");
            Assert.That(app.Session.State.gold,Is.EqualTo(gold-net));Assert.That(app.Session.State.controllers.Count,Is.EqualTo(1));
            Assert.That(app.Session.State.controllers.Single().enchantmentId,Is.EqualTo("base5"));
            string before=StateJson();yield return LoadBootstrap();Click("ContinueRun");
            Assert.That(StateJson(),Is.EqualTo(before));Assert.That(FindActive<TextMeshProUGUI>("ControllersTitle").text,Does.Contain("普通槽 1/4"));
        }
    }
}
