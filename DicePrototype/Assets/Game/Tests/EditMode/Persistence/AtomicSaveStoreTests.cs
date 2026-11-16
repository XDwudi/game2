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
            config = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(json));
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

        // v1.1 rewrite (docs/specs/v11/RULES_DELTA.md): old-version files are deleted on Load, without a notice and without
        // legacy copies; the next run then saves and rotates its backup as usual.
        [Test] public void SAVE_V11_BothSchema2FilesAreDeletedWithoutNoticeAndNewSavesRotateNormally()
        {
            var store=PrepareTwoVersions();
            foreach(var file in new[]{path,path+".backup"}) SetVersion(file,2,"p0.4.0");
            Assert.That(store.Load(),Is.Null);Assert.That(store.LastLoadNotice,Is.Null);Assert.That(store.LastIncompatibleSave,Is.Null);
            Assert.That(File.Exists(path),Is.False);Assert.That(File.Exists(path+".backup"),Is.False);
            Assert.That(store.LastDeletedSaveFiles,Is.EquivalentTo(Full(path,path+".backup")));
            Assert.That(Directory.GetFiles(directory,"*.legacy-schema*"),Is.Empty);Assert.That(Directory.GetFiles(directory,"*.rejected-*"),Is.Empty);
            Assert.That(store.Load(),Is.Null);Assert.That(store.LastLoadNotice,Is.Null);Assert.That(store.LastDeletedSaveFiles,Is.Empty);
            store.Save(first);store.Save(second);store.Save(third);
            Assert.That(File.Exists(path+".backup"),Is.True);
            Assert.That(JsonUtility.ToJson(store.Load()),Is.EqualTo(JsonUtility.ToJson(third)));Assert.That(store.LastLoadNotice,Is.Null);
        }

        [Test] public void SAVE_V11_AConfigVersionChangeAloneDeletesActiveBackupAndEveryLegacyCopyButNothingElse()
        {
            var store=PrepareTwoVersions();
            foreach(var file in new[]{path,path+".backup"}) SetVersion(file,config.saveSchemaVersion,"p0.9.0");
            string[] legacy={path+".legacy-schema5-0123456789abcdef",path+".legacy-schema2-fedcba9876543210"};
            foreach(var file in legacy) File.WriteAllText(file,"{\"schema\":5,\"config\":\"p0.9.0\"}");
            string quarantine=path+".rejected-"+Guid.NewGuid().ToString("N"),other=Path.Combine(directory,"debug-run.json.legacy-schema5-0000000000000000"),settings=Path.Combine(directory,"settings.json");
            foreach(var file in new[]{quarantine,other,settings}) File.WriteAllText(file,"keep");
            Assert.That(store.Load(),Is.Null);Assert.That(store.LastLoadNotice,Is.Null);Assert.That(store.LastIncompatibleSave,Is.Null);
            foreach(var file in new[]{path,path+".backup"}.Concat(legacy)) Assert.That(File.Exists(file),Is.False,file);
            Assert.That(store.LastDeletedSaveFiles,Is.EquivalentTo(Full(new[]{path,path+".backup"}.Concat(legacy).ToArray())));
            foreach(var file in new[]{quarantine,other,settings}) Assert.That(File.ReadAllText(file),Is.EqualTo("keep"),"not a save of this slot written by another version: "+file);
        }

        [Test] public void SAVE_V11_LegacyCopiesWithoutAnyActiveFileArePurgedSilently()
        {
            Directory.CreateDirectory(directory);string legacy=path+".legacy-schema3-0123456789abcdef";File.WriteAllText(legacy,"{}");
            var store=new AtomicSaveStore(path,config);
            Assert.That(store.Load(),Is.Null);Assert.That(store.LastLoadNotice,Is.Null);Assert.That(File.Exists(legacy),Is.False);
            Assert.That(store.LastDeletedSaveFiles,Is.EqualTo(Full(legacy)));
        }
        // The store reports normalised absolute paths (Unity's data paths mix '/' with the '\' of folder listings).
        private static string[] Full(params string[] files)=>files.Select(Path.GetFullPath).ToArray();

        [Test] public void SAVE_V11_AnOldActiveIsDeletedAndTheCurrentBackupIsStillRestored()
        {
            var store=PrepareTwoVersions();byte[] backup=File.ReadAllBytes(path+".backup");SetVersion(path,config.saveSchemaVersion,"p0.9.0");
            var restored=store.Load();Assert.That(restored,Is.Not.Null,store.LastLoadNotice);Assert.That(restored.actionSequence,Is.EqualTo(first.actionSequence));
            Assert.That(store.LastLoadNotice,Does.Contain("有效备份"),"restoring the backup of this version is reported as before");Assert.That(store.LastIncompatibleSave,Is.Null);
            Assert.That(File.Exists(path),Is.False);CollectionAssert.AreEqual(backup,File.ReadAllBytes(path+".backup"),"a file of this version is never deleted");
        }

        [Test] public void SAVE_V11_ADamagedActiveOfThisVersionIsKeptAndQuarantinedWhileAnOldBackupIsDeleted()
        {
            var store=PrepareTwoVersions();Corrupt(path,"hash");SetVersion(path+".backup",2,"p0.4.0");byte[] damaged=File.ReadAllBytes(path);
            Assert.That(store.Load(),Is.Null);Assert.That(store.LastLoadNotice,Is.Not.Null.And.Not.Empty,"damage of this version keeps its notice");
            CollectionAssert.AreEqual(damaged,File.ReadAllBytes(path),"a damaged file of this version is never deleted");Assert.That(File.Exists(path+".backup"),Is.False);
            store.Save(third);
            var quarantine=Directory.GetFiles(directory,"active-run.json.rejected-*");Assert.That(quarantine,Has.Length.EqualTo(1));CollectionAssert.AreEqual(damaged,File.ReadAllBytes(quarantine[0]));
            Assert.That(File.Exists(path+".backup"),Is.False,"a damaged active never becomes the backup");Assert.That(JsonUtility.ToJson(store.Load()),Is.EqualTo(JsonUtility.ToJson(third)));
        }

        [Test] public void SAVE_V11_TheDebugSlotDeletesItsOwnOldFilesAndLeavesTheNormalSlotAlone()
        {
            var debugState=GameSession.CreateDebugWorkspace(config,new MemorySaveStore()).State;string debugPath=Path.Combine(directory,"debug-run.json");
            var debugStore=new AtomicSaveStore(debugPath,config,SaveSlotPolicy.Debug);debugStore.Save(debugState);debugStore.Save(debugState);
            PrepareTwoVersions();
            foreach(var file in new[]{path,path+".backup",debugPath,debugPath+".backup"}) SetVersion(file,2,"p0.4.0");
            File.WriteAllText(debugPath+".legacy-schema2-0123456789abcdef","{}");File.WriteAllText(path+".legacy-schema2-0123456789abcdef","{}");
            Assert.That(new AtomicSaveStore(debugPath,config,SaveSlotPolicy.Debug).Load(),Is.Null);
            Assert.That(File.Exists(debugPath),Is.False);Assert.That(File.Exists(debugPath+".backup"),Is.False);Assert.That(File.Exists(debugPath+".legacy-schema2-0123456789abcdef"),Is.False);
            Assert.That(File.Exists(path),Is.True);Assert.That(File.Exists(path+".backup"),Is.True);Assert.That(File.Exists(path+".legacy-schema2-0123456789abcdef"),Is.True,"another slot's files are not this store's");
            var reader=new AtomicSaveStore(path,config,SaveSlotPolicy.Normal);Assert.That(reader.Load(),Is.Null);Assert.That(reader.LastLoadNotice,Is.Null);
            Assert.That(Directory.GetFiles(directory,"active-run.json*"),Is.Empty);
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

        [TestCase("first")][TestCase("middle")][TestCase("last")][TestCase("truncated")]
        public void SAVE_CachedKnownGoodDetectsFullByteChangesAndPreservesBackup(string position)
        {
            var store=PrepareTwoVersions();byte[] backup=File.ReadAllBytes(path+".backup");
            byte[] bytes=File.ReadAllBytes(path);var writeTime=File.GetLastWriteTimeUtc(path);
            if(position=="truncated")Array.Resize(ref bytes,bytes.Length-1);
            else {int index=position=="first"?0:position=="last"?bytes.Length-1:bytes.Length/2;bytes[index]^=127;}
            File.WriteAllBytes(path,bytes);File.SetLastWriteTimeUtc(path,writeTime);
            // An independently constructed reader must reject the changed active file.
            var independent=new AtomicSaveStore(path,config);Assert.That(independent.Load().actionSequence,Is.EqualTo(first.actionSequence));
            Assert.That(independent.LastLoadNotice,Does.Contain("\u6709\u6548\u5907\u4efd"));
            store.Save(third);AssertRecoveredWrite(store,backup,bytes);
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
            else broken.lastSubmission.breakdown.score=ExactScoreMath.Add(broken.lastSubmission.breakdown.score,"1");
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
            if (kind == "schema")
            {
                // v1.1 rewrite (docs/specs/v11/RULES_DELTA.md): a declared schema other than the current one is another
                // version's save, not damage of this version: both files are deleted and nothing is reported.
                Assert.That(store.LastLoadNotice, Is.Null); Assert.That(store.LastIncompatibleSave, Is.Null);
                Assert.That(File.Exists(path), Is.False); Assert.That(File.Exists(path + ".backup"), Is.False);
                Assert.That(Directory.GetFiles(directory, "*.legacy-schema*"), Is.Empty);
                return;
            }
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
            // v1.1 rewrite (RULES_DELTA): another version's active file is deleted by Load instead of being quarantined.
            if (kind == "schema") { Assert.That(store.LastDeletedSaveFiles, Is.EqualTo(Full(path))); AssertOverwrittenWithoutQuarantine(store, backup); }
            else AssertRecoveredWrite(store, backup, rejected);
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
            // v1.1 rewrite (RULES_DELTA): another version's active file is overwritten, never quarantined or kept as backup.
            if (kind == "schema") AssertOverwrittenWithoutQuarantine(freshAdapter, backup);
            else AssertRecoveredWrite(freshAdapter, backup, rejected);
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
        /// <summary>v1.1: the valid backup is untouched, the new state is active, and no copy of the old-version file exists.</summary>
        private void AssertOverwrittenWithoutQuarantine(AtomicSaveStore store, byte[] backup)
        {
            CollectionAssert.AreEqual(backup, File.ReadAllBytes(path + ".backup"), "Another version's active file must never replace the valid backup.");
            var accepted = store.Load();
            Assert.That(accepted, Is.Not.Null, store.LastLoadNotice);
            Assert.That(JsonUtility.ToJson(accepted), Is.EqualTo(JsonUtility.ToJson(third)));
            Assert.That(Directory.GetFiles(directory, "active-run.json.rejected-*"), Is.Empty);
            Assert.That(Directory.GetFiles(directory, "*.legacy-schema*"), Is.Empty);
        }
        private static void SetVersion(string file, int schema, string version)
        {
            var e = JsonUtility.FromJson<Envelope>(File.ReadAllText(file)); e.schema = schema; e.config = version;
            File.WriteAllText(file, JsonUtility.ToJson(e));
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
