# 七项返工与交易时钟观察复核 — 2026-11-01 08:11 UTC

结论：原七项失败在本批真实运行中已修复，限域通过；交易录制器的时钟拥有者定位缺陷也有两条实际购买录像支持关闭。**U05 强化结果在 1080p / 中文 150% 下另有真实文字越界与重叠，保持 REWORK。** 不据本报告签完整回归、源码冻结、Player、性能或整版验收。

审查者 `/root/v07_spec_audit` 仅只读源码、现有报告、截图与录像；未启动 Unity 或修改生产。输入为 art0.7.0 / p0.6.0 / schema4；原规格 `ART_SPEC.md:274,424`。原 `075917` 全套 152/145/7 与 `080637` 七项复现失败仍保留。

## 实际检查范围与绑定

- `nunit-20261101-081107.xml`：08:10:46–08:11:07 UTC，七项全部 Passed、零跳过，21.408 秒。XML SHA `356e2b335309772ae8a27bd3ae09f897f8ee3576afea001d000d9e768b8ba003`。
- 归档七个隔离目录中的 34 文件全部核对大小与 SHA；13 张 1920×1080 GPU PNG **逐张实际打开**。测试 Setup 明确将 `art07.textScale` 设为 100。
- binding 的 1358 个源文件在测试起止完全一致，本次复核时实际文件也全部匹配；五程序集起止 SHA/MVID 一致。Runtime MVID `6a68fcb9-848d-429a-ac05-c25d6f6bfd8e`，Core MVID `8f690e0d-a075-4b72-98e4-2cb64ced2fa3`。
- 本批是修复后的未提交工作树证据。录像 BuildIdentity 仍写 `86d740c`，不能以该标识冒充全部字节等于旧提交；本报告使用实际源码 SHA、NUnit binding 和 MVID 限定适用源。
- 详细输入、13 图 SHA、实际时钟日志、录像及另七张实看关键帧 SHA 在同名 `.bindings.json` 与缓存 `artifacts/cache/v07-seven-fixes-review/checks.json`。

## 七项修复符合性

| 用例 | 独立核对与本批结果 |
| --- | --- |
| UPGRADE ActualCeramicAndSingleQuestionFlipRenderBeforeMiddleAndAfter | 唯一 Canvas 直属 `Art07TransactionMotion`，真实 owner 的 playing/kind/duration/elapsed 相同。0/.175/.475/.80/.99、原 420/360ms、材质参数、三物理面、问号朝向、GPU 差分、mesh/transform 与 state/save 断言未减少，本批通过。 |
| UPGRADE FastReducedPauseAndSkipKeepCommittedStateAndResetSurfaces | 原 Fast150/Reduced120ms、暂停冻结、Skip 释放、无残余 renderer、状态及文件不变全部保留并通过。 |
| CoreLoop ClickLockReorderAndSubmitMatchesPreview | 新 helper 从真实源点拖至目标槽右半、经过 drag 与可见插入线后结束；`[D001,D002]` 与持久化顺序、预览和提交得分原断言均通过。 |
| ReloadDuringRollRestoresLocksOrderAndAcceptedFaceResults | 同样以合法插入落点完成重排，仍在 Roll 忙碌中重载；完整 StateJson、`[D001,D003]`、剩余投掷数和物理朝向断言保留并通过。 |
| RealKeyboardTenDiceMappingModalSeedFocusAndHeldKeys | 三处 L 后均已有原 bounded `WaitForIdle`，包括关闭 Pause 后第三处。真实 Dynamic 输入、十键映射、锁后可见池映射、破碎后 D006、模态/输入焦点抑制、held R 与 held Enter 单次提交、精确收据均未移除并通过。 |
| PooledRefreshKeepsNodeIdentitiesAndResetsScrollInputAndSlider | 只增加三列抽屉的真实 `FullScoreDetails` 入口。两次预热后原 12 次循环，节点身份、滚动范围/位置/速度、滑块 .45 填充、失效监听清理、caret/focus、状态稳定断言全部保留并通过。 |
| V06 RemoteThirtyTwoTargetsScrollAndKeepExactlyTwoWithoutOverlap | 只把标题期望对齐已实现短入口“全部 32 件 >”，仍拒绝 `/`；没有更改 32 目标、真实滚轮/点击可达、只选两颗、不替换第三颗、只提交一次、两值 2/5 和消费断言，本批通过。 |

