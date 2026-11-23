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
                var roots=FrozeRoots15(main);
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

        private static readonly HashSet<string> BackdropOmissions15=new HashSet<string>(StringComparer.Ordinal)
        {
            "Stats07","ScorePanel","Pause","Handbook","PreviousSubmission","DebugWorkspace","DebugMark",
            "Roll","Confirm","Clear","Submit","EndStage","Skip","ActionReason","ActionReasonUnderlay",
            "Settle11","Qualified","QualifiedContinueIncome","GoalGap11","GoalGapLabel11","GoalReward11"
        };
        private static RectTransform[] FrozeRoots15(RectTransform root)
        {
            return root.Cast<Transform>().OfType<RectTransform>().Where(r=>r.gameObject.activeInHierarchy&&
                !BackdropOmissions15.Contains(r.name)&&!r.name.StartsWith("QualifiedReward",StringComparison.Ordinal)).ToArray();
        }

        // Rebuild the same W11 drawing hierarchy from detached saved faces and cards. The staging
        // input components never see an event: only their immutable graphic and die meshes are kept.
        private void BuildSavedSettlementBackdrop07()
        {
            var original=ViewState;var actualMain=main;var oldSources=controllerSourceCache;
            var stage=UiKit.Rect(main,"SavedSettlementBoardStaging07",0,0,1920,1080);
            var group=UiKit.Component<CanvasGroup>(stage.gameObject);group.interactable=false;group.blocksRaycasts=false;
            var snapshot=original.lastSubmission?.snapshot;
            if(snapshot?.stageId!=original.stage.stageId||snapshot?.runId!=original.runId)snapshot=null;
            var saved=original.Clone();var hand=saved.hand=new HandState();
            // FinalizeStage clears the hand. The last submission is the saved source of its dice and rolls.
            hand.hasRolled=snapshot!=null;hand.rollsRemaining=snapshot?.rollsRemaining??0;
            hand.handId=snapshot?.handId??"";hand.rollsUsed=snapshot?.rollsUsed??0;
            // The receipt records unused submissions after the scoring command, including any budget changes.
            // Its value also drives the remaining-submissions chip on the settlement paper.
            saved.stage.remainingSubmissions=original.lastStageSettlement.remainingSubmissions;
            if(snapshot!=null)
            {
                saved.controllers=snapshot.equippedControllers.Select(c=>c.Clone()).ToList();
                saved.stage.capacity=snapshot.capacity;
                var shown=new HashSet<string>((snapshot.poolDice??Array.Empty<PoolDie>()).Select(d=>d.dieId),StringComparer.Ordinal);
                // Dice granted at stage end were never on this table. Unrolled surviving dice may be absent from poolDice.
                var receipt=original.processedReceipts.LastOrDefault(q=>q.settlement?.settlementId==original.lastStageSettlement.settlementId);
                foreach(var id in receipt?.effects.Where(e=>e.kind=="DieAdded").Select(e=>e.dieId)??Enumerable.Empty<string>())
                    if(!shown.Contains(id))saved.dice.RemoveAll(d=>d.dieId==id);
                foreach(var die in snapshot.dice??Array.Empty<SubmissionDie>())
                {
                    var restored=saved.dice.FirstOrDefault(d=>d.dieId==die.dieId);
                    if(restored==null){restored=new DieInstance{dieId=die.dieId};saved.dice.Add(restored);}
                    restored.faces=die.permanentFacesCopy.Select(f=>f.Clone()).ToArray();restored.lifetime=die.lifetime;restored.isGlass=die.isGlass;
                    var face=restored.faces.First(f=>f.faceId==die.faceId);face.baseColor=die.currentColor;
                    hand.orderedLockedIds.Add(die.dieId);
                    hand.currentResults.Add(new DieHandState{dieId=die.dieId,currentFaceId=die.faceId,currentColor=die.currentColor,currentPip=die.currentPip,isLocked=true,hasRolled=true});
                }
                foreach(var die in snapshot.poolDice??Array.Empty<PoolDie>())
                {
                    var restored=saved.dice.FirstOrDefault(d=>d.dieId==die.dieId);
                    if(restored==null)continue;
                    var face=restored.faces.FirstOrDefault(f=>f.faceId==die.faceId);if(face!=null)face.baseColor=die.currentColor;
                    hand.currentResults.Add(new DieHandState{dieId=die.dieId,currentFaceId=die.faceId,currentColor=die.currentColor,currentPip=die.currentPip,hasRolled=true});
                }
            }
            int oldPool=poolPage,oldSlot=slotPage;
            try
            {
                main=stage;ViewState=saved;controllerSourceCache=null;poolPage=slotPage=0;
                DrawCards11();DrawStageCard11();DrawTray11();DrawResources11();DrawSubmit11();
                // The saved frame has no score presentation. Put the slot numbers on tabs, keep the bare table free of captions.
                for(int i=0;i<Math.Min(8,saved.stage.capacity);i++)
                {
                    var placeholder=stage.Find("EmptySlot"+i);if(placeholder!=null)placeholder.gameObject.SetActive(false);
                    var number=stage.Find("SlotIndex"+i) as RectTransform;if(number==null)continue;
                    var chip=Surface11(stage,"SavedSlotTab15_"+i,number.anchoredPosition.x-2,-number.anchoredPosition.y-1,28,28,"ui_chip_formula",UiKit.Panel);
                    number.gameObject.SetActive(false);TabLabel11(chip,"Ordinal",(i+1).ToString(),16,LabelOn11(chip));
                }
                table.FinishLayoutTransitions();Canvas.ForceUpdateCanvases();
                var poses=saved.dice.Select(d=>table.GetFaceOrientation(d.dieId)).Where(p=>p!=null&&p.gameObject.activeInHierarchy);
                var aliases=new Dictionary<string,string>(StringComparer.Ordinal);int poolIndex=0;
                foreach(var die in saved.dice.OrderBy(d=>d.createdSerial))
                {
                    int slot=hand.orderedLockedIds.IndexOf(die.dieId);
                    aliases[die.dieId]=slot>=0?"$static-preview:settlement-locked-"+slot:"$static-preview:settlement-pool-"+poolIndex++;
                }
                settlementBackdrop07.Capture(FrozeRoots15(stage),poses,table.transform,aliases);
            }
            finally
            {
                ViewState=original;main=actualMain;controllerSourceCache=oldSources;poolPage=oldPool;slotPage=oldSlot;
                stage.gameObject.SetActive(false);table.Hide();visibleDice.Clear();dieCenters.Clear();controllerRects.Clear();lockedSlotRects07.Clear();GripAt11.Clear();
            }
        }
    }
}
