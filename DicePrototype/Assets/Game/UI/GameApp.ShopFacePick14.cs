using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    // V1.1 task W14 (SHOP_LAYOUT 5 and 7): die names by their traits, and face upgrades chosen on the table. The flow is a
    // view state only: one purchase command is sent at the confirmation (S3); choosing and cancelling never touch the
    // session, the save or a random number.
    public sealed partial class GameApp
    {
        // ================================================================ die names (SHOP_LAYOUT 5.1)
        /// <summary>A die's title ("红面多 · 大点"), its subtitle ("点数和 24") and the 同款 tag of the second of two
        /// identical candidates.</summary>
        public struct DieName14 { public string title,subtitle; public bool same; }
        private static readonly string[] ColourWords14={"全蓝","蓝五面","蓝面多","红蓝各半","红面多","红五面","全红"};
        private static readonly string[] CountWords14={"","一","两","三","四","五","六"};
        /// <summary>The words of a die in priority order: special (玻璃, 金面 N), colour (by the number of red faces), size
        /// (大点 / 小点, the new-die price thresholds), repeat (三个 6: the largest group of equal pips, the higher pip on a
        /// tie, from the price's repeat threshold).</summary>
        public static List<string> DieWords14(DieInstance die,GenerationConfig g,out string repeat)
        {
            var words=new List<string>();repeat=null;
            if(die==null||die.faces==null)return words;
            if(die.isGlass)words.Add("玻璃");
            int gold=die.faces.Count(f=>f.isGold);if(gold>0)words.Add("金面 "+gold);
            int red=Mathf.Clamp(die.faces.Count(f=>f.baseColor=="red"),0,6);words.Add(ColourWords14[red]);
            int sum=die.faces.Sum(f=>f.permanentPip);
            if(sum>=g.highSumThreshold)words.Add("大点");else if(sum<=g.smallSumThreshold)words.Add("小点");
            var top=die.faces.GroupBy(f=>f.permanentPip).OrderByDescending(x=>x.Count()).ThenByDescending(x=>x.Key).First();
            if(top.Count()>=g.maxRepeatThreshold){repeat=CountWords14[Mathf.Clamp(top.Count(),0,6)]+"个 "+top.Key.ToString(CultureInfo.InvariantCulture);words.Add(repeat);}
            return words;
        }
        /// <summary>One die's name on its own: the first two words, and 点数和 N.</summary>
        public static DieName14 DieTitle14(DieInstance die,GenerationConfig g)
        {
            var words=DieWords14(die,g,out _);
            return new DieName14{title=string.Join(" · ",words.Take(2)),subtitle="点数和 "+(die?.faces?.Sum(f=>f.permanentPip)??0).ToString(CultureInfo.InvariantCulture)};
        }
        private static string FaceKey14(FaceState f)=>f.permanentPip+"|"+f.baseColor+"|"+f.isGold+"|"+f.texture+"|"+f.baseWeight;
        private static bool SameDie14(DieInstance a,DieInstance b)=>a.isGlass==b.isGlass&&a.faces.Select(FaceKey14).OrderBy(x=>x,StringComparer.Ordinal).SequenceEqual(b.faces.Select(FaceKey14).OrderBy(x=>x,StringComparer.Ordinal));
        private static string PipList14(DieInstance die,string colour)=>string.Join(" ",die.faces.Where(f=>f.baseColor==colour).Select(f=>f.permanentPip).OrderByDescending(p=>p).Select(p=>p.ToString(CultureInfo.InvariantCulture)));
        /// <summary>
        /// The names of the dice on sale together (SHOP_LAYOUT 5.1): when two share a title, each adds its next unused word
        /// (the repeat, then "和 N"); dice with the same six faces are equal and the second is tagged 同款; otherwise each
        /// adds its red faces' sum ("红 12"), and if they still share it their subtitles list the pips by colour.
        /// </summary>
        public static DieName14[] DieTitles14(DieInstance[] dice,GenerationConfig g)
        {
            var names=new DieName14[dice.Length];var titles=new List<string>[dice.Length];var extra=new List<string>[dice.Length];
            for(int i=0;i<dice.Length;i++)
            {
                var words=DieWords14(dice[i],g,out string repeat);titles[i]=words.Take(2).ToList();
                int sum=dice[i].faces.Sum(f=>f.permanentPip);
                extra[i]=new List<string>();if(repeat!=null)extra[i].Add(repeat);extra[i].Add("和 "+sum.ToString(CultureInfo.InvariantCulture));
                names[i]=new DieName14{subtitle="点数和 "+sum.ToString(CultureInfo.InvariantCulture)};
            }
            string Title(int i)=>string.Join(" · ",titles[i]);
            IEnumerable<int[]> Ties()=>Enumerable.Range(0,dice.Length).GroupBy(Title,StringComparer.Ordinal).Where(x=>x.Count()>1).Select(x=>x.ToArray()).ToArray();
            foreach(var tie in Ties())foreach(int i in tie){string next=extra[i].FirstOrDefault(w=>!titles[i].Contains(w));if(next!=null)titles[i].Add(next);}
            foreach(var tie in Ties())
            {
                // Identical candidates: the later ones are 同款 (buying either is the same).
                for(int a=0;a<tie.Length;a++)for(int b=0;b<a;b++)if(SameDie14(dice[tie[a]],dice[tie[b]])){names[tie[a]].same=true;break;}
                var different=tie.Where(i=>!names[i].same).ToArray();
                if(different.Length<2)continue;
                foreach(int i in different)
                {
                    int red=dice[i].faces.Count(f=>f.baseColor=="red");
                    if(red>0&&red<6)titles[i].Add("红 "+dice[i].faces.Where(f=>f.baseColor=="red").Sum(f=>f.permanentPip).ToString(CultureInfo.InvariantCulture));
                }
            }
            foreach(var tie in Ties())
            {
                // Only dice that differ and still share the title list their pips; a 同款 pair keeps 点数和 N.
                var different=tie.Where(i=>!names[i].same).ToArray();
                if(different.Length<2)continue;
                foreach(int i in different)
                {
                    string red=PipList14(dice[i],"red"),blue=PipList14(dice[i],"blue");
                    names[i].subtitle=string.Join(" · ",new[]{red.Length>0?"红 "+red:null,blue.Length>0?"蓝 "+blue:null}.Where(x=>x!=null));
                }
            }
            for(int i=0;i<dice.Length;i++)names[i].title=Title(i);
            return names;
        }
        private DieName14[] DieTitles14(DieInstance[] dice)=>DieTitles14(dice,Config.generation);

        // ================================================================ face pick on the table (SHOP_LAYOUT 7)
        // v1.1.1 (W7b, RULES_DELTA 7.1 and 9.2): a per-face upgrade takes one or two faces of the same die (the limit is
        // the session's MaxSelectableFaces), and the same flow serves a self-selected upgrade, which has no offer
        // (custom: the upgrade id stands in contentId and the confirmation sends CustomDieUpgrade).
        private sealed class FacePick14
        {
            public string offerId,contentId,dieId; public bool whole,custom,adding; public int maxFaces=1;
            /// <summary>The chosen faces in the order they were chosen (at most maxFaces).</summary>
            public readonly List<string> faces=new List<string>();
            /// <summary>The face chosen last (the one the big die turns to), or null.</summary>
            public string faceId=>faces.Count==0?null:faces[faces.Count-1];
        }
        private FacePick14 facePick14;
        public bool FacePickActive14=>facePick14!=null;
        /// <summary>S0 browsing, S1 choosing a die, S2 choosing a face (also while a second face is being added), S3
        /// confirming (SHOP_LAYOUT 7.1).</summary>
        public string FacePickStep14=>facePick14==null?"S0":facePick14.dieId==null?"S1":!facePick14.whole&&(facePick14.faces.Count==0||facePick14.adding)?"S2":"S3";
        public string FacePickDie14=>facePick14?.dieId;
        public string FacePickFace14=>facePick14?.faceId;
        /// <summary>The chosen faces in face id order, as the purchase command will carry them.</summary>
        public string[] FacePickFaces14=>facePick14==null?Array.Empty<string>():FaceArray14(facePick14);
        /// <summary>True while the flow is a self-selected upgrade (no shelf offer).</summary>
        public bool FacePickCustom14=>facePick14?.custom==true;
        private static string[] FaceArray14(FacePick14 pick)=>pick.whole?Array.Empty<string>():pick.faces.OrderBy(f=>f,StringComparer.Ordinal).ToArray();
        // A self-selected upgrade has no offer of the shop: the panel reads this stand-in (id and category only); its
        // price, legality and the command come from the session's self-selected queries.
        private ShopOffer customOffer14;
        private ShopOffer FacePickOffer14=>facePick14==null?null:facePick14.custom?customOffer14:ViewState?.shop?.offers.FirstOrDefault(o=>o.offerId==facePick14.offerId);
        /// <summary>The live price of the picked good: the offer's net price, or the self-selected price.</summary>
        private long FacePickPrice14(ShopOffer offer)=>facePick14?.custom==true?Session.GetCustomDieUpgradePrice(offer.contentId):Session.GetOfferNetPrice(offer);
        private int MaxFaces14(string contentId)=>contentId!=null&&contentId.StartsWith("U",StringComparison.Ordinal)?Session.MaxSelectableFaces(contentId):0;
        /// <summary>S1: a face upgrade or 删除骰子 of the shelf starts the flow from its price button or its card.</summary>
        private void BeginFacePick14(ShopOffer offer)
        {
            if(offer==null||offer.sold||IsBusy||Session.HasPendingSave||ViewState?.phase!="Shop")return;
            CloseModal();
            int max=MaxFaces14(offer.contentId);
            facePick14=new FacePick14{offerId=offer.offerId,contentId=offer.contentId,whole=max<=0,maxFaces=Math.Max(1,max)};
            Refresh();
        }
        /// <summary>S1 of a self-selected upgrade (RULES_DELTA 9.2): the same flow without an offer.</summary>
        private void BeginCustomFacePick14(string upgradeId)
        {
            if(string.IsNullOrEmpty(upgradeId)||IsBusy||Session.HasPendingSave||ViewState?.phase!="Shop")return;
            CloseModal();
            int max=MaxFaces14(upgradeId);
            customOffer14=new ShopOffer{offerId="",contentId=upgradeId,category="die_upgrade",groupId=""};
            facePick14=new FacePick14{offerId="",contentId=upgradeId,custom=true,whole=max<=0,maxFaces=Math.Max(1,max)};
            Refresh();
        }
        /// <summary>Back to S0 without any command.</summary>
        public void CancelFacePick14()
        {
            if(facePick14==null)return;
            facePick14=null;
            if(!inMenu&&ViewState?.phase=="Shop")Refresh();
        }
        private bool FacePickStillValid14()
        {
            var offer=FacePickOffer14;
            if(offer==null||offer.sold||ViewState.phase!="Shop")return false;
            if(facePick14.dieId!=null&&!ViewState.dice.Any(d=>d.dieId==facePick14.dieId&&d.lifetime=="permanent")){facePick14.dieId=null;facePick14.faces.Clear();facePick14.adding=false;}
            return true;
        }
        private void FacePickChooseDie14(string dieId)
        {
            if(facePick14==null||IsBusy)return;
            // Another die (or the same one again) starts its faces afresh (W7b: 换骰子时清空已选面).
            facePick14.dieId=dieId;facePick14.faces.Clear();facePick14.adding=false;
            Refresh();
        }
        /// <summary>A click on a face. Choosing the first face goes to S3 as before. While a second face is being added
        /// (再选一个面, S2 again) another face joins the first; in S3 a click on a face of the big die replaces the
        /// choice with that one face.</summary>
        private void FacePickChooseFace14(string faceId)
        {
            var pick=facePick14;
            if(pick==null||pick.dieId==null||pick.whole||IsBusy||string.IsNullOrEmpty(faceId))return;
            if(pick.adding)
            {
                // The face already chosen keeps the single choice (the same as 只改一面).
                if(!pick.faces.Contains(faceId)&&pick.faces.Count<pick.maxFaces)pick.faces.Add(faceId);
                pick.adding=false;
            }
            else{pick.faces.Clear();pick.faces.Add(faceId);}
            Refresh();
        }
        /// <summary>S3 with one face of an upgrade that takes two: back to the six faces to add the second.</summary>
        private void FacePickAddFace14()
        {
            var pick=facePick14;
            if(pick==null||pick.whole||pick.dieId==null||pick.faces.Count==0||pick.faces.Count>=pick.maxFaces||IsBusy)return;
            pick.adding=true;Refresh();
        }
        private void FacePickConfirm14()
        {
            var offer=FacePickOffer14;if(offer==null||IsBusy||Session.HasPendingSave)return;
            var pick=facePick14;if(FacePickReason14(offer,pick)!=null)return;
            // S4: the one purchase command; the existing upgrade receipt plays the face being rewritten. One face keeps
            // the old single-face field (RULES_DELTA 12.1: it equals a one-element array); two faces go in the array.
            facePick14=null;
            string[] faces=FaceArray14(pick);
            if(pick.custom)Send(new SessionCommand{kind=CommandKind.CustomDieUpgrade,upgradeId=pick.contentId,targetDieId=pick.dieId,targetFaceIds=faces});
            else if(faces.Length>1)Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId,targetDieId=pick.dieId,targetFaceIds=faces});
            else Send(new SessionCommand{kind=CommandKind.Buy,offerId=offer.offerId,targetDieId=pick.dieId,targetFaceId=faces.Length==0?null:faces[0]});
        }

        /// <summary>"5 点红面", with its traits in brackets: "5 点红面（光滑、黄金、权重 2）".</summary>
        private static string FaceText14(FaceState f)
        {
            var traits=new List<string>();
            if(f.texture=="smooth")traits.Add("光滑");else if(f.texture=="rough")traits.Add("粗糙");
            if(f.isGold)traits.Add("黄金");
            if(f.baseWeight>1)traits.Add("权重 "+f.baseWeight);
            return f.permanentPip+" 点"+(f.baseColor=="red"?"红":"蓝")+"面"+(traits.Count>0?"（"+string.Join("、",traits)+"）":"");
        }
        /// <summary>"第 3 颗 · 红面多 · 大点": the die's place in 我的骰子 and its title.</summary>
        private string OwnedDieLabel14(string dieId)
        {
            var dice=ViewState.dice.OrderBy(d=>d.createdSerial).ToList();int at=dice.FindIndex(d=>d.dieId==dieId);
            return at<0?"骰子":"第 "+(at+1)+" 颗 · "+DieTitle14(dice[at],Config.generation).title;
        }
        /// <summary>Why the chosen target cannot be bought (SHOP_LAYOUT 7.2, last column), or null.</summary>
        private string FacePickReason14(ShopOffer offer,FacePick14 pick)
        {
            if(pick==null||pick.dieId==null||!pick.whole&&pick.faces.Count==0)return "";
            if(offer.groupId!="LOOT"&&Session.IsLootPending)return "先领取首领战利品";
            string[] faces=FaceArray14(pick);
            if(offer.category=="die_upgrade")
            {
                // v1.1.1 (W7b): which faces are legal is the session's answer (LegalUpgradeFaces); with two faces chosen the
                // reason names the one that cannot be changed, so the other can be kept.
                if(pick.whole){if(!Session.IsUpgradeTargetLegal(offer.contentId,pick.dieId,null))return SelectedUpgradeReason16(offer,pick.dieId,null);}
                else
                {
                    var legal=Session.LegalUpgradeFaces(offer.contentId,pick.dieId);
                    string bad=faces.FirstOrDefault(f=>!legal.Contains(f));
                    if(bad!=null)
                    {
                        string why=SelectedUpgradeReason16(offer,pick.dieId,bad);
                        var face=faces.Length<2?null:ViewState.dice.FirstOrDefault(d=>d.dieId==pick.dieId)?.faces.FirstOrDefault(f=>f.faceId==bad);
                        return face==null?why:FaceText14(face)+"："+why;
                    }
                }
            }
            long cost=FacePickPrice14(offer);
            if(cost>ViewState.gold)return "还差 "+(cost-ViewState.gold)+" 金币";
            string error=pick.custom?Session.CustomDieUpgradeError(offer.contentId,pick.dieId,faces)
                :offer.category=="die_upgrade"?Session.PurchaseErrorForFaces(offer.offerId,pick.dieId,faces)
                :Session.PurchaseError(offer.offerId,pick.dieId,null);
            if(error==null)return null;
            return PlayerShopReason16(error,offer,pick.dieId,pick.faceId).TrimEnd('。');
        }
        // The approved S3 wording also describes a rejected purchase with the same fully selected target.
        private string SelectedUpgradeReason16(ShopOffer offer,string dieId,string faceId)
        {
            var die=ViewState?.dice.FirstOrDefault(d=>d.dieId==dieId&&d.lifetime=="permanent");
            if(offer?.category!="die_upgrade"||die==null||offer.contentId!="U05"&&!die.faces.Any(f=>f.faceId==faceId))return "这个目标不能再改造";
            switch(offer.contentId)
            {
                case "U01":return "这一面已经是光滑的，换一个面";
                case "U02":return "这一面已经是粗糙的，换一个面";
                case "U03":return "这一面已经是红色，换一个面";
                case "U04":return "这一面已经是蓝色，换一个面";
                case "U05":return "这颗已经是玻璃骰，换一颗";
                case "U06":return "这一面已经镀金，换一个面";
                case "U08":return "这一面已经是 "+Config.maxPipValue+" 点，换一个面";
                case "U09":return "这一面已经是 "+Config.minPipValue+" 点，换一个面";
                case "U10":return "这一面的权重已到上限 "+Config.maxFaceWeight+"，换一个面";
                case "U11":return "对面已经和这一面相同，换一个面";
                default:return "这个目标不能再改造";
            }
        }

        /// <summary>S3's change line and its note (SHOP_LAYOUT 7.2, columns 3 and 4).</summary>
        private void FacePickChange14(ShopOffer offer,FacePick14 pick,out string change,out string note)
        {
            change="";note=null;var die=ViewState.dice.FirstOrDefault(d=>d.dieId==pick.dieId);if(die==null)return;
            if(offer.contentId=="U05")
            {
                change=OwnedDieLabel14(die.dieId)+" → 玻璃骰";
                // p1.1.1 (RULES_DELTA 7.3): glass is judged after a submission, die by die; never on a roll or in the pool.
                note="计分 ×"+Config.glassContributionMultiplier+"；"+GlassRiskText111+"，留在骰池里不会碎";return;
            }
            if(offer.contentId=="O04")
            {
                change="删除 "+OwnedDieLabel14(die.dieId);
                note="连同它的改造一起删除；提交栏仍是 "+Session.EffectiveCapacity+" 格";return;
            }
            // v1.1.1 (W7b, RULES_DELTA 7.1): two faces show both changes, one line each in face order; the notes that
            // name one face's opposite or weight become one shared sentence (the price line below is the same price).
            string[] chosen=FaceArray14(pick);
            if(chosen.Length>1)
            {
                var lines=new List<string>();var notes=new List<string>();
                foreach(string id in chosen){FaceChange14(offer,die,id,out string one,out string oneNote);if(!string.IsNullOrEmpty(one))lines.Add(one);if(oneNote!=null&&!notes.Contains(oneNote))notes.Add(oneNote);}
                change=string.Join("\n",lines);
                switch(offer.contentId)
                {
                    case "U01":note="两个面的对面都更难掷出";break;
                    case "U02":note="两个面的对面都更容易掷出";break;
                    case "U06":note=GoldFaceText111+"；"+string.Join("；",chosen.Select(id=>GildWeightText111(die.faces.FirstOrDefault(f=>f.faceId==id))).Distinct());break;
                    case "U07":note="颜色、纹理、黄金不变；买下时各抽一次";break;
                    case "U10":note="更容易掷出这两面";break;
                    default:note=notes.Count==0?null:string.Join("；",notes);break;
                }
                return;
            }
            FaceChange14(offer,die,pick.faceId,out change,out note);
        }
        /// <summary>One face's change line and its note for a per-face upgrade.</summary>
        private void FaceChange14(ShopOffer offer,DieInstance die,string faceId,out string change,out string note)
        {
            change="";note=null;
            var face=die.faces.FirstOrDefault(f=>f.faceId==faceId);if(face==null)return;
            var after=die.Clone();var changed=after.faces.First(f=>f.faceId==face.faceId);
            var opposite=die.faces.FirstOrDefault(f=>f.faceId==Config.Opposite(face.faceId));
            switch(offer.contentId)
            {
                case "U01":changed.texture="smooth";note=opposite!=null?"它的对面（"+FaceText14(opposite)+"）更难掷出":null;break;
                case "U02":changed.texture="rough";note=opposite!=null?"它的对面（"+FaceText14(opposite)+"）更容易掷出":null;break;
                case "U03":changed.baseColor="red";break;
                case "U04":changed.baseColor="blue";break;
                // p1.1.1 (RULES_DELTA 7.2): a gold face adds to the bonus line and a newly gilded face gains weight.
                case "U06":changed.isGold=true;changed.baseWeight=Math.Min(Config.maxFaceWeight,changed.baseWeight+Config.gildedFaceWeightDelta);note=GoldFaceText111+"；"+GildWeightText111(face);break;
                case "U07":
                    change=FaceText14(face)+" → 随机变成 "+string.Join("、",Enumerable.Range(Config.minPipValue,Config.maxPipValue-Config.minPipValue+1).Where(n=>n!=face.permanentPip))+" 之一";
                    note="颜色、纹理、黄金不变；买下时才抽";return;
                case "U08":changed.permanentPip=Math.Min(Config.maxPipValue,changed.permanentPip+1);break;
                case "U09":changed.permanentPip=Math.Max(Config.minPipValue,changed.permanentPip-1);break;
                case "U10":changed.baseWeight=Math.Min(Config.maxFaceWeight,changed.baseWeight+1);note="更容易掷出这一面";break;
                case "U11":
                    if(opposite!=null)
                    {
                        var copy=after.faces.First(f=>f.faceId==opposite.faceId);copy.permanentPip=face.permanentPip;copy.baseColor=face.baseColor;
                        change="对面 "+FaceText14(opposite)+" → "+FaceText14(copy);
                    }
                    note="把这一面的点数和颜色复制到对面";return;
            }
            change=FaceText14(face)+" → "+FaceText14(changed);
        }

        /// <summary>The face-pick panel in place of 骰子与骰面: the good, a prompt, the chosen die (real, turnable, turning to
        /// the chosen face) with its six faces, the change and the price, and the buttons.</summary>
        private void FacePickPanel14(ShopLayout14 l)
        {
            var offer=FacePickOffer14;var pick=facePick14;if(offer==null)return;bool c=Large11;
            var panel=Section14(main,"FacePick14","FacePickTitle14",NameOf(offer.contentId),l.dice,out float titleRight);
            var strip=panel.Find("HeaderStrip") as RectTransform;
            string category=pick.custom?"自选改造":offer.category=="service"?"服务":"骰面改造";float categorySize=Body11(18),catW=ShopLabelWidth14(category,categorySize);
            // Inside the header strip's drawn frame (its right end carries the strip's decoration).
            var stripFrame=FrameInsets11(strip.GetComponent<Image>());float stripRight=strip.anchoredPosition.x+strip.sizeDelta.x-stripFrame.z-12;
            FitLine13(panel,"FacePickCategory14",category,stripRight-catW-4,(ShopHead14-Line11(categorySize))*.5f,catW+4,categorySize,LabelOn11(strip,UiKit.Muted),TextAlignmentOptions.MidlineRight,false);
            Color ink=LabelOn11(panel,UiKit.TextColor),muted=LabelOn11(panel,UiKit.Muted);
            float x=ShopPad14+4,w=l.dice.width-2*(ShopPad14+4),y=ShopHead14+10;
            TextMeshProUGUI Line(string name,string text,float size,Color color,bool bold)
            {
                var label=UiKit.Text(panel,name,text,x,y,w,10,Mathf.Max(size,19.5f/Mathf.Max(.01f,UiKit.FontScale)),color,TextAlignmentOptions.TopLeft);
                if(bold)label.fontStyle=FontStyles.Bold;label.textWrappingMode=TextWrappingModes.Normal;label.enableAutoSizing=false;
                // A prompt that wraps breaks at its punctuation (before the bracket, after a comma), not mid-phrase.
                label.text=WrapLines21(label,text,w,int.MaxValue)??text;
                float h=Mathf.Ceil(label.GetPreferredValues(label.text,w,0).y)+2;label.rectTransform.sizeDelta=new Vector2(w,h);y+=h+6;return label;
            }
            string step=FacePickStep14;
            // v1.1.1 (W7b; W9a fix): the second-face button. It sits on its own row under the price; with 150 % text that row
            // reached the foot row (V14 SH08: it covered 确认购买), so there it goes beside the die, under the die's name (where
            // S2 puts its prompt), the die row growing to hold it, with the shorter word when the long one does not fit.
            bool wantAdd=step=="S3"&&!pick.whole&&pick.faces.Count<pick.maxFaces,addBeside=wantAdd&&UiKit.FontScale>1.3f;
            string addFace=null;float addX=x,addY=0,addSlotW=0;
            if(step=="S1")
            {
                Line("FacePickShort14",ShortText11(offer.contentId),20,ink,false);
                string prompt=offer.contentId=="U05"?"选一颗骰子，整颗变成玻璃骰":offer.contentId=="O04"?"选一颗要删除的骰子":"选一颗骰子，再选要改的面";
                Line("FacePickPrompt14",prompt+"（在下面的“我的骰子”里）",20,ink,true);
            }
            else
            {
                var die=ViewState.dice.First(d=>d.dieId==pick.dieId);
                // The chosen die, real and turnable (a click on one of its faces chooses that face); its name and the step
                // beside it.
                const float big=120;
                var view=DieView073(panel,"FacePickDie14","facepick-"+die.dieId,die,x,y,big,big,.86f,false,false,null,
                    pick.whole?(Action<int>)null:i=>{var f=die.faces.FirstOrDefault(face=>DiceTable.FaceIndex(face.faceId)==i);if(f!=null)FacePickChooseFace14(f.faceId);});
                if(pick.faceId!=null)view.TurnToFace(DiceTable.FaceIndex(pick.faceId),reduced||UiKit.ReducedMotion||instant07);
                float rowTop=y,textX=x+big+12,textW=w-big-12;
                var dieLabel=UiKit.Text(panel,"FacePickDieLabel14",OwnedDieLabel14(die.dieId),textX,rowTop,textW,10,20,ink,TextAlignmentOptions.TopLeft);
                dieLabel.fontStyle=FontStyles.Bold;dieLabel.enableAutoSizing=false;dieLabel.textWrappingMode=TextWrappingModes.Normal;
                // A long name breaks at a " · " between its words, not inside a word (第 1 颗 · 金面 6 / 蓝面多).
                dieLabel.text=BalancedLines11(dieLabel,dieLabel.text,textW);
                float labelH=Mathf.Ceil(dieLabel.GetPreferredValues(dieLabel.text,textW,0).y)+2;dieLabel.rectTransform.sizeDelta=new Vector2(textW,labelH);
                // The row is as tall as the die or the text beside it, whichever is taller (with enlarged text the S2 prompt
                // takes two or three lines and must end above the face buttons).
                float rowH=Mathf.Max(big,labelH);
                if(step=="S2")
                {
                    // v1.1.1 (W7b): the second face of an upgrade that takes two is chosen on the same six tiles.
                    var prompt=UiKit.Text(panel,"FacePickPrompt14",pick.adding?"再选一个面，价格不变":"选要改的面：点下面的方块，或点骰子上的面",textX,rowTop+labelH+6,textW,10,20,ink,TextAlignmentOptions.TopLeft);
                    prompt.enableAutoSizing=false;prompt.textWrappingMode=TextWrappingModes.Normal;
                    prompt.text=WrapLines21(prompt,prompt.text,textW,int.MaxValue)??prompt.text;
                    float promptH=Mathf.Ceil(prompt.GetPreferredValues(prompt.text,textW,0).y)+2;
                    prompt.rectTransform.sizeDelta=new Vector2(textW,promptH);rowH=Mathf.Max(rowH,labelH+6+promptH);
                }
                if(addBeside){addX=textX;addY=rowTop+labelH+6;addSlotW=textW;rowH=Mathf.Max(rowH,labelH+6+60);}
                y+=rowH+10;
                if(!pick.whole&&step=="S2")
                {
                    // Six face buttons, 60 units each (40 px at 720p, the touch rule), texture under the tile. S2 only: in S3 the
                    // big die is turned to the chosen face (a click on another of its faces, or 换一面, changes it), which leaves
                    // the change, its note and the price room with enlarged text.
                    const float tile=60;float gap=Mathf.Min(10,(w-6*tile)/5f);
                    // v1.1.1 (W7b): the faces the session will not accept for this upgrade are dimmed (they can still be
                    // clicked: S3 then says why and keeps 确认 off); every chosen face is ringed.
                    FaceStrip14(panel,die,x+(w-(6*tile+5*gap))*.5f,y,tile,muted,FacePickChooseFace14,pick.faceId,"Face_",gap,pick.faces,
                        offer.category=="die_upgrade"?Session.LegalUpgradeFaces(offer.contentId,die.dieId):null);
                    y+=tile+Line11(16)+8;
                }
                if(step=="S3")
                {
                    FacePickChange14(offer,pick,out string change,out string note);
                    string reason=FacePickReason14(offer,pick);
                    Line("PurchasePreviewBody",change,20,ink,true);
                    if(reason!=null)Line("PurchaseUnavailable",reason,20,ink,false);
                    else if(note!=null)Line("FacePickNote14",note,20,ink,false);
                    long cost=FacePickPrice14(offer);
                    Line("FacePickPrice14",!string.IsNullOrEmpty(offer.groupId)?"免费":"价格 "+cost+"（你有 "+PlayerNumberFormat.Gold(ViewState.gold)+"）",20,muted,false);
                    // v1.1.1 (W7b, RULES_DELTA 7.1): an upgrade that takes two faces offers the second one here, for the
                    // same price. It is a second-level button of its own row, above the foot row.
                    if(wantAdd){addFace=string.IsNullOrEmpty(offer.groupId)?"再选一个面（价格不变）":"再选一个面";if(!addBeside){addX=x;addY=y;y+=60+6;}}
                }
            }
            // Buttons along the foot of the panel, right to left: 确认购买 ⦿N, 换一面 / 换一颗, 取消 (second-level: the screen's one
            // primary colour stays on 下一关). At their usual padding (16, gaps 10) the three buttons of S3 do not fit the
            // panel's row with enlarged text; the padding then steps down to 12 and the gaps to 6, so all stay inside it.
            float by=l.dice.height-ShopFoot14-4-60,bx=l.dice.width-ShopPad14;
            bool confirmShown=step=="S3";
            string swap=step=="S3"&&!pick.whole?"换一面":step=="S2"&&pick.adding?"只改一面":step=="S2"||step=="S3"&&pick.whole?"换一颗":null;
            long confirmCost=FacePickPrice14(offer);bool gift=!string.IsNullOrEmpty(offer.groupId);string confirmWord=gift?"确认领取":offer.contentId=="O04"?"确认删除":"确认购买";
            float wordW=ShopLabelWidth14(confirmWord,20),priceW=26+4+ShopLabelWidth14(confirmCost.ToString(CultureInfo.InvariantCulture),22);
            float swapW=swap==null?0:ShopLabelWidth14(swap,20),cancelLabelW=ShopLabelWidth14("取消",20);
            float RowWidth(float p,float g)=>(confirmShown?p+wordW+(gift?0:10+priceW)+p+g:0)+(swap!=null?swapW+2*p+g:0)+cancelLabelW+2*p+8;
            float pad=16,gapB=10;if(RowWidth(pad,gapB)>l.dice.width-2*ShopPad14){pad=12;gapB=6;}
            if(confirmShown)
            {
                bool can=FacePickReason14(offer,pick)==null&&!IsBusy&&!Session.HasPendingSave;
                float cw=2*pad+wordW+(gift?0:10+priceW);
                var confirm=ShopButton14(panel,"ConfirmPurchase",confirmWord,bx-cw,by,cw,60,FacePickConfirm14,can);
                var label=confirm.transform.Find("Label").GetComponent<TextMeshProUGUI>();var face=(Image)confirm.targetGraphic;
                label.alignment=TextAlignmentOptions.MidlineLeft;label.rectTransform.anchoredPosition=new Vector2(pad,0);label.rectTransform.sizeDelta=new Vector2(wordW,60);
                if(!gift)
                {
                var coin=UiKit.Box(confirm.transform,"Coin",pad+wordW+10,17,26,26,can?Color.white:Alpha14(Color.white,.6f)).GetComponent<Image>();coin.sprite=UiKit.Template("ui_icon_coin");coin.preserveAspect=true;coin.raycastTarget=false;
                NoWrap11(UiKit.Text(confirm.transform,"Price",confirmCost.ToString(CultureInfo.InvariantCulture),pad+wordW+10+30,0,priceW-30,60,22,LabelOn11(face,can?UiKit.Gold:UiKit.Muted),TextAlignmentOptions.MidlineLeft)).fontStyle=FontStyles.Bold;
                }
                bx-=cw+gapB;
            }
            if(swap=="换一面"){float ww=swapW+2*pad;ShopButton14(panel,"ChangeFace14",swap,bx-ww,by,ww,60,()=>{if(facePick14!=null){facePick14.faces.Clear();facePick14.adding=false;Refresh();}},!IsBusy);bx-=ww+gapB;}
            if(swap=="只改一面"){float ww=swapW+2*pad;ShopButton14(panel,"KeepOneFace14",swap,bx-ww,by,ww,60,()=>{if(facePick14!=null){facePick14.adding=false;Refresh();}},!IsBusy);bx-=ww+gapB;}
            if(swap=="换一颗"){float ww=swapW+2*pad;ShopButton14(panel,"ChangeDie14",swap,bx-ww,by,ww,60,()=>{if(facePick14!=null){facePick14.dieId=null;facePick14.faces.Clear();facePick14.adding=false;Refresh();}},!IsBusy);bx-=ww+gapB;}
            float cancelW=cancelLabelW+2*pad+8;ShopButton14(panel,"CancelFacePick14","取消",bx-cancelW,by,cancelW,60,CancelFacePick14,true);
            if(addFace!=null)
            {
                float addW=Mathf.Min(w,ShopLabelWidth14(addFace,20)+32);
                if(addBeside&&addW>addSlotW){addFace="再选一个面";addW=Mathf.Min(addSlotW,ShopLabelWidth14(addFace,20)+32);}
                ShopButton14(panel,"AddFace14",addFace,addX,addY,addW,60,FacePickAddFace14,!IsBusy);
            }
        }
        /// <summary>A die of 我的骰子 that can be chosen (S1–S3): a hit area named Target_&lt;die&gt; over its cell, ringed in
        /// gold while every die can be chosen and around the chosen one afterwards.</summary>
        private void FacePickTarget14(Transform panel,DieInstance die,float x,float y,float size)
        {
            string id=die.dieId;bool chosen=facePick14?.dieId==id;
            var b=UiKit.Button(panel,"Target_"+id,"",x,y,size,size,()=>FacePickChooseDie14(id),!IsBusy);
            var root=(RectTransform)b.transform;
            foreach(string part in new[]{"Step","Lip","Label"})root.Find(part)?.gameObject.SetActive(false);
            var face=(Image)b.targetGraphic;face.sprite=null;face.color=Color.clear;face.raycastTarget=true;
            ((RectTransform)face.transform).anchoredPosition=Vector2.zero;((RectTransform)face.transform).sizeDelta=new Vector2(size,size);
            if(facePick14!=null&&(facePick14.dieId==null||chosen))UiKit.Ring(root,"TargetRing14",-4,-4,size+8,size+8,UiKit.Gold);
        }
    }
}
