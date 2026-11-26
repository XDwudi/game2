using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using DiceCraft.Presentation;
using TMPro;
using UnityEngine;
using Image=UnityEngine.UI.Image;

namespace DiceCraft
{
    // High-score celebration. v0.73 showed a plate over the dice pool; V1.1 (task W11, PLAY_LAYOUT section 3, AX-01) lights
    // the top bar and the rims of the dice tray and the submission tray instead, names the tier beside the total in the
    // formula row ("一手达标！/三倍目标！/十倍目标！" since p1.1.1; "过半！/一手达标！/两倍目标！" before) and lets sparks rise
    // from the gain in the top bar.
    // Still a separate, non-blocking layer that starts on the committed FinalScore beat, never extends the presentation and
    // never takes input. Nothing in it is drawn over the dice.
    public sealed partial class GameApp
    {
        private RectTransform highScore073,highScoreCrown073;
        private CanvasGroup highScoreGroup073;
        private readonly List<Image> highScoreGlows073=new List<Image>();
        private readonly List<RectTransform> highScoreSparks073=new List<RectTransform>();
        private Vector2 highScoreSparkOrigin073;
        private int highScoreTier073;
        private float highScoreElapsed073,highScoreFade073=-1;
        private string highScoreValue073;
        private bool highScoreReduced073;
        /// <summary>Tier of the celebration on screen (1..3), or 0 when none is showing.</summary>
        public int HighScoreTierShown073=>highScore073!=null&&highScore073.gameObject.activeSelf?highScoreTier073:0;
        /// <summary>The hand total as the formula row shows it while the celebration is up (it counts up during FinalScore).</summary>
        public string HighScoreText073=>highScore073==null||!highScore073.gameObject.activeSelf?null:IsPresenting07&&receiptNumber07!=null?receiptNumber07.text:CompactScore(highScoreValue073);
        public bool HighScoreFading073=>highScoreFade073>=0;
        public float HighScoreElapsed073=>highScoreElapsed073;

