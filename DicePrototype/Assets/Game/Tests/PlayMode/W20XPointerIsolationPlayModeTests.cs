using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W20X item 0 (docs/specs/v11/W20X_TEST_CHANGES.md): windowed PlayMode results do not depend on where the
    /// real OS cursor rests. In W14's final runs the cursor rested over the Game view and two tests failed. The UI input
    /// module's Point action checks the state of its controls when it is enabled, which happens on every Bootstrap load: the
    /// resting position became a pointer that hovered whatever lay under it, and later real input kept it there.
    ///
    /// The harness now disables the real (native) pointer devices for every case of this fixture before the scene loads,
    /// and sets their position to the control's default (0, 0), so the initial state check finds nothing and queued real
    /// input is dropped. They are handed back once the scene is unloaded. Tests drive the pointer themselves through
    /// PointerEventData and ExecuteEvents, which never needed a device. The resource diagnostic is its own fixture and
    /// still watches the real mouse and keyboard. Nothing here moves the OS cursor: the Input System's device state is not
    /// the cursor.
    ///
    /// Setting the environment variable DICECRAFT_W20X_POINTER_ISOLATION=off before starting the Editor turns the
    /// isolation off for a negative-control run of the regression tests below.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private const string PointerIsolationVariable20X="DICECRAFT_W20X_POINTER_ISOLATION";
        private static bool PointerIsolationOn20X=>!string.Equals(Environment.GetEnvironmentVariable(PointerIsolationVariable20X),"off",StringComparison.OrdinalIgnoreCase);
        private readonly List<InputDevice> isolatedPointers20X=new List<InputDevice>();
        private StrayCursor20X stray20X;

        /// <summary>The real pointer devices: the mice, pens and touch screens the platform backend created. A device a test
        /// adds itself is not native and is left alone.</summary>
        private static InputDevice[] RealPointers20X()=>InputSystem.devices.Where(d=>d is Pointer&&d.native).ToArray();
        private static Mouse RealMouse20X()
        {
            var mouse=InputSystem.devices.OfType<Mouse>().FirstOrDefault(m=>m.native);
            Assert.That(mouse,Is.Not.Null,"This regression needs the platform's real Mouse device.");
            return mouse;
        }

        private void IsolateRealPointers20X()
        {
            if(!PointerIsolationOn20X)return;
            foreach(var device in RealPointers20X())
            {
                InputSystem.DisableDevice(device);
                // A disabled device keeps its last position (Pointer.position is a dontReset control). The UI module's Point
                // action would read it in its initial state check, and the card hover tilt (Art07ButtonMotion) reads
                // Pointer.current: the control's default value takes its place.
                InputState.Change(((Pointer)device).position,Vector2.zero);
                if(!isolatedPointers20X.Contains(device))isolatedPointers20X.Add(device);
            }
        }
        private void RestoreRealPointers20X()
        {
            foreach(var device in isolatedPointers20X)if(device.added)InputSystem.EnableDevice(device);
            isolatedPointers20X.Clear();
        }

        /// <summary>A resting OS cursor as the Input System sees it: the real Mouse device reports one position on every
        /// update. Every device's input goes to the Game view whatever has focus (the worst case for the harness); the
        /// routing settings are put back on dispose.</summary>
        private sealed class StrayCursor20X:IDisposable
        {
            public readonly Mouse Device;
            private readonly InputSettings.EditorInputBehaviorInPlayMode routing;
            private readonly InputSettings.BackgroundBehavior background;
            private Vector2 at;private bool resting;
            /// <summary>State events queued on the real mouse so far.</summary>
            public int Queued{get;private set;}
            public StrayCursor20X(Mouse mouse)
            {
                Device=mouse;routing=InputSystem.settings.editorInputBehaviorInPlayMode;background=InputSystem.settings.backgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
                InputSystem.settings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
                InputSystem.onBeforeUpdate+=Queue;
            }
            public void Rest(Vector2 screenPoint){at=screenPoint;resting=true;Queue();}
            public void Lift(){resting=false;}
            private void Queue()
            {
                if(!resting||!Device.added)return;
                InputSystem.QueueStateEvent(Device,new MouseState{position=at});Queued++;
            }
            public void Dispose()
            {
                InputSystem.onBeforeUpdate-=Queue;resting=false;
                InputSystem.settings.editorInputBehaviorInPlayMode=routing;InputSystem.settings.backgroundBehavior=background;
            }
        }
        private void StopStrayCursor20X(){stray20X?.Dispose();stray20X=null;}

        private static Vector2 ScreenCentre20X(RectTransform target)=>RectTransformUtility.WorldToScreenPoint(null,target.TransformPoint(target.rect.center));
        private static InputSystemUIInputModule UiModule20X()
        {
            var module=EventSystem.current!=null?EventSystem.current.GetComponent<InputSystemUIInputModule>():null;
            Assert.That(module,Is.Not.Null,"Bootstrap's EventSystem carries the InputSystemUIInputModule.");
            return module;
        }

        /// <summary>
        /// Positive control: with the harness isolation lifted for a moment, the stray cursor's state events on the real
        /// Mouse reach the UI input module, which hovers <paramref name="target"/>, and <paramref name="reached"/> sees the
        /// element react. Then the isolation is restored and the module drops the pointer it made (a disable and enable sends
        /// the exit events and forgets the pointer, as a scene change does).
        /// </summary>
        private IEnumerator StrayControl20X(RectTransform target,Func<bool> reached,string what)
        {
            var module=UiModule20X();var mouse=stray20X.Device;var at=ScreenCentre20X(target);
            RestoreRealPointers20X();
            bool hit=false,effect=false;string seen="nothing";
            try
            {
                stray20X.Rest(at);
                double deadline=Time.realtimeSinceStartupAsDouble+3;
                while(Time.realtimeSinceStartupAsDouble<deadline)
                {
                    yield return null;
                    var ray=module.GetLastRaycastResult(mouse.deviceId);
                    seen=ray.gameObject!=null?ray.gameObject.name:"nothing";
                    hit=ray.gameObject!=null&&ray.gameObject.transform.IsChildOf(target);
                    if(hit&&reached()){effect=true;break;}
                }
            }
            finally
            {
                stray20X.Lift();
                IsolateRealPointers20X();
                module.enabled=false;module.enabled=true;
            }
            TestContext.WriteLine("W20X-00 positive control: stray cursor at "+at+" over "+what+": module hit "+hit+" ("+seen+"), reacted "+effect);
            Assert.That(hit,Is.True,"Positive control: without the isolation the stray cursor at "+at+" reaches "+what+" through the UI input module (last raycast: "+seen+").");
            Assert.That(effect,Is.True,"Positive control: "+what+" reacts to the stray cursor.");
            if(PointerIsolationOn20X)Assert.That(module.IsPointerOverGameObject(mouse.deviceId),Is.False,"After the control the module holds no pointer for the real mouse.");
        }
        /// <summary>After the run: the stray cursor kept queueing, the module never made a pointer for the real mouse and the
        /// device never took the stray position (its events were dropped).</summary>
        private void AssertStrayBlocked20X(int queuedBefore,string what)
        {
            var module=UiModule20X();var mouse=stray20X.Device;
            if(!PointerIsolationOn20X)TestContext.WriteLine("W20X-00 negative control: the harness isolation is off ("+PointerIsolationVariable20X+"=off).");
            Assert.That(stray20X.Queued-queuedBefore,Is.GreaterThan(10),what+": the stray cursor kept reporting its position.");
            Assert.That(module.IsPointerOverGameObject(mouse.deviceId),Is.False,what+": the module holds no pointer for the real mouse.");
            Assert.That(mouse.position.ReadUnprocessedValue(),Is.EqualTo(Vector2.zero),what+": the real mouse never took the stray position.");
        }

        /// <summary>
        /// W20X-00, test 1: PLAY_ART07_RESULTROWS_GameplayItemHoverUsesDelayAndKeepsInventoryUnchanged with a stray real
        /// cursor resting (a) over the first card of the row, another element with a hover tip (the W14 failure: that card's
        /// tip took the shared tooltip, so the item's exit could not hide it), and (b) over the hovered item itself. For each
        /// position a positive control shows the cursor reaching that element when the isolation is lifted; with it in place
        /// the original body runs unchanged and passes.
        /// </summary>
        [UnityTest] public IEnumerator PLAY_W20X_00_StrayRealMouseLeavesTheGameplayItemHoverTestUnaffected()
        {
            stray20X=new StrayCursor20X(RealMouse20X());
            StartRun("70912");
            foreach(bool overItem in new[]{false,true})
            {
                int queued=0;string what=null;
                yield return GameplayItemHover07(button=>StrayOverHover20X(button,overItem,q=>queued=q,w=>what=w));
                AssertStrayBlocked20X(queued,what);
                stray20X.Lift();
            }
            StopStrayCursor20X();
        }
        private IEnumerator StrayOverHover20X(UnityEngine.UI.Button item,bool overItem,Action<int> queuedFrom,Action<string> named)
        {
            RectTransform target=(RectTransform)item.transform;string what="the hovered item "+item.name;
            if(!overItem)
            {
                var card=app.canvas.GetComponentsInChildren<Art07HoverTip>(false).Select(h=>h.transform as RectTransform)
                    .FirstOrDefault(t=>t!=null&&t.name.StartsWith("Controller_",StringComparison.Ordinal));
                Assert.That(card,Is.Not.Null,"The play row shows a card with a hover tip.");
                target=card;what="the card "+card.name+" (another element with a hover tip)";
            }
            named(what);
            yield return StrayControl20X(target,()=>HasActive("ContentTooltip"),what);
            // The control's tooltip closes with the module's exit event (60 ms) before the original body starts. Without the
            // isolation (negative control) the module's re-enable reads the resting position again and the tip stays: the
            // body then runs as it did in W14.
            double deadline=Time.realtimeSinceStartupAsDouble+2;
            while(PointerIsolationOn20X&&HasActive("ContentTooltip")&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            if(PointerIsolationOn20X)Assert.That(HasActive("ContentTooltip"),Is.False,"The control's tooltip closed.");
            queuedFrom(stray20X.Queued);
            // The body's hover starts in this same frame and the cursor's first event is read in the next one, the order of
            // the W14 failure (the item's tip showed first, the card's tip then took the shared tooltip).
            stray20X.Rest(ScreenCentre20X(target));
        }

        /// <summary>
        /// W20X-00, test 2: PLAY_V13_CT01_CT02_CardGalleryInBothThemesAtTwoTextSizes with a stray real cursor resting over
        /// GalleryCompactTall13_0_3 (default style, 100 %, page 4: the card whose ordinal disc failed CT-02 in W14's run) for
        /// the whole walk of both styles, both text sizes and every page. A positive control first shows the card lifting
        /// under the cursor when the isolation is lifted. Captures and reports go to artifacts/cache/v11/w20x/&lt;size&gt;/stray-ct02.
        /// </summary>
        [UnityTest] public IEnumerator PLAY_W20X_00_StrayRealMouseLeavesTheCardGalleryContrastTestUnaffected()
        {
            stray20X=new StrayCursor20X(RealMouse20X());int queued=0;
            w13Output20X=W20XOutput("stray-ct02");
            try{yield return W13GalleryRun(()=>StrayOverGalleryCard20X(q=>queued=q));}
            finally{w13Output20X=null;}
            AssertStrayBlocked20X(queued,"the gallery walk");
            StopStrayCursor20X();
        }
        private IEnumerator StrayOverGalleryCard20X(Action<int> queuedFrom)
        {
            app.ApplyTheme11(GameTheme.DefaultId);W11TextScale(100);
            app.ShowCardGallery13(4);yield return null;Canvas.ForceUpdateCanvases();
            var card=FindActive<RectTransform>("GalleryCompactTall13_0_3");
            var painted=card.Cast<Transform>().OfType<RectTransform>().Where(t=>t.gameObject.activeSelf&&t.name!="Focus").ToArray();
            Assert.That(painted.Length,Is.GreaterThan(3));
            var rest=painted.ToDictionary(t=>t,t=>t.anchoredPosition);
            // The card lifts (8 units) when hovered; the idle sway alone moves a child by under 2.
            yield return StrayControl20X(card,()=>painted.Any(t=>Vector2.Distance(t.anchoredPosition,rest[t])>4),"the gallery card "+card.name);
            var at=ScreenCentre20X(card);
            app.HideCardGallery13();
            queuedFrom(stray20X.Queued);
            stray20X.Rest(at);
            yield return null;yield return null;
        }

        /// <summary>Evidence folder of W20X: artifacts/cache/v11/w20x/&lt;width&gt;x&lt;height&gt;/&lt;name&gt;.</summary>
        private static string W20XOutput(string name)
        {
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w20x",Screen.width+"x"+Screen.height,name));
            Directory.CreateDirectory(dir);return dir;
        }
    }
}
