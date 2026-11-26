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
                Click("Confirm");yield return WaitForIdle();
                var expectedScore = app.Session.PreviewScore();
                var frozenSnapshot = app.Session.PreviewSnapshot();
                Assert.That(ExactScoreMath.Compare(expectedScore.score,app.Session.State.stage.target), Is.LessThan(0), "This regression must follow a non-clearing submission.");
                string prePreview = StateJson();
                byte[] prePreviewFile = File.ReadAllBytes(app.SaveLocation);
                int commits = 0;
                Action<CommandReceipt> observer = _ => commits++;
                app.Session.Committed += observer;
                var expectedEffects = app.Session.PreviewSubmissionEffects();
                // p1.1.1 (RULES_DELTA 6, cultivation counting): besides the one headline effect a rewrite may now record
                // the per-face changes it made. Every effect still comes from this card and lands on the second die.
                var headline = expectedEffects.Where(e => e.kind == (controllerId == "C07" ? "DieCopied" : "FaceDyed")).ToArray();
                string listed = string.Join(", ", expectedEffects.Select(e => e.kind + ":" + e.sourceId + ":" + e.dieId));
                Assert.That(headline, Has.Length.EqualTo(1), listed);
                Assert.That(expectedEffects.All(e => e.sourceId == controllerId && e.dieId == ids[1]), Is.True, listed);
                Assert.That(headline[0].sourceId, Is.EqualTo(controllerId));
                Assert.That(headline[0].dieId, Is.EqualTo(ids[1]));
                if (controllerId == "C07") Assert.That(headline[0].otherDieId, Is.EqualTo(ids[0]));
                else Assert.That(headline[0].faceId, Is.EqualTo(frozenSnapshot.dice[1].faceId));
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
                // p1.1.1 (RULES_DELTA 12.1, 13): the first scoring hand of a run also records the run's best hand
                // (HandRecordBroken); a record is not a permanent effect, so the effect preview does not list it.
                Assert.That(submitted.lastEffects.Where(e => e.kind == "HandRecordBroken" || e.kind == "ClearRatioRecord").All(e => string.IsNullOrEmpty(e.dieId)), Is.True);
                CollectionAssert.AreEqual(effectKeys, submitted.lastEffects.Where(e => e.kind != "HandRecordBroken" && e.kind != "ClearRatioRecord").Select(JsonUtility.ToJson).ToArray(), "Committed effects must match preview source, target, order and all detail fields.");
                var affected = submitted.dice.Single(d => d.dieId == ids[1]);
                if (controllerId == "C07")
                {
                    // p1.1.1 (RULES_DELTA 6, FaceState.cultivated): the copy marks every target face it really changed and does
                    // not copy the source's marks, so the faces equal the source's apart from that mark.
                    string Unmarked(FaceState f) { var copy = JsonUtility.FromJson<FaceState>(JsonUtility.ToJson(f)); copy.cultivated = false; return JsonUtility.ToJson(copy); }
                    CollectionAssert.AreEqual(frozenSnapshot.dice[0].permanentFacesCopy.Select(Unmarked).ToArray(), affected.faces.Select(Unmarked).ToArray());
                    var before = frozenSnapshot.dice[1].permanentFacesCopy;
                    foreach (var face in affected.faces)
                        Assert.That(face.cultivated, Is.EqualTo(Unmarked(face) != Unmarked(before.Single(f => f.faceId == face.faceId))), face.faceId + ": marked exactly when the copy changed it");
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
                Click("PreviousSubmission"); Click("FullScoreDetails");
                string[] originalPanel = ReadHistoryPanel(count);
                Assert.That(originalPanel[0], Does.EndWith("= " + System.Numerics.BigInteger.Parse(expectedScore.score).ToString("N0",System.Globalization.CultureInfo.InvariantCulture) + "（最后去掉小数）"));
                Assert.That(originalPanel[1], Does.Contain(app.Config.Controller(controllerId).displayName));
                // v1.1 (W09, TX-02): the affected die and face are named ("骰子 2 的第 3 面"), never shown as D002 / F03.
                Assert.That(originalPanel[1], Does.Contain(GameApp.DieName11(ids[1])));
                Assert.That(originalPanel[1], Does.Not.Contain(ids[1]));
                if (controllerId == "C07") Assert.That(originalPanel[1], Does.Contain(GameApp.DieName11(ids[0]) + " 的六面复制到" + GameApp.DieName11(ids[1])));
                else Assert.That(originalPanel[1], Does.Contain(GameApp.DieFaceName11(ids[1], frozenSnapshot.dice[1].faceId) + "永久染红"));
                Assert.That(originalPanel[3], Does.Contain("点数 2 · 蓝").And.Contain("六面合计 12"),
                    "Both effects happen after scoring: the second die in this receipt keeps its blue two and its original six faces.");
                Assert.That(StateJson(), Is.EqualTo(beforeHistory));
                Click("CloseDialog");
                ClickDie(ids[0]); Click("Confirm");yield return WaitForIdle();
                string beforeCurrent = StateJson();
                yield return LegacyClick07("Factor0");
                yield return LegacyClick07("FullScoreDetails");
                string currentEffects = FindActive<TextMeshProUGUI>("Triggered").text;
                Assert.That(currentEffects, Does.Not.Contain("的六面复制到"));
                Assert.That(currentEffects, Does.Not.Contain("永久染红"), "The current hand preview must not borrow last hand's permanent effects.");
                Assert.That(StateJson(), Is.EqualTo(beforeCurrent));
                Click("CloseDialog");
                string beforeReload = StateJson();
                yield return LoadBootstrap();
                ResumeBoundaryFixture();
                Assert.That(StateJson(), Is.EqualTo(beforeReload));
                Click("PreviousSubmission"); Click("FullScoreDetails");
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
