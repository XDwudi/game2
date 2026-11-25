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
        private AbilitySource[] ViewSources => controllerSourceCache??(controllerSourceCache=ControllerRules.ResolveSources(ViewState.controllers,ViewBindings,ViewDisabled10));
        /// <summary>v0.9: the current value of a growth card ("×1.35" / "+24"), or null for other abilities.</summary>
        private string GrowthText09(string abilityId,ControllerDynamicState state)
        {
            if(abilityId==null||state==null)return null;
            // v1.1 (W09): one number format for every multiplier (PlayerNumberFormat).
            if(ControllerRules.IsGrowthMultiplier(abilityId))return PlayerNumberFormat.Times(20+state.growth,20);
            if(ControllerRules.IsGrowthBase(abilityId))return "+"+state.growth;
            if(abilityId=="C141")return "+"+Factor(checked((int)(Config.Controller("C141").parameters.initialAdditive100-state.growth*Config.Controller("C141").parameters.decayPerClear100))); // W3A-ADAPT: C141 is an additive bonus now
            if(abilityId=="C63")return "+"+Factor(ViewState.dice.Count(d=>d.lifetime=="permanent")*Config.Controller("C63").parameters.additivePerPermanentDie100); // W3A-ADAPT: C63 is an additive bonus now
            return null;
        }
        /// <summary>v0.9 C100: before a stage starts, the controllers the altars will destroy when it does.</summary>
        private System.Collections.Generic.HashSet<string> AltarVictims09()
        {
            var victims=new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            if(ViewState==null||(ViewState.phase!="Shop"&&ViewState.phase!="StagePreview"))return victims;
            foreach(var pair in ControllerRules.AltarPlan(ViewState.controllers))victims.Add(pair.Value);
            return victims;
        }
        private string ControllerStateLabel(ControllerInstance c,bool detail=false)
        {
            var source=ViewSources.FirstOrDefault(s=>s.emitterId==c.instanceId);
            string text=AltarVictims09().Contains(c.instanceId)?"下一关开始时被左侧的祭坛摧毁（可拖动调序避开）":DisabledNow10(c.instanceId)?"本关被首领减益封住，没有任何效果":DisabledNext10(c.instanceId)?"首领关开始后将失效（可拖动调序）":"";
            if(c.definitionId=="C41")
            {
                var target=(ViewBindings??ControllerRules.FreezeBindings(ViewState.controllers)).FirstOrDefault(b=>b.sourceInstanceId==c.instanceId)?.targetInstanceId;
                // v1.1 (W09): the copied card is named (by its position when needed), never shown by instance id.
                var neighbor=ViewState.controllers.FirstOrDefault(x=>x.instanceId==target);
                string copy;
                if(source?.abilityId!=null)
                {
                    bool chain=source.stateOwnerId!=target;
                    copy=detail && chain && neighbor!=null
                        ?"右边："+CardPosition11(ViewState.controllers,target)+" "+NameOf(neighbor.definitionId)
                            +"\n实际复制："+CardPosition11(ViewState.controllers,source.stateOwnerId)+" "+NameOf(source.abilityId)
                        :detail?"复制：右边的"+NameOf(source.abilityId):"复制："+NameOf(source.abilityId);
                    if(detail && (source.abilityId=="C45" || source.abilityId=="C51" || GrowthText09(source.abilityId,source.state)!=null))
                        copy+="\n成长与状态来自："+CardPosition11(ViewState.controllers,source.stateOwnerId);
                }
                else copy=string.IsNullOrEmpty(target)?"右边没有卡":
                    ViewState.stage?.destroyedControllerIds?.Contains(target)==true?"复制对象已损坏":"右边的卡不能复制";
                text=string.IsNullOrEmpty(text)?copy:text+" · "+copy;
            }
            if(source?.abilityId=="C45")text+=(text.Length>0?" · ":"")+(source.state.activeColor=="red"?"当前红":"当前蓝");
            if(source?.abilityId=="C51")text+=(text.Length>0?" · ":"")+PlayerNumberFormat.Times(source.state.turnoverUnits2,2);
            string growth=GrowthText09(source?.abilityId,source?.state);
            if(growth!=null)text+=(text.Length>0?" · ":"")+"当前 "+growth;
            if(c.state!=null&&c.state.saleBonus>0&&c.definitionId!="C52"&&c.definitionId!="C41")text+=(text.Length>0?" · ":"")+"售价 "+OwnedSalePrice10(c);
            if(source?.abilityId=="C05")text+=(text.Length>0?" · ":"")+(ViewState.hand?.c05UsedEmitterIds.Contains(c.instanceId)==true?"本手已用":"本手可用");
            if(source?.abilityId=="C43")text+=(text.Length>0?" · ":"")+"上次 "+NameOf(ViewState.previousPrimaryHandId);
            if(c.definitionId=="C52"||c.definitionId=="C41")text+=(text.Length>0?" · ":"")+"售价 "+OwnedSalePrice10(c);
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
            string preview="松手取消";
            if(controllerInsertion>=0)
            {
                var candidate=ViewState.controllers.Select(c=>c.Clone()).ToList();int from=candidate.FindIndex(c=>c.instanceId==draggedController);var moved=candidate[from];candidate.RemoveAt(from);
                int to=controllerInsertion>from?controllerInsertion-1:controllerInsertion;candidate.Insert(Math.Max(0,Math.Min(candidate.Count,to)),moved);
                var sources=ControllerRules.ResolveSources(candidate);
                // v1.1 (W09): cards by their position in the new order, never by instance id; "提交栏 N 格" instead of "容量 K".
                var mimics=sources.Where(s=>s.emitterDefinitionId=="C41").Take(4).Select(s=>CardPosition11(candidate,s.emitterId)+" "+NameOf("C41")+" → "+(s.abilityId==null?"无能力":NameOf(s.abilityId)+"（"+(CardPosition11(candidate,s.stateOwnerId)??"—")+"）"));
                // p1.0.0: outside the debug workspace the core quotes the capacity for the new order (boss modifier, disabled cards and altars included).
                int k=ViewState.debugCapacityOverride>0?ViewState.debugCapacityOverride:ViewState.debugWorkspace?ControllerRules.Capacity(candidate,Config,ViewState.isDebug):Session.CapacityForOrder(candidate.Select(c=>c.instanceId).ToList());
                preview="放在这里 · 栏位 "+ControllerRules.OrdinaryUsed(candidate,Config)+(IsDebugWorkspace?"（调试）":IsPracticeRun11?"":" / "+ViewState.controllerSlots)+" · 提交栏 "+Session.EffectiveCapacity+" → "+k+" 格\n"+string.Join("\n",mimics);
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
            var p=OpenModal("卡牌顺序");
            UiKit.Text(p,"OrderHint",CanReorderControllers?"拖到卡片左边或右边插入，按 Esc 取消。顺序从左到右、跨行连续。":"关卡中不能调整顺序（商店和关卡选择页可以）。",40,94,1500,70,24,UiKit.Muted);
            var list=UiKit.Scroll(p,"OrderList",40,180,1500,535,Math.Max(535,((ViewState.controllers.Count+2)/3)*175));
            for(int i=0;i<ViewState.controllers.Count;i++)
            {
                var c=ViewState.controllers[i];float x=(i%3)*490,y=(i/3)*175;
                var b=UiKit.Button(list,"OrderCard"+c.instanceId,(i+1)+". "+NameOf(c.definitionId)+" · "+EnchantShort(c.enchantmentId)+"\n"+ControllerStateLabel(c),x,y,470,105,()=>ShowController(c,ViewState.phase=="Shop"));
                b.GetComponentInChildren<TextMeshProUGUI>().fontSize=21*UiKit.FontScale;AttachControllerDrag(b,c);
                UiKit.Button(list,"MoveLeft"+c.instanceId,"←",x,y+115,225,60,()=>MoveController(c.instanceId,-1),CanReorderControllers&&i>0);
                UiKit.Button(list,"MoveRight"+c.instanceId,"→",x+245,y+115,225,60,()=>MoveController(c.instanceId,1),CanReorderControllers&&i<ViewState.controllers.Count-1);
            }
            if(ViewState.controllers.Count==0)UiKit.Text(list,"EmptyControllers","当前没有卡牌。",20,20,1400,80,28,UiKit.Muted);
        }
        private void OnApplicationFocus(bool focused){if(!focused){FinishLootReveal15();CancelControllerDrag();CancelDieDrag07();if(IsBusy)SkipVisual();}}
    }
}
