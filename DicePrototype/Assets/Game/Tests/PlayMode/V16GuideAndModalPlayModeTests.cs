using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest] public IEnumerator PLAY_V16_PauseAndStyleLegendRespectFontScaleAndSession()
        {
            StartRun("16080");Click("StartStage");
            string before=StateJson();byte[] checkpoint=File.ReadAllBytes(app.SaveLocation);
            var oldTheme=app.ThemeId11;
            try
            {
                foreach(var theme in ThemeRegistry.All)
                foreach(int scale in new[]{100,150})
                {
                    SettlementField07("textScale07",scale);UiKit.FontScale=scale/100f;app.ApplyTheme11(theme.Id);
                    Click("Pause");
                    Assert.That(InformationText07("Info"),Is.EqualTo("进度已自动保存。\n种子 16080"));
                    var pause=FindActive<RectTransform>("Dialog");
                    Assert.That(pause.GetComponentsInChildren<Button>().Where(b=>b.IsInteractable()&&W11PrimaryColoured(b.targetGraphic.color)).Select(b=>b.name),Is.EqualTo(new[]{"Resume"}));
                    Click("Resume");Click("ViewDice");Click("StyleLegend073");
                    var legend=FindActive<RectTransform>("Dialog");
                    var tiles=legend.GetComponentsInChildren<RectTransform>().Where(rect=>rect.name.StartsWith("StyleCell")).ToArray();
                    Assert.That(tiles.Length,Is.EqualTo(8));
                    foreach(var tile in tiles)
                    {
                        var note=tile.Find("Note").GetComponent<TextMeshProUGUI>();note.ForceMeshUpdate();
                        Assert.That(note.fontSize,Is.GreaterThanOrEqualTo(19.5f*UiKit.FontScale-.01f),theme.Id);
                        Assert.That(note.textInfo.characterCount,Is.EqualTo(note.text.Length),tile.name);
                        Assert.That(note.isTextTruncated,Is.False,tile.name);
                        Assert.That(note.preferredHeight,Is.LessThanOrEqualTo(note.rectTransform.rect.height+1),theme.Id+" "+scale+" "+tile.name);
                    }
                    Click("CloseDialog");yield return null;
                }
            }
            finally { SettlementField07("textScale07",100);UiKit.FontScale=1;app.ApplyTheme11(oldTheme,false); }
            Assert.That(StateJson(),Is.EqualTo(before));CollectionAssert.AreEqual(checkpoint,File.ReadAllBytes(app.SaveLocation));
        }

        [UnityTest] public IEnumerator PLAY_V16_InternalErrorToastShowsPlayerCopyAndRetainsExactDetails()
        {
            const string raw="数据格式无效：fixture-16";
            typeof(GameApp).GetMethod("QueueNotice07",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(app,new object[]{raw,true});
            app.Refresh();yield return null;
            var toast=FindActive<RectTransform>("Toast");
            Assert.That(toast.Find("Text").GetComponent<TextMeshProUGUI>().text,Is.EqualTo("操作没有完成（内部错误）。点“详情”可复制错误信息。"));
            Click("ToastDetails07");
            Assert.That(InformationText07("NoticeDetailBody"),Is.EqualTo(raw));
            string original=GUIUtility.systemCopyBuffer;
            try { Click("CopyInternalError16");Assert.That(GUIUtility.systemCopyBuffer,Is.EqualTo(raw)); }
            finally { GUIUtility.systemCopyBuffer=original; }
            Click("CloseDialog");Assert.That(app.Session.State,Is.Null);
            Assert.That(File.Exists(app.SaveLocation),Is.False);
        }
    }
}
