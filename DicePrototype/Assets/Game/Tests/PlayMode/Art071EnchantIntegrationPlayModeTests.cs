using System;
using System.Collections;
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

namespace DiceCraft.Tests
{
    // Integrate as a new PlayMode file. Reuses the real Bootstrap/isolation fixture.
    public sealed partial class GameplayPlayModeTests
    {
        private Art071EnchantCue[] EnchantLive071(string kind)=>app.canvas.GetComponentsInChildren<Art071EnchantCue>(true)
            .Where(c=>c.IsBound&&c.BoundEnchantmentId==kind).ToArray();
        private void EnchantEffectsTick071(float dt)=>typeof(GameApp).GetMethod("TickLibraryEffects07",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,new object[]{dt});
        private void EnchantCheckpoint071(string state,byte[] saved,long sequence)
        {
            Assert.That(StateJson(),Is.EqualTo(state),"Complete committed state including all RNG streams changed during visual-only playback.");
            Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
            CollectionAssert.AreEqual(saved,File.ReadAllBytes(app.SaveLocation));
        }
        private void EnchantBudgetVisible071(RunState after,string[] ids,string context)
        {
            Canvas.ForceUpdateCanvases();
            var value=FindActive<TextMeshProUGUI>("RollBudgetValue");
            Assert.That(value.text,Is.EqualTo(after.hand.rollsRemaining.ToString()),context+": visible budget is still the old hand value");
            Assert.That(PostInkScreenRects07(value,true),Is.Not.Empty,"Actual visible glyphs, not only TMP source text.");
            var icon=FindActive<RectTransform>("RollBudgetIcon");var target=V06ScreenRect(icon);
            var cues=EnchantLive071("roll1");CollectionAssert.AreEquivalent(ids,cues.Select(c=>c.BoundInstanceId));
            foreach(var cue in cues)
            {
                var actual=V06ScreenRect((RectTransform)cue.transform);
                Assert.That(Vector2.Distance(actual.center,target.center),Is.LessThanOrEqualTo(.75f),context+": cue captured the previous header or omitted its parent offset");
                Assert.That(cue.GetComponent<CanvasGroup>().alpha,Is.GreaterThan(0));
                Assert.That(cue.GetComponentsInChildren<UnityEngine.UI.Graphic>(true).All(g=>!g.raycastTarget),Is.True);
            }
        }

        [UnityTest]
        public IEnumerator PLAY_ART071_ENCHANT_CommittedNewHandBudgetUsesActualHeaderAndVisibleValue()
        {
            bool wasEnabled=app.enabled;
            try
            {
                foreach(bool firstQualified in new[]{false,true})
                {
                    app.enabled=wasEnabled;
                    FxScene07(s=>
                    {
                        s.target=firstQualified?"1":"1800000";s.remainingSubmissions=3;
                        s.controllers[0].enchantmentId="roll1";s.controllers[1].enchantmentId="roll1";
                    });
                    FinalOptions07("Normal",false);Canvas.ForceUpdateCanvases();
                    var before=app.Session.State;Assert.That(before.stage.qualified,Is.False);
                    var oldIcon=V06ScreenRect(FindActive<RectTransform>("RollBudgetIcon"));
                    app.enabled=false; // deterministic external clock, as in existing qualified-layout integration tests
                    var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                    var after=app.Session.State;Assert.That(after.hand.handId,Is.Not.EqualTo(before.hand.handId));
                    Assert.That(after.stage.qualified,Is.EqualTo(firstQualified));
                    Assert.That(after.hand.rollsRemaining,Is.Not.EqualTo(before.hand.rollsRemaining),"Fixture must expose stale visible values.");
                    string state=StateJson();byte[] saved=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
                    var receipt=PresentationReceiptAdapter.BuildCommitted(result,before,after,app.Config);
                    var grant=receipt.Events.Single(e=>e.EffectFamily=="hand_roll_bonus");
                    Assert.That(grant.IsScoring||grant.CountsForChain,Is.False);
                    CollectionAssert.AreEquivalent(after.hand.rollBonusSourceIds,grant.SourceEmitterIds);
                    Assert.That(grant.Operand,Is.EqualTo((2*app.Config.enchantRules.rollBonus).ToString()));
                    Assert.That(grant.AfterValue,Is.EqualTo(after.hand.rollsRemaining.ToString()));
                    var player=QualifiedPlayer07();var beat=player.Sequence.Beats.Single(b=>b.Phase==PresentationPhase.TargetState);
                    player.Tick(beat.StartMs/1000d);EnchantEffectsTick071(0);Canvas.ForceUpdateCanvases();
                    Assert.That(app.PresentationPlaying07,Is.True);
                    EnchantBudgetVisible071(after,after.hand.rollBonusSourceIds,firstQualified?"first-qualified":"not-qualified");
                    if(firstQualified)
                    {
                        Assert.That(app.QualifiedStampPlaying07,Is.True);
                        var actualIcon=V06ScreenRect(FindActive<RectTransform>("RollBudgetIcon"));
                        Assert.That(Vector2.Distance(oldIcon.center,actualIcon.center),Is.GreaterThan(20),"Fixture did not exercise actual header replacement.");
                    }
                    yield return CaptureEdge07("v071-enchant-budget-"+(firstQualified?"first-qualified":"not-qualified"));
                    EnchantCheckpoint071(state,saved,sequence);
                    app.SkipVisual();Assert.That(app.ActiveEnchantmentSources071,Is.Empty);
                    Assert.That(FindActive<TextMeshProUGUI>("RollBudgetValue").text,Is.EqualTo(after.hand.rollsRemaining.ToString()));
                    EnchantCheckpoint071(state,saved,sequence);
                }
            }
            finally{app.enabled=wasEnabled;}
        }

