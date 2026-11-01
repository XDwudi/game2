using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using DiceCraft.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private PresentationPlayer QualifiedPlayer07()=>(PresentationPlayer)typeof(GameApp).GetField("presentationPlayer07",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(app);
        private Art07QualifiedStamp QualifiedMotion07()=>app.canvas.GetComponentInChildren<Art07QualifiedStamp>(true);
        private void QualifiedTick07(float seconds)=>typeof(GameApp).GetMethod("TickQualifiedStamp07",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,new object[]{seconds});
        private IEnumerator QualifiedRendered07(){yield return new WaitForEndOfFrame();Canvas.ForceUpdateCanvases();}
        private void QualifiedGeometry07(string context)
        {
            Canvas.ForceUpdateCanvases();var panel=FindActive<RectTransform>("Stats07");var panelRect=V06ScreenRect(panel);
            var texts=panel.GetComponentsInChildren<TextMeshProUGUI>(false);var ink=texts.ToDictionary(t=>t,t=>PostInkScreenRects07(t,true));
            foreach(var pair in ink)
            foreach(var glyph in pair.Value)
            {
                Assert.That(panelRect.Contains(glyph.min)&&panelRect.Contains(glyph.max),Is.True,context+" outside stats: "+pair.Key.name);
                foreach(var other in ink.Where(k=>k.Key!=pair.Key))foreach(var g in other.Value)
                    Assert.That(glyph.Overlaps(g),Is.False,context+" ink collision: "+pair.Key.name+" / "+other.Key.name);
                foreach(string name in new[]{"DebugWorkspace","PreviousSubmission","Handbook","Pause"})if(HasActive(name))
                    Assert.That(glyph.Overlaps(V06ScreenRect((RectTransform)Button(name).transform)),Is.False,context+" covers "+name);
            }
            var motion=QualifiedMotion07();if(motion==null)return;
            foreach(string name in new[]{"FullStamp","PersistentBadge"})
            {
                var root=motion.transform.Find(name) as RectTransform;if(root==null||!root.gameObject.activeInHierarchy)continue;
                var group=root.GetComponent<CanvasGroup>();if(group.alpha<=.01f)continue;
                foreach(var graphic in root.GetComponentsInChildren<UnityEngine.UI.Graphic>(false).Where(g=>!(g is TMP_Text)&&!(g is TMP_SubMeshUI)))
                    foreach(var entry in ink)foreach(var glyph in entry.Value)
                        Assert.That(V06ScreenRect(graphic.rectTransform).Overlaps(glyph),Is.False,context+" "+name+"/"+graphic.name+" covers "+entry.Key.name+" actual ink");
                foreach(var text in root.GetComponentsInChildren<TextMeshProUGUI>(false))
                {
                    bool fullyVisible=name=="PersistentBadge"||motion.Elapsed<=.28f;
                    var glyphs=PostInkScreenRects07(text,fullyVisible);var rect=V06ScreenRect(root);
                    foreach(var g in glyphs)Assert.That(rect.Contains(g.min)&&rect.Contains(g.max),Is.True,context+" stamp text outside paper");
                }
            }
        }
        private string QualifiedSample07(string context)
        {
            var motion=QualifiedMotion07();var full=motion.transform.Find("FullStamp") as RectTransform;var badge=motion.transform.Find("PersistentBadge") as RectTransform;
            return context+" frame="+Time.frameCount+" elapsed="+motion.Elapsed.ToString("R")+" busy="+app.IsBusy+" presenting="+app.PresentationPlaying07+
                " full="+full.anchoredPosition.ToString("F6")+" scale="+full.localScale.ToString("F6")+" alpha="+full.GetComponent<CanvasGroup>().alpha.ToString("R")+
                " badge="+badge.anchoredPosition.ToString("F6")+" scoreFrame="+FindActive<RectTransform>("StageScoreFrame").position.ToString("F6");
        }
        [UnityTest] public IEnumerator PLAY_ART07_QUALIFIED_StripKeepsEveryGlyphClearAndReceiptHandoffIsContinuous()
        {
            bool enabled=app.enabled;
            try
            {
                foreach(var scenario in new[]{new{font=100,last=false},new{font=125,last=false},new{font=150,last=false},new{font=150,last=true}})
                {
                    int font=scenario.font;string suffix=scenario.last?"-settlement":"";
                    app.enabled=enabled;FxScene07(s=>{s.target="1";if(scenario.last)s.remainingSubmissions=1;});FinalOptions07("Normal",false);
                    SettlementField07("textScale07",font);UiKit.FontScale=font/100f;app.Refresh();
                    yield return app.StartCoroutine(QualifiedRendered07());app.enabled=false;
                    Assert.That(app.Send(new SessionCommand{kind=CommandKind.Submit}).accepted,Is.True);
                    string state=StateJson();byte[] saved=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;
                    var player=QualifiedPlayer07();var target=player.Sequence.Beats.Single(b=>b.Phase==PresentationPhase.TargetState);
                    Assert.That(target.DurationMs,Is.EqualTo(280));player.Tick(target.StartMs/1000.0);
                    Assert.That(app.QualifiedStampPlaying07,Is.True);Assert.That(app.IsBusy,Is.True);
                    Assert.That(FindActive<TextMeshProUGUI>("StageScore").text.Replace(",",""),Is.EqualTo(app.Session.State.stage.score),"TargetState uses the committed receipt's cumulative score, while old hand budgets retain their original handoff.");
                    var motion=QualifiedMotion07();Assert.That(motion.Duration,Is.EqualTo(.48f).Within(.0001f));
                    var full=motion.transform.Find("FullStamp") as RectTransform;var title=full.Find("Title").GetComponent<TextMeshProUGUI>();
                    Assert.That(title.text,Is.EqualTo("本关目标已达成"));Assert.That(title.fontSize,Is.EqualTo(24*font/100f));
                    Vector3 stableFrame=FindActive<RectTransform>("StageScoreFrame").position;
                    var samples=new List<string>();bool handoff=false;
                    for(int ms=0;ms<=480;ms+=10)
                    {
                        if(ms>0)
                        {
                            Vector2 before=full.anchoredPosition;bool wasPlaying=player.IsPlaying;
                            player.Tick(.01);
                            if(wasPlaying&&!player.IsPlaying)
                            {
                                handoff=true;
                                Assert.That(Vector2.Distance(before,full.anchoredPosition),Is.LessThanOrEqualTo(.001f),"Receipt handoff must not move the visible stamp without advancing its clock.");
                                Assert.That(app.IsBusy,Is.False,"Only the 280ms receipt phase is blocking.");
                                Assert.That(motion.IsPlaying,Is.True,"The 200ms tail remains visible after input unlocks.");
                            }
                            QualifiedTick07(ms/1000f-motion.Elapsed);
                        }
                        yield return app.StartCoroutine(QualifiedRendered07());
                        QualifiedGeometry07("font"+font+suffix+" ms"+ms);
                        if(scenario.last&&!player.IsPlaying)Assert.That(HasActive("RollBudgetValue")||HasActive("SubmitBudgetValue")||HasActive("QualifiedReward0"),Is.False,"S09 must not show obsolete hand action budgets or duplicate the central settlement rewards.");
                        Assert.That(Vector3.Distance(stableFrame,FindActive<RectTransform>("StageScoreFrame").position),Is.LessThanOrEqualTo(.001f),"The reserved score frame must not jump when the detached before snapshot becomes the saved hand.");
                        AssertInteractionCheckpoint07(state,saved,seq);samples.Add(QualifiedSample07("font"+font+suffix+" ms"+ms));
                        if(new[]{0,100,270,280,380,480}.Contains(ms))yield return app.StartCoroutine(CaptureEdge07("S08-qualified-strip-font"+font+suffix+"-ms"+ms));
                    }
                    Assert.That(handoff,Is.True);Assert.That(motion.IsPlaying,Is.False);Assert.That(app.IsBusy,Is.False);
                    Assert.That(motion.Elapsed,Is.EqualTo(.48f).Within(.0001f));
                    File.WriteAllLines(Path.Combine(saveDirectory,"qualified-strip-font"+font+suffix+"-samples.txt"),samples);
                }
            }
            finally{app.SkipVisual();app.enabled=enabled;}
        }
        [UnityTest] public IEnumerator PLAY_ART07_QUALIFIED_RetargetDoesNotTeleportAndShortModesKeepSavedState()
        {
            bool enabled=app.enabled;
            try
            {
                foreach(string mode in new[]{"Normal","Fast","Reduced","Instant"})
                {
                    app.enabled=enabled;FxScene07(s=>s.target="1");FinalOptions07(mode=="Reduced"?"Normal":mode,mode=="Reduced");
                    SettlementField07("textScale07",150);UiKit.FontScale=1.5f;app.Refresh();yield return app.StartCoroutine(QualifiedRendered07());
                    app.enabled=false;Assert.That(app.Send(new SessionCommand{kind=CommandKind.Submit}).accepted,Is.True);
                    string state=StateJson();byte[] save=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
                    if(mode=="Instant")
                    {
                        Assert.That(app.PresentationPlaying07||app.QualifiedStampPlaying07||app.IsBusy,Is.False);
                        yield return app.StartCoroutine(QualifiedRendered07());QualifiedGeometry07(mode);
                        yield return app.StartCoroutine(CaptureEdge07("S08-qualified-strip-Instant"));AssertInteractionCheckpoint07(state,save,sequence);continue;
                    }
                    var player=QualifiedPlayer07();var target=player.Sequence.Beats.Single(b=>b.Phase==PresentationPhase.TargetState);player.Tick(target.StartMs/1000.0);
                    var motion=QualifiedMotion07();Assert.That(motion.Duration,Is.EqualTo(mode=="Normal"?.48f:.12f).Within(.0001f));
                    QualifiedTick07(mode=="Normal"?.3f:.04f);yield return app.StartCoroutine(QualifiedRendered07());QualifiedGeometry07(mode);
                    if(mode=="Normal")
                    {
                        // Exercise the real component's defensive retarget at nonzero alpha,
                        // then return to the actual header target without a zero-time jump.
                        var full=motion.transform.Find("FullStamp") as RectTransform;var badge=motion.transform.Find("PersistentBadge") as RectTransform;
                        Vector2 a=full.anchoredPosition,b=badge.anchoredPosition;
                        motion.SetPositions(a+new Vector2(11,7),b+new Vector2(9,-6));
                        Assert.That(Vector2.Distance(full.anchoredPosition,a),Is.LessThanOrEqualTo(.001f));
                        Assert.That(Vector2.Distance(badge.anchoredPosition,b),Is.LessThanOrEqualTo(.001f));
                        QualifiedTick07(0);Assert.That(Vector2.Distance(full.anchoredPosition,a),Is.LessThanOrEqualTo(.001f));
                    }
                    Click("Pause");float paused=motion.Elapsed;QualifiedTick07(.07f);Assert.That(motion.Elapsed,Is.EqualTo(paused));Click("Resume");
                    app.SkipVisual();yield return app.StartCoroutine(QualifiedRendered07());QualifiedGeometry07(mode+" skip");
                    Assert.That(app.PresentationPlaying07||app.QualifiedStampPlaying07||app.IsBusy,Is.False);AssertInteractionCheckpoint07(state,save,sequence);
                    yield return app.StartCoroutine(CaptureEdge07("S08-qualified-strip-"+mode+"-final"));
                }
            }
            finally{app.SkipVisual();app.enabled=enabled;}
        }
    }
}
