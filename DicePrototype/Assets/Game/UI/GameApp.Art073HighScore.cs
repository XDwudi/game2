using System.Collections.Generic;
using System.Linq;
using DiceCraft.Presentation;
using TMPro;
using UnityEngine;
using Image=UnityEngine.UI.Image;

namespace DiceCraft
{
    // v0.73 high-score celebration (reference image 4). A separate, non-blocking layer over the dice pool:
    // it starts on the committed FinalScore beat, never extends the presentation and never takes input.
    public sealed partial class GameApp
    {
        private RectTransform highScore073,highScorePlate073,highScoreCrown073;
        private CanvasGroup highScoreGroup073;
        private TextMeshProUGUI highScoreNumber073;
        private readonly List<RectTransform> highScoreRays073=new List<RectTransform>(),highScoreSparks073=new List<RectTransform>();
        private int highScoreTier073;
        private float highScoreElapsed073,highScoreCount073,highScoreFade073=-1;
        private string highScoreValue073;
        private bool highScoreReduced073;
        /// <summary>Tier of the celebration on screen (1..3), or 0 when none is showing.</summary>
        public int HighScoreTierShown073=>highScore073!=null&&highScore073.gameObject.activeSelf?highScoreTier073:0;
        public string HighScoreText073=>highScoreNumber073!=null?highScoreNumber073.text:null;
        public bool HighScoreFading073=>highScoreFade073>=0;
        public float HighScoreElapsed073=>highScoreElapsed073;

