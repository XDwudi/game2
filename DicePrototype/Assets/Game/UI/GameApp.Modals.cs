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
        private bool diceSheet07,faceProbabilityExpanded07,saveErrorExpanded07;
        private string modalIdentity07;
        private Art07PanelMotion modalTail07;
        private bool PrepareModalMotion07(string identity)
        {
            bool fresh=!modal||modalIdentity07!=identity;modalIdentity07=identity;
            if(modalTail07==null)
            {
                var go=new GameObject("ModalFadeTail07",typeof(RectTransform),typeof(CanvasGroup),typeof(Art07PanelMotion));
                var rect=(RectTransform)go.transform;rect.SetParent(canvas.transform,false);rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(0,1);rect.anchoredPosition=Vector2.zero;rect.sizeDelta=new Vector2(1920,1080);
                modalTail07=go.GetComponent<Art07PanelMotion>();go.GetComponent<CanvasGroup>().blocksRaycasts=false;go.GetComponent<CanvasGroup>().interactable=false;go.SetActive(false);
            }
            modalTail07.CancelTail();return fresh;
        }
        private void FadeModal07(RectTransform panel,bool fresh,float enter=.16f,float exit=.12f)
        {UiKit.Component<Art07PanelMotion>(panel.gameObject).Enter(enter,exit,fresh,reduced);}
        private RectTransform OpenModal(string title,bool dicePreview=false)
        {
            bool fresh=PrepareModalMotion07(title);
            modal=true;UiKit.Clear(overlay);table.HidePreview();
            // Screen-space UI would otherwise occlude the main-camera preview aperture.
            main.gameObject.SetActive(!dicePreview);
            var scrim=new Color(.015f,.03f,.035f,.35f);
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
            UiKit.Button(p,"CloseDialog","×",1490,24,70,60,CloseModal);
            bool error=title=="本次结果尚未保存"||title=="存档读取说明"||title=="存档恢复说明"||title=="旧版本存档",manual=title.Contains("手册"),newRun=title=="新建挑战";
            FadeModal07(p,fresh,error?.12f:manual?.14f:newRun?.18f:.16f,error||manual||newRun||title=="暂停"?.10f:.12f);
            return p;
        }
        private void CloseModal()
        {
            if(DisplayPreferences!=null&&DisplayPreferences.IsPending){DisplayPreferences.Revert();return;}
            // The tail has only renderer meshes. Logical close, input, save and dice restoration stay synchronous.
            if(modal&&modalTail07!=null)
            {
                for(int i=0;i<overlay.childCount;i++)
                {
                    var panel=overlay.GetChild(i) as RectTransform;if(panel==null||panel.name!="Dialog"||!panel.gameObject.activeInHierarchy)continue;
                    var motion=panel.GetComponent<Art07PanelMotion>();modalTail07.CaptureExit(panel,motion!=null?motion.ExitDuration:.1f,reduced);break;
                }
            }
            bool restoreDice=diceSheet07;diceSheet07=false;CancelControllerDrag();displayPanel=false;debugPanel=false;controllerOrderPanel=false;modal=false;modalIdentity07=null;detailDieId=null;EndSheetDie073();table.HidePreview();main.gameObject.SetActive(true);UiKit.Clear(overlay);if(restoreDice)Refresh();else SyncLockedGripsAfterModal07();
        }
        private void SaveFailure()
        {
            var p=Art07Modal("本次结果尚未保存",480,180,960,720);
            var list=UiKit.Scroll(p,"SaveFailureDetails",24,100,912,392,392);float y=0;
            Art07DetailSection(list,"Message","可以采取的操作","本次结果已经确定，尚未写入存档。规则操作已暂停。\n重试会保存同一个候选结果，不会重新抽骰、刷新商店、扣款或领取奖励。\n放弃将恢复上一已提交检查点。",ref y,880);
            if(saveErrorExpanded07)Art07DetailSection(list,"Error","保存错误",notice,ref y,880);list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            UiKit.Button(p,"ExpandSaveError",saveErrorExpanded07?"收起错误详情":"展开错误详情",24,508,448,60,()=>{saveErrorExpanded07=!saveErrorExpanded07;SaveFailure();});
            UiKit.Button(p,"CopySaveError","复制错误详情",488,508,448,60,()=>{GUIUtility.systemCopyBuffer=notice??"";});
            UiKit.ActionButton(p,"RetrySave","重试保存",24,580,912,60,()=>
            {
                if(!Session.HasPendingSave)return;
                var r=Session.RetrySave();
                if(r.accepted)
                {
                    if(r.receipt!=null)
                    {
                        PrepareTransactionVisual07(r.receipt.command);
                        PrepareSettlementBackdrop07(r.receipt.command);ResolveSettlementBackdrop07(r.receipt.command,r);
                    }
                    CloseModal();if(r.receipt!=null)PresentCommitted(r.receipt.command,r);else Refresh();
                }
                else{notice=r.error;SaveFailure();}
            },UiKit.Gold);
            UiKit.Button(p,"AbandonPendingSave","放弃本次未保存操作并返回检查点",24,648,912,60,()=>
            {
                if(!Session.HasPendingSave)return;
                var r=Session.AbandonPendingSave();
                if(!r.accepted){notice=r.error;SaveFailure();return;}
                DiscardPreparedDiceInteraction07();CloseModal();notice="已返回上一已提交检查点。";Refresh();
            });
        }
        private void ShowLoadIssue07(string detail,bool recoveredBackup)
        { DrawLoadIssue07(detail,recoveredBackup); }
        private void LegacyPause()
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
            if(IsBusy)return;
            DrawDiceSheet07(id,null,null,1);
        }
        private RectTransform OpenDiceSheet07(string title)
        {
            bool fresh=PrepareModalMotion07("dice-sheet");
            sheetDie073=null;sheetFacing073=null;
            modal=true;diceSheet07=true;UiKit.Clear(overlay);table.HidePreview();table.Hide();main.gameObject.SetActive(false);
            // The real 3D preview uses an aperture; opaque screen-space panels cover all other areas.
            var scrim=new Color(.015f,.03f,.035f,.35f);
            UiKit.Box(overlay,"ScrimTop",0,0,1920,208,scrim,true);UiKit.Box(overlay,"ScrimBottom",0,608,1920,472,scrim,true);
            UiKit.Box(overlay,"ScrimLeft",0,208,576,400,scrim,true);UiKit.Box(overlay,"ScrimRight",1032,208,888,400,scrim,true);
            var p=UiKit.Rect(overlay,"Dialog",208,96,1504,888);
            UiKit.Box(p,"PanelTop",0,0,1504,112,UiKit.Panel,true);UiKit.Box(p,"PanelBottom",0,512,1504,376,UiKit.Panel,true);
            UiKit.Box(p,"PanelLeft",0,112,368,400,UiKit.Panel,true);UiKit.Box(p,"PanelRight",824,112,680,400,UiKit.Panel,true);
            UiKit.Outline(p,"Frame",0,0,1504,888,UiKit.Paper,3);
            UiKit.Text(p,"DialogTitle",title,24,16,1360,56,32,UiKit.Paper);
            UiKit.Button(p,"CloseDialog","×",1416,12,64,64,CloseModal);
            FadeModal07(p,fresh,.16f,.10f);
            // A stationary veil reveals the world-space preview through the stationary aperture.
            // Moving/fading the whole aperture would expose a misaligned die outside its opening.
            var veil=UiKit.Box(overlay,"PreviewReveal07",576,208,456,400,UiKit.Panel,false);
            var veilGroup=UiKit.Component<CanvasGroup>(veil.gameObject);
            p.GetComponent<Art07PanelMotion>().SetPreviewReveal(veilGroup,.06f,.10f,fresh,reduced);
            return p;
        }
        private void DrawDiceSheet07(string selectedDie,ShopOffer offer,string selectedFace,float scrollPosition)
        {
            var candidates=ViewState.dice.Where(d=>offer==null||d.lifetime=="permanent").ToArray();
            var die=candidates.FirstOrDefault(d=>d.dieId==selectedDie);
            bool whole=offer!=null&&(offer.contentId=="U05"||offer.contentId=="O04");
            var p=OpenDiceSheet07(offer==null?"骰库 · 永久六面与本次结果":NameOf(offer.contentId)+" · 选择永久"+(whole?"骰":"骰面"));detailDieId=selectedDie;
            UiKit.Text(p,"Source",offer==null?"浏览与旋转不改变骰序、投掷结果或随机数。":"商店 · "+Session.GetOfferPrice(offer)+" 金币 · "+(whole?"作用于整颗永久骰":"仅作用于所选物理面"),24,76,1424,34,19,UiKit.Muted);
            var targets=UiKit.Scroll(p,"ShopTargetList",24,120,320,640,Math.Max(640,candidates.Length*104));
            var scroll=targets.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            for(int i=0;i<candidates.Length;i++)
            {
                var d=candidates[i];string id=d.dieId;
                var button=UiKit.Button(targets,"Target_"+id,"",0,i*104,296,96,()=>DrawDiceSheet07(id,offer,null,scroll.verticalNormalizedPosition),true,id==selectedDie);
                // v0.73: the list shows the real die (drag turns it, a click picks it), not one flat face.
                DieView073(button.transform,"DieThumbnail","sheet-"+id,d,6,4,84,82,.56f,true,false,()=>DrawDiceSheet07(id,offer,null,scroll.verticalNormalizedPosition),null,scroll.viewport);
                UiKit.Text(button.transform,"Identity",id,98,6,186,40,21,UiKit.Ink);
                UiKit.Text(button.transform,"Lifetime",(d.lifetime=="permanent"?"永久":"本关")+(d.isGlass?" · 玻璃":""),98,48,186,40,18,id==selectedDie?UiKit.Ink:PaperMuted071);
            }
            scroll.verticalNormalizedPosition=scrollPosition;
            UiKit.Text(p,"DieCount","活动骰 "+ViewState.dice.Count+" · 永久骰 "+ViewState.dice.Count(d=>d.lifetime=="permanent")+"\n物理对面 F01/F06 · F02/F05 · F03/F04",24,772,320,96,18,UiKit.Muted);
            if(die==null)
            {
                UiKit.Text(p,"TargetHelp","先从左侧选择永久骰。\n选择或取消均不扣款。",400,192,400,160,27,UiKit.Paper);
                UiKit.Button(p,"ConfirmPurchase","请选择目标",1000,800,456,64,null,false);return;
            }
            var current=ViewState.hand?.currentResults.FirstOrDefault(r=>r.dieId==die.dieId);
            bool rolled=current!=null&&current.hasRolled;
            var before=offer==null?Session.ProbabilityUnits(die.dieId):FaceProbability(die);
            var afterDie=die.Clone();
            if(offer!=null&&whole&&offer.contentId=="U05")afterDie.isGlass=true;
            var afterFace=afterDie.faces.FirstOrDefault(f=>f.faceId==selectedFace);
            if(offer!=null&&afterFace!=null)
            {
                switch(offer.contentId)
                {
                    case "U01":afterFace.texture="smooth";break;case "U02":afterFace.texture="rough";break;
                    case "U03":afterFace.baseColor="red";break;case "U04":afterFace.baseColor="blue";break;case "U06":afterFace.isGold=true;break;
                    // U07 has no deterministic candidate: never consume RNG in an inspection screen.
                }
            }
            var after=offer==null?before:FaceProbability(afterDie);
            // v0.73: choosing a face turns the real die to it; the flat cross of face tiles is gone.
            Action<string> select=face=>{TurnSheetToFace073(face);DrawDiceSheet07(die.dieId,offer,face,scroll.verticalNormalizedPosition);};
            BeginSheetDie073(die,(offer?.offerId??"view")+"|"+die.dieId,new Vector2(804,408),before);
            DrawFaceChips073(p,die,rolled?current.currentFaceId:null,selectedFace,select,whole);
            table.ShowPreview(die,sheetCenter073,sheetRotation073,2.15f);
            var hit=UiKit.Component<DiePointer>(UiKit.Box(p,"RotatePreview",368,112,456,400,Color.clear,true).gameObject);
            hit.dragStart=null;hit.dragEnd=null;hit.dragged=DragSheetDie073;
            hit.clicked=whole?(Action<UnityEngine.EventSystems.PointerEventData>)null:e=>{if(table.TryPickPreviewFace073(e.position,out int picked))select(die.faces.First(f=>DiceTable.FaceIndex(f.faceId)==picked).faceId);};
            if(whole)UiKit.Outline(p,"WholeDieTarget",370,114,452,396,UiKit.Gold,3);
            UiKit.Text(p,"PreviewHint",whole?"拖动旋转 · 永久六面":"拖动旋转 · 点骰面可选中",368,520,456,40,20,UiKit.Muted,TextAlignmentOptions.Center);
            var layers=UiKit.Scroll(p,"DieLayers",368,568,456,208,208);float ly=0;
            var snapshot=Session.PreviewSnapshot();var score=Session.PreviewScoreRange()?.minimum;
            int at=snapshot==null?-1:Array.FindIndex(snapshot.dice,d=>d.dieId==die.dieId);
            string effective=at>=0&&score!=null?score.effectivePips[at]+" / "+ColorName07(score.effectiveColors[at]):"未进入提交集合";
            string actual=rolled?current.currentPip+" / "+ColorName07(current.currentColor)+(current.remoteOverride?"（遥控）":""):"未生成";
            string permanent=rolled?die.faces.First(f=>f.faceId==current.currentFaceId).permanentPip.ToString():"六面和 "+die.faces.Sum(f=>f.permanentPip);
            var chosen=die.faces.FirstOrDefault(f=>f.faceId==selectedFace);
            if(offer!=null)
            {
                string change=offer.contentId=="U07"&&chosen!=null?"购买后：在 "+string.Join("、",Enumerable.Range(1,6).Where(n=>n!=chosen.permanentPip))+" 中等概率取一个。颜色、纹理、镀金保持；不预抽结果。":
                    whole?offer.contentId=="O04"?"购买后：移除此骰及全部强化；提交容量保持。至少保留 1 颗永久骰。":"购买后：整骰玻璃。每次实际参与投掷有 15% 永久损坏风险；锁定时不判定。":
                    afterFace==null?"请选择右侧的物理面。":"当前："+FaceSummary07(chosen)+"\n购买后："+FaceSummary07(afterFace);
                Art07DetailSection(layers,"PurchasePreview","改造预览",change,ref ly,424);
            }
            Art07DetailSection(layers,"Layers","永久 → 当前 → 有效",permanent+" → "+actual+" → "+effective,ref ly,424);
            if(chosen!=null)Art07DetailSection(layers,"SelectedFace",chosen.faceId+" · 永久属性",FaceSummary07(chosen)+(die.isGlass?" · 玻璃":""),ref ly,424);
            if(offer==null)
            {
                string sources="永久纹理、"+(ViewState.hand?.largeBuff==true?"大数多多、":"")+(ViewState.hand?.smallBuff==true?"小数多多、":"")+string.Join("、",ViewSources.Where(c=>c.abilityId=="C34"||c.abilityId=="C35").Select(c=>NameOf(c.abilityId)));
                Art07DetailSection(layers,"ProbabilitySources","概率来源",sources.TrimEnd('、')+"。当前表已经包含生效加权。",ref ly,424);
                if(at>=0&&score!=null)
                {
                    var valueSources=new List<string>();
                    if(current.remoteOverride)valueSources.Add("遥控骰子：当前点数覆盖；永久面与颜色保持。");
                    if(score.effectivePips[at]!=current.currentPip&&ViewSources.Any(source=>source.abilityId=="C12"))valueSources.Add(NameOf("C12")+"："+Description("C12"));
                    if(score.effectiveColors[at]!=current.currentColor)
                    {
                        if(ViewState.hand.redWave)valueSources.Add(NameOf("I01")+"："+Description("I01"));
                        if(at==0&&ViewSources.Any(source=>source.abilityId=="C03"))valueSources.Add(NameOf("C03")+"："+Description("C03"));
                    }
                    if(valueSources.Count>0)Art07DetailSection(layers,"ValueSources","本次值转换",string.Join("\n",valueSources),ref ly,424);
                    var trace=ScoreExplanation.Build(snapshot,score,Config);
                    Art07DetailSection(layers,"EffectiveSources","计分来源",string.Join("\n",trace.dice[at].steps.Select(ScoreStepText)),ref ly,424);
                }
            }
            layers.sizeDelta=new Vector2(layers.sizeDelta.x,ly+12);
            UiKit.Button(p,"ProbabilityGroups",faceProbabilityExpanded07?"按物理面 · 收起":"按点数组 · 展开",848,504,600,60,()=>{faceProbabilityExpanded07=!faceProbabilityExpanded07;DrawDiceSheet07(die.dieId,offer,selectedFace,scroll.verticalNormalizedPosition);});
            DrawProbability07(p,die,afterDie,before,after,offer!=null&&selectedFace!=null&&offer.contentId!="U07");
            if(offer==null)UiKit.Button(p,"DoneDiceDetail","完成查看",848,800,600,64,CloseModal);
            else
            {
                bool targetReady=whole||selectedFace!=null;
                string error=targetReady?Session.PurchaseError(offer.offerId,die.dieId,whole?null:selectedFace):"请选择一个物理面。";
                bool same=targetReady&&offer.category=="die_upgrade"&&!Session.IsUpgradeTargetLegal(offer.contentId,die.dieId,whole?null:selectedFace);
                string label=same?"已拥有此强化":!targetReady?"请选择目标面":offer.contentId=="O04"?"支付 "+Session.GetOfferPrice(offer)+" 并移除":"支付 "+Session.GetOfferPrice(offer)+" 并强化";
                var reason=UiKit.Scroll(p,"TargetPurchaseReason",368,792,456,72,72);
                var reasonText=UiKit.Text(reason,"PurchaseUnavailable",error??"余额 "+ViewState.gold+" → "+((System.Numerics.BigInteger)ViewState.gold-Session.GetOfferPrice(offer)),0,0,420,64,19,error==null?UiKit.Muted:UiKit.Gold);reason.sizeDelta=new Vector2(reason.sizeDelta.x,FitHeight(reasonText,64));
                UiKit.ActionButton(p,"ConfirmPurchase",label,848,800,600,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId,targetDieId=die.dieId,targetFaceId=whole?null:selectedFace});},UiKit.Gold,error==null&&!IsBusy);
                if(offer.contentId=="O04")UiKit.Outline(p,"RemoveDieWarning",848,800,600,64,UiKit.Red,3);
            }
        }
        private void ShowNewDieOffer07(ShopOffer offer)
        {
            var die=offer.die;var p=OpenDiceSheet07("加入骰子 · 已生成的固定候选");
            UiKit.Text(p,"Source","商店 · 永久骰 "+ViewState.dice.Count(d=>d.lifetime=="permanent")+" → "+(ViewState.dice.Count(d=>d.lifetime=="permanent")+1)+" · 提交容量保持 K="+Session.EffectiveCapacity,24,76,1424,34,19,UiKit.Muted);
            var attributes=UiKit.Scroll(p,"CandidateFaces",24,120,320,640,640);float y=0;
            foreach(var face in die.faces)Art07DetailSection(attributes,face.faceId,face.faceId,FaceSummary07(face),ref y,288);
            if(offer.priceReasons!=null&&offer.priceReasons.Length>0)Art07DetailSection(attributes,"PriceReasons","价格来源",string.Join(" · ",offer.priceReasons),ref y,288);
            attributes.sizeDelta=new Vector2(attributes.sizeDelta.x,y+16);
            BeginSheetDie073(die,"candidate|"+offer.offerId,new Vector2(804,408),FaceProbability(die));
            DrawFaceChips073(p,die,null,null,null,false);DrawProbability07(p,die,die,FaceProbability(die),FaceProbability(die),false);
            UiKit.Button(p,"ProbabilityGroups",faceProbabilityExpanded07?"按物理面 · 收起":"按点数组 · 展开",848,504,600,60,()=>{faceProbabilityExpanded07=!faceProbabilityExpanded07;ShowNewDieOffer07(offer);});
            table.ShowPreview(die,sheetCenter073,sheetRotation073,2.15f);
            var hit=UiKit.Component<DiePointer>(UiKit.Box(p,"RotatePreview",368,112,456,400,Color.clear,true).gameObject);
            hit.clicked=null;hit.dragStart=null;hit.dragEnd=null;hit.dragged=DragSheetDie073;
            UiKit.Text(p,"CandidateNotice","拖动旋转 · 候选永久六面\n当前值：未生成\n查看和取消不会重新生成候选。",368,528,456,208,23,UiKit.Muted);
            string error=Session.PurchaseError(offer.offerId);int price=Session.GetOfferPrice(offer);
            var reason=UiKit.Scroll(p,"CandidatePurchaseReason",368,760,456,104,104);
            var reasonText=UiKit.Text(reason,"PurchaseUnavailable",error??"价格 "+price+" 金币\n余额 "+ViewState.gold+" → "+((System.Numerics.BigInteger)ViewState.gold-price),0,0,424,100,21,error==null?UiKit.Gold:UiKit.Muted);reason.sizeDelta=new Vector2(reason.sizeDelta.x,FitHeight(reasonText,100));
            UiKit.ActionButton(p,"ConfirmPurchase","支付 "+price+" 并加入",848,800,600,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId});},UiKit.Gold,error==null&&!IsBusy);
        }
        private static string ColorName07(string color)=>color=="red"?"红 R":"蓝 B";
        private static string FaceSummary07(FaceState f)=>f.permanentPip+"点 · "+ColorName07(f.baseColor)+" · "+(f.texture=="smooth"?"光滑":f.texture=="rough"?"粗糙":"无纹理")+(f.isGold?" · 黄金":"");
        private void DrawFaceNet07(Transform parent,DieInstance die,string currentFace,string selectedFace,Action<string> select)
        {
            string[] ids={"F01","F04","F02","F03","F06","F05"};int[] cols={1,0,1,2,1,1},rows={0,1,1,1,2,3};
            for(int i=0;i<ids.Length;i++)
            {
                string id=ids[i];var face=die.faces.First(f=>f.faceId==id);float x=1008+cols[i]*96,y=112+rows[i]*96;
                var tile=Art07PipTile(parent,"Face_"+id,face.permanentPip,face.baseColor=="red"?UiKit.Red:UiKit.Blue,x,y,88);
                foreach(RectTransform child in tile)if(child.gameObject.activeSelf&&child.name.StartsWith("Pip"))child.anchoredPosition=new Vector2(child.anchoredPosition.x,child.anchoredPosition.y*.7f-20);
                UiKit.Text(tile,"ID",id,4,0,80,30,18,UiKit.Ink,TextAlignmentOptions.Center);
                if(face.isGold)UiKit.Outline(tile,"GoldRim",5,5,78,78,UiKit.Gold,2);
                if(id==currentFace)UiKit.Outline(tile,"CurrentFace",-4,-4,96,96,UiKit.Paper,3);
                if(id==selectedFace)for(int corner=0;corner<4;corner++)
                {
                    float cx=corner%2==0?-6:78,cy=corner<2?-6:90;
                    UiKit.Box(tile,"SelectedH"+corner,cx,cy,16,4,UiKit.Gold);
                    UiKit.Box(tile,"SelectedV"+corner,corner%2==0?-6:90,corner<2?-6:78,4,16,UiKit.Gold);
                }
                var button=UiKit.Component<UnityEngine.UI.Button>(tile.gameObject);button.enabled=select!=null;button.interactable=select!=null;button.onClick.RemoveAllListeners();
                button.targetGraphic=tile.GetComponent<UnityEngine.UI.Image>();button.targetGraphic.raycastTarget=true;if(select!=null)button.onClick.AddListener(()=>select(id));
            }
            UiKit.Text(parent,"FaceLegend","纸白框：当前朝上\n金角标：已选物理面",848,112,152,200,18,UiKit.Muted);
        }
        private void DrawProbability07(Transform parent,DieInstance die,DieInstance afterDie,int[] before,int[] after,bool comparison)
        {
            var list=UiKit.Scroll(parent,"FaceProbabilities",848,576,600,200,396);int row=0;
            Action<string,int,int> entry=(label,oldUnits,newUnits)=>
            {
                float y=row++*64;
                UiKit.Text(list,"ProbabilityLabel"+row,label,0,y,120,36,19,UiKit.Paper);
                string value=(oldUnits/10000m).ToString("0.####")+"%"+(comparison?" → "+(newUnits/10000m).ToString("0.####")+"%":"");
                UiKit.Text(list,"ProbabilityValue"+row,value,124,y,444,36,19,UiKit.Paper,TextAlignmentOptions.TopRight);
                UiKit.Box(list,"EmptyBar"+row,0,y+42,568,8,UiKit.Ink);
                UiKit.Box(list,"ProbabilityBar"+row,0,y+42,568f*newUnits/Config.probabilityUnits,8,UiKit.Teal);
            };
            if(faceProbabilityExpanded07)for(int i=0;i<die.faces.Length;i++)entry(die.faces[i].faceId,before[i],after[i]);
            else for(int n=1;n<=6;n++)entry(n+" 点",die.faces.Select((f,i)=>f.permanentPip==n?before[i]:0).Sum(),afterDie.faces.Select((f,i)=>f.permanentPip==n?after[i]:0).Sum());
            list.sizeDelta=new Vector2(list.sizeDelta.x,row*64);
        }
        private void ShowBreakdownComplete07(SubmissionSnapshot snapshot,ScoreBreakdown b)
        {
            if(snapshot==null||b==null)return;
            var p=OpenModal("分数来源 · 只读快照");
            var receipt=ViewState.processedReceipts.LastOrDefault(r=>r.command!=null&&r.command.kind==CommandKind.Submit);
            bool historical=ViewState.lastSubmission!=null&&snapshot.handId==ViewState.lastSubmission.snapshot.handId;
            if(historical&&receipt07!=null)UiKit.Button(p,"ReplayReceipt","回放记录",1130,24,320,58,()=>{CloseModal();ReplayLastPresentation07();},!IsBusy);
            if(historical&&HasCommittedPresentationDetails07(snapshot.handId))UiKit.Button(p,"ReceiptEffectsDetails","表现与后置记录",760,24,350,60,ShowCommittedPresentationDetails07,!IsBusy);
            string effects=historical&&receipt!=null?string.Join("\n",receipt.effects.Select(EffectText)):string.Join("\n",Session.PreviewSubmissionEffects().Select(EffectText));
            var trace=ScoreExplanation.Build(snapshot,b,Config);
            float diceHeight=trace.dice.Sum(d=>86+d.steps.Length*34);
            float globalHeight=58+trace.global.Length*46;
            var list=UiKit.Scroll(p,"ScoreDetail",40,110,1500,560,Math.Max(620,110+diceHeight+globalHeight+280));
            var formula=UiKit.Text(list,"Formula",ScoreFormula07(b),12,6,1440,80,29,UiKit.Gold);
            float cursor=6+FitHeight(formula,80)+20;
            for(int i=0;i<snapshot.dice.Length;i++)
            {
                var d=snapshot.dice[i];var f=d.permanentFacesCopy.First(x=>x.faceId==d.faceId);
                var dieLine=UiKit.Text(list,"DieLine"+i,$"{i+1}. {d.dieId}/{d.faceId}  永久 {f.permanentPip} → 当前 {d.currentPip} → 有效 {b.effectivePips[i]}  /  {(b.effectiveColors[i]=="red"?"红 R":"蓝 B")}\n永久六面和 {d.permanentSixFaceSum}  ·  本骰点数贡献 {b.pipContributions[i]}",12,cursor,1440,70,25);
                // Both source lines use the current font's real preferred height, including accessibility scaling.
                cursor+=FitHeight(dieLine,70)+2;
                for(int j=0;j<trace.dice[i].steps.Length;j++)
                {
                    var line=UiKit.Text(list,"DieStep"+i+"_"+j,ScoreStepText(trace.dice[i].steps[j]),40,cursor,1410,33,23,UiKit.Muted);cursor+=FitHeight(line,33)+4;
                }
                cursor+=14;
            }
            var globalTitle=UiKit.Text(list,"GlobalTitle","全局结算 · 中间不取整",12,cursor,1440,48,28,UiKit.Gold);cursor+=FitHeight(globalTitle,48)+10;
            for(int i=0;i<trace.global.Length;i++){var line=UiKit.Text(list,"GlobalStep"+i,ScoreStepText(trace.global[i]),12,cursor,1440,43,25);cursor+=FitHeight(line,43)+5;}
            string triggered=b.triggeredControllerIds.Length==0?"本次无控制器触发":string.Join(" · ",b.triggeredControllerIds.Select(NameOf));
            var footer=UiKit.Text(list,"Triggered",triggered+"\n永久复制/扩散只在提交后执行，不回溯更改本次分数。"+(effects.Length>0?(historical?"\n\n实际提交后效果：\n":"\n\n确认提交将执行：\n")+effects:""),12,cursor+12,1440,260,25,UiKit.Teal);
            list.sizeDelta=new Vector2(list.sizeDelta.x,Math.Max(620,cursor+FitHeight(footer,260)+36));
            UiKit.Button(p,"ScoreDrawerBack","返回三列抽屉",40,700,500,60,()=>ShowBreakdown(snapshot,b));
            UiKit.Button(p,"CopyExactScore","复制精确分数与公式",1040,700,500,60,()=>
            {
                GUIUtility.systemCopyBuffer=formula.text;
                var label=p.Find("CopyExactScore")?.GetComponentInChildren<TextMeshProUGUI>();if(label!=null)label.text="已复制精确分数与公式";
            });
        }
        private static float FitHeight(TextMeshProUGUI label,float minimum)
        {
            float height=Math.Max(minimum,label.GetPreferredValues(label.text,label.rectTransform.rect.width,float.PositiveInfinity).y+4);
            label.rectTransform.sizeDelta=new Vector2(label.rectTransform.sizeDelta.x,height);return height;
        }
        private string ScoreStepSource07(ScoreStep step)
        {
            string label=step.sourceId!=null?NameOf(step.sourceId):step.kind=="glass"?"玻璃":step.kind=="gold"?"本面镀金":step.kind=="unused_rolls"?"剩余投掷奖励":step.kind=="hand"?"牌型总倍率":step.kind=="color"?"独立颜色乘区":step.kind=="red_group"?"同点红组":step.kind=="floor"?"最后向下取整":"最终加分";
            if(step.kind=="enchantment_base"||step.kind=="enchantment_multiplier")label+=" · 附魔";
            if(!string.IsNullOrEmpty(step.emitterId))label+=" ["+step.emitterId+(step.stateOwnerId!=step.emitterId&&!string.IsNullOrEmpty(step.stateOwnerId)?" → "+step.stateOwnerId:"")+"]";
            return label;
        }
        private string ScoreStepText(ScoreStep step)
        {
            string label=ScoreStepSource07(step),before=step.before,after=step.after;
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
            var chosen=new List<string>();var pips=new Dictionary<string,int>();float targetScrollPosition=1;
            Action draw=null;
            draw=()=>
            {
                bool fresh=PrepareModalMotion07("remote-targets");
                modal=true;UiKit.Clear(overlay);table.HidePreview();main.gameObject.SetActive(true);
                UiKit.Box(overlay,"RemoteScrim",0,0,1920,1080,new Color(.015f,.03f,.035f,.3f),true);
                var unlocked=ViewState.dice.Where(d=>ViewState.hand==null||!ViewState.hand.orderedLockedIds.Contains(d.dieId)).ToArray();
                Action<string> choose=id=>
                {
                    if(chosen.Contains(id)){chosen.Remove(id);pips.Remove(id);}else if(chosen.Count<2){chosen.Add(id);pips[id]=6;}
                    draw();
                };
                // The overlay admits only unlocked target hot zones; gameplay actions beneath remain blocked.
                foreach(var d in unlocked)
                {
                    if(!dieCenters.TryGetValue(d.dieId,out var center))continue;string id=d.dieId;
                    var hot=UiKit.Box(overlay,"RemoteBoard"+id,center.x-56,center.y-56,112,112,Color.clear,true);
                    var button=UiKit.Component<UnityEngine.UI.Button>(hot.gameObject);button.enabled=true;button.interactable=true;button.targetGraphic=hot.GetComponent<UnityEngine.UI.Image>();button.onClick.RemoveAllListeners();button.onClick.AddListener(()=>choose(id));
                    UiKit.Outline(hot,"TargetEdge",0,0,112,112,chosen.Contains(id)?UiKit.Gold:UiKit.Paper,chosen.Contains(id)?4:2);
                }
                var p=UiKit.Frame(overlay,"Dialog",496,496,1280,400,UiKit.Panel,UiKit.Paper,true);
                UiKit.Text(p,"DialogTitle","遥控骰子 · 选择两颗未锁骰",24,8,1136,48,27,UiKit.Paper);
                UiKit.Button(p,"CloseDialog","×",1196,8,60,60,CloseModal);
                FadeModal07(p,fresh);
                UiKit.Text(p,"RemoteRule","消耗本道具及 1 次投掷，全部未锁骰照常投掷。\n玻璃可能损坏；目标损坏不补选、不退还。颜色与物理面照常抽取。",24,64,1232,64,17,UiKit.Muted);
                // The single visible row must align after every wheel tick and at both ends.
                // Keep an 8-reference-pixel inter-row gap; do not count a trailing gap as content.
                const float targetHeight=64,targetPitch=72;
                int targetRows=(unlocked.Length+7)/8;
                var targets=UiKit.Scroll(p,"RemoteTargetList",24,136,1232,targetHeight,Math.Max(targetHeight,targetRows*targetPitch-(targetPitch-targetHeight)));
                var scroll=targets.GetComponentInParent<UnityEngine.UI.ScrollRect>();scroll.scrollSensitivity=targetPitch;
                for(int i=0;i<unlocked.Length;i++)
                {
                    string id=unlocked[i].dieId;
                    UiKit.Button(targets,"RemoteDie"+id,id,(i%8)*150,(i/8)*targetPitch,144,targetHeight,()=>{targetScrollPosition=scroll.verticalNormalizedPosition;choose(id);},true,chosen.Contains(id));
                }
                scroll.verticalNormalizedPosition=targetScrollPosition;
                for(int slot=0;slot<2;slot++)
                {
                    float x=24+slot*624;string id=slot<chosen.Count?chosen[slot]:null;
                    UiKit.Text(p,id==null?"EmptyRemoteSlot"+slot:"Chosen"+id,"目标 "+(slot+1)+" · "+(id??"请选择未锁骰"),x,208,600,36,20,id==null?UiKit.Muted:UiKit.Gold);
                    for(int n=1;n<=6;n++)
                    {
                        int pip=n;bool odd=ViewSources.Any(source=>source.abilityId=="C12")&&pip%2==1;
                        UiKit.Button(p,id==null?"EmptyRemotePip"+slot+"_"+n:"RemotePip"+id+"_"+n,pip+(odd?"→"+(pip+1):""),x+(n-1)*96,248,88,64,()=>{if(id==null)return;targetScrollPosition=scroll.verticalNormalizedPosition;pips[id]=pip;draw();},id!=null,id!=null&&pips[id]==pip);
                    }
                }
                UiKit.Text(p,"RemoteSelection","已选 "+chosen.Count+"/2 · 上方目标栏可滚动\n数字箭头表示控制器转换后的有效值",24,324,576,68,17,UiKit.Muted);
                UiKit.ActionButton(p,"RemoteConfirm","使用并投掷",648,328,608,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.UseItem,itemInstanceId=item.instanceId,dieIds=chosen.ToArray(),targetPips=chosen.Select(id=>pips[id]).ToArray()});},UiKit.Blue,chosen.Count==2&&!IsBusy&&Session.PendingIds.Length==0);
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
