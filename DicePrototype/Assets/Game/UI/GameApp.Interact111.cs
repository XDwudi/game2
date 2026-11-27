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
        // ---------------------------------------------------------------- the key ring and taking a locked die back (8)
        // View state only: which locked die is selected for 撤回, and whether the key ring's 使用 asked the player to point
        // at one. Neither touches the session; the one command is UnlockDie, sent by the 撤回 button.
        private string unlockPick111;
        private bool unlockArmed111;
        /// <summary>The locked die selected for 撤回, or null.</summary>
        public string UnlockPick111=>unlockPick111;
        /// <summary>True after the key ring's 使用: the locked dice that can be taken back are marked.</summary>
        public bool UnlockArmed111=>unlockArmed111;
        private bool UnlockPhase111=>ViewState!=null&&ViewState.phase=="Selecting"&&ViewState.hand!=null&&!IsPresenting07;
        /// <summary>The session's answer for one locked die (or for any, with null) on the state that is on screen. Dice
        /// selected but not yet locked block the command, as the session's own quote says.</summary>
        private UnlockView UnlockQuote111(string dieId)
        {
            var view=Session.UnlockQuoteIn(ViewState,dieId);
            if(view.available&&Session.PendingIds.Length>0){view.available=false;view.error="先锁定选中的骰子，或取消选择。";}
            return view;
        }
        /// <summary>The player holds something that takes a locked die back (a key ring, or 回心转意 in effect this hand,
        /// used or not), so a locked die answers a click with the 撤回 choice or the reason it is not possible.</summary>
        private bool HoldsUnlockSource111
        {
            get
            {
                if(ViewState?.hand==null)return false;
                var view=Session.UnlockQuoteIn(ViewState);
                return view.freeAvailable||view.keyRingUsesTotal>0||ViewState.hand.unlockCardUsed;
            }
        }
        private void SyncUnlock111()
        {
            if(!UnlockPhase111||ViewState.hand.orderedLockedIds.Count==0){unlockPick111=null;unlockArmed111=false;return;}
            if(unlockPick111!=null&&!ViewState.hand.orderedLockedIds.Contains(unlockPick111))unlockPick111=null;
        }
        /// <summary>The line over the locked dice: they can be taken back while the session says some die can.</summary>
        private string LockRuleText111()
        {
            SyncUnlock111();
            return UnlockPhase111&&UnlockQuote111(null).available?"已锁定 · 点一颗骰子可以撤回":"已锁定 · 本手不可移回骰池";
        }
        /// <summary>A click on a locked die. With a source in hand it selects the die (again: clears the selection) and
        /// the action row shows 撤回 or the reason; nothing is sent. Without one the click is not handled (the die's
        /// details open as before).</summary>
        private bool UnlockClick111(string dieId)
        {
            if(!UnlockPhase111||IsBusy||Session.HasPendingSave||!HoldsUnlockSource111)return false;
            unlockArmed111=false;
            unlockPick111=unlockPick111==dieId?null:dieId;
            // The row's hint is the unlock line now, not the summary of the previous action.
            LastTransactionSummary07="";
            Refresh();return true;
        }
        /// <summary>The key ring's 使用 (RULES_DELTA 8: the item's button is the same unlock): no command; the player is
        /// asked to point at a locked die, and the dice that can be taken back are marked.</summary>
        private void ArmKeyRing111()
        {
            CloseModal();
            unlockArmed111=true;unlockPick111=null;LastTransactionSummary07="";
            Refresh();
        }
        public void CancelUnlockPick111(){if(unlockPick111==null&&!unlockArmed111)return;unlockPick111=null;unlockArmed111=false;Refresh();}
        /// <summary>"回心转意（本手免费）" or "钥匙串（剩 N 次）": the source the rules will use, never a choice.</summary>
        private string UnlockSourceText111(UnlockView view)
            =>view.sourceId=="C154"?NameOf("C154")+"（本手免费）":view.sourceId=="I13"?NameOf("I13")+"（剩 "+view.keyRingUses+" 次）":"";
        /// <summary>Why the selected die cannot be taken back, in the player's words.</summary>
        private string UnlockReason111(UnlockView view)
        {
            if(view==null||view.available)return null;
            if(view.error==GameSession.ItemsForbidden)return view.freeAvailable?null:"首领减益“徒手”：本关不能用"+NameOf("I13")+(ViewState.hand.unlockCardUsed?"，"+NameOf("C154")+"本手的免费次数也用过了。":"。");
            if(view.error==GameSession.UnlockNoSource)return ViewState.hand.unlockCardUsed?NameOf("C154")+"本手的免费次数用过了，也没有"+NameOf("I13")+"。":"没有可用的撤回次数。";
            if(view.error==GameSession.UnlockNeedsResults)return "掷骰后才能撤回已锁定的骰子。";
            if(view.error==GameSession.UnlockTargetInvalid)return "选一颗已锁定的骰子。";
            return view.error;
        }
        /// <summary>
        /// The action row while a locked die is selected: the primary button is 撤回 with the source the rules will use
        /// written on it (an explicit button: the click on the die only selected it), the second one clears the
        /// selection, and the hint says what will happen or why it cannot. Returns false when no die is selected.
        /// </summary>
        private bool UnlockActions111(Layout11 l,out string reason)
        {
            reason=null;SyncUnlock111();
            if(unlockPick111==null)return false;
            string id=unlockPick111;var view=UnlockQuote111(id);string die=DieName11(id);
            if(view.available)
            {
                reason=view.sourceId=="C154"?"撤回"+die+"：用"+NameOf("C154")+"，本手免费 1 次。骰子回到骰池，可以重掷或再锁定。"
                    :"撤回"+die+"：用掉"+NameOf("I13")+" 1 次（现在剩 "+view.keyRingUses+" 次），不能反悔。骰子回到骰池，可以重掷或再锁定。";
                // W9a fix: at 150 % "撤回 · 钥匙串（剩 3 次）" is wider than the primary slot and ran over the button's edges; the
                // button then grows to the right, into the free room before 结束本关, keeping its text size.
                string unlockLabel="撤回 · "+UnlockSourceText111(view);
                float unlockNeed=PreferredWidth11(unlockLabel,Large11?22:28)+48;
                if(unlockNeed>l.primary.width)l.primary=new Rect(l.primary.x,l.primary.y,Mathf.Min(unlockNeed,l.end.x-16-l.primary.x),l.primary.height);
                PrimaryButton11("UnlockDie111",unlockLabel,()=>SendUnlock111(id),true,l);
            }
            else
            {
                reason="不能撤回"+die+"："+(UnlockReason111(view)??"");
                PrimaryButton11("UnlockDie111","撤回",()=>{},false,l);
            }
            SecondaryButton11("UnlockCancel111","取消选择",CancelUnlockPick111,l);
            return true;
        }
        private void SendUnlock111(string dieId)
        {
            if(IsBusy||Session.HasPendingSave)return;
            unlockPick111=null;unlockArmed111=false;
            Send(new SessionCommand{kind=CommandKind.UnlockDie,targetDieId=dieId});
        }
        /// <summary>The hint of the action row after the key ring's 使用, until a die is selected.</summary>
        private string UnlockHint111(string reason)
        {
            SyncUnlock111();
            if(!unlockArmed111||unlockPick111!=null)return reason;
            var any=UnlockQuote111(null);
            return any.available?"点一颗已锁定的骰子来撤回。":"现在不能撤回："+(UnlockReason111(any)??"");
        }
        /// <summary>The marks of one locked slot: a gold ring on the selected die; after the key ring's 使用, a thin ring and
        /// "可撤回" on every die the session would take back.</summary>
        private void DrawUnlockMark111(string dieId,Rect slot)
        {
            if(!UnlockPhase111||IsBusy)return;
            if(unlockPick111==dieId){UiKit.Ring(main,"UnlockPick111_"+dieId,slot.x-4,slot.y-4,slot.width+8,slot.height+8,UiKit.Gold);return;}
            if(!unlockArmed111||!UnlockQuote111(dieId).available)return;
            UiKit.Outline(main,"UnlockTarget111_"+dieId,slot.x-2,slot.y-2,slot.width+4,slot.height+4,UiKit.Gold,2);
            float size=Body11(16),w=slot.width-8,h=Line11(size);
            var tag=Surface11(main,"UnlockTargetTag111_"+dieId,slot.x+4,slot.yMax-h-10,w,h+4,"ui_chip_label",UiKit.PanelLight);
            NoWrap11(UiKit.Text(tag,"Text","可撤回",0,0,w,h+4,size,LabelOn11(tag,UiKit.TextColor),TextAlignmentOptions.Center));
        }
        /// <summary>The uses left on an item that has several (the key ring's 1 to 3), on the bottom-right corner of its
        /// slot. V1.1.1 W9b: the box sat inside the slot and covered the icon's lower right (the key ring's padlock); it is
        /// now a tight box hung over the slot's corner, <paramref name="outX"/> and <paramref name="outY"/> units past its
        /// edges (less than the gap to the next slot and to the row below), so only a sliver of the picture is under it.
        /// The digit keeps the body size.</summary>
        private void ItemUses111(Transform icon,OwnedItem item,float size,float outX,float outY)
        {
            if(item==null||Config.Consumable(item.contentId).initialUses<=1)return;
            string count=item.usesRemaining.ToString(CultureInfo.InvariantCulture);
            float font=Body11(16),px=font*UiKit.FontScale,w=Mathf.Max(22,Mathf.Ceil(PreferredWidth11(count,font))+10),h=Mathf.Min(size,Mathf.Ceil(px*1.05f)+4),line=Line11(font);
            var mark=UiKit.Frame(icon,"Uses111",size-w+outX,size-h+outY,w,h,UiKit.Background,UiKit.PanelLight);
            // The label keeps a whole line's height, centred on the box: the digit's ink sits inside the box.
            NoWrap11(UiKit.Text(mark,"Count",count,0,(h-line)*.5f,w,line,font,LabelOn11(mark,UiKit.Paper),TextAlignmentOptions.Center)).raycastTarget=false;
        }
        /// <summary>Why no locked die can be taken back right now (the key ring's state line and its lock mark), or null
        /// when the session would accept an unlock. With a free unlock left the rules use that first.</summary>
        private string KeyRingState111()
        {
            var view=UnlockQuote111(null);
            return view.available?null:UnlockReason111(view);
        }
        /// <summary>How the key ring is used, for its details (it has no 使用 command of its own).</summary>
        private string KeyRingUsage111(OwnedItem item)
            =>"点一颗已锁定的骰子，再按“撤回”，它就回到骰池（可以重掷，也可以再锁定）。每次用掉 1 次，还剩 "+item.usesRemaining+" 次，用完才消失。持有"+NameOf("C154")+"时先用它每手 1 次的免费撤回。";
        /// <summary>The records of an accepted UnlockDie in words, one line each: the die that came back and its source,
        /// then the key ring's uses before and after (no such record for the free unlock).</summary>
        private string UnlockSummary111(CommandReceipt receipt)
        {
            if(receipt?.effects==null)return "";
            var lines=new List<string>();
            foreach(var e in receipt.effects)
            {
                if(e.kind=="DieUnlocked")lines.Add("已撤回"+DieName11(e.dieId)+"（"+(e.sourceId=="C154"?NameOf("C154")+"，本手的免费次数已用":NameOf("I13"))+"）");
                else if(e.kind=="ItemUsed"&&e.sourceId=="I13")lines.Add(e.newValue<=0?NameOf("I13")+"用完了，道具栏空出 1 格":NameOf("I13")+"还剩 "+e.newValue+" 次（"+e.oldValue+" → "+e.newValue+"）");
            }
            return string.Join(" · ",lines);
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
