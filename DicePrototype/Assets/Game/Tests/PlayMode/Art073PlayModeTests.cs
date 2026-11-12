using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    // v0.73 acceptance (docs/specs/v073/VERSION_PLAN.md section 8): V73-B shop page, V73-D high-score
    // celebration, V73-E dice library, V73-F no flat face tiles where a die's faces are shown.
    public sealed partial class GameplayPlayModeTests
    {
        private IEnumerator Art073Settle(float seconds=.45f)
        {
            double until=Time.realtimeSinceStartupAsDouble+seconds;
            while(Time.realtimeSinceStartupAsDouble<until)yield return null;
            Canvas.ForceUpdateCanvases();yield return null;
        }
        private Art073DieView[] Art073Views(string under)
        {
            var root=FindActive<RectTransform>(under);
            return root.GetComponentsInChildren<Art073DieView>(false).Where(v=>v.gameObject.activeInHierarchy).ToArray();
        }
        private static bool Art073Inside(Rect inner,Rect outer,float slack=1.5f)=>
            inner.xMin>=outer.xMin-slack&&inner.xMax<=outer.xMax+slack&&inner.yMin>=outer.yMin-slack&&inner.yMax<=outer.yMax+slack;
        private static bool Art073Overlap(Rect a,Rect b,float slack=1f)=>
            a.xMin<b.xMax-slack&&b.xMin<a.xMax-slack&&a.yMin<b.yMax-slack&&b.yMin<a.yMax-slack;

        [UnityTest]
        public IEnumerator PLAY_ART073_ViewDiceOpensLibraryWithRealDiceAndFaceButtonsWithoutChangingState()
        {
            StartRun("73001");Click("StartStage");Click("Roll");app.SkipVisual();yield return WaitForIdle();
            string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
            var open=Button("ViewDice");Rect hit=ExtraBounds07((RectTransform)open.transform);
            Assert.That(hit.width,Is.GreaterThanOrEqualTo(39.9f));Assert.That(hit.height,Is.GreaterThanOrEqualTo(39.9f));
            Assert.That(app.UiDice073.ActiveVisualCount,Is.Zero,"No interface die is drawn on the plain stage screen.");
            Click("ViewDice");
            Assert.That(FindActive<TextMeshProUGUI>("DialogTitle").text,Does.StartWith("骰库"));
            var dice=app.Session.State.dice.OrderBy(d=>d.createdSerial).ToArray();
            foreach(var die in dice)Assert.That(HasActive("Target_"+die.dieId),Is.True,die.dieId);
            yield return Art073Settle();
            // Every list entry is a real die on the interface layer, not a flat face tile.
            var views=Art073Views("ShopTargetList");
            Assert.That(views.Length,Is.EqualTo(dice.Length));Assert.That(views.All(v=>v.IsShown),Is.True);
            Assert.That(app.UiDice073.ActiveVisualCount,Is.EqualTo(dice.Length));
            Assert.That(app.UiDiceLayer073.Image.enabled,Is.True);
            Assert.That(app.UiDiceLayer073.Texture.width,Is.EqualTo(Screen.width));Assert.That(app.UiDiceLayer073.Texture.height,Is.EqualTo(Screen.height));
            Assert.That(app.canvas.GetComponentsInChildren<RectTransform>(false).Any(r=>r.name=="ExampleFace"),Is.False,"V73-F: no flat face tile in the dice library.");
            Assert.That(app.table.GetFaceOrientation("$die-preview"),Is.Not.Null,"The large die is the real table die.");
            // Each face button turns the real die until that physical face looks at the player.
            for(int face=0;face<6;face++)
            {
                Click("Face_F0"+(face+1));
                double deadline=Time.realtimeSinceStartupAsDouble+2;while(app.SheetTurning073&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;
                Assert.That(app.SheetFacingFace073,Is.EqualTo(face),"face button "+(face+1));
                Assert.That(app.table.PreviewFacingFace073(),Is.EqualTo(face),"drawn pose "+(face+1));
                Assert.That(FindActive<TextMeshProUGUI>("FacingFace073").text,Does.Contain("面 "+(face+1)).And.Contain(dice[0].faces[face].permanentPip+"点"));
                Assert.That(FindActive<Transform>("Face_F0"+(face+1)).Find("SelectedH0").gameObject.activeSelf,Is.True);
            }
            // Dragging turns it and keeps the pose; a point on the facing face picks that face.
            Quaternion before=app.SheetRotation073;var rotate=FindActive<DiePointer>("RotatePreview");
            ExecuteEvents.Execute(rotate.gameObject,new PointerEventData(EventSystem.current){delta=new Vector2(140,-60)},ExecuteEvents.dragHandler);
            Assert.That(Quaternion.Angle(before,app.SheetRotation073),Is.GreaterThan(20f));
            Click("Face_F03");
            {double deadline=Time.realtimeSinceStartupAsDouble+2;while(app.SheetTurning073&&Time.realtimeSinceStartupAsDouble<deadline)yield return null;}
            var area=(RectTransform)rotate.transform;var centre=RectTransformUtility.WorldToScreenPoint(null,area.TransformPoint(area.rect.center));
            Assert.That(app.table.TryPickPreviewFace073(centre,out int picked),Is.True);Assert.That(picked,Is.EqualTo(2));
            Click("CloseDialog");yield return null;yield return null;
            Assert.That(app.UiDice073.ActiveVisualCount,Is.Zero,"Closing the library removes every interface die.");
            Assert.That(app.UiDiceLayer073.Image.enabled,Is.False);
            Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
        }

        [UnityTest]
        public IEnumerator PLAY_ART073_DieViewDragTurnAndFacePickArePureViewOperations()
        {
            var fixture=ShopBoundaryFixture();yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();
            yield return Art073Settle(.8f);
            string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            var view=Art073Views("OwnedStrip").First();Assert.That(view.IsShown,Is.True);
            Quaternion rest=view.Rotation;view.Turn(new Vector2(90,-40));yield return null;
            Assert.That(Quaternion.Angle(rest,view.Rotation),Is.GreaterThan(15f));
            var rect=(RectTransform)view.transform;var centre=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
            for(int face=0;face<6;face++)
            {
                view.TurnToFace(face,true);yield return null;
                Assert.That(view.FacingFace,Is.EqualTo(face));
                Assert.That(app.UiDice073.TryPickFace073(view.Slot,centre,out int picked),Is.True,"centre ray "+face);Assert.That(picked,Is.EqualTo(face));
            }
            // A redraw of the same die keeps the pose the player left.
            Quaternion left=view.Rotation;app.Refresh();yield return null;yield return null;
            Assert.That(Quaternion.Angle(left,Art073Views("OwnedStrip").First().Rotation),Is.LessThan(.01f));
            Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
        }

        private void Art073CheckShopPage(string scope)
        {
            foreach(string gone in new[]{"Tab0","Tab1","Tab2","Tab3","Tab4","ActiveShopTab","ShopOwnedItems"})Assert.That(HasActive(gone),Is.False,scope+": "+gone);
            Rect screen=Screen.safeArea;var offers=app.Session.State.shop.offers;
            var sections=new Dictionary<string,string>{{"controller","ControllerShelf"},{"new_die","DiceCounter"},{"consumable","ConsumableSection"},
                {"hand_upgrade","ResearchSection"},{"color_upgrade","ResearchSection"},{"die_upgrade","UpgradeSection"},{"service","ServiceSection"}};
            foreach(string section in sections.Values.Distinct().Concat(new[]{"OwnedStrip","ShopProducts","Stats07"}))
                Assert.That(Art073Inside(V06ScreenRect(FindActive<RectTransform>(section)),screen),Is.True,scope+": "+section+" leaves the screen");
            var cards=new List<KeyValuePair<string,Rect>>();
            foreach(var offer in offers)
            {
                string name=(offer.category=="controller"?"Offer_":"Product_")+offer.offerId;var card=FindActive<RectTransform>(name);
                Rect bounds=V06ScreenRect(card);
                long cost=app.Session.GetOfferNetPrice(offer);
                Assert.That(card.Find("PriceChip/Price").GetComponent<TextMeshProUGUI>().text,Is.EqualTo(cost<0?"+"+(-cost):cost.ToString()),scope+": price of "+name);
                var buy=Button("Buy_"+offer.offerId);Rect buyBounds=ExtraBounds07((RectTransform)buy.transform);
                Assert.That(buyBounds.height,Is.GreaterThanOrEqualTo(39.9f),scope+": buy target of "+name);
                // More goods than one row scroll inside their own section: such a card is judged by the part its viewport shows.
                var scroll=card.GetComponentInParent<UnityEngine.UI.ScrollRect>();
                if(scroll!=null)
                {
                    Rect view=V06ScreenRect(scroll.viewport);
                    Assert.That(Art073Inside(view,V06ScreenRect(FindActive<RectTransform>(sections[offer.category]))),Is.True,scope+": scroll viewport of "+name+" leaves its section");
                    bounds=Rect.MinMaxRect(Mathf.Max(bounds.xMin,view.xMin),Mathf.Max(bounds.yMin,view.yMin),Mathf.Min(bounds.xMax,view.xMax),Mathf.Min(bounds.yMax,view.yMax));
                    if(bounds.width<=0||bounds.height<=0)continue;
                }
                else Assert.That(Art073Inside(bounds,V06ScreenRect(FindActive<RectTransform>(sections[offer.category]))),Is.True,scope+": "+name+" leaves its section");
                cards.Add(new KeyValuePair<string,Rect>(name,bounds));
            }
            cards.Add(new KeyValuePair<string,Rect>("RefreshControllers",V06ScreenRect((RectTransform)Button("RefreshControllers").transform)));
            cards.Add(new KeyValuePair<string,Rect>("LeaveShop",V06ScreenRect((RectTransform)Button("LeaveShop").transform)));
            cards.Add(new KeyValuePair<string,Rect>("OwnedStrip",V06ScreenRect(FindActive<RectTransform>("OwnedStrip"))));
            cards.Add(new KeyValuePair<string,Rect>("Study",V06ScreenRect((RectTransform)FindActive<RectTransform>("Study"))));
            for(int a=0;a<cards.Count;a++)for(int b=a+1;b<cards.Count;b++)
                Assert.That(Art073Overlap(cards[a].Value,cards[b].Value),Is.False,scope+": "+cards[a].Key+" overlaps "+cards[b].Key);
            Assert.That(Art073Inside(V06ScreenRect((RectTransform)Button("RefreshControllers").transform),V06ScreenRect(FindActive<RectTransform>("ControllerShelf"))),Is.True);
        }

        [UnityTest]
        public IEnumerator PLAY_ART073_ShopSinglePageShowsEverySectionRealDiceAndNoTabs()
        {
            var fixture=ShopBoundaryFixture();yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();
            Assert.That(app.Session.State.phase,Is.EqualTo("Shop"));
            string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);long sequence=app.Session.ActionSequence;
            yield return Art073Settle(.8f);
            Art073CheckShopPage("base");
            var offers=app.Session.State.shop.offers;
            foreach(string category in new[]{"controller","consumable","die_upgrade","service","new_die"})Assert.That(offers.Any(o=>o.category==category),Is.True,category);
            Assert.That(FindActive<TextMeshProUGUI>("ShopTitle").text,Is.EqualTo("商店"));
            Assert.That(Button("LeaveShop").GetComponentInChildren<TextMeshProUGUI>().text,Is.EqualTo("下一关"));
            // Dice for sale and owned dice are real dice; nothing in either place is a flat face tile.
            int forSale=offers.Count(o=>o.category=="new_die"),owned=Math.Min(8,app.Session.State.dice.Count);
            var counter=Art073Views("DiceCounter");var strip=Art073Views("OwnedStrip");
            Assert.That(counter.Length,Is.EqualTo(forSale));Assert.That(strip.Length,Is.EqualTo(owned));
            Assert.That(counter.Concat(strip).All(v=>v.IsShown),Is.True);
            Assert.That(app.UiDice073.ActiveVisualCount,Is.EqualTo(forSale+owned));
            foreach(string place in new[]{"DiceCounter","OwnedStrip"})
                Assert.That(FindActive<RectTransform>(place).GetComponentsInChildren<RectTransform>(false).Any(r=>r.name=="ExampleFace"||r.name.StartsWith("PreviewFace",StringComparison.Ordinal)),Is.False,"V73-F: "+place);
            // A die on the counter turns under a drag and opens its candidate sheet on a click.
            var sale=counter[0];Quaternion pose=sale.Rotation;
            ExecuteEvents.Execute(sale.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(sale.gameObject,new PointerEventData(EventSystem.current){delta=new Vector2(120,30)},ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(sale.gameObject,new PointerEventData(EventSystem.current),ExecuteEvents.endDragHandler);
            Assert.That(Quaternion.Angle(pose,sale.Rotation),Is.GreaterThan(20f));
            ExecuteEvents.Execute(sale.gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
            Assert.That(FindActive<TextMeshProUGUI>("DialogTitle").text,Does.StartWith("加入骰子"));
            yield return null;yield return null;
            Assert.That(app.UiDice073.ActiveVisualCount,Is.Zero,"Screen dice hide while a modal owns the screen.");
            Click("CloseDialog");yield return Art073Settle(.3f);
            Assert.That(app.UiDice073.ActiveVisualCount,Is.EqualTo(forSale+owned));
            // An owned die opens the dice library on itself.
            ExecuteEvents.Execute(strip[1].gameObject,new PointerEventData(EventSystem.current){button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
            Assert.That(FindActive<TextMeshProUGUI>("DialogTitle").text,Does.StartWith("骰库"));
            Assert.That(FindActive<TextMeshProUGUI>("LayersBody"),Is.Not.Null);Click("CloseDialog");yield return null;
            Assert.That(StateJson(),Is.EqualTo(state));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
        }

        [UnityTest]
        public IEnumerator PLAY_ART073_ShopAtMaximumShelfSizesStillFitsOnePage()
        {
            foreach(int font in new[]{100,150})
            {
                PlayerPrefs.SetInt("art07.textScale",font);
                var fixture=ShopBoundaryFixture();
                fixture.controllerOfferPurchases=app.Config.shop.maxControllerOffers-app.Config.shop.initialControllerOffers;
                fixture.consumableOfferPurchases=app.Config.shop.maxConsumableOffers-app.Config.shop.initialConsumableOffers;
                var offered=new HashSet<string>(fixture.shop.offers.Select(o=>o.contentId));int added=0;
                foreach(var definition in app.Config.controllers.Where(c=>!offered.Contains(c.id)))
                {
                    if(fixture.shop.offers.Count(o=>o.category=="controller")>=app.Config.shop.maxControllerOffers)break;
                    AddBoundaryOffer(fixture,"max-controller-"+added++,"controller",definition.id);
                }
                foreach(var definition in app.Config.consumables.Where(c=>!offered.Contains(c.id)))
                {
                    if(fixture.shop.offers.Count(o=>o.category=="consumable")>=app.Config.shop.maxConsumableOffers)break;
                    AddBoundaryOffer(fixture,"max-item-"+added++,"consumable",definition.id);
                }
                Assert.That(fixture.shop.offers.Count(o=>o.category=="controller"),Is.EqualTo(app.Config.shop.maxControllerOffers));
                Assert.That(fixture.shop.offers.Count(o=>o.category=="consumable"),Is.EqualTo(app.Config.shop.maxConsumableOffers));
                yield return LoadBoundaryFixture(fixture);ResumeBoundaryFixture();yield return Art073Settle(.8f);
                Art073CheckShopPage("maximum font "+font);
                // Six controller cards: two rows of three, all inside the shelf, none under the refresh button.
                var rows=app.Session.State.shop.offers.Where(o=>o.category=="controller").Select(o=>FindActive<RectTransform>("Offer_"+o.offerId).anchoredPosition.y).Distinct().Count();
                Assert.That(rows,Is.EqualTo(2));
                Assert.That(HasActive("ConsumableShelf"),Is.True,"More than three consumables scroll inside their own section.");
                foreach(var text in FindActive<RectTransform>("Stats07").GetComponentsInChildren<TextMeshProUGUI>(false))
                    Assert.That(Art073Inside(V06ScreenRect(text.rectTransform),V06ScreenRect(FindActive<RectTransform>("Stats07")),3),Is.True,"font "+font+": "+text.name+" leaves the status panel");
            }
            PlayerPrefs.SetInt("art07.textScale",100);
        }

        [UnityTest]
        public IEnumerator PLAY_ART073_LockedDiceShowTrayLipAndPadlockAndStyleLegendShowsEightRealDice()
        {
            StartRun("73002");Click("StartStage");Click("Roll");app.SkipVisual();yield return WaitForIdle();
            string first=app.Session.State.dice.OrderBy(d=>d.createdSerial).First().dieId;
            ClickDie(first);Click("Confirm");yield return WaitForIdle();
            Assert.That(app.Session.State.hand.orderedLockedIds,Is.EquivalentTo(new[]{first}));
            var lip=FindActive<RectTransform>("SlotTray073_0");var slot=FindActive<RectTransform>("Slot0");
            Assert.That(V06ScreenRect(lip).yMax,Is.LessThanOrEqualTo(V06ScreenRect(slot).yMin+6*app.canvas.scaleFactor),"The tray lip sits at the bottom edge of its slot, below the die.");
            Assert.That(FindActive<RectTransform>("LockMark0").Find("Padlock").gameObject.activeSelf,Is.True);
            Assert.That(HasActive("SlotTray073_1"),Is.False,"Only locked slots carry a tray lip.");
            string state=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
            Click("ViewDice");Click("StyleLegend073");yield return Art073Settle(.6f);
            Assert.That(FindActive<TextMeshProUGUI>("DialogTitle").text,Is.EqualTo("骰子样式"));
            var cells=app.canvas.GetComponentsInChildren<RectTransform>(false).Where(r=>r.name.StartsWith("StyleCell",StringComparison.Ordinal)).ToArray();
            Assert.That(cells.Length,Is.EqualTo(8));
            string[] names=cells.Select(c=>c.Find("NameChip/Name").GetComponent<TextMeshProUGUI>().text).ToArray();
            CollectionAssert.AreEqual(new[]{"普通","光滑","粗糙","黄金","玻璃","复合","临时","锁定"},names);
            var views=cells.Select(c=>c.GetComponentInChildren<Art073DieView>(false)).ToArray();
            Assert.That(views.All(v=>v!=null&&v.IsShown),Is.True);Assert.That(app.UiDice073.ActiveVisualCount,Is.EqualTo(8));
            // Finishes are separate submeshes over an unchanged pip mesh: plain 1, smooth/rough/gold 2.
            int[] expectedSubmeshes={1,2,2,2,1,2,1,1};
            for(int i=0;i<8;i++)
            {
                var face=app.UiDice073.GetFaceOrientation("$static-preview:style-"+i).Find("Face_F01");
                Assert.That(face.GetComponent<MeshFilter>().sharedMesh.subMeshCount,Is.EqualTo(expectedSubmeshes[i]),names[i]);
                Assert.That(face.GetComponent<MeshRenderer>().sharedMaterial,Is.SameAs(app.table.faceMaterial),names[i]);
                var body=app.UiDice073.GetFaceOrientation("$static-preview:style-"+i).Find("Body").GetComponent<MeshRenderer>();
                Assert.That(body.sharedMaterial,Is.SameAs(i==4||i==5?app.table.glassMaterial:app.table.bodyMaterial),names[i]);
                Assert.That(body.gameObject.layer,Is.EqualTo(Art073DiceLayer.Layer));
            }
            Click("CloseDialog");yield return null;
            Assert.That(StateJson(),Is.EqualTo(state));CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));
        }

        [UnityTest]
        public IEnumerator PLAY_ART073_HighScoreBurstStartsOnFinalScoreCountsUpAndLeavesWithoutBlocking()
        {
            Art07Fixtures.Apply(app,Art07FixtureKind.G147);FinalOptions07("Normal",false);
            Assert.That(app.HighScoreTierShown073,Is.Zero);
            Art07Fixtures.Submit(app,Art07FixtureKind.G147);string state=GameSession.StateHash(app.Session.State);byte[] save=File.ReadAllBytes(app.SaveLocation);
            Assert.That(app.HighScoreTierShown073,Is.Zero,"Nothing before the committed final-score beat.");
            yield return WaitFxPhase07(PresentationPhase.FinalScore);
            Assert.That(app.HighScoreTierShown073,Is.EqualTo(HighScore073.Burst),"967,680 at total multiplier x26,880 is the top tier.");
            Assert.That(HasActive("Crown"),Is.True);Assert.That(HasActive("Ray0"),Is.True);Assert.That(HasActive("Spark0"),Is.True);
            var layer=FindActive<RectTransform>("HighScore073");var group=layer.GetComponent<CanvasGroup>();
            Assert.That(group.blocksRaycasts,Is.False);Assert.That(group.interactable,Is.False);
            Assert.That(layer.GetComponentsInChildren<UnityEngine.UI.Graphic>(false).Where(g=>g.name!="Number"&&g.name!="Caption"&&g.name!="Chain"&&g.name!="Formula").All(g=>!g.raycastTarget||!group.blocksRaycasts),Is.True);
            Assert.That(FindActive<TextMeshProUGUI>("Chain").text,Does.Contain("连锁").And.Contain("26,880"));
            Assert.That(FindActive<TextMeshProUGUI>("Formula").text.Count(c=>c=='×'),Is.GreaterThanOrEqualTo(2));
            int ticks=layer.GetComponentsInChildren<RectTransform>(false).Count(r=>r.name.StartsWith("Scored",StringComparison.Ordinal));
            Assert.That(ticks,Is.EqualTo(4),"One gold tick per scoring controller.");
            // The number counts up to the committed score and never passes it.
            System.Numerics.BigInteger last=-1,final=System.Numerics.BigInteger.Parse("967680");bool counted=false;
            double limit=Time.realtimeSinceStartupAsDouble+6;
            while(app.PresentationPlaying07&&Time.realtimeSinceStartupAsDouble<limit)
            {
                var shown=System.Numerics.BigInteger.Parse(app.HighScoreText073.Replace(",",""));
                Assert.That(shown>=last&&shown<=final,Is.True,"count-up is monotonic: "+app.HighScoreText073);
                if(shown>0&&shown<final)counted=true;last=shown;
                Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));
                yield return null;
            }
            Assert.That(counted,Is.True,"At least one rendered frame showed an intermediate value.");
            Assert.That(app.PresentationPlaying07||app.IsBusy,Is.False);
            // After the presentation it only fades: input is free and the final number is exact.
            Assert.That(app.HighScoreTierShown073,Is.EqualTo(HighScore073.Burst));Assert.That(app.HighScoreFading073,Is.True);
            Assert.That(app.HighScoreText073,Is.EqualTo("967,680"));
            limit=Time.realtimeSinceStartupAsDouble+2;while(app.HighScoreTierShown073>0&&Time.realtimeSinceStartupAsDouble<limit){Assert.That(app.IsBusy,Is.False);yield return null;}
            Assert.That(app.HighScoreTierShown073,Is.Zero);Assert.That(HasActive("Plate"),Is.False);
            Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state));CollectionAssert.AreEqual(save,File.ReadAllBytes(app.SaveLocation));
        }

        [UnityTest]
        public IEnumerator PLAY_ART073_HighScoreFastReducedInstantSkipAndBelowThreshold()
        {
            foreach(string mode in new[]{"Fast","Reduced","Instant","Skip"})
            {
                Art07Fixtures.Apply(app,Art07FixtureKind.G147);FinalOptions07(mode=="Fast"||mode=="Instant"?mode:"Normal",mode=="Reduced");
                Art07Fixtures.Submit(app,Art07FixtureKind.G147);string state=GameSession.StateHash(app.Session.State);
                if(mode=="Instant")
                {
                    Assert.That(app.PresentationPlaying07,Is.False);Assert.That(app.HighScoreTierShown073,Is.Zero,"Instant never shows the celebration.");
                    yield return null;Assert.That(app.HighScoreTierShown073,Is.Zero);continue;
                }
                yield return WaitFxPhase07(PresentationPhase.FinalScore);
                Assert.That(app.HighScoreTierShown073,Is.EqualTo(HighScore073.Burst),mode);
                if(mode=="Fast"||mode=="Reduced")Assert.That(app.HighScoreText073,Is.EqualTo("967,680"),mode+" shows the final value at once.");
                if(mode=="Reduced")
                {
                    Assert.That(HasActive("Ray0"),Is.False);Assert.That(HasActive("Spark0"),Is.False);Assert.That(HasActive("Crown"),Is.True);
                    yield return null;
                    Assert.That(FindActive<RectTransform>("Plate").localScale,Is.EqualTo(Vector3.one),"Reduced motion: no scale.");
                    Assert.That(FindActive<RectTransform>("HighScore073").GetComponent<CanvasGroup>().alpha,Is.EqualTo(1));
                }
                if(mode=="Skip")
                {
                    app.SkipVisual();
                    Assert.That(app.HighScoreTierShown073,Is.Zero,"Skipping drops the celebration in the same call.");Assert.That(HasActive("Plate"),Is.False);
                }
                double limit=Time.realtimeSinceStartupAsDouble+6;
                while((app.PresentationPlaying07||app.HighScoreTierShown073>0)&&Time.realtimeSinceStartupAsDouble<limit)yield return null;
                Assert.That(app.HighScoreTierShown073,Is.Zero,mode);Assert.That(app.IsBusy,Is.False);
                Assert.That(GameSession.StateHash(app.Session.State),Is.EqualTo(state),mode);
            }
            // A hand that scores nothing never celebrates; a replay of a high hand celebrates again.
            Art07Fixtures.Apply(app,Art07FixtureKind.ZeroMultiplier);FinalOptions07("Normal",false);Art07Fixtures.Submit(app,Art07FixtureKind.ZeroMultiplier);
            {double limit=Time.realtimeSinceStartupAsDouble+6;while(app.PresentationPlaying07&&Time.realtimeSinceStartupAsDouble<limit){Assert.That(app.HighScoreTierShown073,Is.Zero);yield return null;}}
            Art07Fixtures.Apply(app,Art07FixtureKind.G147);Art07Fixtures.Submit(app,Art07FixtureKind.G147);app.SkipVisual();yield return null;
            Assert.That(app.ReplayLastPresentation07(),Is.True);yield return WaitFxPhase07(PresentationPhase.FinalScore);
            Assert.That(app.HighScoreTierShown073,Is.EqualTo(HighScore073.Burst),"Read-only replay shows the same celebration.");
            app.SkipVisual();Assert.That(app.HighScoreTierShown073,Is.Zero);
        }
    }
}
