using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private int settingsTab07,quality07=1;
        private float musicVolume07=.35f,sfxVolume07=.7f,uiVolume07=.5f;
        private bool scanlines07;
        private string rebindingAction07,settingsNotice07="";
        private UnityEngine.UI.ScrollRect settingsScroll07;
        private int settingsRenderedTab07=-1;
        private readonly Dictionary<string,Key> bindings07=new Dictionary<string,Key>();
        private static readonly string[] BindingActions07={"Roll","Lock","Submit","Clear","Handbook"};
        private static readonly string[] BindingNames07={"掷骰","确认锁定","提交","清除待选","牌型手册"};
        private static readonly Key[] BindingDefaults07={Key.R,Key.L,Key.Enter,Key.Backspace,Key.H};

        private void InitializeSettingsPreferences07()
        {
            musicVolume07=Mathf.Clamp01(PlayerPrefs.GetFloat("art07.music",.35f));
            sfxVolume07=Mathf.Clamp01(PlayerPrefs.GetFloat("art07.sfx",.7f));
            uiVolume07=Mathf.Clamp01(PlayerPrefs.GetFloat("art07.ui",.5f));
            quality07=Mathf.Clamp(PlayerPrefs.GetInt("art07.quality",1),0,2);
            scanlines07=PlayerPrefs.GetInt("art07.scanlines",0)==1;
            EnsureBindings07();
        }
        private void EnsureBindings07()
        {
            if(bindings07.Count>0)return;
            for(int i=0;i<BindingActions07.Length;i++)
            {
                string saved=PlayerPrefs.GetString("art07.key."+BindingActions07[i],BindingDefaults07[i].ToString());
                Key key;if(!Enum.TryParse(saved,out key)||!BindingAllowed07(key)||bindings07.ContainsValue(key))key=BindingDefaults07[i];
                // A previously edited collision is resolved to a remaining valid default, never two actions at once.
                if(bindings07.ContainsValue(key))key=BindingDefaults07.First(k=>!bindings07.ContainsValue(k));
                bindings07[BindingActions07[i]]=key;
            }
        }
        private static bool BindingAllowed07(Key key)=>key!=Key.None&&key!=Key.Escape&&key!=Key.LeftShift&&key!=Key.RightShift&&key!=Key.LeftCtrl&&key!=Key.RightCtrl&&key!=Key.LeftAlt&&key!=Key.RightAlt&&key!=Key.LeftMeta&&key!=Key.RightMeta&&!((int)key>=(int)Key.Digit1&&(int)key<=(int)Key.Digit0);
        private bool Art07KeyPressed(string action)
        {
            EnsureBindings07();return Keyboard.current!=null&&bindings07.TryGetValue(action,out var key)&&Keyboard.current[key].wasPressedThisFrame;
        }
        private bool Art07HandleRebinding()
        {
            var keyboard=Keyboard.current;if(keyboard==null)return rebindingAction07!=null;
            if(rebindingAction07!=null)
            {
                if(!modal){rebindingAction07=null;return false;}
                foreach(var key in keyboard.allKeys)
                {
                    if(!key.wasPressedThisFrame)continue;
                    if(key.keyCode==Key.Escape){rebindingAction07=null;settingsNotice07="已取消改键。";ShowSettings07();return true;}
                    if(!BindingAllowed07(key.keyCode)){settingsNotice07="请选择普通单键；Esc、数字选择键和单独修饰键保留。";ShowSettings07();return true;}
                    if(bindings07.Any(b=>b.Key!=rebindingAction07&&b.Value==key.keyCode)){settingsNotice07="该按键已用于另一动作，请选择其他按键。";ShowSettings07();return true;}
                    bindings07[rebindingAction07]=key.keyCode;PlayerPrefs.SetString("art07.key."+rebindingAction07,key.keyCode.ToString());
                    rebindingAction07=null;settingsNotice07="快捷键已保存。";ShowSettings07();return true;
                }
                return true;
            }
            // The project disables automatic navigation events; provide precise keyboard slider steps here.
            var selected=EventSystem.current?.currentSelectedGameObject;
            var slider=selected==null?null:selected.GetComponent<UnityEngine.UI.Slider>();
            if(modal&&slider!=null)
            {
                if(keyboard.leftArrowKey.wasPressedThisFrame||keyboard.downArrowKey.wasPressedThisFrame)slider.value=Mathf.Max(0,slider.value-.02f);
                if(keyboard.rightArrowKey.wasPressedThisFrame||keyboard.upArrowKey.wasPressedThisFrame)slider.value=Mathf.Min(1,slider.value+.02f);
                return !keyboard.escapeKey.wasPressedThisFrame;
            }
            return false;
        }
        private string Art07BindingLabel(string action){EnsureBindings07();return bindings07[action].ToString();}
        private void Pause()
        {
            displayPanel=false;debugPanel=false;controllerOrderPanel=false;rebindingAction07=null;
            var p=Art07Modal("暂停",640,220,640,640);
            UiKit.Text(p,"Info","已提交的结果会保留。\n返回后继续当前对局。",24,96,592,124,26,UiKit.Muted);
            UiKit.ActionButton(p,"Resume","返回对局",24,244,592,72,CloseModal,UiKit.Paper);
            UiKit.Button(p,"Settings","设置",24,332,592,72,ShowSettings07);
            UiKit.Button(p,"MainMenu",Session?.HasPendingSave==true?"处理保存问题":"保存并返回主菜单",24,420,592,72,()=>{if(Session?.HasPendingSave==true)SaveFailure();else ReturnMenu();});
            if(IsBusy||SettlementRevealPlaying07)UiKit.Button(p,"Skip","显示最终结果",24,508,592,72,()=>{CloseModal();SkipVisual();});
        }
        private void ShowSettings07()
        {
            EnsureBindings07();
            float scrollPosition=settingsScroll07!=null&&settingsRenderedTab07==settingsTab07?settingsScroll07.verticalNormalizedPosition:1;
            var p=Art07Modal("设置",320,96,1280,888);
            string[] tabs={"声音","画面","操作与辅助"};
            for(int i=0;i<3;i++){int tab=i;UiKit.FlatButton(p,"SettingsTab"+i,tabs[i],24+i*416,80,400,64,()=>{rebindingAction07=null;settingsNotice07="";settingsTab07=tab;ShowSettings07();},true,tab==settingsTab07);}
            float contentHeight=settingsTab07==2?Compact07?2120:1460:Compact07?1000:660;
            var list=UiKit.Scroll(p,"SettingsPage"+settingsTab07,24,160,1232,616,contentHeight);
            if(settingsTab07==0)
            {
                float stride=Compact07?172:112;
                Volume07(list,"Master","总音量",volume,0,v=>{volume=v;PlayerPrefs.SetFloat("volume",v);ApplyAudioPreferences07();});
                Volume07(list,"Music","音乐",musicVolume07,stride,v=>{musicVolume07=v;PlayerPrefs.SetFloat("art07.music",v);ApplyAudioPreferences07();});
                Volume07(list,"SFX","音效",sfxVolume07,stride*2,v=>{sfxVolume07=v;PlayerPrefs.SetFloat("art07.sfx",v);ApplyAudioPreferences07();});
                Volume07(list,"UI","界面声音",uiVolume07,stride*3,v=>{uiVolume07=v;PlayerPrefs.SetFloat("art07.ui",v);ApplyAudioPreferences07();});
                UiKit.Button(list,"Audition","短试听",824,stride*4+8,360,64,()=>PlaySound("lock"));
                UiKit.Text(list,"AudioHelp","左右方向键微调已选滑块；静音保留所有视觉提示。",12,stride*4+94,1172,96,24,UiKit.Muted);
            }
            else if(settingsTab07==1)
            {
                UiKit.Button(list,"DisplaySettings","自动尺寸 / 分辨率 / 显示器 / 显示模式",12,8,1172,80,ShowDisplaySettings);
                SettingsChoice07(list,"Quality","画面质量",new[]{"低","标准","高"}[quality07],124,()=>{quality07=(quality07+1)%3;PlayerPrefs.SetInt("art07.quality",quality07);ApplyArtPreferences07();ShowSettings07();});
                SettingsChoice07(list,"Grain","背景颗粒",grain07?"开":"关",Compact07?292:236,()=>{grain07=!grain07;PlayerPrefs.SetInt("art07.grain",grain07?1:0);ApplyArtPreferences07();ShowSettings07();});
                SettingsChoice07(list,"Scanlines","桌布扫描线",scanlines07?"开":"关",Compact07?460:348,()=>{scanlines07=!scanlines07;PlayerPrefs.SetInt("art07.scanlines",scanlines07?1:0);ApplyArtPreferences07();ShowSettings07();});
                UiKit.Text(list,"QualityHint","画质只影响表现，保留骰点、锁定与倍率来源。\n低画质或减少动态时不显示扫描线。\n显示设置使用独立的 15 秒确认与回退。",12,Compact07?640:476,1172,160,24,UiKit.Muted);
            }
            else
            {
                float stride=Compact07?156:100,y=0;
                SettingsChoice07(list,"Speed","演出速度",instant07?"Instant":fast?"Fast":"Normal",y,()=>{if(instant07){instant07=false;fast=false;}else if(fast){instant07=true;fast=false;}else fast=true;PlayerPrefs.SetInt("fast",fast?1:0);PlayerPrefs.SetInt("art07.instant",instant07?1:0);ApplyArtPreferences07();ShowSettings07();});y+=stride;
                SettingsChoice07(list,"Reduced","减少动态",reduced?"开":"关",y,()=>{reduced=!reduced;UiKit.ReducedMotion=reduced;PlayerPrefs.SetInt("reduced",reduced?1:0);ApplyArtPreferences07();ShowSettings07();});y+=stride;
                SettingsChoice07(list,"TextScale","文字缩放",textScale07+"%",y,()=>{textScale07=textScale07==100?125:textScale07==125?150:100;UiKit.FontScale=textScale07/100f;PlayerPrefs.SetInt("art07.textScale",textScale07);CancelDieDrag07();CancelControllerDrag();Refresh();ShowSettings07();});y+=stride;
                SettingsChoice07(list,"ColorAssist","红圆 / 蓝菱辅助",colorAssist07?"开":"关",y,()=>{colorAssist07=!colorAssist07;PlayerPrefs.SetInt("art07.colorAssist",colorAssist07?1:0);ApplyArtPreferences07();Refresh();ShowSettings07();});y+=stride;
                SettingsChoice07(list,"Shake","桌面震动",shake07?"开":"关",y,()=>{shake07=!shake07;PlayerPrefs.SetInt("art07.shake",shake07?1:0);ApplyArtPreferences07();ShowSettings07();});y+=stride;
                UiKit.Text(list,"KeysTitle","快捷键 · 点击后按一个新按键",12,y,1172,64,28,UiKit.Paper);y+=76;
                for(int i=0;i<BindingActions07.Length;i++)
                {
                    string action=BindingActions07[i];int index=i;
                    UiKit.Text(list,"KeyLabel"+action,BindingNames07[index],12,y,420,64,24,UiKit.Muted);
                    UiKit.Button(list,"Bind"+action,rebindingAction07==action?"等待按键…":Art07BindingLabel(action),460,y,724,64,()=>{rebindingAction07=action;settingsNotice07="按 Esc 取消改键。";ShowSettings07();});y+=76;
                }
                UiKit.Text(list,"ReservedKeys","Esc 关闭最上层；数字键保留骰子选择。输入框与模态层拦截下层快捷键。",12,y,1172,84,22,UiKit.Muted);y+=104;
                UiKit.Button(list,"ResetKeys","恢复默认快捷键",460,y,724,64,()=>{for(int i=0;i<BindingActions07.Length;i++)PlayerPrefs.DeleteKey("art07.key."+BindingActions07[i]);bindings07.Clear();EnsureBindings07();settingsNotice07="已恢复默认快捷键。";ShowSettings07();});y+=96;
                DrawDebugAccess07(list,y);y+=260;
                list.sizeDelta=new Vector2(list.sizeDelta.x,y);
            }
            UiKit.Text(p,"SettingsNotice",settingsNotice07,24,790,812,72,22,UiKit.Gold);
            UiKit.Button(p,"SettingsDone","完成",864,800,392,64,()=>{rebindingAction07=null;PlayerPrefs.Save();CloseModal();});
            settingsScroll07=list.GetComponentInParent<UnityEngine.UI.ScrollRect>();settingsScroll07.verticalNormalizedPosition=scrollPosition;settingsRenderedTab07=settingsTab07;
        }
        private void SettingsChoice07(Transform parent,string id,string label,string current,float y,Action change)
        {
            UiKit.Text(parent,id+"Label",label,12,y,Compact07?1172:420,64,26,UiKit.Paper);
            UiKit.Button(parent,id,current+"  >",Compact07?12:460,y+(Compact07?68:0),Compact07?1172:724,68,change);
        }
        private void Volume07(Transform p,string id,string label,float value,float y,Action<float> changed)
        {
            UiKit.Text(p,id+"Label",label,12,y,420,64,28,UiKit.Paper);
            float x=Compact07?12:460,sy=y+(Compact07?72:0),width=Compact07?984:524;
            var root=UiKit.Box(p,id+"Slider",x,sy,width,64,Color.clear,true);
            UiKit.Box(root,"Track",0,28,width,8,UiKit.PanelLight);
            var fillArea=UiKit.Rect(root,"FillArea",16,28,width-32,8);
            var fill=UiKit.Box(fillArea,"Fill",0,0,width-32,8,UiKit.Gold);
            fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.offsetMin=fill.offsetMax=Vector2.zero;
            var handleArea=UiKit.Rect(root,"HandleArea",16,12,width-32,40);
            var handle=UiKit.Box(handleArea,"Handle",0,0,32,0,UiKit.Paper);
            handle.pivot=new Vector2(.5f,.5f);handle.anchoredPosition=Vector2.zero;
            var slider=UiKit.Component<UnityEngine.UI.Slider>(root.gameObject);slider.onValueChanged.RemoveAllListeners();slider.minValue=0;slider.maxValue=1;slider.wholeNumbers=false;
            slider.fillRect=fill;slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();slider.direction=UnityEngine.UI.Slider.Direction.LeftToRight;
            // Pool layout resets the driven anchors; an unchanged value otherwise skips Slider.UpdateVisuals.
            // Both assignments happen before rendering and without callbacks.
            slider.SetValueWithoutNotify(value==0?1:0);slider.SetValueWithoutNotify(value);
            var number=UiKit.Text(p,id+"Value",Mathf.RoundToInt(value*100)+"%",1020,sy,164,64,28,UiKit.Gold,TextAlignmentOptions.Center);
            slider.onValueChanged.AddListener(v=>{number.text=Mathf.RoundToInt(v*100)+"%";changed(v);});
        }
        private void DrawDebugAccess07(Transform p,float y)
        {
            UiKit.Text(p,"DebugAccessLabel","调试口令",12,y,1172,52,24,UiKit.Muted);
            var field=FreeInput(p,"DebugPassword","输入口令","",12,y+60,756,68,null,100);
            var feedback=UiKit.Text(p,"DebugPasswordError",DebugUnlocked?"已解锁；普通对局保持原状。":"只解锁入口，不修改普通对局。",12,y+144,1172,72,22,UiKit.Muted);
            Action enter=()=>{if(debugPasswordSubmitFrame==Time.frameCount)return;debugPasswordSubmitFrame=Time.frameCount;if(UnlockDebug(field.text)){feedback.text="已解锁；可打开组合试验。";ShowDebugEntry07();}else feedback.text="口令不匹配。请核对大小写和空格。";};
            field.onSubmit.AddListener(_=>enter());
            UiKit.Button(p,"UnlockDebug",DebugUnlocked?"组合试验":"确认口令",800,y+60,384,68,()=>{if(DebugUnlocked)ShowDebugEntry07();else enter();});
        }
    }
}
