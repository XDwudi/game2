using System;
using System.Collections.Generic;
using DiceCraft.Core;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private ScorePreview preparedLockPreview07, frozenLockPreview07;
        private SubmissionSnapshot preparedLockSnapshot07, frozenLockSnapshot07;
        private bool lockVisual07, rollDotsActive07;
        private float lockElapsed07,lockDuration07,lockSpeed07=1,rollDotsElapsed07,rollDotsSpeed07=1;
        private Art07RollButtonDots rollDots07;
        private readonly Dictionary<string,LockMark07> lockMarks07=new Dictionary<string,LockMark07>(10);
        private sealed class LockMark07 { public RectTransform rect;public CanvasGroup alpha;public Vector2 rest; }
        public bool LockVisualPlaying07=>lockVisual07;
        public float LockVisualElapsed07=>lockElapsed07;
        public float LockVisualDuration07=>lockDuration07;
        public bool RollButtonDotsPlaying07=>rollDotsActive07;

        private void PrepareDiceInteraction07(SessionCommand command)
        {
            // A failed durable commit owns this capture until RetrySave consumes it or Abandon releases it.
            // Later rejected commands must neither replace nor erase the pre-command C53 range/snapshot.
            if(Session!=null&&Session.HasPendingSave)return;
            DiscardPreparedDiceInteraction07();
            if(command.kind!=CommandKind.ConfirmLocks||IsBusy)return;
            // Capture before Execute; pending choices are not included in the old preview.
            // These APIs are pure and C53 remains a deterministic range, never a fresh draw.
            preparedLockPreview07=Session.PreviewScoreRange();preparedLockSnapshot07=Session.PreviewSnapshot();
        }
        private void DiscardPreparedDiceInteraction07()
        {
            if(Session!=null&&Session.HasPendingSave)return;
            preparedLockPreview07=null;preparedLockSnapshot07=null;
        }
        private bool TryPresentLockedDice07(SessionCommand command,CommandResult result)
        {
            if(command.kind!=CommandKind.ConfirmLocks)return false;
            string[] ordered=result.receipt.command.dieIds;
            frozenLockPreview07=preparedLockPreview07;frozenLockSnapshot07=preparedLockSnapshot07;DiscardPreparedDiceInteraction07();
            presentationAudio07.PlayCue("SFX_LOCK",receiptId:result.receipt.commandId);
            if(ordered.Length==0||instant07||reduced)
            {
                frozenLockPreview07=null;frozenLockSnapshot07=null;
                bool previous=table.ReducedMotion;table.ReducedMotion=true;
                try{Refresh();table.FinishLayoutTransitions();}finally{table.ReducedMotion=previous;}
                return true;
            }
            lockVisual07=true;lockElapsed07=0;lockSpeed07=fast?2:1;
            lockDuration07=(DiceTable.LockMoveSeconds07+(ordered.Length-1)*DiceTable.LockStaggerSeconds07)/lockSpeed07;
            lockMarks07.Clear();Session.Busy=true;table.BeginLockLayout07(ordered);
            Refresh();table.TickLockLayout07(0);UpdateLockMarks07();return true;
        }
        private ScorePreview DisplayScorePreview07()=>lockVisual07?frozenLockPreview07:Session.PreviewScoreRange();
        private SubmissionSnapshot DisplayScoreSnapshot07()=>lockVisual07?frozenLockSnapshot07:Session.PreviewSnapshot();
        private void BindLockArrival07(RectTransform mark,string id)
        {
            var group=UiKit.Component<CanvasGroup>(mark.gameObject);group.interactable=false;group.blocksRaycasts=false;
            var value=new LockMark07{rect=mark,alpha=group,rest=mark.anchoredPosition};
            lockMarks07[id]=value;UpdateLockMark07(id,value);
        }
        private void UpdateLockMarks07(){foreach(var pair in lockMarks07)UpdateLockMark07(pair.Key,pair.Value);}
        private void UpdateLockMark07(string id,LockMark07 mark)
        {
            if(mark.rect==null||mark.alpha==null)return;
            float t=lockVisual07?Mathf.Clamp01((table.LockProgress07(id)-.75f)*4):1;
            mark.alpha.alpha=t;mark.rect.anchoredPosition=mark.rest+Vector2.up*(4*(1-t));
        }
        private void BeginRollButtonFeedback07(bool firstRoll)
        {
            rollDotsElapsed07=0;rollDotsSpeed07=fast?2:1;
            rollDotsActive07=firstRoll&&!instant07&&!reduced;
        }
        private void BindRollButtonFeedback07(UnityEngine.UI.Button button)
        {
            rollDots07=UiKit.Component<Art07RollButtonDots>(button.gameObject);rollDots07.Bind(button);
            rollDots07.Evaluate(rollDotsElapsed07,rollDotsActive07,reduced);
        }
        private void TickDiceInteraction07(float seconds)
        {
            if(modal)return;
            if(rollDotsActive07)
            {
                rollDotsElapsed07+=Math.Max(0,seconds)*rollDotsSpeed07;
                if(rollDotsElapsed07>=.18f)rollDotsActive07=false;
                if(rollDots07!=null)rollDots07.Evaluate(rollDotsElapsed07,rollDotsActive07,reduced);
            }
            if(!lockVisual07)return;
            lockElapsed07=Math.Min(lockDuration07,lockElapsed07+Math.Max(0,seconds));
            table.TickLockLayout07(lockElapsed07*lockSpeed07);UpdateLockMarks07();
            if(lockElapsed07>=lockDuration07)CompleteDiceInteraction07();
        }
        // All existing skip/focus/settings/return paths already route through SkipVisual.
        private bool CompleteDiceInteraction07()
        {
            rollDotsActive07=false;if(rollDots07!=null)rollDots07.Evaluate(.18f,false,reduced);
            if(!lockVisual07)return false;
            table.FinishLockLayout07();lockElapsed07=lockDuration07;lockVisual07=false;
            frozenLockPreview07=null;frozenLockSnapshot07=null;UpdateLockMarks07();lockMarks07.Clear();
            Session.Busy=false;Refresh();return true;
        }
    }
}
