using System;
using System.Linq;
using NUnit.Framework;
using DiceCraft.Core;

namespace DiceCraft.Tests
{
    public sealed class FlowTests : SessionTestSupport
    {
        [Test] public void FLOW_A01_InitialStateAndNoSubmitBeforeRoll()
        {
            var s = New(); Assert.That(s.State.phase, Is.EqualTo("StagePreview")); Do(s, CommandKind.StartStage);
            Assert.That(s.State.dice.Count, Is.EqualTo(5)); Assert.That(s.State.stage.capacity, Is.EqualTo(5));
            Assert.That(s.State.stage.remainingSubmissions, Is.EqualTo(3)); Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(3));
            Assert.That(s.Execute(Command(s, CommandKind.Submit)).accepted, Is.False);
        }
        [Test] public void FLOW_A02_A03_A04_OrderedIrreversibleLocks()
        {
            var s = Ready(); Do(s, CommandKind.Roll); Lock(s, "D003", "D001");
            Assert.That(s.State.hand.orderedLockedIds, Is.EqualTo(new[] { "D003", "D001" })); Assert.That(s.TogglePending("D003"), Is.False);
            var before = s.State;
            Do(s, CommandKind.Reorder, c => c.dieIds = new[] { "D001", "D003" });
            Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(before.hand.rollsRemaining)); Assert.That(s.State.diceRngState, Is.EqualTo(before.diceRngState));
            var bad = Command(s, CommandKind.Reorder); bad.dieIds = new[] { "D001", "D002" }; before = s.State;
            Assert.That(s.Execute(bad).accepted, Is.False); AssertUnchanged(before, s.State);
        }
        [Test] public void FLOW_A05_PendingBlocksEveryPersistentActionExceptConfirm()
        {
            var s = Ready(); Do(s, CommandKind.Roll); Assert.That(s.TogglePending("D001"), Is.True);
            var before = s.State;
            foreach (var kind in new[] { CommandKind.Roll, CommandKind.Submit, CommandKind.Reorder, CommandKind.UseItem })
                Assert.That(s.Execute(Command(s, kind)).accepted, Is.False, kind.ToString());
            AssertUnchanged(before, s.State); s.ClearPending(); Assert.That(s.PendingIds, Is.Empty);
        }
        [Test] public void FLOW_A06_OverCapacityRejectsWholeBatch()
        {
            var s = Ready("C09"); Do(s, CommandKind.Roll); Lock(s, "D001", "D002", "D003");
            s.TogglePending("D004"); s.TogglePending("D005"); var before = s.State;
            Assert.That(s.Execute(Command(s, CommandKind.ConfirmLocks)).accepted, Is.False); AssertUnchanged(before, s.State);
            Assert.That(s.PendingIds.Length, Is.EqualTo(2));
        }
        [Test] public void FLOW_A07_TemporaryDiceDoNotExpandCapacity()
        {
            string item = null; var s = Edit(Ready("C09"), x => item = AddItem(x, "I02"));
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = item);
            Assert.That(s.State.dice.Count, Is.EqualTo(6)); Assert.That(s.State.stage.capacity, Is.EqualTo(4)); Assert.That(s.State.baseSubmitCapacity, Is.EqualTo(5));
        }
        [Test] public void FLOW_A08_ControllerBudgetsCombine()
        {
            var s = Edit(New(), x => { Equip(x, "C01", "C02", "C03"); x.controllers[0].enchantmentId = "submit1"; x.controllers[1].enchantmentId = "submit1"; x.controllers[2].enchantmentId = "roll1"; });
            Do(s, CommandKind.StartStage); Assert.That(s.State.stage.remainingSubmissions, Is.EqualTo(5)); Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(4));
        }
        [Test] public void FLOW_A09_A11_C05OnlyAfterRollOnceAndSurvivesLoad()
        {
            var s = Ready("C05"); Do(s, CommandKind.Roll); Lock(s, "D001", "D002", "D003"); Do(s, CommandKind.Roll); Do(s, CommandKind.Roll);
            Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(3)); Assert.That(s.State.hand.c05Used, Is.True);
            s = new GameSession(cfg, new MemorySaveStore(), s.State);
            for (int i = 0; i < 3; i++) Do(s, CommandKind.Roll);
            Assert.That(s.State.hand.rollsRemaining, Is.Zero); Assert.That(s.State.hand.c05Used, Is.True);
        }
        [Test] public void FLOW_A10_LockingAfterLastRollDoesNotGrantC05()
        {
            var s = Ready("C05"); Do(s, CommandKind.Roll); Lock(s, "D001", "D002"); Do(s, CommandKind.Roll); Do(s, CommandKind.Roll); Lock(s, "D003");
            Assert.That(s.State.hand.rollsRemaining, Is.Zero); Assert.That(s.State.hand.c05Used, Is.False);
        }
        [Test] public void FLOW_A12_ExtraRollAndSubmissionPersistForStage()
        {
            string a = null, b = null;
            var s = Edit(Selecting(new[] { 1,2,3,4,5 }, controllers: new[]{"C05"}, lockedCount: 1), x => { a = AddItem(x, "I06"); b = AddItem(x, "I07"); x.hand.c05UsedEmitterIds = x.controllers.Where(c => c.definitionId == "C05").Select(c => c.instanceId).ToList(); });
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = a); Do(s, CommandKind.UseItem, c => c.itemInstanceId = b);
            Assert.That(s.State.stage.remainingSubmissions, Is.EqualTo(4)); Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(1)); Assert.That(s.State.hand.c05Used, Is.True);
            Do(s, CommandKind.Submit); Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(4)); Assert.That(s.State.hand.c05Used, Is.False);
        }
        [Test] public void FLOW_C01_C02_CopyUsesFrozenScoreAndDeepCopies()
        {
            var s = Selecting(new[] { 1,2 }, new[] { "red","blue" }, new[] { "C07" });
            s = Edit(s, x => { x.dice[0].faces[2].permanentPip = 6; x.dice[0].faces[3].isGold = true; x.dice[0].isGlass = true; });
            var before = s.PreviewScore(); Do(s, CommandKind.Submit);
            Assert.That(s.State.lastSubmission.breakdown.score, Is.EqualTo(before.score));
            var r = s.State; Assert.That(r.dice[1].dieId, Is.EqualTo("D002")); Assert.That(r.dice[1].isGlass, Is.True);
            Assert.That(r.dice[1].faces[2].permanentPip, Is.EqualTo(6)); Assert.That(r.dice[1].faces[3].isGold, Is.True);
            r.dice[0].faces[2].permanentPip = 1; Assert.That(r.dice[1].faces[2].permanentPip, Is.EqualTo(6));
        }
        [Test] public void FLOW_C03_C04_CopyDoesNotTriggerLaterOrOnEffectivePair()
        {
            var s = Selecting(new[] { 1,2 }, controllers: new[] { "C07", "C12" }); Do(s, CommandKind.Submit);
            Assert.That(s.State.lastEffects.Any(x => x.kind == "DieCopied"), Is.False);
            s = Selecting(new[] { 1,2 }, controllers: new[] { "C07" }); s = Edit(s, x => x.stage.submissionOrdinal = 1);
            Do(s, CommandKind.Submit); Assert.That(s.State.lastEffects.Any(x => x.kind == "DieCopied"), Is.False);
        }
        [Test] public void FLOW_C05_C06_DiffusionUsesPhysicalFaceAfterScoring()
        {
            var s = Selecting(new[] { 1,2,3 }, new[] { "red","blue","red" }, new[] { "C15" });
            s = Edit(s, x => x.hand.currentResults[1].currentFaceId = "F02"); var before = s.PreviewScore();
            Do(s, CommandKind.Submit); Assert.That(s.State.lastSubmission.breakdown.score, Is.EqualTo(before.score)); Assert.That(s.State.dice[1].faces[1].baseColor, Is.EqualTo("red"));
            s = Selecting(new[] { 1,2,3 }, new[] { "red","blue","red" }, new[] { "C15" });
            s = Edit(s, x => { x.hand.redWave = true; x.hand.currentResults[1].currentFaceId = "F02"; }); Do(s, CommandKind.Submit);
            Assert.That(s.State.dice[1].faces[1].baseColor, Is.EqualTo("blue")); Assert.That(s.State.lastEffects.Any(x => x.kind == "FaceDyed"), Is.False);
        }
        [Test] public void FLOW_C07_RemoteAllowsAbsentPipAndPreservesPhysicalFace()
        {
            string item = null; var s = Edit(Ready(), x => { foreach (var f in x.dice[0].faces) f.permanentPip = 1; item = AddItem(x, "I03"); });
            Do(s, CommandKind.UseItem, c => { c.itemInstanceId = item; c.dieIds = new[] { "D001", "D002" }; c.targetPips = new[] { 6,2 }; });
            Assert.That(s.State.hand.currentResults[0].currentPip, Is.EqualTo(6)); Assert.That(s.State.hand.currentResults[0].remoteOverride, Is.True);
            Assert.That(s.State.dice[0].faces.All(f => f.permanentPip == 1), Is.True); Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(2));
            Do(s, CommandKind.Roll); Assert.That(s.State.hand.currentResults[0].currentPip, Is.EqualTo(1)); Assert.That(s.State.hand.currentResults[0].remoteOverride, Is.False);
        }
        [Test] public void FLOW_C08_RemoteRejectsBadTargetsAtomically()
        {
            string item = null; var s = Edit(Ready(), x => item = AddItem(x, "I03"));
            foreach (var ids in new[] { new[] { "D001", "D001" }, new[] { "D001" }, new[] { "D001", "Missing" } })
            {
                var c = Command(s, CommandKind.UseItem); c.itemInstanceId = item; c.dieIds = ids; c.targetPips = new[] { 6,6 }; var before = s.State;
                Assert.That(s.Execute(c).accepted, Is.False); AssertUnchanged(before, s.State);
            }
        }
        [Test] public void FLOW_C09_C11_BrokenRemoteTargetsConsumeItemAndNoDiceFails()
        {
            uint seed = 1;
            while (true) { var r = new GameRng(seed); if (r.NextIndex(100) < 15 && r.NextIndex(100) < 15) break; seed++; }
            string item = null; var s = Edit(Ready("C05"), x => { x.dice.RemoveRange(2,3); x.hand.currentResults.RemoveRange(2,3); foreach (var d in x.dice) d.isGlass = true; x.effectRngState = seed; x.hand.rollsRemaining = 1; item = AddItem(x, "I03"); });
            var oldDiceRng = s.State.diceRngState;
            Do(s, CommandKind.UseItem, c => { c.itemInstanceId = item; c.dieIds = new[] { "D001","D002" }; c.targetPips = new[] { 6,6 }; });
            Assert.That(s.State.phase, Is.EqualTo("RunFailed")); Assert.That(s.State.dice, Is.Empty); Assert.That(s.State.inventory, Is.Empty);
            Assert.That(s.State.hand.c05Used, Is.False); Assert.That(s.State.diceRngState, Is.EqualTo(oldDiceRng));
        }
        [Test] public void FLOW_C10_LockedGlassNeverRollsOrBreaks()
        {
            var s = Ready(); Do(s, CommandKind.Roll); Lock(s, "D001");
            s = Edit(s, x => x.dice[0].isGlass = true); var before = s.State;
            Do(s, CommandKind.Roll); Do(s, CommandKind.Roll);
            Assert.That(s.State.dice.Any(x => x.dieId == "D001"), Is.True); Assert.That(s.State.effectRngState, Is.EqualTo(before.effectRngState));
            Assert.That(s.State.hand.currentResults[0].currentFaceId, Is.EqualTo(before.hand.currentResults[0].currentFaceId));
        }
        [Test] public void FLOW_C12_TemporaryLimitIsSimultaneousNotHistorical()
        {
            var s = Ready();
            for (int i = 0; i < 3; i++) { string item = null; s = Edit(s, x => { item = AddItem(x, "I02"); x.isDebug = false; }); Do(s, CommandKind.UseItem, c => c.itemInstanceId = item); }
            string denied = null; s = Edit(s, x => { denied = AddItem(x, "I02"); x.isDebug = false; });
            var c0 = Command(s, CommandKind.UseItem); c0.itemInstanceId = denied; Assert.That(s.Execute(c0).accepted, Is.False);
            s = Edit(s, x => { var victim = x.dice.Last().dieId; x.dice.RemoveAll(d => d.dieId == victim); x.hand.currentResults.RemoveAll(r => r.dieId == victim); x.isDebug = false; });
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = denied); Assert.That(s.State.dice.Count(x => x.lifetime == "stage_temporary"), Is.EqualTo(3));
        }
        [TestCase(false)] [TestCase(true)] public void FLOW_C14_C15_SettlementClearsTemporaryDiceWithFinalWinPriority(bool final)
        {
            var s = Selecting(new[] { 4,4,4,4,4 }, new[] { "red","red","red","red","red" });
            s = Edit(s, x => { x.dice.RemoveRange(2,3); x.hand.currentResults.RemoveRange(2,3); x.hand.orderedLockedIds.RemoveRange(2,3);
                if (final) { x.stage.stageIndex = 23; x.stage.stageId = cfg.stages[23].stageId; }
                x.stage.target = "1"; foreach (var d in x.dice) { d.lifetime = "stage_temporary"; d.createdStageId = x.stage.stageId; } });
            Do(s, CommandKind.Submit); Do(s, CommandKind.EndStage); Assert.That(s.State.phase, Is.EqualTo("StageSettlement")); Assert.That(s.State.gold, Is.EqualTo(7)); Assert.That(s.State.dice, Is.Empty);
            Do(s, CommandKind.Continue); Assert.That(s.State.phase, Is.EqualTo(final ? "RunWon" : "RunFailed"));
        }
        [Test] public void FLOW_C13_CopyPreservesTemporaryTargetLifetime()
        {
            var s = Selecting(new[] { 1,2 }, controllers: new[] { "C07" });
            s = Edit(s, x => { x.dice[1].lifetime = "stage_temporary"; x.dice[1].createdStageId = x.stage.stageId; });
            Do(s, CommandKind.Submit); Assert.That(s.State.dice[1].lifetime, Is.EqualTo("stage_temporary"));
        }
        [Test] public void FullSlotsWithUnlockedDiceCanStillRollButAllLockedCannot()
        {
            var s = Ready("C09"); Do(s, CommandKind.Roll); Lock(s, "D001","D002","D003","D004"); Do(s, CommandKind.Roll);
            Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(1));
            s = Ready(); Do(s, CommandKind.Roll); Lock(s, "D001","D002","D003","D004","D005"); Assert.That(s.Execute(Command(s, CommandKind.Roll)).accepted, Is.False);
        }
        [Test] public void LastSubmissionTargetWinsBeforeExhaustionAndNewHandClearsEffects()
        {
            var s = Selecting(new[] { 4,4,4,4,4 }, new[] { "red","red","red","red","red" });
            s = Edit(s, x => x.stage.remainingSubmissions = 1); Do(s, CommandKind.Submit); Assert.That(s.State.phase, Is.EqualTo("StageSettlement"));
            s = Selecting(new[] { 1 }, new[] { "red" }); s = Edit(s, x => { x.hand.redWave = true; x.hand.largeBuff = true; x.hand.smallBuff = true; x.hand.c05UsedEmitterIds = x.controllers.Where(c => c.definitionId == "C05").Select(c => c.instanceId).ToList(); });
            Do(s, CommandKind.Submit); Assert.That(s.State.phase, Is.EqualTo("HandReady")); Assert.That(s.State.hand.redWave || s.State.hand.largeBuff || s.State.hand.smallBuff || s.State.hand.c05Used, Is.False);
        }
    }
}
