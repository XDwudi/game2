using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DiceCraft.Core;
using DiceCraft.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft.Verification
{
    public enum Art07RecordingMode { NormalHighest, FastHighest, ReducedHighest, ContinuousHighest, NormalSample, NormalSharedHalf, FastSharedHalf, ReducedSharedHalf, NormalZero, InstantHighest, NormalPost, ReducedPost, NormalQualified, FastPost }

    /// <summary>
    /// Explicit, isolated debug-only visual evidence recorder. ScreenCapture includes the real
    /// composited UI. Fixed presentation time is an offline inspection clock, never an FPS result.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public sealed partial class Art07VisualRecorder : MonoBehaviour
    {
        public const int FramesPerSecond = 60;
        private const float Step = 1f / FramesPerSecond;
        private static Art07VisualRecorder active;
        private static readonly string[] PreferenceKeys = { "fast", "reduced", "art07.instant" };
        [Serializable] private sealed class Preference { public string key; public bool existed; public int value; }
        [Serializable] private sealed class Frame
        {
            public int index, unityFrame, cycle, width, height, presentationTicks, nonblockingTicks, chainCount, comboTier;
            public double videoSeconds, presentationSeconds, realtimeSeconds, unscaledDeltaSeconds;
            public string file, pngSha256, stage, command, receiptId, score, multiplierNumerator, multiplierDenominator, stateHash, saveSha256, normalSaveSha256;
            public bool presentationPlaying, contentValid, fixedClockObserved;
            public int sampledPixels, quantizedColors;
            public double nonBlackRatio, brightRatio, meanLuminance, highContrastAreaRatio, redBlueSwapAreaRatio;
            public TransactionFrame07 transaction;
        }
        [Serializable] private sealed class Cycle
        {
            public int index, firstFrame, lastFrame, durationMilliseconds, clockTicks;
            public string command, receiptId, stateHashBefore, stateHashAfter, saveShaBefore, saveShaAfter;
            public bool completed, stateAndSaveStable;
        }
        [Serializable] private sealed class Report
        {
            public string schema = "art07-visual-recording-1", status = "running", error, restoreError, startedUtc, endedUtc;
            public string outputDirectory, mode, fixture, artVersion, configVersion, configSha256, unityVersion, runtimeAssemblyMvid, coreAssemblyMvid, recorderSourceSha256, buildGuid;
            public string platform, os, gpu, graphicsApi, buildIdentity, saveDirectory, normalSaveShaBefore, normalSaveShaAfter, debugSaveShaBefore, debugSaveShaAfter;
            public string captureMethod = "ScreenCapture.CaptureScreenshotAsTexture after WaitForEndOfFrame", clock = "offline fixed presentation timestep 1/60; not a realtime performance result";
            public int framesPerSecond = FramesPerSecond, frames, width, height, requestedCycles, sourceTickCalls, nonblockingTickCalls;
            public bool editor, batchMode, reducedMotion, fixedClockIntegrated, restoredTiming, restoredPreferences, normalSaveUnchanged;
            public float previousCaptureDeltaTime;
            public string transactionClip,transactionSourceManifestSha256;
            public Preference[] previousPreferences;
            public List<Cycle> cycles = new List<Cycle>();
        }

        private GameApp app;
        private Report report;
        private Art07RecordingMode mode;
        private PresentationReceipt receipt;
        private StreamWriter frameWriter;
        private Coroutine runner;
        private bool finishing, clockEnabled;
        private int frameIndex, lastCapturedUnityFrame = -1, totalTicks, cycleTicks, tickFrame = -1, ticksInFrame;
        private int totalNonblockingTicks,nonblockingTickFrame=-1,nonblockingTicksInFrame;
        private string expectedStateHash, expectedSaveHash, normalSavePath;
        private double startedAt, lastProgressAt;
        private Color32[] priorSamples;
        public bool IsRecording => report != null && report.status == "running";
        public string OutputDirectory => report?.outputDirectory;
        public string Status => report?.status ?? "not_started";
        public string Error => report?.error;

        /// <summary>Call only from GameApp's presentation Tick, never from a rule clock or RNG.</summary>
        public static float PresentationDeltaTime(GameApp owner, float actualUnscaledDelta)
        {
            var recorder = active;
            if (recorder == null || !recorder.IsRecording || !recorder.clockEnabled || recorder.app != owner) return actualUnscaledDelta;
            if(recorder.nonblockingTickFrame==Time.frameCount&&recorder.nonblockingTicksInFrame>0){recorder.Finish("failed","Blocking and nonblocking clocks both advanced in one rendered frame.");return 0;}
            if (recorder.tickFrame != Time.frameCount) { recorder.tickFrame = Time.frameCount; recorder.ticksInFrame = 0; }
            recorder.ticksInFrame++; recorder.totalTicks++; recorder.cycleTicks++;
            return Step;
        }
        public static float NonblockingDeltaTime(GameApp owner,float actualUnscaledDelta)
        {
            var recorder=active;
            if(recorder==null||!recorder.IsRecording||!recorder.clockEnabled||recorder.app!=owner)return actualUnscaledDelta;
            if(recorder.nonblockingTickFrame!=Time.frameCount){recorder.nonblockingTickFrame=Time.frameCount;recorder.nonblockingTicksInFrame=0;}
            if(recorder.nonblockingTicksInFrame>0||recorder.tickFrame==Time.frameCount&&recorder.ticksInFrame>0)
            {recorder.Finish("failed","Nonblocking tail advanced twice or shared a blocking clock tick in one rendered frame.");return 0;}
            recorder.nonblockingTicksInFrame++;recorder.totalNonblockingTicks++;return Step;
        }

        public static Art07VisualRecorder Begin(GameApp owner, string absoluteOutputDirectory, Art07RecordingMode mode, int continuousCycles = 3)
            => BeginCore07(owner,absoluteOutputDirectory,mode,continuousCycles,Art07TransactionClip.None);
        private static Art07VisualRecorder BeginCore07(GameApp owner,string absoluteOutputDirectory,Art07RecordingMode mode,int continuousCycles,Art07TransactionClip transactionClip)
        {
            if (!Application.isPlaying || owner == null || !owner.StartupReady07 || owner.Session == null) throw new InvalidOperationException("Start recording on a running GameApp.");
            if (active != null && active.IsRecording) throw new InvalidOperationException("Another art recording is active.");
            if (owner.IsBusy || owner.Session.HasPendingSave || !owner.IsDebugWorkspace) throw new InvalidOperationException("Enter an idle isolated debug workspace before recording.");
            if (string.IsNullOrWhiteSpace(GameApp.SaveDirectoryOverride) || !Path.IsPathRooted(GameApp.SaveDirectoryOverride)) throw new InvalidOperationException("Set a unique absolute SaveDirectoryOverride before GameApp.Awake; normal user storage is not a recording workspace.");
            string saveRoot = Path.GetFullPath(GameApp.SaveDirectoryOverride);
            if (!IsWithin(owner.SaveLocation, saveRoot) || IsWithin(saveRoot, Application.persistentDataPath)) throw new InvalidOperationException("The running app must use the isolated override, outside normal user storage.");
            if (string.IsNullOrWhiteSpace(absoluteOutputDirectory) || !Path.IsPathRooted(absoluteOutputDirectory)) throw new ArgumentException("Use a complete absolute artifact directory path.");
            string output = Path.GetFullPath(absoluteOutputDirectory);
            if (IsWithin(output, Application.dataPath)) throw new ArgumentException("Record outside Assets so PNG evidence is not imported as game content.");
            if (Directory.Exists(output) && Directory.EnumerateFileSystemEntries(output).Any()) throw new IOException("Recording output must be new or empty; old evidence is never overwritten.");
            if (!Enum.IsDefined(typeof(Art07RecordingMode), mode) || continuousCycles < 2 || continuousCycles > 10) throw new ArgumentOutOfRangeException(nameof(mode));
            Directory.CreateDirectory(output); Directory.CreateDirectory(Path.Combine(output, "frames"));
            var node = new GameObject("Art07VisualRecorder_Explicit"); var recorder = node.AddComponent<Art07VisualRecorder>();
            recorder.app = owner; recorder.mode = mode; recorder.transactionClip07=transactionClip; recorder.normalSavePath = Path.Combine(saveRoot, "active-run.json");
            recorder.startedAt = recorder.lastProgressAt = Time.realtimeSinceStartupAsDouble;
            recorder.report = new Report { outputDirectory = output, mode = mode.ToString(), fixture = FixtureFor(mode).ToString(),
                artVersion = GameApp.ArtVersion, configVersion = owner.Config.configVersion, configSha256 = Hash(Encoding.UTF8.GetBytes(owner.configAsset.text)), startedUtc = DateTime.UtcNow.ToString("O"),
                unityVersion = Application.unityVersion, runtimeAssemblyMvid = typeof(GameApp).Module.ModuleVersionId.ToString(), coreAssemblyMvid = typeof(GameSession).Module.ModuleVersionId.ToString(),
                recorderSourceSha256 = HashFile(Path.Combine(Application.dataPath, "Game/Runtime/Verification/Art07VisualRecorder.cs")), buildGuid = Application.buildGUID,
                platform = Application.platform.ToString(), os = SystemInfo.operatingSystem, gpu = SystemInfo.graphicsDeviceName, graphicsApi = SystemInfo.graphicsDeviceType.ToString(),
                buildIdentity = Resources.Load<TextAsset>("BuildIdentity")?.text, editor = Application.isEditor, batchMode = Application.isBatchMode,
                width = Screen.width, height = Screen.height, saveDirectory = saveRoot, normalSaveShaBefore = HashFile(recorder.normalSavePath), debugSaveShaBefore = HashFile(owner.SaveLocation),
                reducedMotion = IsReduced(mode), requestedCycles = mode == Art07RecordingMode.ContinuousHighest ? continuousCycles : 1,
                previousCaptureDeltaTime = Time.captureDeltaTime, previousPreferences = PreferenceKeys.Select(k => new Preference { key = k, existed = PlayerPrefs.HasKey(k), value = PlayerPrefs.GetInt(k, 0) }).ToArray() };
            try
            {
                if(transactionClip!=Art07TransactionClip.None){recorder.report.mode=(IsReduced(mode)?"Reduced":"Normal")+transactionClip;recorder.report.fixture="Real generated debug shop / "+transactionClip;recorder.report.requestedCycles=1;}
                active = recorder; recorder.WriteReport();
                recorder.frameWriter = new StreamWriter(Path.Combine(output, "frames.jsonl"), false, new UTF8Encoding(false)) { AutoFlush = true };
                File.WriteAllText(Path.Combine(output, "state-before-fixture.json"), JsonUtility.ToJson(owner.Session.State, true));
                Time.captureFramerate = FramesPerSecond;
                recorder.runner = recorder.StartCoroutine(recorder.Guarded(recorder.Record()));
            }
            catch (Exception ex) { recorder.Finish("failed", ex.ToString()); }
            return recorder;
        }

        public void Cancel() { if (IsRecording) Finish("cancelled", "Recording explicitly cancelled; partial frames retained."); }

        private IEnumerator Guarded(IEnumerator body)
        {
            while (IsRecording)
            {
                object current;
                try { if (!body.MoveNext()) break; current = body.Current; }
                catch (Exception ex) { Finish("failed", ex.ToString()); yield break; }
                yield return current;
            }
            if (IsRecording) Finish("captured_pending_visual_review", null);
        }

        private IEnumerator Record()
        {
            if(transactionClip07!=Art07TransactionClip.None)
            {
                var transaction=RecordTransaction07();while(transaction.MoveNext())yield return transaction.Current;yield break;
            }
            var fixture = FixtureFor(mode);
            var applied = Art07Fixtures.Apply(app, fixture);
            File.WriteAllText(Path.Combine(report.outputDirectory, "fixture.json"), JsonUtility.ToJson(applied, true));
            SetOptions(mode==Art07RecordingMode.InstantHighest?"Instant":mode == Art07RecordingMode.FastHighest||mode==Art07RecordingMode.FastSharedHalf||mode==Art07RecordingMode.FastPost ? "Fast" : "Normal", IsReduced(mode));
            expectedStateHash = GameSession.StateHash(app.Session.State); expectedSaveHash = HashFile(app.SaveLocation);
            File.WriteAllText(Path.Combine(report.outputDirectory, "state-before-submit.json"), JsonUtility.ToJson(app.Session.State, true));
            clockEnabled = true;
            yield return null;
            // Real composited lead-in frames establish the unchanged submitted dice and UI.
            for (int i = 0; i < 15; i++) { yield return new WaitForEndOfFrame(); Capture("lead", -1, null); yield return null; }
            for (int cycle = 0; cycle < report.requestedCycles; cycle++)
            {
                cycleTicks = 0;
                var detail = new Cycle { index = cycle, firstFrame = frameIndex, command = cycle == 0 ? "Submit" : "ReadOnlyReplay", stateHashBefore = expectedStateHash, saveShaBefore = expectedSaveHash };
                report.cycles.Add(detail);
                if (cycle == 0)
                {
                    receipt = Art07Fixtures.Submit(app, fixture);
                    if (receipt.FallbackReason != null) throw new InvalidOperationException("Fallback presentation is not the requested visual recording: " + receipt.FallbackReason);
                    expectedStateHash = GameSession.StateHash(app.Session.State); expectedSaveHash = HashFile(app.SaveLocation);
                    File.WriteAllText(Path.Combine(report.outputDirectory, "state-committed.json"), JsonUtility.ToJson(app.Session.State, true));
                }
                else if (!app.ReplayLastPresentation07()) throw new InvalidOperationException("The real read-only presentation replay was refused.");
                detail.receiptId = receipt.GameReceiptId; detail.durationMilliseconds = app.PresentationDurationMilliseconds07;
                bool instant=mode==Art07RecordingMode.InstantHighest;
                if(instant&&app.PresentationPlaying07)throw new InvalidOperationException("Instant must finish synchronously.");
                if (!instant&&(!app.PresentationPlaying07 || detail.durationMilliseconds <= 0)) throw new InvalidOperationException("The requested animated presentation did not start.");
                int expectedTicks = instant?0:(int)Math.Ceiling(detail.durationMilliseconds * FramesPerSecond / 1000d), captured = 0;
                while (true)
                {
                    yield return new WaitForEndOfFrame(); Capture("presentation", cycle, detail.command); captured++;
                    if (!instant&&captured > 2 && totalTicks == 0) throw new InvalidOperationException("Fixed presentation clock hook was not called. Integrate PresentationDeltaTime before recording.");
                    if (!app.PresentationPlaying07) break;
                    if (captured > expectedTicks + 3) throw new InvalidOperationException("Presentation did not finish at its declared fixed-time duration; it may be paused or the clock hook is missing.");
                    yield return null;
                }
                detail.clockTicks = cycleTicks;
                if (Math.Abs(cycleTicks - expectedTicks) > 1) throw new InvalidOperationException("Observed presentation ticks differ from the declared 60 Hz timeline.");
                detail.lastFrame = frameIndex - 1; detail.completed = true;
                detail.stateHashAfter = GameSession.StateHash(app.Session.State); detail.saveShaAfter = HashFile(app.SaveLocation);
                detail.stateAndSaveStable = detail.stateHashAfter == expectedStateHash && detail.saveShaAfter == expectedSaveHash;
                if (!detail.stateAndSaveStable) throw new InvalidOperationException("Presentation changed committed state or save bytes.");
                WriteReport();
                // Continuous highest Combo has a documented 100 ms quiet gap; no skipped beats.
                int hold = cycle + 1 < report.requestedCycles ? 6 : 30;
                for (int i = 0; i < hold; i++) { yield return null; yield return new WaitForEndOfFrame(); Capture("hold", cycle, detail.command); }
                yield return null;
            }
        }

        private static bool IsReduced(Art07RecordingMode value)=>value==Art07RecordingMode.ReducedHighest||value==Art07RecordingMode.ReducedSharedHalf||value==Art07RecordingMode.ReducedPost;
        private static Art07FixtureKind FixtureFor(Art07RecordingMode value)
        {
            switch(value)
            {
                case Art07RecordingMode.NormalSample:return Art07FixtureKind.GoldenSample;
                case Art07RecordingMode.NormalSharedHalf:case Art07RecordingMode.FastSharedHalf:case Art07RecordingMode.ReducedSharedHalf:return Art07FixtureKind.SharedHalf;
                case Art07RecordingMode.NormalZero:return Art07FixtureKind.ZeroMultiplier;
                case Art07RecordingMode.NormalPost:case Art07RecordingMode.ReducedPost:case Art07RecordingMode.FastPost:return Art07FixtureKind.PermanentPost;
                case Art07RecordingMode.NormalQualified:return Art07FixtureKind.Qualified;
                default:return Art07FixtureKind.G147;
            }
        }

        private void Capture(string stage, int cycle, string command)
        {
            if (Time.frameCount == lastCapturedUnityFrame) throw new InvalidOperationException("Two screenshots were requested for the same Unity frame.");
            if (lastCapturedUnityFrame >= 0 && Time.frameCount != lastCapturedUnityFrame + 1) throw new InvalidOperationException("A Unity frame was missed; the recording is not a contiguous 60 Hz sequence.");
            if (Screen.width != report.width || Screen.height != report.height) throw new InvalidOperationException("Game view dimensions changed during recording.");
            string stateHash = GameSession.StateHash(app.Session.State), saveSha = HashFile(app.SaveLocation), normalSha = HashFile(normalSavePath);
            if (stateHash != expectedStateHash || saveSha != expectedSaveHash || normalSha != report.normalSaveShaBefore) throw new InvalidOperationException("State/save changed outside the explicitly recorded command.");
            if (tickFrame == Time.frameCount && ticksInFrame > 1) throw new InvalidOperationException("The presentation clock advanced more than once in a rendered frame.");
            Texture2D texture = null;
            var frame = new Frame { index = frameIndex, unityFrame = Time.frameCount, cycle = cycle, width = Screen.width, height = Screen.height,
                videoSeconds = frameIndex / (double)FramesPerSecond, presentationSeconds = cycleTicks / (double)FramesPerSecond, realtimeSeconds = Time.realtimeSinceStartupAsDouble - startedAt,
                unscaledDeltaSeconds = Time.unscaledDeltaTime, stage = stage, command = command, receiptId = receipt?.GameReceiptId, score = receipt?.ScoreDecimal,
                multiplierNumerator = receipt?.MultiplierNumeratorDecimal, multiplierDenominator = receipt?.MultiplierDenominatorDecimal, chainCount = receipt?.ChainCount ?? 0, comboTier = receipt?.ComboTier ?? 0,
                stateHash = stateHash, saveSha256 = saveSha, normalSaveSha256 = normalSha, presentationPlaying = app.PresentationPlaying07,
                presentationTicks = tickFrame == Time.frameCount ? ticksInFrame : 0, nonblockingTicks=nonblockingTickFrame==Time.frameCount?nonblockingTicksInFrame:0, fixedClockObserved = totalTicks > 0, file = "frames/frame_" + frameIndex.ToString("D6") + ".png" };
            if(transactionClip07!=Art07TransactionClip.None)frame.transaction=InspectTransactionFrame07(stage);
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null || texture.width != Screen.width || texture.height != Screen.height) throw new InvalidOperationException("ScreenCapture returned no full-size composited frame.");
                Analyze(texture.GetPixels32(), texture.width, texture.height, frame);
                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length < 64) throw new IOException("PNG encoding returned no usable image.");
                string absolutePath = Path.Combine(report.outputDirectory, frame.file); File.WriteAllBytes(absolutePath, png);
                frame.pngSha256 = Hash(png);
                if (HashFile(absolutePath) != frame.pngSha256) throw new IOException("Written PNG hash differs from captured bytes.");
                frameWriter.WriteLine(JsonUtility.ToJson(frame));
                frameIndex++; report.frames = frameIndex; report.sourceTickCalls = totalTicks;report.nonblockingTickCalls=totalNonblockingTicks;
                report.fixedClockIntegrated = totalTicks > 0; lastCapturedUnityFrame = Time.frameCount; lastProgressAt = Time.realtimeSinceStartupAsDouble;
                if (!frame.contentValid) throw new InvalidOperationException("Blank/invalid screenshot retained at " + absolutePath + "; this recording did not pass capture integrity.");
            }
            finally { if (texture != null) Destroy(texture); }
        }

        private void Analyze(Color32[] pixels, int width, int height, Frame frame)
        {
            if (pixels == null || pixels.Length != width * height) throw new InvalidOperationException("Invalid screenshot pixel buffer.");
            var samples = new List<Color32>(); var colors = new bool[4096]; int nonBlack = 0, bright = 0, changed = 0, redBlue = 0; double luminance = 0;
            for (int y = 0; y < height; y += 16) for (int x = 0; x < width; x += 16)
            {
                Color32 p = pixels[y * width + x]; int n = samples.Count; samples.Add(p);
                int maximum = Math.Max(p.r, Math.Max(p.g, p.b)); if (maximum > 8) nonBlack++; if (maximum >= 64) bright++;
                int q = ((p.r >> 4) << 8) | ((p.g >> 4) << 4) | (p.b >> 4); if (!colors[q]) { colors[q] = true; frame.quantizedColors++; }
                double luma = Luma(p); luminance += luma;
                if (priorSamples != null && n < priorSamples.Length)
                {
                    Color32 previous = priorSamples[n]; if (Math.Abs(luma - Luma(previous)) > .2) changed++;
                    if (p.r > p.b + 64 && previous.b > previous.r + 64 || p.b > p.r + 64 && previous.r > previous.b + 64) redBlue++;
                }
            }
            frame.sampledPixels = samples.Count; frame.nonBlackRatio = nonBlack / (double)samples.Count; frame.brightRatio = bright / (double)samples.Count;
            frame.meanLuminance = luminance / samples.Count; frame.highContrastAreaRatio = changed / (double)samples.Count; frame.redBlueSwapAreaRatio = redBlue / (double)samples.Count;
            frame.contentValid = frame.nonBlackRatio >= .10 && frame.brightRatio >= .005 && frame.quantizedColors >= 8;
            priorSamples = samples.ToArray();
        }
        private static double Luma(Color32 p) => (.2126 * p.r + .7152 * p.g + .0722 * p.b) / 255;

        private void SetOptions(string speed, bool reduced)
        {
            Press("Pause"); Press("Settings"); Press("SettingsTab2");
            int guard = 0;
            while (!ButtonText("Speed").StartsWith(speed, StringComparison.Ordinal) && guard++ < 3) Press("Speed");
            if (!ButtonText("Speed").StartsWith(speed, StringComparison.Ordinal)) throw new InvalidOperationException("Could not select requested presentation speed through settings.");
            string desired = reduced ? "开" : "关";
            if (!ButtonText("Reduced").StartsWith(desired, StringComparison.Ordinal)) Press("Reduced");
            if (!ButtonText("Reduced").StartsWith(desired, StringComparison.Ordinal)) throw new InvalidOperationException("Could not select reduced motion through settings.");
            Press("SettingsDone");
        }
        private Button FindButton(string name)
        {
            var buttons = app.canvas.GetComponentsInChildren<Button>(true).Where(b => b.name == name && b.gameObject.activeInHierarchy && b.interactable).ToArray();
            if (buttons.Length != 1) throw new InvalidOperationException("Expected one active settings action " + name + ", found " + buttons.Length + ".");
            return buttons[0];
        }
        private void Press(string name) => FindButton(name).onClick.Invoke();
        private string ButtonText(string name) => FindButton(name).GetComponentInChildren<TextMeshProUGUI>().text;

        private void Update()
        {
            if (!IsRecording) return;
            double now = Time.realtimeSinceStartupAsDouble;
            if (now - lastProgressAt > 12) Finish("failed", "No completed composited frame for 12 seconds. WaitForEndOfFrame may be unavailable in this batch Editor; run the explicit recorder in a visible Game view or Player.");
            else if (now - startedAt > 900) Finish("failed", "Recording exceeded its 15 minute wall-clock safety limit.");
        }
        private void Finish(string status, string error)
        {
            if (finishing || report == null || report.status != "running") return;
            finishing = true; clockEnabled = false; report.status = status; report.error = error;
            if (runner != null) StopCoroutine(runner);
            try
            {
                if (app != null && app.IsBusy) app.SkipVisual();
                if (app != null && app.canvas != null)
                {
                    var old = report.previousPreferences.ToDictionary(p => p.key);
                    SetOptions(old["art07.instant"].value == 1 ? "Instant" : old["fast"].value == 1 ? "Fast" : "Normal", old["reduced"].value == 1);
                }
            }
            catch (Exception ex) { report.restoreError = ex.ToString(); report.status = "failed"; }
            finally
            {
                // One cleanup failure must not prevent restoring the remaining global settings.
                try { Time.captureDeltaTime = report.previousCaptureDeltaTime; report.restoredTiming = Math.Abs(Time.captureDeltaTime - report.previousCaptureDeltaTime) < .0000001f; }
                catch (Exception ex) { RestorationFailed(ex); }
                try
                {
                    foreach (var p in report.previousPreferences) { if (p.existed) PlayerPrefs.SetInt(p.key, p.value); else PlayerPrefs.DeleteKey(p.key); }
                    PlayerPrefs.Save();
                    report.restoredPreferences = report.previousPreferences.All(p => PlayerPrefs.HasKey(p.key) == p.existed && (!p.existed || PlayerPrefs.GetInt(p.key) == p.value));
                }
                catch (Exception ex) { RestorationFailed(ex); }
                try
                {
                    report.normalSaveShaAfter = HashFile(normalSavePath); report.debugSaveShaAfter = app == null ? "app_unavailable" : HashFile(app.SaveLocation);
                    report.normalSaveUnchanged = report.normalSaveShaBefore == report.normalSaveShaAfter;
                }
                catch (Exception ex) { RestorationFailed(ex); }
                if (!report.normalSaveUnchanged || !report.restoredTiming || !report.restoredPreferences) report.status = "failed";
                report.frames = frameIndex; report.sourceTickCalls = totalTicks;report.nonblockingTickCalls=totalNonblockingTicks; report.fixedClockIntegrated = totalTicks > 0; report.endedUtc = DateTime.UtcNow.ToString("O");
                try { frameWriter?.Dispose(); } catch (Exception ex) { RestorationFailed(ex); } frameWriter = null;
                if (active == this) active = null;
                try { WriteReport(); Debug.Log("ART07_VISUAL_RECORDING " + report.status + " " + Path.Combine(report.outputDirectory, "recording.json")); }
                catch (Exception ex) { Debug.LogError("Art07 recording report output failed: " + ex.Message); }
                finishing = false; enabled = false;
            }
        }
        private void RestorationFailed(Exception ex) { report.status = "failed"; report.restoreError = (report.restoreError ?? "") + "\n" + ex; }
        private void WriteReport() => File.WriteAllText(Path.Combine(report.outputDirectory, "recording.json"), JsonUtility.ToJson(report, true));
        private void OnDisable() { if (IsRecording) Finish("failed", "Recorder disabled before completion; partial evidence retained."); }
        private void OnDestroy() { if (IsRecording) Finish("failed", "Recorder destroyed before completion; partial evidence retained."); }
        private static bool IsWithin(string path, string directory)
        {
            string absolute = Path.GetFullPath(path), root = Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.Equals(absolute, root, StringComparison.OrdinalIgnoreCase) || absolute.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        private static string HashFile(string path) => File.Exists(path) ? Hash(File.ReadAllBytes(path)) : "missing";
        private static string Hash(byte[] data) { using (var sha = SHA256.Create()) return string.Concat(sha.ComputeHash(data).Select(b => b.ToString("x2"))); }
    }
}