        private void BeginHighScore073()
        {
            if(receipt07==null||!receipt07.IsSubmission||main==null)return;
            int tier=HighScore073.Tier(receipt07.ScoreDecimal,receipt07.StageTarget,receipt07.ComboTier);
            if(tier==HighScore073.None){HideHighScore073();return;}
            var options=presentationPlayer07.Sequence.Options;
            bool quiet=options.ReducedMotion||reduced,fastMode=options.Speed!=PresentationSpeed.Normal;
            if(highScore073==null)
            {
                highScore073=UiKit.Rect(canvas.transform,"HighScore073",0,0,1920,1080);
                highScoreGroup073=highScore073.gameObject.AddComponent<CanvasGroup>();highScoreGroup073.blocksRaycasts=false;highScoreGroup073.interactable=false;
                // The tier words use glyphs from the dynamic fallback font. The plate is rebuilt on every replay,
                // which would create and release that fallback material each time; this invisible, always-active
                // text keeps it alive from the first celebration on (the 200-replay stability rule).
                var keeper=UiKit.Text(canvas.transform,"HighScoreGlyphs073","高分超爆",0,0,8,8,22,Color.clear);
                keeper.textWrappingMode=TextWrappingModes.NoWrap;keeper.transform.SetSiblingIndex(0);
            }
            highScore073.anchoredPosition=main.anchoredPosition;highScore073.SetSiblingIndex(main.GetSiblingIndex()+1);
            highScore073.gameObject.SetActive(true);UiKit.Clear(highScore073);highScoreRays073.Clear();highScoreSparks073.Clear();
            highScoreTier073=tier;highScoreElapsed073=0;highScoreFade073=-1;highScoreReduced073=quiet;highScoreValue073=receipt07.ScoreDecimal;
            float beat=beat07!=null?beat07.DurationMs/1000f:.3f;
            highScoreCount073=quiet||fastMode||highScoreValue073.Length>15?0:Mathf.Clamp(beat,.18f,HighScore073.MaxCountSeconds);

            bool c=Compact07;float cx=c?1131:1132,cy=c?560:610,pw=tier==HighScore073.High?820:900,ph=c?200:tier==HighScore073.High?212:232;
            Color line=tier==HighScore073.High?UiKit.Blue:UiKit.Gold;
            UiKit.Box(highScore073,"Veil",c?360:368,c?404:466,c?1542:1528,c?244:286,new Color(.02f,.06f,.07f,.34f));
            if(tier>=HighScore073.Super&&!quiet)
                for(int side=0;side<2;side++)
                {
                    var ray=UiKit.Box(highScore073,"Ray"+side,0,0,300,75,side==0?UiKit.Gold:UiKit.Gold).GetComponent<Image>();
                    ray.sprite=UiKit.Template("fx_score_ray");ray.raycastTarget=false;
                    var rect=ray.rectTransform;rect.pivot=new Vector2(0,.5f);rect.anchoredPosition=new Vector2(cx+(side==0?pw*.5f-8:-pw*.5f+8),-cy);
                    rect.localScale=new Vector3(side==0?1:-1,1,1);ray.gameObject.SetActive(ray.sprite!=null);highScoreRays073.Add(rect);
                }
            highScorePlate073=UiKit.Box(highScore073,"Plate",cx-pw*.5f,cy-ph*.5f,pw,ph,line);
            var plate=highScorePlate073.GetComponent<Image>();plate.sprite=UiKit.Template("fx_score_plate");plate.type=plate.sprite!=null?Image.Type.Sliced:Image.Type.Simple;
            if(plate.sprite==null)plate.color=new Color(.03f,.09f,.1f,.94f);
            highScorePlate073.pivot=new Vector2(.5f,.5f);highScorePlate073.anchoredPosition=new Vector2(cx,-cy);
            string title=tier==HighScore073.Burst?"爆  分":tier==HighScore073.Super?"超 高 分":"高  分";
            // Regular weight on purpose: this text is rebuilt on every replay and a bold run would churn a fallback material.
            UiKit.Text(highScorePlate073,"Caption",title,0,c?10:14,pw,34,22,line,TextAlignmentOptions.Center);
            highScoreNumber073=UiKit.Text(highScorePlate073,"Number",CompactScore(HighScore073.CountValue(highScoreValue073,highScoreCount073>0?0:1)),30,c?36:44,pw-60,c?96:112,c?78:tier==HighScore073.High?88:100,
                tier==HighScore073.High?UiKit.TextColor:UiKit.Hex("FFF1CF"),TextAlignmentOptions.Midline);
            // The interface font on purpose: the separate number font has no comma, and a text that is rebuilt on every
            // replay would create and release that fallback material each time (the 200-replay stability rule).
            highScoreNumber073.textWrappingMode=TextWrappingModes.NoWrap;highScoreNumber073.enableAutoSizing=true;
            highScoreNumber073.fontSizeMin=36;highScoreNumber073.fontSizeMax=highScoreNumber073.fontSize;
            string multiplier=receipt07.MultiplierDenominatorDecimal=="1"&&receipt07.MultiplierNumeratorDecimal.Length<=12?Big(receipt07.MultiplierNumeratorDecimal):ScoreFactorShort07(receipt07.MultiplierNumeratorDecimal,receipt07.MultiplierDenominatorDecimal);
            string gold="<color=#"+ColorUtility.ToHtmlStringRGB(UiKit.Gold)+">";
            var chain=UiKit.Text(highScorePlate073,"Chain","连锁 "+gold+receipt07.ChainCount+"</color>     |     倍率 "+gold+"×"+multiplier+"</color>",30,ph-(c?60:70),pw-60,36,c?22:26,UiKit.TextColor,TextAlignmentOptions.Center);
            chain.richText=true;chain.textWrappingMode=TextWrappingModes.NoWrap;
            string formula=HighScore073.Formula(lastPresentationAfter07?.lastSubmission?.breakdown,n=>Factor(n),(n,d)=>ScoreFactorShort07(n,d),n=>n!=null&&n.Length>9?ScoreFactorShort07(n):CompactScore(n));
            if(!string.IsNullOrEmpty(formula))
            {
                float fw=Mathf.Clamp(120+formula.Length*15f,280,pw-80);
                var chip=UiKit.Box(highScore073,"FormulaChip",cx-fw*.5f,cy+ph*.5f-22,fw,46,Color.white).GetComponent<Image>();
                chip.sprite=UiKit.Template("fx_formula_chip");chip.type=chip.sprite!=null?Image.Type.Sliced:Image.Type.Simple;if(chip.sprite==null)chip.color=UiKit.Background;
                var text=UiKit.Text(chip.transform,"Formula",formula,12,0,fw-24,46,22,UiKit.TextColor,TextAlignmentOptions.Midline);
                text.textWrappingMode=TextWrappingModes.NoWrap;text.overflowMode=TextOverflowModes.Overflow;
            }
            highScoreCrown073=null;
            if(tier==HighScore073.Burst)
            {
                var crown=UiKit.Box(highScore073,"Crown",cx-60,cy-ph*.5f-(c?44:56),120,80,Color.white).GetComponent<Image>();
                crown.sprite=UiKit.Template("fx_score_crown");crown.raycastTarget=false;crown.gameObject.SetActive(crown.sprite!=null);highScoreCrown073=crown.rectTransform;
            }
            int sparks=quiet||options.Quality==PresentationQuality.Low?0:tier==HighScore073.Burst?18:tier==HighScore073.Super?10:0;
            for(int i=0;i<sparks;i++)
            {
                float size=16+i%3*6;
                var spark=UiKit.Box(highScore073,"Spark"+i,cx,cy,size,size,i%3==0?UiKit.Red:i%3==1?UiKit.Blue:UiKit.Gold).GetComponent<Image>();
                spark.sprite=UiKit.Template("fx_score_spark");spark.raycastTarget=false;spark.rectTransform.pivot=new Vector2(.5f,.5f);
                spark.gameObject.SetActive(spark.sprite!=null);highScoreSparks073.Add(spark.rectTransform);
            }
            // The cards that produced the score keep a gold tick until the celebration leaves.
            int marks=0;
            foreach(string id in receipt07.ScoringEmitterIds)
            {
                if(marks>=10||!TryCardBounds07(id,out var card))continue;
                var tick=UiKit.Box(highScore073,"Scored"+marks++,card.xMax-34,card.yMin-8,38,38,Color.white).GetComponent<Image>();
                tick.sprite=UiKit.Template("ui_badge_check");tick.raycastTarget=false;tick.gameObject.SetActive(tick.sprite!=null);
            }
            EvaluateHighScore073();
        }
        private void EndHighScore073(bool immediately)
        {
            if(highScore073==null||!highScore073.gameObject.activeSelf)return;
            if(immediately){HideHighScore073();return;}
            if(highScoreNumber073!=null)highScoreNumber073.text=CompactScore(highScoreValue073);
            if(highScoreFade073<0)highScoreFade073=0;
        }
        private void HideHighScore073()
        {
            highScoreFade073=-1;highScoreTier073=0;
            if(highScore073!=null){UiKit.Clear(highScore073);highScore073.gameObject.SetActive(false);}
        }
        private void TickHighScore073(float seconds)
        {
            if(highScore073==null||!highScore073.gameObject.activeSelf||modal)return;
            highScoreElapsed073+=Mathf.Max(0,seconds);
            // Never leave before it could be read, even when a fast presentation ends at once.
            if(highScoreFade073>=0&&highScoreElapsed073>=HighScore073.MinVisibleSeconds)highScoreFade073+=Mathf.Max(0,seconds);
            EvaluateHighScore073();
            float fade=highScoreReduced073?HighScore073.ReducedFadeSeconds:HighScore073.FadeSeconds;
            if(highScoreFade073>=fade)HideHighScore073();
        }
        private void EvaluateHighScore073()
        {
            if(highScorePlate073==null)return;
            float t=highScoreElapsed073,pop=highScoreReduced073?1:Mathf.Clamp01(t/HighScore073.PopSeconds);
            float scale=highScoreReduced073?1:pop<.6f?Mathf.Lerp(.86f,1.04f,pop/.6f):Mathf.Lerp(1.04f,1f,(pop-.6f)/.4f);
            float fadeSeconds=highScoreReduced073?HighScore073.ReducedFadeSeconds:HighScore073.FadeSeconds;
            float fade=highScoreFade073<0?1:1-Mathf.Clamp01(highScoreFade073/fadeSeconds);
            highScoreGroup073.alpha=(highScoreReduced073?1:Mathf.Clamp01(t/.08f))*fade;
            highScorePlate073.localScale=Vector3.one*scale*(highScoreReduced073?1:Mathf.Lerp(.98f,1f,fade));
            if(highScoreNumber073!=null&&highScoreFade073<0)
            {
                float progress=highScoreCount073<=0?1:Mathf.Clamp01((t-.04f)/highScoreCount073);
                progress=1-(1-progress)*(1-progress)*(1-progress);
                string shown=CompactScore(HighScore073.CountValue(highScoreValue073,progress>=.999f?1:progress));
                if(highScoreNumber073.text!=shown)highScoreNumber073.text=shown;
            }
            float grow=Mathf.Clamp01(t/.3f);grow=1-(1-grow)*(1-grow);
            for(int i=0;i<highScoreRays073.Count;i++)
            {
                var ray=highScoreRays073[i];float pulse=1+.04f*Mathf.Sin(t*9f+i);
                ray.localScale=new Vector3((i==0?1:-1)*Mathf.Lerp(.25f,1f,grow)*pulse,1,1);
            }
            if(highScoreCrown073!=null&&!highScoreReduced073)
            {
                float drop=Mathf.Clamp01((t-.06f)/.2f);drop=1-(1-drop)*(1-drop);
                highScoreCrown073.localScale=Vector3.one*Mathf.Lerp(1.5f,1f,drop);highScoreCrown073.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*5f)*3f*(1-Mathf.Clamp01(t/1.2f)));
            }
            Vector2 centre=highScorePlate073.anchoredPosition;
            for(int i=0;i<highScoreSparks073.Count;i++)
            {
                var spark=highScoreSparks073[i];float delay=.02f*(i%5),life=Mathf.Clamp01((t-delay)/.75f),ease=1-(1-life)*(1-life);
                float angle=i*Mathf.PI*2/Mathf.Max(1,highScoreSparks073.Count)+.3f,reach=(i%2==0?560:470)+i%3*26;
                spark.anchoredPosition=centre+new Vector2(Mathf.Cos(angle)*reach*ease,Mathf.Sin(angle)*reach*.34f*ease+70*ease*ease);
                spark.localScale=Vector3.one*(1-life*.7f);spark.localRotation=Quaternion.Euler(0,0,t*220f*(i%2==0?1:-1));
                var image=spark.GetComponent<Image>();var color=image.color;color.a=life>=1?0:1-life*life;image.color=color;
            }
        }
    }
}
