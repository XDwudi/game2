using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DiceCraft.Core;
using TMPro;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private bool handbookColors,handbookRules09;
        private string[][] HandbookRules09 => new string[][]
        {
            // v1.1 (text proposal 5 #6): the order ScoreCalculator applies, including the item and boss factors after the enchantments.
            new[]{"计分顺序","点数合计（含再计一次）＋各种加值 ＝ 基础分；再依次乘以牌型倍率、颜色倍率、红组倍率、各卡牌倍率和倍率附魔；用了道具“灵光一闪”时再 ×2，首领减益让得分减半时再 ×0.5；最后向下取整一次。"},
            new[]{"成长值","卡面左上角标着 ×n 或 +n 的卡牌会随对局变强。成长发生在触发它的那次计分之后，所以触发成长的那一手用的还是成长前的数值。卖出或损毁就失去成长值；同名的几张卡牌各自成长；百变模仿使用被模仿那张卡的成长值，但自己不成长。"},
            new[]{"再计一次","指定的提交骰把自己的点数贡献再加进基础分一次，逐骰加值、玻璃和黄金都一并翻倍。多个来源可以叠加成再计多次。不改变牌型、红组和骰子数量。"},
            new[]{"留池骰","本手投出后没有锁进提交栏的骰子。它们不计分，但板凳深度、厚积薄发、幕后推手、空城计和场外红人会读取它们的数量、点数和颜色。骰子比提交栏多时，留什么在池里也是一种选择。"},
            new[]{"牌型倍率加值","成双成对、三足鼎立把数值直接加在牌型倍率上，例如一对从 ×2 变成 ×3.5，之后仍会被颜色、红组和卡牌倍率继续放大。"},
            new[]{"投掷次数","一锤定音要求只投 1 次就提交；越挫越勇从第 2 次投掷起每次加分；孤注一掷要求把投掷次数用完；熟能生巧每投 1 次成长一点。"},
            new[]{"栏位",$"卡牌栏位开局 {Config.initialControllerSlots} 个，商店服务“扩充栏位”最多加到 {Config.maxOrdinaryControllerSlots} 个。带“不占栏位”附魔的卡牌不占栏位；所有卡牌合计最多 {Config.maxControllerInstances} 张。"},
            new[]{"祭坛","每关开始时摧毁它右侧相邻的卡牌并成长。把它放在最右边就不会摧毁任何东西；想喂它，就把便宜的卡排在它右边。"}
        };
        private void DrawHandbookRules09(Transform p)
        {
            var list=UiKit.Scroll(p,"HandbookScroll",24,144,1456,720,720);float y=8;
            // p1.0.0: the cycle, boss and face-growth rules follow the v0.9 rows on the same page.
            var rules=HandbookRules09.Concat(HandbookRules10()).ToArray();
            for(int i=0;i<rules.Length;i++)
            {
                var title=UiKit.Text(list,"Rule09Title"+i,rules[i][0],16,y,1400,44,26,UiKit.Ink);title.fontStyle=FontStyles.Bold;y+=FitHeight(title,44)+2;
                var body=UiKit.Text(list,"Rule09Body"+i,rules[i][1],16,y,1400,60,22,PaperMuted071);y+=FitHeight(body,60)+10;
                UiKit.Box(list,"Rule09Divider"+i,16,y,1400,1,PaperMuted071);y+=14;
            }
            list.sizeDelta=new Vector2(list.sizeDelta.x,Math.Max(720,y+8));
        }
        public void ShowHandbook()
        {
            if(Config==null||IsBusy||IsControllerDragging||Session?.HasPendingSave==true)return;
            var s=ViewState;
            int capacity=s==null?Config.initialSubmitCapacity:s.stage.started&&s.phase!="Shop"?s.stage.capacity:Session.EffectiveCapacity;
            var allRows=HandbookRules.Build(Config,s?.handLevels??new System.Collections.Generic.List<HandLevel>(),s?.colorLevels??new System.Collections.Generic.List<HandLevel>(),
                s?.controllers??new System.Collections.Generic.List<ControllerInstance>(),capacity,s?.dice.Count(d=>d.lifetime=="permanent")??Config.initialDiceCount,
                s?.dice.Count??Config.initialDiceCount,s==null?null:ViewBindings,s?.isDebug??false);
            var rows=allRows.Where(r=>r.category==(handbookColors?"color":"hand"))
                .OrderByDescending(r=>r.handId=="scatter"?-1:r.category=="color"?0:Config.Hand(r.handId).structuralPriority).ToArray();
            var p=Art07Modal("牌型手册",208,96,1504,888,"handbook16");
            UiKit.Button(p,"Tutorial","练习模式",688,12,352,60,()=>{CloseModal();BeginTutorial();});
            UiKit.Button(p,"ReviewGuide","重新查看引导",1056,12,336,60,ShowGuide07);
            // p0.9.0: a third page explains growth, retriggers, pool dice, hand multiplier adds and slots.
            UiKit.FlatButton(p,"HandbookHands","牌型",24,76,476,60,()=>{handbookColors=false;handbookRules09=false;ShowHandbook();},true,!handbookColors&&!handbookRules09);
            UiKit.FlatButton(p,"HandbookColors","颜色与红组",514,76,476,60,()=>{handbookColors=true;handbookRules09=false;ShowHandbook();},true,handbookColors&&!handbookRules09);
            UiKit.FlatButton(p,"HandbookRules09","进阶规则",1004,76,476,60,()=>{handbookRules09=true;ShowHandbook();},true,handbookRules09);
            var paper=UiKit.PaperFrame(p,"HandbookPaper",16,140,1472,732,UiKit.Paper);
            if(handbookRules09){DrawHandbookRules09(p);Canvas.ForceUpdateCanvases();return;}
            var preview=Session?.PreviewScoreRange()?.minimum;
            if(!handbookColors)
            {
                bool large=UiKit.FontScale>=1.25f;
                float rowHeight=large?132:88;
                var list=UiKit.Scroll(p,"HandbookScroll",24,144,1456,large?556:616,7*rowHeight);
                for(int i=0;i<rows.Length;i++)
                {
                    var r=rows[i];float x=(i/7)*736,y=(i%7)*rowHeight;
                    var row=UiKit.FlatButton(list,"HandbookRow_"+r.handId,"",x,y,712,rowHeight-4,()=>{art07HandSelection=r.handId;ShowHandbook();});
                    row.targetGraphic.color=art07HandSelection==r.handId?new Color(.08f,.18f,.18f,.12f):Color.clear;
                    bool primary=preview!=null&&(preview.primaryHandId==r.handId||(r.handId=="scatter"&&(preview.primaryHandId=="none"||preview.primaryHandId=="scatter")));
                    bool chosen=preview!=null&&(preview.chosenGroupId==r.handId||(r.handId=="straight"&&preview.straightMult100>0));
                    if(primary)UiKit.Box(row.transform,"Structure",3,8,3,rowHeight-20,UiKit.Gold);
                    UiKit.Text(row.transform,"Name",r.displayName,12,8,350,42,24,UiKit.Ink);
                    UiKit.Text(row.transform,"Level",r.maxLevel==0?"不能升级":"Lv."+r.level+" / "+r.maxLevel,390,8,306,42,20,PaperMuted071,TextAlignmentOptions.TopRight);
                    var numbers=r.example.Where(char.IsDigit).Select(c=>c-'0').Where(n=>n>=1&&n<=6).ToArray();
                    if (chosen)
                    {
                        var chip=Surface11(row.transform,"ScoringHand",214,8,168,42,"ui_chip_label",UiKit.Gold);
                        FitLine13(chip,"Label","本手计分",6,0,156,19.5f,LabelOn11(chip),TextAlignmentOptions.Center,false);
                    }
                    float bottom=large?76:46;
                    for(int n=0;n<numbers.Length;n++)Art07PipTile(row.transform,"Example"+n,numbers[n],UiKit.Ink,12+n*34,bottom,30);
                    UiKit.Text(row.transform,"Multiplier","×"+Factor(r.currentMult100),316,bottom-2,208,40,22,UiKit.Ink);
                    UiKit.Text(row.transform,"Reachable",r.canForm?"凑得出":"凑不出",534,bottom,162,38,19.5f,PaperMuted071,TextAlignmentOptions.TopRight);
                }
                var selected=rows.FirstOrDefault(r=>r.handId==art07HandSelection)??rows[0];
                art07HandSelection=selected.handId;
                string requirement = "不能升级";
                if (selected.handId != "scatter")
                {
                    int permanent = Config.Hand(selected.handId).requirePermanentDice;
                    requirement = $"至少提交 {selected.minimumDice} 颗"
                        + (permanent > 0 ? $" · 需要 {permanent} 颗永久骰" : "")
                        + " · " + selected.upgradeReason;
                }
                string explanation = selected.displayName + "：" + selected.condition + "\n" + requirement;
                if (!string.IsNullOrEmpty(selected.formReason)) explanation += "\n" + selected.formReason;
                var frame = FrameInsets11(paper.GetComponent<UnityEngine.UI.Image>());
                float left = Mathf.Max(40, 16 + frame.x + 2);
                float top = large ? 708 : 768;
                var selection = UiKit.Text(p,"HandbookSelection",explanation,left,top,1464-left,large?152:96,20,UiKit.Ink);
                selection.alignment = TextAlignmentOptions.TopLeft;
            }
            else
            {
                var list=UiKit.Scroll(p,"HandbookScroll",24,144,1456,720,Math.Max(720,UiKit.FontScale>1?1100:720));
                float sectionHeight=UiKit.FontScale>1?220:164;
                for(int i=0;i<rows.Length;i++)
                {
                    var r=rows[i];bool red=r.handId=="all_red";var card=UiKit.Box(list,"HandbookRow_"+r.handId,0,i*sectionHeight,1432,sectionHeight-12,Color.clear);
                    UiKit.Box(card,"ColorMark",4,16,4,sectionHeight-44,red?UiKit.Red:UiKit.Blue);
                    UiKit.Box(card,"Divider",16,sectionHeight-14,1400,1,PaperMuted071);
                    UiKit.Text(card,"Name",(red?"● ":"◆ ")+r.displayName+"   Lv."+r.level,20,16,640,54,28,UiKit.Ink);
                    UiKit.Text(card,"Value","颜色倍率 ×"+Factor(r.currentMult100),724,16,680,54,28,UiKit.Ink,TextAlignmentOptions.TopRight);
                    UiKit.Text(card,"Rule","提交的骰子全是"+(red?"红色":"蓝色")+"即可（1 颗也算）。\n"+(r.canForm?"":r.formReason+" · ")+r.upgradeReason,20,80,1380,sectionHeight-88,23,PaperMuted071);
                }
                float redTop=sectionHeight*2+16;
                UiKit.Text(list,"RedTitle","红组倍率",12,redTop,1380,64,30,UiKit.Ink);
                UiKit.Text(list,"RedRule","同一点数的红骰越多倍率越高（取最大的一组）。它和全红/全蓝分开算。",12,redTop+72,1380,64,24,PaperMuted071);
                for(int i=0;i<Config.redRewards.Length;i++)
                {
                    var reward=Config.redRewards[i];int col=i%4,row=i/4;
                    var entry=UiKit.Box(list,"RedReward"+i,col*356,redTop+152+row*76,336,68,new Color(0,0,0,.04f));
                    UiKit.Text(entry,"Value",reward.count+" 颗  ×"+Factor(reward.factor100),8,8,320,52,25,UiKit.Ink,TextAlignmentOptions.Center);
                }
                string sources=NameOf("C24")+"："+ShortText11("C24")+"\n"+NameOf("C25")+"："+ShortText11("C25");
                float sourceTop=redTop+152+((Config.redRewards.Length+3)/4)*76+16;
                var text=UiKit.Text(list,"GroupingSources",sources,12,sourceTop,1380,140,23,PaperMuted071);
                list.sizeDelta=new Vector2(list.sizeDelta.x,sourceTop+FitHeight(text,140)+20);
            }
            Canvas.ForceUpdateCanvases();
        }

        private int[] FaceProbability(DieInstance die) => ProbabilityCalculator.BuildUnits(die.faces,false,false,Config,
            ViewState==null?0:ViewSources.Count(c=>c.abilityId=="C34"),ViewState==null?0:ViewSources.Count(c=>c.abilityId=="C35"));
        private static string EnchantShort(string id) => id=="slotless"?"不占栏位":id=="base10"?"基础 +10":id=="mult2"?"得分 ×2":id=="submit1"?"提交 +1":id=="roll1"?"掷骰 +1":"无附魔";
        private string EnchantLabel(string id) => id=="slotless"?"不占栏位：不占卡牌栏位（仍算在 "+Config.maxControllerInstances+" 张上限里）":id=="base10"?"基础 +10：这张卡生效时基础分 +10":id=="mult2"?"得分 ×2：这张卡生效时得分 ×2":id=="submit1"?"每关提交 +1":id=="roll1"?"每手掷骰 +1":"无附魔";
        /// <summary>V1.1 (W13): an enchantment's badge by name through the theme lookup (UiKit.Template: the active style's own
        /// badge_enchant_*, else the default template); the serialized list is only the last fallback, so a style's badges
        /// show wherever a badge is drawn.</summary>
        private Sprite EnchantSprite(string id)
            =>UiKit.Template("badge_enchant_"+id)
              ??enchantIcons?.FirstOrDefault(s=>s!=null&&s.name=="badge_enchant_"+id)
              ??enchantIcons?.FirstOrDefault(s=>s!=null&&s.name=="Enchant_"+id);
        private void EnchantBadge(Transform parent,string id,float x,float y,float size)
        {
            if(string.IsNullOrEmpty(id)||id=="none")return;
            var sprite=EnchantSprite(id);
            var box=UiKit.Rect(parent,"EnchantFrame",x-2,y-2,size+4,size+4);
            if(sprite!=null){var image=UiKit.Component<UnityEngine.UI.Image>(UiKit.Rect(box,"Badge",2,2,size,size).gameObject);image.sprite=sprite;image.raycastTarget=false;image.preserveAspect=true;}
            else UiKit.Text(box,"Symbol",id=="slotless"?"−":id=="base10"?"+10":id=="mult2"?"×2":"+1",0,0,size+4,size+4,16,UiKit.Ink,TextAlignmentOptions.Center);
        }
        private string ControllerPurchaseText(ShopOffer offer)
        {
            // v1.1 (G3): the new copy is added to what you hold; nothing is replaced or traded in.
            int owned=ViewState.controllers.Count(c=>c.definitionId==offer.contentId);
            var final=ViewState.controllers.Select(c=>c.Clone()).ToList();
            final.Add(offer.controller);
            // V1.1 W14 (UI_REWRITES Shop.cs:132, D2): "买下后：卡牌栏位 0 → 1 / 5 · 共 1/10 张" and the price as a sum.
            int basis=Config.Controller(offer.contentId).buyPrice,price=Session.GetOfferPrice(offer);
            long enchant=offer.controller==null||offer.controller.enchantmentId=="none"?0:ControllerRules.EnchantPrice(offer.controller.enchantmentId,Config),cut=basis+enchant-price;
            return $"买下后：卡牌栏位 {ControllerRules.OrdinaryUsed(ViewState.controllers)} → {ControllerRules.OrdinaryUsed(final)} / {ViewState.controllerSlots} · 共 {final.Count}/{Config.maxControllerInstances} 张\n"+
                (enchant>0||cut>0?$"价格 {price} = {basis}"+(enchant>0?$" + 附魔 {enchant}":"")+(cut>0?$" − 砍价 {cut}":""):$"价格 {price}")+
                (owned>0?$"\n已有 {owned} 张同名卡牌；买下后共 {owned+1} 张，每张各自生效。":"");
        }
        private void ShowController(ControllerInstance item,bool selling)
        {
            if(IsBusy)return;controllerOrderPanel=false;
            var p=Art07Modal(NameOf(item.definitionId),480,180,960,720);
            var card=UiKit.Frame(p,"DetailCard",24,104,184,272,UiKit.Paper,UiKit.Ink);
            DrawControllerRarity07(card,item.definitionId,false,184,272);
            Icon(card,item.definitionId,20,70,144);
            // v1.1 (W09): the position printed on the card ("第 2 张"), never its instance id.
            UiKit.Text(card,"Identity",CardPosition11(ViewState.controllers,item.instanceId)??"",8,222,168,40,18,UiKit.Ink,TextAlignmentOptions.Center);
            EnchantBadge(card,item.enchantmentId,132,10,40);
            var source=ViewSources.FirstOrDefault(x=>x.emitterId==item.instanceId);
            var list=PaperDetailScroll071(p,"ControllerDetail",232,104,704,500);float y=0;
            var def=Config.Controller(item.definitionId);
            string rules=ShortText11(item.definitionId);
            if(!string.IsNullOrWhiteSpace(def.details) && def.details!=rules)rules+="\n"+def.details;
            Art07DetailSection(list,"Skill","效果",rules,ref y,672,true);
            Art07DetailSection(list,"Enchantment","附魔",EnchantLabel(item.enchantmentId),ref y,672,true);
            if((item.enchantmentId=="base10"||item.enchantmentId=="mult2")&&(source?.abilityId==null||!Config.Controller(source.abilityId).scoreEnchantEligible))
                Art07DetailSection(list,"NoScoreTrigger","附魔不会触发","这张卡不参与计分，所以“"+EnchantShort(item.enchantmentId)+"”附魔不会触发。",ref y,672,true);
            string current=ControllerStateLabel(item,true);
            if(source?.abilityId=="C53")
            {
                if(item.definitionId!="C53")current+="\n"+Config.Controller("C53").details;
                var last=ViewState.lastSubmission;
                var draw=last?.snapshot?.resolvedRandomChoices?.FirstOrDefault(q=>q.ownerInstanceId==source.stateOwnerId);
                if(draw!=null)current+="\n上一手抽到 "+PlayerNumberFormat.Times(draw.units2,2)+"（下一次重新抽）";
            }
            if(!string.IsNullOrWhiteSpace(current))Art07DetailSection(list,"Current","当前状态",current.Trim(),ref y,672,true);
            if(item.definitionId=="C41"||item.definitionId=="C52")
            {
                // v1.1 (W09, user decision D5): a card bought from this shop's shelf resells for at most the price paid until
                // the player leaves; when that cap is below the ordinary price, the basis names the cap instead of half the
                // list price (RULES_DELTA 1.1 item 9 and 8.3). Such a card has no fund or bonus yet: both grow at stage end.
                long sale=Session.GetControllerSalePrice(item.instanceId),ordinary=ControllerRules.SalePrice(item,Config);
                var paid=ViewState.phase=="Shop"?ViewState.shop?.paidControllerPrices?.FirstOrDefault(p=>p!=null&&p.instanceId==item.instanceId):null;
                if(paid==null || paid.paid>=ordinary)
                    Art07DetailSection(list,"Fund","卖价","卖价 "+sale+" = 基础 "+(sale-item.state.fundAccrual-item.state.saleBonus)+
                        " + 成长 "+item.state.fundAccrual+" + 聚宝盆 "+item.state.saleBonus+"（卖出时才得到）",ref y,672,true);
            }
            var preview=Session.PreviewScoreRange();
            string trigger=preview==null?"锁定骰子后显示是否生效":preview.isRandom?"含随机结果，提交后才知道":preview.minimum.triggeredSourceIds.Contains(item.instanceId)?"会生效":"不会生效";
            Art07DetailSection(list,"Trigger","这一手",trigger,ref y,672,true);
            // V1.1 W14 (UI_REWRITES V05.cs:172, 176; D5): the sale price in words, with the same-shop cap shown when it applies.
            Art07DetailSection(list,"Price","交易与规则",SaleText14(item)+"。卖掉后这张卡和附魔一起消失，它改造过的骰面保留。",ref y,672,true);
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            if(selling)
            {
                UiKit.Button(p,"SellController","卖出 +"+Session.GetControllerSalePrice(item.instanceId)+" 金币",24,628,912,64,()=>Art07ConfirmControllerSale(item));
                UiKit.Outline(p,"SellOutline",24,628,912,64,UiKit.Red,2);
            }
            else if(!CanReorderControllers)UiKit.Text(p,"HeldRule","关卡中不能卖出或调整顺序（在商店里可以）。",24,628,912,64,24,UiKit.Muted);
        }
        // Keep existing viewport geometry and hierarchy; the paper is a non-raycasting
        // sibling behind the content, so scrolling, focus and footer actions stay intact.
        private RectTransform PaperDetailScroll071(Transform parent,string name,float x,float y,float width,float height)
        {
            UiKit.PaperFrame(parent,name+"Paper",x-12,y-12,width+24,height+24,UiKit.Paper);
            return UiKit.Scroll(parent,name,x,y,width,height,height);
        }
        private void Art07DetailSection(Transform parent,string id,string heading,string text,ref float y,float width,bool onPaper=false)
        {
            // Dark amber remains a section accent on warm paper; warning/action colors
            // outside this content panel keep their existing semantic treatment.
            UiKit.Text(parent,id+"Title",heading,0,y,width,44,23,onPaper?new Color(.42f,.25f,.10f):UiKit.Gold);y+=48*UiKit.FontScale;
            var body=UiKit.Text(parent,id+"Body",text,0,y,width,40,24,onPaper?UiKit.Ink:UiKit.Paper);y+=FitHeight(body,40)+28;
        }
        private void Art07ConfirmControllerSale(ControllerInstance item)
        {
            var p=Art07Modal("卖出 "+NameOf(item.definitionId)+"？",600,332,720,416);
            long price=Session.GetControllerSalePrice(item.instanceId);
            var list=UiKit.Scroll(p,"SaleDetails",24,90,672,214,214);
            var text=UiKit.Text(list,"Loss","卖价 "+price+" 金币"+(string.IsNullOrEmpty(item.enchantmentId)||item.enchantmentId=="none"?"":"\n附魔一起失去："+EnchantShort(item.enchantmentId))+"\n改造过的骰面保留。",0,0,640,200,25,UiKit.Paper);
            list.sizeDelta=new Vector2(list.sizeDelta.x,FitHeight(text,200));
            UiKit.Button(p,"CancelSale","取消",24,326,312,64,()=>ShowController(item,true));
            UiKit.Button(p,"ConfirmSale","卖出 +"+price+" 金币",360,326,336,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Sell,itemInstanceId=item.instanceId});});
            UiKit.Outline(p,"ConfirmOutline",360,326,336,64,UiKit.Red,2);
        }
        [Serializable]private sealed class BuildIdentity{public string sourceCommit=null,goldenCaseSetSha256=null,releaseVersion=null,artVersion=null;}
        private void ShowBuildVersion()
        {
            var asset=Resources.Load<TextAsset>("BuildIdentity");var identity=asset==null?new BuildIdentity():JsonUtility.FromJson<BuildIdentity>(asset.text);
            var p=Art07Modal("版本与构建",480,280,960,520);
            string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(configAsset.text))).Replace("-","").ToLowerInvariant();
            string tech=$"美术 {ArtVersion} / 发行 {identity.releaseVersion??"开发中"}\n玩法 {Config.configVersion} / 存档 schema {Config.saveSchemaVersion}\nUnity {Application.unityVersion} / {Application.platform}\n源码 {identity.sourceCommit??"开发工作树（未冻结）"}\n配置 SHA-256\n{hash}\n黄金样例 SHA-256\n{identity.goldenCaseSetSha256??"未写入构建身份"}";
            string release=string.IsNullOrWhiteSpace(identity.releaseVersion)?"开发中":identity.releaseVersion;
            string commit=identity.sourceCommit;
            string build=!string.IsNullOrEmpty(commit)&&commit.Length>=8&&commit.Take(8).All(Uri.IsHexDigit)?commit.Substring(0,8):"开发中";
            string unity=string.Join(".",Application.unityVersion.Split('.').Take(2));
            string summary=$"游戏版本 {release}（规则 {Config.configVersion}）\n构建 {build}\n存档格式 {Config.saveSchemaVersion} · {Application.platform} · Unity {unity}";
            UiKit.Text(p,"BuildIdentity",summary,24,104,912,200,25,LabelOn11(p));
            UiKit.Button(p,"CopyBuildIdentity16","复制完整技术信息",24,328,912,64,()=>
            {
                GUIUtility.systemCopyBuffer=tech;
                p.Find("CopyBuildIdentity16/Label").GetComponent<TextMeshProUGUI>().text="已复制";
            });
            var credits=UiKit.Scroll(p,"BuildCredits16",24,400,912,100,100);
            var credit=UiKit.Text(credits,"FontCredits16",FontCredits16,0,0,892,100,19.5f,LabelOn11(p,UiKit.Muted));
            float creditH=Mathf.Ceil(credit.GetPreferredValues(credit.text,892,Mathf.Infinity).y)+8;
            credit.rectTransform.sizeDelta=new Vector2(892,creditH);
            credits.sizeDelta=new Vector2(credits.sizeDelta.x,Mathf.Max(100,creditH));
        }
    }
}
