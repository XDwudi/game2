using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private string DiceRetryDisplay07(string method)=>JsonUtility.ToJson(typeof(GameApp).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,null));
        private RunState DiceRetryCandidate07()=>JsonUtility.FromJson<RunState>(JsonUtility.FromJson<TestSaveEnvelope>(File.ReadAllText(app.SaveLocation+".tmp")).payload);
        private void DiceRetryPreparedCleared07()
        {
            foreach(string field in new[]{"preparedLockPreview07","preparedLockSnapshot07"})
                Assert.That(typeof(GameApp).GetField(field,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(app),Is.Null,field+" must not leak across retry/abandon.");
        }

        [UnityTest] public IEnumerator PLAY_ART07_DICEINTERACT_SaveFaultRetryPreservesOldC53PreviewAndAbandonReleasesIt()
        {
            yield return DiceLabelBegin07(150);DiceInteractionC53Scene07();yield return new WaitForSecondsRealtime(.25f);
            string[] ids=app.Session.State.dice.Select(d=>d.dieId).ToArray();
            ClickDie(ids[0]);Click("Confirm");app.SkipVisual();yield return null;
            app.enabled=false;app.table.enabled=false;
            int commits=0;Action<CommandReceipt> observer=r=>commits++;app.Session.Committed+=observer;
            try
            {
                string[] order={ids[3],ids[1],ids[2]};foreach(string id in order)ClickDie(id);app.table.TickLayoutTransitions07(.12f);
                var rngBefore=app.Session.State;
                string before=StateJson(),oldSnapshot=JsonUtility.ToJson(app.Session.PreviewSnapshot()),oldRange=JsonUtility.ToJson(app.Session.PreviewScoreRange());
                Assert.That(app.Session.PreviewScoreRange().isRandom,Is.True,"C53 must be genuinely unrevealed before confirm.");
                string oldLabel=FindActive<TextMeshProUGUI>("FinalScore").text;byte[] diskBefore=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
                string log=Path.Combine(saveDirectory,"debug-actions.jsonl");int logCount=File.ReadAllLines(log).Length;RunState candidate;
                using(var locked=new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    Click("Confirm");Assert.That(app.Session.HasPendingSave,Is.True,"Real AtomicSaveStore replacement must fail under the held handle.");
                    Assert.That(app.LockVisualPlaying07,Is.False);Assert.That(app.IsBusy,Is.False);Assert.That(commits,Is.Zero);
                    DiceInteractionInvariant07(before,diskBefore);Assert.That(File.ReadAllLines(log).Length,Is.EqualTo(logCount));
                    candidate=DiceRetryCandidate07();CollectionAssert.AreEqual(new[]{ids[0]}.Concat(order),candidate.hand.orderedLockedIds);
                    Assert.That(candidate.diceRngState,Is.EqualTo(rngBefore.diceRngState));Assert.That(candidate.shopRngState,Is.EqualTo(rngBefore.shopRngState));Assert.That(candidate.effectRngState,Is.EqualTo(rngBefore.effectRngState));
                    string frozenCandidate=JsonUtility.ToJson(candidate);
                    // A genuine rejected new command used to erase the prepared pre-confirm snapshot.
                    var rejected=app.Send(new SessionCommand{kind=CommandKind.Roll});Assert.That(rejected.accepted,Is.False);Assert.That(app.Session.HasPendingSave,Is.True);
                    Click("RetrySave");Assert.That(app.Session.HasPendingSave,Is.True);Assert.That(commits,Is.Zero);
                    Assert.That(JsonUtility.ToJson(DiceRetryCandidate07()),Is.EqualTo(frozenCandidate));
                    DiceInteractionInvariant07(before,diskBefore);Assert.That(File.ReadAllLines(log).Length,Is.EqualTo(logCount));
                    Assert.That(DiceRetryDisplay07("DisplayScoreSnapshot07"),Is.EqualTo(oldSnapshot));Assert.That(DiceRetryDisplay07("DisplayScorePreview07"),Is.EqualTo(oldRange));
                    yield return CaptureEdge07("dice-retry-real-file-lock-old-checkpoint");
                }
                var queuedRetry=Button("RetrySave").onClick;Click("RetrySave");
                Assert.That(app.Session.HasPendingSave,Is.False);Assert.That(commits,Is.EqualTo(1));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+1));
                Assert.That(StateJson(),Is.EqualTo(JsonUtility.ToJson(candidate)),"Exactly the frozen candidate, including RNG, commits after release.");
                Assert.That(JsonUtility.ToJson(new AtomicSaveStore(app.SaveLocation,app.Config).Load()),Is.EqualTo(StateJson()));
                Assert.That(app.LockVisualPlaying07,Is.True);Assert.That(app.IsBusy,Is.True);Assert.That(app.LockVisualDuration07,Is.EqualTo(.23f).Within(.000001f));
                Assert.That(DiceRetryDisplay07("DisplayScoreSnapshot07"),Is.EqualTo(oldSnapshot));Assert.That(DiceRetryDisplay07("DisplayScorePreview07"),Is.EqualTo(oldRange));
                Assert.That(FindActive<TextMeshProUGUI>("FinalScore").text,Is.EqualTo(oldLabel));DiceRetryPreparedCleared07();
                string committed=StateJson();byte[] saved=File.ReadAllBytes(app.SaveLocation);
                queuedRetry.Invoke();Assert.That(commits,Is.EqualTo(1));Assert.That(File.ReadAllLines(log).Length,Is.EqualTo(logCount+1));DiceInteractionInvariant07(committed,saved);
                DiceInteractionBothClocks07(.229f);Assert.That(app.LockVisualPlaying07,Is.True);
                Assert.That(DiceRetryDisplay07("DisplayScoreSnapshot07"),Is.EqualTo(oldSnapshot));Assert.That(DiceRetryDisplay07("DisplayScorePreview07"),Is.EqualTo(oldRange));
                yield return CaptureEdge07("dice-retry229-old-c53-range-and-snapshot");
                DiceInteractionBothClocks07(.0011f);Assert.That(app.LockVisualPlaying07,Is.False);Assert.That(app.IsBusy,Is.False);
                Assert.That(DiceRetryDisplay07("DisplayScoreSnapshot07"),Is.EqualTo(JsonUtility.ToJson(app.Session.PreviewSnapshot())));
                Assert.That(DiceRetryDisplay07("DisplayScorePreview07"),Is.EqualTo(JsonUtility.ToJson(app.Session.PreviewScoreRange())));
                Assert.That(app.Session.PreviewScoreRange().isRandom,Is.True);DiceInteractionInvariant07(committed,saved);
                yield return CaptureEdge07("dice-retry230-new-unrevealed-c53-range");

                // A second real fault is abandoned; no stale capture may survive into a later successful lock.
                string last=ids.Single(id=>!app.Session.State.hand.orderedLockedIds.Contains(id));ClickDie(last);app.table.TickLayoutTransitions07(.12f);
                using(var locked=new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    Click("Confirm");Assert.That(app.Session.HasPendingSave,Is.True);Assert.That(commits,Is.EqualTo(1));
                    Click("AbandonPendingSave");Assert.That(app.Session.HasPendingSave,Is.False);DiceRetryPreparedCleared07();
                    DiceInteractionInvariant07(committed,saved);CollectionAssert.AreEqual(new[]{last},app.Session.PendingIds,"Abandon preserves the prior UI selection, as Core already specifies.");Assert.That(File.ReadAllLines(log).Length,Is.EqualTo(logCount+1));
                }
                Click("Clear");Assert.That(app.Session.PendingIds,Is.Empty);
                string nextOldRange=JsonUtility.ToJson(app.Session.PreviewScoreRange()),nextOldSnapshot=JsonUtility.ToJson(app.Session.PreviewSnapshot());
                ClickDie(last);app.table.TickLayoutTransitions07(.12f);Click("Confirm");
                Assert.That(commits,Is.EqualTo(2));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+2));Assert.That(app.LockVisualPlaying07,Is.True);
                Assert.That(DiceRetryDisplay07("DisplayScorePreview07"),Is.EqualTo(nextOldRange));Assert.That(DiceRetryDisplay07("DisplayScoreSnapshot07"),Is.EqualTo(nextOldSnapshot));
                string lastCommitted=StateJson();byte[] lastSaved=File.ReadAllBytes(app.SaveLocation);app.SkipVisual();DiceInteractionInvariant07(lastCommitted,lastSaved);
                Assert.That(File.ReadAllLines(log).Length,Is.EqualTo(logCount+2));DiceRetryPreparedCleared07();
                yield return CaptureEdge07("dice-retry-after-abandon-next-confirm-correct");
            }
            finally{app.Session.Committed-=observer;if(app.IsBusy)app.SkipVisual();app.enabled=true;app.table.enabled=true;}
        }
    }
}
