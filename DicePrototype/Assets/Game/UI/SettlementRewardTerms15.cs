using System;
using System.Collections.Generic;
using DiceCraft.Core;

namespace DiceCraft
{
    public static class SettlementRewardTerms15
    {
        public readonly struct RewardChip15
        {
            public readonly string id, label, icon;
            public readonly long amount;
            public string Text => label+" +"+PlayerNumberFormat.Gold(amount);
            public RewardChip15(string id,string label,long amount,string icon)
            { this.id=id; this.label=label; this.amount=amount; this.icon=icon; }
        }

        public static List<RewardChip15> Chips(StageSettlement r)
        {
            var chips=new List<RewardChip15> {
                new RewardChip15("SettlementBaseReward","过关奖励",r.baseReward,"ui_icon_stage"),
                new RewardChip15("SettlementUnusedReward","剩余提交 "+r.remainingSubmissions+" 次",r.unusedSubmissionReward,"ui_icon_submit"),
                new RewardChip15("SettlementInterest","利息",r.interest,"ui_icon_interest")
            };
            chips.AddRange(ControllerChips(r));
            return chips;
        }

        public static IEnumerable<RewardChip15> ControllerChips(StageSettlement r)
        {
            if(r.controllerIncome>r.bountyIncome)yield return new RewardChip15("SettlementControllerIncome","铁饭碗",r.controllerIncome-r.bountyIncome,"C95");
            if(r.bountyIncome>0)yield return new RewardChip15("SettlementBounty10","赏金猎人",r.bountyIncome,"C133");
        }

        public static long InterestRate(StageSettlement receipt,int extraPerPig) =>
            1L+(long)receipt.pigSourceCount*extraPerPig;

        public static string Interest(StageSettlement r,int divisor,int basicCap,int extraPerPig,int adviserStep,int purchases,string card,string adviserName)
        {
            long rate=InterestRate(r,extraPerPig);
            divisor=Math.Max(1,divisor);
            string line="利息：每 "+divisor+" 金币 +"+PlayerNumberFormat.Gold(rate)+"，每关最多 +"+PlayerNumberFormat.Gold(basicCap/divisor*rate);
            if(r.interestPrincipalCap<=basicCap)return line;
            long adviser=(long)adviserStep*purchases;
            var names=new List<string>();
            if(r.interestPrincipalCap-basicCap>adviser)names.Add(card);
            if(adviser>0)names.Add(adviserName);
            return line+"（"+string.Join("、",names)+"：最多 +"+PlayerNumberFormat.Gold(r.interestPrincipalCap/divisor*rate)+"）";
        }

    }
}
