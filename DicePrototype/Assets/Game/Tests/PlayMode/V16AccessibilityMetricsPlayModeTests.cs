using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        // SCREENS_LAYOUT §13. Use the same page key for the initial view and its scrolled views.
        private static readonly string[] AccessPages16 = {
            "startup","menu-empty","menu-saved","new-empty","new-saved","quit","version",
            "handbook-hands","handbook-colours","handbook-rules","shop-handbook",
            "settings-0","settings-1","settings-2","settings-themes","settings-display","settings-confirm",
            "select-common","select-pressure","select-boss-disabled","select-boss-goat","select-busy","select-save-pending",
            "chapter-intel","pause","save-failure","load-recovery","notice-full","dice-styles",
            "guide-1","guide-2","guide-3","guide-4","guide-5","hint-0","hint-1","hint-2"
        };

        private readonly Dictionary<string,(Rect rect,string frame)> hits16 = new Dictionary<string,(Rect,string)>();

        [UnityTest]
        public IEnumerator PLAY_V16_BossHoverUsesReadableShortTextOnItsRaycastSurface()
        {
            int original=Mathf.RoundToInt(UiKit.FontScale*100);
            try
            {
                StartRun("160506");
                foreach(int scale in new[]{100,150})
                {
                    W11TextScale(scale);
                    yield return W11Settle();
                    var card=W14Main.Find("CycleCard2");
                    var boss=app.Session.CycleView().Single(v=>v.stageKind=="boss");
                    foreach(var content in new[]{("ModifierDetails16",app.Config.Modifier(boss.modifierId).shortText),
                        ("LootDetails16",app.Config.Loot(boss.lootKind).shortText)})
                    {
                        var area=(RectTransform)card.Find(content.Item1);
                        var pointer=new PointerEventData(EventSystem.current){position=V06ScreenRect(area).center};
                        var intersections=new List<RaycastResult>();
                        EventSystem.current.RaycastAll(pointer,intersections);
                        Assert.That(intersections.Count,Is.Positive,area.name);
                        Assert.That(intersections[0].gameObject,Is.EqualTo(area.gameObject),area.name);
                        ExecuteEvents.Execute(area.gameObject,pointer,ExecuteEvents.pointerEnterHandler);
                        yield return new WaitForSecondsRealtime(.42f);
                        var tip=FindActive<RectTransform>("ContentTooltip");
                        var text=tip.Find("State").GetComponent<TextMeshProUGUI>();
                        Assert.That(text.text,Is.EqualTo(content.Item2));
                        text.ForceMeshUpdate();
                        var printed=text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(c=>c.isVisible).ToArray();
                        Assert.That(printed,Is.Not.Empty);
                        Assert.That(printed.Min(c=>c.pointSize)*text.transform.TransformVector(Vector3.up).magnitude,
                            Is.GreaterThanOrEqualTo(19.5f*UiKit.FontScale*app.canvas.scaleFactor-.01f),area.name);
                        ExecuteEvents.Execute(area.gameObject,pointer,ExecuteEvents.pointerExitHandler);
                        yield return new WaitForSecondsRealtime(.09f);
                        Assert.That(HasActive("ContentTooltip"),Is.False);
                    }
                }
            }
            finally { W11TextScale(original); }
        }

        private void MeasureScreenAccess16(string path,Transform[] roots,
            Dictionary<TextMeshProUGUI,List<(Rect box,Color32 colour,char character,float points)>> measured,
            List<string> report,List<string> failures)
        {
            if(Screen.width!=1280||Screen.height!=720)return;
            string frame=Path.GetFileNameWithoutExtension(path);
            string page=AccessPages16.FirstOrDefault(id=>frame==id||frame.StartsWith(id+"-",StringComparison.Ordinal));
            if(page==null)return;
            int keyNumbers=0,markers=0;
            foreach(var item in measured)
            {
                var t=item.Key;
                var parent=t.transform.parent;
                string id=W20XPath(t.transform,app.canvas.transform);
                // Transform the TMP em to physical pixels, including any scale on the actual label.
                float pixels=item.Value.Min(sample=>sample.points)*t.rectTransform.TransformVector(Vector3.up).magnitude;
                if(DecorativeMarker16(page,t))
                {
                    markers++;
                    report.Add(frame+" SC-05 decorative "+id+" '"+t.text+"' "+pixels.ToString("F2")+"px");
                    continue;
                }
                bool goal=t.name=="Goal"&&parent.name.StartsWith("CycleCard",StringComparison.Ordinal);
                bool money=t.name=="Money"&&parent.name=="Stats07";
                bool key=goal||money;
                if(key)keyNumbers++;
                float floor=key?21:13;
                if(pixels+.01f<floor)
                    failures.Add(frame+" SC-05 "+id+" '"+t.text+"' "+pixels.ToString("F2")+"px below "+floor+"px");
            }
            if(frame==page&&(page.StartsWith("select-",StringComparison.Ordinal)||page=="chapter-intel"))
            {
                // The ordinary and boss stage each print a standalone target. Stage select also prints current gold.
                int required=page=="chapter-intel"?2:3;
                if(keyNumbers!=required)failures.Add(frame+" SC-05 measured "+keyNumbers+" key numbers, expected "+required);
            }
            var controls=roots.SelectMany(r=>r.GetComponentsInChildren<Selectable>(false))
                .Where(s=>s.isActiveAndEnabled&&s.IsInteractable()).Select(s=>(RectTransform)s.transform);
            var dice=roots.SelectMany(r=>r.GetComponentsInChildren<Art073DieView>(false))
                .Where(d=>d.isActiveAndEnabled).Select(d=>(RectTransform)d.transform);
            int targets=0;
            foreach(var rt in controls.Concat(dice).Distinct())
            {
                string id=page+" "+W20XPath(rt,app.canvas.transform);
                // The slider's 38-unit handle sits on a full-height clickable root. Input fields
                // likewise have a clickable frame around their narrower text viewport.
                var receivers=rt.GetComponentsInChildren<Graphic>(false).Where(g=>g.isActiveAndEnabled&&g.raycastTarget&&
                    g.GetComponentInParent<Selectable>()==rt.GetComponent<Selectable>());
                var hit=V06ScreenRect(rt);
                var clip=new Rect(0,0,Screen.width,Screen.height);
                foreach(var scroll in rt.GetComponentsInParent<ScrollRect>(false))
                    if(rt.IsChildOf(scroll.content))clip=Intersection16(clip,V06ScreenRect(scroll.viewport));
                var best=new Rect();
                foreach(var g in receivers)
                {
                    var visible=Intersection16(Intersection16(V06ScreenRect(g.rectTransform),hit),clip);
                    if(Mathf.Min(visible.width,visible.height)>Mathf.Min(best.width,best.height))best=visible;
                }
                targets++;
                if(!hits16.TryGetValue(id,out var prior)||Mathf.Min(best.width,best.height)>Mathf.Min(prior.rect.width,prior.rect.height))
                    hits16[id]=(best,frame);
            }
            report.Add(frame+" SC-05 "+measured.Count+" visible labels, "+keyNumbers+" key numbers, "+markers+
                " enumerated markers; SC-06 "+targets+" active control rectangles");
        }

        private bool DecorativeMarker16(string page,TextMeshProUGUI t)
        {
            var p=t.transform.parent;
            if(page=="dice-styles"&&t.name=="Number"&&t.text=="1"&&p.name=="Tray"&&p.parent?.name=="StyleCell7")
                return true;
            if(!page.StartsWith("select-",StringComparison.Ordinal)||t.name!="Ordinal"||p.parent!=W14Main)return false;
            var disc=p.Find("OrdinalDisc11") as RectTransform;
            if(disc==null||!disc.gameObject.activeInHierarchy||!Art073Inside(V06ScreenRect(t.rectTransform),V06ScreenRect(disc)))return false;
            int at=app.Session.State.controllers.FindIndex(card=>p.name=="Controller_"+card.instanceId);
            return at>=0&&t.text==(at+1).ToString();
        }

        private static Rect Intersection16(Rect a,Rect b)
        {
            float x=Mathf.Max(a.xMin,b.xMin),y=Mathf.Max(a.yMin,b.yMin);
            return new Rect(x,y,Mathf.Max(0,Mathf.Min(a.xMax,b.xMax)-x),Mathf.Max(0,Mathf.Min(a.yMax,b.yMax)-y));
        }

        private void FinishHitAreas16(List<string> report,List<string> failures)
        {
            foreach(var entry in hits16.OrderBy(p=>p.Key,StringComparer.Ordinal))
            {
                var rect=entry.Value.rect;
                string row=entry.Key+" SC-06 "+rect.width.ToString("F2")+" x "+rect.height.ToString("F2")+
                    "px, best visible in "+entry.Value.frame;
                report.Add(row);
                if(rect.width<39.99f||rect.height<39.99f)failures.Add(row+" below 40px");
            }
            hits16.Clear();
        }
    }
}
