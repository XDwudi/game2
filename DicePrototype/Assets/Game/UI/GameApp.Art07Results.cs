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
                // v1.1.1 (W7a): the hand's own multiplier = hand x red group x (1 + bonus) x cards (ScoreFactors111), as before without item and boss.
                var m=DiceCraft.Presentation.ScoreFactors111.Multiplier(b,false);BigInteger n=m.Numerator,d=m.Denominator;
                if(n*multD>multN*d){multN=n;multD=d;}
            }
            if(multN>0){var gcd=BigInteger.GreatestCommonDivisor(multN,multD);multN/=gcd;multD/=gcd;}
            // v1.1 (W09): a decimal multiplier; practice (from the handbook) has its own labels instead of the debug ones.
            string time=runClockKnown07&&runClockId07==ViewState.runId?TimeSpan.FromSeconds(runSeconds07).ToString(@"hh\:mm\:ss"):IsPracticeRun11?"练习不计时":ViewState.debugWorkspace?"调试时长未记录":"未记录";
            string kind=IsPracticeRun11?" · 练习模式 · 不计成绩":ViewState.isDebug?" · 调试对局 · 不计普通成绩":"";
            return new[]{CompactScore(total.ToString()),CompactScore(max.ToString()),PlayerNumberFormat.Times(multN,multD),time,"种子 "+ViewState.seed+kind};
        }
        private void DrawRunResult07()
        {
            SyncRunClock07(); SaveRunClock07();
            bool won = ViewState.phase == "RunWon", large = Large11;
            float top = large ? 40 : 160, height = large ? 1000 : 760;
            var paper = UiKit.PaperFrame(main, "RunResult", 64, top, 1368, height, UiKit.Paper);
            Color ink = LabelOn11(paper, UiKit.Ink), muted = LabelOn11(paper, PaperMuted071);
            var title = NoWrap11(UiKit.Text(paper, "EndTitle", won ? "24 关全部通过" : "这一局结束了",
                40, 32, 860, large ? 96 : 90, large ? 40 : 58, ink));
            title.fontStyle = FontStyles.Bold; UiKit.Role(title, TextRole.ScreenTitle);
            TextMeshProUGUI message;
            if (won)
            {
                // A debug terminal checkpoint may omit its active modifier. The run-end seal always belongs to tier four.
                var modifier = Config.bossModifiers.FirstOrDefault(m => m.id == ViewState.stage.modifierId && m.tier == 4)?.id
                    ?? Config.bossModifiers.First(m => m.tier == 4).id;
                BossEmblem15(paper, "BossEmblem15", modifier, 920, 24, 176);
                message = UiKit.Text(paper, "EndMessage", "这一桌，属于你的构筑。", 40, 128, 860, large ? 66 : 44, 26, ink);
            }
            else
            {
                bool noDice = ViewState.failureReason == "NoDice", noPermanent = ViewState.failureReason == "NoPermanentDice";
                string reason = noDice ? "没有骰子可用了。" : noPermanent ? "本关通过了，但没有骰子可以继续。" : "提交次数用完，没有达到本关目标。";
                string tag = noDice ? "骰子用完" : noPermanent ? "没有骰子" : "未达标";
                float tagH = large ? 64 : 56;
                var reasonTag = UiKit.Rect(paper, "FailureReasonTag", 40, 136, 220, tagH);
                UiKit.Outline(reasonTag, "PrintedTag", 0, 0, 220, tagH, UiKit.Danger, 2);
                UiKit.Text(reasonTag, "Label", tag, 4, 0, 212, tagH, 21, CueOn11(paper, UiKit.Danger), TextAlignmentOptions.Center);
                var delta = BigInteger.Max(0, ExactScoreMath.Parse(ViewState.stage.target) - ExactScoreMath.Parse(ViewState.stage.score));
                message = UiKit.Text(paper, "EndMessage", reason + "\n距离本关目标还差 " + CompactScore(delta.ToString()) + " 分。",
                    276, 136, 1052, large ? 118 : 96, 26, ink);
                int index = ViewState.stage.stageIndex;
                UiKit.Text(paper, "EndWhere15", $"第 {index + 1} 关 · 第 {Config.stages[index].chapter} 章 · {StageKindLabel10(index, ViewState.stage.risk)}",
                    40, large ? 262 : 236, 1288, large ? 50 : 36, 22, muted);
            }
            var values = ResultStatisticRows07();
            string[] names = {"EndTotalScore","EndMaximumScore","EndMaximumMultiplier","EndDuration"};
            string[] labels = {"总得分","最大单手","最高倍率","用时"};
            var groups = new RectTransform[6];
            float statsY = won ? 236 : large ? 336 : 300;
            for (int i = 0; i < 4; i++)
            {
                float x = 40 + (large ? (i % 2) * 652 : i * 328), y = statsY + (large ? (i / 2) * 151 : 0);
                float w = large ? 636 : 304, h = large ? 139 : 112;
                var cell = Surface11(paper, names[i], x, y, w, h, "ui_chip_formula", Color.Lerp(UiKit.Paper,UiKit.Ink,.06f));
                UiKit.Text(cell, "Caption", labels[i], 16, 8, w-32, large ? 45 : 34, 20, LabelOn11(cell, muted));
                var number = NoWrap11(UiKit.Text(cell, "Value", values[i], 16, large ? 51 : 42, w-32, large ? 75 : 61, 34, LabelOn11(cell, ink), TextAlignmentOptions.MidlineLeft));
                number.fontStyle = FontStyles.Bold;
                if (i != 3 || runClockKnown07) UiKit.Role(number, TextRole.BigNumber);
                float measured = number.GetPreferredValues(number.text).x;
                if (measured > w-32) number.fontSize *= (w-32)/measured;
                UiKit.FitNumberInk21(number);
                groups[i] = cell;
            }
            float buildY = large ? (won ? 544 : 640) : won ? 372 : 432;
            groups[4] = ResultBuild15(paper, buildY);
            float seedY = large ? buildY + 180 : won ? 540 : 600;
            var seed = UiKit.Text(paper, "EndStats", values[4], 40, seedY, 1288, large ? 46 : 40, large ? 20 : 22, muted);
            groups[5] = seed.rectTransform;
            float by = large ? 906 : 648, bh = large ? 72 : 80;
            Primary16(paper, "Again", "再开一局", 40, by, 360, bh, ShowNewRun07);
            SolidButton11(paper, "EndBreakdown", "查看构筑", 424, by, 300, bh, ShowControllerOrder, true, 23);
            SolidButton11(paper, "ReturnMenu", "返回主菜单", 748, by, 300, bh, ReturnMenu, true, 23);
            ResultDice15(top, height, won);
            var reveal = UiKit.Component<Art07ScreenReveal>(main.gameObject);
            string key = ViewState.runId + ":" + ViewState.phase + ":" + ViewState.processedReceipts.LastOrDefault()?.commandId;
            if (won)
            {
                foreach(var t in new[]{title,message}) { var g=t.GetComponent<CanvasGroup>(); if(g!=null)g.alpha=1; }
                reveal.BindAfterStamp(key, groups);
                var stamp = UiKit.Rect(paper, "RunVictorySeal07", 1112, 62, 216, 100);
                UiKit.Outline(stamp, "PrintedSeal", 0, 0, 216, 100, UiKit.Gold, 3);
                UiKit.Outline(stamp, "InnerSeal", 7, 7, 202, 86, UiKit.Gold, 1);
                UiKit.Text(stamp, "SealLabel", "全关完成", 12, 18, 192, 64, 24, ink, TextAlignmentOptions.Center);
                reveal.BindStamp(key, stamp);
                var skip = SolidButton11(paper, "SkipResultReveal", "显示全部", 1068, by, 260, bh, CompleteRunResult07, true, 23);
                reveal.ShowWhilePlaying15(skip.gameObject);
            }
            else reveal.Bind(key, .12f, new[]{title.rectTransform,message.rectTransform}.Concat(groups).ToArray());
        }

        private RectTransform ResultBuild15(RectTransform paper, float y)
        {
            float titleH = Large11 ? 46 : 40, tileH = Large11 ? 116 : 104;
            var build = UiKit.Rect(paper, "EndBuild", 40, y, 1288, titleH + tileH);
            var title = UiKit.Text(build, "Heading", "构筑", 0, 0, 1288, titleH, 22, LabelOn11(paper,UiKit.Ink));
            title.fontStyle = FontStyles.Bold; UiKit.Role(title,TextRole.SectionTitle);
            var held = ViewState.controllers;
            if (held.Count == 0) UiKit.Text(build,"Empty","没有卡牌",0,titleH,1288,tileH,21,LabelOn11(paper,PaperMuted071));
            else TileRow13(held.Count,(i,px) =>
            {
                var c = held[i];
                var face = new CardFace13{contentId=c.definitionId,enchantment=c.enchantmentId,ordinal=i+1,namePx=px};
                return DrawCard13(build,"Controller_"+c.instanceId,104*i,titleH,100,tileH,face,CardSize13.Tile,()=>ShowController(c,false),true);
            });
            return build;
        }

        private void ResultDice15(float y, float height, bool won)
        {
            var submitted = ViewState.lastSubmission?.snapshot?.dice;
            int count = won ? ViewState.dice.Count : submitted?.Length ?? 0;
            string key = ViewState.runId + ":" + ViewState.phase + ":" + ViewState.actionSequence;
            if (key != resultDiceKey07) { resultDiceKey07=key; resultDicePage07=0; }
            const int perPage=10;
            int pages = Mathf.Max(1, (count + perPage - 1) / perPage);
            resultDicePage07 = Mathf.Clamp(resultDicePage07,0,pages-1);
            float headerH = Large11 ? 80 : 68;
            var panel = Surface11(main,"ResultDice15",1464,y,424,height,"ui_section_panel",UiKit.Section(UiKit.Panel));
            var cap = Surface11(panel,"HeaderStrip",8,8,408,headerH-8,"ui_section_header",UiKit.Caption(UiKit.PanelLight));
            float titleW = pages > 1 ? 222 : 378;
            FitLine13(cap,"LastDiceTitle",won ? "你的骰子 · " + count + " 颗" : "最后一手提交的骰子",12,4,titleW,22,LabelOn11(cap),TextAlignmentOptions.MidlineLeft,true,role:TextRole.SectionTitle);
            if (pages > 1)
            {
                SolidButton11(cap,"ResultDicePrevious","‹",234,4,52,headerH-20,()=>{resultDicePage07--;Refresh();},resultDicePage07>0,22);
                NoWrap11(UiKit.Text(cap,"ResultDicePage",(resultDicePage07+1)+"/"+pages,288,4,57,headerH-20,19.5f,LabelOn11(cap),TextAlignmentOptions.Center));
                SolidButton11(cap,"ResultDiceNext","›",346,4,52,headerH-20,()=>{resultDicePage07++;Refresh();},resultDicePage07+1<pages,22);
            }
            int shown = Mathf.Min(perPage, count - resultDicePage07 * perPage);
            // The ordinary eight-die case uses four 130-high rows. When a valid nine/ten-die
            // page needs a fifth row, share the space above the previous-hand button.
            int rows = Math.Max(4,(shown+1)/2);
            float rowH = Mathf.Min(130,(height - headerH - 94)/rows);
            for(int i=0;i<shown;i++)
            {
                int index = i + perPage * resultDicePage07;
                DieInstance die; string faceId;
                if (won)
                {
                    die = ViewState.dice[index];
                    string last = submitted?.FirstOrDefault(d=>d.dieId==die.dieId)?.faceId;
                    faceId = (die.faces.FirstOrDefault(f=>f.faceId==last)??die.faces[0]).faceId;
                }
                else
                {
                    var d = submitted[index]; if(d.permanentFacesCopy==null)continue;
                    die = new DieInstance{dieId="$result"+index,createdSerial=index,lifetime=d.lifetime,isGlass=d.isGlass,
                        faces=d.permanentFacesCopy.Select(f=>f.Clone()).ToArray()};
                    // Reconstruct the submitted face as actually scored, including temporary pip/colour.
                    faceId=d.faceId;
                    var face=die.faces.First(f=>f.faceId==faceId); face.permanentPip=d.currentPip; face.baseColor=d.currentColor;
                }
                DieView073(panel,"ResultDie15_"+index,"result-"+die.dieId,die,16+(i%2)*196,headerH+8+(i/2)*rowH,196,rowH,
                    .9f,false,false,null,null,null,DiceTable.FaceIndex(faceId));
            }
            if(ViewState.lastSubmission!=null) SolidButton11(panel,"PreviousSubmission","上一手明细",24,height-72,376,56,
                ()=>ShowBreakdown(ViewState.lastSubmission.snapshot,ViewState.lastSubmission.breakdown),!IsBusy,Large11?19.5f:21);
        }

    }
}
