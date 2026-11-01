# E0439AE 六配置屏幕复核：商店、手册、计分详情与调试

状态：本报告所列实际可见内容的规格符合性与制作质量未发现新缺陷；跨配置限域复核通过。不是 v0.7 整版、美术全量、全部媒体、Player 或发布通过。

输入为 art0.7.0，规则 p0.6.0 / schema4，源码检查点 `e0439ae0b6efdcd12ef2f756e8bf5a91b71d0b1b`。审查者 `/root/v07_shop_ui` 只读截图、源文件和已完成证据，没有运行 Unity、改生产、改测试或改验收标准。

独立性边界：本审查者曾参与基础商店、手册、详情及部分排版/测试修复，不能将本人检查作为这些实现的唯一独立最终质量签署。对 root 后续三列抽屉、跨尺寸布局增量和非本人实施内容进行独立复核；基础商店/详情已由 `/root/v07_art` 对指定六张完成交叉检查，范围如下，不扩大其样本范围。该交叉审查者是图标作者，因此只独立核布局集成，不自行签署图标风格质量。

## 实际证据与覆盖

屏幕根目录：[六配置原始截图](E:/agent_workspace/gpt/game2/artifacts/cache/v07-checkpoint-e0439ae-0900-screens)。每套 `screen-report.json` 均完成，为 `captured_pending_visual_review`、48 张、error 为空。本报告实际逐张打开其中分配的 22 张，共 **132 张**；另看 QF/S09 交叉样本 4 张、同源完整 PlayMode 专项样本 11 张，共 **147 张原始 PNG**。实际查看清单与逐图 SHA 见 [viewed-images.json](E:/agent_workspace/gpt/game2/artifacts/cache/v07-final-screen-shop-review/viewed-images.json)，未查看的 PNG 不标作实图审查。

| 配置 | 实际像素 / 文字比例 | 本范围实际查看 |
|---|---|---:|
| Normal1080 | 1920×1080 / 100% | 22 |
| Small720Font150 | 1280×720 / 150% | 22 |
| Laptop768 | 1366×768 / 100% | 22 |
| Normal1440 | 2560×1440 / 100% | 22 |
| Aspect16x10 | 2560×1600 / 100% | 22 |
| Ultrawide | 3440×1440 / 100% | 22 |

各套对应编号为 013、014、015、016、026、027、028、029、031、033–045：口令/调试入口、手册两页、三列预览/复制/历史抽屉、控制器/道具详情、商店五页、32 控制器对局和七个调试分类。高分辨率原图通过图像查看器缩放显示，未据此声称逐像素目视审过每个字形。

**覆盖边界**：六套 48 屏路线没有点击 `FullScoreDetails`；026/027/031 是三列抽屉，不能算完整逐步明细。720 大字号手册、商店左栏和调试表单包含真实滚动视口，单张顶部图不能证明全部条目可见。完整明细、逐字 GPU 与滚轮到达证据来自下面明确命名的同源专项，不加到六套路线数量，也不推定六尺寸均执行了专项。

## 实图结论

### S10 商店

六张固定货架与下方五个分类的位置稳定。720 / 150% 的控制器名称、稀有度几何刻度、附魔角标和报价均存在，未复现旧版大字号标题整段消失；“总持有已满”仍保留金额，可以比较成本。道具“库存已满”与控制器容量原因分开，未用泛称覆盖价格。

钱包完整显示 40，金币单位独立，利息“每 10 金币利息 +1 / 计息本金上限 50”与设置/手册按钮没有碰撞。研习页显示当前等级与升级后倍率；骰子强化、服务及空骰柜的说明与操作区分清楚。已装备区底部半行属于视口裁切且有滚动通道，不误报为标题缺字，也不以顶部图声称十件都已展示。商品摘要省略号有既有详情入口，不声称其摘要含全部效果文字。

16:10 与超宽保持居中工作区，桌面背景填充额外区域，未见卡片横向拉伸、最右报价或固定动作被屏边截断。

代表原图：[720 / 150% 道具页](E:/agent_workspace/gpt/game2/artifacts/cache/v07-checkpoint-e0439ae-0900-screens/Small720Font150/screens/033-shop-tab-0.png)、[超宽研习页](E:/agent_workspace/gpt/game2/artifacts/cache/v07-checkpoint-e0439ae-0900-screens/Ultrawide/screens/034-shop-tab-1.png)。

### S14 手册与 S15 详情

100% 的牌型页两列展示 14 个真实牌型，散点在末尾；等级、倍率和可达性可区分。720 / 150% 每列仅显示前部行，保留滚动条，底部规则说明有独立滚动区；不将可见约四行当作全 14 行覆盖。颜色页明确独立乘区、提交非空且无数量门槛、取同点红色最大组。720 下红组曲线后部仍需滚动，100% 各高分辨率可见 0–8 档。

