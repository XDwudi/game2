# v0.6 核心独立复核进度

审查人 `/root/v06_spec_review`，2026-10-31。当前是开发中分模块复核，不能替代最终 Unity、Player、Mac 实机、交付和人工体验。

- 原样生产配置和 160 黄金集与附件解析结果逐项一致；见 `production-data-comparison.json`。
- `managed-rules-run01`：1696/1696，通过。160 原始黄金、1024 额外复制/附魔/大整数差分场景、512 独立 Fraction 概率向量；计分附带输入不变、解释最终值及一次取整检查。
- `managed-rules-run02`：2051/2051，通过。额外覆盖数字 JSON 键保值与非法结构、C51/基金状态边界、纯非提交普通/调试附魔隔离、绑定失效与动态状态深拷贝、共享 C53 预览及全部普通/专用/替换池逐票 CDF/RNG。
- `managed-rules-run03`：2055/2056，通过 2055、失败 1。新增共享 owner 随机阶段、强制分支消耗、独立 K32、本次调试输入保留、普通检查点隔离、重复 requestId 原回执均通过。**失败为合法 debug C05+base10 结束达标关后无法进入商店**，`ControllerCandidates` 将已持有实验附魔交给正常替换池校验，抛出 Invalid current enchantment。已交会话实施者修复，失败完整保留。
- `managed-rules-run04`：2067/2067，通过。失败项已修复，并扩为全部六种纯非提交控制器 × base10/mult2 的 12 个合法调试组合进入商店回归；普通商店报价仍必须符合普通附魔池。
- `managed-rules-run05`：2074/2074，通过。新增实际文件 IO 的正常/只读调试往返、双向错误分区写入拒绝且原文件不变、有效校验和的错误分区加载拒绝、从错误分区活动文件恢复普通备份、错误活动文件不覆盖好备份、外部破坏使字节缓存失效、临时文件路径写失败仍能恢复原检查点。使用生产 AtomicSaveStore，但 JSON 适配仍为 System.Text.Json，不能替代 Unity JsonUtility 的实测。
- `managed-rules-run06`：2077/2077，通过。新增独立重放会话产出的两条轨迹：普通 145 命令（每步比较完整原回执、预算/分数/余额、恢复和重复请求，最后完整状态一致），调试 175 命令（24关、23店、8高压，每步比较三流 RNG、stateHash、恢复和重复请求，最后完整状态一致）。普通轨迹在第8关合法失败，不声称普通难度通关；调试轨迹用于流程覆盖，不声称平衡通过。另验证 Unity 可能生成的空 shop/hand/settlement 占位 DTO 在 canonical hash 和后续回执上与 null 同义；这是模拟占位结构的托管复核，实际 JsonUtility 回归仍需 Unity 报告。

每次报告有对应 `-binding.json` 和 `.log`，核对执行前后源 SHA 一致。run01/run02 绑定 18 个规则/配置/审查源文件；run03 将会话加入绑定，总 26 文件。run03 报告旧 scope 字样仍写 rules，它实际包含 5 个 SESSION- 前缀的会话用例；后续报告已更正，不修改留存的旧报告。

run04 绑定 26 个源/输入文件；run05 增加 AtomicSaveStore 和独立 PersistenceChecks，绑定 28 个文件。两次执行前后源均一致。测试产生的真实小型文件保留在 `disk-io/`，没有覆盖普通应用存档。

run06 绑定 34 个文件，包含两条轨迹、终态、会话源清单以及独立 TraceChecks。每步详情见 `managed-rules-run06.trace-details.json`。仍保留并明确区分已有真实 Unity 中间失败报告：EditMode 754/753/1、775/774/1 与 PlayMode 28/27/1，不能用托管通过覆盖真实引擎失败；对应修复后的真实重跑由 root 完成。

额外计分向量最高 116 位十进制数，包含三个 32 控制器极端场景；结果来自供应参考的跨语言差分，因此不声称全控制器算法是独立发明。512 概率向量为独立实现。输入自洽阶段另有 27018 个独立基础判型/乘区场景，见规格审查。

UI/会话接口发现单列 `UI_INTEGRATION_FINDINGS.md`。暂未签最终规格符合或制作质量结论；后续修复后重跑受影响检查，并等待实际 Unity/Player 证据。

最终冻结 `6f9e285432ab92e7304830a42c9e9e4cd74d97c3`：`source-close-review01.json` 逐项确认 run06 的 34 个输入源均未变化，因此 2077/2077 仍绑定当前核心。真实完整 Unity 最终 EditMode 776/776、PlayMode 28/28 已由 root 执行并经独立报告/发现清单/实际加载模块/完整源快照核对，见 `unity-evidence-review04.json`；这包含真实 JsonUtility 与输入焦点修复的全量回归，旧失败报告保留。最终 Player 和交付继续单独复核，详见 `GATE_REVIEW.md`。
