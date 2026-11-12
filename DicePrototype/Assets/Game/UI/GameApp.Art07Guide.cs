using DiceCraft.Core;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private bool guide07;
        private int guideStep07;
        private string guideIdentity07;
        private Art07PanelMotion guideTail07;
        private bool guideInline07;
        private string boundaryTip07;
        public const string ZeroRollHintKey07="art07.hint.zero_rolls",RandomHintKey07="art07.hint.c53_preview",QualifiedHintKey07="art07.hint.qualified_continue";
        private void InitializeGuide07(){guide07=PlayerPrefs.GetInt("art07.guideSeen",0)==0;guideStep07=0;}
        private void ShowGuide07(){CloseModal();guide07=true;guideStep07=0;guideIdentity07=null;Refresh();}
        private void AdvanceGuide07(CommandKind kind)
        {
            if(!guide07)return;
            if(kind==CommandKind.NewRun)guideStep07=0;
            if(kind==CommandKind.Roll&&guideStep07==0)guideStep07=1;
            if(kind==CommandKind.ConfirmLocks&&guideStep07<=2)guideStep07=3;
            if(kind==CommandKind.Submit&&guideStep07==3)guideStep07=4;
        }
        private void DismissGuide07()
        {
            var paper=GuideElement07("FirstRunGuide");
            if(paper!=null)
            {
                if(guideTail07==null)
                {
                    var tail=UiKit.Rect(canvas.transform,"GuideFadeTail07",0,0,1920,1080);
                    guideTail07=UiKit.Component<Art07PanelMotion>(tail.gameObject);
                }
                guideTail07.CaptureExit(paper,.08f,reduced);
            }
            guideDockTailHold07=guideDocked07;guide07=false;guideIdentity07=null;PlayerPrefs.SetInt("art07.guideSeen",1);PlayerPrefs.Save();Refresh();
            if(guideDockTailHold07)StartCoroutine(ReleaseGuideDock07());
        }
        private void DrawGuide07()
        {
            guideInline07=false;guideDocked07=false;
            if(guideDockTailHold07&&!guide07&&!inMenu&&!modal&&ViewState!=null){ApplyGuideDock07(ViewState.phase=="Shop");guideInline07=true;}
            if(DrawBoundaryTip07())return;
            if(!guide07||inMenu||modal||ViewState==null||ViewState.debugWorkspace||IsBusy)return;
            bool shop=ViewState.phase=="Shop";
            if(!shop&&ViewState.phase!="HandReady"&&ViewState.phase!="Selecting")return;
            if(Session.PendingIds.Length>0&&guideStep07<2)guideStep07=2;
            if(ViewState.hand!=null&&ViewState.hand.orderedLockedIds.Count>0&&guideStep07<3)guideStep07=3;
            if(shop)guideStep07=4;else if(guideStep07==4)return;
            string[] title={"1 / 5  投掷","2 / 5  待选","3 / 5  确认锁定","4 / 5  提交","5 / 5  关末构筑"};
            string[] body={"每手总投掷包含首掷。点击掷骰开始。","点选骰子进入待选；再点一次可取消。","确认后，本手不能随时解除锁定。","牌型、颜色、红组分别计算，再相乘。","买骰只增加骰池，提交栏需要独立扩容。"};
            RectTransform target=GuideElement07(shop?"DiceCounter":guideStep07==0?"Roll":guideStep07==1?
                "Die_"+visibleDice.FirstOrDefault():guideStep07==2?"Confirm":"Submit");
            if(target==null)return;
            var obstacles=GuideObstacles07();Rect targetBounds=GuideBounds07(target);
            const float w=400,h=144;
            Rect paperBounds;
            if(!TryGuidePaper07(w,h,targetBounds,obstacles,out paperBounds))
            {DrawGuideFallback07(targetBounds,obstacles,title[guideStep07],body[guideStep07],shop);return;}
            // One short arrow is anchored at the actual target, not at a guessed
            // paper-card coordinate. It does not draw a connector across HUD/dice.
            DrawGuideArrow07(targetBounds,obstacles,paperBounds,guideStep07==1&&!shop);
            DrawGuidePaper07(paperBounds,title[guideStep07],body[guideStep07],shop);
        }
        private void DrawGuidePaper07(Rect bounds,string title,string body,bool shop)
        {
            const float w=400,h=144;
            var paper=UiKit.Frame(main,"FirstRunGuide",bounds.x,bounds.y,w,h,UiKit.Paper,UiKit.Ink);
            float close=Mathf.Max(60,40/Mathf.Max(.1f,canvas.scaleFactor));
            // At 150% the unchanged title/body need 46/88 reference pixels.
            // The v0.71 sliced paper ends at y135.5 inside this 144px rect;
            // stack the text through y135 instead of its transparent bottom edge.
            float textWidth=w-close-40;
            UiKit.Text(paper,"GuideTitle",title,16,1,textWidth,46,21,UiKit.Ink);
            var description=UiKit.Text(paper,"GuideBody",body,16,47,textWidth,88,20,UiKit.Ink);
            description.textWrappingMode=TextWrappingModes.Normal;description.overflowMode=TextOverflowModes.Overflow;
            UiKit.ActionButton(paper,"DismissGuide","×",w-close-8,6,close,close,DismissGuide07,UiKit.Paper);
            AnimateGuide07(paper,shop);
        }
        private void AnimateGuide07(RectTransform paper,bool shop)
        {
            string identity=ViewState.runId+"/"+guideStep07+"/"+shop+"/"+guideInline07;
            bool fresh=guideIdentity07!=identity;guideIdentity07=identity;
            if(fresh&&guideTail07!=null)guideTail07.CancelTail();
            UiKit.Component<Art07PanelMotion>(paper.gameObject).Enter(.12f,.08f,fresh,reduced);
        }
        private void DrawGuideFallback07(Rect target,List<Rect> obstacles,string title,string body,bool shop)
        {
            // Keep the existing notice-suspension flag; the rendered content is a full paper.
            guideInline07=true;guideDocked07=true;
            Rect bounds=ApplyGuideDock07(shop);
            // Re-read live obstacles after the local reflow. The arrow stays a
            // short mark at the real action target; it never crosses card text.
            DrawGuideArrow07(target,GuideObstacles07(),bounds,guideStep07==1&&!shop);
            DrawGuidePaper07(bounds,title,body,shop);
        }

        private RectTransform GuideElement07(string name)
        {
            for(int i=0;i<main.childCount;i++)
            {
                var child=main.GetChild(i) as RectTransform;
                if(child!=null&&child.gameObject.activeInHierarchy&&child.name==name)return child;
            }
            return null;
        }
        private Rect GuideBounds07(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);
            Vector2 low=new Vector2(float.MaxValue,float.MaxValue),high=new Vector2(float.MinValue,float.MinValue);
            for(int i=0;i<4;i++)
            {
                Vector3 local=main.InverseTransformPoint(corners[i]);var point=new Vector2(local.x,-local.y);
                low=Vector2.Min(low,point);high=Vector2.Max(high,point);
            }
            return Rect.MinMaxRect(low.x,low.y,high.x,high.y);
        }
        private Rect GuideDieBounds07(string id,Rect hit)
        {
            var orientation=table.GetFaceOrientation(id);
            if(orientation==null||table.viewCamera==null)return hit;
            Rect result=hit;
            foreach(var renderer in orientation.GetComponentsInChildren<Renderer>(false))
            {
                if(!renderer.enabled)continue;Bounds b=renderer.bounds;
                for(int i=0;i<8;i++)
                {
                    Vector3 world=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    Vector3 screen=table.viewCamera.WorldToScreenPoint(world);if(screen.z<=0)continue;
                    Vector2 point=ScreenToDesign(screen);result=Rect.MinMaxRect(Mathf.Min(result.xMin,point.x),Mathf.Min(result.yMin,point.y),Mathf.Max(result.xMax,point.x),Mathf.Max(result.yMax,point.y));
                }
            }
            return result;
        }
        private List<Rect> GuideObstacles07()
        {
            var result=new List<Rect>();
            foreach(var button in main.GetComponentsInChildren<Button>(false))result.Add(GuideBounds07((RectTransform)button.transform));
            foreach(var rect in main.GetComponentsInChildren<RectTransform>(false))
                if(rect.name.StartsWith("Product_")||rect.name=="SelfStudy")result.Add(GuideBounds07(rect));
            foreach(var text in main.GetComponentsInChildren<TextMeshProUGUI>(false))
                if(!string.IsNullOrWhiteSpace(text.text))result.Add(GuideBounds07(text.rectTransform));
            foreach(var die in dieCenters)
            {
                var hit=GuideElement07("Die_"+die.Key);if(hit!=null)result.Add(GuideDieBounds07(die.Key,GuideBounds07(hit)));
            }
            return result;
        }
        private static bool GuideClear07(Rect rect,List<Rect> obstacles,float gap=6)
        {
            rect=Rect.MinMaxRect(rect.xMin-gap,rect.yMin-gap,rect.xMax+gap,rect.yMax+gap);
            foreach(var blocked in obstacles)if(rect.Overlaps(blocked))return false;
            return true;
        }
        private static bool TryGuidePaper07(float width,float height,Rect target,List<Rect> obstacles,out Rect result)
        {
            result=default;float best=float.PositiveInfinity;
            // Search only the main table; sidebar numbers and navigation are
            // never sacrificed. Empty decorative controller slots are legal.
            for(float y=24;y+height<=1056;y+=16)
                for(float x=368;x+width<=1896;x+=16)
                {
                    var candidate=new Rect(x,y,width,height);if(!GuideClear07(candidate,obstacles,8))continue;
                    float distance=(candidate.center-target.center).sqrMagnitude;
                    if(distance>=best)continue;best=distance;result=candidate;
                }
            return !float.IsPositiveInfinity(best);
        }
        private void DrawGuideArrow07(Rect target,List<Rect> obstacles,Rect paper,bool die)
        {
            Vector2[] directions=die?new[]{Vector2.right,Vector2.left,Vector2.down,Vector2.up}:
                new[]{Vector2.down,Vector2.up,Vector2.right,Vector2.left};
            foreach(float length in new[]{24f,12f,8f})foreach(var direction in directions)
            {
                Vector2 tip=direction==Vector2.down?new Vector2(target.center.x,target.yMin-4):
                    direction==Vector2.up?new Vector2(target.center.x,target.yMax+4):
                    direction==Vector2.right?new Vector2(target.xMin-4,target.center.y):new Vector2(target.xMax+4,target.center.y);
                // Design-space Y points down. Vector2.down represents the arrow
                // from above, so convert its direction before drawing the lines.
                Vector2 travel=new Vector2(direction.x,-direction.y),start=tip-travel*length,normal=new Vector2(-travel.y,travel.x);
                Vector2 a=tip-travel*Mathf.Min(10,length*.6f)+normal*6,b=tip-travel*Mathf.Min(10,length*.6f)-normal*6;
                Vector2 min=Vector2.Min(Vector2.Min(start,tip),Vector2.Min(a,b))-Vector2.one*2,max=Vector2.Max(Vector2.Max(start,tip),Vector2.Max(a,b))+Vector2.one*2;
                var bounds=Rect.MinMaxRect(min.x,min.y,max.x,max.y);
                if(bounds.xMin<8||bounds.xMax>1912||bounds.yMin<8||bounds.yMax>1072||bounds.Overlaps(paper)||!GuideClear07(bounds,obstacles,0))continue;
                var arrow=UiKit.Rect(main,"GuideArrow",bounds.x,bounds.y,bounds.width,bounds.height);
                Vector2 origin=bounds.position;GuideArrowLine07(arrow,"Stem",start-origin,tip-origin);GuideArrowLine07(arrow,"HeadA",a-origin,tip-origin);GuideArrowLine07(arrow,"HeadB",b-origin,tip-origin);
                // A geometry marker, not a hit target: regression tests check
                // that the actual tip terminates outside the live target edge.
                UiKit.Rect(arrow,"GuideArrowTip",tip.x-origin.x,tip.y-origin.y,0,0);
                return;
            }
        }
        private static void GuideArrowLine07(Transform parent,string name,Vector2 from,Vector2 to)
        {
            Vector2 delta=to-from,center=(from+to)*.5f;
            var line=UiKit.Box(parent,name,center.x,center.y,delta.magnitude,3,UiKit.Gold);
            line.pivot=new Vector2(.5f,.5f);line.localRotation=Quaternion.Euler(0,0,-Mathf.Atan2(delta.y,delta.x)*Mathf.Rad2Deg);
        }
        private bool DrawBoundaryTip07()
        {
            if(inMenu||modal||ViewState==null||ViewState.debugWorkspace||IsBusy||Session.HasPendingSave)return false;
            if(ViewState.phase!="HandReady"&&ViewState.phase!="Selecting"){boundaryTip07=null;return false;}
            bool zero=ViewState.hand!=null&&ViewState.hand.hasRolled&&ViewState.hand.rollsRemaining==0;
            bool random=ViewSources.Any(source=>source.abilityId=="C53");
            bool qualified=ViewState.stage.qualified&&ViewState.stage.remainingSubmissions>0;
            if(boundaryTip07==ZeroRollHintKey07&&!zero||boundaryTip07==RandomHintKey07&&!random||boundaryTip07==QualifiedHintKey07&&!qualified)boundaryTip07=null;
            if(string.IsNullOrEmpty(boundaryTip07))
            {
                if(zero&&PlayerPrefs.GetInt(ZeroRollHintKey07,0)==0)boundaryTip07=ZeroRollHintKey07;
                else if(random&&PlayerPrefs.GetInt(RandomHintKey07,0)==0)boundaryTip07=RandomHintKey07;
                else if(qualified&&PlayerPrefs.GetInt(QualifiedHintKey07,0)==0)boundaryTip07=QualifiedHintKey07;
            }
            if(string.IsNullOrEmpty(boundaryTip07))return false;
            string body=boundaryTip07==ZeroRollHintKey07?"投掷用尽后，仍可选骰、确认锁定、排序和提交。":boundaryTip07==RandomHintKey07?"C53 的倍率在提交后揭示；预览与取消不会重新抽取。":"目标已达成：可以继续提交，也可点击「结束本轮」。";
            // Reuse the reason row. No paper card covers dice, score, or the action buttons.
            var reason=main.Find("ActionReason")?.GetComponent<TextMeshProUGUI>();
            if(reason==null)return false;
            reason.text="首次提示 · "+body;reason.color=UiKit.Gold;reason.textWrappingMode=TextWrappingModes.NoWrap;
            reason.rectTransform.sizeDelta=new Vector2(Compact07?1432:1480,42);
            // Normal HUD has room below sidebar navigation; compact HUD has room after its raised action row.
            float x=Compact07?1838:286;
            UiKit.ActionButton(main,"DismissBoundaryHint07","×",x,1012,60,60,DismissBoundaryTip07,UiKit.Paper);
            return true;
        }
        private void DismissBoundaryTip07()
        {
            if(string.IsNullOrEmpty(boundaryTip07))return;
            PlayerPrefs.SetInt(boundaryTip07,1);PlayerPrefs.Save();boundaryTip07=null;Refresh();
        }
    }
}
