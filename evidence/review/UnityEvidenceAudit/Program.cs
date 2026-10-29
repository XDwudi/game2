using System.Reflection.Metadata;
using System.Security.Cryptography;
using System.Text.Json;
using DiceCraft.Core;

internal static class Program
{
    static readonly JsonSerializerOptions Json = new() { IncludeFields = true, WriteIndented = true };
    static string Root, Output; static int Passed;
    static string Enc<T>(T value) => JsonSerializer.Serialize(value, Json);
    static void Check(bool condition, string label) { if (!condition) throw new Exception(label); }
    static string Hash(string file) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(file))).ToLowerInvariant();
    sealed class Store : ISaveStore { public int Writes; public void Save(RunState state) { Writes++; } }
    static void Act(GameSession s, CommandKind kind) { var r = s.Execute(new SessionCommand { kind = kind, commandId = Guid.NewGuid().ToString("N"), seed = 777, expectedActionSequence = s.State?.actionSequence ?? 0 }); Check(r.accepted, kind + ": " + r.error); }
    static void PreviewCase(GameConfig cfg, string controller, bool suppress, bool pending)
    {
        var init = new GameSession(cfg, new Store()); Act(init, CommandKind.NewRun);
        var fixture = init.State; fixture.isDebug = true;
        fixture.controllers.Add(new OwnedItem { instanceId = "AUDIT-" + controller, contentId = controller });
        if (controller == "C07" && suppress) fixture.controllers.Add(new OwnedItem { instanceId = "AUDIT-C12", contentId = "C12" });
        for (int i = 0; i < fixture.dice.Count; i++) foreach (var face in fixture.dice[i].faces) { face.permanentPip = i + 1; face.baseColor = i == 0 || i == 2 ? "red" : "blue"; }
        var store = new Store(); var s = new GameSession(cfg, store, fixture);
        Act(s, CommandKind.StartStage); Act(s, CommandKind.Roll);
        int count = controller == "C07" ? 2 : 3;
        for (int i = 1; i <= count; i++) Check(s.TogglePending("D" + i.ToString("D3")), "select");
        Act(s, CommandKind.ConfirmLocks);
        if (controller == "C15" && suppress) { var changed = s.State; changed.hand.redWave = true; s = new GameSession(cfg, store, changed); }
        if (pending) Check(s.TogglePending("D005"), "pending select");
        int commits = 0; s.Committed += _ => commits++;
        string before = Enc(s.State); string beforePending = Enc(s.PendingIds); int writes = store.Writes;
        var effects = s.PreviewSubmissionEffects(); Check(effects.Length == (suppress ? 0 : 1), "effect count");
        string expected = Enc(effects);
        for (int i = 0; i < 8; i++) Check(Enc(s.PreviewSubmissionEffects()) == expected, "stable repeated preview");
        if (effects.Length > 0) { effects[0].dieId = "tampered"; Check(Enc(s.PreviewSubmissionEffects()) == expected, "returned effect aliases state"); }
        Check(Enc(s.State) == before && Enc(s.PendingIds) == beforePending && store.Writes == writes && commits == 0, "preview has side effect");
        s.ClearPending(); Act(s, CommandKind.Submit);
        Check(Enc(s.State.lastEffects.Where(e => e.kind == "DieCopied" || e.kind == "FaceDyed").ToArray()) == expected, "preview differs from commit");
        Check(commits == 1 && store.Writes == writes + 1, "commit missing/duplicated");
        Console.WriteLine("PASS preview_" + controller + "_suppressed=" + suppress + "_pending=" + pending); Passed++;
    }
    static void PdbAudit()
    {
        var entries = new List<object>();
        foreach (string name in new[] { "DiceCraft.Core", "DiceCraft.Runtime", "DiceCraft.EditMode", "DiceCraft.PlayModeTests" })
        {
            string pdb = Path.Combine(Root, "DicePrototype/Library/ScriptAssemblies", name + ".pdb");
            using var stream = File.OpenRead(pdb); using var provider = MetadataReaderProvider.FromPortablePdbStream(stream);
            var reader = provider.GetMetadataReader();
            foreach (var handle in reader.Documents)
            {
                var doc = reader.GetDocument(handle); string path = reader.GetString(doc.Name);
                if (!path.Contains("Assets/Game/", StringComparison.OrdinalIgnoreCase) && !path.Contains("Assets\\Game\\", StringComparison.OrdinalIgnoreCase)) continue;
                string resolved = Path.IsPathRooted(path) ? path : Path.Combine(Root, "DicePrototype", path);
                byte[] expected = reader.GetBlobBytes(doc.Hash); string checksum = Convert.ToHexString(expected).ToLowerInvariant();
                string algorithm = reader.GetGuid(doc.HashAlgorithm).ToString(); string current = null;
                if (File.Exists(resolved)) current = Convert.ToHexString(expected.Length == 20 ? SHA1.HashData(File.ReadAllBytes(resolved)) : SHA256.HashData(File.ReadAllBytes(resolved))).ToLowerInvariant();
                entries.Add(new { assembly = name, source = path, algorithm, compiledChecksum = checksum, currentChecksum = current, matches = current == checksum, pdbSha256 = Hash(pdb) });
            }
        }
        File.WriteAllText(Path.Combine(Output, "unity-pdb-source-check.json"), Enc(entries));
        Console.WriteLine("PDB source checks: " + entries.Count + " (mismatches are reported, not silently accepted)");
    }
    static int Main(string[] args)
    {
        Root = Path.GetFullPath(args[0]); Output = Path.Combine(Root, "evidence/review");
        var cfg = JsonSerializer.Deserialize<GameConfig>(File.ReadAllText(Path.Combine(Root, "DicePrototype/Assets/Game/Data/P0Config.json")), Json); cfg.Validate();
        foreach (string controller in new[] { "C07", "C15" }) foreach (bool suppress in new[] { false, true }) foreach (bool pending in new[] { false, true }) PreviewCase(cfg, controller, suppress, pending);
        var empty = new GameSession(cfg, new Store()); Check(empty.PreviewSubmissionEffects().Length == 0, "empty preview"); Act(empty, CommandKind.NewRun); Act(empty, CommandKind.StartStage); Check(empty.PreviewSubmissionEffects().Length == 0, "handready preview"); Act(empty, CommandKind.Roll); Check(empty.PreviewSubmissionEffects().Length == 0, "unlocked preview"); Console.WriteLine("PASS preview_empty_phases"); Passed++;
        PdbAudit(); File.WriteAllText(Path.Combine(Output, "preview-effects-independent-results.json"), Enc(new { passed = Passed, failed = 0, unityExecuted = false, source = "DicePrototype/Assets/Game/Core/Session/GameSession.cs", sourceSha256 = Hash(Path.Combine(Root, "DicePrototype/Assets/Game/Core/Session/GameSession.cs")) }));
        return 0;
    }
}
