using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using DiceCraft.Core;
using Unity.Profiling;
using Unity.Profiling.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Profiling;

namespace DiceCraft.Verification
{
    /// <summary>
    /// Opt-in built-player acceptance probe. No GameObject, files, or save override are
    /// created unless --verify-p0 is present. Normal replay uses GameApp's real command
    /// and presentation paths. The subsequently installed stress fixture is explicitly
    /// Debug, isolated from both the normal replay and the user's persistent saves.
    /// </summary>
    public sealed class PlayerVerification : MonoBehaviour
    {
        private const string ProbeRevision = "p05-player-independent-clocks-7";
        private const double WarmupSeconds = 10;
        private const double SampleSeconds = 180;
        private const double FrameBudgetMs = 16.7;
        // Evidence completeness criterion, not a replacement for the spec's frame budget.
        private const double RequiredCoverage = .95;
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static string configuredTrace, configuredDirectory, startupFailure;
        private GameApp app;
        private Trace trace;
        private readonly List<ActualStep> actualSteps = new List<ActualStep>();
        private readonly List<string> unityErrors = new List<string>();
        private readonly List<string> screenshots = new List<string>();
        private readonly List<string> rejectedScreenshots = new List<string>();
        private readonly List<string> graphicsTimingWarnings = new List<string>();
        private readonly List<Dictionary<string, object>> screenshotDetails = new List<Dictionary<string, object>>();
        private readonly List<Dictionary<string, object>> animationChecks = new List<Dictionary<string, object>>();
        private readonly List<FrameSample> frames = new List<FrameSample>();
        private readonly List<Marker> markers = new List<Marker>();
        private readonly FrameTiming[] frameTimings = new FrameTiming[1];
        private Dictionary<string, object> environment, performance, productionPacing;
        private string configHash, traceHash, normalSave, normalSaveHash, stressSave, phase = "Starting";
        private bool finished, sampling, normalPassed, stressCompleted, frameTimingEnabled;
        private int currentStep = -1, rollAnimations, skippedAnimations, stressRolls, stressReorders, stressSelections;
        private int reducedMotionChecks, handbookPureReadChecks;
        private bool debug24Passed;
        private bool checkpointExported;
        private string importedCheckpointStatus="not_requested";
        [Serializable]private sealed class CheckpointExpectation{public string inputStateSha256,outputStateSha256;public long submissionScore;}
        private readonly List<Dictionary<string, object>> debug24Steps = new List<Dictionary<string, object>>();
        private string normalFinalPhase; private int normalFinalStage;
        private int gpuInvalidSamples, gpuMissingSamples, gpuDuplicateTimestamps, timingReadFrames, focusedSampleFrames;
        private int timingWarningsDuringSampling;
        private int frameBufferGrowthEvents;
        private int samplingFrameCapacity, frameSampleRecordBytes;
        private double warmupObservedFps;
        private long observerFramePayloadBytes;
        private bool captureInProgress;
        private double captureDeadline;
        private int firstCollection0, firstCollection1, firstCollection2;
        private int collected0, collected1, collected2;
        private double samplingStart, samplingEnd, stressStart, lastTick;
        private ulong lastFrameTimestamp;
        private ulong cpuTimerFrequency;
        private CpuClockChain cpuClocks;
        private Marker mainThread, renderThread, gcAllocated, gcUsed, uiBuild, gpuCounter;

        [Serializable] private sealed class Trace
        {
            public uint seed;
            public string configVersion, policy, expectedFinalPhase;
            public int expectedFinalStage;
            public bool isDebug;
            public TraceStep[] steps;
        }
        [Serializable] private sealed class TraceStep
        {
            public string kind, offerId, itemInstanceId, targetDieId, targetFaceId, handId;
            public string[] dieIds;
            public int[] targetPips;
            public bool risk;
            public uint seed;
            public long actionSequence, submissionScore, stageScore, target;
            public int gold, stageIndex, rollsRemaining, remainingSubmissions;
            public string phase;
        }
        [Serializable] private sealed class ActualStep
        {
            public int index;
            public string kind, commandId, phase;
            public long actionSequence, submissionScore, stageScore, target;
            public int gold, stageIndex, rollsRemaining, remainingSubmissions;
            public uint diceRngState, shopRngState, effectRngState;
            public bool isDebug, savedStateMatches, renderedStateMatches;
            public string[] pendingConfirmed;
            public bool qualified;
            public string ruleStateSha256;
        }
        private struct FrameSample
        {
            public int frame;
            public double elapsed, frameMs, mainMs, renderMs, timingCpuMs, gpuMs;
            public double gpuRawMs, gpuCounterMs;
            public int gpuSampleStatus, uiSampleBatch;
            public double gcAllocatedBytes, gcUsedBytes, uiBuildMs, uiBuildCalls, monoUsedBytes;
            public ulong frameStartTimestamp, presentCalledTimestamp, frameCompleteTimestamp;
            public double frameStartDeltaMs, presentCalledDeltaMs, frameCompleteDeltaMs;
            public int timingObservationStatus, frameStartDeltaStatus, presentDeltaStatus, completeDeltaStatus;
        }
        private struct CpuClockObservation
        {
            public ulong frameStart, present, complete;
            public double startDelta, presentDelta, completeDelta;
            public int observationStatus, startStatus, presentStatus, completeStatus;
        }
        // Value types only: this observer adds no per-frame managed allocations and
        // consumes the existing GetLatestTimings result, without a new capture call.
        private struct CpuClockChain
        {
            private ulong seenStart, previousStart, previousPresent, previousComplete;
            private int previousUnityFrame;
            public CpuClockObservation Observe(bool received, ulong start, ulong present, ulong complete, ulong frequency, int unityFrame)
            {
                var result = new CpuClockObservation { frameStart = start, present = present, complete = complete,
                    startDelta = double.NaN, presentDelta = double.NaN, completeDelta = double.NaN };
                if (!received) result.observationStatus = 0;
                else if (start == 0) result.observationStatus = 1;
                else if (start == seenStart) result.observationStatus = 2;
                else if (seenStart != 0 && start < seenStart) result.observationStatus = 3;
                else
                {
                    result.observationStatus = 4;
                    if (previousUnityFrame != 0 && unityFrame != previousUnityFrame + 1)
                    {
                        result.observationStatus = 5;
                        previousStart = previousPresent = previousComplete = 0;
                    }
                    result.startDelta = ClockDelta(start, previousStart, frequency, out result.startStatus);
                    result.presentDelta = ClockDelta(present, previousPresent, frequency, out result.presentStatus);
                    result.completeDelta = ClockDelta(complete, previousComplete, frequency, out result.completeStatus);
                    seenStart = previousStart = start;
                    previousPresent = result.presentStatus == 3 ? 0 : present;
                    previousComplete = result.completeStatus == 3 ? 0 : complete;
                    previousUnityFrame = unityFrame;
                    return result;
                }
                // Keep seenStart to reject repeated native samples after an explicit gap.
                previousStart = previousPresent = previousComplete = 0;
                previousUnityFrame = unityFrame;
                return result;
            }
        }
        private static double ClockDelta(ulong current, ulong previous, ulong frequency, out int status)
        {
            if (current == 0) { status = 0; return double.NaN; }
            if (frequency == 0) { status = 4; return double.NaN; }
            if (previous == 0) { status = 1; return double.NaN; }
            if (current <= previous) { status = 3; return double.NaN; }
            status = 2; return (current - previous) * 1000d / frequency;
        }
        [Serializable] private sealed class CaptureContent
        {
            public bool valid;
            public int sampledPixels, nonBlackPixels, brightPixels, quantizedColors, minimumChannelMaximum, maximumChannelMaximum;
            public double nonBlackRatio, brightRatio;
        }
        private sealed class Marker : IDisposable
        {
            public string name, unit, unavailableReason;
            public bool nanoseconds;
            public ProfilerRecorder recorder;
            private readonly List<ProfilerRecorderSample> drained = new List<ProfilerRecorderSample>(128);
            public int lastBatchSize;
            public bool Available => recorder.Valid;
            public double Value(double divisor = 1)
            {
                return Available && recorder.Count > 0 ? recorder.LastValue / divisor : double.NaN;
            }
            public double TimeValue()
            {
                return nanoseconds ? Value(1000000) : double.NaN;
            }
            public double ReadNewUiFrame(out double calls)
            {
                calls = double.NaN;
                lastBatchSize = 0;
                if (!Available || !nanoseconds) return double.NaN;
                bool runningBefore = recorder.IsRunning;
                bool wrapped = recorder.WrappedAround;
                drained.Clear();
                // CopyTo(reset:true) clears the collected history. Explicitly restart if
                // the platform treats reset as stop; never read a stale LastValue afterward.
                recorder.CopyTo(drained, true);
                if (!recorder.IsRunning) recorder.Start();
                lastBatchSize = drained.Count;
                if (!runningBefore || !recorder.IsRunning || wrapped || recorder.Count != 0)
                {
                    unavailableReason = "UI stream stopped, wrapped, or failed to clear; affected frames are unmeasured.";
                    return double.NaN;
                }
                if (drained.Count > 1)
                {
                    unavailableReason = "Multiple UI frame samples arrived in one read; they cannot be attributed to one rendered frame.";
                    return double.NaN;
                }
                // A healthy running timer with no sample means no invocation this frame.
                if (drained.Count == 0) { calls = 0; return 0; }
                calls = drained[0].Count;
                return drained[0].Value / 1000000d;
            }
            public void BeginUiWindow()
            {
                if (!Available) return;
                recorder.Reset(); recorder.Start(); drained.Clear();
                if (!recorder.IsRunning || recorder.Count != 0)
                    unavailableReason = "UI recorder could not start an empty sampling window.";
            }
            public void Dispose() { if (recorder.Valid) recorder.Dispose(); }
        }

