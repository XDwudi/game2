using System;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using DiceCraft.Core;

namespace DiceCraft.Tests
{
    /// <summary>
    /// p1.1.1 session behaviour inside one stage (docs/specs/v111/RULES_DELTA.md sections 4.5, 5-7, 10, 13): the frozen
    /// stage score and target, growth execution, the highest hand and the clear ratio, the star pip of C137, glass,
    /// cultivation rewrites and their count, the in-stage part of boss modifiers, save validation and debug scenes.
    /// Every expected number is worked out by hand from the rule text; the arithmetic is in the comment next to it.
    /// Unless a test says otherwise a hand has no roll left (no unused-roll bonus), all levels are 0, and a submission
    /// of mixed colours has no colour bonus: score = floor(base x hand x red group x (1 + bonuses) x factors).
    /// </summary>
    public sealed class V111StageTests : SessionTestSupport
    {
        /// <summary>An effect-stream state whose next draws are the given tickets; arguments are range, ticket pairs.</summary>
        private static uint SeedFor(params int[] rangeTicketPairs)
        {
            for (uint seed = 1; seed < 20000000; seed++)
            {
                var rng = new GameRng(seed); bool match = true;
                for (int i = 0; match && i < rangeTicketPairs.Length; i += 2) match = rng.NextIndex(rangeTicketPairs[i]) == rangeTicketPairs[i + 1];
                if (match) return seed;
            }
            throw new InvalidOperationException("No seed found for the requested tickets.");
        }

        /// <summary>A debug-workspace run that has just entered stage 1 with these cards, after an optional edit.</summary>
        private GameSession Start(string[] controllers, Action<RunState> edit = null)
        {
            var s = Edit(New(), x => { Equip(x, controllers); edit?.Invoke(x); }); Do(s, CommandKind.StartStage); return s;
        }

        /// <summary>
        /// Puts the hand into the selecting phase with real physical faces: die i shows its face F01, whose permanent
        /// pip and base colour are set to the given values (no remote override). The first `locked` dice are locked in
        /// order; the rest stay in the pool showing F01. `dice` trims or extends the dice of the run first.
        /// </summary>
        private GameSession Deal(GameSession s, int[] pips, string[] colors, int locked = -1, int dice = -1, Action<RunState> edit = null) => Edit(s, x =>
        {
            if (dice > 0)
            {
                while (x.dice.Count > dice) x.dice.RemoveAt(x.dice.Count - 1);
                while (x.dice.Count < dice) x.dice.Add(DiceFactory.CreateStandard(cfg, x.nextDieSerial++));
            }
            x.phase = "Selecting"; x.hand.hasRolled = true; x.hand.rollsUsed = 1; x.hand.rollsRemaining = 0; x.hand.orderedLockedIds.Clear(); x.hand.currentResults.Clear();
            int n = locked < 0 ? pips.Length : locked; var row = x.dice.OrderBy(d => d.createdSerial).ToArray();
            for (int i = 0; i < row.Length; i++)
            {
                var face = row[i].faces.First(f => f.faceId == "F01");
                if (i < pips.Length) { face.permanentPip = pips[i]; face.baseColor = colors[i]; }
                x.hand.currentResults.Add(new DieHandState { dieId = row[i].dieId, currentFaceId = "F01", currentPip = face.permanentPip, currentColor = face.baseColor, hasRolled = true, isLocked = i < n });
                if (i < n) x.hand.orderedLockedIds.Add(row[i].dieId);
            }
            edit?.Invoke(x);
        });

        private CommandResult Submit(GameSession s) => Do(s, CommandKind.Submit);
        private static ControllerInstance Card(RunState x, string id, int ordinal = 0) => x.controllers.Where(c => c.definitionId == id).ElementAt(ordinal);
        private static FaceState Face(RunState x, string dieId, string faceId) => x.dice.First(d => d.dieId == dieId).faces.First(f => f.faceId == faceId);
        private static int[] Pips(RunState x, string dieId) => x.dice.First(d => d.dieId == dieId).faces.OrderBy(f => f.faceId, StringComparer.Ordinal).Select(f => f.permanentPip).ToArray();
        private static string[] R(params string[] colors) => colors;

        /// <summary>Clears the running stage: the score is preset one point short of the target, one red 3 is submitted, then the stage is ended.</summary>
        private GameSession Clear(GameSession s, Action<RunState> beforeEnd = null)
        {
            s = Deal(s, new[] { 3 }, R("red"), edit: x => x.stage.score = (BigInteger.Parse(x.stage.target) - 1).ToString());
            Submit(s);
            if (s.State.phase == "StageSettlement") return s;
            if (beforeEnd != null) s = Edit(s, beforeEnd);
            Do(s, CommandKind.EndStage); return s;
        }

        /// <summary>The shop after the given stage (0-based) with a consistent settlement receipt, as in the p1.0 tests.</summary>
        private GameSession ShopAfterStage(int index, Action<RunState> edit = null) => Edit(Shop(), x =>
        {
            var def = cfg.stages[index]; x.stage.stageIndex = index; x.stage.stageId = def.stageId; x.stage.stageKind = def.stageKind; x.stage.target = def.normalTarget; x.stage.score = def.normalTarget;
            x.stage.settlementId = x.runId + "/" + def.stageId; x.lastStageSettlement.settlementId = x.stage.settlementId; x.lastStageSettlement.stageId = def.stageId;
            x.lastStageSettlement.score = x.stage.score; x.lastStageSettlement.target = x.stage.target; x.lastStageSettlement.stageKind = def.stageKind;
            edit?.Invoke(x);
        });
        /// <summary>The stage-select screen of the boss stage `stageIndex` (2, 5, ...) with the given modifier planned for it.</summary>
        private GameSession BossPreview(string modifierId, int stageIndex, Action<RunState> edit, params string[] controllers)
        {
            var s = ShopAfterStage(stageIndex - 1, x => { x.cycles[stageIndex / 3].modifierId = modifierId; Equip(x, controllers); edit?.Invoke(x); });
            Do(s, CommandKind.LeaveShop); return s;
        }
        private GameSession Boss(string modifierId, params string[] controllers) { var s = BossPreview(modifierId, 2, null, controllers); Do(s, CommandKind.StartStage); return s; }

        // ---- 1. the frozen stage score and target (sections 4.5, 11, 12.1) ----

        [TestCase("79", "42", TestName = "V111_C156_Target160_Score79_Triggers")]
        [TestCase("80", "7", TestName = "V111_C156_Target160_Score80_DoesNotTrigger")]
        public void V111_C156PreviewAndSubmitReadTheFrozenStageScoreAndTarget(string scoreBefore, string expected)
        {
            // Red 3 + blue 4, mixed, scatter: base 7. C156 adds +5 when 2 x frozen score < frozen target (160):
            // 2 x 79 = 158 < 160 -> 7 x (1 + 5) = 42; 2 x 80 = 160 is not below -> 7.
            var s = Deal(Start(new[] { "C156" }), new[] { 3, 4 }, R("red", "blue"), edit: x => x.stage.score = scoreBefore);
            var preview = s.PreviewScore();
            Assert.That(preview.score, Is.EqualTo(expected), "the preview no longer throws and reads the same two numbers");
            var result = Submit(s);
            Assert.That(result.receipt.score, Is.EqualTo(expected));
            Assert.That(result.receipt.submission.snapshot.stageScoreBefore, Is.EqualTo(scoreBefore));
            Assert.That(result.receipt.submission.snapshot.stageTarget, Is.EqualTo("160"));
            Assert.That(s.State.stage.score, Is.EqualTo((int.Parse(scoreBefore) + int.Parse(expected)).ToString()), "the hand's own score never enters the comparison");
        }

        [Test] public void V111_C156ReadsTheTargetActuallyInForce_BossPercentThenThePlainTargetOnceLifted()
        {
            // Spec section 11: stage 6 has base 1620; under the 150% wall (B11) the target is 2430, so 1214 triggers
            // (2428 < 2430) and 1215 does not (2430). Once the talisman lifts the modifier the target is 1620 again:
            // 809 triggers (1618 < 1620), 810 does not.
            Func<string, bool, string> score = (before, lift) =>
            {
                string talisman = null;
                var s = BossPreview("B11", 5, x => talisman = AddItem(x, "I08"), "C156"); Do(s, CommandKind.StartStage);
                Assert.That(s.State.stage.target, Is.EqualTo("2430"));
                if (lift) { Do(s, CommandKind.UseItem, c => c.itemInstanceId = talisman); Assert.That(s.State.stage.target, Is.EqualTo("1620")); }
                s = Deal(s, new[] { 3, 4 }, R("red", "blue"), edit: x => x.stage.score = before);
                var result = Submit(s);
                Assert.That(result.receipt.submission.snapshot.stageTarget, Is.EqualTo(lift ? "1620" : "2430"));
                return result.receipt.score;
            };
            Assert.That(score("1214", false), Is.EqualTo("42")); Assert.That(score("1215", false), Is.EqualTo("7"));
            Assert.That(score("809", true), Is.EqualTo("42")); Assert.That(score("810", true), Is.EqualTo("7"));
        }

        [Test] public void V111_C156ReadsThePressureTargetWhenThePressureChallengeWasChosen()
        {
            // Stage 2: normal target 320, pressure target 480. With 239 banked: 478 < 480 triggers under pressure;
            // with 160 banked: 320 is not below 320 on the normal branch, and is below 480 on the pressure branch.
            Func<bool, string, string> score = (risk, before) =>
            {
                var s = ShopAfterStage(0, x => Equip(x, "C156")); Do(s, CommandKind.LeaveShop);
                Do(s, CommandKind.ChooseChallenge, c => c.risk = risk); Do(s, CommandKind.StartStage);
                s = Deal(s, new[] { 3, 4 }, R("red", "blue"), edit: x => x.stage.score = before);
                var result = Submit(s); Assert.That(result.receipt.submission.snapshot.stageTarget, Is.EqualTo(risk ? "480" : "320")); return result.receipt.score;
            };
            Assert.That(score(true, "239"), Is.EqualTo("42")); Assert.That(score(true, "240"), Is.EqualTo("7"));
            Assert.That(score(false, "160"), Is.EqualTo("7")); Assert.That(score(true, "160"), Is.EqualTo("42"));
        }

