# 长 Toast 返工独立复核

复核人：`/root/v07_shop_ui`。输入：art0.7.0 / p0.6.0；复核时间：2026-11-01 UTC。

**限域结论：此次普通长错误“摘要提示 → 玩家主动点详情 → 全文”修复在所列 Windows Editor 场景的文字、交互与只读行为复核通过，可以关闭此次长文自动弹出模态窗口的问题。Debug32 只读预估分暂遮是已记录的 root 工程 fallback，非用户豁免；不是全 Toast 系统或全版美术 ACCEPTED。**

独立性边界：复核人曾实现旧 Toast 基础排布与早期测试。本报告独立复核的是 `v07_art` 此次长文摘要、主动详情变更，以及 root 等按钮颜色过渡完成后再采集 GPU 对照帧的修复；不自签旧实现。短提示证据只作为本次整合回归背景。此次未修改生产文件、测试标准，未运行 Unity；真实运行由 root 完成。

## 依据与实际运行

[ART_SPEC.md](<E:/agent_workspace/gpt/game2/docs/specs/v07/ART_SPEC.md:415>) 规定 Toast 宽 480、最多两行、最多同时两条、两秒后淡出且不遮掷骰和提交。现有 root 工程排布在商店底部临时隐藏 ShopHint；Debug32 没有空位时使用单条纸签临时覆盖只读预估分数，保持动作区可用。这是已记录的 root 工程 fallback，并非用户亲自批准的标准例外，不构成用户豁免或整版通过。此次主动详情入口为 root 确认的工程变更。

| 真实运行 | NUnit 完整结果 | 本报告覆盖 | 源码快照 / 实际环境 |
|---|---|---|---|
| [044648](<E:/agent_workspace/gpt/game2/evidence/v07/unity/nunit-20261101-044648.xml>) | 3/3，失败 0 | 两项 Toast；另一个钱包测试不在结论范围 | 1280×720；1,286 个源文件；开始/结束源码与程序集一致 |
| [045449](<E:/agent_workspace/gpt/game2/evidence/v07/unity/nunit-20261101-045449.xml>) | 13/13，失败 0 | 两项 Toast；另外 11 项不在结论范围 | 1920×1080；1,288 个源文件；开始/结束源码与程序集一致 |

两次均为 Unity 6000.6.7f1、Windows Editor、Windows 10 19045、RTX 3080、D3D11。每档分辨率均包含 Normal / Reduced × 100% / 125% / 150% × MaximumPool / MaximumTable / Debug32 / Shop：24 个摘要与主动详情配对，另含一次暂停计时用例和 24 张短提示回归截图。

真实测试名称：

- `PLAY_ART07_ToastActualPaperGlyphsAndMainActionsAtThreeFontsNormalReduced`
- `PLAY_ART07_ToastPausePreservesVisibleTimeAndLongErrorsKeepCompleteDetails`

## 规格符合性与实际制作质量

| 项目 | 结论 | 证据与限定 |
|---|---|---|
| 长文不自动弹窗 | 通过 | 测试在主动点击前断言没有 Dialog / NoticeDetailBody；摘要实图仍为主界面。仅实际点“详情”后出现全文模态，关闭后回到主界面。 |
| 480 宽、最多两行与字号 | 通过 | 全矩阵检查纸签宽度与 1–2 行；摘要保留原错误原因前缀、显式省略号。摘要及详情按钮保持 `20 × FontScale`，全文为 `24 × FontScale`，没有缩字。完整字符串保留于详情。 |
| 操作热区与因子 | 通过 | 详情热区实际至少 40 px；TMP 字形在热区内，真实 GraphicRaycaster 命中。摘要不覆盖掷骰、确认、清除、提交、结束、手册、暂停、商店刷新/标签/离店及六个 Factor；聚焦描边也位于热区内部。全文模态打开时背景被模态阻挡属于正常行为，不计为背景因子仍可点击。 |
| 满池 / 满槽 | 通过 | 所看 720 / 1080 实图利用实际空位排布，正文、详情列与掷骰/提交按钮分离；150% 没有空白文字、中文方框或尾字裁切。 |
| 商店 | 通过 | 纸签处于底带，暂藏辅助 ShopHint；刷新、价格、五个标签与离店操作保持清楚。短错误同时两条有真实截图，长错误摘要与详情列不相互挤压。 |
| Debug32 极端布局 | 操作无碰撞通过；只读分数遮盖保留 | 纸签临时覆盖只读预览分数区域；1080 实图可见部分大分数字形从纸签周围露出，**不能同时完整读取该预览分数**。六个 Factor 与主要动作不被遮挡。这是已记录的 root 工程 fallback（2 秒显示加 0.2 秒淡出），非用户豁免、非整版通过；不宣称该区没有任何信息遮挡。 |
| 全文与恢复 | 通过 | 主动详情完整显示本次诊断字符串，正文、标题、关闭按钮无叠字或缺字；关闭后恢复主界面。专用保存失败恢复流程仍由原分支处理，不被此普通错误详情覆盖。 |
| 状态与存档 | 通过 | 48 个摘要/详情配对的 StateHash、存档 SHA 与 actionSequence 相同；96 次实际详情/关闭操作均保持只读；146 张截图的记录均保留对应状态/存档。没有以重放核心命令制造界面结果。 |
| 动态与时间 | 通过，限测试 | Normal 与 Reduced 均覆盖。暂停 2.4 秒期间隐藏提示，返回后仍具有可见时间，随后在可见期限结束淡出。此项按测试与记录核对，未对暂停截图做独立视觉签署。 |

