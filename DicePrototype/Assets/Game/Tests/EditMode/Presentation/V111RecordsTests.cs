using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;
using DiceCraft.Core;
using NUnit.Framework;

namespace DiceCraft.Presentation.Tests
{
    /// <summary>
    /// v1.1.1 task W7b (RULES_DELTA 12.1 last row and 13 row 4): the formal all-time highest hand kept outside a run
    /// (FormalRecordStore) and how a run's own records read (RunRecords111). Pure logic: no engine type, so the same
    /// tests run in the .NET presentation host and in Unity's EditMode runner.
    /// </summary>
    public sealed class V111RecordsTests
    {
        private static string NewFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "dicecraft-w7b-records-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder); return folder;
        }
        private static void Remove(string folder) { try { Directory.Delete(folder, true); } catch (IOException) { } catch (UnauthorizedAccessException) { } }
        private static string Record(string folder) => Path.Combine(folder, FormalRecordStore.FileName);
        private static RunState Run(string highest, bool isDebug = false, bool debugWorkspace = false)
            => new RunState { runId = "run", highestHandScore = highest, isDebug = isDebug, debugWorkspace = debugWorkspace };

        // ---------------------------------------------------------------- the formal record file
        [Test] public void RECORD111_MissingFileIsAnEmptyRecordAndNothingIsWrittenByReading()
        {
            string folder = NewFolder();
            try
            {
                var store = new FormalRecordStore(Record(folder));
                Assert.That(store.HistoricalHighestHandScore, Is.EqualTo(BigInteger.Zero));
                Assert.That(store.LastLoadIssue, Is.Null);
                Assert.That(Directory.GetFiles(folder), Is.Empty, "Loading must not create a file.");
            }
            finally { Remove(folder); }
        }

        [Test] public void RECORD111_AFormalRunRaisesTheRecordAndItSurvivesANewStore()
        {
            string folder = NewFolder();
            try
            {
                var store = new FormalRecordStore(Record(folder));
                Assert.That(store.NoteRun(Run("1500")), Is.True);
                Assert.That(store.HistoricalHighestHandScore, Is.EqualTo(new BigInteger(1500)));
                Assert.That(File.Exists(Record(folder)), Is.True);
                Assert.That(File.Exists(Record(folder) + ".tmp"), Is.False, "The temporary file is swapped in, not left behind.");
                var again = new FormalRecordStore(Record(folder));
                Assert.That(again.HistoricalHighestHandScore, Is.EqualTo(new BigInteger(1500)));
                Assert.That(again.LastLoadIssue, Is.Null);
            }
            finally { Remove(folder); }
        }

        [Test] public void RECORD111_OnlyAHigherHandRaisesItAndNotingTwiceChangesNothing()
        {
            string folder = NewFolder();
            try
            {
                var store = new FormalRecordStore(Record(folder));
                store.NoteRun(Run("900"));
                byte[] written = File.ReadAllBytes(Record(folder));
                Assert.That(store.NoteRun(Run("900")), Is.False, "The same run noted again is the same maximum.");
                Assert.That(store.NoteRun(Run("899")), Is.False);
                Assert.That(store.NoteRun(Run("0")), Is.False, "A new run starts at 0 and never resets the record.");
                Assert.That(store.HistoricalHighestHandScore, Is.EqualTo(new BigInteger(900)));
                CollectionAssert.AreEqual(written, File.ReadAllBytes(Record(folder)), "Nothing is rewritten when the record does not rise.");
                Assert.That(store.NoteRun(Run("901")), Is.True);
                Assert.That(new FormalRecordStore(Record(folder)).HistoricalHighestHandScore, Is.EqualTo(new BigInteger(901)));
                Assert.That(File.Exists(Record(folder) + ".backup"), Is.True, "The previous file is kept as the backup.");
                Assert.That(FormalRecordStore.TryParseRecord(File.ReadAllText(Record(folder) + ".backup"), out var previous), Is.True);
                Assert.That(previous, Is.EqualTo(new BigInteger(900)));
            }
            finally { Remove(folder); }
        }

        [Test] public void RECORD111_DebugRunsDebugWorkspacesAndPracticeNeverWrite()
        {
            string folder = NewFolder();
            try
            {
                var store = new FormalRecordStore(Record(folder));
                Assert.That(store.NoteRun(Run("5000", isDebug: true)), Is.False);
                Assert.That(store.NoteRun(Run("5000", debugWorkspace: true)), Is.False);
                Assert.That(store.NoteRun(Run("5000", isDebug: true, debugWorkspace: true)), Is.False);
                Assert.That(store.NoteRun(Run("5000"), practice: true), Is.False);
                Assert.That(store.NoteRun(null), Is.False);
                Assert.That(store.HistoricalHighestHandScore, Is.EqualTo(BigInteger.Zero));
                Assert.That(Directory.GetFiles(folder), Is.Empty, "A debug source must not create or change the record file.");
                store.NoteRun(Run("120"));
                byte[] formal = File.ReadAllBytes(Record(folder));
                store.NoteRun(Run("999999", debugWorkspace: true));
                Assert.That(store.HistoricalHighestHandScore, Is.EqualTo(new BigInteger(120)));
                CollectionAssert.AreEqual(formal, File.ReadAllBytes(Record(folder)));
            }
            finally { Remove(folder); }
        }