六条 `UPGRADE_CLOCK` 日志均为 `HUD/Art07TransactionMotion`、elapsed0、`otherPooledClocks=1`：Normal0.42/0.36、Fast0.15/0.15、Reduced0.12/0.12。说明不是通过移除冻结背景快照来避开复现条件。

## 13 张 GPU 图的独立视觉结论

U05 五图为 p0/p18/p48/p80/p99：固定三面立体姿态，奶油色向冷青色材质过渡可见，点阵与前后文字保持一致。U07 六图含 p0/p18/p48/p80/p99 和 question-hidden 反事实：1 点 → 正向且不镜像的问号 → 2 点，隐藏问号图无残留旧点，未看到旧新点阵叠成假值；Fast/Reduced 的两张问号图也正确显示。100% 本组未见新增文字遮挡。

这些是手动组件时钟采样后的真实 GPU 图，非连续实时性能结果；不能据五个采样点声明所有中间帧或所有字号通过。

## 两条 owner smoke 连续录制

`artifacts/cache/v07-upgrade-owner-smoke-r01` 的 setup 为 1920×1080 / 字体150%，真实生成调试商店、经公开选目标与确认购买。两个报告的 Runtime/Core MVID 与上述测试相同；各自 169 条 C# /配置快照实际 SHA 已核对，共用 manifest SHA `7c48358f76b00319955f955725dc567e46468770186bae8ecc584b558a3b9827`。具体条数以 bindings 的实际计数为准。

| 录制 | 验证 |
| --- | --- |
| GlassDie-normal | 72 PNG 全 SHA/尺寸、索引和 Unity 帧连续；MP4 用 FFmpeg 完整解码 72 帧，无错误；420ms / 26 ticks。实看 frame15、27、40、41，包含前、中、近终、清理后。 |
| RandomFace-normal | 68 PNG 同样完整核对；MP4 完整解码 68 帧，无错误；360ms / 22 ticks。实看 frame15、25、36，1点、问号、2点符合真实收据。 |

每段单一 Buy 的 commandId、stateHash、sequence、金钱 110→109 对应收据；lead 和已提交段分别保持各自 state/save hash，提交后不再推进规则。最后 snapshotGraphics0、previewInactive、playing/busyFalse；普通存档不变、偏好/时间恢复均为 true。`actual-clock-owners.json` 实际列出隐藏 `SettlementBackdrop07/FrozenUiGroup0` 与独立 `HUD/Art07TransactionMotion`；前者 duration0、不播放，后者持有实际交易 0.36 结束状态。因此共存观察定位缺陷可限域关闭。旧递归 SingleOrDefault 不再用于此位置。

此录制采用既有 fixed60Hz 演出 clock，是实际渲染序列，**不是 60fps 性能测试**。仅两条代表交易路线，不替代全部交易媒体或新冻结版全套。

## 新发现 UO01：U05 的 150% 文本越界与重叠

真实原图：`artifacts/cache/v07-upgrade-owner-smoke-r01/GlassDie-normal/frames/frame_000040.png`，frame15/27 也复现。当前 `GameApp.Art07Effects.cs:559–568` 的 `PhysicalUpgrade07` 框为 x392/y512/w1496/h416；`Before` 固定 y194/h194，`Result/After` 固定 h218，`Result/Current` 固定 y242。U05 比单面 U07 多一行“骰体”。

- 左 `Before` 末行“物理权重 1”越过金框底 y928，约 ROI x405–650 / y914–950。
- 右 `Result/After` 末行与 `Result/Current` 首行重叠，约 ROI x1410–1710 / y750–815；frame40 全显时最清楚。
- U07 frame36 未见这两处问题，100% 的七例图也没有；不能仅用 U07 或默认字号替代 U05 大字体验收。

已向 root/art 提供完整路径与节点定位。修复应保留字号、完整物理面/权重/当前投出信息和真实收据语义，按实际 preferred height 安排垂直空间并重验受影响画面。原图/录像保持历史失败，不覆盖或改成 passed。**owner 观察修复通过与 UO01 制作质量返工是两个独立结论。**
