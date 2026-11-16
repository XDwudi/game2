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
            string text="过关奖励 +"+r.baseReward+"\n剩余提交奖励 +"+r.unusedSubmissionReward+"\n利息 +"+r.interest+"（每 "+Config.interestDivisor+" 金币 +1）"+(r.controllerIncome>0?"\n铁饭碗 +"+r.controllerIncome:"");
            if(pigs>0)text+="\n继续提交：每次提交收入 +"+(pigs*Config.Controller("C01").parameters.goldPerValidSubmission)+"，不计入现在结束的奖励。";
            text+="\n本手没提交的骰子不计分；已用的道具与投掷不退。";
            var hover=UiKit.Component<Art07HoverTip>(button.gameObject);
            hover.show=()=>
            {
                if(modal||IsBusy)return;
                // V1.1 (W11): above the end-stage button at the bottom right, clear of the action row.
                var p=UiKit.Frame(overlay,"EndStagePreview07",1200,568,688,376,UiKit.Paper,UiKit.Ink);
                UiKit.Text(p,"Title",QualifiedSummary07(),20,14,648,52,27,UiKit.Ink);
                var body=UiKit.Text(p,"Body",text,20,78,648,280,UiKit.FontScale>1?18:23,UiKit.Ink);body.overflowMode=TextOverflowModes.Ellipsis;
            };
            hover.hide=()=>{var p=overlay.Find("EndStagePreview07");if(p!=null)p.gameObject.SetActive(false);};
        }
    }
}
