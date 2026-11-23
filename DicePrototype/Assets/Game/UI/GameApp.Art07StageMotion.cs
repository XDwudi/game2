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
        private BossEntryMotion15 bossEntry15;
        public bool BossEntryPlaying15 => bossEntry15 != null && bossEntry15.Playing;
        private string lastBossEntry15;

        private void BeginBossEntry15(SessionCommand command, CommandResult result, RunState before, RunState after)
        {
            if (command.kind != CommandKind.StartStage || !result.accepted || result.duplicate || result.pendingSave ||
                before?.phase != "StagePreview" || after?.phase != "HandReady" || after.stage.stageKind != "boss" ||
                after.stage.modifierNullified || string.IsNullOrEmpty(after.stage.modifierId) || instant07 ||
                lastBossEntry15 == command.commandId) return;
            lastBossEntry15 = command.commandId;
            bossEntry15 = UiKit.Component<BossEntryMotion15>(gameObject);
            bossEntry15.Begin(reduced, fast, PaintBossEntry15,
                () => !inMenu && ViewState?.runId == after.runId && ViewState.stage.stageId == after.stage.stageId &&
                    PlayPhase11 && !instant07 && reduced == bossEntry15.Reduced && fast == bossEntry15.Fast,
                () => modal);
            BindBossEntryButtons15();
        }

        private void BindBossEntryButtons15()
        {
            if (!BossEntryPlaying15) return;
            foreach (var b in main.GetComponentsInChildren<UnityEngine.UI.Button>(false))
            {
                b.onClick.RemoveListener(CompleteBossEntry15);
                b.onClick.AddListener(CompleteBossEntry15);
            }
        }

        private void CompleteBossEntry15()
        {
            if (BossEntryPlaying15) bossEntry15.Complete();
        }

        private void PaintBossEntry15(float shift, float stamp, float alpha, float mood)
        {
            // During scene unload Unity can destroy the canvas before it disables the motion.
            if (main == null) return;
            var card = main.Find("StageCard11") as RectTransform;
            if (card != null)
            {
                card.anchoredPosition = new Vector2(L11.stage.x + shift, -L11.stage.y);
                var mark = card.Find("BossModifier10/BossEmblem11");
                if (mark != null) mark.localScale = Vector3.one * stamp;
                UiKit.Component<CanvasGroup>(card.gameObject).alpha = alpha;
            }
            SetTableMood11(PlayBackdrop11 && PlayPhase11 && BossActive11 ? mood : 0);
        }

        private Art07TransactionMotion stageCardExit07;
        private string stageCardPreparedId07,stageCardPlayedId07;
        private int stageCardExitGeneration07;
        public bool StageCardExitPlaying07=>stageCardExit07?.IsPlaying==true;
        public float StageCardExitElapsed07=>stageCardExit07?.Elapsed??0;
        public float StageCardExitDuration07=>stageCardExit07?.Duration??0;
        public int StageCardExitGraphicCount07=>stageCardExit07?.CapturedGraphicCount??0;

        private RectTransform[] StageCardRoots07()=>new[]{main.Find("CycleCard0") as RectTransform,main.Find("CycleCard1") as RectTransform,main.Find("CycleCard2") as RectTransform}
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
            if(command!=null && command.kind!=CommandKind.StartStage)CompleteBossEntry15();
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
            BeginBossEntry15(command,result,before,after);
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

    // A visual clock separate from the session and input locks. Only a freshly committed StartStage arms it.
    public sealed class BossEntryMotion15 : MonoBehaviour
    {
        public bool Playing { get; private set; }
        public bool Reduced { get; private set; }
        public bool Fast { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration => Reduced ? .12f : Fast ? .38f : .76f;
        public bool AutomaticTick { get; set; } = true;
        private Action<float,float,float,float> paint;
        private Func<bool> sameStage, paused;

        public void Begin(bool reduced, bool fast, Action<float,float,float,float> draw, Func<bool> same, Func<bool> pause)
        {
            Reduced=reduced; Fast=fast; paint=draw; sameStage=same; paused=pause;
            Elapsed=0; Playing=true; Paint();
        }
        public void Complete()
        {
            if(!Playing)return;
            Elapsed=Duration; Playing=false;
            var finalPaint = paint;
            paint = null;
            sameStage = null;
            paused = null;
            finalPaint?.Invoke(0,1,1,1);
        }
        public void Tick(float seconds)
        {
            if(!Playing)return;
            if(!sameStage()){Complete();return;}
            if(paused())return;
            Elapsed=Mathf.Min(Duration,Elapsed+Mathf.Max(0,seconds));
            if(Elapsed>=Duration)Complete();else Paint();
        }
        public void Paint()
        {
            if(!Playing)return;
            if(Reduced){paint(0,1,Mathf.Clamp01(Elapsed/.12f),1);return;}
            float t=Elapsed*(Fast?2:1);
            float remaining=1-Mathf.InverseLerp(.10f,.40f,t);
            float stamp=t<.40f?0:1-Mathf.InverseLerp(.40f,.76f,t);
            paint(420*remaining*remaining*remaining,1+.18f*stamp*stamp,1,Mathf.Clamp01(t/.40f));
        }
        private void Update()
        {
            if(!Playing||!AutomaticTick)return;
            var key=UnityEngine.InputSystem.Keyboard.current;
            if(!paused() && key!=null && (key.enterKey.wasPressedThisFrame||key.numpadEnterKey.wasPressedThisFrame)){Complete();return;}
            Tick(Time.unscaledDeltaTime);
        }
        private void OnApplicationFocus(bool focused){if(!focused)Complete();}
        private void OnApplicationPause(bool value){if(value)Complete();}
        private void OnDisable(){Complete();}
    }

}
