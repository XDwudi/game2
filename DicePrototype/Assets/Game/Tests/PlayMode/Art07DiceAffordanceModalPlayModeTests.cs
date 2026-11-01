using System.Collections;
using System.Collections.Generic;
using System.IO;
using DiceCraft.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        [UnityTest]
        public IEnumerator PLAY_ART07_DICE_SettingsRebuildAllCloseRoutesRestoreOnlyLegalGrips()
        {
            foreach(int font in new[]{100,150})
            {
                yield return DiceLabelBegin07(font);DiceLabelApply07(10,true);
                var ids=app.Session.State.dice;
                for(int i=0;i<8;i++)ClickDie(ids[i].dieId);
                Click("Confirm");yield return WaitForIdle();yield return new WaitForSecondsRealtime(.25f);
                string before=StateJson();byte[] disk=File.ReadAllBytes(app.SaveLocation);
                string normalPath=Path.Combine(saveDirectory,"active-run.json");byte[] normal=File.ReadAllBytes(normalPath);
                long sequence=app.Session.ActionSequence;
                foreach(string close in new[]{"SettingsDone","CloseDialog","Escape"})
                {
                    yield return LegacyVisibleClick07(Button("Pause"));
                    yield return LegacyVisibleClick07(Button("Settings"));
                    yield return LegacyVisibleClick07(Button("SettingsTab2"));
                    // Both real settings callbacks rebuild the background while the modal is active.
                    Click("TextScale");Click("ColorAssist");
                    Assert.That(AffordanceGrips07(),Is.Empty,"The modal redraw must be the same entry path as the observed defect.");
                    if(close=="Escape")yield return GripDebugRealEscape07();
                    else yield return LegacyVisibleClick07(Button(close));
                    yield return new WaitForSecondsRealtime(.25f);Canvas.ForceUpdateCanvases();
                    Assert.That(HasActive("Dialog"),Is.False);
                    string capture="settings-close-"+close+"-initial-font"+font+"-actual"+PlayerPrefs.GetInt("art07.textScale");
                    yield return CaptureAffordance07(capture);
                    var grips=AffordanceGrips07();Assert.That(grips.Length,Is.EqualTo(8),close+" must restore handles after the modal-only redraw.");
                    Texture2D shot=new Texture2D(2,2,TextureFormat.RGBA32,false);
                    try
                    {
                        Assert.That(shot.LoadImage(File.ReadAllBytes(Path.Combine(saveDirectory,"art07-dice-affordance",capture+".png"))),Is.True);
                        foreach(var grip in grips)
                        {
                            var dots=grip.GetComponentsInChildren<Graphic>(false);Assert.That(dots.Length,Is.EqualTo(6));
                            foreach(var dot in dots)
                            {
                                Assert.That(dot.raycastTarget,Is.False);Assert.That(dot.canvasRenderer.cull,Is.False);
                                Assert.That(dot.canvasRenderer.GetInheritedAlpha(),Is.GreaterThan(.99f));
                                Assert.That(dot.canvasRenderer.GetMesh().vertexCount,Is.GreaterThanOrEqualTo(4));
                                Assert.That(GripPaperPixels07(shot,GuideCheckScreenRect07(dot.rectTransform)),Is.GreaterThan(0),capture+" / "+grip.name+" / "+dot.name);
                            }
                            var data=new PointerEventData(EventSystem.current){position=GuideCheckScreenRect07(grip).center,button=PointerEventData.InputButton.Left};
                            var rays=new List<RaycastResult>();EventSystem.current.RaycastAll(data,rays);
                            Assert.That(rays.Count,Is.GreaterThan(0));Assert.That(rays[0].gameObject.GetComponentInParent<DiePointer>(),Is.SameAs(grip.GetComponentInParent<DiePointer>()));
                            CheckAffordanceAgainstPips07(grip);
                        }
                    }
                    finally{UnityEngine.Object.Destroy(shot);}
                    Assert.That(StateJson(),Is.EqualTo(before));Assert.That(app.Session.ActionSequence,Is.EqualTo(sequence));
                    CollectionAssert.AreEqual(disk,File.ReadAllBytes(app.SaveLocation));CollectionAssert.AreEqual(normal,File.ReadAllBytes(normalPath));
                }
            }
        }
    }
}
