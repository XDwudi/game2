using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using DiceCraft.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_ART071_FullArtEveryContentAndPrefabUsesPrintAssets()
        {
            var ids=app.Config.controllers.Select(x=>x.id).Concat(app.Config.consumables.Select(x=>x.id)).Concat(app.Config.services.Select(x=>x.id)).Concat(app.Config.diceUpgrades.Select(x=>x.id)).ToArray();
            CollectionAssert.AreEquivalent(ids.Select(id=>"illustration_"+id.ToLowerInvariant()),app.contentIcons.Select(s=>s.name));
            foreach(var sprite in app.contentIcons)
            {
                Assert.That(sprite.texture.filterMode,Is.EqualTo(FilterMode.Bilinear),sprite.name);
                Assert.That(Mathf.Min(sprite.rect.width,sprite.rect.height),Is.GreaterThanOrEqualTo(768),sprite.name);
                Assert.That(sprite.border,Is.EqualTo(Vector4.zero),sprite.name+" illustration cannot be nine-sliced");
            }
            foreach(var original in ArtLibraryReferences())
            {
                foreach(var image in original.GetComponentsInChildren<Image>(true).Where(i=>i.sprite!=null))
                {
                    Assert.That(image.sprite.name.StartsWith("icon_",StringComparison.OrdinalIgnoreCase),Is.False,original.assetId+"/"+image.name);
                    Assert.That(image.sprite.texture.filterMode,Is.EqualTo(FilterMode.Bilinear),original.assetId+"/"+image.name);
                    #if UNITY_EDITOR
                    Assert.That(UnityEditor.AssetDatabase.GetAssetPath(image.sprite),Does.StartWith("Assets/Art/Art071/").Or.StartWith("Assets/Art/Art072/").Or.StartWith("Assets/Art/Art073/"),original.assetId+"/"+image.name); // v0.72 and v0.73 replace the shared surfaces by name; illustrations, symbols and FX stay Art071.
                    #endif
                }
            }
            var wheel=ArtLibraryClone("fx_random_reveal");
            try
            {
                Assert.That(wheel.icon.sprite.name,Is.EqualTo("fx_ring_soft"));Assert.That(wheel.uprightWheelLabels.Length,Is.EqualTo(9));
                for(int i=0;i<9;i++)
                {
                    wheel.SetRevealSlice(i);wheel.ConfigureCommittedReveal(true,.2f);wheel.Play(Vector3.zero,true,.2f);wheel.Evaluate(.5f);
                    var label=wheel.uprightWheelLabels[i];var inWheel=wheel.icon.rectTransform.localRotation*label.localPosition;
                    Assert.That(inWheel.x,Is.EqualTo(0).Within(.001f));Assert.That(inWheel.y,Is.GreaterThan(40));
                    Assert.That(Quaternion.Angle(wheel.icon.rectTransform.localRotation*label.localRotation,Quaternion.identity),Is.LessThan(.001f));
                    Assert.That(wheel.value.text,Is.Empty,"Wheel sectors must never invent an uncommitted result");
                }
            }
            finally{UnityEngine.Object.Destroy(wheel.gameObject);}
            yield return null;
        }

        [UnityTest]
        public IEnumerator PLAY_ART071_FullArtMenuUsesWordmarkAndPrintedBacks()
        {
            yield return ExtraBegin07();
            var title=FindActive<Image>("Title");Assert.That(title.sprite.name,Is.EqualTo("ui_wordmark_dicecraft"));
            Assert.That(title.preserveAspect,Is.True);Assert.That(title.raycastTarget,Is.False);
            var backs=app.canvas.GetComponentsInChildren<Image>().Where(i=>i.name=="PrintedCardBack").ToArray();
            Assert.That(backs.Length,Is.EqualTo(2));Assert.That(backs.All(b=>b.sprite.name=="ui_card_back"&&b.preserveAspect&&!b.raycastTarget),Is.True);
            yield return ExtraCapture07("v071-full-menu","S01","Actual production menu with shared print wordmark and card backs.");ExtraFinish07();
        }

        [UnityTest]
        public IEnumerator PLAY_ART071_FullArtGalleryRendersAll71ThroughProductionViews()
        {
            yield return ExtraBegin07();
            var ids=app.Config.controllers.Select(x=>x.id).Concat(app.Config.consumables.Select(x=>x.id)).Concat(app.Config.services.Select(x=>x.id)).Concat(app.Config.diceUpgrades.Select(x=>x.id)).ToArray();
            var card=typeof(GameApp).GetMethod("DrawControllerFullBody07",BindingFlags.Instance|BindingFlags.NonPublic);
            var icon=typeof(GameApp).GetMethod("Icon",BindingFlags.Instance|BindingFlags.NonPublic);
            var gallery=UiKit.Rect(app.canvas.transform,"FullArtGallery071",0,0,1920,1080);
            try
            {
                for(int page=0;page<(ids.Length+5)/6;page++)
                {
                    UiKit.Clear(gallery);UiKit.Box(gallery,"Backdrop",0,0,1920,1080,UiKit.Felt);
                    UiKit.Text(gallery,"GalleryTitle","v0.71  实际Unity插画图册  "+(page+1),60,24,1800,56,28,UiKit.Paper);
                    var current=ids.Skip(page*6).Take(6).ToArray();
                    for(int col=0;col<current.Length;col++)
                    {
                        string id=current[col];float x=62+col*300;
                        var panel=UiKit.PaperFrame(gallery,"Card_"+id,x,105,270,380,UiKit.Paper);
                        if(id.StartsWith("C",StringComparison.Ordinal))card.Invoke(app,new object[]{panel,id,"查看条件","none",0});
                        else{UiKit.Text(panel,"Name",id,12,20,246,54,28,UiKit.Ink);icon.Invoke(app,new object[]{panel,id,27f,88f,216f});}
                        UiKit.Text(gallery,"Identifier_"+id,id,x,500,270,42,24,UiKit.Paper);
                        var medium=UiKit.Rect(gallery,"Medium_"+id,x,572,176,176);
                        var small=UiKit.Rect(gallery,"Small_"+id,x+190,572,64,64);
                        icon.Invoke(app,new object[]{medium,id,0f,0f,176f});
                        icon.Invoke(app,new object[]{small,id,0f,0f,64f});
                        var paper=UiKit.Box(gallery,"SmallPaper_"+id,x,810,270,176,UiKit.Paper);
                        var paperMedium=UiKit.Rect(paper,"Medium_"+id,4,0,176,176);
                        var paperSmall=UiKit.Rect(paper,"Small_"+id,190,56,64,64);
                        icon.Invoke(app,new object[]{paperMedium,id,0f,0f,176f});icon.Invoke(app,new object[]{paperSmall,id,0f,0f,64f});
                    }
                    yield return null;Canvas.ForceUpdateCanvases();
                    Assert.That(gallery.GetComponentsInChildren<Image>().Count(i=>i.name.StartsWith("Icon_",StringComparison.Ordinal)),Is.EqualTo(current.Length*5),"Every ID has one full and four separately pooled size/background instances.");
                    foreach(var image in gallery.GetComponentsInChildren<Image>().Where(i=>i.name.StartsWith("Icon_",StringComparison.Ordinal)))
                    {Assert.That(image.sprite,Is.Not.Null);Assert.That(image.sprite.name,Does.StartWith("illustration_"));Assert.That(image.preserveAspect,Is.True);}
                    yield return ExtraCapture07("v071-full-gallery-"+page.ToString("D2"),"asset-gallery","Production card/Icon rendering; six IDs per page at real full, 176 and 64 pixel design sizes on felt and paper. This is an asset inspection view, not a gameplay screen.");
                }
            }
            finally{UnityEngine.Object.Destroy(gallery.gameObject);}
            ExtraFinish07();
        }
    }
}
