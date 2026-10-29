# P0 UI / 表现层独立源码复核

审查者：`/root/spec_audit`。日期：2026-10-28。实施者：root。

范围为 `DicePrototype/Assets/Game/Runtime`、`UI`、`Editor` 下全部 C# 与 VertexFace shader；参照 `docs/specs/P0_v0.4.md` 第 19、21、22、25–30 节。此次只读生产文件，未调用 Unity 或操作场景。此前 Core / Session 测试通过没有用于豁免本轮审查。

## 当前结论

| 维度 | 结论 |
| --- | --- |
| 规格符合 | RV-UI-01 至 05 已由 PlayMode04 对应真实回归关闭，限定其已测编译版本；剩余 UI 交互/视觉覆盖见 UI_HIGH_RISK_CHECKLIST.md |
| 制作质量 | 原五项缺陷的修复通过。测试后 Runtime 仍有修改，最新工作树需要受影响回归，不能自动沿用旧通过 |
| 实际运行 | 独立核对 root 的 PlayMode04 13/13 与 EditMode03 268/268 原始报告；审查者另实跑 .NET 预览检查 9/9。Windows/性能未通过，见 UNITY_RUNTIME_REVIEW.md |

最新正式证据结论见 `UNITY_RUNTIME_REVIEW.md`；下面第二、三轮“待验/开放”文字作为修复前历史保留，不再代表最新状态。

## 第二轮源码复核（修复后，实际 Unity 回归尚待执行）

生产文件快照见 `ui-followup-source-manifest.json`。对新增 `FailureAndInputPlayModeTests.cs` 和真实 JsonUtility 的 `AtomicSaveStoreTests.cs` 已完成只读测试质量检查，没有以测试文件存在代替测试执行。

| 缺陷 | 修复源码复核 | 运行证据状态 |
| --- | --- | --- |
| RV-UI-01 | GameApp.Refresh 59 行在菜单/空 State 分支也显示 SaveFailure；StartNew 94 行与 BeginTutorial 100 行保护待保存候选。针对首次失败、两条替换入口、原种子重试的测试已经编写 | 待 Unity 真运行 |
| RV-UI-02 | LogCommand 265 行捕获 IOException、UnauthorizedAccessException、SecurityException，保留警告；新测试验证存档成功、UI 刷新且只读日志未变化 | 待 Unity 真运行；异常类型此前 Windows IO 已复现 |
| RV-UI-03 | BuyFooter 调用 PurchaseError 并显示原因、禁止确认。槽满/同 ID 新测试不会通过金币充足绕过资格 | 待 Unity 真运行；更多目标边界已有核心层测试 |
| RV-UI-04 | ProjectSetup 74 行设 sendNavigationEvents=false。新测试向 InputSystem 注入真实 KeyboardState(Key.Enter)，验证序号只加一、得分增加且不打开选中 Pause；此测试不是直接调用提交处理器 | 待场景重新生成及 Unity 真运行 |
| RV-UI-05 | 当前 Gameplay/ScorePanel 仍没有上一手快照入口，永久效果仍未显示 | 保持开放 |

本轮阅读的持久化新增测试分别校验真实 JsonUtility 往返、有效 hash 的语义坏档、schema/hash 损坏、备份恢复后写入与未先 Load 的直接写入、双文件损坏、历史 null 组不影响后续随机与计分。隔离目录是每个用例新建的 temporaryCachePath 子目录，不覆盖正式存档。覆盖设计合理；原生序列化与 Unity InputSystem 的实际结果仍以 root 执行的原始报告为准。

## 第三轮：RV-UI-05 修复复核

`GameApp.Gameplay` 新增 `PreviousSubmission`（审查时 195 行），直接读取 `lastSubmission.snapshot / breakdown`。当前手空预览仍走独立空值分支；没有将上一手得分混入当前正式预览。

