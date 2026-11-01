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
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest] public IEnumerator PLAY_ART07_ScoreDrawerThreeFontsMapEverySourceAndWheelReachEveryGlyph()
        {
            yield return ExtraBegin07();
            foreach(int font in new[]{100,125,150})
            {
                yield return CardSetFont07(font);if(app.Session.State==null)StartRun("70931");
                foreach(var kind in new[]{Art07FixtureKind.MaximumTable,Art07FixtureKind.Debug32})
                {
                    var scene=InformationScene07(kind);scene.randomOverrides.Clear();
                    // A legal pending selection is outside the frozen submission.
                    // Opening, copying and closing must preserve it as well as S.
                    string pending=null;
                    if(kind==Art07FixtureKind.MaximumTable)
                    {
                        pending=scene.orderedLockedIds.Last();scene.orderedLockedIds.Remove(pending);
                        scene.currentResults.Single(d=>d.dieId==pending).isLocked=false;
                    }
                    ApplyInformationScene07(scene);if(pending!=null){app.Session.TogglePending(pending);app.Refresh();}
                    yield return ExtraIdle07();
                    var snapshot=app.Session.PreviewSnapshot();var score=app.Session.PreviewScoreRange().minimum;
                    string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);var selections=app.Session.PendingIds.ToArray();var locked=app.Session.State.hand.orderedLockedIds.ToArray();
                    yield return ExtraPress07("Factor4");
                    yield return DrawerWalk07(kind+"-font"+font,snapshot,score);
                    yield return ExtraPress07("CloseDialog");
                    Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                    CollectionAssert.AreEqual(selections,app.Session.PendingIds);CollectionAssert.AreEqual(locked,app.Session.State.hand.orderedLockedIds);
                    app.Session.ClearPending();app.Refresh();
                }
            }
            ExtraFinish07();
        }

        [UnityTest] public IEnumerator PLAY_ART07_ScoreDrawerC53BranchAndHistoricalRowsKeepFrozenExactValuesAndCopy()
        {
            yield return ExtraBegin07();yield return CardSetFont07(150);StartRun("70932");
            var scene=InformationScene07(Art07FixtureKind.SharedHalf);scene.randomOverrides.Clear();ApplyInformationScene07(scene);yield return ExtraIdle07();
            var frozen=app.Session.PreviewSnapshot();string owner=app.Session.PreviewScoreRange().randomOwnerIds.Single();
            int q=app.Config.Controller("C53").parameters.factorUnits2.Last();var branch=frozen.Clone();
            branch.resolvedRandomChoices=new[]{new RandomMultiplierChoice{ownerInstanceId=owner,units2=q}};
            var exact=ScoreCalculator.Preview(branch,app.Config);Assert.That(exact.isRandom,Is.False);
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            yield return ExtraPress07("RandomPreview");yield return ExtraPress07("RandomBranch8");
            yield return DrawerWalk07("C53-shared-q-last-branch",branch,exact.minimum);
            yield return ExtraPress07("FullScoreDetails");
            Assert.That(FindActive<TextMeshProUGUI>("Formula").text,Does.Contain("控制器 "+exact.minimum.controllerMultiplierNumerator+"/"+exact.minimum.controllerMultiplierDenominator));
            yield return ExtraPress07("ScoreDrawerBack");yield return ExtraPress07("CloseDialog");
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            scene=InformationScene07(Art07FixtureKind.PermanentPost);ApplyInformationScene07(scene);yield return ExtraIdle07();
            yield return ExtraPress07("Submit",true);app.SkipVisual();yield return ExtraIdle07();
            var history=app.Session.State.lastSubmission;Assert.That(history,Is.Not.Null);
            yield return ExtraPress07("PreviousSubmission");
            Assert.That(FindActive<TextMeshProUGUI>("Triggered").text,Does.Contain("实际提交后效果：").And.Contain("永久点数：").And.Contain("本次计分后自毁"));
            yield return DrawerWalk07("historical-permanent",history.snapshot,history.breakdown);
            yield return ExtraPress07("CloseDialog");ExtraFinish07();
        }

        private IEnumerator DrawerWalk07(string id,SubmissionSnapshot snapshot,ScoreBreakdown score)
        {
            var dialog=FindActive<RectTransform>("Dialog");bool responsive=Screen.safeArea.width<=1366&&UiKit.FontScale>=1.25f;
            Assert.That(dialog.GetComponent<Art07ScoreDrawerMotion>().IsPlaying,Is.False);
            Vector2 expectedPosition=responsive?new Vector2(352,-304):new Vector2(496,-400);
            TestContext.WriteLine("Settled drawer anchor: "+dialog.anchoredPosition.x.ToString("R")+", "+dialog.anchoredPosition.y.ToString("R"));
            Assert.That(Vector2.Distance(dialog.anchoredPosition,expectedPosition),Is.LessThanOrEqualTo(.001f),"RectTransform float placement precision; less than 0.001 design pixel.");
            Assert.That(dialog.sizeDelta,Is.EqualTo(responsive?new Vector2(1536,632):new Vector2(1280,520)));
            Assert.That(dialog.GetComponent<Art07PanelMotion>().ExitDuration,Is.EqualTo(.10f));
            Assert.That(dialog.GetComponent<Art07ScoreDrawerMotion>().IsPlaying,Is.False);
            var formula=FindActive<TextMeshProUGUI>("Formula");var scroll=formula.GetComponentInParent<UnityEngine.UI.ScrollRect>();Assert.That(scroll,Is.Not.Null);
            Assert.That(scroll.horizontal,Is.False);Assert.That(scroll.vertical,Is.True);
            Assert.That(formula.text,Does.Contain("控制器 "+score.controllerMultiplierNumerator+"/"+score.controllerMultiplierDenominator));
            Assert.That(formula.text,Does.EndWith("= "+System.Numerics.BigInteger.Parse(score.score).ToString("N0",System.Globalization.CultureInfo.InvariantCulture)));
            var trace=ScoreExplanation.Build(snapshot,score,app.Config);var expected=new List<string>();
            void CheckStep(string key,ScoreStep step)
            {
                expected.Add("ScoreRow_"+key);var row=FindActive<RectTransform>("ScoreRow_"+key);
                string source=row.Find("Source").GetComponent<TextMeshProUGUI>().text;
                string calculation=row.Find("Calculation").GetComponent<TextMeshProUGUI>().text;
                string result=row.Find("Result").GetComponent<TextMeshProUGUI>().text;
                Assert.That(calculation,Is.EqualTo(step.operation=="floor"?step.before+" → 向下取整一次":step.before+" "+step.operation+" "+step.operand));
                Assert.That(result,Is.EqualTo(step.after),key+" must show exact reduced-rational result, never HUD approximation");
                if(step.sourceId!=null)Assert.That(source,Does.Contain(app.Config.Controller(step.sourceId).displayName));
                if(!string.IsNullOrEmpty(step.emitterId))Assert.That(source,Does.Contain(step.emitterId));
                if(!string.IsNullOrEmpty(step.stateOwnerId)&&step.stateOwnerId!=step.emitterId)Assert.That(source,Does.Contain("→ "+step.stateOwnerId));
                Assert.That(source,Is.Not.Empty);
            }
            for(int i=0;i<snapshot.dice.Length;i++)
            {
                string key="Die"+i;expected.Add("ScoreRow_"+key);var row=FindActive<RectTransform>("ScoreRow_"+key);var die=snapshot.dice[i];var face=die.permanentFacesCopy.Single(f=>f.faceId==die.faceId);
                Assert.That(row.Find("Source").GetComponent<TextMeshProUGUI>().text,Is.EqualTo((i+1)+". "+die.dieId+" / "+die.faceId));
                Assert.That(row.Find("Calculation").GetComponent<TextMeshProUGUI>().text,Is.EqualTo($"永久 {face.permanentPip} → 当前 {die.currentPip} → 有效 {score.effectivePips[i]}\n有效颜色 {(score.effectiveColors[i]=="red"?"红 R":"蓝 B")}\n永久六面和 {die.permanentSixFaceSum}"));
                Assert.That(row.Find("Result").GetComponent<TextMeshProUGUI>().text,Is.EqualTo("本骰点数贡献\n"+score.pipContributions[i]));
                for(int j=0;j<trace.dice[i].steps.Length;j++)CheckStep("Die"+i+"Step"+j,trace.dice[i].steps[j]);
            }
            for(int i=0;i<trace.global.Length;i++)CheckStep("Global"+i,trace.global[i]);
            var rows=scroll.content.GetComponentsInChildren<RectTransform>(false).Where(r=>r.name.StartsWith("ScoreRow_",StringComparison.Ordinal)).OrderBy(r=>-r.anchoredPosition.y).ToArray();
            CollectionAssert.AreEqual(expected,rows.Select(r=>r.name).ToArray());
            foreach(var row in rows)
            {
                var source=V06ScreenRect((RectTransform)row.Find("Source"));var calculation=V06ScreenRect((RectTransform)row.Find("Calculation"));var result=V06ScreenRect((RectTransform)row.Find("Result"));
                Assert.That(source.xMax,Is.LessThan(calculation.xMin));Assert.That(calculation.xMax,Is.LessThan(result.xMin));
                Assert.That(Mathf.Abs(source.yMax-calculation.yMax),Is.LessThanOrEqualTo(.01f));Assert.That(Mathf.Abs(source.yMax-result.yMax),Is.LessThanOrEqualTo(.01f));
            }
            var labels=scroll.content.GetComponentsInChildren<TextMeshProUGUI>(false);
            foreach(var text in labels)
            {
                text.ForceMeshUpdate();Assert.That(text.enableAutoSizing,Is.False);Assert.That(text.isTextTruncated,Is.False);Assert.That(text.isTextOverflowing,Is.False);
                Assert.That(text.rectTransform.rect.height,Is.GreaterThanOrEqualTo(text.GetPreferredValues(text.text,text.rectTransform.rect.width,float.PositiveInfinity).y-.1f));
                foreach(var glyph in ScoreGlyphRects07(text))Assert.That(ScoreContains07(V06ScreenRect(text.rectTransform),glyph),Is.True,text.name+" glyph leaves its actual cell");
            }
            foreach(string name in new[]{"CloseDialog","CopyExactScore","FullScoreDetails"})
            {
                var rect=V06ScreenRect((RectTransform)Button(name).transform);Assert.That(rect.width,Is.GreaterThanOrEqualTo(40));Assert.That(rect.height,Is.GreaterThanOrEqualTo(40));
            }
            string state=StateJson(),clipboard=GUIUtility.systemCopyBuffer;byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
            try{yield return ExtraPress07("CopyExactScore");Assert.That(GUIUtility.systemCopyBuffer,Is.EqualTo(formula.text));}
            finally{GUIUtility.systemCopyBuffer=clipboard;}
            var seen=labels.ToDictionary(t=>t,t=>new HashSet<int>());var wanted=labels.ToDictionary(t=>t,t=>Enumerable.Range(0,t.textInfo.characterCount).Where(i=>!char.IsWhiteSpace(t.textInfo.characterInfo[i].character)).ToArray());
            int budget=Mathf.CeilToInt(scroll.content.rect.height/scroll.scrollSensitivity)+4;bool middle=false;int captures=0,events=0;
            for(int turn=0;turn<=budget;turn++)
            {
                yield return null;Canvas.ForceUpdateCanvases();Rect viewport=V06ScreenRect(scroll.viewport);
                var ready=new Dictionary<TextMeshProUGUI,int[]>();
                foreach(var text in labels)
                {
                    var inside=wanted[text].Where(i=>ScoreContains07(viewport,BreakdownGlyphRect07(text,i))).ToArray();
                    foreach(int index in inside)seen[text].Add(index);if(inside.Length>0)ready.Add(text,inside);
                }
                bool all=labels.All(t=>seen[t].Count==wanted[t].Length);
                bool capture=turn==0||!middle&&scroll.verticalNormalizedPosition<=.5f||all;
                if(capture&&ready.Count>0)
                {
                    if(scroll.verticalNormalizedPosition<=.5f)middle=true;
                    yield return BreakdownGpuCapture07(ready);
                    yield return ExtraCapture07(id+"-drawer-page"+captures++,"S07","Three real columns: exact source/operation/result; first/middle/final visible page GPU samples. Every glyph is reached by real wheel traversal.");
                }
                Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                if(all)break;Assert.That(turn,Is.LessThan(budget),"All three-column glyphs must become fully visible by wheel.");
                float prior=scroll.content.anchoredPosition.y;
                var pointer=new PointerEventData(EventSystem.current){position=viewport.center,scrollDelta=new Vector2(0,-1)};
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(pointer,hits);Assert.That(hits,Is.Not.Empty);
                Assert.That(hits[0].gameObject.GetComponentInParent<UnityEngine.UI.ScrollRect>(),Is.SameAs(scroll));
                ExecuteEvents.ExecuteHierarchy(hits[0].gameObject,pointer,ExecuteEvents.scrollHandler);events++;yield return null;Canvas.ForceUpdateCanvases();
                Assert.That(scroll.content.anchoredPosition.y,Is.GreaterThan(prior+.01f));
            }
            Assert.That(events,Is.GreaterThan(0));Assert.That(captures,Is.GreaterThanOrEqualTo(2));
            foreach(var text in labels)Assert.That(seen[text].Count,Is.EqualTo(wanted[text].Length));
            File.WriteAllText(Path.Combine(extra07.output,id+"-drawer-mapping.txt"),string.Join("\n",rows.Select(r=>r.name+" | "+r.Find("Source").GetComponent<TextMeshProUGUI>().text+" | "+r.Find("Calculation").GetComponent<TextMeshProUGUI>().text+" | "+r.Find("Result").GetComponent<TextMeshProUGUI>().text)));
        }
    }
}
