using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>Real Bootstrap + opt-in resource route. Editor evidence is not Player acceptance.</summary>
    public sealed class Art07ResourceDiagnosticPlayModeTests
    {
        private const string ScenePath = "Assets/Game/Scenes/Bootstrap.unity";
        private string evidence, isolatedSaves, oldSaveOverride;
        private bool oldSuppressDisplay;
        private GameApp app;
        private Art07ResourceDiagnostic probe;
        private Preference[] preferences;
        private Dictionary<string, byte[]> ordinaryFiles;
        private readonly HashSet<string> diagnosticClockKeys = new HashSet<string>();
        private readonly List<string> cleanupErrors = new List<string>();

        [Serializable] private sealed class Preference { public string key, kind, value; public bool existed; }
        [Serializable] private sealed class Preferences { public Preference[] values; }
        [Serializable] private sealed class FileProof { public string path, sha256; public long bytes; }
        [Serializable] private sealed class GuardReport
        {
            public string schema = "art07-resource-integration-guard-1", unity, runtimeMvid, testMvid, evidence;
            public bool editor = true, playerAcceptance = false, ordinaryFilesUnchanged, preferencesUnchanged, preferencesRestored;
            public FileProof[] filesBefore, filesAfter;
            public Preference[] preferencesBefore, preferencesAfter, preferencesAfterRestore;
            public string[] diagnosticClockKeys, cleanupErrors;
        }
        [Serializable] private sealed class DiagnosticReport
        {
            public string schema, status, error, configSha256, configVersion, runtimeMvid, output, saveDirectory;
            public bool editor, wholeReleaseAccepted, startupDisplaySuppressed, ordinaryPreferenceValuesUnchanged, preferencesRestored, appDestroyedBeforeClockCleanup, idleStatePendingAndSavesUnchanged;
            public string[] diagnosticClockKeysRegistered, diagnosticClockKeysRemoved;
            public Phase[] phases; public Snapshot[] snapshots; public Idle idle;
            public Boundary idleBefore, idleAfter;
        }
        [Serializable] private sealed class Phase { public string name; }
        [Serializable] private sealed class Boundary { public string stateSha256; public string[] pending; public SaveProof[] saves; }
        [Serializable] private sealed class SaveProof { public string relativePath, sha256; public long bytes; }
        [Serializable] private sealed class Snapshot
        {
            public string name, phase; public int activeBodies;
            public long activeBodyTriangles, textureObjectBytes, fontBytes, referencedArtBytes, renderTargetBytes, unknownBytes;
            public TextureInfo[] textures; public FontInfo[] fonts; public MeshInfo[] meshes;
        }
        [Serializable] private sealed class TextureInfo { public string entityId, category; public long nativeBytes; public string[] owners; }
        [Serializable] private sealed class FontInfo { public string entityId; public string[] atlasIds; }
        [Serializable] private sealed class MeshInfo { public string owner, category, meshId; public bool active, rendererEnabled, sceneInstance; public long triangles; }
        [Serializable] private sealed class Idle
        {
            public double requestedSeconds, actualSeconds;
            public int rows, validGcRows, missingGcRows, zeroGcRows, nonzeroGcRows, unfocusedRows, movingRows, pointerChangedRows, inputRows, sequenceChangedRows;
            public bool observationComplete;
        }

        [UnitySetUp] public IEnumerator Setup()
        {
            // Match the existing test evidence directory convention. TestEvidence.RunFilters
            // supplies the independent NUnit/assembly/source binding for this test too.
            evidence = Path.Combine(Application.temporaryCachePath, "DiceCraftTests_" + Guid.NewGuid().ToString("N"));
            isolatedSaves = Path.Combine(evidence, "isolated-saves"); Directory.CreateDirectory(isolatedSaves);
            preferences = CapturePreferences(); ordinaryFiles = CaptureOrdinaryFiles();
            oldSaveOverride = GameApp.SaveDirectoryOverride; oldSuppressDisplay = GameApp.SuppressStartupDisplay;
            GameApp.SaveDirectoryOverride = isolatedSaves; GameApp.SuppressStartupDisplay = true;
            var load = SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            Assert.That(load, Is.Not.Null); yield return load; yield return null;
            app = Object.FindAnyObjectByType<GameApp>(); Assert.That(app, Is.Not.Null);
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (!app.StartupReady07 && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(app.StartupReady07, Is.True, "Real Bootstrap did not become ready within 30 seconds.");
            Assert.That(app.SaveLocation, Is.EqualTo(Path.Combine(isolatedSaves, "active-run.json")));
            Assert.That(app.Session.State, Is.Null, "The diagnostic must begin at a genuinely empty menu.");
            Assert.That(app.ViewState, Is.Null); Canvas.ForceUpdateCanvases();
        }

        [UnityTest, Timeout(240000)]
        public IEnumerator PLAY_ART07_ResourceDiagnostic_IsolatedRealRouteInventoryAndTwentySecondIdle()
        {
            string output = Path.Combine(evidence, "art07-resource-diagnostic");
            string expectedConfigSha = Hash(Encoding.UTF8.GetBytes(app.configAsset.text));
            string expectedConfigVersion = app.Config.configVersion;
            string expectedFont = app.uiFont.GetEntityId().ToString();
            string expectedBodyMesh = app.table.bodyMesh.GetEntityId().ToString();
            var prefsBeforeProbe = CapturePreferences();
            probe = Art07ResourceDiagnostic.BeginForTests(app, output);
            double deadline = Time.realtimeSinceStartupAsDouble + 200;
            // Do not enumerate objects, clone state, format strings, or write files in this loop.
            while (!probe.FinishedForTests && Time.realtimeSinceStartupAsDouble < deadline) yield return null;
            Assert.That(probe.FinishedForTests, Is.True, "Resource probe exceeded its own bounded execution/cleanup time; preserve its output and outer guard.");
            Assert.That(probe.ErrorForTests, Is.Null.Or.Empty, probe.ErrorForTests);
            string reportPath = Path.Combine(output, "resource-report.json");
            Assert.That(File.Exists(reportPath), Is.True, "A completed probe must produce its real report.");
            var report = JsonUtility.FromJson<DiagnosticReport>(File.ReadAllText(reportPath));
            foreach (string key in report.diagnosticClockKeysRegistered ?? Array.Empty<string>()) diagnosticClockKeys.Add(key);
            Assert.That(report.schema, Is.EqualTo("art07-player-resources-1"));
            Assert.That(report.status, Is.EqualTo("diagnostic_completed_not_acceptance"), report.error);
            Assert.That(report.editor, Is.True); Assert.That(report.wholeReleaseAccepted, Is.False);
            Assert.That(report.runtimeMvid, Is.EqualTo(typeof(GameApp).Module.ModuleVersionId.ToString()));
            Assert.That(report.configVersion, Is.EqualTo(expectedConfigVersion)); Assert.That(report.configSha256, Is.EqualTo(expectedConfigSha));
            Assert.That(Path.GetFullPath(report.output), Is.EqualTo(Path.GetFullPath(output)));
            Assert.That(Path.GetFullPath(report.saveDirectory), Is.EqualTo(Path.GetFullPath(isolatedSaves)));
            Assert.That(report.startupDisplaySuppressed, Is.True);
            Assert.That(report.ordinaryPreferenceValuesUnchanged, Is.True); Assert.That(report.preferencesRestored, Is.True);
            Assert.That(report.appDestroyedBeforeClockCleanup, Is.True); Assert.That(app == null, Is.True);
            Assert.That(report.diagnosticClockKeysRegistered, Is.Not.Empty);
            CollectionAssert.AreEquivalent(report.diagnosticClockKeysRegistered, report.diagnosticClockKeysRemoved);
            foreach (string key in report.diagnosticClockKeysRegistered) Assert.That(PlayerPrefs.HasKey(key), Is.False, key);
            Assert.That(JsonUtility.ToJson(new Preferences { values = CapturePreferences() }), Is.EqualTo(JsonUtility.ToJson(new Preferences { values = prefsBeforeProbe })));
            Assert.That(SameFiles(ordinaryFiles, CaptureOrdinaryFiles()), Is.True, "The isolated route touched ordinary persistent files.");

            string[] snapshotNames = report.snapshots.Select(s => s.name).ToArray();
            CollectionAssert.AreEquivalent(new[] { "baseline-menu", "handbook-hands", "handbook-colors", "settings-tab-0", "settings-tab-1", "settings-tab-2", "actual-shop-current-tab", "ten-ordinary-dice-after-ui-visits", "after-idle" }, snapshotNames);
            Assert.That(report.snapshots.Single(s => s.name == "actual-shop-current-tab").phase, Is.EqualTo("Shop"));
            Assert.That(report.phases.Any(p => p.name == "command:Submit"), Is.True);
            Assert.That(report.phases.Any(p => p.name == "command:Continue"), Is.True);
            var baseline = report.snapshots.Single(s => s.name == "baseline-menu");
            Assert.That(baseline.fonts.Any(f => f.entityId == expectedFont), Is.True, "The real scene's primary font must appear by native identity.");
            foreach (var snapshot in report.snapshots)
            {
                Assert.That(snapshot.textures, Is.Not.Empty, snapshot.name);
                Assert.That(snapshot.textures.Select(t => t.entityId).Distinct().Count(), Is.EqualTo(snapshot.textures.Length), "Textures must be unique native objects.");
                Assert.That(snapshot.fonts.SelectMany(f => f.atlasIds).Where(id => !string.IsNullOrEmpty(id)), Is.Not.Empty);
                foreach (string atlasId in snapshot.fonts.SelectMany(f => f.atlasIds).Where(id => !string.IsNullOrEmpty(id)).Distinct())
                    Assert.That(snapshot.textures.Any(t => t.entityId == atlasId && t.category == "font" && t.owners.Any(o => o.StartsWith("TMP:", StringComparison.Ordinal))), Is.True, snapshot.name + " atlas " + atlasId);
                Assert.That(snapshot.textureObjectBytes, Is.EqualTo(snapshot.fontBytes + snapshot.referencedArtBytes + snapshot.renderTargetBytes + snapshot.unknownBytes), "Unknown resources must remain in the total.");
            }
            var ten = report.snapshots.Single(s => s.name == "ten-ordinary-dice-after-ui-visits");
            Assert.That(ten.activeBodies, Is.EqualTo(10)); Assert.That(ten.activeBodyTriangles, Is.GreaterThan(0));
            var bodies = ten.meshes.Where(m => m.category == "ordinary-body" && m.active && m.rendererEnabled).ToArray();
            Assert.That(bodies, Has.Length.EqualTo(10)); Assert.That(bodies.Select(m => m.owner).Distinct().Count(), Is.EqualTo(10));
            Assert.That(bodies.All(m => m.meshId == expectedBodyMesh && m.sceneInstance && m.triangles > 0), Is.True, "Count the actual ten body instances, not outlines or faces sharing their mesh.");
            Assert.That(ten.meshes.Any(m => m.category == "body-outline-pass"), Is.True);
            Assert.That(ten.meshes.Any(m => m.category == "fragment-pool-instance" && m.sceneInstance && m.triangles > 0), Is.True);
            Assert.That(report.idleStatePendingAndSavesUnchanged, Is.True);
            Assert.That(report.idleBefore.stateSha256, Is.Not.Empty); Assert.That(report.idleAfter.stateSha256, Is.EqualTo(report.idleBefore.stateSha256));
            CollectionAssert.AreEqual(report.idleBefore.pending, report.idleAfter.pending);
            Assert.That(report.idleBefore.saves, Is.Not.Empty, "Real debug transactions must create isolated save/log evidence before idle.");
            Assert.That(JsonUtility.ToJson(report.idleAfter), Is.EqualTo(JsonUtility.ToJson(report.idleBefore)));
            Assert.That(report.idle.observationComplete, Is.True); Assert.That(report.idle.requestedSeconds, Is.EqualTo(20));
            Assert.That(report.idle.actualSeconds, Is.GreaterThanOrEqualTo(20)); Assert.That(report.idle.rows, Is.GreaterThan(0));
            Assert.That(report.idle.validGcRows, Is.EqualTo(report.idle.rows)); Assert.That(report.idle.missingGcRows, Is.Zero);
            Assert.That(report.idle.zeroGcRows + report.idle.nonzeroGcRows, Is.EqualTo(report.idle.rows));
            Assert.That(report.idle.unfocusedRows + report.idle.movingRows + report.idle.pointerChangedRows + report.idle.inputRows + report.idle.sequenceChangedRows, Is.Zero);
            // Nonzero GC remains valid observed data; the specification says "尽量0", not a new hard gate.
            string[] idleCsv = File.ReadAllLines(Path.Combine(output, "idle-frames.csv"));
            Assert.That(idleCsv.Length, Is.EqualTo(report.idle.rows + 1));
            Assert.That(idleCsv[0], Does.Contain("gcAvailable,counterBatch,focused"));
            Assert.That(File.ReadLines(Path.Combine(output, "resource-frames.csv")).Skip(1).Any(), Is.True);
            TestContext.WriteLine("Resource tool integration only, not Player acceptance: " + reportPath);
        }

        [UnityTearDown] public IEnumerator Teardown()
        {
            // Recovery is outside the measured window. Keep it independent of probe success.
            if (app != null)
            {
                Safe(() => RememberClock(app.Session?.State)); Safe(() => RememberClock(app.ViewState));
            }
            if (probe != null) Object.Destroy(probe.gameObject);
            yield return null;
            var bootstrap = SceneManager.GetSceneByPath(ScenePath);
            if (bootstrap.IsValid() && bootstrap.isLoaded)
            {
                var empty = SceneManager.CreateScene("DiceCraftResourceTestCleanup_" + Guid.NewGuid().ToString("N"));
                SceneManager.SetActiveScene(empty); yield return SceneManager.UnloadSceneAsync(bootstrap);
            }
            GameApp.SaveDirectoryOverride = oldSaveOverride; GameApp.SuppressStartupDisplay = oldSuppressDisplay;
            foreach (string key in diagnosticClockKeys) Safe(() => PlayerPrefs.DeleteKey(key));
            Preference[] afterPrefs = null, restoredPrefs = null; Dictionary<string, byte[]> afterFiles = null;
            Safe(() => afterPrefs = CapturePreferences()); Safe(() => afterFiles = CaptureOrdinaryFiles());
            bool preferencesUnchanged = afterPrefs != null && JsonUtility.ToJson(new Preferences { values = preferences }) == JsonUtility.ToJson(new Preferences { values = afterPrefs });
            bool filesUnchanged = afterFiles != null && SameFiles(ordinaryFiles, afterFiles);
            // Restore only explicit preference keys and this run's new clock keys. Ordinary
            // persistent files are a read-only guard: never overwrite concurrent user work.
            if (!preferencesUnchanged && preferences != null) foreach (var pref in preferences) Safe(() => RestorePreference(pref));
            Safe(PlayerPrefs.Save); Safe(() => restoredPrefs = CapturePreferences());
            var guard = new GuardReport { unity = Application.unityVersion, runtimeMvid = typeof(GameApp).Module.ModuleVersionId.ToString(),
                testMvid = GetType().Module.ModuleVersionId.ToString(), evidence = evidence, ordinaryFilesUnchanged = filesUnchanged,
                preferencesUnchanged = preferencesUnchanged, preferencesRestored = restoredPrefs != null && JsonUtility.ToJson(new Preferences { values = preferences }) == JsonUtility.ToJson(new Preferences { values = restoredPrefs }),
                preferencesBefore = preferences, preferencesAfter = afterPrefs, preferencesAfterRestore = restoredPrefs,
                filesBefore = Proofs(ordinaryFiles), filesAfter = Proofs(afterFiles), diagnosticClockKeys = diagnosticClockKeys.ToArray(), cleanupErrors = cleanupErrors.ToArray() };
            if (!string.IsNullOrEmpty(evidence)) Safe(() => File.WriteAllText(Path.Combine(evidence, "resource-integration-guard.json"), JsonUtility.ToJson(guard, true), new UTF8Encoding(false)));
            TestContext.WriteLine("Isolated save/log evidence: " + evidence);
            Assert.That(cleanupErrors, Is.Empty, string.Join("\n", cleanupErrors));
            Assert.That(filesUnchanged, Is.True, "Ordinary file changes are failure evidence; this test never writes back or deletes user files.");
            Assert.That(preferencesUnchanged, Is.True, "Ordinary preference changes are retained as failure evidence, even if restored.");
            Assert.That(guard.preferencesRestored, Is.True);
            app = null; probe = null;
        }
        private void RememberClock(RunState state)
        { if (state?.debugWorkspace == true && !string.IsNullOrEmpty(state.runId)) diagnosticClockKeys.Add("art07.runSeconds." + state.runId); }
        private void Safe(Action action) { try { action(); } catch (Exception ex) { cleanupErrors.Add(ex.ToString()); } }
        private static Dictionary<string, byte[]> CaptureOrdinaryFiles()
        {
            string root = Path.GetFullPath(Application.persistentDataPath);
            return Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(p => p.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar), File.ReadAllBytes, StringComparer.Ordinal) : new Dictionary<string, byte[]>(StringComparer.Ordinal);
        }
        private static bool SameFiles(Dictionary<string, byte[]> a, Dictionary<string, byte[]> b)
        { return a != null && b != null && a.Count == b.Count && a.All(p => b.TryGetValue(p.Key, out var bytes) && p.Value.SequenceEqual(bytes)); }
        private static FileProof[] Proofs(Dictionary<string, byte[]> files)
        { return files == null ? Array.Empty<FileProof>() : files.OrderBy(f => f.Key, StringComparer.Ordinal).Select(f => new FileProof { path = f.Key, bytes = f.Value.LongLength, sha256 = Hash(f.Value) }).ToArray(); }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        private static Preference[] CapturePreferences()
        {
            var values = new List<Preference>();
            foreach (string key in new[] { "fast", "reduced", "shopTab", "art07.textScale", "art07.colorAssist", "art07.instant", "art07.shake", "art07.grain", "art07.quality", "art07.scanlines", "art07.guideSeen", GameApp.ZeroRollHintKey07, GameApp.RandomHintKey07, GameApp.QualifiedHintKey07 })
                values.Add(new Preference { key = key, kind = "int", existed = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key, 0).ToString(CultureInfo.InvariantCulture) });
            foreach (string key in new[] { "volume", "art07.music", "art07.sfx", "art07.ui" })
                values.Add(new Preference { key = key, kind = "float", existed = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetFloat(key, 0).ToString("R", CultureInfo.InvariantCulture) });
            foreach (string action in new[] { "Roll", "Lock", "Clear", "Submit", "Handbook" })
            { string key = "art07.key." + action; values.Add(new Preference { key = key, kind = "string", existed = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetString(key, "") }); }
            return values.ToArray();
        }
        private static void RestorePreference(Preference pref)
        {
            if (!pref.existed) PlayerPrefs.DeleteKey(pref.key);
            else if (pref.kind == "int") PlayerPrefs.SetInt(pref.key, int.Parse(pref.value, CultureInfo.InvariantCulture));
            else if (pref.kind == "float") PlayerPrefs.SetFloat(pref.key, float.Parse(pref.value, CultureInfo.InvariantCulture));
            else PlayerPrefs.SetString(pref.key, pref.value);
        }
    }
}
