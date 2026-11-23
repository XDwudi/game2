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
        private RectTransform OpenModal(string title,bool dicePreview=false,string id=null)
        {
            bool fresh=PrepareModalMotion07(id??title);
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
            UiKit.Role(UiKit.Text(p,"DialogTitle",title,40,Large11?18:24,1390,Large11?80:62,37),TextRole.ScreenTitle);
            UiKit.Button(p,"CloseDialog","×",1490,24,70,60,CloseModal);
            bool error=id=="save-failure16"||id=="load-issue16",manual=id=="handbook16";
            FadeModal07(p,fresh,error?.12f:manual?.14f:.16f,error||manual||id=="pause16"?.10f:.12f);
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
            var p=Art07Modal("本次结果尚未保存",480,180,960,720,"save-failure16");
            var list=UiKit.Scroll(p,"SaveFailureDetails",24,100,912,392,392);float y=0;
            ModalSection16(list,p,"Message","可以采取的操作","这一步的结果还没存进存档（可能磁盘已满或没有写入权限）。\n“重试保存”会保存同一个结果，不会重新掷骰或重复扣钱。\n“放弃”会回到上次保存的进度。",ref y,880);
            if(saveErrorExpanded07)ModalSection16(list,p,"Error","保存错误",notice,ref y,880);list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            CenterButtonLabel16(UiKit.Button(p,"ExpandSaveError",saveErrorExpanded07?"收起错误详情":"展开错误详情",24,508,448,60,()=>{saveErrorExpanded07=!saveErrorExpanded07;SaveFailure();}));
            CenterButtonLabel16(UiKit.Button(p,"CopySaveError","复制错误详情",488,508,448,60,()=>{GUIUtility.systemCopyBuffer=notice??"";}));
            Primary16(p,"RetrySave","重试保存",24,580,912,60,()=>
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
            });
            UiKit.Button(p,"AbandonPendingSave","放弃这一步，回到上次保存",24,648,912,60,()=>
            {
                if(!Session.HasPendingSave)return;
                var r=Session.AbandonPendingSave();
                if(!r.accepted){notice=r.error;SaveFailure();return;}
                DiscardPreparedDiceInteraction07();CloseModal();notice="已回到上次保存的进度。";Refresh();
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
            UiKit.Role(UiKit.Text(p,"DialogTitle",title,24,Large11?0:16,1360,Large11?70:56,32,UiKit.Paper),TextRole.ScreenTitle);
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
            var p=OpenDiceSheet07(offer==null?"你的骰子":NameOf(offer.contentId)+(whole?" · 选择一颗骰子":" · 选择要改造的面"));detailDieId=selectedDie;
            UiKit.Text(p,"Source",offer==null?"拖动可以旋转骰子。":(string.IsNullOrEmpty(offer.groupId)?Session.GetOfferPrice(offer)+" 金币":"免费改造")+" · "+(whole?"改造整颗骰子":"只改造选中的这一面"),24,Large11?67:76,1424,Large11?46:34,19.5f,UiKit.Muted);
            var targets=UiKit.Scroll(p,"ShopTargetList",24,120,320,640,Math.Max(640,candidates.Length*104));
            var scroll=targets.GetComponentInParent<UnityEngine.UI.ScrollRect>();
            for(int i=0;i<candidates.Length;i++)
            {
                var d=candidates[i];string id=d.dieId;
                var button=UiKit.Button(targets,"Target_"+id,"",0,i*104,296,96,()=>DrawDiceSheet07(id,offer,null,scroll.verticalNormalizedPosition),true,id==selectedDie);
                // v0.73: the list shows the real die (drag turns it, a click picks it), not one flat face.
                DieView073(button.transform,"DieThumbnail","sheet-"+id,d,6,4,84,82,.56f,true,false,()=>DrawDiceSheet07(id,offer,null,scroll.verticalNormalizedPosition),null,scroll.viewport);
                UiKit.Text(button.transform,"Identity",DieName11(id),98,6,186,40,21,UiKit.Ink);
                UiKit.Text(button.transform,"Lifetime",(d.lifetime=="permanent"?"永久":"临时（本关）")+(d.isGlass?" · 玻璃":""),98,48,186,40,18,id==selectedDie?UiKit.Ink:PaperMuted071);
            }
            scroll.verticalNormalizedPosition=scrollPosition;
            // v1.1 (W09): no face ids; the opposite faces are already listed under the face buttons.
            UiKit.Text(p,"DieCount","共 "+ViewState.dice.Count+" 颗（永久 "+ViewState.dice.Count(d=>d.lifetime=="permanent")+" 颗）",24,772,320,96,18,UiKit.Muted);
            if(die==null)
            {
                UiKit.Text(p,"TargetHelp","先在左边选一颗骰子。\n选择和取消都不花钱。",400,192,400,160,27,UiKit.Paper);
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
                    // p1.0.0: carve, plane, weight and mirror are deterministic, so they can be previewed.
                    case "U08":afterFace.permanentPip=Math.Min(Config.maxPipValue,afterFace.permanentPip+1);break;case "U09":afterFace.permanentPip=Math.Max(Config.minPipValue,afterFace.permanentPip-1);break;
                    case "U10":afterFace.baseWeight=Math.Min(Config.maxFaceWeight,afterFace.baseWeight+1);break;
                    case "U11":{var opposite=afterDie.faces.First(f=>f.faceId==Config.Opposite(afterFace.faceId));opposite.permanentPip=afterFace.permanentPip;opposite.baseColor=afterFace.baseColor;break;}
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
            UiKit.Text(p,"PreviewHint",whole?"拖动可以旋转骰子。":"拖动旋转 · 点击骰面选择",368,520,456,40,20,UiKit.Muted,TextAlignmentOptions.Center);
            var layers=UiKit.Scroll(p,"DieLayers",368,568,456,208,208);float ly=0;
            var snapshot=Session.PreviewSnapshot();var score=Session.PreviewScoreRange()?.minimum;
            int at=snapshot==null?-1:Array.FindIndex(snapshot.dice,d=>d.dieId==die.dieId);
            bool scoring=at>=0&&score!=null;
            string layersText=rolled?DiceLayers16(die.faces.First(f=>f.faceId==current.currentFaceId),
                current.currentPip,current.currentColor,scoring?(int?)score.effectivePips[at]:null,
                scoring?score.effectiveColors[at]:null):"还没掷";
            var chosen=die.faces.FirstOrDefault(f=>f.faceId==selectedFace);
            if(offer!=null)
            {
                string change=offer.contentId=="U07"&&chosen!=null?"改造后这一面随机变成 "+string.Join("、",Enumerable.Range(Config.minPipValue,Config.maxPipValue-Config.minPipValue+1).Where(n=>n!=chosen.permanentPip))+" 之一（颜色、纹理、黄金不变）。":
                    whole?offer.contentId=="O04"?"删除这颗骰子（连同改造），提交栏格数不变。至少要留 1 颗。":"整颗变成玻璃骰（计分 ×"+Config.glassContributionMultiplier+"）。每次掷到它有 "+Session.GlassBreakPercentIn(ViewState)+"% 概率碎掉，锁定后不会碎。":
                    afterFace==null?"请在右边选一个面。":offer.contentId=="U11"?"对面（"+FaceName11(Config.Opposite(chosen.faceId))+"）现在："+FaceSummary07(die.faces.First(f=>f.faceId==Config.Opposite(chosen.faceId)))+"\n改造后："+FaceSummary07(afterDie.faces.First(f=>f.faceId==Config.Opposite(chosen.faceId))):"当前："+FaceSummary07(chosen)+"\n改造后："+FaceSummary07(afterFace);
                Art07DetailSection(layers,"PurchasePreview","改造预览",change,ref ly,424);
            }
            Art07DetailSection(layers,"Layers","点数",layersText,ref ly,424);
            if(chosen!=null)Art07DetailSection(layers,"SelectedFace",FaceName11(chosen.faceId),FaceSummary07(chosen)+(die.isGlass?" · 玻璃":""),ref ly,424);
            if(offer==null)
            {
                var odds=new List<string>{"骰面权重"};
                if(die.faces.Any(f=>f.texture=="smooth"||f.texture=="rough"))odds.Add("骰面纹理");
                if(ViewState.hand?.largeBuff==true&&die.faces.Any(f=>f.permanentPip==Config.maxPipValue))odds.Add(NameOf("I04"));
                if(ViewState.hand?.smallBuff==true&&die.faces.Any(f=>f.permanentPip==Config.minPipValue))odds.Add(NameOf("I05"));
                int bias=ViewSources.Count(c=>c.abilityId=="C34")-ViewSources.Count(c=>c.abilityId=="C35");
                if(bias!=0)
                {
                    string source=bias>0?"C34":"C35";var biasedPips=Config.Controller(source).parameters.targetPips;
                    if(die.faces.Any(f=>biasedPips.Contains(f.permanentPip))&&die.faces.Any(f=>!biasedPips.Contains(f.permanentPip)))odds.Add(NameOf(source));
                }
                Art07DetailSection(layers,"ProbabilitySources","影响概率",string.Join("、",odds)+"（下表已计入）",ref ly,424);
                var valueSources=new List<string>();
                if(rolled&&current.remoteOverride)valueSources.Add("遥控骰子指定了点数（骰面本身不变）");
                if(scoring)
                {
                    if(score.effectivePips[at]!=current.currentPip&&ViewSources.Any(source=>source.abilityId=="C12"))valueSources.Add(NameOf("C12")+"："+ShortText11("C12"));
                    string intermediate=current.currentColor;
                    if(at==Config.Controller("C03").parameters.index && intermediate!="red" && ViewSources.Any(source=>source.abilityId=="C03"))
                    {
                        valueSources.Add(NameOf("C03")+"："+ShortText11("C03"));
                        intermediate="red";
                    }
                    if(snapshot.redWave && intermediate!="red")valueSources.Add(NameOf("I01")+"："+ShortText11("I01"));
                    if(snapshot.blueWave && intermediate!="blue")valueSources.Add(NameOf("I10")+"："+ShortText11("I10"));
                    var trace=ScoreExplanation.Build(snapshot,score,Config);
                    Art07DetailSection(layers,"EffectiveSources","计分来源",string.Join("\n",trace.dice[at].steps.Select(step=>ScoreStepText(step,snapshot))),ref ly,424);
                }
                if(valueSources.Count>0)Art07DetailSection(layers,"ValueSources","这次被改动",string.Join("\n",valueSources),ref ly,424);
            }
            layers.sizeDelta=new Vector2(layers.sizeDelta.x,ly+12);
            UiKit.Button(p,"ProbabilityGroups",faceProbabilityExpanded07?"按点数合并显示":"按六个面分别显示",848,504,600,60,()=>{faceProbabilityExpanded07=!faceProbabilityExpanded07;DrawDiceSheet07(die.dieId,offer,selectedFace,scroll.verticalNormalizedPosition);});
            DrawProbability07(p,die,afterDie,before,after,offer!=null&&selectedFace!=null&&offer.contentId!="U07");
            if(offer==null)UiKit.Button(p,"DoneDiceDetail","关闭",848,800,600,64,CloseModal);
            else
            {
                bool targetReady=whole||selectedFace!=null;
                string error=targetReady?Session.PurchaseError(offer.offerId,die.dieId,whole?null:selectedFace):"请选一个面。";
                bool same=targetReady&&offer.category=="die_upgrade"&&!Session.IsUpgradeTargetLegal(offer.contentId,die.dieId,whole?null:selectedFace);
                string label=same?"这个目标不能再改造":!targetReady?"请选一个面。":offer.contentId=="O04"?"花 "+Session.GetOfferPrice(offer)+" 金币删除":!string.IsNullOrEmpty(offer.groupId)?"免费改造":"花 "+Session.GetOfferPrice(offer)+" 金币改造";
                var reason=UiKit.Scroll(p,"TargetPurchaseReason",368,792,456,72,72);
                var reasonText=UiKit.Text(reason,"PurchaseUnavailable",error??"金币 "+PlayerNumberFormat.Gold(ViewState.gold)+" → "+PlayerNumberFormat.Integer((System.Numerics.BigInteger)ViewState.gold-Session.GetOfferPrice(offer)),0,0,420,64,19,error==null?UiKit.Muted:UiKit.Gold);reason.sizeDelta=new Vector2(reason.sizeDelta.x,FitHeight(reasonText,64));
                UiKit.ActionButton(p,"ConfirmPurchase",label,848,800,600,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId,targetDieId=die.dieId,targetFaceId=whole?null:selectedFace});},UiKit.Gold,error==null&&!IsBusy);
                if(offer.contentId=="O04")UiKit.Outline(p,"RemoveDieWarning",848,800,600,64,UiKit.Red,3);
            }
        }
        private void ShowNewDieOffer07(ShopOffer offer)
        {
            var die=offer.die;bool gift=!string.IsNullOrEmpty(offer.groupId);var p=OpenDiceSheet07(gift?"领取骰子":"购买骰子");
            UiKit.Text(p,"Source",(gift?"领取后：":"买下后：")+"骰子 "+ViewState.dice.Count(d=>d.lifetime=="permanent")+" → "+(ViewState.dice.Count(d=>d.lifetime=="permanent")+1)+" 颗（提交栏仍是 "+Session.EffectiveCapacity+" 格）",24,Large11?67:76,1424,Large11?46:34,19.5f,UiKit.Muted);
            var attributes=UiKit.Scroll(p,"CandidateFaces",24,120,320,640,640);float y=0;
            foreach(var face in die.faces)Art07DetailSection(attributes,face.faceId,FaceName11(face.faceId),FaceSummary07(face),ref y,288);
            if(offer.priceReasons!=null&&offer.priceReasons.Length>0)Art07DetailSection(attributes,"PriceReasons","价格构成",string.Join(" · ",offer.priceReasons),ref y,288);
            attributes.sizeDelta=new Vector2(attributes.sizeDelta.x,y+16);
            BeginSheetDie073(die,"candidate|"+offer.offerId,new Vector2(804,408),FaceProbability(die));
            DrawFaceChips073(p,die,null,null,null,false);DrawProbability07(p,die,die,FaceProbability(die),FaceProbability(die),false);
            UiKit.Button(p,"ProbabilityGroups",faceProbabilityExpanded07?"按点数合并显示":"按六个面分别显示",848,504,600,60,()=>{faceProbabilityExpanded07=!faceProbabilityExpanded07;ShowNewDieOffer07(offer);});
            table.ShowPreview(die,sheetCenter073,sheetRotation073,2.15f);
            var hit=UiKit.Component<DiePointer>(UiKit.Box(p,"RotatePreview",368,112,456,400,Color.clear,true).gameObject);
            hit.clicked=null;hit.dragStart=null;hit.dragEnd=null;hit.dragged=DragSheetDie073;
            var notice=UiKit.Text(p,"CandidateNotice","拖动旋转查看六个面",368,528,456,208,23,UiKit.Muted);
            if(!gift)
            {
                // Keep the gesture hint at its original coordinates. Normal shop candidates additionally
                // explain O03; reward dice have a different source and keep their own acquisition flow.
                float hintHeight=FitHeight(notice,0);
                float viewHeight=208-hintHeight-8;
                var content=UiKit.Scroll(p,"CandidateRulesScroll",368,528+hintHeight+8,456,viewHeight,viewHeight);
                var rules=UiKit.Text(content,"CandidateRules",PlayerRules11("O03"),0,0,428,viewHeight,20,UiKit.Muted);
                float rulesHeight=FitHeight(rules,0);
                content.sizeDelta=new Vector2(content.sizeDelta.x,Mathf.Max(viewHeight,rulesHeight));
            }
            string error=Session.PurchaseError(offer.offerId);int price=Session.GetOfferPrice(offer);
            var reason=UiKit.Scroll(p,"CandidatePurchaseReason",368,760,456,104,104);
            var reasonText=UiKit.Text(reason,"PurchaseUnavailable",error??(gift?"免费领取":"金币 "+PlayerNumberFormat.Gold(ViewState.gold)+" → "+PlayerNumberFormat.Integer((System.Numerics.BigInteger)ViewState.gold-price)),0,0,424,100,21,error==null?UiKit.Gold:UiKit.Muted);reason.sizeDelta=new Vector2(reason.sizeDelta.x,FitHeight(reasonText,100));
            UiKit.ActionButton(p,"ConfirmPurchase",gift?"免费领取":"花 "+price+" 金币购买",848,800,600,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId});},UiKit.Gold,error==null&&!IsBusy);
        }
        // v1.1 (W09): the colour name only; the English initials R/B are gone (colour-assist symbols stay on the dice).
        private static string ColorName07(string color)=>color=="red"?"红":"蓝";
        private static string FaceSummary07(FaceState f)=>f.permanentPip+" 点 · "+ColorName07(f.baseColor)+" · "+(f.texture=="smooth"?"光滑":f.texture=="rough"?"粗糙":"无纹理")+(f.isGold?" · 黄金面":"")+(f.baseWeight>1?" · 权重 "+f.baseWeight:"");
        private void DrawProbability07(Transform parent,DieInstance die,DieInstance afterDie,int[] before,int[] after,bool comparison)
        {
            var list=UiKit.Scroll(parent,"FaceProbabilities",848,576,600,200,396);int row=0;
            Action<string,int,int> entry=(label,oldUnits,newUnits)=>
            {
                float y=row++*64;
                UiKit.Text(list,"ProbabilityLabel"+row,label,0,y,120,36,19,UiKit.Paper);
                // v1.1 (W09, text proposal 3): probabilities with one decimal, the same precision everywhere.
                string value=PlayerNumberFormat.Percent(oldUnits,10000)+(comparison?" → "+PlayerNumberFormat.Percent(newUnits,10000):"");
                UiKit.Text(list,"ProbabilityValue"+row,value,124,y,444,36,19,UiKit.Paper,TextAlignmentOptions.TopRight);
                UiKit.Box(list,"EmptyBar"+row,0,y+42,568,8,UiKit.Ink);
                UiKit.Box(list,"ProbabilityBar"+row,0,y+42,568f*newUnits/Config.probabilityUnits,8,UiKit.Teal);
            };
            if(faceProbabilityExpanded07)for(int i=0;i<die.faces.Length;i++)entry(FaceName11(die.faces[i].faceId),before[i],after[i]);
            else for(int n=1;n<=6;n++)entry(n+" 点",die.faces.Select((f,i)=>f.permanentPip==n?before[i]:0).Sum(),afterDie.faces.Select((f,i)=>f.permanentPip==n?after[i]:0).Sum());
            list.sizeDelta=new Vector2(list.sizeDelta.x,row*64);
        }
        private void ShowBreakdownComplete07(SubmissionSnapshot snapshot,ScoreBreakdown b)
        {
            if(snapshot==null||b==null)return;
            var p=OpenModal("逐步明细");
            bool historical=ViewState.lastSubmission!=null&&snapshot.handId==ViewState.lastSubmission.snapshot.handId;
            if(historical&&HasCommittedPresentationDetails07(snapshot.handId))UiKit.Button(p,"ReplayReceipt","重播动画",1130,24,320,58,()=>{CloseModal();ReplayLastPresentation07();},!IsBusy);
            if(historical&&HasCommittedPresentationDetails07(snapshot.handId))UiKit.Button(p,"ReceiptEffectsDetails","计分记录",760,24,350,60,ShowCommittedPresentationDetails07,!IsBusy);
            string effects=ScoreEffects16(snapshot,historical);
            var trace=ScoreExplanation.Build(snapshot,b,Config);
            float diceHeight=trace.dice.Sum(d=>86+d.steps.Length*34);
            float globalHeight=58+trace.global.Length*46;
            var list=UiKit.Scroll(p,"ScoreDetail",40,110,1500,560,Math.Max(620,110+diceHeight+globalHeight+280));
            var formula=UiKit.Text(list,"Formula",ScoreFormula07(b),12,6,1440,80,29,UiKit.Gold);
            float cursor=6+FitHeight(formula,80)+20;
            for(int i=0;i<snapshot.dice.Length;i++)
            {
                var d=snapshot.dice[i];var f=d.permanentFacesCopy.First(x=>x.faceId==d.faceId);
                // v1.1 (W09): "骰子 1 的第 3 面" instead of D001/F03; colours without the R/B letters.
                var dieLine=UiKit.Text(list,"DieLine"+i,$"{i+1}. {DieFaceName11(d.dieId,d.faceId)}  {DiceLayers16(f,d.currentPip,d.currentColor,b.effectivePips[i],b.effectiveColors[i])}\n六面合计 {d.permanentSixFaceSum}  ·  {ScoreContribution16(b,i)}",12,cursor,1440,70,25);
                // Both source lines use the current font's real preferred height, including accessibility scaling.
                cursor+=FitHeight(dieLine,70)+2;
                for(int j=0;j<trace.dice[i].steps.Length;j++)
                {
                    var line=UiKit.Text(list,"DieStep"+i+"_"+j,ScoreStepText(trace.dice[i].steps[j],snapshot),40,cursor,1410,33,23,UiKit.Muted);cursor+=FitHeight(line,33)+4;
                }
                cursor+=14;
            }
            var globalTitle=UiKit.Text(list,"GlobalTitle","整手计算",12,cursor,1440,48,28,UiKit.Gold);cursor+=FitHeight(globalTitle,48)+10;
            for(int i=0;i<trace.global.Length;i++){var line=UiKit.Text(list,"GlobalStep"+i,ScoreStepText(trace.global[i],snapshot),12,cursor,1440,43,25);cursor+=FitHeight(line,43)+5;}
            string triggered=b.triggeredControllerIds.Length==0?"没有卡牌生效":string.Join(" · ",b.triggeredControllerIds.Select(NameOf));
            var footer=UiKit.Text(list,"Triggered",triggered+"\n复制、染色等永久效果在计分后才发生，不影响这一手的分数。"+(effects.Length>0?(historical?"\n\n计分后发生了：\n":"\n\n提交后会发生：\n")+effects:""),12,cursor+12,1440,260,25,UiKit.Teal);
            list.sizeDelta=new Vector2(list.sizeDelta.x,Math.Max(620,cursor+FitHeight(footer,260)+36));
            UiKit.Button(p,"ScoreDrawerBack","返回",40,700,500,60,()=>ShowBreakdown(snapshot,b));
            // v1.1 (W09): the copied formula is the decimal readout ("≈" where rounded), so the label no longer says "精确".
            UiKit.Button(p,"CopyExactScore","复制公式",1040,700,500,60,()=>
            {
                GUIUtility.systemCopyBuffer=formula.text;
                var label=p.Find("CopyExactScore")?.GetComponentInChildren<TextMeshProUGUI>();if(label!=null)label.text="已复制";
            });
        }
        private static float FitHeight(TextMeshProUGUI label,float minimum)
        {
            float height=Math.Max(minimum,label.GetPreferredValues(label.text,label.rectTransform.rect.width,float.PositiveInfinity).y+4);
            label.rectTransform.sizeDelta=new Vector2(label.rectTransform.sizeDelta.x,height);return height;
        }
        private string ScoreStepSource07(ScoreStep step,SubmissionSnapshot snapshot=null)
        {
            string label=step.sourceId!=null?NameOf(step.sourceId):step.kind=="glass"?"玻璃":step.kind=="gold"?"黄金面":step.kind=="unused_rolls"?"剩余掷骰奖励":step.kind=="hand"?"牌型倍率":step.kind=="color"?"颜色倍率":step.kind=="red_group"?"红组倍率":step.kind=="floor"?"去掉小数":"加分";
            if(step.kind=="enchantment_base"||step.kind=="enchantment_multiplier")label+=" · 附魔";
            if(step.kind=="retrigger")label+=" · 再计一次";
            // p1.0.0: boss modifier and item steps.
            if(step.kind=="sealed")label+=" · 封印";
            if(step.kind=="boss_hand")label+=" · 牌型倍率";
            if(step.kind=="boss")label+=" · 得分减半";
            // p0.9.0: name the cards that changed a shared step ("C84:150" = +1.5 on the hand multiplier, "C73:2" = two pool dice).
            if(!string.IsNullOrEmpty(step.note))
                label+="（含 "+string.Join("、",step.note.Split(',').Select(part=>{var kv=part.Split(':');return NameOf(kv[0])+(step.kind=="hand"?" +"+Factor(int.Parse(kv[1])):" +"+kv[1]+" 颗");}))+"）";
            // v1.1 (W09): the card is located by the position printed on it, never by its instance id;
            // a copied state names the card it comes from ("第 3 张，复制第 4 张").
            if(!string.IsNullOrEmpty(step.emitterId))
            {
                IList<ControllerInstance> cards=snapshot?.equippedControllers??(IList<ControllerInstance>)ViewState?.controllers;
                string at=CardPosition11(cards,step.emitterId);
                string owner=step.stateOwnerId!=step.emitterId&&!string.IsNullOrEmpty(step.stateOwnerId)?CardPosition11(cards,step.stateOwnerId):null;
                if(at!=null)label+="（"+at+(owner==null?"":"，复制"+owner)+"）";
            }
            return label;
        }
        private string ScoreStepText(ScoreStep step,SubmissionSnapshot snapshot=null)
        {
            string label=ScoreStepSource07(step,snapshot),before=ScoreValue07(step.before),after=ScoreValue07(step.after);
            return step.operation=="floor"?$"{label}：{before} → {after}":$"{label}：{before} {step.operation} {ScoreValue07(step.operand)} → {after}";
        }
        /// <summary>v0.9: a growth counter shown the way the card uses it ("×1.35" or "+24").</summary>
        private static string GrowthValue09(string abilityId,long growth)
            => ControllerRules.IsGrowthMultiplier(abilityId)?PlayerNumberFormat.Times(20+growth,20):"+"+growth;
        private string EffectText(EffectRecord e,SubmissionSnapshot snapshot)
        {
            string added=EffectText10(e);if(added!=null)return added;
            // v1.1 (W09): dice and faces by name ("骰子 1 的第 2 面"), never by D001 / F02 or instance ids.
            switch(e.kind)
            {
                case "ControllerEnchanted":
                {
                    var parts=(e.detail??"").Split(':');
                    string target=e.sourceId=="C114"||snapshot!=null
                        ? CardLabelIn11(parts[0],snapshot?.equippedControllers)??"一张卡牌"
                        : CardLabel11(parts[0]);
                    return NameOf(e.sourceId)+"："+target+" 获得附魔 "+EnchantShort(parts.LastOrDefault());
                }
                case "DieCopied":return NameOf(e.sourceId)+"："+DieName11(e.otherDieId)+" 的六面复制到"+DieName11(e.dieId);
                case "FaceDyed":return NameOf(e.sourceId)+"："+DieFaceName11(e.dieId,e.faceId)+(e.sourceId=="C38"?"永久染蓝":"永久染红");
                case "FaceValueChanged":return "骰面点数："+DieFaceName11(e.dieId,e.faceId)+" "+e.oldValue+" → "+e.newValue;
                case "DieDyed":return "永久染色："+DieName11(e.dieId)+" 六面全部染红";
                case "DieBroken":return DieName11(e.dieId)+" 玻璃损坏，已移出骰组";
                case "TemporaryDieExpired":return DieName11(e.dieId)+" 临时骰已在关卡结束时移除";
                case "ExtraRolls":return NameOf(e.sourceId)+"：增加 "+e.newValue+" 次掷骰";
                case "SubmissionIncome":return "存钱罐：本次提交收入 +"+e.newValue+" 金币";
                case "HandGrown":return "牌型提升："+NameOf(e.detail)+" "+e.oldValue+" → "+e.newValue+"级";
                case "ActiveColorChanged":{string color=(e.detail??"").Split(':').LastOrDefault();return "飘忽不定：下一手加成色 "+(color=="red"?"红":color=="blue"?"蓝":"未定");}
                case "ControllerDestroyed":return "玻璃大炮自毁";
                case "FundAccrued":
                    var previous=snapshot?.equippedControllers?.FirstOrDefault(c=>c.instanceId==e.detail)?.state?.fundAccrual;
                    return previous.HasValue?"成长基金卖价 +"+(e.newValue-previous.Value):"成长基金：卖价累计增加 "+e.newValue;
                // v0.9
                case "ControllerGrown":return e.sourceId=="C141"?"昙花一现：已过 "+e.newValue+" 关，倍率 −0.5":NameOf(e.sourceId)+"：成长 "+GrowthValue09(e.sourceId,e.oldValue)+" → "+GrowthValue09(e.sourceId,e.newValue);
                case "ControllerGrowthReset":return NameOf(e.sourceId)+"：牌型中断，回到 "+GrowthValue09(e.sourceId,e.newValue);
                case "ControllerSacrificed":return "祭坛：摧毁右侧的"+NameOf((e.detail??"").Split(':').LastOrDefault())+"，成长到 "+GrowthValue09("C100",e.newValue);
                case "WarIncome":return "以战养战：+"+e.newValue+" 金币（本关第 "+e.oldValue+" 次）";
                case "SaleBonusGrown":return "聚宝盆："+e.oldValue+" 张卡牌卖价各 +"+e.newValue;
                case "ControllerSlotAdded":return "卡牌栏位 "+e.oldValue+" → "+e.newValue;
                // v1.1 (W09): an unknown effect never shows its English kind name.
                default:return (string.IsNullOrEmpty(e.sourceId)?"":NameOf(e.sourceId)+"：")+"状态已更新";
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
                bool large=Large11;
                var p=UiKit.Frame(overlay,"Dialog",496,496,1280,large?520:400,UiKit.Panel,UiKit.Paper,true);
                var fill=(RectTransform)p.Find("Fill");
                UiKit.Role(UiKit.Text(p,"DialogTitle","遥控骰子 · 选 2 颗骰池里的骰子",24,large?4:8,1136,large?64:48,27,LabelOn11(fill,UiKit.Paper)),TextRole.ScreenTitle);
                UiKit.Button(p,"CloseDialog","×",1196,8,60,60,CloseModal);
                FadeModal07(p,fresh);
                UiKit.Text(p,"RemoteRule","会用掉这个道具和 1 次掷骰：选中的 2 颗掷出你指定的点数，其余照常掷。\n玻璃骰仍可能碎，碎了不退。",24,large?70:59,1232,large?94:72,19.5f,LabelOn11(fill,UiKit.Muted));
                // The single visible row must align after every wheel tick and at both ends.
                // Keep an 8-reference-pixel inter-row gap; do not count a trailing gap as content.
                const float targetHeight=64,targetPitch=72;
                int targetRows=(unlocked.Length+7)/8;
                var targets=UiKit.Scroll(p,"RemoteTargetList",24,large?170:136,1232,targetHeight,Math.Max(targetHeight,targetRows*targetPitch-(targetPitch-targetHeight)));
                var scroll=targets.GetComponentInParent<UnityEngine.UI.ScrollRect>();scroll.scrollSensitivity=targetPitch;
                for(int i=0;i<unlocked.Length;i++)
                {
                    string id=unlocked[i].dieId;
                    UiKit.Button(targets,"RemoteDie"+id,DieName11(id),(i%8)*150,(i/8)*targetPitch,144,targetHeight,()=>{targetScrollPosition=scroll.verticalNormalizedPosition;choose(id);},true,chosen.Contains(id));
                }
                scroll.verticalNormalizedPosition=targetScrollPosition;
                for(int slot=0;slot<2;slot++)
                {
                    float x=24+slot*624;string id=slot<chosen.Count?chosen[slot]:null;
                    UiKit.Text(p,id==null?"EmptyRemoteSlot"+slot:"Chosen"+id,"第 "+(slot+1)+" 颗："+(id==null?"请选择":DieName11(id)),x,large?240:208,600,large?54:36,20,LabelOn11(fill,id==null?UiKit.Muted:UiKit.Gold));
                    for(int n=1;n<=6;n++)
                    {
                        int pip=n;bool odd=ViewSources.Any(source=>source.abilityId=="C12")&&pip%2==1;
                        var button=UiKit.Button(p,id==null?"EmptyRemotePip"+slot+"_"+n:"RemotePip"+id+"_"+n,pip+(odd?"→"+(pip+1):""),x+(n-1)*(large?102:96),large?300:248,large?98:88,large?72:64,()=>{if(id==null)return;targetScrollPosition=scroll.verticalNormalizedPosition;pips[id]=pip;draw();},id!=null,id!=null&&pips[id]==pip);
                        var label=NoWrap11(button.transform.Find("Label").GetComponent<TextMeshProUGUI>());
                        if(large)label.fontSize=20*UiKit.FontScale;
                    }
                }
                UiKit.Text(p,"RemoteSelection","已选 "+chosen.Count+"/2"+(ViewSources.Any(source=>source.abilityId=="C12")?"\n“3→4”表示“不要奇数”会把它算作 4":""),24,large?386:322,600,large?120:72,19.5f,LabelOn11(fill,UiKit.Muted));
                UiKit.ActionButton(p,"RemoteConfirm","使用并掷骰",648,large?394:328,608,large?80:64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.UseItem,itemInstanceId=item.instanceId,dieIds=chosen.ToArray(),targetPips=chosen.Select(id=>pips[id]).ToArray()});},UiKit.Blue,chosen.Count==2&&!IsBusy&&Session.PendingIds.Length==0);
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
            // v1.1 (text proposal 5 #5): practice starts the first-run guide where an ordinary new run starts it.
            AdvanceGuide07(CommandKind.NewRun);guideIdentity07=null;boundaryTip07=null;
            notice="练习：目标 50 分，骰子固定为 4红 4红 2蓝 3蓝 6蓝。\n不影响你的正式存档。";
            Refresh();
        }
    }
}
