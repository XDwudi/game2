using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using DiceCraft.Core;

namespace DiceCraft.Tests
{
    public abstract class SessionTestSupport
    {
        protected GameConfig cfg;
        private int commandCounter;
        [SetUp] public void ReadConfig()
        {
#if UNITY_5_3_OR_NEWER
            string path = Path.Combine(UnityEngine.Application.dataPath, "Game/Data/P0Config.json");
#else
            string path = Path.Combine(Directory.GetCurrentDirectory(), "DicePrototype/Assets/Game/Data/P0Config.json");
#endif
            string json = File.ReadAllText(path);
            GameConfig.ValidateJson(json);
            json = GameConfig.PrepareJsonForUnity(json);
#if UNITY_5_3_OR_NEWER
            cfg = UnityEngine.JsonUtility.FromJson<GameConfig>(json);
#else
            cfg = System.Text.Json.JsonSerializer.Deserialize<GameConfig>(json, new System.Text.Json.JsonSerializerOptions { IncludeFields = true });
#endif
            cfg.Validate(); commandCounter = 0;
        }

        protected SessionCommand Command(GameSession s, CommandKind kind)
        {
            return new SessionCommand { commandId = "test_" + (++commandCounter), expectedActionSequence = s.State == null ? 0 : s.State.actionSequence, kind = kind };
        }
        protected CommandResult Do(GameSession s, CommandKind kind, Action<SessionCommand> configure = null)
        {
            var c = Command(s, kind); configure?.Invoke(c); var result = s.Execute(c);
            Assert.That(result.accepted, Is.True, kind + ": " + result.error); return result;
        }
        protected GameSession New(uint seed = 1, ISaveStore store = null)
        {
            var s = new GameSession(cfg, store ?? new MemorySaveStore()); Do(s, CommandKind.NewRun, c => c.seed = seed); return s;
        }
        protected GameSession Edit(GameSession s, Action<RunState> edit, ISaveStore store = null)
        {
            var snapshot = s.State; snapshot.isDebug = true; snapshot.debugWorkspace = true; edit(snapshot); snapshot.debugWorkspace = snapshot.isDebug; if (!snapshot.isDebug) snapshot.debugCapacityOverride = 0;
            if (snapshot.hand != null) foreach(var result in snapshot.hand.currentResults.Where(r => r.hasRolled)) {
                var die = snapshot.dice.FirstOrDefault(d => d.dieId == result.dieId);
                var face = die == null ? null : die.faces.FirstOrDefault(f => f.faceId == result.currentFaceId);
                if(face != null) result.currentColor = face.baseColor;
            } return new GameSession(cfg, store ?? new MemorySaveStore(), snapshot);
        }
        protected void Equip(RunState s, params string[] ids)
        {
            s.controllers.Clear();
            foreach (var id in ids) { int serial = s.nextItemSerial++; s.controllers.Add(new ControllerInstance { definitionId = id, enchantmentId = "none", acquiredSerial = serial, instanceId = "ITEM_" + serial.ToString("D5") }); }
            foreach(var c in s.controllers.Where(c => c.definitionId == "C45")) c.state.activeColor = "red";
            if (ids.Length > s.controllerSlots) s.controllerSlots = ids.Length;
        }
        protected string AddItem(RunState s, string id)
        {
            var item = new OwnedItem { contentId = id, instanceId = "ITEM_" + (s.nextItemSerial++).ToString("D5") };
            s.inventory.Add(item); s.consumableSlots = Math.Max(s.consumableSlots, s.inventory.Count); return item.instanceId;
        }
        protected GameSession Ready(params string[] controllers)
        {
            var s = Edit(New(), x => Equip(x, controllers)); Do(s, CommandKind.StartStage); return s;
        }
        protected GameSession Selecting(int[] pips, string[] colors = null, string[] controllers = null, int lockedCount = -1)
        {
            var s = Ready(controllers ?? Array.Empty<string>());
            return Edit(s, x => {
                x.phase = "Selecting"; x.hand.hasRolled = true; x.hand.rollsUsed = 1; x.hand.rollsRemaining = 0;
                x.dice.Clear(); x.hand.currentResults.Clear(); x.hand.orderedLockedIds.Clear();
                x.baseSubmitCapacity = cfg.initialSubmitCapacity; x.stage.capacity = Math.Max(pips.Length, ControllerRules.Capacity(x.controllers, cfg)); x.debugCapacityOverride = x.stage.capacity;
                int n = lockedCount < 0 ? pips.Length : lockedCount;
                for (int i = 0; i < pips.Length; i++) {
                    var d = DiceFactory.CreateStandard(cfg, i + 1); x.dice.Add(d);
                    // A forced current color still comes from its selected physical face.
                    d.faces[0].baseColor=colors==null?"blue":colors[i];
                    x.hand.currentResults.Add(new DieHandState { dieId = d.dieId, currentFaceId = "F01", currentPip = pips[i], currentColor = colors == null ? "blue" : colors[i], hasRolled = true, remoteOverride = true, isLocked = i < n });
                    if (i < n) x.hand.orderedLockedIds.Add(d.dieId);
                }
                x.nextDieSerial = pips.Length + 1;
            });
        }
        protected GameSession Shop()
        {
            var s = Selecting(new[] { 4,4,4,4,4 }, new[] { "red","red","red","red","red" });
            Do(s, CommandKind.Submit); Do(s, CommandKind.EndStage); Do(s, CommandKind.Continue); return Edit(s, x => x.debugCapacityOverride = 0);
        }
        protected string PutOffer(RunState s, string category, string contentId, int basePrice = 0, DieInstance die = null)
        {
            var id = "OFFER_" + (s.nextOfferSerial++).ToString("D5");
            s.shop.offers.RemoveAll(o => o.category == category && o.contentId == contentId);
            int cap = category == "controller" ? cfg.shop.initialControllerOffers + s.controllerOfferPurchases : category == "consumable" ? cfg.shop.initialConsumableOffers + s.consumableOfferPurchases : int.MaxValue;
            if (s.shop.offers.Count(o => o.category == category) >= cap) s.shop.offers.Remove(s.shop.offers.First(o => o.category == category));
            var offer = new ShopOffer { offerId = id, batchId = s.shop.generationId + "/" + category + (category == "controller" ? "/" + s.shop.controllerGeneration : ""), category = category, contentId = contentId, basePrice = basePrice, die = die };
            if (category == "controller") {
                s.shop.offers.RemoveAll(o => o.category == category && o.contentId == contentId);
                int serial = s.nextItemSerial++;
                offer.basePrice = cfg.Controller(contentId).buyPrice;
                offer.controller = new ControllerInstance { instanceId = "ITEM_" + serial.ToString("D5"), acquiredSerial = serial, definitionId = contentId, enchantmentId = "none" };
            }
            s.shop.offers.Add(offer); return id;
        }
        protected void Lock(GameSession s, params string[] ids)
        {
            foreach (string id in ids) Assert.That(s.TogglePending(id), Is.True);
            Do(s, CommandKind.ConfirmLocks);
        }
        protected void AssertUnchanged(RunState before, RunState after)
        {
            Assert.That(after.actionSequence, Is.EqualTo(before.actionSequence)); Assert.That(after.gold, Is.EqualTo(before.gold));
            Assert.That(after.diceRngState, Is.EqualTo(before.diceRngState)); Assert.That(after.shopRngState, Is.EqualTo(before.shopRngState)); Assert.That(after.effectRngState, Is.EqualTo(before.effectRngState));
            Assert.That(after.dice.Count, Is.EqualTo(before.dice.Count)); Assert.That(after.inventory.Count, Is.EqualTo(before.inventory.Count));
            if (before.hand != null) { Assert.That(after.hand.rollsRemaining, Is.EqualTo(before.hand.rollsRemaining)); Assert.That(after.hand.orderedLockedIds, Is.EqualTo(before.hand.orderedLockedIds)); }
        }
    }
}
