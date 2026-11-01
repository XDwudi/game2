using System.Collections;
using System.IO;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest] public IEnumerator PLAY_ART07_SETTLE_BACKDROP_SaveRetryKeepsLiveTableAndSingleReward()
        {
            StartRun("70914");var scene=InformationScene07(Art07FixtureKind.MaximumTable);scene.target="1";scene.remainingSubmissions=3;
            ApplyInformationScene07(scene);Click("Submit");app.SkipVisual();
            yield return app.StartCoroutine(CaptureEdge07("S09-before-save-retry"));
            string before=StateJson();var original=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
            CommandResult failed;
            using(var locked=new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                failed=app.Send(new SessionCommand{kind=CommandKind.EndStage});
                Assert.That(failed.pendingSave,Is.True);Assert.That(failed.accepted,Is.False);
                Assert.That(app.SettlementBackdropVisible07,Is.False);Assert.That(StateJson(),Is.EqualTo(before));
                CollectionAssert.AreEqual(original,File.ReadAllBytes(app.SaveLocation));
            }
            Click("RetrySave");Assert.That(app.Session.HasPendingSave,Is.False);Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+1));
            Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));
            Assert.That(app.SettlementBackdropVisible07,Is.True);Assert.That(app.SettlementBackdropFromHistory07,Is.False,"Retry can still capture the retained live pre-commit table.");
            Assert.That(app.SettlementBackdropGraphicCount07,Is.GreaterThan(0));Assert.That(app.SettlementBackdropDiceMeshCount07,Is.GreaterThan(0));
            Assert.That(app.Session.State.lastStageSettlement.goldAfter,Is.EqualTo(failed.receipt.settlement.goldAfter));
            string state=StateJson();var saved=File.ReadAllBytes(app.SaveLocation);app.SkipVisual();app.Refresh();
            yield return app.StartCoroutine(CaptureEdge07("S09-after-save-retry-live"));
            Assert.That(app.Session.RetrySave().accepted,Is.False);Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(saved,File.ReadAllBytes(app.SaveLocation));
        }
    }
}
