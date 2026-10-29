using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using DiceCraft.Core;

internal static class Program
{
    private static readonly JsonSerializerOptions Options = new JsonSerializerOptions { IncludeFields = true };
    private static readonly List<string> Passed = new List<string>();
    private static int Main(string[] args)
    {
        var failures = new List<string>();
        string data = args.Length == 0 ? Path.GetFullPath("DicePrototype/Assets/Game/Data") : args[0];
        try
        {
            string json = File.ReadAllText(Path.Combine(data, "P0Config.json"));
            GameConfig.ValidateJson(json);
            GameConfig config = JsonSerializer.Deserialize<GameConfig>(json, Options);
            config.Validate(); Passed.Add("CONFIG-exact38-and11");
            foreach (var example in JsonDocument.Parse(File.ReadAllText(Path.Combine(data, "GoldenScores.json"))).RootElement.GetProperty("cases").EnumerateArray())
            {
                string id = example.GetProperty("id").GetString();
                try
                {
                    int serial = 0;
                    var snapshot = new SubmissionSnapshot
                    {
                        runId = "golden", stageId = "STAGE_01", handId = id, configVersion = config.configVersion,
                        capacity = example.GetProperty("capacity").GetInt32(), rollsRemaining = example.GetProperty("rollsRemaining").GetInt32(),
                        redWave = example.GetProperty("redWave").GetBoolean(),
                        equippedControllerIds = example.GetProperty("controllerIds").EnumerateArray().Select(v => v.GetString()).ToArray(),
                        handLevels = example.GetProperty("levels").EnumerateObject().Select(v => new HandLevel { handId = v.Name, level = v.Value.GetInt32() }).ToArray(),
                        dice = example.GetProperty("dice").EnumerateArray().Select(d => new SubmissionDie
                        {
                            dieId = "D" + (++serial).ToString("D3"), faceId = "F01", currentPip = d.GetProperty("p").GetInt32(), currentColor = d.GetProperty("c").GetString(),
                            permanentSixFaceSum = d.TryGetProperty("sum6", out var sum) ? sum.GetInt32() : 21,
                            isGold = d.TryGetProperty("gold", out var gold) && gold.GetBoolean(), isGlass = d.TryGetProperty("glass", out var glass) && glass.GetBoolean(), lifetime = "permanent"
                        }).ToArray()
                    };
                    string before = JsonSerializer.Serialize(snapshot, Options);
                    var result = ScoreCalculator.Evaluate(snapshot, config);
                    JsonElement actual = JsonSerializer.SerializeToElement(result, Options);
                    int fields = 0;
                    foreach (var expected in example.GetProperty("expected").EnumerateObject())
                    {
                        if (JsonSerializer.Serialize(actual.GetProperty(expected.Name)) != JsonSerializer.Serialize(expected.Value))
                            throw new Exception(expected.Name + ": expected " + expected.Value + ", got " + actual.GetProperty(expected.Name));
                        fields++;
                    }
                    if (fields != 18 || JsonSerializer.Serialize(snapshot, Options) != before) throw new Exception("Field count/input mutation.");
                    Passed.Add("GOLD-" + id);
                }
                catch (Exception error) { failures.Add(id + ": " + error); }
            }
            VerifyVectors(config);
            var faces = DiceFactory.CreateStandard(config, 1).faces;
            int tested = 0, min = int.MaxValue;
            string[] textures = {"none", "smooth", "rough"};
            for (int pattern = 0; pattern < 729; pattern++)
            {
                int rest = pattern;
                for (int i = 0; i < 6; i++) { faces[i].texture = textures[rest % 3]; rest /= 3; }
                for (int buffs = 0; buffs < 4; buffs++)
                {
                    var units = ProbabilityCalculator.BuildUnits(faces, (buffs & 1) != 0, (buffs & 2) != 0, config);
                    if (units.Sum() != 1000000 || units.Min() < 1) throw new Exception("Probability invariant.");
                    tested++; min = Math.Min(min, units.Min());
                }
            }
            Passed.Add("PROB-" + tested + "-combinations-min-" + min);
            var rng = new GameRng(1);
            var expectedRng = new uint[] {270369,67634689,2647435461,307599695,2398689233};
            foreach (uint expected in expectedRng) if (rng.NextNonZeroUInt() != expected) throw new Exception("RNG vector mismatch.");
            Passed.Add("RNG-first5");
            bool missingRejected = false;
            try { GameConfig.ValidateJson(json.Replace("\"occupiedIndex\": 0", "\"wrongIndex\": 0")); } catch (ArgumentException) { missingRejected = true; }
            if (!missingRejected) throw new Exception("Missing zero-valued config parameter was accepted.");
            Passed.Add("CONFIG-missing-zero-rejected");
        }
        catch (Exception error) { failures.Add(error.ToString()); }
        string hostRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../.."));
        var sourceHashes = Directory.GetFiles(Path.GetFullPath(Path.Combine(hostRoot, "..")), "*.cs")
            .Concat(new[] {Path.Combine(data, "P0Config.json"), Path.Combine(data, "GoldenScores.json")})
            .OrderBy(p => p, StringComparer.Ordinal).Select(p => new { file = Path.GetFileName(p), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))).ToLowerInvariant() }).ToArray();
        var report = new { runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, utc = DateTime.UtcNow.ToString("O"), sourceHashes, passed = Passed, failures };
        string output = Path.Combine(hostRoot, "report.json");
        File.WriteAllText(output, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine("Pure C# validation: " + Passed.Count + " groups passed; " + failures.Count + " failed. " + output);
        foreach (string failure in failures) Console.WriteLine(failure);
        return failures.Count == 0 ? 0 : 1;
    }
    private static void VerifyVectors(GameConfig config)
    {
        string[] textures = {"none", "smooth", "rough", "none", "none"};
        int[][] expected = {
            new[] {166667,166667,166667,166667,166666,166666},
            new[] {173334,173334,173333,173333,173333,133333},
            new[] {160000,160000,160000,160000,160000,200000},
            new[] {153846,153846,153846,153846,153846,230770},
            new[] {214286,142857,142857,142857,142857,214286}
        };
        for (int i = 0; i < expected.Length; i++)
        {
            var faces = DiceFactory.CreateStandard(config, 1).faces;
            faces[0].texture = textures[i];
            var actual = ProbabilityCalculator.BuildUnits(faces, i >= 3, i == 4, config);
            if (!actual.SequenceEqual(expected[i])) throw new Exception("Probability contract vector " + i);
            Passed.Add("PROB-vector-" + (i+1));
        }
    }
}
