using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    /// <summary>
    /// V1.1 (task W11, PLAY_LAYOUT section 4): a small shape badge on a die instead of text beside it. Red is a circle,
    /// blue a diamond (the existing "红圆 / 蓝菱辅助" colour assist), glass a pane with two shine strokes, a stage-temporary
    /// die a clock. Drawn as vector geometry with an ink rim, so it stays crisp at every scale and reads on any table.
    /// Colours are given by the caller from the active palette at draw time. Never a raycast target.
    /// </summary>
    [RequireComponent(typeof(RectTransform),typeof(CanvasRenderer))]
    public sealed class Play11Badge : MaskableGraphic
    {
        public enum Shape { Circle, Diamond, Glass, Clock }
        private Shape shape;
        private Color fill=Color.white, ink=Color.black, paper=Color.white;
        public Shape Kind=>shape;

        public void Configure(Shape kind,Color fillColor,Color inkColor,Color paperColor)
        {
            raycastTarget=false;
            if(shape==kind&&fill==fillColor&&ink==inkColor&&paper==paperColor){color=Color.white;return;}
            shape=kind;fill=fillColor;ink=inkColor;paper=paperColor;color=Color.white;SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();Rect r=rectTransform.rect;Vector2 c=r.center;float radius=Mathf.Min(r.width,r.height)*.5f;
            float rim=Mathf.Max(1.5f,radius*.2f);
            switch(shape)
            {
                case Shape.Circle:
                    Disc(vh,c,radius,ink);Disc(vh,c,radius-rim,fill);
                    break;
                case Shape.Diamond:
                    Diamond(vh,c,radius,ink);Diamond(vh,c,radius-rim*1.35f,fill);
                    break;
                case Shape.Glass:
                {
                    float h=radius*.92f;
                    RectQuad(vh,new Rect(c.x-h,c.y-h,h*2,h*2),ink);
                    float i=h-rim;RectQuad(vh,new Rect(c.x-i,c.y-i,i*2,i*2),fill);
                    // Two diagonal shine strokes in paper.
                    Stroke(vh,c+new Vector2(-i*.62f,-i*.05f),c+new Vector2(-i*.05f,i*.62f),Mathf.Max(1.2f,rim*.75f),paper);
                    Stroke(vh,c+new Vector2(-i*.25f,-i*.55f),c+new Vector2(i*.55f,i*.25f),Mathf.Max(1f,rim*.55f),paper);
                    break;
                }
                case Shape.Clock:
                {
                    Disc(vh,c,radius,ink);Disc(vh,c,radius-rim,paper);
                    float hand=Mathf.Max(1.2f,rim*.7f);
                    Stroke(vh,c,c+Vector2.up*(radius-rim)*.7f,hand,ink);
                    Stroke(vh,c,c+Vector2.right*(radius-rim)*.55f,hand,ink);
                    break;
                }
            }
        }

        private static void Disc(VertexHelper vh,Vector2 c,float radius,Color color)
        {
            if(radius<=0)return;const int segments=28;int start=vh.currentVertCount;
            vh.AddVert(c,color,Vector2.zero);
            for(int i=0;i<=segments;i++){float a=i*Mathf.PI*2/segments;vh.AddVert(c+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,color,Vector2.zero);}
            for(int i=1;i<=segments;i++)vh.AddTriangle(start,start+i,start+i+1);
        }
        private static void Diamond(VertexHelper vh,Vector2 c,float radius,Color color)
        {
            if(radius<=0)return;int start=vh.currentVertCount;
            vh.AddVert(c+Vector2.up*radius,color,Vector2.zero);vh.AddVert(c+Vector2.right*radius,color,Vector2.zero);
            vh.AddVert(c+Vector2.down*radius,color,Vector2.zero);vh.AddVert(c+Vector2.left*radius,color,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
        private static void RectQuad(VertexHelper vh,Rect r,Color color)
        {
            int start=vh.currentVertCount;
            vh.AddVert(new Vector2(r.xMin,r.yMin),color,Vector2.zero);vh.AddVert(new Vector2(r.xMin,r.yMax),color,Vector2.zero);
            vh.AddVert(new Vector2(r.xMax,r.yMax),color,Vector2.zero);vh.AddVert(new Vector2(r.xMax,r.yMin),color,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
        private static void Stroke(VertexHelper vh,Vector2 a,Vector2 b,float width,Color color)
        {
            Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x)*width*.5f;int start=vh.currentVertCount;
            vh.AddVert(a-n,color,Vector2.zero);vh.AddVert(b-n,color,Vector2.zero);vh.AddVert(b+n,color,Vector2.zero);vh.AddVert(a+n,color,Vector2.zero);
            vh.AddTriangle(start,start+1,start+2);vh.AddTriangle(start,start+2,start+3);
        }
    }
}
