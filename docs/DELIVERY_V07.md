# P0 v0.7 交付草稿

当前状态：制作与验证中，尚无本稿确认的 v0.7 发行下载链接。本文预留交付入口，不能作为构建、平台、美术或整版通过记录。最终状态以 STATE、冻结 manifest 和实际证据为准。

本轮为美术与表现版本 art0.7.0，发行版本 p0.7.0；玩法配置 p0.6.0、存档 schema4 保持，Unity 6000.6.7f1。范围来自三份已归档输入及 [实施契约](tasks/P0-V07-ART.md)：统一主题、3D骰子、内容图标、22屏、计分动画、声音与辅助设置，不包含规则平衡调整。

计划沿用用户授权的 GitHub Release 三附件渠道：Windows x64、macOS Universal、M5验收工具包，存放于 Releases/p0.7.0/。实际下载链接、文件大小、SHA-256、源提交、构建GUID及完整Unity测试数量须由冻结产物生成后填写；当前不猜测或预填。旧 v0.6 发行和失败证据保留。

| 项目 | 本草稿状态 | 最终依据 |
|---|---|---|
| 完整Unity测试与源码/PDB绑定 | 4041766：999 EditMode + 154 PlayMode，零失败/跳过，独立复核通过；后续探针计数修复的新全量仍待跑 | [4041766完整回归](../evidence/v07/review/FULL_REGRESSION_4041766_REVIEW.md)，不替代新源/Player验证 |
| Windows真实流程、界面、180秒性能与原生显示 | 404实际145步状态核对完成，但动画覆盖检查失败；独立显示专项限域通过；新完整流程/性能/恢复仍待 | [显示专项实际复核](../evidence/v07/review/PLAYER_4041766_DISPLAY_ONLY_REVIEW.md)，不与主失败报告拼接为通过 |
| Mac Universal构建 | 404真实构建成功，0错误/75警告；封包与实机待验 | [实际构建报告](../evidence/v07/unity/build-macos-20261101-114045.json)，新修复后的构建仍待 |
| M5原生Arm64/Metal、封装与双向存档 | 待用户实际运行 | [M5执行说明](tasks/P0-V07-MAC-VERIFICATION.md) |
| 美术、动效与可读性 | 已完成指定场景独立复核；人工风格与体验仍待验 | [最终屏幕增量](../evidence/v07/review/FINAL_0F04E51_SCREEN_ART_INCREMENTAL_REVIEW.md)、[抓手/商店增量](../evidence/v07/review/FINAL_0F04E51_GRIP_SCREEN_REVIEW.md)、[九段演出](../evidence/v07/review/FINAL_0F04E51_CLIPS_REVIEW.md)、[POST独立复核](../evidence/v07/review/FINAL_0F04E51_POST_INCREMENTAL_REVIEW.md)；各自仅覆盖报告所列范围 |
| 声音 | 实际AudioMixer总线未接通，分类音量fallback可用；混音和声音听审待完成 | 不将静音录像、WAV库存或音量滑块视为正式音频验收 |
| GitHub上传、三包全量下载、新目录启动 | 待执行 | 远端摘要与本地完整恢复证据 |
| 工具离线检查 | 118/118；仓库确定性检查20/20 | [工具交接](tasks/P0-V07-DELIVERY-TOOLS.md)，仅工具范围 |

玩家操作说明随包附带 [PLAYER_README_V07.txt](PLAYER_README_V07.txt)。M5工具包包含同源Mac包、真实Windows报告/检查点与原规则输入，用户按说明在Mac运行后回传结果。即使自动检查通过，人工美术/体验和反向存档未完成时也不宣称整版 ACCEPTED。

当前媒体采集身份为 `0f04e51`；新的 `821ec59` 只修改验证探针与测试前置，产品UI、资源与演出不变。[精确差异复核](../evidence/v07/review/MEDIA_REUSE_0F04E51_TO_821EC59_REVIEW.md)允许按原已审范围复用媒体，不重标为新采集，也不替代新Player、构建身份或性能结果。

新增ART14三指定底色检查已实际采集并独立查看43图，覆盖567个绘制实例。[九宫格/角标](../evidence/v07/review/ART14_SLICES_REVIEW.md)和[透明纹理](../evidence/v07/review/ART14_TEXTURES_REVIEW.md)在各自边缘检查范围通过；深青底柔光的可见色阶保留为观察项。实际821ec59 Windows流程在商店入场时序检查处失败，现仅修复检查器的等待前置；新的完整验证和可封包结论仍待实际结果。

上述商店修复已在404实际Player通过五页签检查，后续发现并修正探针对同步减少动态投掷的覆盖计数；原失败继续保留。[资源证据索引](../evidence/v07/review/ART_RESOURCE_TRACEABILITY.md)链接可编辑源、许可、预设和实测；[821→404差异审查](../evidence/v07/review/MEDIA_REUSE_821EC59_TO_4041766_REVIEW.md)保留媒体原身份。实际显示专项的两种超出当前桌面能力的尺寸为not_supported，未算通过；没有以一次显示成功推断历史失败原因。

2026-11-01：向 `XDwudi/game2` 的 `codex/v07-art-and-ui` 推送完整源码、策划及内部测试/媒体证据被自动审批拒绝，命令未执行。用户既有授权涵盖发行附件；源码与内部证据的远端同步待明确确认。本地验证和封包继续，尚未创建本版远端发行。

若实际只能提供受限诊断候选，必须由真实报告生成单独 notice/manifest，明确保留原生显示或性能失败，不用旧版失败数字替代本版观察，也不降低Mac完整验收标准。最终发布说明应按真实结果重写本草稿。
