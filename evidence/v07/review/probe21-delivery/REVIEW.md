# Probe21 交付工具候选独立复核

复核者 `/root/v07_run_review`；对象为 `v07-probe21-delivery-readiness/FROZEN_CANDIDATE.json` 指定的 **9 个冻结文件**，不包括该目录早期的 cache runner / 旧 patch。只读生产工具与运行证据，写入仅本复核缓存目录；没有运行 Unity、Player、网络发布或修改旧 JSON。

**结论：冻结候选的规格符合性与工具实现质量检查通过，可按这9份SHA合并。** 这个结论只涵盖工具代码和下列确定性检查，不代表性能、原生设备、Mac、包发布或整版验收通过。

## 独立实际验证

- 初始候选工具测试独立重跑130/130，仓库测试20/20；初始额外负例发现coverage阈值漏洞，记录保留在 `coverage-negative.json`，没有用测试全绿掩盖问题。
- 作者修复并冻结后，独立再次执行 **131/131工具测试，0失败**；最终日志 `final-candidate-tests.log`。仓库 **20/20** 日志为 `independent-repository-tests.log`。
- 额外执行 `independent_negative_checks.py`：**17个独立检查场景**，以真实4f020f3严格报告为输入，变体仅在内存或本目录临时副本中。检查旧报告、CSV、两份回放JSONL前后SHA相同；9份候选SHA与冻结清单匹配。
- 实际旧strict报告可作为显式FAILED诊断参考，普通严格路径仍拒绝；产生的说明保留 `acceptanceStatus=FAILED`、`measurementCoverageStatus=INCOMPLETE_OR_UNAVAILABLE`、`fullWindowsPassed=false`、`fullReleaseAccepted=false`。
- 额外负例拒绝：降低coverage阈值、coverage>1、样本数与比例不符、有样本却available=false、focus超过raw行数、raw行数不等于valid+1、采样不足180秒、phase未完成、failure非空、实际配置hash缺失、UI失败、窗口真实高度不符、GPU缺样计数不符、P95与CSV不符、CSV缺失和CSV截短。

工具测试用例使用合成fixture/mock；没有把这些fixture当实际Player或硬件通过记录。真实strict输入只是只读消费历史已完成报告，不是重新运行当前Probe21。

## 选取与判定边界

新增完整strict诊断分支保留 `validate_report` 的版本/身份/轨迹、平台、UI、原生显示和状态检查，仅从诊断选择中剥离“必须整体passed”和“性能必须通过”两类失败，再明确要求 `Completed`、stressCompleted、`budget_exceeded`、exit3、failure=null、P95>16.7、采样≥180秒、原1080p及10骰/8栏/10控制器/5道具场景。性能没有被改称通过。

普通严格路径及Mac严格路径未修改为允许失败。显式 `--allow-failed-windows-performance` 必须搭配 `--review-candidate`；仅选择该诊断分支，不会成为完整Windows或版本接受。`review_reference_notice` 从已选择的同一完整报告提取原生事实，不合并另一次native-only。未执行检查ID保存在 `nativeChecksNotExecuted`；物理事件与跨进程恢复仍分别陈述。

正式 `tools/run_v07_windows_player.py` 先检查实际冻结gate/build GUID/输出/二进制probe标志与轨迹，再运行严格流程。完整有效strict预算失败直接成为明确FAILED参考；只有严格流程停在 `V07NativeDisplay` 且没有可用完整参考时才启动独立deferred。UI、回放、数据绑定或CSV失败不通过deferred掩盖。runner保留非零最终退出和false总通过状态；自动恢复仍调用既有真实独立进程helper，非模拟组件重建。

## 原始帧与focus边界

严格FAILED目录必须存在原CSV；包装、Mac比较、Mac入口和发布复核各自调用原CSV检查。验证包含行数、连续frame编号、elapsed/interval时钟一致、采样覆盖、剔除且仅剔除初始部分行后的P50/P95/均值/最大值、GPU状态码、raw缺样/无效计数与有效GPU覆盖。CSV SHA进入kit及manifest，并在publisher重核。

focus/gpu缺样等累计计数的边界修正为经核对的 `rawRows=frameCount+1`，没有更改原计数。真实strict为9,430条raw / 9,429条有效间隔，focused9,430可合法保留；超过raw边界仍拒绝。CSV没有逐行focus列，因此只能核计数范围，说明已明确这不是窗口可见性证明。GPU缺样和异常没有从统计中消去，完整性仍与性能失败分开报告。

**本复核发现并关闭的漏洞：** 初始candidate对 `requiredMetricCoverage` 信任报告数值，把真实报告.95改为.5时仍接受且称COMPLETE_AS_REPORTED；现strict验证固定.95、四指标样本/预期/available/比例必须一致且覆盖0..1，公告也只以.95判断完整。额外负例重验后不能再把GPU .8424说完整。原始漏洞记录未删。

这套CSV核验不是对所有Profiler字段逐项重算的声明；主要线程/GC/UI的样本计数和覆盖比例做一致性校验，精确持续时间重算针对帧间隔，GPU做状态/样本覆盖校验。性能根因和实际新性能结论仍由真实Player采样决定。

## 包、Mac与发布链

- package_v07：Windows报告必须通过诊断合同及真实trace，严格FAILED额外要求CSV；M5 kit带该原始CSV。配置、完整身份及现有构建gate没有取消。
- release_v07_manifest：严格FAILED状态与deferred/native-pending状态分开；保留原性能报告、raw SHA、逐项显示记录、false总接受。若未提供独立恢复，仍明确加入 `Windows separate-process display recovery pending`，没有从待验列表丢失。
- publish_v07_release：复核manifest与原报告的FAILED说明、原性能、原CSV完全一致；明确显示缺失GPU数量和覆盖状态。不把原生自动检查通过等同物理焦点/断屏通过；原发布显式flag和源/Git约束仍在。
- verify_mac与compare_directories：先校验Windows参考/真实trace/CSV，再按原Mac严格规则检查；Metal/Arm64、同probe/同源身份和回放比较未放宽。失败Windows不豁免Mac自身性能/原生条件。
- VerifyMac.command：移除对所有候选硬编码“原生P22未通过”的错误文案，按notice区分所选自动原生检查事实，始终说明Windows性能FAILED、Mac未测及整版未接受。
- Mac执行说明补原CSV和focus边界。通用 `PLAYER_README_V07.txt` 已要求读随包manifest/REVIEW_CANDIDATE，且保留Mixer回退与Mac人工待验，本次无需再复制或修改该文件。

## 限制与后续

本机未找到bash，**没有执行Mac launcher或作bash -n检查**；只审其Shell引用、分支、参数传递以及测试中的LF/BOM检查。Mac工具真实系统调用/签名/ditto/Metal由M5验收证明，不能以Windows Python测试代替。

合并后仍需真实新Player完成严格/恢复采样、三个实际ZIP与manifest校验、远程上传下载及新目录恢复运行。现有外传审批问题、本机AudioMixer缺口及人工体验状态均不由此次工具审查解除。
