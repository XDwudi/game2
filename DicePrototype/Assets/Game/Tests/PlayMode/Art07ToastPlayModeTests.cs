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
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest] public IEnumerator PLAY_ART07_ToastActualPaperGlyphsAndMainActionsAtThreeFontsNormalReduced()
        {
            Assert.That(Screen.width==1280&&Screen.height==720||Screen.width==1920&&Screen.height==1080,Is.True,"Run both real viewport configurations.");
            yield return ExtraBegin07();
            foreach(bool reduce in new[]{false,true})
            {
                if(PlayerPrefs.GetInt("reduced",0)!=(reduce?1:0))
                {
                    if(app.Session.State==null)yield return ExtraPress07("MenuSettings");else{yield return ExtraPress07("Pause");yield return ExtraPress07("Settings");}
                    yield return ExtraPress07("SettingsTab2");yield return ExtraPress07("Reduced");yield return ExtraPress07("SettingsDone");
                }
                foreach(int font in new[]{100,125,150})
                {
                    yield return CardSetFont07(font);if(app.Session.State==null)StartRun("70829");
                    foreach(var kind in new[]{Art07FixtureKind.MaximumPool,Art07FixtureKind.MaximumTable,Art07FixtureKind.Debug32})
                    {
                        ApplyInformationScene07(InformationScene07(kind));yield return ExtraIdle07();
                        string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
                        var first=app.Send(new SessionCommand{kind=CommandKind.ConfirmLocks});var second=app.Send(new SessionCommand{kind=CommandKind.Reorder,dieIds=Array.Empty<string>()});
                        Assert.That(first.accepted||second.accepted,Is.False,"Two actual rejection paths must exercise the notification transport.");
                        yield return null;
                        var labels=ToastLabels07();Assert.That(labels.Length,Is.InRange(1,2));
                        Assert.That(labels.Any(t=>t.text==first.error),Is.True,"The first rejected action is immediately explained.");
                        foreach(var label in labels)ToastBounds07(label,kind==Art07FixtureKind.Debug32);
                        yield return ScoreLabelGpuCapture07(labels);
                        yield return ExtraCapture07("toast-"+kind+"-"+font+"-"+(reduce?"reduced":"normal"),"UI-toast","Real rejection text, actual TMP/GPU glyphs and visible actions; diagnostic overflow may temporarily cover only its score preview.");
                        Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                    }
                    yield return TransactionShop07(s=>true);yield return ExtraIdle07();
                    string shopState=StateJson();byte[] shopDisk=File.ReadAllBytes(app.SaveLocation);long shopSequence=app.Session.ActionSequence;
                    var buy=app.Send(new SessionCommand{kind=CommandKind.Buy,offerId="missing-toast-probe"});
                    var sell=app.Send(new SessionCommand{kind=CommandKind.Sell,itemInstanceId="missing-toast-probe"});
                    Assert.That(buy.accepted||sell.accepted,Is.False);yield return null;
                    var shopLabels=ToastLabels07();Assert.That(shopLabels.Length,Is.EqualTo(2),"The shop must show both complete errors.\n"+string.Join("\n",app.NoticeBlockerReport073(49*UiKit.FontScale)));
                    CollectionAssert.AreEquivalent(new[]{buy.error,sell.error},shopLabels.Select(t=>t.text));
                    foreach(var label in shopLabels)ToastBounds07(label,false);
                    yield return ScoreLabelGpuCapture07(shopLabels);
                    yield return ExtraCapture07("toast-shop-"+font+"-"+(reduce?"reduced":"normal"),"UI-toast","Two actual shop rejection reasons in the bottom band; LeaveShop and refresh remain unobscured.");
                    Assert.That(StateJson(),Is.EqualTo(shopState));Assert.That(app.Session.ActionSequence,Is.EqualTo(shopSequence));CollectionAssert.AreEqual(shopDisk,File.ReadAllBytes(app.SaveLocation));
                }
            }
            ExtraFinish07();
        }
        [UnityTest] public IEnumerator PLAY_ART07_ToastPausePreservesVisibleTimeAndLongErrorsKeepCompleteDetails()
        {
            yield return ExtraBegin07();yield return CardSetFont07(150);StartRun("70830");ApplyInformationScene07(InformationScene07(Art07FixtureKind.MaximumTable));yield return ExtraIdle07();
            string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
            var rejected=app.Send(new SessionCommand{kind=CommandKind.ConfirmLocks});Assert.That(rejected.accepted,Is.False);yield return null;
            Assert.That(ToastLabels07().Single().text,Is.EqualTo(rejected.error));yield return new WaitForSecondsRealtime(.3f);
            yield return LegacyClick07("Pause");yield return null;
            Assert.That(ToastLabels07(),Is.Empty,"Pause hides the toast rather than leaving paper above the modal.");
            yield return new WaitForSecondsRealtime(2.4f);
            yield return LegacyClick07("CloseDialog");yield return null;
            Assert.That(ToastLabels07().Single().text,Is.EqualTo(rejected.error),"Hidden modal time cannot consume the message's two visible seconds.");
            Assert.That(ToastLabels07().Single().GetComponentInParent<CanvasGroup>().alpha,Is.EqualTo(1).Within(.01f));
            yield return ExtraCapture07("toast-resumed-after-pause","UI-toast","Same actual rejection remains fully visible after a pause longer than its lifetime.");
            yield return new WaitForSecondsRealtime(2.3f);Assert.That(ToastLabels07(),Is.Empty,"The visible queue must actually expire.");
            Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            // UI-only diagnostic text exercises the ordinary-error transport. It
            // must remain a toast until the player explicitly requests the full copy.
            const string longText="输入不合法：完整诊断说明包含本次操作的对象、目标、范围和失败原因，所有内容必须保留供玩家阅读，不能省略或缩小字号。";
            foreach(bool reduce in new[]{false,true})
            {
                if(PlayerPrefs.GetInt("reduced",0)!=(reduce?1:0))
                {
                    yield return ExtraPress07("Pause");yield return ExtraPress07("Settings");yield return ExtraPress07("SettingsTab2");yield return ExtraPress07("Reduced");yield return ExtraPress07("SettingsDone");
                }
                foreach(int font in new[]{100,125,150})
                {
                    yield return CardSetFont07(font);
                    foreach(var kind in new[]{Art07FixtureKind.MaximumPool,Art07FixtureKind.MaximumTable,Art07FixtureKind.Debug32})
                    {
                        ApplyInformationScene07(InformationScene07(kind));yield return ExtraIdle07();
                        yield return ToastLongDetails07("toast-long-"+kind+"-"+font+"-"+(reduce?"reduced":"normal"),longText,kind==Art07FixtureKind.Debug32);
                    }
                    yield return TransactionShop07(s=>true);yield return ExtraIdle07();
                    yield return ToastLongDetails07("toast-long-shop-"+font+"-"+(reduce?"reduced":"normal"),longText,false);
                }
            }
            ExtraFinish07();
        }
        private IEnumerator ToastLongDetails07(string id,string complete,bool diagnostic)
        {
            string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
            var queue=typeof(GameApp).GetMethod("QueueNotice07",BindingFlags.Instance|BindingFlags.NonPublic);
            queue.Invoke(app,new object[]{"",false});queue.Invoke(app,new object[]{complete,true});app.Refresh();yield return null;
            Assert.That(HasActive("Dialog"),Is.False,"An ordinary long reason must never open a modal automatically.");
            Assert.That(HasActive("NoticeDetailBody"),Is.False);
            var summary=ToastLabels07().Single();Assert.That(summary.text,Does.StartWith("输入不合法："));Assert.That(summary.text,Does.EndWith("…"),"Shortening is explicit beside a full-text affordance.");
            Assert.That(summary.fontSize,Is.EqualTo(20*UiKit.FontScale));ToastBounds07(summary,diagnostic);
            var details=Button("ToastDetails07");Rect hit=V06ScreenRect((RectTransform)details.transform);
            Assert.That(hit.width,Is.GreaterThanOrEqualTo(39.99f));Assert.That(hit.height,Is.GreaterThanOrEqualTo(39.99f));
            Assert.That(ExtraHit07(details.gameObject,hit.center),Is.True,"The real GraphicRaycaster must reach the details button.");
            Assert.That(V06ScreenRect(summary.rectTransform).Overlaps(hit),Is.False,"The summary and details target occupy separate columns.");
            var detailLabel=details.GetComponentInChildren<TextMeshProUGUI>();detailLabel.ForceMeshUpdate();
            Assert.That(detailLabel.fontSize,Is.EqualTo(20*UiKit.FontScale));Assert.That(detailLabel.isTextOverflowing||detailLabel.isTextTruncated,Is.False);
            foreach(var glyph in ScoreGlyphRects07(detailLabel))Assert.That(ScoreContains07(hit,glyph),Is.True,"Details glyph remains inside its target.");
            details.Select();
            // Let Selectable's real highlight tint finish before comparing hidden
            // backgrounds. The pixel-noise threshold remains unchanged.
            yield return new WaitForSecondsRealtime(Mathf.Max(.12f,details.colors.fadeDuration+.05f));
            var focus=(RectTransform)details.transform.Find("Focus");Assert.That(focus.gameObject.activeInHierarchy,Is.True);
            Assert.That(ScoreContains07(hit,V06ScreenRect(focus)),Is.True,"Keyboard focus must remain inside the details target, clear of multiplier buttons.");
            yield return ScoreLabelGpuCapture07(new[]{summary,detailLabel});
            yield return ExtraCapture07(id+"-summary","UI-toast","Ordinary long error is a 480-wide summary and explicit details target; no automatic modal, unchanged rule state.");
            Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            yield return ExtraPress07("ToastDetails07");yield return ExtraIdle07();
            Assert.That(HasActive("Dialog"),Is.True,"Only the explicit click opens the existing full explanation.");
            var body=FindActive<TextMeshProUGUI>("NoticeDetailBody");Assert.That(body.text,Is.EqualTo(complete));Assert.That(ToastLabels07(),Is.Empty);
            Assert.That(body.isTextOverflowing||body.isTextTruncated,Is.False);yield return ScoreLabelGpuCapture07(new[]{body});
            yield return ExtraCapture07(id+"-details","UI-toast","Player-requested existing scroll details preserves the exact complete ordinary error.");
            yield return ExtraPress07("CloseDialog");yield return null;Assert.That(HasActive("Dialog"),Is.False);
            Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
        }
        private TextMeshProUGUI[] ToastLabels07()
        {
            var layer=app.canvas.transform.Find("ToastLayer07");
            return layer==null||!layer.gameObject.activeInHierarchy?Array.Empty<TextMeshProUGUI>():layer.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.name=="Text").ToArray();
        }
        private void ToastBounds07(TextMeshProUGUI label,bool allowDiagnosticPreview)
        {
            label.ForceMeshUpdate();var panel=(RectTransform)label.transform.parent;Rect paper=V06ScreenRect(panel);
            Assert.That(panel.rect.width,Is.EqualTo(480));Assert.That(label.textInfo.lineCount,Is.InRange(1,2));Assert.That(label.isTextOverflowing||label.isTextTruncated,Is.False);
            foreach(var glyph in ScoreGlyphRects07(label))Assert.That(ScoreContains07(paper,glyph),Is.True,label.text);
            Assert.That(ScoreContains07(new Rect(0,0,Screen.width,Screen.height),paper),Is.True);
            string[] actions=app.Session.State.phase=="Shop"?new[]{"LeaveShop","RefreshControllers","Pause","Handbook","Study"}:new[]{"Roll","Confirm","Clear","Submit","EndStage","Pause","Handbook"};
            if(app.Session.State.phase!="Shop"&&HasActive("Factor0"))actions=actions.Concat(Enumerable.Range(0,6).Select(i=>"Factor"+i)).ToArray();
            foreach(string name in actions)
            {
                var button=Button(name);Rect action=V06ScreenRect((RectTransform)button.transform);
                Assert.That(paper.Overlaps(action),Is.False,"Toast must not cover "+name);
                Assert.That(ExtraHit07(button.gameObject,action.center),Is.True,"Toast must never intercept "+name);
            }
            if(!allowDiagnosticPreview)foreach(var pointer in app.canvas.GetComponentsInChildren<DiePointer>(false))
                Assert.That(paper.Overlaps(V06ScreenRect((RectTransform)pointer.transform)),Is.False,"Toast covers actual die "+pointer.name);
        }
    }
}