        [Test] public void RECORD111_AnOlderRecordFileWithoutTheFieldReadsAsZeroAndKeepsItsOtherFields()
        {
            string folder = NewFolder();
            try
            {
                File.WriteAllText(Record(folder), "{\"schema\":1,\"runsFinished\":7}", new UTF8Encoding(false));
                var store = new FormalRecordStore(Record(folder));
                Assert.That(store.HistoricalHighestHandScore, Is.EqualTo(BigInteger.Zero));
                Assert.That(store.LastLoadIssue, Is.Null, "A file without the field is an older record, not damage.");
                Assert.That(store.NoteRun(Run("321")), Is.True);
                string text = File.ReadAllText(Record(folder));
                Assert.That(text, Does.Contain("\"runsFinished\":7"), "Fields this version does not know stay in the file.");
                Assert.That(text, Does.Contain("\"historicalHighestHandScore\":\"321\""));
                Assert.That(new FormalRecordStore(Record(folder)).HistoricalHighestHandScore, Is.EqualTo(new BigInteger(321)));
                // An empty object is the smallest older file.
                File.WriteAllText(Record(folder), "{}", new UTF8Encoding(false));
                var empty = new FormalRecordStore(Record(folder));
                Assert.That(empty.HistoricalHighestHandScore, Is.EqualTo(BigInteger.Zero));
                Assert.That(empty.NoteRun(Run("8")), Is.True);
                Assert.That(new FormalRecordStore(Record(folder)).HistoricalHighestHandScore, Is.EqualTo(new BigInteger(8)));
            }
            finally { Remove(folder); }
        }

        [TestCase("")]
        [TestCase("not json at all")]
        [TestCase("{\"historicalHighestHandScore\":\"12")]
        [TestCase("{\"historicalHighestHandScore\":\"-5\"}")]
        [TestCase("{\"historicalHighestHandScore\":\"007\"}")]
        [TestCase("{\"historicalHighestHandScore\":\"1e9\"}")]
        [TestCase("{\"historicalHighestHandScore\":\"\"}")]
        [TestCase("[1,2,3]")]
        public void RECORD111_ADamagedFileDoesNotThrowAndStartsFromZeroWithoutABackup(string damaged)
        {
            string folder = NewFolder();
            try
            {
                File.WriteAllText(Record(folder), damaged, new UTF8Encoding(false));
                FormalRecordStore store = null;
                Assert.DoesNotThrow(() => store = new FormalRecordStore(Record(folder)));
                Assert.That(store.HistoricalHighestHandScore, Is.EqualTo(BigInteger.Zero));
                Assert.That(store.LastLoadIssue, Is.Not.Null);
                Assert.DoesNotThrow(() => store.NoteRun(Run("40")));
                Assert.That(new FormalRecordStore(Record(folder)).HistoricalHighestHandScore, Is.EqualTo(new BigInteger(40)), "The next formal record replaces the damaged file.");
                Assert.That(new FormalRecordStore(Record(folder)).LastLoadIssue, Is.Null);
            }
            finally { Remove(folder); }
        }

        [Test] public void RECORD111_ADamagedFileFallsBackToASoundBackupAndDoesNotReplaceIt()
        {
            string folder = NewFolder();
            try
            {
                var store = new FormalRecordStore(Record(folder));
                store.NoteRun(Run("700")); store.NoteRun(Run("800")); // file 800, backup 700
                File.WriteAllText(Record(folder), "\0\0garbage", new UTF8Encoding(false));
                var recovered = new FormalRecordStore(Record(folder));
                Assert.That(recovered.HistoricalHighestHandScore, Is.EqualTo(new BigInteger(700)), "The sound backup is used.");
                Assert.That(recovered.LastLoadIssue, Is.Not.Null);
                Assert.That(recovered.NoteRun(Run("650")), Is.False, "A lower hand does not lower or rewrite the record.");
                Assert.That(recovered.NoteRun(Run("750")), Is.True);
                Assert.That(FormalRecordStore.TryParseRecord(File.ReadAllText(Record(folder) + ".backup"), out var backup), Is.True, "The damaged file was not rotated into the backup.");
                Assert.That(backup, Is.EqualTo(new BigInteger(700)));
                Assert.That(new FormalRecordStore(Record(folder)).HistoricalHighestHandScore, Is.EqualTo(new BigInteger(750)));
            }
            finally { Remove(folder); }
        }

