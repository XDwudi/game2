using System;
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
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        // Explicit debug checkpoint. The real core LeaveShop transition prepares a legal
        // stage-two preview; this fixture is not evidence of normal player progression.
        private IEnumerator LoadChallengePreview07()
        {
            var shop=ShopBoundaryFixture();
            var session=new GameSession(app.Config,new MemorySaveStore(),shop);
            CoreShopStep(session,CommandKind.LeaveShop);
            var fixture=session.State;
            Assert.That(fixture.phase,Is.EqualTo("StagePreview"));
            Assert.That(fixture.isDebug&&fixture.debugWorkspace,Is.True);
            Assert.That(app.Config.stages[fixture.stage.stageIndex].riskEnabled,Is.True);
            Assert.That(fixture.stage.risk,Is.False);
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();
            yield return WaitInteractionLayout07();
        }

        private static int InteractionLogLines07(string path)=>File.Exists(path)?File.ReadAllLines(path).Length:0;
        private string InteractionButtonText07(string name)=>Button(name).GetComponentInChildren<TextMeshProUGUI>().text;
        private void AssertInteractionCheckpoint07(string json,byte[] save,long sequence)
        {
            Assert.That(StateJson(),Is.EqualTo(json));
            Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
            CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
        }

        [UnityTest] public IEnumerator PLAY_ART07_ChallengeSelectionIsUiOnlyAndStartCommitsOnlyNecessaryCommands()
        {
            foreach(bool risk in new[]{false,true})
            {
                yield return LoadChallengePreview07();
                string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;
                string log=Path.Combine(saveDirectory,"debug-actions.jsonl");int logged=InteractionLogLines07(log);
                foreach(string choice in new[]{"Risk","Normal","Risk","Normal",risk?"Risk":"Normal"})
                {
                    Click(choice);yield return null;
                    Assert.That(InteractionButtonText07(choice),Is.EqualTo("✓ 已选择"));
                    Assert.That(InteractionButtonText07(choice=="Risk"?"Normal":"Risk"),Is.EqualTo("选择挑战"));
                    AssertInteractionCheckpoint07(before,disk,seq);
                    Assert.That(InteractionLogLines07(log),Is.EqualTo(logged));
                    Assert.That(app.Session.State.stage.started,Is.False);
                }
                Click("StartStage");app.SkipVisual();
                var state=app.Session.State;
                Assert.That(state.phase,Is.EqualTo("HandReady"));Assert.That(state.stage.started,Is.True);Assert.That(state.stage.risk,Is.EqualTo(risk));
                var commands=state.processedReceipts.Where(r=>r.actionSequence>seq).Select(r=>r.command.kind).ToArray();
                CollectionAssert.AreEqual(risk?new[]{CommandKind.ChooseChallenge,CommandKind.StartStage}:new[]{CommandKind.StartStage},commands);
                Assert.That(state.actionSequence,Is.EqualTo(seq+commands.Length));Assert.That(InteractionLogLines07(log),Is.EqualTo(logged+commands.Length));
                var def=app.Config.stages[state.stage.stageIndex];Assert.That(state.stage.target,Is.EqualTo(risk?def.riskTarget:def.normalTarget));
                Assert.That(JsonUtility.ToJson(new AtomicSaveStore(app.SaveLocation,app.Config,SaveSlotPolicy.Debug).Load()),Is.EqualTo(StateJson()));
            }
        }

        [UnityTest] public IEnumerator PLAY_ART07_ChallengeStartSaveFaultKeepsSingleCandidateAndStopsBeforeNextCommand()
        {
            foreach(bool risk in new[]{true,false})
            {
                yield return LoadChallengePreview07();Click(risk?"Risk":"Normal");
                string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;
                string log=Path.Combine(saveDirectory,"debug-actions.jsonl");int logged=InteractionLogLines07(log);
                RunState candidate;string frozenCandidate;
                using(var locked=new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    Click("StartStage");
                    Assert.That(app.Session.HasPendingSave,Is.True,"This case requires a real AtomicSaveStore replace failure, not a mocked result.");
                    Assert.That(app.IsBusy,Is.False);AssertInteractionCheckpoint07(before,disk,seq);
                    Assert.That(JsonUtility.ToJson(app.ViewState),Is.EqualTo(before));Assert.That(InteractionLogLines07(log),Is.EqualTo(logged));
                    candidate=JsonUtility.FromJson<RunState>(JsonUtility.FromJson<TestSaveEnvelope>(File.ReadAllText(app.SaveLocation+".tmp")).payload);
                    frozenCandidate=JsonUtility.ToJson(candidate);
                    Assert.That(candidate.actionSequence,Is.EqualTo(seq+1));
                    Assert.That(candidate.processedReceipts.Last().command.kind,Is.EqualTo(risk?CommandKind.ChooseChallenge:CommandKind.StartStage));
                    Assert.That(candidate.phase,Is.EqualTo(risk?"StagePreview":"HandReady"));Assert.That(candidate.stage.started,Is.EqualTo(!risk));
                    // A second start attempt and failed retry must preserve the exact frozen candidate.
                    Click("CloseDialog");Click("StartStage");Click("RetrySave");
                    Assert.That(app.Session.HasPendingSave,Is.True);AssertInteractionCheckpoint07(before,disk,seq);
                    Assert.That(JsonUtility.ToJson(JsonUtility.FromJson<RunState>(JsonUtility.FromJson<TestSaveEnvelope>(File.ReadAllText(app.SaveLocation+".tmp")).payload)),Is.EqualTo(frozenCandidate));
                    Assert.That(InteractionLogLines07(log),Is.EqualTo(logged));
                }
                var duplicateRetry=Button("RetrySave").onClick;Click("RetrySave");app.SkipVisual();
                Assert.That(app.Session.HasPendingSave,Is.False);Assert.That(StateJson(),Is.EqualTo(frozenCandidate));
                Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));Assert.That(InteractionLogLines07(log),Is.EqualTo(logged+1));
                string committed=StateJson();duplicateRetry.Invoke();Assert.That(StateJson(),Is.EqualTo(committed));
                if(risk)
                {
                    Assert.That(app.Session.State.phase,Is.EqualTo("StagePreview"),"Saving ChooseChallenge must not silently execute a new StartStage.");
                    Assert.That(InteractionButtonText07("Risk"),Is.EqualTo("✓ 已选择"));
                    Click("StartStage");app.SkipVisual();
                    CollectionAssert.AreEqual(new[]{CommandKind.ChooseChallenge,CommandKind.StartStage},app.Session.State.processedReceipts.Where(r=>r.actionSequence>seq).Select(r=>r.command.kind));
                    Assert.That(InteractionLogLines07(log),Is.EqualTo(logged+2));
                }
                Assert.That(app.Session.State.phase,Is.EqualTo("HandReady"));Assert.That(app.Session.State.stage.risk,Is.EqualTo(risk));
                Assert.That(JsonUtility.ToJson(new AtomicSaveStore(app.SaveLocation,app.Config,SaveSlotPolicy.Debug).Load()),Is.EqualTo(StateJson()));
            }
        }

        [UnityTest] public IEnumerator PLAY_ART07_DropOutsideSlotsCancelsEvenWhenNearestDieIsWithinOldRadius()
        {
            StartRun("70803");Art07Fixtures.Apply(app,Art07FixtureKind.GoldenSample);yield return WaitInteractionLayout07();
            var ids=app.Session.State.hand.orderedLockedIds.ToArray();string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;
            var data=BeginDieInteraction07(ids[0],out var pointer);var target=FindActive<RectTransform>("Die_"+ids[2]);
            Vector2 outside=InteractionCenter07(target)+new Vector2(0,134*app.canvas.scaleFactor);
            MoveDieInteraction07(pointer,data,outside);Assert.That(HasActive("DieInsertion"),Is.False);
            ExecuteEvents.Execute(pointer.gameObject,data,ExecuteEvents.endDragHandler);yield return WaitInteractionLayout07();
            AssertInteractionCheckpoint07(before,disk,seq);Assert.That(HasActive("DragDieOrigin07"),Is.False);
        }

        private IEnumerator WaitInteractionLayout07()
        {
            double deadline=Time.realtimeSinceStartupAsDouble+3;
            while((app.IsBusy||app.table.IsTransitioning)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(app.IsBusy||app.table.IsTransitioning,Is.False,"Dice layout must reach its stable pose before pointer geometry assertions.");
            Canvas.ForceUpdateCanvases();yield return null;
        }
        private Vector2 InteractionCenter07(RectTransform rect)=>RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        private Transform InteractionDieRoot07(string id)=>app.table.GetFaceOrientation(id).parent.parent;
        private PointerEventData BeginDieInteraction07(string id,out DiePointer pointer)
        {
            pointer=FindActive<DiePointer>("Die_"+id);var origin=InteractionCenter07((RectTransform)pointer.transform);
            var data=new PointerEventData(EventSystem.current){position=origin,pressPosition=origin,button=PointerEventData.InputButton.Left,dragging=true,pointerDrag=pointer.gameObject,eligibleForClick=false};
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(data,hits);
            Assert.That(hits.Count,Is.GreaterThan(0));Assert.That(hits[0].gameObject==pointer.gameObject||hits[0].gameObject.transform.IsChildOf(pointer.transform),Is.True,"Real die hit region must be unobstructed.");
            ExecuteEvents.Execute(pointer.gameObject,data,ExecuteEvents.beginDragHandler);return data;
        }
        private void MoveDieInteraction07(DiePointer pointer,PointerEventData data,Vector2 position)
        {
            data.delta=position-data.position;data.position=position;ExecuteEvents.Execute(pointer.gameObject,data,ExecuteEvents.dragHandler);Canvas.ForceUpdateCanvases();
        }
        private void AssertDieFollow07(string id,Vector2 position,Vector2 origin)
        {
            Vector2 projected=app.table.viewCamera.WorldToScreenPoint(InteractionDieRoot07(id).position);
            Assert.That(Vector2.Distance(projected,position),Is.LessThan(1f),"The real 3D die root must follow the actual pointer coordinates.");
            var placeholder=FindActive<RectTransform>("DragDieOrigin07");Assert.That(Vector2.Distance(InteractionCenter07(placeholder),origin),Is.LessThan(1f));
        }

        [UnityTest] public IEnumerator PLAY_ART07_LockedDieDragFollowsPointerAndEscapeOrFocusCallbackRestoresCheckpoint()
        {
            StartRun("70801");Art07Fixtures.Apply(app,Art07FixtureKind.GoldenSample);yield return WaitInteractionLayout07();
            string id=app.Session.State.hand.orderedLockedIds[0];
            foreach(bool escape in new[]{true,false})
            {
                string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;
                Vector3 resting=InteractionDieRoot07(id).position,scale=InteractionDieRoot07(id).localScale;
                var data=BeginDieInteraction07(id,out var pointer);Vector2 origin=data.position;
                MoveDieInteraction07(pointer,data,origin+new Vector2(120,160));AssertDieFollow07(id,data.position,origin);Assert.That(HasActive("DieInsertion"),Is.False,"Outside the submission slots there is no legal insertion preview.");AssertInteractionCheckpoint07(before,disk,seq);
                MoveDieInteraction07(pointer,data,origin+new Vector2(180,200));AssertDieFollow07(id,data.position,origin);Assert.That(HasActive("DieInsertion"),Is.False);AssertInteractionCheckpoint07(before,disk,seq);
                if(escape){using(var keyboard=new RoutedKeyboard())yield return keyboard.Press(Key.Escape);}
                else
                {
                    // Unity message invokes the real lifecycle handler. This is callback
                    // integration coverage, not a claim of OS foreground-switch testing.
                    app.gameObject.SendMessage("OnApplicationFocus",false,SendMessageOptions.RequireReceiver);yield return null;
                }
                yield return WaitInteractionLayout07();
                Assert.That(HasActive("DragDieOrigin07")||HasActive("DieInsertion"),Is.False);Assert.That(HasActive("Dialog"),Is.False,"Esc cancels dragging instead of opening Pause.");
                Assert.That(Vector3.Distance(InteractionDieRoot07(id).position,resting),Is.LessThan(.001f));
                Assert.That(Vector3.Distance(InteractionDieRoot07(id).localScale,scale),Is.LessThan(.001f));
                AssertInteractionCheckpoint07(before,disk,seq);
                // A late pointer-up after cancellation cannot turn into a reorder.
                ExecuteEvents.Execute(pointer.gameObject,data,ExecuteEvents.endDragHandler);AssertInteractionCheckpoint07(before,disk,seq);
                if(!escape)app.gameObject.SendMessage("OnApplicationFocus",true,SendMessageOptions.RequireReceiver);
            }
        }

        [UnityTest] public IEnumerator PLAY_ART07_LockedDieDropCommitsExactlyOneReorderWithoutUnlocking()
        {
            StartRun("70802");Art07Fixtures.Apply(app,Art07FixtureKind.GoldenSample);yield return WaitInteractionLayout07();
            var before=app.Session.State;string[] ids=before.hand.orderedLockedIds.ToArray();Assert.That(ids.Length,Is.GreaterThanOrEqualTo(3));
            long seq=before.actionSequence;string log=Path.Combine(saveDirectory,"debug-actions.jsonl");int logged=InteractionLogLines07(log);
            string json=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            var data=BeginDieInteraction07(ids[0],out var pointer);Vector2 origin=data.position;
            // The die occupies the slot's left side after the label reflow. Explicitly
            // drop in the third slot's right half to request insertion AFTER that slot.
            var targetSlot=FindActive<RectTransform>("Slot2");
            Vector2 target=RectTransformUtility.WorldToScreenPoint(null,targetSlot.TransformPoint(targetSlot.rect.center+Vector2.right*targetSlot.rect.width*.25f));
            MoveDieInteraction07(pointer,data,target);AssertDieFollow07(ids[0],target,origin);Assert.That(HasActive("DieInsertion"),Is.True);AssertInteractionCheckpoint07(json,disk,seq);
            ExecuteEvents.Execute(pointer.gameObject,data,ExecuteEvents.endDragHandler);yield return WaitInteractionLayout07();
            var after=app.Session.State;string[] expected=ids.Skip(1).Take(2).Concat(new[]{ids[0]}).Concat(ids.Skip(3)).ToArray();
            CollectionAssert.AreEqual(expected,after.hand.orderedLockedIds);CollectionAssert.AreEquivalent(ids,after.hand.orderedLockedIds);
            Assert.That(after.actionSequence,Is.EqualTo(seq+1));var receipts=after.processedReceipts.Where(r=>r.actionSequence>seq).ToArray();Assert.That(receipts.Length,Is.EqualTo(1));
            Assert.That(receipts[0].command.kind,Is.EqualTo(CommandKind.Reorder));CollectionAssert.AreEqual(expected,receipts[0].command.dieIds);
            Assert.That(InteractionLogLines07(log),Is.EqualTo(logged+1));Assert.That(app.Session.PendingIds,Is.Empty);
            CollectionAssert.AreEqual(before.hand.currentResults.Select(JsonUtility.ToJson),after.hand.currentResults.Select(JsonUtility.ToJson));
            CollectionAssert.AreEqual(before.dice.Select(JsonUtility.ToJson),after.dice.Select(JsonUtility.ToJson));
            Assert.That(after.hand.currentResults.Where(r=>ids.Contains(r.dieId)).All(r=>r.isLocked&&r.hasRolled),Is.True);
            Assert.That(after.hand.rollsRemaining,Is.EqualTo(before.hand.rollsRemaining));Assert.That(after.stage.remainingSubmissions,Is.EqualTo(before.stage.remainingSubmissions));
            Assert.That(after.diceRngState,Is.EqualTo(before.diceRngState));Assert.That(after.effectRngState,Is.EqualTo(before.effectRngState));Assert.That(after.shopRngState,Is.EqualTo(before.shopRngState));
            Assert.That(HasActive("DragDieOrigin07")||HasActive("DieInsertion"),Is.False);
            string committed=StateJson();byte[] saved=File.ReadAllBytes(app.SaveLocation);
            ExecuteEvents.Execute(pointer.gameObject,data,ExecuteEvents.endDragHandler);AssertInteractionCheckpoint07(committed,saved,seq+1);
            Assert.That(InteractionLogLines07(log),Is.EqualTo(logged+1));
            Assert.That(JsonUtility.ToJson(new AtomicSaveStore(app.SaveLocation,app.Config,SaveSlotPolicy.Debug).Load()),Is.EqualTo(committed));
        }
    }
}
