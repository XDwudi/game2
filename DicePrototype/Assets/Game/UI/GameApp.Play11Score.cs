using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DiceCraft.Core;
using DiceCraft.Presentation;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Vector2=UnityEngine.Vector2;
using Vector3=UnityEngine.Vector3;
using Quaternion=UnityEngine.Quaternion;

namespace DiceCraft
{
    // V1.1 task W11, PLAY_LAYOUT section 3 (B1): the score is shown where it is made. Each submitted die gets "+N" above it,
    // the hand name lands on the submission tray, triggered cards lift with their badge, the named formula chips at the
    // bottom of the dice tray light in order, and the total rolls into the top bar. Nothing is drawn over a submitted
    // die (AX-01); the high-score tiers light the top bar and the tray rims instead of a plate over the dice.
    // The beats, their timing and the speed settings are the existing PresentationSequence ones.
    public sealed partial class GameApp
    {
        private struct Factor11 { public int role; public string name,value; public Color tint; }
        /// <summary>
        /// The named factors of a score, in the order ScoreCalculator applies them. Factors of x1 are left out, as on the old
        /// celebration formula; roles keep their button names. v1.1.1 (W7a, RULES_DELTA 2 and 13): the list is
        /// ScoreFactors111's, so the chips multiply to the score before the final floor - Factor0 base, Factor1 hand,
        /// Factor3 red group, Factor5 the bonus line "加成 ×(1+A)" (it took the place of the colour chip Factor2 and of the
        /// enchantment chip: colour, gold faces, card bonuses and the score enchantment are its entries, shown when the
        /// chip is clicked), Factor4 the cards' multipliers, Factor6+ item and boss.
        /// </summary>
        private List<Factor11> Factors11(ScoreBreakdown b)
        {
            var list=new List<Factor11>();if(b==null)return list;int g=6;
            foreach(var f in ScoreFactors111.List(b,true))
            {
                string value=PlayerNumberFormat.Times(f.Numerator,f.Denominator,ChipDigits07);
                switch(f.Key)
                {
                    case "base":list.Add(new Factor11{role=0,name="基础分",value=PlayerNumberFormat.Integer(f.Numerator,9),tint=UiKit.Blue});break;
                    case "hand":
                        string hand=string.IsNullOrEmpty(b.chosenGroupId)?(b.straightMult100>0?"顺子":"牌型"):NameOf(b.chosenGroupId)+(b.straightMult100>0?"＋顺子":"");
                        list.Add(new Factor11{role=1,name=hand,value="×"+Factor(b.totalHandMult100),tint=UiKit.Red});break;
                    case "red":list.Add(new Factor11{role=3,name="红组",value="×"+Factor(b.redMult100),tint=UiKit.Red});break;
                    case "additive":list.Add(new Factor11{role=5,name="加成",value=value,tint=UiKit.Violet});break;
                    case "controller":list.Add(new Factor11{role=4,name="卡牌",value=value,tint=UiKit.Gold});break;
                    default:list.Add(new Factor11{role=g++,name=NameOf(f.SourceId),value=value,tint=f.Key=="boss"?UiKit.Boss:UiKit.Teal});break;
                }
            }
            return list;
        }
        /// <summary>True when a card or its enchantment added to the bonus line (then the line is shown with the cards).</summary>
        private static bool BonusFromCards11(ScoreBreakdown b)=>b?.additiveEntries!=null&&b.additiveEntries.Any(e=>!string.IsNullOrEmpty(e.emitterId));