长文压力样本是测试注入 UI 队列的诊断文字，**不是伪称真实玩法返回的错误**。短提示用真实被拒的 ConfirmLocks、无效重排，以及商店不存在商品购买/道具出售验证“操作后立即有提示”。测试注入仅检验文本布局、主动详情及只读行为，没有改动玩法数据。

## 实图与 GPU 核对

共绑定 146 张 PNG，独立实际查看 **54 张**：720 长提示 30 张（全部 24 个摘要 + 6 个详情）、720 短提示 4 张、1080 长提示 18 张（12 个摘要 + 6 个详情）、1080 短提示 2 张。逐张查看标记在 bindings，不将未查看图或单纯 NUnit 通过视作人工实图复核。

代表实图：

- [720 满槽 150% Normal 摘要](<C:/Users/f/AppData/Local/Temp/XDwudi/DiceCraft P0/DiceCraftTests_e144b0836dd844ebb89c407eadf1a5e2/extra-capture/019-toast-long-MaximumTable-150-normal-summary.png>)
- [720 Debug32 150% Reduced 摘要](<C:/Users/f/AppData/Local/Temp/XDwudi/DiceCraft P0/DiceCraftTests_e144b0836dd844ebb89c407eadf1a5e2/extra-capture/045-toast-long-Debug32-150-reduced-summary.png>)
- [1080 满池 150% Normal 摘要](<C:/Users/f/AppData/Local/Temp/XDwudi/DiceCraft P0/DiceCraftTests_cf10765b4955406f9a8849c6bcffa2d6/extra-capture/017-toast-long-MaximumPool-150-normal-summary.png>)
- [1080 商店 150% Normal 全文](<C:/Users/f/AppData/Local/Temp/XDwudi/DiceCraft P0/DiceCraftTests_cf10765b4955406f9a8849c6bcffa2d6/extra-capture/024-toast-long-shop-150-normal-details.png>)
- [1080 Debug32 150% Reduced 全文](<C:/Users/f/AppData/Local/Temp/XDwudi/DiceCraft P0/DiceCraftTests_cf10765b4955406f9a8849c6bcffa2d6/extra-capture/046-toast-long-Debug32-150-reduced-details.png>)

144 份 GPU 字形对照报告包含 **4,986 个非空白字形探针**。保留原阈值：前景变化阈值 2/255、隐藏帧背景噪声不超过 1/255；实际最大背景差为 0，每个被检查字形均有前景变化像素。

| 范围 | GPU 报告 | 字形探针 | 最少变化像素 |
|---|---:|---:|---:|
| 720 长提示 + 详情 | 48 | 1,988 | 9 |
| 720 短提示 | 24 | 504 | 11 |
| 1080 长提示 + 详情 | 48 | 2,016 | 20 |
| 1080 短提示 | 24 | 478 | 20 |

本次采样等待按钮 Select 颜色过渡完成，随后仍取三个真实 EndOfFrame 帧进行隐藏/隐藏/可见对照；没有通过提高噪声容差或降低前景阈值掩盖之前失败。原失败报告与原临时证据保留。

## 源码、程序集与证据绑定

[bindings.json](<E:/agent_workspace/gpt/game2/evidence/v07/review/toast-long-rework/bindings.json>) 包含两份 NUnit XML/绑定、来源快照、四个隔离目录、全部 PNG/sidecar/GPU 报告 SHA-256，以及每屏状态/存档/序列号、配对和实际按钮操作。

| 运行 | 来源快照 SHA-256 | Runtime MVID | Tests MVID |
|---|---|---|---|
| 720 | `452aadfdb4a741580fb232a0cd84993d74a8479cab8d76493b5affab20b0ba7a` | `7c581c97-44f9-49fa-b764-2daaf941fd99` | `96ce2763-cb35-44a0-96dd-9bfbd586c118` |
| 1080 | `9bc394f5963e5a946113444b899a1883dadf95c812f1fc3efd924120eb0bb1d0` | `a6f0330c-52f3-428b-8bb0-877c4b30e971` | `bb7831e6-a596-4d76-bada-4af5362989df` |

两次整个源集合和 Runtime MVID 不同，分别绑定，未宣称是同一个完整源码快照。此次相关四文件 `GameApp.Art07Toasts.cs`、`Art07ToastPlayModeTests.cs`、`Art07ScoreLabelsPlayModeTests.cs`、`GameApp.V05.cs` 的捕获哈希一致，并与复核读取的当前文件一致。Core MVID 均为 `8f690e0d-a075-4b72-98e4-2cb64ced2fa3`；Config SHA 为 `747ed1efc7bea15873f66a81eee5c7654971c21031f2a1517ce629adce7b6e6c`。

720 原 Temp 已有对应 `artifacts/cache/v07-wallet-toast-720-044648/` 副本，所有绑定证据逐字节相同。1080 绑定原隔离目录，归档由 root 后续完成。报告中的旧 BuildIdentity HEAD 不用于证明新发布包；本次依赖实际源快照、MVID、程序集 SHA 与截图 sidecar。

## 边界与交接

本次可以限域关闭普通长错误自动 modal、文字丢失或缩字、主动详情遮六 Factor 这组返工问题。未验任意长度多页正文的实际滚轮、Toast 音效、macOS/正式 Player，也不以这些 Editor 截图证明最终构建或性能通过。Debug32 预估分数临时遮盖作为已记录的 root 工程 fallback 保留，非用户豁免；复核人对旧基础实现的独立性限制也保留。

交 root 更新 STATE 并归档 1080 原证据；无需改动生产或门槛。本报告仅签署以上修复范围，发布与全版质量结论由总集成和独立验收继续处理。
