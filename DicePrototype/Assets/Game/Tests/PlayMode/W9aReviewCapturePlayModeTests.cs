using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1.1 task W9a item 5 (docs/specs/v111/briefs/W9A_UNITY_INTEGRATION.md): review captures of the screens that the W7a
    /// notes (§6), the W7b notes (§2) and W8 (W8-06, D1 and D2) ask to be looked at in Unity and that no other test saves.
    /// Each case drives the real interface to the screen and saves it as the player sees it (debug markers hidden) to
    /// artifacts/cache/v111/w9a/&lt;width&gt;x&lt;height&gt;/; run at 1920x1080 and at 1280x720. A step whose control is not on
    /// screen is written to w9a-steps.txt beside the pictures instead of failing the case: the pictures are judged by a
    /// person (docs/specs/v111/W9A_NOTES.md §5), not by these cases.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private static readonly string[] AllStyles9a={"default","swiss","sancai","comic","deco","glass"};
        private readonly List<string> steps9a=new List<string>();
        private static string W9aOutput
        {
            get
            {
                string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v111","w9a",Screen.width+"x"+Screen.height));
                Directory.CreateDirectory(dir);return dir;
            }
        }
        /// <summary>Saves the screen; with <paramref name="redraw"/> the interface is redrawn first (not for an open dialog).</summary>
        private IEnumerator W9aShot(string name,bool redraw=true)
        {
            bool clean=GameApp.CleanCapture11;GameApp.CleanCapture11=true;
            try
            {
                if(redraw)app.Refresh();
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                var texture=ScreenCapture.CaptureScreenshotAsTexture();
                try{Assert.That(texture.width,Is.EqualTo(Screen.width));File.WriteAllBytes(Path.Combine(W9aOutput,name+".png"),texture.EncodeToPNG());}
                finally{UObj.Destroy(texture);}
                steps9a.Add("shot "+name);
            }
            finally{GameApp.CleanCapture11=clean;}
        }
        private bool TryClick9a(string name)
        {
            var button=app.canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).FirstOrDefault(b=>b.name==name&&b.gameObject.activeInHierarchy&&b.interactable);
            if(button==null){steps9a.Add("not on screen: "+name);return false;}
            button.onClick.Invoke();Canvas.ForceUpdateCanvases();return true;
        }
        private IEnumerator Wait9a(float seconds=.35f)
        {
            float deadline=Time.realtimeSinceStartup+6;
            while(app.IsBusy&&Time.realtimeSinceStartup<deadline)yield return null;
            yield return new WaitForSecondsRealtime(seconds);Canvas.ForceUpdateCanvases();
        }
        private void W9aSteps(string file)=>File.WriteAllLines(Path.Combine(W9aOutput,file),steps9a);
        /// <summary>The shop of ShopBoundaryFixture with two more cards and two more goods (the shelves' maximum), so the
        /// fixture keeps the purchase counters a real shop would have.</summary>
        private RunState W9aShop(string[] cards,string[] goods,string[] owned)
        {
            var fixture=ShopBoundaryFixture();
            for(int i=0;i<cards.Length;i++)AddBoundaryOffer(fixture,"w9a-card-"+cards[i],"controller",cards[i]);
            for(int i=0;i<goods.Length;i++)AddBoundaryOffer(fixture,"w9a-item-"+goods[i],"consumable",goods[i]);
            fixture.controllerOfferPurchases=fixture.shop.offers.Count(o=>o.category=="controller")-app.Config.shop.initialControllerOffers;
            fixture.consumableOfferPurchases=fixture.shop.offers.Count(o=>o.category=="consumable")-app.Config.shop.initialConsumableOffers;
            fixture.controllers.Clear();foreach(string id in owned)fixture.controllers.Add(BoundaryController(fixture,id));
            return fixture;
        }

        /// <summary>W8-06 D1, D2: C154, C155, C156 and I13 in every style, beside the style's other cards: the play row's compact
        /// cards and item bar, the shop shelves and the build tiles, and a card detail.</summary>
        [UnityTest,Timeout(900000)] public IEnumerator PLAY_W9A_NewArtInThePlayRowTheShopAndTheDetailOfEveryStyle()
        {
            steps9a.Clear();
            yield return WithStyles22(()=>Run());
            W9aSteps("w9a-art-steps.txt");

            IEnumerator Run()
            {
                GameApp.CleanCapture11=true;
                StartRun("90901");
                foreach(string theme in AllStyles9a)
                {
                    app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));
                    var scene=W11Scene(new[]{"C154","C155","C156","C24","C08","C46","C50","C55","C45","C41"},new[]{4,4,6,2,4},"bbbrb",2,true,items:0);
                    int serial=app.Session.State.nextItemSerial+scene.controllers.Count;
                    scene.inventory.Add(new OwnedItem{instanceId="ITEM_"+serial.ToString("D5"),contentId="I13",usesRemaining=3});
                    scene.inventory.Add(new OwnedItem{instanceId="ITEM_"+(serial+1).ToString("D5"),contentId="I04"});
                    W11Apply(scene);yield return W11Settle();
                    foreach(string id in new[]{"C154","C155","C156"})Assert.That(HasActive("Icon_"+id),Is.True,theme+": "+id+" is drawn in the play row");
                    yield return W9aShot("art-play-"+theme);
                    if(theme==GameTheme.DefaultId)
                    {
                        // W7b §2: the key ring's detail ("用法", the button).
                        if(TryClick9a("Item_"+app.Session.State.inventory.First(i=>i.contentId=="I13").instanceId)){yield return Wait9a();yield return W9aShot("keyring-detail",false);TryClick9a("CloseDialog");yield return Wait9a(.2f);}
                    }
                }
                var fixture=W9aShop(new[]{"C155","C156"},new[]{"I13","I04"},new[]{"C154","C24","C155","C39","C156"});
                string ring=BoundaryItem(fixture,"I13");fixture.inventory.Single(i=>i.instanceId==ring).usesRemaining=3;
                yield return W14Load(fixture);
                foreach(string theme in AllStyles9a)
                {
                    app.ApplyTheme11(theme);yield return Art073Settle(.3f);
                    yield return W9aShot("art-shop-"+theme);
                    yield return InspectOfferCard("w9a-card-C155");yield return Art073Settle(.3f);
                    yield return W9aShot("art-detail-C155-"+theme,false);
                    TryClick9a("CloseDialog");yield return Art073Settle(.2f);
                }
                app.ApplyTheme11(GameTheme.DefaultId);yield return Art073Settle(.3f);
                foreach(string offer in new[]{"w9a-card-C156","w9a-item-I13"})
                {
                    yield return InspectOfferCard(offer);yield return Art073Settle(.3f);
                    yield return W9aShot("art-detail-"+offer.Substring(offer.LastIndexOf('-')+1)+"-default",false);
                    TryClick9a("CloseDialog");yield return Art073Settle(.2f);
                }
            }
        }

        /// <summary>W7a §6 and W7b §2 in the default style: the score chips with the bonus, the score drawer and the complete
        /// breakdown, growth and cultivation cards and their details, one blue die, the boss that halves growth, glass dice
        /// before and after a submission, the handbook pages, the card gallery, the debug panel, the shop with growth cards
        /// and face upgrades (one face, adding the second, two faces, the purchase), and the unlock button. At 1280x720 the
        /// screens the notes name for large text are saved again at 150 %.</summary>
        [UnityTest,Timeout(900000)] public IEnumerator PLAY_W9A_ScoreCardShopAndUnlockScreensOfP111()
        {
            steps9a.Clear();GameApp.CleanCapture11=true;
            try
            {
                StartRun("90902");
                foreach(int font in Screen.width==1280?new[]{100,150}:new[]{100})
                {
                    W11TextScale(font);string tag=font==100?"":"-text150";
                    // Bonus cards (growth, decay, star, comeback), a multiplying growth card and the cultivation card; five blue dice locked.
                    var scene=W11Scene(new[]{"C56","C141","C137","C156","C120","C54"},new[]{4,4,4,4,4},"bbbbb",5,true,target:"40000");
                    scene.controllers[0].state.growth=120;scene.controllers[5].state.growth=20;
                    W11Apply(scene);yield return W11Settle();
                    yield return W9aShot("play-bonus-preview"+tag);
                    if(TryClick9a("Factor0"))
                    {
                        yield return Wait9a(.7f);yield return W9aShot("score-drawer"+tag,false);
                        if(TryClick9a("BonusToggle111")){yield return Wait9a(.7f);yield return W9aShot("score-drawer-bonus-open"+tag,false);}
                        if(TryClick9a("FullScoreDetails")){yield return Wait9a(.4f);yield return W9aShot("score-breakdown"+tag,false);TryClick9a("ScoreDrawerBack");yield return Wait9a(.3f);}
                        TryClick9a("CloseDialog");yield return Wait9a(.3f);
                    }
                    if(TryClick9a("Controller_"+scene.controllers[0].instanceId)){yield return Wait9a();yield return W9aShot("detail-growth-C56"+tag,false);TryClick9a("CloseDialog");yield return Wait9a(.2f);}
                    if(TryClick9a("Controller_"+scene.controllers[4].instanceId)){yield return Wait9a();yield return W9aShot("detail-cultivation-C120"+tag,false);TryClick9a("CloseDialog");yield return Wait9a(.2f);}
                    // Glass: three locked dice, two of them glass; then the submission and what the summary says.
                    var glass=W11Scene(new string[0],new[]{4,4,4,2,6},"bbbbr",3,true);foreach(var die in glass.dice.Take(2))die.isGlass=true;
                    W11Apply(glass);yield return W11Settle();yield return W9aShot("play-glass-preview"+tag);
                    if(TryClick9a("Submit")){yield return Wait9a(.2f);app.SkipVisual();yield return Wait9a(.4f);yield return W9aShot("play-glass-after"+tag);}
                    // The unlock button with a key ring (W7b §2, longest at 150 %).
                    W11Apply(W7bUnlockScene(new string[0],3,true));yield return W11Settle();
                    ClickDie(app.Session.State.hand.orderedLockedIds.First());yield return Wait9a(.2f);
                    yield return W9aShot("unlock-by-key-ring"+tag);
                    if(font!=100)continue;
                    // One blue die: the bonus chip x1.25.
                    W11Apply(W11Scene(new string[0],new[]{4},"b",1,true));yield return W11Settle();yield return W9aShot("play-single-blue");
                    // The boss that halves growth (B35 返璞归真) and a growth card's detail there.
                    var boss=W11Scene(new[]{"C56","C55","C54"},new[]{4,4,4,4,4},"bbbbb",3,true,stageIndex:8,modifier:"B35");
                    boss.controllers[0].state.growth=120;boss.controllers[1].state.growth=9;boss.controllers[2].state.growth=20;
                    W11Apply(boss);yield return W11Settle();yield return W9aShot("play-boss-B35");
                    if(TryClick9a("Controller_"+boss.controllers[0].instanceId)){yield return Wait9a();yield return W9aShot("detail-growth-halved",false);TryClick9a("CloseDialog");yield return Wait9a(.2f);}
                    // The handbook's colour and rules pages, the card gallery's first page, the debug panel.
                    if(TryClick9a("Handbook"))
                    {
                        yield return Wait9a();
                        if(TryClick9a("HandbookColors")){yield return Wait9a();yield return W9aShot("handbook-colors",false);}
                        if(TryClick9a("HandbookRules09")){yield return Wait9a();yield return W9aShot("handbook-rules",false);}
                        TryClick9a("CloseDialog");yield return Wait9a(.2f);
                    }
                    app.ShowCardGallery13(0);yield return Wait9a();yield return W9aShot("gallery-p0",false);app.HideCardGallery13();yield return Wait9a(.2f);
                    app.ShowDebugPanel();yield return Wait9a();yield return W9aShot("debug-panel",false);TryClick9a("CloseDialog");yield return Wait9a(.2f);
                }
                // The shop: growth cards on the shelf, the cultivation card in the build, the header with the custom upgrade, and
                // gilding (one face, adding a second, two faces, the purchase) and embrittling (the whole die).
                var shop=W9aShop(new[]{"C56","C137"},new[]{"I13","I04"},new[]{"C120","C56","C24"});
                AddBoundaryOffer(shop,"w9a-u06","die_upgrade","U06");AddBoundaryOffer(shop,"w9a-u05","die_upgrade","U05");
                string die=shop.dice.OrderBy(d=>d.createdSerial).First().dieId;
                yield return W14Load(shop);
                foreach(int font in Screen.width==1280?new[]{100,150}:new[]{100})
                {
                    W11TextScale(font);yield return Art073Settle(.3f);string tag=font==100?"":"-text150";
                    yield return W9aShot("shop"+tag);
                    yield return OpenBoundaryOffer("w9a-u06");
                    if(TryClick9a("Target_"+die)&&TryClick9a("Face_F01"))
                    {
                        yield return Wait9a(.2f);yield return W9aShot("facepick-gild-one"+tag,false);
                        if(TryClick9a("AddFace14")){yield return Wait9a(.2f);yield return W9aShot("facepick-gild-adding"+tag,false);}
                        if(TryClick9a("Face_F02")){yield return Wait9a(.2f);yield return W9aShot("facepick-gild-two"+tag,false);}
                        if(font==100&&TryClick9a("ConfirmPurchase")){yield return new WaitForSecondsRealtime(.45f);yield return W9aShot("upgrade-two-face-transaction",false);app.SkipVisual();yield return Wait9a(.3f);}
                    }
                    TryClick9a("CloseDialog");yield return Wait9a(.2f);
                    yield return OpenBoundaryOffer("w9a-u05");
                    if(TryClick9a("Target_"+die)){yield return Wait9a(.2f);yield return W9aShot("facepick-glass"+tag,false);}
                    TryClick9a("CloseDialog");yield return Wait9a(.2f);
                }
            }
            finally{W11TextScale(100);GameApp.CleanCapture11=false;W9aSteps("w9a-screens-steps.txt");}
        }
    }
}
