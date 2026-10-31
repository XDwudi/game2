using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DiceCraft.Verification;
using UnityEngine;
using DiceCraft.Core;

internal static class MetricChecks
{
    private static int Main()
    {
        // Only invoke managed report helpers. No GameObject, scene, recorder, GameApp
        // or Unity native API is constructed or executed in this host.
        var type = typeof(PlayerVerification);
        var metric = type.GetMethod("Metric", BindingFlags.NonPublic | BindingFlags.Static);
        var complete = type.GetMethod("CompleteCoverage", BindingFlags.NonPublic | BindingFlags.Static);
        var json = type.GetMethod("Json", BindingFlags.NonPublic | BindingFlags.Static);
        Dictionary<string, object> Read(double[] samples, string issue = null, bool preserve = false) =>
            (Dictionary<string, object>)metric.Invoke(null, new object[] { samples, "ms", "managed-test", issue, preserve });
        bool Complete(Dictionary<string, object> value) => (bool)complete.Invoke(null, new object[] { value });
        int checks = 0;
        void Check(bool value, string name) { if (!value) throw new Exception(name); checks++; Console.WriteLine("PASS " + name); }
        var full = Read(Enumerable.Range(1, 100).Select(x => (double)x).ToArray());
        Check(Complete(full) && (double)full["p95"] == 95 && (double)full["validFrameCoverage"] == 1, "full_coverage_nearest_rank_p95");
        var missing = Read(Enumerable.Repeat(double.NaN, 100).ToArray());
        Check(!Complete(missing) && missing["p95"] == null && !(bool)missing["available"], "missing_is_null_not_zero");
        var sparse = Read(new[] { 1d }.Concat(Enumerable.Repeat(double.NaN, 99)).ToArray());
        Check((bool)sparse["available"] && !Complete(sparse), "one_sample_cannot_certify_continuous_capture");
        var boundary = Read(Enumerable.Repeat(1d, 95).Concat(Enumerable.Repeat(double.NaN, 5)).ToArray());
        Check(Complete(boundary), "ninety_five_percent_boundary");
        var below = Read(Enumerable.Repeat(1d, 94).Concat(Enumerable.Repeat(double.NaN, 6)).ToArray());
        Check(!Complete(below), "ninety_four_percent_incomplete");
        var interrupted = Read(new[] { 1d, 2d, double.NaN }, "recorder interrupted", true);
        Check((string)interrupted["unavailableReason"] == "recorder interrupted" && !Complete(interrupted), "earlier_samples_do_not_hide_interruption");
        var payload = new Dictionary<string, object> { ["metric"] = missing, ["text"] = "quote\" slash\\ newline\n中文" };
        using var document = JsonDocument.Parse((string)json.Invoke(null, new object[] { payload }));
        Check(document.RootElement.GetProperty("metric").GetProperty("p95").ValueKind == JsonValueKind.Null
            && document.RootElement.GetProperty("text").GetString() == (string)payload["text"], "report_json_preserves_null_and_escaping");
        var classifyGpu = type.GetMethod("ClassifyGpuDuration", BindingFlags.NonPublic | BindingFlags.Static);
        int Gpu(double value) => (int)classifyGpu.Invoke(null, new object[] { value, 230000d });
        Check(Gpu(.226304) == 1, "ordinary_gpu_duration_valid");
        Check(Gpu(100) == 1, "slow_gpu_frame_is_not_filtered_by_budget");
        Check(Gpu(13163700653408.648) == 4, "observed_impossible_gpu_duration_rejected");
        Check(Gpu(0) == 2, "zero_gpu_duration_is_missing");
        Check(Gpu(-1) == 3 && Gpu(double.NaN) == 3 && Gpu(double.PositiveInfinity) == 3, "invalid_gpu_numeric_values_rejected");
        var analyze = type.GetMethod("AnalyzeCapturePixels", BindingFlags.NonPublic | BindingFlags.Static);
        bool ImageValid(Color32[] pixels)
        {
            var value = analyze.Invoke(null, new object[] { pixels, 128, 128 });
            return (bool)value.GetType().GetField("valid").GetValue(value);
        }
        Check(!ImageValid(new Color32[128 * 128]), "all_black_capture_rejected");
        var flat = Enumerable.Repeat(new Color32(17, 29, 34, 255), 128 * 128).ToArray();
        Check(!ImageValid(flat), "blank_dark_clear_color_rejected");
        var watermark = new Color32[128 * 128]; watermark[0] = new Color32(255, 255, 255, 255);
        Check(!ImageValid(watermark), "isolated_watermark_does_not_validate_black_frame");
        var content = new Color32[128 * 128];
        for (int y = 0; y < 128; y++) for (int x = 0; x < 128; x++) content[y * 128 + x] = new Color32((byte)(40 + x), (byte)(70 + y), 100, 255);
        Check(ImageValid(content), "varied_visible_content_accepted");
        var frameCapacity = type.GetMethod("RequiredFrameCapacity", BindingFlags.NonPublic | BindingFlags.Static);
        Check((int)frameCapacity.Invoke(null, new object[] { 60d }) == 60000, "frame_preallocation_keeps_minimum_at_60fps");
        Check((int)frameCapacity.Invoke(null, new object[] { 1600d }) == 576000, "frame_preallocation_has_double_observed_180s_capacity");
        var chainType = type.GetNestedType("CpuClockChain", BindingFlags.NonPublic);
        var observe = chainType.GetMethod("Observe");
        object chain = Activator.CreateInstance(chainType);
        object Observe(bool received, ulong start, ulong present, ulong end, int frame, ulong frequency = 10000000) =>
            observe.Invoke(chain, new object[] { received, start, present, end, frequency, frame });
        int Status(object row, string name) => (int)row.GetType().GetField(name).GetValue(row);
        double Delta(object row, string name) => (double)row.GetType().GetField(name).GetValue(row);
        var first = Observe(true, 1000000, 1000100, 1000200, 1);
        Check(Status(first,"observationStatus")==4 && Status(first,"startStatus")==1 && double.IsNaN(Delta(first,"startDelta")), "clock_first_valid_observation_keeps_raw_without_inventing_interval");
        var next = Observe(true, 1166667, 1166767, 1166867, 2);
        Check(Status(next,"startStatus")==2 && Math.Abs(Delta(next,"startDelta")-16.6667)<1e-8
            && Math.Abs(Delta(next,"presentDelta")-16.6667)<1e-8 && Math.Abs(Delta(next,"completeDelta")-16.6667)<1e-8, "clock_three_axes_convert_cpu_ticks_independently");
        var duplicate = Observe(true, 1166667, 1166767, 1166867, 3);
        Check(Status(duplicate,"observationStatus")==2 && double.IsNaN(Delta(duplicate,"presentDelta")), "clock_duplicate_breaks_interval_chain");
        var afterDuplicate = Observe(true, 1333334, 1333434, 1333534, 4);
        Check(Status(afterDuplicate,"startStatus")==1 && double.IsNaN(Delta(afterDuplicate,"startDelta")), "clock_after_duplicate_does_not_bridge_gap");
        Observe(false,0,0,0,5);
        var repeatedAfterGap=Observe(true,1333334,1333434,1333534,6);
        Check(Status(repeatedAfterGap,"observationStatus")==2, "clock_seen_timestamp_survives_missing_observation");
        var nonAdjacent=Observe(true,1500001,1500101,1500201,9);
        Check(Status(nonAdjacent,"observationStatus")==5 && Status(nonAdjacent,"startStatus")==1, "clock_skipped_unity_observation_resets_baseline");
        var backward=Observe(true,1490000,1490100,1490200,10);
        Check(Status(backward,"observationStatus")==3 && double.IsNaN(Delta(backward,"completeDelta")), "clock_backward_start_is_gap_not_negative_duration");
        chain=Activator.CreateInstance(chainType);
        Observe(true,1000,1100,1200,1);
        var backwardsAxis=Observe(true,2000,1050,2200,2);
        Check(Status(backwardsAxis,"presentStatus")==3 && double.IsNaN(Delta(backwardsAxis,"presentDelta")) && Status(backwardsAxis,"completeStatus")==2,
            "clock_bad_present_axis_does_not_erase_valid_complete_axis");
        var axisRecovery=Observe(true,3000,3100,3200,3);
        Check(Status(axisRecovery,"presentStatus")==1 && double.IsNaN(Delta(axisRecovery,"presentDelta")), "clock_nonmonotonic_axis_needs_new_baseline");
        chain=Activator.CreateInstance(chainType);
        var noFrequency=Observe(true,1000,1100,1200,1,0);
        Check(Status(noFrequency,"startStatus")==4 && double.IsNaN(Delta(noFrequency,"startDelta")), "clock_missing_frequency_is_null_not_zero");
        var member = type.GetMethod("JsonMember", BindingFlags.NonPublic | BindingFlags.Static);
        var elements = type.GetMethod("JsonElements", BindingFlags.NonPublic | BindingFlags.Static);
        string Member(string source, string key) => (string)member.Invoke(null, new object[] { source, key });
        List<string> Elements(string source) => (List<string>)elements.Invoke(null, new object[] { source });
        Check(Member("{\"x\":{\"a\":1},\"a\":null}", "a") == "null", "json_null_slicer_ignores_nested_members");
        Check(Member("{\"a\":[],\"b\":null}", "a") == "[]", "json_empty_array_not_treated_as_null");
        Check(Member("{\"x\":\"quote\\\" and } ]\",\"a\":true}", "a") == "true", "json_slicer_skips_escaped_delimiters");
        Check(Elements(" [ {\"a\":[1,2]}, null, \"x,]y\" ] ").Count == 3, "json_elements_preserve_nested_values");
        bool absentRejected = false; try { Member("{\"x\":1}", "a"); } catch (TargetInvocationException e) { absentRejected = e.InnerException is FormatException; }
        Check(absentRejected, "json_absent_member_not_defaulted");
        var traceText = System.IO.File.ReadAllText("evidence/v06/session/debug-24-trace.json");
        var traceSteps = Elements(Member(traceText, "steps"));
        Check(traceSteps.Count == 175 && Member(Member(traceSteps[0], "command"), "debugScene") != "null"
            && Member(Member(Member(traceSteps[0], "command"), "debugScene"), "currentResults") == "null", "json_actual_v06_trace_175_steps_and_explicit_null");
        var configText = System.IO.File.ReadAllText("DicePrototype/Assets/Game/Data/P0Config.json");
        var cfg = JsonSerializer.Deserialize<GameConfig>(GameConfig.PrepareJsonForUnity(configText), new JsonSerializerOptions { IncludeFields = true });
        cfg.Validate();
        var stressFactory = type.GetMethod("CreateStressState", BindingFlags.NonPublic | BindingFlags.Static);
        var stress = (RunState)stressFactory.Invoke(null, new object[] { cfg });
        var stressSession = new GameSession(cfg, new MemorySaveStore(), stress);
        Check(stress.isDebug && stress.debugWorkspace && stress.dice.Count == 10 && stress.stage.capacity == 8 && stress.hand.orderedLockedIds.Count == 8
            && stress.controllers.Count == 10 && ControllerRules.OrdinaryUsed(stress.controllers) == 4 && stress.inventory.Count == 5, "actual_stress_factory_v06_production_invariants_10_8_10_5");
        Check(stress.controllers.Select(c => c.instanceId).Concat(stress.inventory.Select(i => i.instanceId)).Distinct().Count() == 15
            && stress.inventory.All(i => i.instanceId.StartsWith("ITEM_")), "stress_shared_item_namespace_has_no_collision");
        var roll = stressSession.Execute(new SessionCommand { commandId = "managed-roll", expectedActionSequence = stressSession.ActionSequence, kind = CommandKind.Roll });
        Check(roll.accepted && stressSession.State.dice.Count == 10 && stressSession.State.hand.rollsRemaining == 499, "stress_fixture_real_roll_preserves_locked_glass_population");
        var ordered = stressSession.State.hand.orderedLockedIds.AsEnumerable().Reverse().ToArray();
        var reorder = stressSession.Execute(new SessionCommand { commandId = "managed-reorder", expectedActionSequence = stressSession.ActionSequence, kind = CommandKind.Reorder, dieIds = ordered });
        Check(reorder.accepted && stressSession.State.hand.orderedLockedIds.SequenceEqual(ordered), "stress_fixture_actual_reorder_command_accepted");
        string frozen = GameSession.StateHash(stressSession.State);
        var score = stressSession.PreviewScoreRange();
        Check(score != null && ExactScoreMath.Parse(score.minimum.score) > 0 && GameSession.StateHash(stressSession.State) == frozen, "stress_preview_is_positive_and_pure");
        var chainFactory = type.GetMethod("CreateBrokenMimicChainState", BindingFlags.NonPublic | BindingFlags.Static);
        var chainState = (RunState)chainFactory.Invoke(null, new object[] { cfg });
        var chainSession = new GameSession(cfg, new MemorySaveStore(), chainState);
        Check(chainState.controllers.Count == 14 && chainState.stage.mimicBindings.Length == 14
            && ControllerRules.ResolveSources(chainSession.State.controllers, chainSession.State.stage.mimicBindings).All(s => s.abilityId == null), "actual_broken_chain_fixture_preserves_14_frozen_bindings");
        var validateFlags = type.GetMethod("ValidateDeferredDisplayArgs", BindingFlags.NonPublic | BindingFlags.Static);
        bool FlagsAccepted(params string[] flags)
        {
            try { validateFlags.Invoke(null, new object[] { flags }); return true; }
            catch (TargetInvocationException ex) when (ex.InnerException is InvalidOperationException) { return false; }
        }
        Check(FlagsAccepted("--verify-p0", "trace.json"), "default_full_verification_flags_remain_valid");
        Check(FlagsAccepted("--verify-p0", "trace.json", "--verify-defer-native-display"), "restricted_verification_requires_explicit_full_trace_flag");
        Check(!FlagsAccepted("--verify-defer-native-display"), "restricted_verification_without_full_trace_rejected");
        foreach (string incompatible in new[] { "--verify-display-only", "--verify-native-display-experiment", "--verify-display-recovery-write", "--verify-display-recovery-read" })
            Check(!FlagsAccepted("--verify-p0", "trace.json", "--verify-defer-native-display", incompatible), "restricted_verification_rejects_" + incompatible);
        var scope = type.GetMethod("VerificationScope", BindingFlags.NonPublic | BindingFlags.Static);
        string Scope(bool displayOnly, bool nativeExperiment, bool deferred) => (string)scope.Invoke(null, new object[] { displayOnly, nativeExperiment, deferred });
        Check(Scope(false,false,false) == "full_or_recovery", "default_report_scope_preserved");
        Check(Scope(true,false,false) == "display_only_no_replay_or_stress", "display_only_scope_does_not_claim_full_run");
        Check(Scope(false,true,false) == "native_display_experiment_no_acceptance", "native_experiment_scope_does_not_claim_acceptance");
        Check(Scope(false,false,true) == "full-gameplay-ui-perf_native_display_pending", "restricted_report_scope_exposes_native_display_gap");
        var completedStatus = type.GetMethod("CompletedVerificationStatus", BindingFlags.NonPublic | BindingFlags.Static);
        string CompletionStatus(bool deferred, string perf, int code) => (string)completedStatus.Invoke(null,new object[]{deferred,perf,code});
        Check(CompletionStatus(false,"within_frame_budget",0)=="passed", "default_success_status_preserved");
        Check(CompletionStatus(true,"within_frame_budget",0)=="partial_passed_native_display_pending", "restricted_success_never_returns_passed");
        Check(CompletionStatus(true,"budget_exceeded",3)=="budget_exceeded" && CompletionStatus(false,"budget_exceeded",3)=="budget_exceeded", "restricted_mode_preserves_performance_failure");
        Check(CompletionStatus(true,"measured_with_gaps",2)=="measured_with_gaps" && CompletionStatus(false,"measured_with_gaps",2)=="measured_with_gaps", "restricted_mode_preserves_evidence_gap_failure");
        Console.WriteLine(checks + " managed report/capture/trace/fixture/scope checks passed. Unity player behavior was not executed.");
        return 0;
    }
}