本路线控制器详情是 **C30 蓝色疯狂 +10 基础分附魔**。技能、该实例附魔、当前状态、触发条件和交易信息分区清楚；三段几何稀有度与附魔不挤标题。混色时条件未满足信息清楚，未误显示当前已触发。720 的附魔说明换两行，关闭按钮和滚动区保持可用。道具红色浪潮的效果、合法阶段、当前状态和使用动作清楚。此样本不代表 C41 所有者、C53 各分支等所有详情组合均已看图。

代表原图：[720 牌型页](E:/agent_workspace/gpt/game2/artifacts/cache/v07-checkpoint-e0439ae-0900-screens/Small720Font150/screens/015-handbook-hands.png)、[720 控制器详情](E:/agent_workspace/gpt/game2/artifacts/cache/v07-checkpoint-e0439ae-0900-screens/Small720Font150/screens/028-controller-detail.png)。

### S07 三列与完整逐步明细

三列抽屉的来源、运算和结果保持分栏。样本公式完整表达 `741 × 9 × 1 × 1 × 1/1 × 2^1`，最后取整一次得到 **13,338**；永久/当前/有效点数、永久六面和与单骰贡献没有混为一项。复制后状态文案改变而固定动作未移位；历史抽屉的回放和后置记录入口未压住关闭按钮。

另实际看同源专项的 150% 完整明细顶部、中段、底部、32 控制器长整数及历史永久后效。来源行保持独立高度，未见旧的第 3/4/5 行重叠；完整长整数 **1,081,570,823,829,042,554,880** 在全明细中可读，三列结果栏必要换行而非丢位。最后取整和永久效果只在提交后执行的说明能滚到；历史底部展示点数改造、染色、C01 本次收入、控制器自毁，未混回本手分数。

样本：[完整明细顶部](E:/agent_workspace/gpt/game2/artifacts/cache/v07-full-play-e0439ae-085726/DiceCraftTests_7cdebf4eea26435b91a3e22a361b9a32/extra-capture/000-MaximumTable-font150-page00.png)、[完整明细来源中段](E:/agent_workspace/gpt/game2/artifacts/cache/v07-full-play-e0439ae-085726/DiceCraftTests_7cdebf4eea26435b91a3e22a361b9a32/extra-capture/008-MaximumTable-font150-page08.png)、[长整数尾部](E:/agent_workspace/gpt/game2/artifacts/cache/v07-full-play-e0439ae-085726/DiceCraftTests_7cdebf4eea26435b91a3e22a361b9a32/extra-capture/150-Debug32-font150-page99.png)、[历史永久后效](E:/agent_workspace/gpt/game2/artifacts/cache/v07-full-play-e0439ae-085726/DiceCraftTests_c099754b8a4a4581b2e00cfeaaad2c8f/extra-capture/024-historical-permanent-effects-font150-page24.png)。

### S20/S21 调试

口令 `XD is god`、复制/空白/读取三入口与普通存档隔离说明可读。32 控制器候选和已应用数量一致，七分类导航、固定五个底部动作、关闭/返回与只读预览分区稳定。720 / 150% 右侧精确大整数没有省略；当前物理面、点数、颜色、提交顺序按可读行换行。资源页明确钱包上限 10 亿、数量/容量 1–32；三条随机流分别显示。长表单需滚动，截图未遍历全部字段。

HUD 展示九张紧凑卡加“全部 32 件 >”，骰池与锁栏分页可见，不把九张卡算作已看全 32 项。另看同源专项的遥控骰最后一行 D025–D032，以及点击 D031 后仍保留该行的原图：目标、点数与确认区未交叠。真实 32 目标滚轮、完全可见及射线命中来自专项断言，不从两张静态图推导。

## 同源专项引用与证据绑定

完整 [NUnit 085726](E:/agent_workspace/gpt/game2/evidence/v07/unity/nunit-20261101-085726.xml) 为实际 **152/152、0 失败、0 跳过**。本报告只引用以下已通过用例，不重跑或更改门槛：

- `PLAY_ART07_BreakdownEverySourceFitsAndWheelRevealsRealGlyphsFont100/125/150` 与 `PLAY_ART07_BreakdownHistoricalEffectsRemainCompleteAndReadonlyAtFont150`：实际 `Factor4 → FullScoreDetails`，每一来源行/字形完整、顺序和字号、真实滚轮视口射线、逐字 GPU 有墨迹，读取前后 state / actionSequence / 存档字节一致。
- `PLAY_ART07_ScoreDrawerThreeFontsMapEverySourceAndWheelReachEveryGlyph`、`PLAY_ART07_ScoreDrawerC53BranchAndHistoricalRowsKeepFrozenExactValuesAndCopy`：三列所有来源、C53 冻结分支、历史与精确复制。所看 C53 图为共享 q 的确定分支 `189/2 → 94`，不是重抽结果。
- 两项 `PLAY_V05_Handbook...`：容量/简化/升级/重载响应及 20 次只读查询；`PLAY_V06_DebugThirtyTwoDiceLevelsInputFailureAndExactPreviewAreAccessible`：32 骰、100 级、非法输入不提交、分页及精确预览。
- `PLAY_ART07_RemoteThirtyTwoRowsEachWheelFullyVisibleAndScrollReadOnlyAtTwoFonts` 与旧 `PLAY_V06_RemoteThirtyTwoTargetsScrollAndKeepExactlyTwoWithoutOverlap`：32 项真实滚轮/完全可见/射线、精确两个目标及原状态语义。

