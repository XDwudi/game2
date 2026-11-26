using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using NUnit.Framework;
using DiceCraft.Core;

namespace DiceCraft.Tests
{
    /// <summary>
    /// p1.1.1 session behaviour of the shop, items, the unlock command and save schema 8
    /// (docs/specs/v111/RULES_DELTA.md sections 3.1, 5, 7.1, 7.2, 8, 9 and 12.1).
    /// Every expected number is worked out by hand from the rule text; the arithmetic is in the comment next to it.
    /// Shop draws are steered by searching a stream state whose next tickets are the wanted ones. The search replays
    /// the documented draw (weighted id draw in card id order, then one enchantment ticket) on its own, so a test also
    /// checks that the session picked the card the rule text says.
    /// </summary>
    public sealed class V111ShopTests : SessionTestSupport
    {
        private sealed class FaultStore : ISaveStore
        {
            public bool fail = true; public int attempts;
            public void Save(RunState snapshot) { attempts++; if (fail) throw new InvalidOperationException("disk full"); }
        }

        // ---- helpers ----

        private static readonly int[] Fours = { 4, 4, 4, 4, 4 };
        private static readonly string[] Reds = { "red", "red", "red", "red", "red" };
        private static string[] A(params string[] values) => values;

        /// <summary>A stream state whose next draws are the given tickets; arguments are range, ticket pairs.</summary>
        private static uint SeedFor(params int[] rangeTicketPairs)
        {
            for (uint seed = 1; seed < 60000000; seed++)
            {
                var rng = new GameRng(seed); bool match = true;
                for (int i = 0; match && i < rangeTicketPairs.Length; i += 2) match = rng.NextIndex(rangeTicketPairs[i]) == rangeTicketPairs[i + 1];
                if (match) return seed;
            }
            throw new InvalidOperationException("No seed found for the requested tickets.");
        }

        /// <summary>The weighted id draw of section 9.1 replayed by the test: one ticket over the weights of the pool in id order.</summary>
        private static ControllerDefinition Pick(ControllerDefinition[] pool, int[] weights, int total, GameRng rng)
        {
            int ticket = rng.NextIndex(total);
            for (int i = 0; i < pool.Length; i++) { ticket -= weights[i]; if (ticket < 0) return pool[i]; }
            throw new InvalidOperationException("ticket outside the pool");
        }
        /// <summary>A shop stream state whose first id draw from `pool` gives a card accepted by `want` and whose next ticket (if a range is given) is `ticket`.</summary>
        private static uint ShopSeed(ControllerDefinition[] pool, Func<ControllerDefinition, bool> want, int range = 0, int ticket = 0)
        {
            int[] weights = pool.Select(c => c.offerWeight).ToArray(); int total = weights.Sum();
            for (uint seed = 1; seed < 60000000; seed++)
            {
                var rng = new GameRng(seed);
                if (!want(Pick(pool, weights, total, rng))) continue;
                if (range == 0 || rng.NextIndex(range) == ticket) return seed;
            }
            throw new InvalidOperationException("No shop seed found.");
        }

        /// <summary>
        /// The candidates of a controller draw as section 9.3 lists them: every card in id order, without the ids
        /// already offered in this shop (the shelf batch and the kept groups; `withoutShelf` drops the shelf first, as
        /// a refresh does) and without a unique multiplier card that is held.
        /// </summary>
        private ControllerDefinition[] Candidates(RunState x, bool withoutShelf = false, bool lowPrice = false)
        {
            var offered = x.shop.offers.Where(o => o.category == "controller" && !(withoutShelf && string.IsNullOrEmpty(o.groupId))).Select(o => o.contentId).ToArray();
            return cfg.controllers.Where(c => !offered.Contains(c.id) && !(cfg.shop.uniqueControllerIds.Contains(c.id) && x.controllers.Any(h => h.definitionId == c.id)) && (!lowPrice || c.buyPrice <= 4))
                .OrderBy(c => c.id, StringComparer.Ordinal).ToArray();
        }
        private bool Scoring(ControllerDefinition c) => !cfg.enchantRules.nonSubmitControllerIds.Contains(c.id);

        /// <summary>Stage 1 settled (five red 4s), then moved to the settlement of the stage `index` (0-based) with a consistent receipt. Debug workspace.</summary>
        private GameSession SettledAt(int index, Action<RunState> edit = null, params string[] controllers)
        {
            var s = Selecting(Fours, Reds, controllers); Do(s, CommandKind.Submit); Do(s, CommandKind.EndStage);
            return Edit(s, x =>
            {
                var def = cfg.stages[index]; x.stage.stageIndex = index; x.stage.stageId = def.stageId; x.stage.stageKind = def.stageKind; x.stage.target = def.normalTarget; x.stage.score = def.normalTarget;
                x.stage.settlementId = x.runId + "/" + def.stageId; x.lastStageSettlement.settlementId = x.stage.settlementId; x.lastStageSettlement.stageId = def.stageId;
                x.lastStageSettlement.score = x.stage.score; x.lastStageSettlement.target = x.stage.target; x.lastStageSettlement.stageKind = def.stageKind;
                x.debugCapacityOverride = 0; x.gold = 100; edit?.Invoke(x);
            });
        }
        /// <summary>The shop generated after the stage `index` (0-based): its cycle is min(8, (index + 1) / 3 + 1).</summary>
        private GameSession ShopAt(int index, Action<RunState> edit = null, params string[] controllers)
        {
            var s = SettledAt(index, edit, controllers); Do(s, CommandKind.Continue); return s;
        }
        private static int CycleOf(int index) => Math.Min(8, (index + 1) / 3 + 1);
        private static ShopOffer[] ShelfCards(RunState x) => x.shop.offers.Where(o => o.category == "controller" && string.IsNullOrEmpty(o.groupId)).ToArray();
        private static ShopOffer[] Loot(RunState x) => x.shop.offers.Where(o => o.groupId == "LOOT").ToArray();
        private static string[] ControllerDraws(CommandResult r) => r.receipt.randomResults.Where(t => t.stream == "shop" && (t.kind == "ControllerId" || t.kind == "ControllerEnchantment")).Select(t => t.kind == "ControllerId" ? "id" : "enchant").ToArray();

        /// <summary>Buys an added shelf slot (O05) with the shop stream set so that the new card is one `want` accepts and its enchantment ticket is the given one.</summary>
        private GameSession Expand(GameSession shop, Func<ControllerDefinition, bool> want, int range, int ticket, out ShopOffer added, out CommandResult result)
        {
            string service = null;
            var s = Edit(shop, x => { x.gold = 100; service = PutOffer(x, "service", "O05"); x.shopRngState = ShopSeed(Candidates(x), want, range, ticket); });
            result = Do(s, CommandKind.Buy, c => c.offerId = service); added = ShelfCards(s.State).Last(); return s;
        }
        /// <summary>The same for one named card, with ticket 0 of its pool: no enchantment.</summary>
        private GameSession Expand(GameSession shop, string card, out ShopOffer added) => Expand(shop, c => c.id == card, Scoring(cfg.Controller(card)) ? 5000 : 10000, 0, out added, out _);
        /// <summary>The shop of a settled state for a given shop stream state.</summary>
        private GameSession Open(GameSession settled, uint shopState, out CommandResult result)
        {
            var s = Edit(settled, x => x.shopRngState = shopState); result = Do(s, CommandKind.Continue); return s;
        }
        private GameSession Open(GameSession settled, uint shopState) => Open(settled, shopState, out _);

        /// <summary>Executes a command that must be refused, checks that nothing changed (state hash and sequence) and returns the reason.</summary>
        private string Refused(GameSession s, CommandKind kind, Action<SessionCommand> configure = null)
        {
            var before = s.State; var c = Command(s, kind); configure?.Invoke(c); var r = s.Execute(c);
            Assert.That(r.accepted, Is.False, kind + " should have been refused");
            var after = s.State; Assert.That(GameSession.StateHash(after), Is.EqualTo(GameSession.StateHash(before)), "a refusal changes nothing"); AssertUnchanged(before, after);
            return r.error;
        }

        /// <summary>A debug-workspace run that has just entered stage 1 with these cards, after an optional edit.</summary>
        private GameSession Start(string[] controllers, Action<RunState> edit = null)
        {
            var s = Edit(New(), x => { Equip(x, controllers); edit?.Invoke(x); }); Do(s, CommandKind.StartStage); return s;
        }
        /// <summary>
        /// Puts the hand into the selecting phase with real physical faces: every die shows its face F01; die i gets
        /// the given pip and colour on that face (no remote override). `lockedOrder` is the locked list in order.
        /// </summary>
        private GameSession Hand(GameSession s, string[] lockedOrder, int rollsRemaining = 0, int[] pips = null, string[] colors = null, Action<RunState> edit = null) => Edit(s, x =>
        {
            x.phase = "Selecting"; x.hand.hasRolled = true; x.hand.rollsUsed = 1; x.hand.rollsRemaining = rollsRemaining; x.hand.orderedLockedIds.Clear(); x.hand.currentResults.Clear();
            var row = x.dice.OrderBy(d => d.createdSerial).ToArray();
            for (int i = 0; i < row.Length; i++)
            {
                var face = row[i].faces.First(f => f.faceId == "F01");
                if (pips != null && i < pips.Length) face.permanentPip = pips[i];
                if (colors != null && i < colors.Length) face.baseColor = colors[i];
                x.hand.currentResults.Add(new DieHandState { dieId = row[i].dieId, currentFaceId = "F01", currentPip = face.permanentPip, currentColor = face.baseColor, hasRolled = true, isLocked = lockedOrder.Contains(row[i].dieId) });
            }
            x.hand.orderedLockedIds.AddRange(lockedOrder); edit?.Invoke(x);
        });
        private CommandResult Unlock(GameSession s, string dieId) => Do(s, CommandKind.UnlockDie, c => c.targetDieId = dieId);
        private static OwnedItem Key(RunState x) => x.inventory.Single(i => i.contentId == "I13");
        private static ControllerInstance Card(RunState x, string id, int ordinal = 0) => x.controllers.Where(c => c.definitionId == id).ElementAt(ordinal);
        private static FaceState Face(RunState x, string dieId, string faceId) => x.dice.First(d => d.dieId == dieId).faces.First(f => f.faceId == faceId);
        private static string Kinds(CommandResult r) => string.Join(",", r.receipt.effects.Select(e => e.kind));

        private GameSession ShopAfterStage(int index, Action<RunState> edit = null) => Edit(Shop(), x =>
        {
            var def = cfg.stages[index]; x.stage.stageIndex = index; x.stage.stageId = def.stageId; x.stage.stageKind = def.stageKind; x.stage.target = def.normalTarget; x.stage.score = def.normalTarget;
            x.stage.settlementId = x.runId + "/" + def.stageId; x.lastStageSettlement.settlementId = x.stage.settlementId; x.lastStageSettlement.stageId = def.stageId;
            x.lastStageSettlement.score = x.stage.score; x.lastStageSettlement.target = x.stage.target; x.lastStageSettlement.stageKind = def.stageKind;
            edit?.Invoke(x);
        });
        /// <summary>Stage 3 (the first boss) started under the given modifier with these cards.</summary>
        private GameSession Boss(string modifierId, string[] controllers, Action<RunState> edit = null)
        {
            var s = ShopAfterStage(1, x => { x.cycles[0].modifierId = modifierId; Equip(x, controllers); edit?.Invoke(x); });
            Do(s, CommandKind.LeaveShop); Do(s, CommandKind.StartStage); return s;
        }
        /// <summary>An ordinary (non-debug) run in stage 1 with these cards, loaded through the normal constructor.</summary>
        private GameSession Ordinary(Action<RunState> edit, params string[] controllers)
        {
            var x = New().State; Equip(x, controllers); edit?.Invoke(x); var s = new GameSession(cfg, new MemorySaveStore(), x); Do(s, CommandKind.StartStage);
            Assert.That(s.State.isDebug, Is.False); return s;
        }
        private void AssertRejected(GameSession s, Action<RunState> corrupt, string reason)
        {
            var x = s.State; corrupt(x);
            Assert.That(() => new GameSession(cfg, new MemorySaveStore(), x), Throws.Exception, reason);
        }

        // ---- 1. the enchantment chance of a shop cycle (section 9.1) ----

        [Test] public void V111_TheShopCycleIsTheCycleOfTheNextStage()
        {
            // N = min(8, floor(j / 3) + 1) for the finished stage number j: the shop after stage 2 is cycle 1, after stage 3 cycle 2.
            var s = Shop();
            var expected = new Dictionary<int, int> { { 1, 1 }, { 2, 1 }, { 3, 2 }, { 5, 2 }, { 6, 3 }, { 8, 3 }, { 9, 4 }, { 12, 5 }, { 15, 6 }, { 18, 7 }, { 21, 8 }, { 23, 8 } };
            foreach (var pair in expected) { var x = s.State; x.stage.stageIndex = pair.Key - 1; Assert.That(s.ShopCycleIn(x), Is.EqualTo(pair.Value), "after stage " + pair.Key); }
            Assert.That(cfg.enchantRules.shopChanceByCyclePercent, Is.EqualTo(new[] { 10, 20, 35, 45, 55, 60, 65, 65 }));
        }

