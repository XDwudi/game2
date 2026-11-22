using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DiceCraft.Core;
using UnityEngine.InputSystem;

namespace DiceCraft
{
    // v1.1 (task W09, text proposal P0): names that replace internal ids on player screens, the practice-mode
    // labels, localized option and key names, and the hidden debug entry. Presentation only: no rule, save
    // or session state is read differently here.
    public sealed partial class GameApp
    {
        /// <summary>"D006" becomes "骰子 6": dice are numbered in the order they were acquired (text proposal 2.1).</summary>
        public static string DieName11(string dieId)
        {
            int number=SerialOf11(dieId,'D');
            return number>0?"骰子 "+number.ToString(CultureInfo.InvariantCulture):"骰子";
        }
        /// <summary>"F02" becomes "第 2 面".</summary>
        public static string FaceName11(string faceId)
        {
            int number=SerialOf11(faceId,'F');
            return number>0?"第 "+number.ToString(CultureInfo.InvariantCulture)+" 面":"骰面";
        }
        /// <summary>"骰子 3 的第 2 面".</summary>
        public static string DieFaceName11(string dieId,string faceId)=>DieName11(dieId)+" 的"+FaceName11(faceId);
        private static int SerialOf11(string id,char prefix)
        {
            if(string.IsNullOrEmpty(id)||id.Length<2||id[0]!=prefix)return 0;
            return int.TryParse(id.Substring(1),NumberStyles.None,CultureInfo.InvariantCulture,out int value)?value:0;
        }

        /// <summary>The settings label of a presentation speed (the values stay the stored English keys).</summary>
        public static string SpeedLabel11(string mode)=>mode=="Instant"?"即时":mode=="Fast"?"快速":mode=="Normal"?"标准":mode;

        /// <summary>A key as printed for the player instead of its Input System enum name.</summary>
        private static string KeyLabel11(Key key)
        {
            switch(key)
            {
                case Key.Enter:return "回车";
                case Key.NumpadEnter:return "小键盘回车";
                case Key.Backspace:return "退格";
                case Key.Space:return "空格";
                case Key.LeftArrow:return "←";
                case Key.RightArrow:return "→";
                case Key.UpArrow:return "↑";
                case Key.DownArrow:return "↓";
                case Key.PageUp:return "PgUp";
                case Key.PageDown:return "PgDn";
                case Key.Backquote:return "`";
                case Key.Quote:return "'";
                case Key.Semicolon:return ";";
                case Key.Comma:return ",";
                case Key.Period:return ".";
                case Key.Slash:return "/";
                case Key.Backslash:return "\\";
                case Key.LeftBracket:return "[";
                case Key.RightBracket:return "]";
                case Key.Minus:return "-";
                case Key.Equals:return "=";
                case Key.NumpadPlus:return "小键盘 +";
                case Key.NumpadMinus:return "小键盘 -";
                case Key.NumpadMultiply:return "小键盘 *";
                case Key.NumpadDivide:return "小键盘 /";
                case Key.NumpadPeriod:return "小键盘 .";
                case Key.NumpadEquals:return "小键盘 =";
                case Key.ContextMenu:return "菜单键";
            }
            int numpad=(int)key-(int)Key.Numpad0;
            if(numpad>=0&&numpad<=9)return "小键盘 "+numpad.ToString(CultureInfo.InvariantCulture);
            return key.ToString();
        }

        /// <summary>
        /// Practice started from the handbook. The session still marks it as a separate debug-slot run (its save and rule
        /// limits are unchanged); only its labels and the first-run guide follow the ordinary game (text proposal 5 #5).
        /// </summary>
        private bool IsPracticeRun11=>tutorial&&ViewState?.debugWorkspace==true;

        /// <summary>A card named for the player: its name, plus its position when several owned cards share that name.</summary>
        private string CardLabel11(string instanceId,IList<ControllerInstance> cards=null)
        {
            cards=cards??ViewState?.controllers;
            var label=CardLabelIn11(instanceId,cards);
            if(label!=null)return label;
            var before=lastPresentationBefore07?.controllers.FirstOrDefault(c=>c.instanceId==instanceId);
            return before==null?"一张卡牌":NameOf(before.definitionId);
        }
        private string CardLabelIn11(string instanceId,IList<ControllerInstance> cards)
        {
            if(string.IsNullOrEmpty(instanceId)||cards==null)return null;
            var card=cards.FirstOrDefault(c=>c?.instanceId==instanceId);
            if(card==null)return null;
            string name=NameOf(card.definitionId);
            if(cards.Count(c=>c?.definitionId==card.definitionId)>1)
            {
                int index=cards.IndexOf(card);
                name+="（第 "+(index+1).ToString(CultureInfo.InvariantCulture)+" 张）";
            }
            return name;
        }
        /// <summary>"第 N 张": the ordinal printed on the card, or null when the card is not in that row.</summary>
        private static string CardPosition11(IList<ControllerInstance> cards,string instanceId)
        {
            if(cards==null||string.IsNullOrEmpty(instanceId))return null;
            for(int i=0;i<cards.Count;i++)if(cards[i]!=null&&cards[i].instanceId==instanceId)return "第 "+(i+1).ToString(CultureInfo.InvariantCulture)+" 张";
            return null;
        }

        /// <summary>The fallback for a missing illustration: the first character of the name, never the content id.</summary>
        private string MissingIconLabel11(string id)
        {
            if(string.IsNullOrEmpty(id))return "—";
            try{string name=NameOf(id);return string.IsNullOrEmpty(name)?"—":name.Substring(0,1);}
            catch(System.Exception){return "—";}
        }

        /// <summary>The target of a boss card as the stage intel shows it: scaled by the modifier unless it was lifted.</summary>
        private static string BossTarget10(CycleStageView view,bool lifted)=>lifted?view.normalTarget:view.bossTarget;
        /// <summary>v1.1 (text proposal 5 #7): the shop's next-stage target uses the same computation as "查看关卡".</summary>
        private string ShopNextTarget11(int next)
        {
            var view=Session.CycleView().FirstOrDefault(v=>v.stageIndex==next);
            if(view==null)return Config.stages[next].normalTarget;
            return view.stageKind=="boss"?BossTarget10(view,view.modifierNullified):view.normalTarget;
        }

        // ---------------------------------------------------------------- hidden debug entry (text proposal 5 #2)
        private bool debugAccessRevealed11;
        private bool SettingsPageOpen11=>modal&&modalIdentity07=="settings16";
        /// <summary>
        /// Shows the debug password row on the settings page for the rest of this session. Players never see it; the
        /// developer reveals it with Ctrl+Shift+D while the settings page is open. Revealing does not unlock anything.
        /// </summary>
        public void RevealDebugAccess11()
        {
            if(debugAccessRevealed11)return;
            debugAccessRevealed11=true;
            if(SettingsPageOpen11){rebindingAction07=null;settingsNotice07="";settingsTab07=2;ShowSettings07();}
        }
        private bool DebugAccessVisible11=>debugAccessRevealed11||DebugUnlocked;
        private bool DebugRevealChord11(Keyboard keyboard)
        {
            if(keyboard==null||debugAccessRevealed11||!SettingsPageOpen11||!keyboard.dKey.wasPressedThisFrame)return false;
            bool ctrl=keyboard.leftCtrlKey.isPressed||keyboard.rightCtrlKey.isPressed,shift=keyboard.leftShiftKey.isPressed||keyboard.rightShiftKey.isPressed;
            if(!ctrl||!shift)return false;
            RevealDebugAccess11();return true;
        }
    }
}
