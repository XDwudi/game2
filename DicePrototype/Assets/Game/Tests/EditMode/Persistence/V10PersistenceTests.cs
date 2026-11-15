using System;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Tests
{
    /// <summary>
    /// p1.0.0 state through the real JsonUtility / AtomicSaveStore path a standalone player uses: the cycle plan, the
    /// boss modifier and its disabled controllers, the frozen stage context and face changes of a submission, face
    /// weights, the settlement's boss fields, and the gift groups of a shop.
    /// </summary>
    public sealed class V10PersistenceTests : SessionTestSupport
    {
        private static ControllerInstance Card(string id, int serial) => new ControllerInstance { definitionId = id, enchantmentId = "none", acquiredSerial = serial, instanceId = "ITEM_" + serial.ToString("D5") };

        [Test]
        public void SAVE_V10_CyclePlanBossStageFaceChangesAndGiftGroupsSurviveARealJsonRoundTrip()
        {
            string folder = Path.Combine(Application.temporaryCachePath, "V10RoundTrip_" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "debug-run.json");
            var store = new AtomicSaveStore(path, cfg, SaveSlotPolicy.Debug);
            var session = GameSession.CreateDebugWorkspace(cfg, store);
            string[] plan = session.State.cycles.Select(c => c.modifierId + "/" + c.lootKind).ToArray();
            var scene = session.ExportDebugScene(); scene.gold = 12; scene.target = "1"; scene.capacityOverride = 5; scene.stageIndex = 2; scene.modifierId = "B25";
            scene.controllers = new[] { "C50", "C46", "C117", "C124", "C111", "C133" }.Select((id, i) => Card(id, i + 1)).ToList(); scene.controllerSlotPurchases = 1;
            Do(session, CommandKind.ApplyDebugScene, c => c.debugScene = scene);
            Assert.That(session.State.stage.modifierId, Is.EqualTo("B25")); Assert.That(session.State.stage.disabledControllerIds.Length, Is.EqualTo(2));
            session = Reopen(session, path);
            Assert.That(session.State.cycles.Select(c => c.modifierId + "/" + c.lootKind), Is.EqualTo(plan), "The cycle plan is part of the save.");
            Assert.That(session.CurrentModifierId, Is.EqualTo("B25"));
            Do(session, CommandKind.Roll); Lock(session, "D001", "D002");
            session = Reopen(session, path);
            Do(session, CommandKind.Submit);
            var live = session.State.lastSubmission;
            Assert.That(live.snapshot.modifierId, Is.EqualTo("B25")); Assert.That(live.snapshot.stageKind, Is.EqualTo("boss")); Assert.That(live.snapshot.disabledControllerIds.Length, Is.EqualTo(2));
            Assert.That(live.snapshot.ordinaryControllerSlots, Is.EqualTo(6));
            Assert.That(live.breakdown.triggeredSourceIds.Length, Is.EqualTo(2), "C50 and C46 are disabled; C117 and C124 trigger.");
            Assert.That(live.breakdown.afterScorePlan.faceChanges.Select(f => f.sourceId + ":" + f.kind), Is.EqualTo(new[] { "C117:gold", "C124:weightDelta", "C124:weightDelta" }));
            session = Reopen(session, path);
            Assert.That(JsonUtility.ToJson(session.State.lastSubmission), Is.EqualTo(JsonUtility.ToJson(live)), "The recorded submission, including the boss context and face changes, is preserved.");
            var second = session.State.dice.Single(d => d.dieId == "D002"); var up = second.faces.Single(f => f.faceId == live.snapshot.dice[1].faceId);
            Assert.That(up.isGold, Is.True); Assert.That(up.baseWeight, Is.EqualTo(2));
            Assert.That(session.State.stage.primaryHandsUsed.Count, Is.LessThanOrEqualTo(1));

            Do(session, CommandKind.EndStage);
            session = Reopen(session, path);
            var r = session.State.lastStageSettlement;
            Assert.That(r.stageKind, Is.EqualTo("boss")); Assert.That(r.lootKind, Is.EqualTo(session.State.cycles[0].lootKind)); Assert.That(r.bountyIncome, Is.EqualTo(5)); Assert.That(r.controllerIncome, Is.EqualTo(5));
            Do(session, CommandKind.Continue);
            session = Reopen(session, path);
            var groups = session.ShopGroups();
            Assert.That(groups.Select(g => g.groupId), Is.EqualTo(new[] { "LOOT", "GIFT_C111" }));
            Assert.That(session.State.shop.offers.Count(o => o.groupId == "GIFT_C111"), Is.EqualTo(3));
            string cash = session.State.shop.offers.Single(o => o.groupId == "LOOT" && o.category == "gold").offerId; long gold = session.State.gold;
            Do(session, CommandKind.Buy, c => c.offerId = cash);
            session = Reopen(session, path);
            Assert.That(session.State.gold, Is.EqualTo(gold + cfg.lootGoldValue)); Assert.That(session.ShopGroups().First().closed, Is.True);
            Do(session, CommandKind.LeaveShop);
            session = Reopen(session, path);
            Assert.That(session.State.stage.stageKind, Is.EqualTo("normal")); Assert.That(session.CycleView().Select(v => v.stageIndex), Is.EqualTo(new[] { 3, 4, 5 }));
            TestContext.WriteLine("Real JsonUtility v1.0 evidence retained: " + folder);
        }

        /// <summary>Loads the file a fresh store reads, checks it hashes like the live state, and continues from it.</summary>
        private GameSession Reopen(GameSession live, string path)
        {
            var expected = live.State; var store = new AtomicSaveStore(path, cfg, SaveSlotPolicy.Debug);
            var loaded = store.Load(); Assert.That(loaded, Is.Not.Null, store.LastLoadNotice); Assert.That(store.LastLoadNotice, Is.Null);
            Assert.That(GameSession.StateHash(loaded), Is.EqualTo(GameSession.StateHash(expected)));
            Assert.That(GameSession.StateHash(loaded), Is.EqualTo(loaded.processedReceipts.Last().stateHash));
            Assert.That(JsonUtility.ToJson(loaded.stage), Is.EqualTo(JsonUtility.ToJson(expected.stage)));
            if (expected.shop != null) Assert.That(JsonUtility.ToJson(loaded.shop), Is.EqualTo(JsonUtility.ToJson(expected.shop)));
            return new GameSession(cfg, store, loaded);
        }
    }
}
