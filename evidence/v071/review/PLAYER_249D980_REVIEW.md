# v0.71 Windows Player 独立复核 — 249d980

审查者：`/root/project_audit`。采样期间未访问正在写入的证据目录；收到运行完成通知后，只读检查报告、原 CSV、全部回放、构建字节、日志、存档隔离与实际 PNG。没有操作 Unity、启动 Player、修改生产文件、改写原始证据或调整验收门槛。

**结论：该 Windows 构建实际启动并完成了限定范围的玩法 / UI / 回放验证，以及独立的显示设置崩溃恢复；完整 Windows 验收未通过。** 原生独占显示在两次真实运行中失败；独立延后显示检查的流程虽然完成，180 秒性能仍超过原 16.7 ms 门槛，并存在 GPU 采样覆盖缺口。保留 `failed_or_partial_no_full_acceptance`，不能将四次运行拼接为 strict-full 通过。

## 源与实际二进制

- 源：`249d980f2ae3615d573df089403f298a6713ae96`；应用 `p0.7.1` / 美术 `art0.7.1` / 规则 `p0.6.0` / schema `4`。
- Windows 构建 GUID：`2af8d019326049369f05314a26d58bcc`。
- 构建报告：`evidence/v071/unity/build-windows-20261109-010717.json`，StandaloneWindows64、固定 Windows_x64 Profile、Unity 6000.6.7f1、Succeeded、0 错误 / 3 警告。
- EXE SHA256：`3726509af94bdacbfe23f85da13109a7e70fb7d61913506e98690bd3208b2fd7`。
- Player Runtime SHA256：`a7561fdafbce5eb4acdc41689ed91e116ba5f34f7e1833878668bee06f6d9353`；MVID `3d281b2d-aef0-45d8-ba0c-45d0ed0ee96c`。这是 Player 编译产物，不能与 Editor 的 Runtime MVID 混用。

本次独立重哈希 **305 个构建文件**，完整路径 / SHA 与 runner 运行前清单一致，runner 的运行后比较也为一致。三个主进程报告及恢复进程绑定同源身份和 GUID，主报告 SHA 与 serial-summary 中记录相同。对 Player PDB 审计中 Core 18 + Runtime 81 共 **99 份源**，重新检查当前源哈希及实际 DLL / PDB 哈希，零不匹配；本轮没有再次解析 Portable PDB，原解析记录由 `pdb-windows-249d980.json` 提供。这与此前独立 Editor 全量 1013 / 163 回归属于不同证据层。

## 四次真实运行分别结论

| 运行 | 真实结果 | 本报告的解释 |
|---|---|---|
| strict-full，PID 3224 | exit 1，`V07NativeDisplay` 失败 | `UI display mode could not reach confirmation: 0`；原日志明确 `Failed to change display to ExclusiveFullscreen...reverting to FullscreenWindow`。这次完整路径没有通过，未完成其后性能阶段。 |
| native-deferred-independent，PID 51748 | exit 3，`Completed / budget_exceeded` | 新进程显式带 `--verify-defer-native-display`；可用作本轮玩法 / UI / 回放和失败性能诊断。其 `native-display-verification-deferred` 保持 `not_executed`。 |
| native-display-only，PID 48120 | exit 1，`V07NativeDisplay` 同样失败 | 独立复现独占模式不能进入确认；没有用此运行给主报告补写 native 通过。 |
| separate-process-recovery | writer PID 23488 → reader PID 36000；reader exit 0 | 限定的待确认显示设置进程中断恢复通过，不能替代完整原生显示矩阵或性能。 |

严格验证函数对所选 deferred / budget_exceeded 报告仍明确拒绝；仅带诊断候选参数的读取验证接受其作为受限参考。三个主报告及失败日志原件保留，未覆盖或合并。

## 全部回放、交互与隔离存档

逐条从输入 trace 与原始 JSONL 重新比较，而不是只读报告的 `passed`：

