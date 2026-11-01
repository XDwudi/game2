using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private Art07FaceTransition permanentTransition07;
        private readonly Dictionary<string,DieInstance> permanentVisualAfter07=new Dictionary<string,DieInstance>(StringComparer.Ordinal);
        private readonly List<string> unchangedPermanentTargets07=new List<string>();
        private readonly List<KeyValuePair<string,CanvasGroup>> brokenOutlines07=new List<KeyValuePair<string,CanvasGroup>>();
        private readonly Dictionary<Art07PrefabView,float> effectLocalDurations07=new Dictionary<Art07PrefabView,float>();
        private Art07QualifiedStamp qualifiedStamp07;
        private RectTransform qualifiedLayer07;
        private string qualifiedStage07;
        public int ActivePermanentFaceTransitions07=>permanentTransition07?.ActiveFaces??0;
        public int CreatedPermanentFacePairs07=>permanentTransition07?.CreatedPairs??0;
        public string[] TransitionBeforeMeshNames07=>permanentTransition07?.BeforeMeshNames??Array.Empty<string>();
        public string[] UnchangedPermanentTargets07=>unchangedPermanentTargets07.ToArray();
        public string[] BrokenOutlineIds07=>brokenOutlines07.Select(p=>p.Key).ToArray();
        public bool QualifiedStampPlaying07=>qualifiedStamp07?.IsPlaying==true;
        public float QualifiedStampElapsed07=>qualifiedStamp07?.Elapsed??0;
        public int QualifiedStampBegins07=>qualifiedStamp07?.Begins??0;
        private void EnsurePermanentTransition07()
        {
            if(permanentTransition07!=null)return;
            var go=new GameObject("Art07PermanentFaces");go.transform.SetParent(table.transform,false);
            permanentTransition07=go.AddComponent<Art07FaceTransition>();permanentTransition07.Initialize();
        }
        // Raw effects are already committed. This is a display copy for dice which expire
        // during the same settlement; surviving after-state data always remains authoritative.
        private void CapturePermanentDisplay07(CommandReceipt committed,RunState before,RunState after)
        {
            permanentVisualAfter07.Clear();foreach(var die in before.dice)permanentVisualAfter07[die.dieId]=die.Clone();
            foreach(var e in committed.effects)
            {
                if(e.dieId==null||!permanentVisualAfter07.TryGetValue(e.dieId,out var target))continue;
                if(e.kind=="DieCopied"&&permanentVisualAfter07.TryGetValue(e.otherDieId,out var source)){target.faces=source.faces.Select(f=>f.Clone()).ToArray();target.isGlass=source.isGlass;}
                else if(e.kind=="DieDyed")foreach(var face in target.faces)face.baseColor=e.detail;
                else if(e.kind=="FaceDyed"){var face=target.faces.FirstOrDefault(f=>f.faceId==e.faceId);if(face!=null)face.baseColor=e.detail;}
                else if(e.kind=="FaceValueChanged"){var face=target.faces.FirstOrDefault(f=>f.faceId==e.faceId);if(face!=null)face.permanentPip=checked((int)e.newValue);}
            }
            foreach(var die in after.dice)permanentVisualAfter07[die.dieId]=die.Clone();
        }
        private static bool IsPermanentEvent07(PresentationEvent e)=>e.EffectFamily=="DieCopied"||e.EffectFamily=="DieDyed"||e.EffectFamily=="FaceDyed"||e.EffectFamily=="FaceValueChanged";
        private static Color[] FaceColors07(DieInstance die)=>die.faces.Select(f=>f.baseColor=="red"?UiKit.Red:UiKit.Blue).ToArray();
        private static bool FaceLooksDifferent07(FaceState a,FaceState b)=>a.permanentPip!=b.permanentPip||a.baseColor!=b.baseColor||a.texture!=b.texture||a.isGold!=b.isGold;
        private bool BeginPermanentTarget07(string id,IEnumerable<PresentationEvent> events,Vector2 center)
        {
            var before=lastPresentationBefore07?.dice.FirstOrDefault(d=>d.dieId==id);
            if(before==null||!permanentVisualAfter07.TryGetValue(id,out var after))return false;
            var prior=lastPresentationBefore07.hand.currentResults.FirstOrDefault(d=>d.dieId==id);if(prior==null)return false;
            var display=prior.Clone();display.remoteOverride=false;int current=DiceTable.FaceIndex(display.currentFaceId);
            display.currentPip=before.faces[current].permanentPip;display.currentColor=before.faces[current].baseColor;
            table.Display(before,display,center,Compact07?.65f:.82f);
            var oldImages=Art07FaceTransition.Capture(table.GetFaceOrientation(id),id,FaceColors07(before));
            display.currentPip=after.faces[current].permanentPip;display.currentColor=after.faces[current].baseColor;
            table.Display(after,display,center,Compact07?.65f:.82f);
            var newImages=Art07FaceTransition.Capture(table.GetFaceOrientation(id),id,FaceColors07(after));
            bool changed=false,copy=events.Any(e=>e.EffectFamily=="DieCopied");
            for(int i=0;i<6;i++)if(FaceLooksDifferent07(before.faces[i],after.faces[i]))
            {
                EnsurePermanentTransition07();var a=before.faces[i];var b=after.faces[i];bool colorOnly=a.permanentPip==b.permanentPip&&a.isGold==b.isGold&&a.texture==b.texture;
                float seconds=effectOptions07.ReducedMotion?.08f:effectOptions07.Speed==PresentationSpeed.Fast?.14f:copy?.42f:colorOnly?.22f:.32f;
                permanentTransition07.Add(oldImages[i],newImages[i],seconds,0,colorOnly&&!effectOptions07.ReducedMotion,a.permanentPip!=b.permanentPip&&!effectOptions07.ReducedMotion,a.permanentPip!=b.permanentPip);changed=true;
            }
            if(before.isGlass!=after.isGlass)
            {
                EnsurePermanentTransition07();float seconds=effectOptions07.ReducedMotion?.08f:effectOptions07.Speed==PresentationSpeed.Fast?.14f:.42f;
                permanentTransition07.AddBody(table.GetFaceOrientation(id).Find("Body").GetComponent<MeshRenderer>(),before.isGlass?1:0,after.isGlass?1:0,seconds);changed=true;
            }
            var label=main.Find("DieState"+id)?.GetComponent<TextMeshProUGUI>();
            if(label!=null)
            {
                var a=before.faces[current];var b=after.faces[current];
                string pip=a.permanentPip==b.permanentPip?b.permanentPip.ToString():a.permanentPip+"→"+b.permanentPip;
                string color=a.baseColor==b.baseColor?ColorText07(b.baseColor):ColorText07(a.baseColor)+"→"+ColorText07(b.baseColor);
                // The ordinary side label is only 56 reference pixels wide.
                // Keep the full point/color transition in the target's own slot,
                // using deliberate lines and the unchanged accessible font size.
                // Other permanent faces and source order remain in the receipt.
                bool compact=Compact07;label.text=changed?(compact?"永久 "+pip+"\n"+color:"永久\n"+pip+"\n"+color):(compact?"永久 "+pip+"\n净变化0":"永久\n"+pip+"\n未变");
                label.color=b.baseColor=="red"?UiKit.Red:UiKit.Blue;
                label.alignment=TextAlignmentOptions.TopLeft;label.lineSpacing=compact?0:-20;
                label.textWrappingMode=TextWrappingModes.NoWrap;label.overflowMode=TextOverflowModes.Ellipsis;
                if(lockedSlotRects07.TryGetValue(id,out var slot))
                {
                    float x=center.x+44,y=slot.y+36;
                    label.rectTransform.anchoredPosition=new Vector2(x,-y);
                    label.rectTransform.sizeDelta=new Vector2(slot.xMax-x,slot.yMax-y-2);
                }
                label.ForceMeshUpdate();
            }
            if(!changed)
            {
                unchangedPermanentTargets07.Add(id);float size=Compact07?78:110;
                UiKit.Box(effectLabels07,"PermanentNoChange_"+id,center.x-size*.4f,center.y+size*.55f,size*.8f,2,UiKit.Gold);
            }
            if(copy&&changed&&EffectReserve07("permanent",EffectPoolKind.SourceLine,1)>0)
            {
                string source=lastPresentationBefore07.hand.orderedLockedIds.FirstOrDefault();
                if(source!=null&&dieCenters.TryGetValue(source,out var from))
                {
                    var link=Effect07("fx_copy_link","six_faces/"+id,Vector2.zero,new Vector2(192,192));
                    if(link!=null){link.SetPolyline(new[]{new Vector2(from.x,-from.y+64),new Vector2(from.x,-from.y+80),new Vector2(center.x,-center.y+80),new Vector2(center.x,-center.y+64)});effectLocalDurations07[link]=effectOptions07.ReducedMotion?.12f:effectOptions07.Speed==PresentationSpeed.Fast?.14f:.42f;}
                }
            }
            return changed;
        }
        private void MarkPermanentSources07(IEnumerable<PresentationEvent> events)
        {
            foreach(string id in events.Where(IsPermanentEvent07).SelectMany(e=>e.SourceEmitterIds).Distinct(StringComparer.Ordinal))
                if(TryCardBounds07(id,out var r))UiKit.Outline(effectLabels07,"PermanentSource_"+id,r.xMin-2,r.yMin-2,r.width+4,r.height+4,UiKit.Paper,2);
        }
        private void BeginUpgradeFaces07(DieInstance before,DieInstance after,Vector2 center,Quaternion rotation,bool reducedMotion)
        {
            EnsurePermanentTransition07();permanentTransition07.Finish();
            table.ShowPreview(before,center,rotation,2);var a=Art07FaceTransition.Capture(table.GetFaceOrientation("$die-preview"),"$die-preview",FaceColors07(before));
            table.ShowPreview(after,center,rotation,2);var b=Art07FaceTransition.Capture(table.GetFaceOrientation("$die-preview"),"$die-preview",FaceColors07(after));
            for(int i=0;i<6;i++)if(FaceLooksDifferent07(before.faces[i],after.faces[i]))
            {
                bool colorOnly=before.faces[i].permanentPip==after.faces[i].permanentPip&&before.faces[i].texture==after.faces[i].texture&&before.faces[i].isGold==after.faces[i].isGold;
                permanentTransition07.Add(a[i],b[i],reducedMotion?.08f:.24f,reducedMotion?0:.06f,colorOnly&&!reducedMotion,before.faces[i].permanentPip!=after.faces[i].permanentPip&&!reducedMotion,before.faces[i].permanentPip!=after.faces[i].permanentPip);
            }
        }
        private void ShowAdditionalBrokenOutlines07(EffectRecord[] broken,Dictionary<string,DiePose07> poses)
        {
            int start=effectOptions07.ReducedMotion||effectOptions07.Quality==PresentationQuality.Low?0:2;
            foreach(var e in broken.Skip(start))if(poses.TryGetValue(e.dieId,out var pose))
            {
                Vector2 low=Vector2.positiveInfinity,high=Vector2.negativeInfinity;
                for(int i=0;i<8;i++)
                {
                    Vector3 corner=new Vector3((i&1)==0?-.5f:.5f,(i&2)==0?-.5f:.5f,(i&4)==0?-.5f:.5f);
                    Vector3 point=pose.Position+pose.Rotation*Vector3.Scale(corner,pose.Scale);Vector3 screen=table.viewCamera.WorldToScreenPoint(point);
                    var design=ScreenToDesign(screen);low=Vector2.Min(low,design);high=Vector2.Max(high,design);
                }
                var outline=UiKit.Rect(effectLabels07,"BrokenOutline_"+e.dieId,low.x,low.y,high.x-low.x,high.y-low.y);
                UiKit.Outline(outline,"Contour",0,0,high.x-low.x,high.y-low.y,UiKit.Muted,2);
                UiKit.Text(outline,"Removed","×",0,0,high.x-low.x,high.y-low.y,26,UiKit.Muted,TextAlignmentOptions.Center);
                var group=UiKit.Component<CanvasGroup>(outline.gameObject);group.alpha=1;group.interactable=false;group.blocksRaycasts=false;brokenOutlines07.Add(new KeyValuePair<string,CanvasGroup>(e.dieId,group));
            }
        }
        private bool QualifiedStampPlayingForCurrentStage07=>qualifiedStamp07!=null&&qualifiedStamp07.IsPlaying&&qualifiedStage07==ViewState?.stage?.stageId;
        private void BeginQualifiedStamp07()
        {
            if(receipt07==null||!receipt07.QualifiedAfter||receipt07.QualifiedBefore)return;
            if(qualifiedStamp07==null)
            {
                qualifiedLayer07=UiKit.Rect(canvas.transform,"Art07QualifiedLayer",0,0,1920,1080);
                var full=UiKit.Frame(qualifiedLayer07,"FullStamp",48,428,272,52,UiKit.Gold,UiKit.Ink);var fullAlpha=UiKit.Component<CanvasGroup>(full.gameObject);fullAlpha.blocksRaycasts=false;fullAlpha.interactable=false;
                var title=UiKit.Text(full,"Title","本关目标已达成",8,4,256,44,24,UiKit.Ink,TextAlignmentOptions.Center);
                var small=UiKit.Frame(qualifiedLayer07,"PersistentBadge",300,438,28,28,UiKit.Gold,UiKit.Ink);var smallAlpha=UiKit.Component<CanvasGroup>(small.gameObject);smallAlpha.blocksRaycasts=false;smallAlpha.interactable=false;
                UiKit.Text(small,"Check","✓",0,0,28,28,20,UiKit.Ink,TextAlignmentOptions.Center);
                qualifiedStamp07=qualifiedLayer07.gameObject.AddComponent<Art07QualifiedStamp>();qualifiedStamp07.Initialize(full,fullAlpha,title,small,smallAlpha);
            }
            qualifiedLayer07.anchoredPosition=main.anchoredPosition;qualifiedLayer07.SetAsLastSibling();if(overlay!=null)overlay.SetAsLastSibling();
            qualifiedLayer07.Find("FullStamp/Title").GetComponent<TMP_Text>().fontSize=24*UiKit.FontScale;
            qualifiedStage07=lastPresentationAfter07.stage.stageId;
            var options=presentationPlayer07.Sequence.Options;qualifiedStamp07.Begin(receipt07.GameReceiptId,options.ReducedMotion,options.Speed==PresentationSpeed.Fast,new Vector2(48,-428),new Vector2(300,-438));
            // The committed TargetState beat may still display the old hand snapshot.
            // Reserve the same score layout now, before this stamp's first rendered frame.
            // Preserve the stats sibling so repainting this panel cannot cover navigation.
            var previousStats=main.Find("Stats07");int sibling=previousStats!=null?previousStats.GetSiblingIndex():0;
            Header();main.Find("Stats07")?.SetSiblingIndex(sibling);
            AlignQualifiedStamp07();
        }
        private void AlignQualifiedStamp07()
        {
            if(qualifiedStamp07==null||main==null)return;
            var frame=main.Find("Stats07/StageScoreFrame") as RectTransform;if(frame==null)return;
            Vector3 origin=main.InverseTransformPoint(frame.TransformPoint(Vector3.zero));
            var slot=main.Find("Stats07/QualifiedStampSlot07") as RectTransform;
            Vector2 start=slot!=null?(Vector2)main.InverseTransformPoint(slot.TransformPoint(Vector3.zero)):new Vector2(origin.x+12,origin.y+60);
            qualifiedStamp07.SetPositions(start,new Vector2(origin.x+frame.rect.width-32,origin.y-4));
        }
        private void TickQualifiedStamp07(float seconds)
        {
            if(qualifiedStamp07==null)return;
            if(inMenu||ViewState==null||ViewState.stage.stageId!=qualifiedStage07||!IsPresenting07&&!ViewState.stage.qualified||ViewState.phase=="Shop"||ViewState.phase=="StagePreview"){qualifiedStamp07.Hide();return;}
            qualifiedLayer07.anchoredPosition=main.anchoredPosition;
            AlignQualifiedStamp07();
            qualifiedStamp07.Tick(seconds,modal);
        }
        private void FinishReceiptTailOnSkip07()
        {
            if(qualifiedStamp07?.IsPlaying==true&&qualifiedStamp07.ReceiptId==receipt07?.GameReceiptId&&presentationPlayer07.Sequence!=null&&presentationPlayer07.Clock.ElapsedMilliseconds+.001<presentationPlayer07.Sequence.TotalDurationMs)qualifiedStamp07.Finish();
        }
    }
}
