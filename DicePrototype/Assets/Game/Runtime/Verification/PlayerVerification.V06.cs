using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace DiceCraft.Verification
{
    public sealed partial class PlayerVerification
    {
        private readonly List<Dictionary<string, object>> v06Checks = new List<Dictionary<string, object>>();
        private readonly List<Dictionary<string, object>> displayChecks = new List<Dictionary<string, object>>();
        private readonly List<Dictionary<string, object>> inputDiagnostics = new List<Dictionary<string, object>>();
        private readonly List<Dictionary<string, object>> resolutionDiagnostics = new List<Dictionary<string, object>>();
        private string debugTraceHash;
        [Serializable] private sealed class DebugTrace
        {
            public string configVersion, expectedFinalPhase;
            public bool debug;
            public int stages, shops, risks;
            public RunState initialWorkspace;
            public DebugTraceStep[] steps;
        }
        [Serializable] private sealed class DebugTraceStep
        {
            public SessionCommand command;
            public long actionSequence, gold;
            public string phase, stageScore, target, submissionScore, stateHash;
            public int stageIndex;
            public uint diceRngState, shopRngState, effectRngState;
        }
        [Serializable] private sealed class RecoveryReceipt
        {
            public string schema = "p06-native-display-recovery-1", buildIdentity, settingsPath, pendingSha256, status, evidenceDirectory;
            public int writerProcessId, readerProcessId;
            public DisplayChoice confirmed, candidate, actual;
        }
        private void V06Check(string id, string method, object actual)
        {
            v06Checks.Add(new Dictionary<string, object> { ["id"] = id, ["status"] = "passed", ["method"] = method, ["actual"] = actual });
            File.WriteAllText(Path.Combine(configuredDirectory, "v06-checks.json"), Json(v06Checks), Encoding.UTF8);
        }
        // These adapters dispatch real uGUI events to active controls. No rules state is
        // changed by reflection; reflection only accesses existing GameApp entry points.
        private T ActiveControl<T>(string name) where T : Component
        {
            var found = app.canvas.GetComponentsInChildren<T>(false).Where(x => x.gameObject.activeInHierarchy && x.name == name).ToArray();
            Require(found.Length == 1, "Expected one active " + typeof(T).Name + " named " + name + ", found " + found.Length);
            return found[0];
        }
        private Vector2 VisiblePoint(Component control)
        {
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)control.transform;
            var scroll = control.GetComponentInParent<ScrollRect>();
            if (scroll != null && scroll.viewport != null && scroll.content != null)
            {
                scroll.StopMovement();
                var bounds = RectTransformUtility.CalculateRelativeRectTransformBounds(scroll.viewport, rect);
                float delta = scroll.viewport.rect.center.y - bounds.center.y;
                var position = scroll.content.anchoredPosition;
                position.y = Mathf.Clamp(position.y + delta, 0, Mathf.Max(0, scroll.content.rect.height - scroll.viewport.rect.height));
                scroll.content.anchoredPosition = position;
                Canvas.ForceUpdateCanvases();
            }
            var center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
            Require(center.x >= 0 && center.x <= Screen.width && center.y >= 0 && center.y <= Screen.height, "UI control is outside actual window: " + control.name);
            if (scroll != null && scroll.viewport != null)
                Require(RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, center, null), "UI control remains clipped: " + control.name);
            return center;
        }
        private void Press(string name)
        {
            var b = ActiveControl<Button>(name);
            Require(b.IsInteractable(), "UI button disabled: " + name);
            var e = new PointerEventData(EventSystem.current) { position = VisiblePoint(b), button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(b.gameObject, e, ExecuteEvents.pointerClickHandler);
        }
        private void Input(string name, string value, bool submit = false)
        {
            var field = ActiveControl<TMP_InputField>(name);
            Require(field.IsInteractable(), "UI field disabled: " + name);
            VisiblePoint(field);
            field.text = value; // invokes the production onValueChanged validator
            if (submit) field.onSubmit.Invoke(value);
        }
        private DebugScene Draft => (DebugScene)GetPrivate(app, "debugDraft");
        private IEnumerator WheelToControl(string name)
        {
            var target = ActiveControl<Button>(name);
            var scroll = target.GetComponentInParent<ScrollRect>();
            Require(scroll != null && scroll.viewport != null, "Wheel target is not in a scroll view.");
            int events = 0;
            while (events < 100)
            {
                Canvas.ForceUpdateCanvases();
                var rect = (RectTransform)target.transform;
                var center = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center));
                if (RectTransformUtility.RectangleContainsScreenPoint(scroll.viewport, center, null)) break;
                var viewportCenter = RectTransformUtility.WorldToScreenPoint(null, scroll.viewport.TransformPoint(scroll.viewport.rect.center));
                var e = new PointerEventData(EventSystem.current) { position = viewportCenter, scrollDelta = new Vector2(0, center.y < viewportCenter.y ? -1 : 1) };
                ExecuteEvents.Execute(scroll.gameObject, e, ExecuteEvents.scrollHandler);
                events++; yield return null;
            }
            Require(events > 0 && events < 100, "Wheel could not reach offscreen target " + name);
            V06Check("wheel-reach-" + name, "Actual uGUI ScrollRect.OnScroll events with one notch each; no direct scroll position assignment", new Dictionary<string, object> { ["events"] = events, ["normalizedPosition"] = scroll.verticalNormalizedPosition });
        }
        private IEnumerator EscapeThroughInputSystem(string scenario)
        {
            Require(!sampling, "Synthetic input routing must stay outside the pressure sample.");
            var previous = Keyboard.current;
            var background = InputSystem.settings.backgroundBehavior;
            var updates = InputSystem.settings.updateMode;
            string layout = "DiceCraftProbeKeyboard" + Guid.NewGuid().ToString("N");
            Keyboard injected = null;
            int dynamicPressed = 0, dynamicEdges = 0, currentEdges = 0;
            string stateBefore = GameSession.StateHash(app.Session.State);
            string savePath = app.SaveLocation;
            string diskBefore = File.Exists(savePath) ? FileHash(savePath) : null;
            var samples = new List<Dictionary<string, object>>();
            var diagnostic = new Dictionary<string, object> { ["scenario"] = scenario,
                ["method"] = "Probe-owned background-capable Keyboard layout; queued event consumed by normal Dynamic input and GameApp.Update. Not physical keyboard/focus evidence.",
                ["focusedBefore"] = Application.isFocused, ["backgroundBehavior"] = background.ToString(), ["updateMode"] = updates.ToString(),
                ["previousDeviceId"] = previous?.deviceId, ["previousDeviceEnabled"] = previous?.enabled,
                ["dragBefore"] = app.IsControllerDragging, ["displayPendingBefore"] = app.DisplayPreferences.IsPending,
                ["stateHashBefore"] = stateBefore, ["diskHashBefore"] = diskBefore, ["samples"] = samples };
            Action observe = () =>
            {
                if (injected == null || InputState.currentUpdateType != InputUpdateType.Dynamic) return;
                bool edge = injected.escapeKey.wasPressedThisFrame;
                if (injected.escapeKey.isPressed) dynamicPressed++;
                if (edge)
                {
                    dynamicEdges++;
                    // Select only our synthetic keyboard for this routed test edge.
                    // This runs before ordinary MonoBehaviour.Update, not instead of it.
                    injected.MakeCurrent();
                    if (Keyboard.current == injected) currentEdges++;
                }
                if (samples.Count < 12) samples.Add(new Dictionary<string, object> {
                    ["frame"] = Time.frameCount, ["focused"] = Application.isFocused,
                    ["deviceId"] = injected.deviceId, ["enabled"] = injected.enabled, ["canRunInBackground"] = injected.canRunInBackground,
                    ["currentDeviceId"] = Keyboard.current?.deviceId, ["isCurrent"] = Keyboard.current == injected,
                    ["escapePressed"] = injected.escapeKey.isPressed, ["escapePressedThisFrame"] = edge,
                    ["dragAtInputUpdate"] = app.IsControllerDragging, ["displayPendingAtInputUpdate"] = app.DisplayPreferences.IsPending });
            };
            try
            {
                Require(updates == InputSettings.UpdateMode.ProcessEventsInDynamicUpdate, "Player input must use normal Dynamic updates.");
                // Do not modify global IgnoreFocus, editor routing, or physical devices.
                InputSystem.RegisterLayout("{\"name\":\"" + layout + "\",\"extend\":\"Keyboard\",\"runInBackground\":\"enabled\"}");
                injected = (Keyboard)InputSystem.AddDevice(layout, "DiceCraftVerificationKeyboard"); injected.MakeCurrent();
                diagnostic["injectedDeviceId"] = injected.deviceId;
                diagnostic["injectedEnabledBefore"] = injected.enabled;
                diagnostic["injectedCanRunInBackground"] = injected.canRunInBackground;
                Require(injected.enabled, "Probe-owned keyboard is disabled; see injectedEnabledBefore and focus diagnostics. Global routing remains unchanged.");
                Require(injected.canRunInBackground, "Probe-owned keyboard does not report background capability; see injectedCanRunInBackground and registered layout. Global routing remains unchanged.");
                InputSystem.onAfterUpdate += observe;
                InputSystem.QueueStateEvent(injected, new KeyboardState(Key.Escape));
                yield return null; yield return null;
                InputSystem.QueueStateEvent(injected, new KeyboardState());
                yield return null;
            }
            finally
            {
                InputSystem.onAfterUpdate -= observe;
                diagnostic["dynamicPressedUpdates"] = dynamicPressed; diagnostic["dynamicPressedEdges"] = dynamicEdges; diagnostic["currentDeviceEdges"] = currentEdges;
                diagnostic["focusedAfter"] = Application.isFocused; diagnostic["dragAfter"] = app.IsControllerDragging;
                diagnostic["displayPendingAfter"] = app.DisplayPreferences.IsPending;
                diagnostic["stateUnchanged"] = GameSession.StateHash(app.Session.State) == stateBefore;
                diagnostic["diskUnchanged"] = diskBefore == (File.Exists(savePath) ? FileHash(savePath) : null);
                if (injected != null && injected.added) InputSystem.RemoveDevice(injected);
                InputSystem.RemoveLayout(layout);
                if (previous != null && previous.added) previous.MakeCurrent();
                diagnostic["previousDeviceRestored"] = previous == null ? Keyboard.current == null : !previous.added || Keyboard.current == previous;
                diagnostic["globalSettingsUnchanged"] = InputSystem.settings.backgroundBehavior == background && InputSystem.settings.updateMode == updates;
                inputDiagnostics.Add(diagnostic);
                File.WriteAllText(Path.Combine(configuredDirectory, "v06-input-diagnostics.json"), Json(inputDiagnostics), Encoding.UTF8);
            }
            Require(dynamicPressed > 0 && dynamicEdges > 0 && currentEdges > 0, "Synthetic Escape did not reach a current enabled keyboard in the actual Dynamic input update; see v06-input-diagnostics.json.");
            Require(InputSystem.settings.backgroundBehavior == background && InputSystem.settings.updateMode == updates, "Probe changed global input routing settings.");
        }
        private IEnumerator WaitIdle()
        {
            double until = Time.realtimeSinceStartupAsDouble + 12;
            while (app.IsBusy && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Require(!app.IsBusy, "Presentation did not finish within 12 seconds.");
            yield return null;
        }
        private void InstallDebugWorkspace(RunState state, string directory)
        {
            Require(state.debugWorkspace, "Verification fixture must be an isolated debug workspace.");
            var store = new AtomicSaveStore(Path.Combine(configuredDirectory, directory, "debug-run.json"), app.Config, SaveSlotPolicy.Debug);
            var session = new GameSession(app.Config, store, state);
            store.Save(state);
            typeof(GameApp).GetProperty(nameof(GameApp.Session)).GetSetMethod(true).Invoke(app, new object[] { session });
            SetPrivate(app, "debugSaves", store); SetPrivate(app, "inMenu", false); SetPrivate(app, "tutorial", false);
            InvokePrivate("CloseModal"); app.Refresh();
        }
        private IEnumerator VerifyV06UiAndIsolation()
        {
            phase = "V06DebugUi";
            string normal = JsonUtility.ToJson(app.Session.State), disk = FileHash(normalSave);
            InvokePrivate("Pause"); yield return null;
            Input("DebugPassword", "xd is god", true); yield return null;
            Require(!app.DebugUnlocked && JsonUtility.ToJson(app.Session.State) == normal, "Invalid password changed/unlocked ordinary challenge.");
            Input("DebugPassword", "XD is god ", true); yield return null;
            Require(!app.DebugUnlocked, "Trailing whitespace password was accepted.");
            Input("DebugPassword", "XD is god", true); yield return null;
            Require(app.DebugUnlocked && JsonUtility.ToJson(app.Session.State) == normal && FileHash(normalSave) == disk, "Unlock must not mutate ordinary challenge.");
            Press("UnlockDebug"); yield return null;
            Require(app.IsDebugWorkspace && app.SaveLocation != normalSave, "UI did not enter independent debug workspace.");
            V06Check("debug-password-isolation", "TMP input submit + actual uGUI button events", new Dictionary<string, object> { ["wrongCaseRejected"] = true, ["whitespaceRejected"] = true, ["normalCheckpointUnchanged"] = true });

            Press("DebugTab0");
            yield return WheelToControl("AddC53");
            yield return Capture("v06-debug-last-controller-row-wheel");
            while (Draft.controllers.Count > 0)
            {
                Press("DebugOwned" + Draft.controllers[0].instanceId); Press("DebugRemoveController");
            }
            string[] cards = { "C41", "C53", "C44", "C51", "C45", "C52" };
            string[] enchants = { "base10", "mult2", "submit1", "roll1", "slotless", "none" };
            for (int i = 0; i < cards.Length; i++)
            {
                Press("Add" + cards[i]); Press("DebugEnchant" + enchants[i]);
                if (cards[i] == "C51") Input("Turnover", "1000000000");
                if (cards[i] == "C52") Input("Fund", "9");
            }
            yield return Capture("v06-debug-controllers");
            Press("DebugTab1"); Press("FreshHand");
            while (Draft.dice.Count < 32) Press("AddDie");
            Press("BatchPip6"); Press("AllRed"); Press("CurrentFace");
            // Four red sixes, C44 and C51 make the preview exceed Int64 without floating point.
            foreach (string id in Draft.dice.Take(4).Select(x => x.dieId).ToArray())
            { Press("DebugDie" + id); Press("DebugLock"); }
            Press("DebugTab2"); Press("MaxLevels");
            Press("DebugTab3"); Input("Wallet", "1000000000"); Input("Capacity", "32");
            Input("RollsLeft", "3"); Input("SubmitsLeft", "3"); Input("StageScore", "0"); Input("Target", "1000000000000000000000000000000");
            foreach (var item in app.Config.consumables) Press("AddItem" + item.id);
            Press("ApplyDebugScene"); yield return WaitIdle();
            var state = app.Session.State;
            Require(state.isDebug && state.debugWorkspace && state.dice.Count == 32 && state.stage.capacity == 32, "UI debug scene did not apply 32 dice/capacity.");
            Require(state.controllers.Count == cards.Length && state.controllers.Select(c => c.definitionId).SequenceEqual(cards), "UI debug controller combination differs.");
            Require(state.controllers.Select(c => c.enchantmentId).SequenceEqual(enchants), "UI debug enchantments differ.");
            Require(state.handLevels.Concat(state.colorLevels).All(h => h.level == 100), "UI level100 was not committed.");
            Require(state.dice.All(d => d.faces.All(f => f.permanentPip == 6 && f.baseColor == "red")), "UI dice face edits were not committed.");
            Require(app.Config.consumables.All(c => state.inventory.Any(i => i.contentId == c.id)), "UI item editing omitted a consumable.");
            Require(FileHash(normalSave) == disk, "Applying debug scene wrote ordinary checkpoint.");
            File.WriteAllText(Path.Combine(configuredDirectory, "v06-ui-debug-state.json"), JsonUtility.ToJson(state, true), Encoding.UTF8);
            Press("RunDebugScene"); yield return null;
            yield return Capture("v06-debug-32dice");
            string before = JsonUtility.ToJson(app.Session.State), debugDisk = FileHash(app.SaveLocation);
            var preview = app.Session.PreviewScoreRange();
            Require(preview != null && preview.isRandom && preview.randomOwnerIds.Length == 1, "C41/C53 preview did not share one random owner.");
            Require(ExactScoreMath.Parse(preview.minimum.score) > long.MaxValue && ExactScoreMath.Parse(preview.maximum.score) > ExactScoreMath.Parse(preview.minimum.score), "BigInteger random preview range was not exercised.");
            for (int i = 0; i < 20; i++)
            {
                Require(JsonUtility.ToJson(app.Session.PreviewScoreRange()) == JsonUtility.ToJson(preview), "Random preview changed during pure read.");
                Require(JsonUtility.ToJson(app.Session.State) == before && FileHash(app.SaveLocation) == debugDisk, "Preview consumed RNG/sequence or wrote state.");
            }
            InvokePrivate("ShowBreakdown", app.Session.PreviewSnapshot(), preview.minimum);
            yield return Capture("v06-debug-bigint-random-preview"); InvokePrivate("CloseModal");
            V06Check("debug-editor-full-configuration", "Real UI controls; formal ApplyDebugScene transaction; 20 pure preview reads", new Dictionary<string, object> {
                ["dice"] = 32, ["capacity"] = 32, ["controllers"] = cards, ["enchantments"] = enchants, ["allLevels"] = 100,
                ["previewMinimum"] = preview.minimum.score, ["previewMaximum"] = preview.maximum.score, ["sharedRandomOwners"] = preview.randomOwnerIds,
                ["normalCheckpointUnchanged"] = true, ["previewStateAndRngUnchanged"] = true
            });
            yield return VerifyV06Targets32();
            app.ShowDebugPanel(); Press("ReturnNormal"); yield return null;
            Require(JsonUtility.ToJson(app.Session.State) == normal && FileHash(normalSave) == disk && app.SaveLocation == normalSave, "Return to ordinary checkpoint did not preserve exact state.");
        }

        private IEnumerator VerifyV06Targets32()
        {
            phase = "V06Targets32";
            Require(app.Session.State.isDebug && app.Session.State.dice.Count == 32, "Target regression requires the applied 32-die debug workspace.");
            var item = app.Session.State.inventory.Last(i => i.contentId == "I03");
            yield return CaptureResolutionSet("v06-remote-target32", () => InvokePrivate("RemoteTargets", item), true, "RemoteTargetList");
            string before = GameSession.StateHash(app.Session.State), disk = FileHash(app.SaveLocation);
            InvokePrivate("RemoteTargets", item); yield return null;
            yield return WheelToControl("RemoteDieD031"); yield return PressTargetReady("RemoteDieD031"); yield return PressTargetReady("RemoteDieD032");
            yield return PressTargetReady("RemotePipD031_2"); yield return PressTargetReady("RemotePipD032_5");
            Require(ActiveControl<Button>("RemoteConfirm").IsInteractable(), "Remote target32 pair is not confirmable.");
            yield return Capture("v06-remote-D031-D032-selected");
            yield return PressTargetReady("CloseDialog"); yield return null;
            Require(GameSession.StateHash(app.Session.State) == before && FileHash(app.SaveLocation) == disk, "Cancelling remote target selection spent item/roll/RNG or wrote a save.");
            InvokePrivate("RemoteTargets", item); yield return null;
            yield return PressTargetReady("RemoteDieD031"); yield return PressTargetReady("RemoteDieD032"); yield return PressTargetReady("RemotePipD031_2"); yield return PressTargetReady("RemotePipD032_5");
            var prior = app.Session.State;
            yield return PressTargetReady("RemoteConfirm"); yield return null;
            if (app.IsBusy) app.SkipVisual(); yield return WaitIdle();
            var after = app.Session.State;
            Require(after.actionSequence == prior.actionSequence + 1 && after.hand.rollsRemaining == prior.hand.rollsRemaining - 1
                && !after.inventory.Any(i => i.instanceId == item.instanceId), "Remote target32 confirmation did not consume exactly one command/item/roll.");
            var a = after.hand.currentResults.Single(r => r.dieId == "D031"); var b = after.hand.currentResults.Single(r => r.dieId == "D032");
            Require(a.currentPip == 2 && b.currentPip == 5 && a.remoteOverride && b.remoteOverride, "Late-list remote targets received wrong pip overrides.");
            Require(after.hand.orderedLockedIds.SequenceEqual(prior.hand.orderedLockedIds), "Remote confirmation changed locked dice.");
            V06Check("remote-target32-ui", "Real scroll/target/pip/confirm/close UI; production UseItem and animation path", new Dictionary<string, object> {
                ["targets"] = new[] { "D031", "D032" }, ["pips"] = new[] { a.currentPip, b.currentPip }, ["cancelWasPure"] = true,
                ["commandDelta"] = after.actionSequence - prior.actionSequence, ["itemConsumedOnce"] = true, ["rollDelta"] = prior.hand.rollsRemaining - after.hand.rollsRemaining });

            // Obtain a valid settled debug Shop through production commands, then install
            // two explicit test offers in its isolated save. This is a UI fixture, not a
            // claim about natural shop probability or normal progression.
            var scene = app.Session.ExportDebugScene(); scene.target = "1"; scene.score = "1";
            Accept(app.Send(new SessionCommand { kind = CommandKind.ApplyDebugScene, debugScene = scene })); yield return WaitIdle();
            Accept(app.Send(new SessionCommand { kind = CommandKind.EndStage })); yield return WaitIdle();
            Accept(app.Send(new SessionCommand { kind = CommandKind.Continue })); yield return WaitIdle();
            var shop = app.Session.State; Require(shop.phase == "Shop", "Debug target test did not reach a real settled shop.");
            shop.shop.offers.Clear();
            var upgrade = new ShopOffer { offerId = "VERIFY_TARGET32_U06", category = "die_upgrade", contentId = "U06",
                batchId = shop.shop.generationId + "/die_upgrade", basePrice = app.Config.Upgrade("U06").buyPrice };
            var deletion = new ShopOffer { offerId = "VERIFY_TARGET32_O04", category = "service", contentId = "O04",
                batchId = shop.shop.generationId + "/service", basePrice = app.Config.Service("O04").baseBuyPrice };
            shop.shop.offers.Add(upgrade); shop.shop.offers.Add(deletion);
            InstallDebugWorkspace(shop, "debug-target32-shop-save");
            yield return CaptureResolutionSet("v06-shop-target32", () => InvokePrivate("TargetDie", upgrade, null, 1f), true, "ShopTargetList");
            before = GameSession.StateHash(app.Session.State); disk = FileHash(app.SaveLocation);
            InvokePrivate("TargetDie", upgrade, null, 1f); yield return null;
            yield return WheelToControl("Target_D032"); yield return PressTargetReady("Target_D032"); yield return PressTargetReady("Face_F01");
            yield return Capture("v06-upgrade-D032-F01-preview");
            yield return PressTargetReady("CloseDialog"); yield return null;
            Require(GameSession.StateHash(app.Session.State) == before && FileHash(app.SaveLocation) == disk, "Cancelling late-list upgrade selection mutated state/wallet/RNG.");
            InvokePrivate("TargetDie", upgrade, null, 1f); yield return PressTargetReady("Target_D032"); yield return PressTargetReady("Face_F01");
            prior = app.Session.State; yield return PressTargetReady("ConfirmPurchase"); yield return WaitIdle(); after = app.Session.State;
            Require(after.actionSequence == prior.actionSequence + 1 && after.dice.Single(d => d.dieId == "D032").faces.Single(f => f.faceId == "F01").isGold
                && after.shop.offers.Single(o => o.offerId == upgrade.offerId).sold && after.gold < prior.gold, "Late-list face purchase did not commit once on D032/F01.");
            before = GameSession.StateHash(after); disk = FileHash(app.SaveLocation);
            InvokePrivate("TargetDie", deletion, null, 1f); yield return PressTargetReady("Target_D031");
            yield return Capture("v06-delete-D031-preview"); yield return PressTargetReady("CloseDialog"); yield return null;
            Require(GameSession.StateHash(app.Session.State) == before && FileHash(app.SaveLocation) == disk, "Cancelling late-list deletion changed state.");
            InvokePrivate("TargetDie", deletion, null, 1f); yield return PressTargetReady("Target_D031");
            prior = app.Session.State; yield return PressTargetReady("ConfirmPurchase"); yield return WaitIdle(); after = app.Session.State;
            Require(after.actionSequence == prior.actionSequence + 1 && after.dice.Count == 31 && !after.dice.Any(d => d.dieId == "D031")
                && after.dice.Any(d => d.dieId == "D032") && after.shop.offers.Single(o => o.offerId == deletion.offerId).sold,
                "Late-list deletion affected wrong die or failed its single transaction.");
            Require(FileHash(normalSave) == normalSaveHash, "Target32 debug fixtures modified ordinary checkpoint.");
            V06Check("shop-target32-ui", "Three actual resolutions + real target/face/cancel/confirm UI on valid isolated Shop", new Dictionary<string, object> {
                ["upgradeDie"] = "D032", ["upgradeFace"] = "F01", ["deletedDie"] = "D031", ["remainingDice"] = after.dice.Count,
                ["cancelUpgradeWasPure"] = true, ["cancelDeletionWasPure"] = true, ["normalCheckpointUnchanged"] = true });
        }

        private readonly List<Dictionary<string, object>> target32PointerDiagnostics = new List<Dictionary<string, object>>();
        private IEnumerator PressTargetReady(string name)
        {
            // Newly rebuilt graphics acquire valid Canvas depth/culling during the real
            // frame. Waiting here is not permission to dispatch through an obstruction.
            yield return null;
            var button = ActiveControl<Button>(name);
            Require(button.IsInteractable(), "Target32 UI button disabled: " + name);
            Vector2 point = VisiblePoint(button);
            Canvas.ForceUpdateCanvases();
            var rect = (RectTransform)button.transform;
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            var screenCorners = corners.Select(c => RectTransformUtility.WorldToScreenPoint(null, c)).ToArray();
            bool insideScreen = screenCorners.All(c => c.x >= -.5f && c.x <= Screen.width + .5f && c.y >= -.5f && c.y <= Screen.height + .5f);
            bool insideMasks = button.GetComponentsInParent<Mask>().Where(m => m.enabled).All(m =>
                screenCorners.All(c => RectTransformUtility.RectangleContainsScreenPoint((RectTransform)m.transform, c, null)));
            var e = new PointerEventData(EventSystem.current) { position = point, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>(); EventSystem.current.RaycastAll(e, hits);
            var firstButton = hits.Count == 0 ? null : hits[0].gameObject.GetComponentInParent<Button>();
            var graphic = button.targetGraphic;
            target32PointerDiagnostics.Add(new Dictionary<string, object> {
                ["frame"] = Time.frameCount, ["target"] = name, ["focused"] = Application.isFocused,
                ["screenWidth"] = Screen.width, ["screenHeight"] = Screen.height, ["insideScreen"] = insideScreen,
                ["insideAllMasks"] = insideMasks, ["graphicDepth"] = graphic == null ? -1 : graphic.depth,
                ["graphicCulled"] = graphic != null && graphic.canvasRenderer.cull,
                ["firstHit"] = hits.Count == 0 ? null : hits[0].gameObject.name,
                ["firstButton"] = firstButton == null ? null : firstButton.name, ["firstButtonIsTarget"] = firstButton == button,
                ["hits"] = hits.Take(8).Select(h => h.gameObject.name).ToArray() });
            File.WriteAllText(Path.Combine(configuredDirectory, "v06-target32-raycasts.json"), Json(target32PointerDiagnostics), Encoding.UTF8);
            Require(insideScreen && insideMasks, "Target32 control is not completely visible: " + name);
            Require(firstButton == button, "Target32 first raycast is not the intended button: " + name + "; first hit " + (hits.Count == 0 ? "none" : hits[0].gameObject.name));
            ExecuteEvents.Execute(button.gameObject, e, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private PointerEventData BeginControllerDrag(string id)
        {
            var card = ActiveControl<Button>("OrderCard" + id);
            var e = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left, position = VisiblePoint(card) };
            ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.beginDragHandler);
            Require(app.IsControllerDragging, "Actual controller BeginDrag handler did not start.");
            return e;
        }
        private void EndControllerDrag(string id, PointerEventData e, Vector2 destination)
        {
            var card = ActiveControl<Button>("OrderCard" + id); e.position = destination;
            ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(card.gameObject, e, ExecuteEvents.endDragHandler);
        }
        private static RunState CreateBrokenMimicChainState(GameConfig config)
        {
            var chain = GameSession.CreateDebugWorkspace(config, new MemorySaveStore()).State; chain.isDebug = true;
            chain.controllers = Enumerable.Range(1, 15).Select(i => new ControllerInstance { definitionId = i == 15 ? "C46" : "C41", instanceId = "ITEM_" + i.ToString("D5"), acquiredSerial = i, enchantmentId = "none" }).ToList();
            chain.nextItemSerial = 16;
            var session = new GameSession(config, new MemorySaveStore(), chain);
            Accept(session.Execute(new SessionCommand { kind = CommandKind.StartStage, commandId = "chain-start", expectedActionSequence = 0 }));
            chain = session.State; chain.controllers.RemoveAt(14); chain.stage.destroyedControllerIds.Add("ITEM_00015");
            return chain;
        }
        private IEnumerator VerifyV06ControllerDrag()
        {
            phase = "V06ControllerDrag";
            var state = GameSession.CreateDebugWorkspace(app.Config, new MemorySaveStore()).State;
            state.isDebug = true;
            string[] definitions = { "C41", "C21", "C46" };
            state.controllers = definitions.Select((id, i) => new ControllerInstance { definitionId = id, instanceId = "ITEM_" + (i + 1).ToString("D5"), acquiredSerial = i + 1, enchantmentId = "none" }).ToList();
            state.nextItemSerial = 4;
            InstallDebugWorkspace(state, "debug-drag-save");
            int capacityBefore = app.Session.EffectiveCapacity;
            app.ShowControllerOrder(); yield return null;
            yield return SetActualResolution(1280, 720); yield return null;
            Require(ActiveControl<Button>("OrderCard" + state.controllers[0].instanceId) != null, "Controller order panel did not survive native resize.");
            string first = state.controllers[0].instanceId, last = state.controllers[2].instanceId;
            var e = BeginControllerDrag(first);
            var target = ActiveControl<Button>("OrderCard" + last); var rect = (RectTransform)target.transform;
            var destination = RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(new Vector3(rect.rect.xMax - 8, rect.rect.center.y)));
            yield return Capture("v06-controller-drag-preview");
            EndControllerDrag(first, e, destination); yield return null;
            var ordered = app.Session.State;
            Require(ordered.actionSequence == state.actionSequence + 1 && ordered.controllers.Last().instanceId == first, "Real drag did not commit exactly one permutation.");
            Require(app.Session.EffectiveCapacity < capacityBefore, "C41 right-neighbor change did not update effective capacity.");
            Require(ordered.gold == state.gold && ordered.diceRngState == state.diceRngState && ordered.shopRngState == state.shopRngState && ordered.effectRngState == state.effectRngState, "Reorder consumed wallet or RNG.");
            yield return Capture("v06-controller-drag-committed");
            string exact = JsonUtility.ToJson(ordered), disk = FileHash(app.SaveLocation);
            e = BeginControllerDrag(first); var point = e.position;
            EndControllerDrag(first, e, point); yield return null;
            Require(JsonUtility.ToJson(app.Session.State) == exact && FileHash(app.SaveLocation) == disk, "Same-slot drop must be a true no-op.");
            e = BeginControllerDrag(first); EndControllerDrag(first, e, new Vector2(-50, -50)); yield return null;
            Require(JsonUtility.ToJson(app.Session.State) == exact && FileHash(app.SaveLocation) == disk, "Outside drop changed persisted state.");
            BeginControllerDrag(first); yield return EscapeThroughInputSystem("controller-drag-cancel");
            Require(!app.IsControllerDragging && JsonUtility.ToJson(app.Session.State) == exact && FileHash(app.SaveLocation) == disk, "Esc did not cancel drag without changes.");
            BeginControllerDrag(first); InvokePrivate("OnApplicationFocus", false); yield return null;
            Require(!app.IsControllerDragging && JsonUtility.ToJson(app.Session.State) == exact, "Focus-loss callback did not cancel drag.");
            V06Check("controller-drag-and-cancel", "uGUI BeginDrag/Drag/EndDrag events; InputSystem Escape; focus callback (not physical Alt-Tab)", new Dictionary<string, object> {
                ["capacityBefore"] = capacityBefore, ["capacityAfter"] = app.Session.EffectiveCapacity, ["sameSlotNoop"] = true, ["outsideCancelled"] = true, ["escapeCancelled"] = true,
                ["focusCallbackCancelled"] = true, ["physicalFocusLoss"] = "not_executed", ["statePreserved"] = true
            });
            InvokePrivate("CloseModal"); Accept(app.Send(new SessionCommand { kind = CommandKind.StartStage })); yield return WaitIdle();
            app.ShowControllerOrder(); yield return null;
            var frozenCard = ActiveControl<Button>("OrderCard" + first);
            ExecuteEvents.Execute(frozenCard.gameObject, new PointerEventData(EventSystem.current) { position = VisiblePoint(frozenCard), button = PointerEventData.InputButton.Left }, ExecuteEvents.beginDragHandler);
            Require(!app.IsControllerDragging, "Started-stage controller order was draggable.");
            V06Check("controller-stage-frozen", "Attempted actual BeginDrag after StartStage", true);
            // Independent, explicitly damaged debug fixture exercises a chain whose
            // terminal owner disappeared after StartStage. Frozen bindings may not rebind.
            var chain = CreateBrokenMimicChainState(app.Config);
            InstallDebugWorkspace(chain, "debug-chain-save"); app.ShowControllerOrder(); yield return null;
            Require(ControllerRules.ResolveSources(app.Session.State.controllers, app.Session.State.stage.mimicBindings).All(s => s.abilityId == null), "Destroyed terminal owner rebound in a long mimic chain.");
            string firstLabel = ActiveControl<Button>("OrderCardITEM_00001").GetComponentInChildren<TextMeshProUGUI>().text;
            string lastLabel = ActiveControl<Button>("OrderCardITEM_00014").GetComponentInChildren<TextMeshProUGUI>().text;
            Require(firstLabel.Contains("无可解析能力") && lastLabel.Contains("目标已损坏"), "Long-chain invalid/destroyed hints are missing from actual cards.");
            yield return Capture("v06-mimic-chain-invalid-first"); yield return WheelToControl("OrderCardITEM_00014");
            yield return Capture("v06-mimic-chain-destroyed-last");
            V06Check("long-mimic-chain-destroyed-owner-ui", "14 C41 frozen chain; actual rendered first/last labels; dedicated debug fixture", new Dictionary<string, object> { ["firstLabel"] = firstLabel, ["lastLabel"] = lastLabel });
            app.ReturnNormalWorkspace(); yield return null;
            yield return SetActualResolution(1920, 1080); app.Refresh();
            Require(FileHash(normalSave) == normalSaveHash, "Controller fixture modified ordinary save.");
        }

        private IEnumerator DisplayReady()
        {
            double until = Time.realtimeSinceStartupAsDouble + 14;
            while (app.DisplayPreferences.IsTransitioning && Time.realtimeSinceStartupAsDouble < until) yield return null;
            Require(!app.DisplayPreferences.IsTransitioning, "Native display transition exceeded timeout.");
            yield return null;
        }
        private Dictionary<string, object> ActualDisplay()
        {
            var d = app.DisplayPreferences.ReadActual();
            return new Dictionary<string, object> { ["display"] = d.displayName, ["width"] = d.width, ["height"] = d.height, ["mode"] = d.mode.ToString(), ["nativeWidth"] = d.nativeWidth, ["nativeHeight"] = d.nativeHeight, ["focused"] = Application.isFocused };
        }
        private void DisplayRecord(string id, string status, string reason = null)
        {
            displayChecks.Add(new Dictionary<string, object> { ["id"] = id, ["status"] = status, ["reason"] = reason, ["actualNativeReadback"] = ActualDisplay() });
            File.WriteAllText(Path.Combine(configuredDirectory, "v06-display-checks.json"), Json(displayChecks), Encoding.UTF8);
        }
        private IEnumerator VerifyV06Displays()
        {
            phase = "V06NativeDisplay";
            var prefs = app.DisplayPreferences;
            string before = JsonUtility.ToJson(app.Session.State), disk = FileHash(normalSave);
            app.ShowDisplaySettings(); yield return null;
            foreach (int mode in new[] { 0, 1, 2 })
            {
                Press("Mode" + mode); Press("ApplyDisplay"); yield return DisplayReady();
                Require(prefs.IsPending && prefs.CanConfirm, "UI display mode could not reach confirmation: " + mode);
                var expected = mode == 0 ? DisplaySettings.PlatformFullscreen : mode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
                Require(Screen.fullScreenMode == expected, "Native fullscreen mode differs from selected mode.");
                yield return Capture("v06-display-mode-" + mode);
                Press("ConfirmDisplay"); yield return null;
                Require(!prefs.IsPending, "Display UI confirmation did not commit preference.");
                DisplayRecord("mode-" + expected, "passed");
            }
            if (DisplaySettings.PlatformFullscreen == FullScreenMode.ExclusiveFullScreen)
            {
                var original = prefs.ReadActual(); var d = Screen.mainWindowDisplayInfo;
                var sizes = prefs.Resolutions(d, FullScreenMode.ExclusiveFullScreen).Where(s => s.x != d.width || s.y != d.height).ToArray();
                if (sizes.Length == 0) DisplayRecord("exclusive-non-native-escape", "not_supported", "No non-native exclusive mode exposed by Screen.resolutions.");
                else
                {
                    var size = sizes.FirstOrDefault(s => s.x == 1280 && s.y == 720); if (size.x == 0) size = sizes[0];
                    Require(prefs.Apply(prefs.Choice(d, FullScreenMode.ExclusiveFullScreen, size.x, size.y)), "Non-native exclusive apply rejected.");
                    yield return DisplayReady();
                    Require(prefs.CanConfirm && Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen && Screen.width == size.x && Screen.height == size.y, "Non-native exclusive resolution was mistaken for a missing display or failed readback.");
                    app.ShowDisplaySettings(); yield return Capture("v06-exclusive-non-native-pending");
                    yield return EscapeThroughInputSystem("exclusive-non-native-revert"); yield return DisplayReady();
                    Require(!prefs.IsPending && Screen.width == original.width && Screen.height == original.height && Screen.fullScreenMode == original.mode, "Escape from non-native exclusive did not restore prior mode.");
                    DisplayRecord("exclusive-non-native-escape", "passed", "Actual supported non-native exclusive mode followed by InputSystem Escape rollback.");
                }
            }
            else DisplayRecord("exclusive-non-native-escape", "not_applicable", "macOS uses MaximizedWindow; no ExclusiveFullScreen claim is made.");
            // Arbitrary aspect ratios exercise the actual OS window and letterboxing,
            // never an offscreen render texture. Unsupported work areas are explicit.
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440), new Vector2Int(1600, 1000), new Vector2Int(1920, 800) })
            {
                var d = Screen.mainWindowDisplayInfo;
                string label = "window-" + size.x + "x" + size.y;
                if (size.x > d.workArea.width || size.y > d.workArea.height)
                { DisplayRecord(label, "not_supported", "Requested client dimensions exceed current display work area."); continue; }
                Require(prefs.Apply(prefs.Choice(d, FullScreenMode.Windowed, size.x, size.y)), "Display API rejected supported test window.");
                yield return DisplayReady();
                Require(prefs.CanConfirm && Screen.width == size.x && Screen.height == size.y && Screen.fullScreenMode == FullScreenMode.Windowed, "Actual window size differs from request.");
                app.ShowDisplaySettings(); yield return Capture("v06-display-" + size.x + "x" + size.y);
                Press("ConfirmDisplay"); yield return null;
                DisplayRecord(label, "passed");
            }
            var confirmed = prefs.ReadActual();
            var display = Screen.mainWindowDisplayInfo;
            Require(prefs.Apply(prefs.Choice(display, FullScreenMode.Windowed, 1280, 720)), "Could not begin timeout test.");
            yield return DisplayReady(); Require(prefs.CanConfirm, "Timeout test was never confirmable.");
            double start = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - start < 14) { Require(prefs.IsPending, "Display confirmation expired before 14 observed seconds."); yield return null; }
            while ((prefs.IsPending || prefs.IsTransitioning) && Time.realtimeSinceStartupAsDouble - start < 21) yield return null;
            Require(!prefs.IsPending && !prefs.IsTransitioning && Screen.width == confirmed.width && Screen.height == confirmed.height && Screen.fullScreenMode == confirmed.mode, "15-second timeout failed to restore confirmed native mode.");
            DisplayRecord("15-second-timeout-revert", "passed", "Real elapsed time observed; no shortened timer or mocked clock.");
            Require(prefs.Apply(prefs.Choice(Screen.mainWindowDisplayInfo, FullScreenMode.Windowed, 1280, 720)), "Could not begin Escape test.");
            yield return DisplayReady(); yield return EscapeThroughInputSystem("window-display-revert"); yield return DisplayReady();
            Require(!prefs.IsPending && Screen.width == confirmed.width && Screen.height == confirmed.height && Screen.fullScreenMode == confirmed.mode, "Escape failed to restore confirmed native display.");
            DisplayRecord("escape-revert", "passed", "InputSystem keyboard state event handled by GameApp.Update.");
            DisplayRecord("crash-pending-recovery", "not_executed", "Requires separate --verify-display-recovery-write/read processes; this main probe does not simulate a crash by rebuilding a component.");
            DisplayRecord("physical-focus-loss-and-display-removal", "not_executed", "Physical Alt-Tab/display disconnect not performed by this probe.");
            Require(JsonUtility.ToJson(app.Session.State) == before && FileHash(normalSave) == disk, "Display preferences changed game sequence, RNG or save.");
            InvokePrivate("CloseModal"); yield return SetActualResolution(1920, 1080); app.Refresh();
            V06Check("display-state-isolation", "Native Screen readback and exact ordinary save/state before/after", true);
        }

        private IEnumerator ReplayV06DebugTrace()
        {
            phase = "Debug24Replay";
            string[] args = Environment.GetCommandLineArgs();
            string path = Array.IndexOf(args, "--verify-debug-trace") >= 0 ? RequiredArgument(args, "--verify-debug-trace") : Path.Combine(Path.GetDirectoryName(configuredTrace), "debug-24-trace.json");
            Require(File.Exists(path), "Debug integration trace missing: " + path);
            string json = File.ReadAllText(path); debugTraceHash = Hash(json);
            var script = JsonUtility.FromJson<DebugTrace>(json);
            Require(script.debug && script.configVersion == app.Config.configVersion && script.initialWorkspace != null && script.steps.Length > 0, "Debug trace header is invalid.");
            // JsonUtility materializes some explicit JSON nulls as default nested DTOs.
            // Restore only members whose ORIGINAL JSON token was null, not empty or invalid data.
            string rawInitial = JsonMember(json, "initialWorkspace");
            if (JsonMember(rawInitial, "lastSubmission") == "null") script.initialWorkspace.lastSubmission = null;
            if (JsonMember(rawInitial, "lastStageSettlement") == "null") script.initialWorkspace.lastStageSettlement = null;
            if (JsonMember(rawInitial, "shop") == "null") script.initialWorkspace.shop = null;
            if (JsonMember(rawInitial, "hand") == "null") script.initialWorkspace.hand = null;
            var rawSteps = JsonElements(JsonMember(json, "steps"));
            Require(rawSteps.Count == script.steps.Length, "Debug trace JSON element count differs.");
            for (int i = 0; i < script.steps.Length; i++)
            {
                var rawScene = JsonMember(JsonMember(rawSteps[i], "command"), "debugScene");
                if (rawScene == "null") script.steps[i].command.debugScene = null;
                else if (script.steps[i].command.debugScene != null)
                {
                    if (JsonMember(rawScene, "currentResults") == "null") script.steps[i].command.debugScene.currentResults = null;
                    if (JsonMember(rawScene, "mimicBindings") == "null") script.steps[i].command.debugScene.mimicBindings = null;
                }
            }
            InstallDebugWorkspace(script.initialWorkspace, "debug24-save");
            int shops = 0, settlements = 0, risks = 0;
            for (int i = 0; i < script.steps.Length; i++)
            {
                var step = script.steps[i]; var command = step.command.Clone();
                if (command.kind == CommandKind.ConfirmLocks)
                {
                    foreach (var id in command.dieIds) Require(app.Session.TogglePending(id), "Debug trace could not select " + id);
                    app.Refresh(); yield return null;
                }
                var result = app.Send(command); Accept(result);
                if (app.IsBusy) { yield return null; app.SkipVisual(); }
                yield return WaitIdle();
                var state = app.Session.State;
                Require(state.actionSequence == step.actionSequence && state.phase == step.phase && state.gold == step.gold && state.stage.stageIndex == step.stageIndex
                    && state.stage.score == step.stageScore && state.stage.target == step.target && result.receipt.score == step.submissionScore
                    && state.diceRngState == step.diceRngState && state.shopRngState == step.shopRngState && state.effectRngState == step.effectRngState,
                    "Debug trace state/scoring/RNG differs at " + i + " " + command.kind);
                Require(GameSession.StateHash(state) == step.stateHash, "Debug trace canonical state differs at " + i + " " + command.kind);
                var stored = new AtomicSaveStore(app.SaveLocation, app.Config, SaveSlotPolicy.Debug).Load();
                Require(stored != null && JsonUtility.ToJson(stored) == JsonUtility.ToJson(state) && JsonUtility.ToJson(app.ViewState) == JsonUtility.ToJson(state), "Debug replay disk/render state differs.");
                if (command.kind == CommandKind.EndStage) settlements++;
                if (command.kind == CommandKind.Continue && state.phase == "Shop") shops++;
                if (command.kind == CommandKind.ChooseChallenge && command.risk) risks++;
                debug24Steps.Add(new Dictionary<string, object> { ["index"] = i, ["kind"] = command.kind.ToString(), ["actionSequence"] = state.actionSequence, ["phase"] = state.phase,
                    ["stageIndex"] = state.stage.stageIndex, ["gold"] = state.gold, ["score"] = state.stage.score, ["risk"] = state.stage.risk,
                    ["ruleStateSha256"] = GameSession.StateHash(state), ["savedStateMatches"] = true, ["renderedStateMatches"] = true });
                File.AppendAllText(Path.Combine(configuredDirectory, "debug24-replay.jsonl"), Json(debug24Steps.Last()) + "\n", Encoding.UTF8);
                if (command.kind == CommandKind.EndStage && (state.stage.stageIndex == 0 || state.stage.stageIndex == 23)) yield return Capture("debug24-settlement-" + (state.stage.stageIndex + 1));
            }
            Require(app.Session.State.phase == script.expectedFinalPhase && settlements == script.stages && shops == script.shops && risks == script.risks && app.Session.State.isDebug, "Debug transition coverage incomplete.");
            Require(FileHash(normalSave) == normalSaveHash, "Debug24 replay altered ordinary checkpoint.");
            yield return Capture("debug24-final");
            File.WriteAllText(Path.Combine(configuredDirectory, "debug24-final-state.json"), JsonUtility.ToJson(app.Session.State, true), Encoding.UTF8);
            debug24Passed = true;
            V06Check("debug24-real-player-replay", "GameApp.Send/presentation; every canonical trace state, RNG, disk and rendered state compared", new Dictionary<string, object> { ["stages"] = settlements, ["shops"] = shops, ["risks"] = risks, ["steps"] = script.steps.Length, ["traceSha256"] = debugTraceHash });
            app.ReturnNormalWorkspace(); yield return null;
        }

        // Small read-only token slicer. Values are never interpreted as code and no
        // malformed/absent value is silently replaced by a gameplay default.
        private static void JsonWhitespace(string json, ref int at) { while (at < json.Length && char.IsWhiteSpace(json[at])) at++; }
        private static int JsonTokenEnd(string json, int at)
        {
            Require(at < json.Length, "Missing JSON token.");
            char first = json[at++];
            if (first == '"')
            {
                while (at < json.Length) { char c = json[at++]; if (c == '\\') { Require(at < json.Length, "Truncated JSON escape."); at++; } else if (c == '"') return at; }
                throw new FormatException("Unterminated JSON string.");
            }
            if (first == '{' || first == '[')
            {
                char close = first == '{' ? '}' : ']';
                while (at < json.Length)
                {
                    JsonWhitespace(json, ref at); Require(at < json.Length, "Truncated JSON container.");
                    if (json[at] == close) return at + 1;
                    if (json[at] == ',' || json[at] == ':') at++; else at = JsonTokenEnd(json, at);
                }
                throw new FormatException("Unterminated JSON container.");
            }
            while (at < json.Length && !char.IsWhiteSpace(json[at]) && json[at] != ',' && json[at] != '}' && json[at] != ']') at++;
            return at;
        }
        private static string JsonMember(string json, string name)
        {
            Require(json != null, "Missing JSON object for " + name);
            int at = 0; JsonWhitespace(json, ref at); Require(at < json.Length && json[at++] == '{', "Expected JSON object for " + name);
            while (at < json.Length)
            {
                JsonWhitespace(json, ref at); if (json[at] == '}') break;
                Require(json[at] == '"', "Expected JSON member name.");
                int end = JsonTokenEnd(json, at); string key = json.Substring(at + 1, end - at - 2); at = end;
                JsonWhitespace(json, ref at); Require(json[at++] == ':', "Expected JSON colon."); JsonWhitespace(json, ref at);
                end = JsonTokenEnd(json, at); string value = json.Substring(at, end - at); at = end;
                if (key == name) return value;
                JsonWhitespace(json, ref at); if (json[at] == ',') at++; else if (json[at] != '}') throw new FormatException("Expected JSON member delimiter.");
            }
            throw new FormatException("Required JSON member is absent: " + name);
        }
        private static List<string> JsonElements(string json)
        {
            Require(json != null, "Missing JSON array."); int at = 0; JsonWhitespace(json, ref at);
            Require(at < json.Length && json[at++] == '[', "Expected JSON array."); var result = new List<string>();
            while (at < json.Length)
            {
                JsonWhitespace(json, ref at); if (json[at] == ']') return result;
                int end = JsonTokenEnd(json, at); result.Add(json.Substring(at, end - at)); at = end;
                JsonWhitespace(json, ref at); if (json[at] == ',') at++; else Require(json[at] == ']', "Expected JSON array delimiter.");
            }
            throw new FormatException("Unterminated JSON array.");
        }
        private IEnumerator VerifyDisplayRecoveryProcess()
        {
            string[] args = Environment.GetCommandLineArgs();
            bool writer = Array.IndexOf(args, "--verify-display-recovery-write") >= 0;
            Require(!(writer && Array.IndexOf(args, "--verify-display-recovery-read") >= 0), "Choose one recovery process role.");
            string directory = Path.GetFullPath(RequiredArgument(args, writer ? "--verify-display-recovery-write" : "--verify-display-recovery-read"));
            // Dedicated path is named by the runner; it never aliases the game's real preferences.
            Require(!string.Equals(directory.TrimEnd(Path.DirectorySeparatorChar), Application.persistentDataPath.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase), "Recovery evidence cannot use the player's persistent-data directory.");
            Directory.CreateDirectory(directory);
            string settings = Path.Combine(directory, "display-settings.json"), ready = Path.Combine(directory, "ready-to-terminate.json");
            var prefs = app.DisplayPreferences;
            int pid = System.Diagnostics.Process.GetCurrentProcess().Id;
            phase = writer ? "DisplayRecoveryWrite" : "DisplayRecoveryRead";
            environment = EnvironmentReport();
            if (writer)
            {
                Require(!File.Exists(settings) && !File.Exists(ready), "Writer requires a new, empty recovery evidence directory.");
                yield return SetActualResolution(1280, 720); app.Refresh();
                prefs.Initialize(settings, false);
                var confirmed = prefs.ReadActual();
                var candidate = prefs.Choice(Screen.mainWindowDisplayInfo, FullScreenMode.FullScreenWindow, Screen.mainWindowDisplayInfo.width, Screen.mainWindowDisplayInfo.height);
                Require(prefs.Apply(candidate), "Recovery writer could not apply candidate."); yield return DisplayReady();
                Require(prefs.CanConfirm && JsonMember(File.ReadAllText(settings), "pending") == "true", "Pending preference was not durable before termination.");
                var receipt = new RecoveryReceipt { status = "awaiting_external_process_termination", writerProcessId = pid, confirmed = confirmed,
                    candidate = candidate, actual = prefs.ReadActual(), settingsPath = settings, pendingSha256 = FileHash(settings),
                    buildIdentity = Resources.Load<TextAsset>("BuildIdentity")?.text, evidenceDirectory = configuredDirectory };
                File.WriteAllText(ready, JsonUtility.ToJson(receipt, true), new UTF8Encoding(false));
                // Parent must kill this process promptly while pending is still true.
                Debug.Log("P06_DISPLAY_RECOVERY_READY " + ready);
                Console.Error.WriteLine("P06_DISPLAY_RECOVERY_READY " + ready);
                WriteReport("awaiting_external_process_termination", null, -1);
                double deadline = Time.realtimeSinceStartupAsDouble + 14;
                while (Time.realtimeSinceStartupAsDouble < deadline && prefs.IsPending) yield return null;
                throw new InvalidOperationException("Recovery writer was not externally terminated before the real confirmation timeout. Start again in a new directory.");
            }
            else
            {
                Require(File.Exists(settings) && File.Exists(ready), "Recovery reader requires a prior writer receipt and settings.");
                var receipt = JsonUtility.FromJson<RecoveryReceipt>(File.ReadAllText(ready));
                Require(receipt.writerProcessId != pid && receipt.status == "awaiting_external_process_termination" && receipt.pendingSha256 == FileHash(settings)
                    && JsonMember(File.ReadAllText(settings), "pending") == "true", "Recovery input is not unchanged pending data from another process.");
                prefs.Initialize(settings, true); yield return DisplayReady();
                Require(!prefs.IsPending && !prefs.IsTransitioning && Screen.width == receipt.confirmed.width && Screen.height == receipt.confirmed.height && Screen.fullScreenMode == receipt.confirmed.mode,
                    "Fresh process did not restore confirmed native mode.");
                Require(JsonMember(File.ReadAllText(settings), "pending") == "false", "Recovered settings were not durably cleared.");
                receipt.status = "passed"; receipt.readerProcessId = pid; receipt.actual = prefs.ReadActual(); receipt.evidenceDirectory = configuredDirectory;
                File.WriteAllText(Path.Combine(directory, "recovery-read-receipt.json"), JsonUtility.ToJson(receipt, true), new UTF8Encoding(false));
                DisplayRecord("fresh-process-pending-recovery", "passed", "Writer was externally terminated; reader PID differs and hashes the exact durable pending settings.");
                yield return Capture("v06-display-fresh-process-recovery");
                CheckUnityErrors(); WriteReport("recovery_passed", null, 0); finished = true;
                Debug.Log("P06_DISPLAY_RECOVERY_PASSED " + Path.Combine(directory, "recovery-read-receipt.json"));
                Application.Quit(0);
            }
        }
    }
}