- **普通回放 145 / 145 步**：种子、命令类型、序号、阶段、分数、目标、金币、投掷 / 提交预算全部对应；每步 `savedStateMatches`、`renderedStateMatches` 为 true，`isDebug` 为 false。终点是输入预期的第 8 关 `RunFailed`，不是 24 关通关，不是玩家胜率数据。
- **调试回放 175 / 175 步**：命令与规则状态哈希逐条对应，终点第 24 关 `RunWon`；使用明确的 debug 输入，包含 24 关 / 23 商店 / 8 风险场景。不能冒充普通流程通关。
- 所选报告 **18 个唯一 UI 检查**均 passed，覆盖实际导航、五商店页签、骰面详情、滚动抵达第 32 骰、真实购买 / 刷新、控制器拖拽与取消、冻结和复制链等。50 个动画检查无失败项。操作来自自动探针，其中键盘取消使用注入设备的正常 Dynamic 输入路径；不声称进行了物理键盘、真实用户失焦或人工游玩验收。
- 运行报告的 `unityErrors` 为空，所选 Player 原日志没有发现 swapchain / timestamp 等额外报错；严格与 native-only 的明确失败日志另行保留。

隔离普通存档位于本次证据的 `normal-save/active-run.json`，调试压力存档位于独立 `debug-stress-save/`。实读普通存档 SHA256 为 `83157d904d242ffaf38036bef6cc0f7619784c5150ee4b495af0face94775c3c`，与报告在压力采样前记录的哈希一致；源码亦在调试压力后强制比较。没有将调试压力写入该普通存档。

用户真实存档目录的保护另有范围：runner 对六类普通 / 调试 / 显示 / 操作记录文件及其点后缀副本做前后 SHA 比较，最终 `ordinarySaveFilesUnchanged=true`。审查已核其代码和当前 12 个文件哈希，但 runner 没有持久化运行前哈希清单，**因此本审查不能独立重建真实目录的运行前快照**。此部分结论由真实 runner 前后比较记录支持，不将其描述为审查者自己完成了前后双快照校验，也不扩张为注册表 / 所有 PlayerPrefs 不变。

## 原 CSV 性能复算：未通过

参考报告为 `native-deferred-independent/player-20261109-012013-a25233c9/player-report.json`，SHA256 `d80997d7065ea1ce671b17af7356066036f1076d00975d42159a8f1c5e3316ae`。实跑环境：Windows 10 19045 x64、Ryzen 9 5900X、约 64 GB RAM、RTX 3080 10 GB、Direct3D11、Development Build、PC 画质、1920×1080 Windowed。通用 FramePacer 安装，vSync 0 / targetFrameRate -1；不据此宣称固定扫描输出或可见性证明。

10 秒预热后采样 **180.0039074 秒**，场景为 10 骰 / 8 提交槽 / 10 控制器 / 5 道具。预热与采样合计各执行 190 次投掷、重排、待选及清除；自动保存与真实 UI 预览保持开启。

独立读取全部 **10097 行 CSV**，只按原探针规则排除首个半帧，保留 **10096 个完整帧间隔**。帧号 5811–15907 连续，elapsed 严格递增并与每行 interval 相符，末行覆盖到 180.0011 秒；没有删除慢帧。重新计算分位数、均值、最大值及 10 类指标的样本 / 覆盖率，均与报告一致。

| 指标 | 独立复算 | 原门槛 / 结论 |
|---|---:|---|
| LateUpdate 帧间隔 P95 | **16.92579999999566 ms** | 要求 ≤16.7 ms，失败 |
| 平均 / P50 / 最大帧间隔 | 17.8277671 / 16.6666 / 78.4153 ms | 诊断数据，不替代 P95 判定 |
| GPU 有效样本 | 9225 / 10096 | 覆盖 **91.3728209% <95%**，证据完整性不足 |
| GPU 缺失 / 无效 | 871 个零值不可用 / 0 个负或异常值 | 原状态 2；缺口不插补、不以 profiler 备用计数冒充有效值 |
| Main Thread / GC / UI 指标覆盖 | 100% | 不能抵消 GPU 缺口或帧预算失败 |

