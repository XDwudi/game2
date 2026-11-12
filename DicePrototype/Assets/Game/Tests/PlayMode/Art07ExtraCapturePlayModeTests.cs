using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [Serializable] private sealed class ExtraSource07 { public string path,sha256; }
        [Serializable] private sealed class ExtraControl07
        {public string path,text;public Rect bounds,visibleBounds;public bool interactable,hit40Physical,centerRaycastHits,textOverflow;}
        [Serializable] private sealed class ExtraScreen07
        {
            public string id,screen,scope,png,pngSha256,stateHashBefore,stateHashAfter,saveShaBefore,saveShaAfter,pendingShaBefore,pendingShaAfter;
            public string phase,savePath,mode;public int width,height,frame;public float fontScale,canvasScale;
            public long sequence,gold;public bool pendingSave,unchanged;public double nonBlackRatio;public ExtraControl07[] controls;
        }
        [Serializable] private sealed class ExtraAction07
        {public string name,stateBefore,stateAfter,saveBefore,saveAfter;public bool mutationAllowed,pendingAllowed;public long beforeSequence,afterSequence;}
        [Serializable] private sealed class ExtraReport07
        {
            public string schema="art07-extra-capture-1",status="in_progress",startedUtc,endedUtc,output,unity,platform,os,gpu,graphicsApi;
            public string artVersion,rulesVersion,configSha256,runtimeMvid,coreMvid,testMvid,runtimeAssemblySha256,sourceManifestSha256,buildIdentity;
            public string sourceBinding="Stable working-tree source SHA snapshot plus loaded assembly MVID; release commit acceptance remains pending";
            public string acceptance="Actual capture/action evidence only; pending independent visual review, not acceptance";
            public List<ExtraScreen07> screens=new List<ExtraScreen07>();public List<ExtraAction07> actions=new List<ExtraAction07>();
        }
        private ExtraReport07 extra07;
        private ExtraSource07[] extraSources07;
        private static string ExtraHash07(byte[] data){using(var sha=SHA256.Create())return string.Concat(sha.ComputeHash(data).Select(x=>x.ToString("x2")));}
        private static string ExtraFileHash07(string path)=>File.Exists(path)?ExtraHash07(File.ReadAllBytes(path)):"missing";
        private string ExtraStateHash07()=>app.Session.State==null?"no-run":GameSession.StateHash(app.Session.State);
        private void ExtraWrite07()=>File.WriteAllText(Path.Combine(extra07.output,"extra-report.json"),JsonUtility.ToJson(extra07,true));
        private ExtraSource07[] ExtraSourceSnapshot07()
        {
            string game=Path.Combine(Application.dataPath,"Game");
            return Directory.GetFiles(game,"*.cs",SearchOption.AllDirectories).Where(p=>!p.Replace('\\','/').Split('/').Any(part=>part.EndsWith("~",StringComparison.Ordinal)))
                .Concat(new[]{Path.Combine(game,"Data/P0Config.json")}).OrderBy(p=>p,StringComparer.Ordinal)
                .Select(p=>new ExtraSource07{path=p,sha256=ExtraFileHash07(p)}).ToArray();
        }
        private void ExtraAssertSourceFreeze07()
        {
            var now=ExtraSourceSnapshot07();Assert.That(now.Length,Is.EqualTo(extraSources07.Length),"Source set changed during capture.");
            for(int i=0;i<now.Length;i++){Assert.That(now[i].path,Is.EqualTo(extraSources07[i].path));Assert.That(now[i].sha256,Is.EqualTo(extraSources07[i].sha256),now[i].path);}
            Assert.That(typeof(GameApp).Module.ModuleVersionId.ToString(),Is.EqualTo(extra07.runtimeMvid));
        }
        private IEnumerator ExtraBegin07()
        {
            string output=Path.Combine(saveDirectory,"extra-capture");Directory.CreateDirectory(output);
            extraSources07=ExtraSourceSnapshot07();string manifest=string.Join("\n",extraSources07.Select(s=>s.path+" "+s.sha256));
            File.WriteAllText(Path.Combine(output,"source-snapshot.sha256"),manifest,new UTF8Encoding(false));
            extra07=new ExtraReport07{startedUtc=DateTime.UtcNow.ToString("O"),output=output,unity=Application.unityVersion,platform=Application.platform.ToString(),os=SystemInfo.operatingSystem,
                gpu=SystemInfo.graphicsDeviceName,graphicsApi=SystemInfo.graphicsDeviceType.ToString(),artVersion=GameApp.ArtVersion,rulesVersion=app.Config.configVersion,
                configSha256=ExtraHash07(Encoding.UTF8.GetBytes(app.configAsset.text)),runtimeMvid=typeof(GameApp).Module.ModuleVersionId.ToString(),coreMvid=typeof(GameSession).Module.ModuleVersionId.ToString(),testMvid=GetType().Module.ModuleVersionId.ToString(),
                runtimeAssemblySha256=ExtraFileHash07(typeof(GameApp).Assembly.Location),sourceManifestSha256=ExtraHash07(Encoding.UTF8.GetBytes(manifest)),buildIdentity=Resources.Load<TextAsset>("BuildIdentity")?.text};
            ExtraWrite07();int wanted=100;string configured=Environment.GetEnvironmentVariable("DICE_ART07_EXTRA_FONT_PERCENT");
            if(!string.IsNullOrEmpty(configured))Assert.That(int.TryParse(configured,out wanted)&&new[]{100,125,150}.Contains(wanted),Is.True,"Explicit font request must be 100, 125 or 150.");
            yield return ExtraPress07("MenuSettings");yield return ExtraPress07("SettingsTab2");
            for(int i=0;i<3&&Mathf.RoundToInt(UiKit.FontScale*100)!=wanted;i++)yield return ExtraPress07("TextScale");
            Assert.That(Mathf.RoundToInt(UiKit.FontScale*100),Is.EqualTo(wanted));yield return ExtraPress07("SettingsDone");
        }
        private IEnumerator ExtraIdle07(bool pendingAllowed=false)
        {
            // UI handlers change modal state synchronously; GameApp.Update applies it to DiceTable.
            yield return null;
            double deadline=Time.realtimeSinceStartupAsDouble+8;
            while((app.IsBusy||app.PresentationPlaying07||app.table.IsTransitioning&&!app.table.Paused)&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
            Assert.That(app.IsBusy||app.PresentationPlaying07||app.table.IsTransitioning&&!app.table.Paused,Is.False,
                $"Idle timeout: busy={app.IsBusy}, presentation={app.PresentationPlaying07}, layout={app.table.IsTransitioning}, paused={app.table.Paused}");
            if(!pendingAllowed)Assert.That(app.Session.HasPendingSave,Is.False,"Unexpected persistence failure; no fallback screenshot.");
            // A settings modal intentionally freezes a layout started by changing font scale.
            // Verify that it stays frozen; waiting for completion here would deadlock until Close.
            bool pausedLayout=app.table.IsTransitioning&&app.table.Paused;
            var transforms=pausedLayout?app.table.GetComponentsInChildren<Transform>(false):Array.Empty<Transform>();
            var positions=transforms.Select(t=>t.localPosition).ToArray();var rotations=transforms.Select(t=>t.localRotation).ToArray();
            yield return new WaitForSecondsRealtime(.24f);Canvas.ForceUpdateCanvases();
            if(pausedLayout)
            {
                Assert.That(app.table.Paused,Is.True,"The modal must keep layout frozen until it closes.");
                for(int i=0;i<transforms.Length;i++){Assert.That(transforms[i],Is.Not.Null);Assert.That(transforms[i].localPosition,Is.EqualTo(positions[i]));Assert.That(transforms[i].localRotation,Is.EqualTo(rotations[i]));}
            }
        }
        private Rect ExtraBounds07(RectTransform rect)
        {
            var corners=new Vector3[4];rect.GetWorldCorners(corners);var points=corners.Select(p=>RectTransformUtility.WorldToScreenPoint(null,p)).ToArray();
            return Rect.MinMaxRect(points.Min(p=>p.x),points.Min(p=>p.y),points.Max(p=>p.x),points.Max(p=>p.y));
        }
        private bool ExtraHit07(GameObject target,Vector2 position)
        {
            var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);
            return hits.Count>0&&(hits[0].gameObject==target||hits[0].gameObject.transform.IsChildOf(target.transform));
        }
        private IEnumerator ExtraReveal07(RectTransform rect)
        {
            foreach(var scroll in rect.GetComponentsInParent<ScrollRect>(false))
            {
                Canvas.ForceUpdateCanvases();Rect v=ExtraBounds07(scroll.viewport),t=ExtraBounds07(rect),c=ExtraBounds07(scroll.content);
                if(scroll.vertical&&c.height>v.height&&(t.yMin<v.yMin||t.yMax>v.yMax))scroll.verticalNormalizedPosition=Mathf.Clamp01(scroll.verticalNormalizedPosition+(t.center.y-v.center.y)/(c.height-v.height));
                yield return null;
            }
            Canvas.ForceUpdateCanvases();yield return null;
        }
        private IEnumerator ExtraPress07(string name,bool mutation=false,bool pendingAllowed=false)
        {
            yield return ExtraIdle07(pendingAllowed);var button=Button(name);yield return ExtraReveal07((RectTransform)button.transform);button=Button(name);
            Rect bounds=ExtraBounds07((RectTransform)button.transform);Assert.That(button.IsInteractable(),Is.True,name);Assert.That(bounds.width,Is.GreaterThanOrEqualTo(39.99f),name);Assert.That(bounds.height,Is.GreaterThanOrEqualTo(39.99f),name);
            Assert.That(ExtraHit07(button.gameObject,bounds.center),Is.True,"Covered UI target "+name);
            var row=new ExtraAction07{name=name,mutationAllowed=mutation,pendingAllowed=pendingAllowed,stateBefore=ExtraStateHash07(),saveBefore=ExtraFileHash07(app.SaveLocation),beforeSequence=app.Session.ActionSequence};
            button.onClick.Invoke();yield return ExtraIdle07(pendingAllowed);row.stateAfter=ExtraStateHash07();row.saveAfter=ExtraFileHash07(app.SaveLocation);row.afterSequence=app.Session.ActionSequence;extra07.actions.Add(row);ExtraWrite07();
            if(!mutation){Assert.That(row.stateAfter,Is.EqualTo(row.stateBefore),name);Assert.That(row.saveAfter,Is.EqualTo(row.saveBefore),name);}
        }
        private static Rect ExtraIntersect07(Rect a,Rect b){float x=Math.Max(a.xMin,b.xMin),y=Math.Max(a.yMin,b.yMin);return Rect.MinMaxRect(x,y,Math.Max(x,Math.Min(a.xMax,b.xMax)),Math.Max(y,Math.Min(a.yMax,b.yMax)));}
        private ExtraControl07[] ExtraControls07()=>app.canvas.GetComponentsInChildren<Selectable>(false).Select(c=>
        {
            Rect bounds=ExtraBounds07((RectTransform)c.transform),visible=ExtraIntersect07(bounds,new Rect(0,0,Screen.width,Screen.height));
            foreach(var mask in c.GetComponentsInParent<RectMask2D>(false))if(mask.isActiveAndEnabled)visible=ExtraIntersect07(visible,ExtraBounds07((RectTransform)mask.transform));
            foreach(var mask in c.GetComponentsInParent<Mask>(false))if(mask.isActiveAndEnabled)visible=ExtraIntersect07(visible,ExtraBounds07((RectTransform)mask.transform));
            var label=c.GetComponentInChildren<TextMeshProUGUI>();if(label!=null)label.ForceMeshUpdate();
            var names=new List<string>();for(var t=c.transform;t!=null;t=t.parent)names.Add(t.name);names.Reverse();
            return new ExtraControl07{path=string.Join("/",names),text=label?.text,bounds=bounds,visibleBounds=visible,interactable=c.IsInteractable(),hit40Physical=visible.width>=39.99f&&visible.height>=39.99f,centerRaycastHits=ExtraHit07(c.gameObject,bounds.center),textOverflow=label!=null&&label.isTextOverflowing};
        }).ToArray();
        private IEnumerator ExtraCapture07(string id,string screen,string scope,bool pendingAllowed=false)
        {
            yield return ExtraIdle07(pendingAllowed);ExtraAssertSourceFreeze07();string state=ExtraStateHash07(),save=ExtraFileHash07(app.SaveLocation),pending=ExtraFileHash07(app.SaveLocation+".tmp");
            yield return new WaitForEndOfFrame();var row=new ExtraScreen07{id=id,screen=screen,scope=scope,png=extra07.screens.Count.ToString("D3")+"-"+id+".png",stateHashBefore=state,saveShaBefore=save,pendingShaBefore=pending,
                phase=app.Session.State?.phase??"MainMenu",savePath=app.SaveLocation,mode=Screen.fullScreenMode.ToString(),width=Screen.width,height=Screen.height,frame=Time.frameCount,fontScale=UiKit.FontScale,canvasScale=app.canvas.scaleFactor,
                sequence=app.Session.ActionSequence,gold=app.Session.State?.gold??0,pendingSave=app.Session.HasPendingSave,controls=ExtraControls07()};
            Texture2D image=null;
            try
            {
                image=ScreenCapture.CaptureScreenshotAsTexture();Assert.That(image,Is.Not.Null);Assert.That(image.width,Is.EqualTo(Screen.width));Assert.That(image.height,Is.EqualTo(Screen.height));
                var pixels=image.GetPixels32();int sample=0,lit=0;var colors=new HashSet<int>();
                for(int i=0;i<pixels.Length;i+=Math.Max(1,pixels.Length/16384)){var p=pixels[i];sample++;if(Math.Max(p.r,Math.Max(p.g,p.b))>12)lit++;colors.Add((p.r>>4)<<8|(p.g>>4)<<4|(p.b>>4));}
                row.nonBlackRatio=lit/(double)sample;Assert.That(row.nonBlackRatio,Is.GreaterThanOrEqualTo(.1));Assert.That(colors.Count,Is.GreaterThanOrEqualTo(8));
                byte[] png=image.EncodeToPNG();row.pngSha256=ExtraHash07(png);File.WriteAllBytes(Path.Combine(extra07.output,row.png),png);
                row.stateHashAfter=ExtraStateHash07();row.saveShaAfter=ExtraFileHash07(app.SaveLocation);row.pendingShaAfter=ExtraFileHash07(app.SaveLocation+".tmp");
                row.unchanged=row.stateHashAfter==state&&row.saveShaAfter==save&&row.pendingShaAfter==pending;extra07.screens.Add(row);ExtraWrite07();
                File.WriteAllText(Path.Combine(extra07.output,row.png+".json"),JsonUtility.ToJson(row,true));Assert.That(row.unchanged,Is.True,"Capture cannot advance rules or replace a pending candidate.");
            }
            finally{if(image!=null)UnityEngine.Object.Destroy(image);}
        }
        private void ExtraFinish07(){ExtraAssertSourceFreeze07();extra07.status="captured_pending_visual_review";extra07.endedUtc=DateTime.UtcNow.ToString("O");ExtraWrite07();TestContext.WriteLine("Extra real capture, pending review: "+extra07.output);}
        private IEnumerator ExtraLoadFixture07(RunState fixture,string id)
        {
            File.WriteAllText(Path.Combine(extra07.output,id+"-fixture.json"),JsonUtility.ToJson(fixture,true));yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();yield return ExtraIdle07();
        }

        [UnityTest] public IEnumerator PLAY_ART07_ExtraCapture_S11PurchaseReplacementAndFundSale()
        {
            yield return ExtraBegin07();var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();
            var old=BoundaryController(fixture,"C52");old.state.fundAccrual=18;fixture.controllers.Add(old);
            var seller=BoundaryController(fixture,"C41");seller.state.fundAccrual=6;fixture.controllers.Add(seller);
            AddBoundaryOffer(fixture,"extra-buy","controller","C01");AddBoundaryOffer(fixture,"extra-replace","controller","C52");
            fixture.shop.offers.Single(o=>o.offerId=="extra-replace").controller.enchantmentId="slotless";
            yield return ExtraLoadFixture07(fixture,"transactions");yield return ExtraCapture07("purchase-before","S11","Explicit valid debug shop before immediate ordinary purchase.");
            long seq=app.Session.ActionSequence,gold=app.Session.State.gold,price=app.Session.GetOfferNetPrice(app.Session.State.shop.offers.Single(o=>o.offerId=="extra-buy"));
            yield return ExtraPress07("Buy_extra-buy",true);Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));Assert.That(app.Session.State.gold,Is.EqualTo(gold-price));Assert.That(HasActive("Dialog"),Is.False);
            yield return ExtraCapture07("purchase-after","S11","Immediate purchase committed once; sold stamp and actual equipment.");
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);var offer=app.Session.State.shop.offers.Single(o=>o.offerId=="extra-replace");long net=app.Session.GetOfferNetPrice(offer);Assert.That(net,Is.LessThan(0));
            yield return ExtraPress07("Buy_extra-replace");Assert.That(Button("ConfirmPurchase").GetComponentInChildren<TextMeshProUGUI>().text,Does.StartWith("替换并获得"));
            yield return ExtraCapture07("replacement-net-income","S11","Real old fund sale price and negative net cost; no charge yet.");
            yield return ExtraPress07("CancelReplacement");Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            yield return ExtraPress07("Buy_extra-replace");gold=app.Session.State.gold;seq=app.Session.ActionSequence;yield return ExtraPress07("ConfirmPurchase",true);
            Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));Assert.That(app.Session.State.gold,Is.EqualTo(gold-net));
            var replacement=app.Session.State.controllers.Single(c=>c.definitionId=="C52");Assert.That(replacement.instanceId,Is.EqualTo(offer.controller.instanceId));Assert.That(replacement.state.fundAccrual,Is.Zero);Assert.That(replacement.enchantmentId,Is.EqualTo("slotless"));
            yield return ExtraCapture07("replacement-after","S11","Committed replacement; new identity/enchantment and zero new fund, real wallet credit.");
            yield return ExtraPress07("Controller_"+replacement.instanceId);yield return ExtraCapture07("replacement-new-instance","S15","Read-only details of initialized replacement.");yield return ExtraPress07("CloseDialog");
            yield return ExtraPress07("Controller_"+seller.instanceId);yield return ExtraPress07("SellController");before=StateJson();disk=File.ReadAllBytes(app.SaveLocation);
            yield return ExtraCapture07("fund-sale-confirmation","S11","Sale confirmation shows actual C41 fund and lost enchantment.");
            yield return ExtraPress07("CancelSale");Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            yield return ExtraPress07("SellController");long sale=app.Session.GetControllerSalePrice(seller.instanceId);gold=app.Session.State.gold;seq=app.Session.ActionSequence;
            yield return ExtraPress07("ConfirmSale",true);Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));Assert.That(app.Session.State.gold,Is.EqualTo(gold+sale));Assert.That(app.Session.State.controllers.Any(c=>c.instanceId==seller.instanceId),Is.False);
            yield return ExtraCapture07("fund-sale-after","S11","Actual single sale; wallet and equipment reflect the same receipt.");ExtraFinish07();
        }

        [UnityTest] public IEnumerator PLAY_ART07_ExtraCapture_S11RefreshAndExpandOnlyTheirOwnShelves()
        {
            yield return ExtraBegin07();
            foreach(bool free in new[]{false,true})
            {
                var fixture=ShopBoundaryFixture();fixture.shop.offers.RemoveAll(o=>o.category=="service");fixture.freeFirstRefreshOwned=free;
                AddBoundaryOffer(fixture,"extra-O05","service","O05");AddBoundaryOffer(fixture,"extra-O06","service","O06");
                yield return ExtraLoadFixture07(fixture,free?"refresh-free":"refresh-paid");
                var consumable=app.Session.State.shop.offers.First(o=>o.category=="consumable");yield return ExtraPress07("Buy_"+consumable.offerId,true);
                string prefix=free?"free":"paid";yield return ExtraCapture07(prefix+"-refresh-before","S11","Actual first price and a sold non-controller quote before refresh.");
                for(int i=0;i<2;i++)
                {
                    int cost=free&&i==0?0:i==0?3:5;Assert.That(app.Session.GetRefreshPrice(),Is.EqualTo(cost));var before=app.Session.State;long seq=before.actionSequence;
                    var nonControllers=before.shop.offers.Where(o=>o.category!="controller").Select(JsonUtility.ToJson).ToArray();
                    yield return ExtraPress07("RefreshControllers",true);var after=app.Session.State;
                    Assert.That(after.actionSequence,Is.EqualTo(seq+1));Assert.That(after.gold,Is.EqualTo(before.gold-cost));Assert.That(app.Session.GetRefreshPrice(),Is.EqualTo(i==0?5:7));
                    CollectionAssert.AreEqual(nonControllers,after.shop.offers.Where(o=>o.category!="controller").Select(JsonUtility.ToJson));
                    Assert.That(after.processedReceipts.Last().command.kind,Is.EqualTo(CommandKind.RefreshControllers));
                    yield return ExtraCapture07(prefix+"-refresh-"+(i+1),"S11","Real refresh committed; other categories/sold state byte-identical, next price "+app.Session.GetRefreshPrice()+".");
                }
                if(!free)foreach(string service in new[]{"O05","O06"})
                {
                    var before=app.Session.State;string category=service=="O05"?"controller":"consumable";
                    var prior=before.shop.offers.Where(o=>o.category==category).Select(JsonUtility.ToJson).ToArray();
                    yield return ExtraCapture07("expand-"+service+"-before","S11","Actual service quote before a single capacity purchase.");
                    yield return ExtraPress07("Buy_extra-"+service,true);var after=app.Session.State;Assert.That(after.actionSequence,Is.EqualTo(before.actionSequence+1));
                    var expanded=after.shop.offers.Where(o=>o.category==category).ToArray();Assert.That(expanded.Length,Is.EqualTo(prior.Length+1));CollectionAssert.AreEqual(prior,expanded.Take(prior.Length).Select(JsonUtility.ToJson));
                    Assert.That(service=="O05"?after.controllerOfferPurchases:after.consumableOfferPurchases,Is.EqualTo((service=="O05"?before.controllerOfferPurchases:before.consumableOfferPurchases)+1));
                    yield return ExtraCapture07("expand-"+service+"-after","S11","Exactly one legal new quote; old shelf entries preserved. Empty-candidate branch not covered.");
                }
            }
            ExtraFinish07();
        }

        [UnityTest] public IEnumerator PLAY_ART07_ExtraCapture_S13UpgradeTargetsCancelAndRandomRange()
        {
            yield return ExtraBegin07();var fixture=ShopBoundaryFixture();fixture.shop.offers.Clear();
            foreach(string id in new[]{"U01","U07","U05"})AddBoundaryOffer(fixture,"extra-"+id,"die_upgrade",id);
            yield return ExtraLoadFixture07(fixture,"upgrades");
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            yield return ExtraPress07("Buy_extra-U01");Assert.That(Button("ConfirmPurchase").interactable,Is.False);
            yield return ExtraCapture07("upgrade-no-target","S13","No implicit die/face target; confirm disabled.");
            yield return ExtraPress07("Target_D001");yield return ExtraPress07("Face_F02");Assert.That(Button("ConfirmPurchase").interactable,Is.True);
            yield return ExtraCapture07("upgrade-selected-face","S13","Real permanent/current/effective data and deterministic smooth-face preview.");
            yield return ExtraPress07("CloseDialog");Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            yield return ExtraPress07("Buy_extra-U01");yield return ExtraPress07("Target_D001");yield return ExtraPress07("Face_F02");long seq=app.Session.ActionSequence;
            yield return ExtraPress07("ConfirmPurchase",true);Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));Assert.That(app.Session.State.dice.Single(d=>d.dieId=="D001").faces.Single(f=>f.faceId=="F02").texture,Is.EqualTo("smooth"));
            yield return ExtraCapture07("upgrade-after","S13","Actual purchased smooth face; sold quote retained.");
            before=StateJson();disk=File.ReadAllBytes(app.SaveLocation);yield return ExtraPress07("Buy_extra-U07");yield return ExtraPress07("Target_D001");yield return ExtraPress07("Face_F02");
            Assert.That(FindActive<TextMeshProUGUI>("PurchasePreviewBody").text,Does.Contain("等概率取一个").And.Contain("颜色、纹理、镀金保持"));
            yield return ExtraCapture07("random-upgrade-range","S13","U07 shows only legal range; newly smooth permanent face visible; no RNG draw.");yield return ExtraPurchaseText07("random-upgrade-readable");yield return ExtraPress07("CloseDialog");
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            yield return ExtraPress07("Buy_extra-U05");yield return ExtraPress07("Target_D001");yield return ExtraCapture07("whole-die-upgrade-preview","S13","Whole-die target has no chosen physical face.");yield return ExtraPurchaseText07("glass-risk-readable");
            yield return ExtraPress07("ConfirmPurchase",true);Assert.That(app.Session.State.dice.Single(d=>d.dieId=="D001").isGlass,Is.True);Assert.That(string.IsNullOrEmpty(app.Session.State.processedReceipts.Last().command.targetFaceId),Is.True);
            yield return ExtraPress07("Buy_extra-U07");yield return ExtraPress07("Target_D001");yield return ExtraPress07("Face_F02");yield return ExtraCapture07("glass-with-smooth-face","S13","Actual committed glass body with the prior permanent smooth face.");yield return ExtraPress07("CloseDialog");
            fixture=app.Session.State;fixture.shop.offers.RemoveAll(o=>o.contentId=="U01");AddBoundaryOffer(fixture,"extra-duplicate","die_upgrade","U01");yield return ExtraLoadFixture07(fixture,"duplicate-upgrade");
            yield return ExtraPress07("Buy_extra-duplicate");yield return ExtraPress07("Target_D001");yield return ExtraPress07("Face_F02");
            Assert.That(Button("ConfirmPurchase").interactable,Is.False);yield return ExtraCapture07("upgrade-already-owned","S13","Core rejects duplicate attribute; price/target retained and no transaction.");yield return ExtraPress07("CloseDialog");ExtraFinish07();
        }

        private IEnumerator ExtraPurchaseText07(string label)
        {
            var text=FindActive<TextMeshProUGUI>("PurchasePreviewBody");var scroll=text.GetComponentInParent<ScrollRect>();
            Assert.That(scroll,Is.Not.Null);text.ForceMeshUpdate();
            int expected=text.text.Count(c=>!char.IsWhiteSpace(c));var seen=new HashSet<int>();
            Assert.That(text.textInfo.characterCount,Is.EqualTo(text.text.Length),"All preview characters must exist in TMP layout.");
            float overflow=Math.Max(0,scroll.content.rect.height-scroll.viewport.rect.height);
            int pages=Math.Max(1,Mathf.CeilToInt(overflow/Math.Max(1,scroll.viewport.rect.height*.75f))+1);
            string state=ExtraStateHash07(),save=ExtraFileHash07(app.SaveLocation);
            for(int page=0;page<pages;page++)
            {
                scroll.verticalNormalizedPosition=pages==1?1:1-page/(float)(pages-1);Canvas.ForceUpdateCanvases();yield return null;text.ForceMeshUpdate();
                Rect visible=ExtraBounds07(scroll.viewport);
                for(int i=0;i<text.textInfo.characterCount;i++)
                {
                    var ch=text.textInfo.characterInfo[i];if(char.IsWhiteSpace(ch.character)||!ch.isVisible)continue;
                    var lower=RectTransformUtility.WorldToScreenPoint(null,text.transform.TransformPoint(ch.bottomLeft));
                    var upper=RectTransformUtility.WorldToScreenPoint(null,text.transform.TransformPoint(ch.topRight));
                    if(lower.x>=visible.xMin-.1f&&upper.x<=visible.xMax+.1f&&lower.y>=visible.yMin-.1f&&upper.y<=visible.yMax+.1f)seen.Add(i);
                }
                var pay=Button("ConfirmPurchase");Rect payBounds=ExtraBounds07((RectTransform)pay.transform);
                Assert.That(pay.IsInteractable(),Is.True);Assert.That(ExtraHit07(pay.gameObject,payBounds.center),Is.True);
                yield return ExtraCapture07(label+"-page"+page,"S13","Actual scrolled purchase preview; all glyphs must be fully visible across these frames, payment stays usable.");
            }
            Assert.That(seen.Count,Is.EqualTo(expected),"Some range/risk characters were never fully rendered inside the viewport.");
            Assert.That(ExtraStateHash07(),Is.EqualTo(state));Assert.That(ExtraFileHash07(app.SaveLocation),Is.EqualTo(save));
            scroll.verticalNormalizedPosition=1;Canvas.ForceUpdateCanvases();yield return null;
        }

        [UnityTest] public IEnumerator PLAY_ART07_ExtraCapture_S13RemoteTargetsCancelAndSingleUse()
        {
            yield return ExtraBegin07();var fixture=NewBoundaryFixture("extra-remote");string item=BoundaryItem(fixture,"I03");yield return ExtraLoadFixture07(fixture,"remote");yield return ExtraPress07("StartStage",true);
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);yield return ExtraPress07("Item_"+item);yield return ExtraPress07("Use");
            Assert.That(Button("RemoteConfirm").interactable,Is.False);yield return ExtraCapture07("remote-empty","S13","Actual narrow target drawer; two distinct dice required.");
            yield return ExtraPress07("RemoteDieD001");yield return ExtraPress07("RemoteDieD002");yield return ExtraPress07("RemotePipD001_4");yield return ExtraPress07("RemotePipD002_1");
            Assert.That(Button("RemoteConfirm").interactable,Is.True);yield return ExtraCapture07("remote-selected","S13","Two actual unlocked targets and chosen current-result values; cost/risk visible.");
            yield return ExtraPress07("CloseDialog");Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
            yield return ExtraPress07("Item_"+item);yield return ExtraPress07("Use");yield return ExtraPress07("RemoteDieD001");yield return ExtraPress07("RemoteDieD002");yield return ExtraPress07("RemotePipD001_4");yield return ExtraPress07("RemotePipD002_1");
            int rolls=app.Session.State.hand.rollsRemaining;long seq=app.Session.ActionSequence;yield return ExtraPress07("RemoteConfirm",true);var after=app.Session.State;
            Assert.That(after.actionSequence,Is.EqualTo(seq+1));Assert.That(after.processedReceipts.Last().command.kind,Is.EqualTo(CommandKind.UseItem));Assert.That(after.hand.rollsRemaining,Is.EqualTo(rolls-1));Assert.That(after.inventory.Any(i=>i.instanceId==item),Is.False);
            Assert.That(after.hand.currentResults.Single(r=>r.dieId=="D001").currentPip,Is.EqualTo(4));Assert.That(after.hand.currentResults.Single(r=>r.dieId=="D002").currentPip,Is.EqualTo(1));Assert.That(after.hand.currentResults.All(r=>r.hasRolled),Is.True);
            yield return ExtraCapture07("remote-after-real-roll","S13","Actual one-item/one-roll committed result, no replacement die or synthesized score.");ExtraFinish07();
        }

        [UnityTest] public IEnumerator PLAY_ART07_ExtraCapture_S22RealSaveFaultRetryAbandonAndBackupRecovery()
        {
            yield return ExtraBegin07();StartRun("70822");yield return ExtraPress07("StartStage",true);
            string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long seq=app.Session.ActionSequence;string candidate;
            using(var locked=new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                yield return ExtraPress07("Roll",true,true);Assert.That(app.Session.HasPendingSave,Is.True);Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
                candidate=JsonUtility.ToJson(JsonUtility.FromJson<RunState>(JsonUtility.FromJson<TestSaveEnvelope>(File.ReadAllText(app.SaveLocation+".tmp")).payload));
                yield return ExtraCapture07("save-failure","S22","Real AtomicSaveStore replacement denied by a held file handle; checkpoint unchanged.",true);
                yield return ExtraPress07("ExpandSaveError",false,true);yield return ExtraCapture07("save-error-expanded","S22","Actual adapter error, not fabricated UI text.",true);
                yield return ExtraPress07("RetrySave",true,true);Assert.That(app.Session.HasPendingSave,Is.True);Assert.That(StateJson(),Is.EqualTo(before));
                Assert.That(JsonUtility.ToJson(JsonUtility.FromJson<RunState>(JsonUtility.FromJson<TestSaveEnvelope>(File.ReadAllText(app.SaveLocation+".tmp")).payload)),Is.EqualTo(candidate));
            }
            yield return ExtraPress07("RetrySave",true,true);Assert.That(app.Session.HasPendingSave,Is.False);Assert.That(StateJson(),Is.EqualTo(candidate));Assert.That(app.Session.ActionSequence,Is.EqualTo(seq+1));
            yield return ExtraCapture07("save-retry-committed","S22","Released file lock; the identical frozen candidate committed exactly once.");
            before=StateJson();disk=File.ReadAllBytes(app.SaveLocation);seq=app.Session.ActionSequence;
            using(var locked=new FileStream(app.SaveLocation,FileMode.Open,FileAccess.Read,FileShare.Read))
            {
                yield return ExtraPress07("Roll",true,true);Assert.That(app.Session.HasPendingSave,Is.True);yield return ExtraCapture07("save-before-abandon","S22","Second real write failure, with a new frozen Roll candidate.",true);
                yield return ExtraPress07("AbandonPendingSave",true,true);Assert.That(app.Session.HasPendingSave,Is.False);Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));Assert.That(app.Session.ActionSequence,Is.EqualTo(seq));
            }
            yield return ExtraCapture07("save-abandoned-checkpoint","S22","Actual prior dice and wallet restored together; no reroll and no mixed state.");
            string path=app.SaveLocation;byte[] backup=File.ReadAllBytes(path+".backup");
            var restored=new AtomicSaveStore(path+".backup",app.Config,SaveSlotPolicy.Normal).Load();Assert.That(restored,Is.Not.Null);
            File.WriteAllBytes(Path.Combine(extra07.output,"pre-corruption-active.json"),File.ReadAllBytes(path));File.WriteAllBytes(Path.Combine(extra07.output,"pre-corruption-backup.json"),backup);
            File.WriteAllText(path,"{invalid-json: explicit isolated load-recovery fault}");yield return LoadBootstrap();
            Assert.That(StateJson(),Is.EqualTo(JsonUtility.ToJson(restored)));Assert.That(FindActive<TextMeshProUGUI>("DialogTitle").text,Is.EqualTo("存档恢复说明"));CollectionAssert.AreEqual(backup,File.ReadAllBytes(path+".backup"));
            yield return ExtraCapture07("backup-recovery-dialog","S22","Actual startup restored a validated backup after explicit isolated active-file corruption.");
            yield return ExtraPress07("CloseLoadIssue07");yield return ExtraPress07("ContinueRun");Assert.That(StateJson(),Is.EqualTo(JsonUtility.ToJson(restored)));Assert.That(Button("Roll").interactable,Is.True);
            yield return ExtraCapture07("backup-recovered-board","S22","Stable recovered checkpoint is playable; no historical effect replay.");ExtraFinish07();
        }
    }
}