        [Test] public void RECORD111_AScoreLongerThanSixtyFourBitsIsKeptExactly()
        {
            string folder = NewFolder();
            try
            {
                string huge = "123456789012345678901234567890";
                var store = new FormalRecordStore(Record(folder));
                Assert.That(store.NoteRun(Run(huge)), Is.True);
                Assert.That(new FormalRecordStore(Record(folder)).HistoricalHighestHandScore.ToString(), Is.EqualTo(huge));
                Assert.That(store.Raise("not a number"), Is.False);
                Assert.That(store.Raise(null), Is.False);
                Assert.That(store.HistoricalHighestHandScore.ToString(), Is.EqualTo(huge));
            }
            finally { Remove(folder); }
        }

        [Test] public void RECORD111_AFailedWriteKeepsTheValueInMemoryAndTheNextNoteWritesIt()
        {
            string folder = NewFolder();
            try
            {
                // The record path is an existing directory, so the write cannot succeed.
                string blocked = Path.Combine(folder, "blocked"); Directory.CreateDirectory(Path.Combine(blocked, FormalRecordStore.FileName));
                var store = new FormalRecordStore(Path.Combine(blocked, FormalRecordStore.FileName));
                Assert.DoesNotThrow(() => store.NoteRun(Run("60")));
                Assert.That(store.HistoricalHighestHandScore, Is.EqualTo(new BigInteger(60)), "The session's value is still shown.");
                Assert.That(store.PendingWrite, Is.True);
                Assert.That(store.LastWriteIssue, Is.Not.Null);
                Directory.Delete(Path.Combine(blocked, FormalRecordStore.FileName));
                Assert.That(store.NoteRun(Run("60")), Is.False, "The record did not rise again; the pending value is written.");
                Assert.That(store.PendingWrite, Is.False);
                Assert.That(new FormalRecordStore(Path.Combine(blocked, FormalRecordStore.FileName)).HistoricalHighestHandScore, Is.EqualTo(new BigInteger(60)));
            }
            finally { Remove(folder); }
        }

        // ---------------------------------------------------------------- the run's own records as text
        [TestCase("3", "1", "×3.0")]
        [TestCase("16", "5", "×3.2")]
        [TestCase("13", "5", "×2.6")]
        [TestCase("329", "100", "×3.2")]   // 3.29 is written 3.2: rounded down, never up to a ratio not reached
        [TestCase("399", "100", "×3.9")]
        [TestCase("1", "1", "×1.0")]
        [TestCase("1", "3", "×0.3")]
        [TestCase("123456", "1", "×123,456.0")]
        public void RECORD111_ClearRatioIsWrittenWithOneDecimalRoundedDown(string numerator, string denominator, string expected)
        {
            Assert.That(RunRecords111.ClearRatio(numerator, denominator), Is.EqualTo(expected));
        }

        [TestCase("0", "1")]
        [TestCase("", "1")]
        [TestCase(null, null)]
        [TestCase("5", "0")]
        [TestCase("x", "2")]
        public void RECORD111_ClearRatioIsAbsentBeforeTheFirstClearedStageOrWhenUnreadable(string numerator, string denominator)
        {
            Assert.That(RunRecords111.ClearRatio(numerator, denominator), Is.Null);
        }

        [Test] public void RECORD111_SpecExampleNineHundredOverThreeHundredThenThirteenHundredOverFiveHundredStaysThree()
        {
            // RULES_DELTA 13 row 4: first 900/300, then 1300/500; the highest clear ratio is still 3.
            var state = new RunState { highestClearRatioNumerator = "3", highestClearRatioDenominator = "1" };
            Assert.That(RunRecords111.ClearRatio(state), Is.EqualTo("×3.0"));
            Assert.That(RunRecords111.ClearRatio("1300", "500"), Is.EqualTo("×2.6"));
        }

        [Test] public void RECORD111_MarksComeFromTheStageFlagAndTheSettlementReceipt()
        {
            var state = new RunState { stage = new StageState { handRecordBroken = true }, lastStageSettlement = new StageSettlement { settlementId = "S1" } };
            Assert.That(RunRecords111.HandRecordBroken(state), Is.True);
            Assert.That(RunRecords111.ClearRatioBroken(state), Is.False, "No receipt carries this settlement.");
            state.processedReceipts.Add(new CommandReceipt { settlement = new StageSettlement { settlementId = "S0" }, effects = { new EffectRecord { kind = "ClearRatioRecord" } } });
            Assert.That(RunRecords111.ClearRatioBroken(state), Is.False, "Another stage's record is not this stage's.");
            state.processedReceipts.Add(new CommandReceipt { settlement = new StageSettlement { settlementId = "S1" }, effects = { new EffectRecord { kind = "HandRecordBroken" } } });
            Assert.That(RunRecords111.ClearRatioBroken(state), Is.False, "An equal or lower ratio records nothing.");
            state.processedReceipts.Last().effects.Add(new EffectRecord { kind = "ClearRatioRecord", detail = "0/1:3/1" });
            Assert.That(RunRecords111.ClearRatioBroken(state), Is.True);
            state.stage.handRecordBroken = false;
            Assert.That(RunRecords111.HandRecordBroken(state), Is.False);
            Assert.That(RunRecords111.HandRecordBroken(null), Is.False);
            Assert.That(RunRecords111.ClearRatioBroken(null), Is.False);
        }
    }
}
