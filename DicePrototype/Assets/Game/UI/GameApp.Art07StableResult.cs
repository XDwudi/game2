using System;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private bool stableResultRequested07;
        private int stableSourceMarks07;
        public bool StablePresentationResultVisible07=>!inMenu&&stableResultRequested07&&receipt07!=null&&ViewState!=null&&
            (ViewState.phase=="HandReady"||ViewState.phase=="Selecting")&&ViewState.runId==receipt07.RunId&&ViewState.lastSubmission?.snapshot?.handId==receipt07.HandId;
        public int StablePresentationSourceMarks07=>StablePresentationResultVisible07?stableSourceMarks07:0;
        private void CompletePresentationView07(PresentationReceipt completed)
        {
            FinishReceiptTailOnSkip07();
            ClearPresentationEffects07();
            stableResultRequested07=completed!=null&&receipt07!=null&&completed.GameReceiptId==receipt07.GameReceiptId;
            Session.Busy=false;ViewState=null;Refresh();
            // Instant commits the HUD and physical dice pose in the same frame.
            // A normal layout tween here would briefly leave dice in now-empty slots.
            if(presentationPlayer07.Sequence?.Options.Speed==PresentationSpeed.Instant)table.FinishLayoutTransitions();
        }
        // A committed result remains until the next successful action creates a different
        // throw/selection. Rejected requests, pause, settings and view refresh never clear it.
        private void AdvanceStablePresentation07(SessionCommand command)
        {
            bool replaces=command.kind==CommandKind.NewRun||command.kind==CommandKind.ApplyDebugScene||
                command.kind==CommandKind.Submit||command.kind==CommandKind.Roll||command.kind==CommandKind.ConfirmLocks||
                command.kind==CommandKind.UseItem&&command.targetPips!=null&&command.targetPips.Length==2;
            if(replaces){stableResultRequested07=false;stableSourceMarks07=0;recoveredResultRun07=null;recoveredResultHand07=null;}
        }
        private bool DrawStablePresentationScore07(RectTransform panel,float width,float height)
        {
            stableSourceMarks07=0;if(!StablePresentationResultVisible07)return false;
            bool compact=Compact07,thin=height<110;
            UiKit.Text(panel,"StableResultCaption07","上一手得分 · 已保存",20,12,width-680,42,compact?18:24,UiKit.Paper);
            var score=UiKit.Text(panel,"StableResultScore07",CompactScore(receipt07.ScoreDecimal),width-660,10,630,68,compact?28:48,UiKit.Gold,TextAlignmentOptions.MidlineRight);
            if(art07NumberFont!=null)score.font=art07NumberFont;
            score.rectTransform.pivot=new Vector2(1,.5f);score.rectTransform.anchoredPosition=new Vector2(width-30,compact?-34:-44);
            string multiplier="总倍率 ×"+DisplayFraction07(receipt07.MultiplierNumeratorDecimal+(receipt07.MultiplierDenominatorDecimal=="1"?"":"/"+receipt07.MultiplierDenominatorDecimal));
            string chain=receipt07.ChainCount==0?"":" · 连锁 "+receipt07.ChainCount;
            string zeroReason=receipt07.Events.Any(e=>e.AbilityId=="C49"&&e.IsScoring&&e.Operand=="0")?" · 千年老二 ×0，没有有效5点":"";
            var post=receipt07.Events.Where(e=>e.Phase==PresentationPhase.PostScore).ToArray();
            string changes=post.Length==0?"":"\n后置："+PostSummary07(new PresentationBeat(PresentationPhase.PostScore,0,0,null,true,post));
            var detail=UiKit.Text(panel,"StableResultFactors07",multiplier+chain+zeroReason+changes,20,thin?58:compact?65:82,width-(thin?1180:290),thin?40:compact?48:64,thin?20:compact?17:21,UiKit.Muted);
            detail.textWrappingMode=TextWrappingModes.Normal;detail.overflowMode=TextOverflowModes.Ellipsis;
            UiKit.FlatButton(panel,"StablePresentationDetails07","完整记录",width-(thin?910:250),thin?44:compact?58:84,230,60,ShowCommittedPresentationDetails07);
            // These are persistent read-only markers, separate from the active FX pool.
            var marks=UiKit.Rect(main,"StableResultSourceMarks07",0,0,1920,1080);
            foreach(string id in receipt07.Events.Where(e=>e.IsScoring).SelectMany(e=>e.SourceEmitterIds).Distinct(StringComparer.Ordinal))
            {
                if(!TryCardBounds07(id,out var bounds))continue;
                UiKit.Outline(marks,"Source_"+id,bounds.xMin-2,bounds.yMin-2,bounds.width+4,bounds.height+4,UiKit.Gold,2);
                UiKit.Text(marks,"Check_"+id,"✓",bounds.xMin+4,bounds.yMax-28,26,24,17,UiKit.Ink);stableSourceMarks07++;
            }
            return true;
        }
    }
}
