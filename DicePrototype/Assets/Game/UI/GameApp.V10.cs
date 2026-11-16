using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    // p1.0.0 (docs/specs/v10/RULES_SPEC.md section 9): the three-stage cycle panel on the stage-select screen and in
    // the shop, the boss modifier readout in a stage, and the dialog for boss loot and controller gifts.
    public sealed partial class GameApp
    {
        private const float CycleW10=488,CycleH10=380,CycleGap10=18;

        // ---------------------------------------------------------------- shared state helpers
        private bool InStageView10 => ViewState?.stage!=null&&ViewState.stage.started&&!ViewState.stage.settled&&(ViewState.phase=="HandReady"||ViewState.phase=="Selecting");
        /// <summary>Controllers the boss modifier has disabled for the stage being played.</summary>
        private string[] ViewDisabled10 => InStageView10?ViewState.stage.disabledControllerIds:Array.Empty<string>();
        private bool DisabledNow10(string instanceId)=>ViewDisabled10.Contains(instanceId);
        /// <summary>Before a boss stage: the controllers its modifier will disable when the stage starts.</summary>
        private string[] DisabledNextIds10()
        {
            if(ViewState==null)return Array.Empty<string>();
            if(ViewState.phase=="StagePreview")return Session.EntryView()?.disabledControllerIds??Array.Empty<string>();
            if(ViewState.phase!="Shop")return Array.Empty<string>();
            int next=ViewState.stage.stageIndex+1;
            if(next>=Config.stages.Length||Config.stages[next].stageKind!="boss"||next/3>=ViewState.cycles.Count)return Array.Empty<string>();
            var after=ControllerRules.AfterAltars(ViewState.controllers);
            return after.Any(c=>c.definitionId=="C131")?Array.Empty<string>():ControllerRules.DisabledBy(after,Config,ViewState.cycles[next/3].modifierId);
        }
        private bool DisabledNext10(string instanceId)=>DisabledNextIds10().Contains(instanceId);
        /// <summary>What an owned controller sells for, including the recycler (C140).</summary>
        private long OwnedSalePrice10(ControllerInstance c)=>Session.ControllerSalePriceIn(ViewState,c);
        /// <summary>
        /// Names what raised the interest principal cap of a settlement. The cap itself comes from the receipt; the share
        /// of the adviser service (O10) is its purchase count times the configured step, the rest is the snowball card (C96).
        /// </summary>
        private string InterestCapNote10(StageSettlement r)
        {
            if(r.interestPrincipalCap<=Config.maxInterestPrincipal)return "";
            long adviser=(long)Config.interestPrincipalStep*ViewState.interestCapPurchases,snowball=r.interestPrincipalCap-Config.maxInterestPrincipal-adviser;
            var names=new System.Collections.Generic.List<string>();if(snowball>0)names.Add(NameOf("C96"));if(adviser>0)names.Add(NameOf("O10"));
            return "（"+string.Join("、",names)+"：计息本金上限 "+r.interestPrincipalCap+"）";
        }
        private string StageKindLabel10(int stageIndex,bool risk)
        {
            string kind=Config.stages[Mathf.Clamp(stageIndex,0,Config.stages.Length-1)].stageKind;
            return kind=="boss"?"首领关":kind=="pressure"?(risk?"高压挑战":"高压关"):"普通关";
        }
        private static Color KindTint10(string kind)=>kind=="boss"?UiKit.Danger:kind=="pressure"?UiKit.Red:UiKit.Teal;
        private string LootText10(string lootKind)=>string.IsNullOrEmpty(lootKind)?"终局首领 · 通关即胜利":NameOf(lootKind)+"："+Description(lootKind);

        // ---------------------------------------------------------------- sealed dice, must-fill, extra score factors
        /// <summary>Which locked dice the boss modifier seals in the hand as it stands (empty without a sealing modifier).</summary>
        private bool[] SealedPreview10()
        {
            if(!InStageView10||ViewState.phase!="Selecting"||Session.CurrentModifierId==null||ViewState.hand.orderedLockedIds.Count==0)return Array.Empty<bool>();
            return Session.PreviewScoreRange()?.minimum?.sealedDice??Array.Empty<bool>();
        }
        private void SealedMark10(float x,float y,float w,float h,int index)
        {
            UiKit.Outline(main,"SealedSlot10_"+index,x+3,y+3,w-6,h-6,UiKit.Danger,2);
            var tag=UiKit.Text(main,"SealedTag10_"+index,"封印",x+6,y+h-30,64,26,15,UiKit.Danger);tag.fontStyle=FontStyles.Bold;tag.textWrappingMode=TextWrappingModes.NoWrap;
        }
        private bool MustFillBlocked10 => InStageView10&&ControllerRules.ModifierHas(Config,Session.CurrentModifierId,"MustFill")
            &&ViewState.hand.orderedLockedIds.Count!=Math.Min(ViewState.stage.capacity,ViewState.dice.Count);
        /// <summary>The item multiplier and the boss half-score factor, appended to the score formula when present.</summary>
        private string GlobalFactorText10(ScoreBreakdown b)
        {
            if(b.globalFactors==null||b.globalFactors.Length==0)return "";
            // v1.1 (W09): the boss half reads "0.5", never "1/2".
            return string.Concat(b.globalFactors.Select(f=>" × "+NameOf(f.abilityId)+" "+PlayerNumberFormat.Ratio(ExactScoreMath.Parse(f.numerator),ExactScoreMath.Parse(f.denominator),PlayerNumberFormat.Unlimited)));
        }

        // ---------------------------------------------------------------- in-stage boss readout
        private void DrawModifierChip10(Transform panel,float width)
        {
            if(!InStageView10||ViewState.stage.stageKind!="boss"||string.IsNullOrEmpty(ViewState.stage.modifierId))return;
            bool lifted=ViewState.stage.modifierNullified;string id=ViewState.stage.modifierId;
            // The readout sits in the free band of the left column, between the budget counters and the buttons. (Its first
            // version was a 164 px box lower down, which the previous-hand, handbook and pause buttons covered.) The text
            // shrinks to fit two lines; a click or hover gives the full stage intel.
            var box=UiKit.Frame(panel,"BossModifier10",12,709,width-24,67,Color.Lerp(UiKit.Background,UiKit.Danger,lifted?.04f:.16f),lifted?UiKit.PanelLight:UiKit.Danger);
            // The title row is 30 px for a 16 px face: a row shorter than the line height renders nothing at all in ellipsis mode.
            var title=UiKit.Text(box,"BossModifierName",(lifted?"减益已解除 · ":"首领减益 · ")+NameOf(id),10,0,width-44,30,16,lifted?UiKit.Muted:UiKit.Paper);
            title.fontStyle=FontStyles.Bold;title.textWrappingMode=TextWrappingModes.NoWrap;title.overflowMode=TextOverflowModes.Overflow;
            var body=UiKit.Text(box,"BossModifierText",Description(id),10,27,width-44,38,14,lifted?UiKit.Muted:UiKit.TextColor);
            body.enableAutoSizing=true;body.fontSizeMin=11;body.fontSizeMax=14;body.overflowMode=TextOverflowModes.Ellipsis;body.lineSpacing=-8;
            var open=HitArea073(box,"BossModifierOpen",0,0,width-24,67,()=>{if(!IsBusy)ShowCycle10();});
            Art07AttachTooltip(open,"boss-modifier",NameOf(id),Description(id),lifted?"已解除":"本关生效",()=>{if(!IsBusy)ShowCycle10();});
        }

        // ---------------------------------------------------------------- stage-select screen
        private void StagePreview10()
        {
            // V1.1 (W11): the stage select shares the play screen's top bar and card row, so the cards do not jump when the
            // stage starts; its own content is centred below them (the left status column is gone).
            DrawCards11();
            const float shift=-160;
            for(int chapter=0;chapter<Config.stages.Length/3;chapter++)
            {
                float left=380+shift+chapter*188;bool currentChapter=chapter==ViewState.stage.stageIndex/3;
                var caption=UiKit.Text(main,"StageChapter"+chapter,"第 "+(chapter+1)+" 章",left,340,164,40,17,currentChapter?UiKit.Gold:UiKit.Muted,TextAlignmentOptions.Center);
                caption.fontStyle=currentChapter?FontStyles.Bold:FontStyles.Normal;
                UiKit.Box(main,"StageChapterRule"+chapter,left,412,164,currentChapter?3:1,currentChapter?UiKit.Gold:UiKit.PanelLight);
                for(int within=0;within<3;within++)
                {
                    int index=chapter*3+within;float x=left+within*56;
                    bool complete=index<ViewState.stage.stageIndex,current=index==ViewState.stage.stageIndex,boss=within==2;
                    Color fill=current?UiKit.Gold:complete?Color.Lerp(UiKit.Felt,UiKit.Teal,.35f):boss?Color.Lerp(UiKit.Felt,UiKit.Danger,.28f):UiKit.Felt;
                    var tick=UiKit.Box(main,"StageTick"+index,x,380,48,28,fill).GetComponent<Image>();
                    tick.sprite=UiKit.Template("ui_frame_block");tick.type=tick.sprite!=null?Image.Type.Sliced:Image.Type.Simple;tick.pixelsPerUnitMultiplier=1.6f;
                    UiKit.Text(main,"StageTickLabel"+index,complete?"✓":(index+1).ToString(),x,380,48,28,12,current?UiKit.Ink:complete?UiKit.Teal:boss?UiKit.Paper:UiKit.Muted,TextAlignmentOptions.Center);
                    if(current)UiKit.Outline(main,"CurrentStageOutline",x-2,378,52,32,Color.clear,1);
                }
            }
            DrawCycleCards10(main,380+shift-10,432,true);
            UiKit.Text(main,"PreviewBudgets",$"投掷 {Session.InitialRolls} 次 / 手     提交 {Session.InitialSubmissions} 次 / 关     提交栏 {Session.EffectiveCapacity} 格",416+shift,822,1380,48,26,UiKit.Muted);
            var doomed=ControllerRules.AltarPlan(ViewState.controllers).Select(p=>NameOf(ViewState.controllers.First(c=>c.instanceId==p.Value).definitionId)).ToArray();
            var entry=Session.EntryView();
            var disabled=(entry?.disabledControllerIds??Array.Empty<string>()).Select(id=>ViewState.controllers.FirstOrDefault(c=>c.instanceId==id)).Where(c=>c!=null).Select(c=>NameOf(c.definitionId)).ToArray();
            string warning="入关后，控制器顺序固定。";
            if(doomed.Length>0)warning+="开始后祭坛将摧毁："+string.Join("、",doomed)+"（可先在上方拖动调序）";
            if(disabled.Length>0)warning+=(doomed.Length>0?"\n":"")+"首领减益会让这些卡本关失效："+string.Join("、",disabled)+"（可先在上方拖动调序）";
            if(entry!=null&&entry.scapegoat)warning+=(doomed.Length>0?"\n":"")+"替罪羊会在入关时自毁，解除首领减益。";
            var note=UiKit.Text(main,"ControllerOrderWarning",warning,416+shift,870,1380,62,doomed.Length>0||disabled.Length>0?18:20,doomed.Length>0||disabled.Length>0?UiKit.Danger:UiKit.Gold);
            note.lineSpacing=-6;
            UiKit.ActionButton(main,"StartStage","开始挑战 →",600,936,720,76,StartPreviewStage07,UiKit.Primary);
            BindStageCardsEntrance07();
        }

        /// <summary>The three stage cards of the cycle that holds the next stage. Only the stage-select screen is interactive.</summary>
        private void DrawCycleCards10(Transform parent,float left,float top,bool interactive)
        {
            var view=Session.CycleView();var entry=interactive?Session.EntryView():null;
            for(int i=0;i<view.Length;i++)CycleCard10(parent,view[i],i,left+i*(CycleW10+CycleGap10),top,interactive,entry);
        }
        private void CycleCard10(Transform parent,CycleStageView v,int slot,float x,float y,bool interactive,StageEntryView entry)
        {
            bool own=interactive&&v.current;Color tint=KindTint10(v.stageKind);const float inner=CycleW10-48;
            var p=UiKit.PaperFrame(parent,"CycleCard"+slot,x,y,CycleW10,CycleH10,UiKit.Paper,UiKit.Ink);
            if(v.current)UiKit.Ring(p,"CurrentCycleCard",-4,-4,CycleW10+5,CycleH10+4,UiKit.Gold);
            UiKit.Box(p,"KindStrip",24,16,inner,4,tint);
            var title=UiKit.Text(p,"Title","第 "+(v.stageIndex+1)+" 关",24,24,280,58,30,UiKit.Ink);title.fontStyle=FontStyles.Bold;title.textWrappingMode=TextWrappingModes.NoWrap;
            var stamp=UiKit.Rect(p,"KindSeal",CycleW10-142,32,116,44);if(v.stageKind!="normal")stamp.localRotation=Quaternion.Euler(0,0,-6);
            UiKit.Outline(stamp,"StampBorder",0,0,116,44,tint,2);
            UiKit.Text(stamp,"KindTag",v.stageKind=="boss"?"首领":v.stageKind=="pressure"?"高压":"普通",4,0,108,44,18,tint,TextAlignmentOptions.Center);
            UiKit.Box(p,"Rule",24,90,inner,2,Color.Lerp(UiKit.Ink,UiKit.Paper,.65f));
            if(v.stageKind=="pressure")
            {
                bool choseRisk=own?PreviewRisk07:v.risk;
                CycleOption10(p,"Normal","普通挑战",v.normalTarget,v.normalReward,100,own,!choseRisk,false);
                CycleOption10(p,"Risk","高压挑战",v.riskTarget,v.riskReward,194,own,choseRisk,true);
                var hint=UiKit.Text(p,"PressureHint",own?"选一种挑战再开始。高压：目标 ×1.5，过关金币 +5。":v.cleared?(v.risk?"已完成高压挑战。":"已按普通挑战通过。"):"到这一关时再选择。高压：目标 ×1.5，过关金币 +5。",24,292,inner,72,16,PaperMuted071);
                hint.overflowMode=TextOverflowModes.Ellipsis;hint.lineSpacing=-6;
            }
            else if(v.stageKind=="boss")
            {
                bool lifted=v.modifierNullified||(own&&entry!=null&&entry.scapegoat);
                string target=BossTarget10(v,lifted);
                UiKit.Text(p,"GoalLabel",target!=v.normalTarget?"目标分数（已含首领减益）":"目标分数",24,96,inner,28,16,PaperMuted071);
                string compact=CompactScore(target);
                var goal=UiKit.Text(p,"Goal",compact,24,120,inner,54,compact.Length>10?26:UiKit.FontScale>1?28:36,UiKit.Ink);goal.fontStyle=FontStyles.Bold;goal.textWrappingMode=TextWrappingModes.NoWrap;
                UiKit.Text(p,"ModifierLabel",lifted?"首领减益（已解除）":"首领减益",24,176,inner,26,16,PaperMuted071);
                var name=UiKit.Text(p,"ModifierName",NameOf(v.modifierId),24,198,inner,38,24,lifted?PaperMuted071:UiKit.Danger);name.fontStyle=FontStyles.Bold;name.textWrappingMode=TextWrappingModes.NoWrap;
                var text=UiKit.Text(p,"ModifierText",Description(v.modifierId),24,236,inner,58,16,lifted?PaperMuted071:UiKit.Ink);text.overflowMode=TextOverflowModes.Ellipsis;text.lineSpacing=-6;
                if(own&&entry!=null&&entry.scapegoat)UiKit.Text(p,"ModifierLift","替罪羊会在入关时解除它",24,294,inner,24,15,UiKit.Teal);
                UiKit.Box(p,"LootRule",24,320,inner,1,Color.Lerp(UiKit.Ink,UiKit.Paper,.65f));
                var loot=UiKit.Text(p,"Loot","战利品 · "+LootText10(v.lootKind),24,324,inner,48,15,UiKit.Ink);loot.overflowMode=TextOverflowModes.Ellipsis;loot.lineSpacing=-6;
            }
            else
            {
                UiKit.Text(p,"GoalLabel","目标分数",24,104,inner,34,18,PaperMuted071);
                string compact=CompactScore(tutorial&&own?"50":v.normalTarget);
                var goal=UiKit.Text(p,"Goal",compact,24,138,inner,82,compact.Length>10?30:UiKit.FontScale>1?34:44,UiKit.Ink);goal.fontStyle=FontStyles.Bold;goal.textWrappingMode=TextWrappingModes.NoWrap;
                UiKit.Text(p,"Reward","过关奖励  +"+v.normalReward+" 金币",24,224,inner,50,23,UiKit.Ink);
                if(own)
                {
                    // The single choice stays a real button, as on the old card: it keeps keyboard focus and the tests' hot zone.
                    var select=UiKit.FlatButton(p,"Normal","✓ 已选择",24,292,inner,60,()=>ChoosePreview07(false),true,true);
                    select.GetComponentInChildren<TextMeshProUGUI>().color=UiKit.Ink;((Image)select.targetGraphic).color=Color.Lerp(UiKit.Paper,UiKit.Gold,.45f);
                }
                else{var hint=UiKit.Text(p,"NormalHint","没有额外条件。",24,292,inner,60,16,PaperMuted071);hint.overflowMode=TextOverflowModes.Ellipsis;}
            }
            if(v.cleared)
            {
                var veil=UiKit.Box(p,"ClearedVeil",6,6,CycleW10-16,CycleH10-18,new Color(UiKit.Paper.r,UiKit.Paper.g,UiKit.Paper.b,.5f));veil.GetComponent<Image>().raycastTarget=false;
                var done=UiKit.Text(p,"Cleared","✓ 已通过",24,60,200,28,16,UiKit.Teal);done.fontStyle=FontStyles.Bold;
            }
        }
        /// <summary>One row of the pressure card: a button on the stage-select screen, a plain row elsewhere.</summary>
        private void CycleOption10(Transform card,string id,string title,string target,int reward,float y,bool selectable,bool selected,bool risk)
        {
            const float w=CycleW10-48,h=86,state=150;Transform row;float textWidth=w-28;
            if(selectable)
            {
                // The button's own label carries the choice state on the right; title and numbers sit on the left.
                var button=UiKit.FlatButton(card,id,selected?"✓ 已选择":"选择挑战",24,y,w,h,()=>ChoosePreview07(risk),true,selected);
                ((Image)button.targetGraphic).color=selected?Color.Lerp(UiKit.Paper,UiKit.Gold,.45f):PaperMuted071;row=button.transform;
                var label=row.Find("Label").GetComponent<TextMeshProUGUI>();label.color=UiKit.Ink;label.fontSize=17*UiKit.FontScale;label.textWrappingMode=TextWrappingModes.NoWrap;
                label.rectTransform.anchoredPosition=new Vector2(w-state-8,0);label.rectTransform.sizeDelta=new Vector2(state,h);textWidth=w-28-state;
            }
            else
            {
                var frame=UiKit.Box(card,id+"Row",24,y,w,h,selected?Color.Lerp(UiKit.Paper,UiKit.Gold,.3f):new Color(0,0,0,.06f));row=frame;
            }
            var name=UiKit.Text(row,"OptionTitle",title,14,4,textWidth,38,22,UiKit.Ink);name.fontStyle=FontStyles.Bold;name.textWrappingMode=TextWrappingModes.NoWrap;
            var info=UiKit.Text(row,"OptionInfo","目标 "+CompactScore(target)+" · +"+reward+" 金币",14,44,textWidth,36,17,UiKit.Ink);info.textWrappingMode=TextWrappingModes.NoWrap;info.overflowMode=TextOverflowModes.Ellipsis;
            if(risk)UiKit.Box(row,"RiskMark",w-6,10,4,h-20,UiKit.Red);
        }

        // ---------------------------------------------------------------- shop: stage intel and gifts
        private void ShopCycleAndGifts10()
        {
            UiKit.ActionButton(main,"ViewCycle","查看关卡",1396,8,236,56,ShowCycle10,UiKit.Paper,!IsBusy);
            var groups=ViewState.shop.groups;if(groups==null||groups.Count==0)return;
            bool open=groups.Any(g=>!g.closed),loot=groups.Any(g=>g.groupId=="LOOT");
            UiKit.ActionButton(main,"ViewGifts",loot?"首领战利品":"商店赠礼",1650,8,246,56,ShowGifts10,open?UiKit.Gold:UiKit.Paper,!IsBusy);
            if(open)UiKit.Box(main,"GiftsPending10",1880,4,14,14,UiKit.Danger);
        }
        private void ShowCycle10()
        {
            if(IsBusy)return;var view=Session.CycleView();if(view.Length==0)return;
            var p=Art07Modal("关卡情报 · 第 "+(view[0].stageIndex/3+1)+" 章",150,150,1620,690);
            DrawCycleCards10(p,60,104,false);
            var names=DisabledNextIds10().Select(id=>ViewState.controllers.FirstOrDefault(c=>c.instanceId==id)).Where(c=>c!=null).Select(c=>NameOf(c.definitionId)).ToArray();
            string note="首领的减益在周期开始时就已确定。可以趁现在调整构筑、控制器顺序，或准备破咒符与替罪羊。";
            if(names.Length>0)note="按现在的顺序，下一关首领会让这些卡失效："+string.Join("、",names)+"。可以拖动调序。";
            var hint=UiKit.Text(p,"CycleHint",note,60,500,1500,64,20,names.Length>0?UiKit.Danger:UiKit.Muted);hint.overflowMode=TextOverflowModes.Ellipsis;
            UiKit.ActionButton(p,"CloseCycle","知道了",660,590,300,64,CloseModal,UiKit.Paper);
        }
        private void ShowGifts10()
        {
            if(IsBusy||ViewState.shop==null||ViewState.shop.groups.Count==0)return;
            var groups=ViewState.shop.groups;const float cardW=384,cardH=312,gap=18;
            var p=Art07Modal("战利品与赠礼",110,80,1700,920);
            var list=UiKit.Scroll(p,"GiftList",30,96,1640,800,Math.Max(800,groups.Count*(56+cardH+26)));float y=0;
            foreach(var group in groups)
            {
                string title=group.groupId=="LOOT"?"首领战利品 · "+NameOf(group.kind):NameOf(group.kind)+"的赠礼";
                string status=group.closed?"已领完":"还可以领 "+(group.picks-group.picked)+" 件";
                var heading=UiKit.Text(list,"GiftTitle_"+group.groupId,title+"　　"+status,8,y,1560,48,26,group.closed?UiKit.Muted:UiKit.Gold);heading.fontStyle=FontStyles.Bold;heading.textWrappingMode=TextWrappingModes.NoWrap;
                y+=56;
                var offers=ViewState.shop.offers.Where(o=>o.groupId==group.groupId).ToArray();
                for(int i=0;i<offers.Length;i++)GiftCard10(list,offers[i],group,8+i*(cardW+gap),y,cardW,cardH);
                y+=cardH+26;
            }
            list.sizeDelta=new Vector2(list.sizeDelta.x,Math.Max(800,y));
        }
        private void GiftCard10(Transform parent,ShopOffer offer,ShopGroup group,float x,float y,float w,float h)
        {
            var p=UiKit.PaperFrame(parent,"Product_"+offer.offerId,x,y,w,h,UiKit.Paper);ShopCardSurface073(p.GetComponent<Image>());
            string name=offer.category=="gold"?"折现":offer.category=="new_die"?"特制骰子":NameOf(offer.contentId);
            var label=UiKit.Text(p,"Name",name,12,8,w-24,40,22,UiKit.Ink,TextAlignmentOptions.Center);label.fontStyle=FontStyles.Bold;label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;
            string text;
            if(offer.category=="gold"){var coin=UiKit.Box(p,"Coin",(w-72)*.5f,56,72,72,Color.white).GetComponent<Image>();coin.sprite=UiKit.Template("ui_icon_coin");coin.raycastTarget=false;text="放弃这一组，换成 "+offer.basePrice+" 金币。只能在还没领取时选择。";}
            else if(offer.category=="new_die")text=string.Join("\n",offer.die.faces.Select(f=>f.faceId.Substring(1).TrimStart('0')+" 面："+f.permanentPip+" 点 "+(f.baseColor=="red"?"红":"蓝")+(f.isGold?" · 黄金":"")));
            else if(offer.category=="hand_upgrade")
            {
                bool color=Config.colorDefinitions.Any(d=>d.id==offer.contentId);int level=(color?ViewState.colorLevels:ViewState.handLevels).First(l=>l.handId==offer.contentId).level;
                int max=color?Config.maxColorLevel:Config.Hand(offer.contentId).maxLevel,next=Math.Min(max,level+Math.Max(1,offer.levels));
                int basis=color?Config.Color(offer.contentId).baseMult100:Config.Hand(offer.contentId).baseMult100,step=color?Config.Color(offer.contentId).incrementPerLevel100:Config.Hand(offer.contentId).incrementPerLevel100;
                text="直接升级：Lv."+level+" → Lv."+next+"\n倍率 ×"+Factor(basis+step*level)+" → ×"+Factor(basis+step*next);
            }
            else
            {
                Icon(p,offer.contentId,(w-96)*.5f,50,96);
                text=(offer.category=="controller"?Art07Rarity(offer.contentId)+(offer.controller.enchantmentId!="none"?" · "+EnchantShort(offer.controller.enchantmentId):"")+"\n":"")+Description(offer.contentId);
            }
            bool picture=offer.category!="gold"&&offer.category!="new_die"&&offer.category!="hand_upgrade";
            float top=offer.category=="gold"?136:picture?150:54;
            var body=UiKit.Text(p,"Summary",text,14,top,w-28,h-top-96,offer.category=="new_die"?16:15,UiKit.Ink,offer.category=="new_die"||offer.category=="hand_upgrade"?TextAlignmentOptions.Top:TextAlignmentOptions.TopLeft);
            body.overflowMode=TextOverflowModes.Ellipsis;body.lineSpacing=-6;
            string availability=offer.sold?"":Art07Availability07(offer);
            UiKit.Text(p,"Availability",availability,8,h-94,w-16,22,14,UiKit.Danger,TextAlignmentOptions.Center);
            var inspect=HitArea073(p,"Inspect",0,0,w-6,h-98,()=>{if(!IsBusy&&offer.category!="gold")InspectOffer(offer);});
            Art07AttachTooltip(inspect,offer.offerId,name,offer.category=="new_die"?"点击查看这颗骰子":Description(offer.contentId),string.IsNullOrEmpty(availability)?"免费":availability,()=>{if(offer.category!="gold")InspectOffer(offer);});
            BuyButton073(p,offer,16,h-70,w-36,60);
            if(offer.sold||group.closed)SoldVeil073(p,w,h);
        }

        // ---------------------------------------------------------------- texts for new content
        private string ItemSummary10(string itemId,RunState before,RunState after)
        {
            switch(itemId)
            {
                case "I08":return "首领减益已解除";
                case "I09":return "本手下一次提交得分 ×2";
                case "I10":return "本次计分全部视为蓝色 · 永久骰面不变";
                case "I11":return "第一颗已锁骰的朝上面已镀金";
                case "I12":return "本关提交栏容量 +"+(after.stage.capacity-before.stage.capacity);
                default:return "";
            }
        }
        private string ItemUnavailable10(OwnedItem item)
        {
            var s=ViewState;var h=s.hand;bool locked=h.hasRolled&&h.orderedLockedIds.Count>0;
            if(item.contentId!="I08"&&ControllerRules.ModifierHas(Config,Session.CurrentModifierId,"NoItems"))return "首领减益“徒手”：本关不能使用道具。";
            switch(item.contentId)
            {
                case "I08":return Session.CurrentModifierId==null?"只能在首领关、减益仍生效时使用。":s.phase!="HandReady"||s.stage.submissionOrdinal!=0||h.rollsUsed!=0?"只能在本关第一手首掷之前使用。":null;
                case "I09":return h.doubleScore?"本手已经生效。":null;
                case "I10":return !locked?"需要投掷并至少锁定一颗骰子。":h.blueWave?"本手已经生效。":h.redWave?"本手已经用了红色浪潮。":null;
                case "I11":
                    if(!locked)return "需要投掷并至少锁定一颗骰子。";
                    var first=h.currentResults.First(r=>r.dieId==h.orderedLockedIds[0]);
                    return s.dice.First(d=>d.dieId==first.dieId).faces.First(f=>f.faceId==first.currentFaceId).isGold?"第一颗已锁骰的朝上面已经是黄金面。":null;
                case "I12":return s.stage.capacity>=(s.isDebug?Config.debug.maxSubmitCapacity:Config.maxSubmitCapacity)?"提交栏容量已到上限。":null;
                default:return null;
            }
        }
        /// <summary>Effect records added by p1.0.0. Returns null for anything older.</summary>
        private string EffectText10(EffectRecord e)
        {
            switch(e.kind)
            {
                case "ModifierNullified":return NameOf(e.sourceId)+"：首领减益已解除";
                case "ControllerDisabled":return "首领减益："+NameOfInstance10(e.detail)+" 本关失效";
                case "GoldLost":return NameOf(e.sourceId)+"：金币 "+e.oldValue+" → "+e.newValue;
                case "GoldTaxed":return NameOf(e.sourceId)+"：金币 "+e.oldValue+" → "+e.newValue;
                case "GoldSpent":return "一掷千金：花费 "+(e.oldValue-e.newValue)+" 金币";
                case "HandDowngraded":return NameOf(e.sourceId)+"："+NameOf(e.detail)+" "+e.oldValue+" → "+e.newValue+"级";
                // v1.1 (W09): dice and faces by name ("骰子 1 的第 2 面"), never by D001 / F02.
                case "FaceChanged":
                    {
                        string what=(e.detail??"").Split(':')[0],where=DieFaceName11(e.dieId,e.faceId);
                        string change=what=="pipDelta"||what=="pipSet"?"点数 "+e.oldValue+" → "+e.newValue:what=="color"?(e.newValue==1?"染红":"染蓝")+(e.oldValue==e.newValue?"（已是该色）":""):what=="gold"?(e.newValue==1?"镀金":"去掉黄金")+(e.oldValue==e.newValue?"（无变化）":""):what=="texture"?"纹理变为"+((e.detail??"").EndsWith("smooth")?"光滑":(e.detail??"").EndsWith("rough")?"粗糙":"无")+(e.oldValue==e.newValue?"（无变化）":""):"权重 "+e.oldValue+" → "+e.newValue;
                        return NameOf(e.sourceId)+"："+where+" "+change;
                    }
                case "FaceMirrored":return "镜像："+DieFaceName11(e.dieId,e.faceId)+" 变为 "+e.newValue+" 点"+(e.detail=="red"?"红":"蓝");
                case "DieGlassed":return "吹玻璃："+DieName11(e.dieId)+" 变为玻璃骰";
                case "DieAdded":return (string.IsNullOrEmpty(e.sourceId)?"":NameOf(e.sourceId)+"：")+"获得"+DieName11(e.dieId);
                case "ItemGranted":return "百宝袋：获得 "+NameOf((e.detail??"").Split(':').LastOrDefault());
                case "ControllerGranted":return "滥竽充数：获得 "+NameOf((e.detail??"").Split(':').LastOrDefault());
                case "ControllerEnchanted":return NameOf(e.sourceId)+"："+NameOfInstance10((e.detail??"").Split(':')[0])+" 获得附魔 "+EnchantShort((e.detail??"").Split(':').LastOrDefault());
                case "ControllerExpired":return "昙花一现：倍率耗尽，已消失";
                case "RandomUpgrade":return "无心插柳："+DieFaceName11(e.dieId,e.faceId)+" 获得 "+NameOf(e.detail);
                case "RandomUpgradeMissed":return "无心插柳："+DieFaceName11(e.dieId,e.faceId)+" 的 "+NameOf(e.detail)+" 落空";
                case "LootGold":return "战利品折现：+"+e.newValue+" 金币";
                case "BossRerolled":return "贿赂首领：第 "+e.newValue+" 章的减益改为 "+NameOf((e.detail??"").Split(':').LastOrDefault());
                case "InterestCapRaised":return "理财顾问：计息本金上限 "+e.newValue;
                default:return null;
            }
        }
        // v1.1 (W09): a card that is no longer owned is described, never shown by its instance id.
        private string NameOfInstance10(string instanceId)=>CardLabel11(instanceId);
        /// <summary>Handbook rows for the p1.0.0 mechanics, shown under the growth page.</summary>
        private string[][] HandbookRules10()
        {
            var rows=new List<string[]>
            {
                new[]{"三关一周期","每章三关：普通关、高压关、首领关。高压关开始前可以选高压挑战：目标 ×1.5，过关金币 +5。首领关带一个减益，通关后的商店里有战利品。"},
                new[]{"首领减益","减益在每章开始时就定好，关卡选择页和商店的“查看关卡”都能看到。从开始挑战到结算为止生效。替罪羊、破咒符可以解除它，贿赂首领可以重抽。"},
                new[]{"封印","被封印的提交骰点数贡献为 0，但仍然参与牌型、颜色、红组和位置判定。"},
                new[]{"失效的控制器","首领减益可以让一些控制器本关失效：没有技能、附魔不触发，也不能被百变模仿复制；仍然占栏位，仍然计入控制欲和草根逆袭的件数。"},
                new[]{"战利品与赠礼","首领关后的商店有一组免费的战利品，按种类可以拿 1 到 3 件，也可以整组折现成金币。杂货郎、铁匠铺每家商店各送一次三选一。"},
                new[]{"骰面权重","每个骰面有权重，权重越高越容易掷出，上限 5。灌铅和配重可以提高权重。"},
                new[]{"骰面养成","点金手、表里同色、潜移默化、见五成群、二三其德、灌铅、抛光匠、养精蓄锐、移花接木在提交计分后永久改写骰面；拔苗助长、大事化小在每关结算时改写。"},
            };
            if(ViewState?.cycles!=null&&ViewState.stage!=null)
            {
                int cycle=Math.Min(ViewState.cycles.Count-1,(ViewState.stage.stageIndex+(ViewState.stage.settled?1:0))/3);
                if(cycle>=0){var plan=ViewState.cycles[cycle];rows.Add(new[]{"本章首领 · "+NameOf(plan.modifierId),Description(plan.modifierId)+"　战利品："+LootText10(plan.lootKind)});}
            }
            return rows.ToArray();
        }
    }
}
