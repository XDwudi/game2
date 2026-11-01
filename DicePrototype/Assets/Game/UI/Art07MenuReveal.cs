using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    /// <summary>Finite menu entrance and a two-pixel, eight-second felt breath. Never moves dice or input.</summary>
    public sealed class Art07MenuReveal : MonoBehaviour
    {
        private string key;
        private bool shown,playing;
        private float elapsed,fade,stagger,breath;
        private CanvasGroup[] buttons;
        private Image veil;
        private RectTransform panel;
        private Renderer felt;
        private MaterialPropertyBlock properties;

        public void Bind(string identity,RectTransform menuPanel,Image screenVeil,params RectTransform[] controls)
        {
            panel=menuPanel;veil=screenVeil;enabled=true;
            if(felt==null){var table=GameObject.Find("Art07Felt");if(table!=null)felt=table.GetComponent<Renderer>();}
            if(properties==null)properties=new MaterialPropertyBlock();
            if(key==identity){Complete();return;}
            key=identity;fade=shown||UiKit.ReducedMotion?.12f:.24f;stagger=shown||UiKit.ReducedMotion?0:.035f;
            shown=true;elapsed=0;breath=0;playing=true;
            buttons=new CanvasGroup[controls.Length];
            for(int i=0;i<controls.Length;i++)
            {
                var group=controls[i].GetComponent<CanvasGroup>();if(group==null)group=controls[i].gameObject.AddComponent<CanvasGroup>();
                group.alpha=0;group.blocksRaycasts=true;group.interactable=true;buttons[i]=group;
            }
            veil.raycastTarget=false;veil.color=new Color(UiKit.Background.r,UiKit.Background.g,UiKit.Background.b,1);
            ResetFelt();
        }
        public void Complete()
        {
            playing=false;if(veil!=null)veil.color=new Color(UiKit.Background.r,UiKit.Background.g,UiKit.Background.b,0);
            if(buttons!=null)foreach(var group in buttons)if(group!=null)group.alpha=1;
        }
        private void Update()
        {
            if(panel==null||!panel.gameObject.activeInHierarchy){Complete();ResetFelt();enabled=false;return;}
            if(playing)
            {
                elapsed+=Time.unscaledDeltaTime;
                if(veil!=null)veil.color=new Color(UiKit.Background.r,UiKit.Background.g,UiKit.Background.b,1-Mathf.Clamp01(elapsed/fade));
                for(int i=0;i<buttons.Length;i++)if(buttons[i]!=null)buttons[i].alpha=Mathf.Clamp01((elapsed-i*stagger)/fade);
                if(elapsed>=fade+stagger*(buttons.Length-1))Complete();
            }
            breath+=Time.unscaledDeltaTime;
            float wave=UiKit.ReducedMotion?0:(1-Mathf.Cos(breath*Mathf.PI*.25f))*.5f;
            if(felt!=null)
            {
                felt.GetPropertyBlock(properties);properties.SetFloat("_MenuDriftPixels",wave*2);properties.SetFloat("_MenuBreath",wave*.01f);felt.SetPropertyBlock(properties);
            }
        }
        private void ResetFelt()
        {
            if(felt==null||properties==null)return;
            felt.GetPropertyBlock(properties);properties.SetFloat("_MenuDriftPixels",0);properties.SetFloat("_MenuBreath",0);felt.SetPropertyBlock(properties);
        }
        private void OnDisable(){Complete();ResetFelt();}
        private void OnDestroy(){ResetFelt();}
    }
}
