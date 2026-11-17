using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W11c (checkpoint 2, CP2-2): pressure stages are red in the Swiss theme. The pressure target numbers keep
    /// the pressure hue (THEME_SPEC §2), the other pressure cues use the pressure colour, the default theme is unchanged,
    /// and a theme switch empties the measured-surface caches.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        /// <summary>The surface colour under each label as drawn: the frame captured with the labels hidden, the median
        /// colour under the label's glyphs.</summary>
        private IEnumerator W11cFills(IList<TextMeshProUGUI> labels,Dictionary<TextMeshProUGUI,Color> fills)
        {
            Canvas.ForceUpdateCanvases();
            var rects=labels.ToDictionary(t=>t,t=>{t.ForceMeshUpdate();return W11bGlyphRects(t);});
            foreach(var t in labels)t.enabled=false;
            yield return new WaitForEndOfFrame();var shot=ScreenCapture.CaptureScreenshotAsTexture();
            foreach(var t in labels)t.enabled=true;Canvas.ForceUpdateCanvases();
            try
            {
                var px=shot.GetPixels32();int w=shot.width,h=shot.height;
                foreach(var t in labels)
                {
                    var under=new List<Color>();
                    foreach(var r in rects[t])
                        for(int y=Mathf.Max(0,Mathf.FloorToInt(r.yMin));y<Mathf.Min(h,Mathf.CeilToInt(r.yMax));y++)
                            for(int x=Mathf.Max(0,Mathf.FloorToInt(r.xMin));x<Mathf.Min(w,Mathf.CeilToInt(r.xMax));x++)under.Add(px[y*w+x]);
                    Assert.That(under.Count,Is.GreaterThan(5),t.name+" has no glyph pixels to measure.");
                    under.Sort((a,b)=>GameApp.Luminance11(a).CompareTo(GameApp.Luminance11(b)));fills[t]=under[under.Count/2];
                }
            }
            finally{UObj.Destroy(shot);}
        }
        private static float W11cHueDistance(Color a,Color b)
        {
            Color.RGBToHSV(a,out float ha,out _,out _);Color.RGBToHSV(b,out float hb,out _,out _);
            float d=Mathf.Abs(ha-hb);return Mathf.Min(d,1-d)*360f;
        }
        private static float W11cSaturation(Color c){Color.RGBToHSV(c,out _,out float s,out _);return s;}
        private static string W11cHex(Color c)=>"#"+ColorUtility.ToHtmlStringRGB(c);

        [UnityTest] public IEnumerator PLAY_V11_W11c_PressureCuesAreRedInSwissAndUnchangedInDefault()
        {
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            var report=new List<string>();
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);
                StartRun("71113");
                foreach(string theme in new[]{"default","swiss"})
                {
                    // A theme switch empties the measured sprite fills and frames: they never hold another theme's sprites.
                    if(theme=="swiss")Assert.That(GameApp.SurfaceCacheCount11,Is.GreaterThan(0),"The default play screen must have measured some surfaces before the switch.");
                    app.ApplyTheme11(theme,false);Assert.That(app.ThemeId11,Is.EqualTo(theme));
                    Assert.That(GameApp.SurfaceCacheCount11,Is.Zero,theme+": the surface caches must be empty right after the theme switch.");
                    var pressure=UiKit.Pressure;
                    foreach(int percent in new[]{100,150})
                    {
                        W11TextScale(percent);string scope=theme+" "+percent+"% "+Screen.width+"x"+Screen.height;
                        // Checkpoint scene (d): a pressure stage with the challenge accepted, two dice locked, 1,240 of 3,000.
                        W11Apply(W11Scene(new[]{"C39","C46","C50","C08"},new[]{5,5,3,5,1},"bbrbr",2,true,stageIndex:4,risk:true,target:"3000",score:"1240"));
                        yield return W11Settle();
                        var target=FindActive<TextMeshProUGUI>("Target");var gap=FindActive<TextMeshProUGUI>("GoalGap11");var title=FindActive<TextMeshProUGUI>("StageCardTitle11");
                        var fills=new Dictionary<TextMeshProUGUI,Color>();
                        yield return W11cFills(new[]{target,gap,title},fills);
                        foreach(var label in new[]{target,gap})
                        {
                            var color=label.color;color.a=1;float ratio=GameApp.Contrast11(color,fills[label]);
                            report.Add(scope+" "+label.name+" "+W11cHex(color)+" on "+W11cHex(fills[label])+" "+ratio.ToString("0.00",CultureInfo.InvariantCulture)+":1 hue offset "+
                                W11cHueDistance(color,pressure).ToString("0.0",CultureInfo.InvariantCulture)+" deg, saturation "+W11cSaturation(color).ToString("0.00",CultureInfo.InvariantCulture)+" (pressure "+W11cHex(pressure)+")");
                            if(theme=="default")
                                Assert.That(ColorUtility.ToHtmlStringRGBA(label.color),Is.EqualTo(ColorUtility.ToHtmlStringRGBA(pressure)),scope+": the default theme keeps the pressure colour on "+label.name);
                            else
                            {
                                Assert.That(W11cHueDistance(color,pressure),Is.LessThanOrEqualTo(12f),scope+" "+label.name+" "+W11cHex(color)+" must keep the pressure hue.");
                                Assert.That(W11cSaturation(color),Is.GreaterThanOrEqualTo(.5f),scope+" "+label.name+" "+W11cHex(color)+" must stay saturated.");
                                Assert.That(ratio,Is.GreaterThanOrEqualTo(GameApp.MinContrast11),scope+" "+label.name+" "+W11cHex(color)+" on "+W11cHex(fills[label]));
                            }
                        }
                        // The other cues: the stage tag and the stage card's header (filled, may be deepened for their labels),
                        // the stage card's ring, the tray's accent line and the progress bar (exactly the pressure colour).
                        var tag=FindActive<Image>("StageTag11");var header=FindActive<Image>("StageCardHeader11");
                        var ring=FindActive<Image>("StageCardEdge11");var accent=FindActive<Image>("TrayAccent11");
                        var bar=FindActive<RectTransform>("StageProgress072").Find("Fill").GetComponent<Image>();
                        var titleColor=title.color;titleColor.a=1;float titleRatio=GameApp.Contrast11(titleColor,fills[title]);
                        report.Add(scope+" StageTag11 "+W11cHex(tag.color)+" StageCardHeader11 "+W11cHex(header.color)+" (title "+W11cHex(titleColor)+" on "+W11cHex(fills[title])+" "+titleRatio.ToString("0.00",CultureInfo.InvariantCulture)+":1)"+
                            " StageCardEdge11 "+W11cHex(ring.color)+" TrayAccent11 "+W11cHex(accent.color)+" progress "+W11cHex(bar.color));
                        foreach(var exact in new[]{ring,accent,bar})
                            Assert.That(ColorUtility.ToHtmlStringRGB(exact.color),Is.EqualTo(ColorUtility.ToHtmlStringRGB(pressure)),scope+": "+exact.name+" draws in the pressure colour.");
                        foreach(var filled in new[]{tag,header})
                        {
                            Assert.That(W11cHueDistance(filled.color,pressure),Is.LessThanOrEqualTo(12f),scope+": "+filled.name+" "+W11cHex(filled.color)+" keeps the pressure hue.");
                            Assert.That(W11cSaturation(filled.color),Is.GreaterThanOrEqualTo(.5f),scope+": "+filled.name+" "+W11cHex(filled.color)+" stays saturated.");
                        }
                        Assert.That(titleRatio,Is.GreaterThanOrEqualTo(GameApp.MinContrast11),scope+": the stage card's title must stay readable on its header.");
                        if(theme=="swiss")Assert.That(W11cHueDistance(pressure,UiKit.Hex("E80E16")),Is.LessThan(.5f),"Swiss pressure is the accent red E80E16 (CP2-2).");
                    }
                    W11TextScale(100);
                }
            }
            finally
            {
                File.WriteAllLines(Path.Combine(W11Output,"w11c-pressure.txt"),report);
                app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
            }
        }
    }
}
