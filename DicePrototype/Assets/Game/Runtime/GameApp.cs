using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DiceCraft
{
    public sealed partial class GameApp : MonoBehaviour
    {
        public TextAsset configAsset;
        public TMP_FontAsset uiFont;
        public DiceTable table;
        public Canvas canvas;
        public Sprite[] contentIcons;
        public Sprite[] enchantIcons;
        public AudioClip[] sounds;
        public static string SaveDirectoryOverride;
        public GameSession Session { get; private set; }
        public GameConfig Config { get; private set; }
        public RunState ViewState { get; private set; }
        private AtomicSaveStore saves;
        private GameSession displayedSession;
        private RectTransform main, overlay;
        private TMP_InputField seedField;
        private AudioSource audioSource;
        private bool inMenu=true, modal, fast, reduced, tutorial;
        private float volume=.45f;
        private int shopTab, tutorialStep;
        private string detailDieId;
        private readonly List<string> visibleDice = new List<string>();
        private readonly Dictionary<string,Vector2> dieCenters = new Dictionary<string,Vector2>();
        private static readonly Key[] DigitKeys={Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6,Key.Digit7,Key.Digit8,Key.Digit9,Key.Digit0};
        private Coroutine visual;
        public bool IsBusy => Session!=null && Session.Busy;
        public string SaveLocation => ViewState?.debugWorkspace==true&&!tutorial?(debugSaves?.SavePath??saves?.SavePath??startupSavePath07):(saves?.SavePath??startupSavePath07);

        void Awake()
        {
            Application.runInBackground=true;
            // Use the native 60 FPS cap as a safe fallback. The Windows scheduler owns
            // pacing only after successful installation, independently of game/probe state.
            QualitySettings.vSyncCount=0; Application.targetFrameRate=60;
            FramePacer.Install();
            UiKit.Font=uiFont;
            InitializeUiFontMaterials071();
            InitializeArtPreferences07();
            GameConfig.ValidateJson(configAsset.text);
            Config=JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(configAsset.text)); Config.Validate();
            volume=PlayerPrefs.GetFloat("volume",.45f); fast=PlayerPrefs.GetInt("fast",0)==1; reduced=PlayerPrefs.GetInt("reduced",0)==1;
            shopTab=PlayerPrefs.GetInt("shopTab",0);
            audioSource=gameObject.AddComponent<AudioSource>(); audioSource.volume=volume;
            InitializePresentation07();
            main=UiKit.Rect(canvas.transform,"Screen",0,0,1920,1080); overlay=UiKit.Rect(canvas.transform,"ModalLayer",0,0,1920,1080);
            InitializeDisplay();
            InitializeUiDice073();
            table.RestVariety11=true; // V1.1 (W11, B4): unthrown dice rest on varied faces

            InitializeTheme11();
            ApplyArtPreferences07();
            BeginStartupLoad07();
        }
        public void Refresh()
        {
            RebuildView();
            // Finish the layout/text meshes queued by this UI transaction now. Leaving
            // them until the render tail makes an otherwise paced frame arrive late.
            // The same work and content remain; no rule state or timing probe is changed.
            Canvas.ForceUpdateCanvases();
        }
        private void RebuildView()
        {
            if(!inMenu||!StartupReady07)ShowMenuTray17(false);
            if(!StartupReady07){DrawStartupLoad07();return;}
            // Pending selection and presentation flags do not mutate committed run data.
            // Reuse its detached snapshot until the session or accepted sequence changes.
            if(IsPresenting07 && lastPresentationBefore07!=null) ViewState=lastPresentationBefore07;
            else if(!ReferenceEquals(displayedSession,Session)||ViewState==null||ViewState.actionSequence!=Session.ActionSequence)
            { ViewState=Session?.State;displayedSession=Session;controllerSourceCache=null; }
            SyncLootPage15();
            SyncSettlementBackdropView07();
            UiKit.Clear(main); table.Hide(); visibleDice.Clear(); dieCenters.Clear(); controllerRects.Clear();lockedSlotRects07.Clear();
            if(inMenu || ViewState==null) { inMenu=true; HideHighScore073(); ApplyTableMood11(); ClearScore11(); Menu(); if(Session.HasPendingSave)SaveFailure(); return; }
            Header();
            switch(ViewState.phase)
            {
                case "StagePreview": StagePreview();break;
                case "Shop": Shop();break;
                case "StageSettlement": Settlement();break;
                case "RunWon": case "RunFailed": RunEnd();break;
                default: Gameplay();break;
            }
            DrawNavigation07();
            ApplyTableMood11();
            if(!PlayPhase11&&!IsPresenting07)ClearScore11();
            AlignQualifiedStamp07();
            DrawGuide07();
            DrawNotice07();
            AttachEndStageSummary07();
            /* legacy navigation disabled
            UiKit.Text(main,"Toast",notice,40,1030,1260,34,21,UiKit.Gold);
            if(DebugUnlocked&&IsDebugWorkspace)UiKit.Button(main,"DebugWorkspace","调试面板",36,1020,180,44,ShowDebugPanel);
            UiKit.Button(main,"Pause","设置 / 暂停",1540,1020,160,44,Pause);
            UiKit.Button(main,"Handbook","牌型手册 [H]",1340,1020,180,44,ShowHandbook,!IsBusy&&!Session.HasPendingSave);
            */
            if(Session.HasPendingSave) SaveFailure();
        }
        public void StartNew(string seedText)
        {
            if(!StartupReady07)return;
            if(Session!=null&&Session.HasPendingSave){SaveFailure();return;}
            uint seed;
            if(string.IsNullOrWhiteSpace(seedText)) { var bytes=new byte[4]; using(var rng=System.Security.Cryptography.RandomNumberGenerator.Create())rng.GetBytes(bytes);seed=BitConverter.ToUInt32(bytes,0); }
            else if(!GameRng.TryParseSeed(seedText,out seed)){notice=SeedError16; Refresh();return;}
            UiKit.Release(main);UiKit.Release(overlay);
            tutorial=false; Session=new GameSession(Config,saves);inMenu=false;notice=""; Send(new SessionCommand{kind=CommandKind.NewRun,seed=seed});
        }
        public CommandResult Send(SessionCommand cmd)
        {
            if(!StartupReady07)return new CommandResult{accepted=false,error="存档仍在读取，请稍候。"};
            if(IsControllerDragging)return new CommandResult{accepted=false,error="先完成或取消卡牌拖动。"};
            if(cmd.kind==CommandKind.ConfirmLocks)cmd.dieIds=Session.PendingIds;
            cmd.commandId=Guid.NewGuid().ToString("N");cmd.expectedActionSequence=Session.ActionSequence;
            if(!IsBusy)presentationBefore07=ViewState??Session.State;
            PrepareDiceInteraction07(cmd);PrepareTransactionVisual07(cmd);
            PrepareSettlementBackdrop07(cmd);
            var result=Session.Execute(cmd);
            ResolveSettlementBackdrop07(cmd,result);
            if(!result.accepted){DiscardPreparedDiceInteraction07();DiscardPreparedTransactionVisual07();QueueCommandRejection16(cmd,result.error??"操作未完成");PlaySound("reject");Refresh();return result;}
            if(result.noChange){DiscardPreparedDiceInteraction07();DiscardPreparedTransactionVisual07();return result;}
            if(result.duplicate){DiscardPreparedDiceInteraction07();DiscardPreparedTransactionVisual07();}
            if(cmd.kind==CommandKind.NewRun)StartRunClock07();
            PresentCommitted(cmd,result);
            return result;
        }
        private void LegacyPresentCommitted(SessionCommand cmd,CommandResult result)
        {
            if(cmd.kind==CommandKind.NewRun)inMenu=false;
            notice="";
            if(result.receipt!=null && result.receipt.effects!=null)
            {
                notice=string.Join("；",result.receipt.effects.Where(e=>e.kind=="DieCopied"||e.kind=="FaceDyed"||e.kind=="DieBroken"||e.kind=="TemporaryDieExpired"||e.kind=="ControllerDestroyed").Select(e=>EffectText(e,result.receipt.submission?.snapshot)));
                if(result.receipt.effects.Any(e=>e.kind=="ControllerDestroyed"))PlaySound("cannon");
                if(result.receipt.effects.Any(e=>e.kind=="DieBroken"))PlaySound("break");
                else if(result.receipt.effects.Any(e=>e.kind=="DieCopied"))PlaySound("copy");
                else if(result.receipt.effects.Any(e=>e.kind=="FaceDyed"))PlaySound("dye");
            }
            if(tutorial) tutorialStep++;
            bool roll=cmd.kind==CommandKind.Roll || (cmd.kind==CommandKind.UseItem && cmd.targetPips.Length==2);
            bool submit=cmd.kind==CommandKind.Submit;
            if(submit)
            {
                var committedScore=Session.State.lastSubmission.breakdown;
                if(committedScore.enchantBaseCount>0){notice+=" 附魔触发：基础 +"+committedScore.enchantBaseAdd;PlaySound("enchant_base10");}
                // p1.1.1: the score enchantment adds enchantRules.scoreAdd100 to the bonus line each time it triggers.
                if(committedScore.enchantAdditiveCount>0){notice+=" 附魔触发：加成 +"+Factor(Config.enchantRules.scoreAdd100)+" 共 "+committedScore.enchantAdditiveCount+" 次";PlaySound("enchant_mult2");}
            }
            if(roll || submit)
            {
                Session.Busy=true;Refresh();
                if(roll)table.BeginRoll(reduced?0:fast?.15f:.90f);
                PlaySound(roll?"roll":"use");
                visual=StartCoroutine(EndVisual(reduced?0:fast?.15f:roll?.90f:.25f));
            }
            else {PlaySound(cmd.kind==CommandKind.ConfirmLocks?"lock":cmd.kind==CommandKind.Buy||cmd.kind==CommandKind.Study?"purchase":"use");Refresh();}
            if(!result.duplicate)LogCommand(cmd,result);
        }
        private IEnumerator EndVisual(float seconds){float elapsed=0;while(elapsed<seconds){if(!modal)elapsed+=Time.unscaledDeltaTime;yield return null;}FinishTransactionEffects07();table.FinishRoll();presentationAudio07?.PlayCue("SFX_DICE_LAND");Session.Busy=false;visual=null;Refresh();}
        public void SkipVisual(){if(LootRevealPlaying15){FinishLootReveal15();return;}if(CompleteDiceInteraction07()){table.FinishLayoutTransitions();return;}if(SettlementRevealPlaying07){CompleteSettlementReveal07();return;}if(IsPresenting07){presentationPlayer07.Skip();table.FinishLayoutTransitions();return;}if(visual!=null){StopCoroutine(visual);visual=null;}FinishTransactionEffects07();table.FinishRoll();if(Session!=null)Session.Busy=false;Refresh();table.FinishLayoutTransitions();}
        private void Update()
        {
            if(!StartupReady07){TickStartupLoad07();TickDisplay();return;}
            TickDisplay();
            TickDiceSheet073();
            TickRunClock07();
            TickNotice07();
            TickLootReveal15();
            TickTerminalCue07();
            TickPresentation07();
            table.Paused=modal;
            if(Art07HandleRebinding())return;
            if(draggedDie07!=null){if(Keyboard.current?.escapeKey.wasPressedThisFrame==true)CancelDieDrag07();return;}
            var k=Keyboard.current;if(k==null)return;
            if(IsControllerDragging){if(k.escapeKey.wasPressedThisFrame)CancelControllerDrag();return;}
            if(DisplayPreferences!=null&&DisplayPreferences.IsPending){if(k.escapeKey.wasPressedThisFrame)DisplayPreferences.Revert();return;}
            // V1.1 W14 (SHOP_LAYOUT 7): Esc leaves the shop's face pick before it opens the pause page.
            if(k.escapeKey.wasPressedThisFrame){if(modal)CloseModal();else if(FacePickActive14)CancelFacePick14();else if(lootFromShop15)EnterShop15();else if(!inMenu)Pause();return;}
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            var input=selected!=null?selected.GetComponentInParent<TMP_InputField>():null;
            if(input!=null)
            {
                // The gameplay Enter binding never shares a frame with a focused editor.
                // Explicit routing also works with Input System keyboards in both Players.
                if(input.isFocused&&(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame))
                {string value=input.text;input.onSubmit.Invoke(value);if(input!=null)input.DeactivateInputField();}
                return;
            }
            if(inMenu||modal||Session==null||ViewState==null||IsBusy)return;
            if(ViewState.phase=="StageSettlement"&&(k.enterKey.wasPressedThisFrame||k.numpadEnterKey.wasPressedThisFrame))
            {
                if(SettlementRevealPlaying07)CompleteSettlementReveal07();else ContinueSettlement15();
                return;
            }
            if(LootPage15 && !FacePickActive14 && (k.enterKey.wasPressedThisFrame || k.numpadEnterKey.wasPressedThisFrame))
            { if(LootRevealPlaying15) FinishLootReveal15(); else EnterShop15(); return; }
            if(Art07KeyPressed("Handbook")){ShowHandbook();return;}
            if(ViewState.phase!="Selecting"&&ViewState.phase!="HandReady")return;
            for(int i=0;i<DigitKeys.Length&&i<visibleDice.Count;i++)if(k[DigitKeys[i]].wasPressedThisFrame){Session.TogglePending(visibleDice[i]);Refresh();return;}
            if(Art07KeyPressed("Lock"))Send(new SessionCommand{kind=CommandKind.ConfirmLocks});
            else if(Art07KeyPressed("Roll"))Send(new SessionCommand{kind=CommandKind.Roll});
            else if(Art07KeyPressed("Clear")){Session.ClearPending();Refresh();}
            else if(Art07KeyPressed("Submit"))Send(new SessionCommand{kind=CommandKind.Submit});
        }
        private void ReturnMenu(){if(!StartupReady07)return;CompleteSettlementReveal07();if(IsBusy)SkipVisual();if(Session.HasPendingSave){SaveFailure();return;}CloseModal();facePick14=null;inMenu=true; if(tutorial||IsDebugWorkspace){tutorial=false;Session=new GameSession(Config,saves,saves.Load());debugDraft=null;}Refresh();}
        private void OnApplicationPause(bool paused){PauseNotices07(paused);if(paused){FinishLootReveal15();CancelControllerDrag();CancelDieDrag07();if(IsBusy)SkipVisual();CancelFacePick14();}}
        private void OnDestroy(){ReleaseUiFontMaterials071();AbandonStartupLoad07();SaveRunClock07();FramePacer.Stop();UiKit.Font=null;}
        private void LogCommand(SessionCommand command,CommandResult result)
        {
            try {var s=Session.State;string file=Path.Combine(SaveDirectoryOverride??Application.persistentDataPath,(tutorial||s.isDebug||s.debugWorkspace?"debug":"normal")+"-actions.jsonl");
                File.AppendAllText(file,JsonUtility.ToJson(new ActionLog{seed=s.seed,runId=s.runId,phase=s.phase,action=command,receipt=result.receipt})+"\n",Encoding.UTF8);}
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException){Debug.LogWarning("操作日志写入失败（存档已保存）："+ex.Message);}
        }
        [Serializable]private sealed class ActionLog{public uint seed;public string runId,phase;public SessionCommand action;public CommandReceipt receipt;}
        private void PlaySound(string name)
        {
            string cue=name=="reject"?"SFX_UI_DENIED":name=="roll"?"SFX_DICE_ROLL":name=="lock"?"SFX_LOCK":name=="purchase"?"SFX_COIN":name=="refresh"?"SFX_REFRESH":name=="break"||name=="cannon"?"SFX_BREAK":name=="copy"||name=="dye"?"SFX_DYE_COPY":name.StartsWith("enchant",StringComparison.Ordinal)?"SFX_CARD_TRIGGER":"SFX_UI_PRESS";
            presentationAudio07?.PlayCue(cue,semanticVariant:name=="cannon"?"card":name);
        }
        // v1.1 (W09): every player-facing number goes through PlayerNumberFormat (text proposal section 3).
        private static string Factor(int n)=>PlayerNumberFormat.Ratio(n,100);
        // Exact views keep every digit (thousands separators, no abbreviation and no markup).
        private static string Big(string value)=>PlayerNumberFormat.Exact(value??"0",PlayerNumberFormat.Unlimited);
        // Compact views: up to 15 digits in full, longer as "约 1.23×10^19".
        private static string CompactScore(string value)=>PlayerNumberFormat.Exact(value??"0");
        private string NameOf(string id){if(string.IsNullOrEmpty(id)||id=="none"||id=="scatter")return "散点";if(id=="all_blue"||id=="all_red")return Config.Color(id).displayName;if(id.StartsWith("B"))return Config.Modifier(id).displayName;if(id.StartsWith("L"))return Config.Loot(id).displayName;if(id=="GOLD")return "折现";if(id.StartsWith("C"))return Config.Controller(id).displayName;if(id.StartsWith("I"))return Config.Consumable(id).displayName;if(id.StartsWith("O"))return Config.Service(id).displayName;if(id.StartsWith("U"))return Config.Upgrade(id).displayName;return Config.Hand(id).displayName;}
        private string Description(string id){if(id=="all_blue"||id=="all_red"){var c=Config.Color(id);return "提交的骰子全是"+(id=="all_red"?"红":"蓝")+"色即成立（1 颗也算）。加成 +"+Factor(c.baseAdd100)+"，每升 1 级再 +"+Factor(c.addPerLevel100)+"，最高 "+c.maxLevel+" 级。";}if(id.StartsWith("B"))return Config.Modifier(id).description;if(id.StartsWith("L"))return Config.Loot(id).description;if(id=="GOLD")return "放弃这一组战利品，换成 "+Config.lootGoldValue+" 金币。";if(id.StartsWith("C"))return Config.Controller(id).description;if(id.StartsWith("I"))return Config.Consumable(id).description;if(id.StartsWith("O"))return Config.Service(id).description;if(id.StartsWith("U"))return Config.Upgrade(id).description;var h=Config.Hand(id);return "本局一直有效；每升 1 级倍率 +"+Factor(h.incrementPerLevel100)+"，最高 "+h.maxLevel+" 级。";}
    }
}
