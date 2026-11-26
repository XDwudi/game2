using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DiceCraft.Core;
using DiceCraft.Presentation;

namespace DiceCraft
{
    // V1.1.1 task W7a (RULES_DELTA sections 2, 5, 6, 7 and 13): the wording of p1.1.1 numbers in one place, so every
    // screen says the same thing. A bonus of the additive zone reads "加成 +X", a card multiplier "得分 ×N", a base
    // score add "基础分 +N"; all values come from the configuration and the session, never from literals.
    public sealed partial class GameApp
    {
        /// <summary>A bonus in hundredths with its sign: 150 -> "+1.5".</summary>
        private static string Bonus111(long amount100)=>"+"+PlayerNumberFormat.Ratio(amount100,100);
        /// <summary>The factor of the bonus line: 375 -> "×4.75" (1 + A).</summary>
        private static string BonusFactor111(long total100,int fullDigits=PlayerNumberFormat.FullDigits)=>PlayerNumberFormat.Times(100+(BigInteger)total100,100,fullDigits);
        /// <summary>A growth value the way its card uses it: "加成 +1.5", "得分 ×1.25" or "基础分 +15".</summary>
        private static string GrowthShown111(string abilityId,long value)
            =>ControllerRules.IsGrowthMultiplier(abilityId)?"得分 "+PlayerNumberFormat.Times(value,100)
              :ControllerRules.IsGrowthBase(abilityId)?"基础分 +"+value:"加成 "+Bonus111(value);
        /// <summary>A change of a growth value in the card's unit: "+0.4" (bonus and multiplier, hundredths) or "+3" (base points).</summary>
        private static string GrowthStep111(string abilityId,long units)
            =>(units<0?"−":"+")+(ControllerRules.IsGrowthBase(abilityId)?Math.Abs(units).ToString():PlayerNumberFormat.Ratio(Math.Abs(units),100));
        /// <summary>The same for a recorded growth counter (the grown part) of the card `instanceId`: start, catch-up and
        /// growth together. The start and the catch-up never change, so the catch-up is read from the card as it is now
        /// (0 when the card is gone).</summary>
        private string GrowthShown111(string abilityId,string instanceId,long growth,IEnumerable<ControllerInstance> cards=null)
        {
            if(!ControllerRules.HasGrowth(abilityId))return growth.ToString();
            var card=(cards??Enumerable.Empty<ControllerInstance>()).Concat(ViewState?.controllers??Enumerable.Empty<ControllerInstance>()).FirstOrDefault(c=>c!=null&&c.instanceId==instanceId);
            return GrowthShown111(abilityId,checked(Config.Controller(abilityId).parameters.growthStart+(card?.state?.growthCatchup??0)+growth));
        }
        /// <summary>True while the boss effect that reads growth at half is in force (RULES_DELTA 5.1).</summary>
        private bool GrowthHalved111
            =>ViewState!=null&&ViewState.stage!=null&&ViewState.stage.started&&ViewState.phase!="Shop"&&ViewState.phase!="StagePreview"
              &&ControllerRules.ModifierHas(Config,Session.CurrentModifierId,"GrowthHalf");
        /// <summary>
        /// What a card is worth right now, in the card's own words, or null when it has no changing value: growth cards
        /// (start + catch-up + growth, the growth at half under the boss effect), 昙花一现, 收藏家, 客串明星 and 绝地反击.
        /// `state` is the state the ability reads (a mimic passes its target's); `held` is false for a shop offer.
        /// </summary>
        private string CardValue111(string abilityId,ControllerDynamicState state,bool held)
        {
            if(abilityId==null||state==null)return null;
            if(ControllerRules.HasGrowth(abilityId))return GrowthShown111(abilityId,ControllerRules.GrowthValue(Config,abilityId,state,held&&GrowthHalved111));
            var p=Config.Controller(abilityId).parameters;
            switch(abilityId)
            {
                case "C141":return "加成 "+Bonus111(Math.Max(0,p.initialAdditive100-state.growth*p.decayPerClear100));
                case "C63":return held?"加成 "+Bonus111((long)ViewState.dice.Count(d=>d.lifetime=="permanent")*p.additivePerPermanentDie100):null;
                case "C137":return state.starPip>0?"本关明星点数 "+state.starPip:"明星点数待入关";
                case "C156":
                    if(!held||ViewState?.stage==null||!ViewState.stage.started||ViewState.phase=="Shop"||ViewState.phase=="StagePreview")return null;
                    return ExactScoreMath.Parse(ViewState.stage.score)*p.targetDivisor<ExactScoreMath.Parse(ViewState.stage.target)?"加成 "+Bonus111(p.additiveBonus100)+" 生效中":"累计已过半，不生效";
                default:return null;
            }
        }
        /// <summary>"当前 加成 +1.5" for a card state line; the fixed sentences of 客串明星 and 绝地反击 stand alone.</summary>
        private static string CurrentValue111(string value)
            =>value==null?null:value.StartsWith("加成 ",StringComparison.Ordinal)&&!value.EndsWith("生效中",StringComparison.Ordinal)||value.StartsWith("得分 ",StringComparison.Ordinal)||value.StartsWith("基础分 ",StringComparison.Ordinal)?"当前 "+value:value;

