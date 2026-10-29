# 骰子构筑 P0

基于用户提供的 v0.4 策划和配置 p0.4.0（save schema 2）的 Windows x64 原型。验收状态请查 ../docs/STATE.md；这里提供操作与复现方法，不以文件存在代替测试通过。

## 运行

完整构建目录为仓库根 Builds/Windows，启动 DiceCraft.exe；需保留旁边的 Data 目录、UnityPlayer.dll 和其他构建文件。构建目录不提交Git，源工程、编辑源、美术导出件和验收证据提交Git。

主菜单可输入0至4294967295的十进制种子，留空随机生成；“继续”恢复最后一次成功写入的操作。“练习”使用独立内存状态，不覆盖正式挑战。

## 操作

1. 选择关卡挑战，进入后点击投掷或按空格。
2. 点击骰子或按1–9、0切换待锁。E确认锁定；确认后本手不能直接解锁。
3. 拖动已锁骰子与已有栏位交换顺序；前n栏保持连续。右键骰子查看永久六面、概率与当前/有效值；详情右下可拖动旋转。
4. Enter提交已锁骰。待锁未处理时，提交、投掷与道具操作会拒绝。
5. 查看逐骰明细或“上一手”冻结快照，确认永久复制/染色的目标与实际效果。
6. 过关后进入商店，按4类标签查看商品；购买前查看价格、槽位和目标资格。出售控制器/未用道具获得1金币，商品不补货。
7. Esc打开设置或关闭弹窗。支持快动画、减少动态、音量与跳过表现。所有结果在动画前写盘；跳过不会重抽。

六关普通目标为180、300、500、800、1200、1800。第2、4、5关可以选高压。所有正式规则以 ../docs/specs/P0_v0.4.md 为准。

## 存档与日志

默认目录为 Unity persistentDataPath（Windows通常是 %USERPROFILE%/AppData/LocalLow/XDwudi/DiceCraft P0）。active-run.json带schema/config和SHA-256；成功写盘保留backup，损坏原件另存rejected副本。读档先验证完整结构及规则不变量，失败回退上一有效备份，双损坏不会覆盖原件。

IO失败会保留同一候选快照，点击重试不会重投、重复扣费或重复奖励。normal-actions.jsonl与debug-actions.jsonl分别记录操作。日志写入失败不应阻断已保存的动作。

## 开发环境

- Unity Editor 6000.6.7f1，Windows Mono x64，D3D11；URP 17.6.0、uGUI/TMP 2.6.0、Input System 1.20.1，精确依赖见 Packages/packages-lock.json。
- Blender 5.2.3 LTS，可编辑源与可重跑导出脚本见 ../art/README.md。
- Unity Pipeline 0.9.1-exp.1只用于开发自动化；游戏玩法不依赖外部服务。
- 中文字体 Noto Sans CJK SC（SIL OFL），许可证随 Assets/Game/Fonts 保存。

用指定Unity打开本目录，加载 Assets/Game/Scenes/Bootstrap.unity 即可Play。重新装配场景使用菜单 DiceCraft/Rebuild Bootstrap Scene；构建使用 DiceCraft/Build Windows。场景生成会替换Bootstrap，手改场景前先提交/备份。

Core/Rules和Core/Session为无Unity依赖的确定性C#。GameApp消费语义命令和冻结快照，DiceTable只呈现结果。配置和52个黄金示例从原规格机械提取；不得以改预期使测试通过。

## 验证

Unity Test Runner中分别运行DiceCraft.EditMode和DiceCraft.PlayModeTests。Editor/TestEvidence自动将NUnit XML保存到 ../evidence/unity。真实Unity报告包括发现数、通过数和失败原因；不将离线.NET编译当作引擎测试。

正常六关自动轨迹：../evidence/runs/normal-win.json，seed 20261032，共97合法命令。该轨迹经过种子搜索，是功能复现，不是玩家胜率证据。

完整播放器回放与180秒压力验证见 [探针说明](Assets/Game/Runtime/Verification/README.md)。普通启动不会启用探针。所有验收启动使用独立目录，不覆盖玩家存档；Debug压力场景不可当作正常通关。

本轮没有真人体验测试和移动端验收结论。验收报告必须同时查源版本/构建哈希，修改使相关旧证据失效。
