using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEngine;

// Disposable Built-in-pipeline control. No game code, UI, input package or frame pacer.
public sealed class NativeDisplayProbe : MonoBehaviour
{
    [Serializable] public sealed class Mode
    {
        public int width, height;
        public uint refreshNumerator, refreshDenominator;
    }
    [Serializable] public sealed class Frame
    {
        public string phase, utc, fullScreenMode;
        public int frame, width, height, displayWidth, displayHeight;
        public double realtime;
        public bool focused, runInBackground;
        public Mode currentResolution;
        public uint displayRefreshNumerator, displayRefreshDenominator;
    }
    [Serializable] public sealed class Arm
    {
        public string id, status = "not_started", requestException;
        public Mode requested;
        public bool explicitRefresh, exclusiveAtEnd, restoredWindowedThreeStableFrames;
        public double observedSeconds;
        public int observedFrames, trailingExactFrames;
        public Frame before, end, restored;
        public Mode[] resolutionsBefore;
    }
    [Serializable] public sealed class Report
    {
        public string schema = "p06-native-minimal-diagnostic-1";
        public string status = "diagnostic_running";
        public string scope = "Disposable Built-in empty-scene control; observed values only, no game or release acceptance";
        public string utc, updatedUtc, unityVersion, buildGuid, os, graphicsApi, graphicsVersion, gpu, outputDirectory;
        public string failure;
        public bool isEditor, developmentBuild, baselineThreeStableFrames, finalWindowedThreeStableFrames;
        public int processId, targetFrameRate, vSyncCount, exitCode = -1;
        public Mode[] initialResolutions;
        public Frame initial, baseline, finalActual;
        public List<Arm> arms = new List<Arm>();
        public int errorCount, warningCount;
    }
    [Serializable] public sealed class LogEvent
    {
        public string utc, phase, type, message, stack;
    }