        // Scoring pool: W = 18 + 10 + 4 + 9 + 9 = 50, tickets 0..4999. With P percent: none (100 - P) x 50, then
        // slotless P x 18, base10 P x 10, mult2 P x 4, submit1 P x 9, roll1 P x 9.
        // Cycle 3 (P = 35): none 0-3249, slotless 3250-3879, base10 3880-4229, mult2 4230-4369, submit1 4370-4684, roll1 4685-4999.
        [TestCase(6, 0, "none")] [TestCase(6, 3249, "none")] [TestCase(6, 3250, "slotless")] [TestCase(6, 3879, "slotless")]
        [TestCase(6, 3880, "base10")] [TestCase(6, 4229, "base10")] [TestCase(6, 4230, "mult2")] [TestCase(6, 4369, "mult2")]
        [TestCase(6, 4370, "submit1")] [TestCase(6, 4684, "submit1")] [TestCase(6, 4685, "roll1")] [TestCase(6, 4999, "roll1")]
        // Cycle 1 (P = 10): none 0-4499, slotless 4500-4679, base10 4680-4779.
        [TestCase(0, 4499, "none")] [TestCase(0, 4500, "slotless")] [TestCase(0, 4679, "slotless")] [TestCase(0, 4680, "base10")]
        // Cycle 2 (P = 20): none 0-3999, slotless 4000-4359.
        [TestCase(3, 3999, "none")] [TestCase(3, 4000, "slotless")]
        // Cycle 8 (P = 65): none 0-1749, slotless 1750-2919, base10 2920-3569, ..., roll1 4415-4999.
        [TestCase(22, 1749, "none")] [TestCase(22, 1750, "slotless")] [TestCase(22, 2919, "slotless")] [TestCase(22, 2920, "base10")] [TestCase(22, 4415, "roll1")] [TestCase(22, 4999, "roll1")]
        public void V111_AScoringCardDrawsItsShelfEnchantmentFromOneTicketOfTheCycle(int stageIndex, int ticket, string expected)
        {
            var s = Expand(ShopAt(stageIndex), Scoring, 5000, ticket, out var added, out var result);
            Assert.That(Scoring(cfg.Controller(added.contentId)), Is.True);
            var draws = result.receipt.randomResults.Where(t => t.stream == "shop").ToArray();
            Assert.That(draws.Select(t => t.kind), Is.EqualTo(A("ControllerId", "ControllerEnchantment")), "an added slot is one id draw and one enchantment ticket");
            Assert.That(draws[1].range, Is.EqualTo(5000)); Assert.That(draws[1].rawTicket, Is.EqualTo(ticket)); Assert.That(draws[1].appliedValue, Is.EqualTo(expected));
            Assert.That(added.controller.enchantmentId, Is.EqualTo(expected), "cycle " + CycleOf(stageIndex) + ", ticket " + ticket);
        }

        // Non-scoring pool: W = 50 + 0 + 0 + 25 + 25 = 100, tickets 0..9999.
        // Cycle 3 (P = 35): none 0-6499, slotless 6500-8249, submit1 8250-9124, roll1 9125-9999; never base10 or mult2.
        [TestCase(6, 0, "none")] [TestCase(6, 6499, "none")] [TestCase(6, 6500, "slotless")] [TestCase(6, 8249, "slotless")]
        [TestCase(6, 8250, "submit1")] [TestCase(6, 9124, "submit1")] [TestCase(6, 9125, "roll1")] [TestCase(6, 9999, "roll1")]
        // Cycle 1 (P = 10): none 0-8999, slotless 9000-9499.
        [TestCase(0, 8999, "none")] [TestCase(0, 9000, "slotless")]
        public void V111_ANonScoringCardDrawsFromItsOwnPoolAtTheSameCycleChance(int stageIndex, int ticket, string expected)
        {
            var s = Expand(ShopAt(stageIndex), c => !Scoring(c), 10000, ticket, out var added, out var result);
            Assert.That(cfg.enchantRules.nonSubmitControllerIds, Does.Contain(added.contentId));
            var draw = result.receipt.randomResults.Single(t => t.kind == "ControllerEnchantment");
            Assert.That(draw.range, Is.EqualTo(10000)); Assert.That(draw.rawTicket, Is.EqualTo(ticket)); Assert.That(added.controller.enchantmentId, Is.EqualTo(expected));
        }

        [Test] public void V111_TheNewCardsUseThePoolsTheSpecificationNames()
        {
            // C154 is in the non-scoring pool; C155 and C156 can carry a scoring enchantment; the removed cards are gone.
            Assert.That(cfg.enchantRules.nonSubmitControllerIds, Does.Contain("C154"));
            Assert.That(ControllerRules.IsEnchantmentLegal("C154", "mult2", cfg), Is.False); Assert.That(ControllerRules.IsEnchantmentLegal("C154", "slotless", cfg), Is.True);
            Assert.That(ControllerRules.IsEnchantmentLegal("C155", "mult2", cfg), Is.True); Assert.That(ControllerRules.IsEnchantmentLegal("C156", "base10", cfg), Is.True);
            foreach (string removed in A("C90", "C128", "C145")) Assert.That(cfg.controllers.Any(c => c.id == removed), Is.False, removed);
            var s = Expand(ShopAt(6), "C154", out var added); Assert.That(added.contentId, Is.EqualTo("C154"));
        }

        [TestCase(0)] [TestCase(1)] [TestCase(2)] [TestCase(4)]
        public void V111_InCyclesOneAndTwoTheLowPriceGuaranteeHasNoEnchantmentAndDrawsNoTicket(int stageIndex)
        {
            Assert.That(CycleOf(stageIndex), Is.LessThanOrEqualTo(2));
            // A boss stage adds loot after the shelf; L3 keeps controller draws out of it.
            var settledBase = SettledAt(stageIndex, x => { if (stageIndex % 3 == 2) x.cycles[stageIndex / 3].lootKind = "L3"; });
            for (uint seed = 1; seed <= 40; seed++)
            {
                var settled = Open(settledBase, seed, out var result);
                var shelf = ShelfCards(settled.State); Assert.That(shelf.Length, Is.EqualTo(4));
                Assert.That(cfg.Controller(shelf[0].contentId).buyPrice, Is.LessThanOrEqualTo(4)); Assert.That(shelf[0].controller.enchantmentId, Is.EqualTo("none"), "seed " + seed);
                // Four id draws and only three enchantment tickets: the guarantee card is followed directly by the next id draw.
                Assert.That(ControllerDraws(result), Is.EqualTo(A("id", "id", "enchant", "id", "enchant", "id", "enchant")), "seed " + seed);
                var draws = result.receipt.randomResults.Where(t => t.stream == "shop").ToArray();
                Assert.That(draws[1].beforeState, Is.EqualTo(draws[0].afterState), "the stream did not move between the guarantee card and the second id draw");
                // The first id draw is the test's own replay of the low-price pool.
                var low = cfg.controllers.Where(c => c.buyPrice <= 4).OrderBy(c => c.id, StringComparer.Ordinal).ToArray();
                Assert.That(shelf[0].contentId, Is.EqualTo(Pick(low, low.Select(c => c.offerWeight).ToArray(), low.Sum(c => c.offerWeight), new GameRng(seed)).id));
            }
        }

        [Test] public void V111_ARefreshInCycleTwoKeepsTheGuaranteeFreeOfATicketAndAnAddedSlotNeverReopensIt()
        {
            var s = ShopAt(3); Assert.That(CycleOf(3), Is.EqualTo(2));
            var refresh = Do(s, CommandKind.RefreshControllers);
            Assert.That(ControllerDraws(refresh), Is.EqualTo(A("id", "id", "enchant", "id", "enchant", "id", "enchant")));
            Assert.That(ShelfCards(s.State)[0].controller.enchantmentId, Is.EqualTo("none")); Assert.That(cfg.Controller(ShelfCards(s.State)[0].contentId).buyPrice, Is.LessThanOrEqualTo(4));
            // An added slot is an ordinary draw: it may cost more than 4 and it draws its ticket (4000 is the first slotless ticket of cycle 2).
            s = Expand(s, c => c.buyPrice > 4 && Scoring(c), 5000, 4000, out var added, out var result);
            Assert.That(ControllerDraws(result), Is.EqualTo(A("id", "enchant"))); Assert.That(cfg.Controller(added.contentId).buyPrice, Is.GreaterThan(4)); Assert.That(added.controller.enchantmentId, Is.EqualTo("slotless"));
        }

        [Test] public void V111_FromCycleThreeTheGuaranteeCardDrawsItsTicketLikeAnyOther()
        {
            int enchanted = 0; var settledBase = SettledAt(22);
            for (uint seed = 1; seed <= 40; seed++)
            {
                var settled = Open(settledBase, seed, out var result);
                Assert.That(ControllerDraws(result), Is.EqualTo(A("id", "enchant", "id", "enchant", "id", "enchant", "id", "enchant")));
                var first = ShelfCards(settled.State)[0]; Assert.That(cfg.Controller(first.contentId).buyPrice, Is.LessThanOrEqualTo(4));
                if (first.controller.enchantmentId != "none") enchanted++;
            }
            // 65% per shop in cycle 8: 40 shops without a single enchanted guarantee card would be 0.35^40.
            Assert.That(enchanted, Is.GreaterThan(0));
        }

        // Independent reference vectors for the whole first shelf, computed outside the game code by a script that
        // re-implements section 9.1 from the rule text (listed in docs/specs/v111/W3B2_NOTES.md). The cycle-1 vectors are
        // in V05_ShopInterleavesIdAndEnchantmentWithFrozenReferenceVectors; these are the same four seeds in cycle 3,
        // where the guarantee card draws a ticket too.
        [TestCase(1u, "C59:slotless,C155:none,C05:none,C50:submit1")]
        [TestCase(0u, "C48:none,C126:none,C89:mult2,C49:none")]
        [TestCase(20261030u, "C135:mult2,C08:mult2,C36:none,C115:roll1")]
        [TestCase(4294967295u, "C142:none,C97:slotless,C138:base10,C141:base10")]
        public void V111_CycleThreeShelfMatchesTheIndependentReferenceVectors(uint seed, string expected)
        {
            var s = SettledAt(6, x => x.shopRngState = new GameRng(seed ^ 0xC8013EA4u).State); Do(s, CommandKind.Continue);
            Assert.That(string.Join(",", ShelfCards(s.State).Select(o => o.contentId + ":" + o.controller.enchantmentId)), Is.EqualTo(expected));
        }

        [Test] public void V111_EnchantedLootAndTheEnchantingTableDoNotUseTheCycleChance()
        {
            // L2 in cycle 2 (20%): all three cards are enchanted, from the old non-empty pool (range 18+10+4+9+9 = 50 or 50+25+25 = 100).
            var settledBase = SettledAt(2, x => x.cycles[0].lootKind = "L2");
            for (uint seed = 1; seed <= 12; seed++)
            {
                var s = Open(settledBase, seed, out var result);
                var cards = Loot(s.State).Where(o => o.category == "controller").ToArray(); Assert.That(cards.Length, Is.EqualTo(3));
                Assert.That(cards.All(o => o.controller.enchantmentId != "none"), Is.True);
                var lootIds = cards.Select(o => o.contentId).ToArray();
                foreach (var ticket in result.receipt.randomResults.Where(t => t.kind == "ControllerEnchantment" && lootIds.Contains(t.sourceId)))
                    Assert.That(ticket.range, Is.EqualTo(Scoring(cfg.Controller(ticket.sourceId)) ? 50 : 100), ticket.sourceId);
            }
            // O11 in cycle 1 (10%): the card always gets an enchantment, from the effect stream.
            string table = null; var shop = Edit(Shop(), x => { x.gold = 100; Equip(x, "C13"); table = PutOffer(x, "service", "O11"); });
            var bought = Do(shop, CommandKind.Buy, c => c.offerId = table); Assert.That(shop.State.controllers.Single().enchantmentId, Is.Not.EqualTo("none"));
            var draw = bought.receipt.randomResults.Single(t => t.kind == "ControllerEnchantment"); Assert.That(draw.stream, Is.EqualTo("effect")); Assert.That(draw.range, Is.EqualTo(50));
        }

        // ---- 2. unique multiplier cards (section 9.3) ----

        [Test] public void V111_TheUniqueListIsTheNineteenMultiplierCards()
        {
            Assert.That(cfg.shop.uniqueControllerIds.OrderBy(x => x, StringComparer.Ordinal), Is.EqualTo(A("C02", "C09", "C31", "C33", "C41", "C42", "C53", "C54", "C58", "C71", "C87", "C89", "C100", "C101", "C103", "C125", "C130", "C138", "C155").OrderBy(x => x, StringComparer.Ordinal)));
            Assert.That(cfg.shop.uniqueControllerIds, Does.Not.Contain("C49"), "the half-score penalty does not make C49 a multiplier card");
        }

