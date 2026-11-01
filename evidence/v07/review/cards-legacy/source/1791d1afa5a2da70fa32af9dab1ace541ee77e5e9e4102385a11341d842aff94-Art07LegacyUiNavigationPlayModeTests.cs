using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private IEnumerator LegacyClick07(string name)
        {
            yield return WaitForIdle();yield return LegacyVisibleClick07(Button(name));
        }
        private IEnumerator LegacyVisibleClick07(Button target)
        {
            yield return WaitForIdle();Canvas.ForceUpdateCanvases();
            // The player scrolls the actual ScrollRect rather than invoking an offscreen callback.
            foreach(var scroll in target.GetComponentsInParent<ScrollRect>(false))
            {
                int attempts=0,budget=Mathf.CeilToInt(scroll.content.rect.height/Mathf.Max(1,scroll.scrollSensitivity))+4;
                while(!TargetFullyInsideViewport(target)&&attempts++<budget)
                {
                    var corners=new Vector3[4];((RectTransform)target.transform).GetWorldCorners(corners);
                    float center=scroll.viewport.InverseTransformPoint((corners[0]+corners[2])/2).y;
                    var pointer=new PointerEventData(EventSystem.current){position=RectTransformUtility.WorldToScreenPoint(null,scroll.viewport.TransformPoint(scroll.viewport.rect.center)),scrollDelta=new Vector2(0,center>scroll.viewport.rect.center.y?1:-1)};
                    ExecuteEvents.Execute(scroll.gameObject,pointer,ExecuteEvents.scrollHandler);Canvas.ForceUpdateCanvases();yield return null;
                }
                Assert.That(TargetFullyInsideViewport(target),Is.True,"Cannot inspect a masked product or setting: "+target.name);
            }
            // Panel entrance/transaction effects are real UI gates, not an eligibility exception.
            // Wait within the existing two-second render-ready budget, then require the real first hit.
            float deadline=Time.realtimeSinceStartup+2f;bool hit=false;Vector2 position=Vector2.zero;
            do
            {
                yield return null;Canvas.ForceUpdateCanvases();var rect=(RectTransform)target.transform;
                position=RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
                var hits=new List<RaycastResult>();EventSystem.current.RaycastAll(new PointerEventData(EventSystem.current){position=position},hits);
                hit=target.IsInteractable()&&hits.Count>0&&hits[0].gameObject.GetComponentInParent<Button>()==target;
            }while(!hit&&Time.realtimeSinceStartup<deadline);
            Assert.That(target.IsInteractable(),Is.True,target.name);Assert.That(hit,Is.True,"Covered or unrendered UI: "+target.name);
            Assert.That(position.x,Is.InRange(0,Screen.width));Assert.That(position.y,Is.InRange(0,Screen.height));
            var allCorners=new Vector3[4];((RectTransform)target.transform).GetWorldCorners(allCorners);
            var points=allCorners.Select(p=>RectTransformUtility.WorldToScreenPoint(null,p)).ToArray();
            Assert.That(points.Max(p=>p.x)-points.Min(p=>p.x),Is.GreaterThanOrEqualTo(39.99f),target.name+" width");
            Assert.That(points.Max(p=>p.y)-points.Min(p=>p.y),Is.GreaterThanOrEqualTo(39.99f),target.name+" height");
            ExecuteEvents.Execute(target.gameObject,new PointerEventData(EventSystem.current){position=position,button=PointerEventData.InputButton.Left},ExecuteEvents.pointerClickHandler);
            Canvas.ForceUpdateCanvases();
        }
    }
}
