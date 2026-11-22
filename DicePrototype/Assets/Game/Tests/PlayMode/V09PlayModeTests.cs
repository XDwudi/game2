using System;
using System.Collections;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    // p0.9.0 interface (docs/specs/v09/RULES_SPEC.md): per-run slots and the slot service in the real shop, the C98
    // discount on the price chip, growth values on the cards, the pool summary, the third handbook page and the
    // stage-clear income row. Every action goes through the same buttons or commands a player uses.
    public sealed partial class GameplayPlayModeTests
    {
        private string CardState09(string definitionId)
        {
            // V1.1 (W11): a table card with nothing to report has no state line (its short text is printed instead); read
            // that as an empty state. Only the drawn (active) state line counts.
            // V1.1 (W13, CARD_TEMPLATE §4): the state is the value badge on the card's picture, the number without "当前 ".
            string instance = app.Session.State.controllers.Single(c => c.definitionId == definitionId).instanceId;
            var state = FindActive<Transform>("Controller_" + instance).GetComponentsInChildren<TextMeshProUGUI>(false).FirstOrDefault(t => t.name == "State");
            return state == null ? "" : state.text;
        }
        private static void Equip09(RunState fixture, params string[] definitions)
        {
            fixture.controllers.Clear();
            foreach (string id in definitions) fixture.controllers.Add(BoundaryController(fixture, id));
        }
        private static ControllerInstance Card09(RunState fixture, string definitionId) => fixture.controllers.Single(c => c.definitionId == definitionId);

        [UnityTest]
        public IEnumerator PLAY_V09_ShopSellsASlotShowsTheDiscountAndUnblocksTheSixthController()
        {
            var fixture = ShopBoundaryFixture(); fixture.shop.offers.Clear();
            Equip09(fixture, "C98", "C55", "C58", "C02", "C06");
            AddBoundaryOffer(fixture, "C13", "controller", "C13");
            AddBoundaryOffer(fixture, "C42", "controller", "C42");
            AddBoundaryOffer(fixture, "O08", "service", "O08");
            AddBoundaryOffer(fixture, "O01", "service", "O01");
            AddBoundaryOffer(fixture, "U06", "die_upgrade", "U06");
            yield return LoadBoundaryFixture(fixture); ResumeBoundaryFixture(); yield return WaitForIdle();
            Assert.That(app.Session.State.controllerSlots, Is.EqualTo(5));
            // C98: controllers and die upgrades cost one less, never below one.
            AssertBoundaryCardPrice("C13", 1); AssertBoundaryCardPrice("C42", 5); AssertBoundaryCardPrice("U06", 2);
            AssertBoundaryCardPrice("O08", 8); AssertBoundaryCardPrice("O01", 2);
            // V1.1 (W14, CARD_TEMPLATE §5): the reason is on the price button (v0.73: a reason line OfferAvailability_<id>).
            Assert.That(PriceWord14("C13"), Is.EqualTo("卡牌栏位已满")); Assert.That(Button("Buy_C13").interactable, Is.False);
            long gold = app.Session.State.gold;
            yield return BuyBoundaryOffer("O08");
            Assert.That(app.Session.State.controllerSlots, Is.EqualTo(6)); Assert.That(app.Session.State.controllerSlotPurchases, Is.EqualTo(1));
            Assert.That(app.Session.State.gold, Is.EqualTo(gold - 8));
            Assert.That(PriceWord14("C13"), Is.EqualTo("购买"), "The sixth slot makes the card buyable."); Assert.That(Button("Buy_C13").interactable, Is.True);
            yield return BuyBoundaryOffer("C13");
            Assert.That(app.Session.State.controllers.Count, Is.EqualTo(6)); Assert.That(app.Session.OrdinaryControllerSlotsUsed, Is.EqualTo(6));
            Assert.That(app.Session.State.gold, Is.EqualTo(gold - 8 - 1));
            Assert.That(PriceWord14("C42"), Is.EqualTo("卡牌栏位已满"), "Six ordinary cards fill six slots.");
        }

        [UnityTest]
        public IEnumerator PLAY_V09_AltarVictimIsMarkedInTheShopAndNamedBeforeTheStageStarts()
        {
            var fixture = ShopBoundaryFixture(); fixture.shop.offers.Clear();
            Equip09(fixture, "C13", "C100", "C50");
            yield return LoadBoundaryFixture(fixture); ResumeBoundaryFixture(); yield return WaitForIdle();
            string altar = app.Session.State.controllers.Single(c => c.definitionId == "C100").instanceId, victim = app.Session.State.controllers.Single(c => c.definitionId == "C50").instanceId;
            Assert.That(FindActive<TextMeshProUGUI>("OwnedControllersTitle").text, Does.Contain("祭坛"));
            Assert.That(FindActive<Transform>("Controller_" + victim).Find("AltarVictim09"), Is.Not.Null, "The card right of the altar is outlined.");
            Assert.That(FindActive<Transform>("Controller_" + altar).Find("AltarVictim09"), Is.Null);
            Click("LeaveShop"); yield return WaitForIdle();
            Assert.That(app.Session.State.phase, Is.EqualTo("StagePreview"));
            Assert.That(FindActive<TextMeshProUGUI>("ControllerOrderWarning").text, Does.Contain("祭坛将摧毁").And.Contain(app.Config.Controller("C50").displayName));
            Assert.That(app.Session.State.controllers.Count, Is.EqualTo(3), "Nothing is destroyed before the stage starts.");
            Click("StartStage"); yield return WaitForIdle();
            Assert.That(app.Session.State.stage.started, Is.True);
            Assert.That(app.Session.State.controllers.Select(c => c.definitionId), Is.EqualTo(new[] { "C13", "C100" }));
            // C50 costs 4: the altar grows by 0.4.
            Assert.That(CardState09("C100"), Is.EqualTo("×1.4"));
        }

        [UnityTest]
        public IEnumerator PLAY_V09_HandScreenShowsGrowthPoolSummaryAndTheNewHandbookPage()
        {
            var fixture = NewBoundaryFixture("v09-hand-fixture");
            Equip09(fixture, "C55", "C58", "C70");
            Card09(fixture, "C55").state.growth = 24; Card09(fixture, "C58").state.growth = 8;
            for (int added = 0; added < 2; added++) fixture.dice.Add(DiceFactory.CreateStandard(app.Config, fixture.nextDieSerial++));
            var session = new GameSession(app.Config, new MemorySaveStore(), fixture);
            CoreShopStep(session, CommandKind.StartStage); CoreShopStep(session, CommandKind.Roll);
            Assert.That(session.TogglePending("D001"), Is.True); Assert.That(session.TogglePending("D002"), Is.True);
            CoreShopStep(session, CommandKind.ConfirmLocks);
            yield return LoadBoundaryFixture(session.State); ResumeBoundaryFixture(); yield return WaitForIdle();
            var state = app.Session.State;
            Assert.That(state.dice.Count, Is.EqualTo(7)); Assert.That(state.hand.orderedLockedIds.Count, Is.EqualTo(2));
            Assert.That(CardState09("C55"), Is.EqualTo("+24")); Assert.That(CardState09("C58"), Is.EqualTo("×1.4"));
            var left = state.hand.currentResults.Where(r => r.hasRolled && !r.isLocked).ToArray();
            string summary = FindActive<TextMeshProUGUI>("PoolSummary09").text;
            Assert.That(summary, Does.StartWith("留池 5 颗 · 点数和 " + left.Sum(r => r.currentPip) + " · 红 " + left.Count(r => r.currentColor == "red") + " · 6点 " + left.Count(r => r.currentPip == 6)));
            // The live preview already includes the pool card: +6 for each of the five dice left behind.
            var preview = app.Session.PreviewScoreRange().minimum;
            Assert.That(preview.controllerBaseAdds.Single(a => a.abilityId == "C70").amount, Is.EqualTo("30"));
            Assert.That(preview.controllerBaseAdds.Single(a => a.abilityId == "C55").amount, Is.EqualTo("24"));

            Click("Handbook"); Click("HandbookRules09");
            Assert.That(FindActive<TextMeshProUGUI>("Rule09Title1").text, Is.EqualTo("成长值"));
            Assert.That(FindActive<TextMeshProUGUI>("Rule09Title3").text, Is.EqualTo("留池骰")); Assert.That(FindActive<TextMeshProUGUI>("Rule09Body3").text, Does.Contain("不计分").And.Contain("板凳深度"));
            Assert.That(Button("HandbookRules09").interactable, Is.True);
            Click("HandbookHands"); Assert.That(HasActive("HandbookRow_pair"), Is.True); Assert.That(HasActive("Rule09Title1"), Is.False);
            Click("CloseDialog");

            // Submitting grows the real card after the score: C55 was worth 24 in this hand and shows 26 afterwards.
            var result = app.Send(new SessionCommand { kind = CommandKind.Submit }); Assert.That(result.accepted, Is.True, result.error);
            Assert.That(result.receipt.submission.breakdown.controllerBaseAdds.Single(a => a.abilityId == "C55").amount, Is.EqualTo("24"));
            yield return WaitForIdle();
            Assert.That(app.Session.State.controllers.Single(c => c.definitionId == "C55").state.growth, Is.EqualTo(26));
            Assert.That(CardState09("C55"), Is.EqualTo("+26"));
        }

        [UnityTest]
        public IEnumerator PLAY_V09_SettlementListsStageIncomeFromControllersAndTheWiderInterestCap()
        {
            var fixture = NewBoundaryFixture("v09-settlement-fixture");
            Equip09(fixture, "C95", "C96", "C58");
            foreach (var die in fixture.dice) foreach (var face in die.faces) { face.permanentPip = 4; face.baseColor = "red"; }
            fixture.gold = 40;
            var session = new GameSession(app.Config, new MemorySaveStore(), fixture);
            CoreShopStep(session, CommandKind.StartStage); CoreShopStep(session, CommandKind.Roll);
            foreach (var die in session.State.dice) Assert.That(session.TogglePending(die.dieId), Is.True);
            CoreShopStep(session, CommandKind.ConfirmLocks);
            yield return LoadBoundaryFixture(session.State); ResumeBoundaryFixture(); yield return WaitForIdle();
            var submit = app.Send(new SessionCommand { kind = CommandKind.Submit }); Assert.That(submit.accepted, Is.True, submit.error); yield return WaitForIdle();
            Assert.That(app.Session.State.stage.qualified, Is.True);
            var end = app.Send(new SessionCommand { kind = CommandKind.EndStage }); Assert.That(end.accepted, Is.True, end.error); yield return WaitForIdle();
            var r = app.Session.State.lastStageSettlement;
            // 40 gold: principal 40 under the widened cap of 50 -> interest 8; C95 pays 3; two unused submissions pay 2; stage reward 5.
            Assert.That(r.interestPrincipalCap, Is.EqualTo(50)); Assert.That(r.interest, Is.EqualTo(8)); Assert.That(r.controllerIncome, Is.EqualTo(3));
            Assert.That(r.goldAfter, Is.EqualTo(40 + 5 + 2 + 8 + 3));
            if(app.SettlementRevealPlaying07)Click("SkipSettlementReveal07");
            Assert.That(FindActive<TextMeshProUGUI>("SettlementControllerIncome").text, Is.EqualTo("铁饭碗 +3"));
            Assert.That(FindActive<TextMeshProUGUI>("SettlementInterest").text, Is.EqualTo("利息 +8"));
            Assert.That(FindActive<TextMeshProUGUI>("SettlementNetCash").text, Is.EqualTo("+18"));
            Assert.That(FindActive<TextMeshProUGUI>("SettleGold15").text, Is.EqualTo("金币 40 → 58"));
            Click("SettlementDetailsToggle15");
            Assert.That(FindActive<TextMeshProUGUI>("SettlementInterestDetail").text, Does.Contain("每 5 金币 +1").And.Contain("利滚利：最多 +10"));
            Assert.That(FindActive<TextMeshProUGUI>("OtherChange15_0").text,Does.Contain(app.Config.Controller("C58").displayName));
            Assert.That(app.Session.State.controllers.Single(c => c.definitionId == "C58").state.growth, Is.EqualTo(4), "C58 grows when the stage is cleared.");
        }
    }
}
