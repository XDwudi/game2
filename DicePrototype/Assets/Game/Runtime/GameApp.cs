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
        private string notice="", detailDieId;
        private readonly List<string> visibleDice = new List<string>();
        private readonly Dictionary<string,Vector2> dieCenters = new Dictionary<string,Vector2>();
        private static readonly Key[] DigitKeys={Key.Digit1,Key.Digit2,Key.Digit3,Key.Digit4,Key.Digit5,Key.Digit6,Key.Digit7,Key.Digit8,Key.Digit9,Key.Digit0};
        private Coroutine visual;
        public bool IsBusy => Session!=null && Session.Busy;
        public string SaveLocation => saves.SavePath;

        void Awake()
        {
            Application.runInBackground=true;
            // Use the native 60 FPS cap as a safe fallback. The Windows scheduler owns
            // pacing only after successful installation, independently of game/probe state.
            QualitySettings.vSyncCount=0; Application.targetFrameRate=60;
            FramePacer.Install();
            UiKit.Font=uiFont;
            GameConfig.ValidateJson(configAsset.text);
            Config=JsonUtility.FromJson<GameConfig>(configAsset.text); Config.Validate();
            saves=new AtomicSaveStore(Path.Combine(SaveDirectoryOverride??Application.persistentDataPath,"active-run.json"),Config);
            volume=PlayerPrefs.GetFloat("volume",.45f); fast=PlayerPrefs.GetInt("fast",0)==1; reduced=PlayerPrefs.GetInt("reduced",0)==1;
            shopTab=PlayerPrefs.GetInt("shopTab",0);
            audioSource=gameObject.AddComponent<AudioSource>(); audioSource.volume=volume;
            main=UiKit.Rect(canvas.transform,"Screen",0,0,1920,1080); overlay=UiKit.Rect(canvas.transform,"ModalLayer",0,0,1920,1080);
            try { var restored=saves.Load(); Session=new GameSession(Config,saves,restored); notice=saves.LastLoadNotice??""; }
            catch(Exception ex){ Session=new GameSession(Config,saves); notice="恢复失败，原存档保留："+ex.Message; }
            Refresh();
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
            // Pending selection and presentation flags do not mutate committed run data.
            // Reuse its detached snapshot until the session or accepted sequence changes.
            if(!ReferenceEquals(displayedSession,Session)||ViewState==null||ViewState.actionSequence!=Session.ActionSequence)
            { ViewState=Session?.State;displayedSession=Session; }
            UiKit.Clear(main); table.Hide(); visibleDice.Clear(); dieCenters.Clear();
            if(inMenu || ViewState==null) { inMenu=true; Menu(); if(Session.HasPendingSave)SaveFailure(); return; }
            Header();
            switch(ViewState.phase)
            {
                case "StagePreview": StagePreview();break;
                case "Shop": Shop();break;
                case "StageSettlement": Settlement();break;
                case "RunWon": case "RunFailed": RunEnd();break;
                default: Gameplay();break;
            }
            UiKit.Text(main,"Toast",notice,40,1030,1500,34,21,UiKit.Gold);
            UiKit.Button(main,"Pause","设置 / 暂停",1540,1020,160,44,Pause);
            if(Session.HasPendingSave) SaveFailure();
        }
        private void Menu()
        {
            UiKit.Box(main,"Accent",72,94,72,5,UiKit.Gold);
            UiKit.Text(main,"Overline","DICE / BUILD / DECIDE",72,125,960,45,25,UiKit.Teal);
            UiKit.Text(main,"Title","骰子构筑",64,210,1000,150,112);
            UiKit.Text(main,"Subtitle","每一次锁定，都在塑造下一次可能。",72,383,1000,60,34,UiKit.Muted);
            UiKit.Text(main,"Edition","P0  ·  六关挑战\n五颗骰子开始，构筑属于你的得分方式。",74,495,900,110,27);
            UiKit.Text(main,"RuleHint","投掷 → 选择 → 确认锁定 → 提交\n确认后的骰子，本手不能直接解除。",74,715,940,120,27,UiKit.Muted);
            var panel=UiKit.Box(main,"StartPanel",1160,200,650,680,UiKit.Panel);
            UiKit.Text(panel,"StartTitle","开始一场挑战",42,42,560,60,38);
            UiKit.Text(panel,"SeedLabel","复现种子（可留空）",42,133,560,36,23,UiKit.Muted);
            seedField=UiKit.Input(panel,"SeedInput","0 — 4294967295","",42,182,566,64);
            UiKit.Button(panel,"NewRun","新挑战  →",42,278,566,70,()=>StartNew(seedField.text),true,true);
            UiKit.Button(panel,"ContinueRun","继续已保存的挑战",42,370,566,64,()=>{tutorial=false;inMenu=false;notice="已恢复最近一次成功操作。";Refresh();},ViewState!=null);
            UiKit.Button(panel,"Tutorial","练习：锁定与得分",42,454,566,64,BeginTutorial);
            UiKit.Text(panel,"SavedSeed",ViewState==null?"每次有效操作后自动保存。":"存档种子 "+ViewState.seed+"  ·  第 "+(ViewState.stage.stageIndex+1)+" 关",42,552,566,70,21,UiKit.Muted);
            UiKit.Text(main,"MenuNotice",notice,72,930,1730,70,22,UiKit.Gold);
            UiKit.Text(main,"Version","原型测试版  /  p0.4.0",74,1020,1500,32,20,UiKit.Muted);
        }
        public void StartNew(string seedText)
        {
            if(Session!=null&&Session.HasPendingSave){SaveFailure();return;}
            uint seed;
            if(string.IsNullOrWhiteSpace(seedText)) { var bytes=new byte[4]; using(var rng=System.Security.Cryptography.RandomNumberGenerator.Create())rng.GetBytes(bytes);seed=BitConverter.ToUInt32(bytes,0); }
            else if(!GameRng.TryParseSeed(seedText,out seed)){notice="请输入 0 到 4294967295 的十进制整数。"; Refresh();return;}
            UiKit.Release(main);UiKit.Release(overlay);
            tutorial=false; Session=new GameSession(Config,saves);inMenu=false;notice=""; Send(new SessionCommand{kind=CommandKind.NewRun,seed=seed});
        }
        public CommandResult Send(SessionCommand cmd)
        {
            if(cmd.kind==CommandKind.ConfirmLocks)cmd.dieIds=Session.PendingIds;
            cmd.commandId=Guid.NewGuid().ToString("N");cmd.expectedActionSequence=Session.ActionSequence;
            var result=Session.Execute(cmd);
            if(!result.accepted){notice=result.error??"操作未完成";PlaySound("reject");Refresh();return result;}
            PresentCommitted(cmd,result);
            return result;
        }
        private void PresentCommitted(SessionCommand cmd,CommandResult result)
        {
            if(cmd.kind==CommandKind.NewRun)inMenu=false;
            notice="";
            if(result.receipt!=null && result.receipt.effects!=null)
            {
                notice=string.Join("；",result.receipt.effects.Where(e=>e.kind=="DieCopied"||e.kind=="FaceDyed"||e.kind=="DieBroken"||e.kind=="TemporaryDieExpired").Select(EffectText));
                if(result.receipt.effects.Any(e=>e.kind=="DieBroken"))PlaySound("break");
                else if(result.receipt.effects.Any(e=>e.kind=="DieCopied"))PlaySound("copy");
                else if(result.receipt.effects.Any(e=>e.kind=="FaceDyed"))PlaySound("dye");
            }
            if(tutorial) tutorialStep++;
            bool roll=cmd.kind==CommandKind.Roll || (cmd.kind==CommandKind.UseItem && cmd.targetPips.Length==2);
            bool submit=cmd.kind==CommandKind.Submit;
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
        private IEnumerator EndVisual(float seconds){yield return new WaitForSecondsRealtime(seconds);table.FinishRoll();Session.Busy=false;visual=null;Refresh();}
        public void SkipVisual(){if(visual!=null){StopCoroutine(visual);visual=null;}table.FinishRoll();if(Session!=null)Session.Busy=false;Refresh();}
        private void Header()
        {
            UiKit.Text(main,"Brand","骰子构筑",36,24,330,60,38);
            UiKit.Text(main,"RunKind",tutorial?"独立练习 · 不带入挑战":ViewState.isDebug?"DEBUG · 测试挑战":"P0 / 短程构筑",39,87,500,32,20,tutorial||ViewState.isDebug?UiKit.Gold:UiKit.Muted);
            UiKit.Text(main,"Stage","第 "+(ViewState.stage.stageIndex+1)+" / 6 关",535,29,320,50,30);
            UiKit.Text(main,"Seed","种子 "+ViewState.seed,535,84,440,32,20,UiKit.Muted);
            UiKit.Text(main,"Goal","目标  "+ViewState.stage.target.ToString("N0"),980,32,420,50,30,UiKit.Gold);
            UiKit.Text(main,"Money",ViewState.gold+"  金币",1510,27,370,58,37,UiKit.Gold,TextAlignmentOptions.TopRight);
            UiKit.Text(main,"Autosave","自动保存  ·  #"+ViewState.actionSequence,1500,88,380,30,19,UiKit.Muted,TextAlignmentOptions.TopRight);
            UiKit.Box(main,"Divider",36,132,1844,2,UiKit.PanelLight);
        }
        private void StagePreview()
        {
            var def=Config.stages[ViewState.stage.stageIndex];
            int normalTarget=tutorial?50:def.normalTarget;
            UiKit.Text(main,"PreviewLabel","下一关",90,205,800,48,28,UiKit.Teal);
            UiKit.Text(main,"PreviewNumber",(ViewState.stage.stageIndex+1).ToString("00"),80,275,570,210,155,UiKit.Paper);
            UiKit.Text(main,"PreviewCopy","用有限的提交次数，\n累积达到目标分数。",90,535,800,130,42);
            UiKit.Text(main,"Budgets",$"每手 {Session.InitialRolls} 次投掷   /   本关 {Session.InitialSubmissions} 次提交\n提交容量 {Session.EffectiveCapacity}   ·   骰池 {ViewState.dice.Count}\n每次剩余投掷 +{Config.bonusPerUnusedRoll} 基础分",90,720,880,160,28,UiKit.Muted);
            UiKit.Button(main,"Normal",$"普通挑战\n{normalTarget} 分 · 基础奖励 {def.normalReward}",1040,275,720,145,()=>Send(new SessionCommand{kind=CommandKind.ChooseChallenge,risk=false}),true,!ViewState.stage.risk);
            if(def.riskEnabled)UiKit.Button(main,"Risk",$"高压挑战\n{def.riskTarget} 分 · 基础奖励 {def.riskReward}",1040,445,720,145,()=>Send(new SessionCommand{kind=CommandKind.ChooseChallenge,risk=true}),true,ViewState.stage.risk);
            else UiKit.Text(main,"NoRisk","本关仅开放普通挑战",1040,475,720,60,26,UiKit.Muted);
            UiKit.Text(main,"ChoiceHint","进入后无法更改挑战。\n过关奖励 + 剩余提交 + 奖励前余额利息。",1040,640,720,90,26,UiKit.Muted);
            UiKit.Button(main,"StartStage","进入挑战  →",1040,795,720,84,()=>Send(new SessionCommand{kind=CommandKind.StartStage}),true,true);
        }
        private void Gameplay()
        {
            var s=ViewState;var h=s.hand;bool pending=Session.PendingIds.Length>0;bool busy=Session.Busy;
            var left=UiKit.Box(main,"RoundPanel",36,165,285,625,UiKit.Panel);
            UiKit.Text(left,"ScoreLabel","本关累计",22,24,245,38,24,UiKit.Muted);
            UiKit.Text(left,"StageScore",s.stage.score.ToString("N0"),20,75,250,80,52);
            UiKit.Text(left,"Attempts","剩余提交",22,185,245,38,24,UiKit.Muted);
            UiKit.Text(left,"AttemptsValue",s.stage.remainingSubmissions.ToString(),22,232,245,65,46);
            UiKit.Text(left,"Rolls","剩余投掷",22,330,245,38,24,UiKit.Muted);
            UiKit.Text(left,"RollsValue",h.rollsRemaining.ToString(),22,376,245,65,46,UiKit.Teal);
            UiKit.Text(left,"HandEffects",(h.redWave?"红潮 · 本手有效\n":"")+(h.largeBuff?"6点加权 · 本手\n":"")+(h.smallBuff?"1点加权 · 本手\n":"")+(s.stage.stageRollBonus>0?"本关加掷 +"+s.stage.stageRollBonus+"\n":"")+(s.controllers.Any(x=>x.contentId=="C05")?"再来三次："+(h.c05Used?"已用":"未用"):""),22,482,245,130,20,UiKit.Gold);
            UiKit.Text(main,"PoolTitle","骰池",355,165,450,36,25,UiKit.Muted);
            UiKit.Text(main,"PoolHint",h.hasRolled?"点击待锁  ·  右键查看六面与概率":"投掷开始这一手",820,165,565,36,21,UiKit.Muted,TextAlignmentOptions.TopRight);
            var unlocked=s.dice.Where(d=>!h.orderedLockedIds.Contains(d.dieId)).OrderBy(d=>d.createdSerial).ToList();
            for(int i=0;i<unlocked.Count;i++)
            {
                var die=unlocked[i];var result=h.currentResults.FirstOrDefault(x=>x.dieId==die.dieId);
                float x=380+(i%5)*200,y=unlocked.Count>5?250+(i/5)*155:310;
                DrawDie(die,result,new Vector2(x+75,y),false,i,unlocked.Count>5?.8f:1f);
            }
            UiKit.Box(main,"PoolLine",355,515,1030,2,UiKit.PanelLight);
            UiKit.Text(main,"SlotsTitle",$"有序提交   {h.orderedLockedIds.Count} / {s.stage.capacity}",355,536,700,35,25,UiKit.Muted);
            UiKit.Text(main,"LockRule","确认后本手不可直接解除",860,536,525,35,21,UiKit.Gold,TextAlignmentOptions.TopRight);
            float slotW=1030f/s.stage.capacity;
            for(int i=0;i<s.stage.capacity;i++)
            {
                float x=355+i*slotW;UiKit.Box(main,"SlotLine"+i,x+5,739,slotW-10,3,i<h.orderedLockedIds.Count?UiKit.Gold:UiKit.PanelLight);
                UiKit.Text(main,"SlotIndex"+i,(i+1).ToString("00"),x+10,579,slotW-20,30,18,UiKit.Muted);
                if(i<h.orderedLockedIds.Count){var id=h.orderedLockedIds[i];DrawDie(s.dice.First(d=>d.dieId==id),h.currentResults.First(r=>r.dieId==id),new Vector2(x+slotW/2,640),true,i,Mathf.Min(.85f,slotW/180f));}
                else UiKit.Text(main,"EmptySlot"+i,"空栏",x,638,slotW,45,23,UiKit.Muted,TextAlignmentOptions.Center);
            }
            bool canRoll=!busy&&!pending&&h.rollsRemaining>0&&unlocked.Count>0;
            UiKit.Button(main,"Roll",h.hasRolled?"重掷未锁骰  [空格]":"投掷全部骰子  [空格]",355,744,300,58,()=>Send(new SessionCommand{kind=CommandKind.Roll}),canRoll);
            UiKit.Button(main,"Confirm","确认锁定  [E]",670,744,235,58,()=>Send(new SessionCommand{kind=CommandKind.ConfirmLocks}),!busy&&pending);
            UiKit.Button(main,"Clear","清除选择",920,744,160,58,()=>{Session.ClearPending();PlaySound("cancel");Refresh();},!busy&&pending);
            UiKit.Button(main,"Submit","提交已锁骰  [Enter]",1095,744,290,58,()=>Send(new SessionCommand{kind=CommandKind.Submit}),!busy&&!pending&&h.orderedLockedIds.Count>0,true);
            if(busy)UiKit.Button(main,"Skip","跳过表现",1150,808,235,42,SkipVisual);
            else if(pending)UiKit.Text(main,"PendingHint","先确认或清除待锁，再投掷、调序或提交。",355,813,1030,35,22,UiKit.Gold);
            else if(h.orderedLockedIds.Count==s.stage.capacity && unlocked.Count>0)UiKit.Text(main,"FullHint","提交栏已满；仍可重掷未锁骰，但不能继续锁入。",355,470,1030,35,21,UiKit.Gold);
            ScorePanel();InventoryStrip(false);
            if(s.lastSubmission!=null)UiKit.Button(main,"PreviousSubmission","上一手 "+s.lastSubmission.breakdown.score+" 分 · 明细",36,810,285,46,()=>ShowBreakdown(s.lastSubmission.snapshot,s.lastSubmission.breakdown));
            if(tutorial&&!pending&&!busy)UiKit.Text(main,"TutorialGuide",tutorialStep<3?"练习：点击骰子使其待锁，再按 E 确认。也可以先清除选择。":"练习：右侧预览只计算已锁骰；剩余投掷会增加基础分。提交后前往商店。",355,813,1030,42,20,UiKit.Teal);
        }
        private void DrawDie(DieInstance die,DieHandState result,Vector2 center,bool locked,int index,float scale=1f)
        {
            table.Display(die,result,center,scale);dieCenters[die.dieId]=center;
            bool selected=Session.PendingIds.Contains(die.dieId);
            var hit=UiKit.Box(main,"Die_"+die.dieId,center.x-70,center.y-65,140,130,new Color(0,0,0,0),true);
            var pointer=UiKit.Component<DiePointer>(hit.gameObject);pointer.clicked=null;pointer.dragStart=null;pointer.dragged=null;pointer.dragEnd=null;string id=die.dieId;
            pointer.clicked=e=> {if(e.button==PointerEventData.InputButton.Right||locked){ShowDie(id);return;}if(modal||IsBusy)return;Session.TogglePending(id);Refresh();};
            if(locked){pointer.dragEnd=e=>{if(modal||IsBusy||Session.PendingIds.Length>0)return;Vector2 ui=new Vector2(e.position.x/Screen.width*1920,(1-e.position.y/Screen.height)*1080);string target=dieCenters.Where(k=>ViewState.hand.orderedLockedIds.Contains(k.Key)).OrderBy(k=>(k.Value-ui).sqrMagnitude).First().Key;
                var ids=ViewState.hand.orderedLockedIds.ToArray();int from=Array.IndexOf(ids,id),to=Array.IndexOf(ids,target);(ids[from],ids[to])=(ids[to],ids[from]);Send(new SessionCommand{kind=CommandKind.Reorder,dieIds=ids});};}
            if(!locked)visibleDice.Add(id);
            string current=result!=null&&result.hasRolled?result.currentPip+(result.currentColor=="red"?" 红 R":" 蓝 B"):"等待投掷";
            bool compact=locked||scale<.9f;
            UiKit.Text(main,"DieCaption"+id,current,center.x-90,center.y+(compact?50:64),180,26,locked?20:22,result?.currentColor=="red"?UiKit.Red:UiKit.Blue,TextAlignmentOptions.Center);
            string tag=(locked?"已锁":selected?"◇ 待锁":"["+(index==9?0:index+1)+"] "+id)+(die.lifetime=="stage_temporary"?" · 本关":"")+(die.isGlass?" · 玻璃":"");
            UiKit.Text(main,"DieState"+id,tag,center.x-100,center.y+(compact?77:96),200,22,locked?16:17,selected||locked?UiKit.Gold:UiKit.Muted,TextAlignmentOptions.Center);
            if(selected)UiKit.Box(main,"Selected"+id,center.x-63,center.y-60,126,3,UiKit.Gold);
        }
        private void ScorePanel()
        {
            var p=UiKit.Box(main,"ScorePanel",1420,165,460,637,UiKit.Panel);UiKit.Text(p,"Title","提交预览",24,20,412,42,28);
            ScoreBreakdown b=Session.PreviewScore();
            if(b==null){UiKit.Text(p,"Empty","确认锁定骰子后，\n这里会显示完整得分。\n\n一颗骰子也可以提交。",24,116,410,270,26,UiKit.Muted);return;}
            string hand=b.chosenGroupId==null||b.chosenGroupId==""?"散点":NameOf(b.chosenGroupId);
            UiKit.Text(p,"Hand",hand+(b.straightMult100>0?" + 顺子":"")+(b.colorMult100>0?" + 同色":""),24,80,412,70,27,UiKit.Teal);
            string[] rows={"骰子贡献  "+b.pipSum+"  +  剩掷奖励  "+b.unusedRollBonus,
                $"同点 {Factor(b.groupMult100)} + 顺子 {Factor(b.straightMult100)} + 颜色 {Factor(b.colorMult100)}",
                "牌型总倍率  ×"+Factor(b.totalHandMult100),
                "同点红组  "+b.redCount+"颗 · ×"+Factor(b.redMult100),
                "控制器总乘  ×"+b.controllerMultiplier,
                "最终加分  +"+b.finalFlatAdd};
            for(int i=0;i<rows.Length;i++)UiKit.Text(p,"Breakdown"+i,rows[i],24,166+i*46,412,40,22,i==2?UiKit.Gold:UiKit.Paper);
            UiKit.Text(p,"FinalScore",b.score.ToString("N0"),24,459,412,80,57,UiKit.Gold);
            UiKit.Button(p,"Expanded","查看逐骰与效果明细",24,562,412,48,()=>ShowBreakdown(Session.PreviewSnapshot(),b));
        }
        private void Settlement()
        {
            var r=ViewState.lastStageSettlement;
            UiKit.Text(main,"Cleared","挑战达成",100,230,900,100,72,UiKit.Teal);
            UiKit.Text(main,"FinalStageScore",r.score.ToString("N0")+" 分",100,390,900,120,83);
            UiKit.Text(main,"ScoreTarget","目标 "+r.target.ToString("N0")+"  ·  溢出分不带入下一关",105,550,900,55,26,UiKit.Muted);
            var p=UiKit.Box(main,"Rewards",1090,220,690,590,UiKit.Panel);
            UiKit.Text(p,"RewardTitle","本关收益",34,32,620,60,36);
            UiKit.Text(p,"RewardRows",$"奖励前金币                 {r.goldBefore}\n基础奖励                     +{r.baseReward}\n剩余提交                     +{r.unusedSubmissionReward}\n利息{(r.pig?"（存钱罐）":"")}                     +{r.interest}",34,132,620,280,31);
            UiKit.Text(p,"Balance","余额  "+r.goldAfter,34,435,620,80,47,UiKit.Gold);
            UiKit.Button(main,"SettlementContinue",ViewState.stage.settlementContinuation=="Shop"?"前往商店  →":"查看挑战结果  →",1090,850,690,76,()=>Send(new SessionCommand{kind=CommandKind.Continue}),true,true);
            UiKit.Button(main,"LastBreakdown","查看刚才的得分",100,740,700,64,()=>ShowBreakdown(ViewState.lastSubmission.snapshot,ViewState.lastSubmission.breakdown));
        }
        private void RunEnd()
        {
            bool won=ViewState.phase=="RunWon";
            UiKit.Text(main,"EndTitle",won?"六关完成":"挑战结束",100,235,1700,130,90,won?UiKit.Teal:UiKit.Paper);
            UiKit.Text(main,"EndMessage",won?"你让五颗起始骰子，走完了整条构筑路线。":ViewState.failureReason=="NoDice"?"骰组耗尽。":"本关提交次数已用尽，未达到目标。",105,430,1700,90,33,UiKit.Muted);
            UiKit.Text(main,"EndStats",$"种子 {ViewState.seed}    /    第 {ViewState.stage.stageIndex+1} 关    /    本关 {ViewState.stage.score:N0} 分    /    金币 {ViewState.gold}",105,590,1700,70,30,UiKit.Gold);
            UiKit.Button(main,"ReturnMenu","返回主菜单",105,785,780,80,ReturnMenu,true,true);
            if(ViewState.lastSubmission!=null)UiKit.Button(main,"EndBreakdown","最后一次提交明细",960,785,780,80,()=>ShowBreakdown(ViewState.lastSubmission.snapshot,ViewState.lastSubmission.breakdown));
        }
        private void Update()
        {
            var k=Keyboard.current;if(k==null)return;
            if(k.escapeKey.wasPressedThisFrame){if(modal)CloseModal();else if(!inMenu)Pause();return;}
            if(inMenu||modal||Session==null||ViewState==null||IsBusy)return;
            var selected=EventSystem.current!=null?EventSystem.current.currentSelectedGameObject:null;
            if(selected!=null&&selected.GetComponentInParent<TMP_InputField>()!=null)return;
            if(ViewState.phase!="Selecting"&&ViewState.phase!="HandReady")return;
            for(int i=0;i<DigitKeys.Length&&i<visibleDice.Count;i++)if(k[DigitKeys[i]].wasPressedThisFrame){Session.TogglePending(visibleDice[i]);Refresh();return;}
            if(k.eKey.wasPressedThisFrame)Send(new SessionCommand{kind=CommandKind.ConfirmLocks});
            else if(k.spaceKey.wasPressedThisFrame)Send(new SessionCommand{kind=CommandKind.Roll});
            else if(k.enterKey.wasPressedThisFrame)Send(new SessionCommand{kind=CommandKind.Submit});
        }
        private void ReturnMenu(){if(IsBusy)SkipVisual();CloseModal();inMenu=true; if(tutorial){tutorial=false;Session=new GameSession(Config,saves,saves.Load());}Refresh();}
        private void OnApplicationPause(bool paused){if(paused&&IsBusy)SkipVisual();}
        private void OnDestroy(){FramePacer.Stop();UiKit.Font=null;}
        private void LogCommand(SessionCommand command,CommandResult result)
        {
            try {var s=ViewState;string file=Path.Combine(SaveDirectoryOverride??Application.persistentDataPath,(tutorial||s.isDebug?"debug":"normal")+"-actions.jsonl");
                File.AppendAllText(file,JsonUtility.ToJson(new ActionLog{seed=s.seed,runId=s.runId,phase=s.phase,action=command,receipt=result.receipt})+"\n",Encoding.UTF8);}
            catch(Exception ex) when(ex is IOException || ex is UnauthorizedAccessException || ex is System.Security.SecurityException){Debug.LogWarning("操作日志写入失败（存档已保存）："+ex.Message);}
        }
        [Serializable]private sealed class ActionLog{public uint seed;public string runId,phase;public SessionCommand action;public CommandReceipt receipt;}
        private void PlaySound(string name){if(sounds==null)return;var c=sounds.FirstOrDefault(x=>x!=null&&x.name.IndexOf(name,StringComparison.OrdinalIgnoreCase)>=0);if(c!=null)audioSource.PlayOneShot(c,1f);}
        private string Factor(int n)=>(n/100m).ToString("0.##");
        private string NameOf(string id){if(string.IsNullOrEmpty(id))return "散点";if(id.StartsWith("C"))return Config.Controller(id).displayName;if(id.StartsWith("I"))return Config.Consumable(id).displayName;if(id.StartsWith("O"))return Config.Service(id).displayName;if(id.StartsWith("U"))return Config.Upgrade(id).displayName;return Config.Hand(id).displayName;}
        private string Description(string id){if(id.StartsWith("C"))return Config.Controller(id).description;if(id.StartsWith("I"))return Config.Consumable(id).description;if(id.StartsWith("O"))return Config.Service(id).description;if(id.StartsWith("U"))return Config.Upgrade(id).description;var h=Config.Hand(id);return "本Run持续生效；每级倍率 +"+Factor(h.incrementPerLevel100)+"，最高4级。";}
    }
}
