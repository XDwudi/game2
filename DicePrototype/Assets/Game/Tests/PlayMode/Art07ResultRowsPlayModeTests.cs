using System;
using System.Collections;
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
        [UnityTest] public IEnumerator PLAY_ART07_RESULTROWS_RealVictoryShowsSixOrderedRowsAfterSealWithoutChangingSave()
        {
            StartRun("70911");
            foreach(int font in new[]{100,125,150})
            {
                SettlementField07("textScale07",font);UiKit.FontScale=font/100f;
                var scene=InformationScene07(Art07FixtureKind.MaximumTable);scene.stageIndex=23;scene.target="1";scene.remainingSubmissions=1;
                ApplyInformationScene07(scene);Click("Submit");app.SkipVisual();
                if(app.Session.State.phase=="StageSettlement")Click("SettlementContinue");
                Assert.That(app.Session.State.phase,Is.EqualTo("RunWon"));
                var reveal=FindActive<Art07ScreenReveal>("Screen");reveal.AutomaticTick=false;
                var names=new[]{"EndTotalScore","EndMaximumScore","EndMaximumMultiplier","EndDuration","EndBuild","EndStats"};
                var rows=names.Select(FindActive<RectTransform>).ToArray();
                string state=StateJson();var disk=File.ReadAllBytes(app.SaveLocation);
                Assert.That(reveal.Elapsed,Is.Zero);Assert.That(reveal.Duration,Is.EqualTo(1.2f).Within(.001f));
                Assert.That(rows.All(r=>r.GetComponent<CanvasGroup>().alpha==0),Is.True);
                reveal.Tick(.36f);Assert.That(rows.All(r=>r.GetComponent<CanvasGroup>().alpha==0),Is.True,"No statistics precede the 360ms seal.");
                for(int i=0;i<rows.Length;i++)
                {
                    reveal.Tick((i==5?1.2f:.36f+(i+1)*.14f)-reveal.Elapsed);
                    for(int j=0;j<=i;j++)Assert.That(rows[j].GetComponent<CanvasGroup>().alpha,Is.EqualTo(1).Within(.001f),names[j]);
                    for(int j=i+1;j<rows.Length;j++)Assert.That(rows[j].GetComponent<CanvasGroup>().alpha,Is.EqualTo(0).Within(.001f),names[j]);
                    yield return app.StartCoroutine(CaptureEdge07("S18-row"+i+"-font"+font));
                }
                Assert.That(reveal.IsPlaying,Is.False);
                Assert.That(rows[5].GetComponent<TextMeshProUGUI>().text,Does.Contain("种子").And.Contain("调试对局"));
                var paper=FindActive<RectTransform>("RunResult");
                foreach(var row in rows)
                {
                    var visible=row.GetComponentsInChildren<TextMeshProUGUI>(false)
                        .Where(t=>t.isActiveAndEnabled&&!string.IsNullOrWhiteSpace(t.text)).ToArray();
                    Assert.That(visible,Is.Not.Empty,row.name);
                    foreach(var label in visible)AssertSettlementInk07(label,V06ScreenRect(paper));
                }
                // Each build tile intentionally has an empty generic Button/Label plus its actual printed Name.
                foreach(var owned in app.Session.State.controllers)
                {
                    var name=rows[4].Find("Controller_"+owned.instanceId+"/Name").GetComponent<TextMeshProUGUI>();
                    Assert.That(name.text.Replace("\n",""),Is.EqualTo(app.Config.Controller(owned.definitionId).displayName));
                }
                for(int i=0;i<rows.Length;i++)for(int j=i+1;j<rows.Length;j++)
                    Assert.That(V06ScreenRect(rows[i]).Overlaps(V06ScreenRect(rows[j])),Is.False,names[i]+" vs "+names[j]);
                app.Refresh();Assert.That(reveal.IsPlaying,Is.False);Assert.That(reveal.Elapsed,Is.EqualTo(reveal.Duration));
                Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                reveal.AutomaticTick=true;
            }
            UiKit.ReducedMotion=true;SettlementField07("reduced",true);
            var reducedScene=InformationScene07(Art07FixtureKind.MaximumTable);reducedScene.stageIndex=23;reducedScene.target="1";reducedScene.remainingSubmissions=1;
            ApplyInformationScene07(reducedScene);Click("Submit");app.SkipVisual();if(app.Session.State.phase=="StageSettlement")Click("SettlementContinue");
            var reducedReveal=FindActive<Art07ScreenReveal>("Screen");reducedReveal.AutomaticTick=false;
            Assert.That(reducedReveal.Duration,Is.EqualTo(.12f));reducedReveal.Tick(.06f);
            Assert.That(FindActive<RectTransform>("RunVictorySeal07").localScale,Is.EqualTo(Vector3.one));
            yield return app.StartCoroutine(CaptureEdge07("S18-reduced-mid-font150"));
            string finalState=StateJson();var finalDisk=File.ReadAllBytes(app.SaveLocation);Click("SkipResultReveal");
            Assert.That(reducedReveal.IsPlaying,Is.False);Assert.That(StateJson(),Is.EqualTo(finalState));CollectionAssert.AreEqual(finalDisk,File.ReadAllBytes(app.SaveLocation));
            UiKit.ReducedMotion=false;reducedReveal.AutomaticTick=true;
        }

        [UnityTest] public IEnumerator PLAY_ART07_RESULTROWS_GameplayItemHoverUsesDelayAndKeepsInventoryUnchanged()
        {
            StartRun("70912");
            yield return GameplayItemHover07(null);
        }
        /// <summary>The body of the item hover test. V1.1 (W20X item 0): shared with the stray-cursor regression test, whose
        /// <paramref name="beforeHover"/> puts a resting real cursor somewhere on the screen once the item button exists.</summary>
        private IEnumerator GameplayItemHover07(Func<UnityEngine.UI.Button,IEnumerator> beforeHover)
        {
            ApplyInformationScene07(InformationScene07(Art07FixtureKind.MaximumTable));
            var item=app.Session.State.inventory.First();var button=Button("Item_"+item.instanceId);
            var hover=button.GetComponent<Art07HoverTip>();Assert.That(hover,Is.Not.Null);
            if(beforeHover!=null)yield return beforeHover(button);
            string state=StateJson();var disk=File.ReadAllBytes(app.SaveLocation);
            float shownAt=-1,delta=0;var original=hover.show;
            hover.show=()=>{shownAt=Time.unscaledTime;delta=Time.unscaledDeltaTime;original();};
            float began=Time.unscaledTime;hover.OnPointerEnter(new PointerEventData(EventSystem.current));
            Assert.That(HasActive("ContentTooltip"),Is.False);
            double limit=Time.realtimeSinceStartupAsDouble+3;
            while(shownAt<0&&Time.realtimeSinceStartupAsDouble<limit)yield return null;
            Assert.That(shownAt-began,Is.InRange(.3f-.0001f,.3f+delta+.001f));
            Assert.That(FindActive<Art07PanelMotion>("ContentTooltip").Duration,Is.EqualTo(.08f));
            yield return app.StartCoroutine(CaptureEdge07("S15-gameplay-item-hover"));
            hover.OnPointerExit(new PointerEventData(EventSystem.current));yield return new WaitForSecondsRealtime(.09f);
            Assert.That(HasActive("ContentTooltip"),Is.False);Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
        }
    }
}
