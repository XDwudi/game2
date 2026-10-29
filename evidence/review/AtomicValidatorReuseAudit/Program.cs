using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using DiceCraft;
using DiceCraft.Core;

internal static class AtomicValidatorReuseAudit
{
    private static readonly JsonSerializerOptions Json = new JsonSerializerOptions { IncludeFields = true, WriteIndented = true };
    private static readonly List<object> results = new List<object>();
    private static int failures;
    private static string Encode(object x) => JsonSerializer.Serialize(x, x.GetType(), Json);
    private static void Check(bool x, string message) { if (!x) throw new Exception(message); }
    private static void Run(string name, Action body)
    {
        try { body(); results.Add(new { name, passed = true }); Console.WriteLine("PASS " + name); }
        catch (Exception e) { failures++; results.Add(new { name, passed = false, error = e.ToString() }); Console.WriteLine("FAIL " + name + ": " + e.Message); }
    }
    public static int Main(string[] args)
    {
        string root = Path.GetFullPath(args[0]);
        string output = Path.Combine(root, "evidence/review/AtomicValidatorReuseAudit");
        var cfg = JsonSerializer.Deserialize<GameConfig>(File.ReadAllText(Path.Combine(root, "DicePrototype/Assets/Game/Data/P0Config.json")), Json);
        cfg.Validate();
        var harness = typeof(AtomicValidatorReuseAudit).Assembly.GetType("ReviewHarness");
        var flags = BindingFlags.NonPublic | BindingFlags.Static;
        harness.GetField("root", flags).SetValue(null, root);
        harness.GetField("reportDir", flags).SetValue(null, output);
        harness.GetField("cfg", flags).SetValue(null, cfg);
        void Invoke(string method, object value) => harness.GetMethod(method, flags).Invoke(null, new[] { value });
        Run("IO prior independent regression / recovered backup preserved", () => Invoke("BackupPreservation", true));
        Run("IO prior independent regression / direct write without Load", () => Invoke("BackupPreservation", false));
        foreach (string mode in new[] { "checksum", "schema", "semantic", "both" })
            Run("IO prior independent regression / " + mode, () => Invoke("InvalidEnvelope", mode));
        Run("Reusable validator stays stateless and input-pure across valid/invalid/repeated calls", () =>
        {
            var producer = new GameSession(cfg, new MemorySaveStore());
            var fixtures = new List<RunState>();
            foreach (var kind in new[] { CommandKind.NewRun, CommandKind.StartStage, CommandKind.Roll })
            {
                var command = new SessionCommand { kind = kind, commandId = kind.ToString(), expectedActionSequence = producer.ActionSequence, seed = 4321 };
                Check(producer.Execute(command).accepted, "fixture failed " + kind); fixtures.Add(producer.State);
            }
            var reusable = new GameSession(cfg, new MemorySaveStore());
            string before = Encode(fixtures), configBefore = Encode(cfg);
            for (int n = 0; n < 100; n++) foreach (var f in fixtures) reusable.ValidateState(f);
            var invalid = fixtures[2].Clone(); invalid.dice[1].dieId = invalid.dice[0].dieId;
            bool rejected = false; try { reusable.ValidateState(invalid); } catch { rejected = true; }
            Check(rejected, "semantic corruption was accepted");
            Parallel.For(0, 100, i => reusable.ValidateState(fixtures[i % fixtures.Count]));
            Check(before == Encode(fixtures) && configBefore == Encode(cfg), "validator mutated input/config");
            Check(reusable.State == null && reusable.ActionSequence == 0 && reusable.PendingIds.Length == 0 && !reusable.HasPendingSave && !reusable.Busy, "validator retained gameplay state");
            string path = Path.Combine(output, "reused-adapter-" + Guid.NewGuid().ToString("N"), "run.json");
            var adapter = new AtomicSaveStore(path, cfg);
            var field = typeof(AtomicSaveStore).GetField("validator", BindingFlags.Instance | BindingFlags.NonPublic);
            object original = field.GetValue(adapter);
            foreach (var f in fixtures) { adapter.Save(f); Check(Encode(adapter.Load()) == Encode(f), "reused adapter roundtrip mismatch"); }
            Check(ReferenceEquals(original, field.GetValue(adapter)), "validator was not reused");
            Check(((GameSession)original).State == null, "adapter validator acquired state");
        });
        string[] files = { "DicePrototype/Assets/Game/Runtime/AtomicSaveStore.cs", "DicePrototype/Assets/Game/Core/Session/GameSession.Validation.cs", "DicePrototype/Assets/Game/Core/Session/GameSession.cs" };
        var hashes = files.ToDictionary(p => p, p => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root, p)))).ToLowerInvariant());
        File.WriteAllText(Path.Combine(output, "results.json"), JsonSerializer.Serialize(new { utc = DateTime.UtcNow, runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription, limitation = "Real Windows IO and production C#; System.Text.Json shim replaces Unity JsonUtility. No Unity runtime was launched.", results, total = results.Count, failures, sourceHashes = hashes }, Json));
        return failures == 0 ? 0 : 1;
    }
}
