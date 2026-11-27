using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UObj = UnityEngine.Object;

namespace DiceCraft.Tests
{
    /// <summary>
    /// V1.1.1 task W9b-ui (docs/tasks/P0-V111.md Q8; docs/specs/v111/W9A_NOTES.md §5.4): the six layout faults the W9a
    /// review found, checked on the real screens at 100 % and 150 % text, and saved for review to
    /// artifacts/cache/v111/w9b/&lt;width&gt;x&lt;height&gt;/ (run at 1920x1080 and at 1280x720):
    /// C155's shelf text without a line of one character; the compact value badges and the key ring's count clear of their
    /// icons; the broken glass leading the post-score summary; the 可撤回 tag above the die and a selection that stands out.
    /// </summary>
    public sealed partial class GameplayPlayModeTests
    {
        private static string W9bOutput
        {
            get
            {
                string dir=Path.GetFullPath(Path.Combine(Application.dataPath,"..","..","artifacts","cache","v111","w9b",Screen.width+"x"+Screen.height));
                Directory.CreateDirectory(dir);return dir;
            }
        }
        private IEnumerator W9bShot(string name,bool redraw=true)
        {
            bool clean=GameApp.CleanCapture11;GameApp.CleanCapture11=true;
            try
            {
                if(redraw)app.Refresh();
                Canvas.ForceUpdateCanvases();
                yield return new WaitForEndOfFrame();
                var texture=ScreenCapture.CaptureScreenshotAsTexture();
                try{File.WriteAllBytes(Path.Combine(W9bOutput,name+".png"),texture.EncodeToPNG());}
                finally{UObj.Destroy(texture);}
            }
            finally{GameApp.CleanCapture11=clean;}
            if(redraw)app.Refresh();
            Canvas.ForceUpdateCanvases();
        }
        private static float W9bArea(Rect r)=>Mathf.Max(0,r.width)*Mathf.Max(0,r.height);
        private static float W9bCovered(Rect cover,Rect target)
        {
            float w=Mathf.Min(cover.xMax,target.xMax)-Mathf.Max(cover.xMin,target.xMin),h=Mathf.Min(cover.yMax,target.yMax)-Mathf.Max(cover.yMin,target.yMin);
            return w<=0||h<=0||W9bArea(target)<=0?0:w*h/W9bArea(target);
        }
        /// <summary>A label is whole: not truncated, every character drawn, all glyphs inside <paramref name="frame"/>.</summary>
        private static void W9bWhole(TextMeshProUGUI label,Rect frame,string at,List<string> failures)
        {
            label.ForceMeshUpdate();
            int expected=label.text.Count(ch=>!char.IsWhiteSpace(ch));
            var glyphs=W11bGlyphRects(label);
            if(label.isTextTruncated)failures.Add(at+": '"+label.text+"' truncated");
            if(glyphs.Count!=expected)failures.Add(at+": '"+label.text+"' draws "+glyphs.Count+" of "+expected+" characters");
            var outside=glyphs.FirstOrDefault(g=>g.xMin<frame.xMin-1.5f||g.xMax>frame.xMax+1.5f||g.yMin<frame.yMin-1.5f||g.yMax>frame.yMax+1.5f);
            if(outside.width>0)failures.Add(at+": '"+label.text+"' glyph "+outside+" outside "+frame);
        }