        private enum Formula11Mode{Empty,Preview,Presenting,Stable,Recovered,LastHand}
        private int litChips11=-1;
        private float formulaLitAt11;
        /// <summary>V1.1: the score row at the bottom of the dice tray (and the expected hand on the submission tray).</summary>
        private void ScorePanel()
        {
            var s=ViewState;if(s?.hand==null||main==null)return;
            var l=L11;bool c=Large11;
            var row=UiKit.Rect(main,"ScorePanel",l.chips.x,l.chips.y,l.chips.width,l.chips.height);
            ScoreBreakdown b=null;ScorePreview preview=null;string caption;string total=null;Formula11Mode mode;
            bool recovered=RecoveredPresentationResultVisible07,stable=StablePresentationResultVisible07;
            stableSourceMarks07=0;
            // Card badges and tickets belong to one scored hand: they stay through its result and leave with it.
            if(!stable&&!IsPresenting07)ClearScore11();
            if(IsPresenting07&&receipt07!=null)
            {
                mode=Formula11Mode.Presenting;b=lastPresentationAfter07?.lastSubmission?.breakdown;caption=(presentationPlayer07.IsReplay?"重播 · ":"")+"计分中…";
            }
            else if(stable)
            {
                mode=Formula11Mode.Stable;b=s.lastSubmission?.breakdown;caption="上一手"+(receipt07.ChainCount>0?" · 生效 "+receipt07.ChainCount+" 张":"");total=CompactScore(receipt07.ScoreDecimal);
            }
            else if(recovered)
            {
                mode=Formula11Mode.Recovered;b=s.lastSubmission.breakdown;caption="已恢复上一手结果";total=CompactScore(b.score);
            }
            else
            {
                preview=DisplayScorePreview07();b=preview?.minimum;
                if(b!=null){mode=Formula11Mode.Preview;caption=preview.isRandom?"随机结果范围":b.multiplierFactors.Any(f=>f.abilityId=="C53")?"预计 · 调试覆盖":"预计";total=preview.isRandom?CompactScore(b.score)+" — "+CompactScore(preview.maximum.score):CompactScore(b.score);}
                else if(s.lastSubmission?.snapshot?.stageId==s.stage.stageId&&s.lastSubmission.breakdown!=null){mode=Formula11Mode.LastHand;b=s.lastSubmission.breakdown;caption="上一手";total=CompactScore(b.score);}
                else{mode=Formula11Mode.Empty;caption=s.stage.score=="0"?"本关还没有得分":"本关累计 "+CompactScore(s.stage.score);}
            }
            // Caption chip.
            float capW=mode==Formula11Mode.Empty?l.chips.width:Mathf.Clamp(PreferredWidth11(caption,Body11(c?17:20))+32,c?120:96,c?420:330);
            var capChip=Surface11(row,"FormulaCaption11",0,0,capW,l.chips.height,"ui_chip_formula",Color.Lerp(UiKit.Background,UiKit.Panel,.5f));
            NoWrap11(UiKit.Text(capChip,"FormulaCaptionText11",caption,10,0,capW-20,l.chips.height,Body11(c?17:20),LabelOn11(capChip,mode==Formula11Mode.Presenting?UiKit.Gold:UiKit.Muted),TextAlignmentOptions.Center));
            DrawHand11(mode,b,preview);
            if(mode==Formula11Mode.Empty){receiptNumber07=null;return;}
            // Right end: the record / random / receipt button; the total follows the last factor.
            string detailName=mode==Formula11Mode.Presenting?"PresentationDetails07":mode==Formula11Mode.Stable?"StablePresentationDetails07":mode==Formula11Mode.Recovered?"RecoveredDetails07":preview!=null&&b.multiplierFactors.Any(f=>f.abilityId=="C53")?"RandomPreview":null;
            float detailW=detailName==null?0:c?150:132;
            string totalName=mode==Formula11Mode.Stable?"StableResultScore07":mode==Formula11Mode.Recovered?"RecoveredScore07":"FinalScore";
            bool final=mode!=Formula11Mode.Presenting||beat07!=null&&beat07.Phase>=PresentationPhase.FinalScore;
            string totalText=mode==Formula11Mode.Presenting?(final?CompactScore(beat07.Phase==PresentationPhase.FinalScore?CountedScore11():receipt07.ScoreDecimal):"?"):total;
            string tier=mode==Formula11Mode.Presenting&&final||mode==Formula11Mode.Stable?TierWord11():"";
            float tierW=tier.Length>0?PreferredWidth11(tier,Body11(c?16:19))+12:0;
            float totalTextW=PreferredWidth11(totalText,c?26:32)+tierW;
            float totalW=Mathf.Clamp(totalTextW+36,c?140:130,c?520:460);
            // A high combo (ComboTier 3..5) bursts on the total chip at FinalScore (fx_combo_burst with its ring, arcs and
            // stars, Art071ComboEdgeAccents). The chip then widens to the left of its number so those marks have room
            // beside the digits; the room is reserved for the whole presentation, so the factor chips never re-flow.
            int combo=mode==Formula11Mode.Presenting&&receipt07!=null?receipt07.ComboTier:0;
            float comboRoom=combo>=5?200:combo==4?100:combo==3?40:0;
            float right=l.chips.width-(detailW>0?detailW+8:0);
            string post=mode==Formula11Mode.Stable?StablePostSummary11():"";
            float postFont=Body11(c?15:18),postW=post.Length>0?Mathf.Min(c?420:360,PreferredWidth11(post,postFont)+12):0,postMin=Mathf.Min(postW,c?200:160);
            // Factor chips between the caption and the total, separated by "x" and closed by "=", then the total.
            float x0=capW+12;
            float font=c?22:26,sep=c?26:30,pad=30;
            // Chip names are body text: the 19.5-unit minimum scaled by the text size, like the other text (W11b). They
            // never shrink. When the row is short of room the values and signs tighten first (values to 75 %), then the
            // post-score summary gives up width down to its minimum (it ends with an ellipsis; the record has it all), then
            // the rightmost factors show their value only. The base keeps its name; the breakdown (a click) names them all.
            const float nameFont=19.5f;
            var factors=Factors11(b);
            var named=Enumerable.Repeat(true,factors.Count).ToArray();
            float[] Widths()=>factors.Select((f,i)=>PreferredWidth11(f.value,font)+(named[i]?PreferredWidth11(f.name,nameFont)+pad:pad-6)).ToArray();
            float Available()=>right-(postW>0?postW+8:0)-totalW-comboRoom-x0-6;
            var widths=Widths();float need=widths.Sum()+sep*factors.Count;
            if(need>Available())
            {
                float squeeze=Mathf.Max(.75f,Available()/need);font*=squeeze;sep*=Mathf.Max(.7f,squeeze);pad=24;
                widths=Widths();need=widths.Sum()+sep*factors.Count;
                if(need>Available()&&postW>postMin)postW=Mathf.Max(postMin,postW-(need-Available()));
                for(int i=factors.Count-1;i>=1&&need>Available();i--){named[i]=false;widths=Widths();need=widths.Sum()+sep*factors.Count;}
            }
            // After the hand (also when its presentation was skipped) the changes made after scoring stay readable beside
            // the record button: "后置：提交收入 +1 · 永久改点 2 面" (v0.73 kept them in its result rail).
            if(post.Length>0)
            {
                right-=postW+8;
                var line=UiKit.Text(row,"StableResultFactors07",post,right+8,0,postW,l.chips.height,postFont,Color.Lerp(OnFelt11,UiKit.Felt,.2f),TextAlignmentOptions.MidlineRight);
                line.textWrappingMode=TextWrappingModes.NoWrap;line.overflowMode=TextOverflowModes.Ellipsis;
            }
            float totalX=Mathf.Min(right-totalW-comboRoom,x0+widths.Sum()+sep*factors.Count);
            float chipW=totalW+(final?comboRoom:0);
            var totalChip=Surface11(row,"FinalScoreChip11",totalX,0,chipW,l.chips.height,"ui_chip_formula",final?UiKit.Reward:Color.Lerp(UiKit.Background,UiKit.Panel,.5f));
            Color totalInk=LabelOn11(totalChip,final?(Color?)null:UiKit.Muted);
            var number=NoWrap11(UiKit.Text(totalChip,totalName,totalText,chipW-totalW+14,0,totalW-28-tierW,l.chips.height,c?26:32,totalInk,TextAlignmentOptions.MidlineRight));
            if(final&&art07NumberFont!=null&&mode!=Formula11Mode.Preview&&totalText.IndexOf(',')<0)number.font=art07NumberFont;
            if(tier.Length>0)NoWrap11(UiKit.Text(totalChip,"TierWord11",tier,chipW-14-PreferredWidth11(tier,Body11(c?16:19)),0,PreferredWidth11(tier,Body11(c?16:19))+2,l.chips.height,Body11(c?16:19),totalInk,TextAlignmentOptions.MidlineLeft));
            receiptNumber07=mode==Formula11Mode.Presenting?number:null;
            if(receiptNumber07!=null){number.rectTransform.pivot=new Vector2(1,.5f);number.rectTransform.anchoredPosition=new Vector2(chipW-14-tierW,-l.chips.height*.5f);receiptNumberOrigin07=number.rectTransform.anchoredPosition;}
            if(detailName!=null)
            {
                string label=detailName=="RecoveredDetails07"?"得分明细":detailName=="RandomPreview"?(b.multiplierFactors.Where(f=>f.abilityId=="C53").Select(f=>f.stateOwnerId).Distinct().Count()>1?"随机来源":preview.isRandom?"9 种结果":"覆盖来源"):"完整记录";
                Action click=detailName=="RecoveredDetails07"?(Action)(()=>ShowBreakdown(s.lastSubmission.snapshot,s.lastSubmission.breakdown)):detailName=="RandomPreview"?ShowRandomPreview07:ShowCommittedPresentationDetails07;
                var detail=SolidButton11(row,detailName,label,l.chips.width-detailW,0,detailW,l.chips.height,click,detailName!="RandomPreview"||!LockVisualPlaying07,Body11(c?15:18));
                var t=detail.GetComponentInChildren<TextMeshProUGUI>();t.textWrappingMode=TextWrappingModes.Normal;t.lineSpacing=-12;
            }
            float x=x0;int litCount=0;
            var snapshot=mode==Formula11Mode.Preview?DisplayScoreSnapshot07():s.lastSubmission?.snapshot;
            for(int i=0;i<factors.Count;i++)
            {
                var f=factors[i];bool lit=ChipLit11(mode,f.role,b);if(lit)litCount++;
                bool clickable=mode==Formula11Mode.Preview||mode==Formula11Mode.Stable||mode==Formula11Mode.Recovered||mode==Formula11Mode.LastHand;
                var bd=b;var chip=FormulaChip11(row,f,x,widths[i],l.chips.height,lit,font,nameFont,named[i],clickable&&!LockVisualPlaying07,()=>ShowBreakdown(snapshot,bd));
                if(lit&&mode==Formula11Mode.Presenting&&litCount>litChips11&&litChips11>=0)UiKit.Component<Play11Pop>(chip.gameObject).Play((litCount-litChips11-1)*.06f);
                x+=widths[i];
                // The signs sit straight on the felt (the tray is an open frame): they take the felt's contrast colour.
                NoWrap11(UiKit.Text(row,"FormulaSep11_"+i,i<factors.Count-1?"×":"=",x,0,sep,l.chips.height,c?20:24,lit?OnFelt11:Color.Lerp(OnFelt11,UiKit.Felt,.45f),TextAlignmentOptions.Center));
                x+=sep;
            }
            litChips11=mode==Formula11Mode.Presenting?litCount:-1;
            if(mode==Formula11Mode.Stable)StableCardBadges11();
        }
        private string StablePostSummary11()
        {
            var post=receipt07?.Events.Where(e=>e.Phase==PresentationPhase.PostScore).ToArray();
            return post==null||post.Length==0?"":"后置："+PostSummary07(new PresentationBeat(PresentationPhase.PostScore,0,0,null,true,post));
        }
        private bool ChipLit11(Formula11Mode mode,int role,ScoreBreakdown b=null)
        {
            if(mode!=Formula11Mode.Presenting)return true;
            var phase=beat07?.Phase??PresentationPhase.Receipt;
            if(phase>=PresentationPhase.FinalScore)return true;
            if(phase==PresentationPhase.ControllerAndEnchant)return role<=5;
            // v1.1.1: the bonus line (role 5) lights with the cards when a card added to it, otherwise with the hand
            // and the red group - the phase PresentationReceiptAdapter gives its "additive" event.
            if(phase==PresentationPhase.BaseAndStructure)return role<=3||role==5&&!BonusFromCards11(b);
            return false;
        }
        private RectTransform FormulaChip11(Transform row,Factor11 f,float x,float w,float h,bool lit,float font,float nameFont,bool showName,bool clickable,Action click)
        {
            var button=UiKit.Button(row,"Factor"+f.role,f.value,x,0,w,h,click,clickable);
            var root=(RectTransform)button.transform;
            root.Find("Step").gameObject.SetActive(false);root.Find("Lip").gameObject.SetActive(false);
            var face=(Image)button.targetGraphic;face.sprite=UiKit.Template("ui_chip_formula");face.type=face.sprite!=null?Image.Type.Sliced:Image.Type.Simple;
            face.rectTransform.anchoredPosition=Vector2.zero;face.rectTransform.sizeDelta=new Vector2(w,h);
            face.color=lit?f.tint:Color.Lerp(UiKit.Background,UiKit.Panel,.5f);
            // W11b: name and value take the palette colour that reads on this chip as drawn (4.5:1).
            Color ink=lit?LabelOn11(face):LabelOn11(face,UiKit.Muted);
            float nameW=showName?PreferredWidth11(f.name,nameFont)+2:0;
            var name=NoWrap11(UiKit.Text(root,"Name",f.name,10,0,Mathf.Max(1,nameW),h,nameFont/Mathf.Max(.01f,UiKit.FontScale),ink,TextAlignmentOptions.MidlineLeft));
            name.fontSize=nameFont*UiKit.FontScale;name.gameObject.SetActive(showName);
            var label=root.Find("Label").GetComponent<TextMeshProUGUI>();
            label.text=f.value;label.color=ink;label.fontSize=font*UiKit.FontScale;label.alignment=TextAlignmentOptions.MidlineRight;label.textWrappingMode=TextWrappingModes.NoWrap;
            // The value takes everything right of the name up to the chip's inner edge (8 units).
            label.rectTransform.anchoredPosition=new Vector2(10+nameW+(showName?2:0),0);label.rectTransform.sizeDelta=new Vector2(w-nameW-(showName?20:18),h);
            return root;
        }
        /// <summary>Width of a one-line text at a design size: the font's own glyph advances (main font, then its fallbacks),
        /// an em-based estimate only for a glyph no font has loaded yet. Rich-text tags count as text (a safe overestimate).</summary>
        private static float PreferredWidth11(string text,float size,TMP_FontAsset font=null)
        {
            if(string.IsNullOrEmpty(text))return 0;float w=0,px=size*UiKit.FontScale;
            foreach(char ch in text)
            {
                float advance=GlyphAdvance11(font??UiKit.Font,ch,px,0);
                w+=advance>=0?advance:(ch<0x2E80?(ch==' '?.3f:.62f):1f)*px;
            }
            return Mathf.Ceil(w)+1;
        }
        private static float GlyphAdvance11(TMP_FontAsset font,uint ch,float px,int depth)
        {
            if(font==null||depth>3)return -1;
            if(font.characterLookupTable!=null&&font.characterLookupTable.TryGetValue(ch,out var c)&&c.glyph!=null)
                return c.glyph.metrics.horizontalAdvance*c.scale*c.glyph.scale*px/Mathf.Max(1,font.faceInfo.pointSize)*(font.faceInfo.scale>0?font.faceInfo.scale:1);
            if(font.fallbackFontAssetTable!=null)foreach(var fallback in font.fallbackFontAssetTable){float a=GlyphAdvance11(fallback,ch,px,depth+1);if(a>=0)return a;}
            return -1;
        }
        /// <summary>The expected (or scored) hand on the submission tray: it "lands" when the hand beat starts.</summary>
        private void DrawHand11(Formula11Mode mode,ScoreBreakdown b,ScorePreview preview)
        {
            var l=L11;bool c=Large11;float ix=l.info.x+16,iw=l.info.width-32,top=submitHandTop11;
            float bottom=ViewState.lastSubmission!=null?l.info.yMax-76:l.info.yMax-12;float h=Mathf.Max(30,bottom-top);
            string text;Color color=UiKit.TextColor;float size=Body11(c?17:21);
            if(b==null)
            {
                text=ViewState.hand.hasRolled?"锁定骰子后显示预计牌型":"";color=UiKit.Muted;size=Body11(c?15:18);
            }
            else
            {
                string primary=string.IsNullOrEmpty(b.primaryHandId)?"散点":NameOf(b.primaryHandId);
                string chosen=string.IsNullOrEmpty(b.chosenGroupId)?(b.straightMult100>0?"顺子":"散点"):NameOf(b.chosenGroupId)+(b.straightMult100>0?"＋顺子":"");
                string colorName=string.IsNullOrEmpty(b.colorId)?"混色":NameOf(b.colorId);
                string structure=primary==chosen?chosen:"结构 "+primary+"／计分 "+chosen;
                if(mode==Formula11Mode.Presenting)
                {
                    bool landed=beat07!=null&&beat07.Phase>=PresentationPhase.BaseAndStructure;
                    text=landed?structure+"\n"+colorName:"";color=UiKit.Gold;size=c?20:26;
                }
                else if(mode==Formula11Mode.Preview)text=(preview!=null&&preview.isRandom?"随机结果范围":"预计")+"："+structure+"\n"+colorName;
                else text=structure+"\n"+colorName;
            }
            var hand=UiKit.Text(main,"Hand",text,ix,top,iw,h,size,LabelOn11(main.Find("SubmitInfo11") as RectTransform,color),TextAlignmentOptions.TopLeft);
            hand.lineSpacing=-10;hand.enableAutoSizing=true;hand.fontSizeMax=size*UiKit.FontScale;hand.fontSizeMin=Body11(14)*UiKit.FontScale;
            if(mode==Formula11Mode.Presenting&&text.Length>0&&beat07?.Phase==PresentationPhase.BaseAndStructure)UiKit.Component<Play11Pop>(hand.gameObject).Play(0);
        }
        private string TierWord11()
        {
            if(receipt07==null)return "";
            int tier=HighScore073.Tier(receipt07.ScoreDecimal,receipt07.StageTarget,receipt07.ComboTier);
            // DEV_PLAN 4.1 item 5: the three banner tiers are named by what they mean.
            return tier==HighScore073.Burst?"两倍目标！":tier==HighScore073.Super?"一手达标！":tier==HighScore073.High?"过半！":"";
        }
        private string CountedScore11()
        {
            if(receipt07==null||beat07==null||beat07.Phase!=PresentationPhase.FinalScore)return receipt07?.ScoreDecimal??"0";
            var options=presentationPlayer07.Sequence.Options;
            if(options.ReducedMotion||reduced||options.Speed!=PresentationSpeed.Normal||receipt07.ScoreDecimal.Length>15)return receipt07.ScoreDecimal;
            float t=Mathf.Clamp01((float)(presentationPlayer07.Clock.ElapsedMilliseconds-beat07.StartMs)/Mathf.Max(1,beat07.DurationMs*.8f));
            return HighScore073.CountValue(receipt07.ScoreDecimal,1-(1-t)*(1-t)*(1-t));
        }
        private string RolledStageScore11()
        {
            if(receipt07==null||beat07==null)return ViewState.stage.score;
            var options=presentationPlayer07.Sequence.Options;
            BigInteger from=ExactScoreMath.Parse(receipt07.StageScoreBefore),to=ExactScoreMath.Parse(receipt07.StageScoreAfter);
            if(options.ReducedMotion||options.Speed!=PresentationSpeed.Normal||to<=from)return receipt07.StageScoreAfter;
            float t=Mathf.Clamp01((float)(presentationPlayer07.Clock.ElapsedMilliseconds-beat07.StartMs)/Mathf.Max(1,beat07.DurationMs));
            int permille=Mathf.Clamp((int)((1-(1-t)*(1-t))*1000),0,1000);
            return ExactScoreMath.SaveDecimal(from+(to-from)*permille/1000);
        }

