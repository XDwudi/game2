using System;
using System.Linq;
using System.Numerics;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private string runClockId07;
        private float runSeconds07,clockSaveElapsed07;
        private bool runClockKnown07;
        private int resultDicePage07;
        private string resultDiceKey07;
        private void StartRunClock07()
        {
            SaveRunClock07();
            runClockId07=Session.State.runId;runSeconds07=0;runClockKnown07=true;
            PlayerPrefs.SetFloat("art07.runSeconds."+runClockId07,0);PlayerPrefs.Save();
        }
        private void TickRunClock07()
        {
            if(ViewState==null||inMenu||modal)return;
            SyncRunClock07();
            if(ViewState.phase=="RunWon"||ViewState.phase=="RunFailed")return;
            runSeconds07+=Time.unscaledDeltaTime;clockSaveElapsed07+=Time.unscaledDeltaTime;
            if(clockSaveElapsed07>=30)SaveRunClock07();
        }
        private void SyncRunClock07()
        {
            if(ViewState==null||runClockId07==ViewState.runId)return;
            SaveRunClock07();runClockId07=ViewState.runId;
            runClockKnown07=PlayerPrefs.HasKey("art07.runSeconds."+runClockId07);
            runSeconds07=PlayerPrefs.GetFloat("art07.runSeconds."+runClockId07,0);
        }
        private void SaveRunClock07()
        {if(runClockKnown07&&runClockId07!=null){PlayerPrefs.SetFloat("art07.runSeconds."+runClockId07,runSeconds07);PlayerPrefs.Save();}clockSaveElapsed07=0;}
        private string[] ResultStatisticRows07()
        {
            BigInteger total=0,max=0,multN=0,multD=1;
            foreach(var record in ViewState.processedReceipts.Where(r=>r.command.kind==CommandKind.Submit))
            {
                var b=record.submission.breakdown;var score=ExactScoreMath.Parse(b.score);total+=score;if(score>max)max=score;
                var n=(BigInteger)b.totalHandMult100*b.colorMult100*b.redMult100*ExactScoreMath.Parse(b.controllerMultiplierNumerator)*BigInteger.Pow(Config.enchantRules.scoreMultiplier,b.enchantMultiplierCount);
                var d=1000000*ExactScoreMath.Parse(b.controllerMultiplierDenominator);
                if(n*multD>multN*d){multN=n;multD=d;}
            }
            if(multN>0){var gcd=BigInteger.GreatestCommonDivisor(multN,multD);multN/=gcd;multD/=gcd;}
            string time=runClockKnown07&&runClockId07==ViewState.runId?TimeSpan.FromSeconds(runSeconds07).ToString(@"hh\:mm\:ss"):ViewState.debugWorkspace?"调试时长未记录":"旧存档未记录";
            return new[]{"总得分  "+CompactScore(total.ToString()),"用时  "+time,"最大单手  "+CompactScore(max.ToString()),"最高倍率  ×"+CompactScore(multN.ToString())+(multD==1?"":"/"+multD),"种子  "+ViewState.seed+"   ·   "+(ViewState.isDebug?"调试对局 · 不计普通成绩":"普通对局")};
        }
        private void DrawRunResult07()
        {
            SyncRunClock07();SaveRunClock07();bool won=ViewState.phase=="RunWon";
            var submitted=ViewState.lastSubmission?.snapshot?.dice;
            string diceKey=ViewState.runId+":"+ViewState.phase+":"+ViewState.actionSequence;
            if(resultDiceKey07!=diceKey){resultDiceKey07=diceKey;resultDicePage07=0;}
            int diceCount=won?ViewState.dice.Count:submitted?.Length??0;
            if(diceCount>0)
            {
                UiKit.Text(main,"LastDiceTitle",won?"最终骰组 · "+diceCount+" 颗":"最后有效提交",1510,170,360,60,UiKit.FontScale>1?20:26,UiKit.Muted);
                int pages=(diceCount+9)/10;resultDicePage07=Mathf.Clamp(resultDicePage07,0,pages-1);
                for(int i=0;i<Math.Min(10,diceCount-resultDicePage07*10);i++)
                {
                    int index=resultDicePage07*10+i;DieInstance die;DieHandState face;
                    if(won)
                    {
                        die=ViewState.dice[index];
                        string lastFace=submitted?.FirstOrDefault(d=>d.dieId==die.dieId)?.faceId;
                        var current=die.faces.FirstOrDefault(f=>f.faceId==lastFace)??die.faces[0];
                        face=new DieHandState{dieId=die.dieId,currentFaceId=current.faceId,currentPip=current.permanentPip,currentColor=current.baseColor,hasRolled=true};
                    }
                    else
                    {
                        var d=submitted[index];if(d.permanentFacesCopy==null)continue;
                        die=new DieInstance{dieId="$result"+index,createdSerial=index,lifetime=d.lifetime,isGlass=d.isGlass,faces=d.permanentFacesCopy.Select(f=>f.Clone()).ToArray()};
                        face=new DieHandState{dieId=die.dieId,currentFaceId=d.faceId,currentPip=d.currentPip,currentColor=d.currentColor,hasRolled=true,isLocked=true};
                    }
                    table.Display(die,face,new UnityEngine.Vector2(1560+i%2*220,300+i/2*130),.9f);
                }
                if(pages>1)
                {
                    UiKit.Button(main,"ResultDicePrevious","上一页",1504,928,176,64,()=>{resultDicePage07--;Refresh();},resultDicePage07>0);
                    UiKit.Button(main,"ResultDiceNext","下一页",1696,928,176,64,()=>{resultDicePage07++;Refresh();},resultDicePage07<pages-1);
                    UiKit.Text(main,"ResultDicePage",(resultDicePage07+1)+" / "+pages,1504,1000,368,48,22,UiKit.Muted,TextAlignmentOptions.Center);
                }
            }
            var p=UiKit.Frame(main,"RunResult",472,152,976,776,UiKit.Paper,UiKit.Ink);
            string failure=ViewState.failureReason=="NoDice"?"活动骰已耗尽。":ViewState.failureReason=="NoPermanentDice"?"本关已成功，但没有永久骰可继续。":"提交次数已用尽，未达到本关目标。";
            if(!won)
            {
                UiKit.Box(p,"FailureTitleBand",24,16,928,112,UiKit.Ink);
                var tag=UiKit.Frame(p,"FailureReasonTag",40,140,200,44,UiKit.Hex("87535A"),UiKit.Hex("87535A"));
                UiKit.Text(tag,"Label",ViewState.failureReason=="NoDice"?"活动骰耗尽":ViewState.failureReason=="NoPermanentDice"?"无永久骰":"未达关卡目标",8,2,184,40,UiKit.FontScale>1?16:20,UiKit.Paper,TextAlignmentOptions.Center);
            }
            var title=UiKit.Text(p,"EndTitle",won?"24 关完成":"本次挑战结束",40,28,896,100,UiKit.FontScale>1?40:58,won?UiKit.Ink:UiKit.Paper);
            var delta=BigInteger.Max(0,ExactScoreMath.Parse(ViewState.stage.target)-ExactScoreMath.Parse(ViewState.stage.score));
            string detail=won?"这一桌，属于你的构筑。":failure+"\n距离本关目标还差 "+CompactScore(delta.ToString())+" 分。";
            var message=UiKit.Text(p,"EndMessage",detail,40,won?144:196,896,100,UiKit.FontScale>1?20:26,UiKit.Ink);
            var values=ResultStatisticRows07();
            RectTransform[] victoryRows=null;
            TextMeshProUGUI stats;
            if(won)
            {
                victoryRows=new RectTransform[6];
                string[] rowNames={"EndTotalScore","EndDuration","EndMaximumScore","EndMaximumMultiplier"};
                for(int i=0;i<4;i++)victoryRows[i]=UiKit.Text(p,rowNames[i],values[i],40,244+i*48,896,48,UiKit.FontScale>1?21:28,UiKit.Ink).rectTransform;
                stats=UiKit.Text(p,"EndStats",values[4],40,540,896,48,UiKit.FontScale>1?18:24,UiKit.Ink);
                victoryRows[5]=stats.rectTransform;
            }
            else stats=UiKit.Text(p,"EndStats",values[0]+"     "+values[1]+"\n"+string.Join("\n",values.Skip(2)),40,310,896,180,UiKit.FontScale>1?21:28,UiKit.Ink);
            var build=UiKit.Text(p,"EndBuild","构筑  "+(ViewState.controllers.Count==0?"无控制器":string.Join(" · ",ViewState.controllers.Select(c=>NameOf(c.definitionId)))),40,won?436:504,896,won?96:100,UiKit.FontScale>1?17:23,UiKit.Ink);build.overflowMode=TextOverflowModes.Ellipsis;
            if(won)victoryRows[4]=build.rectTransform;
            UiKit.ActionButton(p,"Again","再开一局",40,650,280,66,ShowNewRun07,UiKit.Blue);
            UiKit.ActionButton(p,"EndBreakdown","查看构筑",348,650,280,66,ShowControllerOrder,UiKit.Paper);
            UiKit.ActionButton(p,"ReturnMenu","返回主菜单",656,650,280,66,ReturnMenu,UiKit.Paper);
            var reveal=UiKit.Component<Art07ScreenReveal>(main.gameObject);
            string revealKey=ViewState.runId+":"+ViewState.phase+":"+ViewState.processedReceipts.LastOrDefault()?.commandId;
            if(won){foreach(var label in new[]{title,message}){var alpha=label.GetComponent<CanvasGroup>();if(alpha!=null)alpha.alpha=1;}reveal.BindAfterStamp(revealKey,victoryRows);}
            else reveal.Bind(ViewState.runId+":"+ViewState.phase,.12f,title.rectTransform,message.rectTransform,stats.rectTransform,build.rectTransform);
            if(won)
            {
                var seal=UiKit.Frame(p,"RunVictorySeal07",720,24,216,100,UiKit.Paper,UiKit.Gold);
                UiKit.Text(seal,"SealLabel","全关完成",12,22,192,56,24,UiKit.Ink,TextAlignmentOptions.Center);
                reveal.BindStamp(revealKey,seal);
                UiKit.Button(p,"SkipResultReveal","显示全部",704,592,232,60,CompleteRunResult07);
            }
        }
    }
}
