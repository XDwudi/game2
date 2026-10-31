using System;
using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
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
        public IEnumerator PLAY_V06_DebugPasswordAddApplyReloadAndReturnPreserveNormalCheckpoint()
        {
            StartRun("606");var normal=StateJson();byte[] normalDisk=File.ReadAllBytes(app.SaveLocation);
            Assert.That(app.UnlockDebug("XD is god "),Is.False);Assert.That(app.UnlockDebug("xd is god"),Is.False);Assert.That(app.UnlockDebug(" XD is god"),Is.False);
            Assert.That(StateJson(),Is.EqualTo(normal));Click("Pause");
            var password=FindActive<TMP_InputField>("DebugPassword");password.text="XD is god";Click("UnlockDebug");
            Assert.That(app.DebugUnlocked,Is.True);Assert.That(StateJson(),Is.EqualTo(normal));Click("UnlockDebug");
            Assert.That(app.IsDebugWorkspace,Is.True);Assert.That(app.Session.State.isDebug,Is.False);
            Click("AddC05");Click("DebugEnchantbase10");Click("AddC41");Click("AddC45");
            Click("ApplyDebugScene");Assert.That(app.Session.State.isDebug,Is.True,FindActive<TextMeshProUGUI>("DebugStatus").text);
            Assert.That(app.Session.State.controllers.Count,Is.EqualTo(3));
            Assert.That(app.Session.State.controllers[0].enchantmentId,Is.EqualTo("base10"));
            Assert.That(app.Session.State.controllers[2].state.activeColor,Is.EqualTo("red"));
            Click("DebugTab3");Click("AddItemI01");Click("AddItemI03");Click("ApplyDebugScene");
            Assert.That(app.Session.State.inventory.Count,Is.EqualTo(2));
            string debug=StateJson();Assert.That(app.SaveLocation,Does.EndWith("debug-run.json"));
            CollectionAssert.AreEqual(normalDisk,File.ReadAllBytes(Path.Combine(saveDirectory,"active-run.json")));
            Click("ReturnNormal");Assert.That(StateJson(),Is.EqualTo(normal));
            yield return LoadBootstrap();Assert.That(app.DebugUnlocked,Is.False);Assert.That(StateJson(),Is.EqualTo(normal));
            Assert.That(app.UnlockDebug("XD is god"),Is.True);Assert.That(app.ResumeDebugWorkspace(),Is.True);
            Assert.That(StateJson(),Is.EqualTo(debug));Click("CloseDialog");Click("Pause");Click("MainMenu");
            Assert.That(StateJson(),Is.EqualTo(normal));Click("ContinueRun");Assert.That(app.IsDebugWorkspace,Is.False);
            CollectionAssert.AreEqual(normalDisk,File.ReadAllBytes(app.SaveLocation));
        }

        [UnityTest]
        public IEnumerator PLAY_V06_DebugThirtyTwoDiceLevelsInputFailureAndExactPreviewAreAccessible()
        {
            StartRun("607");Assert.That(app.UnlockDebug("XD is god"),Is.True);app.EnterDebugWorkspace();
            Click("DebugTab1");for(int i=5;i<32;i++)Click("AddDie");
            Assert.That(HasActive("DebugDieD032"),Is.True);Click("DebugDieD032");Click("DebugPip0");
            Click("DebugTab2");Click("MaxLevels");Assert.That(HasActive("Levelall_red"),Is.True);
            Click("DebugTab3");FindActive<TMP_InputField>("Capacity").text="32";
            FindActive<TMP_InputField>("Target").text="999999999999999999999999999999999999999999999999999999999999";
            FindActive<TMP_InputField>("Wallet").text="bad";string before=StateJson();Click("ApplyDebugScene");
            Assert.That(StateJson(),Is.EqualTo(before));Assert.That(FindActive<TMP_InputField>("Wallet").text,Is.EqualTo("bad"));
            FindActive<TMP_InputField>("Wallet").text="1000000000";Click("ApplyDebugScene");
            Assert.That(app.Session.State.dice.Count,Is.EqualTo(32),FindActive<TextMeshProUGUI>("DebugStatus").text);
            Assert.That(app.Session.State.stage.capacity,Is.EqualTo(32));Assert.That(app.Session.State.handLevels.All(l=>l.level==100),Is.True);
            Assert.That(app.Session.State.colorLevels.All(l=>l.level==100),Is.True);
            Click("RunDebugScene");Assert.That(HasActive("PoolPage"),Is.True);Assert.That(HasActive("SlotPage"),Is.True);
            Canvas.ForceUpdateCanvases();
            var poolPageBounds=V06ScreenRect(Button("PoolPage").GetComponent<RectTransform>());
            foreach(var die in app.canvas.GetComponentsInChildren<DiePointer>(false).Where(d=>d.name.StartsWith("Die_")))
                Assert.That(poolPageBounds.Overlaps(V06ScreenRect(die.GetComponent<RectTransform>())),Is.False,"Dice pool pagination must not cover visible dice hit areas.");
            var slotPageBounds=V06ScreenRect(Button("SlotPage").GetComponent<RectTransform>());
            foreach(var label in app.canvas.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.name.StartsWith("SlotIndex")))
                Assert.That(slotPageBounds.Overlaps(V06ScreenRect(label.rectTransform)),Is.False,"Submission pagination must not cover slot numbers.");
            Click("PoolPage");Click("PoolPage");Click("PoolPage");Assert.That(HasActive("Die_D032"),Is.True);
            Click("Roll");app.SkipVisual();string pure=StateJson();app.ShowHandbook();Click("HandbookColors");
            Assert.That(HasActive("HandbookRow_all_red"),Is.True);Assert.That(StateJson(),Is.EqualTo(pure));
            yield return null;
        }
        private static Rect V06ScreenRect(RectTransform target)
        {
            var corners=new Vector3[4];target.GetWorldCorners(corners);
            return Rect.MinMaxRect(corners.Min(c=>c.x),corners.Min(c=>c.y),corners.Max(c=>c.x),corners.Max(c=>c.y));
        }

        [UnityTest]
        public IEnumerator PLAY_V06_ControllerInsertionNoOpOutsideCancelAndFrozenMimic()
        {
            StartRun("608");var fixture=app.Session.State;
            foreach(string id in new[]{"C41","C45","C51","C52"})
            {int serial=fixture.nextItemSerial++;fixture.controllers.Add(new ControllerInstance{instanceId="ITEM_"+serial.ToString("D5"),definitionId=id,acquiredSerial=serial,state=new ControllerDynamicState{activeColor=id=="C45"?"red":null}});}
            new AtomicSaveStore(app.SaveLocation,app.Config,SaveSlotPolicy.Normal).Save(fixture);
            yield return LoadBootstrap();Click("ContinueRun");Click("PreviewControllers");
            var original=app.Session.State;string first=original.controllers[0].instanceId,last=original.controllers[3].instanceId;
            var from=FindActive<DiePointer>("OrderCard"+first);var target=FindActive<RectTransform>("OrderCard"+last);
            Vector2 pos=RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(new Vector3(target.rect.xMax-10,target.rect.center.y,0)));
            var data=new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left,position=pos};
            ExecuteEvents.Execute(from.gameObject,data,ExecuteEvents.beginDragHandler);ExecuteEvents.Execute(from.gameObject,data,ExecuteEvents.dragHandler);
            Assert.That(app.IsControllerDragging,Is.True);Assert.That(app.Send(new SessionCommand{kind=CommandKind.StartStage}).accepted,Is.False);
            ExecuteEvents.Execute(from.gameObject,data,ExecuteEvents.endDragHandler);
            Assert.That(app.Session.State.controllers.Last().instanceId,Is.EqualTo(first));
            Assert.That(app.Session.State.controllers[0].instanceId,Is.EqualTo(original.controllers[1].instanceId));
            Assert.That(app.Session.State.actionSequence,Is.EqualTo(original.actionSequence+1));
            Assert.That(app.Session.State.effectRngState,Is.EqualTo(original.effectRngState));
            string after=StateJson();from=FindActive<DiePointer>("OrderCard"+first);data.position=new Vector2(-100,-100);
            ExecuteEvents.Execute(from.gameObject,data,ExecuteEvents.beginDragHandler);ExecuteEvents.Execute(from.gameObject,data,ExecuteEvents.endDragHandler);
            Assert.That(StateJson(),Is.EqualTo(after));
            var ids=app.Session.State.controllers.Select(c=>c.instanceId).ToArray();var noop=app.Send(new SessionCommand{kind=CommandKind.ReorderControllers,controllerIds=ids});
            Assert.That(noop.accepted&&noop.noChange,Is.True);Assert.That(StateJson(),Is.EqualTo(after));
            Click("CloseDialog");Click("StartStage");app.ShowControllerOrder();
            Assert.That(Button("MoveLeft"+first).interactable,Is.False);Assert.That(Button("MoveRight"+first).interactable,Is.False);
        }

        [UnityTest]
        public IEnumerator PLAY_V06_SettingsPasswordFocusBlocksSubmitAndDisplaySettingsStayOutsideRun()
        {
            StartRun("609");Click("StartStage");Click("Roll");Click("Skip");ClickDie("D001");Click("Confirm");string original=StateJson();
            Click("Pause");var password=FindActive<TMP_InputField>("DebugPassword");password.text="XD is god";password.ActivateInputField();
            yield return null;Assert.That(password.isFocused,Is.True,"Password editor must own focus before keyboard input.");
            using(var keyboard=new RoutedKeyboard()){yield return keyboard.Press(UnityEngine.InputSystem.Key.Enter);}
            Assert.That(StateJson(),Is.EqualTo(original));Assert.That(app.DebugUnlocked,Is.True);
            Click("DisplaySettings");Assert.That(HasActive("ResolutionList"),Is.True);Assert.That(StateJson(),Is.EqualTo(original));
            Click("Mode2");Click("AutomaticResolution");Assert.That(StateJson(),Is.EqualTo(original));Click("CloseDialog");
            Assert.That(app.Session.State.hand.orderedLockedIds,Is.EquivalentTo(new[]{"D001"}));
        }
    }
}
