# 骰子构筑 P0

基于用户提供的 v0.5 策划和配置 p0.5.0（save schema 3）的骰子构筑原型，目标 Windows x64 与 macOS Universal。验收状态请查 ../docs/STATE.md；这里提供操作与复现方法，不以文件存在代替测试通过。

## 运行

v0.5 输出为仓库根 `Builds/v05/Windows/DicePrototype.exe` 与 `Builds/v05/macOS/DicePrototype.app`；Windows须保留 Data 目录、UnityPlayer.dll 和全部运行文件。双平台完整压缩包使用 Git LFS 存入 `Releases/p0.5.0/`。原始Builds缓存不提交。Mac原生实测、签名与Intel运行状态以该版本清单为准。

主菜单可输入0至4294967295的十进制种子，留空随机生成；“继续”恢复最后一次成功写入的操作。“练习”使用独立内存状态，不覆盖正式挑战。

## 操作

1. 选择关卡挑战，进入后点击投掷或按空格。
2. 点击骰子或按1–9、0切换待锁。E确认锁定；确认后本手不能直接解锁。
3. 拖动已锁骰子与已有栏位交换顺序；前n栏保持连续。右键骰子查看永久六面、概率与当前/有效值；详情右下可拖动旋转。
4. Enter提交已锁骰。待锁未处理时，提交、投掷与道具操作会拒绝。
5. 查看逐骰明细或“上一手”冻结快照，确认永久复制/染色的目标与实际效果。
6. 达标且仍有提交次数时可以继续；“结束本轮”结算奖励后进入商店。待锁选择未处理或动画忙碌时不能结束。最后一关结束后进入整局结算。
7. 商店按4类标签查看商品；购买前显示附魔、普通占槽、总持有数与替换净价。出售控制器/未用道具获得1金币。买骰不改变提交容量K；C21/C22改变K但不增加骰子。
8. H或右上“牌型手册”显示全部13种牌型、当前升级次数、倍率和可达条件。Esc打开设置或关闭弹窗。支持快动画、减少动态、音量与跳过表现。所有结果在动画前写盘；跳过不会重抽。

共24关，第2、5、8、11、14、17、20、23关可选高压。正式规则与全部40个控制器以 ../docs/specs/P0_v0.5.md 为准。

## 存档与日志

默认目录为 Unity persistentDataPath（Windows通常是 `%USERPROFILE%/AppData/LocalLow/XDwudi/DiceCraft P0`；Mac通常在 `~/Library/Application Support/XDwudi/DiceCraft P0`，实际路径可在暂停页查看）。active-run.json带schema/config和SHA-256；成功写盘保留backup，损坏原件另存rejected副本。读档先验证完整结构、冻结历史分数及规则不变量，失败回退上一有效备份，双损坏不会覆盖原件。v0.4/schema2 的Run不迁移，原档和备份保留为legacy副本；设置与历史成绩不清空。

IO失败会保留同一候选快照，点击重试不会重投、重复扣费或重复奖励。normal-actions.jsonl与debug-actions.jsonl分别记录操作。日志写入失败不应阻断已保存的动作。

## 开发环境

- Unity Editor 6000.6.7f1，Windows Mono x64/D3D11，Mac Mono Universal/Metal；URP 17.6.0、uGUI/TMP 2.6.0、Input System 1.20.1，精确依赖见 Packages/packages-lock.json。
- Blender 5.2.3 LTS，可编辑源与可重跑导出脚本见 ../art/README.md。
- Unity Pipeline 0.9.1-exp.1只用于开发自动化；游戏玩法不依赖外部服务。
- 中文字体 Noto Sans CJK SC（SIL OFL），许可证随 Assets/Game/Fonts 保存。

用指定Unity打开本目录，加载 Assets/Game/Scenes/Bootstrap.unity 即可Play。增量导入用 `DiceCraft/Upgrade v0.5 Assets and Profiles`；重新装配会替换Bootstrap，应先提交/备份。构建选择 `Assets/Settings/BuildProfiles/Windows_x64.asset` 或 `macOS_Universal.asset`，两者均用 `DiceCraft/Build Active Verified Profile`。旧Windows菜单也经过同一测试与源哈希门禁。

自动构建须分别启动两个Editor进程，不能并发写同一工程：

```text
Unity -batchmode -projectPath <绝对工程目录> -activeBuildProfile Assets/Settings/BuildProfiles/Windows_x64.asset -executeMethod DiceCraft.Editor.PrototypeBuild.BuildActiveProfile -logFile <绝对日志路径> -quit
```

Mac替换Profile路径为 `Assets/Settings/BuildProfiles/macOS_Universal.asset`。先运行真实Unity EditMode/PlayMode并取得独立PDB源核验；提交源码后使用根目录 `tools/v05_delivery.py identity` 生成包内身份，再用 `gate --edit <NUnit路径> --play <NUnit路径> --pdb <独立报告>` 绑定。资源或源码改变必须重验受影响检查。身份JSON是提交后的派生产物；菜单版本页和包内BUILD_IDENTITY.json应与manifest一致。

Core/Rules和Core/Session为无Unity依赖的确定性C#。GameApp消费语义命令和冻结快照，DiceTable只呈现结果。配置和100个黄金示例（每例26字段）从原规格机械提取；不得以改预期使测试通过。

Windows Player使用通用60Hz帧调度，失败或移除时回退Unity原生60帧设置；失焦/暂停采用粗睡眠，恢复重新建立节拍。它不读取游戏状态或测试开关，不影响RNG、保存和规则。设计、成本及实际测量边界见 ../docs/decisions/P0-FRAME-PACING.md。

## 验证

Unity Test Runner中分别运行DiceCraft.EditMode和DiceCraft.PlayModeTests。Editor/TestEvidence自动将NUnit XML保存到 ../evidence/v05/unity。真实Unity报告包括发现数、通过数和失败原因；不将离线.NET编译当作引擎测试。

v0.5正常自动轨迹：`../evidence/v05/session/normal-run-trace.json`，seed 20261037，共161合法命令，真实预期为第9关失败。轨迹经过种子搜索，只作功能复现，不是24关正常通关或玩家胜率证据。独立Debug24初态用真实命令覆盖24关、8次高压、23个商店与终局。

完整播放器回放与180秒压力验证见 `../evidence/v05/session/PLAYER_PROBE.md`。使用 `--verify-p0 <轨迹绝对路径> --evidence-dir <输出目录>`；跨平台读档额外传 `--verify-import-save <另一平台cross-platform-checkpoint目录>`。普通启动不会启用探针。所有验收启动使用独立目录，不覆盖玩家存档；Debug压力场景不可当作正常通关。Mac脚本见 `../tools/macos/VerifyMac.command` 与 `../docs/tasks/P0-V05-MAC-VERIFICATION.md`。

本轮没有真人体验测试和移动端验收结论。验收报告必须同时查源版本/构建哈希，修改使相关旧证据失效。
