using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1 task W20X item 5 (W20X_REPORT): pixel identity. The same walk of screens is captured before and after the
    /// theme-interface change; the styles that set none of the new keys (default, swiss, sancai) must come out pixel for
    /// pixel the same, with every remaining difference explained by the noise of two captures of the unchanged code.
    /// The comic style (which sets them) is captured too, to show the change.
    ///
    /// Reduced motion is on for the whole walk (the felt's light sweep, the card sway and the idle floats stop) and the
    /// debug markers are hidden (CleanCapture11), so a capture depends only on the state that is drawn. Screens: the menu and
    /// its settings (画面 tab), stage select, three play states, card details and the dice-style page and the handbook over
    /// the play screen, the stage settlement, the shop that follows it, the run end, the common and full shops with the
    /// offer details and the chapter view, the face pick S1 to S3, and two card-gallery pages.
    ///
    /// Captures go to artifacts/cache/v11/w20x/&lt;width&gt;x&lt;height&gt;/captures/&lt;label&gt;/, label from the environment
    /// variable DICECRAFT_W20X_CAPTURE_LABEL (default: suite-&lt;UTC time&gt;), with a manifest.json.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        public static readonly string[] IdentityThemes20X={"default","swiss","sancai","comic"};

        [Serializable] private sealed class IdentityManifest20X { public string label,utc,head; public int width,height; public string[] files; }

        private string identityDir20X;
        private readonly List<string> identityFiles20X=new List<string>();

        private IEnumerator IdentityShot20X(string theme,string screen)
        {
            Canvas.ForceUpdateCanvases();
            yield return new WaitForEndOfFrame();
            var texture=ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Assert.That(texture.width,Is.EqualTo(Screen.width));
                string file=theme+"-"+screen+".png";
                File.WriteAllBytes(Path.Combine(identityDir20X,file),texture.EncodeToPNG());identityFiles20X.Add(file);
            }
            finally{UObj.Destroy(texture);}
        }
        /// <summary>Applies a style and lets the redraw, the panel motions and any shop entrance finish.</summary>
        private IEnumerator IdentityTheme20X(string theme)
        {
            app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));
            yield return Art073Settle(.45f);
        }
        private void IdentityClose20X(){if(HasActive("CloseDialog"))Click("CloseDialog");Canvas.ForceUpdateCanvases();}
        private static string IdentityHead20X()
        {
            try
            {
                string git=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..",".git"));
                string head=File.ReadAllText(Path.Combine(git,"HEAD")).Trim();
                if(!head.StartsWith("ref: ",StringComparison.Ordinal))return head;
                string reference=head.Substring(5).Trim(),loose=Path.Combine(git,reference.Replace('/',Path.DirectorySeparatorChar));
                if(File.Exists(loose))return File.ReadAllText(loose).Trim();
                string packed=Path.Combine(git,"packed-refs");
                if(File.Exists(packed))foreach(string line in File.ReadAllLines(packed))if(line.EndsWith(" "+reference,StringComparison.Ordinal))return line.Split(' ')[0];
                return reference;
            }
            catch(Exception e){return "unknown ("+e.Message+")";}
        }
        /// <summary>A hand that reaches its target at once: four locked blue fours against a target of 1.</summary>
        private DebugScene IdentityWinningHand20X(int stageIndex)=>W11Scene(new[]{"C39","C139","C34","C08","C46"},new[]{4,4,4,4,2},"bbbbr",4,true,stageIndex:stageIndex,target:"1",score:"0");
        private IEnumerator IdentityEndStage20X(int stageIndex)
        {
            W11Apply(IdentityWinningHand20X(stageIndex));yield return W11Settle();
            var submit=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(submit.accepted,Is.True,submit.error);
            app.SkipVisual();yield return W11Settle();
            if(app.Session.State.phase!="StageSettlement")
            {
                var end=app.Send(new SessionCommand{kind=CommandKind.EndStage});Assert.That(end.accepted,Is.True,end.error);
                app.SkipVisual();
            }
            yield return W11Settle();
            Assert.That(app.Session.State.phase,Is.EqualTo("StageSettlement"));
        }

        [UnityTest] public IEnumerator PLAY_W20X_05_IdentityCapturesOfEveryScreenInFourStyles()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Capture at actual 1920x1080 or 1280x720.");
            foreach(string theme in IdentityThemes20X)Assert.That(ThemeRegistry.Find(theme),Is.Not.Null,theme);
            string label=Environment.GetEnvironmentVariable("DICECRAFT_W20X_CAPTURE_LABEL");
            if(string.IsNullOrWhiteSpace(label))label="suite-"+DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            identityDir20X=W20XOutput(Path.Combine("captures",label));identityFiles20X.Clear();
            foreach(string old in Directory.GetFiles(identityDir20X,"*.png"))File.Delete(old);
            bool hadPreview=PlayerPrefs.HasKey(ThemeRegistry.PreviewKey);int previewValue=PlayerPrefs.GetInt(ThemeRegistry.PreviewKey,0);
            string savedTheme=PlayerPrefs.GetString(ThemeRegistry.PreferenceKey,GameTheme.DefaultId);
            try
            {
                PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,1);PlayerPrefs.SetInt("reduced",1);GameApp.CleanCapture11=true;
                yield return LoadBootstrap();
                Assert.That(UiKit.ReducedMotion,Is.True,"The walk runs with reduced motion.");
                // The menu and its settings page (画面 tab with the 画面风格 row).
                foreach(string theme in IdentityThemes20X)
                {
                    yield return IdentityTheme20X(theme);yield return IdentityShot20X(theme,"01-menu");
                    Click("MenuSettings");Click("SettingsTab1");yield return Art073Settle(.3f);yield return IdentityShot20X(theme,"02-settings-picture");
                    IdentityClose20X();
                }
                // Stage select.
                StartRun("72005");Assert.That(app.Session.State.phase,Is.EqualTo("StagePreview"));
                foreach(string theme in IdentityThemes20X){yield return IdentityTheme20X(theme);yield return IdentityShot20X(theme,"03-select");}
                // Play: selecting (two locked, one chosen), a boss stage before the first throw, ten dice and seven cards.
                W11Apply(W11Scene(W11Cards3,new[]{4,4,6,2,4},"bbbrb",2,true));app.Session.TogglePending(app.Session.State.dice[4].dieId);app.Refresh();yield return W11Settle();
                foreach(string theme in IdentityThemes20X){yield return IdentityTheme20X(theme);yield return IdentityShot20X(theme,"04-play-selecting");}
                W11Apply(W11Scene(new[]{"C39","C139","C34","C56","C08"},new[]{1,2,3,4,5},"bbrbb",0,false,stageIndex:8,modifier:"B02",target:"13500",score:"0",items:2));yield return W11Settle();
                foreach(string theme in IdentityThemes20X){yield return IdentityTheme20X(theme);yield return IdentityShot20X(theme,"05-play-boss");}
                W11Apply(W11Scene(W11Cards7,new[]{6,6,5,3,1,4,2,6,5,1},"bbrbrbbrbb",3,true,items:3,capacity:8));yield return W11Settle();
                string firstCard=app.Session.State.controllers[0].instanceId;
                foreach(string theme in IdentityThemes20X)
                {
                    yield return IdentityTheme20X(theme);yield return IdentityShot20X(theme,"06-play-ten-dice");
                    // Over the play screen: a card's details, the dice-style page, the handbook.
                    Click("Controller_"+firstCard);yield return Art073Settle(.3f);yield return IdentityShot20X(theme,"07-play-card-details");IdentityClose20X();
                    app.ShowDiceStyleLegend073();yield return Art073Settle(.3f);yield return IdentityShot20X(theme,"08-dice-styles");IdentityClose20X();
                    app.ShowHandbook();yield return Art073Settle(.3f);yield return IdentityShot20X(theme,"09-handbook");IdentityClose20X();
                }
                // The stage settlement, then the shop it leads to; the run end after the last stage.
                foreach(string theme in IdentityThemes20X)
                {
                    app.ApplyTheme11(theme);
                    yield return IdentityEndStage20X(3);yield return Art073Settle(.45f);yield return IdentityShot20X(theme,"10-settlement");
                    Click("SettlementContinue");yield return WaitForIdle();yield return Art073Settle(.45f);
                    Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));yield return IdentityShot20X(theme,"11-shop-after-settlement");
                    yield return IdentityEndStage20X(app.Config.stages.Length-1);
                    Click("SettlementContinue");yield return WaitForIdle();yield return Art073Settle(.6f);
                    Assert.That(app.Session.State.phase,Is.EqualTo("RunWon"));
                    // 用时 shows the time played, which depends on how long the walk took: a fixed value keeps the capture
                    // a function of the drawn state (the clock stops at the run end).
                    typeof(GameApp).GetField("runSeconds07",BindingFlags.NonPublic|BindingFlags.Instance).SetValue(app,754f);
                    app.Refresh();yield return Art073Settle(.3f);yield return IdentityShot20X(theme,"12-run-won");
                }
                // The common shop with an offer's details and the chapter view; the full shop.
                yield return W14Load(W14ShopFixture(false));
                string offer=app.Session.State.shop.offers.First(o=>o.category=="controller").offerId;
                foreach(string theme in IdentityThemes20X)
                {
                    yield return IdentityTheme20X(theme);yield return IdentityShot20X(theme,"13-shop-common");
                    yield return InspectOfferCard(offer);yield return Art073Settle(.3f);yield return IdentityShot20X(theme,"14-shop-offer-details");IdentityClose20X();
                    Click("ViewCycle");yield return Art073Settle(.3f);yield return IdentityShot20X(theme,"15-shop-chapter");IdentityClose20X();
                }
                yield return W14Load(W14ShopFixture(true));
                foreach(string theme in IdentityThemes20X){yield return IdentityTheme20X(theme);yield return IdentityShot20X(theme,"16-shop-full");}
                // The face pick on the table: S1 (choose a die), S2 (choose a face), S3 (the change).
                var pick=ShopBoundaryFixture();pick.shop.offers.Clear();AddBoundaryOffer(pick,"pick-gold","die_upgrade","U06");
                yield return W14Load(pick);
                string die=app.Session.State.dice.OrderBy(d=>d.createdSerial).First().dieId;
                foreach(string theme in IdentityThemes20X)
                {
                    yield return IdentityTheme20X(theme);
                    Click("Buy_pick-gold");yield return Art073Settle(.3f);yield return IdentityShot20X(theme,"17-facepick-s1");
                    Click("Target_"+die);yield return Art073Settle(.3f);yield return IdentityShot20X(theme,"18-facepick-s2");
                    Click("Face_F02");yield return Art073Settle(.3f);yield return IdentityShot20X(theme,"19-facepick-s3");
                    Click("CancelFacePick14");yield return null;
                }
                // Two pages of the card gallery (drawn over the whole screen).
                foreach(string theme in IdentityThemes20X)
                {
                    app.ApplyTheme11(theme);
                    app.ShowCardGallery13(0);yield return Art073Settle(.3f);yield return IdentityShot20X(theme,"20-gallery-p0");
                    app.ShowCardGallery13(6);yield return Art073Settle(.3f);yield return IdentityShot20X(theme,"21-gallery-p6");
                    app.HideCardGallery13();
                }
            }
            finally
            {
                app.HideCardGallery13();GameApp.CleanCapture11=false;app.ApplyTheme11(GameTheme.DefaultId);
                if(hadPreview)PlayerPrefs.SetInt(ThemeRegistry.PreviewKey,previewValue);else PlayerPrefs.DeleteKey(ThemeRegistry.PreviewKey);
                PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,savedTheme);PlayerPrefs.Save();
                var manifest=new IdentityManifest20X{label=label,utc=DateTime.UtcNow.ToString("O"),head=IdentityHead20X(),width=Screen.width,height=Screen.height,files=identityFiles20X.ToArray()};
                File.WriteAllText(Path.Combine(identityDir20X,"manifest.json"),JsonUtility.ToJson(manifest,true));
            }
            Assert.That(identityFiles20X.Count,Is.EqualTo(21*IdentityThemes20X.Length),"Every screen in every style.");
            TestContext.WriteLine("W20X-05 identity captures: "+identityFiles20X.Count+" in "+identityDir20X);
        }
    }
}
