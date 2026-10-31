using System;
using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_PreviousSubmissionRetainsOriginalScoreAndPermanentEffectsAfterNextRollAndReload()
        {
            foreach (string controllerId in new[] {"C07", "C15"})
            {
                TestContext.WriteLine("History and preview effect fixture: " + controllerId);
                var setup = new GameSession(app.Config, new MemorySaveStore());
                Assert.That(setup.Execute(new SessionCommand {kind = CommandKind.NewRun, commandId = "history-fixture-" + controllerId, seed = 777}).accepted, Is.True);
                var fixture = setup.State;
                fixture.isDebug = true; fixture.debugWorkspace = true;
                fixture.controllers.Add(BoundaryController(fixture, controllerId));
                for (int i = 0; i < fixture.dice.Count; i++)
                    foreach (var face in fixture.dice[i].faces)
                    {
                        face.permanentPip = i + 1;
                        face.baseColor = i == 0 || i == 2 ? "red" : "blue";
                    }
                // Only the explicit fixture's permanent data changes. Targets, scoring rules,
                // submission count and prices all retain the production configuration.
                new GameSession(app.Config, new MemorySaveStore(), fixture);
                yield return LoadBoundaryFixture(fixture);
                ResumeBoundaryFixture(); Click("StartStage"); Click("Roll"); Click("Skip");
                int count = controllerId == "C07" ? 2 : 3;
                string[] ids = app.Session.State.dice.Take(count).Select(d => d.dieId).ToArray();
                foreach (string id in ids) ClickDie(id);
                Click("Confirm");
                var expectedScore = app.Session.PreviewScore();
                var frozenSnapshot = app.Session.PreviewSnapshot();
                Assert.That(ExactScoreMath.Compare(expectedScore.score,app.Session.State.stage.target), Is.LessThan(0), "This regression must follow a non-clearing submission.");
                string prePreview = StateJson();
                byte[] prePreviewFile = File.ReadAllBytes(app.SaveLocation);
                int commits = 0;
                Action<CommandReceipt> observer = _ => commits++;
                app.Session.Committed += observer;
                var expectedEffects = app.Session.PreviewSubmissionEffects();
                Assert.That(expectedEffects, Has.Length.EqualTo(1));
                Assert.That(expectedEffects[0].kind, Is.EqualTo(controllerId == "C07" ? "DieCopied" : "FaceDyed"));
                Assert.That(expectedEffects[0].sourceId, Is.EqualTo(controllerId));
                Assert.That(expectedEffects[0].dieId, Is.EqualTo(ids[1]));
                if (controllerId == "C07") Assert.That(expectedEffects[0].otherDieId, Is.EqualTo(ids[0]));
                else Assert.That(expectedEffects[0].faceId, Is.EqualTo(frozenSnapshot.dice[1].faceId));
                string[] effectKeys = expectedEffects.Select(JsonUtility.ToJson).ToArray();
                for (int repeat = 0; repeat < 5; repeat++)
                    CollectionAssert.AreEqual(effectKeys, app.Session.PreviewSubmissionEffects().Select(JsonUtility.ToJson).ToArray());
                expectedEffects[0].dieId = "MUTATED_RETURNED_PREVIEW";
                CollectionAssert.AreEqual(effectKeys, app.Session.PreviewSubmissionEffects().Select(JsonUtility.ToJson).ToArray());
                Assert.That(StateJson(), Is.EqualTo(prePreview), "Effect preview must not advance RNG, sequences, counters or permanent dice.");
                CollectionAssert.AreEqual(prePreviewFile, File.ReadAllBytes(app.SaveLocation), "Effect preview must not write a save.");
                Assert.That(commits, Is.Zero, "Effect preview must not notify commit observers.");
                Click("Submit");
                Assert.That(commits, Is.EqualTo(1));
                app.Session.Committed -= observer;
                yield return WaitForIdle();
                var submitted = app.Session.State;
                Assert.That(submitted.phase, Is.EqualTo("HandReady"));
                Assert.That(submitted.lastSubmission.breakdown.score, Is.EqualTo(expectedScore.score));
                Assert.That(JsonUtility.ToJson(submitted.lastSubmission.snapshot), Is.EqualTo(JsonUtility.ToJson(frozenSnapshot)));
                CollectionAssert.AreEqual(effectKeys, submitted.lastEffects.Select(JsonUtility.ToJson).ToArray(), "Committed effects must match preview source, target, order and all detail fields.");
                var affected = submitted.dice.Single(d => d.dieId == ids[1]);
                if (controllerId == "C07")
                {
                    CollectionAssert.AreEqual(frozenSnapshot.dice[0].permanentFacesCopy.Select(JsonUtility.ToJson).ToArray(), affected.faces.Select(JsonUtility.ToJson).ToArray());
                    Assert.That(frozenSnapshot.dice[1].permanentSixFaceSum, Is.EqualTo(12), "History must keep the target's pre-copy faces.");
                    Assert.That(affected.faces.Sum(f => f.permanentPip), Is.EqualTo(6));
                }
                else
                {
                    string faceId = frozenSnapshot.dice[1].faceId;
                    Assert.That(frozenSnapshot.dice[1].permanentFacesCopy.Single(f => f.faceId == faceId).baseColor, Is.EqualTo("blue"));
                    Assert.That(affected.faces.Single(f => f.faceId == faceId).baseColor, Is.EqualTo("red"));
                }
                string history = JsonUtility.ToJson(submitted.lastSubmission);
                Click("Roll"); Click("Skip");
                Assert.That(app.Session.State.lastEffects, Is.Empty, "Next Roll must replace the immediate effect list, proving history uses the submission receipt.");
                Assert.That(JsonUtility.ToJson(app.Session.State.lastSubmission), Is.EqualTo(history));
                string beforeHistory = StateJson();
                Click("PreviousSubmission");
                string[] originalPanel = ReadHistoryPanel(count);
                Assert.That(originalPanel[0], Does.EndWith("= " + System.Numerics.BigInteger.Parse(expectedScore.score).ToString("N0",System.Globalization.CultureInfo.InvariantCulture)));
                Assert.That(originalPanel[1], Does.Contain(app.Config.Controller(controllerId).displayName));
                Assert.That(originalPanel[1], Does.Contain(ids[1]));
                if (controllerId == "C07") Assert.That(originalPanel[1], Does.Contain(ids[0] + " 的六面复制到 " + ids[1]));
                else Assert.That(originalPanel[1], Does.Contain(frozenSnapshot.dice[1].faceId + " 永久染红"));
                Assert.That(originalPanel[3], Does.Contain("永久 2 → 当前 2"), "Historical detail must display pre-effect data.");
                Assert.That(StateJson(), Is.EqualTo(beforeHistory));
                Click("CloseDialog");
                ClickDie(ids[0]); Click("Confirm");
                string beforeCurrent = StateJson();
                Click("Expanded");
                string currentEffects = FindActive<TextMeshProUGUI>("Triggered").text;
                Assert.That(currentEffects, Does.Not.Contain("的六面复制到"));
                Assert.That(currentEffects, Does.Not.Contain("永久染红"), "The current hand preview must not borrow last hand's permanent effects.");
                Assert.That(StateJson(), Is.EqualTo(beforeCurrent));
                Click("CloseDialog");
                string beforeReload = StateJson();
                yield return LoadBootstrap();
                ResumeBoundaryFixture();
                Assert.That(StateJson(), Is.EqualTo(beforeReload));
                Click("PreviousSubmission");
                CollectionAssert.AreEqual(originalPanel, ReadHistoryPanel(count), "Reload must retain the exact historical formula, faces and source/target effect text.");
                Assert.That(StateJson(), Is.EqualTo(beforeReload));
                Click("CloseDialog");
            }
        }

        private string[] ReadHistoryPanel(int diceCount)
        {
            return new[] {FindActive<TextMeshProUGUI>("Formula").text, FindActive<TextMeshProUGUI>("Triggered").text}
                .Concat(Enumerable.Range(0, diceCount).Select(i => FindActive<TextMeshProUGUI>("DieLine" + i).text)).ToArray();
        }
    }
}
