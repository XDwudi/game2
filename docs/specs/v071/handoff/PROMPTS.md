# v0.71 图片生成提示词记录

这些是本次实际使用的提示词。母版和商店图为视觉参考，正式文字、点数、价格、附魔及状态由游戏数据渲染。新增素材用最终母版作参考，不继续引用初稿。

## 01 对局母版初稿

```text
Use case: ui-mockup
Asset type: one polished 16:9 gameplay visual master for an original Chinese dice-building roguelike, v0.71.
Image 1 is a STRUCTURAL REFERENCE showing the current implementation. Redesign its art decisively; do not copy its flat empty panels or thin default typography. Preserve the main zones: compact left score/status column, controller cards across the top, two small consumable pockets upper right, central 3D dice play area, ordered locked dice along the lower area, actions at the bottom. This is a target visual, not a screenshot of a working build.

Art direction: playful retro screen-printed tabletop arcade game. Deep petrol teal textile felt, warm ivory paper cards, chunky charcoal silhouettes, coral red and saturated cyan accents, sparing amber gold. Strong inviting visual hierarchy similar in broad tabletop readability to polished indie deckbuilders, entirely original assets. No characters. No science-fiction frames. No ornate fantasy brass or wood table. No photoreal casino. No flat SaaS dashboard. No CRT distortion. No empty tall outlined rectangles.

Make actual illustrated card faces the visual signature: four substantial portrait cream cards across the upper center, each with a LARGE bold original two-to-four-color print illustration occupying 60 percent of the face: (1) a red pip die with radiating print shapes; (2) a witty staircase of dice; (3) two dice mirrored inside an ink oval; (4) a ticket vault with stacked tokens. Crisp blocky print edges, subtle half-tone texture, limited palette, rich composition within each card, consistent cream/ink frame and short soft offset shadow. Tiny clean name labels may be used, never big algebraic plus signs as the whole artwork. One small mint enhancement seal, one violet edge glint, same base frames.

Surface hierarchy: quiet low contrast felt with a broad gentle pool of light at the dice; flattened information strips and genuine raised pressable buttons only. Background texture restrained. Clean readable cream Chinese sans-serif UI, bold condensed numerals. Left column visually compact with the exact labels "第 1 关", "目标 180", "本关 110", "金币 0", "投掷 0", "提交 2"; 110 is large cyan and 180 smaller cream, simple cyan progress indicator. Avoid numerous boxed subsections.

Dice are beautiful tangible stylized 3D objects, not icons: rounded ivory ceramic with soft bevels, shallow inset enamel pips, one broad upper-left highlight, gentle teal bounce and visible soft contact shadows on the felt. Three available dice centered in the play area show top values blue six, blue three, red one. Two slightly smaller locked dice below show blue six and red four. Maintain believable cube faces, standard 1-6 pip arrangements. All bodies ivory; color comes from pips. The available dice have small amber selection brackets and unobtrusive small empty check marks. Locked dice live in a connected padded low-profile tray with a small solid lock seal and ordered labels "01" and "02"; three empty slots are understated recessed dots or corner marks, not big rectangles.

A shallow scoring ribbon above the dice says "得分预览" and "待提交", with muted tiny contribution pills, never six giant buttons. Bottom actions use consistent rounded stepped paper edges, short shadow and 3D press depth. Three major action areas labelled "重掷", "确认锁定", "提交 2 颗", with "清除待选" as small secondary text and "结束本轮" at the far right as a subdued option. Main focus is amber "确认锁定", blue reroll subdued. "牌型手册" and "设置" are small left-bottom controls.
Composition professionally art-directed and dense enough to feel like a finished game; dice large and central, cards richly illustrated, plenty of calm breathing room but no vast unused shelf. No watermark, no build hash, no browser chrome, no explanatory callout arrows. Output one full gameplay screen, not a multi-panel sheet.
```

## 02 母版定向修正

```text
Use case: style-transfer
Input image: this is the selected v0.71 visual direction for an original Chinese dice-building roguelike. Produce the polished final visual master by editing only the following targets, keeping the composition, objects, camera, rounded ceramic dice, teal felt, large card illustrations, typography hierarchy and overall color relationships.
1. Reduce distress and grain by roughly 60 percent. Paper cards and buttons should be warm clean printed stock with small controlled edge wear, not burnt or torn ancient parchment. Make the tabletop weave quieter and lower contrast. Keep the rich printed illustration shapes.
2. Correct the FOUR card title plaques across the top, left to right, to precisely "红点共鸣", "简单顺子", "镜像", "金库". These are illustrative art labels; no mathematical formulas on the card titles. Leave the illustrations unchanged.
3. Keep the shallow scoring strip but simplify it to the exact title "得分预览", with large "10" on the right, and small restrained contribution labels beneath; do not expand the strip or add any panels.
4. The cyan dice near the center remain blue-pipped ivory ceramic; maintain six, three, one visible top counts for the three available dice, and six / four for the two locked dice. Keep the remaining three tray slots understated.
5. All bottom buttons retain the same geometry. Make "提交 2 颗" a slightly less saturated cream-and-ink secondary treatment, and "确认锁定" the single strongest amber action. Make reroll visibly inactive because remaining rolls are zero, while keeping its label legible.
Do not add objects, panels or effects. No watermark. Output the complete 16:9 screen. This is a visual concept, not a new gameplay specification.
```

