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
        private Art07SettlementBackdrop settlementBackdrop07;
        private string preparedSettlementBackdropCommand07,settlementBackdropIdentity07;
        public bool SettlementBackdropVisible07=>settlementBackdrop07!=null&&settlementBackdrop07.Visible;
        public int SettlementBackdropGraphicCount07=>settlementBackdrop07==null?0:settlementBackdrop07.GraphicCount;
        public int SettlementBackdropDiceMeshCount07=>settlementBackdrop07==null?0:settlementBackdrop07.DiceMeshCount;
        public bool SettlementBackdropFromHistory07 { get; private set; }

        private void EnsureSettlementBackdrop07()
        {
            if(settlementBackdrop07!=null)return;
            var layer=UiKit.Rect(canvas.transform,"SettlementBackdrop07",0,0,1920,1080);
            var group=UiKit.Component<CanvasGroup>(layer.gameObject);group.interactable=false;group.blocksRaycasts=false;
            settlementBackdrop07=layer.gameObject.AddComponent<Art07SettlementBackdrop>();
        }
        // Synchronous render meshes only; invisible until this exact command commits a settlement.
        private void PrepareSettlementBackdrop07(SessionCommand command)
        {
            if(command==null||IsBusy||Session.HasPendingSave||
                command.kind!=CommandKind.Submit&&command.kind!=CommandKind.EndStage||
                ViewState?.hand==null||inMenu)return;
            EnsureSettlementBackdrop07();settlementBackdropIdentity07=null;preparedSettlementBackdropCommand07=command.commandId;
            SettlementBackdropFromHistory07=false;
            try
            {
                // A ready hand can still be returning dice from the previous submission.
                // Freeze the current layout, not an in-between pose beside already-final labels.
                table.FinishLayoutTransitions();
                AlignSettlementBackdrop07();
                var excluded=new HashSet<string>(new[]{"Stats07","ScorePanel","Pause","Handbook","PreviousSubmission","DebugWorkspace","DebugMark","ActionReason"},StringComparer.Ordinal);
                var roots=main.Cast<Transform>().OfType<RectTransform>().Where(r=>r.gameObject.activeInHierarchy&&!excluded.Contains(r.name)).ToArray();
                var dice=ViewState.dice.Select(d=>table.GetFaceOrientation(d.dieId)).Where(t=>t!=null&&t.gameObject.activeInHierarchy);
                settlementBackdrop07.Capture(roots,dice,table.transform);
            }
            catch(Exception ex)
            {
                settlementBackdrop07.Clear();preparedSettlementBackdropCommand07=null;
                Debug.LogWarning("结算桌面快照不可用，将从已保存记录显示静态桌面："+ex.Message);
            }
        }
        private void ResolveSettlementBackdrop07(SessionCommand command,CommandResult result)
        {
            if(preparedSettlementBackdropCommand07==command.commandId)
            {
                preparedSettlementBackdropCommand07=null;
                if(result.accepted&&!result.noChange&&!result.duplicate&&Session.State?.phase=="StageSettlement")
                    settlementBackdropIdentity07=SettlementIdentity07(Session.State);
                else {settlementBackdrop07?.Clear();settlementBackdropIdentity07=null;}
            }
            if(Session.State?.phase!="StageSettlement"&&preparedSettlementBackdropCommand07==null)
            {settlementBackdrop07?.Clear();settlementBackdropIdentity07=null;}
        }
        private void SyncSettlementBackdropView07()
        {
            if(settlementBackdrop07==null)return;
            if(inMenu||Session.State?.phase!="StageSettlement")
            {settlementBackdrop07.Clear();settlementBackdropIdentity07=null;preparedSettlementBackdropCommand07=null;return;}
            if(ViewState?.phase!="StageSettlement")settlementBackdrop07.Hide();
        }
        private void AlignSettlementBackdrop07()
        {
            var root=(RectTransform)settlementBackdrop07.transform;root.anchoredPosition=main.anchoredPosition;
            if(root.GetSiblingIndex()>main.GetSiblingIndex())root.SetSiblingIndex(main.GetSiblingIndex());
        }
        private void ShowSettlementBackdrop07()
        {
            EnsureSettlementBackdrop07();AlignSettlementBackdrop07();
            string identity=SettlementIdentity07(ViewState);
            if(settlementBackdropIdentity07!=identity||!settlementBackdrop07.HasCapture)
            {
                BuildSavedSettlementBackdrop07();settlementBackdropIdentity07=identity;SettlementBackdropFromHistory07=true;
            }
            settlementBackdrop07.Show();
        }

        // Recovery has no previous render frame. Reconstruct only recorded data, visibly labelled.
        // This staging hierarchy is captured and hidden synchronously before any EOF/input frame.
        private void BuildSavedSettlementBackdrop07()
        {
            var record=ViewState.lastSubmission;
            var snapshot=record?.snapshot?.stageId==ViewState.stage.stageId?record.snapshot:null;
            var stage=UiKit.Rect(main,"SavedSettlementBoardStaging07",0,0,1920,1080);
            var group=UiKit.Component<CanvasGroup>(stage.gameObject);group.interactable=false;group.blocksRaycasts=false;
            bool compact=Compact07;
            var controllers=snapshot?.equippedControllers??ViewState.controllers.ToArray();
            int count=controllers.Length,shown=Math.Min(count>10?9:10,count);
            bool small=count>5||compact;
            float x0=compact?360:368,y0=compact?18:24,cw=compact?234:small?208:160,ch=compact?117:small?104:224,gap=compact?12:24;
            for(int i=0;i<Math.Max(4,shown);i++)
            {
                float x=x0+i%5*(cw+gap),y=y0+(small?i/5*(ch+(compact?18:16)):0);
                if(i>=shown){UiKit.Outline(stage,"SavedControllerEmpty"+i,x,y,cw,ch,UiKit.PanelLight);continue;}
                var c=controllers[i];var card=UiKit.Button(stage,"SavedController"+i,"",x,y,cw,ch,()=>{},true);
                card.targetGraphic.color=UiKit.Paper;UiKit.CardSurface(card,small);
                if(!small)DrawControllerFullBody07(card.transform,c.definitionId,"记录",c.enchantmentId,i+1);
                else
                {
                    bool tall=ch>=112;float icon=tall?64:48,tx=tall?84:68;
                    DrawControllerRarity07(card.transform,c.definitionId,true,cw,ch);EnchantBadge(card.transform,c.enchantmentId,cw-40,2,28);
                    UiKit.Text(card.transform,"Ordinal",(i+1).ToString(),cw-76,2,28,30,12,UiKit.Ink,TextAlignmentOptions.Center);
                    Icon(card.transform,c.definitionId,12,38,icon);
                    var name=UiKit.Text(card.transform,"Name",NameOf(c.definitionId),tx,28,cw-tx-12,tall?42:36,tall?18:16,UiKit.Ink,TextAlignmentOptions.Center);
                    name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;
                    UiKit.Text(card.transform,"State","记录",tx,tall?68:58,cw-tx-12,tall?34:32,14,UiKit.Ink,TextAlignmentOptions.Center);
                }
            }
            if(count>10)UiKit.Text(stage,"SavedControllerOverflow","记录共 "+count+" 件",x0+4*(cw+gap),y0+ch+(compact?18:16),cw,ch,20,UiKit.Paper,TextAlignmentOptions.Center);
            float poolX=compact?360:368,poolY=compact?408:472,poolW=compact?1542:1528;
            UiKit.Text(stage,"SavedBoardCaption",snapshot==null?"已保存关末 · 静态桌面":"已提交记录 · 本关最后一手",poolX+8,poolY,1000,36,compact?18:22,UiKit.Muted);
            var submitted=snapshot?.dice??Array.Empty<SubmissionDie>();
            var submittedIds=new HashSet<string>(submitted.Select(d=>d.dieId),StringComparer.Ordinal);
            var remaining=ViewState.dice.Where(d=>!submittedIds.Contains(d.dieId)).Take(10).ToArray();
            var poses=new List<Transform>();
            for(int i=0;i<remaining.Length;i++)
            {
                int row=i/5,rowCount=Math.Min(5,remaining.Length-row*5);float cell=280;
                float start=poolX+(poolW-rowCount*cell)/2;
                var center=new Vector2(start+i%5*cell+90,poolY+(compact?84:remaining.Length>5?68:132)+row*(compact?96:136));
                string id="settlement-pool-"+i;
                table.ShowStaticPreview07(id,remaining[i].Clone(),center,Quaternion.Euler(0,18,0),compact?.72f:remaining.Length>5?1.04f:1.18f);
                poses.Add(table.GetFaceOrientation("$static-preview:"+id));
                UiKit.Text(stage,"SavedPoolState"+i,"静置",center.x+(compact?52:72),center.y-16,100,40,compact?14:17,UiKit.Muted);
            }
            int capacity=snapshot?.capacity??ViewState.stage.capacity,slots=Math.Min(8,capacity),columns=compact?4:Math.Max(1,slots);
            float sy=compact?657:756,sw=compact?228:152,sh=compact?102:124,sg=compact?18:24;
            float sx=poolX+(poolW-columns*(sw+sg)+sg)/2;
            UiKit.Text(stage,"SavedSlotsTitle",snapshot==null?"关末静态槽位":"记录提交 "+submitted.Length+" / "+capacity+(capacity>8?" · 显示前8槽":""),poolX+16,sy,1000,36,compact?18:22,UiKit.Paper);
            for(int i=0;i<slots;i++)
            {
                float x=sx+i%columns*(sw+sg),y=sy+36+i/columns*(sh+18);bool used=i<submitted.Length;
                UiKit.Outline(stage,"SavedSlot"+i,x,y,sw,sh,used?UiKit.Gold:UiKit.PanelLight,3);
                UiKit.Text(stage,"SavedSlotIndex"+i,(i+1).ToString("00"),x+(used?(compact?126:94):8),y+4,50,32,16,UiKit.Muted);
                if(!used)continue;
                var source=submitted[i];var die=new DieInstance{dieId=source.dieId,lifetime=source.lifetime,isGlass=source.isGlass,faces=source.permanentFacesCopy.Select(f=>f.Clone()).ToArray()};
                // A detached display copy contains the physical face's recorded current overlay.
                var face=die.faces.First(f=>f.faceId==source.faceId);face.permanentPip=source.currentPip;face.baseColor=source.currentColor;
                string id="settlement-locked-"+i;var center=new Vector2(x+(compact?82:44),y+(compact?52:62));
                var rotation=Quaternion.AngleAxis(18,Vector3.up)*Quaternion.FromToRotation(DiceTable.Normals[DiceTable.FaceIndex(source.faceId)],Vector3.up);
                table.ShowStaticPreview07(id,die,center,rotation,compact?.65f:.82f);poses.Add(table.GetFaceOrientation("$static-preview:"+id));
            }
            Canvas.ForceUpdateCanvases();
            try{settlementBackdrop07.Capture(stage.Cast<Transform>().OfType<RectTransform>(),poses,table.transform);}
            finally{stage.gameObject.SetActive(false);table.HidePreview();}
        }
    }
}