        [Test] public void V111_AHeldUniqueCardIsNeverDrawnOnARefreshWhileAnOrdinaryHeldCardStillIs()
        {
            // Holding C53 and C49. A refresh first draws from the low-price pool (both list 4). Without C53 in the pool,
            // the ticket that used to open C53's span now opens the next card; C49 keeps its span.
            var s = ShopAt(0, null, "C53", "C49"); Assert.That(s.IsUniqueControllerHeld("C53"), Is.True); Assert.That(s.IsUniqueControllerHeld("C49"), Is.False);
            var x0 = s.State; var filtered = Candidates(x0, withoutShelf: true, lowPrice: true);
            Assert.That(filtered.Any(c => c.id == "C53"), Is.False); Assert.That(filtered.Any(c => c.id == "C49"), Is.True);
            var unfiltered = cfg.controllers.Where(c => c.buyPrice <= 4).OrderBy(c => c.id, StringComparer.Ordinal).ToArray();
            string successor = unfiltered[Array.FindIndex(unfiltered, c => c.id == "C53") + 1].id;
            int before53 = unfiltered.TakeWhile(c => c.id != "C53").Sum(c => c.offerWeight), total = filtered.Sum(c => c.offerWeight);
            var afterC53 = Edit(s, x => x.shopRngState = SeedFor(total, before53)); Do(afterC53, CommandKind.RefreshControllers);
            Assert.That(ShelfCards(afterC53.State)[0].contentId, Is.EqualTo(successor), "the ticket where C53 used to start now gives the next card");
            int before49 = filtered.TakeWhile(c => c.id != "C49").Sum(c => c.offerWeight);
            var owned49 = Edit(s, x => x.shopRngState = SeedFor(total, before49)); Do(owned49, CommandKind.RefreshControllers);
            Assert.That(ShelfCards(owned49.State)[0].contentId, Is.EqualTo("C49"), "a held card that is not unique can be offered again");
            // And over many refreshes C53 never shows up anywhere on the shelf.
            for (uint seed = 1; seed <= 150; seed++)
            {
                var r = Edit(s, x => x.shopRngState = seed); Do(r, CommandKind.RefreshControllers);
                Assert.That(ShelfCards(r.State).Select(o => o.contentId), Does.Not.Contain("C53"), "seed " + seed);
            }
        }

        [Test] public void V111_AfterSellingTheUniqueCardARefreshCanOfferItAgain()
        {
            var s = ShopAt(0, null, "C53");
            Do(s, CommandKind.Sell, c => c.itemInstanceId = s.State.controllers.Single().instanceId); Assert.That(s.IsUniqueControllerHeld("C53"), Is.False);
            var pool = Candidates(s.State, withoutShelf: true, lowPrice: true); Assert.That(pool.Any(c => c.id == "C53"), Is.True);
            s = Edit(s, x => x.shopRngState = SeedFor(pool.Sum(c => c.offerWeight), pool.TakeWhile(c => c.id != "C53").Sum(c => c.offerWeight)));
            Do(s, CommandKind.RefreshControllers); Assert.That(ShelfCards(s.State)[0].contentId, Is.EqualTo("C53"));
        }

        [Test] public void V111_AnAddedSlotStillExcludesAUniqueCardThatWasSoldFromTheCurrentBatch()
        {
            // The shelf batch holds a sold C53 offer. After the card is sold back, an added slot must not draw C53 (the
            // batch keeps its no-replacement rule); a refresh withdraws the batch, and then C53 is a candidate again.
            string offer = null; var s = Edit(ShopAt(0), x => offer = PutOffer(x, "controller", "C53"));
            Do(s, CommandKind.Buy, c => c.offerId = offer); Do(s, CommandKind.Sell, c => c.itemInstanceId = s.State.controllers.Single().instanceId);
            Assert.That(s.State.shop.offers.Single(o => o.offerId == offer).sold, Is.True);
            var pool = Candidates(s.State); Assert.That(pool.Any(c => c.id == "C53"), Is.False);
            string successor = pool.First(c => string.CompareOrdinal(c.id, "C53") > 0).id;
            string service = null;
            var expanded = Edit(s, x => { service = PutOffer(x, "service", "O05"); x.shopRngState = SeedFor(pool.Sum(c => c.offerWeight), pool.TakeWhile(c => string.CompareOrdinal(c.id, "C53") < 0).Sum(c => c.offerWeight)); });
            Do(expanded, CommandKind.Buy, c => c.offerId = service);
            Assert.That(ShelfCards(expanded.State).Last().contentId, Is.EqualTo(successor)); Assert.That(ShelfCards(expanded.State).Count(o => o.contentId == "C53"), Is.EqualTo(1), "only the sold offer");
            var low = Candidates(s.State, withoutShelf: true, lowPrice: true); Assert.That(low.Any(c => c.id == "C53"), Is.True);
            s = Edit(s, x => x.shopRngState = SeedFor(low.Sum(c => c.offerWeight), low.TakeWhile(c => c.id != "C53").Sum(c => c.offerWeight)));
            Do(s, CommandKind.RefreshControllers); Assert.That(ShelfCards(s.State)[0].contentId, Is.EqualTo("C53"), "the refresh withdrew the batch with the sold offer");
        }

        [Test] public void V111_ALiveOfferOfAHeldUniqueCardIsRefusedAndStaysUntilTheHeldCopyIsSold()
        {
            string offer = null; var s = Edit(Shop(), x => { x.gold = 100; Equip(x, "C53"); offer = PutOffer(x, "controller", "C53"); });
            Assert.That(s.PurchaseError(offer), Is.EqualTo(GameSession.UniqueControllerOwned));
            Assert.That(Refused(s, CommandKind.Buy, c => c.offerId = offer), Is.EqualTo(GameSession.UniqueControllerOwned));
            Assert.That(s.State.shop.offers.Single(o => o.offerId == offer).sold, Is.False, "the offer is not removed");
            // A disabled or enchanted copy is still a held copy; an ordinary same-name card is unaffected.
            string other = null; var ordinary = Edit(s, x => other = PutOffer(x, "controller", "C49")); ordinary = Edit(ordinary, x => Equip(x, "C53", "C49"));
            Assert.That(ordinary.PurchaseError(other), Is.Null, "C49 may be held twice");
            Do(s, CommandKind.Sell, c => c.itemInstanceId = s.State.controllers.Single().instanceId);
            Assert.That(s.PurchaseError(offer), Is.Null); Do(s, CommandKind.Buy, c => c.offerId = offer);
            Assert.That(s.State.controllers.Single().definitionId, Is.EqualTo("C53"));
        }

        [Test] public void V111_RareAndEnchantedLootNeverOfferAHeldUniqueCardAndAClaimIsCheckedAgain()
        {
            // Holding every unique card: L1 (three of the rare cards) and L2 (three weighted cards) avoid all of them.
            string[] unique = cfg.shop.uniqueControllerIds;
            foreach (string kind in A("L1", "L2"))
            {
                var holding = SettledAt(2, x => { x.cycles[0].lootKind = kind; Equip(x, unique); });
                for (uint seed = 1; seed <= 25; seed++)
                {
                    var s = Open(holding, seed);
                    var cards = Loot(s.State).Where(o => o.category == "controller").Select(o => o.contentId).ToArray(); Assert.That(cards.Length, Is.EqualTo(3));
                    Assert.That(cards.Intersect(unique), Is.Empty, kind + " seed " + seed);
                    Assert.That(ShelfCards(s.State).Select(o => o.contentId).Intersect(unique), Is.Empty, "first shelf, seed " + seed);
                }
            }
            // Without holding them the same loot does offer unique cards (so the filter above is what removed them).
            int seen = 0; var empty = SettledAt(2, x => x.cycles[0].lootKind = "L1");
            for (uint seed = 1; seed <= 25; seed++) seen += Loot(Open(empty, seed).State).Count(o => o.category == "controller" && unique.Contains(o.contentId));
            Assert.That(seen, Is.GreaterThan(0));
            // A loot card that became illegal after it was generated is refused on the claim; the cash-out still works.
            var loot = Open(empty, 1); var claim = Loot(loot.State).First(o => o.category == "controller");
            string held = unique.First(u => cfg.Controller(u).rarity == "rare" && !loot.State.shop.offers.Any(o => o.contentId == u));
            loot = Edit(loot, x => { var o = x.shop.offers.Single(v => v.offerId == claim.offerId); o.contentId = held; o.controller.definitionId = held; o.basePrice = cfg.Controller(held).buyPrice; Equip(x, held); });
            Assert.That(Refused(loot, CommandKind.Buy, c => c.offerId = claim.offerId), Is.EqualTo(GameSession.UniqueControllerOwned));
            Do(loot, CommandKind.Buy, c => c.offerId = Loot(loot.State).Single(o => o.category == "gold").offerId);
        }

        [Test] public void V111_AMimicOfAUniqueCardIsNotASecondCopyAndANormalRunCannotHoldTwo()
        {
            // C41 copying C53 does not hold C53: an offer of C53 is refused only because the real one is held.
            string offer = null; var s = Edit(Shop(), x => { x.gold = 100; Equip(x, "C41", "C13"); offer = PutOffer(x, "controller", "C53"); });
            Assert.That(s.PurchaseError(offer), Is.Null); Assert.That(s.IsUniqueControllerHeld("C41"), Is.True, "the mimic itself is on the unique list");
            // A debug scene may hold two copies to test sources; a normal run may not.
            var debug = Edit(Shop(), x => Equip(x, "C53", "C53")); Assert.That(debug.State.controllers.Count, Is.EqualTo(2));
            var normal = Edit(Shop(), x => x.isDebug = false);
            AssertRejected(normal, x => Equip(x, "C53", "C53"), "two copies of a unique card in a normal run");
            var fine = normal.State; Equip(fine, "C49", "C49", "C53"); Assert.That(() => new GameSession(cfg, new MemorySaveStore(), fine), Throws.Nothing);
        }

        // ---- 3. growth catch-up (section 5) ----

        [Test] public void V111_ACycleFiveShelfOffersDeepBlueWithAHeadStartOfTwoAndItScoresPlusThree()
        {
            // Cycle 5 (after stage 13): additive catch-up (5 - 1) x 0.5 = +2, so C56 starts at +1 + 2 = +3.
            var s = Expand(ShopAt(12), "C56", out var added); Assert.That(CycleOf(12), Is.EqualTo(5));
            Assert.That(added.controller.state.growthCatchup, Is.EqualTo(200)); Assert.That(added.controller.state.growth, Is.Zero);
            Do(s, CommandKind.Buy, c => c.offerId = added.offerId);
            var card = Card(s.State, "C56"); Assert.That(card.state.growthCatchup, Is.EqualTo(200), "inherited as offered"); Assert.That(card.state.growth, Is.Zero, "the grown part starts at 0");
            Assert.That(ControllerRules.GrowthValue(cfg, "C56", card.state), Is.EqualTo(300));
            // Next stage: one blue 6. First hand 6 x (1 + 0.25 + 3) = 25.5 -> 25; it then grows 0.4: 6 x (1 + 0.25 + 3.4) = 27.9 -> 27.
            Do(s, CommandKind.LeaveShop); Do(s, CommandKind.StartStage);
            s = Hand(s, A("D001"), 0, new[] { 6 }, A("blue")); Assert.That(Do(s, CommandKind.Submit).receipt.score, Is.EqualTo("25"));
            Assert.That(Card(s.State, "C56").state.growth, Is.EqualTo(40)); Assert.That(Card(s.State, "C56").state.growthCatchup, Is.EqualTo(200));
            s = Hand(s, A("D001"), 0, new[] { 6 }, A("blue")); Assert.That(Do(s, CommandKind.Submit).receipt.score, Is.EqualTo("27"));
        }

        // additive +0.5, base +6, multiplier +0.1 per cycle after the first; cycle 1 gives nothing.
        [TestCase(0, "C56", 0)] [TestCase(3, "C56", 50)] [TestCase(6, "C55", 12)] [TestCase(9, "C58", 30)] [TestCase(22, "C55", 42)] [TestCase(22, "C54", 70)] [TestCase(22, "C134", 350)]
        [TestCase(22, "C13", 0)] [TestCase(22, "C141", 0)]
        public void V111_AnAddedShelfSlotCarriesTheCatchUpOfTheShopCycle(int stageIndex, string card, int expected)
        {
            var s = Expand(ShopAt(stageIndex), card, out var added); Assert.That(added.contentId, Is.EqualTo(card));
            Assert.That(added.controller.state.growthCatchup, Is.EqualTo(expected), card + " in cycle " + CycleOf(stageIndex));
            Do(s, CommandKind.Buy, c => c.offerId = added.offerId); var owned = s.State.controllers.Last();
            Assert.That(owned.state.growthCatchup, Is.EqualTo(expected)); Assert.That(owned.state.growth, Is.Zero);
        }

