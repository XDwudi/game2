# Player 手册页签前置最小修复复核

**限定结论：静态修复合理，真实两项 Editor 专项通过。** 根因是验证器遗漏当前页签前置；未发现为迎合探针而修改产品或降低原断言。新版完整回归和独立 Player 尚未执行通过，原失败保持失败。

审查者 `/root/v07_shop_ui`，只读；未运行 Unity/Player，未改生产、测试或门槛。基线提交 `0f04e51a2add69ca69f44c2d0c7b8425367e7a89`，art0.7.0 / p0.6.0 / schema4。记录与 SHA：`artifacts/cache/v07-player-handbook-independent-review/bindings.json`。

## 根因与修复范围

原真实失败：`evidence/v07/player-0f04e51-1019/strict-full/player-20261101-101931-894e99a9/player-report.json`。Probe `p07-player-art-ui-17`，status=`failed`、exit1、phase=`NormalReplay`，currentStepIndex=2（已执行3/145步），`handbookPureReadChecks=0`，报 `Handbook scatter row is missing.`，不是完整 Player 通过。

源码链条相符：`VerifyV07MenuAndSettings` 经真实 `MenuHandbook` 打开14行牌型，随后点击 `HandbookColors` 核对两个独立颜色条目，再关闭。产品 `GameApp.V05.cs` 的 `handbookColors` 是该 GameApp 实例内的 UI 页签状态，关闭后保留；不是写入游戏存档的规则字段。原 `VerifyHandbookPureReads` 第一次打开后直接查“散点”，此时仍处于颜色页。ART_SPEC S14 明确牌型14行与颜色独立页签，不要求每次打开强制重置为牌型页。

本次项目源差异准确限定为两个文件；缓存原字节逐一等于基线 Git blob：

- `PlayerVerification.cs`：ProbeRevision17→18；首轮读取前增加两行注释及 `yield return PressTargetReady("HandbookHands")`。使用既有真实可交互按钮路径，没有反射强改页签、写 Session、自动补造牌型行或改产品默认页。
- `V05InterfacePlayModeTests.cs`：既有20次纯读测试先真实打开 Colors，断言 `HandbookRow_all_red` 存在，关闭后每轮点击 Hands，再执行原牌型与只读断言。原20次、14行、八豹子存在、各等级、pending、完整 state、磁盘字节与键盘模态输入断言保留；增加的是跨页签前置覆盖。

对 Player 文件的归一化完整差异已验证只有上述修改。**95 条原 Require 语句逐项相同**，20次循环与颜色页回切未删，三尺寸、滚动/真实命令、原生显示、10秒预热/180秒采样/P95≤16.7ms等未改。Core、GameApp产品界面、规则配置与标准均无修改。

## 真实专项及绑定

`evidence/v07/unity/nunit-20261101-102406.xml`：**2/2、0失败、0跳过、4.9598803秒**。

| 原具名用例 | 结果 | 实际时长 |
|---|---|---:|
| PLAY_V05_HandbookChangesWithCapacitySimpleStraightUpgradeAndReload | Passed | 3.393238秒 |
| PLAY_V05_HandbookTwentyReadsPreservePendingSaveAndAllRuleState | Passed | 1.524710秒 |

报告 SHA 与 binding/归档一致。实际起止 UTC10:24:01.2447513–10:24:06.7895221，1,360源文件起止相同，5程序集 MVID/DLL 起止相同；UTC10:27:40.710270 独立再读当前磁盘也无差异。该当前比较只对该时点有效。专项 Runtime MVID=`dea6b318-5db4-4f86-9b94-c1dad2e85dca`，没有冒用旧构建MVID。

归档 `artifacts/cache/v07-player-handbook-rework-102406` 的两隔离目录、6个文件逐SHA核验，**0 PNG**，本记录不声称实图复核。专项验证了改动后的真实 UI/规则/存档行为，但未执行修改后的完整 Player 探针。

原失败报告未改写；旧 Player/其构建保全由root另行记录，本审查未全量复算其606文件复制，不代签。下一源检查点的完整回归、重建、新 Player完整145步/调试24关/显示/180秒性能，以及Mac实机和整版验收仍待对应真实结果。
