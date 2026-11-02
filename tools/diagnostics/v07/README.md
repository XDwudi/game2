# v0.7 Windows 资源与启动隔离诊断

这两个工具用于同一冻结Windows构建的资源/闲置采集和四项启动参数拒绝检查。默认只读计划，只有显式 `--execute` 才启动Player。它们不封包、不发布、不联网，也不将资源诊断、Editor结果或参数拒绝检查当作180秒正式性能、原生显示、M5或整版验收。

需要Python 3.11+、本仓库完整源码/工具/证据，以及Unity实际生成的新Windows构建。实际执行只支持Windows；无需第三方Python包。先完成新源冻结、同源Unity完整回归和构建gate；旧构建或未冻结修改会被拒绝。两个脚本均按自身 `parents[3]` 找仓库根，保留本目录层级运行。

## 资源与20秒闲置采集

从仓库根运行，先查看计划：

```powershell
python -B tools/diagnostics/v07/run_resource_diagnostic_v3.py --build-report evidence/v07/unity/build-windows-<新时间戳>.json --directory artifacts/cache/v07-resource-run-<新时间戳>
```

核对新构建后，在同一命令末尾加 `--execute`。保持Player前台可见、鼠标静止，不操作或最小化。默认外层限时270秒，`--timeout`仅允许240至300秒，不改变探针内部采样长度。

实际命令固定使用 `--art07-resource-diagnostic <新目录>/diagnostic`，1920×1080窗口、日志放在外层。探针独立访问既有页面和十骰场景，采集资源清单与20秒idle；详见[原规格](../../../docs/specs/v07/ART_SPEC.md)。没有正式verify、pacing或Profiler参数，不调用系统显示设置。

执行前验证当前冻结gate、源码/配置/黄金集、Windows构建报告、实际boot.config GUID和Runtime诊断入口。EXE、Runtime DLL、boot.config和build-report的SHA在预检查后、启动前再次核对；前三项还必须与完整before-build清单一致。实际报告再次核对非Editor、buildIdentity/GUID/config SHA、分辨率/窗口模式和隔离保存位置。

输出目录必须是本仓库 `artifacts/cache` 下不存在的新目录；拒绝复用、越界和祖先reparse/link。Player自行创建diagnostic子目录。外层包含Player.log、process-output.log、process.json、external-guards-before/after.json和runner-summary.json；子目录原样保留resource-report.json、两份CSV及隔离存档。

退出0表示只读计划或 `diagnostic_completed_not_acceptance`；1表示运行/诊断/守卫失败或未完成；2表示执行前拒绝。原报告status、error、phaseHistory与scope不改写；JsonUtility的null或空字符串error均表示无错，非空错误仍失败。报告中的 `wholeReleaseAccepted` 和汇总的 `formalPerformanceAcceptance` 始终false。工具不自动裁定纹理/显存预算、idle零分配、最高Combo或正式帧时门槛。

## 四项坏参数拒绝

使用同一新构建与另一个全新目录：

```powershell
python -B tools/diagnostics/v07/run_bad_arguments.py --build-report evidence/v07/unity/build-windows-<新时间戳>.json --directory artifacts/cache/v07-resource-negative-<新时间戳>
```

默认只读计划；加 `--execute` 才执行以下四项。每个进程最多45秒，首个非预期结果即停止，剩余项明确未运行；这不缩短有效资源诊断或任何正式采样。

| 案例 | 输入 | 必须观察 |
|---|---|---|
| missing-path | 资源flag后无路径 | `requires one fresh absolute output directory.` |
| nonempty-path | 新路径已有sentinel.txt | `Evidence directory must be fresh or empty.`；输入目录逐SHA不变 |
| mixed-verify | 资源flag与合法normal/debug轨迹、独立旧验证输出路径混用 | `Resource diagnostic cannot run with verification or pacing modes.`；旧路径不创建 |
| mixed-pacing | 资源flag与帧率实验、独立旧实验输出路径混用 | 同上；旧路径不创建 |

每项还必须自然exit1，并在Player.log中出现具体拒绝文本和Art07ResourceDiagnostic来源；timeout、缺程序、其他退出码或混入crash不能算预期拒绝。旧验证/实验目录、日志标记、采样report/CSV不得出现，被拒资源输入应保持不变。两旧入口在创建host前会先创建各自输出目录，故合法旧参数对应目录不存在及无旧日志支持early-return生效；这里不声称枚举了运行中GameObject。

每例保留日志、process.json、guards-before/after.json、case-result.json及适用的sentinel。总目录还保存suite前后守卫、输入轨迹SHA及bad-arguments-summary.json。`passed=true`只指四项明确拒绝及隔离检查均满足，整版接受仍为false。

## 共同守卫与边界

- 完整Build树每个文件计算字节数和SHA，包括backup/未知文件；普通 `LocalLow/XDwudi/DiceCraft P0` 也做完整文件守卫。不沿reparse/link读取；不保存或打印存档正文。变动记失败，不回写或恢复用户数据。
- Registry只以KEY_READ访问 `HKCU/Software/XDwudi/DiceCraft P0`。先枚举名称，再仅读取 `art07.*`、`fast/reduced/shopTab/volume`及单列的Screenmanager键（支持Unity哈希后缀）；类型和值仅用于SHA，不输出原值，不访问Editor/其他产品键，不写Registry。
- 游戏偏好变化导致失败。Unity Screenmanager引擎自存变化单独记录，不混入游戏键守卫，也不是显示验收。C#探针可能按真实UI合同保存偏好、登记本次clock再清理；外部前后哈希不代表“从未写过偏好”。
- 仅持有本次Popen句柄；超时、异常或中断时只终止该句柄，最多额外等待10秒，保留pid/exit/termination事实。不按进程名杀进程、不控制其他Player/Editor、不覆盖失败目录。
- 日志和证据全部留本地。坏参数driver固定校验其复用wrapper的已审SHA；改wrapper必须显式重新复核与更新绑定，不能无提示换实现。

## 自包含工具检查

```powershell
python -B -m unittest discover -s tools/diagnostics/v07 -p "test_*.py" -v
python -B -m unittest discover -s tests -v
```

本目录测试只使用临时合成文件/Mock进程，不读写真实Registry，不启动Player或Editor；临时目录在本测试目录内创建并清理。18项基础wrapper、3项JsonUtility空错误兼容、8项坏参数driver共29项。`test_bad_arguments.py`仅复用本目录 `test_resource_runner.py` 的隔离fixture，无cache旧实现依赖。合成通过不是实际Player或平台通过。

源码范围与真实最新结果见[STATE](../../../docs/STATE.md)。运行后应审原始资源清单、CSV、错误日志和守卫；不可只依据退出码给资源或整版签署通过。