        // ---------------------------------------------------------------- colours, enchantments, gold, glass
        /// <summary>A colour's bonus at a level: "加成 +1.75".</summary>
        private string ColorBonus111(string colorId,int level)
        {var c=Config.Color(colorId);return "加成 "+Bonus111(c.baseAdd100+(long)level*c.addPerLevel100);}
        /// <summary>"加成 +0.75 → +1.25" for a colour upgrade from `level` by `gain` levels (capped at the colour's maximum).</summary>
        private string ColorUpgrade111(string colorId,int level,int gain=1)
        {
            var c=Config.Color(colorId);int next=Math.Min(c.maxLevel,level+gain);
            return "加成 "+Bonus111(c.baseAdd100+(long)level*c.addPerLevel100)+" → "+Bonus111(c.baseAdd100+(long)next*c.addPerLevel100);
        }
        private string GoldFaceText111=>"朝上提交时加成 "+Bonus111(Config.goldAdd100);
        /// <summary>What gilding does to this face's weight: "权重 +1（上限 5）", or that it is at the limit already.</summary>
        private string GildWeightText111(FaceState face)
            =>face!=null&&face.baseWeight>=Config.maxFaceWeight?"权重已到上限 "+Config.maxFaceWeight:"权重 +"+Config.gildedFaceWeightDelta+"（上限 "+Config.maxFaceWeight+"）";
        private string GlassRiskText111=>"提交后每颗有 "+Session.GlassBreakPercentIn(ViewState)+"% 概率碎裂";

        // ---------------------------------------------------------------- the bonus line of a score
        /// <summary>Where one bonus of the additive zone comes from, for the expanded bonus line.</summary>
        private string BonusSource111(AdditiveEntry e,SubmissionSnapshot snapshot)
        {
            IList<ControllerInstance> cards=snapshot?.equippedControllers??(IList<ControllerInstance>)ViewState?.controllers;
            string at=string.IsNullOrEmpty(e.emitterId)?null:CardPosition11(cards,e.emitterId);
            string owner=!string.IsNullOrEmpty(e.stateOwnerId)&&e.stateOwnerId!=e.emitterId?CardPosition11(cards,e.stateOwnerId):null;
            string where=at==null?"":"（"+at+(owner==null?"":"，复制"+owner)+"）";
            switch(e.kind)
            {
                case "color":return NameOf(e.abilityId);
                case "gold":return "黄金面"+(snapshot!=null&&e.dieIndex>=0&&e.dieIndex<snapshot.dice.Length?"（"+DieName11(snapshot.dice[e.dieIndex].dieId)+"）":"");
                case "enchantment":
                {
                    // The enchantment sits on the emitting card; a mimic's entry names the copied ability as its owner.
                    var emitter=cards?.FirstOrDefault(c=>c.instanceId==e.emitterId);
                    return (emitter==null?"卡牌":NameOf(emitter.definitionId))+" · 附魔"+(at==null?"":"（"+at+"）");
                }
                default:return NameOf(e.abilityId)+where;
            }
        }
        /// <summary>The bonus line in words: "加成 ×4.75 = 1 + 全蓝 0.75 + 黄金面 0.5 + …"; "加成 ×1（没有加成）" when A is 0.</summary>
        private string BonusBreakdown111(ScoreBreakdown b,SubmissionSnapshot snapshot)
        {
            if(b==null)return "";
            var entries=b.additiveEntries??Array.Empty<AdditiveEntry>();
            if(entries.Length==0)return "加成 "+BonusFactor111(0,PlayerNumberFormat.Unlimited)+"（没有加成）";
            return "加成 "+BonusFactor111(b.additiveTotal100,PlayerNumberFormat.Unlimited)+" = 1 + "+string.Join(" + ",entries.Select(e=>BonusSource111(e,snapshot)+" "+PlayerNumberFormat.Ratio(e.amount100,100)));
        }
        /// <summary>The label of a displayed factor (ScoreFactors111 keys).</summary>
        private string FactorLabel111(ScoreFactor111 f)
            =>f.Key=="base"?"基础":f.Key=="hand"?"牌型":f.Key=="red"?"红组":f.Key=="additive"?"加成":f.Key=="controller"?"卡牌":NameOf(f.SourceId);

