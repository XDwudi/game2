using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [Serializable] private sealed class WalletRead07
        {
            public string amount,stateHash,saveSha,sourceSha,runtimeMvid;
            public int width,height,fontPercent;
            public float amountFontSize,tracking,minimumDigitInkGapPixels;
            public List<CardGlyph07> labels=new List<CardGlyph07>();
        }

        [UnityTest] public IEnumerator PLAY_ART07_ShopWalletExactDigitsInterestAndGpuAtThreeFonts()
        {
            Assert.That(Screen.width==1280&&Screen.height==720||Screen.width==1920&&Screen.height==1080,Is.True,
                "Run once in each actual 720p and 1080p GameView, never a simulated viewport.");
            int width=Screen.width,height=Screen.height;yield return ExtraBegin07();
            foreach(int font in new[]{100,125,150})
            {
                yield return CardSetFont07(font);
                foreach(long gold in new long[]{0,1000010,1000000000})
                {
                    // Explicit legal isolated debug save fixture. Rendering and tab navigation are read-only.
                    var fixture=ShopBoundaryFixture();fixture.gold=gold;fixture.controllers.Clear();
                    fixture.controllers.Add(BoundaryController(fixture,"C41"));fixture.controllers.Add(BoundaryController(fixture,"C01"));
                    string id="wallet-"+font+"-"+gold;
                    yield return ExtraLoadFixture07(fixture,id);
                    string state=StateJson();byte[] saved=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
                    string amount=gold.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    var panel=FindActive<RectTransform>("Stats07");var money=FindActive<TextMeshProUGUI>("Money");
                    long sources=ControllerRules.ResolveSources(app.Session.State.controllers).Count(s=>s.abilityId=="C01");
                    Assert.That(sources,Is.EqualTo(2),"This wallet fixture retains real C01 and C41 interest contributions.");
                    string interest="每"+app.Config.interestDivisor+"金币利息 +"+(1+sources*app.Config.Controller("C01").parameters.extraInterestPerTen)+"\n计息本金上限 "+app.Config.maxInterestPrincipal;
                    string[] names={"Brand","MoneyUnit","Money","Interest"};string[] expected={"骰子构筑","金币",amount,interest};
                    var labels=names.Select(FindActive<TextMeshProUGUI>).ToArray();
                    var report=new WalletRead07{amount=amount,stateHash=ExtraStateHash07(),saveSha=ExtraFileHash07(app.SaveLocation),
                        sourceSha=extra07.sourceManifestSha256,runtimeMvid=typeof(GameApp).Module.ModuleVersionId.ToString(),
                        width=Screen.width,height=Screen.height,fontPercent=font,amountFontSize=money.fontSize,tracking=money.characterSpacing,
                        minimumDigitInkGapPixels=float.MaxValue};
                    for(int i=0;i<labels.Length;i++)report.labels.Add(CardGlyphCheck07(panel,labels[i],expected[i],292));
                    Assert.That(money.fontSize,Is.EqualTo(38*font/100f).Within(.001f),"Do not shrink the wallet to conceal overflow.");
                    Assert.That(money.enableAutoSizing,Is.False);Assert.That(money.textInfo.lineCount,Is.EqualTo(1));
                    Assert.That(labels[3].textInfo.lineCount,Is.EqualTo(2),"Keep the complete interest rate and principal cap.");
                    for(int a=0;a<report.labels.Count;a++)for(int b=a+1;b<report.labels.Count;b++)
                        Assert.That(CardOverlap07(report.labels[a].ink,report.labels[b].ink),Is.False,names[a]+" ink intersects "+names[b]);
                    foreach(string button in new[]{"Pause","Handbook"})
                    {
                        Rect bounds=CardLocalRect07(panel,(RectTransform)Button(button).transform);
                        foreach(var label in report.labels)Assert.That(CardOverlap07(label.ink,bounds),Is.False,label.name+" covers "+button);
                        Assert.That(ExtraBounds07((RectTransform)Button(button).transform).height,Is.GreaterThanOrEqualTo(39.99f));
                    }
                    // Test unpadded glyph ink metrics, not SDF quad padding (which intentionally overlaps).
                    // The counterfactual GPU pass below independently requires pixels for every actual digit.
                    for(int i=1;i<money.textInfo.characterCount;i++)
                    {
                        var left=money.textInfo.characterInfo[i-1];var right=money.textInfo.characterInfo[i];
                        var lm=left.textElement.glyph.metrics;var rm=right.textElement.glyph.metrics;
                        float edge=left.origin+(lm.horizontalBearingX+lm.width)*left.scale;
                        float next=right.origin+rm.horizontalBearingX*right.scale;
                        float gap=RectTransformUtility.WorldToScreenPoint(null,money.transform.TransformPoint(new Vector3(next,0))).x-
                            RectTransformUtility.WorldToScreenPoint(null,money.transform.TransformPoint(new Vector3(edge,0))).x;
                        report.minimumDigitInkGapPixels=Mathf.Min(report.minimumDigitInkGapPixels,gap);
                        Assert.That(gap,Is.GreaterThanOrEqualTo(.5f),"Adjacent wallet digit ink must remain visibly separate at the actual resolution.");
                    }
                    if(money.textInfo.characterCount<2)report.minimumDigitInkGapPixels=0;
                    File.WriteAllText(Path.Combine(extra07.output,id+"-glyphs.json"),JsonUtility.ToJson(report,true));
                    Assert.That(report.labels.SelectMany(l=>l.errors).ToArray(),Is.Empty,"Full glyph vertices and bounds must pass before GPU verification.");
                    yield return ScoreLabelGpuCapture07(labels);
                    yield return ExtraCapture07(id,"S10","Exact wallet digits, separate currency, complete real interest and navigation; real GPU glyph evidence at this actual resolution/font.");
                    // v0.73: every shop section is on this one page. A plain redraw must keep the same wallet, without creating a transaction.
                    app.Refresh();yield return null;
                    Assert.That(FindActive<TextMeshProUGUI>("Money").text,Is.EqualTo(amount));
                    Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
                    CollectionAssert.AreEqual(saved,File.ReadAllBytes(app.SaveLocation));
                    Assert.That(Screen.width,Is.EqualTo(width));Assert.That(Screen.height,Is.EqualTo(height));
                }
            }
            ExtraFinish07();
        }
    }
}
