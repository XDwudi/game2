using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private RectTransform OpenModal(string title,bool dicePreview=false)
        {
            modal=true;UiKit.Clear(overlay);table.HidePreview();
            // Screen-space UI would otherwise occlude the main-camera preview aperture.
            main.gameObject.SetActive(!dicePreview);
            var scrim=new Color(.015f,.03f,.035f,.94f);
            RectTransform p;
            if(!dicePreview){UiKit.Box(overlay,"Scrim",0,0,1920,1080,scrim,true);p=UiKit.Box(overlay,"Dialog",160,145,1600,790,UiKit.Panel,true);}
            else
            {
                // Leave one aperture for a permanent-data preview rendered by the main camera.
                UiKit.Box(overlay,"ScrimTop",0,0,1920,660,scrim,true);UiKit.Box(overlay,"ScrimBottom",0,880,1920,200,scrim,true);
                UiKit.Box(overlay,"ScrimLeft",0,660,1460,220,scrim,true);UiKit.Box(overlay,"ScrimRight",1740,660,180,220,scrim,true);
                p=UiKit.Rect(overlay,"Dialog",160,145,1600,790);
                UiKit.Box(p,"PanelTop",0,0,1600,515,UiKit.Panel,true);UiKit.Box(p,"PanelBottom",0,735,1600,55,UiKit.Panel,true);
                UiKit.Box(p,"PanelLeft",0,515,1300,220,UiKit.Panel,true);UiKit.Box(p,"PanelRight",1580,515,20,220,UiKit.Panel,true);
            }
            UiKit.Text(p,"DialogTitle",title,40,24,1390,62,37);
            UiKit.Button(p,"CloseDialog","×",1490,24,70,56,CloseModal);
            return p;
        }
        private void CloseModal(){if(DisplayPreferences!=null&&DisplayPreferences.IsPending){DisplayPreferences.Revert();return;}CancelControllerDrag();displayPanel=false;debugPanel=false;controllerOrderPanel=false;modal=false;detailDieId=null;table.HidePreview();main.gameObject.SetActive(true);UiKit.Clear(overlay);}
        private void SaveFailure()
        {
            var p=OpenModal("保存尚未成功");
            UiKit.Text(p,"Message","这一操作的结果已经确定，正在等待写入。\n后续规则操作已暂停；重试不会再次抽取随机数、扣款或发放奖励。\n\n"+notice,40,150,1500,300,31,UiKit.Gold);
            UiKit.Button(p,"RetrySave","重试保存原结果",40,610,1500,70,()=>{var r=Session.RetrySave();CloseModal();if(r.accepted)PresentCommitted(r.receipt.command,r);else{notice=r.error;Refresh();}},true,true);
        }
        private void Pause()
        {
            if(IsBusy)SkipVisual();
            displayPanel=false;debugPanel=false;controllerOrderPanel=false;
            var p=OpenModal("暂停与设置");
            UiKit.Text(p,"Info","规则结果已自动保存。暂停、快动画或跳过表现不会重新投掷。",40,100,1500,65,25,UiKit.Muted);
            UiKit.Button(p,"Fast","快动画："+(fast?"开":"关"),40,180,720,58,()=>{fast=!fast;PlayerPrefs.SetInt("fast",fast?1:0);Pause();});
            UiKit.Button(p,"Reduced","减少动态："+(reduced?"开":"关"),820,180,720,58,()=>{reduced=!reduced;PlayerPrefs.SetInt("reduced",reduced?1:0);Pause();});
            UiKit.Text(p,"VolumeLabel","音量",40,265,1500,40,25);
            var sliderRoot=UiKit.Box(p,"Volume",40,315,1500,28,UiKit.PanelLight,true);
            var slider=UiKit.Component<UnityEngine.UI.Slider>(sliderRoot.gameObject);slider.onValueChanged.RemoveAllListeners();slider.fillRect=null;slider.minValue=0;slider.maxValue=1;slider.value=volume;
            var fill=UiKit.Box(sliderRoot,"Fill",0,0,1500,28,UiKit.Gold);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;slider.fillRect=fill;
            slider.onValueChanged.AddListener(v=>{volume=v;audioSource.volume=v;PlayerPrefs.SetFloat("volume",v);});
            UiKit.Button(p,"Resume","返回游戏",40,625,720,60,CloseModal,true,true);
            UiKit.Button(p,"MainMenu","保存并返回主菜单",820,625,720,60,ReturnMenu);
            UiKit.Button(p,"DisplaySettings","显示与分辨率",40,380,720,58,ShowDisplaySettings);
            UiKit.Button(p,"PauseHandbook","牌型与颜色手册 [H]",820,380,720,58,ShowHandbook,ViewState!=null&&!Session.HasPendingSave);
            DebugAccess(p);
            UiKit.Text(p,"SavePath","存档："+saves.SavePath,40,690,1500,60,18,UiKit.Muted);
        }
        private void ShowDie(string id)
        {
            if(IsBusy)return;detailDieId=id;var die=ViewState.dice.First(x=>x.dieId==id);
            var p=OpenModal(id+" · 六面与概率",true); detailDieId=id;
            var current=ViewState.hand?.currentResults.FirstOrDefault(x=>x.dieId==id);
            var units=Session.ProbabilityUnits(id);
            FaceCards(p,die,125,null,units);
            UiKit.Text(p,"Opposites","物理对面：F01 ↔ F06     F02 ↔ F05     F03 ↔ F04\n概率包含当前已激活加权；上方始终显示永久六面数据。",40,405,1500,100,26,UiKit.Muted);
            string text="永久六面总和："+die.faces.Sum(f=>f.permanentPip)+(die.isGlass?"    玻璃：每次参与投掷有15%永久损坏风险":"")+(die.lifetime=="stage_temporary"?"    本关临时骰":"");
            if(current!=null&&current.hasRolled)
            {
                var face=die.faces.First(f=>f.faceId==current.currentFaceId);int effective=current.currentPip;
                if(ViewSources.Any(x=>x.abilityId=="C12")&&effective%2==1)effective++;
                text+="\n本次 "+current.currentFaceId+"：永久 "+face.permanentPip+" → 当前 "+current.currentPip+" → 有效 "+effective;
                if(current.remoteOverride)text+="（遥控覆盖）";
                text+="\n当前颜色 "+(current.currentColor=="red"?"红 R":"蓝 B")+"；计分颜色以提交栏位置、控制器和红潮为准。";
            }
            UiKit.Text(p,"Layers",text,40,535,1200,175,25,UiKit.Gold);
            var rotation=Quaternion.Euler(20,25,15);table.ShowPreview(die,new Vector2(1600,770),rotation,1.4f);
            var previewHit=UiKit.Component<DiePointer>(UiKit.Box(p,"RotatePreview",1300,515,280,220,Color.clear,true).gameObject);previewHit.clicked=null;previewHit.dragStart=null;previewHit.dragged=null;previewHit.dragEnd=null;
            previewHit.dragged=e=>{rotation=Quaternion.Euler(e.delta.y*.65f,-e.delta.x*.65f,0)*rotation;table.ShowPreview(die,new Vector2(1600,770),rotation,1.4f);};
            UiKit.Text(p,"PreviewHint","拖动旋转 · 永久六面",1290,737,300,40,18,UiKit.Muted,TextAlignmentOptions.Center);
        }
        private void ShowBreakdown(SubmissionSnapshot snapshot,ScoreBreakdown b)
        {
            if(snapshot==null||b==null)return;
            var p=OpenModal("分数来源 · 只读快照");
            var receipt=ViewState.processedReceipts.LastOrDefault(r=>r.command!=null&&r.command.kind==CommandKind.Submit);
            bool historical=ViewState.lastSubmission!=null&&snapshot.handId==ViewState.lastSubmission.snapshot.handId;
            string effects=historical&&receipt!=null?string.Join("\n",receipt.effects.Select(EffectText)):string.Join("\n",Session.PreviewSubmissionEffects().Select(EffectText));
            var trace=ScoreExplanation.Build(snapshot,b,Config);
            float diceHeight=trace.dice.Sum(d=>86+d.steps.Length*34);
            float globalHeight=58+trace.global.Length*46;
            var list=UiKit.Scroll(p,"ScoreDetail",40,110,1500,620,Math.Max(620,110+diceHeight+globalHeight+280));
            var formula=UiKit.Text(list,"Formula",$"基础 {b.baseTotal} × 牌型 {Factor(b.totalHandMult100)} × 颜色 {Factor(b.colorMult100)} × 红组 {Factor(b.redMult100)} × 控制器 {b.controllerMultiplierNumerator}/{b.controllerMultiplierDenominator} × 2^{b.enchantMultiplierCount}\n全部相乘后向下取整一次 = {Big(b.score)}",12,6,1440,80,29,UiKit.Gold);
            float cursor=6+FitHeight(formula,80)+20;
            for(int i=0;i<snapshot.dice.Length;i++)
            {
                var d=snapshot.dice[i];var f=d.permanentFacesCopy.First(x=>x.faceId==d.faceId);
                UiKit.Text(list,"DieLine"+i,$"{i+1}. {d.dieId}/{d.faceId}  永久 {f.permanentPip} → 当前 {d.currentPip} → 有效 {b.effectivePips[i]}  /  {(b.effectiveColors[i]=="red"?"红 R":"蓝 B")}\n永久六面和 {d.permanentSixFaceSum}  ·  本骰点数贡献 {b.pipContributions[i]}",12,cursor,1440,70,25);
                cursor+=72;
                for(int j=0;j<trace.dice[i].steps.Length;j++)
                {
                    var line=UiKit.Text(list,"DieStep"+i+"_"+j,ScoreStepText(trace.dice[i].steps[j]),40,cursor,1410,33,23,UiKit.Muted);cursor+=FitHeight(line,33)+4;
                }
                cursor+=14;
            }
            UiKit.Text(list,"GlobalTitle","全局结算 · 中间不取整",12,cursor,1440,48,28,UiKit.Gold);cursor+=58;
            for(int i=0;i<trace.global.Length;i++){var line=UiKit.Text(list,"GlobalStep"+i,ScoreStepText(trace.global[i]),12,cursor,1440,43,25);cursor+=FitHeight(line,43)+5;}
            string triggered=b.triggeredControllerIds.Length==0?"本次无控制器触发":string.Join(" · ",b.triggeredControllerIds.Select(NameOf));
            var footer=UiKit.Text(list,"Triggered",triggered+"\n永久复制/扩散只在提交后执行，不回溯更改本次分数。"+(effects.Length>0?(historical?"\n\n实际提交后效果：\n":"\n\n确认提交将执行：\n")+effects:""),12,cursor+12,1440,260,25,UiKit.Teal);
            list.sizeDelta=new Vector2(list.sizeDelta.x,Math.Max(620,cursor+FitHeight(footer,260)+36));
        }
        private static float FitHeight(TextMeshProUGUI label,float minimum)
        {
            float height=Math.Max(minimum,label.GetPreferredValues(label.text,label.rectTransform.rect.width,float.PositiveInfinity).y+4);
            label.rectTransform.sizeDelta=new Vector2(label.rectTransform.sizeDelta.x,height);return height;
        }
        private string ScoreStepText(ScoreStep step)
        {
            string label=step.sourceId!=null?NameOf(step.sourceId):step.kind=="glass"?"玻璃":step.kind=="gold"?"本面镀金":step.kind=="unused_rolls"?"剩余投掷奖励":step.kind=="hand"?"牌型总倍率":step.kind=="color"?"独立颜色乘区":step.kind=="red_group"?"同点红组":step.kind=="floor"?"最后向下取整":"最终加分";
            if(step.kind=="enchantment_base"||step.kind=="enchantment_multiplier")label+=" · 附魔";
            if(!string.IsNullOrEmpty(step.emitterId))label+=" ["+step.emitterId+(step.stateOwnerId!=step.emitterId&&!string.IsNullOrEmpty(step.stateOwnerId)?" → "+step.stateOwnerId:"")+"]";
            string before=step.before,after=step.after;
            return step.operation=="floor"?$"{label}：{before} → {after}":$"{label}：{before} {step.operation} {step.operand} → {after}";
        }
        private string EffectText(EffectRecord e)
        {
            switch(e.kind)
            {
                case "DieCopied":return NameOf(e.sourceId)+"："+e.otherDieId+" 的六面复制到 "+e.dieId;
                case "FaceDyed":return NameOf(e.sourceId)+"："+e.dieId+" / "+e.faceId+(e.sourceId=="C38"?" 永久染蓝":" 永久染红");
                case "FaceValueChanged":return "永久点数："+e.dieId+" / "+e.faceId+" "+e.oldValue+" → "+e.newValue;
                case "DieDyed":return "永久染色："+e.dieId+" 六面全部染红";
                case "DieBroken":return e.dieId+" 玻璃损坏，已移出骰组";
                case "TemporaryDieExpired":return e.dieId+" 临时骰已在关卡结束时移除";
                case "ExtraRolls":return NameOf(e.sourceId)+"：增加 "+e.newValue+" 次投掷";
                case "SubmissionIncome":return "存钱罐：本次提交收入 +"+e.newValue+" 金币";
                case "HandGrown":return "牌型提升："+NameOf(e.detail)+" "+e.oldValue+" → "+e.newValue+"级";
                case "ActiveColorChanged":return "飘忽不定：下一颜色 "+e.detail;
                case "ControllerDestroyed":return "玻璃大炮："+e.detail+" 本次计分后自毁";
                case "FundAccrued":return "成长基金：本实例累积增值 "+e.newValue;
                default:return (string.IsNullOrEmpty(e.sourceId)?"":NameOf(e.sourceId)+" · ")+e.kind;
            }
        }
        private void RemoteTargets(OwnedItem item)
        {
            var chosen=new List<string>();var pips=new Dictionary<string,int>();
            Action draw=null;
            draw=()=>
            {
                var p=OpenModal("遥控骰子 · 恰好2个未锁目标");
                UiKit.Text(p,"RemoteRule","选择两颗不同未锁骰，每颗指定1–6。确认同时消耗道具和1次投掷；所有未锁骰一起投。\n玻璃先检查损坏：目标若损坏，不补选、不退还。物理面、颜色与镀层照常抽取。",40,108,1500,100,27,UiKit.Muted);
                var unlocked=ViewState.dice.Where(d=>ViewState.hand==null||!ViewState.hand.orderedLockedIds.Contains(d.dieId)).OrderBy(d=>d.createdSerial).ToArray();
                for(int i=0;i<unlocked.Length;i++){string id=unlocked[i].dieId;UiKit.Button(p,"RemoteDie"+id,id,40+(i%5)*300,240+(i/5)*70,280,55,()=>{if(chosen.Contains(id)){chosen.Remove(id);pips.Remove(id);}else if(chosen.Count<2){chosen.Add(id);pips[id]=6;}draw();},true,chosen.Contains(id));}
                for(int i=0;i<chosen.Count;i++)
                {
                    string id=chosen[i];float y=415+i*90;UiKit.Text(p,"Chosen"+id,id,40,y,210,60,30,UiKit.Gold);
                    for(int n=1;n<=6;n++){int pip=n;bool odd=ViewSources.Any(x=>x.abilityId=="C12")&&pip%2==1;UiKit.Button(p,"RemotePip"+id+"_"+n,pip+(odd?"→"+(pip+1):""),260+(n-1)*205,y,190,58,()=>{pips[id]=pip;draw();},true,pips[id]==pip);}
                }
                UiKit.Button(p,"RemoteConfirm","确认使用并投掷",40,660,1500,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.UseItem,itemInstanceId=item.instanceId,dieIds=chosen.ToArray(),targetPips=chosen.Select(id=>pips[id]).ToArray()});},chosen.Count==2&&!IsBusy&&Session.PendingIds.Length==0,true);
            };
            draw();
        }
        private void BeginTutorial()
        {
            if(Session!=null&&Session.HasPendingSave){SaveFailure();return;}
            UiKit.Release(main);UiKit.Release(overlay);
            var practiceConfig=JsonUtility.FromJson<GameConfig>(GameConfig.PrepareJsonForUnity(configAsset.text));
            practiceConfig.stages[0].normalTarget="50";
            var memory=new MemorySaveStore();var practice=new GameSession(practiceConfig,memory);
            practice.Execute(new SessionCommand{kind=CommandKind.NewRun,commandId="tutorial-new",seed=41});
            var state=practice.State;state.isDebug=true;state.debugWorkspace=true;
            int[] values={4,4,2,3,6};
            for(int i=0;i<state.dice.Count;i++)foreach(var face in state.dice[i].faces){face.permanentPip=values[i];face.baseColor=i<2?"red":"blue";}
            Session=new GameSession(practiceConfig,memory,state);tutorial=true;tutorialStep=0;inMenu=false;
            notice="独立练习：目标50分，固定4红、4红、2蓝、3蓝、6蓝。不会改变正式挑战存档。";
            Refresh();
        }
    }
}
