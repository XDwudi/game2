using System;
using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Core.Tests;
using DiceCraft.Presentation;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Presentation.Tests
{
    public sealed class PresentationTests
    {
        private GameConfig cfg;
        [SetUp] public void Setup() { cfg = JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(File.ReadAllText(Path.Combine(Application.dataPath, "Game/Data/P0Config.json")))); cfg.Validate(); }
        private ControllerInstance Card(string id, int serial, string enchant = "none") => new ControllerInstance { definitionId = id, instanceId = "c" + serial, acquiredSerial = serial, enchantmentId = enchant };
        private SubmissionSnapshot Snap(int[] pips, params ControllerInstance[] cards) => new SubmissionSnapshot { runId = "run", handId = "hand", configVersion = cfg.configVersion, capacity = Math.Max(5, pips.Length), equippedControllers = cards, dice = pips.Select((p, i) => new SubmissionDie { dieId = "d" + i, faceId = "F01", currentPip = p, currentColor = "blue", permanentSixFaceSum = 21 }).ToArray() };
        private PresentationReceipt Build(SubmissionSnapshot snapshot, params EffectRecord[] effects)
        {
            var b = ScoreCalculator.Evaluate(snapshot, cfg);
            return PresentationReceiptAdapter.Build(new CommandReceipt { commandId = "receipt", score = b.score, submission = new SubmissionRecord { snapshot = snapshot, breakdown = b }, effects = effects.ToList() },
                new RunState { runId = "run", stage = new StageState { score = "0", target = "1800000" } },
                new RunState { runId = "run", phase = "HandReady", stage = new StageState { score = b.score, target = "1800000" } }, cfg);
        }
        [TestCase("799", "100", 0)] [TestCase("800", "100", 1)] [TestCase("801", "100", 1)]
        [TestCase("3199", "100", 1)] [TestCase("3200", "100", 2)] [TestCase("3201", "100", 2)]
        [TestCase("12799", "100", 2)] [TestCase("12800", "100", 3)] [TestCase("12801", "100", 3)]
        [TestCase("102399", "100", 3)] [TestCase("102400", "100", 4)] [TestCase("102401", "100", 4)]
        [TestCase("819199", "100", 4)] [TestCase("819200", "100", 5)] [TestCase("819201", "100", 5)]
        [TestCase("1", "4", 0)] [TestCase("1", "1", 0)] [TestCase("10000000000000000000000000000000000000000000000000000000000000000000000", "3", 5)]
        public void FX01_ExactTierBoundaries(string numerator, string denominator, int expected) => Assert.That(ScorePresentationTier.Resolve("100", numerator, denominator), Is.EqualTo(expected));
        [Test] public void FX02_ZeroScoreOrMultiplierOverridesBurst()
        { Assert.That(ScorePresentationTier.Resolve("0", "99999999999999999999", "1"), Is.Zero); Assert.That(ScorePresentationTier.Resolve("1", "0", "1"), Is.Zero); Assert.Throws<ArgumentOutOfRangeException>(() => ScorePresentationTier.Resolve("1", "1", "0")); }
        public static IEnumerable GoldenReceipts()
        {
            foreach (var item in GoldenJsonLoader.Read(File.ReadAllText(Path.Combine(Application.dataPath, "Game/Data/GoldenScores.json"))).cases)
                yield return new TestCaseData(item).SetName("ART07_RECEIPT_" + item.id);
        }
        [TestCaseSource(nameof(GoldenReceipts))]
        public void AllFrozenGoldenReceiptsAdaptWithoutMutationOrBudgetOverflow(GoldenCase example)
        {
            var snap = example.Snapshot(cfg); string original = JsonUtility.ToJson(snap); var receipt = Build(snap);
            Assert.That(receipt.ScoreDecimal, Is.EqualTo(example.expected.score)); Assert.That(JsonUtility.ToJson(snap), Is.EqualTo(original));
            Assert.That(receipt.Events.Select(e => e.EventId).Distinct().Count(), Is.EqualTo(receipt.Events.Count));
            Assert.That(PresentationSequence.Create(receipt).TotalDurationMs, Is.LessThanOrEqualTo(3200));
            Assert.That(PresentationSequence.Create(receipt, new PresentationOptions(PresentationSpeed.Fast)).TotalDurationMs, Is.LessThanOrEqualTo(800));
            Assert.That(PresentationSequence.Create(receipt, new PresentationOptions(PresentationSpeed.Instant)).TotalDurationMs, Is.Zero);
        }
        [Test] public void FX06_G147ExactScoreMultiplierFourSourcesNoQualified()
        {
            var snap = Snap(new[] { 6, 6, 6, 6, 6 }, Card("C02", 1), Card("C30", 2), Card("C31", 3), Card("C32", 4));
            snap.handLevels = new[] { new HandLevel { handId = "five_kind", level = 4 } }; snap.colorLevels = new[] { new HandLevel { handId = "all_blue", level = 4 } }; snap.rollsRemaining = 2;
            var r = Build(snap); Assert.That(r.ScoreDecimal, Is.EqualTo("967680")); Assert.That(r.MultiplierNumeratorDecimal, Is.EqualTo("26880")); Assert.That(r.MultiplierDenominatorDecimal, Is.EqualTo("1"));
            Assert.That(r.ChainCount, Is.EqualTo(4)); Assert.That(r.ComboTier, Is.EqualTo(5)); Assert.That(r.QualifiedAfter, Is.False); Assert.That(r.Events.Any(e => e.EffectFamily == "qualified"), Is.False);
        }
        [Test] public void FX03_MultiDieAndOwnEnchantOnlyOneEmitter()
        { var r = Build(Snap(new[] { 1, 1, 1 }, Card("C10", 1, "mult2"))); Assert.That(r.ChainCount, Is.EqualTo(1)); Assert.That(r.Events.Count(e => e.EmitterId == "c1" && e.IsScoring), Is.EqualTo(4)); }
        [Test] public void FX03_PostOnlyAndIdentityFactorsDoNotCount()
        {
            var snap = Snap(new[] { 2, 2 }, Card("C01", 1), Card("C43", 2), Card("C38", 3), Card("C53", 4));
            snap.previousPrimaryHandId = "pair"; snap.handLevels = new[] { new HandLevel { handId = "pair", level = 4 } };
            snap.resolvedRandomChoices = new[] { new RandomMultiplierChoice { ownerInstanceId = "c4", units2 = 2 } };
            var r = Build(snap); Assert.That(r.ChainCount, Is.Zero); Assert.That(r.Events.Count(e => e.EffectFamily == "random_reveal"), Is.EqualTo(1));
            snap.equippedControllers[0].enchantmentId = "base10"; Assert.That(Build(snap).ScoringEmitterIds, Is.EquivalentTo(new[] { "c1" }));
        }
        [Test] public void FX03_C45NoMatchIsHitButNoChainUntilScoringEnchant()
        {
            var card = Card("C45", 1); card.state.activeColor = "red"; var snap = Snap(new[] { 2 }, card);
            Assert.That(Build(snap).ChainCount, Is.Zero); card.enchantmentId = "base10"; Assert.That(Build(snap).ChainCount, Is.EqualTo(1));
        }
        [Test] public void FX03_ZeroAndFractionAreNonIdentityScoringSources()
        { var r = Build(Snap(new[] { 2 }, Card("C49", 1))); Assert.That(r.ChainCount, Is.EqualTo(1)); Assert.That(r.ComboTier, Is.Zero); Assert.That(r.Events.Single(e => e.EffectFamily == "final_score").Detail, Does.Contain("×0")); }
        [Test] public void FX04_SharedHalfRevealOnceBeforeBothFactors()
        {
            var snap = Snap(new[] { 5 }, Card("C41", 1), Card("C53", 2)); snap.resolvedRandomChoices = new[] { new RandomMultiplierChoice { ownerInstanceId = "c2", units2 = 1 } };
            var r = Build(snap); Assert.That(r.ChainCount, Is.EqualTo(2));
            var reveals = r.Events.Where(e => e.EffectFamily == "random_reveal").ToArray(); Assert.That(reveals.Length, Is.EqualTo(1)); Assert.That(reveals[0].SourceEmitterIds.Count, Is.EqualTo(2)); Assert.That(reveals[0].Operand, Is.EqualTo("1/2"));
            var beats = PresentationSequence.Create(r).Beats.Where(b => b.Phase == PresentationPhase.ControllerAndEnchant).ToArray(); Assert.That(beats[0].Events[0].EffectFamily, Is.EqualTo("random_reveal"));
        }
        [Test] public void EffectivePipIdempotentSourcesShareFrozenAttribution()
        {
            var snap = Snap(new[] { 1, 2 }, Card("C41", 1), Card("C12", 2)); var r = Build(snap);
            Assert.That(r.ScoringEmitterIds, Is.EquivalentTo(new[] { "c1", "c2" })); Assert.That(r.Events.Where(e => e.EffectFamily == "effective_pip").All(e => e.BeforeValue == "1" && e.AfterValue == "2"), Is.True);
            snap.dice[0].currentPip = 2; Assert.That(Build(snap).ChainCount, Is.Zero);
        }
        [Test] public void EffectiveRedIdentityAndItemOverrideDoNotFabricateContribution()
        {
            var snap = Snap(new[] { 1 }, Card("C03", 1)); Assert.That(Build(snap).ChainCount, Is.EqualTo(1));
            snap.dice[0].currentColor = "red"; Assert.That(Build(snap).ChainCount, Is.Zero);
            snap.dice[0].currentColor = "blue"; snap.redWave = true; Assert.That(Build(snap).ChainCount, Is.Zero);
        }
        [Test] public void GroupingAndShortStraightOwnershipAreExplicit()
        {
            var map = Snap(new[] { 1, 4 }, Card("C24", 1)); foreach (var d in map.dice) d.currentColor = "red"; Assert.That(Build(map).ChainCount, Is.EqualTo(1));
            var merge = Snap(new[] { 2, 4 }, Card("C25", 1)); foreach (var d in merge.dice) d.currentColor = "red"; Assert.That(Build(merge).ChainCount, Is.EqualTo(1));
            merge.dice[0].currentPip = 4; Assert.That(Build(merge).ChainCount, Is.Zero);
            Assert.That(Build(Snap(new[] { 1, 2, 3, 4 }, Card("C29", 1))).ChainCount, Is.EqualTo(1)); Assert.That(Build(Snap(new[] { 1, 2, 3, 4, 5 }, Card("C29", 1))).ChainCount, Is.Zero);
        }
        [Test] public void AggregatedBaseContributionsPreserveEachFrozenEmitterAndExactSum()
        {
            var snap = Snap(new[] { 2, 2 }, Card("C41", 1), Card("C44", 2)); snap.wallet = 9007199254740993L;
            var r = Build(snap); var adds = r.Events.Where(e => e.EffectFamily == "controller_base").ToArray();
            Assert.That(adds.Length, Is.EqualTo(2)); Assert.That(adds.Select(e => e.EmitterId), Is.EquivalentTo(new[] { "c1", "c2" }));
            Assert.That(adds[0].Operand, Is.EqualTo("18014398509481986")); Assert.That(adds[1].AfterValue, Is.EqualTo("36028797018963976"));
        }
        [Test] public void FX05_PostSourceGroupAndTrueEffectOrderRemainDetached()
        {
            var snap = Snap(new[] { 2, 3 }, Card("C41", 1), Card("C38", 2));
            var effects = new[] { new EffectRecord { kind = "DieCopied", sourceId = "C07", dieId = "d1", otherDieId = "d0" }, new EffectRecord { kind = "FaceValueChanged", sourceId = "C36+C37", dieId = "d0", faceId = "F01", oldValue = 2, newValue = 2 }, new EffectRecord { kind = "FaceDyed", sourceId = "C38", dieId = "d0", faceId = "F01", detail = "blue" } };
            var r = Build(snap, effects); var post = r.Events.Where(e => e.Phase == PresentationPhase.PostScore).ToArray();
            CollectionAssert.AreEqual(effects.Select(e => e.kind), post.Select(e => e.EffectFamily)); Assert.That(post[2].EmitterId, Is.Null); Assert.That(post[2].SourceEmitterIds.Count, Is.EqualTo(2));
            Assert.That(PresentationSequence.Create(r).Beats.Single(b => b.Phase == PresentationPhase.PostScore).DurationMs, Is.LessThanOrEqualTo(480)); effects[2].detail = "red"; Assert.That(post[2].AfterValue, Is.EqualTo("blue"));
        }
        [Test] public void FX07_Debug32RetainsAllSourcesWithFourMainFoci()
        {
            var snap = Snap(new[] { 6 }, Enumerable.Range(1, 32).Select(i => Card("C46", i, "mult2")).ToArray()); snap.debug = true;
            var r = Build(snap); var sequence = PresentationSequence.Create(r); Assert.That(r.ChainCount, Is.EqualTo(32)); Assert.That(sequence.Beats.Count(b => b.FocusEmitterId != null), Is.EqualTo(4));
            Assert.That(sequence.TotalDurationMs, Is.LessThanOrEqualTo(3200)); Assert.That(sequence.Beats.SelectMany(b => b.Events).Count(), Is.EqualTo(r.Events.Count));
        }
        [Test] public void FX08_PauseSkipRestoreReplayNeverReenterGameOrRepeatLiveEvents()
        {
            var r = Build(Snap(new[] { 2 }, Card("C49", 1))); var p = new PresentationPlayer(); int beats = 0, final = 0;
            p.BeatStarted += b => beats++; p.FinalStateRequested += x => final++;
            p.Play(PresentationSequence.Create(r)); int initial = beats; p.Clock.Paused = true; p.Tick(20); Assert.That(beats, Is.EqualTo(initial)); Assert.That(p.Clock.ElapsedMilliseconds, Is.Zero);
            p.Skip(); Assert.That(p.IsPlaying, Is.False); Assert.That(final, Is.EqualTo(1)); Assert.That(r.Events.All(e => p.WasPlayed(r.GameReceiptId, e.EventId)), Is.True);
            Assert.That(p.Play(PresentationSequence.Create(r)), Is.False); Assert.That(beats, Is.EqualTo(initial));
            p.Play(PresentationSequence.Create(r), true); p.Tick(20); Assert.That(beats, Is.GreaterThan(initial)); p.Restore(r); Assert.That(p.IsPlaying, Is.False);
        }
        [Test] public void FX09_SpeedsAndReducedMotionShareExactReceipt()
        {
            var r = Build(Snap(new[] { 6 }, Card("C46", 1)));
            foreach (PresentationSpeed speed in Enum.GetValues(typeof(PresentationSpeed))) foreach (bool reduced in new[] { false, true })
            {
                var sequence = PresentationSequence.Create(r, new PresentationOptions(speed, reduced)); Assert.That(sequence.Receipt, Is.SameAs(r));
                var player = new PresentationPlayer(); PresentationReceipt final = null; player.FinalStateRequested += x => final = x; player.Play(sequence); player.Tick(10); Assert.That(final, Is.SameAs(r));
                if (reduced || speed == PresentationSpeed.Instant) { Assert.That(ScorePresentationTier.ParticleCount(5, sequence.Options), Is.Zero); Assert.That(ScorePresentationTier.ScalePeak(5, sequence.Options), Is.EqualTo(1)); Assert.That(ScorePresentationTier.ShakePixels(5, sequence.Options), Is.Zero); }
            }
        }
        [Test] public void OnlyNewSuccessfullySavedResultMayStartPresentation()
        { foreach (var result in new[] { new CommandResult(), new CommandResult { accepted = true, duplicate = true }, new CommandResult { accepted = true, pendingSave = true }, new CommandResult { accepted = true, noChange = true } }) Assert.Throws<ArgumentException>(() => PresentationReceiptAdapter.BuildCommitted(result, null, null, cfg)); }
        [Test] public void FX11_AudioMapHasSixteenLogicalCuesTwentyFiveFiles()
        {
            Assert.That(AudioCueMap.Cues.Count, Is.EqualTo(16)); Assert.That(AudioCueMap.Cues.SelectMany(c => c.AssetIds).Distinct().Count(), Is.EqualTo(25));
            Assert.That(AudioCueMap.AssetFor("SFX_SCORE_BURST", tier: 2), Is.Null); Assert.That(AudioCueMap.AssetFor("SFX_SCORE_BURST", tier: 5), Is.EqualTo("sfx_score_burst_l5"));
            Assert.That(AudioCueMap.AssetFor("SFX_DYE_COPY", semanticVariant: "DieCopied"), Is.EqualTo("sfx_copy")); Assert.That(AudioCueMap.ScorePitch(100), Is.EqualTo(2).Within(.00001));
            var r = Build(Snap(new[] { 2 }), new EffectRecord { kind = "FundAccrued", sourceId = "C52", detail = "c1", newValue = 3 }); Assert.That(r.Events.Single(e => e.EffectFamily == "FundAccrued").AudioCue, Is.Null);
        }
        [Test] public void FX11_AudioBudgetMergesThrottlesAndReleasesOnlyItsReceipt()
        {
            var b = new AudioVoiceBudget(); Assert.That(b.Admit("SFX_UI_HOVER", "a", "r1", 0, 1), Is.GreaterThanOrEqualTo(0)); Assert.That(b.Admit("SFX_UI_HOVER", "b", "r2", .1, 1), Is.EqualTo(-1));
            for (int i = 0; i < 7; i++) Assert.That(b.Admit("SFX_CARD_TRIGGER", "c" + i, "r2", 0, 1), Is.GreaterThanOrEqualTo(0));
            Assert.That(b.ActiveCount(0), Is.EqualTo(8)); Assert.That(b.Admit("SFX_CARD_TRIGGER", "extra", "r2", 0, 1), Is.GreaterThanOrEqualTo(0), "Higher-priority trigger replaces hover.");
            Assert.That(b.Admit("SFX_CARD_TRIGGER", "extra", "r2", .01, 1), Is.EqualTo(-1)); Assert.That(b.ReleaseReceipt("r1").Length, Is.Zero); Assert.That(b.ActiveCount(.1), Is.EqualTo(8)); Assert.That(b.ReleaseReceipt("r2").Length, Is.EqualTo(8));
        }
        [Test] public void FX07_GlobalFocusAndPoolBudgetsHaveScopedRelease()
        {
            var budget = new PresentationEffectBudget(); var normal = new PresentationOptions();
            Assert.That(budget.TryBeginGroup("a", "main", true), Is.True); Assert.That(budget.TryBeginGroup("b", "main", true), Is.False);
            for (int i = 0; i < 3; i++) Assert.That(budget.TryBeginGroup("b", "aux" + i, false), Is.True);
            Assert.That(budget.TryBeginGroup("b", "overflow", false), Is.False);
            Assert.That(budget.Reserve("a", "main", EffectPoolKind.NumberLabel, 20, normal), Is.EqualTo(8));
            Assert.That(budget.Reserve("b", "aux0", EffectPoolKind.NumberLabel, 1, normal), Is.Zero);
            Assert.That(budget.Reserve("a", "main", EffectPoolKind.Particle, 100, new PresentationOptions(reducedMotion: true)), Is.Zero);
            budget.ReleaseReceipt("a"); Assert.That(budget.ActiveGroups, Is.EqualTo(3));
            Assert.That(budget.Reserve("b", "aux0", EffectPoolKind.NumberLabel, 8, normal), Is.EqualTo(8));
        }
        private sealed class PooledView { public string Owner; public bool Complete; public float Scale; }
        [Test] public void FX07_PoolIsBoundedResetsBindingsAndIgnoresDoubleReturn()
        {
            var pool = new PresentationObjectPool<PooledView>(2, 3, () => new PooledView(), x => { x.Owner = null; x.Complete = false; x.Scale = 1; });
            var a = pool.Rent(); var b = pool.Rent(); var c = pool.Rent(); Assert.That(pool.Rent(), Is.Null); Assert.That(pool.CreatedCount, Is.EqualTo(3));
            a.Owner = "old"; a.Complete = true; a.Scale = 3; pool.Return(a); pool.Return(a);
            Assert.That(pool.LeasedCount, Is.EqualTo(2)); var next = pool.Rent(); Assert.That(next.Owner, Is.Null); Assert.That(next.Complete, Is.False); Assert.That(next.Scale, Is.EqualTo(1));
            Assert.That(pool.Rent(), Is.Null);
        }
        [Test] public void QualifiedStampFollowsRealTransitionAndVictoryOnlyActualResultPhase()
        {
            var before = new RunState { runId = "run", phase = "Selecting", stage = new StageState { score = "0", target = "10" } };
            var after = new RunState { runId = "run", phase = "StageSettlement", stage = new StageState { score = "10", target = "10", qualified = true, settlementContinuation = "RunWon" } };
            var command = new CommandReceipt { commandId = "r", phase = "StageSettlement" }; var receipt = PresentationReceiptAdapter.Build(command, before, after, cfg);
            Assert.That(receipt.Events.Count(e => e.EffectFamily == "qualified"), Is.EqualTo(1)); Assert.That(receipt.Events.Any(e => e.EffectFamily == "run_result"), Is.False);
            before.stage.qualified = true; Assert.That(PresentationReceiptAdapter.Build(command, before, after, cfg).Events.Any(e => e.EffectFamily == "qualified"), Is.False);
            after.phase = "RunWon"; Assert.That(PresentationReceiptAdapter.Build(command, before, after, cfg).Events.Single(e => e.EffectFamily == "run_result").AfterValue, Is.EqualTo("RunWon"));
        }
    }
}
