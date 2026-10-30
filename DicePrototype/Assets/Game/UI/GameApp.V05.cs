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
        public void ShowHandbook()
        {
            if(Session==null||ViewState==null||IsBusy||Session.HasPendingSave)return;
            var s=ViewState;
            int capacity=s.stage.started&&s.phase!="Shop"?s.stage.capacity:Session.EffectiveCapacity;
            var rows=HandbookRules.Build(Config,s.handLevels,s.controllers,capacity,
                s.dice.Count(d=>d.lifetime=="permanent"),s.dice.Count);
            var p=OpenModal("本局牌型等级 · 牌型手册");
            UiKit.Text(p,"HandbookHint",$"未获得强化 = 0级 · 当前 K={capacity} / 活动骰 {s.dice.Count} · 全部13种始终列出",40,94,1500,38,24,UiKit.Teal);
            var list=UiKit.Scroll(p,"HandbookScroll",40,145,1500,475,rows.Length*178);
            for(int i=0;i<rows.Length;i++)
            {
                var r=rows[i];var card=UiKit.Box(list,"HandbookRow_"+r.handId,0,i*178,1480,166,UiKit.PanelLight);
                var color=r.canForm?UiKit.Paper:UiKit.Muted;
                string level=r.handId=="none"||r.maxLevel==0?"不可强化":$"升级 {r.level}/{r.maxLevel}";
                UiKit.Text(card,"Name",r.displayName+" · "+level,18,13,490,38,27,color);
                UiKit.Text(card,"Value","当前 ×"+Factor(r.currentMult100),1110,12,340,40,29,UiKit.Gold,TextAlignmentOptions.TopRight);
                UiKit.Text(card,"Example","例 "+r.example+" · "+r.condition,18,58,850,36,23,color);
                UiKit.Text(card,"Calculation",$"基础 ×{Factor(r.baseMult100)}  ·  每级 +{Factor(r.incrementPerLevel100)}",900,60,550,35,22,UiKit.Muted,TextAlignmentOptions.TopRight);
                UiKit.Text(card,"Availability",$"组型：{(r.canForm?"当前数量可达":r.formReason)}\n升级：{(r.upgradeEligible?"满足供给条件（仍需商品与金币）":r.upgradeReason)}",18,102,1430,58,21,UiKit.Muted);
            }
            UiKit.Text(p,"HandbookStacking","同点家族只取强化后最高一项；顺子与全蓝/全红相加。散点仅保底。",40,636,1500,35,23,UiKit.Paper);
            UiKit.Text(p,"HandbookRed","红组是独立乘区："+string.Join("  ·  ",Config.redRewards.Select(r=>r.count+"颗 ×"+Factor(r.factor100))),40,684,1500,60,23,UiKit.Gold);
            Canvas.ForceUpdateCanvases();
        }

        private int[] FaceProbability(DieInstance die) => ProbabilityCalculator.BuildUnits(die.faces,false,false,Config,
            ViewState!=null&&ViewState.controllers.Any(c=>c.definitionId=="C34"),ViewState!=null&&ViewState.controllers.Any(c=>c.definitionId=="C35"));
        private static string EnchantShort(string id) => id=="slotless"?"免槽":id=="base5"?"触发 +5":id=="mult120"?"触发 ×1.2":"普通占槽";
        private static string EnchantLabel(string id) => id=="slotless"?"免槽 · 不占普通控制器槽，仍计入总持有10件":id=="base5"?"基础分 +5 · 本次提交触发本卡时加一次":id=="mult120"?"整手 ×1.2 · 本次提交触发本卡时乘一次":"无附魔 · 占用1个普通控制器槽";
        private void EnchantBadge(Transform parent,string id,float x,float y,float size)
        {
            if(string.IsNullOrEmpty(id)||id=="none")return;
            var sprite=enchantIcons?.FirstOrDefault(s=>s!=null&&s.name=="Enchant_"+id);
            var box=UiKit.Box(parent,"EnchantFrame",x-2,y-2,size+4,size+4,id=="slotless"?UiKit.Teal:id=="base5"?UiKit.Gold:UiKit.Blue);
            if(sprite!=null){var image=UiKit.Component<UnityEngine.UI.Image>(UiKit.Rect(box,"Badge",2,2,size,size).gameObject);image.sprite=sprite;image.raycastTarget=false;}
            else UiKit.Text(box,"Symbol",id=="slotless"?"−":id=="base5"?"+5":"×",0,0,size+4,size+4,18,UiKit.Ink,TextAlignmentOptions.Center);
        }
        private string ControllerPurchaseText(ShopOffer offer)
        {
            var old=ViewState.controllers.FirstOrDefault(c=>c.definitionId==offer.contentId);
            var final=ViewState.controllers.Where(c=>c.definitionId!=offer.contentId).Select(c=>c.Clone()).ToList();
            final.Add(offer.controller);
            return $"普通槽 {ControllerRules.OrdinaryUsed(ViewState.controllers)} → {ControllerRules.OrdinaryUsed(final)} / {ViewState.controllerSlots}   ·   总持有 {ViewState.controllers.Count} → {final.Count} / {Config.maxControllerInstances}\n"+
                (old==null?$"基础价 {Config.Controller(offer.contentId).buyPrice} + 附魔溢价 = {Session.GetOfferPrice(offer)}":"替换：新卡标价 "+Session.GetOfferPrice(offer)+" − 旧卡回收 "+Session.GetOfferTradeIn(offer)+" = 净支出 "+Session.GetOfferNetPrice(offer));
        }
        private void ShowController(ControllerInstance item,bool selling)
        {
            if(IsBusy)return;
            var p=OpenModal(NameOf(item.definitionId));Icon(p,item.definitionId,40,110,90);
            UiKit.Text(p,"ControllerEffect",Description(item.definitionId),160,110,1360,175,31);
            EnchantBadge(p,item.enchantmentId,40,325,65);
            UiKit.Text(p,"ControllerEnchantment",EnchantLabel(item.enchantmentId),135,330,1380,85,29,UiKit.Teal);
            UiKit.Text(p,"ControllerCounts",$"普通槽 {ControllerRules.OrdinaryUsed(ViewState.controllers)}/{ViewState.controllerSlots} · 持有 {ViewState.controllers.Count}/{Config.maxControllerInstances}\n当前Run有效；关内不能更换或出售。",40,455,1500,100,26,UiKit.Muted);
            if(selling)UiKit.Button(p,"SellController","确认出售 · +1 金币（附魔不加价）",40,650,1500,70,()=>{CloseModal();Send(new SessionCommand{kind=CommandKind.Sell,itemInstanceId=item.instanceId});},true,true);
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
