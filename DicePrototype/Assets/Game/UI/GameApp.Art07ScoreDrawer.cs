using System;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        // Primary S07 surface; all calculations come from the supplied frozen
        // snapshot/breakdown. The wide original reader is an explicit second view.
        private void ShowBreakdown(SubmissionSnapshot snapshot,ScoreBreakdown b)
        {
            if(snapshot==null||b==null)return;
            bool responsive=Screen.safeArea.width<=1366&&UiKit.FontScale>=1.25f;
            float x=responsive?352:496,y=responsive?304:400,w=responsive?1536:1280,h=responsive?632:520;
            var p=Art07Modal("得分明细",x,y,w,h);
            // Art07Modal already starts the 160ms fade; set its original-spec
            // close time without restarting it, then unfold from the score edge.
            FadeModal07(p,false,.16f,.10f);
            UiKit.Component<Art07ScoreDrawerMotion>(p.gameObject).Begin(.16f,reduced);
            var trace=ScoreExplanation.Build(snapshot,b,Config);
            bool historical=ViewState.lastSubmission!=null&&snapshot.handId==ViewState.lastSubmission.snapshot.handId;
            string effects=ScoreEffects16(snapshot,historical);
            var list=UiKit.Scroll(p,"ScoreDrawerTable",24,96,w-48,h-184,h-184);
            float contentWidth=list.rect.width-16,cursor=6;
            var formula=UiKit.Text(list,"Formula",ScoreFormula07(b),8,cursor,contentWidth,80,29,UiKit.Gold);
            formula.textWrappingMode=TextWrappingModes.Normal;cursor+=FitHeight(formula,80)+20;
            const float gap=16;float sourceWidth=(contentWidth-gap*2)*.28f,calculationWidth=(contentWidth-gap*2)*.43f;
            float resultWidth=contentWidth-gap*2-sourceWidth-calculationWidth;
            float calculationX=sourceWidth+gap,resultX=calculationX+calculationWidth+gap;
            float headingHeight=48*UiKit.FontScale;
            UiKit.Text(list,"SourceColumnTitle","来源",8,cursor,sourceWidth,headingHeight,24,UiKit.Paper);
            UiKit.Text(list,"CalculationColumnTitle","计算式",8+calculationX,cursor,calculationWidth,headingHeight,24,UiKit.Paper);
            UiKit.Text(list,"ResultColumnTitle","结果",8+resultX,cursor,resultWidth,headingHeight,24,UiKit.Paper);
            cursor+=headingHeight+8;
            void Row(string id,string source,string calculation,string result,float font,Color sourceColor)
            {
                var row=UiKit.Rect(list,"ScoreRow_"+id,8,cursor,contentWidth,1);
                var a=UiKit.Text(row,"Source",source,0,0,sourceWidth,48,font,sourceColor);
                var c=UiKit.Text(row,"Calculation",calculation,calculationX,0,calculationWidth,48,font,UiKit.Muted);
                var r=UiKit.Text(row,"Result",result,resultX,0,resultWidth,48,font,UiKit.Gold);
                foreach(var cell in new[]{a,c,r}){cell.textWrappingMode=TextWrappingModes.Normal;cell.overflowMode=TextOverflowModes.Overflow;cell.enableAutoSizing=false;}
                float height=Math.Max(FitHeight(a,48),Math.Max(FitHeight(c,48),FitHeight(r,48)))+12;
                row.sizeDelta=new Vector2(contentWidth,height);
                UiKit.Box(row,"Rule",0,height-2,contentWidth,1,UiKit.PanelLight);
                cursor+=height+8;
            }
            for(int i=0;i<snapshot.dice.Length;i++)
            {
                var die=snapshot.dice[i];var face=die.permanentFacesCopy.Single(f=>f.faceId==die.faceId);
                // v1.1 (W09): "骰子 1 的第 3 面" instead of D001 / F03; colours without the R/B letters.
                Row("Die"+i,(i+1)+". "+DieFaceName11(die.dieId,die.faceId),
                    DiceLayers16(face,die.currentPip,die.currentColor,b.effectivePips[i],b.effectiveColors[i])+"\n六面合计 "+die.permanentSixFaceSum,
                    ScoreContribution16(b,i),25,UiKit.Paper);
                for(int j=0;j<trace.dice[i].steps.Length;j++)
                {
                    var step=trace.dice[i].steps[j];Row("Die"+i+"Step"+j,ScoreStepSource07(step,snapshot),ScoreStepCalculation07(step),ScoreValue07(step.after),23,UiKit.Paper);
                }
            }
            var title=UiKit.Text(list,"DrawerGlobalTitle","整手计算",8,cursor,contentWidth,48,28,UiKit.Gold);cursor+=FitHeight(title,48)+10;
            for(int i=0;i<trace.global.Length;i++){var step=trace.global[i];Row("Global"+i,ScoreStepSource07(step,snapshot),ScoreStepCalculation07(step),ScoreValue07(step.after),25,UiKit.Paper);}
            string triggered=b.triggeredControllerIds.Length==0?"没有卡牌生效":string.Join(" · ",b.triggeredControllerIds.Select(NameOf));
            var footer=UiKit.Text(list,"Triggered",triggered+"\n复制、染色等永久效果在计分后才发生，不影响这一手的分数。"+(effects.Length>0?(historical?"\n\n计分后发生了：\n":"\n\n提交后会发生：\n")+effects:""),8,cursor+12,contentWidth,260,25,UiKit.Teal);
            list.sizeDelta=new Vector2(list.sizeDelta.x,cursor+FitHeight(footer,260)+36);
            float bw=(w-48-36)/4,by=h-76;
            // v1.1 (W09): the copied formula is the decimal readout ("≈" where rounded), so the label no longer says "精确".
            UiKit.Button(p,"CopyExactScore","复制公式",24,by,bw,64,()=>
            {
                GUIUtility.systemCopyBuffer=ScoreFormula07(b);
                var label=p.Find("CopyExactScore")?.GetComponentInChildren<TextMeshProUGUI>();if(label!=null)label.text="已复制";
            });
            UiKit.Button(p,"FullScoreDetails","逐步明细",24+bw+12,by,bw,64,()=>ShowBreakdownComplete07(snapshot,b));
            if(historical&&HasCommittedPresentationDetails07(snapshot.handId))UiKit.Button(p,"ReplayReceipt","重播动画",24+2*(bw+12),by,bw,64,()=>{CloseModal();ReplayLastPresentation07();},!IsBusy);
            if(historical&&HasCommittedPresentationDetails07(snapshot.handId))UiKit.Button(p,"ReceiptEffectsDetails","计分记录",24+3*(bw+12),by,bw,64,ShowCommittedPresentationDetails07,!IsBusy);
        }
        // W16b: only exact identity factors are omitted. The copied and visible formula use the same frozen score.
        private string ScoreFormula07(ScoreBreakdown b)
        {
            var formula = new System.Text.StringBuilder("基础 " + Big(b.baseTotal));
            void Factor100(string label,int units)
            {
                if (units != 100) formula.Append(" × ").Append(label).Append(" ").Append(Factor(units));
            }
            void Ratio(string label,string top,string bottom)
            {
                var n=ExactScoreMath.Parse(top);
                var d=ExactScoreMath.Parse(bottom);
                if (n != d) formula.Append(" × ").Append(label).Append(" ").Append(PlayerNumberFormat.Ratio(n,d,PlayerNumberFormat.Unlimited));
            }
            Factor100("牌型",b.totalHandMult100);
            Factor100("颜色",b.colorMult100);
            Factor100("红组",b.redMult100);
            Ratio("卡牌",b.controllerMultiplierNumerator,b.controllerMultiplierDenominator);
            if (b.enchantMultiplierCount > 0)
                formula.Append(" × 附魔 ").Append(PlayerNumberFormat.Integer(EnchantFactor07(b.enchantMultiplierCount),PlayerNumberFormat.Unlimited));
            if (b.globalFactors != null)
                foreach (var factor in b.globalFactors) Ratio(NameOf(factor.abilityId),factor.numerator,factor.denominator);
            return formula.Append("\n= ").Append(Big(b.score)).Append("（最后去掉小数）").ToString();
        }

        private static string DiceLayers16(FaceState face,int rolled,string rolledColor,int? scored,string scoredColor)
        {
            bool differs=face.permanentPip!=rolled || face.baseColor!=rolledColor ||
                (scored.HasValue && (scored.Value!=rolled || scoredColor!=rolledColor));
            if (!differs) return "点数 "+rolled+" · "+ColorName07(rolledColor);
            string layer(int value,string color)=>value+" 点"+ColorName07(color)+"色";
            return "骰面原本 "+layer(face.permanentPip,face.baseColor)+" · 这次掷出 "+layer(rolled,rolledColor)+
                (scored.HasValue?" · 计分按 "+layer(scored.Value,scoredColor):"");
        }

        private string ScoreEffects16(SubmissionSnapshot snapshot,bool historical)
        {
            if(!historical)return string.Join("\n",Session.PreviewSubmissionEffects().Select(effect=>EffectText(effect,snapshot)));
            var receipt=ViewState.processedReceipts.LastOrDefault(r=>r.command?.kind==CommandKind.Submit && r.submission?.snapshot?.handId==snapshot.handId);
            return receipt?.effects==null?"":string.Join("\n",receipt.effects.Select(effect=>EffectText(effect,snapshot)));
        }

        private static string ScoreContribution16(ScoreBreakdown score,int index)
            =>score.sealedDice!=null && index<score.sealedDice.Length && score.sealedDice[index]
                ?"被封印：计 0":"这颗骰子计 "+score.pipContributions[index]+" 分";
        /// <summary>An exact step value as a decimal with every digit kept (no abbreviation and no markup in the breakdown).</summary>
        private static string ScoreValue07(string exact)=>PlayerNumberFormat.Exact(exact,PlayerNumberFormat.Unlimited);
        private static string ScoreStepCalculation07(ScoreStep step)=>step.operation=="floor"?ScoreValue07(step.before)+" → 去掉小数":ScoreValue07(step.before)+" "+step.operation+" "+ScoreValue07(step.operand);
    }
}