        [Test] public void V111_FirstShelfRefreshAndLootAllCarryTheCatchUpOfTheirShop()
        {
            // Stage 6 is a boss: the shop after it is cycle 3. Additive 2 x 50, base 2 x 6, multiplier 2 x 10.
            Func<string, long> expected = id => ControllerRules.IsGrowthAdditive(id) ? 100 : ControllerRules.IsGrowthBase(id) ? 12 : ControllerRules.IsGrowthMultiplier(id) ? 20 : 0;
            var seen = new HashSet<string>(); int lootGrowth = 0;
            var rare = SettledAt(5, x => x.cycles[1].lootKind = "L1"); var enchantedLoot = SettledAt(5, x => x.cycles[1].lootKind = "L2");
            for (uint seed = 1; seed <= 30; seed++)
            {
                var s = Open(seed % 2 == 0 ? rare : enchantedLoot, seed);
                foreach (var o in s.State.shop.offers.Where(v => v.category == "controller"))
                {
                    Assert.That(o.controller.state.growthCatchup, Is.EqualTo(expected(o.contentId)), o.contentId + " seed " + seed);
                    if (expected(o.contentId) > 0) { seen.Add(o.contentId); if (o.groupId == "LOOT") lootGrowth++; }
                }
                Do(s, CommandKind.Buy, c => c.offerId = Loot(s.State).Single(o => o.category == "gold").offerId); Do(s, CommandKind.RefreshControllers);
                foreach (var o in ShelfCards(s.State)) { Assert.That(o.controller.state.growthCatchup, Is.EqualTo(expected(o.contentId)), "refresh " + o.contentId); if (expected(o.contentId) > 0) seen.Add(o.contentId); }
            }
            Assert.That(seen.Any(ControllerRules.IsGrowthAdditive) && seen.Any(ControllerRules.IsGrowthBase) && seen.Any(ControllerRules.IsGrowthMultiplier), Is.True, "all three kinds were offered");
            Assert.That(lootGrowth, Is.GreaterThan(0), "a growth card was offered as loot");
        }

        [Test] public void V111_ALootGrowthCardKeepsItsCatchUpWhenClaimedAndADirectGiftHasNone()
        {
            // L1 after stage 3 (cycle 2): a rare multiplier growth card carries +0.1.
            GameSession loot = null; ShopOffer card = null; var settledBase = SettledAt(2, x => x.cycles[0].lootKind = "L1");
            for (uint seed = 1; seed <= 60 && card == null; seed++)
            {
                loot = Open(settledBase, seed);
                card = Loot(loot.State).FirstOrDefault(o => o.category == "controller" && ControllerRules.IsGrowthMultiplier(o.contentId));
            }
            Assert.That(card, Is.Not.Null); Assert.That(card.controller.state.growthCatchup, Is.EqualTo(10));
            Do(loot, CommandKind.Buy, c => c.offerId = card.offerId); Assert.That(loot.State.controllers.Single().state.growthCatchup, Is.EqualTo(10));
            // C112 grants a common card at the end of a late stage (stage 13): the first effect ticket picks C55 from
            // the common cards that are not held. A direct gift has no catch-up.
            var s = Start(A("C112")); var commons = cfg.controllers.Where(c => c.rarity == "common").OrderBy(c => c.id, StringComparer.Ordinal).ToArray();
            s = Edit(s, x => { var def = cfg.stages[12]; x.stage.stageIndex = 12; x.stage.stageId = def.stageId; x.stage.stageKind = def.stageKind; });
            s = Hand(s, A("D001"), 0, new[] { 3 }, A("red"), x => { x.stage.score = (BigInteger.Parse(x.stage.target) - 1).ToString(); x.effectRngState = SeedFor(commons.Length, Array.FindIndex(commons, c => c.id == "C55")); });
            Do(s, CommandKind.Submit); if (s.State.phase != "StageSettlement") Do(s, CommandKind.EndStage);
            var granted = Card(s.State, "C55"); Assert.That(granted.state.growthCatchup, Is.Zero); Assert.That(granted.state.growth, Is.Zero);
        }

        // ---- 4. die upgrades: one or two faces, gilding, the self-selected upgrade (sections 7.1, 7.2, 9.2) ----

        private GameSession UpgradeShop(string upgradeId, out string offer, Action<RunState> edit = null, params string[] controllers)
        {
            string id = null; var s = Edit(Shop(), x => { x.gold = 100; Equip(x, controllers); if (upgradeId != null) id = PutOffer(x, "die_upgrade", upgradeId); edit?.Invoke(x); });
            offer = id; return s;
        }
        private GameSession UpgradeShop(Action<RunState> edit = null, params string[] controllers) => UpgradeShop(null, out _, edit, controllers);
        private CommandResult Custom(GameSession s, string upgradeId, string dieId, params string[] faces) => Do(s, CommandKind.CustomDieUpgrade, c => { c.upgradeId = upgradeId; c.targetDieId = dieId; c.targetFaceIds = faces; });
        private string CustomRefused(GameSession s, string upgradeId, string dieId, params string[] faces) => Refused(s, CommandKind.CustomDieUpgrade, c => { c.upgradeId = upgradeId; c.targetDieId = dieId; c.targetFaceIds = faces; });

        [Test] public void V111_AShelfUpgradeOnTwoFacesOfOneDieIsOnePurchaseAndGrowsTheGrindstoneOnce()
        {
            var s = UpgradeShop("U06", out string offer, null, "C60"); long gold = s.State.gold;
            var result = Do(s, CommandKind.Buy, c => { c.offerId = offer; c.targetDieId = "D001"; c.targetFaceIds = A("F05", "F02"); });
            Assert.That(s.State.gold, Is.EqualTo(gold - 3), "the price of one U06");
            foreach (string f in A("F02", "F05")) { Assert.That(Face(s.State, "D001", f).isGold, Is.True); Assert.That(Face(s.State, "D001", f).baseWeight, Is.EqualTo(2), "weight 1 + 1"); }
            Assert.That(s.State.dice.First(d => d.dieId == "D001").faces.Count(f => f.isGold), Is.EqualTo(2));
            Assert.That(Card(s.State, "C60").state.growth, Is.EqualTo(50), "one transaction, +0.5 once: +1 becomes +1.5");
            Assert.That(result.receipt.effects.Count(e => e.kind == "ControllerGrown" && e.sourceId == "C60"), Is.EqualTo(1));
            Assert.That(result.receipt.effects.Where(e => e.kind == "DieUpgraded").Select(e => e.faceId), Is.EqualTo(A("F02", "F05")), "applied in face id order");
            Assert.That(result.receipt.command.targetFaceIds, Is.EqualTo(A("F05", "F02")), "the command is recorded as sent");
            Assert.That(s.State.shop.selfDieUpgradeUsed, Is.Zero, "a shelf upgrade uses none of the self-selected allowance");
        }

        [Test] public void V111_SelfSelectedGildingOfTwoFacesIsOnePurchaseAndOneAllowance()
        {
            // The example of section 7.1: F02 and F05 of one die, bought once (3 + 1 = 4), one allowance used.
            var s = UpgradeShop(null, "C60"); long gold = s.State.gold; Assert.That(s.CustomDieUpgradesRemaining, Is.EqualTo(2));
            var result = Custom(s, "U06", "D001", "F02", "F05");
            Assert.That(s.State.gold, Is.EqualTo(gold - 4)); Assert.That(s.State.shop.selfDieUpgradeUsed, Is.EqualTo(1)); Assert.That(s.CustomDieUpgradesRemaining, Is.EqualTo(1));
            Assert.That(Face(s.State, "D001", "F02").isGold && Face(s.State, "D001", "F05").isGold, Is.True);
            Assert.That(Card(s.State, "C60").state.growth, Is.EqualTo(50), "from +1 to +1.5");
            var effect = result.receipt.effects.Single(e => e.kind == "CustomDieUpgraded");
            Assert.That(effect.sourceId, Is.EqualTo("U06")); Assert.That(effect.dieId, Is.EqualTo("D001")); Assert.That(effect.detail, Is.EqualTo("F02,F05")); Assert.That(effect.oldValue, Is.EqualTo(4)); Assert.That(effect.newValue, Is.EqualTo(1));
            Assert.That(result.receipt.randomResults, Is.Empty, "the self-selected list draws nothing");
        }

        [Test] public void V111_TwoFacesWithOneIllegalAreRefusedAsAWholeAndTheLegalFaceCanBeChosenAlone()
        {
            // F05 is gold already: the whole request is refused, no gold, allowance, random state or face changes.
            var s = UpgradeShop("U06", out string offer, x => { Face(x, "D001", "F05").isGold = true; }, "C60");
            Assert.That(CustomRefused(s, "U06", "D001", "F02", "F05"), Is.EqualTo(GameSession.UpgradeTargetRejected));
            Assert.That(Refused(s, CommandKind.Buy, c => { c.offerId = offer; c.targetDieId = "D001"; c.targetFaceIds = A("F02", "F05"); }), Is.EqualTo(GameSession.UpgradeTargetRejected));
            Assert.That(Face(s.State, "D001", "F02").isGold, Is.False); Assert.That(s.State.shop.selfDieUpgradeUsed, Is.Zero); Assert.That(Card(s.State, "C60").state.growth, Is.Zero);
            Assert.That(s.CustomDieUpgradeError("U06", "D001", A("F02", "F05")), Is.EqualTo(GameSession.UpgradeTargetRejected)); Assert.That(s.CustomDieUpgradeError("U06", "D001", A("F02")), Is.Null);
            Assert.That(s.LegalUpgradeFaces("U06", "D001"), Is.EqualTo(A("F01", "F02", "F03", "F04", "F06")));
            Custom(s, "U06", "D001", "F02"); Assert.That(Face(s.State, "D001", "F02").isGold, Is.True); Assert.That(s.State.shop.selfDieUpgradeUsed, Is.EqualTo(1));
        }

        [Test] public void V111_TheFaceCountOfAnUpgradeFollowsItsDefinition()
        {
            var s = UpgradeShop();
            Assert.That(A("U01", "U02", "U03", "U04", "U06", "U07", "U08", "U09", "U10").Select(s.MaxSelectableFaces), Is.All.EqualTo(2)); Assert.That(s.MaxSelectableFaces("U05"), Is.Zero); Assert.That(s.MaxSelectableFaces("U11"), Is.EqualTo(1));
            Assert.That(CustomRefused(s, "U06", "D001"), Is.EqualTo(GameSession.UpgradeFaceCountRejected), "no face");
            Assert.That(CustomRefused(s, "U06", "D001", "F02", "F03", "F04"), Is.EqualTo(GameSession.UpgradeFaceCountRejected), "three faces");
            Assert.That(CustomRefused(s, "U06", "D001", "F02", "F02"), Is.EqualTo(GameSession.UpgradeFaceCountRejected), "the two faces must differ");
            Assert.That(CustomRefused(s, "U06", "D001", "F02", "F99"), Is.EqualTo(GameSession.UpgradeTargetRejected), "an unknown face");
            Assert.That(CustomRefused(s, "U06", "D999", "F02"), Is.EqualTo(GameSession.UpgradeTargetRejected), "an unknown die");
            Assert.That(CustomRefused(s, "U99", "D001", "F02"), Is.EqualTo(GameSession.UnknownDieUpgrade));
            // U05 takes a die and no face: two face ids cannot buy it, neither can one.
            Assert.That(CustomRefused(s, "U05", "D001", "F01", "F02"), Is.EqualTo(GameSession.UpgradeTakesWholeDie)); Assert.That(CustomRefused(s, "U05", "D001", "F01"), Is.EqualTo(GameSession.UpgradeTakesWholeDie));
            Assert.That(s.LegalUpgradeFaces("U05", "D001"), Is.Empty); Assert.That(s.IsUpgradeTargetLegal("U05", "D001", null), Is.True);
            Custom(s, "U05", "D001"); Assert.That(s.State.dice.First(d => d.dieId == "D001").isGlass, Is.True);
            Assert.That(CustomRefused(s, "U05", "D001"), Is.EqualTo(GameSession.UpgradeTargetRejected), "already glass");
            // U11 takes exactly one source face.
            Assert.That(CustomRefused(s, "U11", "D002", "F01", "F02"), Is.EqualTo(GameSession.UpgradeSingleFaceOnly));
        }

        [Test] public void V111_TheOldSingleFaceFieldIsAOneElementArrayAndMustAgreeWithTheArray()
        {
            var s = UpgradeShop("U06", out string offer);
            Assert.That(Refused(s, CommandKind.Buy, c => { c.offerId = offer; c.targetDieId = "D001"; c.targetFaceId = "F02"; c.targetFaceIds = A("F03"); }), Is.EqualTo(GameSession.UpgradeFacesInconsistent));
            Assert.That(Refused(s, CommandKind.Buy, c => { c.offerId = offer; c.targetDieId = "D001"; c.targetFaceId = "F02"; c.targetFaceIds = A("F02", "F03"); }), Is.EqualTo(GameSession.UpgradeFacesInconsistent));
            Assert.That(Refused(s, CommandKind.CustomDieUpgrade, c => { c.upgradeId = "U06"; c.targetDieId = "D001"; c.targetFaceId = "F02"; c.targetFaceIds = A("F03"); }), Is.EqualTo(GameSession.UpgradeFacesInconsistent));
            Do(s, CommandKind.Buy, c => { c.offerId = offer; c.targetDieId = "D001"; c.targetFaceId = "F02"; c.targetFaceIds = A("F02"); });
            Assert.That(Face(s.State, "D001", "F02").isGold, Is.True);
            Do(s, CommandKind.CustomDieUpgrade, c => { c.upgradeId = "U06"; c.targetDieId = "D001"; c.targetFaceId = "F03"; });
            Assert.That(Face(s.State, "D001", "F03").isGold, Is.True);
            // Any other purchase ignores the face array.
            string item = null; s = Edit(s, x => item = PutOffer(x, "consumable", "I06")); Do(s, CommandKind.Buy, c => { c.offerId = item; c.targetFaceIds = A("F01", "F01", "F01"); });
        }