        // BeforeSceneLoad runs before scene objects' Awake, including GameApp's save load.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Bootstrap()
        {
            string[] args = Environment.GetCommandLineArgs();
            int flag = Array.IndexOf(args, "--verify-p0");
            if (flag < 0) return;
            try
            {
                configuredTrace = RequiredArgument(args, "--verify-p0");
                string baseDirectory = RequiredArgument(args, "--evidence-dir");
                configuredTrace = Path.GetFullPath(configuredTrace);
                configuredDirectory = Path.Combine(Path.GetFullPath(baseDirectory),
                    "player-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture) + "-" + Guid.NewGuid().ToString("N").Substring(0, 8));
                Directory.CreateDirectory(configuredDirectory);
                GameApp.SaveDirectoryOverride = Path.Combine(configuredDirectory, "normal-save");
            }
            catch (Exception ex)
            {
                // Even malformed opt-in arguments must never let GameApp load the user's save.
                configuredDirectory = Path.Combine(Path.GetTempPath(), "DiceCraft-Verification-" + Guid.NewGuid().ToString("N"));
                GameApp.SaveDirectoryOverride = Path.Combine(configuredDirectory, "normal-save");
                startupFailure = ex.ToString();
            }
            var host = new GameObject("P0 Player Verification (opt-in)");
            DontDestroyOnLoad(host);
            host.AddComponent<PlayerVerification>();
        }

        private static string RequiredArgument(string[] args, string flag)
        {
            int index = Array.IndexOf(args, flag);
            if (index < 0 || index + 1 >= args.Length || args[index + 1].StartsWith("--", StringComparison.Ordinal))
                throw new ArgumentException("Required argument: " + flag + " <path>");
            return args[index + 1];
        }

        private void Awake() { Application.logMessageReceived += OnUnityLog; }
        private void OnUnityLog(string message, string stack, LogType type)
        {
            if ((type == LogType.Error || type == LogType.Exception || type == LogType.Assert) && unityErrors.Count < 100)
                unityErrors.Add(type + ": " + message + "\n" + stack);
            if (message.IndexOf("GetFrameStatistics", StringComparison.OrdinalIgnoreCase) >= 0
                || (message.IndexOf("timestamp", StringComparison.OrdinalIgnoreCase) >= 0 && message.IndexOf("Direct3D", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                if (graphicsTimingWarnings.Count < 32 && !graphicsTimingWarnings.Contains(message)) graphicsTimingWarnings.Add(message);
                if (sampling) timingWarningsDuringSampling++;
            }
        }
        private void Start() { StartCoroutine(Guard(Run())); }

        // Drive nested enumerators ourselves so exceptions in any verification coroutine
        // generate a durable failure report instead of silently terminating the test.
        private IEnumerator Guard(IEnumerator work)
        {
            var stack = new Stack<IEnumerator>();
            stack.Push(work);
            while (stack.Count > 0 && !finished)
            {
                object next = null;
                Exception error = null;
                bool moved = false;
                try
                {
                    moved = stack.Peek().MoveNext();
                    if (moved) next = stack.Peek().Current;
                }
                catch (Exception ex) { error = ex; }
                if (error != null)
                {
                    // Execute inspection cleanup (including restoring an isolated Debug
                    // preview session) before reporting and quitting on nested failures.
                    while (stack.Count > 0)
                    {
                        try { (stack.Pop() as IDisposable)?.Dispose(); }
                        catch (Exception cleanup) { error = new AggregateException(error, cleanup); }
                    }
                    Fail(error); yield break;
                }
                if (!moved) { (stack.Pop() as IDisposable)?.Dispose(); continue; }
                if (next is IEnumerator nested) stack.Push(nested);
                else yield return next;
            }
        }

        private IEnumerator Run()
        {
            Directory.CreateDirectory(configuredDirectory);
            if (startupFailure != null) throw new ArgumentException(startupFailure);
            Require(!Application.isEditor, "P0 player acceptance must run in a built player, not the Unity Editor.");
            Debug.Log("P0 verification evidence: " + configuredDirectory);
            double deadline = Time.realtimeSinceStartupAsDouble + 30;
            while (app == null || app.Session == null || app.Config == null)
            {
                app = FindAnyObjectByType<GameApp>();
                Require(Time.realtimeSinceStartupAsDouble < deadline, "GameApp was not ready within 30 seconds.");
                yield return null;
            }
            Require(SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null, "A real rendered player is required; null graphics is not visual/performance evidence.");
            normalSave = app.SaveLocation;
            Require(IsWithin(normalSave, Path.Combine(configuredDirectory, "normal-save")), "Normal save is outside the evidence directory.");
            Require(app.Session.State == null, "The isolated normal replay must start without a prior run.");
            // Fix presentation settings in memory only; never overwrite PlayerPrefs.
            SetPrivate(app, "fast", false);
            SetPrivate(app, "reduced", false);
            Require(app.table.ImportedRollClipCount == 2, "The built scene must contain both imported roll clips.");
            // A hidden/no-op initial window size can leave the first swapchain black.
            // Force a real alternate-size transition, then require actual rendered content.
            yield return SetActualResolution(1280, 720);
            app.Refresh();
            yield return new WaitForSecondsRealtime(.25f);
            yield return SetActualResolution(1920, 1080);
            app.Refresh();
            environment = EnvironmentReport();
            string json = File.ReadAllText(configuredTrace, Encoding.UTF8);
            traceHash = Hash(json);
            configHash = Hash(app.configAsset.text);
            trace = JsonUtility.FromJson<Trace>(json);
            Require(trace != null && trace.steps != null && trace.steps.Length > 0, "Trace is empty.");
            Require(!trace.isDebug && trace.configVersion == app.Config.configVersion, "Trace is debug or uses another config version.");
            Require(trace.steps[0].kind == "NewRun" && trace.steps[0].seed == trace.seed, "Trace must start with its declared seed.");
            if(Array.IndexOf(Environment.GetCommandLineArgs(),"--verify-import-save")>=0)
            {
                VerifyCheckpoint(RequiredArgument(Environment.GetCommandLineArgs(),"--verify-import-save"),"imported-checkpoint");
                importedCheckpointStatus="passed";
            }
            phase = "MainMenuCapture";
            WriteReport("running", null, -1);
            yield return Capture("startup-render-ready", 3);
            yield return Capture("main-menu-1920x1080");
            yield return CaptureResolutionSet("build-version",()=>InvokePrivate("ShowBuildVersion"));
            phase = "NormalReplay";
            for (int i = 0; i < trace.steps.Length; i++)
            {
                currentStep = i;
                TraceStep step = trace.steps[i];
                yield return WaitReady();
                yield return null; // Separate real inputs; never start a roll in a screenshot-encoding frame.
                Require(i == 0 || step.kind != "NewRun", "The normal trace must contain exactly one NewRun.");
                Require(Enum.TryParse(step.kind, out CommandKind kind) && Enum.IsDefined(typeof(CommandKind), kind), "Unknown trace command " + step.kind);
                bool reducedThisRoll = kind == CommandKind.Roll && rollAnimations == 1;
                if (reducedThisRoll) SetPrivate(app, "reduced", true);
                CommandResult result = null;
                if (kind == CommandKind.NewRun) app.StartNew(step.seed.ToString(CultureInfo.InvariantCulture));
                else
                {
                    if (kind == CommandKind.ConfirmLocks)
                    {
                        Require(app.Session.PendingIds.Length == 0, "Unexpected pending selection before ConfirmLocks.");
                        foreach (string id in step.dieIds ?? Array.Empty<string>())
                            Require(app.Session.TogglePending(id), "Could not select " + id);
                        app.Refresh();
                        yield return null; // Render the same pending selection the player sees.
                    }
                    result = app.Send(new SessionCommand
                    {
                        kind = kind, seed = step.seed, risk = step.risk,
                        offerId = step.offerId, itemInstanceId = step.itemInstanceId,
                        targetDieId = step.targetDieId, targetFaceId = step.targetFaceId, handId = step.handId,
                        dieIds = step.dieIds ?? Array.Empty<string>(), targetPips = step.targetPips ?? Array.Empty<int>()
                    });
                    Require(result.accepted && !result.duplicate && !result.pendingSave, "Rejected " + step.kind + ": " + result.error);
                }
                if (app.IsBusy)
                {
                    if (kind == CommandKind.Roll) rollAnimations++;
                    if (kind == CommandKind.Roll && rollAnimations == 1) yield return VerifyFirstAnimatedRoll();
                    if (reducedThisRoll)
                    {
                        Require(!app.table.IsRolling, "Reduced-motion Roll must finish the table's zero-duration presentation immediately.");
                        reducedMotionChecks++;
                    }
                    // Exercise both complete animations and the production skip path.
                    if (kind == CommandKind.Roll && rollAnimations % 3 == 0)
                    {
                        yield return new WaitForSecondsRealtime(.12f);
                        app.SkipVisual();
                        skippedAnimations++;
                    }
                }
                yield return WaitReady();
                if (reducedThisRoll) SetPrivate(app, "reduced", false);
                if (kind == CommandKind.Roll) VerifyLandedDice(reducedThisRoll ? "reduced-zero" : rollAnimations % 3 == 0 ? "skipped" : "completed");
                RunState state = app.Session.State;
                Require(state != null && !state.isDebug, "Normal replay acquired debug state.");
                CommandReceipt receipt = result?.receipt ?? state.processedReceipts.LastOrDefault();
                Require(receipt != null, "Command has no saved receipt.");
                Require(receipt.command.kind == kind && receipt.actionSequence == step.actionSequence, "Saved command receipt differs from the trace command.");
                ValidateExpected(step, state, receipt);
                string committed = JsonUtility.ToJson(state);
                var loaded = new AtomicSaveStore(normalSave, app.Config).Load();
                Require(loaded != null && JsonUtility.ToJson(loaded) == committed, "On-disk state differs from the accepted state.");
                Require(app.ViewState != null && JsonUtility.ToJson(app.ViewState) == committed, "UI is not rendering the accepted state.");
                actualSteps.Add(new ActualStep
                {
                    index = i, kind = step.kind, commandId = receipt.commandId, actionSequence = state.actionSequence,
                    submissionScore = receipt.score, phase = state.phase, stageScore = state.stage.score, target = state.stage.target,
                    gold = state.gold, stageIndex = state.stage.stageIndex, rollsRemaining = state.hand?.rollsRemaining ?? 0,
                    remainingSubmissions = state.stage.remainingSubmissions, isDebug = state.isDebug,
                    diceRngState = state.diceRngState, shopRngState = state.shopRngState, effectRngState = state.effectRngState,
                    pendingConfirmed = kind == CommandKind.ConfirmLocks ? step.dieIds : Array.Empty<string>(),
                    savedStateMatches = true, renderedStateMatches = true, qualified = state.stage.qualified, ruleStateSha256 = RuleStateHash(state)
                });
                File.AppendAllText(Path.Combine(configuredDirectory, "normal-replay.jsonl"), JsonUtility.ToJson(actualSteps[actualSteps.Count - 1]) + "\n", Encoding.UTF8);
                CheckUnityErrors();
                if(!checkpointExported&&state.stage.stageIndex>=2&&kind==CommandKind.ConfirmLocks&&state.hand.orderedLockedIds.Count>0)
                    ExportCheckpoint(state);
                if (state.stage.qualified && state.phase == "HandReady" && screenshots.All(x => !x.StartsWith("normal-qualified", StringComparison.Ordinal))) yield return CaptureResolutionSet("normal-qualified-continue-or-end");
                if (state.phase == "StageSettlement") yield return Capture("normal-stage-" + (state.stage.stageIndex + 1));
                else if (state.phase == "Shop" && screenshots.All(x => !x.StartsWith("normal-shop", StringComparison.Ordinal)))
                    yield return CaptureResolutionSet("normal-shop");
                else if (state.phase == "Selecting" && state.hand.hasRolled && !app.IsBusy && screenshots.All(x => !x.StartsWith("normal-selecting", StringComparison.Ordinal)))
                {
                    yield return CaptureResolutionSet("normal-selecting");
                    yield return VerifyHandbookPureReads();
                    yield return CaptureResolutionSet("normal-handbook", () => InvokePrivate("ShowHandbook"), true, "HandbookScroll");
                    string dieId = state.dice[0].dieId;
                    yield return CaptureResolutionSet("normal-die-detail-permanent-preview", () => InvokePrivate("ShowDie", dieId));
                }
                if (state.phase == "Selecting" && state.hand.orderedLockedIds.Count > 0 && screenshots.All(x => !x.StartsWith("normal-score-preview", StringComparison.Ordinal)))
                {
                    var snapshot = app.Session.PreviewSnapshot(); var breakdown = app.Session.PreviewScore();
                    yield return CaptureResolutionSet("normal-score-preview", () => InvokePrivate("ShowBreakdown", snapshot, breakdown), true);
                }
                WriteReport("running", null, -1);
            }
            normalFinalPhase = app.Session.State.phase; normalFinalStage = app.Session.State.stage.stageIndex + 1;
            Require(trace.expectedFinalPhase == "RunWon" || trace.expectedFinalPhase == "RunFailed", "Trace must explicitly declare a terminal normal outcome.");
            Require(normalFinalPhase == trace.expectedFinalPhase && normalFinalStage == trace.expectedFinalStage, "Trace did not match its declared terminal stage/outcome.");
            Require(normalFinalPhase != "RunWon" || normalFinalStage == 24, "A normal win must finish stage 24.");
            Require(rollAnimations > 0 && skippedAnimations > 0 && reducedMotionChecks > 0, "Full animation, reduced-motion and skip paths were not all exercised.");
            yield return Capture(normalFinalPhase == "RunWon" ? "normal-run-won" : "normal-run-failed-stage-" + normalFinalStage);
            File.WriteAllText(Path.Combine(configuredDirectory, "normal-final-state.json"), JsonUtility.ToJson(app.Session.State, true), Encoding.UTF8);
            normalSaveHash = Hash(File.ReadAllText(normalSave, Encoding.UTF8));
            normalPassed = true;
            WriteReport("running", null, -1);
            yield return VerifyDebugTwentyFourStages();
            yield return RunStress();
            Require(Hash(File.ReadAllText(normalSave, Encoding.UTF8)) == normalSaveHash, "Debug stress modified the isolated normal save.");
            CheckUnityErrors();
            phase = "Completed";
            string performanceStatus = (string)performance["status"];
            int exitCode = performanceStatus == "budget_exceeded" ? 3 : performanceStatus == "measured_with_gaps" ? 2 : 0;
            WriteReport(exitCode == 0 ? "passed" : performanceStatus, null, exitCode);
            finished = true;
            Debug.Log("P0 verification complete: normal replay passed; performance " + performanceStatus + "; evidence " + configuredDirectory);
            Application.Quit(exitCode);
        }

        private IEnumerator VerifyHandbookPureReads()
        {
            Require(!sampling && !app.IsBusy, "Handbook checks must occur outside the sample window.");
            string original = JsonUtility.ToJson(app.Session.State);
            string saved = Hash(File.ReadAllText(app.SaveLocation, Encoding.UTF8));
            string[] pending = app.Session.PendingIds;
            for (int i = 0; i < 20; i++)
            {
                InvokePrivate("ShowHandbook"); yield return null;
                Require((bool)GetPrivate(app, "modal"), "Handbook did not open.");
                if (i == 0)
                {
                    var texts = ((Transform)GetPrivate(app, "overlay")).GetComponentsInChildren<Component>(true)
                        .Where(c => c != null && c.gameObject.activeInHierarchy && c.GetType().FullName == "TMPro.TextMeshProUGUI")
                        .Select(c => c.GetType().GetProperty("text").GetValue(c)?.ToString() ?? "");
                    string allText = string.Join("\n", texts);
                    Require(allText.Contains("散点"), "Handbook scatter row is missing.");
                    foreach (var hand in app.Config.handDefinitions) Require(allText.Contains(hand.displayName), "Handbook missing row " + hand.displayName);
                }
                InvokePrivate("CloseModal"); yield return null;
                Require(!(bool)GetPrivate(app, "modal") && JsonUtility.ToJson(app.Session.State) == original && app.Session.PendingIds.SequenceEqual(pending), "Handbook inspection changed rules or pending selection.");
                Require(Hash(File.ReadAllText(app.SaveLocation, Encoding.UTF8)) == saved, "Handbook inspection wrote the save.");
                handbookPureReadChecks++;
            }
        }

        private IEnumerator VerifyDebugTwentyFourStages()
        {
            phase = "DebugTwentyFourStages";
            var initial = new GameSession(app.Config, new MemorySaveStore());
            Accept(initial.Execute(new SessionCommand { kind = CommandKind.NewRun, seed = 20261030, commandId = "debug24-new", expectedActionSequence = 0 }));
            var state = initial.State;
            state.isDebug = true; state.runId = "DEBUG-24-STAGES";
            foreach (var die in state.dice) foreach (var face in die.faces) { face.permanentPip = 4; face.baseColor = "red"; face.isGold = true; }
            foreach (var level in state.handLevels) level.level = 8;
            string[] ids = { "C25", "C30", "C31", "C32" };
            for (int i = 0; i < ids.Length; i++) state.controllers.Add(new ControllerInstance { instanceId = "ITEM_" + (i + 1).ToString("D5"), acquiredSerial = i + 1, definitionId = ids[i], enchantmentId = "none" });
            state.nextItemSerial = 5;
            string directory = Path.Combine(configuredDirectory, "debug24-save");
            var store = new AtomicSaveStore(Path.Combine(directory, "active-run.json"), app.Config);
            var session = new GameSession(app.Config, store, state); store.Save(state);
            typeof(GameApp).GetProperty(nameof(GameApp.Session)).GetSetMethod(true).Invoke(app, new object[] { session });
            SetPrivate(app, "saves", store); GameApp.SaveDirectoryOverride = directory;
            SetPrivate(app, "inMenu", false); SetPrivate(app, "tutorial", false); SetPrivate(app, "reduced", true); app.Refresh();
            File.WriteAllText(Path.Combine(configuredDirectory, "debug24-initial-state.json"), JsonUtility.ToJson(state, true), Encoding.UTF8);
            int shops = 0, risks = 0;
            for (int stage = 0; stage < 24; stage++)
            {
                Require(app.Session.State.stage.stageIndex == stage && app.Session.State.phase == "StagePreview", "Debug24 stage preview mismatch.");
                if (app.Config.stages[stage].riskEnabled) { yield return Debug24Action(CommandKind.ChooseChallenge, c => c.risk = true); risks++; }
                yield return Debug24Action(CommandKind.StartStage);
                yield return Debug24Action(CommandKind.Roll);
                yield return Debug24Action(CommandKind.ConfirmLocks, c => c.dieIds = app.Session.State.dice.Select(d => d.dieId).ToArray());
                int goldBefore = app.Session.State.gold;
                yield return Debug24Action(CommandKind.Submit);
                Require(app.Session.State.stage.qualified && app.Session.State.phase == "HandReady" && app.Session.State.gold == goldBefore, "Debug24 qualification paid early or ended early.");
                if (stage == 0)
                {
                    yield return Debug24Action(CommandKind.Roll);
                    yield return Debug24Action(CommandKind.ConfirmLocks, c => c.dieIds = app.Session.State.dice.Select(d => d.dieId).ToArray());
                    yield return Debug24Action(CommandKind.Submit);
                    Require(app.Session.State.stage.submissionOrdinal == 2 && app.Session.State.stage.remainingSubmissions == 1 && app.Session.State.gold == goldBefore, "Qualified continuation reset stage counters or paid early.");
                }
                yield return Debug24Action(CommandKind.EndStage);
                Require(app.Session.State.phase == "StageSettlement", "Debug24 EndStage did not produce one settlement.");
                if (stage == 0 || stage == 23) yield return Capture("debug24-settlement-stage-" + (stage + 1));
                yield return Debug24Action(CommandKind.Continue);
                if (stage < 23)
                {
                    Require(app.Session.State.phase == "Shop" && app.Session.State.shop != null, "Debug24 missing shop."); shops++;
                    yield return Debug24Action(CommandKind.LeaveShop);
                }
            }
            Require(shops == 23 && risks == 8 && app.Session.State.phase == "RunWon" && app.Session.State.stage.stageIndex == 23 && app.Session.State.shop == null, "Debug24 did not finish exactly 24 stages, 8 risks and 23 shops.");
            yield return Capture("debug24-run-won");
            File.WriteAllText(Path.Combine(configuredDirectory, "debug24-final-state.json"), JsonUtility.ToJson(app.Session.State, true), Encoding.UTF8);
            debug24Passed = true;
            SetPrivate(app, "reduced", false);
            Require(Hash(File.ReadAllText(normalSave, Encoding.UTF8)) == normalSaveHash, "Debug24 mutated the normal save.");
        }

        private IEnumerator Debug24Action(CommandKind kind, Action<SessionCommand> configure = null)
        {
            yield return WaitReady();
            var command = new SessionCommand { kind = kind }; configure?.Invoke(command);
            if (kind == CommandKind.ConfirmLocks)
            {
                foreach (string id in command.dieIds) Require(app.Session.TogglePending(id), "Debug24 could not select " + id);
                app.Refresh();
            }
            var result = app.Send(command); Accept(result);
            if (app.IsBusy) app.SkipVisual();
            yield return WaitReady();
            var state = app.Session.State; string committed = JsonUtility.ToJson(state);
            Require(state.isDebug && JsonUtility.ToJson(new AtomicSaveStore(app.SaveLocation, app.Config).Load()) == committed && JsonUtility.ToJson(app.ViewState) == committed, "Debug24 disk/UI state mismatch.");
            debug24Steps.Add(new Dictionary<string, object> {
                ["kind"] = kind.ToString(), ["sequence"] = state.actionSequence, ["stage"] = state.stage.stageIndex + 1,
                ["phase"] = state.phase, ["qualified"] = state.stage.qualified, ["gold"] = state.gold,
                ["score"] = state.stage.score, ["risk"] = state.stage.risk, ["ruleStateSha256"] = RuleStateHash(state)
            });
            File.AppendAllText(Path.Combine(configuredDirectory, "debug24-replay.jsonl"), Json(debug24Steps[debug24Steps.Count - 1]) + "\n", Encoding.UTF8);
            CheckUnityErrors(); yield return null;
        }

        private static string RuleStateHash(RunState state)
        {
            // Strip only run/command identity which the presentation generates from GUIDs.
            // Every rule-bearing field and all three RNG streams remain in the digest.
            var canonical = state.Clone(); canonical.runId = "CANONICAL-RUN";
            canonical.processedReceipts.Clear();
            if (!string.IsNullOrEmpty(canonical.stage.settlementId)) canonical.stage.settlementId = "CANONICAL-RUN/" + canonical.stage.stageId;
            if (canonical.lastStageSettlement != null) canonical.lastStageSettlement.settlementId = "CANONICAL-RUN/" + canonical.lastStageSettlement.stageId;
            if (canonical.lastSubmission != null) canonical.lastSubmission.snapshot.runId = "CANONICAL-RUN";
            return Hash(JsonUtility.ToJson(canonical));
        }

        private void ExportCheckpoint(RunState state)
        {
            string folder=Path.Combine(configuredDirectory,"cross-platform-checkpoint");Directory.CreateDirectory(folder);
            File.Copy(normalSave,Path.Combine(folder,"active-run.json"),false);
            var command=new SessionCommand{kind=CommandKind.Submit,commandId="cross-platform-submit",expectedActionSequence=state.actionSequence};
            var trial=new GameSession(app.Config,new MemorySaveStore(),state);
            var result=trial.Execute(command);Require(result.accepted&&!result.pendingSave,"Checkpoint continuation rejected.");
            File.WriteAllText(Path.Combine(folder,"command.json"),JsonUtility.ToJson(command),new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(folder,"expected.json"),JsonUtility.ToJson(new CheckpointExpectation{
                inputStateSha256=RuleStateHash(state),outputStateSha256=RuleStateHash(trial.State),submissionScore=result.receipt.score}),new UTF8Encoding(false));
            VerifyCheckpoint(folder,"local-checkpoint-reload");checkpointExported=true;
        }

        private void VerifyCheckpoint(string folder,string outputName)
        {
            folder=Path.GetFullPath(folder);string source=Path.Combine(folder,"active-run.json");
            string originalHash=FileHash(source),local=Path.Combine(configuredDirectory,outputName,"active-run.json");
            Directory.CreateDirectory(Path.GetDirectoryName(local));File.Copy(source,local,false);
            var expected=JsonUtility.FromJson<CheckpointExpectation>(File.ReadAllText(Path.Combine(folder,"expected.json")));
            var command=JsonUtility.FromJson<SessionCommand>(File.ReadAllText(Path.Combine(folder,"command.json")));
            var store=new AtomicSaveStore(local,app.Config);var restored=store.Load();
            Require(restored!=null&&RuleStateHash(restored)==expected.inputStateSha256,"Cross-platform checkpoint input differs.");
            var session=new GameSession(app.Config,store,restored);var result=session.Execute(command);
            Require(result.accepted&&!result.duplicate&&!result.pendingSave&&result.receipt.score==expected.submissionScore,"Cross-platform continuation score differs.");
            Require(RuleStateHash(session.State)==expected.outputStateSha256,"Cross-platform continuation rules/RNG differ.");
            var reloaded=new GameSession(app.Config,store,store.Load());
            Require(RuleStateHash(reloaded.State)==expected.outputStateSha256,"Cross-platform result did not persist.");
            Require(reloaded.Execute(command).duplicate&&RuleStateHash(reloaded.State)==expected.outputStateSha256,"Cross-platform restart duplicated a submission.");
            Require(FileHash(source)==originalHash,"Imported original save was modified.");
            File.WriteAllText(Path.Combine(configuredDirectory,outputName,"verification.json"),Json(new Dictionary<string,object>{
                ["passed"]=true,["sourceSaveSha256"]=originalHash,["inputStateSha256"]=expected.inputStateSha256,
                ["outputStateSha256"]=expected.outputStateSha256,["submissionScore"]=expected.submissionScore}),new UTF8Encoding(false));
        }

        private IEnumerator WaitReady()
        {
            double deadline = Time.realtimeSinceStartupAsDouble + 10;
            while (app.IsBusy)
            {
                Require(Time.realtimeSinceStartupAsDouble < deadline, "Presentation Busy did not finish within 10 seconds.");
                yield return null;
            }
            Require(!app.Session.HasPendingSave, "Save failure is pending.");
        }
        private static void ValidateExpected(TraceStep expected, RunState actual, CommandReceipt receipt)
        {
            string context = "Trace #" + expected.actionSequence + " " + expected.kind + ": ";
            Require(actual.actionSequence == expected.actionSequence, context + "action sequence mismatch");
            Require(receipt.score == expected.submissionScore, context + "submission score mismatch");
            Require(actual.stage.score == expected.stageScore && actual.stage.target == expected.target, context + "stage score/target mismatch");
            Require(actual.gold == expected.gold && actual.stage.stageIndex == expected.stageIndex, context + "gold/stage mismatch");
            Require((actual.hand?.rollsRemaining ?? 0) == expected.rollsRemaining && actual.stage.remainingSubmissions == expected.remainingSubmissions, context + "budget mismatch");
            Require(actual.phase == expected.phase, context + "phase mismatch: " + actual.phase + " != " + expected.phase);
        }

        private IEnumerator RunStress()
        {
            phase = "DebugStressWarmup";
            InstallStressFixture();
            yield return null;
            yield return CaptureUnlockedDebugPool();
            yield return CaptureResolutionSet("debug-stress-capacity");
            yield return CaptureResolutionSet("debug-handbook-eight-capacity", () => InvokePrivate("ShowHandbook"), true, "HandbookScroll");
            yield return CaptureResolutionSet("debug-die-detail-permanent-preview", () => InvokePrivate("ShowDie", "D002"));
            var stressSnapshot = app.Session.PreviewSnapshot(); var stressBreakdown = app.Session.PreviewScore();
            yield return CaptureResolutionSet("debug-score-preview", () => InvokePrivate("ShowBreakdown", stressSnapshot, stressBreakdown), true);
            DiscoverMarkers();
            frameTimingEnabled = FrameTimingManager.IsFeatureEnabled();
            cpuTimerFrequency = FrameTimingManager.GetCpuTimerFrequency();
            productionPacing = ReadProductionPacing();
            stressStart = Time.realtimeSinceStartupAsDouble;
            int warmupStartFrame = Time.frameCount;
            int cycle = -1, operation = 0;
            // Real game interactions throughout warmup and sampling. The only fabricated
            // state is the explicitly saved Debug fixture, installed once before this loop.
            while (Time.realtimeSinceStartupAsDouble - stressStart < WarmupSeconds + SampleSeconds)
            {
                double elapsed = Time.realtimeSinceStartupAsDouble - stressStart;
                int newCycle = (int)Math.Floor(elapsed);
                if (newCycle != cycle && (cycle < 0 || operation == 4))
                {
                    Require(!app.IsBusy && app.Session.PendingIds.Length == 0, "Stress cycle was not ready.");
                    cycle = newCycle; operation = 0;
                    AssertStressCapacity();
                    Accept(app.Send(new SessionCommand { kind = CommandKind.Roll }));
                    stressRolls++;
                }
                double within = elapsed - cycle;
                if (operation == 0 && within >= .25)
                {
                    app.SkipVisual(); skippedAnimations++; operation = 1;
                }
                if (operation == 1 && within >= .50)
                {
                    var ids = app.Session.State.hand.orderedLockedIds.ToArray();
                    string first = ids[0]; Array.Copy(ids, 1, ids, 0, ids.Length - 1); ids[ids.Length - 1] = first;
                    Accept(app.Send(new SessionCommand { kind = CommandKind.Reorder, dieIds = ids }));
                    stressReorders++; operation = 2;
                }
                if (operation == 2 && within >= .70)
                {
                    Require(app.Session.TogglePending("D009"), "Stress pending selection failed.");
                    app.Refresh(); stressSelections++; operation = 3;
                }
                if (operation == 3 && within >= .85)
                {
                    app.Session.ClearPending(); app.Refresh(); operation = 4;
                }
                if (!sampling && elapsed >= WarmupSeconds)
                {
                    phase = "DebugStressSampling";
                    warmupObservedFps = (Time.frameCount - warmupStartFrame) / (Time.realtimeSinceStartupAsDouble - stressStart);
                    samplingFrameCapacity = RequiredFrameCapacity(warmupObservedFps);
                    // The only frame-array allocation occurs before the measured window.
                    // No old large backing array is abandoned here: the list starts empty.
                    frames.Capacity = samplingFrameCapacity;
                    frameSampleRecordBytes = Marshal.SizeOf<FrameSample>();
                    observerFramePayloadBytes = (long)samplingFrameCapacity * frameSampleRecordBytes;
                    // ProfilerRecorder counters describe a completed frame. Let both
                    // the allocation frame and its published counter value pass before
                    // opening the measured window; keep every subsequently sampled frame.
                    yield return null;
                    yield return null;
                    firstCollection0 = GC.CollectionCount(0); firstCollection1 = GC.CollectionCount(1); firstCollection2 = GC.CollectionCount(2);
                    samplingStart = lastTick = Time.realtimeSinceStartupAsDouble;
                    uiBuild.BeginUiWindow();
                    sampling = true;
                }
                CheckUnityErrors();
                yield return null;
            }
            // The sample interval starts on the first frame after warmup, so explicitly
            // keep operating until >= 180 measured seconds (not merely 190 wall seconds).
            while (Time.realtimeSinceStartupAsDouble - samplingStart < SampleSeconds)
            {
                if (app.IsBusy) app.SkipVisual();
                app.Session.ClearPending(); app.Refresh();
                yield return null;
            }
            samplingEnd = Time.realtimeSinceStartupAsDouble;
            sampling = false;
            collected0 = GC.CollectionCount(0) - firstCollection0;
            collected1 = GC.CollectionCount(1) - firstCollection1;
            collected2 = GC.CollectionCount(2) - firstCollection2;
            app.SkipVisual(); app.Session.ClearPending(); app.Refresh();
            AssertStressCapacity();
            Require(frames.Count > 0 && samplingEnd - samplingStart >= SampleSeconds, "Stress sampling did not cover 180 seconds.");
            stressCompleted = true;
            performance = BuildPerformanceReport();
            WriteFrames();
            foreach (var marker in markers) marker.Dispose();
            yield return Capture("debug-stress-final");
            File.WriteAllText(Path.Combine(configuredDirectory, "debug-final-state.json"), JsonUtility.ToJson(app.Session.State, true), Encoding.UTF8);
        }

        private void InstallStressFixture()
        {
            var memory = new MemorySaveStore();
            var isolated = new GameSession(app.Config, memory);
            Accept(isolated.Execute(new SessionCommand { kind = CommandKind.NewRun, seed = 20261028, commandId = "debug-fixture-new", expectedActionSequence = 0 }));
            Accept(isolated.Execute(new SessionCommand { kind = CommandKind.StartStage, commandId = "debug-fixture-start", expectedActionSequence = 1 }));
            RunState s = isolated.State;
            s.isDebug = true; s.runId = "DEBUG-PERFORMANCE-" + Guid.NewGuid().ToString("N");
            s.baseSubmitCapacity = 5; s.controllerSlots = 4; s.consumableSlots = 5;
            s.stage.capacity = 8;
            s.phase = "Selecting"; s.dice.Clear(); s.hand.currentResults.Clear(); s.hand.orderedLockedIds.Clear();
            // Extra rolls are a Debug stress budget, never part of the normal run.
            s.hand.hasRolled = true; s.hand.rollsUsed = 1; s.hand.rollsRemaining = 500;
            s.hand.largeBuff = true; s.hand.smallBuff = true;
            for (int i = 1; i <= 10; i++)
            {
                var die = DiceFactory.CreateStandard(app.Config, i, i <= 8 ? "permanent" : "stage_temporary", i <= 8 ? "" : s.stage.stageId);
                // Glass dice stay locked; rolling the two non-glass temporary dice
                // preserves the required maximum population throughout measurement.
                die.isGlass = i == 2 || i == 5;
                for (int f = 0; f < die.faces.Length; f++)
                {
                    die.faces[f].texture = (i + f) % 3 == 0 ? "rough" : (i + f) % 3 == 1 ? "smooth" : "none";
                    die.faces[f].isGold = die.isGlass || i % 3 == 0 || f == 0;
                    die.faces[f].baseColor = i % 2 == 0 ? "red" : "blue";
                }
                var face = die.faces[(i - 1) % 6];
                s.dice.Add(die);
                s.hand.currentResults.Add(new DieHandState { dieId = die.dieId, hasRolled = true, isLocked = i <= 8,
                    currentFaceId = face.faceId, currentPip = face.permanentPip, currentColor = face.baseColor });
                if (i <= 8) s.hand.orderedLockedIds.Add(die.dieId);
            }
            s.nextDieSerial = 11;
            s.controllers.Clear(); s.inventory.Clear();
            string[] controllerIds = { "C03", "C06", "C12", "C39", "C21", "C22", "C25", "C30", "C31", "C36" };
            for (int i = 0; i < controllerIds.Length; i++) s.controllers.Add(new ControllerInstance {
                instanceId = "ITEM_" + (i + 1).ToString("D5"), acquiredSerial = i + 1, definitionId = controllerIds[i],
                enchantmentId = i < 2 ? "base5" : i < 4 ? "mult120" : "slotless" });
            string[] itemIds = { "I01", "I03", "I04", "I06", "I07" };
            for (int i = 0; i < itemIds.Length; i++) s.inventory.Add(new OwnedItem { instanceId = "DEBUG-I-" + i, contentId = itemIds[i] });
            s.nextItemSerial = 16;
            string directory = Path.Combine(configuredDirectory, "debug-stress-save");
            stressSave = Path.Combine(directory, "active-run.json");
            var store = new AtomicSaveStore(stressSave, app.Config);
            var session = new GameSession(app.Config, store, s); // Production invariants validate the fixture.
            store.Save(s);
            File.WriteAllText(Path.Combine(configuredDirectory, "debug-initial-state.json"), JsonUtility.ToJson(s, true), Encoding.UTF8);
            // This stress Session replacement is deliberately outside
            // the normal-run evidence. All subsequent stress actions use GameApp.Send.
            var setter = typeof(GameApp).GetProperty(nameof(GameApp.Session)).GetSetMethod(true);
            Require(setter != null, "GameApp.Session private setter is unavailable.");
            setter.Invoke(app, new object[] { session });
            SetPrivate(app, "saves", store);
            GameApp.SaveDirectoryOverride = directory;
            SetPrivate(app, "inMenu", false); SetPrivate(app, "tutorial", false);
            app.Refresh();
            Require(IsWithin(app.SaveLocation, directory), "Stress save escaped its dedicated directory.");
            AssertStressCapacity();
        }
        private void AssertStressCapacity()
        {
            RunState s = app.Session.State;
            Require(s.isDebug && s.dice.Count == 10 && s.stage.capacity == 8 && s.hand.orderedLockedIds.Count == 8
                && s.controllers.Count == 10 && ControllerRules.OrdinaryUsed(s.controllers) == 4 && s.controllers.Count(c => c.enchantmentId == "slotless") == 6 && s.inventory.Count == 5, "Stress fixture no longer has 10 dice / 8 slots / 4 ordinary + 6 slotless controllers / 5 items.");
        }
        private static void Accept(CommandResult result)
        {
            Require(result != null && result.accepted && !result.duplicate && !result.pendingSave, "Stress command rejected: " + result?.error);
        }

        private void DiscoverMarkers()
        {
            var handles = new List<ProfilerRecorderHandle>();
            ProfilerRecorderHandle.GetAvailable(handles);
            var descriptions = handles.Where(h => h.Valid).Select(ProfilerRecorderHandle.GetDescription).ToList();
            File.WriteAllLines(Path.Combine(configuredDirectory, "profiler-markers.tsv"),
                descriptions.Select(d => d.Category.Name + "\t" + d.Name + "\t" + d.UnitType), Encoding.UTF8);
            mainThread = CreateMarker(descriptions, 1, "Main Thread");
            renderThread = CreateMarker(descriptions, 1, "Render Thread", "CPU Render Thread Frame Time");
            gpuCounter = CreateMarker(descriptions, 1, "GPU Frame Time");
            gcAllocated = CreateMarker(descriptions, 1, "GC Allocated In Frame");
            gcUsed = CreateMarker(descriptions, 1, "GC Used Memory");
            // Only an actual UI batch/rebuild marker counts. willRenderCanvases is not
            // called a rebuild: it may fire when no geometry changed.
            uiBuild = CreateMarker(descriptions, 128, "Canvas.BuildBatch", "UGUI.Rendering.UpdateBatches", "CanvasUpdateRegistry.PerformUpdate");
        }
        private Marker CreateMarker(List<ProfilerRecorderDescription> descriptions, int capacity, params string[] candidates)
        {
            var result = new Marker { name = string.Join(" | ", candidates), unavailableReason = "No matching marker is exposed by this player build." };
            foreach (string candidate in candidates)
            {
                int index = descriptions.FindIndex(d => d.Name == candidate);
                if (index < 0) continue;
                var description = descriptions[index];
                result.name = description.Name; result.unit = description.UnitType.ToString();
                result.nanoseconds = description.UnitType == ProfilerMarkerDataUnit.TimeNanoseconds;
                try
                {
                    result.recorder = ProfilerRecorder.StartNew(description.Category, description.Name, capacity);
                    result.unavailableReason = result.recorder.Valid ? null : "Marker is listed but recorder is invalid.";
                }
                catch (Exception ex) { result.unavailableReason = ex.Message; }
                break;
            }
            markers.Add(result);
            return result;
        }

        private void LateUpdate()
        {
            if (!finished && captureInProgress && Time.realtimeSinceStartupAsDouble > captureDeadline)
            {
                Fail(new TimeoutException("Screenshot rendering did not produce a valid frame within 12 seconds. Keep the actual player window visible and unminimized."));
                return;
            }
            if (!sampling || finished) return;
            try
            {
                double now = Time.realtimeSinceStartupAsDouble;
                double cpu = double.NaN, gpu = double.NaN, rawGpu = double.NaN;
                int gpuStatus = 0;
                bool receivedTiming = false;
                if (frameTimingEnabled)
                {
                    FrameTimingManager.CaptureFrameTimings();
                    if (FrameTimingManager.GetLatestTimings(1, frameTimings) > 0)
                    {
                        receivedTiming = true;
                        timingReadFrames++;
                        rawGpu = frameTimings[0].gpuFrameTime;
                        if (frameTimings[0].frameStartTimestamp != lastFrameTimestamp)
                        {
                            lastFrameTimestamp = frameTimings[0].frameStartTimestamp;
                            cpu = frameTimings[0].cpuFrameTime > 0 ? frameTimings[0].cpuFrameTime : double.NaN;
                            gpuStatus = ClassifyGpuDuration(rawGpu, now * 1000d);
                            if (gpuStatus == 1) gpu = rawGpu;
                            else if (gpuStatus == 3 || gpuStatus == 4) gpuInvalidSamples++;
                        }
                        else { gpuStatus = 5; gpuDuplicateTimestamps++; }
                    }
                }
                CpuClockObservation clocks = cpuClocks.Observe(receivedTiming,
                    receivedTiming ? frameTimings[0].frameStartTimestamp : 0,
                    receivedTiming ? frameTimings[0].cpuTimePresentCalled : 0,
                    receivedTiming ? frameTimings[0].cpuTimeFrameComplete : 0, cpuTimerFrequency, Time.frameCount);
                if (gpuStatus == 0 || gpuStatus == 2 || gpuStatus == 5) gpuMissingSamples++;
                if (Application.isFocused) focusedSampleFrames++;
                double uiMs = uiBuild.ReadNewUiFrame(out double uiCalls);
                if (frames.Count == frames.Capacity) frameBufferGrowthEvents++;
                frames.Add(new FrameSample
                {
                    frame = Time.frameCount, elapsed = now - samplingStart, frameMs = (now - lastTick) * 1000,
                    mainMs = mainThread.TimeValue(), renderMs = renderThread.TimeValue(), timingCpuMs = cpu, gpuMs = gpu,
                    gpuRawMs = rawGpu, gpuCounterMs = gpuCounter.TimeValue(), gpuSampleStatus = gpuStatus, uiSampleBatch = uiBuild.lastBatchSize,
                    gcAllocatedBytes = gcAllocated.Value(), gcUsedBytes = gcUsed.Value(),
                    uiBuildMs = uiMs, uiBuildCalls = uiCalls, monoUsedBytes = Profiler.GetMonoUsedSizeLong(),
                    frameStartTimestamp = clocks.frameStart, presentCalledTimestamp = clocks.present, frameCompleteTimestamp = clocks.complete,
                    frameStartDeltaMs = clocks.startDelta, presentCalledDeltaMs = clocks.presentDelta, frameCompleteDeltaMs = clocks.completeDelta,
                    timingObservationStatus = clocks.observationStatus, frameStartDeltaStatus = clocks.startStatus,
                    presentDeltaStatus = clocks.presentStatus, completeDeltaStatus = clocks.completeStatus
                });
                lastTick = now;
            }
            catch (Exception ex) { Fail(ex); }
        }

        // This rejects physically impossible timer results, not slow frames. A single
        // GPU frame cannot predate this process's entire elapsed lifetime. Original API
        // values and status remain in CSV, and any invalid value forces an evidence gap.
        private static int ClassifyGpuDuration(double milliseconds, double processElapsedMilliseconds)
        {
            if (double.IsNaN(milliseconds) || double.IsInfinity(milliseconds) || milliseconds < 0) return 3;
            if (milliseconds == 0) return 2;
            return milliseconds > processElapsedMilliseconds ? 4 : 1;
        }

        private static int RequiredFrameCapacity(double observedFramesPerSecond)
        {
            Require(!double.IsNaN(observedFramesPerSecond) && !double.IsInfinity(observedFramesPerSecond) && observedFramesPerSecond >= 0,
                "Warmup frame rate is invalid.");
            return Math.Max(60000, checked((int)Math.Ceiling(observedFramesPerSecond * SampleSeconds * 2d)));
        }

        private Dictionary<string, object> BuildPerformanceReport()
        {
            // Exclude the first partial interval when sampling was enabled mid-frame.
            FrameSample[] validFrames = frames.Skip(1).ToArray();
            var frame = Metric(validFrames.Select(x => x.frameMs), "ms", "monotonic LateUpdate-to-LateUpdate interval");
            var cpu = Metric(validFrames.Select(x => x.mainMs), "ms", mainThread.name, mainThread.unavailableReason);
            var timingCpu = Metric(validFrames.Select(x => x.timingCpuMs), "ms", "FrameTimingManager.cpuFrameTime", "Frame timing unavailable or returned no positive timings.");
            var gpu = Metric(validFrames.Select(x => x.gpuMs), "ms", "FrameTimingManager.gpuFrameTime", "Frame timing unavailable or GPU timings unsupported by this build/driver.");
            var allocation = Metric(validFrames.Select(x => x.gcAllocatedBytes), "bytes/frame", gcAllocated.name, gcAllocated.unavailableReason);
            var used = Metric(validFrames.Select(x => x.gcUsedBytes), "bytes", gcUsed.name, gcUsed.unavailableReason);
            var ui = Metric(validFrames.Select(x => x.uiBuildMs), "ms/frame", uiBuild.name, uiBuild.unavailableReason, true);
            var uiCalls = Metric(validFrames.Select(x => x.uiBuildCalls), "marker invocations/frame", uiBuild.name, uiBuild.unavailableReason, true);
            bool frameBudgetMet = frame["p95"] != null && (double)frame["p95"] <= FrameBudgetMs;
            bool gaps = !CompleteCoverage(cpu) || !CompleteCoverage(gpu) || !CompleteCoverage(allocation) || !CompleteCoverage(ui)
                || uiBuild.unavailableReason != null || gpuInvalidSamples > 0 || timingWarningsDuringSampling > 0 || frameBufferGrowthEvents > 0
                || Screen.width != 1920 || Screen.height != 1080;
            string status = !frameBudgetMet ? "budget_exceeded" : gaps ? "measured_with_gaps" : "within_frame_budget";
            return new Dictionary<string, object>
            {
                ["status"] = status, ["targetFrameMs"] = FrameBudgetMs, ["targetFps"] = 60,
                ["targetResolution"] = "1920x1080", ["targetResolutionMet"] = Screen.width == 1920 && Screen.height == 1080,
                ["isDebugFixture"] = true, ["dice"] = 10, ["submitSlots"] = 8, ["controllers"] = 10, ["inventory"] = 5,
                ["warmupSeconds"] = WarmupSeconds, ["sampleSeconds"] = samplingEnd - samplingStart,
                ["frameCount"] = validFrames.Length, ["frameBudgetMet"] = frameBudgetMet,
                ["focusedSampleFrames"] = focusedSampleFrames, ["frameBufferInitialCapacity"] = samplingFrameCapacity,
                ["warmupObservedFps"] = warmupObservedFps, ["frameBufferMinimumCapacity"] = 60000, ["frameBufferCapacitySafetyFactor"] = 2,
                ["frameSampleRecordBytes"] = frameSampleRecordBytes, ["frameBufferEstimatedObserverPayloadBytes"] = observerFramePayloadBytes,
                ["frameBufferFinalCapacity"] = frames.Capacity, ["frameBufferGrowthEvents"] = frameBufferGrowthEvents,
                ["requiredMetricCoverage"] = RequiredCoverage,
                ["coverageCriterionSource"] = "Conservative probe evidence-completeness criterion; it does not replace or relax the P0 frame budget.",
                ["operationsIncludingWarmup"] = new Dictionary<string, object> { ["roll"] = stressRolls, ["reorder"] = stressReorders, ["pendingSelectionAndClear"] = stressSelections },
                ["operationSchedule"] = "Each second: Roll at 0.00, SkipVisual at 0.25, rotate locked order at 0.50, TogglePending+Refresh at 0.70, ClearPending+Refresh at 0.85. Real autosave and UI preview remain active.",
                ["frameTimingEnabled"] = frameTimingEnabled, ["frameInterval"] = frame,
                ["gpuTimerFrequency"] = FrameTimingManager.GetGpuTimerFrequency(), ["cpuTimerFrequency"] = FrameTimingManager.GetCpuTimerFrequency(),
                ["frameTimingReadFrames"] = timingReadFrames, ["gpuInvalidSamples"] = gpuInvalidSamples,
                ["gpuMissingSamples"] = gpuMissingSamples, ["gpuDuplicateTimestamps"] = gpuDuplicateTimestamps,
                ["graphicsTimingWarningsDuringSampling"] = timingWarningsDuringSampling,
                ["gpuSampleStatusLegend"] = new Dictionary<string, object> { ["0"] = "no timing returned", ["1"] = "valid",
                    ["2"] = "zero/unavailable", ["3"] = "invalid negative or non-finite", ["4"] = "invalid: exceeds process lifetime", ["5"] = "duplicate timing timestamp" },
                ["mainThread"] = cpu, ["renderThread"] = Metric(validFrames.Select(x => x.renderMs), "ms", renderThread.name, renderThread.unavailableReason),
                ["frameTimingCpu"] = timingCpu, ["gpu"] = gpu, ["gcAllocated"] = allocation, ["gcUsed"] = used,
                ["independentFrameClocks"] = BuildFrameClockReport(validFrames), ["productionFramePacing"] = productionPacing,
                ["productionFramePacingAfterSampling"] = ReadProductionPacing(),
                ["gpuRawApiValues"] = Metric(validFrames.Select(x => x.gpuRawMs), "ms (unvalidated)", "Raw FrameTimingManager.gpuFrameTime; diagnostic only, may contain invalid values"),
                ["gpuProfilerCounter"] = Metric(validFrames.Select(x => x.gpuCounterMs), "ms (diagnostic)", gpuCounter.name, gpuCounter.unavailableReason),
                ["monoUsed"] = Metric(validFrames.Select(x => x.monoUsedBytes), "bytes", "Profiler.GetMonoUsedSizeLong"),
                ["uiRebuild"] = ui, ["uiRebuildMarkerCalls"] = uiCalls,
                ["gcCollections"] = new Dictionary<string, object> { ["generation0"] = collected0,
                    ["generation1"] = collected1, ["generation2"] = collected2 },
                ["limitations"] = new[] {
                    "This is automated player operation, not human playtesting, player win rate, or a mobile performance result.",
                    "The frame budget check uses P95 of measured frame intervals; CPU Main Thread may include presentation waits. Raw CPU/GPU metrics remain separately available for diagnosis.",
                    "Frame-start, Present-called and frame-complete timestamp differences are diagnostic peers of LateUpdate, never replacements for its original verdict. Present-called is not scanout; frame-complete is GPU completion interrupting the CPU. Adjacent unique GetLatestTimings(1) observations may still skip native records; explicit gaps are broken but native adjacency is not guaranteed.",
                    "Recorder units and exact marker names are retained. UI marker invocation count is not the number of individual rebuilt elements.",
                    "Before sampling, the probe allocates max(60000, ceil(warmup FPS * 180 seconds * 2)) frame records once. Estimated observer bytes cover record payload, not object headers or all profiler overhead. Any expansion during sampling still forces a gap.",
                    "GPU durations that are negative, non-finite or longer than the process lifetime are invalid, retained in raw CSV and force a measurement gap; no merely slow frame is filtered by the 16.7ms budget.",
                    "Focus is recorded as context, not proof of window visibility. Keep the actual player visible and unminimized; inspect the native player log for swapchain/timestamp warnings that may bypass Unity log callbacks.",
                    "Unavailable metrics are null; availability gaps or a non-1080p run cannot certify the complete performance condition. Independent review remains required."
                }
            };
        }
        private Dictionary<string, object> BuildFrameClockReport(FrameSample[] values)
        {
            const string missing = "Timestamp absent, duplicate, non-increasing, no CPU frequency, or no adjacent valid observation. See raw values and status counts.";
            return new Dictionary<string, object>
            {
                ["diagnosticOnly"] = true, ["cpuTimerFrequencyHz"] = cpuTimerFrequency,
                ["rawRowsIncludingInitialPartialInterval"] = frames.Count,
                ["frameStart"] = Metric(values.Select(x => x.frameStartDeltaMs), "ms", "FrameTiming.frameStartTimestamp difference", missing),
                ["presentCalled"] = Metric(values.Select(x => x.presentCalledDeltaMs), "ms", "FrameTiming.cpuTimePresentCalled difference", missing),
                ["frameComplete"] = Metric(values.Select(x => x.frameCompleteDeltaMs), "ms", "FrameTiming.cpuTimeFrameComplete difference", missing),
                ["observationStatusCounts"] = StatusCounts(values.Select(x => x.timingObservationStatus)),
                ["frameStartDeltaStatusCounts"] = StatusCounts(values.Select(x => x.frameStartDeltaStatus)),
                ["presentDeltaStatusCounts"] = StatusCounts(values.Select(x => x.presentDeltaStatus)),
                ["completeDeltaStatusCounts"] = StatusCounts(values.Select(x => x.completeDeltaStatus)),
                ["zeroFrameStartRows"] = values.Count(x => x.frameStartTimestamp == 0),
                ["zeroPresentRows"] = values.Count(x => x.presentCalledTimestamp == 0),
                ["zeroCompleteRows"] = values.Count(x => x.frameCompleteTimestamp == 0),
                ["observationStatusLegend"] = "0 no timing; 1 zero frame-start; 2 repeated frame-start; 3 backward frame-start; 4 new frame-start; 5 new frame-start after skipped Unity observation (delta baseline reset).",
                ["deltaStatusLegend"] = "0 unavailable observation or zero axis; 1 valid current timestamp without adjacent baseline; 2 valid positive difference; 3 non-increasing axis; 4 CPU frequency unavailable.",
                ["definitions"] = "Unity 6000.6: frameStartTimestamp is CPU clock when frame started; cpuTimePresentCalled is CPU clock at Present call; cpuTimeFrameComplete is CPU clock when GPU finished rendering and interrupted CPU. Units are converted using GetCpuTimerFrequency.",
                ["adjacencyLimit"] = "Differences require adjacent Unity observations with new monotonic frame-start values and a valid monotonic axis. GetLatestTimings(1) does not prove no native records were skipped. No clock replaces the original LateUpdate interval or acceptance criteria."
            };
        }
        private static Dictionary<string, object> StatusCounts(IEnumerable<int> statuses)
        {
            return statuses.GroupBy(x => x).ToDictionary(x => x.Key.ToString(CultureInfo.InvariantCulture), x => (object)x.Count());
        }
        private static Dictionary<string, object> ReadProductionPacing()
        {
            // Read only, outside the measured window. Never installs, stops or configures a pacer.
            Type type = typeof(GameApp).Assembly.GetType("DiceCraft.FramePacer", false);
            var result = new Dictionary<string, object> {
                ["classPresent"] = type != null, ["installed"] = type == null ? (object)false : null,
                ["strategyDescription"] = null, ["metadataIssue"] = null,
                ["stopwatchFrequencyHz"] = System.Diagnostics.Stopwatch.Frequency,
                ["vSyncCount"] = QualitySettings.vSyncCount, ["applicationTargetFrameRate"] = Application.targetFrameRate,
                ["readTiming"] = "Outside sampling only; public static read-only metadata, never a per-frame reflection call." };
            if (type == null) return result;
            try
            {
                var installed = type.GetProperty("IsInstalled", BindingFlags.Public | BindingFlags.Static);
                if (installed != null && installed.PropertyType == typeof(bool)) result["installed"] = installed.GetValue(null);
                else result["metadataIssue"] = "FramePacer class exists but IsInstalled metadata is unavailable.";
                var strategyProperty = type.GetProperty("StrategyDescription", BindingFlags.Public | BindingFlags.Static);
                var strategyField = type.GetField("StrategyDescription", BindingFlags.Public | BindingFlags.Static);
                result["strategyDescription"] = strategyProperty != null ? strategyProperty.GetValue(null) : strategyField?.GetValue(null);
                if (result["strategyDescription"] == null) result["metadataIssue"] = "StrategyDescription metadata is unavailable; class presence alone does not establish the active pacing policy.";
            }
            catch (Exception e) { result["metadataIssue"] = e.GetType().Name + ": " + e.Message; }
            return result;
        }
        private static bool CompleteCoverage(Dictionary<string, object> metric)
        {
            return (bool)metric["available"] && (double)metric["validFrameCoverage"] >= RequiredCoverage;
        }
        private static Dictionary<string, object> Metric(IEnumerable<double> source, string unit, string method, string unavailable = null, bool preserveIssue = false)
        {
            double[] observations = source.ToArray();
            double[] values = observations.Where(x => !double.IsNaN(x) && !double.IsInfinity(x)).OrderBy(x => x).ToArray();
            return new Dictionary<string, object>
            {
                ["available"] = values.Length > 0, ["unit"] = unit, ["method"] = method,
                ["unavailableReason"] = values.Length == 0 ? unavailable ?? "No samples returned." : preserveIssue ? unavailable : null,
                ["samples"] = values.Length, ["expectedFrames"] = observations.Length,
                ["validFrameCoverage"] = observations.Length == 0 ? 0d : (double)values.Length / observations.Length,
                ["mean"] = values.Length == 0 ? (object)null : values.Average(),
                ["p50"] = values.Length == 0 ? (object)null : values[(int)Math.Ceiling(values.Length * .5) - 1],
                ["p95"] = values.Length == 0 ? (object)null : values[(int)Math.Ceiling(values.Length * .95) - 1],
                ["max"] = values.Length == 0 ? (object)null : values[values.Length - 1]
            };
        }
        private void WriteFrames()
        {
            using (var writer = new StreamWriter(Path.Combine(configuredDirectory, "performance-frames.csv"), false, new UTF8Encoding(false)))
            {
                writer.WriteLine("frame,elapsed_s,interval_ms,main_thread_ms,render_thread_ms,frame_timing_cpu_ms,gpu_ms,gc_alloc_bytes,gc_used_bytes,ui_build_ms,ui_build_marker_calls,mono_used_bytes,gpu_raw_ms,gpu_sample_status,gpu_counter_ms,ui_sample_batch,frame_start_timestamp,present_called_timestamp,frame_complete_timestamp,frame_start_delta_ms,present_called_delta_ms,frame_complete_delta_ms,timing_observation_status,frame_start_delta_status,present_delta_status,complete_delta_status,cpu_timer_frequency_hz");
                foreach (var f in frames)
                    writer.WriteLine(string.Join(",", f.frame.ToString(CultureInfo.InvariantCulture), Number(f.elapsed), Number(f.frameMs), Number(f.mainMs), Number(f.renderMs), Number(f.timingCpuMs), Number(f.gpuMs), Number(f.gcAllocatedBytes), Number(f.gcUsedBytes), Number(f.uiBuildMs), Number(f.uiBuildCalls), Number(f.monoUsedBytes), f.gpuRawMs.ToString("R", CultureInfo.InvariantCulture), f.gpuSampleStatus.ToString(CultureInfo.InvariantCulture), Number(f.gpuCounterMs), f.uiSampleBatch.ToString(CultureInfo.InvariantCulture),
                        f.frameStartTimestamp.ToString(CultureInfo.InvariantCulture), f.presentCalledTimestamp.ToString(CultureInfo.InvariantCulture), f.frameCompleteTimestamp.ToString(CultureInfo.InvariantCulture),
                        Number(f.frameStartDeltaMs), Number(f.presentCalledDeltaMs), Number(f.frameCompleteDeltaMs),
                        f.timingObservationStatus.ToString(CultureInfo.InvariantCulture), f.frameStartDeltaStatus.ToString(CultureInfo.InvariantCulture), f.presentDeltaStatus.ToString(CultureInfo.InvariantCulture), f.completeDeltaStatus.ToString(CultureInfo.InvariantCulture), cpuTimerFrequency.ToString(CultureInfo.InvariantCulture)));
            }
        }

        private IEnumerator VerifyFirstAnimatedRoll()
        {
            Require(app.IsBusy && app.table.IsRolling, "First normal Roll did not start an actual table animation.");
            string savedState = JsonUtility.ToJson(app.Session.State);
            var ids = app.Session.State.hand.currentResults.Where(r => !r.isLocked).Select(r => r.dieId).ToArray();
            Require(ids.Length > 0, "No animated dice exist for the first Roll.");
            var pivots = new Transform[ids.Length];
            var firstPosition = new Vector3[ids.Length];
            var firstRotation = new Quaternion[ids.Length];
            var poseRecords = new List<Dictionary<string, object>>();
            var motionCheck = new Dictionary<string, object> { ["check"] = "first-normal-roll-motion", ["passed"] = false,
                ["importedClipCount"] = app.table.ImportedRollClipCount, ["dice"] = poseRecords };
            animationChecks.Add(motionCheck);
            double started = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < ids.Length; i++)
            {
                Transform face = app.table.GetFaceOrientation(ids[i]);
                Require(face != null && face.parent != null && face.parent.name == "RollPivot", "Actual visible RollPivot is missing for " + ids[i]);
                pivots[i] = face.parent;
                poseRecords.Add(new Dictionary<string, object> { ["dieId"] = ids[i], ["start"] = Pose(pivots[i]) });
            }
            while (Time.realtimeSinceStartupAsDouble - started < .32) yield return null;
            double sampleA = Time.realtimeSinceStartupAsDouble - started;
            motionCheck["sampleASeconds"] = sampleA;
            Require(sampleA >= .30 && sampleA <= .50 && app.table.IsRolling, "Could not observe the first animated Roll within the required 0.3–0.5 second window.");
            for (int i = 0; i < ids.Length; i++)
            {
                firstPosition[i] = pivots[i].localPosition; firstRotation[i] = pivots[i].localRotation;
                poseRecords[i]["sampleA"] = Pose(pivots[i]);
            }
            while (Time.realtimeSinceStartupAsDouble - started < .45) yield return null;
            double sampleB = Time.realtimeSinceStartupAsDouble - started;
            motionCheck["sampleBSeconds"] = sampleB;
            Require(sampleB >= .30 && sampleB <= .50 && app.table.IsRolling, "Second motion sample missed the required 0.3–0.5 second window.");
            for (int i = 0; i < ids.Length; i++)
            {
                float movement = Vector3.Distance(firstPosition[i], pivots[i].localPosition);
                float angle = Quaternion.Angle(firstRotation[i], pivots[i].localRotation);
                poseRecords[i]["sampleB"] = Pose(pivots[i]); poseRecords[i]["positionDelta"] = movement; poseRecords[i]["rotationDeltaDegrees"] = angle;
                Require(movement > .0001f || angle > .05f, "The actual visible RollPivot did not change between motion samples: " + ids[i]);
            }
            Require(JsonUtility.ToJson(app.Session.State) == savedState, "Animation changed committed rules or RNG state.");
            motionCheck["passed"] = true;
        }

        private void VerifyLandedDice(string presentation)
        {
            Require(!app.IsBusy && !app.table.IsRolling, "Roll presentation did not finish.");
            var results = new List<Dictionary<string, object>>();
            var landingCheck = new Dictionary<string, object> { ["check"] = "landed", ["presentation"] = presentation,
                ["actionSequence"] = app.Session.State.actionSequence, ["passed"] = false, ["dice"] = results };
            animationChecks.Add(landingCheck);
            foreach (var die in app.Session.State.hand.currentResults.Where(r => r.hasRolled))
            {
                Transform face = app.table.GetFaceOrientation(die.dieId);
                Require(face != null && face.parent != null, "Visible landing transform is missing: " + die.dieId);
                Transform pivot = face.parent;
                float positionError = pivot.localPosition.magnitude;
                float rotationError = Quaternion.Angle(pivot.localRotation, Quaternion.identity);
                float scaleError = Vector3.Distance(pivot.localScale, Vector3.one);
                Vector3 normal = face.TransformDirection(DiceTable.Normals[DiceTable.FaceIndex(die.currentFaceId)]).normalized;
                float upDot = Vector3.Dot(normal, Vector3.up);
                results.Add(new Dictionary<string, object> { ["dieId"] = die.dieId, ["faceId"] = die.currentFaceId,
                    ["positionError"] = positionError, ["rotationErrorDegrees"] = rotationError, ["scaleError"] = scaleError, ["faceNormalUpDot"] = upDot });
                Require(positionError <= .0001f && rotationError <= .01f && scaleError <= .0001f,
                    "RollPivot did not reset to zero position / identity rotation / unit scale: " + die.dieId);
                Require(upDot >= .9999f, "The committed physical face is not facing world +Y: " + die.dieId + "/" + die.currentFaceId);
            }
            landingCheck["passed"] = true;
        }

        private static Dictionary<string, object> Pose(Transform transform)
        {
            Vector3 p = transform.localPosition, s = transform.localScale; Quaternion q = transform.localRotation;
            return new Dictionary<string, object> { ["position"] = new[] { p.x, p.y, p.z },
                ["rotationQuaternion"] = new[] { q.x, q.y, q.z, q.w }, ["scale"] = new[] { s.x, s.y, s.z } };
        }

        private IEnumerator SetActualResolution(int width, int height)
        {
            Require(!sampling, "Resolution changes are forbidden inside the performance sample window.");
            Screen.SetResolution(width, height, FullScreenMode.Windowed);
            double deadline = Time.realtimeSinceStartupAsDouble + 12;
            int stableFrames = 0;
            while (stableFrames < 3)
            {
                Require(Time.realtimeSinceStartupAsDouble < deadline,
                    "Window resolution did not become " + width + "x" + height + "; actual " + Screen.width + "x" + Screen.height + ". A scaled capture is not accepted.");
                yield return null;
                stableFrames = Screen.width == width && Screen.height == height && Screen.fullScreenMode == FullScreenMode.Windowed ? stableFrames + 1 : 0;
            }
        }

        private IEnumerator CaptureUnlockedDebugPool()
        {
            Require(!sampling && !app.IsBusy && app.Session.State.isDebug, "The all-unlocked visual inspection requires an isolated Debug session outside sampling.");
            GameSession stressSession = app.Session;
            string originalState = JsonUtility.ToJson(stressSession.State);
            string originalSave = Hash(File.ReadAllText(app.SaveLocation, Encoding.UTF8));
            RunState visualState = stressSession.State;
            visualState.runId += "-ALL-UNLOCKED-VISUAL";
            visualState.hand.orderedLockedIds.Clear();
            foreach (var die in visualState.hand.currentResults) die.isLocked = false;
            var memory = new MemorySaveStore();
            var visualSession = new GameSession(app.Config, memory, visualState);
            MethodInfo setter = typeof(GameApp).GetProperty(nameof(GameApp.Session)).GetSetMethod(true);
            Require(setter != null, "GameApp.Session private setter is unavailable for the Debug visual fixture.");
            try
            {
                setter.Invoke(app, new object[] { visualSession });
                app.Refresh();
                yield return CaptureResolutionSet("debug-ten-dice-unlocked-pool");
            }
            finally
            {
                setter.Invoke(app, new object[] { stressSession });
                app.Refresh();
            }
            Require(ReferenceEquals(app.Session, stressSession) && JsonUtility.ToJson(app.Session.State) == originalState,
                "The all-unlocked visual inspection changed the original stress session.");
            Require(Hash(File.ReadAllText(app.SaveLocation, Encoding.UTF8)) == originalSave && memory.Saved == null,
                "The all-unlocked visual inspection unexpectedly wrote a save.");
            AssertStressCapacity();
        }

        private IEnumerator CaptureResolutionSet(string name, Action showModal = null, bool scrollDetail = false, string scrollName = "ScoreDetail")
        {
            Require(!sampling && !app.IsBusy && !(bool)GetPrivate(app, "modal"), "Visual capture must begin outside sampling, without a busy presentation or open modal.");
            string before = JsonUtility.ToJson(app.Session.State);
            string saved = File.Exists(app.SaveLocation) ? Hash(File.ReadAllText(app.SaveLocation, Encoding.UTF8)) : null;
            string[] pending = app.Session.PendingIds;
            int[] widths = { 1920, 1280, 2560 }, heights = { 1080, 720, 1440 };
            for (int i = 0; i < widths.Length; i++)
            {
                yield return SetActualResolution(widths[i], heights[i]);
                app.Refresh();
                if (showModal != null)
                {
                    showModal();
                    Require((bool)GetPrivate(app, "modal"), "The real detail modal did not open.");
                }
                yield return Capture(name + "-" + widths[i] + "x" + heights[i]);
                if (scrollDetail)
                {
                    // Operate the existing ScrollRect through its public property, avoiding
                    // a separate fake detail renderer or any rules-state change.
                    var scroll = ((Transform)GetPrivate(app, "overlay")).GetComponentsInChildren<Component>(true).FirstOrDefault(c => c != null && c.GetType().FullName == "UnityEngine.UI.ScrollRect"
                        && c.gameObject.activeInHierarchy && c.name == scrollName);
                    Require(scroll != null, "The real modal scroll view is missing: " + scrollName);
                    scroll.GetType().GetProperty("verticalNormalizedPosition").SetValue(scroll, .5f);
                    yield return Capture(name + "-middle-" + widths[i] + "x" + heights[i]);
                    scroll.GetType().GetProperty("verticalNormalizedPosition").SetValue(scroll, 0f);
                    yield return Capture(name + "-bottom-" + widths[i] + "x" + heights[i]);
                }
                if (showModal != null)
                {
                    InvokePrivate("CloseModal");
                    Require(!(bool)GetPrivate(app, "modal"), "The detail modal did not close.");
                    yield return null;
                }
                CheckUnityErrors();
            }
            yield return SetActualResolution(1920, 1080);
            app.Refresh();
            yield return null;
            Require(JsonUtility.ToJson(app.Session.State) == before && app.Session.PendingIds.SequenceEqual(pending), "Visual inspection mutated the committed state or pending selection.");
            string afterSaved = File.Exists(app.SaveLocation) ? Hash(File.ReadAllText(app.SaveLocation, Encoding.UTF8)) : null;
            Require(saved == afterSaved, "Visual inspection modified the save file.");
            Require(!(bool)GetPrivate(app, "modal") && Screen.width == 1920 && Screen.height == 1080, "Visual inspection did not restore the normal 1080p view.");
        }

        private IEnumerator Capture(string name, int requiredConsecutiveValidFrames = 1)
        {
            Require(!sampling, "Screenshot encoding is forbidden inside the performance sample window.");
            captureInProgress = true;
            captureDeadline = Time.realtimeSinceStartupAsDouble + 12;
            int attempt = 0, consecutive = 0;
            try
            {
                while (Time.realtimeSinceStartupAsDouble < captureDeadline)
                {
                    attempt++;
                    yield return null;
                    yield return new WaitForEndOfFrame();
                    Texture2D texture = ScreenCapture.CaptureScreenshotAsTexture();
                    Require(texture != null, "Screenshot capture returned no texture.");
                    bool accepted;
                    try
                    {
                        CaptureContent content = AnalyzeCapturePixels(texture.GetPixels32(), texture.width, texture.height);
                        bool dimensionsMatch = texture.width == Screen.width && texture.height == Screen.height;
                        bool valid = dimensionsMatch && content.valid;
                        consecutive = valid ? consecutive + 1 : 0;
                        accepted = valid && consecutive >= requiredConsecutiveValidFrames;
                        string file = accepted ? name + ".png" : name + (valid ? "-warmup-" : "-rejected-") + attempt + ".png";
                        // Never overwrite a rejected frame with a later good one. Every
                        // attempted image is retained and its classification is explicit.
                        File.WriteAllBytes(Path.Combine(configuredDirectory, file), texture.EncodeToPNG());
                        if (accepted) screenshots.Add(file);
                        else if (!valid) rejectedScreenshots.Add(file);
                        RunState state = app?.Session?.State;
                        screenshotDetails.Add(new Dictionary<string, object> { ["file"] = file, ["width"] = texture.width, ["height"] = texture.height,
                            ["screenWidth"] = Screen.width, ["screenHeight"] = Screen.height, ["dimensionsMatch"] = dimensionsMatch,
                            ["accepted"] = accepted, ["contentValid"] = content.valid, ["content"] = content, ["attempt"] = attempt,
                            ["requiredConsecutiveValidFrames"] = requiredConsecutiveValidFrames, ["consecutiveValidFrames"] = consecutive,
                            ["classification"] = accepted ? "accepted" : valid ? "valid_warmup" : "rejected_blank_or_wrong_dimensions",
                            ["phase"] = state?.phase ?? "MainMenu", ["runId"] = state?.runId, ["actionSequence"] = state?.actionSequence,
                            ["isDebug"] = state?.isDebug ?? false, ["isFocused"] = Application.isFocused,
                            ["modalOpen"] = app != null && (bool)GetPrivate(app, "modal") });
                    }
                    finally { Destroy(texture); }
                    if (accepted) yield break;
                    yield return new WaitForSecondsRealtime(.10f);
                }
                throw new TimeoutException("No stable non-blank rendered content for " + name + ". Rejected images were retained; this capture did not pass.");
            }
            finally { captureInProgress = false; }
        }

        private static CaptureContent AnalyzeCapturePixels(Color32[] pixels, int width, int height)
        {
            Require(pixels != null && width > 0 && height > 0 && pixels.Length == width * height, "Invalid captured pixel buffer.");
            var result = new CaptureContent { minimumChannelMaximum = 255 };
            var colors = new bool[4096];
            for (int y = 0; y < height; y += 16)
                for (int x = 0; x < width; x += 16)
                {
                    Color32 p = pixels[y * width + x];
                    int maximum = Math.Max(p.r, Math.Max(p.g, p.b));
                    result.sampledPixels++;
                    if (maximum > 8) result.nonBlackPixels++;
                    if (maximum >= 64) result.brightPixels++;
                    result.minimumChannelMaximum = Math.Min(result.minimumChannelMaximum, maximum);
                    result.maximumChannelMaximum = Math.Max(result.maximumChannelMaximum, maximum);
                    int color = ((p.r >> 4) << 8) | ((p.g >> 4) << 4) | (p.b >> 4);
                    if (!colors[color]) { colors[color] = true; result.quantizedColors++; }
                }
            result.nonBlackRatio = (double)result.nonBlackPixels / result.sampledPixels;
            result.brightRatio = (double)result.brightPixels / result.sampledPixels;
            // These are blank-frame rejection heuristics, not an art/readability score.
            // The known prototype screens have >4% bright samples and many UI colors.
            result.valid = result.nonBlackRatio >= .10 && result.brightRatio >= .005 && result.quantizedColors >= 8;
            return result;
        }

        private Dictionary<string, object> EnvironmentReport()
        {
            return new Dictionary<string, object>
            {
                ["utc"] = DateTime.UtcNow.ToString("O"), ["unityVersion"] = Application.unityVersion,
                ["buildGuid"] = Application.buildGUID, ["developmentBuild"] = Debug.isDebugBuild, ["isEditor"] = Application.isEditor,
                ["platform"] = Application.platform.ToString(), ["os"] = SystemInfo.operatingSystem,
                ["processArchitecture"] = RuntimeInformation.ProcessArchitecture.ToString(),
                ["cpu"] = SystemInfo.processorType, ["cpuThreads"] = SystemInfo.processorCount, ["systemMemoryMB"] = SystemInfo.systemMemorySize,
                ["gpu"] = SystemInfo.graphicsDeviceName, ["gpuMemoryMB"] = SystemInfo.graphicsMemorySize,
                ["graphicsVersion"] = SystemInfo.graphicsDeviceVersion, ["graphicsApi"] = SystemInfo.graphicsDeviceType.ToString(),
                ["width"] = Screen.width, ["height"] = Screen.height, ["fullscreenMode"] = Screen.fullScreenMode.ToString(),
                ["quality"] = QualitySettings.names[QualitySettings.GetQualityLevel()], ["vSyncCount"] = QualitySettings.vSyncCount,
                ["targetFrameRate"] = Application.targetFrameRate, ["presentationFast"] = false, ["presentationReducedMotion"] = false,
                ["probeArguments"] = new[] { "--verify-p0", configuredTrace, "--evidence-dir", configuredDirectory }
            };
        }
        private void WriteReport(string status, string error, int exitCode)
        {
            var report = new Dictionary<string, object>
            {
                ["schema"] = "p0-player-verification-5", ["probeRevision"] = ProbeRevision,
                ["probeAssemblyMvid"] = typeof(PlayerVerification).Module.ModuleVersionId.ToString(),
                ["status"] = status, ["phase"] = phase, ["exitCode"] = exitCode,
                ["updatedUtc"] = DateTime.UtcNow.ToString("O"), ["environment"] = environment,
                ["traceSha256"] = traceHash, ["configSha256"] = configHash, ["configVersion"] = trace?.configVersion,
                ["buildIdentity"] = Resources.Load<TextAsset>("BuildIdentity")?.text,
                ["checkpointExported"] = checkpointExported,["importedCheckpointStatus"] = importedCheckpointStatus,
                ["evidenceDirectory"] = configuredDirectory, ["normalSave"] = normalSave, ["normalSaveSha256BeforeStress"] = normalSaveHash, ["debugStressSave"] = stressSave,
                ["normalReplay"] = new Dictionary<string, object> { ["passed"] = normalPassed, ["seed"] = trace?.seed,
                    ["expectedSteps"] = trace?.steps?.Length, ["completedSteps"] = actualSteps.Count, ["currentStepIndex"] = currentStep,
                    ["normalIsDebug"] = false, ["terminalPhase"] = normalFinalPhase, ["terminalStage"] = normalFinalStage, ["completedAll24"] = normalPassed && normalFinalPhase == "RunWon" && normalFinalStage == 24, ["rollAnimations"] = rollAnimations, ["animationSkipsTotal"] = skippedAnimations,
                    ["reducedMotionChecks"] = reducedMotionChecks,
                    ["validation"] = "Each accepted command's sequence, phase, stage, score, gold, roll/submission budgets match trace; disk load and GameApp.ViewState equal committed state." },
                ["handbookPureReadChecks"] = handbookPureReadChecks, ["debug24"] = new Dictionary<string, object> { ["passed"] = debug24Passed, ["isDebug"] = true, ["steps"] = debug24Steps, ["purpose"] = "24-stage/risk/shop/finalization integration only; not normal progression or difficulty evidence" },
                ["stressCompleted"] = stressCompleted, ["performance"] = performance, ["screenshots"] = screenshots,
                ["rejectedScreenshots"] = rejectedScreenshots, ["graphicsTimingWarnings"] = graphicsTimingWarnings,
                ["screenshotDetails"] = screenshotDetails, ["animationChecks"] = animationChecks,
                ["unityErrors"] = unityErrors, ["failure"] = error
            };
            Directory.CreateDirectory(configuredDirectory);
            File.WriteAllText(Path.Combine(configuredDirectory, "player-report.json"), Json(report), new UTF8Encoding(false));
        }
        private void Fail(Exception error)
        {
            if (finished) return;
            finished = true; sampling = false;
            try
            {
                if (frames.Count > 0) WriteFrames();
                WriteReport("failed", error.ToString(), 1);
            }
            catch (Exception reportError) { Debug.LogError("P0 failure report could not be saved: " + reportError); }
            foreach (var marker in markers) marker.Dispose();
            Debug.LogError("P0 verification FAILED at " + phase + " step " + currentStep + ": " + error);
            Application.Quit(1);
        }
        private void CheckUnityErrors() { Require(unityErrors.Count == 0, "Unity emitted an error: " + (unityErrors.Count == 0 ? "" : unityErrors[0])); }
        private void OnDestroy()
        {
            Application.logMessageReceived -= OnUnityLog;
            foreach (var marker in markers) marker.Dispose();
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static void SetPrivate(object target, string field, object value)
        {
            FieldInfo info = target.GetType().GetField(field, PrivateInstance);
            Require(info != null, "Verification adapter field not found: " + field);
            info.SetValue(target, value);
        }
        private static object GetPrivate(object target, string field)
        {
            FieldInfo info = target.GetType().GetField(field, PrivateInstance);
            Require(info != null, "Verification adapter field not found: " + field);
            return info.GetValue(target);
        }
        private void InvokePrivate(string method, params object[] arguments)
        {
            MethodInfo info = typeof(GameApp).GetMethod(method, PrivateInstance|BindingFlags.Public);
            Require(info != null, "Verification adapter UI method not found: " + method);
            info.Invoke(app, arguments);
        }
        private static bool IsWithin(string file, string directory)
        {
            return Path.GetFullPath(file).StartsWith(Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase);
        }
        private static string Hash(string content)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(content))).Replace("-", "").ToLowerInvariant();
        }
        private static string FileHash(string path)
        {
            using(var sha=SHA256.Create())using(var stream=File.OpenRead(path))return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        }
        private static string Number(double value) { return double.IsNaN(value) || double.IsInfinity(value) ? "" : value.ToString("R", CultureInfo.InvariantCulture); }
        // Small report-only serializer allows true JSON null for unsupported metrics.
        // Unity JsonUtility is still used for all production DTO serialization.
        private static string Json(object value)
        {
            if (value == null) return "null";
            if (value is string text)
            {
                var b = new StringBuilder("\"");
                foreach (char c in text)
                {
                    if (c == '\\' || c == '"') b.Append('\\').Append(c);
                    else if (c < 32) b.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    else b.Append(c);
                }
                return b.Append('"').ToString();
            }
            if (value is bool boolean) return boolean ? "true" : "false";
            if (value is IDictionary map)
            {
                var values = new List<string>();
                foreach (DictionaryEntry pair in map) values.Add(Json(pair.Key.ToString()) + ":" + Json(pair.Value));
                return "{" + string.Join(",", values) + "}";
            }
            if (value is IEnumerable items)
            {
                var values = new List<string>(); foreach (object item in items) values.Add(Json(item));
                return "[" + string.Join(",", values) + "]";
            }
            if (value is IFormattable formatted) return formatted.ToString(null, CultureInfo.InvariantCulture);
            return JsonUtility.ToJson(value);
        }
    }
}

