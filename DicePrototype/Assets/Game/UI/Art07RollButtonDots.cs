using TMPro;
using UnityEngine;

namespace DiceCraft
{
    /// <summary>One finite mechanical scan, driven by the shared presentation clock, never a loop.</summary>
    public sealed class Art07RollButtonDots : MonoBehaviour
    {
        private TextMeshProUGUI label;
        private RectTransform grid;
        private readonly UnityEngine.UI.Image[] dots=new UnityEngine.UI.Image[15];
        public int VisibleStep { get; private set; }=-1;
        public bool IsShowing=>grid!=null&&grid.gameObject.activeSelf;
        public void Bind(UnityEngine.UI.Button button)
        {
            label=button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
            var rect=(RectTransform)button.transform;
            grid=UiKit.Rect(rect,"RollMechanicalDots07",(rect.rect.width-58)*.5f,(rect.rect.height-30)*.5f,58,30);
            for(int y=0;y<3;y++)for(int x=0;x<5;x++)
                dots[y*5+x]=UiKit.Box(grid,"Dot"+x+y,x*12,y*12,6,6,UiKit.Paper,false).GetComponent<UnityEngine.UI.Image>();
        }
        public void Evaluate(float elapsed,bool active,bool reduced)
        {
            if(label==null||grid==null)return;
            float duration=reduced?.08f:.18f;bool show=active&&elapsed<duration;
            grid.gameObject.SetActive(show);label.alpha=show?0:1;
            VisibleStep=show?(reduced?2:Mathf.Min(5,Mathf.FloorToInt(elapsed/.03f))):-1;
            if(!show)return;
            for(int y=0;y<3;y++)for(int x=0;x<5;x++)
            {var color=UiKit.Paper;color.a=reduced||x==VisibleStep||x==VisibleStep-1?.95f:.24f;dots[y*5+x].color=color;}
        }
        private void OnDisable(){if(label!=null)label.alpha=1;VisibleStep=-1;}
    }
}
