using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DiceCraft.Core;
using UnityEngine;

namespace DiceCraft
{
    public enum SaveSlotPolicy { Any, Normal, Debug }
    /// <summary>Writes and flushes a complete candidate before replacing the active save.
    /// The core owns retry/idempotency; this adapter never recomputes game results.</summary>
    public sealed class AtomicSaveStore : ISaveStore
    {
        [Serializable] private sealed class Envelope
        {
            public int schema;
            public string config, sha256, payload;
        }
        public string SavePath { get; }
        public string LastLoadNotice { get; private set; }
        public sealed class IncompatibleSaveInfo
        {
            public int Schema { get; }
            public string ConfigVersion { get; }
            public bool IsLegacyPreserved { get; }
            internal IncompatibleSaveInfo(int schema,string version,bool preserved)
            { Schema=schema;ConfigVersion=version;IsLegacyPreserved=preserved; }
        }
        /// <summary>
        /// v1.1 (docs/specs/v11/DEV_PLAN.md 4.5): always null. A save written by another version is deleted on Load
        /// instead of being reported; the property is kept only so that existing callers still compile.
        /// </summary>
        public IncompatibleSaveInfo LastIncompatibleSave { get; private set; }
        /// <summary>
        /// v1.1 diagnostic only: the files the last Load deleted because another version wrote them (active, backup,
        /// earlier legacy copies). Empty when nothing was deleted. Never shown to the player.
        /// </summary>
        public string[] LastDeletedSaveFiles { get; private set; } = Array.Empty<string>();
        private readonly GameConfig config;
        private readonly GameSession validator;
        private readonly SaveSlotPolicy policy;
        private byte[] lastValidatedWrite;
        public AtomicSaveStore(string path, GameConfig cfg, SaveSlotPolicy slotPolicy=SaveSlotPolicy.Any)
        {
            SavePath = path; config = cfg; policy=slotPolicy;
            // This private session never receives commands or a restored state. Reuse
            // the exact production validator without cloning all historical receipts
            // merely to decide whether the old active file is a safe backup.
            validator = new GameSession(cfg, new MemorySaveStore());
        }
        public void Save(RunState snapshot)
        {
            // A successful write alone does not prove validity: callers other than
            // GameSession can use this adapter. Validate before accepting any bytes
            // into the private known-good cache or touching existing files.
            validator.ValidateState(snapshot);
            ValidateSlot(snapshot);
            Directory.CreateDirectory(Path.GetDirectoryName(SavePath));
            string payload = JsonUtility.ToJson(snapshot);
            var envelope = new Envelope { schema = config.saveSchemaVersion, config = config.configVersion,
                payload = payload, sha256 = Hash(payload) };
            byte[] data = Encoding.UTF8.GetBytes(JsonUtility.ToJson(envelope));
            string temp = SavePath + ".tmp";
            using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            { file.Write(data, 0, data.Length); file.Flush(true); }
            if (File.Exists(SavePath))
            {
                // Never replace a good backup with a corrupt/incompatible active file.
                // Validate at write time too: Save is allowed without a preceding Load.
                // The previous envelope usually is the exact byte array this adapter
                // validated and committed. Compare all bytes (never just mtime/length)
                // to avoid parsing the growing receipt history again on every save.
                // External changes and a fresh adapter retain the full original check.
                bool valid = lastValidatedWrite != null && BytesEqual(File.ReadAllBytes(SavePath),lastValidatedWrite);
                if(!valid)valid=TryRead(SavePath, out _, out _);
                // A corrupt file of this version is quarantined as before. v1.1: a file written by another version
                // is simply overwritten; it is neither quarantined nor rotated into the backup.
                if (!valid && !IsOtherVersion(SavePath))
                {
                    string rejected = SavePath + ".rejected-" + Guid.NewGuid().ToString("N");
                    File.Copy(SavePath, rejected, false);
                }
                File.Replace(temp, SavePath, valid ? SavePath + ".backup" : null, true);
            }
            else File.Move(temp, SavePath);
            // Failed Replace/Move must leave the previous cache intact for exact retry.
            lastValidatedWrite=data;
        }
        // Compare every byte while avoiding Mono LINQ enumerator overhead on large saves.
        // Length or timestamps alone never establish that the active file is still validated.
        private static bool BytesEqual(byte[] left, byte[] right)
        {
            if (ReferenceEquals(left,right)) return true;
            if (left==null||right==null||left.Length!=right.Length) return false;
            for (int i=0;i<left.Length;i++) if(left[i]!=right[i]) return false;
            return true;
        }
        public RunState Load()
        {
            LastLoadNotice = null;LastIncompatibleSave=null;
            DeleteOtherVersionSaves();
            Exception first = null; bool current = false;
            foreach (string candidate in new[] { SavePath, SavePath + ".backup" })
            {
                // A file of another version that could not be deleted is ignored: never read and never reported.
                if (!File.Exists(candidate) || IsOtherVersion(candidate)) continue;
                current = true;
                if (TryRead(candidate, out RunState state, out Exception error))
                {
                    if (candidate != SavePath) LastLoadNotice = "存档文件损坏，已改用自动备份（可能回到稍早的进度）。";
                    return state;
                }
                if (first == null) first = error;
            }
            if (!current) return null; // no save of this version: nothing to restore and nothing to report
            LastLoadNotice = first == null ? "没有可恢复的存档" : first.Message;
            return null;
        }
        /// <summary>
        /// v1.1 (DEV_PLAN 4.5): the active save and the backup are deleted when another version wrote them (the envelope
        /// declares a different save schema or configuration version), together with every legacy copy that earlier
        /// versions left next to this slot. Nothing of this version is ever deleted here: a damaged file of this version
        /// keeps the existing backup fallback, notice and quarantine. No notice is raised for a deleted old save.
        /// Deleting is best effort; a file that cannot be deleted now is retried by the next Load.
        /// </summary>
        private void DeleteOtherVersionSaves()
        {
            var deleted = new System.Collections.Generic.List<string>();
            foreach (string candidate in new[] { SavePath, SavePath + ".backup" })
                if (File.Exists(candidate) && IsOtherVersion(candidate) && TryDelete(candidate)) deleted.Add(candidate);
            string folder = Path.GetDirectoryName(SavePath), prefix = Path.GetFileName(SavePath) + ".legacy-schema";
            string[] legacy = Array.Empty<string>();
            try { if (!string.IsNullOrEmpty(folder) && Directory.Exists(folder)) legacy = Directory.GetFiles(folder, prefix + "*"); }
            catch (IOException) { } catch (UnauthorizedAccessException) { }
            // The exact prefix check guards against wildcard matches on short (8.3) file names.
            foreach (string file in legacy.Where(f => Path.GetFileName(f).StartsWith(prefix, StringComparison.Ordinal)).OrderBy(f => f, StringComparer.Ordinal))
                if (TryDelete(file)) deleted.Add(file);
            // Report normalised absolute paths: SavePath may mix '/' and '\' while the folder listing uses '\'.
            LastDeletedSaveFiles = deleted.Select(Path.GetFullPath).ToArray();
        }
        private static bool TryDelete(string file)
        {
            try { File.Delete(file); return !File.Exists(file); }
            catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
        }
        /// <summary>
        /// Whether a file's envelope declares a version other than the current one. An envelope that cannot be read or
        /// declares no version at all is damage, not another version, and is never deleted.
        /// </summary>
        private bool IsOtherVersion(string path)
        {
            try { return DeclaresOtherVersion(JsonUtility.FromJson<Envelope>(File.ReadAllText(path, Encoding.UTF8))); }
            catch (Exception) { return false; }
        }
        private bool DeclaresOtherVersion(Envelope e)
            => e != null && e.schema > 0 && !string.IsNullOrEmpty(e.config) && (e.schema != config.saveSchemaVersion || e.config != config.configVersion);
        private bool TryRead(string path, out RunState state, out Exception error)
        {
            state=null;error=null;
            try
            {
                var e=JsonUtility.FromJson<Envelope>(File.ReadAllText(path,Encoding.UTF8));
                // v1.1: another version's file is never read; Load deletes it first and Save overwrites it.
                if(DeclaresOtherVersion(e))throw new InvalidDataException("存档来自其他版本。");
                if(e==null||e.schema!=config.saveSchemaVersion||e.config!=config.configVersion)throw new InvalidDataException("存档文件已损坏");
                if(string.IsNullOrEmpty(e.payload)||Hash(e.payload)!=e.sha256)throw new InvalidDataException("存档文件已损坏");
                state=JsonUtility.FromJson<RunState>(e.payload);
                if(state==null||state.configVersion!=config.configVersion||state.saveSchemaVersion!=config.saveSchemaVersion||
                   string.IsNullOrEmpty(state.runId)||state.dice==null||state.stage==null||state.gold<0)throw new InvalidDataException("存档文件已损坏");
                // Unity serializes null nested classes as all-default objects. Normalize
                // only its exact empty placeholder before the first-ever Submit; a real
                // or inconsistent historical record must still fail validation.
                if(state.lastSubmission!=null&&state.processedReceipts!=null
                    &&!state.processedReceipts.Any(r=>r?.command?.kind==CommandKind.Submit)
                    &&JsonUtility.ToJson(state.lastSubmission)==JsonUtility.ToJson(new SubmissionRecord()))
                    state.lastSubmission=null;
                // Use the production invariants before trusting a file as a valid backup.
                validator.ValidateState(state);
                ValidateSlot(state);
                return true;
            }
            catch(Exception ex){state=null;error=ex;return false;}
        }
        private void ValidateSlot(RunState state)
        {
            if(policy==SaveSlotPolicy.Normal&&(state.debugWorkspace||state.isDebug))throw new InvalidDataException("调试存档不能作为普通挑战恢复或保存。");
            if(policy==SaveSlotPolicy.Debug&&!state.debugWorkspace)throw new InvalidDataException("普通挑战不能写入调试分区。");
        }
        private static string Hash(string text)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