        [Test] public void V111_ARandomTransformOfTwoFacesDrawsInFaceIdOrderFromTheFiveOtherPips()
        {
            // F02 shows 2: the other pips ascending are 1,3,4,5,6, ticket 4 -> 6. F05 shows 5: 1,2,3,4,6, ticket 0 -> 1.
            var s = UpgradeShop(x => x.effectRngState = SeedFor(5, 4, 5, 0));
            Assert.That(Face(s.State, "D001", "F02").permanentPip, Is.EqualTo(2)); Assert.That(Face(s.State, "D001", "F05").permanentPip, Is.EqualTo(5));
            var result = Custom(s, "U07", "D001", "F05", "F02");
            Assert.That(result.receipt.randomResults.Select(t => t.stream + ":" + t.kind + ":" + t.sourceId + ":" + t.range + ":" + t.rawTicket), Is.EqualTo(A("effect:FaceTransform:D001/F02:5:4", "effect:FaceTransform:D001/F05:5:0")));
            Assert.That(Face(s.State, "D001", "F02").permanentPip, Is.EqualTo(6)); Assert.That(Face(s.State, "D001", "F05").permanentPip, Is.EqualTo(1));
            Assert.That(s.State.shopRngState, Is.EqualTo(UpgradeShop(x => x.effectRngState = SeedFor(5, 4, 5, 0)).State.shopRngState), "the shop stream is not used");
        }

        [Test] public void V111_GildingAddsOneWeightUpToFiveAndRefusesAGoldFace()
        {
            // The example of section 7.2: two faces of weight 1 and 5 are gilded to 2 and 5.
            var s = UpgradeShop(x => Face(x, "D001", "F05").baseWeight = 5);
            var result = Custom(s, "U06", "D001", "F02", "F05");
            Assert.That(Face(s.State, "D001", "F02").baseWeight, Is.EqualTo(2)); Assert.That(Face(s.State, "D001", "F05").baseWeight, Is.EqualTo(5)); Assert.That(Face(s.State, "D001", "F05").isGold, Is.True);
            Assert.That(result.receipt.effects.Where(e => e.kind == "FaceChanged").Select(e => e.faceId + ":" + e.detail + ":" + e.oldValue + ">" + e.newValue), Is.EqualTo(A("F02:gold:0>1", "F02:weightDelta:1>2", "F05:gold:0>1")));
            Assert.That(Face(s.State, "D001", "F02").baseColor, Is.EqualTo(Face(UpgradeShop().State, "D001", "F02").baseColor), "the base colour stays");
            Assert.That(CustomRefused(s, "U06", "D001", "F02"), Is.EqualTo(GameSession.UpgradeTargetRejected), "already gold");
            Assert.That(Face(s.State, "D001", "F02").cultivated, Is.False, "a shop upgrade is not a card rewrite");
            // Standard die, two faces gilded from 1 to 2: a gold face comes up with probability 4/8.
            var units = s.ProbabilityUnits("D002"); Assert.That(units.Sum(), Is.EqualTo(1000000));
            var even = UpgradeShop(); Custom(even, "U06", "D002", "F01", "F02"); var weighted = even.ProbabilityUnits("D002");
            Assert.That(weighted[0] + weighted[1], Is.EqualTo(500000), "weights 2,2,1,1,1,1: the two gold faces hold 4 of 8");
        }

        [Test] public void V111_TheGildingPenAddsWeightAndStopsAtFive()
        {
            string pen = null;
            var s = Hand(Start(A("C134"), x => pen = AddItem(x, "I11")), A("D001"), 0, null, null, x => Face(x, "D001", "F01").baseWeight = 5);
            var used = Do(s, CommandKind.UseItem, c => c.itemInstanceId = pen);
            Assert.That(Face(s.State, "D001", "F01").isGold, Is.True); Assert.That(Face(s.State, "D001", "F01").baseWeight, Is.EqualTo(5), "the cap");
            Assert.That(used.receipt.effects.Single(e => e.kind == "ItemUsed").oldValue, Is.EqualTo(1)); Assert.That(used.receipt.effects.Single(e => e.kind == "ItemUsed").newValue, Is.Zero);
            Assert.That(s.State.inventory, Is.Empty); Assert.That(s.State.consumedItemIds, Does.Contain(pen)); Assert.That(Card(s.State, "C134").state.growth, Is.EqualTo(40));
            string second = null; var light = Hand(Start(A("C60"), x => second = AddItem(x, "I11")), A("D002"));
            Do(light, CommandKind.UseItem, c => c.itemInstanceId = second); Assert.That(Face(light.State, "D002", "F01").baseWeight, Is.EqualTo(2));
            Assert.That(Card(light.State, "C60").state.growth, Is.Zero, "an item rewrite is not a die upgrade transaction");
        }

        [Test] public void V111_AMirrorCopiesPipAndColourOnlyAndCountsOnce()
        {
            // Source F02: 5, blue, gold, weight 3. Its opposite F05 becomes 5 blue; its own gold state and weight stay.
            var s = UpgradeShop(x => { var f = Face(x, "D001", "F02"); f.permanentPip = 5; f.baseColor = "blue"; f.isGold = true; f.baseWeight = 3; var o = Face(x, "D001", "F05"); o.permanentPip = 2; o.baseColor = "red"; }, "C60");
            Custom(s, "U11", "D001", "F02"); var target = Face(s.State, "D001", "F05");
            Assert.That(target.permanentPip, Is.EqualTo(5)); Assert.That(target.baseColor, Is.EqualTo("blue")); Assert.That(target.isGold, Is.False); Assert.That(target.baseWeight, Is.EqualTo(1));
            Assert.That(Card(s.State, "C60").state.growth, Is.EqualTo(50));
            Custom(s, "U05", "D002"); Assert.That(Card(s.State, "C60").state.growth, Is.EqualTo(100), "a whole die is one transaction too");
        }

        [Test] public void V111_SelfSelectedUpgradesAreLimitedToTwoPerShopAndARefreshOrReloadDoesNotResetThem()
        {
            var s = UpgradeShop("U01", out string shelf);
            Custom(s, "U03", "D001", "F02"); Custom(s, "U04", "D001", "F01"); Assert.That(s.State.shop.selfDieUpgradeUsed, Is.EqualTo(2)); Assert.That(s.CustomDieUpgradesRemaining, Is.Zero);
            Assert.That(CustomRefused(s, "U06", "D001", "F03"), Is.EqualTo(GameSession.SelfDieUpgradeLimitReached));
            Do(s, CommandKind.RefreshControllers);
            Assert.That(CustomRefused(s, "U06", "D001", "F03"), Is.EqualTo(GameSession.SelfDieUpgradeLimitReached), "a refresh gives no new allowance");
            var reloaded = new GameSession(cfg, new MemorySaveStore(), s.State); Assert.That(reloaded.CustomDieUpgradesRemaining, Is.Zero);
            Assert.That(reloaded.CustomDieUpgradeOptions().All(o => o.error == GameSession.SelfDieUpgradeLimitReached), Is.True);
            // The unsold shelf upgrade can still be bought, and it does not touch the allowance.
            Do(s, CommandKind.Buy, c => { c.offerId = shelf; c.targetDieId = "D002"; c.targetFaceId = "F01"; });
            Assert.That(Face(s.State, "D002", "F01").texture, Is.EqualTo("smooth")); Assert.That(s.State.shop.selfDieUpgradeUsed, Is.EqualTo(2));
            // The next shop starts at 0 again.
            Do(s, CommandKind.LeaveShop); Do(s, CommandKind.StartStage);
            var next = Hand(s, A("D001"), 0, new[] { 3 }, A("red"), x => x.stage.score = (BigInteger.Parse(x.stage.target) - 1).ToString());
            Do(next, CommandKind.Submit); if (next.State.phase != "StageSettlement") Do(next, CommandKind.EndStage); Do(next, CommandKind.Continue);
            Assert.That(next.State.shop.selfDieUpgradeUsed, Is.Zero); Assert.That(next.CustomDieUpgradesRemaining, Is.EqualTo(2));
        }

        [Test] public void V111_TheSelfSelectedPriceIsBasePlusOneAndTheBargainerAppliesWithAFloorOfOne()
        {
            // Gilding lists 3: self-selected 3 + 1 = 4, shelf 3. With one C98: self-selected 3, shelf 2 (the example of section 9.2).
            var plain = UpgradeShop("U06", out string plainOffer); Assert.That(plain.GetCustomDieUpgradePrice("U06"), Is.EqualTo(4));
            Assert.That(plain.GetOfferPrice(plain.State.shop.offers.Single(o => o.offerId == plainOffer)), Is.EqualTo(3));
            var s = UpgradeShop("U06", out string offer, null, "C98"); Assert.That(s.GetCustomDieUpgradePrice("U06"), Is.EqualTo(3));
            Assert.That(s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(2));
            long gold = s.State.gold; Custom(s, "U06", "D001", "F02"); Assert.That(s.State.gold, Is.EqualTo(gold - 3));
            // A mimic of C98 is a second source: U01 lists 1, 1 + 1 - 2 = 0 -> floor 1; U06 3 + 1 - 2 = 2.
            var two = UpgradeShop(null, "C41", "C98"); Assert.That(two.GetCustomDieUpgradePrice("U01"), Is.EqualTo(1)); Assert.That(two.GetCustomDieUpgradePrice("U06"), Is.EqualTo(2));
            // The item discount (O02) does not apply to upgrades.
            var discount = UpgradeShop(x => x.itemDiscount = 3); Assert.That(discount.GetCustomDieUpgradePrice("U06"), Is.EqualTo(4));
            // Every upgrade is listed in id order with its live price.
            var options = s.CustomDieUpgradeOptions(); Assert.That(options.Select(o => o.upgradeId), Is.EqualTo(cfg.diceUpgrades.Select(u => u.id).OrderBy(x => x, StringComparer.Ordinal)));
            Assert.That(options.All(o => o.price == Math.Max(1, cfg.Upgrade(o.upgradeId).buyPrice + 1 - 1)), Is.True); Assert.That(options.Length, Is.EqualTo(11));
            Assert.That(options.Single(o => o.upgradeId == "U06").error, Is.Null); Assert.That(options.Single(o => o.upgradeId == "U05").maxSelectableFaces, Is.Zero);
        }

        [Test] public void V111_ASelfSelectedUpgradeNeedsGoldAShopAndTheBossLootResolvedFirst()
        {
            var poor = UpgradeShop(x => x.gold = 3); Assert.That(CustomRefused(poor, "U06", "D001", "F02"), Is.EqualTo("金币不足。"));
            Assert.That(poor.CustomDieUpgradeOptions().Single(o => o.upgradeId == "U06").error, Is.EqualTo("金币不足。")); Assert.That(poor.CustomDieUpgradeOptions().Single(o => o.upgradeId == "U01").error, Is.Null);
            var glass = UpgradeShop(x => { foreach (var d in x.dice) d.isGlass = true; }); var u05 = glass.CustomDieUpgradeOptions().Single(o => o.upgradeId == "U05");
            Assert.That(u05.hasTarget, Is.False); Assert.That(u05.error, Is.EqualTo(GameSession.UpgradeTargetRejected));
            var stage = Hand(Start(A()), A("D001")); Assert.That(stage.CustomDieUpgradeOptions(), Is.Empty); Assert.That(stage.CustomDieUpgradesRemaining, Is.Zero);
            Assert.That(Refused(stage, CommandKind.CustomDieUpgrade, c => { c.upgradeId = "U06"; c.targetDieId = "D001"; c.targetFaceIds = A("F02"); }), Is.EqualTo("当前阶段不能执行此操作。"));
            var loot = SettledAt(2, x => x.cycles[0].lootKind = "L4"); Do(loot, CommandKind.Continue); Assert.That(loot.IsLootPending, Is.True);
            Assert.That(CustomRefused(loot, "U06", "D001", "F02"), Is.EqualTo("先领取首领战利品。"));
            // Loot upgrades are free, take the same one or two faces and grow C60 once each.
            var workshop = SettledAt(2, x => x.cycles[0].lootKind = "L4", "C60"); Do(workshop, CommandKind.Continue);
            var free = Loot(workshop.State).First(o => o.category == "die_upgrade" && cfg.Upgrade(o.contentId).maxSelectableFaces == 2);
            var faces = workshop.LegalUpgradeFaces(free.contentId, "D001").Take(2).ToArray(); long before = workshop.State.gold;
            Assert.That(workshop.PurchaseErrorForFaces(free.offerId, "D001", faces), Is.Null);
            Do(workshop, CommandKind.Buy, c => { c.offerId = free.offerId; c.targetDieId = "D001"; c.targetFaceIds = faces; });
            Assert.That(workshop.State.gold, Is.EqualTo(before)); Assert.That(Card(workshop.State, "C60").state.growth, Is.EqualTo(50)); Assert.That(workshop.State.shop.selfDieUpgradeUsed, Is.Zero);
        }

