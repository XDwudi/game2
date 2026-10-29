using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using DiceCraft.Verification;
using UnityEngine;

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
        void Check(bool value, string name) { if (!value) throw new Exception(name); Console.WriteLine("PASS " + name); }
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
        Console.WriteLine("18 managed report/capture checks passed. Unity player behavior was not executed.");
        return 0;
    }
}
