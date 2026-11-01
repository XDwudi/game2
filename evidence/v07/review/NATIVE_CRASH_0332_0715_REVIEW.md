# 两次 Editor 原生崩溃只读诊断

2026-11-01 UTC；独立诊断 `/root/v07_spec_audit`。只读现有 dump、封存日志、Windows 事件和两个指定 Unity 进程的查询信息；未附加调试器、重启/结束进程、改 Assets/Library/图集/驱动/安全设置。仅写本报告、bindings 与 `artifacts/cache/v07-native-crash-review`。当前源码检查点 `86d740cdcc3045605e41417dc0353d4cb4d76c60` 晚于事故；本文不是该提交的运行通过报告。

**结论：两次事故均有“AssetImportWorker 大量持有内存 → 系统提交内存接近上限 → worker 和主 Editor 在同一原生分配错误处理位置崩溃”的直接证据链。** 这已超出此前“可能为外部内存压力”的猜测，但尚未确认 worker 内存增长的具体缺陷、资源、单次请求大小或可靠修复。不能把重启成功、加载专项通过或 999 项 EditMode 通过当作根因关闭；152 项 PlayMode 的最终结果由 root 另行记录。

## 两份主 Editor dump 与栈

| 项目 | 03:32 事故 | 07:15 事故 |
| --- | --- | --- |
| dump UTC / 主进程 | 03:32:13 / PID 40096 | 07:15:56 / PID 23240 |
| 主进程启动 UTC | 10-31 18:28:00 | 11-01 03:33:23 |
| 异常 | 两者均 `0x80000003`，`Unity.dll+0x3832e73`，日志符号 `MimallocPrimErrorHandler`；不是直接访问违规异常 | 同左 |
| 触发上下文 | `MimallocAllocator::Allocate → VertexData::AllocateMemory → Mesh::Clear → VertexHelper.FillMesh → Canvas.ForceUpdateCanvases`；栈中真实调用者是 ScoreLabelsControllerOverflow 用例的 Click | `MemoryManager::Reallocate → … → GroupAtlasSprites → PackSpriteAtlases → OnEnterPlayModePreAwakeStatic`；处于进入 Play 前，不能归因于尚未执行的 GameApp.Awake/异步读档 |
| 系统提交量 / 限额 | 135,475,134,464 / 136,475,475,968 字节，**99.267%** | 136,298,926,080 / 136,475,475,968 字节，**99.871%** |
| 提交余量 | 1,000,341,504 字节（954.00 MiB） | 176,549,888 字节（168.37 MiB） |
| 可用物理内存 | 2.102 GiB | 1.252 GiB |
| 主 Editor PrivateUsage | 4.024 GiB | 4.927 GiB |
| 系统历史 PeakCommitment | 等于限额 | 等于限额 |

两份 dump 的 SystemMemoryInfo revision=1/flags=15、ProcessVmCounters revision=2/flags=31 有效。按本机 Windows SDK `10.0.22000.0/um/minidumpapiset.h` 的 pack(4) 布局读取；SystemMemory 中 BasicPerfInfo 与旧 PerfInfo 的四个内存计数完全相等。异常地址亦与 Application Error 事件相符。这些值来自事故时的 dump，而非重启后的采样。SDK 文件 SHA、所有字段、原始字节大小/SHA 与脚本均已绑定。

**分配请求大小尚未知。** 两份完整日志已逐行流式检索，没有包含“requested allocation / failed to allocate / out of memory”等请求大小文本；dump 中限定的 allocator 错误字符串扫描也未找到尺寸。`0x200000` 是 WithAvxXStateContext，未设 FullMemory；现有材料未经过带局部变量的原生符号调试，因此不将任意寄存器、系统余量或 worker 总占用冒充单次申请大小。两个 ErrorHandler 栈相同，也不证明两次申请的大小或上游缺陷相同。

## Windows 事件确认主要占用来自 worker

实际查询仅为两次 UTC 各前后 2 分钟的 System 2004 / Application 1000（初步另列 1001，不用于结论）。2004 原 XML 已归档，事件记录号分别 **314425** 与 **314434**。

| 事件时间 UTC | 系统提交量 / 限额 | 事件列出的主要进程 |
| --- | --- | --- |
| 03:32:06.0096389 | 136,419,205,120 / 136,475,475,968，99.959% | Unity 45536：53,630,738,432 字节；Unity 36888：51,841,703,936；主 40096：3,916,464,128 |
| 07:15:55.6748902 | 136,432,898,048 / 136,475,475,968，99.969% | Unity 42140：104,254,992,384 字节；主 23240：5,290,356,736；另一既有 Blender：1,228,517,376 |

事件与 dump 是不同时间的采样，数值不应强行相等。Application Error 的 PID 45536/36888/40096 和 42140/23240，均在相应事故秒附近记录相同异常码与 Unity.dll 偏移。

角色核验：

