using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private void PrepareEnchantmentHand25(int submissions)
        {
            StartRun("11425");
            FinalOptions07("Instant",false);
            var scene=W11Scene(new[]{"C114","C02","C02"},new[]{4,4,4,4,2},"rrrrr",5,true,stageIndex:2,modifier:"B02",target:"1");
            scene.remainingSubmissions=submissions;
            // Only the third card is eligible for the boss effect.
            scene.controllers[1].enchantmentId="base10";
            W11Apply(scene);
            Click("Submit");
            app.SkipVisual();
        }

        private string ExpectedEnchantment25()
            =>app.Config.Controller("C114").displayName+"："+app.Config.Controller("C02").displayName+"（第 3 张） 获得附魔 ";

        [UnityTest]
        public IEnumerator PLAY_V25_C114_SeparateStageEndUsesItsLastSubmittedRoster()
        {
            PrepareEnchantmentHand25(2);
            yield return WaitForIdle();
            var settled=app.Send(new SessionCommand{kind=CommandKind.EndStage});
            Assert.That(settled.accepted,Is.True,settled.error);
            app.SkipVisual();
            var state=app.Session.State;
            var receipt=state.processedReceipts.Last();
            Assert.That(receipt.command.kind,Is.EqualTo(CommandKind.EndStage));
            Assert.That(receipt.submission,Is.Null);
            var effect=receipt.effects.Single(e=>e.sourceId=="C114"&&e.kind=="ControllerEnchanted");
            Assert.That(effect.detail.Split(':')[0],Is.EqualTo(state.lastSubmission.snapshot.equippedControllers[2].instanceId));
            Click("SettlementDetailsToggle15");
            Assert.That(FindActive<TextMeshProUGUI>("OtherChange15_0").text,Does.StartWith(ExpectedEnchantment25()));
        }

        [UnityTest] public IEnumerator PLAY_V25_C114_PreviousHandRetainsSoldCardAfterReload()
        {
            yield return CheckChangedRoster25(true);
        }

        [UnityTest] public IEnumerator PLAY_V25_C114_PreviousHandRetainsSameNameCardPositionAfterReload()
        {
            yield return CheckChangedRoster25(false);
        }

        private IEnumerator CheckChangedRoster25(bool sell)
        {
            PrepareEnchantmentHand25(1);
            yield return WaitForIdle();
            var state=app.Session.State;
            Assert.That(state.phase,Is.EqualTo("StageSettlement"));
            var original=state.processedReceipts.Single(r=>r.submission?.snapshot?.handId==state.lastSubmission.snapshot.handId);
            var frozen=original.submission.snapshot.equippedControllers;
            var effect=original.effects.Single(e=>e.kind=="ControllerEnchanted"&&e.sourceId=="C114");
            string target=effect.detail.Split(':')[0];
            Assert.That(target,Is.EqualTo(frozen[2].instanceId));
            Assert.That(frozen[2].definitionId,Is.EqualTo(frozen[1].definitionId));
            string receiptJson=JsonUtility.ToJson(original);

            Click("SettlementContinue");
            yield return WaitForIdle();
            Assert.That(app.LootPage15,Is.True);
            Click("LootCashOut15");
            yield return WaitForIdle();
            Click("LootEnterShop15");
            if(sell)
            {
                Click("Controller_"+target);
                Click("SellController");
                Click("ConfirmSale");
                yield return WaitForIdle();
                Assert.That(app.Session.State.controllers.Any(c=>c.instanceId==target),Is.False);
            }
            else
            {
                var change=app.Send(new SessionCommand{kind=CommandKind.ReorderControllers,
                    controllerIds=new[]{frozen[0].instanceId,target,frozen[1].instanceId}});
                Assert.That(change.accepted,Is.True,change.error);
                Assert.That(app.Session.State.controllers[1].instanceId,Is.EqualTo(target));
            }
            Click("LeaveShop");
            Click("StartStage");
            yield return WaitForIdle();
            yield return LoadBootstrap();
            ResumeBoundaryFixture();
            yield return WaitForIdle();
            var restored=app.Session.State;
            Assert.That(JsonUtility.ToJson(restored.processedReceipts.Single(r=>r.actionSequence==original.actionSequence)),Is.EqualTo(receiptJson));
            string before=StateJson();
            var disk=File.ReadAllBytes(app.SaveLocation);
            Click("PreviousSubmission");
            Assert.That(FindActive<TextMeshProUGUI>("Triggered").text,Does.Contain(ExpectedEnchantment25()));
            Click("FullScoreDetails");
            Assert.That(FindActive<TextMeshProUGUI>("Triggered").text,Does.Contain(ExpectedEnchantment25()));
            Click("CloseDialog");
            if(!sell)
            {
                // No saved evidence must remain neutral even when the target is currently identifiable.
                var format=app.GetType().GetMethod("EffectText",BindingFlags.NonPublic|BindingFlags.Instance);
                foreach(var missing in new[]{null,new SubmissionSnapshot{equippedControllers=Array.Empty<ControllerInstance>()}})
                {
                    string unknown=(string)format.Invoke(app,new object[]{effect,missing});
                    Assert.That(unknown,Does.Contain("：一张卡牌 获得附魔 "));
                }
                var purchase=effect.Clone();purchase.sourceId="O11";
                Assert.That((string)format.Invoke(app,new object[]{purchase,null}),Does.Contain(app.Config.Controller("C02").displayName+"（第 2 张）"));
            }
            Assert.That(StateJson(),Is.EqualTo(before));
            CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
        }
    }
}