    private Camera cameraView;
    private string phase = "passive_no_native_output", output;
    private Report report;
    private StreamWriter frames, events;
    private bool completed, restoring;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        var host = new GameObject("Native minimal display control");
        DontDestroyOnLoad(host);
        host.AddComponent<NativeDisplayProbe>();
    }

    private void Awake()
    {
        Application.runInBackground = true;
        QualitySettings.vSyncCount = 0;
        Application.targetFrameRate = 60; // Unity native cap only.
        var cameraObject = new GameObject("Pure color camera");
        cameraObject.transform.SetParent(transform);
        cameraView = cameraObject.AddComponent<Camera>();
        cameraView.clearFlags = CameraClearFlags.SolidColor;
        cameraView.backgroundColor = new Color(0.035f, 0.12f, 0.20f);
        cameraView.cullingMask = 0;
        cameraView.allowHDR = false;
        cameraView.allowMSAA = false;
    }

    private void Update()
    {
        // Visible progression, without geometry, physics, materials, UI or input.
        float pulse = 0.025f * Mathf.Sin(Time.unscaledTime);
        cameraView.backgroundColor = new Color(0.035f + pulse, 0.12f, 0.20f - pulse);
    }

    private void Start()
    {
        string[] args = Environment.GetCommandLineArgs();
        int index = Array.IndexOf(args, "--native-output");
        if (index < 0) return; // No native display requests or diagnostic writes by default.
        try
        {
            if (index + 1 >= args.Length || Array.LastIndexOf(args, "--native-output") != index)
                throw new ArgumentException("Exactly one --native-output absolute workspace directory is required.");
            string requested = args[index + 1];
            if (!Path.IsPathRooted(requested)) throw new ArgumentException("--native-output must be absolute.");
            string workspace = FindWorkspace(Application.dataPath);
            string basePath = Path.GetFullPath(requested);
            if (!basePath.StartsWith(workspace + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("--native-output must remain inside the containing workspace.");
            output = Path.Combine(basePath, "native-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-" + Process.GetCurrentProcess().Id);
            if (Directory.Exists(output)) throw new IOException("Diagnostic directory already exists.");
            Directory.CreateDirectory(output);
            frames = NewJournal("frames.jsonl"); events = NewJournal("log-events.jsonl");
            report = new Report
            {
                utc = DateTime.UtcNow.ToString("O"), unityVersion = Application.unityVersion,
                buildGuid = Application.buildGUID, os = SystemInfo.operatingSystem,
                graphicsApi = SystemInfo.graphicsDeviceType.ToString(), graphicsVersion = SystemInfo.graphicsDeviceVersion,
                gpu = SystemInfo.graphicsDeviceName, outputDirectory = output, isEditor = Application.isEditor,
                developmentBuild = UnityEngine.Debug.isDebugBuild, processId = Process.GetCurrentProcess().Id,
                targetFrameRate = Application.targetFrameRate, vSyncCount = QualitySettings.vSyncCount,
                initialResolutions = Resolutions(), initial = Capture()
            };
            Application.logMessageReceived += OnLog;
            Persist();
            if (Application.isEditor) throw new InvalidOperationException("Native diagnostic requires a standalone Player, not Editor Play mode.");
            StartCoroutine(Guarded(Run()));
        }
        catch (Exception ex) { Fail(ex); }
    }

    private static string FindWorkspace(string start)
    {
        for (var directory = new DirectoryInfo(start); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "AGENTS.md")) && Directory.Exists(Path.Combine(directory.FullName, "artifacts")))
                return directory.FullName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        throw new DirectoryNotFoundException("Run this disposable control from its workspace build directory.");
    }

    private StreamWriter NewJournal(string name)
    {
        return new StreamWriter(new FileStream(Path.Combine(output, name), FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(false)) { AutoFlush = true };
    }
    private static Mode ToMode(Resolution r)
    {
        return new Mode { width = r.width, height = r.height, refreshNumerator = r.refreshRateRatio.numerator, refreshDenominator = r.refreshRateRatio.denominator };
    }
    private static Mode[] Resolutions()
    {
        var source = Screen.resolutions; var result = new Mode[source.Length];
        for (int i = 0; i < source.Length; i++) result[i] = ToMode(source[i]);
        return result;
    }
    private Frame Capture()
    {
        var display = Screen.mainWindowDisplayInfo;
        return new Frame { phase = phase, utc = DateTime.UtcNow.ToString("O"), frame = Time.frameCount,
            realtime = Time.realtimeSinceStartupAsDouble, width = Screen.width, height = Screen.height,
            fullScreenMode = Screen.fullScreenMode.ToString(), focused = Application.isFocused,
            runInBackground = Application.runInBackground, currentResolution = ToMode(Screen.currentResolution),
            displayWidth = display.width, displayHeight = display.height,
            displayRefreshNumerator = display.refreshRate.numerator, displayRefreshDenominator = display.refreshRate.denominator };
    }
    private void RecordFrame() { frames.WriteLine(JsonUtility.ToJson(Capture())); }
    private void Persist()
    {
        if (report == null || output == null) return;
        report.updatedUtc = DateTime.UtcNow.ToString("O");
        string file = Path.Combine(output, "report.json"), temporary = file + ".tmp";
        File.WriteAllText(temporary, JsonUtility.ToJson(report, true), new UTF8Encoding(false));
        if (File.Exists(file)) File.Replace(temporary, file, null); else File.Move(temporary, file);
    }
    private void OnLog(string message, string stack, LogType type)
    {
        if (report == null) return;
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errorCount++;
        if (type == LogType.Warning) report.warningCount++;
        events.WriteLine(JsonUtility.ToJson(new LogEvent { utc = DateTime.UtcNow.ToString("O"), phase = phase, type = type.ToString(), message = message, stack = stack }));
    }

    private IEnumerator WindowedStable(string nextPhase)
    {
        phase = nextPhase; restoring = true;
        Screen.SetResolution(1920, 1080, FullScreenMode.Windowed);
        double start = Time.realtimeSinceStartupAsDouble; int stable = 0;
        while (Time.realtimeSinceStartupAsDouble - start < 10)
        {
            yield return null; RecordFrame();
            stable = Screen.fullScreenMode == FullScreenMode.Windowed && Screen.width == 1920 && Screen.height == 1080 ? stable + 1 : 0;
            if (stable >= 3) { restoring = false; yield break; }
        }
        throw new TimeoutException(nextPhase + ": 1920x1080 Windowed did not stabilize for three frames.");
    }
    private IEnumerator Run()
    {
        yield return WindowedStable("initial_windowed_1920x1080");
        report.baselineThreeStableFrames = true; report.baseline = Capture(); Persist();
        var display = Screen.mainWindowDisplayInfo;
        int nativeWidth = display.width, nativeHeight = display.height;
        Resolution selected = default; bool found = false; double nearest = double.MaxValue;
        foreach (var r in Screen.resolutions)
        {
            if (r.width != nativeWidth || r.height != nativeHeight) continue;
            double delta = Math.Abs(r.refreshRateRatio.value - display.refreshRate.value);
            if (!found || delta < nearest) { found = true; nearest = delta; selected = r; }
        }
        for (int index = 0; index < 2; index++)
        {
            bool explicitRefresh = index == 0;
            var arm = new Arm { id = explicitRefresh ? "enumerated-nearest-current-refresh" : "default-refresh-overload",
                explicitRefresh = explicitRefresh, before = Capture(), resolutionsBefore = Resolutions(),
                requested = new Mode { width = nativeWidth, height = nativeHeight,
                    refreshNumerator = explicitRefresh && found ? selected.refreshRateRatio.numerator : 0,
                    refreshDenominator = explicitRefresh && found ? selected.refreshRateRatio.denominator : 0 } };
            report.arms.Add(arm); phase = arm.id;
            if (explicitRefresh && !found) { arm.status = "not_executed_no_matching_enumerated_mode"; Persist(); continue; }
            arm.status = "observing"; Persist(); RecordFrame();
            try
            {
                if (explicitRefresh) Screen.SetResolution(nativeWidth, nativeHeight, FullScreenMode.ExclusiveFullScreen, selected.refreshRateRatio);
                else Screen.SetResolution(nativeWidth, nativeHeight, FullScreenMode.ExclusiveFullScreen);
            }
            catch (Exception ex) { arm.requestException = ex.ToString(); Persist(); }
            double start = Time.realtimeSinceStartupAsDouble, nextPersist = start + 1;
            while (Time.realtimeSinceStartupAsDouble - start < 5)
            {
                yield return null; RecordFrame(); arm.observedFrames++;
                arm.trailingExactFrames = Screen.fullScreenMode == FullScreenMode.ExclusiveFullScreen && Screen.width == nativeWidth && Screen.height == nativeHeight ? arm.trailingExactFrames + 1 : 0;
                if (Time.realtimeSinceStartupAsDouble >= nextPersist) { Persist(); nextPersist += 1; }
            }
            arm.observedSeconds = Time.realtimeSinceStartupAsDouble - start;
            arm.exclusiveAtEnd = arm.trailingExactFrames >= 3; arm.end = Capture();
            arm.status = "observed_not_acceptance"; Persist();
            yield return WindowedStable("restore_after_" + arm.id);
            arm.restoredWindowedThreeStableFrames = true; arm.restored = Capture(); Persist();
        }
        report.finalWindowedThreeStableFrames = !restoring;
        report.finalActual = Capture(); report.status = "diagnostic_completed"; report.exitCode = 0;
        completed = true; Persist();
        UnityEngine.Debug.Log("NATIVE_MINIMAL_DIAGNOSTIC_COMPLETED " + output);
        Application.Quit(0);
    }
    private IEnumerator Guarded(IEnumerator routine)
    {
        var stack = new Stack<IEnumerator>(); stack.Push(routine);
        while (stack.Count > 0)
        {
            var current = stack.Peek(); object yielded = null; bool moved = false; Exception failure = null;
            try { moved = current.MoveNext(); if (moved) yielded = current.Current; }
            catch (Exception ex) { failure = ex; }
            if (failure != null) { Fail(failure); yield break; }
            if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
            if (yielded is IEnumerator child) stack.Push(child); else yield return yielded;
        }
    }
    private void Fail(Exception ex)
    {
        if (report != null)
        {
            report.status = "diagnostic_failed"; report.failure = ex.ToString(); report.exitCode = 1;
            report.finalActual = Capture(); Persist();
        }
        completed = true;
        try { Screen.SetResolution(1920, 1080, FullScreenMode.Windowed); } catch { }
        UnityEngine.Debug.LogError("NATIVE_MINIMAL_DIAGNOSTIC_FAILED " + ex.GetType().Name);
        if (!Application.isEditor) Application.Quit(1);
    }
    private void OnApplicationQuit()
    {
        if (report != null && !completed)
        {
            report.status = "diagnostic_interrupted"; report.exitCode = -1; report.finalActual = Capture(); Persist();
        }
        Application.logMessageReceived -= OnLog;
        frames?.Dispose(); events?.Dispose();
    }
}
