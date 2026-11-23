using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        public AudioMixer art07Mixer;
        public AudioMixerGroup[] art07Buses;
        public AudioClip[] art07Sounds;
        public Sprite[] art07Templates;
        public Art07PrefabView[] art07Prefabs;
        public TMP_FontAsset art07NumberFont;
        private RunState presentationBefore07;
        private RunState lastPresentationBefore07;
        private RunState lastPresentationAfter07;
        private PresentationPlayer presentationPlayer07;
        private PresentationAudioPlayer presentationAudio07;
        private PresentationReceipt receipt07;
        private PresentationBeat beat07;
        private RectTransform effects07;
        private float beatStarted07;
        private TextMeshProUGUI receiptNumber07;
        private Vector2 receiptNumberOrigin07;
        private readonly RectTransform[] particles07=new RectTransform[36];
        private readonly Vector2[] particleOrigins07=new Vector2[36];
        private int particleCount07;
        private bool IsPresenting07=>presentationPlayer07!=null&&presentationPlayer07.IsPlaying;
        private void InitializePresentation07()
        {
            presentationPlayer07=new PresentationPlayer();
            presentationPlayer07.BeatStarted+=OnPresentationBeat07;
            presentationPlayer07.Completed+=CompletePresentationView07;
            presentationAudio07=gameObject.AddComponent<PresentationAudioPlayer>();
            presentationAudio07.Configure(art07Sounds,art07Mixer,art07Buses);ApplyAudioPreferences07();presentationAudio07.PlayMusic();
            UiKit.Templates=art07Templates;
            UiKit.Feedback=cue=>presentationAudio07.PlayCue(cue);
            InitializeEffectLibrary07();
        }
        private void ApplyAudioPreferences07()=>presentationAudio07?.SetVolumes(volume,musicVolume07,sfxVolume07,uiVolume07);
        private PresentationOptions CreatePresentationOptions07(bool fallback=false)=>new PresentationOptions(instant07||fallback?PresentationSpeed.Instant:fast?PresentationSpeed.Fast:PresentationSpeed.Normal,reduced,(PresentationQuality)(quality07==0?2:quality07==2?0:1));
        private void ApplyArtPreferences07()
        {
            UiKit.ReducedMotion=reduced;UiKit.FontScale=textScale07/100f;
            if(Session!=null&&Session.Busy)SkipVisual();
            else if(reduced)FinishTransactionEffects07();
            var light=table.viewCamera!=null?UnityEngine.Object.FindFirstObjectByType<Light>():null;
            if(light!=null)light.shadows=quality07==0?LightShadows.None:LightShadows.Soft;
            var felt=GameObject.Find("Art07Felt");if(felt!=null){var r=felt.GetComponent<Renderer>();var props=new MaterialPropertyBlock();r.GetPropertyBlock(props);props.SetFloat("_Grain",grain07&&!reduced?1:0);props.SetFloat("_Scanlines",scanlines07&&!reduced&&quality07>0?1:0);props.SetFloat("_Sweep072",reduced?0:1);r.SetPropertyBlock(props);}
        }
        private void PresentCommitted(SessionCommand cmd,CommandResult result)
        {
            if(cmd.kind==CommandKind.NewRun)inMenu=false;
            if(tutorial)tutorialStep++;
            notice="";
            if(result.duplicate){Refresh();return;}
            LastTransactionSummary07="";
            AdvanceStablePresentation07(cmd);
            var after=Session.State;
            ArmSettlementReveal07(presentationBefore07,after);
            if((after.phase=="RunWon"||after.phase=="RunFailed")&&presentationBefore07?.phase!=after.phase)terminalCuePending07=after.phase;
            AdvanceGuide07(cmd.kind);
            if(TryPresentLockedDice07(cmd,result)){LogCommand(cmd,result);return;}
            if(cmd.kind==CommandKind.Submit)
            {
                lastPresentationBefore07=presentationBefore07.Clone();lastPresentationAfter07=after.Clone();
                CapturePermanentDisplay07(result.receipt,lastPresentationBefore07,lastPresentationAfter07);
                try{receipt07=PresentationReceiptAdapter.BuildCommitted(result,presentationBefore07,after,Config);}
                catch(Exception ex){receipt07=PresentationReceiptAdapter.Fallback(result.receipt,presentationBefore07,after,ex.Message);Debug.LogWarning("表现降级为最终结果："+ex.Message);}
                Session.Busy=true;
                ResetReceiptEffects07();controllerSourceCache=null;
                var options=CreatePresentationOptions07(receipt07.FallbackReason!=null);
                presentationPlayer07.Play(PresentationSequence.Create(receipt07,options));Refresh();
            }
            else
            {
                bool roll=cmd.kind==CommandKind.Roll||cmd.kind==CommandKind.UseItem&&cmd.targetPips.Length==2;
                if(roll)
                {
                    var brokenPoses=CaptureBrokenPoses07(result.receipt);
                    BeginRollButtonFeedback07(presentationBefore07?.hand?.hasRolled==false);
                    Session.Busy=true;Refresh();float duration=instant07||reduced?0:fast?.15f:.9f;
                    PresentRollEffects07(result.receipt,brokenPoses,duration);
                    table.BeginRoll(duration);presentationAudio07.PlayCue("SFX_DICE_ROLL",receiptId:result.receipt.commandId);
                    if(duration<=0){table.FinishRoll();FinishTransactionEffects07();Session.Busy=false;Refresh();}
                    else visual=StartCoroutine(EndVisual(duration));
                }
                else
                {
                    Refresh();string cue=cmd.kind==CommandKind.ConfirmLocks?"SFX_LOCK":cmd.kind==CommandKind.RefreshControllers?"SFX_REFRESH":"SFX_UI_PRESS";
                    presentationAudio07.PlayCue(cue,receiptId:result.receipt.commandId);
                    PresentTransactionEffects07(cmd,result,presentationBefore07,after);
                }
            }
            LogCommand(cmd,result);
        }
        public bool ReplayLastPresentation07()
        {
            if(receipt07==null||IsBusy||Session.HasPendingSave)return false;
            presentationBefore07=lastPresentationBefore07;Session.Busy=true;notice="正在重播上一手";ResetReceiptEffects07();controllerSourceCache=null;
            var options=CreatePresentationOptions07(receipt07.FallbackReason!=null);
            bool played=presentationPlayer07.Play(PresentationSequence.Create(receipt07,options),true);Refresh();return played;
        }
        public int PresentationDurationMilliseconds07=>presentationPlayer07?.Sequence?.TotalDurationMs??0;
        public bool PresentationPlaying07=>IsPresenting07;
        public PresentationPhase? CurrentPresentationPhase07=>beat07?.Phase;
        public PresentationQuality CurrentPresentationQuality07=>presentationPlayer07.Sequence?.Options.Quality??CreatePresentationOptions07().Quality;
        public bool HasCommittedPresentationDetails07(string handId)=>receipt07!=null&&receipt07.HandId==handId;
        // v1.1 (W09): target dice are named ("骰子 3"), never shown by their ids.
        public string[] CommittedPresentationDetails07=>receipt07==null?Array.Empty<string>():receipt07.Events.Select(e=>EventText07(e)+PermanentFaceDetails07(e)+(e.SourceEmitterIds.Count>0?" · 来源："+string.Join("、",e.SourceEmitterIds.Select(SourceName07)):"")+(e.TargetDieIds.Count>0?" · 骰子："+string.Join("、",e.TargetDieIds.Select(DieName11)):"")).Concat(new[]{BudgetGrantSummary07(lastPresentationBefore07,lastPresentationAfter07)}.Where(x=>!string.IsNullOrEmpty(x))).ToArray();
        private static string PermanentFaceDetails07(PresentationEvent e)
        {
            if(e.EffectFamily=="FaceDyed"||e.EffectFamily=="FaceValueChanged")
                return e.TargetFaceIds.Count==1 ? " · "+FaceName11(e.TargetFaceIds[0]) : "";
            if(e.EffectFamily!="DieCopied"&&e.EffectFamily!="DieDyed")return "";
            Dictionary<string,string> Faces(string value)
            {
                var byId=new Dictionary<string,string>(StringComparer.Ordinal);
                foreach(string entry in (value??"").Split(';'))
                {
                    var fields=entry.Split(':');
                    if(fields.Length!=3 || !byId.TryAdd(fields[0],fields[1]+" 点"+ColorText07(fields[2])))return null;
                }
                return byId;
            }
            var before=Faces(e.BeforeValue);var after=Faces(e.AfterValue);
            if(before?.Count!=6 || after?.Count!=6)return "";
            var lines=new string[6];
            for(int i=0;i<lines.Length;i++)
            {
                string id="F0"+(i+1);
                if(!before.TryGetValue(id,out string oldFace) || !after.TryGetValue(id,out string newFace))return "";
                lines[i]=FaceName11(id)+" "+oldFace+" → "+newFace;
            }
            return " · "+string.Join("；",lines);
        }
        public string FrozenPresentationAfterHash07=>lastPresentationAfter07==null?null:GameSession.StateHash(lastPresentationAfter07);
        public void ShowCommittedPresentationDetails07()
        {
            if(receipt07==null)return;
            var p=OpenModal("本手计分记录");var rows=CommittedPresentationDetails07;
            var list=UiKit.Scroll(p,"CommittedPresentationRows07",32,106,1536,630,630);float y=0;
            for(int i=0;i<rows.Length;i++){var row=UiKit.Text(list,"Event"+i,(i+1)+". "+rows[i],8,y,1488,86,22,UiKit.Paper);row.textWrappingMode=TextWrappingModes.Normal;y+=FitHeight(row,86)+6;}
            list.sizeDelta=new Vector2(list.sizeDelta.x,Math.Max(630,y));
        }
        private void OnPresentationBeat07(PresentationBeat beat)
        {
            beat07=beat;beatStarted07=Time.unscaledTime;
            if(main==null)return;
            if(effects07==null){effects07=UiKit.Rect(canvas.transform,"PresentationEffects",0,0,1920,1080);effects07.SetSiblingIndex(0);}
            effects07.anchoredPosition=main.anchoredPosition;UiKit.Clear(effects07);particleCount07=0;
            ScorePanel();
            // V1.1 (W11): the settle panel follows the receipt beat by beat (gap, then the end-now rewards).
            if(PlayPhase11)DrawSettle11();
            // Build the actual target HUD before binding its icon anchors. The first
            // qualified stamp changes the header layout from the ordinary variant.
            if(beat.Phase==PresentationPhase.TargetState)
            {
                if(receipt07.QualifiedAfter&&!receipt07.QualifiedBefore)BeginQualifiedStamp07();
                else
                {
                    var previousStats=main.Find("Stats07");int sibling=previousStats!=null?previousStats.GetSiblingIndex():0;
                    Header();main.Find("Stats07")?.SetSiblingIndex(sibling);
                }
                // V1.1 (W11): the new hand's budgets (enchantment grants) live in the resources panel.
                if(PlayPhase11)DrawResources11();
            }
            BeginBeatEffects07(beat);
            PresentScoreBeat11(beat);
            if(beat.Phase==PresentationPhase.FinalScore)BeginHighScore073();
            if(!string.IsNullOrEmpty(beat.FocusEmitterId)&&controllerRects.TryGetValue(beat.FocusEmitterId,out var card))
            {
                var corners=new Vector3[4];card.GetWorldCorners(corners);var pos=ScreenToDesign(RectTransformUtility.WorldToScreenPoint(null,corners[1]));
                UiKit.Outline(effects07,"Source",pos.x-3,pos.y-3,card.rect.width+6,card.rect.height+6,UiKit.Paper,3);
            }
            if(art07Prefabs==null||art07Prefabs.Length==0)DrawTargets07(beat);
            foreach(var e in beat.Events.Where(e=>!string.IsNullOrEmpty(e.AudioCue)).Take(4))
                presentationAudio07.PlayCue(e.AudioCue,e.EmitterId??e.StateOwnerId,receipt07.GameReceiptId,(int)beat.Phase,receipt07.ComboTier,e.EffectFamily);
            if(beat.Phase==PresentationPhase.FinalScore)
            {
                var options=presentationPlayer07.Sequence.Options;particleCount07=art07Prefabs!=null&&art07Prefabs.Length>0?0:ScorePresentationTier.ParticleCount(receipt07.ComboTier,options);
                // Fallback sparks (no prefab library) leave upward from the formula total, never toward the submitted dice.
                var l=L11;var total=new Vector2(l.chips.xMax-160,l.chips.y);
                for(int i=0;i<particleCount07;i++){float a=Mathf.PI*(.1f+.8f*i/Math.Max(1,particleCount07-1));var origin=new Vector2(total.x+Mathf.Cos(a)*120,total.y-8-Mathf.Sin(a)*30);particleOrigins07[i]=origin;particles07[i]=UiKit.Box(effects07,"Spark"+i,origin.x,origin.y,6+i%3*2,6+i%3*2,i%3==0?UiKit.Red:i%3==1?UiKit.Blue:UiKit.Gold);}
                if(receipt07.ComboTier>0&&(art07Prefabs==null||art07Prefabs.Length==0))UiKit.Outline(effects07,"ComboRing",l.chips.x-6,l.chips.y-6,l.chips.width+12,l.chips.height+12,UiKit.Gold,2+receipt07.ComboTier);
            }
        }
        private static long ParseLong09(string value)=>long.TryParse(value,System.Globalization.NumberStyles.Integer,System.Globalization.CultureInfo.InvariantCulture,out long parsed)?parsed:0;
        /// <summary>v1.1 (W09): the committed total multiplier as a decimal with its sign ("×140.4").</summary>
        private string ReceiptMultiplier07(int fullDigits=PlayerNumberFormat.FullDigits)
            =>PlayerNumberFormat.Times(ExactScoreMath.Parse(receipt07.MultiplierNumeratorDecimal),ExactScoreMath.Parse(receipt07.MultiplierDenominatorDecimal),fullDigits);
        private string EventText07(PresentationEvent e)
        {
            string name;
            switch(e.EffectFamily)
            {
                case "committed":return "提交";
                case "die_pips":name="点数";break;
                case "contribution":case "controller_die":case "controller_base":case "controller":name=e.AbilityId==null?"卡牌":NameOf(e.AbilityId);break;
                case "retrigger":name=(e.AbilityId==null?"卡牌":NameOf(e.AbilityId))+" 再计一次";break;
                // p1.0.0 boss, item and face-growth events
                case "sealed":return "首领减益 · 这颗骰子被封印";
                case "boss_hand":name="首领减益 · 牌型";break;case "boss":name="首领减益";break;case "item":name="灵光一闪";break;
                case "FaceChanged":return "骰面已改写";
                case "GoldSpent":return "一掷千金 −"+(ParseLong09(e.BeforeValue)-ParseLong09(e.AfterValue))+" 金币";
                case "GoldTaxed":return "首领减益 −"+(ParseLong09(e.BeforeValue)-ParseLong09(e.AfterValue))+" 金币";
                case "HandDowngraded":return "首领减益：牌型等级 "+e.BeforeValue+" → "+e.AfterValue;
                // v0.9 growth and economy events
                case "ControllerGrown":return e.AbilityId=="C141"?"昙花一现 倍率 −0.5":(e.AbilityId==null?"成长":NameOf(e.AbilityId))+" "+GrowthValue09(e.AbilityId,ParseLong09(e.BeforeValue))+" → "+GrowthValue09(e.AbilityId,ParseLong09(e.AfterValue));
                case "ControllerGrowthReset":return (e.AbilityId==null?"成长":NameOf(e.AbilityId))+" 回到 "+GrowthValue09(e.AbilityId,ParseLong09(e.AfterValue));
                case "ControllerSacrificed":return "祭坛吞下右边的卡牌 → "+GrowthValue09("C100",ParseLong09(e.AfterValue));
                case "WarIncome":return "以战养战 +"+e.AfterValue+" 金币";
                case "SaleBonusGrown":return "聚宝盆：卡牌卖价 +"+e.AfterValue;
                case "ControllerSlotAdded":return "卡牌栏位 "+e.BeforeValue+" → "+e.AfterValue;
                case "glass":name="玻璃";break;case "gold":name="黄金面";break;
                case "unused_rolls":name="剩余掷骰";break;case "hand":name="牌型";break;case "color":name="颜色";break;case "red_group":name="红组";break;
                case "enchantment_base":name="附魔";break;case "enchantment_multiplier":name="附魔";break;
                case "effective_pip":name="点数算作";break;case "effective_color":name="颜色算作";break;
                case "red_group_key":return "红色1点按红色4点分组";case "red_group_merge":return "所有红骰算作一组";case "short_straight":return "简化顺子成立";
                case "random_reveal":return "错乱乘积 "+PlayerNumberFormat.Times(e.AfterValue);
                case "final_score":return "本手得分 "+CompactScore(e.AfterValue);
                case "SubmissionIncome":return "提交得 +"+e.AfterValue+" 金币";
                case "stage_income":return "本关结算 +"+e.Operand+" 金币";
                case "FundAccrued":return "成长基金卖价 +"+(ParseLong09(e.AfterValue)-ParseLong09(e.BeforeValue));
                case "ActiveColorChanged":return "下一手加成色："+(e.AfterValue=="red"?"红":"蓝")+(e.BeforeValue==e.AfterValue?"（不变）":"");
                case "DieCopied":return "骰子复制完成";
                case "DieDyed":case "FaceDyed":return e.AfterValue=="red"||e.AfterValue=="blue"
                    ?"骰面染"+ColorText07(e.AfterValue)+(e.BeforeValue==e.AfterValue?"（本来就是"+ColorText07(e.AfterValue)+"）":
                        e.BeforeValue=="red"||e.BeforeValue=="blue"?"（"+ColorText07(e.BeforeValue)+" → "+ColorText07(e.AfterValue)+"）":"")
                    :"骰面染色";
                case "FaceValueChanged":return "骰面点数 "+e.BeforeValue+" → "+e.AfterValue;
                case "HandGrown":return "牌型等级 "+e.BeforeValue+" → "+e.AfterValue;
                case "ControllerDestroyed":return "玻璃大炮自毁（附魔一起消失）";
                case "DieBroken":return "玻璃骰碎了";
                case "TemporaryDieExpired":return "本关临时骰已到期";
                case "hand_roll_bonus":return "附魔：掷骰 +"+e.Operand+" · 本手可掷 "+e.AfterValue;
                case "qualified":return "本关目标已达成";
                case "run_result":return e.AfterValue=="RunWon"?"24 关全部通过":"这一局结束了";
                default:
                    var committed=e.CopyCommittedEffect();
                    return committed==null?"状态已更新":EffectText(committed,lastPresentationAfter07?.lastSubmission?.snapshot);
            }
            string value=e.Operand??e.AfterValue??"";
            if(value=="red")return name+" "+(e.Operation??"")+"红";
            if(value=="blue")return name+" "+(e.Operation??"")+"蓝";
            // v1.1 (W09): exact operands read as decimals ("×3.9", "≈×0.33"), never as fractions.
            return name+" "+OperationText07(e.Operation,value);
        }
        private void DrawTargets07(PresentationBeat beat)
        {
            var groups=beat.Events.SelectMany(e=>e.TargetDieIds.Select(id=>new{Id=id,Event=e})).GroupBy(x=>x.Id).Take(8);
            int n=0;
            foreach(var group in groups)
            {
                if(!dieCenters.TryGetValue(group.Key,out var center))continue;
                float width=Compact07?78:110;
                // Fallback without the prefab library: an outline clear of the die body; its "+N" is DieTicket11.
                UiKit.Outline(effects07,"Target"+n,center.x-width/2-4,center.y-width/2-4,width+8,width+8,UiKit.Gold,3);
                n++;
            }
        }
        private void DrawReceiptScore07(Transform p,float w,float h)
        {
            if(receipt07==null)return;
            bool thin=h<110;
            string[] phases={"提交中…","骰子点数","基础分与倍率","卡牌","本手得分","计分后效果","本关进度"};
            UiKit.Text(p,"Phase",(presentationPlayer07.IsReplay?"重播 · ":"")+phases[(int)(beat07?.Phase??PresentationPhase.Receipt)],20,12,w-650,42,Compact07?18:24,UiKit.Paper);
            bool final=beat07!=null&&beat07.Phase>=PresentationPhase.FinalScore;
            var well=ScoreWell072(p);
            receiptNumber07=UiKit.Text(p,"FinalScore",final?CompactScore(receipt07.ScoreDecimal):"计算中",w-660,10,630,68,Compact07?28:48,UiKit.Gold,TextAlignmentOptions.MidlineRight);
            if(final&&art07NumberFont!=null)receiptNumber07.font=art07NumberFont;
            FitScoreWell072(well,receiptNumber07,w,Compact07?34:44,Compact07?24:36);
            receiptNumber07.rectTransform.pivot=new Vector2(1,.5f);
            receiptNumber07.rectTransform.anchoredPosition=new Vector2(w-30,Compact07?-34:-44);
            receiptNumberOrigin07=receiptNumber07.rectTransform.anchoredPosition;
            // v1.1 (W09): a decimal total multiplier; "连锁 N" is now "生效 N 张" (DEV_PLAN 4.1 item 4).
            string multiplier="总倍率 "+ReceiptMultiplier07();
            string chain=receipt07.ChainCount==0?"":"  ·  生效 "+receipt07.ChainCount+" 张";
            string details=beat07==null?"提交":beat07.Phase==PresentationPhase.PostScore?PostSummary07(beat07):string.Join("  ·  ",beat07.Events.Take(3).Select(EventText07));
            if(beat07!=null&&!final){var sources=beat07.Events.Where(e=>e.IsScoring).SelectMany(e=>e.SourceEmitterIds).Distinct().ToArray();if(sources.Length>0)details+=" · 来源 "+string.Join("、",sources.Select(SourceName07));}
            if(beat07?.Phase==PresentationPhase.TargetState)details=BudgetGrantSummary07(lastPresentationBefore07,lastPresentationAfter07);
            string text=final&&beat07?.Phase!=PresentationPhase.PostScore?multiplier+chain+(string.IsNullOrEmpty(details)?"":" · "+(beat07?.Phase==PresentationPhase.TargetState?details:"")):details;
            // The shallow panel reserves its whole right column for the animated number.
            var line=UiKit.Text(p,"ReceiptFactors",text,20,thin?58:Compact07?65:88,w-(thin?1180:290),thin?40:Compact07?40:58,thin?20:Compact07?17:23,UiKit.Muted);line.overflowMode=TextOverflowModes.Ellipsis;
            TrimRailButton072(UiKit.FlatButton(p,"PresentationDetails07","完整记录",w-(thin?910:250),thin?44:Compact07?60:88,230,60,ShowCommittedPresentationDetails07),thin);
        }
        private void TickPresentation07()
        {
            bool ticking=IsPresenting07||TransactionVisualPlaying07||LockVisualPlaying07;
            float visualDelta=ticking?DiceCraft.Verification.Art07VisualRecorder.PresentationDeltaTime(this,Time.unscaledDeltaTime):(QualifiedStampPlaying07||SettlementRevealPlaying07)?DiceCraft.Verification.Art07VisualRecorder.NonblockingDeltaTime(this,Time.unscaledDeltaTime):Time.unscaledDeltaTime;
            if(IsPresenting07){presentationPlayer07.Clock.Paused=modal;presentationPlayer07.Tick(visualDelta);}
            TickScore11();
            TickHighScore073(Time.unscaledDeltaTime);
            TickLibraryEffects07(visualDelta);TickQualifiedStamp07(visualDelta);TickSettlementReveal07(visualDelta);TickDiceInteraction07(visualDelta);
            if(!IsPresenting07||modal)return;
            float elapsed=(float)(presentationPlayer07.Clock.ElapsedMilliseconds-(beat07?.StartMs??0))/1000f;
            if(beat07?.Phase==PresentationPhase.FinalScore)
            {
                var options=presentationPlayer07.Sequence.Options;float t=Mathf.Clamp01(elapsed/Math.Max(.01f,beat07.DurationMs/1000f));
                if(receiptNumber07!=null){float peak=ScorePresentationTier.ScalePeak(receipt07.ComboTier,options);receiptNumber07.rectTransform.localScale=Vector3.one*(1+(peak-1)*Mathf.Sin(t*Mathf.PI));receiptNumber07.rectTransform.anchoredPosition=receiptNumberOrigin07+Vector2.right*(shake07&&!options.ReducedMotion&&options.Speed==PresentationSpeed.Normal?Mathf.Sin(t*Mathf.PI*2)*Math.Min(3,receipt07.ComboTier):0);}
                for(int i=0;i<particleCount07;i++){var p=particles07[i];if(p==null)continue;float a=Mathf.PI*(.1f+.8f*i/Math.Max(1,particleCount07-1));p.anchoredPosition=new Vector2(particleOrigins07[i].x+Mathf.Cos(a)*36*t,-particleOrigins07[i].y+Mathf.Sin(a)*26*t);p.localScale=Vector3.one*(1-t);}
            }
        }
        private void ClearPresentationEffects07()
        {
            ReturnLibraryEffects07();ClearScore11();
            if(effects07!=null)UiKit.Clear(effects07);particleCount07=0;if(receiptNumber07!=null){receiptNumber07.rectTransform.localScale=Vector3.one;receiptNumber07.rectTransform.anchoredPosition=receiptNumberOrigin07;}
        }
    }
}
