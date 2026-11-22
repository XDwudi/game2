using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private static readonly string[] PopupStyles16={"default","swiss","sancai","comic","deco","glass"};

        [UnityTest]
        public IEnumerator PLAY_W16B_POP05_PopupGeometryAndScrollableRows([ValueSource(nameof(PopupStyles16))] string theme)
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True);
            string folder=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w16b",
                "popups",theme,Screen.width+"x"+Screen.height));
            Directory.CreateDirectory(folder);
            var report=new List<string>{"Unity "+Application.unityVersion+" source "+IdentityHead20X()};
            var failures=new List<string>();
            int priorPreview=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey);PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);
            try
            {
                foreach(int percent in new[]{100,150})
                {
                    IEnumerator Style()
                    {
                        app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));
                        W11TextScale(percent);yield return W11Settle();
                    }
                    IEnumerator Draw(string name)
                    {
                        string path=Path.Combine(folder,percent+"-"+name);
                        yield return PopupFrames16(path,report,failures);
                    }
                    if(app.Session.State==null)StartRun("70793");
                    var random=InformationScene07(Art07FixtureKind.SharedHalf);random.randomOverrides.Clear();
                    ApplyInformationScene07(random);yield return Style();
                    string prior=StateJson();
                    Click("RandomPreview");yield return Draw("random");
                    Click("RandomBranch8");yield return Draw("score");
                    string shown=FindActive<TextMeshProUGUI>("Formula").text;
                    Click("CopyExactScore");Assert.That(GUIUtility.systemCopyBuffer,Is.EqualTo(shown));
                    Click("FullScoreDetails");yield return Draw("steps");Click("CloseDialog");
                    Assert.That(StateJson(),Is.EqualTo(prior));
                    var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                    app.SkipVisual();yield return W11Settle();prior=StateJson();var checkpoint=File.ReadAllBytes(app.SaveLocation);
                    app.ShowCommittedPresentationDetails07();yield return Draw("history");Click("CloseDialog");
                    Assert.That(StateJson(),Is.EqualTo(prior));CollectionAssert.AreEqual(checkpoint,File.ReadAllBytes(app.SaveLocation));

                    var fixture=NewBoundaryFixture("w16b-popup-layout");Equip09(fixture,"C41","C41","C53","C12");
                    BoundaryItem(fixture,"I03");
                    yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();Click("StartStage");yield return WaitForIdle();yield return Style();
                    prior=StateJson();checkpoint=File.ReadAllBytes(app.SaveLocation);
                    Click("ViewDice");yield return Draw("dice-library");Click("CloseDialog");
                    OpenRemoteItem();ChooseRemoteTargets();yield return Draw("remote");Click("CloseDialog");
                    Click("Controller_"+fixture.controllers[0].instanceId);yield return Draw("mimic");Click("CloseDialog");
                    app.ShowControllerOrder();yield return Draw("order");Click("CloseDialog");
                    Assert.That(StateJson(),Is.EqualTo(prior));CollectionAssert.AreEqual(checkpoint,File.ReadAllBytes(app.SaveLocation));

                    var shop=W14ShopFixture(true);yield return W14Load(shop);yield return Style();
                    var seller=app.Session.State.controllers.First();
                    prior=StateJson();checkpoint=File.ReadAllBytes(app.SaveLocation);
                    Click("Controller_"+seller.instanceId);Click("SellController");yield return Draw("sell-confirm");
                    Assert.That(Button("ConfirmSale").GetComponentInChildren<TextMeshProUGUI>().text,Does.Contain(app.Session.GetControllerSalePrice(seller.instanceId).ToString()));
                    Click("CancelSale");Click("CloseDialog");
                    Assert.That(StateJson(),Is.EqualTo(prior));CollectionAssert.AreEqual(checkpoint,File.ReadAllBytes(app.SaveLocation));
                }
            }
            finally
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,priorPreview);
                File.WriteAllLines(Path.Combine(folder,"measurements.txt"),report.Concat(failures.Select(f=>"FAIL "+f)));
            }
            Assert.That(failures,Is.Empty,string.Join("\n",failures));
        }

        private IEnumerator PopupFrames16(string path,List<string> report,List<string> failures)
        {
            yield return Art073Settle(.2f);
            var overlay=Overlay20X;
            var scrolls=overlay.GetComponentsInChildren<ScrollRect>(false).Where(s=>s.content!=null&&s.viewport!=null
                &&s.content.rect.height>s.viewport.rect.height+1).ToArray();
            int views=scrolls.Length==0?1:2;
            for(int side=0;side<views;side++)
            {
                foreach(var scroll in scrolls){scroll.StopMovement();scroll.verticalNormalizedPosition=side==0?1:0;}
                Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
                string stamp=Path.GetFileName(path)+(side==0?"-top":"-bottom");
                var shot=ScreenCapture.CaptureScreenshotAsTexture();
                try{File.WriteAllBytes(path+(side==0?"-top.png":"-bottom.png"),shot.EncodeToPNG());}
                finally{UnityEngine.Object.Destroy(shot);}
                var graphics=overlay.GetComponentsInChildren<Graphic>(false);
                var labels=overlay.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.enabled&&!string.IsNullOrWhiteSpace(t.text)).ToArray();
                int visible=0;
                foreach(var t in labels)
                {
                    t.ForceMeshUpdate();var ink=W11bGlyphRects(t);if(ink.Count==0)continue;
                    var sc=t.GetComponentInParent<ScrollRect>();
                    Rect viewport=sc!=null&&sc.viewport!=null?V06ScreenRect(sc.viewport):new Rect(0,0,Screen.width,Screen.height);
                    var shown=ink.Where(r=>viewport.Contains(r.center)).ToArray();
                    if(shown.Length==0)continue;
                    visible++;
                    string loc=stamp+" "+W11bPath(t.transform);
                    Rect box=V06ScreenRect(t.rectTransform);const float slack=1.5f;
                    if(t.isTextTruncated)failures.Add(loc+" truncated "+t.text);
                    if(shown.Any(r=>r.xMin<box.xMin-slack||r.xMax>box.xMax+slack||r.yMin<box.yMin-slack||r.yMax>box.yMax+slack))
                        failures.Add(loc+" glyph outside text box");
                    for(int g=Array.IndexOf(graphics,t)+1;g>0&&g<graphics.Length;g++)
                    {
                        if(!(graphics[g] is Image image)||!W11bPaints(image,true)||image.transform.IsChildOf(t.transform))continue;
                        Rect cover=V06ScreenRect(image.rectTransform);
                        if(shown.Any(r=>cover.Contains(r.center))){failures.Add(loc+" covered by "+W11bPath(image.transform));break;}
                    }
                }
                if(visible==0)failures.Add(stamp+" no visible popup text");
                report.Add(stamp+" visible labels="+visible+" scrolls="+scrolls.Length);
                if(side==0)continue;
                foreach(var scroll in scrolls)
                {
                    var rects=scroll.content.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.enabled&&!string.IsNullOrWhiteSpace(t.text))
                        .SelectMany(t=>W11bGlyphRects(t)).ToArray();
                    if(rects.Length==0){failures.Add(stamp+" empty scroll content "+scroll.name);continue;}
                    var last=rects.OrderBy(r=>r.yMin).First();var viewport=V06ScreenRect(scroll.viewport);
                    if(last.yMin<viewport.yMin-1.5f||last.yMax>viewport.yMax+1.5f)failures.Add(stamp+" last line clipped in "+scroll.name);
                }
            }
        }
    }
}
