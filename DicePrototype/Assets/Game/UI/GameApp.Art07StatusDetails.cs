using System;
using System.Collections;
using System.Linq;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        // Cold-load presentation state is derived from the committed snapshot only.
        // No presentation receipt is invented, and no effects or income are replayed.
        private string recoveredResultRun07,recoveredResultHand07;
        public bool RecoveredPresentationResultVisible07=>!inMenu&&ViewState?.runId==recoveredResultRun07&&
            recoveredResultHand07!=null&&ViewState?.lastSubmission?.snapshot?.handId==recoveredResultHand07&&
            ViewState.hand!=null&&!ViewState.hand.hasRolled&&ViewState.phase=="HandReady";
        private void RestoreResultView07()
        {
            var s=Session?.State;
            if(s?.phase!="HandReady"||s.hand==null||s.hand.hasRolled||s.lastSubmission?.snapshot?.stageId!=s.stage.stageId)return;
            recoveredResultRun07=s.runId;recoveredResultHand07=s.lastSubmission.snapshot.handId;
        }
        private void ContinueRun07()
        {
            if(!StartupReady07)return;
            if(HasIncompatibleSave07){OpenIncompatibleSave07();return;}
            tutorial=false;inMenu=false;
            notice=RecoveredPresentationResultVisible07?"已恢复上一手结果。":"已恢复最近一次成功操作。";
            Refresh();
            var veil=UiKit.Box(main,"ContinueVeil07",0,0,1920,1080,UiKit.Background).GetComponent<UnityEngine.UI.Image>();
            veil.raycastTarget=false;veil.transform.SetAsLastSibling();
            StartCoroutine(FadeContinue07(veil,reduced?.08f:.18f));
        }
        private IEnumerator FadeContinue07(UnityEngine.UI.Image veil,float duration)
        {
            float elapsed=0;
            while(veil!=null&&veil.gameObject.activeInHierarchy&&elapsed<duration)
            {
                elapsed+=Time.unscaledDeltaTime;var color=UiKit.Background;color.a=1-Mathf.Clamp01(elapsed/duration);veil.color=color;yield return null;
            }
            if(veil!=null)veil.gameObject.SetActive(false);
        }
        private bool DrawRecoveredScore07(RectTransform panel,float width,float height)
        {
            if(!RecoveredPresentationResultVisible07)return false;
            var record=ViewState.lastSubmission;bool compact=Compact07,thin=height<110;
            UiKit.Text(panel,"RecoveredCaption07","已恢复上一手结果",20,12,width-680,42,compact?18:24,UiKit.Paper);
            var score=UiKit.Text(panel,"RecoveredScore07",CompactScore(record.breakdown.score),width-660,10,630,68,compact?28:48,UiKit.Gold,TextAlignmentOptions.MidlineRight);
            if(art07NumberFont!=null)score.font=art07NumberFont;
            score.rectTransform.pivot=new Vector2(1,.5f);score.rectTransform.anchoredPosition=new Vector2(width-30,compact?-34:-44);
            UiKit.Text(panel,"RecoveredState07","结果已保存 · 本手尚未投掷",20,thin?58:compact?65:82,width-(thin?1180:290),thin?40:compact?48:64,thin?20:compact?17:21,UiKit.Muted);
            UiKit.FlatButton(panel,"RecoveredDetails07","只读收据",width-(thin?910:250),thin?44:compact?58:84,230,60,()=>ShowBreakdown(record.snapshot,record.breakdown));
            return true;
        }
        private void DrawQualifiedHeader07(float x,float y,float width,float height)
        {
            var p=UiKit.Frame(main,"Stats07",x,y,width,height,UiKit.Background,UiKit.PanelLight);
            int n=ViewState.stage.stageIndex+1;
            UiKit.Text(p,"Brand","骰子构筑",18,8,width-36,52,26,UiKit.Paper);
            UiKit.Text(p,"Stage",$"第 {n} 关 · 第 {(n-1)/3+1} 章",18,60,width-36,42,20,UiKit.Paper);
            QualifiedStat07(p,"Target","目标分数",CompactScore(ViewState.stage.target),106,width,UiKit.Red);
            // A dedicated 52px stamp strip stays clear of the real score title/value.
            // It is reserved before the first stamp appears and after receipt handoff.
            UiKit.Rect(p,"QualifiedStampSlot07",24,196,272,52);
            string displayedStageScore=QualifiedStampPlayingForCurrentStage07&&IsPresenting07&&lastPresentationAfter07?.stage?.stageId==ViewState.stage.stageId
                ?lastPresentationAfter07.stage.score:ViewState.stage.score;
            QualifiedStat07(p,"StageScore","本关累计",CompactScore(displayedStageScore),252,width,UiKit.Blue);
            if(qualifiedStamp07==null||qualifiedStage07!=ViewState.stage.stageId)
            {
                var badge=UiKit.Frame(p,"QualifiedSavedBadge07",width-44,256,28,28,UiKit.Gold,UiKit.Ink);
                UiKit.Text(badge,"Check","✓",0,0,28,28,16,UiKit.Ink,TextAlignmentOptions.Center);
            }
            QualifiedStat07(p,"Money","金币",ViewState.gold.ToString(),342,width,UiKit.Gold);
            // A final submission may enter S09 during the nonblocking stamp tail.
            // Keep the score frame stable, but never show an old hand's action budgets there.
            bool activeHand=ViewState.hand!=null&&(ViewState.phase=="HandReady"||ViewState.phase=="Selecting");
            if(activeHand)DrawBudgetCounters07(p,432);
            if(activeHand)
            {
                var r=Session.PreviewStageSettlement();
                UiKit.Text(p,"Qualified","现在结束的奖励",18,508,width-36,40,18,UiKit.Gold);
                if(r!=null)
                {
                    string[] lines={"基础奖励  +"+r.baseReward,"剩次奖励  +"+r.unusedSubmissionReward,"预计利息  +"+r.interest};
                    for(int i=0;i<lines.Length;i++)UiKit.Text(p,"QualifiedReward"+i,lines[i],18,548+i*40,width-36,40,18,UiKit.Paper);
                }
                int income=ViewSources.Count(s=>s.abilityId=="C01")*Config.Controller("C01").parameters.goldPerValidSubmission;
                UiKit.Text(p,"QualifiedContinueIncome","续交 C01  +"+income+" / 次",18,660,width-36,40,18,UiKit.Gold);
            }
            if(ViewState.debugWorkspace)UiKit.Box(main,"DebugMark",0,0,1920,6,UiKit.Violet);
        }
        private void QualifiedStat07(Transform parent,string id,string label,string value,float y,float width,Color color)
        {
            UiKit.Outline(parent,id+"Frame",12,y,width-24,86,UiKit.PanelLight);
            UiKit.Text(parent,id+"Label",label,26,y+2,width-52,34,16,UiKit.Muted);
            var t=UiKit.Text(parent,id,value,26,y+34,width-52,50,23,color);
            t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;
        }
    }
}
