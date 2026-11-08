using System.Collections;
using System.Collections.Generic;
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
        [UnityTest]
        public IEnumerator PLAY_ART071_FiveDiceGlassTemporaryLabelsAndFaceHitsSurviveConfirmThenReroll()
        {
            yield return DiceLabelBegin07(100);
            var current=app.Session.State;
            var scene=Art07Fixtures.Create(app.Config,Art07FixtureKind.GoldenSample,current.nextItemSerial,current.nextDieSerial);
            scene.sourceRunId=current.sourceRunId;
            scene.orderedLockedIds.Clear();
            foreach(var result in scene.currentResults)result.isLocked=false;
            // This remains a real five-die debug checkpoint, not ten dice with only
            // the last three shown. One pool die exercises both property lines.
            scene.dice[1].isGlass=false;
            scene.dice[2].isGlass=true;
            scene.dice[2].lifetime="stage_temporary";
            scene.dice[2].createdStageId=app.Config.stages[0].stageId;
            var applied=app.Send(new SessionCommand{kind=CommandKind.ApplyDebugScene,debugScene=scene});
            Assert.That(applied.accepted,Is.True,applied.error);
            Assert.That(applied.pendingSave,Is.False);
            if(HasActive("RunDebugScene"))Click("RunDebugScene");
            if(HasActive("CloseDialog"))Click("CloseDialog");
            yield return WaitInteractionLayout07();
            Assert.That(app.Session.State.dice.Count,Is.EqualTo(5));
            Assert.That(UiKit.FontScale,Is.EqualTo(1));
            string[] firstLocks=app.Session.State.dice.Take(2).Select(d=>d.dieId).ToArray();
            string propertyDie=app.Session.State.dice[2].dieId;
            DiceLabelCheck07();Art071AssertPoolFacesAreHittable();
            yield return CaptureEdge07("v071-five-dice-full-pool-attributes");

            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
            foreach(string id in firstLocks)Art071PointerClick("Die_"+id);
            yield return WaitInteractionLayout07();
            CollectionAssert.AreEqual(firstLocks,app.Session.PendingIds);
            AssertInteractionCheckpoint07(before,disk,sequence);
            DiceLabelCheck07();Art071AssertPoolFacesAreHittable();
            Art071PointerClick("Confirm");
            yield return WaitForIdle();yield return WaitInteractionLayout07();
            CollectionAssert.AreEqual(firstLocks,app.Session.State.hand.orderedLockedIds);
            Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+1));
            Assert.That(app.Session.State.processedReceipts.Last().command.kind,Is.EqualTo(CommandKind.ConfirmLocks));
            Assert.That(app.Session.PendingIds,Is.Empty);
            DiceLabelCheck07();Art071AssertPoolFacesAreHittable();

            before=StateJson();disk=File.ReadAllBytes(app.SaveLocation);sequence=app.Session.ActionSequence;
            var orientation=app.table.GetFaceOrientation(propertyDie);
            var face=orientation.GetComponentsInChildren<MeshRenderer>(false).First(r=>r.enabled&&r.name.StartsWith("Face_F"));
            var facePoint=(Vector2)app.table.viewCamera.WorldToScreenPoint(face.transform.position);
            Art071PointerClick("Die_"+propertyDie,facePoint);
            yield return WaitInteractionLayout07();
            CollectionAssert.AreEqual(new[]{propertyDie},app.Session.PendingIds);
            AssertInteractionCheckpoint07(before,disk,sequence);
            var propertyLabel=FindActive<TextMeshProUGUI>("DieState"+propertyDie);
            Assert.That(propertyLabel.text,Does.Contain("玻璃").And.Contain("临时"));
            Assert.That(FindActive<TextMeshProUGUI>("Pending"+propertyDie).text,Is.EqualTo("01 待锁"));
            DiceLabelCheck07();Art071AssertPoolFacesAreHittable();
            yield return CaptureEdge07("v071-three-pool-glass-temporary-pending");

            Art071PointerClick("Confirm");
            yield return WaitForIdle();yield return WaitInteractionLayout07();
            var locks=firstLocks.Concat(new[]{propertyDie}).ToArray();
            CollectionAssert.AreEqual(locks,app.Session.State.hand.orderedLockedIds);
            Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+1));
            Assert.That(app.Session.State.processedReceipts.Last().command.kind,Is.EqualTo(CommandKind.ConfirmLocks));
            Assert.That(app.Session.PendingIds,Is.Empty);
            Assert.That(HasActive("Pending"+propertyDie),Is.False);
            DiceLabelCheck07();Art071AssertPoolFacesAreHittable();
            var lockedResults=locks.ToDictionary(id=>id,id=>JsonUtility.ToJson(app.Session.State.hand.currentResults.Single(r=>r.dieId==id)));
            int rolls=app.Session.State.hand.rollsRemaining;sequence=app.Session.ActionSequence;
            Art071PointerClick("Roll");
            yield return WaitForIdle();yield return WaitInteractionLayout07();
            Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+1));
            Assert.That(app.Session.State.processedReceipts.Last().command.kind,Is.EqualTo(CommandKind.Roll));
            Assert.That(app.Session.State.hand.rollsRemaining,Is.EqualTo(rolls-1));
            CollectionAssert.AreEqual(locks,app.Session.State.hand.orderedLockedIds);
            foreach(string id in locks)Assert.That(JsonUtility.ToJson(app.Session.State.hand.currentResults.Single(r=>r.dieId==id)),Is.EqualTo(lockedResults[id]),"Reroll must leave every locked physical face/result unchanged.");
            Assert.That(app.Session.State.hand.currentResults.Where(r=>!locks.Contains(r.dieId)).All(r=>r.hasRolled&&!r.isLocked),Is.True);
            Assert.That(app.Session.State.dice.Count,Is.EqualTo(5));
            Assert.That(app.Session.PendingIds,Is.Empty);
            Assert.That(app.Session.HasPendingSave,Is.False);
            DiceLabelCheck07();Art071AssertPoolFacesAreHittable();
            yield return CaptureEdge07("v071-five-dice-confirmed-and-rerolled");
        }

        private GameObject Art071RaycastTarget(Vector2 point)
        {
            var data=new PointerEventData(EventSystem.current){position=point};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Assert.That(hits,Is.Not.Empty,"Real GraphicRaycaster found no target at "+point);
            return ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
        }
        private void Art071PointerClick(string name,Vector2? screenPoint=null)
        {
            Canvas.ForceUpdateCanvases();var root=FindActive<RectTransform>(name);
            Vector2 point=screenPoint??RectTransformUtility.WorldToScreenPoint(null,root.TransformPoint(root.rect.center));
            var target=Art071RaycastTarget(point);
            Assert.That(target,Is.SameAs(root.gameObject),"The visible control must own the actual pointer hit: "+name);
            var data=new PointerEventData(EventSystem.current){position=point,pressPosition=point,button=PointerEventData.InputButton.Left,pointerPress=target,eligibleForClick=true};
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerUpHandler);
            Assert.That(Art071RaycastTarget(point),Is.SameAs(target));
            ExecuteEvents.Execute(target,data,ExecuteEvents.pointerClickHandler);
            Canvas.ForceUpdateCanvases();
        }
        private void Art071AssertPoolFacesAreHittable()
        {
            var pool=app.Session.State.dice.Where(d=>!app.Session.State.hand.orderedLockedIds.Contains(d.dieId)).ToArray();
            Assert.That(pool,Is.Not.Empty);
            foreach(var die in pool)
            {
                var pointer=FindActive<DiePointer>("Die_"+die.dieId);
                Rect hit=V06ScreenRect((RectTransform)pointer.transform);
                var orientation=app.table.GetFaceOrientation(die.dieId);
                var body=orientation.Find("Body").GetComponent<MeshFilter>();
                Assert.That(body.sharedMesh.vertexCount,Is.GreaterThan(0));
                // Project the imported mesh's local bounds, independently of the UI
                // dimensions. Bounds stay available with production read/write off.
                // This conservative box contains the full rounded ceramic silhouette.
                Bounds bounds=body.sharedMesh.bounds;
                for(int corner=0;corner<8;corner++)
                {
                    Vector3 vertex=bounds.center+Vector3.Scale(bounds.extents,new Vector3((corner&1)==0?-1:1,(corner&2)==0?-1:1,(corner&4)==0?-1:1));
                    Vector2 point=app.table.viewCamera.WorldToScreenPoint(body.transform.TransformPoint(vertex));
                    Assert.That(point.x,Is.InRange(hit.xMin-.5f,hit.xMax+.5f),die.dieId+" physical face escapes horizontal hit bounds");
                    Assert.That(point.y,Is.InRange(hit.yMin-.5f,hit.yMax+.5f),die.dieId+" physical face escapes vertical hit bounds");
                }
                var faces=orientation.GetComponentsInChildren<MeshRenderer>(false).Where(r=>r.enabled&&r.name.StartsWith("Face_F")).ToArray();
                Assert.That(faces,Is.Not.Empty);
                foreach(var face in faces)
                {
                    Vector2 point=app.table.viewCamera.WorldToScreenPoint(face.transform.position);
                    Assert.That(Art071RaycastTarget(point),Is.SameAs(pointer.gameObject),die.dieId+" visible physical face does not hit its real DiePointer");
                }
            }
        }

        [UnityTest]
        public IEnumerator PLAY_ART071_CompactCardHoverKeepsBottomHitAndClickThenRestoresPooledVisuals()
        {
            // Ten real equipped cards choose the shared compact-card path. The former
            // root animation moved its bottom edge away from this exact pointer position.
            StartRun("71073");
            Art07Fixtures.Apply(app, Art07FixtureKind.MaximumPool);
            yield return WaitInteractionLayout07();
            Assert.That(UiKit.ReducedMotion, Is.False);
            var item = app.Session.State.controllers[0];
            var button = Button("Controller_" + item.instanceId);
            var root = (RectTransform)button.transform;
            Assert.That(root.rect.height, Is.LessThan(140), "Must exercise the compact card, not a full card whose scale hides the bottom-edge regression.");
            var rootPosition = root.anchoredPosition;
            var rootScale = root.localScale;
            var rootSize = root.sizeDelta;
            var painted = root.Cast<Transform>().OfType<RectTransform>()
                .Where(t => t.gameObject.activeSelf && t.name != "Focus").ToArray();
            Assert.That(painted.Length, Is.GreaterThan(3));
            var positions = painted.ToDictionary(t => t, t => t.anchoredPosition);
            var scales = painted.ToDictionary(t => t, t => t.localScale);
            var illustration = (RectTransform)root.Find("Icon_" + item.definitionId);
            Assert.That(illustration, Is.Not.Null);
            var point = RectTransformUtility.WorldToScreenPoint(null,
                root.TransformPoint(new Vector3(root.rect.center.x, root.rect.yMin + 2, 0)));
            var pointer = new PointerEventData(EventSystem.current) {
                position = point, pressPosition = point,
                button = PointerEventData.InputButton.Left, eligibleForClick = true
            };
            var hits = new List<RaycastResult>();
            GameObject RaycastCard()
            {
                hits.Clear();
                EventSystem.current.RaycastAll(pointer, hits);
                Assert.That(hits, Is.Not.Empty);
                var target = ExecuteEvents.GetEventHandler<IPointerClickHandler>(hits[0].gameObject);
                Assert.That(target, Is.SameAs(button.gameObject), "The original bottom-edge point must still hit the same real Button through GraphicRaycaster.");
                return target;
            }
            void AssertRootStable()
            {
                Assert.That(Vector2.Distance(root.anchoredPosition, rootPosition), Is.LessThan(.001f), "Hover must not move the input rectangle.");
                Assert.That(Vector3.Distance(root.localScale, rootScale), Is.LessThan(.001f));
                Assert.That(Vector2.Distance(root.sizeDelta, rootSize), Is.LessThan(.001f));
            }
            void AssertPaintRestored()
            {
                foreach (var child in painted)
                {
                    Assert.That(Vector2.Distance(child.anchoredPosition, positions[child]), Is.LessThan(.05f), child.name + " did not return to its authored position.");
                    Assert.That(Vector3.Distance(child.localScale, scales[child]), Is.LessThan(.001f), child.name + " accumulated hover scale.");
                }
            }

            string before = StateJson();
            byte[] disk = File.ReadAllBytes(app.SaveLocation);
            long sequence = app.Session.ActionSequence;
            var targetBefore = RaycastCard();
            ExecuteEvents.Execute(targetBefore, pointer, ExecuteEvents.pointerEnterHandler);
            float entered = Time.unscaledTime;
            int observedFrames = 0;
            do
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                AssertRootStable();
                RaycastCard();
                observedFrames++;
            } while (Time.unscaledTime - entered < .25f || observedFrames < 3);
            Assert.That(Vector2.Distance(illustration.anchoredPosition, positions[illustration]), Is.GreaterThan(2), "A fixed hit area must still have an actual animated illustration.");
            AssertInteractionCheckpoint07(before, disk, sequence);

            // Dispatch through the same raycast-selected EventSystem target used by UI input,
            // never Button.onClick.Invoke. The normal callback must open this instance's details.
            int clicks = 0;
            button.onClick.AddListener(() => clicks++);
            pointer.pointerPress = targetBefore;
            ExecuteEvents.Execute(targetBefore, pointer, ExecuteEvents.pointerDownHandler);
            yield return null;
            var targetAfter = RaycastCard();
            ExecuteEvents.Execute(targetBefore, pointer, ExecuteEvents.pointerUpHandler);
            if (targetAfter == targetBefore)
                ExecuteEvents.Execute(targetAfter, pointer, ExecuteEvents.pointerClickHandler);
            Canvas.ForceUpdateCanvases();
            Assert.That(clicks, Is.EqualTo(1));
            Assert.That(FindActive<RectTransform>("DetailCard").Find("Identity").GetComponent<TextMeshProUGUI>().text,
                Is.EqualTo(item.instanceId));
            AssertInteractionCheckpoint07(before, disk, sequence);
            ExecuteEvents.Execute(targetBefore, pointer, ExecuteEvents.pointerExitHandler);
            yield return new WaitForSecondsRealtime(.45f);
            AssertRootStable();
            AssertPaintRestored();

            Click("CloseDialog");
            // Hover again, then use the production redraw/pool lifecycle while it is lifted.
            RaycastCard();
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return new WaitForSecondsRealtime(.2f);
            Assert.That(Vector2.Distance(illustration.anchoredPosition, positions[illustration]), Is.GreaterThan(2));
            app.Refresh();
            Assert.That(Button("Controller_" + item.instanceId), Is.SameAs(button));
            AssertRootStable();
            AssertPaintRestored();
            AssertInteractionCheckpoint07(before, disk, sequence);
            TestContext.WriteLine("Real compact HUD card, GraphicRaycaster, EventSystem pointer handlers, and normal detail callback exercised; no OS mouse automation claim.");
        }
    }
}
