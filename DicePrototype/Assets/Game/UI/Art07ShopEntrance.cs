using System;
using System.Collections.Generic;
using UnityEngine;

namespace DiceCraft
{
    /// <summary>Finite S10 first-visit shelf entrance. All objects retain their production UI handlers.</summary>
    public sealed class Art07ShopEntrance : MonoBehaviour
    {
        private sealed class Track { public RectTransform root; public CanvasGroup group; public Vector2 origin; public float delay; public bool card; }
        private readonly HashSet<string> shownShops=new HashSet<string>();
        private readonly List<Track> tracks=new List<Track>();
        private Func<bool> paused;
        private double lastClock;
        private bool reduced;
        public bool IsPlaying { get; private set; }
        public float Elapsed { get; private set; }
        public float Duration { get; private set; }
        public int EnterSequence { get; private set; }
        public int CardGroupCount { get; private set; }
        public const float ShelfSeconds=.24f,CardSeconds=.18f,CardStagger=.03f,CardRise=12;

        public void Bind(string shopIdentity,bool reduce,bool instant,Func<bool> isPaused,RectTransform[] shelfRoots,RectTransform[][] cardGroups)
        {
            // A redraw caused by a tab, purchase, refresh or capacity service immediately
            // settles the first-visit presentation. That real transaction owns its new motion.
            // UiKit has already laid out reused roots for the new page. Restoring an old
            // origin here would move ActiveShopTab back to its previous selection.
            IsPlaying=false;
            foreach(var old in tracks)if(old.group!=null)old.group.alpha=1;
            tracks.Clear();paused=isPaused;reduced=reduce;CardGroupCount=cardGroups.Length;
            foreach(var root in shelfRoots)Add(root,false,0);
            for(int i=0;i<cardGroups.Length;i++)foreach(var root in cardGroups[i])Add(root,true,reduce?0:i*CardStagger);
            bool fresh=shownShops.Add(shopIdentity);
            if(!fresh||instant){Complete();return;}
            EnterSequence++;Elapsed=0;Duration=reduce?.12f:Mathf.Max(ShelfSeconds,Mathf.Max(0,CardGroupCount-1)*CardStagger+CardSeconds);
            lastClock=Time.realtimeSinceStartupAsDouble;IsPlaying=true;Evaluate();
        }
        private void Add(RectTransform root,bool card,float delay)
        {
            if(root==null||!root.gameObject.activeInHierarchy||tracks.Exists(t=>t.root==root))return;
            var group=UiKit.Component<CanvasGroup>(root.gameObject);
            // Opacity/position only. Existing Button state and raycasts are untouched.
            tracks.Add(new Track{root=root,group=group,origin=root.anchoredPosition,card=card,delay=delay});
        }
        private void Update()
        {
            if(!IsPlaying)return;
            if(tracks.Count==0||tracks[0].root==null||!tracks[0].root.gameObject.activeInHierarchy){Complete();return;}
            double now=Time.realtimeSinceStartupAsDouble;float delta=(float)Math.Max(0,now-lastClock);lastClock=now;
            if(paused?.Invoke()==true)return;
            Elapsed=Mathf.Min(Duration,Elapsed+delta);Evaluate();if(Elapsed>=Duration)Complete();
        }
        private void Evaluate()
        {
            foreach(var t in tracks)
            {
                if(t.root==null||t.group==null)continue;
                float seconds=reduced?.12f:t.card?CardSeconds:ShelfSeconds;
                float p=Mathf.Clamp01((Elapsed-t.delay)/seconds),ease=1-(1-p)*(1-p);
                t.group.alpha=ease;t.root.anchoredPosition=t.origin+Vector2.down*(t.card&&!reduced?CardRise*(1-ease):0);
            }
        }
        public void Complete()
        {
            IsPlaying=false;Elapsed=Duration;
            foreach(var t in tracks)
            {
                if(t.root!=null)t.root.anchoredPosition=t.origin;
                if(t.group!=null)t.group.alpha=1;
            }
        }
        private void OnDisable(){Complete();}
    }
}
