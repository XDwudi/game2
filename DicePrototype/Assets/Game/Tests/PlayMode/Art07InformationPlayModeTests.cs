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

        [UnityTest] public IEnumerator PLAY_ART07_InvalidSeedKeepsDialogAndInputUntilCorrected()
        {
            Click("NewRun");var input=FindActive<TMP_InputField>("SeedInput");
            foreach(string invalid in new[]{"not-a-seed","4294967296","-1"})
            {
                input.text=invalid;Click("StartNew");yield return null;
                Assert.That(FindActive<TMP_InputField>("SeedInput"),Is.SameAs(input));
                Assert.That(input.text,Is.EqualTo(invalid));Assert.That(InformationText07("SeedError"),Does.Contain("输入已保留"));
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
            Assert.That(InformationButton07("Factor4"),Does.Contain("×0").And.Contain("没有有效 5 点"));
            Assert.That(InformationText07("FinalScore"),Is.EqualTo("0"));Assert.That(Button("Submit").interactable,Is.True);
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            Click("Factor4");Click("CloseDialog");Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            Click("Submit");app.SkipVisual();
            Assert.That(app.Session.State.lastSubmission.breakdown.score,Is.EqualTo("0"));
            Assert.That(app.Session.State.lastEffects.Any(e=>e.kind=="SubmissionIncome"),Is.True);
            Assert.That(app.Session.State.lastEffects.Any(e=>e.kind=="HandGrown"),Is.True);
            yield return null;
        }

        [UnityTest] public IEnumerator PLAY_ART07_RandomNineBranchesSharedOwnerAreExactAndReadOnly()
        {
            StartRun("70793");var scene=InformationScene07(Art07FixtureKind.SharedHalf);scene.randomOverrides.Clear();ApplyInformationScene07(scene);
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);var frozen=app.Session.PreviewSnapshot();
            string owner=app.Session.PreviewScoreRange().randomOwnerIds.Single();
            Assert.That(InformationText07("Hand"),Does.Contain("随机结果范围"));Click("RandomPreview");
            Assert.That(InformationText07("RandomOwner"+owner+"Title"),Does.Contain("共用 q"));
            int[] choices=app.Config.Controller("C53").parameters.factorUnits2;
            for(int i=0;i<choices.Length;i++)
            {
                var branch=frozen.Clone();branch.resolvedRandomChoices=new[]{new RandomMultiplierChoice{ownerInstanceId=owner,units2=choices[i]}};
                var exact=ScoreCalculator.Preview(branch,app.Config);Assert.That(exact.isRandom,Is.False);
                Assert.That(InformationText07("RandomScore"+i),Is.EqualTo(exact.minimum.score));
                Assert.That(InformationText07("RandomFactor"+i),Is.EqualTo("q ×"+(choices[i]/2m).ToString("0.#")));
            }
            Assert.That(choices.Length,Is.EqualTo(9));Click("RandomBranch8");Click("CloseDialog");
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            scene=InformationScene07(Art07FixtureKind.SharedHalf);ApplyInformationScene07(scene);
            before=StateJson();disk=File.ReadAllBytes(app.SaveLocation);Click("RandomPreview");
            Assert.That(InformationText07("RandomRangeBody"),Does.Contain("仅下一次调试覆盖"));Assert.That(HasActive("RandomBranch0"),Is.False);
            Click("CloseDialog");Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            scene=InformationScene07(Art07FixtureKind.SharedHalf);scene.randomOverrides.Clear();
            var extra=scene.controllers[1].Clone();extra.acquiredSerial+=1;extra.instanceId="ITEM_"+extra.acquiredSerial.ToString("D5");scene.controllers.Add(extra);
            ApplyInformationScene07(scene);before=StateJson();Click("RandomPreview");Assert.That(InformationText07("RandomMultipleBody"),Does.Contain("不展开组合列表"));
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
            Assert.That(earned,Is.GreaterThan(0));Assert.That(InformationText07("SubmissionIncome"),Does.Contain("本关已入账").And.Contain("+"+earned+" 金币"));
            Assert.That(InformationText07("FundGrowth"),Does.Contain(mimic.instanceId+" · 卖价＋3 · 基金累计 6").And.Contain(fund.instanceId+" · 卖价＋3 · 基金累计 9").And.Contain("不进入钱包"));
            var expired=receipt.effects.Where(e=>e.kind=="TemporaryDieExpired").ToArray();Assert.That(expired.Length,Is.EqualTo(1));
            Assert.That(InformationText07("TemporaryCleanup"),Does.Contain(expired[0].dieId));
            Assert.That(InformationText07("SettlementNetCash"),Does.Contain("+"+(settlement.goldAfter-settlement.goldBefore)));
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);yield return null;
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            long gold=s.gold;Click("SettlementContinue");Assert.That(app.Session.State.gold,Is.EqualTo(gold));
            Assert.That(app.Session.State.controllers.Single(c=>c.instanceId==mimic.instanceId).state.fundAccrual,Is.EqualTo(6));
            scene=InformationScene07(Art07FixtureKind.G147);scene.controllers.Clear();scene.target="1";scene.remainingSubmissions=1;
            foreach(var die in scene.dice)die.lifetime="stage_temporary";
            ApplyInformationScene07(scene);Click("Submit");app.SkipVisual();
            Assert.That(InformationText07("SettlementStatus"),Does.Contain("本关成功").And.Contain("永久骰已耗尽"));
            Click("SettlementContinue");Assert.That(app.Session.State.phase,Is.EqualTo("RunFailed"));Assert.That(app.Session.State.failureReason,Is.EqualTo("NoPermanentDice"));
        }

        [UnityTest] public IEnumerator PLAY_ART07_CardInformationPreservesPricesAndGeometricRarity()
        {
            StartRun("70795");var scene=InformationScene07(Art07FixtureKind.MaximumTable);scene.target="1";scene.controllerOfferPurchases=3;scene.gold=100;
            ApplyInformationScene07(scene);string instance=app.Session.State.controllers.First(c=>c.enchantmentId!="none").instanceId;
            var card=Button("Controller_"+instance);Assert.That(card.GetComponent<Art07HoverTip>(),Is.Not.Null);
            Assert.That(card.transform.Find("RarityTicks").childCount,Is.InRange(1,3));
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
                // v0.73: a full shelf still never hides the transaction price: it stays on the price chip of the card.
                Assert.That(InformationButton07("Buy_"+offer.offerId),Is.EqualTo(cost<0?"替换":"购买"));
                Assert.That(FindActive<Transform>("Offer_"+offer.offerId).Find("PriceChip/Price").GetComponent<TextMeshProUGUI>().text,Is.EqualTo(cost<0?"+"+(-cost):cost.ToString()));
                if(!app.Session.State.controllers.Any(c=>c.definitionId==offer.contentId))Assert.That(InformationText07("OfferAvailability_"+offer.offerId),Is.EqualTo("总持有已满"));
            }
            foreach(var offer in app.Session.State.shop.offers.Where(o=>o.category=="consumable"))
            {
                var product=FindActive<Transform>("Product_"+offer.offerId);
                // v0.73: the reason sits on its own line above the price chip; the card body is the inspect target.
                Assert.That(product.Find("Availability").GetComponent<TextMeshProUGUI>().text,Is.EqualTo("道具库存满"));
                Assert.That(product.GetComponentsInChildren<UnityEngine.UI.Button>().Count(b=>b.name=="Inspect"),Is.EqualTo(1));
                Assert.That(InformationButton07("Buy_"+offer.offerId),Is.EqualTo("购买"));
                Assert.That(product.Find("PriceChip/Price").GetComponent<TextMeshProUGUI>().text,Is.EqualTo(app.Session.GetOfferNetPrice(offer).ToString()));
            }
            yield return null;
        }
    }
}