        // ---- 5. hand and colour upgrades (sections 3.1, 9.2) ----

        [Test] public void V111_AnUpgradeCostsBasePlusTheCurrentLevelAndStopsAtTwelve()
        {
            // The example of section 9.2: level 3, base 2: shelf 5, self study 6.
            string offer = null; var s = Edit(Shop(), x => { x.gold = 100; x.handLevels.Single(h => h.handId == "pair").level = 3; offer = PutOffer(x, "hand_upgrade", "pair"); });
            Assert.That(s.GetHandUpgradePrice("pair", false), Is.EqualTo(5)); Assert.That(s.GetHandUpgradePrice("pair", true), Is.EqualTo(6)); Assert.That(s.GetStudyPrice("pair"), Is.EqualTo(6));
            Assert.That(s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(5));
            Do(s, CommandKind.Buy, c => c.offerId = offer); Assert.That(s.State.gold, Is.EqualTo(95)); Do(s, CommandKind.Study, c => c.handId = "pair"); Assert.That(s.State.gold, Is.EqualTo(95 - 7), "level 4: 2 + 4 + 1");
            // Colours follow the same rule: all_red at level 11 costs 2 + 11 = 13 on the shelf; level 12 is the cap.
            string red = null; var top = Edit(Shop(), x => { x.gold = 100; x.colorLevels.Single(h => h.handId == "all_red").level = 11; red = PutOffer(x, "hand_upgrade", "all_red"); });
            Assert.That(top.GetHandUpgradePrice("all_red", false), Is.EqualTo(13)); Do(top, CommandKind.Buy, c => c.offerId = red);
            Assert.That(top.State.colorLevels.Single(h => h.handId == "all_red").level, Is.EqualTo(12)); Assert.That(top.LegalStudyHands(), Does.Not.Contain("all_red"));
            Assert.That(Refused(top, CommandKind.Study, c => c.handId = "all_red"), Does.Contain("满级"));
            string again = null; top = Edit(top, x => again = PutOffer(x, "hand_upgrade", "all_red")); Assert.That(Refused(top, CommandKind.Buy, c => c.offerId = again), Does.Contain("满级"));
            Assert.That(cfg.maxHandLevel, Is.EqualTo(12)); Assert.That(cfg.maxColorLevel, Is.EqualTo(12)); Assert.That(cfg.handDefinitions.All(h => h.maxLevel == 12), Is.True);
        }

        [Test] public void V111_AFreeLootUpgradeAtLevelElevenGivesOneLevelAndTheScholarGrowsOnce()
        {
            // The example of section 5 (C61): level 11, loot worth 2 levels, 1 level gained, +0.5 once.
            var s = SettledAt(2, x => x.cycles[0].lootKind = "L3", "C61"); Do(s, CommandKind.Continue);
            var hand = Loot(s.State).First(o => o.category == "hand_upgrade"); Assert.That(hand.levels, Is.EqualTo(2)); Assert.That(s.GetOfferPrice(hand), Is.Zero);
            s = Edit(s, x => x.handLevels.Concat(x.colorLevels).Single(h => h.handId == hand.contentId).level = 11);
            var result = Do(s, CommandKind.Buy, c => c.offerId = hand.offerId);
            Assert.That(s.State.handLevels.Concat(s.State.colorLevels).Single(h => h.handId == hand.contentId).level, Is.EqualTo(12));
            Assert.That(Card(s.State, "C61").state.growth, Is.EqualTo(50)); Assert.That(result.receipt.effects.Count(e => e.kind == "ControllerGrown" && e.sourceId == "C61"), Is.EqualTo(1));
            // At level 12 the loot upgrade is refused.
            var full = SettledAt(2, x => x.cycles[0].lootKind = "L3", "C61"); Do(full, CommandKind.Continue); var capped = Loot(full.State).First(o => o.category == "hand_upgrade");
            full = Edit(full, x => x.handLevels.Concat(x.colorLevels).Single(h => h.handId == capped.contentId).level = 12);
            Assert.That(Refused(full, CommandKind.Buy, c => c.offerId = capped.offerId), Does.Contain("满级")); Assert.That(Card(full.State, "C61").state.growth, Is.Zero);
            // A paid level and a self study each grow it by 0.5 per level actually gained.
            string offer = null; var paid = Edit(Shop(), x => { x.gold = 100; Equip(x, "C61"); offer = PutOffer(x, "hand_upgrade", "trips"); });
            Do(paid, CommandKind.Buy, c => c.offerId = offer); Do(paid, CommandKind.Study, c => c.handId = "all_blue"); Assert.That(Card(paid.State, "C61").state.growth, Is.EqualTo(100));
        }

        // ---- 6. item uses and the key ring (section 8) ----

        [Test] public void V111_AKeyRingIsBoughtWithThreeUsesAtTheItemPriceAndSellsForOneWhateverIsLeft()
        {
            string offer = null; var s = Edit(Shop(), x => { x.gold = 100; offer = PutOffer(x, "consumable", "I13"); });
            Assert.That(s.GetOfferPrice(s.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(3));
            Do(s, CommandKind.Buy, c => c.offerId = offer); Assert.That(s.State.gold, Is.EqualTo(97)); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(3)); Assert.That(s.State.inventory.Count, Is.EqualTo(1), "one slot");
            var cheap = Edit(Shop(), x => { x.gold = 100; x.itemDiscount = 3; offer = PutOffer(x, "consumable", "I13"); });
            Assert.That(cheap.GetOfferPrice(cheap.State.shop.offers.Single(o => o.offerId == offer)), Is.EqualTo(1), "3 - 3 stays at the floor of 1");
            foreach (int left in new[] { 3, 2, 1 })
            {
                var sale = Edit(s, x => Key(x).usesRemaining = left); long gold = sale.State.gold; Do(sale, CommandKind.Sell, c => c.itemInstanceId = Key(sale.State).instanceId);
                Assert.That(sale.State.gold, Is.EqualTo(gold + 1), left + " uses left"); Assert.That(sale.State.inventory, Is.Empty);
            }
            // Every other item is created with one use; the bag of tricks (C110) and the supply loot create a key ring with three.
            string other = null; var item = Edit(Shop(), x => { x.gold = 100; other = PutOffer(x, "consumable", "I06"); }); Do(item, CommandKind.Buy, c => c.offerId = other); Assert.That(item.State.inventory.Single().usesRemaining, Is.EqualTo(1));
            var ids = cfg.consumables.Select(c => c.id).OrderBy(x => x, StringComparer.Ordinal).ToArray(); Assert.That(ids.Length, Is.EqualTo(13)); Assert.That(ids.Last(), Is.EqualTo("I13"));
            var bag = Edit(New(), x => { Equip(x, "C110"); x.effectRngState = SeedFor(13, 12); }); Do(bag, CommandKind.StartStage);
            Assert.That(Key(bag.State).usesRemaining, Is.EqualTo(3));
            GameSession supply = null; ShopOffer ring = null;
            var settledBase = SettledAt(2, x => x.cycles[0].lootKind = "L6");
            for (uint seed = 1; seed <= 80 && ring == null; seed++) { supply = Open(settledBase, seed); ring = Loot(supply.State).FirstOrDefault(o => o.contentId == "I13"); }
            Assert.That(ring, Is.Not.Null); Do(supply, CommandKind.Buy, c => c.offerId = ring.offerId); Assert.That(Key(supply.State).usesRemaining, Is.EqualTo(3));
        }

        [Test] public void V111_TheKeyRingCountsDownThreeTwoOneAndFreesItsSlotAtZero()
        {
            string key = null; var s = Start(A("C134"), x => { key = AddItem(x, "I13"); AddItem(x, "I06"); }); Assert.That(s.State.consumableSlots, Is.EqualTo(2));
            s = Hand(s, A("D001", "D002", "D003", "D004"));
            for (int left = 3; left >= 1; left--)
            {
                Assert.That(Key(s.State).usesRemaining, Is.EqualTo(left)); Assert.That(s.State.inventory.Count, Is.EqualTo(2), "the ring holds one slot while it has uses");
                var result = Unlock(s, "D00" + left);
                var used = result.receipt.effects.Single(e => e.kind == "ItemUsed"); Assert.That(used.sourceId, Is.EqualTo("I13")); Assert.That(used.detail, Is.EqualTo(key));
                Assert.That(used.oldValue, Is.EqualTo(left)); Assert.That(used.newValue, Is.EqualTo(left - 1));
                var unlocked = result.receipt.effects.Single(e => e.kind == "DieUnlocked"); Assert.That(unlocked.sourceId, Is.EqualTo("I13")); Assert.That(unlocked.dieId, Is.EqualTo("D00" + left)); Assert.That(unlocked.detail, Is.EqualTo(key));
                Assert.That(Kinds(result), Is.EqualTo("DieUnlocked,ItemUsed,ControllerGrown"), "unlock first, then the item use, then C134");
                Assert.That(Card(s.State, "C134").state.growth, Is.EqualTo(40 * (4 - left)), "+0.4 per use");
            }
            Assert.That(s.State.inventory.Select(i => i.contentId), Is.EqualTo(A("I06")), "the slot is free the moment the last use is spent"); Assert.That(s.State.consumedItemIds, Does.Contain(key));
            Assert.That(Refused(s, CommandKind.UnlockDie, c => c.targetDieId = "D004"), Is.EqualTo(GameSession.UnlockNoSource));
        }

        [Test] public void V111_TheHoarderCountsAKeyRingAsOneItemWhateverItsUses()
        {
            // One blue 6 with C135: (6 + 15 per item) x (1 + 0.25). One full key ring: 21 x 1.25 = 26.25 -> 26; the
            // same ring with one use left: still 26; a ring and another item: 36 x 1.25 = 45.
            foreach (int left in new[] { 3, 1 })
            {
                var s = Hand(Start(A("C135"), x => AddItem(x, "I13")), A("D001"), 0, new[] { 6 }, A("blue"), x => Key(x).usesRemaining = left);
                var result = Do(s, CommandKind.Submit); Assert.That(result.receipt.score, Is.EqualTo("26"), left + " uses"); Assert.That(result.receipt.submission.snapshot.inventoryCount, Is.EqualTo(1));
            }
            var two = Hand(Start(A("C135"), x => { AddItem(x, "I13"); AddItem(x, "I06"); }), A("D001"), 0, new[] { 6 }, A("blue"));
            Assert.That(Do(two, CommandKind.Submit).receipt.score, Is.EqualTo("45"));
        }

