using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Verification;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [Serializable] private sealed class CardGlyph07
        {
            public string name,text,renderedText,font;public int expectedVisibleCharacters,textInfoCharacterCount,renderedCharacterCount;
            public float fontSize,lineHeight,ascent,descent,pointSize;public bool overflow,truncated,alphaVisible;
            public Rect textRect,ink;public List<string> errors=new List<string>();
        }
        [Serializable] private sealed class CardRead07
        {
            public string kind,instance,definition,rarity,enchantment;public Rect card,icon,badge,ticks;
            public List<CardGlyph07> labels=new List<CardGlyph07>();public List<string> errors=new List<string>();
        }
        [Serializable] private sealed class CardScreen07
        {
            public string id,scope,runtimeMvid,coreMvid,testMvid,sourceSnapshotSha,stateHash,saveSha;
            public int width,height,fontPercent;public float canvasScale;public List<CardRead07> cards=new List<CardRead07>();
        }
        private static Rect CardLocalRect07(RectTransform parent,RectTransform child)
        {
            var corners=new Vector3[4];child.GetWorldCorners(corners);var local=corners.Select(parent.InverseTransformPoint).ToArray();
            return Rect.MinMaxRect(local.Min(p=>p.x),-local.Max(p=>p.y),local.Max(p=>p.x),-local.Min(p=>p.y));
        }
        private static bool CardOverlap07(Rect a,Rect b)=>Math.Min(a.xMax,b.xMax)-Math.Max(a.xMin,b.xMin)>.25f&&Math.Min(a.yMax,b.yMax)-Math.Max(a.yMin,b.yMin)>.25f;
        private static string CardWithoutSpace07(string text)=>new string(text.Where(c=>!char.IsWhiteSpace(c)).ToArray());
        private static float CardPaperBottom07(RectTransform card)
        {
            var button=card.GetComponent<UnityEngine.UI.Button>();
            var paper=button!=null?button.targetGraphic as UnityEngine.UI.Image:card.GetComponent<UnityEngine.UI.Image>();
            Assert.That(paper,Is.Not.Null,"Card must expose its actual paper Image.");
            Assert.That(paper.sprite,Is.Not.Null,"Card paper sprite is required for its physical inset.");
            Assert.That(paper.type,Is.EqualTo(UnityEngine.UI.Image.Type.Sliced),"Paper border measurement expects the authored sliced surface.");
            // Art071 card/compact/panel masters: paper ends at source y=990/1024,
            // leaving 17 pixels below it in the 512px export. Nine-slicing preserves
            // this inset; use the rendered Image PPU, not the old 224px card height.
            float inset=17f/(paper.pixelsPerUnit*paper.pixelsPerUnitMultiplier);
            return CardLocalRect07(card,paper.rectTransform).yMax-inset;
        }
        private CardGlyph07 CardGlyphCheck07(RectTransform card,TextMeshProUGUI text,string expected,float paperBottom)
        {
            text.ForceMeshUpdate();var row=new CardGlyph07{name=text.name,text=text.text,font=text.font.name,fontSize=text.fontSize,
                lineHeight=text.font.faceInfo.lineHeight,ascent=text.font.faceInfo.ascentLine,descent=text.font.faceInfo.descentLine,pointSize=text.font.faceInfo.pointSize,
                textRect=CardLocalRect07(card,text.rectTransform),overflow=text.isTextOverflowing,truncated=text.isTextTruncated,
                expectedVisibleCharacters=CardWithoutSpace07(expected).Length,textInfoCharacterCount=text.textInfo.characterCount};
            float alpha=text.color.a*text.canvasRenderer.GetAlpha();foreach(var group in text.GetComponentsInParent<CanvasGroup>(false))alpha*=group.alpha;
            row.alphaVisible=alpha>.99f&&!text.canvasRenderer.cull;
            var ink=new List<Vector3>();var rendered=new List<char>();
            for(int i=0;i<text.textInfo.characterCount;i++)
            {
                var info=text.textInfo.characterInfo[i];if(!info.isVisible||char.IsWhiteSpace(info.character))continue;
                var mesh=text.textInfo.meshInfo[info.materialReferenceIndex];int index=info.vertexIndex;
                var vertices=Enumerable.Range(0,4).Select(v=>mesh.vertices[index+v]).ToArray();
                bool nondegenerate=vertices.Max(p=>p.x)-vertices.Min(p=>p.x)>.01f&&vertices.Max(p=>p.y)-vertices.Min(p=>p.y)>.01f;
                if(!nondegenerate||Enumerable.Range(0,4).All(v=>mesh.colors32[index+v].a==0))continue;
                rendered.Add(info.character);ink.AddRange(vertices.Select(p=>card.InverseTransformPoint(text.transform.TransformPoint(p))));
            }
            row.renderedCharacterCount=rendered.Count;row.renderedText=new string(rendered.ToArray());
            if(ink.Count>0)row.ink=Rect.MinMaxRect(ink.Min(p=>p.x),-ink.Max(p=>p.y),ink.Max(p=>p.x),-ink.Min(p=>p.y));
            if(row.text!=expected)row.errors.Add("Text differs from the current configured value: "+expected);
            if(row.renderedText!=CardWithoutSpace07(expected))row.errors.Add("Visible nonzero glyph quads differ from the complete current text.");
            if(row.renderedCharacterCount!=row.expectedVisibleCharacters||row.textInfoCharacterCount<row.expectedVisibleCharacters)row.errors.Add("Missing rendered characters (including blank Ellipsis lines).");
            if(row.overflow||row.truncated)row.errors.Add("TMP overflow/truncation.");
            if(!row.alphaVisible)row.errors.Add("Text is transparent or culled.");
            if(ink.Count>0&&(row.ink.xMin<-.25f||row.ink.xMax>card.rect.width+.25f||row.ink.yMin<-.25f||row.ink.yMax>paperBottom+.25f))row.errors.Add("Painted glyph lies outside the paper region.");
            if(ink.Count>0&&(row.ink.xMin<row.textRect.xMin-.25f||row.ink.xMax>row.textRect.xMax+.25f||row.ink.yMin<row.textRect.yMin-.25f||row.ink.yMax>row.textRect.yMax+.25f))row.errors.Add("Painted glyph exceeds its own text rectangle.");
            return row;
        }
        private CardRead07 CardCheck07(RectTransform card,ControllerInstance item,string kind,string footer=null)
        {
            var config=app.Config.Controller(item.definitionId);float bottom=CardPaperBottom07(card);
            var row=new CardRead07{kind=kind,instance=item.instanceId,definition=item.definitionId,rarity=config.rarity,enchantment=item.enchantmentId,card=new Rect(0,0,card.rect.width,card.rect.height)};
            string category=config.rarity=="rare"?"稀有":config.rarity=="uncommon"?"进阶":"常见";
            foreach(var pair in new[]{new[]{"RarityLabel",category},new[]{"Name",config.displayName},new[]{"State",footer},new[]{"Identity",item.instanceId}})
            {
                var child=card.Find(pair[0]);if(child==null){if(pair[0]=="RarityLabel"||(pair[0]=="Name"&&kind!="detail")||(pair[0]=="State"&&footer!=null)||(pair[0]=="Identity"&&kind=="detail"))row.errors.Add("Missing required "+pair[0]);continue;}
                if(pair[1]!=null)row.labels.Add(CardGlyphCheck07(card,child.GetComponent<TextMeshProUGUI>(),pair[1],bottom));
            }
            var ordinal=card.Find("Ordinal");if(ordinal!=null)
            {
                int index=app.Session.State.controllers.FindIndex(c=>c.instanceId==item.instanceId)+1;
                row.labels.Add(CardGlyphCheck07(card,ordinal.GetComponent<TextMeshProUGUI>(),index.ToString(),bottom));
            }
            var ticks=(RectTransform)card.Find("RarityTicks");if(ticks==null)row.errors.Add("Missing geometric rarity ticks.");else
            {
                row.ticks=CardLocalRect07(card,ticks);int expected=config.rarity=="rare"?3:config.rarity=="uncommon"?2:1;
                if(ticks.childCount!=expected)row.errors.Add("Wrong rarity tick count.");
                foreach(RectTransform tick in ticks)if(tick.rect.width<=0||tick.rect.height<=0)row.errors.Add("Invisible tick.");
            }
            var icon=(RectTransform)card.Find("Icon_"+item.definitionId);if(icon==null)row.errors.Add("Missing illustration.");else row.icon=CardLocalRect07(card,icon);
            var badge=(RectTransform)card.Find("EnchantFrame");if(item.enchantmentId!="none")
            {
                if(badge==null)row.errors.Add("Missing enchantment badge.");else
                {
                    row.badge=CardLocalRect07(card,badge);var face=(RectTransform)badge.Find("Badge");float size=kind=="detail"?40:card.rect.height>200?32:28;
                    if(face==null||Math.Abs(face.rect.width-size)>.01f||Math.Abs(face.rect.height-size)>.01f)row.errors.Add("Wrong enchantment badge size/resource.");
                }
            }
            foreach(var label in row.labels)
            {
                if(label.errors.Count>0)row.errors.AddRange(label.errors.Select(e=>label.name+": "+e));
                if(badge!=null&&CardOverlap07(label.ink,row.badge))row.errors.Add(label.name+" painted ink overlaps enchantment.");
                if(label.name!="Identity"&&CardOverlap07(label.ink,row.icon))row.errors.Add(label.name+" painted ink overlaps illustration.");
                if(label.name!="RarityLabel"&&label.name!="Ordinal"&&CardOverlap07(label.ink,row.ticks))row.errors.Add(label.name+" painted ink overlaps rarity ticks.");
            }
            for(int a=0;a<row.labels.Count;a++)for(int b=a+1;b<row.labels.Count;b++)if(CardOverlap07(row.labels[a].ink,row.labels[b].ink))row.errors.Add(row.labels[a].name+" painted ink overlaps "+row.labels[b].name);
            return row;
        }
        private string CardExpectedFooter07(ControllerInstance item,bool offer)
        {
            var sources=ControllerRules.ResolveSources(app.Session.State.controllers,app.Session.State.stage.started&&app.Session.State.phase!="Shop"?app.Session.State.stage.mimicBindings:null);
            var source=sources.FirstOrDefault(s=>s.emitterId==item.instanceId);
            if(offer)
            {
                if(item.definitionId=="C41")return "模拟右邻";
                if(item.definitionId=="C45")return string.IsNullOrEmpty(item.state.activeColor)?"购入定色":item.state.activeColor=="red"?"当前 红 ●":"当前 蓝 ◆";
                if(item.definitionId=="C51")return "当前 ×"+(item.state.turnoverUnits2/2m).ToString("0.#");
                if(item.definitionId=="C52")return "卖价 "+ControllerRules.SalePrice(item,app.Config);
                if(item.definitionId=="C53")return "随机倍率";
                return "技能详情";
            }
            if(item.definitionId=="C41")return source?.abilityId==null?"右邻失效":"→"+app.Config.Controller(source.abilityId).displayName;
            if(source?.abilityId=="C45")return source.state.activeColor=="red"?"当前红":"当前蓝";
            if(source?.abilityId=="C51")return "当前 ×"+(source.state.turnoverUnits2/2m).ToString("0.#");
            if(source?.abilityId=="C53")return "随机倍率";
            if(item.definitionId=="C52")return "售价 "+ControllerRules.SalePrice(item,app.Config);
            if(source?.abilityId=="C05")return app.Session.State.hand.c05UsedEmitterIds.Contains(item.instanceId)?"本手已用":"本手可用";
            return "查看条件";
        }
        private IEnumerator CardCapture07(string id,bool shop=false,bool detail=false)
        {
            yield return ExtraCapture07(id,detail?"S15":shop?"S10":"S05","Actual card glyph quads at current real viewport/font; valid isolated debug fixture.");
            var row=new CardScreen07{id=id,scope="Representative full HUD, compact HUD, shop full, detail, and owned mini cards; no claim of all catalog entries.",runtimeMvid=extra07.runtimeMvid,coreMvid=extra07.coreMvid,testMvid=extra07.testMvid,sourceSnapshotSha=extra07.sourceManifestSha256,
                stateHash=ExtraStateHash07(),saveSha=ExtraFileHash07(app.SaveLocation),width=Screen.width,height=Screen.height,fontPercent=Mathf.RoundToInt(UiKit.FontScale*100),canvasScale=app.canvas.scaleFactor};
            var state=app.Session.State;
            if(detail)
            {
                var card=FindActive<RectTransform>("DetailCard");string instance=card.Find("Identity").GetComponent<TextMeshProUGUI>().text;
                row.cards.Add(CardCheck07(card,state.controllers.Single(c=>c.instanceId==instance),"detail"));
                var title=FindActive<TextMeshProUGUI>("DialogTitle");var titleRow=CardGlyphCheck07((RectTransform)title.transform.parent,title,app.Config.Controller(state.controllers.Single(c=>c.instanceId==instance).definitionId).displayName,720);
                row.cards[0].labels.Add(titleRow);row.cards[0].errors.AddRange(titleRow.errors.Select(e=>"DialogTitle: "+e));
            }
            else
            {
                foreach(var item in state.controllers.Take(shop?4:10))
                {
                    var card=(RectTransform)Button("Controller_"+item.instanceId).transform;
                    row.cards.Add(CardCheck07(card,item,shop?"owned-mini":card.rect.height>200?"hud-full":"hud-compact",shop?null:CardExpectedFooter07(item,false)));
                }
                if(shop)foreach(var offer in state.shop.offers.Where(o=>o.category=="controller"))row.cards.Add(CardCheck07((RectTransform)Button("Offer_"+offer.offerId).transform,offer.controller,"shop-full",CardExpectedFooter07(offer.controller,true)));
            }
            File.WriteAllText(Path.Combine(extra07.output,id+"-glyphs.json"),JsonUtility.ToJson(row,true));
            Assert.That(row.cards,Is.Not.Empty);Assert.That(row.cards.SelectMany(c=>c.errors.Select(e=>c.kind+"/"+c.definition+": "+e)).ToArray(),Is.Empty,"See screenshot and -glyphs.json for actual missing/overlapping ink.");
        }
        private IEnumerator CardSetFont07(int font)
        {
            if(app.Session.State==null)yield return ExtraPress07("MenuSettings");else{yield return ExtraPress07("Pause");yield return ExtraPress07("Settings");}
            yield return ExtraPress07("SettingsTab2");for(int i=0;i<3&&Mathf.RoundToInt(UiKit.FontScale*100)!=font;i++)yield return ExtraPress07("TextScale");
            Assert.That(Mathf.RoundToInt(UiKit.FontScale*100),Is.EqualTo(font));yield return ExtraPress07("SettingsDone");
        }
        [UnityTest] public IEnumerator PLAY_ART07_CardReadability_ThreeFontsAtActual720Or1080()
        {
            Assert.That(Screen.width==1920&&Screen.height==1080||Screen.width==1280&&Screen.height==720,Is.True,"Run this test once at actual 1920x1080 and again at actual 1280x720; no fake viewport or skipped configuration.");
            int width=Screen.width,height=Screen.height;yield return ExtraBegin07();
            foreach(int font in new[]{100,125,150})
            {
                yield return CardSetFont07(font);if(app.Session.State==null)StartRun("70820");
                var scene=InformationScene07(Art07FixtureKind.GoldenSample);
                string[] ids={app.Config.controllers.First(c=>c.rarity=="common").id,app.Config.controllers.First(c=>c.rarity=="uncommon").id,app.Config.controllers.First(c=>c.rarity=="rare").id,"C41"};
                for(int i=0;i<4;i++){scene.controllers[i].definitionId=ids[i];scene.controllers[i].enchantmentId="slotless";scene.controllers[i].state=new ControllerDynamicState();}
                ApplyInformationScene07(scene);yield return CardCapture07("font"+font+"-hud-rarities");
                scene=InformationScene07(Art07FixtureKind.GoldenSample);ids=new[]{"C41","C45","C51","C52"};
                for(int i=0;i<4;i++){scene.controllers[i].definitionId=ids[i];scene.controllers[i].enchantmentId="slotless";scene.controllers[i].state=new ControllerDynamicState{activeColor=ids[i]=="C45"?"blue":null,fundAccrual=ids[i]=="C52"?6:0};}
                ApplyInformationScene07(scene);yield return CardCapture07("font"+font+"-hud-current-states");
                yield return ExtraPress07("Controller_"+app.Session.State.controllers[0].instanceId);yield return CardCapture07("font"+font+"-detail",detail:true);yield return ExtraPress07("CloseDialog");
                scene=InformationScene07(Art07FixtureKind.MaximumPool);ApplyInformationScene07(scene);yield return CardCapture07("font"+font+"-ten-compact-cards");
                var shop=ShopBoundaryFixture();shop.controllers.Clear();shop.shop.offers.Clear();shop.controllerOfferPurchases=3;
                foreach(string id in ids){var item=BoundaryController(shop,id,"slotless");if(id=="C45")item.state.activeColor="blue";if(id=="C52")item.state.fundAccrual=6;shop.controllers.Add(item);}
                string[] quotes={"C01","C02","C45","C41","C51","C52"};
                foreach(string id in quotes){AddBoundaryOffer(shop,"read-"+id,"controller",id);var offer=shop.shop.offers.Last();offer.controller.enchantmentId="slotless";}
                yield return ExtraLoadFixture07(shop,"cards-font"+font);Assert.That(Mathf.RoundToInt(UiKit.FontScale*100),Is.EqualTo(font));yield return CardCapture07("font"+font+"-shop-six-plus-owned",shop:true);
                Assert.That(Screen.width,Is.EqualTo(width));Assert.That(Screen.height,Is.EqualTo(height));
            }
            ExtraFinish07();TestContext.WriteLine("Three fonts measured at "+width+"x"+height+" only. The other resolution remains a separate required run.");
        }
    }
}
