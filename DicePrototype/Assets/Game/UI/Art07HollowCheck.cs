using UnityEngine;

namespace DiceCraft
{
    /// <summary>Thin outline of a check; the inside is transparent, not a filled badge.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class Art07HollowCheck : UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper helper)
        {
            helper.Clear();Rect rect=GetPixelAdjustedRect();float size=Mathf.Min(rect.width,rect.height);
            Vector2 origin=rect.center-Vector2.one*size*.5f;
            Vector2[] shape={new Vector2(.03f,.43f),new Vector2(.32f,.08f),new Vector2(.98f,.80f),new Vector2(.80f,.98f),new Vector2(.32f,.46f),new Vector2(.22f,.60f)};
            var outer=new Vector2[shape.Length];var inner=new Vector2[shape.Length];
            for(int i=0;i<shape.Length;i++)outer[i]=origin+shape[i]*size;
            float stroke=Mathf.Max(1.5f,size*.045f);
            for(int i=0;i<shape.Length;i++)
            {
                Vector2 before=(outer[i]-outer[(i+shape.Length-1)%shape.Length]).normalized,after=(outer[(i+1)%shape.Length]-outer[i]).normalized;
                Vector2 n1=new Vector2(-before.y,before.x),n2=new Vector2(-after.y,after.x),sum=n1+n2;
                inner[i]=outer[i]+sum*(stroke/Mathf.Max(.05f,Vector2.Dot(sum,n1)));
                helper.AddVert(outer[i],color,Vector2.zero);helper.AddVert(inner[i],color,Vector2.zero);
            }
            for(int i=0;i<shape.Length;i++){int next=(i+1)%shape.Length;helper.AddTriangle(i*2,next*2,next*2+1);helper.AddTriangle(i*2,next*2+1,i*2+1);}
        }
    }
}
