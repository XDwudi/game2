using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;

namespace DiceCraft.Tests
{
    public sealed class V15ReceiptTermsTests
    {
        [TestCase(0,0,3)]
        [TestCase(3,0,4)]
        [TestCase(4,4,4)]
        [TestCase(7,4,5)]
        public void CommittedRewardChipsKeepTheirOrderAndNeverDoubleCountBounty(long controller,long bounty,int expected)
        {
            var receipt=new StageSettlement{baseReward=5,remainingSubmissions=2,unusedSubmissionReward=2,interest=0,controllerIncome=controller,bountyIncome=bounty};
            var chips=SettlementRewardTerms15.Chips(receipt);
            Assert.That(chips.Count,Is.EqualTo(expected));
            CollectionAssert.AreEqual(new[]{"过关奖励 +5","剩余提交 2 次 +2","利息 +0"},chips.Take(3).Select(c=>c.Text));
            Assert.That(chips.Skip(3).Sum(c=>c.amount),Is.EqualTo(controller));
            if(controller>bounty)Assert.That(chips[3].Text,Is.EqualTo("铁饭碗 +3"));
            if(bounty>0)Assert.That(chips.Last().Text,Is.EqualTo("赏金猎人 +4"));
        }
        [TestCase(0,25,0,"利息：每 5 金币 +1，每关最多 +5")]
        [TestCase(2,25,0,"利息：每 5 金币 +3，每关最多 +15")]
        [TestCase(0,50,0,"利息：每 5 金币 +1，每关最多 +5（利滚利：最多 +10）")]
        [TestCase(0,35,1,"利息：每 5 金币 +1，每关最多 +5（理财顾问：最多 +7）")]
        [TestCase(1,60,1,"利息：每 5 金币 +2，每关最多 +10（利滚利、理财顾问：最多 +24）")]
        public void InterestExplainsRecordedPigCountPrincipalCapAndAdviserShare(int pigs,long cap,int purchases,string expected)
        {
            var r=new StageSettlement{pigSourceCount=pigs,interestPrincipalCap=cap};
            Assert.That(SettlementRewardTerms15.Interest(r,5,25,1,10,purchases,"利滚利","理财顾问"),Is.EqualTo(expected));
        }
    }
}
