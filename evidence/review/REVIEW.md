# P0 v0.4 独立规则与事务审查

- 审查者：`/root/spec_audit`（未参与 Rules、Session 或存档适配的实现）。
- 审查日期：2026-10-28，用户时区 America/New_York。
- 完整任务跨度：Git 基线 `be2be52` 至当前工作树；文件级精确输入 SHA-256 见 `review-results.json`。
- 权威规格：`docs/specs/P0_v0.4.md`，配置 `p0.4.0`，存档结构 2。
- 已读实现范围：`Assets/Game/Core/Rules/*.cs`、`Assets/Game/Core/Session/*.cs`，以及跨层保存边界 `Assets/Game/Runtime/AtomicSaveStore.cs`；同时检查对应 EditMode 测试。
- 写入范围仅本目录；未修改实现、配置、验收规格或黄金预期。

## 当前结论

| 维度 | 结论 | 适用范围 |
| --- | --- | --- |
| 规格符合 | 通过（本审查范围） | 计分、随机、概率、流程、38 内容、交易与幂等，以及已列出的保存恢复路径 |
| 制作质量 | 通过（本审查范围） | 两个实际复现的备份恢复缺陷均已由 root 修复并经独立复验；已读核心边界无未解决阻断缺陷 |
| 实际运行 | .NET 核心及真实 Windows 文件 IO 已执行；Unity 验收未完成 | 本报告不能代替 Unity EditMode、PlayMode、Player 构建及玩家体验验收 |

## 实际执行与证据

审查宿主 `ReviewHarness.csproj` 直接编译生产 Core、Session 和 AtomicSaveStore 源码，.NET SDK 10.0.403，实际运行时 .NET 10.0.14。文件写入使用真实 FileStream / Flush / File.Replace。为了脱离 Unity 编译，仅 JsonUtility 用 System.Text.Json 包装；这不验证 Unity 序列化器行为。

`run-01.log` 记录首次工具因用户目录写权限失败；后续把 DOTNET_CLI_HOME 指向本审查目录，未将工具失败计为测试通过。

- `run-02.log`、`review-results-before-fix.json`：52 黄金样例的全部 18 字段、拒绝命令不变、重复回执及恢复、保存失败冻结候选/RNG/事件、重复预览无副作用通过；备份保留的两个入口均失败。
- `run-03.log`、`session-independent-rerun.json`：独立重新执行实现者 114 个 NUnit 参数化实例，全通过；另行编写的商店精确供给/RNG 对照、资格重检、动态价、折扣最低价、不补货通过。首次备份修复后又实证暴露异常过滤缺陷。
- **最终 `run-05.log`、`review-results.json`：14 组通过，0 失败。** 包含全部上述核心/交易检查和 114 个 Session NUnit 实例，以及两条备份保留路径、checksum 失败、schema 不兼容、语义损坏的有效备份回退、两档都坏时无异常且提供提示。`test-source-manifest.json` 固定审查宿主、作者测试及输入文件哈希。
- `shop-oracle-seed4321.json`：独立 Python 按规格构造首店 oracle。检查 12 个供给的顺序/ID、两骰的全部面和报价，以及最终 shopRngState=3981651957；未直接引用生产商店算法来生成预期。
- 已机械比对：`P0Config.json` 等于规格附录 A；`GoldenScores.json` 的 cases 等于附录 B，包括全部期望字段，没有改写标准。

重跑命令（工作目录为仓库根）：

```powershell
$env:DOTNET_CLI_HOME = 'E:/agent_workspace/gpt/game2/evidence/review/.dotnet'
$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = '1'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
dotnet run --project evidence/review/ReviewHarness.csproj -- E:/agent_workspace/gpt/game2
```

## 返工记录

### RV-01：有效备份会被损坏的旧活动档覆盖（P1）

发现版本 AtomicSaveStore SHA-256 `aed6b104748c9023978ada604329ae8d114e695c1b8db17ddd4679babeba42a0`，原第 33 行无条件 `File.Replace(temp, active, backup, true)`。

复现：先正常写入两次，损坏 active，Load 成功回退 backup，再保存下一动作；backup 变成损坏字节。即使不先调用 Load，直接保存时也出现。后续 active 再损坏便无有效备份可恢复。违反规格第 22 节“一份活动存档与一份有效备份”。

修复与结论：root 在实际替换前验证旧 active，使用完整 GameSession 不变量检查；损坏 active 归档为 rejected 文件并不替换已有有效 backup。`run-05.log` 中先 Load 和未先 Load 两条入口均通过，且第二次损坏仍成功回退。**RV-01 已关闭。**

### RV-02：InvalidDataException 不被 Load 的 catch 过滤捕获（P1）

发现版本 AtomicSaveStore SHA-256 `e1d555e8b9e15f7a397445a3a6ce78f7be47ae55b845ea14b0ff837a2e9e7661`，第 57 行包装为 InvalidDataException，而第 61 行过滤仅 IOException/ArgumentException/InvalidOperationException。

InvalidDataException 不属于 IOException。损坏活动档会立即向调用者抛出，未尝试有效 backup。原实现的 schema/hash 失败也存在同类遗漏。`run-03.log` 和结果 JSON 保留真实异常栈。

修复与结论：root 将 Load 改为直接处理 TryRead 成功/失败返回值，不再包装并过滤异常；损坏 active 后继续尝试 backup。`run-05.log` 的格式、checksum、schema、语义损坏以及两份都坏路径均通过。**RV-02 已关闭。**

## 仍须由集成验证补齐

- 生产 Unity JsonUtility 的完整存档往返和备份恢复。
- Unity EditMode/PlayMode 真实发现、执行和结果文件核查。
- UI 与命令入口连通、输入忙碌保护、3D 面朝向、三分辨率、压力性能及 Windows Player。
- 非 Debug 完整 Run 的实机过程与人类体验判断。

未发现上述环节的问题，不代表已经执行或通过这些环节。本报告不单独将整体 P0 标为 ACCEPTED。
