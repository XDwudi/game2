using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    // V1.1 task W11b: a label drawn on a filled surface takes the palette colour (ink, paper or text colour) that reads best
    // on the surface as it is actually drawn: the surface sprite's centre pixel times the image's tint. The measure is the
    // WCAG 2 contrast ratio; AA (4.5:1) is the floor. These labels are thin CJK strokes (13 px at 720p) that never draw
    // their full colour, so the pick aims 10 % above the floor. When no palette colour gets there on that fill, the fill is
    // changed as little as possible: deepened under the lightest palette colour, or lightened under the darkest, whichever
    // takes fewer 5 % steps. Nothing here names a theme's colour, so the rule holds for every theme, current and future.
    public sealed partial class GameApp
    {
        /// <summary>WCAG 2 AA contrast for body text: the floor every label on a filled surface keeps.</summary>
        public const float MinContrast11=4.5f;
        /// <summary>What a picked palette colour (or an adjusted fill) aims for: AA plus 10 % for thin strokes.</summary>
        public const float PickContrast11=4.95f;
        private static readonly Dictionary<Sprite,Color> spriteFill11=new Dictionary<Sprite,Color>();
        private static readonly Dictionary<Sprite,Vector4> spriteFrame11=new Dictionary<Sprite,Vector4>();
        /// <summary>Sprites whose fill or frame has been measured (W11c: emptied on every theme switch, so the caches never
        /// hold another theme's sprites).</summary>
        public static int SurfaceCacheCount11=>spriteFill11.Count+spriteFrame11.Count;
        private static void ClearSurfaceCaches11(){spriteFill11.Clear();spriteFrame11.Clear();}

        private static float Channel11(float c)=>c<=.04045f?c/12.92f:Mathf.Pow((c+.055f)/1.055f,2.4f);
        /// <summary>WCAG relative luminance of an sRGB colour (alpha ignored).</summary>
        public static float Luminance11(Color c)=>.2126f*Channel11(c.r)+.7152f*Channel11(c.g)+.0722f*Channel11(c.b);
        /// <summary>WCAG contrast ratio between two opaque sRGB colours, 1 to 21.</summary>
        public static float Contrast11(Color a,Color b){float x=Luminance11(a),y=Luminance11(b);return (Mathf.Max(x,y)+.05f)/(Mathf.Min(x,y)+.05f);}

        /// <summary>A sprite's own pixels (sRGB). UI textures are usually not CPU-readable, so they are copied through a
        /// render texture. Null when the sprite cannot be read.</summary>
        private static Color32[] SpritePixels11(Sprite sprite,out int width,out int height)
        {
            width=height=0;if(sprite==null)return null;
            try
            {
                var tex=sprite.texture;Rect r=sprite.textureRect;width=Mathf.RoundToInt(r.width);height=Mathf.RoundToInt(r.height);
                if(width<1||height<1)return null;
                var rt=RenderTexture.GetTemporary(tex.width,tex.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);
                var previous=RenderTexture.active;var copy=new Texture2D(width,height,TextureFormat.RGBA32,false,false);
                try{Graphics.Blit(tex,rt);RenderTexture.active=rt;copy.ReadPixels(new Rect(r.x,r.y,width,height),0,0);copy.Apply(false);return copy.GetPixels32();}
                finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.Destroy(copy);}
            }
            catch(System.Exception){return null;}
        }
        /// <summary>The colour at the centre of a sprite (the fill of a nine-sliced surface), read once per sprite.</summary>
        private static Color SpriteFill11(Sprite sprite)
        {
            if(sprite==null)return Color.white;
            if(spriteFill11.TryGetValue(sprite,out var cached))return cached;
            var pixels=SpritePixels11(sprite,out int w,out int h);
            Color c=pixels==null?Color.white:(Color)pixels[(h/2)*w+w/2];
            spriteFill11[sprite]=c;return c;
        }
        /// <summary>
        /// How far a sliced surface's drawn frame reaches in from each edge (left, bottom, right, top), in the image's rect
        /// units: along the middle row and column, inside the slice border, the last pixel that is transparent or unlike
        /// the centre fill (frame lines, a bevel, a lip, an inner line). Text placed inside it stays off any theme's frame.
        /// </summary>
        private static Vector4 FrameInsets11(Image image)
        {
            if(image==null||image.sprite==null||image.type!=Image.Type.Sliced)return Vector4.zero;
            var sprite=image.sprite;
            if(!spriteFrame11.TryGetValue(sprite,out var px))
            {
                px=Vector4.zero;var p=SpritePixels11(sprite,out int w,out int h);
                if(p!=null&&w>2&&h>2)
                {
                    var fill=p[(h/2)*w+w/2];var border=sprite.border;
                    int Inset(System.Func<int,Color32> at,int count,float limit)
                    {
                        int n=Mathf.Min(count/2,Mathf.RoundToInt(limit)),inset=0;
                        for(int i=0;i<n;i++){var c=at(i);if(c.a<128||Mathf.Abs(c.r-fill.r)+Mathf.Abs(c.g-fill.g)+Mathf.Abs(c.b-fill.b)>48)inset=i+1;}
                        return inset;
                    }
                    px=new Vector4(Inset(i=>p[(h/2)*w+i],w,border.x),Inset(i=>p[i*w+w/2],h,border.y),Inset(i=>p[(h/2)*w+w-1-i],w,border.z),Inset(i=>p[(h-1-i)*w+w/2],h,border.w));
                }
                spriteFrame11[sprite]=px;
            }
            return px/Mathf.Max(.0001f,image.pixelsPerUnit*image.pixelsPerUnitMultiplier);
        }
        /// <summary>
        /// V1.1 (W20Z2): the box of a label's drawn glyphs in the label's own layout coordinates (x right, y down from its
        /// top left): TMP's character boxes of its visible characters, the boxes the W11b frame checks read. (TMP's
        /// textBounds is wider: from the pen origin to the advance, and from the descender to the ascender line.) False when
        /// the label draws no glyph.
        /// </summary>
        private static bool GlyphBox11(TMPro.TextMeshProUGUI label,out Rect box)
        {
            box=default;if(label==null)return false;
            label.ForceMeshUpdate();var info=label.textInfo;var rect=label.rectTransform.rect;
            float left=float.MaxValue,right=float.MinValue,top=float.MinValue,bottom=float.MaxValue;bool any=false;
            for(int i=0;i<info.characterCount;i++)
            {
                var c=info.characterInfo[i];if(!c.isVisible||char.IsWhiteSpace(c.character))continue;
                any=true;left=Mathf.Min(left,c.bottomLeft.x);right=Mathf.Max(right,c.topRight.x);top=Mathf.Max(top,c.topRight.y);bottom=Mathf.Min(bottom,c.bottomLeft.y);
            }
            if(!any)return false;
            box=Rect.MinMaxRect(left-rect.xMin,rect.yMax-top,right-rect.xMin,rect.yMax-bottom);
            return true;
        }
        // Place the actual lettering between two visible edges, including a themed button's bottom lip.
        // Both edges and the label position use the parent’s top-left layout coordinates.
        private static void CenterLabelInk24(TMPro.TextMeshProUGUI label,float top,float bottom)
        {
            if(!GlyphBox11(label,out var ink))return;
            var rect=label.rectTransform;
            float centre=-rect.anchoredPosition.y+ink.center.y;
            rect.anchoredPosition+=Vector2.up*(centre-(top+bottom)*.5f);
        }

        /// <summary>The text box of a label centred on a chip as the player sees it (W11b), in the chip's own layout coordinates
        /// (x right, y down from its top-left): as wide as the inside of the chip's drawn frame and as tall as the chip,
        /// centred on that inside, so a chip with a lip at its foot does not push its label onto the lip.</summary>
        private static Rect ChipText11(RectTransform chip)
        {
            var f=FrameInsets11(chip!=null?chip.GetComponent<Image>():null);var size=chip!=null?chip.rect.size:Vector2.zero;
            float centre=f.w+Mathf.Max(1,size.y-f.w-f.y)*.5f;
            return new Rect(f.x,centre-size.y*.5f,Mathf.Max(1,size.x-f.x-f.z),size.y);
        }
        /// <summary>The surface colour as drawn under a label: sprite centre times the image tint (opaque). In a linear
        /// colour-space project the canvas multiplies and blends in linear light, so the product is taken there too.</summary>
        private static Color RenderedFill11(Image surface,Color? tintOverride=null,int depth=0)
        {
            var fill=surface.sprite!=null?SpriteFill11(surface.sprite):Color.white;var tint=tintOverride??surface.color;
            bool linear=QualitySettings.activeColorSpace==ColorSpace.Linear;
            Color c=linear?(fill.linear*tint.linear).gamma:fill*tint;c.a=1;float alpha=fill.a*tint.a;
            if(alpha<.98f)
            {
                // A see-through fill shows what lies under it: the next surface up, or the table felt.
                var under=depth<4&&surface.transform.parent!=null?surface.transform.parent.GetComponentInParent<Image>():null;
                Color back=under!=null?RenderedFill11(under,null,depth+1):UiKit.Felt;
                c=linear?Color.Lerp(back.linear,c.linear,alpha).gamma:Color.Lerp(back,c,alpha);c.a=1;
            }
            return c;
        }
        private static Color[] Candidates11=>new[]{UiKit.Ink,UiKit.Paper,UiKit.TextColor};
        private static Color BestLabel11(Color fill,out float ratio)
        {
            Color best=UiKit.Ink;ratio=0;
            foreach(var candidate in Candidates11){float r=Contrast11(candidate,fill);if(r>ratio){ratio=r;best=candidate;}}
            return best;
        }
        /// <summary>5 % steps of the tint toward <paramref name="toward"/> until <paramref name="label"/> reaches the pick
        /// contrast on the drawn fill; -1 when 24 steps are not enough.</summary>
        private static int StepsTo11(Image surface,Color toward,Color label)
        {
            Color tint=surface.color;float alpha=tint.a;
            for(int i=0;i<=24;i++)
            {
                if(Contrast11(label,RenderedFill11(surface,tint))>=PickContrast11)return i;
                tint=Color.Lerp(tint,toward,.05f);tint.a=alpha;
            }
            return -1;
        }
        /// <summary>
        /// Colour for a label on <paramref name="surface"/>. A meaning colour (<paramref name="preferred"/>, e.g. gold for
        /// money) stays when it reaches AA (4.5:1) on the drawn fill. Otherwise the label takes the palette colour (ink,
        /// paper, text colour) with the best contrast if that reaches the pick contrast (4.95:1); if none does, the fill is
        /// deepened under the lightest of them or lightened under the darkest, whichever takes fewer steps (a tie deepens,
        /// which keeps an accent's saturation).
        /// </summary>
        private static Color LabelOn11(Image surface,Color? preferred=null)
        {
            if(surface==null)return preferred??UiKit.TextColor;
            Color fill=RenderedFill11(surface);
            if(preferred.HasValue&&Contrast11(preferred.Value,fill)>=MinContrast11)return preferred.Value;
            Color best=BestLabel11(fill,out float ratio);
            if(ratio>=PickContrast11)return best;
            Color light=best,dark=best;
            foreach(var candidate in Candidates11)
            {
                if(Luminance11(candidate)>Luminance11(light))light=candidate;
                if(Luminance11(candidate)<Luminance11(dark))dark=candidate;
            }
            int deepen=StepsTo11(surface,Color.black,light),lighten=StepsTo11(surface,Color.white,dark);
            if(deepen<0&&lighten<0)return best;
            bool down=lighten<0||deepen>=0&&deepen<=lighten;
            Color tint=surface.color;float alpha=tint.a;
            for(int i=0,n=down?deepen:lighten;i<n;i++){tint=Color.Lerp(tint,down?Color.black:Color.white,.05f);tint.a=alpha;}
            surface.color=tint;
            return down?light:dark;
        }
        private static Color LabelOn11(RectTransform surface,Color? preferred=null)=>LabelOn11(surface!=null?surface.GetComponent<Image>():null,preferred);

        /// <summary>
        /// V1.1 (W11c, THEME_SPEC §2): a stage-state cue written as a number (the pressure target in the top bar and the
        /// settle panel's gap) keeps its hue. The cue colour stays when it reaches AA (4.5:1) on the drawn fill; otherwise
        /// it steps 5 % at a time toward white or black, whichever raises the contrast on that fill, at most 8 steps, and
        /// the first step that reaches 4.5:1 is used. Only when none does, the label takes the LabelOn11 pick.
        /// </summary>
        private static Color CueOn11(Image surface,Color cue)
        {
            if(surface==null)return cue;
            Color fill=RenderedFill11(surface);
            if(Contrast11(cue,fill)>=MinContrast11)return cue;
            Color Step(Color toward,int n){var c=Color.Lerp(cue,toward,.05f*n);c.a=cue.a;return c;}
            Color away=Contrast11(Step(Color.white,1),fill)>=Contrast11(Step(Color.black,1),fill)?Color.white:Color.black;
            for(int n=1;n<=8;n++){var c=Step(away,n);if(Contrast11(c,fill)>=MinContrast11)return c;}
            return LabelOn11(surface);
        }
        private static Color CueOn11(RectTransform surface,Color cue)=>CueOn11(surface!=null?surface.GetComponent<Image>():null,cue);
    }
}
