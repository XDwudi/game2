using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest] public IEnumerator PLAY_V21_FW14_AllShopFacePickStepsKeepWholeLines()
        {
            Assert.That((Screen.width==1920&&Screen.height==1080)||(Screen.width==1280&&Screen.height==720));
            string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"../../artifacts/cache/v11/w21/facepick",Screen.width+"x"+Screen.height));
            Directory.CreateDirectory(dir);
            var report=new List<string>{IdentityHead20X()+" | Unity "+Application.unityVersion+" | "+Screen.width+"x"+Screen.height};
            var failures=new List<string>();
            string priorTheme=app.ThemeId11;int priorScale=Mathf.RoundToInt(UiKit.FontScale*100);
            bool priorClean=GameApp.CleanCapture11;
            var ids=app.Config.diceUpgrades.Select(u=>u.id).OrderBy(id=>id).Concat(new[]{"O04"}).ToArray();
            CollectionAssert.AreEqual(Enumerable.Range(1,11).Select(i=>"U"+i.ToString("D2")).Concat(new[]{"O04"}),ids);
            int captured=0;
            try
            {
                GameApp.CleanCapture11=true;
                foreach(string id in ids)
                {
                    var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();
                    AddBoundaryOffer(fixture,"wrap-"+id,id=="O04"?"service":"die_upgrade",id);
                    var die=fixture.dice.OrderBy(d=>d.createdSerial).First();
                    var target=die.faces.Single(f=>f.faceId=="F02");
                    target.baseColor=id=="U03"?"blue":"red";target.permanentPip=4;
                    if(id=="U11")die.faces.Single(f=>f.faceId==app.Config.Opposite("F02")).permanentPip=2;
                    if(id=="U06")die.faces.Single(f=>f.faceId=="F04").isGold=true;
                    yield return W14Load(fixture);app.ApplyTheme11(GameTheme.DefaultId);
                    string state=StateJson();var disk=File.ReadAllBytes(app.SaveLocation);
                    foreach(int percent in new[]{100,150})
                    {
                        W11TextScale(percent);
                        Click("Buy_wrap-"+id);
                        yield return Page("S1");
                        Click("Target_"+die.dieId);
                        bool whole=id=="U05"||id=="O04";
                        if(whole)Assert.That(HasActive("Face_F02"),Is.False,id+" whole-die target must skip face selection");
                        else {yield return Page("S2");Click("Face_F02");}
                        yield return Page("S3");
                        Assert.That(Button("ConfirmPurchase").interactable,Is.True,id+" must exercise the actual eligible preview");
                        if(id=="U06")
                        {
                            Click("ChangeFace14");Click("Face_F04");
                            yield return Page("S3","-illegal");
                            Assert.That(Button("ConfirmPurchase").interactable,Is.False);
                        }
                        Click("CancelFacePick14");
                        Assert.That(app.FacePickStep14,Is.EqualTo("S0"));
                        Assert.That(StateJson(),Is.EqualTo(state),id);CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation),id);

                        IEnumerator Page(string step,string variant="")
                        {
                            yield return new WaitForEndOfFrame();
                            string scope=id+"-"+percent+"-"+step+variant;
                            Assert.That(app.FacePickStep14,Is.EqualTo(step),scope);
                            Assert.That(app.ThemeId11,Is.EqualTo(GameTheme.DefaultId));
                            Assert.That(UiKit.FontScale,Is.EqualTo(percent/100f).Within(.001f));
                            var panel=FindActive<RectTransform>("FacePick14");
                            var order=app.canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(false);
                            var panelRect=V06ScreenRect(panel);
                            foreach(var t in panel.GetComponentsInChildren<TextMeshProUGUI>(false).Where(t=>t.enabled&&!string.IsNullOrWhiteSpace(t.text)))
                            {
                                var boxes=W13GlyphCheck(t,scope,order,failures);
                                if(boxes.Any(b=>!Art073Inside(b,panelRect)))failures.Add(scope+" "+t.name+" outside face panel");
                            }
                            W14ButtonsInside("FacePick14",scope,failures);
                            const BindingFlags member=BindingFlags.Instance|BindingFlags.NonPublic;
                            var type=typeof(GameApp);
                            string dieTitle=(string)type.GetMethod("OwnedDieLabel14",member).Invoke(app,new object[]{die.dieId});
                            void Line(string name,string source,bool balanced=false)
                            {
                                var label=panel.Find(name)?.GetComponent<TextMeshProUGUI>();
                                Assert.That(label,Is.Not.Null,scope+" "+name);
                                float width=label.rectTransform.rect.width;
                                Assert.That(width,Is.EqualTo(balanced||name=="FacePickPrompt14"&&step=="S2"?308:440).Within(.01f),scope+" "+name);
                                var wrap=typeof(GameApp).GetMethod("WrapMeasured21",BindingFlags.NonPublic|BindingFlags.Static);
                                string expected=(string)wrap.Invoke(null,new object[]{label,source,width,balanced?2:int.MaxValue,label.fontSize,balanced});
                                label.ForceMeshUpdate();
                                if(expected==null||label.text!=expected||label.textInfo.lineCount!=expected.Split('\n').Length)
                                    failures.Add(scope+" "+name+" source=["+source+"] expected=["+expected+"] actual=["+label.text+"]");
                                report.Add(scope+" "+name+" "+width+" @"+label.fontSize+" "+label.text.Replace("\n"," / "));
                            }
                            if(step=="S1")
                            {
                                string prompt=id=="U05"?"选一颗骰子，整颗变成玻璃骰":id=="O04"?"选一颗要删除的骰子":"选一颗骰子，再选要改的面";
                                Line("FacePickShort14",id=="O04"?app.Config.Service(id).shortText:app.Config.Upgrade(id).shortText);
                                Line("FacePickPrompt14",prompt+"（在下面的“我的骰子”里）");
                            }
                            else
                            {
                                Line("FacePickDieLabel14",dieTitle,true);
                                if(step=="S2")Line("FacePickPrompt14","选要改的面：点下面的方块，或点骰子上的面");
                                else
                                {
                                    var offer=app.Session.State.shop.offers.Single(o=>o.offerId=="wrap-"+id);
                                    var pick=type.GetField("facePick14",member).GetValue(app);
                                    object[] args={offer,pick,null,null};
                                    type.GetMethod("FacePickChange14",member).Invoke(app,args);
                                    string reason=(string)type.GetMethod("FacePickReason14",member).Invoke(app,new object[]{offer,pick});
                                    Line("PurchasePreviewBody",(string)args[2]);
                                    if(reason!=null)Line("PurchaseUnavailable",reason);
                                    else if(args[3] is string note)Line("FacePickNote14",note);
                                    Line("FacePickPrice14","价格 "+app.Session.GetOfferNetPrice(offer)+"（你有 "+PlayerNumberFormat.Gold(fixture.gold)+"）");
                                }
                            }
                            var shot=ScreenCapture.CaptureScreenshotAsTexture();
                            try{File.WriteAllBytes(Path.Combine(dir,scope+".png"),shot.EncodeToPNG());}
                            finally{UnityEngine.Object.Destroy(shot);}
                            captured++;
                        }
                    }
                }
            }
            finally
            {
                W11TextScale(priorScale);app.ApplyTheme11(priorTheme);GameApp.CleanCapture11=priorClean;
                File.WriteAllLines(Path.Combine(dir,"measurements.txt"),report.Concat(failures.Select(s=>"FAIL "+s)));
            }
            Assert.That(captured,Is.EqualTo(70),"34 reachable states and one illegal target, at each text size");
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(30)));
        }

        private static readonly MethodInfo WrapCard21 = typeof(GameApp).GetMethod("WrapLines21",
            BindingFlags.Static | BindingFlags.NonPublic);

        private static void CheckCardLines21(TextMeshProUGUI label, string source, string scope, List<string> failures)
        {
            label.ForceMeshUpdate();
            int supplied = label.text.Count(c => c == '\n') + 1;
            if (label.textInfo.lineCount != supplied)
                failures.Add(scope + ": TMP drew " + label.textInfo.lineCount + " lines for " + supplied + " supplied");
            string expected = (string)WrapCard21.Invoke(null,
                new object[] { label, source, label.rectTransform.rect.width, supplied });
            if (expected != label.text)
                failures.Add(scope + ": expected [" + expected + "], saw [" + label.text + "]");
        }

        [UnityTest]
        public IEnumerator PLAY_V21_FW12_AllStylesDrawCompleteCardLines()
        {
            var failures = new List<string>();
            var checkedStyles = new HashSet<string>();
            string previous = app.ThemeId11;
            try
            {
                StartRun("72112");
                foreach (var theme in ThemeRegistry.All)
                {
                    app.ApplyTheme11(theme.Id);
                    int controllerLabels = 0;
                    foreach (int percent in new[] { 100, 150 })
                    {
                        W11TextScale(percent);
                        for (int page = 0; page < GameApp.CardGalleryPages13; page++)
                        {
                            var gallery = app.ShowCardGallery13(page);
                            yield return null;
                            if (page <= 4)
                            {
                                var output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "cache",
                                    "v11", "w21", "gallery", Screen.width + "x" + Screen.height, theme.Id));
                                Directory.CreateDirectory(output);
                                yield return new WaitForEndOfFrame();
                                var shot = ScreenCapture.CaptureScreenshotAsTexture();
                                try { File.WriteAllBytes(Path.Combine(output, $"page-{page}-{percent}.png"), shot.EncodeToPNG()); }
                                finally { UnityEngine.Object.Destroy(shot); }
                            }
                            CheckForegroundFonts21(gallery.transform, theme.Id + "/" + percent + "/gallery" + page, failures);
                            var order = app.canvas.GetComponentsInChildren<UnityEngine.UI.Graphic>(false);
                            foreach (var label in gallery.GetComponentsInChildren<TextMeshProUGUI>(false)
                                         .Where(t => t.name == "ShortText11" && t.gameObject.activeInHierarchy))
                            {
                                string where = theme.Id + "/" + percent + "/page" + page + "/" + label.transform.parent.name;
                                W13GlyphCheck(label, where, order, failures);
                                // Mini cards retain TMP wrapping; priced rows and text cards use the new solver.
                                var parent = label.transform.parent;
                                if (parent.GetComponent<RectTransform>().rect.height < 200 && parent.Find("PriceButton13") == null) continue;
                                var icon = parent.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("Icon_C", StringComparison.Ordinal));
                                if (icon == null) continue;
                                var configured = app.Config.Controller(icon.name.Substring(5));
                                CheckCardLines21(label, configured.shortText, where, failures);
                                controllerLabels++;
                            }
                        }
                        app.HideCardGallery13();
                    }
                    Assert.That(controllerLabels, Is.GreaterThan(0), theme.Id);
                    checkedStyles.Add(theme.Id);
                }
            }
            finally
            {
                app.HideCardGallery13();
                W11TextScale(100);
                app.ApplyTheme11(previous);
            }
            Assert.That(checkedStyles.Count, Is.EqualTo(6));
            Assert.That(failures, Is.Empty, string.Join("\n", failures.Take(25)));
        }
    }
}
