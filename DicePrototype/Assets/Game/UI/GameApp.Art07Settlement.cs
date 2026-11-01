using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

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
        private void DrawSettlement07()
        {
            var r=ViewState.lastStageSettlement;if(r==null)return;
            ShowSettlementBackdrop07();
            string identity=SettlementIdentity07(ViewState);
            bool animate=armedSettlement07==identity&&!shownSettlements07.Contains(identity);
            shownSettlements07.Add(identity);if(armedSettlement07==identity)armedSettlement07=null;
            var scrim=UiKit.Box(main,"SettlementScrim07",0,0,1920,1080,new Color(0,0,0,.35f),true);SettlementSkipTarget07(scrim);
            var p=UiKit.Frame(main,"Rewards",520,168,880,744,UiKit.Paper,UiKit.Ink,true);SettlementSkipTarget07(p);
            UiKit.Text(p,"Cleared","第 "+(ViewState.stage.stageIndex+1)+" 关完成",24,20,560,76,UiKit.FontScale>1?32:42,UiKit.Ink);
            var skip=UiKit.Button(p,"SkipSettlementReveal07","跳过演出",604,28,252,60,CompleteSettlementReveal07);
            UiKit.Text(p,"FinalStageScore",CompactScore(r.score)+" / "+CompactScore(r.target),24,108,832,60,28,UiKit.Ink);
            var list=UiKit.Scroll(p,"SettlementDetails",24,184,832,380,380);float y=0;
            Func<string,string,RectTransform> line=(id,text)=>
            {
                var label=UiKit.Text(list,id,text,0,y,800,44,24,UiKit.Ink);y+=FitHeight(label,44)+24;return label.rectTransform;
            };
            var cashRows=new[]{line("SettlementBaseReward","基础奖励  +"+r.baseReward),line("SettlementUnusedReward","剩余提交奖励  +"+r.unusedSubmissionReward),line("SettlementInterest","利息  +"+r.interest)};
            line("SettlementNetCash",$"本次现金净增  +{r.goldAfter-r.goldBefore}\n奖励前余额 {r.goldBefore} → 结算后余额 {r.goldAfter}");
            var receipt=ViewState.processedReceipts.LastOrDefault(q=>q.settlement?.settlementId==r.settlementId);
            var through=ViewState.processedReceipts.Where(q=>receipt!=null&&q.actionSequence<=receipt.actionSequence).ToArray();
            var boundary=through.LastOrDefault(q=>q.command!=null&&(q.command.kind==CommandKind.StartStage||q.command.kind==CommandKind.ApplyDebugScene||q.command.kind==CommandKind.NewRun));
            var current=through.Where(q=>q.actionSequence>(boundary?.actionSequence??0)).ToArray();
            var submissions=current.Where(q=>q.submission?.snapshot.stageId==r.stageId&&q.submission.snapshot.runId==ViewState.runId).ToArray();
            var income=submissions.SelectMany(q=>q.effects).Where(e=>e.kind=="SubmissionIncome").Aggregate(System.Numerics.BigInteger.Zero,(sum,e)=>sum+e.newValue);
            line("SubmissionIncome","提交收入（本关已入账）  +"+income+" 金币\n已包含在奖励前余额中，不再次加入现金净增。");
            var funds=receipt?.effects.Where(e=>e.kind=="FundAccrued").ToArray()??Array.Empty<EffectRecord>();
            var fundLines=funds.Select(e=>
            {
                var prior=submissions.Reverse().Select(q=>q.submission.snapshot.equippedControllers.FirstOrDefault(c=>c.instanceId==e.detail)).FirstOrDefault(c=>c!=null)
                    ??boundary?.command?.debugScene?.controllers.FirstOrDefault(c=>c.instanceId==e.detail);
                string gain=prior==null?"卖价已增长":"卖价＋"+(e.newValue-prior.state.fundAccrual);
                return e.detail+" · "+gain+" · 基金累计 "+e.newValue;
            }).ToArray();
            line("FundGrowth",fundLines.Length==0?"本关无基金卖价增长。":string.Join("\n",fundLines)+"\n仅改变卖价；未出售时不进入钱包。");
            string[] expired=receipt?.effects.Where(e=>e.kind=="TemporaryDieExpired").Select(e=>e.dieId).ToArray()??Array.Empty<string>();
            line("TemporaryCleanup",expired.Length==0?"临时骰清理：本关无到期临时骰。":"临时骰清理：已移除 "+expired.Length+" 颗\n"+string.Join("、",expired));
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+12);
            bool exhausted=ViewState.failureReason=="NoPermanentDice"&&ViewState.stage.settlementContinuation=="RunFailed";
            UiKit.Text(p,"SettlementStatus",exhausted?"本关成功 · 永久骰已耗尽，整局结束。":"本关奖励已处理。",24,574,832,62,24,UiKit.Ink);
            UiKit.ActionButton(p,"SettlementContinue",exhausted?"查看结果 →":ViewState.stage.settlementContinuation=="Shop"?"进入商店 →":"查看整局结果 →",24,648,832,72,()=>{CompleteSettlementReveal07();Send(new SessionCommand{kind=CommandKind.Continue});},UiKit.Blue,!IsBusy&&!Session.HasPendingSave);

            // Symbols are below every HUD/paper label. They never cover wallet digits or controls.
            var coinLayer=UiKit.Rect(main,"SettlementCoins07",0,0,1920,1080);coinLayer.SetAsFirstSibling();
            int count=(int)Math.Min(5,Math.Max(0,r.goldAfter-r.goldBefore));
            var coins=new RectTransform[count];
            for(int i=0;i<count;i++)
            {
                coins[i]=UiKit.Box(coinLayer,"SettlementCoin"+i,0,0,24,24,Color.white);
                var image=coins[i].GetComponent<UnityEngine.UI.Image>();image.sprite=UiKit.Template("ui_icon_coin");image.raycastTarget=false;
            }
            Vector2 target=new Vector2(184,-610);
            var wallet=main.Find("Stats07/Money") as RectTransform;
            if(wallet!=null){var corners=new Vector3[4];wallet.GetWorldCorners(corners);target=main.InverseTransformPoint((corners[0]+corners[2])*.5f);}
            if(settlementReveal07==null)settlementReveal07=gameObject.AddComponent<Art07SettlementReveal>();
            settlementReveal07.Bind(identity,animate,fast,reduced,instant07,p,cashRows,coins,new Vector2(498,-566),target,skip);
        }
    }
}
