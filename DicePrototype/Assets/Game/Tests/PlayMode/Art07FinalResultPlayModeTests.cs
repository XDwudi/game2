using System;
using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private void FinalOptions07(string speed,bool reducedMotion)
        {
            Click("Pause");Click("Settings");Click("SettingsTab2");
            for(int i=0;i<3&&!Button("Speed").GetComponentInChildren<TextMeshProUGUI>().text.StartsWith(speed,StringComparison.Ordinal);i++)Click("Speed");
            Assert.That(Button("Speed").GetComponentInChildren<TextMeshProUGUI>().text,Does.StartWith(speed));
            if(Button("Reduced").GetComponentInChildren<TextMeshProUGUI>().text.StartsWith("开",StringComparison.Ordinal)!=reducedMotion)Click("Reduced");
            Click("SettingsDone");
        }
        private TextMeshProUGUI VisibleFinalText07(string name)=>app.canvas.GetComponentsInChildren<TextMeshProUGUI>(true).Single(t=>t.name==name&&t.gameObject.activeInHierarchy);
        [UnityTest] public IEnumerator PLAY_ART07_FINAL_RealRendererNormalFastReducedInstantRetainCommittedScoreAndSources()
        {
            foreach(string mode in new[]{"Normal","Fast","Reduced","Instant"})
            {
                Art07Fixtures.Apply(app,Art07FixtureKind.G147);FinalOptions07(mode=="Reduced"?"Normal":mode,mode=="Reduced");
                Art07Fixtures.Submit(app,Art07FixtureKind.G147);string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
                if(mode=="Instant")
                {
                    Assert.That(app.PresentationPlaying07,Is.False,"Instant must finish synchronously.");
                    Assert.That(app.table.IsTransitioning,Is.False,"Instant must commit the physical dice pose with the final HUD, without a one-frame stale slot pose.");
                }
                double limit=Time.realtimeSinceStartupAsDouble+6;
                while(app.PresentationPlaying07&&Time.realtimeSinceStartupAsDouble<limit)yield return null;
                Assert.That(app.PresentationPlaying07||app.IsBusy,Is.False);Assert.That(app.StablePresentationResultVisible07,Is.True,mode);
                Assert.That(VisibleFinalText07("StableResultScore07").text.Replace(",",""),Is.EqualTo("967680"));
                Assert.That(VisibleFinalText07("StableResultFactors07").text,Does.Contain("26880").And.Contain("连锁 4"));
                Assert.That(app.StablePresentationSourceMarks07,Is.EqualTo(4));
                app.Refresh();yield return null;
                Assert.That(app.StablePresentationResultVisible07,Is.True);Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
            }
        }
        [UnityTest] public IEnumerator PLAY_ART07_FINAL_SkipKeepsZeroScorePostChangesAndReadOnlyDetails()
        {
            Art07Fixtures.Apply(app,Art07FixtureKind.ZeroMultiplier);Art07Fixtures.Submit(app,Art07FixtureKind.ZeroMultiplier);
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);app.SkipVisual();
            Assert.That(app.StablePresentationResultVisible07,Is.True);Assert.That(VisibleFinalText07("StableResultScore07").text,Is.EqualTo("0"));
            Assert.That(VisibleFinalText07("StableResultFactors07").text,Does.Contain("后置").And.Contain("提交收入").And.Contain("没有有效5点"));
            Click("StablePresentationDetails07");yield return null;Click("CloseDialog");
            Assert.That(app.StablePresentationResultVisible07,Is.True);Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
        }
        [UnityTest] public IEnumerator PLAY_ART07_FINAL_RejectedActionKeepsFinalSuccessfulRollClearsIt()
        {
            Art07Fixtures.Apply(app,Art07FixtureKind.G147);Art07Fixtures.Submit(app,Art07FixtureKind.G147);app.SkipVisual();
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            var rejected=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(rejected.accepted,Is.False);
            Assert.That(app.StablePresentationResultVisible07,Is.True);Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
            var roll=app.Send(new SessionCommand{kind=CommandKind.Roll});Assert.That(roll.accepted,Is.True,roll.error);Assert.That(app.StablePresentationResultVisible07,Is.False);app.SkipVisual();
            Assert.That(app.StablePresentationSourceMarks07,Is.Zero);yield return null;
        }
        [UnityTest] public IEnumerator PLAY_ART07_FINAL_FocusLossSettlesRollAndTransactionWithoutAnotherCommit()
        {
            Art07Fixtures.Apply(app,Art07FixtureKind.G147);Art07Fixtures.Submit(app,Art07FixtureKind.G147);app.SkipVisual();
            Assert.That(app.Send(new SessionCommand{kind=CommandKind.Roll}).accepted,Is.True);Assert.That(app.IsBusy,Is.True);
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            app.SendMessage("OnApplicationFocus",false);Assert.That(app.IsBusy,Is.False);Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
            yield return TransactionShop07(s=>s.shop.offers.Any(o=>o.category=="controller"&&!o.sold));
            Assert.That(app.Send(new SessionCommand{kind=CommandKind.RefreshControllers}).accepted,Is.True);Assert.That(app.TransactionVisualPlaying07,Is.True);
            state=GameSession.StateHash(app.Session.State);save=File.ReadAllBytes(app.SaveLocation);
            app.SendMessage("OnApplicationFocus",false);Assert.That(app.IsBusy||app.TransactionVisualPlaying07,Is.False);Assert.That(app.TransactionSnapshotGraphicCount07,Is.Zero);
            Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));yield return null;
        }
        [UnityTest] public IEnumerator PLAY_ART07_FINAL_EnablingReducedWhileTransactionPausedSettlesFinalUi()
        {
            yield return TransactionShop07(s=>s.shop.offers.Any(o=>o.category=="controller"&&!o.sold));
            Assert.That(app.Send(new SessionCommand{kind=CommandKind.RefreshControllers}).accepted,Is.True);Assert.That(app.TransactionVisualPlaying07,Is.True);
            string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            FinalOptions07("Normal",true);Assert.That(app.IsBusy||app.TransactionVisualPlaying07,Is.False);Assert.That(app.TransactionSnapshotGraphicCount07,Is.Zero);
            foreach(var card in app.canvas.GetComponentsInChildren<RectTransform>(true).Where(r=>r.gameObject.activeInHierarchy&&r.name.StartsWith("Offer_",StringComparison.Ordinal)))
            {Assert.That(card.localRotation,Is.EqualTo(Quaternion.identity));Assert.That(card.localScale,Is.EqualTo(Vector3.one));var group=card.GetComponent<CanvasGroup>();if(group!=null)Assert.That(group.alpha,Is.EqualTo(1));}
            Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));yield return null;
        }
        [UnityTest] public IEnumerator PLAY_ART07_FINAL_ComboKeepsUniformChipsAndEmptyNumberBackground()
        { yield return CheckComboInk07(false); }
        [UnityTest] public IEnumerator PLAY_ART07_FINAL_HighestTenCardsKeepsChipInkOutsideCardBordersAndGlyphs()
        { yield return CheckComboInk07(true); }
        private IEnumerator CheckComboInk07(bool tenCards)
        {
            bool hadQuality=PlayerPrefs.HasKey("art07.quality");int oldQuality=PlayerPrefs.GetInt("art07.quality",1);
            try
            {
            Art07Fixtures.Apply(app,Art07FixtureKind.G147);
            if(tenCards)FxScene07(scene=>
            {
                int first=scene.controllers.Max(c=>c.acquiredSerial)+1;
                string[] added={"C01","C05","C17","C19","C20","C34"};
                for(int i=0;i<added.Length;i++)scene.controllers.Add(new ControllerInstance{instanceId="ITEM_"+(first+i).ToString("D5"),acquiredSerial=first+i,definitionId=added[i],enchantmentId="slotless",state=new ControllerDynamicState()});
            });
            Click("Pause");Click("Settings");Click("SettingsTab1");
            for(int i=0;i<3&&!Button("Quality").GetComponentInChildren<TextMeshProUGUI>().text.StartsWith("标准",StringComparison.Ordinal);i++)Click("Quality");
            Assert.That(Button("Quality").GetComponentInChildren<TextMeshProUGUI>().text,Does.StartWith("标准"));Click("SettingsDone");
            FinalOptions07("Normal",false);
            var before=app.Session.State;var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
            var receipt=PresentationReceiptAdapter.BuildCommitted(result,before,app.Session.State,app.Config);Assert.That(receipt.ComboTier,Is.EqualTo(5));
            if(!tenCards)Assert.That(receipt.ScoreDecimal,Is.EqualTo("967680"));
            string stateHash=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            yield return WaitFxPhase07(PresentationPhase.FinalScore);
            var view=LiveFx07("fx_combo_burst").Single();Assert.That(view.GetComponent<Art07ScoreBurstLayout>().IsConfigured,Is.True);Assert.That(view.transform.localScale,Is.EqualTo(Vector3.one));
            Assert.That(view.burstLayers[1].activeInHierarchy||view.burstLayers[3].activeInHierarchy||view.burstLayers[4].activeInHierarchy,Is.False,"Circular fills must not sit behind the final gold numerals.");
            var scoreRoot=(RectTransform)view.transform.parent;
            var inkCache=new System.Collections.Generic.Dictionary<Sprite,Rect>();
            Rect Ink(UnityEngine.UI.Image image){if(!inkCache.TryGetValue(image.sprite,out var ink)){ink=ReadBurstSpriteInk07(image.sprite);inkCache.Add(image.sprite,ink);}return ink;}
            foreach(float moment in new[]{.05f,.45f,.8f})
            {
                view.Evaluate(moment);
                var chips=view.particles.OfType<RectTransform>().Where(r=>r.gameObject.activeInHierarchy).ToArray();
                Assert.That(chips,Has.Length.EqualTo(36),"G147 keeps the exact L5 particle budget.");
                var forbidden=new System.Collections.Generic.List<Rect>();
                foreach(var text in scoreRoot.GetComponentsInChildren<TMP_Text>(false).Where(t=>!string.IsNullOrEmpty(t.text)))
                {
                    text.ForceMeshUpdate();if(!text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c=>c.isVisible))continue;
                    var b=text.textBounds;forbidden.Add(BurstRectIn07(text.transform,new Rect(b.min.x,b.min.y,b.size.x,b.size.y),scoreRoot));
                }
                foreach(var button in scoreRoot.GetComponentsInChildren<UnityEngine.UI.Button>(false))forbidden.Add(BurstRectIn07(button.transform,((RectTransform)button.transform).rect,scoreRoot));
                var cards=app.canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.name.StartsWith("Controller_",StringComparison.Ordinal)).ToArray();
                Assert.That(cards,Has.Length.EqualTo(tenCards?10:4));
                foreach(var card in cards)
                {
                    forbidden.Add(BurstRectIn07(card.transform,((RectTransform)card.transform).rect,scoreRoot));
                    foreach(var text in card.GetComponentsInChildren<TMP_Text>(false))
                    {
                        text.ForceMeshUpdate();if(!text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(c=>c.isVisible))continue;
                        var b=text.textBounds;forbidden.Add(BurstRectIn07(text.transform,new Rect(b.min.x,b.min.y,b.size.x,b.size.y),scoreRoot));
                    }
                }
                foreach(var chip in chips)
                {
                    var graphic=chip.GetComponent<UnityEngine.UI.Image>();Assert.That(graphic.sprite.name,Does.StartWith("fx_square_chip"),"L5 uses authored square scraps, not the old four star sparks.");
                    var rgb=new Vector3(graphic.color.r,graphic.color.g,graphic.color.b);var red=new Vector3(UiKit.Red.r,UiKit.Red.g,UiKit.Red.b);var blue=new Vector3(UiKit.Blue.r,UiKit.Blue.g,UiKit.Blue.b);
                    Assert.That(Mathf.Min(Vector3.Distance(rgb,red),Vector3.Distance(rgb,blue)),Is.LessThan(.005f),"L5 square scraps use the authored red/blue palette, not gold star tint.");
                    var ink=Ink(graphic);float width=chip.rect.width*ink.width*chip.localScale.x,height=chip.rect.height*ink.height*chip.localScale.y;
                    Assert.That(chip.rect.width,Is.EqualTo(chip.rect.height).Within(.001f));Assert.That(chip.localScale.x,Is.EqualTo(chip.localScale.y).Within(.001f));
                    Assert.That(width,Is.InRange(5f,7f),"Measure imported sprite alpha ink, not its transparent padding.");Assert.That(height,Is.InRange(5f,7f));
                    Assert.That(width*app.canvas.scaleFactor,Is.GreaterThanOrEqualTo(3f),"The square remains visible in the 720p layout.");
                    var visible=BurstRectIn07(chip,new Rect(chip.rect.xMin+ink.x*chip.rect.width,chip.rect.yMin+ink.y*chip.rect.height,ink.width*chip.rect.width,ink.height*chip.rect.height),scoreRoot);
                    Assert.That(forbidden.Any(r=>r.Overlaps(visible)),Is.False,"Chip ink must not overlap any controller card, card glyph, score glyph, or details button. Chip="+visible+" intersections="+string.Join(";",forbidden.Where(r=>r.Overlaps(visible)))+" score="+scoreRoot.rect);
                    Assert.That(visible.yMin,Is.GreaterThan(0f),"All visible ink must stay above the score panel and its inset gold border.");
                }
                var flame=(RectTransform)view.burstLayers[5].transform;var flameInk=Ink(flame.GetComponent<UnityEngine.UI.Image>());
                Assert.That(flame.gameObject.activeInHierarchy,Is.True);Assert.That(flame.rect.height*flameInk.height,Is.InRange(24f,28f),"The actual flame ink keeps a readable height, not a compressed line.");
                var flameVisible=BurstRectIn07(flame,new Rect(flame.rect.xMin+flameInk.x*flame.rect.width,flame.rect.yMin+flameInk.y*flame.rect.height,flameInk.width*flame.rect.width,flameInk.height*flame.rect.height),scoreRoot);
                Assert.That(forbidden.Any(r=>r.Overlaps(flameVisible)),Is.False,"The short flame stays in the lower blank area, away from numerals and the details button.");
                TestContext.WriteLine("Burst alpha-ink geometry t="+moment+": particles="+chips.Length+" firstInkRef="+(chips[0].rect.width*Ink(chips[0].GetComponent<UnityEngine.UI.Image>()).width)+" flameInk="+flameVisible+" canvasScale="+app.canvas.scaleFactor);
            }
            Assert.That(view.transform.GetComponentsInChildren<Transform>(true).Count(t=>t.name.StartsWith("ScoreStepBorder",StringComparison.Ordinal)),Is.EqualTo(32));
            app.SkipVisual();Assert.That(view.gameObject.activeInHierarchy,Is.False);
            Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(stateHash));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));yield return null;
            }
            finally{if(hadQuality)PlayerPrefs.SetInt("art07.quality",oldQuality);else PlayerPrefs.DeleteKey("art07.quality");}
        }
        private static Rect ReadBurstSpriteInk07(Sprite sprite)
        {
            Assert.That(sprite,Is.Not.Null);var source=sprite.texture;var old=RenderTexture.active;
            var target=RenderTexture.GetTemporary(source.width,source.height,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var copy=new Texture2D(source.width,source.height,TextureFormat.RGBA32,false,true);
            try
            {
                Graphics.Blit(source,target);RenderTexture.active=target;copy.ReadPixels(new Rect(0,0,source.width,source.height),0,0);copy.Apply(false,false);
                var pixels=copy.GetPixels32();var region=sprite.textureRect;int minX=source.width,minY=source.height,maxX=-1,maxY=-1;
                for(int y=(int)region.yMin;y<(int)region.yMax;y++)for(int x=(int)region.xMin;x<(int)region.xMax;x++)if(pixels[y*source.width+x].a>=38){minX=Math.Min(minX,x);minY=Math.Min(minY,y);maxX=Math.Max(maxX,x);maxY=Math.Max(maxY,y);}
                Assert.That(maxX,Is.GreaterThanOrEqualTo(minX),"Imported FX texture has no visible alpha ink.");
                // Mirror this project's uGUI Image.GenerateSimpleSprite mapping:
                // outer UVs map into the padded drawing quad, whose denominator is
                // the original sprite.rect, never the trimmed atlas textureRect.
                // Using signed UV extents also preserves flipped atlas orientation.
                var uv=UnityEngine.Sprites.DataUtility.GetOuterUV(sprite);var padding=UnityEngine.Sprites.DataUtility.GetPadding(sprite);
                float sw=Mathf.RoundToInt(sprite.rect.width),sh=Mathf.RoundToInt(sprite.rect.height);
                float x0=padding.x/sw,x1=(sw-padding.z)/sw,y0=padding.y/sh,y1=(sh-padding.w)/sh;
                float MapX(float pixel)=>Mathf.LerpUnclamped(x0,x1,(pixel/source.width-uv.x)/(uv.z-uv.x));
                float MapY(float pixel)=>Mathf.LerpUnclamped(y0,y1,(pixel/source.height-uv.y)/(uv.w-uv.y));
                float a=MapX(minX),b=MapX(maxX+1),c=MapY(minY),d=MapY(maxY+1);
                var ink=Rect.MinMaxRect(Mathf.Min(a,b),Mathf.Min(c,d),Mathf.Max(a,b),Mathf.Max(c,d));
                TestContext.WriteLine("Burst atlas "+sprite.name+" original="+sprite.rect+" trimmed="+region+" offset="+sprite.textureRectOffset+" packing="+sprite.packingRotation+" padding="+padding+" uv="+uv+" ink="+ink);
                return ink;
            }
            finally{RenderTexture.active=old;RenderTexture.ReleaseTemporary(target);UnityEngine.Object.Destroy(copy);}
        }
        private static Rect BurstRectIn07(Transform source,Rect local,Transform target)
        {
            var a=target.InverseTransformPoint(source.TransformPoint(new Vector3(local.xMin,local.yMin,0)));var b=target.InverseTransformPoint(source.TransformPoint(new Vector3(local.xMin,local.yMax,0)));
            var c=target.InverseTransformPoint(source.TransformPoint(new Vector3(local.xMax,local.yMin,0)));var d=target.InverseTransformPoint(source.TransformPoint(new Vector3(local.xMax,local.yMax,0)));
            float minX=Mathf.Min(a.x,b.x,c.x,d.x),maxX=Mathf.Max(a.x,b.x,c.x,d.x),minY=Mathf.Min(a.y,b.y,c.y,d.y),maxY=Mathf.Max(a.y,b.y,c.y,d.y);
            return Rect.MinMaxRect(minX,minY,maxX,maxY);
        }
    }
}
