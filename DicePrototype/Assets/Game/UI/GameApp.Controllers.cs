using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private readonly Dictionary<string,RectTransform> controllerRects=new Dictionary<string,RectTransform>();
        private string draggedController;
        private int controllerInsertion=-1;
        private RectTransform dragGhost, insertionLine, dragBlocker;
        private bool controllerOrderPanel;
        public bool IsControllerDragging => draggedController!=null;
        private bool CanReorderControllers => ViewState!=null&&(ViewState.phase=="Shop"||ViewState.phase=="StagePreview")&&!IsBusy&&!Session.HasPendingSave;
        private MimicBinding[] ViewBindings => ViewState.stage.started&&ViewState.phase!="Shop"&&ViewState.phase!="StagePreview"?ViewState.stage.mimicBindings:null;
        private AbilitySource[] controllerSourceCache;
        private AbilitySource[] ViewSources => controllerSourceCache??(controllerSourceCache=ControllerRules.ResolveSources(ViewState.controllers,ViewBindings));
        private string ControllerStateLabel(ControllerInstance c)
        {
            var source=ViewSources.FirstOrDefault(s=>s.emitterId==c.instanceId);
            string text="";
            if(c.definitionId=="C41")
            {
                var target=(ViewBindings??ControllerRules.FreezeBindings(ViewState.controllers)).FirstOrDefault(b=>b.sourceInstanceId==c.instanceId)?.targetInstanceId;
                text=source?.abilityId!=null?"→ "+NameOf(source.abilityId)+" / "+source.stateOwnerId:
                    !string.IsNullOrEmpty(target)&&ViewState.stage.destroyedControllerIds.Contains(target)?"目标已损坏 "+target+" · 复制失效":
                    !string.IsNullOrEmpty(target)?"右邻 "+target+" · 无可解析能力":"无右邻 · 复制失效";
            }
            if(source?.abilityId=="C45")text+=(text.Length>0?" · ":"")+(source.state.activeColor=="red"?"当前红":"当前蓝");
            if(source?.abilityId=="C51")text+=(text.Length>0?" · ":"")+"×"+(source.state.turnoverUnits2/2m).ToString("0.#");
            if(source?.abilityId=="C05")text+=(text.Length>0?" · ":"")+(ViewState.hand?.c05UsedEmitterIds.Contains(c.instanceId)==true?"本手已用":"本手未用");
            if(source?.abilityId=="C43")text+=(text.Length>0?" · ":"")+"上次 "+NameOf(ViewState.previousPrimaryHandId);
            if(c.definitionId=="C52"||c.definitionId=="C41")text+=(text.Length>0?" · ":"")+"售价 "+ControllerRules.SalePrice(c,Config);
            return text;
        }
        private string CompactControllerState(ControllerInstance item)
        {string state=ControllerStateLabel(item);return state.Length==0?"":" · "+(state.Length>10?state.Substring(0,10)+"…":state);}
        private void AttachControllerDrag(UnityEngine.UI.Button button,ControllerInstance item)
        {
            controllerRects[item.instanceId]=(RectTransform)button.transform;
            var pointer=UiKit.Component<DiePointer>(button.gameObject);pointer.clicked=null;pointer.dragStart=null;pointer.dragged=null;pointer.dragEnd=null;
            if(!CanReorderControllers)return;
            pointer.dragStart=e=>
            {
                if(e.button!=PointerEventData.InputButton.Left||!CanReorderControllers)return;
                draggedController=item.instanceId;controllerInsertion=-1;e.eligibleForClick=false;
                dragBlocker=UiKit.Box(overlay,"DragBlocker",0,0,1920,1080,Color.clear,true);
                dragGhost=UiKit.Box(overlay,"ControllerGhost",0,0,620,215,UiKit.PanelLight,false);
                UiKit.Text(dragGhost,"Name",NameOf(item.definitionId),12,10,596,35,24,UiKit.Gold);
                UpdateControllerDrag(e);
            };
            pointer.dragged=UpdateControllerDrag;
            pointer.dragEnd=e=>
            {
                if(draggedController!=item.instanceId)return;
                UpdateControllerDrag(e);var original=ViewState.controllers.Select(c=>c.instanceId).ToList();int insertion=controllerInsertion;string moved=draggedController;
                CancelControllerDrag();e.eligibleForClick=false;
                if(insertion<0)return;
                int from=original.IndexOf(moved);original.RemoveAt(from);if(insertion>from)insertion--;original.Insert(Math.Max(0,Math.Min(original.Count,insertion)),moved);
                CommitControllerOrder(original.ToArray());
            };
        }
        private void UpdateControllerDrag(PointerEventData e)
        {
            if(!IsControllerDragging)return;
            var p=ScreenToDesign(e.position);dragGhost.anchoredPosition=new Vector2(Mathf.Clamp(p.x+18,0,1300),-Mathf.Clamp(p.y+18,0,865));
            controllerInsertion=-1;if(insertionLine!=null)insertionLine.gameObject.SetActive(false);
            for(int i=0;i<ViewState.controllers.Count;i++)
            {
                if(!controllerRects.TryGetValue(ViewState.controllers[i].instanceId,out var r)||r==null||!r.gameObject.activeInHierarchy)continue;
                var mask=r.GetComponentInParent<UnityEngine.UI.Mask>();
                if(mask!=null&&!RectTransformUtility.RectangleContainsScreenPoint((RectTransform)mask.transform,e.position,null))continue;
                if(!RectTransformUtility.RectangleContainsScreenPoint(r,e.position,null))continue;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(r,e.position,null,out var local);
                bool after=local.x>r.rect.center.x;controllerInsertion=i+(after?1:0);
                var corners=new Vector3[4];r.GetWorldCorners(corners);var screen=RectTransformUtility.WorldToScreenPoint(null,after?corners[2]:corners[1]);var pos=ScreenToDesign(screen);
                insertionLine=UiKit.Box(overlay,"ControllerInsertion",pos.x-3,pos.y,6,r.rect.height*canvas.scaleFactor/canvas.scaleFactor,UiKit.Teal,false);break;
            }
            string preview="区域外 · 松手取消，不改变排列。";
            if(controllerInsertion>=0)
            {
                var candidate=ViewState.controllers.Select(c=>c.Clone()).ToList();int from=candidate.FindIndex(c=>c.instanceId==draggedController);var moved=candidate[from];candidate.RemoveAt(from);
                int to=controllerInsertion>from?controllerInsertion-1:controllerInsertion;candidate.Insert(Math.Max(0,Math.Min(candidate.Count,to)),moved);
                var sources=ControllerRules.ResolveSources(candidate);
                var mimics=sources.Where(s=>s.emitterDefinitionId=="C41").Take(4).Select(s=>s.emitterId+" → "+(s.abilityId==null?"无能力":NameOf(s.abilityId)+" / "+s.stateOwnerId));
                int k=ViewState.debugCapacityOverride>0?ViewState.debugCapacityOverride:ControllerRules.Capacity(candidate,Config,ViewState.isDebug);
                preview="允许插入 · 普通占槽 "+ControllerRules.OrdinaryUsed(candidate)+(ViewState.debugWorkspace?"（调试）":" / "+ViewState.controllerSlots)+" · 容量 K "+Session.EffectiveCapacity+" → "+k+"\n"+string.Join("\n",mimics);
            }
            UiKit.Text(dragGhost,"Candidate",preview,12,52,596,150,20,UiKit.Teal);
        }
        public void CancelControllerDrag()
        {
            draggedController=null;controllerInsertion=-1;
            if(dragGhost!=null)dragGhost.gameObject.SetActive(false);if(insertionLine!=null)insertionLine.gameObject.SetActive(false);if(dragBlocker!=null)dragBlocker.gameObject.SetActive(false);
        }
        private void CommitControllerOrder(string[] ids)
        {
            bool reopen=controllerOrderPanel;
            if(ids.SequenceEqual(ViewState.controllers.Select(c=>c.instanceId)))return;
            var result=Send(new SessionCommand{kind=CommandKind.ReorderControllers,controllerIds=ids});
            if(result.accepted){PlaySound("reorder");if(reopen)ShowControllerOrder();}
        }
        private void MoveController(string id,int direction)
        {
            if(!CanReorderControllers)return;var ids=ViewState.controllers.Select(c=>c.instanceId).ToList();int index=ids.IndexOf(id),to=index+direction;
            if(index<0||to<0||to>=ids.Count)return;ids.RemoveAt(index);ids.Insert(to,id);CommitControllerOrder(ids.ToArray());
        }
        public void ShowControllerOrder()
        {
            controllerOrderPanel=true;controllerRects.Clear();
            var p=OpenModal("控制器排列与状态");
            UiKit.Text(p,"OrderHint",CanReorderControllers?"拖到卡片左/右侧插入；松到区域外或按 Esc 取消。右邻按列表顺序跨行延续。":"本关排列已冻结。商店和关卡开始前可调整。",40,94,1500,70,24,UiKit.Muted);
            var list=UiKit.Scroll(p,"OrderList",40,180,1500,535,Math.Max(535,((ViewState.controllers.Count+2)/3)*175));
            for(int i=0;i<ViewState.controllers.Count;i++)
            {
                var c=ViewState.controllers[i];float x=(i%3)*490,y=(i/3)*175;
                var b=UiKit.Button(list,"OrderCard"+c.instanceId,(i+1)+". "+NameOf(c.definitionId)+" · "+EnchantShort(c.enchantmentId)+"\n"+ControllerStateLabel(c),x,y,470,105,()=>ShowController(c,ViewState.phase=="Shop"));
                b.GetComponentInChildren<TextMeshProUGUI>().fontSize=21;AttachControllerDrag(b,c);
                UiKit.Button(list,"MoveLeft"+c.instanceId,"←",x,y+115,225,43,()=>MoveController(c.instanceId,-1),CanReorderControllers&&i>0);
                UiKit.Button(list,"MoveRight"+c.instanceId,"→",x+245,y+115,225,43,()=>MoveController(c.instanceId,1),CanReorderControllers&&i<ViewState.controllers.Count-1);
            }
            if(ViewState.controllers.Count==0)UiKit.Text(list,"EmptyControllers","当前没有控制器。",20,20,1400,80,28,UiKit.Muted);
        }
        private void OnApplicationFocus(bool focused){if(!focused)CancelControllerDrag();}
    }
}
