# Player Reduced Roll 覆盖修正：实际补丁独立复核

结论：**已集成 revision20 的静态修正符合先前方案，未发现缺陷，也未降低原验收要求。** 本报告只签源码修正与证据链；不声称新 Unity 专项、完整回归或 Player 已通过。复核者 `/root/v07_spec_audit` 未运行 Editor/Player，未修改生产、测试、标准或 HEAD，未外传。

## 原失败与根因

基线 `404176661240a5d9834895416ed3719d6493e253` / revision19 的实际 Windows 记录位于 `evidence/v07/player-4041766-1141/strict-full/player-20261101-114126-2332a3de`。正常 trace 已执行145/145步，终局 RunFailed/第8关；normalReplay.passed=false，rollAnimations=1、animationSkipsTotal=0、reducedMotionChecks=0，末尾三路径覆盖 Require 真实失败。145行回放有49条显式 Roll，savedStateMatches/renderedStateMatches 均为 true，首个完整投掷运动检查真实通过；这些不等于整体 Player 成功。

旧探针根据 rollAnimations==1 选 Reduced，但该计数及 Reduced 检查都位于 IsBusy 内。产品 `GameApp.Art07Presentation.cs:87–90` 的 Reduced Roll 零时长会在 Send 返回前同步 FinishRoll 并 Busy=false，`DiceTable.cs:245–248` 也是立即完成。因此第二次 Roll 后动画计数不变，随后各次重复选择 Reduced，无法进入后续 animated skip。旧失败记录和旧 gate 保留。

## 实际补丁核对

审查时 Assets/Packages/ProjectSettings 相对4041766仅有 `PlayerVerification.cs` 一文件变化，实际 SHA 为 `7e76293d6e9be83eb642c68bcbc2cba0245357fb7fef7852f56077c703cf6787`。root 保存的原文件与4041766 Git blob 原字节一致，SHA `1aa09930202f68d61a4fd9b8944cafcc8f0728a4735db30484e307c11c9b68f6`。

逐条确认：

- ProbeRevision 19→20；正常 trace 循环前新增局部 normalRolls=0。
- Reduced 选择条件只把 rollAnimations==1 改为 normalRolls==1，因此仅第二个已接受的显式 Roll 被选中；不把 UseItem 的附带投掷计入。
- normalRolls++ 位于原 accepted/nonduplicate/nonpending Require 通过之后。拒绝命令仍会先失败，不能被计入。
- 原 Reduced 的 `Require(!app.table.IsRolling, ...)` 与计数整体移出 IsBusy，顺序确认为 accepted → normalRolls → Reduced 立即检查 → Busy 动画分支 → WaitReady；检查之前没有新增 yield。因此仍验证同步立即完成，没有先等待再宣称立即。
- 原 rollAnimations 继续仅在 Busy/显式 Roll 中递增。first-full 验证、每第3个 animated Roll 的 .12秒后 Skip、最终三路径覆盖、VerifyLandedDice 及所有逐步规则/磁盘/ViewState比较均未改。

对全文件进行明确四处转换和块移动后，结果与实际集成文件正文完全相同；仅在此正文比较中统一 CRLF/LF，原字节 SHA 单独保留。解析出的95条原 Require 调用多重集合完全一致，没有删除、改条件或改文案。性能10秒warmup/180秒sample/16.7ms预算与原生显示相关代码也由全文件差异检查确认未变。游戏 UI、动画、规则、资产没有因本补丁改变。

## 当前边界

逻辑上，独立已接受 Roll 计数解除旧闭环，同时保留真实 Busy 动画计数含义。此推导不是下一轮运行计数预测；实际首个完整投掷、Reduced立即完成、Skip覆盖、后续 Debug/显示/性能仍需新源真实执行。root正串行编译并准备两个既有专项，本报告没有用“正在运行”预填通过。

新检查点必须重新绑定全量报告与构建。旧4041766 gate和失败Player不能直接放行revision20；旧艺术媒体/ART14保持原采集身份及已确认有限复用，不由探针修正增加美术签署范围。缓存审计、原/新源码快照、实际补丁和失败证据 SHA 见同名 bindings。
