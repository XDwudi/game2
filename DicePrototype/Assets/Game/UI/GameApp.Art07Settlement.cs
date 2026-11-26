using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private Art07SettlementReveal settlementReveal07;
        private string armedSettlement07;
        private readonly HashSet<string> shownSettlements07=new HashSet<string>(StringComparer.Ordinal);
        public bool SettlementRevealPlaying07=>settlementReveal07!=null&&settlementReveal07.IsPlaying;
        public float SettlementRevealElapsed07=>settlementReveal07==null?0:settlementReveal07.Elapsed;
        private static string SettlementIdentity07(RunState state)
        {
            var settlement=state?.lastStageSettlement;if(settlement==null)return null;
            // Debug can rebuild the same stage within one run. Its newly committed
            // settlement is a new presentation, while reloading that command is not.
            var receipt=state.processedReceipts.LastOrDefault(r=>r.settlement?.settlementId==settlement.settlementId);
            return state.runId+"/"+settlement.settlementId+"/"+(receipt?.commandId??"saved");
        }

        // Called only after a successful, nonduplicate committed command; restores never arm this.
        private void ArmSettlementReveal07(RunState before,RunState after)
        {
            if(after?.phase!="StageSettlement")return;
            string identity=SettlementIdentity07(after);
            if(identity!=null&&identity!=SettlementIdentity07(before))armedSettlement07=identity;
        }
        private void CompleteSettlementReveal07(){if(settlementReveal07!=null)settlementReveal07.Complete();}
        private void TickSettlementReveal07(float seconds)
        {
            if(!SettlementRevealPlaying07)return;
            settlementReveal07.SyncOptions(fast,reduced,instant07);
            if(inMenu||ViewState?.phase!="StageSettlement"||settlementReveal07.Identity!=SettlementIdentity07(ViewState))
            {CompleteSettlementReveal07();return;}
            settlementReveal07.Tick(seconds,modal);
        }
        private void SettlementSkipTarget07(RectTransform target)
        {
            var image=target.GetComponent<UnityEngine.UI.Image>();if(image!=null)image.raycastTarget=true;
            var button=UiKit.Component<UnityEngine.UI.Button>(target.gameObject);button.transition=UnityEngine.UI.Selectable.Transition.None;
            button.enabled=true;button.interactable=true;button.targetGraphic=image;button.onClick.RemoveAllListeners();button.onClick.AddListener(CompleteSettlementReveal07);
        }
        private string detailsIdentity15;
        private bool showSettlementDetails15;

        private void ContinueSettlement15()
        {
            if (SettlementRevealPlaying07 || modal || IsBusy || Session.HasPendingSave || ViewState?.phase != "StageSettlement") return;
            var result = Send(new SessionCommand { kind = CommandKind.Continue });
            if (result.accepted && !result.noChange && !result.duplicate) ArmPagePointerGuard15();
        }

        private void DrawSettlement07()
        {
            var r = ViewState.lastStageSettlement;
            if (r == null) return;
            ShowSettlementBackdrop07();
            string identity = SettlementIdentity07(ViewState);
            bool animate = armedSettlement07 == identity && shownSettlements07.Add(identity);
            if (armedSettlement07 == identity) armedSettlement07 = null;
            if (detailsIdentity15 != identity) { detailsIdentity15 = identity; showSettlementDetails15 = false; }
            bool large = Large11, boss = Config.stages[ViewState.stage.stageIndex].stageKind == "boss";
            bool exhausted = ViewState.failureReason == "NoPermanentDice" && ViewState.stage.settlementContinuation == "RunFailed";
            bool finalBoss = boss && ViewState.stage.settlementContinuation == "RunWon";
            float leftW = large ? 1056 : 816;
            var scrim = UiKit.Box(main,"SettlementScrim07",0,92,1920,988,new Color(0,0,0,.35f),true);
            SettlementSkipTarget07(scrim);
            var bounds = boss ? (large ? new Rect(128,120,1664,800) : new Rect(288,150,1344,660))
                              : (large ? new Rect(400,140,1120,780) : new Rect(520,176,880,620));
            var p = UiKit.PaperFrame(main,"Rewards",bounds.x,bounds.y,bounds.width,bounds.height,UiKit.Paper,UiKit.Ink,true);
            SettlementSkipTarget07(p);
            Color ink = LabelOn11(p,UiKit.Ink), muted = LabelOn11(p,PaperMuted071);
            var title = UiKit.Text(p,"Cleared","第 "+(ViewState.stage.stageIndex+1)+" 关完成",32,24,large?720:560,large?76:64,large?32:42,ink);
            title.fontStyle = FontStyles.Bold; UiKit.Role(title,TextRole.ScreenTitle);
            var kind = Surface11(p,"SettleKind15",large?868:668,32,large?220:180,large?60:48,"ui_chip_formula",boss?UiKit.Boss:r.risk?UiKit.Pressure:UiKit.PanelLight);
            TabLabel11(kind,"Label",StageKindLabel10(ViewState.stage.stageIndex,r.risk),22,LabelOn11(kind));
            NoWrap11(UiKit.Text(p,"FinalStageScore","本关得分 "+CompactScore(r.score)+"（目标 "+CompactScore(r.target)+"）",32,large?108:92,leftW,large?56:40,24,ink));
            ReceiptRule071(p,"ReceiptHeadingRule",32,large?172:140,leftW);
            float summaryY = large ? 188 : 156;
            var summary = UiKit.Rect(p,"SettlementSummary15",32,summaryY,leftW,large?476:376);
            UiKit.Text(summary,"SettleTotalLabel15","共获得",0,0,leftW,large?52:36,22,muted,TextAlignmentOptions.Center);
            float totalW = large ? 400 : 320, totalH = large ? 120 : 104;
            var total = Surface11(summary,"SettleTotal15",(leftW-totalW)*.5f,large?56:40,totalW,totalH,"ui_chip_formula",UiKit.Reward);
            var value = NoWrap11(UiKit.Text(total,"SettlementNetCash","+"+PlayerNumberFormat.Gold(r.goldAfter-r.goldBefore),0,0,totalW,totalH,large?48:56,LabelOn11(total),TextAlignmentOptions.MidlineRight));
            value.fontStyle = FontStyles.Bold; UiKit.Role(value,TextRole.BigNumber);
            var unit = NoWrap11(UiKit.Text(total,"SettleUnit15","金币",0,0,totalW,totalH,large?24:28,LabelOn11(total),TextAlignmentOptions.MidlineLeft));
            float unitW = Mathf.Ceil(unit.GetPreferredValues(unit.text).x)+4, available = totalW-unitW-36;
            value.fontSize = Mathf.Max(31.5f,Mathf.Min(value.fontSize,value.fontSize*available/Mathf.Max(1,value.GetPreferredValues(value.text).x)));
            float numW = Mathf.Min(available,Mathf.Ceil(value.GetPreferredValues(value.text).x)+16), start = (totalW-numW-8-unitW)*.5f;
            value.rectTransform.anchoredPosition = new Vector2(start,0); value.rectTransform.sizeDelta = new Vector2(numW,totalH);
            UiKit.FitNumberInk21(value);
            unit.rectTransform.anchoredPosition = new Vector2(start+numW+8,0); unit.rectTransform.sizeDelta = new Vector2(unitW,totalH);
            UiKit.Text(summary,"SettleGold15","金币 "+PlayerNumberFormat.Gold(r.goldBefore)+" → "+PlayerNumberFormat.Gold(r.goldAfter),0,large?184:152,leftW,large?56:40,24,ink,TextAlignmentOptions.Center);
            var chips = SettlementRewardTerms15.Chips(r);
            var chipRoots = DrawRewardChips15(summary,chips,leftW,large?252:208,large);
            string note = exhausted ? "本关通过了，但你已经没有骰子，这一局结束。" : r.rewardVoided ? "首领减益：本关不发过关奖励和剩余提交奖励" : null;
            if (note != null) UiKit.Text(summary,"SettleNote15",note,0,large?412:332,leftW,large?56:40,20,exhausted?ink:CueOn11(p,UiKit.Danger),TextAlignmentOptions.Center);
            var detailButton = SolidButton11(p,"SettlementDetailsToggle15",showSettlementDetails15?"收起明细":"明细",32,large?676:538,large?260:220,large?64:60,
                ()=>{ if(SettlementRevealPlaying07){CompleteSettlementReveal07();return;} showSettlementDetails15=!showSettlementDetails15; Refresh(); },true,22);
            // v1.1.1 (W7b, RULES_DELTA 13 row 4): the run's highest hand and highest clear ratio, beside 明细.
            DrawSettlementRecords111(p,large,leftW,ink,muted);
            if (showSettlementDetails15)
            {
                var content = UiKit.Scroll(p,"SettlementDetails",32,summaryY,leftW,large?476:376,large?476:376);
                DrawSettlementDetails15(content,r,chips,ink);
            }
            summary.gameObject.SetActive(!showSettlementDetails15);

            RectTransform stamp = null, front = null, back = null;
            if (boss) DrawBossReceipt15(p,r,large,finalBoss,exhausted,out stamp,out front,out back);
            string action = exhausted ? "查看结果" : finalBoss ? "查看整局结果" : boss ? "选择战利品" : "进入商店";
            var proceed = Primary16(main,"SettlementContinue",action,1496,962,392,80,ContinueSettlement15,!IsBusy&&!Session.HasPendingSave);
            var actionLabel = proceed.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            actionLabel.fontSize=(large?20:34)*UiKit.FontScale; actionLabel.fontStyle=FontStyles.Bold;
            UiKit.Role(actionLabel,TextRole.PrimaryButton);
            float labelW=Mathf.Ceil(actionLabel.GetPreferredValues(action).x)+3, arrowSize=large?30:42, sx=(392-labelW-10-arrowSize)*.5f;
            actionLabel.rectTransform.anchoredPosition=new Vector2(sx,0);actionLabel.rectTransform.sizeDelta=new Vector2(labelW,73);
            actionLabel.alignment=TextAlignmentOptions.MidlineLeft;
            var arrow=UiKit.Box(proceed.transform,"Arrow",sx+labelW+10,(73-arrowSize)*.5f,arrowSize,arrowSize,actionLabel.color).GetComponent<Image>();
            arrow.sprite=UiKit.Template("ui_icon_arrow");arrow.preserveAspect=true;arrow.raycastTarget=false;
            var skip=SolidButton11(main,"SkipSettlementReveal07","跳过演出",1496,962,392,80,CompleteSettlementReveal07,true,26);
            var layer=UiKit.Rect(main,"SettlementCoins07",0,0,1920,1080);
            var coins=new RectTransform[Math.Min(5,Math.Max(0,r.goldAfter-r.goldBefore))];
            for(int i=0;i<coins.Length;i++)
            {
                coins[i]=UiKit.Box(layer,"SettlementCoin"+i,0,0,24,24,Color.white);coins[i].pivot=new Vector2(.5f,.5f);
                var image=coins[i].GetComponent<Image>();image.sprite=UiKit.Template("ui_icon_coin");image.preserveAspect=true;image.raycastTarget=false;
            }
            var stats=main.Find("Stats07"); var wallet=stats?.Find("Money")?.GetComponent<TextMeshProUGUI>();
            if(stats!=null)stats.SetAsLastSibling();
            // Sample the fully opened sheet. A redraw mid-entrance may reuse a previously scaled pooled transform.
            p.localScale=Vector3.one;
            Vector2 from=main.InverseTransformPoint(total.TransformPoint(total.rect.center));
            Vector2 to=wallet==null?from:(Vector2)main.InverseTransformPoint(wallet.transform.TransformPoint(wallet.rectTransform.rect.center));
            if(settlementReveal07==null)settlementReveal07=gameObject.AddComponent<Art07SettlementReveal>();
            settlementReveal07.Bind(identity,animate,fast,reduced,instant07,p,chipRoots,coins,from,to,skip,proceed,detailButton,
                value,wallet,r.goldBefore,r.goldAfter,boss&&!exhausted,stamp,front,back);
        }
    }
}
