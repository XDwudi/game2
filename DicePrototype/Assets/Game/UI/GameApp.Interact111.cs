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
