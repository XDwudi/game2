using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private void ShopEntrancePointer07(string name)
        {
            // Every caller waits for a real rendered frame after creating/rebuilding UI.
            // Keep this helper local: S03's diagnostic coroutine has a different contract.
            Canvas.ForceUpdateCanvases();var button=Button(name);var corners=new Vector3[4];
            ((RectTransform)button.transform).GetWorldCorners(corners);
            var point=RectTransformUtility.WorldToScreenPoint(null,(corners[0]+corners[2])*.5f);
            var data=new PointerEventData(EventSystem.current){position=point,button=PointerEventData.InputButton.Left};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Assert.That(button.IsInteractable(),Is.True,name);Assert.That(hits.Count,Is.GreaterThan(0),name);
            Assert.That(hits[0].gameObject==button.gameObject||hits[0].gameObject.transform.IsChildOf(button.transform),Is.True,name+" actual hotzone covered");
            ExecuteEvents.Execute(button.gameObject,data,ExecuteEvents.pointerClickHandler);
        }
        private IEnumerator ShopEntranceFrame07()
        {
            yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();
        }
        private IEnumerator PrepareFirstShopEntrance07(bool reduce,int stage)
        {
            // This is an explicitly isolated debug scene. Actual Submit and Continue
            // buttons below create the settlement and generated shop through core.
            Art07Fixtures.Apply(app,Art07FixtureKind.G147);FinalOptions07("Normal",reduce);
            var s=app.Session.State;var scene=Art07Fixtures.Create(app.Config,Art07FixtureKind.G147,s.nextItemSerial,s.nextDieSerial);
            scene.sourceRunId=s.sourceRunId;scene.stageIndex=stage;scene.gold=1000000;
            scene.target="1";scene.remainingSubmissions=1;scene.controllerOfferPurchases=3;scene.consumableOfferPurchases=3;
            File.WriteAllText(Path.Combine(saveDirectory,"shop-entrance-"+stage+"-fixture.json"),JsonUtility.ToJson(scene,true));
            var applied=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});Assert.That(applied.accepted,Is.True,applied.error);
            yield return app.StartCoroutine(ShopEntranceFrame07());
            ShopEntrancePointer07("Submit");app.SkipVisual();Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));
            yield return app.StartCoroutine(ShopEntranceFrame07());
            if(app.SettlementRevealPlaying07){ShopEntrancePointer07("SkipSettlementReveal07");yield return app.StartCoroutine(ShopEntranceFrame07());}
            Assert.That(app.IsBusy||app.Session.HasPendingSave,Is.False);
        }
        private Art07ShopEntrance ShopEntranceMotion07()=>app.canvas.GetComponentInChildren<Art07ShopEntrance>(true);
        private RectTransform ShopEntranceRoot07(string name)=>FindActive<RectTransform>(name);
        private IEnumerator ShopEntrancePng07(string name)
        {
            yield return new WaitForEndOfFrame();var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try{File.WriteAllBytes(Path.Combine(saveDirectory,name+".png"),texture.EncodeToPNG());}finally{UnityEngine.Object.Destroy(texture);}
        }
        private string ShopEntranceSample07(Art07ShopEntrance motion,string tag)
        {
            var names=app.Session.State.shop.offers.Where(o=>o.category=="controller").Select(o=>"Offer_"+o.offerId);
            return tag+" frame="+Time.frameCount+" time="+Time.realtimeSinceStartupAsDouble.ToString("F6")+" elapsed="+motion.Elapsed+" duration="+motion.Duration+
                " shelf="+ShopEntranceRoot07("ControllerShelf").GetComponent<CanvasGroup>().alpha+" "+string.Join(";",names.Select(n=>{var r=ShopEntranceRoot07(n);return n+":"+r.anchoredPosition+" alpha="+r.GetComponent<CanvasGroup>().alpha;}));
        }
        [UnityTest] public IEnumerator PLAY_ART07_ShopFirstEntryUses240msShelf30msCardStagger12pxRiseAndReducedFade()
        {
            foreach(bool reduce in new[]{false,true})
            {
                int stage=reduce?1:0;yield return PrepareFirstShopEntrance07(reduce,stage);
                long before=app.Session.ActionSequence;ShopEntrancePointer07("SettlementContinue");
                Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));Assert.That(app.Session.ActionSequence,Is.EqualTo(before+1));
                Assert.That(app.Session.State.processedReceipts.Last().command.kind,Is.EqualTo(CommandKind.Continue));
                var motion=ShopEntranceMotion07();Assert.That(motion,Is.Not.Null);Assert.That(motion.IsPlaying,Is.True);
                Assert.That(motion.CardGroupCount,Is.EqualTo(11));Assert.That(motion.Duration,Is.EqualTo(reduce?.12f:.48f).Within(.0001));Assert.That(motion.Duration,Is.LessThanOrEqualTo(.52f));
                var offers=app.Session.State.shop.offers.Where(o=>o.category=="controller").ToArray();Assert.That(offers.Length,Is.EqualTo(6));
                var first=ShopEntranceRoot07("Offer_"+offers[0].offerId);var second=ShopEntranceRoot07("Offer_"+offers[1].offerId);
                // v0.73 single-page shop: six controller offers sit in two rows; the first row rests at y 124.
                Assert.That(first.anchoredPosition.y,Is.EqualTo(reduce?-124:-136).Within(.01));
                Assert.That(first.GetComponent<CanvasGroup>().alpha,Is.Zero);Assert.That(second.GetComponent<CanvasGroup>().alpha,Is.Zero);
                Assert.That(app.IsBusy||app.Session.Busy,Is.False,"The shelf entrance must not create a gameplay input lock.");
                string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;
                var samples=new List<string>{ShopEntranceSample07(motion,"start")};bool captured=false;double deadline=Time.realtimeSinceStartupAsDouble+3;
                while(motion.IsPlaying&&Time.realtimeSinceStartupAsDouble<deadline)
                {
                    AssertInteractionCheckpoint07(state,disk,seq);samples.Add(ShopEntranceSample07(motion,"frame"));
                    float p=Mathf.Clamp01(motion.Elapsed/(reduce?.12f:.18f)),ease=1-(1-p)*(1-p);
                    Assert.That(first.GetComponent<CanvasGroup>().alpha,Is.EqualTo(ease).Within(.001));
                    Assert.That(first.anchoredPosition.y,Is.EqualTo(-124-(reduce?0:12*(1-ease))).Within(.01));
                    float p2=Mathf.Clamp01((motion.Elapsed-(reduce?0:.03f))/(reduce?.12f:.18f));
                    Assert.That(second.GetComponent<CanvasGroup>().alpha,Is.EqualTo(1-(1-p2)*(1-p2)).Within(.001));
                    if(!captured&&motion.Elapsed>0){captured=true;yield return app.StartCoroutine(ShopEntrancePng07("shop-entrance-"+(reduce?"reduced":"normal")+"-moving"));}
                    else yield return null;
                }
                Assert.That(motion.IsPlaying,Is.False);Assert.That(captured,Is.True,"At least one real in-progress rendered frame must be recorded.");
                Assert.That(first.anchoredPosition.y,Is.EqualTo(-124).Within(.01));Assert.That(first.GetComponent<CanvasGroup>().alpha,Is.EqualTo(1));
                AssertInteractionCheckpoint07(state,disk,seq);samples.Add(ShopEntranceSample07(motion,"settled"));
                File.WriteAllLines(Path.Combine(saveDirectory,"shop-entrance-"+(reduce?"reduced":"normal")+"-frames.txt"),samples);
                yield return app.StartCoroutine(ShopEntrancePng07("shop-entrance-"+(reduce?"reduced":"normal")+"-settled"));
            }
        }
        [UnityTest] public IEnumerator PLAY_ART07_ShopRedrawSettlesWithoutReplayingAndRefreshKeepsItsOwnTransaction()
        {
            yield return PrepareFirstShopEntrance07(false,2);ShopEntrancePointer07("SettlementContinue");
            var motion=ShopEntranceMotion07();Assert.That(motion.IsPlaying,Is.True);int enters=motion.EnterSequence;
            string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;string identity=app.Session.State.shop.generationId;
            // v0.73: the shop has no tabs. Any ordinary redraw (here: a plain refresh of the view, twice) must settle the
            // first-visit entrance without replaying it and must leave reused roots at their laid-out positions.
            yield return app.StartCoroutine(ShopEntranceFrame07());app.Refresh();
            Assert.That(motion.EnterSequence,Is.EqualTo(enters));Assert.That(motion.IsPlaying,Is.False);AssertInteractionCheckpoint07(state,disk,seq);
            Assert.That(ShopEntranceRoot07("LeaveShop").anchoredPosition.x,Is.EqualTo(1512).Within(.01),"Old animated roots must not overwrite a reused root's layout.");
            yield return app.StartCoroutine(ShopEntranceFrame07());app.Refresh();
            Assert.That(motion.EnterSequence,Is.EqualTo(enters));Assert.That(motion.IsPlaying,Is.False);AssertInteractionCheckpoint07(state,disk,seq);
            Assert.That(ShopEntranceRoot07("ShopProducts").anchoredPosition,Is.EqualTo(new Vector2(368,-512)));
            yield return app.StartCoroutine(ShopEntranceFrame07());ShopEntrancePointer07("RefreshControllers");
            Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));Assert.That(app.Session.State.processedReceipts.Last().command.kind,Is.EqualTo(CommandKind.RefreshControllers));
            Assert.That(app.Session.State.shop.generationId,Is.EqualTo(identity));Assert.That(motion.EnterSequence,Is.EqualTo(enters));Assert.That(motion.IsPlaying,Is.False);
            Assert.That(app.TransactionVisualPlaying07,Is.True,"Existing successful refresh renderer must retain ownership of its own transaction.");
            state=StateJson();disk=File.ReadAllBytes(app.SaveLocation);seq=app.Session.ActionSequence;
            double deadline=Time.realtimeSinceStartupAsDouble+3;while(app.TransactionVisualPlaying07&&Time.realtimeSinceStartupAsDouble<deadline){AssertInteractionCheckpoint07(state,disk,seq);yield return null;}
            Assert.That(app.TransactionVisualPlaying07,Is.False);AssertInteractionCheckpoint07(state,disk,seq);
            Assert.That(motion.EnterSequence,Is.EqualTo(enters));Assert.That(motion.IsPlaying,Is.False);
            foreach(var offer in app.Session.State.shop.offers.Where(o=>o.category=="controller"))
            {var root=ShopEntranceRoot07("Offer_"+offer.offerId);Assert.That(root.anchoredPosition.y,Is.EqualTo(-124).Within(.01).Or.EqualTo(-316).Within(.01));Assert.That(root.GetComponent<CanvasGroup>().alpha,Is.EqualTo(1));}
            yield return app.StartCoroutine(ShopEntrancePng07("shop-entrance-refresh-final"));
        }
    }
}
