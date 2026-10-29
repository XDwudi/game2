# Unity EditMode / PlayMode 独立运行证据复核

审查：`/root/spec_audit`；运行与集成：root；2026-10-29 UTC。审查者未操作 Unity；独立解析原始 NUnit XML / JSON、核对测试源码和编译 PDB，并实际运行 .NET 预览副作用检查。规格仍为用户原始 P0 v0.4。

## 当前范围结论

| 维度 | 结论与边界 |
| --- | --- |
| 规格符合性 | **通过已覆盖的核心规则、保存边界和五项 UI 缺陷回归**。52 黄金、概率/配置、Session 与真实 JsonUtility 检查、不可逆锁定/调序、教程隔离、Enter 单入口、上一手历史及 C07/C15 效果预览已有真实证据。UI_HIGH_RISK_CHECKLIST 的剩余交互/视觉边界没有因这些通过而自动关闭 |
| 制作质量 | **已测编译版本的相关修复通过；最新工作树回归未完成**。预览复用真实提交逻辑的隔离副本，不重复写一套效果判断；失败历史保留，黄金期望未改，JSON 与 XML 名单完全一致。测试后新的输入优化、重试展示、动画/探针修改不能沿用旧通过结论 |
| 实际运行 | root 的真实 Unity 6000.6.7f1 EditMode 268/268、PlayMode 13/13 通过；审查者独立 .NET 预览检查 9/9 通过。**Windows 构建与性能未通过**：第一轮 Player 只完成 3/97 步，在非 Legacy 动画缺 Animator 错误处失败，未进入压力采样。动画修复后尚需新构建/运行证据 |

没有整体 ACCEPTED、移动端或人工体验通过结论。

## 原始证据一致性

- EditMode：`evidence/unity/editmode-03.json` 与 `nunit-20261029-003107.xml`，2026-10-29 00:31:05–00:31:07 UTC；268 个叶子用例、268 Passed、0 Failed / Skipped / Inconclusive。逐一比较 FullName 多重集合相同。包括 143 Rules、114 Session、11 真实 AtomicSaveStore 用例；全部 GOLD-S01 至 GOLD-S52 已执行。
- PlayMode：`playmode-04.json` 与 `nunit-20261029-004024.xml`，00:40:15–00:40:24 UTC；13 个叶子用例全部 Passed，0 跳过。五项 UI 修复对应测试均在结果中，包括真实 KeyboardState(Key.Enter) 输入与上一手恢复测试。
- 历史保留：EditMode01/02 均 229/255、26 失败；PlayMode01 0/12、12 失败；02 10/12、2 失败；03 11/13、2 失败。没有删除失败用例或用零项检查冒充成功。
- 原规格 Markdown SHA-256 与 Downloads 原件一致；Config JSON 与规格 JSON 对象完全相等；GoldenScores 包装内的 52 cases 与原附录 JSON 完全相等。测试仍逐字段比较全部 18 个 ScoreBreakdown 字段。
- 黄金 null 适配只在测试读取时将源 `chosenGroupId:null` 转哨兵，JsonUtility 反序列化后恢复 null。单独的 null / 空串 / 非空串及全部原 null token 检查已执行，没有将 null 和空串期望混为一谈。

复核脚本与完整逐项结果：`audit_unity_reports.py`、`unity-report-independent-audit.json`、`.log`。这些报告绑定原 JSON/XML SHA-256；脚本不是游戏运行替代物。

## 原缺陷关闭记录（限定已测编译体）

