using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using BigInteger = System.Numerics.BigInteger;

namespace DiceCraft.Tests
{
    // v1.1 task W09 (text P0): number format (TX-01), names instead of internal ids and English labels (TX-02),
    // and one test for each interface-side error of text proposal section 5 (TX-08, items 2 to 7).
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest] public IEnumerator PLAY_V11_TEXT_NumberFormatUsesDecimalsSeparatorsAndShortLargeNumbers()
        {
            // Multipliers: at most two decimals, trailing zeros removed, "≈" when rounded (text proposal 3).
            Assert.That(PlayerNumberFormat.Ratio(702,5),Is.EqualTo("140.4"));
            Assert.That(PlayerNumberFormat.Ratio(39,10),Is.EqualTo("3.9"));
            Assert.That(PlayerNumberFormat.Ratio(9,2),Is.EqualTo("4.5"));
            Assert.That(PlayerNumberFormat.Ratio(702,25),Is.EqualTo("28.08"));
            Assert.That(PlayerNumberFormat.Ratio(1,3),Is.EqualTo("≈0.33"));
            Assert.That(PlayerNumberFormat.Ratio(2,3),Is.EqualTo("≈0.67"));
            Assert.That(PlayerNumberFormat.Ratio(243,32),Is.EqualTo("≈7.59"));
            Assert.That(PlayerNumberFormat.Ratio(-1,2),Is.EqualTo("−0.5"));
            Assert.That(PlayerNumberFormat.Times(702,5),Is.EqualTo("×140.4"));
            Assert.That(PlayerNumberFormat.Times(1,3),Is.EqualTo("≈×0.33"));
            Assert.That(PlayerNumberFormat.Times("39/10"),Is.EqualTo("×3.9"));
            // Thousands separators from four digits, gold included.
            Assert.That(PlayerNumberFormat.Ratio(38961,10),Is.EqualTo("3,896.1"));
            Assert.That(PlayerNumberFormat.Integer(999),Is.EqualTo("999"));
            Assert.That(PlayerNumberFormat.Integer(1000),Is.EqualTo("1,000"));
            Assert.That(PlayerNumberFormat.Gold(1000010),Is.EqualTo("1,000,010"));
            Assert.That(PlayerNumberFormat.Exact("38961/10"),Is.EqualTo("3,896.1"));
            // Up to fifteen digits in full; longer as "约 1.23×10^n" (DEV_PLAN 4.1 item 10), the exponent as a superscript.
            Assert.That(PlayerNumberFormat.Integer(BigInteger.Parse("999999999999999")),Is.EqualTo("999,999,999,999,999"));
            Assert.That(PlayerNumberFormat.Integer(BigInteger.Pow(10,15)),Is.EqualTo("约 1×10<sup>15</sup>"));
            Assert.That(PlayerNumberFormat.Integer(BigInteger.Parse("12345678901234567890")),Is.EqualTo("约 1.23×10<sup>19</sup>"));
            Assert.That(PlayerNumberFormat.Times(BigInteger.Parse("4294967296"),1,6),Is.EqualTo("约 ×4.29×10<sup>9</sup>"));
            Assert.That(PlayerNumberFormat.Integer(BigInteger.Parse("12345678901234567890"),PlayerNumberFormat.Unlimited),Is.EqualTo("12,345,678,901,234,567,890"));
            // Probabilities: one decimal.
            Assert.That(PlayerNumberFormat.Percent(166667,10000),Is.EqualTo("16.7%"));
            Assert.That(PlayerNumberFormat.Percent(0,10000),Is.EqualTo("0%"));
            Assert.That(PlayerNumberFormat.Percent(1000000,10000),Is.EqualTo("100%"));
            // Values that are not numbers pass through unchanged.
            Assert.That(PlayerNumberFormat.Exact("red"),Is.EqualTo("red"));
            Assert.That(PlayerNumberFormat.Exact("4,4"),Is.EqualTo("4,4"));
            yield return null;
        }

        [UnityTest] public IEnumerator PLAY_V11_TEXT_NamesReplaceIdsAndLabelsAreChinese()
        {
            Assert.That(GameApp.DieName11("D006"),Is.EqualTo("骰子 6"));
            Assert.That(GameApp.FaceName11("F02"),Is.EqualTo("第 2 面"));
            Assert.That(GameApp.DieFaceName11("D003","F02"),Is.EqualTo("骰子 3 的第 2 面"));
            Assert.That(GameApp.DieName11("$menu0"),Is.EqualTo("骰子"),"A non-player die id is never printed.");
            Assert.That(GameApp.SpeedLabel11("Instant"),Is.EqualTo("即时"));Assert.That(GameApp.SpeedLabel11("Fast"),Is.EqualTo("快速"));Assert.That(GameApp.SpeedLabel11("Normal"),Is.EqualTo("标准"));
            // The table header no longer prints the art-set name; the menu no longer prints version or build.
            Assert.That(HasActive("Version"),Is.False);
            StartRun("71101");Click("StartStage");
            var edition=FindActive<RectTransform>("Stats07").Find("Edition").GetComponent<TextMeshProUGUI>();
            Assert.That(edition.gameObject.activeSelf,Is.True);Assert.That(edition.text,Is.Empty);
            // Settings: localized speed option and key names (TX-02, English enum names).
            Click("Pause");Click("Settings");Click("SettingsTab2");
            Assert.That(Button("Speed").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("标准  >"));
            Assert.That(Button("BindSubmit").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("回车"));
            Assert.That(Button("BindClear").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("退格"));
            Assert.That(Button("BindRoll").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("R"));
            Click("SettingsDone");yield return null;
        }

        // Text proposal 5 #2: the debug password row is hidden from players; Ctrl+Shift+D on the settings page reveals it.
        [UnityTest] public IEnumerator PLAY_V11_TEXT_DebugPasswordRowIsHiddenUntilTheSettingsChord()
        {
            using(var keyboard=new RoutedKeyboard())
            {
                // The chord outside the settings page does nothing.
                yield return keyboard.PressChord(Key.LeftCtrl,Key.LeftShift,Key.D);
                Click("MenuSettings");Click("SettingsTab2");
                Assert.That(HasActive("DebugAccessLabel")||HasActive("DebugPassword")||HasActive("UnlockDebug"),Is.False,"Players never see the debug entry.");
                // D alone or without Shift does not reveal it either.
                yield return keyboard.Press(Key.D);yield return keyboard.PressChord(Key.LeftCtrl,Key.D);
                Assert.That(HasActive("DebugPassword"),Is.False);
                yield return keyboard.PressChord(Key.LeftCtrl,Key.LeftShift,Key.D);
                Assert.That(HasActive("DebugAccessLabel")&&HasActive("DebugPassword")&&HasActive("UnlockDebug"),Is.True,"The developer chord reveals the existing entry.");
                Assert.That(app.DebugUnlocked,Is.False,"Revealing never unlocks.");
            }
            var field=FindActive<TMP_InputField>("DebugPassword");field.text="xd is god";Click("UnlockDebug");
            Assert.That(app.DebugUnlocked,Is.False);Assert.That(FindActive<TextMeshProUGUI>("DebugPasswordError").text,Does.Contain("口令不匹配"));
            yield return null;field.text="XD is god";Click("UnlockDebug");
            Assert.That(app.DebugUnlocked,Is.True,"The existing unlock keeps working once revealed.");
            Click("CloseDialog");Assert.That(app.Session.State,Is.Null);
        }

        // Text proposal 5 #3: the self-upgrade title counts the uses left from the configuration (two per shop), not "一次".
        [UnityTest] public IEnumerator PLAY_V11_TEXT_SelfUpgradeTitleCountsUsesFromTheConfiguration()
        {
            var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();
            int limit=app.Config.shop.selfStudyLimit;Assert.That(limit,Is.GreaterThan(1),"The shipped configuration allows several self-upgrades per shop.");
            Assert.That(Button("Study").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("指定升级 · 加价 "+app.Config.shop.selfStudyPremium));
            Click("Study");
            Assert.That(FindActive<TextMeshProUGUI>("DialogTitle").text,Is.EqualTo("指定升级 · 本店还剩 "+limit+" 次"));
            Click("StudyBuypair");yield return WaitForIdle();
            Assert.That(app.Session.State.shop.selfStudyUsed,Is.EqualTo(1));
            Assert.That(Button("Study").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("指定升级 · 还剩 "+(limit-1)+" 次"));
            Click("Study");
            Assert.That(FindActive<TextMeshProUGUI>("DialogTitle").text,Is.EqualTo("指定升级 · 本店还剩 "+(limit-1)+" 次"));
            Click("CloseDialog");
        }

        // Text proposal 5 #4: "续交 C01 +0 / 次" no longer shows without a piggy bank, and names the card when it does.
        [UnityTest] public IEnumerator PLAY_V11_TEXT_ContinueIncomeLineNeedsThePiggyBankAndNamesIt()
        {
            StartRun("71104");
            var scene=InformationScene07(Art07FixtureKind.Qualified);
            Assert.That(scene.controllers.Any(c=>c.definitionId=="C01"||c.definitionId=="C41"),Is.False,"This fixture holds no piggy bank and no copy of one.");
            ApplyInformationScene07(scene);Click("Submit");app.SkipVisual();yield return WaitForIdle();
            // The reward lines prove the qualified sidebar of an active hand is drawn (the saved badge hides while the stamp plays).
            Assert.That(app.Session.State.stage.qualified,Is.True);Assert.That(HasActive("QualifiedReward0"),Is.True);
            Assert.That(HasActive("QualifiedContinueIncome"),Is.False,"No continue-income line without a piggy bank.");
            scene=InformationScene07(Art07FixtureKind.Qualified);
            scene.controllers[0].definitionId="C01";scene.controllers[0].enchantmentId="none";scene.controllers[0].state=new ControllerDynamicState();
            ApplyInformationScene07(scene);Click("Submit");app.SkipVisual();yield return WaitForIdle();
            Assert.That(app.Session.State.stage.qualified,Is.True);
            int income=app.Config.Controller("C01").parameters.goldPerValidSubmission;
            Assert.That(InformationText07("QualifiedContinueIncome"),Is.EqualTo("续交 "+app.Config.Controller("C01").displayName+"  +"+income+" / 次"));
            Assert.That(InformationText07("QualifiedContinueIncome"),Does.Not.Contain("C01"));
        }

        // Text proposal 5 #5: practice from the handbook shows practice labels instead of debug ones and the normal guide.
        [UnityTest] public IEnumerator PLAY_V11_TEXT_PracticeShowsPracticeLabelsAndTheFirstRunGuide()
        {
            PlayerPrefs.SetInt("art07.guideSeen",0);yield return LoadBootstrap();
            Click("MenuHandbook");Click("Tutorial");
            Assert.That(app.Session.State.debugWorkspace,Is.True,"Practice keeps its separate session and save rules.");
            Assert.That(FindActive<RectTransform>("Stats07").Find("Edition").GetComponent<TextMeshProUGUI>().text,Is.EqualTo("练习模式"));
            Assert.That(HasActive("DebugMark"),Is.False,"No debug marker in practice.");
            Click("StartStage");
            Assert.That(FindActive<TextMeshProUGUI>("GuideTitle").text,Does.StartWith("1 / 5"),"The first-run guide behaves as in an ordinary run.");
            Click("DismissGuide");
            Click("Roll");app.SkipVisual();yield return WaitForIdle();
            foreach(var die in app.Session.State.dice)ClickDie(die.dieId);
            Click("Confirm");yield return WaitForIdle();Click("Submit");yield return WaitForIdle();
            Assert.That(app.Session.State.stage.qualified,Is.True);
            Click("EndStage");yield return WaitForIdle();Click("SettlementContinue");yield return WaitForIdle();
            Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));
            Assert.That(InformationText07("ControllerCapacity07"),Does.Contain("练习模式").And.Not.Contain("调试"));
            Click("Pause");Click("MainMenu");Assert.That(app.Session.State,Is.Null,"Practice never becomes the ordinary save.");
        }

        // Text proposal 5 #6: the handbook's scoring order includes the item ×2 and the boss halving, after the enchantments.
        [UnityTest] public IEnumerator PLAY_V11_TEXT_HandbookScoringOrderIncludesItemAndBossFactors()
        {
            Click("MenuHandbook");Click("HandbookRules09");
            Assert.That(InformationText07("Rule09Title0"),Is.EqualTo("计分顺序"));
            string body=InformationText07("Rule09Body0");
            Assert.That(body,Does.Contain(app.Config.Consumable("I09").displayName).And.Contain("×2").And.Contain("减半").And.Contain("×0.5"));
            int enchant=body.IndexOf("倍率附魔",System.StringComparison.Ordinal),item=body.IndexOf(app.Config.Consumable("I09").displayName,System.StringComparison.Ordinal);
            int boss=body.IndexOf("减半",System.StringComparison.Ordinal),floor=body.IndexOf("向下取整",System.StringComparison.Ordinal);
            Assert.That(enchant,Is.GreaterThanOrEqualTo(0));
            Assert.That(enchant<item&&item<boss&&boss<floor,Is.True,"ScoreCalculator applies the item factor, then the boss half, after the enchantments and before the floor.");
            Assert.That(body,Does.Not.Contain("控制器"),"D2: the cards are called 卡牌.");
            Click("CloseDialog");yield return null;
        }

        // Text proposal 5 #7: the shop's next-stage target applies the boss modifier exactly as "查看关卡" does.
        [UnityTest] public IEnumerator PLAY_V11_TEXT_ShopNextTargetMatchesStageIntelWithBossModifier()
        {
            const string modifier="B11"; // 厚墙: target ×1.5 (TargetPercent)
            Assert.That(app.Config.Modifier(modifier).effects,Does.Contain("TargetPercent"));
            var fixture=NewBoundaryFixture("v11-boss-next-shop");fixture.cycles[0].modifierId=modifier;
            foreach(var die in fixture.dice)foreach(var face in die.faces){face.permanentPip=4;face.baseColor="red";}
            var memory=new MemorySaveStore();var session=new GameSession(app.Config,memory,fixture);
            for(int stage=0;stage<2;stage++)
            {
                if(stage>0)CoreShopStep(session,CommandKind.LeaveShop);
                CoreShopStep(session,CommandKind.StartStage);
                var started=session.State;started.stage.target="1";session=new GameSession(app.Config,memory,started);
                CoreShopStep(session,CommandKind.Roll);
                foreach(var die in session.State.dice)Assert.That(session.TogglePending(die.dieId),Is.True);
                CoreShopStep(session,CommandKind.ConfirmLocks);CoreShopStep(session,CommandKind.Submit);CoreShopStep(session,CommandKind.EndStage);CoreShopStep(session,CommandKind.Continue);
            }
            var shop=session.State;Assert.That(shop.phase,Is.EqualTo("Shop"));Assert.That(shop.stage.stageIndex,Is.EqualTo(1));
            Assert.That(app.Config.stages[2].stageKind,Is.EqualTo("boss"));
            yield return LoadBoundaryFixture(shop);ResumeBoundaryFixture();
            string scaled=PlayerNumberFormat.Exact(ExactScoreMath.SaveDecimal(ExactScoreMath.Parse(app.Config.stages[2].normalTarget)*app.Config.Modifier(modifier).amount/100));
            Assert.That(scaled,Is.Not.EqualTo(PlayerNumberFormat.Exact(app.Config.stages[2].normalTarget)));
            // V1.1 (W14, SHOP_LAYOUT 3): the next stage is in the top bar's middle (v0.73: the left column's NextStageValue).
            Assert.That(InformationText07("NextStage14").Replace("\n"," "),Does.EndWith("目标 "+scaled),"The top bar includes the boss modifier.");
            Click("ViewCycle");
            Assert.That(FindActive<RectTransform>("CycleCard2").Find("Goal").GetComponent<TextMeshProUGUI>().text,Is.EqualTo(scaled),"Stage intel shows the same number.");
            Click("CloseCycle");
        }

        // User decision D5 (RULES_DELTA 1.1 item 9 and 8.3): a card bought from this shop's shelf resells for at most the price
        // paid until the player leaves. Three 砍价高手 bring 成长基金 (list price 4, ordinary resale 2) down to 1, so the
        // purchase summary and the fund detail must show 1 in this shop and the ordinary 2 after it.
        [UnityTest] public IEnumerator PLAY_V11_TEXT_SameShopResaleCapIsShownForAJustBoughtFund()
        {
            var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();
            Equip09(fixture,"C98","C98","C98");
            AddBoundaryOffer(fixture,"C52","controller","C52");
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();yield return WaitForIdle();
            yield return BuyBoundaryOffer("C52");
            var bought=app.Session.State.controllers.Single(c=>c.definitionId=="C52");
            Assert.That(app.Session.State.shop.paidControllerPrices.Single(p=>p.instanceId==bought.instanceId).paid,Is.EqualTo(1));
            Assert.That(ControllerRules.SalePrice(bought,app.Config),Is.EqualTo(2),"The ordinary resale is half of the list price.");
            Assert.That(app.Session.GetControllerSalePrice(bought.instanceId),Is.EqualTo(1),"Capped at the price paid in this shop.");
            Assert.That(app.LastTransactionSummary07,Does.Contain("卖价 1").And.Not.Contain("卖价 2"),"The summary uses the capped session price.");
            Click("Controller_"+bought.instanceId);
            Assert.That(InformationText07("FundBody"),Does.Contain("基础卖价 1（本店买入，离店前不超过买价）").And.Contain("= 当前卖价 1。"));
            // V1.1 (W14, SHOP_LAYOUT 8, UI_REWRITES V05.cs:172): the cap is said where the price is (v1.1 W09: "当前卖价 1。").
            Assert.That(InformationText07("PriceBody"),Does.StartWith("卖价 1（本店买入，不超过买价 1，离店后恢复）。"));
            Click("CloseDialog");
            Click("LeaveShop");yield return WaitForIdle();
            Assert.That(app.Session.State.phase,Is.EqualTo("StagePreview"));
            Click("Controller_"+bought.instanceId);
            Assert.That(InformationText07("FundBody"),Does.Contain("基础卖价 2（标价的一半）").And.Contain("= 当前卖价 2。"),"After the shop the ordinary price applies again.");
            Click("CloseDialog");
        }
    }
}
