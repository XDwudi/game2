using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W22i (docs/specs/v11/W22I_REPORT.md): the five styles' card illustrations, boss emblems and compact icons
    /// in the game.
    ///   01 Every style has its own picture for every content id (184 illustrations, 7 emblems, 184 compact icons; missing
    ///      0), and wherever the card gallery, the shop and the play row draw a card picture, it is the style's own: never the
    ///      default style's sprite.
    ///   02 The compact icon stays in the style (CARD_TEMPLATE 7 as changed by W22i): the style's icon, then its own whole
    ///      illustration, then the default icon, then the default illustration; a style without art of its own shows the
    ///      default's.
    ///   03 The 我的构筑 tiles keep at least 2 units inside the drawn frame of their panel (W20Z2 finding 2), measured as W20Z2
    ///      measured its labels: the panel's frame as the W11b visible-frame check reads it (FrameInsets11). Six styles, 100 %
    ///      and 150 % text, the common and the full shop. The styles whose frame leaves the room keep the W14 row.
    ///   04 What loading a style's art costs (W22i item 6): the first switch to each style on the shop, in time and texture
    ///      memory, the whole set of a style, and the totals after all six styles. A report, not a gate.
    ///   05 Content art loads per picture on first use and the switch to another style releases the previous style's
    ///      pictures (W22i item 6: GameTheme.ContentArt, ThemeRegistry.Activate); reloaded pictures draw the same pixels.
    /// Reports: artifacts/cache/v11/w22i/&lt;width&gt;x&lt;height&gt;/&lt;test&gt;/.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private static readonly string[] Styles22i={"swiss","sancai","comic","deco","glass"};
        private static readonly string[] Families22i={"seal","constraint","multiplier","cards","economy","target","final"};
        private static readonly MethodInfo ContentSprite22i=typeof(GameApp).GetMethod("ContentSprite071",BindingFlags.NonPublic|BindingFlags.Instance);
        private static readonly MethodInfo CompactIcon22i=typeof(GameApp).GetMethod("CompactIcon13",BindingFlags.NonPublic|BindingFlags.Instance);
        private string[] ContentIds22i=>app.Config.controllers.Select(c=>c.id).Concat(app.Config.consumables.Select(c=>c.id))
            .Concat(app.Config.services.Select(c=>c.id)).Concat(app.Config.diceUpgrades.Select(c=>c.id)).ToArray();
        private static string W22iOutput(string name)
        {
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w22i",Screen.width+"x"+Screen.height,name));
            Directory.CreateDirectory(dir);return dir;
        }
        private Sprite ContentSprite22(string id)=>(Sprite)ContentSprite22i.Invoke(app,new object[]{id});
        private Sprite CompactIcon22(string id)=>(Sprite)CompactIcon22i.Invoke(app,new object[]{id});
        private CompactIconSet13 IconSet22(string theme)=>app.compactIcons13.FirstOrDefault(s=>s!=null&&s.theme==theme);
        private static Sprite IconOf22(CompactIconSet13 set,string id)=>set?.icons?.FirstOrDefault(s=>s!=null&&s.name=="icon_"+id);
        /// <summary>Runs <paramref name="body"/> with preview styles listed and restores the player's settings afterwards.</summary>
        private IEnumerator WithStyles22(Func<IEnumerator> body)
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            try{PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);yield return body();}
            finally
            {
                app.HideCardGallery13();W11TextScale(100);GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
            }
        }

        /// <summary>The card pictures drawn under a root (Icon_&lt;content id&gt;, the template's and the shop's), each checked to be
        /// the style's own illustration or compact icon of that id. Returns how many were checked.</summary>
        private int OwnPictures22(Transform root,string theme,string scope,List<string> failures)
        {
            var style=ThemeRegistry.Find(theme);var set=IconSet22(theme);var ids=new HashSet<string>(ContentIds22i);
            var defaults=new HashSet<Sprite>(app.contentIcons.Where(s=>s!=null));
            var defaultIcons=new HashSet<Sprite>(IconSet22(GameTheme.DefaultId)?.icons?.Where(s=>s!=null)??Enumerable.Empty<Sprite>());
            int checkedPictures=0;
            foreach(var image in root.GetComponentsInChildren<Image>(false).Where(i=>i.gameObject.activeInHierarchy&&i.name.StartsWith("Icon_",StringComparison.Ordinal)))
            {
                string id=image.name.Substring(5);if(!ids.Contains(id))continue;
                checkedPictures++;var sprite=image.sprite;string at=scope+" "+W11bPath(image.transform);
                if(sprite==null){failures.Add(at+": no picture");continue;}
                if(defaults.Contains(sprite)||defaultIcons.Contains(sprite)){failures.Add(at+": the default style's "+sprite.name);continue;}
                if(!ReferenceEquals(sprite,style.ContentArt("illustration_"+id))&&!ReferenceEquals(sprite,IconOf22(set,id)))failures.Add(at+": "+sprite.name+" is not "+theme+"'s picture of "+id);
            }
            return checkedPictures;
        }

        [UnityTest,Timeout(900000)] public IEnumerator PLAY_W22I_01_EveryStyleShowsItsOwnArtWherePicturesAppear()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Run at actual 1920x1080 or 1280x720.");
            var report=new List<string>();var failures=new List<string>();
            yield return WithStyles22(()=>Run());
            File.WriteAllLines(Path.Combine(W22iOutput("w22i-01"),"w22i-01.txt"),report.Concat(new[]{"","PROBLEMS "+failures.Count}).Concat(failures));
            foreach(string line in report)TestContext.WriteLine(line);
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(40)));

            IEnumerator Run()
            {
                GameApp.CleanCapture11=true;
                foreach(string theme in Styles22i)Assert.That(ThemeRegistry.Find(theme),Is.Not.Null,theme+" is offered (with previews)");
                var ids=ContentIds22i;Assert.That(ids.Length,Is.EqualTo(184));
                var pictures=Styles22i.ToDictionary(t=>t,t=>new Dictionary<string,int>());
                void Count(string theme,string where,int n){pictures[theme][where]=n;if(n==0)failures.Add(theme+" "+where+": no card picture drawn");}
                StartRun("72201");
                foreach(string theme in Styles22i)
                {
                    app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));yield return null;
                    var style=ThemeRegistry.Current;var set=IconSet22(theme);
                    int missingArt=0,missingIcons=0,missingEmblems=0,oversize=0,notOwnLookup=0;
                    foreach(string id in ids)
                    {
                        var art=style.ContentArt("illustration_"+id);
                        if(art==null){missingArt++;failures.Add(theme+": no illustration_"+id);}
                        else
                        {
                            if(art.texture.width>1024||art.texture.height>1024){oversize++;failures.Add(theme+": illustration_"+id+" is "+art.texture.width+"x"+art.texture.height+" (import cap 1024)");}
                            if(!ReferenceEquals(ContentSprite22(id),art)){notOwnLookup++;failures.Add(theme+": ContentSprite071("+id+") is not its own illustration");}
                        }
                        var icon=IconOf22(set,id);
                        if(icon==null){missingIcons++;failures.Add(theme+": no icon_"+id);}
                        else if(!ReferenceEquals(CompactIcon22(id),icon)){notOwnLookup++;failures.Add(theme+": CompactIcon13("+id+") is "+(CompactIcon22(id)?.name??"null")+", not its own icon");}
                    }
                    foreach(string family in Families22i)
                    {
                        var emblem=style.ContentArt("emblem_"+family);
                        if(emblem==null){missingEmblems++;failures.Add(theme+": no emblem_"+family);}
                        else if(emblem.texture.width>1024||emblem.texture.height>1024){oversize++;failures.Add(theme+": emblem_"+family+" is "+emblem.texture.width+"x"+emblem.texture.height);}
                    }
                    if(set==null)failures.Add(theme+": no bound icon set");
                    else if(set.icons.Length!=ids.Length)failures.Add(theme+": "+set.icons.Length+" bound icons");
                    report.Add(theme+": missing illustrations "+missingArt+", emblems "+missingEmblems+", compact icons "+missingIcons+"; over 1024 px "+oversize+"; lookups not the style's own "+notOwnLookup);
                    // Where pictures are drawn: every gallery page and the play row (three full cards; ten mini cards).
                    int gallery=0;
                    for(int page=0;page<GameApp.CardGalleryPages13;page++)
                    {var root=app.ShowCardGallery13(page);yield return null;Canvas.ForceUpdateCanvases();gallery+=OwnPictures22(root,theme,theme+" gallery p"+page,failures);}
                    app.HideCardGallery13();Count(theme,"gallery",gallery);
                    W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));yield return W11Settle();
                    Count(theme,"play, three cards",OwnPictures22(app.canvas.transform,theme,theme+" play (three cards)",failures));
                    W11Apply(W13TenCards());yield return W11Settle();
                    Count(theme,"play, ten cards",OwnPictures22(app.canvas.transform,theme,theme+" play (ten cards)",failures));
                }
                // The shops (a fixture load restarts the scene; the style is applied again on it).
                foreach(bool full in new[]{false,true})
                {
                    yield return W14Load(W14ShopFixture(full));
                    foreach(string theme in Styles22i)
                    {
                        app.ApplyTheme11(theme);yield return Art073Settle(.3f);
                        Count(theme,full?"full shop":"common shop",OwnPictures22(app.canvas.transform,theme,theme+(full?" full shop":" common shop"),failures));
                    }
                }
                foreach(string theme in Styles22i)report.Add(theme+": card pictures checked: "+string.Join(", ",pictures[theme].Select(p=>p.Key+" "+p.Value))+" (total "+pictures[theme].Values.Sum()+")");
            }
        }

        [UnityTest] public IEnumerator PLAY_W22I_02_CompactIconStaysInTheStyle()
        {
            var report=new List<string>();var saved=app.compactIcons13;
            const string probeId="w22i-probe";
            try
            {
                yield return WithStyles22(()=>Run());
            }
            finally
            {
                app.compactIcons13=saved;ThemeRegistry.UnregisterForTests(probeId);app.ApplyTheme11(GameTheme.DefaultId);
                File.WriteAllLines(Path.Combine(W22iOutput("w22i-02"),"w22i-02.txt"),report);
            }

            IEnumerator Run()
            {
                StartRun("72202");yield return null;
                var defaults=IconSet22(GameTheme.DefaultId);Assert.That(defaults,Is.Not.Null);
                string Name(Sprite s)=>s==null?"null":s.name+" ("+(IconSets22(s)??"illustration")+")";
                foreach(string theme in Styles22i)
                {
                    var set=IconSet22(theme);Assert.That(set,Is.Not.Null,theme+" has a bound icon set");
                    app.compactIcons13=saved;app.ApplyTheme11(theme);
                    Assert.That(GameApp.CompactIconCacheCount13,Is.Zero,"A theme switch empties the cache.");
                    var style=ThemeRegistry.Current;var own=style.ContentArt("illustration_C01");Assert.That(own,Is.Not.Null,theme+" draws its own C01");
                    Assert.That(CompactIcon22("C01"),Is.SameAs(IconOf22(set,"C01")),theme+": its own icon first");
                    // Without its icon: its own whole illustration, never the default icon that is still bound.
                    app.compactIcons13=saved.Select(s=>s.theme==theme?new CompactIconSet13{theme=s.theme,icons=s.icons.Where(i=>i.name!="icon_C01").ToArray()}:s).ToArray();
                    app.ApplyTheme11(theme);
                    Assert.That(IconOf22(defaults,"C01"),Is.Not.Null,"the default icon is still there");
                    Assert.That(CompactIcon22("C01"),Is.SameAs(own),theme+": then its own illustration, not the default icon");
                    report.Add(theme+": C01 -> "+Name(IconOf22(set,"C01"))+"; without its icon -> "+Name(CompactIcon22("C01")));
                    // Cached per (theme, id).
                    int count=GameApp.CompactIconCacheCount13;CompactIcon22("C01");Assert.That(GameApp.CompactIconCacheCount13,Is.EqualTo(count),"cached per (theme, id)");
                }
                // A style without art of its own (an in-memory test style): the default icon, then the default illustration.
                app.compactIcons13=saved;
                var probe=GameTheme.CreateForTests(probeId,new ThemePalette());ThemeRegistry.RegisterForTests(probe);
                app.ApplyTheme11(probeId);Assert.That(app.ThemeId11,Is.EqualTo(probeId));
                Assert.That(probe.ContentArt("illustration_C01"),Is.Null,"the probe has no art");
                Assert.That(CompactIcon22("C01"),Is.SameAs(IconOf22(defaults,"C01")),"a style without art: the default icon");
                app.compactIcons13=saved.Select(s=>new CompactIconSet13{theme=s.theme,icons=s.icons.Where(i=>i.name!="icon_C01").ToArray()}).ToArray();
                app.ApplyTheme11(probeId);
                var defaultArt=ContentSprite22("C01");Assert.That(defaultArt,Is.Not.Null);Assert.That(app.contentIcons,Does.Contain(defaultArt),"the default illustration");
                Assert.That(CompactIcon22("C01"),Is.SameAs(defaultArt),"then the default illustration");
                report.Add(probeId+" (no art): C01 -> "+Name(IconOf22(defaults,"C01"))+"; without the default icon -> "+Name(CompactIcon22("C01")));
                // The default style: its icon, then its illustration.
                app.compactIcons13=saved;app.ApplyTheme11(GameTheme.DefaultId);
                Assert.That(CompactIcon22("C01"),Is.SameAs(IconOf22(defaults,"C01")));
                app.compactIcons13=saved.Select(s=>new CompactIconSet13{theme=s.theme,icons=s.icons.Where(i=>i.name!="icon_C01").ToArray()}).ToArray();
                app.ApplyTheme11(GameTheme.DefaultId);
                Assert.That(CompactIcon22("C01"),Is.SameAs(ContentSprite22("C01")));Assert.That(app.contentIcons,Does.Contain(CompactIcon22("C01")));
                report.Add("default: C01 -> "+Name(IconOf22(defaults,"C01"))+"; without its icon -> "+Name(CompactIcon22("C01")));
                app.compactIcons13=saved;
            }
        }
        /// <summary>The theme of the bound icon set holding <paramref name="sprite"/>, or null.</summary>
        private string IconSets22(Sprite sprite)=>app.compactIcons13.FirstOrDefault(s=>s?.icons!=null&&s.icons.Contains(sprite))?.theme;

        [UnityTest,Timeout(900000)] public IEnumerator PLAY_W22I_03_BuildRowTilesKeepTwoUnitsInsideTheFrame()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Run at actual 1920x1080 or 1280x720.");
            var report=new List<string>();var failures=new List<string>();
            yield return WithStyles22(()=>Run());
            File.WriteAllLines(Path.Combine(W22iOutput("w22i-03"),"w22i-03.txt"),report.Concat(new[]{"","PROBLEMS "+failures.Count}).Concat(failures));
            foreach(string line in report.Take(12))TestContext.WriteLine(line);
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(40)));

            IEnumerator Run()
            {
                GameApp.CleanCapture11=true;
                const float Clearance=2f,Slack=.05f;
                float unit=Screen.width/1920f;string F(float v)=>v.ToString("0.0",CultureInfo.InvariantCulture);
                var styles=new[]{GameTheme.DefaultId}.Concat(Styles22i).ToArray();
                foreach(bool full in new[]{false,true})
                {
                    yield return W14Load(W14ShopFixture(full));
                    int cards=app.Session.State.controllers.Count;
                    if(full)Assert.That(cards,Is.EqualTo(10),"the full shop's ten cards");
                    if(cards==0){report.Add((full?"full":"common")+" shop: no card held, no tile");continue;}
                    foreach(string theme in styles)foreach(int text in new[]{100,150})
                    {
                        app.ApplyTheme11(theme);W11TextScale(text);yield return null;Canvas.ForceUpdateCanvases();
                        string scope=theme+" "+text+"% "+(full?"full":"common")+" shop";
                        var panel=FindActive<Image>("Build14");var inner=W11bInnerRect(panel,false);
                        var tiles=panel.transform.Cast<Transform>().Where(t=>t.gameObject.activeSelf&&t.name.StartsWith("Controller_",StringComparison.Ordinal)).Select(t=>(RectTransform)t).ToList();
                        if(tiles.Count!=Math.Min(cards,10)){failures.Add(scope+": "+tiles.Count+" tiles for "+cards+" cards");continue;}
                        float left=float.MaxValue,right=float.MaxValue,top=float.MaxValue,bottom=float.MaxValue;
                        foreach(var tile in tiles)
                        {
                            var r=V06ScreenRect(tile);
                            left=Mathf.Min(left,(r.xMin-inner.xMin)/unit);right=Mathf.Min(right,(inner.xMax-r.xMax)/unit);
                            bottom=Mathf.Min(bottom,(r.yMin-inner.yMin)/unit);top=Mathf.Min(top,(inner.yMax-r.yMax)/unit);
                        }
                        var first=tiles.OrderBy(t=>t.anchoredPosition.x).First();
                        report.Add(scope+": "+tiles.Count+" tiles "+F(first.sizeDelta.x)+"x"+F(first.sizeDelta.y)+" from x "+F(first.anchoredPosition.x)+"; inside the drawn frame by left "+F(left)+", right "+F(right)+", top "+F(top)+", bottom "+F(bottom)+" units");
                        float inside=Mathf.Min(Mathf.Min(left,right),Mathf.Min(top,bottom));
                        if(inside<Clearance-Slack)failures.Add(scope+": a tile is "+F(inside)+" units inside the drawn frame of Build14 (at least "+F(Clearance)+")");
                        // A style whose frame leaves the room keeps the W14 row: 12 units in, 104 (150 %: 116) units tall.
                        if(theme=="default"||theme=="swiss")
                        {
                            float tall=UiKit.FontScale>1.01f?116:104;
                            if(Mathf.Abs(first.anchoredPosition.x-12)>.01f||tiles.Any(t=>Mathf.Abs(t.sizeDelta.y-tall)>.01f))failures.Add(scope+": the row moved (x "+F(first.anchoredPosition.x)+", height "+F(first.sizeDelta.y)+")");
                        }
                    }
                    W11TextScale(100);
                }
            }
        }

        /// <summary>One texture's memory: the Profiler's runtime size and the size of its data (every mip, the format's block
        /// size), which is also what a player build stores for it.</summary>
        private static void TextureBytes22(Texture texture,out long runtime,out long data)
        {
            runtime=Profiler.GetRuntimeMemorySizeLong(texture);data=0;
            for(int m=0;m<Mathf.Max(1,texture.mipmapCount);m++)data+=GraphicsFormatUtility.ComputeMipmapSize(Mathf.Max(1,texture.width>>m),Mathf.Max(1,texture.height>>m),texture.graphicsFormat);
        }
        private sealed class TextureCount22{public int count;public long runtime,data;}
        /// <summary>Every loaded 2D texture: totals, and the part named illustration_ / emblem_ (a style's art; the default's
        /// illustrations are bound in the scene and loaded from the start) and icon_.</summary>
        private static Dictionary<string,TextureCount22> Textures22()
        {
            var result=new Dictionary<string,TextureCount22>{{"all",new TextureCount22()},{"art",new TextureCount22()},{"icons",new TextureCount22()}};
            foreach(var t in Resources.FindObjectsOfTypeAll<Texture2D>())
            {
                TextureBytes22(t,out long runtime,out long data);
                void Add(string key){var c=result[key];c.count++;c.runtime+=runtime;c.data+=data;}
                Add("all");
                if(t.name.StartsWith("illustration_",StringComparison.Ordinal)||t.name.StartsWith("emblem_",StringComparison.Ordinal))Add("art");
                else if(t.name.StartsWith("icon_",StringComparison.Ordinal))Add("icons");
            }
            return result;
        }
        private static string Mb22(long bytes)=>(bytes/1048576.0).ToString("0.0",CultureInfo.InvariantCulture)+" MiB";

        /// <summary>
        /// 05 (W22i item 6, lazy loading): a style loads a content picture when it is first asked for, and the switch to
        /// another style releases the pictures the previous one loaded: none of their textures stays loaded. Back on the
        /// style, the pictures load again and draw the same pixels (the card gallery's first page, captured before and after).
        /// </summary>
        [UnityTest,Timeout(900000)] public IEnumerator PLAY_W22I_05_SwitchingStylesReleasesThePreviousStylesArt()
        {
            var report=new List<string>();
            yield return WithStyles22(()=>Run());
            File.WriteAllLines(Path.Combine(W22iOutput("w22i-05"),"w22i-05.txt"),report);
            foreach(string line in report)TestContext.WriteLine(line);

            IEnumerator Run()
            {
                GameApp.CleanCapture11=true;StartRun("72205");yield return null;
                var ids=ContentIds22i;
                app.ApplyTheme11("sancai");app.ApplyTheme11("swiss");
                var swiss=ThemeRegistry.Current;Assert.That(swiss.Id,Is.EqualTo("swiss"));
                Assert.That(swiss.ContentArtCount,Is.LessThan(ids.Length),"a switch does not load the whole set");
                // The gallery's first page, then every picture of the style once.
                var root=app.ShowCardGallery13(0);yield return Art073Settle(.3f);
                var area=V06ScreenRect((RectTransform)root);
                yield return new WaitForEndOfFrame();var first=ScreenCapture.CaptureScreenshotAsTexture();
                var loaded=new HashSet<Texture2D>();
                foreach(string id in ids)loaded.Add(swiss.ContentArt("illustration_"+id).texture);
                foreach(string family in Families22i)loaded.Add(swiss.ContentArt("emblem_"+family).texture);
                Assert.That(loaded.Count,Is.EqualTo(ids.Length+Families22i.Length),"every picture is its own texture");
                int Resident()=>Resources.FindObjectsOfTypeAll<Texture2D>().Count(t=>loaded.Contains(t));
                Assert.That(Resident(),Is.EqualTo(loaded.Count),"loaded");
                app.HideCardGallery13();
                // The switch away releases them.
                app.ApplyTheme11("sancai");yield return null;
                report.Add("swiss -> sancai: released "+ThemeRegistry.LastReleasedArt+" pictures; swiss textures still loaded: "+Resident());
                Assert.That(ThemeRegistry.LastReleasedArt,Is.EqualTo(loaded.Count),"the switch releases every picture swiss loaded");
                Assert.That(Resident(),Is.Zero,"no swiss picture stays loaded once another style is current");
                // Back on swiss: loaded again on use, drawn alike.
                app.ApplyTheme11("swiss");
                var again=swiss.ContentArt("illustration_C01");Assert.That(again,Is.Not.Null);
                Assert.That(again.texture,Is.Not.Null);Assert.That(again.texture.width,Is.GreaterThan(0).And.LessThanOrEqualTo(1024));
                root=app.ShowCardGallery13(0);yield return Art073Settle(.3f);
                yield return new WaitForEndOfFrame();var second=ScreenCapture.CaptureScreenshotAsTexture();
                int changed=0,worst=0;
                try
                {
                    var a=first.GetPixels32();var b=second.GetPixels32();int w=first.width;
                    for(int y=Mathf.Max(0,(int)area.yMin);y<Mathf.Min(first.height,(int)area.yMax);y++)
                        for(int x=Mathf.Max(0,(int)area.xMin);x<Mathf.Min(w,(int)area.xMax);x++)
                        {
                            var p=a[y*w+x];var q=b[y*w+x];int d=Mathf.Max(Mathf.Abs(p.r-q.r),Mathf.Max(Mathf.Abs(p.g-q.g),Mathf.Abs(p.b-q.b)));
                            if(d>0){changed++;worst=Mathf.Max(worst,d);}
                        }
                }
                finally{UObj.Destroy(first);UObj.Destroy(second);}
                report.Add("swiss gallery page 0 before the release and after the reload: "+changed+" pixels differ (largest "+worst+" levels)");
                Assert.That(worst,Is.LessThanOrEqualTo(2),"the reloaded pictures draw as before");
                app.HideCardGallery13();
            }
        }

        [UnityTest,Timeout(900000)] public IEnumerator PLAY_W22I_04_ThemeArtLoadTimeAndTextureMemory()
        {
            var report=new List<string>();var rows=new List<string>();
            yield return WithStyles22(()=>Run());
            string dir=W22iOutput("w22i-04");
            File.WriteAllLines(Path.Combine(dir,"load-cost.txt"),report);
            File.WriteAllLines(Path.Combine(dir,"load-cost.tsv"),new[]{"style\tpass\tswitch_ms\tdrawn_ms\tall_art_ms\tart_count\tart_data_bytes\tart_runtime_bytes\tall_textures_data_bytes\tall_textures_runtime_bytes"}.Concat(rows));
            foreach(string line in report)TestContext.WriteLine(line);

            IEnumerator Run()
            {
                yield return W14Load(W14ShopFixture(false));
                // A cold start for every style: the registry forgets the loaded styles and the unused assets are unloaded.
                app.ApplyTheme11(GameTheme.DefaultId);ThemeRegistry.ResetForTests();app.ApplyTheme11(GameTheme.DefaultId);
                yield return Art073Settle(.3f);yield return Resources.UnloadUnusedAssets();GC.Collect();yield return null;
                var start=Textures22();
                report.Add("W22i item 6, "+Screen.width+"x"+Screen.height+", Unity "+Application.unityVersion+", editor play mode; screen: the common shop; HEAD "+IdentityHead20X());
                report.Add("start (default style): textures "+start["all"].count+" "+Mb22(start["all"].data)+" data / "+Mb22(start["all"].runtime)+" runtime; style art "+start["art"].count+" "+Mb22(start["art"].data)+"; compact icons "+start["icons"].count+" "+Mb22(start["icons"].data)+" data / "+Mb22(start["icons"].runtime)+" runtime");
                foreach(var set in app.compactIcons13.Where(s=>s!=null))
                {
                    long data=0,runtime=0;foreach(var icon in set.icons.Where(i=>i!=null)){TextureBytes22(icon.texture,out long r,out long d);data+=d;runtime+=r;}
                    report.Add("  bound icon set "+set.theme+": "+set.icons.Length+" icons, "+Mb22(data)+" data / "+Mb22(runtime)+" runtime (loaded with the scene)");
                }
                var ids=ContentIds22i;
                foreach(int pass in new[]{1,2})
                {
                    foreach(string theme in Styles22i)
                    {
                        var before=Textures22();
                        var clock=Stopwatch.StartNew();
                        app.ApplyTheme11(theme);
                        double switched=clock.Elapsed.TotalMilliseconds;
                        yield return new WaitForEndOfFrame();
                        double drawn=clock.Elapsed.TotalMilliseconds;
                        yield return null;
                        var shown=Textures22();
                        // The whole set (what the card gallery or a long run reaches): every illustration and emblem once.
                        var style=ThemeRegistry.Current;clock.Restart();int found=0;
                        foreach(string id in ids)if(style.ContentArt("illustration_"+id)!=null)found++;
                        foreach(string family in Families22i)if(style.ContentArt("emblem_"+family)!=null)found++;
                        double all=clock.Elapsed.TotalMilliseconds;
                        var full=Textures22();
                        Assert.That(found,Is.EqualTo(ids.Length+Families22i.Length),theme+": every illustration and emblem");
                        report.Add(theme+" pass "+pass+": ApplyTheme11 "+switched.ToString("0.0",CultureInfo.InvariantCulture)+" ms, drawn "+drawn.ToString("0.0",CultureInfo.InvariantCulture)+" ms; on the shop the style's art adds "+
                            (shown["art"].count-before["art"].count)+" textures "+Mb22(shown["art"].data-before["art"].data)+" data / "+Mb22(shown["art"].runtime-before["art"].runtime)+" runtime (all textures "+Mb22(shown["all"].runtime-before["all"].runtime)+" runtime); "+
                            "the whole set ("+found+") asked in "+all.ToString("0.0",CultureInfo.InvariantCulture)+" ms, then style art loaded "+full["art"].count+" textures "+Mb22(full["art"].data)+" data / "+Mb22(full["art"].runtime)+" runtime; all textures "+Mb22(full["all"].runtime)+" runtime");
                        rows.Add(theme+"\t"+pass+"\t"+switched.ToString("0.0",CultureInfo.InvariantCulture)+"\t"+drawn.ToString("0.0",CultureInfo.InvariantCulture)+"\t"+all.ToString("0.0",CultureInfo.InvariantCulture)+"\t"+full["art"].count+"\t"+full["art"].data+"\t"+full["art"].runtime+"\t"+full["all"].data+"\t"+full["all"].runtime);
                        yield return null;
                    }
                    var end=Textures22();
                    report.Add("after pass "+pass+" through the five styles (each style's whole set asked once): textures "+end["all"].count+" "+Mb22(end["all"].data)+" data / "+Mb22(end["all"].runtime)+" runtime; style art "+end["art"].count+" "+Mb22(end["art"].data)+" data / "+Mb22(end["art"].runtime)+" runtime; compact icons "+end["icons"].count+" "+Mb22(end["icons"].runtime)+" runtime");
                }
                app.ApplyTheme11(GameTheme.DefaultId);yield return null;
                var back=Textures22();
                report.Add("back on default: textures "+back["all"].count+" "+Mb22(back["all"].runtime)+" runtime; style art "+back["art"].count+" "+Mb22(back["art"].runtime)+" runtime");
            }
        }
    }
}
