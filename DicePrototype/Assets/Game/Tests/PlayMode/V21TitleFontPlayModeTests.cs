using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace DiceCraft.Tests
{
    public sealed partial class GameplayPlayModeTests
    {
        private const TextRole GoldenRoles21 = TextRole.ScreenTitle | TextRole.StageNumber | TextRole.BigNumber
            | TextRole.BossName | TextRole.PrimaryButton;

        [UnityTest]
        public IEnumerator PLAY_V21_FW16_SancaiGoldenTitleFont() => TitleMeasurements21("sancai");
        [UnityTest]
        public IEnumerator PLAY_V21_FW16_ComicTitleFont() => TitleMeasurements21("comic");
        [UnityTest]
        public IEnumerator PLAY_V21_FW16_DecoTitleFont() => TitleMeasurements21("deco");
        [UnityTest]
        public IEnumerator PLAY_V21_FW16_GlassTitleFont() => TitleMeasurements21("glass");

        [UnityTest]
        public IEnumerator PLAY_V21_FW18_DefaultBaseFont() => TitleMeasurements21("default");
        [UnityTest]
        public IEnumerator PLAY_V21_FW18_SwissBaseFont() => TitleMeasurements21("swiss");

        private static TextRole ExpectedRoles21(string theme)
        {
            if (theme == "default" || theme == "swiss") return TextRole.None;
            var roles = GoldenRoles21;
            if (theme == "comic" || theme == "glass") roles |= TextRole.SectionTitle;
            if (theme == "glass") roles |= TextRole.CardName;
            return roles;
        }

        private IEnumerator TitleMeasurements21(string theme)
        {
            Assert.That((Screen.width == 1920 && Screen.height == 1080) ||
                        (Screen.width == 1280 && Screen.height == 720), Is.True, "Use an actual supported window.");
            string output = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "artifacts", "cache",
                "v11", "w21", theme == "sancai" ? "golden" : Path.Combine("promotion", theme), Screen.width + "x" + Screen.height));
            Directory.CreateDirectory(output);
            var report = new List<string> { "Unity " + Application.unityVersion + " | " + Screen.width + "x" +
                Screen.height + " | source " + IdentityHead20X(), "FW-03..08,16 " + theme + ". " +
                "Frames and contrast checked on named labels. Disabled labels are exempt from contrast only. " +
                "FW-06 compares rendered Han ink in the same stage tag at the same size." };
            var failures = new List<string>();
            bool priorClean = GameApp.CleanCapture11;
            try
            {
                GameApp.CleanCapture11 = true;
                PlayerPrefs.SetInt("reduced", 1);
                foreach (int percent in new[] {100, 150})
                {
                    W11Apply(IdentityWinningHand20X(0));
                    yield return GoldenStyle21(theme, percent);
                    yield return GoldenScreen21("normal-goal", percent, output, report, failures, true,
                        (Label21("StageGoal11"), TextRole.BigNumber));

                    W11Apply(W11Scene(new[]{"C39","C139","C34","C56","C08"},new[]{1,2,3,4,5},"bbrbb",
                        0,false,stageIndex:8,modifier:"B02",target:"13500",score:"0",items:2));
                    yield return W11Settle();
                    yield return GoldenScreen21("play", percent, output, report, failures, true,
                        (Label21("StageLabel11"), TextRole.StageNumber), (Label21("BossModifierName"), TextRole.BossName),
                        (Button("Roll").GetComponentInChildren<TextMeshProUGUI>(), TextRole.PrimaryButton),
                        (Label21("StageCardTitle11"), TextRole.SectionTitle), (Label21("BossModifierText"), TextRole.None));

                    yield return W14Load(W14ShopFixture(false));
                    yield return GoldenStyle21(theme, percent);
                    var firstOffer = app.Session.State.shop.offers.First(o => o.category == "controller");
                    var card = W14Main.Find("Offer_" + firstOffer.offerId);
                    Assert.That(card, Is.Not.Null, "A real shelf offer must be drawn on the shop canvas.");
                    var cardName = Label21("Name", card);
                    cardName.ForceMeshUpdate();
                    Assert.That(cardName.GetParsedText(), Is.EqualTo(app.Config.Controller(firstOffer.contentId).displayName));
                    Assert.That(cardName.textInfo.characterInfo.Take(cardName.textInfo.characterCount).Any(c => c.isVisible), Is.True);
                    yield return GoldenScreen21("shop", percent, output, report, failures, true,
                        (Label21("ShopTitle"), TextRole.ScreenTitle), (Label21("ControllerShelfTitle"), TextRole.SectionTitle),
                        (cardName, TextRole.CardName),
                        (Button("LeaveShop").GetComponentInChildren<TextMeshProUGUI>(), TextRole.PrimaryButton));

                    yield return IdentityEndStage20X(3);
                    yield return Art073Settle(.45f);
                    Assert.That(app.ViewState.stage.stageKind, Is.EqualTo("normal"));
                    var reward = app.ViewState.lastStageSettlement;
                    var total = Label21("SettlementNetCash");
                    total.ForceMeshUpdate();
                    Assert.That(total.GetParsedText(), Is.EqualTo("+" + PlayerNumberFormat.Gold(reward.goldAfter-reward.goldBefore)));
                    yield return GoldenScreen21("settlement", percent, output, report, failures, true,
                        (Label21("Cleared"), TextRole.ScreenTitle), (total, TextRole.BigNumber),
                        (Label21("SettleGold15"), TextRole.None),
                        (Button("SettlementContinue").GetComponentInChildren<TextMeshProUGUI>(), TextRole.PrimaryButton));
                    byte[] settlementSave = File.ReadAllBytes(app.SaveLocation);
                    Click("SettlementDetailsToggle15");
                    yield return GoldenScreen21("settlement-details", percent, output, report, failures, true,
                        (Label21("Cleared"), TextRole.ScreenTitle),
                        (Label21("SettlementBaseRewardDetail"), TextRole.None),
                        (Label21("SettlementBaseRewardDetailAmount"), TextRole.None),
                        (Button("SettlementDetailsToggle15").GetComponentInChildren<TextMeshProUGUI>(), TextRole.None),
                        (Button("SettlementContinue").GetComponentInChildren<TextMeshProUGUI>(), TextRole.PrimaryButton));
                    Click("SettlementDetailsToggle15");
                    CollectionAssert.AreEqual(settlementSave, File.ReadAllBytes(app.SaveLocation));

                    var pick = ShopBoundaryFixture();
                    pick.shop.offers.Clear();
                    AddBoundaryOffer(pick, "pick-gold", "die_upgrade", "U06");
                    yield return W14Load(pick);
                    yield return GoldenStyle21(theme, percent);
                    string die = app.Session.State.dice.OrderBy(d => d.createdSerial).First().dieId;
                    Click("Buy_pick-gold"); Click("Target_" + die); Click("Face_F02");
                    yield return Art073Settle(.35f);
                    Assert.That(app.FacePickStep14, Is.EqualTo("S3"));
                    // S3 dims the shop behind its veil. The undimmed shop title is sampled in "shop";
                    // here the visible title, chosen die and face, and ways to return or confirm belong to FacePick14.
                    var foreground = W14Main.Find("FacePick14");
                    Assert.That(foreground, Is.Not.Null);
                    yield return GoldenScreen21("facepick-S3", percent, output, report, failures, true,
                        (Label21("FacePickTitle14", foreground), TextRole.SectionTitle),
                        (Label21("FacePickDieLabel14", foreground), TextRole.None),
                        (Label21("PurchasePreviewBody", foreground), TextRole.None),
                        (foreground.Find("ChangeFace14/Label").GetComponent<TextMeshProUGUI>(), TextRole.None),
                        (foreground.Find("CancelFacePick14/Label").GetComponent<TextMeshProUGUI>(), TextRole.None),
                        (foreground.Find("ConfirmPurchase/Label").GetComponent<TextMeshProUGUI>(), TextRole.None));
                }
            }
            finally
            {
                report.AddRange(failures.Select(f => "FAIL " + f));
                File.WriteAllLines(Path.Combine(output, "font-measurements.txt"), report);
                GameApp.CleanCapture11 = priorClean;
                if (app != null) app.ApplyTheme11(GameTheme.DefaultId, false);
            }
            Assert.That(failures, Is.Empty, string.Join("\n", failures));
        }

        private IEnumerator GoldenStyle21(string theme, int percent)
        {
            app.ApplyTheme11(theme);
            Assert.That(app.ThemeId11, Is.EqualTo(theme));
            Assert.That(UiKit.TitleFont != null, Is.EqualTo(ExpectedRoles21(theme) != TextRole.None),
                theme + ": the player must resolve exactly the approved title configuration.");
            Assert.That(ThemeRegistry.Current.TitleRoles, Is.EqualTo(ExpectedRoles21(theme)));
            W11TextScale(percent);
            yield return W11Settle();
        }

        private TextMeshProUGUI Label21(string name, Transform under = null)
        {
            var labels = (under == null ? app.canvas.transform : under).GetComponentsInChildren<TextMeshProUGUI>(false);
            var hit = labels.FirstOrDefault(t => t.enabled && t.name == name);
            Assert.That(hit, Is.Not.Null, "Expected live label " + name);
            return hit;
        }

        // FW-04 expectations are keyed by the player-facing widget. Reading its current font to infer
        // its role would let a forgotten Role() call pass. A saved menu and an optional reroll are secondary.
        private TextRole ForegroundRole21(TextMeshProUGUI label)
        {
            var parent = label.transform.parent;
            string p = parent.name;
            switch (label.name)
            {
                case "DialogTitle": case "ShopTitle": case "EndTitle": return TextRole.ScreenTitle;
                case "Cleared" when p == "Rewards": return TextRole.ScreenTitle;
                case "StageCardTitle11": case "ControllerShelfTitle": case "ProductsTitle14":
                case "DiceCounterTitle": case "OwnedDiceTitle14": case "FacePickTitle14":
                case "LastDiceTitle": return TextRole.SectionTitle;
                case "Heading" when p == "EndBuild": return TextRole.SectionTitle;
                case "StageLabel11": return TextRole.StageNumber;
                case "Title" when p.StartsWith("CycleCard", StringComparison.Ordinal): return TextRole.StageNumber;
                case "StageGoal11": case "SettlementNetCash": return TextRole.BigNumber;
                case "Goal" when p.StartsWith("CycleCard", StringComparison.Ordinal): return TextRole.BigNumber;
                case "Value" when p == "EndTotalScore" || p == "EndMaximumScore" || p == "EndMaximumMultiplier"
                    || (p == "EndDuration" && label.text.Any(char.IsDigit)): return TextRole.BigNumber;
                case "BossModifierName" when p == "BossModifier10": return TextRole.BossName;
                case "ModifierName" when p.StartsWith("CycleCard", StringComparison.Ordinal): return TextRole.BossName;
                case "BossName15": case "LootName15": return TextRole.BossName;
                case "Name":
                    // Tall cards print the name centred above their independent body. Tiles have no body; compact names sit beside their picture.
                    var body = parent.Find("ShortText11");
                    if (parent.GetComponent<Button>() != null && body != null && body.gameObject.activeInHierarchy &&
                        label.alignment == TextAlignmentOptions.Center)
                        return TextRole.CardName;
                    break;
                case "Label":
                    switch (p)
                    {
                        case "StartNew": case "ContinueRun": case "StartStage": case "Resume": case "RetrySave":
                        case "ConfirmDisplay": case "ApplyDisplay": case "Again": case "LeaveShop":
                        case "SettlementContinue": return TextRole.PrimaryButton;
                        case "LootEnterShop15" when label.text == "进入商店": return TextRole.PrimaryButton;
                        case "NewRun" when app.ViewState == null: return TextRole.PrimaryButton;
                        case "Roll": case "Submit": case "Confirm":
                            if (parent.Find("Step")?.gameObject.activeSelf == true) return TextRole.PrimaryButton;
                            break;
                    }
                    break;
            }
            return label.name.StartsWith("GiftTitle_", StringComparison.Ordinal) && p == "LootChoices15"
                ? TextRole.SectionTitle : TextRole.None;
        }

        private static bool BaseRoleBold21(TextMeshProUGUI label, TextRole role)
        {
            if (role == TextRole.PrimaryButton)
                return new[]{"StartStage","LeaveShop","LootEnterShop15","SettlementContinue"}.Contains(label.transform.parent.name);
            if (label.name == "DialogTitle") return label.text == "新的一局";
            return label.name != "StageCardTitle11";
        }

        private void CheckForegroundFont21(TextMeshProUGUI label, List<TMP_CharacterInfo> printed,
            string scope, List<string> failures)
        {
            if (printed.Count == 0) return;
            var role = ForegroundRole21(label);
            bool title = (ExpectedRoles21(app.ThemeId11) & role) != 0;
            bool score = label.name == "FinalScore" || label.name == "StableResultScore07" || label.name == "RecoveredScore07";
            var expected = title ? UiKit.TitleFont : UiKit.Font;
            if (score && app.art07NumberFont != null && label.font == app.art07NumberFont) expected = app.art07NumberFont;
            string path = scope + " " + W11bPath(label.transform);
            if (expected == null || label.font != expected ||
                (title && (label.fontStyle != FontStyles.Normal || label.fontWeight != FontWeight.Regular)) ||
                (!title && role != TextRole.None && label.fontStyle != (BaseRoleBold21(label,role) ? FontStyles.Bold : FontStyles.Normal)))
            {
                failures.Add(path + " FW-04 role " + ForegroundRole21(label) + " font=" + label.font?.name);
                return;
            }
            foreach (var c in printed)
            {
                if (c.elementType == TMP_TextElementType.Sprite)
                {
                    // TMP maps an explicit <sprite> tag to a sprite character, not to the source '<'.
                    if (label.richText && c.index >= 0 && c.index + 7 < label.text.Length &&
                        string.Compare(label.text, c.index, "<sprite", 0, 7, StringComparison.OrdinalIgnoreCase) == 0 &&
                        c.textElement is TMP_SpriteCharacter sprite && sprite.textAsset is TMP_SpriteAsset atlas &&
                        atlas.spriteCharacterTable.Contains(sprite)) continue;
                    failures.Add(path + " FW-03 unexpected sprite at source index " + c.index);
                    continue;
                }
                // TMP overwrites c.character with its missing-glyph replacement, but c.index still refers
                // to the original UTF-16 string, including any <size>, <b> or <mspace> tags before the glyph.
                if (c.index < 0 || c.index >= label.text.Length) { failures.Add(path + " FW-03 missing source index"); continue; }
                uint scalar = (uint)char.ConvertToUtf32(label.text, c.index);
                // This score-summary role intentionally allows its last rendered glyph to be an ellipsis.
                if (label.overflowMode == TextOverflowModes.Ellipsis && label.isTextTruncated &&
                    c.index == printed[printed.Count-1].index && c.textElement?.unicode == 0x2026) scalar = 0x2026;
                var weight = (c.style & FontStyles.Bold) != 0 ? FontWeight.Bold : label.fontWeight;
                var character = TMP_FontAssetUtilities.GetCharacterFromFontAsset(scalar, expected, true, c.style, weight, out _);
                // TMP_Text.GetTextElement retries the regular face for synthetic bold when the
                // physical bold face and its styled fallbacks do not contain this character.
                if (character == null && (c.style != FontStyles.Normal || weight != FontWeight.Regular))
                    character = TMP_FontAssetUtilities.GetCharacterFromFontAsset(scalar, expected, true, FontStyles.Normal, FontWeight.Regular, out _);
                if (c.textElement == null || c.textElement.unicode != scalar || character == null ||
                    c.fontAsset != character.textAsset || c.textElement.textAsset != c.fontAsset ||
                    (title && c.fontAsset != UiKit.TitleFont && !UiKit.TitleFont.fallbackFontAssetTable.Contains(c.fontAsset)))
                    failures.Add(path + " FW-03 original U+" + scalar.ToString("X4") + " drawn U+" +
                        (c.textElement?.unicode.ToString("X4") ?? "none") + " via " + c.fontAsset?.name);
            }
        }

        private void CheckVisibleLabel21(TextMeshProUGUI label,string scope,List<string> failures)
        {
            if (!label.isActiveAndEnabled || label.color.a * label.canvasRenderer.GetInheritedAlpha() <= .05f) return;
            label.ForceMeshUpdate();
            var clip=new Rect(0,0,Screen.width,Screen.height);
            foreach(var scroll in label.GetComponentsInParent<ScrollRect>(false))
            {
                if (scroll.viewport == null || !label.transform.IsChildOf(scroll.content)) continue;
                var vp=V06ScreenRect(scroll.viewport);
                clip=Rect.MinMaxRect(Mathf.Max(clip.xMin,vp.xMin),Mathf.Max(clip.yMin,vp.yMin),
                    Mathf.Min(clip.xMax,vp.xMax),Mathf.Min(clip.yMax,vp.yMax));
            }
            var printed = new List<TMP_CharacterInfo>();
            foreach(var c in label.textInfo.characterInfo.Take(label.textInfo.characterCount))
                if (c.isVisible && c.color.a>0 && PrintedCharacter24(c.character) && clip.Overlaps(GlyphArea24(label.transform,c)))
                    printed.Add(c);
            CheckForegroundFont21(label, printed, scope, failures);
        }

        private void CheckForegroundFonts21(Transform root, string scope, List<string> failures)
        {
            foreach (var label in root.GetComponentsInChildren<TextMeshProUGUI>(false))
                CheckVisibleLabel21(label,scope,failures);
        }

        [UnityTest] public IEnumerator PLAY_V21_FW03_ForegroundRejectsAVisibleReplacementAtItsRichTextSourceIndex()
        {
            var probe = UiKit.Text(app.canvas.transform, "FwGlyphProbe21", "<b>金币</b> <mspace=0.6em>12</mspace> →", 0, 0, 480, 80);
            try
            {
                yield return null;
                var failures = new List<string>();
                CheckForegroundFonts21(probe.transform, "valid", failures);
                Assert.That(failures, Is.Empty, string.Join("\n",failures));
                probe.text = "<b>金币</b> <mspace=0.6em>12</mspace> " + char.ConvertFromUtf32(0x0378);
                probe.ForceMeshUpdate();
                var missing = probe.textInfo.characterInfo.Take(probe.textInfo.characterCount)
                    .Single(c => c.index == probe.text.Length - 1);
                Assert.That(missing.isVisible, Is.True, "The fallback square must actually draw in this project.");
                Assert.That(missing.textElement.unicode, Is.Not.EqualTo(0x378u));
                CheckForegroundFonts21(probe.transform, "missing", failures);
                Assert.That(failures.Count, Is.EqualTo(1));
                Assert.That(failures[0], Does.Contain("FW-03 original U+0378"));
            }
            finally { UnityEngine.Object.Destroy(probe.gameObject); }
        }

        private IEnumerator GoldenScreen21(string screen, int percent, string output, List<string> report,
            List<string> failures, bool saveShot, params (TextMeshProUGUI label, TextRole role)[] samples)
        {
            string scope = screen + "-" + percent;
            Canvas.ForceUpdateCanvases();
            var order = app.canvas.GetComponentsInChildren<Graphic>(false);
            if (screen == "normal-goal" || screen == "play") CheckForegroundFonts21(W14Main, scope, failures);
            var inkRects = new Dictionary<TextMeshProUGUI, List<Rect>>();
            foreach (var (label, role) in samples)
            {
                bool themed = (ExpectedRoles21(app.ThemeId11) & role) != 0;
                if (label.font != (themed ? UiKit.TitleFont : UiKit.Font) ||
                    (themed && (label.fontStyle != FontStyles.Normal || label.fontWeight != FontWeight.Regular)))
                    failures.Add(scope + " FW-04 " + W11bPath(label.transform) + ": " + label.font.name + " " + label.fontStyle);
                label.ForceMeshUpdate();
                if (role != TextRole.None || screen == "facepick-S3" || screen.StartsWith("settlement",StringComparison.Ordinal))
                {
                    inkRects[label] = W13GlyphCheck(label, scope, order, failures);
                    if (inkRects[label].Count == 0) failures.Add(scope + ": no rendered glyphs for " + label.name);
                    float designSize = label.textInfo.characterInfo.Take(label.textInfo.characterCount)
                        .Where(c => c.isVisible).Select(c => c.pointSize).DefaultIfEmpty(0f).Min();
                    if (designSize < 19.5f || (Screen.height == 720 && designSize * app.canvas.scaleFactor < 12.99f))
                        failures.Add(scope + " FW-08 " + label.name + " size " + designSize);
                    report.Add(scope + " " + W11bPath(label.transform) + " " + role + " " + label.font.name +
                        " | " + designSize + " design, " + (designSize * app.canvas.scaleFactor).ToString("F2") + " px");
                }
                if (!themed) continue;
                for (int i = 0; i < label.textInfo.characterCount; i++)
                {
                    var c = label.textInfo.characterInfo[i];
                    if (char.IsWhiteSpace(c.character)) continue;
                    uint source = label.text[c.index];
                    var chain = UiKit.TitleFont.fallbackFontAssetTable;
                    if (c.textElement == null || c.textElement.unicode != source ||
                        (c.fontAsset != UiKit.TitleFont && !chain.Contains(c.fontAsset)))
                        failures.Add(scope + " FW-03 " + label.name + " missing or wrong glyph U+" + source.ToString("X4"));
                    if (!UiKit.TitleFont.characterLookupTable.ContainsKey(source) && c.fontAsset != chain.FirstOrDefault())
                        failures.Add(scope + " FW-03 expected the physical bold fallback for U+" + source.ToString("X4"));
                }
            }

            yield return new WaitForEndOfFrame();
            var drawn = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                if (saveShot) File.WriteAllBytes(Path.Combine(output, scope + ".png"), drawn.EncodeToPNG());
                foreach (var label in inkRects.Keys) label.enabled = false;
                yield return new WaitForEndOfFrame();
                var blank = Screen20Xb();
                foreach (var label in inkRects.Keys) label.enabled = true;
                Canvas.ForceUpdateCanvases();
                foreach (var entry in inkRects)
                {
                    var label = entry.Key;
                    var button = label.GetComponentInParent<Selectable>();
                    if (button != null && !button.interactable)
                    { report.Add(scope + " FW-07 disabled, exempt: " + W11bPath(label.transform)); continue; }
                    var colors = new List<Color32>();
                    foreach (Rect rect in entry.Value)
                        for (int y = Mathf.Max(0, Mathf.FloorToInt(rect.yMin)); y < Mathf.Min(drawn.height, Mathf.CeilToInt(rect.yMax)); y++)
                            for (int x = Mathf.Max(0, Mathf.FloorToInt(rect.xMin)); x < Mathf.Min(drawn.width, Mathf.CeilToInt(rect.xMax)); x++)
                                colors.Add(blank[y * drawn.width + x]);
                    if (colors.Count == 0) { failures.Add(scope + " FW-07 no backdrop pixels " + label.name); continue; }
                    colors.Sort((a,b) => GameApp.Luminance11(a).CompareTo(GameApp.Luminance11(b)));
                    var fill = colors[colors.Count / 2];
                    float ratio = GameApp.Contrast11(label.color, fill);
                    string line = scope + " FW-07 " + W11bPath(label.transform) + " foreground #" +
                        ColorUtility.ToHtmlStringRGB(label.color) + " actual background #" + ColorUtility.ToHtmlStringRGB(fill) +
                        " contrast " + ratio.ToString("F2", CultureInfo.InvariantCulture);
                    report.Add(line);
                    if (ratio < GameApp.MinContrast11) failures.Add(line);
                }
                if (screen == "play" && ExpectedRoles21(app.ThemeId11) != TextRole.None)
                    yield return CompareInk21(Label21("StageLabel11"), drawn.GetPixels32(), blank, scope, report, failures);
            }
            finally
            {
                foreach (var label in inkRects.Keys) if (label != null) label.enabled = true;
                UnityEngine.Object.Destroy(drawn);
            }
        }

        // Compare the visible top and bottom of each Han glyph (not the padded SDF mesh). Use subpixel
        // threshold crossings on the row's strongest stroke to avoid rounding a half-pixel budget at 720p.
        private static List<float> HanInkCenters21(TextMeshProUGUI label, Color32[] frame, Color32[] blank)
        {
            var centers = new List<float>();
            label.ForceMeshUpdate();
            foreach (var c in label.textInfo.characterInfo.Take(label.textInfo.characterCount))
            {
                if (c.character < 0x4e00 || c.character > 0x9fff) continue;
                Vector3 a = label.transform.TransformPoint(c.bottomLeft), b = label.transform.TransformPoint(c.topRight);
                int y0 = Mathf.Max(0, Mathf.FloorToInt(a.y)-1), y1 = Mathf.Min(Screen.height-1, Mathf.CeilToInt(b.y)+1);
                var rows = new float[y1-y0+1];
                for (int y = y0; y <= y1; y++)
                    for (int x = Mathf.Max(0, Mathf.FloorToInt(a.x)); x < Mathf.Min(Screen.width, Mathf.CeilToInt(b.x)); x++)
                    {
                        int i = y * Screen.width + x;
                        var p = frame[i]; var q = blank[i];
                        rows[y-y0] = Mathf.Max(rows[y-y0], Math.Abs(p.r-q.r) + Math.Abs(p.g-q.g) + Math.Abs(p.b-q.b));
                    }
                float threshold = rows.Max() * .5f;
                Assert.That(threshold, Is.GreaterThan(5f), "no visible ink for " + c.character);
                int lo = Array.FindIndex(rows, k => k >= threshold), hi = Array.FindLastIndex(rows, k => k >= threshold);
                float bottom = lo > 0 ? lo - 1 + Mathf.InverseLerp(rows[lo-1], rows[lo], threshold) : lo;
                float top = hi+1 < rows.Length ? hi + Mathf.InverseLerp(rows[hi], rows[hi+1], threshold) : hi;
                centers.Add(y0 + (bottom+top)*.5f);
            }
            return centers;
        }

        private IEnumerator CompareInk21(TextMeshProUGUI stage, Color32[] title, Color32[] blank,
            string scope, List<string> report, List<string> failures)
        {
            var themed = HanInkCenters21(stage, title, blank);
            var font = stage.font; var style = stage.fontStyle; var weight = stage.fontWeight;
            try
            {
                stage.font = UiKit.Font;
                stage.fontStyle = FontStyles.Bold;
                stage.ForceMeshUpdate();
                yield return new WaitForEndOfFrame();
                var baseline = HanInkCenters21(stage, Screen20Xb(), blank);
                Assert.That(themed.Count, Is.EqualTo(2), "Expected 第 and 关 in the stage tag.");
                Assert.That(baseline.Count, Is.EqualTo(themed.Count));
                float em = stage.fontSize * app.canvas.scaleFactor;
                float delta = (themed.Average() - baseline.Average()) / em;
                string result = scope + " FW-06 " + stage.text + " themed centers=[" +
                    string.Join(", ", themed) + "] base centers=[" + string.Join(", ", baseline) +
                    "] pixels/em=" + em + " signed mean offset=" + delta.ToString("F4", CultureInfo.InvariantCulture);
                report.Add(result);
                if (Mathf.Abs(delta) > .03f) failures.Add(result);
            }
            finally { stage.font = font; stage.fontStyle = style; stage.fontWeight = weight; stage.ForceMeshUpdate(); }
        }
    }
}
