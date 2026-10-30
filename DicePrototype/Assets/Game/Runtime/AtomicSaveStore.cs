using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DiceCraft.Core;
using UnityEngine;

namespace DiceCraft
{
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
        private readonly GameConfig config;
        private readonly GameSession validator;
        public AtomicSaveStore(string path, GameConfig cfg)
        {
            SavePath = path; config = cfg;
            // This private session never receives commands or a restored state. Reuse
            // the exact production validator without cloning all historical receipts
            // merely to decide whether the old active file is a safe backup.
            validator = new GameSession(cfg, new MemorySaveStore());
        }
        public void Save(RunState snapshot)
        {
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
                bool valid = TryRead(SavePath, out _, out _);
                if (!valid)
                {
                    string rejected = SavePath + ".rejected-" + Guid.NewGuid().ToString("N");
                    File.Copy(SavePath, rejected, false);
                }
                File.Replace(temp, SavePath, valid ? SavePath + ".backup" : null, true);
            }
            else File.Move(temp, SavePath);
        }
        public RunState Load()
        {
            LastLoadNotice = null;
            if (!File.Exists(SavePath) && !File.Exists(SavePath + ".backup")) return null;
            Exception first = null;
            foreach (string candidate in new[] { SavePath, SavePath + ".backup" })
            {
                if (!File.Exists(candidate)) continue;
                if (TryRead(candidate, out RunState state, out Exception error))
                {
                    if (candidate != SavePath) LastLoadNotice = "活动存档不可读，已恢复上一份有效备份。";
                    return state;
                }
                if (first == null) first = error;
            }
            LastLoadNotice = first == null ? "没有可恢复的存档" : first.Message;
            return null;
        }
        private bool TryRead(string path, out RunState state, out Exception error)
        {
            state=null;error=null;
            try
            {
                var e=JsonUtility.FromJson<Envelope>(File.ReadAllText(path,Encoding.UTF8));
                if(e==null||e.schema!=config.saveSchemaVersion||e.config!=config.configVersion)
                {
                    // Version isolation preserves both an old active Run and its previous
                    // backup before future schema-3 saves rotate the normal backup path.
                    if(e!=null && e.schema>0 && e.schema<config.saveSchemaVersion)
                    {
                        string legacy=path+".legacy-schema"+e.schema+"-"+Hash(File.ReadAllText(path,Encoding.UTF8)).Substring(0,16);
                        if(!File.Exists(legacy))File.Copy(path,legacy,false);
                    }
                    throw new InvalidDataException("存档版本不兼容，旧档已保留，请开始新挑战。");
                }
                if(string.IsNullOrEmpty(e.payload)||Hash(e.payload)!=e.sha256)throw new InvalidDataException("存档校验失败");
                state=JsonUtility.FromJson<RunState>(e.payload);
                if(state==null||state.configVersion!=config.configVersion||state.saveSchemaVersion!=config.saveSchemaVersion||
                   string.IsNullOrEmpty(state.runId)||state.dice==null||state.stage==null||state.gold<0)throw new InvalidDataException("存档内容不完整");
                // Unity serializes null nested classes as all-default objects. Normalize
                // only its exact empty placeholder before the first-ever Submit; a real
                // or inconsistent historical record must still fail validation.
                if(state.lastSubmission!=null&&state.processedReceipts!=null
                    &&!state.processedReceipts.Any(r=>r?.command?.kind==CommandKind.Submit)
                    &&JsonUtility.ToJson(state.lastSubmission)==JsonUtility.ToJson(new SubmissionRecord()))
                    state.lastSubmission=null;
                // Use the production invariants before trusting a file as a valid backup.
                validator.ValidateState(state);
                return true;
            }
            catch(Exception ex){state=null;error=ex;return false;}
        }
        private static string Hash(string text)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(text))).Replace("-", "").ToLowerInvariant();
        }
    }
}
