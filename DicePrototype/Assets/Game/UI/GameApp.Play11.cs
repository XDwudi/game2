using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    // V1.1 task W11: the play screen golden sample (docs/specs/v11/PLAY_LAYOUT.md). Presentation only: every value comes
    // from the committed run state or the session's pure previews; no rule, save or random state is touched here.
    // Coordinates are 1920x1080 design units. Colours are read from UiKit at draw time (THEME_SPEC section 1); text sits on
    // a surface (top bar, panels, chips, cards), never directly on the table, so a light table theme stays readable.
    public sealed partial class GameApp
    {
        /// <summary>Captures only: hide the debug-workspace markers so a fixture scene shows what a player would see.</summary>
        public static bool CleanCapture11;

        private struct Layout11
        {
            public Rect top,cards,stage,tray,dice,chips,resources,submit,info,rail,settle,hint,secondary,primary,end;
        }
        private Layout11 L11
        {
            get
            {
                bool c=Large11;float info=c?360:300;
                return new Layout11
                {
                    top=new Rect(16,12,1888,72),cards=new Rect(32,96,1448,244),stage=new Rect(1496,96,392,244),
                    tray=new Rect(32,352,1448,376),dice=new Rect(52,374,1408,276),chips=new Rect(52,660,1408,60),
                    resources=new Rect(1496,352,392,376),submit=new Rect(32,740,1448,202),
                    info=new Rect(32,740,info,202),rail=new Rect(32+info+12,740,1448-info-12,202),settle=new Rect(1496,740,392,202),
                    hint=c?new Rect(32,958,600,88):new Rect(32,958,512,88),
                    secondary=c?new Rect(644,966,240,72):new Rect(560,966,264,72),
                    primary=c?new Rect(896,962,380,80):new Rect(840,962,400,80),end=new Rect(1496,962,392,80)
                };
            }
        }
        /// <summary>
        /// Enlarged text (125% or 150%). The play screen is laid out in design units, so the 1280x720 compact mode and the
        /// same text size at 1920x1080 need the same room: every text-driven choice here follows the text size, not the
        /// screen (PLAY_LAYOUT preamble: the compact parameters at 125/150%).
        /// </summary>
        private bool Large11=>UiKit.FontScale>1.01f;
        private bool PlayPhase11=>ViewState!=null&&(ViewState.phase=="HandReady"||ViewState.phase=="Selecting");
        // The top bar frames the stage select, the play screen and the stage settlement: when the last hand of a stage hands
        // over to the settlement, the score frame stays where the player was reading it.
        private bool FramePhase11=>PlayPhase11||ViewState?.phase=="StagePreview"||ViewState?.phase=="StageSettlement";
        /// <summary>Text drawn straight on the table felt (the trays are open frames): ink on a light felt, light text on a dark one.</summary>
        private static Color OnFelt11{get{var f=UiKit.Felt;return .2126f*f.r+.7152f*f.g+.0722f*f.b>.5f?UiKit.Ink:UiKit.TextColor;}}
        private string StageKind11=>ViewState==null?"normal":Config.stages[Mathf.Clamp(ViewState.stage.stageIndex,0,Config.stages.Length-1)].stageKind;
        private bool StageRisk11=>ViewState!=null&&(ViewState.phase=="StagePreview"?PreviewRisk07:ViewState.stage.risk);
        /// <summary>Boss cues apply while its modifier is in force; a scapegoat that lifted it returns the table to normal.</summary>
        private bool BossActive11=>StageKind11=="boss"&&ViewState.phase!="StagePreview"&&!string.IsNullOrEmpty(ViewState.stage.modifierId)&&!ViewState.stage.modifierNullified;
        /// <summary>Pressure cues follow the accepted pressure challenge (target x1.5); a pressure stage played as the ordinary challenge looks ordinary.</summary>
        private bool PressureActive11=>StageKind11=="pressure"&&StageRisk11;
        private Color StageTint11=>BossActive11?UiKit.Boss:PressureActive11?UiKit.Pressure:UiKit.PanelLight;

        /// <summary>The player-facing short text of a content item (W10 shortText); the rule description stays for the details view.</summary>
        private string ShortText11(string id)
        {
            string text=null;
            if(string.IsNullOrEmpty(id))return "";
            if(id.StartsWith("C"))text=Config.Controller(id).shortText;
            else if(id.StartsWith("I"))text=Config.Consumable(id).shortText;
            else if(id.StartsWith("O"))text=Config.Service(id).shortText;
            else if(id.StartsWith("U"))text=Config.Upgrade(id).shortText;
            else if(id.StartsWith("B"))text=Config.Modifier(id).shortText;
            else if(id.StartsWith("L"))text=Config.Loot(id).shortText;
            if(!string.IsNullOrWhiteSpace(text))return text;
            string d=Description(id);int stop=d.IndexOf('。');return stop>0?d.Substring(0,stop+1):d;
        }
        private string Details11(string id)
        {
            string text=null;
            if(id.StartsWith("C"))text=Config.Controller(id).details;
            else if(id.StartsWith("I"))text=Config.Consumable(id).details;
            else if(id.StartsWith("B"))text=Config.Modifier(id).details;
            return string.IsNullOrWhiteSpace(text)?Description(id):text;
        }
        private static RectTransform Surface11(Transform parent,string name,float x,float y,float w,float h,string template,Color tint,bool raycast=false)
        {
            var r=UiKit.Box(parent,name,x,y,w,h,tint,raycast);var image=r.GetComponent<Image>();
            image.sprite=UiKit.Template(template);image.type=image.sprite!=null?Image.Type.Sliced:Image.Type.Simple;image.fillCenter=true;
            image.pixelsPerUnitMultiplier=1;
            return r;
        }
        private static string Hex11(Color c)=>"#"+ColorUtility.ToHtmlStringRGB(c);
        private static TextMeshProUGUI NoWrap11(TextMeshProUGUI t){t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Overflow;return t;}

        // ================================================================ top bar (PLAY_LAYOUT 1, TopBar11)
        private TextMeshProUGUI topScore11;
        private RectTransform topScoreFrame11;
        private void TopBar11()
        {
            var s=ViewState;var l=L11;bool c=Large11,play=PlayPhase11||s.phase=="StageSettlement";
            // "Stats07" keeps the name of the old status column it replaces: the qualified stamp, the cold-load badge and
            // the settlement backdrop already address the run status HUD by this name.
            var bar=Surface11(main,"Stats07",l.top.x,l.top.y,l.top.width,l.top.height,"ui_topbar",UiKit.Panel);
            float right=l.top.xMax-8,pauseX=right-108,handX=pauseX-12-108;bool debug=DebugUnlocked&&IsDebugWorkspace&&!CleanCapture11;
            float clusterLeft=debug?handX-12-148:handX;
            float goldW=c?232:176,goldX=clusterLeft-16-goldW;
            int n=s.stage.stageIndex+1;
            // Stage tag: number on a chip tinted by stage kind.
            float tagW=c?176:160;
            var tag=Surface11(bar,"StageTag11",8,8,tagW,56,"ui_chip_formula",StageTint11);
            var stage=NoWrap11(UiKit.Text(tag,"StageLabel11",$"第 {n} 关",0,2,tagW,48,c?24:28,UiKit.TextColor,TextAlignmentOptions.Center));
            stage.fontStyle=FontStyles.Bold;
            // Eight chapter points and the chapter caption with the stage kind.
            float dotsX=8+tagW+20;int chapters=Mathf.Max(1,Config.stages.Length/3),chapter=s.stage.stageIndex/3;
            for(int i=0;i<chapters;i++)
            {
                bool done=i<chapter,current=i==chapter;float size=current?20:14;
                var dot=UiKit.Box(bar,"ChapterDot"+i,dotsX+i*24+(20-size)*.5f,12+(20-size)*.5f,size,size,current?StageTint11:done?UiKit.Gold:UiKit.PanelLight).GetComponent<Image>();
                dot.sprite=UiKit.Template("ui_pip_dot");dot.preserveAspect=true;
                if(current){var ring=UiKit.Ring(bar,"ChapterCurrentRing11",dotsX+i*24-3,9,26,26,UiKit.Paper);ring.GetComponent<Image>().pixelsPerUnitMultiplier=6;}
            }
            string kindWord=StageKindLabel10(s.stage.stageIndex,StageRisk11);
            string kindColored=BossActive11||PressureActive11?"<color="+Hex11(Color.Lerp(StageTint11,UiKit.TextColor,.25f))+">"+kindWord+"</color>":kindWord;
            var caption=NoWrap11(UiKit.Text(bar,"Chapter",$"第 {chapter+1} 章 · "+kindColored,dotsX,36,chapters*24+40,32,Body11(c?17:20),UiKit.Muted));
            caption.richText=true;
            // Practice and the debug workspace keep their own label; an ordinary run leaves it empty.
            string edition=IsPracticeRun11?"练习模式":IsDebugWorkspace&&!CleanCapture11?"调试对局":"";
            float captionW=caption.GetPreferredValues(caption.text).x;
            NoWrap11(UiKit.Text(bar,"Edition",edition,dotsX+captionW+12,36,140,32,c?17:20,IsDebugWorkspace?UiKit.Violet:UiKit.Gold));
            // The score frame starts after the caption line (and its practice / debug label), so the two never touch.
            float editionW=edition.Length>0?PreferredWidth11(edition,c?17:20)+12:0;
            float frameX=Mathf.Max(dotsX+chapters*24+56,dotsX+captionW+editionW+20),frameRight=goldX-l.top.x-16-(play?284:0);
            if(play)DrawTopScore11(bar,frameX,Mathf.Min(c?640:560,frameRight-frameX),c);
            else
            {
                // Stage select: the stage has not started, its target is chosen on the cards below.
                topScore11=null;topScoreFrame11=null;
            }
            // Gold.
            var coin=UiKit.Box(bar,"MoneyIcon11",goldX-l.top.x+6,18,36,36,Color.white).GetComponent<Image>();coin.sprite=UiKit.Template("ui_icon_coin");coin.preserveAspect=true;coin.gameObject.SetActive(coin.sprite!=null);
            var money=NoWrap11(UiKit.Text(bar,"Money",PlayerNumberFormat.Gold(s.gold),goldX-l.top.x+48,6,goldW-48-(c?52:44),60,c?30:34,UiKit.Gold,TextAlignmentOptions.MidlineRight));
            money.fontStyle=FontStyles.Bold;
            NoWrap11(UiKit.Text(bar,"MoneyLabel11","金币",goldX-l.top.x+goldW-(c?50:42),6,c?50:42,60,Body11(c?17:20),UiKit.Muted,TextAlignmentOptions.MidlineLeft));
            if(IsDebugWorkspace&&!CleanCapture11)UiKit.Box(main,"DebugMark",0,0,1920,6,UiKit.Violet);
        }
        /// <summary>Score frame of the top bar: stage score / target and the progress bar. The stamp slot sits to its right.</summary>
        private void DrawTopScore11(RectTransform bar,float x,float w,bool c)
        {
            var s=ViewState;
            var frame=UiKit.Inset(bar,"StageScoreFrame",x,6,w,60);topScoreFrame11=frame;
            string shown=DisplayedStageScore11();
            // Right-aligned in its reserved width: while a total rolls in, the digits grow toward the left and stay next to "/".
            topScore11=NoWrap11(UiKit.Text(frame,"StageScore",CompactScore(shown),14,0,w*.5f,48,c?28:34,s.stage.qualified?UiKit.Gold:UiKit.TextColor,TextAlignmentOptions.MidlineRight));
            topScore11.fontStyle=FontStyles.Bold;
            // While a total rolls in, reserve the width of the final stage score so the target never moves under it.
            string widest=IsPresenting07&&receipt07!=null&&lastPresentationAfter07?.stage?.stageId==s.stage.stageId?CompactScore(receipt07.StageScoreAfter):topScore11.text;
            float scoreW=Mathf.Min(w*.55f,Mathf.Max(topScore11.GetPreferredValues(topScore11.text).x,topScore11.GetPreferredValues(widest).x)+4);topScore11.rectTransform.sizeDelta=new Vector2(scoreW,48);
            UiKit.Component<Art072ValuePop>(topScore11.gameObject).Configure(s.runId+"|"+s.stage.stageId+"|StageScore",shown);
            // The score pops (scale up to Art072ValuePop.Peak from its left edge) when it changes: the target starts after the
            // popped width, so the two numbers never touch.
            float targetX=14+scoreW*Art072ValuePop.Peak+8;
            NoWrap11(UiKit.Text(frame,"Target","/ "+CompactScore(s.stage.target),targetX,4,Mathf.Max(40,w-targetX-26),44,c?26:32,PressureActive11?UiKit.Pressure:UiKit.Muted,TextAlignmentOptions.MidlineLeft));
            BigIntegerRatio11(shown,s.stage.target,out float ratio);
            UiKit.Progress(frame,"StageProgress072",14,48,w-28,7,ratio,s.stage.qualified?UiKit.Gold:PressureActive11?UiKit.Pressure:UiKit.Blue,s.runId+"|"+s.stage.stageId);
            UiKit.Rect(bar,"QualifiedStampSlot07",x+w+12,10,272,52);
            if(s.stage.qualified&&(qualifiedStamp07==null||qualifiedStage07!=s.stage.stageId))
            {
                var badge=UiKit.Frame(frame,"QualifiedSavedBadge07",w-36,6,28,28,UiKit.Gold,UiKit.Ink);
                UiKit.Text(badge,"Check","✓",0,0,28,28,16,UiKit.Ink,TextAlignmentOptions.Center);
            }
        }
        /// <summary>Where a gold change pops on the play screen: in the top bar, in the slot right of the score frame (the
        /// qualified-stamp slot), just left of the gold readout. Clear of every button and text of the bar.</summary>
        private Vector2 TopCashPoint11()
        {
            var l=L11;
            if(topScoreFrame11==null||!topScoreFrame11.gameObject.activeInHierarchy)return new Vector2(l.top.center.x-110,l.top.y+7);
            var corners=new Vector3[4];topScoreFrame11.GetWorldCorners(corners);
            var right=ScreenToDesign(RectTransformUtility.WorldToScreenPoint(null,corners[3]));
            return new Vector2(right.x+12,l.top.y+7);
        }
        private static void BigIntegerRatio11(string shown,string target,out float ratio)
        {
            var t=ExactScoreMath.Parse(target);var v=ExactScoreMath.Parse(shown);
            ratio=t.IsZero||t.Sign<0?1:(float)System.Numerics.BigInteger.Min(1000,System.Numerics.BigInteger.Max(0,v*1000/t))/1000f;
        }
        /// <summary>The stage score the top bar shows: during a scoring presentation the total rolls in from FinalScore on.</summary>
        private string DisplayedStageScore11()
        {
            if(IsPresenting07&&receipt07!=null&&lastPresentationAfter07?.stage?.stageId==ViewState.stage.stageId&&beat07!=null&&beat07.Phase>=DiceCraft.Presentation.PresentationPhase.FinalScore)
                return beat07.Phase>DiceCraft.Presentation.PresentationPhase.FinalScore?receipt07.StageScoreAfter:RolledStageScore11();
            if(QualifiedStampPlayingForCurrentStage07&&IsPresenting07&&lastPresentationAfter07?.stage?.stageId==ViewState.stage.stageId)return lastPresentationAfter07.stage.score;
            return ViewState.stage.score;
        }

        // ================================================================ play screen (PLAY_LAYOUT 1)
        private void Gameplay11()
        {
            var s=ViewState;
            DrawCards11();DrawStageCard11();DrawTray11();DrawResources11();DrawSubmit11();ScorePanel();DrawSettle11();DrawActions11();
        }

        // ---------------------------------------------------------------- cards (Cards11)
        private enum CardMode11{Full,Text,Mini}
        private void DrawCards11()
        {
            var s=ViewState;var l=L11;bool c=Large11;int count=s.controllers.Count;
            bool mini=count>7||c&&count>5;
            if(mini)
            {
                int shown=count>10?9:count;float cw=(l.cards.width-4*12)/5,ch=(l.cards.height-10)/2;
                for(int i=0;i<shown;i++)DrawTableCard11(s.controllers[i],l.cards.x+i%5*(cw+12),l.cards.y+i/5*(ch+10),cw,ch,CardMode11.Mini);
                if(count>10)
                {
                    var all=UiKit.Button(main,"AllControllerStates","全部 "+count+" 件 >",l.cards.x+4*(cw+12),l.cards.y+ch+10,cw,ch,ShowControllerOrder);
                    var label=all.GetComponentInChildren<TextMeshProUGUI>();label.fontSize=22*UiKit.FontScale;label.textWrappingMode=TextWrappingModes.NoWrap;
                }
                return;
            }
            int empty=Math.Max(0,s.controllerSlots-ControllerRules.OrdinaryUsed(s.controllers));
            const float strip=56,gap=12;
            float cap=c?280:216;
            float cardW=count==0?0:Mathf.Min(cap,(l.cards.width-empty*(strip+gap)-gap*(count-1))/count);
            float x=l.cards.x;
            for(int i=0;i<count;i++){DrawTableCard11(s.controllers[i],x,l.cards.y,cardW,l.cards.height,c?CardMode11.Text:CardMode11.Full);x+=cardW+gap;}
            for(int i=0;i<empty&&x+strip<=l.cards.xMax+.5f;i++)
            {
                var slot=Surface11(main,"ControllerEmpty"+i,x,l.cards.y+8,strip,l.cards.height-16,"ui_slot_inventory",Color.white);
                UiKit.Text(slot,"Plus","+",0,0,strip,l.cards.height-16,30,UiKit.Muted,TextAlignmentOptions.Center);
                x+=strip+gap;
            }
        }
        private string CardTableState11(ControllerInstance item){string state=CardState07(item);return state=="点击查看条件"?"":state.Replace("→ ","→");}
        private void DrawTableCard11(ControllerInstance item,float x,float y,float w,float h,CardMode11 mode)
        {
            var b=UiKit.Button(main,"Controller_"+item.instanceId,"",x,y,w,h,()=>ShowController(item,false),!IsBusy);
            b.targetGraphic.color=UiKit.Paper;UiKit.CardSurface(b,mode==CardMode11.Mini);
            var t=b.transform;int ordinal=ViewState.controllers.IndexOf(item)+1;string state=CardTableState11(item);
            const float pad=10;
            bool mini=mode==CardMode11.Mini;
            // Meta row: ordinal disc, rarity ticks and word (on a mini card under its picture), enchantment badge top right.
            // Every label here is as tall as its line: an Ellipsis label shorter than its line renders nothing (v1.0 lesson).
            float metaY=mini?h-8-Line11(14):6,metaX=mini?8:pad,disc=Mathf.Min(24,Line11(14));
            var ordinalDisc=UiKit.Box(t,"OrdinalDisc11",metaX,metaY+(Line11(14)-disc)*.5f,disc,disc,UiKit.Ink).GetComponent<Image>();ordinalDisc.sprite=UiKit.Template("ui_pip_dot");
            NoWrap11(UiKit.Text(t,"Ordinal",ordinal.ToString(),metaX,metaY,disc,Line11(14),14,UiKit.Paper,TextAlignmentOptions.Center));
            Rarity11(t,item.definitionId,metaX+disc+6,metaY,mini?Mathf.Min(96,w*.36f):Mathf.Max(48,w-pad-30-metaX-44));
            float badge=mini?28:32;EnchantBadge(t,item.enchantmentId,w-pad-badge+2,mini?6:4,badge);
            string text=ShortText11(item.definitionId);
            if(mode==CardMode11.Full)
            {
                var name=NoWrap11(UiKit.Text(t,"Name",NameOf(item.definitionId),pad,32,w-pad*2,Line11(22),22,UiKit.Ink,TextAlignmentOptions.Center));
                name.fontStyle=FontStyles.Bold;
                var body=UiKit.Text(t,"ShortText11",text,pad,0,w-pad*2,200,20,UiKit.Ink,TextAlignmentOptions.Top);
                body.lineSpacing=-14;
                // The short text is complete at 20 units (13 px at 720p, AX-03); a long text steps down only as far as the
                // illustration needs to stay recognisable (about 80 units), never below the 19.5-unit body size.
                float stateH=state.Length>0?Line11(Body11(16)):0,textH=0,top=68;
                foreach(float size in new[]{20f,19.5f})
                {
                    body.fontSize=size*UiKit.FontScale;textH=Mathf.Ceil(body.GetPreferredValues(text,w-pad*2,0).y)+2;
                    if(h-12-textH-4-(stateH>0?stateH+6:0)-top>=80)break;
                }
                float textY=h-12-textH;body.rectTransform.anchoredPosition=new Vector2(pad,-textY);body.rectTransform.sizeDelta=new Vector2(w-pad*2,textH);
                float bottom=textY-4;
                if(state.Length>0)
                {
                    bottom-=stateH+2;
                    UiKit.Box(t,"StateChip11",pad,bottom,w-pad*2,stateH,new Color(UiKit.Ink.r,UiKit.Ink.g,UiKit.Ink.b,.08f));
                    // A full line of height: an Ellipsis label shorter than its line renders nothing at all.
                    NoWrap11(UiKit.Text(t,"State",state,pad,bottom,w-pad*2,stateH,Body11(16),DisabledNow10(item.instanceId)||DisabledNext10(item.instanceId)?UiKit.Danger:UiKit.Ink,TextAlignmentOptions.Center));
                    bottom-=4;
                }
                float imageSize=Mathf.Max(24,Mathf.Min(w-pad*2-8,bottom-top));
                Icon(t,item.definitionId,(w-imageSize)*.5f,top+Mathf.Max(0,(bottom-top-imageSize)*.5f),imageSize);
            }
            else if(mode==CardMode11.Text)
            {
                float icon=48,top=Mathf.Max(36,6+Line11(14)+2),nameH=Mathf.Max(icon,Line11(22));Icon(t,item.definitionId,pad,top+(nameH-icon)*.5f,icon);
                var name=NoWrap11(UiKit.Text(t,"Name",NameOf(item.definitionId),pad+icon+8,top,w-pad*2-icon-8,nameH,22,UiKit.Ink,TextAlignmentOptions.MidlineLeft));
                name.fontStyle=FontStyles.Bold;
                float textTop=top+nameH+4;
                if(state.Length>0)
                {
                    float stateH=Line11(Body11(16));
                    NoWrap11(UiKit.Text(t,"State",state,pad,textTop,w-pad*2,stateH,Body11(16),DisabledNow10(item.instanceId)||DisabledNext10(item.instanceId)?UiKit.Danger:UiKit.Ink,TextAlignmentOptions.MidlineLeft));
                    textTop+=stateH+2;
                }
                var body=UiKit.Text(t,"ShortText11",text,pad,textTop,w-pad*2,h-12-textTop,20,UiKit.Ink,TextAlignmentOptions.TopLeft);
                // At 150 % a card with a state line has three lines' room: the text shrinks as far as the 19.5-unit body size
                // (13 px at 720p, AX-03) before it would overflow.
                body.lineSpacing=-14;body.enableAutoSizing=true;body.fontSizeMax=20*UiKit.FontScale;body.fontSizeMin=19.5f;
            }
            else
            {
                float icon=Mathf.Min(56,metaY-12);Icon(t,item.definitionId,8,6,icon);
                float tx=8+icon+10,tw=w-tx-pad-badge-4;
                var name=NoWrap11(UiKit.Text(t,"Name",NameOf(item.definitionId),tx,4,tw,Line11(20),20,UiKit.Ink,TextAlignmentOptions.MidlineLeft));
                name.fontStyle=FontStyles.Bold;
                float textTop=4+Line11(20);
                if(state.Length>0)
                {
                    NoWrap11(UiKit.Text(t,"State",state,tx,textTop,w-tx-pad,Line11(Body11(15)),Body11(15),DisabledNow10(item.instanceId)||DisabledNext10(item.instanceId)?UiKit.Danger:UiKit.Ink,TextAlignmentOptions.MidlineLeft));
                    textTop+=Line11(Body11(15));
                }
                // Two rows of five: the short text is shown when it fits whole at the body size (AX-03: 13 px at 720p);
                // otherwise the card keeps its name and state and the text is in its hover and details (PLAY_LAYOUT 8).
                // The text ends above the meta row (ordinal and rarity) at the card's foot.
                float room=metaY-4-textTop,textW=w-tx-pad;
                var body=UiKit.Text(t,"ShortText11",text,tx,textTop,textW,room,Body11(18),UiKit.Ink,TextAlignmentOptions.TopLeft);
                body.lineSpacing=-16;
                if(body.GetPreferredValues(text,textW,0).y>room+.5f)body.gameObject.SetActive(false);
            }
            AttachControllerDrag(b,item);
            Art07AttachTooltip(b,item.instanceId,NameOf(item.definitionId),text+"\n"+Details11(item.definitionId),EnchantShort(item.enchantmentId)+(state.Length>0?" · "+state:""),()=>ShowController(item,false));
        }
        private void Rarity11(Transform parent,string id,float x,float y,float labelWidth)
        {
            string rarity=Config.Controller(id).rarity;int count=rarity=="rare"?3:rarity=="uncommon"?2:1;
            Color tint=rarity=="rare"?UiKit.RarityRare:rarity=="uncommon"?UiKit.RarityAdvanced:UiKit.RarityCommon;
            var strip=UiKit.Rect(parent,"RarityTicks",x,y+10,30,5);
            for(int i=0;i<count;i++)UiKit.Box(strip,"Tick"+i,i*11,0,8,5,tint);
            for(int i=count;i<3;i++){var stale=strip.Find("Tick"+i);if(stale!=null)stale.gameObject.SetActive(false);}
            strip.anchoredPosition=new Vector2(x,-(y+Line11(14)*.5f-2.5f));
            NoWrap11(UiKit.Text(parent,"RarityLabel",Art07Rarity(id),x+34,y,Mathf.Max(30,labelWidth-34),Line11(14),14,UiKit.Ink,TextAlignmentOptions.MidlineLeft));
        }
        /// <summary>Height of one line of TMP text at this size and the current text scale (Noto Sans SC line height ~1.45 em).</summary>
        private static float Line11(float size)=>Mathf.Ceil(size*UiKit.FontScale*1.46f)+2;
        /// <summary>A body-text size that ends at no less than 19.5 design units after the text scale: 13 px at 1280x720 (AX-03).</summary>
        private static float Body11(float size)=>Mathf.Max(size,19.5f/Mathf.Max(.01f,UiKit.FontScale));

        // ---------------------------------------------------------------- stage card (StageCard11)
        private void DrawStageCard11()
        {
            var s=ViewState;var l=L11;bool c=Large11;string kind=StageKind11;
            float x=l.stage.x,y=l.stage.y,w=l.stage.width,h=l.stage.height;
            var card=Surface11(main,"StageCard11",x,y,w,h,"ui_stage_card",UiKit.Paper);
            float inner=w-36;
            bool boss=kind=="boss"&&!string.IsNullOrEmpty(s.stage.modifierId),lifted=boss&&s.stage.modifierNullified;
            Color tint=boss&&!lifted?UiKit.Boss:PressureActive11?UiKit.Pressure:UiKit.PanelLight;
            if(boss&&!lifted)
            {
                // The boss frame: two tinted rings just inside the ink edge.
                UiKit.Ring(card,"StageCardEdge11",4,4,w-16,h-18,UiKit.Boss);
                UiKit.Ring(card,"StageCardEdgeInner11",9,9,w-26,h-28,new Color(UiKit.Boss.r,UiKit.Boss.g,UiKit.Boss.b,.55f));
            }
            else if(PressureActive11)UiKit.Ring(card,"StageCardEdge11",5,5,w-18,h-20,UiKit.Pressure);
            float headerH=Mathf.Max(40,Line11(Body11(c?18:22)));
            var header=Surface11(card,"StageCardHeader11",14,12,inner,headerH,"ui_chip_formula",tint);
            string title;
            if(boss)
            {
                var def=Config.Modifier(s.stage.modifierId);
                string tier=def.tier>=4?"终局":def.tier==3?"三档":def.tier==2?"二档":"一档";
                title=(lifted?"减益已解除 · ":"首领减益 · ")+tier;
            }
            else title=StageKindLabel10(s.stage.stageIndex,StageRisk11);
            NoWrap11(UiKit.Text(header,"StageCardTitle11",title,12,0,inner-24,headerH,Body11(c?18:22),UiKit.TextColor,TextAlignmentOptions.MidlineLeft));
            if(boss)
            {
                float top=headerH+18;
                var box=UiKit.Rect(card,"BossModifier10",14,top,inner,h-top-14);
                // Emblem placeholder until the W17 boss emblems exist: a block in the tier colour with tier bars.
                var def=Config.Modifier(s.stage.modifierId);int bars=Mathf.Clamp(def.tier,1,4);
                Color emblem=lifted?UiKit.PanelLight:def.tier>=4?UiKit.Boss:def.tier==3?UiKit.RarityRare:def.tier==2?UiKit.RarityAdvanced:UiKit.RarityCommon;
                var mark=Surface11(box,"BossEmblem11",0,6,72,72,"ui_chip_formula",emblem);
                for(int i=0;i<bars;i++)UiKit.Box(mark,"TierBar"+i,16,16+i*12,40,6,UiKit.Ink);
                float nameSize=c?20:28,nameH=Line11(nameSize),boxH=h-top-14,lootH=52;
                var name=NoWrap11(UiKit.Text(box,"BossModifierName",NameOf(s.stage.modifierId),84,0,inner-84,nameH,nameSize,lifted?PaperMuted071:UiKit.Ink));
                name.fontStyle=FontStyles.Bold;
                var body=UiKit.Text(box,"BossModifierText",ShortText11(s.stage.modifierId),84,nameH,inner-84,Mathf.Max(40,boxH-lootH-4-nameH),20,lifted?PaperMuted071:UiKit.Ink);
                body.lineSpacing=-14;body.enableAutoSizing=true;body.fontSizeMax=20*UiKit.FontScale;body.fontSizeMin=Body11(15)*UiKit.FontScale;
                string loot=s.stage.stageIndex/3<s.cycles.Count?s.cycles[s.stage.stageIndex/3].lootKind:null;
                var lootText=UiKit.Text(box,"BossLoot11",string.IsNullOrEmpty(loot)?"终局首领 · 通关即胜利":"通关奖励："+ShortText11(loot),0,boxH-lootH,inner,lootH,18,lifted?PaperMuted071:Color.Lerp(UiKit.Ink,UiKit.Boss,.55f));
                lootText.lineSpacing=-14;lootText.enableAutoSizing=true;lootText.fontSizeMax=Body11(18)*UiKit.FontScale;lootText.fontSizeMin=Body11(14)*UiKit.FontScale;
                var open=HitArea073(card,"BossModifierOpen",0,0,w-8,h-10,()=>{if(!IsBusy)ShowCycle10();});
                Art07AttachTooltip(open,"boss-modifier",NameOf(s.stage.modifierId),Details11(s.stage.modifierId),lifted?"已解除":"本关生效",()=>{if(!IsBusy)ShowCycle10();});
                return;
            }
            // Four lines, each as tall as its line at the current text size, so 150 % still fits the card (AX-03).
            float lineSize=Body11(c?14:20),goalSize=c?30:42,ty=headerH+18;
            NoWrap11(UiKit.Text(card,"StageGoalLabel11","目标分数",14,ty,inner,Line11(lineSize),lineSize,PaperMuted071));ty+=Line11(lineSize);
            var goal=NoWrap11(UiKit.Text(card,"StageGoal11",CompactScore(s.stage.target),14,ty,inner,Line11(goalSize),goalSize,PressureActive11?UiKit.Pressure:UiKit.Ink));
            goal.fontStyle=FontStyles.Bold;ty+=Line11(goalSize);
            int reward=s.stage.reward>0?s.stage.reward:StageRisk11?Config.stages[s.stage.stageIndex].riskReward:Config.stages[s.stage.stageIndex].normalReward;
            NoWrap11(UiKit.Text(card,"StageReward11","过关奖励 +"+reward+" 金币",14,ty,inner,Line11(lineSize),lineSize,UiKit.Ink));ty+=Line11(lineSize);
            string note=kind=="pressure"?(StageRisk11?"目标 ×1.5，过关金币 +5":"普通挑战，目标不变"):"提交栏 "+s.stage.capacity+" 格 · 每手掷 "+Session.InitialRolls+" 次";
            NoWrap11(UiKit.Text(card,"StageNote11",note,14,ty,inner,Line11(Body11(c?14:17)),Body11(c?14:17),PaperMuted071));
            HitArea073(card,"StageCardOpen11",0,0,w-8,h-10,()=>{if(!IsBusy)ShowCycle10();});
        }

        // ---------------------------------------------------------------- dice tray (Tray11)
        private void DrawTray11()
        {
            var s=ViewState;var h=s.hand;var l=L11;bool c=Large11;
            var tray=Surface11(main,"Tray11",l.tray.x,l.tray.y,l.tray.width,l.tray.height,"ui_dice_tray",Color.Lerp(UiKit.Panel,UiKit.PanelLight,.55f));
            // Accent line just inside the rim: the stage kind speaks here (PLAY_LAYOUT 4).
            if(BossActive11)
            {
                UiKit.Ring(main,"TrayAccent11",l.tray.x+6,l.tray.y+6,l.tray.width-12,l.tray.height-12,UiKit.Boss);
                UiKit.Ring(main,"TrayAccentInner11",l.tray.x+10,l.tray.y+10,l.tray.width-20,l.tray.height-20,new Color(UiKit.Boss.r,UiKit.Boss.g,UiKit.Boss.b,.6f));
            }
            else if(PressureActive11)UiKit.Ring(main,"TrayAccent11",l.tray.x+7,l.tray.y+7,l.tray.width-14,l.tray.height-14,UiKit.Pressure).GetComponent<Image>().pixelsPerUnitMultiplier=1.6f;
            else UiKit.Ring(main,"TrayAccent11",l.tray.x+7,l.tray.y+7,l.tray.width-14,l.tray.height-14,new Color(UiKit.Ghost.r,UiKit.Ghost.g,UiKit.Ghost.b,.75f)).GetComponent<Image>().pixelsPerUnitMultiplier=1.6f;
            // Title tab on the rim.
            var tab=Surface11(main,"PoolTab11",l.tray.x+24,l.tray.y-14,c?120:100,34,"ui_chip_formula",UiKit.Panel);
            NoWrap11(UiKit.Text(tab,"PoolTitle","骰池",0,0,c?120:100,34,Body11(c?18:20),UiKit.Muted,TextAlignmentOptions.Center));
            var dice=s.dice.Where(d=>!h.orderedLockedIds.Contains(d.dieId)).OrderBy(d=>d.createdSerial).ToList();poolPage=Math.Min(poolPage,Math.Max(0,(dice.Count-1)/10));
            // Before the first throw of a hand the dice rest unlit (B4): a shade over the tray floor, no words.
            if(!h.hasRolled&&dice.Count>0){var veil=UiKit.Box(main,"PreRollVeil11",l.dice.x-4,l.dice.y-6,l.dice.width+8,l.dice.height+8,new Color(UiKit.Shadow.r,UiKit.Shadow.g,UiKit.Shadow.b,.32f));veil.GetComponent<Image>().raycastTarget=false;}
            if(h.hasRolled&&ViewSources.Any(x=>x.abilityId=="C70"||x.abilityId=="C71"||x.abilityId=="C72"||x.abilityId=="C73"||x.abilityId=="C74"))
            {
                var rest=h.currentResults.Where(r=>r.hasRolled&&!r.isLocked).ToArray();
                string summaryText="留池 "+rest.Length+" 颗 · 点数和 "+rest.Sum(r=>r.currentPip)+" · 红 "+rest.Count(r=>r.currentColor=="red")+" · 6点 "+rest.Count(r=>r.currentPip==6);
                float sw=c?620:500;var chip=Surface11(main,"PoolSummaryTab11",l.tray.xMax-24-sw,l.tray.y-14,sw,34,"ui_chip_formula",UiKit.Panel);
                NoWrap11(UiKit.Text(chip,"PoolSummary09",summaryText,10,0,sw-20,34,Body11(c?16:19),UiKit.Teal,TextAlignmentOptions.Center));
            }
            // One row of up to ten dice (a page). Five or fewer stand at about 1.5x the v1.0 size; more share the row's width
            // and still come out larger than the former second row (PLAY_LAYOUT 8: one row instead of two).
            int shown=Math.Min(10,dice.Count-poolPage*10);
            bool paged=dice.Count>10;float left=l.dice.x+(paged?72:0),width=l.dice.width-(paged?72:0);
            float cellW=Mathf.Min(272,width/Mathf.Max(1,shown)),scale=Mathf.Min(1.75f,(cellW-30)/101f);
            float start=left+(width-shown*cellW)*.5f,cy=l.dice.y+l.dice.height*.5f-10;
            for(int j=0;j<shown;j++)
            {
                var d=dice[j+poolPage*10];
                DrawPoolDie11(d,h.currentResults.FirstOrDefault(r=>r.dieId==d.dieId),new Vector2(start+j*cellW+cellW*.5f,cy),j,scale,cellW);
            }
            if(paged)
            {
                float pageSize=Mathf.Max(60,40/Mathf.Max(.1f,canvas.scaleFactor));
                UiKit.Button(main,"PoolPage",">",l.dice.x,l.dice.y+8,pageSize,pageSize,()=>{poolPage=(poolPage+1)%((dice.Count+9)/10);Refresh();});
                var tag=Surface11(main,"PoolPageTab11",l.dice.x,l.dice.y+12+pageSize,pageSize,36,"ui_chip_formula",UiKit.Panel);
                NoWrap11(UiKit.Text(tag,"PoolPageIndex",(poolPage+1)+" / "+((dice.Count+9)/10),0,0,pageSize,36,16,UiKit.Muted,TextAlignmentOptions.Center));
            }
        }
        private void DrawPoolDie11(DieInstance die,DieHandState result,Vector2 center,int index,float scale,float cellW)
        {
            bool selected=Session.PendingIds.Contains(die.dieId),c=Large11;
            // Orthographic camera 60 degrees down: the body projects about 101 x 127 design units per unit of scale at the
            // 18-degree turn, up to 139 x 136 for a resting die turned by up to 32 degrees (B4), plus the 6-unit pending lift.
            float w=Mathf.Min(cellW-6,scale*139+8),height=scale*136+14;
            table.Display(die,result,center,scale,selected);dieCenters[die.dieId]=center;
            var hit=UiKit.Box(main,"Die_"+die.dieId,center.x-w/2,center.y-height/2,w,height,Color.clear,true);
            var pointer=UiKit.Component<DiePointer>(hit.gameObject);pointer.clicked=null;pointer.dragStart=null;pointer.dragged=null;pointer.dragEnd=null;string id=die.dieId;
            UiKit.Component<Art07DieHover>(hit.gameObject).Bind(table,id,!selected&&!modal&&!IsBusy&&ViewState.phase=="Selecting");
            pointer.clicked=e=>{if(modal||IsBusy||draggedDie07!=null)return;if(e.button==UnityEngine.EventSystems.PointerEventData.InputButton.Right){ShowDie(id);return;}Session.TogglePending(id);Refresh();};
            visibleDice.Add(id);
            // Projected half-extents of a thrown die's body.
            float halfW=scale*53,halfH=scale*65;
            if(selected)
            {
                var ring=UiKit.Ring(main,"PendingRing"+id,center.x-halfW-10,center.y-halfH-14,halfW*2+20,halfH*2+22,UiKit.Gold);
                UiKit.Component<Art072Pulse>(ring.gameObject).Configure(UiKit.Gold);
                int order=Array.IndexOf(Session.PendingIds,id);
                if(order>=Math.Max(0,ViewState.stage.capacity-ViewState.hand.orderedLockedIds.Count))
                {
                    var corner=UiKit.Rect(main,"ExcessPending"+id,center.x-halfW-16,center.y-halfH-14,26,26);
                    for(int i=0;i<4;i++){var stripe=UiKit.Box(corner,"Stripe"+i,2+i*6,6,2,18,UiKit.Gold);stripe.localRotation=Quaternion.Euler(0,0,-40);}
                }
                float tagW=Mathf.Min(cellW-8,c?120:96),tagH=c?36:30;
                Vector2 at=new Vector2(center.x-tagW*.5f,center.y+halfH+10);
                var tag=Surface11(main,"PendingTag11_"+id,at.x,at.y,tagW,tagH,"ui_chip_formula",UiKit.Gold);
                NoWrap11(UiKit.Text(main,"Pending"+id,"已选 "+(order+1),at.x,at.y,tagW,tagH,Body11(c?14:17),UiKit.Ink,TextAlignmentOptions.Center));
            }
            // While locked dice fly to the tray the pool re-flows under the badges' final places: they return when it settles.
            if(!LockVisualPlaying07)DieBadges11(die,result,center,halfW,halfH,true,false);
        }
        /// <summary>Shape badges around a die: colour (when thrown and colour assist is on), glass, stage-temporary (PLAY_LAYOUT 4).</summary>
        private void DieBadges11(DieInstance die,DieHandState result,Vector2 center,float halfW,float halfH,bool corner,bool locked,Rect? column=null)
        {
            string id=die.dieId;float size=locked?22:Large11?26:24;
            var shapes=new List<Play11Badge.Shape>();var fills=new List<Color>();
            if(result?.hasRolled==true&&colorAssist07){bool red=result.currentColor=="red";shapes.Add(red?Play11Badge.Shape.Circle:Play11Badge.Shape.Diamond);fills.Add(red?UiKit.Red:UiKit.Blue);}
            if(die.isGlass){shapes.Add(Play11Badge.Shape.Glass);fills.Add(Color.Lerp(UiKit.Blue,UiKit.Paper,.55f));}
            if(die.lifetime=="stage_temporary"){shapes.Add(Play11Badge.Shape.Clock);fills.Add(UiKit.Paper);}
            string[] names={"ColorBadge11_","GlassBadge11_","TempBadge11_"};
            var used=new HashSet<string>();
            for(int i=0;i<shapes.Count;i++)
            {
                string name=(shapes[i]==Play11Badge.Shape.Circle||shapes[i]==Play11Badge.Shape.Diamond?names[0]:shapes[i]==Play11Badge.Shape.Glass?names[1]:names[2])+id;used.Add(name);
                Vector2 p;
                // On a pool die each badge takes a corner of the die's box that its body leaves empty: top right ("骰子右上的
                //小形状角标"), then top left, then bottom right; never over the faces. In a slot they stack in the right column.
                if(column.HasValue)p=new Vector2(column.Value.x+(column.Value.width-size)*.5f,column.Value.y+i*(size+6));
                else
                {
                    float sx=i==1?-1:1,sy=i==2?1:-1;
                    p=new Vector2(center.x+sx*halfW*.72f-size*.5f,center.y+sy*halfH*.9f-size*.5f);
                }
                var r=UiKit.Rect(main,name,p.x,p.y,size,size);
                UiKit.Component<Play11Badge>(r.gameObject).Configure(shapes[i],fills[i],UiKit.Ink,UiKit.Paper);
            }
            foreach(string n in names)if(!used.Contains(n+id)){var stale=main.Find(n+id);if(stale!=null)stale.gameObject.SetActive(false);}
        }

        // ---------------------------------------------------------------- hand resources (Resources11)
        private void DrawResources11()
        {
            var s=ViewState;var l=L11;bool c=Large11;
            var panel=Surface11(main,"Resources11",l.resources.x,l.resources.y,l.resources.width,l.resources.height,"ui_panel_hud",Color.white);
            var budget=IsPresenting07&&beat07?.Phase==DiceCraft.Presentation.PresentationPhase.TargetState&&lastPresentationAfter07?.hand!=null&&lastPresentationAfter07.hand.handId!=lastPresentationBefore07?.hand?.handId?lastPresentationAfter07:s;
            float x=l.resources.x+14,w=l.resources.width-28;
            for(int i=0;i<2;i++)
            {
                bool roll=i==0;float y=l.resources.y+14+i*96;int amount=roll?budget.hand.rollsRemaining:budget.stage.remainingSubmissions;
                UiKit.Inset(main,roll?"RollBudgetWell11":"SubmitBudgetWell11",x,y,w,86);
                var icon=UiKit.Rect(main,roll?"RollBudgetIcon":"SubmitBudgetIcon",x+14,y+25,36,36);
                if(roll){var cup=UiKit.Box(icon,amount==0?"EmptyCup":"Cup",0,0,36,36,amount==0?new Color(1,1,1,.38f):Color.white).GetComponent<Image>();cup.sprite=UiKit.Template("ui_icon_roll");cup.raycastTarget=false;cup.preserveAspect=true;var other=icon.Find(amount==0?"Cup":"EmptyCup");if(other!=null)other.gameObject.SetActive(false);}
                else
                {
                    UiKit.Box(icon,"Ticket",2,6,32,24,UiKit.Paper);UiKit.Box(icon,"TicketLine1",8,12,20,2,UiKit.Ink);UiKit.Box(icon,"TicketLine2",8,19,14,2,UiKit.Ink);
                    UiKit.Box(icon,"LeftNotch",0,15,4,6,UiKit.Background);UiKit.Box(icon,"RightNotch",32,15,4,6,UiKit.Background);
                }
                NoWrap11(UiKit.Text(main,roll?"RollBudgetLabel":"SubmitBudgetLabel",roll?"还能掷":"还能提交",x+60,y,c?180:170,86,c?20:24,UiKit.Muted,TextAlignmentOptions.MidlineLeft));
                var value=NoWrap11(UiKit.Text(main,roll?"RollBudgetValue":"SubmitBudgetValue",amount.ToString(),x+w-130,y,116,86,c?44:52,amount==0?UiKit.Muted:roll?UiKit.Teal:UiKit.Gold,TextAlignmentOptions.MidlineRight));
                value.fontStyle=FontStyles.Bold;
                UiKit.Component<Art072ValuePop>(value.gameObject).Configure(budget.runId+"|"+budget.stage.stageId+"|"+(roll?budget.hand.handId:"submit"),amount.ToString());
            }
            DrawItems11(l);
        }
        private void DrawItems11(Layout11 l)
        {
            var s=ViewState;bool c=Large11;float x=l.resources.x+14,w=l.resources.width-28,y=l.resources.y+210;
            int slots=Math.Min(5,s.consumableSlots);
            bool noItems=ControllerRules.ModifierHas(Config,Session.CurrentModifierId,"NoItems")&&InStageView10;
            // One line tall at the current size (the body size at 125/150 %), above the item row at y + 34.
            float titleSize=Body11(c?13:20);
            NoWrap11(UiKit.Text(main,"ItemsTitle","道具 "+s.inventory.Count+" / "+s.consumableSlots+(noItems?" · 本关禁用":""),x,y,w,Line11(titleSize),titleSize,noItems?Color.Lerp(UiKit.Muted,UiKit.Boss,.6f):UiKit.Muted));
            float size=64,gap=Mathf.Min(12,(w-5*size)/4);
            for(int i=0;i<slots;i++)
            {
                float px=x+i*(size+gap),py=y+34;
                if(i>=s.inventory.Count){UiKit.Ring(main,"ItemEmpty"+i,px,py,size,size,UiKit.PanelLight);continue;}
                var item=s.inventory[i];var b=UiKit.Button(main,"Item_"+item.instanceId,"",px,py,size,size,()=>ShowOwned(item,false),!IsBusy);
                UiKit.CardSurface(b,true);((Image)b.targetGraphic).sprite=UiKit.Template("ui_card_item");
                Icon(b.transform,item.contentId,6,5,size-12);
                Art07AttachTooltip(b,item.instanceId,NameOf(item.contentId),ShortText11(item.contentId)+"\n"+Details11(item.contentId),Art07ItemUnavailable(item)??"可使用 · 点击查看完整说明",()=>ShowOwned(item,false));
                if(Art07ItemUnavailable(item)!=null)
                {
                    var mark=UiKit.Frame(b.transform,"UnavailableLock",size-26,0,26,28,UiKit.Background,UiKit.PanelLight);
                    var symbol=UiKit.Box(mark,"Lock",3,3,20,22,Color.white).GetComponent<Image>();symbol.sprite=UiKit.Template("ui_icon_lock");symbol.raycastTarget=false;
                }
                if(noItems)
                {
                    var slash=UiKit.Box(b.transform,"NoItemsSlash11",-4,size*.5f-2,size*1.3f,4,UiKit.Boss);slash.pivot=new Vector2(.5f,.5f);slash.anchoredPosition=new Vector2(size*.5f,-size*.5f);slash.localRotation=Quaternion.Euler(0,0,45);
                }
            }
            DrawViewDice11(x,y+34+size+12,w,60);
        }
        private void DrawViewDice11(float x,float y,float w,float h)
        {
            bool any=ViewState.dice.Count>0;
            var button=SolidButton11(main,"ViewDice","查看骰子",x,y,w,h,()=>{var first=ViewState.dice.OrderBy(d=>d.createdSerial).FirstOrDefault();if(first!=null)ShowDie(first.dieId);},any&&!IsBusy,22);
            var icon=UiKit.Box(button.transform,"ViewDiceIcon",16,(h-40)*.5f,40,40,any&&!IsBusy?Color.white:new Color(1,1,1,.5f)).GetComponent<Image>();
            icon.sprite=UiKit.Template("ui_icon_dice_view");icon.raycastTarget=false;icon.preserveAspect=true;icon.gameObject.SetActive(icon.sprite!=null);
            Art07AttachTooltip(button,"view-dice","查看骰子","查看每颗骰子的六个面。拖动骰子可以旋转。","不改变骰序、投掷结果或随机数",null);
        }

        // ---------------------------------------------------------------- submission tray (Submit11)
        private void DrawSubmit11()
        {
            var s=ViewState;var h=s.hand;var l=L11;bool c=Large11;
            var info=Surface11(main,"SubmitInfo11",l.info.x,l.info.y,l.info.width,l.info.height,"ui_panel_hud",Color.white);
            float ix=l.info.x+16,iw=l.info.width-32,y=l.info.y+10;
            var title=NoWrap11(UiKit.Text(main,"SlotsTitle",$"提交栏 {h.orderedLockedIds.Count} / {s.stage.capacity}",ix,y,iw,36,c?20:24,UiKit.TextColor,TextAlignmentOptions.MidlineLeft));
            y+=38;
            if(h.orderedLockedIds.Count>0)
            {
                var rule=UiKit.Text(main,"LockRule","已锁定 · 本手不可移回骰池",ix,y,iw,30,Body11(c?15:18),UiKit.Gold);
                rule.ForceMeshUpdate();float rh=Mathf.Ceil(rule.GetPreferredValues(rule.text,iw,0).y)+2;rule.rectTransform.sizeDelta=new Vector2(iw,rh);y+=rh+4;
            }
            submitHandTop11=y;
            // Expected hand block ("Hand") is drawn with the score row (ScorePanel), it changes with every beat.
            if(s.lastSubmission!=null)SolidButton11(main,"PreviousSubmission","上一手明细",ix,l.info.yMax-70,iw,60,()=>ShowBreakdown(ViewState.lastSubmission.snapshot,ViewState.lastSubmission.breakdown),!IsBusy,c?17:21);
            // The rail: an open tray frame; the dice show through its centre.
            var rail=Surface11(main,"LockedTray071",l.rail.x,l.rail.y,l.rail.width,l.rail.height,"ui_dice_tray",Color.Lerp(UiKit.Panel,UiKit.PanelLight,.55f));
            slotPage=Math.Min(slotPage,Math.Max(0,(s.stage.capacity-1)/8));int slots=Math.Min(8,s.stage.capacity-slotPage*8);
            // More than eight slots page; the page button then owns the rail's left end and the slots start after it.
            float pagerRoom=s.stage.capacity>8?72:0;
            float inner=l.rail.width-48-pagerRoom,gap=slots>6?8:12,slotW=Mathf.Min(152,(inner-gap*(slots-1))/Math.Max(1,slots)),slotH=166;
            float sx=l.rail.x+24+pagerRoom+(inner-(slots*slotW+(slots-1)*gap))*.5f,sy=l.rail.y+16;
            var sealed10=SealedPreview10();
            for(int j=0;j<slots;j++)
            {
                int i=j+slotPage*8;float x=sx+j*(slotW+gap);bool locked=i<h.orderedLockedIds.Count;
                UiKit.Ring(main,"Slot"+i,x,sy,slotW,slotH,locked?Color.Lerp(UiKit.Gold,UiKit.Paper,.15f):new Color(UiKit.Ghost.r,UiKit.Ghost.g,UiKit.Ghost.b,.75f));
                var index=NoWrap11(UiKit.Text(main,"SlotIndex"+i,(i+1).ToString(),x+slotW-30,sy+6,24,26,c?14:16,UiKit.Muted,TextAlignmentOptions.Center));
                if(locked)
                {
                    var id=h.orderedLockedIds[i];var die=s.dice.First(d=>d.dieId==id);var result=h.currentResults.First(r=>r.dieId==id);
                    // A locked die (18-degree turn) projects about 101 units wide per unit of scale: it stands 2 units in from
                    // the slot's left edge, which leaves the post-score change text (DieState) its full width on the right.
                    float scale=slotW>=148?.82f:slotW>=128?.76f:.70f,half=50.5f*scale;Vector2 center=new Vector2(x+2+half,sy+slotH-12-52);
                    lockedSlotRects07[id]=new Rect(x,sy,slotW,slotH);
                    var lip=UiKit.Box(main,"SlotTray073_"+i,x+4,sy+slotH-6,slotW-8,20,Color.white).GetComponent<Image>();
                    lip.sprite=UiKit.Template("ui_tray_front");lip.type=lip.sprite!=null?Image.Type.Sliced:Image.Type.Simple;lip.raycastTarget=false;lip.gameObject.SetActive(lip.sprite!=null);
                    DrawSlotDie11(die,result,center,i,scale,new Rect(x,sy,slotW,slotH));
                    if(i<sealed10.Length&&sealed10[i])SealedMark11(x,sy,slotW,slotH,i);
                }
                else UiKit.Text(main,"EmptySlot"+i,"·  ·  ·",x+8,sy+60,slotW-16,48,20,UiKit.PanelLight,TextAlignmentOptions.Center);
            }
            if(s.stage.capacity>8)
            {
                float pageSize=Mathf.Max(60,40/Mathf.Max(.1f,canvas.scaleFactor));
                UiKit.Button(main,"SlotPage",">",l.rail.x+8,l.rail.y+12,pageSize,pageSize,()=>{slotPage=(slotPage+1)%((s.stage.capacity+7)/8);Refresh();});
                var tag=Surface11(main,"SlotPageTab11",l.rail.x+8,l.rail.y+16+pageSize,pageSize,34,"ui_chip_formula",UiKit.Panel);
                NoWrap11(UiKit.Text(tag,"SlotPageIndex",(slotPage+1)+" / "+((s.stage.capacity+7)/8),0,0,pageSize,34,15,UiKit.Muted,TextAlignmentOptions.Center));
            }
        }
        private float submitHandTop11=788;
        /// <summary>Hit-local x of each locked die's grip (right of the die), set when the slot is drawn.</summary>
        private readonly Dictionary<string,float> GripX11=new Dictionary<string,float>(StringComparer.Ordinal);
        private void DrawSlotDie11(DieInstance die,DieHandState result,Vector2 center,int index,float scale,Rect slot)
        {
            string id=die.dieId;
            table.Display(die,result,center,scale,false);dieCenters[id]=center;
            // The die's own part of the slot: everything left of the right column.
            float hitX=slot.x+2,w=slot.width-36,height=118;
            var hit=UiKit.Box(main,"Die_"+id,hitX,center.y-height/2,w,height,Color.clear,true);
            var pointer=UiKit.Component<DiePointer>(hit.gameObject);pointer.clicked=null;pointer.dragStart=null;pointer.dragged=null;pointer.dragEnd=null;
            UiKit.Component<Art07DieHover>(hit.gameObject).Bind(table,id,false);
            pointer.clicked=e=>{if(modal||IsBusy||draggedDie07!=null)return;ShowDie(id);};
            pointer.dragStart=e=>{if(e.button!=UnityEngine.EventSystems.PointerEventData.InputButton.Left||!CanMoveLockedDice07)return;draggedDie07=id;e.eligibleForClick=false;};
            pointer.dragged=e=>
            {
                if(draggedDie07!=id)return;var pos=ScreenToDesign(e.position);
                table.DisplayDragged(die,result,pos,scale*1.06f);
                UiKit.Outline(main,"DragDieOrigin07",hitX,center.y-height/2,w,height,UiKit.Muted,2);
                if(TryDieInsertion07(pos,out _,out var edge))dieInsertion07=UiKit.Box(overlay,"DieInsertion",edge.x,edge.y,edge.width,edge.height,UiKit.Gold);
                else if(dieInsertion07!=null)dieInsertion07.gameObject.SetActive(false);
            };
            pointer.dragEnd=e=>
            {
                if(draggedDie07!=id)return;e.eligibleForClick=false;
                if(!TryDieInsertion07(ScreenToDesign(e.position),out int to,out _)){CancelDieDrag07();return;}
                var ids=ViewState.hand.orderedLockedIds.ToList();int from=ids.IndexOf(id);if(from==to){CancelDieDrag07();return;}
                ids.RemoveAt(from);ids.Insert(to,id);CancelDieDrag07(false);
                float duration=table.LayoutDuration;table.LayoutDuration=.14f;
                try{Send(new SessionCommand{kind=CommandKind.Reorder,dieIds=ids.ToArray()});}finally{table.LayoutDuration=duration;}
            };
            // The narrow grip stands in the gap right of the die (its left edge is the slot's edge), inside the same hit.
            GripX11[id]=Mathf.Min(w-12,center.x+50.5f*scale+4-hitX);
            DrawLockedGrip07(hit,id,false);
            // Right column of the slot: padlock, then the shape badges; the permanent-change label uses the same column.
            Rect column=new Rect(slot.x+slot.width-32,slot.y+40,28,slot.height-48);
            DrawLockMark07(column.x+3,slot.y+36,index,id);
            DieBadges11(die,result,center,0,0,false,true,new Rect(column.x,column.y+30,column.width,column.height-30));
            // The permanent-change label (PostScore beat) writes into this label; it is empty otherwise.
            var label=UiKit.Text(main,"DieState"+id,"",center.x+44,slot.y+40,slot.xMax-4-(center.x+44),slot.height-48,14,result?.currentColor=="red"?UiKit.Red:UiKit.Blue);
            label.textWrappingMode=TextWrappingModes.NoWrap;
        }
        private void SealedMark11(float x,float y,float w,float h,int index)
        {
            UiKit.Outline(main,"SealedSlot10_"+index,x+3,y+3,w-6,h-6,UiKit.Danger,2);
            // While the hand scores, the die's ticket in the same band says "封印"; one word, not two on top of each other.
            if(IsPresenting07)return;
            var tag=NoWrap11(UiKit.Text(main,"SealedTag10_"+index,"封印",x+8,y+6,56,Line11(Body11(16)),Body11(16),UiKit.Danger));tag.fontStyle=FontStyles.Bold;
        }

        // ---------------------------------------------------------------- settle panel (above EndStage)
        private void DrawSettle11()
        {
            // While a hand scores the panel follows the receipt: the gap counts down as the total rolls in (TickScore11) and
            // from the post-score beats on it shows the committed stage, which may just have reached its target.
            var s=IsPresenting07&&beat07!=null&&beat07.Phase>=DiceCraft.Presentation.PresentationPhase.PostScore&&lastPresentationAfter07?.stage?.stageId==ViewState.stage.stageId?lastPresentationAfter07:ViewState;
            var l=L11;bool c=Large11;
            var panel=Surface11(main,"Settle11",l.settle.x,l.settle.y,l.settle.width,l.settle.height,"ui_panel_hud",Color.white);
            float x=l.settle.x+16,w=l.settle.width-32,y=l.settle.y+12;
            // Redrawn between beats without a full rebuild: the other variant's labels must go.
            foreach(string other in s.stage.qualified?new[]{"GoalGapLabel11","GoalGap11","GoalReward11"}:new[]{"Qualified","QualifiedReward0","QualifiedReward1","QualifiedReward2","QualifiedContinueIncome"})
            {var stale=main.Find(other);if(stale!=null)stale.gameObject.SetActive(false);}
            if(s.stage.qualified)
            {
                var r=Session.PreviewStageSettlement();
                // Every line as tall as its line at the current text size: four or five lines fit 150 % (AX-03).
                float head=c?18:24,line=Body11(c?13:19);
                NoWrap11(UiKit.Text(main,"Qualified",r==null?"已达标":"现在结束 +"+PlayerNumberFormat.Gold(r.goldAfter-r.goldBefore)+" 金币",x,y,w,Line11(head),head,UiKit.Gold));
                y+=Line11(head)+2;
                if(r!=null)
                {
                    // v1.1 glossary (text proposal 2.2): 过关奖励, 剩余提交奖励, 利息.
                    string[] lines={"过关奖励  +"+PlayerNumberFormat.Gold(r.baseReward),"剩余提交奖励  +"+PlayerNumberFormat.Gold(r.unusedSubmissionReward),"利息  +"+PlayerNumberFormat.Gold(r.interest)+(r.controllerIncome>0?"  ·  铁饭碗 +"+PlayerNumberFormat.Gold(r.controllerIncome):"")};
                    for(int i=0;i<lines.Length;i++)NoWrap11(UiKit.Text(main,"QualifiedReward"+i,lines[i],x,y+i*Line11(line),w,Line11(line),line,UiKit.TextColor));
                    y+=lines.Length*Line11(line)+2;
                }
                int income=ViewSources.Count(v=>v.abilityId=="C01")*Config.Controller("C01").parameters.goldPerValidSubmission;
                if(income>0)NoWrap11(UiKit.Text(main,"QualifiedContinueIncome","续交 "+NameOf("C01")+"  +"+income+" / 次",x,y,w,Line11(line),line,UiKit.Gold));
                return;
            }
            var gap=ExactScoreMath.Parse(s.stage.target)-ExactScoreMath.Parse(ReferenceEquals(s,ViewState)?DisplayedStageScore11():s.stage.score);if(gap.Sign<0)gap=0;
            float labelSize=Body11(c?18:22),gapSize=c?30:44,labelH=Line11(labelSize),gapH=Line11(gapSize);
            NoWrap11(UiKit.Text(main,"GoalGapLabel11","离目标还差",x,y,w,labelH,labelSize,UiKit.Muted));
            var value=NoWrap11(UiKit.Text(main,"GoalGap11",CompactScore(gap.ToString()),x,y+labelH,w,gapH,gapSize,PressureActive11?UiKit.Pressure:UiKit.TextColor));
            value.fontStyle=FontStyles.Bold;
            int reward=s.stage.reward>0?s.stage.reward:s.stage.risk?Config.stages[s.stage.stageIndex].riskReward:Config.stages[s.stage.stageIndex].normalReward;
            float noteY=y+labelH+gapH+2;
            var note=UiKit.Text(main,"GoalReward11","达标后结束：过关奖励 +"+reward+" 金币，剩下的每次提交 +"+Config.goldPerUnusedSubmission,x,noteY,w,l.settle.yMax-8-noteY,Body11(c?15:18),UiKit.Muted);
            note.lineSpacing=-10;
        }

        // ---------------------------------------------------------------- actions (Actions11, PLAY_LAYOUT 2)
        private void DrawActions11()
        {
            var s=ViewState;var h=s.hand;var l=L11;bool c=Large11,busy=IsBusy,pending=Session.PendingIds.Length>0;
            var unlocked=s.dice.Count(d=>!h.orderedLockedIds.Contains(d.dieId));
            int lockRoom=Math.Max(0,s.stage.capacity-h.orderedLockedIds.Count);bool excess=Session.PendingIds.Length>lockRoom;
            bool canRoll=h.rollsRemaining>0&&unlocked>0;
            bool canSubmit=h.hasRolled&&h.orderedLockedIds.Count>0&&s.stage.remainingSubmissions>0&&!MustFillBlocked10;
            string key=Art07BindingLabel("Roll");
            string reason;
            if(busy)
            {
                reason=IsPresenting07?"计分中…":LockVisualPlaying07?"锁定中…":TransactionVisualPlaying07?"":"掷骰中…";
                if(!string.IsNullOrEmpty(LastTransactionSummary07)&&!IsPresenting07)reason=LastTransactionSummary07;
                // While the dice are in the air the pressed button stays where it was, disabled, with its short progress dots
                // (v0.72 feedback); the row changes to the next step when they land.
                if(!IsPresenting07&&!LockVisualPlaying07&&!TransactionVisualPlaying07&&h.rollsRemaining>=0)
                    BindRollButtonFeedback07(PrimaryButton11("Roll",h.hasRolled?"重掷 · 还能掷 "+h.rollsRemaining+" ["+key+"]":"掷骰 ["+key+"]",()=>Send(new SessionCommand{kind=CommandKind.Roll}),false,l));
            }
            else if(!h.hasRolled)
            {
                reason=canRoll?"按“掷骰”开始这一手。":"没有可掷的骰子：提交已锁定的骰子。";
                if(canRoll)BindRollButtonFeedback07(PrimaryButton11("Roll","掷骰 ["+key+"]",()=>Send(new SessionCommand{kind=CommandKind.Roll}),true,l));
            }
            else if(pending)
            {
                reason=excess?"提交栏只剩 "+lockRoom+" 格：先取消多选的骰子。":"按“锁定”留下选中的骰子；再点一次骰子可以取消。";
                PrimaryButton11("Confirm",excess?"最多锁 "+lockRoom+" 颗":"锁定 "+Session.PendingIds.Length+" 颗",()=>Send(new SessionCommand{kind=CommandKind.ConfirmLocks}),!excess,l);
                SecondaryButton11("Clear","取消选择",()=>{Session.ClearPending();Refresh();},l);
            }
            else if(canSubmit)
            {
                reason=h.orderedLockedIds.Count==s.stage.capacity||!canRoll?"按“提交”计分。":"按“提交”计分，或者重掷其余的骰子。";
                PrimaryButton11("Submit","提交 "+h.orderedLockedIds.Count+" 颗",()=>Send(new SessionCommand{kind=CommandKind.Submit}),true,l);
                if(canRoll)BindRollButtonFeedback07(SecondaryButton11("Roll","重掷 · 还能掷 "+h.rollsRemaining+" ["+key+"]",()=>Send(new SessionCommand{kind=CommandKind.Roll}),l));
            }
            else
            {
                reason=h.orderedLockedIds.Count>0&&MustFillBlocked10?"首领减益“满员”：要放满提交栏（"+Math.Min(s.stage.capacity,s.dice.Count)+" 颗）才能提交。":
                    canRoll?"点选要留下的骰子，再按“锁定”；也可以重掷。":"投掷已用完：点选要留下的骰子，再按“锁定”。";
                if(canRoll)BindRollButtonFeedback07(PrimaryButton11("Roll","重掷 · 还能掷 "+h.rollsRemaining+" ["+key+"]",()=>Send(new SessionCommand{kind=CommandKind.Roll}),true,l));
            }
            // Once the target is reached the next step is a choice: keep playing or end the stage.
            if(!busy&&!pending&&s.stage.qualified&&s.stage.remainingSubmissions>0&&string.IsNullOrEmpty(LastTransactionSummary07))
                reason=!h.hasRolled&&canRoll?"已达标：按“掷骰”继续，或者按“结束本关”结算。":"已达标：可以继续提交，也可以结束本关。";
            if(!busy&&!pending&&!string.IsNullOrEmpty(LastTransactionSummary07))reason=LastTransactionSummary07;
            // Hint tab: a surface for the text (never written straight on the table).
            var tab=Surface11(main,"ActionReasonUnderlay",l.hint.x,l.hint.y,l.hint.width,l.hint.height,"ui_panel_hud",Color.white);
            var hint=UiKit.Text(main,"ActionReason",reason,l.hint.x+16,l.hint.y+6,l.hint.width-32,l.hint.height-12,Body11(c?16:21),UiKit.TextColor,TextAlignmentOptions.MidlineLeft);
            hint.lineSpacing=-8;hint.enableAutoSizing=true;hint.fontSizeMax=Body11(c?16:21)*UiKit.FontScale;hint.fontSizeMin=Body11(c?13:17)*UiKit.FontScale;
            // End the stage: always bottom right; disabled with its reason until the target is reached.
            if(busy)
            {
                SolidButton11(main,"Skip","跳过演出",l.end.x,l.end.y,l.end.width,l.end.height,SkipVisual,true,24);
            }
            else
            {
                bool can=Session.CanEndStage;
                var end=UiKit.Button(main,"EndStage",can?"结束本关 · 结算":"结束本关 · 结算\n<size=72%>达标后可结束</size>",l.end.x,l.end.y,l.end.width,l.end.height,()=>Send(new SessionCommand{kind=CommandKind.EndStage}),can);
                var label=end.GetComponentInChildren<TextMeshProUGUI>();label.richText=true;label.fontSize=(c?20:24)*UiKit.FontScale;label.lineSpacing=-12;
                if(s.stage.qualified)UiKit.Ring(end.transform,"QualifiedGoldBorder",-4,-4,l.end.width+8,l.end.height+4,UiKit.Reward);
            }
        }
        private UnityEngine.UI.Button PrimaryButton11(string name,string label,Action click,bool enabled,Layout11 l)
        {
            var b=UiKit.ActionButton(main,name,label,l.primary.x,l.primary.y,l.primary.width,l.primary.height,click,UiKit.Primary,enabled);
            var text=b.GetComponentInChildren<TextMeshProUGUI>();text.fontSize=(Large11?22:28)*UiKit.FontScale;text.textWrappingMode=TextWrappingModes.NoWrap;
            return b;
        }
        private UnityEngine.UI.Button SecondaryButton11(string name,string label,Action click,Layout11 l)
        {
            if(name==null)return null;
            var b=SolidButton11(main,name,label,l.secondary.x,l.secondary.y,l.secondary.width,l.secondary.height,click,true,Large11?17:21);
            var text=b.GetComponentInChildren<TextMeshProUGUI>();text.textWrappingMode=TextWrappingModes.Normal;text.lineSpacing=-10;
            return b;
        }
        /// <summary>
        /// A second-level button with its own dark surface. The v0.72 ghost button is an outline with light text: on a light
        /// table theme its text would stand on the table itself (THEME_SPEC 5), so play-screen buttons always carry a face.
        /// </summary>
        private UnityEngine.UI.Button SolidButton11(Transform parent,string name,string label,float x,float y,float w,float h,Action click,bool enabled,float font)
        {
            var b=UiKit.FlatButton(parent,name,label,x,y,w,h,click,enabled);
            var face=(Image)b.targetGraphic;face.sprite=UiKit.Template("ui_chip_formula");face.type=face.sprite!=null?Image.Type.Sliced:Image.Type.Simple;
            var tint=Color.Lerp(UiKit.Background,UiKit.Panel,.55f);if(!enabled)tint=Color.Lerp(tint,UiKit.PanelLight,.25f);face.color=tint;
            var text=b.GetComponentInChildren<TextMeshProUGUI>();text.fontSize=font*UiKit.FontScale;text.color=enabled?UiKit.TextColor:UiKit.Muted;
            return b;
        }

        // ---------------------------------------------------------------- table mood (B2: boss stage)
        private float tableMoodShown11=-1;
        /// <summary>Boss stage: the felt darkens and a vignette closes in (felt shader properties, so dice and interface are untouched).</summary>
        private void ApplyTableMood11()
        {
            float mood=FramePhase11&&ViewState.phase!="StageSettlement"&&BossActive11?1:0;
            if(Mathf.Approximately(mood,tableMoodShown11))return;
            var felt=GameObject.Find("Art07Felt");if(felt==null)return;var r=felt.GetComponent<Renderer>();if(r==null)return;
            var props=new MaterialPropertyBlock();r.GetPropertyBlock(props);
            props.SetFloat("_Dim11",mood*.26f);props.SetFloat("_Vignette11",mood*.5f);r.SetPropertyBlock(props);
            tableMoodShown11=mood;
        }
        public float TableMood11=>tableMoodShown11<0?0:tableMoodShown11;
    }
}
