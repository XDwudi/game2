using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private static readonly string[] ScreenThemes24 = {"default","swiss","sancai","comic","deco","glass"};
        private delegate IEnumerator Visit24(string screen, params Transform[] foreground);
        private static readonly string[] RequiredScreens24 = {
            "startup","menu-empty","menu-saved","new-empty","new-saved","quit","version",
            "handbook-hands","handbook-colours","handbook-rules","settings-0","settings-1","settings-2",
            "settings-themes","settings-display","select-common","select-pressure","select-boss-disabled",
            "select-boss-goat","chapter-intel","pause","save-failure","load-recovery","dice-styles",
            "guide-1","guide-2","guide-3","guide-4","guide-5","hint-0","hint-1","hint-2",
            "shop-common","shop-full","shop-offer-details","shop-handbook","facepick-s1","facepick-s2","facepick-s3",
            "receipt","receipt-details","loot-L1","loot-L2","loot-L3","loot-L4","loot-L5","loot-L6",
            "loot-L4-s2","loot-only-cash","loot-full-slots","shop-claim","result-win","result-win-page2","result-submissions","result-temporary","result-no-dice"
        };

        [UnityTest] public IEnumerator PLAY_V24_SC16_SE04_Default() => Matrix24("default");
        [UnityTest] public IEnumerator PLAY_V24_SC16_SE04_Swiss() => Matrix24("swiss");
        [UnityTest] public IEnumerator PLAY_V24_SC16_SE04_Sancai() => Matrix24("sancai");
        [UnityTest] public IEnumerator PLAY_V24_SC16_SE04_Comic() => Matrix24("comic");
        [UnityTest] public IEnumerator PLAY_V24_SC16_SE04_Deco() => Matrix24("deco");
        [UnityTest] public IEnumerator PLAY_V24_SC16_SE04_Glass() => Matrix24("glass");

        private IEnumerator Matrix24(string theme)
        {
            var errors = new List<string>();
            foreach (int scale in new[]{100,150})
            {
                string dir=Path.Combine(Application.dataPath,"..","..","artifacts","cache","v11","w24","screens",
                    Screen.width+"x"+Screen.height,theme,scale.ToString());
                Directory.CreateDirectory(dir);
                var report=new List<string>{IdentityHead20X()+" | Unity "+Application.unityVersion};
                int start=errors.Count;
                IEnumerator Capture(string name,params Transform[] roots)
                {
                    if ((name=="menu-empty" || name=="menu-saved") && HasActive("Toast"))
                        errors.Add("FAIL "+theme+" "+scale+" "+name+" | unexpected transient notice in the ordinary menu capture");
                    var foreground=ForegroundRoots24(name,roots);
                    var action=PrimaryAction24(name,foreground,report,errors);
                    yield return Frame24(theme+" "+scale+" "+name,Path.Combine(dir,name+".png"),foreground,report,errors,action);
                    if (name.StartsWith("hint-", StringComparison.Ordinal))
                        MeasureFirstHint16(theme, scale, name, report, errors);
                    IEnumerator PageStep(ScrollRect scroll,int step)
                    {
                        string detail=name+"-"+scroll.name+"-"+step;
                        PrimaryAction24(name,foreground,report,errors);
                        yield return Frame24(detail,Path.Combine(dir,detail+".png"),new[]{scroll.viewport.transform},report,errors);
                    }
                    yield return ScrollPages24(foreground,PageStep);
                }
                // These four SC-03 boundary views do not extend the established W20X 56-screen inventory.
                var extraSeen=new HashSet<string>();
                IEnumerator Extra(string page,params Transform[] roots)
                {
                    Assert.That(extraSeen.Add(page),Is.True,page);
                    yield return new WaitForSecondsRealtime(.2f);
                    var fg=ForegroundRoots24(page,roots);
                    var action=PrimaryAction24(page,fg,report,errors);
                    string folder=Path.Combine(dir,"primary-extra");Directory.CreateDirectory(folder);
                    yield return Frame24(theme+" "+scale+" "+page,Path.Combine(folder,page+".png"),fg,report,errors,action);
                }
                try
                {
                    yield return Walk24(theme,scale,Capture,Extra);
                    CollectionAssert.AreEquivalent(new[]{"settings-confirm","notice-full","select-busy","select-save-pending"},extraSeen);
                }
                finally
                {
                    FinishHitAreas16(report,errors);
                    File.WriteAllLines(Path.Combine(dir,"measurements.txt"),report.Concat(errors.Skip(start)));
                }
            }
            Assert.That(errors,Is.Empty,string.Join("\n",errors));
        }

        // SC-03 counts active, painted buttons in the current foreground. A coloured outline,
        // text, or a modal's underlying game is not another primary button.
        private Transform[] ForegroundRoots24(string page,Transform[] roots)
        {
            var overlay=Overlay20X;
            return roots.Select(root=>
            {
                Assert.That(root!=null&&root.gameObject.activeInHierarchy,Is.True,page+" inactive foreground root");
                if(root!=overlay)return root;
                // UiKit pools Rect:Dialog and Box:Dialog separately under the same overlay.
                // The first namesake may be inactive after navigating from New Run to Quit.
                var dialogs=root.Cast<Transform>().Where(child=>child.name=="Dialog"&&child.gameObject.activeInHierarchy).ToArray();
                Assert.That(dialogs.Length,Is.EqualTo(1),page+" active Dialog count");
                return dialogs[0];
            }).ToArray();
        }

        private Button PrimaryAction24(string page,Transform[] foreground,List<string> report,List<string> errors)
        {
            string expected;
            switch(page)
            {
                case "menu-empty": expected="NewRun"; break;
                case "menu-saved": expected="ContinueRun"; break;
                case "new-empty": case "new-saved": expected="StartNew"; break;
                case "pause": expected="Resume"; break;
                case "save-failure": expected="RetrySave"; break;
                case "settings-display": expected="ApplyDisplay"; break;
                case "settings-confirm": expected="ConfirmDisplay"; break;
                case "startup": case "notice-full": expected=""; break;
                case "select-busy": case "select-save-pending":
                    expected="";break;
                case "select-common": case "select-pressure": case "select-boss-goat": case "select-boss-disabled":
                    expected=app.IsBusy||app.Session.HasPendingSave ? "" : "StartStage"; break;
                case "settings-0": case "settings-1": case "settings-2": case "settings-themes":
                case "handbook-hands": case "handbook-colours": case "handbook-rules": case "shop-handbook":
                case "chapter-intel": case "dice-styles": case "version": case "quit": case "load-recovery":
                case "guide-1": case "guide-2": case "guide-3": case "guide-4": case "guide-5":
                case "hint-0": case "hint-1": case "hint-2":
                    expected=""; break;
                default: return null;
            }
            var controls=foreground.SelectMany(r=>r.GetComponentsInChildren<Button>(false)).Distinct().ToArray();
            if(page=="quit")Assert.That(controls.Any(b=>b.name=="ConfirmQuit"&&b.IsInteractable()),Is.True,"quit confirmation control in foreground");
            // A disabled primary-coloured face still looks like a primary action. Count its paint,
            // then independently check the expected action can actually be used.
            var active=controls.Where(b=>b.targetGraphic is Image face && W11bPaints(face,true)&& W11PrimaryColoured(face.color)).ToArray();
            string names=string.Join(",",active.Select(button=>button.name));
            string row=page+" SC-03 primary ["+names+"], expected ["+expected+"]";
            report.Add(row);
            if(names!=expected)errors.Add(row);
            var primary=active.FirstOrDefault(b=>b.name==expected);
            if(primary!=null&&!primary.IsInteractable())errors.Add(page+" primary action is disabled: "+expected);
            return primary;
        }

        private static bool AdjustedPrimaryFace24(Image face)
        {
            // Match the real W11b rule, including its 8-bit rounding: same hue family alone
            // cannot prove that a sub-4.95 result comes from a permitted surface adjustment.
            Color32 applied=face.color;
            foreach(var toward in new[]{Color.black,Color.white})
            {
                var tint=UiKit.Primary;
                for(int step=1;step<=24;step++)
                {
                    tint=Color.Lerp(tint,toward,.05f);
                    if(((Color32)tint).Equals(applied))return true;
                }
            }
            return false;
        }

        private static bool PrimaryPick24(float contrast,Color32 background,bool adjusted)
        {
            if(contrast>=GameApp.PickContrast11)return true;
            if(contrast<GameApp.MinContrast11||!adjusted)return false;
            return !new[]{UiKit.Ink,UiKit.Paper,UiKit.TextColor}.Any(c=>
                GameApp.Contrast11((Color32)c,background)>=GameApp.PickContrast11);
        }

        private IEnumerator CheckPrimaryArrow24(string scope,string path,Image arrow,Color32[] drawn,Color32[] background,
            int width,int height,bool adjusted,List<string> report,List<string> failures)
        {
            // Capture the same Image, including sprite padding, UVs and pixel adjustment, on a black matte.
            // Place the reference above the foreground: an obstruction in the original frame must still fail.
            var matte=UnityEngine.Object.Instantiate(arrow,arrow.transform.parent,false);
            try
            {
                matte.name="ArrowContrastProbe24";matte.raycastTarget=false;
                var brush=UnityEngine.Object.Instantiate(arrow,matte.transform,false);
                var rect=brush.rectTransform;
                rect.anchorMin=Vector2.zero;rect.anchorMax=Vector2.one;rect.pivot=arrow.rectTransform.pivot;
                rect.offsetMin=rect.offsetMax=Vector2.zero;rect.localRotation=Quaternion.identity;rect.localScale=Vector3.one;
                brush.raycastTarget=false;brush.color=Color.white;brush.enabled=true;
                matte.sprite=null;matte.overrideSprite=null;matte.type=Image.Type.Simple;matte.material=null;
                matte.color=Color.black;matte.enabled=true;
                var canvas=matte.gameObject.AddComponent<Canvas>();
                canvas.overrideSorting=true;canvas.sortingLayerID=arrow.GetComponentInParent<Canvas>().sortingLayerID;
                canvas.sortingOrder=short.MaxValue;
                Canvas.ForceUpdateCanvases();yield return new WaitForEndOfFrame();
                var reference=Screen20Xb();
                var area=V06ScreenRect(arrow.rectTransform);
                int left=Mathf.Max(0,Mathf.CeilToInt(area.xMin)),right=Mathf.Min(width,Mathf.FloorToInt(area.xMax));
                int bottom=Mathf.Max(0,Mathf.CeilToInt(area.yMin)),top=Mathf.Min(height,Mathf.FloorToInt(area.yMax));
                int samples=0,misses=0;float lowest=float.PositiveInfinity;
                for(int y=bottom;y<top;y++)for(int x=left;x<right;x++)
                {
                    int i=y*width+x;var pixel=reference[i];
                    if(pixel.r<254||pixel.g<254||pixel.b<254)continue;
                    float ratio=GameApp.Contrast11(drawn[i],background[i]);samples++;
                    lowest=Mathf.Min(lowest,ratio);
                    if(!PrimaryPick24(ratio,background[i],adjusted))misses++;
                }
                string line=scope+" primary arrow, visible core pixels="+samples+", minimum contrast="+lowest.ToString("F3")+", misses="+misses;
                report.Add(line);if(samples<3||misses>0)failures.Add(line);

                // Three native-pixel crops in draw / background / mask order allow offline verification of every reported pixel.
                if(right>left&&top>bottom)
                {
                    int w=right-left,h=top-bottom;var strip=new Color32[3*w*h];
                    for(int y=0;y<h;y++)
                    {
                        int i=(bottom+y)*width+left,j=y*w*3;
                        Array.Copy(drawn,i,strip,j,w);Array.Copy(background,i,strip,j+w,w);Array.Copy(reference,i,strip,j+2*w,w);
                    }
                    var proof=new Texture2D(w*3,h,TextureFormat.RGBA32,false);
                    try
                    {
                        proof.SetPixels32(strip);proof.Apply();
                        var dir=Path.Combine(Path.GetDirectoryName(path),"primary-arrow");Directory.CreateDirectory(dir);
                        File.WriteAllBytes(Path.Combine(dir,Path.GetFileName(path)),proof.EncodeToPNG());
                    }
                    finally {UnityEngine.Object.Destroy(proof);}
                }
            }
            finally {matte.gameObject.SetActive(false);UnityEngine.Object.Destroy(matte.gameObject);}
        }

        // Both the screenshot matrix and the table-text inventory must see real scrolled rows.
        private static IEnumerator ScrollPages24(Transform[] roots,Func<ScrollRect,int,IEnumerator> inspect)
        {
            foreach(var scroll in roots.SelectMany(r=>r.GetComponentsInChildren<ScrollRect>(false)).Distinct())
            {
                float extra=scroll.content.rect.height-scroll.viewport.rect.height;
                if(!scroll.vertical||extra<2)continue;
                float previous=scroll.verticalNormalizedPosition;
                int steps=Mathf.CeilToInt(extra/Mathf.Max(1,scroll.viewport.rect.height*.8f));
                try
                {
                    for(int step=1;step<=steps;step++)
                    {
                        scroll.StopMovement();scroll.verticalNormalizedPosition=1f-(float)step/steps;
                        yield return null;
                        yield return inspect(scroll,step);
                    }
                }
                finally{scroll.StopMovement();scroll.verticalNormalizedPosition=previous;}
            }
        }

        private IEnumerator Walk24(string theme,int scale,Visit24 visit,Visit24 primaryBoundary=null)
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720);
            var seen=new HashSet<string>();string oldDirectory=saveDirectory;
            bool wasClean=GameApp.CleanCapture11;
            bool preview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);
            int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey);
            bool hasTheme=PlayerPrefs.HasKey(ThemeRegistry.PreferenceKey);
            string previous=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            IEnumerator Page(string name,params Transform[] roots)
            {
                Assert.That(seen.Add(name),Is.True,"Duplicate scene "+name);
                yield return new WaitForSecondsRealtime(.40f);
                Assert.That(app.ThemeId11,Is.EqualTo(theme),name);
                Assert.That(UiKit.FontScale,Is.EqualTo(scale/100f).Within(.001f),name);
                Assert.That(roots,Is.Not.Empty,name);
                Assert.That(roots.All(r=>r!=null&&r.gameObject.activeInHierarchy),Is.True,name+" inactive foreground");
                yield return visit(name,roots);
            }
            try
            {
                // A separate storage root for each walk allows a genuine empty menu and then a genuine saved menu.
                saveDirectory=Path.Combine(oldDirectory,"screen-walk-"+theme+"-"+scale);
                Directory.CreateDirectory(saveDirectory);GameApp.SaveDirectoryOverride=saveDirectory;
                GameApp.CleanCapture11=true;
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,theme);
                PlayerPrefs.SetInt("art07.textScale",scale);PlayerPrefs.SetInt("art07.guideSeen",1);
                foreach(string key in BoundaryHintKeys07)PlayerPrefs.SetInt(key,1);
                yield return Startup24(Page);
                yield return Page("menu-empty",W14Main);Click("NewRun");yield return Page("new-empty",Overlay20X);Click("CloseDialog");
                Click("Quit");yield return Page("quit",Overlay20X);Click("CloseDialog");
                Click("BuildVersion");yield return Page("version",Overlay20X);Click("CloseDialog");
                Click("MenuHandbook");
                foreach(var tab in new[]{("HandbookHands","hands"),("HandbookColors","colours"),("HandbookRules09","rules")})
                {Click(tab.Item1);yield return Page("handbook-"+tab.Item2,Overlay20X);}
                Click("CloseDialog");Click("MenuSettings");
                for(int i=0;i<3;i++){Click("SettingsTab"+i);yield return Page("settings-"+i,Overlay20X);}
                Click("SettingsTab1");Click("Theme");yield return Page("settings-themes",Overlay20X);Click("ThemeDone16");
                Click("DisplaySettings");yield return Page("settings-display",Overlay20X);
                if(primaryBoundary!=null)
                {
                    // The draft is the unchanged actual mode, monitor and size. Exercise the real
                    // apply/confirmation route using the walk's isolated display preference file.
                    var actual=app.DisplayPreferences.ReadActual();Click("ApplyDisplay");
                    double deadline=Time.realtimeSinceStartupAsDouble+12;
                    while(app.DisplayPreferences.IsPending&&!app.DisplayPreferences.CanConfirm&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                    Assert.That(app.DisplayPreferences.IsPending&&app.DisplayPreferences.CanConfirm,Is.True,app.DisplayPreferences.Notice);
                    var confirmed=app.DisplayPreferences.ReadActual();
                    Assert.That(new[]{confirmed.width,confirmed.height},Is.EqualTo(new[]{actual.width,actual.height}));
                    Assert.That(confirmed.displayName,Is.EqualTo(actual.displayName));Assert.That(confirmed.mode,Is.EqualTo(actual.mode));
                    yield return primaryBoundary("settings-confirm",Overlay20X);Click("ConfirmDisplay");
                    Assert.That(app.DisplayPreferences.IsPending,Is.False);
                }
                W11Invoke("CloseModal");
                if(primaryBoundary!=null)
                {
                    string detail=string.Join("\n",app.Config.lootKinds.Select(k=>k.shortText));
                    W11Invoke("QueueNotice07",detail,false);app.Refresh();yield return null;
                    Click("ToastDetails07");Assert.That(InformationText07("NoticeDetailBody"),Is.EqualTo(detail));
                    yield return primaryBoundary("notice-full",Overlay20X);Click("CloseDialog");
                }
                StartRun("24016");yield return Page("select-common",W14Main);
                Click("Pause");yield return Page("pause",Overlay20X);Click("MainMenu");
                yield return Page("menu-saved",W14Main);Click("NewRun");yield return Page("new-saved",Overlay20X);Click("CloseDialog");
                Click("ContinueRun");
                // Capture the ordinary menus before exercising and abandoning a real failed save.
                if(primaryBoundary!=null)yield return PrimaryStageBoundary24(primaryBoundary);
                Click("StartStage");yield return WaitForIdle();
                using(var locked=new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
                {
                    Click("Roll");Assert.That(app.Session.HasPendingSave,Is.True);
                    yield return Page("save-failure",Overlay20X);
                }
                Click("RetrySave");app.SkipVisual();yield return WaitForIdle();
                byte[] valid=File.ReadAllBytes(app.SaveLocation);
                File.WriteAllBytes(app.SaveLocation+".backup",valid);File.WriteAllText(app.SaveLocation,"{damaged fixture");
                yield return LoadBootstrap();Assert.That(HasActive("CloseLoadIssue07"),Is.True);
                yield return Page("load-recovery",Overlay20X);Click("CloseLoadIssue07");
                yield return ShopAndStages24(Page);
                yield return Guides24(Page);
                yield return Rewards24(Page);
                Assert.That(seen,Is.EquivalentTo(RequiredScreens24),theme+" "+scale+" complete routes");
            }
            finally
            {
                saveDirectory=oldDirectory;GameApp.SaveDirectoryOverride=oldDirectory;
                GameApp.CleanCapture11=wasClean;
                if(preview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                if(hasTheme)PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,previous);else PlayerPrefs.DeleteKey(ThemeRegistry.PreferenceKey);
            }
        }

        private IEnumerator PrimaryStageBoundary24(Visit24 capture)
        {
            var before=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;
            // Stage preview does not start a transaction clock itself; inject the same transient
            // Busy flag used by the presenter and by SE-05. Never change the committed state.
            app.Session.Busy=true;
            try
            {
                app.Refresh();Assert.That(Button("StartStage").IsInteractable(),Is.False);
                yield return capture("select-busy",W14Main);
            }
            finally {app.Session.Busy=false;app.Refresh();}
            using(new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                Click("StartStage");Assert.That(app.Session.HasPendingSave,Is.True);
                // The player can dismiss the error modal; now the pending selection is the actual foreground.
                Click("CloseDialog");Assert.That(app.Session.State.phase,Is.EqualTo("StagePreview"));
                Assert.That(Button("StartStage").IsInteractable(),Is.False);
                yield return capture("select-save-pending",W14Main);
                CollectionAssert.AreEqual(before,File.ReadAllBytes(app.SaveLocation));
            }
            app.Refresh();Click("AbandonPendingSave");Assert.That(app.Session.HasPendingSave,Is.False);
            Assert.That(app.Session.ActionSequence,Is.EqualTo(seq));CollectionAssert.AreEqual(before,File.ReadAllBytes(app.SaveLocation));
        }

        private IEnumerator Startup24(Visit24 visit)
        {
#if UNITY_EDITOR
            var release=new TaskCompletionSource<bool>();
            GameApp.StartupReadBarrierForTests07=path=>{
                if(!release.Task.Wait(TimeSpan.FromSeconds(30)))throw new TimeoutException("Screen-walk read barrier timed out.");
            };
            try
            {
                yield return LoadingPendingScene07();
                double deadline=Time.realtimeSinceStartupAsDouble+5;
                while(!HasActive("StartupReadHint07")&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(HasActive("StartupReadHint07"),Is.True);Assert.That(app.StartupReady07,Is.False);
                yield return visit("startup",W14Main);
            }
            finally { release.TrySetResult(true);GameApp.StartupReadBarrierForTests07=null; }
            yield return LoadingWaitReady07();
#else
            Assert.Inconclusive("The reproducible startup read barrier is provided by the windowed Editor verification runner.");
            yield break;
#endif
        }

        private IEnumerator ShopAndStages24(Visit24 visit)
        {
            yield return W14Load(ShopAfterStage10(0));Click("LeaveShop");yield return WaitForIdle();
            Click("Risk");yield return visit("select-pressure",W14Main);
            foreach(bool goat in new[]{false,true})
            {
                var shop=ShopAfterStage10(1);shop.cycles[0].modifierId="B25";
                Equip09(shop,goat?new[]{"C100","C13","C131","C50","C46"}:new[]{"C100","C13","C50","C46"});
                yield return W14Load(shop);Click("LeaveShop");yield return WaitForIdle();
                string warnings=InformationText07("ControllerOrderWarning");
                Assert.That(warnings,Does.Contain("祭坛").And.Contain(goat?"替罪羊":"失效"));
                yield return visit(goat?"select-boss-goat":"select-boss-disabled",W14Main);
            }
            yield return W14Load(W14ShopFixture(false));yield return visit("shop-common",W14Main);
            var offer=app.Session.State.shop.offers.First(o=>o.category=="controller");
            Click("Offer_"+offer.offerId);yield return visit("shop-offer-details",Overlay20X);W11Invoke("CloseModal");
            Click("ViewCycle");yield return visit("chapter-intel",Overlay20X);Click("CloseCycle");
            Click("Handbook");yield return visit("shop-handbook",Overlay20X);Click("CloseDialog");
            app.ShowDiceStyleLegend073();yield return visit("dice-styles",Overlay20X);W11Invoke("CloseModal");
            yield return W14Load(W14ShopFixture(true));yield return visit("shop-full",W14Main);
            var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();AddBoundaryOffer(fixture,"pick-gold","die_upgrade","U06");
            yield return W14Load(fixture);
            string die=app.Session.State.dice.OrderBy(d=>d.createdSerial).First().dieId;
            Click("Buy_pick-gold");yield return visit("facepick-s1",FindActive<RectTransform>("FacePick14"));
            Click("Target_"+die);yield return visit("facepick-s2",FindActive<RectTransform>("FacePick14"));
            Click("Face_F02");yield return visit("facepick-s3",FindActive<RectTransform>("FacePick14"));Click("CancelFacePick14");
        }

        private IEnumerator Guides24(Visit24 visit)
        {
            PlayerPrefs.SetInt("art07.guideSeen",0);
            var fixture=NewBoundaryFixture("sc16-guide");fixture.isDebug=false;fixture.debugWorkspace=false;
            foreach(var die in fixture.dice)foreach(var face in die.faces){face.permanentPip=6;face.baseColor="blue";}
            string path=Path.Combine(saveDirectory,"active-run.json");
            new AtomicSaveStore(path,app.Config,SaveSlotPolicy.Normal).Save(fixture);
            yield return LoadBootstrap();Click("ContinueRun");Click("StartStage");
            for(int i=1;i<=5;i++)
            {
                yield return new WaitForSecondsRealtime(.25f);
                Assert.That(InformationText07("GuideTitle"),Does.StartWith(i+" / 5"));
                yield return visit("guide-"+i,FindActive<RectTransform>("FirstRunGuide"));
                if(i==1){Click("Roll");app.SkipVisual();}
                if(i==2)foreach(var die in app.Session.State.dice)ClickDie(die.dieId);
                if(i==3){Click("Confirm");yield return WaitForIdle();}
                if(i==4){Click("Submit");app.SkipVisual();Assert.That(app.Session.CanEndStage);Click("EndStage");Click("SettlementContinue");}
            }
            Click("DismissGuide");
            for(int kind=0;kind<3;kind++)
            {
                foreach(string key in BoundaryHintKeys07)PlayerPrefs.SetInt(key,1);
                PlayerPrefs.DeleteKey(BoundaryHintKeys07[kind]);
                var hints=NewBoundaryFixture("sc16-hint-"+kind);hints.isDebug=false;hints.debugWorkspace=false;
                if(kind==1)hints.controllers.Add(BoundaryController(hints,"C53"));
                var core=new GameSession(app.Config,new MemorySaveStore(),hints);CoreShopStep(core,CommandKind.StartStage);
                if(kind==0)for(int i=0;i<3;i++)CoreShopStep(core,CommandKind.Roll);
                hints=core.State;if(kind==2){hints.stage.score=hints.stage.target;hints.stage.qualified=true;}
                new AtomicSaveStore(path,app.Config,SaveSlotPolicy.Normal).Save(hints);
                yield return LoadBootstrap();Click("ContinueRun");
                Assert.That(InformationText07("ActionReason"),Does.StartWith("提示："));
                yield return visit("hint-"+kind,FindActive<RectTransform>("ActionReason"),Button("DismissBoundaryHint07").transform);
                Click("DismissBoundaryHint07");
            }
        }

        private IEnumerator Rewards24(Visit24 visit)
        {
            foreach(string kind in new[]{"L1","L2","L3","L4","L5","L6"})
            {
                yield return LoadBoundaryFixture(BeforeBossLoot15(kind,kind=="L6"));ResumeBoundaryFixture();
                FinalOptions07("Instant",false);
                if(kind=="L1")
                {
                    yield return visit("receipt",FindActive<RectTransform>("Rewards"));
                    Click("SettlementDetailsToggle15");yield return visit("receipt-details",FindActive<RectTransform>("Rewards"));
                }
                Click("SettlementContinue");yield return WaitForIdle();Assert.That(app.LootPage15,Is.True);
                yield return visit("loot-"+kind,W14Main);
                if(kind=="L4")
                {
                    var offer=app.Session.State.shop.offers.First(o=>o.groupId=="LOOT"&&o.category!="gold"&&o.contentId!="U05"&&Button("Buy_"+o.offerId).interactable);
                    var die=app.Session.State.dice.First(d=>d.faces.Any(f=>app.Session.IsUpgradeTargetLegal(offer.contentId,d.dieId,f.faceId)));
                    Click("Buy_"+offer.offerId);Click("Target_"+die.dieId);
                    Assert.That(app.FacePickStep14,Is.EqualTo("S2"));
                    yield return visit("loot-L4-s2",FindActive<RectTransform>("FacePick14"));Click("CancelFacePick14");
                }
                if(kind=="L6")
                {
                    var offer=app.Session.State.shop.offers.First(o=>o.groupId=="LOOT"&&o.category=="consumable"&&Button("Buy_"+o.offerId).interactable);
                    Click("Buy_"+offer.offerId);yield return W14Load(app.Session.State);Click("ViewGifts");
                    Assert.That(app.LootFromShop15,Is.True);yield return visit("shop-claim",W14Main);
                }
            }
            foreach(bool fullDice in new[]{true,false})
            {
                var fixture=BeforeBossLoot15(fullDice?"L5":"L6");
                if(fullDice)
                {
                    while(fixture.dice.Count(d=>d.lifetime=="permanent")<app.Config.maxPermanentDice)
                        fixture.dice.Add(DiceFactory.CreateStandard(app.Config,fixture.nextDieSerial++));
                }
                else while(fixture.inventory.Count<fixture.consumableSlots)BoundaryItem(fixture,"I01");
                yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();FinalOptions07("Instant",false);
                Click("SettlementContinue");yield return WaitForIdle();Assert.That(app.LootPage15);
                Assert.That(HasActive(fullDice?"LootOnlyCash15":"LootRoomHint15"));
                yield return visit(fullDice?"loot-only-cash":"loot-full-slots",W14Main);
            }
            foreach(string kind in new[]{"win","submissions","temporary","no-dice"})
            {
                yield return TerminalFixture15(kind);if(HasActive("SkipResultReveal"))Click("SkipResultReveal");
                yield return visit("result-"+kind,W14Main);
                if(kind=="win")
                {
                    Click("ResultDiceNext");Assert.That(InformationText07("ResultDicePage"),Is.EqualTo("2/2"));
                    yield return visit("result-win-page2",W14Main);
                }
            }
        }

        private static bool PrintedCharacter24(char ch)
        {
            // TMP_InputField appends U+200B even to a genuinely empty seed. Its visible placeholder is measured separately.
            return !char.IsWhiteSpace(ch)&&char.GetUnicodeCategory(ch)!=System.Globalization.UnicodeCategory.Format;
        }

        private static Rect GlyphArea24(Transform transform,TMP_CharacterInfo glyph)
        {
            var a=transform.TransformPoint(glyph.bottomLeft);var b=transform.TransformPoint(glyph.topLeft);
            var c=transform.TransformPoint(glyph.topRight);var d=transform.TransformPoint(glyph.bottomRight);
            return Rect.MinMaxRect(Mathf.Min(a.x,b.x,c.x,d.x),Mathf.Min(a.y,b.y,c.y,d.y),
                Mathf.Max(a.x,b.x,c.x,d.x),Mathf.Max(a.y,b.y,c.y,d.y));
        }

        // Compare actual painted pixels in their own coordinate system. Rotated cards cannot use the
        // screen rectangle spanned by a sliced sprite's two diagonal corners.
        private struct InkArea24
        {
            private Rect rect;
            private Matrix4x4 inverse;
            public InkArea24(RectTransform transform,Rect rect)
            {
                inverse=transform.worldToLocalMatrix;this.rect=rect;
                this.rect.xMin-=1.5f;this.rect.xMax+=1.5f;this.rect.yMin-=1.5f;this.rect.yMax+=1.5f;
            }
            public bool Contains(Vector3 point)=>rect.Contains(inverse.MultiplyPoint3x4(point));
        }

        private sealed class InkBounds24
        {
            public InkArea24 label;
            public InkArea24[] paper,rings;
            public string surface;
        }

        private static InkBounds24 Bounds24(TextMeshProUGUI label,List<Rect> visible,Graphic[] order)
        {
            var layers=new List<Image>();
            W11bVisibleFrame(label,visible,order,out string surface,layers);
            var bounds=new InkBounds24 {label=new InkArea24(label.rectTransform,label.rectTransform.rect),surface=surface};
            if(layers.Count==0)return bounds;
            var face=layers[0];var tiles=new List<Image>{face};
            // A die-preview modal cuts a real aperture into one sheet with four rectangles. Keep that hole open:
            // an ink pixel must be on at least one actual paper tile, including at the seam between two tiles.
            foreach(string prefix in new[]{"Paper","Panel"})
            {
                var names=new[]{prefix+"Top",prefix+"Right",prefix+"Bottom",prefix+"Left"};
                if(!names.Contains(face.name))continue;
                var parts=names.Select(name=>face.transform.parent.Find(name)?.GetComponent<Image>()).ToArray();
                if(parts.All(p=>p!=null&&W11bPaints(p,true)&&p.color==face.color))
                {tiles=parts.ToList();bounds.surface=face.transform.parent.name+"/"+prefix+" (four tiles)";}
            }
            bounds.paper=tiles.Select(p=>new InkArea24(p.rectTransform,W11bInnerRect(p,false,true))).ToArray();
            bounds.rings=layers.Skip(1).Select(p=>new InkArea24(p.rectTransform,W11bInnerRect(p,true,true))).ToArray();
            return bounds;
        }

        // W11b's legacy pass excludes the entire modal layer. These samples use the supplied foreground
        // (including the modal itself), the same real glyph geometry and a second frame with only its ink hidden.
        private IEnumerator Frame24(string scope,string path,Transform[] roots,List<string> report,List<string> failures,Button primary=null)
        {
            Canvas.ForceUpdateCanvases();
            var order=app.canvas.GetComponentsInChildren<Graphic>(false);
            var labels=roots.SelectMany(r=>r.GetComponentsInChildren<TextMeshProUGUI>(false)).Distinct()
                .Where(t=>t.isActiveAndEnabled&&!string.IsNullOrWhiteSpace(t.text)&&t.color.a*t.canvasRenderer.GetInheritedAlpha()>.05f).ToArray();
            var measured=new Dictionary<TextMeshProUGUI,List<(Rect box,Color32 colour,char character,float points)>>();
            var bounds=new Dictionary<TextMeshProUGUI,InkBounds24>();
            foreach(var label in labels)
            {
                label.ForceMeshUpdate();
                var chars=label.textInfo.characterInfo.Take(label.textInfo.characterCount).Where(c=>c.isVisible&&PrintedCharacter24(c.character)).ToArray();
                var ink=chars.Select(c=>GlyphArea24(label.transform,c)).ToList();
                string id=scope+" "+W20XPath(label.transform,app.canvas.transform);
                if(label.isTextTruncated||ink.Count!=label.GetParsedText().Count(PrintedCharacter24))
                    failures.Add(id+" truncated or missing glyph: "+label.GetParsedText());
                var clip=new Rect(0,0,Screen.width,Screen.height);
                var scrolls=label.GetComponentsInParent<ScrollRect>(false).Where(s=>label.transform.IsChildOf(s.content)).ToArray();
                foreach(var mask in scrolls)
                {
                    if(!label.transform.IsChildOf(mask.content))continue;
                    var vp=V06ScreenRect(mask.viewport);
                    clip=Rect.MinMaxRect(Math.Max(clip.xMin,vp.xMin),Math.Max(clip.yMin,vp.yMin),
                        Math.Min(clip.xMax,vp.xMax),Math.Min(clip.yMax,vp.yMax));
                }
                var samples=new List<(Rect box,Color32 colour,char character,float points)>();
                var visible=ink.Where(box=>box.xMin>=clip.xMin&&box.xMax<=clip.xMax&&box.yMin>=clip.yMin&&box.yMax<=clip.yMax).ToList();
                if(scrolls.Length==0&&visible.Count!=ink.Count)failures.Add(id+" visible foreground outside the screen");
                if(visible.Count==0)continue;
                bounds[label]=Bounds24(label,visible,order);
                var printed=new List<TMP_CharacterInfo>();
                for(int i=0;i<chars.Length;i++)
                {
                    var c=chars[i];var b=ink[i];
                    if(b.xMin<clip.xMin||b.xMax>clip.xMax||b.yMin<clip.yMin||b.yMax>clip.yMax)continue;
                    samples.Add((b,c.color,c.character,c.pointSize));
                    if(c.color.a>0)printed.Add(c);
                }
                CheckForegroundFont21(label,printed,scope,failures);
                if(samples.Count>0)measured[label]=samples;
            }
            MeasureScreenAccess16(path,roots,measured,report,failures);
            Assert.That(measured.Count,Is.GreaterThan(0),scope+" has no foreground text in its viewport");
            bool adjusted=primary!=null&&AdjustedPrimaryFace24((Image)primary.targetGraphic);
            if(primary!=null&&!measured.Keys.Any(t=>t.GetComponentInParent<Button>()==primary))
                failures.Add(scope+" primary has no visible label");
            yield return new WaitForEndOfFrame();
            var arrow=primary!=null ? primary.transform.Find("Arrow")?.GetComponent<Image>() : null;
            bool arrowWasEnabled=arrow!=null&&arrow.enabled;
            if(primary!=null && primary.name=="StartStage" && (arrow==null || !arrow.isActiveAndEnabled || arrow.sprite==null))
                failures.Add(scope+" StartStage is missing its visible arrow");
            var shot=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                File.WriteAllBytes(path,shot.EncodeToPNG());var drawn=shot.GetPixels32();
                if(arrow!=null&&arrow.isActiveAndEnabled&&arrow.sprite!=null)
                {
                    // Hide only this arrow; text, other images and the exact button face remain drawn.
                    arrow.enabled=false;yield return new WaitForEndOfFrame();
                    var arrowBackground=Screen20Xb();
                    yield return CheckPrimaryArrow24(scope,path,arrow,drawn,arrowBackground,shot.width,shot.height,adjusted,report,failures);
                    arrow.enabled=true;
                }
                foreach(var label in measured.Keys)label.enabled=false;
                yield return new WaitForEndOfFrame();var blank=Screen20Xb();
                foreach(var pair in measured)
                {
                    var label=pair.Key;var control=label.GetComponentInParent<Selectable>();
                    float lowest=float.MaxValue;
                    bool primaryText=primary!=null&&control==primary;
                    float bestStroke=1;int opaqueStrokes=0,opaqueMisses=0;
                    var boundary=bounds[label];string id=scope+" "+W20XPath(label.transform,app.canvas.transform);
                    foreach(var sample in pair.Value)
                    {
                        var b=sample.box;
                        float strongest=0;bool outsideOwn=false,outsideFrame=false;
                        // The sample below is the pixel centre (x+.5,y+.5). Include centres inside the
                        // glyph even when a one-pixel stroke straddles an integer boundary at 720p.
                        for(int y=Mathf.Max(0,Mathf.CeilToInt(b.yMin-.5f));y<=Mathf.Min(shot.height-1,Mathf.FloorToInt(b.yMax-.5f));y++)
                        for(int x=Mathf.Max(0,Mathf.CeilToInt(b.xMin-.5f));x<=Mathf.Min(shot.width-1,Mathf.FloorToInt(b.xMax-.5f));x++)
                        {
                            var on=drawn[y*shot.width+x];var off=blank[y*shot.width+x];
                            int change=Math.Abs(on.r-off.r)+Math.Abs(on.g-off.g)+Math.Abs(on.b-off.b);
                            strongest=Mathf.Max(strongest,change);
                            if(change<3)continue;
                            if(primaryText)
                            {
                                float observed=GameApp.Contrast11(on,off);
                                bestStroke=Mathf.Max(bestStroke,observed);
                                // The edge of an anti-aliased glyph is blended by design. Full-strength
                                // ink is independently attributable when all three rendered channels
                                // equal the TMP mesh colour and the pixel changes on hiding this label.
                                if(on.r==sample.colour.r&&on.g==sample.colour.g&&on.b==sample.colour.b)
                                {opaqueStrokes++;if(!PrimaryPick24(observed,off,adjusted))opaqueMisses++;}
                            }
                            var point=new Vector3(x+.5f,y+.5f);
                            if(!boundary.label.Contains(point))outsideOwn=true;
                            if(boundary.paper!=null)
                            {
                                bool onPaper=false;
                                foreach(var tile in boundary.paper)if(tile.Contains(point)){onPaper=true;break;}
                                if(!onPaper)outsideFrame=true;
                                foreach(var ring in boundary.rings)if(!ring.Contains(point))outsideFrame=true;
                            }
                        }
                        if(outsideOwn)failures.Add(id+" '"+sample.character+"' has painted pixels outside its label");
                        if(outsideFrame)failures.Add(id+" '"+sample.character+"' has painted pixels outside "+boundary.surface);
                        if(strongest<3)failures.Add(id+" '"+sample.character+"' has no visible ink at "+b);
                        int cx=Mathf.Clamp(Mathf.RoundToInt(b.center.x),0,shot.width-1),cy=Mathf.Clamp(Mathf.RoundToInt(b.center.y),0,shot.height-1);
                        Color bg=blank[cy*shot.width+cx];
                        float inkContrast=GameApp.Contrast11(sample.colour,bg);
                        lowest=Mathf.Min(lowest,inkContrast);
                        if(primaryText&&!PrimaryPick24(inkContrast,bg,adjusted))
                            failures.Add(id+" '"+sample.character+"' misses the primary ink pick on the actual surface ("+inkContrast.ToString("F3")+", W11b adjustment="+adjusted+")");
                    }
                    string row=scope+" "+W11bPath(label.transform)+" "+pair.Value.Count+" glyphs, min contrast "+lowest.ToString("F2");
                    report.Add(row);if((control==null||control.IsInteractable())&&lowest<GameApp.MinContrast11)failures.Add(row);
                    if(primaryText)
                    {
                        string stroke=id+" primary ink, strongest drawn contrast="+bestStroke.ToString("F3")+
                            ", full-strength pixels="+opaqueStrokes+", misses="+opaqueMisses;
                        report.Add(stroke);if(bestStroke<GameApp.MinContrast11||opaqueMisses!=0)failures.Add(stroke);
                    }
                }
            }
            finally
            {
                if(arrow!=null)arrow.enabled=arrowWasEnabled;
                foreach(var label in measured.Keys)if(label!=null)label.enabled=true;
                UnityEngine.Object.Destroy(shot);
            }
        }
    }
}
