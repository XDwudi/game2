using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        [UnityTest]
        public IEnumerator PLAY_FirstNewRunSaveFailureOffersRetryAndBlocksBothReplacementEntrances()
        {
            // An empty directory at the isolated file path reliably denies the atomic rename
            // without changing permissions or touching the user's normal save directory.
            Directory.CreateDirectory(app.SaveLocation);
            FindActive<TMP_InputField>("SeedInput").text = "707";
            Click("NewRun");
            var pending = app.Session;
            Assert.That(pending.HasPendingSave, Is.True);
            Assert.That(pending.State, Is.Null);
            Assert.That(app.ViewState, Is.Null);
            Assert.That(Button("RetrySave").interactable, Is.True);
            app.StartNew("999"); // Bypass the scrim to exercise the entry-point guard itself.
            Assert.That(app.Session, Is.SameAs(pending));
            Button("Tutorial").onClick.Invoke();
            Assert.That(app.Session, Is.SameAs(pending));
            Assert.That(pending.HasPendingSave, Is.True);
            Assert.That(pending.State, Is.Null);
            Assert.That(Button("RetrySave").interactable, Is.True);
            Directory.Delete(app.SaveLocation, false); // Remove only this test's empty fault fixture.
            Click("RetrySave");
            Assert.That(app.Session, Is.SameAs(pending));
            Assert.That(pending.HasPendingSave, Is.False);
            Assert.That(pending.State.seed, Is.EqualTo(707u));
            Assert.That(pending.State.actionSequence, Is.EqualTo(1));
            Assert.That(pending.State.phase, Is.EqualTo("StagePreview"));
            var saved = new AtomicSaveStore(app.SaveLocation, app.Config).Load();
            Assert.That(saved, Is.Not.Null);
            Assert.That(JsonUtility.ToJson(saved), Is.EqualTo(StateJson()));
            yield return null;
        }

        [UnityTest]
        public IEnumerator PLAY_ReadOnlyActionLogCannotAbortAnAlreadySavedUiCommand()
        {
            StartRun("708");
            string log = Path.Combine(saveDirectory, "normal-actions.jsonl");
            Assert.That(File.Exists(log), Is.True);
            FileAttributes original = File.GetAttributes(log);
            byte[] contents = File.ReadAllBytes(log);
            try
            {
                File.SetAttributes(log, original | FileAttributes.ReadOnly);
                LogAssert.Expect(LogType.Warning, new Regex("^操作日志写入失败（存档已保存）："));
                Assert.DoesNotThrow(() => Click("StartStage"));
                Assert.That(app.Session.State.phase, Is.EqualTo("HandReady"));
                Assert.That(app.Session.State.actionSequence, Is.EqualTo(2));
                Assert.That(Button("Roll").interactable, Is.True, "UI must refresh after the committed command even if its log failed.");
                Assert.That(app.Session.HasPendingSave, Is.False);
                var saved = new AtomicSaveStore(app.SaveLocation, app.Config).Load();
                Assert.That(saved, Is.Not.Null);
                Assert.That(JsonUtility.ToJson(saved), Is.EqualTo(StateJson()));
                CollectionAssert.AreEqual(contents, File.ReadAllBytes(log));
                yield return null;
            }
            finally { File.SetAttributes(log, original); }
        }

        [UnityTest]
        public IEnumerator PLAY_ShopPurchaseFooterShowsSlotAndDuplicateFailuresBeforeConfirm()
        {
            // Build the shop by legal practice actions, then save an explicit debug boundary
            // fixture with enough money and full slots. The UI must consult core eligibility.
            Click("Tutorial"); Click("StartStage"); Click("Roll"); Click("Skip");
            foreach (string id in app.Session.State.dice.Select(d => d.dieId).ToArray()) ClickDie(id);
            Click("Confirm"); Click("Submit");
            yield return WaitForIdle();
            if(app.Session.CanEndStage)Click("EndStage");
            Click("SettlementContinue");
            var fixture = app.Session.State;
            Assert.That(fixture.isDebug, Is.True);
            fixture.gold = 1000;
            fixture.controllers.Clear();
            var controller = fixture.shop.offers.First(o => o.category == "controller");
            fixture.controllers.Add(BoundaryController(fixture, controller.contentId, controller.controller.enchantmentId));
            fixture.inventory.Clear();
            for (int i = 0; i < fixture.consumableSlots; i++)
                fixture.inventory.Add(new OwnedItem {instanceId = "TEST_FULL_" + i, contentId = "I01"});
            var consumable = fixture.shop.offers.First(o => o.category == "consumable");
            new GameSession(app.Config, new MemorySaveStore(), fixture); // Enforce production invariants before saving fixture.
            new AtomicSaveStore(app.SaveLocation, app.Config).Save(fixture);
            yield return LoadBootstrap();
            Click("ContinueRun");
            string before = StateJson();
            InspectOfferCard(controller.offerId);
            Assert.That(app.Session.PurchaseError(controller.offerId), Is.Not.Null);
            Assert.That(Button("ConfirmPurchase").interactable, Is.False);
            Assert.That(FindActive<TextMeshProUGUI>("PurchaseUnavailable").text, Does.Contain("控制器"));
            Assert.That(StateJson(), Is.EqualTo(before));
            Click("CloseDialog"); Click("Tab1");
            InspectOfferCard(consumable.offerId);
            Assert.That(app.Session.PurchaseError(consumable.offerId), Is.Not.Null);
            Assert.That(Button("ConfirmPurchase").interactable, Is.False);
            Assert.That(FindActive<TextMeshProUGUI>("PurchaseUnavailable").text, Does.Contain("槽位"));
            Assert.That(StateJson(), Is.EqualTo(before), "Inspecting an ineligible offer must not charge, sell or consume it.");
            Click("CloseDialog");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PLAY_RealEnterKeySubmitsOnceWithoutInvokingSelectedPauseButton()
        {
            StartRun("709"); Click("StartStage"); Click("Roll"); Click("Skip");
            ClickDie(app.Session.State.dice[0].dieId); Click("Confirm");
            long sequence = app.Session.State.actionSequence;
            long scoreBefore = app.Session.State.stage.score;
            long score = app.Session.PreviewScore().score;
            EventSystem.current.SetSelectedGameObject(Button("Pause").gameObject);
            Assert.That(EventSystem.current.currentSelectedGameObject.name, Is.EqualTo("Pause"));
            Keyboard previous = Keyboard.current;
            Keyboard injected = null;
            var editorRouting = InputSystem.settings.editorInputBehaviorInPlayMode;
            var backgroundBehavior = InputSystem.settings.backgroundBehavior;
            bool observedDynamicEnter = false;
            Action observeInput = () =>
            {
                if (InputState.currentUpdateType == InputUpdateType.Dynamic && injected != null && injected.enterKey.isPressed)
                    observedDynamicEnter = true;
            };
            try
            {
                // A batch Editor has no focused Game View. Isolate that Editor routing
                // condition while retaining the production Dynamic update and key handlers.
                // These in-memory settings are restored below and are never saved as assets.
                InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
                Assert.That(InputSystem.settings.updateMode, Is.EqualTo(InputSettings.UpdateMode.ProcessEventsInDynamicUpdate));
                injected = InputSystem.AddDevice<Keyboard>("DiceCraftRegressionKeyboard");
                injected.MakeCurrent();
                InputSystem.onAfterUpdate += observeInput;
                // This goes through the actual input update and GameApp.Update; neither Send
                // nor onClick is invoked for Enter. The UI Submit action sees the same event.
                InputSystem.QueueStateEvent(injected, new KeyboardState(Key.Enter));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(injected, new KeyboardState());
                yield return null;
                Assert.That(observedDynamicEnter, Is.True, "The injected Enter must reach a real Dynamic input update before testing product behavior.");
                Assert.That(app.Session.State.actionSequence, Is.EqualTo(sequence + 1), "Enter must execute exactly one rules command.");
                Assert.That(app.Session.State.stage.score, Is.EqualTo(scoreBefore + score));
                Assert.That(app.canvas.GetComponentsInChildren<Transform>(false).Any(t => t.name == "Dialog" && t.gameObject.activeInHierarchy), Is.False,
                    "The stale selected Pause button must not also receive UI Submit.");
                Assert.That(EventSystem.current.sendNavigationEvents, Is.False);
                yield return WaitForIdle();
                Assert.That(app.Session.State.actionSequence, Is.EqualTo(sequence + 1), "A held Enter must not repeat.");
            }
            finally
            {
                InputSystem.onAfterUpdate -= observeInput;
                if (injected != null && injected.added) InputSystem.RemoveDevice(injected);
                InputSystem.settings.editorInputBehaviorInPlayMode = editorRouting;
                InputSystem.settings.backgroundBehavior = backgroundBehavior;
                if (previous != null && previous.added) previous.MakeCurrent();
            }
        }

        private void InspectOfferCard(string offerId)
        {
            var card = FindActive<Transform>("Offer_" + offerId);
            var inspect = card.GetComponentsInChildren<UnityEngine.UI.Button>().Single(b => b.name == "Inspect");
            Assert.That(inspect.interactable, Is.True);
            inspect.onClick.Invoke();
            Canvas.ForceUpdateCanvases();
        }
    }
}
