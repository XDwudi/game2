using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DiceCraft.Core;

class ShopPersistenceReview
{
    static readonly JsonSerializerOptions J = new JsonSerializerOptions { IncludeFields = true };
    static readonly List<object> Checks = new List<object>();
    static string Dump(object value) => JsonSerializer.Serialize(value, value.GetType(), J);
    static string Sha(byte[] value) => Convert.ToHexStringLower(SHA256.HashData(value));
    static void Check(bool value, string name) { Checks.Add(new { name, passed = value }); if (!value) throw new Exception(name); }
    static RunState Parse(string path)
    {
        using var envelope = JsonDocument.Parse(File.ReadAllText(path));
        string payload = envelope.RootElement.GetProperty("payload").GetString();
        Check(Sha(Encoding.UTF8.GetBytes(payload)) == envelope.RootElement.GetProperty("sha256").GetString(), "Actual envelope checksum " + Path.GetFileName(path));
        var state = JsonSerializer.Deserialize<RunState>(payload, J);
        Check(state.processedReceipts.All(x => x.command.kind != CommandKind.Submit), "Actual fixture has no Submit before loader normalization");
        // The actual Unity loader was observed to normalize this exact pre-Submit
        // SubmissionRecord placeholder. STJ is not claimed to emulate JsonUtility.
        state.lastSubmission = null;
        return state;
    }
    static int Main(string[] args)
    {
        string root = Directory.GetCurrentDirectory();
        string actual = Path.Combine(root, "evidence/v06/player/player-20261031-152132-2388cb4a/debug-refresh-shop-save/debug-run.json");
        string output = args[0]; if (File.Exists(output)) throw new Exception("Refuse overwrite");
        var inputs = new[] { actual, actual + ".backup", "DicePrototype/Assets/Game/Core/Session/GameSession.Debug.cs", "DicePrototype/Assets/Game/Core/Session/GameSession.Validation.cs", "DicePrototype/Assets/Game/Data/P0Config.json", "evidence/v06/review/harness/bin/Debug/net10.0/ReviewHarness.dll", "evidence/v06/review/shop_persistence/Program.cs", "evidence/v06/review/shop_persistence/ShopPersistenceReview.csproj" };
        var hashes = inputs.ToDictionary(x => x, x => Sha(File.ReadAllBytes(x)));
        string error = null; RunState after = null; string expected = null;
        try
        {
            var cfg = JsonSerializer.Deserialize<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText("DicePrototype/Assets/Game/Data/P0Config.json")), J); cfg.Validate();
            after = Parse(actual); var before = Parse(actual + ".backup");
            var validator = new GameSession(cfg, new MemorySaveStore());
            validator.ValidateState(after); validator.ValidateState(before);
            expected = after.processedReceipts.Last().stateHash;
            string frozen = Dump(after);
            Check(GameSession.StateHash(after) == expected, "New hash matches original actual persisted receipt with unused DTOs retained");
            Check(frozen == Dump(after), "Hash has no mutation side effects");
            var session = new GameSession(cfg, new MemorySaveStore(), before);
            var command = after.processedReceipts.Last().command.Clone();
            var result = session.Execute(command);
            Check(result.accepted && !result.duplicate && GameSession.StateHash(session.State) == expected, "Actual backup plus actual persisted command reproduces exact receipt hash");
            Check(session.State.gold == 997 && session.State.actionSequence == 6 && session.State.shop.controllerRefreshCount == 1, "Actual fee/sequence/count preserved");
            var restored = new GameSession(cfg, new MemorySaveStore(), after);
            Check(restored.Execute(command).duplicate && GameSession.StateHash(restored.State) == expected, "Actual restored duplicate preserves state");
            foreach (var field in new[] { "contentId", "offerId", "batchId", "category", "basePrice", "sold", "priceReasons" })
            {
                var changed = after.Clone(); var o = changed.shop.offers.First(x => x.category != "controller" && x.category != "new_die");
                var f = typeof(ShopOffer).GetField(field);
                object value = f.FieldType == typeof(string) ? ((string)f.GetValue(o)) + "_changed" : f.FieldType == typeof(int) ? ((int)f.GetValue(o)) + 1 : f.FieldType == typeof(bool) ? !(bool)f.GetValue(o) : new[] { "changed" };
                f.SetValue(o, value); Check(GameSession.StateHash(changed) != expected, "Unrelated quote field remains hash-significant: " + field);
            }
            foreach (bool controller in new[] { true, false })
            {
                var active = after.Clone(); var o = active.shop.offers.FirstOrDefault(x => x.category == (controller ? "controller" : "new_die"));
                // The retained shop has no new-die offer. Construct only this active
                // payload sensitivity fixture; keep actual transaction checks above unchanged.
                if (o == null) { o = active.shop.offers[0]; o.category = "new_die"; o.controller = null; o.die = active.dice[0].Clone(); }
                string beforeMutation = GameSession.StateHash(active);
                if (controller) o.controller.enchantmentId = o.controller.enchantmentId == "base10" ? "none" : "base10";
                else o.die.faces[0].permanentPip++;
                Check(GameSession.StateHash(active) != beforeMutation, "Active payload retains significance: " + (controller ? "controller" : "die"));
            }
            var malformed = after.Clone(); malformed.shop.offers.First(x => x.category != "controller").controller.state.fundAccrual = 1;
            bool rejected = false; try { validator.ValidateState(malformed); } catch { rejected = true; }
            Check(rejected && GameSession.StateHash(malformed) != expected, "Hidden unused nonempty payload is rejected and not erased");
            var nullFaces = after.Clone(); nullFaces.shop.offers.First(x => x.category != "new_die").die.faces = null;
            rejected = false; try { validator.ValidateState(nullFaces); } catch { rejected = true; }
            Check(rejected && GameSession.StateHash(nullFaces) != expected, "Malformed null face array is rejected before cloning and is not canonicalized away");
        }
        catch (Exception ex) { error = ex.ToString(); }
        bool stable = hashes.All(x => x.Value == Sha(File.ReadAllBytes(x.Key)));
        File.WriteAllText(output, JsonSerializer.Serialize(new { scope = "Independent managed replay of actual failing Player save/backup; not Unity execution", status = error == null && stable ? "passed" : "failed", error, sourceStable = stable, receiptHash = expected, checks = Checks, inputs = hashes }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{Checks.Count} checks; stable={stable}; error={error}");
        return error == null && stable ? 0 : 1;
    }
}
