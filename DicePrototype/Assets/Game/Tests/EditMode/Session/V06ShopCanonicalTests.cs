using System;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;

namespace DiceCraft.Tests
{
    public sealed class V06ShopCanonicalTests : SessionTestSupport
    {
        [Test] public void V06_ShopOnlyEmptyUnusedPayloadsCanonicalizeToNull()
        {
            var session = Shop(); var original = session.State; var hydrated = original.Clone();
            foreach (var offer in hydrated.shop.offers)
            {
                if (offer.category != "controller") offer.controller = new ControllerInstance();
                if (offer.category != "new_die") offer.die = new DieInstance { faces = Array.Empty<FaceState>() };
            }
            session.ValidateState(hydrated);
            Assert.That(GameSession.StateHash(hydrated), Is.EqualTo(GameSession.StateHash(original)));
            Assert.That(hydrated.shop.offers.First(o => o.category != "controller").controller, Is.Not.Null, "Hashing is pure; no state mutation.");
            var a = new GameSession(cfg, new MemorySaveStore(), original);
            var b = new GameSession(cfg, new MemorySaveStore(), hydrated);
            var command = Command(a, CommandKind.RefreshControllers);
            Assert.That(a.Execute(command).accepted, Is.True); Assert.That(b.Execute(command).accepted, Is.True);
            Assert.That(GameSession.StateHash(b.State), Is.EqualTo(GameSession.StateHash(a.State)));
            Assert.That(b.State.shopRngState, Is.EqualTo(a.State.shopRngState));
        }

        [TestCase("controller-instance")] [TestCase("controller-definition")] [TestCase("controller-enchantment")]
        [TestCase("controller-serial")] [TestCase("controller-dead")] [TestCase("controller-state-null")]
        [TestCase("controller-color")] [TestCase("controller-turnover")] [TestCase("controller-fund")]
        [TestCase("die-id")] [TestCase("die-serial")] [TestCase("die-lifetime")] [TestCase("die-stage")]
        [TestCase("die-faces")] [TestCase("die-null-faces")] [TestCase("die-glass")] [TestCase("die-revision")]
        public void V06_ShopUnusedNonemptyPayloadIsNotErasedAndIsRejected(string defect)
        {
            var session = Shop(); var state = session.State; string cleanHash = GameSession.StateHash(state);
            var offer = state.shop.offers.First(o => o.category != "controller" && o.category != "new_die");
            if (defect.StartsWith("controller-"))
            {
                offer.controller = new ControllerInstance(); var c = offer.controller;
                if (defect == "controller-instance") c.instanceId = "ITEM_99999";
                if (defect == "controller-definition") c.definitionId = "C01";
                if (defect == "controller-enchantment") c.enchantmentId = "base10";
                if (defect == "controller-serial") c.acquiredSerial = 1;
                if (defect == "controller-dead") c.alive = false;
                if (defect == "controller-state-null") c.state = null;
                if (defect == "controller-color") c.state.activeColor = "red";
                if (defect == "controller-turnover") c.state.turnoverUnits2 = 4;
                if (defect == "controller-fund") c.state.fundAccrual = 3;
            }
            else
            {
                offer.die = new DieInstance { faces = Array.Empty<FaceState>() }; var d = offer.die;
                if (defect == "die-id") d.dieId = "D001";
                if (defect == "die-serial") d.createdSerial = 1;
                if (defect == "die-lifetime") d.lifetime = "permanent";
                if (defect == "die-stage") d.createdStageId = state.stage.stageId;
                if (defect == "die-faces") d.faces = DiceFactory.CreateStandard(cfg, 1).faces;
                if (defect == "die-null-faces") d.faces = null;
                if (defect == "die-glass") d.isGlass = true;
                if (defect == "die-revision") d.dataRevision = 1;
            }
            Assert.That(GameSession.StateHash(state), Is.Not.EqualTo(cleanHash));
            Assert.Catch<Exception>(() => session.ValidateState(state));
        }

        [Test] public void V06_ShopActivePayloadEvenIfEmptyIsNeverCanonicalizedAway()
        {
            var state = Shop().State; var offer = state.shop.offers.First(o => o.category == "controller");
            offer.controller = null; string missing = GameSession.StateHash(state);
            offer.controller = new ControllerInstance(); Assert.That(GameSession.StateHash(state), Is.Not.EqualTo(missing));
            offer.category = "new_die"; offer.controller = null; offer.die = null; missing = GameSession.StateHash(state);
            offer.die = new DieInstance { faces = Array.Empty<FaceState>() };
            Assert.That(GameSession.StateHash(state), Is.Not.EqualTo(missing));
        }
    }
}