## 03 商店衍生

```text
Use case: ui-mockup
Asset type: v0.71 shop screen derived from an existing visual master for an original Chinese dice-building game.
Input Image 1 = STYLE MASTER. Reuse its exact printed ivory paper, bold ink illustration language, stepped button thickness, deep petrol teal felt, restrained red/blue accents, soft shadows, Chinese type weights. This is the single art direction.
Input Image 2 = CURRENT SHOP FUNCTIONAL REFERENCE. Preserve its organization and functions: wallet/equipped cards on the left, controller shelf on top, five shop tabs across the middle, selected tab products below, leave shop bottom right. Do not preserve the current empty rectangular look.

Make a finished 16:9 game interface, not concept painting. The entire screen belongs to the same game as Image 1.
Left 17% width: compact cream/ink game mark "骰子构筑", amber wallet "5", small interest line "每10金币利息 +1", occupied controller capacity "0/4", and three low-contrast pockets for equipment without a giant empty panel. A modest "手册" and "设置" control at bottom.
Upper shop area: three generously illustrated controller cards, each matching master card frame. Correct visible titles from left to right "有也不交", "简单顺子", "首当其冲". Large illustrative motifs are respectively an ivory die kept under a folded ticket, a staircase of dice in cyan and ink, a tiny leading coral flag with a line of chunky dice. Prices below cards "购买 6", "购买 4", "购买 5". Card art takes most of the card, title not over illustration. Add a compact refresh button "刷新控制器 3" top right. Put a quiet framed cream detail ticket on the right of the shelf with selected "简单顺子" enlarged motif and short generic "控制器详情" label, so 3 products do not leave a huge empty rectangle. No invented precise game rule text.
Middle five small paper tabs: "道具", "研习", "骰子强化", "局外服务", "骰柜". "研习" is active, indicated by amber underline and ink text, others quiet.
Below: three aligned low paper cards for study offers. Left "一对", center "两对", right "自选研习". The first two show oversized but simple paired dice illustrations and clear "Lv.0 → 1" with values respectively "×2 → ×2.75" and "×3 → ×4". Both purchase buttons say "购买 2". Right says "选择研习内容" as its only action. Values are visual references from current screen, no additional values.
Bottom right strong cream button "离开商店". Left low-priority short line "切换分区不会重抽商品".
Quiet grain, clean print edges, no torn parchment. Card and item imagery must be substantial, not tiny floating glyphs. Only active buttons have distinct press depth, data and descriptions are flat. No glass neon UI, no brass ornaments, no browser chrome, no build numbers, no screenshot thumbnail. No oversized empty boxes. Maintain actual desktop Chinese readability.
```

## 04 透明插画样件

```text
Use case: background-extraction / game UI illustration
Input image is the v0.71 STYLE REFERENCE, not an edit target for the entire screen.
Produce ONE standalone controller illustration inspired by its second card (dice climbing a staircase). This will be a real image asset inserted inside an existing Unity card frame.
Subject: three chunky ivory dice ascending a three-step petrol-teal staircase from lower-left to upper-right, bold charming screen-printed silhouette, a few cyan rays and sparse coral stippled print marks. Match the reference's cream #F4E8CB, ink #162629, petrol #173E40, cyan #55B6D9, tiny coral #EC655D. Reuse identical drawing language, restrained print grain, consistent thick ink outlines and small highlight planes.
Composition: square, single centered complete motif, art occupies about 78% of image, 10% clear padding all sides, nothing cut off.
Actual transparent background, no white or cream square, no paper rectangle, no card frame, no header, no text, no numerals, no price, no symbols claiming a score, no UI button, no ground plane outside the motif, no full canvas shadow. The ivory fill belongs only to the dice. Individual pip arrangements on each visible face stay simple valid 1–6 dice pips, illustration is thematic not a rules diagram. No checkerboard rendered into the image. Keep alpha edges clean.
```

