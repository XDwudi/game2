using System;
using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private IEnumerator TransactionShop07(Func<RunState,bool> condition)
        {
            Art07Fixtures.Apply(app,Art07FixtureKind.G147);
            for(uint seed=701;seed<861;seed++)
            {
                var state=app.Session.State;var scene=Art07Fixtures.Create(app.Config,Art07FixtureKind.G147,state.nextItemSerial,state.nextDieSerial);
                scene.gold=1000000;scene.remainingSubmissions=1;scene.target="1";scene.shopRngState=seed;
                var applied=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});Assert.That(applied.accepted,Is.True,applied.error);
                var submit=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(submit.accepted,Is.True,submit.error);app.SkipVisual();
                Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));
                var enter=app.Send(new SessionCommand{kind=CommandKind.Continue});Assert.That(enter.accepted,Is.True,enter.error);
                Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));yield return null;
                if(condition(app.Session.State)){Canvas.ForceUpdateCanvases();yield break;}
            }
            Assert.Fail("No real generated shop met the fixture condition in 160 deterministic seeds.");
        }
        private RectTransform TransactionRect07(string name)=>app.canvas.GetComponentsInChildren<RectTransform>(true).First(r=>r.name==name&&r.gameObject.activeInHierarchy);
        private IEnumerator TransactionAt07(float seconds)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+3;
            while(app.TransactionVisualPlaying07&&app.TransactionVisualElapsed07<seconds&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(app.TransactionVisualPlaying07,Is.True,"Transaction phase was skipped before sampling "+seconds+" at "+app.TransactionVisualElapsed07);
        }
        private IEnumerator TransactionDone07()
        {
            double deadline=Time.realtimeSinceStartupAsDouble+3;
            while(app.TransactionVisualPlaying07&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(app.TransactionVisualPlaying07,Is.False);Assert.That(app.Session.Busy,Is.False);
        }
        private System.Collections.Generic.List<Tuple<string,Rect>> TransactionReadableGlyphs07()
        {
            var result=new System.Collections.Generic.List<Tuple<string,Rect>>();
            var design=(RectTransform)app.canvas.transform;
            foreach(var text in app.canvas.GetComponentsInChildren<TMP_Text>(false))
            {
                if(text.GetComponentInParent<Art07PrefabView>()!=null||string.IsNullOrWhiteSpace(text.text)||text.canvasRenderer.GetInheritedAlpha()<=0)continue;
                text.ForceMeshUpdate();
                for(int i=0;i<text.textInfo.characterCount;i++)
                {
                    var glyph=text.textInfo.characterInfo[i];if(!glyph.isVisible||char.IsWhiteSpace(glyph.character))continue;
                    var mesh=text.textInfo.meshInfo[glyph.materialReferenceIndex];var vertices=Enumerable.Range(0,4).Select(v=>mesh.vertices[glyph.vertexIndex+v]).ToArray();
                    if(Enumerable.Range(0,4).All(v=>mesh.colors32[glyph.vertexIndex+v].a==0))continue;
                    var rect=Rect.MinMaxRect(vertices.Min(v=>v.x),vertices.Min(v=>v.y),vertices.Max(v=>v.x),vertices.Max(v=>v.y));
                    result.Add(Tuple.Create(text.name+"/"+glyph.character,BurstRectIn07(text.transform,rect,design)));
                }
            }
            return result;
        }
        private void AssertTransactionTicketClear07(System.Collections.Generic.List<Tuple<string,Rect>> oldGlyphs,bool reduced)
        {
            var ticket=LiveFx07("fx_cash_ticket").Single();Assert.That(ticket.ReducedMotion,Is.EqualTo(reduced));
            foreach(string required in new[]{"Money","Interest","ControllerShelfTitle"})
                Assert.That(app.canvas.GetComponentsInChildren<TMP_Text>(false).Any(t=>t.name==required&&!string.IsNullOrWhiteSpace(t.text)),Is.True,required);
            Assert.That(Button("RefreshControllers").GetComponentInChildren<TMP_Text>().text,Is.Not.Empty);
            var glyphs=TransactionReadableGlyphs07().Concat(oldGlyphs).ToArray();Assert.That(glyphs.Length,Is.GreaterThan(10));
            float previous=ticket.NormalizedTime;var design=(RectTransform)app.canvas.transform;
            try
            {
                foreach(float time in new[]{0f,.5f,.9f})
                {
                    ticket.Evaluate(time);Canvas.ForceUpdateCanvases();
                    var graphics=ticket.GetComponentsInChildren<UnityEngine.UI.Graphic>(false).Where(g=>g.enabled&&(!(g is TMP_Text t)||!string.IsNullOrEmpty(t.text))).ToArray();
                    Assert.That(graphics,Is.Not.Empty);
                    foreach(var graphic in graphics)
                    {
                        var rect=(RectTransform)graphic.transform;var bounds=BurstRectIn07(rect,rect.rect,design);
                        foreach(var glyph in glyphs)Assert.That(bounds.Overlaps(glyph.Item2),Is.False,"Cash ticket "+graphic.name+" covers "+glyph.Item1+" at "+time+" reduced="+reduced+" ticket="+bounds+" glyph="+glyph.Item2);
                    }
                }
            }
            finally{ticket.Evaluate(previous);}
        }
        private Rect TransactionFinalRect07(RectTransform target)
        {
            // Predict the actual final unrotated/unscaled target. Never derive the
            // expected width from the transient -80-degree flip being tested.
            var matrix=((RectTransform)app.canvas.transform).worldToLocalMatrix*target.parent.localToWorldMatrix*Matrix4x4.TRS(target.localPosition,Quaternion.identity,Vector3.one);
            var r=target.rect;var points=new[]{new Vector3(r.xMin,r.yMin),new Vector3(r.xMin,r.yMax),new Vector3(r.xMax,r.yMin),new Vector3(r.xMax,r.yMax)}.Select(matrix.MultiplyPoint3x4).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        private void AssertExpansionFrameMatchesFinalTarget07(string targetName)
        {
            var target=TransactionRect07(targetName);var frame=TransactionRect07("NewSlotOutline");
            var expected=TransactionFinalRect07(target);var actual=TransactionFinalRect07(frame);
            Assert.That(actual.xMin,Is.EqualTo(expected.xMin).Within(.5f),"Expansion frame final x");Assert.That(actual.yMin,Is.EqualTo(expected.yMin).Within(.5f),"Expansion frame final y");
            Assert.That(actual.width,Is.EqualTo(expected.width).Within(.5f),"Expansion frame must use the full unrotated slot width");Assert.That(actual.height,Is.EqualTo(expected.height).Within(.5f),"Expansion frame final height");
            TestContext.WriteLine("Expansion final geometry "+targetName+" expected="+expected+" frame="+actual+" animatedEuler="+target.localEulerAngles);
        }
        [UnityTest] public IEnumerator PLAY_ART07_TX_RefreshOnlyControllerShelfUsesOld100AndNew180Milliseconds()
        {
            foreach(bool reduced in new[]{false,true})
            {
            yield return TransactionShop07(s=>true);
            FinalOptions07("Normal",reduced);Canvas.ForceUpdateCanvases();var oldGlyphs=TransactionReadableGlyphs07();
            var before=app.Session.State;string other=JsonUtility.ToJson(new TransactionOfferList07{offers=before.shop.offers.Where(o=>o.category!="controller").ToArray()});
            var products=TransactionRect07("ShopProducts");var productScale=products.localScale;var productPosition=products.localPosition;
            var result=app.Send(new SessionCommand{kind=CommandKind.RefreshControllers});Assert.That(result.accepted,Is.True,result.error);
            Assert.That(app.TransactionVisualKind07,Is.EqualTo("controller_refresh"));Assert.That(app.TransactionVisualDuration07,Is.EqualTo(reduced?.12f:.28f).Within(.0001f));Assert.That(app.Session.Busy,Is.True);
            AssertTransactionTicketClear07(oldGlyphs,reduced);
            Assert.That(app.TransactionSnapshotGraphicCount07,Is.GreaterThan(0));Assert.That(app.TransactionAnimatedTargets07.All(x=>x.StartsWith("Offer_")||x.StartsWith("Buy_")||x.StartsWith("OfferRarity_")),Is.True);
            var next=app.Session.State;string hash=GameSession.StateHash(next);byte[] save=File.ReadAllBytes(app.SaveLocation);
            foreach(var offer in next.shop.offers.Where(o=>o.category=="controller"))Assert.That(TransactionRect07("Offer_"+offer.offerId).GetComponent<CanvasGroup>().alpha,Is.Zero);
            var repeat=app.Send(new SessionCommand{kind=CommandKind.RefreshControllers});Assert.That(repeat.accepted,Is.False);Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(hash));
            Click("Pause");float paused=app.TransactionVisualElapsed07;yield return new WaitForSecondsRealtime(.18f);
            Assert.That(app.TransactionVisualElapsed07,Is.EqualTo(paused));Click("CloseDialog");
            yield return TransactionAt07(reduced?.12f*.12f/.28f:.12f);
            foreach(var offer in next.shop.offers.Where(o=>o.category=="controller"))Assert.That(TransactionRect07("Offer_"+offer.offerId).GetComponent<CanvasGroup>().alpha,Is.GreaterThan(0));
            Assert.That(products.localScale,Is.EqualTo(productScale));Assert.That(products.localPosition,Is.EqualTo(productPosition));
            Assert.That(JsonUtility.ToJson(new TransactionOfferList07{offers=app.Session.State.shop.offers.Where(o=>o.category!="controller").ToArray()}),Is.EqualTo(other));
            yield return TransactionDone07();Assert.That(app.TransactionSnapshotGraphicCount07,Is.Zero);Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(hash));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
            }
        }
        [UnityTest] public IEnumerator PLAY_ART07_TX_PurchaseUsesCommittedNewInstanceAndSkipRestoresAllGroups()
        {
            foreach(bool reduced in new[]{false,true})
            {
            yield return TransactionShop07(s=>s.shop.offers.Any(o=>o.category=="controller"&&!s.controllers.Any(c=>c.definitionId==o.contentId)));
            FinalOptions07("Normal",reduced);Canvas.ForceUpdateCanvases();var oldGlyphs=TransactionReadableGlyphs07();
            var before=app.Session.State;var offer=before.shop.offers.First(o=>o.category=="controller"&&!before.controllers.Any(c=>c.definitionId==o.contentId));
            var result=app.Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId});Assert.That(result.accepted,Is.True,result.error);
            var acquired=app.Session.State.controllers.Single(c=>!before.controllers.Any(b=>b.instanceId==c.instanceId));
            Assert.That(app.TransactionVisualKind07,Is.EqualTo("purchase"));Assert.That(app.TransactionVisualDuration07,Is.EqualTo(reduced?.12f:.34f).Within(.0001f));
            AssertTransactionTicketClear07(oldGlyphs,reduced);
            Assert.That(app.LastTransactionSummary07,Does.Contain(acquired.instanceId));Assert.That(app.TransactionAnimatedTargets07,Does.Contain("Controller_"+acquired.instanceId));
            Assert.That(TransactionRect07("Controller_"+acquired.instanceId).GetComponent<CanvasGroup>().alpha,Is.Zero);
            string hash=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            app.SkipVisual();Assert.That(app.TransactionVisualPlaying07,Is.False);Assert.That(app.Session.Busy,Is.False);Assert.That(app.TransactionSnapshotGraphicCount07,Is.Zero);
            Assert.That(TransactionRect07("Controller_"+acquired.instanceId).GetComponent<CanvasGroup>().alpha,Is.EqualTo(1));
            Assert.That(TransactionRect07("Offer_"+offer.offerId).GetComponent<CanvasGroup>().alpha,Is.EqualTo(1));
            Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(hash));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));yield return null;
            }
        }
        [UnityTest] public IEnumerator PLAY_ART07_TX_ExpansionAnimatesOnlyActuallyAddedSlotAndPreservesOtherOffers()
        {
            foreach(string service in new[]{"O05","O06"})
            {
                yield return TransactionShop07(s=>s.shop.offers.Any(o=>o.contentId==service));yield return null;
                var before=app.Session.State;var offer=before.shop.offers.Single(o=>o.contentId==service);string category=service=="O05"?"controller":"consumable";
                var result=app.Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId});Assert.That(result.accepted,Is.True,result.error);
                var after=app.Session.State;var added=after.shop.offers.Where(o=>o.category==category&&!before.shop.offers.Any(b=>b.offerId==o.offerId)).ToArray();Assert.That(added,Has.Length.EqualTo(1));
                Assert.That(app.TransactionVisualKind07,Is.EqualTo("offer_expansion"));Assert.That(app.TransactionVisualDuration07,Is.EqualTo(.34f).Within(.0001f));
                Assert.That(app.TransactionAnimatedTargets07.All(n=>n=="NewSlotOutline"||n.Contains(added[0].offerId)),Is.True);
                foreach(var prior in before.shop.offers.Where(o=>o.offerId!=offer.offerId))Assert.That(JsonUtility.ToJson(after.shop.offers.Single(o=>o.offerId==prior.offerId)),Is.EqualTo(JsonUtility.ToJson(prior)));
                // v0.73: every section is on one page, so the new consumable is its real product card (scrolled into view), not a stand-in.
                if(service=="O06"){Assert.That(app.TransactionAnimatedTargets07,Does.Contain("Product_"+added[0].offerId));Assert.That(TransactionRect07("Product_"+offer.offerId),Is.Not.Null,"The service card stays visible.");}
                AssertExpansionFrameMatchesFinalTarget07((service=="O05"?"Offer_":"Product_")+added[0].offerId);
                string hash=GameSession.StateHash(after);byte[] save=File.ReadAllBytes(app.SaveLocation);yield return TransactionDone07();Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(hash));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
            }
        }
        [UnityTest] public IEnumerator PLAY_ART07_TX_PhysicalUpgradeUsesActualFaceAndOverlapping360Or420Timeline()
        {
            foreach(string upgrade in new[]{"U07","U05"})
            {
                yield return TransactionShop07(s=>s.shop.offers.Any(o=>o.contentId==upgrade));yield return null;
                var before=app.Session.State;var offer=before.shop.offers.Single(o=>o.contentId==upgrade);var die=before.dice.First(d=>d.lifetime=="permanent");string face=upgrade=="U05"?null:die.faces.First(f=>app.Session.IsUpgradeTargetLegal(upgrade,die.dieId,f.faceId)).faceId;
                var result=app.Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId,targetDieId=die.dieId,targetFaceId=face});Assert.That(result.accepted,Is.True,result.error);
                Assert.That(app.TransactionVisualKind07,Is.EqualTo("die_upgrade"));Assert.That(app.TransactionVisualDuration07,Is.EqualTo(upgrade=="U05"?.42f:.36f).Within(.0001f));
                Assert.That(TransactionRect07("ShopProducts").GetComponent<CanvasGroup>().alpha,Is.Zero);
                string hash=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);var after=app.Session.State.dice.Single(d=>d.dieId==die.dieId);
                if(face!=null)foreach(var original in die.faces.Where(f=>f.faceId!=face))Assert.That(JsonUtility.ToJson(after.faces.Single(f=>f.faceId==original.faceId)),Is.EqualTo(JsonUtility.ToJson(original)));
                yield return TransactionAt07(.22f);
                var preview=app.table.GetFaceOrientation("$die-preview");Assert.That(preview.gameObject.activeInHierarchy,Is.True);
                if(face!=null)Assert.That(preview.Find("Face_"+face).GetComponent<MeshFilter>().sharedMesh.name,Does.StartWith("Pips_"+after.faces.Single(f=>f.faceId==face).permanentPip+"_"));
                else {var props=new MaterialPropertyBlock();preview.Find("Body").GetComponent<MeshRenderer>().GetPropertyBlock(props);Assert.That(props.GetFloat("_Glass"),Is.GreaterThan(0).And.LessThanOrEqualTo(1));}
                yield return TransactionDone07();Assert.That(preview.gameObject.activeInHierarchy,Is.False);Assert.That(TransactionRect07("ShopProducts").GetComponent<CanvasGroup>().alpha,Is.EqualTo(1));
                Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(hash));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
            }
        }
        [UnityTest] public IEnumerator PLAY_ART07_TX_FailedPurchaseDiscardsInvisibleCaptureAndKeepsCheckpoint()
        {
            yield return TransactionShop07(s=>s.shop.offers.Any(o=>o.category=="die_upgrade"));yield return null;
            var before=app.Session.State;var offer=before.shop.offers.First(o=>o.category=="die_upgrade");string hash=GameSession.StateHash(before);byte[] save=File.ReadAllBytes(app.SaveLocation);
            var result=app.Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId,targetDieId="missing",targetFaceId="F01"});Assert.That(result.accepted,Is.False);
            Assert.That(app.TransactionVisualPlaying07,Is.False);Assert.That(app.Session.Busy,Is.False);Assert.That(app.TransactionSnapshotGraphicCount07,Is.Zero);
            Assert.That(app.Session.State.shop.offers.Single(o=>o.offerId==offer.offerId).sold,Is.False);
            Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(hash));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));yield return null;
        }
        [UnityTest] public IEnumerator PLAY_ART07_TX_SaveFailureRetainsOldShelfAndRetryAnimatesSameSavedCandidate()
        {
            yield return TransactionShop07(s=>true);string before=GameSession.StateHash(app.Session.State);byte[] disk=File.ReadAllBytes(app.SaveLocation);RunState candidate;
            using(var locked=new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                var result=app.Send(new SessionCommand{kind=CommandKind.RefreshControllers});Assert.That(result.accepted,Is.False);Assert.That(app.Session.HasPendingSave,Is.True);
                Assert.That(app.TransactionVisualPlaying07,Is.False);Assert.That(app.Session.Busy,Is.False);Assert.That(app.TransactionSnapshotGraphicCount07,Is.Zero);
                Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                candidate=JsonUtility.FromJson<RunState>(JsonUtility.FromJson<TransactionSaveEnvelope07>(File.ReadAllText(app.SaveLocation+".tmp")).payload);
                Click("RetrySave");Assert.That(app.Session.HasPendingSave,Is.True);Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(before));
            }
            Click("RetrySave");Assert.That(app.Session.HasPendingSave,Is.False);Assert.That(app.TransactionVisualPlaying07,Is.True);
            Assert.That(app.TransactionSnapshotGraphicCount07,Is.GreaterThan(0));Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(GameSession.StateHash(candidate)));
            string committed=GameSession.StateHash(app.Session.State);byte[] committedDisk=File.ReadAllBytes(app.SaveLocation);
            yield return TransactionDone07();Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(committed));CollectionAssert.AreEqual(committedDisk,File.ReadAllBytes(app.SaveLocation));
        }
        [Serializable] private sealed class TransactionSaveEnvelope07 { public string payload; }
        [Serializable] private sealed class TransactionOfferList07 { public ShopOffer[] offers; }
    }
}
