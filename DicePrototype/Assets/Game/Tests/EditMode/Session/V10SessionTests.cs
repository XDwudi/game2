using System;
using System.Linq;
using NUnit.Framework;
using DiceCraft.Core;

namespace DiceCraft.Tests
{
    /// <summary>
    /// Session rules added by p1.0.0 (docs/specs/v10/RULES_SPEC.md): the three-stage cycle, boss modifiers, the
    /// scapegoat and the talisman, boss loot and gift groups, the stage-start and stage-end controllers, the new
    /// items, die upgrades and services. Expected numbers are worked out here from the rule text.
    /// The reference hand is five red 4s with no roll left. p1.1.1: 20 base x 12 (five of a kind) x 4.5 (red group of 5) x (1 + 0.5 all red) = 1620.
    /// Stage targets (section 11): stage 2 is 320 (pressure 480), the first boss stage 500 (750 under the 150% wall).
    /// </summary>
    public sealed class V10SessionTests : SessionTestSupport
    {
        private static uint SeedWithFirstTicket(int range, int ticket)
        {
            for (uint seed = 1; seed < 200000; seed++) if (new GameRng(seed).NextIndex(range) == ticket) return seed;
            throw new InvalidOperationException("No seed found for ticket " + ticket);
        }
        private GameSession Force(GameSession s, int[] pips, string color) => Edit(s, x =>
        {
            x.phase = "Selecting"; x.hand.hasRolled = true; x.hand.rollsUsed = 1; x.hand.rollsRemaining = 0; x.hand.orderedLockedIds.Clear();
            for (int i = 0; i < x.hand.currentResults.Count; i++)
            {
                var r = x.hand.currentResults[i]; bool locked = i < pips.Length;
                x.dice.First(d => d.dieId == r.dieId).faces[0].baseColor = color;
                r.currentFaceId = "F01"; r.currentPip = locked ? pips[i] : 1; r.currentColor = color; r.hasRolled = true; r.remoteOverride = true; r.isLocked = locked;
                if (locked) x.hand.orderedLockedIds.Add(r.dieId);
            }
        });
        private static readonly int[] Fours = { 4, 4, 4, 4, 4 };
        private static long Growth(GameSession s, string id) => s.State.controllers.Single(c => c.definitionId == id).state.growth;

        /// <summary>The shop after the given stage (0-based), in a debug workspace, with the settlement receipt kept consistent.</summary>
        private GameSession ShopAfterStage(int index, Action<RunState> edit = null) => Edit(Shop(), x =>
        {
            var def = cfg.stages[index]; x.stage.stageIndex = index; x.stage.stageId = def.stageId; x.stage.stageKind = def.stageKind; x.stage.target = def.normalTarget; x.stage.score = def.normalTarget;
            x.stage.settlementId = x.runId + "/" + def.stageId; x.lastStageSettlement.settlementId = x.stage.settlementId; x.lastStageSettlement.stageId = def.stageId;
            x.lastStageSettlement.score = x.stage.score; x.lastStageSettlement.target = x.stage.target; x.lastStageSettlement.stageKind = def.stageKind;
            edit?.Invoke(x);
        });
        /// <summary>The stage-select screen of the first boss (stage 3) with the given modifier planned for it.</summary>
        private GameSession BossPreview(string modifierId, Action<RunState> edit, params string[] controllers)
        {
            var s = ShopAfterStage(1, x => { x.cycles[0].modifierId = modifierId; Equip(x, controllers); edit?.Invoke(x); });
            Do(s, CommandKind.LeaveShop); return s;
        }
        private GameSession Boss(string modifierId, params string[] controllers) { var s = BossPreview(modifierId, null, controllers); Do(s, CommandKind.StartStage); return s; }
        private string Submit(GameSession s) => Do(s, CommandKind.Submit).receipt.score;

        // ---- cycle plan ----

        [Test] public void V10_ANewRunPlansEightCyclesWithTieredModifiersNoRepeatsAndLootForTheFirstSeven()
        {
            for (uint seed = 1; seed <= 60; seed++)
            {
                var plan = New(seed).State.cycles; Assert.That(plan.Count, Is.EqualTo(8), "seed " + seed);
                for (int i = 0; i < 8; i++)
                {
                    var m = cfg.Modifier(plan[i].modifierId);
                    if (i == 7) { Assert.That(m.tier, Is.EqualTo(4)); Assert.That(plan[i].lootKind, Is.Empty); continue; }
                    Assert.That(m.tier, Is.LessThan(4)); Assert.That(m.minCycle, Is.LessThanOrEqualTo(i + 1), "seed " + seed + " cycle " + (i + 1));
                    Assert.That(cfg.lootKinds.Any(k => k.id == plan[i].lootKind), Is.True);
                    if (i > 0) Assert.That(plan[i].lootKind, Is.Not.EqualTo(plan[i - 1].lootKind), "loot never repeats in consecutive cycles");
                }
                Assert.That(cfg.Modifier(plan[0].modifierId).tier, Is.EqualTo(1), "the first boss only draws tier 1");
                Assert.That(plan.Take(3).All(c => cfg.Modifier(c.modifierId).tier <= 2), Is.True, "tier 3 starts at cycle 4");
                Assert.That(plan.Select(c => c.modifierId).Distinct().Count(), Is.EqualTo(8));
            }
            var a = New(7).State; var b = New(7); Do(b, CommandKind.StartStage); Do(b, CommandKind.Roll);
            Assert.That(b.State.cycles.Select(c => c.modifierId + c.lootKind), Is.EqualTo(a.cycles.Select(c => c.modifierId + c.lootKind)), "the plan depends on the seed only");
            Assert.That(b.State.stageRngState, Is.EqualTo(a.stageRngState));
        }

        [Test] public void V10_StageKindsFollowTheCycleAndOnlyThePressureStageOffersTheHarderChallenge()
        {
            for (int i = 0; i < cfg.stages.Length; i++) Assert.That(cfg.stages[i].stageKind, Is.EqualTo(new[] { "normal", "pressure", "boss" }[i % 3]), "stage " + (i + 1));
            var first = New(); var refuse = Command(first, CommandKind.ChooseChallenge); refuse.risk = true;
            Assert.That(first.Execute(refuse).accepted, Is.False, "a normal stage has no pressure option");
            var second = ShopAfterStage(0); Do(second, CommandKind.LeaveShop);
            Assert.That(second.State.stage.stageKind, Is.EqualTo("pressure")); Assert.That(second.State.stage.modifierId ?? "", Is.Empty);
            Do(second, CommandKind.ChooseChallenge, c => c.risk = true);
            Assert.That(second.State.stage.target, Is.EqualTo("480"), "320 x 1.5"); Assert.That(second.State.stage.reward, Is.EqualTo(10));
            var boss = BossPreview("B05", null); refuse = Command(boss, CommandKind.ChooseChallenge); refuse.risk = true;
            Assert.That(boss.Execute(refuse).accepted, Is.False, "a boss stage has no pressure option");
            Assert.That(boss.State.stage.stageKind, Is.EqualTo("boss")); Assert.That(boss.State.stage.modifierId, Is.EqualTo("B05"));
        }

        [Test] public void V10_CycleViewShowsTheThreeStagesOfTheCycleWithModifierLootAndProgress()
        {
            var s = New(3); var view = s.CycleView(); var plan = s.State.cycles[0];
            Assert.That(view.Select(v => v.stageKind), Is.EqualTo(new[] { "normal", "pressure", "boss" }));
            Assert.That(view[0].current, Is.True); Assert.That(view[0].cleared, Is.False); Assert.That(view[1].riskTarget, Is.EqualTo("480")); Assert.That(view[1].riskReward, Is.EqualTo(10));
            Assert.That(view[2].modifierId, Is.EqualTo(plan.modifierId)); Assert.That(view[2].lootKind, Is.EqualTo(plan.lootKind)); Assert.That(view[0].modifierId, Is.Empty);
            var shop = ShopAfterStage(0); view = shop.CycleView();
            Assert.That(view[0].cleared, Is.True); Assert.That(view[1].current, Is.True, "in the shop the panel looks at the next stage");
            var afterBoss = ShopAfterStage(2); view = afterBoss.CycleView();
            Assert.That(view[0].stageIndex, Is.EqualTo(3), "after a boss the panel shows the next cycle"); Assert.That(view[2].modifierId, Is.EqualTo(afterBoss.State.cycles[1].modifierId));
            var wall = ShopAfterStage(1, x => x.cycles[0].modifierId = "B11");
            Assert.That(wall.CycleView()[2].bossTarget, Is.EqualTo("750"), "500 x 1.5");
        }

        // ---- budget modifiers ----

        [TestCase("B09", "", 2)]
        [TestCase("B09", "C78", 3)]
        [TestCase("B32", "C78", 1)]
        [TestCase("B05", "C78", 4)]
        public void V10_RollModifiersChangeTheBaseBudget(string modifier, string controllers, int rolls)
        {
            var s = Boss(modifier, controllers.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(rolls)); Assert.That(s.InitialRolls, Is.EqualTo(rolls));
        }

