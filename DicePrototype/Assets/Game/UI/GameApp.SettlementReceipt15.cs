using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using RewardChip15 = DiceCraft.SettlementRewardTerms15.RewardChip15;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private RectTransform[] DrawRewardChips15(RectTransform parent,List<RewardChip15> chips,float width,float y,bool large)
        {
            float height=large?68:52,iconSize=large?36:28;
            var area=UiKit.Rect(parent,"SettlementChipArea15",0,y,width,large?148:116);
            var roots=new RectTransform[chips.Count];
            int first=0;float rowWidth=0,rowY=0;
            void CenterRow(int end)
            {
                float x=(width-rowWidth)*.5f;
                for(int j=first;j<end;j++)
                {
                    roots[j].anchoredPosition=new Vector2(x,-rowY);
                    x+=roots[j].sizeDelta.x+12;
                }
            }
            for(int i=0;i<chips.Count;i++)
            {
                var chip=chips[i];var sprite=chip.icon.StartsWith("C",StringComparison.Ordinal)?CompactIcon13(chip.icon):UiKit.Template(chip.icon);
                var root=roots[i]=Surface11(area,chip.id+"Chip",0,0,1,height,"ui_chip_formula",Color.Lerp(UiKit.Paper,UiKit.Ink,.06f));
                float left=sprite==null?12:12+iconSize+8;
                var label=NoWrap11(UiKit.Text(root,chip.id,chip.Text,left,0,width-left-16,height,22,LabelOn11(root),TextAlignmentOptions.MidlineLeft));
                float textW=Mathf.Ceil(label.GetPreferredValues(chip.Text).x)+4;
                float chipW=Mathf.Min(width,left+textW+16);
                root.sizeDelta=new Vector2(chipW,height);label.rectTransform.sizeDelta=new Vector2(chipW-left-16,height);
                if(sprite!=null)
                {
                    var icon=UiKit.Box(root,"Icon",12,(height-iconSize)*.5f,iconSize,iconSize,chip.icon.StartsWith("C",StringComparison.Ordinal)?Color.white:label.color).GetComponent<Image>();
                    icon.sprite=sprite;icon.raycastTarget=false;icon.preserveAspect=true;
                }
                if(i>first&&rowWidth+12+chipW>width)
                {CenterRow(i);first=i;rowWidth=0;rowY+=height+12;}
                rowWidth+=(i>first?12:0)+chipW;
            }
            CenterRow(chips.Count);return roots;
        }

        private string InterestDetail15(StageSettlement r)=>SettlementRewardTerms15.Interest(r,Config.interestDivisor,Config.maxInterestPrincipal,
            Config.Controller("C01").parameters.extraInterestPerTen,Config.interestPrincipalStep,ViewState.interestCapPurchases,NameOf("C96"),NameOf("O10"));

        private static readonly HashSet<string> StageEndSources15=new HashSet<string>(StringComparer.Ordinal)
            {"C58","C59","C129","C132","C141","C99","C120","C121","C115","C112","C114","C116"};

        private void DrawSettlementDetails15(RectTransform list,StageSettlement r,List<RewardChip15> chips,Color ink)
        {
            float width=list.sizeDelta.x-24,y=6;
            void Line(string id,string body,string amount=null)
            {
                float moneyW=amount==null?0:Mathf.Max(96,48*UiKit.FontScale);
                var label=UiKit.Text(list,id,body,12,y,width-moneyW,44,21,ink,TextAlignmentOptions.MidlineLeft);
                float height=FitHeight(label,44);
                if(amount!=null)
                {
                    var money=NoWrap11(UiKit.Text(list,id+"Amount",amount,12+width-moneyW,y,moneyW,height,21,ink,TextAlignmentOptions.MidlineRight));
                    money.fontStyle=FontStyles.Bold;
                }
                y+=height+5;ReceiptRule071(list,id+"Rule",12,y,width);y+=9;
            }
            foreach(var chip in chips)
                Line(chip.id+"Detail",chip.id=="SettlementInterest"?InterestDetail15(r):chip.label,"+"+PlayerNumberFormat.Gold(chip.amount));
            var receipt=ViewState.processedReceipts.LastOrDefault(q=>q.settlement?.settlementId==r.settlementId);
            if(receipt!=null)
            {
                long boundary=ViewState.processedReceipts.Where(q=>q.actionSequence<=receipt.actionSequence&&q.command!=null&&
                    (q.command.kind==CommandKind.StartStage||q.command.kind==CommandKind.ApplyDebugScene||q.command.kind==CommandKind.NewRun))
                    .Select(q=>q.actionSequence).DefaultIfEmpty(0).Max();
                var income=ViewState.processedReceipts.Where(q=>q.actionSequence>boundary&&q.actionSequence<=receipt.actionSequence&&
                    q.submission?.snapshot?.stageId==r.stageId&&q.submission.snapshot.runId==ViewState.runId)
                    .SelectMany(q=>q.effects).Where(e=>e.kind=="SubmissionIncome").Aggregate(System.Numerics.BigInteger.Zero,(sum,e)=>sum+e.newValue);
                if(income>0)Line("SubmissionIncome","本关提交时已得 +"+PlayerNumberFormat.Integer(income)+" 金币（已算在上面的金币里）");
                var funds=receipt.effects.Where(e=>e.kind=="FundAccrued").Select(e=>
                {
                    var card=ViewState.controllers.FirstOrDefault(c=>c.instanceId==e.detail);
                    // FundAccrued is one +3 event per emitter, including a copied fund. Its newValue is cumulative.
                    return CardLabel11(e.detail)+"卖价 +3"+(card==null?"（卖出时才得到）":"（现价 "+PlayerNumberFormat.Gold(Session.ControllerSalePriceIn(ViewState,card))+"，卖出时才得到）");
                }).ToArray();
                if(funds.Length>0)Line("FundGrowth",string.Join("\n",funds));
                int expired=receipt.effects.Count(e=>e.kind=="TemporaryDieExpired");
                if(expired>0)Line("TemporaryCleanup","本关的 "+expired+" 颗临时骰已移除");
                var other=receipt.effects.Where(e=>StageEndSources15.Contains(e.sourceId)).ToArray();
                if(other.Length>0)
                {
                    Line("OtherChanges15","结算时的其他变化");
                    for(int i=0;i<other.Length;i++)Line("OtherChange15_"+i,EffectText(other[i],receipt.submission?.snapshot));
                }
            }
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+12);
        }

        private void DrawBossReceipt15(RectTransform paper,StageSettlement r,bool large,bool finalBoss,bool exhausted,
            out RectTransform stamp,out RectTransform front,out RectTransform back)
        {
            float w=large?512:432,h=large?752:612;
            UiKit.Box(paper,"BossColumnRule15",large?1104:864,24,2,h,Color.Lerp(UiKit.Paper,UiKit.Ink,.45f));
            var col=UiKit.Rect(paper,"BossColumn15",large?1120:880,24,w,h);
            string modifier=ViewState.stage.modifierId;
            if(!string.IsNullOrEmpty(modifier))BossEmblem15(col,"BossEmblem15",modifier,(w-176)*.5f,0,176);
            stamp=Surface11(col,"BossDefeated15",(w-176)*.5f+108,116,108,60,"ui_frame_block",UiKit.Paper);
            UiKit.Ring(stamp,"Edge",1,1,106,58,UiKit.Boss);stamp.localRotation=Quaternion.Euler(0,0,-8);
            var mark=UiKit.Text(stamp,"Text","击败",4,0,100,58,large?20:25,CueOn11(stamp,UiKit.Boss),TextAlignmentOptions.Center);mark.fontStyle=FontStyles.Bold;
            var name=UiKit.Text(col,"BossName15",string.IsNullOrEmpty(modifier)?"首领":NameOf(modifier),0,large?188:184,w,large?64:44,28,LabelOn11(paper,UiKit.Ink),TextAlignmentOptions.Center);
            name.fontStyle=FontStyles.Bold;UiKit.Role(name,TextRole.BossName);
            ReceiptRule071(col,"BossReceiptRule15",0,large?260:240,w);front=back=null;
            if(exhausted)return;
            if(finalBoss)
            {
                UiKit.Text(col,"FinalBossNote15","终局首领 · 通关即胜利",0,large?276:264,w,large?60:40,22,LabelOn11(paper,PaperMuted071),TextAlignmentOptions.Center);return;
            }
            if(string.IsNullOrEmpty(r.lootKind))return;
            float cardH=large?452:332;
            var plaque=UiKit.Rect(col,"LootPlaque15",0,large?276:256,w,cardH);
            back=Surface11(plaque,"LootBack15",0,0,w,cardH,"ui_card_back",Color.white);
            front=Surface11(plaque,"LootFront15",0,0,w,cardH,"ui_stage_card",UiKit.Paper);
            foreach(var side in new[]{front,back}){side.pivot=new Vector2(.5f,.5f);side.anchoredPosition=new Vector2(w/2,-cardH/2);}
            var def=Config.Loot(r.lootKind);
            UiKit.Text(front,"LootCaption15","首领战利品",16,12,w-32,large?48:36,20,LabelOn11(front,PaperMuted071),TextAlignmentOptions.Center);
            var picture=LootPictureSprite15(r.lootKind);
            if(picture!=null)
            {
                var img=UiKit.Box(front,"LootPicture15",(w-160)*.5f,large?64:48,160,160,Color.white).GetComponent<Image>();img.sprite=picture;img.raycastTarget=false;img.preserveAspect=true;
            }
            else
            {
                var missing=Surface11(front,"LootPicture15",(w-160)*.5f,large?64:48,160,160,"ui_frame_block",UiKit.PanelLight);
                UiKit.Text(missing,"MissingPicture15","战利品图\n占位",8,8,144,144,21,LabelOn11(missing),TextAlignmentOptions.Center);
            }
            var item=NoWrap11(UiKit.Text(front,"LootName15",def.displayName,16,large?236:220,w-32,large?60:42,26,LabelOn11(front,UiKit.Ink),TextAlignmentOptions.Center));
            item.fontStyle=FontStyles.Bold;
            var shortText=UiKit.Text(front,"LootShort15",def.shortText,16,large?300:262,w-32,large?140:62,20,LabelOn11(front,UiKit.Ink),TextAlignmentOptions.Center);
            shortText.text=BalancedLines11(shortText,def.shortText,w-32);
            var hit=HitArea073(plaque,"LootHover15",0,0,w,cardH,CompleteSettlementReveal07);
            Art07AttachTooltip(hit,"settlement-loot",def.displayName,def.shortText,def.details,null);
        }
    }
}
