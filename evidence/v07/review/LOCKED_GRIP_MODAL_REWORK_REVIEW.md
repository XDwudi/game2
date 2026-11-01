# 025 模态关闭后锁骰抓手恢复：独立限域复核

**结论：025 已复现缺陷在 revision02 修复及本报告两尺寸场景内关闭。** 规格符合性与制作质量均通过本次限定检查；不表示整版、全部模态、最终媒体、原生 Player 或全量回归通过。007 普通抓手的原疑点继续保持“误判已撤回”，没有据其改动产品。

审查者 `/root/v07_shop_ui` 未编写本次生产修复或新增两项回归，只读源文件、真实 NUnit、归档存档和截图；未运行 Unity、未修改生产/测试/标准。虽然曾参与旧模态界面实现，此结论针对 art/root 新增的抓手装饰同步增量。权威输入为 art0.7.0（ART_SPEC S05/S06，尤其第 206 行），规则 p0.6.0 / schema4。

## 缺陷及最小修复

原缺陷不是骰子解锁或排序规则失效，而是模态存在时重绘背景会按原 `CanMoveLockedDice07` 隐藏抓手，关闭模态时却不恢复装饰。真实 Debug 场景应用和普通设置的字号/颜色辅助回调都能触发。原失败保留：

| 实际报告 | 场景 | 结果 |
|---|---|---|
| [092559](E:/agent_workspace/gpt/game2/evidence/v07/unity/nunit-20261101-092559.xml) | Debug 的 `RunDebugScene` 关闭 | 1 项 0 过 1 败；期望 8，实际 0 |
| [092846](E:/agent_workspace/gpt/game2/evidence/v07/unity/nunit-20261101-092846.xml) | 设置改字号/颜色辅助后 `SettingsDone` | 1 项 0 过 1 败；期望 8，实际 0 |

独立查看两张原失败 EOF 图，八个已锁槽保留骰子、锁标、槽号，却确实没有六点抓手。没有以缩放截图的主观印象替代复现。

生产相对 E0439AE 的语义差异仅两处：

- `GameApp.Modals.cs` 原 `restoreDice` 分支不变，普通关闭的 `else` 新增一次 `SyncLockedGripsAfterModal07()`。
- `GameApp.Art07DiceAffordances.cs` 新增 18 行同步：遍历已布局、实际可见的 `Die_` 命中对象；沿用原 Selecting、无待选、无 busy、无 pending-save、无 modal 条件；只为已锁骰恢复原六点装饰，否则隐藏已有装饰。

该路径没有调用规则命令、RNG、读写存档、`Refresh()`、骰池布局或表现时钟，也不替换原 `DiePointer`。装饰继续 `raycastTarget=false`。静态上避免关闭暂停时重画整盘、重置表现或提前显露最终预览；本次三用例不是全部暂停/表现生命周期的新全量证明。

## 两尺寸实际回归与实图

| 报告 / 归档 | 实际尺寸 | 结果 | 归档与本次实际打开 |
|---|---|---|---|
| [093026](E:/agent_workspace/gpt/game2/evidence/v07/unity/nunit-20261101-093026.xml) / [1080 归档](E:/agent_workspace/gpt/game2/artifacts/cache/v07-grip-rework-1080-093026) | 1920×1080 | 3/3，0 失败/跳过/不确定；18.0881595 秒 | 18 PNG 全部核 SHA；实际看 8 张 |
| [093122](E:/agent_workspace/gpt/game2/evidence/v07/unity/nunit-20261101-093122.xml) / [720 归档](E:/agent_workspace/gpt/game2/artifacts/cache/v07-grip-rework-720-093122) | 1280×720 | 同三项 3/3，0 失败/跳过/不确定；16.7147756 秒 | 18 PNG 全部核 SHA；实际看 9 张 |

两尺寸各实际看 Debug 的 `RunDebugScene`、`CloseDialog`、Esc（150%）三路径；设置的 `SettingsDone`（实际 125%）、`CloseDialog` 和 Esc（实际 150%）三路径；原 pending-eight 与 locked-eight（150%）。另看 720 的 Debug RunDebugScene 100%，确认普通单排布局与大字号双排布局不同。加两张原失败图，共 **19 张实际查看**，逐图 SHA 在 [viewed-images.json](E:/agent_workspace/gpt/game2/artifacts/cache/v07-grip-independent-final-review/viewed-images.json)。没有将全部 36 PNG 的哈希核验声称为 36 张逐图目视。

实图中各条关闭路径恢复八组清楚的 2×3 纸白点，位置紧靠对应骰子左侧，不盖骰面点阵、槽号、锁标或红蓝/材质说明；1080 单排与 720 大字号双排都无邻槽混淆。旧待选状态仍显示骰身外金色虚线和空心勾，没有提前出现排序抓手。确认锁定后空心勾消失、六点抓手出现。固定动作区无新增遮挡。

代表图：[1080 Debug Esc 150%](E:/agent_workspace/gpt/game2/artifacts/cache/v07-grip-rework-1080-093026/DiceCraftTests_10fb4fd07ae8448290cc1e3bb748fd51/art07-dice-affordance/debug-close-Escape-font150.png)、[720 Debug 运行场景 150%](E:/agent_workspace/gpt/game2/artifacts/cache/v07-grip-rework-720-093122/DiceCraftTests_b949fcce4a3f4745aeb8f2d08f6c13d5/art07-dice-affordance/debug-close-RunDebugScene-font150.png)、[720 设置关闭 150%](E:/agent_workspace/gpt/game2/artifacts/cache/v07-grip-rework-720-093122/DiceCraftTests_673a514857414173a17be061408af073/art07-dice-affordance/settings-close-CloseDialog-initial-font100-actual150.png)、[720 待选空心勾](E:/agent_workspace/gpt/game2/artifacts/cache/v07-grip-rework-720-093122/DiceCraftTests_56409ad2be274a5dbb290f6ffe95c850/art07-dice-affordance/pending-eight-font150.png)。

