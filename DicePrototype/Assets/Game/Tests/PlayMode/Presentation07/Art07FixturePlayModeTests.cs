using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using DiceCraft.Verification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using TMPro;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest] public IEnumerator PLAY_ART07_G147UsesPublicDebugCommandsAndPreservesNormalCheckpoint()
        {
            app.StartNew("20261031"); byte[] normal = File.ReadAllBytes(app.SaveLocation);
            var report = Art07Fixtures.Apply(app, Art07FixtureKind.G147);
            Assert.That(report.expectedValuesMatched, Is.True); Assert.That(report.previewScore, Is.EqualTo("967680")); Assert.That(report.locked, Is.EqualTo(5));
            var receipt = Art07Fixtures.Submit(app, Art07FixtureKind.G147); string committed = GameSession.StateHash(app.Session.State);
            Assert.That(receipt.ChainCount, Is.EqualTo(4)); Assert.That(receipt.ComboTier, Is.EqualTo(5)); app.SkipVisual();
            Assert.That(GameSession.StateHash(app.Session.State), Is.EqualTo(committed));
            CollectionAssert.AreEqual(normal, File.ReadAllBytes(Path.Combine(saveDirectory, "active-run.json")));
            yield return null;
        }
        [UnityTest] public IEnumerator PLAY_ART07_AllDiagnosticScenesValidateThroughRealSession()
        {
            foreach (Art07FixtureKind kind in System.Enum.GetValues(typeof(Art07FixtureKind)))
            {
                var report = Art07Fixtures.Apply(app, kind); Assert.That(report.debugWorkspace && report.expectedValuesMatched, Is.True, kind.ToString());
                if (kind == Art07FixtureKind.MaximumTable || kind == Art07FixtureKind.MaximumPool)
                { Assert.That(app.Session.State.dice.Count(d => d.lifetime == "permanent"), Is.EqualTo(8)); Assert.That(ControllerRules.OrdinaryUsed(app.Session.State.controllers), Is.EqualTo(4)); }
                if (kind == Art07FixtureKind.ZeroMultiplier)
                { var receipt = Art07Fixtures.Submit(app, kind); Assert.That(receipt.Events.Any(e => e.EffectFamily == "SubmissionIncome"), Is.True); app.SkipVisual(); }
                yield return null;
            }
        }
        [UnityTest] public IEnumerator PLAY_ART07_RealCommittedReceiptThreeSpeedsAndReducedMotionPreserveStateAndSaveBytes()
        {
            Art07Fixtures.Apply(app, Art07FixtureKind.G147); var receipt = Art07Fixtures.Submit(app, Art07FixtureKind.G147); app.SkipVisual();
            string state = GameSession.StateHash(app.Session.State); byte[] disk = File.ReadAllBytes(app.SaveLocation);
            foreach (PresentationSpeed speed in System.Enum.GetValues(typeof(PresentationSpeed))) foreach (bool reduced in new[] { false, true })
            {
                var player = new PresentationPlayer(); var sequence = PresentationSequence.Create(receipt, new PresentationOptions(speed, reduced));
                PresentationReceipt final = null; player.FinalStateRequested += r => final = r;
                player.Play(sequence); player.Clock.Paused = true; player.Tick(100); player.Clock.Paused = false; player.Tick(10);
                Assert.That(final.ScoreDecimal, Is.EqualTo("967680")); Assert.That(final.MultiplierNumeratorDecimal, Is.EqualTo("26880"));
                Assert.That(GameSession.StateHash(app.Session.State), Is.EqualTo(state)); CollectionAssert.AreEqual(disk, File.ReadAllBytes(app.SaveLocation));
                yield return null;
            }
        }
        [UnityTest, Timeout(600000)] public IEnumerator PLAY_ART07_TwoHundredFullRendererReplaysKeepNodesMaterialsAndCommittedStateStable()
        {
            bool hadInstant = PlayerPrefs.HasKey("art07.instant"); int originalInstant = PlayerPrefs.GetInt("art07.instant", 0);
            try
            {
                Art07Fixtures.Apply(app, Art07FixtureKind.G147); Art07Fixtures.Submit(app, Art07FixtureKind.G147); app.SkipVisual();
                Click("Pause"); Click("Settings"); Click("SettingsTab2");
                int changes = 0;
                while (!Button("Speed").GetComponentInChildren<TextMeshProUGUI>().text.StartsWith("Fast", System.StringComparison.Ordinal) && changes++ < 3) Click("Speed");
                Assert.That(Button("Speed").GetComponentInChildren<TextMeshProUGUI>().text, Does.StartWith("Fast")); Click("SettingsDone");
                string committed = GameSession.StateHash(app.Session.State); byte[] disk = File.ReadAllBytes(app.SaveLocation);
                IEnumerator Replay()
                {
                    Assert.That(app.ReplayLastPresentation07(), Is.True); Assert.That(app.PresentationDurationMilliseconds07, Is.GreaterThan(0).And.LessThanOrEqualTo(800));
                    double deadline = Time.realtimeSinceStartupAsDouble + 5;
                    while (app.PresentationPlaying07 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
                    Assert.That(app.PresentationPlaying07, Is.False, "Full visual replay must finish without Skip or another rule command.");
                    Assert.That(GameSession.StateHash(app.Session.State), Is.EqualTo(committed));
                    yield return null;
                }
                yield return Replay(); yield return Replay();
                string[] nodes = app.canvas.GetComponentsInChildren<Transform>(true).Select(t => t.GetEntityId().ToString()).OrderBy(x => x, System.StringComparer.Ordinal).ToArray();
                string[] materials = Resources.FindObjectsOfTypeAll<Material>().Select(m => m.GetEntityId().ToString()).OrderBy(x => x, System.StringComparer.Ordinal).ToArray();
                for (int i = 0; i < 200; i++)
                {
                    yield return Replay();
                    CollectionAssert.AreEqual(nodes, app.canvas.GetComponentsInChildren<Transform>(true).Select(t => t.GetEntityId().ToString()).OrderBy(x => x, System.StringComparer.Ordinal).ToArray(), "Canvas node growth at replay " + i);
                    CollectionAssert.AreEqual(materials, Resources.FindObjectsOfTypeAll<Material>().Select(m => m.GetEntityId().ToString()).OrderBy(x => x, System.StringComparer.Ordinal).ToArray(), "Material growth at replay " + i);
                }
                CollectionAssert.AreEqual(disk, File.ReadAllBytes(app.SaveLocation), "Read-only replay must not write the committed save.");
            }
            finally { if (hadInstant) PlayerPrefs.SetInt("art07.instant", originalInstant); else PlayerPrefs.DeleteKey("art07.instant"); }
        }
    }
}