        // ---- 2. growth execution (sections 5, 5.1) ----

        [Test] public void V111_C54AnotherHandHalvesWhatItHasGrown_175Then160Then152()
        {
            // Spec section 5: bought in cycle 3 (catch-up 0.2), grown 0.3: x1.75 now. Another hand halves the grown
            // part only: 30 -> 15 (x1.60), then 15 -> 7 (x1.52). Start value and catch-up never move.
            var s = Start(new[] { "C54" }, x => { Card(x, "C54").state.growthCatchup = 20; Card(x, "C54").state.growth = 30; x.previousPrimaryHandId = "pair"; });
            s = Edit(s, x => x.stage.remainingSubmissions = 5);
            // Hand 1, one blue 4 (scatter, not the previous pair): 4 x 1.25 x 1.75 = 8.75 -> 8.
            s = Deal(s, new[] { 4 }, R("blue")); var first = Submit(s);
            Assert.That(first.receipt.score, Is.EqualTo("8"));
            var factor = first.receipt.submission.breakdown.multiplierFactors.Single(f => f.abilityId == "C54");
            Assert.That(factor.numerator + "/" + factor.denominator, Is.EqualTo("7/4"));
            Assert.That(Card(s.State, "C54").state.growth, Is.EqualTo(15));
            Assert.That(first.receipt.effects.Any(e => e.kind == "ControllerGrowthHalved" && e.sourceId == "C54" && e.oldValue == 30 && e.newValue == 15), Is.True);
            // Hand 2, two blue 4s (a pair, not the previous scatter): 8 x 2 x 1.25 x 1.60 = 32.
            s = Deal(s, new[] { 4, 4 }, R("blue", "blue")); var second = Submit(s);
            Assert.That(second.receipt.score, Is.EqualTo("32"));
            factor = second.receipt.submission.breakdown.multiplierFactors.Single(f => f.abilityId == "C54");
            Assert.That(factor.numerator + "/" + factor.denominator, Is.EqualTo("8/5"));
            Assert.That(Card(s.State, "C54").state.growth, Is.EqualTo(7), "15 / 2 rounds down to 7 hundredths");
            // Hand 3, the pair again: 8 x 2 x 1.25 x 1.52 = 30.4 -> 30, and the same hand grows it by 0.1.
            s = Deal(s, new[] { 4, 4 }, R("blue", "blue")); var third = Submit(s);
            Assert.That(third.receipt.score, Is.EqualTo("30"));
            factor = third.receipt.submission.breakdown.multiplierFactors.Single(f => f.abilityId == "C54");
            Assert.That(factor.numerator + "/" + factor.denominator, Is.EqualTo("38/25"));
            Assert.That(Card(s.State, "C54").state.growth, Is.EqualTo(17));
            Assert.That(Card(s.State, "C54").state.growthCatchup, Is.EqualTo(20));
        }

        [Test] public void V111_C54TheFirstSubmissionOfARunHasNothingToCompareWith()
        {
            var s = Deal(Start(new[] { "C54" }, x => Card(x, "C54").state.growth = 30), new[] { 4 }, R("blue"));
            Submit(s);
            Assert.That(Card(s.State, "C54").state.growth, Is.EqualTo(30));
            Assert.That(s.State.previousPrimaryHandId, Is.EqualTo("scatter"), "scatter is remembered as the previous hand");
        }

        [Test] public void V111_GrowthFreezeSkipsTheHalvingTheGrowthAndTheC141Countdown()
        {
            // B07 freezes growth: C54 keeps 30 on another hand, C55 does not gain 3, C141 does not count the clear.
            var s = Boss("B07", "C54", "C55", "C141");
            s = Edit(s, x => { Card(x, "C54").state.growth = 30; x.previousPrimaryHandId = "pair"; });
            s = Deal(s, new[] { 4 }, R("blue")); Submit(s);
            Assert.That(Card(s.State, "C54").state.growth, Is.EqualTo(30));
            Assert.That(Card(s.State, "C55").state.growth, Is.EqualTo(0));
            s = Clear(s);
            Assert.That(Card(s.State, "C141").state.growth, Is.EqualTo(0));
        }

        [Test] public void V111_GrowthHalfOnlyChangesWhatIsReadTheStoredValueKeepsGrowing()
        {
            // Spec section 5.1 (B35): C55 at +12 with 3 grown reads 12 + floor(3 / 2) = 13 this stage; C56 with 0.45
            // grown reads 1 + floor(45 / 2) / 100 = 1.22. The stored values are untouched and still grow: 3 + 3, 45 + 40.
            var s = Boss("B35", "C55", "C56");
            s = Edit(s, x => { Card(x, "C55").state.growth = 3; Card(x, "C56").state.growth = 45; });
            s = Deal(s, new[] { 4 }, R("blue")); var result = Submit(s); var b = result.receipt.submission.breakdown;
            Assert.That(b.controllerBaseAdds.Single(a => a.abilityId == "C55").amount, Is.EqualTo("13"));
            Assert.That(b.additiveEntries.Single(e => e.abilityId == "C56").amount100, Is.EqualTo(122));
            Assert.That(Card(s.State, "C55").state.growth, Is.EqualTo(6));
            Assert.That(Card(s.State, "C56").state.growth, Is.EqualTo(85));
        }

        [Test] public void V111_GrowthHalfLiftedByTheTalismanReadsTheFullValueAgain()
        {
            // The same C55 (+12, 3 grown) reads +15 once the modifier is lifted.
            string talisman = null;
            var s = BossPreview("B35", 2, x => { talisman = AddItem(x, "I08"); Card(x, "C55").state.growth = 3; }, "C55"); Do(s, CommandKind.StartStage);
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = talisman);
            s = Deal(s, new[] { 4 }, R("blue")); var result = Submit(s);
            Assert.That(result.receipt.submission.breakdown.controllerBaseAdds.Single(a => a.abilityId == "C55").amount, Is.EqualTo("15"));
        }

        [Test] public void V111_SubmitGrowthCardsReadTheOldValueThenGrowInTheirOwnUnits()
        {
            // Section 5: C55 +12 then +15; C56 bought in cycle 5 (catch-up 2.0) reads +3 the first time and +3.4 the
            // next; C57 with two real red dice and the virtual one of C146 (group of 3) reads +1 and then +1.8.
            var s = Start(new[] { "C55", "C56", "C57", "C146" }, x => Card(x, "C56").state.growthCatchup = 200);
            s = Edit(s, x => x.stage.remainingSubmissions = 5);
            // Blue 2 + blue 2: no red face, so C56 grows; no red group, so C57 does not.
            s = Deal(s, new[] { 2, 2 }, R("blue", "blue")); var first = Submit(s).receipt.submission.breakdown;
            Assert.That(first.controllerBaseAdds.Single(a => a.abilityId == "C55").amount, Is.EqualTo("12"));
            Assert.That(first.additiveEntries.Single(e => e.abilityId == "C56").amount100, Is.EqualTo(300));
            Assert.That(first.additiveEntries.Single(e => e.abilityId == "C57").amount100, Is.EqualTo(100));
            Assert.That(Card(s.State, "C55").state.growth, Is.EqualTo(3));
            Assert.That(Card(s.State, "C56").state.growth, Is.EqualTo(40));
            Assert.That(Card(s.State, "C57").state.growth, Is.EqualTo(0));
            // Red 2 + red 2: the red group is 2 real dice + 1 virtual = 3, so C57 grows by 0.8; C56 does not.
            s = Deal(s, new[] { 2, 2 }, R("red", "red")); var second = Submit(s).receipt.submission.breakdown;
            Assert.That(second.controllerBaseAdds.Single(a => a.abilityId == "C55").amount, Is.EqualTo("15"));
            Assert.That(second.additiveEntries.Single(e => e.abilityId == "C56").amount100, Is.EqualTo(340));
            Assert.That(Card(s.State, "C56").state.growth, Is.EqualTo(40));
            Assert.That(Card(s.State, "C57").state.growth, Is.EqualTo(80));
            s = Deal(s, new[] { 2, 2 }, R("red", "red")); var third = Submit(s).receipt.submission.breakdown;
            Assert.That(third.additiveEntries.Single(e => e.abilityId == "C57").amount100, Is.EqualTo(180));
        }

        [Test] public void V111_StageClearGrowsC58ByFifteenAndC59BySixPerUnusedSubmission()
        {
            // Section 5: C58 +0.15 per cleared stage (x1.55 after two); C59 +6 per unused submission at settlement
            // (2 left -> +12, so +24 next stage).
            var s = Start(new[] { "C58", "C59" });
            s = Clear(s);
            Assert.That(s.State.lastStageSettlement.remainingSubmissions, Is.EqualTo(2));
            Assert.That(Card(s.State, "C58").state.growth, Is.EqualTo(15));
            Assert.That(Card(s.State, "C59").state.growth, Is.EqualTo(12));
            Assert.That(ControllerRules.GrowthValue(cfg, "C59", Card(s.State, "C59").state), Is.EqualTo(24));
            Do(s, CommandKind.Continue); s = Edit(s, x => x.debugCapacityOverride = 0); Do(s, CommandKind.LeaveShop); Do(s, CommandKind.StartStage);
            s = Clear(s);
            Assert.That(ControllerRules.GrowthValue(cfg, "C58", Card(s.State, "C58").state), Is.EqualTo(155));
        }

        [TestCase(false, 0, TestName = "V111_C132_NormalChallenge_DoesNotGrow")]
        [TestCase(true, 250, TestName = "V111_C132_PressureChallenge_GrowsOnTheClear")]
        public void V111_C132GrowsOnlyWhenThePressureChallengeWasChosenAndCleared(bool risk, int expectedGrowth)
        {
            var s = ShopAfterStage(0, x => Equip(x, "C132")); Do(s, CommandKind.LeaveShop);
            Do(s, CommandKind.ChooseChallenge, c => c.risk = risk); Do(s, CommandKind.StartStage);
            s = Clear(s);
            Assert.That(Card(s.State, "C132").state.growth, Is.EqualTo(expectedGrowth));
        }