CSV SHA256：`db69dd26c322d3fb828875daf2be1d9a4092640c1a22e9684b5fe46b50d3b224`。当前生产 CSV 校验器对历史 deferred 路径按原约定返回空结果，本次审查另做了上述完整复算，没有把空返回算通过。reported focusedSampleFrames 为 10097，但 CSV 没有逐帧 focus 字段，只能核数量边界；焦点本身也不等于窗口未最小化 / 可见的独立证明。未以平均 FPS、FrameTiming 的其他时钟、GPU 低耗时或硬件性能替换原 LateUpdate / 16.7 ms 条件。

## 独立进程恢复的限定通过

已检查 writer、ready-to-terminate、reader、recovery receipt、最终 display-settings 和独立读取验证结果。writer 在 1280×720 Windowed 为已确认状态后，实际切到待确认的 2560×1440 FullscreenWindow，写出 pending 哈希 `5d888a8cb2275ab508d3486263672e81844832526b90f1319a8cd6b65ae2ca44`。runner 观察到 ready 后约 78 ms 终止自己持有的 writer 句柄；新 PID reader 读取同一隔离路径，恢复到原 1280×720 Windowed，pending=false，exit 0。两个不同 PID、待确认哈希、源身份、GUID、恢复实际值一致。

此候选模式为 **FullscreenWindow**，不是已失败的 **ExclusiveFullScreen**；恢复通过不能关闭独占问题。它也不是 macOS 运行或真实用户切屏 / Alt-Tab / 设备拔插验收。

## 实际 Player 图与限制

实际查看 **7 张**所选进程的原始 PNG，元数据 / 像素尺寸 / SHA 已绑定：菜单1080、普通Selecting1080、达标后1080与720、8槽压力720、商店720、压力结束1080。不是把报告的115张已接受截图全部声称人工看过。

- Selecting1080、8槽压力720及压力结束1080中，卡牌 / 分数 / 骰池 / 提交标题与操作区未见新增稳定遮挡；最大容量的两标题保持离开盘槽线。
- 菜单版本 / 构建信息与说明可见；商店720有三件商品、购买价格、右侧详情滚动区、页签及离店入口。菜单和部分商店由本审查者参与制作，这只是作者观察，不能自签独立制作质量。
- `normal-qualified-continue-or-end-1920x1080.png` 捕到骰子从提交槽返回池的中间位置，右侧两骰一度交叠，金章尚呈实色条；后续同状态720图已经五骰均匀落位，永久达标勾保留。源码捕获仅要求 `!app.IsBusy`，随即 Refresh 后截图，而桌面布局缓动独立更新。**该1080图不用于证明稳定落位或达标章完整外观**；现有事实提示是过渡帧，单张截图不足以确认新的稳定布局缺陷，也不足以为整段过渡质量签通过。已向 root 如实报告，未删除或替换原图。

此前完整 Editor 回归的两张 Guide150 图由 root 另行实看并确认没有新增引导 / 主要按钮重叠；该交叉观察不当成本审查者对自己 Guide 实现的独立签署，也不把 Editor 图片冒充本轮 Player 截图。

## 交付解释

本报告与同名 `.bindings.json` 绑定 80 份证据 JSON / JSONL / CSV、原日志 / 进程记录、305 个构建文件及7张实际观察图。复核脚本在 `cache/v071/review-player-249d980.py`，仅生成审查绑定；没有重跑或修改 Player。

可以将该包标为已实跑的 **v0.71 诊断 / 开发候选**。Windows 性能与原生独占显示仍未通过；GPU完整性、物理输入 / 焦点事件、Mac原生实跑、人工风格与体验等缺口继续保留。完整自动 Editor 回归、成功构建、受限 Windows 玩法检查和崩溃恢复，各自有效但不能合并成整版 ACCEPTED 或发布批准。
