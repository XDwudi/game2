using System;
using System.Collections;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_ShopCapacityDiscountAndStudyUiRefreshAcrossTransactions()
        {
            foreach (bool studyFirst in new[] {false, true})
            {
                var fixture = ShopBoundaryFixture(); fixture.shop.offers.Clear();
                AddBoundaryOffer(fixture, "C09", "controller", "C09");
                AddBoundaryOffer(fixture, "C02", "controller", "C02");
                AddBoundaryOffer(fixture, "I04", "consumable", "I04");
                AddBoundaryOffer(fixture, "O01", "service", "O01");
                AddBoundaryOffer(fixture, "O02", "service", "O02");
                AddBoundaryOffer(fixture, "straight", "hand_upgrade", "straight");
                AddBoundaryOffer(fixture, "pair", "hand_upgrade", "pair");
                AddBoundaryOffer(fixture, "U06", "die_upgrade", "U06");
                yield return LoadBoundaryFixture(fixture); ResumeBoundaryFixture();
                if (!studyFirst)
                {
                    yield return BuyBoundaryOffer("C09");
                    Assert.That(app.Session.EffectiveCapacity, Is.EqualTo(4));
                    yield return OpenBoundaryOffer("straight");
                    Assert.That(Button("ConfirmPurchase").interactable, Is.False);
                    Assert.That(FindActive<TextMeshProUGUI>("PriceBody").text, Does.Contain("这个牌型已满级，或骰子/提交栏不够。"));
                    Click("CloseDialog"); Click("Study");
                    Assert.That(HasActive("StudyBuystraight"), Is.False, "Self-study must apply the same reduced capacity immediately.");
                    Click("CloseDialog");
                    string instance = app.Session.State.controllers.Single(c => c.contentId == "C09").instanceId;
                    long beforeSell = app.Session.State.gold;
                    Click("Controller_" + instance); Click("SellController"); Click("ConfirmSale");
                    Assert.That(app.Session.State.gold, Is.EqualTo(beforeSell + Math.Max(1, app.Config.Controller("C09").buyPrice / 2)));
                    Assert.That(app.Session.EffectiveCapacity, Is.EqualTo(5));
                    yield return OpenBoundaryOffer("straight"); Assert.That(Button("ConfirmPurchase").interactable, Is.True); Click("CloseDialog");
                    Click("Study"); Assert.That(HasActive("StudyBuystraight"), Is.True); Click("CloseDialog");
                    Assert.That(app.Session.State.shop.offers.Single(o => o.offerId == "C09").sold, Is.True, "Selling must not replenish the original offer.");
                    var beforeDiscount = app.Session.State;
                    yield return BuyBoundaryOffer("O02");
                    Assert.That(app.Session.State.gold, Is.EqualTo(beforeDiscount.gold - app.Config.Service("O02").baseBuyPrice), "The discount service itself uses the old price.");
                    Assert.That(app.Session.State.itemDiscount, Is.EqualTo(1));
                    AssertBoundaryCardPrice("I04", Math.Max(app.Config.minDiscountedItemPrice, app.Config.Consumable("I04").baseBuyPrice - 1));
                    AssertBoundaryCardPrice("O01", Math.Max(app.Config.minDiscountedItemPrice, app.Config.Service("O01").baseBuyPrice - 1));
                    AssertBoundaryCardPrice("C02", app.Config.Controller("C02").buyPrice);
                    AssertBoundaryCardPrice("U06", app.Config.Upgrade("U06").buyPrice);
                    Assert.That(app.Session.State.shopRngState, Is.EqualTo(beforeDiscount.shopRngState));
                    Assert.That(app.Session.State.effectRngState, Is.EqualTo(beforeDiscount.effectRngState));
                    Assert.That(app.Session.State.shop.offers.Count, Is.EqualTo(beforeDiscount.shop.offers.Count));
                }
                int basePrice = app.Config.Hand("pair").baseUpgradePrice;
                int increment = app.Config.shop.upgradePricePerExistingLevel;
                int premium = app.Config.shop.selfStudyPremium;
                if (studyFirst)
                {
                    Click("Study");
                    Assert.That(Button("StudyBuypair").GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo((basePrice + premium) + " 金币"));
                    long gold = app.Session.State.gold; Click("StudyBuypair");
                    Assert.That(app.Session.State.gold, Is.EqualTo(gold - basePrice - premium));
                    yield return OpenBoundaryOffer("pair");
                    // V1.1 (W14, UI_REWRITES Shop.cs:136): 价格：5 金币（你有 7）.
                    Assert.That(DialogLabel("PriceBody"), Does.StartWith("价格：" + (basePrice + increment) + " 金币"));
                    gold = app.Session.State.gold; Click("ConfirmPurchase");
                    Assert.That(app.Session.State.gold, Is.EqualTo(gold - basePrice - increment));
                }
                else
                {
                    yield return BuyBoundaryOffer("pair");
                    Click("Study");
                    Assert.That(FindActive<TextMeshProUGUI>("StudyNamepair").text, Does.Contain("Lv.1 → 2"));
                    Assert.That(Button("StudyBuypair").GetComponentInChildren<TextMeshProUGUI>().text, Is.EqualTo((basePrice + increment + premium) + " 金币"));
                    long gold = app.Session.State.gold; Click("StudyBuypair");
                    Assert.That(app.Session.State.gold, Is.EqualTo(gold - basePrice - increment - premium));
                }
                Assert.That(app.Session.State.handLevels.Single(h => h.handId == "pair").level, Is.EqualTo(2));
                Assert.That(app.Session.State.shop.selfStudyUsed, Is.EqualTo(1));
                yield return WaitForIdle();
                Assert.That(Button("Study").interactable, Is.True, "p0.8.0 allows two self-studies per shop.");
                Click("Study"); long secondGold = app.Session.State.gold; Click("StudyBuypair");
                Assert.That(app.Session.State.gold, Is.EqualTo(secondGold - basePrice - 2 * increment - premium));
                Assert.That(app.Session.State.handLevels.Single(h => h.handId == "pair").level, Is.EqualTo(3));
                Assert.That(app.Session.State.shop.selfStudyUsed, Is.EqualTo(2));
                yield return WaitForIdle();
                Assert.That(Button("Study").interactable, Is.False);
                Assert.That(JsonUtility.ToJson(new AtomicSaveStore(app.SaveLocation, app.Config).Load()), Is.EqualTo(StateJson()));
            }
        }

        [UnityTest]
        public IEnumerator PLAY_ShopTargetCancellationAndPermanentDiceBoundariesPreserveOffers()
        {
            var fixture = ShopBoundaryFixture(); fixture.shop.offers.Clear();
            foreach (string id in Enumerable.Range(1, 7).Select(i => "U" + i.ToString("D2"))) AddBoundaryOffer(fixture, id, "die_upgrade", id);
            yield return LoadBoundaryFixture(fixture); ResumeBoundaryFixture();
            foreach (string id in Enumerable.Range(1, 7).Select(i => "U" + i.ToString("D2")))
            {
                string before = StateJson(); var priorDie = app.Session.State.dice.Single(d => d.dieId == "D001");
                yield return OpenBoundaryOffer(id);
                // V1.1 (W14, SHOP_LAYOUT 7): the offer opens the face pick on the table (S1); there is nothing to confirm before a
                // die (and a face) is chosen (v0.73: the dice sheet with a disabled confirmation).
                Assert.That(app.FacePickStep14, Is.EqualTo("S1")); Assert.That(HasActive("ConfirmPurchase"), Is.False, "Choosing an offer must not choose its target implicitly.");
                SelectUpgradeTarget(id);
                Assert.That(Button("ConfirmPurchase").interactable, Is.True);
                // v1.1 (W09, TX-02): colours without the English initials R/B.
                // V1.1 (W14, SHOP_LAYOUT 7.2): the change before and after, in the face-pick panel; cancelling is its 取消.
                if (id == "U01") Assert.That(FindActive<TextMeshProUGUI>("PurchasePreviewBody").text, Is.EqualTo("2 点蓝面 → 2 点蓝面（光滑）"));
                if (id == "U07")
                {
                    // The face as it is now (the earlier upgrades of this loop gave it a texture and gold): 2 点蓝面（粗糙、黄金）.
                    var f2 = priorDie.faces.Single(f => f.faceId == "F02"); var traits = new System.Collections.Generic.List<string>();
                    if (f2.texture == "smooth") traits.Add("光滑"); else if (f2.texture == "rough") traits.Add("粗糙");
                    if (f2.isGold) traits.Add("黄金"); if (f2.baseWeight > 1) traits.Add("权重 " + f2.baseWeight);
                    string faceText = f2.permanentPip + " 点" + (f2.baseColor == "red" ? "红" : "蓝") + "面" + (traits.Count > 0 ? "（" + string.Join("、", traits) + "）" : "");
                    string others = string.Join("、", Enumerable.Range(app.Config.minPipValue, app.Config.maxPipValue - app.Config.minPipValue + 1).Where(n => n != f2.permanentPip));
                    Assert.That(FindActive<TextMeshProUGUI>("PurchasePreviewBody").text.Replace("\n", ""), Is.EqualTo(faceText + " → 随机变成 " + others + " 之一"));
                    Assert.That(FindActive<TextMeshProUGUI>("FacePickNote14").text, Is.EqualTo("颜色、纹理、黄金不变；买下时才抽"));
                }
                Click("CancelFacePick14");
                Assert.That(StateJson(), Is.EqualTo(before), "Selecting and cancelling " + id + " must not charge, modify or draw RNG.");
                yield return OpenBoundaryOffer(id); SelectUpgradeTarget(id);
                long gold = app.Session.State.gold; Click("ConfirmPurchase");
                var current = app.Session.State;
                Assert.That(current.gold, Is.EqualTo(gold - app.Config.Upgrade(id).buyPrice));
                var command = current.processedReceipts.Last().command;
                Assert.That(command.offerId, Is.EqualTo(id));
                Assert.That(command.targetDieId, Is.EqualTo("D001"));
                if (id == "U05") Assert.That(string.IsNullOrEmpty(command.targetFaceId), Is.True);
                else Assert.That(command.targetFaceId, Is.EqualTo("F02"), "The actual UI callback must preserve the chosen physical face.");
                var changed = current.dice.Single(d => d.dieId == "D001");
                Assert.That(changed.dataRevision, Is.EqualTo(priorDie.dataRevision + 1));
                CollectionAssert.AreEqual(priorDie.faces.Select(f => f.faceId), changed.faces.Select(f => f.faceId));
                if (id == "U01")
                {
                    fixture = app.Session.State;
                    fixture.shop.offers.RemoveAll(o => o.category == "die_upgrade" && o.contentId == "U01");
                    AddBoundaryOffer(fixture, "duplicate-smooth", "die_upgrade", "U01");
                    yield return LoadBoundaryFixture(fixture); ResumeBoundaryFixture();
                    string accepted = StateJson();
                    yield return OpenBoundaryOffer("duplicate-smooth"); SelectUpgradeTarget("U01");
                    Assert.That(Button("ConfirmPurchase").interactable, Is.False);
                    Assert.That(FindActive<TextMeshProUGUI>("PurchaseUnavailable").text, Is.EqualTo("这一面已经是光滑的，换一个面"));
                    Click("CancelFacePick14"); Assert.That(StateJson(), Is.EqualTo(accepted));
                }
                if (id == "U07")
                {
                    var oldFace = priorDie.faces.Single(f => f.faceId == "F02");
                    var newFace = changed.faces.Single(f => f.faceId == "F02");
                    Assert.That(newFace.permanentPip, Is.Not.EqualTo(oldFace.permanentPip));
                    Assert.That(newFace.baseColor, Is.EqualTo(oldFace.baseColor));
                    Assert.That(newFace.texture, Is.EqualTo(oldFace.texture));
                    Assert.That(newFace.isGold, Is.EqualTo(oldFace.isGold));
                    foreach (var face in changed.faces.Where(f => f.faceId != "F02"))
                        Assert.That(JsonUtility.ToJson(face), Is.EqualTo(JsonUtility.ToJson(priorDie.faces.Single(f => f.faceId == face.faceId))));
                }
            }

            fixture = ShopBoundaryFixture();
            var candidates = fixture.shop.offers.Where(o => o.category == "new_die").Take(2).Select(o => o.Clone()).ToArray();
            Assert.That(candidates, Has.Length.EqualTo(2));
            fixture.dice.Add(DiceFactory.CreateStandard(app.Config, fixture.nextDieSerial++)); fixture.dice.Add(DiceFactory.CreateStandard(app.Config, fixture.nextDieSerial++));
            fixture.shop.offers = candidates.ToList(); AddBoundaryOffer(fixture, "remove", "service", "O04");
            string frozenCandidate = JsonUtility.ToJson(candidates[1].die);
            yield return LoadBoundaryFixture(fixture); ResumeBoundaryFixture();
            yield return BuyBoundaryOffer(candidates[0].offerId);
            Assert.That(app.Session.State.dice.Count, Is.EqualTo(8));
            string full = StateJson();
            Assert.That(PriceWord14(candidates[1].offerId),Is.EqualTo("骰子已满 "+app.Config.maxPermanentDice+" 颗"));
            var missing=app.Send(new SessionCommand{kind=CommandKind.Buy,offerId="remove"});
            Assert.That(missing.accepted,Is.False);
            Assert.That(missing.error,Is.EqualTo("请选择永久骰子。"));
            yield return null;
            Assert.That(ToastLabels07().Any(label=>label.text=="请选择骰子。"),Is.True);
            Assert.That(StateJson(),Is.EqualTo(full));
            yield return OpenBoundaryOffer(candidates[1].offerId);
            Assert.That(Button("ConfirmPurchase").interactable, Is.False);
            Assert.That(FindActive<TextMeshProUGUI>("PurchaseUnavailable").text, Is.EqualTo("骰子已满 "+app.Config.maxPermanentDice+" 颗。"));
            Assert.That(app.Session.PurchaseError(candidates[1].offerId),Is.EqualTo("永久骰数量已满或候选无效。"));
            Click("CloseDialog"); Assert.That(StateJson(), Is.EqualTo(full));
            yield return OpenBoundaryOffer("remove"); Click("Target_D001"); Click("CancelFacePick14");
            Assert.That(StateJson(), Is.EqualTo(full), "Cancelling deletion must retain the entire die and its upgrades.");
            yield return OpenBoundaryOffer("remove"); Click("Target_D001"); Click("ConfirmPurchase");
            Assert.That(app.Session.State.dice.Count, Is.EqualTo(7));
            Assert.That(app.Session.State.baseSubmitCapacity, Is.EqualTo(5));
            yield return OpenBoundaryOffer(candidates[1].offerId);
            Assert.That(Button("ConfirmPurchase").interactable, Is.True);
            // v0.73: the candidate's faces are on the real die (drag or the face buttons show each one), not on flat tiles.
            var candidatePose = app.table.GetFaceOrientation("$die-preview"); Assert.That(candidatePose, Is.Not.Null);
            foreach (var face in candidates[1].die.faces)
            {
                Assert.That(candidatePose.Find("Face_"+face.faceId).GetComponent<MeshFilter>().sharedMesh.name, Does.StartWith("Pips_"+face.permanentPip+"_"), "The fixed candidate die must carry its actual pip count on every physical face.");
                Assert.That(FindActive<Transform>("Face_"+face.faceId).Cast<Transform>().Any(t=>t.gameObject.activeSelf&&t.name.StartsWith("Pip")), Is.False, "No flat pips on the face buttons.");
            }
            Click("ConfirmPurchase");
            Assert.That(app.Session.State.dice.Count, Is.EqualTo(8));
            Assert.That(JsonUtility.ToJson(app.Session.State.shop.offers.Single(o => o.offerId == candidates[1].offerId).die), Is.EqualTo(frozenCandidate));
            var added = app.Session.State.dice.OrderByDescending(d => d.createdSerial).First();
            CollectionAssert.AreEqual(candidates[1].die.faces.Select(JsonUtility.ToJson), added.faces.Select(JsonUtility.ToJson));
            Assert.That(app.Session.State.shopRngState, Is.EqualTo(fixture.shopRngState), "A full pool, deletion and reopening must not redraw candidates.");

            fixture = app.Session.State; fixture.dice = fixture.dice.Take(1).ToList(); fixture.shop.offers.Clear();
            AddBoundaryOffer(fixture, "last-remove", "service", "O04");
            yield return LoadBoundaryFixture(fixture); ResumeBoundaryFixture();
            string lastDieState = StateJson();
            Assert.That(PriceWord14("last-remove"),Is.EqualTo("至少留 1 颗骰子"));
            Assert.That(app.Session.PurchaseError("last-remove",fixture.dice[0].dieId),Is.EqualTo("此服务已达上限或没有合法目标。"));
            yield return OpenBoundaryOffer("last-remove"); Click("Target_" + fixture.dice[0].dieId);
            Assert.That(Button("ConfirmPurchase").interactable, Is.False);
            Assert.That(FindActive<TextMeshProUGUI>("PurchaseUnavailable").text, Is.EqualTo("至少要留 1 颗骰子"));
            Click("CancelFacePick14"); Assert.That(StateJson(), Is.EqualTo(lastDieState));
        }

        [UnityTest]
        public IEnumerator PLAY_TX07_SellingTheLastUnenchantedCardGivesTheTableItsActualRejectionReason()
        {
            var fixture=ShopBoundaryFixture();
            fixture.controllers.Clear();
            var unenchanted=BoundaryController(fixture,"C02");
            fixture.controllers.Add(unenchanted);
            fixture.shop.offers.Clear();
            AddBoundaryOffer(fixture,"table-tx07","service","O11");
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();

            yield return OpenBoundaryOffer("table-tx07");
            Assert.That(Button("ConfirmPurchase").interactable,Is.True);
            Click("CloseDialog");Click("Controller_"+unenchanted.instanceId);Click("SellController");Click("ConfirmSale");
            yield return WaitForIdle();
            string before=StateJson();
            yield return OpenBoundaryOffer("table-tx07");
            Assert.That(Button("ConfirmPurchase").interactable,Is.False);
            Assert.That(DialogLabel("PriceBody"),Does.Contain("没有可以附魔的卡牌。").And.Not.Contain("合法目标"));
            Assert.That(app.Session.PurchaseError("table-tx07"),Is.EqualTo("此服务已达上限或没有合法目标。"));
            Click("CloseDialog");
            var rejected=app.Send(new SessionCommand{kind=CommandKind.Buy,offerId="table-tx07"});
            Assert.That(rejected.accepted,Is.False);
            Assert.That(rejected.error,Is.EqualTo("此服务已达上限或没有合法目标。"));
            yield return null;
            Assert.That(ToastLabels07().Any(label=>label.text=="没有可以附魔的卡牌。"),Is.True);
            Assert.That(StateJson(),Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator PLAY_TX07_TotalDiceLimitUsesTheActiveCapOnTheShelfAndInTheOffer()
        {
            var fixture=ShopBoundaryFixture();
            var candidate=fixture.shop.offers.First(o=>o.category=="new_die");
            fixture.shop.offers=new System.Collections.Generic.List<ShopOffer>{candidate};
            while(fixture.dice.Count<app.Config.maxPermanentDice-1)
                fixture.dice.Add(DiceFactory.CreateStandard(app.Config,fixture.nextDieSerial++));
            while(fixture.dice.Count<app.Config.maxActiveDice)
                fixture.dice.Add(DiceFactory.CreateStandard(app.Config,fixture.nextDieSerial++,"stage_temporary",fixture.stage.stageId));
            // A valid restored state can carry these dice; ordinary settlement expires the temporary ones.
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();
            string before=StateJson();
            string activeLimit="骰子已满 "+app.Config.maxActiveDice+" 颗";
            Assert.That(PriceWord14(candidate.offerId),Is.EqualTo(activeLimit));
            yield return OpenBoundaryOffer(candidate.offerId);
            Assert.That(FindActive<TextMeshProUGUI>("PurchaseUnavailable").text,Is.EqualTo(activeLimit+"。"));
            Assert.That(Button("ConfirmPurchase").interactable,Is.False);
            Assert.That(StateJson(),Is.EqualTo(before));
        }

        [UnityTest]
        public IEnumerator PLAY_TX07_UnknownPurchaseDiagnosticKeepsRawDetailsAndGameLog()
        {
            var fixture=ShopBoundaryFixture();
            yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();
            const string raw="折现报价无效。";
            LogAssert.Expect(LogType.Warning,"商店操作被拒："+raw);
            W11Invoke("QueueCommandRejection16",new SessionCommand{kind=CommandKind.Buy},raw);
            app.Refresh();yield return null;
            Assert.That(ToastLabels07().Any(label=>label.text=="这件商品暂时不能购买。"),Is.True);
            Click("ToastDetails07");
            Assert.That(InformationText07("NoticeDetailBody"),Is.EqualTo(raw));
            string original=GUIUtility.systemCopyBuffer;
            try { Click("CopyInternalError16");Assert.That(GUIUtility.systemCopyBuffer,Is.EqualTo(raw)); }
            finally { GUIUtility.systemCopyBuffer=original; }
        }

        private RunState ShopBoundaryFixture()
        {
            var fixture = NewBoundaryFixture("shop-boundary-fixture");
            foreach (var die in fixture.dice) foreach (var face in die.faces) { face.permanentPip = 4; face.baseColor = "red"; }
            var session = new GameSession(app.Config, new MemorySaveStore(), fixture);
            CoreShopStep(session, CommandKind.StartStage); CoreShopStep(session, CommandKind.Roll);
            foreach (var die in session.State.dice) Assert.That(session.TogglePending(die.dieId), Is.True);
            CoreShopStep(session, CommandKind.ConfirmLocks); CoreShopStep(session, CommandKind.Submit); CoreShopStep(session, CommandKind.EndStage); CoreShopStep(session, CommandKind.Continue);
            var shop = session.State; Assert.That(shop.phase, Is.EqualTo("Shop"));
            shop.gold = 200;
            foreach (var die in shop.dice) die.faces = DiceFactory.CreateStandard(app.Config, die.createdSerial).faces;
            return shop;
        }
        private static void CoreShopStep(GameSession session, CommandKind kind)
        {
            long seq = session.State.actionSequence;
            var result = session.Execute(new SessionCommand {kind = kind, commandId = "shop-setup-" + (seq + 1), expectedActionSequence = seq});
            Assert.That(result.accepted, Is.True, kind + ": " + result.error);
        }
        private void AddBoundaryOffer(RunState fixture, string offerId, string category, string contentId)
        {
            int basePrice = category == "controller" ? app.Config.Controller(contentId).buyPrice
                : category == "consumable" ? app.Config.Consumable(contentId).baseBuyPrice
                : category == "hand_upgrade" ? app.Config.Hand(contentId).baseUpgradePrice
                : category == "die_upgrade" ? app.Config.Upgrade(contentId).buyPrice
                : category == "service" ? app.Config.Service(contentId).baseBuyPrice : 0;
            fixture.shop.offers.Add(new ShopOffer
            {
                offerId = offerId, batchId = fixture.shop.generationId + "/" + category + (category == "controller" ? "/" + fixture.shop.controllerGeneration : ""), category = category, contentId = contentId, basePrice = basePrice,
                controller = category == "controller" ? BoundaryController(fixture, contentId) : null
            });
        }
        // Schema 4 persists the controller's acquisition identity, including unsold offers.
        // Allocate from the same serial namespace as production; never bypass validation.
        private static ControllerInstance BoundaryController(RunState fixture, string definitionId, string enchantmentId = "none")
        {
            int serial = fixture.nextItemSerial++;
            return new ControllerInstance
            {
                instanceId = "ITEM_" + serial.ToString("D5"), acquiredSerial = serial,
                definitionId = definitionId, enchantmentId = enchantmentId
            };
        }
        private void ShopTabFor(string offerId)
        {
            // v0.73: the shop is one page; every section is always visible, so there is no tab to open.
            Assert.That(app.Session.State.shop.offers.Count(o => o.offerId == offerId), Is.EqualTo(1));
        }
        private IEnumerator OpenBoundaryOffer(string id) { yield return WaitForIdle(); ShopTabFor(id); yield return InspectOfferCard(id); }
        private IEnumerator BuyBoundaryOffer(string id) { yield return OpenBoundaryOffer(id); yield return LegacyClick07("ConfirmPurchase"); yield return WaitForIdle(); }
        private void AssertBoundaryCardPrice(string id, int price)
        {
            ShopTabFor(id);
            // V1.1 (W14, CARD_TEMPLATE §5): the transaction price is on the card's price button (v0.73: a gold price chip).
            var priced = app.Session.State.shop.offers.Single(o => o.offerId == id);
            var priceCard = FindActive<Transform>((priced.category=="controller"?"Offer_":"Product_")+id);
            Assert.That(priceCard.Find("Buy_" + id + "/Price").GetComponent<TextMeshProUGUI>().text, Is.EqualTo(price.ToString()));
            // The button's word (its PriceWord13 label; the first label is now the price) says what it does: 购买 exactly when
            // it can be pressed, otherwise why not (v0.73: the button always said 购买 and a separate line gave the reason).
            string word = PriceWord14(id); bool can = Button("Buy_" + id).interactable;
            Assert.That(word, Is.Not.Empty, id);
            Assert.That(word == "购买", Is.EqualTo(can), id + " reads '" + word + "' with interactable " + can);
        }
        private string DialogLabel(string name) => FindActive<Transform>("Dialog").GetComponentsInChildren<TextMeshProUGUI>().Single(t => t.name == name).text;
        private string FaceLabel(string faceId, string name) => FindActive<Transform>("Face_" + faceId).GetComponentsInChildren<TextMeshProUGUI>().Single(t => t.name == name).text;
        private void SelectUpgradeTarget(string id) { Click("Target_D001"); if (id != "U05") Click("Face_F02"); }
    }
}
