using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace DiceCraft
{
    /// <summary>Disposable, opt-in timing-route experiment; never an AC-PERF verdict.</summary>
    public sealed class FramePacingExperiment : MonoBehaviour
    {
        private static string directory;
        private const double WarmupSeconds = 3, DurationSeconds = 20;
        private readonly FrameTiming[] timings = new FrameTiming[1];
        private readonly List<ModeCapture> captures = new List<ModeCapture>();
        private readonly List<string> errors = new List<string>();
        private readonly List<string> lifecycleChecks = new List<string>();
        private GameApp app;
        private ModeCapture active;
        private bool sampling, finished;
        private double start, lastTick;
        private ulong seenFrameStart, lastStart, lastPresent, lastComplete, cpuFrequency;
        private int originalWidth, originalHeight, originalVsync, originalTarget;
        private double originalDesktopHz;
        private string failure, failurePhase, phase = "startup";
        private bool restoreConfirmed, originalPacer;

        private struct Record
        {
            public int unityFrame, status;
            public bool focused;
            public double elapsed, interval, startDelta, presentDelta, completeDelta;
            public ulong frameStart, present, complete;
        }
        private sealed class ModeCapture
        {
            public string name;
            public Dictionary<string, object> environment;
            public Dictionary<string, object> endingEnvironment;
            public FullScreenMode requestedMode;
            public readonly List<Record> rows = new List<Record>(60000);
            public double seconds;
            public int growths, modeMismatchRows;
            public bool completed, timingEnabled;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Configure()
        {
            directory = null;
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--pacing-experiment");
            if (index < 0) return;
            try
            {
                if (index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                    throw new ArgumentException("--pacing-experiment requires an evidence directory.");
                directory = Path.GetFullPath(args[index + 1]);
                Directory.CreateDirectory(directory);
                if (Array.IndexOf(args, "--verify-p0") >= 0)
                    throw new ArgumentException("Do not combine pacing-experiment and verify-p0 in one player.");
                // A fresh child directory ensures the idle menu never loads an earlier run.
                GameApp.SaveDirectoryOverride = Path.Combine(directory, "pacing-save-" + Guid.NewGuid().ToString("N"));
            }
            catch (Exception e)
            {
                if (directory != null) File.WriteAllText(Path.Combine(directory, "pacing-startup-failure.txt"), e.ToString());
                Debug.LogError(e); directory = null; Application.Quit(1);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Launch()
        {
            if (directory == null) return;
            var owner = new GameObject("OptInFramePacingExperiment");
            DontDestroyOnLoad(owner); owner.AddComponent<FramePacingExperiment>();
        }

        private IEnumerator Start()
        {
            originalWidth = Screen.width; originalHeight = Screen.height;
            originalDesktopHz = Screen.currentResolution.refreshRateRatio.value;
            originalVsync = QualitySettings.vSyncCount; originalTarget = Application.targetFrameRate;
            originalPacer = FramePacer.IsInstalled;
            cpuFrequency = FrameTimingManager.GetCpuTimerFrequency();
            Application.logMessageReceived += OnLog;
            var stack = new Stack<IEnumerator>(); stack.Push(Run());
            while (stack.Count > 0)
            {
                object current = null; bool moved = false;
                try { moved = stack.Peek().MoveNext(); if (moved) current = stack.Peek().Current; }
                catch (Exception e) { failure = e.ToString(); break; }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (current is IEnumerator nested) { stack.Push(nested); continue; }
                yield return current;
            }
            if (sampling && active != null) active.seconds = Time.realtimeSinceStartupAsDouble - start;
            sampling = false;
            if (failure != null) failurePhase = phase;
            while (stack.Count > 0)
            {
                try { (stack.Pop() as IDisposable)?.Dispose(); }
                catch (Exception e) { failure = (failure ?? "") + "\nCleanup: " + e; }
            }
            // Release exclusive display ownership before writing/exit on success or failure.
            phase = "restoring-windowed";
            IEnumerator restore = RestoreWindow();
            while (true)
            {
                bool moved = false;
                try { moved = restore.MoveNext(); }
                catch (Exception e) { failure = (failure ?? "") + "\nRestore: " + e; break; }
                if (!moved) break;
                yield return restore.Current;
            }
            finished = true;
            Application.logMessageReceived -= OnLog;
            if (errors.Count > 0) failure = (failure ?? "") + "\nUnity errors: " + string.Join("\n", errors);
            try { WriteEvidence(); }
            catch (Exception e) { failure = (failure ?? "") + "\nEvidence: " + e; Debug.LogError(failure); }
            Application.Quit(failure == null && restoreConfirmed ? 0 : 1);
        }

        private IEnumerator Run()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (app == null)
            {
                app = FindAnyObjectByType<GameApp>();
                if (app != null && app.Session != null) break;
                app = null;
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("GameApp not ready.");
                yield return null;
            }
            if (app.ViewState != null) throw new InvalidOperationException("Experiment must start in a fresh idle menu.");
            yield return CaptureMode("windowed-software-60", FullScreenMode.Windowed, 0);
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--deadline-pacing") >= 0)
            {
                yield return CaptureMode("windowed-deadline-60", FullScreenMode.Windowed, 0, true);
                yield return CheckPacerLifecycle();
            }
            else yield return CaptureMode("exclusive-requested-60hz-vsync1", FullScreenMode.ExclusiveFullScreen, 1);
        }

        private IEnumerator CheckPacerLifecycle()
        {
            phase = "pacer-lifecycle-outside-sampling";
            FramePacer.Stop(); FramePacer.Stop();
            Check(!FramePacer.IsInstalled && Application.targetFrameRate == 60 && QualitySettings.vSyncCount == 0, "Stop restores native fallback");
            Check(FramePacer.Install() && FramePacer.Install(), "Repeated install succeeds");
            var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
            Func<FramePacer> host = () => (FramePacer)typeof(FramePacer).GetField("lifetime", flags).GetValue(null);
            Func<DeadlineFrameScheduler> clock = () => (DeadlineFrameScheduler)typeof(FramePacer).GetField("scheduler", flags).GetValue(null);
            host().SendMessage("OnApplicationFocus", false);
            double before = Time.realtimeSinceStartupAsDouble;
            yield return null; yield return null; yield return null;
            Check(clock().IsSuspended && !clock().NextDeadline.HasValue && Time.realtimeSinceStartupAsDouble - before >= .03,
                "Simulated focus callback keeps background wait without active deadline");
            host().SendMessage("OnApplicationPause", true); host().SendMessage("OnApplicationFocus", true);
            yield return null;
            Check(clock().IsSuspended, "Simulated pause still holds after focus recovery");
            host().SendMessage("OnApplicationPause", false);
            yield return null; yield return null;
            Check(!clock().IsSuspended && clock().NextDeadline.HasValue, "Simulated pause recovery reanchors cadence");
            FramePacer.Stop(); Check(FramePacer.Install(), "Same-frame stop/reinstall succeeds");
            yield return null; yield return null;
            Check(FramePacer.IsInstalled && Application.targetFrameRate == -1, "Old delayed host destruction does not stop new owner");
            Destroy(host().gameObject);
            yield return null; yield return null;
            Check(!FramePacer.IsInstalled && Application.targetFrameRate == 60 && QualitySettings.vSyncCount == 0,
                "Actual host destruction restores fallback");
            Check(FramePacer.Install(), "Reinstall after actual destruction succeeds");
            yield return null;
        }
        private void Check(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
            lifecycleChecks.Add(message);
        }

        private IEnumerator CaptureMode(string name, FullScreenMode mode, int vsync, bool deadlinePacer = false)
        {
            phase = name + "/switch";
            sampling = false; active = null;
            FramePacer.Stop();
            QualitySettings.vSyncCount = vsync; Application.targetFrameRate = 60;
            if (deadlinePacer)
            {
                if (!FramePacer.Install()) throw new InvalidOperationException("Desktop frame pacer could not be installed.");
            }
            Screen.SetResolution(1920, 1080, mode, new RefreshRate { numerator = 60, denominator = 1 });
            double deadline = Time.realtimeSinceStartupAsDouble + 15;
            int stable = 0;
            while (stable < 3)
            {
                stable = Screen.width == 1920 && Screen.height == 1080 && Screen.fullScreenMode == mode ? stable + 1 : 0;
                if (Time.realtimeSinceStartupAsDouble > deadline)
                    throw new TimeoutException("Requested mode did not become actual: " + name + "; actual " + Screen.fullScreenMode + " " + Screen.width + "x" + Screen.height);
                yield return null;
            }
            active = new ModeCapture { name = name, requestedMode = mode, environment = EnvironmentSnapshot(), timingEnabled = FrameTimingManager.IsFeatureEnabled() };
            captures.Add(active);
            phase = name + "/warmup";
            double warmup = Time.realtimeSinceStartupAsDouble;
            while (Time.realtimeSinceStartupAsDouble - warmup < WarmupSeconds) yield return null;
            seenFrameStart = lastStart = lastPresent = lastComplete = 0;
            start = lastTick = Time.realtimeSinceStartupAsDouble;
            sampling = true; phase = name + "/sampling";
            while (Time.realtimeSinceStartupAsDouble - start < DurationSeconds)
            {
                if (failure != null) throw new InvalidOperationException(failure);
                yield return null;
            }
            active.seconds = Time.realtimeSinceStartupAsDouble - start;
            sampling = false; active.completed = true; active.endingEnvironment = EnvironmentSnapshot();
        }

        private void LateUpdate()
        {
            if (finished || active == null) return;
            try
            {
                if (active.timingEnabled) FrameTimingManager.CaptureFrameTimings();
                if (!sampling) return;
                if (Screen.fullScreenMode != active.requestedMode || Screen.width != 1920 || Screen.height != 1080) active.modeMismatchRows++;
                double now = Time.realtimeSinceStartupAsDouble;
                var row = new Record { unityFrame = Time.frameCount, elapsed = now - start,
                    interval = active.rows.Count == 0 ? double.NaN : (now - lastTick) * 1000,
                    startDelta = double.NaN, presentDelta = double.NaN, completeDelta = double.NaN,
                    focused = Application.isFocused, status = 0 };
                if (active.timingEnabled && FrameTimingManager.GetLatestTimings(1, timings) > 0)
                {
                    row.frameStart = timings[0].frameStartTimestamp;
                    row.present = timings[0].cpuTimePresentCalled; row.complete = timings[0].cpuTimeFrameComplete;
                    if (row.frameStart == 0) row.status = 1;
                    else if (row.frameStart == seenFrameStart) row.status = 2;
                    else if (seenFrameStart != 0 && row.frameStart < seenFrameStart) row.status = 3;
                    else
                    {
                        row.status = 4;
                        row.startDelta = Delta(row.frameStart, lastStart);
                        row.presentDelta = Delta(row.present, lastPresent);
                        row.completeDelta = Delta(row.complete, lastComplete);
                    }
                }
                // No cross-gap interval is presented as a consecutive frame interval.
                if (row.status == 4)
                {
                    // A non-increasing axis is itself a gap, not a new valid baseline.
                    bool presentValid = row.present > 0 && (lastPresent == 0 || row.present > lastPresent);
                    bool completeValid = row.complete > 0 && (lastComplete == 0 || row.complete > lastComplete);
                    seenFrameStart = lastStart = row.frameStart;
                    lastPresent = presentValid ? row.present : 0; lastComplete = completeValid ? row.complete : 0;
                }
                else { lastStart = lastPresent = lastComplete = 0; }
                if (active.rows.Count == active.rows.Capacity) active.growths++;
                active.rows.Add(row); lastTick = now;
            }
            catch (Exception e) { failure = e.ToString(); sampling = false; }
        }

        private double Delta(ulong value, ulong previous)
        {
            return cpuFrequency == 0 || value == 0 || previous == 0 || value <= previous
                ? double.NaN : (value - previous) * 1000d / cpuFrequency;
        }

        private IEnumerator RestoreWindow()
        {
            FramePacer.Stop();
            if (originalPacer && !FramePacer.Install()) throw new InvalidOperationException("Original frame pacer could not be restored.");
            QualitySettings.vSyncCount = originalVsync; Application.targetFrameRate = originalTarget;
            Screen.SetResolution(originalWidth, originalHeight, FullScreenMode.Windowed);
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            int stable = 0;
            while (stable < 3)
            {
                bool restored = Screen.fullScreenMode == FullScreenMode.Windowed && Screen.width == originalWidth && Screen.height == originalHeight
                    && Math.Abs(Screen.currentResolution.refreshRateRatio.value - originalDesktopHz) < .1;
                stable = restored ? stable + 1 : 0;
                if (Time.realtimeSinceStartupAsDouble > deadline) throw new TimeoutException("Window/desktop refresh restoration could not be confirmed before exit.");
                yield return null;
            }
            restoreConfirmed = true;
        }

        private void OnLog(string message, string trace, LogType kind)
        {
            if (kind == LogType.Error || kind == LogType.Exception || kind == LogType.Assert) errors.Add(message + "\n" + trace);
        }

        private void OnApplicationQuit()
        {
            // Also release exclusive ownership if the user closes the experiment early.
            // The controlled exit path above waits for, and records, actual restoration.
            if (Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen)
                Screen.SetResolution(Math.Max(1, originalWidth), Math.Max(1, originalHeight), FullScreenMode.Windowed);
        }

        private Dictionary<string, object> EnvironmentSnapshot()
        {
            var rate = Screen.currentResolution.refreshRateRatio;
            return new Dictionary<string, object> {
                ["mode"] = Screen.fullScreenMode.ToString(), ["width"] = Screen.width, ["height"] = Screen.height,
                ["desktopWidth"] = Screen.currentResolution.width, ["desktopHeight"] = Screen.currentResolution.height,
                ["actualRefreshNumerator"] = rate.numerator, ["actualRefreshDenominator"] = rate.denominator,
                ["actualRefreshHz"] = rate.value, ["requested60HzMatched"] = Math.Abs(rate.value - 60d) < .05,
                ["vSyncCount"] = QualitySettings.vSyncCount, ["targetFrameRate"] = Application.targetFrameRate,
                ["deadlineFramePacerInstalled"] = FramePacer.IsInstalled,
                ["focused"] = Application.isFocused };
        }

        private static object Stats(IEnumerable<double> source)
        {
            var a = source.Where(x => !double.IsNaN(x) && !double.IsInfinity(x)).OrderBy(x => x).ToArray();
            if (a.Length == 0) return null;
            return new Dictionary<string, object> { ["samples"] = a.Length, ["meanMs"] = a.Average(),
                ["p50Ms"] = a[(int)Math.Ceiling(a.Length * .5)-1], ["p95Ms"] = a[(int)Math.Ceiling(a.Length * .95)-1], ["maxMs"] = a[a.Length-1] };
        }

        private void WriteEvidence()
        {
            var modes = new List<object>();
            foreach (var c in captures)
            {
                using (var w = new StreamWriter(Path.Combine(directory, c.name + ".csv"), false, new UTF8Encoding(false)))
                {
                    w.WriteLine("unity_frame,elapsed_s,late_interval_ms,frame_start_timestamp,present_called_timestamp,frame_complete_timestamp,frame_start_delta_ms,present_called_delta_ms,frame_complete_delta_ms,timing_status,focused,cpu_timer_frequency_hz");
                    foreach (var r in c.rows) w.WriteLine(string.Join(",", r.unityFrame, Number(r.elapsed), Number(r.interval), r.frameStart,
                        r.present, r.complete, Number(r.startDelta), Number(r.presentDelta), Number(r.completeDelta), r.status, r.focused ? 1 : 0, cpuFrequency));
                }
                modes.Add(new Dictionary<string, object> {
                    ["name"] = c.name, ["completed"] = c.completed, ["sampleSeconds"] = c.seconds, ["environment"] = c.environment,
                    ["endingEnvironment"] = c.endingEnvironment, ["modeMismatchRows"] = c.modeMismatchRows,
                    ["rows"] = c.rows.Count, ["focusedRows"] = c.rows.Count(x => x.focused), ["observerBufferGrowths"] = c.growths,
                    ["frameTimingEnabled"] = c.timingEnabled,
                    ["timingStatuses"] = c.rows.GroupBy(x => x.status).ToDictionary(g => g.Key.ToString(), g => (object)g.Count()),
                    ["presentZeroRows"] = c.rows.Count(x => x.present == 0), ["completeZeroRows"] = c.rows.Count(x => x.complete == 0),
                    ["presentDeltaUnavailableRows"] = c.rows.Count(x => double.IsNaN(x.presentDelta)),
                    ["completeDeltaUnavailableRows"] = c.rows.Count(x => double.IsNaN(x.completeDelta)),
                    ["lateUpdateInterval"] = Stats(c.rows.Select(x => x.interval)), ["frameStartInterval"] = Stats(c.rows.Select(x => x.startDelta)),
                    ["presentCalledInterval"] = Stats(c.rows.Select(x => x.presentDelta)), ["frameCompleteInterval"] = Stats(c.rows.Select(x => x.completeDelta)) });
            }
            var report = new Dictionary<string, object> {
                ["kind"] = "disposable-frame-pacing-route-experiment", ["revision"] = "pacing-route-2", ["utc"] = DateTime.UtcNow.ToString("O"),
                ["status"] = failure == null && restoreConfirmed ? "experiment_completed_not_AC_PERF" : "experiment_failed", ["failure"] = failure,
                ["phase"] = phase, ["failurePhase"] = failurePhase, ["unityVersion"] = Application.unityVersion, ["buildGuid"] = Application.buildGUID,
                ["assemblyMvid"] = typeof(FramePacingExperiment).Assembly.ManifestModule.ModuleVersionId.ToString(),
                ["cpu"] = SystemInfo.processorType, ["gpu"] = SystemInfo.graphicsDeviceName, ["graphicsApi"] = SystemInfo.graphicsDeviceType.ToString(),
                ["os"] = SystemInfo.operatingSystem, ["cpuTimerFrequencyHz"] = cpuFrequency,
                ["warmupSecondsEach"] = WarmupSeconds, ["requestedSampleSecondsEach"] = DurationSeconds,
                ["interaction"] = "Identical idle main menu; no Refresh calls, game commands, stress fixture, or altered gameplay state.",
                ["lifecycleChecks"] = lifecycleChecks,
                ["lifecycleLimit"] = "Lifecycle checks run after timing samples. Pause/focus callbacks are injected into the actual component, not OS window focus changes; host destruction and native cap restoration are real. This is not a physical window-switch test.",
                ["isolatedSaveDirectory"] = GameApp.SaveDirectoryOverride, ["originalDesktopHz"] = originalDesktopHz,
                ["restorationConfirmedBeforeExit"] = restoreConfirmed, ["restoredEnvironment"] = EnvironmentSnapshot(),
                ["timingStatusLegend"] = "0 no timing; 1 zero frameStart; 2 duplicate frameStart; 3 nonmonotonic frameStart; 4 unique frameStart. Gaps reset delta baselines.",
                ["timestampDefinitions"] = "Unity local 6000.6 CoreModule.xml: frameStartTimestamp=CPU clock when frame started; cpuTimePresentCalled=CPU clock at Present call; cpuTimeFrameComplete=CPU clock when GPU finished rendering and interrupted CPU. Differences use GetCpuTimerFrequency. Present call is not physical scanout and may precede synchronization wait.",
                ["limitations"] = "20s idle technical comparison only. Not the 180s 10-dice/7-slot/10-controller/5-item AC-PERF scene. All timing series retained separately, no best-series verdict. First partial LateUpdate interval is blank; raw row remains. Missing/duplicate timing gaps are retained; valid deltas require adjacent unique valid samples. No threshold or acceptance change.",
                ["modes"] = modes };
            File.WriteAllText(Path.Combine(directory, "pacing-report.json"), Json(report), new UTF8Encoding(false));
        }

        private static string Number(double x) => double.IsNaN(x) || double.IsInfinity(x) ? "" : x.ToString("R", CultureInfo.InvariantCulture);
        private static string Json(object value)
        {
            if (value == null) return "null";
            if (value is string s) return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n").Replace("\t", "\\t") + "\"";
            if (value is bool b) return b ? "true" : "false";
            if (value is IDictionary dictionary)
            {
                var entries = new List<string>(); foreach (DictionaryEntry entry in dictionary) entries.Add(Json(entry.Key.ToString()) + ":" + Json(entry.Value));
                return "{" + string.Join(",", entries) + "}";
            }
            if (value is IEnumerable sequence) { var entries = new List<string>(); foreach (object item in sequence) entries.Add(Json(item)); return "[" + string.Join(",", entries) + "]"; }
            if (value is double d && (double.IsNaN(d) || double.IsInfinity(d))) return "null";
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }
    }
}
