using System;
using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private DebugScene InformationScene07(Art07FixtureKind kind)
        {
            Assert.That(app.UnlockDebug("XD is god"),Is.True);
            Assert.That(app.EnterDebugWorkspace(),Is.True);
            var s=app.Session.State;
            var scene=Art07Fixtures.Create(app.Config,kind,s.nextItemSerial,s.nextDieSerial);
            scene.sourceRunId=s.sourceRunId;return scene;
        }
        private void ApplyInformationScene07(DebugScene scene)
        {
            var result=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});
            Assert.That(result.accepted,Is.True,result.error);app.SkipVisual();
            if(HasActive("RunDebugScene"))Click("RunDebugScene");
            Canvas.ForceUpdateCanvases();
        }
        private string InformationText07(string id)=>FindActive<TextMeshProUGUI>(id).text;
        private string InformationButton07(string id)=>Button(id).GetComponentInChildren<TextMeshProUGUI>().text;

        [TestCase(null, "理财顾问：计息的金币上限+10；道具打折有效，最低1金币。")]
        [TestCase("12", "理财顾问：利息上限提高到每关 +12")]
        public void PLAY_W16B_AdvisorOnlyShowsTheRecordedEffectiveMaximum(string detail,string expected)
        {
            var effect=new EffectRecord{kind="InterestCapRaised",sourceId="O10",newValue=35,detail=detail};
            var method=typeof(GameApp).GetMethod("EffectText10",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic);
            Assert.That(method.Invoke(app,new object[]{effect}),Is.EqualTo(expected));
        }

        [UnityTest] public IEnumerator PLAY_ART07_InvalidSeedKeepsDialogAndInputUntilCorrected()
        {
            Click("NewRun");var input=FindActive<TMP_InputField>("SeedInput");
            foreach(string invalid in new[]{"not-a-seed","4294967296","-1"})
            {
                input.text=invalid;Click("StartNew");yield return null;
                Assert.That(FindActive<TMP_InputField>("SeedInput"),Is.SameAs(input));
                Assert.That(input.text,Is.EqualTo(invalid));Assert.That(InformationText07("SeedError"),Is.EqualTo("种子只能是 0 到 4294967295 的整数。"));
                Assert.That(app.Session.State,Is.Null);Assert.That(File.Exists(app.SaveLocation),Is.False);
            }
            input.text="70791";Click("StartNew");Assert.That(app.Session.State.seed,Is.EqualTo(70791));
            Assert.That(HasActive("SeedInput"),Is.False);Assert.That(File.Exists(app.SaveLocation),Is.True);
        }

        [UnityTest] public IEnumerator PLAY_ART07_ScoreStructureChosenItemAndZeroReasonRemainIndependent()
        {
            StartRun("70792");var scene=InformationScene07(Art07FixtureKind.ZeroMultiplier);
            scene.dice.RemoveRange(3,2);scene.currentResults.RemoveRange(3,2);scene.orderedLockedIds.RemoveRange(3,2);
            scene.handLevels.Single(h=>h.handId=="pair").level=8;scene.previousPrimaryHandId="trips";
            ApplyInformationScene07(scene);
            var preview=app.Session.PreviewScoreRange().minimum;
            Assert.That(preview.primaryHandId,Is.EqualTo("trips"));Assert.That(preview.chosenGroupId,Is.EqualTo("pair"));
            Assert.That(InformationText07("Hand"),Does.Contain("结构 三条／计分 一对"));
            // p1.1.1 (W7a; RULES_DELTA 4.2 C49): 千年老二 without a five halves the hand instead of zeroing it, so the card chip
            // reads the half and the hand scores (6 + 2 unused rolls x 3) x pair level 8 (x8) x bonus line 1.25 x 1/2 = 60 (was 0).
            Assert.That(InformationButton07("Factor4"),Does.Contain("×0.5").And.Not.Contain("没有有效"));
            Assert.That(InformationText07("FinalScore"),Is.EqualTo("60"));Assert.That(Button("Submit").interactable,Is.True);
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            Click("Factor4");Click("CloseDialog");Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            Click("Submit");app.SkipVisual();
            Assert.That(app.Session.State.lastSubmission.breakdown.score,Is.EqualTo("60"));
            Assert.That(app.Session.State.lastEffects.Any(e=>e.kind=="SubmissionIncome"),Is.True);
            Assert.That(app.Session.State.lastEffects.Any(e=>e.kind=="HandGrown"),Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator PLAY_W16B_MimicChainNamesBothFrozenPositionsWithoutChangingTheSession()
        {
            var fixture=NewBoundaryFixture("w16b-mimic");
            Equip09(fixture,"C41","C41","C53");
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();Click("StartStage");
            yield return WaitForIdle();
            string before=StateJson();
            byte[] disk=File.ReadAllBytes(app.SaveLocation);
            Click("Controller_"+fixture.controllers[0].instanceId);
            string state=InformationText07("CurrentBody");
            Assert.That(state,Does.StartWith("右边：第 2 张 "+app.Config.Controller("C41").displayName+"\n实际复制：第 3 张 "+app.Config.Controller("C53").displayName));
            Assert.That(state,Does.Not.Contain("ITEM_").And.Not.Contain("上一手抽到"));
            Click("CloseDialog");
            Click("Controller_"+fixture.controllers[1].instanceId);
            Assert.That(InformationText07("CurrentBody"),Does.StartWith("复制：右边的"+app.Config.Controller("C53").displayName));
            Click("CloseDialog");
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
        }

        [UnityTest] public IEnumerator PLAY_ART07_RandomNineBranchesSharedOwnerAreExactAndReadOnly()
        {
            StartRun("70793");var scene=InformationScene07(Art07FixtureKind.SharedHalf);scene.randomOverrides.Clear();ApplyInformationScene07(scene);
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);var frozen=app.Session.PreviewSnapshot();
            string owner=app.Session.PreviewScoreRange().randomOwnerIds.Single();
            Assert.That(InformationText07("Hand"),Does.Contain("随机结果范围"));Click("RandomPreview");
            Assert.That(InformationText07("RandomOwner"+owner+"Title"),Does.StartWith("错乱乘积（第 "));
            Assert.That(InformationText07("RandomOwner"+owner+"Body"),Is.EqualTo("提交时才抽取倍率，模仿它的卡用同一个结果。"));
            int[] choices=app.Config.Controller("C53").parameters.factorUnits2;
            for(int i=0;i<choices.Length;i++)
            {
                var branch=frozen.Clone();branch.resolvedRandomChoices=new[]{new RandomMultiplierChoice{ownerInstanceId=owner,units2=choices[i]}};
                var exact=ScoreCalculator.Preview(branch,app.Config);Assert.That(exact.isRandom,Is.False);
                // v1.1 (W09, text proposal 3): the branch score has thousands separators (same digits, PlayerNumberFormat).
                Assert.That(InformationText07("RandomScore"+i),Is.EqualTo(PlayerNumberFormat.Exact(exact.minimum.score)));
                Assert.That(InformationText07("RandomFactor"+i),Is.EqualTo("×"+(choices[i]/2m).ToString("0.#")));
            }
            Assert.That(choices.Length,Is.EqualTo(9));
            for(int i=0;i<choices.Length;i++)
            {
                Click("RandomBranch"+i);
                Assert.That(HasActive("CopyExactScore"),Is.True);
                Click("CloseDialog");
                if(i+1<choices.Length)Click("RandomPreview");
            }
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            scene=InformationScene07(Art07FixtureKind.SharedHalf);ApplyInformationScene07(scene);
            before=StateJson();disk=File.ReadAllBytes(app.SaveLocation);Click("RandomPreview");
            Assert.That(InformationText07("RandomRangeBody"),Does.Contain("仅下一次调试覆盖"));Assert.That(HasActive("RandomBranch0"),Is.False);
            Click("CloseDialog");Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            scene=InformationScene07(Art07FixtureKind.SharedHalf);scene.randomOverrides.Clear();
            var extra=scene.controllers[1].Clone();extra.acquiredSerial+=1;extra.instanceId="ITEM_"+extra.acquiredSerial.ToString("D5");scene.controllers.Add(extra);
            ApplyInformationScene07(scene);before=StateJson();Click("RandomPreview");Assert.That(InformationText07("RandomMultipleBody"),Is.EqualTo("每张各抽一次倍率，这里只列出总分范围。"));
            Assert.That(HasActive("RandomBranch0"),Is.False);Click("CloseDialog");Assert.That(StateJson(),Is.EqualTo(before));yield return null;
        }

        [UnityTest] public IEnumerator PLAY_ART07_SettlementUsesActualIncomeFundAndTemporaryCleanupReceipts()
        {
            StartRun("70794");var scene=InformationScene07(Art07FixtureKind.GoldenSample);
            var mimic=scene.controllers.Single(c=>c.definitionId=="C41");mimic.state.fundAccrual=3;
            var fund=scene.controllers.Single(c=>c.definitionId=="C52");fund.state.fundAccrual=6;
            var income=scene.controllers.Single(c=>c.definitionId=="C02");income.definitionId="C01";income.enchantmentId="none";
            scene.controllers=new System.Collections.Generic.List<ControllerInstance>{mimic,fund,income};scene.target="1";
            ApplyInformationScene07(scene);Click("Submit");app.SkipVisual();Click("EndStage");app.SkipVisual();
            Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));var s=app.Session.State;
            var settlement=s.lastStageSettlement;var receipt=s.processedReceipts.Last(q=>q.settlement?.settlementId==settlement.settlementId);
            long earned=s.processedReceipts.Where(q=>q.submission!=null).SelectMany(q=>q.effects).Where(e=>e.kind=="SubmissionIncome").Sum(e=>e.newValue);
            Assert.That(InformationText07("SettlementNetCash"),Is.EqualTo("+"+PlayerNumberFormat.Gold(settlement.goldAfter-settlement.goldBefore)));
            Click("SettlementDetailsToggle15");
            Assert.That(earned,Is.GreaterThan(0));Assert.That(InformationText07("SubmissionIncome"),Is.EqualTo("本关提交时已得 +"+earned+" 金币（已算在上面的金币里）"));
            // v1.1 (W09, TX-02): each fund line names its card (both names are unique here), never its instance id.
            string fundText=InformationText07("FundGrowth");
            Assert.That(fundText,Does.Contain(app.Config.Controller("C41").displayName+"卖价 +3（现价 "+app.Session.ControllerSalePriceIn(s,s.controllers.Single(c=>c.instanceId==mimic.instanceId))+"，卖出时才得到）").And.Contain(app.Config.Controller("C52").displayName+"卖价 +3（现价 "+app.Session.ControllerSalePriceIn(s,s.controllers.Single(c=>c.instanceId==fund.instanceId))+"，卖出时才得到）"));
            Assert.That(fundText,Does.Not.Contain(mimic.instanceId).And.Not.Contain(fund.instanceId));
            var expired=receipt.effects.Where(e=>e.kind=="TemporaryDieExpired").ToArray();Assert.That(expired.Length,Is.EqualTo(1));
            Assert.That(InformationText07("TemporaryCleanup"),Is.EqualTo("本关的 "+expired.Length+" 颗临时骰已移除"));
            Assert.That(HasActive("SettlementNetCash"),Is.False,"The details replace the summary.");
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);yield return null;
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            long gold=s.gold;Click("SettlementContinue");Assert.That(app.Session.State.gold,Is.EqualTo(gold));
            Assert.That(app.Session.State.controllers.Single(c=>c.instanceId==mimic.instanceId).state.fundAccrual,Is.EqualTo(6));
            scene=InformationScene07(Art07FixtureKind.G147);scene.controllers.Clear();scene.target="1";scene.remainingSubmissions=1;
            foreach(var die in scene.dice)die.lifetime="stage_temporary";
            ApplyInformationScene07(scene);Click("Submit");app.SkipVisual();
            Assert.That(InformationText07("SettleNote15"),Is.EqualTo("本关通过了，但你已经没有骰子，这一局结束。"));
            Click("SettlementContinue");Assert.That(app.Session.State.phase,Is.EqualTo("RunFailed"));Assert.That(app.Session.State.failureReason,Is.EqualTo("NoPermanentDice"));
        }

        [UnityTest] public IEnumerator PLAY_ART07_CardInformationPreservesPricesAndGeometricRarity()
        {
            StartRun("70795");var scene=InformationScene07(Art07FixtureKind.MaximumTable);scene.target="1";scene.controllerOfferPurchases=2;scene.gold=100; /* p0.9.0: shelf 4 + 2 = 6 */
            ApplyInformationScene07(scene);string instance=app.Session.State.controllers.First(c=>c.enchantmentId!="none").instanceId;
            var card=Button("Controller_"+instance);Assert.That(card.GetComponent<Art07HoverTip>(),Is.Not.Null);
            // V1.1 (W13, CARD_TEMPLATE §3): on the table the geometric rarity is the gem's shape (round, diamond, six-point
            // star); the ticks stay on the detail card below.
            string rarity=app.Config.Controller(app.Session.State.controllers.Single(c=>c.instanceId==instance).definitionId).rarity;
            Assert.That(card.transform.Find("RarityGem13").GetComponent<UnityEngine.UI.Image>().sprite.name,Is.EqualTo("ui_gem_"+(rarity=="rare"?"rare":rarity=="uncommon"?"advanced":"common")));
            var hover=card.GetComponent<Art07HoverTip>();var originalShow=hover.show;var originalHide=hover.hide;
            float shownAt=-1,hiddenAt=-1,showFrame=0,hideFrame=0;
            hover.show=()=>{shownAt=Time.unscaledTime;showFrame=Time.unscaledDeltaTime;originalShow();};
            hover.hide=()=>{hiddenAt=Time.unscaledTime;hideFrame=Time.unscaledDeltaTime;originalHide();};
            float enteredAt=Time.unscaledTime;hover.OnPointerEnter(new PointerEventData(EventSystem.current));
            Assert.That(HasActive("ContentTooltip"),Is.False);
            double deadline=Time.realtimeSinceStartupAsDouble+3;
            while(shownAt<0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(shownAt-enteredAt,Is.GreaterThanOrEqualTo(.3f-.0001f));
            Assert.That(shownAt-enteredAt,Is.LessThanOrEqualTo(.3f+showFrame+.001f),"Show on the first rendered frame after the real 300ms deadline.");
            Assert.That(HasActive("ContentTooltip"),Is.True);
            float exitedAt=Time.unscaledTime;hiddenAt=-1;hover.OnPointerExit(new PointerEventData(EventSystem.current));
            deadline=Time.realtimeSinceStartupAsDouble+3;while(hiddenAt<0&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(hiddenAt-exitedAt,Is.GreaterThanOrEqualTo(.06f-.0001f));
            Assert.That(hiddenAt-exitedAt,Is.LessThanOrEqualTo(.06f+hideFrame+.001f));Assert.That(HasActive("ContentTooltip"),Is.False);
            TestContext.WriteLine("Actual hover delay "+(shownAt-enteredAt)+"s; leave delay "+(hiddenAt-exitedAt)+"s; same-frame deltas "+showFrame+"/"+hideFrame+"s.");
            Click("Controller_"+instance);var detail=FindActive<Transform>("DetailCard");
            Assert.That(detail.Find("RarityTicks").childCount,Is.InRange(1,3));Assert.That(detail.GetComponentsInChildren<TextMeshProUGUI>().Any(t=>t.text.Contains("▰")),Is.False);
            var badge=detail.GetComponentsInChildren<RectTransform>().Single(r=>r.name=="Badge");Assert.That(badge.sizeDelta,Is.EqualTo(new Vector2(40,40)));
            Click("CloseDialog");Click("Submit");app.SkipVisual();Click("EndStage");app.SkipVisual();Click("SettlementContinue");
            foreach(var offer in app.Session.State.shop.offers.Where(o=>o.category=="controller"))
            {
                long cost=app.Session.GetOfferNetPrice(offer);
                // v1.1 (RULES_DELTA 1.1 item 5): no replacement credit, so no offer has a negative price or a 替换 button.
                Assert.That(cost,Is.GreaterThanOrEqualTo(0),offer.offerId);
                // V1.1 (W14, CARD_TEMPLATE §5): a full shelf still never hides the transaction price; the price button shows
                // it with the reason instead of "购买" (v0.73: a separate price chip, a reason line and a "购买" button).
                var buy=FindActive<Transform>("Offer_"+offer.offerId).Find("Buy_"+offer.offerId);
                Assert.That(buy.Find("Price").GetComponent<TextMeshProUGUI>().text,Is.EqualTo(cost.ToString()));
                // v1.1: an owned card's offer is an ordinary purchase as well, so every card offer reports the full count
                // (UI_REWRITES Art07Shop.cs:122-126 with D2).
                Assert.That(buy.Find("PriceWord13").GetComponent<TextMeshProUGUI>().text,Is.EqualTo("卡牌已满 10 张"));
                Assert.That(buy.GetComponent<UnityEngine.UI.Button>().interactable,Is.False,"A full shelf cannot be bought from the price button.");
            }
            foreach(var offer in app.Session.State.shop.offers.Where(o=>o.category=="consumable"))
            {
                var product=FindActive<Transform>("Product_"+offer.offerId);
                // V1.1 (W14): the reason is on the price button; the row's body (Inspect) is still the inspect target.
                var buy=product.Find("Buy_"+offer.offerId);
                Assert.That(buy.Find("PriceWord13").GetComponent<TextMeshProUGUI>().text,Is.EqualTo("道具栏已满"));
                Assert.That(product.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name=="Inspect"),Is.EqualTo(1));
                Assert.That(buy.GetComponent<UnityEngine.UI.Button>().interactable,Is.False);
                Assert.That(buy.Find("Price").GetComponent<TextMeshProUGUI>().text,Is.EqualTo(app.Session.GetOfferNetPrice(offer).ToString()));
            }
            yield return null;
        }
    }
}