        [Test] public void V111_C129GrowsOnABossClearEvenWhenTheModifierWasLifted()
        {
            string talisman = null;
            var s = BossPreview("B25", 2, x => talisman = AddItem(x, "I08"), "C13", "C12", "C129"); Do(s, CommandKind.StartStage);
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = talisman);
            s = Clear(s);
            Assert.That(Card(s.State, "C129").state.growth, Is.EqualTo(250), "+2.5 per cleared boss stage: +3.5 after one");
        }

        [Test] public void V111_TheAltarGrowsByTheConfiguredBasePriceOfItsVictimEvenWhenTheBossThenDisablesIt()
        {
            // Section 5: an altar eats a card of base price 6 with a +5 enchantment and grows 6 x 0.15 = 0.9 only:
            // x1.25 becomes x2.15. The altars act before the disabled list is frozen, so B25 does not stop it.
            var s = BossPreview("B25", 2, x => Card(x, "C54").enchantmentId = "mult2", "C100", "C54", "C13");
            Do(s, CommandKind.StartStage);
            Assert.That(s.State.controllers.Select(c => c.definitionId), Is.EqualTo(new[] { "C100", "C13" }));
            Assert.That(Card(s.State, "C100").state.growth, Is.EqualTo(90));
            Assert.That(ControllerRules.GrowthValue(cfg, "C100", Card(s.State, "C100").state), Is.EqualTo(215));
            Assert.That(s.State.stage.disabledControllerIds, Is.EqualTo(s.State.controllers.Select(c => c.instanceId)), "the list is frozen from the row left after the altar");
        }

        [Test] public void V111_C141CountsDecaysInGrowthAndIsRemovedAtTheFourth()
        {
            // Section 5.1: bonus = 4 - decays. Fresh: +4; after three clears +1; the fourth clear removes it at once.
            var s = Deal(Start(new[] { "C141" }), new[] { 3, 4 }, R("red", "blue"));
            Assert.That(s.PreviewScore().additiveEntries.Single(e => e.abilityId == "C141").amount100, Is.EqualTo(400));
            s = Clear(s);
            Assert.That(Card(s.State, "C141").state.growth, Is.EqualTo(1));
            var last = Deal(Start(new[] { "C141" }, x => Card(x, "C141").state.growth = 3), new[] { 3, 4 }, R("red", "blue"));
            Assert.That(last.PreviewScore().additiveEntries.Single(e => e.abilityId == "C141").amount100, Is.EqualTo(100));
            last = Clear(last);
            Assert.That(last.State.controllers.Any(c => c.definitionId == "C141"), Is.False);
            Assert.That(last.State.processedReceipts.Last().effects.Any(e => e.kind == "ControllerExpired" && e.sourceId == "C141"), Is.True);
            Assert.That(last.State.gold, Is.EqualTo(last.State.lastStageSettlement.goldAfter), "it is removed, not sold: no sale price is paid");
        }

        // ---- 3. the highest hand, C125 and the clear ratio (sections 5.2, 13) ----

        /// <summary>The spec example of section 5.2: the old record is 60 and the three hands of the stage score 60, 61 and 75.</summary>
        private GameSession ThreeHands606175(string target)
        {
            // Mixed scatter hands score floor((pips + C55) x 1.25): C125 itself multiplies by its x1.25, and C55 (+12 and
            // what it has grown, +3 per submission) is the tuning weight. Grown 30: (2 + 4 + 42) x 1.25 = 60.
            // Then 33: (1 + 3 + 45) x 1.25 = 61.25 -> 61. Then 36: (3 + 4 + 5 + 48) x 1.25 = 75.
            var s = Start(new[] { "C125", "C55" }, x => { Card(x, "C55").state.growth = 30; x.highestHandScore = "60"; });
            s = Edit(s, x => x.stage.target = target);
            s = Deal(s, new[] { 2, 4 }, R("red", "blue")); var first = Submit(s);
            Assert.That(first.receipt.score, Is.EqualTo("60"));
            Assert.That(s.State.highestHandScore, Is.EqualTo("60")); Assert.That(s.State.stage.handRecordBroken, Is.False, "an equal score is not a record");
            s = Deal(s, new[] { 1, 3 }, R("red", "blue")); var second = Submit(s);
            Assert.That(second.receipt.score, Is.EqualTo("61"));
            Assert.That(s.State.highestHandScore, Is.EqualTo("61")); Assert.That(s.State.stage.handRecordBroken, Is.True);
            Assert.That(second.receipt.effects.Any(e => e.kind == "HandRecordBroken" && e.detail == "60:61"), Is.True);
            s = Deal(s, new[] { 3, 4, 5 }, R("red", "blue", "red")); var third = Submit(s);
            Assert.That(third.receipt.score, Is.EqualTo("75"));
            Assert.That(s.State.highestHandScore, Is.EqualTo("75"));
            return s;
        }

        [Test] public void V111_C125GrowsOnceForAStageThatBeatTheRecordTwice()
        {
            // 60 + 61 + 75 = 196 reaches 160 on the last submission, so the stage settles: +0.2 once, x1.25 -> x1.45.
            var s = ThreeHands606175("160");
            Assert.That(s.State.phase, Is.EqualTo("StageSettlement"));
            Assert.That(Card(s.State, "C125").state.growth, Is.EqualTo(20));
            Assert.That(ControllerRules.GrowthValue(cfg, "C125", Card(s.State, "C125").state), Is.EqualTo(145));
            Assert.That(s.State.processedReceipts.Last().effects.Count(e => e.kind == "ControllerGrown" && e.sourceId == "C125"), Is.EqualTo(1));
            Assert.That(s.State.highestClearRatioNumerator + "/" + s.State.highestClearRatioDenominator, Is.EqualTo("49/40"), "196 / 160");
        }

        [Test] public void V111_C125DoesNotGrowWhenTheStageIsLost()
        {
            var s = ThreeHands606175("999");
            Assert.That(s.State.phase, Is.EqualTo("RunFailed"));
            Assert.That(s.State.highestHandScore, Is.EqualTo("75"), "the record of the run stands even though the stage was lost");
            Assert.That(Card(s.State, "C125").state.growth, Is.EqualTo(0));
            Assert.That(s.State.highestClearRatioNumerator + "/" + s.State.highestClearRatioDenominator, Is.EqualTo("0/1"), "a lost stage never updates the clear ratio");
        }

        [Test] public void V111_TheHighestHandIsRecordedWithoutC125AndAPreviewWritesNothing()
        {
            var s = Deal(Start(new string[0]), new[] { 3, 4 }, R("red", "blue"));
            Assert.That(s.PreviewScore().score, Is.EqualTo("7")); Assert.That(s.PreviewSubmissionEffects(), Is.Empty);
            Assert.That(s.State.highestHandScore, Is.EqualTo("0")); Assert.That(s.State.stage.handRecordBroken, Is.False);
            Submit(s);
            Assert.That(s.State.highestHandScore, Is.EqualTo("7")); Assert.That(s.State.stage.handRecordBroken, Is.True);
            // The flag belongs to the stage: the next stage starts without it, the record of the run stays.
            s = Clear(s); Do(s, CommandKind.Continue); s = Edit(s, x => x.debugCapacityOverride = 0); Do(s, CommandKind.LeaveShop);
            Assert.That(s.State.stage.handRecordBroken, Is.False); Assert.That(s.State.highestHandScore, Is.EqualTo("7"));
        }

        [Test] public void V111_C125NeedsARealEffectiveUnfrozenInstanceAtTheSettlement()
        {
            // Disabled by B25 (leftmost two): no growth although the record was beaten. Frozen by B07: none either.
            // A mimic of C125 has no state of its own: only the real card grows, once.
            // (The boss fixtures come from a shop after a 1620-point hand, so the record is put back to 0 first.)
            var disabled = Clear(Edit(Boss("B25", "C125", "C13", "C12"), x => x.highestHandScore = "0"));
            Assert.That(disabled.State.stage.handRecordBroken, Is.True); Assert.That(Card(disabled.State, "C125").state.growth, Is.EqualTo(0));
            var frozen = Clear(Edit(Boss("B07", "C125"), x => x.highestHandScore = "0"));
            Assert.That(frozen.State.stage.handRecordBroken, Is.True); Assert.That(Card(frozen.State, "C125").state.growth, Is.EqualTo(0));
            var copied = Clear(Start(new[] { "C41", "C125" }));
            Assert.That(Card(copied.State, "C125").state.growth, Is.EqualTo(20)); Assert.That(Card(copied.State, "C41").state.growth, Is.EqualTo(0));
        }

        [Test] public void V111_TheClearRatioKeepsTheBestReducedFractionAndAnEqualValueKeepsTheRecord()
        {
            // Section 13: first 900 / 300 = 3, then 1300 / 500 = 2.6: the record stays 3. A later 1600 / 500 = 16/5
            // replaces it. The hand is red 4 + blue 6 = 10 on top of a preset score; the third stage is a boss stage,
            // so its modifier is taken off the fixture.
            Func<GameSession, string, string, GameSession> clearWith = (session, target, before) =>
            {
                session = Deal(session, new[] { 4, 6 }, R("red", "blue"), edit: x =>
                {
                    x.stage.target = target; x.stage.score = before; x.stage.qualified = true;
                    x.stage.modifierId = ""; x.stage.modifierNullified = false; x.stage.disabledControllerIds = new string[0];
                });
                Assert.That(Submit(session).receipt.score, Is.EqualTo("10"));
                if (session.State.phase != "StageSettlement") Do(session, CommandKind.EndStage);
                return session;
            };
            Func<GameSession, GameSession> next = session => { Do(session, CommandKind.Continue); Do(session, CommandKind.LeaveShop); Do(session, CommandKind.StartStage); return session; };
            Func<GameSession, string> ratio = session => session.State.highestClearRatioNumerator + "/" + session.State.highestClearRatioDenominator;
            var s = clearWith(Start(new string[0]), "300", "890");
            Assert.That(s.State.lastStageSettlement.score, Is.EqualTo("900"));
            Assert.That(ratio(s), Is.EqualTo("3/1"));
            Assert.That(s.State.processedReceipts.Last().effects.Any(e => e.kind == "ClearRatioRecord" && e.detail == "0/1:3/1"), Is.True);
            s = clearWith(next(s), "500", "1290");
            Assert.That(ratio(s), Is.EqualTo("3/1"));
            s = clearWith(next(s), "500", "1590");
            Assert.That(ratio(s), Is.EqualTo("16/5"));
            // An equal value keeps the record as it is: 600 / 200 = 3, then 900 / 300 = 3 records nothing new.
            var equal = clearWith(Start(new string[0]), "200", "590");
            equal = clearWith(next(equal), "300", "890");
            Assert.That(ratio(equal), Is.EqualTo("3/1"));
            Assert.That(equal.State.processedReceipts.Last().effects.Any(e => e.kind == "ClearRatioRecord"), Is.False);
        }

        // ---- 4. the star pip of C137 (section 5.3) ----

        [Test] public void V111_EveryRealC137DrawsItsOwnStarPipAtStageStartAndAMimicSharesItsTarget()
        {
            // Spec example: tickets 0 and 5 make the stars 1 and 6. Submitting 1, 1, 6 (a pair, mixed colours, base 8,
            // x2): the first card adds +2, the second +1: 8 x 2 x (1 + 3) = 64.
            uint seed = SeedFor(6, 0, 6, 5);
            var plain = Edit(New(), x => { Equip(x, "C137", "C137"); x.effectRngState = seed; });
            var start = Do(plain, CommandKind.StartStage);
            Assert.That(plain.State.controllers.Select(c => c.state.starPip), Is.EqualTo(new[] { 1, 6 }));
            Assert.That(start.receipt.randomResults.Where(r => r.kind == "C137Star").Select(r => r.rawTicket + ":" + r.range + ":" + r.stream), Is.EqualTo(new[] { "0:6:effect", "5:6:effect" }));
            plain = Deal(plain, new[] { 1, 1, 6 }, R("red", "blue", "red")); var b = Submit(plain).receipt.submission.breakdown;
            Assert.That(b.additiveEntries.Where(e => e.abilityId == "C137").Select(e => e.amount100), Is.EqualTo(new long[] { 200, 100 }));
            Assert.That(b.score, Is.EqualTo("64"));
            // A mimic of the first card adds another +2 and does not draw: 8 x 2 x (1 + 5) = 96, still two tickets.
            var copied = Edit(New(), x => { Equip(x, "C41", "C137", "C137"); x.effectRngState = seed; });
            start = Do(copied, CommandKind.StartStage);
            Assert.That(start.receipt.randomResults.Count(r => r.kind == "C137Star"), Is.EqualTo(2));
            Assert.That(copied.State.controllers.Select(c => c.state.starPip), Is.EqualTo(new[] { 0, 1, 6 }));
            copied = Deal(copied, new[] { 1, 1, 6 }, R("red", "blue", "red"));
            Assert.That(Submit(copied).receipt.score, Is.EqualTo("96"));
        }

        [Test] public void V111_ADisabledC137StoresItsStarPipAndTheTalismanNeedsNoDraw()
        {
            // B25 disables the two leftmost cards. All three C137 draw (tickets 0, 5, 2 -> stars 1, 6, 3). Submitting
            // 1, 1, 3 (pair, base 5, x2): only the third card is effective: one 3 -> +1: 5 x 2 x 2 = 20.
            // With the modifier lifted the stored values are used: +2 (two 1s) + 0 (no 6) + 1 = +3: 5 x 2 x 4 = 40.
            uint seed = SeedFor(6, 0, 6, 5, 6, 2);
            Func<bool, GameSession> enter = lift =>
            {
                string talisman = null;
                var s = BossPreview("B25", 2, x => { talisman = AddItem(x, "I08"); x.effectRngState = seed; }, "C137", "C137", "C137");
                var start = Do(s, CommandKind.StartStage);
                Assert.That(start.receipt.randomResults.Count(r => r.kind == "C137Star"), Is.EqualTo(3));
                Assert.That(s.State.controllers.Select(c => c.state.starPip), Is.EqualTo(new[] { 1, 6, 3 }));
                Assert.That(s.State.stage.disabledControllerIds.Length, Is.EqualTo(2));
                if (lift)
                {
                    var used = Do(s, CommandKind.UseItem, c => c.itemInstanceId = talisman);
                    Assert.That(used.receipt.randomResults, Is.Empty, "lifting the modifier draws nothing");
                    Assert.That(s.State.controllers.Select(c => c.state.starPip), Is.EqualTo(new[] { 1, 6, 3 }));
                }
                return Deal(s, new[] { 1, 1, 3 }, R("red", "blue", "red"));
            };
            Assert.That(Submit(enter(false)).receipt.score, Is.EqualTo("20"));
            Assert.That(Submit(enter(true)).receipt.score, Is.EqualTo("40"));
        }

        [Test] public void V111_ADebugSceneGivesStarPipsExplicitlyAndACopyOfADisabledC137AddsNothing()
        {
            // Spec example, last sentence: while the first C137 is disabled, neither it nor its copy adds anything.
            // The scene binds the mimic (fourth card) to the first C137 and gives the stars 1 and 6 itself.
            var s = Start(new[] { "C137", "C13", "C137", "C41" });
            var scene = s.ExportDebugScene(); var ids = scene.controllers.Select(c => c.instanceId).ToArray();
            scene.modifierId = "B25"; scene.controllers[0].state.starPip = 1; scene.controllers[2].state.starPip = 6;
            scene.mimicBindings = new[] { new MimicBinding { sourceInstanceId = ids[3], targetInstanceId = ids[0] } };
            var applied = Do(s, CommandKind.ApplyDebugScene, c => c.debugScene = scene);
            Assert.That(applied.receipt.randomResults, Is.Empty, "applying a scene never draws a star pip");
            Assert.That(s.State.controllers.Select(c => c.state.starPip), Is.EqualTo(new[] { 1, 0, 6, 0 }));
            // 1, 1, 6: only the second C137 (star 6) is effective: +1. C13 is disabled too. 8 x 2 x 2 = 32.
            s = Deal(s, new[] { 1, 1, 6 }, R("red", "blue", "red"));
            Assert.That(Submit(s).receipt.score, Is.EqualTo("32"));
        }

        [Test] public void V111_TheStarPipIsClearedAtSettlementAndDrawnAgainInTheNextStage()
        {
            var s = Start(new[] { "C137" });
            Assert.That(Card(s.State, "C137").state.starPip, Is.InRange(1, 6));
            s = Clear(s);
            Assert.That(Card(s.State, "C137").state.starPip, Is.EqualTo(0), "between stages the card has no value");
            Do(s, CommandKind.Continue); s = Edit(s, x => { x.debugCapacityOverride = 0; x.effectRngState = SeedFor(6, 3); });
            Do(s, CommandKind.LeaveShop); uint before = s.State.effectRngState;
            Assert.That(Card(s.State, "C137").state.starPip, Is.EqualTo(0));
            var start = Do(s, CommandKind.StartStage);
            Assert.That(start.receipt.randomResults.Count(r => r.kind == "C137Star"), Is.EqualTo(1));
            Assert.That(Card(s.State, "C137").state.starPip, Is.EqualTo(4));
            Assert.That(s.State.effectRngState, Is.Not.EqualTo(before));
        }

        // ---- 5. glass (section 7.3) ----

        private GameSession GlassHand(int[] pips, string[] colors, int locked, uint effectSeed, params int[] glassIndices)
            => Deal(Start(new string[0]), pips, colors, locked, pips.Length, x => { foreach (int i in glassIndices) x.dice[i].isGlass = true; x.effectRngState = effectSeed; });

        [Test] public void V111_SubmittedGlassIsJudgedAfterTheScore_Ticket14BreaksAndTicket15Stays()
        {
            // Spec example: two frozen glass dice draw 14 and 15: only the first is removed.
            // Red 2 and blue 3, each x3: base 6 + 9 = 15, mixed scatter: 15.
            var s = GlassHand(new[] { 2, 3 }, R("red", "blue"), 2, SeedFor(100, 14, 100, 15), 0, 1);
            var result = Submit(s);
            Assert.That(result.receipt.score, Is.EqualTo("15"));
            Assert.That(result.receipt.randomResults.Where(r => r.kind == "Glass").Select(r => r.sourceId + ":" + r.rawTicket + ":" + r.appliedValue), Is.EqualTo(new[] { "D001:14:break", "D002:15:keep" }));
            Assert.That(result.receipt.effects.Where(e => e.kind == "DieBroken").Select(e => e.dieId), Is.EqualTo(new[] { "D001" }));
            Assert.That(s.State.dice.Select(d => d.dieId), Is.EqualTo(new[] { "D002" }));
            Assert.That(s.State.stage.score, Is.EqualTo("15"), "the score already paid is kept");
            Assert.That(s.State.phase, Is.EqualTo("HandReady"), "a die is left and submissions remain: the next hand starts");
        }

        [Test] public void V111_RollingNeverBreaksGlassAndAGlassDieLeftInThePoolIsNotJudged()
        {
            var s = Edit(Start(new string[0]), x => { foreach (var d in x.dice) d.isGlass = true; x.effectRngState = SeedFor(100, 0, 100, 0); });
            uint effect = s.State.effectRngState;
            var roll = Do(s, CommandKind.Roll);
            Assert.That(roll.receipt.randomResults.Any(r => r.kind == "Glass"), Is.False); Assert.That(roll.receipt.effects.Any(e => e.kind == "DieBroken"), Is.False);
            Assert.That(s.State.dice.Count, Is.EqualTo(5)); Assert.That(s.State.effectRngState, Is.EqualTo(effect));
            // Only the first die is locked and submitted; the glass die in the pool draws nothing. The submitted one draws 0.
            var hand = GlassHand(new[] { 2, 3 }, R("red", "blue"), 1, SeedFor(100, 0, 100, 0), 0, 1);
            var result = Submit(hand);
            Assert.That(result.receipt.randomResults.Where(r => r.kind == "Glass").Select(r => r.sourceId), Is.EqualTo(new[] { "D001" }));
            Assert.That(hand.State.dice.Select(d => d.dieId), Is.EqualTo(new[] { "D002" }));
        }

        [Test] public void V111_ASealedGlassDieScoringZeroIsStillJudged()
        {
            // B05 seals the first submitted die: it contributes 0 and the hand scores 0, but the die was submitted.
            var s = Boss("B05");
            s = Deal(s, new[] { 4 }, R("red"), edit: x => { x.dice[0].isGlass = true; x.effectRngState = SeedFor(100, 14); });
            var result = Submit(s);
            Assert.That(result.receipt.score, Is.EqualTo("0"));
            Assert.That(result.receipt.effects.Count(e => e.kind == "DieBroken" && e.dieId == "D001"), Is.EqualTo(1));
            Assert.That(s.State.dice.Count, Is.EqualTo(4)); Assert.That(s.State.phase, Is.EqualTo("HandReady"));
        }

        [Test] public void V111_GlassIdentityIsFrozenWithTheSubmission_C07CopiesDoNotChangeWhoIsJudged()
        {
            // First die glass, second plain: C07 makes the second glass too, but only the first is judged this hand.
            var first = Deal(Start(new[] { "C07" }), new[] { 2, 5 }, R("red", "blue"), 2, 2, x => { x.dice[0].isGlass = true; x.effectRngState = SeedFor(100, 15, 100, 14, 100, 14); });
            var result = Submit(first);
            Assert.That(result.receipt.randomResults.Where(r => r.kind == "Glass").Select(r => r.sourceId + ":" + r.rawTicket), Is.EqualTo(new[] { "D001:15" }));
            Assert.That(first.State.dice.Select(d => d.isGlass), Is.EqualTo(new[] { true, true }));
            // The new glass die takes part from the next submission on: both draw 14 and both break.
            first = Deal(first, new[] { 2, 5 }, R("red", "blue")); result = Submit(first);
            Assert.That(result.receipt.randomResults.Where(r => r.kind == "Glass").Select(r => r.sourceId + ":" + r.rawTicket), Is.EqualTo(new[] { "D001:14", "D002:14" }));
            Assert.That(first.State.dice, Is.Empty);
            Assert.That(first.State.phase, Is.EqualTo("RunFailed")); Assert.That(first.State.failureReason, Is.EqualTo("NoDice"));
            // First die plain, second glass: the copy makes the second plain, and it is judged all the same.
            var second = Deal(Start(new[] { "C07" }), new[] { 2, 5 }, R("red", "blue"), 2, 2, x => { x.dice[1].isGlass = true; x.effectRngState = SeedFor(100, 14); });
            result = Submit(second);
            Assert.That(result.receipt.effects.Any(e => e.kind == "DieCopied" && e.dieId == "D002"), Is.True);
            Assert.That(result.receipt.randomResults.Where(r => r.kind == "Glass").Select(r => r.sourceId + ":" + r.rawTicket), Is.EqualTo(new[] { "D002:14" }));
            Assert.That(second.State.dice.Select(d => d.dieId), Is.EqualTo(new[] { "D001" }));
        }

        [TestCase(0, "RunFailed", TestName = "V111_Glass_LastDieBreaksOnTheClearingHand_SettlesThenFailsWithoutDice")]
        [TestCase(23, "RunWon", TestName = "V111_Glass_LastDieBreaksOnTheClearingHandOfTheFinalStage_Wins")]
        public void V111_TheLastGlassDieBreakingOnAClearingHandSettlesTheStageFirst(int stageIndex, string continuation)
        {
            var s = Deal(Start(new string[0]), new[] { 4 }, R("red"), 1, 1, x =>
            {
                var def = cfg.stages[stageIndex]; x.stage.stageIndex = stageIndex; x.stage.stageId = def.stageId; x.stage.stageKind = def.stageKind; x.stage.modifierNullified = false; x.stage.modifierId = "";
                x.stage.target = "10"; x.stage.score = "9"; x.dice[0].isGlass = true; x.effectRngState = SeedFor(100, 3);
            });
            var result = Submit(s);
            Assert.That(result.receipt.effects.Any(e => e.kind == "DieBroken"), Is.True);
            Assert.That(s.State.phase, Is.EqualTo("StageSettlement"));
            Assert.That(result.receipt.settlement, Is.Not.Null, "one successful settlement");
            Assert.That(s.State.stage.settlementContinuation, Is.EqualTo(continuation));
        }

        [Test] public void V111_TheDebugOverrideOfAGlassDrawStillWorks()
        {
            var s = GlassHand(new[] { 2 }, R("red"), 1, SeedFor(100, 14), 0);
            s = Edit(s, x => x.randomOverrides.Add(new DebugRandomOverride { kind = "Glass", sourceId = "D001", value = "keep" }));
            var result = Submit(s); var draw = result.receipt.randomResults.Single(r => r.kind == "Glass");
            Assert.That(draw.rawTicket, Is.EqualTo(14)); Assert.That(draw.rawValue, Is.EqualTo("break")); Assert.That(draw.appliedValue, Is.EqualTo("keep")); Assert.That(draw.overridden, Is.True);
            Assert.That(s.State.dice.Count, Is.EqualTo(1)); Assert.That(s.GlassBreakPercentIn(s.State), Is.EqualTo(15));
        }

        // ---- 6. cultivation: rewrites, gilding weight and the count (sections 6, 6.1, 7.2) ----

        private static int Count(GameSession s, string id, int ordinal = 0) => Card(s.State, id, ordinal).state.cultivationFacesChanged;
        private static string[] Cultivated(RunState x) => x.dice.OrderBy(d => d.createdSerial).SelectMany(d => d.faces.OrderBy(f => f.faceId, StringComparer.Ordinal).Where(f => f.cultivated).Select(f => d.dieId + "/" + f.faceId)).ToArray();

        [Test] public void V111_GildingAddsOneWeightUpToTheCapAndAnAlreadyGoldFaceChangesNothing()
        {
            // Section 7.2 example: two faces of weight 1 and 5 newly gilded end at 2 and 5. C117 gilds the up faces of
            // the first and last submitted dice. The third hand shows a face that is already gold: nothing changes.
            var s = Deal(Start(new[] { "C117" }), new[] { 2, 3 }, R("red", "blue"), edit: x => Face(x, "D002", "F01").baseWeight = 5);
            s = Edit(s, x => x.stage.remainingSubmissions = 5);
            Submit(s);
            Assert.That(Face(s.State, "D001", "F01").isGold && Face(s.State, "D002", "F01").isGold, Is.True);
            Assert.That(Face(s.State, "D001", "F01").baseWeight, Is.EqualTo(2));
            Assert.That(Face(s.State, "D002", "F01").baseWeight, Is.EqualTo(5), "weight 5 is the cap");
            Assert.That(Face(s.State, "D001", "F01").baseColor, Is.EqualTo("red"), "the base colour stays");
            Assert.That(Count(s, "C117"), Is.EqualTo(2), "both faces changed (gold), one count per face");
            Assert.That(Cultivated(s.State), Is.EqualTo(new[] { "D001/F01", "D002/F01" }));
            s = Deal(s, new[] { 2 }, R("red")); var again = Submit(s);
            Assert.That(Face(s.State, "D001", "F01").baseWeight, Is.EqualTo(2), "an already gold face gains no weight");
            Assert.That(Count(s, "C117"), Is.EqualTo(2));
            Assert.That(again.receipt.effects.Any(e => e.kind == "FaceCultivated"), Is.False);
        }

        [Test] public void V111_C127CopiesFourPropertiesOntoTheLastFaceCountsItOnceAndTheFourthRewriteRaisesTheResale()
        {
            // Section 6.1: the first up face (5, red, smooth, gold) is copied onto the last up face (2, blue, weight 3):
            // pip, colour, texture and gold are copied, the base weight is not; newly gold: weight 3 -> 4.
            // Section 6 example: a card with 3 rewrites counts this one face once: 4, and its resale gains 1
            // (C127 costs 5: floor(5 / 2) = 2, + floor(4 / 4) = 3).
            var s = Deal(Start(new[] { "C127" }, x => Card(x, "C127").state.cultivationFacesChanged = 3), new[] { 5, 2 }, R("red", "blue"), edit: x =>
            {
                var source = Face(x, "D001", "F01"); source.texture = "smooth"; source.isGold = true; source.baseWeight = 2; Face(x, "D002", "F01").baseWeight = 3;
            });
            Assert.That(s.ControllerSalePriceIn(s.State, Card(s.State, "C127")), Is.EqualTo(2));
            var result = Submit(s); var target = Face(s.State, "D002", "F01");
            Assert.That(target.permanentPip + "/" + target.baseColor + "/" + target.texture + "/" + target.isGold + "/" + target.baseWeight, Is.EqualTo("5/red/smooth/True/4"));
            Assert.That(result.receipt.effects.Count(e => e.kind == "FaceCultivated"), Is.EqualTo(1));
            Assert.That(Count(s, "C127"), Is.EqualTo(4));
            Assert.That(s.ControllerSalePriceIn(s.State, Card(s.State, "C127")), Is.EqualTo(3));
        }

        [Test] public void V111_C127RemovingGoldDoesNotTakeTheWeightBack()
        {
            var s = Deal(Start(new[] { "C127" }), new[] { 5, 5 }, R("red", "red"), edit: x => { var target = Face(x, "D002", "F01"); target.isGold = true; target.baseWeight = 2; });
            Submit(s); var after = Face(s.State, "D002", "F01");
            Assert.That(after.isGold, Is.False); Assert.That(after.baseWeight, Is.EqualTo(2));
            Assert.That(Count(s, "C127"), Is.EqualTo(1), "losing the gold layer is a change of the face");
        }

        [Test] public void V111_TheCultivationCountStopsAt24AndTheResaleBonusAtSix()
        {
            // C122 (cost 4, resale 2) sets every submitted up face to 5. From 23, two changed faces only reach 24:
            // resale 2 + floor(24 / 4) = 8.
            var s = Deal(Start(new[] { "C122" }, x => Card(x, "C122").state.cultivationFacesChanged = 23), new[] { 1, 2 }, R("red", "blue"));
            var result = Submit(s);
            Assert.That(Face(s.State, "D001", "F01").permanentPip, Is.EqualTo(5), "1 goes straight to 5"); Assert.That(Face(s.State, "D002", "F01").permanentPip, Is.EqualTo(5));
            Assert.That(Count(s, "C122"), Is.EqualTo(24));
            Assert.That(result.receipt.effects.Where(e => e.kind == "FaceCultivated").Select(e => e.oldValue + ">" + e.newValue), Is.EqualTo(new[] { "23>24", "24>24" }));
            Assert.That(s.ControllerSalePriceIn(s.State, Card(s.State, "C122")), Is.EqualTo(8));
        }

        [Test] public void V111_ARewriteThroughAMimicIsCreditedToTheCopiedCardAndAnUnchangedFaceToNobody()
        {
            // The mimic (left) runs C122 first and changes both faces; the real C122 then finds them at 5 already.
            // Both changes are credited to the real card; the mimic holds no count.
            var s = Deal(Start(new[] { "C41", "C122" }), new[] { 1, 2 }, R("red", "blue"));
            var result = Submit(s); string real = Card(s.State, "C122").instanceId, mimic = Card(s.State, "C41").instanceId;
            Assert.That(Count(s, "C122"), Is.EqualTo(2)); Assert.That(Count(s, "C41"), Is.EqualTo(0));
            Assert.That(result.receipt.effects.Where(e => e.kind == "FaceCultivated").Select(e => e.dieId + "/" + e.faceId + "/" + e.detail), Is.EqualTo(new[] { "D001/F01/" + mimic + ":" + real, "D002/F01/" + mimic + ":" + real }));
        }

        [Test] public void V111_C36TwiceRaisesTheFirstThreeFacesByTheNetSumAndCountsOnlyTheFacesThatChanged()
        {
            // Section 6.1 example: capacity 5, four dice submitted, first pips 5, 2, 6, two C36: 6, 4, 6.
            // Faces one and two changed: each C36 counts 1 for each; the third (already 6) counts nothing.
            var s = Deal(Start(new[] { "C36", "C36" }), new[] { 5, 2, 6, 3 }, R("red", "blue", "red", "blue"));
            Submit(s);
            Assert.That(new[] { "D001", "D002", "D003", "D004" }.Select(d => Face(s.State, d, "F01").permanentPip), Is.EqualTo(new[] { 6, 4, 6, 3 }));
            Assert.That(Count(s, "C36", 0), Is.EqualTo(2)); Assert.That(Count(s, "C36", 1), Is.EqualTo(2));
            Assert.That(Cultivated(s.State), Is.EqualTo(new[] { "D001/F01", "D002/F01" }));
        }

        [Test] public void V111_C36AndC37CancelToANetOfZeroAndNothingCounts()
        {
            var s = Deal(Start(new[] { "C36", "C37" }), new[] { 5, 2 }, R("red", "blue"));
            Submit(s);
            Assert.That(Face(s.State, "D001", "F01").permanentPip, Is.EqualTo(5)); Assert.That(Face(s.State, "D002", "F01").permanentPip, Is.EqualTo(2));
            Assert.That(Count(s, "C36"), Is.EqualTo(0)); Assert.That(Count(s, "C37"), Is.EqualTo(0)); Assert.That(Cultivated(s.State), Is.Empty);
        }

        [Test] public void V111_C123LowersFourFiveSixToItsFloorOfThreeAndRaisesOne()
        {
            // Section 6.1: with one C123, 1 -> 2, 4 -> 3, 5 -> 3, 6 -> 4.
            var s = Deal(Start(new[] { "C123" }), new[] { 1, 4, 5, 6 }, R("red", "blue", "red", "blue"));
            Submit(s);
            Assert.That(new[] { "D001", "D002", "D003", "D004" }.Select(d => Face(s.State, d, "F01").permanentPip), Is.EqualTo(new[] { 2, 3, 3, 4 }));
            Assert.That(Count(s, "C123"), Is.EqualTo(4));
        }

        [Test] public void V111_C123LeavesAFaceThatIsAlreadyBelowItsFloorWhenItRuns()
        {
            // Two C37 first lower the faces by 2 (4 -> 2, 6 -> 4). C123 planned -2 for both from the frozen 4 and 6;
            // when it runs, the face at 2 is not above the floor 3 and stays 2, the face at 4 goes to 3.
            var s = Deal(Start(new[] { "C37", "C37", "C123" }), new[] { 4, 6 }, R("red", "blue"));
            Submit(s);
            Assert.That(Face(s.State, "D001", "F01").permanentPip, Is.EqualTo(2)); Assert.That(Face(s.State, "D002", "F01").permanentPip, Is.EqualTo(3));
            Assert.That(Count(s, "C37", 0), Is.EqualTo(2)); Assert.That(Count(s, "C37", 1), Is.EqualTo(2));
            Assert.That(Count(s, "C123"), Is.EqualTo(1), "only the face it really changed");
        }

        [Test] public void V111_C07CopiesTheFirstDieAndCountsOnlyTheFacesThatDifferedAndNeverCopiesHistoryMarks()
        {
            // Section 6.1 example: only three faces differ, so the count is +3. The copy takes the weights as they are
            // (no gilding weight) and the target keeps its own marks: a marked face of the source marks nothing.
            var s = Deal(Start(new[] { "C07" }), new[] { 2, 5 }, R("red", "blue"), 2, 2, x =>
            {
                Face(x, "D001", "F06").cultivated = true;                    // history of the source
                var gold = Face(x, "D001", "F02"); gold.isGold = true;       // differs on the target: copied gold, weight stays 1
                Face(x, "D001", "F03").permanentPip = 6;                     // differs on the target
            });
            // Faces that differ on D002: F01 (2 red against 5 blue), F02 (gold), F03 (pip 6 against 3).
            Submit(s);
            Assert.That(Pips(s.State, "D002"), Is.EqualTo(new[] { 2, 2, 6, 4, 5, 6 }));
            Assert.That(Face(s.State, "D002", "F02").isGold, Is.True); Assert.That(Face(s.State, "D002", "F02").baseWeight, Is.EqualTo(1));
            Assert.That(Count(s, "C07"), Is.EqualTo(3));
            Assert.That(Cultivated(s.State), Is.EqualTo(new[] { "D001/F06", "D002/F01", "D002/F02", "D002/F03" }));
        }

        [Test] public void V111_C26CountsOnlyTheFacesThatWereNotRed()
        {
            // Section 6.1 example: a die with two red faces (standard F01 and F04): the other four are counted.
            var s = Deal(Start(new[] { "C26" }), new[] { 1 }, R("red"));
            Submit(s);
            Assert.That(s.State.dice[0].faces.All(f => f.baseColor == "red"), Is.True);
            Assert.That(Count(s, "C26"), Is.EqualTo(4));
            Assert.That(Cultivated(s.State), Is.EqualTo(new[] { "D001/F02", "D001/F03", "D001/F05", "D001/F06" }));
        }

        [Test] public void V111_C15DyesTheBlueFacesBetweenRedOnesAndC38TheFirstAndLast()
        {
            // Section 6.1: red blue red blue red changes at most two faces (the two blue ones) for C15.
            var spread = Deal(Start(new[] { "C15" }), new[] { 1, 2, 3, 4, 6 }, R("red", "blue", "red", "blue", "red"));
            Submit(spread);
            Assert.That(spread.State.dice.Select(d => d.faces.First(f => f.faceId == "F01").baseColor), Is.All.EqualTo("red"));
            Assert.That(Count(spread, "C15"), Is.EqualTo(2)); Assert.That(Cultivated(spread.State), Is.EqualTo(new[] { "D002/F01", "D004/F01" }));
            // C38: a single red die: one face repainted blue. A face that is blue already counts nothing.
            var single = Deal(Start(new[] { "C38" }), new[] { 3 }, R("red")); single = Edit(single, x => x.stage.remainingSubmissions = 5);
            Submit(single);
            Assert.That(Face(single.State, "D001", "F01").baseColor, Is.EqualTo("blue")); Assert.That(Count(single, "C38"), Is.EqualTo(1));
            single = Deal(single, new[] { 3 }, R("blue")); Submit(single);
            Assert.That(Count(single, "C38"), Is.EqualTo(1), "repainting in the same colour is no rewrite");
        }

        [Test] public void V111_C118CopiesTheBaseColourToTheOppositeFaceEvenUnderAWave()
        {
            // Section 6.1 example: the up face is red underneath and counts as blue (blue wave): the opposite face
            // (F06, blue) is still dyed red.
            var s = Deal(Start(new[] { "C118" }), new[] { 3 }, R("red"), edit: x => x.hand.blueWave = true);
            Submit(s);
            Assert.That(Face(s.State, "D001", "F06").baseColor, Is.EqualTo("red")); Assert.That(Count(s, "C118"), Is.EqualTo(1));
        }

        [Test] public void V111_C119SetsTheFirstTwoOtherFacesOfTheFirstAndLastDieToTheUpPip()
        {
            // Section 6.1 example: the first die shows 6 and three other faces differ (F03, F04, F06): the first two
            // by face id become 6. The die is both first and last, so it is handled once.
            var s = Deal(Start(new[] { "C119" }), new[] { 6 }, R("red"), edit: x =>
            {
                Face(x, "D001", "F02").permanentPip = 6; Face(x, "D001", "F03").permanentPip = 2; Face(x, "D001", "F04").permanentPip = 3;
                Face(x, "D001", "F05").permanentPip = 6; Face(x, "D001", "F06").permanentPip = 5;
            });
            Submit(s);
            Assert.That(Pips(s.State, "D001"), Is.EqualTo(new[] { 6, 6, 6, 6, 6, 5 }));
            Assert.That(Count(s, "C119"), Is.EqualTo(2)); Assert.That(Cultivated(s.State), Is.EqualTo(new[] { "D001/F03", "D001/F04" }));
        }

        [Test] public void V111_C124AddsTwoWeightUpToFiveAndC126RaisesAPoolFaceByTwo()
        {
            // C124: weight 4 becomes 5, one face only. C126: a pool die showing 4 becomes 6.
            var s = Deal(Start(new[] { "C124", "C126" }), new[] { 3, 4 }, R("red", "blue"), 1, 2, x => Face(x, "D001", "F01").baseWeight = 4);
            Submit(s);
            Assert.That(Face(s.State, "D001", "F01").baseWeight, Is.EqualTo(5)); Assert.That(Count(s, "C124"), Is.EqualTo(1));
            Assert.That(Face(s.State, "D002", "F01").permanentPip, Is.EqualTo(6)); Assert.That(Count(s, "C126"), Is.EqualTo(1));
            Assert.That(Cultivated(s.State), Is.EqualTo(new[] { "D001/F01", "D002/F01" }));
        }

        [Test] public void V111_ATemporaryDieIsRewrittenButNeverCountedOrMarked()
        {
            var s = Deal(Start(new[] { "C122" }), new[] { 1, 2 }, R("red", "blue"), 2, 2, x => { x.dice[1].lifetime = "stage_temporary"; x.dice[1].createdStageId = x.stage.stageId; });
            var result = Submit(s);
            Assert.That(Face(s.State, "D002", "F01").permanentPip, Is.EqualTo(5));
            Assert.That(Count(s, "C122"), Is.EqualTo(1)); Assert.That(Cultivated(s.State), Is.EqualTo(new[] { "D001/F01" }));
            Assert.That(result.receipt.effects.Count(e => e.kind == "FaceCultivated"), Is.EqualTo(1));
        }

        [TestCase("C120", "2,3,4,4,5,6", TestName = "V111_C120_RaisesTheThreeLowestFacesOfEveryPermanentDie")]
        [TestCase("C121", "1,2,3,3,4,5", TestName = "V111_C121_LowersTheThreeHighestFacesOfEveryPermanentDie")]
        public void V111_StageEndFaceGrowthMovesThreeFacesPerDie(string id, string expected)
        {
            // Section 6.1 example: a standard die 1-6 becomes 2, 3, 4, 4, 5, 6 under C120; under C121 its 4, 5, 6 become 3, 4, 5.
            // Two permanent dice: three faces each, six rewrites.
            var s = Edit(Start(new[] { id }), x => { while (x.dice.Count > 2) x.dice.RemoveAt(x.dice.Count - 1); x.hand.currentResults.RemoveAll(r => x.dice.All(d => d.dieId != r.dieId)); });
            s = Edit(s, x => { x.stage.score = x.stage.target; x.stage.qualified = true; });
            Do(s, CommandKind.Roll); Do(s, CommandKind.EndStage);
            Assert.That(string.Join(",", Pips(s.State, "D001")), Is.EqualTo(expected)); Assert.That(string.Join(",", Pips(s.State, "D002")), Is.EqualTo(expected));
            Assert.That(Count(s, id), Is.EqualTo(6));
            Assert.That(Cultivated(s.State).Length, Is.EqualTo(6));
        }

        [Test] public void V111_C120ThenC121EachSourceInOrderAndAFaceAtItsLimitIsSkipped()
        {
            // A die of 6, 6, 6, 6, 5, 5: C120 can only raise the two 5s (F05, F06) -> all 6, two rewrites.
            // C121 then lowers the three highest by face id: F01, F02, F03 -> 5.
            var s = Edit(Start(new[] { "C121", "C120" }), x =>
            {
                while (x.dice.Count > 1) x.dice.RemoveAt(x.dice.Count - 1); x.hand.currentResults.RemoveAll(r => r.dieId != "D001");
                int[] pips = { 6, 6, 6, 6, 5, 5 }; for (int i = 0; i < 6; i++) x.dice[0].faces[i].permanentPip = pips[i];
                x.stage.score = x.stage.target; x.stage.qualified = true;
            });
            Do(s, CommandKind.Roll); Do(s, CommandKind.EndStage);
            Assert.That(Pips(s.State, "D001"), Is.EqualTo(new[] { 5, 5, 5, 6, 6, 6 }));
            Assert.That(Count(s, "C120"), Is.EqualTo(2)); Assert.That(Count(s, "C121"), Is.EqualTo(3));
        }

        [Test] public void V111_C115DrawsAFaceThenOneOfItsLegalUpgradesThreeTimes()
        {
            // One standard permanent die. Step 1: six faces, ticket 4 -> F05 (5, blue, not gold): legal U03, U06, U08
            // (section 6.1 example), ticket 1 -> U06: gold, weight 1 -> 2. Step 2: five faces left, ticket 0 -> F01
            // (1, red): legal U04, U06, U08, ticket 2 -> U08: pip 2. Step 3: four faces left (F02, F03, F04, F06),
            // ticket 3 -> F06 (6, blue): legal U03, U06, ticket 0 -> U03: red.
            uint seed = SeedFor(6, 4, 3, 1, 5, 0, 3, 2, 4, 3, 2, 0);
            var s = Edit(Start(new[] { "C115" }), x =>
            {
                while (x.dice.Count > 1) x.dice.RemoveAt(x.dice.Count - 1); x.hand.currentResults.RemoveAll(r => r.dieId != "D001");
                x.stage.score = x.stage.target; x.stage.qualified = true;
            });
            Do(s, CommandKind.Roll); s = Edit(s, x => x.effectRngState = seed);
            var end = Do(s, CommandKind.EndStage);
            Assert.That(end.receipt.randomResults.Where(r => r.kind.StartsWith("C115")).Select(r => r.kind + ":" + r.range + ":" + r.rawTicket + ":" + r.appliedValue), Is.EqualTo(new[]
            {
                "C115Face:6:4:D001/F05", "C115Upgrade:3:1:U06", "C115Face:5:0:D001/F01", "C115Upgrade:3:2:U08", "C115Face:4:3:D001/F06", "C115Upgrade:2:0:U03"
            }));
            var five = Face(s.State, "D001", "F05"); Assert.That(five.isGold, Is.True); Assert.That(five.baseWeight, Is.EqualTo(2)); Assert.That(five.baseColor, Is.EqualTo("blue"));
            Assert.That(Face(s.State, "D001", "F01").permanentPip, Is.EqualTo(2)); Assert.That(Face(s.State, "D001", "F06").baseColor, Is.EqualTo("red"));
            Assert.That(Count(s, "C115"), Is.EqualTo(3));
            Assert.That(Cultivated(s.State), Is.EqualTo(new[] { "D001/F01", "D001/F05", "D001/F06" }));
            Assert.That(end.receipt.effects.Where(e => e.kind == "RandomUpgrade").Select(e => e.faceId + ":" + e.detail), Is.EqualTo(new[] { "F05:U06", "F01:U08", "F06:U03" }));
        }

        [Test] public void V111_C115WithNoPermanentFaceToImproveDrawsNothing()
        {
            // Only a temporary die is left: the candidate list is empty, the source stops and the stream does not move.
            var s = Edit(Start(new[] { "C115" }), x =>
            {
                while (x.dice.Count > 1) x.dice.RemoveAt(x.dice.Count - 1); x.hand.currentResults.RemoveAll(r => r.dieId != "D001");
                x.dice[0].lifetime = "stage_temporary"; x.dice[0].createdStageId = x.stage.stageId; x.stage.score = x.stage.target; x.stage.qualified = true;
            });
            Do(s, CommandKind.Roll); uint before = s.State.effectRngState;
            var end = Do(s, CommandKind.EndStage);
            Assert.That(end.receipt.randomResults.Any(r => r.kind.StartsWith("C115")), Is.False);
            Assert.That(end.receipt.effects.Any(e => e.sourceId == "C115"), Is.False);
            Assert.That(s.State.effectRngState, Is.EqualTo(before)); Assert.That(Count(s, "C115"), Is.EqualTo(0));
        }

        // ---- 7. boss modifiers inside the stage (section 10) ----

        [Test] public void V111_TheDisabledListIsFrozenAfterAltarAndScapegoatAndBeforeTheOtherStageStartAbilities()
        {
            // Row: altar, victim, C137, C136. The altar eats its neighbour first; B25 then disables the two leftmost
            // cards of what is left (the altar and C137). The disabled C137 still stores a star pip; C136, third, is
            // effective and adds its temporary die.
            var s = BossPreview("B25", 2, null, "C100", "C13", "C137", "C136");
            var start = Do(s, CommandKind.StartStage); var row = s.State.controllers;
            Assert.That(row.Select(c => c.definitionId), Is.EqualTo(new[] { "C100", "C137", "C136" }));
            Assert.That(s.State.stage.disabledControllerIds, Is.EqualTo(new[] { row[0].instanceId, row[1].instanceId }));
            Assert.That(row[1].state.starPip, Is.InRange(1, 6));
            Assert.That(start.receipt.effects.Count(e => e.kind == "DieAdded" && e.sourceId == "C136"), Is.EqualTo(1));
            Assert.That(row[0].state.growth, Is.EqualTo(30), "C13 costs 2: 2 x 0.15");
        }

        [Test] public void V111_B29DisablesOnlyTheFirstRareCardOfTheRowAndTheTalismanClearsTheList()
        {
            // Section 10 example: uncommon A, rare B, rare C: only B is disabled. (C46 is uncommon, C54 and C58 rare.)
            string talisman = null;
            var s = BossPreview("B29", 2, x => talisman = AddItem(x, "I08"), "C46", "C54", "C58"); Do(s, CommandKind.StartStage);
            Assert.That(s.State.stage.disabledControllerIds, Is.EqualTo(new[] { Card(s.State, "C54").instanceId }));
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = talisman);
            Assert.That(s.State.stage.disabledControllerIds, Is.Empty); Assert.That(s.CurrentModifierId, Is.Null);
        }

        [Test] public void V111_ADisabledC53DrawsNoTicket()
        {
            // B25 disables the first two cards; only the third, a real effective C53, draws (ruling A06).
            var s = Boss("B25", "C53", "C13", "C53");
            s = Deal(s, new[] { 4 }, R("blue")); var result = Submit(s);
            Assert.That(result.receipt.randomResults.Where(r => r.kind == "C53").Select(r => r.sourceId), Is.EqualTo(new[] { Card(s.State, "C53", 1).instanceId }));
            Assert.That(result.receipt.submission.snapshot.resolvedRandomChoices.Select(c => c.ownerInstanceId), Is.EqualTo(new[] { Card(s.State, "C53", 1).instanceId }));
        }

        // ---- 8. save validation of the new state ----

        /// <summary>An ordinary (non-debug) run in stage 1 with these cards; the state is edited and loaded through the normal constructor.</summary>
        private GameSession Ordinary(params string[] controllers)
        {
            var x = New().State; Equip(x, controllers); var s = new GameSession(cfg, new MemorySaveStore(), x); Do(s, CommandKind.StartStage);
            Assert.That(s.State.isDebug, Is.False); return s;
        }
        private void AssertRejected(GameSession s, Action<RunState> corrupt, string reason)
        {
            var x = s.State; corrupt(x);
            Assert.That(() => new GameSession(cfg, new MemorySaveStore(), x), Throws.Exception, reason);
        }

        [Test] public void V111_AnOrdinaryRunWithTheNewStateSurvivesAReloadAndKeepsItsHash()
        {
            var s = Ordinary("C137", "C122", "C54");
            Do(s, CommandKind.Roll); Lock(s, s.State.hand.currentResults.Select(r => r.dieId).Take(2).ToArray()); Do(s, CommandKind.Submit);
            var saved = s.State; var reloaded = new GameSession(cfg, new MemorySaveStore(), saved);
            Assert.That(GameSession.StateHash(reloaded.State), Is.EqualTo(GameSession.StateHash(saved)));
            Assert.That(reloaded.State.highestHandScore, Is.EqualTo(saved.processedReceipts.Last().score));
            Assert.That(reloaded.State.stage.handRecordBroken, Is.EqualTo(saved.processedReceipts.Last().score != "0"));
            Assert.That(Card(reloaded.State, "C137").state.starPip, Is.InRange(1, 6));
        }

        [Test] public void V111_ValidationRejectsNewStateThatIsOutOfRangeOrInconsistent()
        {
            var s = Ordinary("C137", "C122", "C54", "C13");
            Do(s, CommandKind.Roll); Lock(s, s.State.hand.currentResults.Select(r => r.dieId).Take(2).ToArray()); Do(s, CommandKind.Submit);
            Assert.That(BigInteger.Parse(s.State.highestHandScore), Is.GreaterThan(BigInteger.Zero));
            AssertRejected(s, x => x.highestHandScore = "0", "the record cannot be below a committed submission");
            AssertRejected(s, x => x.highestHandScore = "007", "not a canonical decimal");
            AssertRejected(s, x => x.highestHandScore = null, "missing");
            AssertRejected(s, x => { x.highestClearRatioNumerator = "2"; x.highestClearRatioDenominator = "2"; }, "not reduced");
            AssertRejected(s, x => x.highestClearRatioDenominator = "0", "zero denominator");
            AssertRejected(s, x => x.highestClearRatioNumerator = "3", "stage 1 is not settled yet: no clear ratio can exist");
            AssertRejected(s, x => Card(x, "C137").state.starPip = 0, "a real C137 of a stage in progress has drawn its star");
            AssertRejected(s, x => Card(x, "C137").state.starPip = 7, "above 6");
            AssertRejected(s, x => Card(x, "C13").state.starPip = 3, "only C137 has a star pip");
            AssertRejected(s, x => Card(x, "C122").state.cultivationFacesChanged = 25, "above the cap");
            AssertRejected(s, x => Card(x, "C13").state.cultivationFacesChanged = 1, "not a cultivation card");
            AssertRejected(s, x => Card(x, "C54").state.growthCatchup = 15, "not a whole number of cycle steps (10)");
            AssertRejected(s, x => Card(x, "C54").state.growthCatchup = 80, "more than seven later cycles");
            AssertRejected(s, x => Card(x, "C13").state.growthCatchup = 50, "not a growth card");
            AssertRejected(s, x => Card(x, "C54").state.growthCatchup = -10, "negative");
            // The legal neighbours of the rejected values load.
            var ok = s.State; Card(ok, "C54").state.growthCatchup = 70; Card(ok, "C122").state.cultivationFacesChanged = 24; Card(ok, "C137").state.starPip = 6;
            Assert.That(() => new GameSession(cfg, new MemorySaveStore(), ok), Throws.Nothing);
        }

        [Test] public void V111_ValidationTiesTheRecordFlagAndTheStarPipToTheStage()
        {
            var fresh = Ordinary("C137");
            AssertRejected(fresh, x => x.stage.handRecordBroken = true, "no submission and no record yet");
            AssertRejected(fresh, x => x.highestHandScore = "5", "a run without a submission has no record");
            var settled = Ordinary("C137");
            settled = new GameSession(cfg, new MemorySaveStore(), Mutate(settled.State, x => x.stage.score = (BigInteger.Parse(x.stage.target) - 1).ToString()));
            Do(settled, CommandKind.Roll); Lock(settled, settled.State.hand.currentResults.Select(r => r.dieId).Take(5).ToArray()); Do(settled, CommandKind.Submit);
            if (settled.State.phase != "StageSettlement") Do(settled, CommandKind.EndStage);
            Assert.That(Card(settled.State, "C137").state.starPip, Is.EqualTo(0));
            AssertRejected(settled, x => Card(x, "C137").state.starPip = 4, "no star pip between stages");
            AssertRejected(settled, x => { x.highestClearRatioNumerator = "0"; x.highestClearRatioDenominator = "1"; }, "below the ratio of the stage just settled");
            Assert.That(() => new GameSession(cfg, new MemorySaveStore(), settled.State), Throws.Nothing, "control: the untouched state loads");
        }
        private static RunState Mutate(RunState x, Action<RunState> edit) { edit(x); return x; }

        // ---- 9. debug scenes ----

        [Test] public void V111_ADebugSceneCarriesTheNewStateOutAndBackIn()
        {
            var s = Start(new[] { "C137", "C54", "C122", "C55" });
            var scene = s.ExportDebugScene();
            scene.controllers[0].state.starPip = 5;
            scene.controllers[1].state.growth = 30; scene.controllers[1].state.growthCatchup = 25;      // a debug scene is not bound to whole cycle steps
            scene.controllers[2].state.cultivationFacesChanged = 9;
            scene.controllers[3].state.growth = 7; scene.controllers[3].state.growthCatchup = 12;
            scene.dice[0].faces[2].cultivated = true;
            scene.highestHandScore = "1234"; scene.highestClearRatioNumerator = "13"; scene.highestClearRatioDenominator = "5"; scene.handRecordBroken = true;
            Do(s, CommandKind.ApplyDebugScene, c => c.debugScene = scene);
            var x = s.State;
            Assert.That(Card(x, "C137").state.starPip, Is.EqualTo(5));
            Assert.That(Card(x, "C54").state.growth + "/" + Card(x, "C54").state.growthCatchup, Is.EqualTo("30/25"));
            Assert.That(Card(x, "C122").state.cultivationFacesChanged, Is.EqualTo(9));
            Assert.That(Card(x, "C55").state.growth + "/" + Card(x, "C55").state.growthCatchup, Is.EqualTo("7/12"));
            Assert.That(x.dice[0].faces[2].cultivated, Is.True);
            Assert.That(x.highestHandScore + " " + x.highestClearRatioNumerator + "/" + x.highestClearRatioDenominator + " " + x.stage.handRecordBroken, Is.EqualTo("1234 13/5 True"));
            // Exported again, the scene carries the same values.
            var again = s.ExportDebugScene();
            Assert.That(again.controllers[0].state.starPip, Is.EqualTo(5)); Assert.That(again.controllers[2].state.cultivationFacesChanged, Is.EqualTo(9));
            Assert.That(again.dice[0].faces[2].cultivated, Is.True);
            Assert.That(again.highestHandScore + " " + again.highestClearRatioNumerator + "/" + again.highestClearRatioDenominator + " " + again.handRecordBroken, Is.EqualTo("1234 13/5 True"));
            // The growth and catch-up of the scene are what the scorer reads: red 3 + blue 4 = 7, C55 adds 12 + 12 + 7 = 31,
            // C54 multiplies by 1.25 + 0.25 + 0.30 = 1.8: 38 x 1.8 = 68.4 -> 68. Below 1234, so the debug record stands.
            s = Deal(s, new[] { 3, 4 }, R("red", "blue"));
            Assert.That(Submit(s).receipt.score, Is.EqualTo("68"));
            Assert.That(s.State.highestHandScore, Is.EqualTo("1234"));
        }

        [Test] public void V111_ADebugSceneWithInconsistentNewStateIsRejectedWithoutChangingAnything()
        {
            var s = Start(new[] { "C137", "C13" }); var before = s.State;
            Func<Action<DebugScene>, CommandResult> apply = edit => { var scene = s.ExportDebugScene(); edit(scene); return s.Execute(new SessionCommand { commandId = Guid.NewGuid().ToString("N"), expectedActionSequence = s.State.actionSequence, kind = CommandKind.ApplyDebugScene, debugScene = scene }); };
            Assert.That(apply(x => x.handRecordBroken = true).accepted, Is.False, "a record flag needs a record above 0");
            Assert.That(apply(x => x.controllers[0].state.starPip = 7).accepted, Is.False);
            Assert.That(apply(x => x.controllers[1].state.starPip = 2).accepted, Is.False, "not a C137");
            Assert.That(apply(x => { x.highestClearRatioNumerator = "4"; x.highestClearRatioDenominator = "2"; }).accepted, Is.False, "not reduced");
            Assert.That(apply(x => x.highestHandScore = "-1").accepted, Is.False);
            Assert.That(apply(x => x.controllers[1].state.cultivationFacesChanged = 1).accepted, Is.False, "not a cultivation card");
            Assert.That(GameSession.StateHash(s.State), Is.EqualTo(GameSession.StateHash(before)));
            Assert.That(apply(x => { x.highestHandScore = ""; x.highestClearRatioNumerator = ""; x.highestClearRatioDenominator = ""; x.controllers[0].state.starPip = 0; }).accepted, Is.True, "empty records mean a new run; star 0 means no value this stage");
            Assert.That(s.State.highestHandScore + " " + s.State.highestClearRatioNumerator + "/" + s.State.highestClearRatioDenominator, Is.EqualTo("0 0/1"));
        }
    }
}
