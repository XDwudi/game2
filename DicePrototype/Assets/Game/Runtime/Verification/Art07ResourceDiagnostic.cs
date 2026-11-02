using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DiceCraft.Core;
using DiceCraft.Presentation;
using TMPro;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace DiceCraft.Verification
{
    /// <summary>Opt-in real-Player resource evidence. Never installed during ordinary play.</summary>
    public sealed class Art07ResourceDiagnostic : MonoBehaviour
    {
        public const string Argument = "--art07-resource-diagnostic";
        private const double WarmSeconds = 3, IdleSeconds = 20;
        private const int MaxFrames = 30000;
        private static string configuredDirectory, startupFailure;
        private static bool configured;
        private GameApp app;
        private Report report;
        private bool quitWhenDone = true, sampling, observing, finished;
        private double idleStart, idleEnd, deadline;
        private long idleSequence;
        private Vector2 idleMouse;
        private int rowCount, phaseIndex, resourceCount;
        private readonly IdleRow[] rows = new IdleRow[MaxFrames];
        private readonly CounterRow[] resourceRows = new CounterRow[MaxFrames];
        private readonly List<ProfilerRecorderSample> gcBatch = new List<ProfilerRecorderSample>(128);
        private ProfilerRecorder gcRecorder, textureRecorder, drawRecorder, triangleRecorder;
        private readonly List<string> clockKeys = new List<string>();
        private Art07PanelMotion[] panels;
        private Art07PrefabView[] effects;
        private Art07ScreenReveal[] screens;
        private Art07StageCardsMotion[] stageCards;
        private Art07ShopEntrance[] shops;
        private Art07ScoreDrawerMotion[] drawers;
        private string error;

        [Serializable] private sealed class Report
        {
            public string schema = "art07-player-resources-1", status = "running", error, startedUtc, endedUtc;
            public string unity, platform, os, cpu, gpu, graphicsApi, buildGuid, buildIdentity, configVersion, configSha256, runtimeMvid, output, saveDirectory;
            public bool editor, development, wholeReleaseAccepted, startupDisplaySuppressed, ordinaryPreferenceValuesUnchanged, preferencesRestored, appDestroyedBeforeClockCleanup;
            public int width, height, systemMemoryMB, gpuMemoryMB;
            public string displayMode, memoryMethod = "Profiler.GetRuntimeMemorySizeLong per unique native Texture object; Texture Memory counter separately. Not physical GPU residency or independently split CPU/GPU bytes.";
            public string classification = "TMP atlas references -> font; actual GameApp sprite/prefab and scene Renderer references -> art; RenderTexture -> render-target unless explicitly referenced as art; everything else -> unknown. Unknown is never subtracted as free memory.";
            public string geometryScope = "Actual ten ordinary Body renderers; outlines and physical faces separately. Fragment prefab and instantiated pool meshes separately; inactive pool is not active-effect peak proof.";
            public string limitation = "Resource/idle diagnostic only; no highest-combo, UI latency, formal 180-second P95, native display or platform acceptance.";
            public List<Snapshot> snapshots = new List<Snapshot>();
            public List<Phase> phases = new List<Phase>();
            public List<string> notes = new List<string>();
            public Preference[] preferencesBefore, preferencesAfter, preferencesAfterRestore;
            public Boundary idleBefore, idleAfter;
            public bool idleStatePendingAndSavesUnchanged;
            public string[] diagnosticClockKeysRegistered, diagnosticClockKeysRemoved;
            public IdleSummary idle;
        }
        [Serializable] private sealed class Phase { public int index, frame; public double realtime; public string name, rulePhase; public long actionSequence; }
        [Serializable] private sealed class Preference { public string key, kind, value; public bool existed; }
        [Serializable] private sealed class Boundary { public string stateSha256; public string[] pending; public FileHash[] saves; }
        [Serializable] private sealed class FileHash { public string relativePath, sha256; public long bytes; }
        [Serializable] private sealed class TextureRow
        {
            public string entityId, name, type, format, dimension, category;
            public int width, height, mipCount;
            public long nativeBytes;
            public bool nativeSizeAvailable, readable;
            public string[] owners;
        }
        [Serializable] private sealed class FontRow { public string entityId, name, population; public string[] atlasIds, fallbackIds; public int atlasCount; }
        [Serializable] private sealed class MeshRow
        {
            public string owner, category, meshId, meshName;
            public bool sceneInstance, active, rendererEnabled, allTriangleTopology;
            public int vertices, submeshes;
            public long triangles, nativeBytes;
        }
        [Serializable] private sealed class Snapshot
        {
            public string name, phase; public int frame; public double realtime; public long actionSequence;
            public TextureRow[] textures; public FontRow[] fonts; public MeshRow[] meshes;
            public long textureObjectBytes, fontBytes, referencedArtBytes, renderTargetBytes, unknownBytes;
            public int unavailableTextureSizes, activeBodies;
            public long activeBodyTriangles;
            public bool textureCounterAvailable; public long textureCounterBytes;
        }
        [Serializable] private struct IdleRow
        {
            public int unityFrame, observedCounterBatch;
            public double elapsed;
            public long gcBytes, actionSequence;
            public bool gcAvailable, focused, motionIdle, pointerUnchanged, inputInactive;
        }
        [Serializable] private struct CounterRow
        {
            public int unityFrame, phase;
            public double realtime;
            public bool textureAvailable, drawAvailable, triangleAvailable;
            public long textureBytes, draws, triangles;
        }
        [Serializable] private sealed class IdleSummary
        {
            public double requestedSeconds = IdleSeconds, actualSeconds;
            public int rows, validGcRows, missingGcRows, zeroGcRows, nonzeroGcRows, unfocusedRows, movingRows, pointerChangedRows, inputRows, sequenceChangedRows;
            public long maximumGcBytes;
            public bool observationComplete;
            public string scope = "Prewarmed stable ten-die debug table, no probe transactions during window. Reads previous completed Profiler counter batches; 0 samples is missing, not 0 B. Observer arrays preallocated; no enumeration, JSON, screenshot or file I/O during window.";
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            configured = false; configuredDirectory = null; startupFailure = null;
            string[] args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args, Argument);
            if (index < 0) return;
            configured = true;
            // Fail closed even when malformed or combined with another verification mode.
            GameApp.SaveDirectoryOverride = Path.Combine(Application.temporaryCachePath, "ResourceDiagnosticFailure-" + Guid.NewGuid().ToString("N"));
            GameApp.SuppressStartupDisplay = true;
            try
            {
                if (args.Count(a => a == Argument) != 1 || index + 1 >= args.Length || args[index + 1].StartsWith("-", StringComparison.Ordinal))
                    throw new ArgumentException(Argument + " requires one fresh absolute output directory.");
                if (args.Any(a => a.StartsWith("--verify-", StringComparison.Ordinal) || a == "--pacing-experiment" || a == "--deadline-pacing"))
                    throw new ArgumentException("Resource diagnostic cannot run with verification or pacing modes.");
                configuredDirectory = PrepareDirectory(args[index + 1]);
                GameApp.SaveDirectoryOverride = Path.Combine(configuredDirectory, "isolated-saves");
            }
            catch (Exception ex) { startupFailure = ex.ToString(); }
        }
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (!configured) return;
            var host = new GameObject("Art07ResourceDiagnostic_OptIn"); DontDestroyOnLoad(host); host.AddComponent<Art07ResourceDiagnostic>();
        }
        private static string PrepareDirectory(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || !Path.IsPathRooted(path)) throw new ArgumentException("Absolute output path required.");
            string full = Path.GetFullPath(path);
            if (Within(full, Application.dataPath) || Within(full, Application.persistentDataPath) || Within(Application.persistentDataPath, full))
                throw new ArgumentException("Evidence must be separate from Assets and ordinary persistent data.");
            if (Directory.Exists(full) && Directory.EnumerateFileSystemEntries(full).Any()) throw new IOException("Evidence directory must be fresh or empty.");
            Directory.CreateDirectory(full); return full;
        }
        private static bool Within(string path, string root)
        {
            string p = Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string r = Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return p.Equals(r, StringComparison.OrdinalIgnoreCase) || p.StartsWith(r + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
#if UNITY_EDITOR
        // Integration-test entry. Caller owns an already isolated scene; no production injection.
        public static Art07ResourceDiagnostic BeginForTests(GameApp owner, string output)
        {
            if (!Application.isPlaying || owner == null || string.IsNullOrEmpty(GameApp.SaveDirectoryOverride) ||
                !Path.IsPathRooted(GameApp.SaveDirectoryOverride) || Within(GameApp.SaveDirectoryOverride, Application.persistentDataPath) || Within(Application.persistentDataPath, GameApp.SaveDirectoryOverride))
                throw new InvalidOperationException("Tests require a running isolated GameApp.");
            configuredDirectory = PrepareDirectory(output); startupFailure = null;
            var probe = new GameObject("Art07ResourceDiagnostic_Test").AddComponent<Art07ResourceDiagnostic>();
            probe.app = owner; probe.quitWhenDone = false; return probe;
        }
        public bool FinishedForTests => finished;
        public string ErrorForTests => error;
#endif
        private IEnumerator Start()
        {
            report = new Report { startedUtc = DateTime.UtcNow.ToString("O"), unity = Application.unityVersion,
                editor = Application.isEditor, development = Debug.isDebugBuild, platform = Application.platform.ToString(),
                os = SystemInfo.operatingSystem, cpu = SystemInfo.processorType, gpu = SystemInfo.graphicsDeviceName,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), systemMemoryMB = SystemInfo.systemMemorySize,
                gpuMemoryMB = SystemInfo.graphicsMemorySize, buildGuid = Application.buildGUID,
                runtimeMvid = typeof(GameApp).Module.ModuleVersionId.ToString(), output = configuredDirectory,
                saveDirectory = GameApp.SaveDirectoryOverride, startupDisplaySuppressed = GameApp.SuppressStartupDisplay,
                preferencesBefore = ReadPreferences() };
            deadline = Time.realtimeSinceStartupAsDouble + 180;
            yield return Guarded(Run());
            sampling = observing = false;
            TryCleanup(RegisterClock, "register final diagnostic clock");
            TryCleanup(() => gcRecorder.Dispose(), "dispose GC recorder");
            TryCleanup(() => textureRecorder.Dispose(), "dispose texture recorder");
            TryCleanup(() => drawRecorder.Dispose(), "dispose draw recorder");
            TryCleanup(() => triangleRecorder.Dispose(), "dispose triangle recorder");
            // GameApp.OnDestroy writes its new run clock. Clean ONLY this diagnostic's keys afterward.
            if (app != null) { TryCleanup(() => Destroy(app), "destroy diagnostic GameApp"); yield return null; }
            report.appDestroyedBeforeClockCleanup = app == null;
            report.diagnosticClockKeysRegistered = clockKeys.ToArray();
            if (report.appDestroyedBeforeClockCleanup) foreach (string key in clockKeys) TryCleanup(() => PlayerPrefs.DeleteKey(key), "remove diagnostic clock " + key);
            else error = (error ?? "") + " GameApp still exists; clock cleanup cannot be guaranteed.";
            if (clockKeys.Count > 0) TryCleanup(PlayerPrefs.Save, "flush diagnostic clock cleanup");
            var removedClockKeys = new List<string>();
            foreach (string key in clockKeys) TryCleanup(() => {
                if (!PlayerPrefs.HasKey(key)) removedClockKeys.Add(key);
                else error = (error ?? "") + " Diagnostic clock remains: " + key;
            }, "verify diagnostic clock removal " + key);
            report.diagnosticClockKeysRemoved = removedClockKeys.ToArray();
            TryCleanup(() => {
                report.preferencesAfter = ReadPreferences();
                report.ordinaryPreferenceValuesUnchanged = SamePreferences(report.preferencesBefore, report.preferencesAfter);
                if (!report.ordinaryPreferenceValuesUnchanged)
                {
                    error = (error ?? "") + " Ordinary preference values changed; original values restored and both snapshots retained.";
                    foreach (var preference in report.preferencesBefore) TryCleanup(() => RestorePreference(preference), "restore " + preference.key);
                    PlayerPrefs.Save();
                }
                report.preferencesAfterRestore = ReadPreferences(); report.preferencesRestored = SamePreferences(report.preferencesBefore, report.preferencesAfterRestore);
            }, "verify and restore ordinary preferences");
            report.error = error; report.endedUtc = DateTime.UtcNow.ToString("O");
            report.status = error != null ? "diagnostic_failed" : report.idle?.observationComplete == true ? "diagnostic_completed_not_acceptance" : "diagnostic_incomplete";
            try { WriteOutput(); } catch (Exception ex) { error = (error ?? "") + " Evidence write failed: " + ex; Debug.LogError(error); }
            finished = true;
            if (quitWhenDone) Application.Quit(error != null ? 1 : report.idle?.observationComplete == true ? 0 : 3);
        }
        private IEnumerator Guarded(IEnumerator body)
        {
            var stack = new Stack<IEnumerator>(); stack.Push(body);
            while (stack.Count > 0)
            {
                bool moved = false; object current = null;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception ex) { error = ex.ToString(); break; }
                if (!moved) { TryCleanup(() => (stack.Pop() as IDisposable)?.Dispose(), "completed iterator cleanup"); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
            while (stack.Count > 0) TryCleanup(() => (stack.Pop() as IDisposable)?.Dispose(), "failed iterator cleanup");
        }
        private IEnumerator Run()
        {
            if (startupFailure != null) throw new InvalidOperationException(startupFailure);
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null || Application.isBatchMode && !Application.isEditor)
                throw new InvalidOperationException("Actual visible graphics Player required; Null graphics/headless Player is not resource evidence.");
            while (app == null || !app.StartupReady07 || app.Session == null)
            { if (app == null) app = FindAnyObjectByType<GameApp>(); CheckDeadline(); yield return null; }
            if (app.ViewState != null) throw new InvalidOperationException("Expected fresh diagnostic menu with no loaded run.");
            report.width = Screen.width; report.height = Screen.height; report.displayMode = Screen.fullScreenMode.ToString();
            report.configVersion = app.Config.configVersion; report.configSha256 = Hash(Encoding.UTF8.GetBytes(app.configAsset.text)); report.buildIdentity = Resources.Load<TextAsset>("BuildIdentity")?.text;
            gcRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 128);
            textureRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Texture Memory", 1);
            drawRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
            triangleRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
            observing = true;
            PhaseStart("baseline-menu"); yield return Stable(); SnapshotNow("baseline-menu");
            yield return Press("MenuHandbook"); yield return Stable(); SnapshotNow("handbook-hands");
            yield return Press("HandbookColors"); yield return Stable(); SnapshotNow("handbook-colors"); yield return Press("CloseDialog");
            yield return Press("MenuSettings");
            for (int tab = 0; tab < 3; tab++) { yield return Press("SettingsTab" + tab); yield return Stable(); SnapshotNow("settings-tab-" + tab); }
            // Done flushes unchanged preferences by the normal UI contract; do not describe it as no disk write.
            yield return Press("SettingsDone");
            if (!app.UnlockDebug("XD is god") || !app.EnterDebugWorkspace()) throw new InvalidOperationException("Cannot enter real isolated debug workspace.");
            RegisterClock();
            var shopScene = Art07Fixtures.Create(app.Config, Art07FixtureKind.Qualified, app.ViewState.nextItemSerial, app.ViewState.nextDieSerial);
            yield return Apply(shopScene); yield return Send(CommandKind.Submit);
            if (app.ViewState.phase != "StageSettlement") yield return Send(CommandKind.EndStage);
            yield return Send(CommandKind.Continue);
            if (app.ViewState.phase != "Shop") throw new InvalidOperationException("Actual settlement did not enter Shop.");
            PhaseStart("actual-shop-current-tab"); yield return Stable(); SnapshotNow("actual-shop-current-tab");
            report.notes.Add("Current saved shop tab viewed; no Tab buttons pressed because those write the user's shopTab preference. All loaded textures remain inventoried.");
            var ten = Art07Fixtures.Create(app.Config, Art07FixtureKind.MaximumTable, app.ViewState.nextItemSerial, app.ViewState.nextDieSerial);
            foreach (var die in ten.dice) { die.isGlass = false; foreach (var face in die.faces) { face.isGold = false; face.texture = "none"; } }
            yield return Apply(ten); PhaseStart("ten-ordinary-dice-after-ui-visits"); yield return Stable();
            SnapshotNow("ten-ordinary-dice-after-ui-visits");
            if (report.snapshots[report.snapshots.Count - 1].activeBodies != 10) throw new InvalidOperationException("Expected ten actual active Body renderers.");
            if (EventSystem.current != null) EventSystem.current.SetSelectedGameObject(null);
            PhaseStart("idle-warmup-and-observation"); report.idleBefore = ReadBoundary();
            yield return Stable();
            // All observer allocations/phase objects/hashes precede warmup. Two full unsampled
            // frames and a counter reset prevent setup work entering previous-frame GC samples.
            yield return null; yield return null;
            gcBatch.Clear(); if (gcRecorder.Valid) { gcRecorder.Reset(); gcRecorder.Start(); }
            yield return null;
            idleSequence = app.Session.ActionSequence; idleMouse = Mouse.current == null ? Vector2.zero : Mouse.current.position.ReadValue();
            idleStart = Time.realtimeSinceStartupAsDouble; sampling = true;
            while (Time.realtimeSinceStartupAsDouble - idleStart < IdleSeconds) { CheckDeadline(); yield return null; }
            idleEnd = Time.realtimeSinceStartupAsDouble; sampling = false;
            report.idleAfter = ReadBoundary();
            report.idleStatePendingAndSavesUnchanged = JsonUtility.ToJson(report.idleBefore) == JsonUtility.ToJson(report.idleAfter);
            report.idle = SummarizeIdle();
            PhaseStart("after-idle"); SnapshotNow("after-idle");
            if (Screen.width != report.width || Screen.height != report.height || Screen.fullScreenMode.ToString() != report.displayMode)
                throw new InvalidOperationException("Display changed during diagnostic; probe did not request a change.");
        }
        private IEnumerator Apply(DebugScene scene)
        {
            PhaseStart("applying-debug-resource-scene");
            scene.sourceRunId = app.ViewState.sourceRunId;
            var result = app.Send(new SessionCommand { kind = CommandKind.ApplyDebugScene, debugScene = scene });
            if (!result.accepted || result.pendingSave) throw new InvalidOperationException("Debug scene rejected: " + result.error);
            var close = app.canvas.GetComponentsInChildren<Button>(true).FirstOrDefault(b => b.gameObject.activeInHierarchy && b.name == "RunDebugScene");
            if (close != null) close.onClick.Invoke();
            yield return Stable();
        }
        private IEnumerator Send(CommandKind kind)
        {
            PhaseStart("command:" + kind);
            var result = app.Send(new SessionCommand { kind = kind });
            if (!result.accepted || result.pendingSave) throw new InvalidOperationException(kind + " rejected: " + result.error);
            yield return Stable();
        }
        private IEnumerator Press(string name)
        {
            PhaseStart("ui:" + name);
            var matches = app.canvas.GetComponentsInChildren<Button>(true).Where(b => b.name == name && b.gameObject.activeInHierarchy && b.IsInteractable()).ToArray();
            if (matches.Length != 1) throw new InvalidOperationException("Expected one active interactable button: " + name);
            matches[0].onClick.Invoke(); yield return null;
        }
        private void PhaseStart(string name)
        {
            phaseIndex = report.phases.Count;
            report.phases.Add(new Phase { index = phaseIndex, name = name, frame = Time.frameCount, realtime = Time.realtimeSinceStartupAsDouble,
                rulePhase = app?.ViewState?.phase ?? "MainMenu", actionSequence = app?.Session?.ActionSequence ?? 0 });
        }
        private void CacheMotionChecks()
        {
            panels = Resources.FindObjectsOfTypeAll<Art07PanelMotion>(); effects = Resources.FindObjectsOfTypeAll<Art07PrefabView>();
            screens = Resources.FindObjectsOfTypeAll<Art07ScreenReveal>(); stageCards = Resources.FindObjectsOfTypeAll<Art07StageCardsMotion>();
            shops = Resources.FindObjectsOfTypeAll<Art07ShopEntrance>(); drawers = Resources.FindObjectsOfTypeAll<Art07ScoreDrawerMotion>();
        }
        private IEnumerator Stable()
        {
            CacheMotionChecks(); double stableSince = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - stableSince < WarmSeconds)
            { CheckDeadline(); if (!MotionIdle() || !Application.isFocused || !InputInactive()) stableSince = Time.realtimeSinceStartupAsDouble; yield return null; }
        }
        private bool MotionIdle()
        {
            if (app == null || app.IsBusy || app.Session.HasPendingSave || app.table.IsRolling || app.table.IsTransitioning || app.PresentationPlaying07 ||
                app.TransactionVisualPlaying07 || app.SettlementRevealPlaying07 || app.StageCardExitPlaying07 || app.LockVisualPlaying07 ||
                app.QualifiedStampPlaying07 || app.ActivePermanentFaceTransitions07 != 0 || app.ActiveArtEffects07 != 0 || app.ActiveCardPunches07 != 0) return false;
            if (panels != null) foreach (var v in panels) if (v != null && v.isActiveAndEnabled && v.IsPlaying) return false;
            if (effects != null) foreach (var v in effects) if (v != null && v.gameObject.scene.IsValid() && v.gameObject.activeInHierarchy && v.IsPlaying) return false;
            if (screens != null) foreach (var v in screens) if (v != null && v.isActiveAndEnabled && v.IsPlaying) return false;
            if (stageCards != null) foreach (var v in stageCards) if (v != null && v.isActiveAndEnabled && v.IsPlaying) return false;
            if (shops != null) foreach (var v in shops) if (v != null && v.isActiveAndEnabled && v.IsPlaying) return false;
            if (drawers != null) foreach (var v in drawers) if (v != null && v.isActiveAndEnabled && v.IsPlaying) return false;
            return true;
        }
        private static bool InputInactive()
        {
            var mouse = Mouse.current; var key = Keyboard.current;
            return (key == null || !key.anyKey.isPressed) && (mouse == null ||
                !mouse.leftButton.isPressed && !mouse.rightButton.isPressed && !mouse.middleButton.isPressed && mouse.delta.ReadValue().sqrMagnitude == 0 && mouse.scroll.ReadValue().sqrMagnitude == 0);
        }
        private void CheckDeadline() { if (error != null) throw new InvalidOperationException(error); if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Resource diagnostic exceeded its bounded 180-second execution deadline."); }
        private static bool ReadCounter(ProfilerRecorder recorder, out long value)
        { value = 0; if (!recorder.Valid || !recorder.IsRunning || recorder.Count == 0) return false; value = recorder.LastValue; return value >= 0; }
        private void LateUpdate()
        {
            if (!observing || finished) return;
            try { ObserveFrame(); }
            catch (Exception ex) { error = "Observer failure: " + ex; sampling = observing = false; }
        }
        private void ObserveFrame()
        {
            if (!observing || finished) return;
            if (resourceCount < resourceRows.Length)
            {
                var row = new CounterRow { unityFrame = Time.frameCount, phase = phaseIndex, realtime = Time.realtimeSinceStartupAsDouble };
                row.textureAvailable = ReadCounter(textureRecorder, out row.textureBytes); row.drawAvailable = ReadCounter(drawRecorder, out row.draws); row.triangleAvailable = ReadCounter(triangleRecorder, out row.triangles);
                resourceRows[resourceCount++] = row;
            }
            else { error = "Resource observation buffer exhausted; no growth permitted."; observing = sampling = false; return; }
            gcBatch.Clear(); bool healthy = gcRecorder.Valid && gcRecorder.IsRunning && !gcRecorder.WrappedAround;
            if (gcRecorder.Valid) { gcRecorder.CopyTo(gcBatch, true); if (!gcRecorder.IsRunning) gcRecorder.Start(); }
            if (!sampling) return;
            if (rowCount >= rows.Length) { error = "Idle buffer exhausted; no growth permitted."; sampling = false; return; }
            bool available = healthy && gcBatch.Count == 1 && gcBatch[0].Value >= 0;
            rows[rowCount++] = new IdleRow { unityFrame = Time.frameCount, elapsed = Time.realtimeSinceStartupAsDouble - idleStart,
                observedCounterBatch = gcBatch.Count, gcAvailable = available, gcBytes = available ? gcBatch[0].Value : 0,
                focused = Application.isFocused, motionIdle = MotionIdle(), actionSequence = app.Session.ActionSequence,
                pointerUnchanged = Mouse.current == null || Mouse.current.position.ReadValue() == idleMouse, inputInactive = InputInactive() };
        }
        private IdleSummary SummarizeIdle()
        {
            var summary = new IdleSummary { actualSeconds = idleEnd - idleStart, rows = rowCount };
            for (int i = 0; i < rowCount; i++)
            {
                var r = rows[i]; if (r.gcAvailable) { summary.validGcRows++; if (r.gcBytes == 0) summary.zeroGcRows++; else summary.nonzeroGcRows++; summary.maximumGcBytes = Math.Max(summary.maximumGcBytes, r.gcBytes); } else summary.missingGcRows++;
                if (!r.focused) summary.unfocusedRows++; if (!r.motionIdle) summary.movingRows++; if (!r.pointerUnchanged) summary.pointerChangedRows++;
                if (!r.inputInactive) summary.inputRows++; if (r.actionSequence != idleSequence) summary.sequenceChangedRows++;
            }
            // Completion assesses observation conditions, NOT a stricter zero-GC performance requirement.
            summary.observationComplete = summary.actualSeconds >= IdleSeconds && rowCount > 0 && summary.missingGcRows == 0 && summary.unfocusedRows == 0 &&
                summary.movingRows == 0 && summary.pointerChangedRows == 0 && summary.inputRows == 0 && summary.sequenceChangedRows == 0 && report.idleStatePendingAndSavesUnchanged;
            return summary;
        }
        private static string Id(Object value) => value == null ? "" : value.GetEntityId().ToString();
        private void SnapshotNow(string name)
        {
            if (sampling) throw new InvalidOperationException("Object enumeration must stay outside idle measurement.");
            var owners = new Dictionary<string, HashSet<string>>(); var fontIds = new HashSet<string>(); var artIds = new HashSet<string>();
            Action<Texture, string, bool> use = (texture, owner, isFont) => {
                if (texture == null) return; string id = Id(texture); if (!owners.TryGetValue(id, out var refs)) owners[id] = refs = new HashSet<string>();
                refs.Add(owner); if (isFont) fontIds.Add(id); else artIds.Add(id);
            };
            var fontRows = new List<FontRow>();
            foreach (var font in Resources.FindObjectsOfTypeAll<TMP_FontAsset>())
            {
                var atlases = font.atlasTextures ?? Array.Empty<Texture2D>();
                foreach (var texture in atlases) use(texture, "TMP:" + font.name + ":atlas", true);
                fontRows.Add(new FontRow { entityId = Id(font), name = font.name, population = font.atlasPopulationMode.ToString(),
                    atlasCount = atlases.Length, atlasIds = atlases.Select(Id).ToArray(), fallbackIds = (font.fallbackFontAssetTable ?? new List<TMP_FontAsset>()).Select(Id).ToArray() });
            }
            Action<Sprite, string> sprite = (s, owner) => { if (s != null) { use(s.texture, owner, false); use(s.associatedAlphaSplitTexture, owner + ":alpha", false); } };
            foreach (var s in app.contentIcons ?? Array.Empty<Sprite>()) sprite(s, "GameApp.contentIcons:" + s?.name);
            foreach (var s in app.enchantIcons ?? Array.Empty<Sprite>()) sprite(s, "GameApp.enchantIcons:" + s?.name);
            foreach (var s in app.art07Templates ?? Array.Empty<Sprite>()) sprite(s, "GameApp.art07Templates:" + s?.name);
            Action<Material, string> material = (m, owner) => { if (m != null) foreach (int key in m.GetTexturePropertyNameIDs()) use(m.GetTexture(key), owner + ":material:" + m.name + ":property:" + key, false); };
            foreach (var prefab in app.art07Prefabs ?? Array.Empty<Art07PrefabView>())
            {
                if (prefab == null) continue;
                foreach (var image in prefab.GetComponentsInChildren<Image>(true)) sprite(image.sprite, "Art07Prefab:" + prefab.assetId + ":Image:" + image.name);
                foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true)) foreach (var m in renderer.sharedMaterials) material(m, "Art07Prefab:" + prefab.assetId + ":Renderer:" + renderer.name);
            }
            foreach (var renderer in FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (renderer.gameObject.scene.IsValid()) foreach (var m in renderer.sharedMaterials) material(m, "SceneRenderer:" + Hierarchy(renderer.transform));
            var textureRows = new List<TextureRow>(); var seenTextureIds = new HashSet<string>();
            var snap = new Snapshot { name = name, frame = Time.frameCount, realtime = Time.realtimeSinceStartupAsDouble, phase = app.ViewState?.phase ?? "MainMenu", actionSequence = app.Session.ActionSequence };
            foreach (var texture in Resources.FindObjectsOfTypeAll<Texture>())
            {
                string id = Id(texture);
                if (!seenTextureIds.Add(id)) throw new InvalidOperationException("Duplicate loaded native texture identity: " + id);
                long bytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(texture);
                string category = fontIds.Contains(id) ? "font" : artIds.Contains(id) ? "referenced-art" : texture is RenderTexture ? "render-target" : "unknown";
                var row = new TextureRow { entityId = id, name = texture.name, type = texture.GetType().Name, dimension = texture.dimension.ToString(),
                    format = texture.graphicsFormat.ToString(), width = texture.width, height = texture.height, mipCount = texture.mipmapCount,
                    readable = texture is Texture2D two && two.isReadable, nativeBytes = bytes, nativeSizeAvailable = bytes > 0, category = category,
                    owners = owners.TryGetValue(id, out var refs) ? refs.OrderBy(s => s, StringComparer.Ordinal).ToArray() : Array.Empty<string>() };
                textureRows.Add(row); snap.textureObjectBytes += bytes;
                if (bytes <= 0) snap.unavailableTextureSizes++;
                if (category == "font") snap.fontBytes += bytes; else if (category == "referenced-art") snap.referencedArtBytes += bytes;
                else if (category == "render-target") snap.renderTargetBytes += bytes; else snap.unknownBytes += bytes;
            }
            snap.textures = textureRows.OrderBy(t => t.entityId, StringComparer.Ordinal).ToArray(); snap.fonts = fontRows.ToArray();
            snap.textureCounterAvailable = ReadCounter(textureRecorder, out snap.textureCounterBytes);
            var meshes = new List<MeshRow>();
            if (app.ViewState != null) foreach (var die in app.ViewState.dice)
            {
                var orientation = app.table.GetFaceOrientation(die.dieId); if (orientation == null) continue;
                foreach (var filter in orientation.GetComponentsInChildren<MeshFilter>(true))
                {
                    string category = filter.name == "Body" ? "ordinary-body" : filter.name == "InkContour" ? "body-outline-pass" : filter.name.StartsWith("Face_F", StringComparison.Ordinal) ? "physical-face" : "other-die-mesh";
                    var row = MeshEntry(filter, die.dieId + "/" + filter.name, category); meshes.Add(row);
                    if (category == "ordinary-body" && row.active && row.rendererEnabled) { snap.activeBodies++; snap.activeBodyTriangles += row.triangles; }
                }
            }
            foreach (var view in Resources.FindObjectsOfTypeAll<Art07PrefabView>()) if (view.assetId == "fx_dice_break")
                foreach (var filter in view.GetComponentsInChildren<MeshFilter>(true)) meshes.Add(MeshEntry(filter, "fx_dice_break/" + Id(view) + "/" + filter.name, view.gameObject.scene.IsValid() ? "fragment-pool-instance" : "fragment-prefab-reference"));
            snap.meshes = meshes.ToArray(); report.snapshots.Add(snap);
        }
        private static MeshRow MeshEntry(MeshFilter filter, string owner, string category)
        {
            Mesh mesh = filter.sharedMesh; var renderer = filter.GetComponent<Renderer>();
            var row = new MeshRow { owner = owner, category = category, sceneInstance = filter.gameObject.scene.IsValid(),
                active = filter.gameObject.activeInHierarchy, rendererEnabled = renderer != null && renderer.enabled,
                meshId = Id(mesh), meshName = mesh == null ? "" : mesh.name, allTriangleTopology = mesh != null };
            if (mesh == null) return row;
            row.vertices = mesh.vertexCount; row.submeshes = mesh.subMeshCount; row.nativeBytes = UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(mesh);
            for (int s = 0; s < mesh.subMeshCount; s++) { if (mesh.GetTopology(s) == MeshTopology.Triangles) row.triangles += mesh.GetIndexCount(s) / 3; else row.allTriangleTopology = false; }
            return row;
        }
        private static string Hierarchy(Transform t) { string path = t.name; while (t.parent != null) { t = t.parent; path = t.name + "/" + path; } return path; }
        private void RegisterClock()
        {
            if (app == null) return;
            // EnterDebugWorkspace writes the clock before Refresh. The authoritative session
            // therefore also covers an exception before ViewState receives the new run.
            // This clone is only read outside the idle observation window.
            TryCleanup(() => RegisterClockState(app.Session?.State), "read authoritative diagnostic clock");
            TryCleanup(() => RegisterClockState(app.ViewState), "read presented diagnostic clock");
        }
        private void RegisterClockState(RunState state)
        {
            if (state?.debugWorkspace != true || string.IsNullOrEmpty(state.runId)) return;
            string key = "art07.runSeconds." + state.runId;
            if (!clockKeys.Contains(key)) clockKeys.Add(key);
        }
        private void TryCleanup(Action action, string label)
        { try { action(); } catch (Exception ex) { error = (error ?? "") + " Cleanup " + label + ": " + ex; } }
        private static bool SamePreferences(Preference[] a, Preference[] b) => JsonUtility.ToJson(new PreferenceList { values = a }) == JsonUtility.ToJson(new PreferenceList { values = b });
        private static void RestorePreference(Preference p)
        {
            if (!p.existed) PlayerPrefs.DeleteKey(p.key);
            else if (p.kind == "int") PlayerPrefs.SetInt(p.key, int.Parse(p.value, CultureInfo.InvariantCulture));
            else if (p.kind == "float") PlayerPrefs.SetFloat(p.key, float.Parse(p.value, CultureInfo.InvariantCulture));
            else PlayerPrefs.SetString(p.key, p.value);
        }
        private Boundary ReadBoundary()
        {
            if (sampling) throw new InvalidOperationException("State/file hashing must stay outside idle observation.");
            string root = Path.GetFullPath(GameApp.SaveDirectoryOverride);
            return new Boundary { stateSha256 = Hash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(app.Session.State))), pending = app.Session.PendingIds,
                saves = Directory.Exists(root) ? Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal).Select(path =>
                    new FileHash { relativePath = path.Substring(root.Length).TrimStart(Path.DirectorySeparatorChar), bytes = new FileInfo(path).Length, sha256 = Hash(File.ReadAllBytes(path)) }).ToArray() : Array.Empty<FileHash>() };
        }
        private static string Hash(byte[] bytes) { using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
        [Serializable] private sealed class PreferenceList { public Preference[] values; }
        private static Preference[] ReadPreferences()
        {
            var rows = new List<Preference>();
            foreach (string key in new[] { "fast", "reduced", "shopTab", "art07.textScale", "art07.colorAssist", "art07.instant", "art07.shake", "art07.grain", "art07.quality", "art07.scanlines", "art07.guideSeen", GameApp.ZeroRollHintKey07, GameApp.RandomHintKey07, GameApp.QualifiedHintKey07 })
                rows.Add(new Preference { key = key, kind = "int", existed = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetInt(key, 0).ToString(CultureInfo.InvariantCulture) });
            foreach (string key in new[] { "volume", "art07.music", "art07.sfx", "art07.ui" })
                rows.Add(new Preference { key = key, kind = "float", existed = PlayerPrefs.HasKey(key), value = PlayerPrefs.GetFloat(key, 0).ToString("R", CultureInfo.InvariantCulture) });
            foreach (string key in new[] { "Roll", "Lock", "Clear", "Submit", "Handbook" })
                rows.Add(new Preference { key = "art07.key." + key, kind = "string", existed = PlayerPrefs.HasKey("art07.key." + key), value = PlayerPrefs.GetString("art07.key." + key, "") });
            return rows.ToArray();
        }
        private void WriteOutput()
        {
            if (string.IsNullOrEmpty(configuredDirectory)) { Debug.LogError(report.error ?? "Resource diagnostic has no usable evidence directory."); return; }
            File.WriteAllText(Path.Combine(configuredDirectory, "resource-report.json"), JsonUtility.ToJson(report, true), new UTF8Encoding(false));
            var culture = CultureInfo.InvariantCulture;
            using (var writer = new StreamWriter(Path.Combine(configuredDirectory, "idle-frames.csv"), false, new UTF8Encoding(false)))
            {
                writer.WriteLine("unityFrame,elapsed_s,gcBytes,gcAvailable,counterBatch,focused,motionIdle,pointerUnchanged,inputInactive,actionSequence");
                for (int i = 0; i < rowCount; i++) { var r = rows[i]; writer.WriteLine(string.Join(",", r.unityFrame, r.elapsed.ToString("R", culture), r.gcAvailable ? r.gcBytes.ToString(culture) : "", r.gcAvailable, r.observedCounterBatch, r.focused, r.motionIdle, r.pointerUnchanged, r.inputInactive, r.actionSequence)); }
            }
            using (var writer = new StreamWriter(Path.Combine(configuredDirectory, "resource-frames.csv"), false, new UTF8Encoding(false)))
            {
                writer.WriteLine("unityFrame,realtime_s,phase,textureBytes,draws,triangles");
                for (int i = 0; i < resourceCount; i++) { var r = resourceRows[i]; writer.WriteLine(string.Join(",", r.unityFrame, r.realtime.ToString("R", culture), r.phase,
                    r.textureAvailable ? r.textureBytes.ToString(culture) : "", r.drawAvailable ? r.draws.ToString(culture) : "", r.triangleAvailable ? r.triangles.ToString(culture) : "")); }
            }
        }
        private void OnDestroy() { observing = sampling = false; gcRecorder.Dispose(); textureRecorder.Dispose(); drawRecorder.Dispose(); triangleRecorder.Dispose(); }
    }
}