| 缺陷 | 已执行用例 | 结论 |
| --- | --- | --- |
| RV-UI-01 首次保存失败入口 | PLAY_FirstNewRunSaveFailureOffersRetryAndBlocksBothReplacementEntrances | 已关闭：原候选留存、两条替换入口保护、原种子重试与磁盘一致 |
| RV-UI-02 只读日志中断展示 | PLAY_ReadOnlyActionLogCannotAbortAnAlreadySavedUiCommand | 已关闭：日志只读时存档提交与 UI 继续，原日志保留 |
| RV-UI-03 购买资格反馈 | PLAY_ShopPurchaseFooterShowsSlotAndDuplicateFailuresBeforeConfirm | 已关闭对应通用入口缺陷：PurchaseError 决定提示/确认状态，重复控制器与满库存实跑；所有复杂目标组合仍见高风险清单 |
| RV-UI-04 Enter 双消费者 | PLAY_RealEnterKeySubmitsOnceWithoutInvokingSelectedPauseButton | 已关闭：真实输入注入只加一次序号/得分，已选 Pause 未收到第二次 Submit |
| RV-UI-05 上一手与永久效果不可查 | PLAY_PreviousSubmissionRetainsOriginalScoreAndPermanentEffectsAfterNextRollAndReload | 已关闭：C07/C15 两种 Debug 夹具分别覆盖下一手 Roll 清空即时效果后仍显示原分数/原面/源目标；重载后文字与快照一致；当前手不借用旧效果 |

UI-R02 的提交前永久目标提示已通过源码复核；C07/C15 预览 API 已在第 13 个 PlayMode 用例中实际运行并与提交匹配。该用例没有在触发前点击 Expanded 断言正向提示文字，C20 第 K 栏提示也未有专门 UI 断言，保留这部分细化覆盖，不再将“完全未实现目标提示”当作当前缺陷。

## 新 PreviewSubmissionEffects 的独立检查

当前 `GameSession.cs` 132–142 行持锁复制 state，清空副本 lastEffects 后直接 Submit(candidate)，仅返回复制/染色事件的克隆。Submit/SettleStage/StartHand 只改传入候选，未调用 SavePending 或 Committed；cfg 为读取来源。Pending 不加入正式 S，预览既不确认也不清除它。

审查者独立 .NET 实跑：C07 命中 / C12 抑制、C15 命中 / 红潮抑制，每种分别有/无 Pending；另有无 Run、HandReady、未锁骰的空预览组。重复预览 8 次与修改返回 EffectRecord 后，state（包含 3 RNG、序号、永久面、计数）、Pending、保存次数、提交观察者次数均不变；随后真正提交得到完全相同效果。9 组通过。证据为 `UnityEvidenceAudit/Program.cs`、`preview-effects-independent-results.json`、`unity-evidence-independent.log`。该宿主使用当前生产 Core 源码，不模拟 Unity UI。

## 版本新鲜度，禁止自动续用旧通过

独立读取四个 Unity Portable PDB 的 29 个项目源校验和，见 `unity-pdb-source-check.json`。当前 Core / EditMode / PlayMode 测试源匹配编译记录；但检查时 Runtime 的 DiceTable、GameApp、PlayerVerification、GameApp.Modals 已有后续修改。

已测 Runtime.dll SHA-256 `7317507e6841eaf8b2615a8355a80355d3dbc04ef4d5819aa8d8bc5fef4a9b2d`；PDB `9a76f5e465d56efe9567658cb3f74f04ea954d1091c2d6eac56f6b00b1246988`。其中 Modals 编译源为 `54cc84265f07a1c9473a9b0649f1629d6fb7e33d494caf2dba7db3c77d8ea3de`，即本轮开始核对的含历史/预览分离版本。之后 PresentCommitted / RetrySave 统一展示路径与数字键数组优化等，必须在最新编译后重跑受影响 PlayMode。DiceTable 的 Player Animator 修复尤其不能用 Editor PlayMode 成功替代 Windows Player 重验。

EditMode03 早于新增 PreviewSubmissionEffects 的 Core 编译；原规则检查仍证明其当时版本，新增方法由后续 PlayMode04 和本次独立 .NET 9 组补验。最终交付需再绑定统一冻结源码/构建，不把本报告的旧 hash 当成最新工作树 hash。
