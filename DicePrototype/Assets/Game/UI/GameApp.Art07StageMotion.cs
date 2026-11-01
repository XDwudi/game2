using System;
using System.Collections;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private Art07TransactionMotion stageCardExit07;
        private string stageCardPreparedId07,stageCardPlayedId07;
        private int stageCardExitGeneration07;
        public bool StageCardExitPlaying07=>stageCardExit07?.IsPlaying==true;
        public float StageCardExitElapsed07=>stageCardExit07?.Elapsed??0;
        public float StageCardExitDuration07=>stageCardExit07?.Duration??0;
        public int StageCardExitGraphicCount07=>stageCardExit07?.CapturedGraphicCount??0;

        private RectTransform[] StageCardRoots07()=>new[]{main.Find("NormalCard") as RectTransform,main.Find("RiskCard") as RectTransform}
            .Where(r=>r!=null&&r.gameObject.activeInHierarchy).ToArray();
        private void BindStageCardsEntrance07()
        {
            UiKit.Component<Art07StageCardsMotion>(main.gameObject).Bind(ViewState.runId,ViewState.stage.stageId,reduced,instant07,()=>modal||inMenu,StageCardRoots07());
        }
        private void EnsureStageCardExit07()
        {
            if(stageCardExit07!=null)return;
            var root=UiKit.Rect(canvas.transform,"StageCardExit07",0,0,1920,1080);
            var group=UiKit.Component<CanvasGroup>(root.gameObject);group.interactable=false;group.blocksRaycasts=false;
            stageCardExit07=root.gameObject.AddComponent<Art07TransactionMotion>();
        }
        private void PrepareStageCardExit07(SessionCommand command)
        {
            if(command?.kind!=CommandKind.StartStage||Session==null||Session.Busy||Session.HasPendingSave||ViewState?.phase!="StagePreview")return;
            EnsureStageCardExit07();stageCardExit07.Finish(false);stageCardPreparedId07=null;
            ((RectTransform)stageCardExit07.transform).anchoredPosition=main.anchoredPosition;
            // Only challenge cards are copied. Top controllers and all next-state input remain live.
            try{stageCardExit07.Prepare(StageCardRoots07());stageCardPreparedId07=command.commandId;}
            catch(Exception ex){stageCardExit07.DiscardPrepared();Debug.LogWarning("关卡卡面捕获不可用，直接显示真实入关状态："+ex.Message);}
        }
        private void DiscardStageCardExit07()
        {
            if(stageCardExit07?.IsPlaying==true)return;
            stageCardExit07?.DiscardPrepared();stageCardPreparedId07=null;
        }
        private void PresentStageCardExit07(SessionCommand command,CommandResult result,RunState before,RunState after)
        {
            if(command.kind!=CommandKind.StartStage)return;
            if(!result.accepted||result.pendingSave||result.duplicate||before?.phase!="StagePreview"||after?.phase!="HandReady"||before.runId!=after.runId||before.stage.stageId!=after.stage.stageId)
            {DiscardStageCardExit07();return;}
            if(stageCardPreparedId07!=command.commandId||stageCardPlayedId07==command.commandId||instant07)
            {DiscardStageCardExit07();return;}
            stageCardPreparedId07=null;stageCardPlayedId07=command.commandId;
            var root=(RectTransform)stageCardExit07.transform;root.anchoredPosition=main.anchoredPosition;
            root.SetSiblingIndex(Math.Min(canvas.transform.childCount-1,main.GetSiblingIndex()+1));overlay.SetAsLastSibling();
            stageCardExit07.Begin("stage_preview_exit",.22f,.22f,false,fast,reduced,null);
            StartCoroutine(TickStageCardExit07(++stageCardExitGeneration07,after.runId,after.stage.stageId));
        }
        private IEnumerator TickStageCardExit07(int generation,string runId,string stageId)
        {
            double lastClock=Time.realtimeSinceStartupAsDouble;
            while(stageCardExit07!=null&&stageCardExit07.IsPlaying&&generation==stageCardExitGeneration07)
            {
                if(inMenu||Session?.State?.runId!=runId||Session.State.stage.stageId!=stageId){stageCardExit07.Finish(false);yield break;}
                yield return null;
                double now=Time.realtimeSinceStartupAsDouble;float delta=(float)Math.Max(0,now-lastClock);lastClock=now;
                if(generation==stageCardExitGeneration07)stageCardExit07.Tick(delta,modal);
            }
        }
    }
}