`GameApp.Modals.ShowBreakdown` 查找最近一条 Submit 回执，并仅当展示快照的 handId 等于 lastSubmission.handId 时附加该回执 effects。根据 `GameSession.Submit` 每次原子更新 lastSubmission 并在同一事务保存回执的约束，历史来源一致；后续 Roll/Confirm/Reorder 清空 lastEffects 也不会丢掉该 Submit 回执中的效果。EffectText 中 C07 的 otherDieId→dieId 与核心 source→target 方向一致，C15 使用真实目标 dieId/faceId。只读打开明细没有发出核心命令，也不重算旧手。

`GameApp.Send` 仅在 accepted 结果之后取 result.receipt.effects 显示复制/染色/损坏/临时骰到期；失败路径不会先播放成功效果。新提示不是重新应用永久改造。保存重试旁路的展示需实际 UI 回归，列入清单。

RV-UI-05 **修复源码通过，运行项待验**。绑定快照见 `ui05-source-manifest.json`；本次只对上述修复逻辑作结论，同期其他新增三维预览/动画代码不由此自动获得通过结论。最低回归应为：C07/C15 未达标提交→下一手 Roll→查上一手→重载后再查，核对旧分数和源/目标不变；打开当前手 Expanded 不得显示上一手已执行效果。

## RV-UI-01 — 新 Run 首次保存失败没有重试入口（P1）

发现位置：GameApp.cs `Refresh` 原 59–60、72 行及 `StartNew`；GameApp.Modals.cs `SaveFailure`。

`Refresh` 在 `inMenu` 或 `ViewState == null` 时执行 Menu 并立即返回，保存失败弹窗只在方法尾部显示。首次 NewRun 写入失败后，Core 保留候选而未发布 state，所以 UI 必然进入无 state 的早退分支，玩家看不到“重试保存原结果”。再点新挑战会重建 GameSession，丢失待保存快照。

复现建议：用占用/只读目录阻断首次 active-run.json 写入，点击新挑战；确认候选留在 Session.HasPendingSave 且仍可找到 RetrySave。恢复写权限重试，必须保存原种子/序号且不重新抽数。待保存期间 StartNew 与 BeginTutorial 不应替换 Session。

修复建议：对菜单和无 state 分支也覆盖 SaveFailure；阻止绕过待保存状态的 Session 替换入口。

## RV-UI-02 — 日志文件只读可打断已成功操作的展示（P2）

发现位置：GameApp.cs `Send` 原 107 行与 `LogCommand` 原 264 行。

Send 在核心成功保存之后、Refresh/动画之前调用 LogCommand；LogCommand 只捕获 IOException。真实 Windows 下对只读文件执行 File.AppendAllText 抛出的是 UnauthorizedAccessException，本轮已实际验证该异常类型。因此 active-run.json 可正常保存但 normal-actions.jsonl 被设只读时，投掷已扣次数并写盘，UI 却在刷新前异常退出，玩家仍看到旧结果。

复现建议：启动正常 Run 后把 normal-actions.jsonl 设为只读，再投掷；动作仍应刷新为已保存结果，日志告警不应让 Send 抛出。测试须在 finally 恢复文件属性。

修复建议：将日志写入明确视为非阻断步骤，至少捕获 UnauthorizedAccessException；不能吞掉核心保存失败。

## RV-UI-03 — 购买确认只检查金币，没有预先显示目标/资格无效（P2）

发现位置：GameApp.Shop.cs `BuyFooter` 原 93–98 行。

确认按钮只使用 `ViewState.gold >= price`。已有 `Session.PurchaseError` 未被 UI 调用；满槽、已有相同颜色/金层/玻璃、购买 C09 后不合法的顺子强化等仍显示可购买，只在点击后由 Core 拒绝。金币正确保留，但购买前的合法目标/资格反馈不满足规格第 18、26 节。

复现建议：在金币足够时查看同色染色目标、最后一颗永久骰的删除服务、满槽控制器、C09 后已展示的顺子升级。应在确认前显示具体原因并禁止确认；卖出 C09 后资格恢复，原未售商品再次可购，不补货。

