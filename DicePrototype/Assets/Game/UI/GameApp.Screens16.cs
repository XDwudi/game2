using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace DiceCraft
{
    public sealed partial class GameApp
    {
        private const string SeedError16="种子只能是 0 到 4294967295 的整数。";
        private const string FontCredits16="字体：Noto Sans CJK SC、Noto Sans SC、Noto Serif SC、Ma Shan Zheng、ZCOOL KuaiLe、ZCOOL XiaoWei，均以 SIL Open Font License 1.1 授权；许可证见游戏目录中的 Licenses 文件夹。";

        private void DrawMenuWordmark16(bool showRules)
        {
            var mark=UiKit.PaperFrame(main,"MenuWordmark",128,86,1000,334,UiKit.Paper);
            UiKit.Box(mark,"RegistrationRule",34,24,932,2,UiKit.Ink);
            var title=UiKit.Box(mark,"Title",38,34,924,174,Color.white).GetComponent<Image>();
            title.sprite=UiKit.Template("ui_wordmark_dicecraft");title.preserveAspect=true;
            UiKit.Component<Art072IdleFloat>(title.gameObject).Configure(4,3.2f,0);
            UiKit.Text(mark,"Subtitle","把一次掷骰，变成你的构筑。",38,202,924,56,25,LabelOn11(mark,UiKit.Ink),TextAlignmentOptions.Center);
            if(showRules)UiKit.Text(mark,"MenuRules","掷骰  →  选中  →  锁定  →  提交",38,260,924,52,21,LabelOn11(mark,PaperMuted071),TextAlignmentOptions.Center);
        }

        private static Button Primary16(Transform parent,string name,string label,float x,float y,float width,float height,Action click,bool enabled=true)
        {
            var button=UiKit.ActionButton(parent,name,label,x,y,width,height,click,UiKit.Primary,enabled);
            var text=button.GetComponentInChildren<TextMeshProUGUI>();
            text.color=LabelOn11(button.targetGraphic as Image,enabled?(Color?)null:UiKit.Muted);
            return button;
        }

        private TextMeshProUGUI pickerHelp16;

        private void ShowThemePicker16()
        {
            var p=Art07Modal("画面风格",320,96,1280,888,"theme-picker16");
            pickerHelp16=UiKit.Text(p,"ThemeHelp16","只改变外观，不影响对局与存档。",24,84,1232,Large11?56:48,22,LabelOn11(p,UiKit.Muted));
            var options=ThemeRegistry.Selectable(ThemeRegistry.ShowPreviewThemes);
            for(int i=0;i<options.Count;i++)
            {
                var theme=options[i];
                bool selected=theme.Id==ThemeId11;
                float height=Large11?285:277;
                string id="ThemeTile16_"+theme.Id;
                var tile=UiKit.Button(p,id,(selected?"✓ ":"")+theme.DisplayName,24+(i%3)*416,140+(i/3)*(height+16),400,height,()=>
                {
                    if(theme.Id!=ThemeId11)
                    {
                        PlayerPrefs.SetString(ThemeRegistry.PreferenceKey,theme.Id);
                        PlayerPrefs.Save();
                        ApplyTheme11(theme.Id);
                        ShowThemePicker16();
                    }
                    var chosen=p.Find(id);
                    if(chosen!=null)EventSystem.current?.SetSelectedGameObject(chosen.gameObject);
                });
                var preview=UiKit.Box(tile.transform,"ThemePreview16",8,8,384,216,Color.white).GetComponent<Image>();
                preview.sprite=Resources.Load<Sprite>(theme.IsDefault?"ThemePreviews/default":"Themes/"+theme.Id+"/preview");
                preview.preserveAspect=true;
                var name=tile.transform.Find("Label").GetComponent<TextMeshProUGUI>();
                name.rectTransform.anchoredPosition=new Vector2(8,-228);
                name.rectTransform.sizeDelta=new Vector2(384,Large11?52:44);
                var face=(Image)tile.targetGraphic;
                name.color=LabelOn11(face,UiKit.Ink);
                if(Large11)
                {
                    var r=preview.rectTransform;
                    float top=-r.anchoredPosition.y+r.rect.height;
                    CenterLabelInk24(name,top,face.rectTransform.rect.height-FrameInsets11(face).y);
                }
                if(selected)
                {
                    UiKit.Outline(tile.transform,"CurrentTheme16",-4,-4,408,height+8,UiKit.Gold,4);
                    EventSystem.current?.SetSelectedGameObject(tile.gameObject);
                }
            }
            UiKit.Button(p,"ThemeDone16","完成",864,800,392,64,()=>
            {
                settingsTab07=1;ShowSettings07();
                var row=overlay.GetComponentInChildren<UnityEngine.UI.ScrollRect>()?.content.Find("Theme");
                if(row!=null)EventSystem.current?.SetSelectedGameObject(row.gameObject);
            });
        }

        // Bootstrap turns off automatic uGUI navigation because battle Enter submits a hand.
        // Keep keyboard selection inside this modal, including its Close and Done controls.
        private bool ThemePickerKey16(Keyboard keyboard)
        {
            if(!modal||pickerHelp16==null||!pickerHelp16.gameObject.activeInHierarchy||keyboard==null)return false;
            bool enter=keyboard.enterKey.wasPressedThisFrame||keyboard.numpadEnterKey.wasPressedThisFrame;
            bool previous=keyboard.leftArrowKey.wasPressedThisFrame,next=keyboard.rightArrowKey.wasPressedThisFrame;
            bool up=keyboard.upArrowKey.wasPressedThisFrame,down=keyboard.downArrowKey.wasPressedThisFrame;
            bool tab=keyboard.tabKey.wasPressedThisFrame;
            if(!enter&&!previous&&!next&&!up&&!down&&!tab)return false;
            var eventSystem=EventSystem.current;
            if(eventSystem==null)return false;
            var controls=pickerHelp16.transform.parent.GetComponentsInChildren<Button>(false);
            int index=Array.FindIndex(controls,b=>b.gameObject==eventSystem.currentSelectedGameObject);
            if(enter)
            {
                if(index>=0&&controls[index].IsInteractable())controls[index].OnSubmit(new BaseEventData(eventSystem));
                return true;
            }
            int last=controls.Length-1;
            if(tab)index=(index+(keyboard.shiftKey.isPressed?-1:1)+controls.Length)%controls.Length;
            else if(up)index=Mathf.Max(0,index-3);
            else if(down)index=Mathf.Min(last,index+3);
            else index=Mathf.Clamp(index+(previous?-1:1),0,last);
            eventSystem.SetSelectedGameObject(controls[index].gameObject);
            return true;
        }

        private static void ModalSection16(RectTransform content,RectTransform panel,string id,string heading,string body,ref float y,float width)
        {
            var title=UiKit.Text(content,id+"Title",heading,0,y,width,44,23,LabelOn11(panel,UiKit.Gold));
            y+=FitHeight(title,44)+4;
            var text=UiKit.Text(content,id+"Body",body,0,y,width,40,24,LabelOn11(panel,UiKit.TextColor));
            y+=FitHeight(text,40)+28;
        }

        // A scroll view can have pooled siblings from another kind of dialog. Follow the active
        // content's ancestry to sample the actual panel behind these rows.
        private static Color SettingsInk16(Transform content,Color preferred)
        {
            return LabelOn11(content.parent.parent.parent as RectTransform,preferred);
        }
    }
}
