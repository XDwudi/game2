using UnityEngine;
using UnityEngine.EventSystems;

namespace DiceCraft
{
    /// <summary>Uses the existing die pointer area; changes only the physical contour tint.</summary>
    public sealed class Art07DieHover : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler
    {
        private DiceTable table;
        private string die;
        private bool allowed;
        public void Bind(DiceTable owner,string id,bool canHover)
        {Clear();table=owner;die=id;allowed=canHover;}
        public void OnPointerEnter(PointerEventData data){if(allowed&&table!=null)table.SetHoveredDie07(die,true);}
        public void OnPointerExit(PointerEventData data)=>Clear();
        private void Clear(){if(table!=null&&!string.IsNullOrEmpty(die))table.SetHoveredDie07(die,false);}
        private void OnDisable()=>Clear();
        private void OnApplicationFocus(bool focused){if(!focused)Clear();}
    }
}
