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
    // V1.1.1 task W7b (RULES_DELTA sections 7.1, 8, 9.2 and 13): the new player actions of p1.1.1. Presentation only:
    // prices, allowances, legality and the unlock source are the session's answers (W3B2_NOTES 2.2); nothing is
    // recomputed here, and every action is one session command.
    public sealed partial class GameApp
    {
        // ---------------------------------------------------------------- die upgrade commands (7.1)
        /// <summary>The faces a die upgrade command names, in face id order: the array, or the old single field.</summary>
        private static string[] CommandFaces111(SessionCommand cmd)
        {
            if(cmd==null)return Array.Empty<string>();
            if(cmd.targetFaceIds!=null&&cmd.targetFaceIds.Length>0)return cmd.targetFaceIds.Where(f=>!string.IsNullOrEmpty(f)).OrderBy(f=>f,StringComparer.Ordinal).ToArray();
            return string.IsNullOrEmpty(cmd.targetFaceId)?Array.Empty<string>():new[]{cmd.targetFaceId};
        }
        /// <summary>Two faces of a die in two lines each, for the upgrade receipt ("第 2 面  5 点红 / 光滑 · 黄金面 · 权重 2").</summary>
        private static string UpgradeFacesDescription111(DieInstance die,string[] faceIds)
            =>string.Join("\n",faceIds.Select(id=>die.faces.FirstOrDefault(f=>f.faceId==id)).Where(f=>f!=null)
                .Select(f=>FaceName11(f.faceId)+"  "+f.permanentPip+" 点"+ColorText07(f.baseColor)+"\n"+TextureText07(f.texture)+" · "+(f.isGold?"黄金面":"普通面")+" · 权重 "+f.baseWeight));
        // ---------------------------------------------------------------- the self-selected die upgrade (9.2)
        /// <summary>The entry's label, in the style of 指定升级: what is left of this shop's allowance.</summary>
        private static string CustomUpgradeEntryText111(int left)=>left<=0?"本店自选改造已用完":"自选改造 · 还剩 "+left+" 次";
        /// <summary>Why a row of the self-selected list cannot be bought, in the player's words (the session's reason).</summary>
        private string CustomUpgradeReason111(DieUpgradeOption option)
        {
            if(option==null||option.error==null)return null;
            if(option.error==GameSession.SelfDieUpgradeLimitReached)return "本店自选改造已用完";
            if(option.error==GameSession.UpgradeTargetRejected)return option.maxSelectableFaces<=0?"没有能改造的骰子":"没有能改造的骰面";
            if(option.error=="金币不足。")return "还差 "+PlayerNumberFormat.Gold(Math.Max(0,option.price-ViewState.gold))+" 金币";
            if(Session.IsLootPending)return "先领取首领战利品";
            return PlayerShopReason16(option.error,null).TrimEnd('。');
        }
        /// <summary>
        /// The self-selected list (RULES_DELTA 9.2): every die upgrade with its short text, its live price (the premium and
        /// the discount card included) and, when it cannot be bought, the reason. A row's price button starts the same
        /// die and face pick as a shelf upgrade; the confirmation there sends CustomDieUpgrade.
        /// </summary>
        public void ShowCustomUpgrades111()
        {
            if(ViewState?.phase!="Shop"||IsBusy)return;
            var options=Session.CustomDieUpgradeOptions();int left=Session.CustomDieUpgradesRemaining;
            var p=OpenModal("自选改造 · 本店还剩 "+left+" 次");
            const float row=96;
            var list=UiKit.Scroll(p,"CustomUpgradeList111",40,100,1500,620,Math.Max(600,options.Length*row));
            for(int i=0;i<options.Length;i++)
            {
                var option=options[i];string id=option.upgradeId;string reason=CustomUpgradeReason111(option);float y=i*row;
                var name=UiKit.Text(list,"CustomName"+id,NameOf(id)+"   "+ShortText11(id),12,y+6,1070,44,27);
                name.textWrappingMode=TextWrappingModes.NoWrap;name.overflowMode=TextOverflowModes.Ellipsis;
                string faces=option.maxSelectableFaces<=0?"整颗骰子":option.maxSelectableFaces==1?"选 1 个面":"选 1 个或 2 个面，价格相同";
                var note=UiKit.Text(list,"CustomNote"+id,reason==null?faces:"现在不能买："+reason,12,y+50,1070,36,20,UiKit.Muted);
                note.textWrappingMode=TextWrappingModes.NoWrap;note.overflowMode=TextOverflowModes.Ellipsis;
                UiKit.Button(list,"CustomBuy"+id,option.price+" 金币",1110,y+8,340,65,()=>BeginCustomFacePick14(id),reason==null,true);
            }
            if(options.Length==0)UiKit.Text(list,"NoCustom111","现在没有可以自选的改造。",15,30,1400,90,28,UiKit.Muted);
        }
        /// <summary>What a self-selected upgrade did: the faces' changes, then what is left of the shop's allowance.</summary>
        private string CustomUpgradeSummary111(SessionCommand cmd,RunState before,RunState after)
        {
            string changes=PurchasedDieSummary07(cmd,before,after);
            int left=after?.shop==null?0:Math.Max(0,Config.shop.selfDieUpgradeLimit-after.shop.selfDieUpgradeUsed);
            string head="自选改造："+NameOf(cmd.upgradeId)+"（−"+PlayerNumberFormat.Gold(Math.Max(0,(before?.gold??0)-(after?.gold??0)))+" 金币，本店还剩 "+left+" 次）";
            return string.IsNullOrEmpty(changes)?head:head+" · "+changes;
        }
    }
}
