using System;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private string previewChoiceKey07;
        private bool previewRisk07;
        private string noticeText07="";
        private string terminalCuePending07,terminalCueId07;
        private void TickTerminalCue07()
        {
            if(terminalCuePending07==null||IsBusy)return;
            terminalCueId07=Session.State.runId+"/end/"+Session.ActionSequence;
            presentationAudio07?.PlayCue("SFX_RUN_END",receiptId:terminalCueId07,semanticVariant:terminalCuePending07);terminalCuePending07=null;
        }
        private void CompleteRunResult07()
        {
            main.GetComponent<Art07ScreenReveal>()?.Complete();
            if(terminalCueId07!=null)presentationAudio07?.StopReceipt(terminalCueId07);terminalCuePending07=null;
        }
        private string notice
        {
            get=>noticeText07;
            set=>QueueNotice07(value);
        }
        private bool PreviewRisk07
        {
            get
            {
                string key=ViewState.runId+"/"+ViewState.stage.stageId;
                if(previewChoiceKey07!=key){previewChoiceKey07=key;previewRisk07=ViewState.stage.risk;}
                return previewRisk07;
            }
        }
        private void ChoosePreview07(bool risk){_ = PreviewRisk07;previewRisk07=risk;Refresh();}
        private void StartPreviewStage07()
        {
            if(PreviewRisk07!=Session.State.stage.risk)
            {
                var chosen=Send(new SessionCommand{kind=CommandKind.ChooseChallenge,risk=PreviewRisk07});
                if(!chosen.accepted||chosen.pendingSave)return;
            }
            Send(new SessionCommand{kind=CommandKind.StartStage});
        }
        private void DrawNotice07()=>DrawToasts07();
        private void TickNotice07()=>TickToasts07();
        private string QualifiedSummary07()
        {
            var r=Session.PreviewStageSettlement();
            return r==null?"目标已达成":"现在结束 +"+(r.goldAfter-r.goldBefore)+" 金币";
        }
        private void AttachEndStageSummary07()
        {
            if(ViewState?.stage?.qualified!=true||ViewState.hand==null)return;
            var button=main.Find("EndStage")?.GetComponent<Button>();if(button==null)return;
            var r=Session.PreviewStageSettlement();if(r==null)return;
            int pigs=ViewSources.Count(x=>x.abilityId=="C01");
            // v1.1 glossary (text proposal 2.2): 过关奖励, 剩余提交奖励, 利息 written as its rule.
            long rate=SettlementRewardTerms15.InterestRate(r,Config.Controller("C01").parameters.extraInterestPerTen);
            string text="过关奖励 +"+r.baseReward+"\n剩余提交奖励 +"+r.unusedSubmissionReward+"\n利息 +"+r.interest+"（每 "+Config.interestDivisor+" 金币 +"+PlayerNumberFormat.Gold(rate)+"）";
            foreach(var chip in SettlementRewardTerms15.ControllerChips(r))text+="\n"+chip.Text;
            if(pigs>0)text+="\n继续提交：每次提交收入 +"+(pigs*Config.Controller("C01").parameters.goldPerValidSubmission)+"，不计入现在结束的奖励。";
            text+="\n本手没提交的骰子不计分；已用的道具与投掷不退。";
            var hover=UiKit.Component<Art07HoverTip>(button.gameObject);
            hover.show=()=>
            {
                if(modal||IsBusy)return;
                // Anchor the paper above its button; grow upward for the wrapped lines actually earned by this hand.
                var end=L11.end;
                float width=Large11?824:688,textWidth=width-40;
                var p=UiKit.Frame(overlay,"EndStagePreview07",end.xMax-width,0,width,376,UiKit.Paper,UiKit.Ink);
                var title=UiKit.Text(p,"Title",QualifiedSummary07(),20,14,textWidth,52,27,UiKit.Ink);
                float titleH=Mathf.Ceil(title.GetPreferredValues(title.text,textWidth,0).y)+8;
                title.rectTransform.sizeDelta=new Vector2(textWidth,titleH);
                float bodyTop=14+titleH+12;
                var body=UiKit.Text(p,"Body",text,20,bodyTop,textWidth,280,Large11?18:23,UiKit.Ink);
                float bodyH=Mathf.Ceil(body.GetPreferredValues(text,textWidth,0).y)+8;
                body.rectTransform.sizeDelta=new Vector2(textWidth,bodyH);
                float height=Mathf.Max(376,bodyTop+bodyH+18);
                p.sizeDelta=new Vector2(width,height);
                p.anchoredPosition=new Vector2(end.xMax-width,-(end.yMin-18-height));
            };
            hover.hide=()=>{var p=overlay.Find("EndStagePreview07");if(p!=null)p.gameObject.SetActive(false);};
        }
    }
}
