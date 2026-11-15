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
    // p1.0.0 interface (docs/specs/v10/RULES_SPEC.md section 9): the three stage cards on the stage-select screen
    // and behind the shop's button, the boss modifier in a stage, disabled controllers, the settlement loot row,
    // the loot and gift dialog, and the new handbook rows. Every action goes through the buttons or commands a
    // player uses; fixtures are explicit debug checkpoints, not evidence of normal progression.
    public sealed partial class GameplayPlayModeTests
    {
        private string Text10(string name) => FindActive<TextMeshProUGUI>(name).text;
        private string CardText10(string card, string child) => FindActive<Transform>(card).GetComponentsInChildren<TextMeshProUGUI>(true).Single(t => t.name == child).text;
        /// <summary>The shop after the given stage (0-based) with a consistent settlement receipt.</summary>
        private RunState ShopAfterStage10(int index)
        {
            var x = ShopBoundaryFixture(); var def = app.Config.stages[index];
            x.stage.stageIndex = index; x.stage.stageId = def.stageId; x.stage.stageKind = def.stageKind; x.stage.target = def.normalTarget; x.stage.score = def.normalTarget;
            x.stage.settlementId = x.runId + "/" + def.stageId; x.lastStageSettlement.settlementId = x.stage.settlementId; x.lastStageSettlement.stageId = def.stageId;
            x.lastStageSettlement.score = x.stage.score; x.lastStageSettlement.target = x.stage.target; x.lastStageSettlement.stageKind = def.stageKind;
            x.shop.offers.Clear(); return x;
        }
        private static void AllRedFours10(RunState fixture)
        {
            foreach (var die in fixture.dice) foreach (var face in die.faces) { face.permanentPip = 4; face.baseColor = "red"; }
        }
        private IEnumerator PlayFiveRedFours10()
        {
            var roll = app.Send(new SessionCommand { kind = CommandKind.Roll }); Assert.That(roll.accepted, Is.True, roll.error); yield return WaitForIdle();
            foreach (var die in app.Session.State.dice.Take(app.Session.State.stage.capacity)) Assert.That(app.Session.TogglePending(die.dieId), Is.True);
            var locks = app.Send(new SessionCommand { kind = CommandKind.ConfirmLocks }); Assert.That(locks.accepted, Is.True, locks.error); yield return WaitForIdle();
            var submit = app.Send(new SessionCommand { kind = CommandKind.Submit }); Assert.That(submit.accepted, Is.True, submit.error); yield return WaitForIdle();
        }

        [UnityTest]
        public IEnumerator PLAY_V10_ShopButtonAndStageSelectShowTheThreeStagesOfTheCycle()
        {
            var fixture = ShopAfterStage10(0);
            fixture.cycles[0].modifierId = "B25"; fixture.cycles[0].lootKind = "L1";
            yield return LoadBoundaryFixture(fixture); ResumeBoundaryFixture(); yield return WaitForIdle();
            Assert.That(HasActive("ViewGifts"), Is.False, "No loot and no gift controller: no gift button.");
            Click("ViewCycle");
            Assert.That(CardText10("CycleCard0", "KindTag"), Is.EqualTo("普通")); Assert.That(CardText10("CycleCard1", "KindTag"), Is.EqualTo("高压")); Assert.That(CardText10("CycleCard2", "KindTag"), Is.EqualTo("首领"));
            Assert.That(CardText10("CycleCard0", "Cleared"), Does.Contain("已通过"));
            string options = string.Join(" | ", FindActive<Transform>("CycleCard1").GetComponentsInChildren<TextMeshProUGUI>(true).Where(t => t.name == "OptionInfo").Select(t => t.text));
            Assert.That(options, Does.Contain("600").And.Contain("+5").And.Contain("900").And.Contain("+10"));
            Assert.That(CardText10("CycleCard2", "ModifierName"), Is.EqualTo(app.Config.Modifier("B25").displayName));
            Assert.That(CardText10("CycleCard2", "ModifierText"), Is.EqualTo(app.Config.Modifier("B25").description));
            Assert.That(CardText10("CycleCard2", "Loot"), Does.Contain(app.Config.Loot("L1").displayName));
            Assert.That(CardText10("CycleCard2", "Goal"), Is.EqualTo("1,050").Or.EqualTo("1050"));
            Assert.That(FindActive<Transform>("CycleCard1").Find("CurrentCycleCard"), Is.Not.Null, "In the shop the panel highlights the next stage.");
            Assert.That(HasActive("Risk"), Is.False, "The shop panel is read-only.");
            Click("CloseCycle"); Assert.That(HasActive("CycleCard0"), Is.False);

            Click("LeaveShop"); yield return WaitForIdle();
            Assert.That(app.Session.State.phase, Is.EqualTo("StagePreview")); Assert.That(app.Session.State.stage.stageKind, Is.EqualTo("pressure"));
            Assert.That(CardText10("CycleCard2", "ModifierName"), Is.EqualTo(app.Config.Modifier("B25").displayName), "The boss of the cycle is shown two stages ahead.");
            Assert.That(Button("Normal").GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("✓ 已选择"));
            Click("Risk"); yield return null;
            Assert.That(Button("Risk").GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("✓ 已选择"));
            Click("StartStage"); yield return WaitForIdle();
            Assert.That(app.Session.State.stage.risk, Is.True); Assert.That(app.Session.State.stage.target, Is.EqualTo("900")); Assert.That(app.Session.State.stage.reward, Is.EqualTo(10));
            Assert.That(Text10("Chapter"), Does.Contain("高压挑战"));
        }

        [UnityTest]
        public IEnumerator PLAY_V10_BossStageDisablesCardsShowsItsModifierAndPaysLootInTheShop()
        {
            var fixture = ShopAfterStage10(1); AllRedFours10(fixture);
            fixture.cycles[0].modifierId = "B25"; fixture.cycles[0].lootKind = "L1";
            Equip09(fixture, "C50", "C46", "C13");
            var session = new GameSession(app.Config, new MemorySaveStore(), fixture); CoreShopStep(session, CommandKind.LeaveShop);
            yield return LoadBoundaryFixture(session.State); ResumeBoundaryFixture(); yield return WaitForIdle();
            Assert.That(app.Session.State.phase, Is.EqualTo("StagePreview")); Assert.That(app.Session.State.stage.modifierId, Is.EqualTo("B25"));
            Assert.That(FindActive<Transform>("CycleCard2").Find("CurrentCycleCard"), Is.Not.Null);
            Assert.That(Text10("ControllerOrderWarning"), Does.Contain("失效").And.Contain(app.Config.Controller("C50").displayName).And.Contain(app.Config.Controller("C46").displayName));
            Assert.That(CardState09("C50"), Is.EqualTo("首领关将失效")); Assert.That(CardState09("C13"), Is.Not.EqualTo("首领关将失效"));
            Click("StartStage"); yield return WaitForIdle();
            var state = app.Session.State;
            Assert.That(state.stage.disabledControllerIds.Length, Is.EqualTo(2));
            Assert.That(Text10("BossModifierName"), Does.Contain(app.Config.Modifier("B25").displayName)); Assert.That(Text10("BossModifierText"), Is.EqualTo(app.Config.Modifier("B25").description));
            Assert.That(CardState09("C50"), Is.EqualTo("本关失效")); Assert.That(CardState09("C46"), Is.EqualTo("本关失效"));
            Assert.That(Text10("Chapter"), Does.Contain("首领关"));
            yield return PlayFiveRedFours10();
            // C50 (+20) and C46 (x2) are disabled; two rolls are left unused (+3 each): (20 + 6) x 12 x 2 x 5.
            Assert.That(app.Session.State.lastSubmission.breakdown.score, Is.EqualTo("3120")); Assert.That(app.Session.State.stage.qualified, Is.True);
            var end = app.Send(new SessionCommand { kind = CommandKind.EndStage }); Assert.That(end.accepted, Is.True, end.error); yield return WaitForIdle();
            Assert.That(Text10("SettlementLoot10"), Does.Contain(app.Config.Loot("L1").displayName).And.Contain("商店"));
            Click("SettlementContinue"); yield return WaitForIdle();
            Assert.That(app.Session.State.phase, Is.EqualTo("Shop"));
            var loot = app.Session.State.shop.offers.Where(o => o.groupId == "LOOT").ToArray();
            Assert.That(loot.Count(o => o.category == "controller"), Is.EqualTo(3)); Assert.That(loot.Count(o => o.category == "gold"), Is.EqualTo(1));
            Assert.That(FindActive<Transform>("ControllerShelf").GetComponentsInChildren<Transform>(true).Any(t => loot.Any(o => t.name == "Offer_" + o.offerId)), Is.False, "Loot never mixes into the shelf.");
            Assert.That(Button("ViewGifts").GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("首领战利品")); Assert.That(HasActive("GiftsPending10"), Is.True);
            Click("ViewGifts");
            Assert.That(Text10("GiftTitle_LOOT"), Does.Contain(app.Config.Loot("L1").displayName).And.Contain("还可以领 1 件"));
            foreach (var offer in loot) Assert.That(HasActive("Product_" + offer.offerId), Is.True, offer.category);
            var pick = loot.First(o => o.category == "controller"); int owned = app.Session.State.controllers.Count; long gold = app.Session.State.gold;
            Assert.That(Button("Buy_" + pick.offerId).GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("领取"));
            Click("Buy_" + pick.offerId); yield return WaitForIdle();
            Assert.That(app.Session.State.controllers.Count, Is.EqualTo(owned + 1)); Assert.That(app.Session.State.controllers.Last().definitionId, Is.EqualTo(pick.contentId));
            Assert.That(app.Session.State.gold, Is.EqualTo(gold), "Loot is free.");
            Assert.That(HasActive("GiftsPending10"), Is.False);
            Click("ViewGifts");
            Assert.That(Text10("GiftTitle_LOOT"), Does.Contain("已领完"));
            foreach (var rest in loot.Where(o => o.offerId != pick.offerId)) Assert.That(Button("Buy_" + rest.offerId).interactable, Is.False, "The rest of the group is void.");
            Click("CloseDialog");
        }

        [UnityTest]
        public IEnumerator PLAY_V10_GiftControllerAddsAFreePickToTheNextShop()
        {
            var fixture = NewBoundaryFixture("v10-gift-fixture"); AllRedFours10(fixture);
            Equip09(fixture, "C111");
            var session = new GameSession(app.Config, new MemorySaveStore(), fixture); CoreShopStep(session, CommandKind.StartStage);
            yield return LoadBoundaryFixture(session.State); ResumeBoundaryFixture(); yield return WaitForIdle();
            yield return PlayFiveRedFours10();
            var end = app.Send(new SessionCommand { kind = CommandKind.EndStage }); Assert.That(end.accepted, Is.True, end.error); yield return WaitForIdle();
            Assert.That(HasActive("SettlementLoot10"), Is.False, "An ordinary stage has no loot.");
            Click("SettlementContinue"); yield return WaitForIdle();
            var gifts = app.Session.State.shop.offers.Where(o => o.groupId == "GIFT_C111").ToArray();
            Assert.That(gifts.Length, Is.EqualTo(3)); Assert.That(gifts.All(o => o.category == "consumable"), Is.True);
            Assert.That(Button("ViewGifts").GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("商店赠礼"));
            Click("ViewGifts");
            Assert.That(Text10("GiftTitle_GIFT_C111"), Does.Contain(app.Config.Controller("C111").displayName));
            Click("Buy_" + gifts[0].offerId); yield return WaitForIdle();
            Assert.That(app.Session.State.inventory.Single().contentId, Is.EqualTo(gifts[0].contentId));
            Assert.That(app.Session.ShopGroups().Single().closed, Is.True);
        }

        [UnityTest]
        public IEnumerator PLAY_V10_HandbookListsTheCycleRulesAndTheBossOfThisChapter()
        {
            var fixture = NewBoundaryFixture("v10-handbook-fixture"); fixture.cycles[0].modifierId = "B07";
            var session = new GameSession(app.Config, new MemorySaveStore(), fixture); CoreShopStep(session, CommandKind.StartStage);
            yield return LoadBoundaryFixture(session.State); ResumeBoundaryFixture(); yield return WaitForIdle();
            Click("Handbook"); Click("HandbookRules09");
            Assert.That(Text10("Rule09Title1"), Is.EqualTo("成长值"), "The v0.9 rows stay first.");
            Assert.That(Text10("Rule09Title8"), Is.EqualTo("三关一周期")); Assert.That(Text10("Rule09Body8"), Does.Contain("高压").And.Contain("首领"));
            Assert.That(Text10("Rule09Title10"), Is.EqualTo("封印"));
            Assert.That(Text10("Rule09Title15"), Is.EqualTo("本章首领 · " + app.Config.Modifier("B07").displayName)); Assert.That(Text10("Rule09Body15"), Does.Contain(app.Config.Modifier("B07").description));
            Click("CloseDialog");
        }
    }
}
