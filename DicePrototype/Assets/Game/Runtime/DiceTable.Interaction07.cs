using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceCraft
{
    public sealed partial class DiceTable
    {
        private readonly Dictionary<string,float> lockDelays07=new Dictionary<string,float>(10);
        private MaterialPropertyBlock contourProperties07;
        private float lockClock07;
        public const float PendingRiseSeconds07=.12f, PendingReturnSeconds07=.08f;
        public const float LockMoveSeconds07=.18f, LockStaggerSeconds07=.025f;

        // Only the accepted command's ordered IDs are scheduled. No rule or RNG work lives here.
        public void BeginLockLayout07(string[] orderedIds)
        {
            FinishLockLayout07();lockClock07=0;
            for(int i=0;i<orderedIds.Length;i++)lockDelays07[orderedIds[i]]=i*LockStaggerSeconds07;
        }
        public void TickLockLayout07(float authoredSeconds)
        {
            lockClock07=Mathf.Max(0,authoredSeconds);
            foreach(var pair in visuals)
                if(pair.Value.lockTrack07&&lockDelays07.TryGetValue(pair.Key,out float delay))
                    ApplyLayoutAt07(pair.Value,Mathf.Max(0,lockClock07-delay));
        }
        public void FinishLockLayout07()
        {
            foreach(var v in visuals.Values)
                if(v.lockTrack07){ApplyLayoutAt07(v,LockMoveSeconds07);v.lockTrack07=false;}
            lockDelays07.Clear();
        }
        public float LockProgress07(string id)=>lockDelays07.TryGetValue(id,out float delay)?Mathf.Clamp01((lockClock07-delay)/LockMoveSeconds07):1;
        public float LayoutDurationFor07(string id)=>visuals.TryGetValue(id,out var v)?v.layoutDuration:0;
        public float LayoutElapsedFor07(string id)=>visuals.TryGetValue(id,out var v)?v.layoutElapsed:0;
        public bool LayoutPlayingFor07(string id)=>visuals.TryGetValue(id,out var v)&&v.layoutMoving;
        public void SetHoveredDie07(string id,bool hovered)
        {
            if(!visuals.TryGetValue(id,out var v)||v.outlineRenderer==null)return;
            if(contourProperties07==null)contourProperties07=new MaterialPropertyBlock();
            v.outlineRenderer.GetPropertyBlock(contourProperties07);
            contourProperties07.SetColor("_BaseColor",hovered?UiKit.Paper:UiKit.Ink);
            v.outlineRenderer.SetPropertyBlock(contourProperties07);
        }
        public void TickLayoutTransitions07(float seconds)
        {
            if(Paused)return;
            foreach(var v in visuals.Values)
            {
                if(!v.root.activeSelf||!v.layoutMoving||v.lockTrack07)continue;
                if(ReducedMotion||UiKit.ReducedMotion){FinishLayoutTransitions();break;}
                ApplyLayoutAt07(v,v.layoutElapsed+Mathf.Max(0,seconds));
            }
        }
        private void ApplyLayoutAt07(Visual v,float elapsed)
        {
            v.layoutElapsed=elapsed;
            float t=Mathf.Clamp01(elapsed/Mathf.Max(.001f,v.layoutDuration)),smooth=t*t*(3-2*t);
            v.root.transform.position=Vector3.LerpUnclamped(v.layoutFrom,v.resting,smooth);
            float punch=v.lockPunch?1f+.04f*Mathf.Sin(t*Mathf.PI):1f;
            v.root.transform.localScale=Vector3.LerpUnclamped(v.layoutScaleFrom,v.layoutScaleTo,smooth)*punch;
            v.pivot.localRotation=Quaternion.Slerp(v.layoutRotationFrom,v.target,smooth);
            if(t>=1){v.layoutMoving=false;v.root.transform.position=v.resting;v.root.transform.localScale=v.layoutScaleTo;v.pivot.localRotation=v.target;}
            UpdateFaceVisibility(v);
        }
        private void OnApplicationFocus(bool focused){if(!focused)FinishLayoutTransitions();}
        private void OnApplicationPause(bool paused){if(paused)FinishLayoutTransitions();}
    }
}
