using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W20Xb item A (THEME_SPEC §3.1; W20X_REPORT §9.1): every theme that ships Table/table_scene.png has it
    /// drawn by the felt at its native size, filling the view, with no band of the dim room (_BaseColor × 0.24) above or
    /// below it; the same for Table/page_scene.png on the shop. Until W20Xb the theme pictures were imported at 2048 × 1024
    /// and the felt fitted that 2:1 picture into the 16:9 view: squeezed to 960 rows, with about 60 dark rows above and below.
    ///
    /// The felt is read as drawn: reduced motion (no grain, no light sweep), every canvas and every other renderer (the dice,
    /// and with them their shadows) switched off for one frame, the screen captured. It is compared with
    ///   (1) the imported picture resampled to the view by the GPU (Graphics.Blit, the felt's own sample points and sampler):
    ///       the placement, which must be the same picture at the same place, and
    ///   (2) the PNG file resampled on the CPU at the same sample points (bilinear in linear light, as an sRGB texture):
    ///       what was authored, so the difference also shows the import's compression.
    /// The four calm zones of THEME_SPEC §3.1 (scaled to the view) are measured against the kit's zone colour, read from the
    /// PNG; the rows at the top and bottom are searched for the band colour. Report and felt captures:
    /// artifacts/cache/v11/w20xb/&lt;size&gt;/w20xb-a/.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        // THEME_SPEC §3.1, 1920 x 1080 design pixels (x, y from the top, width, height).
        private static readonly RectInt[] CalmZones20Xb={new RectInt(52,374,1408,276),new RectInt(52,660,1408,60),new RectInt(344,740,1136,202),new RectInt(32,740,360,202)};
        private static readonly string[] CalmZoneNames20Xb={"dice area 52,374,1408,276","formula row 52,660,1408,60","submit bar 344,740,1136,202","submit note 32,740,360,202"};
        /// <summary>
        /// The placement must be the same picture at the same place. The felt computes its sample point from the pixel position
        /// (position / screen size, then the fit), the blit interpolates it; at an exact texel centre the two can differ by an
        /// ulp, and the GPU's bilinear weights (a few bits of sub-texel precision) then mix up to about 1/64 of the previous
        /// texel into the felt's pixel: invisible on flat colour, up to about 34 levels on a black texel next to a white one.
        /// So every drawn pixel must lie within the per-channel range of the imported texels around its sample point (the
        /// bilinear footprint and one texel more on each side), widened by this many levels (the camera's HDR target rounds the
        /// felt's colour once more than the blit does). A squeezed or shifted picture puts other content there, a band its
        /// own colour.
        /// </summary>
        private const int PlacementTolerance20Xb=3;
        /// <summary>The average difference from the GPU reference, in levels (flat colour differs by the HDR rounding only).</summary>
        private const double PlacementMeanTolerance20Xb=1.5;
        /// <summary>The average difference from the authored PNG, in levels: the placement plus the import's compression (the
        /// default table's CompressedHQ), which is reported in full but not judged pixel by pixel.</summary>
        private const double AuthoredMeanTolerance20Xb=2.0;
        /// <summary>A calm zone is a single colour in the PNG; as drawn, at most this many levels from it.</summary>
        private const int CalmZoneTolerance20Xb=2;

        private static string W20XbOutput(string name)
        {
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w20xb",Screen.width+"x"+Screen.height,name));
            Directory.CreateDirectory(dir);return dir;
        }
        /// <summary>A picture as Unity orders texture rows: row 0 at the bottom.</summary>
        private sealed class Picture20Xb{public int w,h;public Color32[] px;}
        private static Picture20Xb PngPicture20Xb(string path)
        {
            Assert.That(File.Exists(path),Is.True,path);
            var texture=new Texture2D(2,2,TextureFormat.RGBA32,false,false);
            try{Assert.That(texture.LoadImage(File.ReadAllBytes(path),false),Is.True,path);return new Picture20Xb{w=texture.width,h=texture.height,px=texture.GetPixels32()};}
            finally{UObj.Destroy(texture);}
        }
        /// <summary>(1) The imported texture resampled to the view by the GPU, as the felt samples it (screen UV, its sampler).</summary>
        private static Color32[] GpuResample20Xb(Texture source,int w,int h)
        {
            var target=RenderTexture.GetTemporary(w,h,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;
            try
            {
                Graphics.Blit(source,target);RenderTexture.active=target;
                var read=new Texture2D(w,h,TextureFormat.RGBA32,false,false);
                try{read.ReadPixels(new Rect(0,0,w,h),0,0,false);read.Apply(false,false);return read.GetPixels32();}
                finally{UObj.Destroy(read);}
            }
            finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(target);}
        }
        private static float[] linear20Xb;
        /// <summary>(2) The PNG resampled on the CPU at the felt's sample points (pixel centres of the view stretched over the
        /// picture), bilinear in linear light like a GPU filtering an sRGB texture without mipmaps.</summary>
        private static Color32[] CpuResample20Xb(Picture20Xb p,int w,int h)
        {
            if(linear20Xb==null){linear20Xb=new float[256];for(int i=0;i<256;i++)linear20Xb[i]=Mathf.GammaToLinearSpace(i/255f);}
            var lut=linear20Xb;var dst=new Color32[w*h];
            void Taps(int view,int size,int pixel,out int i0,out int i1,out float f)
            {
                float t=(pixel+.5f)*size/view-.5f;
                if(t<=0){i0=i1=0;f=0;return;}
                i0=Mathf.FloorToInt(t);f=t-i0;if(i0>=size-1){i0=i1=size-1;f=0;return;}i1=i0+1;
            }
            byte Channel(byte a,byte b,byte c,byte d,float fx,float fy)
            {
                float v=Mathf.Lerp(Mathf.Lerp(lut[a],lut[b],fx),Mathf.Lerp(lut[c],lut[d],fx),fy);
                return (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.LinearToGammaSpace(v)*255f),0,255);
            }
            for(int y=0;y<h;y++)
            {
                Taps(h,p.h,y,out int y0,out int y1,out float fy);
                for(int x=0;x<w;x++)
                {
                    Taps(w,p.w,x,out int x0,out int x1,out float fx);
                    Color32 a=p.px[y0*p.w+x0],b=p.px[y0*p.w+x1],c=p.px[y1*p.w+x0],d=p.px[y1*p.w+x1];
                    dst[y*w+x]=new Color32(Channel(a.r,b.r,c.r,d.r,fx,fy),Channel(a.g,b.g,c.g,d.g,fx,fy),Channel(a.b,b.b,c.b,d.b,fx,fy),255);
                }
            }
            return dst;
        }
        /// <summary>The felt alone, as drawn: every canvas and every other renderer (dice, their shadows) off for one frame.</summary>
        private IEnumerator FeltOnly20Xb(Action<Color32[]> read)
        {
            var felt=GameObject.Find("Art07Felt").GetComponent<Renderer>();
            var canvases=UObj.FindObjectsByType<Canvas>().Where(c=>c.enabled).ToList();
            var others=UObj.FindObjectsByType<Renderer>().Where(r=>r.enabled&&r!=felt).ToList();
            foreach(var c in canvases)c.enabled=false;foreach(var r in others)r.enabled=false;
            try
            {
                yield return null;yield return new WaitForEndOfFrame();
                var shot=ScreenCapture.CaptureScreenshotAsTexture();
                try{Assert.That(shot.width==Screen.width&&shot.height==Screen.height,Is.True,"Screen-sized capture.");read(shot.GetPixels32());}
                finally{UObj.Destroy(shot);}
            }
            finally{foreach(var c in canvases)if(c!=null)c.enabled=true;foreach(var r in others)if(r!=null)r.enabled=true;}
        }
        private static int Delta20Xb(Color32 a,Color32 b)=>Math.Max(Math.Abs(a.r-b.r),Math.Max(Math.Abs(a.g-b.g),Math.Abs(a.b-b.b)));
        /// <summary>Pixels of <paramref name="drawn"/> (the view, w x h) outside the per-channel range of the texels around
        /// their sample point (the texture stretched over the view, as the felt draws a 16:9 picture: the bilinear footprint and
        /// one texel more on each side) by more than <paramref name="tolerance"/> levels (see <see cref="PlacementTolerance20Xb"/>).</summary>
        private static int OutsideFootprint20Xb(Color32[] drawn,int w,int h,Color32[] texels,int tw,int th,int tolerance,out int worst)
        {
            int outside=0;worst=0;
            for(int y=0;y<h;y++)
            {
                float ty=(y+.5f)*th/h-.5f;int y0=Math.Max(0,Mathf.FloorToInt(ty)-1),y1=Math.Min(th-1,Mathf.CeilToInt(ty)+1);
                for(int x=0;x<w;x++)
                {
                    float tx=(x+.5f)*tw/w-.5f;int x0=Math.Max(0,Mathf.FloorToInt(tx)-1),x1=Math.Min(tw-1,Mathf.CeilToInt(tx)+1);
                    int rl=255,gl=255,bl=255,rh=0,gh=0,bh=0;
                    for(int yy=y0;yy<=y1;yy++)for(int xx=x0;xx<=x1;xx++)
                    {
                        var c=texels[yy*tw+xx];
                        if(c.r<rl)rl=c.r;if(c.r>rh)rh=c.r;if(c.g<gl)gl=c.g;if(c.g>gh)gh=c.g;if(c.b<bl)bl=c.b;if(c.b>bh)bh=c.b;
                    }
                    var d=drawn[y*w+x];
                    int e=Math.Max(Math.Max(Math.Max(rl-d.r,d.r-rh),Math.Max(gl-d.g,d.g-gh)),Math.Max(bl-d.b,d.b-bh));
                    if(e>tolerance){outside++;if(e>worst)worst=e;}
                }
            }
            return outside;
        }
        private static string Stats20Xb(Color32[] drawn,Color32[] reference,out int max,out double mean)
        {
            var histogram=new long[256];long sum=0;max=0;
            for(int i=0;i<drawn.Length;i++){int d=Delta20Xb(drawn[i],reference[i]);histogram[d]++;sum+=d;if(d>max)max=d;}
            mean=(double)sum/drawn.Length;
            long target=(long)Math.Ceiling(drawn.Length*.999),seen=0;int p999=0;for(int d=0;d<256;d++){seen+=histogram[d];if(seen>=target){p999=d;break;}}
            long over2=histogram.Skip(3).Sum(),over8=histogram.Skip(9).Sum();
            return "max "+max+", mean "+mean.ToString("0.000",CultureInfo.InvariantCulture)+", 99.9% within "+p999+", pixels over 2: "+over2+", over 8: "+over8+" (of "+drawn.Length+")";
        }

        /// <summary>One picture of one theme: placement, authored picture, calm zones (table_scene only) and bands.</summary>
        private void FeltCheck20Xb(GameTheme theme,string screen,string file,Texture imported,Color32[] drawn,bool zones,List<string> report,List<string> failures)
        {
            int w=Screen.width,h=Screen.height;string scope=theme.Id+" "+screen+" ("+file+")";
            string path=Path.Combine(Application.dataPath,"Resources","Themes",theme.Id,"Table",file);
            var png=PngPicture20Xb(path);
            // Native size: the imported texture is the PNG's own size, without mipmaps (as the default table, bg_table_scene.png).
            report.Add(scope+": PNG "+png.w+"x"+png.h+", imported "+imported.width+"x"+imported.height+", mipmaps "+imported.mipmapCount+", "+imported.filterMode+", "+imported.wrapMode);
            if(imported.width!=png.w||imported.height!=png.h)failures.Add(scope+": imported at "+imported.width+"x"+imported.height+", the PNG is "+png.w+"x"+png.h);
            if(imported.mipmapCount!=1)failures.Add(scope+": imported with "+imported.mipmapCount+" mip levels (the default table has none)");
            // (1) Placement.
            var gpu=GpuResample20Xb(imported,w,h);
            var texels=GpuResample20Xb(imported,imported.width,imported.height);
            int outside=OutsideFootprint20Xb(drawn,w,h,texels,imported.width,imported.height,PlacementTolerance20Xb,out int worst);
            report.Add(scope+": drawn vs the imported picture resampled by the GPU: "+Stats20Xb(drawn,gpu,out _,out double placementMean)+"; pixels outside the range of the texels around their sample point by more than "+PlacementTolerance20Xb+": "+outside+(outside>0?" (worst by "+worst+")":""));
            if(outside>0||placementMean>PlacementMeanTolerance20Xb)
            {
                failures.Add(scope+": the felt does not draw the imported picture in place ("+outside+" pixels outside their neighbourhood, worst by "+worst+"; mean difference "+placementMean.ToString("0.000",CultureInfo.InvariantCulture)+")");
                // Diagnosis: the GPU reference next to the felt capture.
                var reference=new Texture2D(w,h,TextureFormat.RGBA32,false,false);
                try{reference.SetPixels32(gpu);reference.Apply(false,false);File.WriteAllBytes(Path.Combine(W20XbOutput("w20xb-a"),theme.Id+"-"+screen+"-gpu-reference.png"),reference.EncodeToPNG());}
                finally{UObj.Destroy(reference);}
            }
            // (2) Authored.
            var authored=CpuResample20Xb(png,w,h);
            report.Add(scope+": drawn vs the PNG resampled on the CPU: "+Stats20Xb(drawn,authored,out _,out double authoredMean));
            if(authoredMean>AuthoredMeanTolerance20Xb)failures.Add(scope+": the felt draws a different picture from the PNG (mean difference "+authoredMean.ToString("0.000",CultureInfo.InvariantCulture)+")");
            report.Add(scope+": imported vs the PNG (both resampled to the view, the compression): "+Stats20Xb(gpu,authored,out _,out _));
            // Calm zones: the pixels whose bilinear footprint lies inside the zone, against the zone colour of the PNG.
            float sx=(float)png.w/w,sy=(float)png.h/h;
            if(zones)for(int z=0;z<CalmZones20Xb.Length;z++)
            {
                var r=CalmZones20Xb[z];int bottom=png.h-r.y-r.height,top=png.h-r.y-1;// texture rows, from the bottom
                var zone=png.px[bottom*png.w+r.x];int other=0;
                for(int ty=bottom;ty<=top;ty++)for(int tx=r.x;tx<r.x+r.width;tx++)if(Delta20Xb(png.px[ty*png.w+tx],zone)>0)other++;
                if(other>0){report.Add(scope+" calm zone "+CalmZoneNames20Xb[z]+": "+other+" pixels of the PNG differ from #"+ColorUtility.ToHtmlStringRGB(zone)+", not one colour");failures.Add(scope+": calm zone "+CalmZoneNames20Xb[z]+" is not one colour in the PNG");continue;}
                int inner=0,innerMax=0,rect=0,rectMax=0,rectVsAuthored=0;
                for(int y=0;y<h;y++)
                {
                    float ty=(y+.5f)*sy-.5f;int y0=Mathf.FloorToInt(ty),y1=Mathf.CeilToInt(ty);float cy=(y+.5f)*sy;
                    for(int x=0;x<w;x++)
                    {
                        float tx=(x+.5f)*sx-.5f;int x0=Mathf.FloorToInt(tx),x1=Mathf.CeilToInt(tx);float cx=(x+.5f)*sx;
                        bool centre=cx>=r.x&&cx<r.x+r.width&&cy>=bottom&&cy<top+1;
                        if(!centre)continue;
                        var seen=drawn[y*w+x];int d=Delta20Xb(seen,zone);rect++;rectMax=Math.Max(rectMax,d);rectVsAuthored=Math.Max(rectVsAuthored,Delta20Xb(seen,authored[y*w+x]));
                        if(x0>=r.x&&x1<r.x+r.width&&y0>=bottom&&y1<=top){inner++;innerMax=Math.Max(innerMax,d);}
                    }
                }
                report.Add(scope+" calm zone "+CalmZoneNames20Xb[z]+": zone colour #"+ColorUtility.ToHtmlStringRGB(zone)+"; largest difference "+innerMax+" over "+inner+" pixels sampling only the zone, "+rectMax+" over all "+rect+" pixels of the scaled rect (its edge pixels blend the neighbours: "+rectVsAuthored+" against the resampled PNG)");
                if(inner==0||innerMax>CalmZoneTolerance20Xb)failures.Add(scope+": calm zone "+CalmZoneNames20Xb[z]+" drawn "+innerMax+" levels from #"+ColorUtility.ToHtmlStringRGB(zone)+" ("+inner+" pixels)");
            }
            // Bands: rows at the top and bottom of the view drawn in the dim room's colour (_BaseColor x 0.24, linear light).
            var felt=theme.Palette.felt.linear;var band=(Color32)new Color(felt.r*.24f,felt.g*.24f,felt.b*.24f,1).gamma;
            int edge=Mathf.CeilToInt(70f/sy),bandRows=0,pictureBandRows=0;
            foreach(int y in Enumerable.Range(0,edge).Concat(Enumerable.Range(h-edge,edge)))
            {
                int drawnBand=0,authoredBand=0;
                for(int x=0;x<w;x++){if(Delta20Xb(drawn[y*w+x],band)<=2)drawnBand++;if(Delta20Xb(authored[y*w+x],band)<=2)authoredBand++;}
                if(drawnBand>=w*.99f){bandRows++;if(authoredBand>=w*.99f)pictureBandRows++;}
            }
            report.Add(scope+": band colour #"+ColorUtility.ToHtmlStringRGB(band)+": "+bandRows+" of the top and bottom "+edge+" rows drawn in it ("+pictureBandRows+" of them are that colour in the picture itself)");
            if(bandRows>pictureBandRows)failures.Add(scope+": "+(bandRows-pictureBandRows)+" rows at the top or bottom are the dim band #"+ColorUtility.ToHtmlStringRGB(band));
            // Evidence: the felt as drawn.
            var shot=new Texture2D(w,h,TextureFormat.RGBA32,false,false);
            try{shot.SetPixels32(drawn);shot.Apply(false,false);File.WriteAllBytes(Path.Combine(W20XbOutput("w20xb-a"),theme.Id+"-"+screen+"-felt.png"),shot.EncodeToPNG());}
            finally{UObj.Destroy(shot);}
        }

        [UnityTest] public IEnumerator PLAY_W20XB_A_ThemeTablePicturesDrawnAtNativeSizeWithoutBands()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Run at actual 1920x1080 or 1280x720.");
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();var failures=new List<string>();
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);PlayerPrefs.SetInt("reduced",1);
                yield return LoadBootstrap();
                Assert.That(UiKit.ReducedMotion,Is.True,"Reduced motion: the felt adds no grain or light sweep.");
                // Every theme folder that ships a table_scene.png is checked (a new kit is covered without editing this test).
                string themesRoot=Path.Combine(Application.dataPath,"Resources","Themes");
                var shipped=Directory.GetDirectories(themesRoot).Where(d=>File.Exists(Path.Combine(d,"Table","table_scene.png"))).Select(Path.GetFileName).OrderBy(id=>id,StringComparer.Ordinal).ToList();
                var themes=ThemeRegistry.All.Where(t=>!t.IsDefault&&!t.Id.StartsWith("w20x",StringComparison.Ordinal)).ToList();
                foreach(var t in themes)t.EnsureLoaded();
                var withTable=themes.Where(t=>t.TableScene!=null).OrderBy(t=>t.Id,StringComparer.Ordinal).ToList();
                Assert.That(withTable.Select(t=>t.Id),Is.EqualTo(shipped),"Every shipped table_scene.png belongs to a registered theme.");
                Assert.That(withTable,Is.Not.Empty);
                report.Add("view "+Screen.width+"x"+Screen.height+"; themes with table_scene: "+string.Join(", ",withTable.Select(t=>t.Id))+"; with page_scene: "+string.Join(", ",withTable.Where(t=>t.PageScene!=null).Select(t=>t.Id)));
                // The play screen: table_scene (a stage before the boss: no dim, no vignette).
                StartRun("72011");
                // Chapter one is the authored colour. W18 measures the later chapter tints in their own real play states.
                W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true,stageIndex:0));yield return W11Settle();
                foreach(var theme in withTable)
                {
                    app.ApplyTheme11(theme.Id);yield return Art073Settle(.35f);
                    Assert.That(app.PlayBackdrop11,Is.True);Assert.That(FeltPicture20X(),Is.SameAs(theme.TableScene),theme.Id+": the play screen draws table_scene.");
                    Color32[] drawn=null;yield return FeltOnly20Xb(px=>drawn=px);
                    FeltCheck20Xb(theme,"play","table_scene.png",theme.TableScene,drawn,true,report,failures);
                }
                // The shop: page_scene where a theme ships one.
                yield return W14Load(W14ShopFixture(false));
                foreach(var theme in withTable.Where(t=>t.PageScene!=null))
                {
                    app.ApplyTheme11(theme.Id);yield return Art073Settle(.35f);
                    Assert.That(app.PlayBackdrop11,Is.False);Assert.That(FeltPicture20X(),Is.SameAs(theme.PageScene),theme.Id+": the shop draws page_scene.");
                    Color32[] drawn=null;yield return FeltOnly20Xb(px=>drawn=px);
                    FeltCheck20Xb(theme,"shop","page_scene.png",theme.PageScene,drawn,false,report,failures);
                }
            }
            finally
            {
                app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                File.WriteAllLines(Path.Combine(W20XbOutput("w20xb-a"),"w20xb-a.txt"),report.Concat(new[]{"","PROBLEMS "+failures.Count}).Concat(failures));
            }
            foreach(string line in report)TestContext.WriteLine(line);
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(40)));
        }
    }
}