修复建议：BuyFooter 读取 `Session.PurchaseError(offerId, dieId, faceId)`，展示原因并决定按钮状态；执行层继续独立校验。

## RV-UI-04 — Enter 同时由默认 UI Submit 和玩法快捷键消费（P2，需实机确认具体表现）

发现位置：ProjectSetup.cs 原 74 行、GameApp.cs Update 原 253 行；UiKit.Button 未关闭导航。

已检查本地 Input System 包原源码：DefaultInputActions 的 Submit 绑定 `*/{Submit}`，Keyboard.enter 的 usage 为 Submit；InputSystemUIInputModule 在 EventSystem.sendNavigationEvents 为 true 时向选中对象发送 submit；uGUI Button.OnSubmit 调用 Press。与此同时 GameApp.Update 在玩法阶段直接 Enter→提交。

因此同一按键有两个独立消费者。鼠标点击后很多按钮因 Refresh 被销毁，会掩盖问题；模态关闭后仍选中底层 Pause 按钮，或用方向键选中按钮时，双入口仍存在。

复现建议：有已锁骰时打开 Pause，用 Esc 关闭，然后 Enter；另用键盘导航选中 Roll 后 Enter。验收同一按键仅执行一次设计动作，不激活旧选中按钮；模态期间不提交玩法。现有仅调用按钮处理器的测试不能覆盖此路径。

修复建议：鼠标优先、独立快捷键方案可禁用 EventSystem.sendNavigationEvents；或将输入统一消费，保证默认 UI Submit 与玩法 Enter 不重叠。

## RV-UI-05 — 非过关提交后无法查看刚才的实际结算与永久效果（P2）

发现位置：GameApp.cs Send / Gameplay / ScorePanel；GameApp.Modals.cs ShowBreakdown。

未达目标的 Submit 进入新一手后，Refresh 的预览为空；Gameplay 没有 lastSubmission 入口，仅 Settlement 和 RunEnd 能打开之前的快照。C07/C15 的 lastEffects 也没有在 UI 呈现。玩家无法在普通手与手之间核查实际分数及刚发生的复制/扩散源目标，影响规格第 19、28 节要求的可解释性。

复现建议：不达目标地提交第一手，核对是否可查看该手 score、贡献、倍率；另触发 C07/C15 后核对目标身份与已经提交的永久变更。界面应读已保存快照与事件，不能重新触发效果或重算旧手。

修复建议：Gameplay 常驻上一手得分/明细入口，展示关键已提交 EffectRecord 的源、目标与变化；保留空的当前手预览，不把两手数据混合。

## 已检查且暂未发现缺陷的边界

- 种子解析通过 GameRng.TryParseSeed，空值生成并保存 uint；拒绝后不启动 Run。
- 教程使用单独 GameConfig 与 MemorySaveStore，目标 50，返回菜单重新读取正式存档；不把固定练习骰面写入正式活动档。
- pending 锁定、重排与后端同一命令入口；UI 没有普通解锁动作。拖放只交换数组成员，后端再次校验集合。
- 空预览提前返回；ShowBreakdown 对 null 快照/明细有 guard；冻结快照提供逐骰永久数据。
- 物理 faceId 决定朝上方向，遥控只覆盖该物理面本次显示 pip；并未转到另一张永久面冒充遥控。
- 单 dieId 对应一个表现场景对象；锁定移动已有视图，没有新增可计分实体。
- 金色边框与红蓝点数独立；永久面概率预览不使用当前遥控值。
- 本轮没有据源码推定三分辨率、最终材质/面朝向、性能或动画品质通过；这些保留给真实运行检查。

简化滚动当前由 DiceTable 程序化播放，未接入已交付 Roll_A / Roll_B FBX，且每次 Refresh 先对齐结果姿态；这属于需要明确记录的表现简化，不能冒充已按第 29 节完整 Blender 片段链路和衔接验证。用户允许适当开发美术，具体是否作为本轮延期项由主协调者据最新授权记录。
