using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using DiceCraft.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W13 (docs/specs/v11/CARD_TEMPLATE.md): the card template. The debug gallery (GameApp.ShowCardGallery13)
    /// lays out every size, rarity, enchantment, value badge and disabled state, the goods and the price-button states with
    /// the production drawing code; the play row draws its cards with the same template. Captures go to
    /// artifacts/cache/v11/w13/&lt;width&gt;x&lt;height&gt;.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private string W13Output
        {
            get
            {
                // V1.1 (W20X item 0): the stray-cursor regression run of the gallery writes its own folder, not W13's.
                string dir=w13Output20X??Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w13",Screen.width+"x"+Screen.height));
                Directory.CreateDirectory(dir);return dir;
            }
        }
        private string w13Output20X;
        private IEnumerator W13Capture(string name)
        {
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try{Assert.That(texture.width,Is.EqualTo(Screen.width));File.WriteAllBytes(Path.Combine(W13Output,name+".png"),texture.EncodeToPNG());}
            finally{UObj.Destroy(texture);}
        }
        private static readonly string[] W13RarityKeys={"common","advanced","rare"};
        // V1.1 (W14, CARD_TEMPLATE E2): a hand upgrade is a good of the shop's 升级与服务 rows too.
        private static readonly string[] W13GoodsCategories={"道具","服务","骰面改造","新骰子","牌型升级"};
        private static bool W13Overlap(Rect a,Rect b)=>Math.Min(a.xMax,b.xMax)-Math.Max(a.xMin,b.xMin)>1&&Math.Min(a.yMax,b.yMax)-Math.Max(a.yMin,b.yMin)>1;
        private static Color W13Palette(int rarity)=>rarity==2?UiKit.RarityRare:rarity==1?UiKit.RarityAdvanced:UiKit.RarityCommon;
        private static bool W13SameColour(Color a,Color b)=>Mathf.Abs(a.r-b.r)<.004f&&Mathf.Abs(a.g-b.g)<.004f&&Mathf.Abs(a.b-b.b)<.004f;

        /// <summary>
        /// The checks of W11GlyphCheck (complete text: every visible character drawn; not truncated; glyphs inside the
        /// text's own box and inside the visible frame of the surface they are drawn on), collected for a whole gallery
        /// page instead of stopping at the first text; and, since a frame check only looks at what is drawn before a text,
        /// no glyph covered by a filled image drawn after it (a short text running under the price button).
        /// </summary>
        private List<Rect> W13GlyphCheck(TextMeshProUGUI text,string scope,Graphic[] order,List<string> failures)
        {
            text.ForceMeshUpdate();int expected=text.GetParsedText().Count(c=>!char.IsWhiteSpace(c));
            string at=scope+" "+W11bPath(text.transform)+" '"+text.text.Replace("\n","/")+"'";
            if(text.isTextTruncated)failures.Add(at+": truncated");
            var rects=W11bGlyphRects(text);
            if(rects.Count!=expected)failures.Add(at+": draws "+rects.Count+" of "+expected+" characters");
            var own=V06ScreenRect(text.rectTransform);const float slack=1.5f;
            if(rects.Any(r=>r.xMin<own.xMin-slack||r.xMax>own.xMax+slack||r.yMin<own.yMin-slack||r.yMax>own.yMax+slack))failures.Add(at+": glyphs outside its own box "+own);
            var frame=W11bVisibleFrame(text,rects,order,out string surface);
            if(frame.HasValue)
            {
                var outside=rects.FirstOrDefault(r=>r.xMin<frame.Value.xMin-slack||r.xMax>frame.Value.xMax+slack||r.yMin<frame.Value.yMin-slack||r.yMax>frame.Value.yMax+slack);
                if(outside.width>0)failures.Add(at+": glyph "+outside+" outside the visible frame "+frame.Value+" of "+surface);
            }
            for(int i=Array.IndexOf(order,text)+1;i>0&&i<order.Length;i++)
            {
                if(!(order[i] is Image image)||!W11bPaints(image,true)||image.transform.IsChildOf(text.transform))continue;
                var cover=V06ScreenRect(image.rectTransform);
                var hidden=rects.FirstOrDefault(r=>W13Overlap(r,cover));
                if(hidden.width>0){failures.Add(at+": glyph "+hidden+" covered by "+W11bPath(image.transform)+" drawn after it");break;}
            }
            return rects;
        }

        /// <summary>CT-01 and the template's rules on one gallery page: every text complete and inside its drawn frame; short
        /// texts complete in the Shop and Table sizes; body text and value badges at least 19.5 units (AX-03); no text over
        /// another, over the enchantment badge or (the value badge aside) over the picture; the value chip clear of the
        /// enchantment badge; rarity as frame colour and gem shape, goods with a Ghost frame, no gem and their category; a
        /// disabled card dimmed with 本关失效; price buttons in the second-level style, usable only when nothing stops a buy.</summary>
        private void W13GalleryChecks(RectTransform root,int page,string scope,List<string> report,List<string> failures)
        {
            Canvas.ForceUpdateCanvases();
            var order=app.canvas.GetComponentsInChildren<Graphic>(false);
            int before=failures.Count,texts=0,shortTexts=0,badges=0;
            foreach(var t in root.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(t.text))){W13GlyphCheck(t,scope,order,failures);texts++;}
            var cards=root.Cast<Transform>().Where(c=>c.gameObject.activeSelf&&c.GetComponent<Button>()!=null&&c.name.StartsWith("Gallery",StringComparison.Ordinal)).ToArray();
            if(cards.Length==0)failures.Add(scope+": no cards on the page");
            bool tallSize=page<=2;
            foreach(var card in cards)
            {
                string at=scope+" "+card.name;
                var labels=card.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(t.text)).ToArray();
                var body=card.Find("ShortText11");
                if(tallSize&&(body==null||!body.gameObject.activeSelf))failures.Add(at+": the short text is not shown (Shop and Table sizes show it whole)");
                if(body!=null&&body.gameObject.activeSelf)shortTexts++;
                foreach(var t in labels)
                    if((t.name=="ShortText11"||t.name=="State")&&t.fontSize<19.49f)failures.Add(at+" "+t.name+" at "+t.fontSize.ToString("0.0",CultureInfo.InvariantCulture)+" units, below the 19.5 floor");
                var inks=labels.Select(t=>new KeyValuePair<TextMeshProUGUI,List<Rect>>(t,W11bGlyphRects(t))).ToList();
                for(int i=0;i<inks.Count;i++)for(int j=i+1;j<inks.Count;j++)
                    if(inks[i].Value.Any(a=>inks[j].Value.Any(b=>W13Overlap(a,b))))failures.Add(at+": "+inks[i].Key.name+" and "+inks[j].Key.name+" overlap");
                var enchant=card.Find("EnchantFrame/Badge") as RectTransform;
                Rect? enchantRect=enchant!=null&&enchant.gameObject.activeInHierarchy?V06ScreenRect(enchant):(Rect?)null;
                // The enchantment badge follows the active style: with Swiss active it is Swiss's own badge_enchant_*.
                if(enchantRect.HasValue)
                {
                    var badgeSprite=enchant.GetComponent<Image>()?.sprite;
                    if(badgeSprite==null||!badgeSprite.name.StartsWith("badge_enchant_",StringComparison.Ordinal))failures.Add(at+": the enchantment badge has no badge_enchant_* sprite");
                    else if(app.ThemeId11=="swiss"&&!ReferenceEquals(badgeSprite,Resources.Load<Sprite>("Themes/swiss/UI/"+badgeSprite.name)))failures.Add(at+": the enchantment badge is not Swiss's own "+badgeSprite.name);
                }
                var picture=card.Cast<Transform>().FirstOrDefault(x=>x.gameObject.activeSelf&&x.name.StartsWith("Icon_",StringComparison.Ordinal)) as RectTransform;
                if(picture==null)failures.Add(at+": no picture");
                foreach(var pair in inks)
                {
                    if(enchantRect.HasValue&&pair.Value.Any(r=>W13Overlap(r,enchantRect.Value)))failures.Add(at+": "+pair.Key.name+" over the enchantment badge");
                    bool onPicture=pair.Key.name=="State"||pair.Key.transform.parent!=card;
                    if(picture!=null&&!onPicture&&pair.Value.Any(r=>W13Overlap(r,V06ScreenRect(picture))))failures.Add(at+": "+pair.Key.name+" over the picture");
                }
                var chip=card.Find("ValueBadge13") as RectTransform;
                if(chip!=null&&chip.gameObject.activeSelf){badges++;if(enchantRect.HasValue&&W13Overlap(V06ScreenRect(chip),enchantRect.Value))failures.Add(at+": the value badge covers the enchantment badge");}
                // Rarity, goods and the disabled state, read from the card's name (GalleryKind13_rarity_variant).
                var gem=card.Find("RarityGem13")?.GetComponent<Image>();var ring=card.Find("RarityFrame13")?.GetComponent<Image>();
                bool goods=card.name.Contains("Goods");
                if(ring==null||!ring.gameObject.activeSelf)failures.Add(at+": no rarity frame");
                if(goods)
                {
                    if(gem!=null&&gem.gameObject.activeSelf)failures.Add(at+": a good has a gem");
                    // V1.1 (W14): a sold good is dimmed like a disabled card, its Ghost frame with it (62 %). The face itself may
                    // then be lightened in 5 % steps by LabelOn11 until a label colour reads on it (W11; the default style's
                    // dimmed goods row reads 0.639): a face below 0.75 is a dimmed one (white deepened four steps is 0.81).
                    var goodFace=(Image)card.GetComponent<Button>().targetGraphic;bool dimmed=goodFace.color.r<.75f;
                    Color ghost=dimmed?new Color(UiKit.Ghost.r*.62f,UiKit.Ghost.g*.62f,UiKit.Ghost.b*.62f,UiKit.Ghost.a):UiKit.Ghost;
                    if(ring!=null&&!W13SameColour(ring.color,ghost))failures.Add(at+": a good's frame is not Ghost"+(dimmed?" (dimmed)":"")+": frame #"+ColorUtility.ToHtmlStringRGBA(ring.color)+", face #"+ColorUtility.ToHtmlStringRGBA(goodFace.color)+", Ghost #"+ColorUtility.ToHtmlStringRGBA(UiKit.Ghost));
                    var category=card.Find("Category13")?.GetComponent<TextMeshProUGUI>();
                    if(category==null||!category.gameObject.activeSelf||!W13GoodsCategories.Contains(category.text))failures.Add(at+": no goods category");
                }
                else
                {
                    string[] parts=card.name.Split('_');
                    if(parts.Length==3&&int.TryParse(parts[1],out int rarity)&&int.TryParse(parts[2],out int variant))
                    {
                        bool disabled=variant==4||variant==5;
                        Color expected=W13Palette(rarity);if(disabled)expected=new Color(expected.r*.62f,expected.g*.62f,expected.b*.62f,expected.a);
                        if(gem==null||!gem.gameObject.activeSelf||gem.sprite==null||gem.sprite.name!="ui_gem_"+W13RarityKeys[rarity])failures.Add(at+": gem "+(gem!=null&&gem.sprite!=null?gem.sprite.name:"missing")+" for "+W13RarityKeys[rarity]);
                        if(ring!=null&&!W13SameColour(ring.color,expected))failures.Add(at+": frame #"+ColorUtility.ToHtmlStringRGB(ring.color)+" for "+W13RarityKeys[rarity]+(disabled?" (dimmed)":""));
                        var face=(Image)card.GetComponent<Button>().targetGraphic;
                        if(disabled&&(face.color.r>.63f||card.Find("State")?.GetComponent<TextMeshProUGUI>().text!="本关失效"))failures.Add(at+": a disabled card is not dimmed with 本关失效");
                        if(!disabled&&face.color!=Color.white)failures.Add(at+": an enabled card is dimmed");
                    }
                    else if(!card.name.StartsWith("GalleryIcon13_",StringComparison.Ordinal))failures.Add(at+": unexpected gallery card name");
                }
                // The price button (Shop size): second-level style, never the primary colour (AX-02); usable only to buy.
                var price=card.Find("PriceButton13")?.GetComponent<Button>();
                if(page<=1&&(price==null||!price.gameObject.activeSelf))failures.Add(at+": no price button");
                if(price!=null&&price.gameObject.activeSelf)
                {
                    if(W11PrimaryColoured(price.targetGraphic.color))failures.Add(at+": the price button is in the primary colour");
                    string word=price.transform.Find("PriceWord13")?.GetComponent<TextMeshProUGUI>()?.text;
                    if(string.IsNullOrEmpty(word))failures.Add(at+": the price button says nothing");
                    else if(price.interactable!=(word=="购买"))failures.Add(at+": the price button reads '"+word+"' but interactable is "+price.interactable);
                    if(!V06ScreenRect(card as RectTransform).Overlaps(V06ScreenRect(price.transform as RectTransform)))failures.Add(at+": the price button is off the card");
                }
            }
            report.Add(scope+": "+cards.Length+" cards, "+texts+" texts, "+shortTexts+" short texts, "+badges+" value badges, "+(failures.Count-before)+" problems");
        }

        [UnityTest] public IEnumerator PLAY_V13_CT01_CT02_CardGalleryInBothThemesAtTwoTextSizes()=>W13GalleryRun(null);
        /// <summary>The body of the CT-01/CT-02 gallery test. V1.1 (W20X item 0): shared with the stray-cursor regression test,
        /// whose <paramref name="afterStart"/> leaves a resting real cursor over a gallery card before the pages are walked.</summary>
        private IEnumerator W13GalleryRun(Func<IEnumerator> afterStart)
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Run at actual 1920x1080 or 1280x720.");
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();var failures=new List<string>();var contrast=new List<string>();var contrastFailures=new List<string>();
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);GameApp.CleanCapture11=true;
                StartRun("71301");
                if(afterStart!=null)yield return afterStart();
                foreach(string theme in new[]{"default","swiss"})
                {
                    app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));
                    if(theme=="default")Assert.That(new[]{UiKit.RarityCommon,UiKit.RarityAdvanced,UiKit.RarityRare}.Select(ColorUtility.ToHtmlStringRGB),Is.EqualTo(new[]{"B8C9C5","55B6D9","AD92DB"}),"CP2-3 default rarity colours.");
                    foreach(int percent in new[]{100,150})
                    {
                        W11TextScale(percent);
                        for(int page=0;page<GameApp.CardGalleryPages13;page++)
                        {
                            string scope=theme+"-text"+percent+"-p"+page;
                            var root=app.ShowCardGallery13(page);yield return null;
                            W13GalleryChecks(root,page,scope,report,failures);
                            yield return W13Capture("gallery-"+scope);
                            yield return W11bContrastPass(scope,contrast,contrastFailures,root);
                        }
                        app.HideCardGallery13();
                    }
                    W11TextScale(100);
                }
            }
            finally
            {
                app.HideCardGallery13();W11TextScale(100);GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                File.WriteAllLines(Path.Combine(W13Output,"ct01-gallery.txt"),report.Concat(new[]{"","PROBLEMS "+failures.Count}).Concat(failures));
                File.WriteAllLines(Path.Combine(W13Output,"ct02-contrast.txt"),contrast);
            }
            Assert.That(report.Count,Is.EqualTo(2*2*GameApp.CardGalleryPages13),"Every page in both themes at both text sizes.");
            Assert.That(contrast.Count,Is.GreaterThan(400),"Too few labels measured.");
            Assert.That(failures,Is.Empty,"CT-01 and template rules ("+failures.Count+"):\n"+string.Join("\n",failures.Take(60)));
            Assert.That(contrastFailures,Is.Empty,"CT-02 labels below 4.5:1:\n"+string.Join("\n",contrastFailures.Take(60)));
        }

        /// <summary>A play-row fixture of ten cards (two-row mini cards) with values and enchantments: the seven cards the
        /// Swiss theme redrew, a growth card, a colour card and a copy card.</summary>
        private DebugScene W13TenCards()
        {
            var scene=W11Scene(new[]{"C39","C139","C34","C56","C08","C46","C50","C55","C45","C41"},new[]{4,4,6,2,4},"bbbrb",2,true);
            string[] enchant={"none","none","roll1","none","slotless","mult2","none","base10","none","submit1"};
            for(int i=0;i<scene.controllers.Count;i++)scene.controllers[i].enchantmentId=enchant[i];
            scene.controllers[7].state.growth=24;scene.controllers[8].state.activeColor="red";
            return scene;
        }

        /// <summary>CIE L*a*b* (D65) of an sRGB colour.</summary>
        private static void W13Lab(Color c,out double l,out double a,out double b)
        {
            double Linear(double u)=>u<=.04045?u/12.92:Math.Pow((u+.055)/1.055,2.4);
            double r=Linear(c.r),g=Linear(c.g),bl=Linear(c.b);
            double x=(.4124564*r+.3575761*g+.1804375*bl)/.95047,y=.2126729*r+.7151522*g+.0721750*bl,z=(.0193339*r+.1191920*g+.9503041*bl)/1.08883;
            double F(double t)=>t>216.0/24389?Math.Pow(t,1.0/3):(24389.0/27*t+16)/116;
            double fx=F(x),fy=F(y),fz=F(z);l=116*fy-16;a=500*(fx-fy);b=200*(fy-fz);
        }
        /// <summary>CIEDE2000 colour difference of two L*a*b* colours (Sharma, Wu and Dalal 2005).</summary>
        private static double W13DeltaE00(double l1,double a1,double b1,double l2,double a2,double b2)
        {
            double Rad(double d)=>d*Math.PI/180;
            double c1=Math.Sqrt(a1*a1+b1*b1),c2=Math.Sqrt(a2*a2+b2*b2),cm=(c1+c2)/2,cm7=Math.Pow(cm,7);
            double g=.5*(1-Math.Sqrt(cm7/(cm7+Math.Pow(25,7))));
            double a1p=(1+g)*a1,a2p=(1+g)*a2,c1p=Math.Sqrt(a1p*a1p+b1*b1),c2p=Math.Sqrt(a2p*a2p+b2*b2);
            double Hue(double bb,double ap){if(bb==0&&ap==0)return 0;double v=Math.Atan2(bb,ap)*180/Math.PI;return v<0?v+360:v;}
            double h1=Hue(b1,a1p),h2=Hue(b2,a2p);
            double dl=l2-l1,dc=c2p-c1p,dh=0;
            if(c1p*c2p!=0){dh=h2-h1;if(dh>180)dh-=360;else if(dh<-180)dh+=360;}
            double dH=2*Math.Sqrt(c1p*c2p)*Math.Sin(Rad(dh/2));
            double lm=(l1+l2)/2,cpm=(c1p+c2p)/2,hm=h1+h2;
            if(c1p*c2p!=0)hm=Math.Abs(h1-h2)<=180?(h1+h2)/2:(h1+h2<360?(h1+h2+360)/2:(h1+h2-360)/2);
            double t=1-.17*Math.Cos(Rad(hm-30))+.24*Math.Cos(Rad(2*hm))+.32*Math.Cos(Rad(3*hm+6))-.20*Math.Cos(Rad(4*hm-63));
            double theta=30*Math.Exp(-Math.Pow((hm-275)/25,2)),cpm7=Math.Pow(cpm,7),rc=2*Math.Sqrt(cpm7/(cpm7+Math.Pow(25,7)));
            double sl=1+.015*Math.Pow(lm-50,2)/Math.Sqrt(20+Math.Pow(lm-50,2)),sc=1+.045*cpm,sh=1+.015*cpm*t,rt=-Math.Sin(Rad(2*theta))*rc;
            return Math.Sqrt(Math.Pow(dl/sl,2)+Math.Pow(dc/sc,2)+Math.Pow(dH/sh,2)+rt*(dc/sc)*(dH/sh));
        }
        private static double W13DeltaE00(Color x,Color y){W13Lab(x,out var l1,out var a1,out var b1);W13Lab(y,out var l2,out var a2,out var b2);return W13DeltaE00(l1,a1,b1,l2,a2,b2);}
        private static readonly string[] W13Warnings={"本关失效","首领关将失效","将被祭坛摧毁","右邻失效"};

        /// <summary>
        /// THEME_SPEC §2 valueBadge (W13 review): in every style a value badge (×1.5, +12, 本手可用) and a warning badge
        /// (本关失效, 首领关将失效) are told apart at a glance. Measured as drawn on the gallery's Table page: the chip fill under
        /// each badge label on a capture with the labels hidden (so after any LabelOn11 deepening). Between the value fill
        /// and the warning fill: CIEDE2000 at least 20 and luminance contrast at least 1.5:1; each label on its own chip at
        /// least 4.5:1.
        /// </summary>
        [UnityTest] public IEnumerator PLAY_V13_ValueAndWarningBadgesAreDistinctInEveryTheme()
        {
            // The CIEDE2000 implementation against the first pair of Sharma, Wu and Dalal's test data.
            Assert.That(W13DeltaE00(50,2.6772,-79.7751,50,0,-82.7485),Is.EqualTo(2.0425).Within(.0001));
            Assert.That(W13DeltaE00(50,-1.3802,-84.2814,50,0,-82.7485),Is.EqualTo(1.0000).Within(.0001));
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();var failures=new List<string>();
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);StartRun("71304");W11TextScale(100);
                foreach(var theme in ThemeRegistry.All)
                {
                    app.ApplyTheme11(theme.Id);Assert.That(app.ThemeId11,Is.EqualTo(theme.Id));
                    var root=app.ShowCardGallery13(2);yield return null;Canvas.ForceUpdateCanvases();
                    var labels=root.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.name=="State"&&t.gameObject.activeInHierarchy&&!string.IsNullOrWhiteSpace(t.text)).ToList();
                    foreach(var t in labels)t.ForceMeshUpdate();
                    var glyphs=labels.ToDictionary(t=>t,W11bGlyphRects);
                    foreach(var t in labels)t.enabled=false;
                    yield return new WaitForEndOfFrame();var shot=ScreenCapture.CaptureScreenshotAsTexture();
                    foreach(var t in labels)t.enabled=true;Canvas.ForceUpdateCanvases();
                    var values=new List<Color>();var warnings=new List<Color>();
                    try
                    {
                        var px=shot.GetPixels32();int w=shot.width,h=shot.height;
                        foreach(var t in labels)
                        {
                            var under=new List<Color>();
                            foreach(var r in glyphs[t])
                                for(int y=Mathf.Max(0,Mathf.FloorToInt(r.yMin));y<Mathf.Min(h,Mathf.CeilToInt(r.yMax));y++)
                                    for(int x=Mathf.Max(0,Mathf.FloorToInt(r.xMin));x<Mathf.Min(w,Mathf.CeilToInt(r.xMax));x++)under.Add(px[y*w+x]);
                            if(under.Count<6){failures.Add(theme.Id+" "+W11bPath(t.transform)+": no pixels under the label");continue;}
                            under.Sort((a,b)=>GameApp.Luminance11(a).CompareTo(GameApp.Luminance11(b)));
                            Color fill=under[under.Count/2];var label=t.color;label.a=1;
                            float ratio=GameApp.Contrast11(label,fill);
                            if(ratio<GameApp.MinContrast11)failures.Add(theme.Id+" '"+t.text+"' label #"+ColorUtility.ToHtmlStringRGB(label)+" on #"+ColorUtility.ToHtmlStringRGB(fill)+" "+ratio.ToString("0.00",CultureInfo.InvariantCulture)+":1");
                            (W13Warnings.Contains(t.text)?warnings:values).Add(fill);
                        }
                    }
                    finally{UObj.Destroy(shot);}
                    Assert.That(values,Is.Not.Empty,theme.Id+": the gallery's Table page must show value badges.");
                    Assert.That(warnings,Is.Not.Empty,theme.Id+": the gallery's Table page must show warning badges.");
                    Color Median(List<Color> list){var sorted=list.OrderBy(c=>GameApp.Luminance11(c)).ToList();return sorted[sorted.Count/2];}
                    Color value=Median(values),warning=Median(warnings);
                    double delta=W13DeltaE00(value,warning);float contrast=GameApp.Contrast11(value,warning);
                    // Every badge of a kind is drawn alike; the closest pair across the kinds is held to the same bounds.
                    double closest=values.SelectMany(v=>warnings.Select(x=>W13DeltaE00(v,x))).Min();
                    float lowest=values.SelectMany(v=>warnings.Select(x=>GameApp.Contrast11(v,x))).Min();
                    string line=theme.Id+": value fill #"+ColorUtility.ToHtmlStringRGB(value)+" ("+values.Count+" badges), warning fill #"+ColorUtility.ToHtmlStringRGB(warning)+" ("+warnings.Count+" badges): CIEDE2000 "+
                        delta.ToString("0.0",CultureInfo.InvariantCulture)+" (closest pair "+closest.ToString("0.0",CultureInfo.InvariantCulture)+"), luminance contrast "+contrast.ToString("0.00",CultureInfo.InvariantCulture)+":1 (lowest pair "+lowest.ToString("0.00",CultureInfo.InvariantCulture)+":1)";
                    report.Add(line);
                    if(closest<20||lowest<1.5f)failures.Add(line);
                    yield return W13Capture("badges-"+theme.Id);
                    app.HideCardGallery13();
                }
            }
            finally
            {
                app.HideCardGallery13();W11TextScale(100);app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                File.WriteAllLines(Path.Combine(W13Output,"value-warning-badges.txt"),report.Concat(failures.Select(f=>"FAIL "+f)));
            }
            Assert.That(report.Count,Is.EqualTo(ThemeRegistry.All.Count),"Every theme measured.");
            Assert.That(failures,Is.Empty,"Value and warning badges must be told apart, labels readable:\n"+string.Join("\n",failures));
        }

        /// <summary>CT-04 and the W13 play-screen record: the ten-card mini row and the states a, b3, c and f in both themes,
        /// each card of the row read by the card check (glyphs, badges and picture clear of each other).</summary>
        [UnityTest] public IEnumerator PLAY_V13_CT04_TenCardRowAndPlayScreenStatesInBothThemes()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Capture at actual 1920x1080 or 1280x720.");
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);GameApp.CleanCapture11=true;
                StartRun("71302");
                foreach(string theme in new[]{"default","swiss"})
                {
                    app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));
                    foreach(int percent in Screen.width==1280?new[]{100,150}:new[]{100})
                    {
                        W11TextScale(percent);string tag=theme+(percent==100?"":"-text"+percent);
                        // CT-04: ten cards, two rows of five.
                        W11Apply(W13TenCards());yield return W11Settle();
                        var state=app.Session.State;Assert.That(state.controllers.Count,Is.EqualTo(10));
                        foreach(var item in state.controllers)
                        {
                            var card=(RectTransform)Button("Controller_"+item.instanceId).transform;
                            Assert.That(card.rect.height,Is.LessThan(140),"Ten cards draw the two-row mini cards.");
                            var read=CardCheck07(card,item,"hud-compact",TableCardFooter11(item));
                            report.Add(tag+" "+item.definitionId+" '"+string.Join(" | ",read.labels.Select(l=>l.name+"="+l.text))+"'"+(read.errors.Count>0?" ERRORS "+string.Join("; ",read.errors):""));
                            Assert.That(read.errors,Is.Empty,tag+" "+item.definitionId+": "+string.Join("; ",read.errors));
                            // The enchantment badge follows the active style (the style's own badge_enchant_*, else the default).
                            if(item.enchantmentId!="none")
                            {
                                var badge=card.Find("EnchantFrame/Badge")?.GetComponent<UnityEngine.UI.Image>();
                                Assert.That(badge,Is.Not.Null,tag+" "+item.definitionId+": no enchantment badge.");
                                var swissBadge=Resources.Load<Sprite>("Themes/swiss/UI/badge_enchant_"+item.enchantmentId);
                                Assert.That(swissBadge,Is.Not.Null,"Swiss ships badge_enchant_"+item.enchantmentId+".");
                                if(theme=="swiss")Assert.That(badge.sprite,Is.SameAs(swissBadge),tag+" "+item.definitionId+": the enchantment badge must be Swiss's own.");
                                else Assert.That(badge.sprite,Is.SameAs(app.enchantIcons.First(s=>s!=null&&s.name=="badge_enchant_"+item.enchantmentId)),tag+" "+item.definitionId+": the default badge.");
                                report.Add(tag+" "+item.definitionId+" enchantment badge "+badge.sprite.name+(theme=="swiss"?" (Swiss)":" (default)"));
                            }
                        }
                        Assert.That(Button("Controller_"+state.controllers[7].instanceId).transform.Find("State")?.GetComponent<TextMeshProUGUI>()?.text,Is.EqualTo("基础分 +36"),"The growth card's value badge (p1.1.1, W7a: 积少成多 starts at +12, grown 24).");
                        // For the record: what a redraw of the play screen with ten template cards costs on this machine (the
                        // first full run found a per-card GPU read-back here; the ring band is now measured once per sprite).
                        var clock=System.Diagnostics.Stopwatch.StartNew();var times=new List<double>();
                        for(int i=0;i<6;i++){clock.Restart();app.Refresh();Canvas.ForceUpdateCanvases();times.Add(clock.Elapsed.TotalMilliseconds);}
                        report.Add(tag+" redraw with ten cards: "+string.Join(", ",times.Select(t=>t.ToString("0.0",CultureInfo.InvariantCulture)))+" ms");
                        yield return W11Settle();
                        yield return W13Capture(tag+"-ct04-ten-card-row");
                        // (a) selecting: five dice, three cards; two locked, one selected.
                        W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));
                        app.Session.TogglePending(app.Session.State.dice[4].dieId);app.Refresh();
                        yield return W11Settle();yield return W13Capture(tag+"-a-selecting");
                        // (f) ten dice, seven cards; three locked.
                        W11Apply(W11Scene(W11Cards7,new[]{6,6,5,3,1,4,2,6,5,1},"bbrbrbbrbb",3,true,items:3,capacity:8));
                        yield return W11Settle();yield return W13Capture(tag+"-f-ten-dice-seven-cards");
                        if(percent!=100)continue;
                        // (c) boss stage at the start of a hand (stage 9, 徒手): the disabled cards read 本关失效.
                        W11Apply(W11Scene(new[]{"C39","C139","C34","C56","C08"},new[]{1,2,3,4,5},"bbrbb",0,false,stageIndex:8,modifier:"B02",target:"13500",score:"0",items:2));
                        yield return W11Settle();yield return W13Capture(tag+"-c-boss-hand-start");
                        // (b3) mid-scoring, held on the total: the trigger badges over the cards.
                        W11Apply(W11Scene(new[]{"C39","C139","C34","C08","C46"},new[]{4,4,4,4,2},"bbbbr",4,true,target:"3000",score:"0"));
                        yield return W11Settle();
                        var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                        bool enabled=app.enabled;app.enabled=false;
                        try
                        {
                            W11HoldAt(PresentationPhase.FinalScore,.95f);W11Invoke("TickHighScore073",.3f);yield return null;
                            Assert.That(app.ScoreCardBadges11,Is.GreaterThan(0),"The card badges must be on screen.");
                            yield return W13Capture(tag+"-b3-scoring-total");
                        }
                        finally{app.enabled=enabled;}
                        app.SkipVisual();yield return W11Settle();
                    }
                    W11TextScale(100);
                }
            }
            finally
            {
                W11TextScale(100);GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                File.WriteAllLines(Path.Combine(W13Output,"ct04-ten-card-row.txt"),report);
            }
        }

        /// <summary>CARD_TEMPLATE §7: the compact icon is the current theme's baked icon, then the default theme's, then the
        /// whole illustration; looked up once per (theme, id) and forgotten on a theme switch. The baked icons are imported
        /// as 160-px sprites with mipmaps (trilinear), and the play row's compact cards draw them.
        /// V1.1 (W22i): every style bakes all 184 icons, and a style with art of its own never shows another style's picture
        /// (its icon, then its own illustration, then the default's: PLAY_W22I_02); Swiss's C01 is now its own icon.</summary>
        [UnityTest] public IEnumerator PLAY_V13_CompactIconsResolveThemeThenDefaultThenIllustration()
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var saved=app.compactIcons13;
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);
                StartRun("71303");
                var defaults=saved.Single(s=>s.theme==GameTheme.DefaultId);var swiss=saved.Single(s=>s.theme=="swiss");
                var ids=app.Config.controllers.Select(c=>c.id).Concat(app.Config.consumables.Select(c=>c.id)).Concat(app.Config.services.Select(c=>c.id)).Concat(app.Config.diceUpgrades.Select(c=>c.id)).ToArray();
                Assert.That(defaults.icons.Length,Is.EqualTo(ids.Length),"One default icon per card, item, service and face upgrade.");
                CollectionAssert.AreEquivalent(ids.Select(id=>"icon_"+id),defaults.icons.Select(s=>s.name));
                // V1.1 (W22i): was the seven trial redraws (C08, C139, C34, C39, C46, C50, C56); the whole set is installed now.
                CollectionAssert.AreEquivalent(ids.Select(id=>"icon_"+id),swiss.icons.Select(s=>s.name),"The Swiss theme bakes all its illustrations (W22i; 185 in p1.1.1).");
                foreach(var icon in defaults.icons.Concat(swiss.icons))
                {
                    Assert.That(icon.texture.width==160&&icon.texture.height==160,Is.True,icon.name+" is "+icon.texture.width+"x"+icon.texture.height);
                    Assert.That(icon.texture.mipmapCount,Is.GreaterThan(1),icon.name+" has no mipmaps.");
                    Assert.That(icon.texture.filterMode,Is.EqualTo(FilterMode.Trilinear),icon.name);
                }
                var lookup=typeof(GameApp).GetMethod("CompactIcon13",BindingFlags.NonPublic|BindingFlags.Instance);
                Sprite Icon(string id)=>(Sprite)lookup.Invoke(app,new object[]{id});
                Sprite Of(CompactIconSet13 set,string id)=>set.icons.Single(s=>s.name=="icon_"+id);
                app.ApplyTheme11(GameTheme.DefaultId);Assert.That(GameApp.CompactIconCacheCount13,Is.Zero,"A theme switch empties the cache.");
                Assert.That(Icon("C39"),Is.SameAs(Of(defaults,"C39")));Assert.That(Icon("I03"),Is.SameAs(Of(defaults,"I03")));
                Assert.That(GameApp.CompactIconCacheCount13,Is.EqualTo(2));Icon("C39");Assert.That(GameApp.CompactIconCacheCount13,Is.EqualTo(2),"Cached per (theme, id).");
                app.ApplyTheme11("swiss");Assert.That(GameApp.CompactIconCacheCount13,Is.Zero,"A theme switch empties the cache.");
                Assert.That(Icon("C39"),Is.SameAs(Of(swiss,"C39")),"The current theme's icon first.");
                // V1.1 (W22i): was Of(defaults,"C01"), "Then the default theme's": Swiss has its own C01 icon now, and a style with art
                // of its own never falls to the default icon (its own illustration comes first; PLAY_W22I_02).
                Assert.That(Icon("C01"),Is.SameAs(Of(swiss,"C01")),"Swiss draws its own C01 icon (W22i).");
                // Neither theme has an icon for C01 any more: the whole illustration.
                app.compactIcons13=saved.Select(s=>new CompactIconSet13{theme=s.theme,icons=s.icons.Where(i=>i.name!="icon_C01").ToArray()}).ToArray();
                app.ApplyTheme11("swiss");
                var illustration=(Sprite)typeof(GameApp).GetMethod("ContentSprite071",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(app,new object[]{"C01"});
                Assert.That(illustration,Is.Not.Null);Assert.That(Icon("C01"),Is.SameAs(illustration),"Then the whole illustration.");
                app.compactIcons13=saved;app.ApplyTheme11(GameTheme.DefaultId);
                // The play row's mini cards draw the baked icons (default theme).
                W11Apply(W13TenCards());yield return W11Settle();
                foreach(var item in app.Session.State.controllers)
                {
                    var image=Button("Controller_"+item.instanceId).transform.Find("Icon_"+item.definitionId)?.GetComponent<Image>();
                    Assert.That(image,Is.Not.Null,item.definitionId);Assert.That(image.sprite,Is.SameAs(Of(defaults,item.definitionId)),item.definitionId);
                }
                app.ApplyTheme11("swiss");yield return W11Settle();
                var c39=app.Session.State.controllers.Single(c=>c.definitionId=="C39");
                Assert.That(Button("Controller_"+c39.instanceId).transform.Find("Icon_C39").GetComponent<Image>().sprite,Is.SameAs(Of(swiss,"C39")));
            }
            finally
            {
                app.compactIcons13=saved;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
            }
        }
    }
}
