using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private readonly Dictionary<string,PresentationObjectPool<Art07PrefabView>> effectPools07=new Dictionary<string,PresentationObjectPool<Art07PrefabView>>();
        private readonly Dictionary<Art07PrefabView,Transform> effectPlacements07=new Dictionary<Art07PrefabView,Transform>();
        private readonly List<Art07PrefabView> activeEffects07=new List<Art07PrefabView>();
        private readonly PresentationEffectBudget effectBudget07=new PresentationEffectBudget();
        private readonly HashSet<string> punchedEmitters07=new HashSet<string>(StringComparer.Ordinal);
        private readonly List<CardTail07> cardTails07=new List<CardTail07>();
        private readonly List<string> revealedOwners07=new List<string>();
        private readonly List<PermanentStamp07> permanentStamps07=new List<PermanentStamp07>();
        private RectTransform libraryEffects07,libraryUnder07,effectLabels07,sourceMarks07;
        private string effectReceipt07;
        private float effectElapsed07,effectDuration07;
        private bool effectOnClock07;
        private PresentationOptions effectOptions07;
        private int sourceMarkCount07,offscreenLinks07;
        private sealed class CardTail07 { public RectTransform Card; public double StartedMs; }
        private sealed class PermanentStamp07 { public TextMeshProUGUI Stamp,Prefix; }
        private struct DiePose07 { public Vector3 Position,Scale; public Quaternion Rotation; }
        public int ActiveArtEffects07=>activeEffects07.Count;
        public int CreatedArtEffects07=>effectPools07.Values.Sum(p=>p.CreatedCount);
        public int ActiveCardPunches07=>cardTails07.Count;
        public int ActiveSourceMarks07=>sourceMarkCount07;
        public int InvisibleSourceLinks07=>offscreenLinks07;
        public string[] RevealedOwnerIds07=>revealedOwners07.ToArray();
        public string LastTransactionSummary07 { get; private set; }

        private void InitializeEffectLibrary07()
        {
            libraryUnder07=UiKit.Rect(canvas.transform,"Art07UnderEffects",0,0,1920,1080);
            libraryEffects07=UiKit.Rect(canvas.transform,"Art07LocalEffects",0,0,1920,1080);
            effectLabels07=UiKit.Rect(libraryEffects07,"EffectLabels",0,0,1920,1080);
            sourceMarks07=UiKit.Rect(libraryEffects07,"SourceMarks",0,0,1920,1080);
            InitializeEnchantCues071();
            foreach(var prefab in art07Prefabs??Array.Empty<Art07PrefabView>())
            {
                if(prefab==null||!prefab.isEffect||effectPools07.ContainsKey(prefab.assetId))continue;
                int count=prefab.assetId=="fx_value_ticket"?16:prefab.assetId=="fx_copy_link"?12:prefab.assetId=="fx_target_bracket"?8:prefab.assetId=="fx_face_change"||prefab.assetId=="fx_card_destroy"?3:prefab.assetId=="fx_dice_break"?2:1;
                var captured=prefab;
                effectPools07.Add(prefab.assetId,new PresentationObjectPool<Art07PrefabView>(count,count,()=>
                {
                    bool ui=captured.transform is RectTransform;
                    Transform placement=ui?libraryEffects07:new GameObject("Placement_"+captured.assetId).transform;
                    if(!ui)placement.SetParent(table.transform,false);
                    var view=Instantiate(captured,placement,false);view.name=captured.assetId;view.autoTick=false;
                    if(!ui)effectPlacements07.Add(view,placement);
                    foreach(var g in view.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))g.raycastTarget=false;
                    return view;
                },v=>{v.ResetForPool();if(effectPlacements07.TryGetValue(v,out var p))p.gameObject.SetActive(false);}));
            }
        }
        private void ResetReceiptEffects07()
        {
            ReturnLibraryEffects07(true);punchedEmitters07.Clear();revealedOwners07.Clear();offscreenLinks07=0;
        }
        private void EffectLayerOrder07()
        {
            if(libraryEffects07==null)return;
            libraryUnder07.SetSiblingIndex(0);libraryEffects07.SetSiblingIndex(Math.Min(canvas.transform.childCount-1,main.GetSiblingIndex()+1));
            if(overlay!=null)overlay.SetAsLastSibling();
            libraryEffects07.anchoredPosition=main.anchoredPosition;libraryUnder07.anchoredPosition=main.anchoredPosition;
        }
        private Art07PrefabView Effect07(string asset,string identity,Vector2 point,Vector2 size,string exact="",string description="",bool behind=false)
        {
            if(!effectPools07.TryGetValue(asset,out var pool))return null;
            var view=pool.Rent();if(view==null)return null;
            var r=(RectTransform)view.transform;r.SetParent(behind?libraryUnder07:libraryEffects07,false);
            r.anchorMin=r.anchorMax=r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(point.x,-point.y);
            r.localRotation=Quaternion.identity;r.localScale=new Vector3(size.x/r.sizeDelta.x,size.y/r.sizeDelta.y,1);
            view.Bind(identity,"",exact,description);
            // The receipt already carries its operation; never show a baked '+ ×4'.
            if(asset=="fx_value_ticket"&&view.icon!=null)view.icon.enabled=false;
            view.Play(Vector3.zero,reduced,effectDuration07);view.autoTick=false;activeEffects07.Add(view);return view;
        }
        private bool TryCardBounds07(string id,out Rect bounds)
        {
            bounds=default;
            if(id==null||!controllerRects.TryGetValue(id,out var card)||card==null||!card.gameObject.activeInHierarchy)return false;
            var corners=new Vector3[4];card.GetWorldCorners(corners);
            var a=ScreenToDesign(RectTransformUtility.WorldToScreenPoint(null,corners[1]));
            var b=ScreenToDesign(RectTransformUtility.WorldToScreenPoint(null,corners[3]));
            bounds=Rect.MinMaxRect(Math.Min(a.x,b.x),Math.Min(a.y,b.y),Math.Max(a.x,b.x),Math.Max(a.y,b.y));return true;
        }
        private string SourceName07(string id)
        {
            var list=lastPresentationBefore07?.controllers;int i=list==null?-1:list.FindIndex(c=>c.instanceId==id);
            return i<0?id??"来源未记录":"#"+(i+1)+" "+NameOf(list[i].definitionId);
        }
        private bool EffectGroup07(string group,bool mainGroup=false)=>effectBudget07.TryBeginGroup(effectReceipt07,group,mainGroup);
        private int EffectReserve07(string group,EffectPoolKind kind,int count)
        {
            var options=effectOptions07??CreatePresentationOptions07();
            if(options.Quality==PresentationQuality.Low&&(kind==EffectPoolKind.Fragment||kind==EffectPoolKind.Particle))return 0;
            return effectBudget07.Reserve(effectReceipt07,group,kind,count,options);
        }
        private void MarkBeatSources07(PresentationBeat beat)
        {
            UiKit.Clear(sourceMarks07);sourceMarkCount07=0;
            var events=beat.Phase>=PresentationPhase.FinalScore?receipt07.Events:beat.Events;
            var ids=events.Where(e=>e.IsScoring).SelectMany(e=>e.SourceEmitterIds).Distinct(StringComparer.Ordinal).ToArray();
            foreach(string id in ids)
            {
                if(!TryCardBounds07(id,out var r))continue;
                int n=sourceMarkCount07++;
                UiKit.Outline(sourceMarks07,"Hit"+n,r.xMin-2,r.yMin-2,r.width+4,r.height+4,UiKit.Gold,2);
                UiKit.Text(sourceMarks07,"Check"+n,"✓",r.xMin+4,r.yMax-28,26,24,17,UiKit.Ink);
            }
            // Fast/aggregate uses static marks. A Normal card receives one complete 220 ms punch per receipt.
            string focus=beat.FocusEmitterId??(beat.Phase==PresentationPhase.DiceContributions||beat.Phase==PresentationPhase.BaseAndStructure?ids.FirstOrDefault():null);
            if(focus!=null&&!reduced&&effectOptions07.Speed==PresentationSpeed.Normal&&controllerRects.TryGetValue(focus,out var card)&&punchedEmitters07.Add(focus))
                cardTails07.Add(new CardTail07{Card=card,StartedMs=presentationPlayer07.Clock.ElapsedMilliseconds});
        }
        private void BeginBeatEffects07(PresentationBeat beat)
        {
            ReturnLibraryEffects07(false);EffectLayerOrder07();effectReceipt07=receipt07.GameReceiptId;effectOnClock07=true;effectDuration07=Math.Max(.001f,beat.DurationMs/1000f);
            effectOptions07=presentationPlayer07.Sequence.Options;EffectGroup07("main",true);MarkBeatSources07(beat);
            PresentScoringEnchants071(beat);
            if(beat.FocusEmitterId!=null&&TryCardBounds07(beat.FocusEmitterId,out var source))
            {
                Effect07("fx_source_hit",beat.FocusEmitterId,source.position,source.size);
                var e=beat.Events.FirstOrDefault(x=>x.Operand!=null&&x.IsScoring);
                if(e!=null&&EffectReserve07("main",EffectPoolKind.NumberLabel,1)>0)
                    Effect07("fx_value_ticket",e.EventId,new Vector2(source.center.x-90,Compact07?260:258),new Vector2(180,48),(e.Operation??"")+DisplayFraction07(e.Operand));
                if(e!=null&&e.StateOwnerId!=null&&e.StateOwnerId!=e.EmitterId&&EffectGroup07("links"))Link07(e.StateOwnerId,e.EmitterId,e.EventId);
            }
            if(beat.Phase==PresentationPhase.DiceContributions)
            {
                foreach(var group in beat.Events.SelectMany(e=>e.TargetDieIds.Select(id=>new{Id=id,Event=e})).GroupBy(x=>x.Id).Take(8))
                {
                    if(!dieCenters.TryGetValue(group.Key,out var center)||EffectReserve07("main",EffectPoolKind.NumberLabel,1)==0)continue;
                    var e=group.Last().Event;float size=Compact07?78:110;
                    Effect07("fx_target_bracket",group.Key,center-Vector2.one*(size+8)*.5f,Vector2.one*(size+8));
                    Effect07("fx_value_ticket",e.EventId,new Vector2(center.x-62,center.y-size*.5f-50),new Vector2(124,42),e.AfterValue??e.Operand);
                }
            }
            var reveals=beat.Events.Where(e=>e.EffectFamily=="random_reveal").GroupBy(e=>e.StateOwnerId).Select(g=>g.First()).ToArray();
            foreach(var e in reveals)if(!revealedOwners07.Contains(e.StateOwnerId))revealedOwners07.Add(e.StateOwnerId);
            if(reveals.Length==1)
            {
                var e=reveals[0];var v=Effect07("fx_random_reveal",e.StateOwnerId,new Vector2(Compact07?680:700,Compact07?285:290),new Vector2(130,110),"×"+DisplayFraction07(e.AfterValue));
                string[] values={"1/2","1","2","3","4","5","6","7","8"};int slice=Array.IndexOf(values,e.AfterValue);
                if(v!=null){if(slice>=0)v.SetRevealSlice(slice);v.ConfigureCommittedReveal(effectOptions07.Speed==PresentationSpeed.Fast||effectOptions07.ReducedMotion,effectDuration07);}
                if(e.SourceEmitterIds.Count>1)ShowEffectSummary07("共享抽中 q="+DisplayFraction07(e.AfterValue)+" · "+e.SourceEmitterIds.Count+" 来源合计 q"+(e.SourceEmitterIds.Count==2?"²":"^"+e.SourceEmitterIds.Count)+" · 各来源见完整记录",true);
                if(EffectGroup07("links"))foreach(string id in e.SourceEmitterIds.Where(id=>id!=e.StateOwnerId))Link07(e.StateOwnerId,id,e.EventId);
            }
            else if(reveals.Length>1)
            {
                // Multiple real owners are one static list, never multiple competing wheels.
                ShowEffectSummary07("已提交随机结果："+string.Join(" · ",reveals.Select(e=>SourceName07(e.StateOwnerId)+" ×"+DisplayFraction07(e.AfterValue))),true);
                if(EffectGroup07("links"))foreach(var e in reveals)foreach(string id in e.SourceEmitterIds.Where(id=>id!=e.StateOwnerId))Link07(e.StateOwnerId,id,e.EventId);
            }
            if(beat.Phase==PresentationPhase.PostScore)PresentPostEffects07(beat);
            if(beat.Phase==PresentationPhase.FinalScore)
            {
                var burst=Effect07("fx_combo_burst",receipt07.GameReceiptId,Vector2.zero,new Vector2(600,Compact07?112:160),"");
                if(burst!=null)
                {
                    burst.SetBurstLevel(receipt07.ComboTier);burst.SetParticleBudget(ScorePresentationTier.ParticleCount(receipt07.ComboTier,effectOptions07));
                    // Above Fill, below exact text. Preserve the first visual-review correction.
                    var scoreRoot=receiptNumber07.rectTransform.parent as RectTransform;var r=(RectTransform)burst.transform;
                    r.SetParent(scoreRoot,false);r.anchoredPosition=Vector2.zero;
                    burst.ConfigureScoreBurst(new Vector2(scoreRoot.rect.width,scoreRoot.rect.height),effectOptions07.Quality==PresentationQuality.Low);
                    r.SetSiblingIndex(receiptNumber07.transform.GetSiblingIndex());
                }
            }
        }
        private void PresentPostEffects07(PresentationBeat beat)
        {
            MarkPermanentSources07(beat.Events);
            var destroyed=beat.Events.Where(e=>e.EffectFamily=="ControllerDestroyed").ToArray();
            // Allocate by semantic category, so three changed dice cannot consume all auxiliary groups.
            bool risk=destroyed.Length>0&&EffectGroup07("risk");
            var targets=beat.Events.Where(e=>IsPermanentEvent07(e)&&e.TargetDieIds.Count>0).SelectMany(e=>e.TargetDieIds.Select(id=>new{Id=id,Event=e})).GroupBy(x=>x.Id).ToArray();
            bool permanent=targets.Length>0&&EffectGroup07("permanent");
            int visible=0;
            foreach(var group in targets)
            {
                if(!dieCenters.TryGetValue(group.Key,out var center))continue;
                bool changed=permanent&&BeginPermanentTarget07(group.Key,group.Select(x=>x.Event),center);
                if(changed&&visible++<3)
                {
                    // The persistent prefix already owns space inside this slot.
                    // Give that same space to one short stamp; do not stack a
                    // second caption below the die or mutate the pooled caption rect.
                    // Frame the physical die column; the right column owns the
                    // slot number and full permanent-state description.
                    var faceSize=new Vector2(92,Compact07?102:128);
                    var faceFx=Effect07("fx_face_change",group.Key,center-faceSize*.5f,faceSize);
                    if(faceFx!=null)
                    {
                        effectLocalDurations07[faceFx]=effectOptions07.ReducedMotion?.12f:effectOptions07.Speed==PresentationSpeed.Fast?.14f:.36f;
                        var state=main.Find("DieState"+group.Key)?.GetComponent<TextMeshProUGUI>();
                        if(state!=null&&state.text.StartsWith("永久",StringComparison.Ordinal))
                        {
                            var r=state.rectTransform;
                            var prefix=UiKit.Text(effectLabels07,"PermanentPrefix_"+group.Key,"永久",r.anchoredPosition.x,-r.anchoredPosition.y,r.rect.width,r.rect.height,14,UiKit.Paper,state.alignment);
                            prefix.font=state.font;prefix.fontSize=state.fontSize;prefix.lineSpacing=state.lineSpacing;
                            prefix.textWrappingMode=TextWrappingModes.NoWrap;prefix.overflowMode=TextOverflowModes.Overflow;
                            var stamp=UiKit.Text(effectLabels07,"PermanentStamp_"+group.Key,"永久",r.anchoredPosition.x,-r.anchoredPosition.y,r.rect.width,r.rect.height,14,UiKit.Gold,state.alignment);
                            stamp.font=state.font;stamp.fontSize=state.fontSize;stamp.lineSpacing=state.lineSpacing;
                            stamp.textWrappingMode=TextWrappingModes.NoWrap;stamp.overflowMode=TextOverflowModes.Overflow;
                            // Same-position crossfade hands the temporary gold
                            // stamp to the permanent paper prefix, preserving
                            // point/color origins and line baselines throughout.
                            state.text="\u3000\u3000"+state.text.Substring(2);
                            state.ForceMeshUpdate();stamp.ForceMeshUpdate();prefix.ForceMeshUpdate();
                            permanentStamps07.Add(new PermanentStamp07{Stamp=stamp,Prefix=prefix});
                            EvaluatePermanentStamps07(0);
                        }
                    }
                }
            }
            if(risk)foreach(var e in destroyed.Take(3))
            {
                string id=e.EmitterId??e.SourceEmitterIds.FirstOrDefault();
                if(!TryCardBounds07(id,out var r))continue;
                var v=Effect07("fx_card_destroy",e.EventId,r.position,r.size);
                if(v!=null)v.SetParticleBudget(EffectReserve07("risk",EffectPoolKind.Fragment,5));
            }
            if(EffectGroup07("states"))
            {
                foreach(var e in beat.Events.Where(e=>e.EffectFamily=="ActiveColorChanged"||e.EffectFamily=="FundAccrued"))
                {
                    var ids=e.EffectFamily=="ActiveColorChanged"?e.SourceEmitterIds:new[]{e.EmitterId??e.StateOwnerId};
                    foreach(string id in ids)
                    {
                        if(!TryCardBounds07(id,out var r))continue;
                        UiKit.Outline(effectLabels07,"State"+id,r.xMin-2,r.yMin-2,r.width+4,r.height+4,UiKit.Violet,2);
                        var label=controllerRects[id].Find("State")?.GetComponent<TextMeshProUGUI>();
                        if(label==null)continue;
                        if(e.EffectFamily=="ActiveColorChanged")label.text=(e.BeforeValue==e.AfterValue?"仍为":"下手")+ColorText07(e.AfterValue);
                        else {var owner=lastPresentationAfter07?.controllers.FirstOrDefault(c=>c.instanceId==id);if(owner!=null)label.text="售价 "+ControllerRules.SalePrice(owner,Config);}
                    }
                }
                var cash=beat.Events.FirstOrDefault(e=>e.EffectFamily=="SubmissionIncome"||e.EffectFamily=="stage_income");
                if(cash!=null&&EffectReserve07("states",EffectPoolKind.NumberLabel,1)>0)
                    Effect07("fx_cash_ticket",cash.EventId,new Vector2(960,Compact07?284:320),new Vector2(220,58),"+"+(cash.Operand??cash.AfterValue));
            }
        }
        private string PostSummary07(PresentationBeat beat)
        {
            // Keep rule order while grouping repeated same-family changes; no important risk disappears behind Take(3).
            var parts=new List<string>();
            foreach(var group in beat.Events.GroupBy(e=>e.EffectFamily))
            {
                int count=group.Count();var e=group.First();
                if(e.EffectFamily=="ControllerDestroyed")parts.Add("已消耗 "+count+" 张卡");
                else if(e.EffectFamily=="ActiveColorChanged")parts.Add("下手颜色 "+string.Join("/",group.Select(x=>(x.AfterValue=="red"?"红":"蓝")).Distinct()));
                else if(e.EffectFamily=="FaceValueChanged")parts.Add("永久改点 "+count+" 面");
                else if(e.EffectFamily=="DieCopied")parts.Add("六面复制 "+count+" 骰");
                else if(e.EffectFamily=="DieDyed"||e.EffectFamily=="FaceDyed")parts.Add("永久染色 "+count+" 项");
                else if(e.EffectFamily=="HandGrown")parts.Add("牌型成长 "+count+" 项");
                else if(e.EffectFamily=="FundAccrued")parts.Add("基金卖价成长 "+count+" 项");
                else parts.Add(EventText07(e)+(count>1?" 等"+count+"项":""));
            }
            return string.Join(" · ",parts);
        }
        private static string DisplayFraction07(string exact)=>exact=="1/2"?"0.5":exact;
        private void ShowEffectSummary07(string text,bool multiline=false,Action details=null)
        {
            // Reserved score area: never cover the pool or the action buttons.
            float x=Compact07?376:384,y=Compact07?340:366,w=Compact07?1510:1496,h=Compact07?52:72;
            var p=UiKit.Frame(effectLabels07,"Summary",x,y,w,h,UiKit.Background,UiKit.PanelLight);
            var label=UiKit.Text(p,"Text",text,8,4,w-(multiline?270:16),h-8,Compact07?16:21,UiKit.Paper);
            label.textWrappingMode=TextWrappingModes.Normal;label.overflowMode=TextOverflowModes.Ellipsis;
            if(multiline)UiKit.Button(p,"AllResults07","完整记录",w-250,4,238,h-8,details??ShowCommittedPresentationDetails07);
        }
        private Dictionary<string,DiePose07> CaptureBrokenPoses07(CommandReceipt receipt)
        {
            var poses=new Dictionary<string,DiePose07>(StringComparer.Ordinal);
            foreach(var e in receipt.effects.Where(e=>e.kind=="DieBroken"))
            {
                var source=table.GetFaceOrientation(e.dieId);
                if(source!=null)poses[e.dieId]=new DiePose07{Position=source.position,Rotation=source.rotation,Scale=source.lossyScale};
            }
            return poses;
        }
        private void PresentRollEffects07(CommandReceipt committed,Dictionary<string,DiePose07> poses,float rollDuration)
        {
            ReturnLibraryEffects07(true);EffectLayerOrder07();effectReceipt07=committed.commandId;effectOnClock07=false;effectElapsed07=0;
            effectDuration07=Math.Max(.12f,Math.Min(.32f,rollDuration));effectOptions07=CreatePresentationOptions07();EffectGroup07("main",true);
            var broken=committed.effects.Where(e=>e.kind=="DieBroken").ToArray();
            if(broken.Length==0)return;
            LastTransactionSummary07="玻璃骰已破碎 "+broken.Length+" 颗 · 提交容量保持";
            ShowEffectSummary07(LastTransactionSummary07);
            if(!instant07&&EffectGroup07("risk")){ShowAdditionalBrokenOutlines07(broken,poses);}
            if(!instant07&&EffectGroup07("risk"))foreach(var e in broken.Take(2))
            {
                if(poses.TryGetValue(e.dieId,out var pose))BreakDie07(e.dieId,committed.commandId+"/break/"+e.dieId,pose);
                presentationAudio07.PlayCue("SFX_BREAK",e.dieId,committed.commandId,semanticVariant:"DieBroken");
            }
        }
        private void BreakDie07(string dieId,string eventId,DiePose07 pose)
        {
            if(!effectPools07.TryGetValue("fx_dice_break",out var pool))return;
            var v=pool.Rent();if(v==null)return;
            var placement=effectPlacements07[v];placement.SetPositionAndRotation(pose.Position,pose.Rotation);
            Vector3 parentScale=placement.parent.lossyScale;
            placement.localScale=new Vector3(pose.Scale.x/parentScale.x,pose.Scale.y/parentScale.y,pose.Scale.z/parentScale.z);
            placement.gameObject.SetActive(true);
            // Motion affects the child only; the placed world pose cannot be reset by Play/Evaluate.
            v.Bind(eventId,"","","");v.SetParticleBudget(EffectReserve07("risk",EffectPoolKind.Fragment,6));
            v.Play(Vector3.zero,reduced,effectDuration07);activeEffects07.Add(v);
        }
        private void Link07(string from,string to,string eventId)
        {
            if(!TryCardBounds07(from,out var a)||!TryCardBounds07(to,out var b)){offscreenLinks07++;return;}
            if(EffectReserve07("links",EffectPoolKind.SourceLine,1)==0)return;
            var link=Effect07("fx_copy_link",eventId,Vector2.zero,new Vector2(192,192));if(link==null)return;
            Vector2[] points;
            if(Math.Abs(a.center.y-b.center.y)<20)
            {
                float lane=Math.Max(a.yMax,b.yMax)+7;
                points=new[]{new Vector2(a.center.x,a.yMax),new Vector2(a.center.x,lane),new Vector2(b.center.x,lane),new Vector2(b.center.x,b.yMax)};
            }
            else
            {
                var visible=controllerRects.Keys.Select(id=>TryCardBounds07(id,out var r)?r:default).Where(r=>r.width>0).ToArray();
                float outside=visible.Max(r=>r.xMax)+8;bool down=b.center.y>a.center.y;
                float ay=down?a.yMax:a.yMin,by=down?b.yMin:b.yMax,al=ay+(down?6:-6),bl=by+(down?-6:6);
                points=new[]{new Vector2(a.center.x,ay),new Vector2(a.center.x,al),new Vector2(outside,al),new Vector2(outside,bl),new Vector2(b.center.x,bl),new Vector2(b.center.x,by)};
            }
            link.SetPolyline(points.Select(p=>new Vector2(p.x,-p.y)).ToArray());
        }
        private void PresentTransactionEffects07(SessionCommand cmd,CommandResult result,RunState before,RunState after)
        {
            if(before==null||after==null||result.duplicate)return;
            PresentStageCardExit07(cmd,result,before,after);
            // S09 owns this committed stage cash presentation; avoid a second generic cash ticket.
            if(after.phase=="StageSettlement"&&after.lastStageSettlement?.settlementId!=before.lastStageSettlement?.settlementId)return;
            LastTransactionSummary07="";
            string grant=BudgetGrantSummary07(before,after);
            if(cmd.kind==CommandKind.StartStage)LastTransactionSummary07=grant;
            if(cmd.kind==CommandKind.Buy)LastTransactionSummary07=PurchasedDieSummary07(cmd,before,after);
            if(cmd.kind==CommandKind.UseItem)
            {
                string id=before.inventory.FirstOrDefault(i=>i.instanceId==cmd.itemInstanceId)?.contentId;
                LastTransactionSummary07=id=="I06"?"提交 +"+(after.stage.remainingSubmissions-before.stage.remainingSubmissions):id=="I07"?"剩掷 +"+(after.hand.rollsRemaining-before.hand.rollsRemaining):id=="I04"?"本手 6 点偏置已应用":id=="I05"?"本手 1 点偏置已应用":id=="I01"?"本次计分有效全红 · 永久骰面不变":id=="I02"?"本关临时骰 +"+(after.dice.Count-before.dice.Count):"本次遥控投掷已提交";
            }
            if(BeginCommittedTransactionVisual07(cmd,result,before,after))
            {
                if(!instant07&&TransactionVisualPlaying07)
                {
                    EffectLayerOrder07();effectReceipt07=result.receipt.commandId;effectOnClock07=false;effectElapsed07=0;effectDuration07=TransactionVisualDuration07;
                    effectOptions07=CreatePresentationOptions07();EffectGroup07("main",true);PresentAcquiredEnchant071(before,after);
                }
                long cashDelta=after.gold-before.gold;
                if(!instant07&&cashDelta!=0&&TransactionVisualPlaying07)
                {
                    EffectLayerOrder07();effectReceipt07=result.receipt.commandId;effectOnClock07=false;effectElapsed07=0;effectDuration07=TransactionVisualDuration07;
                    effectOptions07=CreatePresentationOptions07();EffectGroup07("main",true);
                    // Keep the cash ticket in the header gap between its title and shelf capacity.
                    if(EffectReserve07("main",EffectPoolKind.NumberLabel,1)>0)Effect07("fx_cash_ticket",result.receipt.commandId,new Vector2(744,32),new Vector2(220,58),(cashDelta>0?"+":"")+cashDelta);
                }
                return;
            }
            if(instant07)return;
            ReturnLibraryEffects07(true);EffectLayerOrder07();effectReceipt07=result.receipt.commandId;effectOnClock07=false;effectElapsed07=0;effectDuration07=fast?.15f:.32f;
            effectOptions07=CreatePresentationOptions07();EffectGroup07("main",true);
            PresentBudgetEnchants071(before,after);
            if(cmd.kind==CommandKind.Buy)
            {
                var offer=before.shop?.offers.FirstOrDefault(o=>o.offerId==cmd.offerId);var icon=ContentSprite071(offer?.contentId);
                var v=Effect07("fx_acquire_remove",cmd.offerId,new Vector2(1450,640),new Vector2(120,160),"","已获得");
                if(v!=null){if(icon!=null)v.Bind(cmd.offerId,"","","已获得",icon);else if(v.icon!=null)v.icon.enabled=false;}
            }
            if(cmd.kind==CommandKind.RefreshControllers)Effect07("fx_source_hit",result.receipt.commandId,new Vector2(385,155),new Vector2(1500,322));
            if(cmd.kind==CommandKind.UseItem)
            {
                string id=before.inventory.FirstOrDefault(i=>i.instanceId==cmd.itemInstanceId)?.contentId;
                if(id=="I04"||id=="I05")Effect07("fx_probability_change",result.receipt.commandId,new Vector2(1668,288),new Vector2(180,92),id=="I04"?"6 点偏置":"1 点偏置");
                else if(id=="I01")Effect07("fx_face_change",result.receipt.commandId,new Vector2(1752,284),new Vector2(96,96));
            }
            if(!string.IsNullOrEmpty(LastTransactionSummary07))ShowEffectSummary07(LastTransactionSummary07,true,ShowLastTransactionDetails07);
            long delta=after.gold-before.gold;
            if(delta!=0&&EffectReserve07("main",EffectPoolKind.NumberLabel,1)>0)Effect07("fx_cash_ticket",result.receipt.commandId,new Vector2(52,690),new Vector2(220,58),(delta>0?"+":"")+delta);
        }
        private Art07TransactionMotion transactionMotion07;
        private RectTransform transactionReadout07;
        private string preparedTransactionId07;
        private bool transactionGate07;
        private Action cleanupTransactionView07;
        private string acquiredTransactionInstance07;
        public bool TransactionVisualPlaying07=>transactionMotion07?.IsPlaying==true;
        public float TransactionVisualDuration07=>transactionMotion07?.Duration??0;
        public float TransactionVisualElapsed07=>transactionMotion07?.Elapsed??0;
        public string TransactionVisualKind07=>transactionMotion07?.Kind;
        public int TransactionSnapshotGraphicCount07=>transactionMotion07?.CapturedGraphicCount??0;
        public int TransactionSnapshotMeshCount07=>transactionMotion07?.CreatedSnapshotMeshes??0;
        public string[] TransactionAnimatedTargets07=>transactionMotion07?.AnimatedTargets??Array.Empty<string>();
        private void EnsureTransactionMotion07()
        {
            if(transactionMotion07!=null)return;
            var root=UiKit.Rect(canvas.transform,"Art07TransactionMotion",0,0,1920,1080);
            transactionMotion07=root.gameObject.AddComponent<Art07TransactionMotion>();
            transactionReadout07=UiKit.Rect(root,"CommittedTransactionView",0,0,1920,1080);
        }
        // Root Send calls this after setting command identity, before Execute. Capture stays invisible.
        private void PrepareTransactionVisual07(SessionCommand cmd)
        {
            PrepareStageCardExit07(cmd);
            if(cmd==null||Session==null||Session.Busy||Session.HasPendingSave||cmd.kind!=CommandKind.Buy&&cmd.kind!=CommandKind.RefreshControllers)return;
            EnsureTransactionMotion07();DiscardPreparedTransactionVisual07();
            var before=presentationBefore07;if(before?.phase!="Shop")return;
            var sources=new List<RectTransform>();
            if(cmd.kind==CommandKind.RefreshControllers)
                foreach(var offer in before.shop.offers.Where(o=>o.category=="controller"))sources.AddRange(TransactionOfferRoots07(offer));
            else
            {
                var offer=before.shop.offers.FirstOrDefault(o=>o.offerId==cmd.offerId);
                if(offer!=null)sources.AddRange(TransactionOfferRoots07(offer));
            }
            ((RectTransform)transactionMotion07.transform).anchoredPosition=main.anchoredPosition;
            try{transactionMotion07.Prepare(sources);preparedTransactionId07=cmd.commandId;}
            catch(Exception ex){transactionMotion07.DiscardPrepared();preparedTransactionId07=null;Debug.LogWarning("交易旧画面捕获不可用，显示真实成交状态："+ex.Message);}
        }
        private void DiscardPreparedTransactionVisual07()
        {DiscardStageCardExit07();if(transactionMotion07?.IsPlaying==true)return;transactionMotion07?.DiscardPrepared();preparedTransactionId07=null;}
        private RectTransform FindTransactionRect07(string name)
        {
            return main.GetComponentsInChildren<RectTransform>(true).FirstOrDefault(r=>r.name==name&&r.gameObject.activeInHierarchy);
        }
        private RectTransform[] TransactionOfferRoots07(ShopOffer offer)
        {
            if(offer==null)return Array.Empty<RectTransform>();
            var card=FindTransactionRect07((offer.category=="controller"?"Offer_":"Product_")+offer.offerId);
            if(offer.category!="controller")return card==null?Array.Empty<RectTransform>():new[]{card};
            return new[]{card,FindTransactionRect07("Buy_"+offer.offerId),FindTransactionRect07("OfferRarity_"+offer.offerId)}.Where(r=>r!=null).ToArray();
        }
        private bool BeginCommittedTransactionVisual07(SessionCommand cmd,CommandResult result,RunState before,RunState after)
        {
            if(cmd.kind!=CommandKind.Buy&&cmd.kind!=CommandKind.RefreshControllers)return false;
            EnsureTransactionMotion07();
            LastTransactionSummary07=CommittedTransactionSummary07(cmd,before,after);
            if(result.duplicate||instant07||after.phase!="Shop"){DiscardPreparedTransactionVisual07();if(!result.duplicate)Refresh();return true;}
            if(preparedTransactionId07!=cmd.commandId)transactionMotion07.DiscardPrepared();preparedTransactionId07=null;
            ReturnLibraryEffects07(true);modalTail07?.CancelTail();transactionGate07=true;Session.Busy=true;Refresh();Canvas.ForceUpdateCanvases();
            var rect=(RectTransform)transactionMotion07.transform;rect.anchoredPosition=main.anchoredPosition;rect.SetSiblingIndex(Math.Min(canvas.transform.childCount-1,main.GetSiblingIndex()+2));if(overlay!=null)overlay.SetAsLastSibling();
            UiKit.Clear(transactionReadout07);acquiredTransactionInstance07=null;
            if(cmd.kind==CommandKind.RefreshControllers)
            {
                LastTransactionSummary07="控制器货架已刷新 · 实际支付 "+(before.gold-after.gold)+" · 其他分区保持";
                transactionMotion07.Begin("controller_refresh",.28f,.10f,false,fast,reduced,CompleteTransactionVisual07);
                foreach(var offer in after.shop.offers.Where(o=>o.category=="controller"))foreach(var root in TransactionOfferRoots07(offer))transactionMotion07.TrackReveal(root,.10f,.18f,true);
                return true;
            }
            var bought=before.shop.offers.FirstOrDefault(o=>o.offerId==cmd.offerId);
            if(bought==null){CompleteTransactionVisual07();return true;}
            if(bought.category=="die_upgrade")
            {
                BeginUpgradeTransaction07(cmd,bought,before,after);return true;
            }
            if(bought.contentId=="O05"||bought.contentId=="O06")
            {
                BeginExpansionTransaction07(bought,before,after);return true;
            }
            transactionMotion07.Begin("purchase",.34f,.16f,true,fast,reduced,CompleteTransactionVisual07);
            foreach(var root in TransactionOfferRoots07(after.shop.offers.FirstOrDefault(o=>o.offerId==cmd.offerId)))transactionMotion07.TrackReveal(root,.16f,.001f);
            var controller=after.controllers.FirstOrDefault(c=>!before.controllers.Any(b=>b.instanceId==c.instanceId));
            var item=after.inventory.FirstOrDefault(c=>!before.inventory.Any(b=>b.instanceId==c.instanceId));
            var die=after.dice.FirstOrDefault(c=>!before.dice.Any(b=>b.dieId==c.dieId));
            if(controller!=null)
            {
                acquiredTransactionInstance07=controller.instanceId;FocusTransactionAcquisition07();
                var target=FindTransactionRect07("Controller_"+controller.instanceId);transactionMotion07.TrackReveal(target,.16f,.18f,false,true);
                LastTransactionSummary07="已装备 "+NameOf(controller.definitionId)+" · "+controller.instanceId+" · "+Art07OfferState(controller)+" · "+EnchantShort(controller.enchantmentId);
            }
            else if(item!=null)
            {
                acquiredTransactionInstance07=item.instanceId;var target=FindTransactionRect07("ShopItem_"+item.instanceId);
                if(target==null)target=TransactionAcquisitionCard07(bought,item.instanceId,null);
                transactionMotion07.TrackReveal(target,.16f,.18f,false,true);LastTransactionSummary07="已获得 "+NameOf(item.contentId)+" · "+item.instanceId;
            }
            else if(die!=null)
            {
                var target=TransactionAcquisitionCard07(bought,die.dieId,die);transactionMotion07.TrackReveal(target,.16f,.18f,false,true);
                LastTransactionSummary07="已加入永久骰 "+die.dieId+" · 永久骰 "+before.dice.Count(d=>d.lifetime=="permanent")+"→"+after.dice.Count(d=>d.lifetime=="permanent")+" · 提交容量保持";
            }
            else if(string.IsNullOrEmpty(LastTransactionSummary07))LastTransactionSummary07="已购买 "+NameOf(bought.contentId)+" · 实际净支出 "+(before.gold-after.gold);
            return true;
        }
        private string CommittedTransactionSummary07(SessionCommand cmd,RunState before,RunState after)
        {
            if(cmd.kind==CommandKind.RefreshControllers)return "控制器货架已刷新 · 实际支付 "+(before.gold-after.gold)+" · 其他分区保持";
            var offer=before.shop?.offers.FirstOrDefault(o=>o.offerId==cmd.offerId);if(offer==null)return LastTransactionSummary07;
            if(offer.category=="die_upgrade")return PurchasedDieSummary07(cmd,before,after);
            if(offer.contentId=="O05"||offer.contentId=="O06")
            {
                bool c=offer.contentId=="O05";int oldCount=c?Config.shop.initialControllerOffers+before.controllerOfferPurchases:Config.shop.initialConsumableOffers+before.consumableOfferPurchases;
                int newCount=c?Config.shop.initialControllerOffers+after.controllerOfferPurchases:Config.shop.initialConsumableOffers+after.consumableOfferPurchases;
                bool added=after.shop.offers.Any(o=>o.category==(c?"controller":"consumable")&&!before.shop.offers.Any(b=>b.offerId==o.offerId));
                return (c?"控制器":"道具")+"货架 "+oldCount+"→"+newCount+" · 容量升级已生效"+(added?"":" · 本店暂无合法商品");
            }
            var controller=after.controllers.FirstOrDefault(c=>!before.controllers.Any(b=>b.instanceId==c.instanceId));
            if(controller!=null)return "已装备 "+NameOf(controller.definitionId)+" · "+controller.instanceId+" · "+Art07OfferState(controller)+" · "+EnchantShort(controller.enchantmentId);
            var item=after.inventory.FirstOrDefault(c=>!before.inventory.Any(b=>b.instanceId==c.instanceId));if(item!=null)return "已获得 "+NameOf(item.contentId)+" · "+item.instanceId;
            var die=after.dice.FirstOrDefault(c=>!before.dice.Any(b=>b.dieId==c.dieId));if(die!=null)return "已加入永久骰 "+die.dieId+" · 提交容量保持";
            return "已购买 "+NameOf(offer.contentId)+" · "+(after.gold>before.gold?"净收入 "+(after.gold-before.gold):"净支出 "+(before.gold-after.gold));
        }
        private RectTransform TransactionAcquisitionCard07(ShopOffer offer,string identity,DieInstance die)
        {
            var p=UiKit.Frame(transactionReadout07,"AcquiredInstance07",1440,536,420,360,UiKit.Paper,UiKit.Ink);
            UiKit.Text(p,"Title",die==null?NameOf(offer.contentId):"新增永久骰",16,12,388,58,26,UiKit.Ink,TextAlignmentOptions.Center);
            if(die==null)Icon(p,offer.contentId,154,94,112);
            else for(int i=0;i<6;i++){var face=die.faces[i];Art07PipTile(p,"PhysicalFace"+face.faceId,face.permanentPip,face.baseColor=="red"?UiKit.Red:UiKit.Blue,48+i%3*112,88+i/3*94,76);}
            UiKit.Text(p,"Identity",identity,16,280,388,48,21,UiKit.Ink,TextAlignmentOptions.Center);return p;
        }
        private void FocusTransactionAcquisition07()
        {
            if(acquiredTransactionInstance07==null)return;var target=FindTransactionRect07("Controller_"+acquiredTransactionInstance07);if(target==null)return;
            var scroll=target.GetComponentInParent<UnityEngine.UI.ScrollRect>();if(scroll==null)return;
            float range=scroll.content.rect.height-scroll.viewport.rect.height;
            if(range>0){float top=-target.anchoredPosition.y;scroll.verticalNormalizedPosition=1-Mathf.Clamp01((top-Math.Max(0,scroll.viewport.rect.height-target.rect.height)*.5f)/range);Canvas.ForceUpdateCanvases();}
        }
        private void BeginExpansionTransaction07(ShopOffer bought,RunState before,RunState after)
        {
            bool controllers=bought.contentId=="O05";string category=controllers?"controller":"consumable";
            int oldSlots=controllers?Config.shop.initialControllerOffers+before.controllerOfferPurchases:Config.shop.initialConsumableOffers+before.consumableOfferPurchases;
            int newSlots=controllers?Config.shop.initialControllerOffers+after.controllerOfferPurchases:Config.shop.initialConsumableOffers+after.consumableOfferPurchases;
            var added=after.shop.offers.Where(o=>o.category==category&&!before.shop.offers.Any(b=>b.offerId==o.offerId)).ToArray();
            LastTransactionSummary07=(controllers?"控制器":"道具")+"货架 "+oldSlots+"→"+newSlots+" · 容量升级已生效"+(added.Length==0?" · 本店暂无合法商品":"");
            transactionMotion07.Begin("offer_expansion",.34f,0,false,fast,reduced,CompleteTransactionVisual07);
            if(added.Length>0)
            {
                foreach(var offer in added)
                {
                    var roots=TransactionOfferRoots07(offer);
                    if(roots.Length==0)
                    {
                        int index=Array.FindIndex(after.shop.offers.Where(o=>o.category==category).ToArray(),o=>o.offerId==offer.offerId);
                        var p=UiKit.Frame(transactionReadout07,"AddedOffer_"+offer.offerId,392+Math.Min(4,index)*296,512,280,416,UiKit.Paper,UiKit.Ink);
                        UiKit.Text(p,"Title","新增道具位 "+(index+1),12,12,256,64,24,UiKit.Ink,TextAlignmentOptions.Center);Icon(p,offer.contentId,84,104,112);
                        UiKit.Text(p,"Item",NameOf(offer.contentId),12,236,256,66,24,UiKit.Ink,TextAlignmentOptions.Center);
                        UiKit.Text(p,"Price",Session.GetOfferPrice(offer)+" 金币 · 道具页",12,326,256,66,21,UiKit.Ink,TextAlignmentOptions.Center);roots=new[]{p};
                    }
                    ExpansionFrame07(roots[0]);
                    foreach(var root in roots)transactionMotion07.TrackReveal(root,.16f,.18f,true);
                }
            }
            else
            {
                var empty=controllers?FindTransactionRect07("EmptyControllerOffer"+oldSlots):null;
                if(empty==null){empty=UiKit.Frame(transactionReadout07,"AddedEmptySlot07",392+Math.Min(4,oldSlots)*296,512,280,416,UiKit.Background,UiKit.PanelLight);UiKit.Text(empty,"Empty","本店暂无合法商品\n容量升级已生效",12,84,256,200,25,UiKit.Muted,TextAlignmentOptions.Center);}
                ExpansionFrame07(empty);transactionMotion07.TrackReveal(empty,.16f,.18f,true);
            }
        }
        private void ExpansionFrame07(RectTransform target)
        {
            var corners=new Vector3[4];target.GetWorldCorners(corners);var a=ScreenToDesign(RectTransformUtility.WorldToScreenPoint(null,corners[1]));var b=ScreenToDesign(RectTransformUtility.WorldToScreenPoint(null,corners[3]));
            var frame=UiKit.Rect(transactionReadout07,"NewSlotOutline",a.x,a.y,b.x-a.x,b.y-a.y);UiKit.Outline(frame,"Line",0,0,b.x-a.x,b.y-a.y,UiKit.Gold,3);transactionMotion07.TrackReveal(frame,0,.16f,false,false,true);
        }
        private void BeginUpgradeTransaction07(SessionCommand cmd,ShopOffer bought,RunState before,RunState after)
        {
            var oldDie=before.dice.FirstOrDefault(d=>d.dieId==cmd.targetDieId)?.Clone();var newDie=after.dice.FirstOrDefault(d=>d.dieId==cmd.targetDieId)?.Clone();
            if(oldDie==null||newDie==null){CompleteTransactionVisual07();return;}
            bool whole=bought.contentId=="U05",special=whole||bought.contentId=="U07";float seconds=whole?.42f:.36f;string faceId=whole?"F02":cmd.targetFaceId;
            var oldFace=oldDie.faces.First(f=>f.faceId==faceId);var newFace=newDie.faces.First(f=>f.faceId==faceId);
            var p=UiKit.Rect(transactionReadout07,"PhysicalUpgrade07",392,512,1496,416);
            UiKit.Outline(p,"Frame",0,0,1496,416,UiKit.Gold,3);
            UiKit.Box(p,"LeftPaper",4,4,450,408,UiKit.Panel);UiKit.Box(p,"RightPaper",1014,4,478,408,UiKit.Panel);
            UiKit.Text(p,"Title",NameOf(bought.contentId)+" · 已保存",20,16,418,60,29,UiKit.Paper);
            UiKit.Text(p,"Identity",cmd.targetDieId+"\n"+(whole?"整骰属性":"物理目标 "+faceId),20,90,418,92,25,UiKit.Gold);
            UiKit.Text(p,"Before",(whole?"购买前 · ":"购买前\n")+UpgradeFaceDescription07(oldDie,oldFace,whole),20,194,418,206,23,UiKit.Muted);
            var result=UiKit.Rect(p,"Result",1030,18,446,378);
            UiKit.Text(result,"After",(whole?"购买后 · ":"购买后\n")+UpgradeFaceDescription07(newDie,newFace,whole),0,0,442,218,25,UiKit.Paper);
            var current=after.hand?.currentResults.FirstOrDefault(d=>d.dieId==cmd.targetDieId);
            UiKit.Text(result,"Current",current?.hasRolled==true?"当前投出另计："+current.currentPip+"点 "+ColorText07(current.currentColor):"当前投出：未生成\n未修改其他物理面",0,242,442,118,22,UiKit.Muted);
            UiKit.Text(p,"TargetFace",whole?"整骰玻璃材质":faceId+" · 永久面",504,354,450,48,23,UiKit.Gold,TextAlignmentOptions.Center);
            var focus=UiKit.Rect(p,"TargetOutline",566,70,304,260);UiKit.Outline(focus,"Line",0,0,304,260,UiKit.Gold,3);var focusGroup=UiKit.Component<CanvasGroup>(focus.gameObject);
            var wipe=UiKit.Box(p,"MaterialWipe",568,72,300,3,UiKit.Gold);
            Vector2 center=new Vector2(392+718,512+202);Quaternion rotation=Quaternion.AngleAxis(18,-table.viewCamera.transform.forward)*Quaternion.FromToRotation(DiceTable.Normals[DiceTable.FaceIndex(faceId)],-table.viewCamera.transform.forward);
            if(whole)rotation=Quaternion.AngleAxis(-22,table.viewCamera.transform.right)*Quaternion.AngleAxis(28,table.viewCamera.transform.up)*Quaternion.FromToRotation(DiceTable.Normals[DiceTable.FaceIndex(faceId)],-table.viewCamera.transform.forward);
            MeshRenderer body=null,target=null;var bodyOriginal=new MaterialPropertyBlock();var props=new MaterialPropertyBlock();bool captured=false;
            if(special)BeginSpecialUpgradePreview07(oldDie,newDie,faceId,whole,center,rotation);
            else BeginUpgradeFaces07(oldDie,newDie,center,rotation,reduced);
            Action<float> tick=time=>
            {
                float transitionTime=reduced?transactionMotion07.Elapsed:time;float blend=reduced?Mathf.Clamp01(transitionTime/.08f):Mathf.Clamp01((time-.06f)/.24f);
                table.ShowPreview(newDie,center,rotation,2.0f);
                if(special)
                {
                    float progress=reduced?Mathf.Clamp01(transactionMotion07.Elapsed/.12f):Mathf.Clamp01(time/seconds);
                    upgradeSurface07.SetProgress(progress,reduced);focusGroup.alpha=reduced?1:time<=.1f?1:.35f;wipe.gameObject.SetActive(false);return;
                }
                permanentTransition07?.SetTime(transitionTime);
                var orientation=table.GetFaceOrientation("$die-preview");
                if(!captured&&orientation!=null){body=orientation.Find("Body").GetComponent<MeshRenderer>();target=orientation.Find("Face_"+faceId).GetComponent<MeshRenderer>();body.GetPropertyBlock(bodyOriginal);captured=true;}
                if(body!=null){body.GetPropertyBlock(props);props.SetFloat("_Glass",Mathf.Lerp(oldDie.isGlass?1:0,newDie.isGlass?1:0,blend));body.SetPropertyBlock(props);}
                if(target!=null){target.GetPropertyBlock(props);props.SetColor("_BaseColor",Color.Lerp(oldFace.baseColor=="red"?UiKit.Red:UiKit.Blue,newFace.baseColor=="red"?UiKit.Red:UiKit.Blue,blend));target.SetPropertyBlock(props);}
                focusGroup.alpha=reduced?1:time<=.1f?1:.35f;wipe.gameObject.SetActive(!reduced&&time>=.06f&&time<=.3f);wipe.anchoredPosition=new Vector2(568,-(72+256*blend));
            };
            cleanupTransactionView07=()=>{upgradeSurface07?.Finish();permanentTransition07?.Finish();if(body!=null)body.SetPropertyBlock(bodyOriginal);if(target!=null){target.GetPropertyBlock(props);props.SetColor("_BaseColor",newFace.baseColor=="red"?UiKit.Red:UiKit.Blue);target.SetPropertyBlock(props);}table.HidePreview();};
            transactionMotion07.Begin("die_upgrade",seconds,0,false,fast,reduced,CompleteTransactionVisual07,tick);
            transactionMotion07.HideUntilFinished(FindTransactionRect07("ShopProducts"));transactionMotion07.TrackReveal(result,.18f,.18f);
        }
        private static string UpgradeFaceDescription07(DieInstance die,FaceState face,bool whole)
        {return (whole?"骰体："+(die.isGlass?"玻璃":"陶瓷")+"\n":"")+face.faceId+"  "+face.permanentPip+"点 · "+ColorText07(face.baseColor)+"\n"+TextureText07(face.texture)+" · "+(face.isGold?"黄金边":"普通边")+"\n物理权重 "+face.baseWeight;}
        private void CompleteTransactionVisual07()
        {
            cleanupTransactionView07?.Invoke();cleanupTransactionView07=null;if(transactionReadout07!=null)UiKit.Clear(transactionReadout07);
            if(transactionGate07){transactionGate07=false;if(Session!=null)Session.Busy=false;Refresh();FocusTransactionAcquisition07();}
        }

        public void ShowLastTransactionDetails07()
        {
            if(string.IsNullOrEmpty(LastTransactionSummary07))return;
            var p=OpenModal("已提交变化 · 只读");var list=UiKit.Scroll(p,"TransactionChanges07",32,106,1536,630,630);
            var text=UiKit.Text(list,"Changes07",LastTransactionSummary07.Replace(" · ","\n"),8,0,1488,624,24,UiKit.Paper);
            text.textWrappingMode=TextWrappingModes.Normal;list.sizeDelta=new Vector2(list.sizeDelta.x,FitHeight(text,624));
        }
        private string BudgetGrantSummary07(RunState before,RunState after)
        {
            if(after==null)return "";var parts=new List<string>();
            Func<string,string> source=id=>{var c=after.controllers.FirstOrDefault(x=>x.instanceId==id);return c==null?id:NameOf(c.definitionId)+"（"+id+"）";};
            if(after.stage?.started==true&&(before?.stage?.started!=true||before.stage.stageIndex!=after.stage.stageIndex))
                foreach(var id in after.stage.submissionBonusSourceIds)parts.Add(source(id)+" 提交 +"+Config.enchantRules.submissionBonus);
            if(after.hand!=null&&after.hand.handId!=before?.hand?.handId)
                foreach(var id in after.hand.rollBonusSourceIds)parts.Add(source(id)+" 剩掷 +"+Config.enchantRules.rollBonus);
            return string.Join(" · ",parts);
        }
        private string PurchasedDieSummary07(SessionCommand cmd,RunState before,RunState after)
        {
            var prior=before.dice.FirstOrDefault(d=>d.dieId==cmd.targetDieId);var next=after.dice.FirstOrDefault(d=>d.dieId==cmd.targetDieId);
            if(prior==null||next==null)return "";
            var changes=new List<string>();
            if(prior.isGlass!=next.isGlass)changes.Add("永久玻璃 "+(next.isGlass?"开启":"关闭"));
            foreach(var face in next.faces)
            {
                var old=prior.faces.FirstOrDefault(f=>f.faceId==face.faceId);if(old==null)continue;var fields=new List<string>();
                if(old.permanentPip!=face.permanentPip)fields.Add("点数 "+old.permanentPip+"→"+face.permanentPip);
                if(old.baseColor!=face.baseColor)fields.Add("颜色 "+ColorText07(old.baseColor)+"→"+ColorText07(face.baseColor));
                if(old.baseWeight!=face.baseWeight)fields.Add("权重 "+old.baseWeight+"→"+face.baseWeight);
                if(old.texture!=face.texture)fields.Add("纹理 "+TextureText07(old.texture)+"→"+TextureText07(face.texture));
                if(old.isGold!=face.isGold)fields.Add("黄金 "+(face.isGold?"开启":"关闭"));
                if(fields.Count>0)changes.Add(face.faceId+" 永久："+string.Join("，",fields));
            }
            var current=after.hand?.currentResults.FirstOrDefault(d=>d.dieId==cmd.targetDieId);
            if(changes.Count>0&&current?.hasRolled==true)changes.Add("当前投出："+current.currentPip+"点 "+ColorText07(current.currentColor));
            return changes.Count==0?"":cmd.targetDieId+" · "+string.Join(" · ",changes);
        }
        private static string ColorText07(string color)=>color=="red"?"红":color=="blue"?"蓝":color;
        private static string TextureText07(string texture)=>texture=="rough"?"粗糙":texture=="smooth"?"光滑":"普通";
        private void EvaluatePermanentStamps07(float seconds)
        {
            float start=effectOptions07.ReducedMotion||effectOptions07.Speed==PresentationSpeed.Fast?0:.22f;
            float end=effectOptions07.ReducedMotion?.08f:effectOptions07.Speed==PresentationSpeed.Fast?.14f:.36f;
            float mix=Mathf.Clamp01((seconds-start)/(end-start));
            foreach(var pair in permanentStamps07)
            {
                if(pair.Stamp!=null)pair.Stamp.alpha=1-mix;
                if(pair.Prefix!=null)pair.Prefix.alpha=mix;
            }
        }
        private void TickLibraryEffects07(float visualDelta)
        {
            if(modal)return;
            transactionMotion07?.Tick(visualDelta,false);
            if(IsPresenting07&&beat07?.Phase==PresentationPhase.PostScore)
            {
                float elapsed=(float)(presentationPlayer07.Clock.ElapsedMilliseconds-beat07.StartMs)/1000;
                permanentTransition07?.SetTime(elapsed);EvaluatePermanentStamps07(elapsed);
            }
            for(int i=cardTails07.Count-1;i>=0;i--)
            {
                var tail=cardTails07[i];float elapsed=(float)(presentationPlayer07.Clock.ElapsedMilliseconds-tail.StartedMs)/1000;
                if(tail.Card==null||reduced||elapsed>=.22f){if(tail.Card!=null)tail.Card.localScale=Vector3.one;cardTails07.RemoveAt(i);continue;}
                tail.Card.localScale=Vector3.one*(elapsed<.06f?Mathf.Lerp(1,.98f,elapsed/.06f):elapsed<.14f?Mathf.Lerp(.98f,1.03f,(elapsed-.06f)/.08f):Mathf.Lerp(1.03f,1,(elapsed-.14f)/.08f));
            }
            if(activeEffects07.Count==0&&effectReceipt07==null)return;
            float t=Mathf.Clamp01((effectOnClock07?(float)(presentationPlayer07.Clock.ElapsedMilliseconds-(beat07?.StartMs??0))/1000:(effectElapsed07+=visualDelta))/Math.Max(.001f,effectDuration07));
            foreach(var v in activeEffects07)v.Evaluate(effectLocalDurations07.TryGetValue(v,out float local)?Mathf.Clamp01(t*effectDuration07/local):t);
            TickEnchantCues071(t);
            foreach(var outline in brokenOutlines07)if(outline.Value!=null)outline.Value.alpha=1-t;
            if(!effectOnClock07&&t>=1)ReturnLibraryEffects07(true);
        }
        private void FinishTransactionEffects07(){if(qualifiedStamp07?.IsPlaying==true)qualifiedStamp07.Finish();transactionMotion07?.Finish();cleanupTransactionView07?.Invoke();cleanupTransactionView07=null;ReturnLibraryEffects07(true);}
        private void ReturnLibraryEffects07(bool clearTails=true)
        {
            foreach(var cue in enchantCues071)cue.ResetForPool();
            enchantTargets071.Clear();
            permanentTransition07?.Finish();unchangedPermanentTargets07.Clear();brokenOutlines07.Clear();effectLocalDurations07.Clear();permanentStamps07.Clear();
            foreach(var view in activeEffects07){effectPools07[view.assetId].Return(view);if(view.transform is RectTransform)view.transform.SetParent(libraryEffects07,false);}activeEffects07.Clear();
            if(effectLabels07!=null)UiKit.Clear(effectLabels07);if(sourceMarks07!=null)UiKit.Clear(sourceMarks07);sourceMarkCount07=0;
            if(clearTails){foreach(var tail in cardTails07)if(tail.Card!=null)tail.Card.localScale=Vector3.one;cardTails07.Clear();}
            if(effectReceipt07!=null)effectBudget07.ReleaseReceipt(effectReceipt07);effectReceipt07=null;
        }
    }
}
