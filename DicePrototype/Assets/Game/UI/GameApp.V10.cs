using System;
using System.Collections.Generic;
using System.Linq;
using DiceCraft.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace DiceCraft
{
    // p1.0.0 (docs/specs/v10/RULES_SPEC.md section 9): the three-stage cycle panel on the stage-select screen and in
    // the shop, the boss modifier readout in a stage, and the dialog for boss loot and controller gifts.
    public sealed partial class GameApp
    {
        // ---------------------------------------------------------------- shared state helpers
        private bool InStageView10 => ViewState?.stage!=null&&ViewState.stage.started&&!ViewState.stage.settled&&(ViewState.phase=="HandReady"||ViewState.phase=="Selecting");
        /// <summary>Controllers the boss modifier has disabled for the stage being played.</summary>
        private string[] ViewDisabled10 => InStageView10?ViewState.stage.disabledControllerIds:Array.Empty<string>();
        private bool DisabledNow10(string instanceId)=>ViewDisabled10.Contains(instanceId);
        /// <summary>Before a boss stage: the controllers its modifier will disable when the stage starts.</summary>
        private string[] DisabledNextIds10()
        {
            if(ViewState==null)return Array.Empty<string>();
            if(ViewState.phase=="StagePreview")return Session.EntryView()?.disabledControllerIds??Array.Empty<string>();
            if(ViewState.phase!="Shop")return Array.Empty<string>();
            int next=ViewState.stage.stageIndex+1;
            if(next>=Config.stages.Length||Config.stages[next].stageKind!="boss"||next/3>=ViewState.cycles.Count)return Array.Empty<string>();
            var after=ControllerRules.AfterAltars(ViewState.controllers);
            return after.Any(c=>c.definitionId=="C131")?Array.Empty<string>():ControllerRules.DisabledBy(after,Config,ViewState.cycles[next/3].modifierId);
        }
        private bool DisabledNext10(string instanceId)=>DisabledNextIds10().Contains(instanceId);
        /// <summary>What an owned controller sells for, including the recycler (C140).</summary>
        private long OwnedSalePrice10(ControllerInstance c)=>Session.ControllerSalePriceIn(ViewState,c);
        private string StageKindLabel10(int stageIndex,bool risk)
        {
            string kind=Config.stages[Mathf.Clamp(stageIndex,0,Config.stages.Length-1)].stageKind;
            return kind=="boss"?"首领关":kind=="pressure"?(risk?"高压挑战":"高压关"):"普通关";
        }
        private string LootText10(string lootKind)=>string.IsNullOrEmpty(lootKind)?"终局首领 · 通关即胜利":NameOf(lootKind)+"："+Description(lootKind);

        // ---------------------------------------------------------------- sealed dice, must-fill, extra score factors
        /// <summary>Which locked dice the boss modifier seals in the hand as it stands (empty without a sealing modifier).</summary>
        private bool[] SealedPreview10()
        {
            if(!InStageView10||ViewState.phase!="Selecting"||Session.CurrentModifierId==null||ViewState.hand.orderedLockedIds.Count==0)return Array.Empty<bool>();
            return Session.PreviewScoreRange()?.minimum?.sealedDice??Array.Empty<bool>();
        }
        private void SealedMark10(float x,float y,float w,float h,int index)
        {
            UiKit.Outline(main,"SealedSlot10_"+index,x+3,y+3,w-6,h-6,UiKit.Danger,2);
            var tag=UiKit.Text(main,"SealedTag10_"+index,"封印",x+6,y+h-30,64,26,15,UiKit.Danger);tag.fontStyle=FontStyles.Bold;tag.textWrappingMode=TextWrappingModes.NoWrap;
        }
        private bool MustFillBlocked10 => InStageView10&&ControllerRules.ModifierHas(Config,Session.CurrentModifierId,"MustFill")
            &&ViewState.hand.orderedLockedIds.Count!=Math.Min(ViewState.stage.capacity,ViewState.dice.Count);
        // ---------------------------------------------------------------- in-stage boss readout
        private void DrawModifierChip10(Transform panel,float width)
        {
            if(!InStageView10||ViewState.stage.stageKind!="boss"||string.IsNullOrEmpty(ViewState.stage.modifierId))return;
            bool lifted=ViewState.stage.modifierNullified;string id=ViewState.stage.modifierId;
            // The readout sits in the free band of the left column, between the budget counters and the buttons. (Its first
            // version was a 164 px box lower down, which the previous-hand, handbook and pause buttons covered.) The text
            // shrinks to fit two lines; a click or hover gives the full stage intel.
            var box=UiKit.Frame(panel,"BossModifier10",12,709,width-24,67,Color.Lerp(UiKit.Background,UiKit.Danger,lifted?.04f:.16f),lifted?UiKit.PanelLight:UiKit.Danger);
            // The title row is 30 px for a 16 px face: a row shorter than the line height renders nothing at all in ellipsis mode.
            var title=UiKit.Text(box,"BossModifierName",(lifted?"减益已解除 · ":"首领减益 · ")+NameOf(id),10,0,width-44,30,16,lifted?UiKit.Muted:UiKit.Paper);
            title.fontStyle=FontStyles.Bold;title.textWrappingMode=TextWrappingModes.NoWrap;title.overflowMode=TextOverflowModes.Overflow;
            var body=UiKit.Text(box,"BossModifierText",Description(id),10,27,width-44,38,14,lifted?UiKit.Muted:UiKit.TextColor);
            body.enableAutoSizing=true;body.fontSizeMin=11;body.fontSizeMax=14;body.overflowMode=TextOverflowModes.Ellipsis;body.lineSpacing=-8;
            var open=HitArea073(box,"BossModifierOpen",0,0,width-24,67,()=>{if(!IsBusy)ShowCycle10();});
            Art07AttachTooltip(open,"boss-modifier",NameOf(id),Description(id),lifted?"已解除":"本关生效",()=>{if(!IsBusy)ShowCycle10();});
        }

        // The stage selector and the chapter intelligence dialog share the same three cards.
        private const float StageWidth16 = 472, StageContent16 = StageWidth16 - 48;
        private float StageHeight16 => Large11 ? 592 : 452;

        private void StagePreview10()
        {
            DrawCards11();
            var rules = Surface11(main, "PrepRules16", 1496, 96, 392, 244, "ui_stage_card", UiKit.Paper);
            string budgets = $"每手最多掷 {Session.InitialRolls} 次\n每关 {Session.InitialSubmissions} 次提交\n每次最多提交 {Session.EffectiveCapacity} 颗";
            UiKit.Text(rules, "PreviewBudgets", budgets, 20, 12, 352, 220, 22, LabelOn11(rules, UiKit.Ink), TextAlignmentOptions.MidlineLeft);
            ChapterStrip16();
            DrawCycleCards10(main, 32, Large11 ? 468 : 448, true);
            StageEntryNotes16();
            StartStageButton16();
            BindStageCardsEntrance07();
        }

        private void ChapterStrip16()
        {
            float height = Large11 ? 104 : 84, headerH = Large11 ? 42 : 30, tickH = Large11 ? 40 : 28;
            var strip = Surface11(main, "ChapterStrip16", 32, 352, 1448, height, "ui_section_panel", UiKit.Section(UiKit.Panel));
            int stage = ViewState.stage.stageIndex;
            for (int chapter = 0; chapter < Config.stages.Length / 3; chapter++)
            {
                float x = chapter * 181 + 8, tickY = 8 + headerH;
                bool active = chapter == stage / 3;
                Color color = active ? CueOn11(strip, UiKit.Gold) : LabelOn11(strip, UiKit.Muted);
                var title = UiKit.Text(strip, "StageChapter" + chapter, $"第 {chapter + 1} 章", x, 8, 165, headerH, 19.5f, color, TextAlignmentOptions.Center);
                title.fontStyle = active ? FontStyles.Bold : FontStyles.Normal;
                for (int within = 0; within < 3; within++)
                {
                    int index = chapter * 3 + within;
                    Color fill = index == stage ? UiKit.Gold : index < stage ? Color.Lerp(UiKit.Felt, UiKit.Teal, .35f)
                        : within == 2 ? Color.Lerp(UiKit.Felt, UiKit.Danger, .28f) : UiKit.Felt;
                    var tick = Surface11(strip, "StageTick" + index, x + within * 56, tickY, 48, tickH, "ui_frame_block", fill);
                    tick.GetComponent<Image>().pixelsPerUnitMultiplier = 1.6f;
                    NoWrap11(UiKit.Text(tick, "StageTickLabel" + index, index < stage ? "✓" : (index + 1).ToString(),
                        0, 0, 48, tickH, 19.5f, LabelOn11(tick), TextAlignmentOptions.Center));
                }
                if (active) UiKit.Box(strip, "StageChapterRule" + chapter, x, tickY + tickH + 3, 165, 3, color);
            }
        }

        private void StageEntryNotes16()
        {
            const float width = 352, maxHeight = 612;
            var paper = Surface11(main, "EntryNotes16", 1496, 0, 392, maxHeight, "ui_stage_card", UiKit.Paper);
            var lines = new List<string> { "开始本关后，卡牌顺序就不能再调。" };
            string Victims(IEnumerable<string> ids) => string.Join("、", ids.Select(id => CardLabel11(id)));
            void Add(string text, Color cue) => lines.Add("<color=" + Hex11(CueOn11(paper, cue)) + ">" + text + "</color>");
            var entry = Session.EntryView();
            string doomed = Victims(ControllerRules.AltarPlan(ViewState.controllers).Select(p => p.Value));
            if (doomed.Length > 0) Add("开始本关时祭坛会摧毁：" + doomed + "。把祭坛放到最右边可以避免。", UiKit.Danger);
            string disabled = Victims(entry?.disabledControllerIds ?? Array.Empty<string>());
            if (disabled.Length > 0) Add("首领减益会让这些卡牌本关失效：" + disabled + "。调整顺序也许能避开。", UiKit.Danger);
            if (entry != null && entry.scapegoat) Add("替罪羊会在本关开始时自毁，解除首领减益。", UiKit.Teal);
            var label = UiKit.Text(paper, "ControllerOrderWarning", string.Join("\n", lines), 20, 12, width, maxHeight, 19.5f, LabelOn11(paper, UiKit.Ink));
            float textH = Mathf.Ceil(label.GetPreferredValues(label.text, width, 0).y) + 2;
            float panelH = Mathf.Clamp(textH + 24, 58, maxHeight);
            paper.sizeDelta = new Vector2(392, panelH);
            paper.anchoredPosition = new Vector2(1496, panelH - 964);
            if (textH > panelH - 24)
            {
                var content = UiKit.Scroll(paper, "EntryNotesOverflow16", 20, 12, width, panelH - 24, textH);
                label.gameObject.SetActive(false);
                label = UiKit.Text(content, "ControllerOrderWarning", string.Join("\n", lines), 0, 0, width, textH, 19.5f, LabelOn11(paper, UiKit.Ink));
            }
            label.rectTransform.sizeDelta = new Vector2(width, textH);
        }

        private void StartStageButton16()
        {
            bool enabled = !IsBusy && !Session.HasPendingSave;
            var button = UiKit.ActionButton(main, "StartStage", $"开始第 {ViewState.stage.stageIndex + 1} 关", 1496, 976, 392, 96, StartPreviewStage07, UiKit.Primary, enabled);
            var label = NoWrap11(button.transform.Find("Label").GetComponent<TextMeshProUGUI>());
            label.fontSize = (Large11 ? 20 : 34) * UiKit.FontScale;
            label.fontStyle = FontStyles.Bold;
            UiKit.Role(label, TextRole.PrimaryButton);
            label.color = LabelOn11((Image)button.targetGraphic, enabled ? (Color?)null : label.color, withIcon: enabled);
            float w = Mathf.Ceil(label.GetPreferredValues(label.text).x), arrow = Large11 ? 30 : 42;
            float x = (392 - w - 10 - arrow) / 2;
            label.rectTransform.anchoredPosition = new Vector2(x, 0);
            label.rectTransform.sizeDelta = new Vector2(w + 2, 89);
            label.alignment = TextAlignmentOptions.MidlineLeft;
            var icon = UiKit.Box(button.transform, "Arrow", x + w + 10, (89 - arrow) / 2, arrow, arrow, label.color).GetComponent<Image>();
            icon.sprite = UiKit.Template("ui_icon_arrow");
            icon.preserveAspect = true;
        }

        private void DrawCycleCards10(Transform parent, float x, float y, bool interactive)
        {
            var view = Session.CycleView();
            var entry = interactive ? Session.EntryView() : null;
            for (int i = 0; i < view.Length; i++)
            {
                var v = view[i];
                bool own = interactive && v.current;
                var p = UiKit.PaperFrame(parent, "CycleCard" + i, x + i * (StageWidth16 + 16), y, StageWidth16, StageHeight16, UiKit.Paper);
                if (v.current) UiKit.Ring(p, "CurrentCycleCard", -4, -4, StageWidth16 + 5, StageHeight16 + 4, UiKit.Gold);
                var kind = v.stageKind == "boss" ? UiKit.Boss : v.stageKind == "pressure" ? UiKit.Pressure : UiKit.Teal;
                UiKit.Box(p, "KindStrip", 24, 16, StageContent16, 4, kind);
                var title = NoWrap11(UiKit.Text(p, "Title", $"第 {v.stageIndex + 1} 关", 24, 24, 280, Large11 ? 66 : 58, 30, LabelOn11(p, UiKit.Ink)));
                title.fontStyle = FontStyles.Bold;
                UiKit.Role(title, TextRole.StageNumber);
                float sealH = Large11 ? 52 : 44;
                var stamp = UiKit.Rect(p, "KindSeal", 330, 32, 116, sealH);
                if (v.stageKind != "normal") stamp.localRotation = Quaternion.Euler(0, 0, -6);
                Color cue = CueOn11(p, kind);
                UiKit.Outline(stamp, "StampBorder", 0, 0, 116, sealH, cue, 2);
                UiKit.Text(stamp, "KindTag", v.stageKind == "boss" ? "首领" : v.stageKind == "pressure" ? "高压" : "普通",
                    4, 0, 108, sealH, 19.5f, cue, TextAlignmentOptions.Center);
                UiKit.Box(p, "Rule", 24, 90, StageContent16, 2, Color.Lerp(UiKit.Ink, UiKit.Paper, .65f));
                if (v.stageKind == "pressure") PressureStage16(p, v, own);
                else if (v.stageKind == "boss") BossStage16(p, v, own, entry, !interactive);
                else NormalStage16(p, v, own);
                if (v.cleared)
                {
                    // Keep all body ink legible. Draw the cleared paper wash behind the labels and the status in the free foot.
                    var veil = UiKit.Box(p, "ClearedVeil", 6, 6, StageWidth16 - 12, StageHeight16 - 12, new Color(UiKit.Paper.r, UiKit.Paper.g, UiKit.Paper.b, .5f));
                    veil.SetAsFirstSibling();
                    var done = UiKit.Text(p, "Cleared", "✓ 已通过", 24, StageHeight16 - 58, StageContent16, 46, 19.5f, CueOn11(p, UiKit.Teal));
                    done.fontStyle = FontStyles.Bold;
                }
            }
        }

        private static void StageTarget16(RectTransform card, string target, float y, float h, float font)
        {
            var value = NoWrap11(UiKit.Text(card, "Goal", target, 24, y, StageContent16, h, font, LabelOn11(card, UiKit.Ink)));
            value.fontStyle = FontStyles.Bold;
            UiKit.Role(value, TextRole.BigNumber);
            float measured = value.GetPreferredValues(value.text).x;
            if (measured > StageContent16) value.fontSize *= StageContent16 / measured;
            UiKit.FitNumberInk21(value);
        }

        private void NormalStage16(RectTransform card, CycleStageView v, bool own)
        {
            Color ink = LabelOn11(card, UiKit.Ink);
            UiKit.Text(card, "GoalLabel", "目标分数", 24, 100, StageContent16, Large11 ? 42 : 30, 19.5f, ink);
            StageTarget16(card, CompactScore(tutorial && own ? "50" : v.normalTarget), Large11 ? 146 : 130, Large11 ? 98 : 82, 44);
            UiKit.Text(card, "Reward", $"过关奖励 {v.normalReward} 金币", 24, Large11 ? 248 : 216, StageContent16, Large11 ? 54 : 40, 23, ink);
            float y = Large11 ? 316 : 268;
            if (!own)
            {
                UiKit.Text(card, "NormalHint", "没有额外条件。", 24, y, StageContent16, 60, 19.5f, LabelOn11(card, UiKit.Muted));
                return;
            }
            var selected = UiKit.FlatButton(card, "Normal", "✓ 已选择", 24, y, StageContent16, 60, () => ChoosePreview07(false), true, true);
            var face = (Image)selected.targetGraphic;
            face.color = Color.Lerp(UiKit.Paper, UiKit.Gold, .45f);
            selected.transform.Find("Label").GetComponent<TextMeshProUGUI>().color = LabelOn11(face);
        }

        private void PressureStage16(RectTransform card, CycleStageView v, bool own)
        {
            bool risky = own ? PreviewRisk07 : v.risk;
            float y = 100;
            y += PressureOption16(card, "Normal", "普通挑战", v.normalTarget, v.normalReward, y, own, !risky, false) + 8;
            y += PressureOption16(card, "Risk", "高压挑战", v.riskTarget, v.riskReward, y, own, risky, true) + 8;
            string ratio = PlayerNumberFormat.Ratio(ExactScoreMath.Parse(v.riskTarget), ExactScoreMath.Parse(v.normalTarget));
            string hint = v.cleared ? v.risk ? "已完成高压挑战。" : "已按普通挑战通过。"
                : (v.current ? "" : "到这一关时再选择。") + $"高压挑战：目标 ×{ratio}，过关奖励 {v.riskReward} 金币（普通 {v.normalReward}）。";
            StageParagraph16(card, "PressureHint", hint, y, 19.5f, LabelOn11(card, UiKit.Muted));
        }

        private float PressureOption16(RectTransform card, string id, string title, string target, int reward, float y, bool interactive, bool selected, bool risk)
        {
            const float inset = 14, w = StageContent16, body = 19.5f;
            string left = "目标 " + CompactScore(target), right = $"奖励 {reward} 金币", info = left + " · " + right;
            bool split = PreferredWidth11(info, body) > w - 2 * inset;
            float h = split ? 142 : Large11 ? 99 : 86, titleH = Large11 ? 47 : 40;
            Color fill = selected ? Color.Lerp(UiKit.Paper, UiKit.Gold, .45f) : Color.Lerp(UiKit.Paper, UiKit.Ink, .12f);
            RectTransform row;
            Image face;
            if (interactive)
            {
                var button = UiKit.FlatButton(card, id, selected ? "✓ 已选择" : "选择挑战", 24, y, w, h, () => ChoosePreview07(risk), true, selected);
                row = (RectTransform)button.transform;
                face = (Image)button.targetGraphic;
                face.sprite = UiKit.Template("ui_frame_block");
                face.color = fill;
                var state = NoWrap11(row.Find("Label").GetComponent<TextMeshProUGUI>());
                state.fontSize = body * UiKit.FontScale;
                state.color = LabelOn11(face);
                state.alignment = TextAlignmentOptions.MidlineRight;
                state.rectTransform.anchoredPosition = new Vector2(w - 190 - inset, -3);
                state.rectTransform.sizeDelta = new Vector2(190, titleH);
            }
            else
            {
                row = Surface11(card, id + "Row", 24, y, w, h, "ui_frame_block", fill);
                face = row.GetComponent<Image>();
            }
            Color ink = LabelOn11(face);
            var heading = NoWrap11(UiKit.Text(row, "OptionTitle", title, inset, 3, 194, titleH, 22, ink, TextAlignmentOptions.MidlineLeft));
            heading.fontStyle = FontStyles.Bold;
            UiKit.Text(row, "OptionInfo", split ? left + "\n" + right : info, inset, titleH + 3, w - 2 * inset, h - titleH - 5, body, ink, TextAlignmentOptions.MidlineLeft);
            if (risk) UiKit.Box(row, "RiskMark", w - 6, 10, 4, h - 20, UiKit.Pressure);
            return h;
        }

        private static float StageParagraph16(RectTransform card, string name, string value, float y, float size, Color color)
        {
            var text = UiKit.Text(card, name, value, 24, y, StageContent16, 1, size, color);
            text.text = WrapParagraph21(text,value,StageContent16);
            float h = Mathf.Ceil(text.GetPreferredValues(text.text, StageContent16, 0).y) + 3;
            text.rectTransform.sizeDelta = new Vector2(StageContent16, h);
            return y + h;
        }

        private void BossStage16(RectTransform p, CycleStageView v, bool own, StageEntryView entry, bool inDialog)
        {
            bool goat = own && entry != null && entry.scapegoat, lifted = v.modifierNullified || goat;
            string target = BossTarget10(v, lifted);
            Color ink = LabelOn11(p, UiKit.Ink), muted = LabelOn11(p, UiKit.Muted);
            float y = StageParagraph16(p, "GoalLabel", target == v.normalTarget ? "目标分数" : "目标分数（已含首领减益）", 100, 19.5f, muted);
            float goalH = Line11(36);
            StageTarget16(p, CompactScore(target), y, goalH, 36);
            y += goalH + 2;
            string prefix = lifted ? "首领减益（已解除）· " : "首领减益 · ";
            var tag = NoWrap11(UiKit.Text(p, "ModifierLabel", prefix, 24, y, StageContent16, 50, 19.5f, muted, TextAlignmentOptions.MidlineLeft));
            var name = NoWrap11(UiKit.Text(p, "ModifierName", NameOf(v.modifierId), 24, y, StageContent16, 50, 24, lifted ? muted : CueOn11(p, UiKit.Boss), TextAlignmentOptions.MidlineLeft));
            name.fontStyle = FontStyles.Bold;
            UiKit.Role(name, TextRole.BossName);
            float prefixW = Mathf.Ceil(tag.GetPreferredValues(prefix).x), nameW = Mathf.Ceil(name.GetPreferredValues(name.text).x);
            if (prefixW + nameW > StageContent16)
            {
                float factor = StageContent16 / (prefixW + nameW + 2);
                tag.fontSize *= factor;
                name.fontSize *= factor;
                prefixW = Mathf.Ceil(tag.GetPreferredValues(prefix).x);
            }
            float lineH = Mathf.Max(tag.GetPreferredValues(prefix).y, name.GetPreferredValues(name.text).y) + 4;
            tag.rectTransform.sizeDelta = new Vector2(prefixW, lineH);
            name.rectTransform.anchoredPosition = new Vector2(24 + prefixW, -y);
            name.rectTransform.sizeDelta = new Vector2(StageContent16 - prefixW, lineH);
            y += lineH;
            float top = y;
            y = StageParagraph16(p, "ModifierText", ShortText11(v.modifierId), y, 19.5f, ink);
            StageTooltip16(p, "Modifier", v.modifierId, top, y - top, inDialog);
            if (goat) y = StageParagraph16(p, "ModifierLift", "替罪羊会在本关开始时解除这个减益", y + 2, 19.5f, CueOn11(p, UiKit.Teal));
            UiKit.Box(p, "LootRule", 24, y + 4, StageContent16, 1, Color.Lerp(UiKit.Ink, UiKit.Paper, .65f));
            top = y + 10;
            string loot = string.IsNullOrEmpty(v.lootKind) ? "终局首领 · 通关即胜利" : "战利品 · " + NameOf(v.lootKind) + "：" + ShortText11(v.lootKind);
            y = StageParagraph16(p, "Loot", loot, top, 19.5f, ink);
            if (!string.IsNullOrEmpty(v.lootKind)) StageTooltip16(p, "Loot", v.lootKind, top, y - top, inDialog);
        }

        private void StageTooltip16(RectTransform p, string role, string id, float y, float h, bool inDialog)
        {
            // Stage details are hover-only. A plain raycast surface keeps the text out of button navigation.
            var hit = UiKit.Box(p, role + "Details16", 24, y, StageContent16, h, Color.clear, true);
            Art07AttachTooltip(hit, "stage-" + id, NameOf(id), Details11(id), ShortText11(id), null, "", inDialog);
        }

        private void ShowCycle10()
        {
            if (IsBusy) return;
            var view = Session.CycleView();
            if (view.Length == 0) return;
            var p = Art07Modal("关卡情报 · 第 " + (view[0].stageIndex / 3 + 1) + " 章", 150, Large11 ? 58 : 170, 1620, Large11 ? 964 : 740);
            DrawCycleCards10(p, 86, 96, false);
            var names = DisabledNextIds10().Select(id => CardLabel11(id)).ToArray();
            string hint = names.Length == 0 ? "每章的首领减益在本章开始时就定好了。现在可以调整构筑和卡牌顺序，或准备“破咒符”“替罪羊”来解除它。"
                : "按现在的顺序，下一关首领会让这些卡牌失效：" + string.Join("、", names) + "。关掉这个窗口后，在“我的构筑”里拖动调整顺序。";
            UiKit.Text(p, "CycleHint", hint, 86, Large11 ? 700 : 560, 1448, Large11 ? 96 : 64, 20, names.Length > 0 ? CueOn11(p, UiKit.Danger) : LabelOn11(p, UiKit.Muted));
            var close = UiKit.ActionButton(p, "CloseCycle", "知道了", 660, Large11 ? 812 : 644, 300, 64, CloseModal, UiKit.Paper);
            close.transform.Find("Label").GetComponent<TextMeshProUGUI>().color = LabelOn11((Image)close.targetGraphic);
        }


        // ---------------------------------------------------------------- texts for new content
        private string ItemSummary10(string itemId,RunState before,RunState after)
        {
            switch(itemId)
            {
                case "I08":return "首领减益已解除";
                case "I09":return "下一次提交得分 ×2";
                case "I10":return "下一次提交全部算蓝色（骰面不变）";
                case "I11":return "第 1 颗锁定骰子朝上的面变成黄金面";
                case "I12":return "本关提交栏 +"+(after.stage.capacity-before.stage.capacity)+" 格";
                default:return "";
            }
        }
        private string ItemUnavailable10(OwnedItem item)
        {
            var s=ViewState;var h=s.hand;bool locked=h.hasRolled&&h.orderedLockedIds.Count>0;
            if(item.contentId!="I08"&&ControllerRules.ModifierHas(Config,Session.CurrentModifierId,"NoItems"))return "首领减益“徒手”：本关不能使用道具。";
            switch(item.contentId)
            {
                case "I08":return Session.CurrentModifierId==null?"只能在首领关、减益仍生效时使用。":s.phase!="HandReady"||s.stage.submissionOrdinal!=0||h.rollsUsed!=0?"本关第一次掷骰前才能用。":null;
                case "I09":return h.doubleScore?"本手已经生效。":null;
                case "I10":return !locked?"需要先掷骰并锁定至少 1 颗骰子。":h.blueWave?"本手已经生效。":h.redWave?"本手已经用了红色浪潮。":null;
                case "I11":
                    if(!locked)return "需要先掷骰并锁定至少 1 颗骰子。";
                    var first=h.currentResults.First(r=>r.dieId==h.orderedLockedIds[0]);
                    return s.dice.First(d=>d.dieId==first.dieId).faces.First(f=>f.faceId==first.currentFaceId).isGold?"第 1 颗锁定骰子朝上的面已经是黄金面。":null;
                case "I12":return s.stage.capacity>=(s.isDebug?Config.debug.maxSubmitCapacity:Config.maxSubmitCapacity)?$"提交栏已经是最多的 {(s.isDebug?Config.debug.maxSubmitCapacity:Config.maxSubmitCapacity)} 格。":null;
                default:return null;
            }
        }
        /// <summary>Effect records added by p1.0.0. Returns null for anything older.</summary>
        private string EffectText10(EffectRecord e)
        {
            switch(e.kind)
            {
                case "ModifierNullified":return NameOf(e.sourceId)+"：首领减益已解除";
                case "ControllerDisabled":return "首领减益："+NameOfInstance10(e.detail)+" 本关失效";
                case "GoldLost":return NameOf(e.sourceId)+"：金币 "+e.oldValue+" → "+e.newValue;
                case "GoldTaxed":return NameOf(e.sourceId)+"：金币 "+e.oldValue+" → "+e.newValue;
                case "GoldSpent":return "一掷千金：花费 "+(e.oldValue-e.newValue)+" 金币";
                case "HandDowngraded":return NameOf(e.sourceId)+"："+NameOf(e.detail)+" "+e.oldValue+" → "+e.newValue+"级";
                // v1.1 (W09): dice and faces by name ("骰子 1 的第 2 面"), never by D001 / F02.
                case "FaceChanged":
                    {
                        string what=(e.detail??"").Split(':')[0],where=DieFaceName11(e.dieId,e.faceId);
                        string change=what=="pipDelta"||what=="pipSet"?"点数 "+e.oldValue+" → "+e.newValue:what=="color"?(e.newValue==1?"染红":"染蓝")+(e.oldValue==e.newValue?"（已是该色）":""):what=="gold"?(e.newValue==1?"镀金":"去掉黄金")+(e.oldValue==e.newValue?"（无变化）":""):what=="texture"?"纹理变为"+((e.detail??"").EndsWith("smooth")?"光滑":(e.detail??"").EndsWith("rough")?"粗糙":"无")+(e.oldValue==e.newValue?"（无变化）":""):"权重 "+e.oldValue+" → "+e.newValue;
                        return NameOf(e.sourceId)+"："+where+" "+change;
                    }
                case "FaceMirrored":return "镜像："+DieFaceName11(e.dieId,e.faceId)+" 变为 "+e.newValue+" 点"+(e.detail=="red"?"红":"蓝");
                case "DieGlassed":return "吹玻璃："+DieName11(e.dieId)+" 变为玻璃骰";
                case "DieAdded":return (string.IsNullOrEmpty(e.sourceId)?"":NameOf(e.sourceId)+"：")+"获得"+DieName11(e.dieId);
                case "ItemGranted":return "百宝袋：获得 "+NameOf((e.detail??"").Split(':').LastOrDefault());
                case "ControllerGranted":return "滥竽充数：获得 "+NameOf((e.detail??"").Split(':').LastOrDefault());
                case "ControllerExpired":return "昙花一现：倍率耗尽，已消失";
                case "RandomUpgrade":return "无心插柳："+DieFaceName11(e.dieId,e.faceId)+" 获得 "+NameOf(e.detail);
                case "RandomUpgradeMissed":return "无心插柳："+DieFaceName11(e.dieId,e.faceId)+" 的 "+NameOf(e.detail)+" 落空";
                case "LootGold":return "战利品折现：+"+e.newValue+" 金币";
                case "BossRerolled":return "贿赂首领：第 "+e.newValue+" 章的减益改为 "+NameOf((e.detail??"").Split(':').LastOrDefault());
                case "InterestCapRaised":
                    return long.TryParse(e.detail,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out long maximum)
                        ? "理财顾问：利息上限提高到每关 +"+PlayerNumberFormat.Gold(maximum)
                        : NameOf("O10")+"："+Config.Service("O10").details;
                default:return null;
            }
        }
        // v1.1 (W09): a card that is no longer owned is described, never shown by its instance id.
        private string NameOfInstance10(string instanceId)=>CardLabel11(instanceId);
        /// <summary>Handbook rows for the p1.0.0 mechanics, shown under the growth page.</summary>
        private string[][] HandbookRules10()
        {
            var rows=new List<string[]>
            {
                new[]{"每章三关",$"普通关、高压关、首领关各一。高压关开始前可以选高压挑战：目标 ×1.5，过关奖励 {Config.stages.First(stage=>stage.riskEnabled).riskReward} 金币（普通 {Config.stages.First(stage=>stage.riskEnabled).normalReward}）。首领关带一个减益，通关后先选战利品，再进商店。"},
                new[]{"首领减益","减益在每章开始时就定好，选关页和商店的“查看本章”都能看到。从本关开始到结算为止生效。替罪羊、破咒符可以解除它，贿赂首领可以重抽。"},
                new[]{"封印","被封印的骰子计 0 分，但仍算牌型、颜色、红组和位置。"},
                new[]{"失效的卡牌","首领减益可以让一些卡牌本关失效：没有技能、附魔不触发，也不能被百变模仿复制；仍然占栏位，仍然计入控制欲和草根逆袭的张数。"},
                new[]{"战利品与赠礼",$"首领关通关后先选战利品：按种类可以拿 1 到 3 件，也可以整组折现 {Config.lootGoldValue} 金币。杂货郎、铁匠铺每家商店各送一次三选一。"},
                new[]{"骰面权重","每个骰面有权重，权重越高越容易掷出，上限 5。灌铅和配重可以提高权重。"},
                new[]{"骰面养成","点金手、表里同色、潜移默化、见五成群、二三其德、灌铅、抛光匠、养精蓄锐、移花接木在提交计分后永久改写骰面；拔苗助长、大事化小在每关结算时改写。"},
            };
            if(ViewState?.cycles!=null&&ViewState.stage!=null)
            {
                int cycle=Math.Min(ViewState.cycles.Count-1,(ViewState.stage.stageIndex+(ViewState.stage.settled?1:0))/3);
                if(cycle>=0){var plan=ViewState.cycles[cycle];rows.Add(new[]{"本章首领 · "+NameOf(plan.modifierId),ShortText11(plan.modifierId)+"　"+(string.IsNullOrEmpty(plan.lootKind)?"终局首领 · 通关即胜利":"战利品："+NameOf(plan.lootKind)+"："+ShortText11(plan.lootKind))});}
            }
            return rows.ToArray();
        }
    }
}
