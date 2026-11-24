using System;
using System.Collections;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Verification;
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
        private static Rect ScreenRect10(RectTransform t) { var c = new Vector3[4]; t.GetWorldCorners(c); return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y); }
        /// <summary>The in-stage boss readout is on screen, not under a left-column button, and its description is not cut.</summary>
        private void AssertBossReadoutVisible10(string modifierId)
        {
            Canvas.ForceUpdateCanvases();
            var chip = ScreenRect10(FindActive<RectTransform>("BossModifier10"));
            Assert.That(chip.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(chip.yMin, Is.GreaterThanOrEqualTo(0)); Assert.That(chip.xMax, Is.LessThanOrEqualTo(Screen.width)); Assert.That(chip.yMax, Is.LessThanOrEqualTo(Screen.height));
            foreach (string name in new[] { "PreviousSubmission", "Handbook", "Pause" })
                if (HasActive(name)) Assert.That(chip.Overlaps(ScreenRect10(FindActive<RectTransform>(name))), Is.False, "The boss readout must not sit under the " + name + " button.");
            var body = FindActive<TextMeshProUGUI>("BossModifierText"); body.ForceMeshUpdate();
            // V1.1 (W11, PLAY_LAYOUT 1): the stage card prints the modifier's short text (config shortText, as on the cards);
            // the full description is in its hover and in the cycle panel.
            // V1.1 (W11b, PLAY_LAYOUT 8 row 33): a two-line effect breaks at the punctuation nearest its middle; that line
            // break is the only difference from the short text.
            Assert.That(body.text.Replace("\n", ""), Is.EqualTo(app.Config.Modifier(modifierId).shortText)); Assert.That(body.isTextOverflowing, Is.False, "The short text of " + modifierId + " must fit its box.");
            foreach(var paragraph in new[]{body,FindActive<TextMeshProUGUI>("BossLoot11")})
            {
                paragraph.ForceMeshUpdate();
                Assert.That(paragraph.textInfo.lineCount,Is.EqualTo(1+paragraph.text.Count(c=>c=='\n')),
                    modifierId+" "+paragraph.name+" must use explicit word-aware lines in the actual font");
            }
            // Both lines must really be drawn: every non-blank character has a visible glyph that lies inside the readout.
            foreach (var text in new[] { FindActive<TextMeshProUGUI>("BossModifierName"), body })
            {
                text.ForceMeshUpdate(); var info = text.textInfo; int drawn = 0;
                Assert.That(info.characterCount, Is.EqualTo(text.text.Length), text.name + " of " + modifierId + " lost characters");
                for (int i = 0; i < info.characterCount; i++)
                {
                    var c = info.characterInfo[i]; if (char.IsWhiteSpace(c.character)) continue;
                    Assert.That(c.isVisible, Is.True, text.name + " of " + modifierId + ": character " + c.character + " is not drawn"); drawn++;
                    Vector3 low = text.transform.TransformPoint(c.bottomLeft), high = text.transform.TransformPoint(c.topRight);
                    Assert.That(low.x, Is.GreaterThanOrEqualTo(chip.xMin - 1)); Assert.That(high.x, Is.LessThanOrEqualTo(chip.xMax + 1));
                    Assert.That(low.y, Is.GreaterThanOrEqualTo(chip.yMin - 1), text.name + " of " + modifierId + " hangs below the readout"); Assert.That(high.y, Is.LessThanOrEqualTo(chip.yMax + 1), text.name + " of " + modifierId + " rises above the readout");
                }
                Assert.That(drawn, Is.GreaterThan(0));
            }
            Assert.That(FindActive<TextMeshProUGUI>("BossModifierName").text, Does.Contain(app.Config.Modifier(modifierId).displayName));
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
            Assert.That(options, Does.Contain("目标 600").And.Contain("奖励 5 金币").And.Contain("目标 900").And.Contain("奖励 10 金币"));
            Assert.That(CardText10("CycleCard2", "ModifierName"), Is.EqualTo(app.Config.Modifier("B25").displayName));
            Assert.That(CardText10("CycleCard2", "ModifierText"), Is.EqualTo(app.Config.Modifier("B25").shortText));
            Assert.That(CardText10("CycleCard2", "Loot"), Does.Contain(app.Config.Loot("L1").displayName));
            Assert.That(CardText10("CycleCard2", "Goal"), Is.EqualTo("1,000").Or.EqualTo("1000"));
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
        public IEnumerator PLAY_V10_EveryBossModifierReadoutIsUncoveredAndFitsItsBoxDuringAStage()
        {
            // Explicit debug scenes: the same hand screen under each of the 39 modifiers. This only checks the readout
            // (position, not covered by the left-column buttons, description not cut); the rules are tested elsewhere.
            Art07Fixtures.Apply(app, Art07FixtureKind.G147); yield return WaitForIdle();
            foreach (var modifier in app.Config.bossModifiers)
            {
                var s = app.Session.State; var scene = Art07Fixtures.Create(app.Config, Art07FixtureKind.G147, s.nextItemSerial, s.nextDieSerial);
                scene.sourceRunId = s.sourceRunId; scene.stageIndex = modifier.tier == 4 ? app.Config.stages.Length - 1 : 2; scene.modifierId = modifier.id;
                var applied = app.Send(new SessionCommand { kind = CommandKind.ApplyDebugScene, debugScene = scene }); Assert.That(applied.accepted, Is.True, modifier.id + ": " + applied.error);
                yield return WaitForIdle();
                Assert.That(app.Session.State.stage.modifierId, Is.EqualTo(modifier.id)); Assert.That(Text10("BossModifierName"), Does.Contain(modifier.displayName));
                AssertBossReadoutVisible10(modifier.id);
                // Two real frames are kept beside the isolated save for a human to look at: a short and the longest description.
                if (modifier.id == "B06" || modifier.id == "B38") yield return ShopEntrancePng07("v10-boss-readout-" + modifier.id);
            }
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
            Assert.That(Text10("BossModifierName"), Does.Contain(app.Config.Modifier("B25").displayName)); Assert.That(Text10("BossModifierText"), Is.EqualTo(app.Config.Modifier("B25").shortText));
            Assert.That(CardState09("C50"), Is.EqualTo("本关失效")); Assert.That(CardState09("C46"), Is.EqualTo("本关失效"));
            Assert.That(Text10("Chapter"), Does.Contain("首领关"));
            double entryDeadline = Time.realtimeSinceStartupAsDouble + 3;
            while (app.BossEntryPlaying15 && Time.realtimeSinceStartupAsDouble < entryDeadline) yield return null;
            Assert.That(app.BossEntryPlaying15, Is.False, "Read the settled modifier after its entrance.");
            AssertBossReadoutVisible10("B25");
            yield return PlayFiveRedFours10();
            // After the first hand the previous-hand button exists and the qualified header is drawn: the readout must still be there, uncovered.
            Assert.That(HasActive("PreviousSubmission"), Is.True); AssertBossReadoutVisible10("B25");
            // C50 (+20) and C46 (x2) are disabled; two rolls are left unused (+3 each): (20 + 6) x 12 x 2 x 5.
            Assert.That(app.Session.State.lastSubmission.breakdown.score, Is.EqualTo("3120")); Assert.That(app.Session.State.stage.qualified, Is.True);
            var end = app.Send(new SessionCommand { kind = CommandKind.EndStage }); Assert.That(end.accepted, Is.True, end.error); yield return WaitForIdle();
            if (app.SettlementRevealPlaying07) Click("SkipSettlementReveal07");
            Assert.That(Text10("LootName15"), Is.EqualTo(app.Config.Loot("L1").displayName));
            Assert.That(Text10("LootShort15").Replace("\n",""),Is.EqualTo(app.Config.Loot("L1").shortText));
            Click("SettlementContinue"); yield return WaitForIdle();
            Assert.That(app.Session.State.phase, Is.EqualTo("Shop"));
            var loot = app.Session.State.shop.offers.Where(o => o.groupId == "LOOT").ToArray();
            Assert.That(loot.Count(o => o.category == "controller"), Is.EqualTo(3)); Assert.That(loot.Count(o => o.category == "gold"), Is.EqualTo(1));
            Assert.That(HasActive("ControllerShelf") || HasActive("ViewGifts"), Is.False);
            Assert.That(Text10("LootName15"),Is.EqualTo(app.Config.Loot("L1").displayName));
            Assert.That(Text10("LootStatus15"),Is.EqualTo("还可以领 1 件"));
            foreach (var offer in loot.Where(o=>o.category!="gold")) Assert.That(HasActive("Offer_" + offer.offerId), Is.True, offer.category);
            Assert.That(HasActive("LootCashOut15"),Is.True);
            var pick = loot.First(o => o.category == "controller"); int owned = app.Session.State.controllers.Count; long gold = app.Session.State.gold;
            Assert.That(Button("Buy_" + pick.offerId).GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo("领取"));
            Click("Buy_" + pick.offerId); yield return WaitForIdle();
            Assert.That(app.Session.State.controllers.Count, Is.EqualTo(owned + 1)); Assert.That(app.Session.State.controllers.Last().definitionId, Is.EqualTo(pick.contentId));
            Assert.That(app.Session.State.gold, Is.EqualTo(gold), "Loot is free.");
            Assert.That(HasActive("GiftsPending10"), Is.False);
            Assert.That(HasActive("ViewGifts"), Is.False);
            Assert.That(Text10("LootStatus15"), Is.EqualTo("已领完"));
            foreach (var rest in loot.Where(o => o.offerId != pick.offerId && o.category!="gold"))
                Assert.That(HasActive("Buy_" + rest.offerId), Is.False, "Unselected rewards have no claim button.");
            Click("LootEnterShop15");
            Assert.That(HasActive("ControllerShelf"),Is.True);Assert.That(HasActive("ViewGifts"),Is.False);
        }

        [UnityTest]
        public IEnumerator PLAY_V11_RewardHintsMatchTheActiveStageAndItsCardIncome()
        {
            // Normal-slot checkpoints. The modifier is drawn by the real seed. Start, roll, lock, submit,
            // hover and settlement all run through the player's regular Bootstrap controls.
            yield return CheckRewardHint11(5000, 2, "B04", false, new[] { "C133" }, false, 100);
            yield return CheckRewardHint11(5000, 2, "B04", false, new[] { "C95", "C133", "C01" }, false, 150);
            yield return CheckRewardHint11(5000, 0, "", false, new[] { "C95", "C133" }, false, 150);
            yield return CheckRewardHint11(5000, 1, "", true, new[] { "C133" }, false, 150);
            yield return CheckRewardHint11(5007, 2, "B03", false, new[] { "C133" }, false, 100);
            yield return CheckRewardHint11(5006, 5, "B12", false, new[] { "C133" }, false, 100);
            yield return CheckRewardHint11(5000, 2, "B04", false, new[] { "C95" }, true, 150);
            yield return CheckRewardHint11(5000, 2, "B04", false, new[] { "C131" }, false, 150);
        }

        private IEnumerator CheckRewardHint11(uint seed, int index, string modifier, bool risk, string[] cards, bool dispel, int font)
        {
            string context="stage "+index+" "+modifier+" "+string.Join("+",cards)+" font "+font+(dispel?" I08":"");
            var source=new GameSession(app.Config,new MemorySaveStore());
            var created=source.Execute(new SessionCommand{kind=CommandKind.NewRun,commandId="reward-forecast",seed=seed});
            Assert.That(created.accepted,Is.True,created.error);
            var s=source.State;var def=app.Config.stages[index];
            s.stage.stageIndex=index;s.stage.stageId=def.stageId;s.stage.stageKind=def.stageKind;
            s.stage.target=def.normalTarget;s.stage.reward=def.normalReward;
            s.stage.modifierId=def.stageKind=="boss"?s.cycles[index/3].modifierId:"";
            Assert.That(s.stage.modifierId,Is.EqualTo(modifier),context);
            AllRedFours10(s);Equip09(s,cards);s.gold=40;
            string item=dispel?BoundaryItem(s,"I08"):null;
            new AtomicSaveStore(app.SaveLocation,app.Config,SaveSlotPolicy.Normal).Save(s);
            PlayerPrefs.SetInt("art07.textScale",font);PlayerPrefs.SetInt("art07.instant",1);
            yield return LoadBootstrap();Click("ContinueRun");
            Assert.That(app.Session.State.isDebug,Is.False,context);
            if(risk)Click("Risk");
            Click("StartStage");yield return WaitForIdle();
            string normal="达标后结束：过关奖励 +"+(risk?def.riskReward:def.normalReward)+" 金币，剩下的每次提交 +"+app.Config.goldPerUnusedSubmission;
            const string voided="本关不发过关奖励和剩余提交奖励。";
            bool suppressed=modifier=="B04"&&!cards.Contains("C131");
            Assert.That(Text10("GoalReward11"),Is.EqualTo(suppressed?voided:normal),context);
            W11GlyphCheck(FindActive<TextMeshProUGUI>("GoalReward11"),context);
            if(suppressed)
                Assert.That(Text10("BossModifierText").Replace("\n",""),Is.EqualTo(app.Config.Modifier("B04").shortText),context);
            if(modifier=="B12")
            {
                Assert.That(app.Session.State.gold,Is.EqualTo(20),context);
                yield break; // The entry tax changes the wallet; the unqualified forecast still quotes normal rewards.
            }
            if(dispel)
            {
                Click("Item_"+item);Click("Use");yield return WaitForIdle();
                Assert.That(app.Session.State.stage.modifierNullified,Is.True,context);
                Assert.That(Text10("GoalReward11"),Is.EqualTo(normal),context);
                suppressed=false;
            }
            if(cards.Contains("C131"))Assert.That(app.Session.State.stage.modifierNullified,Is.True,context);

            Click("Roll");app.SkipVisual();yield return WaitForIdle();
            foreach(var die in app.Session.State.dice)ClickDie(die.dieId);
            Click("Confirm");app.SkipVisual();yield return WaitForIdle();
            Assert.That(Text10("GoalReward11"),Is.EqualTo(suppressed?voided:normal),context+" selecting");
            if(suppressed&&font==150)yield return W11Capture("reward-copy-B04-selecting-150");
            Click("Submit");app.SkipVisual();yield return WaitForIdle();
            Assert.That(app.Session.State.stage.qualified,Is.True,context);
            if(modifier=="B03")Assert.That(app.Session.State.gold,Is.EqualTo(40-s.dice.Count),context);
            var receipt=app.Session.PreviewStageSettlement();
            long salary=cards.Contains("C95")?app.Config.Controller("C95").parameters.goldPerStage:0;
            var hunter=app.Config.Controller("C133").parameters;
            long bounty=cards.Contains("C133")?(def.stageKind=="boss"?hunter.goldPerBoss:risk?hunter.goldPerPressure:0):0;
            Assert.That(receipt.rewardVoided,Is.EqualTo(suppressed),context);
            Assert.That(receipt.baseReward,Is.EqualTo(suppressed?0:risk?def.riskReward:def.normalReward),context);
            Assert.That(receipt.unusedSubmissionReward,Is.EqualTo(suppressed?0:receipt.remainingSubmissions*app.Config.goldPerUnusedSubmission),context);
            Assert.That(receipt.interest,Is.Positive,context);
            Assert.That(receipt.controllerIncome,Is.EqualTo(salary+bounty),context);
            Assert.That(receipt.bountyIncome,Is.EqualTo(bounty),context);
            string salaryText="铁饭碗 +"+salary,bountyText="赏金猎人 +"+bounty;
            Assert.That(Text10("QualifiedReward0"),Is.EqualTo("过关奖励  +"+receipt.baseReward+(bounty>0?"  ·  "+bountyText:"")),context);
            Assert.That(Text10("QualifiedReward1"),Is.EqualTo("剩余提交奖励  +"+receipt.unusedSubmissionReward),context);
            Assert.That(Text10("QualifiedReward2"),Is.EqualTo("利息  +"+receipt.interest+(salary>0?"  ·  "+salaryText:"")),context);
            for(int line=0;line<3;line++)W11GlyphCheck(FindActive<TextMeshProUGUI>("QualifiedReward"+line),context);
            var hover=Button("EndStage").GetComponent<Art07HoverTip>();
            var pointer=new UnityEngine.EventSystems.PointerEventData(UnityEngine.EventSystems.EventSystem.current);
            hover.OnPointerEnter(pointer);
            double deadline=Time.realtimeSinceStartupAsDouble+3;
            while(!HasActive("EndStagePreview07")&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            var panel=FindActive<RectTransform>("EndStagePreview07");
            var body=panel.Find("Body").GetComponent<TextMeshProUGUI>();
            var title=panel.Find("Title").GetComponent<TextMeshProUGUI>();
            var bounds=V06ScreenRect(panel);
            Assert.That(bounds.xMin>=0&&bounds.xMax<=Screen.width+1&&bounds.yMax<=Screen.height+1,Is.True,context+" tooltip fits screen");
            Assert.That(bounds.yMin,Is.GreaterThan(V06ScreenRect((RectTransform)Button("EndStage").transform).yMax),context+" tooltip clears its button");
            W11GlyphCheck(title,context);
            var lines=body.text.Split('\n');
            int pigs=cards.Count(id=>id=="C01");
            Assert.That(receipt.pigSourceCount,Is.EqualTo(pigs),context);
            long rate=1L+pigs*app.Config.Controller("C01").parameters.extraInterestPerTen;
            CollectionAssert.Contains(lines,"利息 +"+receipt.interest+"（每 "+app.Config.interestDivisor+" 金币 +"+rate+"）",context);
            CollectionAssert.AreEquivalent(new[]{salary>0?salaryText:null,bounty>0?bountyText:null}.Where(x=>x!=null),
                lines.Where(x=>x.StartsWith("铁饭碗")||x.StartsWith("赏金猎人")),context);
            W11GlyphCheck(body,context);
            Assert.That(V06ScreenRect(title.rectTransform).yMin,Is.GreaterThan(V06ScreenRect(body.rectTransform).yMax),context+" title clears body");
            Assert.That(V06ScreenRect(body.rectTransform).yMin,Is.GreaterThan(bounds.yMin),context+" body stays inside paper");
            if(suppressed&&font==150)yield return W11Capture("reward-copy-B04-qualified-hover-150");
            hover.OnPointerExit(pointer);Click("EndStage");yield return WaitForIdle();
            if(app.SettlementRevealPlaying07)Click("SkipSettlementReveal07");
            Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"),context);
            Assert.That(app.Session.State.lastStageSettlement.goldAfter,Is.EqualTo(receipt.goldAfter),context);
            Assert.That(HasActive("SettlementControllerIncome"),Is.EqualTo(salary>0),context);
            Assert.That(HasActive("SettlementBounty10"),Is.EqualTo(bounty>0),context);
            if(salary>0)Assert.That(Text10("SettlementControllerIncome"),Is.EqualTo(salaryText),context);
            if(bounty>0)Assert.That(Text10("SettlementBounty10"),Is.EqualTo(bountyText),context);
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
            Assert.That(HasActive("LootPlaque15"), Is.False, "An ordinary stage has no loot.");
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
            Assert.That(Text10("Rule09Title8"), Is.EqualTo("每章三关")); Assert.That(Text10("Rule09Body8"), Does.Contain("高压").And.Contain("首领"));
            Assert.That(Text10("Rule09Title10"), Is.EqualTo("封印"));
            Assert.That(Text10("Rule09Title15"), Is.EqualTo("本章首领 · " + app.Config.Modifier("B07").displayName));
            Assert.That(Text10("Rule09Body15"), Does.Contain(app.Config.Modifier("B07").shortText));
            Click("CloseDialog");
        }
    }
}
