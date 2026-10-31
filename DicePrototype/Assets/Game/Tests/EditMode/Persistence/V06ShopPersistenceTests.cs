using System;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;

namespace DiceCraft.Tests
{
    public sealed class V06ShopPersistenceTests : SessionTestSupport
    {
        [TestCase(false)] [TestCase(true)]
        public void SAVE_V06_RefreshShopRealJsonRoundTripPreservesQuoteHashRngAndReopen(bool freeFirst)
        {
            string folder = Path.Combine(Application.temporaryCachePath, "V06ShopRoundTrip_" + Guid.NewGuid().ToString("N"));
            string path = Path.Combine(folder, "debug-run.json");
            var store = new AtomicSaveStore(path, cfg, SaveSlotPolicy.Debug);
            var session = GameSession.CreateDebugWorkspace(cfg, store);
            var scene = session.ExportDebugScene(); scene.gold = 1000; scene.score = scene.target; scene.freeFirstRefreshOwned = freeFirst;
            Do(session, CommandKind.ApplyDebugScene, c => c.debugScene = scene);
            Do(session, CommandKind.EndStage); Do(session, CommandKind.Continue);
            Assert.That(session.State.phase, Is.EqualTo("Shop"));
            // Same real JsonUtility/AtomicSaveStore path used by a standalone Player.
            CheckLoaded(session.State, new AtomicSaveStore(path, cfg, SaveSlotPolicy.Debug));
            for (int iteration = 0; iteration < 3; iteration++)
            {
                var before = session.State;
                var command = Command(session, CommandKind.RefreshControllers);
                long expectedCost = iteration == 0 && freeFirst ? 0 : 3 + 2 * iteration;
                Assert.That(session.Execute(command).accepted, Is.True);
                var after = session.State;
                Assert.That(after.gold, Is.EqualTo(before.gold - expectedCost));
                Assert.That(after.diceRngState, Is.EqualTo(before.diceRngState));
                Assert.That(after.effectRngState, Is.EqualTo(before.effectRngState));
                Assert.That(after.shopRngState, Is.Not.EqualTo(before.shopRngState));
                Assert.That(after.shop.controllerRefreshCount, Is.EqualTo(iteration + 1));
                var freshStore = new AtomicSaveStore(path, cfg, SaveSlotPolicy.Debug);
                var restored = CheckLoaded(after, freshStore);
                byte[] committedBytes = File.ReadAllBytes(path);
                session = new GameSession(cfg, freshStore, restored);
                Assert.That(session.Execute(command).duplicate, Is.True);
                CollectionAssert.AreEqual(committedBytes, File.ReadAllBytes(path), "Duplicate after reopen must not rewrite or redraw.");
                Assert.That(GameSession.StateHash(session.State), Is.EqualTo(GameSession.StateHash(after)));
            }
            TestContext.WriteLine("Real JsonUtility shop evidence retained: " + folder);
        }

        private RunState CheckLoaded(RunState expected, AtomicSaveStore store)
        {
            var loaded = store.Load(); Assert.That(loaded, Is.Not.Null, store.LastLoadNotice);
            Assert.That(store.LastLoadNotice, Is.Null);
            Assert.That(JsonUtility.ToJson(loaded.shop), Is.EqualTo(JsonUtility.ToJson(expected.shop)), "All quote/payload/order/sold/price/batch fields preserved.");
            Assert.That(loaded.gold, Is.EqualTo(expected.gold)); Assert.That(loaded.actionSequence, Is.EqualTo(expected.actionSequence));
            Assert.That(loaded.shopRngState, Is.EqualTo(expected.shopRngState));
            Assert.That(loaded.diceRngState, Is.EqualTo(expected.diceRngState)); Assert.That(loaded.effectRngState, Is.EqualTo(expected.effectRngState));
            Assert.That(GameSession.StateHash(loaded), Is.EqualTo(GameSession.StateHash(expected)));
            Assert.That(GameSession.StateHash(loaded), Is.EqualTo(loaded.processedReceipts.Last().stateHash));
            Assert.That(JsonUtility.ToJson(loaded.processedReceipts.Last()), Is.EqualTo(JsonUtility.ToJson(expected.processedReceipts.Last())));
            return loaded;
        }
    }
}
