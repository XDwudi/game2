using System;
using System.Linq;
using BigInteger = System.Numerics.BigInteger;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DiceCraft
{
    // Presentation only. Coordinates refer to the design root shared by UI and DiceTable.
    public sealed partial class GameApp
    {
        public const string ArtVersion="art0.7.0";
        private int textScale07=100;
        private bool colorAssist07=true, instant07, shake07=true, grain07=true;
        private string draggedDie07;
        private RectTransform dieInsertion07;
        private readonly System.Collections.Generic.Dictionary<string,Rect> lockedSlotRects07=new System.Collections.Generic.Dictionary<string,Rect>();
        private bool Compact07 => Screen.safeArea.width<=1366 && textScale07>=125;
        private float C07(float physical)=>physical*1.5f;
        private void InitializeArtPreferences07()
        {
            textScale07=PlayerPrefs.GetInt("art07.textScale",100);
            if(textScale07!=100&&textScale07!=125&&textScale07!=150)textScale07=100;
            UiKit.FontScale=textScale07/100f;
            colorAssist07=PlayerPrefs.GetInt("art07.colorAssist",1)==1;
            instant07=PlayerPrefs.GetInt("art07.instant",0)==1;
            shake07=PlayerPrefs.GetInt("art07.shake",1)==1;
            grain07=PlayerPrefs.GetInt("art07.grain",1)==1;
            InitializeSettingsPreferences07();
        }
        private void DrawNavigation07()
        {
            float x=Compact07?C07(24):40,w=Compact07?C07(192):288;
            if(ViewState.phase=="Shop"){UiKit.Button(main,"Pause","设置",40,316,132,60,Pause);UiKit.Button(main,"Handbook","手册",186,316,132,60,ShowHandbook);return;}
            UiKit.Button(main,"Pause","设置 / 暂停",x,Compact07?C07(650):944,w,60,Pause);
            UiKit.Button(main,"Handbook","牌型手册 [H]",x,Compact07?C07(600):870,w,60,ShowHandbook,!IsBusy&&!Session.HasPendingSave);
            if(ViewState.lastSubmission!=null)UiKit.Button(main,"PreviousSubmission","上一手得分明细",x,Compact07?C07(550):802,w,60,()=>ShowBreakdown(ViewState.lastSubmission.snapshot,ViewState.lastSubmission.breakdown),!IsBusy);
            if(DebugUnlocked&&IsDebugWorkspace)UiKit.Button(main,"DebugWorkspace","组合试验",x,Compact07?C07(500):730,w,60,ShowDebugPanel,!IsBusy);
        }
        private void Header()
        {
            bool c=Compact07;float x=c?18:24,y=c?18:24,w=c?324:320,h=c?1044:1032;
            if(ViewState.phase=="Shop"){DrawShopWallet07();return;}
            if(QualifiedStampPlayingForCurrentStage07||ViewState.stage.qualified&&ViewState.hand!=null&&(ViewState.phase=="HandReady"||ViewState.phase=="Selecting")){DrawQualifiedHeader07(x,y,w,h);return;}
            var p=UiKit.Frame(main,"Stats07",x,y,w,h,UiKit.Background,UiKit.PanelLight);
            UiKit.Text(p,"Brand","骰子构筑",18,20,w-36,64,c?30:36,UiKit.Paper);
            UiKit.Text(p,"Edition",ViewState.debugWorkspace?"调试对局":"复古街机骰桌",18,90,w-36,36,18,ViewState.debugWorkspace?UiKit.Violet:UiKit.Muted);
            int n=ViewState.stage.stageIndex+1;
            UiKit.Text(p,"Stage",$"第 {n} 关",18,150,w-36,70,38,UiKit.Paper);
            UiKit.Text(p,"Chapter",$"第 {(n-1)/3+1} 章 · {(n-1)%3+1} / 3",18,224,w-36,40,20,UiKit.Muted);
            Stat07(p,"Target","目标分数",CompactScore(ViewState.stage.target),286,w,UiKit.Red);
            Stat07(p,"StageScore","本关累计",CompactScore(ViewState.stage.score),410,w,UiKit.Blue);
            Stat07(p,"Money","金币",ViewState.gold.ToString(),534,w,UiKit.Gold);
            if(ViewState.phase=="Shop")
            {
                UiKit.Text(p,"ShopBudget",$"普通槽 {ControllerRules.OrdinaryUsed(ViewState.controllers)} / {ViewState.controllerSlots}\n总持有 {ViewState.controllers.Count} / 10\n永久骰 {ViewState.dice.Count(d=>d.lifetime=="permanent")} / 8",18,672,w-36,134,20,UiKit.Muted);
            }
            else if(ViewState.hand!=null&&(ViewState.phase=="HandReady"||ViewState.phase=="Selecting"))
            {
                DrawBudgetCounters07(p);
                if(ViewState.stage.qualified)UiKit.Text(p,"Qualified",QualifiedSummary07(),18,744,w-36,42,c?17:20,UiKit.Gold);
            }
            if(ViewState.debugWorkspace)UiKit.Box(main,"DebugMark",0,0,1920,6,UiKit.Violet);
        }
        private void Stat07(Transform parent,string id,string label,string value,float y,float width,Color color)
        {
            UiKit.Outline(parent,id+"Frame",12,y,width-24,110,UiKit.PanelLight);
            UiKit.Text(parent,id+"Label",label,26,y+10,width-52,34,20,UiKit.Muted);
            var t=UiKit.Text(parent,id,value,26,y+42,width-52,66,value.Length>10?24:textScale07>100?28:37,color);
            t.textWrappingMode=TextWrappingModes.NoWrap;t.overflowMode=TextOverflowModes.Ellipsis;
        }
        private void DrawBudgetCounters07(Transform parent,float top=646)
        {
            for(int i=0;i<2;i++)
            {
                bool roll=i==0;float x=roll?18:166;int amount=roll?ViewState.hand.rollsRemaining:ViewState.stage.remainingSubmissions;
                UiKit.Text(parent,roll?"RollBudgetLabel":"SubmitBudgetLabel",roll?"投掷":"提交",x,top,138,34,14,UiKit.Muted);
                var icon=UiKit.Rect(parent,roll?"RollBudgetIcon":"SubmitBudgetIcon",x,top+34,30,28);
                if(roll&&amount==0)
                {
                    UiKit.Box(icon,"EmptyCupBackground",0,0,30,28,Color.Lerp(UiKit.Background,UiKit.Blue,.25f));
                    // Stepped hollow circle in the existing pixel-symbol style.
                    foreach(var r in new[]{new Rect(9,2,12,3),new Rect(5,5,4,3),new Rect(2,8,3,12),new Rect(5,20,4,3),new Rect(9,23,12,3),new Rect(21,20,4,3),new Rect(25,8,3,12),new Rect(21,5,4,3)})
                        UiKit.Box(icon,"Ring"+r.x+"_"+r.y,r.x,r.y,r.width,r.height,UiKit.Blue);
                }
                else if(roll)
                {
                    var cup=UiKit.Box(icon,"Cup",1,0,28,28,Color.white).GetComponent<UnityEngine.UI.Image>();cup.sprite=UiKit.Template("ui_icon_roll");cup.raycastTarget=false;
                }
                else
                {
                    UiKit.Box(icon,"Ticket",1,3,28,22,UiKit.Paper);
                    UiKit.Box(icon,"TicketLine1",6,8,18,2,UiKit.Ink);UiKit.Box(icon,"TicketLine2",6,14,12,2,UiKit.Ink);
                    UiKit.Box(icon,"LeftNotch",0,12,4,5,UiKit.Background);UiKit.Box(icon,"RightNotch",26,12,4,5,UiKit.Background);
                }
                UiKit.Text(parent,roll?"RollBudgetValue":"SubmitBudgetValue",amount.ToString(),x+38,top+24,100,52,22,amount==0?UiKit.Muted:UiKit.Teal,TextAlignmentOptions.MidlineLeft);
            }
        }
        private void Menu()
        {
            UiKit.Text(main,"Overline","DICE  /  BUILD  /  DECIDE",120,102,880,50,24,UiKit.Gold);
            var title=UiKit.Text(main,"Title","骰子构筑",112,170,920,150,96,UiKit.Paper);
            float titleHeight=Mathf.Max(150,title.GetPreferredValues(title.text,920,float.PositiveInfinity).y);
            title.rectTransform.sizeDelta=new Vector2(920,titleHeight);
            float subtitleY=Mathf.Max(330,170+titleHeight+8);
            var subtitle=UiKit.Text(main,"Subtitle","把一次投掷，变成你的构筑。",120,subtitleY,880,70,30,UiKit.Muted);
            float subtitleHeight=Mathf.Max(70,subtitle.GetPreferredValues(subtitle.text,880,float.PositiveInfinity).y);
            subtitle.rectTransform.sizeDelta=new Vector2(880,subtitleHeight);
            float artworkOffset=Mathf.Max(0,subtitleY+subtitleHeight+24-424);
            for(int i=0;i<4;i++)
            {
                var back=UiKit.Frame(main,"MenuCardBack"+i,150+i*205,424+artworkOffset,148,176,UiKit.Paper,UiKit.Ink);
                UiKit.Outline(back,"PrintedBorder",10,10,128,156,UiKit.Ink,2);
                UiKit.Outline(back,"Inset",17,17,114,142,UiKit.Teal,2);
                var seal=UiKit.Box(back,"BackSeal",54,68,40,40,UiKit.Ink);
                seal.pivot=new Vector2(.5f,.5f);seal.anchoredPosition=new Vector2(74,-88);seal.localRotation=Quaternion.Euler(0,0,45);
                UiKit.Box(back,"SealCenter",67,81,14,14,UiKit.Paper);
                for(int corner=0;corner<4;corner++)UiKit.Box(back,"Corner"+corner,corner%2==0?24:116,corner<2?24:144,8,8,UiKit.Teal);
            }
            for(int i=0;i<3;i++)
            {
                var d=new DieInstance{dieId="$menu"+i,createdSerial=i,lifetime="permanent",faces=Enumerable.Range(1,6).Select(f=>new FaceState{faceId="F"+f.ToString("00"),permanentPip=f,baseColor=f==1||f==4?"red":"blue"}).ToArray()};
                table.Display(d,new DieHandState{currentFaceId="F0"+(i*2+1),currentPip=i*2+1,currentColor=i==0?"red":"blue",hasRolled=true,isLocked=true},new Vector2(260+i*250,720+artworkOffset),1.8f);
            }
            UiKit.Text(main,"MenuRules","投掷  →  选择  →  锁定  →  提交",120,866+artworkOffset,900,60,28,UiKit.Paper);
            UiKit.Text(main,"LockHint","确认锁定后，本手不可直接解除。",120,944+artworkOffset,900,52,23,UiKit.Muted);
            var p=UiKit.Frame(main,"MenuPanel",1120,220,560,640,UiKit.Background,UiKit.PanelLight);
            UiKit.Text(p,"MenuLabel","一桌新的可能",40,28,480,55,30,UiKit.Gold);
            UiKit.Button(p,"ContinueRun",HasIncompatibleSave07?"旧版本存档":"继续对局",40,108,480,64,ContinueRun07,ViewState!=null||HasIncompatibleSave07,true);
            UiKit.Button(p,"NewRun","新建挑战",40,188,480,64,ShowNewRun07);
            UiKit.Button(p,"MenuHandbook","牌型手册",40,268,480,64,ShowMenuHandbook07);
            UiKit.Button(p,"MenuSettings","设置",40,348,480,64,ShowSettings07);
            UiKit.Button(p,"Quit","退出游戏",40,428,480,64,ConfirmQuit07);
            UiKit.Text(p,"SavedSeed",ViewState==null?(HasIncompatibleSave07?IncompatibleVersionSummary07():"暂无存档 · 开始你的第一局"):$"存档：第 {ViewState.stage.stageIndex+1} 关 · {ViewState.gold} 金币\n{(ViewState.debugWorkspace?"调试":"普通")}对局 · 种子 {ViewState.seed}",40,520,480,90,20,UiKit.Muted);
            var identityAsset=Resources.Load<TextAsset>("BuildIdentity");
            var identity=identityAsset==null?null:JsonUtility.FromJson<BuildIdentity>(identityAsset.text);
            string build=Application.isEditor?null:identity?.sourceCommit;build=string.IsNullOrEmpty(build)?"开发中":build.Substring(0,Math.Min(8,build.Length));
            UiKit.Text(main,"Version",Config.configVersion+" / "+ArtVersion+" / 构建 "+build,1000,946,880,44,20,UiKit.Muted,TextAlignmentOptions.TopRight);
            UiKit.Button(main,"BuildVersion","版本与构建",1510,998,280,60,ShowBuildVersion);
            DrawNotice07();
            var veil=UiKit.Box(main,"MenuEntranceVeil",0,0,1920,1080,UiKit.Background).GetComponent<UnityEngine.UI.Image>();veil.transform.SetAsLastSibling();
            UiKit.Component<Art07MenuReveal>(main.gameObject).Bind("menu:"+(ViewState?.runId??"new")+":"+(ViewState?.actionSequence??0),p,veil,
                (RectTransform)p.Find("ContinueRun"),(RectTransform)p.Find("NewRun"),(RectTransform)p.Find("MenuHandbook"),(RectTransform)p.Find("MenuSettings"),(RectTransform)p.Find("Quit"));
        }
        private void ShowNewRun07()
        {
            OpenNewRunPanel07();
        }
        private void ShowMenuHandbook07()
        {
            ShowHandbook();
        }
        private void ConfirmQuit07(){var p=OpenModal("退出游戏");UiKit.Text(p,"QuitHint","最近一次成功操作已保存。",40,160,1500,100,32);UiKit.ActionButton(p,"ConfirmQuit","退出",500,430,600,72,()=>Application.Quit(),UiKit.Paper);}
        private void StagePreview()
        {
            DrawControllers07();
            for(int i=0;i<24;i++)
            {
                float x=390+i*60+(i/3)*8;bool complete=i<ViewState.stage.stageIndex,current=i==ViewState.stage.stageIndex;
                var fill=complete?UiKit.Teal:current?UiKit.Gold:UiKit.Panel;
                UiKit.Box(main,"StageTick"+i,x,300,42,32,fill);
                UiKit.Text(main,"StageTickLabel"+i,complete?"✓":(i+1).ToString(),x,300,42,32,16,fill==UiKit.Panel?UiKit.Muted:UiKit.Ink,TextAlignmentOptions.Center);
                if(current)UiKit.Outline(main,"CurrentStageOutline",x-3,297,48,38,UiKit.Gold,2);
            }
            var def=Config.stages[ViewState.stage.stageIndex];float x0=def.riskEnabled?464:576;
            ChallengeCard07("Normal","普通挑战",tutorial?"50":def.normalTarget,def.normalReward,x0,false);
            if(def.riskEnabled)ChallengeCard07("Risk","高压挑战",def.riskTarget,def.riskReward,1056,true);
            UiKit.Text(main,"PreviewBudgets",$"投掷 {Session.InitialRolls} 次 / 手     提交 {Session.InitialSubmissions} 次 / 关     容量 K {Session.EffectiveCapacity}",416,830,1380,58,26,UiKit.Muted);
            UiKit.Text(main,"ControllerOrderWarning","入关后，控制器顺序固定。",416,890,1380,40,20,UiKit.Gold);
            UiKit.ActionButton(main,"StartStage","开始挑战 →",816,936,720,76,StartPreviewStage07,UiKit.Blue);
            BindStageCardsEntrance07();
        }
        private void ChallengeCard07(string id,string title,string score,int reward,float x,bool risk)
        {
            bool selected=PreviewRisk07==risk;var p=UiKit.Frame(main,id+"Card",x,432,480,360,UiKit.Panel,selected?UiKit.Gold:risk?UiKit.Red:UiKit.Paper);
            if(risk)
            {
                UiKit.Box(p,"RiskHeader",12,10,456,10,UiKit.Red);
                for(int i=0;i<17;i++)foreach(float edge in new[]{6f,464f})
                {
                    var stripe=UiKit.Box(p,"RiskStripe"+i+"_"+edge,edge,28+i*18,10,2,UiKit.Red);
                    stripe.localRotation=Quaternion.Euler(0,0,-45);
                }
            }
            UiKit.Text(p,"Title",title,24,28,432,58,34,risk?UiKit.Red:UiKit.Paper);
            UiKit.Text(p,"Goal","目标分  "+CompactScore(score),24,116,432,100,29,UiKit.Paper);
            UiKit.Text(p,"Reward","基础奖励  "+reward+" 金币",24,218,432,48,25,UiKit.Gold);
            UiKit.Button(p,id,selected?"✓ 已选择":"选择挑战",24,284,432,60,()=>ChoosePreview07(risk),true,selected);
        }
        private void DrawControllers07()
        {
            bool c=Compact07;float x=c?360:368,y=c?18:24;
            int count=ViewState.controllers.Count;bool mini=count>5||c;int shown=count>10?9:count;
            float cw=c?234:mini?208:160,ch=c?117:mini?104:224,gap=c?12:24,rowGap=c?18:16;
            for(int i=0;i<Math.Max(4,shown);i++)
            {
                float px=x+(i%5)*(cw+gap),py=y+(mini?i/5*(ch+rowGap):0);
                if(i>=shown){UiKit.Outline(main,"ControllerEmpty"+i,px,py,cw,ch,UiKit.PanelLight);continue;}
                var item=ViewState.controllers[i];DrawController07(main,item,px,py,cw,ch,mini,false);
            }
            if(count>10)
            {
                var all=UiKit.Button(main,"AllControllerStates","全部 "+count+" 件 >",x+4*(cw+gap),y+ch+rowGap,cw,ch,ShowControllerOrder);
                var label=all.GetComponentInChildren<TextMeshProUGUI>();label.fontSize=22*UiKit.FontScale;label.textWrappingMode=TextWrappingModes.NoWrap;
            }
        }
        private void DrawController07(Transform parent,ControllerInstance item,float x,float y,float w,float h,bool compact,bool selling)
        {
            var b=UiKit.Button(parent,"Controller_"+item.instanceId,"",x,y,w,h,()=>ShowController(item,selling),!IsBusy);
            b.targetGraphic.color=UiKit.Paper;UiKit.CardSurface(b,compact);
            int ordinal=ViewState.controllers.IndexOf(item)+1;
            if(!compact)DrawControllerFullBody07(b.transform,item.definitionId,CardState07(item),item.enchantmentId,ordinal);
            else
            {
                bool tall=h>=112;float icon=tall?64:48,tx=tall?84:68;
                DrawControllerRarity07(b.transform,item.definitionId,true,w,h);
                EnchantBadge(b.transform,item.enchantmentId,w-40,2,28);
                UiKit.Text(b.transform,"Ordinal",ordinal.ToString(),w-76,2,28,30,12,UiKit.Ink,TextAlignmentOptions.Center);
                Icon(b.transform,item.definitionId,12,38,icon);
                var name=UiKit.Text(b.transform,"Name",NameOf(item.definitionId),tx,28,w-tx-12,tall?42:36,tall?18:16,UiKit.Ink,TextAlignmentOptions.Center);
                name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;
                string shortState=CardState07(item);if(shortState=="点击查看条件")shortState="查看条件";
                var state=UiKit.Text(b.transform,"State",shortState.Replace("→ ","→"),tx,tall?68:58,w-tx-12,tall?34:32,14,UiKit.Ink,TextAlignmentOptions.Center);
                state.textWrappingMode=TextWrappingModes.NoWrap;state.overflowMode=TextOverflowModes.Ellipsis;
            }
            AttachControllerDrag(b,item);
            Art07AttachTooltip(b,item.instanceId,NameOf(item.definitionId),Description(item.definitionId),EnchantShort(item.enchantmentId)+" · "+CardState07(item),()=>ShowController(item,selling));
        }
        private string ShortSkill07(string id)
        {
            string s=Description(id);int pos=s.IndexOf('。');if(pos>=0)s=s.Substring(0,pos);return s.Length>16?s.Substring(0,16)+"…":s;
        }
        private string CardState07(ControllerInstance item)
        {
            var source=ViewSources.FirstOrDefault(s=>s.emitterId==item.instanceId);
            if(item.definitionId=="C41")return source?.abilityId==null?"右邻失效":"→ "+NameOf(source.abilityId);
            if(source?.abilityId=="C45")return source.state.activeColor=="red"?"当前红":"当前蓝";
            if(source?.abilityId=="C51")return "当前 ×"+(source.state.turnoverUnits2/2m).ToString("0.#");
            if(source?.abilityId=="C53")return "随机倍率";
            if(item.definitionId=="C52")return "售价 "+ControllerRules.SalePrice(item,Config);
            if(source?.abilityId=="C05")return ViewState.hand?.c05UsedEmitterIds.Contains(item.instanceId)==true?"本手已用":"本手可用";
            return "点击查看条件";
        }
        private void DrawItems07()
        {
            bool c=Compact07;float x=c?1602:1536,y=c?18:24,size=c?84:88,gap=c?12:20;
            UiKit.Text(main,"ItemsTitle","局内道具",x,y,300,30,18,UiKit.Muted);
            for(int i=0;i<Math.Min(5,ViewState.consumableSlots);i++)
            {
                float px=x+(i%3)*(size+gap),py=y+36+i/3*(size+12);
                if(i>=ViewState.inventory.Count){UiKit.Outline(main,"ItemEmpty"+i,px,py,size,size,UiKit.PanelLight);continue;}
                var item=ViewState.inventory[i];var b=UiKit.Button(main,"Item_"+item.instanceId,"",px,py,size,size,()=>ShowOwned(item,false),!IsBusy);Icon(b.transform,item.contentId,8,6,size-16);
                Art07AttachTooltip(b,item.instanceId,NameOf(item.contentId),Description(item.contentId),Art07ItemUnavailable(item)??"可使用 · 点击查看完整说明",()=>ShowOwned(item,false));
                if(Art07ItemUnavailable(item)!=null)
                {
                    var mark=UiKit.Frame(b.transform,"UnavailableLock",size-30,2,28,30,UiKit.Background,UiKit.PanelLight);
                    var symbol=UiKit.Box(mark,"Lock",3,3,22,24,Color.white).GetComponent<UnityEngine.UI.Image>();symbol.sprite=UiKit.Template("ui_icon_lock");symbol.raycastTarget=false;
                }
            }
        }
        private void Gameplay()
        {
            DrawControllers07();DrawItems07();ScorePanel();
            var s=ViewState;var h=s.hand;bool c=Compact07,pending=Session.PendingIds.Length>0,busy=IsBusy;
            float poolX=c?360:368,poolY=c?408:472,poolW=c?1542:1528;
            UiKit.Text(main,"PoolTitle","骰池",poolX+8,poolY,72,36,c?18:22,UiKit.Muted);
            var dice=s.dice.Where(d=>!h.orderedLockedIds.Contains(d.dieId)).OrderBy(d=>d.createdSerial).ToList();poolPage=Math.Min(poolPage,Math.Max(0,(dice.Count-1)/10));
            int shown=Math.Min(10,dice.Count-poolPage*10);const float cellWidth=280;
            for(int j=0;j<shown;j++)
            {
                int row=j/5,rowCount=Math.Min(5,shown-row*5);
                float start=poolX+(poolW-rowCount*cellWidth)/2;
                // Keep the 104/118-width ceramic bodies. The main-camera
                // projection is taller than the cube's world-space scale.
                float cy=poolY+(c?84:shown>5?68:132)+row*(c?96:136);
                var d=dice[j+poolPage*10];
                DrawDie(d,h.currentResults.FirstOrDefault(r=>r.dieId==d.dieId),new Vector2(start+j%5*cellWidth+90,cy),false,j,c?.72f:shown>5?1.04f:1.18f,true);
            }
            if(dice.Count>10)
            {
                float pageSize=Mathf.Max(60,40/Mathf.Max(.1f,canvas.scaleFactor));
                UiKit.Button(main,"PoolPage",">",poolX+4,poolY+56,pageSize,pageSize,()=>{poolPage=(poolPage+1)%((dice.Count+9)/10);Refresh();});
                UiKit.Text(main,"PoolPageIndex",(poolPage+1)+" / "+((dice.Count+9)/10),poolX+4,poolY+60+pageSize,64,40,18,UiKit.Muted,TextAlignmentOptions.Center);
            }
            float sy=c?657:756,slotW=c?228:152,slotH=c?102:124,sgap=c?18:24;
            UiKit.Text(main,"SlotsTitle",$"有序提交  {h.orderedLockedIds.Count} / {s.stage.capacity}",poolX+16,sy,740,c?36:32,c?18:22,UiKit.Paper);
            UiKit.Text(main,"LockRule","已锁定 · 本手不可移回骰池",poolX+740,sy,poolW-756,32,c?16:20,UiKit.Gold,TextAlignmentOptions.TopRight);
            slotPage=Math.Min(slotPage,Math.Max(0,(s.stage.capacity-1)/8));int slots=Math.Min(8,s.stage.capacity-slotPage*8);int columns=c?4:slots;
            float sx=poolX+(poolW-columns*(slotW+sgap)+sgap)/2;
            for(int j=0;j<slots;j++)
            {
                int i=j+slotPage*8;float x=sx+j%columns*(slotW+sgap),y=sy+(c?36:36)+j/columns*(slotH+18);
                bool locked=i<h.orderedLockedIds.Count;UiKit.Outline(main,"Slot"+i,x,y,slotW,slotH,locked?UiKit.Gold:UiKit.PanelLight,3);
                UiKit.Text(main,"SlotIndex"+i,(i+1).ToString("00"),x+(locked?(c?126:94):8),y+4,locked?(c?50:32):38,32,16,UiKit.Muted);
                if(locked){var id=h.orderedLockedIds[i];DrawLockMark07(x+(c?198:128),y+10,i,id);lockedSlotRects07[id]=new Rect(x,y,slotW,slotH);DrawDie(s.dice.First(d=>d.dieId==id),h.currentResults.First(r=>r.dieId==id),new Vector2(x+(c?82:44),y+(c?52:62)),true,i,c?.65f:.82f,true);}
                else UiKit.Text(main,"EmptySlot"+i,"空栏",x+30,y+38,slotW-60,48,20,UiKit.Muted,TextAlignmentOptions.Center);
            }
            if(s.stage.capacity>8)
            {
                float pageSize=Mathf.Max(60,40/Mathf.Max(.1f,canvas.scaleFactor));
                UiKit.Button(main,"SlotPage",">",poolX-8,sy+56,pageSize,pageSize,()=>{slotPage=(slotPage+1)%((s.stage.capacity+7)/8);Refresh();});
                UiKit.Text(main,"SlotPageIndex",(slotPage+1)+" / "+((s.stage.capacity+7)/8),poolX-8,sy+60+pageSize,64,40,18,UiKit.Muted,TextAlignmentOptions.Center);
            }
            float ax=c?360:368,ay=c?933:956,ah=c?72:72;
            float[] widths=c?new[]{264f,324,216,324,342}:new[]{250f,310,210,310,350};float gap=c?18:18;
            var rollButton=UiKit.ActionButton(main,"Roll",(h.hasRolled?"重掷 ":"掷骰 ")+h.rollsRemaining+" [R]",ax,ay,widths[0],ah,()=>Send(new SessionCommand{kind=CommandKind.Roll}),UiKit.Blue,!busy&&!pending&&h.rollsRemaining>0&&dice.Count>0);BindRollButtonFeedback07(rollButton);ax+=widths[0]+gap;
            int lockRoom=Math.Max(0,s.stage.capacity-h.orderedLockedIds.Count);bool excess=Session.PendingIds.Length>lockRoom;
            UiKit.ActionButton(main,"Confirm",excess?"可再锁 "+lockRoom+" 颗":"确认锁定 "+Session.PendingIds.Length,ax,ay,widths[1],ah,()=>Send(new SessionCommand{kind=CommandKind.ConfirmLocks}),UiKit.Paper,!busy&&pending&&!excess);ax+=widths[1]+gap;
            UiKit.Button(main,"Clear","清除待选",ax,ay,widths[2],ah,()=>{Session.ClearPending();Refresh();},!busy&&pending);ax+=widths[2]+gap;
            UiKit.ActionButton(main,"Submit","提交 "+h.orderedLockedIds.Count+" 颗",ax,ay,widths[3],ah,()=>Send(new SessionCommand{kind=CommandKind.Submit}),UiKit.Teal,!busy&&!pending&&h.hasRolled&&h.orderedLockedIds.Count>0&&s.stage.remainingSubmissions>0);ax+=widths[3]+gap;
            var endStage=UiKit.Button(main,busy?"Skip":"EndStage",busy?"显示最终结果":"结束本轮",ax,ay,widths[4],ah,busy?(Action)SkipVisual:()=>Send(new SessionCommand{kind=CommandKind.EndStage}),busy||Session.CanEndStage);
            if(!busy&&s.stage.qualified)UiKit.Outline(endStage.transform,"QualifiedGoldBorder",0,0,widths[4],ah,UiKit.Gold,3);
            string reason=excess?"可再锁 "+lockRoom+" 颗；请取消超额待选，不会部分锁定":pending?"先确认锁定，或清除待选":!h.hasRolled?"先投掷，再选择骰子":dice.Count==0?"没有未锁骰；可提交已锁骰":h.rollsRemaining==0?"投掷已用尽；仍可锁定、排序与提交":h.orderedLockedIds.Count==s.stage.capacity?"提交栏已满；仅重掷未锁骰":"锁定不可撤销 · 提交将消耗 1 次提交";
            if(!busy&&!pending&&!string.IsNullOrEmpty(LastTransactionSummary07))reason=LastTransactionSummary07;
            UiKit.Text(main,"ActionReason",reason,c?378:384,c?1012:1034,1480,42,20,UiKit.Muted);
        }
        private void DrawDie(DieInstance die,DieHandState result,Vector2 center,bool locked,int index,float scale=1f,bool sideLabels=false)
        {
            bool selected=Session.PendingIds.Contains(die.dieId),c=Compact07;float w=locked?(c?180:sideLabels?112:144):(c?180:sideLabels?126:180),height=c?90:112;
            table.Display(die,result,center,scale,selected);dieCenters[die.dieId]=center;
            var hit=UiKit.Box(main,"Die_"+die.dieId,center.x-w/2,center.y-height/2,w,height,Color.clear,true);
            var pointer=UiKit.Component<DiePointer>(hit.gameObject);pointer.clicked=null;pointer.dragStart=null;pointer.dragged=null;pointer.dragEnd=null;string id=die.dieId;
            UiKit.Component<Art07DieHover>(hit.gameObject).Bind(table,id,!locked&&!selected&&!modal&&!IsBusy&&ViewState.phase=="Selecting");
            pointer.clicked=e=>{if(modal||IsBusy||draggedDie07!=null)return;if(e.button==PointerEventData.InputButton.Right||locked){ShowDie(id);return;}Session.TogglePending(id);Refresh();};
            if(locked)
            {
                pointer.dragStart=e=>{if(e.button!=PointerEventData.InputButton.Left||!CanMoveLockedDice07)return;draggedDie07=id;e.eligibleForClick=false;};
                pointer.dragged=e=>
                {
                    if(draggedDie07!=id)return;var pos=ScreenToDesign(e.position);
                    table.DisplayDragged(die,result,pos,scale*1.06f);
                    UiKit.Outline(main,"DragDieOrigin07",center.x-w/2,center.y-height/2,w,height,UiKit.Muted,2);
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
                DrawLockedGrip07(hit,id,c);
            }
            else visibleDice.Add(id);
            if(selected)
            {
                float edgeHeight=sideLabels?(c?96:scale>1.1f?154:132):(c?92:144),edgeWidth=sideLabels?(c?88:scale>1.1f?132:124):w+16;
                for(int i=0;i<6;i++){float y=center.y-(sideLabels?6:0)-edgeHeight/2+i*edgeHeight/6;UiKit.Box(main,"PendingLeft"+id+i,center.x-edgeWidth/2,y,3,edgeHeight/12,UiKit.Gold);UiKit.Box(main,"PendingRight"+id+i,center.x+edgeWidth/2,y,3,edgeHeight/12,UiKit.Gold);}
                string order=(Array.IndexOf(Session.PendingIds,id)+1).ToString("00");
                if(Array.IndexOf(Session.PendingIds,id)>=Math.Max(0,ViewState.stage.capacity-ViewState.hand.orderedLockedIds.Count))
                {
                    var corner=UiKit.Rect(main,"ExcessPending"+id,center.x-edgeWidth/2-12,center.y-edgeHeight/2-8,26,26);
                    for(int i=0;i<4;i++){var stripe=UiKit.Box(corner,"Stripe"+i,2+i*6,6,2,18,UiKit.Gold);stripe.localRotation=Quaternion.Euler(0,0,-40);}
                }
                if(!sideLabels){DrawPendingCheck07(id,center.x-w/2-28,center.y+(c?36:72),36,c);UiKit.Text(main,"Pending"+id,order+" 待锁",center.x-w/2,center.y+(c?36:72),w/2,36,c?13:16,UiKit.Gold,TextAlignmentOptions.Center);}
            }
            string label=result?.hasRolled==true?(colorAssist07?(result.currentColor=="red"?"● 红":"◇ 蓝"):""):"待投掷";
            string properties=(die.isGlass?"玻璃":"")+(die.lifetime=="stage_temporary"?(die.isGlass?" 临时":"临时"):"");
            if(sideLabels)
            {
                if(properties.Length>0)label+=(label.Length>0?"\n":"")+(locked&&!c?properties.Replace(" ","\n"):properties);
                float stateFont=locked?14:c?14:17,pendingFont=c?13:16;
                float lineRatio=UiKit.Font!=null&&UiKit.Font.faceInfo.pointSize>0?UiKit.Font.faceInfo.lineHeight/UiKit.Font.faceInfo.pointSize:1.448f;
                int lines=label.Split('\n').Length;float lineSpacing=locked&&!c?-20:0;
                float stateHeight=lines*stateFont*UiKit.FontScale*lineRatio+(lines-1)*lineSpacing*stateFont*UiKit.FontScale*.01f+1;
                float pendingHeight=selected?pendingFont*UiKit.FontScale*lineRatio+1:0;
                float labelWidth=locked?(c?94:56):128;
                float x=center.x+(locked?(c?44:50):c?52:72),y=locked?center.y-(c?15:26):center.y-(stateHeight+pendingHeight)/2;
                if(selected)
                {
                    string order=(Array.IndexOf(Session.PendingIds,id)+1).ToString("00");
                    DrawPendingCheck07(id,x,y,pendingHeight,c);
                    UiKit.Text(main,"Pending"+id,order+" 待锁",x+28,y,labelWidth-28,pendingHeight,pendingFont,UiKit.Gold);
                }
                var state=UiKit.Text(main,"DieState"+id,label,x,y+pendingHeight,labelWidth,stateHeight,stateFont,result?.currentColor=="red"?UiKit.Red:UiKit.Blue);
                UiKit.Component<Art07PoolLabelFollower>(state.gameObject).Bind(table,id,center,!locked&&!selected&&LockVisualPlaying07);
                state.lineSpacing=lineSpacing;state.textWrappingMode=TextWrappingModes.NoWrap;return;
            }
            if(properties.Length>0)label+=" "+properties;
            var dieLabel=UiKit.Text(main,"DieState"+id,label,selected?center.x:center.x-w/2,center.y+(locked?(c?28:34):c?36:72),selected?w/2:w,36,locked?14:c?14:17,result?.currentColor=="red"?UiKit.Red:UiKit.Blue,TextAlignmentOptions.Center);
            dieLabel.lineSpacing=0;dieLabel.textWrappingMode=TextWrappingModes.NoWrap;dieLabel.overflowMode=TextOverflowModes.Ellipsis;
        }
        private bool TryDieInsertion07(Vector2 position,out int destination,out Rect edge)
        {
            destination=-1;edge=default;if(draggedDie07==null||ViewState.hand==null)return false;
            foreach(var slot in lockedSlotRects07)
            {
                if(!slot.Value.Contains(position))continue;
                var ids=ViewState.hand.orderedLockedIds;int from=ids.IndexOf(draggedDie07),at=ids.IndexOf(slot.Key);if(from<0||at<0)return false;
                bool after=position.x>=slot.Value.center.x-.1f;int gap=at+(after?1:0);
                destination=Mathf.Clamp(gap-(from<gap?1:0),0,ids.Count-1);
                edge=new Rect(after?slot.Value.xMax+5:slot.Value.xMin-9,slot.Value.yMin,4,slot.Value.height);return true;
            }
            return false;
        }
        private void CancelDieDrag07(bool redraw=true)
        {
            bool active=draggedDie07!=null;draggedDie07=null;if(dieInsertion07!=null)dieInsertion07.gameObject.SetActive(false);
            var origin=main?.Find("DragDieOrigin07");if(origin!=null)origin.gameObject.SetActive(false);
            if(active&&redraw){float duration=table.LayoutDuration;table.LayoutDuration=.12f;try{Refresh();}finally{table.LayoutDuration=duration;}}
        }
        private void DrawLockMark07(float x,float y,int index,string dieId)
        {
            var mark=UiKit.Rect(main,"LockMark"+index,x,y,22,24);
            UiKit.Outline(mark,"Shackle",5,0,12,12,UiKit.Gold,2);UiKit.Box(mark,"Body",1,9,20,14,UiKit.Gold);UiKit.Box(mark,"Keyhole",10,13,2,6,UiKit.Ink);BindLockArrival07(mark,dieId);
        }
        private void ScorePanel()
        {
            bool c=Compact07;float x=c?360:368,y=c?282:288,w=c?1542:1528,h=c?122:160;
            var p=UiKit.Frame(main,"ScorePanel",x,y,w,h,UiKit.Background,UiKit.PanelLight);
            if(IsPresenting07){DrawReceiptScore07(p,w,h);return;}
            if(DrawStablePresentationScore07(p,w,h))return;
            if(DrawRecoveredScore07(p,w,h))return;
            var preview=DisplayScorePreview07();var b=preview?.minimum;
            if(b==null){UiKit.Text(p,"Empty","—  ·  "+(ViewState.hand.hasRolled?"锁定骰子后计算":"投掷后选择骰子"),20,20,w-40,h-40,28,UiKit.Muted,TextAlignmentOptions.Center);return;}
            bool randomSource=b.multiplierFactors.Any(f=>f.abilityId=="C53");
            string primary=string.IsNullOrEmpty(b.primaryHandId)?"散点":NameOf(b.primaryHandId);
            string chosen=string.IsNullOrEmpty(b.chosenGroupId)?(b.straightMult100>0?"顺子":"散点"):NameOf(b.chosenGroupId)+(b.straightMult100>0?"＋顺子":"");
            string color=string.IsNullOrEmpty(b.colorId)?"混色":NameOf(b.colorId);
            string caption=preview.isRandom?"随机结果范围":randomSource?"预计得分 · 调试覆盖":"预计得分";
            UiKit.Text(p,"Hand","结构 "+primary+"／计分 "+chosen+"\n"+caption+" · "+color,20,4,randomSource?530:w-650,c?56:68,c?14:UiKit.FontScale>1?18:20,UiKit.Paper);
            var finalScore=UiKit.Text(p,"FinalScore",preview.isRandom?CompactScore(b.score)+" — "+CompactScore(preview.maximum.score):CompactScore(b.score),w-610,0,580,c?58:76,c?24:40,UiKit.Paper,TextAlignmentOptions.MidlineRight);
            finalScore.textWrappingMode=TextWrappingModes.NoWrap;finalScore.lineSpacing=0;
            if(randomSource)
            {
                int owners=b.multiplierFactors.Where(f=>f.abilityId=="C53").Select(f=>f.stateOwnerId).Distinct().Count();
                string label=owners>1?"查看随机来源":preview.isRandom?"查看 9 种结果":"查看覆盖来源";
                UiKit.Button(p,"RandomPreview",label,564,0,300,60,ShowRandomPreview07,!LockVisualPlaying07);
            }
            bool zeroFive=b.multiplierFactors.Any(f=>f.abilityId=="C49"&&f.numerator=="0");
            string control=zeroFive?"控制器 ×0\n没有有效 5 点":"控制器 ×"+ScoreFactorShort07(b.controllerMultiplierNumerator,b.controllerMultiplierDenominator);
            string[] labels={"基础 "+(b.baseTotal.Length>9?ScoreFactorShort07(b.baseTotal):CompactScore(b.baseTotal)),"牌型 ×"+Factor(b.totalHandMult100),"颜色 ×"+Factor(b.colorMult100),"红组 ×"+Factor(b.redMult100),control,"附魔 ×2^"+b.enchantMultiplierCount};
            float bw=(w-48)/6;for(int i=0;i<6;i++)
            {
                float factorHeight=c?60:64;
                var button=UiKit.Button(p,"Factor"+i,labels[i],16+i*bw,c?62:80,bw-8,factorHeight,()=>ShowBreakdown(DisplayScoreSnapshot07(),b),!LockVisualPlaying07);
                var text=button.GetComponentInChildren<TextMeshProUGUI>();text.fontSize=(c?15:UiKit.FontScale>1?16:20)*UiKit.FontScale;
                text.rectTransform.anchoredPosition=new Vector2(8,-2);text.rectTransform.sizeDelta=new Vector2(bw-24,factorHeight-4);
                text.textWrappingMode=TextWrappingModes.NoWrap;text.lineSpacing=i==4&&zeroFive?-20:0;
            }
        }
        // HUD-only approximation. The button retains the original exact breakdown.
        private static string ScoreFactorShort07(string numerator,string denominator="1")
        {
            string exact=denominator=="1"?numerator:numerator+"/"+denominator;
            if(exact.Length<=9)return exact;
            BigInteger n=ExactScoreMath.Parse(numerator),d=ExactScoreMath.Parse(denominator);if(n.IsZero)return "0";
            int exponent=n.ToString().Length-d.ToString().Length;
            if(exponent>=0){if(n<d*BigInteger.Pow(10,exponent))exponent--;}
            else if(n*BigInteger.Pow(10,-exponent)<d)exponent--;
            int shift=2-exponent;BigInteger scaledN=n,scaledD=d;
            if(shift>=0)scaledN*=BigInteger.Pow(10,shift);else scaledD*=BigInteger.Pow(10,-shift);
            BigInteger units=(scaledN*2+scaledD)/(scaledD*2);
            if(units>=1000){units/=10;exponent++;}
            int mantissa=(int)units;string decimals=(mantissa%100).ToString("00").TrimEnd('0');
            return "≈"+(mantissa/100)+(decimals.Length==0?"":"."+decimals)+"e"+exponent;
        }
        private void ShowRandomPreview07()
        {
            var preview=Session.PreviewScoreRange();var snapshot=Session.PreviewSnapshot();
            if(preview==null||snapshot==null)return;
            var factors=preview.minimum.multiplierFactors.Where(f=>f.abilityId=="C53").ToArray();
            string[] owners=factors.Select(f=>f.stateOwnerId).Distinct(StringComparer.Ordinal).ToArray();
            var p=Art07Modal("随机结果 · 只读分支",496,400,1280,520);
            var list=UiKit.Scroll(p,"RandomBranches",24,96,1232,336,336);float y=0;
            string range=preview.isRandom?"随机结果范围 "+Big(preview.minimum.score)+" — "+Big(preview.maximum.score):"仅下一次调试覆盖 · 预计 "+Big(preview.minimum.score);
            Art07DetailSection(list,"RandomRange","总分范围",range,ref y,1200);
            foreach(string owner in owners)
            {
                var emitters=factors.Where(f=>f.stateOwnerId==owner).Select(f=>f.emitterId).ToArray();
                string sharing=emitters.Length>1?"共用 q · ":"独立 q · ";
                var forced=ViewState.isDebug&&ViewState.debugWorkspace?ViewState.randomOverrides.FirstOrDefault(o=>o.kind=="C53"&&o.sourceId==owner):null;
                Art07DetailSection(list,"RandomOwner"+owner,sharing+owner,"使用此 q："+string.Join("、",emitters)+(forced==null?"\n正式提交后才确定结果。":"\n仅下一次覆盖：×"+(int.Parse(forced.value)/2m).ToString("0.#")),ref y,1200);
            }
            if(owners.Length==1&&preview.isRandom)
            {
                string owner=owners[0];int index=0;
                foreach(int q in Config.Controller("C53").parameters.factorUnits2)
                {
                    var branch=snapshot.Clone();branch.resolvedRandomChoices=new[]{new RandomMultiplierChoice{ownerInstanceId=owner,units2=q}};
                    var evaluated=ScoreCalculator.Preview(branch,Config);var exact=evaluated.minimum;
                    float rowHeight=Math.Max(80,80*UiKit.FontScale);
                    UiKit.Text(list,"RandomFactor"+index,"q ×"+(q/2m).ToString("0.#"),0,y,240,rowHeight-8,26,UiKit.Paper);
                    var score=UiKit.Text(list,"RandomScore"+index,exact.score,256,y,656,rowHeight-8,26,UiKit.Gold);score.overflowMode=TextOverflowModes.Ellipsis;
                    UiKit.Button(list,"RandomBranch"+index,"查看分项",944,y,240,64,()=>ShowBreakdown(branch,exact));
                    y+=rowHeight;index++;
                }
            }
            else if(owners.Length>1)Art07DetailSection(list,"RandomMultiple","多个真实 C53","仅列各来源与总范围，不展开组合列表。每个真实 C53 独立确定 q，模仿者与目标共用 q。",ref y,1200);
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            UiKit.Text(p,"RandomReadOnly","浏览不会抽样、消耗覆盖或改变当前提交。",24,448,1232,60,24,UiKit.Muted);
        }
        private void Settlement()
        { DrawSettlement07(); }
        private void RunEnd()
        {DrawRunResult07();}
        private void LegacyArt07RunEnd()
        {
            bool won=ViewState.phase=="RunWon";var p=UiKit.Frame(main,"RunResult",472,152,976,776,UiKit.Paper,UiKit.Ink);
            UiKit.Text(p,"EndTitle",won?"24 关完成":"本次挑战结束",40,44,896,120,58,UiKit.Ink);
            UiKit.Text(p,"EndMessage",won?"这一桌，属于你的构筑。":ViewState.failureReason=="NoDice"?"活动骰已耗尽。":"提交次数已用尽，未达到本关目标。",40,224,896,120,28,UiKit.Ink);
            UiKit.Text(p,"EndStats",$"种子 {ViewState.seed}\n第 {ViewState.stage.stageIndex+1} 关 · 累计 {CompactScore(ViewState.stage.score)}\n{(ViewState.isDebug?"调试对局 · 不进入普通成绩":"普通对局")}",40,380,896,160,27,UiKit.Ink);
            UiKit.ActionButton(p,"Again","再开一局",40,650,280,66,ShowNewRun07,UiKit.Blue);
            UiKit.ActionButton(p,"EndBreakdown","查看构筑",348,650,280,66,ShowControllerOrder,UiKit.Paper);
            UiKit.ActionButton(p,"ReturnMenu","返回主菜单",656,650,280,66,ReturnMenu,UiKit.Paper);
        }
    }
}