        [UnityTest]
        public IEnumerator PLAY_ART071_ENCHANT_SlotlessFrameFollowsRealPurchasedCardAndPauseSkipCleanUp()
        {
            bool wasEnabled=app.enabled;
            try
            {
                yield return TransactionShop07(s=>s.shop.offers.Any(o=>o.category=="controller"&&o.controller.enchantmentId=="slotless"&&!s.controllers.Any(c=>c.definitionId==o.contentId)));
                FinalOptions07("Normal",false);Canvas.ForceUpdateCanvases();
                var before=app.Session.State;var offer=before.shop.offers.First(o=>o.category=="controller"&&o.controller.enchantmentId=="slotless"&&!before.controllers.Any(c=>c.definitionId==o.contentId));
                app.enabled=false;
                var result=app.Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId});Assert.That(result.accepted,Is.True,result.error);
                var acquired=app.Session.State.controllers.Single(c=>!before.controllers.Any(p=>p.instanceId==c.instanceId));
                var cue=EnchantLive071("slotless").Single();Assert.That(cue.BoundInstanceId,Is.EqualTo(acquired.instanceId));
                string state=StateJson();byte[] saved=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
                Vector2 first=Vector2.zero,last=Vector2.zero;float elapsed=0;
                foreach(float at in new[]{.08f,.20f,.28f})
                {
                    EnchantEffectsTick071(at-elapsed);elapsed=at;Canvas.ForceUpdateCanvases();
                    var target=V06ScreenRect(FindActive<RectTransform>("Controller_"+acquired.instanceId));
                    var bounds=V06ScreenRect((RectTransform)cue.transform);
                    Assert.That(Vector2.Distance(target.center,bounds.center),Is.LessThanOrEqualTo(.75f),"Frame detached from the moving card at "+at);
                    Assert.That(Vector2.Distance(target.size,bounds.size),Is.LessThanOrEqualTo(.75f),"Frame extent lost the actual card size.");
                    Assert.That(cue.GetComponent<CanvasGroup>().alpha,Is.GreaterThan(0));
                    if(at==.08f)first=target.center;last=target.center;
                    if(at==.20f)
                    {
                        Click("Pause");float stopped=cue.NormalizedTime;EnchantEffectsTick071(.25f);
                        Assert.That(cue.NormalizedTime,Is.EqualTo(stopped));Click("CloseDialog");
                        yield return CaptureEdge07("v071-enchant-slotless-purchase-follow");
                    }
                    EnchantCheckpoint071(state,saved,sequence);
                }
                Assert.That(Vector2.Distance(first,last),Is.GreaterThan(3),"The real normal purchase rise must actually move; Reduced-only coverage would miss this regression.");
                app.SkipVisual();Assert.That(app.ActiveEnchantmentSources071,Is.Empty);
                Assert.That(app.TransactionVisualPlaying07,Is.False);Assert.That(app.Session.Busy,Is.False);
                Assert.That(FindActive<RectTransform>("Controller_"+acquired.instanceId).GetComponent<CanvasGroup>().alpha,Is.EqualTo(1));
                EnchantCheckpoint071(state,saved,sequence);
            }
            finally{app.enabled=wasEnabled;}
        }

        [UnityTest]
        public IEnumerator PLAY_ART071_ENCHANT_MultiplierArcReachesActualReceiptAreaWithoutMutatingReceipt()
        {
            bool wasEnabled=app.enabled;
            try
            {
                FxScene07(s=>{FxControllers07(s,"C46");s.controllers[0].enchantmentId="mult2";});
                FinalOptions07("Normal",false);Canvas.ForceUpdateCanvases();app.enabled=false;
                var before=app.Session.State;var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                var receipt=PresentationReceiptAdapter.BuildCommitted(result,before,app.Session.State,app.Config);
                string state=StateJson();byte[] saved=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
                var e=receipt.Events.Single(x=>x.EffectFamily=="enchantment_multiplier");
                var player=QualifiedPlayer07();var beat=player.Sequence.Beats.Single(b=>b.Events.Any(x=>x.EventId==e.EventId));
                player.Tick(beat.StartMs/1000d);EnchantEffectsTick071(0);Canvas.ForceUpdateCanvases();
                var cue=EnchantLive071("mult2").Single();Assert.That(cue.BoundInstanceId,Is.EqualTo(e.EmitterId));
                var arc=(RectTransform)cue.transform.Find("MultiplierArc");var initial=V06ScreenRect(arc).center;
                player.Tick(beat.DurationMs*.9/1000d);EnchantEffectsTick071(0);Canvas.ForceUpdateCanvases();
                var target=V06ScreenRect(FindActive<TextMeshProUGUI>("ReceiptFactors").rectTransform);
                var actual=V06ScreenRect(arc);var near=target;near.xMin-=actual.width;near.xMax+=actual.width;near.yMin-=actual.height;near.yMax+=actual.height;
                Assert.That(near.Contains(actual.center),Is.True,"Arc endpoint must reach the actual visible receipt factors, not remain at a card badge.");
                Assert.That(Vector2.Distance(initial,actual.center),Is.GreaterThan(20),"The fixture must exercise a real route.");
                Assert.That(cue.GetComponent<CanvasGroup>().alpha,Is.GreaterThan(0));
                Assert.That(FindActive<TextMeshProUGUI>("ReceiptFactors").text,Does.Contain("附魔倍率"));
                yield return CaptureEdge07("v071-enchant-multiplier-at-receipt");EnchantCheckpoint071(state,saved,sequence);
                app.SkipVisual();Assert.That(app.ActiveEnchantmentSources071,Is.Empty);EnchantCheckpoint071(state,saved,sequence);
            }
            finally{app.enabled=wasEnabled;}
        }
    }
}