        /// <summary>Q8 ① and ②: on compact cards the value badge covers at most 15 % of the icon (it sat on the icon's top
        /// and covered about half of it), its text whole and clear of the name; the key ring's count covers at most 6 % of
        /// the item's picture, stays whole, and touches neither the next slot nor the 查看骰子 button.</summary>
        private void W9bCheckIcons(string scope,string[] required,List<string> failures,List<string> report)
        {
            Canvas.ForceUpdateCanvases();
            var s=app.Session.State;int badges=0;
            foreach(var c in s.controllers)
            {
                var card=Button("Controller_"+c.instanceId).transform;string at=scope+" "+c.definitionId;
                var chip=card.Find("ValueBadge13") as RectTransform;var icon=card.Find("Icon_"+c.definitionId) as RectTransform;
                bool shown=chip!=null&&chip.gameObject.activeInHierarchy;
                if(!shown){if(required.Contains(c.definitionId))failures.Add(at+": no value badge");continue;}
                if(icon==null||!icon.gameObject.activeInHierarchy){failures.Add(at+": no compact icon");continue;}
                badges++;
                var chipRect=V06ScreenRect(chip);float covered=W9bCovered(chipRect,V06ScreenRect(icon));
                var state=card.Find("State").GetComponent<TextMeshProUGUI>();
                report.Add(at+" '"+state.text+"' covers "+(covered*100).ToString("0.0",CultureInfo.InvariantCulture)+"% of the icon, text "+state.fontSize.ToString("0.0",CultureInfo.InvariantCulture));
                if(covered>.15f)failures.Add(at+": the value badge '"+state.text+"' covers "+(covered*100).ToString("0",CultureInfo.InvariantCulture)+"% of the icon");
                if(state.fontSize<19.49f)failures.Add(at+": the value badge text is "+state.fontSize+" units, below the 19.5 floor");
                W9bWhole(state,chipRect,at+" State",failures);
                var cardRect=V06ScreenRect((RectTransform)card);
                if(chipRect.xMin<cardRect.xMin-1||chipRect.xMax>cardRect.xMax+1||chipRect.yMin<cardRect.yMin-1||chipRect.yMax>cardRect.yMax+1)failures.Add(at+": the value badge "+chipRect+" leaves the card "+cardRect);
                var name=card.Find("Name")?.GetComponent<TextMeshProUGUI>();
                if(name!=null&&name.gameObject.activeInHierarchy&&W11bGlyphRects(name).Any(g=>W13Overlap(g,chipRect)))failures.Add(at+": the name runs under the value badge");
            }
            if(badges<required.Length)failures.Add(scope+": only "+badges+" value badges");
            var ring=s.inventory.FirstOrDefault(i=>i.contentId=="I13");
            if(ring!=null)W9bCheckRingCount(scope,"Item_"+ring.instanceId,s.inventory.Where(i=>i!=ring).Select(i=>"Item_"+i.instanceId).Concat(new[]{"ItemEmpty"+s.inventory.Count,"ViewDice"}),failures,report);
        }
        /// <summary>The key ring's count on its item button: whole, over at most 6 % of the picture, clear of its neighbours.</summary>
        private void W9bCheckRingCount(string scope,string button,IEnumerable<string> neighbours,List<string> failures,List<string> report)
        {
            var item=Button(button).transform as RectTransform;
            var uses=item.Find("Uses111") as RectTransform;var picture=item.Find("Icon_I13") as RectTransform;
            if(uses==null||picture==null){failures.Add(scope+": the key ring has no count or no picture");return;}
            var usesRect=V06ScreenRect(uses);float under=W9bCovered(usesRect,V06ScreenRect(picture));
            report.Add(scope+" key ring count covers "+(under*100).ToString("0.0",CultureInfo.InvariantCulture)+"% of the picture");
            if(under>.06f)failures.Add(scope+": the key ring's count covers "+(under*100).ToString("0",CultureInfo.InvariantCulture)+"% of its picture");
            W9bWhole(uses.Find("Count").GetComponent<TextMeshProUGUI>(),usesRect,scope+" key ring count",failures);
            foreach(string other in neighbours)
            {
                var r=app.canvas.GetComponentsInChildren<RectTransform>(false).FirstOrDefault(t=>t.name==other&&t.gameObject.activeInHierarchy);
                if(r!=null&&W13Overlap(usesRect,V06ScreenRect(r)))failures.Add(scope+": the key ring's count runs onto "+other);
            }
        }
        private DebugScene W9bTenCards()
        {
            var scene=W11Scene(new[]{"C154","C155","C156","C24","C08","C46","C50","C55","C45","C41"},new[]{4,4,6,2,4},"bbbrb",2,true,items:0);
            scene.controllers[8].state.activeColor="red";
            int serial=app.Session.State.nextItemSerial+scene.controllers.Count;
            scene.inventory.Add(new OwnedItem{instanceId="ITEM_"+serial.ToString("D5"),contentId="I13",usesRemaining=3});
            scene.inventory.Add(new OwnedItem{instanceId="ITEM_"+(serial+1).ToString("D5"),contentId="I04"});
            return scene;
        }

