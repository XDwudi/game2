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
            tutorial=false;inMenu=false;
            notice="已读取存档，继续上次的进度。";
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
            var well=ScoreWell072(panel);
            var score=UiKit.Text(panel,"RecoveredScore07",CompactScore(record.breakdown.score),width-660,10,630,68,compact?28:48,UiKit.Gold,TextAlignmentOptions.MidlineRight);
            if(art07NumberFont!=null)score.font=art07NumberFont;
            FitScoreWell072(well,score,width,compact?34:44,compact?24:36);
            score.rectTransform.pivot=new Vector2(1,.5f);score.rectTransform.anchoredPosition=new Vector2(width-30,compact?-34:-44);
            UiKit.Text(panel,"RecoveredState07","结果已保存 · 本手尚未投掷",20,thin?58:compact?65:82,width-(thin?1180:290),thin?40:compact?48:64,thin?20:compact?17:21,UiKit.Muted);
            TrimRailButton072(UiKit.FlatButton(panel,"RecoveredDetails07","只读收据",width-(thin?910:250),thin?44:compact?58:84,230,60,()=>ShowBreakdown(record.snapshot,record.breakdown)),thin);
            return true;
        }
    }
}