        [Test] public void V111_KeyRingUsesSurviveTheHandTheStageAndAReload()
        {
            var s = Hand(Start(A(), x => AddItem(x, "I13")), A("D001", "D002"), 0, new[] { 3, 3 }, A("red", "red"), x => x.stage.score = (BigInteger.Parse(x.stage.target) - 1).ToString());
            Unlock(s, "D002"); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(2));
            var reloaded = new GameSession(cfg, new MemorySaveStore(), s.State); Assert.That(Key(reloaded.State).usesRemaining, Is.EqualTo(2));
            Do(s, CommandKind.Submit); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(2), "a new hand");
            if (s.State.phase != "StageSettlement") Do(s, CommandKind.EndStage);
            Do(s, CommandKind.Continue); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(2), "the shop");
            Do(s, CommandKind.LeaveShop); Do(s, CommandKind.StartStage); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(2), "the next stage");
        }

        [Test] public void V111_AKeyRingIsNotUsedThroughTheItemCommand()
        {
            string key = null; var s = Hand(Start(A(), x => key = AddItem(x, "I13")), A("D001"));
            Assert.That(Refused(s, CommandKind.UseItem, c => c.itemInstanceId = key), Is.EqualTo(GameSession.KeyRingUsesUnlock)); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(3));
        }

        // ---- 7. the unlock command (section 8) ----

        [Test] public void V111_TheFreeUnlockComesFirstThenTheKeyRingAndTheOthersKeepTheirOrder()
        {
            // The example of section 8: locked D003, D001, D004; an effective C154 and a key ring with 3 uses.
            string key = null; var s = Hand(Start(A("C154", "C134"), x => key = AddItem(x, "I13")), A("D003", "D001", "D004"));
            var quote = s.UnlockQuote("D001"); Assert.That(quote.available, Is.True); Assert.That(quote.sourceId, Is.EqualTo("C154")); Assert.That(quote.sourceInstanceId, Is.EqualTo(Card(s.State, "C154").instanceId));
            Assert.That(quote.freeAvailable, Is.True); Assert.That(quote.keyRingUses, Is.EqualTo(3)); Assert.That(quote.keyRingUsesTotal, Is.EqualTo(3));
            var free = Unlock(s, "D001");
            Assert.That(Kinds(free), Is.EqualTo("DieUnlocked"), "the free unlock records the unlock only");
            var effect = free.receipt.effects.Single(); Assert.That(effect.sourceId, Is.EqualTo("C154")); Assert.That(effect.dieId, Is.EqualTo("D001")); Assert.That(effect.detail, Is.EqualTo(Card(s.State, "C154").instanceId));
            Assert.That(s.State.hand.unlockCardUsed, Is.True); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(3)); Assert.That(s.State.hand.orderedLockedIds, Is.EqualTo(A("D003", "D004")));
            Assert.That(Card(s.State, "C134").state.growth, Is.Zero, "a free unlock is not an item use");
            quote = s.UnlockQuote("D003"); Assert.That(quote.sourceId, Is.EqualTo("I13")); Assert.That(quote.sourceInstanceId, Is.EqualTo(key)); Assert.That(quote.freeAvailable, Is.False);
            var paid = Unlock(s, "D003");
            Assert.That(Key(s.State).usesRemaining, Is.EqualTo(2)); Assert.That(s.State.hand.orderedLockedIds, Is.EqualTo(A("D004")), "only D004 is still locked");
            Assert.That(paid.receipt.effects.Single(e => e.kind == "DieUnlocked").sourceId, Is.EqualTo("I13"));
            // Both dice are back in the pool at their creation-order places, unlocked.
            Assert.That(s.State.hand.currentResults.Where(r => !r.isLocked).Select(r => r.dieId), Is.EqualTo(A("D001", "D002", "D003", "D005")));
            Assert.That(s.PreviewSnapshot().poolDice.Select(p => p.dieId), Is.EqualTo(A("D001", "D002", "D003", "D005")));
            // The next hand: the free unlock is back, the ring still has 2.
            Do(s, CommandKind.Submit); Assert.That(s.State.hand.unlockCardUsed, Is.False); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(2));
        }

        [Test] public void V111_OneHandWithAFreeUnlockAndTwoKeyUsesGrowsTheItemCardTwice()
        {
            // The example of section 5 (C134): +0.4 x 2 = +0.8.
            var s = Hand(Start(A("C154", "C134"), x => AddItem(x, "I13")), A("D001", "D002", "D003"));
            Unlock(s, "D001"); Unlock(s, "D002"); Unlock(s, "D003");
            Assert.That(Card(s.State, "C134").state.growth, Is.EqualTo(80)); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(1)); Assert.That(s.State.hand.orderedLockedIds, Is.Empty);
        }

        [Test] public void V111_UnderTheNoItemsBossTheFreeUnlockWorksAndTheKeyRingIsRefusedWithoutLosingAUse()
        {
            // B02 forbids items. The example of section 8: with the free unlock used, the ring is refused and keeps its uses.
            var s = Hand(Boss("B02", A("C154"), x => AddItem(x, "I13")), A("D001", "D002"));
            Assert.That(s.CurrentModifierId, Is.EqualTo("B02")); var quote = s.UnlockQuote("D001"); Assert.That(quote.sourceId, Is.EqualTo("C154")); Assert.That(quote.keyRingBlocked, Is.True);
            Unlock(s, "D001"); Assert.That(s.State.hand.unlockCardUsed, Is.True);
            quote = s.UnlockQuote("D002"); Assert.That(quote.available, Is.False); Assert.That(quote.error, Is.EqualTo(GameSession.ItemsForbidden)); Assert.That(quote.keyRingUses, Is.EqualTo(3));
            Assert.That(Refused(s, CommandKind.UnlockDie, c => c.targetDieId = "D002"), Is.EqualTo(GameSession.ItemsForbidden)); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(3));
            // Without the card the ring alone cannot be used there.
            var bare = Hand(Boss("B02", A(), x => AddItem(x, "I13")), A("D001")); Assert.That(Refused(bare, CommandKind.UnlockDie, c => c.targetDieId = "D001"), Is.EqualTo(GameSession.ItemsForbidden));
        }

        [Test] public void V111_CopiesAndMimicsShareOneFreeUnlockAndADisabledCardGivesNone()
        {
            // Two real C154: one free unlock per hand, named after the leftmost source.
            var two = Hand(Start(A("C154", "C154")), A("D001", "D002"));
            var first = Unlock(two, "D001"); Assert.That(first.receipt.effects.Single().detail, Is.EqualTo(Card(two.State, "C154", 0).instanceId));
            Assert.That(Refused(two, CommandKind.UnlockDie, c => c.targetDieId = "D002"), Is.EqualTo(GameSession.UnlockNoSource));
            // A mimic to the left of the card is the leftmost source; the allowance is still one.
            string key = null; var mimic = Hand(Start(A("C41", "C154"), x => key = AddItem(x, "I13")), A("D001", "D002"));
            var copied = Unlock(mimic, "D001"); Assert.That(copied.receipt.effects.Single().sourceId, Is.EqualTo("C154")); Assert.That(copied.receipt.effects.Single().detail, Is.EqualTo(Card(mimic.State, "C41").instanceId));
            Unlock(mimic, "D002"); Assert.That(Key(mimic.State).usesRemaining, Is.EqualTo(2), "the second unlock of the hand pays with the ring");
            // B25 disables the two leftmost cards: the C154 there is no source, so the ring pays at once.
            var disabled = Hand(Boss("B25", A("C154", "C13", "C01"), x => AddItem(x, "I13")), A("D001"));
            Assert.That(disabled.State.stage.disabledControllerIds, Does.Contain(Card(disabled.State, "C154").instanceId));
            Assert.That(disabled.UnlockQuote("D001").sourceId, Is.EqualTo("I13")); Unlock(disabled, "D001"); Assert.That(Key(disabled.State).usesRemaining, Is.EqualTo(2)); Assert.That(disabled.State.hand.unlockCardUsed, Is.False);
            // Two key rings: the earliest one in the inventory is used up first.
            string a = null, b = null; var rings = Hand(Start(A(), x => { a = AddItem(x, "I13"); b = AddItem(x, "I13"); x.inventory[0].usesRemaining = 1; }), A("D001", "D002"));
            Assert.That(rings.UnlockQuote().keyRingUsesTotal, Is.EqualTo(4));
            Unlock(rings, "D001"); Assert.That(rings.State.inventory.Single().instanceId, Is.EqualTo(b)); Unlock(rings, "D002"); Assert.That(rings.State.inventory.Single().usesRemaining, Is.EqualTo(2));
        }

        [Test] public void V111_AnUnlockNeedsALockedDieWithAResultAndASourceAndRefusalsCostNothing()
        {
            string key = null; var s = Hand(Start(A(), x => key = AddItem(x, "I13")), A("D001", "D002"));
            Assert.That(Refused(s, CommandKind.UnlockDie, c => c.targetDieId = "D003"), Is.EqualTo(GameSession.UnlockTargetInvalid), "not locked");
            Assert.That(Refused(s, CommandKind.UnlockDie, c => c.targetDieId = "D999"), Is.EqualTo(GameSession.UnlockTargetInvalid), "no such die");
            Assert.That(Refused(s, CommandKind.UnlockDie), Is.EqualTo(GameSession.UnlockTargetInvalid), "no target");
            Assert.That(s.UnlockQuote("D003").available, Is.False); Assert.That(s.UnlockQuote().available, Is.True, "some locked die can be unlocked");
            // Dice selected but not confirmed: the old gate refuses the command and nothing is spent.
            Assert.That(s.TogglePending("D003"), Is.True);
            Assert.That(Refused(s, CommandKind.UnlockDie, c => c.targetDieId = "D001"), Is.EqualTo("先锁定选中的骰子，或取消选择。")); Assert.That(s.UnlockQuote("D001").available, Is.False);
            s.ClearPending(); Assert.That(s.UnlockQuote("D001").available, Is.True);
            // Before the first roll of a hand there is nothing to unlock.
            var ready = Start(A("C154"), x => AddItem(x, "I13")); Assert.That(ready.State.phase, Is.EqualTo("HandReady"));
            Assert.That(Refused(ready, CommandKind.UnlockDie, c => c.targetDieId = "D001"), Is.EqualTo("当前阶段不能执行此操作。")); Assert.That(ready.UnlockQuote("D001").error, Is.EqualTo(GameSession.UnlockNeedsResults));
            // No card and no ring.
            var none = Hand(Start(A()), A("D001")); Assert.That(Refused(none, CommandKind.UnlockDie, c => c.targetDieId = "D001"), Is.EqualTo(GameSession.UnlockNoSource));
            Assert.That(none.UnlockQuote("D001").sourceId, Is.Empty);
            // In a shop the command is refused.
            var shop = Edit(Shop(), x => AddItem(x, "I13")); Assert.That(Refused(shop, CommandKind.UnlockDie, c => c.targetDieId = "D001"), Is.EqualTo("当前阶段不能执行此操作。")); Assert.That(shop.UnlockQuote().available, Is.False);
        }

        [Test] public void V111_AnUnlockSpendsNoRollDrawsNothingAndKeepsTheFaceOfTheDie()
        {
            // No roll left; D002 shows a remote-set 6 on its face F01.
            var s = Hand(Start(A("C154", "C05")), A("D001", "D002", "D003", "D004", "D005"), 0, null, null, x => { var r = x.hand.currentResults.First(v => v.dieId == "D002"); r.currentPip = 6; r.remoteOverride = true; });
            var before = s.State; var locked = before.hand.currentResults.First(r => r.dieId == "D002");
            var result = Unlock(s, "D002"); var after = s.State; var back = after.hand.currentResults.First(r => r.dieId == "D002");
            Assert.That(result.receipt.randomResults, Is.Empty);
            Assert.That(after.diceRngState, Is.EqualTo(before.diceRngState)); Assert.That(after.effectRngState, Is.EqualTo(before.effectRngState)); Assert.That(after.shopRngState, Is.EqualTo(before.shopRngState)); Assert.That(after.stageRngState, Is.EqualTo(before.stageRngState));
            Assert.That(after.hand.rollsRemaining, Is.Zero); Assert.That(after.hand.rollsUsed, Is.EqualTo(before.hand.rollsUsed)); Assert.That(after.phase, Is.EqualTo("Selecting"));
            Assert.That(back.isLocked, Is.False); Assert.That(back.hasRolled, Is.True); Assert.That(back.currentFaceId, Is.EqualTo(locked.currentFaceId)); Assert.That(back.currentPip, Is.EqualTo(6)); Assert.That(back.currentColor, Is.EqualTo(locked.currentColor)); Assert.That(back.remoteOverride, Is.True);
            // One die is unlocked with no roll left, which is C05's condition - but an unlock grants nothing.
            Assert.That(result.receipt.effects.Any(e => e.kind == "ExtraRolls"), Is.False); Assert.That(after.hand.c05UsedEmitterIds, Is.Empty);
            Assert.That(after.stage.remainingSubmissions, Is.EqualTo(before.stage.remainingSubmissions)); Assert.That(after.dice.Count, Is.EqualTo(before.dice.Count));
            // It can stay in the pool (the submission then has four dice) or be locked again.
            Assert.That(s.PreviewSnapshot().dice.Select(d => d.dieId), Is.EqualTo(A("D001", "D003", "D004", "D005"))); Assert.That(s.PreviewSnapshot().poolDice.Single().dieId, Is.EqualTo("D002"));
            Lock(s, "D002"); Assert.That(s.State.hand.orderedLockedIds, Is.EqualTo(A("D001", "D003", "D004", "D005", "D002")), "locked again, at the end of the order");
        }

        [Test] public void V111_C05JudgesTheUnlockedCountAtTheNextRollThatUsesTheLastRoll()
        {
            // One roll left, all five locked. Unlocking D003 grants nothing; the roll that uses the last roll then sees
            // one unlocked die (within 1-2) and C05 grants its three rolls.
            var s = Hand(Start(A("C154", "C05")), A("D001", "D002", "D003", "D004", "D005"), 1);
            Unlock(s, "D003"); Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(1));
            var roll = Do(s, CommandKind.Roll);
            Assert.That(roll.receipt.randomResults.Where(t => t.kind == "DieFace").Select(t => t.sourceId), Is.EqualTo(A("D003")), "only the unlocked die is rolled");
            Assert.That(roll.receipt.effects.Count(e => e.kind == "ExtraRolls"), Is.EqualTo(1)); Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(3));
        }

        [Test] public void V111_TheLastLockedDieCanBeUnlockedButASubmissionStillNeedsOne()
        {
            var s = Hand(Start(A("C154")), A("D001"));
            Unlock(s, "D001"); Assert.That(s.State.hand.orderedLockedIds, Is.Empty); Assert.That(s.PreviewSnapshot(), Is.Null);
            Assert.That(Refused(s, CommandKind.Submit), Does.Contain("锁定至少 1 颗"));
            Lock(s, "D002"); Do(s, CommandKind.Submit);
        }

        [Test] public void V111_ARepeatedUnlockRequestReturnsTheFirstReceiptAndAFailedSaveIsRetriedOnce()
        {
            var s = Hand(Start(A(), x => AddItem(x, "I13")), A("D001", "D002"));
            var command = Command(s, CommandKind.UnlockDie); command.targetDieId = "D001";
            var first = s.Execute(command); Assert.That(first.accepted, Is.True); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(2));
            var again = s.Execute(command); Assert.That(again.duplicate, Is.True); Assert.That(again.receipt.stateHash, Is.EqualTo(first.receipt.stateHash)); Assert.That(again.receipt.effects.Count, Is.EqualTo(first.receipt.effects.Count));
            Assert.That(Key(s.State).usesRemaining, Is.EqualTo(2), "no second use"); Assert.That(s.State.hand.orderedLockedIds, Is.EqualTo(A("D002")));
            // The same request after a reload of the saved state is still the first receipt.
            var reloaded = new GameSession(cfg, new MemorySaveStore(), s.State); Assert.That(reloaded.Execute(command).duplicate, Is.True); Assert.That(Key(reloaded.State).usesRemaining, Is.EqualTo(2));
            // A failed write: nothing is committed; the retry stores the same candidate, one use spent.
            var store = new FaultStore(); var faulty = Edit(s, x => { }, store); var retry = Command(faulty, CommandKind.UnlockDie); retry.targetDieId = "D002";
            var pending = faulty.Execute(retry); Assert.That(pending.pendingSave, Is.True); Assert.That(Key(faulty.State).usesRemaining, Is.EqualTo(2), "not committed yet");
            Assert.That(faulty.Execute(retry).pendingSave, Is.True, "the same request while the save is pending does not run again");
            store.fail = false; var saved = faulty.RetrySave(); Assert.That(saved.accepted, Is.True); Assert.That(saved.receipt.stateHash, Is.EqualTo(pending.receipt.stateHash));
            Assert.That(Key(faulty.State).usesRemaining, Is.EqualTo(1)); Assert.That(faulty.State.hand.orderedLockedIds, Is.Empty);
        }

        [Test] public void V111_AnUnlockedDieCanBeRolledAgainAndGlassIsNotJudged()
        {
            var s = Hand(Start(A("C154")), A("D001", "D002"), 2, null, null, x => x.dice.First(d => d.dieId == "D001").isGlass = true);
            var unlocked = Unlock(s, "D001"); Assert.That(unlocked.receipt.effects.Any(e => e.kind == "DieBroken"), Is.False); Assert.That(s.State.dice.Count, Is.EqualTo(5));
            var roll = Do(s, CommandKind.Roll);
            Assert.That(roll.receipt.randomResults.Where(t => t.kind == "DieFace").Select(t => t.sourceId), Is.EqualTo(A("D001", "D003", "D004", "D005")), "the unlocked die takes part in the roll at its creation-order place");
            Assert.That(s.State.hand.rollsRemaining, Is.EqualTo(1));
        }

        // ---- 8. save schema 8, validation and debug scenes (section 12.1) ----

        [Test] public void V111_ANewRunIsSchemaEightAndOlderSavesAreRefused()
        {
            var s = New(); Assert.That(s.State.saveSchemaVersion, Is.EqualTo(8)); Assert.That(s.State.configVersion, Is.EqualTo("p1.1.1")); Assert.That(cfg.saveSchemaVersion, Is.EqualTo(8));
            AssertRejected(s, x => x.saveSchemaVersion = 7, "the p1.1.0 structure"); AssertRejected(s, x => x.saveSchemaVersion = 9, "a newer structure");
            AssertRejected(s, x => x.configVersion = "p1.1.0", "the p1.1.0 rules"); AssertRejected(s, x => { x.saveSchemaVersion = 7; x.configVersion = "p1.1.0"; }, "a p1.1.0 save");
            Assert.That(() => new GameSession(cfg, new MemorySaveStore(), s.State), Throws.Nothing);
        }

        [Test] public void V111_AHandWithAUsedFreeUnlockAndAPartlyUsedRingSurvivesAReload()
        {
            // An ordinary run: C154 and a key ring. Unlock twice (free, then the ring) and reload.
            var s = Ordinary(x => AddItem(x, "I13"), "C154"); Do(s, CommandKind.Roll);
            var ids = s.State.hand.currentResults.Select(r => r.dieId).Take(3).ToArray(); Lock(s, ids);
            Unlock(s, ids[1]); var command = Command(s, CommandKind.UnlockDie); command.targetDieId = ids[0]; Assert.That(s.Execute(command).accepted, Is.True);
            var saved = s.State; Assert.That(saved.isDebug, Is.False); Assert.That(saved.hand.unlockCardUsed, Is.True); Assert.That(Key(saved).usesRemaining, Is.EqualTo(2)); Assert.That(saved.hand.orderedLockedIds, Is.EqualTo(A(ids[2])));
            var reloaded = new GameSession(cfg, new MemorySaveStore(), saved); var again = reloaded.State;
            Assert.That(GameSession.StateHash(again), Is.EqualTo(GameSession.StateHash(saved))); Assert.That(again.hand.unlockCardUsed, Is.True); Assert.That(Key(again).usesRemaining, Is.EqualTo(2)); Assert.That(again.hand.orderedLockedIds, Is.EqualTo(saved.hand.orderedLockedIds));
            Assert.That(reloaded.Execute(command).duplicate, Is.True, "the same request after the reload changes nothing"); Assert.That(GameSession.StateHash(reloaded.State), Is.EqualTo(GameSession.StateHash(saved)));
            Assert.That(saved.processedReceipts.Last().command.kind, Is.EqualTo(CommandKind.UnlockDie)); Assert.That(saved.processedReceipts.Last().command.targetDieId, Is.EqualTo(ids[0]));
        }

        [Test] public void V111_AShopWithAUsedSelfSelectedUpgradeSurvivesAReload()
        {
            var s = Edit(Shop(), x => { x.gold = 100; AddItem(x, "I13"); Key(x).usesRemaining = 2; x.isDebug = false; }); Assert.That(s.State.isDebug, Is.False);
            var command = Command(s, CommandKind.CustomDieUpgrade); command.upgradeId = "U06"; command.targetDieId = "D001"; command.targetFaceIds = A("F02", "F05"); Assert.That(s.Execute(command).accepted, Is.True);
            var saved = s.State; Assert.That(saved.shop.selfDieUpgradeUsed, Is.EqualTo(1));
            var reloaded = new GameSession(cfg, new MemorySaveStore(), saved);
            Assert.That(GameSession.StateHash(reloaded.State), Is.EqualTo(GameSession.StateHash(saved))); Assert.That(reloaded.State.shop.selfDieUpgradeUsed, Is.EqualTo(1)); Assert.That(Key(reloaded.State).usesRemaining, Is.EqualTo(2));
            var duplicate = reloaded.Execute(command); Assert.That(duplicate.duplicate, Is.True); Assert.That(reloaded.State.shop.selfDieUpgradeUsed, Is.EqualTo(1)); Assert.That(reloaded.State.gold, Is.EqualTo(saved.gold));
            var recorded = saved.processedReceipts.Last().command; Assert.That(recorded.kind, Is.EqualTo(CommandKind.CustomDieUpgrade)); Assert.That(recorded.upgradeId, Is.EqualTo("U06")); Assert.That(recorded.targetFaceIds, Is.EqualTo(A("F02", "F05")));
            // The hash covers the new fields.
            var changed = s.State; changed.shop.selfDieUpgradeUsed = 2; Assert.That(GameSession.StateHash(changed), Is.Not.EqualTo(GameSession.StateHash(saved)));
            changed = s.State; Key(changed).usesRemaining = 3; Assert.That(GameSession.StateHash(changed), Is.Not.EqualTo(GameSession.StateHash(saved)));
        }

        [Test] public void V111_ValidationRejectsNewShopAndItemStateThatIsOutOfRange()
        {
            var hand = Ordinary(x => { AddItem(x, "I13"); AddItem(x, "I06"); }, "C13"); Do(hand, CommandKind.Roll);
            AssertRejected(hand, x => Key(x).usesRemaining = 0, "a held ring has at least one use"); AssertRejected(hand, x => Key(x).usesRemaining = 4, "at most three");
            AssertRejected(hand, x => x.inventory.Single(i => i.contentId == "I06").usesRemaining = 2, "any other item has exactly one"); AssertRejected(hand, x => x.inventory.Single(i => i.contentId == "I06").usesRemaining = 0, "zero");
            AssertRejected(hand, x => x.hand.unlockCardUsed = true, "no C154 in force");
            foreach (int ok in new[] { 1, 2, 3 }) { var x = hand.State; Key(x).usesRemaining = ok; Assert.That(() => new GameSession(cfg, new MemorySaveStore(), x), Throws.Nothing); }
            var withCard = Ordinary(null, "C154"); AssertRejected(withCard, x => x.hand.unlockCardUsed = true, "before the first roll of the hand");
            Do(withCard, CommandKind.Roll); var used = withCard.State; used.hand.unlockCardUsed = true; Assert.That(() => new GameSession(cfg, new MemorySaveStore(), used), Throws.Nothing);
            var hashA = withCard.State; var hashB = withCard.State; hashB.hand.unlockCardUsed = true; Assert.That(GameSession.StateHash(hashA), Is.Not.EqualTo(GameSession.StateHash(hashB)));

            string growth = null, plain = null; var shop = Edit(Shop(), x => { growth = PutOffer(x, "controller", "C56"); plain = PutOffer(x, "controller", "C13"); x.isDebug = false; });
            Assert.That(shop.State.isDebug, Is.False);
            AssertRejected(shop, x => x.shop.selfDieUpgradeUsed = 3, "above the limit"); AssertRejected(shop, x => x.shop.selfDieUpgradeUsed = -1, "negative");
            AssertRejected(shop, x => x.shop.offers.Single(o => o.offerId == growth).controller.state.growthCatchup = 50, "cycle 1 offers carry no catch-up");
            AssertRejected(shop, x => x.shop.offers.Single(o => o.offerId == plain).controller.state.growthCatchup = 50, "not a growth card");
            AssertRejected(shop, x => x.shop.offers.Single(o => o.offerId == growth).controller.state.growth = 40, "an offer has not grown");
            // A debug workspace may hold other catch-up values on a growth card offer, never a negative one.
            var debug = Edit(Shop(), x => growth = PutOffer(x, "controller", "C56"));
            var free = debug.State; free.shop.offers.Single(o => o.offerId == growth).controller.state.growthCatchup = 250; Assert.That(() => new GameSession(cfg, new MemorySaveStore(), free), Throws.Nothing);
            AssertRejected(debug, x => x.shop.offers.Single(o => o.offerId == growth).controller.state.growthCatchup = -50, "negative");
        }

        [Test] public void V111_ADebugSceneCarriesKeyRingUsesAndTheFreeUnlockFlag()
        {
            var s = Hand(Start(A("C154"), x => AddItem(x, "I13")), A("D001", "D002"));
            Unlock(s, "D001"); Unlock(s, "D002"); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(2));
            var scene = s.ExportDebugScene(); Assert.That(scene.unlockCardUsed, Is.True); Assert.That(scene.inventory.Single().usesRemaining, Is.EqualTo(2));
            Func<DebugScene, CommandResult> apply = v => s.Execute(new SessionCommand { commandId = Guid.NewGuid().ToString("N"), expectedActionSequence = s.State.actionSequence, kind = CommandKind.ApplyDebugScene, debugScene = v });
            Assert.That(apply(scene).accepted, Is.True); Assert.That(s.State.hand.unlockCardUsed, Is.True); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(2));
            var again = s.ExportDebugScene(); Assert.That(again.unlockCardUsed, Is.True); Assert.That(again.inventory.Single().usesRemaining, Is.EqualTo(2));
            // An item given without a use count is as created; an explicit count must be within the item's range.
            scene = s.ExportDebugScene(); scene.inventory.Single().usesRemaining = 0; scene.unlockCardUsed = false; Assert.That(apply(scene).accepted, Is.True);
            Assert.That(Key(s.State).usesRemaining, Is.EqualTo(3)); Assert.That(s.State.hand.unlockCardUsed, Is.False);
            scene = s.ExportDebugScene(); scene.inventory.Single().usesRemaining = 1; Assert.That(apply(scene).accepted, Is.True); Assert.That(Key(s.State).usesRemaining, Is.EqualTo(1));
            scene = s.ExportDebugScene(); scene.inventory.Single().usesRemaining = 4; Assert.That(apply(scene).accepted, Is.False, "more than a key ring holds");
            // The shop a scene leads to follows its stage: a scene at stage 13 opens a cycle-5 shop with the full allowance.
            scene = s.ExportDebugScene(); scene.stageIndex = 12; scene.target = "10"; scene.score = "10"; scene.modifierId = ""; Assert.That(apply(scene).accepted, Is.True);
            Do(s, CommandKind.EndStage); Do(s, CommandKind.Continue);
            Assert.That(s.ShopCycleIn(s.State), Is.EqualTo(5)); Assert.That(s.CustomDieUpgradesRemaining, Is.EqualTo(2));
            foreach (var o in s.State.shop.offers.Where(v => v.category == "controller"))
                Assert.That(o.controller.state.growthCatchup, Is.EqualTo(ControllerRules.IsGrowthAdditive(o.contentId) ? 200 : ControllerRules.IsGrowthBase(o.contentId) ? 24 : ControllerRules.IsGrowthMultiplier(o.contentId) ? 40 : 0), o.contentId);
        }
    }
}
