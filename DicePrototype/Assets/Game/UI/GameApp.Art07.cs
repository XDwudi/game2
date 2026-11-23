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
        public const string ArtVersion="art0.7.3";
        private int textScale07=100;
        private bool colorAssist07=true, instant07, shake07=true, grain07=true;
        private string draggedDie07;
        private RectTransform dieInsertion07;
        private readonly System.Collections.Generic.Dictionary<string,Rect> lockedSlotRects07=new System.Collections.Generic.Dictionary<string,Rect>();
        private bool Compact07 => Screen.safeArea.width<=1366 && textScale07>=125;
        private bool Spacious071 => !Compact07&&UiKit.FontScale<=1&&ViewState!=null&&ViewState.controllers.Count<=5&&!ViewSources.Any(s=>s.abilityId=="C53"||s.abilityId=="C49");
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
            if(ViewState.phase=="RunWon"||ViewState.phase=="RunFailed")return;
            float x=Compact07?C07(24):40,w=Compact07?C07(192):288;
            if(FramePhase11)
            {
                // V1.1 (W11): handbook and pause sit at the right end of the top bar; the previous-hand button is on the
                // submission tray (DrawSubmit11); the debug workspace button joins the top bar only in that workspace.
                var l=L11;float right=l.top.xMax-8,pauseX=right-108,handX=pauseX-12-108,y=l.top.y+6;
                UiKit.Button(main,"Handbook","手册",handX,y,108,60,ShowHandbook,!IsBusy&&!Session.HasPendingSave);
                UiKit.Button(main,"Pause","暂停",pauseX,y,108,60,Pause);
                if(DebugUnlocked&&IsDebugWorkspace&&!CleanCapture11)
                {
                    var button=UiKit.Button(main,"DebugWorkspace","组合试验",handX-12-148,y,148,60,ShowDebugPanel,!IsBusy);
                    var face=(UnityEngine.UI.Image)button.targetGraphic;
                    var frame=FrameInsets11(face);
                    float left=Mathf.Max(8,frame.x+2),rightPad=Mathf.Max(8,frame.z+2);
                    var label=button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                    FitLine13(button.transform,"Label",label.text,left,0,face.rectTransform.rect.width-left-rightPad,23,label.color,TextAlignmentOptions.Center,false);
                    CenterButtonLabel16(button);
                }
                return;
            }
            // V1.1 W14 (SHOP_LAYOUT 3): the shop draws settings and handbook in its own top bar (Shop14), before the
            // face-pick veil.
            if(ViewState.phase=="Shop")return;
            UiKit.Button(main,"Pause","设置 / 暂停",x,Compact07?C07(650):944,w,60,Pause);
            UiKit.Button(main,"Handbook","牌型手册 [H]",x,Compact07?C07(600):870,w,60,ShowHandbook,!IsBusy&&!Session.HasPendingSave);
            if(ViewState.lastSubmission!=null)UiKit.Button(main,"PreviousSubmission","上一手得分明细",x,Compact07?C07(550):802,w,60,()=>ShowBreakdown(ViewState.lastSubmission.snapshot,ViewState.lastSubmission.breakdown),!IsBusy);
            if(DebugUnlocked&&IsDebugWorkspace)UiKit.Button(main,"DebugWorkspace","组合试验",x,Compact07?C07(500):730,w,60,ShowDebugPanel,!IsBusy);
        }
        private void Header()
        {
            if(FramePhase11)TopBar11();
            else if(ViewState.phase=="Shop")ShopTopBar14();
        }
        private void Menu()=>Menu071();
        private void ShowNewRun07()
        {
            OpenNewRunPanel07();
        }
        private void ShowMenuHandbook07()
        {
            ShowHandbook();
        }
        private void ConfirmQuit07()
        {
            var p=Art07Modal("退出游戏",560,330,800,420,"quit16");
            UiKit.Text(p,"QuitHint","进度已自动保存。",24,112,752,96,26,LabelOn11(p),TextAlignmentOptions.Center);
            UiKit.Button(p,"ConfirmQuit","退出",200,300,400,72,()=>Application.Quit());
        }
        private void StagePreview()=>StagePreview10();
        private void DrawControllers07()
        {
            bool c=Compact07;float x=c?360:368,y=c?18:24;
            int count=ViewState.controllers.Count;bool mini=count>5||c;int shown=count>10?9:count;
            float cw=c?234:mini?208:Spacious071&&count<=4?228:198,ch=c?117:mini?104:Spacious071&&count<=4?304:264,gap=c?12:24,rowGap=c?18:16;
            // p0.9.0: empty places follow the run's ordinary slots (5 to 7); the single large row holds at most five.
            int places=Math.Max(shown,Math.Min(mini?10:5,ViewState.controllerSlots));
            for(int i=0;i<places;i++)
            {
                float px=x+(i%5)*(cw+gap),py=y+(mini?i/5*(ch+rowGap):0);
                if(i>=shown){var empty=UiKit.Frame(main,"ControllerEmpty"+i,px,py,cw,ch,Color.Lerp(UiKit.Felt,UiKit.Background,.2f),UiKit.Panel);UiKit.Surface(empty,"ui_slot_inventory");UiKit.Text(empty,"Capacity","·  ·  ·",8,ch/2-20,cw-16,40,22,UiKit.PanelLight,TextAlignmentOptions.Center);continue;}
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
            if(AltarVictims09().Contains(item.instanceId))return "将被祭坛摧毁";
            if(DisabledNow10(item.instanceId))return "本关失效";
            if(DisabledNext10(item.instanceId))return "首领关将失效";
            if(item.definitionId=="C41")return source?.abilityId==null?"右邻失效":"→ "+NameOf(source.abilityId);
            if(source?.abilityId=="C45")return source.state.activeColor=="red"?"当前红":"当前蓝";
            if(source?.abilityId=="C51")return "当前 ×"+PlayerNumberFormat.Ratio(source.state.turnoverUnits2,2);
            if(source?.abilityId=="C53")return "随机倍率";
            string growth=GrowthText09(source?.abilityId,source?.state);
            if(growth!=null)return "当前 "+growth;
            if(item.definitionId=="C52")return "售价 "+OwnedSalePrice10(item);
            if(source?.abilityId=="C05")return ViewState.hand?.c05UsedEmitterIds.Contains(item.instanceId)==true?"本手已用":"本手可用";
            return "点击查看条件";
        }
        // V1.1 (W11): the play screen is GameApp.Play11.cs (docs/specs/v11/PLAY_LAYOUT.md). The v0.73 layout below is kept
        // unreferenced for comparison until the new look reaches every screen (W14-W16).
        private void Gameplay()=>Gameplay11();
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
            var padlock=UiKit.Template("ui_tag_lock");
            if(padlock!=null)
            {
                // v0.73: brass padlock art in the same 22 x 24 footprint as the v0.7 block mark.
                var art=UiKit.Box(mark,"Padlock",-3,-4,28,30,Color.white).GetComponent<UnityEngine.UI.Image>();art.sprite=padlock;art.raycastTarget=false;art.preserveAspect=true;
            }
            else{UiKit.Outline(mark,"Shackle",5,0,12,12,UiKit.Gold,2);UiKit.Box(mark,"Body",1,9,20,14,UiKit.Gold);UiKit.Box(mark,"Keyhole",10,13,2,6,UiKit.Ink);}
            BindLockArrival07(mark,dieId);
        }
        private void ScorePanel07Legacy()
        {
            // Reserve the gap for 1.03x triggered cards as well as rotating Combo chips.
            bool c=Compact07,thin=Spacious071;float x=c?360:368,y=c?282:thin?350:308,w=c?1542:1528,h=c?122:thin?104:156;
            var p=UiKit.Frame(main,"ScorePanel",x,y,w,h,UiKit.Background,UiKit.PanelLight);UiKit.Surface(p,"ui_panel_hud");
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
            string structure=primary==chosen?chosen:"结构 "+primary+"／计分 "+chosen;
            UiKit.Text(p,"Hand",thin?caption+" · "+structure+" · "+color:"结构 "+primary+"／计分 "+chosen+"\n"+caption+" · "+color,20,4,randomSource?530:w-650,c?56:thin?44:68,c?14:UiKit.FontScale>1?18:20,UiKit.Paper);
            var well=ScoreWell072(p);
            var finalScore=UiKit.Text(p,"FinalScore",preview.isRandom?CompactScore(b.score)+" — "+CompactScore(preview.maximum.score):CompactScore(b.score),w-(thin?420:620),thin?-4:0,thin?390:580,c?58:thin?100:76,c?24:thin?64:40,UiKit.Paper,TextAlignmentOptions.MidlineRight);
            finalScore.textWrappingMode=TextWrappingModes.NoWrap;finalScore.lineSpacing=0;
            FitScoreWell072(well,finalScore,w,c?29:thin?48:38,c?25:thin?40:36,thin?416:600);
            if(randomSource)
            {
                int owners=b.multiplierFactors.Where(f=>f.abilityId=="C53").Select(f=>f.stateOwnerId).Distinct().Count();
                string label=owners>1?"查看随机来源":preview.isRandom?"查看 9 种结果":"查看覆盖来源";
                UiKit.Button(p,"RandomPreview",label,564,0,300,60,ShowRandomPreview07,!LockVisualPlaying07);
            }
            bool zeroFive=b.multiplierFactors.Any(f=>f.abilityId=="C49"&&f.numerator=="0");
            // v1.1 (W09): decimals instead of fractions, the enchantment as its actual factor, "卡牌" for controllers (D2).
            string control=zeroFive?"卡牌 ×0\n没有有效 5 点":"卡牌 "+PlayerNumberFormat.Times(ExactScoreMath.Parse(b.controllerMultiplierNumerator),ExactScoreMath.Parse(b.controllerMultiplierDenominator),ChipDigits07);
            string[] labels={"基础 "+PlayerNumberFormat.Integer(ExactScoreMath.Parse(b.baseTotal),9),"牌型 ×"+Factor(b.totalHandMult100),"颜色 ×"+Factor(b.colorMult100),"红组 ×"+Factor(b.redMult100),control,"附魔 "+PlayerNumberFormat.Times(EnchantFactor07(b.enchantMultiplierCount),1,ChipDigits07)};
            float bw=(w-(thin?440:48))/6;for(int i=0;i<6;i++)
            {
                // rc.2: the shallow rail's factor hit area is 60 units (was 52) so it stays 40 physical
                // pixels high at 1280x720; the painted chip below is trimmed to stay inside the slab.
                float factorHeight=c?60:thin?60:64;
                var button=UiKit.FlatButton(p,"Factor"+i,labels[i],16+i*bw,c?62:thin?44:80,bw-8,factorHeight,()=>ShowBreakdown(DisplayScoreSnapshot07(),b),!LockVisualPlaying07);
                var text=button.GetComponentInChildren<TextMeshProUGUI>();text.fontSize=(c?15:UiKit.FontScale>1?16:20)*UiKit.FontScale;
                text.rectTransform.anchoredPosition=new Vector2(8,-2);text.rectTransform.sizeDelta=new Vector2(bw-24,factorHeight-4);
                text.textWrappingMode=TextWrappingModes.NoWrap;text.lineSpacing=i==4&&zeroFive?-20:0;
                bool neutral=i==1?b.totalHandMult100==100:i==2?b.colorMult100==100:i==3?b.redMult100==100:i==4?!zeroFive&&b.controllerMultiplierNumerator==b.controllerMultiplierDenominator:i==5&&b.enchantMultiplierCount==0;
                StyleFactorChip072(button,i,neutral);
                if(thin)
                {
                    // The shallow rail is 104 high: the painted chip (50..94) stays inside the slab while the
                    // hit rectangle fills 44..104.
                    var chipFace=((UnityEngine.UI.Image)button.targetGraphic).rectTransform;
                    chipFace.anchoredPosition=new Vector2(0,-6);chipFace.sizeDelta=new Vector2(bw-8,factorHeight-16);
                    text.rectTransform.anchoredPosition=new Vector2(8,0);
                }
            }
        }
        // HUD-only approximation. The button retains the original exact breakdown.
        // v1.1 (W09): decimals through PlayerNumberFormat; more than nine integer digits read "约 1.23×10^n".
        private static string ScoreFactorShort07(string numerator,string denominator="1")
            =>PlayerNumberFormat.Ratio(ExactScoreMath.Parse(numerator),ExactScoreMath.Parse(denominator),9);
        // A multiplier chip has room for "×" plus about ten digit widths at 150% text: six integer digits and two decimals.
        private const int ChipDigits07=6;
        /// <summary>The actual factor of n score-multiplier enchantments (2^n with the shipped configuration).</summary>
        private BigInteger EnchantFactor07(int count)=>BigInteger.Pow(Config.enchantRules.scoreMultiplier,Math.Max(0,count));
        private void ShowRandomPreview07()
        {
            var preview=Session.PreviewScoreRange();var snapshot=Session.PreviewSnapshot();
            if(preview==null||snapshot==null)return;
            var factors=preview.minimum.multiplierFactors.Where(f=>f.abilityId=="C53").ToArray();
            string[] owners=factors.Select(f=>f.stateOwnerId).Distinct(StringComparer.Ordinal).ToArray();
            var p=Art07Modal("错乱乘积 · 可能的结果",496,400,1280,520);
            var list=UiKit.Scroll(p,"RandomBranches",24,96,1232,336,336);float y=0;
            string range=preview.isRandom?"随机结果范围 "+CompactScore(preview.minimum.score)+" — "+CompactScore(preview.maximum.score):"仅下一次调试覆盖 · 预计 "+CompactScore(preview.minimum.score);
            Art07DetailSection(list,"RandomRange","总分范围",range,ref y,1200);
            // v1.1 (W09): cards are named by their printed position and name, never by instance id.
            var cards=snapshot.equippedControllers;
            foreach(string owner in owners)
            {
                var emitters=factors.Where(f=>f.stateOwnerId==owner).Select(f=>f.emitterId).ToArray();
                string sharing=emitters.Length>1?"，模仿它的卡用同一个结果。":"。";
                var forced=ViewState.isDebug&&ViewState.debugWorkspace?ViewState.randomOverrides.FirstOrDefault(o=>o.kind=="C53"&&o.sourceId==owner):null;
                Art07DetailSection(list,"RandomOwner"+owner,NameOf("C53")+"（"+CardPosition11(cards,owner)+"）","提交时才抽取倍率"+sharing+(forced==null?"":"\n仅下一次覆盖："+PlayerNumberFormat.Times(int.Parse(forced.value),2)),ref y,1200);
            }
            if(owners.Length==1&&preview.isRandom)
            {
                string owner=owners[0];int index=0;
                foreach(int q in Config.Controller("C53").parameters.factorUnits2)
                {
                    var branch=snapshot.Clone();branch.resolvedRandomChoices=new[]{new RandomMultiplierChoice{ownerInstanceId=owner,units2=q}};
                    var evaluated=ScoreCalculator.Preview(branch,Config);var exact=evaluated.minimum;
                    float rowHeight=Math.Max(80,80*UiKit.FontScale);
                    UiKit.Text(list,"RandomFactor"+index,PlayerNumberFormat.Times(q,2),0,y,240,rowHeight-8,26,UiKit.Paper);
                    var score=UiKit.Text(list,"RandomScore"+index,CompactScore(exact.score),256,y,656,rowHeight-8,26,UiKit.Gold);score.overflowMode=TextOverflowModes.Ellipsis;
                    UiKit.Button(list,"RandomBranch"+index,"查看明细",944,y,240,64,()=>ShowBreakdown(branch,exact));
                    y+=rowHeight;index++;
                }
            }
            else if(owners.Length>1)Art07DetailSection(list,"RandomMultiple","多张"+NameOf("C53"),"每张各抽一次倍率，这里只列出总分范围。",ref y,1200);
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            UiKit.Text(p,"RandomReadOnly","查看不会抽取结果，也不影响这次提交。",24,448,1232,60,24,UiKit.Muted);
        }
        private void Settlement()
        { DrawSettlement07(); }
        private void RunEnd()
        {DrawRunResult07();}
    }
}
