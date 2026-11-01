using System.Collections;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private IEnumerator InterestCheck07(int sources,string id)
        {
            long rate=1L+sources*app.Config.Controller("C01").parameters.extraInterestPerTen;
            Assert.That(ControllerRules.ResolveSources(app.Session.State.controllers).Count(s=>s.abilityId=="C01"),Is.EqualTo(sources));
            string expected="每"+app.Config.interestDivisor+"金币利息 +"+rate+"\n计息本金上限 "+app.Config.maxInterestPrincipal;
            yield return ExtraCapture07(id,"S10","Actual interest label uses current resolved C01 sources, including C41, without mutating state or save.");
            var text=FindActive<TextMeshProUGUI>("Interest");var panel=FindActive<RectTransform>("Stats07");
            var glyph=CardGlyphCheck07(panel,text,expected,292);
            File.WriteAllText(Path.Combine(extra07.output,id+"-interest-glyphs.json"),JsonUtility.ToJson(glyph,true));
            Assert.That(glyph.errors,Is.Empty);Assert.That(text.textInfo.lineCount,Is.EqualTo(2),"Interest must remain two complete lines at 150%.");
            Rect inkScreen=ExtraBounds07(text.rectTransform),pause=ExtraBounds07((RectTransform)Button("Pause").transform);
            Assert.That(inkScreen.yMin,Is.GreaterThan(pause.yMax),"Interest text rectangle must finish above the fixed navigation buttons.");
        }
        [UnityTest] public IEnumerator PLAY_ART07_ShopInterestUsesLiveC01CopiesAndMatchesRealSettlement()
        {
            yield return ExtraBegin07();yield return CardSetFont07(150);
            var fixture=ShopBoundaryFixture();fixture.controllers.Clear();fixture.shop.offers.Clear();
            var mimic=BoundaryController(fixture,"C41");fixture.controllers.Add(mimic);AddBoundaryOffer(fixture,"interest-pig","controller","C01");
            yield return ExtraLoadFixture07(fixture,"interest-shop");yield return InterestCheck07(0,"interest-before-purchase");
            yield return ExtraPress07("Buy_interest-pig",true);yield return InterestCheck07(2,"interest-pig-plus-right-neighbor-copy");
            var pig=app.Session.State.controllers.Single(c=>c.definitionId=="C01");long sequence=app.Session.ActionSequence;
            var reordered=app.Send(new SessionCommand{kind=CommandKind.ReorderControllers,controllerIds=new[]{pig.instanceId,mimic.instanceId}});
            Assert.That(reordered.accepted,Is.True,reordered.error);Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence+1));
            yield return InterestCheck07(1,"interest-after-legal-reorder");
            // Cross-check the displayed rate against a fresh, real settlement, not the previous shop receipt.
            // Only the future debug target and dice fixture are prescribed; all commands are production transactions.
            var scene=DebugScene.FromState(app.Session.State);scene.target="1";scene.score="0";scene.remainingSubmissions=3;scene.rollsRemaining=3;
            scene.currentResults=null;scene.orderedLockedIds.Clear();scene.randomOverrides.Clear();
            foreach(var die in scene.dice)foreach(var face in die.faces){face.permanentPip=4;face.baseColor="red";}
            ApplyInformationScene07(scene);yield return ExtraIdle07();yield return ExtraPress07("Roll",true);
            foreach(var die in app.Session.State.dice)ClickDie(die.dieId);
            yield return ExtraPress07("Confirm",true);yield return ExtraPress07("Submit",true);Assert.That(app.Session.CanEndStage,Is.True);
            yield return ExtraPress07("EndStage",true);var receipt=app.Session.State.lastStageSettlement;
            Assert.That(receipt,Is.Not.Null);Assert.That(receipt.pigSourceCount,Is.EqualTo(1));
            Assert.That(receipt.interestPrincipal,Is.EqualTo(System.Math.Min(receipt.goldBefore,app.Config.maxInterestPrincipal)));
            long rate=1L+app.Config.Controller("C01").parameters.extraInterestPerTen;
            Assert.That(receipt.interest,Is.EqualTo(rate*(receipt.interestPrincipal/app.Config.interestDivisor)));
            File.WriteAllText(Path.Combine(extra07.output,"fresh-interest-settlement.json"),JsonUtility.ToJson(receipt,true));
            yield return ExtraPress07("SettlementContinue",true);yield return InterestCheck07(1,"interest-new-shop-after-settlement");
            yield return ExtraPress07("Controller_"+pig.instanceId);yield return ExtraPress07("SellController");yield return ExtraPress07("ConfirmSale",true);
            yield return InterestCheck07(0,"interest-after-selling-pig");ExtraFinish07();
        }
    }
}