        /// <summary>Handbook rows of the p1.1.1 mechanics, after the older rows so that their row numbers stay.</summary>
        private string[][] HandbookRules111() => new string[][]
        {
            new[]{"加成","全红或全蓝、朝上提交的黄金面（每颗 "+Bonus111(Config.goldAdd100)+"）、卡面写着“加成 +X”的效果和“加成”附魔（每次 "+Bonus111(Config.enchantRules.scoreAdd100)+"），全部先加在一起，再让得分 ×（1 ＋ 合计）。例如 +2、+1.5、+0.5 合计 4，得分 ×5。它们之间是相加；卡面写“得分 ×N”的卡牌才各自相乘。得分明细里的“加成”一行列出每一项。"},
            new[]{"养成卡","会永久改写骰面的卡牌是养成卡："+string.Join("、",Config.controllers.Where(c=>c.isCultivation).Select(c=>c.displayName))+"。排在最左边的一张养成卡不占卡牌栏位；每张养成卡每改写 "+Config.cultivationFacesPerSaleBonus+" 个骰面，卖价 +1，最多 +"+Config.cultivationMaxSaleBonus+"。"},
            new[]{"黄金面与玻璃骰","新镀金的面权重 +"+Config.gildedFaceWeightDelta+"（上限 "+Config.maxFaceWeight+"），更容易掷出。玻璃骰计分 ×"+Config.glassContributionMultiplier+"；每次提交后，提交的每颗玻璃骰有 "+Config.glassBreakPercent+"% 概率碎裂，投掷时和留在骰池里都不会碎。"},
        };

        // ---------------------------------------------------------------- cultivation cards
        private bool IsCultivation111(string definitionId)=>definitionId!=null&&definitionId.StartsWith("C",StringComparison.Ordinal)&&Config.Controller(definitionId).isCultivation;
        /// <summary>The cultivation lines of a held card: its tag, the free slot, the faces it rewrote and what they add to its price.</summary>
        private string CultivationText111(ControllerInstance c)
        {
            if(c==null||!IsCultivation111(c.definitionId))return null;
            bool free=ControllerRules.FreeCultivationInstanceId(ViewState.controllers,Config)==c.instanceId;
            int faces=c.state?.cultivationFacesChanged??0,per=Config.cultivationFacesPerSaleBonus,cap=Config.cultivationMaxSaleBonus;
            int bonus=Math.Min(cap,faces/Math.Max(1,per));
            return "养成卡"+(free?" · 最左边的一张养成卡，不占卡牌栏位":c.enchantmentId==ControllerRules.SlotlessEnchantmentId?"":" · 占 1 个卡牌栏位（只有最左边的一张养成卡不占）")
                +"\n已改写 "+faces+" 个骰面 · 卖价 +"+bonus+"（每改写 "+per+" 个面卖价 +1，最多 +"+cap+"）";
        }
    }
}