        // ---------------------------------------------------------------- source effects (score layer above the screen)
        private RectTransform score11Layer;
        private readonly Dictionary<string,RectTransform> cardBadges11=new Dictionary<string,RectTransform>(StringComparer.Ordinal);
        private readonly List<CardLift11> cardLifts11=new List<CardLift11>();
        private sealed class CardLift11{public RectTransform card;public Vector2 rest;public double startMs;}
        public int ScoreTickets11=>score11Layer==null?0:score11Layer.Cast<Transform>().Count(t=>t.gameObject.activeSelf&&t.name.StartsWith("DieTicket11_",StringComparison.Ordinal));
        public int ScoreCardBadges11=>score11Layer==null?0:score11Layer.Cast<Transform>().Count(t=>t.gameObject.activeSelf&&t.name.StartsWith("CardBadge11_",StringComparison.Ordinal));
        private RectTransform Score11Layer()
        {
            if(score11Layer==null)
            {
                score11Layer=UiKit.Rect(canvas.transform,"Score11Layer",0,0,1920,1080);
                var group=UiKit.Component<CanvasGroup>(score11Layer.gameObject);group.blocksRaycasts=false;group.interactable=false;
                // The tier words use glyphs outside the static atlas; this always-active, invisible text keeps their fallback
                // material alive across the replays (the 200-replay stability rule).
                var keeper=UiKit.Text(canvas.transform,"Score11Glyphs","过半！一手达标两倍目标计分中…",0,0,8,8,20,Color.clear);
                keeper.textWrappingMode=TextWrappingModes.NoWrap;keeper.transform.SetSiblingIndex(0);
            }
            score11Layer.anchoredPosition=main.anchoredPosition;
            score11Layer.SetSiblingIndex(Math.Min(canvas.transform.childCount-1,main.GetSiblingIndex()+1));
            if(overlay!=null)overlay.SetAsLastSibling();
            return score11Layer;
        }
        private void ClearScore11()
        {
            foreach(var lift in cardLifts11)if(lift.card!=null)lift.card.anchoredPosition=lift.rest;
            cardLifts11.Clear();cardBadges11.Clear();litChips11=-1;
            if(score11Layer!=null)UiKit.Clear(score11Layer);
        }
        /// <summary>Per beat: "+N" over each scoring die in its slot, a badge on each card that took part, the gain in the top bar.</summary>
        private void PresentScoreBeat11(PresentationBeat beat)
        {
            if(receipt07==null||!receipt07.IsSubmission)return;
            var layer=Score11Layer();bool c=Large11;
            if(beat.Phase==PresentationPhase.DiceContributions||beat.Phase==PresentationPhase.FinalScore&&beat.IsAggregate&&presentationPlayer07.Sequence.Options.Speed==PresentationSpeed.Instant)
            {
                var sealedDice=lastPresentationAfter07?.lastSubmission?.breakdown?.sealedDice;var order=lastPresentationBefore07?.hand?.orderedLockedIds;
                int n=0;
                foreach(var group in beat.Events.Where(e=>e.Phase==PresentationPhase.DiceContributions).SelectMany(e=>e.TargetDieIds.Select(id=>new{Id=id,Event=e})).GroupBy(x=>x.Id))
                {
                    if(!lockedSlotRects07.TryGetValue(group.Key,out var slot)||!dieCenters.TryGetValue(group.Key,out var center))continue;
                    int index=order==null?-1:order.IndexOf(group.Key);bool isSealed=sealedDice!=null&&index>=0&&index<sealedDice.Length&&sealedDice[index];
                    var last=group.Last().Event;string value=isSealed?"封印":"+"+DisplayFraction07(last.AfterValue??last.Operand??"0");
                    // The ticket pops from its centre (up to Play11Pop's 1.12): it stays inside its slot at the peak too.
                    float w=Mathf.Clamp(PreferredWidth11(value,c?18:22)+24,64,Mathf.Max(64,(slot.width-6)/1.12f));
                    float tx=Mathf.Clamp(center.x-w*.5f,slot.x+2+w*.06f,slot.xMax-2-w*1.06f);
                    var ticket=Surface11(layer,"DieTicket11_"+group.Key,tx,slot.y+4,w,32,"ui_chip_formula",isSealed?UiKit.Danger:UiKit.Gold);
                    ticket.pivot=new Vector2(.5f,.5f);ticket.anchoredPosition=new Vector2(tx+w*.5f,-(slot.y+4+16));
                    var box=ChipText11(ticket);
                    NoWrap11(UiKit.Text(ticket,"Value",value,box.x,box.y,box.width,box.height,c?18:22,LabelOn11(ticket),TextAlignmentOptions.Center));
                    UiKit.Component<Play11Pop>(ticket.gameObject).Play(n++*.04f);
                }
            }
            foreach(string id in beat.Events.Where(e=>e.IsScoring&&e.CountsForChain).SelectMany(e=>e.SourceEmitterIds).Distinct(StringComparer.Ordinal))
                CardBadge11(layer,id,true);
            if(beat.Phase==PresentationPhase.FinalScore)TopGain11(layer);
            // The gain chip has been read by the time the post-score changes start; its slot then carries a gold change.
            if(beat.Phase>=PresentationPhase.PostScore){var gain=layer.Find("TopGain11");if(gain!=null)gain.gameObject.SetActive(false);}
        }
        /// <summary>The badge of a card that took part in this score: "+6×4", "×1.3"; the card lifts once as it appears.</summary>
        private void CardBadge11(Transform layer,string emitterId,bool animate)
        {
            if(cardBadges11.ContainsKey(emitterId)||!controllerRects.TryGetValue(emitterId,out var card)||card==null||!card.gameObject.activeInHierarchy)return;
            if(!TryCardBounds07(emitterId,out var bounds))return;
            string text=CardContribution11(emitterId);if(string.IsNullOrEmpty(text))return;
            bool c=Large11;float w=Mathf.Clamp(PreferredWidth11(text,c?16:20)+22,58,Mathf.Max(58,bounds.width-8)),h=c?30:32;
            var badge=Surface11(layer,"CardBadge11_"+emitterId,bounds.xMax-w-4,bounds.yMin+2,w,h,"ui_chip_formula",UiKit.Reward);
            // Pinned at its top-right corner: the pop grows it into the card, never past the card's edge.
            badge.pivot=new Vector2(1,1);badge.anchoredPosition=new Vector2(bounds.xMax-4,-(bounds.yMin+2));
            var box=ChipText11(badge);
            NoWrap11(UiKit.Text(badge,"Value",text,box.x,box.y,box.width,box.height,c?16:20,LabelOn11(badge),TextAlignmentOptions.Center));
            cardBadges11[emitterId]=badge;
            if(animate)
            {
                UiKit.Component<Play11Pop>(badge.gameObject).Play(0);
                if(!reduced&&presentationPlayer07!=null&&presentationPlayer07.Sequence?.Options.ReducedMotion==false)
                    cardLifts11.Add(new CardLift11{card=card,rest=card.anchoredPosition,startMs=presentationPlayer07.Clock.ElapsedMilliseconds});
            }
        }
        /// <summary>Everything a card did to this score, summed per operation: "+6×4" (four +6), "+20", "×1.3".</summary>
        private string CardContribution11(string emitterId)
        {
            if(receipt07==null)return null;
            var events=receipt07.Events.Where(e=>e.IsScoring&&e.CountsForChain&&e.SourceEmitterIds.Contains(emitterId)&&!string.IsNullOrEmpty(e.Operand)).ToArray();
            // v1.1.1 (W7a): a bonus of the additive zone (the card's own or its score enchantment's) is not a base add:
            // it reads "加成+1.5" and is never summed with base points.
            bool Bonus(PresentationEvent e)=>e.EffectFamily=="controller_additive"||e.EffectFamily=="enchantment_additive";
            var adds=events.Where(e=>e.Operation=="+"&&!Bonus(e)).Select(e=>ExactFraction.Parse(e.Operand)).ToArray();
            var bonuses=events.Where(e=>e.Operation=="+"&&Bonus(e)).Select(e=>ExactFraction.Parse(e.Operand)).ToArray();
            var muls=events.Where(e=>e.Operation=="×").Select(e=>ExactFraction.Parse(e.Operand)).ToArray();
            if(muls.Length>0)
            {
                BigInteger num=1,den=1;foreach(var m in muls){num*=m.Numerator;den*=m.Denominator;}
                return PlayerNumberFormat.Times(num,den,ChipDigits07);
            }
            string bonus=null;
            if(bonuses.Length>0)
            {
                BigInteger num=0,den=1;foreach(var a in bonuses){num=num*a.Denominator+a.Numerator*den;den*=a.Denominator;}
                bonus="加成+"+PlayerNumberFormat.Ratio(num,den,6);
            }
            if(adds.Length>0)
            {
                bool same=adds.All(a=>a.Numerator==adds[0].Numerator&&a.Denominator==adds[0].Denominator);
                if(same&&adds.Length>1)return "+"+PlayerNumberFormat.Ratio(adds[0].Numerator,adds[0].Denominator,6)+"×"+adds.Length+(bonus==null?"":" "+bonus);
                BigInteger num=0,den=1;foreach(var a in adds){num=num*a.Denominator+a.Numerator*den;den*=a.Denominator;}
                return "+"+PlayerNumberFormat.Ratio(num,den,6)+(bonus==null?"":" "+bonus);
            }
            if(bonus!=null)return bonus;
            var other=receipt07.Events.FirstOrDefault(e=>e.IsScoring&&e.SourceEmitterIds.Contains(emitterId));
            return other==null?null:"✓";
        }
        /// <summary>After the presentation the badges stay on the cards that scored, until the next throw or lock.</summary>
        private void StableCardBadges11()
        {
            if(receipt07==null)return;var layer=Score11Layer();
            foreach(string id in receipt07.ScoringEmitterIds){CardBadge11(layer,id,false);if(cardBadges11.ContainsKey(id))stableSourceMarks07++;}
        }
        private void TopGain11(Transform layer)
        {
            if(topScoreFrame11==null||receipt07==null)return;bool c=Large11;
            var corners=new Vector3[4];topScoreFrame11.GetWorldCorners(corners);var a=ScreenToDesign(RectTransformUtility.WorldToScreenPoint(null,corners[1]));var b=ScreenToDesign(RectTransformUtility.WorldToScreenPoint(null,corners[3]));
            string text="+"+CompactScore(receipt07.ScoreDecimal);float w=Mathf.Clamp(PreferredWidth11(text,c?20:24)+28,90,260);
            var gain=Surface11(layer,"TopGain11",b.x+12,a.y+8,w,44,"ui_chip_formula",UiKit.Reward);
            var box=ChipText11(gain);
            NoWrap11(UiKit.Text(gain,"Value",text,box.x,box.y,box.width,box.height,c?20:24,LabelOn11(gain),TextAlignmentOptions.Center));
            UiKit.Component<Play11Pop>(gain.gameObject).Play(0);
        }
        /// <summary>Per frame while scoring: card lifts, the total counting up, the top-bar score rolling in.</summary>
        private void TickScore11()
        {
            if(!IsPresenting07)return;
            double now=presentationPlayer07.Clock.ElapsedMilliseconds;
            for(int i=cardLifts11.Count-1;i>=0;i--)
            {
                var lift=cardLifts11[i];if(lift.card==null){cardLifts11.RemoveAt(i);continue;}
                float t=(float)(now-lift.startMs)/280f;
                float offset=t>=1?0:10*Mathf.Sin(Mathf.Clamp01(t)*Mathf.PI);
                lift.card.anchoredPosition=lift.rest+Vector2.up*offset;
                if(t>=1){lift.card.anchoredPosition=lift.rest;cardLifts11.RemoveAt(i);}
            }
            if(beat07?.Phase==PresentationPhase.FinalScore)
            {
                if(receiptNumber07!=null){string counted=CompactScore(CountedScore11());if(receiptNumber07.text!=counted)receiptNumber07.text=counted;}
                if(topScore11!=null)
                {
                    string rolled=RolledStageScore11(),shown=CompactScore(rolled);
                    if(topScore11.text!=shown)
                    {
                        topScore11.text=shown;
                        // The settle panel's gap counts down with the rolling total; the moment the total reaches the target the
                        // panel turns to the end-now rewards (it never reads "离目标还差 0").
                        var gapText=main.Find("GoalGap11");
                        if(gapText!=null&&gapText.gameObject.activeSelf)
                        {
                            var g=ExactScoreMath.Parse(ViewState.stage.target)-ExactScoreMath.Parse(rolled);
                            if(g.Sign<=0)DrawSettle11();else gapText.GetComponent<TextMeshProUGUI>().text=CompactScore(g.ToString());
                        }
                        var fill=topScoreFrame11?.Find("StageProgress072/Fill");var ease=fill!=null?fill.GetComponent<Art072ProgressEase>():null;
                        if(ease!=null){BigIntegerRatio11(rolled,ViewState.stage.target,out float ratio);var track=(RectTransform)fill.parent;float width=ratio<=0?0:Mathf.Max(7,track.rect.width*Mathf.Clamp01(ratio));ease.Configure(ViewState.runId+"|"+ViewState.stage.stageId,width,7);}
                    }
                }
            }
        }
    }

    /// <summary>V1.1: a one-shot pop (scale 0.7 to 1.12 to 1) for a value that has just appeared. Decorative; rests at once under reduced motion.</summary>
    public sealed class Play11Pop:MonoBehaviour
    {
        public const float Duration=.2f;
        private float started=-1,delay;
        public bool Playing=>started>=0;
        public void Play(float wait)
        {
            if(UiKit.ReducedMotion){started=-1;transform.localScale=Vector3.one;return;}
            delay=Mathf.Max(0,wait);started=Time.unscaledTime;Apply();
        }
        private void LateUpdate(){if(started>=0)Apply();}
        private void Apply()
        {
            float t=(Time.unscaledTime-started-delay)/Duration;float scale;
            if(t<0)scale=.7f;else if(t>=1){scale=1;started=-1;}
            else scale=t<.6f?Mathf.Lerp(.7f,1.12f,t/.6f):Mathf.Lerp(1.12f,1f,(t-.6f)/.4f);
            transform.localScale=new Vector3(scale,scale,1);
        }
        private void OnDisable(){started=-1;transform.localScale=Vector3.one;}
    }
}