        private void BeginHighScore073()
        {
            if(receipt07==null||!receipt07.IsSubmission||main==null)return;
            // p1.1.1 (W7b, RULES_DELTA 13 row 5): the hand against the target frozen with it; tiers at 1, 3 and 10 targets.
            int tier=HighScore073.Tier(receipt07.ScoreDecimal,receipt07.HandTarget,receipt07.ComboTier);
            if(tier==HighScore073.None){HideHighScore073();return;}
            var options=presentationPlayer07.Sequence.Options;
            bool quiet=options.ReducedMotion||reduced;
            if(highScore073==null)
            {
                highScore073=UiKit.Rect(canvas.transform,"HighScore073",0,0,1920,1080);
                highScoreGroup073=highScore073.gameObject.AddComponent<CanvasGroup>();highScoreGroup073.blocksRaycasts=false;highScoreGroup073.interactable=false;
            }
            highScore073.anchoredPosition=main.anchoredPosition;highScore073.SetSiblingIndex(main.GetSiblingIndex()+1);if(overlay!=null)overlay.SetAsLastSibling();
            highScore073.gameObject.SetActive(true);UiKit.Clear(highScore073);highScoreGlows073.Clear();highScoreSparks073.Clear();
            highScoreTier073=tier;highScoreElapsed073=0;highScoreFade073=-1;highScoreReduced073=quiet;highScoreValue073=receipt07.ScoreDecimal;

            var l=L11;Color line=tier==HighScore073.High?UiKit.Blue:UiKit.Reward;
            // Light on the top bar (where the total rolls in) and on the rims of both trays. Rings only: their centres stay clear.
            Glow073("TopGlow",l.top,6,line,1f);
            if(tier>=HighScore073.Super)Glow073("TopGlowOuter",l.top,12,line,.45f);
            Glow073("TrayGlow",l.tray,6,line,.9f);
            Glow073("RailGlow",l.rail,6,line,tier>=HighScore073.Super?.9f:.6f);
            // The gain chip in the top bar is the spark source and the crown's perch.
            var gain=score11Layer!=null?score11Layer.Find("TopGain11") as RectTransform:null;
            Rect anchor=gain!=null&&gain.gameObject.activeSelf?new Rect(gain.anchoredPosition.x,-gain.anchoredPosition.y,gain.rect.width,gain.rect.height):new Rect(l.top.center.x-60,l.top.y+12,120,44);
            highScoreSparkOrigin073=anchor.center;
            highScoreCrown073=null;
            if(tier==HighScore073.Burst)
            {
                var crown=UiKit.Box(highScore073,"Crown",anchor.xMax-30,anchor.y-22,48,32,Color.white).GetComponent<Image>();
                crown.sprite=UiKit.Template("fx_score_crown");crown.raycastTarget=false;crown.preserveAspect=true;crown.gameObject.SetActive(crown.sprite!=null);highScoreCrown073=crown.rectTransform;
                highScoreCrown073.pivot=new Vector2(.5f,1f);highScoreCrown073.anchoredPosition=new Vector2(anchor.xMax-6,-(anchor.y-20));
            }
            int sparks=quiet||options.Quality==PresentationQuality.Low?0:tier==HighScore073.Burst?16:tier==HighScore073.Super?10:0;
            for(int i=0;i<sparks;i++)
            {
                float size=12+i%3*5;
                var spark=UiKit.Box(highScore073,"Spark"+i,highScoreSparkOrigin073.x,highScoreSparkOrigin073.y,size,size,i%3==0?UiKit.Red:i%3==1?UiKit.Blue:UiKit.Gold).GetComponent<Image>();
                spark.sprite=UiKit.Template("fx_score_spark");spark.raycastTarget=false;spark.rectTransform.pivot=new Vector2(.5f,.5f);
                spark.gameObject.SetActive(spark.sprite!=null);highScoreSparks073.Add(spark.rectTransform);
            }
            EvaluateHighScore073();
        }
        private void Glow073(string name,Rect around,float pad,Color color,float strength)
        {
            var ring=UiKit.Ring(highScore073,name,around.x-pad,around.y-pad,around.width+pad*2,around.height+pad*2,color).GetComponent<Image>();
            ring.raycastTarget=false;ring.pixelsPerUnitMultiplier=.7f;var c=color;c.a=strength;ring.color=c;highScoreGlows073.Add(ring);
        }
        /// <summary>
        /// v1.1 (W09, text proposal 1 #1): the celebration formula with a name before each factor and the item and boss
        /// factors the score also applies. Decimals, never fractions. v1.1.1 (W7a): colour, gold faces, card bonuses and
        /// the score enchantment are the one factor "加成" (1 + A).
        /// </summary>
        private string ScoreFormulaShort07(ScoreBreakdown b)
            =>HighScore073.Formula(b,n=>Factor(n),(n,d)=>ScoreFactorShort07(n,d),n=>ScoreFactorShort07(n),
                (key,value)=>FormulaLabel07(key)+" "+value,
                f=>NameOf(f.abilityId)+" "+PlayerNumberFormat.Ratio(ExactScoreMath.Parse(f.numerator),ExactScoreMath.Parse(f.denominator),9));
        private static string FormulaLabel07(string key)=>key=="base"?"基础":key=="hand"?"牌型":key=="additive"?"加成":key=="red"?"红组":key=="controller"?"卡牌":key;
        private void EndHighScore073(bool immediately)
        {
            if(highScore073==null||!highScore073.gameObject.activeSelf)return;
            if(immediately){HideHighScore073();return;}
            if(highScoreFade073<0)highScoreFade073=0;
        }
        private void HideHighScore073()
        {
            highScoreFade073=-1;highScoreTier073=0;
            if(highScore073!=null){UiKit.Clear(highScore073);highScore073.gameObject.SetActive(false);}
            highScoreGlows073.Clear();highScoreSparks073.Clear();
        }
        private void TickHighScore073(float seconds)
        {
            if(highScore073==null||!highScore073.gameObject.activeSelf||modal)return;
            // The celebration belongs to the play screen; another screen (settlement, shop, menu) takes it away at once.
            if(inMenu||!PlayPhase11){HideHighScore073();return;}
            highScoreElapsed073+=Mathf.Max(0,seconds);
            // Never leave before it could be read, even when a fast presentation ends at once.
            if(highScoreFade073>=0&&highScoreElapsed073>=HighScore073.MinVisibleSeconds)highScoreFade073+=Mathf.Max(0,seconds);
            EvaluateHighScore073();
            float fade=highScoreReduced073?HighScore073.ReducedFadeSeconds:HighScore073.FadeSeconds;
            if(highScoreFade073>=fade)HideHighScore073();
        }
        private void EvaluateHighScore073()
        {
            if(highScore073==null)return;
            float t=highScoreElapsed073;
            float fadeSeconds=highScoreReduced073?HighScore073.ReducedFadeSeconds:HighScore073.FadeSeconds;
            float fade=highScoreFade073<0?1:1-Mathf.Clamp01(highScoreFade073/fadeSeconds);
            highScoreGroup073.alpha=(highScoreReduced073?1:Mathf.Clamp01(t/.08f))*fade;
            // A slow breath on the rims; steady under reduced motion.
            float breath=highScoreReduced073?1:.72f+.28f*Mathf.Cos(t*7f);
            foreach(var glow in highScoreGlows073)if(glow!=null)glow.canvasRenderer.SetAlpha(breath);
            if(highScoreCrown073!=null&&!highScoreReduced073)
            {
                float drop=Mathf.Clamp01((t-.06f)/.2f);drop=1-(1-drop)*(1-drop);
                highScoreCrown073.localScale=Vector3.one*Mathf.Lerp(1.5f,1f,drop);highScoreCrown073.localRotation=Quaternion.Euler(0,0,Mathf.Sin(t*5f)*4f*(1-Mathf.Clamp01(t/1.2f)));
            }
            // Sparks rise and spread from the gain chip; they never travel down toward the trays.
            for(int i=0;i<highScoreSparks073.Count;i++)
            {
                var spark=highScoreSparks073[i];float delay=.02f*(i%5),life=Mathf.Clamp01((t-delay)/.7f),ease=1-(1-life)*(1-life);
                float angle=Mathf.PI*(.08f+.84f*i/Mathf.Max(1,highScoreSparks073.Count-1)),reach=(i%2==0?190:150)+i%3*22;
                spark.anchoredPosition=new Vector2(highScoreSparkOrigin073.x+Mathf.Cos(angle)*reach*ease,-(highScoreSparkOrigin073.y-Mathf.Sin(angle)*22*ease+44*ease*ease));
                spark.localScale=Vector3.one*(1-life*.7f);spark.localRotation=Quaternion.Euler(0,0,t*220f*(i%2==0?1:-1));
                var image=spark.GetComponent<Image>();var color=image.color;color.a=life>=1?0:1-life*life;image.color=color;
            }
        }
    }
}
