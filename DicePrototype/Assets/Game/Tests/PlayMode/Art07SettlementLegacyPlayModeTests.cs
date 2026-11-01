using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private void SettlementField07(string name,object value)=>typeof(GameApp).GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).SetValue(app,value);
        private void SettlementTick07(float delta)=>typeof(GameApp).GetMethod("TickSettlementReveal07",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,new object[]{delta});
        private void PrepareRealSettlement07(int font,bool fastMode=false,bool reduce=false,bool instantMode=false)
        {
            SettlementField07("textScale07",font);SettlementField07("fast",fastMode);SettlementField07("reduced",reduce);SettlementField07("instant07",instantMode);UiKit.FontScale=font/100f;UiKit.ReducedMotion=reduce;
            var scene=InformationScene07(Art07FixtureKind.MaximumTable);scene.target="1";scene.remainingSubmissions=3;
            ApplyInformationScene07(scene);Click("Submit");app.SkipVisual();
            Assert.That(app.table.IsTransitioning,Is.False,"Skip must finish the post-submit return to the pool before EndStage is clickable.");
            Click("EndStage");
            Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));
        }
        private void AssertSettlementInk07(TextMeshProUGUI text,Rect area)
        {
            text.ForceMeshUpdate();Assert.That(text.textInfo.characterCount,Is.GreaterThan(0),text.name);
            foreach(var character in text.textInfo.characterInfo.Take(text.textInfo.characterCount))
            {
                if(char.IsWhiteSpace(character.character)||character.character=='\n')continue;
                Assert.That(character.isVisible,Is.True,text.name+" missing visible glyph "+character.character);
                var lo=RectTransformUtility.WorldToScreenPoint(null,text.transform.TransformPoint(character.bottomLeft));
                var hi=RectTransformUtility.WorldToScreenPoint(null,text.transform.TransformPoint(character.topRight));
                Assert.That(lo.x,Is.GreaterThanOrEqualTo(area.xMin-1),text.name);Assert.That(lo.y,Is.GreaterThanOrEqualTo(area.yMin-1),text.name);
                Assert.That(hi.x,Is.LessThanOrEqualTo(area.xMax+1),text.name);Assert.That(hi.y,Is.LessThanOrEqualTo(area.yMax+1),text.name);
            }
        }
        [UnityTest] public IEnumerator PLAY_ART07_SETTLE_PaperCashStaggerPauseRedrawSkipAndFontsUseOneCommittedReceipt()
        {
            StartRun("70905");bool enabled=app.enabled;app.enabled=false;
            try
            {
                foreach(int font in new[]{100,125,150})
                {
                    PrepareRealSettlement07(font);var motion=app.GetComponent<Art07SettlementReveal>();Assert.That(motion,Is.Not.Null);Assert.That(motion.IsPlaying,Is.True);
                    string state=StateJson();var disk=File.ReadAllBytes(app.SaveLocation);int begins=motion.Begins;
                    var paper=FindActive<RectTransform>("Rewards");Assert.That(paper.sizeDelta,Is.EqualTo(new Vector2(880,744)));
                    Assert.That(FindActive<UnityEngine.UI.Image>("SettlementScrim07").color.a,Is.EqualTo(.35f).Within(.001));
                    var rows=new[]{"SettlementBaseReward","SettlementUnusedReward","SettlementInterest"}.Select(FindActive<TextMeshProUGUI>).ToArray();
                    Assert.That(rows.All(t=>t.GetComponent<CanvasGroup>().alpha==0),Is.True);
                    yield return app.StartCoroutine(CaptureEdge07("settlement-font"+font+"-ms000"));
                    SettlementTick07(.18f);Assert.That(paper.localScale,Is.EqualTo(Vector3.one));
                    yield return app.StartCoroutine(CaptureEdge07("settlement-font"+font+"-ms180"));
                    SettlementTick07(.05f);Assert.That(rows[0].GetComponent<CanvasGroup>().alpha,Is.EqualTo(.5f).Within(.01));Assert.That(rows[1].GetComponent<CanvasGroup>().alpha,Is.Zero);
                    app.Refresh();Assert.That(motion.Begins,Is.EqualTo(begins));Assert.That(motion.Elapsed,Is.EqualTo(.23f).Within(.001));
                    Click("Pause");SettlementTick07(.5f);Assert.That(motion.Elapsed,Is.EqualTo(.23f).Within(.001));Click("Resume");
                    foreach(float mark in new[]{.28f,.38f,.48f,.60f,.74f})
                    {
                        SettlementTick07(mark-motion.Elapsed);Assert.That(motion.ActiveCoinCount,Is.InRange(0,5));
                        yield return app.StartCoroutine(CaptureEdge07("settlement-font"+font+"-ms"+Mathf.RoundToInt(mark*1000).ToString("D3")));
                        Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                    }
                    Assert.That(motion.IsPlaying,Is.False);Assert.That(motion.ActiveCoinCount,Is.Zero);
                    foreach(var row in rows)AssertSettlementInk07(row,V06ScreenRect((RectTransform)row.transform.parent.parent));
                    AssertSettlementInk07(FindActive<TextMeshProUGUI>("Cleared"),V06ScreenRect(paper));
                    app.Refresh();Assert.That(motion.Begins,Is.EqualTo(begins));Assert.That(motion.IsPlaying,Is.False);
                    long gold=app.Session.State.gold;int receiptCount=app.Session.State.processedReceipts.Count;Click("SettlementContinue");
                    Assert.That(app.Session.State.gold,Is.EqualTo(gold));Assert.That(app.Session.State.processedReceipts.Count,Is.EqualTo(receiptCount+1));
                }
                PrepareRealSettlement07(100);var current=app.GetComponent<Art07SettlementReveal>();string accepted=StateJson();var bytes=File.ReadAllBytes(app.SaveLocation);
                ExecuteEvents.Execute(FindActive<RectTransform>("SettlementScrim07").gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.pointerClickHandler);
                Assert.That(current.IsPlaying,Is.False);Assert.That(StateJson(),Is.EqualTo(accepted));CollectionAssert.AreEqual(bytes,File.ReadAllBytes(app.SaveLocation));
                PrepareRealSettlement07(100);Click("SkipSettlementReveal07");Assert.That(current.IsPlaying,Is.False);
                PrepareRealSettlement07(100);current.SendMessage("OnApplicationFocus",false);Assert.That(current.IsPlaying,Is.False);Assert.That(current.ActiveCoinCount,Is.Zero);
                PrepareRealSettlement07(100);Click("Pause");SettlementField07("reduced",true);SettlementTick07(.016f);
                Assert.That(current.IsPlaying,Is.False,"Changing reduced motion while paused must finish existing travel without losing the committed result.");Click("Resume");
            }
            finally{app.enabled=enabled;UiKit.ReducedMotion=false;}
        }
        [UnityTest] public IEnumerator PLAY_ART07_SETTLE_FastReducedInstantAndReopenNeverRewardOrReplay()
        {
            StartRun("70906");bool enabled=app.enabled;app.enabled=false;
            try
            {
                foreach(string mode in new[]{"Fast","Reduced","Instant"})
                {
                    PrepareRealSettlement07(150,mode=="Fast",mode=="Reduced",mode=="Instant");var motion=app.GetComponent<Art07SettlementReveal>();
                    string state=StateJson();var disk=File.ReadAllBytes(app.SaveLocation);
                    if(mode=="Fast"){Assert.That(motion.Duration,Is.LessThanOrEqualTo(.8f));SettlementTick07(.37f);}
                    else if(mode=="Reduced")
                    {
                        Assert.That(FindActive<RectTransform>("Rewards").localScale,Is.EqualTo(Vector3.one));SettlementTick07(.06f);Assert.That(motion.ActiveCoinCount,Is.Zero);
                        yield return app.StartCoroutine(CaptureEdge07("settlement-reduced-font150-ms060"));SettlementTick07(.06f);
                    }
                    Assert.That(motion.IsPlaying,Is.False);Assert.That(motion.ActiveCoinCount,Is.Zero);
                    yield return app.StartCoroutine(CaptureEdge07("settlement-"+mode.ToLowerInvariant()+"-font150-final"));
                    int begins=motion.Begins;Click("Pause");Click("MainMenu");
                    Assert.That(app.ResumeDebugWorkspace(),Is.True);if(HasActive("RunDebugScene"))Click("RunDebugScene");else Click("CloseDialog");
                    Assert.That(motion.IsPlaying,Is.False);Assert.That(motion.Begins,Is.EqualTo(begins));
                    Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                }
            }
            finally{app.enabled=enabled;UiKit.ReducedMotion=false;}
        }
        [UnityTest] public IEnumerator PLAY_ART07_LEGACY_ExactVersionReadOnlyDetailsAndNewRunCancellationKeepOldBytes()
        {
            string file=app.SaveLocation;byte[] old=System.Text.Encoding.UTF8.GetBytes("{\"schema\":3,\"config\":\"p0.5.0\",\"payload\":\"{}\",\"sha256\":\"legacy-fixture\"}");
            File.WriteAllBytes(file,old);yield return LoadBootstrap();
            Assert.That(app.Session.State,Is.Null);Assert.That(InformationText07("LegacyVersions07Body"),Does.Contain("p0.5.0").And.Contain("schema 3").And.Contain("p0.6.0").And.Contain("schema 4"));
            var archives=Directory.GetFiles(saveDirectory,"*.legacy-schema3-*");Assert.That(archives.Length,Is.EqualTo(1));CollectionAssert.AreEqual(old,File.ReadAllBytes(archives[0]));
            foreach(int font in new[]{100,125,150})
            {
                SettlementField07("textScale07",font);UiKit.FontScale=font/100f;
                typeof(GameApp).GetMethod("OpenIncompatibleSave07",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,null);Canvas.ForceUpdateCanvases();
                var version=FindActive<TextMeshProUGUI>("LegacyVersions07Body");var content=(RectTransform)version.transform.parent;
                var scroll=content.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                content.anchoredPosition=new Vector2(0,Mathf.Clamp(-version.rectTransform.anchoredPosition.y-48,0,Mathf.Max(0,content.rect.height-scroll.viewport.rect.height)));
                Canvas.ForceUpdateCanvases();AssertSettlementInk07(version,V06ScreenRect(scroll.viewport));
                AssertSettlementInk07(Button("LegacyNewRun07").GetComponentInChildren<TextMeshProUGUI>(),V06ScreenRect((RectTransform)Button("LegacyNewRun07").transform));
                yield return app.StartCoroutine(CaptureEdge07("legacy-specific-versions-font"+font));
                CollectionAssert.AreEqual(old,File.ReadAllBytes(file));Assert.That(app.Session.State,Is.Null);
            }
            string clipboard=GUIUtility.systemCopyBuffer;
            try{Click("CopyLoadIssue07");Assert.That(GUIUtility.systemCopyBuffer,Does.Contain("p0.5.0").And.Contain("schema 3"));}
            finally{GUIUtility.systemCopyBuffer=clipboard;}
            Click("LegacyNewRun07");Assert.That(app.Session.State,Is.Null);CollectionAssert.AreEqual(old,File.ReadAllBytes(file));
            FindActive<TMP_InputField>("SeedInput").text="123";Click("CloseDialog");Assert.That(app.Session.State,Is.Null);CollectionAssert.AreEqual(old,File.ReadAllBytes(file));
            Assert.That(Button("ContinueRun").interactable,Is.True);Assert.That(InformationButton07("ContinueRun"),Is.EqualTo("旧版本存档"));Click("ContinueRun");
            Assert.That(HasActive("LegacyNewRun07"),Is.True);Assert.That(Directory.GetFiles(saveDirectory,"*.legacy-schema3-*").Length,Is.EqualTo(1));
            CollectionAssert.AreEqual(old,File.ReadAllBytes(file));CollectionAssert.AreEqual(old,File.ReadAllBytes(archives[0]));
        }
    }
}
