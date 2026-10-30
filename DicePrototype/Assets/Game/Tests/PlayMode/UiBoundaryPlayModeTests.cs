using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [Serializable] private sealed class TestSaveEnvelope { public string payload; }
        [Serializable] private sealed class TestActionLine { public SessionCommand action; public CommandReceipt receipt; }

        [UnityTest]
        public IEnumerator PLAY_RemoteItemTargetsCancelC12LabelsAndBrokenTargetMatchActualUi()
        {
            foreach (bool breakTarget in new[] {false, true})
            {
                var fixture = NewBoundaryFixture("remote-" + breakTarget);
                fixture.controllers.Add(BoundaryController(fixture, "C12"));
                fixture.inventory.Add(new OwnedItem {instanceId = "TEST_REMOTE", contentId = "I03"});
                fixture.dice[0].isGlass = true;
                fixture.effectRngState = EffectState(rng => (rng.NextIndex(100) < app.Config.glassBreakPercent) == breakTarget);
                yield return LoadBoundaryFixture(fixture);
                Click("ContinueRun"); Click("StartStage");
                string original = StateJson();
                byte[] saved = File.ReadAllBytes(app.SaveLocation);
                var before = app.Session.State;
                OpenRemoteItem();
                Assert.That(Button("RemoteConfirm").interactable, Is.False);
                Click("RemoteDieD001");
                Assert.That(Button("RemoteConfirm").interactable, Is.False);
                Click("RemoteDieD001");
                Assert.That(Button("RemoteConfirm").interactable, Is.False, "Clicking a chosen target must deselect it.");
                ChooseRemoteTargets();
                Click("RemoteDieD003");
                Assert.That(HasActive("ChosenD003"), Is.False, "A third target cannot replace one of the two chosen targets.");
                for (int pip = 1; pip <= 6; pip++)
                    foreach (string id in new[] {"D001", "D002"})
                        Assert.That(Button("RemotePip" + id + "_" + pip).GetComponentInChildren<TextMeshProUGUI>().text,
                            Is.EqualTo(pip % 2 == 1 ? pip + "→" + (pip + 1) : pip.ToString()), "C12 must distinguish chosen current and effective values.");
                Click("RemotePipD001_1"); Click("RemotePipD002_5");
                Assert.That(StateJson(), Is.EqualTo(original));
                Click("CloseDialog");
                var pointer = FindActive<DiePointer>("Die_D003");
                ExecuteEvents.Execute(pointer.gameObject, new PointerEventData(EventSystem.current) {button = PointerEventData.InputButton.Right}, ExecuteEvents.pointerClickHandler);
                Assert.That(HasActive("Dialog"), Is.True);
                Click("CloseDialog");
                Assert.That(StateJson(), Is.EqualTo(original), "Cancel and right-click inspection cannot consume an item or roll.");
                CollectionAssert.AreEqual(saved, File.ReadAllBytes(app.SaveLocation));
                OpenRemoteItem();
                Assert.That(Button("RemoteConfirm").interactable, Is.False, "Cancelled choices cannot leak into a reopened target dialog.");
                ChooseRemoteTargets(); Click("RemotePipD001_1"); Click("RemotePipD002_5");
                Click("RemoteConfirm");
                Assert.That(app.IsBusy, Is.True);
                var after = app.Session.State;
                Assert.That(after.actionSequence, Is.EqualTo(before.actionSequence + 1));
                Assert.That(after.hand.rollsRemaining, Is.EqualTo(before.hand.rollsRemaining - 1));
                Assert.That(after.hand.rollsUsed, Is.EqualTo(1));
                Assert.That(after.inventory.Any(i => i.instanceId == "TEST_REMOTE"), Is.False);
                Assert.That(after.consumedItemIds.Count(i => i == "TEST_REMOTE"), Is.EqualTo(1));
                var command = after.processedReceipts.Last().command;
                Assert.That(command.kind, Is.EqualTo(CommandKind.UseItem));
                CollectionAssert.AreEqual(new[] {"D001", "D002"}, command.dieIds);
                CollectionAssert.AreEqual(new[] {1, 5}, command.targetPips);
                Assert.That(after.hand.currentResults.All(r => r.hasRolled), Is.True, "The item rolls every surviving unlocked die, not only its targets.");
                Assert.That(after.dice.Count, Is.EqualTo(breakTarget ? 4 : 5));
                if (breakTarget)
                {
                    Assert.That(after.dice.Any(d => d.dieId == "D001"), Is.False);
                    Assert.That(HasActive("Die_D001"), Is.False);
                    Assert.That(FindActive<TextMeshProUGUI>("Toast").text, Does.Contain("D001 玻璃损坏"));
                    Assert.That(after.hand.currentResults.Count(r => r.remoteOverride), Is.EqualTo(1), "Broken targets are not replaced or refunded.");
                }
                else Assert.That(after.hand.currentResults.Single(r => r.dieId == "D001").currentPip, Is.EqualTo(1));
                Assert.That(after.hand.currentResults.Single(r => r.dieId == "D002").currentPip, Is.EqualTo(5));
                foreach (var die in after.dice)
                    Assert.That(JsonUtility.ToJson(die), Is.EqualTo(JsonUtility.ToJson(before.dice.Single(d => d.dieId == die.dieId))), "Remote must preserve permanent faces, colors, glass and physical IDs.");
                Click("Skip");
                foreach (var result in after.hand.currentResults.Where(r => r.remoteOverride))
                {
                    var orientation = app.table.GetFaceOrientation(result.dieId);
                    Assert.That(Vector3.Dot(orientation.TransformDirection(DiceTable.Normals[DiceTable.FaceIndex(result.currentFaceId)]), Vector3.up), Is.GreaterThan(.999f));
                    Assert.That(ConnectedMeshComponents(orientation.Find("Face_" + result.currentFaceId).GetComponent<MeshFilter>().sharedMesh), Is.EqualTo(result.currentPip), "The rolled physical face must show current, not C12 effective, pips.");
                    ClickDie(result.dieId);
                }
                Click("Confirm");
                CollectionAssert.AreEqual(breakTarget ? new[] {6} : new[] {2, 6}, app.Session.PreviewScore().effectivePips);
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator PLAY_RealKeyboardTenDiceMappingModalSeedFocusAndHeldKeys()
        {
            var fixture = NewBoundaryFixture("keyboard-ten");
            for (int serial = 6; serial <= 10; serial++)
                fixture.dice.Add(DiceFactory.CreateStandard(app.Config, serial, serial <= 7 ? "permanent" : "stage_temporary", serial <= 7 ? "" : fixture.stage.stageId));
            fixture.nextDieSerial = 11; fixture.controllers.Add(BoundaryController(fixture, "C22"));
            fixture.dice.Single(d => d.dieId == "D005").isGlass = true;
            fixture.effectRngState = EffectState(rng => rng.NextIndex(100) >= app.Config.glassBreakPercent && rng.NextIndex(100) < app.Config.glassBreakPercent);
            yield return LoadBoundaryFixture(fixture);
            using (var keyboard = new RoutedKeyboard())
            {
                string beforeSeedInput = StateJson();
                var seed = FindActive<TMP_InputField>("SeedInput");
                EventSystem.current.SetSelectedGameObject(seed.gameObject); seed.ActivateInputField();
                yield return null;
                Assert.That(seed.isFocused, Is.True);
                foreach (Key key in new[] {Key.Digit1, Key.Digit0, Key.E, Key.Space, Key.Enter})
                    yield return keyboard.Press(key);
                Assert.That(StateJson(), Is.EqualTo(beforeSeedInput), "Typing/focusing the seed must not execute commands on the existing saved Run.");
                Assert.That(app.Session.PendingIds, Is.Empty);
                seed.DeactivateInputField(); EventSystem.current.SetSelectedGameObject(null);
                Click("ContinueRun"); Click("StartStage");
                long sequence = app.Session.State.actionSequence;
                yield return keyboard.Press(Key.Space, 1.05f);
                yield return WaitForIdle();
                Assert.That(app.Session.State.actionSequence, Is.EqualTo(sequence + 1), "A held Space must roll once, including after the animation ends.");
                Assert.That(app.Session.State.dice.Count, Is.EqualTo(10));
                Key[] digits = {Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5, Key.Digit6, Key.Digit7, Key.Digit8, Key.Digit9, Key.Digit0};
                for (int index = 0; index < digits.Length; index++)
                {
                    yield return keyboard.Press(digits[index]);
                    CollectionAssert.AreEqual(new[] {"D" + (index + 1).ToString("D3")}, app.Session.PendingIds, "Digit mapping at index " + index);
                    yield return keyboard.Press(digits[index]);
                    Assert.That(app.Session.PendingIds, Is.Empty);
                }
                yield return keyboard.Press(Key.Digit1); yield return keyboard.Press(Key.E);
                CollectionAssert.AreEqual(new[] {"D001"}, app.Session.State.hand.orderedLockedIds);
                yield return keyboard.Press(Key.Digit1); yield return keyboard.Press(Key.E);
                CollectionAssert.AreEqual(new[] {"D001", "D002"}, app.Session.State.hand.orderedLockedIds, "Digits must follow the visible pool after a die is locked.");
                yield return keyboard.Press(Key.Space);
                if (app.IsBusy) Click("Skip");
                yield return WaitForIdle();
                Assert.That(app.Session.State.dice.Any(d => d.dieId == "D005"), Is.False);
                yield return keyboard.Press(Key.Digit0);
                Assert.That(app.Session.PendingIds, Is.Empty, "An index beyond the surviving pool must be ignored.");
                yield return keyboard.Press(Key.Digit3);
                CollectionAssert.AreEqual(new[] {"D006"}, app.Session.PendingIds, "After locks and breakage, the third visible die is D006.");
                string beforeModal = StateJson(); string[] pending = app.Session.PendingIds;
                Click("Pause");
                foreach (Key key in digits.Concat(new[] {Key.E, Key.Space, Key.Enter}))
                    yield return keyboard.Press(key);
                Assert.That(StateJson(), Is.EqualTo(beforeModal));
                CollectionAssert.AreEqual(pending, app.Session.PendingIds, "Modal input must not toggle pending selections either.");
                Assert.That(HasActive("Dialog"), Is.True);
                yield return keyboard.Press(Key.Escape);
                Assert.That(HasActive("Dialog"), Is.False);
                Assert.That(StateJson(), Is.EqualTo(beforeModal), "Escape closes only the modal.");
                CollectionAssert.AreEqual(pending, app.Session.PendingIds);
                yield return keyboard.Press(Key.E);
                long beforeSubmit = app.Session.State.actionSequence;
                long expected = app.Session.PreviewScore().score;
                yield return keyboard.Press(Key.Enter, .4f);
                yield return WaitForIdle();
                Assert.That(app.Session.State.actionSequence, Is.EqualTo(beforeSubmit + 1));
                Assert.That(app.Session.State.lastSubmission.breakdown.score, Is.EqualTo(expected));
                CollectionAssert.AreEqual(new[] {"D001", "D002", "D006"}, app.Session.State.lastSubmission.snapshot.dice.Select(d => d.dieId));
                Assert.That(keyboard.DynamicUpdatesWithPressedKeys, Is.GreaterThan(0));
            }
        }

        [UnityTest]
        public IEnumerator PLAY_ExistingRunRollAndSubmitSaveFailureRetriesExactCandidateAndPresentsOnce()
        {
            foreach (CommandKind kind in new[] {CommandKind.Roll, CommandKind.Submit})
            {
                var fixture = NewBoundaryFixture("retry-" + kind);
                if (kind == CommandKind.Roll)
                {
                    fixture.dice[0].isGlass = true;
                    fixture.effectRngState = EffectState(rng => rng.NextIndex(100) < app.Config.glassBreakPercent);
                }
                else
                {
                    fixture.controllers.Add(BoundaryController(fixture, "C07"));
                    for (int i = 0; i < 2; i++) foreach (var face in fixture.dice[i].faces) face.permanentPip = i + 1;
                }
                yield return LoadBoundaryFixture(fixture);
                Click("ContinueRun"); Click("StartStage");
                if (kind == CommandKind.Submit) { Click("Roll"); Click("Skip"); ClickDie("D001"); ClickDie("D002"); Click("Confirm"); }
                string before = StateJson(); byte[] diskBefore = File.ReadAllBytes(app.SaveLocation);
                string log = Path.Combine(saveDirectory, "debug-actions.jsonl");
                int loggedBefore = File.ReadAllLines(log).Length;
                RunState candidate;
                using (var locked = new FileStream(app.SaveLocation, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    Click(kind == CommandKind.Roll ? "Roll" : "Submit");
                    Assert.That(app.Session.HasPendingSave, Is.True);
                    Assert.That(app.IsBusy, Is.False, "Presentation starts only after a durable commit.");
                    Assert.That(StateJson(), Is.EqualTo(before));
                    Assert.That(JsonUtility.ToJson(app.ViewState), Is.EqualTo(before), "The published UI must retain the old committed state.");
                    CollectionAssert.AreEqual(diskBefore, File.ReadAllBytes(app.SaveLocation));
                    Assert.That(File.ReadAllLines(log).Length, Is.EqualTo(loggedBefore));
                    candidate = JsonUtility.FromJson<RunState>(JsonUtility.FromJson<TestSaveEnvelope>(File.ReadAllText(app.SaveLocation + ".tmp")).payload);
                    string frozen = JsonUtility.ToJson(candidate);
                    Click("CloseDialog");
                    Button("Roll").onClick.Invoke(); // A subsequent command cannot replace the pending candidate.
                    Assert.That(Button("RetrySave").interactable, Is.True);
                    Click("RetrySave"); // Retry while the same fault remains.
                    Assert.That(app.Session.HasPendingSave, Is.True);
                    Assert.That(StateJson(), Is.EqualTo(before));
                    Assert.That(JsonUtility.ToJson(JsonUtility.FromJson<RunState>(JsonUtility.FromJson<TestSaveEnvelope>(File.ReadAllText(app.SaveLocation + ".tmp")).payload)), Is.EqualTo(frozen));
                    Assert.That(File.ReadAllLines(log).Length, Is.EqualTo(loggedBefore));
                }
                var retryEvent = Button("RetrySave").onClick;
                Click("RetrySave");
                Assert.That(app.Session.HasPendingSave, Is.False);
                Assert.That(app.IsBusy, Is.True, "Retry must use the same presentation path as an ordinary successful command.");
                Assert.That(StateJson(), Is.EqualTo(JsonUtility.ToJson(candidate)));
                Assert.That(JsonUtility.ToJson(new AtomicSaveStore(app.SaveLocation, app.Config).Load()), Is.EqualTo(StateJson()));
                string expectedToast = kind == CommandKind.Roll ? "D001 玻璃损坏" : "D001 的六面复制到 D002";
                Assert.That(FindActive<TextMeshProUGUI>("Toast").text, Does.Contain(expectedToast));
                if (kind == CommandKind.Roll) Assert.That(HasActive("Die_D001"), Is.False);
                else Assert.That(app.Session.State.dice.Single(d => d.dieId == "D002").dataRevision, Is.EqualTo(1));
                var actionLines = File.ReadAllLines(log).Select(JsonUtility.FromJson<TestActionLine>).ToArray();
                Assert.That(actionLines.Length, Is.EqualTo(loggedBefore + 1));
                Assert.That(actionLines.Last().action.kind, Is.EqualTo(kind));
                Assert.That(actionLines.Last().action.commandId, Is.EqualTo(candidate.processedReceipts.Last().commandId));
                Assert.That(JsonUtility.ToJson(actionLines.Last().receipt), Is.EqualTo(JsonUtility.ToJson(candidate.processedReceipts.Last())));
                string committed = StateJson();
                retryEvent.Invoke(); // A queued second click cannot replay the committed candidate.
                Assert.That(StateJson(), Is.EqualTo(committed));
                Assert.That(File.ReadAllLines(log).Length, Is.EqualTo(loggedBefore + 1));
                Click("Skip");
                yield return WaitForIdle();
                Assert.That(StateJson(), Is.EqualTo(committed));
            }
        }

        private RunState NewBoundaryFixture(string id)
        {
            var session = new GameSession(app.Config, new MemorySaveStore());
            var result = session.Execute(new SessionCommand {kind = CommandKind.NewRun, commandId = id, seed = 20261028});
            Assert.That(result.accepted, Is.True, result.error);
            var fixture = session.State; fixture.isDebug = true; return fixture;
        }
        private IEnumerator LoadBoundaryFixture(RunState fixture)
        {
            new GameSession(app.Config, new MemorySaveStore(), fixture);
            new AtomicSaveStore(app.SaveLocation, app.Config).Save(fixture);
            yield return LoadBootstrap();
            Assert.That(app.Session.State.isDebug, Is.True);
        }
        private uint EffectState(Func<GameRng, bool> accept)
        {
            for (uint candidate = 1; candidate < 10000; candidate++) if (accept(new GameRng(candidate))) return candidate;
            Assert.Fail("No deterministic effect stream fixture found."); return 0;
        }
        private bool HasActive(string name) => app.canvas.GetComponentsInChildren<Transform>(false).Any(t => t.name == name && t.gameObject.activeInHierarchy);
        private void OpenRemoteItem() { Click("Item_TEST_REMOTE"); Click("Use"); }
        private void ChooseRemoteTargets() { Click("RemoteDieD001"); Click("RemoteDieD002"); Assert.That(Button("RemoteConfirm").interactable, Is.True); }

        private sealed class RoutedKeyboard : IDisposable
        {
            private readonly Keyboard previous;
            private readonly InputSettings.EditorInputBehaviorInPlayMode editorRouting;
            private readonly InputSettings.BackgroundBehavior background;
            private readonly Keyboard keyboard;
            public int DynamicUpdatesWithPressedKeys { get; private set; }
            public RoutedKeyboard()
            {
                previous = Keyboard.current;
                editorRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
                background = InputSystem.settings.backgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                keyboard = InputSystem.AddDevice<Keyboard>("DiceCraftBoundaryKeyboard"); keyboard.MakeCurrent();
                InputSystem.onAfterUpdate += Observe;
            }
            private void Observe()
            {
                if (InputState.currentUpdateType == InputUpdateType.Dynamic && keyboard.anyKey.isPressed) DynamicUpdatesWithPressedKeys++;
            }
            public IEnumerator Press(Key key, float holdSeconds = 0)
            {
                int before = DynamicUpdatesWithPressedKeys;
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                yield return null; yield return null;
                if (holdSeconds > 0) yield return new WaitForSecondsRealtime(holdSeconds);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState());
                yield return null;
                Assert.That(DynamicUpdatesWithPressedKeys, Is.GreaterThan(before), key + " must reach actual Dynamic input updates.");
            }
            public void Dispose()
            {
                InputSystem.onAfterUpdate -= Observe;
                if (keyboard.added) InputSystem.RemoveDevice(keyboard);
                InputSystem.settings.editorInputBehaviorInPlayMode = editorRouting;
                InputSystem.settings.backgroundBehavior = background;
                if (previous != null && previous.added) previous.MakeCurrent();
            }
        }
    }
}
