using System;
using System.Linq;
using NUnit.Framework;
using DiceCraft.Core;

namespace DiceCraft.Tests
{
    public sealed class ShopTests : SessionTestSupport
    {
        [TestCase(false,36)] [TestCase(true,38)] public void FLOW_E01_RewardUsesOldBalance(bool pig, int expected)
        {
            var s = Selecting(new[] { 4,4,4,4,4 }, new[] { "red","red","red","red","red" }, pig ? new[] { "C01" } : null);
            s = Edit(s, x => x.gold = 27); Do(s, CommandKind.Submit); Do(s, CommandKind.EndStage);
            Assert.That(s.State.gold, Is.EqualTo(expected)); Assert.That(s.State.lastStageSettlement.interest, Is.EqualTo(pig ? 4 : 2));
        }
        [Test] public void FLOW_E02_BuyingPigNeverChangesPriorSettlement()
        {
            string offer = null; var s = Edit(Shop(), x => { x.gold = 100; offer = PutOffer(x, "controller", "C01"); });
            int interest = s.State.lastStageSettlement.interest; Do(s, CommandKind.Buy, c => c.offerId = offer);
            Assert.That(s.State.lastStageSettlement.interest, Is.EqualTo(interest));
            Assert.That(s.Execute(Command(s, CommandKind.Continue)).accepted, Is.False);
        }
        [TestCase("C01")] [TestCase("C02")] [TestCase("C03")] [TestCase("C04")] [TestCase("C05")]
        [TestCase("C06")] [TestCase("C07")] [TestCase("C08")] [TestCase("C09")] [TestCase("C10")]
        [TestCase("C11")] [TestCase("C12")] [TestCase("C13")] [TestCase("C14")] [TestCase("C15")]
        [TestCase("C16")] [TestCase("C17")] [TestCase("C18")] [TestCase("C19")] [TestCase("C20")]
        [TestCase("C21")] [TestCase("C22")] [TestCase("C23")] [TestCase("C24")] [TestCase("C25")]
        [TestCase("C26")] [TestCase("C27")] [TestCase("C28")] [TestCase("C29")] [TestCase("C30")]
        [TestCase("C31")] [TestCase("C32")] [TestCase("C33")] [TestCase("C34")] [TestCase("C35")]
        [TestCase("C36")] [TestCase("C37")] [TestCase("C38")] [TestCase("C39")] [TestCase("C40")]
        public void CONTENT_ControllerPurchaseDuplicateAndSell(string id)
        {
            string offer = null, second = null;
            var s = Edit(Shop(), x => { x.gold = 100; offer = PutOffer(x,"controller",id); second = offer; });
            var buy = Command(s, CommandKind.Buy); buy.offerId = offer;
            Assert.That(s.Execute(buy).accepted, Is.True); Assert.That(s.State.controllers.Single().contentId, Is.EqualTo(id));
            Assert.That(s.Execute(buy).duplicate, Is.True); Assert.That(s.State.gold, Is.EqualTo(100 - cfg.Controller(id).buyPrice));
            var bad = Command(s, CommandKind.Buy); bad.offerId = second; var before = s.State;
            Assert.That(s.Execute(bad).accepted, Is.False); AssertUnchanged(before,s.State);
            var instance = s.State.controllers.Single().instanceId; Do(s,CommandKind.Sell,c=>c.itemInstanceId=instance);
            Assert.That(s.State.gold, Is.EqualTo(before.gold + 1)); Assert.That(s.State.shop.offers.First(x=>x.offerId==offer).sold,Is.True);
        }
        [Test] public void FLOW_E03_ControllerFullRejectsWithoutRestocking()
        {
            string offer = null; var s = Edit(Shop(), x => { x.gold = 100; Equip(x,"C01","C02","C03","C04"); offer = PutOffer(x,"controller","C05"); });
            var c = Command(s,CommandKind.Buy); c.offerId=offer; var before=s.State;
            Assert.That(s.Execute(c).accepted,Is.False); AssertUnchanged(before,s.State);
            int count=s.State.shop.offers.Count; Do(s,CommandKind.Sell,c2=>c2.itemInstanceId=s.State.controllers[0].instanceId);
            Assert.That(s.State.shop.offers.Count,Is.EqualTo(count)); Do(s,CommandKind.Buy,c2=>c2.offerId=offer);
        }
        [Test] public void FLOW_E04_SellOnceAndConsumedCannotSell()
        {
            string item=null; var s=Edit(Shop(),x=>item=AddItem(x,"I01")); var c=Command(s,CommandKind.Sell); c.itemInstanceId=item;
            int before=s.State.gold; Assert.That(s.Execute(c).accepted,Is.True); Assert.That(s.Execute(c).duplicate,Is.True);
            Assert.That(s.State.gold,Is.EqualTo(before+1)); c=Command(s,CommandKind.Sell); c.itemInstanceId=item; Assert.That(s.Execute(c).accepted,Is.False);
        }
        [Test] public void FLOW_E05_E06_DiscountChargesOldPriceAndMinimumTwo()
        {
            string discount=null,item=null; var s=Edit(Shop(),x=>{x.gold=100;x.itemDiscount=2;discount=PutOffer(x,"service","O02");item=PutOffer(x,"consumable","I04");});
            Do(s,CommandKind.Buy,c=>c.offerId=discount); Assert.That(s.State.gold,Is.EqualTo(95)); Assert.That(s.State.itemDiscount,Is.EqualTo(3));
            Assert.That(s.GetOfferPrice(s.State.shop.offers.First(x=>x.offerId==item)),Is.EqualTo(2)); Do(s,CommandKind.Buy,c=>c.offerId=item);
            var owned=s.State.inventory.Single(); Do(s,CommandKind.Sell,c=>c.itemInstanceId=owned.instanceId); Assert.That(s.State.gold,Is.EqualTo(94));
        }
        [Test] public void FLOW_E07_DiscountDoesNotAffectControllersOrUpgrades()
        {
            string cOffer=null,uOffer=null; var s=Edit(Shop(),x=>{x.gold=100;x.itemDiscount=3;cOffer=PutOffer(x,"controller","C10");uOffer=PutOffer(x,"die_upgrade","U06");});
            Do(s,CommandKind.Buy,c=>c.offerId=cOffer); Do(s,CommandKind.Buy,c=>{c.offerId=uOffer;c.targetDieId="D001";c.targetFaceId="F01";});
            Assert.That(s.State.gold,Is.EqualTo(90));
        }
        [TestCase("O01")] [TestCase("O02")] public void FLOW_E08_MaxedServiceRejects(string id)
        {
            string offer=null; var s=Edit(Shop(),x=>{x.gold=100;x.consumableSlots=5;x.itemDiscount=3;offer=PutOffer(x,"service",id);});
            var c=Command(s,CommandKind.Buy);c.offerId=offer;var before=s.State;Assert.That(s.Execute(c).accepted,Is.False);AssertUnchanged(before,s.State);
        }
        [TestCase("U01")] [TestCase("U02")] [TestCase("U03")] [TestCase("U04")] [TestCase("U05")] [TestCase("U06")] [TestCase("U07")]
        public void CONTENT_UpgradeAppliesOnlyToTargetAndIsIdempotent(string id)
        {
            string offer=null;var s=Edit(Shop(),x=>{x.gold=100; x.dice[0].faces[0].baseColor=id=="U03"?"blue":"red"; offer=PutOffer(x,"die_upgrade",id);});
            var c=Command(s,CommandKind.Buy);c.offerId=offer;c.targetDieId="D001";c.targetFaceId="F01";var before=s.State;
            Assert.That(s.Execute(c).accepted,Is.True);var after=s.State;Assert.That(s.Execute(c).duplicate,Is.True);AssertUnchanged(after,s.State);
            Assert.That(after.gold,Is.EqualTo(before.gold-cfg.Upgrade(id).buyPrice));Assert.That(after.dice[0].dataRevision,Is.EqualTo(before.dice[0].dataRevision+1));
            Assert.That(after.dice[1].faces[0].permanentPip,Is.EqualTo(before.dice[1].faces[0].permanentPip));
            switch(id){
                case "U01":Assert.That(after.dice[0].faces[0].texture,Is.EqualTo("smooth"));break;
                case "U02":Assert.That(after.dice[0].faces[0].texture,Is.EqualTo("rough"));break;
                case "U03":Assert.That(after.dice[0].faces[0].baseColor,Is.EqualTo("red"));break;
                case "U04":Assert.That(after.dice[0].faces[0].baseColor,Is.EqualTo("blue"));break;
                case "U05":Assert.That(after.dice[0].isGlass,Is.True);break;
                case "U06":Assert.That(after.dice[0].faces[0].isGold,Is.True);break;
                case "U07":Assert.That(after.dice[0].faces[0].permanentPip,Is.Not.EqualTo(before.dice[0].faces[0].permanentPip));break;
            }
        }
        [TestCase("U01")] [TestCase("U02")] [TestCase("U03")] [TestCase("U04")] [TestCase("U05")] [TestCase("U06")]
        public void FLOW_E09_IdenticalUpgradeRejectsWithoutMoneyOrRandomness(string id)
        {
            string offer=null;var s=Edit(Shop(),x=>{x.gold=100;var f=x.dice[0].faces[0];f.texture=id=="U01"?"smooth":"rough";f.baseColor=id=="U03"?"red":"blue";f.isGold=true;x.dice[0].isGlass=true;offer=PutOffer(x,"die_upgrade",id);});
            var c=Command(s,CommandKind.Buy);c.offerId=offer;c.targetDieId="D001";c.targetFaceId="F01";var before=s.State;
            Assert.That(s.Execute(c).accepted,Is.False);AssertUnchanged(before,s.State);Assert.That(s.State.shop.offers.Last().sold,Is.False);
        }
        [Test] public void FLOW_E10_TransformExcludesOldPipAndPreservesAttributes()
        {
            foreach(uint seed in Enumerable.Range(1,80).Select(x=>(uint)x)){
                string offer=null;var s=Edit(Shop(),x=>{x.gold=100;var f=x.dice[0].faces[0];f.permanentPip=3;f.baseColor="blue";f.isGold=true;f.texture="rough";x.effectRngState=seed;offer=PutOffer(x,"die_upgrade","U07");});
                Do(s,CommandKind.Buy,c=>{c.offerId=offer;c.targetDieId="D001";c.targetFaceId="F01";});var face=s.State.dice[0].faces[0];
                Assert.That(face.permanentPip,Is.InRange(1,6));Assert.That(face.permanentPip,Is.Not.EqualTo(3));Assert.That(face.baseColor,Is.EqualTo("blue"));Assert.That(face.isGold,Is.True);Assert.That(face.texture,Is.EqualTo("rough"));
            }
        }
        [Test] public void FLOW_E11_NewDiePreviewFixedAndPurchaseClones()
        {
            var s=Edit(Shop(),x=>x.gold=100);var offers=s.State.shop.offers.Where(x=>x.category=="new_die").ToArray();Assert.That(offers.Length,Is.EqualTo(2));
            var rng=s.State.shopRngState;var reloaded=new GameSession(cfg,new MemorySaveStore(),s.State);var again=reloaded.State.shop.offers.Where(x=>x.category=="new_die").ToArray();
            Assert.That(again.Select(x=>x.offerId),Is.EqualTo(offers.Select(x=>x.offerId)));Assert.That(again[0].die.faces.Select(x=>x.permanentPip),Is.EqualTo(offers[0].die.faces.Select(x=>x.permanentPip)));
            Do(s,CommandKind.Buy,c=>c.offerId=offers[0].offerId);Assert.That(s.State.baseSubmitCapacity,Is.EqualTo(5));Assert.That(s.State.dice.Count,Is.EqualTo(6));Assert.That(s.State.shopRngState,Is.EqualTo(rng));
            var exposed=s.State;exposed.dice.Last().faces[0].permanentPip=9;Assert.That(exposed.shop.offers.First(x=>x.offerId==offers[0].offerId).die.faces[0].permanentPip,Is.InRange(1,6));
            Assert.That(s.Execute(new SessionCommand{kind=CommandKind.Buy,commandId="again",expectedActionSequence=s.State.actionSequence,offerId=offers[0].offerId}).accepted,Is.False);
        }
        [Test] public void FLOW_E12_LastPermanentDieCannotBeDeletedEvenWithTemporaryDie()
        {
            string offer=null;var s=Edit(Shop(),x=>{x.gold=100;x.dice.RemoveRange(2,3);x.dice[1].lifetime="stage_temporary";x.dice[1].createdStageId=x.stage.stageId;offer=PutOffer(x,"service","O04");});
            var c=Command(s,CommandKind.Buy);c.offerId=offer;c.targetDieId="D001";var before=s.State;Assert.That(s.Execute(c).accepted,Is.False);AssertUnchanged(before,s.State);
        }
        [Test] public void FLOW_E15_C09RechecksDisplayedHandQualification()
        {
            string cOffer=null,hOffer=null;var s=Edit(Shop(),x=>{x.gold=100;cOffer=PutOffer(x,"controller","C09");hOffer=PutOffer(x,"hand_upgrade","straight");x.handLevels.First(h=>h.handId=="straight").level=1;});
            Do(s,CommandKind.Buy,c=>c.offerId=cOffer);var c2=Command(s,CommandKind.Buy);c2.offerId=hOffer;var before=s.State;
            Assert.That(s.Execute(c2).accepted,Is.False);AssertUnchanged(before,s.State);Assert.That(s.State.handLevels.First(h=>h.handId=="straight").level,Is.EqualTo(1));
            Do(s,CommandKind.Sell,c=>c.itemInstanceId=s.State.controllers.Single().instanceId);Do(s,CommandKind.Buy,c=>c.offerId=hOffer);
        }
        [Test] public void ServicesO01O02O04AndNewDieCapacityRespectLimits()
        {
            string slots=null,discount=null,delete=null;var s=Edit(Shop(),x=>{x.gold=100;slots=PutOffer(x,"service","O01");discount=PutOffer(x,"service","O02");delete=PutOffer(x,"service","O04");});
            Do(s,CommandKind.Buy,c=>c.offerId=slots);Assert.That(s.State.consumableSlots,Is.EqualTo(3));
            Do(s,CommandKind.Buy,c=>c.offerId=discount);Assert.That(s.State.itemDiscount,Is.EqualTo(1));
            Do(s,CommandKind.Buy,c=>{c.offerId=delete;c.targetDieId="D001";});Assert.That(s.State.dice.Count,Is.EqualTo(4));Assert.That(s.State.baseSubmitCapacity,Is.EqualTo(5));
            foreach(var offer in s.State.shop.offers.Where(x=>x.category=="new_die").ToArray()) Do(s,CommandKind.Buy,c=>c.offerId=offer.offerId);
            Assert.That(s.State.baseSubmitCapacity,Is.EqualTo(5));Assert.That(s.State.dice.Count,Is.EqualTo(6));
        }
        [Test] public void SelfStudyAndRandomHandUseCurrentLevelAndEligibility()
        {
            string offer=null;var s=Edit(Shop(),x=>{x.gold=100;offer=PutOffer(x,"hand_upgrade","pair");});
            Do(s,CommandKind.Buy,c=>c.offerId=offer);Assert.That(s.GetStudyPrice("pair"),Is.EqualTo(10));
            Do(s,CommandKind.Study,c=>c.handId="pair");Assert.That(s.State.gold,Is.EqualTo(84));Assert.That(s.State.handLevels.First(x=>x.handId=="pair").level,Is.EqualTo(2));
            Assert.That(s.Execute(new SessionCommand{kind=CommandKind.Study,commandId="secondstudy",expectedActionSequence=s.State.actionSequence,handId="pair"}).accepted,Is.False);
        }
        [Test] public void GeneratedShopHasExactCategoriesNoReplacementAndAffordableFirstController()
        {
            var s=Shop();var offers=s.State.shop.offers;
            foreach(var category in new[]{"controller","consumable","hand_upgrade","die_upgrade","service","new_die"}){
                int expected=category=="controller"?3:category=="service"?1:2;
                Assert.That(offers.Count(x=>x.category==category),Is.EqualTo(expected),category);
                if(category!="new_die") Assert.That(offers.Where(x=>x.category==category).Select(x=>x.contentId).Distinct().Count(),Is.EqualTo(expected));
            }
            Assert.That(cfg.Controller(offers.First(x=>x.category=="controller").contentId).buyPrice,Is.LessThanOrEqualTo(8));
        }
        [Test] public void ShopGoldDoesNotLimitPurchaseCountAndInventoryFullDoesNotEraseSupply()
        {
            var s=Edit(Shop(),x=>{x.gold=100;AddItem(x,"I01");AddItem(x,"I02");});int before=s.State.shop.offers.Count;
            var item=s.State.shop.offers.First(x=>x.category=="consumable");var c=Command(s,CommandKind.Buy);c.offerId=item.offerId;Assert.That(s.Execute(c).accepted,Is.False);
            Do(s,CommandKind.Sell,c2=>c2.itemInstanceId=s.State.inventory[0].instanceId);Do(s,CommandKind.Buy,c2=>c2.offerId=item.offerId);
            foreach(var offer in s.State.shop.offers.Where(x=>x.category=="controller").ToArray()) Do(s,CommandKind.Buy,c2=>c2.offerId=offer.offerId);
            Assert.That(s.State.shop.offers.Count,Is.EqualTo(before));Assert.That(s.State.controllers.Count,Is.EqualTo(3));
        }
        [TestCase("U01")] [TestCase("U02")] [TestCase("U03")] [TestCase("U04")] [TestCase("U05")] [TestCase("U06")] [TestCase("U07")]
        public void CONTENT_UpgradeRejectsMissingTargetWithoutConsumingOffer(string id)
        {
            string offer=null;var s=Edit(Shop(),x=>{x.gold=100;offer=PutOffer(x,"die_upgrade",id);});
            var c=Command(s,CommandKind.Buy);c.offerId=offer;c.targetDieId="missing";c.targetFaceId="F01";var before=s.State;
            Assert.That(s.Execute(c).accepted,Is.False);AssertUnchanged(before,s.State);Assert.That(s.State.shop.offers.Last().sold,Is.False);
        }
        [Test] public void CONTENT_O03_FullPermanentPoolRejectsAndExistingCandidatesNeverRefresh()
        {
            var s=Edit(Shop(),x=>{x.gold=100;for(int i=6;i<=8;i++)x.dice.Add(DiceFactory.CreateStandard(cfg,i));x.nextDieSerial=9;});
            var offer=s.State.shop.offers.First(x=>x.category=="new_die");var c=Command(s,CommandKind.Buy);c.offerId=offer.offerId;var before=s.State;
            Assert.That(s.Execute(c).accepted,Is.False);AssertUnchanged(before,s.State);
            string deletion=null;s=Edit(s,x=>deletion=PutOffer(x,"service","O04"));
            Do(s,CommandKind.Buy,c2=>{c2.offerId=deletion;c2.targetDieId="D007";});
            Do(s,CommandKind.Buy,c2=>c2.offerId=offer.offerId);Assert.That(s.State.dice.Count,Is.EqualTo(8));Assert.That(s.State.baseSubmitCapacity,Is.EqualTo(5));
            Assert.That(s.State.shopRngState,Is.EqualTo(before.shopRngState));
        }
        [TestCase("O01")] [TestCase("O02")] [TestCase("O04")]
        public void CONTENT_ServiceDuplicateReceiptDoesNotRepeatMutation(string id)
        {
            string offer=null;var s=Edit(Shop(),x=>{x.gold=100;offer=PutOffer(x,"service",id);});
            var c=Command(s,CommandKind.Buy);c.offerId=offer;c.targetDieId="D001";Assert.That(s.Execute(c).accepted,Is.True);var after=s.State;
            Assert.That(s.Execute(c).duplicate,Is.True);AssertUnchanged(after,s.State);Assert.That(s.State.consumableSlots,Is.EqualTo(after.consumableSlots));Assert.That(s.State.itemDiscount,Is.EqualTo(after.itemDiscount));
        }
        [Test] public void SixthDieRequiresIndependentCapacityBeforeSixKindStudy()
        {
            var s=Edit(Shop(),x=>{x.gold=100;Equip(x,"C21");});Assert.That(s.LegalStudyHands(),Does.Not.Contain("six_kind"));
            var offer=s.State.shop.offers.First(x=>x.category=="new_die");Do(s,CommandKind.Buy,c=>c.offerId=offer.offerId);
            Assert.That(s.LegalStudyHands(),Does.Contain("six_kind"));Do(s,CommandKind.Study,c=>c.handId="six_kind");
            Assert.That(s.State.handLevels.First(x=>x.handId=="six_kind").level,Is.EqualTo(1));Assert.That(s.LegalStudyHands(),Does.Not.Contain("seven_kind"));
        }
    }
}