独立直接读原 PNG 做了限定 ROI 观测，未修改图片、未新增验收门槛：颜色取原 UiKit.Paper `F4E8CB`、每通道容差两级；两张 baseline 每槽纸白像素均 0。修后每一张关闭/locked 样本、每一槽均为六个分离的连通点：1080 每槽 54 像素，720 普通 100% 单排每槽 24，720 紧凑布局每槽 40。采用与实际布局相符的 ROI，不能把紧凑坐标套到 100% 单排图。坐标、每点连通面积和全部观测保存在 [pixel-observations.json](E:/agent_workspace/gpt/game2/artifacts/cache/v07-grip-independent-final-review/pixel-observations.json)。

## 实际断言与规则边界

三项真实引擎用例名称完整一致，原阈值未降低：

1. `PLAY_ART07_DICE_DebugAppliedTableAllCloseRoutesRestoreGripsWithoutApplyingDraft`：在真实隔离 Debug 工作区应用公开 MaximumTable 夹具，先确认模态期间抓手为 0；编辑未应用的钱包草稿，再走三种关闭路径。按钮关闭经既有可见性/射线点击 helper；Esc 通过 Input System 注入 KeyboardState 并确认 Dynamic 更新实际收到。关闭后无需测试侧 `Refresh`，必须有 8 个抓手、各 6 个实际渲染点；每点必须未 cull、alpha > .99、有效网格、PNG 内确有 Paper 像素。抓手在原 ≥39.9 物理像素容差的 40px 命中框内，中心射线第一命中必须属于原骰 `DiePointer`，且不与可见点阵重叠。
2. 同一用例检查关闭前后完整 state、actionSequence、debug save 字节、普通 active-run 字节和三个 RNG 都相同；未应用草稿不得被关闭动作提交。之后通过现有 EventSystem begin/drag/end 路径拖拽，必须出现合法插入线、保持相同锁骰集合并变为预期顺序，且仅增加一条事务。两尺寸实际归档的最终 debug 收据均为 `ApplyDebugScene, Reorder` 三组交替，关闭本身没有命令，没有解锁事务。这里是实际引擎输入事件路径验证，不冒称真人鼠标或 OS 硬件实测。
3. `PLAY_ART07_DICE_SettingsRebuildAllCloseRoutesRestoreOnlyLegalGrips`：真实设置页调用原字号与颜色辅助按钮回调造成 modal 内重绘，分别完成、X、Esc 关闭；同样检查八组真实像素/射线/点阵避让与 state、sequence、debug/normal 存档不变。文件名包含初始和**实际**字号，未把字号循环后的 100/125 错标为 150。
4. 原 `PLAY_ART07_DICE_VisibleGripUsesExistingHitAndPendingHollowChecksKeepPipsClear` 在 100/125/150 三档均运行：空心勾实际轮廓网格、待选无抓手、确认后八抓手、已有待选或 Busy 时不可拖、取消待选/解除 Busy 后恢复，以及 state/save 只读检查保持。该原用例及其后续 helper 与 E0439AE 归一化源码全文一致；未删原断言。这里三项用例共用四段说明，不是新增第四项测试。

Debug 失败复现与修复后测试文件均为相同 `d410addf…` 原字节；Settings 失败与修复后文件均为相同 `0d1f0e78…`。原 8→0 失败未通过改测试消失。

## 源码、程序集与换行符绑定

[bindings.json](E:/agent_workspace/gpt/game2/artifacts/cache/v07-grip-independent-final-review/bindings.json) 的独立只读核验无差异：

- 两修复后报告各 **1,360 个源文件起止相同**，两尺寸源集合和字节相同，五程序集 MVID/DLL 相同。每份 NUnit、binding、archive 清单及全部归档文件 SHA/字节数一致；两批分别 36 文件、18 PNG。
- 实际 Runtime MVID `c909cab3-7bef-47a5-bf52-0d1e156abf0a`，PlayModeTests MVID `99e568a2-7ef8-449a-858c-6782e0dc8bfe`。这已不是 E0439AE 完整回归时的 Runtime MVID。
- [09:33:09 PDB 审计](E:/agent_workspace/gpt/game2/artifacts/cache/v07-grip-targeted-pdb-0931.json) 五程序集、169 份文档、零差异，与两专项 MVID/DLL 一致。四个修改文件按 [实际编译源码副本](E:/agent_workspace/gpt/game2/artifacts/cache/v07-grip-compiled-source-0931) 核 PDB checksum，而非拿后来的当前字节替代。
- revision02 两生产候选及新增 Settings 测试与实际编译副本逐字节相同；归档副本 SHA 也与两专项 started/finished source SHA 相同。测试时使用候选的全 CRLF 文件。随后 root 恢复旧文件未改行原 EOL，并将新行/新测试使用 LF；独立核过四文件归一化字节与行序完全相同，但物理 SHA 已不同。这个后续整理不能冒称已在 093026/093122 实跑，仍须新检查点、重新编译和完整回归绑定。

本报告关闭的是实际发现的 **025 装饰恢复缺陷**；旧 E0439AE 025 原图和两个失败报告继续保留，不能改写为当时通过。007 原普通抓手疑点的撤回仍成立。最终六配置媒体、全量 Editor 回归、独立 Player/性能、macOS 及人工风格/体验验收仍由各自流程完成，本专项不替代这些工作。