        [Test] public void V10_TheFixedRollBudgetStillAcceptsTheExtraRollItem()
        {
            string item = null; var s = Edit(Boss("B32"), x => item = AddItem(x, "I07"));
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = item);
            Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(2)); Assert.That(s.InitialRolls, Is.EqualTo(2));
        }

        [TestCase("B27", "", 2)]
        [TestCase("B33", "", 1)]
        [TestCase("B33", "C101", 1)]
        [TestCase("B27", "C79", 3)]
        public void V10_SubmissionModifiersNeverGoBelowOne(string modifier, string controllers, int submissions)
        {
            var s = Boss(modifier, controllers.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            Assert.That(s.State.stage.remainingSubmissions, Is.EqualTo(submissions));
        }

        [TestCase("B10", "", 4)]
        [TestCase("B34", "", 3)]
        [TestCase("B34", "C22", 5)]
        public void V10_CapacityModifiersShrinkTheSubmitRow(string modifier, string controllers, int capacity)
        {
            var s = Boss(modifier, controllers.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            Assert.That(s.State.stage.capacity, Is.EqualTo(capacity)); Assert.That(s.EffectiveCapacity, Is.EqualTo(capacity));
        }

        [Test] public void V10_ThePreviewQuotesBudgetsTargetAndDisabledCardsForTheBossThatIsComing()
        {
            var s = BossPreview("B09", null, "C78");
            Assert.That(s.InitialRolls, Is.EqualTo(3), "3 + C78 - 1"); Assert.That(s.EntryView().modifierId, Is.EqualTo("B09"));
            s = BossPreview("B11", null);
            Assert.That(s.State.stage.target, Is.EqualTo("750")); Assert.That(s.EntryView().target, Is.EqualTo("750"));
            s = BossPreview("B25", null, "C50", "C46", "C13");
            Assert.That(s.EntryView().disabledControllerIds, Is.EqualTo(s.State.controllers.Take(2).Select(c => c.instanceId).ToArray()));
            s = BossPreview("B27", null, "C79");
            Assert.That(s.InitialSubmissions, Is.EqualTo(3)); Assert.That(s.State.stage.remainingSubmissions, Is.EqualTo(3));
        }

        // ---- session modifiers ----

        [Test] public void V10_MustFillRejectsAShortSubmissionAndAcceptsAFullRow()
        {
            var s = Force(Boss("B01"), new[] { 4, 4, 4 }, "red"); var before = s.State;
            Assert.That(s.Execute(Command(s, CommandKind.Submit)).accepted, Is.False); AssertUnchanged(before, s.State);
            s = Force(s, Fours, "red"); Assert.That(Submit(s), Is.EqualTo("1620"));
        }

        [Test] public void V10_NoItemsBlocksEveryItemExceptTheTalisman()
        {
            string extra = null, talisman = null; var s = Edit(Boss("B02"), x => { extra = AddItem(x, "I06"); talisman = AddItem(x, "I08"); });
            var blocked = Command(s, CommandKind.UseItem); blocked.itemInstanceId = extra; var before = s.State;
            Assert.That(s.Execute(blocked).accepted, Is.False); AssertUnchanged(before, s.State);
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = talisman);
            Assert.That(s.State.stage.modifierNullified, Is.True); Assert.That(s.CurrentModifierId, Is.Null);
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = extra);
            Assert.That(s.State.stage.remainingSubmissions, Is.EqualTo(4));
        }

        [TestCase(10, 5)]
        [TestCase(3, 0)]
        public void V10_TheTaxTakesOneGoldPerSubmittedDieDownToZero(int gold, int left)
        {
            var s = Force(Edit(Boss("B03"), x => x.gold = gold), Fours, "red"); Submit(s);
            Assert.That(s.State.gold, Is.EqualTo(left));
        }

        [Test] public void V10_NoRewardVoidsTheStageRewardButKeepsInterest()
        {
            var s = Force(Edit(Boss("B04"), x => x.gold = 12), Fours, "red"); Submit(s); var end = Do(s, CommandKind.EndStage).receipt.settlement;
            Assert.That(end.rewardVoided, Is.True); Assert.That(end.baseReward, Is.Zero); Assert.That(end.unusedSubmissionReward, Is.Zero);
            Assert.That(end.interest, Is.EqualTo(2), "12 gold: one per five"); Assert.That(end.goldAfter, Is.EqualTo(14));
        }

        [Test] public void V10_TheTollTakesHalfTheGoldRoundedDownOnEntry()
        {
            var s = BossPreview("B12", x => x.gold = 11); var start = Do(s, CommandKind.StartStage);
            Assert.That(s.State.gold, Is.EqualTo(6)); Assert.That(start.receipt.effects.Any(e => e.kind == "GoldLost" && e.oldValue == 11 && e.newValue == 6), Is.True);
        }

        [Test] public void V10_GrowthFreezeStopsEveryGrowthValueForTheStageIncludingItsSettlement()
        {
            var s = Boss("B07", "C62", "C55", "C58");
            Do(s, CommandKind.Roll); Assert.That(Growth(s, "C62"), Is.Zero, "rolling does not grow C62");
            s = Force(s, Fours, "red"); Submit(s); Assert.That(Growth(s, "C55"), Is.Zero, "submitting does not grow C55");
            Do(s, CommandKind.EndStage); Assert.That(Growth(s, "C58"), Is.Zero, "clearing the stage does not grow C58");
            var free = Force(Boss("B05", "C55", "C58"), Fours, "red"); Submit(free); Do(free, CommandKind.EndStage);
            Assert.That(Growth(free, "C55"), Is.EqualTo(3), "p1.1.1: +3 per submission"); Assert.That(Growth(free, "C58"), Is.EqualTo(15), "p1.1.1: +0.15 per cleared stage");
        }

        [Test] public void V10_HandDowngradeLowersThePrimaryHandAfterAnyUpgrade()
        {
            var s = Force(Edit(Boss("B24"), x => x.handLevels.Single(h => h.handId == "five_kind").level = 3), Fours, "red"); Submit(s);
            Assert.That(s.State.handLevels.Single(h => h.handId == "five_kind").level, Is.EqualTo(2));
            s = Force(Edit(Boss("B24", "C43"), x => { x.handLevels.Single(h => h.handId == "five_kind").level = 3; x.previousPrimaryHandId = "five_kind"; }), Fours, "red"); Submit(s);
            Assert.That(s.State.handLevels.Single(h => h.handId == "five_kind").level, Is.EqualTo(3), "+1 from C43, then -1");
            s = Force(Boss("B24"), Fours, "red"); Submit(s);
            Assert.That(s.State.handLevels.Single(h => h.handId == "five_kind").level, Is.Zero, "never below level 0");
        }

        [Test] public void V10_DisabledControllersHaveNoAbilityBudgetOrEnchantButStayInPlace()
        {
            var s = Boss("B25", "C50", "C46", "C13");
            Assert.That(s.State.stage.disabledControllerIds, Is.EqualTo(s.State.controllers.Take(2).Select(c => c.instanceId).ToArray()));
            s = Force(s, Fours, "red"); var receipt = Do(s, CommandKind.Submit).receipt;
            Assert.That(receipt.score, Is.EqualTo("1620"), "C50 (+30 base) and C46 (+2 bonus) are disabled");
            Assert.That(receipt.submission.snapshot.disabledControllerIds.Length, Is.EqualTo(2)); Assert.That(receipt.submission.breakdown.triggeredSourceIds, Is.Empty);
            Assert.That(s.State.controllers.Count, Is.EqualTo(3), "a disabled card is not removed");
            Assert.That(Boss("B25", "C79", "C13", "C78").State.stage.remainingSubmissions, Is.EqualTo(3), "a disabled C79 adds no submission");
            Assert.That(Boss("B25", "C13", "C50", "C79").State.stage.remainingSubmissions, Is.EqualTo(4), "the third card is not disabled");
            var rare = Boss("B29", "C09", "C13");
            Assert.That(rare.State.stage.capacity, Is.EqualTo(5), "a disabled C09 does not shrink the row"); Assert.That(rare.State.stage.disabledControllerIds.Length, Is.EqualTo(1));
            var mimic = Force(Boss("B25", "C13", "C50", "C41", "C46"), Fours, "red");
            Assert.That(Submit(mimic), Is.EqualTo("5940"), "C13 and C50 disabled; the mimic copies the live C46, +2 each: 20 x 12 x 4.5 x (1 + 0.5 + 2 + 2)");
            mimic = Force(Boss("B25", "C41", "C46", "C13"), Fours, "red");
            Assert.That(Submit(mimic), Is.EqualTo("1620"), "a disabled mimic and a disabled target give nothing");
        }

        [Test] public void V10_EnchantSilenceStopsBudgetEnchantsAndScoreEnchants()
        {
            Action<RunState> enchant = x => { x.controllers[0].enchantmentId = "roll1"; x.controllers[1].enchantmentId = "submit1"; x.controllers[2].enchantmentId = "mult2"; };
            var s = BossPreview("B06", enchant, "C13", "C10", "C50"); Do(s, CommandKind.StartStage);
            Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(3)); Assert.That(s.State.stage.remainingSubmissions, Is.EqualTo(3));
            s = Force(s, Fours, "red"); Assert.That(Submit(s), Is.EqualTo("4050"), "(20 + 30) x 12 x 4.5 x 1.5; the +1.5 enchant of C50 is silent");
            var loud = BossPreview("B05", enchant, "C13", "C10", "C50"); Do(loud, CommandKind.StartStage);
            Assert.That(loud.State.hand.rollsRemaining, Is.EqualTo(4)); Assert.That(loud.State.stage.remainingSubmissions, Is.EqualTo(4));
            var final = BossPreview("B37", enchant, "C13", "C10", "C50", "C46"); Do(final, CommandKind.StartStage);
            // p1.1.1 section 10: B37 disables the two leftmost cards and silences every enchantment. The third card (C50)
            // still acts, its +1.5 enchantment does not: (20 + 30) x 12 x 4.5 x (1 + 0.5 + 2 from C46) = 9450.
            Assert.That(final.State.stage.disabledControllerIds.Length, Is.EqualTo(2)); Assert.That(final.State.hand.rollsRemaining, Is.EqualTo(3));
            final = Force(final, Fours, "red"); Assert.That(Submit(final), Is.EqualTo("9450"));
        }

        [Test] public void V10_ScoringModifiersReachTheScoreThroughTheSession()
        {
            Assert.That(Submit(Force(Boss("B19"), Fours, "red")), Is.EqualTo("810"), "hand multiplier 12 halves to 6: 20 x 6 x 4.5 x 1.5");
            Assert.That(Submit(Force(Boss("B05"), Fours, "red")), Is.EqualTo("1296"), "the first die is sealed: 16 x 12 x 4.5 x 1.5");
            Assert.That(Submit(Force(Boss("B22"), Fours, "red")), Is.EqualTo("240"), "red group x1 and no all-red bonus: 20 x 12");
            var slow = Force(Boss("B26"), Fours, "red"); Assert.That(Submit(slow), Is.EqualTo("810"), "first submission halved");
            slow = Force(slow, Fours, "red"); Assert.That(Submit(slow), Is.EqualTo("1620"));
            var tired = Force(Boss("B30"), Fours, "red"); Assert.That(Submit(tired), Is.EqualTo("1620"));
            tired = Force(tired, Fours, "red"); Assert.That(Submit(tired), Is.EqualTo("810"), "second submission halved");
            var repeat = Force(Boss("B21"), Fours, "red"); Assert.That(Submit(repeat), Is.EqualTo("1620"));
            Assert.That(repeat.State.stage.primaryHandsUsed, Is.EqualTo(new[] { "five_kind" }));
            repeat = Force(repeat, Fours, "red"); Assert.That(Submit(repeat), Is.EqualTo("135"), "the same primary hand scores x1 the second time: 20 x 1 x 4.5 x 1.5");
            Assert.That(Do(Force(Boss("B19"), Fours, "red"), CommandKind.Submit).receipt.submission.snapshot.modifierId, Is.EqualTo("B19"));
        }

        // ---- scapegoat and talisman ----

        [Test] public void V10_TheScapegoatDestroysItselfAndLiftsTheModifierBeforeItApplies()
        {
            var s = BossPreview("B09", null, "C13", "C131");
            Assert.That(s.EntryView().scapegoat, Is.True); Assert.That(s.InitialRolls, Is.EqualTo(3), "the preview already counts on the scapegoat");
            var start = Do(s, CommandKind.StartStage);
            Assert.That(s.State.controllers.Select(c => c.definitionId), Is.EqualTo(new[] { "C13" })); Assert.That(s.State.stage.modifierNullified, Is.True);
            Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(3)); Assert.That(start.receipt.effects.Any(e => e.kind == "ModifierNullified" && e.sourceId == "C131"), Is.True);
            var wall = BossPreview("B11", x => x.gold = 9, "C131"); Do(wall, CommandKind.StartStage);
            Assert.That(wall.State.stage.target, Is.EqualTo("500"));
            var toll = BossPreview("B12", x => x.gold = 9, "C131"); Do(toll, CommandKind.StartStage);
            Assert.That(toll.State.gold, Is.EqualTo(9), "the toll never applies");
            var plain = Edit(ShopAfterStage(0, x => Equip(x, "C131")), x => { }); Do(plain, CommandKind.LeaveShop); Do(plain, CommandKind.StartStage);
            Assert.That(plain.State.controllers.Count, Is.EqualTo(1), "the scapegoat only acts on a boss stage");
        }

        [Test] public void V10_TheTalismanRestoresBudgetsTargetAndDisabledCardsAndKeepsEarlierItemBonuses()
        {
            string extra = null, talisman = null;
            var s = Edit(Boss("B27"), x => { extra = AddItem(x, "I06"); talisman = AddItem(x, "I08"); });
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = extra); Assert.That(s.State.stage.remainingSubmissions, Is.EqualTo(3));
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = talisman); Assert.That(s.State.stage.remainingSubmissions, Is.EqualTo(4), "3 base + 1 from the item");
            s = Edit(Boss("B09"), x => talisman = AddItem(x, "I08")); Do(s, CommandKind.UseItem, c => c.itemInstanceId = talisman);
            Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(3));
            s = Edit(Boss("B11"), x => talisman = AddItem(x, "I08")); Assert.That(s.State.stage.target, Is.EqualTo("750"));
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = talisman); Assert.That(s.State.stage.target, Is.EqualTo("500"));
            s = Edit(Boss("B25", "C50", "C46"), x => talisman = AddItem(x, "I08")); Do(s, CommandKind.UseItem, c => c.itemInstanceId = talisman);
            Assert.That(s.State.stage.disabledControllerIds, Is.Empty);
            Assert.That(Submit(Force(s, Fours, "red")), Is.EqualTo("9450"), "(20 + 30) x 12 x 4.5 x (1 + 0.5 + 2)");
            s = Edit(Boss("B10"), x => talisman = AddItem(x, "I08")); Do(s, CommandKind.UseItem, c => c.itemInstanceId = talisman);
            Assert.That(s.State.stage.capacity, Is.EqualTo(5));
        }

        [Test] public void V10_TheTalismanOnlyWorksBeforeTheFirstRollOfABossStageWithALiveModifier()
        {
            string talisman = null;
            var late = Edit(Boss("B09"), x => talisman = AddItem(x, "I08")); Do(late, CommandKind.Roll);
            var use = Command(late, CommandKind.UseItem); use.itemInstanceId = talisman; Assert.That(late.Execute(use).accepted, Is.False, "after the first roll");
            var normal = Edit(Ready(), x => talisman = AddItem(x, "I08"));
            use = Command(normal, CommandKind.UseItem); use.itemInstanceId = talisman; Assert.That(normal.Execute(use).accepted, Is.False, "not a boss stage");
            var gone = BossPreview("B09", x => talisman = AddItem(x, "I08"), "C131"); Do(gone, CommandKind.StartStage);
            use = Command(gone, CommandKind.UseItem); use.itemInstanceId = talisman; Assert.That(gone.Execute(use).accepted, Is.False, "the scapegoat already lifted it");
            Assert.That(gone.State.inventory.Count, Is.EqualTo(1), "a rejected use keeps the item");
        }

        // ---- stage-start and stage-end controllers ----

        [Test] public void V10_StageStartControllersActLeftToRightAndDisabledOnesDoNot()
        {
            // p1.1.1: C128 was removed; the stage-start abilities are now C137 (star pip), C136 and C110, left to right.
            var s = Ready("C137", "C136", "C110"); var state = s.State;
            Assert.That(state.controllers[0].state.starPip, Is.InRange(1, 6), "C137 draws its star pip for the stage");
            Assert.That(state.processedReceipts.Last().randomResults.Select(r => r.kind), Is.EqualTo(new[] { "C137Star", "C110Item" }), "left to right on the effect stream");
            Assert.That(state.dice.Count(d => d.isGlass), Is.Zero, "no card turns a die to glass any more");
            Assert.That(state.dice.Count, Is.EqualTo(6)); Assert.That(state.dice.Count(d => d.lifetime == "stage_temporary"), Is.EqualTo(1));
            Assert.That(state.hand.currentResults.Count, Is.EqualTo(6), "the temporary die joins the first hand");
            Assert.That(state.inventory.Count, Is.EqualTo(1)); Assert.That(cfg.consumables.Any(c => c.id == state.inventory[0].contentId), Is.True);
            var full = Edit(New(), x => { Equip(x, "C110"); AddItem(x, "I06"); AddItem(x, "I06"); x.consumableSlots = 2; }); Do(full, CommandKind.StartStage);
            Assert.That(full.State.inventory.Count, Is.EqualTo(2), "no room, no item");
            var disabled = Boss("B25", "C136", "C110", "C13");
            Assert.That(disabled.State.dice.Count, Is.EqualTo(5)); Assert.That(disabled.State.inventory, Is.Empty);
            var two = Ready("C41", "C136");
            Assert.That(two.State.dice.Count(d => d.lifetime == "stage_temporary"), Is.EqualTo(2), "the mimic is a second source");
            var star = Ready("C41", "C137");
            Assert.That(star.State.processedReceipts.Last().randomResults.Count(r => r.kind == "C137Star"), Is.EqualTo(1), "only the real C137 draws; its mimic shares the value");
            Assert.That(star.State.controllers[0].state.starPip, Is.Zero);
        }

        [Test] public void V10_StageEndFaceGrowthRaisesTheThreeLowestAndLowersTheThreeHighestFacesOfEveryPermanentDie()
        {
            // p1.1.1 section 6.1: three faces per die. C120: 1,2,3 -> 2,3,4. C121: 6,5,4 -> 5,4,3. Both: every C120 first
            // (2,3,4,4,5,6), then C121 lowers the 6, the 5 and the first 4 by face id (F03).
            var s = Force(Ready("C120"), Fours, "red"); Submit(s); Do(s, CommandKind.EndStage);
            foreach (var die in s.State.dice) Assert.That(die.faces.OrderBy(f => f.faceId).Select(f => f.permanentPip), Is.EqualTo(new[] { 2, 3, 4, 4, 5, 6 }), die.dieId);
            s = Force(Ready("C121"), Fours, "red"); Submit(s); Do(s, CommandKind.EndStage);
            foreach (var die in s.State.dice) Assert.That(die.faces.OrderBy(f => f.faceId).Select(f => f.permanentPip), Is.EqualTo(new[] { 1, 2, 3, 3, 4, 5 }), die.dieId);
            s = Force(Ready("C120", "C121"), Fours, "red"); Submit(s); Do(s, CommandKind.EndStage);
            foreach (var die in s.State.dice) Assert.That(die.faces.OrderBy(f => f.faceId).Select(f => f.permanentPip), Is.EqualTo(new[] { 2, 3, 3, 4, 4, 5 }), die.dieId);
        }

        [Test] public void V10_TheFadingCardCountsStagesAddsLessEachTimeAndExpires()
        {
            // p1.1.1 sections 4.2 and 5.1: bonus +4 minus one per cleared stage; removed at the fourth decay.
            // Reference hand 20 x 12 x 4.5 x (1 + 0.5 + bonus): +4 -> 5940, +3 -> 4860, +2 -> 3780, +1 -> 2700.
            var s = Force(Ready("C141"), Fours, "red"); Assert.That(Submit(s), Is.EqualTo("5940"), "+4 when new");
            Do(s, CommandKind.EndStage); Assert.That(Growth(s, "C141"), Is.EqualTo(1));
            var worn = Force(Edit(Ready("C141"), x => x.controllers[0].state.growth = 1), Fours, "red"); Assert.That(Submit(worn), Is.EqualTo("4860"), "+3 after one stage");
            var later = Force(Edit(Ready("C141"), x => x.controllers[0].state.growth = 2), Fours, "red"); Assert.That(Submit(later), Is.EqualTo("3780"), "+2 after two stages");
            Do(later, CommandKind.EndStage); Assert.That(Growth(later, "C141"), Is.EqualTo(3), "the third decay does not remove it yet");
            var last = Force(Edit(Ready("C141"), x => x.controllers[0].state.growth = 3), Fours, "red"); Assert.That(Submit(last), Is.EqualTo("2700"), "+1 on its last stage");
            var end = Do(last, CommandKind.EndStage);
            Assert.That(last.State.controllers, Is.Empty); Assert.That(end.receipt.effects.Any(e => e.kind == "ControllerExpired" && e.sourceId == "C141"), Is.True);
        }

        [Test] public void V10_BossAndPressureClearsGrowTheirCardsAndPayTheBounty()
        {
            var boss = Force(Boss("B05", "C129", "C132", "C133"), Fours, "red"); Submit(boss); var end = Do(boss, CommandKind.EndStage).receipt.settlement;
            Assert.That(Growth(boss, "C129"), Is.EqualTo(250), "p1.1.1: +2.5 per cleared boss stage"); Assert.That(Growth(boss, "C132"), Is.Zero);
            Assert.That(end.bountyIncome, Is.EqualTo(4)); Assert.That(end.controllerIncome, Is.EqualTo(4)); Assert.That(end.stageKind, Is.EqualTo("boss")); Assert.That(end.lootKind, Is.EqualTo(boss.State.cycles[0].lootKind));
            var pressure = ShopAfterStage(0, x => Equip(x, "C129", "C132", "C133")); Do(pressure, CommandKind.LeaveShop); Do(pressure, CommandKind.ChooseChallenge, c => c.risk = true); Do(pressure, CommandKind.StartStage);
            pressure = Force(pressure, Fours, "red"); Submit(pressure); end = Do(pressure, CommandKind.EndStage).receipt.settlement;
            Assert.That(Growth(pressure, "C132"), Is.EqualTo(250), "p1.1.1: +2.5 per cleared pressure challenge"); Assert.That(Growth(pressure, "C129"), Is.Zero);
            Assert.That(end.bountyIncome, Is.EqualTo(2)); Assert.That(end.baseReward, Is.EqualTo(10)); Assert.That(end.lootKind ?? "", Is.Empty);
            var calm = Force(Ready("C129", "C132", "C133"), Fours, "red"); Submit(calm); end = Do(calm, CommandKind.EndStage).receipt.settlement;
            Assert.That(end.bountyIncome, Is.Zero); Assert.That(Growth(calm, "C129") + Growth(calm, "C132"), Is.Zero);
        }

        [Test] public void V10_StageEndGeneratorsGrantACommonCardRandomFaceUpgradesAndAfterABossAnEnchantAndADie()
        {
            var s = Force(Ready("C112", "C115"), Fours, "red"); Submit(s);
            Func<DieInstance, string> faces = d => string.Join(",", d.faces.Select(f => f.faceId + ":" + f.permanentPip + f.baseColor + f.texture + f.isGold));
            var facesBefore = s.State.dice.ToDictionary(d => d.dieId, faces); var end = Do(s, CommandKind.EndStage);
            // p1.1.1 section 6.1: C115 only draws faces it can improve, so every one of its three upgrades changes a face.
            foreach (var applied in end.receipt.effects.Where(e => e.sourceId == "C115" && e.kind == "RandomUpgrade"))
                Assert.That(faces(s.State.dice.Single(d => d.dieId == applied.dieId)), Is.Not.EqualTo(facesBefore[applied.dieId]), "C115 changed " + applied.dieId);
            var granted = s.State.controllers.Last();
            Assert.That(s.State.controllers.Count, Is.EqualTo(3)); Assert.That(cfg.Controller(granted.definitionId).rarity, Is.EqualTo("common")); Assert.That(granted.enchantmentId, Is.EqualTo("none"));
            Assert.That(end.receipt.effects.Count(e => e.sourceId == "C115" && e.kind == "RandomUpgrade"), Is.EqualTo(3)); Assert.That(end.receipt.effects.Any(e => e.kind == "RandomUpgradeMissed"), Is.False);
            Assert.That(end.receipt.effects.Where(e => e.sourceId == "C115" && e.kind == "RandomUpgrade").Select(e => e.dieId + "/" + e.faceId).Distinct().Count(), Is.EqualTo(3), "three different faces");
            Assert.That(s.State.controllers.Single(c => c.definitionId == "C115").state.cultivationFacesChanged, Is.EqualTo(3));
            var full = Edit(New(), x => { Equip(x, "C112", "C13", "C10", "C50", "C46"); x.controllerSlots = 5; });
            Do(full, CommandKind.StartStage); full = Force(full, Fours, "red"); Submit(full); Do(full, CommandKind.EndStage);
            Assert.That(full.State.controllers.Count, Is.EqualTo(5), "no free ordinary slot, no card");
            var boss = Force(Boss("B05", "C114", "C116", "C13"), Fours, "red"); Submit(boss); end = Do(boss, CommandKind.EndStage);
            Assert.That(boss.State.controllers.Single(c => c.definitionId == "C114").enchantmentId, Is.EqualTo("none"), "it never enchants itself");
            Assert.That(boss.State.controllers.Count(c => c.enchantmentId != "none"), Is.EqualTo(1));
            Assert.That(boss.State.dice.Count(d => d.lifetime == "permanent"), Is.EqualTo(6));
            Assert.That(end.receipt.effects.Any(e => e.kind == "ControllerEnchanted" && e.sourceId == "C114"), Is.True);
            var calm = Force(Ready("C114", "C116", "C13"), Fours, "red"); Submit(calm); Do(calm, CommandKind.EndStage);
            Assert.That(calm.State.controllers.All(c => c.enchantmentId == "none"), Is.True, "only after a boss stage"); Assert.That(calm.State.dice.Count, Is.EqualTo(5));
        }

        [Test] public void V10_NoCardLowersTheGlassBreakChanceAnyMore()
        {
            // p1.1.1: C145 (the glass film) was removed. The chance is 15% for every run, judged after the submission
            // (section 7.3): the tickets the film used to save (2, 7) break now, and so does 14; 15 does not.
            foreach (int ticket in new[] { 1, 2, 7, 14, 15 })
            {
                var s = Edit(Force(Ready("C13", "C12"), new[] { 4 }, "red"), x => { x.dice[0].isGlass = true; x.effectRngState = SeedWithFirstTicket(100, ticket); });
                Assert.That(s.GlassBreakPercentIn(s.State), Is.EqualTo(15)); Submit(s);
                Assert.That(s.State.dice.Count, Is.EqualTo(ticket < 15 ? 4 : 5), "15 percent, ticket " + ticket);
            }
        }

        // ---- submission effects ----

        [Test] public void V10_FaceChangeCardsRewriteThePermanentFacesAfterTheScore()
        {
            var s = Force(Ready("C117", "C124"), Fours, "red"); Submit(s);
            // p1.1.1: C117 gilds the first and the last submitted die (weight +1 for a newly gold face), then C124 adds
            // 2 weight to every submitted face: 1 + 1 + 2 = 4 on the two gilded faces, 1 + 2 = 3 on the others.
            Assert.That(s.State.dice.OrderBy(d => d.createdSerial).Select(d => d.faces.Single(f => f.faceId == "F01").isGold), Is.EqualTo(new[] { true, false, false, false, true }), "C117 gilds the first and last submitted dice");
            Assert.That(s.State.dice.OrderBy(d => d.createdSerial).Select(d => d.faces.Single(f => f.faceId == "F01").baseWeight), Is.EqualTo(new[] { 4, 3, 3, 3, 4 }));
            s = Force(Ready("C118"), Fours, "red"); Submit(s);
            Assert.That(s.State.dice.All(d => d.faces.Single(f => f.faceId == "F06").baseColor == "red"), Is.True, "the opposite of F01 is F06");
            s = Force(Ready("C119"), Fours, "red"); Submit(s);
            Assert.That(s.State.dice.OrderBy(d => d.createdSerial).First().faces.OrderBy(f => f.faceId).Select(f => f.permanentPip), Is.EqualTo(new[] { 1, 1, 1, 4, 5, 6 }), "p1.1.1: F02 and F03 take the permanent pip of F01");
            Assert.That(s.State.dice.OrderBy(d => d.createdSerial).Last().faces.OrderBy(f => f.faceId).Select(f => f.permanentPip), Is.EqualTo(new[] { 1, 1, 1, 4, 5, 6 }), "the last die too");
            Assert.That(s.State.dice.OrderBy(d => d.createdSerial).ElementAt(2).faces.OrderBy(f => f.faceId).Select(f => f.permanentPip), Is.EqualTo(new[] { 1, 2, 3, 4, 5, 6 }), "the dice in between are untouched");
            s = Force(Ready("C122"), Fours, "red"); Submit(s);
            Assert.That(s.State.dice.All(d => d.faces.Single(f => f.faceId == "F01").permanentPip == 5), Is.True, "p1.1.1: permanent 1 is set to 5");
            s = Force(Ready("C123"), Fours, "red"); Submit(s);
            Assert.That(s.State.dice.All(d => d.faces.Single(f => f.faceId == "F01").permanentPip == 2), Is.True);
            s = Force(Ready("C125"), Fours, "red"); Submit(s);
            Assert.That(s.State.dice.All(d => d.faces.All(f => f.texture == "none" && !f.cultivated)), Is.True, "p1.1.1: C125 is a growth card now and rewrites no face");
            s = Force(Ready("C126"), new[] { 4, 4, 4 }, "red"); Submit(s);
            Assert.That(s.State.dice.OrderBy(d => d.createdSerial).Select(d => d.faces.Single(f => f.faceId == "F01").permanentPip), Is.EqualTo(new[] { 1, 1, 1, 3, 3 }), "p1.1.1: the two pool dice grow by 2");
            var preview = Force(Ready("C117"), Fours, "red");
            Assert.That(preview.PreviewSubmissionEffects().Any(e => e.kind == "FaceChanged" && e.sourceId == "C117"), Is.True, "the preview shows the change");
            Assert.That(preview.State.dice.Any(d => d.faces.Any(f => f.isGold)), Is.False, "and applies nothing");
        }

        [Test] public void V10_WeightsStopAtTheCapAndChangeTheRollOdds()
        {
            var s = Edit(Ready("C124"), x => { foreach (var d in x.dice) d.faces[0].baseWeight = 5; });
            s = Force(s, Fours, "red"); Submit(s);
            Assert.That(s.State.dice.All(d => d.faces[0].baseWeight == 5), Is.True);
            int[] units = s.ProbabilityUnits(s.State.dice[0].dieId);
            Assert.That(units[0], Is.EqualTo(500000), "weight 5 against five faces of weight 1"); Assert.That(units.Skip(1).All(u => u == 100000), Is.True);
        }

        [Test] public void V10_ThrowingGoldPaysAfterTheScoreAndOnlyWhenTheWalletCoversIt()
        {
            // p1.1.1 section 4.1: C138 costs 2 gold per trigger and multiplies by 2.5: 1620 x 2.5 = 4050.
            var s = Force(Edit(Ready("C138"), x => x.gold = 3), Fours, "red"); Assert.That(Submit(s), Is.EqualTo("4050")); Assert.That(s.State.gold, Is.EqualTo(1));
            s = Force(s, Fours, "red"); Assert.That(Submit(s), Is.EqualTo("1620"), "1 gold is not enough"); Assert.That(s.State.gold, Is.EqualTo(1));
            var two = Force(Edit(Ready("C41", "C138"), x => x.gold = 3), Fours, "red");
            Assert.That(Submit(two), Is.EqualTo("4050"), "3 gold pays the left source only"); Assert.That(two.State.gold, Is.EqualTo(1));
        }

        // ---- items ----

        [Test] public void V10_NewItemsDoubleTheScoreTurnDiceBlueGildAFaceAndWidenTheRow()
        {
            string id = null;
            var twice = Edit(Force(Ready(), Fours, "red"), x => id = AddItem(x, "I09")); Do(twice, CommandKind.UseItem, c => c.itemInstanceId = id);
            Assert.That(Submit(twice), Is.EqualTo("4800")); Assert.That(twice.State.hand.doubleScore, Is.False, "the next hand starts clean");
            var blue = Edit(Force(Ready(), Fours, "red"), x => id = AddItem(x, "I10")); Do(blue, CommandKind.UseItem, c => c.itemInstanceId = id);
            Assert.That(Submit(blue), Is.EqualTo("720"), "all blue: 20 x 12 x 3, no red group");
            string red = null; var both = Edit(Force(Ready(), Fours, "red"), x => { id = AddItem(x, "I10"); red = AddItem(x, "I01"); });
            Do(both, CommandKind.UseItem, c => c.itemInstanceId = id); var second = Command(both, CommandKind.UseItem); second.itemInstanceId = red;
            Assert.That(both.Execute(second).accepted, Is.False, "one wave per hand");
            var pen = Edit(Force(Ready(), Fours, "red"), x => id = AddItem(x, "I11")); Do(pen, CommandKind.UseItem, c => c.itemInstanceId = id);
            Assert.That(pen.State.dice.OrderBy(d => d.createdSerial).First().faces.Single(f => f.faceId == "F01").isGold, Is.True);
            Assert.That(Submit(pen), Is.EqualTo("2880"), "the gilded die counts 8: 24 x 12 x 2 x 5");
            var wide = Edit(Ready(), x => id = AddItem(x, "I12")); Do(wide, CommandKind.UseItem, c => c.itemInstanceId = id);
            Assert.That(wide.State.stage.capacity, Is.EqualTo(6)); Assert.That(wide.EffectiveCapacity, Is.EqualTo(6));
            var used = Edit(Ready("C134"), x => id = AddItem(x, "I12")); Do(used, CommandKind.UseItem, c => c.itemInstanceId = id);
            Assert.That(Growth(used, "C134"), Is.EqualTo(2), "every used item grows C134");
        }

        // ---- shop: loot, gifts, services, upgrades ----

        private GameSession LootShop(string kind, Action<RunState> edit = null, params string[] controllers)
        {
            var s = BossPreview("B05", x => { x.cycles[0].lootKind = kind; edit?.Invoke(x); }, controllers); Do(s, CommandKind.StartStage);
            s = Force(s, Fours, "red"); Submit(s); Do(s, CommandKind.EndStage); Do(s, CommandKind.Continue); return s;
        }
        private static ShopOffer[] Loot(GameSession s) => s.State.shop.offers.Where(o => o.groupId == "LOOT").ToArray();
        /// <summary>v1.1: takes the cash-out of the pending boss loot, which is always allowed before a first pick.</summary>
        private void CashLoot(GameSession s) => Do(s, CommandKind.Buy, c => c.offerId = Loot(s).Single(o => o.category == "gold").offerId);

        [Test] public void V10_RareControllerLootIsFreeTakesOnePickAndThenCloses()
        {
            var s = LootShop("L1"); var group = s.ShopGroups().Single(); var cards = Loot(s).Where(o => o.category == "controller").ToArray();
            Assert.That(group.kind, Is.EqualTo("L1")); Assert.That(group.picks, Is.EqualTo(1)); Assert.That(cards.Length, Is.EqualTo(3));
            Assert.That(cards.All(o => cfg.Controller(o.contentId).rarity == "rare" && o.controller.enchantmentId == "none" && s.GetOfferPrice(o) == 0), Is.True);
            Assert.That(cards.Select(o => o.contentId).Distinct().Count(), Is.EqualTo(3)); Assert.That(Loot(s).Count(o => o.category == "gold"), Is.EqualTo(1));
            long gold = s.State.gold; Do(s, CommandKind.Buy, c => c.offerId = cards[0].offerId);
            Assert.That(s.State.gold, Is.EqualTo(gold)); Assert.That(s.State.controllers.Single().definitionId, Is.EqualTo(cards[0].contentId));
            Assert.That(s.ShopGroups().Single().closed, Is.True);
            foreach (var rest in Loot(s).Where(o => !o.sold)) { var again = Command(s, CommandKind.Buy); again.offerId = rest.offerId; Assert.That(s.Execute(again).accepted, Is.False, rest.category); }
        }

        // v1.1 rewrite (RULES_DELTA): a refresh is refused while the loot waits (boss loot first); once the loot is cashed
        // out, a refresh still replaces only the shelf and leaves the loot group as it is.
        [Test] public void V10_LootCanBeCashedForFiveGoldInsteadAndSurvivesALaterRefresh()
        {
            var s = LootShop("L1", x => x.gold = 20); string[] before = Loot(s).Select(o => o.offerId).ToArray();
            var early = Command(s, CommandKind.RefreshControllers); var untouched = s.State;
            var refused = s.Execute(early); Assert.That(refused.accepted, Is.False); Assert.That(refused.error, Is.EqualTo("先领取首领战利品。")); AssertUnchanged(untouched, s.State);
            long gold = s.State.gold; Do(s, CommandKind.Buy, c => c.offerId = Loot(s).Single(o => o.category == "gold").offerId);
            Assert.That(s.State.gold, Is.EqualTo(gold + 5)); Assert.That(s.ShopGroups().Single().closed, Is.True); Assert.That(s.IsLootPending, Is.False);
            Do(s, CommandKind.RefreshControllers); Assert.That(Loot(s).Select(o => o.offerId), Is.EqualTo(before), "refreshing only replaces the shelf");
            var card = Command(s, CommandKind.Buy); card.offerId = Loot(s).First(o => o.category == "controller").offerId; Assert.That(s.Execute(card).accepted, Is.False);
        }

        [Test] public void V10_EnchantedLootHandLootDiceLootUpgradeLootAndItemLootFollowTheirRules()
        {
            var enchanted = LootShop("L2");
            Assert.That(Loot(enchanted).Where(o => o.category == "controller").All(o => o.controller.enchantmentId != "none"), Is.True); Assert.That(Loot(enchanted).Count(o => o.category == "controller"), Is.EqualTo(3));
            var hands = LootShop("L3", null, "C61"); var hand = Loot(hands).First(o => o.category == "hand_upgrade");
            Assert.That(hand.levels, Is.EqualTo(2)); Do(hands, CommandKind.Buy, c => c.offerId = hand.offerId);
            Assert.That(hands.State.handLevels.Concat(hands.State.colorLevels).Single(h => h.handId == hand.contentId).level, Is.EqualTo(2)); Assert.That(Growth(hands, "C61"), Is.EqualTo(4), "two levels, +2 each");
            var dice = LootShop("L5"); var offers = Loot(dice).Where(o => o.category == "new_die").ToArray(); Assert.That(offers.Length, Is.EqualTo(3));
            Assert.That(offers[0].die.faces.Select(f => f.permanentPip).Distinct().Count(), Is.EqualTo(1), "mono-pip die");
            Assert.That(offers[1].die.faces.Select(f => f.baseColor).Distinct().Count(), Is.EqualTo(1), "mono-colour die");
            Assert.That(offers[2].die.faces.Count(f => f.isGold), Is.EqualTo(2), "two gold faces");
            Do(dice, CommandKind.Buy, c => c.offerId = offers[2].offerId); Assert.That(dice.State.dice.Count, Is.EqualTo(6));
            var workshop = LootShop("L4"); Assert.That(workshop.ShopGroups().Single().picks, Is.EqualTo(3));
            foreach (var upgrade in Loot(workshop).Where(o => o.category == "die_upgrade").ToArray())
            {
                var target = workshop.State.dice.SelectMany(d => d.faces.Select(f => new { d.dieId, f.faceId })).First(t => workshop.IsUpgradeTargetLegal(upgrade.contentId, t.dieId, t.faceId));
                Do(workshop, CommandKind.Buy, c => { c.offerId = upgrade.offerId; c.targetDieId = target.dieId; c.targetFaceId = target.faceId; });
            }
            Assert.That(workshop.ShopGroups().Single().picked, Is.EqualTo(3)); Assert.That(workshop.ShopGroups().Single().closed, Is.True);
            var supply = LootShop("L6"); var items = Loot(supply).Where(o => o.category == "consumable").ToArray(); Assert.That(items.Length, Is.EqualTo(3));
            Do(supply, CommandKind.Buy, c => c.offerId = items[0].offerId); Do(supply, CommandKind.Buy, c => c.offerId = items[1].offerId);
            var third = Command(supply, CommandKind.Buy); third.offerId = items[2].offerId; Assert.That(supply.Execute(third).accepted, Is.False, "two item slots");
            var cash = Command(supply, CommandKind.Buy); cash.offerId = Loot(supply).Single(o => o.category == "gold").offerId; Assert.That(supply.Execute(cash).accepted, Is.False, "no cashing after a pick");
            Assert.That(ShopAfterStage(0).ShopGroups(), Is.Empty, "no loot after an ordinary stage");
        }

        [Test] public void V10_GiftControllersAddAFreeGroupToEveryShopAndAMimicAddsAPick()
        {
            var s = Force(Ready("C111", "C113"), Fours, "red"); Submit(s); Do(s, CommandKind.EndStage); Do(s, CommandKind.Continue);
            var groups = s.ShopGroups(); Assert.That(groups.Select(g => g.groupId), Is.EqualTo(new[] { "GIFT_C111", "GIFT_C113" }));
            Assert.That(groups.All(g => g.picks == 1), Is.True);
            Assert.That(s.State.shop.offers.Count(o => o.groupId == "GIFT_C111" && o.category == "consumable"), Is.EqualTo(3));
            Assert.That(s.State.shop.offers.Count(o => o.groupId == "GIFT_C113" && o.category == "die_upgrade"), Is.EqualTo(3));
            var item = s.State.shop.offers.First(o => o.groupId == "GIFT_C111"); Do(s, CommandKind.Buy, c => c.offerId = item.offerId);
            Assert.That(s.State.inventory.Single().contentId, Is.EqualTo(item.contentId));
            var more = Command(s, CommandKind.Buy); more.offerId = s.State.shop.offers.First(o => o.groupId == "GIFT_C111" && !o.sold).offerId; Assert.That(s.Execute(more).accepted, Is.False);
            var two = Force(Ready("C41", "C111"), Fours, "red"); Submit(two); Do(two, CommandKind.EndStage); Do(two, CommandKind.Continue);
            Assert.That(two.ShopGroups().Single().picks, Is.EqualTo(2));
            var craft = Force(Ready("C113", "C60"), Fours, "red"); Submit(craft); Do(craft, CommandKind.EndStage); Do(craft, CommandKind.Continue);
            var upgrade = craft.State.shop.offers.First(o => o.groupId == "GIFT_C113");
            var target = craft.State.dice.SelectMany(d => d.faces.Select(f => new { d.dieId, f.faceId })).First(t => craft.IsUpgradeTargetLegal(upgrade.contentId, t.dieId, t.faceId));
            Do(craft, CommandKind.Buy, c => { c.offerId = upgrade.offerId; c.targetDieId = target.dieId; c.targetFaceId = target.faceId; });
            Assert.That(Growth(craft, "C60"), Is.EqualTo(2), "a free die upgrade still counts as one bought in the shop");
        }

        [Test] public void V10_BribingTheBossRerollsTheUpcomingModifierAtARisingPrice()
        {
            string offer = null; var s = Edit(ShopAfterStage(0), x => { x.gold = 50; offer = PutOffer(x, "service", "O09"); });
            string before = s.State.cycles[0].modifierId; var others = s.State.cycles.Skip(1).Select(c => c.modifierId).ToArray();
            Assert.That(s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(4));
            Do(s, CommandKind.Buy, c => c.offerId = offer);
            string after = s.State.cycles[0].modifierId;
            Assert.That(after, Is.Not.EqualTo(before)); Assert.That(others, Does.Not.Contain(after)); Assert.That(cfg.Modifier(after).tier, Is.EqualTo(1));
            Assert.That(s.State.gold, Is.EqualTo(46)); Assert.That(s.State.bossRerolls, Is.EqualTo(1)); Assert.That(s.State.cycles.Skip(1).Select(c => c.modifierId), Is.EqualTo(others));
            s = Edit(s, x => offer = PutOffer(x, "service", "O09"));
            Assert.That(s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(6));
            Do(s, CommandKind.LeaveShop); Assert.That(s.CycleView()[2].modifierId, Is.EqualTo(after));
            var next = Edit(ShopAfterStage(2), x => { x.gold = 50; offer = PutOffer(x, "service", "O09"); }); string second = next.State.cycles[1].modifierId;
            Do(next, CommandKind.Buy, c => c.offerId = offer);
            Assert.That(next.State.cycles[1].modifierId, Is.Not.EqualTo(second), "after a boss the next cycle's boss is rerolled");
        }

        [TestCase(0)] [TestCase(1)]
        public void W16B_AdvisorReceiptFreezesTheEffectiveInterestCapAtPurchase(int pigRate)
        {
            var pig = cfg.Controller("C01").parameters;
            int original = pig.extraInterestPerTen;
            try
            {
                pig.extraInterestPerTen = pigRate;
                string offer = null;
                var session = Edit(ShopAfterStage(0), x => {
                    x.gold = 100; Equip(x, "C96", "C01"); offer = PutOffer(x, "service", "O10");
                });
                var receipt = Do(session, CommandKind.Buy, c => c.offerId = offer).receipt;
                var effect = receipt.effects.Single(e => e.kind == "InterestCapRaised");
                long baseCap = cfg.maxInterestPrincipal + cfg.interestPrincipalStep;
                long totalCap = baseCap + cfg.Controller("C96").parameters.interestPrincipalAdd;
                Assert.That(effect.newValue, Is.EqualTo(baseCap));
                Assert.That(effect.detail, Is.EqualTo((totalCap / cfg.interestDivisor * (1 + pigRate)).ToString()));
                var sale = Do(session, CommandKind.Sell, c => c.itemInstanceId = session.State.controllers.First(a=>a.definitionId=="C96").instanceId);
                Assert.That(sale.accepted, Is.True);
                var restored = new GameSession(cfg, new MemorySaveStore(), session.State);
                Assert.That(restored.State.processedReceipts.Single(r => r.commandId == receipt.commandId)
                    .effects.Single(e => e.kind == "InterestCapRaised").detail, Is.EqualTo(effect.detail));
            }
            finally { pig.extraInterestPerTen = original; }
        }

        [Test] public void V10_TheAdvisorRaisesTheInterestCapTwiceAndTheEnchantTableEnchantsOneBareCard()
        {
            string offer = null; var s = Edit(ShopAfterStage(0), x => { x.gold = 100; offer = PutOffer(x, "service", "O10"); });
            Assert.That(s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(6)); Do(s, CommandKind.Buy, c => c.offerId = offer);
            s = Edit(s, x => offer = PutOffer(x, "service", "O10"));
            Assert.That(s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(10)); Do(s, CommandKind.Buy, c => c.offerId = offer);
            s = Edit(s, x => offer = PutOffer(x, "service", "O10")); var third = Command(s, CommandKind.Buy); third.offerId = offer;
            Assert.That(s.Execute(third).accepted, Is.False, "two purchases at most"); Assert.That(s.State.interestCapPurchases, Is.EqualTo(2));
            Do(s, CommandKind.LeaveShop); Do(s, CommandKind.StartStage); s = Force(Edit(s, x => x.gold = 100), Fours, "red"); Submit(s);
            var end = Do(s, CommandKind.EndStage).receipt.settlement;
            Assert.That(end.interestPrincipalCap, Is.EqualTo(45)); Assert.That(end.interest, Is.EqualTo(9));
            var table = Edit(ShopAfterStage(0), x => { x.gold = 50; Equip(x, "C13", "C10"); x.controllers[0].enchantmentId = "roll1"; offer = PutOffer(x, "service", "O11"); });
            Assert.That(table.GetOfferPrice(table.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(6)); Do(table, CommandKind.Buy, c => c.offerId = offer);
            Assert.That(table.State.controllers[1].enchantmentId, Is.Not.EqualTo("none")); Assert.That(table.State.controllers[0].enchantmentId, Is.EqualTo("roll1"));
            table = Edit(table, x => offer = PutOffer(x, "service", "O11")); var none = Command(table, CommandKind.Buy); none.offerId = offer;
            Assert.That(table.Execute(none).accepted, Is.False, "no bare controller left");
        }

        [Test] public void V10_NewDieUpgradesCarvePlaneWeightAndMirrorOneFace()
        {
            Func<string, string, string, GameSession> buy = (upgrade, dieId, faceId) =>
            {
                string offer = null; var s = Edit(ShopAfterStage(0), x => { x.gold = 50; offer = PutOffer(x, "die_upgrade", upgrade, cfg.Upgrade(upgrade).buyPrice); });
                Do(s, CommandKind.Buy, c => { c.offerId = offer; c.targetDieId = dieId; c.targetFaceId = faceId; }); return s;
            };
            Assert.That(buy("U08", "D001", "F01").State.dice[0].faces.Single(f => f.faceId == "F01").permanentPip, Is.EqualTo(2));
            Assert.That(buy("U09", "D001", "F06").State.dice[0].faces.Single(f => f.faceId == "F06").permanentPip, Is.EqualTo(5));
            Assert.That(buy("U10", "D001", "F03").State.dice[0].faces.Single(f => f.faceId == "F03").baseWeight, Is.EqualTo(2));
            var mirrored = buy("U11", "D001", "F01").State.dice[0].faces.Single(f => f.faceId == "F06");
            Assert.That(mirrored.permanentPip, Is.EqualTo(1)); Assert.That(mirrored.baseColor, Is.EqualTo("red"), "F01 of a standard die is a red 1");
            var shop = ShopAfterStage(0);
            Assert.That(shop.IsUpgradeTargetLegal("U08", "D001", "F06"), Is.False, "a 6 cannot grow"); Assert.That(shop.IsUpgradeTargetLegal("U09", "D001", "F01"), Is.False, "a 1 cannot shrink");
            Assert.That(Edit(shop, x => x.dice[0].faces[0].baseWeight = 5).IsUpgradeTargetLegal("U10", "D001", "F01"), Is.False, "weight is capped at 5");
            Assert.That(Edit(shop, x => { x.dice[0].faces[5].permanentPip = 1; x.dice[0].faces[5].baseColor = "red"; }).IsUpgradeTargetLegal("U11", "D001", "F01"), Is.False, "nothing to copy");
        }

        [Test] public void V10_TheRecyclerSellsAtTheFullListPrice()
        {
            var s = Edit(ShopAfterStage(0), x => { Equip(x, "C02", "C140"); x.controllers[0].enchantmentId = "mult2"; });
            Assert.That(s.GetControllerSalePrice(s.State.controllers[0].instanceId), Is.EqualTo(13), "7 + 6 for the enchantment");
            long gold = s.State.gold; Do(s, CommandKind.Sell, c => c.itemInstanceId = s.State.controllers[0].instanceId); Assert.That(s.State.gold, Is.EqualTo(gold + 13));
            var plain = Edit(ShopAfterStage(0), x => Equip(x, "C02"));
            Assert.That(plain.GetControllerSalePrice(plain.State.controllers[0].instanceId), Is.EqualTo(3), "half of 7 without the recycler");
        }

        // ---- save validation and debug scenes ----

        [Test] public void V10_ANormalSaveMustKeepItsCyclePlanBossModifierAndWeightsLegal()
        {
            var state = New(5).State; state.cycles[0].modifierId = state.cycles[7].modifierId;
            Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception, "a final-boss modifier in cycle 1");
            state = New(5).State; state.cycles.RemoveAt(0); Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception);
            state = New(5).State; state.stage.modifierId = "B05"; Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception, "stage 1 is not a boss stage");
            state = New(5).State; state.dice[0].faces[0].baseWeight = 6; Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception);
            state = New(5).State; state.interestCapPurchases = 3; Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception);
            state = New(5).State; state.stageRngState = 0; Assert.That(() => new GameSession(cfg, new MemorySaveStore(), state), Throws.Exception);
            Assert.That(new GameSession(cfg, new MemorySaveStore(), New(5).State).State.cycles.Count, Is.EqualTo(8));
        }

        [Test] public void V10_DebugScenesCarryTheModifierAndTheItemEffectsOfTheHand()
        {
            var s = Force(Boss("B19"), Fours, "red"); string id = null; s = Edit(s, x => id = AddItem(x, "I09")); Do(s, CommandKind.UseItem, c => c.itemInstanceId = id);
            var scene = s.ExportDebugScene();
            Assert.That(scene.modifierId, Is.EqualTo("B19")); Assert.That(scene.doubleScore, Is.True);
            var copy = GameSession.CreateDebugWorkspace(cfg, new MemorySaveStore(), s.State);
            Do(copy, CommandKind.ApplyDebugScene, c => c.debugScene = scene);
            Assert.That(copy.State.stage.modifierId, Is.EqualTo("B19")); Assert.That(copy.State.hand.doubleScore, Is.True);
            Assert.That(Submit(copy), Is.EqualTo("1620"), "20 x 6 (hand multiplier halved by the boss) x 4.5 x 1.5 = 810, doubled by the item");
        }

        // ---- independent review R1 (evidence/v10/review/RULES_REVIEW_R1.md): the fixed findings and rules it found untested ----

        /// <summary>A legal ordinary save: the state is edited and loaded through the normal constructor, so the full validation runs.</summary>
        private GameSession Reload(GameSession s, Action<RunState> edit = null)
        {
            var x = s.State; edit?.Invoke(x); Assert.That(x.isDebug, Is.False, "these tests must stay outside the debug workspace");
            return new GameSession(cfg, new MemorySaveStore(), x);
        }
        /// <summary>Finishes the running stage of an ordinary run: the score is preset one point short, then one real hand is rolled and submitted.</summary>
        private GameSession Finish(GameSession s)
        {
            s = Reload(s, x => x.stage.score = (System.Numerics.BigInteger.Parse(x.stage.target) - 1).ToString());
            Do(s, CommandKind.Roll); Lock(s, s.State.hand.currentResults.Select(r => r.dieId).Take(s.State.stage.capacity).ToArray()); Do(s, CommandKind.Submit);
            if (s.State.phase != "StageSettlement") Do(s, CommandKind.EndStage);
            return s;
        }
        private GameSession ClearStage(GameSession s) { Do(s, CommandKind.StartStage); return Finish(s); }

        [Test] public void V10R_AnOrdinaryRunKeepsItsBossStageAndLootShopThroughSaveRoundTrips()
        {
            var s = New(11);
            s = ClearStage(s); Do(s, CommandKind.Continue); Do(s, CommandKind.LeaveShop);
            s = ClearStage(s); Do(s, CommandKind.Continue); s = Reload(s); Do(s, CommandKind.LeaveShop);
            string planned = s.State.cycles[0].modifierId, loot = s.State.cycles[0].lootKind;
            Assert.That(s.State.stage.modifierId, Is.EqualTo(planned)); Assert.That(s.State.stage.stageKind, Is.EqualTo("boss"));
            s = Reload(s); Do(s, CommandKind.StartStage); Assert.That(s.CurrentModifierId, Is.EqualTo(planned));
            s = Reload(s); Assert.That(s.CurrentModifierId, Is.EqualTo(planned), "the boss stage survives a reload");
            s = Finish(s); var settlement = s.State.lastStageSettlement;
            Assert.That(settlement.stageKind, Is.EqualTo("boss")); Assert.That(settlement.lootKind, Is.EqualTo(loot));
            s = Reload(s); Do(s, CommandKind.Continue); s = Reload(s);
            var group = s.ShopGroups().Single(g => g.groupId == "LOOT"); Assert.That(group.kind, Is.EqualTo(loot)); Assert.That(group.closed, Is.False);
            long gold = s.State.gold; var cash = s.State.shop.offers.Single(o => o.groupId == "LOOT" && o.category == "gold");
            Do(s, CommandKind.Buy, c => c.offerId = cash.offerId); s = Reload(s);
            Assert.That(s.State.gold, Is.EqualTo(gold + cfg.lootGoldValue)); Assert.That(s.ShopGroups().Single(g => g.groupId == "LOOT").closed, Is.True);
            Do(s, CommandKind.LeaveShop); Assert.That(s.State.stage.stageIndex, Is.EqualTo(3)); Assert.That(s.State.stage.modifierId ?? "", Is.Empty, "stage 4 is a normal stage");
        }

        [Test] public void V10R_TheCapacityCharmStopsAtEightInAnOrdinaryRun()
        {
            var s = New(3); Do(s, CommandKind.StartStage);
            s = Reload(s, x => { AddItem(x, "I12"); AddItem(x, "I12"); });
            foreach (string id in s.State.inventory.Select(i => i.instanceId).ToArray()) Do(s, CommandKind.UseItem, c => c.itemInstanceId = id);
            s = Reload(s, x => { AddItem(x, "I12"); AddItem(x, "I12"); }); var more = s.State.inventory.Select(i => i.instanceId).ToArray();
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = more[0]); Assert.That(s.State.stage.capacity, Is.EqualTo(cfg.maxSubmitCapacity));
            var fourth = Command(s, CommandKind.UseItem); fourth.itemInstanceId = more[1];
            Assert.That(s.Execute(fourth).accepted, Is.False, "the row never grows past 8 outside the debug workspace"); Assert.That(s.State.inventory.Count, Is.EqualTo(1));
        }

        [Test] public void V10R_TheTalismanTakesTheTargetFromTheConfigurationAndRechecksTheQualifiedFlag()
        {
            // Ordinary run, second boss (stage 6) with the thick wall: the target comes back from the configuration, not from a division.
            var s = New(5);
            // v1.1 rewrite (RULES_DELTA): the boss loot after stage 3 is cashed out before the shop can be left.
            for (int stage = 0; stage < 5; stage++) { s = ClearStage(s); Do(s, CommandKind.Continue); if (s.IsLootPending) CashLoot(s); if (stage < 4) Do(s, CommandKind.LeaveShop); }
            s = Reload(s, x => { var clash = x.cycles.FirstOrDefault(c => c.modifierId == "B11"); if (clash != null) clash.modifierId = x.cycles[1].modifierId; x.cycles[1].modifierId = "B11"; });
            Do(s, CommandKind.LeaveShop); Do(s, CommandKind.StartStage);
            Assert.That(s.State.stage.target, Is.EqualTo((System.Numerics.BigInteger.Parse(cfg.stages[5].normalTarget) * 3 / 2).ToString()));
            string talisman = null; s = Reload(s, x => talisman = AddItem(x, "I08"));
            Do(s, CommandKind.UseItem, c => c.itemInstanceId = talisman);
            Assert.That(s.State.stage.target, Is.EqualTo(cfg.stages[5].normalTarget)); Assert.That(s.State.stage.qualified, Is.False); Assert.That(s.CurrentModifierId ?? "", Is.Empty);
            // A debug scene can hold a score between the two targets: lifting the wall must mark the stage as qualified at once.
            var scene = Boss("B11"); string second = null; scene = Edit(scene, x => { second = AddItem(x, "I08"); x.stage.score = "600"; }); // p1.1.1: between 500 and the 750 of the wall
            Assert.That(scene.State.stage.qualified, Is.False);
            Do(scene, CommandKind.UseItem, c => c.itemInstanceId = second);
            Assert.That(scene.State.stage.target, Is.EqualTo(cfg.stages[2].normalTarget)); Assert.That(scene.State.stage.qualified, Is.True);
        }

        [Test] public void V10R_ADisabledControllerKeepsItsGrowthValueThroughRollsItemsClearsAndSettlement()
        {
            // B25 disables the two leftmost cards. C62 grows per roll, C58 per stage cleared; the live C55 grows per submission.
            var s = Boss("B25", "C62", "C58", "C55"); Do(s, CommandKind.Roll);
            Assert.That(Growth(s, "C62"), Is.Zero, "a disabled C62 does not grow on a roll");
            s = Force(s, Fours, "red"); Submit(s); Assert.That(Growth(s, "C55"), Is.EqualTo(cfg.Controller("C55").parameters.growthEvent), "the live card still grows");
            Do(s, CommandKind.EndStage); Assert.That(Growth(s, "C58"), Is.Zero, "a disabled C58 does not grow at settlement"); Assert.That(Growth(s, "C62"), Is.Zero);
            // Item use (C134) and the boss clear (C129).
            var t = Boss("B25", "C134", "C129", "C13"); string item = null; t = Edit(t, x => item = AddItem(x, "I07"));
            Do(t, CommandKind.UseItem, c => c.itemInstanceId = item); Assert.That(Growth(t, "C134"), Is.Zero, "a disabled C134 does not grow on an item");
            t = Force(t, Fours, "red"); Submit(t); Do(t, CommandKind.EndStage); Assert.That(Growth(t, "C129"), Is.Zero, "a disabled C129 does not grow on the boss clear");
            // Unused submissions (C59) and the countdown of the fading card (C141).
            var u = Force(Boss("B25", "C141", "C59", "C13"), Fours, "red"); Submit(u); Do(u, CommandKind.EndStage);
            Assert.That(Growth(u, "C141"), Is.Zero, "the fading card does not count a stage it was disabled in"); Assert.That(Growth(u, "C59"), Is.Zero);
            Assert.That(u.State.controllers.Any(c => c.definitionId == "C141"), Is.True);
            // The same boss card grows when it stands third and stays live.
            var live = Force(Boss("B25", "C13", "C12", "C129"), Fours, "red"); Submit(live); Do(live, CommandKind.EndStage);
            Assert.That(Growth(live, "C129"), Is.EqualTo(cfg.Controller("C129").parameters.growthEvent));
        }

        [Test] public void V10R_DisabledEconomyCardsPayNothingAndTheGlassRiskDoesNotDependOnTheCards()
        {
            var s = Force(Boss("B25", "C95", "C133", "C13"), Fours, "red"); Submit(s); var end = Do(s, CommandKind.EndStage).receipt.settlement;
            Assert.That(end.controllerIncome, Is.Zero, "disabled C95 and C133 pay nothing at settlement"); Assert.That(end.bountyIncome, Is.Zero);
            // p1.1.1: C145 was removed, so no card, disabled or not, changes the glass risk.
            var off = Boss("B25", "C10", "C13", "C12"); Assert.That(off.GlassBreakPercentIn(off.State), Is.EqualTo(15));
            var none = Boss("B25"); Assert.That(none.GlassBreakPercentIn(none.State), Is.EqualTo(15));
        }

        [Test] public void V10R_TheAltarActsBeforeTheScapegoatAndAMimicOfTheScapegoatDoesNothing()
        {
            var eaten = Boss("B05", "C100", "C131"); // the altar destroys its right neighbour first, so no scapegoat is left to act
            Assert.That(eaten.State.controllers.Select(c => c.definitionId), Is.EqualTo(new[] { "C100" })); Assert.That(eaten.CurrentModifierId, Is.EqualTo("B05"));
            var copied = Boss("B05", "C41", "C131"); // only the real scapegoat removes itself; the mimic stays
            Assert.That(copied.State.controllers.Select(c => c.definitionId), Is.EqualTo(new[] { "C41" }));
            Assert.That(copied.State.stage.modifierNullified, Is.True); Assert.That(copied.CurrentModifierId ?? "", Is.Empty);
        }

        [Test] public void V10R_TheSparkIsLostWhenTheStageEndsWithoutASubmissionAndTheGildingPenRefusesAGoldFace()
        {
            var s = Force(Ready(), Fours, "red"); Submit(s); // stage 1 is cleared by the first hand; a second hand is ready
            string spark = null; s = Edit(s, x => spark = AddItem(x, "I09")); Do(s, CommandKind.UseItem, c => c.itemInstanceId = spark);
            Assert.That(s.State.hand.doubleScore, Is.True);
            Do(s, CommandKind.EndStage); Do(s, CommandKind.Continue); Do(s, CommandKind.LeaveShop); Do(s, CommandKind.StartStage);
            Assert.That(s.State.hand.doubleScore, Is.False, "the effect does not carry into the next stage"); Assert.That(s.State.inventory, Is.Empty, "the item is spent");
            var g = Force(Ready(), Fours, "red"); string pen = null;
            g = Edit(g, x =>
            {
                pen = AddItem(x, "I11"); var first = x.hand.currentResults.First(r => r.dieId == x.hand.orderedLockedIds[0]);
                x.dice.First(d => d.dieId == first.dieId).faces.First(f => f.faceId == first.currentFaceId).isGold = true;
            });
            var use = Command(g, CommandKind.UseItem); use.itemInstanceId = pen; Assert.That(g.Execute(use).accepted, Is.False, "an already gilded face is not a legal target");
        }

        [Test] public void V10R_BribingAfterStageTwentyOneRedrawsTheFinalBossFromTheFinalPool()
        {
            string offer = null; var s = ShopAfterStage(20, x => { x.gold = 50; offer = PutOffer(x, "service", "O09"); });
            string before = s.State.cycles[7].modifierId; var others = s.State.cycles.Take(7).Select(c => c.modifierId).ToArray();
            Do(s, CommandKind.Buy, c => c.offerId = offer);
            Assert.That(s.State.cycles[7].modifierId, Is.Not.EqualTo(before)); Assert.That(cfg.Modifier(s.State.cycles[7].modifierId).tier, Is.EqualTo(4));
            Assert.That(s.State.cycles.Take(7).Select(c => c.modifierId), Is.EqualTo(others), "earlier cycles are not touched");
        }

        [Test] public void V10R_TheFinalBossPaysNoLootRunsNoBossClearGeneratorsAndWinsTheRun()
        {
            var s = ShopAfterStage(22, x => { x.cycles[7].modifierId = "B31"; Equip(x, "C114", "C116", "C13"); });
            Do(s, CommandKind.LeaveShop); Do(s, CommandKind.StartStage); Assert.That(s.CurrentModifierId, Is.EqualTo("B31"));
            int dice = s.State.dice.Count; s = Edit(s, x => x.stage.target = "1"); s = Force(s, Fours, "red"); Submit(s);
            if (s.State.phase != "StageSettlement") Do(s, CommandKind.EndStage);
            Assert.That(s.State.lastStageSettlement.lootKind ?? "", Is.Empty); Assert.That(s.State.stage.settlementContinuation, Is.EqualTo("RunWon"));
            Assert.That(s.State.controllers.All(c => c.enchantmentId == "none"), Is.True, "C114 does not act after the final boss");
            Assert.That(s.State.dice.Count, Is.EqualTo(dice), "C116 does not act after the final boss");
            Do(s, CommandKind.Continue); Assert.That(s.State.phase, Is.EqualTo("RunWon"));
        }

        [Test] public void V10R_TheRecyclerPaysFullPriceOnlyForControllersThatWereNotAcquiredInThisShop()
        {
            var s = Edit(Shop(), x => { Equip(x, "C140", "C98", "C46"); x.gold = 30; x.controllerSlots = 5; });
            var old = s.State.controllers.Single(c => c.definitionId == "C46");
            Assert.That(s.GetControllerSalePrice(old.instanceId), Is.EqualTo(cfg.Controller("C46").buyPrice), "a card held before this shop sells at its full list price");
            string offer = null; s = Edit(s, x => offer = PutOffer(x, "controller", "C72"));
            long before = s.State.gold, price = s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer));
            Assert.That(price, Is.EqualTo(cfg.Controller("C72").buyPrice + cfg.Controller("C98").parameters.priceDelta), "C98 lowers the buy price");
            Do(s, CommandKind.Buy, c => c.offerId = offer); var bought = s.State.controllers.Single(c => c.definitionId == "C72");
            Assert.That(s.State.shop.acquiredControllerIds, Is.EqualTo(new[] { bought.instanceId }));
            Assert.That(s.GetControllerSalePrice(bought.instanceId), Is.EqualTo(cfg.Controller("C72").buyPrice / cfg.controllerSellDivisor), "a card bought in this shop still sells at half");
            Do(s, CommandKind.Sell, c => c.itemInstanceId = bought.instanceId);
            Assert.That(s.State.gold, Is.LessThan(before), "buying and reselling in one shop never profits, even with C98"); Assert.That(s.State.shop.acquiredControllerIds, Is.Empty);
            // Bought here and kept through a stage: in the next shop it sells at full price.
            s = Edit(s, x => offer = PutOffer(x, "controller", "C72")); Do(s, CommandKind.Buy, c => c.offerId = offer);
            Do(s, CommandKind.LeaveShop); Do(s, CommandKind.StartStage); s = Force(s, Fours, "red"); Submit(s);
            if (s.State.phase != "StageSettlement") Do(s, CommandKind.EndStage);
            Do(s, CommandKind.Continue);
            var kept = s.State.controllers.Single(c => c.definitionId == "C72");
            Assert.That(s.GetControllerSalePrice(kept.instanceId), Is.EqualTo(cfg.Controller("C72").buyPrice));
            // v1.1 rewrite (RULES_DELTA): a same-name copy is paid in full, nothing is traded in. The C46 held from before this
            // shop still sells at its full list price; the new copy, acquired here, sells at half.
            s = Edit(s, x => { offer = PutOffer(x, "controller", "C46"); x.shop.offers.Single(o => o.offerId == offer).controller.enchantmentId = "base10"; x.gold = 30; });
            long goldBefore = s.State.gold; long full = s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer));
            Do(s, CommandKind.Buy, c => c.offerId = offer);
            Assert.That(s.State.gold, Is.EqualTo(goldBefore - full), "no trade-in credit");
            var copies = s.State.controllers.Where(c => c.definitionId == "C46").ToArray(); Assert.That(copies.Length, Is.EqualTo(2));
            Assert.That(s.GetControllerSalePrice(copies[0].instanceId), Is.EqualTo(cfg.Controller("C46").buyPrice), "the copy held from before this shop");
            Assert.That(s.GetControllerSalePrice(copies[1].instanceId), Is.EqualTo((cfg.Controller("C46").buyPrice + ControllerRules.EnchantPrice("base10", cfg)) / cfg.controllerSellDivisor), "the copy acquired here");
            // A loot controller claimed in this shop sells at half as well, never above the gold option.
            var loot = LootShop("L1", null, "C140"); var card = Loot(loot).First(o => o.category == "controller");
            Do(loot, CommandKind.Buy, c => c.offerId = card.offerId); var claimed = loot.State.controllers.Single(c => c.definitionId == card.contentId);
            Assert.That(loot.GetControllerSalePrice(claimed.instanceId), Is.EqualTo(cfg.Controller(card.contentId).buyPrice / cfg.controllerSellDivisor));
            Assert.That(loot.GetControllerSalePrice(claimed.instanceId), Is.LessThanOrEqualTo(cfg.lootGoldValue));
        }

        // ---- p1.1.0 (docs/specs/v11/RULES_DELTA.md): same-name copies (F-DUP-01/02/04) and boss loot first (F-LOOT-01/02) ----
        // Expected numbers are worked out from the rule text, never read back from the code under test.

        private static readonly string[] TenCommons = { "C01", "C03", "C10", "C13", "C19", "C20", "C27", "C38", "C39", "C48" };
        private static readonly string[] TenRares = { "C02", "C05", "C12", "C25", "C31", "C33", "C43", "C44", "C54", "C58" };
        private static ShopOffer[] Shelf(RunState s) => s.shop.offers.Where(o => o.category == "controller" && string.IsNullOrEmpty(o.groupId)).ToArray();
        /// <summary>A settled first stage, ready for Continue to generate its shop.</summary>
        private GameSession Settled() { var s = Force(Ready(), Fours, "red"); Submit(s); Do(s, CommandKind.EndStage); return s; }

        [Test] public void V11_AnOwnedCardCanBeTheFirstShelfOfferBecauseNothingOwnedIsExcluded()
        {
            // The first shelf offer is drawn by weight from the guarantee pool (list price at most 4), in id order. C01 opens
            // that pool, so a first ticket below its weight offers C01 -- now even while a C01 is owned.
            var pool = cfg.controllers.Where(c => c.buyPrice <= cfg.shop.firstControllerGuaranteeMaxBasePrice).OrderBy(c => c.id, StringComparer.Ordinal).ToArray();
            Assert.That(pool[0].id, Is.EqualTo("C01"));
            uint state = 1; while (new GameRng(state).NextIndex(pool.Sum(c => c.offerWeight)) >= pool[0].offerWeight) state++;
            string owned = null; var s = Edit(Settled(), x => { Equip(x, "C01"); owned = x.controllers[0].instanceId; x.shopRngState = state; });
            Do(s, CommandKind.Continue);
            var first = Shelf(s.State).First();
            Assert.That(first.contentId, Is.EqualTo("C01"), "an owned card is not excluded from the shelf");
            Assert.That(first.controller.instanceId, Is.Not.EqualTo(owned));
            Assert.That(ControllerRules.IsEnchantmentLegal("C01", first.controller.enchantmentId, cfg), Is.True);
        }

        [Test] public void V11_OwnedCardsKeepAppearingOnShelvesByWeightAndNoShelfEverHoldsTwoSameNameOffers()
        {
            // Ten owned commons. Expected owned offers per shelf of four: the first draw from the guarantee pool, three
            // more from the whole pool (approximately, ignoring the cards already drawn).
            var guarantee = cfg.controllers.Where(c => c.buyPrice <= cfg.shop.firstControllerGuaranteeMaxBasePrice).ToArray();
            double perShelf = (double)guarantee.Where(c => TenCommons.Contains(c.id)).Sum(c => c.offerWeight) / guarantee.Sum(c => c.offerWeight)
                + (cfg.shop.initialControllerOffers - 1) * (double)cfg.controllers.Where(c => TenCommons.Contains(c.id)).Sum(c => c.offerWeight) / cfg.controllers.Sum(c => c.offerWeight);
            var settled = Settled(); int shelves = 0, owned = 0;
            for (uint seed = 1; seed <= 200; seed++)
            {
                string expand = null;
                var s = Edit(settled, x => { Equip(x, TenCommons); x.gold = 100; x.shopRngState = new GameRng(seed ^ 0x5EED11u).State; });
                Do(s, CommandKind.Continue); shelves++;
                var shelf = Shelf(s.State); owned += shelf.Count(o => TenCommons.Contains(o.contentId));
                Assert.That(shelf.Length, Is.EqualTo(cfg.shop.initialControllerOffers)); Assert.That(shelf.Select(o => o.contentId).Distinct().Count(), Is.EqualTo(shelf.Length), "seed " + seed);
                if (seed % 20 != 0) continue;
                // A refresh and an extra controller offer (O05) keep the shelf free of repeats as well.
                Do(s, CommandKind.RefreshControllers); shelf = Shelf(s.State); Assert.That(shelf.Select(o => o.contentId).Distinct().Count(), Is.EqualTo(shelf.Length), "refresh, seed " + seed);
                s = Edit(s, x => expand = PutOffer(x, "service", "O05")); Do(s, CommandKind.Buy, c => c.offerId = expand);
                shelf = Shelf(s.State); Assert.That(shelf.Length, Is.EqualTo(cfg.shop.initialControllerOffers + 1)); Assert.That(shelf.Select(o => o.contentId).Distinct().Count(), Is.EqualTo(shelf.Length), "O05, seed " + seed);
            }
            double expected = shelves * perShelf;
            Assert.That(owned, Is.InRange(expected * 0.5, expected * 1.5), "owned cards appear about as often as their weight says (expected about " + expected.ToString("F0") + ")");
        }

        [Test] public void V11_RareAndEnchantedLootOfferOwnedCardsButNeverACardOnTheShelf()
        {
            int ownedRare = 0, ownedEnchanted = 0;
            for (uint seed = 1; seed <= 20; seed++)
            {
                var rare = LootShop("L1", x => x.shopRngState = new GameRng(seed ^ 0x10070u).State, TenRares);
                var shelf = Shelf(rare.State).Select(o => o.contentId).ToArray(); var cards = Loot(rare).Where(o => o.category == "controller").ToArray();
                Assert.That(cards.Length, Is.EqualTo(3)); Assert.That(cards.Select(o => o.contentId).Distinct().Count(), Is.EqualTo(3));
                Assert.That(cards.All(o => cfg.Controller(o.contentId).rarity == "rare" && o.controller.enchantmentId == "none"), Is.True);
                Assert.That(cards.Select(o => o.contentId).Intersect(shelf), Is.Empty, "loot never repeats a shelf card, seed " + seed);
                ownedRare += cards.Count(o => TenRares.Contains(o.contentId));
            }
            for (uint seed = 1; seed <= 30; seed++)
            {
                var enchanted = LootShop("L2", x => x.shopRngState = new GameRng(seed ^ 0x20070u).State, TenCommons);
                var shelf = Shelf(enchanted.State).Select(o => o.contentId).ToArray(); var cards = Loot(enchanted).Where(o => o.category == "controller").ToArray();
                Assert.That(cards.Length, Is.EqualTo(3)); Assert.That(cards.Select(o => o.contentId).Distinct().Count(), Is.EqualTo(3));
                Assert.That(cards.All(o => o.controller.enchantmentId != "none"), Is.True);
                Assert.That(cards.Select(o => o.contentId).Intersect(shelf), Is.Empty, "loot never repeats a shelf card, seed " + seed);
                ownedEnchanted += cards.Count(o => TenCommons.Contains(o.contentId));
            }
            Assert.That(ownedRare, Is.GreaterThan(0), "rare loot offers owned rares"); Assert.That(ownedEnchanted, Is.GreaterThan(0), "enchanted loot offers owned cards");
        }

        [Test] public void V11_TwoCopiesTakeTwoSlotsGrowApartAndSellApartWithoutReplacementOrTradeIn()
        {
            string offer = null;
            var s = Edit(Shop(), x => { x.gold = 100; Equip(x, "C55"); x.controllers[0].state.growth = 10; offer = PutOffer(x, "controller", "C55"); });
            string first = s.State.controllers.Single().instanceId; var quoted = s.State.shop.offers.Single(o => o.offerId == offer);
            Assert.That(s.GetOfferTradeIn(quoted), Is.Zero); Assert.That(s.GetOfferNetPrice(quoted), Is.EqualTo(cfg.Controller("C55").buyPrice));
            var bought = Do(s, CommandKind.Buy, c => c.offerId = offer).receipt;
            Assert.That(bought.effects.Select(e => e.kind), Has.None.EqualTo("ControllerReplaced")); Assert.That(s.State.gold, Is.EqualTo(100 - cfg.Controller("C55").buyPrice));
            Assert.That(s.State.controllers.Select(c => c.definitionId), Is.EqualTo(new[] { "C55", "C55" })); Assert.That(s.State.controllers[0].instanceId, Is.EqualTo(first));
            Assert.That(s.State.controllers.Select(c => c.state.growth), Is.EqualTo(new long[] { 10, 0 })); Assert.That(s.OrdinaryControllerSlotsUsed, Is.EqualTo(2));
            // Stage 2, p1.1.1: each copy adds its own value, 12 + 10 and 12 + 0: (20 + 22 + 12) x 12 x 4.5 x 1.5 = 4374, then each grows by 3.
            Do(s, CommandKind.LeaveShop); Do(s, CommandKind.StartStage); s = Force(s, Fours, "red");
            Assert.That(Submit(s), Is.EqualTo("4374"));
            Assert.That(s.State.lastSubmission.breakdown.controllerBaseAdds.Select(a => a.emitterId + ":" + a.amount), Is.EqualTo(new[] { first + ":22", s.State.controllers[1].instanceId + ":12" }), "each copy lists its own add");
            Assert.That(s.State.controllers.Select(c => c.state.growth), Is.EqualTo(new long[] { 13, 3 }));
            Do(s, CommandKind.EndStage); Do(s, CommandKind.Continue);
            long gold = s.State.gold; Do(s, CommandKind.Sell, c => c.itemInstanceId = first);
            Assert.That(s.State.gold, Is.EqualTo(gold + cfg.Controller("C55").buyPrice / cfg.controllerSellDivisor), "the sold copy is paid on its own");
            Assert.That(s.State.controllers.Single().state.growth, Is.EqualTo(3), "the other copy keeps its own growth");
        }

        [Test] public void V11_SameNameCopiesInANormalRunSurviveRestoresWithTheirScoreHistory()
        {
            var state = New(7).State; Equip(state, "C55", "C46", "C55", "C46");
            var s = new GameSession(cfg, new MemorySaveStore(), state); Assert.That(s.State.isDebug, Is.False);
            Do(s, CommandKind.StartStage); s = Reload(s); Do(s, CommandKind.Roll);
            Lock(s, s.State.hand.currentResults.Select(r => r.dieId).Take(s.State.stage.capacity).ToArray()); Do(s, CommandKind.Submit);
            var live = s.State; s = Reload(s); // restoring re-evaluates the frozen submission with both copies of each card
            Assert.That(s.State.lastSubmission.breakdown.score, Is.EqualTo(live.lastSubmission.breakdown.score));
            Assert.That(s.State.lastSubmission.breakdown.additiveEntries.Count(e => e.abilityId == "C46"), Is.EqualTo(2), "p1.1.1: each copy of C46 adds its +2");
            Assert.That(s.State.controllers.Where(c => c.definitionId == "C55").Select(c => c.state.growth), Is.EqualTo(new long[] { 3, 3 }));
            Assert.That(s.State.controllers.Select(c => c.instanceId).Distinct().Count(), Is.EqualTo(4));
        }

        [Test] public void V11_SessionEffectsStackPerCopyOrApplyOnceAsTheDeltaTableSays()
        {
            // Budgets and capacity: one step per copy, exactly like a mimicked source.
            Assert.That(Ready("C78", "C78").State.hand.rollsRemaining, Is.EqualTo(cfg.totalRollsPerHand + 2));
            Assert.That(Ready("C79", "C79").State.stage.remainingSubmissions, Is.EqualTo(cfg.submissionsPerStage + 2));
            Assert.That(Ready("C21", "C21").State.stage.capacity, Is.EqualTo(cfg.initialSubmitCapacity + 2));
            // Settlement income per copy: two iron rice bowls pay 3 + 3.
            var pay = Force(Ready("C95", "C95"), Fours, "red"); Submit(pay); Assert.That(Do(pay, CommandKind.EndStage).receipt.settlement.controllerIncome, Is.EqualTo(6));
            // Gift picks per copy (three choices, two picks).
            var gifts = Force(Ready("C111", "C111"), Fours, "red"); Submit(gifts); Do(gifts, CommandKind.EndStage); Do(gifts, CommandKind.Continue);
            Assert.That(gifts.ShopGroups().Single().picks, Is.EqualTo(2));
            // While-held switches apply once: the recycler. (The protective film C145 was removed in p1.1.1; the glass
            // risk no longer depends on any card.)
            var recycle = Edit(ShopAfterStage(0), x => Equip(x, "C02", "C140", "C140"));
            Assert.That(recycle.GetControllerSalePrice(recycle.State.controllers[0].instanceId), Is.EqualTo(cfg.Controller("C02").buyPrice));
            var film = Ready("C140", "C140"); Assert.That(film.GlassBreakPercentIn(film.State), Is.EqualTo(15));
            // Self-destroying cards act one instance at a time: only the leftmost scapegoat is spent on a boss.
            var goats = Boss("B05", "C131", "C131");
            Assert.That(goats.State.controllers.Select(c => c.definitionId), Is.EqualTo(new[] { "C131" })); Assert.That(goats.State.stage.modifierNullified, Is.True);
            // Fading cards count down apart, p1.1.1: +(4 - 3) and +4 on the reference hand, 20 x 12 x 4.5 x (1 + 0.5 + 1 + 4) = 7020;
            // the older one reaches its fourth decay and expires.
            var fade = Edit(Ready("C141", "C141"), x => x.controllers[0].state.growth = 3); string young = fade.State.controllers[1].instanceId;
            fade = Force(fade, Fours, "red"); Assert.That(Submit(fade), Is.EqualTo("7020")); Do(fade, CommandKind.EndStage);
            Assert.That(fade.State.controllers.Select(c => c.instanceId + ":" + c.state.growth), Is.EqualTo(new[] { young + ":1" }));
        }

        [Test] public void V11_PendingBossLootRefusesEveryOtherShopActionButSellingAndReordering()
        {
            string item = null;
            var s = LootShop("L1", x => { x.gold = 50; item = AddItem(x, "I01"); }, "C13", "C111");
            Assert.That(s.State.phase, Is.EqualTo("Shop")); Assert.That(s.IsLootPending, Is.True); Assert.That(s.IsLootPendingIn(s.State), Is.True);
            Assert.That(s.ShopGroups().Select(g => g.groupId), Is.EqualTo(new[] { "LOOT", "GIFT_C111" }));
            var shelf = Shelf(s.State).First(); var gift = s.State.shop.offers.First(o => o.groupId == "GIFT_C111");
            var other = s.State.shop.offers.First(o => string.IsNullOrEmpty(o.groupId) && o.category == "consumable");
            var before = s.State;
            var refused = new[] {
                Command(s, CommandKind.Buy), Command(s, CommandKind.Buy), Command(s, CommandKind.Buy),
                Command(s, CommandKind.RefreshControllers), Command(s, CommandKind.Study), Command(s, CommandKind.LeaveShop) };
            refused[0].offerId = shelf.offerId; refused[1].offerId = gift.offerId; refused[2].offerId = other.offerId; refused[4].handId = s.LegalStudyHands().First();
            foreach (var command in refused)
            {
                var result = s.Execute(command);
                Assert.That(result.accepted, Is.False, command.kind.ToString()); Assert.That(result.error, Is.EqualTo("先领取首领战利品。"), command.kind.ToString()); AssertUnchanged(before, s.State);
            }
            Assert.That(s.PurchaseError(shelf.offerId), Is.EqualTo("先领取首领战利品。")); Assert.That(s.PurchaseError(gift.offerId), Is.EqualTo("先领取首领战利品。"));
            Assert.That(s.PurchaseError(Loot(s).First(o => o.category == "controller").offerId), Is.Null, "the loot itself can be claimed");
            // Allowed while the loot waits: reordering and selling cards or items.
            Do(s, CommandKind.ReorderControllers, c => c.controllerIds = s.State.controllers.Select(x => x.instanceId).Reverse().ToArray());
            Do(s, CommandKind.Sell, c => c.itemInstanceId = item); Do(s, CommandKind.Sell, c => c.itemInstanceId = s.State.controllers.Single(x => x.definitionId == "C13").instanceId);
            Assert.That(s.IsLootPending, Is.True);
            Do(s, CommandKind.Buy, c => c.offerId = Loot(s).First(o => o.category == "controller").offerId);
            Assert.That(s.IsLootPending, Is.False); Assert.That(s.PurchaseError(gift.offerId), Is.Null);
            Do(s, CommandKind.Buy, c => c.offerId = gift.offerId); Do(s, CommandKind.LeaveShop); Assert.That(s.State.phase, Is.EqualTo("StagePreview"));
        }

        [Test] public void V11_AnOrdinaryRunRestoredInItsLootShopKeepsTheGateUntilTheLootIsCashed()
        {
            var s = New(11);
            s = ClearStage(s); Do(s, CommandKind.Continue); Do(s, CommandKind.LeaveShop);
            s = ClearStage(s); Do(s, CommandKind.Continue); Do(s, CommandKind.LeaveShop);
            s = ClearStage(s); Assert.That(s.State.stage.stageKind, Is.EqualTo("boss"));
            Do(s, CommandKind.Continue); Assert.That(s.IsLootPending, Is.True, "the shop opens on the boss loot");
            s = Reload(s); Assert.That(s.IsLootPending, Is.True, "a restored loot shop keeps the gate");
            var leave = s.Execute(Command(s, CommandKind.LeaveShop)); Assert.That(leave.accepted, Is.False); Assert.That(leave.error, Is.EqualTo("先领取首领战利品。"));
            s = Reload(s); CashLoot(s); s = Reload(s); Assert.That(s.IsLootPending, Is.False);
            Do(s, CommandKind.LeaveShop); Assert.That(s.State.stage.stageIndex, Is.EqualTo(3));
        }

        [Test] public void V11_TheLootPageCanSellCardsOrItemsToMakeRoomForTheLoot()
        {
            var cards = LootShop("L1", null, "C13", "C10", "C19", "C20", "C27"); var card = Loot(cards).First(o => o.category == "controller");
            var claim = Command(cards, CommandKind.Buy); claim.offerId = card.offerId; var full = cards.Execute(claim);
            Assert.That(full.accepted, Is.False); Assert.That(full.error, Is.EqualTo("普通卡牌栏位或总张数已满，可先出售卡牌。")); Assert.That(cards.IsLootPending, Is.True);
            Do(cards, CommandKind.Sell, c => c.itemInstanceId = cards.State.controllers[0].instanceId);
            Do(cards, CommandKind.Buy, c => c.offerId = card.offerId); Assert.That(cards.IsLootPending, Is.False);
            var items = LootShop("L6", x => { AddItem(x, "I01"); AddItem(x, "I02"); }); var supply = Loot(items).Where(o => o.category == "consumable").ToArray();
            claim = Command(items, CommandKind.Buy); claim.offerId = supply[0].offerId; Assert.That(items.Execute(claim).error, Is.EqualTo("道具栏位已满，可先出售道具。"), "two of two item slots are taken");
            Do(items, CommandKind.Sell, c => c.itemInstanceId = items.State.inventory[0].instanceId); Do(items, CommandKind.Buy, c => c.offerId = supply[0].offerId);
            Assert.That(items.State.inventory.Select(i => i.contentId), Does.Contain(supply[0].contentId));
        }

        // Rewritten after the coordinator's decision of 2026-11-16 (RULES_DELTA 2.1 and 5.3; was
        // V11_AMultiPickLootHoldsTheShopUntilEveryPickIsTakenOrNothingClaimableIsLeft): the first pick unlocks the shop,
        // the remaining picks stay claimable in this shop and are forfeited when the player leaves it.
        [Test] public void V11_AMultiPickLootUnlocksTheShopAtTheFirstPickAndKeepsTheRestClaimableUntilLeaving()
        {
            Func<GameSession, ShopOffer, GameSession> claim = (s, upgrade) =>
            {
                var target = s.State.dice.SelectMany(d => d.faces.Select(f => new { d.dieId, f.faceId })).First(t => s.IsUpgradeTargetLegal(upgrade.contentId, t.dieId, t.faceId));
                Do(s, CommandKind.Buy, c => { c.offerId = upgrade.offerId; c.targetDieId = target.dieId; c.targetFaceId = target.faceId; }); return s;
            };
            var shop = LootShop("L4", x => x.gold = 50); var upgrades = Loot(shop).Where(o => o.category == "die_upgrade").ToArray();
            Assert.That(shop.ShopGroups().Single().picks, Is.EqualTo(3)); Assert.That(shop.IsLootPending, Is.True);
            claim(shop, upgrades[0]);
            Assert.That(shop.IsLootPending, Is.False, "the first pick unlocks the shop");
            Assert.That(shop.ShopGroups().Single().picked, Is.EqualTo(1)); Assert.That(shop.ShopGroups().Single().closed, Is.False);
            Assert.That(shop.PurchaseError(Shelf(shop.State).First().offerId), Is.Not.EqualTo("先领取首领战利品。"));
            Do(shop, CommandKind.RefreshControllers);
            // The other picks can still be taken in this shop; the gold cannot, since something was already picked.
            var cash = Command(shop, CommandKind.Buy); cash.offerId = Loot(shop).Single(o => o.category == "gold").offerId;
            Assert.That(shop.Execute(cash).error, Is.EqualTo("已经领取过战利品，不能再折现。"));
            claim(shop, upgrades[1]); Assert.That(shop.ShopGroups().Single().picked, Is.EqualTo(2));
            Do(shop, CommandKind.LeaveShop); Assert.That(shop.State.phase, Is.EqualTo("StagePreview")); Assert.That(shop.State.shop, Is.Null, "the third pick is forfeited");
            // Item loot with two item slots: two picks fill the slots and the third does not hold the player, so nothing has
            // to be sold before leaving.
            var items = LootShop("L6"); var supply = Loot(items).Where(o => o.category == "consumable").ToArray();
            Do(items, CommandKind.Buy, c => c.offerId = supply[0].offerId); Do(items, CommandKind.Buy, c => c.offerId = supply[1].offerId);
            Assert.That(items.IsLootPending, Is.False); Assert.That(items.ShopGroups().Single().closed, Is.False);
            var third = Command(items, CommandKind.Buy); third.offerId = supply[2].offerId; Assert.That(items.Execute(third).accepted, Is.False, "two of two item slots are taken");
            Do(items, CommandKind.LeaveShop); Assert.That(items.State.inventory.Count, Is.EqualTo(2));
        }

        // ---- user decision D on stacked C98 discounts (RULES_DELTA 1.1 item 9 and 5.1): a card bought in the open shop
        // resells for at most the price actually paid until the player leaves that shop ----

        /// <summary>A shop after stage 1 with the given bargainers (slotless, so they take no ordinary slot) and one shelf offer.</summary>
        private GameSession BargainShop(int bargainers, string card, out string offer, params string[] others)
        {
            string id = null;
            var s = Edit(Shop(), x =>
            {
                x.gold = 50; Equip(x, others.Concat(Enumerable.Repeat("C98", bargainers)).ToArray());
                foreach (var c in x.controllers.Where(c => c.definitionId == "C98")) c.enchantmentId = "slotless";
                id = PutOffer(x, "controller", card);
            });
            offer = id; return s;
        }

        [Test] public void V11_ThreeOrMoreBargainersCanNoLongerBuyAndResellAtAProfitInTheSameShop()
        {
            // List 4 with three C98: bought for max(1, 4 - 3) = 1; half of 4 would resell for 2, the cap keeps it at 1.
            var s = BargainShop(3, "C50", out string offer);
            Assert.That(s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(1));
            long before = s.State.gold; Do(s, CommandKind.Buy, c => c.offerId = offer);
            var bought = s.State.controllers.Last(); Assert.That(bought.definitionId, Is.EqualTo("C50"));
            Assert.That(s.State.shop.paidControllerPrices.Select(p => p.instanceId + ":" + p.paid), Is.EqualTo(new[] { bought.instanceId + ":1" }));
            Assert.That(s.GetControllerSalePrice(bought.instanceId), Is.EqualTo(1)); Assert.That(s.ControllerSalePriceIn(s.State, bought), Is.EqualTo(1));
            Do(s, CommandKind.Sell, c => c.itemInstanceId = bought.instanceId);
            Assert.That(s.State.gold, Is.EqualTo(before), "no profit"); Assert.That(s.State.shop.paidControllerPrices, Is.Empty);
            // List 8 with five C98: bought for 3; half of 8 would be 4, the cap keeps it at 3.
            var dear = BargainShop(5, "C47", out string dearOffer);
            before = dear.State.gold; Do(dear, CommandKind.Buy, c => c.offerId = dearOffer); var card = dear.State.controllers.Last();
            Assert.That(dear.State.gold, Is.EqualTo(before - 3)); Assert.That(dear.GetControllerSalePrice(card.instanceId), Is.EqualTo(3));
            Do(dear, CommandKind.Sell, c => c.itemInstanceId = card.instanceId); Assert.That(dear.State.gold, Is.EqualTo(before));
            // Without a discount the cap changes nothing: list 4 bought for 4 still resells for half, 2.
            var plain = BargainShop(0, "C50", out string plainOffer); Do(plain, CommandKind.Buy, c => c.offerId = plainOffer);
            Assert.That(plain.GetControllerSalePrice(plain.State.controllers.Last().instanceId), Is.EqualTo(2));
        }

        [Test] public void V11_TheResaleCapEndsWhenThePlayerLeavesTheShop()
        {
            var s = BargainShop(3, "C50", out string offer); Do(s, CommandKind.Buy, c => c.offerId = offer); string bought = s.State.controllers.Last().instanceId;
            Assert.That(s.GetControllerSalePrice(bought), Is.EqualTo(1));
            Do(s, CommandKind.LeaveShop); Assert.That(s.State.shop, Is.Null);
            Assert.That(s.GetControllerSalePrice(bought), Is.EqualTo(2), "after leaving, half of the list price of 4");
            Do(s, CommandKind.StartStage); s = Force(s, Fours, "red"); Submit(s);
            if (s.State.phase != "StageSettlement") Do(s, CommandKind.EndStage);
            Do(s, CommandKind.Continue); Assert.That(s.State.phase, Is.EqualTo("Shop"));
            long gold = s.State.gold; Do(s, CommandKind.Sell, c => c.itemInstanceId = bought);
            Assert.That(s.State.gold, Is.EqualTo(gold + 2), "a later shop pays the ordinary price, more than the 1 paid");
        }

        [Test] public void V11_AFreeLootCardIsNotCappedByItsZeroPrice()
        {
            var s = LootShop("L1", null, "C98", "C98", "C98"); var card = Loot(s).First(o => o.category == "controller");
            Do(s, CommandKind.Buy, c => c.offerId = card.offerId);
            Assert.That(s.State.shop.paidControllerPrices, Is.Empty, "a free claim is not a purchase");
            Assert.That(s.State.shop.acquiredControllerIds, Does.Contain(card.controller.instanceId));
            Assert.That(s.GetControllerSalePrice(card.controller.instanceId), Is.EqualTo(Math.Max(cfg.baseControllerSellPrice, cfg.Controller(card.contentId).buyPrice / cfg.controllerSellDivisor)),
                "half of its list price, as a just-claimed loot card already sold for in p1.0.0");
        }

        [Test] public void V11_TheRecyclerKeepsHalfPriceForCardsBoughtHereAndTheCapAppliesOnTop()
        {
            var s = BargainShop(3, "C50", out string offer, "C140", "C46");
            string held = s.State.controllers.Single(c => c.definitionId == "C46").instanceId;
            Assert.That(s.GetControllerSalePrice(held), Is.EqualTo(cfg.Controller("C46").buyPrice), "a card held before this shop: full list price");
            Do(s, CommandKind.Buy, c => c.offerId = offer); string bought = s.State.controllers.Last().instanceId;
            Assert.That(s.GetControllerSalePrice(bought), Is.EqualTo(1), "bought here: half (2) by the recycler's rule, then capped at the 1 paid");
            Do(s, CommandKind.LeaveShop);
            Assert.That(s.GetControllerSalePrice(bought), Is.EqualTo(cfg.Controller("C50").buyPrice), "after leaving, the recycler pays the full list price");
        }

        [Test] public void V11_ThePaidPriceSurvivesARestoreAndTamperedRecordsAreRejected()
        {
            var s = BargainShop(3, "C50", out string offer); Do(s, CommandKind.Buy, c => c.offerId = offer); string bought = s.State.controllers.Last().instanceId;
            var restored = new GameSession(cfg, new MemorySaveStore(), s.State);
            Assert.That(restored.GetControllerSalePrice(bought), Is.EqualTo(1)); Assert.That(restored.State.shop.paidControllerPrices.Single().paid, Is.EqualTo(1));
            // Each tampered copy must be refused by the paid-price rule itself, not by an unrelated check or a crash.
            const string invalid = "本店买价记录无效。";
            Func<Action<RunState>, TestDelegate> tampered = edit => () => { var x = s.State; edit(x); new GameSession(cfg, new MemorySaveStore(), x); };
            Assert.That(tampered(x => x.shop.paidControllerPrices.Clear()), Throws.Exception.With.Message.EqualTo(invalid), "a card bought from the shelf keeps its paid price");
            Assert.That(tampered(x => x.shop.paidControllerPrices[0].paid = 5), Throws.Exception.With.Message.EqualTo(invalid), "more than the list price of 4");
            Assert.That(tampered(x => x.shop.paidControllerPrices[0].paid = 0), Throws.Exception.With.Message.EqualTo(invalid), "a purchase costs at least 1");
            Assert.That(tampered(x => x.shop.paidControllerPrices.Add(x.shop.paidControllerPrices[0].Clone())), Throws.Exception.With.Message.EqualTo(invalid), "one record per card");
            Assert.That(tampered(x => x.shop.paidControllerPrices.Add(new ShopPaidPrice { instanceId = x.controllers[0].instanceId, paid = 1 })), Throws.Exception.With.Message.EqualTo(invalid), "a card not acquired in this shop");
            Assert.That(tampered(x => x.shop.paidControllerPrices.Add(null)), Throws.Exception.With.Message.EqualTo(invalid), "a null record");
            Assert.That(tampered(x => x.shop.paidControllerPrices = null), Throws.Exception.With.Message.EqualTo(invalid), "a missing list");
            var loot = LootShop("L1"); var card = Loot(loot).First(o => o.category == "controller"); Do(loot, CommandKind.Buy, c => c.offerId = card.offerId);
            Assert.That(() => { var x = loot.State; x.shop.paidControllerPrices.Add(new ShopPaidPrice { instanceId = card.controller.instanceId, paid = 1 }); new GameSession(cfg, new MemorySaveStore(), x); },
                Throws.Exception.With.Message.EqualTo(invalid), "a free loot claim has no paid price");
        }
    }
}
