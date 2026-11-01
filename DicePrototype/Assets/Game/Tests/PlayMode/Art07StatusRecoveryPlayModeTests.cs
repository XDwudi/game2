using System.Collections;
using System.IO;
using System.Linq;
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
        [UnityTest] public IEnumerator PLAY_ART07_STATUS_ColdLoadDuringSubmissionShowsSavedFinalWithoutReplayingEffects()
        {
            StartRun("70901");Click("StartStage");Click("Roll");app.SkipVisual();ClickDie("D001");Click("Confirm");yield return WaitForIdle();Click("Submit");
            Assert.That(app.PresentationPlaying07,Is.True);
            Assert.That(app.Session.State.phase,Is.EqualTo("HandReady"));
            string before=StateJson(),score=app.Session.State.lastSubmission.breakdown.score;byte[] saved=File.ReadAllBytes(app.SaveLocation);
            foreach(int font in new[]{100,125,150})
            {
                PlayerPrefs.SetInt("art07.textScale",font);yield return LoadBootstrap();Click("ContinueRun");
                Assert.That(app.RecoveredPresentationResultVisible07,Is.True);
                Assert.That(app.PresentationPlaying07||app.IsBusy,Is.False,"Reload must show the saved result without replaying a receipt.");
                Assert.That(InformationText07("RecoveredScore07").Replace(",",""),Is.EqualTo(score));
                Assert.That(InformationText07("RecoveredCaption07"),Is.EqualTo("已恢复上一手结果"));
                yield return CaptureEdge07("S02-recovered-font"+font);
                Click("RecoveredDetails07");yield return null;Click("CloseDialog");app.Refresh();
                Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(saved,File.ReadAllBytes(app.SaveLocation));
                Assert.That(app.RecoveredPresentationResultVisible07,Is.True);
            }
            var rejected=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(rejected.accepted,Is.False);
            Assert.That(app.RecoveredPresentationResultVisible07,Is.True);
            Click("Roll");app.SkipVisual();Assert.That(app.RecoveredPresentationResultVisible07,Is.False);
        }
        [UnityTest] public IEnumerator PLAY_ART07_STATUS_ExcessPendingMarksOnlyOverflowAndCannotPartiallyLock()
        {
            yield return ExtraBegin07();
            StartRun("70902");var scene=InformationScene07(Art07FixtureKind.MaximumPool);scene.capacityOverride=2;ApplyInformationScene07(scene);
            yield return CardSetFont07(150);
            var ids=app.Session.State.dice.Take(3).Select(d=>d.dieId).ToArray();string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            foreach(var id in ids)ClickDie(id);
            Assert.That(InformationButton07("Confirm"),Is.EqualTo("可再锁 2 颗"));Assert.That(Button("Confirm").interactable,Is.False);
            Assert.That(HasActive("ExcessPending"+ids[0])||HasActive("ExcessPending"+ids[1]),Is.False);Assert.That(HasActive("ExcessPending"+ids[2]),Is.True);
            yield return CaptureEdge07("S05-excess-pending-150");
            var rejected=app.Send(new SessionCommand{kind=CommandKind.ConfirmLocks});Assert.That(rejected.accepted,Is.False);
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            ClickDie(ids[2]);Assert.That(HasActive("ExcessPending"+ids[2]),Is.False);Assert.That(Button("Confirm").interactable,Is.True);
            Click("Confirm");yield return WaitForIdle();CollectionAssert.AreEqual(ids.Take(2),app.Session.State.hand.orderedLockedIds);
        }
        [UnityTest] public IEnumerator PLAY_ART07_STATUS_QualifiedRewardsStayVisibleAboveNavigationAtAllTextScales()
        {
            yield return ExtraBegin07();
            StartRun("70903");var scene=InformationScene07(Art07FixtureKind.PermanentPost);scene.target="1";ApplyInformationScene07(scene);
            Click("Submit");app.SkipVisual();Assert.That(app.Session.State.stage.qualified,Is.True);
            var reward=app.Session.PreviewStageSettlement();string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            foreach(int font in new[]{100,125,150})
            {
                yield return CardSetFont07(font);
                Assert.That(InformationText07("QualifiedReward0"),Is.EqualTo("基础奖励  +"+reward.baseReward));
                Assert.That(InformationText07("QualifiedReward1"),Is.EqualTo("剩次奖励  +"+reward.unusedSubmissionReward));
                Assert.That(InformationText07("QualifiedReward2"),Is.EqualTo("预计利息  +"+reward.interest));
                Assert.That(HasActive("QualifiedGoldBorder"),Is.True);
                var badge=FindActive<RectTransform>("QualifiedSavedBadge07");
                var scoreFrame=V06ScreenRect(FindActive<RectTransform>("StageScoreFrame"));
                var badgeRect=V06ScreenRect(badge);
                Assert.That(scoreFrame.Contains(badgeRect.min)&&scoreFrame.Contains(badgeRect.max),Is.True,"The saved qualification badge belongs to the actual compact score frame.");
                var panel=FindActive<RectTransform>("Stats07");
                foreach(var label in panel.GetComponentsInChildren<TextMeshProUGUI>(false))
                {
                    label.ForceMeshUpdate();
                    foreach(var ch in label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(c=>!char.IsWhiteSpace(c.character)))
                        Assert.That(ch.isVisible,Is.True,label.name+" "+ch.character+" font"+font);
                    if(label.textInfo.characterCount==0)continue;
                    var b=label.textBounds;Rect ink=BurstRectIn07(label.transform,new Rect(b.min.x,b.min.y,b.size.x,b.size.y),app.canvas.transform);
                    foreach(string button in new[]{"DebugWorkspace","PreviousSubmission","Handbook","Pause"})
                    {
                        var rect=(RectTransform)Button(button).transform;
                        Assert.That(ink.Overlaps(BurstRectIn07(rect,rect.rect,app.canvas.transform)),Is.False,label.name+" overlaps "+button+" font"+font);
                    }
                }
                yield return CaptureEdge07("S08-qualified-sidebar-font"+font);
                Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
        }

        [UnityTest] public IEnumerator PLAY_ART07_STATUS_ColdQualifiedSaveKeepsGoldBorderAndBadgeWithoutRestamping()
        {
            var fixture=NewBoundaryFixture("art07-qualified-cold");fixture.isDebug=false;fixture.debugWorkspace=false;
            foreach(var die in fixture.dice)foreach(var face in die.faces){face.permanentPip=6;face.baseColor="blue";}
            new AtomicSaveStore(app.SaveLocation,app.Config,SaveSlotPolicy.Normal).Save(fixture);
            yield return LoadBootstrap();Click("ContinueRun");Click("StartStage");Click("Roll");app.SkipVisual();
            foreach(var die in app.Session.State.dice)ClickDie(die.dieId);
            Click("Confirm");app.SkipVisual();Click("Submit");app.SkipVisual();
            Assert.That(app.Session.State.stage.qualified,Is.True);
            string state=StateJson();var disk=File.ReadAllBytes(app.SaveLocation);
            foreach(int font in new[]{100,125,150})
            {
                PlayerPrefs.SetInt("art07.textScale",font);yield return LoadBootstrap();Click("ContinueRun");
                Assert.That(app.QualifiedStampBegins07,Is.Zero);Assert.That(app.QualifiedStampPlaying07,Is.False);
                Assert.That(HasActive("QualifiedGoldBorder")&&HasActive("QualifiedSavedBadge07"),Is.True);
                yield return app.StartCoroutine(CaptureEdge07("S08-qualified-cold-font"+font));
                Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            }
        }
    }
}