        [UnityTest,Timeout(900000)] public IEnumerator PLAY_W9B_CompactBadgesAndTheKeyRingCountLeaveTheIconsClear()
        {
            var failures=new List<string>();var report=new List<string>();
            yield return WithStyles22(()=>Run());
            File.WriteAllLines(Path.Combine(W9bOutput,"w9b-icons.txt"),report.Concat(failures.Select(f=>"FAIL "+f)));
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(40)));

            IEnumerator Run()
            {
                try
                {
                    StartRun("90911");
                    foreach(int font in new[]{100,150})
                    {
                        W11TextScale(font);string tag=font==100?"":"-text150";
                        foreach(string theme in AllStyles9a)
                        {
                            app.ApplyTheme11(theme);Assert.That(app.ThemeId11,Is.EqualTo(theme));
                            // Ten cards: the play row's mini cards at both text sizes, the key ring among the items.
                            W11Apply(W9bTenCards());yield return W11Settle();
                            W9bCheckIcons(theme+tag+" ten cards",new[]{"C154","C156"},failures,report);
                            yield return W9bShot("play-row-"+theme+tag);
                            if(font==100)continue;
                            // Five cards at 150 %: the tall compact (text) cards.
                            var five=W11Scene(new[]{"C154","C156","C55","C45","C41"},new[]{4,4,6,2,4},"bbbrb",2,true,items:0);
                            five.controllers[3].state.activeColor="red";
                            W11Apply(five);yield return W11Settle();
                            W9bCheckIcons(theme+tag+" five cards",new[]{"C154","C156"},failures,report);
                            yield return W9bShot("play-five-"+theme+tag);
                        }
                    }
                }
                finally{W11TextScale(100);}
            }
        }

        /// <summary>Q8 ③: after a submission in which a glass die broke, the post-score summary of the formula row starts
        /// with the break, whole ("后置：1 颗玻璃骰碎了（提交栏格数不变）"), before the run record; at 100 % and 150 % text.</summary>
        [UnityTest] public IEnumerator PLAY_W9B_BrokenGlassLeadsTheSummaryAndIsNeverCut()
        {
            StartRun("90912");var report=new List<string>();
            const string lead="后置：1 颗玻璃骰碎了（提交栏格数不变）";
            try
            {
                foreach(int font in new[]{100,150})
                {
                    W11TextScale(font);string tag=font==100?"":"-text150";
                    var glass=W11Scene(new string[0],new[]{4,4,4,2,6},"bbbbr",3,true);
                    glass.dice[0].isGlass=true;glass.dice[1].isGlass=true;
                    glass.randomOverrides.Add(new DebugRandomOverride{kind="Glass",sourceId=glass.dice[0].dieId,value="break"});
                    glass.randomOverrides.Add(new DebugRandomOverride{kind="Glass",sourceId=glass.dice[1].dieId,value="keep"});
                    W11Apply(glass);yield return W11Settle();
                    Click("Submit");app.SkipVisual();yield return WaitForIdle();yield return W11Settle();
                    Assert.That(app.Session.State.lastEffects.Count(e=>e.kind=="DieBroken"),Is.EqualTo(1),tag+": one glass die broke.");
                    Assert.That(app.Session.State.lastEffects.Any(e=>e.kind=="HandRecordBroken"),Is.True,tag+": the first hand sets the run record.");
                    var label=FindActive<TextMeshProUGUI>("StableResultFactors07");label.ForceMeshUpdate();
                    report.Add("font"+font+": '"+label.text+"' in "+label.rectTransform.rect.width.ToString("0",CultureInfo.InvariantCulture)+" units, truncated "+label.isTextTruncated);
                    Assert.That(label.text,Does.StartWith(lead),tag+": the break leads the summary.");
                    Assert.That(label.text.IndexOf("本局最高单手",StringComparison.Ordinal),Is.GreaterThan(lead.Length),tag+": the run record follows the break.");
                    var info=label.textInfo;
                    for(int i=0;i<lead.Length;i++)
                    {
                        if(char.IsWhiteSpace(lead[i]))continue;
                        Assert.That(i<info.characterCount&&info.characterInfo[i].isVisible&&info.characterInfo[i].character==lead[i],Is.True,
                            tag+": '"+lead[i]+"' (character "+i+" of the break) is not drawn: '"+label.text+"'");
                    }
                    W11bAssertChipRow("font"+font+"-glass-after",report);
                    yield return W9bShot("glass-after"+tag);
                }
            }
            finally{W11TextScale(100);File.WriteAllLines(Path.Combine(W9bOutput,"w9b-glass.txt"),report);}
        }

        /// <summary>Q8 ④ and ⑤: after the key ring's 使用 the 可撤回 tags sit above the dice (in the slot's top band, not on the
        /// die), whole; a selected locked die has a selection at least twice as thick as the slot's own ring, with a halo
        /// outside it; at 100 % and 150 % text.</summary>
        [UnityTest] public IEnumerator PLAY_W9B_UnlockTagsAboveTheDiceAndTheSelectionStandsOut()
        {
            StartRun("90913");var failures=new List<string>();
            try
            {
                foreach(int font in new[]{100,150})
                {
                    W11TextScale(font);string tag=font==100?"":"-text150";
                    W11Apply(W7bUnlockScene(new string[0],3,true));yield return W11Settle();
                    var locked=app.Session.State.hand.orderedLockedIds.ToArray();string ring=W7bKeyRing.instanceId;
                    Click("Item_"+ring);Click("Use");Assert.That(app.UnlockArmed111,Is.True);
                    Canvas.ForceUpdateCanvases();
                    foreach(string id in locked)
                    {
                        string at=tag+" "+id;
                        var chip=FindActive<RectTransform>("UnlockTargetTag111_"+id);var die=FindActive<RectTransform>("Die_"+id);
                        var chipRect=V06ScreenRect(chip);var dieRect=V06ScreenRect(die);
                        // Screen space: y grows upwards, so the tag is above the die when its foot is not below the die's top.
                        if(chipRect.yMin<dieRect.yMax-1)failures.Add(at+": the 可撤回 tag "+chipRect+" reaches into the die "+dieRect);
                        W9bWhole(chip.Find("Text").GetComponent<TextMeshProUGUI>(),chipRect,at+" 可撤回",failures);
                    }
                    yield return W9bShot("unlock-armed"+tag);
                    ClickDie(locked[1]);Canvas.ForceUpdateCanvases();
                    Assert.That(app.UnlockPick111,Is.EqualTo(locked[1]));
                    var pick=FindActive<UnityEngine.UI.Image>("UnlockPick111_"+locked[1]);var halo=FindActive<UnityEngine.UI.Image>("UnlockPickGlow111_"+locked[1]);
                    var slot=FindActive<UnityEngine.UI.Image>("Slot1");
                    float Edge(UnityEngine.UI.Image i)=>i.sprite==null?0:i.sprite.border.x/(i.pixelsPerUnit*i.pixelsPerUnitMultiplier);
                    if(Edge(pick)<1.8f*Edge(slot))failures.Add(tag+": the selection ring ("+Edge(pick)+" units) is not clearly thicker than the slot ring ("+Edge(slot)+")");
                    Rect Design(UnityEngine.UI.Image i){var r=i.rectTransform;return new Rect(r.anchoredPosition.x,-r.anchoredPosition.y,r.sizeDelta.x,r.sizeDelta.y);}
                    Rect s=Design(slot),p=Design(pick),h=Design(halo);
                    if(p.xMin>s.xMin-4||p.yMin>s.yMin-4||p.xMax<s.xMax+4||p.yMax<s.yMax+4)failures.Add(tag+": the selection ring "+p+" does not stand clear of the slot "+s);
                    if(h.xMin>p.xMin-3||h.yMin>p.yMin-3||h.xMax<p.xMax+3||h.yMax<p.yMax+3)failures.Add(tag+": the halo "+h+" is not outside the ring "+p);
                    if(halo.GetComponent<Art072Pulse>()==null)failures.Add(tag+": the halo does not pulse like the pool's selection");
                    yield return W9bShot("unlock-pick"+tag);
                    Click("UnlockCancel111");Canvas.ForceUpdateCanvases();
                }
            }
            finally{W11TextScale(100);}
            Assert.That(failures,Is.Empty,string.Join("\n",failures));
        }

        /// <summary>Q8 (fault): C155's short text broke as "……×2； / 再 / 含两颗1点改为×4。" on four styles' shelves. Every shelf
        /// card's short text in every style, at 100 % and 150 % text, has no line of a single character; the shelves and
        /// C155's detail are saved for review.</summary>
        [UnityTest,Timeout(900000)] public IEnumerator PLAY_W9B_ShelfTextsHaveNoLineOfOneCharacter()
        {
            var failures=new List<string>();var report=new List<string>();
            yield return WithStyles22(()=>Run());
            File.WriteAllLines(Path.Combine(W9bOutput,"w9b-shelf-text.txt"),report.Concat(failures.Select(f=>"FAIL "+f)));
            Assert.That(failures,Is.Empty,string.Join("\n",failures.Take(40)));

            IEnumerator Run()
            {
                try
                {
                    StartRun("90914");
                    var fixture=W9aShop(new[]{"C155","C156"},new[]{"I13","I04"},new[]{"C154","C24","C155","C39","C156"});
                    string ring=BoundaryItem(fixture,"I13");fixture.inventory.Single(i=>i.instanceId==ring).usesRemaining=3;
                    yield return W14Load(fixture);
                    foreach(int font in new[]{100,150})
                    {
                        W11TextScale(font);string tag=font==100?"":"-text150";
                        foreach(string theme in AllStyles9a)
                        {
                            app.ApplyTheme11(theme);yield return Art073Settle(.3f);Canvas.ForceUpdateCanvases();
                            var cards=app.canvas.GetComponentsInChildren<UnityEngine.UI.Button>(false).Where(b=>b.name.StartsWith("Offer_",StringComparison.Ordinal)&&b.gameObject.activeInHierarchy).ToArray();
                            if(!cards.Any(b=>b.name=="Offer_w9a-card-C155"))failures.Add(theme+tag+": C155 is not on the shelf");
                            foreach(var card in cards)
                            {
                                var body=card.transform.Find("ShortText11")?.GetComponent<TextMeshProUGUI>();
                                if(body==null||!body.gameObject.activeInHierarchy)continue;
                                body.ForceMeshUpdate();var info=body.textInfo;var lines=new List<string>();
                                for(int k=0;k<info.lineCount;k++)
                                {
                                    var line=info.lineInfo[k];var text=new string(Enumerable.Range(line.firstCharacterIndex,Math.Max(0,line.lastCharacterIndex-line.firstCharacterIndex+1))
                                        .Where(i=>i<info.characterCount).Select(i=>info.characterInfo[i].character).ToArray()).Trim();
                                    lines.Add(text);
                                }
                                string shown=string.Join(" / ",lines);
                                if(card.name=="Offer_w9a-card-C155")report.Add(theme+tag+" C155: "+shown);
                                if(lines.Count>1&&lines.Any(l=>l.Count(ch=>!char.IsPunctuation(ch)&&!char.IsWhiteSpace(ch))<2))
                                    failures.Add(theme+tag+" "+card.name+": a line of one character: "+shown);
                                if(body.isTextTruncated)failures.Add(theme+tag+" "+card.name+": truncated");
                            }
                            // Q8 ②: the key ring's count in the build row of the shop too.
                            if(HasActive("ShopItem_"+ring))
                                W9bCheckRingCount(theme+tag+" shop",
                                    "ShopItem_"+ring,app.Session.State.inventory.Where(i=>i.instanceId!=ring).Select(i=>"ShopItem_"+i.instanceId).Concat(Enumerable.Range(0,5).Select(i=>"OwnedItemEmpty"+i)),failures,report);
                            else failures.Add(theme+tag+": the key ring is not in the shop's build row");
                            yield return W9bShot("shop-"+theme+tag);
                            if(!HasActive("Offer_w9a-card-C155"))continue;
                            yield return InspectOfferCard("w9a-card-C155");yield return Art073Settle(.3f);
                            yield return W9bShot("detail-C155-"+theme+tag,false);
                            if(HasActive("CloseDialog"))Click("CloseDialog");yield return Art073Settle(.2f);
                        }
                    }
                }
                finally{W11TextScale(100);}
            }
        }
    }
}
