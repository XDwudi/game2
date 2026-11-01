using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceCraft.Verification
{
    public enum Art07TransactionClip { None, RefreshControllers, BuyController, ExpandControllers, ExpandItems, RandomFace, GlassDie }

    // Finite debug-only routes on the existing recorder and clock. No new renderer/rule clock.
    public sealed partial class Art07VisualRecorder
    {
        [Serializable] private sealed class TransactionSource07 { public string path,sha256; }
        [Serializable] private sealed class TransactionSetup07
        {public string kind,commandId,stateHash,saveSha;public long sequence;public bool accepted,pendingSave;public SessionCommand command;}
        [Serializable] private sealed class TransactionTarget07
        {public string name;public bool active;public float alpha;public Vector3 position,scale;public Quaternion rotation;}
        [Serializable] private sealed class TransactionFrame07
        {
            public string clip,kind,receiptId,sourceManifestSha,summary,targetFaceMesh;public long sequence;
            public bool playing,busy,previewActive;public float elapsed,duration,glass;
            public int snapshotGraphics,activeSnapshotRenderers,previewRenderers;
            public TransactionTarget07[] targets;
        }
        private Art07TransactionClip transactionClip07;
        private TransactionSource07[] transactionSources07;
        private string transactionReceiptId07,transactionDie07,transactionFace07;
        private long transactionSequence07;

        public static Art07VisualRecorder BeginTransaction(GameApp owner,string absoluteOutputDirectory,Art07TransactionClip clip,bool reducedMotion=false)
        {
            if(clip==Art07TransactionClip.None||!Enum.IsDefined(typeof(Art07TransactionClip),clip))throw new ArgumentOutOfRangeException(nameof(clip));
            return BeginCore07(owner,absoluteOutputDirectory,reducedMotion?Art07RecordingMode.ReducedHighest:Art07RecordingMode.NormalHighest,3,clip);
        }
        private TransactionSource07[] TransactionSources07()
        {
            string root=Path.Combine(Application.dataPath,"Game");
            return Directory.GetFiles(root,"*.cs",SearchOption.AllDirectories).Where(p=>!p.Replace('\\','/').Split('/').Any(s=>s.EndsWith("~",StringComparison.Ordinal)))
                .Concat(new[]{Path.Combine(root,"Data/P0Config.json")}).OrderBy(p=>p,StringComparer.Ordinal).Select(p=>new TransactionSource07{path=p,sha256=HashFile(p)}).ToArray();
        }
        private void CheckTransactionSources07()
        {
            var now=TransactionSources07();if(now.Length!=transactionSources07.Length||now.Where((s,i)=>s.path!=transactionSources07[i].path||s.sha256!=transactionSources07[i].sha256).Any())
                throw new InvalidOperationException("Transaction source/config changed while recording.");
            if(typeof(GameApp).Module.ModuleVersionId.ToString()!=report.runtimeAssemblyMvid||typeof(GameSession).Module.ModuleVersionId.ToString()!=report.coreAssemblyMvid)
                throw new InvalidOperationException("Loaded assembly changed while recording.");
        }
        private CommandResult TransactionSetupSend07(SessionCommand cmd)
        {
            var result=app.Send(cmd);var row=new TransactionSetup07{kind=cmd.kind.ToString(),commandId=result.receipt?.commandId,accepted=result.accepted,pendingSave=result.pendingSave,
                stateHash=GameSession.StateHash(app.Session.State),saveSha=HashFile(app.SaveLocation),sequence=app.Session.ActionSequence,command=result.receipt?.command??cmd};
            File.AppendAllText(Path.Combine(report.outputDirectory,"setup-commands.jsonl"),JsonUtility.ToJson(row)+"\n");
            if(!result.accepted||result.pendingSave||app.Session.HasPendingSave)throw new InvalidOperationException("Real debug-shop preparation failed: "+result.error);
            lastProgressAt=Time.realtimeSinceStartupAsDouble;return result;
        }
        private bool ChooseTransaction07(out ShopOffer offer,out string die,out string face)
        {
            offer=null;die=face=null;var s=app.Session.State;
            if(transactionClip07==Art07TransactionClip.RefreshControllers)return s.gold>=app.Session.GetRefreshPrice();
            string id=transactionClip07==Art07TransactionClip.ExpandControllers?"O05":transactionClip07==Art07TransactionClip.ExpandItems?"O06":transactionClip07==Art07TransactionClip.RandomFace?"U07":transactionClip07==Art07TransactionClip.GlassDie?"U05":null;
            foreach(var candidate in s.shop.offers.Where(o=>!o.sold&&(id==null?o.category=="controller"&&!s.controllers.Any(c=>c.definitionId==o.contentId):o.contentId==id)))
            {
                if(candidate.category=="die_upgrade")
                {
                    foreach(var d in s.dice.Where(d=>d.lifetime=="permanent"))foreach(var f in d.faces)
                    {
                        string targetFace=id=="U05"?null:f.faceId;
                        if(app.Session.PurchaseError(candidate.offerId,d.dieId,targetFace)!=null)continue;
                        offer=candidate;die=d.dieId;face=targetFace;return true;
                    }
                }
                else if(app.Session.PurchaseError(candidate.offerId)==null){offer=candidate;return true;}
            }
            return false;
        }
        private Button TransactionButton07(string name)=>app.canvas.GetComponentsInChildren<Button>(false).Single(b=>b.name==name&&b.gameObject.activeInHierarchy);
        private static Rect TransactionBounds07(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(null,p)).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        private bool TransactionHit07(Button button)
        {
            if(button==null||!button.IsInteractable())return false;
            Rect bounds=TransactionBounds07((RectTransform)button.transform);
            if(bounds.width<39.99f||bounds.height<39.99f||bounds.xMin<0||bounds.yMin<0||bounds.xMax>Screen.width||bounds.yMax>Screen.height)return false;
            foreach(var scroll in button.GetComponentsInParent<ScrollRect>(false))
            {Rect viewport=TransactionBounds07(scroll.viewport);if(bounds.yMin<viewport.yMin-.5f||bounds.yMax>viewport.yMax+.5f||bounds.xMin<viewport.xMin-.5f||bounds.xMax>viewport.xMax+.5f)return false;}
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=bounds.center},hits);
            return hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==button;
        }
        private void TransactionClick07(Button button)
        {
            if(!TransactionHit07(button))throw new InvalidOperationException("Transaction UI is not a visible40px first-raycast target: "+button?.name);
            var bounds=TransactionBounds07((RectTransform)button.transform);
            ExecuteEvents.Execute(button.gameObject,new PointerEventData(EventSystem.current){position=bounds.center,button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);Canvas.ForceUpdateCanvases();
        }
        private IEnumerator ReadyTransactionButton07(Button button)
        {
            double deadline=Time.realtimeSinceStartupAsDouble+3;
            while(!TransactionHit07(button))
            {
                foreach(var scroll in button.GetComponentsInParent<ScrollRect>(false))
                {
                    Rect target=TransactionBounds07((RectTransform)button.transform),viewport=TransactionBounds07(scroll.viewport);
                    if(target.yMin<viewport.yMin-.5f||target.yMax>viewport.yMax+.5f)
                        ExecuteEvents.Execute(scroll.gameObject,new PointerEventData(EventSystem.current){position=viewport.center,scrollDelta=new Vector2(0,target.center.y>viewport.center.y?1:-1)},ExecuteEvents.scrollHandler);
                }
                if(Time.realtimeSinceStartupAsDouble>deadline)throw new InvalidOperationException("Transaction UI did not become actually clickable: "+button.name);
                Canvas.ForceUpdateCanvases();yield return null;
            }
        }
        private IEnumerator RecordTransaction07()
        {
            // Record every actual fixture command before the contiguous visual span starts.
            transactionSources07=TransactionSources07();string manifest=string.Join("\n",transactionSources07.Select(s=>s.path+" "+s.sha256));
            report.transactionClip=transactionClip07.ToString();report.transactionSourceManifestSha256=Hash(System.Text.Encoding.UTF8.GetBytes(manifest));
            File.WriteAllText(Path.Combine(report.outputDirectory,"source-snapshot.sha256"),manifest);WriteReport();
            var debugClose=app.canvas.GetComponentsInChildren<Button>(false).FirstOrDefault(b=>b.name=="RunDebugScene");
            if(debugClose!=null){var ready=ReadyTransactionButton07(debugClose);while(ready.MoveNext())yield return ready.Current;TransactionClick07(debugClose);yield return null;}
            ShopOffer offer=null;string die=null,face=null;bool found=false;
            for(uint seed=701;seed<861;seed++)
            {
                var state=app.Session.State;var scene=Art07Fixtures.Create(app.Config,Art07FixtureKind.G147,state.nextItemSerial,state.nextDieSerial);
                scene.sourceRunId=state.sourceRunId;scene.gold=100;scene.target="1";scene.remainingSubmissions=1;scene.shopRngState=seed;
                scene.controllerOfferPurchases=app.Config.shop.maxControllerOffers-app.Config.shop.initialControllerOffers-(transactionClip07==Art07TransactionClip.ExpandControllers?1:0);
                scene.consumableOfferPurchases=app.Config.shop.maxConsumableOffers-app.Config.shop.initialConsumableOffers-(transactionClip07==Art07TransactionClip.ExpandItems?1:0);
                TransactionSetupSend07(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});
                TransactionSetupSend07(new SessionCommand{kind=CommandKind.Submit});app.SkipVisual();
                if(app.Session.State.phase!="StageSettlement")throw new InvalidOperationException("Debug target did not produce the real settlement.");
                TransactionSetupSend07(new SessionCommand{kind=CommandKind.Continue});yield return null;
                if(app.Session.State.phase!="Shop")throw new InvalidOperationException("Real Continue did not enter the shop.");
                if(ChooseTransaction07(out offer,out die,out face)){found=true;break;}
            }
            if(!found)throw new InvalidOperationException("No legal generated shop for transaction route within the existing160-seed fixture budget.");
            File.WriteAllText(Path.Combine(report.outputDirectory,"fixture-shop.json"),JsonUtility.ToJson(app.Session.State,true));
            SetOptions("Normal",IsReduced(mode));yield return null;
            string setupState=GameSession.StateHash(app.Session.State),setupSave=HashFile(app.SaveLocation);transactionDie07=die;transactionFace07=face;
            // Prepare S13 through the actual offer, die and face controls. Only the final click mutates rules.
            var route=new List<Func<Button>>();string trigger;
            if(transactionClip07==Art07TransactionClip.RefreshControllers)trigger="RefreshControllers";
            else if(transactionClip07==Art07TransactionClip.BuyController)trigger="Buy_"+offer.offerId;
            else
            {
                route.Add(()=>TransactionButton07(offer.category=="die_upgrade"?"Tab2":"Tab3"));
                route.Add(()=>app.canvas.GetComponentsInChildren<Transform>(false).Single(t=>t.name=="Product_"+offer.offerId).GetComponentsInChildren<Button>(false).Single(b=>b.name=="Inspect"));
                if(die!=null)route.Add(()=>TransactionButton07("Target_"+die));
                if(face!=null)route.Add(()=>TransactionButton07("Face_"+face));
                trigger="ConfirmPurchase";
            }
            foreach(var get in route)
            {
                var b=get();var ready=ReadyTransactionButton07(b);while(ready.MoveNext())yield return ready.Current;
                TransactionClick07(b);yield return null;
                if(GameSession.StateHash(app.Session.State)!=setupState||HashFile(app.SaveLocation)!=setupSave)throw new InvalidOperationException("Preparing a transaction target changed saved rules.");
            }
            var final=TransactionButton07(trigger);var finalReady=ReadyTransactionButton07(final);while(finalReady.MoveNext())yield return finalReady.Current;
            CheckTransactionSources07();expectedStateHash=setupState;expectedSaveHash=setupSave;transactionSequence07=app.Session.ActionSequence;clockEnabled=true;cycleTicks=0;
            File.WriteAllText(Path.Combine(report.outputDirectory,"state-before-transaction.json"),JsonUtility.ToJson(app.Session.State,true));
            yield return null;
            for(int i=0;i<15;i++){yield return new WaitForEndOfFrame();Capture("lead",0,null);yield return null;}
            var before=app.Session.State;long quotedCost=transactionClip07==Art07TransactionClip.RefreshControllers?app.Session.GetRefreshPrice():app.Session.GetOfferNetPrice(offer);
            var detail=new Cycle{index=0,firstFrame=frameIndex,command=transactionClip07==Art07TransactionClip.RefreshControllers?"RefreshControllers":"Buy",stateHashBefore=expectedStateHash,saveShaBefore=expectedSaveHash};report.cycles.Add(detail);
            TransactionClick07(TransactionButton07(trigger));var after=app.Session.State;
            if(app.Session.HasPendingSave||after.actionSequence!=before.actionSequence+1)throw new InvalidOperationException("Exactly one successful saved transaction was required.");
            var committed=after.processedReceipts.Last();if(committed.command.kind.ToString()!=detail.command||offer!=null&&committed.command.offerId!=offer.offerId||die!=null&&committed.command.targetDieId!=die||face!=null&&committed.command.targetFaceId!=face)
                throw new InvalidOperationException("The clicked action committed a different command/target.");
            transactionReceiptId07=committed.commandId;transactionSequence07=after.actionSequence;detail.receiptId=transactionReceiptId07;
            VerifyTransactionReceipt07(before,after,offer,die,face,quotedCost);
            detail.durationMilliseconds=Mathf.RoundToInt(app.TransactionVisualDuration07*1000);int normal=transactionClip07==Art07TransactionClip.RefreshControllers?280:transactionClip07==Art07TransactionClip.GlassDie?420:transactionClip07==Art07TransactionClip.RandomFace?360:340;
            if(detail.durationMilliseconds!=(IsReduced(mode)?Math.Min(120,normal):normal)||!app.TransactionVisualPlaying07)throw new InvalidOperationException("Actual transaction duration/renderer did not match the production route.");
            expectedStateHash=GameSession.StateHash(after);expectedSaveHash=HashFile(app.SaveLocation);
            File.WriteAllText(Path.Combine(report.outputDirectory,"state-committed.json"),JsonUtility.ToJson(after,true));File.WriteAllText(Path.Combine(report.outputDirectory,"transaction-receipt.json"),JsonUtility.ToJson(committed,true));
            int expectedTicks=(int)Math.Ceiling(detail.durationMilliseconds*FramesPerSecond/1000d),captured=0;
            while(true)
            {
                yield return new WaitForEndOfFrame();Capture("transaction",0,detail.command);captured++;
                if(captured>2&&totalTicks==0)throw new InvalidOperationException("The existing fixed60Hz presentation hook did not run for the transaction.");
                if(!app.TransactionVisualPlaying07)break;
                if(captured>expectedTicks+3)throw new InvalidOperationException("Transaction did not end on its actual declared timeline.");yield return null;
            }
            detail.clockTicks=cycleTicks;if(Math.Abs(cycleTicks-expectedTicks)>1)throw new InvalidOperationException("Transaction tick count differs from declared fixed60Hz duration.");
            detail.lastFrame=frameIndex-1;detail.completed=true;detail.stateHashAfter=GameSession.StateHash(app.Session.State);detail.saveShaAfter=HashFile(app.SaveLocation);detail.stateAndSaveStable=detail.stateHashAfter==expectedStateHash&&detail.saveShaAfter==expectedSaveHash;
            if(!detail.stateAndSaveStable||app.Session.Busy||app.TransactionSnapshotGraphicCount07!=0)throw new InvalidOperationException("Transaction cleanup or stable committed state failed.");WriteReport();
            for(int i=0;i<30;i++){yield return null;yield return new WaitForEndOfFrame();Capture("hold",0,detail.command);}
            CheckTransactionSources07();yield return null;
        }
        private TransactionFrame07 InspectTransactionFrame07(string stage)
        {
            if(!app.IsDebugWorkspace)throw new InvalidOperationException("A transaction capture left the isolated debug workspace.");
            if(app.Session.ActionSequence!=transactionSequence07)throw new InvalidOperationException("Action sequence changed outside the single recorded transaction.");
            var motion=app.canvas.GetComponentsInChildren<DiceCraft.Presentation.Art07TransactionMotion>(true).SingleOrDefault();
            var preview=app.table.GetFaceOrientation("$die-preview");bool visible=preview!=null&&preview.gameObject.activeInHierarchy;
            var row=new TransactionFrame07{clip=transactionClip07.ToString(),kind=app.TransactionVisualKind07,receiptId=transactionReceiptId07,sourceManifestSha=report.transactionSourceManifestSha256,sequence=app.Session.ActionSequence,
                playing=app.TransactionVisualPlaying07,busy=app.Session.Busy,elapsed=app.TransactionVisualElapsed07,duration=app.TransactionVisualDuration07,summary=app.LastTransactionSummary07,snapshotGraphics=app.TransactionSnapshotGraphicCount07,
                activeSnapshotRenderers=motion==null?0:motion.GetComponentsInChildren<CanvasRenderer>(false).Count(r=>r.GetMesh()!=null&&r.GetMesh().vertexCount>0&&r.GetInheritedAlpha()>0),previewActive=visible,previewRenderers=visible?preview.GetComponentsInChildren<MeshRenderer>(false).Count(r=>r.enabled):0};
            row.targets=app.TransactionAnimatedTargets07.Select(name=>app.canvas.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(t=>t.name==name)).Where(t=>t!=null)
                .Select(t=>new TransactionTarget07{name=t.name,active=t.gameObject.activeInHierarchy,alpha=t.GetComponent<CanvasGroup>()?.alpha??1,position=t.localPosition,scale=t.localScale,rotation=t.localRotation}).ToArray();
            if(visible){string face=transactionFace07??"F02";row.targetFaceMesh=preview.Find("Face_"+face)?.GetComponent<MeshFilter>()?.sharedMesh?.name;var renderer=preview.Find("Body")?.GetComponent<MeshRenderer>();if(renderer!=null){var props=new MaterialPropertyBlock();renderer.GetPropertyBlock(props);row.glass=props.GetFloat("_Glass");}}
            if(stage=="transaction"&&row.playing)
            {
                if(motion==null||row.targets.Length==0)throw new InvalidOperationException("Transaction has no real motion/animated target renderer.");
                if((transactionClip07==Art07TransactionClip.RefreshControllers||transactionClip07==Art07TransactionClip.BuyController)&&row.elapsed<.05f&&row.activeSnapshotRenderers==0)throw new InvalidOperationException("Old shop renderer missing at the transaction opening.");
                if(transactionDie07!=null&&(!row.previewActive||row.previewRenderers==0||string.IsNullOrEmpty(row.targetFaceMesh)))throw new InvalidOperationException("Physical upgrade preview renderer is missing.");
            }
            return row;
        }
        private void VerifyTransactionReceipt07(RunState before,RunState after,ShopOffer offer,string die,string face,long quotedCost)
        {
            if(after.gold!=before.gold-quotedCost)throw new InvalidOperationException("Actual wallet differs from the visible transaction quote.");
            if(transactionClip07==Art07TransactionClip.RefreshControllers)
            {
                var old=before.shop.offers.Where(o=>o.category!="controller").Select(JsonUtility.ToJson);
                var current=after.shop.offers.Where(o=>o.category!="controller").Select(JsonUtility.ToJson);
                if(!old.SequenceEqual(current))throw new InvalidOperationException("Controller refresh changed another shelf.");return;
            }
            if(offer==null||!after.shop.offers.Single(o=>o.offerId==offer.offerId).sold)throw new InvalidOperationException("Purchased offer was not committed sold.");
            if(transactionClip07==Art07TransactionClip.BuyController)
            {
                var acquired=after.controllers.Where(c=>!before.controllers.Any(b=>b.instanceId==c.instanceId)).ToArray();
                if(acquired.Length!=1||acquired[0].instanceId!=offer.controller.instanceId||acquired[0].definitionId!=offer.contentId)throw new InvalidOperationException("Acquired controller does not match its committed quote.");
            }
            if(transactionClip07==Art07TransactionClip.ExpandControllers||transactionClip07==Art07TransactionClip.ExpandItems)
            {
                bool controllers=transactionClip07==Art07TransactionClip.ExpandControllers;string category=controllers?"controller":"consumable";
                int old=controllers?before.controllerOfferPurchases:before.consumableOfferPurchases,current=controllers?after.controllerOfferPurchases:after.consumableOfferPurchases;
                if(current!=old+1||after.shop.offers.Count(o=>o.category==category&&!before.shop.offers.Any(b=>b.offerId==o.offerId))!=1)throw new InvalidOperationException("Expansion must add exactly the real new slot/quote.");
                foreach(var prior in before.shop.offers.Where(o=>o.offerId!=offer.offerId))if(JsonUtility.ToJson(after.shop.offers.Single(o=>o.offerId==prior.offerId))!=JsonUtility.ToJson(prior))throw new InvalidOperationException("Expansion changed an old quote.");
            }
            if(die!=null)
            {
                var old=before.dice.Single(d=>d.dieId==die);var current=after.dice.Single(d=>d.dieId==die);
                if(current.dataRevision!=old.dataRevision+1)throw new InvalidOperationException("Physical upgrade revision did not advance once.");
                foreach(var prior in before.dice.Where(d=>d.dieId!=die))if(JsonUtility.ToJson(after.dice.Single(d=>d.dieId==prior.dieId))!=JsonUtility.ToJson(prior))throw new InvalidOperationException("Upgrade modified another die.");
                if(face==null){if(old.isGlass||!current.isGlass||!old.faces.Select(JsonUtility.ToJson).SequenceEqual(current.faces.Select(JsonUtility.ToJson)))throw new InvalidOperationException("Glass upgrade did not retain the same six permanent faces.");}
                else
                {
                    var previous=old.faces.Single(f=>f.faceId==face);var next=current.faces.Single(f=>f.faceId==face);
                    if(previous.permanentPip==next.permanentPip||previous.baseColor!=next.baseColor||previous.texture!=next.texture||previous.isGold!=next.isGold)throw new InvalidOperationException("U07 must change only its committed target point value.");
                    if(!old.faces.Where(f=>f.faceId!=face).Select(JsonUtility.ToJson).SequenceEqual(current.faces.Where(f=>f.faceId!=face).Select(JsonUtility.ToJson)))throw new InvalidOperationException("U07 changed a different physical face.");
                }
            }
        }
    }
}