- 03:32 的 `Crash_2026-11-01_033210154` 同目录保存 `AssetImportWorkerHW1.log` 和 worker dump；worker dump 的 PID **36888** 与事件完全一致。日志 parentPid=40096。另 HW2 日志 parentPid=40096、启动 01:51:38，与事件 45536 的创建时间相配；这项为时间/父子角色关联，没有第二份 worker dump 独立确认 PID。
- 07:15 的 `AssetImportWorkerHW0-prev.log` 启动 03:33:37、parentPid=23240，匹配事件 42140 的创建时间 03:33:37.7161696。其 crash 栈明确包含 `ShaderVariantCollection::WarmupShadersImpl → EditorShaderLifecycle::ReloadAllShaders → RunAssetImportWorkerClientV2`；没有可取得的该 worker dump，PID 关联依据需保留这个区别。
- 当前 07:47:28 的 **39516** 已通过只读 `PROCESS_QUERY_LIMITED_INFORMATION` 查询明确确认 `-name AssetImportWorkerHW0`、`-parentPid 10504`、同项目、`Logs/AssetImportWorkerHW0.log`、`-force-d3d12`。没有输出许可/账户参数值。WMI 查询被拒绝；后续有限查询成功，未使用 VM_READ/WRITE、调试或进程控制。当前是角色快照，不是历史增长曲线。

## worker 日志中的增长

三份小 worker 日志已逐字节复制保存，避免下一次正常启动覆盖。以下是 Unity 原文 `Memory consumption` 指标，不与 OS PrivateUsage 混为同一统计口径。

| worker | 内存记录数 | 起始 → 末尾（日志原单位） | Domain Reload 日志数 |
| --- | ---: | --- | ---: |
| HW1 / 主 40096 | 152 | 0.60 GB → 39.04 GB | 64 |
| HW2 / 主 40096 | 152 | 0.59 GB → 40.50 GB | 64 |
| HW0 / 主 23240 | 306 | 0.59 GB → 79.04 GB | 137 |

末段多次 `Received Prepare`，伴随 `graphicsApiMask: 65536 -> 327680` 和 `number of updated asset objects reloaded=0`，内存继续上升；HW0 最后两条为 78.79 GB 与 79.04 GB。卸载数千个 unused Assets 后总量并未显著回落。可以据此优先调查反复 Prepare/域重载/Shader reload 的持有增长；**不能仅凭相关性认定 graphicsApiMask 差异本身就是根因**，也不能依据 worker 使用 D3D12 自动归咎显卡驱动。

只读检查当前生产：GraphicsSettings 的 `m_PreloadedShaders`、`m_AdditionalWarmupCollections` 均为空；Game 代码未发现主动 `ShaderVariantCollection.WarmUp` 调用。这不排除 Unity 内部 warmup 或其他导入/依赖路径，也不证明任何单个 shader 已免责。

## 工具与官方材料的边界

本机 Python、Windows 事件查询、已安装 SDK 头文件足以提取上述元数据。PATH 与常见 SDK Debuggers 目录未找到 cdb/windbg/dumpchk；没有安装 SDK。Unity 安装含本版本 `Unity_x64.pdb`，后续如需请求大小/完整 native heap 所有者可准备离线符号分析，但本轮没有声称做过此步骤。

MINIDUMP 头、异常和 stream 的含义按 [Microsoft header](https://learn.microsoft.com/en-us/windows/win32/api/minidumpapiset/ns-minidumpapiset-minidump_header)、[exception](https://learn.microsoft.com/en-us/windows/win32/api/minidumpapiset/ns-minidumpapiset-minidump_exception) 与 [stream types](https://learn.microsoft.com/en-us/windows/win32/api/minidumpapiset/ne-minidumpapiset-minidump_stream_type) 核对；详细内存结构以实际安装 SDK 为准。Unity [6000.6.7f1 release notes](https://unity.com/releases/editor/whats-new/6000.6.7f1) 的若干 memory/crash 项没有足够条件与本例匹配；[6000.5.0b8](https://unity.com/releases/editor/beta/6000.5.0b8) 曾记录 UUM-133236 的 worker/Shader Variant Collection 崩溃修复，但不是本次 6.6.7 问题的已确认编号或解决办法。不能因此直接升级引擎或套用其他 bug 的规避配置。

## 下一必要验证（未执行）

1. 当前完整 PlayMode 继续由 root 正常收尾；记录主 Editor **及其已确认 worker** 的 PID/创建时间/PrivateBytes、系统 CommittedBytes/CommitLimit 与当前操作。只盯主 Editor 的 3–5 GiB 会遗漏已证实的大头。不能因当前总量回落删除事故记录。
2. 完成当前批次后，如 root 正常保存并退出自己 Editor，核对其已确认 worker 是否一同退出及系统提交量的变化；这可验证资源回收路径，属于临时运行管理，**不是永久修复**。不结束既有 Blender、未知 Unity 或用户其他进程。
3. 随后的同源串行媒体/构建把 worker 内存纳入观察；若冻结源、无重编译仍持续增长，保留当次工作序列/图集和 shader 操作边界。只有再次需要定位时，另立隔离最小试验比较“连续无源码变化的 Play/退出”与“重复 Prepare/域重载”，保持相同源/工具/导入设置并逐阶段记录，而非在生产中删除 Library、关闭图集或改驱动碰碰运气。
4. 若增长复现，准备最小项目和本版本符号/worker dump 供进一步 native 分析，再决定工程修复或官方问题反馈。本轮已有证据不足以判定具体 Unity bug、项目资源 bug 或单次异常尺寸；不为结案猜测。

07:15 pending binding（07:14:29.8613906Z）保存 5 个程序集和 1,356 个文件；事故没有该批完整 NUnit 结果。03:32 原日志/栈与恢复后的 memory CSV 均保留；后者只能说明 PID23240 恢复初期状态，不能解释 PID40096 崩溃。313,469,892 字节大日志采用逐行流式检索与 SHA，未整段加载或输出；12,160,477 字节旧日志同样处理。原始含环境/启动信息的日志和 dump 保留在 cache，本报告只归纳必要字段；不将原始启动材料直接加入发行附件。