以上完整批次实际运行在 **1920×1080**。不能把它推成 720 / 超宽的专项逐字 GPU 全量通过。完整回归的发现集合、存档归档和源闭环见 [FULL_REGRESSION_E0439AE_REVIEW](E:/agent_workspace/gpt/game2/evidence/v07/review/FULL_REGRESSION_E0439AE_REVIEW.md)。其中恢复的是**公共测试夹具记录的 12 项偏好**，不是所有应用偏好；本次 ScreenRecorder 自身报告的是其列出的六个整数偏好恢复，不混同两者。

只读 [bindings.json](E:/agent_workspace/gpt/game2/artifacts/cache/v07-final-screen-shop-review/bindings.json) 已核：

- 六套共 288 PNG 的 SHA、实际像素、字号与报告一致；每张截图采样前后规则 state 与 save 不变；所有标为只读的动作前后 state/save 一致，普通 checkpoint 保持。
- `source-before`（08:59:33 UTC）至 `screens-source-after`（09:20:21 UTC）**1,359 个物理源文件一致**，包含生成的 BuildIdentity；本次独立核对时点 **09:23:15 UTC** 的当前文件 SHA 也无差异。它与完整测试的 1,358 个受审源文件口径不同，不伪称数量相同。之后 root 通知已添加抓手缺陷回归测试，属录制/本次核对之后的新工作，不倒算为 E0439AE 旧媒体源漂移；本报告不声称后续工作树仍等于这份快照。
- 五程序集 MVID、DLL/PDB 字节前后相同，已加载 MVID 与 PDB 审计匹配；168 份 C# 文档校验零差异。Runtime MVID `70c58cbc-d0c0-4a8a-bea7-3e3f1ff9f383`；actual identity 指向 E0439AE。
- 环境为 Unity `6000.6.7f1`、Windows Editor、Direct3D11、RTX 3080。Editor 的零 BuildGUID 不是独立 Player 构建身份。
- 元数据中低于 40 物理像素的“可见部分”均为滚动视口边缘裁切的部分行：各套 2/14/18/2/2/2 条；完整控制框本身未缩小，没有发现完全可见且小于既有 40 px 的交互框。详单保留，不将部分行当作完整热区通过；实际到达性只引用上述专项。

## 交叉样本与未关闭事项

`/root/v07_art` 已实际逐张看 Small720Font150 / Ultrawide 的 028、033、034 共六张，并返回：未见正文、价格、标题的新重叠；货架六件 5/4/6/3/2/7 报价分别留在按钮内，稀有度和附魔角标未压名字；五道具名称、库存已满原因与金额分离；研习 Lv0→1、×12→16、×7→9.25 清楚；C30 详情的 ×4 基础技能、+10 附魔与当前实例分层，720 附魔换行仍在自身区域，关闭无遮挡；超宽居中且无拉伸/出界。其中特别保留 720 详情下方需滚动、左侧装备下缘裁切不等于全列表已看两项边界。原交叉意见与六图 SHA 保存于 [cross-review.json](E:/agent_workspace/gpt/game2/artifacts/cache/v07-final-screen-shop-review/cross-review.json)。

另实际看 Normal1080、Small720Font150 的 030/032 四张，反馈给 art 审查者：QF 勾章不压累计；基础/剩次/利息/C01 行与 Debug 导航没有重叠；结算纸单标题、金额、净增、余额与固定“进入商店”清楚，720 长内容滚动不挤按钮；背景原卡/骰/空栏可见，未见明显双影。静态图不能验证 280 ms 交接或精确 35% 遮罩 alpha，亦不代表连续中央结算动画通过。

**另域已知返工**：root 在本次 025（最大锁骰）确认调试面板关闭后抓手未恢复，正在准备最小修复；该缺陷不在本报告分配的 22 屏内，仍明确保留，不能把本范围通过提升为全部 288 屏或全部媒体通过。007 普通抓手原疑点由其审查者按原图像素撤回，本报告没有替其独立重新判断原因。

S11/S13 连续交易、动效时序、声音/AudioMixer、原生 Player 显示与 180 秒性能、macOS 实机、用户风格与体验验收不由本报告关闭。修复产生新源码后，受影响媒体须重新绑定；本报告只对 E0439AE 所列证据有效。
