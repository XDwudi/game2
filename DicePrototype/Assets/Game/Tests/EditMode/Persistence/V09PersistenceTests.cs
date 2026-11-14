using System;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Tests
{
    /// <summary>
    /// p0.9.0 state through the real JsonUtility / AtomicSaveStore path a standalone player uses: growth, sale bonus,
    /// bought slots, the frozen pool dice and retrigger records of a submission, and the stage income fields.
    /// </summary>
    public sealed class V09PersistenceTests : SessionTestSupport
    {
        private static ControllerInstance Card(string id, int serial) => new ControllerInstance { definitionId = id, enchantmentId = "none", acquiredSerial = serial, instanceId = "ITEM_" + serial.ToString("D5") };

        [Test]
        public void SAVE_V09_GrowthPoolRetriggerSlotsAndStageIncomeSurviveARealJsonRoundTrip()
        {
            string folder = Path.Combine(Application.temporaryCachePath, "V09RoundTrip_" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "debug-run.json");
            var store = new AtomicSaveStore(path, cfg, SaveSlotPolicy.Debug);
            var session = GameSession.CreateDebugWorkspace(cfg, store);
            var scene = session.ExportDebugScene(); scene.gold = 40; scene.target = "1"; scene.controllerSlotPurchases = 2; scene.capacityOverride = 5;
            scene.controllers = new[] { "C55", "C64", "C70", "C99", "C95", "C96", "C58" }.Select((id, i) => Card(id, i + 1)).ToList();
            scene.controllers[0].state.growth = 6;
            scene.dice.Add(DiceFactory.CreateStandard(cfg, 6)); scene.dice.Add(DiceFactory.CreateStandard(cfg, 7));
            Do(session, CommandKind.ApplyDebugScene, c => c.debugScene = scene);
            Assert.That(session.State.controllerSlots, Is.EqualTo(7)); Assert.That(session.State.controllerSlotPurchases, Is.EqualTo(2));
            Do(session, CommandKind.Roll); Do(session, CommandKind.Roll);
            session = Reopen(session, path); Lock(session, "D001", "D002");
            session = Reopen(session, path);
            Assert.That(session.State.hand.rollsUsed, Is.EqualTo(2));
            Do(session, CommandKind.Submit);
            var live = session.State.lastSubmission;
            Assert.That(live.snapshot.poolDice.Length, Is.EqualTo(5)); Assert.That(live.snapshot.rollsUsed, Is.EqualTo(2)); Assert.That(live.snapshot.permanentDiceCount, Is.EqualTo(7));
            Assert.That(live.breakdown.retriggerEntries.Length, Is.EqualTo(1)); Assert.That(live.breakdown.pipRetriggers, Is.EqualTo(new[] { 1, 0 }));
            Assert.That(live.breakdown.controllerBaseAdds.Select(a => a.abilityId + ":" + a.amount), Is.EqualTo(new[] { "C55:6", "C70:30" }));
            Assert.That(live.breakdown.afterScorePlan.growthChanges.Single().delta, Is.EqualTo(2));
            session = Reopen(session, path);
            var restored = session.State.lastSubmission;
            Assert.That(JsonUtility.ToJson(restored), Is.EqualTo(JsonUtility.ToJson(live)), "The recorded submission, including pool dice and retriggers, is preserved.");
            Assert.That(session.State.controllers.Single(c => c.definitionId == "C55").state.growth, Is.EqualTo(8));

            Do(session, CommandKind.EndStage);
            session = Reopen(session, path);
            var r = session.State.lastStageSettlement;
            Assert.That(r.controllerIncome, Is.EqualTo(3)); Assert.That(r.interestPrincipalCap, Is.EqualTo(50)); Assert.That(r.interest, Is.EqualTo(8));
            Assert.That(session.State.controllers.All(c => c.state.saleBonus == 1), Is.True);
            Assert.That(session.State.controllers.Single(c => c.definitionId == "C58").state.growth, Is.EqualTo(4));
            Do(session, CommandKind.Continue);
            session = Reopen(session, path);
            Assert.That(session.State.phase, Is.EqualTo("Shop")); Assert.That(session.State.controllerSlots, Is.EqualTo(7));
            Assert.That(session.State.shop.offers.Count(o => o.category == "controller"), Is.EqualTo(4));
            Assert.That(session.State.shop.offers.Where(o => o.category == "controller").All(o => o.controller.state.growth == 0 && o.controller.state.saleBonus == 0), Is.True);
            TestContext.WriteLine("Real JsonUtility v0.9 evidence retained: " + folder);
        }

        /// <summary>Loads the file a fresh store reads, checks it hashes like the live state, and continues from it.</summary>
        private GameSession Reopen(GameSession live, string path)
        {
            var expected = live.State; var store = new AtomicSaveStore(path, cfg, SaveSlotPolicy.Debug);
            var loaded = store.Load(); Assert.That(loaded, Is.Not.Null, store.LastLoadNotice); Assert.That(store.LastLoadNotice, Is.Null);
            Assert.That(GameSession.StateHash(loaded), Is.EqualTo(GameSession.StateHash(expected)));
            Assert.That(GameSession.StateHash(loaded), Is.EqualTo(loaded.processedReceipts.Last().stateHash));
            Assert.That(loaded.controllers.Count, Is.EqualTo(expected.controllers.Count));
            for (int i = 0; i < expected.controllers.Count; i++) Assert.That(JsonUtility.ToJson(loaded.controllers[i]), Is.EqualTo(JsonUtility.ToJson(expected.controllers[i])));
            return new GameSession(cfg, store, loaded);
        }
    }
}
