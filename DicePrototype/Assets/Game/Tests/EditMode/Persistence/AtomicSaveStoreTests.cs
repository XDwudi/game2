using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Tests
{
    public sealed class AtomicSaveStoreTests
    {
        [Serializable] private sealed class Envelope
        {
            public int schema;
            public string config, sha256, payload;
        }
        private GameConfig config;
        private string directory, path;
        private RunState first, second, third;

        [SetUp] public void Setup()
        {
            string json = File.ReadAllText(Path.Combine(Application.dataPath, "Game/Data/P0Config.json"));
            GameConfig.ValidateJson(json);
            config = JsonUtility.FromJson<GameConfig>(json);
            config.Validate();
            directory = Path.Combine(Application.temporaryCachePath, "DiceCraftPersistence_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            path = Path.Combine(directory, "active-run.json");
            var session = new GameSession(config, new MemorySaveStore());
            Act(session, CommandKind.NewRun);
            first = session.State;
            Act(session, CommandKind.StartStage);
            second = session.State;
            Act(session, CommandKind.Roll);
            third = session.State;
        }
        [TearDown] public void PreserveEvidence()
            => TestContext.WriteLine("Isolated persistence evidence retained: " + directory);

        [Test] public void SAVE_V05_IsolatesBothLegacySchema2FilesAcrossNewRunBackupRotation()
        {
            var store=PrepareTwoVersions();
            foreach(var file in new[]{path,path+".backup"})
            {
                var old=JsonUtility.FromJson<Envelope>(File.ReadAllText(file));old.schema=2;old.config="p0.4.0";
                File.WriteAllText(file,JsonUtility.ToJson(old));
            }
            var active=File.ReadAllBytes(path);var backup=File.ReadAllBytes(path+".backup");
            Assert.That(store.Load(),Is.Null);Assert.That(store.LastLoadNotice,Does.Contain("开始新挑战"));
            var archived=Directory.GetFiles(directory,"*.legacy-schema2-*");Assert.That(archived.Length,Is.EqualTo(2));
            store.Load();Assert.That(Directory.GetFiles(directory,"*.legacy-schema2-*").Length,Is.EqualTo(2));
            store.Save(first);store.Save(second);store.Save(third);
            Assert.That(archived.Any(f=>File.ReadAllBytes(f).SequenceEqual(active)),Is.True);
            Assert.That(archived.Any(f=>File.ReadAllBytes(f).SequenceEqual(backup)),Is.True);
            Assert.That(JsonUtility.ToJson(store.Load()),Is.EqualTo(JsonUtility.ToJson(third)));
        }

        [Test] public void SAVE_RealJsonUtilityRoundTripPreservesDiceRngReceiptsAndPhysicalResults()
        {
            var store = new AtomicSaveStore(path, config);
            store.Save(third);
            var restored = store.Load();
            Assert.That(restored, Is.Not.Null, store.LastLoadNotice);
            Assert.That(store.LastLoadNotice, Is.Null);
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(third)));
            Assert.That(restored.processedReceipts.Count, Is.EqualTo(3));
            Assert.That(restored.hand.currentResults.All(r => r.hasRolled), Is.True);
        }

        [Test] public void SAVE_RejectsInvalidCandidateBeforeChangingActiveBackupOrKnownGoodBytes()
        {
            var store=PrepareTwoVersions();var active=File.ReadAllBytes(path);var backup=File.ReadAllBytes(path+".backup");
            var invalid=third.Clone();invalid.gold=-1;
            Assert.Catch<Exception>(()=>store.Save(invalid));
            CollectionAssert.AreEqual(active,File.ReadAllBytes(path));CollectionAssert.AreEqual(backup,File.ReadAllBytes(path+".backup"));
            store.Save(third);CollectionAssert.AreEqual(active,File.ReadAllBytes(path+".backup"));
            Assert.That(store.Load().actionSequence,Is.EqualTo(third.actionSequence));
        }

        [Test] public void SAVE_ExternalValidReplacementCannotReuseStaleKnownGoodBytes()
        {
            var store=PrepareTwoVersions();string other=Path.Combine(directory,"external.json");
            new AtomicSaveStore(other,config).Save(third);byte[] external=File.ReadAllBytes(other);
            File.WriteAllBytes(path,external);store.Save(first);
            CollectionAssert.AreEqual(external,File.ReadAllBytes(path+".backup"));
            Corrupt(path,"hash");Assert.That(store.Load().actionSequence,Is.EqualTo(third.actionSequence));
        }

        [TestCase("triggers")][TestCase("score")]
        public void SAVE_V05_CorruptFrozenHistoryWithValidHashFallsBackBeforeClone(string defect)
        {
            var fixture=third.Clone();int serial=fixture.nextItemSerial++;
            fixture.controllers.Add(new ControllerInstance{instanceId="ITEM_"+serial.ToString("D5"),definitionId="C38",acquiredSerial=serial});
            var source=new GameSession(config,new MemorySaveStore(),fixture);
            string id=source.State.dice[0].dieId;Assert.That(source.TogglePending(id),Is.True);
            Act(source,CommandKind.ConfirmLocks);Act(source,CommandKind.Submit);
            var store=new AtomicSaveStore(path,config);store.Save(third);store.Save(source.State);
            var envelope=JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
            var broken=JsonUtility.FromJson<RunState>(envelope.payload);
            if(defect=="triggers")broken.lastSubmission.breakdown.triggeredControllerIds=null;
            else broken.lastSubmission.breakdown.score++;
            envelope.payload=JsonUtility.ToJson(broken);
            using(var sha=SHA256.Create())envelope.sha256=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(envelope.payload))).Replace("-","").ToLowerInvariant();
            File.WriteAllText(path,JsonUtility.ToJson(envelope));
            var restored=store.Load();Assert.That(restored,Is.Not.Null,store.LastLoadNotice);
            Assert.That(restored.actionSequence,Is.EqualTo(third.actionSequence));Assert.That(store.LastLoadNotice,Does.Contain("有效备份"));
            Assert.DoesNotThrow(()=>new GameSession(config,new MemorySaveStore(),restored));
        }

        [TestCase("schema")]
        [TestCase("hash")]
        [TestCase("structure")]
        public void SAVE_BothFilesCorruptReturnsNullAndNoticeWithoutOverwritingEvidence(string kind)
        {
            var store = PrepareTwoVersions();
            Corrupt(path, kind); Corrupt(path + ".backup", kind);
            byte[] active = File.ReadAllBytes(path), backup = File.ReadAllBytes(path + ".backup");
            Assert.That(store.Load(), Is.Null);
            Assert.That(store.LastLoadNotice, Is.Not.Null.And.Not.Empty);
            CollectionAssert.AreEqual(active, File.ReadAllBytes(path));
            CollectionAssert.AreEqual(backup, File.ReadAllBytes(path + ".backup"));
        }

        [TestCase("schema")]
        [TestCase("hash")]
        [TestCase("structure")]
        public void SAVE_BackupRecoveryThenSavePreservesLastValidBackup(string kind)
        {
            var store = PrepareTwoVersions();
            byte[] backup = File.ReadAllBytes(path + ".backup");
            Corrupt(path, kind);
            byte[] rejected = File.ReadAllBytes(path);
            var fallback = store.Load();
            Assert.That(fallback, Is.Not.Null, store.LastLoadNotice);
            Assert.That(fallback.actionSequence, Is.EqualTo(first.actionSequence));
            Assert.That(store.LastLoadNotice, Does.Contain("备份"));
            store.Save(third);
            AssertRecoveredWrite(store, backup, rejected);
        }

        [TestCase("schema")]
        [TestCase("hash")]
        [TestCase("structure")]
        public void SAVE_DirectWriteWithoutLoadStillPreservesBackupWhenActiveIsCorrupt(string kind)
        {
            PrepareTwoVersions();
            byte[] backup = File.ReadAllBytes(path + ".backup");
            Corrupt(path, kind);
            byte[] rejected = File.ReadAllBytes(path);
            var freshAdapter = new AtomicSaveStore(path, config);
            freshAdapter.Save(third); // Intentionally no Load on this adapter.
            AssertRecoveredWrite(freshAdapter, backup, rejected);
        }

        [Test] public void SAVE_NullGroupHistoryRemainsNoGroupAndCannotChangeNextHandOrScore()
        {
            var source = new GameSession(config, new MemorySaveStore(), third);
            string id = source.State.dice[0].dieId;
            Assert.That(source.TogglePending(id), Is.True);
            Act(source, CommandKind.ConfirmLocks, c => c.dieIds = new[] {id});
            var preview = source.PreviewScore();
            Assert.That(preview.chosenGroupId, Is.Null, "One submitted die cannot form a same-value group.");
            Act(source, CommandKind.Submit);
            var saved = source.State;
            var store = new AtomicSaveStore(path, config);
            store.Save(saved);
            var loaded = store.Load();
            Assert.That(loaded, Is.Not.Null, store.LastLoadNotice);
            Assert.That(string.IsNullOrEmpty(loaded.lastSubmission.breakdown.chosenGroupId), Is.True);
            Assert.That(loaded.lastSubmission.breakdown.matchedGroupIds, Is.Empty);
            Assert.That(loaded.lastSubmission.breakdown.score, Is.EqualTo(preview.score));
            var recomputed = ScoreCalculator.Evaluate(loaded.lastSubmission.snapshot, config);
            Assert.That(recomputed.chosenGroupId, Is.Null);
            Assert.That(recomputed.score, Is.EqualTo(preview.score));
            Assert.That(JsonUtility.ToJson(recomputed), Is.EqualTo(JsonUtility.ToJson(loaded.lastSubmission.breakdown)));
            var restored = new GameSession(config, new MemorySaveStore(), loaded);
            Act(source, CommandKind.Roll);
            Act(restored, CommandKind.Roll);
            Assert.That(JsonUtility.ToJson(restored.State), Is.EqualTo(JsonUtility.ToJson(source.State)),
                "A historical null-to-empty string must not affect new gameplay, receipts or RNG.");
        }

        private AtomicSaveStore PrepareTwoVersions()
        {
            var store = new AtomicSaveStore(path, config);
            store.Save(first); store.Save(second);
            Assert.That(File.Exists(path + ".backup"), Is.True);
            return store;
        }
        private void AssertRecoveredWrite(AtomicSaveStore store, byte[] backup, byte[] rejected)
        {
            CollectionAssert.AreEqual(backup, File.ReadAllBytes(path + ".backup"), "Corrupt active must never replace the valid backup.");
            var accepted = store.Load();
            Assert.That(accepted, Is.Not.Null, store.LastLoadNotice);
            Assert.That(accepted.actionSequence, Is.EqualTo(third.actionSequence));
            Assert.That(JsonUtility.ToJson(accepted), Is.EqualTo(JsonUtility.ToJson(third)));
            string[] quarantine = Directory.GetFiles(directory, "active-run.json.rejected-*");
            Assert.That(quarantine, Has.Length.EqualTo(1));
            CollectionAssert.AreEqual(rejected, File.ReadAllBytes(quarantine[0]));
        }
        private void Corrupt(string file, string kind)
        {
            var e = JsonUtility.FromJson<Envelope>(File.ReadAllText(file));
            if (kind == "schema") e.schema++;
            else if (kind == "hash") e.sha256 = new string('0', 64);
            else
            {
                var state = JsonUtility.FromJson<RunState>(e.payload);
                // The envelope remains valid and its checksum is recomputed. Only the
                // production state invariants can reject these duplicate physical IDs.
                state.dice[1].dieId = state.dice[0].dieId;
                e.payload = JsonUtility.ToJson(state);
                using (var sha = SHA256.Create())
                    e.sha256 = BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(e.payload))).Replace("-", "").ToLowerInvariant();
            }
            File.WriteAllText(file, JsonUtility.ToJson(e), Encoding.UTF8);
        }
        private static void Act(GameSession session, CommandKind kind, Action<SessionCommand> configure = null)
        {
            long sequence = session.State?.actionSequence ?? 0;
            var command = new SessionCommand {kind = kind, commandId = "persistence-" + (sequence + 1), expectedActionSequence = sequence, seed = 20261028};
            configure?.Invoke(command);
            var result = session.Execute(command);
            Assert.That(result.accepted, Is.True, kind + ": " + result.error);
        }
    }
}
