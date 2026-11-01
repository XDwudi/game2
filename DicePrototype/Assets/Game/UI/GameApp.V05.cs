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
        private bool handbookColors;
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
            var p=Art07Modal("牌型手册",208,96,1504,888);
            UiKit.Button(p,"Tutorial","独立固定骰练习",688,12,352,60,()=>{CloseModal();BeginTutorial();});
            UiKit.Button(p,"ReviewGuide","重新查看引导",1056,12,336,60,ShowGuide07);
            UiKit.Button(p,"HandbookHands","牌型 · H",24,76,712,60,()=>{handbookColors=false;ShowHandbook();},true,!handbookColors);
            UiKit.Button(p,"HandbookColors","颜色与红组 · C × R",760,76,720,60,()=>{handbookColors=true;ShowHandbook();},true,handbookColors);
            var preview=Session?.PreviewScoreRange()?.minimum;
            if(!handbookColors)
            {
                float rowHeight=UiKit.FontScale>1?132:88;
                var list=UiKit.Scroll(p,"HandbookScroll",24,144,1456,616,7*rowHeight);
                for(int i=0;i<rows.Length;i++)
                {
                    var r=rows[i];float x=(i/7)*736,y=(i%7)*rowHeight;
                    var row=UiKit.Button(list,"HandbookRow_"+r.handId,"",x,y,712,rowHeight-4,()=>{art07HandSelection=r.handId;ShowHandbook();});
                    bool primary=preview!=null&&(preview.primaryHandId==r.handId||(r.handId=="scatter"&&(preview.primaryHandId=="none"||preview.primaryHandId=="scatter")));
                    bool chosen=preview!=null&&(preview.chosenGroupId==r.handId||(r.handId=="straight"&&preview.straightMult100>0));
                    if(primary)UiKit.Outline(row.transform,"Structure",2,2,708,rowHeight-8,UiKit.Gold);
                    UiKit.Text(row.transform,"Name",r.displayName,12,8,350,42,24,UiKit.Paper);
                    UiKit.Text(row.transform,"Level",r.maxLevel==0?"不可强化":"Lv."+r.level+" / "+r.maxLevel,390,8,306,42,20,UiKit.Muted,TextAlignmentOptions.TopRight);
                    var numbers=r.example.Where(char.IsDigit).Select(c=>c-'0').Where(n=>n>=1&&n<=6).ToArray();
                    float bottom=UiKit.FontScale>1?76:46;
                    for(int n=0;n<numbers.Length;n++)Art07PipTile(row.transform,"Example"+n,numbers[n],UiKit.Ink,12+n*34,bottom,30);
                    UiKit.Text(row.transform,"Multiplier","×"+Factor(r.currentMult100)+(chosen?" 计分":""),316,bottom-2,208,40,22,chosen?UiKit.Gold:UiKit.Paper);
                    UiKit.Text(row.transform,"Reachable",r.canForm?"数量可达":"数量受限",534,bottom,162,38,18,r.canForm?UiKit.Teal:UiKit.Muted,TextAlignmentOptions.TopRight);
                }
                var selected=rows.FirstOrDefault(r=>r.handId==art07HandSelection)??rows[0];
                art07HandSelection=selected.handId;
                string requirement=selected.handId=="scatter"?"不可强化":$"最低 K {selected.minimumDice} · 永久骰门槛 {Config.Hand(selected.handId).requirePermanentDice} · {selected.upgradeReason}";
                var explanation=UiKit.Scroll(p,"HandbookSelectionScroll",24,772,1456,100,100);
                var selection=UiKit.Text(explanation,"HandbookSelection",selected.displayName+"："+selected.condition+"\n"+requirement+"\n"+selected.formReason,0,0,1424,100,20,UiKit.Muted);
                explanation.sizeDelta=new Vector2(explanation.sizeDelta.x,FitHeight(selection,100));
            }
            else
            {
                var list=UiKit.Scroll(p,"HandbookScroll",24,144,1456,720,Math.Max(720,UiKit.FontScale>1?1100:720));
                float sectionHeight=UiKit.FontScale>1?220:164;
                for(int i=0;i<rows.Length;i++)
                {
                    var r=rows[i];bool red=r.handId=="all_red";var card=UiKit.Frame(list,"HandbookRow_"+r.handId,0,i*sectionHeight,1432,sectionHeight-12,UiKit.Panel,red?UiKit.Red:UiKit.Blue);
                    UiKit.Text(card,"Name",(red?"● ":"◆ ")+r.displayName+"   Lv."+r.level,20,16,640,54,28,UiKit.Paper);
                    UiKit.Text(card,"Value","独立相乘 ×"+Factor(r.currentMult100),724,16,680,54,28,UiKit.Gold,TextAlignmentOptions.TopRight);
                    UiKit.Text(card,"Rule","非空且全部为有效"+(red?"红色":"蓝色")+"；没有数量要求。\n"+r.formReason+" · "+r.upgradeReason,20,80,1380,sectionHeight-88,23,UiKit.Muted);
                }
                float redTop=sectionHeight*2+16;
                UiKit.Text(list,"RedTitle","同点红组 · 独立红组奖励 R",12,redTop,1380,64,30,UiKit.Paper);
                UiKit.Text(list,"RedRule","默认取同点且为红色的最大一组。全红／全蓝不属于牌型 H。",12,redTop+72,1380,64,24,UiKit.Muted);
                for(int i=0;i<Config.redRewards.Length;i++)
                {
                    var reward=Config.redRewards[i];int col=i%4,row=i/4;
                    var entry=UiKit.Frame(list,"RedReward"+i,col*356,redTop+152+row*76,336,68,UiKit.PanelLight);
                    UiKit.Text(entry,"Value",reward.count+" 颗  ×"+Factor(reward.factor100),8,8,320,52,25,UiKit.Gold,TextAlignmentOptions.Center);
                }
                string sources=NameOf("C24")+"："+Description("C24")+"\n"+NameOf("C25")+"："+Description("C25");
                float sourceTop=redTop+152+((Config.redRewards.Length+3)/4)*76+16;
                var text=UiKit.Text(list,"GroupingSources",sources,12,sourceTop,1380,140,23,UiKit.Muted);
                list.sizeDelta=new Vector2(list.sizeDelta.x,sourceTop+FitHeight(text,140)+20);
            }
            Canvas.ForceUpdateCanvases();
        }

        private int[] FaceProbability(DieInstance die) => ProbabilityCalculator.BuildUnits(die.faces,false,false,Config,
            ViewState==null?0:ViewSources.Count(c=>c.abilityId=="C34"),ViewState==null?0:ViewSources.Count(c=>c.abilityId=="C35"));
        private static string EnchantShort(string id) => id=="slotless"?"免槽":id=="base10"?"触发 +10":id=="mult2"?"触发 ×2":id=="submit1"?"提交 +1":id=="roll1"?"投掷 +1":"无附魔";
        private static string EnchantLabel(string id) => id=="slotless"?"免槽 · 不占普通控制器槽，仍计入持有上限":id=="base10"?"基础分 +10 · 本次提交触发本实例时加一次":id=="mult2"?"整手 ×2 · 本次提交触发本实例时乘一次":id=="submit1"?"每关提交 +1 · 开始关卡时发放":id=="roll1"?"每手投掷 +1 · 新手开始时发放":"无附魔 · 占用1个普通控制器槽";
        private void EnchantBadge(Transform parent,string id,float x,float y,float size)
        {
            if(string.IsNullOrEmpty(id)||id=="none")return;
            var sprite=enchantIcons?.FirstOrDefault(s=>s!=null&&s.name=="badge_enchant_"+id)
                ??enchantIcons?.FirstOrDefault(s=>s!=null&&s.name=="Enchant_"+id);
            var box=UiKit.Box(parent,"EnchantFrame",x-2,y-2,size+4,size+4,id=="slotless"?UiKit.Teal:id=="base10"?UiKit.Gold:UiKit.Blue);
            if(sprite!=null){var image=UiKit.Component<UnityEngine.UI.Image>(UiKit.Rect(box,"Badge",2,2,size,size).gameObject);image.sprite=sprite;image.raycastTarget=false;}
            else UiKit.Text(box,"Symbol",id=="slotless"?"−":id=="base10"?"+10":id=="mult2"?"×2":"+1",0,0,size+4,size+4,16,UiKit.Ink,TextAlignmentOptions.Center);
        }
        private string ControllerPurchaseText(ShopOffer offer)
        {
            var old=ViewState.controllers.FirstOrDefault(c=>c.definitionId==offer.contentId);
            var final=ViewState.controllers.Where(c=>c.definitionId!=offer.contentId).Select(c=>c.Clone()).ToList();
            final.Add(offer.controller);
            return $"普通槽 {ControllerRules.OrdinaryUsed(ViewState.controllers)} → {ControllerRules.OrdinaryUsed(final)} / {ViewState.controllerSlots}   ·   总持有 {ViewState.controllers.Count} → {final.Count} / {Config.maxControllerInstances}\n"+
                (old==null?$"基础价 {Config.Controller(offer.contentId).buyPrice} + 附魔溢价 = {Session.GetOfferPrice(offer)}":"替换：新卡标价 "+Session.GetOfferPrice(offer)+" − 旧卡回收 "+Session.GetOfferTradeIn(offer)+(Session.GetOfferNetPrice(offer)<0?" = 净收入 "+(-Session.GetOfferNetPrice(offer)):" = 净支出 "+Session.GetOfferNetPrice(offer)));
        }
        private void ShowController(ControllerInstance item,bool selling)
        {
            if(IsBusy)return;controllerOrderPanel=false;
            var p=Art07Modal(NameOf(item.definitionId),480,180,960,720);
            var card=UiKit.Frame(p,"DetailCard",24,104,184,272,UiKit.Paper,UiKit.Ink);
            DrawControllerRarity07(card,item.definitionId,false,184,272);
            Icon(card,item.definitionId,20,70,144);
            UiKit.Text(card,"Identity",item.instanceId,8,222,168,40,18,UiKit.Ink,TextAlignmentOptions.Center);
            EnchantBadge(card,item.enchantmentId,132,10,40);
            var source=ViewSources.FirstOrDefault(x=>x.emitterId==item.instanceId);
            var list=UiKit.Scroll(p,"ControllerDetail",232,104,704,500,500);float y=0;
            Art07DetailSection(list,"Skill","基础技能",Description(item.definitionId),ref y,672);
            Art07DetailSection(list,"Enchantment","自身附魔",EnchantLabel(item.enchantmentId),ref y,672);
            if((item.enchantmentId=="base10"||item.enchantmentId=="mult2")&&(source?.abilityId==null||!Config.Controller(source.abilityId).scoreEnchantEligible))
                Art07DetailSection(list,"NoScoreTrigger","实验组合 · 无提交触发","保留附魔外观，但本实例当前技能不会产生计分附魔加成。",ref y,672);
            string current=ControllerStateLabel(item);
            if(item.definitionId=="C41")
            {
                var target=(ViewBindings??ControllerRules.FreezeBindings(ViewState.controllers)).FirstOrDefault(b=>b.sourceInstanceId==item.instanceId)?.targetInstanceId;
                var neighbor=ViewState.controllers.FirstOrDefault(c=>c.instanceId==target);
                string binding=string.IsNullOrEmpty(target)?"没有右邻":neighbor==null?target+" · 目标已失效":target+" · "+NameOf(neighbor.definitionId);
                current="绑定右邻："+binding+"\n实际模拟："+(source?.abilityId==null?"无能力":NameOf(source.abilityId))+"\n动态状态所有者："+(source?.stateOwnerId??"无")+"\n"+current;
            }
            if(source?.abilityId=="C53")
            {
                current+="\n可能倍率："+string.Join("、",Config.Controller("C53").parameters.factorUnits2.Select(q=>(q/2m).ToString("0.#")));
                var last=ViewState.lastSubmission;
                var draw=last?.snapshot.resolvedRandomChoices.FirstOrDefault(q=>q.ownerInstanceId==source.stateOwnerId);
                if(draw!=null)current+="\n上一提交记录：×"+(draw.units2/2m).ToString("0.#")+"（非下次预告）";
            }
            Art07DetailSection(list,"Current","当前实例与目标",string.IsNullOrEmpty(current)?"当前 Run 生效；按技能条件判定。":current,ref y,672);
            if(item.definitionId=="C41"||item.definitionId=="C52")
                Art07DetailSection(list,"Fund","本实例基金","基础卖价 "+Config.baseControllerSellPrice+" + 基金累计 "+item.state.fundAccrual+" = 当前卖价 "+Session.GetControllerSalePrice(item.instanceId)+"。未出售时不进入钱包。",ref y,672);
            var preview=Session.PreviewScoreRange();
            string trigger=preview==null?"锁定骰子后可查看当前提交条件。":preview.isRandom?"存在尚未抽取的随机结果；这里不预告下次结果。":preview.minimum.triggeredSourceIds.Contains(item.instanceId)?"当前提交：条件命中。实际增益以分项为准。":"当前提交：条件未满足。卡片仍然有效。";
            Art07DetailSection(list,"Trigger","当前触发",trigger,ref y,672);
            Art07DetailSection(list,"Price","交易与规则","当前卖价 "+Session.GetControllerSalePrice(item.instanceId)+"。出售会失去本实例及附魔，已完成的永久骰面改造保留。",ref y,672);
            list.sizeDelta=new Vector2(list.sizeDelta.x,y+16);
            if(selling)
            {
                UiKit.Button(p,"SellController","出售  +"+Session.GetControllerSalePrice(item.instanceId),24,628,912,64,()=>Art07ConfirmControllerSale(item));
                UiKit.Outline(p,"SellOutline",24,628,912,64,UiKit.Red,2);
            }
            else UiKit.Text(p,"HeldRule","关内顺序已固定；在商店出售或调整。",24,628,912,64,24,UiKit.Muted);
        }
        private void Art07DetailSection(Transform parent,string id,string heading,string text,ref float y,float width)
        {
            UiKit.Text(parent,id+"Title",heading,0,y,width,44,23,UiKit.Gold);y+=48*UiKit.FontScale;
            var body=UiKit.Text(parent,id+"Body",text,0,y,width,40,24,UiKit.Paper);y+=FitHeight(body,40)+28;
        }
        private void Art07ConfirmControllerSale(ControllerInstance item)
        {
            var p=Art07Modal("确认出售 · "+NameOf(item.definitionId),600,332,720,416);
            long price=Session.GetControllerSalePrice(item.instanceId);
            var list=UiKit.Scroll(p,"SaleDetails",24,90,672,214,214);
            var text=UiKit.Text(list,"Loss","卖价 "+price+" 金币\n将失去："+EnchantLabel(item.enchantmentId)+"\n本实例基金累计 "+item.state.fundAccrual+"；已完成的永久改造保留。",0,0,640,200,25,UiKit.Paper);
            list.sizeDelta=new Vector2(list.sizeDelta.x,FitHeight(text,200));
            UiKit.Button(p,"CancelSale","取消",24,326,312,64,()=>ShowController(item,true));
            UiKit.Button(p,"ConfirmSale","出售并获得 "+price,360,326,336,64,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Sell,itemInstanceId=item.instanceId});});
            UiKit.Outline(p,"ConfirmOutline",360,326,336,64,UiKit.Red,2);
        }
        [Serializable]private sealed class BuildIdentity{public string sourceCommit=null,goldenCaseSetSha256=null;}
        private void ShowBuildVersion()
        {
            var asset=Resources.Load<TextAsset>("BuildIdentity");var identity=asset==null?new BuildIdentity():JsonUtility.FromJson<BuildIdentity>(asset.text);
            var p=OpenModal("版本与构建");
            string hash;using(var sha=SHA256.Create())hash=BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(configAsset.text))).Replace("-","").ToLowerInvariant();
            UiKit.Text(p,"BuildIdentity",$"版本 {Config.configVersion} / 存档 schema {Config.saveSchemaVersion}\nUnity {Application.unityVersion} / {Application.platform}\n源码 {identity.sourceCommit??"开发工作树（未冻结）"}\n配置 SHA-256\n{hash}\n黄金样例 SHA-256\n{identity.goldenCaseSetSha256??"未写入构建身份"}",40,125,1510,540,25,UiKit.Paper);
        }
    }
}
