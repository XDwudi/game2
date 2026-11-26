using System;
using System.Globalization;
using System.IO;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using DiceCraft.Core;

namespace DiceCraft
{
    /// <summary>
    /// v1.1.1 (task W7b, RULES_DELTA 12.1 last row): the formal record kept outside a run. One value so far, the
    /// all-time highest score of a single hand (historicalHighestHandScore). It is not part of RunState and a new run
    /// does not reset it. It only ever rises: every note takes the maximum, so noting the same run twice changes
    /// nothing. Debug workspaces, debug runs and practice are ignored.
    ///
    /// The file sits beside the display settings and is written the same way: a complete temporary file, flushed,
    /// then swapped in with the previous file kept as ".backup". A file without the field (an older record file)
    /// reads as 0 and keeps its other content when the field is added. A damaged file never throws: the backup is
    /// used when it is sound, otherwise the record starts at 0, and the damaged file is not rotated into the backup.
    /// No engine type is used, so the same code runs in the .NET validation host.
    /// </summary>
    public sealed class FormalRecordStore
    {
        public const string FileName = "formal-records.json";
        public const string Field = "historicalHighestHandScore";
        private static readonly Regex FieldPattern = new Regex("\"" + Field + "\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.CultureInvariant);
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        public string RecordPath { get; }
        /// <summary>The all-time highest score of a single hand of formal runs; 0 before the first one.</summary>
        public BigInteger HistoricalHighestHandScore { get; private set; }
        /// <summary>What the last load found wrong (for the log only), or null.</summary>
        public string LastLoadIssue { get; private set; }
        /// <summary>What the last failed write reported (for the log only), or null.</summary>
        public string LastWriteIssue { get; private set; }
        /// <summary>The value in memory is higher than the file: a write failed and the next note tries again.</summary>
        public bool PendingWrite { get; private set; }
        private bool mainSound;

        public FormalRecordStore(string path)
        {
            RecordPath = path;
            Load();
        }

        /// <summary>Reads the file again. Never throws; a missing file is an empty record.</summary>
        public void Load()
        {
            LastLoadIssue = null; mainSound = true; PendingWrite = false;
            HistoricalHighestHandScore = BigInteger.Zero;
            if (string.IsNullOrEmpty(RecordPath)) return;
            if (TryReadFile(RecordPath, out var value, out bool exists, out string issue)) { HistoricalHighestHandScore = value; return; }
            if (!exists) { if (TryReadFile(RecordPath + ".backup", out value, out bool backupExists, out _) && backupExists) HistoricalHighestHandScore = value; return; }
            mainSound = false; LastLoadIssue = issue;
            if (TryReadFile(RecordPath + ".backup", out value, out _, out _)) HistoricalHighestHandScore = value;
        }

        /// <summary>
        /// Takes the highest hand of a run into the record. Only a formal run counts: a debug workspace, a debug run
        /// or practice changes nothing and writes nothing. Returns true when the record rose.
        /// </summary>
        public bool NoteRun(RunState state, bool practice = false)
        {
            if (state == null || practice || state.isDebug || state.debugWorkspace) return false;
            return Raise(state.highestHandScore);
        }

        /// <summary>Raises the record to this score when it is higher (a canonical decimal string). Returns true when it rose.</summary>
        public bool Raise(string score)
        {
            if (!TryParseScore(score, out var value)) return false;
            bool rose = value > HistoricalHighestHandScore;
            if (rose) HistoricalHighestHandScore = value;
            if (rose || PendingWrite) Persist();
            return rose;
        }

        /// <summary>A canonical decimal: digits only, no sign, no leading zero except "0" itself.</summary>
        public static bool TryParseScore(string text, out BigInteger value)
        {
            value = BigInteger.Zero;
            if (string.IsNullOrEmpty(text) || (text.Length > 1 && text[0] == '0')) return false;
            foreach (char c in text) if (c < '0' || c > '9') return false;
            return BigInteger.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value);
        }

        /// <summary>
        /// The value a record file's text holds. A JSON object without the field is an older record: 0. Anything
        /// that is not an object, or a field that is not a canonical decimal, is damage.
        /// </summary>
        public static bool TryParseRecord(string text, out BigInteger value)
        {
            value = BigInteger.Zero;
            string body = (text ?? "").Trim().TrimStart('﻿').Trim();
            if (body.Length < 2 || body[0] != '{' || body[body.Length - 1] != '}') return false;
            var match = FieldPattern.Match(body);
            return !match.Success || TryParseScore(match.Groups[1].Value, out value);
        }

        private static bool TryReadFile(string path, out BigInteger value, out bool exists, out string issue)
        {
            value = BigInteger.Zero; exists = false; issue = null;
            try
            {
                if (!File.Exists(path)) return false;
                exists = true;
                if (TryParseRecord(File.ReadAllText(path, Encoding.UTF8), out value)) return true;
                issue = "record file is damaged: " + path; return false;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is NotSupportedException || e is ArgumentException)
            { exists = true; issue = e.Message; return false; }
        }

        /// <summary>The file text with the field set: other fields of a sound existing file stay as they are.</summary>
        private string Compose()
        {
            string field = "\"" + Field + "\":\"" + HistoricalHighestHandScore.ToString(CultureInfo.InvariantCulture) + "\"";
            string existing = null;
            try { if (mainSound && File.Exists(RecordPath)) existing = File.ReadAllText(RecordPath, Encoding.UTF8).Trim().TrimStart('﻿').Trim(); }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException) { existing = null; }
            if (existing != null && TryParseRecord(existing, out _))
            {
                if (FieldPattern.IsMatch(existing)) return FieldPattern.Replace(existing, field, 1) + "\n";
                string rest = existing.Substring(1).TrimStart();
                return "{" + field + (rest == "}" ? "" : ",") + rest + "\n";
            }
            return "{\"schema\":1," + field + "}\n";
        }

        private void Persist()
        {
            LastWriteIssue = null;
            if (string.IsNullOrEmpty(RecordPath)) return;
            try
            {
                string folder = Path.GetDirectoryName(RecordPath);
                if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
                byte[] data = Utf8.GetBytes(Compose());
                string temp = RecordPath + ".tmp";
                using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                { file.Write(data, 0, data.Length); file.Flush(true); }
                // A damaged file is replaced without becoming the backup, so a sound backup survives it.
                if (File.Exists(RecordPath)) File.Replace(temp, RecordPath, mainSound ? RecordPath + ".backup" : null, true);
                else File.Move(temp, RecordPath);
                mainSound = true; PendingWrite = false;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException || e is NotSupportedException)
            { PendingWrite = true; LastWriteIssue = e.Message; }
        }
    }
}
