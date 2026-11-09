using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private UnityEngine.UI.Image[] ComboAccents071(Art07PrefabView view)=>view.GetComponentsInChildren<UnityEngine.UI.Image>(true)
            .Where(i=>i.name=="ComboLocalRing071"||i.name.StartsWith("ComboGoldArc071_",StringComparison.Ordinal)).ToArray();
        [UnityTest]
        public IEnumerator PLAY_ART071_COMBO_RealTiersUseAuthoredEdgeRingArcsAndGoldWithoutCoveringInk()
        {
            bool wasEnabled=app.enabled;
            try
            {
                foreach(int font in new[]{100,150})foreach(bool ten in new[]{false,true})foreach(int tier in new[]{3,4,5})
                {
                    app.enabled=wasEnabled;
                    FxScene07(scene=>
                    {
                        string[] defs=tier==3?new[]{"C46"}:tier==4?new[]{"C02","C30"}:new[]{"C02","C30","C31","C32"};
                        // Additional C01 instances exercise the real ten-card renderer
                        // in the isolated debug fixture without altering score factors.
                        if(ten)defs=defs.Concat(Enumerable.Repeat("C01",10-defs.Length)).ToArray();
                        FxControllers07(scene,defs);
                        if(ten)foreach(var c in scene.controllers.Skip(4))c.enchantmentId="slotless";
                    });
                    FinalOptions07("Normal",false);SettlementField07("textScale07",font);UiKit.FontScale=font/100f;app.Refresh();
                    Canvas.ForceUpdateCanvases();app.enabled=false;
                    var before=app.Session.State;var result=app.Send(new SessionCommand{kind=CommandKind.Submit});Assert.That(result.accepted,Is.True,result.error);
                    var receipt=PresentationReceiptAdapter.BuildCommitted(result,before,app.Session.State,app.Config);Assert.That(receipt.ComboTier,Is.EqualTo(tier));
                    string state=StateJson();byte[] saved=File.ReadAllBytes(app.SaveLocation);
                    var player=QualifiedPlayer07();var beat=player.Sequence.Beats.Single(b=>b.Phase==PresentationPhase.FinalScore);player.Tick(beat.StartMs/1000d);
                    var view=LiveFx07("fx_combo_burst").Single();var score=(RectTransform)view.transform.parent;
                    Assert.That(view.scoreRing071.name,Is.EqualTo("fx_ring_soft"));Assert.That(view.scoreArc071.name,Is.EqualTo("fx_arc_short"));
                    Assert.That(view.scoreRing071.texture.width,Is.GreaterThanOrEqualTo(256));Assert.That(view.scoreArc071.texture.width,Is.GreaterThanOrEqualTo(256));
                    var protectedRects=new List<Rect>();
                    // Sample the actual maximum numeral pulse as a readability stress
                    // pose, using the existing published ScalePeak rather than a new threshold.
                    var final=FindActive<TextMeshProUGUI>("FinalScore");var oldScale=final.rectTransform.localScale;
                    final.rectTransform.localScale=Vector3.one*ScorePresentationTier.ScalePeak(tier,new PresentationOptions());Canvas.ForceUpdateCanvases();
                    foreach(var text in score.GetComponentsInChildren<TextMeshProUGUI>(false))if(!string.IsNullOrWhiteSpace(text.text))protectedRects.AddRange(PostInkScreenRects07(text,true));
                    foreach(var button in score.GetComponentsInChildren<UnityEngine.UI.Button>(false))protectedRects.Add(V06ScreenRect((RectTransform)button.transform));
                    foreach(var card in app.canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.name.StartsWith("Controller_",StringComparison.Ordinal)))protectedRects.Add(V06ScreenRect((RectTransform)card.transform));
                    var nodes=view.GetComponentsInChildren<Transform>(true).Select(t=>t.GetEntityId()).ToArray();
                    foreach(float time in new[]{.08f,.30f,.60f})
                    {
                        view.Evaluate(time);Canvas.ForceUpdateCanvases();
                        var marks=ComboAccents071(view).Where(m=>m.gameObject.activeInHierarchy).ToArray();Assert.That(marks.Length,Is.EqualTo(tier==3?1:3),"The effect must visibly exist, not silently skip every occupied slot.");
                        var gold=view.burstStaticStars.Where(g=>g.activeInHierarchy).ToArray();Assert.That(gold.Length,Is.EqualTo(tier==5?5:0));
                        foreach(var image in marks.Concat(gold.Select(g=>g.GetComponent<UnityEngine.UI.Image>())))
                        {
                            Assert.That(image.raycastTarget,Is.False);Assert.That(image.color.a,Is.GreaterThan(0));
                            var bounds=V06ScreenRect(image.rectTransform);
                            Assert.That(protectedRects.Any(r=>r.Overlaps(bounds)),Is.False,"New ring/arc/star quad covers actual peak score glyph, card or button: "+image.name+" "+bounds);
                            var panel=V06ScreenRect(score);Assert.That(panel.Contains(bounds.min)&&panel.Contains(bounds.max),Is.True,"Local accent escaped the score panel.");
                        }
                        Assert.That(view.burstLayers[1].activeInHierarchy||view.burstLayers[3].activeInHierarchy||view.burstLayers[4].activeInHierarchy,Is.False,"The three legacy central ring/halo layers remain forbidden.");
                        Assert.That(view.particles.Count(p=>p.gameObject.activeInHierarchy),Is.EqualTo(ScorePresentationTier.ParticleCount(tier,new PresentationOptions())));
                    }
                    if(font==150&&ten){view.Evaluate(.30f);yield return CaptureEdge07("v071-combo-edge-L"+tier+"-ten-font150-peak-score-pose");}
                    final.rectTransform.localScale=oldScale;
                    view.Evaluate(.8f);Assert.That(ComboAccents071(view).Any(i=>i.gameObject.activeSelf),Is.False);Assert.That(view.burstStaticStars.Any(g=>g.activeSelf),Is.False,"Gold explosion must become quiet, not keep pulsing.");
                    CollectionAssert.AreEqual(nodes,view.GetComponentsInChildren<Transform>(true).Select(t=>t.GetEntityId()));
                    app.SkipVisual();Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(saved,File.ReadAllBytes(app.SaveLocation));
                }
            }
            finally{app.enabled=wasEnabled;}
        }
        [UnityTest]
        public IEnumerator PLAY_ART071_COMBO_ReducedLowAndPoolReuseKeepNewAccentsQuietAndBounded()
        {
            var host=new GameObject("ComboEdgeFixture",typeof(RectTransform));host.transform.SetParent(app.canvas.transform,false);
            var view=ArtLibraryClone("fx_combo_burst");view.transform.SetParent(host.transform,false);
            try
            {
                // Inactive effect prefabs may defer Awake until first Play. Warm the
                // real one-time layout/mark construction before measuring reuse.
                view.SetBurstLevel(5);view.Play(Vector3.zero);view.ConfigureScoreBurst(new Vector2(1528,156));view.ResetForPool();
                var nodes=view.GetComponentsInChildren<Transform>(true).Select(t=>t.GetEntityId()).ToArray();
                foreach(bool low in new[]{false,true})foreach(bool reduced in new[]{false,true})
                {
                    view.ResetForPool();view.SetBurstLevel(5);view.Play(Vector3.zero,reduced);
                    view.ConfigureScoreBurst(new Vector2(1528,156),low);view.Evaluate(.2f);
                    Assert.That(ComboAccents071(view).Count(i=>i.gameObject.activeInHierarchy),Is.EqualTo(low||reduced?0:3));
                    if(reduced)Assert.That(view.burstStaticStars.Count(g=>g.activeInHierarchy),Is.EqualTo(5));
                    else Assert.That(view.burstStaticStars.Count(g=>g.activeInHierarchy),Is.EqualTo(low?0:5));
                    view.ResetForPool();Assert.That(ComboAccents071(view).Any(i=>i.gameObject.activeSelf),Is.False);
                    view.SetBurstLevel(0);view.Play(Vector3.zero);view.ConfigureScoreBurst(new Vector2(1528,156));view.Evaluate(.2f);
                    Assert.That(ComboAccents071(view).Any(i=>i.gameObject.activeSelf)||view.burstStaticStars.Any(g=>g.activeSelf),Is.False);
                    CollectionAssert.AreEqual(nodes,view.GetComponentsInChildren<Transform>(true).Select(t=>t.GetEntityId()));
                }
                yield return null;
            }
            finally{UnityEngine.Object.Destroy(view.gameObject);UnityEngine.Object.Destroy(host);}
        }
    }
}
